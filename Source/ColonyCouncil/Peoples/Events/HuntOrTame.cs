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
    // ===================== 95. Kill it or win it over (the Lantern Wardens and the Circuit of Tamers) =====================

    /// <summary>
    /// A legend or an alpha on the colony's land (LegendBeasts, never a beast that took a person) and the Wardens and a
    /// tamer of the Circuit at the gate the same day: the hunt or the taming. The colony sides with one, or lets them settle
    /// it in a bout at the gate; the side passed over (or the bout's loser) thinks less of the colony, and the region hears.
    /// </summary>
    public static class HuntOrTame
    {
        public const string TameKind = "pev:tame";
        public const string CoachKind = "pev:coach";

        private static int Now => Find.TickManager.TicksGame;

        public static JobDef BoutJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv_Bout");

        /// <summary>The tamer of the event's party (the Circuit's one among the Wardens).</summary>
        public static Pawn Tamer(HappeningContext ctx) => ctx?.group?.FirstOrDefault(p => p != null && p.Faction != null && p.Faction == ctx.enemy);

        public static List<Pawn> Wardens(HappeningContext ctx) => PeoplesG.Party(ctx).Where(p => p.Faction != null && p.Faction == ctx.faction).ToList();

        /// <summary>The Wardens' hunt on the beast (HappeningWorker_WardensHunt.Hunt), with only the Wardens in it.</summary>
        public static void Hunt(HappeningContext ctx, bool together)
        {
            var wardens = Wardens(ctx);
            if (wardens.Count == 0) return;
            var hunt = new HappeningContext
            {
                map = ctx.map, faction = ctx.faction, stranger = wardens[0], group = wardens.Skip(1).ToList(), a = together ? ctx.a : null, pet = ctx.pet, incident = ctx.incident,
                beast = ctx.beast, note = ctx.note
            };
            HappeningWorker_WardensHunt.Hunt(hunt, together && ctx.a != null);
        }

        /// <summary>The region hears how it went: a line of news.</summary>
        public static void Heard(Map map, string key, HappeningContext ctx, string winner = null)
        {
            News.Post(key.Translate((ctx.faction?.Name ?? "?").Named("WARDENS"), (ctx.enemy?.Name ?? "?").Named("CIRCUIT"), (ctx.beast ?? "?").Named("BEAST"),
                (winner ?? "").Named("WINNER")).Resolve(), ctx.pet != null && ctx.pet.Spawned ? new LookTargets(ctx.pet) : LookTargets.Invalid, NewsKind.World);
        }

        // ---------------- The colony's own taming, the tamer beside ----------------

        /// <summary>
        /// The taming sided with: the beast marked for taming, the colony's best hand with beasts set to it by the game's own
        /// taming (its food, its tries, its revenge at its own rate), the tamer staying by them a while; the Wardens go home.
        /// </summary>
        public static void Tame(HappeningContext ctx)
        {
            var map = ctx?.map;
            var beast = ctx?.pet;
            var tamer = Tamer(ctx);
            if (map == null || !LegendBeasts.Here(beast, map)) return;
            PeoplesG.Home(Wardens(ctx));
            try
            {
                if (map.designationManager.DesignationOn(beast, DesignationDefOf.Tame) == null) map.designationManager.AddDesignation(new Designation(beast, DesignationDefOf.Tame));
                var b = ctx.b;
                var giver = DefDatabase<WorkGiverDef>.GetNamedSilentFail("Tame")?.Worker as WorkGiver_Scanner;
                if (b != null && b.Spawned && !b.Downed && !b.Drafted && giver != null)
                {
                    var job = giver.JobOnThing(b, beast, true);
                    if (job != null) b.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
                if (PeoplesG.Up(tamer, map))
                {
                    tamer.GetLord()?.RemovePawn(tamer);
                    LordMaker.MakeNewLord(tamer.Faction, new LordJob_VisitOnErrand(tamer.Faction, beast.Position, 30 * GenDate.TicksPerHour), map, new List<Pawn> { tamer });
                    var v = CouncilGameComponent.Instance?.happenings?.AddVisit(CoachKind, ctx.incident, map, tamer, b, null, Now + 30 * GenDate.TicksPerHour);
                    if (v != null) v.place = beast;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the taming sided with", e);
            }
            Heard(map, "CC_PEv_HeardTame", ctx);
        }

        /// <summary>The tamer by the colony's taming, each hour: tamed (their word, and home), the beast gone, or a day and more past (home).</summary>
        public static bool CoachHour(Visit v, int now)
        {
            var tamer = v.x;
            var beast = v.place as Pawn;
            if (v.done || !PeoplesG.Up(tamer, v.map)) return false;
            if (beast != null && !beast.Dead && beast.Faction == Faction.OfPlayer)
            {
                v.done = true;
                PeoplesG.SayLine(tamer, v.incident, "tamed");
                Tell.Message(NewsKind.Land, Importance.Notable, v.map, "CC_PEv_TamedOurs".Translate(beast.LabelShort.Named("BEAST"), tamer.LabelShort.Named("TAMER")).Resolve(),
                    beast.Spawned ? new LookTargets(beast) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
                PeoplesG.Home(new List<Pawn> { tamer });
                return false;
            }
            if (beast == null || beast.Dead || !beast.Spawned || beast.Map != v.map || now > v.until)
            {
                PeoplesG.Home(new List<Pawn> { tamer });
                return false;
            }
            return true;
        }

        // ---------------- The bout at the gate ----------------

        /// <summary>The bout: the Warden steps up to the tamer and they spar at the gate (JobDriver_PeopleBout); with the play-outs off, its end at once.</summary>
        public static void Bout(HappeningContext ctx)
        {
            var warden = ctx?.stranger;
            var tamer = Tamer(ctx);
            var map = ctx?.map;
            if (map == null || warden == null || tamer == null) return;
            PeoplesG.Say(warden, ctx.Ext, ctx, "bout");
            if (PlayOuts.On && BoutJob != null && PeoplesG.Up(warden, map) && PeoplesG.Up(tamer, map))
            {
                var job = JobMaker.MakeJob(BoutJob, tamer);
                job.count = PeopleEventsMath.BoutTicks;
                warden.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (warden.CurJobDef == BoutJob)
                {
                    Pending[warden.thingIDNumber] = ctx;
                    return;
                }
            }
            BoutDone(warden, tamer, ctx);
        }

        /// <summary>The bouts under way this session (a bout loaded halfway is fought again from the start by nobody: they go home on their hours).</summary>
        public static readonly Dictionary<int, HappeningContext> Pending = new Dictionary<int, HappeningContext>();

        /// <summary>
        /// The bout fought: the winner by their hands (PeopleEventsMath.WardenWins, both by Melee); the Warden's way the
        /// Wardens hunt it alone, the tamer's way the tamer tries to win it over for the Circuit; the loser goes home.
        /// </summary>
        public static void BoutDone(Pawn warden, Pawn tamer, HappeningContext ctx = null)
        {
            if (ctx == null && warden != null && Pending.TryGetValue(warden.thingIDNumber, out var c)) ctx = c;
            if (warden != null) Pending.Remove(warden.thingIDNumber);
            if (ctx == null || warden == null || tamer == null) return;
            var map = ctx.map;
            int w = warden.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0, t = tamer.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
            bool wardenWon = Rand.Chance(PeopleEventsMath.WardenWins(w, t));
            var winner = wardenWon ? warden : tamer;
            var loser = wardenWon ? tamer : warden;
            PeoplesG.SayLine(winner, ctx.incident, wardenWon ? "wardenwon" : "tamerwon");
            var losers = loser.Faction;
            if (losers != null && losers.HasGoodwill && !losers.IsPlayer) WorldSituationWorker.Goodwill(losers, PeopleEventsMath.BoutLoserGoodwill);
            Tell.Message(NewsKind.Land, Importance.Notable, map,
                (wardenWon ? "CC_PEv_BoutWarden" : "CC_PEv_BoutTamer").Translate(warden.LabelShort.Named("WARDEN"), tamer.LabelShort.Named("TAMER"), (ctx.beast ?? "?").Named("BEAST")).Resolve(),
                new LookTargets(winner), MessageTypeDefOf.NeutralEvent);
            Heard(map, "CC_PEv_HeardBout", ctx, winner.Faction?.Name);
            if (wardenWon)
            {
                PeoplesG.Home(new List<Pawn> { tamer });
                Hunt(ctx, false);
                return;
            }
            PeoplesG.Home(Wardens(ctx));
            WinOver(ctx, tamer);
        }

        /// <summary>The tamer's own taming for the Circuit: near the beast, a try every two hours by the game's own chance; its revenge the game's own on a failure.</summary>
        public static void WinOver(HappeningContext ctx, Pawn tamer)
        {
            var map = ctx.map;
            var beast = ctx.pet;
            if (!PeoplesG.Up(tamer, map) || !LegendBeasts.Here(beast, map)) return;
            try
            {
                tamer.GetLord()?.RemovePawn(tamer);
                LordMaker.MakeNewLord(tamer.Faction, new LordJob_VisitOnErrand(tamer.Faction, beast.Position, (PeopleEventsMath.TameTries * PeopleEventsMath.TameTryHours + 4) * GenDate.TicksPerHour),
                    map, new List<Pawn> { tamer });
                var v = CouncilGameComponent.Instance?.happenings?.AddVisit(TameKind, ctx.incident, map, tamer, null, null,
                    Now + (PeopleEventsMath.TameTries * PeopleEventsMath.TameTryHours + 6) * GenDate.TicksPerHour);
                if (v != null) v.place = beast;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the tamer's own taming", e);
            }
        }

        /// <summary>The tamer's tries, each hour: walk up to the beast; every two hours a try; won over, it goes home with them; after six, they give up.</summary>
        public static bool TameHour(Visit v, int now)
        {
            var tamer = v.x;
            var beast = v.place as Pawn;
            var map = v.map;
            if (v.done || !PeoplesG.Up(tamer, map)) return false;
            if (beast == null || beast.Dead || !beast.Spawned || beast.Map != map || now > v.until || v.stake >= PeopleEventsMath.TameTries)
            {
                if (beast != null && !beast.Dead && beast.Faction != tamer.Faction)
                    Tell.Message(NewsKind.Land, Importance.Flavor, map, "CC_PEv_TameGaveUp".Translate(tamer.LabelShort.Named("TAMER"), (beast.LabelShort).Named("BEAST")).Resolve(),
                        new LookTargets(tamer), MessageTypeDefOf.NeutralEvent);
                PeoplesG.Home(new List<Pawn> { tamer });
                return false;
            }
            if (beast.InMentalState) return true;
            if (!tamer.Position.InHorDistOf(beast.Position, 4f))
            {
                if (tamer.CurJobDef != JobDefOf.Goto && tamer.CanReach(beast, PathEndMode.Touch, Danger.Deadly))
                {
                    var go = JobMaker.MakeJob(JobDefOf.Goto, beast.Position);
                    go.locomotionUrgency = LocomotionUrgency.Walk;
                    tamer.jobs.StartJob(go, JobCondition.InterruptForced, null, false, true);
                }
                return true;
            }
            if ((now / GenDate.TicksPerHour) % PeopleEventsMath.TameTryHours != 0) return true;
            v.stake++;
            float chance = PeopleEventsMath.TameChance(tamer.GetStatValue(StatDefOf.TameAnimalChance), beast.GetStatValue(StatDefOf.Wildness));
            tamer.rotationTracker?.FaceTarget(beast);
            if (Rand.Chance(chance))
            {
                v.done = true;
                try
                {
                    beast.SetFaction(tamer.Faction, tamer);
                    tamer.GetLord()?.AddPawn(beast);
                    PeoplesG.SayLine(tamer, v.incident, "wonover");
                    Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PEv_WonOver".Translate(tamer.LabelShort.Named("TAMER"), beast.LabelShort.Named("BEAST")).Resolve(),
                        new LookTargets(beast), MessageTypeDefOf.NeutralEvent);
                    PeoplesG.Home(new List<Pawn> { tamer, beast });
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events: the beast won over", e);
                }
                return false;
            }
            // A try failed: the game's own chance that the beast turns on whoever tried (its manhunter chance on a failed taming).
            if (Rand.Chance(PawnUtility.GetManhunterOnTameFailChance(beast)) && beast.mindState?.mentalStateHandler != null
                && beast.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter, null, true, false, false, tamer, false, false, false))
                Tell.Message(NewsKind.Land, Importance.Important, map, "CC_PEv_TameRevenge".Translate(beast.LabelShort.Named("BEAST"), tamer.LabelShort.Named("TAMER")).Resolve(),
                    new LookTargets(beast), MessageTypeDefOf.ThreatSmall);
            return true;
        }
    }

    /// <summary>
    /// A legend or an alpha is on the colony's land ({BEAST}), and the Lantern Wardens ({FACTION}) and a tamer of the Circuit
    /// ({ENEMY}) arrive the same day: the Wardens would hunt it (its hide for their book, its meat for the colony), the tamer
    /// would win it over. The colony sides with one, or lets them settle it in a bout at the gate.
    /// </summary>
    public class HappeningWorker_HuntOrTame : HappeningWorker_Visitors
    {
        public static Faction Circuit() => PeoplesD.Friendly(PeoplesMath.Tamers);

        private static Pawn Beast(Map map, out string kind) => LegendBeasts.OnLand(map, out kind, false);

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Hunters) != null && Circuit() != null && Beast(map, out _) is Pawn b && LegendBeasts.Tameable(b) && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Hunters);
            ctx.enemy = Circuit();
            ctx.pet = Beast(ctx.map, out string kind);
            if (ctx.faction == null || ctx.enemy == null || ctx.pet == null) return false;
            ctx.note = kind;
            ctx.beast = ctx.pet.LabelShort;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Wardens_Warden"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.group.Add(Arrivals.Make(PeoplesD.FighterKind(ctx.faction), ctx.faction, ext.ages));
            var tamer = Arrivals.Make(PeoplesD.KindNamed(ctx.enemy, "Rimfolk_Tamers_Tamer"), ctx.enemy, ext.ages);
            if (tamer == null) return false;
            StorytellerEvents.Skilled(tamer, SkillDefOf.Animals, new IntRange(10, 16));
            ctx.group.Add(tamer);
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Shooting, p => !p.WorkTagIsDisabled(WorkTags.Violent));
            ctx.b = PeoplesD.Best(ctx.map, SkillDefOf.Animals, p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Handling));
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null || ctx.b != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var gate = PeoplesD.GateSpot(ctx.map);
            float hours = ext.stayHours.RandomInRange;
            var wardens = HuntOrTame.Wardens(ctx);
            var tamer = HuntOrTame.Tamer(ctx);
            return Arrivals.Visit(wardens, ctx.map, ctx.faction, gate, hours) != null && Arrivals.Visit(new List<Pawn> { tamer }, ctx.map, ctx.enemy, gate, hours) != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (!ctx.note.NullOrEmpty()) tags.Add(ctx.note);
            if (ctx.pet != null && PokeWorldLink.IsPokemon(ctx.pet)) tags.Add("pokemon");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (!LegendBeasts.Here(ctx.pet, ctx.map)) return "CC_PeoplesD_BeastGone".Translate(ctx.beast ?? "?").Resolve();
            if (option == 0 && !HuntOrTame.Wardens(ctx).Any(p => PeoplesG.Up(p, ctx.map))) return "CC_PEv_WardensGone".Translate().Resolve();
            if (option == 1)
            {
                if (!LegendBeasts.Tameable(ctx.pet)) return "CC_PEv_CantTame".Translate(ctx.beast ?? "?").Resolve();
                if (ctx.b == null || ctx.b.Dead || ctx.b.Downed) return "CC_PEv_NoHandler".Translate().Resolve();
                int need = TrainableUtility.MinimumHandlingSkill(ctx.pet), have = ctx.b.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0;
                if (need > have) return "CC_PEv_HandlerTooGreen".Translate(ctx.b.LabelShort.Named("PAWN"), need.Named("NEED")).Resolve();
            }
            if (option == 2 && (!PeoplesG.Up(ctx.stranger, ctx.map) || !PeoplesG.Up(HuntOrTame.Tamer(ctx), ctx.map))) return "CC_PEv_NoBout".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault && option == 0) PeoplesG.Say(ctx.stranger, Ext, ctx, "hunt");
            if (!byDefault && option == 1) PeoplesG.Say(HuntOrTame.Tamer(ctx), Ext, ctx, "tame");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The colony's answer: "hunt" (the Wardens', {A} with them), "tame" (the colony's own taming, the tamer beside), "bout" (they settle it at the gate).</summary>
    public class DilemmaEffect_HuntOrTame : DilemmaEffect
    {
        public string side = "bout";

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.pet == null) return;
            string beast = hc.beast ?? hc.pet.LabelShort;
            switch (side)
            {
                case "hunt":
                    sheet.Play("CC_PEv_Impact_SideHunt".Translate(beast, hc.a?.LabelShort ?? "?").Resolve(), "CC_PeoplesD_Impact_HuntBrief".Translate(beast).Resolve());
                    break;
                case "tame":
                    sheet.Play("CC_PEv_Impact_SideTame".Translate(beast, hc.b?.LabelShort ?? "?").Resolve(), "CC_PEv_Impact_SideTameBrief".Translate().Resolve());
                    float revenge = PawnUtility.GetManhunterOnTameFailChance(hc.pet);
                    if (revenge > 0f)
                        sheet.Add(ImpactKind.Health, -1, "CC_PEv_Impact_Revenge".Translate(beast, revenge.ToStringPercent()).Resolve(), "CC_PEv_Impact_RevengeBrief".Translate().Resolve(), "pevrevenge");
                    break;
                default:
                    sheet.Play("CC_PEv_Impact_Bout".Translate(hc.stranger?.LabelShort ?? "?", HuntOrTame.Tamer(hc)?.LabelShort ?? "?", beast).Resolve(), "CC_PEv_Impact_BoutBrief".Translate().Resolve());
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            switch (side)
            {
                case "hunt":
                    PeoplesG.Home(new[] { HuntOrTame.Tamer(hc) });
                    HuntOrTame.Hunt(hc, true);
                    HuntOrTame.Heard(hc.map, "CC_PEv_HeardHunt", hc);
                    break;
                case "tame":
                    HuntOrTame.Tame(hc);
                    break;
                default:
                    HuntOrTame.Bout(hc);
                    break;
            }
        }
    }

    /// <summary>
    /// A friendly bout between two of the peoples' at the gate (the Warden and the tamer): up to the other, practice swings
    /// each way in turn, the other kept facing them, then who won (HuntOrTame.BoutDone). Not kept with the game: a bout
    /// loaded halfway is not fought again.
    /// </summary>
    public class JobDriver_PeopleBout : JobDriver
    {
        private int swings, since;

        private Pawn Other => job.targetA.Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Other == null || Other.Dead || Other.Downed || pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var bout = ToilMaker.MakeToil("PEvBout");
            bout.initAction = () =>
            {
                var o = Other;
                if (o != null) PawnUtility.ForceWait(o, job.count + 300, pawn);
            };
            bout.tickIntervalAction = delta =>
            {
                var o = Other;
                if (o == null) return;
                pawn.rotationTracker.FaceTarget(o);
                since += delta;
                if (since < 90) return;
                since = 0;
                swings++;
                if (swings % 2 == 1) pawn.Drawer?.Notify_MeleeAttackOn(o);
                else o.Drawer?.Notify_MeleeAttackOn(pawn);
                if (swings % 3 == 0) FleckMaker.ThrowDustPuff(o.Position, pawn.Map, 0.5f);
            };
            bout.defaultCompleteMode = ToilCompleteMode.Delay;
            bout.defaultDuration = Mathf.Max(600, job.count);
            bout.handlingFacing = true;
            bout.socialMode = RandomSocialMode.Off;
            yield return bout;
            var done = ToilMaker.MakeToil("PEvBoutDone");
            done.initAction = () => HuntOrTame.BoutDone(pawn, Other);
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }
}
