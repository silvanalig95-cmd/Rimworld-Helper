using System;

namespace ColonyCouncil
{
    /// <summary>What the health panel knows about one of a colonist's conditions, read from the game (no game types, for the tests).</summary>
    public struct HediffFacts
    {
        /// <summary>A wound (Hediff_Injury).</summary>
        public bool injury;
        /// <summary>A scar or old wound that will never heal.</summary>
        public bool permanent;
        /// <summary>A lost body part (Hediff_MissingPart).</summary>
        public bool missingPart;
        /// <summary>A prosthetic, bionic part or implant (Hediff_AddedPart, or countsAsAddedPartOrImplant).</summary>
        public bool addedPart;
        /// <summary>A lasting condition (the def's chronic): a bad back, cataracts, frailty, dementia.</summary>
        public bool chronic;
        /// <summary>The def's isBad: false for buffs, auras, psylinks, drug highs and the like.</summary>
        public bool bad;
        /// <summary>It can be tended (the def's tendable, or a wound).</summary>
        public bool tendable;
        /// <summary>It has been tended and the tending still holds.</summary>
        public bool tended;
        /// <summary>It wants tending now (the game's TendableNow).</summary>
        public bool needsTend;
        /// <summary>An immunity race (HediffComp_Immunizable).</summary>
        public bool immunizable;
        /// <summary>The game counts it as being ill (the def's makesSickThought: flu, food poisoning, gut worms).</summary>
        public bool sick;
        /// <summary>Pregnancy and labour.</summary>
        public bool pregnancy;
        /// <summary>One of Rimfolk's own ailments (a cold, a stiff neck): they pass.</summary>
        public bool ailment;
        /// <summary>The severity at which it kills (0 when it can't).</summary>
        public float lethalSeverity;
        public float severity;
        /// <summary>How its severity moves a day, from its comps (negative while it heals).</summary>
        public float severityPerDay;
        /// <summary>How fast it bleeds.</summary>
        public float bleedRate;

        public bool Lethal => lethalSeverity > 0f;

        /// <summary>How far towards killing (0..1), or 0 when it can't.</summary>
        public float Progress => lethalSeverity > 0f ? Math.Max(0f, Math.Min(1f, severity / lethalSeverity)) : 0f;
    }

    /// <summary>Where an immunity race stands.</summary>
    public enum HealthRace
    {
        /// <summary>No race, or too little to say.</summary>
        None,
        /// <summary>Immunity will come before it kills, or it's already easing.</summary>
        Recovering,
        /// <summary>At today's pace it kills first.</summary>
        Losing,
        /// <summary>Immune: it's on its way out.</summary>
        Immune
    }

    /// <summary>
    /// The health panel's rules, free of game types so Tools/Tests can check each one: which conditions show (only what
    /// will pass or needs a reaction: wounds, bleeding, untended conditions, illnesses and their immunity race, things
    /// that can kill while they're moving, and Rimfolk's everyday ailments; never lost parts, implants, scars, chronic
    /// conditions, pregnancy, or the buffs, auras and magic other mods add), who is winning an immunity race, and how a
    /// condition ranks (bleeding or soon lethal, then untended, then losing a race, then healing).
    /// </summary>
    public static class HudHealthMath
    {
        /// <summary>A change smaller than this a day counts as none.</summary>
        public const float Still = 0.0001f;

        /// <summary>At or past this share of its lethal severity, a condition that isn't recovering is "soon lethal".</summary>
        public const float SoonLethalAt = 0.75f;

        /// <summary>
        /// Whether a condition belongs on the health panel. What lasts (lost parts, added parts and implants, scars,
        /// chronic conditions) and what isn't an illness (pregnancy; anything the game doesn't call bad, which covers
        /// psylinks, auras, buffs and highs) never shows. Then: bleeding, wounds, anything wanting tending, Rimfolk's
        /// ailments, illnesses (an immunity race, or the game calling it being sick), and anything that can kill. What's
        /// left doesn't show: a bad effect that is none of these (another mod's curse or magic, a hangover) is neither
        /// an injury nor an illness, and one that never changes and needs nothing is how they are now, not something that passes.
        /// </summary>
        public static bool Shows(HediffFacts f)
        {
            if (f.missingPart || f.addedPart || f.permanent || f.chronic || f.pregnancy) return false;
            if (f.ailment) return true;
            if (!f.bad) return false;
            if (f.bleedRate > 0f) return true;
            if (f.injury) return true;
            if (f.needsTend || (f.tendable && !f.tended)) return true;
            if (f.immunizable || f.sick) return true;
            if (f.Lethal) return true;
            // Nothing changes and nothing can be done: a lasting effect, not something that passes.
            if (Math.Abs(f.severityPerDay) < Still && !f.tendable) return false;
            // Tended and still going: it passes with care.
            if (f.tendable) return true;
            // Bad and moving, but neither a wound, an illness, a tending need nor a danger: another mod's effect.
            return false;
        }

        /// <summary>
        /// An immunity race from the game's own numbers: immunity (0..1) and how much it gains a day, the severity and how
        /// much it gains a day (its comps, tending included), and where it kills (0: it can't).
        /// </summary>
        public static HealthRace Race(float immunity, float immunityPerDay, float severity, float severityPerDay, float lethal)
        {
            if (immunity >= 0.999f) return HealthRace.Immune;
            if (severityPerDay <= Still) return HealthRace.Recovering;
            if (lethal <= 0f) return immunityPerDay > Still ? HealthRace.Recovering : HealthRace.None;
            if (immunityPerDay <= Still) return HealthRace.Losing;
            float daysToImmune = (1f - immunity) / immunityPerDay;
            return daysToImmune <= DaysToDeath(severity, severityPerDay, lethal) ? HealthRace.Recovering : HealthRace.Losing;
        }

        /// <summary>Days until it kills at today's pace, or a large number when it isn't heading there.</summary>
        public static float DaysToDeath(float severity, float severityPerDay, float lethal)
        {
            if (lethal <= 0f || severityPerDay <= Still) return 9999f;
            return Math.Max(0f, (lethal - severity) / severityPerDay);
        }

        /// <summary>Close to killing: far along (or at a life-threatening stage) and not on its way out.</summary>
        public static bool SoonLethal(HediffFacts f, bool lifeThreatening, HealthRace race)
        {
            if (!f.Lethal && !lifeThreatening) return false;
            if (race == HealthRace.Recovering || race == HealthRace.Immune) return false;
            if (!f.immunizable && f.severityPerDay < -Still) return false;
            return lifeThreatening || f.Progress >= SoonLethalAt;
        }

        /// <summary>
        /// How much one condition matters (the shared ranking): soon lethal is life and limb; wanting tending, losing a
        /// race, or something deadly getting worse is soon; the rest heals or passes and is worth knowing. 0 when it doesn't show.
        /// </summary>
        public static float Score(HediffFacts f, HealthRace race, bool lifeThreatening)
        {
            if (!Shows(f)) return 0f;
            if (SoonLethal(f, lifeThreatening, race)) return HudRank.Dangerous(f.Progress);
            if (f.needsTend) return HudRank.Untended(1);
            if (race == HealthRace.Losing) return HudRank.LosingRace(DaysToDeath(f.severity, f.severityPerDay, f.lethalSeverity));
            if (f.Lethal && !f.immunizable && f.severityPerDay > Still) return HudRank.Worsening(f.Progress);
            return HudRank.Healing(f.Lethal ? f.Progress : Math.Min(1f, f.severity));
        }

        // ---------------- How far along: healing, recovering, passing ----------------

        /// <summary>The game's healing, a day, for a wound to share with the others (Pawn_HealthTracker: 8, more lying down).</summary>
        public const float BaseHealPerDay = 8f;

        /// <summary>Lying down (in bed or not) heals this much more a day, before the bed's own.</summary>
        public const float LyingHealPerDay = 4f;

        /// <summary>How much of a wound has healed (0..1), from the severity it had at its worst and the severity now.</summary>
        public static float Healed(float start, float now)
        {
            if (start <= 0f) return 0f;
            return Math.Max(0f, Math.Min(1f, 1f - now / start));
        }

        /// <summary>
        /// The natural healing a colonist has a day, shared among their wounds that heal on their own: the game's 8, and 4
        /// more lying down with the bed's own on top, times their conditions' healing factors, their healing speed and
        /// their body size. Nothing while starving.
        /// </summary>
        public static float NaturalHealPerDay(bool lying, float bedHealPerDay, float stageFactor, float healingFactor, float healthScale, bool starving)
        {
            if (starving) return 0f;
            float perDay = BaseHealPerDay + (lying ? LyingHealPerDay + Math.Max(0f, bedHealPerDay) : 0f);
            return Math.Max(0f, perDay * stageFactor * healingFactor * healthScale);
        }

        /// <summary>The healing tended wounds share a day on top: the game's 8, half to one and a half times by the tending's quality.</summary>
        public static float TendedHealPerDay(float tendQuality, float healingFactor, float healthScale, bool starving)
        {
            if (starving) return 0f;
            float q = Math.Max(0f, Math.Min(1f, tendQuality));
            return Math.Max(0f, BaseHealPerDay * (0.5f + q) * healingFactor * healthScale);
        }

        /// <summary>
        /// One wound's share of the healing a day: the game heals one wound at random at a time, so each of the
        /// <paramref name="naturalCount"/> healing on their own gets as much on average, and tended ones a share of the
        /// tended healing too.
        /// </summary>
        public static float WoundHealPerDay(float natural, int naturalCount, bool healsNaturally, float tended, int tendedCount, bool healsFromTending)
        {
            float v = 0f;
            if (healsNaturally && naturalCount > 0) v += natural / naturalCount;
            if (healsFromTending && tendedCount > 0) v += tended / tendedCount;
            return v;
        }

        /// <summary>
        /// A rough time, short: under a day in whole hours (at least one), under ten days to a tenth of a day, else in
        /// whole days; through <paramref name="hours"/> and <paramref name="days"/> ("~{0} h", "~{0} d").
        /// </summary>
        public static string RoughTime(float inDays, string hours, string days)
        {
            if (inDays < 0f) return "";
            if (inDays < 1f) return string.Format(hours, Math.Max(1, (int)Math.Round(inDays * 24f)));
            if (inDays < 10f) return string.Format(days, (Math.Round(inDays * 10f) / 10f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            return string.Format(days, ((int)Math.Round(inDays)).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Days until <paramref name="left"/> is gone at <paramref name="perDay"/>; under 0 when it isn't going.</summary>
        public static float DaysToHeal(float left, float perDay)
        {
            if (left <= 0f) return 0f;
            return perDay > Still ? left / perDay : -1f;
        }

        /// <summary>Days until a condition without an immunity race passes, its severity falling to nothing; under 0 when it isn't falling.</summary>
        public static float DaysToPass(float severity, float severityPerDay) => severityPerDay < -Still ? Math.Max(0f, severity) / -severityPerDay : -1f;

        /// <summary>
        /// Days until an illness is over, when immunity wins: until immune, with the severity still moving at today's pace,
        /// then until the severity falls to nothing at its pace once immune (<paramref name="severityPerDayImmune"/>,
        /// negative). Under 0 when it can't be said: no immunity gained, or it kills first.
        /// </summary>
        public static float DaysToRecover(float immunity, float immunityPerDay, float severity, float severityPerDay, float severityPerDayImmune, float lethal)
        {
            float toImmune = 0f;
            if (immunity < 0.999f)
            {
                if (immunityPerDay <= Still) return -1f;
                toImmune = (1f - immunity) / immunityPerDay;
            }
            float then = Math.Max(0f, severity + severityPerDay * toImmune);
            if (lethal > 0f && then >= lethal) return -1f;
            float after = severityPerDayImmune < -Still ? then / -severityPerDayImmune : 0f;
            return toImmune + after;
        }

        /// <summary>
        /// A colonist's row: bleeding (by the hours the game gives them; under 0 for none), else the most pressing of
        /// their conditions, with several untended counted together.
        /// </summary>
        public static float PawnScore(float bleedingHours, float worstCondition, int untended)
        {
            float score = worstCondition;
            if (bleedingHours >= 0f) score = Math.Max(score, HudRank.Bleeding(bleedingHours));
            if (untended > 0) score = Math.Max(score, HudRank.Untended(untended));
            return score;
        }
    }
}
