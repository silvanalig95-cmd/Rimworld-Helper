using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The plain rules behind what visitors and camps do with the colony's land and things (VisitorActs.cs, Borne.cs):
    /// which wild beast a hunter may go after, whether a shot would pass near anyone, how many beasts a hunting party
    /// takes, and whether the name of the dead they carry says a man or a woman. No game types, so the tests can check them.
    /// </summary>
    public static class VisitorActsMath
    {
        /// <summary>The biggest beast hunters go after: a deer or an elk, never an elephant, a rhino or a thrumbo.</summary>
        public const float MaxPreySize = 2.5f;

        /// <summary>How near the prey nobody else may stand (a shot that misses lands about it).</summary>
        public const float ClearOfTarget = 12f;

        /// <summary>How near the line of fire nobody else may stand.</summary>
        public const float ClearOfLine = 3f;

        /// <summary>
        /// A beast hunters from outside may take: wild (nobody's, never tame once), not a predator, no bigger than a deer,
        /// of a kind that lives wild on this land (nothing rare passing through), not marked by the colony for its own
        /// hunters or tamers, and one that won't turn on them: it never does, or Rimfolk keeps it from it
        /// (<paramref name="revengeHeld"/>).
        /// </summary>
        public static bool Fits(bool wild, bool predator, float bodySize, bool livesHere, bool marked, bool tameOnce, float revengeChance, bool revengeHeld)
        {
            if (!wild || predator || marked || tameOnce || !livesHere) return false;
            if (bodySize > MaxPreySize) return false;
            return revengeChance <= 0f || revengeHeld;
        }

        /// <summary>
        /// Someone at (x, z) is near the prey at (tx, tz) or near the line from the hunter at (fx, fz) to it: a shot meant
        /// for the beast could hit them.
        /// </summary>
        public static bool InLineOfFire(float fx, float fz, float tx, float tz, float x, float z)
        {
            float px = x - tx, pz = z - tz;
            if (px * px + pz * pz <= ClearOfTarget * ClearOfTarget) return true;
            float dx = tx - fx, dz = tz - fz, len = dx * dx + dz * dz;
            float t = len <= 0f ? 0f : Math.Max(0f, Math.Min(1f, ((x - fx) * dx + (z - fz) * dz) / len));
            float ex = fx + t * dx - x, ez = fz + t * dz - z;
            return ex * ex + ez * ez <= ClearOfLine * ClearOfLine;
        }

        /// <summary>Beasts a hunting party takes on a visit: one, and one more for each night they stay.</summary>
        public static int KillsAllowed(int nights) => 1 + Math.Max(0, nights);

        /// <summary>How long a party with a hunt under way, or a kill not yet dressed, stays past its time: three hours at most.</summary>
        public const int HoldHours = 3;

        /// <summary>
        /// Whether the name of the dead says who they were: 1 a man ("Grandfather Tobin"), 2 a woman ("Mother Aldry"), 0
        /// when it doesn't say.
        /// </summary>
        public static int GenderOfName(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            foreach (var word in name.Split(' ', ',', '-'))
            {
                switch (word.ToLowerInvariant())
                {
                    case "grandfather":
                    case "father":
                    case "brother":
                    case "uncle":
                    case "grandpa":
                    case "sir":
                    case "lord":
                    case "master":
                        return 1;
                    case "grandmother":
                    case "mother":
                    case "sister":
                    case "aunt":
                    case "grandma":
                    case "dame":
                    case "lady":
                    case "mistress":
                    case "widow":
                        return 2;
                }
            }
            return 0;
        }
    }
}
