using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    [DefOf]
    public static class SickCupDefOf
    {
        public static JobDef Rimfolk_SickCup;
        public static ThoughtDef Rimfolk_SickCup_Got, Rimfolk_SickCup_Gave;

        static SickCupDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(SickCupDefOf));
    }

    /// <summary>
    /// A cup for the sick (C24 of docs/content-update-proposals.md; the player's go of 20 November 2026): someone with a
    /// cold, a cough or a sore throat (SickCupMath.Ailments) is brought something hot by one of the colony at their leisure
    /// who thinks well of them. The bringer takes a cup from the stores (a tea of Vanilla Brewing Expanded - Coffees and Teas
    /// or of Vanilla Brewing Expanded, Vanilla Christmas Expanded's hot cocoa, or honey of Alpha Bees or Vanilla Factions
    /// Expanded - Medieval 2: the first the stores hold that the sick one will have), carries it over, hands it over with a
    /// word, and the sick one drinks it there. The ailment eases a little, so it ends a few hours sooner, and the two think a
    /// little better of each other (Rimfolk_SickCup_Got, Rimfolk_SickCup_Gave). Never against a drug policy, a faith or a
    /// teetotaller (FirstPot.WillDrink); with none of these mods nothing is brought. One a day for each who is ill, waking
    /// hours only, at most one set going every two hours on a map, under the "Small habits" setting. Nothing saved: the one
    /// a day is the memory of the cup. No harm.
    /// </summary>
    public static class SickCup
    {
        public const string Pool = "Rimfolk_Pool_SickCup";

        private static List<ThingDef> drinks;
        private static List<HediffDef> ailments;

        /// <summary>The drinks the player's mods have, in the order they're brought.</summary>
        public static List<ThingDef> Drinks => drinks ??= SickCupMath.Drinks.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d?.ingestible != null).ToList();

        private static List<HediffDef> Ailments => ailments ??= SickCupMath.Ailments.Select(n => DefDatabase<HediffDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();

        public static bool On => SmallHabits.On && Drinks.Count > 0;

        /// <summary>When a cup was last set going on each map (this session).</summary>
        private static readonly Dictionary<int, int> lastOnMap = new Dictionary<int, int>();
        private static Game game;

        /// <summary>Their worst ailment a cup is for, showing (not still hidden), or null.</summary>
        public static Hediff Ailment(Pawn p)
        {
            var hs = p?.health?.hediffSet;
            if (hs == null) return null;
            Hediff worst = null;
            foreach (var h in hs.hediffs)
            {
                if (h == null || !Ailments.Contains(h.def) || !h.Visible) continue;
                var im = h.TryGetComp<HediffComp_AilmentImmunizable>();
                if (im != null && im.Incubating) continue;
                if (worst == null || h.Severity > worst.Severity) worst = h;
            }
            return worst;
        }

        /// <summary>They had a cup from someone today (the memory of it lasts the day).</summary>
        private static bool HadOne(Pawn p)
        {
            var mem = p.needs?.mood?.thoughts?.memories?.Memories;
            if (mem == null || SickCupDefOf.Rimfolk_SickCup_Got == null) return false;
            foreach (var m in mem)
                if (m?.def == SickCupDefOf.Rimfolk_SickCup_Got && !SickCupMath.Due(Find.TickManager.TicksGame - m.age, Find.TickManager.TicksGame, GenDate.TicksPerDay)) return true;
            return false;
        }

        /// <summary>Ill with one of them, awake, up to drinking, and not drafted, downed or in a state.</summary>
        public static bool Sick(Pawn p) =>
            p != null && p.Spawned && !p.Dead && !p.Downed && p.Awake() && !p.Drafted && !p.InMentalState && p.needs?.food != null && Ailment(p) != null;

        /// <summary>The first drink in the stores the sick one will have that the bringer can reach and take, or null.</summary>
        public static Thing CupFor(Pawn by, Pawn sick)
        {
            foreach (var d in Drinks)
            {
                if (!FirstPot.WillDrink(sick, d)) continue;
                Thing best = null;
                float bestDist = float.MaxValue;
                foreach (var t in by.Map.listerThings.ThingsOfDef(d))
                {
                    if (!t.Spawned || !t.IsInAnyStorage() || t.IsForbidden(by) || !t.Position.InHorDistOf(by.Position, SickCupMath.Reach)) continue;
                    float dist = t.Position.DistanceToSquared(by.Position);
                    if (dist >= bestDist || !by.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.None)) continue;
                    best = t;
                    bestDist = dist;
                }
                if (best != null) return best;
            }
            return null;
        }

        /// <summary>Who brings it: of the colony at their leisure, not ill themselves, who think well of them (opinion 0 or more), the one who thinks best of them.</summary>
        public static Pawn BringerFor(Pawn sick)
        {
            Pawn best = null;
            int bestOpinion = int.MinValue;
            foreach (var o in sick.Map.mapPawns.FreeColonistsSpawned)
            {
                if (o == sick || !SmallHabits.CanListen(o) || Ailment(o) != null || o.DevelopmentalStage.Baby() || !o.Position.InHorDistOf(sick.Position, SickCupMath.Reach)) continue;
                if (!(o.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) ?? false)) continue;
                int opinion = o.relations?.OpinionOf(sick) ?? 0;
                if (opinion < 0 || opinion <= bestOpinion || !o.CanReach(sick, PathEndMode.Touch, Danger.None)) continue;
                best = o;
                bestOpinion = opinion;
            }
            return best;
        }

        /// <summary>Once an hour (the council's life hour): on each home map in waking hours, one who is ill may be brought a cup.</summary>
        public static void Hourly()
        {
            if (!On) return;
            if (game != Current.Game)
            {
                game = Current.Game;
                lastOnMap.Clear();
            }
            int now = Find.TickManager.TicksGame;
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome || map.dangerWatcher?.DangerRating != StoryDanger.None) continue;
                int hour = GenLocalDate.HourOfDay(map);
                if (hour < SickCupMath.FromHour || hour >= SickCupMath.ToHour) continue;
                if (lastOnMap.TryGetValue(map.uniqueID, out int last) && now >= last && now - last < SickCupMath.MapHoursBetween * GenDate.TicksPerHour) continue;
                foreach (var sick in map.mapPawns.FreeColonistsSpawned.Where(p => Sick(p) && !HadOne(p)).InRandomOrder())
                {
                    if (TrySend(sick, out _))
                    {
                        lastOnMap[map.uniqueID] = now;
                        break;
                    }
                }
            }
        }

        /// <summary>The cup sent: the bringer leaves what they were doing at their leisure to fetch it. False (with why) when it can't be now.</summary>
        public static bool TrySend(Pawn sick, out string why)
        {
            why = null;
            var by = BringerFor(sick);
            if (by == null)
            {
                why = "nobody at their leisure who thinks well of " + sick.LabelShort + ".";
                return false;
            }
            var cup = CupFor(by, sick);
            if (cup == null)
            {
                why = "no tea, hot cocoa or honey in the stores that " + sick.LabelShort + " will have.";
                return false;
            }
            var job = JobMaker.MakeJob(SickCupDefOf.Rimfolk_SickCup, cup, sick);
            job.count = 1;
            by.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return true;
        }

        /// <summary>
        /// Beside the sick one with the cup: set down by them and drunk there, a word said, the ailment eased and the two
        /// glad of each other. False when it couldn't be given (they went off, or won't have it now).
        /// </summary>
        public static bool Hand(Pawn by, Pawn sick)
        {
            var carried = by.carryTracker?.CarriedThing;
            // together: the job walked them to the sick one's side; Together.Can checks they're face to face still.
            if (carried == null || sick == null || !Together.Can(by, sick) || !Sick(sick) || !FirstPot.WillDrink(sick, carried.def)) return false;
            if (!by.carryTracker.TryDropCarriedThing(sick.Position, ThingPlaceMode.Near, out var set) || set == null) return false;
            var drink = JobMaker.MakeJob(JobDefOf.Ingest, set);
            drink.count = 1;
            sick.jobs.StartJob(drink, JobCondition.InterruptForced, null, true, true);
            var ail = Ailment(sick);
            if (ail != null) ail.Severity = SickCupMath.Eased(ail.Severity);
            // together: handed over where they stand, the two side by side.
            CouncilGameComponent.GiveThought(sick, SickCupDefOf.Rimfolk_SickCup_Got, by);
            CouncilGameComponent.GiveThought(by, SickCupDefOf.Rimfolk_SickCup_Gave, sick);
            string kind = SickCupMath.KindOf(carried.def.defName);
            string line = Flavor.FromPool(Pool, by, null, null, kind, ail != null ? "ill" : null);
            // together: said to the sick one just handed the cup, a step away (Speech.Say shows it only when they're together).
            if (!line.NullOrEmpty()) Speech.Say(by, line.Formatted(by.Named("PAWN"), sick.Named("OTHER"), carried.def.label.Named("DRINK")).Resolve(), sick);
            return true;
        }
    }

    /// <summary>
    /// The cup carried: to the stores for it, then to the sick one (following them a little if they move), set down by them
    /// and drunk there, with a word. Whatever is still in hand when it ends early is set down where they stand, for the
    /// haulers to take back. Ends at any danger.
    /// </summary>
    public class JobDriver_SickCup : JobDriver
    {
        private string sickName;

        private Pawn Sick => job.targetB.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, 1, null, errorOnFailed);

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            sickName = Sick?.LabelShort;
        }

        public override string GetReport() =>
            !sickName.NullOrEmpty() ? "CC_SickCup_Report".Translate(sickName.Named("OTHER")).Resolve() : base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOnDespawnedOrNull(TargetIndex.B);
            this.FailOn(() => Sick == null || !SickCup.Sick(Sick));
            AddFinishAction(condition =>
            {
                var held = pawn.carryTracker?.CarriedThing;
                if (held != null && SickCup.Drinks.Contains(held.def)) pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            var hand = ToilMaker.MakeToil("SickCup");
            hand.initAction = () =>
            {
                try
                {
                    if (!SickCup.Hand(pawn, Sick)) EndJobWith(JobCondition.Incompletable);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a cup for the sick", e);
                }
            };
            hand.tickIntervalAction = delta =>
            {
                var sick = Sick;
                if (sick == null || !pawn.Position.InHorDistOf(sick.Position, 5f))
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                pawn.rotationTracker.FaceTarget(sick);
            };
            hand.defaultCompleteMode = ToilCompleteMode.Delay;
            hand.defaultDuration = 240;
            hand.handlingFacing = true;
            yield return hand;
        }
    }
}
