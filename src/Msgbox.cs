using System;
using System.Drawing;
using System.Windows.Forms;

namespace AltHub
{
    // A dark, themed replacement for the plain white Windows message box. It has the same name and the same Show(...)
    // overloads as System.Windows.Forms.MessageBox, and because it lives in the app's own namespace every existing
    // MessageBox.Show(...) call in the app uses it automatically.
    public static class MessageBox
    {
        public static DialogResult Show(string text) { return Show(null, text, "AltHub", MessageBoxButtons.OK, MessageBoxIcon.None); }
        public static DialogResult Show(string text, string caption) { return Show(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None); }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons) { return Show(null, text, caption, buttons, MessageBoxIcon.None); }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) { return Show(null, text, caption, buttons, icon); }
        public static DialogResult Show(IWin32Window owner, string text) { return Show(owner, text, "AltHub", MessageBoxButtons.OK, MessageBoxIcon.None); }
        public static DialogResult Show(IWin32Window owner, string text, string caption) { return Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons) { return Show(owner, text, caption, buttons, MessageBoxIcon.None); }

        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            text = text ?? "";
            const int textW = 400;
            Size ts = TextRenderer.MeasureText(text, Theme.Base, new Size(textW, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            int textH = Math.Max(24, ts.Height + 4);
            bool scroll = textH > 380;
            int viewH = scroll ? 380 : textH;
            int bodyLeft = icon == MessageBoxIcon.None ? 24 : 80;
            int width = bodyLeft + textW + (scroll ? 40 : 24);
            int height = 24 + Math.Max(viewH, icon == MessageBoxIcon.None ? 0 : 44) + 28 + 40 + 20;

            Form f = Dialogs.Make(caption, width, height);
            if (owner == null) { f.ShowInTaskbar = true; f.StartPosition = FormStartPosition.CenterScreen; }
            f.TopMost = owner == null;

            if (icon != MessageBoxIcon.None) Dialogs.Put(f, new MsgIcon(icon), 24, 24);

            Label lbl = Ui.Lbl(text, "");
            lbl.AutoSize = false;
            lbl.Size = new Size(textW, textH);
            if (scroll)
            {
                Panel host = new Panel();
                host.Tag = "card";
                host.AutoScroll = true;
                host.Size = new Size(textW + 20, viewH);
                host.Controls.Add(lbl);
                Dialogs.Put(f, host, bodyLeft, 24);
            }
            else Dialogs.Put(f, lbl, bodyLeft, 24 + (icon != MessageBoxIcon.None && textH < 44 ? (44 - textH) / 2 : 0));

            string[] labels; DialogResult[] results;
            switch (buttons)
            {
                case MessageBoxButtons.OKCancel: labels = new string[] { "OK", "Cancel" }; results = new DialogResult[] { DialogResult.OK, DialogResult.Cancel }; break;
                case MessageBoxButtons.YesNo: labels = new string[] { "Yes", "No" }; results = new DialogResult[] { DialogResult.Yes, DialogResult.No }; break;
                case MessageBoxButtons.YesNoCancel: labels = new string[] { "Yes", "No", "Cancel" }; results = new DialogResult[] { DialogResult.Yes, DialogResult.No, DialogResult.Cancel }; break;
                case MessageBoxButtons.RetryCancel: labels = new string[] { "Retry", "Cancel" }; results = new DialogResult[] { DialogResult.Retry, DialogResult.Cancel }; break;
                default: labels = new string[] { "OK" }; results = new DialogResult[] { DialogResult.OK }; break;
            }
            int x = width - 20;
            RoundButton first = null;
            for (int i = labels.Length - 1; i >= 0; i--)
            {
                RoundButton b = Ui.Btn(labels[i], 84, null, i == 0 ? "primary" : "");
                b.Width = 84;
                b.DialogResult = results[i];
                x -= b.Width;
                Dialogs.Put(f, b, x, height - 56);
                x -= 8;
                if (i == 0) first = b;
            }
            f.AcceptButton = first;
            bool hasCancel = Array.IndexOf(results, DialogResult.Cancel) >= 0;
            bool hasNo = Array.IndexOf(results, DialogResult.No) >= 0;
            if (hasCancel || results.Length == 1) f.CancelButton = first.DialogResult == DialogResult.OK && results.Length == 1 ? first : null;
            if (hasCancel) foreach (Control c in f.Controls) { Button bb = c as Button; if (bb != null && bb.DialogResult == DialogResult.Cancel) f.CancelButton = bb; }
            if (hasNo && !hasCancel) foreach (Control c in f.Controls) { Button bb = c as Button; if (bb != null && bb.DialogResult == DialogResult.No) f.CancelButton = bb; }
            Theme.Apply(f);
            DialogResult r = owner == null ? f.ShowDialog() : f.ShowDialog(owner);
            f.Dispose();
            return r;
        }
    }

    // Round coloured icon with a simple glyph for the message box.
    public class MsgIcon : Control
    {
        readonly MessageBoxIcon kind;

        public MsgIcon(MessageBoxIcon k)
        {
            kind = k;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(44, 44);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            Color c = Theme.Accent;
            string glyph = "i";
            if (kind == MessageBoxIcon.Warning) { c = Theme.Warn; glyph = "!"; }
            else if (kind == MessageBoxIcon.Error) { c = Theme.Bad; glyph = "x"; }
            else if (kind == MessageBoxIcon.Question) { c = Theme.Accent; glyph = "?"; }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(40, c))) g.FillEllipse(b, 0, 0, 43, 43);
            using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, 6, 6, 31, 31);
            using (Font f = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(glyph, f, Brushes.White, new RectangleF(6, 6, 31, 32), sf);
        }
    }
}
