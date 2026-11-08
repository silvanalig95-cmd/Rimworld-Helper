using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a colonist's day has to hold for an ailment to come on. Most are counted in hours over the day (an hour
    /// counts when the hourly look finds them at it), from <see cref="HeavyWork"/> to <see cref="InSpace"/>; the rest are
    /// checked on their own (a wound, a bite, a cold in someone frail, the diet, sweets, a meal, a drink or a smoke). The
    /// numbers aren't saved anywhere, so new counted ones go before <see cref="Wound"/>; the hour's look
    /// (<see cref="AilmentChecks.Sample"/>) holds them as the bits of a long, so there is room for 63.
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
        /// <summary>Working outdoors between -10 and 6 °C (for an ordinary body: their own comfortable range moves it, Health/BodyMath.cs).</summary>
        ColdWork,
        /// <summary>Outdoors under open sky around midday in the heat, with nothing on the head.</summary>
        SunWork,
        /// <summary>In a small closed room with a wood fire burning.</summary>
        SmokyRoom,
        /// <summary>Research or hand work in the dark (not for eyes that see in it).</summary>
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
        /// <summary>On a caravan that's on the move (the sway of the pack animals, days on the road).</summary>
        CaravanTravel,
        /// <summary>Felling or harvesting trees.</summary>
        Chopping,
        /// <summary>Growing or cutting plants outdoors, above 10 °C.</summary>
        PlantWork,
        /// <summary>Outdoors below -10 °C (for an ordinary body, as above).</summary>
        BitterCold,
        /// <summary>Somewhere 32 °C or hotter (27 °C in a swamp or jungle), awake or asleep (for an ordinary body, as above).</summary>
        Sweltering,
        /// <summary>Outdoors in a swamp or jungle, 18 °C or warmer.</summary>
        Insects,
        /// <summary>On the move through water, marsh or mud.</summary>
        WetFeet,
        /// <summary>Awake in the small hours (midnight to five), unless they're a night owl.</summary>
        NightWork,
        /// <summary>Hungry or worse.</summary>
        Hungry,
        /// <summary>Mood below 30%, or strained (the heart's strain at 30 or more).</summary>
        Worry,
        /// <summary>Outdoors on polluted ground, or under the acid haze of a polluted world (Biotech).</summary>
        Polluted,
        /// <summary>Awake outdoors around midday on deep snow or ice, in bright sun, with nothing over the eyes.</summary>
        SnowGlare,
        /// <summary>Awake on a map in orbit (Odyssey).</summary>
        InSpace,
        /// <summary>Mining rock under a mountain roof, or cutting stone blocks at a bench in a closed room of 60 cells or fewer.</summary>
        StoneDust,
        /// <summary>On the knees: cleaning, or sowing.</summary>
        Kneeling,
        /// <summary>Handling animals: milking, shearing, training, taming.</summary>
        Handling,
        /// <summary>Working at an Alpha Bees beehouse or bee chamber (found by name: without the mod it never comes).</summary>
        Beekeeping,
        /// <summary>Swimming in still water: a lake, a pond or a marsh, never a river or the sea.</summary>
        StillSwim,
        /// <summary>Butchering a small wild animal (a hare, a squirrel, a rat): hunter's fever.</summary>
        SmallGame,
        /// <summary>Butchering an animal that had begun to rot, or was sick when it died: black bane.</summary>
        BadCarcass,
        /// <summary>Smithing or smelting in a closed room of 60 cells or fewer: the forge's fumes.</summary>
        MetalFumes,
        /// <summary>Outdoors with the feet in water, marsh or mud below 10 °C (for an ordinary body, as above).</summary>
        ColdWetFeet,
        /// <summary>Outdoors in an arid land from dusk to before dawn, at 20 °C or more: sandflies.</summary>
        Sandflies,
        /// <summary>Building, mining in the open, sowing or clearing in an arid land, with no rain falling: the dry soil's dust.</summary>
        DesertDust,
        /// <summary>Cleaning a closed room where birds are kept (chickens, ducks, geese, turkeys): the dust of their droppings.</summary>
        BirdBarn,
        /// <summary>On the move or at heavy or hand work with a limb of the old kinds: a peg leg, a wooden foot, a simple prosthetic.</summary>
        OldLimb,
        /// <summary>A deep wound (bite, scratch, stab, cut) left untended for hours.</summary>
        Wound,
        /// <summary>A bite from a warm-blooded animal (one roll per bite; a bite cleaned within hours halves it).</summary>
        AnimalBite,
        /// <summary>A bite or sting from an insectoid (one roll per wound).</summary>
        InsectBite,
        /// <summary>A cold, chest cold or flu in someone old, frail, starving or chilled to the bone.</summary>
        AfterCold,
        /// <summary>Weeks without fruit or vegetables.</summary>
        Diet,
        /// <summary>Sweets and the years.</summary>
        Sweets,
        /// <summary>A fine or lavish meal (rolled per meal, more often from 40 and again from 60).</summary>
        RichMeal,
        /// <summary>An alcoholic drink (rolled per drink).</summary>
        Drink,
        /// <summary>
        /// Getting over one of the illnesses in <see cref="AilmentCause.after"/> (HediffDef names, the game's or a mod's):
        /// rolled once, when it has gone, if it reached <see cref="AilmentCause.minSeverity"/>.
        /// </summary>
        AfterIllness,
        /// <summary>
        /// An injury of one of the kinds in <see cref="AilmentCause.after"/> (a crack, a burn) healed without a scar, on a
        /// part in <see cref="AilmentCause.partGroup"/> when one is named: rolled once per injury, when it has gone.
        /// </summary>
        Healed,
        /// <summary>
        /// Fought in a big fight at home (a raid), or was hurt in it: rolled once per fight, on the first night's sleep
        /// within a day of it; <see cref="AilmentCause.hurtFactor"/> times as likely for those who were hurt.
        /// </summary>
        AfterFight,
        /// <summary>
        /// Something that should not exist: a fresh memory of one of the thoughts in <see cref="AilmentCause.after"/>, or a
        /// wound from an entity (Anomaly). Rolled once per memory, and once a day for wounds.
        /// </summary>
        Horror,
        /// <summary>Raw meat or raw fish eaten, never anything cooked (rolled per meal).</summary>
        RawFood,
        /// <summary>A joint, a cigarette or a cigar (rolled per smoke, likelier for someone who smokes every day).</summary>
        Smoke,
        /// <summary>
        /// A cut, scratch, stab or bite left untended six hours in the wet heat (a swamp, jungle or rainforest at 25 °C or
        /// more): one roll per wound, in the hour it turns six hours old.
        /// </summary>
        WetHeatWound,
        /// <summary>
        /// Mechanites put in: a part or implant of the spacer age or later installed while one of the colony, or one of the
        /// hediffs in <see cref="AilmentCause.after"/> (luciferium). One roll per part, once they're up, within three days.
        /// </summary>
        Mechanites
    }

    /// <summary>One way an ailment can come on: what the day has to hold, how many hours of it, and the chance.</summary>
    public class AilmentCause
    {
        public AilmentTrigger trigger;
        /// <summary>Hours of it in one day before the ailment can come on (counted triggers only).</summary>
        public float hours = 4f;
        /// <summary>The chance, once that day (or per check for the special triggers), before the frequency setting.</summary>
        public float chance = 0.03f;
        /// <summary>Someone who has this already is likelier to get it this way (lice and typhus), by <see cref="boost"/>.</summary>
        public HediffDef boostHediff;
        public float boost = 1f;
        /// <summary>
        /// What it comes after: illnesses (AfterIllness), injuries (Healed) or memories (Horror), by def name, so another
        /// mod's or a DLC's simply doesn't count when it isn't there.
        /// </summary>
        public List<string> after;
        /// <summary>Healed: only an injury on a part in this body part group (or below one that is), e.g. Legs.</summary>
        public string partGroup;
        /// <summary>AfterIllness, Healed: only once the illness or injury has been at least this bad.</summary>
        public float minSeverity;
        /// <summary>AfterFight: the factor for those who were hurt in the fight.</summary>
        public float hurtFactor = 3f;
    }

    /// <summary>
    /// Someone likelier, or less likely, to come down with an ailment: a trait they have (tough), or a condition they
    /// carry already (a bad back, frailty). Names, not references, so another mod's trait or condition simply doesn't
    /// count when that mod is missing.
    /// </summary>
    public class AilmentRisk
    {
        public string trait;
        public string hediff;
        /// <summary>A gene they carry, active (Biotech): a strong stomach never takes worms.</summary>
        public string gene;
        public float factor = 1f;

        [Unsaved] private TraitDef traitDef;
        [Unsaved] private HediffDef hediffDef;
        [Unsaved] private GeneDef geneDef;
        [Unsaved] private bool resolved;

        public bool Applies(Pawn p)
        {
            if (!resolved)
            {
                resolved = true;
                if (!trait.NullOrEmpty()) traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(trait);
                if (!hediff.NullOrEmpty()) hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(hediff);
                if (!gene.NullOrEmpty() && ModsConfig.BiotechActive) geneDef = DefDatabase<GeneDef>.GetNamedSilentFail(gene);
            }
            if (traitDef != null && p.story?.traits != null && p.story.traits.HasTrait(traitDef)) return true;
            if (geneDef != null && p.genes != null && p.genes.HasActiveGene(geneDef)) return true;
            return hediffDef != null && p.health?.hediffSet != null && p.health.hediffSet.HasHediff(hediffDef);
        }
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
        /// <summary>Too small for a message unless the player wants every message (a stiff neck, a smoke cough).</summary>
        public bool quiet;
        /// <summary>Its short name for flavor tags: lines tagged [ail:key] are written for it.</summary>
        public string key;
        public List<AilmentCause> causes;
        public float cooldownDays = 15f;
        /// <summary>Factor on the chance for children (0: never).</summary>
        public float childFactor = 1f;
        /// <summary>Factor on the chance for the old (60 and up, in human years).</summary>
        public float oldFactor = 1f;
        /// <summary>Traits and conditions that make it likelier or less likely (a bad back, a tough constitution).</summary>
        public List<AilmentRisk> risks;
        /// <summary>Only children get it (measles: the grown-ups had it long ago).</summary>
        public bool childrenOnly;
        /// <summary>Only people this old or older, in human years (gout).</summary>
        public float minAge;
        /// <summary>
        /// Someone who has had fruit or vegetables within <see cref="freshDays"/> gets it this much less often from a
        /// meal or a drink (gout: half as often). 1: the diet makes no difference.
        /// </summary>
        public float freshFactor = 1f;
        public float freshDays = 3f;
        /// <summary>
        /// A skill (SkillDef name) that makes it rarer: a beekeeper who knows animals is stung less
        /// (<see cref="AilmentMath.SkillFactor"/>).
        /// </summary>
        public string skill;
        /// <summary>
        /// Only on a map of one of these biomes (BiomeDef names): an ailment of another mod's land, like the spores of
        /// Alpha Biomes' mycotic jungle. A biome that isn't in the game never matches, so without the mod it never comes.
        /// </summary>
        public List<string> biomes;
        /// <summary>Never twice in one person (an appendix, measles).</summary>
        public bool once;
        /// <summary>
        /// Given through the harm service's shared cap (Harm/Harm.cs): one Rimfolk-made hurt in three days, none while
        /// downed, in a sickbed, pregnant or fighting, none for babies, and for children only the <see cref="hurt"/>
        /// class Mild. Counted for the cap when it comes. The ailments of the land, the work and the age are.
        /// </summary>
        public bool capped;
        /// <summary>How bad a hurt it is for the cap: Mild (children may get it) or Injury (grown-ups only).</summary>
        public HurtClass hurt = HurtClass.Injury;
        /// <summary>
        /// Bed rest only when it makes sense (Health/BedRest.cs): the severity from which this ailment sends someone to bed,
        /// in place of the general rule (an illness that can kill, a stage in strong pain or that leaves a capacity poor);
        /// below it they keep working. 0 leaves it to the general rule. A serious illness from the stage it is really
        /// underway, its first fever worked through; tuberculosis from its wasting.
        /// </summary>
        public float restFrom;
        /// <summary>
        /// Its name in the colony's age, each tagged with the ages it's called so in ([tribal|medieval] pond itch,
        /// [industrial|spacer] swimmer's itch): the health tab, its message, letter and journal lines use it
        /// (<see cref="AilmentCauseMath.NameFor"/>). Without one, the def's label.
        /// </summary>
        [MustTranslate] public List<string> names;
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
        /// <summary>What it leaves behind when it's cured outright rather than got over (surgery for appendicitis).</summary>
        public List<AilmentAfterEffect> curedAfterEffects;
        /// <summary>A short message when it comes on: {PAWN} is who has it.</summary>
        [MustTranslate] public string gotText;
        /// <summary>
        /// More ways to say <see cref="gotText"/>, tagged like any line ([winter], [night], [old], [trait:Tough], and [again]
        /// when they've had it before): one that fits is picked, the plain one among them (<see cref="AilmentTexts"/>).
        /// </summary>
        [MustTranslate] public List<string> gotTexts;
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
            if (capped && serious && hurt == HurtClass.Mild) yield return "a serious illness is never mild enough for children";
            if (!names.NullOrEmpty() && !typeof(Hediff_Ailment).IsAssignableFrom(parentDef.hediffClass)) yield return "an ailment named by the age needs hediffClass ColonyCouncil.Hediff_Ailment";
            if (restFrom < 0f) yield return "restFrom is a severity: 0 for the general rule, or the severity bed rest starts at";
            if (restFrom > 0f && restFrom > parentDef.maxSeverity) yield return "restFrom above the ailment's highest severity never comes";
            if (causes != null)
                foreach (var c in causes)
                    if (c != null && (c.trigger == AilmentTrigger.AfterIllness || c.trigger == AilmentTrigger.Healed) && c.after.NullOrEmpty())
                        yield return "a cause after an illness or an injury needs <after>";
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
        /// <summary>Weight factor while it's 25 °C or warmer outside (kidney stones come with the heat).</summary>
        public float hotFactor = 1f;
        /// <summary>Only where plants grow (pollen).</summary>
        public bool needsPlants;
        /// <summary>Only people this old or older, in human years (shingles).</summary>
        public float minAge;
        /// <summary>Picks the same colonists as before when it can (hay fever comes back to the same people).</summary>
        public bool sameVictims;
        /// <summary>The letter's opening; the list of who caught it follows, then <see cref="letterAfter"/>. {PAWN} is the first of them.</summary>
        [MustTranslate] public string letter;
        /// <summary>
        /// More openings for the letter, tagged like any line ([winter], [lowtech], [small], [again] when one of them has had
        /// it before, [one] when it's one person): one that fits is picked, the plain one among them.
        /// </summary>
        [MustTranslate] public List<string> letters;
        [MustTranslate] public string letterAfter;
    }

    /// <summary>An ailment's message or letter told one of its ways: the first as written, or a variant that fits the moment.</summary>
    public static class AilmentTexts
    {
        /// <summary>
        /// One of <paramref name="first"/> and <paramref name="more"/> that fits <paramref name="p"/>'s moment and the
        /// <paramref name="extra"/> tags, not one given out lately while another fits; tags removed. The first as written
        /// when there are no variants or none fits.
        /// </summary>
        public static string Pick(string first, List<string> more, Pawn p, params string[] extra)
        {
            if (more.NullOrEmpty()) return first;
            var all = new List<string>(more.Count + 1);
            if (!first.NullOrEmpty()) all.Add(first);
            all.AddRange(more);
            var tags = Flavor.TagsFor(p);
            foreach (var t in extra)
                if (!t.NullOrEmpty())
                    tags.Add(t);
            return Flavor.PickFresh(all, tags, more) ?? first;
        }
    }
}
