using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What someone's own body says about Rimfolk's fixed lines, read from the game's own numbers so any mod's genes count
    /// (BodyMath has the lines): how far their genes, traits, health and race move their comfortable range; whether they
    /// barely eat (their real hunger rate); whether they can hear; whether darkness bothers their eyes; the armour that
    /// grows in their skin.
    /// </summary>
    public static class BodyFit
    {
        private static readonly Dictionary<int, (int tick, float cold, float heat)> shifts = new Dictionary<int, (int, float, float)>();
        private const int CacheTicks = 2500;

        public static void Reset() => shifts.Clear();

        /// <summary>
        /// How far their comfortable range lies from an ordinary person's, clothes aside: the comfort offsets of their active
        /// genes, their traits and their health, and their race's own range against a human's. <paramref name="cold"/> moves
        /// the cold lines (-10 for a fox tail, -273 for cold immunity), <paramref name="heat"/> the hot ones.
        /// </summary>
        public static void TempShift(Pawn p, out float cold, out float heat)
        {
            cold = 0f;
            heat = 0f;
            if (p == null) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (shifts.TryGetValue(p.thingIDNumber, out var c) && now >= c.tick && now - c.tick < CacheTicks)
            {
                cold = c.cold;
                heat = c.heat;
                return;
            }
            try
            {
                Read(p, StatDefOf.ComfyTemperatureMin, out cold);
                Read(p, StatDefOf.ComfyTemperatureMax, out heat);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("comfortable range", e);
                cold = 0f;
                heat = 0f;
            }
            if (shifts.Count > 600) shifts.Clear();
            shifts[p.thingIDNumber] = (now, cold, heat);
        }

        private static void Read(Pawn p, StatDef stat, out float shift)
        {
            shift = 0f;
            if (stat == null) return;
            if (p.def != ThingDefOf.Human && ThingDefOf.Human != null)
                shift += p.def.GetStatValueAbstract(stat) - ThingDefOf.Human.GetStatValueAbstract(stat);
            if (ModsConfig.BiotechActive && p.genes != null)
                foreach (var g in p.genes.GenesListForReading)
                    if (g?.def?.statOffsets != null && g.Active) shift += g.def.statOffsets.GetStatOffsetFromList(stat);
            if (p.story?.traits != null)
                foreach (var t in p.story.traits.allTraits)
                {
                    var offsets = t?.CurrentData?.statOffsets;
                    if (offsets != null && !t.Suppressed) shift += offsets.GetStatOffsetFromList(stat);
                }
            var hediffs = p.health?.hediffSet?.hediffs;
            if (hediffs != null)
                foreach (var h in hediffs)
                {
                    var offsets = h?.CurStage?.statOffsets;
                    if (offsets != null) shift += offsets.GetStatOffsetFromList(stat);
                }
        }

        /// <summary>
        /// Barely eats: they have a food need, but their hunger (fed, as the game reckons it with every gene and hediff
        /// that changes it) falls at under a tenth of an ordinary adult's. Foodless, fed by light, fed by minerals. A
        /// mineral absorber out of minerals grows hungry again, and counts as eating.
        /// </summary>
        public static bool ReadBarelyEats(Pawn p)
        {
            var food = p?.needs?.food;
            if (food == null) return false;
            try
            {
                return BodyMath.BarelyEats(food.FoodFallPerTickAssumingCategory(HungerCategory.Fed, true), Need_Food.BaseFoodFallPerTick);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Barely eats (the "fasting" tag, read at most once an hour: PeopleTags).</summary>
        public static bool BarelyEats(Pawn p) => p?.needs?.food != null && PeopleTags.Has(p, "fasting");

        /// <summary>Sits down to meals like anyone: has a food need and doesn't barely eat.</summary>
        public static bool Eats(Pawn p) => p?.needs?.food != null && !BarelyEats(p);

        /// <summary>Can hear: the game's Hearing capacity (a deaf gene, ears lost in a raid).</summary>
        public static bool Hears(Pawn p)
        {
            var caps = p?.health?.capacities;
            if (caps == null) return true;
            try
            {
                return caps.CapableOf(PawnCapacityDefOf.Hearing);
            }
            catch
            {
                return true;
            }
        }

        private static ThoughtDef dark;
        private static bool darkLooked;

        /// <summary>
        /// Darkness doesn't trouble their eyes: a gene that sees in the dark (Biotech's dark vision, a nereid's glowing
        /// eyes), or anything else that silences the game's own "in darkness" mood for them.
        /// </summary>
        public static bool SeesInDark(Pawn p)
        {
            if (p == null) return false;
            if (ModsConfig.BiotechActive && p.genes != null)
                foreach (var g in p.genes.GenesListForReading)
                    if (g?.def != null && g.Active && g.def.ignoreDarkness) return true;
            if (!darkLooked)
            {
                darkLooked = true;
                dark = DefDatabase<ThoughtDef>.GetNamedSilentFail("EnvironmentDark");
            }
            return dark != null && Nullified(p, dark);
        }

        /// <summary>The game's own answer to whether a mood is silenced for them (a trait, a gene, a precept, a hediff).</summary>
        public static bool Nullified(Pawn p, ThoughtDef def)
        {
            if (p == null || def == null) return false;
            try
            {
                return ThoughtUtility.ThoughtNullified(p, def);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>The armour grown in their skin (bark, ferrous or plasteel skin): their own sharp armour rating, clothes aside.</summary>
        public static float NaturalArmor(Pawn p)
        {
            if (p == null) return 0f;
            try
            {
                return p.GetStatValue(StatDefOf.ArmorRating_Sharp);
            }
            catch
            {
                return 0f;
            }
        }
    }
}
