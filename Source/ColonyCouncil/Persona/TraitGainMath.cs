using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>The setting "Traits other mods give": what becomes of a trait another mod gives one of the colony's people during play.</summary>
    public enum OtherTraitGains
    {
        /// <summary>Every one, as the other mod gives it.</summary>
        AsTheyCome,
        /// <summary>Only where it fits their life as Rimfolk reads it (the default).</summary>
        WhereTheyFit,
        /// <summary>None: the other mods' gains during play are all refused.</summary>
        None
    }

    /// <summary>Who gave a trait, read from the call that gave it (Persona/TraitGains.cs).</summary>
    public enum TraitGiver
    {
        /// <summary>The game itself (a growth moment, a scenario, a quest, the dev tools).</summary>
        Game,
        /// <summary>Rimfolk's own changes (Persona/TraitChanges.cs, the gods' and the peoples' gifts).</summary>
        Rimfolk,
        /// <summary>The player, by a click (an editor's window, a letter's choice).</summary>
        Player,
        /// <summary>Another mod, during play (Vanilla Social Interactions Expanded's self-reflection after an illness).</summary>
        OtherMod
    }

    /// <summary>
    /// Traits that fit a life (21 November 2026, the player, of Guido, a shotgunner given brawler by Vanilla Social
    /// Interactions Expanded after an illness: "And now they get just a huge debuff for being a brawler and they can't shoot
    /// anymore. To be honest, I don't like it ... be a bit more delicate, I guess, with giving brawler"): which gains are
    /// looked at, what becomes of them by the setting, and Rimfolk's reading of how someone fights: by the fights of the
    /// last year (kills and the marks of a fight, each day counted once, by the weapon they fought with), never by the
    /// weapon in hand the moment it is read, and never a brawler whose Shooting is near their Melee. Pure (no game types),
    /// so Tools/Tests checks it.
    /// </summary>
    public static class TraitGainMath
    {
        /// <summary>The tallies of how they fought: a day they fought with a melee weapon (or bare hands), and one they fought at range.</summary>
        public const string CloseKey = "Rimfolk_FoughtClose", RangeKey = "Rimfolk_FoughtAtRange";

        /// <summary>How far back their fights are read: a year (sixty days).</summary>
        public const int FoughtWithinDays = 60;

        /// <summary>The fewest days of close fighting in that year for Rimfolk's own brawler.</summary>
        public const int CloseDaysMin = 3;

        /// <summary>Melee must be this far above Shooting: never a brawler whose Shooting is at least their Melee.</summary>
        public const int MeleeLead = 2;

        /// <summary>The marks of a fight (Defs/PersonaDefs/Rimfolk_PersonaMarks_Danger.xml), counted by the weapon carried when they're given.</summary>
        public static readonly string[] FightMarks = { "Rimfolk_Mark_HeldTheLine", "Rimfolk_Mark_DownedHardened", "Rimfolk_Mark_RaidsHardened" };

        /// <summary>
        /// A trait being given is looked at further: the hooks and the setting on, in play, one of the colony's own people, not
        /// someone being made (a newcomer's traits are the game's), not from a gene or a scenario, and not in the middle of a
        /// click of the player's (an editor's window, a letter's choice: the player's own).
        /// </summary>
        public static bool Looks(OtherTraitGains setting, bool hooks, bool playing, bool ours, bool generating, bool gene, bool scenario, bool clicked) =>
            setting != OtherTraitGains.AsTheyCome && hooks && playing && ours && !generating && !gene && !scenario && !clicked;

        /// <summary>
        /// Why a gain is refused, or null to let it be: only another mod's; none of them with the setting at None; with it at
        /// Where they fit, those that don't fit (<paramref name="unfit"/>, from Rimfolk's reading of the trait).
        /// </summary>
        public static string Refused(OtherTraitGains setting, TraitGiver giver, string unfit)
        {
            if (giver != TraitGiver.OtherMod || setting == OtherTraitGains.AsTheyCome) return null;
            if (setting == OtherTraitGains.None) return "none";
            return unfit;
        }

        /// <summary>
        /// Who gave it, from the assemblies of the calls that led to it, nearest first (the game's own, Rimfolk's, and the
        /// runtime's and Harmony's skipped as "pass"): the first that is neither the game's nor the runtime's decides; a
        /// click of the player's is theirs whoever's code ran it.
        /// </summary>
        public static TraitGiver Giver(IList<string> callers, bool clicked)
        {
            if (clicked) return TraitGiver.Player;
            if (callers == null) return TraitGiver.Game;
            foreach (var c in callers)
            {
                if (c == "rimfolk") return TraitGiver.Rimfolk;
                if (c == "mod") return TraitGiver.OtherMod;
            }
            return TraitGiver.Game;
        }

        /// <summary>
        /// How they fight, as Rimfolk reads it, or null when they fight up close: "melee" (they can't), "shoots" (their
        /// Shooting is within <see cref="MeleeLead"/> of their Melee or above it), "range" (a fight at range this year, and more
        /// of them than half their close ones), "few" (fewer than <see cref="CloseDaysMin"/> close fights this year, which
        /// only Rimfolk's own brawler asks).
        /// </summary>
        public static string Fights(int melee, bool meleeDisabled, int shooting, bool shootingDisabled, int closeDays, int rangeDays)
        {
            if (meleeDisabled) return "melee";
            if (!shootingDisabled && melee < shooting + MeleeLead) return "shoots";
            if (rangeDays > 0 && rangeDays * 2 > closeDays) return "range";
            if (closeDays < CloseDaysMin) return "few";
            return null;
        }

        /// <summary>
        /// For a trait another mod gives: one who fights at range doesn't fit a close fighter's trait (can't fight in melee,
        /// Shooting near Melee or above it, or more fights at range than up close this year); a quiet life with no fights
        /// either way is no reason to refuse it.
        /// </summary>
        public static bool FitsCloseFighter(string fights) => fights == null || (fights == "few");

        /// <summary>
        /// Of a stretch of fight days, how many were up close and how many at range: those within the year, each day once
        /// (a tally counts one occasion in half a day).
        /// </summary>
        public static (int close, int range) FightDays(IList<int> closeTicks, IList<int> rangeTicks, int now, int dayTicks = 60000)
        {
            int Within(IList<int> ticks)
            {
                if (ticks == null) return 0;
                int n = 0;
                foreach (int t in ticks)
                    if (t <= now && now - t < FoughtWithinDays * dayTicks) n++;
                return n;
            }
            return (Within(closeTicks), Within(rangeTicks));
        }

        /// <summary>A fight counted by the weapon they fought with: up close for a melee weapon or bare hands, at range for a ranged one, else not at all (null).</summary>
        public static bool? Close(bool weaponRanged, bool weaponMelee, bool bareHands)
        {
            if (weaponRanged) return false;
            if (weaponMelee || bareHands) return true;
            return null;
        }

        /// <summary>The rules of Rimfolk's that read a trait at this degree: its gains at that degree (or any), its steps that way.</summary>
        public static bool RuleReads(TraitChangeKind change, int ruleDegree, bool anyDegree, int degree)
        {
            switch (change)
            {
                case TraitChangeKind.Gain: return anyDegree || ruleDegree == degree;
                case TraitChangeKind.StepUp: return degree > 0;
                case TraitChangeKind.StepDown: return degree < 0;
                default: return false;
            }
        }
    }
}
