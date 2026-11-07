using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// An alert while an animal's carcass lies out fresh on one of the colony's maps (the player's request, 6 November
    /// 2026): not in a stockpile or on a shelf, nobody on their way to it, an hour or more after the death
    /// (<see cref="FreshCarcassMath.LyingOut"/>). It lists each one (forbidden, or out past the home area, where it is) and
    /// a click goes to them, so it can be hauled in, butchered or buried before it spoils. The setting "Fresh carcasses
    /// lying out" (<see cref="CouncilSettings.freshCarcassAlert"/>, How you're told, Alerts) turns it off. Looked at every
    /// rare tick, nothing saved.
    /// </summary>
    public class Alert_FreshCarcass : Alert
    {
        private readonly List<Thing> culprits = new List<Thing>();
        private string explanation = "";
        private int lookedAt = -1;

        public Alert_FreshCarcass()
        {
            defaultLabel = "CC_FreshCarcass_Alert".Translate();
            defaultPriority = AlertPriority.Medium;
        }

        public override string GetLabel() =>
            culprits.Count > 1 ? "CC_FreshCarcass_AlertMany".Translate(culprits.Count).Resolve() : "CC_FreshCarcass_Alert".Translate().Resolve();

        public override TaggedString GetExplanation() => explanation;

        public override AlertReport GetReport()
        {
            if (!(ColonyCouncilMod.Settings?.freshCarcassAlert ?? true) || Find.Maps == null) return false;
            int now = Find.TickManager.TicksGame;
            if (now / Sched.RareTicks != lookedAt)
            {
                lookedAt = now / Sched.RareTicks;
                Look();
            }
            return culprits.Count == 0 ? AlertReport.Inactive : AlertReport.CulpritsAre(culprits);
        }

        private void Look()
        {
            culprits.Clear();
            var lines = new List<string>();
            try
            {
                foreach (var map in Find.Maps)
                {
                    if (map == null || !map.IsPlayerHome) continue;
                    // A kill one of the colony's animals is eating (Animals/PetHunts.cs) is seen to: the game's hunt reserves nothing.
                    var eaten = new HashSet<Thing>();
                    foreach (var a in map.mapPawns.SpawnedColonyAnimals)
                        if (a.CurJobDef == JobDefOf.PredatorHunt && a.CurJob.targetA.Thing is Corpse kill)
                            eaten.Add(kill);
                    foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
                    {
                        if (!(t is Corpse c) || !c.Spawned || c.Destroyed) continue;
                        var inner = c.InnerPawn;
                        var rot = c.GetComp<CompRottable>();
                        if (inner?.RaceProps == null || rot == null) continue;
                        bool claimed = map.reservationManager.IsReservedByAnyoneOf(c, Faction.OfPlayer) || eaten.Contains(c);
                        if (!FreshCarcassMath.LyingOut(inner.RaceProps.Animal, rot.Stage == RotStage.Fresh, c.Position.Fogged(map), c.IsInAnyStorage(), claimed, c.Age))
                            continue;
                        culprits.Add(c);
                        if (lines.Count < 12) lines.Add(Line(c, inner, map));
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fresh carcasses lying out", e);
                culprits.Clear();
                lines.Clear();
            }
            if (culprits.Count == 0)
            {
                explanation = "";
                return;
            }
            if (culprits.Count > lines.Count) lines.Add("CC_FreshCarcass_More".Translate(culprits.Count - lines.Count).Resolve());
            explanation = "CC_FreshCarcass_AlertWhat".Translate().Resolve() + "\n\n" + string.Join("\n", lines);
        }

        /// <summary>"- a muffalo (forbidden), past the home area, at Riverside": what it is and why it isn't being seen to.</summary>
        private static string Line(Corpse c, Pawn inner, Map map)
        {
            var notes = new List<string>();
            if (c.IsForbidden(Faction.OfPlayer)) notes.Add("CC_FreshCarcass_Forbidden".Translate().Resolve());
            if (!map.areaManager.Home[c.Position]) notes.Add("CC_FreshCarcass_Far".Translate().Resolve());
            string what = inner.Name != null && !inner.Name.Numerical ? inner.LabelShort + ", " + inner.KindLabel : Find.ActiveLanguageWorker.WithIndefiniteArticle(inner.KindLabel);
            string line = "  - " + what + (notes.Count > 0 ? " (" + string.Join(", ", notes) + ")" : "");
            return Find.Maps.Count(m => m.IsPlayerHome) > 1 ? line + " " + "CC_FreshCarcass_At".Translate(Homes.Name(map).Named("PLACE")).Resolve() : line;
        }
    }
}
