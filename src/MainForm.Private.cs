using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AltHub
{
    // The "Private" tab: a list of saved private servers (named, with their owner), allocation of accounts / groups /
    // tags to them, one-click launch of everybody on a server, and a link check.
    public partial class MainForm
    {
        ListView lvPriv, lvAssigned;
        RoundTextBox tbPsName, tbPsLink;
        DarkCombo cbPsOwner, cbPsWho, cbPsServer;
        readonly Dictionary<string, string> psCheck = new Dictionary<string, string>();     // account id -> last link check text
        readonly Dictionary<string, string> psCheckKind = new Dictionary<string, string>(); // account id -> ok | bad | unknown
        bool psBusy;

        void BuildPrivatePage()
        {
            Panel page = NewPage("Private");
            PageBuilder p = new PageBuilder(page, PageW);
            p.Title("Private servers", "Save your private server links once, see who owns them, and give each account (or a whole group) the one it should join.");

            p.Section("Add a private server");
            tbPsName = Ui.Txt(150, "");
            tbPsLink = Ui.Txt(300, "");
            p.Opt("Name it (Grind, Trading ...) and paste its link.",
                "Works with old links (roblox.com/games/ID?privateServerLinkCode=...) and new share links (roblox.com/share?code=...&type=Server). The server shows up in the list below and in the 'Private server' drop-down of every account.",
                Ui.Fld("Name", 48), tbPsName, Ui.Fld("Link", 36), tbPsLink);
            cbPsOwner = Ui.Cmb(230, "(auto - first selected account)");
            p.Opt("Who owns this server?",
                "Pick the account that owns the private server. Left on auto it is the first account selected on the left. When Roblox tells us the owner (new share links), that name is used and shown too.",
                Ui.Fld("Owner account", 100), cbPsOwner, Ui.Btn("Add server", 110, delegate { AddServerClick(); }, "primary"));

            p.Section("Your private servers");
            lvPriv = new BufferedListView();
            lvPriv.Size = new Size(PageW - PageBuilder.PadX * 2, 170);
            lvPriv.Columns.Add("Name", 120);
            lvPriv.Columns.Add("Game", 150);
            lvPriv.Columns.Add("Owner", 120);
            lvPriv.Columns.Add("Accounts", 70);
            lvPriv.Columns.Add("Link check", 150);
            lvPriv.MultiSelect = false;
            ListStyle.Attach(lvPriv);
            lvPriv.SelectedIndexChanged += delegate { RefreshAssigned(); };
            p.Add(lvPriv);

            p.Opt("Put the accounts selected on the left on this server.",
                "Select one or more accounts in the list on the far left, pick a server above, then press this. Each account remembers it and joins it on Launch. 'Unassign' takes the selected accounts off their private server again.",
                Ui.Btn("Assign to selected accounts", 210, delegate { AssignSelectedAccounts(); }, "primary"),
                Ui.Btn("Unassign", 90, delegate { UnassignSelectedAccounts(); }, ""));
            p.Opt("Send everyone on this server in with one click.",
                "Launches every account that is assigned to the chosen server, one after another (the delay between accounts is on the Launch page).",
                Ui.Btn("Launch its accounts", 170, delegate { LaunchServerAccounts(); }, "primary"));
            p.Opt("Check that the link still works for its accounts.",
                "Asks Roblox whether each assigned account can join right now (an account that is not invited, a deleted server or a changed link shows up here). If nobody is assigned yet, the owner account is used. Checking may briefly start the private server.\n\n'Refresh owners' asks Roblox who owns each server where the link allows it.",
                Ui.Btn("Check link", 100, delegate { CheckLinks(false); }, ""),
                Ui.Btn("Check all", 90, delegate { CheckLinks(true); }, ""),
                Ui.Btn("Refresh owners", 130, delegate { RefreshOwners(); }, ""));
            p.Opt("Rename, change the owner, copy the link or remove it.",
                "Removing a server also takes it off the accounts that used it. It does not touch the real private server on Roblox.",
                Ui.Btn("Edit...", 80, delegate { EditSelectedPrivate(); }, ""),
                Ui.Btn("Copy link", 100, delegate { CopySelectedServerLink(); }, ""),
                Ui.Btn("Remove", 90, delegate { RemoveSelectedPrivate(); }, "danger"));

            p.Section("Accounts on the selected server");
            lvAssigned = new BufferedListView();
            lvAssigned.Size = new Size(PageW - PageBuilder.PadX * 2, 130);
            lvAssigned.Columns.Add("Account", 150);
            lvAssigned.Columns.Add("Group", 100);
            lvAssigned.Columns.Add("Tags", 130);
            lvAssigned.Columns.Add("Link check", 200);
            lvAssigned.MultiSelect = false;
            ListStyle.Attach(lvAssigned);
            p.Add(lvAssigned);

            p.Section("Groups and tags");
            cbPsWho = Ui.Cmb(230, "(select a group or tag)");
            cbPsServer = Ui.Cmb(250, "(select a server)");
            p.Opt("Pick a group (or a tag) and a server.",
                "Every account of that group or tag is put on the server. Use 'Assign and launch' to also start them all right away.",
                Ui.Fld("Who", 36), cbPsWho, Ui.Fld("Server", 50), cbPsServer);
            p.Opt("Send the whole group in.",
                "'Assign' only saves the server on those accounts. 'Assign and launch' saves it and launches them all.",
                Ui.Btn("Assign", 100, delegate { AssignGroup(false); }, ""),
                Ui.Btn("Assign and launch", 160, delegate { AssignGroup(true); }, "primary"));
        }

        // ---------------- helpers ----------------
        string OwnerText(PrivateServer ps)
        {
            if (!string.IsNullOrEmpty(ps.OwnerName)) return ps.OwnerName;
            Account o = Store.FindAccount(ps.OwnerAccountId);
            if (o != null) return AppState.Name(o);
            return "(unknown)";
        }

        string PrivateNameOf(Account a)
        {
            PrivateServer ps = Store.FindServer(a.PrivateServerId);
            return ps == null ? "" : (string.IsNullOrEmpty(ps.Name) ? "private server" : ps.Name);
        }

        PrivateServer SelectedPrivate()
        {
            if (lvPriv == null || lvPriv.SelectedItems.Count == 0) return null;
            return lvPriv.SelectedItems[0].Tag as PrivateServer;
        }

        PrivateServer NeedServer()
        {
            PrivateServer ps = SelectedPrivate();
            if (ps == null) AddLog("Select a private server in the list first.");
            return ps;
        }

        List<Account> OnServer(PrivateServer ps)
        {
            return Store.Accounts.Where(a => a.PrivateServerId == ps.Id).OrderBy(a => a.Order).ToList();
        }

        void ChangedAccounts()
        {
            Store.SaveAccounts();
            RefreshAccountList();
            UpdateStatusCells();
            RefreshPrivatePage();
        }

        // ---------------- refresh ----------------
        void RefreshPrivatePage()
        {
            if (lvPriv == null) return;
            string keep = SelectedPrivate() != null ? SelectedPrivate().Id : "";
            lvPriv.BeginUpdate();
            lvPriv.Items.Clear();
            foreach (PrivateServer ps in Store.Servers)
            {
                string game = string.IsNullOrEmpty(ps.GameName) ? (ps.PlaceId > 0 ? "place " + ps.PlaceId : "") : ps.GameName;
                string check = ps.Check == 0 ? "not checked" : ps.CheckText;
                ListViewItem it = new ListViewItem(new string[] { ps.Name, game, OwnerText(ps), OnServer(ps).Count.ToString(), check });
                it.Tag = ps;
                if (ps.Check == 2) it.ForeColor = Theme.Bad;
                else if (ps.Check == 1) it.ForeColor = Theme.Good;
                else it.ForeColor = Theme.Text;
                lvPriv.Items.Add(it);
                if (ps.Id == keep) it.Selected = true;
            }
            lvPriv.EndUpdate();

            int oi = cbPsOwner.SelectedIndex;
            cbPsOwner.Items.Clear();
            cbPsOwner.Items.Add("(auto - first selected account)");
            foreach (Account a in Store.Accounts) cbPsOwner.Items.Add(AppState.Name(a));
            cbPsOwner.SelectedIndex = oi >= 0 && oi < cbPsOwner.Items.Count ? oi : 0;

            string who = cbPsWho.Text;
            cbPsWho.Items.Clear();
            cbPsWho.Items.Add("(select a group or tag)");
            cbPsWho.Items.Add("Selected accounts on the left");
            foreach (string g in Store.AllGroups()) cbPsWho.Items.Add("Group: " + g);
            foreach (string t in AllTags()) cbPsWho.Items.Add("Tag: " + t);
            int wi = cbPsWho.Items.IndexOf(who);
            cbPsWho.SelectedIndex = wi >= 0 ? wi : 0;

            string srv = cbPsServer.Text;
            cbPsServer.Items.Clear();
            cbPsServer.Items.Add("(select a server)");
            foreach (PrivateServer ps in Store.Servers) cbPsServer.Items.Add(Dialogs.ServerLabel(ps));
            int si = cbPsServer.Items.IndexOf(srv);
            cbPsServer.SelectedIndex = si >= 0 ? si : 0;

            RefreshAssigned();
        }

        void RefreshAssigned()
        {
            if (lvAssigned == null) return;
            lvAssigned.Items.Clear();
            PrivateServer ps = SelectedPrivate();
            if (ps == null) return;
            foreach (Account a in OnServer(ps))
            {
                string c;
                if (!psCheck.TryGetValue(a.Id, out c)) c = "";
                string kind;
                psCheckKind.TryGetValue(a.Id, out kind);
                ListViewItem it = new ListViewItem(new string[] { AppState.Name(a), a.Group ?? "", (a.Tags ?? "").Replace(",", ", "), c });
                it.Tag = a;
                it.ForeColor = kind == "bad" ? Theme.Bad : (kind == "ok" ? Theme.Good : Theme.Text);
                lvAssigned.Items.Add(it);
            }
        }

        List<string> AllTags()
        {
            List<string> t = new List<string>();
            foreach (Account a in Store.Accounts)
                foreach (string x in Util.TagList(a.Tags))
                    if (!t.Contains(x, StringComparer.OrdinalIgnoreCase)) t.Add(x);
            t.Sort(StringComparer.OrdinalIgnoreCase);
            return t;
        }

        // ---------------- add ----------------
        async void AddServerClick()
        {
            string link = tbPsLink.Text.Trim();
            if (link.Length == 0) { AddLog("Paste a private server link first."); return; }
            Account owner = null;
            int oi = cbPsOwner.SelectedIndex;
            if (oi > 0 && oi - 1 < Store.Accounts.Count) owner = Store.Accounts[oi - 1];
            else owner = FirstSelected();
            PrivateServer ps = await AddServerFromLink(link, tbPsName.Text.Trim(), owner);
            if (ps == null) return;
            tbPsName.Text = "";
            tbPsLink.Text = "";
            foreach (ListViewItem it in lvPriv.Items) it.Selected = (it.Tag as PrivateServer) == ps;
        }

        // Adds a private server to the list (or returns the one that already has this link) and fills in its game and owner.
        async Task<PrivateServer> AddServerFromLink(string link, string name, Account owner)
        {
            long pid = 0; string code = ""; long serverId = 0;
            if (!Api.ParseOldPrivate(link, out pid, out code))
            {
                Match share = Regex.Match(link, @"[?&]code=([A-Za-z0-9]+)");
                if (!share.Success || link.IndexOf("share", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    AddLog("That does not look like a private server link.");
                    return null;
                }
                string cookie = (owner != null && owner.Cookie.Length > 0) ? owner.Cookie : AnyCookie();
                if (string.IsNullOrEmpty(cookie)) { AddLog("Add an account first - share links are resolved with an account."); return null; }
                AddLog("Resolving the share link...");
                LaunchSpec r = await Api.ResolveShare(share.Groups[1].Value, cookie);
                if (r == null)
                {
                    AddLog("Could not resolve that link. The account must be allowed to use this private server.");
                    return null;
                }
                pid = r.PlaceId; code = r.LinkCode; serverId = r.ServerId;
            }

            PrivateServer ps = Store.Servers.FirstOrDefault(x => x.PlaceId == pid && x.Code == code);
            bool isNew = ps == null;
            if (isNew)
            {
                ps = new PrivateServer();
                ps.Name = name.Length > 0 ? name : "Server " + (Store.Servers.Count + 1);
                ps.Link = link;
                ps.PlaceId = pid;
                ps.Code = code;
                ps.ServerId = serverId;
                if (owner != null) ps.OwnerAccountId = owner.Id;
                Store.Servers.Add(ps);
            }
            else
            {
                if (name.Length > 0) ps.Name = name;
                if (serverId > 0) ps.ServerId = serverId;
                if (string.IsNullOrEmpty(ps.OwnerAccountId) && owner != null) ps.OwnerAccountId = owner.Id;
            }
            Store.SaveAccounts();
            RefreshPrivatePage();
            AddLog((isNew ? "Saved private server '" : "That private server was already saved as '") + ps.Name + "'.");

            // game name and (when Roblox tells us) the owner, filled in without blocking the UI
            LookupServerInfo(ps);
            return ps;
        }

        async void LookupServerInfo(PrivateServer ps)
        {
            try
            {
                if (string.IsNullOrEmpty(ps.GameName) && ps.PlaceId > 0)
                {
                    string g = await Api.PlaceName(ps.PlaceId);
                    if (!string.IsNullOrEmpty(g)) ps.GameName = g;
                }
                if (ps.ServerId > 0)
                {
                    Account o = Store.FindAccount(ps.OwnerAccountId);
                    string cookie = (o != null && o.Cookie.Length > 0) ? o.Cookie : AnyCookie();
                    string[] info = await Api.VipServer(ps.ServerId, cookie);
                    if (info != null)
                    {
                        if (!string.IsNullOrEmpty(info[1])) ps.OwnerName = info[1];
                    }
                }
                Store.SaveAccounts();
                RefreshPrivatePage();
            }
            catch (Exception ex) { Log.Error("LookupServerInfo", ex); }
        }

        async void RefreshOwners()
        {
            if (psBusy) return;
            psBusy = true;
            try
            {
                int n = 0;
                foreach (PrivateServer ps in Store.Servers.ToList())
                {
                    if (ps.ServerId <= 0) continue;
                    Account o = Store.FindAccount(ps.OwnerAccountId);
                    string cookie = (o != null && o.Cookie.Length > 0) ? o.Cookie : AnyCookie();
                    string[] info = await Api.VipServer(ps.ServerId, cookie);
                    if (info != null && !string.IsNullOrEmpty(info[1])) { ps.OwnerName = info[1]; n++; }
                    if (string.IsNullOrEmpty(ps.GameName) && ps.PlaceId > 0)
                    {
                        string g = await Api.PlaceName(ps.PlaceId);
                        if (!string.IsNullOrEmpty(g)) ps.GameName = g;
                    }
                }
                Store.SaveAccounts();
                RefreshPrivatePage();
                AddLog(n > 0 ? "Owners updated from Roblox for " + n + " server(s)." : "Roblox did not tell us any owner (it only does so for new share links). The owner account you picked is shown instead.");
            }
            catch (Exception ex) { Log.Error("RefreshOwners", ex); }
            psBusy = false;
        }

        // ---------------- allocate ----------------
        void AssignAccounts(PrivateServer ps, List<Account> accs)
        {
            foreach (Account a in accs) a.PrivateServerId = ps.Id;
            if (accs.Count > 0 && string.IsNullOrEmpty(ps.OwnerAccountId) && string.IsNullOrEmpty(ps.OwnerName)) ps.OwnerAccountId = accs[0].Id;
            ChangedAccounts();
        }

        void AssignSelectedAccounts()
        {
            PrivateServer ps = NeedServer();
            if (ps == null) return;
            List<Account> sel = SelectedAccounts();
            if (sel.Count == 0) { AddLog("Select one or more accounts in the list on the far left first."); return; }
            AssignAccounts(ps, sel);
            AddLog("Put " + sel.Count + " account(s) on '" + ps.Name + "'.");
        }

        void UnassignSelectedAccounts()
        {
            List<Account> sel = SelectedAccounts();
            if (sel.Count == 0) { AddLog("Select one or more accounts in the list on the far left first."); return; }
            foreach (Account a in sel) a.PrivateServerId = "";
            ChangedAccounts();
            AddLog("Removed the private server from " + sel.Count + " account(s).");
        }

        List<Account> WhoAccounts()
        {
            string w = cbPsWho.Text;
            if (w.StartsWith("Group: ")) { string g = w.Substring(7); return Store.Accounts.Where(a => a.Group == g).OrderBy(a => a.Order).ToList(); }
            if (w.StartsWith("Tag: ")) { string t = w.Substring(5); return Store.Accounts.Where(a => Util.TagList(a.Tags).Contains(t, StringComparer.OrdinalIgnoreCase)).OrderBy(a => a.Order).ToList(); }
            if (cbPsWho.SelectedIndex == 1) return SelectedAccounts();
            return new List<Account>();
        }

        void AssignGroup(bool launch)
        {
            int si = cbPsServer.SelectedIndex;
            if (si <= 0 || si - 1 >= Store.Servers.Count) { AddLog("Pick a server first."); return; }
            PrivateServer ps = Store.Servers[si - 1];
            List<Account> accs = WhoAccounts();
            if (accs.Count == 0) { AddLog("Pick a group or tag that has accounts (or select accounts on the left)."); return; }
            AssignAccounts(ps, accs);
            AddLog("Put " + accs.Count + " account(s) on '" + ps.Name + "'.");
            if (launch) LaunchOnServer(accs);
        }

        void LaunchServerAccounts()
        {
            PrivateServer ps = NeedServer();
            if (ps == null) return;
            List<Account> accs = OnServer(ps);
            if (accs.Count == 0) { AddLog("No account is assigned to '" + ps.Name + "' yet."); return; }
            LaunchOnServer(accs);
        }

        async void LaunchOnServer(List<Account> accs)
        {
            if (launching) { AddLog("A launch is already running. Press Stop to cancel it."); return; }
            if (accs.Count > 3 && MessageBox.Show(this, "Launch " + accs.Count + " accounts into their private server?", "Launch", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            SetLaunching(true);
            try
            {
                LaunchSpec spec = PrivateSpec(accs[0]);
                if (spec != null) await RunLaunch(accs, spec, true);
            }
            catch (Exception ex) { Log.Error("LaunchOnServer", ex); AddLog("Launch failed: " + ex.Message); }
            SetLaunching(false);
        }

        // ---------------- link check ----------------
        async void CheckLinks(bool all)
        {
            if (psBusy) { AddLog("A check is already running."); return; }
            List<PrivateServer> list = new List<PrivateServer>();
            if (all) list.AddRange(Store.Servers);
            else { PrivateServer ps = NeedServer(); if (ps != null) list.Add(ps); }
            if (list.Count == 0) return;
            psBusy = true;
            try
            {
                foreach (PrivateServer ps in list) await CheckOneServer(ps);
                Store.SaveAccounts();
                RefreshPrivatePage();
            }
            catch (Exception ex) { Log.Error("CheckLinks", ex); AddLog("Link check failed: " + ex.Message); }
            psBusy = false;
        }

        async Task CheckOneServer(PrivateServer ps)
        {
            List<Account> accs = OnServer(ps).Where(a => !string.IsNullOrEmpty(a.Cookie)).ToList();
            if (accs.Count == 0)
            {
                Account o = Store.FindAccount(ps.OwnerAccountId);
                if (o != null && !string.IsNullOrEmpty(o.Cookie)) accs.Add(o);
                else
                {
                    Account any = Store.Accounts.FirstOrDefault(a => !string.IsNullOrEmpty(a.Cookie));
                    if (any != null) accs.Add(any);
                }
            }
            if (accs.Count == 0) { ps.Check = 0; ps.CheckText = "No account to check with"; return; }

            AddLog("Checking '" + ps.Name + "' with " + accs.Count + " account(s)...");
            int ok = 0, bad = 0;
            string firstBad = "";
            foreach (Account a in accs)
            {
                string[] r = await Api.CheckPrivate(ps.PlaceId, ps.Code, a.Cookie);
                psCheckKind[a.Id] = r[0];
                psCheck[a.Id] = r[1];
                if (r[0] == "ok") ok++;
                else if (r[0] == "bad") { bad++; if (firstBad.Length == 0) firstBad = AppState.Name(a) + ": " + r[1]; }
                await Task.Delay(400);
            }
            ps.CheckTicks = DateTime.UtcNow.Ticks;
            if (bad > 0) { ps.Check = 2; ps.CheckText = ok + "/" + accs.Count + " can join - " + firstBad; }
            else if (ok > 0) { ps.Check = 1; ps.CheckText = accs.Count == 1 ? psCheck[accs[0].Id] : ok + "/" + accs.Count + " can join"; }
            else { ps.Check = 0; ps.CheckText = accs.Count == 1 ? psCheck[accs[0].Id] : "Could not check"; }
            AddLog("'" + ps.Name + "': " + ps.CheckText);
        }

        // ---------------- edit / copy / remove ----------------
        void EditSelectedPrivate()
        {
            PrivateServer ps = NeedServer();
            if (ps == null) return;
            if (Dialogs.EditServer(this, ps))
            {
                Store.SaveAccounts();
                RefreshPrivatePage();
                RefreshAccountList();
            }
        }

        void CopySelectedServerLink()
        {
            PrivateServer ps = NeedServer();
            if (ps == null) return;
            string link = !string.IsNullOrEmpty(ps.Link) ? ps.Link : "https://www.roblox.com/games/" + ps.PlaceId + "?privateServerLinkCode=" + ps.Code;
            try { Clipboard.SetText(link); AddLog("Copied the link of '" + ps.Name + "'."); }
            catch (Exception) { AddLog("Could not access the clipboard."); }
        }

        void RemoveSelectedPrivate()
        {
            PrivateServer ps = NeedServer();
            if (ps == null) return;
            int n = OnServer(ps).Count;
            if (MessageBox.Show(this, "Remove the private server '" + ps.Name + "'" + (n > 0 ? " from " + n + " account(s)" : "") + "?\nThe real private server on Roblox is not touched.",
                "Remove private server", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            foreach (Account a in Store.Accounts) if (a.PrivateServerId == ps.Id) a.PrivateServerId = "";
            Store.Servers.Remove(ps);
            ChangedAccounts();
            AddLog("Removed the private server '" + ps.Name + "'.");
        }
    }
}
