using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A colonist's one "known as": the colony's name for them (its short form), the others they hold, and what the peoples say.</summary>
    public sealed class KnownName
    {
        /// <summary>The name the colony knows them by (EpithetTracker.Chief), or null.</summary>
        public EpithetDef name;
        /// <summary>The colony's other names for them: details under the one.</summary>
        public List<EpithetDef> also = new List<EpithetDef>();
        /// <summary>Their own name among the peoples (Renown.Own), while anyone outside speaks of them; else null.</summary>
        public OwnName abroad;

        public string Short => name?.label;
    }

    /// <summary>
    /// A colonist's one "known as" (phase 4 of the review, part 1, one name: the player's decision 6 of 6 November 2026),
    /// put together here and read everywhere it shows. Its short form is the colony's name for them
    /// (<see cref="EpithetTracker.Chief"/>: of the names they hold, a proud one before a dubious one, then the one held
    /// longest, and each name slow to move, <see cref="NameMath"/>); with it, what the peoples say of them (their renown's
    /// own name, <see cref="Renown.Own"/>, read from the history's deeds and the peoples' memory), said in one line. The card,
    /// the Story tab, the tables and the death letter show the short form; the card and People → Renown the line; talk and
    /// letters to outsiders say it by {KNOWNAS}, where "the one the colony calls ..." is the same short form; the chronicle
    /// writes a name's line only when it is the one they are known by. Reputation's six sides stay as the details under it,
    /// with the colony's other names for them.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class KnownAs
    {
        static KnownAs()
        {
            CardSections.RegisterLines(CardKind.Colonist, CardSlot.Who, 30f, CardLines, null, "knownas");
        }

        private static bool NamesOn => ColonyCouncilMod.Settings?.epithets ?? true;

        /// <summary>What a colonist is known by, or null for no one of the colony's.</summary>
        public static KnownName Of(Pawn p)
        {
            var comp = CouncilGameComponent.Instance;
            if (p == null || comp?.epithets == null) return null;
            var k = new KnownName();
            try
            {
                if (NamesOn)
                {
                    k.name = comp.epithets.Chief(p);
                    if (k.name != null) k.also = comp.epithets.AlsoOf(p);
                }
                var o = Renown.On ? Renown.Own(p) : null;
                if (o?.part != null) k.abroad = o;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("known as: a colonist's", e);
            }
            return k;
        }

        /// <summary>The short form: the name the colony knows them by ("the Healer"), or null.</summary>
        public static string Short(Pawn p) => NamesOn ? CouncilGameComponent.Instance?.epithets?.Chief(p)?.label : null;

        /// <summary>
        /// Their "known as" in one line: "Known as the Healer, the one who won the wrestling at the feast with the Lenlia,
        /// talked about in the next town."; "Known in the colony as the Healer." while nobody outside speaks of them; renown's
        /// own line without a name at home. Null with neither.
        /// </summary>
        public static string Line(Pawn p) => Line(Of(p));

        public static string Line(KnownName k)
        {
            if (k == null) return null;
            try
            {
                bool infamous = k.abroad != null && k.abroad.reading.band == RenownBand.Infamous;
                string key = NameMath.LineKey(k.name != null, k.abroad?.part, infamous);
                if (key == null) return null;
                if (k.name == null) return RenownWords.OwnLine(k.abroad, Ages.Ours);
                string renown = k.abroad != null ? RenownWords.Band(k.abroad.reading.band, Ages.Ours) : "";
                string knownAs = k.abroad != null ? RenownWords.KnownAs(k.abroad) ?? "" : "";
                return key.Translate(k.Short.Named("NAME"), knownAs.Named("KNOWNAS"), renown.Named("RENOWN")).Resolve().CapitalizeFirst();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("known as: the line", e);
                return null;
            }
        }

        /// <summary>The details under it: the colony's other names for them, and the sides of their name that show (reputation).</summary>
        public static List<string> Details(Pawn p, KnownName k = null)
        {
            var lines = new List<string>();
            k ??= Of(p);
            if (k == null) return lines;
            if (k.also.Count > 0)
                lines.Add("CC_Name_Own_Also".Translate(TextUtil.JoinAnd(k.also.Select(d => d.label).ToList()).Named("NAMES")).Resolve());
            string sides = Reputations.Public(p) ? Reputations.SidesLine(p) : null;
            if (sides != null) lines.Add(sides);
            return lines;
        }

        /// <summary>On a colonist's card: the one line, then its details.</summary>
        private static List<string> CardLines(ThingRef t)
        {
            var lines = new List<string>();
            var p = ThingRefs.Pawn(t);
            if (p == null) return lines;
            var k = Of(p);
            string line = Line(k);
            if (line != null) lines.Add(line);
            lines.AddRange(Details(p, k));
            return lines;
        }
    }
}
