using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 29. Flooded out (the river folk) =====================

    /// <summary>The flooded families' days: their old and young taken in as guests, and the sickness rolled once.</summary>
    public class FloodState : CampWorkState
    {
        public bool guestsAsked, guestsDone, sickRolled, sick, racked;
        public int racks;
        public List<Pawn> guests = new List<Pawn>();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref racked, "racked");
            Scribe_Values.Look(ref racks, "racks");
            Scribe_Values.Look(ref guestsAsked, "guestsAsked");
            Scribe_Values.Look(ref guestsDone, "guestsDone");
            Scribe_Values.Look(ref sickRolled, "sickRolled");
            Scribe_Values.Look(ref sick, "sick");
            Scribe_Collections.Look(ref guests, "guests", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                guests ??= new List<Pawn>();
                guests.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// Flooded out (item 29): when the river rises at a landing (the flood world event running), boats of river folk families
    /// camp on the colony's bank for five to ten days, nets out to dry. They ask for food until their catch comes in, and
    /// offer their fish and to mend what is worn; two of them may ask to stay for good. With Hospitality the colony can take
    /// their old and young in as its guests. Leaving, they tie a knot in the net for the colony (+12) and leave their drying
    /// racks. It can turn: a sickness comes home with them (the harm and illness budgets), or they ask to stay longer than the
    /// colony likes (the camps' own asking).
    /// </summary>
    public static class FloodedOut
    {
        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_FloodedOut");

        private static int Now => Find.TickManager.TicksGame;

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static FloodState StateOf(Camp c) => c?.workState as FloodState;

        /// <summary>The flood world event running in the region (Council_WS_Flood): its place, or null.</summary>
        public static string Flood()
        {
            var s = WorldSituations.Instance?.Running?.FirstOrDefault(x => x?.def?.defName == "Council_WS_Flood");
            return s == null ? null : (!(s.ctx?.placeName).NullOrEmpty() ? s.ctx.placeName : s.def.label);
        }

        /// <summary>Hospitality's way of making guests is there and on (Integrations/InvitedGuests.cs).</summary>
        public static bool Guests => InvitedGuests.On && InvitedGuests.Found;

        /// <summary>The camp made by the colony's water (the herders' way in the dry year), its families walking in from the edge.</summary>
        public static Camp Start(HappeningContext ctx, bool guests)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            try
            {
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 7);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                var water = DryWater.Water(map);
                var center = water.IsValid ? CampLand.TryFindNear(map, water, 3f, 14f, hot) : IntVec3.Invalid;
                if (!center.IsValid && !CampSite.TryFind(map, out center, hot)) return null;
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, ctx.faction, hot, null, null);
                if (setup == null) return null;
                setup.land = TravelLand.Read(map);
                var c = tracker.Start(setup);
                if (c == null)
                {
                    CampTracker.Discard(setup);
                    return null;
                }
                c.workState = new FloodState { guestsAsked = guests };
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: the flooded families' camp", e);
                return null;
            }
        }

        /// <summary>Their old and young (children first, then the oldest) handed to Hospitality as the colony's guests for the rest of the stay.</summary>
        public static void TakeIn(Camp c, FloodState st)
        {
            st.guestsDone = true;
            if (!Guests || c.map == null) return;
            var who = c.Here.Where(p => p.RaceProps.Humanlike && p != c.leader).OrderByDescending(p => p.DevelopmentalStage.Juvenile()).ThenByDescending(p => p.ageTracker.AgeBiologicalYears)
                .Take(PeopleEvents4Math.GuestsMax).ToList();
            if (who.Count == 0) return;
            float hours = Mathf.Max(6f, (c.untilTick == int.MaxValue ? c.stayTicks : c.untilTick - Now) / (float)GenDate.TicksPerHour);
            try
            {
                Lords.Leave(who);
                var spot = PeoplesD.GateSpot(c.map) ?? c.FireCell;
                if (InvitedGuests.HandOver(ArrivalWhy.TownInvited, who, c.map, c.faction, spot, hours) == null)
                {
                    // Hospitality would not take them: back to their own camp.
                    c.lord?.AddPawns(who);
                    return;
                }
                st.guests.AddRange(who);
                c.Note(c.Text("CC_PEv4_FloodGuests", c.leader, null, who.Count), MessageTypeDefOf.PositiveEvent, Importance.Notable, who[0], true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: the flooded families as guests", e);
            }
        }

        /// <summary>Their racks put up by the fire once they are settled (two at most), theirs while they stay and left standing when they go.</summary>
        public static void Racks(Camp c, FloodState st)
        {
            st.racked = true;
            var def = PeopleEvents4Math.RackDefs.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null && d.category == ThingCategory.Building);
            var map = c.map;
            if (def == null || map == null) return;
            int placed = 0;
            foreach (var cell in GenRadial.RadialCellsAround(c.FireCell, 7f, false))
            {
                if (placed >= 2) break;
                var rect = GenAdj.OccupiedRect(cell, Rot4.North, def.size);
                if (!rect.Cells.All(x => x.InBounds(map) && x.Standable(map) && x.GetEdifice(map) == null && x.GetFirstItem(map) == null && x.GetFirstPawn(map) == null
                                         && !(x.GetTerrain(map)?.IsWater ?? true) && c.MayWork(x))) continue;
                if (rect.ExpandedBy(1).Cells.Any(x => x.InBounds(map) && x.GetEdifice(map) != null)) continue;
                try
                {
                    var t = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                    t.SetFaction(c.faction);
                    GenSpawn.Spawn(t, cell, map, Rot4.North);
                    placed++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 4: the flooded families' racks", e);
                    return;
                }
            }
            st.racks = placed;
        }

        /// <summary>Once a stay, a sickness of the flood water comes home with them (the harm and illness budgets), told.</summary>
        public static void Sickness(Camp c, FloodState st)
        {
            st.sickRolled = true;
            var ill = DefDatabase<HediffDef>.GetNamedSilentFail("Flu");
            if (ill == null || !Harm.On || !Harm.SystemOn(HarmKinds.CampFever.system) || !Harm.Roll(HarmKinds.CampFever, PeopleEvents4Math.FloodSickChance)) return;
            var who = c.map.mapPawns.FreeColonistsSpawned.Where(p => p != null && !p.Dead && Harm.May(p) && !p.health.hediffSet.HasHediff(ill) && p.Position.InHorDistOf(c.FireCell, 40f))
                .RandomElementWithFallback();
            if (who == null || !IllnessBudget.Allows(c.map, ill, who) || Harm.Ail(who, ill, 0.1f, HarmKinds.CampFever) == null) return;
            IllnessBudget.Owe(c.map, ill, who);
            st.sick = true;
            Tell.Message(NewsKind.Health, Importance.Important, c.map, c.Text("CC_PEv4_FloodSick", c.leader, who, 0, ill.label), new LookTargets(who), MessageTypeDefOf.NegativeHealthEvent);
        }
    }

    /// <summary>The flooded families' camp on the colony's bank: their old and young taken in, the sickness, the asks, and the knot as they go.</summary>
    public class CampWorker_FloodedOut : CampWorker
    {
        public override bool FitsMap(Map map) => DryWater.HasWater(map);

        public override IntVec3 SiteFor(Map map, IntVec3 usual, bool shade)
        {
            var water = DryWater.Water(map);
            if (!water.IsValid) return usual;
            var c = CampLand.TryFindNear(map, water, 3f, 14f, shade);
            return c.IsValid ? c : usual;
        }

        public override void Started(Camp c)
        {
            if (!(c.workState is FloodState)) c.workState = new FloodState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is FloodState st))
            {
                Started(c);
                st = FloodedOut.StateOf(c);
            }
            if (!st.racked && c.Settled && c.DaysHere >= 0.5f) FloodedOut.Racks(c, st);
            if (st.guestsAsked && !st.guestsDone && c.Settled && c.DaysHere >= 0.2f) FloodedOut.TakeIn(c, st);
            if (!st.sickRolled && c.DaysHere >= 2f) FloodedOut.Sickness(c, st);
        }

        /// <summary>Going: the knot tied in the net for the colony, and their guests' stay ends with the camp's.</summary>
        public override void Leaving(Camp c)
        {
            var st = FloodedOut.StateOf(c);
            foreach (var g in st?.guests.Where(p => p != null && p.Spawned && !p.Dead) ?? Enumerable.Empty<Pawn>())
            {
                var lord = g.GetLord();
                if (lord != null && InvitedGuests.Hosted(lord)) InvitedGuests.Leave(lord);
            }
            if (c.Hostile || c.faction == null || !c.faction.HasGoodwill || c.faction.IsPlayer || c.tension >= 60f) return;
            WorldSituationWorker.Goodwill(c.faction, PeopleEvents4Math.KnotGoodwill, GoodwillWhy.Helped);
            FactionMemory.Note(c.faction, "sheltered", "CC_PEv4_FloodName".Translate().Resolve());
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = FloodedOut.StateOf(c);
            if (c.tension < 60f) marks.Add("knot");
            if (st != null && st.guests.Count > 0) marks.Add("guests");
            if (st != null && st.sick) marks.Add("sick");
            if (st != null && st.racks > 0) marks.Add("racks");
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            if (a?.key == "food" && c.DaysHere > PeopleEvents4Math.FoodDays) return "CC_PEv4_CatchIn".Translate().Resolve();
            if (a?.key == "guests")
            {
                var st = FloodedOut.StateOf(c);
                if (!FloodedOut.Guests) return "CC_PEv4_NeedsHospitality".Translate().Resolve();
                if (st == null || st.guestsAsked || st.guestsDone) return "CC_PEv4_GuestsTaken".Translate().Resolve();
            }
            return null;
        }
    }

    /// <summary>A boatman of the river folk ({X}) at the gate: their landing is under water ({TALE}), and the families' boats are coming down to the colony's bank.</summary>
    public class HappeningWorker_FloodedOut : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var kind = FloodedOut.Kind;
            return kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && PeoplesD.Friendly(PeoplesMath.Fishers) != null && FloodedOut.Flood() != null && DryWater.HasWater(map)
                   && FloodedOut.On(map) == null && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Fishers);
            ctx.tale = FloodedOut.Flood();
            if (ctx.faction == null || ctx.tale == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_RiverFolk_Ferryman"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (FloodedOut.Guests) tags.Add("hospitality");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (FloodedOut.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map)) return "CC_PEv2_NoRoomCamp".Translate().Resolve();
            if (option == 1 && !FloodedOut.Guests) return "CC_PEv4_NeedsHospitality".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "bank" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The families come to the colony's bank (FloodedOut.Start); with <see cref="guests"/> their old and young become the colony's guests.</summary>
    public class DilemmaEffect_FloodCamp : DilemmaEffect
    {
        public bool guests;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_PEv4_Impact_Flood".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv4_Impact_FloodBrief".Translate().Resolve(), "pev4flood");
            if (guests) sheet.Add(ImpactKind.People, 1, "CC_PEv4_Impact_FloodGuests".Translate(PeopleEvents4Math.GuestsMax).Resolve(), "CC_PEv4_Impact_FloodGuestsBrief".Translate().Resolve(), "pev4floodguests");
            if (Harm.On) sheet.Add(ImpactKind.Health, -1, "CC_PEv4_Impact_FloodSick".Translate().Resolve(), "CC_PEv4_Impact_FloodSickBrief".Translate().Resolve(), "pev4floodsick");
            if (hc.faction != null)
                sheet.Add(ImpactKind.Goodwill, 1, "CC_PEv4_Impact_Knot".Translate(hc.faction.Name, PeopleEvents4Math.KnotGoodwill).Resolve(), "CC_PEv4_Impact_KnotBrief".Translate().Resolve(), "pev4knot");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            FloodedOut.Start(hc, guests);
            // The boatman goes back down to the boats.
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>No room on the bank: they go on down the river, and think a little less of the colony.</summary>
    public class DilemmaEffect_FloodTurned : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.faction != null) sheet.Goodwill(ctx.faction, PeopleEvents4Math.FloodTurnedGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var f = ctx?.faction;
            if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents4Math.FloodTurnedGoodwill, GoodwillWhy.TurnedAway);
        }
    }

    /// <summary>Their old and young taken in as the colony's guests (asked from the camp's page), once they are settled.</summary>
    public class DilemmaEffect_FloodGuests : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_FloodedOut);
            var st = FloodedOut.StateOf(c);
            if (st == null) return;
            st.guestsAsked = true;
            if (c.Settled) FloodedOut.TakeIn(c, st);
        }
    }
}
