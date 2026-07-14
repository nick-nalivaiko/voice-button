using System.Drawing;
using System.IO;
using Forms = System.Windows.Forms;

namespace VoiceButton.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Icon _icon;
    private readonly Forms.NotifyIcon _notifyIcon;

    public TrayIconService(Action show, Action speak, Action stop, Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Показать", null, (_, _) => show());
        menu.Items.Add("Озвучить последний ответ", null, (_, _) => speak());
        menu.Items.Add("Стоп", null, (_, _) => stop());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => exit());

        _icon = LoadIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = "Voice Button",
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => show();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon.Dispose();
    }

    private static Icon LoadIcon()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            var extracted = Icon.ExtractAssociatedIcon(Environment.ProcessPath);
            if (extracted is not null)
            {
                return extracted;
            }
        }

        foreach (var candidate in EnumerateIconCandidates())
        {
            if (File.Exists(candidate))
            {
                return new Icon(candidate);
            }
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    private static IEnumerable<string> EnumerateIconCandidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        yield return Path.Combine(Environment.CurrentDirectory, "Assets", "AppIcon.ico");
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Assets", "AppIcon.ico"));
    }
}
