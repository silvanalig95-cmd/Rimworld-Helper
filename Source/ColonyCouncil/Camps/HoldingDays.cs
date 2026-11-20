using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>The neighbours for good at the colony's days, and the colony's friend at theirs (HoldingDays).</summary>
    public partial class Camp
    {
        /// <summary>A day of the colony's kept with a gathering: the neighbours come to it until this tick (-1: none due), and its name.</summary>
        public int dayGuestUntil = -1;
        public string dayGuestLabel;
        /// <summary>The last of the colony's dead whose grave they came to (a corpse's id), so each is come to once.</summary>
        public int lastGraveSeen = -1;
        /// <summary>Where an errand of theirs goes when it goes to one place ("spot": the grave they come to).</summary>
        public IntVec3 errandSpot = IntVec3.Invalid;
        /// <summary>The last day of theirs the colony's friend was asked to (a day's number), so a day asks once.</summary>
        public int lastGoodDay = -1;

        private void ExposeHoldingDays()
        {
            Scribe_Values.Look(ref dayGuestUntil, "dayGuestUntil", -1);
            Scribe_Values.Look(ref dayGuestLabel, "dayGuestLabel");
            Scribe_Values.Look(ref lastGraveSeen, "lastGraveSeen", -1);
            Scribe_Values.Look(ref errandSpot, "errandSpot", IntVec3.Invalid);
            Scribe_Values.Look(ref lastGoodDay, "lastGoodDay", -1);
        }
    }

    /// <summary>
    /// The neighbours for good keep the colony's days (the content update's B22, 20 November 2026). The family settled for
    /// good at the edge of the land (Camps/Holding.cs) walks over for the colony's days: a day of the calendar kept with a
    /// feast, a bonfire or games, or a custom kept with a gathering (harvest home, the midsummer bonfire): up to three of
    /// them come to the gathering as its guests, and one sets down a dish from their stores; for a funeral (one of the
    /// colony's dead buried in the last two days) two of them come to the grave and stand there a while. On their own good
    /// days (the day they settled, each year; a birthday of one of theirs; a wedding in the family, when two of theirs who
    /// are free marry) they ask the colony's nearest friend over to their fire for the evening. Only while they are at ease
    /// with the colony. The rules are HoldingDaysMath's; what is due is kept on the holding (Camp: saved).
    /// </summary>
    public static class HoldingDays
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The neighbours for good on this map who could come: settled, at peace and at ease with the colony.</summary>
        public static Camp Neighbours(Map map)
        {
            var c = CampTracker.HoldingAt(map);
            return c != null && c.holding && c.CanTalk && c.Settled && !c.Hostile && HoldingDaysMath.AtEase(c.tension) ? c : null;
        }

        private static List<Pawn> Grown(Camp c) => c.Here.Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.InMentalState && p.Awake()).ToList();

        // ================= The colony's days =================

        /// <summary>A day of the calendar kept (Calendar/CalendarKeeping.cs): one kept with a gathering brings the neighbours to it.</summary>
        public static void DayKept(CalendarDay d, Map map)
        {
            if (d == null || map == null) return;
            if (d.way != DayWay.Feast && d.way != DayWay.Bonfire && d.way != DayWay.Games && d.kind != CalendarKind.Harvest) return;
            Expect(map, d.label);
        }

        /// <summary>A custom kept by the council's answer (Dilemmas/Dilemmas.cs): one whose answer gathers the colony brings the neighbours to it.</summary>
        public static void CustomKept(DilemmaContext ctx, int option)
        {
            var o = ctx?.def?.options != null && option >= 0 && option < ctx.def.options.Count ? ctx.def.options[option] : null;
            if (o?.effects == null || !o.effects.Any(e => e is DilemmaEffect_Gathering)) return;
            Expect(ctx.map, ctx.def.traditionLabel.NullOrEmpty() ? ctx.def.label : ctx.def.traditionLabel);
        }

        private static void Expect(Map map, string label)
        {
            var c = Neighbours(map);
            if (c == null) return;
            c.dayGuestUntil = Now + 18 * GenDate.TicksPerHour;
            c.dayGuestLabel = label;
        }

        /// <summary>
        /// The hour (Peoples/PeoplesTies.cs): a day due finds its gathering under way and the neighbours join it as its
        /// guests, one setting down a dish; a fresh grave of the colony's brings two of them to it.
        /// </summary>
        public static void Hourly(int now)
        {
            foreach (var map in Homes.All)
            {
                var c = Neighbours(map);
                if (c == null) continue;
                try
                {
                    if (c.dayGuestUntil >= 0)
                    {
                        if (now > c.dayGuestUntil) c.dayGuestUntil = -1;
                        else if (JoinGathering(c, map)) c.dayGuestUntil = -1;
                    }
                    if (!c.ErrandSet) ToTheGrave(c, map, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("the neighbours' days, the hour", e);
                }
            }
        }

        /// <summary>The colony's gathering under way on the map (a feast, a fire, a party), and its group; false when none is.</summary>
        private static bool Gathering(Map map, out LordJob_PlayOut job, out IntVec3 spot)
        {
            job = null;
            spot = IntVec3.Invalid;
            foreach (var l in map.lordManager.lords)
            {
                if (!(l?.LordJob is LordJob_PlayOut j)) continue;
                var p = PlayOutTracker.Instance?.Get(j.playId);
                if (p == null || p.state != PlayState.Running) continue;
                if (p.kind != PlayKind.Feast && p.kind != PlayKind.Bonfire && p.kind != PlayKind.Gathering) continue;
                job = j;
                spot = j.Spot;
                return spot.IsValid;
            }
            return false;
        }

        internal static bool JoinGathering(Camp c, Map map)
        {
            if (!Gathering(map, out var job, out var spot)) return false;
            var come = Grown(c).Take(HoldingDaysMath.Guests(Grown(c).Count)).ToList();
            if (come.Count == 0) return true;
            foreach (var p in come)
                if (!job.guests.Contains(p)) job.guests.Add(p);
            // A dish from their stores, set down by the gathering.
            int meals = Mathf.Min(come.Count, Mathf.Max(1, c.provisions));
            var dish = ThingMaker.MakeThing(ThingDefOf.MealSimple);
            dish.stackCount = meals;
            var at = PlaySpots.FreeCellNear(spot, map, come[0]);
            if (at.IsValid && GenPlace.TryPlaceThing(dish, at, map, ThingPlaceMode.Near)) c.provisions = Mathf.Max(0, c.provisions - meals);
            else dish = null;
            string text = (dish != null ? "CC_HoldingDay_Came" : "CC_HoldingDay_CameNoDish").Translate(c.HoldingName.Named("NAME"), (c.dayGuestLabel ?? "").Named("DAY"),
                TextUtil.JoinAnd(come.Select(p => p.LabelShort).ToList()).Named("WHO"), meals.Named("COUNT")).Resolve();
            Tell.Message(NewsKind.Colony, Importance.Notable, map, text, new LookTargets(come), MessageTypeDefOf.PositiveEvent);
            Speech.Say(come[0], "CC_HoldingDay_CameLine".Translate((c.dayGuestLabel ?? "").Named("DAY")).Resolve());
            c.Warm(come[0], 1);
            return true;
        }

        /// <summary>One of the colony's dead buried in the last two days: two of them come to stand at the grave.</summary>
        internal static void ToTheGrave(Camp c, Map map, int now, bool anyGrave = false)
        {
            Building_Grave grave = null;
            foreach (var g in map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>())
            {
                var corpse = g.Corpse;
                var inner = corpse?.InnerPawn;
                if (inner == null || inner.Faction != Faction.OfPlayer || !inner.RaceProps.Humanlike || corpse.thingIDNumber == c.lastGraveSeen) continue;
                if (!anyGrave && !HoldingDaysMath.Fresh(corpse.timeOfDeath, now)) continue;
                grave = g;
                break;
            }
            if (grave == null) return;
            c.lastGraveSeen = grave.Corpse.thingIDNumber;
            c.errandSpot = grave.Position;
            string dead = grave.Corpse.InnerPawn.LabelShort;
            var errand = new DilemmaEffect_CampComes
            {
                to = "spot", count = 2, hours = new FloatRange(1.5f, 2.5f), when = new IntRange(8, 19), doing = "mourn",
                lines = new List<string>
                {
                    "CC_HoldingDay_GraveLine1".Translate(dead.Named("DEAD")).Resolve(), "CC_HoldingDay_GraveLine2".Translate(dead.Named("DEAD")).Resolve(),
                    "CC_HoldingDay_GraveLine3".Translate(dead.Named("DEAD")).Resolve()
                }
            };
            c.StartErrand(errand, null);
        }

        // ================= Their own days =================

        /// <summary>
        /// The day (Peoples/PeoplesTies.cs): a good day of theirs (the day they settled, a birthday of one of theirs, a
        /// wedding in the family) asks the colony's nearest friend over to their fire for the evening.
        /// </summary>
        public static void Daily(int now)
        {
            foreach (var map in Homes.All)
            {
                var c = Neighbours(map);
                if (c == null) continue;
                try
                {
                    int day = GenDate.DaysPassed;
                    if (c.lastGoodDay == day) continue;
                    string why = GoodDay(c, map);
                    if (why == null) continue;
                    c.lastGoodDay = day;
                    Invite(c, map, why);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("the neighbours' own days", e);
                }
            }
        }

        /// <summary>What today is to them, in a few words, or null for an ordinary day. A wedding is made here when it comes.</summary>
        private static string GoodDay(Camp c, Map map)
        {
            int today = GenLocalDate.DayOfYear(map);
            int settledDay = c.settledTick >= 0 ? GenDate.DayOfYear(GenDate.TickGameToAbs(c.settledTick), Find.WorldGrid.LongLatOf(map.Tile).x) : -1;
            if (HoldingDaysMath.Anniversary(settledDay, today, c.DaysSettled)) return "CC_HoldingDay_Why_Settled".Translate(c.HoldingName.Named("NAME")).Resolve();
            foreach (var p in c.Here)
                if (p.ageTracker != null && p.ageTracker.BirthDayOfYear == today)
                    return "CC_HoldingDay_Why_Birthday".Translate(p.Named("PAWN")).Resolve();
            if (Rand.Chance(HoldingDaysMath.WeddingChance) && Wed(c, out var a, out var b))
                return "CC_HoldingDay_Why_Wedding".Translate(a.Named("A"), b.Named("B")).Resolve();
            return null;
        }

        /// <summary>Two of theirs who are grown, free of anyone and not close kin marry: the game's own spouses from today.</summary>
        private static bool Wed(Camp c, out Pawn a, out Pawn b)
        {
            a = b = null;
            var free = c.Here.Where(p => p.ageTracker != null && p.ageTracker.AgeBiologicalYears >= 18 && p.relations != null && !LovePartnerRelationUtility.HasAnyLovePartner(p)).ToList();
            foreach (var x in free)
                foreach (var y in free)
                {
                    if (x == y || x.gender == y.gender || x.thingIDNumber > y.thingIDNumber) continue;
                    if (x.relations.FamilyByBlood.Contains(y)) continue;
                    try
                    {
                        x.relations.AddDirectRelation(PawnRelationDefOf.Spouse, y);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("the neighbours' wedding", e);
                        return false;
                    }
                    a = x;
                    b = y;
                    return true;
                }
            return false;
        }

        /// <summary>The colony's nearest friend among them goes over to their fire for the evening.</summary>
        internal static void Invite(Camp c, Map map, string why)
        {
            var friend = c.warmth.Where(kv => KinMemory.Ours(kv.Key) && kv.Key.Spawned && kv.Key.Map == map && !kv.Key.Downed && kv.Value >= HoldingDaysMath.FriendWarmth)
                .OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault();
            if (friend == null) return;
            var visit = new DilemmaEffect_Visit
            {
                who = PlayWho.A, to = PlaySpot.Camp, label = "CC_HoldingDay_VisitLabel".Translate(c.HoldingName.Named("NAME")).Resolve(),
                hours = new IntRange(17, 22), duration = new FloatRange(2f, 3f)
            };
            PlayOut play;
            try
            {
                play = visit.Begin(new DilemmaContext { map = map, a = friend, stranger = c.leader });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the neighbours' evening", e);
                return;
            }
            if (play == null) return;
            // Their own fire: the holding's, never another camp's beside it.
            play.other = c.leader ?? c.Here.FirstOrDefault();
            c.Warm(friend, 1);
            Tell.Message(NewsKind.Colony, Importance.Notable, map,
                "CC_HoldingDay_Invite".Translate(c.HoldingName.Named("NAME"), why.Named("WHY"), friend.Named("PAWN")).Resolve(), friend, MessageTypeDefOf.PositiveEvent);
        }
    }
}
