using System;
using System.Windows.Forms;

namespace AltHub
{
    public partial class MainForm
    {
        ComboBox cbCpuCap;
        NumericUpDown numCpuCap;
        CheckBox chkCpuActive;
        static readonly string[] CpuCapIds = new string[] { "Off", "Each", "Total" };

        void BuildCpuCapSection(PageBuilder p)
        {
            AppSettings S = Store.Settings;
            p.Section("Maximum CPU");
            cbCpuCap = Ui.Cmb(330, "No limit", "Each Roblox window at most...", "All Roblox windows together at most...");
            cbCpuCap.SelectedIndex = Math.Max(0, Array.IndexOf(CpuCapIds, S.CpuCapMode));
            numCpuCap = Ui.Num(1, 100, Math.Max(1, Math.Min(100, S.CpuCapPercent)), 70);
            p.Opt("Caps how much of your CPU Roblox may use.",
                "Uses a Windows job object (Windows 8 or newer). The percentage is of the WHOLE PC, like Task Manager shows it: with 10 % on an 8-core PC one window can use at most 10 % of all the cores.\n\n'Each window' gives every window its own cap, so ten alts at 10 % can still use the whole CPU together. 'All together' shares one cap between all Roblox windows.\n\nA capped window runs slower (lower FPS) instead of hogging the CPU. Combine it with the FPS cap and the memory limit for many alts on a weak PC.",
                Ui.Fld("Mode", 60), cbCpuCap, Ui.Fld("% of the PC", 90), numCpuCap);
            chkCpuActive = Ui.Chk("Do not limit the window I am playing (Each window mode)", S.CpuCapSkipActive, null);
            p.Opt("The Roblox window in front keeps full speed; the cap comes back when you switch away.",
                "Not possible in the 'All together' mode, because windows cannot leave a shared cap.",
                chkCpuActive);
            EventHandler ch = delegate { OnCpuCapChanged(); };
            cbCpuCap.SelectedIndexChanged += ch;
            numCpuCap.ValueChanged += ch;
            chkCpuActive.CheckedChanged += ch;
        }

        void OnCpuCapChanged()
        {
            if (loadingSettings || cbCpuCap == null) return;
            Store.Settings.CpuCapMode = CpuCapIds[Math.Max(0, cbCpuCap.SelectedIndex)];
            Store.Settings.CpuCapPercent = (int)numCpuCap.Value;
            Store.Settings.CpuCapSkipActive = chkCpuActive.Checked;
            Store.SaveSettings();
            CpuCap.Release();                                // every window starts again from the new setting
            AddLog(CpuCap.Enabled ? "CPU limit: " + cbCpuCap.Text.TrimEnd('.') + " " + numCpuCap.Value + " % of the PC." : "CPU limit: off.");
        }
    }
}
