using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AltHub
{
    public class UpdateInfo
    {
        public bool Available;
        public string Latest = "";
        public string Current = "";
        public string Url = "";
        public string Error = "";
        public string AssetUrl = "";        // the release zip (AltHub-vX-windows.zip)
        public string AssetName = "";
        public string SumsUrl = "";         // SHA256SUMS.txt of the release
    }

    // Update check + installer. It asks GitHub for the latest release of this project and compares version numbers. When you
    // say yes (or turn on automatic installs) it downloads the release zip over HTTPS, verifies it against the SHA-256 checksum
    // the release publishes, replaces AltHub.exe (the running one is renamed, so nothing is deleted in use) and restarts.
    public static class Updater
    {
        public const string LatestApi = "https://api.github.com/repos/s2lq/AltHub/releases/latest";
        public const string ReleasesPage = "https://github.com/s2lq/AltHub/releases/latest";

        static readonly HttpClient Http = new HttpClient();
        static Updater()
        {
            try
            {
                Http.Timeout = TimeSpan.FromSeconds(12);
                Http.DefaultRequestHeaders.UserAgent.ParseAdd("AltHub");   // GitHub refuses requests with no User-Agent
            }
            catch (Exception) { }
        }

        public static string CurrentVersion()
        {
            try { return Application.ProductVersion; } catch (Exception) { return "0"; }
        }

        public static async Task<UpdateInfo> Check()
        {
            UpdateInfo info = new UpdateInfo();
            info.Current = CurrentVersion();
            info.Url = ReleasesPage;
            try
            {
                string json = await Http.GetStringAsync(LatestApi);
                object o = Json.Parse(json);
                string tag = Json.Str(o, "tag_name");
                string url = Json.Str(o, "html_url");
                info.Latest = (tag ?? "").TrimStart('v', 'V').Trim();
                if (!string.IsNullOrEmpty(url)) info.Url = url;
                info.Available = IsNewer(info.Latest, info.Current);
                foreach (object a in Json.Arr(Json.Get(o, "assets")))
                {
                    string name = Json.Str(a, "name") ?? "";
                    string dl = Json.Str(a, "browser_download_url") ?? "";
                    if (name.EndsWith("-windows.zip", StringComparison.OrdinalIgnoreCase)) { info.AssetName = name; info.AssetUrl = dl; }
                    else if (name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase)) info.SumsUrl = dl;
                }
            }
            catch (Exception ex)
            {
                info.Error = ex.Message;
                if (ex.Message.IndexOf("404", StringComparison.Ordinal) < 0) Log.Error("Updater", ex);   // 404 = no release published yet
            }
            return info;
        }

        // Downloads, verifies and installs the update. Returns null on success (then restart), otherwise what went wrong.
        public static async Task<string> Install(UpdateInfo info, Action<string> log)
        {
            try
            {
                if (string.IsNullOrEmpty(info.AssetUrl)) return "This release has no Windows download attached.";
                if (string.IsNullOrEmpty(info.SumsUrl)) return "This release has no checksum file (SHA256SUMS.txt), so it is not installed automatically.";
                string exe = Application.ExecutablePath;
                if (!Path.GetFileName(exe).Equals("AltHub.exe", StringComparison.OrdinalIgnoreCase)) return "AltHub is not running from AltHub.exe, so it cannot replace itself.";
                string dir = Path.GetDirectoryName(exe);

                log("Downloading " + info.AssetName + "...");
                byte[] zip;
                using (HttpClient dl = new HttpClient())
                {
                    dl.Timeout = TimeSpan.FromMinutes(10);
                    dl.DefaultRequestHeaders.UserAgent.ParseAdd("AltHub");
                    zip = await dl.GetByteArrayAsync(info.AssetUrl);
                }

                string sums = await Http.GetStringAsync(info.SumsUrl);
                string want = "";
                foreach (string line in sums.Replace("\r", "").Split('\n'))
                    if (line.IndexOf(info.AssetName, StringComparison.OrdinalIgnoreCase) >= 0) { want = line.Trim().Split(' ')[0].ToLowerInvariant(); break; }
                if (want.Length != 64) return "The checksum file does not list " + info.AssetName + ".";
                string got;
                using (SHA256 sha = SHA256.Create()) got = BitConverter.ToString(sha.ComputeHash(zip)).Replace("-", "").ToLowerInvariant();
                if (got != want) return "The download does not match its checksum - it was not installed.";
                log("Checksum OK.");

                byte[] exeBytes = null;
                using (ZipArchive za = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
                    foreach (ZipArchiveEntry e in za.Entries)
                        if (e.FullName.Equals("AltHub.exe", StringComparison.OrdinalIgnoreCase))
                            using (Stream s = e.Open()) using (MemoryStream ms = new MemoryStream()) { s.CopyTo(ms); exeBytes = ms.ToArray(); }
                if (exeBytes == null || exeBytes.Length < 200000 || exeBytes[0] != (byte)'M' || exeBytes[1] != (byte)'Z') return "The download does not contain a valid AltHub.exe.";

                string fresh = exe + ".new", old = exe + ".old";
                File.WriteAllBytes(fresh, exeBytes);
                if (File.Exists(old)) File.Delete(old);
                File.Move(exe, old);                                  // a running exe can be renamed, not overwritten
                try { File.Move(fresh, exe); }
                catch (Exception) { File.Move(old, exe); throw; }
                return null;
            }
            catch (Exception ex)
            {
                Log.Error("Updater.Install", ex);
                return ex.Message;
            }
        }

        static int[] Parse(string v)
        {
            List<int> parts = new List<int>();
            foreach (string s in (v ?? "").Split('.'))
            {
                int n;
                string digits = "";
                foreach (char c in s) { if (c >= '0' && c <= '9') digits += c; else break; }
                int.TryParse(digits, out n);
                parts.Add(n);
            }
            while (parts.Count < 4) parts.Add(0);
            return parts.ToArray();
        }

        // True if "latest" is a higher version than "current".
        public static bool IsNewer(string latest, string current)
        {
            if (string.IsNullOrEmpty(latest)) return false;
            int[] a = Parse(latest);
            int[] b = Parse(current);
            for (int i = 0; i < 4; i++)
            {
                if (a[i] > b[i]) return true;
                if (a[i] < b[i]) return false;
            }
            return false;
        }
    }
}
