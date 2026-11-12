using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AltHub
{
    public class RobloxVersion
    {
        public string Folder = "";          // version-xxxxxxxx
        public string Exe = "";             // full path of RobloxPlayerBeta.exe
        public string FileVersion = "";     // 0.xxx.0.xxxxxxx
        public string Source = "";          // Roblox (installed by Roblox itself) | AltHub (downloaded here)
        public DateTime Date;
    }

    public class DeployEntry
    {
        public string Folder = "";          // version-xxxxxxxx
        public string Version = "";         // 0.xxx.0.xxxxxxx
        public DateTime Date;
    }

    // Roblox version viewer and changer: lists the Roblox clients on this PC and the recent public releases, downloads a chosen
    // release from Roblox's own download servers into AltHub's folder (never touching your normal Roblox install), and
    // remembers which version AltHub should launch (Settings.RobloxVersion, "" = whatever Roblox installed).
    public static class RobloxVersions
    {
        const string ProcExe = "RobloxPlayerBeta.exe";
        public static string Cdn = "https://setup.rbxcdn.com/";    // not const so it can be pointed elsewhere in tests
        static readonly HttpClient Http = MakeClient();

        static HttpClient MakeClient()
        {
            HttpClient c = new HttpClient();
            c.Timeout = TimeSpan.FromMinutes(10);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            return c;
        }

        public static string OfficialRoot
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions"); }
        }

        public static string OwnRoot
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AltHub", "Roblox"); }
        }

        public static List<RobloxVersion> Installed()
        {
            List<RobloxVersion> list = new List<RobloxVersion>();
            foreach (string root in new string[] { OfficialRoot, OwnRoot })
            {
                try
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (string d in Directory.GetDirectories(root))
                    {
                        string exe = Path.Combine(d, ProcExe);
                        if (!File.Exists(exe)) continue;
                        RobloxVersion v = new RobloxVersion();
                        v.Folder = Path.GetFileName(d);
                        v.Exe = exe;
                        v.Source = root == OwnRoot ? "AltHub" : "Roblox";
                        v.Date = File.GetLastWriteTime(exe);
                        try { v.FileVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe).FileVersion ?? ""; } catch (Exception) { }
                        v.FileVersion = v.FileVersion.Replace(", ", ".").Replace(",", ".");
                        list.Add(v);
                    }
                }
                catch (Exception) { }
            }
            return list.OrderByDescending(v => v.Date).ToList();
        }

        // The exe of the version AltHub is told to use, or null for "automatic".
        public static string PinnedExe()
        {
            string pin = Store.Settings.RobloxVersion;
            if (string.IsNullOrEmpty(pin)) return null;
            RobloxVersion v = Installed().FirstOrDefault(x => x.Folder == pin);
            return v == null ? null : v.Exe;
        }

        // The newest public version according to Roblox: { file version, folder }, or null.
        public static async Task<string[]> Latest()
        {
            try
            {
                string json = await Http.GetStringAsync("https://clientsettingscdn.roblox.com/v2/client-version/WindowsPlayer");
                object o = Json.Parse(json);
                string ver = Json.Str(o, "version"), up = Json.Str(o, "clientVersionUpload");
                if (!string.IsNullOrEmpty(up)) return new string[] { ver ?? "", up };
            }
            catch (Exception ex) { Log.Write("RobloxVersions.Latest: " + ex.Message); }
            return null;
        }

        // Recent public releases (newest first) from Roblox's deploy history.
        public static async Task<List<DeployEntry>> History()
        {
            List<DeployEntry> list = new List<DeployEntry>();
            try
            {
                string txt = await Http.GetStringAsync(Cdn + "DeployHistory.txt");
                Regex rx = new Regex(@"New WindowsPlayer (version-[0-9a-fA-F]+) at (.+?), file version: (\d+), (\d+), (\d+), (\d+)");
                foreach (string line in txt.Replace("\r", "").Split('\n'))
                {
                    Match m = rx.Match(line);
                    if (!m.Success) continue;
                    DeployEntry e = new DeployEntry();
                    e.Folder = m.Groups[1].Value;
                    e.Version = m.Groups[3].Value + "." + m.Groups[4].Value + "." + m.Groups[5].Value + "." + m.Groups[6].Value;
                    DateTime dt;
                    if (DateTime.TryParse(m.Groups[2].Value, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out dt)) e.Date = dt;
                    list.Add(e);
                }
            }
            catch (Exception ex) { Log.Write("RobloxVersions.History: " + ex.Message); }
            return list.OrderByDescending(e => e.Date).Take(80).ToList();
        }

        // Where each download package goes inside the version folder (the layout Roblox's own installer uses).
        static readonly Dictionary<string, string> Layout = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "RobloxApp.zip", "" },
            { "shaders.zip", "shaders/" },
            { "ssl.zip", "ssl/" },
            { "WebView2.zip", "" },
            { "content-avatar.zip", "content/avatar/" },
            { "content-configs.zip", "content/configs/" },
            { "content-fonts.zip", "content/fonts/" },
            { "content-sky.zip", "content/sky/" },
            { "content-sounds.zip", "content/sounds/" },
            { "content-textures2.zip", "content/textures/" },
            { "content-models.zip", "content/models/" },
            { "content-textures3.zip", "PlatformContent/pc/textures/" },
            { "content-terrain.zip", "PlatformContent/pc/terrain/" },
            { "content-platform-fonts.zip", "PlatformContent/pc/fonts/" },
            { "extracontent-luapackages.zip", "ExtraContent/LuaPackages/" },
            { "extracontent-translations.zip", "ExtraContent/translations/" },
            { "extracontent-models.zip", "ExtraContent/models/" },
            { "extracontent-textures.zip", "ExtraContent/textures/" },
            { "extracontent-places.zip", "ExtraContent/places/" }
        };

        // Downloads one release into %LocalAppData%\AltHub\Roblox\<folder>. Returns null on success, otherwise what went wrong.
        public static async Task<string> Download(string folder, Action<string> progress)
        {
            try
            {
                if (!Regex.IsMatch(folder ?? "", @"^version-[0-9a-fA-F]+$")) return "That is not a Roblox version folder name.";
                string target = Path.Combine(OwnRoot, folder);
                if (File.Exists(Path.Combine(target, ProcExe))) return null;                 // already there

                progress("Reading the file list of " + folder + "...");
                string manifest = await Http.GetStringAsync(Cdn + folder + "-rbxPkgManifest.txt");
                string[] lines = manifest.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
                List<string[]> pkgs = new List<string[]>();                                  // name, md5
                for (int i = 1; i + 3 < lines.Length; i += 4)
                    if (Layout.ContainsKey(lines[i])) pkgs.Add(new string[] { lines[i], lines[i + 1].ToLowerInvariant() });
                if (!pkgs.Any(p => p[0].Equals("RobloxApp.zip", StringComparison.OrdinalIgnoreCase))) return "Roblox's file list for this version looks unexpected (no RobloxApp.zip).";

                string tmp = target + ".partial";
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                Directory.CreateDirectory(tmp);
                int n = 0;
                foreach (string[] p in pkgs)
                {
                    n++;
                    progress("Downloading " + p[0] + "  (" + n + " of " + pkgs.Count + ")...");
                    byte[] data = await Http.GetByteArrayAsync(Cdn + folder + "-" + p[0]);
                    string got;
                    using (MD5 md5 = MD5.Create()) got = BitConverter.ToString(md5.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
                    if (got != p[1]) { Directory.Delete(tmp, true); return p[0] + " did not download correctly (checksum mismatch). Try again."; }
                    progress("Unpacking " + p[0] + "...");
                    Extract(data, Path.Combine(tmp, Layout[p[0]].Replace('/', Path.DirectorySeparatorChar)));
                }
                File.WriteAllText(Path.Combine(tmp, "AppSettings.xml"),
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<Settings>\r\n\t<ContentFolder>content</ContentFolder>\r\n\t<BaseUrl>http://www.roblox.com</BaseUrl>\r\n</Settings>\r\n");
                if (!File.Exists(Path.Combine(tmp, ProcExe))) { Directory.Delete(tmp, true); return "The download did not contain " + ProcExe + "."; }
                Directory.CreateDirectory(OwnRoot);
                if (Directory.Exists(target)) Directory.Delete(target, true);
                Directory.Move(tmp, target);
                progress("Done.");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error("RobloxVersions.Download", ex);
                return ex.Message;
            }
        }

        static void Extract(byte[] zip, string dir)
        {
            Directory.CreateDirectory(dir);
            string full = Path.GetFullPath(dir);
            using (ZipArchive za = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
                foreach (ZipArchiveEntry e in za.Entries)
                {
                    string name = e.FullName.Replace('\\', '/');
                    string dest = Path.GetFullPath(Path.Combine(dir, name.Replace('/', Path.DirectorySeparatorChar)));
                    if (!dest.StartsWith(full, StringComparison.OrdinalIgnoreCase)) continue;       // never write outside the target folder
                    if (name.EndsWith("/")) { Directory.CreateDirectory(dest); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    using (Stream s = e.Open()) using (FileStream fs = File.Create(dest)) s.CopyTo(fs);
                }
        }
    }
}
