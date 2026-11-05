using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The words of the colony's renown (Languages/English/Keyed/Rimfolk_Renown.xml, Rimfolk_Renown_Colony.xml): what the
    /// colony is known for by its own age ("strong spears", "good swords", "good shots", "steady soldiers"), how far its name
    /// carries by the ways of whoever speaks of it (told at the fires, sung in the halls, in the papers, carried on every
    /// channel; the Empire's people by the court's ways), the deeds its name came from, the news when it moves, and each
    /// colonist's own name ("the one who won the wrestling at the feast with the Lenlia"). Lines told often have numbered
    /// fellows (CC_X_2...), one picked at random.
    /// </summary>
    public static class RenownWords
    {
        public static string T(string key, params NamedArgument[] args) => DipWords.T(key, args);

        public static string P(string key, params NamedArgument[] args) => DipWords.P(key, args);

        /// <summary>The colony's name as its people's news says it.</summary>
        public static string Colony() => Faction.OfPlayerSilentFail?.Name ?? P("CC_Renown_TheColony");

        /// <summary>What it is known for, by an age ("good swords"); null for nothing yet.</summary>
        public static string For(KnownFor k, WorldAge age) => k == KnownFor.None ? null : P(RenownMath.ForKey(k, age));

        /// <summary>
        /// How far a name carries, by the ways of an age ("sung in the halls of the region"), or of the Empire's court
        /// (<paramref name="empire"/>: "spoken of with honor in the region's noble houses").
        /// </summary>
        public static string Band(RenownBand b, WorldAge ways, bool empire = false) =>
            b == RenownBand.Unknown ? P("CC_Renown_Band_Unknown") : P(RenownMath.BandKey(b, ways, empire));

        /// <summary>The colony's renown in one line by the ways of an age: "Sung in the halls of the region, for good swords."</summary>
        public static string Standing(RenownMath.Reading r, WorldAge ways)
        {
            if (r == null) return null;
            string band = Band(r.band, ways);
            string known = r.band >= RenownBand.Town ? For(r.known, Ages.Ours) : null;
            return (known == null ? P("CC_Renown_Line", band.Named("RENOWN")) : P("CC_Renown_LineFor", band.Named("RENOWN"), known.Named("KNOWNFOR"))).CapitalizeFirst();
        }

        /// <summary>The news of the colony's band moved, for the history and the message.</summary>
        public static string Moved(RenownMath.Reading r, bool up)
        {
            var colony = Colony().Named("COLONY");
            var renown = Band(r.band, Ages.Ours).Named("RENOWN");
            string known = For(r.known, Ages.Ours);
            string line;
            if (r.band == RenownBand.Infamous) line = T("CC_Renown_Colony_Infamous", colony);
            else if (!up && r.band == RenownBand.Unknown) line = T("CC_Renown_Colony_Faded", colony);
            else if (up && r.band == RenownBand.Unknown) line = T("CC_Renown_Colony_Mended", colony);
            else if (!up) line = T("CC_Renown_Colony_Down", colony, renown);
            else if (known != null) line = T("CC_Renown_Colony_UpFor", colony, renown, known.Named("KNOWNFOR"));
            else line = T("CC_Renown_Colony_Up", colony, renown);
            return line.CapitalizeFirst();
        }

        /// <summary>A kind of renown in a few words ("contests won at feasts"), for the deeds the colony's name came from.</summary>
        public static string Part(string key) => P("CC_Renown_Part_" + key);

        /// <summary>The deeds a name came from, largest first (at most <paramref name="max"/>), the good and the bad apart.</summary>
        public static string Parts(RenownMath.Reading r, bool good, int max = 4)
        {
            if (r == null) return null;
            var list = r.parts.Where(p => good ? p.Value > 0f : p.Value < 0f).Take(max).Select(p => Part(p.Key)).ToList();
            return list.Count == 0 ? null : list.ToCommaList(true);
        }

        /// <summary>"the head of Tallbrook, the envoy of the Lenlia": the notables who think well of the colony's people.</summary>
        public static string KnownTo(IEnumerable<Notable> notables, int max = 3)
        {
            var list = notables.Where(n => n != null).Distinct().Take(max).Select(n => NotableBook.Called(n)).ToList();
            return list.Count == 0 ? null : list.ToCommaList(true);
        }

        // ================= A colonist's own name =================

        /// <summary>
        /// A colonist's own name, in the words of the part they are known by, with the people it was done with or for and
        /// what it was: "the one who won the wrestling at the feast with the Lenlia", "the one the colony calls the Healer".
        /// Null while nobody outside speaks of them.
        /// </summary>
        public static string KnownAs(OwnName o)
        {
            if (o?.part == null) return null;
            var f = o.people.IsNone ? null : Refs.Faction(o.people);
            bool withPeople = f != null;
            string key = RenownMath.KnownAsWords(o.part, withPeople);
            var args = new List<NamedArgument> { (f != null ? DipWords.People(f) : "").Named("PEOPLE") };
            switch (o.part)
            {
                case "contest":
                    string contest = System.Enum.TryParse(o.detail ?? "", out ContestKind k) ? FeastWords.ContestThe(k) : null;
                    args.Add((contest.NullOrEmpty() ? P("CC_Renown_KnownAs_AContest") : contest.UncapitalizeFirst()).Named("CONTEST"));
                    break;
                case "epithet":
                    args.Add((o.detail ?? "?").Named("NAME"));
                    break;
                case "office":
                    key = "CC_Renown_KnownAs_office_" + (o.detail ?? "");
                    if (!key.CanTranslate()) key = "CC_Renown_KnownAs_office";
                    break;
                case "title":
                    args.Add((o.detail ?? "?").Named("TITLE"));
                    break;
                case "regard":
                    args.Add((o.detail ?? "?").Named("NOTABLE"));
                    break;
            }
            return key.CanTranslate() ? key.Translate(args.ToArray()).Resolve() : null;
        }

        /// <summary>A colonist's own name and how far it carries, by the ways of an age: "Known as the one who broke the raiders' camp that troubled the Lenlia, talked about in the next town."</summary>
        public static string OwnLine(OwnName o, WorldAge ways)
        {
            string name = KnownAs(o);
            if (name == null) return null;
            return P(o.reading.band == RenownBand.Infamous ? "CC_Renown_Own_LineBad" : "CC_Renown_Own_Line", name.Named("KNOWNAS"), Band(o.reading.band, ways).Named("RENOWN")).CapitalizeFirst();
        }
    }
}
