using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's renown in the windows (no acts, it is earned; nothing in This week): its renown on a home's card and on
    /// People → Renown (UI/RenownPage.cs: how far its name carries and for what, the deeds it came from and what is held
    /// against it, who knows it, what it does, and its people known by name), and each colonist's own name on their card.
    /// The houses of release 2d.1 had cards and a page of their own; both went with them (5 November 2026).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RenownPanels
    {
        static RenownPanels()
        {
            CardSections.HomeLines(CardSlot.Now, 40f, h => ColonyLines(), "CC_Renown_Card_Head", "renown");
            CardSections.RegisterLines(CardKind.Colonist, CardSlot.Who, 30f, ColonistLines, null, "renown-colonist");
        }

        private static string P(string key, params NamedArgument[] args) => RenownWords.P(key, args);

        /// <summary>
        /// The colony's renown in lines: how far and for what, the deeds it came from and those held against it, the notables
        /// who know its people, what it does. Its name is the colony's, whichever home's card shows it.
        /// </summary>
        public static List<string> ColonyLines()
        {
            var lines = new List<string>();
            if (!Renown.On)
            {
                lines.Add(P("CC_Renown_Card_Off"));
                return lines;
            }
            try
            {
                var r = Renown.Read();
                if (r == null) return lines;
                lines.Add(RenownWords.Standing(r, Ages.Ours));
                string good = RenownWords.Parts(r, true), bad = RenownWords.Parts(r, false, 3);
                if (good != null) lines.Add(P("CC_Renown_Card_From", good.Named("PARTS")));
                if (bad != null) lines.Add(P("CC_Renown_Card_Against", bad.Named("PARTS")));
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

        /// <summary>A colonist's own name and how far it carries, when anyone outside speaks of them.</summary>
        private static List<string> ColonistLines(ThingRef t)
        {
            var lines = new List<string>();
            var p = ThingRefs.Pawn(t);
            if (p == null || !Renown.On) return lines;
            string line = OwnLine(p);
            if (line != null) lines.Add(line);
            return lines;
        }

        /// <summary>A colonist's own name in a line ("Known as the one who ..., talked about in the next town."), or null.</summary>
        public static string OwnLine(Pawn p)
        {
            try
            {
                return RenownWords.OwnLine(Renown.Own(p), Ages.Ours);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("renown: a colonist's line", e);
                return null;
            }
        }
    }
}
