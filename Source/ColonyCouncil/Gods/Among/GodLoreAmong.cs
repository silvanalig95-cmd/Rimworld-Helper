using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The colony's book of the gods, the parts the gods among the people write into (Gods/Among/).</summary>
    public partial class GodLore
    {
        /// <summary>
        /// The keeper walked out to where a sign was seen and looked (Gods/Among/KeeperAtWork.cs): the entry counts them among
        /// those who saw what is left there, and says so after its words, in the keeper's own voice.
        /// </summary>
        public static void KeeperLooked(LoreEntry e, Pawn keeper)
        {
            var b = Book;
            if (b == null || e == null || keeper == null) return;
            try
            {
                string name = keeper.LabelShort;
                if (e.who == null) e.who = new List<string>();
                if (e.who.Contains(name)) return;
                e.who.Add(name);
                Keeper(out var voice);
                string after = ("CC_GA_KeeperLooked_" + voice).Translate(name.Named("KEEPER")).Resolve();
                if (!after.NullOrEmpty()) e.text = (e.text ?? "") + " " + after;
                b.seen[name] = (b.seen.TryGetValue(name, out int n) ? n : 0) + 1;
                Version++;
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("gods: the book, the keeper looked", ex);
            }
        }
    }
}
