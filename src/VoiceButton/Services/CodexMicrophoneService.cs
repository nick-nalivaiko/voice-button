using System.Windows.Automation;
using VoiceButton.Models;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace VoiceButton.Services;

public sealed class CodexMicrophoneService(CodexWindowFinder windowFinder, AppSettings settings)
{
    public async Task<string> StartVoiceInputAsync(Action<string, string?> report, CancellationToken cancellationToken)
    {
        report("Ищу приложение", "Выбираю активное окно Codex или ChatGPT.");
        var window = windowFinder.FindBestWindow()
            ?? throw new InvalidOperationException("Не найдено окно Codex или ChatGPT. Открой нужное приложение и попробуй снова.");

        if (NativeMethods.IsIconic(window.Handle))
        {
            NativeMethods.ShowWindow(window.Handle, NativeMethods.SwRestore);
        }

        NativeMethods.SetForegroundWindow(window.Handle);
        await Task.Delay(220, cancellationToken);

        report($"Микрофон {window.AppName}", "Запускаю голосовой ввод.");
        var microphoneButton = await FindMicrophoneButtonWithHoverAsync(window.Element, cancellationToken)
            ?? throw new InvalidOperationException($"Не нашел кнопку микрофона {window.AppName}. Открой поле ввода и попробуй снова.");

        InvokeOrClick(microphoneButton);
        await Task.Delay(420, cancellationToken);

        if (settings.RetryMicrophoneIfInactive
            && window.AppKind == AssistantAppKind.Codex
            && !LooksVoiceInputActive(window.Element))
        {
            InvokeOrClick(microphoneButton);
            await Task.Delay(220, cancellationToken);
        }

        return window.AppName;
    }

    public async Task<CodexMicrophoneDiagnostics> DiagnoseAsync(CancellationToken cancellationToken, AssistantAppKind? preferredApp = null)
    {
        var window = windowFinder.FindBestWindow(preferredApp);
        if (window is null)
        {
            return new CodexMicrophoneDiagnostics(false, string.Empty, string.Empty, 0, false, false, false);
        }

        var candidates = FindMicrophoneButtonCandidates(window.Element);
        var hoverUsed = false;
        if (candidates.Count == 0)
        {
            await FindMicrophoneButtonWithHoverAsync(window.Element, cancellationToken);
            hoverUsed = true;
            candidates = FindMicrophoneButtonCandidates(window.Element);
        }

        var active = LooksVoiceInputActive(window.Element);
        return new CodexMicrophoneDiagnostics(
            true,
            window.Title,
            window.ProcessName,
            candidates.Count,
            candidates.Count > 0,
            hoverUsed,
            active);
    }

    private static async Task<AutomationElement?> FindMicrophoneButtonWithHoverAsync(AutomationElement root, CancellationToken cancellationToken)
    {
        var button = FindMicrophoneButton(root);
        if (button is not null)
        {
            return button;
        }

        var bounds = SafeBounds(root);
        if (bounds.IsEmpty)
        {
            return null;
        }

        var hoverPoints = new[]
        {
            new WpfPoint(bounds.Right - 80, bounds.Bottom - 52),
            new WpfPoint(bounds.Right - 126, bounds.Bottom - 52),
            new WpfPoint(bounds.Left + bounds.Width * 0.50, bounds.Bottom - 56)
        };

        foreach (var point in hoverPoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            NativeMethods.SetCursorPos((int)Math.Round(point.X), (int)Math.Round(point.Y));
            await Task.Delay(180, cancellationToken);

            button = FindMicrophoneButton(root);
            if (button is not null)
            {
                return button;
            }
        }

        return null;
    }

    private static AutomationElement? FindMicrophoneButton(AutomationElement root)
    {
        return FindMicrophoneButtonCandidates(root)
            .OrderByDescending(candidate => IsDictationButtonText(candidate.Text))
            .ThenByDescending(candidate => candidate.Bounds.Bottom)
            .ThenByDescending(candidate => candidate.Bounds.Right)
            .Select(candidate => candidate.Element)
            .FirstOrDefault();
    }

    private static List<(AutomationElement Element, WpfRect Bounds, string Text)> FindMicrophoneButtonCandidates(AutomationElement root)
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

        var candidates = new List<(AutomationElement Element, WpfRect Bounds, string Text)>();
        foreach (AutomationElement button in buttons)
        {
            var text = SafeSearchText(button);
            if (!IsMicrophoneButtonText(text))
            {
                continue;
            }

            var bounds = SafeBounds(button);
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            {
                continue;
            }

            candidates.Add((button, bounds, text));
        }

        return candidates;
    }

    private static bool LooksVoiceInputActive(AutomationElement root)
    {
        AutomationElementCollection descendants;
        try
        {
            descendants = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
        }
        catch
        {
            return false;
        }

        foreach (AutomationElement element in descendants)
        {
            var text = SafeSearchText(element);
            if (ContainsAny(text,
                    "listening", "recording", "stop voice", "stop dictation", "stop recording",
                    "слушаю", "запись", "идет запись", "остановить запись", "остановить голос"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDictationButtonText(string text)
    {
        return ContainsAny(text,
            "start dictation", "dictate", "dictation",
            "начать диктовку", "диктов", "розпочати диктування");
    }

    private static bool IsMicrophoneButtonText(string text)
    {
        if (ContainsAny(text, "send", "submit", "copy", "attach", "settings", "history", "отправ", "копировать", "влож", "настрой"))
        {
            return false;
        }

        return ContainsAny(text,
            "microphone", "mic", "voice", "dictate", "dictation", "speech", "audio input",
            "микроф", "голос", "диктов", "речь", "надикт", "говор");
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        return needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
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
            throw new InvalidOperationException("Кнопка микрофона найдена, но у нее нет координат для клика.");
        }

        var x = (uint)Math.Round(bounds.Left + bounds.Width / 2);
        var y = (uint)Math.Round(bounds.Top + bounds.Height / 2);
        NativeMethods.SetCursorPos((int)x, (int)y);
        NativeMethods.mouse_event(NativeMethods.MouseEventLeftDown, x, y, 0, UIntPtr.Zero);
        NativeMethods.mouse_event(NativeMethods.MouseEventLeftUp, x, y, 0, UIntPtr.Zero);
    }

    private static string SafeSearchText(AutomationElement element)
    {
        try
        {
            var current = element.Current;
            return string.Join(" ", current.Name, current.AutomationId, current.HelpText, current.LocalizedControlType);
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
}

public sealed record CodexMicrophoneDiagnostics(
    bool WindowFound,
    string WindowTitle,
    string ProcessName,
    int MicrophoneButtonCount,
    bool MicrophoneButtonFound,
    bool HoverUsed,
    bool VoiceInputLooksActive)
{
    public string WindowLabel => string.IsNullOrWhiteSpace(WindowTitle) ? ProcessName : $"{WindowTitle} ({ProcessName})";

    public string ToLogLine()
    {
        return WindowFound
            ? $"window='{WindowLabel}', micButtons={MicrophoneButtonCount}, micFound={MicrophoneButtonFound}, hoverUsed={HoverUsed}, active={VoiceInputLooksActive}"
            : "window not found";
    }
}
