using System;
using System.Windows.Forms;

namespace AltHub
{
    // In-app keyboard shortcuts (work while the AltHub window is focused). Global ones (any program) are registered in
    // RegisterHotkeys: Ctrl+Alt+F5 launch all, F6 stop all, F7 show / hide the window, F9-F11 layout.
    public partial class MainForm
    {
        static readonly string[] TabOrder = new string[] { "Home", "Launch", "Servers", "Private", "Health", "Layout", "Monitor", "Presets", "Perf", "Settings", "Help" };

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            try
            {
                if (HandleShortcut(keyData)) return true;
            }
            catch (Exception ex) { Log.Error("shortcut", ex); }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        bool HandleShortcut(Keys k)
        {
            // pages: Ctrl+1 .. Ctrl+9 and Ctrl+0
            if ((k & Keys.Modifiers) == Keys.Control)
            {
                Keys code = k & Keys.KeyCode;
                if (code >= Keys.D1 && code <= Keys.D9) { ShowPage(TabOrder[(int)code - (int)Keys.D1]); return true; }
                if (code == Keys.D0) { ShowPage("Help"); return true; }
            }
            switch (k)
            {
                case Keys.Control | Keys.L: LaunchSelected(); return true;
                case Keys.Control | Keys.Shift | Keys.L: LaunchAllListed(); return true;
                case Keys.Control | Keys.Shift | Keys.S: StopAll(); return true;
                case Keys.Control | Keys.Shift | Keys.M: ToggleMuteSelected(); return true;
                case Keys.Control | Keys.N: AddByBrowser(); return true;
                case Keys.Control | Keys.E:
                case Keys.F2: EditSelected(); return true;
                case Keys.Control | Keys.F: tbSearch.Focus(); return true;
                case Keys.Control | Keys.R: CheckAccounts(); return true;
                case Keys.F5: RefreshPresence(); RefreshRobux(false); return true;
                case Keys.Control | Keys.G: SetView(!cardsView); return true;
                case Keys.Control | Keys.T: ShowTagMenu(); return true;
                case Keys.Control | Keys.M: OpenMacro(); return true;
                case Keys.Control | Keys.Oemcomma: ShowPage("Settings"); return true;
                case Keys.Control | Keys.Tab: StepPage(1); return true;
                case Keys.Control | Keys.Shift | Keys.Tab: StepPage(-1); return true;
                case Keys.Control | Keys.Shift | Keys.P: ShowPage("Private"); return true;
                case Keys.Escape:
                    if (tbSearch != null && tbSearch.Focused && tbSearch.Text.Length > 0) { tbSearch.Text = ""; return true; }
                    return false;
            }
            return false;
        }

        void StepPage(int dir)
        {
            int i = Array.IndexOf(TabOrder, currentPage);
            if (i < 0) i = 0;
            ShowPage(TabOrder[(i + dir + TabOrder.Length) % TabOrder.Length]);
        }
    }
}
