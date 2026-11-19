using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // S11, the vault's sealed case (docs/quests-proposals.md; the rules in Flows/VaultFlow.cs, the words in
    // Defs/MissionDefs/Rimfolk_Storyline_Vault.xml). The Sunken Vault (the vaultfolk), else any spacer people. Part 1:
    // components for the vault's failing power cells, collected by its runners (the vault's own power cells mission's ask,
    // which keeps a declining people's age: Peoples/PeoplesG2.cs, VaultSupply). Part 2: one of its engineers stays four to six
    // days as the colony's guest and works a few hours each day at one of the colony's work benches, using a component from
    // its stores for each day's work (no component, the day is lost). Part 3: they leave a sealed case in the colony's care
    // (Rimfolk's own item); half a day after it is set down, a card asks: keep it sealed, and their runner takes it back at
    // the end of the days and the vault owes the colony a favour; or open it now: a sleeper who joins or leaves, a machine
    // that has to be fought (the game's own mechanoid, within the shared harm budget: HarmKinds.StoryFight; else old goods),
    // or old goods. Opened, the vault finds out when its runner comes, and resents it.

    /// <summary>The vault's sealed case beyond its parts: the vault, the case, the endings.</summary>
    public class VaultRules : StorylineRules
    {
        public override string Kind => VaultFlow.Key;

        public override Faction Giver(Map map) =>
            Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && Peoples.CallingOf(f) == PeoplesMath.Vaultfolk)
            ?? Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && Peoples.TechOf(f) >= TechLevel.Spacer);

        public static ThingDef CaseDef => DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_VaultCase");

        /// <summary>The case on this map (lying, carried or in a pack), or null.</summary>
        public static Thing CaseOn(Storyline s, Map map) => s?.thing != null && !s.thing.Destroyed && s.thing.MapHeld == map ? s.thing : null;

        /// <summary>One of the colony's work benches for the engineer (a fabrication bench, a machining table, a smithy, a crafting spot), the finest first.</summary>
        public static Building_WorkTable Bench(Map map, Pawn p)
        {
            string[] best = { "FabricationBench", "TableMachining", "ElectricSmithy", "FueledSmithy", "ElectricTailoringBench", "HandTailoringBench", "CraftingSpot" };
            return map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>()
                .Where(b => b.Spawned && !b.IsBurning() && b.def.hasInteractionCell && (p == null || p.CanReach(b.InteractionCell, PathEndMode.OnCell, Danger.Some)))
                .OrderBy(b => { int i = Array.IndexOf(best, b.def.defName); return i < 0 ? best.Length - 1 : i; }).FirstOrDefault();
        }

        public override bool Ending(Storyline s, string key, Map map, HappeningContext ctx)
        {
            switch (key)
            {
                case "opened":
                    StoryChains.Goodwill(s.giver, VaultFlow.OpenedGoodwill, GoodwillWhy.Of(GoodwillReasonMath.PromiseBroken));
                    return true;
                case "favour":
                    StoryChains.Goodwill(s.giver, VaultFlow.ReturnedGoodwill, GoodwillWhy.Of(GoodwillReasonMath.PromiseKept));
                    // A good turn big enough: the vault owes the colony a favour (Diplomacy/FavoursOwed.cs).
                    FactionMemory.Note(s.giver, "mission", Storylines.Label(s), VaultFlow.FavourWeight);
                    return true;
                case "lostcase":
                    StoryChains.Goodwill(s.giver, VaultFlow.OpenedGoodwill, GoodwillWhy.Of(GoodwillReasonMath.PromiseBroken));
                    return true;
                case "blamed":
                    FactionMemory.Note(s.giver, "prisonerDied", Storylines.Label(s));
                    return true;
            }
            return true;
        }

        /// <summary>Its people at war with the colony or gone: their engineer goes, and the case is the colony's to keep.</summary>
        public override void Gone(Storyline s, Map map)
        {
            var here = new[] { s.x, s.y }.Concat(s.group).Where(p => p != null && p.Spawned && p.Map == map && p.Faction != Faction.OfPlayer && !p.HostileTo(Faction.OfPlayer)).ToList();
            if (here.Count > 0) Arrivals.SendAway(here);
        }
    }

    // ===================== Part 1: power cells for the vault =====================

    /// <summary>
    /// The vault's power cells are failing: it asks for components within six to nine days, collected by its runners and paid
    /// two fifths over their worth (half or more: their plain worth; less: none). Parts given to a people that declines unless
    /// supplied keep its age, as the vault's own mission does.
    /// </summary>
    public class HappeningWorker_VaultCells : HappeningWorker_StorylinePart, IStorylineDebugNow
    {
        protected override int Rating => 2;

        protected override bool Possible(Map map) => Storylines.Rules(VaultFlow.Key).Giver(map) != null;

        protected override string Silver(Mission m, Storyline s) => s?.amount.ToString();

        private static string What => ThingDefOf.ComponentIndustrial.defName;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.making;
            var map = ctx.map;
            var f = Storylines.Rules(VaultFlow.Key).Giver(map);
            if (s == null || f == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Crafting) ?? VarietyUtil.Best(map, SkillDefOf.Social);
            if (m.a == null) return false;
            var x = Arrivals.Make(Arrivals.PersonKind(f), f, new FloatRange(35f, 65f));
            if (x == null) return false;
            Arrivals.Hold(x);
            int n = VaultFlow.Components(IncidentWorker_Happening.Colonists(map).Count);
            s.giver = f;
            s.x = x;
            s.xName = x.LabelShort;
            s.place = LivingEvents.TownOf(f, map) ?? f.Name;
            s.count = n;
            s.amount = VaultFlow.Pay(n, Missions.UnitValue(What));
            if (Peoples.CallingOf(f) == PeoplesMath.Vaultfolk) s.Set("vault");
            m.faction = f;
            m.place = s.place;
            m.x = x;
            m.goals.Add(new MissionGoal { what = What, count = n });
            m.reward = s.amount;
            m.goodwill = 12;
            m.penalty = 6;
            m.days = Rand.Range(VaultFlow.CellsMinDays, VaultFlow.CellsMaxDays);
            Fill(m, ctx);
            ctx.stranger = x;
            ctx.count = n;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            StorylineKit.Accepted(P, Story(m), m, map, null, Story(m)?.amount.ToString());
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null || m.goals.Count == 0)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            var g = m.goals[0];
            g.have = Missions.Count(map, g.what);
            bool all = g.have >= g.count;
            StorylineKit.Remind(P, s, m, map, now, s.amount.ToString());
            if (m.stage == 0 && all)
            {
                m.stage = 1;
                if (m.due < 0)
                {
                    m.due = Mathf.Min(now + GenDate.TicksPerDay / 2, m.deadline);
                    Note(m, map, "ready", LookTargets.Invalid, MessageTypeDefOf.PositiveEvent, null, s.amount.ToString(), TextUtil.DaysString(m.due - now));
                }
            }
            else if (m.stage == 1 && !all) m.stage = 0;
            if (m.stage == 1 && now >= m.due || now >= m.deadline && all)
            {
                StorylineRound3.Collect(map, s.giver, g.what, g.count, s.amount);
                VaultSupply.Noted(s.giver, g.count * Missions.UnitValue(g.what), m.title);
                Missions.Goodwill(m, m.goodwill);
                End(m, MissionState.Done, "sent", null, null, s.amount.ToString());
                return;
            }
            if (now < m.deadline) return;
            if (VaultFlow.CellsOutcome(g.have, g.count) == "partly")
            {
                int pay = VaultFlow.PartPay(g.have, Missions.UnitValue(g.what));
                StorylineRound3.Collect(map, s.giver, g.what, g.have, pay);
                VaultSupply.Noted(s.giver, g.have * Missions.UnitValue(g.what), m.title);
                Missions.Goodwill(m, m.goodwill / 2);
                End(m, MissionState.Done, "partly", null, Missions.Need(new MissionGoal { what = g.what, count = g.have }), pay.ToString());
                return;
            }
            Missions.Goodwill(m, -m.penalty);
            End(m, MissionState.Failed, "none");
        }

        public override string Status(Mission m) =>
            m.stage == 1 ? "CC_Storyline_Pay_Coming".Translate((m.faction?.Name ?? "?").Named("FACTION"), TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve() : null;

        public string DebugNow(Mission m, Map map)
        {
            m.deadline = Find.TickManager.TicksGame;
            return "the day is now: judged as the stores stand.";
        }
    }

    // ===================== Part 2: the engineer at the colony's bench =====================

    /// <summary>
    /// One of the vault's engineers stays four to six days as the colony's guest (a bed and meals) and works about four hours
    /// each day at one of the colony's work benches, using a component from its stores for each day's work. Every day's work
    /// done: worked; some days lost for want of a component: short. The engineer dead or taken prisoner: harmed. On leaving
    /// they pay for the components used.
    /// </summary>
    public class HappeningWorker_VaultEngineer : HappeningWorker_StorylineHost, IStorylineDebugNow
    {
        protected override int Rating => 2;

        public static JobDef WorkJob => DefDatabase<JobDef>.GetNamedSilentFail(JobDriver_StorylineBenchWork.JobName);

        protected override bool Possible(Map map) => Storylines.current?.giver != null && VaultRules.Bench(map, null) != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            if (s?.giver == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Crafting) ?? VarietyUtil.Best(map, SkillDefOf.Social);
            if (m.a == null) return false;
            var engineer = Make(s.giver, new FloatRange(28f, 60f));
            if (engineer == null) return false;
            s.y = engineer;
            s.yName = engineer.LabelShort;
            s.goalA = s.goalB = 0;
            s.baseA = -1;
            m.x = engineer;
            m.faction = s.giver;
            m.place = s.place;
            m.target = 1;
            m.wait = Rand.Range(0.4f, 0.8f);
            float stay = Rand.Range(VaultFlow.EngineerMinDays, VaultFlow.EngineerMaxDays);
            m.days = m.wait + stay;
            m.goals.Add(new MissionGoal { what = ThingDefOf.ComponentIndustrial.defName, count = Mathf.CeilToInt(stay) * VaultFlow.ComponentsPerDay });
            m.goodwill = 8;
            m.penalty = 8;
            Fill(m, ctx);
            ctx.stranger = engineer;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            if (!base.Start(m, map)) return false;
            StorylineKit.Accepted(P, Story(m), m, map, null);
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null || m.x == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            if (m.stage >= 1 && (m.x.Dead || m.x.IsPrisonerOfColony))
            {
                Missions.Goodwill(m, -m.penalty * 2);
                End(m, MissionState.Failed, "harmed", m.x.Spawned ? new LookTargets(m.x) : null);
                return;
            }
            if (m.stage == 0)
            {
                Remind(m, map, now);
                if (now < m.due) return;
                var spot = Hosting.Spot(Beds(m, map), map);
                if (!Arrive(m, map, spot.IsValid ? spot : PeoplesD.GateSpot(map), (m.deadline - now) / (float)GenDate.TicksPerHour + 6f))
                {
                    End(m, MissionState.Failed, StorylineMath.Ended);
                    return;
                }
                m.stage = 1;
                m.lastLook = now;
                m.lacks = 0;
                Note(m, map, "came", new LookTargets(m.x));
                return;
            }
            Work(m, s, map, now);
            if (Stay(m, map, now, s.yName ?? m.x.LabelShort)) return;
            string how = VaultFlow.EngineerOutcome(s.goalA, s.goalB);
            int pay = StorylineMath.Round10(s.goalA * Missions.UnitValue(ThingDefOf.ComponentIndustrial.defName) * 1.5f);
            if (pay > 0) Couriers.PaySilver(map, m.x.Spawned ? m.x.Position : DropCellFinder.TradeDropSpot(map), pay, s.giver, m.x.Spawned ? m.x : null);
            Missions.Goodwill(m, how == "worked" ? m.goodwill : m.goodwill / 2);
            Say(m.x, m, how);
            SeeOff(m, map);
            End(m, MissionState.Done, how, m.x.Spawned ? new LookTargets(m.x) : null, s.goalA.ToString(), pay.ToString());
        }

        /// <summary>Each day from eight in the morning: a component from the stores and a few hours at the bench; none in the stores, the day's work is lost.</summary>
        private void Work(Mission m, Storyline s, Map map, int now)
        {
            var p = m.x;
            int day = now / GenDate.TicksPerDay;
            if (s.baseA == day || GenLocalDate.HourOfDay(map) < 8 || GenLocalDate.HourOfDay(map) > 17 || p == null || !p.Spawned || p.Map != map) return;
            if (!Hosting.Free(p) || (p.needs?.food?.CurLevelPercentage ?? 1f) < Hosting.EatBelow || Hosting.WantsSleep(p)) return;
            var bench = VaultRules.Bench(map, p);
            var job = WorkJob;
            if (bench == null || job == null) return;
            s.baseA = day;
            if (Missions.Take(map, ThingDefOf.ComponentIndustrial.defName, VaultFlow.ComponentsPerDay) < VaultFlow.ComponentsPerDay)
            {
                s.goalB++;
                Note(m, map, "nopart", new LookTargets(p), MessageTypeDefOf.NegativeEvent);
                return;
            }
            s.goalA++;
            var j = JobMaker.MakeJob(job, bench);
            j.count = Mathf.RoundToInt(VaultFlow.WorkHoursPerDay * GenDate.TicksPerHour);
            p.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            var map = m.Map;
            if (s == null || map == null || m.x == null) return null;
            if (m.stage == 0) return "CC_Story3_Vault_Coming".Translate((s.yName ?? "?").Named("RIVAL"), TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve();
            return "CC_Story3_Vault_Working".Translate(s.goalA.Named("WORKED"), s.goalB.Named("SHORT"), (VaultRules.Bench(map, null)?.LabelShort ?? "?").Named("BENCH")).Resolve() + " " + MealsText(m);
        }

        public string DebugNow(Mission m, Map map)
        {
            if (m.stage == 0)
            {
                m.due = Find.TickManager.TicksGame;
                return "the engineer comes now.";
            }
            Story(m).baseA = -1;
            return "today's work starts at the next look (from eight in the morning).";
        }
    }

    // ===================== Part 3: the sealed case =====================

    /// <summary>
    /// The vault leaves a sealed case in the colony's care for five to seven days: their runner brings it within a day and
    /// sets it down by the colony's stores. Half a day later the card asks: keep it sealed (their runner takes it back at the
    /// end of the days: returned), or open it now (opened: a sleeper, a machine or old goods). The case gone in the meantime:
    /// lost.
    /// </summary>
    public class HappeningWorker_VaultCase : HappeningWorker_StorylinePart, IStorylineDebugNow
    {
        public const string Card = "Rimfolk_Storyline_VaultCard";

        protected override int Rating => 2;

        protected override bool Possible(Map map) => Storylines.current?.giver != null && VaultRules.CaseDef != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            if (s?.giver == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            var runner = s.x != null && StoryChains.CanVisit(s.x) ? s.x : Arrivals.Make(Arrivals.PersonKind(s.giver), s.giver, new FloatRange(25f, 55f));
            if (runner == null) return false;
            Arrivals.Hold(runner);
            s.x = runner;
            s.xName = runner.LabelShort;
            m.x = runner;
            m.faction = s.giver;
            m.place = s.place;
            m.wait = Rand.Range(VaultFlow.CaseMinDays, VaultFlow.CaseMaxDays);
            m.days = m.wait + 1.5f;
            m.goodwill = 6;
            Fill(m, ctx);
            ctx.stranger = runner;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null || m.x == null) return false;
            m.stage = 0;
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(Rand.Range(0.3f, 0.8f) * GenDate.TicksPerDay);
            StorylineKit.Accepted(P, s, m, map, null);
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            switch (m.stage)
            {
                case 0:
                {
                    // The runner comes with the case in their pack and sets it down by the stores.
                    if (now < m.due) return;
                    if (MapFacts.ThreatOn(map) && now < m.deadline - GenDate.TicksPerDay) return;
                    var c = HomeQuests.Make(VaultRules.CaseDef);
                    var at = DropCellFinder.TradeDropSpot(map);
                    if (c == null || m.x == null || Arrivals.Visit(new List<Pawn> { m.x }, map, s.giver, at, 6f) == null)
                    {
                        End(m, MissionState.Failed, StorylineMath.Ended);
                        return;
                    }
                    m.x.inventory?.innerContainer.TryAdd(c);
                    s.thing = c;
                    m.thing = c;
                    m.cell = at;
                    m.stage = 1;
                    m.lastLook = now;
                    return;
                }
                case 1:
                {
                    var c = VaultRules.CaseOn(s, map);
                    var runner = m.x;
                    bool there = runner != null && runner.Spawned && runner.Position.InHorDistOf(m.cell, 6f);
                    if (c == null)
                    {
                        End(m, MissionState.Failed, StorylineMath.Ended);
                        return;
                    }
                    if (!there && now - m.lastLook < GenDate.TicksPerHour * 6) return;
                    if (c.ParentHolder is Pawn_InventoryTracker) RelicRules.SetDown(c, map, runner != null && runner.Spawned ? runner.Position : m.cell, false);
                    if (runner != null && runner.Spawned) Arrivals.SendAway(new[] { runner });
                    m.stage = 2;
                    m.lastLook = now;
                    m.due = now + Mathf.RoundToInt(m.wait * GenDate.TicksPerDay);
                    m.deadline = Mathf.Max(m.deadline, m.due + GenDate.TicksPerDay);
                    Note(m, map, "left", new LookTargets(c));
                    return;
                }
                case 2:
                {
                    // In the colony's care: half a day later, the card.
                    if (VaultRules.CaseOn(s, map) == null)
                    {
                        Missions.Goodwill(m, -m.goodwill);
                        End(m, MissionState.Failed, "lost");
                        return;
                    }
                    if (StorylineKit.CardOut(s) || now - m.lastLook < VaultFlow.CardAfterHours * GenDate.TicksPerHour) return;
                    m.stage = 3;
                    var c = s.thing;
                    if (!StorylineKit.Ask(Card, s, m, map, ctx => ctx.place = c)) Answered(m, s, StorylineKit.Context(s, m, map), 0, true);
                    return;
                }
                case 3:
                    if (VaultRules.CaseOn(s, map) == null && !StorylineKit.CardOut(s))
                    {
                        Missions.Goodwill(m, -m.goodwill);
                        End(m, MissionState.Failed, "lost");
                    }
                    return;
                case 4:
                {
                    // Kept sealed: their runner comes back for it at the end of the days.
                    var c = VaultRules.CaseOn(s, map);
                    if (c == null)
                    {
                        Missions.Goodwill(m, -m.goodwill);
                        End(m, MissionState.Failed, "lost");
                        return;
                    }
                    if (now < m.due) return;
                    if (MapFacts.ThreatOn(map) && now < m.deadline + GenDate.TicksPerDay) return;
                    m.stage = 5;
                    m.lastLook = now;
                    if (!Couriers.CollectThings(map, new List<Thing> { c }, s.giver, new List<Thing>())) Returned(m, s, c);
                    return;
                }
                default:
                {
                    var c = s.thing;
                    if (c == null || c.Destroyed || HomeQuests.HeldBy(c, s.giver) || c.MapHeld != map || now - m.lastLook > GenDate.TicksPerDay) Returned(m, s, c);
                    return;
                }
            }
        }

        private void Returned(Mission m, Storyline s, Thing c)
        {
            if (c != null && !c.Destroyed && c.MapHeld != null && !HomeQuests.HeldBy(c, s.giver)) c.Destroy();
            Missions.Goodwill(m, m.goodwill);
            End(m, MissionState.Done, "returned");
        }

        public override void Answered(Mission m, Storyline s, HappeningContext ctx, int option, bool byDefault)
        {
            var map = m.Map;
            var c = VaultRules.CaseOn(s, map);
            if (c == null)
            {
                End(m, MissionState.Failed, "lost");
                return;
            }
            if (option != 1)
            {
                m.stage = 4;
                return;
            }
            Open(m, s, c, map);
        }

        /// <summary>Opened: by the roll, a sleeper (who joins or leaves), a machine (within the harm budget, else old goods), or old goods.</summary>
        private void Open(Mission m, Storyline s, Thing c, Map map)
        {
            var at = c.PositionHeld;
            string what = VaultFlow.Contents(Rand.Value);
            if (what == "machine" && !Machine(m, s, map, at)) what = "goods";
            if (what == "sleeper" && !Sleeper(m, s, map, at)) what = "goods";
            if (what == "goods")
            {
                var goods = Loot.Make(VaultFlow.GoodsWorth(LivingEvents.WealthFactor(map)));
                foreach (var t in goods) GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near);
                s.Set("goods");
            }
            if (!c.Destroyed) c.Destroy();
            s.thing = null;
            End(m, MissionState.Done, "opened", new LookTargets(new TargetInfo(at, map)));
        }

        /// <summary>A sleeper of the vault's people wakes: joins the colony half the time, else goes home at once.</summary>
        private bool Sleeper(Mission m, Storyline s, Map map, IntVec3 at)
        {
            var p = Arrivals.Make(Arrivals.PersonKind(s.giver), s.giver, new FloatRange(20f, 45f));
            if (p == null) return false;
            if (Rand.Chance(VaultFlow.SleeperJoins))
            {
                if (!Arrivals.Join(p, map, at))
                {
                    Arrivals.Discard(p);
                    return false;
                }
                s.Set("joined");
            }
            else
            {
                if (!Arrivals.Spawn(p, map, at))
                {
                    Arrivals.Discard(p);
                    return false;
                }
                Arrivals.SendAway(new[] { p });
                s.Set("ranoff");
            }
            s.name = p.LabelShort;
            s.Set("sleeper");
            return true;
        }

        /// <summary>A machine of the game's own wakes and fights, scaled to the colony, within the shared harm budget.</summary>
        private bool Machine(Mission m, Storyline s, Map map, IntVec3 at)
        {
            var mechs = Faction.OfMechanoids;
            if (mechs == null || !IncidentWorker_Happening.FightsPossible || !Harm.On || !Harm.SystemOn(HarmKinds.StoryFight.system) || !Harm.Roll(HarmKinds.StoryFight, 1f)) return false;
            float power = StorytellerUtility.DefaultThreatPointsNow(map) * VaultFlow.MachineShare;
            var kind = HomeQuests.FighterKind(mechs, Mathf.Max(100f, power));
            if (kind == null || kind.RaceProps?.IsMechanoid != true) return false;
            try
            {
                var mech = PawnGenerator.GeneratePawn(kind, mechs);
                GenSpawn.Spawn(mech, CellFinder.RandomClosewalkCellNear(at, map, 2), map);
                LordMaker.MakeNewLord(mechs, new LordJob_AssaultColony(mechs, false, false, false, false, false), map, new List<Pawn> { mech });
                s.name = mech.LabelShort;
                s.Set("machine");
                StorylineKit.NoteLetter(P, s, m, map, "machine", new LookTargets(mech), LetterDefOf.ThreatBig);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the machine in the case", e);
                return false;
            }
        }

        public override void Describe(Mission m, Storyline s, string choice, HappeningContext ctx, ImpactSheet sheet)
        {
            switch (choice)
            {
                case "keep":
                    sheet.Goodwill(s.giver, VaultFlow.ReturnedGoodwill);
                    sheet.Play("CC_Story3_Vault_ImpactKeep".Translate((s.giver?.Name ?? "?").Named("FACTION")).Resolve(), "CC_Story3_Vault_ImpactKeepBrief".Translate().Resolve());
                    break;
                case "open":
                    sheet.Goodwill(s.giver, VaultFlow.OpenedGoodwill);
                    sheet.Play("CC_Story3_Vault_ImpactOpen".Translate().Resolve(), "CC_Story3_Vault_ImpactOpenBrief".Translate().Resolve());
                    break;
            }
        }

        public override LookTargets Targets(Mission m)
        {
            var s = Story(m);
            var c = s?.thing;
            if (c != null && !c.Destroyed && c.SpawnedOrAnyParentSpawned) return new LookTargets(c.SpawnedParentOrMe);
            return m.x != null && m.x.Spawned ? new LookTargets(m.x) : LookTargets.Invalid;
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            if (s == null) return null;
            switch (m.stage)
            {
                case 0:
                case 1: return "CC_Story3_Vault_CaseComing".Translate((s.xName ?? "?").Named("GIVER")).Resolve();
                case 3: return "CC_Story3_Vault_CaseAsking".Translate().Resolve();
                default:
                    return "CC_Story3_Vault_CaseKept".Translate(TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve();
            }
        }

        public string DebugNow(Mission m, Map map)
        {
            int now = Find.TickManager.TicksGame;
            switch (m.stage)
            {
                case 0:
                    m.due = now;
                    return "the runner comes with the case now.";
                case 2:
                    m.lastLook = now - Mathf.RoundToInt(VaultFlow.CardAfterHours * GenDate.TicksPerHour) - 1;
                    return "the card comes now.";
                case 4:
                    m.due = now;
                    return "the runner comes back for the case now.";
            }
            return "nothing to bring on now (stage " + m.stage + ").";
        }
    }

    /// <summary>
    /// A guest at one of the colony's work benches for a few hours (the vault's engineer): at the bench's place, the game's
    /// smithing look and sound, for the job's count of ticks.
    /// </summary>
    public class JobDriver_StorylineBenchWork : JobDriver
    {
        public const string JobName = "Rimfolk_Storyline_BenchWork";

        private Thing Bench => job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        public override string GetReport() => "CC_Story3_Vault_WorkReport".Translate((Bench?.LabelShort ?? "").Named("BENCH")).Resolve();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating == StoryDanger.High);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            var work = ToilMaker.MakeToil("StorylineBenchWork");
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(600, job.count);
            work.handlingFacing = true;
            work.tickIntervalAction = delta => pawn.rotationTracker.FaceTarget(Bench);
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.WithEffect(() => DefDatabase<EffecterDef>.GetNamedSilentFail("Smith") ?? EffecterDefOf.ConstructMetal, TargetIndex.A);
            yield return work;
        }
    }
}
