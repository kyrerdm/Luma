using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Luma.Themes;

internal enum TrayAction { Settings, Exit, Update }

// The tray is a separate WinForms popup; its metrics and renderer are explicit.
internal sealed class DarkTrayMenu : ContextMenuStrip
{
    private readonly Font menuFont = new("Segoe UI", 9.5f);
    private readonly Font glyphFont = new("Segoe Fluent Icons", 10f);
    private float ScaleFactor => DeviceDpi / 96f;
    private int Scale(int value) => (int)Math.Round(value * ScaleFactor);
    protected override Padding DefaultPadding => new(Scale(6));

    private int ContentWidth
    {
        get
        {
            var width = Scale(208);
            foreach (ToolStripItem item in Items)
                if (item is not ToolStripSeparator)
                    width = Math.Max(width, TextRenderer.MeasureText(item.Text, Font).Width + Scale(55));
            return width;
        }
    }

    // DropDownMenu otherwise derives its width from native text columns and
    // overwrites Padding during layout, ignoring the renderer's icon column.
    public override Size GetPreferredSize(Size proposedSize)
    {
        var height = DefaultPadding.Vertical;
        foreach (ToolStripItem item in Items)
            if (item.Available) height += Scale(item is ToolStripSeparator ? 10 : 34);
        return new Size(ContentWidth + Scale(6) * 2, height);
    }

    public DarkTrayMenu(Func<Color> accent)
    {
        Font = menuFont;
        BackColor = Color.FromArgb(37, 37, 40);
        ForeColor = Color.FromArgb(242, 242, 243);
        ShowImageMargin = false;
        ShowCheckMargin = false;
        ShowItemToolTips = false;
        DoubleBuffered = true;
        Renderer = new DarkRenderer(accent, glyphFont);
        Padding = new Padding(6);
        ItemAdded += (_, _) => ApplyMetrics();
        Opening += (_, _) => { BackColor = Appearance.IsLight ? Color.White : Color.FromArgb(37,37,40); ForeColor = Appearance.IsLight ? Color.FromArgb(32,32,41) : Color.FromArgb(242,242,243); };
    }

    public void AddAction(TrayAction action, string text, EventHandler handler, int? index = null)
    {
        var item = new ToolStripMenuItem(text) { Tag = action, Name = action.ToString() };
        item.Click += handler;
        if (index is int position) Items.Insert(position, item); else Items.Add(item);
    }

    private void ApplyMetrics()
    {
        Padding = DefaultPadding;
        var width = ContentWidth;
        foreach (ToolStripItem item in Items)
        {
            item.Margin = Padding.Empty;
            item.Padding = Padding.Empty;
            if (item is ToolStripSeparator)
            {
                item.AutoSize = false;
                item.Size = new Size(width, Scale(10));
            }
            else
            {
                item.AutoSize = false;
                item.Size = new Size(width, Scale(34));
            }
        }
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyMetrics(); }
    protected override void WndProc(ref Message m) { base.WndProc(ref m); if (m.Msg == 0x02E0) ApplyMetrics(); }
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Width < 20 || Height < 20) return;
        using var path = Rounded(new RectangleF(0, 0, Width, Height), Scale(8));
        var previous = Region;
        Region = new Region(path);
        previous?.Dispose();
    }

    internal Rectangle GlyphArea(ToolStripItem item) => new(item.Bounds.Left + Scale(14), item.Bounds.Top, Scale(20), item.Height);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { menuFont.Dispose(); glyphFont.Dispose(); }
    }

    private static GraphicsPath Rounded(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class DarkRenderer(Func<Color> accent, Font glyphFont) : ToolStripProfessionalRenderer
    {
        private static float Dpi(ToolStrip? owner) => owner is DarkTrayMenu menu ? menu.ScaleFactor : 1;

        private static RectangleF RowBounds(ToolStripItem item, ToolStrip? owner, float scale)
        {
            // The native menu layout trims a text margin from item bounds.
            // Center the visible row inside the popup rather than that column.
            var width = owner?.Width ?? item.Width;
            var inset = Math.Max(6 * scale, Math.Max(item.Bounds.Left, width - item.Bounds.Right));
            return new RectangleF(inset - item.Bounds.Left, 0, width - inset * 2, item.Height);
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(Appearance.IsLight ? Color.White : Color.FromArgb(37, 37, 40));
            e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.ToolStrip.Size));
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            var state = e.Graphics.Save(); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Rounded(new RectangleF(0.5f, 0.5f, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 8 * Dpi(e.ToolStrip));
            using var pen = new Pen(Appearance.IsLight ? Color.FromArgb(214,214,223) : Color.FromArgb(65, 65, 73));
            e.Graphics.DrawPath(pen, path); e.Graphics.Restore(state);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            var scale = Dpi(e.ToolStrip);
            var state = e.Graphics.Save(); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var row = RowBounds(e.Item, e.ToolStrip, scale);
            row.Inflate(0, -scale);
            using var path = Rounded(row, 5 * scale);
            using var fill = new SolidBrush(Appearance.IsLight ? Color.FromArgb(228,228,238) : Color.FromArgb(53, 53, 60));
            e.Graphics.FillPath(fill, path); e.Graphics.Restore(state);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var scale = Dpi(e.ToolStrip);
            var text = new Rectangle((int)Math.Round(43 * scale), 0, e.Item.Width - (int)Math.Round(55 * scale), e.Item.Height);
            var color = e.Item.Enabled ? (Appearance.IsLight ? Color.FromArgb(32,32,41) : Color.FromArgb(242, 242, 243)) : Color.FromArgb(128, 128, 139);
            const TextFormatFlags textFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, text, color, textFlags);
            if (e.Item.Tag is not TrayAction action) return;
            var glyph = action switch { TrayAction.Settings => "\uE713", TrayAction.Update => "\uE895", _ => "\uE8BB" };
            var glyphColor = e.Item.Selected && e.Item.Enabled ? accent() : Color.FromArgb(168, 169, 181);
            var glyphBounds = new Rectangle((int)Math.Round(14 * scale), 0, (int)Math.Round(20 * scale), e.Item.Height);
            TextRenderer.DrawText(e.Graphics, glyph, glyphFont, glyphBounds, glyphColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var scale = Dpi(e.ToolStrip);
            using var pen = new Pen(Color.FromArgb(61, 61, 68));
            var row = RowBounds(e.Item, e.ToolStrip, scale);
            e.Graphics.DrawLine(pen, row.Left + 6 * scale, e.Item.Height / 2f, row.Right - 6 * scale, e.Item.Height / 2f);
        }
    }
}
