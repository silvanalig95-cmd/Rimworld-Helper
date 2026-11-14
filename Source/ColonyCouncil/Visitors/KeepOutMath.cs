namespace ColonyCouncil
{
    /// <summary>What kind of room visitors keep out of (<see cref="KeepOutMath"/>).</summary>
    public enum KeptKind
    {
        /// <summary>Anyone may go in.</summary>
        None,
        /// <summary>A room with a bed of the colony's own people in it: private, at any hour.</summary>
        Bedroom,
        /// <summary>A room kept cold with food in it or stored in it, whatever its role (a freezer with a butcher's table too).</summary>
        ColdStore
    }

    /// <summary>
    /// The pure rules for the rooms visitors keep out of (KeepOut.cs): the colony's bedrooms and its cold stores. Every
    /// visitor, the game's, Rimfolk's and Hospitality's guests, stays out of a bedroom of the colony's people at any hour
    /// unless it is theirs too (a bed given to them there, a lover's place in their partner's bed) or a doctor's work takes
    /// them in, and never while one of the colony sleeps in it; and nobody goes into a cold store. A cold store is told by
    /// what the room is, not by the role the game gives it: kept below a cold line, with food stored in it or lying in it.
    /// No game types, so Tools/Tests can check it.
    /// </summary>
    public static class KeepOutMath
    {
        /// <summary>A room colder than this (in degrees) is kept cold.</summary>
        public const float ColdBelow = 5f;

        /// <summary>
        /// Once counted cold, a room stays so until it is this warm: a freezer's cooler cycling, or the door opened a while,
        /// doesn't make it a store one moment and not the next.
        /// </summary>
        public const float ColdLeaves = 8f;

        /// <summary>This many stacks of food lying in a room make it one that holds food (a meal on a cold table does not).</summary>
        public const int FoodStacks = 3;

        /// <summary>Kept cold now: below the line, or, counted cold before, not yet warm enough to leave it.</summary>
        public static bool Cold(float temperature, bool wasCold) => temperature < (wasCold ? ColdLeaves : ColdBelow);

        /// <summary>Food is kept in the room: a stockpile or shelf in it takes food, or enough of it lies there.</summary>
        public static bool HoldsFood(int foodStacks, bool storesFood) => storesFood || foodStacks >= FoodStacks;

        /// <summary>
        /// What a room is to visitors. Only a proper room counts (closed in, not out of doors). A bed of the colony's people
        /// makes it a bedroom (guest beds alone don't: that is the guests' own room); else kept cold with food in it, a
        /// cold store, whatever the game calls it.
        /// </summary>
        public static KeptKind Kind(bool properRoom, bool colonyBed, bool cold, bool holdsFood)
        {
            if (!properRoom) return KeptKind.None;
            if (colonyBed) return KeptKind.Bedroom;
            return cold && holdsFood ? KeptKind.ColdStore : KeptKind.None;
        }

        /// <summary>
        /// Whether a visitor may not go into the room through its door. <paramref name="inside"/>: they are in it already (or
        /// in its doorway), so the way out stays open. <paramref name="owner"/>: a bed in it is theirs (a guest bed given to
        /// them, a lover's place in their partner's bed). <paramref name="care"/>: a doctor's work for someone in it.
        /// <paramref name="asleep"/>: one of the colony sleeps in it, so only those whose bed it is go in.
        /// </summary>
        public static bool Barred(KeptKind kind, bool asleep, bool inside, bool owner, bool care)
        {
            if (inside) return false;
            switch (kind)
            {
                case KeptKind.ColdStore:
                    return true;
                case KeptKind.Bedroom:
                    if (owner) return false;
                    if (asleep) return true;
                    return !care;
                default:
                    return false;
            }
        }

        /// <summary>A visitor standing in the room who should walk out of it: one who couldn't have come in now.</summary>
        public static bool WalksOut(KeptKind kind, bool asleep, bool owner, bool care) => Barred(kind, asleep, false, owner, care);
    }
}
