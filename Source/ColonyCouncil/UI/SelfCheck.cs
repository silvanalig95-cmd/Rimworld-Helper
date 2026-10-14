using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: "Rimfolk: check myself" (Debug actions, Rimfolk). One window for the player to screenshot when
    /// something looks wrong: the game, Rimfolk and Harmony versions, which game hooks took and which didn't (and why),
    /// which of Rimfolk's parts are switched on, which of the mods it works with it found, and its own lines in the log
    /// this session. Plain English, like the game's own debug tools. Built once when opened (and on "Check again"),
    /// never per frame.
    /// </summary>
    public static class SelfCheck
    {
        [DebugAction("Rimfolk", "Rimfolk: check myself", displayPriority = 1000, allowedGameStates = AllowedGameStates.Playing)]
        private static void CheckMyself() => Find.WindowStack.Add(new Dialog_SelfCheck());

        [DebugAction("Rimfolk", "Send the welcome letter again", allowedGameStates = AllowedGameStates.Playing)]
        private static void WelcomeAgain()
        {
            CouncilGameComponent.Instance?.MarkWelcomed();
            WelcomeLetter.Send();
        }

        /// <summary>One part of the report: a heading, its overall state, and its lines.</summary>
        public class Part
        {
            public string heading;
            public Color color = UiKit.Good;
            public List<(string text, Color color)> lines = new List<(string, Color)>();

            public void Add(string text, Color? color = null) => lines.Add((text, color ?? Color.white));
        }

        /// <summary>
        /// The mods Rimfolk reads or adapts to in code, by name and package id. Content written for other mods (Almanac
        /// pages, events, people) is found from the defs themselves, under "Content for your other mods".
        /// </summary>
        private static readonly (string name, string[] ids)[] Integrations =
        {
            ("RimHUD", new[] { "jaxe.rimhud" }),
            ("Dubs Bad Hygiene", new[] { "dubwise.dubsbadhygiene" }),
            ("Hospitality", new[] { "orion.hospitality" }),
            ("Vanilla Social Interactions Expanded", new[] { "vanillaexpanded.vanillasocialinteractionsexpanded" }),
            ("Vanilla Skills Expanded", new[] { "vanillaexpanded.skills" }),
            ("Vanilla Outposts Expanded", new[] { "vanillaexpanded.outposts" }),
            ("Vanilla Cooking Expanded", new[] { "vanillaexpanded.vcooke" }),
            ("A RimWorld of Magic", new[] { "torann.arimworldofmagic" }),
            ("Alpha Animals", new[] { "sarg.alphaanimals" }),
            ("PokeWorld", PokeWorldLink.KnownPackageIds),
            ("Medieval Overhaul", new[] { "dankpyon.medieval.overhaul" }),
        };

        public static List<Part> Report()
        {
            var parts = new List<Part>();
            Safe(parts, "Versions", Versions);
            Safe(parts, "Game hooks", Hooks);
            Safe(parts, "Rimfolk's parts", Parts);
            Safe(parts, "Mods Rimfolk works with", Mods);
            Safe(parts, "Rimfolk in the log this session", LogLines);
            return parts;
        }

        /// <summary>Each part on its own: one that throws says so and the rest still show.</summary>
        private static void Safe(List<Part> parts, string heading, Action<Part> fill)
        {
            var part = new Part { heading = heading };
            try
            {
                fill(part);
            }
            catch (Exception e)
            {
                part.color = UiKit.Bad;
                part.Add("Couldn't check this: " + e.GetType().Name + ": " + e.Message, UiKit.Bad);
            }
            parts.Add(part);
        }

        private static void Versions(Part part)
        {
            var meta = ColonyCouncilMod.Pack?.ModMetaData;
            part.Add("RimWorld " + VersionControl.CurrentVersionStringWithRev + ", Rimfolk " + (meta?.ModVersion ?? "?") + ", Harmony "
                     + typeof(Harmony).Assembly.GetName().Version);
            var active = ModsConfig.ActiveModsInLoadOrder.ToList();
            int ours = active.FindIndex(m => (m.PackageId ?? "").ToLowerInvariant().StartsWith(ColonyCouncilMod.PackageId));
            int harmony = active.FindIndex(m => (m.PackageId ?? "").ToLowerInvariant().StartsWith("brrainz.harmony"));
            string order = harmony < 0 ? "Harmony isn't in the mod list (it may come from elsewhere)"
                : ours >= 0 && harmony > ours ? "Harmony loads AFTER Rimfolk: move it above" : "Harmony loads before Rimfolk";
            part.Add(active.Count + " active mods" + (ours >= 0 ? "; Rimfolk is number " + (ours + 1) : "") + ". " + order + ".",
                harmony > ours && ours >= 0 ? UiKit.Bad : Color.white);
            if (harmony > ours && ours >= 0) part.color = UiKit.Bad;
            var dlcs = new List<string>();
            if (ModsConfig.RoyaltyActive) dlcs.Add("Royalty");
            if (ModsConfig.IdeologyActive) dlcs.Add("Ideology");
            if (ModsConfig.BiotechActive) dlcs.Add("Biotech");
            if (ModsConfig.AnomalyActive) dlcs.Add("Anomaly");
            if (ModsConfig.OdysseyActive) dlcs.Add("Odyssey");
            part.Add("DLCs: " + (dlcs.Count > 0 ? string.Join(", ", dlcs) : "none") + ". Language: "
                     + (LanguageDatabase.activeLanguage?.FriendlyNameEnglish ?? "?") + ".");
            string screen = "Screen " + Screen.width + "x" + Screen.height + " pixels at interface scale " + Prefs.UIScale.ToString("0.##")
                            + " (" + UI.screenWidth + "x" + UI.screenHeight + ").";
            if (RimfolkTab.LastPaneTopY >= 0f)
                screen += " Inspect pane top at " + RimfolkTab.LastPaneTopY.ToString("0") + "; Rimfolk's pawn tabs last drawn "
                          + RimfolkTab.LastHeight.ToString("0") + " tall.";
            part.Add(screen, UiKit.Faint);
        }

        private static void Hooks(Part part)
        {
            var applied = RimfolkPatches.Applied;
            var failed = RimfolkPatches.Failed;
            if (RimfolkPatches.NotStarted != null || applied.Count + failed.Count == 0)
            {
                part.color = UiKit.Bad;
                part.Add("None were applied: " + (RimfolkPatches.NotStarted ?? "is Harmony loaded before Rimfolk?"), UiKit.Bad);
                return;
            }
            part.Add(applied.Count + " applied, " + failed.Count + " didn't.", failed.Count > 0 ? UiKit.Warn : UiKit.Good);
            if (failed.Count > 0) part.color = UiKit.Warn;
            if (!RimfolkPatches.On)
            {
                part.color = UiKit.Warn;
                part.Add("The \"deeper game hooks\" setting is off: they're in place but do nothing.", UiKit.Warn);
            }
            foreach (var name in failed)
                part.Add("Didn't apply: " + name + Target(name) + ". " + (RimfolkPatches.FailedWhy.TryGetValue(name, out var why) ? why : ""), UiKit.Bad);
            part.Add("Applied: " + string.Join(", ", applied.Select(n => n + Target(n))) + ".", UiKit.Faint);

            // What Harmony itself holds: the methods Rimfolk patches, and which other mods patch the same ones.
            int ours = 0;
            var shared = new List<string>();
            foreach (var m in Harmony.GetAllPatchedMethods())
            {
                var info = Harmony.GetPatchInfo(m);
                if (info?.Owners == null || !info.Owners.Contains(ColonyCouncilMod.PackageId)) continue;
                ours++;
                var others = info.Owners.Where(o => o != ColonyCouncilMod.PackageId).ToList();
                if (others.Count > 0) shared.Add(m.DeclaringType?.Name + "." + m.Name + " (also " + string.Join(", ", others) + ")");
            }
            part.Add("Harmony lists " + ours + " game methods patched by Rimfolk." + (shared.Count > 0 ? " Also patched by other mods: " + string.Join("; ", shared) + "." : ""),
                UiKit.Faint);
        }

        /// <summary>" (Type.Method)" for a patch class, from its HarmonyPatch attribute, when it names its target there.</summary>
        private static string Target(string patchClass)
        {
            try
            {
                var type = typeof(RimfolkPatches).Assembly.GetTypes().FirstOrDefault(t => t.Name == patchClass);
                if (type == null) return "";
                var infos = type.GetCustomAttributes(typeof(HarmonyPatch), false).OfType<HarmonyAttribute>().Select(a => a.info).Where(i => i != null).ToList();
                string declaring = infos.Select(i => i.declaringType?.Name).LastOrDefault(n => n != null);
                string method = infos.Select(i => i.methodName).LastOrDefault(n => n != null);
                if (declaring == null && method == null) return "";
                return " (" + (declaring ?? "?") + (method != null ? "." + method : "") + ")";
            }
            catch
            {
                return "";
            }
        }

        private static void Parts(Part part)
        {
            var s = ColonyCouncilMod.Settings;
            var defaults = new CouncilSettings();
            var off = new List<string>();
            var changed = new List<string>();
            int switches = 0;
            foreach (var f in typeof(CouncilSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object now = f.GetValue(s), was = f.GetValue(defaults);
                if (f.FieldType == typeof(bool))
                {
                    switches++;
                    if (!(bool)now) off.Add(f.Name);
                    if (!Equals(now, was)) changed.Add(f.Name + " " + ((bool)now ? "on" : "off"));
                }
                else if (f.FieldType == typeof(float) || f.FieldType == typeof(int) || f.FieldType.IsEnum)
                {
                    if (f.Name.StartsWith("slowButtons")) continue;
                    if (!Equals(now, was)) changed.Add(f.Name + " " + Format(now) + " (default " + Format(was) + ")");
                }
            }
            part.Add("Stakes: " + s.stakes + ". Life's ups and downs: " + s.lifePressure + ". Council button: " + s.buttonPosition + ".");
            var comp = CouncilGameComponent.Instance;
            if (comp != null)
                part.Add("This colony: era " + comp.era + " (works out as " + Avail.EffectiveTech.ToStringHuman() + "), "
                         + PawnsFinder.AllMaps_FreeColonists.Count() + " colonists on maps, " + comp.chronicle.Count + " chronicle entries, welcome letter "
                         + (comp.Welcomed ? "sent or not needed" : "still to come") + ".");
            part.Add(off.Count == 0 ? "All " + switches + " switches are on." : (switches - off.Count) + " of " + switches + " switches on. Off: " + string.Join(", ", off) + ".",
                off.Count == 0 ? UiKit.Good : Color.white);
            part.Add(changed.Count == 0 ? "Everything else is at its default." : "Changed from the default: " + string.Join(", ", changed) + ".", UiKit.Faint);
        }

        private static string Format(object v) => v is float f ? f.ToString("0.##") : v?.ToString() ?? "null";

        private static void Mods(Part part)
        {
            var found = new List<string>();
            var missing = new List<string>();
            foreach (var (name, ids) in Integrations)
            {
                var mod = ids.Select(People.ActiveMod).FirstOrDefault(m => m != null);
                if (mod == null)
                {
                    missing.Add(name);
                    continue;
                }
                found.Add(name + Live(name) + " [" + (mod.PackageIdPlayerFacing ?? mod.PackageId) + "]");
            }
            part.Add(found.Count > 0 ? "Found: " + string.Join("; ", found) + "." : "None of them is active.", found.Count > 0 ? UiKit.Good : Color.white);
            if (missing.Count > 0) part.Add("Not active: " + string.Join(", ", missing) + ".", UiKit.Faint);

            // Everything in Rimfolk's defs written for a particular mod (a "mods" list on the def or one of its
            // extensions): Almanac pages, events, world events, moments, people.
            var counts = new Dictionary<string, int>();
            var fields = new Dictionary<Type, FieldInfo>();
            void Count(object o)
            {
                if (o == null) return;
                var t = o.GetType();
                if (!fields.TryGetValue(t, out var fi))
                {
                    fi = t.GetField("mods", BindingFlags.Public | BindingFlags.Instance);
                    if (fi != null && !typeof(IEnumerable<string>).IsAssignableFrom(fi.FieldType)) fi = null;
                    fields[t] = fi;
                }
                if (!(fi?.GetValue(o) is IEnumerable<string> ids)) return;
                foreach (var id in ids.Where(People.ModActive).Select(id => People.ActiveMod(id)?.Name ?? id).Distinct())
                    counts[id] = counts.TryGetValue(id, out int n) ? n + 1 : 1;
            }
            foreach (var def in ColonyCouncilMod.Pack?.AllDefs ?? Enumerable.Empty<Def>())
            {
                Count(def);
                if (def.modExtensions != null)
                    foreach (var ext in def.modExtensions)
                        Count(ext);
            }
            part.Add(counts.Count == 0 ? "Content for your other mods: none of the mods Rimfolk writes for is active."
                : "Content for your other mods (" + counts.Count + " mods): " + string.Join(", ", counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key)
                    .Select(kv => kv.Key + " " + kv.Value)) + ".", UiKit.Faint);
        }

        /// <summary>For integrations Rimfolk reads by reflection, whether the reading worked.</summary>
        private static string Live(string name)
        {
            switch (name)
            {
                case "Vanilla Skills Expanded": return Expertise.Available ? " (expertise read)" : " (expertise NOT readable)";
                case "Vanilla Social Interactions Expanded": return SocialInteractions.Active ? " (its events read)" : " (its events NOT found)";
                case "PokeWorld": return PokeWorldLink.Active ? " (linked)" : " (NOT linked)";
                default: return "";
            }
        }

        private static void LogLines(Part part)
        {
            List<LogMessage> mine;
            try
            {
                mine = Log.Messages.Where(m => m?.text != null && m.text.Contains("[Rimfolk]")).ToList();
            }
            catch (InvalidOperationException)
            {
                part.Add("The log was busy; check again.", UiKit.Faint);
                return;
            }
            int errors = mine.Count(m => m.type == LogMessageType.Error);
            int warnings = mine.Count(m => m.type == LogMessageType.Warning);
            part.Add(errors + " errors, " + warnings + " warnings, " + (mine.Count - errors - warnings) + " other lines.",
                errors > 0 ? UiKit.Bad : warnings > 0 ? UiKit.Warn : UiKit.Good);
            if (errors > 0) part.color = UiKit.Bad;
            else if (warnings > 0) part.color = UiKit.Warn;
            foreach (var m in mine.Where(m => m.type != LogMessageType.Message).Reverse().Take(8))
            {
                string first = m.text.Split('\n')[0].Trim();
                if (first.Length > 220) first = first.Substring(0, 220) + "...";
                part.Add((m.repeats > 1 ? m.repeats + "x " : "") + first, m.type == LogMessageType.Error ? UiKit.Bad : UiKit.Warn);
            }
        }

        /// <summary>The report as plain text, for the clipboard and the log.</summary>
        public static string Plain(List<Part> parts)
        {
            var sb = new StringBuilder("[Rimfolk] check myself\n");
            foreach (var p in parts)
            {
                sb.Append("\n== ").Append(p.heading).Append('\n');
                foreach (var (text, _) in p.lines) sb.Append("- ").Append(text).Append('\n');
            }
            return sb.ToString();
        }
    }

    /// <summary>The "Rimfolk: check myself" window: the report, with buttons to copy it, put it in the log, or check again.</summary>
    public class Dialog_SelfCheck : Window
    {
        private List<SelfCheck.Part> parts;
        private string plain;
        private Vector2 scroll;
        private float height = 600f;

        public Dialog_SelfCheck()
        {
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            forcePause = false;
            Build();
        }

        public override Vector2 InitialSize => new Vector2(Mathf.Min(780f, UI.screenWidth - 40f), Mathf.Min(860f, UI.screenHeight - 40f));

        private void Build()
        {
            parts = SelfCheck.Report();
            plain = SelfCheck.Plain(parts);
        }

        public override void DoWindowContents(Rect inRect)
        {
            int bad = parts.Count(p => p.color == UiKit.Bad), warn = parts.Count(p => p.color == UiKit.Warn);
            string stat = bad > 0 ? bad + " need a look" : warn > 0 ? warn + " to note" : "all well";
            float top = UiKit.PageHeader(new Rect(inRect.x, inRect.y, inRect.width - 24f, UiKit.HeaderHeight), Icons.Council, "Rimfolk: check myself",
                "What Rimfolk found when it looked at itself. Take a screenshot, or copy it, for a bug report.", stat,
                bad > 0 ? UiKit.Bad : warn > 0 ? UiKit.Warn : UiKit.Good);

            const float buttonsH = 34f;
            var outRect = new Rect(inRect.x, inRect.y + top, inRect.width, inRect.height - top - buttonsH - 6f);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            foreach (var p in parts)
            {
                y += UiKit.SectionTitle(0f, y, view.width, p.heading, null, p.color);
                foreach (var (text, color) in p.lines) y += UiKit.Paragraph(4f, y, view.width - 4f, text, color) + 3f;
                y += 8f;
            }
            height = Mathf.Max(y, 100f);
            Widgets.EndScrollView();

            float bw = 170f;
            var row = new Rect(inRect.x, inRect.yMax - buttonsH + 4f, bw, 30f);
            if (Widgets.ButtonText(row, "Copy to clipboard"))
            {
                GUIUtility.systemCopyBuffer = plain;
                Messages.Message("Copied Rimfolk's check to the clipboard.", MessageTypeDefOf.SilentInput, false);
            }
            row.x += bw + 8f;
            if (Widgets.ButtonText(row, "Write it to the log"))
            {
                Log.Message(plain);
                Messages.Message("Rimfolk's check is in the log.", MessageTypeDefOf.SilentInput, false);
            }
            row.x += bw + 8f;
            if (Widgets.ButtonText(row, "Check again")) Build();
        }
    }
}
