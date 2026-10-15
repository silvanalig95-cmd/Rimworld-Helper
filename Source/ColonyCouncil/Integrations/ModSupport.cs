using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One mod (or DLC) Rimfolk works with, and what it does with it: the registry behind the self-check's "Mods Rimfolk
    /// works with" and the Almanac's compatibility table (Defs/IntegrationDefs/Council_Mods_Support.xml). The label is
    /// the mod's name.
    /// </summary>
    public class ModSupportDef : Def
    {
        /// <summary>Its package ids (any of them counts; Steam and local copies too).</summary>
        public List<string> packageIds = new List<string>();
        /// <summary>What Rimfolk does with it, in a sentence or two.</summary>
        [MustTranslate] public string what;
        /// <summary>Rimfolk reads it in code (the self-check lists these); otherwise it only writes content for it.</summary>
        public bool code;
        /// <summary>A reading the self-check can test: expertise, vsie, pokeworld, hospitality, magic.</summary>
        public string live;
        public int order = 100;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (packageIds.NullOrEmpty()) yield return "mod support entry has no package ids";
            else if (packageIds.Any(m => m.NullOrEmpty() || m != m.Trim().ToLowerInvariant())) yield return "package ids must be lowercase";
            if (what.NullOrEmpty()) yield return "mod support entry doesn't say what Rimfolk does";
        }
    }

    /// <summary>
    /// What Rimfolk finds of the mods it works with: which are active, whether its readings of them work, and how much
    /// of its own content is written for each. Fills the Almanac's compatibility page (Council_Alm_Mods_Compatibility)
    /// whenever a game starts or loads.
    /// </summary>
    public static class ModSupport
    {
        public const string PageDef = "Council_Alm_Mods_Compatibility";

        public static IEnumerable<ModSupportDef> All => DefDatabase<ModSupportDef>.AllDefsListForReading.OrderBy(d => d.order).ThenBy(d => d.label);

        /// <summary>The active mod for this entry, or null. A DLC counts by its package id too.</summary>
        public static ModMetaData ActiveMod(ModSupportDef d) =>
            d?.packageIds?.Select(People.ActiveMod).FirstOrDefault(m => m != null);

        public static bool Active(ModSupportDef d) => d?.packageIds != null && d.packageIds.Any(id => ModsConfig.IsActive(id) || People.ModActive(id));

        /// <summary>Whether a reading Rimfolk does by reflection worked: " (expertise read)", or "" when there's nothing to test.</summary>
        public static string LiveNote(string live)
        {
            try
            {
                switch (live)
                {
                    case "expertise": return Expertise.Available ? " (expertise read)" : " (expertise NOT readable)";
                    case "vsie": return SocialInteractions.Active ? " (its events read)" : " (its events NOT found)";
                    case "pokeworld": return PokeWorldLink.Active ? " (linked)" : " (NOT linked)";
                    case "hospitality": return Guests.CanAdopt ? " (guests read)" : " (its guest code NOT found)";
                    default: return "";
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mod support " + live, e);
                return "";
            }
        }

        /// <summary>Whether a reading worked, for the table (null when there's nothing to test).</summary>
        private static bool? LiveOk(string live)
        {
            switch (live)
            {
                case "expertise": return Expertise.Available;
                case "vsie": return SocialInteractions.Active;
                case "pokeworld": return PokeWorldLink.Active;
                case "hospitality": return Guests.CanAdopt;
                default: return null;
            }
        }

        private static Dictionary<string, int> counts;

        /// <summary>
        /// How many of Rimfolk's defs are written for this package id: every def (or def extension) with a "mods" list
        /// naming it. Counted once; the defs don't change while the game runs.
        /// </summary>
        public static int ContentFor(string packageId)
        {
            if (packageId.NullOrEmpty()) return 0;
            if (counts == null)
            {
                counts = new Dictionary<string, int>();
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
                    foreach (var id in ids.Where(i => !i.NullOrEmpty()).Select(i => i.Trim().ToLowerInvariant()).Distinct())
                        counts[id] = counts.TryGetValue(id, out int n) ? n + 1 : 1;
                }
                foreach (var def in ColonyCouncilMod.Pack?.AllDefs ?? Enumerable.Empty<Def>())
                {
                    Count(def);
                    if (def.modExtensions != null)
                        foreach (var ext in def.modExtensions)
                            Count(ext);
                }
            }
            return counts.TryGetValue(packageId.Trim().ToLowerInvariant(), out int c) ? c : 0;
        }

        public static int ContentFor(ModSupportDef d) => d?.packageIds?.Sum(ContentFor) ?? 0;

        /// <summary>The compatibility table on the Almanac page: what's here and what it does, then what isn't.</summary>
        public static void FillPage()
        {
            var page = DefDatabase<AlmanacArticleDef>.GetNamedSilentFail(PageDef);
            if (page == null) return;
            try
            {
                var here = new List<string> { "CC_PM_Compat_HeaderHere".Translate().Resolve() };
                var away = new List<string> { "CC_PM_Compat_HeaderAway".Translate().Resolve() };
                foreach (var d in All)
                {
                    string name = (ActiveMod(d)?.Name ?? d.LabelCap.ToString()).Replace("|", "/");
                    string what = (d.what ?? "").Replace("|", "/");
                    if (!Active(d))
                    {
                        away.Add(name + " | " + what);
                        continue;
                    }
                    int n = ContentFor(d);
                    var ok = LiveOk(d.live);
                    string state = ok == false ? "CC_PM_Compat_NotRead".Translate().Resolve()
                        : n > 0 ? "CC_PM_Compat_YesCount".Translate(n).Resolve() : "CC_PM_Compat_Yes".Translate().Resolve();
                    here.Add(name + " | " + state + " | " + what);
                }
                var sections = new List<AlmanacSection>();
                if (here.Count > 1)
                    sections.Add(new AlmanacSection { heading = "CC_PM_Compat_Here".Translate(here.Count - 1).Resolve(), table = here });
                else
                    sections.Add(new AlmanacSection { heading = "CC_PM_Compat_Here".Translate(0).Resolve(), text = "CC_PM_Compat_NoneHere".Translate().Resolve() });
                if (away.Count > 1)
                    sections.Add(new AlmanacSection { heading = "CC_PM_Compat_Away".Translate(away.Count - 1).Resolve(), table = away });
                page.sections = sections;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("compatibility page", e);
            }
        }
    }

    /// <summary>Fills the Almanac's compatibility table when a game starts or loads (everything it reads is in place by then).</summary>
    public class ModSupportComponent : GameComponent
    {
        public ModSupportComponent(Game game)
        {
        }

        public override void FinalizeInit() => ModSupport.FillPage();
    }
}
