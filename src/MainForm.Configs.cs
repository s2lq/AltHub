using System;
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
                p.Opt(qc.Description, qc.Description, b, tag);
            }
            lblQuickNow = Ui.Lbl("", "muted");
            p.Row(lblQuickNow);
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

        void TogglePortable()
        {
            bool on = !Store.IsPortable;
            string warn = on
                ? "Switch to portable mode?\n\nAll your data is copied into the AltHubData folder next to AltHub.exe (" + Store.PortableDir + ") and AltHub restarts. " +
                  (Store.Password == null ? "\nNote: your accounts are encrypted for THIS Windows user. To use the folder on another PC, set a password first (Settings > Security)." : "")
                : "Switch back to normal mode?\n\nYour data is copied back to %AppData%\\AltHub and AltHub restarts.";
            if (MessageBox.Show(this, warn, "Portable mode", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            string err = Store.SetPortable(on);
            if (err != null) { MessageBox.Show(this, "Could not switch: " + err, "Portable mode", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            AddLog(on ? "Portable mode on - restarting." : "Normal mode on - restarting.");
            quitting = true;
            Application.Restart();
        }
    }
}
