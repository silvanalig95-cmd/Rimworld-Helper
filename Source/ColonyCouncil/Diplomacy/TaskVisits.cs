using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// An officer's visit to their town by caravan (docs/design/stage2-diplomacy.md, item 8; decision 14): at most one
    /// officer of a home away on the council's business, each office once a season. The visit is a journey of the journey
    /// planner (Journeys/: the colony's own plan, one a home), made for the officer and a companion to the town, there and
    /// back with a day's stay, the next morning: their last day and hours play out as any journey's (the night before,
    /// packing road food and medicine, dressing for the weather, the gate), and the player can open it in the planner,
    /// change it or call it off. On the road the game's own caravan incidents apply; the caravan is the player's own and can
    /// be turned back. At the town they stay a day (TaskResults.Visited), then turn for home on their own; home again, the
    /// visit is done. Their work waits while they are away.
    /// </summary>
    public static class TaskVisits
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ================= Can they go =================

        /// <summary>Who goes with the officer: a grown colonist at home, free, with no seat if any can go, the best with a weapon first.</summary>
        public static Pawn Companion(Map home, Pawn officer)
        {
            if (home == null) return null;
            var offices = ColonyState.For(home)?.offices;
            return home.mapPawns.FreeColonistsSpawned
                .Where(p => p != null && p != officer && !p.Dead && !p.Downed && !p.InMentalState && !p.IsQuestLodger() && !p.DevelopmentalStage.Juvenile() && !p.IsSlave
                            && !p.WorkTagIsDisabled(WorkTags.Violent) && CouncilTasks.Of(p) == null && JourneyBook.PlanOf(p) == null)
                .OrderBy(p => offices?.OfficeOf(p) != null ? 1 : 0)
                .ThenByDescending(p => (p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0) + (p.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0))
                .FirstOrDefault();
        }

        /// <summary>Why the officer can't be sent to this town now, or null: the road, the colony's people to spare, one away, a season since the last, a journey planned.</summary>
        public static string WhyNot(TaskKind k, Settlement town, Map home, Pawn officer, int lastVisit, CouncilTask self)
        {
            if (!JourneyBook.On) return TaskWords.P("CC_Task_Why_JourneysOff");
            if (home == null || officer == null) return TaskWords.P("CC_Task_Why_NoHome");
            if (town == null || town.Destroyed) return TaskWords.P("CC_Task_Why_NoTown");
            if (!Find.WorldReachability.CanReach(home.Tile, town.Tile)) return TaskWords.P("CC_Task_Why_NoRoad", town.Name.Named("TOWN"));
            var away = CouncilTasks.Away(home, self);
            if (away != null) return TaskWords.P("CC_Task_Why_OneAway", away.OfficerName.Named("PAWN"));
            if (!TaskMath.MayVisitNow(false, lastVisit, Now)) return TaskWords.P("CC_Task_Why_Season", officer.Named("PAWN"));
            var plan = JourneyBook.PlanAt(home);
            if (plan != null) return TaskWords.P("CC_Task_Why_Journey", plan.Dest.Named("PLACE"));
            if (!TaskMath.CanSpare(home.mapPawns.FreeColonistsSpawnedCount)) return TaskWords.P("CC_Task_Why_Spare");
            if (officer.MapHeld != home) return TaskWords.P("CC_Task_Why_Away", officer.Named("PAWN"));
            if (k == TaskKind.Tend && TaskMath.MedicineFor(Medicine(home)) < TaskMath.MinMedicine) return TaskWords.P("CC_Task_Why_NoMedicine");
            return null;
        }

        public static string WhyNot(CouncilTask t, Map home)
        {
            if (t == null || !t.Live) return TaskWords.P("CC_Task_Why_Gone");
            if (!TaskMath.MayVisit(t.kind)) return TaskWords.P("CC_Task_Why_NoVisit");
            if (t.Away) return TaskWords.P("CC_Task_Why_Planned");
            return WhyNot(t.kind, CouncilTasks.TownOf(t, home), home, t.officer, t.lastVisit, t);
        }

        /// <summary>Medicine in the colony's stores (the physician takes some of it).</summary>
        private static int Medicine(Map home) => home?.resourceCounter == null ? 0 : home.resourceCounter.AllCountedAmounts.Where(kv => kv.Key.IsMedicine).Sum(kv => kv.Value);

        /// <summary>The best medicine the stores hold, for the physician's gift (the colony's own, not glitterworld if it has plainer).</summary>
        private static ThingDef MedicineDef(Map home)
        {
            var counted = home?.resourceCounter?.AllCountedAmounts;
            if (counted == null) return null;
            return counted.Where(kv => kv.Key.IsMedicine && kv.Value >= TaskMath.MinMedicine).OrderBy(kv => kv.Key.BaseMarketValue).Select(kv => kv.Key).FirstOrDefault();
        }

        // ================= Planning it =================

        /// <summary>The council's word on a visit, with its cost and risks named (the work that waits, the roads, a physician's risk), then the plan.</summary>
        public static void Ask(CouncilTask t, Map home)
        {
            if (WhyNot(t, home) != null) return;
            var town = CouncilTasks.TownOf(t, home);
            var comp = Companion(home, t.officer);
            string text = TaskWords.VisitAsk(t, home, town, comp);
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, () => Plan(t, home)));
        }

        /// <summary>
        /// The journey made: the officer and a companion, to the town and back with a day there, setting off at the next
        /// morning's hour (the planner's own default), food and medicine packed by the planner's rules, and for the physician
        /// medicine to give. False (and a word) when it can't be made now.
        /// </summary>
        public static bool Plan(CouncilTask t, Map home)
        {
            string why = WhyNot(t, home);
            var town = CouncilTasks.TownOf(t, home);
            // The acts that plan one are greyed with why not, so this only fails when something changed in between.
            if (why != null || town == null) return false;
            try
            {
                var officer = t.officer;
                var comp = Companion(home, officer);
                int nowAbs = Find.TickManager.TicksAbs;
                var plan = new JourneyPlan
                {
                    map = home, departAbs = JourneyRun.HourStart(nowAbs) + JourneyMath.DefaultHoursAhead(JourneyRun.HourOfDay(nowAbs, home)) * GenDate.TicksPerHour,
                    andBack = true, destTile = town.Tile.tileId, destObject = town.ID, destName = town.Name
                };
                plan.travellers.Add(officer);
                if (comp != null) plan.travellers.Add(comp);
                // Twice, as the planner does: the load slows the caravan, and a longer road wants more packed.
                for (int pass = 0; pass < 2; pass++)
                {
                    float mass = JourneyPack.Mass(plan.travellers, plan.packing);
                    var first = JourneyPack.Route(home, plan.DestTile, plan.travellers.Concat(plan.animals).ToList(), mass, plan.departAbs, true, -1f);
                    if (first.noWay) return false;
                    float road = first.oneWayTicks > 0 ? first.oneWayTicks / (float)GenDate.TicksPerDay : 1f;
                    plan.days = TaskMath.TripDays(road);
                    plan.daysChosen = true;
                    var route = JourneyPack.Route(home, plan.DestTile, plan.travellers.Concat(plan.animals).ToList(), mass, plan.departAbs, true, plan.days);
                    plan.low = route.low;
                    plan.high = route.high;
                    plan.packing = JourneyPack.Packing(home, plan.travellers, plan.animals, plan.days, route, plan.packing);
                }
                if (t.kind == TaskKind.Tend)
                {
                    var med = MedicineDef(home);
                    int n = TaskMath.MedicineFor(Medicine(home));
                    if (med != null && n > 0) plan.packing.Add(new JourneyLine(med, n, "other") { added = true });
                    // What is given at their town is what was packed for them, not the travellers' own medicine for the road.
                    t.progress = med != null ? n : 0;
                }
                JourneyRun.Confirm(plan, true);
                t.planId = plan.id;
                t.visit = VisitStage.Planned;
                t.companion = comp;
                t.companionName = comp?.LabelShort;
                t.visitSince = -1;
                Diplomacy.Dirty();
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("council tasks: planning a visit", e);
                return false;
            }
        }

        private static JourneyPlan PlanOf(CouncilTask t) => t == null || t.planId < 0 ? null : JourneyBook.Plans.FirstOrDefault(p => p.id == t.planId && p.Has(t.officer));

        /// <summary>A visit still only planned, called off with its task (the journey's own call-off: timetables back, packs put away).</summary>
        internal static void CallOffPlan(CouncilTask t, Map home)
        {
            var plan = t == null || t.planId < 0 ? null : JourneyBook.Plans.FirstOrDefault(p => p.id == t.planId);
            if (plan != null) JourneyRun.CallOff(plan, "CC_Task_Visit_PlanOff");
            if (t != null && t.visit == VisitStage.Planned) t.visit = VisitStage.None;
        }

        // ================= The hour =================

        public static void Step(DiplomacyHome h, CouncilTask t, int now)
        {
            var home = h.Map;
            var p = t.officer;
            if (p == null || p.Dead || p.Discarded)
            {
                t.visit = VisitStage.None;
                return;
            }
            // The task ended (called off, its officer's seat or its target gone) while they are on the way or there: they turn for home.
            if (!t.Live && (t.visit == VisitStage.Road || t.visit == VisitStage.There) && p.GetCaravan() is Caravan back)
            {
                Homeward(h, t, back, home, false);
                return;
            }
            switch (t.visit)
            {
                case VisitStage.Planned:
                {
                    if (PlanOf(t) != null) return;
                    if (p.GetCaravan() != null)
                    {
                        t.visit = VisitStage.Road;
                        Diplomacy.Dirty();
                        return;
                    }
                    // Gathering at the gate to leave (the game's caravan forming): they're still going.
                    if (p.Spawned && p.GetLord()?.LordJob is LordJob_FormAndSendCaravan) return;
                    Off(h, t, home, TaskWords.P("CC_Task_Visit_CalledOff", p.Named("PAWN")));
                    return;
                }
                case VisitStage.Road:
                {
                    var c = p.GetCaravan();
                    var town = CouncilTasks.TownOf(t, home);
                    if (c == null)
                    {
                        // Turned back home by the player, or carried off; on the map of a fight on the road they wait.
                        if (p.Spawned && Homes.IsHome(p.Map) || People.Kidnapped(p)) Off(h, t, home, TaskWords.P("CC_Task_Visit_TurnedBack", p.Named("PAWN")));
                        return;
                    }
                    if (town == null || town.Destroyed || Diplomacy.Hostile(town.Faction))
                    {
                        Homeward(h, t, c, home, false);
                        return;
                    }
                    bool there = c.Tile == town.Tile || !c.pather.Moving && Find.WorldGrid.ApproxDistanceInTiles(c.Tile, town.Tile) <= 1.01f;
                    if (there) Arrived(h, t, c, town, home);
                    else if (!c.pather.Moving && !c.pather.Paused) c.pather.StartPath(town.Tile, null, true);
                    return;
                }
                case VisitStage.There:
                {
                    var c = p.GetCaravan();
                    var town = CouncilTasks.TownOf(t, home);
                    if (c == null)
                    {
                        if (p.Spawned && Homes.IsHome(p.Map) || People.Kidnapped(p)) Off(h, t, home, TaskWords.P("CC_Task_Visit_TurnedBack", p.Named("PAWN")));
                        return;
                    }
                    // The day there done, they turn for home; moved on before it (by the player, or the town gone), the visit was cut short.
                    if (now - t.visitSince >= Mathf.RoundToInt(TaskMath.StayDays * TaskMath.DayTicks)) Homeward(h, t, c, home, true);
                    else if (town == null || town.Destroyed || Find.WorldGrid.ApproxDistanceInTiles(c.Tile, town.Tile) > 1.5f)
                    {
                        // apart: the officer is far from home; told in a message that looks at the caravan.
                        Tell.Message(NewsKind.World, Importance.Notable, home, TaskWords.P("CC_Task_Visit_CutShort", p.Named("PAWN"), (t.townName ?? "?").Named("TOWN")),
                            new LookTargets(c), MessageTypeDefOf.NeutralEvent);
                        Homeward(h, t, c, home, false);
                    }
                    return;
                }
                case VisitStage.Homeward:
                {
                    if (p.Spawned && p.Map == home)
                    {
                        Back(h, t, home);
                        return;
                    }
                    var c = p.GetCaravan();
                    if (c == null)
                    {
                        if (!p.Spawned && People.Kidnapped(p)) t.visit = VisitStage.None;
                        return;
                    }
                    // Sent somewhere else by the player and stopped there two days on: the council stops counting them away on its business.
                    if (!c.pather.Moving && t.lastVisit >= 0 && now - t.lastVisit > 2 * TaskMath.DayTicks)
                    {
                        t.visit = VisitStage.None;
                        Diplomacy.Dirty();
                    }
                    return;
                }
            }
        }

        /// <summary>At their town: a day there begins (their people's way of receiving such a visit named).</summary>
        private static void Arrived(DiplomacyHome h, CouncilTask t, Caravan c, Settlement town, Map home)
        {
            t.visit = VisitStage.There;
            t.visitSince = Now;
            var form = TaskWords.Form(home, town.Faction);
            // apart: the officer is far from home; told in a message that looks at the caravan.
            Tell.Message(NewsKind.World, Importance.Notable, home, TaskWords.T("CC_Task_Visit_Arrived", t.officer.Named("PAWN"), town.Name.Named("TOWN"), form.rite.Named("RITE")),
                new LookTargets(c), MessageTypeDefOf.NeutralEvent);
            Diplomacy.Dirty();
        }

        /// <summary>The day there is over (or the caravan moved on): what it did, and the road home (the caravan turned for home on its own).</summary>
        private static void Homeward(DiplomacyHome h, CouncilTask t, Caravan c, Map home, bool stayed)
        {
            var town = CouncilTasks.TownOf(t, home);
            string line = null;
            if (stayed && t.Live)
            {
                try
                {
                    line = TaskResults.Visited(h, t, town, c);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("council tasks: the visit's day", e);
                }
                t.visits++;
            }
            t.lastVisit = Now;
            t.visit = VisitStage.Homeward;
            try
            {
                if (home?.Parent != null && c != null && !c.Destroyed) c.pather.StartPath(home.Tile, new CaravanArrivalAction_Enter(home.Parent), true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("council tasks: the road home", e);
            }
            if (!line.NullOrEmpty())
            {
                CouncilTasks.Write(t, home, line, false);
                Tell.Letter(NewsKind.World, Importance.Notable, home, TaskWords.P("CC_Task_Visit_Label", t.officer.Named("PAWN"), (town?.Name ?? t.townName ?? "?").Named("TOWN")),
                    line + "\n\n" + TaskWords.P("CC_Task_Visit_Homeward", t.officer.Named("PAWN")), LetterDefOf.NeutralEvent, c != null ? new LookTargets(c) : LookTargets.Invalid,
                    town?.Faction);
            }
            Diplomacy.Dirty();
        }

        /// <summary>Home again: the visit done; a task that was the visit ends with it.</summary>
        private static void Back(DiplomacyHome h, CouncilTask t, Map home)
        {
            t.visit = VisitStage.None;
            // apart: the officer is home on the map; the message looks at them.
            Outsiders.Say(t.officer, "taskhome", null);
            Tell.Message(NewsKind.Colony, Importance.Notable, home, TaskWords.P("CC_Task_Visit_Back", t.officer.Named("PAWN"), (t.townName ?? CouncilTasks.TownOf(t, home)?.Name ?? "?").Named("TOWN")),
                new LookTargets(t.officer), MessageTypeDefOf.PositiveEvent);
            if (t.Live && TaskMath.VisitOnly(t.kind) && t.visits > 0)
                CouncilTasks.End(h, t, TaskStage.Done, TaskWords.P("CC_Task_Result_Visit_" + t.kind, t.OfficerName.Named("PAWN"), TaskWords.People(CouncilTasks.PeopleOf(t)).Named("PEOPLE"),
                    (t.townName ?? "?").Named("TOWN")), false);
            Diplomacy.Dirty();
        }

        /// <summary>The visit off before it came to anything (the plan called off, turned back, carried off): the task goes on without it.</summary>
        private static void Off(DiplomacyHome h, CouncilTask t, Map home, string line)
        {
            t.visit = VisitStage.None;
            t.planId = -1;
            // apart: what became of the visit, told in a message.
            Tell.Message(NewsKind.Colony, Importance.Notable, home, line, t.officer != null && t.officer.Spawned ? new LookTargets(t.officer) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            Diplomacy.Dirty();
        }
    }
}
