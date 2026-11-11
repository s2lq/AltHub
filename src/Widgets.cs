using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AltHub
{
    public static class Tint
    {
        public static Color Shift(Color c, int d)
        {
            return Color.FromArgb(Clamp(c.R + d), Clamp(c.G + d), Clamp(c.B + d));
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        static int Clamp(int v) { return v < 0 ? 0 : (v > 255 ? 255 : v); }
    }

    // Rounded, self-painted button. Sizes itself to its text so nothing is ever cut off.
    public class RoundButton : Button
    {
        public string Kind = "";        // "", "primary", "danger", "nav"
        public bool Active;             // highlighted state of nav / toggle buttons
        public bool LeftAlign;          // left-align the text (sidebar items) instead of centering
        bool hover;
        bool pressed;
        Color curFill, curText, curBorder;   // smoothly animated toward their target each frame
        bool colorsInit;
        System.Windows.Forms.Timer anim;

        public RoundButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = Theme.Base;
            Height = 30;
        }

        public void FitText(int minWidth)
        {
            Size ts = TextRenderer.MeasureText(Text, Font);
            Width = Math.Max(minWidth, ts.Width + 28);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            if (Width < 6 || Height < 6) return;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);

            // ---- target colours for the current state ----
            Color fill = Theme.Panel2;
            Color border = Theme.Border;
            Color text = Theme.Text;
            if (Kind == "primary")
            {
                fill = Theme.Accent;
                border = Theme.Accent;
                text = Color.White;
                if (hover) fill = Tint.Shift(fill, 18);
            }
            else if (Kind == "danger")
            {
                border = Theme.Bad;
                text = Theme.Bad;
                if (hover) fill = Tint.Mix(Theme.Panel2, Theme.Bad, 0.2f);
            }
            else if (Kind == "nav")
            {
                if (Active) { fill = Theme.Accent; text = Color.White; }
                else { fill = hover ? Theme.Panel2 : Theme.Panel; text = hover ? Theme.Text : Theme.Muted; }
                border = fill;
            }
            else if (hover) fill = Tint.Shift(fill, Theme.Dark ? 14 : -10);
            if (pressed) fill = Tint.Shift(fill, -14);
            if (!Enabled) { text = Theme.Muted; fill = Theme.Panel; border = Theme.Border; }

            // ---- ease the displayed colours toward the target (smooth hover / active) ----
            if (!colorsInit) { curFill = fill; curText = text; curBorder = border; colorsInit = true; }
            else
            {
                curFill = Lerp(curFill, fill, 0.28f);
                curText = Lerp(curText, text, 0.34f);
                curBorder = Lerp(curBorder, border, 0.28f);
            }

            using (GraphicsPath path = Shapes.Round(r, Math.Min(10, Height / 2)))
            {
                using (SolidBrush b = new SolidBrush(curFill)) g.FillPath(b, path);
                if (Diff(curBorder, curFill) > 6)
                    using (Pen pen = new Pen(curBorder)) g.DrawPath(pen, path);
            }
            Rectangle tr = r;
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
            if (LeftAlign) { tr = new Rectangle(r.Left + 12, r.Top, r.Width - 16, r.Height); flags |= TextFormatFlags.Left; }
            else flags |= TextFormatFlags.HorizontalCenter;
            TextRenderer.DrawText(g, Text, Font, tr, curText, flags);

            // keep animating until the colours have settled, then stop the timer
            if (Diff(curFill, fill) + Diff(curText, text) + Diff(curBorder, border) > 4) RunAnim();
            else if (anim != null) anim.Stop();
        }

        static Color Lerp(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        static int Diff(Color a, Color b)
        {
            return Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
        }

        void RunAnim()
        {
            if (anim == null)
            {
                anim = new System.Windows.Forms.Timer();
                anim.Interval = 16;                         // ~60 fps while a button is transitioning
                anim.Tick += delegate { Invalidate(); };
            }
            if (!anim.Enabled) anim.Start();
        }
    }

    // Combo box that follows the theme (the standard one stays white) with a rounded closed box.
    public class DarkCombo : ComboBox
    {
        bool hover;

        public DarkCombo()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            ItemHeight = 22;
            Font = Theme.Base;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool hot = (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush b = new SolidBrush(hot ? Theme.Accent : Theme.Panel2)) e.Graphics.FillRectangle(b, e.Bounds);
            string text = GetItemText(Items[e.Index]);
            TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle(e.Bounds.Left + 8, e.Bounds.Top, e.Bounds.Width - 12, e.Bounds.Height),
                hot ? Color.White : Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x000F) PaintBox();     // WM_PAINT: draw our own look over the system one
        }

        void PaintBox()
        {
            if (!IsHandleCreated || Width < 24 || Height < 12) return;
            try
            {
                using (Graphics g = Graphics.FromHwnd(Handle))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    bool list = DropDownStyle == ComboBoxStyle.DropDownList;
                    Color outer = Parent != null ? Parent.BackColor : Theme.Panel;
                    Color edge = (Focused || hover) ? Theme.Accent : Theme.Border;
                    Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
                    if (list)
                    {
                        g.Clear(outer);
                        using (GraphicsPath path = Shapes.Round(r, 8))
                        {
                            using (SolidBrush b = new SolidBrush(Theme.Panel2)) g.FillPath(b, path);
                            using (Pen pen = new Pen(edge)) g.DrawPath(pen, path);
                        }
                        TextRenderer.DrawText(g, Text, Font, new Rectangle(10, 0, Width - 34, Height), Enabled ? Theme.Text : Theme.Muted,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    }
                    else
                    {
                        using (SolidBrush b = new SolidBrush(Theme.Panel2)) g.FillRectangle(b, Width - 26, 1, 25, Height - 2);
                        using (Pen pen = new Pen(edge)) g.DrawRectangle(pen, r);
                    }
                    int cx = Width - 16;
                    int cy = Height / 2;
                    using (Pen arrow = new Pen(Theme.Muted, 1.8f))
                        g.DrawLines(arrow, new Point[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
                }
            }
            catch (Exception) { }
        }
    }

    // Panel with softly rounded corners (the big cards of the window).
    public class RoundPanel : Panel
    {
        public int Radius = 14;

        public RoundPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            if (Width < 4 || Height < 4) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Shapes.Round(r, Radius))
            {
                using (SolidBrush b = new SolidBrush(BackColor)) g.FillPath(b, path);
                using (Pen pen = new Pen(Theme.Dark ? Tint.Shift(BackColor, 10) : Theme.Border)) g.DrawPath(pen, path);
            }
        }
    }

    // Text input with rounded border. Wraps a borderless TextBox and forwards the members the app uses.
    public class RoundTextBox : Control
    {
        readonly TextBox tb = new TextBox();
        bool focused;

        public RoundTextBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            tb.BorderStyle = BorderStyle.None;
            tb.Font = Theme.Base;
            Controls.Add(tb);
            Height = 28;
            tb.TextChanged += delegate { OnTextChanged(EventArgs.Empty); };
            tb.KeyDown += delegate(object s, KeyEventArgs e) { OnKeyDown(e); };
            tb.Enter += delegate { focused = true; Invalidate(); OnEnter(EventArgs.Empty); };
            tb.Leave += delegate { focused = false; Invalidate(); OnLeave(EventArgs.Empty); };
            ApplyColors();
            LayoutInner();
        }

        public override string Text
        {
            get { return tb.Text; }
            set { tb.Text = value; }
        }

        public bool UseSystemPasswordChar
        {
            get { return tb.UseSystemPasswordChar; }
            set { tb.UseSystemPasswordChar = value; }
        }

        public bool ReadOnly
        {
            get { return tb.ReadOnly; }
            set { tb.ReadOnly = value; }
        }

        public new bool Focused { get { return tb.Focused; } }

        public void Tip(string text)
        {
            Ui.Tips.SetToolTip(this, text);
            Ui.Tips.SetToolTip(tb, text);
        }

        public void ApplyColors()
        {
            bool inset = (Tag as string) == "inset";
            tb.BackColor = inset ? Theme.Bg : Theme.Panel2;
            tb.ForeColor = Enabled ? Theme.Text : Theme.Muted;
            Invalidate();
        }

        void LayoutInner()
        {
            tb.Left = 10;
            tb.Width = Math.Max(10, Width - 20);
            tb.Top = Math.Max(0, (Height - tb.Height) / 2);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutInner(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); tb.Font = Font; LayoutInner(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); ApplyColors(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); tb.Focus(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            if (Width < 6 || Height < 6) return;
            bool inset = (Tag as string) == "inset";
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Shapes.Round(r, 8))
            {
                using (SolidBrush b = new SolidBrush(inset ? Theme.Bg : Theme.Panel2)) g.FillPath(b, path);
                using (Pen pen = new Pen(focused ? Theme.Accent : Theme.Border)) g.DrawPath(pen, path);
            }
        }
    }

    // Small round "i" next to an option; hovering it shows the long explanation.
    public class InfoIcon : Control
    {
        bool hover;

        public InfoIcon(string tip)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(20, 20);
            Cursor = Cursors.Help;
            Ui.Tips.SetToolTip(this, tip);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            Rectangle r = new Rectangle(1, 1, 17, 17);
            using (SolidBrush b = new SolidBrush(hover ? Theme.Accent : Theme.Panel2)) g.FillEllipse(b, r);
            using (Pen pen = new Pen(hover ? Theme.Accent : Theme.Muted)) g.DrawEllipse(pen, r);
            TextRenderer.DrawText(g, "i", Theme.Bold, new Rectangle(1, 0, 18, 19), hover ? Color.White : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
    }

    // Check box with a rounded box and a tick (the standard one is square and ignores the theme).
    public class RoundCheck : CheckBox
    {
        bool hover;

        public RoundCheck()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }


        public override Size GetPreferredSize(Size proposedSize)
        {
            Size t = TextRenderer.MeasureText(Text, Font, new Size(2000, 100), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            return new Size(25 + t.Width + 6, Math.Max(22, t.Height + 6));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            Rectangle box = new Rectangle(1, (Height - 16) / 2, 16, 16);
            Color edge = Checked ? Theme.Accent : (hover && Enabled ? Theme.Accent : Theme.Border);
            using (GraphicsPath path = Shapes.Round(box, 5))
            {
                using (SolidBrush b = new SolidBrush(Checked ? (Enabled ? Theme.Accent : Theme.Border) : Theme.Panel2)) g.FillPath(b, path);
                using (Pen pen = new Pen(edge)) g.DrawPath(pen, path);
            }
            if (Checked)
                using (Pen tick = new Pen(Color.White, 2f))
                    g.DrawLines(tick, new Point[] { new Point(box.Left + 4, box.Top + 8), new Point(box.Left + 7, box.Top + 11), new Point(box.Left + 12, box.Top + 5) });
            TextRenderer.DrawText(g, Text, Font, new Rectangle(25, 0, Math.Max(1, Width - 25), Height), Enabled ? ForeColor : Theme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    // Round radio button to match.
    public class RoundRadio : RadioButton
    {
        bool hover;

        public RoundRadio()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }


        public override Size GetPreferredSize(Size proposedSize)
        {
            Size t = TextRenderer.MeasureText(Text, Font, new Size(2000, 100), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            return new Size(25 + t.Width + 6, Math.Max(22, t.Height + 6));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            Rectangle box = new Rectangle(1, (Height - 16) / 2, 16, 16);
            Color edge = Checked ? Theme.Accent : (hover && Enabled ? Theme.Accent : Theme.Border);
            using (SolidBrush b = new SolidBrush(Theme.Panel2)) g.FillEllipse(b, box);
            using (Pen pen = new Pen(edge, Checked ? 1.5f : 1f)) g.DrawEllipse(pen, box);
            if (Checked)
                using (SolidBrush dot = new SolidBrush(Enabled ? Theme.Accent : Theme.Border))
                    g.FillEllipse(dot, new Rectangle(box.Left + 4, box.Top + 4, 8, 8));
            TextRenderer.DrawText(g, Text, Font, new Rectangle(25, 0, Math.Max(1, Width - 25), Height), Enabled ? ForeColor : Theme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    // Rounds the corners of controls that cannot paint themselves rounded (lists, log box, preview).
    public static class Rounder
    {
        static readonly ConditionalWeakTable<Control, object> hooked = new ConditionalWeakTable<Control, object>();

        public static void Apply(Control c, int radius)
        {
            object o;
            if (!hooked.TryGetValue(c, out o))
            {
                hooked.Add(c, new object());
                c.Resize += delegate { Set(c, radius); };
            }
            Set(c, radius);
        }

        static void Set(Control c, int radius)
        {
            if (c.Width < 4 || c.Height < 4) return;
            try
            {
                using (GraphicsPath p = Shapes.Round(new Rectangle(0, 0, c.Width, c.Height), radius))
                {
                    Region old = c.Region;
                    c.Region = new Region(p);
                    if (old != null) old.Dispose();
                }
            }
            catch (Exception) { }
        }
    }

    // Gives scrollbars (and spin buttons) the dark / light Windows look instead of the plain grey classic one.
    public static class NativeTheme
    {
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        static readonly ConditionalWeakTable<Control, object> hooked = new ConditionalWeakTable<Control, object>();

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        // Dark / light Windows title bar to match the app (Windows 10 2004+ / 11; ignored on older versions).
        public static void TitleBar(Form f)
        {
            try
            {
                if (!f.IsHandleCreated) return;
                int on = Theme.Dark ? 1 : 0;
                if (DwmSetWindowAttribute(f.Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(f.Handle, 19, ref on, 4);
                int round = 2;      // DWMWCP_ROUND: rounded window corners (Windows 11)
                DwmSetWindowAttribute(f.Handle, 33, ref round, 4);
            }
            catch (Exception) { }
        }

        static bool Wanted(Control c)
        {
            if (c is Form) return false;
            if (c is ListView || c is NumericUpDown) return true;
            TextBox tb = c as TextBox;
            if (tb != null) return tb.Multiline;
            if (c.GetType().Name == "UpDownButtons") return true;
            ScrollableControl sc = c as ScrollableControl;
            return sc != null && sc.AutoScroll;
        }

        public static void Apply(Control c)
        {
            if (!Wanted(c)) return;
            object o;
            if (!hooked.TryGetValue(c, out o))
            {
                hooked.Add(c, new object());
                c.HandleCreated += delegate { Set(c); };
            }
            if (c.IsHandleCreated) Set(c);
        }

        static void Set(Control c)
        {
            try { SetWindowTheme(c.Handle, Theme.Dark ? "DarkMode_Explorer" : "Explorer", null); }
            catch (Exception) { }
        }
    }
}
