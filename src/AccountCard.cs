using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AltHub
{
    public static class Shapes
    {
        public static GraphicsPath Round(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static readonly Color[] Palette = new Color[]
        {
            Color.FromArgb(99, 102, 241), Color.FromArgb(52, 168, 100), Color.FromArgb(226, 120, 70),
            Color.FromArgb(70, 150, 235), Color.FromArgb(165, 105, 230), Color.FromArgb(220, 90, 140)
        };

        // Round profile picture, or a coloured circle with the first letter while it is still loading.
        public static void Avatar(Graphics g, Account a, Rectangle r)
        {
            SmoothingMode oldMode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Image img = Avatars.Get(a.UserId);
            if (img != null)
            {
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddEllipse(r);
                    GraphicsState st = g.Save();
                    g.SetClip(path);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(img, r);
                    g.Restore(st);
                }
            }
            else
            {
                int idx = (int)(Math.Abs(a.UserId) % Palette.Length);
                using (SolidBrush b = new SolidBrush(Palette[idx])) g.FillEllipse(b, r);
                string name = string.IsNullOrEmpty(a.Username) ? "?" : a.Username.Substring(0, 1).ToUpperInvariant();
                using (Font f = new Font("Segoe UI Semibold", r.Height * 0.42f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(name, f, Brushes.White, r, sf);
                }
            }
            using (Pen pen = new Pen(Theme.Border, 1.5f)) g.DrawEllipse(pen, r);
            g.SmoothingMode = oldMode;
        }
    }

    // Purple -> cyan gradient button, like the Launch buttons in the reference design.
    public class GradientButton : Button
    {
        bool hover;
        bool stopMode;
        public string Icon = "";
        public Color From = Color.FromArgb(124, 92, 252);
        public Color To = Color.FromArgb(40, 200, 235);
        public Color CornerColor = Color.Empty;     // what shows behind the rounded corners (default: parent's colour)

        public GradientButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
            Height = 34;
            Tag = "gradient";
            Font = Theme.Bold;
        }

        public void SetStop(bool stop)
        {
            stopMode = stop;
            if (stop)
            {
                From = Color.FromArgb(235, 70, 90);
                To = Color.FromArgb(200, 50, 80);
            }
            else
            {
                From = Color.FromArgb(124, 92, 252);
                To = Color.FromArgb(40, 200, 235);
            }
            Invalidate();
        }

        static Color Lighten(Color c)
        {
            return Color.FromArgb(Math.Min(255, c.R + 25), Math.Min(255, c.G + 25), Math.Min(255, c.B + 25));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(!CornerColor.IsEmpty ? CornerColor : (Parent != null ? Parent.BackColor : Theme.Panel));
            if (Width < 4 || Height < 4) return;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color a = Enabled ? (stopMode ? From : Theme.Accent) : Theme.Border;
            Color b = Enabled ? (stopMode ? To : Tint.Shift(Theme.Accent, 45)) : Theme.Border;
            if (hover && Enabled) { a = Lighten(a); b = Lighten(b); }
            using (GraphicsPath path = Shapes.Round(r, 9))
            using (LinearGradientBrush br = new LinearGradientBrush(r, a, b, LinearGradientMode.Horizontal))
            {
                g.FillPath(br, path);
            }
            Color fg = Enabled ? Color.White : Theme.Muted;
            if (Icon.Length > 0)
            {
                int tw = TextRenderer.MeasureText(g, Text, Font, new Size(1000, 40), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
                int x0 = Math.Max(8, (Width - (16 + 8 + tw)) / 2);
                Icons.Draw(g, Icon, new Rectangle(x0, (Height - 16) / 2, 16, 16), fg);
                TextRenderer.DrawText(g, Text, Font, new Rectangle(x0 + 24, 0, Math.Max(1, Width - x0 - 24), Height), fg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
            else
                TextRenderer.DrawText(g, Text, Font, r, fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    // One of the three numbers in the top bar: caption, big value, small extra line.
    public class StatBlock : Panel
    {
        public readonly Label Caption;
        public readonly Label Value;
        public readonly Label Sub;

        // The three lines are painted by the block itself (the labels only hold the text): stacked labels overlapped
        // each other and cut off the top and bottom of the letters.
        const TextFormatFlags Flags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;

        public StatBlock(string caption, int width)
        {
            Size = new Size(width, 52);
            Tag = "card";
            DoubleBuffered = true;
            ResizeRedraw = true;
            Caption = Ui.Lbl(caption, "muted");
            Value = Ui.Lbl("-", "");
            Value.Font = Theme.Stat;
            Sub = Ui.Lbl("", "muted");
            foreach (Label l in new Label[] { Caption, Value, Sub }) l.TextChanged += delegate { Invalidate(); };
            Height = NeededHeight();
        }

        static int LineH(Font f)
        {
            return TextRenderer.MeasureText("Ag%", f, new Size(1000, 200), Flags).Height;
        }

        public int NeededHeight()
        {
            return LineH(Caption.Font) + LineH(Value.Font) + LineH(Sub.Font) + 1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int y = Math.Max(0, (Height - NeededHeight()) / 2);
            int w = Width;
            int h1 = LineH(Caption.Font);
            int h2 = LineH(Value.Font);
            int h3 = LineH(Sub.Font);
            TextRenderer.DrawText(e.Graphics, Caption.Text, Caption.Font, new Rectangle(0, y, w, h1), Theme.Muted, Flags);
            TextRenderer.DrawText(e.Graphics, Value.Text, Value.Font, new Rectangle(0, y + h1, w, h2), Theme.Text, Flags);
            TextRenderer.DrawText(e.Graphics, Sub.Text, Sub.Font, new Rectangle(1, y + h1 + h2, w - 1, h3), Theme.Muted, Flags);
        }

        public void SetTip(string tip)
        {
            Ui.Tips.SetToolTip(this, tip);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { Caption.Dispose(); Value.Dispose(); Sub.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // Expanded account view: profile picture, name, status, the game for this account and a Launch button.
    public class AccountCard : Panel
    {
        public readonly Account Account;
        public bool Selected;
        public readonly Button MenuButton;
        public readonly GradientButton LaunchButton;

        public event Action<AccountCard, bool> CardClicked;
        public event Action<AccountCard> LaunchClicked;
        public event Action<AccountCard> MenuClicked;
        public event Action<AccountCard, string> GameCommitted;
        public event Action<AccountCard, string> PrivateChosen;     // server id, "" = none

        readonly Label lblName;
        readonly Label lblUser;
        readonly Label lblInfo;
        readonly RoundTextBox tbGame;
        readonly DarkCombo cbPriv;
        readonly Label lblNote;
        List<string> privIds = new List<string>();
        string privSig = "";
        bool settingPriv;
        bool hasNote;
        string[] tagArr = new string[0];
        string robuxText = "";
        Color stripe = Color.Empty;
        string pill = "";
        Color pillColor = Color.Gray;
        int pillW = 80;
        string lastGame = "";

        public AccountCard(Account a)
        {
            Account = a;
            DoubleBuffered = true;
            ResizeRedraw = true;
            Size = new Size(340, 280);
            Margin = new Padding(0, 0, 10, 10);
            Tag = "";

            lblName = Ui.Lbl("", "");
            lblName.AutoSize = false;
            lblName.AutoEllipsis = true;
            lblName.Font = Theme.CardName;
            lblName.Location = new Point(84, 14);
            lblName.Size = new Size(150, 24);

            lblUser = Ui.Lbl("", "muted");
            lblUser.AutoSize = false;
            lblUser.AutoEllipsis = true;
            lblUser.Location = new Point(84, 42);
            lblUser.Size = new Size(200, 16);

            lblInfo = Ui.Lbl("", "muted");
            lblInfo.AutoSize = false;
            lblInfo.AutoEllipsis = true;
            lblInfo.Location = new Point(84, 60);
            lblInfo.Size = new Size(200, 16);

            lblNote = Ui.Lbl("", "muted");
            lblNote.AutoSize = false;
            lblNote.AutoEllipsis = true;
            lblNote.Location = new Point(34, 104);
            lblNote.Size = new Size(292, 16);

            Label cap = Ui.Lbl("Game for this account  (link or Place ID, optional)", "muted");
            cap.Location = new Point(14, 128);

            tbGame = Ui.Txt(312, "");
            tbGame.Tag = "inset";
            tbGame.Location = new Point(14, 146);
            tbGame.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            tbGame.Tip("Paste a roblox.com game link (or a private server link) or a Place ID. This account will join it when launched. Leave empty to use the Launch page.");
            tbGame.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitGame(); }
            };
            tbGame.Leave += delegate { CommitGame(); };

            MenuButton = Ui.Btn("...", 34, null, "");
            MenuButton.Height = 28;
            MenuButton.Width = 34;
            MenuButton.Location = new Point(340 - 14 - 34, 14);
            MenuButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            MenuButton.Click += delegate { Action<AccountCard> h = MenuClicked; if (h != null) h(this); };

            Label capP = Ui.Lbl("Private server  (optional)", "muted");
            capP.Location = new Point(14, 182);
            cbPriv = new DarkCombo();
            cbPriv.Width = 312;
            cbPriv.Location = new Point(14, 200);
            cbPriv.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Ui.Tips.SetToolTip(cbPriv, "The private server this account joins when launched. Manage the list on the Private tab.");
            cbPriv.SelectedIndexChanged += delegate
            {
                if (settingPriv) return;
                Action<AccountCard, string> h = PrivateChosen;
                int i = cbPriv.SelectedIndex;
                if (h != null && i >= 0) h(this, i == 0 ? "" : privIds[i - 1]);
            };

            LaunchButton = new GradientButton();
            LaunchButton.Text = "Launch";
            LaunchButton.Location = new Point(14, 234);
            LaunchButton.Size = new Size(312, 36);
            LaunchButton.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            LaunchButton.Click += delegate { Action<AccountCard> h = LaunchClicked; if (h != null) h(this); };

            Controls.Add(lblName);
            Controls.Add(lblUser);
            Controls.Add(lblInfo);
            Controls.Add(cap);
            Controls.Add(lblNote);
            Controls.Add(capP);
            Controls.Add(cbPriv);
            Controls.Add(tbGame);
            Controls.Add(MenuButton);
            Controls.Add(LaunchButton);

            Click += OnAnyClick;
            lblName.Click += OnAnyClick;
            lblUser.Click += OnAnyClick;
            lblInfo.Click += OnAnyClick;
            cap.Click += OnAnyClick;
            capP.Click += OnAnyClick;
            lblNote.Click += OnAnyClick;
        }

        // Tags, Robux, the note, the colour stripe and the private server drop-down.
        public void SetExtras(string tags, string robux, string note, Color color, List<string> ids, List<string> names, string selectedId)
        {
            string[] ta = string.IsNullOrEmpty(tags) ? new string[0] : tags.Split(',');
            bool changed = !string.Join("|", ta).Equals(string.Join("|", tagArr)) || robux != robuxText || color != stripe;
            tagArr = ta; robuxText = robux; stripe = color;
            lblNote.Text = note ?? "";
            if (hasNote != !string.IsNullOrEmpty(note)) { hasNote = !string.IsNullOrEmpty(note); changed = true; }

            string sig = string.Join("|", ids.ToArray()) + "#" + string.Join("|", names.ToArray());
            if (sig != privSig && !cbPriv.DroppedDown)
            {
                settingPriv = true;
                privIds = new List<string>(ids);
                privSig = sig;
                cbPriv.Items.Clear();
                cbPriv.Items.Add("(none)");
                foreach (string n in names) cbPriv.Items.Add(n);
                settingPriv = false;
            }
            if (!cbPriv.DroppedDown)
            {
                int want = string.IsNullOrEmpty(selectedId) ? 0 : privIds.IndexOf(selectedId) + 1;
                if (want >= 0 && cbPriv.SelectedIndex != want)
                {
                    settingPriv = true;
                    cbPriv.SelectedIndex = want;
                    settingPriv = false;
                }
            }
            if (changed) Invalidate();
        }

        void OnAnyClick(object sender, EventArgs e)
        {
            Action<AccountCard, bool> h = CardClicked;
            if (h != null) h(this, (Control.ModifierKeys & Keys.Control) == Keys.Control);
        }

        void CommitGame()
        {
            string t = tbGame.Text.Trim();
            if (t == lastGame) return;
            lastGame = t;
            Action<AccountCard, string> h = GameCommitted;
            if (h != null) h(this, t);
        }

        public void SetState(string name, string user, string info, string pillText, Color pillCol, bool running, string game, bool selected)
        {
            lblName.Text = name;
            lblUser.Text = user;
            lblInfo.Text = info;
            if (!tbGame.Focused)
            {
                tbGame.Text = game;
                lastGame = game.Trim();
            }
            LaunchButton.CornerColor = Theme.Panel2;
            LaunchButton.Text = running ? "Stop" : "Launch";
            LaunchButton.SetStop(running);
            bool changed = pill != pillText || pillColor != pillCol || Selected != selected;
            pill = pillText;
            pillColor = pillCol;
            Selected = selected;
            pillW = TextRenderer.MeasureText(pill, Theme.Small).Width + 34;
            lblName.Width = Math.Max(60, Width - 84 - 14 - 34 - 8 - pillW - 8);
            lblUser.Width = Math.Max(60, Width - 84 - 14);
            lblInfo.Width = Math.Max(60, Width - 84 - 14);
            if (changed) Invalidate();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (lblName != null)
            {
                lblName.Width = Math.Max(60, Width - 84 - 14 - 34 - 8 - pillW - 8);
                lblUser.Width = Math.Max(60, Width - 84 - 14);
                lblInfo.Width = Math.Max(60, Width - 84 - 14);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            Rectangle r = new Rectangle(1, 1, Width - 3, Height - 3);
            using (GraphicsPath path = Shapes.Round(r, 14))
            {
                using (SolidBrush b = new SolidBrush(Theme.Panel2)) g.FillPath(b, path);
                using (Pen pen = new Pen(Selected ? Theme.Accent : Theme.Border, Selected ? 2f : 1f)) g.DrawPath(pen, path);
            }
            Shapes.Avatar(g, Account, new Rectangle(14, 14, 60, 60));

            if (!stripe.IsEmpty)
                using (GraphicsPath sp = Shapes.Round(new Rectangle(0, 20, 6, Height - 40), 3))
                using (SolidBrush sb = new SolidBrush(stripe)) g.FillPath(sb, sp);

            // tag chips on the left, Robux on the right
            int tx = 14;
            TextFormatFlags cf = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            int rw = robuxText.Length == 0 ? 0 : TextRenderer.MeasureText(robuxText, Theme.Small).Width + 4;
            foreach (string t in tagArr)
            {
                int w = TextRenderer.MeasureText(g, t, Theme.Small, new Size(1000, 20), cf).Width + 16;
                if (tx + w > Width - 14 - rw - 6) break;
                Rectangle cr = new Rectangle(tx, 82, w, 18);
                using (GraphicsPath cp = Shapes.Round(cr, 8))
                {
                    using (SolidBrush b = new SolidBrush(Theme.Bg)) g.FillPath(b, cp);
                    using (Pen pen = new Pen(Theme.Border)) g.DrawPath(pen, cp);
                }
                TextRenderer.DrawText(g, t, Theme.Small, cr, Theme.Text, cf);
                tx += w + 5;
            }
            if (hasNote) Icons.Draw(g, "note", new Rectangle(14, 104, 15, 15), Theme.Muted);
            if (rw > 0)
                TextRenderer.DrawText(g, robuxText, Theme.Small, new Rectangle(Width - 14 - rw, 82, rw, 18), Theme.Muted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            Rectangle pr = new Rectangle(Width - 14 - 34 - 8 - pillW, 15, pillW, 24);
            using (GraphicsPath pp = Shapes.Round(pr, 12))
            {
                using (SolidBrush b = new SolidBrush(Theme.Bg)) g.FillPath(b, pp);
                using (Pen pen = new Pen(Theme.Border)) g.DrawPath(pen, pp);
            }
            using (SolidBrush b = new SolidBrush(pillColor)) g.FillEllipse(b, pr.Left + 11, pr.Top + 8, 8, 8);
            TextRenderer.DrawText(g, pill, Theme.Small, new Rectangle(pr.Left + 24, pr.Top, pr.Width - 26, pr.Height), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }
    }
}
