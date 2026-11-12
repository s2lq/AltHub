using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace AltHub
{
    // One-click settings for how heavy Roblox may be, from "ultimate performance" (lowest graphics, tiny memory and CPU
    // budget per window) up to "best visuals" (high graphics, no limits).
    public class QuickConfig
    {
        public string Name = "";
        public string Tagline = "";
        public string Description = "";
        public Action<AppSettings> Apply;
    }

    public static class QuickConfigs
    {
        public static readonly List<QuickConfig> All = new List<QuickConfig>
        {
            Make("Ultimate performance", "Most alts on the weakest PC",
                "Lowest graphics, 15 FPS, about 250 MB of memory and at most 6 % of the PC's CPU per window, background windows throttled, the memory cleaner on.",
                delegate(AppSettings s) {
                    Gfx(s, "Lowest", 0, -1, 0); s.FpsCap = 15; s.Priority = "BelowNormal";
                    s.MaxAlts = true; s.MaxAltsFps = 8; s.MaxAltsIdle = true; s.MaxAltsTrim = true; s.MaxAltsMinimize = false;
                    s.RamCapMode = "Auto250"; s.RamCapSkipActive = true; s.CpuCapMode = "Each"; s.CpuCapPercent = 6; s.CpuCapSkipActive = true;
                    s.Mem.AutoClean = true; s.Mem.Threshold = 75; }),
            Make("Performance", "Many alts, still playable",
                "Low graphics, 20 FPS, about 250 MB of memory and at most 10 % of the PC's CPU per window, background windows throttled.",
                delegate(AppSettings s) {
                    Gfx(s, "LowCpu", 0, -1, 0); s.FpsCap = 20; s.Priority = "BelowNormal";
                    s.MaxAlts = true; s.MaxAltsFps = 10; s.MaxAltsIdle = true; s.MaxAltsTrim = true; s.MaxAltsMinimize = false;
                    s.RamCapMode = "Auto250"; s.RamCapSkipActive = true; s.CpuCapMode = "Each"; s.CpuCapPercent = 10; s.CpuCapSkipActive = true;
                    s.Mem.AutoClean = true; s.Mem.Threshold = 80; }),
            Make("Balanced", "A good middle",
                "Light graphics, 30 FPS, up to 500 MB of memory per window, background windows slowed a little, no CPU cap.",
                delegate(AppSettings s) {
                    Gfx(s, "Light", 0, -1, 0); s.FpsCap = 30; s.Priority = "Normal";
                    s.MaxAlts = true; s.MaxAltsFps = 15; s.MaxAltsIdle = true; s.MaxAltsTrim = false; s.MaxAltsMinimize = false;
                    s.RamCapMode = "Fixed500"; s.RamCapSkipActive = true; s.CpuCapMode = "Off";
                    s.Mem.AutoClean = false; }),
            Make("Quality", "Nice looking, still sensible",
                "Roblox's own graphics, 60 FPS, no memory or CPU limits, nothing throttled.",
                delegate(AppSettings s) {
                    Gfx(s, "Default", 0, -1, 0); s.FpsCap = 60; s.Priority = "Normal";
                    s.MaxAlts = false; s.RamCapMode = "Off"; s.CpuCapMode = "Off"; s.Mem.AutoClean = false; }),
            Make("Best visuals", "Everything on high",
                "Highest graphics quality, high textures, 4x anti-aliasing, 120 FPS, no limits. Needs a strong PC and works best with one or two windows.",
                delegate(AppSettings s) {
                    Gfx(s, "Custom", 21, 3, 4); s.FpsCap = 120; s.Priority = "Normal";
                    s.MaxAlts = false; s.RamCapMode = "Off"; s.CpuCapMode = "Off"; s.Mem.AutoClean = false; })
        };

        // Ready-made setups in the style people share: one click each, same format as an imported .athcfg.
        public static readonly List<QuickConfig> Community = new List<QuickConfig>
        {
            Make("AFK farm overnight", "Leave it running all night",
                "Lowest graphics, 10 FPS, about 250 MB per window, anti-AFK every 10 minutes, auto-relaunch watcher with rejoin, the memory cleaner on and logins checked every 3 hours.",
                delegate(AppSettings s) {
                    Gfx(s, "Lowest", 0, -1, 0); s.FpsCap = 10; s.Priority = "Idle";
                    s.MaxAlts = true; s.MaxAltsFps = 5; s.MaxAltsIdle = true; s.MaxAltsTrim = true; s.MaxAltsMinimize = false;
                    s.RamCapMode = "Auto250"; s.RamCapSkipActive = false; s.CpuCapMode = "Each"; s.CpuCapPercent = 5; s.CpuCapSkipActive = false;
                    s.AntiAfk = true; s.AntiAfkMin = 10; s.AutoRelaunch = true; s.WatcherSec = 60; s.RejoinSameServer = true;
                    s.HealthAuto = true; s.HealthHours = 3; s.Mem.AutoClean = true; s.Mem.Threshold = 70; }),
            Make("Low-end laptop", "Few GB of RAM, weak CPU",
                "Lowest graphics, 20 FPS, 250 MB per window, at most 3 windows at a time, a short launch pause and no background checks that cost battery.",
                delegate(AppSettings s) {
                    Gfx(s, "Lowest", 0, -1, 0); s.FpsCap = 20; s.Priority = "BelowNormal"; s.MaxInstances = 3;
                    s.MaxAlts = true; s.MaxAltsFps = 10; s.MaxAltsIdle = true; s.MaxAltsTrim = true;
                    s.RamCapMode = "Auto250"; s.CpuCapMode = "Total"; s.CpuCapPercent = 60;
                    s.PresenceSec = 60; s.HealthAuto = false; s.Mem.AutoClean = true; s.Mem.Threshold = 70; }),
            Make("Alt army (20+)", "As many windows as the PC can hold",
                "Lowest graphics, 12 FPS, 250 MB and 4 % CPU per window, fast launching, memory cleaner at 80 %. Needs 32 GB of RAM or more for 20 windows.",
                delegate(AppSettings s) {
                    Gfx(s, "Lowest", 0, -1, 0); s.FpsCap = 12; s.Priority = "BelowNormal"; s.MaxInstances = 0;
                    s.MaxAlts = true; s.MaxAltsFps = 6; s.MaxAltsIdle = true; s.MaxAltsTrim = true;
                    s.RamCapMode = "Auto250"; s.CpuCapMode = "Each"; s.CpuCapPercent = 4;
                    s.FastLaunch = true; s.FastLaunchParallel = 4; s.Mem.AutoClean = true; s.Mem.Threshold = 80; }),
            Make("Stream and play", "Smooth main window, quiet alts",
                "Your main window keeps full quality at 60 FPS, the others run light in the background and are slowed while you are not looking at them.",
                delegate(AppSettings s) {
                    Gfx(s, "Light", 0, -1, 0); s.FpsCap = 60; s.Priority = "Normal";
                    s.MaxAlts = true; s.MaxAltsFps = 20; s.MaxAltsIdle = true; s.MaxAltsTrim = false;
                    s.RamCapMode = "Fixed500"; s.RamCapSkipActive = true; s.CpuCapMode = "Each"; s.CpuCapPercent = 12; s.CpuCapSkipActive = true;
                    s.Mem.AutoClean = false; }),
            Make("Screenshots and showcase", "Looks over everything",
                "Highest quality, high textures, 4x anti-aliasing, 144 FPS, no limits. Best with one or two windows.",
                delegate(AppSettings s) {
                    Gfx(s, "Custom", 21, 3, 4); s.FpsCap = 144; s.Priority = "Normal";
                    s.MaxAlts = false; s.RamCapMode = "Off"; s.CpuCapMode = "Off"; s.Mem.AutoClean = false; }),
            Make("Battery saver", "Longest time on battery",
                "Lowest graphics, 10 FPS, low priority, 3 % CPU per window and the checks run less often.",
                delegate(AppSettings s) {
                    Gfx(s, "Lowest", 0, -1, 0); s.FpsCap = 10; s.Priority = "Idle";
                    s.MaxAlts = true; s.MaxAltsFps = 5; s.MaxAltsIdle = true; s.MaxAltsTrim = true;
                    s.RamCapMode = "Auto250"; s.CpuCapMode = "Each"; s.CpuCapPercent = 3;
                    s.PresenceSec = 120; s.WatcherSec = 120; s.HealthAuto = false; s.Mem.AutoClean = true; s.Mem.Threshold = 70; })
        };

        static QuickConfig Make(string name, string tag, string desc, Action<AppSettings> apply)
        {
            QuickConfig q = new QuickConfig();
            q.Name = name; q.Tagline = tag; q.Description = desc; q.Apply = apply;
            return q;
        }

        static void Gfx(AppSettings s, string preset, int quality, int texture, int msaa)
        {
            s.Gfx.Preset = preset; s.Gfx.Quality = quality; s.Gfx.Texture = texture; s.Gfx.Msaa = msaa;
            s.Gfx.NoGrass = preset == "Lowest" || preset == "LowCpu"; s.Gfx.PauseVoxelizer = preset == "Lowest";
            s.Gfx.GraySky = false; s.Gfx.LowCsg = preset == "Lowest" || preset == "LowCpu";
        }

        public static QuickConfig Find(string name) { return All.FirstOrDefault(q => q.Name == name); }
    }

    // Shareable configs: the performance / behaviour settings (never accounts, cookies, webhook URLs, proxies or private servers)
    // as a small .athcfg file that others can import.
    public static class ConfigShare
    {
        static readonly string[] Shared = new string[]
        {
            "Gfx", "FpsCap", "Priority", "MaxAlts", "MaxAltsFps", "MaxAltsIdle", "MaxAltsTrim", "MaxAltsMinimize", "Mem",
            "CpuCapMode", "CpuCapPercent", "CpuCapSkipActive", "RamCapMode", "RamCapSkipActive",
            "LaunchMethod", "LaunchDelaySec", "FastLaunch", "FastLaunchStaggerMs", "FastLaunchParallel", "AutoCloseLast", "MaxInstances", "RefreshCookieOnLaunch",
            "ShowPresence", "PresenceSec", "AutoRelaunch", "WatcherSec", "WatcherUsePresence", "RejoinSameServer",
            "CrashBreaker", "CrashBreakerMax", "CrashBreakerWindowMin", "CrashBreakerCooldownMin",
            "AntiAfk", "AntiAfkMin", "AntiAfkAction", "Layout", "HealthAuto", "HealthHours",
            "Theme", "AccentHex", "CustomThemes", "Language", "View",
            "Tray", "CloseToTray", "MinToTray", "Toasts", "HotkeysLaunch", "CheckUpdates", "UseSavedPerAccount", "PresetExactServers"
        };

        public static string Export(string path, string name)
        {
            try
            {
                Dictionary<string, object> map = new Dictionary<string, object>();
                foreach (string key in Shared)
                {
                    FieldInfo f = typeof(AppSettings).GetField(key);
                    if (f != null) map[key] = f.GetValue(Store.Settings);
                }
                Dictionary<string, object> file = new Dictionary<string, object>();
                file["format"] = "althub-config";
                file["version"] = 1;
                file["name"] = name;
                file["settings"] = map;
                JavaScriptSerializer ser = new JavaScriptSerializer();
                ser.MaxJsonLength = int.MaxValue;
                File.WriteAllText(path, ser.Serialize(file), Encoding.UTF8);
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        // Reads a config file and returns its name and settings (without applying anything), or an error text in err.
        public static IDictionary<string, object> Read(string path, out string name, out string err)
        {
            name = ""; err = null;
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                ser.MaxJsonLength = int.MaxValue;
                IDictionary<string, object> root = ser.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as IDictionary<string, object>;
                if (root == null || !root.ContainsKey("format") || Convert.ToString(root["format"]) != "althub-config") { err = "That file is not an AltHub config."; return null; }
                name = root.ContainsKey("name") ? Convert.ToString(root["name"]) : "";
                IDictionary<string, object> s = root.ContainsKey("settings") ? root["settings"] as IDictionary<string, object> : null;
                if (s == null) { err = "The config has no settings."; return null; }
                return s;
            }
            catch (Exception ex) { err = ex.Message; return null; }
        }

        // Applies the known settings of a config. Returns how many were applied.
        public static int Apply(IDictionary<string, object> settings)
        {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            int n = 0;
            foreach (KeyValuePair<string, object> kv in settings)
            {
                if (Array.IndexOf(Shared, kv.Key) < 0) continue;
                FieldInfo f = typeof(AppSettings).GetField(kv.Key);
                if (f == null) continue;
                try { f.SetValue(Store.Settings, ser.ConvertToType(kv.Value, f.FieldType)); n++; }
                catch (Exception) { }
            }
            return n;
        }
    }
}
