using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    public enum ChronicleKind
    {
        WishMade,
        WishGranted,
        WishExpired,
        WishDismissed,
        Petition,
        Meeting,
        AmbitionChosen,
        AmbitionAchieved,
        AmbitionFailed,
        AmbitionAbandoned,
        PersonalTouch,
        Note,
        /// <summary>Decrees, dilemmas, offices, traditions.</summary>
        Council,
        /// <summary>Friendships, rivalries, mentorships, circles, social moments.</summary>
        Bond,
        /// <summary>Life goals, names, coming of age.</summary>
        Life,
        Secret,
        /// <summary>Deaths, eulogies, vendettas, heirlooms.</summary>
        Loss,
        Dream,
        /// <summary>Visitors, traders, prisoners, beasts, pets, nemeses.</summary>
        Stranger,
        /// <summary>World events: pasts catching up, challenges, wonders, visitors with a purpose.</summary>
        World
    }

    /// <summary>
    /// The chronicle as the colony's view of the one history (World/Ledger/History.cs): which of the history's events it
    /// shows, under which of its kinds, and the words of a season's summary. What it shows: every line told for the
    /// chronicle (its kinds, the world situations' "began" and "ended"), every question's answer (the answers' log,
    /// <see cref="AnswerMath"/>), and the colony's own milestones that other parts wrote with words of their own (a
    /// brigands' hold found, a trading post opened, a pact). Never a deed, a tie or a far town's news without the colony in
    /// it: those are the region window's History. Pure: Tools/Tests checks it.
    /// </summary>
    public static class ChronicleMath
    {
        /// <summary>The chronicle's kind of a history kind, or -1 for one the chronicle didn't tell (a deed, a tie, the region's own "world:camp:found").</summary>
        public static int KindOf(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return -1;
            string fam = HistoryKinds.Family(kind);
            // A small answer stays in the history and the answers' log; the chronicle never told it.
            if (fam == AnswerMath.Family) return AnswerMath.IsMinor(kind) ? -1 : (int)AnswerMath.KindOf(kind);
            // The world family is shared: the chronicle's own world lines are "world" and a situation's "world:began" and
            // "world:ended"; the region's kinds under it ("world:camp:found", "world:heard") are its own.
            if (fam == "world") return kind == "world" || kind == HistoryKinds.WorldBegan || kind == HistoryKinds.WorldEnded ? (int)ChronicleKind.World : -1;
            return Array.IndexOf(HistoryKinds.Chronicle, fam);
        }

        /// <summary>A line told for the chronicle, or a question's answer.</summary>
        public static bool Told(HistoryEntry e) => e != null && KindOf(e.kind) >= 0;

        /// <summary>
        /// The chronicle shows it: a line told for the chronicle or an answer (and their seasons' summaries), or a milestone
        /// with words of its own that happened at one of the colony's homes or had the colony or its people in it
        /// (<paramref name="ours"/>: the colony's homes, the colony as a people, its colonists).
        /// </summary>
        public static bool Shows(HistoryEntry e, Func<Ref, bool> ours)
        {
            if (e == null) return false;
            if (Told(e)) return true;
            if (e.line == null || !e.Weighty || ours == null) return false;
            // A seat's promise and how it was kept are the holder's record, not the colony's story: the election's one line
            // tells the seats (Offices/RecordMath.cs).
            if (HistoryKinds.Family(e.kind) == RecordMath.Family) return false;
            if (!e.place.IsNone && ours(e.place)) return true;
            if (e.actors != null)
                foreach (var a in e.actors)
                    if (ours(a.who))
                        return true;
            return false;
        }

        /// <summary>Under which of the chronicle's kinds it shows (its icon, its colour, its filter): its own, or a world event's for the colony's other milestones.</summary>
        public static ChronicleKind ShownAs(HistoryEntry e)
        {
            int k = e == null ? -1 : KindOf(e.kind);
            return k >= 0 ? (ChronicleKind)k : ChronicleKind.World;
        }

        /// <summary>
        /// The words of a season's summary of small lines, by their kind: "CC_ChronFold_wishMade", with "_Of" when it is of
        /// someone; null for what the chronicle didn't tell (the colony's other milestones never fold).
        /// </summary>
        public static string FoldKey(HistoryEntry e, bool ofSomeone)
        {
            if (e == null) return null;
            int k = KindOf(e.kind);
            if (k < 0) return null;
            string name = HistoryKinds.Family(e.kind) == AnswerMath.Family ? AnswerMath.Family : HistoryKinds.Chronicle[k];
            return "CC_ChronFold_" + name + (ofSomeone ? "_Of" : "");
        }

        /// <summary>Every key a summary's words may need: the chronicle's small kinds and the answers, each with and without someone.</summary>
        public static IEnumerable<string> FoldKeys()
        {
            foreach (var k in HistoryKinds.Chronicle)
            {
                if (Array.IndexOf(HistoryKinds.Milestones, k) >= 0) continue;
                yield return "CC_ChronFold_" + k;
                yield return "CC_ChronFold_" + k + "_Of";
            }
            yield return "CC_ChronFold_" + AnswerMath.Family;
            yield return "CC_ChronFold_" + AnswerMath.Family + "_Of";
        }
    }
}
