using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A star for two (OwnHappening.CirrusTwoWatchers): at night, under a clear sky, two of the colony out of doors near each
    /// other stop and look up as a star crosses the sky, and stand and talk a while. They come away closer. The one who
    /// drifts above notices those who look up, the believers say.
    /// </summary>
    public class CirrusTwoWatchers : BetweenKind
    {
        public override OwnHappening H => OwnHappening.CirrusTwoWatchers;

        private static bool Out(Pawn p) => p.Map != null && !p.Position.Roofed(p.Map) && !p.InBed();

        public static (Pawn a, Pawn b) Pair(Map map)
        {
            var outside = OwnBetween.People(map).Where(Out).ToList();
            foreach (var x in outside.InRandomOrder())
            {
                var y = outside.FirstOrDefault(o => o != x && o.Position.InHorDistOf(x.Position, OwnHappeningsMath.WatchersWithin));
                if (y != null) return (x, y);
            }
            return (null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            if (!OwnHappeningsMath.NightHour(OwnHappenings.Hour(map)) || !OwnHappenings.Clear(map)) return;
            var (x, y) = Pair(map);
            if (x == null) return;
            bool watching = new[] { x, y }.Any(p => p.CurJobDef?.defName == "Skygaze" || p.CurJobDef?.defName == "VSIE_Skygaze");
            if (!OwnHappenings.Calls(b, H, watching ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary, g, map, now)) return;
            Begin(b, g, map, x, y);
        }

        /// <summary>A star crossing the sky over a place: a line of small white lights drawn quickly from one side to the other.</summary>
        public static void Streak(Map map, IntVec3 over)
        {
            if (map == null || map != Find.CurrentMap) return;
            var start = over.ToVector3Shifted() + new Vector3(-9f, 0f, 6f);
            for (int i = 0; i < 14; i++)
                GodFx.Throw(GodFx.MoteDef, start + new Vector3(i * 1.3f, 0f, -i * 0.6f), map, 0.35f - i * 0.015f, new Color(0.95f, 0.97f, 1f), 0f, 0f, 0f, 0.4f + i * 0.04f);
        }

        private static void Begin(OwnHappeningsBook b, God g, Map map, Pawn x, Pawn y)
        {
            var mid = new IntVec3((x.Position.x + y.Position.x) / 2, 0, (x.Position.z + y.Position.z) / 2);
            var s = OwnBetween.Begin(b, OwnHappening.CirrusTwoWatchers, g, map, mid, x, y, 2f);
            Streak(map, mid);
            OwnHappenings.Stand(x, x.Position + IntVec3.North * 5, 300);
            OwnHappenings.Stand(y, y.Position + IntVec3.North * 5, 300);
            OwnBetween.Say(x, OwnHappening.CirrusTwoWatchers, "see", g, y);
            // The two walk to each other, and stand and talk a while.
            if (!x.Position.InHorDistOf(y.Position, 3f)) OwnBetween.Send(y, map, x.Position, 1f, 2);
            s.stage = 1;
            OwnBetween.Wait(s, 300);
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
            if (s.a.Position.InHorDistOf(s.b.Position, 4f) && s.b.CurJobDef != JobDefOf.Goto && Together.Can(s.a, s.b))
            {
                OwnBetween.Face(s.a, s.b, 900);
                OwnBetween.Say(s.b, H, "wish", g, s.a);
                OwnBetween.End(H, BetweenOutcome.Closer, s.a, s.b);
                OwnBetween.Card(H, g, map, s.at, false, null, s.a.Named("A"), s.b.Named("B"));
                OwnBetween.Book(H, g, map, s.at, GodActKind.Omen, s.a, s.b.LabelShort.Named("OTHER"));
                s.stage = -1;
                return;
            }
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var (x, y) = Pair(map);
            if (x == null)
            {
                var two = OwnBetween.People(map).Take(2).ToList();
                if (two.Count < 2) return "two of the colony are needed.";
                x = two[0];
                y = two[1];
            }
            Begin(b, g, map, x, y);
            return "a star crosses over " + x.LabelShort + " and " + y.LabelShort + ".";
        }
    }

    /// <summary>
    /// A shape in the clouds (OwnHappening.CirrusCloudShape): two of the colony out in the open under a clouded sky stop and
    /// point up; each sees the shape of the god they hold in the clouds. Two of rival or other gods argue whose it was, each
    /// holding their own the harder; a doubter laughs; two of the same god come away closer.
    /// </summary>
    public class CirrusCloudShape : BetweenKind
    {
        public override OwnHappening H => OwnHappening.CirrusCloudShape;

        /// <summary>Clouds over the land: weather that is neither clear nor a fog, and no storm.</summary>
        public static bool Clouded(Map map)
        {
            var w = map?.weatherManager?.curWeather;
            if (w == null || OwnHappenings.Clear(map)) return false;
            string n = w.defName ?? "";
            return n.IndexOf("Fog", System.StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("Thunder", System.StringComparison.OrdinalIgnoreCase) < 0
                   && n.IndexOf("Flash", System.StringComparison.OrdinalIgnoreCase) < 0 && !w.isBad;
        }

        public static (Pawn a, Pawn b) Pair(Map map)
        {
            var outside = OwnBetween.People(map).Where(p => OwnBetween.Grown(p) && !p.Position.Roofed(map) && (OwnBetween.HeldBy(p) != null || OwnBetween.Doubts(p))).ToList();
            foreach (var x in outside.Where(p => OwnBetween.HeldBy(p) != null).InRandomOrder())
            {
                var y = outside.FirstOrDefault(o => o != x && o.Position.InHorDistOf(x.Position, OwnHappeningsMath.CloudWithin));
                if (y != null) return (x, y);
            }
            return (null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < 9 || hour > 17 || !Clouded(map)) return;
            var (x, y) = Pair(map);
            if (x == null || !OwnHappenings.Calls(b, H, GodPaceMath.Trigger.Ordinary, g, map, now)) return;
            Begin(b, g, map, x, y);
        }

        private static void Begin(OwnHappeningsBook b, God g, Map map, Pawn x, Pawn y)
        {
            var s = OwnBetween.Begin(b, OwnHappening.CirrusCloudShape, g, map, x.Position, x, y, 1f);
            OwnBetween.Face(x, y, 500);
            s.stage = 1;
            OwnBetween.Wait(s, 60);
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
            var ga = OwnBetween.HeldBy(s.a);
            var gb = OwnBetween.HeldBy(s.b);
            OwnHappenings.Stand(s.a, s.a.Position + IntVec3.North * 6, 400);
            OwnHappenings.Stand(s.b, s.b.Position + IntVec3.North * 6, 400);
            OwnBetween.Say(s.a, H, ga != null ? "shape" : "shapeplain", g, s.b, PawnGods.NameOf(ga, s.a).Named("THEIRS"));
            BetweenOutcome o;
            string reply;
            if (OwnBetween.Doubts(s.b))
            {
                reply = OwnBetween.Say(s.b, H, "doubt", g, s.a);
                o = OwnHappeningsMath.Laugh(OwnBetween.Opinion(s.a, s.b));
                OwnBetween.Doubted(s.b, ga ?? g, reply);
            }
            else if (gb == ga)
            {
                reply = OwnBetween.Say(s.b, H, gb != null ? "same" : "plain", g, s.a, PawnGods.NameOf(gb, s.b).Named("THEIRS"));
                o = BetweenOutcome.Closer;
            }
            else
            {
                reply = OwnBetween.Say(s.b, H, gb != null ? "other" : "plain", g, s.a, PawnGods.NameOf(gb, s.b).Named("THEIRS"));
                o = BetweenOutcome.Quarrel;
            }
            OwnBetween.End(H, o, s.a, s.b, ga, gb);
            OwnBetween.Card(H, g, map, s.at, false, null, s.a.Named("A"), s.b.Named("B"));
            OwnBetween.Book(H, g, map, s.at, GodActKind.Omen, s.a, s.b.LabelShort.Named("OTHER"));
            s.stage = -1;
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var (x, y) = Pair(map);
            if (x == null)
            {
                var two = OwnBetween.People(map).Where(OwnBetween.Grown).Take(2).ToList();
                if (two.Count < 2) return "two grown of the colony are needed.";
                x = two[0];
                y = two[1];
            }
            Begin(b, g, map, x, y);
            return x.LabelShort + " and " + y.LabelShort + " stop to look at the clouds.";
        }
    }
}
