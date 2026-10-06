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
            ?? throw new InvalidOperationException("Для отправки сообщения открой Codex и чат Nox.");

        if (NativeMethods.IsIconic(window.Handle))
        {
            _ = NativeMethods.ShowWindow(window.Handle, NativeMethods.SwRestore);
        }

        _ = NativeMethods.SetForegroundWindow(window.Handle);
        await Task.Delay(220, cancellationToken);

        var conversation = FindConversationEntry(window.Element);
        if (conversation is null)
        {
            throw new InvalidOperationException("Не найден чат Nox в боковой панели Codex.");
        }

        report("Nox", "Открываю чат и отправляю сообщение.");
        InvokeOrClick(conversation);
        await Task.Delay(650, cancellationToken);

        var composer = await FindComposerAsync(window.Element, cancellationToken)
            ?? throw new InvalidOperationException("Не найдено поле ввода Nox.");
        FocusOrClick(composer);
        await Task.Delay(120, cancellationToken);

        var previousClipboard = clipboardService.Capture();
        await clipboardService.SetTextAsync(message, cancellationToken);
        if (!NativeMethods.SendPasteShortcut())
        {
            throw new InvalidOperationException("Windows не удалось вставить сообщение в Nox.");
        }

        await Task.Delay(220, cancellationToken);
        if (!NativeMethods.SendEnterKey())
        {
            throw new InvalidOperationException("Windows не удалось отправить сообщение в Nox.");
        }

        if (settings.RestoreClipboardAfterDictation)
        {
            await Task.Delay(260, cancellationToken);
            previousClipboard.Restore();
        }

        diagnosticsLog.Info("Nox dictation routing", $"state=sent, chars={message.Length}");
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
            diagnosticsLog.Info("Nox answer capture", "state=not-applicable, conversation-entry=missing");
            return null;
        }

        var isSelected = IsSelectedConversation(window.Element, conversation, GetConversationNames());
        diagnosticsLog.Info(
            "Nox answer capture",
            $"state=selection-check, selected={isSelected}, bounds={SafeBounds(conversation)}");
        if (!isSelected)
        {
            return null;
        }

        report("Nox", "Копирую последний ответ в clipboard.");
        var extraction = ExtractLatestAssistantAnswer(window.Element, conversation);
        if (extraction is null || string.IsNullOrWhiteSpace(extraction.Text))
        {
            diagnosticsLog.Info("Nox answer capture", "state=failed, reason=no-confident-assistant-message");
            throw new InvalidOperationException("Не удалось уверенно выделить последний ответ Nox. Прокрути его в видимую область и попробуй снова.");
        }

        TrySelect(extraction.Container);
        await clipboardService.SetTextAsync(extraction.Text, cancellationToken);
        diagnosticsLog.Info("Nox answer capture", $"state=copied, chars={extraction.Text.Length}");
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

        var expectedNames = GetConversationNames();
        var rootBounds = SafeBounds(root);
        foreach (AutomationElement element in descendants)
        {
            if (SafeControlType(element) != ControlType.Button
                || !expectedNames.Contains(SafeName(element).Trim(), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var bounds = SafeBounds(element);
            if (IsSidebarConversationBounds(bounds, rootBounds))
            {
                return element;
            }
        }

        foreach (AutomationElement element in descendants)
        {
            if (!expectedNames.Contains(SafeName(element).Trim(), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var row = FindSelectableAncestor(element) ?? element;
            var bounds = SafeBounds(row);
            if (SafeControlType(row) != ControlType.Document
                && IsSidebarConversationBounds(bounds, rootBounds))
            {
                return row;
            }
        }

        return null;
    }

    private static bool IsSidebarConversationBounds(WpfRect bounds, WpfRect rootBounds)
    {
        return !bounds.IsEmpty
            && bounds.Height <= 96
            && (rootBounds.IsEmpty
                || (bounds.Left < rootBounds.Left + rootBounds.Width * 0.38
                    && bounds.Width <= rootBounds.Width * 0.42));
    }

    private IReadOnlyList<string> GetConversationNames()
    {
        return new[]
            {
                settings.NoksConversationName?.Trim(),
                "Nox",
                "Noks"
            }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

    private static bool IsSelectedConversation(
        AutomationElement root,
        AutomationElement element,
        IReadOnlyList<string> conversationNames)
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

        if (IsNamedConversationDocument(root, conversationNames))
        {
            return true;
        }

        return false;
    }

    private static bool IsNamedConversationDocument(
        AutomationElement root,
        IReadOnlyList<string> conversationNames)
    {
        try
        {
            if (root.Current.ControlType == ControlType.Document
                && conversationNames.Contains(SafeName(root).Trim(), StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            var documents = root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
            foreach (AutomationElement document in documents)
            {
                if (conversationNames.Contains(SafeName(document).Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Accessibility state was unavailable; do not guess from content or colors.
        }

        return false;
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

        var paneBounds = FindThreadPaneBounds(descendants, rootBounds, conversationBounds.Right);
        var contentBounds = paneBounds.IsEmpty
            ? new WpfRect(conversationBounds.Right + 18, rootBounds.Top, Math.Max(1, rootBounds.Right - conversationBounds.Right - 18), rootBounds.Height)
            : new WpfRect(paneBounds.Left, rootBounds.Top, paneBounds.Width, rootBounds.Height);
        var contentLeft = contentBounds.Left;
        var contentWidth = Math.Max(1, contentBounds.Width);
        var assistantLeftLimit = contentLeft + contentWidth * 0.31;
        var composerTop = FindComposerTop(descendants, contentBounds);
        var groups = new Dictionary<string, NoksMessageGroup>(StringComparer.Ordinal);

        foreach (AutomationElement element in descendants)
        {
            if (SafeControlType(element) != ControlType.Text || !IsLeaf(element))
            {
                continue;
            }

            var text = NormalizeText(SafeName(element));
            var bounds = SafeBounds(element);
            var container = FindMessageContainer(element, root, contentBounds, composerTop);
            var isMessageRow = ContainsAny(SafeClassName(container), "message-row");
            if (!paneBounds.IsEmpty && !isMessageRow)
            {
                continue;
            }

            if (!IsUsefulMessageFragment(text, bounds, contentBounds, composerTop, isMessageRow))
            {
                continue;
            }

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

    private static WpfRect FindThreadPaneBounds(
        AutomationElementCollection descendants,
        WpfRect rootBounds,
        double sidebarRight)
    {
        var candidates = new List<WpfRect>();
        foreach (AutomationElement element in descendants)
        {
            if (!ContainsAny(SafeClassName(element), "thread-pane"))
            {
                continue;
            }

            var bounds = SafeBounds(element);
            if (!bounds.IsEmpty
                && bounds.Left >= sidebarRight
                && bounds.Width >= 300
                && bounds.Width <= rootBounds.Width * 0.65
                && bounds.Height >= rootBounds.Height * 0.55)
            {
                candidates.Add(bounds);
            }
        }

        return candidates.Count == 0
            ? WpfRect.Empty
            : candidates
                .OrderBy(bounds => bounds.Left)
                .ThenBy(bounds => bounds.Width)
                .First();
    }

    private static double FindComposerTop(
        AutomationElementCollection descendants,
        WpfRect contentBounds)
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
                && bounds.Left >= contentBounds.Left
                && bounds.Right <= contentBounds.Right + 2
                && bounds.Width > 220
                && bounds.Bottom > contentBounds.Bottom - 240)
            {
                candidates.Add(bounds);
            }
        }

        return candidates.Count == 0 ? contentBounds.Bottom - 72 : candidates.Min(bounds => bounds.Top);
    }

    private static AutomationElement FindMessageContainer(
        AutomationElement element,
        AutomationElement root,
        WpfRect contentBounds,
        double composerTop)
    {
        var contentWidth = Math.Max(1, contentBounds.Width);
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
            if (ContainsAny(SafeClassName(current), "message-row"))
            {
                return current;
            }

            var bounds = SafeBounds(parent);
            if (bounds.IsEmpty
                || bounds.Left < contentBounds.Left + 20
                || bounds.Right > contentBounds.Right - 8
                || bounds.Top < contentBounds.Top + 34
                || bounds.Bottom > composerTop - 2)
            {
                continue;
            }

            if (bounds.Width >= 180
                && bounds.Width <= contentWidth * 0.62
                && bounds.Height <= contentBounds.Height * 0.82)
            {
                best = parent;
            }
        }

        return best;
    }

    private static bool IsAssistantGroup(NoksMessageGroup group, double assistantLeftLimit)
    {
        var className = SafeClassName(group.Container);
        if (ContainsAny(className, "message-row self"))
        {
            return false;
        }

        if (ContainsAny(className, "message-row"))
        {
            return true;
        }

        var metadata = SafeSearchText(group.Container);
        if (ContainsAny(metadata, "user message", "message from you", "your message", "сообщение пользователя", "ваше сообщение"))
        {
            return false;
        }

        if (ContainsAny(metadata, "assistant message", "assistant response", "response from", "ответ ассистента", "ответ noks"))
        {
            return true;
        }

        return group.Bounds.Left <= assistantLeftLimit;
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
        WpfRect contentBounds,
        double composerTop,
        bool isMessageRow)
    {
        if (string.IsNullOrWhiteSpace(text)
            || bounds.IsEmpty
            || bounds.Width <= 1
            || bounds.Height <= 1
            || bounds.Left <= contentBounds.Left + 20
            || bounds.Right >= contentBounds.Right - 6
            || (!isMessageRow
                && (bounds.Top <= contentBounds.Top + 34
                    || bounds.Bottom >= composerTop - 4))
            || TimestampPattern.IsMatch(text)
            || string.Equals(text, "Read", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "ChatGPT said:", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "You said:", StringComparison.OrdinalIgnoreCase))
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
            throw new InvalidOperationException("Элемент Nox найден без доступных координат.");
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
            return string.Join(" ", current.Name, current.AutomationId, current.HelpText, current.ItemStatus, current.ClassName);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string SafeClassName(AutomationElement element)
    {
        try
        {
            return element.Current.ClassName ?? string.Empty;
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
