using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// A coat for the cold one (OwnHappening.HushCoat): one of the colony is taking a chill (the game's hypothermia) while a
    /// warmer coat lies in the stores. One who holds the Long White (else the one who likes them best) carries it out to them,
    /// they put it on, and the frost about them melts away. They won't forget who brought it. The weak kept warm, as she asks.
    /// </summary>
    public class HushCoat : BetweenKind
    {
        public override OwnHappening H => OwnHappening.HushCoat;

        /// <summary>The warmth a coat would add over what they have on where it goes (its cold insulation over the warmest it would replace).</summary>
        private static float Gain(Pawn p, Apparel coat)
        {
            float had = 0f;
            foreach (var w in p.apparel.WornApparel)
                if (!ApparelUtility.CanWearTogether(w.def, coat.def, p.RaceProps.body))
                    had = Mathf.Max(had, w.GetStatValue(StatDefOf.Insulation_Cold));
            return coat.GetStatValue(StatDefOf.Insulation_Cold) - had;
        }

        /// <summary>One taking a chill, up and about, and the warmest coat in the stores that would help them, or none.</summary>
        public static (Pawn cold, Apparel coat) Chilled(Map map)
        {
            var hypo = HediffDefOf.Hypothermia;
            foreach (var p in OwnBetween.People(map).Where(x => x.apparel != null && !x.InBed()))
            {
                var h = p.health?.hediffSet?.GetFirstHediffOfDef(hypo);
                if (h == null || !h.Visible) continue;
                var coat = map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel).OfType<Apparel>()
                    .Where(a => a.Spawned && a.IsInAnyStorage() && !a.IsForbidden(Faction.OfPlayer) && ApparelUtility.HasPartsToWear(p, a.def) && EquipmentUtility.CanEquip(a, p)
                                && Gain(p, a) >= OwnHappeningsMath.CoatWarmerBy)
                    .OrderByDescending(a => Gain(p, a)).FirstOrDefault();
                if (coat != null) return (p, coat);
            }
            return (null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            if (map.mapTemperature.OutdoorTemp > 5f) return;
            var (cold, coat) = Chilled(map);
            if (cold == null || !OwnBetween.Follows(b, H, g, map, OwnHappeningsMath.CoatChance)) return;
            Begin(b, g, map, cold, coat);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Pawn cold, Apparel coat)
        {
            var people = OwnBetween.People(map).Where(p => p != cold && OwnBetween.FreeNow(p) && OwnBetween.Grown(p)).ToList();
            var bringer = OwnBetween.Believer(people, g) ?? people.Where(p => OwnBetween.Opinion(p, cold) >= 0f).OrderByDescending(p => OwnBetween.Opinion(p, cold)).FirstOrDefault();
            var beside = bringer == null ? IntVec3.Invalid : OwnHappenings.Beside(map, cold.Position, bringer);
            if (bringer == null || !beside.IsValid || !OwnHappenings.Carry(bringer, coat, beside, 1)) return false;
            var s = OwnBetween.Begin(b, OwnHappening.HushCoat, g, map, beside, cold, bringer, 3f);
            s.thing = coat;
            s.word = coat.LabelNoCount;
            s.stage = 1;
            return true;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var cold = s.a;
            var bringer = s.b;
            if (!OwnBetween.Still(cold, map) || !OwnBetween.Still(bringer, map) || s.thing == null || s.thing.Destroyed)
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 1)
            {
                // Set down by them: they put it on, and the two speak.
                if (bringer.CurJobDef == JobDefOf.HaulToCell || !s.thing.Spawned)
                {
                    if (now > s.until) s.stage = -1;
                    OwnBetween.Wait(s, 120);
                    return;
                }
                var wear = JobMaker.MakeJob(JobDefOf.Wear, s.thing);
                OwnHappenings.Job(cold, wear);
                if (Together.Can(bringer, cold))
                {
                    OwnBetween.Say(bringer, H, "give", g, cold, s.word.Named("COAT"));
                    OwnBetween.Say(cold, H, "thanks", g, bringer, s.word.Named("COAT"));
                }
                s.stage = 2;
                OwnBetween.Wait(s, 300);
                return;
            }
            // Worn: the frost about them melts away.
            if (cold.apparel?.WornApparel.Contains(s.thing as Apparel) == true || now > s.until)
            {
                bool worn = cold.apparel?.WornApparel.Contains(s.thing as Apparel) == true;
                if (worn)
                {
                    foreach (var c in GenRadial.RadialCellsAround(cold.Position, 2.9f, true).Where(c => c.InBounds(map) && !c.Roofed(map)))
                        map.snowGrid.SetDepth(c, 0f);
                    OwnHappenings.Glow(map, cold.Position, new Color(0.85f, 0.92f, 1f), 6, 1f, 0.4f);
                    OwnBetween.End(H, BetweenOutcome.Kind, cold, bringer, null, null, s.word.Named("COAT"));
                    OwnBetween.Card(H, g, map, cold.Position, false, null, cold.Named("A"), bringer.Named("B"), s.word.Named("COAT"));
                    OwnBetween.Book(H, g, map, cold.Position, GodActKind.Blessing, bringer, cold.LabelShort.Named("OTHER"), s.word.Named("COAT"));
                }
                s.stage = -1;
                return;
            }
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var (cold, coat) = Chilled(map);
            if (cold == null)
            {
                cold = OwnBetween.People(map).FirstOrDefault(p => p.apparel != null);
                coat = cold == null ? null : map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel).OfType<Apparel>()
                    .Where(a => a.Spawned && ApparelUtility.HasPartsToWear(cold, a.def) && EquipmentUtility.CanEquip(a, cold)).OrderByDescending(a => a.GetStatValue(StatDefOf.Insulation_Cold)).FirstOrDefault();
            }
            if (cold == null || coat == null) return "nobody cold, or no coat on the map they could wear.";
            return Begin(b, g, map, cold, coat) ? "a " + coat.LabelNoCount + " is carried to " + cold.LabelShort + "." : "nobody free to carry it.";
        }
    }

    /// <summary>
    /// Quiet asked on the cold night (OwnHappening.HushNoise): on a winter night (or a bitter one) some of the colony are loud
    /// at a party, a game or a long talk, and one who holds the Long White goes over and asks for quiet. One who holds her too
    /// says sorry and stops; one of no strong belief grumbles and stops; a doubter, or one who holds a god set against her (the
    /// Open Cup above all), won't, and the two argue. Noise on the longest night is what she hates most, the stories say.
    /// </summary>
    public class HushNoise : BetweenKind
    {
        public override OwnHappening H => OwnHappening.HushNoise;

        /// <summary>Someone being loud: at a party or a gathering, at a game, or in a long talk.</summary>
        public static bool Loud(Pawn p)
        {
            if (p?.CurJob == null) return false;
            if (p.GetLord()?.LordJob is LordJob_Joinable_Party) return true;
            var joy = p.CurJob.def.joyKind?.defName;
            if (joy == "Social" || joy == "Gaming_Dexterity") return true;
            var n = p.CurJob.def.defName;
            return n.Contains("Party") || n.Contains("PlayMusicalInstrument") || n == "SocialRelax";
        }

        public static (Pawn quiet, Pawn loud) Scene(Map map, God g)
        {
            var people = OwnBetween.People(map);
            var loud = people.Where(Loud).ToList();
            if (loud.Count == 0) return (null, null);
            var quiet = OwnBetween.Believer(people.Where(p => !Loud(p) && OwnBetween.FreeNow(p) && OwnBetween.Grown(p) && loud.Any(l => l.Position.InHorDistOf(p.Position, OwnHappeningsMath.NoiseWithin))), g);
            if (quiet == null) return (null, null);
            return (quiet, loud.OrderBy(l => l.Position.DistanceToSquared(quiet.Position)).First());
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            if (!OwnHappeningsMath.NoiseHour(OwnHappenings.Hour(map))) return;
            var se = GenLocalDate.Season(map);
            if (se != Season.Winter && se != Season.PermanentWinter && map.mapTemperature.OutdoorTemp > -5f) return;
            var (quiet, loud) = Scene(map, g);
            if (quiet == null || !OwnBetween.Follows(b, H, g, map, OwnHappeningsMath.NoiseChance)) return;
            Begin(b, g, map, quiet, loud);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Pawn quiet, Pawn loud)
        {
            if (!OwnBetween.Send(quiet, map, loud.Position, 1f, 2)) return false;
            var s = OwnBetween.Begin(b, OwnHappening.HushNoise, g, map, loud.Position, quiet, loud, 2f);
            s.stage = 1;
            return true;
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
            if (s.a.Position.InHorDistOf(s.b.Position, 4f) && s.a.CurJobDef != JobDefOf.Goto && Together.Can(s.a, s.b))
            {
                var st = OwnBetween.StanceOf(s.b, g);
                OwnBetween.Show(g, map, s.b.Position);
                OwnBetween.Say(s.a, H, "quiet", g, s.b);
                bool refuses = OwnHappeningsMath.Against(st) || st == BetweenStance.Other && OwnBetween.HeldBy(s.b)?.Key == "Revel";
                string reply = OwnBetween.Say(s.b, H, refuses ? "refuse" : st == BetweenStance.Holds ? "sorry" : "grumble", g, s.a);
                if (refuses)
                {
                    OwnBetween.End(H, BetweenOutcome.Quarrel, s.a, s.b, g, OwnBetween.HeldBy(s.b));
                    if (st == BetweenStance.Doubts) OwnBetween.Doubted(s.b, g, reply);
                }
                else if (Loud(s.b))
                {
                    s.b.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                    OwnHappenings.Stand(s.b, s.a.Position, 200);
                }
                OwnBetween.Card(H, g, map, s.b.Position, false, refuses ? "TextRefused" : null, s.a.Named("A"), s.b.Named("B"));
                OwnBetween.Book(H, g, map, s.b.Position, GodActKind.Act, s.a, s.b.LabelShort.Named("OTHER"));
                s.stage = -1;
                return;
            }
            if (s.a.CurJobDef != JobDefOf.Goto) OwnBetween.Send(s.a, map, s.b.Position, 1f, 2);
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var (quiet, loud) = Scene(map, g);
            if (quiet == null)
            {
                var people = OwnBetween.People(map).Where(OwnBetween.Grown).ToList();
                quiet = people.FirstOrDefault();
                loud = people.FirstOrDefault(p => p != quiet);
            }
            if (quiet == null || loud == null) return "two of the colony are needed.";
            return Begin(b, g, map, quiet, loud) ? quiet.LabelShort + " goes to ask " + loud.LabelShort + " for quiet." : "they can't reach each other.";
        }
    }
}
