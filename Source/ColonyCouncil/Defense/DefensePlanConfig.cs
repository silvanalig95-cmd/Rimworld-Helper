using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The player's choices while planning a defense: turn, mirror, closing the gap, shooters and depth added to a drawn
    /// layout, and which building and material each role uses. Kept for the session per template; materials are remembered in the mod settings for all defense
    /// plans together (a colony that builds its walls in granite wants granite for the next gate too) and used again
    /// while there's still enough of them.
    /// </summary>
    public class DefensePlanConfig
    {
        public int rotation;
        public bool mirror;
        /// <summary>Carry the wall line on to the rock (or a wall) on both sides.</summary>
        public bool closeGap = true;
        public readonly Dictionary<string, ThingDef> variant = new Dictionary<string, ThingDef>();
        public readonly Dictionary<string, ThingDef> stuff = new Dictionary<string, ThingDef>();
        public readonly HashSet<string> pickedNow = new HashSet<string>();
        /// <summary>Roles planned without (a trap corridor with its sides left open, so raiders aren't turned away).</summary>
        public readonly HashSet<string> leaveOut = new HashSet<string>();
        /// <summary>A drawn layout's numbers, fixed (a placed plan read again with what it was drawn for); null: the colony's.</summary>
        public DefenseShapeSpec spec;
        /// <summary>The fixed layout from the XML: a plan placed before layouts were drawn.</summary>
        public bool fixedLayout;
        /// <summary>Firing positions and rows of depth the player added to a drawn layout (or took away).</summary>
        public int moreShooters, moreDepth;
        public int version;

        public const string Scope = "defense";

        public void Changed() => version++;

        private static readonly Dictionary<DefenseTemplateDef, DefensePlanConfig> configs = new Dictionary<DefenseTemplateDef, DefensePlanConfig>();

        public static DefensePlanConfig For(DefenseTemplateDef t)
        {
            if (t == null) return new DefensePlanConfig();
            if (!configs.TryGetValue(t, out var c))
            {
                c = new DefensePlanConfig();
                c.LoadRemembered();
                configs[t] = c;
            }
            return c;
        }

        public static void ClearAll() => configs.Clear();

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public void StartSession()
        {
            pickedNow.Clear();
            leaveOut.Clear();
            LoadRemembered();
            Changed();
        }

        /// <summary>
        /// For the first proposal of a plan only: forget the remembered material picks for this session, so each part is
        /// chosen again from what's in stock (nothing in the settings is changed).
        /// </summary>
        public void FromStockForNow()
        {
            stuff.Clear();
            pickedNow.Clear();
            Changed();
        }

        public void LeaveOut(string role, bool leave)
        {
            if (leave ? leaveOut.Add(role) : leaveOut.Remove(role)) Changed();
        }

        public void PickStuff(string role, ThingDef s)
        {
            stuff[role] = s;
            pickedNow.Add(role);
            Remember(role, s);
            Changed();
        }

        public void PickVariant(string role, ThingDef d)
        {
            variant[role] = d;
            Changed();
        }

        /// <summary>More (or fewer) firing positions and rows of depth than the colony's numbers give a drawn layout.</summary>
        public void Resize(int shooters, int depth)
        {
            moreShooters += shooters;
            moreDepth += depth;
            Changed();
        }

        /// <summary>Back to what the colony's numbers give.</summary>
        public void ColonySize()
        {
            moreShooters = moreDepth = 0;
            Changed();
        }

        /// <summary>The choices a placed plan was made with: its turn, mirror, gap, what it left out, and what it was drawn for.</summary>
        public static DefensePlanConfig Of(DefenseLinePlan saved)
        {
            var cfg = new DefensePlanConfig { rotation = saved.rotation, mirror = saved.mirror, closeGap = saved.closeGap };
            foreach (var role in saved.LeftOut) cfg.leaveOut.Add(role);
            cfg.spec = saved.Spec;
            cfg.fixedLayout = cfg.spec == null;
            return cfg;
        }

        /// <summary>Forget the material picks: each is chosen from the stock again.</summary>
        public void UseWhatWeHave()
        {
            stuff.Clear();
            pickedNow.Clear();
            var s = S;
            if (s?.planMaterials != null && s.planMaterials.RemoveAll(e => e != null && e.StartsWith(Scope + "::")) > 0) CouncilSettings.SaveNow();
            Changed();
        }

        private void Remember(string role, ThingDef s)
        {
            var settings = S;
            if (settings == null || s == null) return;
            settings.planMaterials ??= new List<string>();
            string prefix = Scope + "::" + role + "=>";
            settings.planMaterials.RemoveAll(e => e != null && e.StartsWith(prefix));
            settings.planMaterials.Add(prefix + s.defName);
            if (settings.planMaterials.Count > 400) settings.planMaterials.RemoveRange(0, settings.planMaterials.Count - 400);
            CouncilSettings.SaveNow();
        }

        private void LoadRemembered()
        {
            var settings = S;
            if (settings?.planMaterials == null) return;
            string prefix = Scope + "::";
            foreach (var e in settings.planMaterials)
            {
                if (e == null || !e.StartsWith(prefix)) continue;
                int arrow = e.LastIndexOf("=>");
                if (arrow <= prefix.Length) continue;
                string role = e.Substring(prefix.Length, arrow - prefix.Length);
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(e.Substring(arrow + 2));
                if (def != null) stuff[role] = def;
            }
        }

        /// <summary>A copy for trying spots and turns without touching the player's choices.</summary>
        public DefensePlanConfig Copy()
        {
            var c = new DefensePlanConfig
            {
                rotation = rotation, mirror = mirror, closeGap = closeGap, version = version, spec = spec?.Copy(), fixedLayout = fixedLayout,
                moreShooters = moreShooters, moreDepth = moreDepth
            };
            foreach (var kv in variant) c.variant[kv.Key] = kv.Value;
            foreach (var kv in stuff) c.stuff[kv.Key] = kv.Value;
            foreach (var g in pickedNow) c.pickedNow.Add(g);
            foreach (var r in leaveOut) c.leaveOut.Add(r);
            return c;
        }
    }
}
