using System.Diagnostics;
using System.Windows.Automation;

namespace VoiceButton.Services;

public sealed class DictationTextInsertionService(ClipboardService clipboardService)
{
    public DictationTarget CaptureTarget()
    {
        var windowHandle = NativeMethods.GetForegroundWindow();
        var processName = GetProcessName(windowHandle);
        var focusedElement = TryGetFocusedElement();
        var hasVerifiedEditableFocus = HasEditableFocus(focusedElement);
        var shouldAttemptPaste = hasVerifiedEditableFocus
            || IsElementFromWindowProcess(focusedElement, windowHandle);

        return new DictationTarget(
            windowHandle,
            focusedElement,
            shouldAttemptPaste,
            hasVerifiedEditableFocus,
            processName,
            DescribeFocus(focusedElement));
    }

    public async Task<DictationDeliveryResult> DeliverAsync(
        DictationTarget target,
        string text,
        bool insertAutomatically,
        bool restoreClipboardAfterInsert,
        CancellationToken cancellationToken)
    {
        var previousClipboard = clipboardService.Capture();
        await clipboardService.SetTextAsync(text, cancellationToken);

        if (!insertAutomatically || !target.ShouldAttemptPaste || !NativeMethods.IsWindow(target.WindowHandle))
        {
            return DictationDeliveryResult.CopiedToClipboard;
        }

        if (NativeMethods.IsIconic(target.WindowHandle))
        {
            _ = NativeMethods.ShowWindow(target.WindowHandle, NativeMethods.SwRestore);
        }

        _ = NativeMethods.SetForegroundWindow(target.WindowHandle);
        await Task.Delay(80, cancellationToken);
        TryRestoreFocusedElement(target.FocusedElement);
        await Task.Delay(80, cancellationToken);
        if (NativeMethods.GetForegroundWindow() != target.WindowHandle
            || !NativeMethods.SendPasteShortcut())
        {
            return DictationDeliveryResult.CopiedToClipboard;
        }

        await Task.Delay(350, cancellationToken);
        if (restoreClipboardAfterInsert && target.HasVerifiedEditableFocus)
        {
            previousClipboard.Restore();
        }

        return target.HasVerifiedEditableFocus
            ? DictationDeliveryResult.Inserted
            : DictationDeliveryResult.PasteAttempted;
    }

    private static AutomationElement? TryGetFocusedElement()
    {
        try
        {
            return AutomationElement.FocusedElement;
        }
        catch
        {
            return null;
        }
    }

    private static bool HasEditableFocus(AutomationElement? element)
    {
        try
        {
            if (element is null || !element.Current.IsEnabled)
            {
                return false;
            }

            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePattern)
                && valuePattern is ValuePattern value
                && !value.Current.IsReadOnly)
            {
                return true;
            }

            return element.Current.ControlType == ControlType.Edit
                || element.Current.ControlType == ControlType.Document
                || (element.Current.HasKeyboardFocus
                    && element.TryGetCurrentPattern(TextPattern.Pattern, out _));
        }
        catch
        {
            return false;
        }
    }


    private static bool IsElementFromWindowProcess(AutomationElement? element, IntPtr windowHandle)
    {
        if (element is null || windowHandle == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            NativeMethods.GetWindowThreadProcessId(windowHandle, out var windowProcessId);
            return windowProcessId != 0
                && element.Current.ProcessId == windowProcessId
                && element.Current.HasKeyboardFocus;
        }
        catch
        {
            return false;
        }
    }

    private static string GetProcessName(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            NativeMethods.GetWindowThreadProcessId(windowHandle, out var processId);
            if (processId == 0)
            {
                return string.Empty;
            }

            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string DescribeFocus(AutomationElement? element)
    {
        if (element is null)
        {
            return "unavailable";
        }

        try
        {
            var controlType = element.Current.ControlType?.ProgrammaticName ?? "unknown";
            var className = element.Current.ClassName;
            return string.IsNullOrWhiteSpace(className)
                ? controlType
                : $"{controlType}/{className}";
        }
        catch
        {
            return "unavailable";
        }
    }

    private static void TryRestoreFocusedElement(AutomationElement? element)
    {
        if (element is null)
        {
            return;
        }

        try
        {
            element.SetFocus();
        }
        catch
        {
            // Some custom editors preserve focus but do not expose SetFocus.
        }
    }
}

public sealed record DictationTarget(
    IntPtr WindowHandle,
    AutomationElement? FocusedElement,
    bool ShouldAttemptPaste,
    bool HasVerifiedEditableFocus,
    string ProcessName,
    string FocusDescription);

public enum DictationDeliveryResult
{
    Inserted,
    PasteAttempted,
    CopiedToClipboard
}
