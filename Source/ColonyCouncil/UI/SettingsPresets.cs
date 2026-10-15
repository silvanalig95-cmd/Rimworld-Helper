using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Reading and writing CouncilSettings by field name, for the settings window and its presets: the fields named in
    /// <see cref="SettingsCatalog"/>, their defaults, whether a row's needs are met, and how a value reads in a list.
    /// </summary>
    public static class SettingsAccess
    {
        private static Dictionary<string, FieldInfo> fields;
        private static CouncilSettings defaults;
        private static bool verified;

        private static Dictionary<string, FieldInfo> Fields =>
            fields ??= typeof(CouncilSettings).GetFields(BindingFlags.Public | BindingFlags.Instance).ToDictionary(f => f.Name);

        /// <summary>Settings as Rimfolk ships them.</summary>
        public static CouncilSettings Defaults => defaults ??= new CouncilSettings();

        public static FieldInfo Field(string id) => id != null && Fields.TryGetValue(id, out var f) ? f : null;

        public static object Get(CouncilSettings s, string id) => Field(id)?.GetValue(s);

        public static object DefaultOf(string id) => Field(id)?.GetValue(Defaults);

        public static void Set(CouncilSettings s, string id, object value)
        {
            var f = Field(id);
            if (f != null && value != null) f.SetValue(s, value);
        }

        /// <summary>A preset's value as text, as the field's type: "true", "0.75", "Demanding".</summary>
        public static object Parse(Type type, string text)
        {
            if (type == typeof(bool)) return bool.Parse(text);
            if (type == typeof(float)) return float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (type == typeof(int)) return int.Parse(text, CultureInfo.InvariantCulture);
            if (type.IsEnum) return Enum.Parse(type, text);
            if (type == typeof(string)) return text;
            throw new ArgumentException("can't set a " + type.Name + " from a preset");
        }

        /// <summary>Equal, with floats compared to a hair (sliders round to steps of 0.05 at the finest).</summary>
        public static bool Same(object a, object b)
        {
            if (a is float fa && b is float fb) return Mathf.Abs(fa - fb) < 0.001f;
            return Equals(a, b);
        }

        public static bool IsDefault(CouncilSettings s, string id) => Same(Get(s, id), DefaultOf(id));

        /// <summary>Whether a row's needs are met: its switches on (or off), its options chosen, and a game loaded where it needs one.</summary>
        public static bool Met(CouncilSettings s, SettingEntry e)
        {
            if (e.id == "colonyEra" || e.id == "selfCheck") return Current.ProgramState == ProgramState.Playing && CouncilGameComponent.Instance != null;
            foreach (var t in SettingsCatalog.ParseNeeds(e.needs))
            {
                object v = Get(s, t.field);
                if (v == null) continue;
                if (t.values == null)
                {
                    if (v is bool b && b == t.negate) return false;
                }
                else if (t.values.Contains(v.ToString()) == t.negate) return false;
            }
            return true;
        }

        /// <summary>The row's label as one plain line, for lists: a "... and so on" row says what it belongs to.</summary>
        public static string PlainLabel(SettingEntry e, string valueText = null)
        {
            string raw = e.label.Translate().RawText ?? e.label;
            string text = raw.Contains("{0}") ? e.label.Translate(valueText ?? "").Resolve() : raw;
            text = text.TrimEnd(' ', ':');
            if (text.StartsWith("...") || text.StartsWith("…"))
            {
                var parent = SettingsCatalog.Find(e.Parent);
                text = text.TrimStart('.', '…', ' ');
                if (parent != null) text = PlainLabel(parent) + " › " + text;
            }
            return text;
        }

        /// <summary>A value as the player reads it: on/off, 75%, 4.5, or the option's name.</summary>
        public static string ValueText(SettingEntry e, object v)
        {
            switch (v)
            {
                case null: return "-";
                case bool b: return (b ? "CC_SetUI_On" : "CC_SetUI_Off").Translate();
                case float f: return e?.format == "%" ? f.ToStringPercent() : f.ToString(e?.format ?? "0.##");
                case int i: return i.ToString();
            }
            if (v.GetType().IsEnum && e?.optionPrefix != null) return OptionLabel(e, v.ToString());
            return v.ToString();
        }

        public static string OptionLabel(SettingEntry e, string option) => (e.optionPrefix + option).Translate().CapitalizeFirst();

        /// <summary>What has to happen at once when a setting changes (the rest is read where it's used).</summary>
        public static void AfterChange(ICollection<string> ids)
        {
            try
            {
                if (ids.Contains("eventsFromStoryteller")) HappeningTracker.ApplyEventMode();
                if (ids.Contains("buttonPosition") || ids.Contains("roomsButton")) CouncilStartup.ApplyButtonPosition(true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("applying a changed setting", e);
            }
        }

        /// <summary>
        /// Once per session: every public field of CouncilSettings is drawn on exactly one page or listed as not a setting,
        /// and every row names a real field. Tools/Tests checks the same; this catches a game build that skipped the tests.
        /// </summary>
        public static void Verify()
        {
            if (verified) return;
            verified = true;
            try
            {
                var drawn = SettingsCatalog.DrawnFields.GroupBy(f => f).ToDictionary(g => g.Key, g => g.Count());
                var problems = new List<string>();
                foreach (var f in Fields.Keys)
                {
                    drawn.TryGetValue(f, out int n);
                    bool skipped = SettingsCatalog.NotDrawn.ContainsKey(f);
                    if (n == 0 && !skipped) problems.Add(f + " is on no page");
                    if (n > 1) problems.Add(f + " is on " + n + " rows");
                    if (n > 0 && skipped) problems.Add(f + " is drawn but listed as not a setting");
                }
                foreach (var f in drawn.Keys.Where(f => !Fields.ContainsKey(f))) problems.Add("a row names " + f + ", which isn't a setting");
                if (problems.Count > 0) CouncilLog.WarnOnce("settings window: " + string.Join("; ", problems));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("checking the settings window", e);
            }
        }
    }

    /// <summary>
    /// The presets and the full reset: each works out what it would change, says so in a confirmation, and only then
    /// sets the values. A preset sets only the fields it lists (see <see cref="SettingsCatalog"/>).
    /// </summary>
    public static class SettingsPresets
    {
        public struct Change
        {
            public SettingEntry entry;
            public string field;
            public object from;
            public object to;
        }

        /// <summary>What applying the preset would change now: only the values that differ.</summary>
        public static List<Change> Changes(CouncilSettings s, SettingsPreset p)
        {
            var list = new List<Change>();
            foreach (var v in p.values)
            {
                var f = SettingsAccess.Field(v.field);
                if (f == null) continue;
                object now = f.GetValue(s);
                if (v.onlyIfNow != null && !v.onlyIfNow.Split('|').Contains(now?.ToString())) continue;
                object to;
                try
                {
                    to = v.value == SettingsCatalog.DefaultValue ? SettingsAccess.DefaultOf(v.field) : SettingsAccess.Parse(f.FieldType, v.value);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("preset " + p.id + ", " + v.field, e);
                    continue;
                }
                if (SettingsAccess.Same(now, to)) continue;
                list.RemoveAll(c => c.field == v.field);
                list.Add(new Change { entry = SettingsCatalog.Find(v.field), field = v.field, from = now, to = to });
            }
            return list;
        }

        /// <summary>What a full reset would change: every setting the window draws, back to its default.</summary>
        public static List<Change> ResetChanges(CouncilSettings s)
        {
            var list = new List<Change>();
            foreach (var e in SettingsCatalog.All)
                foreach (var field in SettingsCatalog.FieldsOf(e))
                {
                    object now = SettingsAccess.Get(s, field), to = SettingsAccess.DefaultOf(field);
                    if (!SettingsAccess.Same(now, to)) list.Add(new Change { entry = e, field = field, from = now, to = to });
                }
            return list;
        }

        public static void Confirm(CouncilSettings s, SettingsPreset p)
        {
            var changes = Changes(s, p);
            string name = p.label.Translate();
            if (changes.Count == 0)
            {
                Find.WindowStack.Add(new Dialog_MessageBox("CC_SetPreset_Nothing".Translate(name), title: name));
                return;
            }
            string text = "CC_SetPreset_Confirm".Translate(p.tip.Translate(), Count(changes), List(changes));
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, () => Apply(s, changes), false, name));
        }

        public static void ConfirmReset(CouncilSettings s)
        {
            var changes = ResetChanges(s);
            string name = "CC_Settings_Reset".Translate();
            if (changes.Count == 0)
            {
                Find.WindowStack.Add(new Dialog_MessageBox("CC_SetReset_Nothing".Translate(), title: name));
                return;
            }
            string text = "CC_SetReset_Confirm".Translate(Count(changes), List(changes));
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, () => Apply(s, changes), true, name));
        }

        public static void Apply(CouncilSettings s, List<Change> changes)
        {
            foreach (var c in changes) SettingsAccess.Set(s, c.field, c.to);
            SettingsAccess.AfterChange(changes.Select(c => c.field).ToList());
        }

        private static string Count(List<Change> changes)
        {
            int n = changes.Select(Line).Distinct().Count();
            return n == 1 ? "CC_SetPreset_One".Translate() : "CC_SetPreset_Many".Translate(n);
        }

        /// <summary>The changes as lines under their page names, in the order the pages show them.</summary>
        public static string List(List<Change> changes)
        {
            var sb = new StringBuilder();
            var order = new Dictionary<SettingEntry, int>();
            for (int i = 0; i < SettingsCatalog.All.Count; i++) order[SettingsCatalog.All[i]] = i;
            foreach (var page in SettingsCatalog.Pages)
            {
                var lines = changes.Where(c => (c.entry?.page ?? SettingsPage.Advanced) == page)
                    .OrderBy(c => c.entry != null && order.TryGetValue(c.entry, out int i) ? i : int.MaxValue)
                    .Select(Line).Distinct().ToList();
                if (lines.Count == 0) continue;
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine(SettingsCatalog.PageKey(page).Translate().Colorize(UiKit.HeaderColor));
                foreach (var l in lines) sb.AppendLine("  - " + l);
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>"Stakes: Demanding (was Balanced)", or for a slider whose label holds the value, "About one ... every 8 days (was 4)".</summary>
        public static string Line(Change c)
        {
            var e = c.entry;
            if (e == null) return c.field;
            if (e.kind == SettingKind.Action) return SettingsAccess.PlainLabel(e);
            string to = SettingsAccess.ValueText(e, c.to), from = SettingsAccess.ValueText(e, c.from);
            string raw = e.label.Translate().RawText ?? "";
            string head = raw.Contains("{0}") ? SettingsAccess.PlainLabel(e, to) : SettingsAccess.PlainLabel(e) + ": " + to;
            return "CC_SetUI_Was".Translate(head, from);
        }
    }
}
