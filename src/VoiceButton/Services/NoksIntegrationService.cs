using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using VoiceButton.Models;
using WpfRect = System.Windows.Rect;

namespace VoiceButton.Services;

public sealed class NoksIntegrationService(
    CodexWindowFinder windowFinder,
    ClipboardService clipboardService,
    AppSettings settings,
    DiagnosticsLogService diagnosticsLog)
{
    private static readonly Regex AddressedMessagePattern = new(
        @"^\s*(?:(?:hi|hey|hello|привет|привіт)\s+)?(?:noks|nox|knox|nokc|нокс|нокc|ногс|ноксс)(?:\s*[,.:;!\-–—]\s*|\s+)(?<message>.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex TimestampPattern = new(
        @"^(?:read\s+|sent\s+)?\d{1,2}:\d{2}(?:\s*[ap]m)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryExtractAddressedMessage(string transcription, out string message)
    {
        var match = AddressedMessagePattern.Match(transcription ?? string.Empty);
        message = match.Success ? match.Groups["message"].Value.Trim() : string.Empty;
        return !string.IsNullOrWhiteSpace(message);
    }

    public async Task<bool> TrySendAddressedMessageAsync(
        string transcription,
        Action<string, string?> report,
        CancellationToken cancellationToken)
    {
        if (!settings.EnableNoksIntegration
            || !TryExtractAddressedMessage(transcription, out var message))
        {
            return false;
        }

        var window = windowFinder.FindBestWindow(AssistantAppKind.Codex)
            ?? throw new InvalidOperationException("Для отправки сообщения открой Codex и чат Noks.");

        if (NativeMethods.IsIconic(window.Handle))
        {
            _ = NativeMethods.ShowWindow(window.Handle, NativeMethods.SwRestore);
        }

        _ = NativeMethods.SetForegroundWindow(window.Handle);
        await Task.Delay(220, cancellationToken);

        var conversation = FindConversationEntry(window.Element);
        if (conversation is null)
        {
            throw new InvalidOperationException("Не найден чат Noks в боковой панели Codex.");
        }

        report("Noks", "Открываю чат и отправляю сообщение.");
        InvokeOrClick(conversation);
        await Task.Delay(650, cancellationToken);

        var composer = await FindComposerAsync(window.Element, cancellationToken)
            ?? throw new InvalidOperationException("Не найдено поле ввода Noks.");
        FocusOrClick(composer);
        await Task.Delay(120, cancellationToken);

        var previousClipboard = clipboardService.Capture();
        await clipboardService.SetTextAsync(message, cancellationToken);
        if (!NativeMethods.SendPasteShortcut())
        {
            throw new InvalidOperationException("Windows не удалось вставить сообщение в Noks.");
        }

        await Task.Delay(220, cancellationToken);
        if (!NativeMethods.SendEnterKey())
        {
            throw new InvalidOperationException("Windows не удалось отправить сообщение в Noks.");
        }

        if (settings.RestoreClipboardAfterDictation)
        {
            await Task.Delay(260, cancellationToken);
            previousClipboard.Restore();
        }

        diagnosticsLog.Info("Noks dictation routing", $"state=sent, chars={message.Length}");
        return true;
    }

    public async Task<NoksAnswerCapture?> TryCopyLatestAnswerAsync(
        CodexWindow window,
        Action<string, string?> report,
        CancellationToken cancellationToken)
    {
        if (!settings.EnableNoksIntegration || window.AppKind != AssistantAppKind.Codex)
        {
            return null;
        }

        var conversation = FindConversationEntry(window.Element);
        if (conversation is null)
        {
            diagnosticsLog.Info("Noks answer capture", "state=not-applicable, conversation-entry=missing");
            return null;
        }

        var isSelected = IsSelectedConversation(conversation);
        diagnosticsLog.Info(
            "Noks answer capture",
            $"state=selection-check, selected={isSelected}, bounds={SafeBounds(conversation)}");
        if (!isSelected)
        {
            return null;
        }

        report("Noks", "Копирую последний ответ в clipboard.");
        var extraction = ExtractLatestAssistantAnswer(window.Element, conversation);
        if (extraction is null || string.IsNullOrWhiteSpace(extraction.Text))
        {
            diagnosticsLog.Info("Noks answer capture", "state=failed, reason=no-confident-assistant-message");
            throw new InvalidOperationException("Не удалось уверенно выделить последний ответ Noks. Прокрути его в видимую область и попробуй снова.");
        }

        TrySelect(extraction.Container);
        await clipboardService.SetTextAsync(extraction.Text, cancellationToken);
        diagnosticsLog.Info("Noks answer capture", $"state=copied, chars={extraction.Text.Length}");
        return new NoksAnswerCapture(extraction.Text, 1);
    }

    private AutomationElement? FindConversationEntry(AutomationElement root)
    {
        AutomationElementCollection descendants;
        try
        {
            descendants = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
        }
        catch
        {
            return null;
        }

        var expectedName = string.IsNullOrWhiteSpace(settings.NoksConversationName)
            ? "Noks"
            : settings.NoksConversationName.Trim();
        foreach (AutomationElement element in descendants)
        {
            if (!string.Equals(SafeName(element).Trim(), expectedName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var row = FindSelectableAncestor(element) ?? element;
            var bounds = SafeBounds(row);
            var rootBounds = SafeBounds(root);
            if (!bounds.IsEmpty
                && (rootBounds.IsEmpty || bounds.Left < rootBounds.Left + rootBounds.Width * 0.38))
            {
                return row;
            }
        }

        return null;
    }

    private static AutomationElement? FindSelectableAncestor(AutomationElement element)
    {
        var current = element;
        for (var depth = 0; depth < 6; depth++)
        {
            try
            {
                if (current.TryGetCurrentPattern(SelectionItemPattern.Pattern, out _)
                    || current.TryGetCurrentPattern(TogglePattern.Pattern, out _)
                    || current.TryGetCurrentPattern(InvokePattern.Pattern, out _))
                {
                    return current;
                }

                var parent = TreeWalker.ControlViewWalker.GetParent(current);
                if (parent is null)
                {
                    break;
                }

                current = parent;
            }
            catch
            {
                break;
            }
        }

        return null;
    }

    private static bool IsSelectedConversation(AutomationElement element)
    {
        var current = element;
        for (var depth = 0; depth < 6; depth++)
        {
            try
            {
                if (current.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selection)
                    && ((SelectionItemPattern)selection).Current.IsSelected)
                {
                    return true;
                }

                if (current.TryGetCurrentPattern(TogglePattern.Pattern, out var toggle)
                    && ((TogglePattern)toggle).Current.ToggleState == ToggleState.On)
                {
                    return true;
                }

                var stateText = string.Join(" ", current.Current.ItemStatus, current.Current.HelpText, current.Current.AutomationId);
                if (ContainsAny(stateText, "selected", "active", "current", "выбран", "актив", "обрано"))
                {
                    return true;
                }

                var parent = TreeWalker.ControlViewWalker.GetParent(current);
                if (parent is null)
                {
                    break;
                }

                current = parent;
            }
            catch
            {
                break;
            }
        }

        return LooksVisuallySelected(element);
    }

    private static NoksAnswerExtraction? ExtractLatestAssistantAnswer(
        AutomationElement root,
        AutomationElement conversation)
    {
        var rootBounds = SafeBounds(root);
        var conversationBounds = SafeBounds(conversation);
        if (rootBounds.IsEmpty || conversationBounds.IsEmpty)
        {
            return null;
        }

        AutomationElementCollection descendants;
        try
        {
            descendants = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
        }
        catch
        {
            return null;
        }

        var contentLeft = conversationBounds.Right + 18;
        var contentWidth = Math.Max(1, rootBounds.Right - contentLeft);
        var assistantLeftLimit = contentLeft + contentWidth * 0.31;
        var composerTop = FindComposerTop(descendants, rootBounds, contentLeft);
        var groups = new Dictionary<string, NoksMessageGroup>(StringComparer.Ordinal);

        foreach (AutomationElement element in descendants)
        {
            if (SafeControlType(element) != ControlType.Text || !IsLeaf(element))
            {
                continue;
            }

            var text = NormalizeText(SafeName(element));
            var bounds = SafeBounds(element);
            if (!IsUsefulMessageFragment(text, bounds, rootBounds, contentLeft, composerTop))
            {
                continue;
            }

            var container = FindMessageContainer(element, root, rootBounds, contentLeft, composerTop);
            var containerBounds = SafeBounds(container);
            var key = GetRuntimeKey(container);
            if (!groups.TryGetValue(key, out var group))
            {
                group = new NoksMessageGroup(container, containerBounds);
                groups.Add(key, group);
            }

            group.Fragments.Add(new NoksTextFragment(text, bounds));
        }

        var ordered = groups.Values
            .Where(group => group.Fragments.Count > 0)
            .Select(group => group with
            {
                IsAssistant = IsAssistantGroup(group, assistantLeftLimit),
                Text = JoinFragments(group.Fragments)
            })
            .Where(group => !string.IsNullOrWhiteSpace(group.Text))
            .OrderBy(group => group.Bounds.Top)
            .ThenBy(group => group.Bounds.Left)
            .ToList();
        var latestIndex = ordered.FindLastIndex(group => group.IsAssistant);
        if (latestIndex < 0)
        {
            return null;
        }

        var selected = new List<NoksMessageGroup> { ordered[latestIndex] };
        var earliestTop = ordered[latestIndex].Bounds.Top;
        for (var index = latestIndex - 1; index >= 0; index--)
        {
            var group = ordered[index];
            if (!group.IsAssistant)
            {
                break;
            }

            var gap = earliestTop - group.Bounds.Bottom;
            if (gap > 150 || Math.Abs(group.Bounds.Left - ordered[latestIndex].Bounds.Left) > 90)
            {
                break;
            }

            selected.Add(group);
            earliestTop = group.Bounds.Top;
        }

        selected.Reverse();
        var answer = string.Join(
            "\n\n",
            selected.Select(group => group.Text).Where(text => !string.IsNullOrWhiteSpace(text)));
        return string.IsNullOrWhiteSpace(answer)
            ? null
            : new NoksAnswerExtraction(answer.Trim(), selected[0].Container);
    }

    private static double FindComposerTop(
        AutomationElementCollection descendants,
        WpfRect rootBounds,
        double contentLeft)
    {
        var candidates = new List<WpfRect>();
        foreach (AutomationElement element in descendants)
        {
            var type = SafeControlType(element);
            if (type != ControlType.Edit && type != ControlType.Document)
            {
                continue;
            }

            var bounds = SafeBounds(element);
            if (!bounds.IsEmpty
                && bounds.Left > contentLeft
                && bounds.Width > 220
                && bounds.Bottom > rootBounds.Bottom - 240)
            {
                candidates.Add(bounds);
            }
        }

        return candidates.Count == 0 ? rootBounds.Bottom - 72 : candidates.Min(bounds => bounds.Top);
    }

    private static AutomationElement FindMessageContainer(
        AutomationElement element,
        AutomationElement root,
        WpfRect rootBounds,
        double contentLeft,
        double composerTop)
    {
        var contentWidth = Math.Max(1, rootBounds.Right - contentLeft);
        var best = element;
        var current = element;
        for (var depth = 0; depth < 9; depth++)
        {
            AutomationElement? parent;
            try
            {
                parent = TreeWalker.ControlViewWalker.GetParent(current);
            }
            catch
            {
                break;
            }

            if (parent is null || Automation.Compare(parent, root))
            {
                break;
            }

            current = parent;
            var bounds = SafeBounds(parent);
            if (bounds.IsEmpty
                || bounds.Left < contentLeft + 20
                || bounds.Right > rootBounds.Right - 8
                || bounds.Top < rootBounds.Top + 34
                || bounds.Bottom > composerTop - 2)
            {
                continue;
            }

            if (bounds.Width >= 180
                && bounds.Width <= contentWidth * 0.62
                && bounds.Height <= rootBounds.Height * 0.82)
            {
                best = parent;
            }
        }

        return best;
    }

    private static bool IsAssistantGroup(NoksMessageGroup group, double assistantLeftLimit)
    {
        var metadata = SafeSearchText(group.Container);
        if (ContainsAny(metadata, "user message", "message from you", "your message", "сообщение пользователя", "ваше сообщение"))
        {
            return false;
        }

        if (ContainsAny(metadata, "assistant message", "assistant response", "response from", "ответ ассистента", "ответ noks"))
        {
            return true;
        }

        if (LooksLikeUserBubble(group.Bounds))
        {
            return false;
        }

        return group.Bounds.Left <= assistantLeftLimit;
    }

    private static bool LooksVisuallySelected(AutomationElement element)
    {
        var bounds = SafeBounds(element);
        if (bounds.IsEmpty || bounds.Width < 80 || bounds.Height < 18)
        {
            return false;
        }

        var points = new[]
        {
            (X: bounds.Right - 14, Y: bounds.Top + bounds.Height / 2),
            (X: bounds.Left + bounds.Width * 0.72, Y: bounds.Top + bounds.Height / 2)
        };
        foreach (var point in points)
        {
            if (!TrySamplePixel(point.X, point.Y, out var red, out var green, out var blue))
            {
                continue;
            }

            var maximum = Math.Max(red, Math.Max(green, blue));
            var minimum = Math.Min(red, Math.Min(green, blue));
            var luminance = red * 0.2126 + green * 0.7152 + blue * 0.0722;
            if (maximum - minimum <= 24 && luminance >= 42 && luminance <= 105)
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeUserBubble(WpfRect bounds)
    {
        var points = new[]
        {
            (X: bounds.Left - 6, Y: bounds.Top + Math.Min(12, bounds.Height / 2)),
            (X: bounds.Left + 7, Y: bounds.Top + Math.Min(12, bounds.Height / 2)),
            (X: bounds.Left + 7, Y: bounds.Top + bounds.Height / 2)
        };
        foreach (var point in points)
        {
            if (TrySamplePixel(point.X, point.Y, out var red, out var green, out var blue)
                && red > 125
                && green > 85
                && red - blue > 45)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TrySamplePixel(double x, double y, out byte red, out byte green, out byte blue)
    {
        red = 0;
        green = 0;
        blue = 0;
        try
        {
            using var bitmap = new System.Drawing.Bitmap(1, 1);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);
            graphics.CopyFromScreen((int)Math.Round(x), (int)Math.Round(y), 0, 0, new System.Drawing.Size(1, 1));
            var color = bitmap.GetPixel(0, 0);
            red = color.R;
            green = color.G;
            blue = color.B;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string JoinFragments(IReadOnlyList<NoksTextFragment> fragments)
    {
        var ordered = fragments
            .OrderBy(fragment => fragment.Bounds.Top)
            .ThenBy(fragment => fragment.Bounds.Left)
            .ToList();
        var builder = new StringBuilder();
        NoksTextFragment? previous = null;
        foreach (var fragment in ordered)
        {
            if (previous is not null
                && string.Equals(previous.Text, fragment.Text, StringComparison.Ordinal)
                && Math.Abs(previous.Bounds.Top - fragment.Bounds.Top) < 3)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                var gap = previous is null ? 0 : fragment.Bounds.Top - previous.Bounds.Bottom;
                builder.Append(gap > 12 ? "\n\n" : " ");
            }

            builder.Append(fragment.Text);
            previous = fragment;
        }

        return builder.ToString().Trim();
    }

    private static bool IsUsefulMessageFragment(
        string text,
        WpfRect bounds,
        WpfRect rootBounds,
        double contentLeft,
        double composerTop)
    {
        if (string.IsNullOrWhiteSpace(text)
            || bounds.IsEmpty
            || bounds.Width <= 1
            || bounds.Height <= 1
            || bounds.Left <= contentLeft + 20
            || bounds.Right >= rootBounds.Right - 6
            || bounds.Top <= rootBounds.Top + 34
            || bounds.Bottom >= composerTop - 4
            || TimestampPattern.IsMatch(text))
        {
            return false;
        }

        return !ContainsAny(text,
            "new chat", "send a message", "ask anything", "create new chat",
            "новый чат", "отправить сообщение", "создать новый чат",
            "новий чат", "надіслати повідомлення", "створити новий чат");
    }

    private static async Task<AutomationElement?> FindComposerAsync(
        AutomationElement root,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AutomationElementCollection descendants;
            try
            {
                descendants = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
            }
            catch
            {
                return null;
            }

            var rootBounds = SafeBounds(root);
            var candidates = new List<(AutomationElement Element, WpfRect Bounds, string Text)>();
            foreach (AutomationElement element in descendants)
            {
                var type = SafeControlType(element);
                if (type != ControlType.Edit && type != ControlType.Document)
                {
                    continue;
                }

                var bounds = SafeBounds(element);
                if (bounds.IsEmpty || bounds.Width < 220 || bounds.Bottom < rootBounds.Bottom - 260)
                {
                    continue;
                }

                candidates.Add((element, bounds, SafeSearchText(element)));
            }

            var composer = candidates
                .OrderByDescending(candidate => ContainsAny(candidate.Text, "send a message", "message", "сообщение", "повідомлення"))
                .ThenByDescending(candidate => candidate.Bounds.Bottom)
                .ThenByDescending(candidate => candidate.Bounds.Width)
                .Select(candidate => candidate.Element)
                .FirstOrDefault();
            if (composer is not null)
            {
                return composer;
            }

            await Task.Delay(150, cancellationToken);
        }

        return null;
    }

    private static void TrySelect(AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(TextPattern.Pattern, out var textPattern))
            {
                ((TextPattern)textPattern).DocumentRange.Select();
            }
        }
        catch
        {
            // The clipboard still receives the extracted text directly.
        }
    }

    private static bool IsLeaf(AutomationElement element)
    {
        try
        {
            return TreeWalker.ControlViewWalker.GetFirstChild(element) is null;
        }
        catch
        {
            return false;
        }
    }

    private static string GetRuntimeKey(AutomationElement element)
    {
        try
        {
            return string.Join(".", element.GetRuntimeId());
        }
        catch
        {
            var bounds = SafeBounds(element);
            return $"{SafeSearchText(element)}|{bounds.Left:0}|{bounds.Top:0}|{bounds.Width:0}|{bounds.Height:0}";
        }
    }

    private static void InvokeOrClick(AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke))
            {
                ((InvokePattern)invoke).Invoke();
                return;
            }

            if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selection))
            {
                ((SelectionItemPattern)selection).Select();
                return;
            }
        }
        catch
        {
            // Fall back to a coordinate click.
        }

        ClickCenter(element);
    }

    private static void FocusOrClick(AutomationElement element)
    {
        try
        {
            element.SetFocus();
            return;
        }
        catch
        {
            ClickCenter(element);
        }
    }

    private static void ClickCenter(AutomationElement element)
    {
        var bounds = SafeBounds(element);
        if (bounds.IsEmpty)
        {
            throw new InvalidOperationException("Элемент Noks найден без доступных координат.");
        }

        var x = (uint)Math.Round(bounds.Left + bounds.Width / 2);
        var y = (uint)Math.Round(bounds.Top + bounds.Height / 2);
        _ = NativeMethods.SetCursorPos((int)x, (int)y);
        NativeMethods.mouse_event(NativeMethods.MouseEventLeftDown, x, y, 0, UIntPtr.Zero);
        NativeMethods.mouse_event(NativeMethods.MouseEventLeftUp, x, y, 0, UIntPtr.Zero);
    }

    private static string NormalizeText(string text)
    {
        return Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        return needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static string SafeName(AutomationElement element)
    {
        try
        {
            return element.Current.Name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string SafeSearchText(AutomationElement element)
    {
        try
        {
            var current = element.Current;
            return string.Join(" ", current.Name, current.AutomationId, current.HelpText, current.ItemStatus);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static ControlType? SafeControlType(AutomationElement element)
    {
        try
        {
            return element.Current.ControlType;
        }
        catch
        {
            return null;
        }
    }

    private static WpfRect SafeBounds(AutomationElement element)
    {
        try
        {
            return element.Current.BoundingRectangle;
        }
        catch
        {
            return WpfRect.Empty;
        }
    }

    private sealed record NoksTextFragment(string Text, WpfRect Bounds);

    private sealed record NoksMessageGroup(AutomationElement Container, WpfRect Bounds)
    {
        public List<NoksTextFragment> Fragments { get; } = [];

        public bool IsAssistant { get; init; }

        public string Text { get; init; } = string.Empty;
    }

    private sealed record NoksAnswerExtraction(string Text, AutomationElement Container);
}

public sealed record NoksAnswerCapture(string Text, int AnswerCount);
