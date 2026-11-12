using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AltHub
{
    // Theme presets and your own colour schemes (Settings > Appearance).
    public partial class MainForm
    {
        void FillThemeCombo()
        {
            if (cbTheme == null) return;
            bool was = loadingSettings;
            loadingSettings = true;
            cbTheme.Items.Clear();
            foreach (string n in Themes.Names()) cbTheme.Items.Add(n);
            int i = cbTheme.Items.IndexOf(Themes.NameFromSetting(Store.Settings.Theme));
            cbTheme.SelectedIndex = i >= 0 ? i : 0;
            loadingSettings = was;
        }

        void BuildThemeButtons(PageBuilder p)
        {
            p.Opt("Make your own colour scheme, or share one.",
                "'Edit colours...' opens the colours of the current theme so you can change them and save the result as your own theme (it appears in the Theme list). 'Export theme...' saves the current colours as a small .aththeme file you can send to someone; 'Import theme...' adds one you received. 'Delete my theme' removes the selected one if it is yours.",
                Ui.Btn("Edit colours...", 150, delegate { EditThemeColours(); }, ""),
                Ui.Btn("Export theme...", 140, delegate { ExportTheme(); }, ""),
                Ui.Btn("Import theme...", 140, delegate { ImportTheme(); }, ""),
                Ui.Btn("Delete my theme", 150, delegate { DeleteCustomTheme(); }, ""));
        }

        void UseTheme(string setting)
        {
            Store.Settings.Theme = setting;
            Store.SaveSettings();
            Theme.Load(setting, Store.Settings.AccentHex);
            ApplyTheme();
            OnAccountSelectionChanged();
            UpdateMultiChip();
            FillThemeCombo();
        }

        ThemeDef CurrentThemeDef()
        {
            ThemeDef src = Theme.Resolve(Store.Settings.Theme);
            ThemeDef d = ThemeDef.Make(src.Name, src.Dark, src.Bg, src.Panel, src.Panel2, src.Border, src.Text, src.Muted, src.Sel, src.Accent);
            if (!string.IsNullOrEmpty(Store.Settings.AccentHex)) d.Accent = Store.Settings.AccentHex;
            return d;
        }

        void ExportTheme()
        {
            ThemeDef d = CurrentThemeDef();
            using (SaveFileDialog s = new SaveFileDialog())
            {
                s.Filter = "AltHub theme (*.aththeme)|*.aththeme";
                s.FileName = d.Name.Replace(' ', '-') + ".aththeme";
                if (s.ShowDialog(this) != DialogResult.OK) return;
                try { File.WriteAllText(s.FileName, Json.To(d), Encoding.UTF8); AddLog("Theme '" + d.Name + "' saved to " + s.FileName + "."); }
                catch (Exception ex) { AddLog("Could not save the theme: " + ex.Message); }
            }
        }

        void ImportTheme()
        {
            using (OpenFileDialog o = new OpenFileDialog())
            {
                o.Filter = "AltHub theme (*.aththeme)|*.aththeme|All files (*.*)|*.*";
                if (o.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    ThemeDef d = Json.From<ThemeDef>(File.ReadAllText(o.FileName, Encoding.UTF8));
                    if (d == null || !d.Valid || string.IsNullOrEmpty(d.Name)) { AddLog("That file is not a valid AltHub theme."); return; }
                    AddCustomTheme(d);
                    UseTheme(d.Name);
                    AddLog("Theme '" + d.Name + "' imported.");
                }
                catch (Exception ex) { AddLog("Could not read the theme: " + ex.Message); }
            }
        }

        void AddCustomTheme(ThemeDef d)
        {
            if (Themes.Presets.Any(p => p.Name.Equals(d.Name, StringComparison.OrdinalIgnoreCase))) d.Name += " (mine)";
            Store.Settings.CustomThemes.RemoveAll(x => x.Name.Equals(d.Name, StringComparison.OrdinalIgnoreCase));
            Store.Settings.CustomThemes.Add(d);
            Store.SaveSettings();
        }

        void DeleteCustomTheme()
        {
            string cur = Store.Settings.Theme;
            ThemeDef mine = Store.Settings.CustomThemes.FirstOrDefault(x => x.Name.Equals(cur, StringComparison.OrdinalIgnoreCase));
            if (mine == null) { AddLog("Select one of your own themes in the Theme list first."); return; }
            Store.Settings.CustomThemes.Remove(mine);
            UseTheme("Dark");
            AddLog("Deleted the theme '" + mine.Name + "'.");
        }

        void EditThemeColours()
        {
            ThemeDef cur = CurrentThemeDef();
            Form f = Dialogs.Make("Edit theme colours", 460, 560);
            Dialogs.Put(f, Ui.Lbl("Colours are #RRGGBB. Save the result under a name to make it your own theme.", "muted"), 16, 12);
            int y = 40;
            RoundTextBox name = Dialogs.Put(f, Ui.Txt(300, cur.Name + " copy"), 140, y);
            Dialogs.Put(f, Ui.Fld("Name", 120), 16, y + 3); y += 36;
            CheckBox dark = Dialogs.Put(f, Ui.Chk("Dark theme (title bar, shading)", cur.Dark, null), 140, y); y += 34;
            string[] labels = new string[] { "Background", "Panels", "Cards / inputs", "Borders", "Text", "Muted text", "Selection", "Accent" };
            string[] vals = new string[] { cur.Bg, cur.Panel, cur.Panel2, cur.Border, cur.Text, cur.Muted, cur.Sel, cur.Accent };
            RoundTextBox[] boxes = new RoundTextBox[8];
            for (int i = 0; i < 8; i++)
            {
                Dialogs.Put(f, Ui.Fld(labels[i], 120), 16, y + 3);
                boxes[i] = Dialogs.Put(f, Ui.Txt(120, vals[i]), 140, y);
                y += 36;
            }
            Dialogs.OkCancel(f, "Save theme");
            Theme.Apply(f);
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                ThemeDef d = ThemeDef.Make(name.Text.Trim(), dark.Checked, boxes[0].Text.Trim(), boxes[1].Text.Trim(), boxes[2].Text.Trim(), boxes[3].Text.Trim(),
                    boxes[4].Text.Trim(), boxes[5].Text.Trim(), boxes[6].Text.Trim(), boxes[7].Text.Trim());
                if (d.Name.Length == 0 || !d.Valid) AddLog("That theme was not saved: give it a name and use #RRGGBB colours.");
                else
                {
                    if (Themes.Parse(d.Accent).IsEmpty) d.Accent = cur.Accent;
                    AddCustomTheme(d);
                    UseTheme(d.Name);
                    AddLog("Saved your theme '" + d.Name + "'.");
                }
            }
            f.Dispose();
        }
    }
}
