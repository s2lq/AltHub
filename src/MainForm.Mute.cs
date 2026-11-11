using System;
using System.Collections.Generic;
using System.Linq;

namespace AltHub
{
    // Mute button: mutes the Roblox sound of the selected accounts - before they are launched (the mute is applied as soon
    // as their window starts) or while they run. The state is saved per account.
    public partial class MainForm
    {
        void ToggleMuteSelected()
        {
            List<Account> sel = SelectedAccounts();
            if (sel.Count == 0) { AddLog("Select one or more accounts first."); return; }
            bool mute = !sel.All(a => a.Muted);                 // all muted already -> unmute, otherwise mute them all
            foreach (Account a in sel) a.Muted = mute;
            Store.SaveAccounts();
            HashSet<int> pids = RunningPidsOf(sel);
            if (pids.Count > 0) AudioMute.Set(pids, mute);
            RefreshAccountList();
            AddLog((mute ? "Muted " : "Unmuted ") + sel.Count + " account(s)" + (mute && pids.Count == 0 ? " - the sound is muted as soon as their window starts." : "."));
        }

        HashSet<int> RunningPidsOf(List<Account> accs)
        {
            HashSet<int> pids = new HashSet<int>();
            HashSet<string> accIds = new HashSet<string>(accs.Select(a => a.Id));
            foreach (Instance i in Launcher.Instances.ToList())
                if (i.Account != null && accIds.Contains(i.Account.Id) && Launcher.IsAlive(i.Pid)) pids.Add(i.Pid);
            return pids;
        }

        // Every few seconds: keeps muted accounts muted (Roblox only creates its audio session once it plays sound).
        void MuteTick()
        {
            List<Account> muted = Store.Accounts.Where(a => a.Muted).ToList();
            if (muted.Count == 0) return;
            HashSet<int> pids = RunningPidsOf(muted);
            if (pids.Count > 0) AudioMute.Set(pids, true);
        }
    }
}
