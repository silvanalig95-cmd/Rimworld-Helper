using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Families across the years. The game gives people years in cryptosleep (a calendar age beyond the years they
    /// lived) and builds families by calendar years, so a grandmother can be younger than her grandson and a mother
    /// younger than her son. Rimfolk shows it and lets them talk about it: the years asleep beside a colonist's age on
    /// the People page, a part of their story ("Across the years") that explains each relative in their direct line
    /// whose age runs the wrong way round, and now and then, when two such relatives are together, a word between them
    /// (with speech showing and the visual and sound touches on). Nothing here is saved: a remark's time forgotten on
    /// a load only means one more chance.
    /// </summary>
    public static class FamilyYears
    {
        /// <summary>A relative in someone's direct line whose age runs the wrong way round.</summary>
        public struct Kin
        {
            public Pawn other;
            /// <summary>What they are to the one asked about (Parent: their mother or father).</summary>
            public PawnRelationDef relation;
            /// <summary>Generations above (positive) or below (negative), FamilyYearsMath.Generations.</summary>
            public int generations;
        }

        /// <summary>When each pair last spoke of it (by FamilyYearsMath.PairKey).</summary>
        private static readonly Dictionary<string, int> lastRemark = new Dictionary<string, int>();

        private static int Now => Find.TickManager.TicksGame;

        public static int Lived(Pawn p) => p?.ageTracker?.AgeBiologicalYears ?? 0;

        public static int Calendar(Pawn p) => p?.ageTracker?.AgeChronologicalYears ?? 0;

        public static int Slept(Pawn p) => FamilyYearsMath.Slept(Lived(p), Calendar(p));

        /// <summary>
        /// The closest relation in the direct line <paramref name="other"/> has to <paramref name="me"/> (their mother
        /// before anything further up), or null when they aren't in it.
        /// </summary>
        public static PawnRelationDef LineRelation(Pawn me, Pawn other, out int generations)
        {
            generations = 0;
            if (me == null || other == null || me.relations == null || other.relations == null) return null;
            PawnRelationDef best = null;
            foreach (var rel in me.GetRelations(other))
            {
                int g = FamilyYearsMath.Generations(rel.defName);
                if (g == 0 || (best != null && Math.Abs(g) >= Math.Abs(generations))) continue;
                best = rel;
                generations = g;
            }
            return best;
        }

        /// <summary>Living relatives in their direct line whose ages run the wrong way round, nearest generations first.</summary>
        public static List<Kin> UpsideDown(Pawn p)
        {
            var list = new List<Kin>();
            if (p?.relations == null || p.ageTracker == null || p.RaceProps == null || !p.RaceProps.Humanlike) return list;
            int mine = Lived(p);
            foreach (var o in p.relations.FamilyByBlood)
            {
                if (o == null || o == p || o.Dead || o.ageTracker == null) continue;
                var rel = LineRelation(p, o, out int g);
                if (rel == null || !FamilyYearsMath.UpsideDown(g, mine, Lived(o))) continue;
                list.Add(new Kin { other = o, relation = rel, generations = g });
            }
            return list.OrderBy(k => Math.Abs(k.generations)).ThenBy(k => k.other.LabelShort).ToList();
        }

        // ---------------- On the page ----------------

        /// <summary>"24 years in cryptosleep" for the People page's line under a name, or null for two years or less.</summary>
        public static string SleptNote(Pawn p)
        {
            if (p?.ageTracker == null || !FamilyYearsMath.HasSlept(Lived(p), Calendar(p))) return null;
            return "CC_Years_SleptNote".Translate(Slept(p)).Resolve();
        }

        /// <summary>
        /// Their part of the story: when they were born and how many of the years since they lived, and each relative
        /// in their direct line whose age runs the wrong way round, with whose sleep explains it.
        /// </summary>
        public static List<string> StoryLines(Pawn p)
        {
            var lines = new List<string>();
            if (p?.ageTracker == null) return lines;
            if (FamilyYearsMath.HasSlept(Lived(p), Calendar(p)))
                lines.Add("CC_Years_Own".Translate(p.Named("PAWN"), p.ageTracker.BirthYear.Named("YEAR"), Calendar(p).Named("CALENDAR"),
                    Lived(p).Named("LIVED"), Slept(p).Named("SLEPT")).Resolve());
            foreach (var k in UpsideDown(p).Take(5))
            {
                var o = k.other;
                string rel = k.relation.GetGenderSpecificLabel(o);
                // The one above in the line slept while the one below grew up: theirs explains it.
                var elder = k.generations > 0 ? o : p;
                lines.Add((k.generations > 0 ? "CC_Years_KinYounger" : "CC_Years_KinOlder").Translate(
                    p.Named("PAWN"), o.Named("OTHER"), rel.Named("RELATION"), Lived(o).Named("AGE"), Math.Abs(Lived(p) - Lived(o)).Named("DIFF"),
                    Slept(elder).Named("SLEPT"), Calendar(elder).Named("CALENDAR")).Resolve());
            }
            return lines;
        }

        // ---------------- A word between them ----------------

        /// <summary>
        /// The council's day: for each pair of colonists on one map whose ages run the wrong way round and whose time has
        /// come round, now and then a word when they're next together (Together.When), said by either of them.
        /// </summary>
        public static void Daily(CouncilGameComponent comp)
        {
            var s = ColonyCouncilMod.Settings;
            if (comp == null || s == null || !s.visualTouches || !Speech.On) return;
            int now = Now;
            foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned.ToList())
            {
                foreach (var k in UpsideDown(p))
                {
                    var o = k.other;
                    // Each pair is looked at once, from the side of the one below in the line.
                    if (k.generations < 0 || !o.Spawned || o.Map != p.Map || !o.IsColonist || o.Dead) continue;
                    string key = FamilyYearsMath.PairKey(p.thingIDNumber, o.thingIDNumber);
                    if (!FamilyYearsMath.RemarkDue(lastRemark.TryGetValue(key, out int last) ? last : -1, now, GenDate.TicksPerDay)) continue;
                    if (!Rand.Chance(FamilyYearsMath.RemarkChance)) continue;
                    lastRemark[key] = now;
                    var speaker = Rand.Bool ? p : o;
                    var listener = speaker == p ? o : p;
                    Together.When("familyyears|" + key, speaker, listener, () => Remark(speaker, listener), 24f);
                }
            }
            if (lastRemark.Count > 300) lastRemark.Clear();
        }

        private static void Remark(Pawn speaker, Pawn listener)
        {
            // together: Together.When in Daily runs this only when the two are together, and Speech.Say asks again.
            var rel = LineRelation(speaker, listener, out int g);
            if (rel == null) return;
            string line = Flavor.FromPool("Council_Pool_SayFamilyYears", speaker, null, null, FamilyYearsMath.Side(g), Math.Abs(g) >= 2 ? "grand" : "near");
            if (line.NullOrEmpty()) return;
            Speech.Say(speaker, line.Formatted(listener.Named("OTHER"), rel.GetGenderSpecificLabel(listener).Named("RELATION")).Resolve(), listener);
        }

        /// <summary>Forget the last game's remarks (called when a game loads).</summary>
        public static void Reset() => lastRemark.Clear();
    }
}
