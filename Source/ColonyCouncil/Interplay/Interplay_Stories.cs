using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // Personal stories hear what happens around them: a day kept, a favour returned, a friend made at a camp, back on
    // their feet after convalescing, and their nemesis met in a fight.
    public static partial class Interplay
    {
        /// <summary>
        /// Moves a story on a little: a stage waiting on days is that many days nearer, one waiting on a condition held
        /// (a good mood, belonging, low strain) has half a day more of it per day, and one waiting for a word with
        /// <paramref name="talkedWith"/> counts it as had. Returns whether anything moved. The story's own hourly look
        /// then opens the gate as usual.
        /// </summary>
        public static bool Nudge(Arc arc, float days, Pawn talkedWith = null)
        {
            var g = arc?.Gate;
            if (g == null || arc.lost) return false;
            bool moved = false;
            if (Waits(g, ArcGateKind.Time))
            {
                arc.stageTick -= (int)(days * Day);
                moved = true;
            }
            float hours = days * 12f;
            if (g.kind == ArcGateKind.Hold)
            {
                arc.held += hours;
                moved = true;
            }
            if (g.or != null)
                for (int i = 0; i < g.or.Count && i < arc.heldOr.Count; i++)
                    if (g.or[i]?.kind == ArcGateKind.Hold)
                    {
                        arc.heldOr[i] += hours;
                        moved = true;
                    }
            if (talkedWith != null && arc.talkedTick < arc.stageTick)
            {
                var t = g.kind == ArcGateKind.Talk ? g : g.or?.FirstOrDefault(x => x?.kind == ArcGateKind.Talk);
                // The colonist's own word with whoever the stage waits on (the third person, in a story of three).
                var waitsOn = t?.pair == ArcPair.Third ? arc.third : t?.pair == ArcPair.OtherAndThird ? null : arc.other;
                if (t != null && ((waitsOn != null && talkedWith == waitsOn) || (t.anyone && ArcUtil.Opinion(talkedWith, arc.pawn) >= 15))
                    && ArcUtil.Opinion(arc.pawn, talkedWith) >= t.minOpinion && ArcUtil.Opinion(talkedWith, arc.pawn) >= t.minOpinion)
                {
                    arc.talkedTick = Now;
                    arc.talkedWith = talkedWith;
                    moved = true;
                }
            }
            return moved;
        }

        private static bool Waits(ArcGate g, ArcGateKind kind) => g.kind == kind || (g.or?.Any(x => x?.kind == kind) ?? false);

        /// <summary>A story moved on by something outside it: now and then a line in the colonist's journal saying what.</summary>
        private static void NudgeAndTell(Pawn p, float days, string why, Pawn talkedWith, params NamedArgument[] args)
        {
            if (p == null || !ArcTracker.On) return;
            var arc = ArcTracker.Instance?.Of(p);
            if (arc == null || !Nudge(arc, days, talkedWith)) return;
            if (!Ready("storynudge|" + p.thingIDNumber, 3f) || !Rand.Chance(0.5f)) return;
            var all = new List<NamedArgument> { (arc.def.label ?? "").Named("STORY") };
            if (args != null) all.AddRange(args);
            Journal(p, "Council_Pool_Interplay_StoryNudge", new[] { why }, all.ToArray());
        }

        /// <summary>
        /// A calendar day was kept (CalendarKeeping.KeepDay): everyone at home whose story is waiting moves on a day,
        /// and a word they were waiting to have with someone else at home counts as had if the two are together then
        /// (Together.Can: at the day's feast or games, side by side).
        /// </summary>
        public static void Notify_DayKept(CalendarDay d, List<Pawn> home)
        {
            if (!On || d == null || home.NullOrEmpty() || !ArcTracker.On) return;
            Safe("interplay: day kept", () =>
            {
                var tracker = ArcTracker.Instance;
                foreach (var p in home)
                {
                    var arc = tracker?.Of(p);
                    if (arc == null) continue;
                    var with = arc.other != null && home.Contains(arc.other) && Together.Can(p, arc.other) ? arc.other : null;
                    NudgeAndTell(p, 1f, "day", with, (d.label ?? "").Named("NAME"));
                }
            });
        }

        /// <summary>
        /// A people's old debt to the colony was repaid (DilemmaEffect_Remember settling a good turn the colony did): the
        /// stories of those who met the envoy move on two days. A good turn come home lifts more than the one who did it.
        /// </summary>
        public static void Notify_DeedSettled(DilemmaContext ctx, FactionDeed deed)
        {
            if (!On || ctx == null || deed == null || deed.side != DeedSide.Favor || !FactionMemory.On) return;
            Safe("interplay: favour returned", () =>
            {
                var f = deed.faction ?? ctx.faction;
                foreach (var p in new[] { ctx.a, ctx.b })
                    if (Ours(p))
                        NudgeAndTell(p, 2f, "favour", null, (f?.Name ?? "?").Named("FACTION"));
            });
        }

        /// <summary>
        /// A camp has gone, and a colonist made a friend at its fire (CampLife's farewell): their story moves on a day,
        /// and their strain eases a little for the friendship.
        /// </summary>
        public static void Notify_CampFriend(Pawn friend, Pawn them)
        {
            // apart: the camp has gone; the friendship was made face to face at their fire.
            if (!On || !Ours(friend)) return;
            Safe("interplay: camp friend", () =>
            {
                NudgeAndTell(friend, 1f, "camp", null, (them?.LabelShort ?? "?").Named("NAME"));
                if (Heart.Eligible(friend)) Ease(friend, 2f, StrainRelief.Friends);
            });
        }

        /// <summary>Back to full strength after convalescing (HealthTracker): their story, if it waited, moves on two days.</summary>
        public static void Notify_Recovered(Pawn p)
        {
            if (!On || !Ours(p) || !HealthTracker.ConvalescenceOn) return;
            Safe("interplay: recovered", () => NudgeAndTell(p, 2f, "recovered", null));
        }

        // ---------------- A fear faced ----------------

        /// <summary>
        /// Hourly: someone whose story is a fear of their nemesis, who has just gone for that very enemy in a fight, has
        /// faced it. The story ends there, well, whatever stage it had reached.
        /// </summary>
        private static void FearFacedWatch(CouncilGameComponent comp)
        {
            if (!ArcTracker.On || !S.nemeses) return;
            var tracker = ArcTracker.Instance;
            if (tracker == null || tracker.active.Count == 0) return;
            int now = Now;
            foreach (var arc in tracker.active.ToList())
            {
                if (arc?.def == null || arc.lost || arc.tag != "nemesis" || !(arc.def.Worker is ArcWorker_Fear)) continue;
                var p = arc.pawn;
                if (p == null || !p.Spawned || p.Dead || p.mindState == null) continue;
                if (now - p.mindState.lastAttackTargetTick > GenDate.TicksPerHour * 2) continue;
                if (!(p.mindState.lastAttackedTarget.Thing is Pawn target)) continue;
                var n = comp.nemeses.For(target);
                if (n == null || !n.announced || n.victim != p) continue;
                tracker.FinishNow(arc);
            }
        }
    }
}
