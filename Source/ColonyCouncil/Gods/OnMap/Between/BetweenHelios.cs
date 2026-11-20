using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Out into the sun (OwnHappening.HeliosSun; C9 of docs/content-update-proposals.md): on a clear, warm noon, one who holds
    /// the Giver (else the doctor) walks one of the colony who is getting over an illness or an ailment out of doors. The two
    /// sit an hour in the sun, and the illness moves on a little faster for it. Only for someone up and about, never someone
    /// the doctors keep abed, and never in a heat wave. The one walked out won't forget it.
    /// </summary>
    public class HeliosSun : BetweenKind
    {
        public override OwnHappening H => OwnHappening.HeliosSun;

        private static readonly Color Gold = new Color(1f, 0.86f, 0.45f);

        /// <summary>Their illness or ailment showing, one the body is fighting off (not a wound, not still hidden), or null.</summary>
        public static Hediff Illness(Pawn p)
        {
            var hs = p?.health?.hediffSet;
            if (hs == null) return null;
            foreach (var h in hs.hediffs)
            {
                if (h == null || !h.Visible) continue;
                var im = h.TryGetComp<HediffComp_Immunizable>();
                if (im == null || im.FullyImmune) continue;
                if (im is HediffComp_AilmentImmunizable ai && ai.Incubating) continue;
                return h;
            }
            return null;
        }

        /// <summary>One getting over an illness, up and about (not abed and not to be kept abed).</summary>
        public static Pawn Convalescent(Map map) =>
            OwnBetween.People(map).FirstOrDefault(p => !p.InBed() && Illness(p) != null && !HealthAIUtility.ShouldSeekMedicalRest(p));

        public static Pawn Helper(Map map, God g, Pawn sick)
        {
            var people = OwnBetween.People(map).Where(p => p != sick && OwnBetween.FreeNow(p) && OwnBetween.Grown(p)).ToList();
            return OwnBetween.Believer(people, g) ?? people.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0).FirstOrDefault();
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            bool heat = OwnHappenings.Condition(map, "HeatWave") != null;
            if (!OwnHappeningsMath.SunNoon(OwnHappenings.Hour(map), map.mapTemperature.OutdoorTemp, OwnHappenings.Clear(map), heat)) return;
            var sick = Convalescent(map);
            var helper = sick == null ? null : Helper(map, g, sick);
            if (helper == null || !OwnHappenings.Calls(b, H, GodPaceMath.Trigger.Ordinary, g, map, now)) return;
            Begin(b, g, map, sick, helper);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Pawn sick, Pawn helper)
        {
            var spot = CellFinder.RandomClosewalkCellNear(sick.Position, map, 15, c => c.Standable(map) && !c.Roofed(map) && !c.GetTerrain(map).IsWater && c.GetFirstBuilding(map) == null);
            if (!spot.IsValid || !OwnBetween.Send(helper, map, sick.Position, 1f, 2)) return false;
            var s = OwnBetween.Begin(b, OwnHappening.HeliosSun, g, map, spot, sick, helper, 4f);
            s.word = Illness(sick)?.LabelBase ?? "";
            s.stage = 1;
            return true;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var sick = s.a;
            var helper = s.b;
            if (!OwnBetween.Still(sick, map) || !OwnBetween.Still(helper, map))
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 1)
            {
                // The helper comes for them; they walk out into the sun together.
                if (helper.Position.InHorDistOf(sick.Position, 4f) && helper.CurJobDef != JobDefOf.Goto && Together.Can(helper, sick))
                {
                    OwnBetween.Say(helper, H, "come", g, sick, (s.word ?? "").Named("ILLNESS"));
                    OwnBetween.Send(sick, map, s.at, 2f, 1);
                    OwnBetween.Send(helper, map, s.at, 2f, 2);
                    s.stage = 2;
                }
                else if (helper.CurJobDef != JobDefOf.Goto) OwnBetween.Send(helper, map, sick.Position, 1f, 2);
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 120);
                return;
            }
            if (s.stage == 2)
            {
                if (OwnBetween.There(sick, s.at) && OwnBetween.There(helper, s.at) && Together.Can(sick, helper))
                {
                    OwnBetween.Face(sick, helper, GenDate.TicksPerHour);
                    OwnHappenings.Glow(map, sick.Position, Gold, 6, 1f, 0.4f);
                    OwnBetween.Show(g, map, s.at);
                    OwnBetween.Say(sick, H, "warm", g, helper);
                    s.stage = 3;
                    OwnBetween.Wait(s, GenDate.TicksPerHour);
                    return;
                }
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 120);
                return;
            }
            // An hour in the sun: the illness moves on a little.
            var ill = Illness(sick);
            if (ill != null) ill.Severity = Mathf.Max(0.001f, ill.Severity - OwnHappeningsMath.SunEase);
            OwnHappenings.Glow(map, sick.Position, Gold, 4, 0.8f, 0.35f);
            OwnBetween.End(H, BetweenOutcome.Kind, sick, helper, null, null, (s.word ?? "").Named("ILLNESS"));
            OwnBetween.Card(H, g, map, s.at, false, null, sick.Named("A"), helper.Named("B"), (s.word ?? "").Named("ILLNESS"));
            OwnBetween.Book(H, g, map, s.at, GodActKind.Blessing, helper, sick.LabelShort.Named("OTHER"));
            s.stage = -1;
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var sick = Convalescent(map) ?? OwnBetween.People(map).FirstOrDefault(p => !p.InBed());
            var helper = sick == null ? null : Helper(map, g, sick);
            if (sick == null || helper == null) return "one up and about and one free to walk them out are needed.";
            return Begin(b, g, map, sick, helper) ? helper.LabelShort + " walks " + sick.LabelShort + " out into the sun (the illness eases only if they have one)." : "no open ground in the sun near them.";
        }
    }

    /// <summary>
    /// His light on both alike (OwnHappening.HeliosAlike): at a clear noon two of the colony at odds are out of doors near each
    /// other, and the light lies gold on both of them. One who holds the Giver (one of the two, else someone near who walks
    /// over) says he gives to everyone alike, and so might they. They make it up, unless one of them scoffs at it.
    /// </summary>
    public class HeliosAlike : BetweenKind
    {
        public override OwnHappening H => OwnHappening.HeliosAlike;

        private static readonly Color Gold = new Color(1f, 0.86f, 0.45f);

        public static (Pawn a, Pawn b, Pawn speaker) Scene(Map map, God g)
        {
            var people = OwnBetween.People(map).Where(OwnBetween.Grown).ToList();
            var outside = people.Where(p => !p.Position.Roofed(map)).ToList();
            foreach (var x in outside.InRandomOrder())
            {
                var y = outside.FirstOrDefault(o => o != x && o.Position.InHorDistOf(x.Position, OwnHappeningsMath.AlikeWithin)
                                                    && (OwnBetween.Opinion(x, o) <= OwnHappeningsMath.AlikeBelow || OwnBetween.Opinion(o, x) <= OwnHappeningsMath.AlikeBelow));
                if (y == null) continue;
                var speaker = OwnBetween.Holds(x, g) ? x : OwnBetween.Holds(y, g) ? y
                    : OwnBetween.Believer(people.Where(p => p != x && p != y && OwnBetween.FreeNow(p) && p.Position.InHorDistOf(x.Position, 25f)), g);
                if (speaker != null) return (x, y, speaker);
            }
            return (null, null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < OwnHappeningsMath.NoonFrom || hour > 15 || !OwnHappenings.Clear(map)) return;
            var (x, y, speaker) = Scene(map, g);
            if (x == null || !OwnHappenings.Calls(b, H, GodPaceMath.Trigger.Ordinary, g, map, now)) return;
            Begin(b, g, map, x, y, speaker);
        }

        private static void Begin(OwnHappeningsBook b, God g, Map map, Pawn x, Pawn y, Pawn speaker)
        {
            var s = OwnBetween.Begin(b, OwnHappening.HeliosAlike, g, map, x.Position, x, y, 2f);
            s.c = speaker;
            OwnHappenings.Glow(map, x.Position, Gold, 6, 0.9f, 0.4f);
            OwnHappenings.Glow(map, y.Position, Gold, 6, 0.9f, 0.4f);
            OwnBetween.Show(g, map, x.Position);
            OwnHappenings.Stand(x, x.Position + IntVec3.East * 6, 300);
            OwnHappenings.Stand(y, y.Position + IntVec3.East * 6, 300);
            if (speaker != x && speaker != y) OwnBetween.Send(speaker, map, x.Position, 1f, 2);
            if (!x.Position.InHorDistOf(y.Position, 3f)) OwnBetween.Send(y, map, x.Position, 1f, 2);
            s.stage = 1;
            OwnBetween.Wait(s, 200);
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            if (!OwnBetween.Still(s.a, map) || !OwnBetween.Still(s.b, map) || !OwnBetween.Still(s.c, map))
            {
                s.stage = -1;
                return;
            }
            bool near = s.a.Position.InHorDistOf(s.b.Position, 4f) && s.c.Position.InHorDistOf(s.a.Position, 5f) && s.b.CurJobDef != JobDefOf.Goto && s.c.CurJobDef != JobDefOf.Goto;
            if (near && Together.Can(s.a, s.b) && Together.Can(s.c, s.a))
            {
                var other = s.c == s.a ? s.b : s.a;
                OwnBetween.Say(s.c, H, "alike", g, other);
                var sa = OwnBetween.StanceOf(s.a, g);
                var sb = OwnBetween.StanceOf(s.b, g);
                var listener = s.c == s.b ? s.a : s.b;
                string reply = OwnBetween.Say(listener, H, OwnBetween.Doubts(listener) ? "doubt" : "agree", g, s.c);
                var o = OwnHappeningsMath.Mend(sa, sb);
                OwnBetween.Face(s.a, s.b, 400);
                OwnBetween.End(H, o, s.a, s.b, OwnBetween.HeldBy(s.a), OwnBetween.HeldBy(s.b));
                OwnBetween.Card(H, g, map, s.a.Position, false, null, s.a.Named("A"), s.b.Named("B"));
                OwnBetween.Book(H, g, map, s.a.Position, GodActKind.Blessing, s.c, s.a.LabelShort.Named("OTHER"), s.b.LabelShort.Named("THIRD"));
                if (OwnBetween.Doubts(listener)) OwnBetween.Doubted(listener, g, reply);
                s.stage = -1;
                return;
            }
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var (x, y, speaker) = Scene(map, g);
            if (x == null)
            {
                var three = OwnBetween.People(map).Where(OwnBetween.Grown).Take(3).ToList();
                if (three.Count < 2) return "two grown of the colony are needed.";
                x = three[0];
                y = three[1];
                speaker = three.Count > 2 ? three[2] : x;
            }
            Begin(b, g, map, x, y, speaker);
            return "the noon light on " + x.LabelShort + " and " + y.LabelShort + "; " + speaker.LabelShort + " speaks.";
        }
    }

    /// <summary>
    /// Called into the shade (OwnHappening.HeliosShade): on a hot, clear noon one of the colony works on out in the sun, and
    /// a friend walks over and calls them into the shade; the two sit there a while and come away closer. He never means to
    /// burn anyone, his believers say; he only gives too much.
    /// </summary>
    public class HeliosShade : BetweenKind
    {
        public override OwnHappening H => OwnHappening.HeliosShade;

        public static (Pawn worker, Pawn friend, IntVec3 shade) Scene(Map map)
        {
            var people = OwnBetween.People(map).Where(OwnBetween.Grown).ToList();
            foreach (var w in people.Where(p => !p.Position.Roofed(map) && p.CurJob != null && p.CurJob.def != JobDefOf.Wait_MaintainPosture && p.CurJob.def.joyKind == null && !p.InBed()))
            {
                var friend = people.Where(p => p != w && OwnBetween.FreeNow(p) && OwnBetween.Opinion(p, w) >= OwnHappeningsMath.ShadeFriend).OrderByDescending(p => OwnBetween.Opinion(p, w)).FirstOrDefault();
                if (friend == null) continue;
                var shade = GenRadial.RadialCellsAround(w.Position, OwnHappeningsMath.ShadeWithin, true)
                    .FirstOrDefault(c => c.InBounds(map) && c.Roofed(map) && c.Standable(map) && c.GetFirstBuilding(map) == null && w.CanReach(c, PathEndMode.OnCell, Danger.Some));
                if (shade.IsValid) return (w, friend, shade);
            }
            return (null, null, IntVec3.Invalid);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < OwnHappeningsMath.NoonFrom || hour > 16 || !OwnHappenings.Clear(map) || map.mapTemperature.OutdoorTemp < OwnHappeningsMath.ShadeFrom) return;
            var (w, friend, shade) = Scene(map);
            if (w == null || !OwnHappenings.Calls(b, H, GodPaceMath.Trigger.Ordinary, g, map, now)) return;
            Begin(b, g, map, w, friend, shade);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Pawn w, Pawn friend, IntVec3 shade)
        {
            if (!OwnBetween.Send(friend, map, w.Position, 1f, 2)) return false;
            var s = OwnBetween.Begin(b, OwnHappening.HeliosShade, g, map, shade, w, friend, 3f);
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
                if (s.b.Position.InHorDistOf(s.a.Position, 4f) && s.b.CurJobDef != JobDefOf.Goto && Together.Can(s.a, s.b))
                {
                    OwnBetween.Say(s.b, H, "shade", g, s.a);
                    OwnBetween.Send(s.a, map, s.at, 1f, 1);
                    OwnBetween.Send(s.b, map, s.at, 1f, 2);
                    s.stage = 2;
                }
                else if (s.b.CurJobDef != JobDefOf.Goto) OwnBetween.Send(s.b, map, s.a.Position, 1f, 2);
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 120);
                return;
            }
            if (OwnBetween.There(s.a, s.at) && OwnBetween.There(s.b, s.at) && Together.Can(s.a, s.b))
            {
                OwnBetween.Face(s.a, s.b, 2000);
                OwnBetween.Say(s.a, H, OwnBetween.Holds(s.a, g) || OwnBetween.Holds(s.b, g) ? "sun" : "thanks", g, s.b);
                OwnBetween.End(H, BetweenOutcome.Closer, s.a, s.b);
                OwnBetween.Card(H, g, map, s.at, false, null, s.a.Named("A"), s.b.Named("B"));
                OwnBetween.Book(H, g, map, s.at, GodActKind.Act, s.b, s.a.LabelShort.Named("OTHER"));
                s.stage = -1;
                return;
            }
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 120);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var (w, friend, shade) = Scene(map);
            if (w == null)
            {
                var two = OwnBetween.People(map).Where(OwnBetween.Grown).Take(2).ToList();
                if (two.Count < 2) return "two grown of the colony are needed.";
                w = two[0];
                friend = two[1];
                shade = GenRadial.RadialCellsAround(w.Position, 40f, true).FirstOrDefault(c => c.InBounds(map) && c.Roofed(map) && c.Standable(map) && c.GetFirstBuilding(map) == null);
            }
            if (!shade.IsValid) return "no shade (a roof) near them.";
            return Begin(b, g, map, w, friend, shade) ? friend.LabelShort + " goes to call " + w.LabelShort + " into the shade." : "they can't reach each other.";
        }
    }
}
