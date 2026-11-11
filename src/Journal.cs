using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AltHub
{
    public class JournalEntry
    {
        public long Ticks;                  // UTC
        public string AccountId = "";
        public string Account = "";
        public string Kind = "";            // Crash | Kicked | LaunchFailed | Relaunch | Breaker | CookieExpired | Locked | Recovered
        public string Detail = "";
    }

    // The event log: every crash, kick, failed launch, signed-out or locked account and every recovery, per account.
    // Stored as events.json in the data folder (no cookies in it). Used by the Health page.
    public static class Journal
    {
        static readonly List<JournalEntry> list = new List<JournalEntry>();
        static string path = "";
        public static event Action Changed;

        class FileData { public List<JournalEntry> Entries = new List<JournalEntry>(); }

        public static void Init(string dir)
        {
            try
            {
                path = Path.Combine(dir, "events.json");
                list.Clear();
                if (File.Exists(path))
                {
                    FileData d = Json.From<FileData>(File.ReadAllText(path, Encoding.UTF8));
                    if (d != null && d.Entries != null) list.AddRange(d.Entries);
                }
            }
            catch (Exception ex) { Log.Error("Journal.Init", ex); }
        }

        public static void Add(Account a, string kind, string detail)
        {
            Add(a == null ? "" : a.Id, a == null ? "" : AppState.Name(a), kind, detail);
        }

        public static void Add(string accountId, string accountName, string kind, string detail)
        {
            try
            {
                JournalEntry e = new JournalEntry();
                e.Ticks = DateTime.UtcNow.Ticks;
                e.AccountId = accountId ?? "";
                e.Account = accountName ?? "";
                e.Kind = kind;
                e.Detail = detail ?? "";
                lock (list)
                {
                    list.Add(e);
                    if (list.Count > 2000) list.RemoveRange(0, list.Count - 2000);
                }
                Save();
                Action h = Changed;
                if (h != null) h();
            }
            catch (Exception ex) { Log.Error("Journal.Add", ex); }
        }

        public static List<JournalEntry> All()
        {
            lock (list) return list.ToList();
        }

        public static int Count(string accountId, string kind, int days)
        {
            long since = DateTime.UtcNow.AddDays(-days).Ticks;
            lock (list) return list.Count(e => e.AccountId == accountId && e.Ticks >= since && (kind == null || e.Kind == kind));
        }

        public static void Clear()
        {
            lock (list) list.Clear();
            Save();
            Action h = Changed;
            if (h != null) h();
        }

        static void Save()
        {
            try
            {
                if (path.Length == 0) return;
                FileData d = new FileData();
                lock (list) d.Entries = list.ToList();
                File.WriteAllText(path, Json.To(d), Encoding.UTF8);
            }
            catch (Exception ex) { Log.Error("Journal.Save", ex); }
        }

        public static string ToCsv(IEnumerable<JournalEntry> items)
        {
            StringBuilder sb = new StringBuilder("time,account,event,details\r\n");
            foreach (JournalEntry e in items)
                sb.Append(new DateTime(e.Ticks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")).Append(',')
                  .Append(Q(e.Account)).Append(',').Append(Q(e.Kind)).Append(',').Append(Q(e.Detail)).Append("\r\n");
            return sb.ToString();
        }

        static string Q(string s) { return "\"" + (s ?? "").Replace("\"", "\"\"") + "\""; }
    }
}
