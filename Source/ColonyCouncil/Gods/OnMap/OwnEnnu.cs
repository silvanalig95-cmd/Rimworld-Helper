using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Ennu, the Guest, on the map (docs/gods-next-proposals.md, C 5): guests, the colony's name among travellers, stories.
    /// <list type="bullet">
    /// <item>The small test: while the stranger at the gate stays (GodStranger), it asks one small thing, a different one each
    /// time: a story by the fire in the evening, the name of whoever cooked, to be shown the graves, a coat in the cold. Someone
    /// of the colony does it on the map (walks over and tells the story, says their name, walks them to the graves, carries a
    /// coat from the stores), and it counts toward the welcome the stranger judges (StrangerStay.Credit).</item>
    /// <item>Tales by the fire: in the evenings of its stay it sits by the colony's fire and tells a piece of another god's
    /// story to whoever sits near (GodTales.Tell): it carries news between the gods.</item>
    /// <item>The bowl set out: on the Open Door night (its festival), or in the days after a kind welcome, the cook sets a bowl
    /// on the table at dusk; by morning it is empty, and the cook thanked by name in chalk on the table.</item>
    /// <item>The kindest carries something: for a season after the Guest found someone kindest, visitors seek them out and tell
    /// them the road's news.</item>
    /// </list>
    /// </summary>
    public static class OwnEnnu
    {
        public const string Key = "Ennu";
        public const string ThankedThought = "Rimfolk_Thought_GodOwnThanked";
        public const string BowlThought = "Rimfolk_Thought_GodOwnBowl";

        private static God It => OwnHappenings.God(Key);

        private static int Now => OwnHappenings.Now;

        /// <summary>The stranger staying now, when it is the Guest (a god that comes as a stranger), else null.</summary>
        private static StrangerStay Stay(out Pawn x)
        {
            x = null;
            var s = GodsOnMapBook.Instance?.stranger;
            if (s?.x == null || s.phase != StrangerPhase.Staying || !s.x.Spawned || s.x.Dead || s.x.Downed || s.map == null) return null;
            x = s.x;
            return s;
        }

        /// <summary>The colony's fire: a campfire, a hearth, a brazier lit, else any burning heater that gives light.</summary>
        internal static Thing Fire(Map map) =>
            map.listerBuildings.allBuildingsColonist.Where(t => t.Spawned && t.GetComp<CompGlower>()?.Glows == true && t.GetComp<CompRefuelable>() is CompRefuelable r && r.HasFuel)
                .OrderByDescending(t => t.def.defName.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0 || t.def.defName.IndexOf("hearth", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0)
                .ThenByDescending(t => t.GetComp<CompHeatPusher>() != null ? 1 : 0).FirstOrDefault();

        /// <summary>The colony's best cook at home, or null.</summary>
        private static Pawn Cook(Map map) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => People.HasInnerLife(p) && p.skills?.GetSkill(SkillDefOf.Cooking) is SkillRecord r && !r.TotallyDisabled)
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Cooking).Level).FirstOrDefault();

        /// <summary>One of the colony free to do something kind (grown, awake, not busy with anything that matters), the best at <paramref name="skill"/> first.</summary>
        private static Pawn Kind(Map map, SkillDef skill, Pawn not = null) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => p != not && GodOmens.Awake(p) && !p.DevelopmentalStage.Juvenile() && (p.CurJob == null || p.CurJob.def.casualInterruptible || Together.FreeTime(p)))
                .OrderByDescending(p => skill == null ? 0 : p.skills?.GetSkill(skill)?.Level ?? 0).FirstOrDefault();

        // ================= The small test =================

        /// <summary>The hour of its stay: a test asked once, its answer played out; the tales by the fire in the evening.</summary>
        private static void StayHour(OwnHappeningsBook b, int now)
        {
            var s = Stay(out var x);
            if (s == null) return;
            var g = s.God;
            var map = s.map;
            if (b.testOf != x)
            {
                b.testOf = x;
                b.testStage = 0;
                b.testBy = null;
                b.testThing = null;
                b.talesOf = x;
                b.talesTold = 0;
            }
            int hour = OwnHappenings.Hour(map);
            if (b.testStage == 0 && now - s.since > GenDate.TicksPerHour * 3 && hour >= 9 && hour <= 19 && OwnHappenings.Quiet(map)) Ask(b, s, x, now);
            else if (b.testStage == 1) Answer(b, s, x, now);
            else if (b.testStage == 2 && now > b.testUntil) b.testStage = 4;
            if (hour >= OwnHappeningsMath.EveningFrom && hour < OwnHappeningsMath.EveningTo) Tales(b, s, x, g, map, now);
        }

        /// <summary>It asks its small thing: aloud, and in a line of the news.</summary>
        internal static bool Ask(OwnHappeningsBook b, StrangerStay s, Pawn x, int now)
        {
            var map = s.map;
            var g = s.God;
            bool cold = map.mapTemperature.OutdoorTemp < OwnHappeningsMath.CoatBelow && PlayCarry.Stock(map, PlayCarry.Coat, null).Any();
            var tests = OwnHappeningsMath.Tests(cold, Fire(map) != null, MogsWays.Tombs(map).Count > 0, Cook(map) != null && s.ate > 0);
            if (tests.Count == 0)
            {
                b.testStage = 4;
                return false;
            }
            var recent = GodPace.Forms("own" + OwnHappening.EnnuTest);
            int pick = GodPaceMath.Pick(tests.Select(t => t.ToString()).ToList(), tests.Select(_ => 1f).ToList(), recent, Rand.Value);
            if (pick < 0) return false;
            b.test = tests[pick];
            b.testStage = 1;
            b.testUntil = now + Mathf.RoundToInt(OwnHappeningsMath.TestHours * GenDate.TicksPerHour);
            OwnHappenings.Came(b, OwnHappening.EnnuTest, now, b.test.ToString());
            string what = ("CC_GodO_Test_" + b.test).Translate().Resolve();
            OwnHappenings.Say(x, "testask,test:" + b.test.ToString().ToLowerInvariant(), g, Together.Company(x));
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_GodO_TestAsked_Text".Translate(GodStranger.Traveller(GodStranger.Age).Named("TRAVELLER"), what.Named("WHAT")).Resolve(),
                new LookTargets(x), MessageTypeDefOf.NeutralEvent);
            return true;
        }

        /// <summary>Someone of the colony answers it on the map: walks over and does the thing asked.</summary>
        private static void Answer(OwnHappeningsBook b, StrangerStay s, Pawn x, int now)
        {
            var map = s.map;
            if (now > b.testUntil)
            {
                b.testStage = 4;
                return;
            }
            int hour = OwnHappenings.Hour(map);
            switch (b.test)
            {
                case GuestTest.Story:
                {
                    if (hour < OwnHappeningsMath.EveningFrom || hour >= OwnHappeningsMath.EveningTo) return;
                    var fire = Fire(map);
                    var by = Kind(map, SkillDefOf.Social);
                    if (fire == null || by == null) return;
                    var near = CellFinder.StandableCellNear(fire.Position, map, 2.5f);
                    OwnHappenings.GoTo(x, near, GenDate.TicksPerHour);
                    Meet(b, s, x, by, now);
                    return;
                }
                case GuestTest.Cook:
                {
                    var cook = Cook(map);
                    if (cook == null || !GodOmens.Awake(cook)) return;
                    Meet(b, s, x, cook, now);
                    return;
                }
                case GuestTest.Graves:
                {
                    var by = Kind(map, SkillDefOf.Social);
                    if (by == null) return;
                    Meet(b, s, x, by, now);
                    return;
                }
                case GuestTest.Coat:
                {
                    var by = Kind(map, null);
                    var coat = PlayCarry.Stock(map, PlayCarry.Coat, null).OrderBy(t => t.MarketValue).FirstOrDefault(t => by != null && by.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some));
                    if (by == null || coat == null) return;
                    var to = CellFinder.StandableCellNear(x.Position, map, 2f);
                    if (!to.IsValid || !OwnHappenings.Carry(by, coat, to, 1)) return;
                    OwnHappenings.Stand(x, by.Position, GenDate.TicksPerHour * 2);
                    b.testBy = by;
                    b.testThing = coat;
                    b.testStage = 2;
                    b.Due(now + 250);
                    return;
                }
            }
        }

        /// <summary>The one answering goes to the stranger (Together.When, seeking them now), and the thing is done when they are together.</summary>
        private static void Meet(OwnHappeningsBook b, StrangerStay s, Pawn x, Pawn by, int now)
        {
            b.testBy = by;
            b.testStage = 2;
            var test = b.test;
            Together.When("own-ennu-test|" + x.thingIDNumber, by, x, () => Done(by, x, test), OwnHappeningsMath.TestHours, 0f);
        }

        /// <summary>Together now: the story told, the name said, the graves walked to; the stranger thanks them, and it counts.</summary>
        private static void Done(Pawn by, Pawn x, GuestTest test)
        {
            var b = OwnHappenings.Book;
            var s = GodsOnMapBook.Instance?.stranger;
            if (b == null || s?.x != x || b.testStage != 2) return;
            var g = s.God;
            var map = s.map;
            // together: Together.When brought the one answering to the stranger's side.
            switch (test)
            {
                case GuestTest.Story:
                    OwnHappenings.Say(by, "teststory", g, x);
                    break;
                case GuestTest.Cook:
                    OwnHappenings.Say(by, "testcook", g, x);
                    break;
                case GuestTest.Graves:
                {
                    OwnHappenings.Say(by, "testgraves", g, x);
                    var tombs = MogsWays.Tombs(map, true);
                    if (tombs.Count > 0)
                    {
                        var at = CellFinder.StandableCellNear(MogsWays.Middle(tombs), map, 3f);
                        OwnHappenings.GoTo(by, at);
                        OwnHappenings.GoTo(x, CellFinder.StandableCellNear(at, map, 2f));
                        b.testThing = null;
                        b.testStage = 3;
                        b.testUntil = Now + GenDate.TicksPerHour * 3;
                        b.Due(Now + 250);
                        return;
                    }
                    break;
                }
            }
            Passed(b, s, x, by, test);
        }

        /// <summary>The test passed: the stranger thanks them by name, it counts toward the welcome, and the colony's book keeps it.</summary>
        private static void Passed(OwnHappeningsBook b, StrangerStay s, Pawn x, Pawn by, GuestTest test)
        {
            b.testStage = 4;
            s.Credit(by, OwnHappeningsMath.TestCredit);
            s.kindnesses++;
            var g = s.God;
            // together: the stranger thanks whoever just did the thing at its side.
            OwnHappenings.Say(x, "testthanks,test:" + test.ToString().ToLowerInvariant(), g, by, by.Named("OTHER"));
            OwnHappenings.Memory(by, ThankedThought);
            OwnHappenings.Saw(g, s.map, x.Position, GodActKind.Act, by, OwnHappenings.K("Saw_Test" + test), GodStranger.Traveller(GodStranger.Age).Named("TRAVELLER"));
        }

        /// <summary>The test's steps: the coat set down beside the stranger and put on; the walk to the graves.</summary>
        private static void TestStep(OwnHappeningsBook b, int now)
        {
            if (b.testStage != 2 && b.testStage != 3) return;
            var s = Stay(out var x);
            if (s == null || b.testOf != x || now > b.testUntil)
            {
                if (b.testStage >= 2) b.testStage = 4;
                return;
            }
            var by = b.testBy;
            if (b.testStage == 3)
            {
                var tombs = MogsWays.Tombs(s.map, true);
                if (tombs.Count == 0 || by == null)
                {
                    b.testStage = 4;
                    return;
                }
                var mid = MogsWays.Middle(tombs);
                if (x.Position.InHorDistOf(mid, 5f))
                {
                    OwnHappenings.Stand(x, mid, 600);
                    OwnHappenings.Stand(by, mid, 600);
                    Passed(b, s, x, by, GuestTest.Graves);
                    return;
                }
                b.Due(now + 250);
                return;
            }
            if (b.test != GuestTest.Coat) return;
            var coat = b.testThing as Apparel;
            if (coat == null || coat.Destroyed)
            {
                b.testStage = 4;
                return;
            }
            if (coat.Spawned && !coat.IsInAnyStorage() && coat.Position.InHorDistOf(x.Position, 3f) && (by == null || by.CurJobDef != JobDefOf.HaulToCell))
            {
                try
                {
                    coat.DeSpawn();
                    x.apparel.Wear(coat, true);
                    FleckMaker.ThrowDustPuff(x.Position, x.Map, 0.6f);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: the coat for the stranger", e);
                    b.testStage = 4;
                    return;
                }
                if (by != null) Passed(b, s, x, by, GuestTest.Coat);
                else b.testStage = 4;
                return;
            }
            b.Due(now + 250);
        }

        // ================= Tales by the fire =================

        /// <summary>An evening of its stay: by the fire, a piece of another god's story to whoever sits near.</summary>
        private static void Tales(OwnHappeningsBook b, StrangerStay s, Pawn x, God g, Map map, int now)
        {
            if (b.talesTold >= OwnHappeningsMath.TalesAStay || b.testStage == 2 && b.test == GuestTest.Story || !x.Awake() || x.InMentalState) return;
            var fire = Fire(map);
            if (fire != null && !x.Position.InHorDistOf(fire.Position, 5f))
            {
                if (Rand.Chance(OwnHappeningsMath.TalesChance)) OwnHappenings.GoTo(x, CellFinder.StandableCellNear(fire.Position, map, 2.5f), GenDate.TicksPerHour);
                return;
            }
            var listener = Together.Company(x);
            if (listener == null) return;
            TellTale(b, x, listener, g, now);
        }

        /// <summary>It tells the next piece of a story of a god not its own, as a traveller tells it; the colony's book keeps it as heard at the fire.</summary>
        internal static bool TellTale(OwnHappeningsBook b, Pawn x, Pawn listener, God g, int now)
        {
            var others = Gods.All.Where(o => o != null && o != g && o.kind != GodKind.Dead).Select(o => o.Key).ToList();
            if (others.Count == 0) return false;
            // together: the listener is beside it at the fire (Together.Company).
            if (!GodTales.Tell(x, listener, others, "Fire")) return false;
            b.talesTold++;
            OwnHappenings.Came(b, OwnHappening.EnnuTales, now, null, x.ThingID);
            if (x.Spawned) OwnHappenings.Stand(x, listener.Position, 600);
            return true;
        }

        // ================= The bowl set out =================

        /// <summary>The Open Door kept today at a home (Festivals.Kept): tonight a bowl may be set out.</summary>
        public static void OpenDoor(Map map)
        {
            var b = OwnHappenings.Book;
            if (b == null || map == null) return;
            b.openDoorMap = map;
            b.openDoorAt = Now;
        }

        /// <summary>At dusk: the cook carries a meal to the table for whoever comes in the night.</summary>
        internal static bool Bowl(OwnHappeningsBook b, God g, Map map, int now)
        {
            var cook = Kind(map, SkillDefOf.Cooking);
            var table = map.listerBuildings.allBuildingsColonist.Where(t => t.Spawned && t.def.surfaceType == SurfaceType.Eat && map.areaManager.Home[t.Position] && t.Position.GetFirstItem(map) == null)
                .OrderBy(t => cook == null ? 0 : t.Position.DistanceToSquared(cook.Position)).FirstOrDefault();
            var meal = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.Spawned && t.def.IsNutritionGivingIngestible && t.def.ingestible.IsMeal && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer)
                            && cook != null && cook.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some))
                .OrderBy(t => t.MarketValue).FirstOrDefault();
            if (cook == null || table == null || meal == null || !OwnHappenings.Carry(cook, meal, table.Position, 1)) return false;
            b.bowlMap = map;
            b.bowlTable = table;
            b.bowlCook = cook;
            b.bowl = meal;
            b.bowlStage = 1;
            b.bowlUntil = now + GenDate.TicksPerHour * 4;
            b.Due(now + 250);
            OwnHappenings.Came(b, OwnHappening.EnnuBowl, now);
            return true;
        }

        /// <summary>The meal set down on the table: left there (forbidden to the colony's own) for the night.</summary>
        private static void BowlStep(OwnHappeningsBook b, int now)
        {
            if (b.bowlStage != 1) return;
            var map = b.bowlMap;
            var table = b.bowlTable;
            if (map == null || table == null || table.Destroyed || now > b.bowlUntil)
            {
                b.bowlStage = 0;
                return;
            }
            var set = table.Position.GetThingList(map).FirstOrDefault(t => t.def.IsNutritionGivingIngestible && t.def.ingestible.IsMeal && !t.IsInAnyStorage());
            if (set == null)
            {
                b.Due(now + 250);
                return;
            }
            set.SetForbidden(true, false);
            b.bowl = set;
            b.bowlStage = 2;
            var g = It;
            OwnHappenings.Say(b.bowlCook, "bowlset", g);
            OwnHappenings.Saw(g, map, table.Position, GodActKind.Act, b.bowlCook, "CC_GodO_Saw_BowlSet");
        }

        /// <summary>At dawn: the bowl empty, the cook thanked by name in chalk on the table (seen as it is found).</summary>
        internal static bool Morning(OwnHappeningsBook b, int now)
        {
            var map = b.bowlMap;
            var table = b.bowlTable;
            var g = It;
            b.bowlStage = 0;
            if (map == null || table == null || table.Destroyed || g == null) return false;
            var meal = b.bowl;
            if (meal == null || meal.Destroyed || !meal.Spawned || meal.Position != table.Position) return false;
            meal.Destroy();
            string cook = b.bowlCook?.LabelShort ?? "";
            string chalk = "CC_GodO_Bowl_Chalk".Translate(cook.Named("COOK")).Resolve();
            try
            {
                // The chalk on the table: a pale dust where it was written (its words are on the card and in the finder's mouth).
                for (int i = 0; i < 4; i++)
                    FleckMaker.ThrowDustPuffThick(table.DrawPos + new Vector3(Rand.Range(-0.4f, 0.4f), 0f, Rand.Range(-0.3f, 0.3f)), map, 0.5f, new Color(0.95f, 0.95f, 0.9f, 0.85f));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: chalk on the table", e);
            }
            if (b.bowlCook != null) OwnHappenings.Memory(b.bowlCook, BowlThought);
            OwnHappenings.Card(g, map, "Bowl", true, new LookTargets(table), LetterDefOf.PositiveEvent, Importance.Notable, cook.Named("COOK"), chalk.Named("CHALK"));
            var seer = OwnHappenings.Found(b, map, table.Position, "bowl", g, cook);
            OwnHappenings.Saw(g, map, table.Position, GodActKind.Blessing, seer, "CC_GodO_Saw_Bowl", cook.Named("COOK"));
            return true;
        }

        // ================= The kindest carries something =================

        /// <summary>The stranger's stay judged (GodGifts.Outcome): a kind welcome remembered, and its kindest marked for the road's people.</summary>
        public static void Judged(God god, Map map, Treatment t, Pawn kindest)
        {
            var b = OwnHappenings.Book;
            if (b == null || god == null || god.Key != Key || map == null) return;
            if (t != Treatment.Kind && t != Treatment.Generous) return;
            b.kindWelcome = Now;
            if (kindest == null || kindest.Dead) return;
            b.kindest = kindest;
            b.kindestUntil = Now + Mathf.RoundToInt(OwnHappeningsMath.KindestDays * GenDate.TicksPerDay);
        }

        /// <summary>The hour: a visitor on the land seeks out the Guest's kindest and tells them the road's news.</summary>
        private static void Kindest(OwnHappeningsBook b, int now)
        {
            var k = b.kindest;
            if (k == null || now > b.kindestUntil || k.Dead)
            {
                b.kindest = null;
                return;
            }
            if (!k.Spawned || !Homes.IsHome(k.Map) || !GodOmens.Awake(k) || b.newsBy != null) return;
            var map = k.Map;
            var g = It;
            var stranger = GodsOnMapBook.Instance?.stranger?.x;
            var visitor = map.mapPawns.AllPawnsSpawned.Where(x => x.RaceProps?.Humanlike == true && x != stranger && x.Faction != null && !x.Faction.IsPlayer && !x.HostileTo(Faction.OfPlayer)
                                                                && !x.IsPrisoner && !x.Downed && !x.InMentalState && x.Awake() && !GodAvatar.Is(x) && x.CanReach(k, PathEndMode.Touch, Danger.Some))
                .OrderBy(x => x.Position.DistanceToSquared(k.Position)).FirstOrDefault();
            if (visitor == null || g == null) return;
            if (OwnHappenings.forced != OwnHappening.EnnuKindest
                && (!OwnHappeningsMath.Free(OwnHappening.EnnuKindest, OwnHappenings.DaysSince(b, OwnHappening.EnnuKindest, now)) || !Rand.Chance(OwnHappeningsMath.KindestChance)))
                return;
            Seek(b, visitor, k, now);
        }

        internal static bool Seek(OwnHappeningsBook b, Pawn visitor, Pawn k, int now)
        {
            var near = CellFinder.StandableCellNear(k.Position, k.Map, 2f);
            if (!OwnHappenings.GoTo(visitor, near, GenDate.TicksPerHour)) return false;
            b.newsBy = visitor;
            b.newsTo = k;
            b.newsUntil = now + GenDate.TicksPerHour * 2;
            b.Due(now + 120);
            OwnHappenings.Came(b, OwnHappening.EnnuKindest, now, null, k.ThingID);
            return true;
        }

        /// <summary>The visitor beside them: the road's news told, and they say what they heard.</summary>
        private static void NewsStep(OwnHappeningsBook b, int now)
        {
            var v = b.newsBy;
            var k = b.newsTo;
            if (v == null) return;
            if (k == null || v.Dead || !v.Spawned || k.Dead || !k.Spawned || v.Map != k.Map || now > b.newsUntil)
            {
                b.newsBy = b.newsTo = null;
                return;
            }
            if (!v.Position.InHorDistOf(k.Position, 3f))
            {
                if (v.CurJobDef != JobDefOf.Goto) OwnHappenings.GoTo(v, CellFinder.StandableCellNear(k.Position, k.Map, 2f), GenDate.TicksPerHour);
                b.Due(now + 120);
                return;
            }
            b.newsBy = b.newsTo = null;
            var g = It;
            string people = v.Faction?.Name ?? "";
            OwnHappenings.Stand(v, k.Position, 500);
            OwnHappenings.Stand(k, v.Position, 500);
            // together: the visitor walked over to them and stands beside them now.
            OwnHappenings.Say(v, "kindestnews", g, k, k.Named("OTHER"));
            OwnHappenings.Say(k, "kindestheard", g, v, people.Named("PEOPLE"));
            OwnHappenings.Saw(g, k.Map, k.Position, GodActKind.Act, k, "CC_GodO_Saw_Kindest", people.Named("PEOPLE"));
        }

        // ================= The day, the hour, the steps =================

        internal static void Daily(OwnHappeningsBook b, int now)
        {
        }

        internal static void Hourly(OwnHappeningsBook b, int now)
        {
            var g = It;
            if (g == null) return;
            StayHour(b, now);
            Kindest(b, now);
            if (b.bowlStage == 2 && b.bowlMap != null)
            {
                int h = OwnHappenings.Hour(b.bowlMap);
                if (h >= 4 && h <= 7) Morning(b, now);
                else if (now > b.Last(OwnHappening.EnnuBowl) + GenDate.TicksPerDay) b.bowlStage = 0;
            }
            if (b.bowlStage != 0) return;
            foreach (var map in OwnHappenings.HomeMaps)
            {
                int h = OwnHappenings.Hour(map);
                if (h < OwnHappeningsMath.EveningFrom || h > OwnHappeningsMath.EveningFrom + 2 || !OwnHappenings.Quiet(map) || !OwnHappenings.Acts(g, map)) continue;
                bool openDoor = b.openDoorMap == map && now - b.openDoorAt < GenDate.TicksPerDay;
                var why = OwnHappeningsMath.BowlTrigger(openDoor, GodPace.DaysSince(b.kindWelcome, now));
                if (OwnHappenings.forced != OwnHappening.EnnuBowl
                    && (!OwnHappeningsMath.Free(OwnHappening.EnnuBowl, OwnHappenings.DaysSince(b, OwnHappening.EnnuBowl, now)) || !Rand.Chance(GodPaceMath.PerCheck(OwnHappeningsMath.BowlChance(why), 3f))))
                    continue;
                if (Bowl(b, g, map, now)) return;
            }
        }

        internal static void Step(OwnHappeningsBook b, int now)
        {
            TestStep(b, now);
            BowlStep(b, now);
            NewsStep(b, now);
        }
    }
}
