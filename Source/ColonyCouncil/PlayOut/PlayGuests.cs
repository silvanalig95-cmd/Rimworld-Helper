using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Guests at one of the colony's gatherings (<see cref="DilemmaEffect_Gathering.guests"/>): the event's visitor and
    /// those with them, the camp's people, or the visitor and their family. They aren't of the gathering (they keep their
    /// own people's plans), so while it lasts each is walked to a place round it and kept there, and at a feast served a
    /// meal from the colony's stores like everyone else. Never a prisoner, anyone leaving, hostile or not up to it.
    /// </summary>
    public static class PlayGuests
    {
        public const string Stranger = "stranger", Camp = "camp", Kin = "kin";

        /// <summary>Who comes, now the gathering is starting.</summary>
        public static List<Pawn> Resolve(PlayOut p, Map map)
        {
            var list = new List<Pawn>();
            if (p == null || map == null || p.guestsFrom.NullOrEmpty()) return list;
            int cap = Math.Max(1, p.guestCount);
            var x = p.other;
            switch (p.guestsFrom)
            {
                case Stranger:
                    if (Able(x, map))
                    {
                        list.Add(x);
                        var lord = x.GetLord();
                        if (lord != null) list.AddRange(lord.ownedPawns.Where(o => o != x && o.RaceProps.Humanlike && Able(o, map)).Take(cap - 1));
                    }
                    break;
                case Camp:
                {
                    var camp = CampOnMap.Of(map);
                    if (camp != null) list.AddRange(camp.Here.Where(o => Able(o, map)).OrderByDescending(o => o == x).Take(cap));
                    break;
                }
                case Kin:
                {
                    if (!Able(x, map)) break;
                    list.Add(x);
                    var around = (CampOnMap.Of(map)?.Here ?? new List<Pawn>()).Concat(x.GetLord()?.ownedPawns ?? new List<Pawn>());
                    list.AddRange(around.Where(o => o != x && Able(o, map) && (ArcUtil.Kin(o, x) || LovePartnerRelationUtility.LovePartnerRelationExists(o, x))).Distinct().Take(cap - 1));
                    break;
                }
            }
            return list.Distinct().ToList();
        }

        /// <summary>Up to coming: here, calm, awake, not hostile, not a prisoner, not on their way out.</summary>
        public static bool Able(Pawn o, Map map)
        {
            if (o == null || o.Dead || !o.Spawned || o.Map != map || o.Downed || o.InMentalState || !o.RaceProps.Humanlike) return false;
            if (o.Faction == Faction.OfPlayer || o.HostileTo(Faction.OfPlayer) || o.IsPrisoner || o.IsSlave || !o.Awake() || o.InBed()) return false;
            var duty = o.mindState?.duty?.def;
            return duty == null || !duty.defName.StartsWith("ExitMap");
        }

        /// <summary>The words for who came ("Ula, Bram and the others").</summary>
        public static string Names(List<Pawn> guests)
        {
            var names = guests.Where(g => g != null).Take(3).Select(g => g.LabelShort).ToList();
            if (guests.Count > 3) names.Add("CC_Play_GuestsOthers".Translate().Resolve());
            return TextUtil.JoinAnd(names);
        }

        /// <summary>
        /// A look while it lasts: each guest at a place round the spot (the i-th round it), or, at a feast with meals
        /// to serve, sent to eat one first. <paramref name="served"/> and <paramref name="fed"/> are the gathering's own.
        /// </summary>
        public static void Keep(List<Pawn> guests, Map map, IntVec3 spot, bool serve, List<Pawn> served, List<Pawn> fed, string label)
        {
            if (guests.NullOrEmpty() || map == null) return;
            List<Thing> meals = null;
            for (int i = 0; i < guests.Count; i++)
            {
                var g = guests[i];
                if (!Able(g, map) || g.CurJobDef == JobDefOf.Ingest || g.CurJobDef == PlayOutDefOf.Council_PlayOutSpeak) continue;
                if (g.needs?.food != null && g.needs.food.CurCategory >= HungerCategory.UrgentlyHungry && !serve) continue;
                if (serve && !served.Contains(g) && !fed.Contains(g))
                {
                    meals ??= PlaySpots.SortedMeals(map, spot);
                    var meal = PlaySpots.BestMeal(g, meals, out _);
                    if (meal != null)
                    {
                        served.Add(g);
                        var eat = JobMaker.MakeJob(JobDefOf.Ingest, meal);
                        eat.count = 1;
                        // together: a guest at the colony's table, served like everyone else.
                        g.jobs.StartJob(eat, JobCondition.InterruptForced, null, false, true);
                        continue;
                    }
                }
                if (g.CurJobDef == PlayOutDefOf.Council_PlayOutAttend) continue;
                var at = PlaySpots.RingCell(map, spot, i + 2, g, null);
                if (!at.IsValid || !g.CanReach(at, PathEndMode.OnCell, Danger.Some)) at = spot;
                if (!g.CanReach(at, PathEndMode.OnCell, Danger.Some)) continue;
                var job = JobMaker.MakeJob(PlayOutDefOf.Council_PlayOutAttend, at, spot);
                job.expiryInterval = -1;
                g.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
        }
    }

    /// <summary>A guest at a gathering: to their place round it, then there a while, facing it, in company.</summary>
    public class JobDriver_PlayOutAttend : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var stay = ToilMaker.MakeToil("PlayOutAttend");
            stay.defaultCompleteMode = ToilCompleteMode.Delay;
            stay.defaultDuration = 900;
            stay.socialMode = RandomSocialMode.SuperActive;
            stay.handlingFacing = true;
            stay.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceCell(job.targetB.Cell);
                PawnUtility.GainComfortFromCellIfPossible(pawn, delta);
            };
            yield return stay;
        }
    }
}
