using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>An open wish as the petition rule reads it (no game types).</summary>
    public struct WantKey
    {
        /// <summary>The wish's own number.</summary>
        public int id;
        /// <summary>What kind of wish (the def's name).</summary>
        public string def;
        /// <summary>What it is about (the wish's subject key; "" for none).</summary>
        public string subject;
        /// <summary>The thing wanted (a def's name; "" for none).</summary>
        public string thing;
        /// <summary>Who asked (any number that tells colonists apart).</summary>
        public int asker;
        /// <summary>Their colony (any number that tells colonies apart).</summary>
        public int colony;
        /// <summary>The petition it is part of (the first wish's number), or -1 on its own.</summary>
        public int petition;
        /// <summary>When it was made.</summary>
        public int created;
        /// <summary>A want of the asker's own that someone else can share (<see cref="PetitionMath.Shareable"/>).</summary>
        public bool shareable;
    }

    /// <summary>
    /// Phase 6 of the review, part 1 (the player's decision 11 of 6 November 2026: "shared wants come to the council as one
    /// petition"): when several colonists want the same thing for themselves (proper armor, a roomier bedroom), the
    /// council hears one petition with all their names, one deadline and one answer, and a want already in a petition joins
    /// it instead of coming again as a wish of its own. A wish many can back for one thing (a rec room, a sick bay) was
    /// always one wish with co-signers and stays so. No game types, so Tools/Tests can check it.
    /// </summary>
    public static class PetitionMath
    {
        /// <summary>
        /// A want of the asker's own that another's want of the same thing can join: not a wish many back for one thing
        /// (those are co-signed), not one spoken for someone else.
        /// </summary>
        public static bool Shareable(bool allowPetition, bool forSomeoneElse) => !allowPetition && !forSomeoneElse;

        /// <summary>The same want: the same kind of wish, about the same subject and thing, in the same colony.</summary>
        public static bool Same(WantKey a, WantKey b) =>
            a.def == b.def && (a.subject ?? "") == (b.subject ?? "") && (a.thing ?? "") == (b.thing ?? "") && a.colony == b.colony;

        /// <summary>
        /// The open want a new one joins (an index into <paramref name="open"/>), or -1 to be a wish of its own: the same
        /// want held by someone else, both shareable; a petition under way first (the most names), then the oldest.
        /// </summary>
        public static int Join(IList<WantKey> open, WantKey want)
        {
            if (open == null || !want.shareable) return -1;
            int best = -1, bestNames = 0, bestCreated = int.MaxValue;
            for (int i = 0; i < open.Count; i++)
            {
                var o = open[i];
                if (!o.shareable || o.asker == want.asker || !Same(o, want)) continue;
                int names = o.petition >= 0 ? Names(open, o.petition) : 1;
                if (names > bestNames || names == bestNames && o.created < bestCreated)
                {
                    best = i;
                    bestNames = names;
                    bestCreated = o.created;
                }
            }
            return best;
        }

        /// <summary>The petition a want joins: the one it is part of, else one begun with it (its own number).</summary>
        public static int PetitionOf(WantKey lead) => lead.petition >= 0 ? lead.petition : lead.id;

        /// <summary>How many open wants are in this petition.</summary>
        public static int Names(IList<WantKey> open, int petition)
        {
            int n = 0;
            if (open == null || petition < 0) return 0;
            for (int i = 0; i < open.Count; i++)
                if (open[i].petition == petition)
                    n++;
            return n;
        }

        /// <summary>Days a petition's deadline moves on for each name that joins it.</summary>
        public const float DaysPerName = 2f;

        /// <summary>
        /// One deadline for the whole petition: as it stood, two days more for the name that joins, but never past the full
        /// time a new wish of this kind would have from now.
        /// </summary>
        public static int Deadline(int petitionDeadline, int now, int fullTicks, int dayTicks)
        {
            int later = petitionDeadline + (int)(DaysPerName * dayTicks);
            return Math.Max(petitionDeadline, Math.Min(later, now + Math.Max(0, fullTicks)));
        }

        /// <summary>
        /// Whether this open want stands for its petition before the council (on the wishes page, in the inbox, for the
        /// reminders and the last call): a want on its own does; in a petition, only its first (the oldest, then the lowest
        /// number).
        /// </summary>
        public static bool Leads(IList<WantKey> open, int index)
        {
            if (open == null || index < 0 || index >= open.Count) return false;
            var w = open[index];
            if (w.petition < 0) return true;
            for (int i = 0; i < open.Count; i++)
            {
                if (i == index || open[i].petition != w.petition) continue;
                if (open[i].created < w.created || open[i].created == w.created && open[i].id < w.id) return false;
            }
            return true;
        }

        /// <summary>The open wishes as the council counts them (the cap on how many wait at once): a petition once.</summary>
        public static int Count(IList<WantKey> open)
        {
            int n = 0;
            if (open == null) return 0;
            for (int i = 0; i < open.Count; i++)
                if (Leads(open, i))
                    n++;
            return n;
        }

        /// <summary>
        /// A want answered on the map while others in its petition still wait: its own thanks come as a word (a message),
        /// and the letter waits for the last of them, which names everyone.
        /// </summary>
        public static bool LetterAtLast(int stillWaiting) => stillWaiting == 0;
    }
}
