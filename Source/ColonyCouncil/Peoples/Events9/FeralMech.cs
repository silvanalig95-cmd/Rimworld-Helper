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
    // ===================== 85. A mech gone feral (the Lattice) =====================

    /// <summary>
    /// A mech gone feral (item 85): one of the Lattice's light war mechs has slipped its link and roams near the colony, attacking whatever
    /// comes close (it answers to nobody: the game's own hostile mechs), and two link-wardens of the Lattice come to the gate after it. The
    /// colony may let them relink it: they call it over the link from the gate, it walks to them, one of them relinks it there (sparks and a
    /// bar), and the Lattice pays for the trouble. Or the colony takes it down with the wardens beside its people (the game's fight), and
    /// keeps the wreck for its parts. Or the wardens go home and it roams until it wanders off the map. The drovers' bull's kind in the
    /// shared harm budget (a visiting people's beast gone loose). Biotech only. One of the events' visits (kind "pev:feral"): x the first
    /// warden, a the mech, the second warden in its group; its text "people|stage" ("asked", "relink", "down", "home").
    /// </summary>
    public static class FeralMech
    {
        public const string Kind = "pev:feral";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The Lattice's light war mechs loaded (Biotech's, Alpha Mechs'), with their combat power.</summary>
        public static List<PawnKindDef> Kinds() =>
            PeopleEvents9Math.FeralKinds.Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n)).Where(k => k?.race != null && k.RaceProps.IsMechanoid).ToList();

        /// <summary>The kind for this home: the strongest within a share of its threat points, else the weakest.</summary>
        public static PawnKindDef KindFor(Map map)
        {
            var kinds = Kinds();
            int i = PeopleEvents9Math.FeralPick(kinds.Select(k => k.combatPower).ToArray(), StorytellerUtility.DefaultThreatPointsNow(map) * PeopleEvents9Math.FeralPoints);
            return i >= 0 ? kinds[i] : null;
        }

        /// <summary>Where it roams: open ground a fair walk out from the colony toward the map's edge, reachable.</summary>
        public static bool Spots(Map map, out IntVec3 edge, out IntVec3 roam)
        {
            roam = IntVec3.Invalid;
            if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map) && map.reachability.CanReachColony(c), map, CellFinder.EdgeRoadChance_Neutral, out edge)) return false;
            var home = CampSite.ColonyCenter(map);
            var v = (edge - home).ToVector3();
            float len = Mathf.Max(1f, v.magnitude);
            var mid = home + IntVec3.FromVector3(v / len * Mathf.Min(len * 0.6f, Rand.Range(25f, 40f)));
            var home2 = map.areaManager.Home;
            return CellFinder.TryFindRandomCellNear(mid.ClampInsideMap(map), map, 10, c => c.Standable(map) && !c.Fogged(map) && !c.Roofed(map) && c.GetEdifice(map) == null
                                                                                         && (home2 == null || !home2[c]) && map.reachability.CanReachColony(c), out roam);
        }

        public static Pawn MakeMech(PawnKindDef kind)
        {
            if (kind == null || Faction.OfMechanoids == null) return null;
            try
            {
                return PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, Faction.OfMechanoids, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: the feral mech", e);
                return null;
            }
        }

        private static string Stage(Visit v)
        {
            var p = (v?.text ?? "").Split('|');
            return p.Length > 1 ? p[1] : "asked";
        }

        private static void SetStage(Visit v, string stage)
        {
            var p = (v.text ?? "").Split('|');
            v.text = (p.Length > 0 ? p[0] : "") + "|" + stage;
        }

        public static Visit VisitOf(Pawn mech) => CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v != null && v.kind == Kind && !v.done && v.a == mech);

        /// <summary>The event's beginning: the mech in at the edge and out to where it roams, the wardens at the gate, the visit kept.</summary>
        public static bool Loose(HappeningContext ctx, IntVec3 edge, IntVec3 roam)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var mech = ctx?.pet;
            if (map == null || t == null || mech == null) return false;
            try
            {
                GenSpawn.Spawn(mech, edge, map);
                LordMaker.MakeNewLord(mech.Faction, new LordJob_DefendPoint(roam, 8f, 12f, false, false), map, new List<Pawn> { mech });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: the feral mech in", e);
                return false;
            }
            HarmBook.Instance?.Came(HarmKinds.DroversBull, Now);
            var v = t.AddVisit(Kind, ctx.incident, map, ctx.stranger, mech, ctx.group, Now + PeopleEvents9Math.FeralRoamHours * GenDate.TicksPerHour);
            v.text = ctx.faction.GetUniqueLoadID() + "|asked";
            return true;
        }

        /// <summary>The colony lets the wardens relink it: called over the link, it walks to the gate, and a warden relinks it there.</summary>
        public static void Relink(HappeningContext ctx)
        {
            var v = VisitOf(ctx?.pet);
            var mech = ctx?.pet;
            var f = ctx?.faction;
            if (v == null || mech == null || f == null) return;
            SetStage(v, "relink");
            if (mech.Dead || !mech.Spawned)
            {
                Over(v);
                return;
            }
            try
            {
                Lords.Leave(mech);
                mech.SetFaction(f);
                var gate = ctx.stranger != null && ctx.stranger.Spawned ? ctx.stranger.Position : PeoplesD.GateSpot(ctx.map) ?? mech.Position;
                LordMaker.MakeNewLord(f, new LordJob_VisitOnErrand(f, gate, 6 * GenDate.TicksPerHour), ctx.map, new List<Pawn> { mech });
                mech.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: the mech called in", e);
            }
            if (!PlayOuts.On || !PeopleEvents9.Send(ctx.stranger, PeopleEvents9.RelinkJob, mech, PeopleEvents9Math.RelinkTicks)) Relinked(ctx.stranger, mech);
        }

        /// <summary>The colony takes it down, the wardens beside its people: their group goes after it (the game's fight).</summary>
        public static void Down(HappeningContext ctx)
        {
            var v = VisitOf(ctx?.pet);
            if (v == null) return;
            SetStage(v, "down");
            var wardens = PeoplesG.Party(ctx).Where(p => PeoplesG.Up(p, ctx.map)).ToList();
            if (wardens.Count == 0 || ctx.pet == null || ctx.pet.Dead) return;
            Lords.Leave(wardens);
            LordMaker.MakeNewLord(ctx.faction, new LordJob_AssaultThings(ctx.faction, new List<Thing> { ctx.pet }, 1f, false), ctx.map, wardens);
        }

        /// <summary>The wardens sent home; it roams until it wanders off.</summary>
        public static void LetBe(HappeningContext ctx)
        {
            var v = VisitOf(ctx?.pet);
            if (v != null) SetStage(v, "home");
        }

        /// <summary>The relink done at the gate: the mech the Lattice's again, the colony paid for the trouble, the wardens and the mech home.</summary>
        public static void Relinked(Pawn warden, Pawn mech)
        {
            var v = VisitOf(mech);
            if (v == null || v.done) return;
            v.done = true;
            var f = warden?.Faction ?? PeopleEvents9.PeopleOf(v);
            var map = v.map;
            if (mech != null && !mech.Dead && f != null)
            {
                if (mech.Faction != f) mech.SetFaction(f);
                if (mech.Spawned) FleckMaker.ThrowMicroSparks(mech.DrawPos, mech.Map);
                int pay = PeopleEvents9Math.FeralPay(mech.MarketValue);
                Couriers.PaySilver(map, warden != null && warden.Spawned ? warden.Position : mech.PositionHeld, pay, f, PeoplesG.Up(warden, map) ? warden : null);
                PeoplesG.SayLine(warden, v.incident, "relinked");
                Tell.Message(NewsKind.People, Importance.Notable, map, "CC_PEv9_Relinked".Translate((warden?.LabelShort ?? "?").Named("WARDEN"), mech.LabelShort.Named("MECH"), pay.Named("COUNT")).Resolve(),
                    mech.Spawned ? new LookTargets(mech) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
                PeopleEvents9.Goodwill(f, PeopleEvents9Math.RelinkGoodwill, GoodwillWhy.Returned);
            }
            PeoplesG.Home(v.group.Prepend(v.x).Append(mech));
        }

        /// <summary>
        /// The visit's hour: the mech down or destroyed (its wreck the colony's, the wardens home), a relink still to do (the warden sent
        /// again), its hours past (it wanders off, the wardens home).
        /// </summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var mech = v.a;
            var map = v.map;
            string stage = Stage(v);
            if (mech == null || mech.Dead || mech.Downed || !mech.Spawned || mech.Map != map)
            {
                Over(v);
                return false;
            }
            if (stage == "relink")
            {
                var x = v.x;
                if (PeoplesG.Up(x, map) && x.CurJobDef != PeopleEvents9.RelinkJob && !PeopleEvents9.Send(x, PeopleEvents9.RelinkJob, mech, PeopleEvents9Math.RelinkTicks) && now > v.until)
                {
                    Relinked(x, mech);
                    return false;
                }
                if (!PeoplesG.Up(x, map) && now > v.until) Relinked(x, mech);
                return !v.done;
            }
            if (now <= v.until) return true;
            // Its hours past: it wanders off, and the wardens go home.
            v.done = true;
            try
            {
                Lords.Leave(mech);
                LordMaker.MakeNewLord(mech.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Walk, false, false), map, new List<Pawn> { mech });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: the feral mech off", e);
            }
            PeoplesG.Home(v.group.Prepend(v.x));
            return false;
        }

        /// <summary>The mech down or destroyed: finished where it lies, its wreck the colony's to strip for parts, told; the wardens home.</summary>
        private static void Over(Visit v)
        {
            if (v.done) return;
            v.done = true;
            var mech = v.a;
            var map = v.map;
            if (mech != null && !mech.Dead && mech.Downed && mech.Spawned && mech.Faction == Faction.OfMechanoids)
            {
                try
                {
                    mech.Kill(null);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 9: the feral mech finished", e);
                }
            }
            var wreck = mech?.Corpse;
            if (wreck != null && wreck.Spawned && wreck.Map == map)
            {
                wreck.SetForbidden(false, false);
                var warden = v.group.Prepend(v.x).FirstOrDefault(p => PeoplesG.Up(p, map));
                if (warden != null) PeoplesG.SayLine(warden, v.incident, "destroyed");
                Tell.Message(NewsKind.People, Importance.Notable, map, "CC_PEv9_FeralDown".Translate(mech.LabelShort.Named("MECH")).Resolve(), new LookTargets(wreck), MessageTypeDefOf.PositiveEvent);
                PeopleEvents9.Goodwill(PeopleEvents9.PeopleOf(v), PeopleEvents9Math.DownGoodwill, GoodwillWhy.Helped);
            }
            PeoplesG.Home(v.group.Prepend(v.x));
        }
    }

    /// <summary>
    /// Two link-wardens of {FACTION} ({X}) at the gate: a {TALE} of the Lattice's has slipped its link and roams near the colony, attacking
    /// whatever comes close. Biotech only; the drovers' bull's kind in the shared harm budget.
    /// </summary>
    public class HappeningWorker_FeralMech : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            ModsConfig.BiotechActive && Faction.OfMechanoids != null && PeoplesD.Friendly(PeoplesMath.Mechanitors) != null && FeralMech.Kinds().Count > 0
            && Harm.On && Harm.SystemOn(HarmKinds.DroversBull.system) && IncidentWorker_Happening.BattlesAllowed(map) && !MapFacts.ThreatOn(map) && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Mechanitors);
            if (ctx.faction == null || !Harm.Roll(HarmKinds.DroversBull, PeopleEvents9Math.FeralChance)) return false;
            if (!FeralMech.Spots(ctx.map, out var edge, out var roam)) return false;
            ctx.pet = FeralMech.MakeMech(FeralMech.KindFor(ctx.map));
            if (ctx.pet == null) return false;
            ctx.spot = edge;
            // Where it roams, until it is let in: the note.
            ctx.note = roam.x + "," + roam.z;
            ctx.tale = ctx.pet.KindLabel;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Lattice_Warden"), ctx.faction, ext.ages);
            ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Lattice_Warden"), ctx.faction, ext.ages));
            ctx.group.RemoveAll(p => p == null);
            if (ctx.stranger == null)
            {
                Arrivals.Discard(ctx.pet);
                return false;
            }
            ctx.count = PeopleEvents9Math.FeralPay(ctx.pet.MarketValue);
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Shooting);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            if (Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) == null) return false;
            return FeralMech.Loose(ctx, ctx.spot, Roam(ctx));
        }

        protected override void Abort(HappeningContext ctx)
        {
            if (ctx.pet != null && !ctx.pet.Spawned) Arrivals.Discard(ctx.pet);
            ctx.pet = null;
            base.Abort(ctx);
        }

        private static IntVec3 Roam(HappeningContext ctx)
        {
            var p = (ctx?.note ?? "").Split(',');
            return p.Length == 2 && int.TryParse(p[0], out int x) && int.TryParse(p[1], out int z) ? new IntVec3(x, 0, z) : ctx?.spot ?? IntVec3.Invalid;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => PeopleEvents.AgeTags(tags, ctx.faction);

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            var mech = ctx.pet;
            if (mech == null || mech.Dead || mech.Downed || !mech.Spawned) return "CC_PEv9_FeralGone".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "relink" : option == 1 ? "down" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>
    /// The colony's answer about the feral mech: <see cref="how"/> "relink" (the wardens call it in and relink it at the gate; the Lattice
    /// pays for the trouble), "down" (taken down with the wardens beside the colony's people; the wreck the colony's), "home" (the wardens
    /// go home; it roams until it wanders off).
    /// </summary>
    public class DilemmaEffect_FeralMech : DilemmaEffect
    {
        public string how = "relink";

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.pet == null) return;
            switch (how)
            {
                case "relink":
                    sheet.Play("CC_PEv9_Impact_Relink".Translate(hc.stranger?.LabelShort ?? "?", hc.tale ?? "?").Resolve(), "CC_PEv9_Impact_RelinkBrief".Translate().Resolve());
                    sheet.Silver(hc.count);
                    if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents9Math.RelinkGoodwill);
                    break;
                case "down":
                    sheet.Play("CC_PEv9_Impact_Down".Translate(hc.tale ?? "?").Resolve(), "CC_PEv9_Impact_DownBrief".Translate().Resolve());
                    sheet.Add(ImpactKind.Goods, 1, "CC_PEv9_Impact_Wreck".Translate(hc.tale ?? "?").Resolve(), "CC_PEv9_Impact_WreckBrief".Translate().Resolve(), "pev9wreck");
                    sheet.Add(ImpactKind.Health, -1, "CC_PEv9_Impact_Fight".Translate().Resolve(), "CC_PEv9_Impact_FightBrief".Translate().Resolve(), "pev9fight");
                    break;
                default:
                    sheet.Add(ImpactKind.Other, -1, "CC_PEv9_Impact_Roams".Translate(hc.tale ?? "?").Resolve(), "CC_PEv9_Impact_RoamsBrief".Translate().Resolve(), "pev9roams");
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null || hc.pet == null) return;
            switch (how)
            {
                case "relink":
                    FeralMech.Relink(hc);
                    break;
                case "down":
                    FeralMech.Down(hc);
                    break;
                default:
                    // The wardens are sent on by the answer's own send-away.
                    FeralMech.LetBe(hc);
                    break;
            }
        }
    }
}
