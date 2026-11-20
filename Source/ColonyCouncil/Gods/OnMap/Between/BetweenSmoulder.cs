using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The forge flares at a fine piece (OwnHappening.SmoulderPraise): one of the colony finishes a masterwork or a legend,
    /// and the bench throws up a burst of heat and sparks. The maker is proud of it; the colony's next-best hand at that work,
    /// who walks over to look, resents it. Smoulder loves praise, in metal and stone above all; who says so is the believers'.
    /// </summary>
    public class SmoulderPraise : BetweenKind
    {
        public override OwnHappening H => OwnHappening.SmoulderPraise;

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            // It follows a fine piece made (Patch_BetweenFinePiece), never the hour.
        }

        /// <summary>The skill a piece is made with: art by Artistic, the rest by Crafting.</summary>
        private static SkillDef SkillFor(Thing t) => t?.TryGetComp<CompArt>() != null ? SkillDefOf.Artistic : SkillDefOf.Crafting;

        private static int Skill(Pawn p, SkillDef s) => p?.skills?.GetSkill(s)?.Level ?? 0;

        /// <summary>A fine piece made by one of the colony (QualityUtility.SendCraftNotification).</summary>
        internal static void Made(Pawn worker, Thing thing)
        {
            var b = OwnHappenings.Book;
            var g = OwnHappenings.God(OwnSmoulder.Key);
            var map = worker?.Map;
            if (b == null || g == null || map == null || thing == null || !Homes.IsHome(map) || !OwnHappenings.Quiet(map) || !OwnBetween.Able(worker)) return;
            if (!thing.TryGetQuality(out var q)) return;
            float chance = OwnHappeningsMath.PraiseChance((int)q);
            if (chance <= 0f || b.between.scenes.Any(s => s.map == map && s.stage >= 0) || !OwnBetween.Follows(b, OwnHappening.SmoulderPraise, g, map, chance)) return;
            Begin(b, g, map, worker, thing);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Pawn maker, Thing thing)
        {
            var bench = maker.CurJob?.GetTarget(TargetIndex.A).Thing as Building;
            var at = bench != null && bench.Spawned ? bench.Position : maker.Position;
            var skill = SkillFor(thing);
            int mine = Skill(maker, skill);
            var rival = OwnBetween.People(map).Where(p => p != maker && OwnBetween.Grown(p) && Skill(p, skill) >= mine - OwnHappeningsMath.RivalCrafterWithin && Skill(p, skill) > 0)
                .OrderByDescending(p => Skill(p, skill)).FirstOrDefault();
            var s = OwnBetween.Begin(b, OwnHappening.SmoulderPraise, g, map, at, maker, rival, 3f);
            s.thing = thing;
            s.word = thing.LabelShort;
            var loc = at.ToVector3Shifted();
            for (int i = 0; i < 5; i++) FleckMaker.ThrowMicroSparks(loc + Rand.InsideUnitCircleVec3 * 0.6f, map);
            FleckMaker.ThrowHeatGlow(at, map, 2.2f);
            FleckMaker.ThrowFireGlow(loc, map, 1.6f);
            OwnBetween.Show(g, map, at);
            OwnBetween.Card(OwnHappening.SmoulderPraise, g, map, at, false, null, maker.Named("A"), s.word.Named("THING"));
            OwnBetween.Say(maker, OwnHappening.SmoulderPraise, "proud", g, null, s.word.Named("THING"));
            OwnBetween.Proud(OwnHappening.SmoulderPraise, maker, s.word.Named("THING"));
            OwnBetween.Book(OwnHappening.SmoulderPraise, g, map, at, GodActKind.Act, maker, s.word.Named("THING"));
            if (rival == null || !OwnBetween.Send(rival, map, at, 2f))
            {
                s.stage = -1;
                return true;
            }
            s.stage = 1;
            return true;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            if (!OwnBetween.Still(s.b, map) || !OwnBetween.Still(s.a, map))
            {
                s.stage = -1;
                return;
            }
            if (OwnBetween.There(s.b, s.at, 4f) && Together.Can(s.a, s.b))
            {
                OwnHappenings.Stand(s.b, s.at, 300);
                OwnBetween.Say(s.b, H, "envy", g, s.a, (s.word ?? "").Named("THING"));
                OwnBetween.End(H, BetweenOutcome.Jealous, s.b, s.a, null, null, (s.word ?? "").Named("THING"));
                s.stage = -1;
                return;
            }
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var maker = OwnBetween.People(map).Where(OwnBetween.Grown).OrderByDescending(p => Skill(p, SkillDefOf.Crafting)).FirstOrDefault();
            var thing = map.listerThings.AllThings.FirstOrDefault(t => t.Spawned && t.TryGetQuality(out _) && t.def.category == ThingCategory.Item);
            if (maker == null || thing == null) return "nobody, or nothing made with a quality on the map.";
            Begin(b, g, map, maker, thing);
            return "the forge flares for " + maker.LabelShort + " (" + thing.LabelShort + ").";
        }
    }

    /// <summary>A fine piece made: Smoulder's forge may flare for it (SmoulderPraise.Made). Off with the deeper game hooks.</summary>
    [HarmonyPatch(typeof(QualityUtility), nameof(QualityUtility.SendCraftNotification))]
    public static class Patch_BetweenFinePiece
    {
        public static void Postfix(Thing thing, Pawn worker)
        {
            if (!RimfolkPatches.On || !Gods.On || thing == null || worker == null || worker.Faction != Faction.OfPlayer) return;
            try
            {
                SmoulderPraise.Made(worker, thing);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods between them: a fine piece", e);
            }
        }
    }

    /// <summary>
    /// Pulled back from the fall (OwnHappening.SmoulderNearMiss): two of the colony at work under the mountain, close together;
    /// the rock overhead grumbles, the one beside pulls the other back by the arm, and a scatter of stones comes down where
    /// they stood. Nobody is hurt. The one pulled back won't forget it. The mountain turning in its sleep, the miners say.
    /// </summary>
    public class SmoulderNearMiss : BetweenKind
    {
        public override OwnHappening H => OwnHappening.SmoulderNearMiss;

        private static bool UnderMountain(Pawn p) => p?.Map != null && p.Map.roofGrid.RoofAt(p.Position)?.isThickRoof == true;

        /// <summary>Two of the colony at work under the mountain, close together, or none.</summary>
        public static (Pawn a, Pawn b) Pair(Map map)
        {
            var under = OwnBetween.People(map).Where(p => OwnBetween.Grown(p) && UnderMountain(p) && !p.InBed()).ToList();
            foreach (var x in under.InRandomOrder())
            {
                var y = under.FirstOrDefault(o => o != x && o.Position.InHorDistOf(x.Position, OwnHappeningsMath.NearMissWithin));
                if (y != null) return (x, y);
            }
            return (null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < 7 || hour > 19) return;
            var (x, y) = Pair(map);
            var t = OwnHappeningsMath.NearMissTrigger(x != null, GodNear.Of(g, map));
            if (t == GodPaceMath.Trigger.None || !OwnHappenings.Calls(b, H, t, g, map, now)) return;
            Begin(b, g, map, x, y);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Pawn x, Pawn y)
        {
            var at = x.Position;
            var s = OwnBetween.Begin(b, OwnHappening.SmoulderNearMiss, g, map, at, x, y, 1f);
            for (int i = 0; i < 4; i++) FleckMaker.ThrowDustPuffThick(at.ToVector3Shifted() + Rand.InsideUnitCircleVec3, map, Rand.Range(0.8f, 1.3f), new Color(0.5f, 0.45f, 0.4f, 0.7f));
            OwnHappenings.Sound(map, at, 0.7f, "RoofCollapse", "Mine");
            if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(0.1f);
            OwnBetween.Say(y, OwnHappening.SmoulderNearMiss, "pull", g, x);
            // Pulled back two steps toward the one who caught them.
            var back = GenAdj.CellsAdjacent8Way(new TargetInfo(at, map)).Where(c => c.InBounds(map) && c.Standable(map) && x.CanReach(c, PathEndMode.OnCell, Danger.Deadly))
                .OrderBy(c => c.DistanceToSquared(y.Position)).FirstOrDefault();
            if (back.IsValid)
            {
                var go = JobMaker.MakeJob(JobDefOf.Goto, back);
                go.locomotionUrgency = LocomotionUrgency.Sprint;
                OwnHappenings.Job(x, go);
            }
            s.stage = 1;
            OwnBetween.Wait(s, 90);
            return true;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            // The stones come down where they stood, or beside it, never on anyone.
            var cell = new[] { s.at }.Concat(GenAdj.CellsAdjacent8Way(new TargetInfo(s.at, map))).FirstOrDefault(c => c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map) == null);
            if (!cell.IsValid)
            {
                s.stage = -1;
                return;
            }
            var rubble = DefDatabase<ThingDef>.GetNamedSilentFail("Filth_RubbleRock");
            if (rubble != null)
            {
                FilthMaker.TryMakeFilth(cell, map, rubble, 3);
                foreach (var c in GenAdj.CellsAdjacentCardinal(cell, Rot4.North, IntVec2.One).Where(c => c.InBounds(map) && c.Standable(map)).Take(2))
                    FilthMaker.TryMakeFilth(c, map, rubble, 1);
            }
            for (int i = 0; i < 6; i++) FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(), map, Rand.Range(0.9f, 1.5f), new Color(0.45f, 0.42f, 0.38f, 0.8f));
            if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(0.25f);
            OwnBetween.Card(H, g, map, cell, true, null, s.a.Named("A"), s.b.Named("B"));
            if (OwnBetween.Still(s.a, map) && OwnBetween.Still(s.b, map) && Together.Can(s.a, s.b))
            {
                OwnBetween.Say(s.a, H, OwnBetween.Doubts(s.a) ? "doubt" : "thanks", g, s.b);
                OwnBetween.End(H, BetweenOutcome.Kind, s.a, s.b);
            }
            OwnBetween.Book(H, g, map, cell, GodActKind.Omen, s.b, s.a.LabelShort.Named("OTHER"));
            s.stage = -1;
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var (x, y) = Pair(map);
            if (x == null)
            {
                var two = OwnBetween.People(map).Where(OwnBetween.Grown).Take(2).ToList();
                if (two.Count < 2) return "two of the colony are needed.";
                x = two[0];
                y = two[1];
            }
            Begin(b, g, map, x, y);
            return y.LabelShort + " pulls " + x.LabelShort + " back, and stones come down where they stood.";
        }
    }
}
