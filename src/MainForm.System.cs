using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AltHub
{
    // Windows notifications (balloon / toast through the tray icon).
    public static class Notify
    {
        public static NotifyIcon Tray;

        public static void Toast(string title, string text, ToolTipIcon icon)
        {
            try
            {
                if (!Store.Settings.Toasts || Tray == null || !Tray.Visible) return;
                Tray.ShowBalloonTip(6000, title, text, icon);
            }
            catch (Exception) { }
        }
    }

    // Small round colour swatch for the accent picker.
    public class AccentSwatch : Control
    {
        public Color Color;
        public bool Chosen;

        public AccentSwatch(Color c)
        {
            Color = c;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(26, 26);
            Cursor = Cursors.Hand;
            Margin = new Padding(0, 2, 6, 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            using (SolidBrush b = new SolidBrush(Color)) g.FillEllipse(b, 3, 3, 19, 19);
            if (Chosen) using (Pen p = new Pen(Theme.Text, 2f)) g.DrawEllipse(p, 1, 1, 23, 23);
        }
    }

    public partial class MainForm
    {
        NotifyIcon tray;
        ContextMenuStrip trayMenu;
        bool quitting;
        CheckBox chkTray, chkCloseTray, chkMinTray, chkToasts, chkHotkeysLaunch, chkRejoin;
        FlowLayoutPanel accentRow;
        readonly List<AccentSwatch> swatches = new List<AccentSwatch>();

        static readonly string[] AccentNames = new string[] { "Indigo", "Blue", "Cyan", "Green", "Orange", "Red", "Pink", "Purple" };
        static readonly string[] AccentHexes = new string[] { "#6366F1", "#3B82F6", "#06B6D4", "#22C55E", "#F59E0B", "#EF4444", "#EC4899", "#A855F7" };

        // ---------------- settings section ----------------
        void BuildSystemSettings(PageBuilder p)
        {
            AppSettings S = Store.Settings;
            p.Section("Accent colour");
            accentRow = new FlowLayoutPanel();
            accentRow.Size = new Size(PageW - PageBuilder.PadX * 2, 34);
            accentRow.Tag = "card";
            accentRow.WrapContents = true;
            for (int i = 0; i < AccentNames.Length; i++)
            {
                string hex = AccentHexes[i];
                AccentSwatch sw = new AccentSwatch(ParseHex(hex));
                sw.Tag = hex;
                Ui.Tips.SetToolTip(sw, AccentNames[i]);
                sw.Click += delegate { SetAccent(hex); };
                swatches.Add(sw);
                accentRow.Controls.Add(sw);
            }
            RoundButton custom = Ui.Btn("Custom...", 90, delegate { PickCustomAccent(); }, "");
            custom.Margin = new Padding(8, 0, 0, 0);
            accentRow.Controls.Add(custom);
            p.Add(accentRow);
            p.Hint("Highlights, buttons and headings use this colour.", "Applied immediately. Pick one of the swatches or press Custom... to choose any colour.");

            p.Section("Tray, notifications & hotkeys");
            chkTray = Ui.Chk("Show an AltHub icon in the system tray", S.Tray, null);
            p.Opt("Right-click the tray icon: Launch all, Stop all, Quit.",
                "The tray icon lets you control the app without opening the window. Double-click it to bring the window back.",
                chkTray);
            chkMinTray = Ui.Chk("Minimizing hides the window to the tray", S.MinToTray, null);
            chkCloseTray = Ui.Chk("The close button hides the window to the tray (use Quit in the tray menu to exit)", S.CloseToTray, null);
            p.Opt("Keep running quietly in the background.",
                "Needs the tray icon. Accounts keep running and the watcher keeps working while the window is hidden.",
                chkMinTray);
            p.Row(chkCloseTray);
            chkToasts = Ui.Chk("Windows notifications for locked accounts, kicks and crashes", S.Toasts, null);
            p.Opt("Pop-up notifications in addition to the Discord webhook.",
                "Shows a Windows notification when an account gets locked, is kicked or disconnected, or a Roblox window crashes. Uses the tray icon, so it turns the tray icon on.",
                chkToasts);
            chkHotkeysLaunch = Ui.Chk("Global hotkeys: Ctrl+Alt+F5 = Launch all, Ctrl+Alt+F6 = Stop all", S.HotkeysLaunch, null);
            p.Opt("Launch or stop everything from any program.",
                "Launch all starts every account that is visible in the list (same as the Launch all button, without asking). Stop all closes every Roblox window this app started. The layout hotkeys (Ctrl+Alt+F9 / F10 / F11) are on the Layout page.",
                chkHotkeysLaunch);
            chkRejoin = Ui.Chk("Rejoin the same server after a kick, disconnect or crash", S.RejoinSameServer, null);
            p.Opt("The watcher brings an account back into the server it was in.",
                "Turns on auto-relaunch and 'also relaunch when the account left the game' (Automation section above) and remembers the server each account is in. If the old server is gone after two tries, the account joins any server of the game. Private servers are always rejoined.",
                chkRejoin);

            EventHandler ch = delegate { OnSystemSettingChanged(); };
            chkTray.CheckedChanged += ch;
            chkMinTray.CheckedChanged += ch;
            chkCloseTray.CheckedChanged += ch;
            chkToasts.CheckedChanged += ch;
            chkHotkeysLaunch.CheckedChanged += ch;
            chkRejoin.CheckedChanged += ch;
            MarkAccent();
        }

        void SyncSystemControls()
        {
            if (chkTray == null) return;
            AppSettings S = Store.Settings;
            chkTray.Checked = S.Tray;
            chkMinTray.Checked = S.MinToTray;
            chkCloseTray.Checked = S.CloseToTray;
            chkToasts.Checked = S.Toasts;
            chkHotkeysLaunch.Checked = S.HotkeysLaunch;
            chkRejoin.Checked = S.RejoinSameServer;
            MarkAccent();
        }

        void OnSystemSettingChanged()
        {
            if (loadingSettings) return;
            AppSettings S = Store.Settings;
            loadingSettings = true;
            if ((chkToasts.Checked || chkMinTray.Checked || chkCloseTray.Checked) && !chkTray.Checked) chkTray.Checked = true;   // these need the tray icon
            if (!chkTray.Checked) { chkToasts.Checked = false; chkMinTray.Checked = false; chkCloseTray.Checked = false; }
            if (chkRejoin.Checked)
            {
                if (chkRelaunch != null && !chkRelaunch.Checked) chkRelaunch.Checked = true;
                if (chkWatcherPresence != null && !chkWatcherPresence.Checked) chkWatcherPresence.Checked = true;
            }
            loadingSettings = false;
            S.Tray = chkTray.Checked;
            S.MinToTray = chkMinTray.Checked;
            S.CloseToTray = chkCloseTray.Checked;
            S.Toasts = chkToasts.Checked;
            S.HotkeysLaunch = chkHotkeysLaunch.Checked;
            S.RejoinSameServer = chkRejoin.Checked;
            if (chkRejoin.Checked) OnSettingChanged();      // saves the auto-relaunch / presence options that were just ticked
            Store.SaveSettings();
            ApplyTrayVisibility();
            RegisterHotkeys();
        }

        // ---------------- accent ----------------
        static Color ParseHex(string hex)
        {
            try
            {
                string h = (hex ?? "").Trim().TrimStart('#');
                if (h.Length == 6) return Color.FromArgb(Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16), Convert.ToInt32(h.Substring(4, 2), 16));
            }
            catch (Exception) { }
            return Color.Empty;
        }

        static string ToHex(Color c) { return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); }

        void SetAccent(string hex)
        {
            Store.Settings.AccentHex = hex;
            Store.SaveSettings();
            Theme.SetAccent(hex);
            ApplyTheme();
            MarkAccent();
        }

        void PickCustomAccent()
        {
            using (ColorDialog d = new ColorDialog())
            {
                d.FullOpen = true;
                d.Color = Theme.Accent;
                if (d.ShowDialog(this) == DialogResult.OK) SetAccent(ToHex(d.Color));
            }
        }

        void MarkAccent()
        {
            string cur = (Store.Settings.AccentHex ?? "").ToUpperInvariant();
            if (cur.Length == 0) cur = AccentHexes[0];
            foreach (AccentSwatch s in swatches) { s.Chosen = ((string)s.Tag).ToUpperInvariant() == cur; s.Invalidate(); }
        }

        // ---------------- follow Windows theme ----------------
        void SystemThemeTick()
        {
            if (Store.Settings.Theme != "System") return;
            bool dark = Theme.ResolveDark("System");
            if (dark == Theme.Dark) return;
            Theme.Set(dark);
            ApplyTheme();
            OnAccountSelectionChanged();
            UpdateMultiChip();
        }

        // ---------------- tray ----------------
        void SetupTray()
        {
            trayMenu = Ui.Menu();
            trayMenu.Items.Add(Ui.Item("Show AltHub", delegate { ShowFromTray(); }));
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(Ui.Item("Launch all", delegate { TrayLaunchAll(); }));
            trayMenu.Items.Add(Ui.Item("Stop all", delegate { StopAll(); }));
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(Ui.Item("Quit", delegate { quitting = true; Close(); }));
            StyleMenus2(trayMenu);

            tray = new NotifyIcon();
            tray.Text = "AltHub";
            try { tray.Icon = Icon ?? SystemIcons.Application; } catch (Exception) { tray.Icon = SystemIcons.Application; }
            tray.ContextMenuStrip = trayMenu;
            tray.DoubleClick += delegate { ShowFromTray(); };
            Notify.Tray = tray;
            ApplyTrayVisibility();
            Resize += delegate
            {
                if (WindowState == FormWindowState.Minimized && Store.Settings.MinToTray && Store.Settings.Tray) HideToTray();
            };
        }

        void StyleMenus2(ContextMenuStrip m)
        {
            m.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
            m.BackColor = Theme.Panel;
            m.ForeColor = Theme.Text;
            foreach (ToolStripItem it in m.Items) StyleItem(it);
        }

        void ApplyTrayVisibility()
        {
            if (tray != null) tray.Visible = Store.Settings.Tray;
        }

        void HideToTray()
        {
            ShowInTaskbar = false;
            Hide();
        }

        void ShowFromTray()
        {
            ShowInTaskbar = true;
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        void TrayLaunchAll()
        {
            List<Account> all = FilteredAccounts();
            if (all.Count == 0) { AddLog("There are no accounts in the list."); return; }
            StartLaunch(all);
        }
    }
}
