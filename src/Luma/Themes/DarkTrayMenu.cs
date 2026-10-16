using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Luma.Themes;

// The tray uses WinForms, so WPF resources do not style this separate popup.
internal sealed class DarkTrayMenu : ContextMenuStrip
{
    private readonly Font menuFont = new("Segoe UI", 9.5f);

    public DarkTrayMenu(Func<Color> accent)
    {
        Font = menuFont;
        BackColor = Color.FromArgb(37, 37, 40);
        ForeColor = Color.FromArgb(242, 242, 243);
        Padding = new Padding(5);
        ShowImageMargin = false;
        ShowCheckMargin = false;
        ShowItemToolTips = false;
        Renderer = new DarkRenderer(accent);
        ItemAdded += (_, e) =>
        {
            if (e.Item is not { } item) return;
            if (item is ToolStripSeparator) item.Margin = new Padding(4, 3, 4, 3);
            else item.Padding = new Padding(12, 7, 12, 7);
        };
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) menuFont.Dispose();
    }

    private sealed class DarkRenderer(Func<Color> accent) : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(Color.FromArgb(37, 37, 40));
            e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.ToolStrip.Size));
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Color.FromArgb(70, 70, 78));
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            var bounds = new Rectangle(1, 1, e.Item.Width - 2, e.Item.Height - 2);
            using var path = new GraphicsPath();
            const int diameter = 10;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            var state = e.Graphics.Save();
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var fill = new SolidBrush(Color.FromArgb(62, 62, 70));
            e.Graphics.FillPath(fill, path);
            e.Graphics.SmoothingMode = SmoothingMode.None;
            using var mark = new SolidBrush(accent());
            e.Graphics.FillRectangle(mark, 5, e.Item.Height / 2 - 6, 2, 12);
            e.Graphics.Restore(state);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Color.FromArgb(242, 242, 243) : Color.FromArgb(128, 128, 139);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(Color.FromArgb(65, 65, 72));
            e.Graphics.DrawLine(pen, 7, e.Item.Height / 2, e.Item.Width - 7, e.Item.Height / 2);
        }
    }
}
