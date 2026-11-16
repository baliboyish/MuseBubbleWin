using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MuseBubbleWin
{
    internal sealed class ApplePalette
    {
        internal readonly Color Surface, Raised, Ink, Muted, Accent, Border, HeaderTop, HeaderBottom;
        internal ApplePalette(bool light)
        {
            Surface = ColorTranslator.FromHtml(light ? "#F8F8FC" : "#20222D");
            Raised = ColorTranslator.FromHtml(light ? "#FFFFFF" : "#303340");
            Ink = ColorTranslator.FromHtml(light ? "#20212A" : "#F5F5FA");
            Muted = ColorTranslator.FromHtml(light ? "#626574" : "#B6BAC9");
            Accent = ColorTranslator.FromHtml(light ? "#0066E8" : "#6AA5FF");
            Border = ColorTranslator.FromHtml(light ? "#DCDEE8" : "#484C5C");
            HeaderTop = ColorTranslator.FromHtml(light ? "#F6F5FF" : "#3B3D50");
            HeaderBottom = ColorTranslator.FromHtml(light ? "#E9EBF6" : "#2C2F3E");
        }
    }

    internal static class AppleDrawing
    {
        internal static GraphicsPath Rounded(RectangleF rect, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure(); return path;
        }

        internal static Bitmap Orb(int size, bool light)
        {
            var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float s = size;
                for (int i = 9; i >= 1; i--)
                {
                    float inset = s * (0.09f - i * 0.004f);
                    using (var shadow = new SolidBrush(Color.FromArgb(3 + (9 - i), 37, 41, 81)))
                        g.FillEllipse(shadow, inset, inset + s * .035f, s - inset * 2, s - inset * 2);
                }
                var circle = new RectangleF(s * .09f, s * .06f, s * .82f, s * .82f);
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(circle);
                    using (var fill = new PathGradientBrush(path))
                    {
                        fill.CenterPoint = new PointF(s * .39f, s * .29f);
                        fill.CenterColor = Color.FromArgb(239, 244, 255);
                        fill.SurroundColors = new[] { Color.FromArgb(115, 124, 237) };
                        g.FillEllipse(fill, circle);
                    }
                    var state = g.Save(); g.SetClip(path);
                    using (var tint = new LinearGradientBrush(circle, Color.FromArgb(0, 133, 196, 255),
                        Color.FromArgb(135, 225, 154, 248), 50f)) g.FillEllipse(tint, circle);
                    using (var shine = new LinearGradientBrush(circle, Color.FromArgb(160, Color.White),
                        Color.FromArgb(0, Color.White), 90f))
                        g.FillEllipse(shine, s * .14f, s * .07f, s * .68f, s * .47f);
                    g.Restore(state);
                }
                using (var ring = new Pen(Color.FromArgb(light ? 245 : 215, Color.White), Math.Max(1, s * .014f)))
                    g.DrawEllipse(ring, circle);
                float cx = s * .50f, cy = s * .46f, r = s * .235f, waist = r * .22f;
                using (var sparkle = new GraphicsPath())
                {
                    sparkle.AddBezier(cx, cy - r, cx + waist, cy - waist, cx + waist, cy - waist, cx + r, cy);
                    sparkle.AddBezier(cx + r, cy, cx + waist, cy + waist, cx + waist, cy + waist, cx, cy + r);
                    sparkle.AddBezier(cx, cy + r, cx - waist, cy + waist, cx - waist, cy + waist, cx - r, cy);
                    sparkle.AddBezier(cx - r, cy, cx - waist, cy - waist, cx - waist, cy - waist, cx, cy - r);
                    using (var white = new SolidBrush(Color.White)) g.FillPath(white, sparkle);
                }
            }
            return bitmap;
        }

        internal static void RoundWindow(Form form, bool light)
        {
            if (!form.IsHandleCreated || form.Width < 2 || form.Height < 2) return;
            int corners = 2, dark = light ? 0 : 1;
            try
            {
                var margins = new Margins { Left = 1, Right = 1, Top = 1, Bottom = 1 };
                DwmExtendFrameIntoClientArea(form.Handle, ref margins);
                DwmSetWindowAttribute(form.Handle, 20, ref dark, 4);
                if (DwmSetWindowAttribute(form.Handle, 33, ref corners, 4) == 0) return;
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            using (var path = Rounded(new RectangleF(0, 0, form.Width, form.Height), 20 * form.DeviceScale()))
            {
                Region previous = form.Region;
                form.Region = new Region(path);
                if (previous != null) previous.Dispose();
            }
        }

        internal static float DeviceScale(this Control control)
        { using (Graphics g = control.CreateGraphics()) return g.DpiX / 96f; }
        [StructLayout(LayoutKind.Sequential)] struct Margins { public int Left, Right, Top, Bottom; }
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
        [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
        [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
        internal static void Drag(Form form)
        { ReleaseCapture(); SendMessage(form.Handle, 0xA1, new IntPtr(2), IntPtr.Zero); }
    }

    internal sealed class AppleButton : Button
    {
        internal ApplePalette Palette = new ApplePalette(false);
        internal Color Dot = Color.Empty;
        internal string Symbol;
        internal bool Selected;
        bool hover, pressed;
        internal AppleButton(string name, string symbol)
        {
            AccessibleName = name; AccessibleRole = AccessibleRole.PushButton;
            Text = name; Symbol = symbol; Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            BackColor = Palette.Surface;
            SetStyle(ControlStyles.SupportsTransparentBackColor, false);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.SetClip(ClientRectangle, CombineMode.Intersect);
            // Opaque controls must repaint every pixel themselves. Never ask WinForms
            // to replay the parent (and sibling labels) through a transparent button.
            var header = Parent as AppleTitleBar;
            if (header != null)
            {
                var background = new Rectangle(-Left, -Top, Math.Max(1, Parent.Width), Math.Max(1, Parent.Height));
                using (var fill = new LinearGradientBrush(background, Palette.HeaderTop, Palette.HeaderBottom, 25f))
                    g.FillRectangle(fill, ClientRectangle);
            }
            else using (var fill = new SolidBrush(Palette.Surface)) g.FillRectangle(fill, ClientRectangle);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            float scale = Dot.IsEmpty && Symbol == null ? g.DpiX / 96f : Math.Min(Width, Height) / 28f;
            var area = new RectangleF(1, 1, Width - 2, Height - 2);
            using (var path = AppleDrawing.Rounded(area, Math.Min(12 * scale, Height / 2)))
            {
                if (Dot.IsEmpty || hover || Focused)
                    using (var bg = new SolidBrush(Color.FromArgb(pressed ? 65 : hover ? 40 : Selected ? 28 : 0, Palette.Accent)))
                        g.FillPath(bg, path);
                if (Focused) using (var pen = new Pen(Palette.Accent, 2 * scale)) g.DrawPath(pen, path);
            }
            if (!Dot.IsEmpty)
            {
                float d = 12 * scale;
                var dot = new RectangleF((Width - d) / 2, (Height - d) / 2, d, d);
                using (var fill = new SolidBrush(Dot)) g.FillEllipse(fill, dot);
                using (var pen = new Pen(Color.FromArgb(40, Color.Black), scale)) g.DrawEllipse(pen, dot);
                if (hover || Focused)
                    using (var font = new Font(Ui.FontName, 8, FontStyle.Bold))
                    using (var ink = new SolidBrush(Color.FromArgb(85, 40, 30)))
                    using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString(Symbol, font, ink, dot, format);
            }
            else
            {
                if (Symbol != null) { PaintSymbol(g, scale); return; }
                using (var font = new Font(Ui.FontName, 10, FontStyle.Regular))
                using (var ink = new SolidBrush(Selected ? Palette.Accent : Palette.Ink))
                using (var format = new StringFormat { Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(Text, font, ink, new RectangleF(8, 0, Math.Max(1, Width - 16), Height), format);
            }
        }

        void PaintSymbol(Graphics g, float scale)
        {
            float cx = Width / 2f, cy = Height / 2f, r = 5 * scale;
            Color ink = Selected ? Palette.Accent : Palette.Ink;
            using (var pen = new Pen(ink, 1.4f * scale))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                if (Symbol == "\u263c")
                {
                    g.DrawEllipse(pen, cx - 3 * scale, cy - 3 * scale, 6 * scale, 6 * scale);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        g.DrawLine(pen, cx + (float)Math.Cos(a) * r, cy + (float)Math.Sin(a) * r,
                            cx + (float)Math.Cos(a) * r * 1.4f, cy + (float)Math.Sin(a) * r * 1.4f);
                    }
                }
                else if (Symbol == "\u263e")
                {
                    using (var outline = new GraphicsPath())
                    using (var cutout = new GraphicsPath())
                    {
                        outline.AddEllipse(cx - r, cy - r, 2 * r, 2 * r);
                        cutout.AddEllipse(cx - r * .3f, cy - r * 1.3f, 2 * r, 2 * r);
                        using (var region = new Region(outline))
                        using (var fill = new SolidBrush(ink))
                        { region.Exclude(cutout); g.FillRegion(fill, region); }
                    }
                }
                else
                {
                    var state = g.Save(); g.TranslateTransform(cx, cy); g.RotateTransform(35);
                    g.DrawLine(pen, -4 * scale, -6 * scale, 4 * scale, -6 * scale);
                    g.DrawLines(pen, new[] { new PointF(-3 * scale,-6 * scale), new PointF(-3 * scale,-scale),
                        new PointF(-5 * scale,2 * scale), new PointF(5 * scale,2 * scale),
                        new PointF(3 * scale,-scale), new PointF(3 * scale,-6 * scale) });
                    g.DrawLine(pen, 0, 2 * scale, 0, 8 * scale); g.Restore(state);
                }
            }
        }
    }

    internal sealed class AppleTitleBar : Panel
    {
        internal ApplePalette Palette = new ApplePalette(false);
        internal readonly AppleButton CloseButton = new AppleButton("Hide chat", "\u00d7") { Dot = Color.FromArgb(255, 95, 87) };
        internal readonly AppleButton BackButton = new AppleButton("Return to previous app", "\u2212") { Dot = Color.FromArgb(254, 188, 46) };
        internal readonly AppleButton FullButton = new AppleButton("Open Muse in browser", "+") { Dot = Color.FromArgb(40, 200, 64) };
        internal readonly AppleButton ThemeButton = new AppleButton("Switch theme", "\u263c");
        internal readonly AppleButton PinButton = new AppleButton("Keep on top", "\u2197");
        internal readonly Label Title = new Label();
        readonly ToolTip tips = new ToolTip();
        internal AppleTitleBar()
        {
            DoubleBuffered = true; Height = 52;
            Title.Text = "Muse"; Title.TextAlign = ContentAlignment.MiddleCenter;
            Title.Font = new Font(Ui.FontName, 11, FontStyle.Bold); Title.BackColor = Color.Transparent;
            Controls.Add(Title);
            foreach (var button in new[] { CloseButton, BackButton, FullButton, ThemeButton, PinButton })
            { Controls.Add(button); tips.SetToolTip(button, button.AccessibleName); }
            MouseDown += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) AppleDrawing.Drag(FindForm()); };
            Title.MouseDown += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) AppleDrawing.Drag(FindForm()); };
        }
        internal void Apply(bool light, bool pinned)
        {
            Palette = new ApplePalette(light); Title.ForeColor = Palette.Ink;
            ThemeButton.Symbol = light ? "\u263e" : "\u263c";
            ThemeButton.AccessibleName = light ? "Switch to dark theme" : "Switch to light theme";
            tips.SetToolTip(ThemeButton, ThemeButton.AccessibleName);
            PinButton.Selected = pinned;
            PinButton.AccessibleName = pinned ? "Stop keeping on top" : "Keep on top";
            tips.SetToolTip(PinButton, PinButton.AccessibleName);
            foreach (var button in new[] { CloseButton, BackButton, FullButton, ThemeButton, PinButton })
            { button.Palette = Palette; button.Invalidate(); }
            Invalidate();
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (Title == null) return;
            float s = Height / 52f; int target = (int)(28 * s), y = (Height - target) / 2;
            CloseButton.SetBounds((int)(8 * s), y, target, target);
            BackButton.SetBounds((int)(36 * s), y, target, target);
            FullButton.SetBounds((int)(64 * s), y, target, target);
            ThemeButton.SetBounds(Width - (int)(78 * s), y, target, target);
            PinButton.SetBounds(Width - (int)(42 * s), y, target, target);
            int left = (int)(94 * s), right = Width - (int)(88 * s);
            Title.SetBounds(left, 0, Math.Max(0, right - left), Height);
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Width < 1 || Height < 1) return;
            using (var fill = new LinearGradientBrush(ClientRectangle, Palette.HeaderTop, Palette.HeaderBottom, 25f))
                e.Graphics.FillRectangle(fill, ClientRectangle);
            using (var pen = new Pen(Palette.Border)) e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
        protected override void Dispose(bool disposing) { if (disposing) tips.Dispose(); base.Dispose(disposing); }
    }

    internal sealed class AppleMenuRenderer : ToolStripProfessionalRenderer
    {
        internal ApplePalette Palette;
        internal AppleMenuRenderer(bool light) { Palette = new ApplePalette(light); RoundedEdges = true; }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        { using (var fill = new SolidBrush(Palette.Raised)) e.Graphics.FillRectangle(fill, e.AffectedBounds); }
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected) return;
            using (var fill = new SolidBrush(Color.FromArgb(35, Palette.Accent)))
            using (var path = AppleDrawing.Rounded(new RectangleF(3, 1, e.Item.Width - 6, e.Item.Height - 2), 6))
                e.Graphics.FillPath(fill, path);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        { e.TextColor = Palette.Ink; base.OnRenderItemText(e); }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        { using (var pen = new Pen(Palette.Border)) e.Graphics.DrawLine(pen, 12, 3, e.Item.Width - 12, 3); }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        { using (var pen = new Pen(Palette.Border)) e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1); }
    }
}
