using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What visitors say (through Speech, from the pool Rimfolk_VisitorLines, by what they are and their people's ways),
    /// and the few messages about them: a group coming along the road, and one that couldn't stay out in the weather.
    /// Someone at the gate with a letter needs no message: the letter points at them.
    /// </summary>
    public static class VisitorWords
    {
        private const string Pool = "Rimfolk_VisitorLines";

        private static FlavorPoolDef pool;
        private static bool looked;

        private static FlavorPoolDef Lines
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(Pool);
                }
                return pool;
            }
        }

        /// <summary>A line from one of them at a moment of the visit: arrive, wait, stay, pass, leave.</summary>
        public static void Say(Pawn p, VisitorTrip t, string moment)
        {
            if (p == null || t == null || !p.Spawned || p.Dead) return;
            try
            {
                var lines = Lines?.lines;
                if (lines == null) return;
                var tags = new HashSet<string> { moment };
                if (!t.what.NullOrEmpty()) tags.Add(t.what);
                if (t.animals.Count > 0) tags.Add("animals");
                if (p.Faction?.def != null) tags.Add(p.Faction.def.techLevel <= TechLevel.Medieval ? "theirLowtech" : "theirHightech");
                foreach (var tag in Flavor.TagsFor(p)) tags.Add(tag);
                var tone = ToneUtility.ToneFor(p);
                string line = Flavor.PickFresh(lines.For(tone), tags, lines.For(tone));
                if (line == null && tone != Tone.Plain) line = Flavor.PickFresh(lines.plain, tags, lines.plain);
                if (!line.NullOrEmpty()) Speech.Say(p, line);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor words", e);
            }
        }

        private static LookTargets Look(VisitorTrip t)
        {
            var here = t.Party.Where(p => p.Spawned).ToList();
            return here.Count > 0 ? new LookTargets(here) : LookTargets.Invalid;
        }

        /// <summary>A group coming along the road (mourners, a band, a herd): said once, when they come into sight.</summary>
        public static void Coming(VisitorTrip t)
        {
            // Someone at the gate with a letter: the letter says it.
            if (t == null || t.kind == VisitKind.Wait) return;
            try
            {
                string key = "CC_Visit_Come_" + (t.what ?? "");
                if (!key.CanTranslate()) key = "CC_Visit_Come";
                Notify.Message(key.Translate(t.faction?.Name ?? "?").Resolve(), Look(t), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor coming words", e);
            }
        }

        /// <summary>They had to go before their business was done: too cold, too hot, too hungry to stay out.</summary>
        public static void LeftForWeather(VisitorTrip t)
        {
            if (t == null) return;
            try
            {
                Notify.Message("CC_Visit_Weather".Translate(t.faction?.Name ?? "?").Resolve(), Look(t), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor weather words", e);
            }
        }
    }
}
