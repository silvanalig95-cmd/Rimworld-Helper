using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A day off for a friend (A9 of docs/content-update-proposals.md, the player's go of 20 November 2026): a kind colonist,
    /// or someone close to a colonist who has stood worn down or near breaking two days running, asks the council for a day
    /// off for them, as a wish for another (the asker's own wish, about them: Rimfolk_Wish_FriendDayOff). Met when that
    /// colonist is given the heart's day off (the existing button, Heart/HeartTimeOff.cs) or rests on the calendar's rest day
    /// while it waits. Then the one who asked gives thanks (the wish's own), the two grow a little closer (the social web, and
    /// the worn one thinks better of them for speaking up), and often the one who asked keeps them company for an hour of it:
    /// a visit on the map with answers playing out, else the day off's own "time with a friend" (HeartActs.FriendVisit), the
    /// two meeting for a talk. Let lapse, only the one who asked loses trust; the worn one isn't told (not named in the
    /// lapse, and not counted among the friends who take it personally, CouncilDynamics.Expire). No harm.
    /// Kept apart from colony life's first variety build: never asked while "worn thin" runs for them or within days of its
    /// end, nor for a seat holder whose step-down question waits or came lately; and while it waits, neither of those two
    /// begins for them (CouncilLife/LifeArcs.cs, StepDown.cs).
    /// </summary>
    public class WishWorker_FriendDayOff : WishWorker
    {
        public const string DefName = "Rimfolk_Wish_FriendDayOff";

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn asker)
        {
            var map = HomeMap(asker);
            if (map == null || !map.IsPlayerHome || !HeartTimeOff.DayOffOn || !asker.DevelopmentalStage.Adult() || asker.relations == null) yield break;
            bool kind = HasTrait(asker, TraitDefOf.Kind);
            int now = Find.TickManager.TicksGame;
            var cal = ColonyCalendar.For(map);
            foreach (var x in map.mapPawns.FreeColonistsSpawned)
            {
                if (x == asker || !x.DevelopmentalStage.Adult() || x.IsQuestLodger() || !Heart.Eligible(x)) continue;
                var h = Heart.Of(x);
                if (h == null || h.AnyOff || h.strain < HeartCountMath.DayOffFrom) continue;
                float worn = StepDownMath.DaysSince(h.wornSince, now, GenDate.TicksPerDay);
                if (!FriendDayOffMath.WornLongEnough(worn)) continue;
                // A day off can be given now (its five days' wait past), and today isn't a rest day already.
                if (HeartCountMath.DayOffReadyIn((now - h.lastDayOffTick) / (float)GenDate.TicksPerDay) > 0f) continue;
                if (cal != null && cal.RestingToday(map, x)) continue;
                float close = HeartAdvice.ClosenessOf(asker, x);
                if (!FriendDayOffMath.MayAsk(kind, close, asker.relations.OpinionOf(x))) continue;
                if (FriendDayOff.AskedFor(x) || FriendDayOff.KeptApart(x, now)) continue;
                var c = Candidate(FriendDayOffMath.Weight(close, kind, h.breakingSince >= 0),
                    "CC_FriendDayOff_Reason".Translate(asker.Named("PAWN"), x.Named("OTHER")).Resolve());
                c.other = x;
                c.subjectKey = x.ThingID;
                yield return c;
            }
        }

        public override bool IsFulfilled(Wish wish)
        {
            var x = wish.other;
            var h = Heart.Of(x);
            if (x == null || h == null) return false;
            bool resting = x.Spawned && (ColonyCalendar.For(x.Map)?.RestingToday(x.Map, x) ?? false);
            return FriendDayOffMath.Met(h.Off(TimeOffKind.DayOff), h.lastDayOffTick, wish.createdTick, resting);
        }

        /// <summary>
        /// Still makes sense while they are one of the colony at the same home and still strained enough for a day off; once
        /// they are themselves again (or gone) it is let go quietly, with no blame.
        /// </summary>
        public override bool StillValid(Wish wish)
        {
            var x = wish.other;
            if (x == null || x.Dead || !x.IsColonist || x.MapHeld != wish.pawn?.MapHeld) return false;
            var h = Heart.Of(x);
            return h != null && (h.AnyOff || h.strain >= HeartCountMath.DayOffFrom);
        }

        /// <summary>The heart, or its day off, switched off since: nothing the council could give, so no blame.</summary>
        public override bool StillObtainable(Wish wish) => base.StillObtainable(wish) && HeartTimeOff.DayOffOn;

        public override LookTargets LookAt(Wish wish) => new LookTargets(new[] { wish.pawn, wish.other }.Where(p => p != null && p.Spawned));

        /// <summary>The worn one isn't told when it lapses: not among the friends of the one who asked who take it personally.</summary>
        public override bool OtherNotTold => true;

        /// <summary>Met: the two grow closer, and the one who asked often keeps them company for an hour of it.</summary>
        public override void OnMet(Wish wish)
        {
            var asker = wish.pawn;
            var x = wish.other;
            var comp = CouncilGameComponent.Instance;
            if (asker == null || x == null || comp == null || x.Dead) return;
            comp.social.Bump(asker, x, TieKind.Friends, FriendDayOffMath.Closer, "CC_FriendDayOff_TieNote".Translate(asker.Named("PAWN"), x.Named("OTHER")).Resolve());
            CouncilGameComponent.GiveThought(x, CouncilDefOf.Council_SpokeUpForMe, asker);
            if (!Rand.Chance(FriendDayOffMath.CompanyChance)) return;
            FriendDayOff.Company(asker, x);
        }
    }

    /// <summary>The day off for a friend's look-ups for the other parts (CouncilLife/, Heart/HeartAdvice.cs) and its company.</summary>
    public static class FriendDayOff
    {
        public const string WornThinArc = "Rimfolk_Arc_WornThin", StepDownQuestion = "Rimfolk_Dilemma_StepDown";

        /// <summary>The open wish asking a day off for <paramref name="x"/>, or null.</summary>
        public static Wish For(Pawn x)
        {
            var comp = CouncilGameComponent.Instance;
            if (x == null || comp == null) return null;
            for (int i = 0; i < comp.wishes.Count; i++)
            {
                var w = comp.wishes[i];
                if (w != null && w.state == WishState.Open && w.other == x && w.def?.defName == WishWorker_FriendDayOff.DefName) return w;
            }
            return null;
        }

        /// <summary>Someone has asked the council for a day off for them, and it waits.</summary>
        public static bool AskedFor(Pawn x) => For(x) != null;

        /// <summary>
        /// Colony life's first variety build has them in the same days: "worn thin" under way for them or ended lately, or
        /// (a seat holder) the council's question about stepping down waiting for its answer or asked lately.
        /// </summary>
        public static bool KeptApart(Pawn x, int now)
        {
            var arcs = ArcTracker.Instance;
            if (arcs != null)
            {
                if (arcs.Of(x)?.def?.defName == WornThinArc) return true;
                foreach (var r in arcs.PastOf(x))
                    if (r != null && r.defName == WornThinArc && FriendDayOffMath.Within(r.endTick, now, FriendDayOffMath.ApartDays, GenDate.TicksPerDay))
                        return true;
            }
            var letters = Find.LetterStack?.LettersListForReading;
            if (letters != null)
                for (int i = 0; i < letters.Count; i++)
                    if (letters[i] is ChoiceLetter_Dilemma d && !d.resolved && d.ctx?.a == x && d.ctx.def?.defName == StepDownQuestion)
                        return true;
            if (OfficeTracker.HeldBy(x) == null) return false;
            int last = DilemmaTracker.Of(x)?.LastAsked(StepDownQuestion) ?? -1;
            return FriendDayOffMath.Within(last, now, FriendDayOffMath.ApartDays, GenDate.TicksPerDay);
        }

        /// <summary>
        /// The one who asked keeps them company: with answers playing out, a visit of about an hour on the map (they walk over
        /// and stay at their side), else the day off's own time with a friend (they meet for a talk).
        /// </summary>
        public static void Company(Pawn asker, Pawn x)
        {
            try
            {
                if (PlayOuts.On && asker.Spawned && x.Spawned && asker.Map == x.Map)
                {
                    var ctx = new DilemmaContext { map = x.Map, a = asker, b = x };
                    var visit = new DilemmaEffect_Visit
                    {
                        label = "CC_FriendDayOff_VisitLabel".Translate(asker.Named("PAWN"), x.Named("OTHER")).Resolve(),
                        who = PlayWho.A, whom = PlayWho.B, to = PlaySpot.Person, hours = new IntRange(8, 21), duration = new FloatRange(1f, 1.25f)
                    };
                    if (visit.Begin(ctx) != null) return;
                }
                if (x.Spawned) HeartActs.FriendVisit(x, asker);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a day off for a friend: their company", e);
            }
        }

        /// <summary>The heart's advice on a day off for them: who has asked the council for one, or null.</summary>
        public static string AdviceNote(Pawn x)
        {
            var w = For(x);
            return w?.pawn == null ? null : "CC_FriendDayOff_Advice".Translate(w.pawn.Named("PAWN"), x.Named("OTHER")).Resolve();
        }
    }
}
