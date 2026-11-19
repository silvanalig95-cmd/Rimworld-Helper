using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 57. The other side's contract (the Ashen Banner) =====================

    /// <summary>
    /// The other side's contract (item 57): when the storyteller sends a raid on a home and a people at war with the colony, at
    /// peace with the Banner, could be the one sending it, the Banner may have been hired to march with it (at a chance, not
    /// often). The raid is held back in the game's own queue a day or so, as the relay mast and the watch hold things back
    /// (Patch_BannerHired, after their holds), and comes from that people; a sergeant of the Banner comes to the gate under a
    /// white flag to say so, as the Banner keeps its contracts and says them. The colony can buy the contract (the company
    /// stands with the colony against that raid, beside it when it comes), pay them to stand aside (the raid comes as it was
    /// sent), or let them march: the raid comes at the storyteller's own size, a part of it the Banner's soldiers, under that
    /// people's colours. A raid that never comes out of the queue has the colony's silver sent back.
    /// </summary>
    public static class OtherSideContract
    {
        public const string EventDef = "Rimfolk_Evt_OtherSideContract";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The raid coming out of the queue now that the Banner was hired for (between the prefix and the postfix).</summary>
        private static HireRecord coming;

        /// <summary>A people who could have hired the Banner against the colony: at war with the colony, at peace with the Banner, able to raid.</summary>
        public static bool Hirer(Faction f, Faction banner, Map map) =>
            f != null && banner != null && f != banner && !f.HostileTo(banner) && Raids.CanRaid(f, map) && f.def.humanlikeFaction;

        /// <summary>
        /// The storyteller sends a raid (Patch_BannerHired): with the Banner at peace and not lately hired, a people that could have
        /// hired it, and the sergeant able to come, at a chance: the raid held a day or so and comes from that people. True: held.
        /// </summary>
        public static bool Hold(FiringIncident fi)
        {
            if (fi?.parms == null || fi.sourceQuestPart != null || fi.parms.quest != null || !(fi.parms.target is Map map) || !map.IsPlayerHome) return false;
            var b = PeopleEvents6Book.Instance;
            var banner = PeoplesD.Friendly(PeoplesMath.Mercenaries);
            int now = Now;
            if (b == null || banner == null || b.lastHire >= 0 && now - b.lastHire < PeopleEvents6Math.HireGapDays * GenDate.TicksPerDay || !Tell.CanAsk) return false;
            if (b.hires.Any(r => r != null && !r.done && r.map == map)) return false;
            var enemy = Hirer(fi.parms.faction, banner, map) ? fi.parms.faction
                : fi.parms.faction == null ? Find.FactionManager.AllFactionsListForReading.Where(f => Hirer(f, banner, map)).RandomElementWithFallback() : null;
            if (enemy == null || fi.parms.points < enemy.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat) * 1.2f) return false;
            if (!Rand.Chance(PeopleEvents6Math.HireChance * StoryDirection.Factor(StoryWaysMath.Arms))) return false;
            var was = fi.parms.faction;
            fi.parms.faction = enemy;
            if (!fi.def.Worker.CanFireNow(fi.parms))
            {
                fi.parms.faction = was;
                return false;
            }
            int at = now + PeopleEvents6Math.HoldHours(Rand.Value) * GenDate.TicksPerHour;
            if (!Find.Storyteller.incidentQueue.Add(new QueuedIncident(fi, at, 12 * GenDate.TicksPerHour)))
            {
                fi.parms.faction = was;
                return false;
            }
            var r = new HireRecord { map = map, faction = banner, enemy = enemy, def = fi.def.defName, points = fi.parms.points, due = at };
            b.AddHire(r);
            b.lastHire = now;
            // The sergeant to the gate; should they not come after all, the raid comes a day late as it was sent, nobody of the
            // Banner in it and nothing said of them.
            if (!PeopleEvents2.Fire(EventDef, map, banner)) r.done = true;
            return true;
        }

        /// <summary>The newest raid held on this map that the colony hasn't seen come.</summary>
        public static HireRecord Pending(Map map) => PeopleEvents6Book.Instance?.hires.LastOrDefault(r => r != null && !r.done && !r.came && r.map == map);

        /// <summary>The held raid comes out of the queue (Patch_BannerHired): marching with it, the Banner's part of its points kept back for their soldiers.</summary>
        public static void Before(FiringIncident fi)
        {
            coming = null;
            var map = fi?.parms?.target as Map;
            var r = Pending(map);
            if (r == null || r.def != fi.def.defName || fi.parms.faction != r.enemy || Now < r.due - GenDate.TicksPerHour) return;
            coming = r;
            if (r.choice == "march")
                fi.parms.points = Mathf.Max(r.enemy.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat) * 1.05f, r.points * (1f - PeopleEvents6Math.BannerShare));
        }

        /// <summary>The held raid couldn't come this time (the game's queue tries it again): nothing kept for it.</summary>
        public static void NotYet() => coming = null;

        /// <summary>The held raid came: the Banner's soldiers among it, beside the colony, or standing aside, as the colony answered.</summary>
        public static void After(FiringIncident fi)
        {
            var r = coming;
            coming = null;
            if (r == null) return;
            r.came = true;
            r.done = true;
            var map = r.map;
            var banner = r.faction;
            if (map == null || banner == null) return;
            var at = fi.parms.spawnCenter.IsValid && fi.parms.spawnCenter.InBounds(map) ? fi.parms.spawnCenter : CampSite.ColonyCenter(map);
            var from = fi.parms.raidArrivalMode?.walkIn == true ? at : LegendBeasts.EdgeToward(map, at);
            switch (r.choice)
            {
                case "march":
                    March(r, map, from);
                    return;
                case "buyout":
                    Stand(r, map);
                    return;
                default:
                    Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PEv6_HireAside".Translate(banner.Name.Named("FACTION"), (r.enemy?.Name ?? "?").Named("ENEMY")).Resolve(),
                        LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                    return;
            }
        }

        /// <summary>The Banner's soldiers among the raid, under the hirer's colours, worth its kept-back part: in beside the raiders, in their fight.</summary>
        private static void March(HireRecord r, Map map, IntVec3 from)
        {
            var enemy = r.enemy;
            if (enemy == null || !from.IsValid) return;
            float share = r.points * PeopleEvents6Math.BannerShare, made = 0f;
            var list = new List<Pawn>();
            for (int i = 0; i < 8 && made < share; i++)
            {
                var kind = PeoplesD.KindNamed(r.faction, i == 0 ? "Rimfolk_AshenBanner_Sergeant" : i % 2 == 0 ? "Rimfolk_AshenBanner_Shot" : "Rimfolk_AshenBanner_Soldier");
                var p = Arrivals.Make(kind, enemy, new FloatRange(20f, 50f));
                if (p == null) continue;
                if (!Arrivals.Spawn(p, map, from))
                {
                    Arrivals.Discard(p);
                    break;
                }
                list.Add(p);
                made += Mathf.Max(35f, kind.combatPower);
            }
            if (list.Count == 0) return;
            try
            {
                var lord = map.lordManager.lords.LastOrDefault(l => l.faction == enemy && l.ownedPawns.Count > 0);
                if (lord != null) lord.AddPawns(list);
                else LordMaker.MakeNewLord(enemy, new LordJob_AssaultColony(enemy, true, false), map, list);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: the Banner marches with the raid", e);
            }
            Tell.Message(NewsKind.Land, Importance.Important, map, "CC_PEv6_HireMarched".Translate(r.faction.Name.Named("FACTION"), (enemy.Name ?? "?").Named("ENEMY"), list.Count.Named("COUNT")).Resolve(),
                new LookTargets(list), MessageTypeDefOf.ThreatBig);
        }

        /// <summary>The contract the colony bought: the company in from the colony's side, standing with it against the raid.</summary>
        private static void Stand(HireRecord r, Map map)
        {
            var f = r.faction;
            if (f.HostileTo(Faction.OfPlayer) || !Arrivals.TryEntryCell(map, out var entry)) return;
            int n = PeopleEvents6Math.Allies(r.points);
            var list = new List<Pawn>();
            for (int i = 0; i < n; i++)
            {
                var kind = PeoplesD.KindNamed(f, i == 0 ? "Rimfolk_AshenBanner_Sergeant" : i % 2 == 0 ? "Rimfolk_AshenBanner_Shot" : "Rimfolk_AshenBanner_Soldier");
                var p = Arrivals.Make(kind, f, new FloatRange(20f, 50f));
                if (p == null) continue;
                if (!Arrivals.Spawn(p, map, entry))
                {
                    Arrivals.Discard(p);
                    break;
                }
                list.Add(p);
            }
            if (list.Count == 0) return;
            try
            {
                LordMaker.MakeNewLord(f, new LordJob_AssistColony(f, CampSite.ColonyCenter(map)), map, list);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: the company stands with the colony", e);
            }
            // together: the sergeant's word to the colony as the company comes in on its side.
            PeoplesG.SayLine(list[0], DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), "stand");
            Tell.Letter(NewsKind.Land, Importance.Important, map, "CC_PEv6_HireStandLabel".Translate().Resolve(),
                "CC_PEv6_HireStand".Translate(f.Name.Named("FACTION"), (r.enemy?.Name ?? "?").Named("ENEMY"), list.Count.Named("COUNT")).Resolve(), LetterDefOf.PositiveEvent,
                new LookTargets(list), f);
        }

        /// <summary>The events' hour: a held raid that never came out of the queue is let go, the colony's silver sent back.</summary>
        public static void Watch(int now)
        {
            var b = PeopleEvents6Book.Instance;
            if (b == null) return;
            foreach (var r in b.hires.Where(r => r != null && !r.done && !r.came && now > r.due + GenDate.TicksPerDay))
            {
                r.done = true;
                if (r.map == null || r.faction == null || r.choice == "march" || !Find.Maps.Contains(r.map)) continue;
                int paid = r.choice == "buyout" ? PeopleEvents6Math.BuyOutPrice(r.points) : PeopleEvents6Math.StandAsidePrice(r.points);
                Couriers.PaySilver(r.map, CampSite.ColonyCenter(r.map), paid, r.faction);
                Tell.Message(NewsKind.Land, Importance.Notable, r.map, "CC_PEv6_HireNotCome".Translate(r.faction.Name.Named("FACTION"), (r.enemy?.Name ?? "?").Named("ENEMY")).Resolve(),
                    LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            }
        }

        /// <summary>The colony's answer: "buyout", "aside" or "march".</summary>
        public static void Terms(HappeningContext ctx, string choice)
        {
            var r = Pending(ctx?.map);
            if (r == null) return;
            r.choice = choice;
            var f = r.faction;
            if (!PeopleEvents6.Thanks(f)) return;
            if (choice == "buyout") WorldSituationWorker.Goodwill(f, PeopleEvents6Math.BuyOutGoodwill, GoodwillWhy.Paid);
            else if (choice == "aside") WorldSituationWorker.Goodwill(f, PeopleEvents6Math.StandAsideGoodwill, GoodwillWhy.Paid);
        }

        /// <summary>Dev mode: the storyteller's raid on this home, held as if the Banner had been hired for it.</summary>
        public static string Soon(Map map)
        {
            var b = PeopleEvents6Book.Instance;
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail("RaidEnemy");
            if (map == null || b == null || def == null) return "no raid to hold.";
            b.lastHire = -1;
            var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
            for (int i = 0; i < 20; i++)
            {
                if (Hold(new FiringIncident(def, null, parms))) return "a raid is held; the Banner's sergeant is at the gate.";
                b.lastHire = -1;
            }
            return "no raid held (no Banner at peace, no people at war with the colony and at peace with the Banner, or the dice said no twenty times).";
        }
    }

    /// <summary>
    /// A sergeant of {FACTION} ({X}) at the gate under a white flag: {ENEMY} have hired the Banner for their next raid on the
    /// colony. The colony may buy the contract for {COUNT} silver, or pay {DEAD} for them to stand aside.
    /// </summary>
    public class HappeningWorker_OtherSideContract : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Mercenaries) != null && OtherSideContract.Pending(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var r = OtherSideContract.Pending(ctx.map);
            ctx.faction = r?.faction;
            if (r == null || ctx.faction == null || r.enemy == null) return false;
            ctx.enemy = r.enemy;
            ctx.count = PeopleEvents6Math.BuyOutPrice(r.points);
            ctx.variant = PeopleEvents6Math.StandAsidePrice(r.points);
            ctx.dead = ctx.variant.ToString();
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_AshenBanner_Sergeant"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => PeopleEvents.AgeTags(tags, ctx.faction);

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "bought" : option == 1 ? "aside" : "march");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The colony's answer to the sergeant (OtherSideContract.Terms): <see cref="choice"/> "buyout", "aside" or "march".</summary>
    public class DilemmaEffect_OtherSideContract : DilemmaEffect
    {
        public string choice = "march";

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            string enemy = hc.enemy?.Name ?? "?";
            switch (choice)
            {
                case "buyout":
                    sheet.Add(ImpactKind.People, 1, "CC_PEv6_Impact_HireBuy".Translate(hc.faction?.Name ?? "?", enemy).Resolve(), "CC_PEv6_Impact_HireBuyBrief".Translate().Resolve(), "pev6hirebuy");
                    if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents6Math.BuyOutGoodwill);
                    break;
                case "aside":
                    sheet.Add(ImpactKind.Other, 0, "CC_PEv6_Impact_HireAside".Translate(hc.faction?.Name ?? "?", enemy).Resolve(), "CC_PEv6_Impact_HireAsideBrief".Translate().Resolve(), "pev6hireaside");
                    if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents6Math.StandAsideGoodwill);
                    break;
                default:
                    sheet.Add(ImpactKind.Other, -1, "CC_PEv6_Impact_HireMarch".Translate(hc.faction?.Name ?? "?", enemy).Resolve(), "CC_PEv6_Impact_HireMarchBrief".Translate().Resolve(), "pev6hiremarch");
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx) => OtherSideContract.Terms(ctx as HappeningContext, choice);
    }

    /// <summary>
    /// A raid the storyteller sends to a home (not a quest's) may be one the Banner was hired for: held in the game's queue while
    /// the sergeant comes (OtherSideContract.Hold), after the other prefixes that hold raids for a warning have had their turn;
    /// coming back out of the queue, the Banner's part of it as the colony answered (Before, After).
    /// </summary>
    [HarmonyPatch(typeof(Storyteller), nameof(Storyteller.TryFire))]
    public static class Patch_BannerHired
    {
        [HarmonyPriority(Priority.Low)]
        public static bool Prefix(FiringIncident fi, bool queued, ref bool __result, bool __runOriginal)
        {
            if (!__runOriginal) return false;
            if (!RimfolkPatches.On || fi?.def == null || !(fi.def.Worker is IncidentWorker_RaidEnemy)) return true;
            try
            {
                if (queued)
                {
                    OtherSideContract.Before(fi);
                    return true;
                }
                if (!OtherSideContract.Hold(fi)) return true;
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: the Banner hired", e);
                return true;
            }
        }

        public static void Postfix(FiringIncident fi, bool queued, bool __result)
        {
            if (!queued || !RimfolkPatches.On || fi?.def == null || !(fi.def.Worker is IncidentWorker_RaidEnemy)) return;
            try
            {
                if (__result) OtherSideContract.After(fi);
                else OtherSideContract.NotYet();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: the hired raid came", e);
            }
        }
    }
}
