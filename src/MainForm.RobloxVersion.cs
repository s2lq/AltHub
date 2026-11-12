using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AltHub
{
    // Roblox version viewer and changer (Settings > Roblox version...).
    public partial class MainForm
    {
        void ShowRobloxVersions()
        {
            Form f = Dialogs.Make("Roblox version", 780, 668);
            Label info = Dialogs.Put(f, Ui.Lbl("", ""), 16, 12);
            info.AutoSize = false;
            info.Size = new Size(748, 40);

            Dialogs.Put(f, Ui.Lbl("On this PC", "section"), 16, 58);
            ListView lvInst = new BufferedListView();
            lvInst.Size = new Size(748, 140);
            lvInst.Columns.Add("Version", 130);
            lvInst.Columns.Add("Folder", 215);
            lvInst.Columns.Add("From", 70);
            lvInst.Columns.Add("Date", 130);
            lvInst.Columns.Add("In use", 190);
            lvInst.MultiSelect = false;
            ListStyle.Attach(lvInst);
            Dialogs.Put(f, lvInst, 16, 80);

            RoundButton bUse = Ui.Btn("Use selected", 120, null, "primary");
            RoundButton bAuto = Ui.Btn("Automatic (latest)", 150, null, "");
            RoundButton bFolder = Ui.Btn("Open folder", 110, null, "");
            Dialogs.Put(f, bUse, 16, 228);
            Dialogs.Put(f, bAuto, 144, 228);
            Dialogs.Put(f, bFolder, 302, 228);

            Dialogs.Put(f, Ui.Lbl("Recent Roblox releases (from Roblox's download servers)", "section"), 16, 274);
            ListView lvHist = new BufferedListView();
            lvHist.Size = new Size(748, 190);
            lvHist.Columns.Add("Version", 130);
            lvHist.Columns.Add("Released", 150);
            lvHist.Columns.Add("Folder", 230);
            lvHist.Columns.Add("Status", 220);
            lvHist.MultiSelect = false;
            ListStyle.Attach(lvHist);
            Dialogs.Put(f, lvHist, 16, 296);

            RoundButton bDl = Ui.Btn("Download and use selected", 220, null, "primary");
            RoundButton bDlOnly = Ui.Btn("Download only", 130, null, "");
            Dialogs.Put(f, bDl, 16, 494);
            Dialogs.Put(f, bDlOnly, 244, 494);
            Label status = Dialogs.Put(f, Ui.Lbl("", "muted"), 388, 500);
            status.AutoSize = false;
            status.Size = new Size(376, 20);

            Label warn = Dialogs.Put(f, Ui.Lbl("Roblox only lets up-to-date clients (or very recent ones) join games - an old version usually shows \"out of date\". Use this to keep a recent version, to try the newest one, or to run a different build next to your normal install. Downloads come from Roblox's own servers into AltHub's folder; your normal Roblox install is never changed. Versions you choose are launched with the direct launch method.", "muted"), 16, 536);
            warn.AutoSize = false;
            warn.Size = new Size(748, 70);
            RoundButton bClose = Ui.Btn("Close", 100, null, "");
            Dialogs.Put(f, bClose, 664, 616);
            bClose.DialogResult = DialogResult.OK;
            f.AcceptButton = bClose;

            bool busy = false;
            List<DeployEntry> hist = new List<DeployEntry>();
            string[] latest = null;

            Action refresh = null;
            refresh = delegate
            {
                List<RobloxVersion> inst = RobloxVersions.Installed();
                string pin = Store.Settings.RobloxVersion;
                string autoExe = null;
                try { string saved = Store.Settings.RobloxVersion; Store.Settings.RobloxVersion = ""; autoExe = Launcher.FindRobloxExe(); Store.Settings.RobloxVersion = saved; } catch (Exception) { }
                string autoFolder = autoExe == null ? "" : Path.GetFileName(Path.GetDirectoryName(autoExe));
                lvInst.Items.Clear();
                foreach (RobloxVersion v in inst)
                {
                    bool used = pin.Length > 0 ? v.Folder == pin : v.Folder == autoFolder;
                    ListViewItem it = new ListViewItem(new string[] { v.FileVersion, v.Folder, v.Source, v.Date.ToString("yyyy-MM-dd HH:mm"),
                        used ? (pin.Length > 0 ? "chosen by you" : "automatic (newest)") : "" });
                    it.Tag = v;
                    it.ForeColor = used ? Theme.Good : Theme.Text;
                    lvInst.Items.Add(it);
                }
                HashSet<string> have = new HashSet<string>(inst.Select(x => x.Folder));
                lvHist.Items.Clear();
                foreach (DeployEntry e in hist)
                {
                    string st = have.Contains(e.Folder) ? "on this PC" : "";
                    if (latest != null && e.Folder == latest[1]) st = (st.Length > 0 ? st + ", " : "") + "newest";
                    ListViewItem it = new ListViewItem(new string[] { e.Version, e.Date == DateTime.MinValue ? "" : e.Date.ToString("yyyy-MM-dd HH:mm"), e.Folder, st });
                    it.Tag = e;
                    lvHist.Items.Add(it);
                }
                info.Text = (pin.Length > 0 ? "AltHub launches the version you chose: " + pin + "." : "AltHub uses the Roblox that Roblox installed (automatic).")
                    + "\r\nNewest public version: " + (latest == null ? "(could not reach Roblox)" : latest[0] + "  (" + latest[1] + ")");
            };

            Action<string, bool> download = delegate(string folder, bool use)
            {
                if (busy) return;
                busy = true;
                bDl.Enabled = false; bDlOnly.Enabled = false;
                BeginInvoke(new Action(async delegate
                {
                    string err = await RobloxVersions.Download(folder, delegate(string s) { status.Text = s; });
                    busy = false;
                    bDl.Enabled = true; bDlOnly.Enabled = true;
                    if (err != null) { status.Text = "Failed: " + err; AddLog("Roblox version download failed: " + err); }
                    else
                    {
                        status.Text = folder + " is ready.";
                        if (use) { Store.Settings.RobloxVersion = folder; Store.SaveSettings(); AddLog("AltHub will now launch Roblox " + folder + "."); }
                        else AddLog("Downloaded Roblox " + folder + ".");
                    }
                    refresh();
                }));
            };

            bUse.Click += delegate
            {
                if (lvInst.SelectedItems.Count == 0) { status.Text = "Select a version in the top list first."; return; }
                RobloxVersion v = (RobloxVersion)lvInst.SelectedItems[0].Tag;
                Store.Settings.RobloxVersion = v.Folder;
                Store.SaveSettings();
                AddLog("AltHub will now launch Roblox " + v.Folder + " (direct launch).");
                refresh();
            };
            bAuto.Click += delegate { Store.Settings.RobloxVersion = ""; Store.SaveSettings(); AddLog("AltHub uses the Roblox that Roblox installed again."); refresh(); };
            bFolder.Click += delegate
            {
                string dir = lvInst.SelectedItems.Count > 0 ? Path.GetDirectoryName(((RobloxVersion)lvInst.SelectedItems[0].Tag).Exe) : RobloxVersions.OwnRoot;
                try { Directory.CreateDirectory(dir); Process.Start("explorer.exe", "\"" + dir + "\""); } catch (Exception) { }
            };
            bDl.Click += delegate
            {
                if (lvHist.SelectedItems.Count == 0) { status.Text = "Select a release in the lower list first."; return; }
                download(((DeployEntry)lvHist.SelectedItems[0].Tag).Folder, true);
            };
            bDlOnly.Click += delegate
            {
                if (lvHist.SelectedItems.Count == 0) { status.Text = "Select a release in the lower list first."; return; }
                download(((DeployEntry)lvHist.SelectedItems[0].Tag).Folder, false);
            };

            Theme.Apply(f);
            refresh();
            f.Shown += async delegate
            {
                status.Text = "Asking Roblox for the release list...";
                latest = await RobloxVersions.Latest();
                hist = await RobloxVersions.History();
                status.Text = hist.Count == 0 ? "Could not load the release list from Roblox." : "";
                refresh();
            };
            f.ShowDialog(this);
            f.Dispose();
        }
    }
}
