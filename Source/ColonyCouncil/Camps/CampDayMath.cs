using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>The part of a camper's day an hour belongs to.</summary>
    public enum CampHour
    {
        /// <summary>In their own bed.</summary>
        Night,
        /// <summary>Up, a bite at the table or the fire before work.</summary>
        Morning,
        /// <summary>The day's work, by the person and the kind's calling.</summary>
        Work,
        /// <summary>A meal at midday.</summary>
        Noon,
        /// <summary>Supper cooked at the fire and eaten at the table.</summary>
        Supper,
        /// <summary>By the fire: talk among themselves and with colonists who come by.</summary>
        Evening
    }

    /// <summary>One kind of day work a camper can do (CampKindDef.works, CampWork.cs).</summary>
    public enum CampLabour
    {
        Chop,
        Hunt,
        Fish,
        Mine,
        Gather,
        Craft,
        Haul,
        Mend
    }

    /// <summary>
    /// A camper's day (Camps/CampWork.cs): the hours of sleep, meals, work and the evening by the fire, which work a camper
    /// takes up by what they're good at and what their kind comes to do, and how much of each the camp does in a day, so
    /// the work looks busy and the land isn't stripped in a stay. Pure (no game types), so Tools/Tests can check it.
    /// </summary>
    public static class CampDayMath
    {
        /// <summary>The part of the day an hour is: night from ten to six, a morning bite, work from seven, noon, work again, supper from five, the evening from seven.</summary>
        public static CampHour HourOf(int hour)
        {
            hour = ((hour % 24) + 24) % 24;
            if (hour >= 22 || hour < 6) return CampHour.Night;
            if (hour < 7) return CampHour.Morning;
            if (hour < 12) return CampHour.Work;
            if (hour < 13) return CampHour.Noon;
            if (hour < 17) return CampHour.Work;
            if (hour < 19) return CampHour.Supper;
            return CampHour.Evening;
        }

        /// <summary>
        /// Hungry enough to sit down to a meal now: below this at a mealtime (a meal of theirs fills about half the need, so
        /// none is wasted), below <see cref="Starving"/> at any hour.
        /// </summary>
        public static float MealBelow(CampHour h) => h == CampHour.Morning || h == CampHour.Noon || h == CampHour.Supper ? 0.55f : Starving;

        public const float Starving = 0.3f;

        /// <summary>
        /// How many of a work the whole camp does in a day: the kind's own rate where it has one (a fraction is a chance,
        /// rolled in <see cref="Roll"/>), else what its calling brings, by how many grown hands it has; nothing it doesn't do.
        /// </summary>
        public static float PerDay(CampLabour w, bool calling, float kindRate, int hands)
        {
            if (hands <= 0) return 0f;
            if (kindRate > 0f) return kindRate * (calling ? 1.5f : 1f);
            switch (w)
            {
                // Wood for the fire and the building, a tree or two a day; a woodcutters' camp its own rate.
                case CampLabour.Chop: return calling ? 1f + hands * 0.4f : 0.6f + hands * 0.1f;
                case CampLabour.Hunt: return calling ? 0.8f : 0f;
                // A catch or two where there's water with fish in it.
                case CampLabour.Fish: return calling ? hands * 2f : 0.5f + hands * 0.15f;
                // Plain rock: a stonecutters' or prospectors' camp works a face, anyone else digs a little for a hearth stone.
                case CampLabour.Mine: return calling ? 2f + hands : 0f;
                // Berries, herbs, wild fruit: a few plants a day.
                case CampLabour.Gather: return calling ? 2f + hands : 0.5f + hands * 0.25f;
                // Something simple at their spot: a piece a day, more for a crafting kind.
                case CampLabour.Craft: return calling ? Math.Min(3f, 0.5f + hands * 0.5f) : 0.5f;
                default: return 99f;
            }
        }

        /// <summary>A day's count from a rate: the whole part, and the fraction as a chance (<paramref name="roll"/> 0 to 1).</summary>
        public static int Roll(float perDay, double roll)
        {
            if (perDay <= 0f) return 0;
            int n = (int)Math.Floor(perDay);
            return n + (roll < perDay - n ? 1 : 0);
        }

        /// <summary>
        /// How much of the land a stay may take at most, whatever the rates: a tree for every two cells of its reach's
        /// grown trees in ten, plain rock to a dozen cells a day, game to a third of the wild beasts that are fair game.
        /// </summary>
        public static int StayCap(CampLabour w, int available, float days)
        {
            if (available <= 0) return 0;
            switch (w)
            {
                case CampLabour.Chop: return Math.Max(1, available / 5);
                case CampLabour.Hunt: return Math.Max(1, available / 3);
                case CampLabour.Mine: return Math.Max(1, (int)(12 * Math.Max(1f, days)));
                case CampLabour.Gather: return Math.Max(1, available / 2);
                default: return available;
            }
        }

        /// <summary>
        /// The order a grown camper tries the day's work in: their kind's callings first (in the order the kind gives them),
        /// then what they're best at, then the camp's common work (wood, gathering, a catch), then hauling and mending.
        /// <paramref name="skills"/>: their skill for each work (0 to 20, -1 when they can't do it at all).
        /// </summary>
        public static List<CampLabour> Order(IList<CampLabour> callings, Func<CampLabour, int> skills)
        {
            var list = new List<CampLabour>();
            void Add(CampLabour w)
            {
                if (!list.Contains(w) && skills(w) >= 0) list.Add(w);
            }
            if (callings != null)
                foreach (var w in callings) Add(w);
            var rest = new List<CampLabour> { CampLabour.Chop, CampLabour.Gather, CampLabour.Fish, CampLabour.Craft };
            rest.Sort((a, b) => skills(b).CompareTo(skills(a)));
            foreach (var w in rest) Add(w);
            Add(CampLabour.Haul);
            Add(CampLabour.Mend);
            return list;
        }

        /// <summary>A building of theirs is mended when it is below this share of its hit points.</summary>
        public const float MendBelow = 0.8f;

        /// <summary>
        /// The evening's talk: each hour by the fire, the chance one of them says something to another sitting there (one
        /// line, through the speech system), more with more of them sitting together.
        /// </summary>
        public static float TalkChance(int sitting) => sitting < 2 ? 0f : Math.Min(0.6f, 0.2f + 0.1f * sitting);
    }
}
