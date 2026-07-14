using System.Windows.Input;

namespace VoiceButton.Models;

public sealed record HotkeyGesture(bool Ctrl, bool Alt, bool Shift, bool Win, Key Key)
{
    public string StorageValue => string.Join("+", Parts());

    public string DisplayText => string.Join(" + ", Parts());

    public static HotkeyGesture? FromKeyEvent(System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.ImeProcessed)
        {
            key = e.ImeProcessedKey;
        }

        if (IsModifierKey(key))
        {
            return null;
        }

        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var win = Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);
        if (!ctrl && !alt && !shift && !win)
        {
            return null;
        }

        return new HotkeyGesture(ctrl, alt, shift, win, key);
    }

    public static bool TryParse(string? value, out HotkeyGesture gesture)
    {
        gesture = default!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var ctrl = false;
        var alt = false;
        var shift = false;
        var win = false;
        Key? key = null;
        var converter = new KeyConverter();

        foreach (var rawPart in value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = rawPart.Trim();
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                ctrl = true;
                continue;
            }

            if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                alt = true;
                continue;
            }

            if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                shift = true;
                continue;
            }

            if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                win = true;
                continue;
            }

            try
            {
                key = (Key?)converter.ConvertFromString(part);
            }
            catch
            {
                return false;
            }
        }

        if (key is null || IsModifierKey(key.Value) || (!ctrl && !alt && !shift && !win))
        {
            return false;
        }

        gesture = new HotkeyGesture(ctrl, alt, shift, win, key.Value);
        return true;
    }

    private IEnumerable<string> Parts()
    {
        if (Ctrl)
        {
            yield return "Ctrl";
        }

        if (Alt)
        {
            yield return "Alt";
        }

        if (Shift)
        {
            yield return "Shift";
        }

        if (Win)
        {
            yield return "Win";
        }

        yield return KeyToText(Key);
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;
    }

    private static string KeyToText(Key key)
    {
        if (key >= Key.D0 && key <= Key.D9)
        {
            return ((int)(key - Key.D0)).ToString();
        }

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return "Num " + (int)(key - Key.NumPad0);
        }

        return new KeyConverter().ConvertToString(key) ?? key.ToString();
    }
}
