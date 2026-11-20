using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Whose rot is it (OwnHappening.MorelWasted): food left to go off at home (a carcass rotting, a stack nearly spoiled out
    /// of the cold). The cook and another walk over to it; if either holds the Soft Mother and neither scoffs, they carry it
    /// out to the soil together and come away closer ("give it back to her"); else they argue over whose fault it was.
    /// </summary>
    public class MorelWasted : BetweenKind
    {
        public override OwnHappening H => OwnHappening.MorelWasted;

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < 8 || hour > 19) return;
            var waste = OwnHappenings.Wasting(map);
            if (waste == null || !OwnHappenings.Calls(b, H, GodPaceMath.Trigger.Ordinary, g, map, now)) return;
            Begin(b, g, map, waste);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Thing waste)
        {
            var people = OwnBetween.People(map).Where(p => OwnBetween.FreeNow(p) && OwnBetween.Grown(p)).ToList();
            var cook = people.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Cooking)?.Level ?? 0).FirstOrDefault();
            var other = people.Where(p => p != cook).OrderBy(p => p.Position.DistanceToSquared(waste.Position)).FirstOrDefault();
            if (cook == null || other == null || !OwnBetween.Send(cook, map, waste.Position, 2f, 2) || !OwnBetween.Send(other, map, waste.Position, 2f, 2)) return false;
            var s = OwnBetween.Begin(b, OwnHappening.MorelWasted, g, map, waste.Position, cook, other, 4f);
            s.thing = waste;
            s.word = waste.LabelNoCount;
            OwnBetween.Card(OwnHappening.MorelWasted, g, map, waste.Position, false, null, s.word.Named("THING"));
            s.stage = 1;
            return true;
        }

        /// <summary>Open soil at the edge of the colony's fields (else near the middle), for what is given back.</summary>
        private static IntVec3 Soil(Map map, IntVec3 from)
        {
            bool Fits(IntVec3 c) => c.InBounds(map) && c.Standable(map) && !c.Roofed(map) && c.GetFirstBuilding(map) == null && c.GetFirstItem(map) == null
                                    && c.GetTerrain(map).fertility >= 0.7f && !c.GetTerrain(map).IsWater && c.GetZone(map) == null;
            var zone = map.zoneManager.AllZones.OfType<Zone_Growing>().OrderBy(z => z.Cells.First().DistanceToSquared(from)).FirstOrDefault();
            if (zone != null)
            {
                var c = zone.Cells.SelectMany(x => GenAdj.CellsAdjacentCardinal(x, Rot4.North, IntVec2.One)).Where(Fits).FirstOrDefault(Fits);
                if (c.IsValid && Fits(c)) return c;
            }
            return GenRadial.RadialCellsAround(from, 25f, true).FirstOrDefault(c => Fits(c) && !c.InHorDistOf(from, 4f));
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            if (!OwnBetween.Still(s.a, map) || !OwnBetween.Still(s.b, map))
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 2)
            {
                // Laid down at the soil: the god's look there, and done.
                if (s.a.CurJobDef != JobDefOf.HaulToCell || now > s.until)
                {
                    OwnBetween.Show(g, map, s.at);
                    s.stage = -1;
                    return;
                }
                OwnBetween.Wait(s, 250);
                return;
            }
            if (OwnBetween.There(s.a, s.at) && OwnBetween.There(s.b, s.at) && Together.Can(s.a, s.b))
            {
                var sa = OwnBetween.StanceOf(s.a, g);
                var sb = OwnBetween.StanceOf(s.b, g);
                bool give = (sa == BetweenStance.Holds || sb == BetweenStance.Holds) && sa != BetweenStance.Doubts && sb != BetweenStance.Doubts;
                var what = (s.word ?? "").Named("THING");
                OwnBetween.Face(s.a, s.b, 300);
                OwnBetween.Say(s.a, H, sa == BetweenStance.Holds ? "give" : "whose", g, s.b, what);
                string reply = OwnBetween.Say(s.b, H, give ? "agree" : sb == BetweenStance.Doubts ? "doubt" : "blame", g, s.a, what);
                if (give)
                {
                    var soil = Soil(map, s.at);
                    if (soil.IsValid && s.thing != null && s.thing.Spawned && OwnHappenings.Carry(s.a, s.thing, soil, s.thing.stackCount))
                    {
                        OwnBetween.Send(s.b, map, soil, 2f, 2);
                        s.at = soil;
                    }
                    OwnBetween.End(H, BetweenOutcome.Closer, s.a, s.b, null, null, what);
                    OwnBetween.Book(H, g, map, s.at, GodActKind.Act, s.a, s.b.LabelShort.Named("OTHER"), what);
                    s.stage = 2;
                    s.until = now + GenDate.TicksPerHour * 2;
                    OwnBetween.Wait(s, 250);
                    return;
                }
                OwnBetween.End(H, BetweenOutcome.Quarrel, s.a, s.b, OwnBetween.HeldBy(s.a), OwnBetween.HeldBy(s.b), what);
                OwnBetween.Book(H, g, map, s.at, GodActKind.Omen, s.a, s.b.LabelShort.Named("OTHER"), what);
                if (sb == BetweenStance.Doubts) OwnBetween.Doubted(s.b, g, reply);
                s.stage = -1;
                return;
            }
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var waste = OwnHappenings.Wasting(map) ?? map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).FirstOrDefault(t => t.Spawned && !(t is Corpse) && t.def.ingestible != null);
            if (waste == null) return "no food at all on the map.";
            return Begin(b, g, map, waste) ? "the cook and another walk to the " + waste.LabelNoCount + "." : "two free of the colony are needed.";
        }
    }

    /// <summary>
    /// White threads on the sealed stone (OwnHappening.MorelSealed): one of the colony dead lately lies sealed in a
    /// sarcophagus, and at dawn pale threads have grown over the stone and round it. One who holds the Soft Mother walks out
    /// and says the dead are kept from her; one who holds Tamiu or Hush (who keep the dead whole), or a doubter, answers, and
    /// they argue. Nothing comes of the threads but the argument.
    /// </summary>
    public class MorelSealed : BetweenKind
    {
        public override OwnHappening H => OwnHappening.MorelSealed;

        private static bool Sarcophagus(Building_Grave g) => g?.def?.defName != null && g.def.defName.IndexOf("arcophag", System.StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>A sarcophagus at this home holding one of the colony dead lately, not yet answered.</summary>
        public static Building_Grave Sealed(OwnHappeningsBook b, Map map)
        {
            foreach (var g in map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>())
            {
                if (!Sarcophagus(g) || !g.HasCorpse || b.between.sealedSeen.Contains(g.thingIDNumber)) continue;
                var c = g.Corpse;
                if (c?.InnerPawn?.RaceProps?.Humanlike != true || OwnBetween.Now - c.timeOfDeath > OwnHappeningsMath.SealedDays * GenDate.TicksPerDay) continue;
                return g;
            }
            return null;
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            var stone = Sealed(b, map);
            if (stone == null) return;
            b.between.sealedSeen.Add(stone.thingIDNumber);
            b.between.Trim();
            if (!OwnBetween.Follows(b, H, g, map, OwnHappeningsMath.SealedChance)) return;
            Begin(b, g, map, stone, false);
        }

        private static void Begin(OwnHappeningsBook b, God g, Map map, Building_Grave stone, bool now)
        {
            var s = OwnBetween.Begin(b, OwnHappening.MorelSealed, g, map, stone.Position, null, null, 30f);
            s.thing = stone;
            s.word = stone.Corpse?.InnerPawn?.LabelShort ?? "";
            s.stage = 0;
            s.next = OwnBetween.Now + (now ? 1 : Mathf.Max(GenDate.TicksPerHour, OwnBetween.TicksToHour(map, 5)));
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            if (s.stage == 0)
            {
                if (s.thing == null || !s.thing.Spawned)
                {
                    s.stage = -1;
                    return;
                }
                GodSky.Stay(map, SkySign.Threads, s.at, OwnHappeningsMath.SealedHours * GenDate.TicksPerHour, 2.5f);
                OwnBetween.Show(g, map, s.at);
                OwnBetween.Card(H, g, map, s.at, true, null, s.word.Named("DEAD"));
                var people = OwnBetween.People(map).Where(p => OwnBetween.FreeNow(p) && OwnBetween.Grown(p)).ToList();
                var morel = OwnBetween.Believer(people, g);
                var keeper = people.Where(p => p != morel && (OwnBetween.Holds(p, OwnHappenings.God("Mog")) || OwnBetween.Holds(p, OwnHappenings.God("Hush")))).FirstOrDefault()
                             ?? OwnBetween.Doubter(people.Where(p => p != morel));
                if (morel == null || keeper == null || !OwnBetween.Send(morel, map, s.at, 3f) || !OwnBetween.Send(keeper, map, s.at, 3f))
                {
                    OwnBetween.Book(H, g, map, s.at, GodActKind.Omen, null, s.word.Named("DEAD"));
                    s.stage = -1;
                    return;
                }
                s.a = morel;
                s.b = keeper;
                s.stage = 1;
                s.until = now + GenDate.TicksPerHour * 4;
                OwnBetween.Wait(s, 250);
                return;
            }
            if (!OwnBetween.Still(s.a, map) || !OwnBetween.Still(s.b, map))
            {
                s.stage = -1;
                return;
            }
            if (OwnBetween.There(s.a, s.at, 4f) && OwnBetween.There(s.b, s.at, 4f) && Together.Can(s.a, s.b))
            {
                var dead = (s.word ?? "").Named("DEAD");
                OwnBetween.Face(s.a, s.b, 400);
                OwnBetween.Say(s.a, H, "kept", g, s.b, dead);
                bool doubts = OwnBetween.Doubts(s.b);
                string reply = OwnBetween.Say(s.b, H, doubts ? "doubt" : "whole", g, s.a, dead);
                OwnBetween.End(H, BetweenOutcome.Quarrel, s.a, s.b, g, OwnBetween.HeldBy(s.b), dead);
                OwnBetween.Book(H, g, map, s.at, GodActKind.Omen, s.a, dead, s.b.LabelShort.Named("OTHER"));
                if (doubts) OwnBetween.Doubted(s.b, g, reply);
                s.stage = -1;
                return;
            }
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var stone = map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>().FirstOrDefault(x => Sarcophagus(x) && x.HasCorpse)
                        ?? map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>().FirstOrDefault(x => x.HasCorpse);
            if (stone == null) return "no sarcophagus (or grave) holding the dead here.";
            Begin(b, g, map, stone, true);
            return "threads on the stone of " + (stone.Corpse?.InnerPawn?.LabelShort ?? "the dead") + "; one who holds her and one who doesn't walk out to it.";
        }
    }
}
