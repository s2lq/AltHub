using System;
using System.Windows.Forms;

namespace AltHub
{
    public partial class MainForm
    {
        ComboBox cbRamCap;
        CheckBox chkRamActive;
        static readonly string[] RamCapIds = new string[] { "Auto250", "Fixed250", "Fixed500", "Off" };

        void BuildRamCapSection(PageBuilder p)
        {
            AppSettings S = Store.Settings;
            p.Section("Memory per Roblox window");
            cbRamCap = Ui.Cmb(330, "250 MB - raise to 500 MB if a window struggles", "250 MB (fixed)", "500 MB (fixed)", "No limit");
            cbRamCap.SelectedIndex = Math.Max(0, Array.IndexOf(RamCapIds, S.RamCapMode));
            p.Opt("Keeps every Roblox window around 250 MB of RAM.",
                "Gives each Roblox window a hard working-set limit, so Task Manager shows about 250 MB for each one and it stays there. A new window is left alone for about 75 seconds while it loads.\n\nWhat does not fit in the limit stays available to Windows (standby list / page file), so the game keeps working. A window that really needs more memory will stutter: in the first mode the app notices that (constant page faults), moves that window to 500 MB, and takes the limit off if even that is too tight.\n\nThis works on top of 'Max alts' and the RAM cleaner below.",
                Ui.Fld("Limit", 60), cbRamCap);
            chkRamActive = Ui.Chk("Do not limit the window I am playing", S.RamCapSkipActive, null);
            p.Opt("The Roblox window in front is left unlimited; the limit comes back when you switch away.",
                "Recommended: your active game keeps all the memory it wants, while every background alt stays small.",
                chkRamActive);
            EventHandler ch = delegate { OnRamCapChanged(); };
            cbRamCap.SelectedIndexChanged += ch;
            chkRamActive.CheckedChanged += ch;
        }

        void OnRamCapChanged()
        {
            if (loadingSettings || cbRamCap == null) return;
            Store.Settings.RamCapMode = RamCapIds[Math.Max(0, cbRamCap.SelectedIndex)];
            Store.Settings.RamCapSkipActive = chkRamActive.Checked;
            Store.SaveSettings();
            RamCap.Reset();                                  // every window starts again from the new setting
            AddLog(RamCap.Enabled ? "Memory limit per Roblox window: " + cbRamCap.Text + "." : "Memory limit per Roblox window: off.");
        }
    }
}
