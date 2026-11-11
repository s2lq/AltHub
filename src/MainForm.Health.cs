using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AltHub
{
    // The Health page: which logins work, which are signed out or locked, how often each account crashes, one-click recovery
    // (check, re-login in the browser, refresh cookies, unlock help, clean up) and the event log.
    public partial class MainForm
    {
        ListView lvHealth, lvEvents;
        Label lblHealthSum;
        CheckBox chkHealthAuto;
        NumericUpDown numHealthHours;
        bool healthBusy;

        void BuildHealthPage()
        {
            Panel page = NewPage("Health");
            PageBuilder p = new PageBuilder(page, PageW);
            p.Title("Account health", "See which logins work, which are signed out or locked, and fix them from here.");
            lblHealthSum = Ui.Lbl("", "");
            p.Row(lblHealthSum);

            p.Section("Your accounts");
            lvHealth = new BufferedListView();
            lvHealth.Size = new Size(PageW - PageBuilder.PadX * 2, 230);
            lvHealth.Columns.Add("Account", 130);
            lvHealth.Columns.Add("Status", 110);
            lvHealth.Columns.Add("Checked", 90);
            lvHealth.Columns.Add("Last launch", 95);
            lvHealth.Columns.Add("Crashes 7d", 80);
            lvHealth.Columns.Add("What to do", 200);
            lvHealth.MultiSelect = true;
            ListStyle.Attach(lvHealth);
            lvHealth.SelectedIndexChanged += delegate { RefreshEvents(); };
            p.Add(lvHealth);

            p.Opt("Check that the logins still work.",
                "Asks Roblox about every account (or only the ones you select in this list). Rate limits are handled by waiting and retrying. The result is saved on each account, and a change (signed out, locked, or working again) is written to the event log.",
                Ui.Btn("Check all now", 130, delegate { HealthCheck(AllHealthAccounts(), false); }, "primary"),
                Ui.Btn("Check selected", 130, delegate { HealthCheck(SelectedHealth(), false); }, ""));
            p.Opt("Fix the ones that stopped working.",
                "Re-login opens a browser for each selected account that is signed out, with its username (and saved password, if you saved one) filled in - you press Log In and solve any captcha, and the new login replaces the old one with all settings kept. Refresh cookies renews the login of working accounts before it expires (it signs those accounts out of other sessions). Unlock help explains what to do for locked accounts.",
                Ui.Btn("Re-login selected", 150, delegate { ReloginSelected(); }, "primary"),
                Ui.Btn("Refresh cookies", 140, delegate { RefreshCookiesSelected(); }, ""),
                Ui.Btn("Unlock help", 120, delegate { UnlockHelp(); }, ""));
            p.Opt("Tidy up.",
                "Remove broken accounts takes the selected accounts out of AltHub (not off Roblox; a backup is made first). Export problems saves a CSV list of every account that is not healthy, handy when asking for help.",
                Ui.Btn("Remove broken accounts", 190, delegate { RemoveBroken(); }, "danger"),
                Ui.Btn("Export problems...", 150, delegate { ExportProblems(); }, ""));

            p.Section("Automatic checks");
            chkHealthAuto = Ui.Chk("Check all logins in the background", Store.Settings.HealthAuto, null);
            numHealthHours = Ui.Num(1, 72, Math.Max(1, Store.Settings.HealthHours), 70);
            p.Opt("Catches a signed-out or locked account before you launch it.",
                "Every few hours AltHub quietly checks every account and updates this page. It does not pop up messages; broken accounts show in red here and in the account list.",
                chkHealthAuto, Ui.Fld("every (hours)", 100), numHealthHours);
            EventHandler ch = delegate
            {
                if (loadingSettings) return;
                Store.Settings.HealthAuto = chkHealthAuto.Checked;
                Store.Settings.HealthHours = (int)numHealthHours.Value;
                Store.SaveSettings();
            };
            chkHealthAuto.CheckedChanged += ch;
            numHealthHours.ValueChanged += ch;

            p.Section("Event log");
            lvEvents = new BufferedListView();
            lvEvents.Size = new Size(PageW - PageBuilder.PadX * 2, 230);
            lvEvents.Columns.Add("When", 120);
            lvEvents.Columns.Add("Account", 110);
            lvEvents.Columns.Add("Event", 100);
            lvEvents.Columns.Add("Details", 300);
            lvEvents.MultiSelect = false;
            ListStyle.Attach(lvEvents);
            p.Add(lvEvents);
            p.Opt("Every crash, kick, failed launch and recovery.",
                "Shows the events of the account(s) selected above, or of all accounts when none is selected. Crashes, kicks and failed launches come from the watcher and the launcher; signed-out and locked accounts from the login checks.",
                Ui.Btn("Export log...", 120, delegate { ExportEvents(); }, ""),
                Ui.Btn("Clear log", 100, delegate { ClearEvents(); }, ""));

            Journal.Changed += delegate
            {
                if (IsDisposed) return;
                if (InvokeRequired) { try { BeginInvoke(new Action(RefreshEvents)); } catch (Exception) { } }
                else RefreshEvents();
            };
        }

        // ---------------- status of one account ----------------
        void HealthOf(Account a, out string text, out Color color, out string advice)
        {
            LiveInfo li = AppState.Live(a);
            int crashes = Journal.Count(a.Id, "Crash", 7) + Journal.Count(a.Id, "Kicked", 7);
            if (li.Locked || a.LastCheck == "locked") { text = "Locked"; color = Theme.Bad; advice = "Unlock it on roblox.com (see Unlock help)"; return; }
            if (li.CookieBad || a.LastCheck == "expired") { text = "Signed out"; color = Theme.Bad; advice = "Re-login selected"; return; }
            if (a.LastCheck == "error") { text = "Could not check"; color = Theme.Warn; advice = "Roblox did not answer - check again later"; return; }
            if (a.LastCheckTicks == 0) { text = "Not checked"; color = Theme.Muted; advice = "Press Check all now"; return; }
            if (crashes >= 3) { text = "Working"; color = Theme.Warn; advice = "Crashes a lot - lower the FPS cap / graphics"; return; }
            text = "Working"; color = Theme.Good; advice = "";
        }

        List<Account> AllHealthAccounts() { return Store.Accounts.ToList(); }

        List<Account> SelectedHealth()
        {
            List<Account> r = new List<Account>();
            if (lvHealth != null)
                foreach (ListViewItem it in lvHealth.SelectedItems) { Account a = it.Tag as Account; if (a != null) r.Add(a); }
            if (r.Count == 0) AddLog("Select one or more accounts in the health list first.");
            return r;
        }

        // ---------------- refresh ----------------
        void RefreshHealthPage()
        {
            if (lvHealth == null) return;
            HashSet<string> keep = new HashSet<string>();
            foreach (ListViewItem it in lvHealth.SelectedItems) { Account k = it.Tag as Account; if (k != null) keep.Add(k.Id); }
            int good = 0, signed = 0, locked = 0, unchecked_ = 0;
            lvHealth.BeginUpdate();
            lvHealth.Items.Clear();
            foreach (Account a in Store.Accounts.OrderBy(x => x.Order))
            {
                string text, advice; Color col;
                HealthOf(a, out text, out col, out advice);
                if (text == "Locked") locked++; else if (text == "Signed out") signed++; else if (text == "Not checked" || text == "Could not check") unchecked_++; else good++;
                string checkedAgo = a.LastCheckTicks > 0 ? Util.Ago(a.LastCheckTicks) : "-";
                string launchAgo = a.LastUsedTicks > 0 ? Util.Ago(a.LastUsedTicks) : "never";
                int cr = Journal.Count(a.Id, "Crash", 7) + Journal.Count(a.Id, "Kicked", 7);
                ListViewItem it = new ListViewItem(new string[] { AppState.Name(a), text, checkedAgo, launchAgo, cr.ToString(), advice });
                it.Tag = a;
                it.ForeColor = col == Theme.Good ? Theme.Text : col;
                lvHealth.Items.Add(it);
                if (keep.Contains(a.Id)) it.Selected = true;
            }
            lvHealth.EndUpdate();
            lblHealthSum.Text = good + " working   -   " + signed + " signed out   -   " + locked + " locked   -   " + unchecked_ + " not checked";
            lblHealthSum.ForeColor = (signed + locked) > 0 ? Theme.Warn : Theme.Good;
            RefreshEvents();
        }

        void RefreshEvents()
        {
            if (lvEvents == null) return;
            HashSet<string> ids = new HashSet<string>();
            foreach (ListViewItem it in lvHealth.SelectedItems) { Account a = it.Tag as Account; if (a != null) ids.Add(a.Id); }
            IEnumerable<JournalEntry> all = Journal.All();
            if (ids.Count > 0) all = all.Where(e => ids.Contains(e.AccountId));
            lvEvents.BeginUpdate();
            lvEvents.Items.Clear();
            foreach (JournalEntry e in all.OrderByDescending(x => x.Ticks).Take(300))
            {
                ListViewItem it = new ListViewItem(new string[] { new DateTime(e.Ticks, DateTimeKind.Utc).ToLocalTime().ToString("MM-dd HH:mm"), e.Account, EventName(e.Kind), e.Detail });
                it.ForeColor = (e.Kind == "Crash" || e.Kind == "Locked" || e.Kind == "CookieExpired" || e.Kind == "Breaker") ? Theme.Bad
                    : (e.Kind == "Recovered" ? Theme.Good : (e.Kind == "Kicked" || e.Kind == "LaunchFailed" ? Theme.Warn : Theme.Text));
                lvEvents.Items.Add(it);
            }
            lvEvents.EndUpdate();
        }

        static string EventName(string kind)
        {
            switch (kind)
            {
                case "Crash": return "Crashed";
                case "Kicked": return "Kicked / left";
                case "LaunchFailed": return "Launch failed";
                case "Relaunch": return "Relaunched";
                case "Breaker": return "Crash breaker";
                case "CookieExpired": return "Signed out";
                case "Locked": return "Locked";
                case "Recovered": return "Recovered";
            }
            return kind;
        }

        // ---------------- checking ----------------
        // Stores the result of a login check on the account and writes a change to the event log.
        void RecordCheck(Account a, UserInfo u)
        {
            string prev = a.LastCheck ?? "";
            string now = u.Valid ? "ok" : (u.Locked ? "locked" : (u.Error != null && u.Error.StartsWith("Cookie expired") ? "expired" : "error"));
            a.LastCheckTicks = DateTime.UtcNow.Ticks;
            a.LastCheck = now;
            a.LastCheckText = u.Valid ? "" : (u.Error ?? "");
            if (now == prev || now == "error") return;
            if (now == "expired") Journal.Add(a, "CookieExpired", "The saved login no longer works (signed out by Roblox).");
            else if (now == "locked") Journal.Add(a, "Locked", "Roblox returned HTTP 403 (locked / blocked).");
            else if (now == "ok" && (prev == "expired" || prev == "locked")) Journal.Add(a, "Recovered", "The login works again.");
        }

        async void HealthCheck(List<Account> list, bool silent)
        {
            if (healthBusy || list.Count == 0) return;
            healthBusy = true;
            try
            {
                int ok = 0, bad = 0;
                if (!silent) AddLog("Checking " + list.Count + " account(s)...");
                foreach (Account a in list)
                {
                    if (string.IsNullOrEmpty(a.Cookie)) continue;
                    UserInfo u = await Api.Authenticated(a.Cookie);
                    if (!u.Valid && u.Error.Contains("429")) { await System.Threading.Tasks.Task.Delay(8000); u = await Api.Authenticated(a.Cookie); }
                    LiveInfo li = AppState.Live(a);
                    if (u.Valid)
                    {
                        a.Username = u.Name; a.DisplayName = u.Display; a.UserId = u.Id;
                        li.CookieBad = false; li.Locked = false; li.LockedAlerted = false; a.LoginLost = false;
                        ok++;
                    }
                    else if (u.Locked) { li.Locked = true; li.CookieBad = false; bad++; }
                    else if (u.Error.StartsWith("Cookie expired")) { li.CookieBad = true; li.Locked = false; bad++; }
                    RecordCheck(a, u);
                    await System.Threading.Tasks.Task.Delay(300);
                }
                Store.Settings.HealthLastTicks = DateTime.UtcNow.Ticks;
                Store.SaveSettings();
                Store.SaveAccounts();
                if (!IsDisposed) { RefreshAccountList(); RefreshHealthPage(); }
                if (!silent) AddLog("Check done: " + ok + " working" + (bad > 0 ? ", " + bad + " with a problem." : "."));
            }
            catch (Exception ex) { Log.Error("HealthCheck", ex); }
            healthBusy = false;
        }

        // Called every second from the main timer.
        void HealthTick()
        {
            if (!Store.Settings.HealthAuto || healthBusy || Store.Accounts.Count == 0 || tickN < 90) return;
            DateTime last = Store.Settings.HealthLastTicks > 0 ? new DateTime(Store.Settings.HealthLastTicks, DateTimeKind.Utc) : DateTime.MinValue;
            if ((DateTime.UtcNow - last).TotalHours >= Math.Max(1, Store.Settings.HealthHours)) HealthCheck(AllHealthAccounts(), true);
        }

        // ---------------- recovery ----------------
        async void ReloginSelected()
        {
            if (browserCts != null) { browserCts.Cancel(); return; }
            List<Account> sel = SelectedHealth();
            List<Account> todo = sel.Where(a => { string t, ad; Color c; HealthOf(a, out t, out c, out ad); return t != "Working"; }).ToList();
            if (todo.Count == 0) { if (sel.Count > 0) AddLog("The selected accounts are all working - nothing to re-login."); return; }
            AddLog("Re-login for " + todo.Count + " account(s): a browser opens for each - press Log In on each window.");
            int fixedN = 0;
            browserCts = new System.Threading.CancellationTokenSource();
            try
            {
                for (int i = 0; i < todo.Count; i++)
                {
                    if (browserCts.IsCancellationRequested) break;
                    Account a = todo[i];
                    AddLog("Account " + (i + 1) + " of " + todo.Count + ": " + AppState.Name(a) + " - waiting for you to log in...");
                    string cookie = await BrowserLogin.GetCookie(AddLog, browserCts.Token, async delegate(string c)
                    {
                        UserInfo u = await Api.Authenticated(c);
                        return u.Valid;
                    }, a.Username, string.IsNullOrEmpty(a.Password) ? null : a.Password);
                    if (cookie != null && await AddCookie(cookie)) { fixedN++; Journal.Add(a, "Recovered", "Logged in again from the Health page."); }
                    else AddLog(AppState.Name(a) + " was skipped (no login detected).");
                }
            }
            catch (Exception ex) { Log.Error("ReloginSelected", ex); }
            browserCts = null;
            AddLog("Re-login finished: " + fixedN + " of " + todo.Count + " account(s) fixed.");
            RefreshAccountList();
            HealthCheck(todo, true);
        }

        async void RefreshCookiesSelected()
        {
            List<Account> sel = SelectedHealth().Where(a => { string t, ad; Color c; HealthOf(a, out t, out c, out ad); return t == "Working"; }).ToList();
            if (sel.Count == 0) { AddLog("Select working accounts to refresh their cookies."); return; }
            if (MessageBox.Show(this, "Refresh the cookie of " + sel.Count + " working account(s)?\n\nThis signs those accounts out of their other sessions (for example the website in your browser).",
                "Refresh cookies", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            int n = 0;
            foreach (Account a in sel)
            {
                string nc = await Api.RotateCookie(a.Cookie, AddLog);
                if (nc != null) { a.Cookie = nc; n++; }
                await System.Threading.Tasks.Task.Delay(600);
            }
            Store.SaveAccounts();
            AddLog("Refreshed " + n + " of " + sel.Count + " cookie(s).");
            RefreshHealthPage();
        }

        void UnlockHelp()
        {
            MessageBox.Show(this,
                "A locked account returned HTTP 403: Roblox has restricted it (often after many accounts were used from one PC, or after a suspicious login).\n\n" +
                "1. Open roblox.com in your own browser and log in to that account.\n" +
                "2. Roblox will tell you what is needed: verify the email, reset the password, or complete a security check.\n" +
                "3. If it says the account is banned or restricted, use Roblox Support (the button below opens it).\n" +
                "4. When it works in the browser, press Re-login selected here (or Check all now).\n\n" +
                "To avoid it: add a verified email and 2-step verification to every account, and do not launch more accounts than you need.",
                "Unlock help", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenLink("https://www.roblox.com/support", "Roblox Support");
        }

        void RemoveBroken()
        {
            List<Account> sel = SelectedHealth().Where(a => { string t, ad; Color c; HealthOf(a, out t, out c, out ad); return t == "Signed out" || t == "Locked"; }).ToList();
            if (sel.Count == 0) { AddLog("Select signed-out or locked accounts to remove."); return; }
            if (MessageBox.Show(this, "Remove " + sel.Count + " broken account(s) from AltHub?\n\nThis does not touch the Roblox accounts. A backup of your list is made first.",
                "Remove broken accounts", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
            Store.Backup();
            foreach (Account a in sel) { Store.Accounts.Remove(a); selected.Remove(a.Id); }
            Store.SaveAccounts();
            RefreshAccountList();
            RefreshHealthPage();
            AddLog("Removed " + sel.Count + " broken account(s).");
        }

        void ExportProblems()
        {
            StringBuilder sb = new StringBuilder("account,status,checked,last_launch,crashes_7d,advice\r\n");
            int n = 0;
            foreach (Account a in Store.Accounts)
            {
                string text, advice; Color col;
                HealthOf(a, out text, out col, out advice);
                if (text == "Working" && advice.Length == 0) continue;
                n++;
                sb.Append('"').Append(AppState.Name(a).Replace("\"", "\"\"")).Append("\",").Append(text).Append(',')
                  .Append(a.LastCheckTicks > 0 ? Util.Ago(a.LastCheckTicks) : "-").Append(',')
                  .Append(a.LastUsedTicks > 0 ? Util.Ago(a.LastUsedTicks) : "never").Append(',')
                  .Append(Journal.Count(a.Id, "Crash", 7) + Journal.Count(a.Id, "Kicked", 7)).Append(",\"").Append(advice).Append("\"\r\n");
            }
            if (n == 0) { AddLog("Every account is healthy - nothing to export."); return; }
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "CSV (*.csv)|*.csv";
                d.FileName = "althub-problems.csv";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { File.WriteAllText(d.FileName, sb.ToString(), Encoding.UTF8); AddLog("Saved " + n + " problem(s) to " + d.FileName + "."); }
                catch (Exception ex) { AddLog("Could not save: " + ex.Message); }
            }
        }

        void ExportEvents()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "CSV (*.csv)|*.csv";
                d.FileName = "althub-events.csv";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { File.WriteAllText(d.FileName, Journal.ToCsv(Journal.All().OrderBy(e => e.Ticks)), Encoding.UTF8); AddLog("Event log saved to " + d.FileName + "."); }
                catch (Exception ex) { AddLog("Could not save: " + ex.Message); }
            }
        }

        void ClearEvents()
        {
            if (MessageBox.Show(this, "Delete the whole event log?", "Clear log", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            Journal.Clear();
            AddLog("Event log cleared.");
        }
    }
}
