using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's renown in the windows (no acts, it is earned; nothing in This week). Since phase 4 of the review, part 1
    /// (one name), renown shows inside the colony's one name: its card (Character/ColonyName.cs, on Council → Ways and on a
    /// home's card, where the colony is shown to others) says how far the name carries and for what, with the lines from here
    /// below it (what its name comes from and what is held against it, the notables who know its people, what it does); each
    /// colonist's own name abroad is part of their one "known as" (Epithets/KnownAs.cs, on their card and People → Renown).
    /// The houses of release 2d.1 had cards and a page of their own; both went with them (5 November 2026).
    /// </summary>
    public static class RenownPanels
    {
        private static string P(string key, params NamedArgument[] args) => RenownWords.P(key, args);

        /// <summary>What the colony's name comes from (renown's deeds, largest first), or null.</summary>
        public static string From(RenownMath.Reading r)
        {
            string good = RenownWords.Parts(r, true);
            return good == null ? null : P("CC_Renown_Card_From", good.Named("PARTS"));
        }

        /// <summary>What is held against it, or null.</summary>
        public static string Against(RenownMath.Reading r)
        {
            string bad = RenownWords.Parts(r, false, 3);
            return bad == null ? null : P("CC_Renown_Card_Against", bad.Named("PARTS"));
        }

        /// <summary>The notables who know its people, and what its name does (the notables' regard), as lines.</summary>
        public static List<string> Reach(RenownMath.Reading r)
        {
            var lines = new List<string>();
            if (r == null) return lines;
            try
            {
                string known = RenownWords.KnownTo(Renown.People().SelectMany(Renown.KnownTo));
                if (known != null) lines.Add(P("CC_Renown_Card_KnownTo", known.Named("NOTABLES")));
                if (r.score > 0f && r.band >= RenownBand.Town)
                    lines.Add(P("CC_Renown_Colony_Does", ((int)Math.Round(RenownMath.RegardBonus(r.score))).Named("REGARD")));
                else if (r.band == RenownBand.Infamous)
                    lines.Add(P("CC_Renown_Colony_DoesBad", ((int)Math.Round(-RenownMath.RegardBonus(r.score))).Named("REGARD")));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("renown: the colony's lines", e);
            }
            return lines;
        }
    }
}
