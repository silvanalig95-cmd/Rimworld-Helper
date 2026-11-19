using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 78. A landing party (the Hollow Fleet) =====================

    /// <summary>The landing party's stay: the crew who came, the sleeper they brought, the places surveyed, the bells rung, and how it turned.</summary>
    public class PartyState : CampWorkState
    {
        public List<Pawn> crew = new List<Pawn>();
        public Pawn sleeper;
        public int surveyed, bellAt = -1;
        public bool sickRolled, sick, warned, deserted, fed;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref crew, "crew", LookMode.Reference);
            Scribe_References.Look(ref sleeper, "sleeper");
            Scribe_Values.Look(ref surveyed, "surveyed");
            Scribe_Values.Look(ref bellAt, "bellAt", -1);
            Scribe_Values.Look(ref sickRolled, "sickRolled");
            Scribe_Values.Look(ref sick, "sick");
            Scribe_Values.Look(ref warned, "warned");
            Scribe_Values.Look(ref deserted, "deserted");
            Scribe_Values.Look(ref fed, "fed");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                crew ??= new List<Pawn>();
                crew.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// A landing party (item 78): four to six of the Hollow Fleet's crew, set down beyond the colony's land by their shuttle, make a
    /// ship-shape camp by the colony for five to seven days: bunks in a row under one roof, the watch bell rung at every change of the
    /// watch, and by day they survey the land "for when the order comes", logging every place they walk. They buy a week's meals from the
    /// colony at their price, and offer their medic to see to the colony's sick. A deckhand may ask to disembark here and join the colony,
    /// against the officer's wish (the camp's own join; the Fleet minds a little). It can turn, once a stay each: the sleeper they brought,
    /// woken lately, falls ill (the camps' fever, the shared harm and illness budgets), or their watch spots raiders coming and warns the
    /// colony a couple of hours ahead (the raid held in the game's own queue, as the Signal Line's mast does).
    /// </summary>
    public static class LandingParty
    {
        public const string EventDef = "Rimfolk_Evt_LandingParty";

        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_LandingParty");

        private static int Now => Find.TickManager.TicksGame;

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static PartyState StateOf(Camp c) => c?.workState as PartyState;

        /// <summary>The camp made by the colony, the crew walking in from where their shuttle set them down; <paramref name="fed"/>: food went with them from the gate.</summary>
        public static Camp Start(HappeningContext ctx, bool fed)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            var people = new List<Pawn>();
            try
            {
                int n = Rand.RangeInclusive(PeopleEvents8Math.PartyMin, PeopleEvents8Math.PartyMax);
                var kinds = new[] { "Rimfolk_HollowFleet_Officer", "Rimfolk_HollowFleet_Guard", "Rimfolk_HollowFleet_Engineer", "Rimfolk_HollowFleet_Deckhand", "Rimfolk_HollowFleet_Deckhand", "Rimfolk_HollowFleet_Deckhand" };
                for (int i = 0; i < n; i++) people.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, kinds[i % kinds.Length]), ctx.faction, new FloatRange(19f, 55f)));
                people.RemoveAll(p => p == null);
                if (people.Count == 0) return null;
                // Their medic: a deckhand who keeps the ship's sickbay.
                var medic = people.Skip(1).LastOrDefault();
                if (medic != null) StorytellerEvents.Skilled(medic, SkillDefOf.Medicine, new IntRange(10, 14));
                // The sleeper they brought: lately woken from the ship's caskets, still weak from the long sleep.
                var sleeper = people.Count > 2 ? people[people.Count - 2] : null;
                if (sleeper != null && HediffDefOf.CryptosleepSickness != null) sleeper.health.AddHediff(HediffDefOf.CryptosleepSickness);
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * PeopleEvents8Math.PartyMaxDays);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                if (!CampSite.TryFind(map, out var center, hot))
                {
                    foreach (var p in people) Arrivals.Discard(p);
                    return null;
                }
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, ctx.faction, hot, null, null, people);
                if (setup == null)
                {
                    foreach (var p in people) Arrivals.Discard(p);
                    return null;
                }
                setup.land = TravelLand.Read(map);
                var c = tracker.Start(setup);
                if (c == null)
                {
                    CampTracker.Discard(setup);
                    return null;
                }
                c.workState = new PartyState { crew = people.ToList(), sleeper = sleeper, fed = fed };
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: the landing party", e);
                return null;
            }
        }

        /// <summary>Their hour: the watch bell at the change of the watch, the sleeper they brought, a deckhand gone to the colony.</summary>
        public static void Hour(Camp c, PartyState st, int hour)
        {
            int at = GenDate.DaysPassed * 24 + hour;
            if (PeopleEvents8Math.BellHour(hour) && st.bellAt != at)
            {
                st.bellAt = at;
                var ringer = c.Here.Where(p => p.RaceProps.Humanlike && !p.Downed).RandomElementWithFallback();
                if (ringer != null) c.SayMoment(ringer, "bell");
            }
            // A deckhand who disembarked against the officer's wish (the camp's own join): the officer has words for it, and the Fleet minds.
            if (!st.deserted && st.crew.Any(p => p != null && !p.Dead && p.Faction == Faction.OfPlayer))
            {
                st.deserted = true;
                if (c.leader != null && c.leader.Spawned && c.leader.Faction != Faction.OfPlayer) c.SayMoment(c.leader, "deserter");
                var f = c.faction;
                if (f != null && f.HasGoodwill && !f.IsPlayer && !f.defeated && !f.HostileTo(Faction.OfPlayer))
                    WorldSituationWorker.Goodwill(f, PeopleEvents8Math.DeckhandGoodwill, GoodwillWhy.KeptTheirs);
            }
            if (!st.sickRolled && c.DaysHere >= 1f && hour >= 9 && hour <= 17) Sick(c, st);
        }

        /// <summary>
        /// Once a stay: the sleeper they brought, woken lately and still weak, falls ill at their camp (the game's flu, the camps' fever kind
        /// in the shared harm budget and the illness budget); told, and the colony's doctor may see to them (the camps' tending).
        /// </summary>
        public static void Sick(Camp c, PartyState st)
        {
            st.sickRolled = true;
            var map = c.map;
            var who = st.sleeper != null && c.Here.Contains(st.sleeper) ? st.sleeper : c.Here.Where(p => p.RaceProps.Humanlike && p != c.leader).RandomElementWithFallback();
            var ill = DefDatabase<HediffDef>.GetNamedSilentFail("Flu");
            if (ill == null || who == null || !Harm.On || !Harm.SystemOn(HarmKinds.CampFever.system) || !Harm.Roll(HarmKinds.CampFever, PeopleEvents8Math.SleeperSickChance)) return;
            if (!IllnessBudget.Allows(map, ill, who) || Harm.Ail(who, ill, 0.1f, HarmKinds.CampFever) == null) return;
            IllnessBudget.Owe(map, ill, who);
            st.sick = true;
            Tell.Message(NewsKind.Health, Importance.Notable, map, c.Text("CC_PEv8_PartySick", c.leader, null, 0, who.LabelShort), new LookTargets(who), MessageTypeDefOf.NegativeHealthEvent);
        }

        /// <summary>A place surveyed and logged: now and then a word of it.</summary>
        public static void Surveyed(Pawn by, IntVec3 cell)
        {
            var c = CampTracker.VisitorsAt(by?.Map).FirstOrDefault(x => x.kind == Kind && x.Has(by));
            var st = StateOf(c);
            if (st == null) return;
            st.surveyed++;
            c.SayMoment(by, "survey", 0.35f);
        }

        /// <summary>A place to survey: open ground a walk from their camp, out of the fog, that they can reach.</summary>
        public static IntVec3 SurveyCell(Camp c, Pawn p)
        {
            var map = c.map;
            for (int i = 0; i < 12; i++)
            {
                var cell = c.center + IntVec3.FromVector3(new Vector3(Rand.Range(-40f, 40f), 0f, Rand.Range(-40f, 40f)));
                if (!cell.InBounds(map) || cell.InHorDistOf(c.center, 12f) || cell.Fogged(map) || !cell.Standable(map) || cell.GetEdifice(map) != null) continue;
                if (map.areaManager.Home != null && map.areaManager.Home[cell]) continue;
                if (p.CanReach(cell, PathEndMode.OnCell, Danger.Some)) return cell;
            }
            return IntVec3.Invalid;
        }

        /// <summary>
        /// A raid the storyteller sends to a home where a landing party camps (Patch_LineMastWarns, beside the Signal Line's mast): their
        /// watch spots it first, once a stay, and it goes into the game's own queue a couple of hours on while their warning is told now.
        /// True: held.
        /// </summary>
        public static bool Warn(FiringIncident fi)
        {
            var map = fi?.parms?.target as Map;
            var c = On(map);
            var st = StateOf(c);
            if (fi?.def?.Worker == null || st == null || st.warned || !c.Settled || c.Hostile) return false;
            bool raid = fi.def.Worker is IncidentWorker_Raid;
            bool threat = fi.def.category == IncidentCategoryDefOf.ThreatBig || fi.def.category == IncidentCategoryDefOf.ThreatSmall;
            if (!PeoplesFMath.Warns(raid, threat, fi.sourceQuestPart != null || fi.parms.quest != null, false)) return false;
            if (!fi.def.Worker.CanFireNow(fi.parms)) return false;
            int at = Now + PeoplesFMath.WarnHours(Rand.Value) * GenDate.TicksPerHour;
            if (!Find.Storyteller.incidentQueue.Add(new QueuedIncident(fi, at, PeoplesFMath.WarnRetryHours * GenDate.TicksPerHour))) return false;
            st.warned = true;
            if (c.leader != null && c.leader.Spawned) c.SayMoment(c.leader, "raiders");
            string who = fi.parms.faction != null ? fi.parms.faction.Name : "CC_PeoplesF_RaidWarnSomeone".Translate().Resolve();
            Tell.Letter(NewsKind.Land, Importance.Important, map, "CC_PEv8_WatchWarnLabel".Translate().Resolve(),
                "CC_PEv8_WatchWarn".Translate(who.Named("WHO"), (c.faction?.Name ?? "?").Named("FACTION")).Resolve(), LetterDefOf.ThreatSmall, c.Look, c.faction);
            return true;
        }

        /// <summary>The colony sold them a week's meals (carried out by the ask's own carry-out): their price paid.</summary>
        public static void Sold(Camp c, int meals)
        {
            var st = StateOf(c);
            if (st == null || c.faction == null) return;
            st.fed = true;
            Couriers.PaySilver(c.map, c.FireCell, Mathf.Max(0, meals) * PeopleEvents8Math.PartyMealPrice, c.faction, c.leader);
        }

        public static void Leave(Camp c, PartyState st)
        {
            if (!c.Hostile && c.tension < 50f) PeopleEvents8.Goodwill(c.faction, PeopleEvents8Math.PartyGoodwill, GoodwillWhy.CampWell);
        }
    }

    /// <summary>The landing party's ways: the survey by day, the watch bell, the sleeper they brought, the deckhand who stays, the warning.</summary>
    public class CampWorker_LandingParty : CampWorker
    {
        public override void Started(Camp c)
        {
            if (!(c.workState is PartyState)) c.workState = new PartyState { crew = c.members.ToList() };
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is PartyState st))
            {
                Started(c);
                st = LandingParty.StateOf(c);
            }
            if (!c.Settled || st == null) return;
            LandingParty.Hour(c, st, hour);
        }

        /// <summary>By day, the survey: a walk to a place on the land and an hour's look at it, logged.</summary>
        public override Job DayJob(Camp c, Pawn p)
        {
            if (!PeopleEvents8Math.SurveyHour(GenLocalDate.HourOfDay(c.map)) || !p.RaceProps.Humanlike || !Rand.Chance(0.5f)) return null;
            var cell = LandingParty.SurveyCell(c, p);
            return cell.IsValid ? PeopleEvents8.Make(PeopleEvents8.SurveyJob, cell, PeopleEvents8Math.SurveyTicks) : null;
        }

        public override void Leaving(Camp c)
        {
            var st = LandingParty.StateOf(c);
            if (st != null) LandingParty.Leave(c, st);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = LandingParty.StateOf(c);
            if (st == null) return;
            if (st.surveyed >= 5) marks.Add("surveyed");
            if (st.warned) marks.Add("warned");
            if (st.deserted) marks.Add("deserter");
        }

        public override string Fill(Camp c, string text) => text == null || !text.Contains("{SURVEYED}") ? text : text.Replace("{SURVEYED}", (LandingParty.StateOf(c)?.surveyed ?? 0).ToString());

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = LandingParty.StateOf(c);
            if (st == null) return null;
            switch (a?.key)
            {
                case "food":
                    return st.fed ? "CC_PEv8_PartyFed".Translate().Resolve() : null;
                case "medic":
                    return c.Here.Any(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level >= 6) ? null : "CC_PEv8_NoMedic".Translate().Resolve();
            }
            return null;
        }
    }

    /// <summary>An ensign of {FACTION} ({X}) at the gate: a landing party of the Fleet would camp by the colony a few days and survey the land.</summary>
    public class HappeningWorker_LandingParty : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            LandingParty.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && LandingParty.On(map) == null && PeoplesD.Friendly(PeoplesMath.Crew) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Crew);
            if (ctx.faction == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_HollowFleet_Deckhand"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.count = PeopleEvents8Math.PartyMeals;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (PeopleEvents.AgeOf(ctx.faction) >= 2) tags.Add(ModsConfig.RoyaltyActive ? "shuttle" : "pods");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (LandingParty.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map)) ? "CC_PEv2_NoRoomCamp".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "camp" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The landing party's camp made (LandingParty.Start); <see cref="fed"/>: the week's food went with the ensign from the gate.</summary>
    public class DilemmaEffect_LandingParty : DilemmaEffect
    {
        public bool fed;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_PEv8_Impact_Party".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv8_Impact_PartyBrief".Translate().Resolve(), "pev8party");
            sheet.Add(ImpactKind.Other, 1, "CC_PEv8_Impact_PartyWatch".Translate().Resolve(), "CC_PEv8_Impact_PartyWatchBrief".Translate().Resolve(), "pev8watch");
            sheet.Add(ImpactKind.Other, -1, "CC_PEv8_Impact_PartyTurns".Translate().Resolve(), "CC_PEv8_Impact_PartyTurnsBrief".Translate().Resolve(), "pev8partyturns");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents8Math.PartyGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            LandingParty.Start(hc, fed);
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>The landing party buys the colony's meals at their price (carried out to them by the ask's own carry-out).</summary>
    public class DilemmaEffect_PartyBuys : DilemmaEffect
    {
        public int meals = PeopleEvents8Math.PartyMeals;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) => sheet.Silver(meals * PeopleEvents8Math.PartyMealPrice);

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_LandingParty);
            if (c != null) LandingParty.Sold(c, meals);
        }
    }
}
