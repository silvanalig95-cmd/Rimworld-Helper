using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// What a god's occurrence was like for those who saw it (the player, 15 November 2026: "for many of the gods occurences i
    /// don't see any real game impact ... pawns seem to take no mood or heart/strain or other gameplay impact either").
    /// </summary>
    public enum TouchTone
    {
        /// <summary>Nothing to feel: a god leaving, a mark talked of, something told in a letter.</summary>
        None,
        /// <summary>A kindness: a blessing, a gift, a mended thing.</summary>
        Kind,
        /// <summary>A god seen at work or coming: awe more than kindness.</summary>
        Sight,
        /// <summary>A sign: an omen, a strange small thing.</summary>
        Sign,
        /// <summary>An anger, or the dark god at work.</summary>
        Dark
    }

    /// <summary>How someone of the colony stands toward the god they took it for.</summary>
    public enum TouchStance
    {
        /// <summary>No strong belief either way.</summary>
        Ordinary,
        /// <summary>They hold to that god.</summary>
        Devoted,
        /// <summary>They are in dread of it.</summary>
        Dreads,
        /// <summary>They doubt the gods altogether: unmoved.</summary>
        Scoffer
    }

    /// <summary>How strong what follows is: most are small, some notable, a few great.</summary>
    public enum TouchTier
    {
        None,
        Small,
        Notable,
        Great
    }

    /// <summary>The mood someone takes from it (Defs/GodDefs/Rimfolk_GodTouch.xml; Acted is the belief round's own).</summary>
    public enum TouchMood
    {
        None,
        /// <summary>Their own god was kind before their eyes (+4), or seen plainly (+6).</summary>
        Blessed,
        /// <summary>Their own god showed itself (+3: Rimfolk_Belief_GodActed).</summary>
        Acted,
        /// <summary>Someone of no strong belief saw something of the gods (+2), or saw a god plainly (+4).</summary>
        Wonder,
        /// <summary>An odd sign (-1), or an anger seen (-3).</summary>
        Uneasy,
        /// <summary>The one they dread was near (-3), or showed itself (-5).</summary>
        Afraid
    }

    /// <summary>
    /// What a god's occurrence leaves on the map and in the colony's people, beyond the moods (Gods/Touch/GodTouchEffects.cs).
    /// Each god has its own few (<see cref="GodTouchMath.Of"/>); the same kind of effect does each god's own thing (the god's
    /// blessing is its own hediff, the find its own goods). Saved by name, so new kinds may go anywhere.
    /// </summary>
    public enum TouchEffect
    {
        None,
        // ---- Small and kind ----
        /// <summary>The god's own short blessing on those who saw it: a hediff for about a day (Rimfolk_GodTouch_Bless_&lt;God&gt;).</summary>
        Bless,
        /// <summary>Those who saw it come away better at the god's work (skill learning).</summary>
        Learn,
        /// <summary>Flowers come up in a ring round the place (Hollin).</summary>
        Bloom,
        /// <summary>The fires and hearths near the place burn full again (Smoulder).</summary>
        Refuel,
        /// <summary>Broken-down machines near the place work again, and worn buildings are made whole (Pip).</summary>
        Mend,
        /// <summary>Every battery on the map is full (Pip).</summary>
        Charge,
        /// <summary>The weather clears (Cirrus, Helios).</summary>
        ClearSky,
        /// <summary>Snow falls, where the land is cold enough (Hush).</summary>
        Snow,
        /// <summary>Rain comes, where it isn't freezing (Murmur).</summary>
        Rain,
        /// <summary>The rats and other vermin walk off the land (Tamiu).</summary>
        RatsGone,
        // ---- Notable and kind ----
        /// <summary>An inspiration of the god's kind for one who saw it (the game's own, and the player's mods' by name).</summary>
        Inspire,
        /// <summary>Something left where it happened: the god's own goods, by the colony's age.</summary>
        Find,
        /// <summary>The crops near the place grow on overnight (Hollin, Helios, Murmur).</summary>
        Ripen,
        /// <summary>Wild healroot comes up near the place (Hollin).</summary>
        Healroot,
        /// <summary>Wild game of the land's own kinds comes onto the map (Bellwether).</summary>
        Game,
        /// <summary>The ground where it happened turns rich (Morel).</summary>
        RichSoil,
        /// <summary>Travellers come to the colony soon after (Ennu).</summary>
        Visitors,
        /// <summary>A people at peace hears of it and thinks better of the colony (Ennu).</summary>
        Goodwill,
        /// <summary>A trader comes (Ennu's caravan; Ducat's, or one in orbit for a colony with comms).</summary>
        Trader,
        /// <summary>The night sky lights up (the game's aurora; Cirrus).</summary>
        Aurora,
        /// <summary>Something falls from the sky near the edge of the land (the game's ship chunk; Cirrus).</summary>
        SkyGift,
        /// <summary>The stores keep: the food's spoiling set back days (Hush).</summary>
        Preserve,
        /// <summary>A cold snap breaks (Hush; the stores kept instead when there is none).</summary>
        ColdBreak,
        /// <summary>Wounds heal in the night (Selene).</summary>
        Heal,
        /// <summary>A cat walks in and stays with the colony (Tamiu).</summary>
        CatComes,
        /// <summary>Fish left on the bank by the water (Murmur; Odyssey's fish).</summary>
        Fish,
        // ---- Great and kind ----
        /// <summary>The god's own presence stays on the land for days (its condition: GodDef.presence).</summary>
        Linger,
        /// <summary>A vein of gold comes up in the rock nearby (Smoulder).</summary>
        Vein,
        /// <summary>Tame animals wander in and stay (the game's own; Bellwether).</summary>
        Herd,
        /// <summary>The soil of the fields round the place turns rich (Morel).</summary>
        Fertile,
        /// <summary>Someone comes to join the colony (the game's own wanderer; Ennu).</summary>
        Wanderer,
        /// <summary>Everyone at the home bears the cold for a season (Hush).</summary>
        Hardy,
        /// <summary>Every crop grows a long way on in a day, and everyone is warmed through for days (Helios).</summary>
        SunYear,
        /// <summary>An old wound healed in the night (Selene).</summary>
        ScarGone,
        /// <summary>One who saw it calm and steady for a season (Tamiu).</summary>
        Ward,
        /// <summary>A spring wells up where it happened: a small pond (Murmur).</summary>
        Spring,
        /// <summary>Gold found where it happened (Ducat).</summary>
        Hoard,
        // ---- Dark ----
        /// <summary>Brambles come up across the place (Hollin).</summary>
        Brambles,
        /// <summary>The fires near the place go out (Smoulder, Hush, Undertow).</summary>
        FiresOut,
        /// <summary>The wild beasts walk off the land (Bellwether).</summary>
        GameLeaves,
        /// <summary>Food in the stores starts to go off (Morel).</summary>
        Spoil,
        /// <summary>A machine breaks down (the game's own breakdown; Pip).</summary>
        Breakdown,
        /// <summary>Fog comes down (Cirrus, Murmur).</summary>
        Fog,
        /// <summary>A few of a stack gone from the stores, never all (Undertow; silver for Ducat).</summary>
        Vanish,
        /// <summary>Those who saw it sleep badly the night after (Selene).</summary>
        Sleepless,
        /// <summary>The dark stays on the land a day or two (Undertow's own presence).</summary>
        Dim,
        // ---- Revel's (the fifteenth; saved by name, so they go at the end) ----
        /// <summary>Those who saw it come away in high spirits, their recreation filled (Revel).</summary>
        Merry,
        /// <summary>Some of the drink in the stores turns sour and spills, never all of it (Revel's anger).</summary>
        Sour,
        // ---- Each occurrence's own (Gods/Touch/GodImpact.cs, GodImpactMath.Table; saved by name, so they go at the end) ----
        /// <summary>A body rots away to bones in a day or two, and the ground inside the ring round it turns rich.</summary>
        RotAway,
        /// <summary>The rotten stack in the stores is gone, and the floor under it clean.</summary>
        RotGone,
        /// <summary>Those who mourn the dead feel their grief ease sooner.</summary>
        GriefEased,
        /// <summary>The food stored in a room keeps a little less long for a few days.</summary>
        KeepsLess,
        /// <summary>Those closest to a dead person sleep well that night and wake rested.</summary>
        RestKin,
        /// <summary>A few tender crops on a line of frost take the cold (the game's own).</summary>
        FrostLine,
        /// <summary>A room stays cold for a day.</summary>
        ColdRoom,
        /// <summary>Those who stood there are fully rested.</summary>
        Rested,
        /// <summary>The crops in the open grow a day ahead, and solar panels give their most until sundown.</summary>
        LongNoon,
        /// <summary>The open ground a few degrees warmer for the afternoon.</summary>
        Warmth,
        /// <summary>Nothing comes to the colony in the night while the door is kept.</summary>
        NightWard,
        /// <summary>Everyone at the home sleeps deeper that night.</summary>
        MoonSleep,
        /// <summary>Someone near death feels less pain while a cat stays by them.</summary>
        PainEased,
        /// <summary>Someone who came through heals faster for a few days.</summary>
        HealFaster,
        /// <summary>A binge ends sooner, and the binger's recreation is full.</summary>
        BingeEnds,
        /// <summary>A drink of the colony's kind left where it happened.</summary>
        Drink,
        /// <summary>A drink spilled and lost (the sign's own).</summary>
        Spill,
        /// <summary>Those who were there cheered for the day.</summary>
        Cheered,
        /// <summary>A gold coin lying there (the sign's own).</summary>
        Coin,
        /// <summary>A little silver under a table.</summary>
        Silver,
        /// <summary>A light keeps glowing all night with no power.</summary>
        StaysLit,
        /// <summary>A machine's hour running on costs no power and no fuel.</summary>
        FreeHour,
        /// <summary>A few chunks of the place's own stone come down.</summary>
        StoneChunks,
        /// <summary>Miners dig faster for a day.</summary>
        DigFaster,
        /// <summary>The ground round a place stays warm a few days: no snow on it, the plants near it growing.</summary>
        WarmGround,
        /// <summary>While the wind is dead, the turbines stop and fire doesn't spread.</summary>
        StillAir,
        /// <summary>The anima tree's grass spreads a little, and meditating at it is better for a day.</summary>
        AnimaHum,
        /// <summary>One of the colony's herd animals is with young.</summary>
        WithYoung,
        /// <summary>The colony's animals calm and easy to handle for a day.</summary>
        AnimalsCalm,
        /// <summary>A spot stays dark for the night.</summary>
        StaysDark,
        /// <summary>The one closest to a dead person dreams of them kindly.</summary>
        KindDream,
        /// <summary>Two who had the same dream think better of each other.</summary>
        SameDream,
        /// <summary>Everyone's next hour of work goes better.</summary>
        StillHour,
        /// <summary>A work of art is admired more.</summary>
        Admired
    }

    /// <summary>One of a god's effects: what, how strong, and whether it is one of its angers.</summary>
    public struct TouchEntry
    {
        public TouchEffect effect;
        public TouchTier tier;
        public bool dark;

        public TouchEntry(TouchEffect effect, TouchTier tier, bool dark = false)
        {
            this.effect = effect;
            this.tier = tier;
            this.dark = dark;
        }

        public override string ToString() => effect + (dark ? " (dark)" : "") + ", " + tier;
    }

    /// <summary>
    /// The pure rules of the gods' touch (Gods/Touch/): what each god's occurrence leaves behind, felt by the pawns and seen
    /// on the map. The player, 15 November 2026: "for many of the gods occurences i don't see any real game impact ... the
    /// stranger one at the altar i have and had three times already this event. there should be more ingame effect. also
    /// pawns seem to take no mood or heart/strain or other gameplay impact either"; then "effects can be all kinds of things,
    /// be creative"; and "the effect can be something more impactful also, remember these are gods. but keep it balanced,
    /// not that strong effects come up all the time. there should be range and variety". So:
    /// <list type="bullet">
    /// <item>moods for everyone who saw it, by their belief: a believer of that god glad, one who dreads it afraid, someone of no
    /// strong belief awed or uneasy, a scoffer unmoved (<see cref="MoodFor"/>), and the heart eased or strained a little
    /// (<see cref="EaseFor"/>, <see cref="StrainFor"/>);</item>
    /// <item>for an occurrence with no effect of its own (<see cref="HasOwnEffect"/>), one of its god's own effects
    /// (<see cref="Of"/>), in three strengths: small most of the time, notable now and then, great rarely (<see cref="Tier"/>,
    /// each with its own wait, per god and for the colony), the same effect leaned away from (<see cref="Pick"/>);</item>
    /// <item>the same occurrence soon again leaves nothing but the moods (<see cref="SameKeyGapDays"/>), a god that came often
    /// lately gives the others their turn (<see cref="Turn"/>), and an offering taken before everyone's eyes is a god's moment
    /// once a month at most (<see cref="PlainTaking"/>).</item>
    /// </list>
    /// Free of game types. Never a favour: nothing here reads or shows how a god stands with the colony.
    /// </summary>
    public static class GodTouchMath
    {
        /// <summary>
        /// A god's mood's "Why": what was seen (one sentence, closed with a full stop if it has no end of its own), then whose
        /// doing the one who felt it holds it was; either alone when the other is missing.
        /// </summary>
        public static string Why(string words, string whose)
        {
            string w = (words ?? "").Trim();
            if (w.Length > 0 && !w.EndsWith(".") && !w.EndsWith("!") && !w.EndsWith("?")) w += ".";
            string h = (whose ?? "").Trim();
            if (w.Length == 0) return h.Length == 0 ? null : h;
            return h.Length == 0 ? w : w + " " + h;
        }

        // ================= The occurrence =================

        /// <summary>
        /// Something that comes before, not instead: an omen precedes a coming, a dream precedes its gift. A god's coming
        /// or a god seen plainly is a big occurrence (a great effect may follow it); a departure or a mark is none.
        /// </summary>
        public static bool Big(TouchTone tone, bool appearOrReveal, bool plain) => tone != TouchTone.None && tone != TouchTone.Sign && (appearOrReveal || plain);

        /// <summary>
        /// The keys of what was seen whose occurrences already have their own effect on the map: they get the moods only. A key
        /// ending in "_" stands for every key it begins (the weather's answers, the moon's returns, the small tests).
        /// </summary>
        public static readonly string[] OwnEffectKeys =
        {
            // The gods' moments in the world (Gods/World/: a kindness or an anger in its own shape, a gift, the Hunger's due),
            // and the colony's own worship (a prayer answered, the council's answers): each is its own effect. A broken vow's
            // shadow ("vow") has none of its own, so the god's anger may follow it.
            "world", "prayer", "council",
            // On the map: the waking's collapse or true form, the stranger's judgement as it goes, the weather's answer, the late
            // gift, the sacred beast's coming, going, sitting near it, its lead, harm done to it, the moon's return, the great cat.
            "CC_GodM_Saw_Woken", "CC_GodM_Saw_Quake", "CC_GodM_Saw_StrangerShown", "CC_GodM_Saw_StrangerGone", "CC_GodM_Saw_StrangerStruckShown",
            "CC_GodM_Saw_StrangerStruckGone", "CC_GodM_Saw_Weather_", "CC_GodM_Saw_LateGift", "CC_GodM_Saw_Animal", "CC_GodM_Saw_AnimalKin",
            "CC_GodM_Saw_AnimalGone", "CC_GodM_Saw_AnimalGoneHurt", "CC_GodM_Saw_Horn", "CC_GodM_Saw_Sat", "CC_GodM_Saw_Led", "CC_GodM_Saw_Struck",
            "CC_GodM_Saw_Slain", "CC_GodM_Saw_KinStruck", "CC_GodM_Saw_Return_", "CC_GodM_Saw_RiddleRight", "CC_GodM_Saw_RiddleWrong",
            "CC_GodM_Saw_RiddleCuffed", "CC_GodM_Saw_WrappedDead",
            // Each god's own happenings that carry their own effect: the bargain and its vein, the silver seam, the burn, the
            // falling star and the pods, the aurora, the wounded thrumbo tended, the white beast kept waiting, the old tree's
            // healroot, the green walk's grass, the ring dug up, the bowl found empty, the turret, the small tests, the kindest's
            // news, the sulk before the short circuit, the frost-thaw, Murmur's rain, give-back and frogs, Ducat's trail and
            // coin, the moonlit watch, the hollow ear, the beasts shunning, the dark kept off or come near.
            "CC_GodO_Saw_BargainDream", "CC_GodO_Saw_BargainDown", "CC_GodO_Saw_Vein", "CC_GodO_Saw_VeinBent", "CC_GodO_Saw_Seam", "CC_GodO_Saw_Burn",
            "CC_GodO_Saw_Star", "CC_GodO_Saw_FallDream", "CC_GodO_Saw_Pods", "CC_GodO_Saw_Aurora", "CC_GodO_Saw_Tended", "CC_GodO_Saw_RoadWait",
            "CC_GodO_Saw_OldTree", "CC_GodO_Saw_Walk", "CC_GodO_Saw_WalkAlone", "CC_GodO_Saw_RingBuried", "CC_GodO_Saw_Bowl", "CC_GodO_Saw_Turret",
            "CC_GodO_Saw_Kindest", "CC_GodO_Saw_Sulk",
            "CC_GodHush_Saw_Thaw", "CC_GodMurmur_Saw_", "CC_GodDucat_Saw_", "CC_GodSelene_Saw_Watch", "CC_GodUndertow_Saw_",
            // Revel's: the long night's joy, the vision walked to, the craving's dose, the stash, the craving eased, his cup shared
            // (his binge seen has none of its own, so one of his may follow it).
            "CC_GodRevel_Saw_Night", "CC_GodRevel_Saw_Vision", "CC_GodRevel_Saw_Craving", "CC_GodRevel_Saw_Stash", "CC_GodRevel_Saw_LetGo", "CC_GodRevel_Saw_Shared",
            // The gods between the colony's people (Gods/OnMap/Between/): each ends in what it does between two people (their
            // memories of each other, a quarrel, a kindness), so nothing of the touch's own comes on top; those who saw it feel it.
            "CC_GodB_Saw_",
            // The content round 3's on the land (Gods/OnMap/OwnRound3.cs): each leaves its own on the map (the trodden ring and
            // the warm newborn, the ripe field, the rich soil, the work done, the tide), or is a sight seen (the giant in the mist).
            "CC_GodR3_Saw_"
        };

        /// <summary>The Guest's small tests' keys begin so (CC_GodO_Saw_TestStory, _TestCook ...).</summary>
        private const string TestKeys = "CC_GodO_Saw_" + "Test";

        /// <summary>Keys that are not an occurrence on the map at all: something told in a letter, a mark talked of.</summary>
        public static readonly string[] WordsOnlyKeys = { "told", "mark" };

        /// <summary>
        /// Its words key (or the part's own word for it: "world", "prayer", "council") names an occurrence that brings
        /// its own effect: the moods only. Exact, or under a key ending in "_"; and every occurrence the gods' doings table
        /// gives an effect of its own (GodImpactMath.Table).
        /// </summary>
        public static bool HasOwnEffect(string key) => GodImpactMath.Has(key) || InOwnEffectKeys(key);

        /// <summary>Its key is one of <see cref="OwnEffectKeys"/> (exact, or under one ending in "_"), or one of the small tests'.</summary>
        public static bool InOwnEffectKeys(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            foreach (var k in OwnEffectKeys)
                if (key == k || k.EndsWith("_", StringComparison.Ordinal) && key.StartsWith(k, StringComparison.Ordinal))
                    return true;
            // The small tests are keyed by the test (CC_GodO_Saw_TestStory ...).
            return key.StartsWith(TestKeys, StringComparison.Ordinal);
        }

        /// <summary>Its key names words only (a letter's tale, a mark talked of): no mood and no effect.</summary>
        public static bool WordsOnly(string key) => !string.IsNullOrEmpty(key) && WordsOnlyKeys.Contains(key);

        // ================= Moods =================

        /// <summary>
        /// The mood for someone of the colony who saw it, and its stage: by how they stand toward the god they took it for and
        /// what it was like. A scoffer is unmoved; someone in dread of that god takes no comfort from its kindness and is afraid
        /// of its coming; a believer is glad of its kindness (the more, seen plainly) and shaken by its anger.
        /// </summary>
        public static (TouchMood mood, int stage) MoodFor(TouchStance s, TouchTone t, bool plain)
        {
            if (t == TouchTone.None || s == TouchStance.Scoffer) return (TouchMood.None, 0);
            switch (s)
            {
                case TouchStance.Devoted:
                    switch (t)
                    {
                        case TouchTone.Kind: return (TouchMood.Blessed, plain ? 1 : 0);
                        case TouchTone.Sight: return plain ? (TouchMood.Blessed, 1) : (TouchMood.Acted, 0);
                        case TouchTone.Sign: return (TouchMood.Acted, 0);
                        default: return (TouchMood.Uneasy, 1);
                    }
                case TouchStance.Dreads:
                    switch (t)
                    {
                        case TouchTone.Kind: return (TouchMood.None, 0);
                        // A sign of the one they dread: the belief round's own flinch has it (Rimfolk_Belief_Flinched).
                        case TouchTone.Sign: return (TouchMood.None, 0);
                        default: return (TouchMood.Afraid, plain ? 1 : 0);
                    }
                default:
                    switch (t)
                    {
                        case TouchTone.Kind:
                        case TouchTone.Sight:
                            return (TouchMood.Wonder, plain ? 1 : 0);
                        case TouchTone.Sign: return (TouchMood.Uneasy, 0);
                        default: return (TouchMood.Uneasy, 1);
                    }
            }
        }

        /// <summary>Each mood's size by stage, as Rimfolk_GodTouch.xml has them (Acted is Rimfolk_Belief_GodActed's +3).</summary>
        public static float MoodSize(TouchMood m, int stage)
        {
            switch (m)
            {
                case TouchMood.Blessed: return stage > 0 ? 6f : 4f;
                case TouchMood.Acted: return 3f;
                case TouchMood.Wonder: return stage > 0 ? 4f : 2f;
                case TouchMood.Uneasy: return stage > 0 ? -3f : -1f;
                case TouchMood.Afraid: return stage > 0 ? -5f : -3f;
                default: return 0f;
            }
        }

        /// <summary>How long each mood lasts, in days (the defs' durationDays).</summary>
        public static float MoodDays(TouchMood m) => m == TouchMood.Blessed || m == TouchMood.Afraid ? 1.5f : m == TouchMood.Acted || m == TouchMood.Wonder || m == TouchMood.Uneasy ? 1f : 0f;

        /// <summary>
        /// Strain eased for a believer who saw a kindness or a sight of their god (the more, seen plainly), and a little for
        /// someone of no strong belief; through the heart's small joys and their cap (HeartLogic.EaseJoys).
        /// </summary>
        public static float EaseFor(TouchStance s, TouchTone t, bool plain)
        {
            if (s == TouchStance.Scoffer || s == TouchStance.Dreads) return 0f;
            if (s == TouchStance.Devoted)
            {
                if (t == TouchTone.Kind) return plain ? 2.5f : 1.5f;
                if (t == TouchTone.Sight || t == TouchTone.Sign) return plain ? 2f : 1f;
                return 0f;
            }
            return t == TouchTone.Kind || t == TouchTone.Sight ? (plain ? 1f : 0.5f) : 0f;
        }

        /// <summary>
        /// Strain for someone in dread of that god when it comes or is angry (the more, seen plainly), a little for anyone at
        /// its anger; through the heart's dread and its cap (HeartLogic.Build, StrainSource.Dread).
        /// </summary>
        public static float StrainFor(TouchStance s, TouchTone t, bool plain)
        {
            if (s == TouchStance.Scoffer) return 0f;
            if (s == TouchStance.Dreads)
            {
                if (t == TouchTone.Kind) return 0f;
                if (t == TouchTone.Sign) return 0.5f;
                return plain ? 2f : 1f;
            }
            return t == TouchTone.Dark ? (plain ? 1f : 0.5f) : 0f;
        }

        // ================= How strong: small, notable, great =================

        /// <summary>A god's small effects at most this often at a home (twice a day).</summary>
        public const float SmallGodDays = 0.5f;

        /// <summary>A god's notable effects this many days apart at a home, and any god's this many.</summary>
        public const float NotableGodDays = 8f, NotableColonyDays = 3f;

        /// <summary>A god's great effects this many days apart at a home (two a year), and any god's this many (a season and a half).</summary>
        public const float GreatGodDays = 120f, GreatColonyDays = 40f;

        /// <summary>The chance an occurrence with no effect of its own brings one at all, by what it was like.</summary>
        public static float AnyChance(TouchTone t)
        {
            switch (t)
            {
                case TouchTone.Kind: return 0.9f;
                case TouchTone.Sight: return 0.8f;
                case TouchTone.Dark: return 0.5f;
                case TouchTone.Sign: return 0.35f;
                default: return 0f;
            }
        }

        /// <summary>The chance it is notable rather than small: a third, more for a god seen plainly or a coming.</summary>
        public static float NotableChance(TouchTone t, bool big, bool plain)
        {
            if (t == TouchTone.None || t == TouchTone.Sign) return 0f;
            float c = 0.3f * (plain ? 1.4f : 1f) * (big ? 1.3f : 1f);
            return Math.Min(0.6f, t == TouchTone.Dark ? c * 0.6f : c);
        }

        /// <summary>The chance it is great: one in twenty-five, four times that for a big occurrence seen plainly; half for an anger.</summary>
        public static float GreatChance(TouchTone t, bool big, bool plain)
        {
            if (t == TouchTone.None || t == TouchTone.Sign) return 0f;
            float c = 0.04f * (plain ? 2f : 1f) * (big ? 2f : 1f);
            return t == TouchTone.Dark ? c * 0.5f : c;
        }

        /// <summary>
        /// How strong what follows is, with <paramref name="rollAny"/> and <paramref name="rollTier"/> in [0, 1): nothing at all
        /// sometimes (an omen mostly), small most of the time, notable now and then once the waits allow it, great rarely and
        /// only once their long waits are past; a tier whose wait isn't past falls to the one below. Days are since the last of
        /// each at the home (large for never).
        /// </summary>
        public static TouchTier Tier(TouchTone t, bool big, bool plain, float rollAny, float rollTier, float godSmallDays, float godNotableDays,
            float colonyNotableDays, float godGreatDays, float colonyGreatDays) =>
            TierFor(t, big, plain, rollAny, rollTier, godSmallDays, godNotableDays, colonyNotableDays, godGreatDays, colonyGreatDays, 1f);

        /// <summary>
        /// The same, leaned by a gift's worth and fit (<paramref name="lean"/>, OfferingGiftMath.Lean; 1 is <see cref="Tier"/>
        /// itself): a richer or better-loved gift is the likelier followed, and the likelier by something notable or great,
        /// the god's own waits for those shorter by the lean (the colony's waits stay); a token gift the less often.
        /// </summary>
        public static TouchTier TierFor(TouchTone t, bool big, bool plain, float rollAny, float rollTier, float godSmallDays, float godNotableDays,
            float colonyNotableDays, float godGreatDays, float colonyGreatDays, float lean)
        {
            float l = float.IsNaN(lean) ? 1f : Math.Max(0.5f, Math.Min(3f, lean));
            if (t == TouchTone.None || rollAny >= Math.Min(0.97f, AnyChance(t) * (float)Math.Sqrt(l))) return TouchTier.None;
            float great = Math.Min(0.4f, GreatChance(t, big, plain) * l * l), notable = Math.Min(0.75f, NotableChance(t, big, plain) * l);
            bool greatOk = godGreatDays >= GreatGodDays / Math.Max(1f, l) && colonyGreatDays >= GreatColonyDays;
            bool notableOk = NotableWaitPast(godNotableDays, colonyNotableDays, l);
            bool smallOk = godSmallDays >= SmallGodDays;
            if (rollTier < great && greatOk) return TouchTier.Great;
            if (rollTier < great + notable && notableOk) return TouchTier.Notable;
            return smallOk ? TouchTier.Small : TouchTier.None;
        }

        /// <summary>A notable effect's waits are past: the god's own (shorter by a gift's lean) and any god's.</summary>
        public static bool NotableWaitPast(float godNotableDays, float colonyNotableDays, float lean = 1f) =>
            godNotableDays >= NotableGodDays / Math.Max(1f, float.IsNaN(lean) ? 1f : lean) && colonyNotableDays >= NotableColonyDays;

        // ================= Repetition =================

        /// <summary>
        /// The same occurrence again within this many days at a home leaves the moods only: an offering taken four days (the
        /// colony offers often), anything else ten.
        /// </summary>
        public static float SameKeyGapDays(string key) => key != null && key.StartsWith("taken", StringComparison.Ordinal) ? 4f : 10f;

        /// <summary>
        /// The same, for an offering taken with what it was worth (<paramref name="lean"/>, OfferingGiftMath.Lean): the four
        /// days shorter the richer the gift (a day and a half at the least) and longer for a token one (six at the most); a
        /// precious gift asked of the colony first is always followed. Anything else as <see cref="SameKeyGapDays"/>.
        /// </summary>
        public static float GapDays(string key, float lean, bool precious)
        {
            if (key == null || !key.StartsWith("taken", StringComparison.Ordinal)) return SameKeyGapDays(key);
            if (precious) return 0f;
            float l = float.IsNaN(lean) || lean <= 0f ? 1f : lean;
            return Math.Max(1.5f, Math.Min(6f, SameKeyGapDays(key) / l));
        }

        /// <summary>What counts as one occurrence for its gap: an offering taken by any way is "taken|god", else its words key.</summary>
        public static string GapKey(string key, string god) => string.IsNullOrEmpty(key) ? null : key.StartsWith("taken", StringComparison.Ordinal) ? "taken|" + god : key;

        /// <summary>How many days back a god's occurrences at a home count toward giving the others their turn.</summary>
        public const float TurnDays = 10f;

        /// <summary>
        /// A god that came often at a home lately gives the others their turn: its own happenings that much less likely, by how
        /// many of its occurrences were seen there in the last <see cref="TurnDays"/> days (none: as before; never below a third).
        /// </summary>
        public static float Turn(int recentOfGod) => recentOfGod <= 0 ? 1f : Math.Max(0.35f, 1f / (1f + 0.3f * recentOfGod));

        /// <summary>A god takes an offering before everyone's eyes (its moment, the camera on it) a month apart at a home at most, and any god a week and a day.</summary>
        public const float PlainTakingGodDays = 30f, PlainTakingAnyDays = 8f;

        /// <summary>An offering may be taken before everyone's eyes now; else it goes as the gods are felt, quietly.</summary>
        public static bool PlainTaking(float daysSinceGod, float daysSinceAny) => daysSinceGod >= PlainTakingGodDays && daysSinceAny >= PlainTakingAnyDays;

        // ================= Each god's own =================

        private static TouchEntry S(TouchEffect e) => new TouchEntry(e, TouchTier.Small);
        private static TouchEntry N(TouchEffect e) => new TouchEntry(e, TouchTier.Notable);
        private static TouchEntry G(TouchEffect e) => new TouchEntry(e, TouchTier.Great);
        private static TouchEntry D(TouchEffect e, TouchTier t) => new TouchEntry(e, t, true);

        /// <summary>
        /// Each god's own effects, by its key (Tamiu's is Mog), as different as the gods are: Hollin's green, Smoulder's
        /// fires and ore, Bellwether's beasts, Morel's mushrooms and soil, Ennu's travellers, Pip's machines, Cirrus's sky,
        /// Hush's cold, the Hunger's taking, Helios's sun, Selene's sleep and healing, Tamiu's cats, Murmur's water, Revel's merry hours, Ducat's
        /// coin. A god with none here (a new one) has the shared small blessing only.
        /// </summary>
        public static readonly Dictionary<string, TouchEntry[]> Table = new Dictionary<string, TouchEntry[]>
        {
            ["Hollin"] = new[] { S(TouchEffect.Bless), S(TouchEffect.Bloom), S(TouchEffect.Learn), N(TouchEffect.Ripen), N(TouchEffect.Healroot), N(TouchEffect.Inspire), G(TouchEffect.Linger), D(TouchEffect.Brambles, TouchTier.Small) },
            ["Smoulder"] = new[] { S(TouchEffect.Bless), S(TouchEffect.Refuel), S(TouchEffect.Learn), N(TouchEffect.Find), N(TouchEffect.Inspire), G(TouchEffect.Vein), G(TouchEffect.Linger), D(TouchEffect.FiresOut, TouchTier.Small) },
            ["Bellwether"] = new[] { S(TouchEffect.Bless), S(TouchEffect.Learn), N(TouchEffect.Game), N(TouchEffect.Inspire), G(TouchEffect.Herd), G(TouchEffect.Linger), D(TouchEffect.GameLeaves, TouchTier.Small) },
            ["Morel"] = new[] { S(TouchEffect.Bless), S(TouchEffect.Learn), N(TouchEffect.Find), N(TouchEffect.RichSoil), G(TouchEffect.Fertile), D(TouchEffect.Spoil, TouchTier.Notable) },
            ["Ennu"] = new[] { S(TouchEffect.Bless), S(TouchEffect.Learn), N(TouchEffect.Visitors), N(TouchEffect.Goodwill), N(TouchEffect.Trader), N(TouchEffect.Inspire), G(TouchEffect.Wanderer), G(TouchEffect.Linger) },
            ["Pip"] = new[] { S(TouchEffect.Bless), S(TouchEffect.Mend), S(TouchEffect.Charge), N(TouchEffect.Find), N(TouchEffect.Inspire), G(TouchEffect.Linger), D(TouchEffect.Breakdown, TouchTier.Notable) },
            ["Cirrus"] = new[] { S(TouchEffect.Bless), S(TouchEffect.ClearSky), N(TouchEffect.Aurora), N(TouchEffect.SkyGift), N(TouchEffect.Inspire), G(TouchEffect.Linger), D(TouchEffect.Fog, TouchTier.Small) },
            ["Hush"] = new[] { S(TouchEffect.Bless), S(TouchEffect.Snow), N(TouchEffect.Preserve), N(TouchEffect.ColdBreak), G(TouchEffect.Hardy), D(TouchEffect.FiresOut, TouchTier.Small) },
            ["Undertow"] = new[] { S(TouchEffect.Bless), N(TouchEffect.Inspire), D(TouchEffect.FiresOut, TouchTier.Small), D(TouchEffect.Vanish, TouchTier.Notable), D(TouchEffect.Dim, TouchTier.Great) },
            ["Helios"] = new[] { S(TouchEffect.Bless), S(TouchEffect.ClearSky), N(TouchEffect.Ripen), N(TouchEffect.Inspire), G(TouchEffect.SunYear) },
            ["Selene"] = new[] { S(TouchEffect.Bless), N(TouchEffect.Heal), N(TouchEffect.Inspire), G(TouchEffect.ScarGone), G(TouchEffect.Linger), D(TouchEffect.Sleepless, TouchTier.Small) },
            ["Mog"] = new[] { S(TouchEffect.Bless), S(TouchEffect.RatsGone), N(TouchEffect.CatComes), N(TouchEffect.Inspire), G(TouchEffect.Ward) },
            ["Murmur"] = new[] { S(TouchEffect.Bless), S(TouchEffect.Rain), N(TouchEffect.Ripen), N(TouchEffect.Fish), G(TouchEffect.Spring), G(TouchEffect.Linger), D(TouchEffect.Fog, TouchTier.Small) },
            ["Ducat"] = new[] { S(TouchEffect.Bless), N(TouchEffect.Find), N(TouchEffect.Trader), N(TouchEffect.Inspire), G(TouchEffect.Hoard), G(TouchEffect.Linger), D(TouchEffect.Vanish, TouchTier.Notable) },
            ["Revel"] = new[] { S(TouchEffect.Bless), S(TouchEffect.Merry), N(TouchEffect.Find), N(TouchEffect.Inspire), G(TouchEffect.Linger), D(TouchEffect.Sour, TouchTier.Notable) }
        };

        private static readonly TouchEntry[] Shared = { S(TouchEffect.Bless) };

        /// <summary>A god's own effects by its key; the shared small blessing for a god with none.</summary>
        public static IReadOnlyList<TouchEntry> Of(string god) => god != null && Table.TryGetValue(god, out var list) ? list : Shared;

        /// <summary>
        /// The god's effect for this tier and side (kind, or its anger), leaned away from what it did lately at the home
        /// (<paramref name="recent"/>, newest first: GodPaceMath.Lean), among those that can be done now (<paramref name="can"/>);
        /// with none of this tier, one of the tier below. None when nothing can.
        /// </summary>
        public static TouchEntry Pick(IReadOnlyList<TouchEntry> entries, TouchTier tier, bool dark, IList<string> recent, Func<TouchEntry, bool> can, float roll)
        {
            if (entries == null || tier == TouchTier.None) return default;
            for (var t = tier; t >= TouchTier.Small; t--)
            {
                var fit = entries.Where(e => e.tier == t && e.dark == dark && (can == null || can(e))).ToList();
                if (fit.Count == 0) continue;
                int i = GodPaceMath.Pick(fit.Select(e => e.effect.ToString()).ToList(), fit.Select(_ => 1f).ToList(), recent, roll);
                if (i >= 0) return fit[i];
            }
            return default;
        }

        // ================= Sizes =================

        /// <summary>How long a god's own presence stays on the land as a great effect, in days, by its key (a few days for the Hunger's dark).</summary>
        public static float LingerDays(string god)
        {
            switch (god)
            {
                case "Hollin": return 10f;
                case "Bellwether": return 10f;
                case "Smoulder": return 7f;
                case "Ennu": return 7f;
                case "Cirrus": return 7f;
                case "Ducat": return 7f;
                case "Revel": return 5f;
                case "Pip": return 5f;
                case "Selene": return 5f;
                case "Murmur": return 4f;
                case "Undertow": return 2f;
                default: return 5f;
            }
        }

        /// <summary>
        /// The gods whose own presence may stay on the land for days: not the long noon, the white silence or the rot (days of
        /// those would harm the colony), and not Tamiu's watch, which is hers only while she walks the land.
        /// </summary>
        public static readonly string[] Lingers = { "Hollin", "Smoulder", "Bellwether", "Ennu", "Pip", "Cirrus", "Selene", "Murmur", "Ducat", "Revel", "Undertow" };

        /// <summary>How much a small and a notable ripening moves the crops near the place on (a share of their whole growth), and the sun's year every crop.</summary>
        public const float RipenNear = 0.2f, SunYearGrowth = 0.3f;

        /// <summary>Cells round the place the near effects reach.</summary>
        public const float NearRadius = 12f;

        /// <summary>Those who saw it that a blessing reaches, at most.</summary>
        public const int BlessAtMost = 6;

        /// <summary>A share of a stack that goes when something vanishes: a tenth, never all and at least one.</summary>
        public static int VanishCount(int stack) => stack <= 1 ? 0 : Math.Max(1, Math.Min(stack - 1, (int)Math.Round(stack * 0.1f)));

        /// <summary>How many recent effects a home keeps for leaning away from them.</summary>
        public const int RecentKept = 40;

        /// <summary>The days a season lasts in this game (for the season-long effects).</summary>
        public const float SeasonDays = 15f;
    }
}
