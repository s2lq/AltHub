using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AltHub
{
    public static class Theme
    {
        public static bool Dark = true;
        public static Color Bg, Panel, Panel2, Border, Text, Muted, Sel;
        public static Color Accent = Color.FromArgb(99, 102, 241);
        public static Color Good = Color.FromArgb(52, 168, 100);
        public static Color Bad = Color.FromArgb(226, 70, 70);
        public static Color Warn = Color.FromArgb(235, 160, 30);
        public static Color Info = Color.FromArgb(70, 150, 235);
        public static Color Purple = Color.FromArgb(165, 105, 230);

        public static readonly Font Base = new Font("Segoe UI", 9F);
        public static readonly Font Bold = new Font("Segoe UI", 9F, FontStyle.Bold);
        public static readonly Font Small = new Font("Segoe UI", 8.25F);
        public static readonly Font Big = new Font("Segoe UI Semibold", 15F);
        public static readonly Font Mono = new Font("Consolas", 8.5F);
        public static readonly Font Stat = new Font("Segoe UI Semibold", 13F);
        public static readonly Font CardName = new Font("Segoe UI Semibold", 10.5F);

        static Theme() { Set(true); }

        // "Dark" / "Light" / "System" (follows the Windows app mode) -> dark?
        public static bool ResolveDark(string setting)
        {
            if (setting == "Light") return false;
            if (setting != "System") return true;
            try
            {
                object v = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
                if (v is int) return (int)v == 0;
            }
            catch (Exception) { }
            return true;
        }

        // "#RRGGBB" or "" (default indigo)
        public static void SetAccent(string hex)
        {
            Color c = Color.FromArgb(99, 102, 241);
            try
            {
                string h = (hex ?? "").Trim().TrimStart('#');
                if (h.Length == 6) c = Color.FromArgb(Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16), Convert.ToInt32(h.Substring(4, 2), 16));
            }
            catch (Exception) { }
            Accent = c;
        }

        public static void Set(bool dark)
        {
            Dark = dark;
            if (dark)
            {
                Bg = Color.FromArgb(14, 16, 21);
                Panel = Color.FromArgb(22, 25, 32);
                Panel2 = Color.FromArgb(32, 36, 45);
                Border = Color.FromArgb(45, 50, 62);
                Text = Color.FromArgb(236, 238, 244);
                Muted = Color.FromArgb(138, 146, 163);
                Sel = Color.FromArgb(42, 50, 90);
            }
            else
            {
                Bg = Color.FromArgb(242, 244, 249);
                Panel = Color.White;
                Panel2 = Color.FromArgb(246, 247, 250);
                Border = Color.FromArgb(222, 226, 235);
                Text = Color.FromArgb(28, 31, 38);
                Muted = Color.FromArgb(102, 111, 130);
                Sel = Color.FromArgb(224, 230, 255);
            }
        }

        // Recursively paints a control tree. Controls carry a role in Tag: "card", "muted", "section", "inset", "rows" ...
        public static void Apply(Control c)
        {
            string tag = c.Tag as string;
            if (tag == null) tag = "";
            NativeTheme.Apply(c);

            if (c is Form)
            {
                c.BackColor = Bg;
                c.ForeColor = Text;
                Form fm = (Form)c;
                if (fm.IsHandleCreated) NativeTheme.TitleBar(fm);
                else fm.HandleCreated += delegate { NativeTheme.TitleBar(fm); };
            }
            else if (c is RoundButton || c is GradientButton || c is InfoIcon || c is DarkCombo)
            {
                if (c is DarkCombo)
                {
                    // the typing area of an editable combo (Group, Place ID) is drawn by Windows from these colours
                    c.BackColor = Panel2;
                    c.ForeColor = Text;
                }
                c.Invalidate();                       // these paint themselves from the theme colours
            }
            else if (c is RoundTextBox)
            {
                ((RoundTextBox)c).ApplyColors();
                return;                               // do not restyle the inner text box
            }
            else if (c is ListView)
            {
                c.BackColor = tag == "rows" ? Panel : Panel2;
                c.ForeColor = Text;
                ((ListView)c).BorderStyle = BorderStyle.None;
                Rounder.Apply(c, 8);
                c.Invalidate();
            }
            else if (c is TextBox)
            {
                c.BackColor = tag == "log" ? Panel : Panel2;
                c.ForeColor = Text;
                if (!(c.Parent is NumericUpDown)) ((TextBox)c).BorderStyle = BorderStyle.FixedSingle;
                if (((TextBox)c).Multiline) Rounder.Apply(c, 8);
            }
            else if (c is ComboBox)
            {
                c.BackColor = Panel2;
                c.ForeColor = Text;
                ((ComboBox)c).FlatStyle = FlatStyle.Flat;
            }
            else if (c is NumericUpDown)
            {
                c.BackColor = Panel2;
                c.ForeColor = Text;
                Rounder.Apply(c, 6);
            }
            else if (c is Button)
            {
                Button b = (Button)c;
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 1;
                b.BackColor = Panel2;
                b.ForeColor = Text;
                b.FlatAppearance.BorderColor = Border;
            }
            else if (c is CheckBox || c is RadioButton)
            {
                c.ForeColor = tag == "muted" ? Muted : Text;
                c.BackColor = Color.Transparent;
            }
            else if (c is Label)
            {
                Color fc = Text;
                if (tag == "muted") fc = Muted;
                else if (tag == "section") fc = Accent;
                else if (tag == "good") fc = Good;
                else if (tag == "bad") fc = Bad;
                else if (tag == "warn") fc = Warn;
                c.ForeColor = fc;
                c.BackColor = Color.Transparent;
            }
            else if (c is SplitContainer)
            {
                c.BackColor = Bg;
            }
            else if (c is Panel || c is TableLayoutPanel || c is FlowLayoutPanel)
            {
                if (tag == "card") c.BackColor = Panel;
                else if (tag == "card2") c.BackColor = Panel2;
                else if (tag == "panel2") c.BackColor = Panel2;
                else if (tag == "line") c.BackColor = Border;
                else if (tag == "accent") c.BackColor = Accent;
                else if (tag == "preview") { c.BackColor = Bg; Rounder.Apply(c, 8); }
                else c.BackColor = Bg;
                c.ForeColor = Text;
            }

            foreach (Control child in c.Controls) Apply(child);
        }
    }

    public class BufferedListView : ListView
    {
        public BufferedListView()
        {
            DoubleBuffered = true;
        }
    }

    public class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    public class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Panel; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color MenuItemBorder { get { return Theme.Accent; } }
        public override Color MenuItemSelected { get { return Theme.Sel; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Sel; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Sel; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Panel; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Panel; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Panel; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Border; } }
    }

    // Generic owner-drawn look for the simple lists on the pages.
    public static class ListStyle
    {
        public static void Attach(ListView lv)
        {
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.HideSelection = false;
            lv.OwnerDraw = true;
            lv.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lv.DrawColumnHeader += DrawHeader;
            lv.DrawItem += delegate(object s, DrawListViewItemEventArgs e) { };
            lv.DrawSubItem += DrawSub;
            lv.ClientSizeChanged += delegate { FitLast(lv); };
            lv.HandleCreated += delegate { FitLast(lv); };
        }

        // The last column takes the remaining width, so no light header strip is left on the right.
        static void FitLast(ListView lv)
        {
            if (lv.Columns.Count == 0) return;
            int used = 0;
            for (int i = 0; i < lv.Columns.Count - 1; i++) used += lv.Columns[i].Width;
            int w = lv.ClientSize.Width - used;
            if (w < 40) w = 40;
            ColumnHeader last = lv.Columns[lv.Columns.Count - 1];
            if (last.Width != w) last.Width = w;
        }

        static void DrawHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(Theme.Panel)) e.Graphics.FillRectangle(b, e.Bounds);
            using (Pen p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, Theme.Bold, Rectangle.Inflate(e.Bounds, -6, 0), Theme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        static void DrawSub(object sender, DrawListViewSubItemEventArgs e)
        {
            // clean rows: no zebra stripes, a faint hairline between rows, accent tint on the selection
            Color bg = e.Item.Selected ? Theme.Sel : Theme.Panel;
            using (SolidBrush br = new SolidBrush(bg)) e.Graphics.FillRectangle(br, e.Bounds);
            using (Pen hp = new Pen(Tint.Mix(Theme.Panel, Theme.Border, 0.6f))) e.Graphics.DrawLine(hp, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, Theme.Base, Rectangle.Inflate(e.Bounds, -6, 0), e.Item.ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    // Small factory helpers so the page code stays readable.
    public static class Ui
    {
        public static readonly ToolTip Tips = MakeTips();

        static ToolTip MakeTips()
        {
            ToolTip t = new ToolTip();
            t.OwnerDraw = true;
            t.InitialDelay = 250;
            t.ReshowDelay = 100;
            t.AutoPopDelay = 30000;
            t.ShowAlways = true;
            t.Popup += TipPopup;
            t.Draw += TipDraw;
            return t;
        }

        static void TipPopup(object sender, PopupEventArgs e)
        {
            ToolTip t = (ToolTip)sender;
            string text = e.AssociatedControl == null ? "" : t.GetToolTip(e.AssociatedControl);
            Size sz = TextRenderer.MeasureText(text, Theme.Base, new Size(360, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            e.ToolTipSize = new Size(sz.Width + 26, sz.Height + 20);
        }

        static void TipDraw(object sender, DrawToolTipEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(Theme.Panel2)) e.Graphics.FillRectangle(b, e.Bounds);
            using (Pen p = new Pen(Theme.Accent)) e.Graphics.DrawRectangle(p, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
            TextRenderer.DrawText(e.Graphics, e.ToolTipText, Theme.Base, new Rectangle(13, 10, e.Bounds.Width - 26, e.Bounds.Height - 20), Theme.Text,
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        }

        public static Label Lbl(string text, string tag)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Tag = tag;
            l.Font = tag == "section" ? Theme.Bold : (tag == "muted" ? Theme.Small : (tag == "title" ? Theme.Big : Theme.Base));
            return l;
        }

        // Fixed-width caption in front of a control, so inputs line up.
        public static Label Fld(string text, int width)
        {
            Label l = Lbl(text, "");
            l.AutoSize = false;
            l.Width = width;
            l.Height = 22;
            l.TextAlign = ContentAlignment.MiddleLeft;
            return l;
        }

        public static RoundButton Btn(string text, int width, EventHandler click, string kind)
        {
            RoundButton b = new RoundButton();
            b.Kind = kind ?? "";
            b.Tag = kind ?? "";
            b.Text = text;
            b.FitText(width);
            if (click != null) b.Click += click;
            return b;
        }

        public static RoundTextBox Txt(int width, string text)
        {
            RoundTextBox t = new RoundTextBox();
            t.Width = width;
            t.Text = text ?? "";
            return t;
        }

        public static NumericUpDown Num(int min, int max, int value, int width)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = min;
            n.Maximum = max;
            n.Value = Math.Max(min, Math.Min(max, value));
            n.Width = width;
            n.Font = Theme.Base;
            return n;
        }

        public static CheckBox Chk(string text, bool value, string tip)
        {
            CheckBox c = new RoundCheck();
            c.Text = text;
            c.AutoSize = true;
            c.Checked = value;
            c.Font = Theme.Base;
            if (!string.IsNullOrEmpty(tip)) Tips.SetToolTip(c, tip);
            return c;
        }

        public static RadioButton Rad(string text, bool value, string tip)
        {
            RadioButton r = new RoundRadio();
            r.Text = text;
            r.AutoSize = true;
            r.Checked = value;
            r.Font = Theme.Base;
            if (!string.IsNullOrEmpty(tip)) Tips.SetToolTip(r, tip);
            return r;
        }

        public static DarkCombo Cmb(int width, params string[] items)
        {
            DarkCombo c = new DarkCombo();
            c.Width = width;
            foreach (string s in items) c.Items.Add(s);
            if (c.Items.Count > 0) c.SelectedIndex = 0;
            return c;
        }

        public static ContextMenuStrip Menu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
            m.BackColor = Theme.Panel;
            m.ForeColor = Theme.Text;
            m.Font = Theme.Base;
            return m;
        }

        public static ToolStripMenuItem Item(string text, EventHandler click)
        {
            ToolStripMenuItem i = new ToolStripMenuItem(text);
            i.BackColor = Theme.Panel;
            i.ForeColor = Theme.Text;
            if (click != null) i.Click += click;
            return i;
        }
    }

    // Sequential top-to-bottom layout inside a scrolling panel.
    // Every function gets: its control(s), a round (i) with the long explanation, and one short line below.
    public class PageBuilder
    {
        public readonly Panel Host;
        public int Y = 16;
        public const int PadX = 22;
        public int Width;

        public PageBuilder(Panel host, int width)
        {
            Host = host;
            Width = width;
            host.AutoScroll = true;
        }

        public void Title(string title, string subtitle)
        {
            Label t = Ui.Lbl(title, "title");
            t.Location = new Point(PadX, Y);
            Host.Controls.Add(t);
            Y += 38;
            if (!string.IsNullOrEmpty(subtitle)) Note(subtitle);
        }

        public void Section(string text)
        {
            Y += 20;
            Panel bar = new Panel();          // small accent tab in front of the heading, for a bit of colour
            bar.Tag = "accent";
            bar.Size = new Size(4, 16);
            bar.Location = new Point(PadX, Y + 2);
            Host.Controls.Add(bar);
            Label l = Ui.Lbl(text, "section");
            l.Location = new Point(PadX + 12, Y);
            Host.Controls.Add(l);
            Y += 24;
            Panel line = new Panel();
            line.Tag = "line";
            line.Height = 1;
            line.Width = Width - PadX * 2;
            line.Location = new Point(PadX, Y);
            Host.Controls.Add(line);
            Y += 12;
        }

        // Plain wrapped paragraph (page subtitles and the rare free text).
        public Label Note(string text)
        {
            Label l = Ui.Lbl(text, "muted");
            l.AutoSize = false;
            l.Width = Width - PadX * 2;
            Size sz = TextRenderer.MeasureText(text, Theme.Small, new Size(l.Width, 10000), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            l.Height = sz.Height + 6;
            l.Location = new Point(PadX, Y);
            Host.Controls.Add(l);
            Y += l.Height + 4;
            return l;
        }

        // Places the controls left to right; when a line is full the rest wraps onto the next one, so nothing is
        // ever pushed past the right edge of the page. Returns the height used.
        int PlaceRow(Control[] cs, string tip)
        {
            InfoIcon icon = null;
            if (!string.IsNullOrEmpty(tip)) icon = new InfoIcon(tip);
            int limit = Width - PadX - (icon != null ? 26 : 0);

            List<List<Control>> lines = new List<List<Control>>();
            List<Control> cur = new List<Control>();
            int x = PadX;
            foreach (Control c in cs)
            {
                if (cur.Count > 0 && x + c.Width > limit) { lines.Add(cur); cur = new List<Control>(); x = PadX; }
                cur.Add(c);
                x += c.Width + 10;
            }
            lines.Add(cur);

            int y = Y;
            int endX = PadX;
            int lastH = 0;
            for (int li = 0; li < lines.Count; li++)
            {
                int lh = 0;
                foreach (Control c in lines[li]) lh = Math.Max(lh, c.Height);
                if (li == lines.Count - 1 && icon != null) lh = Math.Max(lh, icon.Height);
                int cx = PadX;
                foreach (Control c in lines[li])
                {
                    c.Location = new Point(cx, y + (lh - c.Height) / 2);
                    Host.Controls.Add(c);
                    cx += c.Width + 10;
                }
                endX = cx;
                lastH = lh;
                y += lh + (li < lines.Count - 1 ? 6 : 0);
            }
            if (icon != null)
            {
                icon.Location = new Point(endX - 2, y - lastH + (lastH - icon.Height) / 2);
                Host.Controls.Add(icon);
            }
            return (y - Y) + lastH;
        }

        // Controls on one line, no description.
        public void Row(params Control[] cs)
        {
            Y += PlaceRow(cs, null) + 10;
        }

        // One function: controls + (i) hover explanation + a very short description underneath.
        public void Opt(string shortText, string tip, params Control[] cs)
        {
            int h = PlaceRow(cs, tip);
            Y += h + 2;
            Label s = Ui.Lbl(shortText, "muted");
            s.AutoSize = false;
            s.AutoEllipsis = true;
            s.Width = Width - PadX * 2;
            // Measure the real line height of the font (with descenders), so letters like g / y / p
            // are never clipped at the bottom - also correct under Windows display scaling.
            s.Height = TextRenderer.MeasureText(" Agypj", Theme.Small, new Size(1000, 100),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Height + 2;
            s.Location = new Point(PadX + 2, Y);
            Host.Controls.Add(s);
            Y += s.Height + 12;
        }

        // A row with an (i) but no description line.
        public void RowTip(string tip, params Control[] cs)
        {
            Y += PlaceRow(cs, tip) + 10;
        }

        // A short line of text with an (i) at its end.
        public void Hint(string shortText, string tip)
        {
            Label l = Ui.Lbl(shortText, "muted");
            l.Location = new Point(PadX + 2, Y + 2);
            Host.Controls.Add(l);
            int w = TextRenderer.MeasureText(shortText, Theme.Small).Width;
            InfoIcon icon = new InfoIcon(tip);
            icon.Location = new Point(PadX + 2 + w + 8, Y);
            Host.Controls.Add(icon);
            Y += 28;
        }

        public void Add(Control c)
        {
            c.Location = new Point(PadX, Y);
            Host.Controls.Add(c);
            Y += c.Height + 8;
        }

        public void Gap(int n) { Y += n; }
    }
}
