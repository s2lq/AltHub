using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AltHub
{
    // Compact mode (tighter main window) and the mini window (a small always-on-top launcher).
    public partial class MainForm
    {
        Label topSub;
        MiniForm mini;

        void SetCompact(bool on)
        {
            Store.Settings.CompactUi = on;
            Store.SaveSettings();
            ApplyCompact();
            AddLog(on ? "Compact mode on (Ctrl+Shift+K to switch back)." : "Compact mode off.");
        }

        void ApplyCompact()
        {
            bool c = Store.Settings.CompactUi;
            try
            {
                root.RowStyles[0].Height = c ? 50 : Math.Max(68, statRun.NeededHeight() + 14);
                if (topSub != null) topSub.Visible = !c;
                statCpu.Visible = !c;
                statRam.Visible = !c;
                lblMulti.Visible = !c;
                TableLayoutPanel body = root.GetControlFromPosition(0, 1) as TableLayoutPanel;
                if (body != null) body.Padding = new Padding(c ? 4 : 10);
                split.SplitterWidth = c ? 6 : 10;
                split.Panel1MinSize = c ? 260 : 320;
                split.Panel2MinSize = c ? 520 : 700;
                MinimumSize = c ? new Size(900, 560) : new Size(1240, 700);
                Rectangle wa = Screen.FromControl(this).WorkingArea;
                MinimumSize = new Size(Math.Min(MinimumSize.Width, wa.Width), Math.Min(MinimumSize.Height, wa.Height));
                LayoutCards();
                if (fitTabs != null) fitTabs();
            }
            catch (Exception ex) { Log.Error("ApplyCompact", ex); }
            if (chkCompact != null && chkCompact.Checked != c) chkCompact.Checked = c;
        }

        CheckBox chkCompact;

        void BuildCompactSection(PageBuilder p)
        {
            p.Section("Window size");
            chkCompact = Ui.Chk("Compact mode: smaller bars and margins, fits small screens", Store.Settings.CompactUi, null);
            chkCompact.CheckedChanged += delegate { if (chkCompact.Checked != Store.Settings.CompactUi) SetCompact(chkCompact.Checked); };
            p.Opt("Shortcut: Ctrl+Shift+K", "Shrinks the top bar and the spacing so more fits on a small screen, and lets the window be made much smaller.",
                chkCompact,
                Ui.Btn("Open the mini window", 170, delegate { ToggleMini(); }, ""));
        }

        void ToggleMini()
        {
            if (mini != null && !mini.IsDisposed) { CloseMini(); return; }
            mini = new MiniForm(this);
            mini.FormClosed += delegate { mini = null; if (!quitting) ShowFromTray(); };
            Hide();
            mini.Show();
        }

        void CloseMini()
        {
            if (mini != null && !mini.IsDisposed) mini.Close();
        }

        // used by the mini window
        internal List<Account> MiniAccounts() { return Store.Accounts.OrderBy(a => a.Order).ToList(); }
        internal bool MiniRunning(Account a) { return runningIds.Contains(a.Id); }
        internal string MiniName(Account a) { return DisplayName(a); }
        internal void MiniToggle(Account a)
        {
            if (runningIds.Contains(a.Id)) StopAccount(a);
            else StartLaunch(new List<Account> { a });
        }
        internal void MiniLaunchAll() { StartLaunch(Store.Accounts.OrderBy(a => a.Order).ToList()); }
        internal void MiniStopAll() { StopAll(); }
    }

    // Small always-on-top window: one row per account with a start/stop button.
    public class MiniForm : Form
    {
        readonly MainForm main;
        readonly Panel list = new Panel();
        readonly Timer timer = new Timer();
        readonly Dictionary<string, RoundButton> btns = new Dictionary<string, RoundButton>();
        readonly Dictionary<string, Label> dots = new Dictionary<string, Label>();
        int shownCount = -1;

        public MiniForm(MainForm m)
        {
            main = m;
            Text = "AltHub";
            Font = Theme.Base;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            TopMost = true;
            MaximizeBox = false;
            ClientSize = new Size(300, 360);
            StartPosition = FormStartPosition.Manual;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Point at = new Point(Store.Settings.MiniX, Store.Settings.MiniY);
            if (at.X < 0 || at.Y < 0 || at.X > wa.Right - 100 || at.Y > wa.Bottom - 100) at = new Point(wa.Right - 330, wa.Top + 60);
            Location = at;

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 82;
            RoundButton all = Ui.Btn("Launch all", 90, delegate { main.MiniLaunchAll(); }, "primary");
            RoundButton stop = Ui.Btn("Stop all", 80, delegate { main.MiniStopAll(); }, "danger");
            RoundButton open = Ui.Btn("Open AltHub", 130, delegate { Close(); }, "");
            all.Location = new Point(10, 8);
            stop.Location = new Point(all.Right + 8, 8);
            open.Location = new Point(10, 44);
            bottom.Controls.Add(all);
            bottom.Controls.Add(stop);
            bottom.Controls.Add(open);

            list.Dock = DockStyle.Fill;
            list.AutoScroll = true;
            Controls.Add(list);
            Controls.Add(bottom);
            Rebuild();
            Theme.Apply(this);
            try { NativeTheme.TitleBar(this); } catch (Exception) { }
            timer.Interval = 1000;
            timer.Tick += delegate { Refresh2(); };
            timer.Start();
        }

        void Rebuild()
        {
            list.Controls.Clear();
            btns.Clear();
            dots.Clear();
            List<Account> accs = main.MiniAccounts();
            shownCount = accs.Count;
            int y = 8;
            foreach (Account a in accs)
            {
                Account acc = a;
                Label dot = Ui.Lbl("●", "");
                dot.Location = new Point(10, y + 6);
                Label name = Ui.Lbl(Util.Short(main.MiniName(acc), 20), "");
                name.Location = new Point(30, y + 6);
                RoundButton b = Ui.Btn("Launch", 76, delegate { main.MiniToggle(acc); }, "");
                b.Location = new Point(205, y);
                list.Controls.Add(dot);
                list.Controls.Add(name);
                list.Controls.Add(b);
                btns[acc.Id] = b;
                dots[acc.Id] = dot;
                y += 38;
            }
            Refresh2();
        }

        void Refresh2()
        {
            List<Account> accs = main.MiniAccounts();
            if (accs.Count != shownCount) { Rebuild(); return; }
            foreach (Account a in accs)
            {
                bool run = main.MiniRunning(a);
                RoundButton b;
                if (btns.TryGetValue(a.Id, out b))
                {
                    string t = run ? Lang.T("Stop") : Lang.T("Launch");
                    if (b.Text != t) { b.Text = t; b.Invalidate(); }
                }
                Label d;
                if (dots.TryGetValue(a.Id, out d)) d.ForeColor = run ? Color.FromArgb(70, 200, 110) : Theme.Muted;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            Store.Settings.MiniX = Left;
            Store.Settings.MiniY = Top;
            Store.SaveSettings();
            base.OnFormClosed(e);
        }
    }
}
