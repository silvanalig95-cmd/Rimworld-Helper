using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The facts of the council's tasks and the keeper of secrets (docs/design/stage2-diplomacy.md, item 8), read by the
    // ties' provider (FactsLedger.cs).
    public static partial class Facts
    {
        /// <summary>
        /// A task of the colony's with the scene's people that they know of (the keeper's and the marshal's patrol are
        /// never talked of): us:task and us:task:KIND, with {OFFICER} the officer and {OFFICE} the seat; task:writes when the
        /// officer has written twice or more, task:visited when they visited this season, task:you when the officer is the one talking. Of the one spoken to: they caught
        /// the colony's keeper at it (them:caughtkeeper, and :you when the keeper is the one talking), they were pressed over a
        /// secret (them:pressed), by letter (them:pressed:letter) or face to face (them:pressed:face), the keeper walked with
        /// them after dark lately (keeper:watched). Only reads.
        /// </summary>
        private static void TaskFacts(FactScene s, FactSet f)
        {
            if (s?.people == null || s.people.IsPlayer || DiplomacyBook.Instance == null) return;
            CouncilTask best = null;
            foreach (var (_, t) in CouncilTasks.Running())
            {
                if (!t.Live || !TaskMath.Known(t.kind) || CouncilTasks.PeopleOf(t) != s.people) continue;
                if (best == null || s.speaker != null && t.officer == s.speaker || t.Steps > best.Steps && best.officer != s.speaker) best = t;
            }
            if (best != null)
            {
                f.Tag(TaskMath.TaskTag);
                f.Tag(TaskMath.KindTag(best.kind), 1.5f);
                f.Value(TaskMath.OfficerKey, best.OfficerName);
                f.Value(TaskMath.OfficeKey, TaskWords.Office(best.office));
                if (s.speaker != null && best.officer == s.speaker) f.Tag(TaskMath.YouTag, 2f);
                if (best.letters >= TaskMath.WritesOften) f.Tag(TaskMath.WritesTag, 1.5f);
                int now = Find.TickManager?.TicksGame ?? 0;
                if (best.visits > 0 && best.lastVisit >= 0 && now - best.lastVisit < TaskMath.SeasonDays * TaskMath.DayTicks) f.Tag(TaskMath.VisitedTag, 1.5f);
            }
            var x = s.listener;
            if (x == null) return;
            var n = Outsiders.NotableOf(x);
            if (n != null)
            {
                if (n.done.Contains(KeeperMath.CaughtKey))
                {
                    f.Tag(KeeperMath.CaughtTag, 2f);
                    if (s.speaker != null && KeeperOfSecrets.Is(s.speaker)) f.Tag(KeeperMath.CaughtYouTag, 2f);
                }
                // Pressed at all, and how: by letter or face to face, each its own tag, as their words differ (the colony's letter, the keeper at the gate).
                foreach (var key in new[] { KeeperMath.PressedKey, KeeperMath.PressedLetterKey, KeeperMath.PressedFaceKey })
                    if (n.done.Contains(key))
                        f.Tag(KeeperMath.PressedTagOf(key), 2f);
            }
            if (KeeperOfSecrets.WalkedWith(x)) f.Tag(KeeperMath.WatchedTag, 1.5f);
        }
    }
}
