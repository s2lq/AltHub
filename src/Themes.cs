using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AltHub
{
    // A complete colour scheme. Colours are "#RRGGBB". Built-in presets below; your own are kept in Settings.CustomThemes and can
    // be exported / imported as .aththeme files (JSON of this class).
    public class ThemeDef
    {
        public string Name = "";
        public bool Dark = true;
        public string Bg = "", Panel = "", Panel2 = "", Border = "", Text = "", Muted = "", Sel = "", Accent = "";

        public static ThemeDef Make(string name, bool dark, string bg, string panel, string panel2, string border, string text, string muted, string sel, string accent)
        {
            ThemeDef d = new ThemeDef();
            d.Name = name; d.Dark = dark; d.Bg = bg; d.Panel = panel; d.Panel2 = panel2; d.Border = border; d.Text = text; d.Muted = muted; d.Sel = sel; d.Accent = accent;
            return d;
        }

        public bool Valid
        {
            get { return Themes.Parse(Bg) != Color.Empty && Themes.Parse(Panel) != Color.Empty && Themes.Parse(Panel2) != Color.Empty && Themes.Parse(Border) != Color.Empty
                && Themes.Parse(Text) != Color.Empty && Themes.Parse(Muted) != Color.Empty && Themes.Parse(Sel) != Color.Empty; }
        }
    }

    public static class Themes
    {
        // The two classic themes first, then the presets, ordered dark to light.
        public static readonly List<ThemeDef> Presets = new List<ThemeDef>
        {
            ThemeDef.Make("Dark",     true,  "#0E1015", "#161920", "#20242D", "#2D323E", "#ECEEF4", "#8A92A3", "#2A325A", "#6366F1"),
            ThemeDef.Make("Midnight", true,  "#080A12", "#0E121E", "#161C2E", "#242C46", "#E2E8F8", "#7884A4", "#1E2C56", "#5B8CFF"),
            ThemeDef.Make("AMOLED",   true,  "#000000", "#0A0A0A", "#161616", "#282828", "#F0F0F0", "#828282", "#23233A", "#8B5CF6"),
            ThemeDef.Make("Slate",    true,  "#181A1E", "#202328", "#2B2F36", "#3E434C", "#E8EAEE", "#969CA6", "#343C50", "#60A5FA"),
            ThemeDef.Make("Ocean",    true,  "#09121A", "#0E1C28", "#152838", "#223E54", "#E2F0FA", "#7A96AA", "#18405F", "#22D3EE"),
            ThemeDef.Make("Forest",   true,  "#0E1411", "#141D18", "#1D2A22", "#2C3E32", "#E2F0E6", "#82988A", "#244230", "#3DDC84"),
            ThemeDef.Make("Rose",     true,  "#160E12", "#201519", "#2E1E27", "#462E3C", "#F6E6EE", "#A48294", "#4A2438", "#F472B6"),
            ThemeDef.Make("Sunset",   true,  "#18100E", "#221816", "#32221E", "#4C342C", "#F6E8E2", "#A88A7E", "#4C2C1E", "#F97316"),
            ThemeDef.Make("Light",    false, "#F2F4F9", "#FFFFFF", "#F6F7FA", "#DEE2EB", "#1C1F26", "#666F82", "#E0E6FF", "#6366F1"),
            ThemeDef.Make("Mint",     false, "#ECF6F2", "#FAFEFC", "#F0F9F5", "#C8E0D6", "#18302A", "#5A7868", "#CEEEE0", "#10B981"),
            ThemeDef.Make("Sand",     false, "#F5F0E6", "#FDFAF4", "#F6F1E7", "#DDD4C3", "#322C22", "#7A705E", "#ECE0C4", "#D97706"),
            // community favourites
            ThemeDef.Make("Dracula",      true,  "#1E1F29", "#282A36", "#343746", "#44475A", "#F8F8F2", "#8E94B8", "#44475A", "#BD93F9"),
            ThemeDef.Make("Nord",         true,  "#242933", "#2E3440", "#3B4252", "#4C566A", "#ECEFF4", "#97A3BA", "#434C5E", "#88C0D0"),
            ThemeDef.Make("Tokyo Night",  true,  "#16161E", "#1A1B26", "#24283B", "#343A55", "#C0CAF5", "#7982A9", "#2F3550", "#7AA2F7"),
            ThemeDef.Make("Catppuccin",   true,  "#181825", "#1E1E2E", "#2A2B3C", "#3B3D52", "#CDD6F4", "#8F93AE", "#383A52", "#CBA6F7"),
            ThemeDef.Make("Gruvbox",      true,  "#1D2021", "#282828", "#32302F", "#473F3B", "#EBDBB2", "#A89984", "#3C3836", "#FABD2F"),
            ThemeDef.Make("Monokai",      true,  "#1E1F1C", "#272822", "#33342C", "#49483E", "#F8F8F2", "#A0A08A", "#3E3D32", "#A6E22E"),
            ThemeDef.Make("Cyberpunk",    true,  "#0B0714", "#120C20", "#1C1330", "#33224F", "#F2E9FF", "#9D86C4", "#3A1E5A", "#FF2E97"),
            ThemeDef.Make("Solarized",    true,  "#00212B", "#002B36", "#073642", "#17505E", "#EEE8D5", "#93A1A1", "#0B4452", "#2AA198"),
            ThemeDef.Make("Latte",        false, "#E6E9EF", "#EFF1F5", "#E9ECF1", "#CCD0DA", "#4C4F69", "#7C7F93", "#D8DDF2", "#8839EF"),
            ThemeDef.Make("Paper",        false, "#EDEDED", "#FAFAFA", "#F3F3F3", "#D9D9D9", "#222222", "#6E6E6E", "#E2E2E2", "#2563EB")
        };

        public static Color Parse(string hex)
        {
            try
            {
                string h = (hex ?? "").Trim().TrimStart('#');
                if (h.Length == 6) return Color.FromArgb(Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16), Convert.ToInt32(h.Substring(4, 2), 16));
            }
            catch (Exception) { }
            return Color.Empty;
        }

        public static string Hex(Color c) { return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); }

        // Finds a theme by name among the presets and your own.
        public static ThemeDef Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            ThemeDef d = Presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (d != null) return d;
            return Store.Settings.CustomThemes.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        // What the Theme setting can be: Dark, Light, Follow Windows, the other presets, then your own.
        public static List<string> Names()
        {
            List<string> n = new List<string>();
            n.Add("Dark"); n.Add("Light"); n.Add("Follow Windows");
            foreach (ThemeDef p in Presets) if (p.Name != "Dark" && p.Name != "Light") n.Add(p.Name);
            foreach (ThemeDef c in Store.Settings.CustomThemes) if (!n.Contains(c.Name)) n.Add(c.Name);
            return n;
        }

        public static string SettingFromName(string shown) { return shown == "Follow Windows" ? "System" : shown; }
        public static string NameFromSetting(string setting) { return setting == "System" ? "Follow Windows" : (string.IsNullOrEmpty(setting) ? "Dark" : setting); }
    }
}
