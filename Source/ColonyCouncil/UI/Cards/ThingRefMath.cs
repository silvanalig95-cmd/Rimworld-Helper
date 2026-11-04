using System;
using System.Collections.Generic;
using System.Globalization;

namespace ColonyCouncil
{
    /// <summary>What a <see cref="ThingRef"/> names: the world ledger's kinds, and the things only Rimfolk's windows name.</summary>
    public enum ThingKind : byte
    {
        None,
        /// <summary>Anyone, of the colony or out in the world: the ledger's person (a pawn's thingIDNumber, or a ledger number below 0).</summary>
        Person,
        /// <summary>A settlement of another people (Settlement.ID).</summary>
        Town,
        /// <summary>A people (a faction's loadID).</summary>
        People,
        /// <summary>One of the player's homes (its settlement's id).</summary>
        Colony,
        /// <summary>A family line (a ledger number).</summary>
        House,
        /// <summary>A tile of the planet with nothing on it (the ledger's tile ref).</summary>
        Tile,
        /// <summary>Any other place on the world (a ruin, a raiders' camp, a site, a caravan): its world object's ID.</summary>
        Site,
        /// <summary>A tie of the ledger (Tie.id).</summary>
        Tie,
        /// <summary>An entry of the one history (HistoryEntry.id).</summary>
        Event,
        /// <summary>A letter or call of the colony's to a neighbour (NeighbourLetter.id).</summary>
        Letter
    }

    /// <summary>The eight cards of the interface (docs/design/interface.md, "Things and their cards").</summary>
    public enum CardKind : byte
    {
        None,
        /// <summary>One of the colony's own people: the Roster's page of them.</summary>
        Colonist,
        /// <summary>Anyone outside: a notable, a known face, a founder, someone the history names.</summary>
        Person,
        /// <summary>A town, a post, a hamlet, a ruin, a raiders' camp, a town being built, a bare tile, a colony.</summary>
        Place,
        People,
        House,
        Tie,
        /// <summary>A world event, a history entry, a deed.</summary>
        Event,
        Letter
    }

    /// <summary>The slots every card has, in the order they are drawn (the same on every card, so one layout is learned once).</summary>
    public enum CardSlot : byte
    {
        /// <summary>The picture, the name, what it is in one line, its chips.</summary>
        Head,
        /// <summary>What is happening to it now, and any ask about it.</summary>
        Now,
        /// <summary>Its people: a town's notables, a people's head and towns, a house's members.</summary>
        Who,
        /// <summary>Its ties, with since when and on what terms (stage 2).</summary>
        Ties,
        /// <summary>What they remember of the colony, what was heard from them, letters to and from.</summary>
        Words,
        /// <summary>How it began and the newest lines of its history, the whole history one click on.</summary>
        Story,
        /// <summary>What the colony can do: the bar at the card's foot, each greyed with why not.</summary>
        Acts
    }

    /// <summary>
    /// One way for Rimfolk's windows to name any thing a card can show: the world ledger's <see cref="Ref"/> (a person, a
    /// town, a people, a colony, a house, a tile) and the things only the interface names (any other place on the world
    /// by its world object, a tie, an entry of the history, a letter). A plain value, equal when kind and number are, with a
    /// short text form (the ledger's "p123", "t55", "f3", "c12", "h2", "w801", and "s40" for a site, "y7" a tie, "e12" an
    /// event, "l3" a letter) that the links in text carry. Pure: Tools/Tests checks it. Whether the thing is still there,
    /// its name and its card are the game's side (UI/Cards/ThingRefs.cs), and safe when it is gone.
    /// </summary>
    public readonly struct ThingRef : IEquatable<ThingRef>, IComparable<ThingRef>
    {
        public readonly ThingKind kind;
        public readonly int id;

        public ThingRef(ThingKind kind, int id)
        {
            this.kind = kind;
            this.id = kind == ThingKind.None ? 0 : id;
        }

        public static readonly ThingRef None = default;

        public bool IsNone => kind == ThingKind.None;

        /// <summary>The ledger's ref as a thing (none for none).</summary>
        public static ThingRef From(Ref r)
        {
            switch (r.kind)
            {
                case RefKind.Person: return new ThingRef(ThingKind.Person, r.id);
                case RefKind.Town: return new ThingRef(ThingKind.Town, r.id);
                case RefKind.People: return new ThingRef(ThingKind.People, r.id);
                case RefKind.Colony: return new ThingRef(ThingKind.Colony, r.id);
                case RefKind.House: return new ThingRef(ThingKind.House, r.id);
                case RefKind.Tile: return new ThingRef(ThingKind.Tile, r.id);
                default: return None;
            }
        }

        /// <summary>The ledger's ref it is, for the six kinds the ledger knows; none for a site, a tie, an event or a letter.</summary>
        public Ref ToRef()
        {
            switch (kind)
            {
                case ThingKind.Person: return new Ref(RefKind.Person, id);
                case ThingKind.Town: return new Ref(RefKind.Town, id);
                case ThingKind.People: return new Ref(RefKind.People, id);
                case ThingKind.Colony: return new Ref(RefKind.Colony, id);
                case ThingKind.House: return new Ref(RefKind.House, id);
                case ThingKind.Tile: return new Ref(RefKind.Tile, id);
                default: return Ref.None;
            }
        }

        public static ThingRef Site(int worldObjectId) => worldObjectId < 0 ? None : new ThingRef(ThingKind.Site, worldObjectId);

        public static ThingRef Tie(int tieId) => tieId <= 0 ? None : new ThingRef(ThingKind.Tie, tieId);

        public static ThingRef Event(int entryId) => entryId <= 0 ? None : new ThingRef(ThingKind.Event, entryId);

        public static ThingRef Letter(int letterId) => letterId < 0 ? None : new ThingRef(ThingKind.Letter, letterId);

        /// <summary>A place on the world: a town, a colony, a bare tile or another world object.</summary>
        public bool IsPlace => kind == ThingKind.Town || kind == ThingKind.Colony || kind == ThingKind.Tile || kind == ThingKind.Site;

        /// <summary>
        /// The card it opens, for every kind but a person: a person's card depends on whether they are one of the colony's
        /// own (a colonist's card) or not, which only the game knows (<see cref="CardOf(bool)"/>).
        /// </summary>
        public CardKind Card => CardOf(false);

        /// <summary>The card it opens; for a person, <paramref name="colonist"/> says whether they are one of the colony's own.</summary>
        public CardKind CardOf(bool colonist)
        {
            switch (kind)
            {
                case ThingKind.Person: return colonist ? CardKind.Colonist : CardKind.Person;
                case ThingKind.Town:
                case ThingKind.Colony:
                case ThingKind.Tile:
                case ThingKind.Site: return CardKind.Place;
                case ThingKind.People: return CardKind.People;
                case ThingKind.House: return CardKind.House;
                case ThingKind.Tie: return CardKind.Tie;
                case ThingKind.Event: return CardKind.Event;
                case ThingKind.Letter: return CardKind.Letter;
                default: return CardKind.None;
            }
        }

        private static char Letter(ThingKind k)
        {
            switch (k)
            {
                case ThingKind.Person: return 'p';
                case ThingKind.Town: return 't';
                case ThingKind.People: return 'f';
                case ThingKind.Colony: return 'c';
                case ThingKind.House: return 'h';
                case ThingKind.Tile: return 'w';
                case ThingKind.Site: return 's';
                case ThingKind.Tie: return 'y';
                case ThingKind.Event: return 'e';
                case ThingKind.Letter: return 'l';
                default: return '-';
            }
        }

        private static ThingKind KindOf(char c)
        {
            switch (c)
            {
                case 'p': return ThingKind.Person;
                case 't': return ThingKind.Town;
                case 'f': return ThingKind.People;
                case 'c': return ThingKind.Colony;
                case 'h': return ThingKind.House;
                case 'w': return ThingKind.Tile;
                case 's': return ThingKind.Site;
                case 'y': return ThingKind.Tie;
                case 'e': return ThingKind.Event;
                case 'l': return ThingKind.Letter;
                default: return ThingKind.None;
            }
        }

        /// <summary>"p123", "t55", "s40", "y7", "e12", "l3"; "-" for none. The ledger's six kinds read as the ledger writes them.</summary>
        public override string ToString() => IsNone ? "-" : Letter(kind) + id.ToString(CultureInfo.InvariantCulture);

        /// <summary>Reads the text form back; anything else (empty, an unknown letter, a number that kind can't have) is false and none.</summary>
        public static bool TryParse(string s, out ThingRef r)
        {
            r = None;
            if (string.IsNullOrEmpty(s) || s.Length < 2) return false;
            var k = KindOf(s[0]);
            if (k == ThingKind.None) return false;
            switch (k)
            {
                case ThingKind.Person:
                case ThingKind.Town:
                case ThingKind.People:
                case ThingKind.Colony:
                case ThingKind.House:
                case ThingKind.Tile:
                    // The ledger's own rules for its kinds.
                    if (!Ref.TryParse(s, out var lr)) return false;
                    r = From(lr);
                    return !r.IsNone;
            }
            for (int i = 1; i < s.Length; i++)
                if (s[i] < '0' || s[i] > '9')
                    return false;
            if (!int.TryParse(s.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out int n)) return false;
            switch (k)
            {
                case ThingKind.Site: r = Site(n); break;
                case ThingKind.Tie: r = Tie(n); break;
                case ThingKind.Event: r = Event(n); break;
                case ThingKind.Letter: r = Letter(n); break;
            }
            return !r.IsNone;
        }

        public static ThingRef Parse(string s) => TryParse(s, out var r) ? r : None;

        public bool Equals(ThingRef other) => kind == other.kind && id == other.id;

        public override bool Equals(object obj) => obj is ThingRef r && Equals(r);

        public override int GetHashCode() => unchecked((int)kind * 397 ^ id);

        public static bool operator ==(ThingRef a, ThingRef b) => a.Equals(b);

        public static bool operator !=(ThingRef a, ThingRef b) => !a.Equals(b);

        /// <summary>By kind, then by number: a stable order for lists and tests.</summary>
        public int CompareTo(ThingRef other) => kind != other.kind ? kind.CompareTo(other.kind) : id.CompareTo(other.id);
    }

    /// <summary>
    /// What features add to a card (a section in a slot, an act at its foot), kept in the order the card draws them:
    /// by slot, then by the order given, then in the order they were added. Pure, so Tools/Tests can check the order of
    /// the place card's sections; the game's side holds the drawers (UI/Cards/CardSections.cs, CardActs.cs).
    /// </summary>
    public sealed class CardRegistry<T>
    {
        public sealed class Entry
        {
            public CardKind kind;
            public CardSlot slot;
            public float order;
            /// <summary>Its name: the fold key of its section, and what replaces an entry added again.</summary>
            public string id;
            public int seq;
            public T value;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly Dictionary<long, List<Entry>> sorted = new Dictionary<long, List<Entry>>();
        private int next;

        public int Count => entries.Count;

        public IReadOnlyList<Entry> All => entries;

        /// <summary>Adds an entry; one with the same kind and id replaces the earlier one (keeping its place among equals).</summary>
        public Entry Add(CardKind kind, CardSlot slot, float order, string id, T value)
        {
            if (kind == CardKind.None) return null;
            var e = new Entry { kind = kind, slot = slot, order = order, id = id, value = value, seq = next++ };
            if (!string.IsNullOrEmpty(id))
            {
                int i = entries.FindIndex(x => x.kind == kind && x.id == id);
                if (i >= 0)
                {
                    e.seq = entries[i].seq;
                    entries[i] = e;
                    sorted.Clear();
                    return e;
                }
            }
            entries.Add(e);
            sorted.Clear();
            return e;
        }

        /// <summary>One kind's entries in one slot, in the order they are drawn (made once, kept until something is added).</summary>
        public List<Entry> In(CardKind kind, CardSlot slot)
        {
            long key = (long)kind << 8 | (byte)slot;
            if (sorted.TryGetValue(key, out var list)) return list;
            list = new List<Entry>();
            foreach (var e in entries)
                if (e.kind == kind && e.slot == slot)
                    list.Add(e);
            list.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : a.seq.CompareTo(b.seq));
            sorted[key] = list;
            return list;
        }

        /// <summary>One kind's entries in every slot, slot by slot, as the card draws them.</summary>
        public List<Entry> Of(CardKind kind)
        {
            var list = new List<Entry>();
            foreach (CardSlot s in Enum.GetValues(typeof(CardSlot))) list.AddRange(In(kind, s));
            return list;
        }
    }
}
