using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // Each colony its own character (docs/design/stage3-many-colonies.md, 3d; the player's decision 13A): the five ways and
    // what a colony's work made it (green-fingered, builders, walled...) are one colony's, kept per home in a book of its own
    // by the council's rules (ColonyBook: a home's entry made when first needed, the seat's for a map that isn't a home, an
    // old save's single character placed at the seat, a lost home's kept thirty days). With one colony the book holds one
    // character, saved as it always was.
    public partial class CharacterTracker : IColonyEntry
    {
        /// <summary>The colony's key (Homes.KeyOf), or none while it has no home (an old save's single character not yet placed).</summary>
        public int home = HomesMath.None;
        /// <summary>When its home stopped being ours; -1 while it is.</summary>
        public int lostAt = ColonyStateMath.NotLost;

        public int Home
        {
            get => home;
            set => home = value;
        }

        public int LostAt
        {
            get => lostAt;
            set => lostAt = value;
        }

        /// <summary>The seat's character is saved as the game's one character always was, without its home (CouncilGameComponent.ExposeCharacters).</summary>
        [Unsaved] internal static bool savingSeat;

        private void ExposeHome()
        {
            if (Scribe.mode == LoadSaveMode.Saving && savingSeat) return;
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref lostAt, "lostAt", ColonyStateMath.NotLost);
        }

        /// <summary>The colony's home while it's ours; with one colony, or none placed yet, the seat.</summary>
        public Map Map => home == HomesMath.None ? Homes.Seat : Homes.MapOf(home);

        /// <summary>This is the seat's character (the colony as a whole: its word among the peoples, renown's part of its name).</summary>
        public bool IsSeat
        {
            get
            {
                var seat = Homes.Seat;
                return seat == null || home == HomesMath.None || home == Homes.KeyOf(seat);
            }
        }

        // ---------------- Which colony ----------------

        /// <summary>A home's character (the seat's for a map that isn't a home). Null without a game.</summary>
        public static CharacterTracker For(Map map) => CouncilGameComponent.Instance?.CharacterOf(map);

        /// <summary>The character of the colony someone belongs to (Homes.Of).</summary>
        public static CharacterTracker Of(Pawn p)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return null;
            var homes = Homes.All;
            if (homes.Count <= 1 || p == null) return comp.character;
            return comp.CharacterOf(Homes.Of(p));
        }

        /// <summary>The character of the colony in view (the map on screen when it's a home, else the seat).</summary>
        public static CharacterTracker InView => For(Find.CurrentMap);

        /// <summary>Every colony's character, the seat's first: each home's in the order founded (one with no home: the one its people carry).</summary>
        public static List<CharacterTracker> Ticking()
        {
            var comp = CouncilGameComponent.Instance;
            var list = new List<CharacterTracker>();
            if (comp == null) return list;
            var homes = Homes.All;
            if (homes.Count == 0)
            {
                list.Add(comp.CharacterOf(null));
                return list;
            }
            foreach (var m in homes) list.Add(comp.CharacterOf(m));
            return list;
        }

        /// <summary>
        /// Starts a colony founded from another with half its mother's (decision 13A): half of each way, and half of what its
        /// work made it (the rates; a name kept only where half still holds it, and "walled" never on land with no walls).
        /// </summary>
        internal void StartFrom(CharacterTracker mother, bool walls)
        {
            if (mother == null) return;
            for (int i = 0; i < values.Count && i < mother.values.Count; i++) values[i] = ManyColoniesMath.FoundedValue(mother.values[i]);
            for (int i = 0; i < tiers.Count; i++) tiers[i] = TierFor(values[i], 0);
            traits = ColonyTraits.HalfOf(mother.traits, walls);
            recent = new List<CharacterEvent>();
            nameKey = null;
            version++;
        }
    }

    /// <summary>The council component's side of each colony's character: the book, found, saved, and looked after.</summary>
    public partial class CouncilGameComponent
    {
        /// <summary>One character per colony (CharacterTracker); an old save's single one is the seat's.</summary>
        private List<CharacterTracker> characters = new List<CharacterTracker> { new CharacterTracker() };

        /// <summary>The seat's character: what the colony as a whole is known as. Per colony: CharacterTracker.For or Of.</summary>
        public CharacterTracker character => CharacterOf(Homes.Seat);

        /// <summary>A home's character, made when it first needs one (the seat takes an old save's); for a map that isn't a home, the seat's.</summary>
        public CharacterTracker CharacterOf(Map map)
        {
            characters ??= new List<CharacterTracker>();
            return ColonyBook.Entry(characters, map, () => new CharacterTracker());
        }

        /// <summary>Every character in the book (the dev window's and the save model's count).</summary>
        internal int CharacterCount => characters?.Count ?? 0;

        /// <summary>Homes lost (Homes.Changed): a lost home's character is kept a while, or carried when no home is left.</summary>
        internal void CharactersLost(IReadOnlyList<int> lost) => ColonyBook.Lost(characters, lost, null);

        /// <summary>Once a day: a home that came back is ours again, and characters of homes lost long enough ago are let go.</summary>
        internal void CharactersDaily() => ColonyBook.Daily(characters, null);

        /// <summary>
        /// A colony founded from another (Colonies/Foundings.cs): with people of the mother among its founders, it starts with
        /// half the mother's character; newcomers alone start fresh. Once, as the colony stands.
        /// </summary>
        internal void CharacterFounded(Map map, int mother, int fromMother)
        {
            if (map == null) return;
            var motherChar = characters?.FirstOrDefault(c => c != null && c.home == mother);
            if (motherChar == null && Homes.MapOf(mother) != null) motherChar = CharacterOf(Homes.MapOf(mother));
            if (!ManyColoniesMath.StartsFromMother(fromMother, motherChar != null)) return;
            var mine = CharacterOf(map);
            if (mine == null || mine == motherChar) return;
            mine.StartFrom(motherChar, ColonyTraits.HasWalls(map));
        }

        /// <summary>The old colony abandoned once the new stands (a fresh start that moves the colony): its whole character goes with its council.</summary>
        internal void CharacterCarried(int from, int to) => FoundingMath.CarryCouncil(characters, from, to);

        /// <summary>The day's look at each colony's character: the seat's carries word among the peoples and renown's part of the name.</summary>
        private void CharactersDay()
        {
            foreach (var c in CharacterTracker.Ticking())
            {
                var ch = c;
                Safe("character", () => ch.Daily(this));
            }
            Safe("characters' book", CharactersDaily);
        }

        /// <summary>
        /// Saves the book: the seat's character as "character", as one colony always saved it (its home not written), and the
        /// others' (each with its home) as "characters", only when there are any. An old save's one character is the seat's.
        /// </summary>
        private void ExposeCharacters()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                characters ??= new List<CharacterTracker>();
                characters.RemoveAll(c => c == null);
                var seatKey = Homes.KeyOf(Homes.Seat);
                var seat = characters.FirstOrDefault(c => c.home == seatKey && c.lostAt == ColonyStateMath.NotLost)
                           ?? characters.FirstOrDefault(c => c.home == HomesMath.None) ?? characters.FirstOrDefault();
                if (seat == null)
                {
                    seat = new CharacterTracker();
                    characters.Add(seat);
                }
                var others = characters.Where(c => c != seat).ToList();
                CharacterTracker.savingSeat = true;
                try
                {
                    Scribe_Deep.Look(ref seat, "character");
                }
                finally
                {
                    CharacterTracker.savingSeat = false;
                }
                if (others.Count > 0) Scribe_Collections.Look(ref others, "characters", LookMode.Deep);
                return;
            }
            CharacterTracker one = null;
            List<CharacterTracker> rest = null;
            Scribe_Deep.Look(ref one, "character");
            Scribe_Collections.Look(ref rest, "characters", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                characters = new List<CharacterTracker>();
                if (one != null)
                {
                    // The seat's, saved without its home: placed at the seat on first use (ColonyBook).
                    one.home = HomesMath.None;
                    one.lostAt = ColonyStateMath.NotLost;
                    characters.Add(one);
                }
                if (rest != null) characters.AddRange(rest.Where(c => c != null));
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                characters ??= new List<CharacterTracker>();
                characters.RemoveAll(c => c == null);
                if (characters.Count == 0) characters.Add(new CharacterTracker());
            }
        }
    }
}
