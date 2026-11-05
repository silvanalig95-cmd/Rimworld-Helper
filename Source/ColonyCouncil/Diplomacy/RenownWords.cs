using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The words of house renown (Languages/English/Keyed/Rimfolk_Renown.xml): what a house is known for by its own age
    /// (the colony's for its houses, their people's for theirs: "strong spears", "good swords", "good shots", "steady
    /// soldiers"), how far its name carries by the ways of whoever speaks of it (told at the fires, sung in the halls, in
    /// the papers, a name that carries), the house's word by the colony's age (Ages.Form, the feature Renown: a clan's name
    /// and totem, a house and its arms, a family name and a family firm, a lineage), the deeds its name came from, and the
    /// news when its name moves. Lines told often have numbered fellows (CC_X_2...), one picked at random.
    /// </summary>
    public static class RenownWords
    {
        public static string T(string key, params NamedArgument[] args) => DipWords.T(key, args);

        public static string P(string key, params NamedArgument[] args) => DipWords.P(key, args);

        /// <summary>"the Coor family", "Yerrik's kin".</summary>
        public static string Name(LedgerHouse h) => RegionWords.HouseName(h);

        /// <summary>The house's own age: the colony's for its houses, their people's for theirs.</summary>
        public static WorldAge AgeOf(LedgerHouse h) => h == null || h.ours ? Ages.Ours : Ages.Of(h.people);

        /// <summary>What it is known for, by an age ("good swords"); null for nothing yet.</summary>
        public static string For(KnownFor k, WorldAge age) => k == KnownFor.None ? null : P(RenownMath.ForKey(k, age));

        /// <summary>How far its name carries, by the ways of an age ("sung in the halls of the region").</summary>
        public static string Band(RenownBand b, WorldAge ways) => b == RenownBand.Unknown ? P("CC_Renown_Band_Unknown") : P(RenownMath.BandKey(b, ways));

        /// <summary>The colony's word for a house by its age (a house and its arms) and their way of telling of one (deeds sung in the hall).</summary>
        public static AgeForm Form(Map home, Faction people) => Ages.Form(AgeFeature.Renown, home ?? Homes.Seat, people);

        /// <summary>A house's renown in one line by the ways of an age: "Sung in the halls of the region, for good swords."</summary>
        public static string Standing(LedgerHouse h, RenownMath.Reading r, WorldAge ways)
        {
            if (h == null || r == null) return null;
            string band = Band(r.band, ways);
            string known = r.band >= RenownBand.Town ? For(r.known, AgeOf(h)) : null;
            return (known == null ? P("CC_Renown_Line", band.Named("RENOWN")) : P("CC_Renown_LineFor", band.Named("RENOWN"), known.Named("KNOWNFOR"))).CapitalizeFirst();
        }

        /// <summary>The news of a band moved, for the history, the message and the house's story.</summary>
        public static string Moved(LedgerHouse h, RenownMath.Reading r, bool up, Map home)
        {
            var house = Name(h).Named("HOUSE");
            var renown = Band(r.band, Ages.Ours).Named("RENOWN");
            string known = For(r.known, AgeOf(h));
            string line;
            if (r.band == RenownBand.Infamous) line = T("CC_Renown_Infamous", house);
            else if (!up && r.band == RenownBand.Unknown) line = T("CC_Renown_Faded", house);
            else if (up && r.band == RenownBand.Unknown) line = T("CC_Renown_Mended", house);
            else if (!up) line = T("CC_Renown_Down", house, renown);
            else if (known != null) line = T("CC_Renown_UpFor", house, renown, known.Named("KNOWNFOR"));
            else line = T("CC_Renown_Up", house, renown);
            return line.CapitalizeFirst();
        }

        /// <summary>A kind of renown in a few words ("a champion at a feast"), for the deeds a house's name came from.</summary>
        public static string Part(string key) => P("CC_Renown_Part_" + key);

        /// <summary>The deeds a house's name came from, largest first (at most <paramref name="max"/>), the good and the bad apart.</summary>
        public static string Parts(RenownMath.Reading r, bool good, int max = 4)
        {
            if (r == null) return null;
            var list = r.parts.Where(p => good ? p.Value > 0f : p.Value < 0f).Take(max).Select(p => Part(p.Key)).ToList();
            return list.Count == 0 ? null : list.ToCommaList(true);
        }

        /// <summary>"the head of Tallbrook, the envoy of the Lenlia": the notables who think well of a house's people.</summary>
        public static string KnownTo(IEnumerable<Notable> notables, int max = 3)
        {
            var list = notables.Where(n => n != null).Distinct().Take(max).Select(n => NotableBook.Called(n)).ToList();
            return list.Count == 0 ? null : list.ToCommaList(true);
        }
    }
}
