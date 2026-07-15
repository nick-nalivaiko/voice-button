using System.Windows.Automation;
using VoiceButton.Models;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace VoiceButton.Services;

public sealed class CodexCopyService(CodexWindowFinder windowFinder, ClipboardService clipboardService, AppSettings settings)
{
    private static readonly TimeSpan ClipboardTimeout = TimeSpan.FromSeconds(4);

    public async Task<string> CopyLastAnswerAsync(Action<string, string?> report, CancellationToken cancellationToken)
    {
        report("Ищу приложение", "Выбираю активное окно Codex или ChatGPT.");
        var window = windowFinder.FindBestWindow()
            ?? throw new InvalidOperationException("Не найдено окно Codex или ChatGPT. Открой нужное приложение и попробуй снова.");

        if (NativeMethods.IsIconic(window.Handle))
        {
            NativeMethods.ShowWindow(window.Handle, NativeMethods.SwRestore);
        }

        NativeMethods.SetForegroundWindow(window.Handle);
        await Task.Delay(250, cancellationToken);

        var previousClipboard = clipboardService.Capture();
        var previousSequence = NativeMethods.GetClipboardSequenceNumber();
        var restoreClipboard = false;

        try
        {
            report($"Копирую из {window.AppName}", string.IsNullOrWhiteSpace(window.Title) ? window.ProcessName : window.Title);
            var search = await FindLatestAnswerCopyButtonAsync(window.Element, settings.HoverToRevealCopyButton, report, cancellationToken);
            var copyButton = search.Button;
            if (copyButton is null)
            {
                if (settings.FallbackToClipboardWhenCopyMissing)
                {
                    var fallbackText = clipboardService.GetText();
                    if (!string.IsNullOrWhiteSpace(fallbackText))
                    {
                        report("Clipboard", $"Copy у ответа {window.AppName} не найден, озвучиваю текущий clipboard.");
                        return fallbackText.Trim();
                    }
                }

                throw new InvalidOperationException($"Не нашел кнопку Copy / Копировать у ответа {window.AppName}. Нажми ее вручную и используй кнопку Озвучить clipboard.");
            }

            InvokeOrClick(copyButton);
            restoreClipboard = true;
            var text = await clipboardService.WaitForChangedTextAsync(previousSequence, ClipboardTimeout, cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException($"{window.AppName} скопировал пустой текст.");
            }

            return text.Trim();
        }
        finally
        {
            if (restoreClipboard && settings.RestoreClipboardAfterCopy)
            {
                previousClipboard.Restore();
            }
        }
    }

    public async Task<CodexCopyDiagnostics> DiagnoseAsync(CancellationToken cancellationToken, AssistantAppKind? preferredApp = null)
    {
        var window = windowFinder.FindBestWindow(preferredApp);
        if (window is null)
        {
            return new CodexCopyDiagnostics(false, string.Empty, string.Empty, 0, false, false);
        }

        var snapshot = FindLatestAnswerCopyButton(window.Element);
        var hoverUsed = false;
        if (snapshot.AllCandidateCount == 0 && settings.HoverToRevealCopyButton)
        {
            await HoverNearLatestMessagesAsync(window.Element, cancellationToken);
            hoverUsed = true;
            snapshot = FindLatestAnswerCopyButton(window.Element);
        }

        return new CodexCopyDiagnostics(
            true,
            window.Title,
            window.ProcessName,
            snapshot.AnswerCandidateCount,
            snapshot.Button is not null,
            hoverUsed);
    }

    private static async Task<CopyButtonSearchSnapshot> FindLatestAnswerCopyButtonAsync(
        AutomationElement root,
        bool allowHover,
        Action<string, string?> report,
        CancellationToken cancellationToken)
    {
        var snapshot = FindLatestAnswerCopyButton(root);
        if (snapshot.Button is not null || !allowHover)
        {
            return snapshot;
        }

        report("Ищу Copy ответа", "Игнорирую Copy у твоего промпта и ищу последний готовый ответ.");
        await HoverNearLatestMessagesAsync(root, cancellationToken);
        return FindLatestAnswerCopyButton(root);
    }

    private static CopyButtonSearchSnapshot FindLatestAnswerCopyButton(AutomationElement root)
    {
        var allCandidates = FindCopyButtonCandidates(root);
        if (allCandidates.Count == 0)
        {
            return new CopyButtonSearchSnapshot(null, 0, 0);
        }

        var explicitAnswerCandidates = allCandidates
            .Where(candidate => IsExplicitAnswerCopyButton(candidate.Name))
            .OrderByDescending(candidate => candidate.Bounds.Bottom)
            .ThenByDescending(candidate => candidate.Bounds.Right)
            .ToList();

        var rootBounds = SafeBounds(root);
        var layout = BuildCopyButtonLayout(rootBounds, allCandidates);
        var answerCandidates = explicitAnswerCandidates.Count > 0
            ? explicitAnswerCandidates
            : allCandidates
                .Where(candidate => IsLikelyAnswerCopyButton(candidate, layout))
                .OrderByDescending(candidate => candidate.Bounds.Bottom)
                .ThenByDescending(candidate => candidate.Bounds.Right)
                .ToList();

        return new CopyButtonSearchSnapshot(
            answerCandidates.FirstOrDefault()?.Element,
            allCandidates.Count,
            answerCandidates.Count);
    }

    private static async Task HoverNearLatestMessagesAsync(AutomationElement root, CancellationToken cancellationToken)
    {
        var bounds = SafeBounds(root);
        if (bounds.IsEmpty)
        {
            return;
        }

        var hoverPoints = new[]
        {
            new WpfPoint(bounds.Left + bounds.Width * 0.28, bounds.Bottom - 150),
            new WpfPoint(bounds.Left + bounds.Width * 0.30, bounds.Bottom - 250),
            new WpfPoint(bounds.Left + bounds.Width * 0.42, bounds.Bottom - 180),
            new WpfPoint(bounds.Left + bounds.Width * 0.50, bounds.Bottom - 220)
        };

        foreach (var point in hoverPoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            NativeMethods.SetCursorPos((int)Math.Round(point.X), (int)Math.Round(point.Y));
            await Task.Delay(180, cancellationToken);
        }
    }

    private static List<CopyButtonCandidate> FindCopyButtonCandidates(AutomationElement root)
    {
        AutomationElementCollection buttons;
        try
        {
            buttons = root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
        }
        catch
        {
            return [];
        }

        var candidates = new List<CopyButtonCandidate>();
        foreach (AutomationElement button in buttons)
        {
            var name = SafeName(button);
            if (!IsCopyButtonName(name) || IsCodeCopyButtonName(name))
            {
                continue;
            }

            var bounds = SafeBounds(button);
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            {
                continue;
            }

            candidates.Add(new CopyButtonCandidate(button, bounds, name));
        }

        return candidates;
    }

    private static bool IsCopyButtonName(string name)
    {
        var normalized = name.Trim();
        return IsGenericCopyButtonName(normalized)
            || normalized.StartsWith("copy ", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("копировать ", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("копіювати ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCodeCopyButtonName(string name)
    {
        return name.Contains("copy code", StringComparison.OrdinalIgnoreCase)
            || name.Contains("copy table", StringComparison.OrdinalIgnoreCase)
            || name.Contains("copy link", StringComparison.OrdinalIgnoreCase)
            || name.Contains("copy citation", StringComparison.OrdinalIgnoreCase)
            || name.Contains("код", StringComparison.OrdinalIgnoreCase)
            || name.Contains("code", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExplicitAnswerCopyButton(string name)
    {
        return name.Contains("copy response", StringComparison.OrdinalIgnoreCase)
            || name.Contains("copy answer", StringComparison.OrdinalIgnoreCase)
            || name.Contains("копировать ответ", StringComparison.OrdinalIgnoreCase)
            || name.Contains("копіювати відповідь", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyAnswerCopyButton(CopyButtonCandidate candidate, CopyButtonLayout layout)
    {
        if (IsLikelyPromptCopyButton(candidate, layout))
        {
            return false;
        }

        return IsLeftSideCandidate(candidate.Bounds, layout)
            || candidate.Name.Contains("answer", StringComparison.OrdinalIgnoreCase)
            || candidate.Name.Contains("response", StringComparison.OrdinalIgnoreCase)
            || candidate.Name.Contains("ответ", StringComparison.OrdinalIgnoreCase)
            || candidate.Name.Contains("відповід", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyPromptCopyButton(CopyButtonCandidate candidate, CopyButtonLayout layout)
    {
        if (IsPromptCopyButtonName(candidate.Name))
        {
            return true;
        }

        if (!IsRightSideCandidate(candidate.Bounds, layout))
        {
            return false;
        }

        return IsGenericCopyButtonName(candidate.Name);
    }

    private static bool IsPromptCopyButtonName(string name)
    {
        return name.Contains("copy message", StringComparison.OrdinalIgnoreCase)
            || name.Contains("copy user message", StringComparison.OrdinalIgnoreCase)
            || name.Contains("копировать сообщение", StringComparison.OrdinalIgnoreCase)
            || name.Contains("копіювати повідомлення", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGenericCopyButtonName(string name)
    {
        return string.Equals(name.Trim(), "Copy", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name.Trim(), "Копировать", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name.Trim(), "Копіювати", StringComparison.OrdinalIgnoreCase);
    }

    private static CopyButtonLayout BuildCopyButtonLayout(WpfRect rootBounds, IReadOnlyList<CopyButtonCandidate> candidates)
    {
        if (!rootBounds.IsEmpty && rootBounds.Width > 0)
        {
            return new CopyButtonLayout(rootBounds.Left + rootBounds.Width * 0.55, true);
        }

        if (candidates.Count >= 2)
        {
            var minCenter = candidates.Min(candidate => candidate.Bounds.Left + candidate.Bounds.Width / 2);
            var maxCenter = candidates.Max(candidate => candidate.Bounds.Left + candidate.Bounds.Width / 2);
            if (maxCenter - minCenter >= 160)
            {
                return new CopyButtonLayout((minCenter + maxCenter) / 2, true);
            }
        }

        return new CopyButtonLayout(0, false);
    }

    private static bool IsLeftSideCandidate(WpfRect bounds, CopyButtonLayout layout)
    {
        if (!layout.HasSplit)
        {
            return true;
        }

        var centerX = bounds.Left + bounds.Width / 2;
        return centerX <= layout.SplitX;
    }

    private static bool IsRightSideCandidate(WpfRect bounds, CopyButtonLayout layout)
    {
        if (!layout.HasSplit)
        {
            return false;
        }

        var centerX = bounds.Left + bounds.Width / 2;
        return centerX > layout.SplitX;
    }

    private static void InvokeOrClick(AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern))
            {
                ((InvokePattern)pattern).Invoke();
                return;
            }
        }
        catch
        {
            // Fall back to coordinate click.
        }

        var bounds = SafeBounds(element);
        if (bounds.IsEmpty)
        {
            throw new InvalidOperationException("Кнопка Copy найдена, но у нее нет координат для клика.");
        }

        var x = (uint)Math.Round(bounds.Left + bounds.Width / 2);
        var y = (uint)Math.Round(bounds.Top + bounds.Height / 2);
        NativeMethods.SetCursorPos((int)x, (int)y);
        NativeMethods.mouse_event(NativeMethods.MouseEventLeftDown, x, y, 0, UIntPtr.Zero);
        NativeMethods.mouse_event(NativeMethods.MouseEventLeftUp, x, y, 0, UIntPtr.Zero);
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

    private sealed record CopyButtonCandidate(AutomationElement Element, WpfRect Bounds, string Name);

    private sealed record CopyButtonLayout(double SplitX, bool HasSplit);

    private sealed record CopyButtonSearchSnapshot(
        AutomationElement? Button,
        int AllCandidateCount,
        int AnswerCandidateCount);
}

public sealed record CodexCopyDiagnostics(
    bool WindowFound,
    string WindowTitle,
    string ProcessName,
    int CopyButtonCount,
    bool CopyButtonFound,
    bool HoverUsed)
{
    public string WindowLabel => string.IsNullOrWhiteSpace(WindowTitle) ? ProcessName : $"{WindowTitle} ({ProcessName})";

    public string ToLogLine()
    {
        return WindowFound
            ? $"window='{WindowLabel}', answerCopyCandidates={CopyButtonCount}, copyFound={CopyButtonFound}, hoverUsed={HoverUsed}"
            : "window not found";
    }
}
