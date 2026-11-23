using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MooThumbnail
{
    internal static class Theme
    {
        internal static readonly Color Background = Color.FromArgb(14, 16, 20), Panel = Color.FromArgb(25, 29, 36),
            Border = Color.FromArgb(49, 55, 66), Text = Color.FromArgb(235, 239, 245), Muted = Color.FromArgb(155, 165, 181),
            Orange = Color.FromArgb(255, 124, 35), Green = Color.FromArgb(144, 216, 173), Red = Color.FromArgb(255, 155, 155);
        internal static GraphicsPath Round(Rectangle rectangle, int radius)
        {
            var path = new GraphicsPath(); int d = radius * 2;
            path.AddArc(rectangle.Left, rectangle.Top, d, d, 180, 90);
            path.AddArc(rectangle.Right - d, rectangle.Top, d, d, 270, 90);
            path.AddArc(rectangle.Right - d, rectangle.Bottom - d, d, d, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
        internal static Label Label(string text, float size, Color color, bool bold = false)
        {
            return new Label { Text = text, ForeColor = color, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, RightToLeft = RightToLeft.Yes, AutoEllipsis = true };
        }
    }
    internal class Card : Panel
    {
        internal Card() { DoubleBuffered = true; BackColor = Theme.Panel; Padding = new Padding(18); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (Width < 10 || Height < 10) return;
            using (var path = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), 12))
            using (var pen = new Pen(Theme.Border)) e.Graphics.DrawPath(pen, path);
        }
    }
    internal sealed class ActionButton : Button
    {
        private readonly bool primary; private bool hover;
        internal ActionButton(string text, bool primary = false)
        {
            this.primary = primary; Text = text; AccessibleName = text; Font = new Font("Segoe UI", 10, FontStyle.Bold);
            ForeColor = primary ? Color.FromArgb(30, 20, 13) : Theme.Text; BackColor = Theme.Panel;
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(1, 1, Width - 3, Height - 3);
            if (bounds.Width < 3 || bounds.Height < 3) return;
            Color first = primary ? Color.FromArgb(255, 161, 77) : hover ? Color.FromArgb(57, 64, 78) : Color.FromArgb(39, 45, 55);
            Color last = primary ? Theme.Orange : first;
            if (!Enabled) first = last = Color.FromArgb(51, 54, 60);
            using (var path = Theme.Round(bounds, 8))
            using (var brush = new LinearGradientBrush(bounds, first, last, 90)) e.Graphics.FillPath(brush, path);
            TextRenderer.DrawText(e.Graphics, Text, Font, bounds, Enabled ? ForeColor : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(bounds, -4, -4), Theme.Orange, first);
        }
    }
    internal sealed class BrandHeader : Panel
    {
        private readonly Image logo;
        internal BrandHeader()
        {
            DoubleBuffered = true;
            using (var stream = typeof(BrandHeader).Assembly.GetManifestResourceStream("MooThumbnail.Logo")) logo = new Bitmap(stream);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = g.DpiX / 96f;
            g.DrawImage(logo, new Rectangle(0, (int)(7 * scale), (int)(58 * scale), (int)(58 * scale)));
            int x = (int)(73 * scale);
            using (var font = new Font("Segoe UI", 22, FontStyle.Bold))
            {
                var bounds = new Rectangle(x, (int)(4 * scale), Width - x, (int)(44 * scale));
                using (var textPath = new GraphicsPath())
                using (var silver = new LinearGradientBrush(bounds, Color.White, Color.FromArgb(119, 131, 149), 90))
                {
                    textPath.AddString("Moo-", font.FontFamily, (int)FontStyle.Bold, g.DpiY * font.Size / 72, new Point(x, (int)(5 * scale)), StringFormat.GenericDefault);
                    using (var pen = new Pen(Color.FromArgb(64, 72, 85), 2)) g.DrawPath(pen, textPath);
                    g.FillPath(silver, textPath);
                }
                float width = g.MeasureString("Moo-", font, 1000, StringFormat.GenericTypographic).Width;
                using (var orange = new SolidBrush(Theme.Orange)) g.DrawString("Thumbnail", font, orange, x + width, 5 * scale);
            }
            using (var font = new Font("Segoe UI", 7.2f))
            using (var muted = new SolidBrush(Theme.Muted)) g.DrawString("YOUTUBE THUMBNAIL DOWNLOADER", font, muted, x + 2, 49 * scale);
            using (var font = new Font("Segoe UI", 9))
            {
                var badge = new Rectangle(Math.Max(x + (int)(310 * scale), Width - (int)(190 * scale)), 12, (int)(185 * scale), (int)(30 * scale));
                TextRenderer.DrawText(g, "Windows  •  v1.1.0", font, badge, Theme.Muted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
        protected override void Dispose(bool disposing) { if (disposing) logo.Dispose(); base.Dispose(disposing); }
    }
    internal sealed class QualityOption : RadioButton
    {
        internal Thumbnail Item;
        internal QualityOption()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Theme.Background; Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            Height = 52; Margin = new Padding(0, 3, 0, 3); Cursor = Cursors.Hand; TabStop = true;
        }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(Theme.Panel); g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Width < 30) return;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.Round(bounds, 7))
            using (var brush = new SolidBrush(Checked ? Color.FromArgb(47, 35, 28) : Theme.Background))
            using (var pen = new Pen(Checked ? Theme.Orange : Theme.Border)) { g.FillPath(brush, path); g.DrawPath(pen, path); }
            Color text = Enabled ? Theme.Text : Theme.Muted;
            using (var pen = new Pen(Checked ? Theme.Orange : Theme.Muted, 1.5f)) g.DrawEllipse(pen, Width - 26, (Height - 13) / 2, 13, 13);
            if (Checked) using (var brush = new SolidBrush(Theme.Orange)) g.FillEllipse(brush, Width - 22, (Height - 5) / 2, 5, 5);
            TextRenderer.DrawText(g, Item == null ? "—" : Item.Quality.Name, Font, new Rectangle(65, 4, Width - 100, Height / 2), text,
                TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
            using (var small = new Font("Segoe UI", 8))
            {
                TextRenderer.DrawText(g, Item != null && Item.Available ? Item.Dimensions : "—", small,
                    new Rectangle(65, Height / 2, Width - 100, Height / 2 - 3), Theme.Muted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                string state = Item == null ? "—" : Item.Available ? "متاحة" : Item.Failure == "network" ? "تعذّر الفحص" : "غير متاحة";
                TextRenderer.DrawText(g, state, small, new Rectangle(6, 0, 65, Height), Item != null && Item.Available ? Theme.Green : Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.RightToLeft);
            }
            if (Focused) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(bounds, -3, -3));
        }
    }
}
