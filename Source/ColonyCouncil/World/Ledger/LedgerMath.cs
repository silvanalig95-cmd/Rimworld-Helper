using System;

namespace ColonyCouncil
{
    /// <summary>What a <see cref="Ref"/> names.</summary>
    public enum RefKind : byte
    {
        None,
        /// <summary>Someone out in the world: a pawn's thingIDNumber (0 or more), or a ledger number for someone with no pawn (stored below 0).</summary>
        Person,
        /// <summary>A settlement of another people: its world object id (Settlement.ID).</summary>
        Town,
        /// <summary>A people (a faction): its loadID.</summary>
        People,
        /// <summary>One of the player's homes: its settlement's id (Homes.KeyOf; negative only for a home with no settlement).</summary>
        Colony,
        /// <summary>A family line, ours or theirs: a ledger number (1 or more).</summary>
        House
    }

    /// <summary>
    /// One way to name anything out in the world: a kind and a number. A person with a pawn is their thingIDNumber; a
    /// person with no pawn is a ledger number, stored as its negative (so the two can never meet); a town is its
    /// settlement's id, a people its faction's loadID, a colony its home's settlement id, a house a ledger number. Plain
    /// values, equal when kind and number are, with a short text form ("p123", "p-4", "t55", "f3", "c12", "h2", and "-"
    /// for none) for saving, for history keys and for the tests. Pure (no game types): Tools/Tests checks it.
    /// </summary>
    public readonly struct Ref : IEquatable<Ref>, IComparable<Ref>
    {
        public readonly RefKind kind;
        public readonly int id;

        public Ref(RefKind kind, int id)
        {
            this.kind = kind;
            this.id = kind == RefKind.None ? 0 : id;
        }

        /// <summary>Nothing named.</summary>
        public static readonly Ref None = default;

        public bool IsNone => kind == RefKind.None;

        /// <summary>Someone with a pawn (a thingIDNumber); a negative one (a pawn not yet given an id) names nobody.</summary>
        public static Ref Person(int thingIdNumber) => thingIdNumber < 0 ? None : new Ref(RefKind.Person, thingIdNumber);

        /// <summary>Someone the ledger knows with no pawn, by their ledger number (1 or more).</summary>
        public static Ref LedgerPerson(int number) => number <= 0 ? None : new Ref(RefKind.Person, -number);

        public static Ref Town(int settlementId) => settlementId < 0 ? None : new Ref(RefKind.Town, settlementId);

        public static Ref People(int factionLoadId) => factionLoadId < 0 ? None : new Ref(RefKind.People, factionLoadId);

        /// <summary>A colony by its home's key (HomesMath.None, -1, names nobody).</summary>
        public static Ref Colony(int homeKey) => homeKey == -1 ? None : new Ref(RefKind.Colony, homeKey);

        public static Ref House(int number) => number <= 0 ? None : new Ref(RefKind.House, number);

        /// <summary>A person who has a pawn (the id is its thingIDNumber).</summary>
        public bool HasPawn => kind == RefKind.Person && id >= 0;

        /// <summary>The ledger number behind a person with no pawn or a house; 0 for anything else.</summary>
        public int Number => kind == RefKind.Person && id < 0 ? -id : kind == RefKind.House ? id : 0;

        private static char Letter(RefKind k)
        {
            switch (k)
            {
                case RefKind.Person: return 'p';
                case RefKind.Town: return 't';
                case RefKind.People: return 'f';
                case RefKind.Colony: return 'c';
                case RefKind.House: return 'h';
                default: return '-';
            }
        }

        private static RefKind KindOf(char c)
        {
            switch (c)
            {
                case 'p': return RefKind.Person;
                case 't': return RefKind.Town;
                case 'f': return RefKind.People;
                case 'c': return RefKind.Colony;
                case 'h': return RefKind.House;
                default: return RefKind.None;
            }
        }

        /// <summary>"p123" (a pawn), "p-4" (a person with no pawn), "t55", "f3", "c12", "h2"; "-" for none.</summary>
        public override string ToString() => IsNone ? "-" : Letter(kind) + id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Reads the text form back. Anything else (empty, an unknown letter, a number that kind can't have) is false and none.</summary>
        public static bool TryParse(string s, out Ref r)
        {
            r = None;
            if (string.IsNullOrEmpty(s) || s.Length < 2) return false;
            var k = KindOf(s[0]);
            if (k == RefKind.None) return false;
            // Digits only, with a minus sign at most at the start (no spaces, no plus sign, no decimals).
            for (int i = 1; i < s.Length; i++)
                if (!(s[i] >= '0' && s[i] <= '9' || s[i] == '-' && i == 1 && s.Length > 2))
                    return false;
            if (!int.TryParse(s.Substring(1), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out int n)) return false;
            if (n == 0 && s[1] == '-') return false;
            switch (k)
            {
                case RefKind.Person: r = n >= 0 ? Person(n) : LedgerPerson(-n); break;
                case RefKind.Town: r = Town(n); break;
                case RefKind.People: r = People(n); break;
                case RefKind.Colony: r = Colony(n); break;
                case RefKind.House: r = House(n); break;
            }
            return !r.IsNone;
        }

        /// <summary>The text form read back, or none.</summary>
        public static Ref Parse(string s) => TryParse(s, out var r) ? r : None;

        public bool Equals(Ref other) => kind == other.kind && id == other.id;

        public override bool Equals(object obj) => obj is Ref r && Equals(r);

        public override int GetHashCode() => unchecked((int)kind * 486187739 ^ id);

        public static bool operator ==(Ref a, Ref b) => a.Equals(b);

        public static bool operator !=(Ref a, Ref b) => !a.Equals(b);

        /// <summary>By kind, then by number: a stable order for lists and tests.</summary>
        public int CompareTo(Ref other) => kind != other.kind ? kind.CompareTo(other.kind) : id.CompareTo(other.id);
    }

    /// <summary>A people's age, or a colony's: what its means and its rites look like. The Empire is marked apart where it matters.</summary>
    public enum WorldAge
    {
        Tribal,
        Medieval,
        Industrial,
        Spacer
    }

    /// <summary>The ledger's small rules that aren't about ties (those are <see cref="TieRules"/>). Pure: Tools/Tests checks them.</summary>
    public static class LedgerMath
    {
        /// <summary>
        /// The age of a tech level (the game's TechLevel as a number: 0 undefined, 1 animal, 2 neolithic, 3 medieval, 4
        /// industrial, 5 spacer, 6 ultra, 7 archotech), as Flavor.AddEraTags reads the colony's: neolithic and below
        /// tribal, then medieval, industrial, and spacer for spacer and above.
        /// </summary>
        public static WorldAge AgeOf(int techLevel)
        {
            if (techLevel <= 2) return WorldAge.Tribal;
            if (techLevel == 3) return WorldAge.Medieval;
            if (techLevel == 4) return WorldAge.Industrial;
            return WorldAge.Spacer;
        }

        /// <summary>A town gone from the world is remembered this long (two years) once nobody the colony knows lives there.</summary>
        public const int GoneTownDays = 120;

        /// <summary>
        /// A person stays in the ledger while they are a notable, while a tie of the ledger names them (from, to or about,
        /// live or closed and not yet dropped), or while some reason keeps them in the world.
        /// </summary>
        public static bool KeepPerson(bool notable, bool named, bool kept) => notable || named || kept;

        /// <summary>
        /// A town stays in the ledger while a notable lives there; with nobody there, a gone town is remembered
        /// <see cref="GoneTownDays"/> days from when it was missed, and a standing one isn't kept (the world has it).
        /// </summary>
        public static bool KeepTown(bool notableThere, bool stands, int gone, int now, int dayTicks = TieRules.DayTicks) =>
            notableThere || !stands && gone >= 0 && now - gone < (long)GoneTownDays * dayTicks;
    }
}
