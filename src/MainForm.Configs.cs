using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AltHub
{
    // Quick configs (Perf page), shareable configs and portable mode (Settings).
    public partial class MainForm
    {
        Label lblQuickNow;

        void BuildQuickConfigSection(PageBuilder p)
        {
            p.Section("Quick configs");
            p.Note("From the lowest load to the best looks - one click fills in graphics, frame rate, memory, CPU and throttling.");
            foreach (QuickConfig q in QuickConfigs.All)
            {
                QuickConfig qc = q;
                RoundButton b = Ui.Btn(qc.Name, 190, delegate { ApplyQuickConfig(qc); }, qc.Name == "Balanced" ? "primary" : "");
                Label tag = Ui.Fld(qc.Tagline, 400);
                tag.ForeColor = Theme.Muted;
                tag.Tag = "muted";
                p.Opt(ShortDesc(qc.Description), qc.Description, b, tag);
            }
            p.Section("Community configs");
            p.Note("Setups in the style people share: pick one and it is applied just like a quick config. You can still export your own as a .athcfg file.");
            foreach (QuickConfig q in QuickConfigs.Community)
            {
                QuickConfig qc = q;
                RoundButton b = Ui.Btn(qc.Name, 190, delegate { ApplyQuickConfig(qc); }, "");
                Label tag = Ui.Fld(qc.Tagline, 400);
                tag.ForeColor = Theme.Muted;
                tag.Tag = "muted";
                p.Opt(ShortDesc(qc.Description), qc.Description, b, tag);
            }
            lblQuickNow = Ui.Lbl("", "muted");
            p.Row(lblQuickNow);
        }

        // the line under a config button is one line long: cut at a comma, the full text stays in the (i)
        static string ShortDesc(string d)
        {
            if (d.Length <= 100) return d;
            int cut = d.LastIndexOf(',', 98);
            return (cut > 30 ? d.Substring(0, cut) : d.Substring(0, 98)) + ".";
        }

        void ApplyQuickConfig(QuickConfig q)
        {
            if (MessageBox.Show(this, "Apply \"" + q.Name + "\"?\n\n" + q.Description + "\n\nThis changes your graphics, frame rate, memory, CPU and throttling settings. Windows that are already open keep their old graphics until you relaunch them.",
                "Quick config", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            q.Apply(Store.Settings);
            Store.SaveSettings();
            CpuCap.Release();
            RamCap.Reset();
            RebuildSettingPages();
            if (lblQuickNow != null) lblQuickNow.Text = "Applied: " + q.Name;
            AddLog("Quick config applied: " + q.Name + ".");
        }

        // The Perf and Settings pages are built once from the settings; after a bulk change they are built again.
        void RebuildSettingPages()
        {
            string cur = currentPage;
            foreach (string key in new string[] { "Perf", "Settings" })
            {
                Panel old;
                if (pages.TryGetValue(key, out old)) { pageHost.Controls.Remove(old); pages.Remove(key); old.Dispose(); }
            }
            loadingSettings = true;
            BuildPerfPage();
            BuildSettingsPage();
            loadingSettings = false;
            Theme.Apply(pageHost);
            ApplyTheme();
            ShowPage(string.IsNullOrEmpty(cur) ? "Home" : cur);
        }

        // ---------------- share ----------------
        void BuildShareSection(PageBuilder p)
        {
            p.Section("Shareable configs and portable mode");
            p.Opt("Save your settings as a file others can import (or import theirs).",
                "A config holds how AltHub behaves: graphics, FPS, memory and CPU limits, throttling, launch options, watcher, layout, theme and language. It never contains accounts, cookies, passwords, proxies, webhook links or private servers, so it is safe to share.",
                Ui.Btn("Export my config...", 170, delegate { ExportConfig(); }, ""),
                Ui.Btn("Import a config...", 170, delegate { ImportConfig(); }, ""));
            lblPortable = Ui.Lbl("", "muted");
            lblPortable.AutoSize = false;
            lblPortable.AutoEllipsis = true;
            lblPortable.Size = new Size(600, 22);
            Button bp = Ui.Btn(Store.IsPortable ? "Switch back to normal mode" : "Switch to portable mode", 240, delegate { TogglePortable(); }, "");
            btnPortable = (RoundButton)bp;
            p.Opt("Portable mode keeps all your data next to AltHub.exe.",
                "Settings, accounts, backups and logs go into an AltHubData folder next to AltHub.exe instead of %AppData%, so you can carry AltHub on a USB stick or keep it in one folder. A file named portable.txt next to the exe switches it on. Accounts are encrypted for your Windows user by default; to open the folder on another PC set a password first (Security section above).",
                btnPortable);
            p.Row(lblPortable);
            UpdatePortableLabel();
        }

        RoundButton btnPortable;
        Label lblPortable;

        void UpdatePortableLabel()
        {
            if (lblPortable == null) return;
            lblPortable.Text = (Store.IsPortable ? "Portable mode is ON - data folder: " : "Normal mode - data folder: ") + Store.Dir;
            Ui.Tips.SetToolTip(lblPortable, Store.Dir);
        }

        void ExportConfig()
        {
            using (SaveFileDialog s = new SaveFileDialog())
            {
                s.Filter = "AltHub config (*.athcfg)|*.athcfg";
                s.FileName = "my-althub-config.athcfg";
                if (s.ShowDialog(this) != DialogResult.OK) return;
                string name = Path.GetFileNameWithoutExtension(s.FileName);
                string err = ConfigShare.Export(s.FileName, name);
                AddLog(err == null ? "Config saved to " + s.FileName + "." : "Could not save the config: " + err);
            }
        }

        void ImportConfig()
        {
            using (OpenFileDialog o = new OpenFileDialog())
            {
                o.Filter = "AltHub config (*.athcfg)|*.athcfg|All files (*.*)|*.*";
                if (o.ShowDialog(this) != DialogResult.OK) return;
                string name, err;
                IDictionary<string, object> s = ConfigShare.Read(o.FileName, out name, out err);
                if (s == null) { AddLog("Could not import: " + err); return; }
                if (MessageBox.Show(this, "Import the config \"" + name + "\" (" + s.Count + " settings)?\n\nIt replaces your current performance, launch, layout and theme settings. Your accounts are not touched.",
                    "Import a config", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                int n = ConfigShare.Apply(s);
                Store.SaveSettings();
                CpuCap.Release();
                RamCap.Reset();
                Theme.Load(Store.Settings.Theme, Store.Settings.AccentHex);
                RebuildSettingPages();
                AddLog("Imported the config \"" + name + "\": " + n + " settings applied.");
                if (Store.Settings.Language != Lang.Current) AddLog("The language changes after you restart AltHub.");
            }
        }


        // Portable data can be copied by anyone who gets the folder, so a password is required unless the user accepts the risk.
        // Returns false when the user cancelled.
        bool EnsurePortablePassword(IWin32Window owner)
        {
            if (Store.Password != null) return true;
            while (true)
            {
                Form f = Dialogs.Make("Portable mode - protect your accounts", 520, 250);
                Label msg = Ui.Lbl("Portable mode keeps your accounts in a folder you can carry around. Without a password anyone who copies that folder from this PC can read your Roblox cookies and log in as you.\r\n\r\nSet a password to protect them (recommended).", "");
                msg.AutoSize = false; msg.Size = new Size(488, 120);
                Dialogs.Put(f, msg, 16, 16);
                RoundButton setPw = Ui.Btn("Set a password", 150, null, "primary");
                setPw.DialogResult = DialogResult.OK;
                Dialogs.Put(f, setPw, 16, 150);
                RoundButton cancel = Ui.Btn("Cancel", 90, null, "");
                cancel.DialogResult = DialogResult.Cancel;
                Dialogs.Put(f, cancel, 176, 150);
                LinkLabel risk = new LinkLabel();
                risk.Text = Lang.T("Continue without a password (I accept the risk)");
                risk.AutoSize = true;
                risk.Font = Theme.Small;
                risk.LinkColor = Theme.Muted;
                risk.ActiveLinkColor = Theme.Text;
                risk.BackColor = Color.Transparent;
                bool accepted = false;
                risk.Click += delegate
                {
                    if (MessageBox.Show(f, "Without a password, the accounts in the portable folder are only encrypted for this Windows user, so on another PC they will not open, and anyone with access to this PC can still use them.\r\n\r\nI understand and accept the risk.",
                        "Accept the risk", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) { accepted = true; f.DialogResult = DialogResult.Abort; }
                };
                Dialogs.Put(f, risk, 16, 206);
                Theme.Apply(f);
                DialogResult r = f.ShowDialog(owner);
                f.Dispose();
                if (accepted) return true;
                if (r != DialogResult.OK) return false;
                string pw = Dialogs.AskPassword(owner, "Set a password", "Choose a password for your accounts file.\r\nIf you forget it, the accounts CANNOT be recovered!", true);
                if (pw == null) continue;
                Store.Password = pw;
                Store.SaveAccounts();
                UpdateStorageLabel();
                return true;
            }
        }

        void TogglePortable()
        {
            bool on = !Store.IsPortable;
            string warn = on
                ? "Switch to portable mode?\n\nAll your data is copied into the AltHubData folder next to AltHub.exe (" + Store.PortableDir + ") and AltHub restarts. " +
                  (Store.Password == null ? "\nNote: your accounts are encrypted for THIS Windows user. To use the folder on another PC, set a password first (Settings > Security)." : "")
                : "Switch back to normal mode?\n\nYour data is copied back to %AppData%\\AltHub and AltHub restarts.";
            if (MessageBox.Show(this, warn, "Portable mode", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            if (on && !EnsurePortablePassword(this)) return;
            string err = Store.SetPortable(on);
            if (err != null) { MessageBox.Show(this, "Could not switch: " + err, "Portable mode", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            AddLog(on ? "Portable mode on - restarting." : "Normal mode on - restarting.");
            quitting = true;
            Application.Restart();
        }
    }
}
