using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The old one and the child (OwnHappening.BellwetherTold): at dusk the colony's eldest takes a child out to the edge and
    /// tells them of the Old Horn, and the wild grazers on the land drift close and graze round the two. The child and the
    /// old one come away closer. He is curious about children, the stories say, and lets them come near.
    /// </summary>
    public class BellwetherTold : BetweenKind
    {
        public override OwnHappening H => OwnHappening.BellwetherTold;

        /// <summary>The eldest grown of the colony free now (the age the stories ask for, else forty at least), or null.</summary>
        public static Pawn Elder(Map map) =>
            OwnBetween.People(map).Where(p => OwnBetween.FreeNow(p) && OwnBetween.Grown(p) && p.ageTracker.AgeBiologicalYearsFloat >= 40f)
                .OrderByDescending(p => p.ageTracker.AgeBiologicalYearsFloat).FirstOrDefault();

        public static Pawn Child(Map map) => OwnBetween.People(map).Where(p => OwnBetween.FreeNow(p) && p.DevelopmentalStage.Child()).RandomElementWithFallback();

        /// <summary>The wild beasts on the land that graze, calm and free.</summary>
        public static List<Pawn> Grazers(Map map) =>
            map.mapPawns.AllPawnsSpawned.Where(a => a.RaceProps?.Animal == true && a.Faction == null && !a.Dead && !a.Downed && !a.InMentalState && a.RaceProps.Eats(FoodTypeFlags.Plant)
                                                    && !a.RaceProps.predator).ToList();

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < OwnHappeningsMath.ToldFrom || hour >= OwnHappeningsMath.ToldTo) return;
            var elder = Elder(map);
            var child = elder == null ? null : Child(map);
            var t = OwnHappeningsMath.ToldTrigger(child != null, child == null ? 0 : Grazers(map).Count, GodPace.DaysSince(b.lastPeace, now) < OwnHappeningsMath.PeaceDays);
            if (t == GodPaceMath.Trigger.None || !OwnHappenings.Calls(b, H, t, g, map, now)) return;
            Begin(b, g, map, elder, child);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Pawn elder, Pawn child)
        {
            if (!RCellFinder.TryFindRandomSpotJustOutsideColony(elder, out var spot)) spot = GodOmens.Open(map);
            if (!spot.IsValid || !OwnBetween.Send(elder, map, spot, 2f, 2) || !OwnBetween.Send(child, map, spot, 2f, 2)) return false;
            var s = OwnBetween.Begin(b, OwnHappening.BellwetherTold, g, map, spot, elder, child, 3f);
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
            if (s.stage == 1)
            {
                if (OwnBetween.There(s.a, s.at) && OwnBetween.There(s.b, s.at) && Together.Can(s.a, s.b))
                {
                    OwnBetween.Face(s.a, s.b, 1500);
                    OwnBetween.Say(s.a, H, "tell", g, s.b);
                    // The grazers drift near and graze round them.
                    int n = 0;
                    foreach (var a in Grazers(map).Where(x => x.Position.InHorDistOf(s.at, 70f)).OrderBy(x => x.Position.DistanceToSquared(s.at)).Take(3))
                    {
                        var to = CellFinder.RandomClosewalkCellNear(s.at, map, 6, c => c.Standable(map) && !c.InHorDistOf(s.at, 2.5f));
                        if (!to.IsValid) continue;
                        OwnHappenings.Lead(a, to, 2000);
                        n++;
                    }
                    s.num = n;
                    OwnBetween.Show(g, map, s.at);
                    s.stage = 2;
                    OwnBetween.Wait(s, 900);
                    return;
                }
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 120);
                return;
            }
            // A while later: the child's word, the card and the book.
            if (Together.Can(s.a, s.b))
            {
                OwnBetween.Say(s.b, H, "child", g, s.a);
                OwnBetween.End(H, BetweenOutcome.Closer, s.a, s.b);
            }
            OwnBetween.Card(H, g, map, s.at, false, s.num > 0 ? null : "TextAlone", s.a.Named("A"), s.b.Named("B"));
            OwnBetween.Book(H, g, map, s.at, GodActKind.Blessing, s.a, s.b.LabelShort.Named("OTHER"));
            s.stage = -1;
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var elder = OwnBetween.People(map).Where(OwnBetween.Grown).OrderByDescending(p => p.ageTracker.AgeBiologicalYearsFloat).FirstOrDefault();
            var child = OwnBetween.People(map).FirstOrDefault(p => p.DevelopmentalStage.Child()) ?? OwnBetween.People(map).FirstOrDefault(p => p != elder);
            if (elder == null || child == null) return "an old one and a child (or another) are needed.";
            return Begin(b, g, map, elder, child) ? elder.LabelShort + " takes " + child.LabelShort + " out to the edge at dusk." : "no open ground outside the colony they can reach.";
        }
    }

    /// <summary>
    /// The hunter who let it go (OwnHappening.BellwetherSpared): on a hunt, the beast stops, turns and looks at the hunter, and
    /// the hunter lowers the weapon and lets it go off the land. Only while the colony has food enough, never for a scoffer.
    /// At home one who holds the Old Horn tells them they did right, or the cook grumbles about the meat, and the hunter
    /// answers.
    /// </summary>
    public class BellwetherSpared : BetweenKind
    {
        public override OwnHappening H => OwnHappening.BellwetherSpared;

        /// <summary>One of the colony on a hunt of a wild beast, and the beast.</summary>
        public static (Pawn hunter, Pawn beast) Hunting(Map map)
        {
            foreach (var p in OwnBetween.People(map))
            {
                if (p.CurJobDef != JobDefOf.Hunt || !(p.CurJob.targetA.Thing is Pawn a) || a.Faction != null || a.Dead || a.Downed || a.RaceProps?.Animal != true) continue;
                if (!p.Position.InHorDistOf(a.Position, 30f)) continue;
                return (p, a);
            }
            return (null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            var (hunter, beast) = Hunting(map);
            if (hunter == null || !OwnHappeningsMath.MaySpare(OwnHappenings.FoodDays(map), OwnBetween.Doubts(hunter))) return;
            if (!OwnBetween.Follows(b, H, g, map, OwnHappeningsMath.SparedChance)) return;
            Begin(b, g, map, hunter, beast);
        }

        private static void Begin(OwnHappeningsBook b, God g, Map map, Pawn hunter, Pawn beast)
        {
            var s = OwnBetween.Begin(b, OwnHappening.BellwetherSpared, g, map, beast.Position, hunter, null, 10f);
            s.thing = beast;
            s.word = beast.LabelNoCount;
            // The hunt ends: the hunter stands and looks; the beast stands and looks back, then walks off the land.
            map.designationManager.RemoveAllDesignationsOn(beast);
            OwnHappenings.Stand(hunter, beast.Position, 400);
            OwnHappenings.Lead(beast, beast.Position, 400);
            OwnHappenings.SendOff(beast, 450);
            OwnBetween.Show(g, map, beast.Position);
            OwnBetween.Say(hunter, OwnHappening.BellwetherSpared, "let", g, null, s.word.Named("BEAST"));
            OwnBetween.Card(OwnHappening.BellwetherSpared, g, map, beast.Position, true, null, hunter.Named("A"), s.word.Named("BEAST"));
            OwnBetween.Book(OwnHappening.BellwetherSpared, g, map, beast.Position, GodActKind.Blessing, hunter, s.word.Named("BEAST"));
            s.stage = 1;
            OwnBetween.Wait(s, GenDate.TicksPerHour * 3);
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var hunter = s.a;
            if (!OwnBetween.Still(hunter, map))
            {
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 500);
                return;
            }
            if (s.stage == 1)
            {
                // Home again: one who holds the Old Horn walks over to say they did right, else the cook comes to grumble.
                var people = OwnBetween.People(map).Where(p => p != hunter && OwnBetween.FreeNow(p) && OwnBetween.Grown(p)).ToList();
                var believer = OwnBetween.Believer(people, g);
                var cook = believer != null ? null : people.Where(p => !OwnBetween.Holds(p, g)).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Cooking)?.Level ?? 0).FirstOrDefault();
                var who = believer ?? cook;
                if (who == null || !OwnHappenings.Ours(hunter) || !OwnBetween.Send(who, map, hunter.Position, 2f, 2))
                {
                    if (now > s.until) s.stage = -1;
                    OwnBetween.Wait(s, 1000);
                    return;
                }
                s.b = who;
                s.num = believer != null ? 1 : 0;
                s.stage = 2;
                OwnBetween.Wait(s, 120);
                return;
            }
            if (!OwnBetween.Still(s.b, map))
            {
                s.stage = -1;
                return;
            }
            if (s.b.Position.InHorDistOf(hunter.Position, 4f) && Together.Can(s.b, hunter))
            {
                OwnBetween.Face(s.b, hunter, 400);
                var beast = (s.word ?? "").Named("BEAST");
                if (s.num == 1)
                {
                    OwnBetween.Say(s.b, H, "praise", g, hunter, beast);
                    OwnBetween.End(H, BetweenOutcome.Closer, hunter, s.b, null, null, beast);
                }
                else
                {
                    OwnBetween.Say(s.b, H, "grumble", g, hunter, beast);
                    OwnBetween.Say(hunter, H, "answer", g, s.b, beast);
                    OwnBetween.End(H, BetweenOutcome.Quarrel, s.b, hunter, OwnBetween.HeldBy(s.b), OwnBetween.HeldBy(hunter), beast);
                }
                s.stage = -1;
                return;
            }
            if (s.b.CurJobDef != JobDefOf.Goto) OwnBetween.Send(s.b, map, hunter.Position, 1f, 2);
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var (hunter, beast) = Hunting(map);
            if (hunter == null)
            {
                hunter = OwnBetween.People(map).FirstOrDefault(OwnBetween.Grown);
                beast = BellwetherTold.Grazers(map).OrderBy(a => hunter == null ? 0 : a.Position.DistanceToSquared(hunter.Position)).FirstOrDefault();
            }
            if (hunter == null || beast == null) return "nobody on a hunt, and no wild grazer on the land.";
            Begin(b, g, map, hunter, beast);
            return hunter.LabelShort + " lets the " + beast.LabelNoCount + " go; someone comes to them about it in three hours.";
        }
    }
}
