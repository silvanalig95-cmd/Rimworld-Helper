using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A bowl for the newcomer (OwnHappening.EnnuWelcomed): someone joins the colony, and the colony's kindest (a kind heart
    /// first, else the best talker) carries a meal from the stores to them and stays with them a while. The newcomer won't
    /// forget it. Anyone near who already dislikes the newcomer hears a word from the one who holds the Guest, and is
    /// ashamed. The Guest asks a bed, a bowl, a kind word: the believers say so.
    /// </summary>
    public class EnnuWelcomed : BetweenKind
    {
        public override OwnHappening H => OwnHappening.EnnuWelcomed;

        /// <summary>One who joined this home lately and hasn't been welcomed yet.</summary>
        public static Pawn Newcomer(OwnHappeningsBook b, Map map) =>
            OwnBetween.People(map).FirstOrDefault(p => !b.between.welcomed.Contains(p.thingIDNumber)
                                                        && (p.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 99999999f) < OwnHappeningsMath.NewcomerDays * GenDate.TicksPerDay);

        /// <summary>The colony's kindest free now: a kind heart first, else the best at talk.</summary>
        public static Pawn Kindest(Map map, Pawn not) =>
            OwnBetween.People(map).Where(p => p != not && OwnBetween.FreeNow(p) && OwnBetween.Grown(p))
                .OrderByDescending(p => p.story?.traits?.HasTrait(TraitDefOf.Kind) == true ? 1 : 0).ThenByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault();

        /// <summary>A meal in the stores, the plainest first.</summary>
        private static Thing Meal(Map map) =>
            map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.Spawned && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer) && t.def.IsNutritionGivingIngestible && t.def.ingestible.IsMeal)
                .OrderBy(t => t.def.ingestible.preferability).FirstOrDefault();

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            var newcomer = Newcomer(b, map);
            if (newcomer == null) return;
            b.between.welcomed.Add(newcomer.thingIDNumber);
            b.between.Trim();
            if (!OwnBetween.Follows(b, H, g, map, OwnHappeningsMath.WelcomeChance)) return;
            Begin(b, g, map, newcomer);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Pawn newcomer)
        {
            var kind = Kindest(map, newcomer);
            if (kind == null) return false;
            var s = OwnBetween.Begin(b, OwnHappening.EnnuWelcomed, g, map, newcomer.Position, newcomer, kind, 4f);
            var meal = Meal(map);
            var beside = OwnHappenings.Beside(map, newcomer.Position, kind);
            if (meal != null && beside.IsValid && OwnHappenings.Carry(kind, meal, beside, 1))
            {
                s.word = meal.LabelNoCount;
                s.num = 1;
            }
            else OwnBetween.Send(kind, map, newcomer.Position, 2f, 2);
            s.stage = 1;
            return true;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var newcomer = s.a;
            var kind = s.b;
            if (!OwnBetween.Still(newcomer, map) || !OwnBetween.Still(kind, map))
            {
                s.stage = -1;
                return;
            }
            bool walking = kind.CurJobDef == JobDefOf.HaulToCell || kind.CurJobDef == JobDefOf.Goto;
            if (!walking && kind.Position.InHorDistOf(newcomer.Position, 4f) && Together.Can(kind, newcomer))
            {
                OwnBetween.Face(kind, newcomer, 900);
                OwnBetween.Say(kind, H, s.num == 1 ? "bowl" : "welcome", g, newcomer, (s.word ?? "").Named("MEAL"));
                OwnBetween.Say(newcomer, H, "thanks", g, kind);
                OwnBetween.End(H, BetweenOutcome.Kind, newcomer, kind);
                OwnBetween.Card(H, g, map, newcomer.Position, false, s.num == 1 ? null : "TextNoBowl", newcomer.Named("A"), kind.Named("B"), (s.word ?? "").Named("MEAL"));
                OwnBetween.Book(H, g, map, newcomer.Position, GodActKind.Blessing, kind, newcomer.LabelShort.Named("OTHER"));
                // Anyone near who already dislikes the newcomer hears a word from one who holds the Guest.
                var sour = OwnBetween.People(map).FirstOrDefault(p => p != kind && p != newcomer && p.Position.InHorDistOf(newcomer.Position, 12f)
                                                                      && OwnBetween.Opinion(p, newcomer) <= -10f && Together.Can(kind, p));
                if (sour != null && OwnBetween.Holds(kind, g))
                {
                    OwnBetween.Say(kind, H, "chide", g, sour, newcomer.LabelShort.Named("NEWCOMER"));
                    OwnBetween.End(H, BetweenOutcome.Ashamed, sour, kind, null, null);
                }
                s.stage = -1;
                return;
            }
            if (!walking && !kind.Position.InHorDistOf(newcomer.Position, 4f)) OwnBetween.Send(kind, map, newcomer.Position, 1f, 2);
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var newcomer = Newcomer(b, map) ?? OwnBetween.People(map).OrderBy(p => p.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f).FirstOrDefault();
            if (newcomer == null) return "nobody to welcome.";
            return Begin(b, g, map, newcomer) ? "a bowl is carried to " + newcomer.LabelShort + "." : "nobody free to carry it.";
        }
    }

    /// <summary>
    /// Waiting up for the late one (OwnHappening.EnnuWaitedUp): one of the colony is out far on the land late at night, and
    /// the one who likes them best waits for them at the edge of the colony on their side, a light about them. When the
    /// late one comes in, the two walk in together, closer than before. A place by the fire kept for whoever comes.
    /// </summary>
    public class EnnuWaitedUp : BetweenKind
    {
        public override OwnHappening H => OwnHappening.EnnuWaitedUp;

        public static (Pawn far, Pawn waiter) Late(Map map)
        {
            var mid = OwnHappenings.Middle(map);
            var people = OwnBetween.People(map);
            foreach (var far in people.Where(p => OwnBetween.Grown(p) && !p.Position.InHorDistOf(mid, OwnHappeningsMath.FarCells)))
            {
                var waiter = people.Where(p => p != far && OwnBetween.FreeNow(p) && p.Position.InHorDistOf(mid, 30f) && OwnBetween.Opinion(p, far) >= OwnHappeningsMath.WaiterOpinion)
                    .OrderByDescending(p => OwnBetween.Opinion(p, far)).FirstOrDefault();
                if (waiter != null) return (far, waiter);
            }
            return (null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            if (!OwnHappeningsMath.WaitHour(OwnHappenings.Hour(map))) return;
            var (far, waiter) = Late(map);
            if (far == null || !OwnHappenings.Calls(b, H, GodPaceMath.Trigger.Ordinary, g, map, now)) return;
            Begin(b, g, map, far, waiter);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Pawn far, Pawn waiter)
        {
            var mid = OwnHappenings.Middle(map);
            // The colony's edge on their side: a door of the colony's toward them, else open ground a way out from the middle.
            var door = map.listerBuildings.allBuildingsColonist.OfType<Building_Door>().Where(d => d.Position.InHorDistOf(mid, 40f))
                .OrderBy(d => d.Position.DistanceToSquared(far.Position)).FirstOrDefault();
            var at = door != null ? door.Position : new IntVec3(mid.x + (int)((far.Position.x - mid.x) * 0.3f), 0, mid.z + (int)((far.Position.z - mid.z) * 0.3f));
            if (!OwnBetween.Send(waiter, map, at, 2f, 2)) return false;
            var s = OwnBetween.Begin(b, OwnHappening.EnnuWaitedUp, g, map, at, far, waiter, 7f);
            s.stage = 1;
            return true;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var far = s.a;
            var waiter = s.b;
            if (!OwnBetween.Still(waiter, map) || far == null || far.Dead || far.Map != map)
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 1)
            {
                if (OwnBetween.There(waiter, s.at, 4f))
                {
                    OwnHappenings.Stand(waiter, far.Position, 1200);
                    OwnHappenings.Glow(map, waiter.Position, new UnityEngine.Color(1f, 0.82f, 0.55f), 5, 0.6f, 0.4f);
                    s.stage = 2;
                }
                else if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 250);
                return;
            }
            // Waiting: kept at it, a light about them, until the late one comes in (or the small hours).
            if (far.Position.InHorDistOf(waiter.Position, 7f) && OwnBetween.Still(far, map) && Together.Can(far, waiter))
            {
                OwnBetween.Face(waiter, far, 400);
                OwnBetween.Say(waiter, H, "waited", g, far);
                OwnBetween.Say(far, H, "late", g, waiter);
                OwnBetween.End(H, BetweenOutcome.Closer, far, waiter);
                OwnBetween.Card(H, g, map, waiter.Position, false, null, far.Named("A"), waiter.Named("B"));
                OwnBetween.Book(H, g, map, waiter.Position, GodActKind.Blessing, waiter, far.LabelShort.Named("OTHER"));
                s.stage = -1;
                return;
            }
            int hour = OwnHappenings.Hour(map);
            if (now > s.until || hour >= 4 && hour < 12)
            {
                s.stage = -1;
                return;
            }
            if (waiter.CurJobDef != JobDefOf.Wait_MaintainPosture) OwnHappenings.Stand(waiter, far.Position, 1200);
            if (Rand.Chance(0.3f)) OwnHappenings.Glow(map, waiter.Position, new UnityEngine.Color(1f, 0.82f, 0.55f), 3, 0.5f, 0.35f);
            OwnBetween.Wait(s, 250);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var (far, waiter) = Late(map);
            if (far == null)
            {
                var people = OwnBetween.People(map).Where(OwnBetween.Grown).ToList();
                far = people.OrderByDescending(p => p.Position.DistanceToSquared(OwnHappenings.Middle(map))).FirstOrDefault();
                waiter = people.Where(p => p != far).OrderByDescending(p => OwnBetween.Opinion(p, far)).FirstOrDefault();
            }
            if (far == null || waiter == null) return "two of the colony are needed.";
            return Begin(b, g, map, far, waiter) ? waiter.LabelShort + " waits up for " + far.LabelShort + " at the edge of the colony." : "nowhere to wait they can reach.";
        }
    }
}
