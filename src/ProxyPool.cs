using System;
using System.Collections.Generic;
using System.Linq;

namespace AltHub
{
    // A pool of proxies that are handed out to accounts automatically: every account its own proxy ("1 per proxy"), or
    // several accounts sharing one ("3 per proxy"). The proxy is used for AltHub's own web requests for that account.
    public static class ProxyPool
    {
        // host:port, user:pass@host:port, scheme://user:pass@host:port, or the common seller format host:port:user:pass
        public static string Normalize(string line)
        {
            string s = (line ?? "").Trim();
            if (s.Length == 0 || s.StartsWith("#")) return "";
            if (s.IndexOf("://", StringComparison.Ordinal) < 0 && s.IndexOf('@') < 0)
            {
                string[] p = s.Split(':');
                if (p.Length == 4) return p[2] + ":" + p[3] + "@" + p[0] + ":" + p[1];
            }
            return s;
        }

        public static List<string> Pool()
        {
            List<string> r = new List<string>();
            foreach (string l in (Store.ProxyPool ?? "").Replace("\r", "").Split('\n'))
            {
                string n = Normalize(l);
                if (n.Length > 0 && !r.Contains(n)) r.Add(n);
            }
            return r;
        }

        // host:port without the password, for logs and lists.
        public static string Mask(string proxy)
        {
            string s = proxy ?? "";
            int at = s.LastIndexOf('@');
            if (at >= 0) s = s.Substring(at + 1);
            int sch = s.IndexOf("://", StringComparison.Ordinal);
            return sch >= 0 ? s.Substring(sch + 3) : s;
        }

        // The proxy with room for this account: the first one that has fewer than "per" accounts, otherwise the least used.
        public static string PickFor(Account a, int per)
        {
            List<string> pool = Pool();
            if (pool.Count == 0) return "";
            per = Math.Max(1, per);
            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (string p in pool) counts[p] = 0;
            foreach (Account o in Store.Accounts)
            {
                if (o == a || string.IsNullOrEmpty(o.Proxy)) continue;
                string n = Normalize(o.Proxy);
                if (counts.ContainsKey(n)) counts[n]++;
            }
            foreach (string p in pool) if (counts[p] < per) return p;
            return pool.OrderBy(p => counts[p]).First();
        }

        // Gives a new account a proxy when automatic assignment is on.
        public static void ForNew(Account a)
        {
            if (!Store.Settings.ProxyAuto || !string.IsNullOrEmpty(a.Proxy)) return;
            string p = PickFor(a, Store.Settings.ProxyPerAccounts);
            if (p.Length > 0) { a.Proxy = p; a.ProxyAuto = true; }
        }

        // Hands out proxies to every account that has none (and re-does the ones that were given automatically earlier).
        // Proxies you typed in by hand are kept. Returns how many accounts got one.
        public static int AssignAll()
        {
            foreach (Account a in Store.Accounts) if (a.ProxyAuto) { a.Proxy = ""; a.ProxyAuto = false; }
            int n = 0;
            foreach (Account a in Store.Accounts.OrderBy(x => x.Order))
            {
                if (!string.IsNullOrEmpty(a.Proxy)) continue;
                string p = PickFor(a, Store.Settings.ProxyPerAccounts);
                if (p.Length == 0) break;
                a.Proxy = p; a.ProxyAuto = true; n++;
            }
            return n;
        }

        public static int ClearAuto()
        {
            int n = 0;
            foreach (Account a in Store.Accounts) if (a.ProxyAuto) { a.Proxy = ""; a.ProxyAuto = false; n++; }
            return n;
        }
    }
}
