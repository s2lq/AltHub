using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AltHub
{
    // Settings > Proxies for accounts: a proxy list that is handed out automatically - one proxy per account, or several
    // accounts per proxy (3 per proxy, ...).
    public partial class MainForm
    {
        TextBox tbProxyPool;
        NumericUpDown numProxyPer;
        CheckBox chkProxyAuto;
        Label lblProxyInfo;
        bool proxyBusy;

        void BuildProxySection(PageBuilder p)
        {
            AppSettings S = Store.Settings;
            p.Section("Proxies for accounts");
            p.Note("One proxy per line: host:port, user:pass@host:port or host:port:user:pass. The list is stored encrypted with your accounts.");
            tbProxyPool = new TextBox();
            tbProxyPool.Multiline = true;
            tbProxyPool.ScrollBars = ScrollBars.Vertical;
            tbProxyPool.WordWrap = false;
            tbProxyPool.Font = Theme.Mono;
            tbProxyPool.Size = new Size(PageW - PageBuilder.PadX * 2, 110);
            tbProxyPool.Text = (Store.ProxyPool ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n");
            tbProxyPool.Leave += delegate { SaveProxyPool(); };
            p.Add(tbProxyPool);

            numProxyPer = Ui.Num(1, 50, Math.Max(1, S.ProxyPerAccounts), 70);
            p.Opt("How many accounts share one proxy.",
                "1 = every account gets its own proxy. 3 = three accounts share one proxy, the next three the next one, and so on. If the list has fewer proxies than needed, the proxies are reused round-robin.",
                Ui.Fld("Accounts per proxy", 140), numProxyPer);
            chkProxyAuto = Ui.Chk("Give new accounts a proxy automatically", S.ProxyAuto, null);
            p.Opt("A newly added account takes the next proxy that still has room.",
                "Accounts that already have a proxy (typed in by hand in Edit account) are never changed.",
                chkProxyAuto);
            p.Opt("Hand out the proxies now.",
                "'Assign to all accounts' gives a proxy to every account that has none and re-does the ones handed out automatically before; proxies you typed in by hand stay. 'Test proxies' asks Roblox's public website through each proxy and shows whether it answers and how fast.\n\nThe proxy is used for AltHub's own web requests for that account (login checks, status, launch tickets, link checks). The Roblox game connection itself is separate and does not go through the proxy.",
                Ui.Btn("Assign to all accounts", 190, delegate { AssignProxies(); }, "primary"),
                Ui.Btn("Remove automatic proxies", 200, delegate { RemoveAutoProxies(); }, ""),
                Ui.Btn("Test proxies", 120, delegate { TestProxies(); }, ""));
            lblProxyInfo = Ui.Lbl("", "muted");
            p.Row(lblProxyInfo);

            EventHandler ch = delegate
            {
                if (loadingSettings) return;
                Store.Settings.ProxyPerAccounts = (int)numProxyPer.Value;
                Store.Settings.ProxyAuto = chkProxyAuto.Checked;
                Store.SaveSettings();
                UpdateProxyInfo();
            };
            numProxyPer.ValueChanged += ch;
            chkProxyAuto.CheckedChanged += ch;
            UpdateProxyInfo();
        }

        void SaveProxyPool()
        {
            string t = tbProxyPool.Text.Replace("\r\n", "\n");
            if (t == (Store.ProxyPool ?? "")) return;
            Store.ProxyPool = t;
            Store.SaveAccounts();
            UpdateProxyInfo();
        }

        void UpdateProxyInfo()
        {
            if (lblProxyInfo == null) return;
            int pool = ProxyPool.Pool().Count;
            int have = Store.Accounts.Count(a => !string.IsNullOrEmpty(a.Proxy));
            int auto = Store.Accounts.Count(a => a.ProxyAuto);
            int per = Math.Max(1, Store.Settings.ProxyPerAccounts);
            int need = (int)Math.Ceiling(Store.Accounts.Count / (double)per);
            lblProxyInfo.Text = pool + " proxies in the list  -  " + have + " of " + Store.Accounts.Count + " accounts have one (" + auto + " automatic)"
                + (pool > 0 && pool < need ? "  -  you need " + need + " proxies for " + per + " per proxy" : "");
        }

        void AssignProxies()
        {
            SaveProxyPool();
            Store.Settings.ProxyPerAccounts = (int)numProxyPer.Value;
            Store.SaveSettings();
            if (ProxyPool.Pool().Count == 0) { AddLog("Paste some proxies into the list first."); return; }
            int n = ProxyPool.AssignAll();
            Store.SaveAccounts();
            RefreshAccountList();
            UpdateProxyInfo();
            AddLog("Gave " + n + " account(s) a proxy (" + Store.Settings.ProxyPerAccounts + " per proxy, " + ProxyPool.Pool().Count + " proxies in the list).");
        }

        void RemoveAutoProxies()
        {
            int n = ProxyPool.ClearAuto();
            Store.SaveAccounts();
            RefreshAccountList();
            UpdateProxyInfo();
            AddLog("Removed the automatic proxy from " + n + " account(s).");
        }

        async void TestProxies()
        {
            if (proxyBusy) return;
            SaveProxyPool();
            System.Collections.Generic.List<string> pool = ProxyPool.Pool();
            if (pool.Count == 0) { AddLog("Paste some proxies into the list first."); return; }
            proxyBusy = true;
            try
            {
                AddLog("Testing " + pool.Count + " proxies...");
                int ok = 0;
                foreach (string px in pool)
                {
                    string r = await Api.TestProxy(px);
                    if (r.StartsWith("OK")) ok++;
                    AddLog("  " + ProxyPool.Mask(px) + ": " + r);
                }
                AddLog("Proxy test done: " + ok + " of " + pool.Count + " work.");
            }
            catch (Exception ex) { Log.Error("TestProxies", ex); }
            proxyBusy = false;
        }
    }
}
