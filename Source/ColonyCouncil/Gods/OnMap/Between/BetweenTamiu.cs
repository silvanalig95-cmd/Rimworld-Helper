using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A cat on the grave (OwnHappening.MogGraveCat; Tamiu's key is Mog): at dusk a cat (the colony's own, else a stray in
    /// from the edge) walks to the grave of one of the colony who died this season or the last and sits on it, and the two who
    /// loved the dead most come and sit with it. They come away closer. A cat sits on each new grave of a colony that keeps
    /// its dead well, the stories of her say.
    /// </summary>
    public class MogGraveCat : BetweenKind
    {
        public override OwnHappening H => OwnHappening.MogGraveCat;

        /// <summary>A grave of the colony's dead lately with two of the living who loved them free now: the grave, the dead, and the two.</summary>
        public static (Building_Grave grave, Pawn dead, Pawn a, Pawn b) Scene(Map map)
        {
            var people = OwnBetween.People(map).Where(p => OwnBetween.FreeNow(p)).ToList();
            foreach (var g in map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>().Where(x => x.HasCorpse))
            {
                var dead = g.Corpse?.InnerPawn;
                if (dead?.RaceProps?.Humanlike != true || OwnBetween.Now - g.Corpse.timeOfDeath > OwnHappeningsMath.GraveDays * GenDate.TicksPerDay) continue;
                var two = people.Where(p => OwnBetween.Opinion(p, dead) >= OwnHappeningsMath.MournerFrom).OrderByDescending(p => OwnBetween.Opinion(p, dead)).Take(2).ToList();
                if (two.Count == 2) return (g, dead, two[0], two[1]);
            }
            return (null, null, null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < OwnHappeningsMath.GraveDuskFrom || hour >= OwnHappeningsMath.GraveDuskTo) return;
            var (grave, dead, x, y) = Scene(map);
            if (grave == null || !OwnHappenings.Calls(b, H, GodPaceMath.Trigger.Ordinary, g, map, now)) return;
            Begin(b, g, map, grave, dead, x, y);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Building_Grave grave, Pawn dead, Pawn x, Pawn y)
        {
            var seat = OwnHappenings.Beside(map, grave.Position, null);
            if (!seat.IsValid) return false;
            var cat = OwnHappenings.ColonyCat(map, seat);
            bool stray = cat == null;
            cat ??= OwnHappenings.StrayCat(map, seat);
            if (cat == null) return false;
            OwnHappenings.Lead(cat, seat, 5000);
            OwnBetween.Send(x, map, grave.Position, 2f, 2);
            OwnBetween.Send(y, map, grave.Position, 2f, 2);
            var s = OwnBetween.Begin(b, OwnHappening.MogGraveCat, g, map, grave.Position, x, y, 4f);
            s.c = cat;
            s.thing = grave;
            s.word = dead.LabelShort;
            s.num = stray ? 1 : 0;
            s.stage = 1;
            return true;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var cat = s.c;
            if (!OwnBetween.Still(s.a, map) || !OwnBetween.Still(s.b, map) || cat == null || cat.Dead || !cat.Spawned)
            {
                End(s);
                return;
            }
            if (OwnBetween.There(s.a, s.at, 3f) && OwnBetween.There(s.b, s.at, 3f) && Together.Can(s.a, s.b))
            {
                var dead = (s.word ?? "").Named("DEAD");
                OwnHappenings.Stand(s.a, s.at, 1500);
                OwnHappenings.Stand(s.b, s.at, 1500);
                OwnBetween.Show(g, map, s.at);
                OwnBetween.Say(s.a, H, "cat", g, s.b, dead);
                OwnBetween.Say(s.b, H, "remember", g, s.a, dead);
                OwnBetween.End(H, BetweenOutcome.Closer, s.a, s.b, null, null, dead);
                OwnBetween.Card(H, g, map, s.at, false, s.num == 1 ? "TextStray" : null, s.a.Named("A"), s.b.Named("B"), dead);
                OwnBetween.Book(H, g, map, s.at, GodActKind.Blessing, s.a, s.b.LabelShort.Named("OTHER"), dead);
                End(s);
                return;
            }
            if (now > s.until)
            {
                End(s);
                return;
            }
            OwnBetween.Wait(s, 120);
        }

        /// <summary>Done: a stray goes back the way it came after a while.</summary>
        private static void End(BetweenScene s)
        {
            if (s.num == 1 && s.c != null && !s.c.Dead) OwnHappenings.SendOff(s.c, GenDate.TicksPerHour * 3);
            s.stage = -1;
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var (grave, dead, x, y) = Scene(map);
            if (grave == null)
            {
                grave = map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>().FirstOrDefault(t => t.HasCorpse);
                dead = grave?.Corpse?.InnerPawn;
                var two = OwnBetween.People(map).Where(OwnBetween.Grown).Take(2).ToList();
                if (two.Count == 2)
                {
                    x = two[0];
                    y = two[1];
                }
            }
            if (grave == null || dead == null || x == null) return "a grave holding the dead and two of the colony are needed.";
            return Begin(b, g, map, grave, dead, x, y) ? "a cat sits on " + dead.LabelShort + "'s grave; " + x.LabelShort + " and " + y.LabelShort + " come to sit with it." : "no cat could come.";
        }
    }

    /// <summary>
    /// Riddles at supper (OwnHappening.MogRiddle): at supper two of the colony's quick minds trade riddles. The better one
    /// wins (by wit and the night's luck), and a cat walks at the winner's heel the rest of the evening. A good loser comes
    /// away closer; a sore one resents it. She loves the living who are clever, the stories say.
    /// </summary>
    public class MogRiddle : BetweenKind
    {
        public override OwnHappening H => OwnHappening.MogRiddle;

        private static int Wit(Pawn p) => p?.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;

        public static (Pawn a, Pawn b) Pair(Map map)
        {
            var clever = OwnBetween.People(map).Where(p => OwnBetween.Grown(p) && OwnBetween.FreeNow(p) && Wit(p) >= OwnHappeningsMath.RiddlerFrom).OrderByDescending(Wit).ToList();
            foreach (var x in clever)
            {
                var y = clever.FirstOrDefault(o => o != x && o.Position.InHorDistOf(x.Position, 20f));
                if (y != null) return (x, y);
            }
            return (null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < OwnHappeningsMath.GraveDuskFrom || hour >= OwnHappeningsMath.GraveDuskTo) return;
            var (x, y) = Pair(map);
            if (x == null || !OwnHappenings.Calls(b, H, GodPaceMath.Trigger.Ordinary, g, map, now)) return;
            Begin(b, g, map, x, y);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Pawn x, Pawn y)
        {
            if (!x.Position.InHorDistOf(y.Position, 3f) && !OwnBetween.Send(y, map, x.Position, 1f, 2)) return false;
            var s = OwnBetween.Begin(b, OwnHappening.MogRiddle, g, map, x.Position, x, y, OwnHappeningsMath.HeelHours + 2f);
            s.stage = 1;
            return true;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            if (s.stage == 2)
            {
                // The cat at the winner's heel the evening through.
                var winner = s.c;
                if (s.thing is Pawn cat && !cat.Dead && cat.Spawned && OwnBetween.Still(winner, map) && now < s.until)
                {
                    var heel = OwnHappenings.Beside(map, winner.Position, null);
                    if (heel.IsValid && !cat.Position.InHorDistOf(winner.Position, 2.5f)) OwnHappenings.Lead(cat, heel, 600);
                    OwnBetween.Wait(s, 300);
                    return;
                }
                if (s.num == 1 && s.thing is Pawn stray && !stray.Dead) OwnHappenings.SendOff(stray, 600);
                s.stage = -1;
                return;
            }
            if (!OwnBetween.Still(s.a, map) || !OwnBetween.Still(s.b, map))
            {
                s.stage = -1;
                return;
            }
            if (s.a.Position.InHorDistOf(s.b.Position, 4f) && s.b.CurJobDef != JobDefOf.Goto && Together.Can(s.a, s.b))
            {
                OwnBetween.Face(s.a, s.b, 900);
                OwnBetween.Say(s.a, H, "ask", g, s.b);
                bool firstWins = OwnHappeningsMath.FirstWins(Wit(s.a), Wit(s.b), Rand.Value);
                var winner = firstWins ? s.a : s.b;
                var loser = firstWins ? s.b : s.a;
                OwnBetween.Say(loser, H, "stumped", g, winner);
                var o = OwnHappeningsMath.Laugh(OwnBetween.Opinion(loser, winner));
                OwnBetween.Proud(H, winner);
                OwnBetween.End(H, o == BetweenOutcome.Closer ? BetweenOutcome.Closer : BetweenOutcome.Jealous, loser, winner);
                var cat = OwnHappenings.ColonyCat(map, winner.Position);
                bool stray = cat == null;
                cat ??= OwnHappenings.StrayCat(map, winner.Position);
                OwnBetween.Card(H, g, map, winner.Position, false, cat == null ? "TextNoCat" : null, winner.Named("A"), loser.Named("B"));
                OwnBetween.Book(H, g, map, winner.Position, GodActKind.Blessing, loser, winner.LabelShort.Named("OTHER"));
                if (cat == null)
                {
                    s.stage = -1;
                    return;
                }
                OwnHappenings.Lead(cat, OwnHappenings.Beside(map, winner.Position, null), 1200);
                OwnBetween.Show(g, map, winner.Position);
                s.c = winner;
                s.thing = cat;
                s.num = stray ? 1 : 0;
                s.stage = 2;
                s.until = now + OwnHappeningsMath.HeelHours * GenDate.TicksPerHour;
                OwnBetween.Wait(s, 300);
                return;
            }
            if (s.b.CurJobDef != JobDefOf.Goto) OwnBetween.Send(s.b, map, s.a.Position, 1f, 2);
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var (x, y) = Pair(map);
            if (x == null)
            {
                var two = OwnBetween.People(map).Where(OwnBetween.Grown).OrderByDescending(Wit).Take(2).ToList();
                if (two.Count < 2) return "two grown of the colony are needed.";
                x = two[0];
                y = two[1];
            }
            return Begin(b, g, map, x, y) ? x.LabelShort + " and " + y.LabelShort + " trade riddles." : "they can't reach each other.";
        }
    }
}
