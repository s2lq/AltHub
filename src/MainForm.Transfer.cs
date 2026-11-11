using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace AltHub
{
    // Move to a new PC: export the accounts (with all their per-account settings and the private server list) into one
    // password-protected .althub file, and import such a file.
    public partial class MainForm
    {
        void ExportAccounts()
        {
            List<Account> sel = SelectedAccounts();
            List<Account> list = sel.Count > 0 ? sel : Store.Accounts.ToList();
            if (list.Count == 0) { AddLog("There are no accounts to export."); return; }
            string what = sel.Count > 0 ? sel.Count + " selected account(s)" : "all " + list.Count + " account(s)";
            if (MessageBox.Show(this, "Export " + what + " with their cookies, game, private server, tags, notes and proxy?\n\nThe file is encrypted with a password you choose. Anyone with the file AND the password gets into these accounts - keep both private.",
                "Export accounts", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
            string pw = Dialogs.AskPassword(this, "Export - choose a password", "Password for the backup file:", true);
            if (string.IsNullOrEmpty(pw)) return;
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Title = "Save the backup";
                d.Filter = "AltHub backup (*.althub)|*.althub";
                d.FileName = "AltHub-backup-" + DateTime.Now.ToString("yyyyMMdd") + ".althub";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    AccountFile f = new AccountFile();
                    f.Accounts = list;
                    HashSet<string> ids = new HashSet<string>(list.Where(a => !string.IsNullOrEmpty(a.PrivateServerId)).Select(a => a.PrivateServerId));
                    f.Servers = Store.Servers.Where(s => ids.Contains(s.Id)).ToList();
                    File.WriteAllBytes(d.FileName, Store.Pack(Json.To(f), pw));
                    AddLog("Exported " + list.Count + " account(s) to " + d.FileName + ".");
                }
                catch (Exception ex) { Log.Error("Export", ex); AddLog("Export failed: " + ex.Message); }
            }
        }

        void ImportAccounts()
        {
            string path;
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "Open an AltHub backup";
                d.Filter = "AltHub backup (*.althub)|*.althub|All files (*.*)|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                path = d.FileName;
            }
            string pw = Dialogs.AskPassword(this, "Import - password", "Password of the backup file:", false);
            if (string.IsNullOrEmpty(pw)) return;
            AccountFile f;
            try
            {
                f = Json.From<AccountFile>(Store.Unpack(File.ReadAllBytes(path), pw));
            }
            catch (CryptographicException) { AddLog("Wrong password, or this is not an AltHub backup file."); return; }
            catch (Exception ex) { Log.Error("Import", ex); AddLog("Could not read the file: " + ex.Message); return; }
            if (f == null || f.Accounts == null || f.Accounts.Count == 0) { AddLog("The file has no accounts."); return; }

            DialogResult upd = MessageBox.Show(this, "The file has " + f.Accounts.Count + " account(s).\n\nYes = add new accounts AND update the ones you already have (cookie, game, private server, tags ...)\nNo = only add accounts you do not have yet",
                "Import accounts", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (upd == DialogResult.Cancel) return;

            Store.Backup();
            Dictionary<string, string> idMap = new Dictionary<string, string>();     // id in the file -> id here
            int added = 0, updated = 0;
            foreach (Account inc in f.Accounts)
            {
                Account have = Store.Accounts.FirstOrDefault(a => a.Id == inc.Id)
                    ?? (inc.UserId > 0 ? Store.Accounts.FirstOrDefault(a => a.UserId == inc.UserId) : null)
                    ?? (!string.IsNullOrEmpty(inc.Username) ? Store.Accounts.FirstOrDefault(a => a.Username.Equals(inc.Username, StringComparison.OrdinalIgnoreCase)) : null);
                if (have == null)
                {
                    inc.Order = Store.Accounts.Count;
                    Store.Accounts.Add(inc);
                    idMap[inc.Id] = inc.Id;
                    added++;
                }
                else
                {
                    idMap[inc.Id] = have.Id;
                    if (upd == DialogResult.Yes)
                    {
                        string keepId = have.Id; int keepOrder = have.Order;
                        int ix = Store.Accounts.IndexOf(have);
                        inc.Id = keepId; inc.Order = keepOrder;
                        Store.Accounts[ix] = inc;
                        updated++;
                    }
                }
            }

            // private servers: reuse a server that has the same game + code, otherwise add it
            Dictionary<string, string> srvMap = new Dictionary<string, string>();
            foreach (PrivateServer s in f.Servers ?? new List<PrivateServer>())
            {
                PrivateServer have = Store.Servers.FirstOrDefault(x => x.Id == s.Id || (x.PlaceId == s.PlaceId && x.Code == s.Code));
                if (have == null)
                {
                    if (!string.IsNullOrEmpty(s.OwnerAccountId) && idMap.ContainsKey(s.OwnerAccountId)) s.OwnerAccountId = idMap[s.OwnerAccountId];
                    Store.Servers.Add(s);
                    srvMap[s.Id] = s.Id;
                }
                else srvMap[s.Id] = have.Id;
            }
            foreach (Account a in Store.Accounts)
            {
                string m;
                if (!string.IsNullOrEmpty(a.PrivateServerId) && srvMap.TryGetValue(a.PrivateServerId, out m)) a.PrivateServerId = m;
            }

            Store.SaveAccounts();
            foreach (Account a in Store.Accounts)
                if (!string.IsNullOrEmpty(a.Group) && !Store.Settings.Groups.Contains(a.Group)) Store.Settings.Groups.Add(a.Group);
            Store.SaveSettings();
            RefreshAccountList();
            RefreshPrivatePage();
            AddLog("Imported: " + added + " new account(s), " + updated + " updated. A backup of your old list was saved first.");
        }
    }
}
