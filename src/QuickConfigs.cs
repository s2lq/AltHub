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
            "LaunchMethod", "LaunchDelaySec", "AutoCloseLast", "MaxInstances", "RefreshCookieOnLaunch",
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
