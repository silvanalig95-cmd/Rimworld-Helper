using System;

namespace ColonyCouncil
{
    /// <summary>
    /// Each god's own way of taking what is left for it (docs/gods-next-proposals.md, section D, 5; GodWorshipDef.taking).
    /// <see cref="Vanish"/> is the shared way every god had before (gone by morning, nobody saw it go), kept for any god
    /// whose worship names no way of its own.
    /// </summary>
    public enum OfferingWay
    {
        /// <summary>The shared way: gone in the small hours, nobody saw it go.</summary>
        Vanish,
        /// <summary>Hollin: it sinks into the soil overnight and a young tree stands there by morning.</summary>
        Sapling,
        /// <summary>Smoulder: it goes down a crack in the rock, and the floor is warm after.</summary>
        Crack,
        /// <summary>Bellwether: grazed by morning, a great hoofprint beside where it lay.</summary>
        Grazed,
        /// <summary>Morel: it rots overnight into rich black soil with a mushroom on it.</summary>
        Rot,
        /// <summary>Ennu: eaten or taken by a guest in the night: footprints in to it, none away.</summary>
        Guest,
        /// <summary>Pip: the part is gone, and a broken machine nearby works again (else a light winks three times).</summary>
        Mended,
        /// <summary>Cirrus: taken up in the night in a streak of light, from under the open sky.</summary>
        TakenUp,
        /// <summary>Hush: frosted over in the night, gone at dawn.</summary>
        Frost,
        /// <summary>Undertow: gone, and something else of the colony's with it.</summary>
        Hunger,
        /// <summary>Helios: never taken; the noon light lies on it, and at dusk it is still there.</summary>
        Never,
        /// <summary>Selene: gone only on a night her face shows, in a silver light; on a night with no moon it waits.</summary>
        Moon,
        /// <summary>Murmur: taken down into the well or the wet ground, with a croak from below.</summary>
        Well,
        /// <summary>Ducat: never left lying: given to the next visitor or trader, to carry on.</summary>
        Given
    }

    /// <summary>
    /// The pure rules of each god's way of taking an offering (Gods/Worship/OfferingWays.cs): when it goes (the shared hour
    /// of the night, dawn for Hush's frost, a moonlit night for Selene, a visitor for Ducat, never for Helios), when it is
    /// let go back to the colony instead, whether someone notices it gone by day (round 1's sign), when Hush's frost and
    /// Helios's noon light show on it, how much the Hunger takes besides, and the tag of each way's own words. Free of game
    /// types.
    /// </summary>
    public static class OfferingWayMath
    {
        public static readonly OfferingWay[] All = (OfferingWay[])Enum.GetValues(typeof(OfferingWay));

        /// <summary>The way's own words in the pools are tagged with this (way:sapling and the rest).</summary>
        public static string Tag(OfferingWay w) => "way:" + w.ToString().ToLowerInvariant();

        /// <summary>Night, as the ways count it: from nine in the evening to four in the morning.</summary>
        public static bool Night(int hourOfDay) => hourOfDay >= 21 || hourOfDay <= 4;

        /// <summary>
        /// The god takes it now: before everyone's eyes an hour after it was left when it shows itself plainly (and is the
        /// kind to take at all), else in its own hour: the shared small hours for most; dawn after a night's frost for Hush;
        /// a night whose moon shows any face for Selene; by day with a visitor or a trader on the land to give it to for
        /// Ducat; never for Helios.
        /// </summary>
        public static bool TakesNow(OfferingWay w, bool plain, int hourOfDay, float hoursWaited, float moonLight, bool visitorHere)
        {
            switch (w)
            {
                case OfferingWay.Never:
                    return false;
                case OfferingWay.Given:
                    return visitorHere && hoursWaited >= 1f && hourOfDay >= 7 && hourOfDay <= 20;
                case OfferingWay.Moon:
                    if (moonLight <= 0f) return false;
                    return plain ? hoursWaited >= 1f && Night(hourOfDay) : hoursWaited >= 3f && Night(hourOfDay);
                case OfferingWay.Frost:
                    if (plain) return hoursWaited >= 1f;
                    return hoursWaited >= 6f && hourOfDay >= 5 && hourOfDay <= 7;
                case OfferingWay.TakenUp:
                    if (plain) return hoursWaited >= 1f && Night(hourOfDay);
                    return hoursWaited >= 3f && Night(hourOfDay);
                default:
                    return WorshipMath.GodTakesNow(plain, hourOfDay, hoursWaited);
            }
        }

        /// <summary>Ducat's gift with no visitor to give it to is let go back to the stores after this many hours (three days).</summary>
        public const float GivenWaitHours = 72f;

        /// <summary>Selene's silver waits this many hours for a night with a moon before it is let go.</summary>
        public const float MoonWaitHours = 6f * 24f;

        /// <summary>
        /// It goes back to the colony instead (unforbidden where it lies): Helios's at dusk on the day it lay in the sun
        /// (still there, as the colony sees for itself), Ducat's after three days with nobody to give it to, Selene's after
        /// a dark week.
        /// </summary>
        public static bool LetGo(OfferingWay w, int hourOfDay, float hoursWaited)
        {
            switch (w)
            {
                case OfferingWay.Never: return hoursWaited >= 4f && hourOfDay >= 18 && hourOfDay <= 21;
                case OfferingWay.Given: return hoursWaited >= GivenWaitHours;
                case OfferingWay.Moon: return hoursWaited >= MoonWaitHours;
                default: return false;
            }
        }

        /// <summary>
        /// A gift gone in the night is noticed by day (round 1, Offerings.Notice): every way that takes it unseen in the
        /// night; not Helios's (never taken), not Ducat's (given before everyone's eyes).
        /// </summary>
        public static bool NoticedByDay(OfferingWay w) => w != OfferingWay.Never && w != OfferingWay.Given;

        /// <summary>Hush's frost forms on what is left in the night (once, after two hours there), before it is gone at dawn.</summary>
        public static bool FrostsNow(OfferingWay w, int hourOfDay, float hoursWaited) => w == OfferingWay.Frost && Night(hourOfDay) && hoursWaited >= 2f;

        /// <summary>Helios's noon light lies on what is left (once, about noon).</summary>
        public static bool NoonLightNow(OfferingWay w, int hourOfDay) => w == OfferingWay.Never && hourOfDay >= 11 && hourOfDay <= 13;

        /// <summary>
        /// Undertow takes something else besides: from a stack of the colony's near the place, one, two or three of it (a
        /// larger stack, more), never all of it; none from a single thing.
        /// </summary>
        public static int HungerTakes(int stack) => stack <= 1 ? 0 : Math.Min(stack - 1, Math.Min(3, 1 + stack / 25));

        /// <summary>How much of an offering's favour the god's way counts when it goes back to the colony (Ducat's: none, it was meant to go on).</summary>
        public static float ReturnedShare(OfferingWay w) => w == OfferingWay.Given || w == OfferingWay.Never ? 0f : 0.25f;

        /// <summary>The way's key ending in the words: CC_Wsh_Taken_Way_ and the way.</summary>
        public static string Key(OfferingWay w) => "CC_Wsh_Taken_Way_" + w;
    }
}
