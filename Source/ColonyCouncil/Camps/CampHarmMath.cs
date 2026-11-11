namespace ColonyCouncil
{
    /// <summary>Who or what hurt one of a camp's people, or is about on the land (CampHarmMath.By).</summary>
    public enum CampHarmBy
    {
        /// <summary>Nothing did it: a fall, a fire, the weather.</summary>
        Accident,
        /// <summary>The colony, meaning it: a colonist, one of its animals, a turret or a trap.</summary>
        Colony,
        /// <summary>The colony by mischance: a shot meant for an enemy of both (the game's own crossfire rule), or someone out of their mind.</summary>
        Stray,
        /// <summary>Raiders: a people at war with the colony.</summary>
        Raiders,
        /// <summary>A people at war with the camp's own.</summary>
        Foes,
        /// <summary>A beast: wild and hunting, manhunting, taking revenge for a hunt, or any beast out of its mind.</summary>
        Beast,
        /// <summary>An insect, wild or of a hive.</summary>
        Insect,
        /// <summary>Someone of no people (a wild man), or of a people at peace with both (a quarrel, a fit).</summary>
        Other
    }

    /// <summary>What the rule knows of whoever hurt one of the camp's people, or of a danger about on the land (no victim then).</summary>
    public struct CampHarmFacts
    {
        /// <summary>Something did it (a fall, a fire or the weather has nobody behind it).</summary>
        public bool instigator;
        /// <summary>It is the colony's: a colonist, its animal, a turret or a trap.</summary>
        public bool colony;
        /// <summary>It was aimed at someone hostile to both the colony and the camp's people (the game's own crossfire rule).</summary>
        public bool crossfire;
        /// <summary>The one who did it, or the one hurt, was out of their mind (berserk, manhunting).</summary>
        public bool mad;
        /// <summary>A beast (not a person, not a machine, not a thing of the void).</summary>
        public bool animal;
        /// <summary>An insect.</summary>
        public bool insect;
        /// <summary>Of no people at all.</summary>
        public bool noFaction;
        /// <summary>Of the insects' own hives.</summary>
        public bool hive;
        /// <summary>Of a people at war with the camp's own.</summary>
        public bool hostileToCamp;
        /// <summary>Of a people at war with the colony.</summary>
        public bool hostileToColony;
    }

    /// <summary>
    /// Whether harm done to a camp, or a danger about on the land, is a real attack on the camp (the player's report of 10
    /// November 2026: "camps packed up after an animal taking revenge after being attacked, animal and other more
    /// arbitrary attacks should not get the camp to leave"). Real: raiders, a people at war with the camp's own, and the
    /// colony meaning it (its people, animals, turrets and traps). Arbitrary: a beast (wild and hunting, manhunting, or
    /// taking revenge for a hunt), an insect, a wild man, a fit of madness, a shot meant for an enemy of both, an accident.
    /// For a real attack the camp does as it always did (the colony's harm scatters it at once; raiders and foes are read
    /// hourly: they stand or go); an arbitrary one is fought off by those who can fight, by their fire, while the rest keep
    /// clear, and the camp stays. Pure (no game types), so Tools/Tests can check it; the camp's group asks
    /// <see cref="Scatters"/> on every harm (Trigger_CampHarmed), the camp asks <see cref="RealAttack(CampHarmFacts)"/> of
    /// every danger on the land (CampHarm.RealThreatOn).
    /// </summary>
    public static class CampHarmMath
    {
        /// <summary>A beast or an insect this near their fire (or at one of them) is fought off by those who can fight.</summary>
        public const float FendRadius = 22f;

        /// <summary>Those who can't fight keep this far from it.</summary>
        public const float KeepClear = 9f;

        /// <summary>And run this far when it comes nearer.</summary>
        public const int RunFrom = 14;

        public static CampHarmBy By(CampHarmFacts f)
        {
            if (!f.instigator) return CampHarmBy.Accident;
            if (f.insect && (f.noFaction || f.hive)) return CampHarmBy.Insect;
            if (f.animal && (f.noFaction || f.hive || f.mad)) return CampHarmBy.Beast;
            if (f.colony) return f.crossfire || f.mad ? CampHarmBy.Stray : CampHarmBy.Colony;
            if (f.noFaction) return CampHarmBy.Other;
            if (f.hostileToCamp) return CampHarmBy.Foes;
            if (f.hostileToColony) return CampHarmBy.Raiders;
            return CampHarmBy.Other;
        }

        /// <summary>A real attack on the camp: the colony meaning it, raiders, or a people at war with the camp's own.</summary>
        public static bool RealAttack(CampHarmBy by) => by == CampHarmBy.Colony || by == CampHarmBy.Raiders || by == CampHarmBy.Foes;

        public static bool RealAttack(CampHarmFacts f) => RealAttack(By(f));

        /// <summary>
        /// The camp's group scatters at once (the lord's harm trigger): only the colony meaning it. Raiders and foes are read
        /// by the camp each hour (CampLife.Ends: they defend their fire or go), as before.
        /// </summary>
        public static bool Scatters(CampHarmFacts f) => By(f) == CampHarmBy.Colony;

        /// <summary>Something they fight off or keep clear of, and stay: a beast, an insect, a wild man or a fit of madness.</summary>
        public static bool FendOff(CampHarmBy by) => !RealAttack(by) && by != CampHarmBy.Accident && by != CampHarmBy.Stray;

        /// <summary>One of them stands by the fire against it: grown, able to fight, and armed.</summary>
        public static bool Fights(bool grown, bool canFight, bool armed) => grown && canFight && armed;

        /// <summary>One who doesn't fight runs from it when it comes this near.</summary>
        public static bool Runs(bool fights, float distance) => !fights && distance <= KeepClear;
    }
}
