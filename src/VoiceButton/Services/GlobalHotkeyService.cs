using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace VoiceButton.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int FirstHotkeyId = 0x5642;
    private readonly Dictionary<int, string> _registeredActions = new();
    private HwndSource? _source;
    private IntPtr _handle;

    public event EventHandler<string>? Pressed;

    public bool Register(Window window, IReadOnlyList<GlobalHotkeyRegistration> hotkeys, out string? failedHotkeyLabel)
    {
        failedHotkeyLabel = null;
        EnsureHook(window);
        UnregisterAll();

        for (var index = 0; index < hotkeys.Count; index++)
        {
            var registration = hotkeys[index];
            var hotkeyId = FirstHotkeyId + index;
            var registered = NativeMethods.RegisterHotKey(
                _handle,
                hotkeyId,
                ToNativeModifiers(registration.Gesture) | NativeMethods.ModNoRepeat,
                (uint)KeyInterop.VirtualKeyFromKey(registration.Gesture.Key));

            if (!registered)
            {
                failedHotkeyLabel = registration.Label;
                UnregisterAll();
                return false;
            }

            _registeredActions[hotkeyId] = registration.Id;
        }

        return true;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(WndProc);
        _source = null;
    }

    private void EnsureHook(Window window)
    {
        _handle = new WindowInteropHelper(window).Handle;
        if (_source is not null)
        {
            return;
        }

        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WndProc);
    }

    private void UnregisterAll()
    {
        foreach (var hotkeyId in _registeredActions.Keys.ToArray())
        {
            NativeMethods.UnregisterHotKey(_handle, hotkeyId);
        }

        _registeredActions.Clear();
    }

    private static uint ToNativeModifiers(Models.HotkeyGesture gesture)
    {
        var modifiers = 0u;
        if (gesture.Ctrl)
        {
            modifiers |= NativeMethods.ModControl;
        }

        if (gesture.Alt)
        {
            modifiers |= NativeMethods.ModAlt;
        }

        if (gesture.Shift)
        {
            modifiers |= NativeMethods.ModShift;
        }

        if (gesture.Win)
        {
            modifiers |= NativeMethods.ModWin;
        }

        return modifiers;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmHotkey && _registeredActions.TryGetValue(wParam.ToInt32(), out var actionId))
        {
            handled = true;
            Pressed?.Invoke(this, actionId);
        }

        return IntPtr.Zero;
    }
}
