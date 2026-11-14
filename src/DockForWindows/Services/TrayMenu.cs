using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DockForWindows.Services;

// The tray icon's menu: the two windows it opens (settings, control center) and quit. Drawn like a Windows 11
// menu (rounded window, roomy rows, a soft rounded highlight) in the system's light or dark app theme, instead of
// the WinForms "professional" gradient.
internal static class TrayMenu
{
    public static ContextMenuStrip Create(Action openSettings, Action openControlCenter, Action exit)
    {
        var menu = new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = false, Padding = new Padding(4, 6, 4, 6), Font = new Font("Microsoft YaHei UI", 9.5f), DropShadowEnabled = true };
        menu.Items.Add(Item("打开设置", openSettings));
        menu.Items.Add(Item("打开控制中心", openControlCenter));
        menu.Items.Add(new ToolStripSeparator { Margin = new Padding(0, 3, 0, 3) });
        menu.Items.Add(Item("退出 DockForWin", exit));
        menu.Opening += (_, _) => menu.Renderer = new Renderer(Dark());
        menu.HandleCreated += (_, _) => { int round = 2; NativeMethods.DwmSetWindowAttribute(menu.Handle, 33, ref round, 4); };
        return menu;
    }
    private static ToolStripMenuItem Item(string text, Action action)
    {
        // Sized by its text (audit W07-05): a fixed 176 px cut "退出 DockForWin" short at 150 % scaling.
        var item = new ToolStripMenuItem(text) { AutoSize = true, Padding = new Padding(10, 7, 22, 7) };
        item.Click += (_, _) => action();
        return item;
    }
    private static bool Dark()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); return key?.GetValue("AppsUseLightTheme") is int light && light == 0; }
        catch { return false; }
    }
    private sealed class Renderer : ToolStripRenderer
    {
        private readonly Color _back, _text, _hover, _line;
        public Renderer(bool dark)
        {
            _back = dark ? Color.FromArgb(44, 44, 46) : Color.FromArgb(249, 249, 251);
            _text = dark ? Color.FromArgb(242, 242, 247) : Color.FromArgb(29, 29, 31);
            _hover = dark ? Color.FromArgb(64, 64, 68) : Color.FromArgb(232, 232, 237);
            _line = dark ? Color.FromArgb(70, 70, 74) : Color.FromArgb(222, 222, 228);
        }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { using var brush = new SolidBrush(_back); e.Graphics.FillRectangle(brush, e.AffectedBounds); }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { using var pen = new Pen(_line); e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1); }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
            using var path = Rounded(r, 5); using var brush = new SolidBrush(_hover); g.FillPath(brush, path);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var bounds = new Rectangle(12, 0, e.Item.Width - 24, e.Item.Height);
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, bounds, _text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(_line); int y = e.Item.Height / 2; e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }
        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var path = new GraphicsPath(); int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90); path.CloseFigure();
            return path;
        }
    }
}
