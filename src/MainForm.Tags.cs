using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AltHub
{
    // Tags and colours for accounts (bulk actions on a tag) and the Robux balance.
    public partial class MainForm
    {
        string tagFilter = "";
        RoundButton btnTags;
        DateTime lastRobux = DateTime.MinValue;
        bool robuxBusy;

        List<Account> WithTag(string tag)
        {
            return Store.Accounts.Where(a => Util.TagList(a.Tags).Contains(tag, StringComparer.OrdinalIgnoreCase)).OrderBy(a => a.Order).ToList();
        }

        void UpdateTagsButton()
        {
            if (btnTags == null) return;
            btnTags.Text = tagFilter.Length == 0 ? "Tags" : "Tag: " + Util.Short(tagFilter, 12);
            btnTags.FitText(60);
            btnTags.Active = tagFilter.Length > 0;
            btnTags.Invalidate();
        }

        void ShowTagMenu()
        {
            ContextMenuStrip m = Ui.Menu();
            List<string> tags = AllTags();

            ToolStripMenuItem filter = Ui.Item("Show only accounts with a tag", null);
            filter.DropDownItems.Add(Ui.Item("(all accounts)", delegate { tagFilter = ""; UpdateTagsButton(); RefreshAccountList(); }));
            foreach (string t in tags)
            {
                string name = t;
                filter.DropDownItems.Add(Ui.Item(name + "  (" + WithTag(name).Count + ")", delegate { tagFilter = name; UpdateTagsButton(); RefreshAccountList(); }));
            }
            m.Items.Add(filter);
            m.Items.Add(new ToolStripSeparator());

            m.Items.Add(Ui.Item("Add a tag to the selected accounts...", delegate { AddTagToSelected(); }));
            ToolStripMenuItem colour = Ui.Item("Colour of the selected accounts", null);
            colour.DropDownItems.Add(Ui.Item("(none)", delegate { SetColorOnSelected(""); }));
            foreach (string cn in AccountColors.Names)
            {
                string c = cn;
                colour.DropDownItems.Add(Ui.Item(c, delegate { SetColorOnSelected(c); }));
            }
            m.Items.Add(colour);
            m.Items.Add(new ToolStripSeparator());

            if (tags.Count == 0)
            {
                ToolStripMenuItem none = Ui.Item("No tags yet - add one above or in Edit account", null);
                none.Enabled = false;
                m.Items.Add(none);
            }
            foreach (string t in tags)
            {
                string tag = t;
                ToolStripMenuItem sub = Ui.Item("Tag: " + tag + "  (" + WithTag(tag).Count + ")", null);
                sub.DropDownItems.Add(Ui.Item("Select these accounts", delegate { SelectAccounts(WithTag(tag)); }));
                sub.DropDownItems.Add(Ui.Item("Launch these accounts", delegate { LaunchTag(tag); }));
                sub.DropDownItems.Add(Ui.Item("Stop these accounts", delegate { StopTag(tag); }));
                ToolStripMenuItem col = Ui.Item("Set their colour", null);
                col.DropDownItems.Add(Ui.Item("(none)", delegate { SetColorOnTag(tag, ""); }));
                foreach (string cn in AccountColors.Names)
                {
                    string c = cn;
                    col.DropDownItems.Add(Ui.Item(c, delegate { SetColorOnTag(tag, c); }));
                }
                sub.DropDownItems.Add(col);
                sub.DropDownItems.Add(Ui.Item("Remove this tag from all accounts", delegate { RemoveTag(tag); }));
                m.Items.Add(sub);
            }

            m.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
            m.BackColor = Theme.Panel;
            m.ForeColor = Theme.Text;
            foreach (ToolStripItem it in m.Items) StyleItem(it);
            m.Show(btnTags, new Point(0, btnTags.Height));
        }

        void SelectAccounts(List<Account> list)
        {
            selected = new HashSet<string>(list.Select(a => a.Id));
            ApplySelectionToViews();
            OnAccountSelectionChanged();
            UpdateCount();
            AddLog(list.Count + " account(s) selected.");
        }

        void LaunchTag(string tag)
        {
            List<Account> list = WithTag(tag);
            if (list.Count == 0) return;
            if (list.Count > 3 && MessageBox.Show(this, "Launch " + list.Count + " accounts tagged '" + tag + "'?", "Launch", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            StartLaunch(list);
        }

        void StopTag(string tag)
        {
            int n = 0;
            foreach (Account a in WithTag(tag))
                if (runningIds.Contains(a.Id)) { StopAccount(a); n++; }
            AddLog("Stopped " + n + " account(s) tagged '" + tag + "'.");
        }

        void AddTagToSelected()
        {
            List<Account> sel = SelectedAccounts();
            if (sel.Count == 0) { AddLog("Select one or more accounts first."); return; }
            string t = Dialogs.Prompt(this, "Add a tag", "Tag to add to " + sel.Count + " account(s):", "");
            t = Util.CleanTags(t);
            if (t.Length == 0) return;
            foreach (string one in t.Split(','))
                foreach (Account a in sel)
                {
                    List<string> have = Util.TagList(a.Tags);
                    if (!have.Contains(one, StringComparer.OrdinalIgnoreCase)) { have.Add(one); a.Tags = string.Join(",", have.ToArray()); }
                }
            Store.SaveAccounts();
            RefreshAccountList();
            RefreshPrivatePage();
            AddLog("Tagged " + sel.Count + " account(s) with '" + t + "'.");
        }

        void RemoveTag(string tag)
        {
            foreach (Account a in Store.Accounts)
            {
                List<string> have = Util.TagList(a.Tags);
                have.RemoveAll(x => x.Equals(tag, StringComparison.OrdinalIgnoreCase));
                a.Tags = string.Join(",", have.ToArray());
            }
            if (tagFilter.Equals(tag, StringComparison.OrdinalIgnoreCase)) { tagFilter = ""; UpdateTagsButton(); }
            Store.SaveAccounts();
            RefreshAccountList();
            RefreshPrivatePage();
            AddLog("Removed the tag '" + tag + "' from all accounts.");
        }

        void SetColorOnSelected(string name)
        {
            List<Account> sel = SelectedAccounts();
            if (sel.Count == 0) { AddLog("Select one or more accounts first."); return; }
            foreach (Account a in sel) a.ColorName = name;
            Store.SaveAccounts();
            RefreshAccountList();
        }

        void SetColorOnTag(string tag, string name)
        {
            foreach (Account a in WithTag(tag)) a.ColorName = name;
            Store.SaveAccounts();
            RefreshAccountList();
        }

        // ---------------- Robux ----------------
        async void RefreshRobux(bool all)
        {
            if (robuxBusy) return;
            List<Account> list = all ? Store.Accounts.ToList() : SelectedAccounts();
            list = list.Where(a => !string.IsNullOrEmpty(a.Cookie) && a.UserId > 0 && !AppState.Live(a).CookieBad).ToList();
            if (list.Count == 0) { if (!all) AddLog("Select one or more accounts first."); return; }
            robuxBusy = true;
            lastRobux = DateTime.Now;
            try
            {
                foreach (Account a in list)
                {
                    long r = await Api.Robux(a);
                    if (r >= 0) a.Robux = r;
                    await Task.Delay(300);
                }
                Store.SaveAccounts();
                RefreshAccountList();
                if (!all) AddLog("Robux updated for " + list.Count + " account(s).");
            }
            catch (Exception ex) { Log.Error("RefreshRobux", ex); }
            robuxBusy = false;
        }

        // called every second from the main timer: loads the balances shortly after start and then every 30 minutes
        void RobuxTick()
        {
            if (robuxBusy || Store.Accounts.Count == 0) return;
            if (lastRobux == DateTime.MinValue ? tickN >= 25 : (DateTime.Now - lastRobux).TotalMinutes >= 30) RefreshRobux(true);
        }
    }
}
