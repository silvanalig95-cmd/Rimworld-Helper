using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules behind the ailments of the land, the work and the age (6 November 2026): what the hourly look has
    /// to find for each new cause, the storyteller's lean on the new serious illnesses, and an ailment's name by the
    /// colony's age. No game types, so the tests can hold each cause to what its description and the Almanac say
    /// (AilmentTests). The game side reads the map and passes the facts in (AilmentChecks.cs, AilmentTracker.cs).
    /// </summary>
    public static class AilmentCauseMath
    {
        /// <summary>The game's tech levels as numbers (RimWorld's TechLevel: Medieval 3, Industrial 4, Spacer 5).</summary>
        public const int MedievalTech = 3, IndustrialTech = 4, SpacerTech = 5;

        // ---------------- Swimmer's itch ----------------

        /// <summary>
        /// A swim in still water: a lake, a pond or a marsh, where the snails that carry the itch live. Never a river's
        /// running water or the sea.
        /// </summary>
        public static bool StillWater(bool water, bool river, bool ocean, bool marsh) => (water || marsh) && !river && !ocean;

        // ---------------- Hunter's fever, black bane ----------------

        /// <summary>The largest body counted as small game (a hare, a squirrel, a rat, a raccoon), as the game sizes bodies.</summary>
        public const float SmallGameSize = 0.5f;

        /// <summary>Butchering a small wild animal: hares, squirrels, rats and the like, never a tame or a farm animal.</summary>
        public static bool SmallGame(bool animal, bool wild, float bodySize) => animal && wild && bodySize > 0f && bodySize <= SmallGameSize;

        /// <summary>Butchering an animal that had begun to rot, or that was sick when it died (black bane).</summary>
        public static bool BadCarcass(bool animal, bool rotting, bool diedSick) => animal && (rotting || diedSick);

        // ---------------- Founder's ague ----------------

        /// <summary>The largest closed room where a forge's or a smelter's fumes hang (cells), as for a smoky room.</summary>
        public const int FumeRoomCells = 60;

        /// <summary>Smithing or smelting in a closed room of 60 cells or fewer; a big or open smithy lets the fumes go.</summary>
        public static bool FumeRoom(bool properRoom, bool outdoors, int cells) => properRoom && !outdoors && cells > 0 && cells <= FumeRoomCells;

        // ---------------- Marsh foot ----------------

        /// <summary>Wet feet turn to marsh foot below this, for an ordinary body (°C).</summary>
        public const float ColdWetBelow = 10f;

        /// <summary>
        /// Outdoors with the feet in water, marsh or mud below 10 °C, the line moved by their own cold shift (Health/BodyMath.cs:
        /// a fur coat of their own takes it down, an immunity out of reach).
        /// </summary>
        public static bool ColdWet(bool outdoors, bool wetGround, float temp, float coldShift) =>
            outdoors && wetGround && coldShift > -BodyMath.Never && coldShift < BodyMath.Never && temp < ColdWetBelow + coldShift;

        // ---------------- Sandfly fever, valley fever ----------------

        /// <summary>Sandflies bite from dusk to before dawn in the warm: 18:00 to 5:00, at 20 °C or more.</summary>
        public const int DuskFrom = 18, DawnAt = 5;

        public const float SandflyWarm = 20f;

        public static bool DuskOrNight(int hour) => hour >= DuskFrom || (hour >= 0 && hour < DawnAt);

        /// <summary>Out of doors in an arid land between dusk and dawn, while it's warm.</summary>
        public static bool Sandflies(bool arid, bool outdoors, float temp, int hour) => arid && outdoors && temp >= SandflyWarm && DuskOrNight(hour);

        /// <summary>Digging the dry ground of an arid land: building, mining in the open, sowing or clearing, with no rain falling.</summary>
        public static bool DesertDust(bool arid, bool outdoors, bool digging, bool raining) => arid && outdoors && digging && !raining;

        // ---------------- Stump sores, mechanite fever ----------------

        /// <summary>
        /// A limb of the old kinds: a peg leg, a wooden foot or hand, a simple prosthetic. Weaker than the limb it
        /// replaces and made before the spacer age; a bionic or archotech limb never chafes.
        /// </summary>
        public static bool LowTechLimb(float efficiency, bool betterThanNatural, int techLevel) =>
            !betterThanNatural && efficiency < 1f && techLevel < SpacerTech;

        /// <summary>A part or implant that carries mechanites: made in the spacer age or later, or better than the body's own.</summary>
        public static bool MechanitePart(int techLevel, bool betterThanNatural) => techLevel >= SpacerTech || betterThanNatural;

        /// <summary>Mechanite fever comes within three days of the mechanites going in.</summary>
        public const float MechaniteDays = 3f;

        /// <summary>
        /// Mechanites put in within the last three days, while they were already one of the colony (a raider recruited
        /// with an old bionic arm has nothing new in them).
        /// </summary>
        public static bool FreshMechanites(int ageTicks, int colonistTicks, int dayTicks) =>
            ageTicks >= 0 && ageTicks <= MechaniteDays * dayTicks && colonistTicks > ageTicks;

        // ---------------- Jungle rot ----------------

        /// <summary>Jungle rot takes a wound left open this many hours in the wet heat.</summary>
        public const int RotWoundHours = 6;

        /// <summary>The wet heat it needs: a swamp, jungle or rainforest, 25 °C or more outside.</summary>
        public const float RotHeat = 25f;

        public static bool RotLand(bool wetBiome, float outdoorTemp) => wetBiome && outdoorTemp >= RotHeat;

        /// <summary>
        /// The one hour in a wound's life when it is looked at: untended, six hours old. Each colonist is looked at once an
        /// hour, so each wound has its one roll and nothing needs remembering.
        /// </summary>
        public static bool RotWindow(int ageTicks, bool tended, int hourTicks) =>
            !tended && ageTicks >= RotWoundHours * hourTicks && ageTicks < (RotWoundHours + 1) * hourTicks;

        // ---------------- The storyteller ----------------

        /// <summary>
        /// The new serious illnesses lean with the storyteller's disease setting as the catching clock does: twice as
        /// often at half the interval, half as often at twice it, never more than double or less than a quarter, and
        /// none at all with the game's diseases off (the interval at its cutoff).
        /// </summary>
        public static float DiseaseLean(float intervalFactor, float cutoff)
        {
            if (intervalFactor >= cutoff) return 0f;
            if (intervalFactor <= 0f) return 1f;
            float f = 1f / intervalFactor;
            return f < 0.25f ? 0.25f : f > 2f ? 2f : f;
        }

        // ---------------- The name by the age ----------------

        /// <summary>
        /// An ailment's name in the colony's age: the first of <paramref name="names"/> whose tag block names the age
        /// ("[tribal|medieval] pond itch" for a tribe), else the first untagged one, else null (the def's own label).
        /// </summary>
        public static string NameFor(IList<string> names, string ageTag)
        {
            if (names == null || names.Count == 0) return null;
            string plain = null;
            foreach (var raw in names)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                string line = raw.Trim();
                if (!line.StartsWith("[", StringComparison.Ordinal))
                {
                    plain ??= line;
                    continue;
                }
                int close = line.IndexOf(']');
                if (close <= 1) continue;
                string text = line.Substring(close + 1).Trim();
                if (text.Length == 0) continue;
                foreach (var tag in line.Substring(1, close - 1).Split('|', ','))
                    if (string.Equals(tag.Trim(), ageTag, StringComparison.OrdinalIgnoreCase))
                        return text;
            }
            return plain;
        }
    }
}
