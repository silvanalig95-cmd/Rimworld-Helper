using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The age of another mod's people Rimfolk knows (the player, 20 November 2026: "foreign peoples change [age] at their
    /// pace"; Peoples/Foreign/ForeignPeoples.cs; DESIGN.md "The comms console by radio, and the foreign peoples' ages"): it
    /// moves by Rimfolk's one rule (PeopleAgesMath.Season) at a pace of its own ways (its profile's agePace, else
    /// <see cref="DefaultPace"/>), up to the highest age its profile allows and no further than its kinds can be armed for:
    /// every role that carries arms needs a set of arms for each age above the one its mod made it for, and that set has to
    /// find a weapon of the age among the loaded mods. Pure, so Tools/Tests (ForeignAgesTests) checks it.
    /// </summary>
    public static class ForeignAgesMath
    {
        /// <summary>Years an age takes at its own pace when its profile names none (the hospitallers' and the Circuit's pace).</summary>
        public const float DefaultPace = 12f;

        /// <summary>
        /// A role is armed for an age: it carries no arms, or it has a set for that age and the set finds what it names (a
        /// weapon of the age within its means, or a piece of its kit).
        /// </summary>
        public static bool RoleArmed(bool carriesArms, bool hasSet, bool setFinds) => !carriesArms || hasSet && setFinds;

        /// <summary>
        /// The highest age the people may rise to: its ways' limit (PeopleAgesMath.Limit, never below the age it began in),
        /// lowered to the last age of an unbroken run from the one after its own in which every role is armed
        /// (<paramref name="armed"/>, indexed by age 0 to 3; an age missing counts as unarmed).
        /// </summary>
        public static int ArmedLimit(int own, int limit, IList<bool> armed)
        {
            own = Clamp(own, 0, 3);
            int top = PeopleAgesMath.Limit(limit, own);
            int reach = own;
            for (int a = own + 1; a <= top; a++)
            {
                if (armed == null || a >= armed.Count || !armed[a]) break;
                reach = a;
            }
            return reach;
        }

        /// <summary>Its pace: its profile's when it names one above nothing, else <see cref="DefaultPace"/>.</summary>
        public static float Pace(float pace) => float.IsNaN(pace) || pace <= 0f ? DefaultPace : pace;

        /// <summary>
        /// How many seasons, on average over its fate, a people takes to rise one age from its head start: its own pace, its
        /// step (PeopleAgesMath.StepFactor) and how much the colony quickens it (<paramref name="boost"/>, 1 for none).
        /// </summary>
        public static float SeasonsToRise(float pace, int age, float fate, float headStart, float boost)
        {
            float perSeason = PeopleAgesMath.OwnPace(Pace(pace), age, fate) * Math.Max(1f, boost);
            if (perSeason <= 0f) return float.PositiveInfinity;
            return (float)Math.Ceiling(Math.Max(0f, 1f - headStart) / perSeason - 1e-4f);
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
