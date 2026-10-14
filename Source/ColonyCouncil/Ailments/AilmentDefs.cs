using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a colonist's day has to hold for an ailment to come on. Most are counted in hours over the day (an hour
    /// counts when the hourly look finds them at it); the last four are checked on their own (a wound, a cold in someone
    /// frail, the diet, sweets).
    /// </summary>
    public enum AilmentTrigger
    {
        None,
        /// <summary>Hauling, mining or construction.</summary>
        HeavyWork,
        /// <summary>Crafting, smithing, tailoring or art.</summary>
        HandWork,
        /// <summary>Travelling with a caravan that's on the move, or walking about the map.</summary>
        Walking,
        /// <summary>Working outdoors between -10 and 6 °C.</summary>
        ColdWork,
        /// <summary>Outdoors under open sky around midday in the heat, with nothing on the head.</summary>
        SunWork,
        /// <summary>In a small closed room with a wood fire burning.</summary>
        SmokyRoom,
        /// <summary>Research or hand work in the dark.</summary>
        DarkWork,
        /// <summary>On the move outdoors over marsh, mud, sand, shallow water or deep snow.</summary>
        RoughGround,
        /// <summary>Asleep on the ground, a sleeping spot, a bedroll or a hard bed.</summary>
        HardBed,
        /// <summary>Asleep in a damp room: dirty and under rock, open to rain, or dirty in a wet biome.</summary>
        DampSleep,
        /// <summary>Asleep in a dirty room of four or more beds, or unwashed in a shared room.</summary>
        CrowdedDirty,
        /// <summary>Beating out fires.</summary>
        Firefighting,
        /// <summary>Outdoors under volcanic ash, or in an ash or lava biome.</summary>
        Ashfall,
        /// <summary>Working on something ancient: breaking into ruins, opening caskets, hauling ancient junk.</summary>
        AncientWork,
        /// <summary>A deep wound (bite, scratch, stab, cut) left untended for hours.</summary>
        Wound,
        /// <summary>A cold, chest cold or flu in someone old, frail, starving or chilled to the bone.</summary>
        AfterCold,
        /// <summary>Weeks without fruit or vegetables.</summary>
        Diet,
        /// <summary>Sweets and the years.</summary>
        Sweets
    }

    /// <summary>One way an ailment can come on: what the day has to hold, how many hours of it, and the chance.</summary>
    public class AilmentCause
    {
        public AilmentTrigger trigger;
        /// <summary>Hours of it in one day before the ailment can come on (counted triggers only).</summary>
        public float hours = 4f;
        /// <summary>The chance, once that day (or per check for the special triggers), before the frequency setting.</summary>
        public float chance = 0.03f;
    }

    /// <summary>What getting over an illness can leave behind for a while: a cough, some weakness.</summary>
    public class AilmentAfterEffect
    {
        public HediffDef hediff;
        public float chance = 1f;
        public FloatRange severity = new FloatRange(0.2f, 0.4f);
    }

    /// <summary>
    /// The heart of every Rimfolk ailment and illness: how it comes on, how it's announced, what a doctor says about it,
    /// and what it leaves behind. Its comp hears when the illness ends (recovered, cured or fatal).
    /// </summary>
    public class HediffCompProperties_Ailment : HediffCompProperties
    {
        /// <summary>Part of the serious tier: rare, possibly fatal, behind its own setting, one kind in the colony at a time.</summary>
        public bool serious;
        /// <summary>What an illness leaves behind (a lingering cough): no journal lines or messages of its own.</summary>
        public bool lingering;
        /// <summary>Its short name for flavor tags: lines tagged [ail:key] are written for it.</summary>
        public string key;
        public List<AilmentCause> causes;
        public float cooldownDays = 15f;
        /// <summary>Factor on the chance for children (0: never).</summary>
        public float childFactor = 1f;
        /// <summary>Factor on the chance for the old (60 and up, in human years).</summary>
        public float oldFactor = 1f;
        /// <summary>The severity it starts at when it comes on by circumstance (or by an incident, for those without immunity).</summary>
        public FloatRange startSeverity = new FloatRange(0.3f, 0.5f);
        /// <summary>Passes to people who sleep in the same room as someone who has it (and shows symptoms).</summary>
        public float spreadPerHour;
        /// <summary>Spreads only in a dirty room or to someone unwashed (lice).</summary>
        public bool spreadNeedsDirt;
        /// <summary>The factor on spreading while the sick one is being treated.</summary>
        public float spreadWhileTended = 0.25f;
        /// <summary>No more than this many in the colony have it at once through spreading.</summary>
        public int maxCases = 3;
        public List<AilmentAfterEffect> afterEffects;
        /// <summary>A short message when it comes on: {PAWN} is who has it.</summary>
        [MustTranslate] public string gotText;
        /// <summary>The letter when a serious illness shows itself: {PAWN} is who has it.</summary>
        [MustTranslate] public string letterText;
        /// <summary>A doctor's note for the letter; lines may be tagged [lowtech] or [!lowtech].</summary>
        [MustTranslate] public List<string> notes;

        public HediffCompProperties_Ailment()
        {
            compClass = typeof(HediffComp_Ailment);
        }

        public bool HasCause(AilmentTrigger t)
        {
            if (causes == null) return false;
            foreach (var c in causes)
                if (c != null && c.trigger == t) return true;
            return false;
        }

        public AilmentCause Cause(AilmentTrigger t)
        {
            if (causes == null) return null;
            foreach (var c in causes)
                if (c != null && c.trigger == t) return c;
            return null;
        }

        public override IEnumerable<string> ConfigErrors(HediffDef parentDef)
        {
            foreach (var e in base.ConfigErrors(parentDef)) yield return e;
            if (key.NullOrEmpty()) yield return "ailment has no key";
            if (notes.NullOrEmpty()) yield return "ailment has no doctor's notes";
            if (!serious && parentDef.lethalSeverity > 0f) yield return "an everyday ailment must never be lethal";
            if (serious && letterText.NullOrEmpty() && !(causes == null || causes.Count == 0)) yield return "a serious illness needs a letter";
        }
    }

    /// <summary>
    /// On a Rimfolk disease incident: when it can come (seasons, temperature, plants), how likely it is next to the
    /// other catching ailments, and the letter's text ({VICTIMS} is the list of who caught it).
    /// </summary>
    public class AilmentIncident : DefModExtension
    {
        public float weight = 1f;
        public List<Season> seasons;
        /// <summary>Only while it's this cold or warm outside (°C); the defaults never stop it.</summary>
        public float maxOutdoorTemp = 999f;
        public float minOutdoorTemp = -999f;
        /// <summary>Weight factor in fall and winter (colds go round when people huddle indoors).</summary>
        public float coldSeasonFactor = 1f;
        /// <summary>Only where plants grow (pollen).</summary>
        public bool needsPlants;
        /// <summary>Picks the same colonists as before when it can (hay fever comes back to the same people).</summary>
        public bool sameVictims;
        /// <summary>The letter's opening; the list of who caught it follows, then <see cref="letterAfter"/>. {PAWN} is the first of them.</summary>
        [MustTranslate] public string letter;
        [MustTranslate] public string letterAfter;
    }
}
