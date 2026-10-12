using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// How dangerous a wild animal is, read from the creature itself so every animal mod counts the same way (Alpha
    /// Animals, Megafauna, dinosaurs): how strong it is in a fight (its kind's combat power), how big it is, and how
    /// readily it turns on whoever hurts it. Animal mods add plenty of creatures that look like game and aren't: a
    /// boulder-sized crab nearly immune to arrows, a fire-breathing herd beast, a small thing with a deadly sting.
    /// </summary>
    public static class Beasts
    {
        /// <summary>A muffalo, an elk or a boar fights at about this; more than this isn't ordinary game.</summary>
        public const float GameCombatPower = 150f;

        /// <summary>More than this (a grizzly), and nobody goes after it without the council's say-so.</summary>
        public const float SelfHelpCombatPower = 200f;

        public static float CombatPower(Pawn a) => a?.kindDef?.combatPower ?? 0f;

        /// <summary>Ordinary game for someone who wants to hunt: no predator, nothing that fights back hard.</summary>
        public static bool FitForHunting(Pawn a) =>
            a?.RaceProps != null && a.RaceProps.Animal && !a.RaceProps.predator && CombatPower(a) <= GameCombatPower
            && PawnUtility.GetManhunterOnDamageChance(a.def) <= 0.05f;

        /// <summary>
        /// Too much for a colonist to take on by themselves: a self-help hunt marks only what a small party could
        /// bring down. The council can still order it.
        /// </summary>
        public static bool TooDangerousAlone(Pawn a) =>
            a?.RaceProps != null && (CombatPower(a) > SelfHelpCombatPower || a.RaceProps.baseBodySize > 3f);
    }
}
