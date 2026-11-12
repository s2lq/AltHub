using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AltHub
{
    // First-run setup: language and theme, multi-instance, a quick config, where to keep the data, adding accounts.
    public partial class MainForm
    {
        void FirstRun()
        {
            if (Store.Settings.FirstRunDone) return;
            RunSetupWizard();
        }

        void RunSetupWizard()
        {
            AppSettings S = Store.Settings;
            string lang = S.Language;
            string theme = S.Theme;
            bool multi = true;
            int cfg = -1;                       // index in QuickConfigs.All, -1 = keep as is
            bool portable = Store.IsPortable;
            int addMode = 0;                    // 0 browser, 1 paste, 2 later

            Form f = Dialogs.Make("AltHub - setup", 560, 470);
            f.StartPosition = FormStartPosition.CenterScreen;
            f.ShowInTaskbar = true;
            Panel content = new Panel();
            content.Location = new Point(0, 0);
            content.Size = new Size(560, 400);
            f.Controls.Add(content);
            Label stepLbl = Ui.Lbl("", "muted");
            stepLbl.Location = new Point(18, 436);
            f.Controls.Add(stepLbl);
            RoundButton bBack = Ui.Btn("Back", 90, null, "");
            RoundButton bNext = Ui.Btn("Next", 110, null, "primary");
            RoundButton bSkip = Ui.Btn("Skip for now", 130, null, "");
            bNext.Location = new Point(f.ClientSize.Width - 110 - 16, 424);
            bBack.Location = new Point(bNext.Left - 90 - 8, 424);
            bSkip.Location = new Point(bBack.Left - 130 - 8, 424);
            f.Controls.Add(bBack);
            f.Controls.Add(bNext);
            f.Controls.Add(bSkip);
            const int Steps = 6;
            int step = 0;
            bool finished = false;

            Action<int> show = null;
            show = delegate(int n)
            {
                step = n;
                content.Controls.Clear();
                int y = 20;
                Action<string, string> head = delegate(string t, string sub)
                {
                    Label a = Ui.Lbl(Lang.T(t), "title"); a.Location = new Point(24, y); content.Controls.Add(a); y += 40;
                    if (!string.IsNullOrEmpty(sub))
                    {
                        Label b = Ui.Lbl(Lang.T(sub), "muted"); b.AutoSize = false; b.Size = new Size(510, 64); b.Location = new Point(24, y); content.Controls.Add(b); y += 70;
                    }
                };
                switch (n)
                {
                    case 0:
                    {
                        head("Welcome to AltHub", "Run several Roblox accounts at once. This takes a minute: pick a language and a look, and you are ready.");
                        Label l1 = Ui.Fld("Language", 100); l1.Location = new Point(24, y); content.Controls.Add(l1);
                        List<string[]> langs = Lang.Available();
                        string[] names = langs.Select(x => x[1]).ToArray();
                        DarkCombo cl = Ui.Cmb(300, names);
                        int li = langs.FindIndex(x => x[0] == lang);
                        cl.SelectedIndex = li >= 0 ? li : 0;
                        cl.Location = new Point(130, y); content.Controls.Add(cl);
                        cl.SelectedIndexChanged += delegate
                        {
                            if (cl.SelectedIndex < 0) return;
                            lang = langs[cl.SelectedIndex][0];
                            Lang.Load(lang);              // the wizard switches language at once
                            f.BeginInvoke((MethodInvoker)delegate { show(0); });
                        };
                        y += 44;
                        Label l2 = Ui.Fld("Theme", 100); l2.Location = new Point(24, y); content.Controls.Add(l2);
                        DarkCombo ct = Ui.Cmb(300, Themes.Names().ToArray());
                        int ti = ct.Items.IndexOf(Themes.NameFromSetting(theme));
                        ct.SelectedIndex = ti >= 0 ? ti : 0;
                        ct.Location = new Point(130, y); content.Controls.Add(ct);
                        ct.SelectedIndexChanged += delegate
                        {
                            theme = Themes.SettingFromName(ct.Text);
                            Theme.Load(theme, S.AccentHex);
                            Theme.Apply(f);
                            DarkMode.Apply(Theme.Dark);
                        };
                        break;
                    }
                    case 1:
                    {
                        head("Multi-instance", "To run several Roblox windows the app holds Roblox's 'single instance' lock. Nothing is injected into or changed in Roblox. Roblox may treat multiple clients as against its rules and can change the client at any time: you use this at your own risk, with your own accounts.");
                        CheckBox c = Ui.Chk("Turn multi-instance ON (recommended)", multi, null);
                        c.Location = new Point(24, y); content.Controls.Add(c);
                        c.CheckedChanged += delegate { multi = c.Checked; };
                        break;
                    }
                    case 2:
                    {
                        head("Quick config", "How heavy may each Roblox window be? You can change this later on the Perf tab.");
                        List<RadioButton> rads = new List<RadioButton>();
                        RadioButton keep = Ui.Rad(Lang.T("Keep the default settings"), cfg < 0, null);
                        keep.Location = new Point(24, y); content.Controls.Add(keep); y += 30;
                        keep.CheckedChanged += delegate { if (keep.Checked) cfg = -1; };
                        for (int i = 0; i < QuickConfigs.All.Count; i++)
                        {
                            int idx = i;
                            QuickConfig q = QuickConfigs.All[i];
                            RadioButton r = Ui.Rad(Lang.T(q.Name) + "  -  " + Lang.T(q.Tagline), cfg == i, null);
                            r.Location = new Point(24, y); content.Controls.Add(r); y += 30;
                            r.CheckedChanged += delegate { if (r.Checked) cfg = idx; };
                        }
                        break;
                    }
                    case 3:
                    {
                        head("Your data", "Where should AltHub keep your accounts and settings?");
                        RadioButton rn = Ui.Rad(Lang.T("Normal (in this Windows user's AppData)"), !portable, null);
                        rn.Location = new Point(24, y); content.Controls.Add(rn); y += 32;
                        RadioButton rp = Ui.Rad(Lang.T("Portable (an AltHubData folder next to AltHub.exe, for a USB stick)"), portable, null);
                        rp.Location = new Point(24, y); content.Controls.Add(rp); y += 32;
                        rp.CheckedChanged += delegate { portable = rp.Checked; };
                        Label note = Ui.Lbl(Lang.T("Portable data is encrypted for this Windows user unless you set a password in Settings > Security."), "muted");
                        note.AutoSize = false; note.Size = new Size(510, 40); note.Location = new Point(24, y); content.Controls.Add(note);
                        break;
                    }
                    case 4:
                    {
                        head("Add your accounts", "How do you want to add your first account?");
                        RadioButton a0 = Ui.Rad(Lang.T("Log in with a browser"), addMode == 0, null);
                        a0.Location = new Point(24, y); content.Controls.Add(a0); y += 32;
                        a0.CheckedChanged += delegate { if (a0.Checked) addMode = 0; };
                        RadioButton a1 = Ui.Rad(Lang.T("Paste cookies"), addMode == 1, null);
                        a1.Location = new Point(24, y); content.Controls.Add(a1); y += 32;
                        a1.CheckedChanged += delegate { if (a1.Checked) addMode = 1; };
                        RadioButton a2 = Ui.Rad(Lang.T("Skip for now"), addMode == 2, null);
                        a2.Location = new Point(24, y); content.Controls.Add(a2);
                        a2.CheckedChanged += delegate { if (a2.Checked) addMode = 2; };
                        break;
                    }
                    default:
                    {
                        head("All set", "Press Finish to apply your choices." + (lang != S.Language || portable != Store.IsPortable ? " AltHub restarts to switch language or data location." : ""));
                        break;
                    }
                }
                Theme.Apply(content);
                stepLbl.Text = (n + 1) + " / " + Steps;
                bBack.Enabled = n > 0;
                bBack.Text = Lang.T("Back");
                bNext.Text = Lang.T(n == Steps - 1 ? "Finish" : "Next");
                bSkip.Text = Lang.T("Skip for now");
                bBack.Invalidate(); bNext.Invalidate(); bSkip.Invalidate();
            };
            bNext.Click += delegate { if (step == Steps - 1) { finished = true; f.DialogResult = DialogResult.OK; } else show(step + 1); };
            bBack.Click += delegate { if (step > 0) show(step - 1); };
            bSkip.Click += delegate { f.DialogResult = DialogResult.Cancel; };
            Theme.Apply(f);
            show(0);
            f.ShowDialog(this);
            f.Dispose();

            S.FirstRunDone = true;
            if (!finished)
            {
                // skipped: keep the defaults; multi-instance stays off until it is turned on in Settings
                Lang.Load(S.Language);
                Theme.Load(S.Theme, S.AccentHex);
                S.MultiInstance = false;
                Store.SaveSettings();
                SyncSettingsControls();
                return;
            }
            S.Language = lang;
            S.Theme = theme;
            S.MultiInstance = multi;
            if (cfg >= 0) QuickConfigs.All[cfg].Apply(S);
            Store.SaveSettings();
            Theme.Load(S.Theme, S.AccentHex);
            if (cfg >= 0) { CpuCap.Release(); RamCap.Reset(); RebuildSettingPages(); }
            else { ApplyTheme(); SyncSettingsControls(); }
            if (multi) MultiInstance.Start();

            if (portable && !Store.IsPortable && !EnsurePortablePassword(this)) portable = false;
            if (portable != Store.IsPortable)
            {
                string err = Store.SetPortable(portable);
                if (err != null) MessageBox.Show(this, "Could not switch: " + err, "Portable mode", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else { quitting = true; Application.Restart(); return; }
            }
            if (lang != "en" && Lang.Current != lang) Lang.Load(lang);
            if (lang != Lang.Current || lang != "en")
            {
                // the interface texts were built in the old language: restart so every page uses the new one
                quitting = true; Application.Restart(); return;
            }
            if (addMode == 0) AddByBrowser();
            else if (addMode == 1) AddByPaste();
        }
    }
}
