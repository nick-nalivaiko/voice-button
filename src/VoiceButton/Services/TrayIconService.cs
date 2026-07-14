using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using Forms = System.Windows.Forms;

namespace VoiceButton.Services;

public sealed class TrayIconService : IDisposable
{
    private static readonly Color MenuBackground = Color.FromArgb(6, 17, 32);
    private static readonly Color MenuForeground = Color.FromArgb(232, 241, 255);
    private static readonly Color MenuAccent = Color.FromArgb(55, 208, 244);

    private readonly Icon _icon;
    private readonly Font _menuFont;
    private readonly StyledContextMenuStrip _menu;
    private readonly Forms.NotifyIcon _notifyIcon;

    public TrayIconService(Action show, Action speak, Action stop, Action exit)
    {
        _menuFont = new Font("Segoe UI", 11.25f, FontStyle.Regular, GraphicsUnit.Point);
        _menu = CreateMenu(_menuFont, show, speak, stop, exit);
        _icon = LoadIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = "Voice Button",
            Visible = true,
            ContextMenuStrip = _menu
        };

        _notifyIcon.DoubleClick += (_, _) => show();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _menuFont.Dispose();
        _icon.Dispose();
    }

    private static StyledContextMenuStrip CreateMenu(
        Font font,
        Action show,
        Action speak,
        Action stop,
        Action exit)
    {
        var menu = new StyledContextMenuStrip
        {
            AutoSize = true,
            BackColor = MenuBackground,
            ForeColor = MenuForeground,
            Font = font,
            MinimumSize = new Size(254, 0),
            Padding = new Forms.Padding(6, 10, 6, 10),
            Renderer = new TrayMenuRenderer(),
            ShowCheckMargin = false,
            ShowImageMargin = false
        };

        menu.Items.Add(CreateMenuItem("Показать приложение", show, accent: true));
        menu.Items.Add(CreateMenuItem("Озвучить последний ответ", speak));
        menu.Items.Add(CreateMenuItem("Стоп", stop));
        menu.Items.Add(new Forms.ToolStripSeparator
        {
            AutoSize = false,
            Height = 11,
            Margin = new Forms.Padding(8, 2, 8, 2)
        });
        menu.Items.Add(CreateMenuItem("Выход", exit));
        return menu;
    }

    private static Forms.ToolStripMenuItem CreateMenuItem(string text, Action action, bool accent = false)
    {
        var item = new Forms.ToolStripMenuItem(text)
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            ForeColor = accent ? MenuAccent : MenuForeground,
            Height = 40,
            Padding = new Forms.Padding(16, 0, 16, 0),
            Size = new Size(254, 40),
            TextAlign = ContentAlignment.MiddleLeft
        };
        item.Click += (_, _) => action();
        return item;
    }

    private static Icon LoadIcon()
    {
        var embeddedIcon = TryLoadEmbeddedIcon();
        if (embeddedIcon is not null)
        {
            return embeddedIcon;
        }

        foreach (var candidate in EnumerateTrayIconCandidates())
        {
            if (File.Exists(candidate))
            {
                return new Icon(candidate, 32, 32);
            }
        }

        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            var extracted = Icon.ExtractAssociatedIcon(Environment.ProcessPath);
            if (extracted is not null)
            {
                return extracted;
            }
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    private static Icon? TryLoadEmbeddedIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/VoiceButton;component/Assets/TrayIcon.ico", UriKind.Absolute));
        if (resource?.Stream is null)
        {
            return null;
        }

        using (resource.Stream)
        using (var icon = new Icon(resource.Stream, 32, 32))
        {
            return (Icon)icon.Clone();
        }
    }

    private static IEnumerable<string> EnumerateTrayIconCandidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", "TrayIcon.ico");
        yield return Path.Combine(Environment.CurrentDirectory, "Assets", "TrayIcon.ico");
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Assets", "TrayIcon.ico"));
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath CreateRoundedPath(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var arc = new RectangleF(bounds.Location, new SizeF(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class StyledContextMenuStrip : Forms.ContextMenuStrip
    {
        protected override Forms.Padding DefaultPadding => new(6, 10, 6, 10);

        protected override void OnOpening(CancelEventArgs e)
        {
            base.OnOpening(e);
            UpdateRoundedRegion();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateRoundedRegion();
        }

        private void UpdateRoundedRegion()
        {
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            using var path = CreateRoundedPath(new Rectangle(0, 0, Width, Height), 9);
            var previousRegion = Region;
            Region = new Region(path);
            previousRegion?.Dispose();
        }
    }

    private sealed class TrayMenuRenderer : Forms.ToolStripRenderer
    {
        private static readonly Color Border = Color.FromArgb(30, 150, 255);
        private static readonly Color Hover = Color.FromArgb(14, 44, 77);
        private static readonly Color Pressed = Color.FromArgb(18, 60, 109);
        private static readonly Color Separator = Color.FromArgb(33, 53, 76);

        protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(MenuBackground);
            e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.ToolStrip.Size));
        }

        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            const int radius = 9;
            var right = e.ToolStrip.Width - 1;
            var bottom = e.ToolStrip.Height - 1;
            var bounds = new RectangleF(0.5f, 0.5f, e.ToolStrip.Width - 1f, e.ToolStrip.Height - 1f);
            using var path = CreateRoundedPath(bounds, radius);
            using var pen = new Pen(Border, 1f);
            e.Graphics.DrawPath(pen, path);

            e.Graphics.SmoothingMode = SmoothingMode.None;
            e.Graphics.DrawLine(pen, radius, 0, right - radius, 0);
            e.Graphics.DrawLine(pen, radius, bottom, right - radius, bottom);
            e.Graphics.DrawLine(pen, 0, radius, 0, bottom - radius);
            e.Graphics.DrawLine(pen, right, radius, right, bottom - radius);
        }

        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected && !e.Item.Pressed)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(6, 3, e.Item.Width - 12, e.Item.Height - 6);
            using var path = CreateRoundedPath(bounds, 6);
            using var brush = new SolidBrush(e.Item.Pressed ? Pressed : Hover);
            e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            var color = e.Item.Enabled ? e.Item.ForeColor : Color.FromArgb(104, 124, 148);
            var bounds = new Rectangle(16, 0, e.Item.Width - 32, e.Item.Height);
            Forms.TextRenderer.DrawText(
                e.Graphics,
                e.Text,
                e.TextFont,
                bounds,
                color,
                Forms.TextFormatFlags.Left
                    | Forms.TextFormatFlags.VerticalCenter
                    | Forms.TextFormatFlags.SingleLine
                    | Forms.TextFormatFlags.EndEllipsis
                    | Forms.TextFormatFlags.NoPrefix);
        }

        protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.Height / 2;
            using var pen = new Pen(Separator, 1f);
            e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
        }
    }
}
