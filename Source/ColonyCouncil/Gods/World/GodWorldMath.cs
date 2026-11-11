using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>What a god gives one colonist for good (Gods/World/GodGifts.cs), in the shape of the peoples' gifts.</summary>
    public enum GodGiftKind
    {
        None,
        /// <summary>A blessing of the god's domain on their body (a blessed hand, storm-sense, a healer's touch): a hediff of its own.</summary>
        Hand,
        /// <summary>A beast of the god's: one of its sacred animals, or a wolf, tamed and bonded to them.</summary>
        Beast,
        /// <summary>A RimWorld of Magic class fitting the domain (the Healer's priest, the Storm-father's storm born).</summary>
        Class,
        /// <summary>A single spell of the domain for one who already casts (A RimWorld of Magic's spell scrolls).</summary>
        Spell,
        /// <summary>Mana or a fighter's strength: one of A RimWorld of Magic's gifted traits (mana well, fae blood, giants' blood).</summary>
        Mana,
        /// <summary>A psylink and a Vanilla Psycasts Expanded path fitting the domain.</summary>
        Path,
        /// <summary>The god's relic: the colony faith's lost relic (Ideology), or a masterwork in the god's name.</summary>
        Relic
    }

    /// <summary>What a gift costs (every gift has a price).</summary>
    public enum GodPrice
    {
        /// <summary>Silver or goods carried out and left for the god.</summary>
        Offering,
        /// <summary>The god's mark on them for good (a brand that unsettles people a little).</summary>
        Mark,
        /// <summary>Years of their life.</summary>
        Years,
        /// <summary>Their blood: a cut taken there and then, under the harm budget.</summary>
        Blood,
        /// <summary>The god's rivals' anger, which falls on the colony.</summary>
        Rival
    }

    /// <summary>The shape a god's anger takes on the map (Gods/World/GodHealth.cs).</summary>
    public enum GodWrath
    {
        None,
        /// <summary>The game's own lightning, on the roof over the one who offended (in a storm).</summary>
        Lightning,
        /// <summary>The game's own blight on the fields.</summary>
        Blight,
        /// <summary>Its beasts sent after the one who offended (wolves for the one who killed the sacred stag).</summary>
        Beasts,
        /// <summary>The sun put out an afternoon (the game's own eclipse).</summary>
        Eclipse
    }

    /// <summary>
    /// The pure rules of the gods in the wider world (docs/design/gods.md, stage 2's "The world": brainstorm items 17, 18, 19,
    /// 21 and 22, and the drama between the gods), with no game types so Tools/Tests checks them (GodWorldTests): how often
    /// each thing comes, by how real the gods are; what a god gives and what it asks in return, by its domain, nature and
    /// kind; the shapes of its anger and its kindness and how likely each is by the colony's standing; the quarrels of rival
    /// gods in the weather; the Hunger's bargains and what it takes next; an archotech's exact terms; the Sleeper's door;
    /// the newborn's signs; the myths as the ties' history grows; and what a neighbour's letter tells of its god.
    /// </summary>
    public static class GodWorldMath
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        // ================= How often =================

        /// <summary>How much more or less often the gods act in the world: half as often when only believed, more when they walk.</summary>
        public static float Realness(GodRealness r) => r == GodRealness.Believed ? 0.5f : r == GodRealness.Walking ? 1.4f : 1f;

        /// <summary>What only a real god can do (gifts of power, the void's bargains, the Sleeper's door, the rival's avatar): Felt and Walking, never Believed.</summary>
        public static bool Supernatural(GodRealness r) => r != GodRealness.Believed;

        /// <summary>The chance on one day of something that comes every <paramref name="meanDays"/> on average, times <paramref name="factor"/>.</summary>
        public static float DayChance(float meanDays, float factor = 1f)
        {
            if (float.IsNaN(meanDays) || meanDays <= 0f || float.IsNaN(factor) || factor <= 0f) return 0f;
            return 1f - (float)Math.Exp(-factor / meanDays);
        }

        /// <summary>At least <paramref name="days"/> since <paramref name="last"/> (-1: never).</summary>
        public static bool Gap(int now, int last, float days) => last < 0 || now - last >= days * DayTicks;

        /// <summary>A neighbour's god's festival: an invitation every this many days on average among the colony's friends, never two within the gap.</summary>
        public const float InviteDays = 75f, InviteGap = 40f;

        /// <summary>A temple's envoy asking for a relic the colony holds that is sacred to their god.</summary>
        public const float EnvoyDays = 150f, EnvoyGap = 90f;

        /// <summary>A friend at war with a people whose god is their god's rival asks the colony to stand with them: a holy war.</summary>
        public const float HolyWarDays = 180f, HolyWarGap = 90f;

        /// <summary>Peoples who share a god get on better: looked at once a season (a quadrum).</summary>
        public const int SharedEveryDays = 15;

        /// <summary>Pilgrims to a place on the colony's land that one people calls holy.</summary>
        public const float PilgrimDays = 100f, PilgrimGap = 45f;

        /// <summary>A hermit seer camps at the map's edge.</summary>
        public const float SeerDays = 200f, SeerGap = 120f;

        /// <summary>A raid by a people with a war god: one in this many is led by its champion, and never two within the gap.</summary>
        public const float ChampionShare = 0.15f, ChampionGap = 20f;

        /// <summary>A god that favours the colony offers one of its people a gift: every this many days while favoured, twice as often beloved.</summary>
        public const float GiftDays = 110f, GiftGap = 45f;

        /// <summary>Two rival gods' quarrel in the weather, the god's ask (slight a rival, hide the hunted), the rival who undoes a blessing.</summary>
        public const float QuarrelDays = 240f, QuarrelGap = 120f, AskDays = 160f, AskGap = 80f;

        /// <summary>The ones who don't fit: an archotech's bargain, the dead god's mark found, the fading god, the Sleeper's dream.</summary>
        public const float BargainDays = 200f, BargainGap = 120f, DeadMarkDays = 120f, FadingDays = 240f, SleeperDays = 160f, SleeperGap = 60f;

        /// <summary>The scale on a chance for how real the gods are, and whether it can come at all.</summary>
        public static float Factor(GodRealness r, bool supernatural) => supernatural && !Supernatural(r) ? 0f : Realness(r);

        // ================= Letters and diplomacy =================

        /// <summary>
        /// What a neighbour's letter tells of their god, by what is true at their town (their facts): a sign of its favour when
        /// the town is thriving or flourishing ("miracle"), of its anger when it is struggling, ill or mourning ("curse"), or
        /// nothing; a myth now and then when their god has ties ("myth"); a holy war when their war is their gods' quarrel
        /// ("holywar"). The roll keeps it to some letters, not all.
        /// </summary>
        public static string LetterTells(bool good, bool bad, bool holyWar, bool hasTies, float roll)
        {
            if (float.IsNaN(roll)) return null;
            if (holyWar && roll < 0.6f) return "holywar";
            if (bad && roll < 0.45f) return "curse";
            if (good && roll < 0.45f) return "miracle";
            if (hasTies && roll < 0.25f) return "myth";
            return null;
        }

        /// <summary>
        /// The colony's letter slights the god of those it writes to: written by someone of a faith whose god is that god's
        /// rival, or by a scoffer (a faith with no gods, or one of little certainty), now and then; never in condolences.
        /// </summary>
        public static bool Slights(bool rivalFaith, bool scoffer, bool condolences, float roll) =>
            !condolences && !float.IsNaN(roll) && (rivalFaith && roll < 0.35f || scoffer && roll < 0.2f);

        /// <summary>A slighted people's goodwill lost, and how far their god is displeased (before its nature).</summary>
        public const int SlightGoodwill = -6;
        public const float SlightFavour = -6f;

        /// <summary>
        /// Goodwill a season with a people who shares one of the colony's gods, by the colony's standing with that god: none
        /// below noticed, a little when noticed, more pleased and up. Against a people whose god is the rival of the colony's
        /// patron: a little less, only while that patron favours the colony (a feud that is really the gods').
        /// </summary>
        public static int SharedGoodwill(FavourWord colonyWithGod) =>
            colonyWithGod >= FavourWord.Pleased ? 2 : colonyWithGod == FavourWord.Noticed ? 1 : 0;

        public static int FeudGoodwill(FavourWord colonyWithPatron) => colonyWithPatron >= FavourWord.Favoured ? -1 : 0;

        /// <summary>The feud never takes a people below this goodwill (only the colony's own choices start a war).</summary>
        public const int FeudFloor = 0;

        /// <summary>A holy war's answers: standing with them, sending help, staying out.</summary>
        public const int HolyWarStand = 8, HolyWarEnemy = -12, HolyWarHelp = 4, HolyWarOut = -3;
        public const float HolyWarFavour = 6f, HolyWarHelpFavour = 3f;

        /// <summary>The silver sent with help to a holy war, and to a festival as the colony's gift: by its wealth (a factor near 1), rounded to ten.</summary>
        public static int Gift(float wealthFactor, int baseSilver) => Math.Max(50, (int)Math.Round(baseSilver * Clamp(wealthFactor, 0.5f, 2.5f) / 10f) * 10);

        public const int FestivalSilver = 120, HolyWarSilver = 200;
        public const int FestivalGoodwill = 5, FestivalRegrets = -1;
        public const float FestivalFavour = 4f;

        /// <summary>A relic handed over to a temple's envoy, lent for their festival, or refused.</summary>
        public const int RelicGiven = 15, RelicLent = 6, RelicRefused = -8;
        public const float RelicGivenFavour = 10f, RelicRefusedFavour = -6f;

        /// <summary>A relic lent comes back after this many days.</summary>
        public const int RelicLentDays = 8;

        // ================= Peoples and camps =================

        /// <summary>The kinds of holy place there are.</summary>
        public static readonly string[] SiteKinds = { "tree", "water", "wreck", "grave", "stone" };

        /// <summary>What kind of holy place a domain's people would call one on the colony's land: "tree" (an old tree), "water" (a spring or a river's bank), "wreck" (a crash site, an ancient machine), "grave" (the colony's graves), "stone" (an old ruin), or null.</summary>
        public static string SiteKind(GodDomain d) => d switch
        {
            GodDomain.Wild => "tree",
            GodDomain.Harvest => "tree",
            GodDomain.Hunt => "tree",
            GodDomain.Waters => "water",
            GodDomain.Healing => "water",
            GodDomain.Machine => "wreck",
            GodDomain.Storm => "wreck",
            GodDomain.Void => "wreck",
            GodDomain.Dead => "grave",
            GodDomain.Night => "stone",
            GodDomain.Craft => "stone",
            GodDomain.Way => "stone",
            _ => null
        };

        /// <summary>The pilgrims' standing earned: their god pleased by a stay let be, more by one the colony joined.</summary>
        public const float PilgrimFavour = 3f, PilgrimJoinedFavour = 4f;

        /// <summary>A champion's fall is a sign: their god's rivals (and the colony's own war gods) pleased, the champion's god displeased, or impressed if proud.</summary>
        public const float ChampionFell = 6f, ChampionGodLoss = -5f, ChampionProud = 3f;

        /// <summary>How the champion's god takes the fall: a proud or wrathful war god is impressed by a colony that beat its best, others are displeased.</summary>
        public static float ChampionGodTakes(GodNature n) => n == GodNature.Proud || n == GodNature.Wrathful ? ChampionProud : ChampionGodLoss;

        // ================= Health and wrath =================

        /// <summary>
        /// A night's chance that a fever breaks or a plague eases for a colonist who is ill, by the colony's standing with
        /// its healing god (or their own god of healing): none below noticed. Once an illness.
        /// </summary>
        public static float BlessingChance(FavourWord w, GodKind kind)
        {
            float c = w switch
            {
                FavourWord.Beloved => 0.15f,
                FavourWord.Favoured => 0.1f,
                FavourWord.Pleased => 0.06f,
                FavourWord.Noticed => 0.03f,
                _ => 0f
            };
            // The dead hardly answer; a newborn's is small.
            if (kind == GodKind.Dead) return c * 0.2f;
            if (kind == GodKind.Newborn) return c * 0.6f;
            return c;
        }

        /// <summary>How far an illness eases at the blessing (its severity down by this), never below a sliver.</summary>
        public const float BlessingEase = 0.15f, BlessingFloor = 0.02f;

        /// <summary>A blessing comes in the small hours (0 to 4).</summary>
        public static bool BlessingHour(int hour) => hour >= 0 && hour <= 4;

        /// <summary>
        /// The shape a god's anger takes by its domain, and its nature where it matters: lightning for the storm's, blight
        /// for the harvest's and the waters', beasts for the wild's, the hunt's and war's, the sun put out for the night's,
        /// the dead's and the void's. A patient or cold god never strikes; it waits.
        /// </summary>
        public static GodWrath WrathOf(GodDomain d, GodNature n)
        {
            if (n == GodNature.Patient || n == GodNature.Cold) return GodWrath.None;
            switch (d)
            {
                case GodDomain.Storm: return GodWrath.Lightning;
                case GodDomain.Harvest:
                case GodDomain.Waters: return GodWrath.Blight;
                case GodDomain.Wild:
                case GodDomain.Hunt:
                case GodDomain.War: return GodWrath.Beasts;
                case GodDomain.Night:
                case GodDomain.Dead:
                case GodDomain.Void: return GodWrath.Eclipse;
                default: return GodWrath.None;
            }
        }

        /// <summary>
        /// The mean days between a god's acts of anger at a home while the colony stands displeased or worse with it: a
        /// wrathful god soonest, a grieving or generous one slowest; wrathful standing twice as often as displeased. 0: never.
        /// </summary>
        public static float WrathDays(FavourWord w, GodNature n, GodKind k)
        {
            if (w > FavourWord.Displeased || k == GodKind.Dead || k == GodKind.Archotech) return 0f;
            float days = n switch
            {
                GodNature.Wrathful => 30f,
                GodNature.Proud => 40f,
                GodNature.Jealous => 45f,
                GodNature.Capricious => 50f,
                GodNature.Playful => 70f,
                GodNature.Grieving => 90f,
                GodNature.Generous => 100f,
                _ => 0f
            };
            if (days <= 0f) return 0f;
            return w == FavourWord.Wrathful ? days * 0.5f : days;
        }

        /// <summary>A grudge the god holds against one colonist (a sacred beast killed, its name mocked) lasts this many days.</summary>
        public const int GrudgeDays = 12;

        /// <summary>The beasts come for the one who offended this many days after at the soonest, and each day after by this chance (before the budget).</summary>
        public const int BeastsAfterDays = 2;
        public const float BeastsDayChance = 0.35f;

        /// <summary>How many of the beasts come: two, three for a wrathful god.</summary>
        public static int BeastCount(GodNature n) => n == GodNature.Wrathful ? 3 : 2;

        /// <summary>In a thunderstorm, an hour's chance the god's lightning finds the offender's roof (before the budget).</summary>
        public const float LightningHourChance = 0.12f;

        /// <summary>The lightning lands this far at least from anyone, so it strikes the roof and not a person.</summary>
        public const float LightningClear = 3.5f;

        // ================= Gifts =================

        /// <summary>
        /// The RimWorld of Magic class a god of these domains grants, by the class trait's own name (first the likeliest);
        /// each one Rimfolk's peoples' table knows with its switch in the mod's settings (PeoplesGearMath.MagicClasses).
        /// </summary>
        public static string[] ClassesFor(GodDomain d, GodDomain second)
        {
            switch (d)
            {
                case GodDomain.Healing: return new[] { "Priest", "Paladin" };
                case GodDomain.Wild: return new[] { "Druid" };
                case GodDomain.Harvest: return new[] { "Druid" };
                case GodDomain.Hunt: return new[] { "Ranger" };
                // The Deep Mother (craft and the dead) makes geomancers; the Smith enchanters.
                case GodDomain.Craft: return second == GodDomain.Dead ? new[] { "Geomancer" } : new[] { "Enchanter" };
                case GodDomain.Dead: return new[] { "Necromancer", "DeathKnight" };
                case GodDomain.Void: return new[] { "Necromancer", "DeathKnight" };
                case GodDomain.Storm: return new[] { "StormBorn" };
                case GodDomain.Machine: return new[] { "Technomancer" };
                case GodDomain.Night: return new[] { "TM_Bard" };
                case GodDomain.War: return new[] { "Gladiator", "TM_Monk" };
                case GodDomain.Hearth: return new[] { "InnerFire" };
                case GodDomain.Waters: return new[] { "HeartOfFrost" };
                case GodDomain.Way: return new[] { "TM_Wayfarer" };
                default: return new string[0];
            }
        }

        /// <summary>
        /// A single spell of the domain for one who already casts: A RimWorld of Magic's spell scrolls that any mage may
        /// learn (CompUseEffect_LearnSpell), by the item's defName.
        /// </summary>
        public static string[] SpellsFor(GodDomain d) => d switch
        {
            GodDomain.Harvest => new[] { "SpellOf_Rain", "SpellOf_Sunlight" },
            GodDomain.Storm => new[] { "SpellOf_LightningTrap", "SpellOf_Rain" },
            GodDomain.Hunt => new[] { "SpellOf_Blur", "SpellOf_Invisibility" },
            GodDomain.Healing => new[] { "SpellOf_Heal", "SpellOf_SpellMending" },
            GodDomain.War => new[] { "SpellOf_ArcaneBolt", "SpellOf_ManaShield" },
            GodDomain.Craft => new[] { "SpellOf_PowerNode", "SpellOf_Heater" },
            GodDomain.Way => new[] { "SpellOf_Blink", "SpellOf_Teleport" },
            GodDomain.Dead => new[] { "SpellOf_SummonMinion" },
            GodDomain.Night => new[] { "SpellOf_MageLight", "SpellOf_Invisibility" },
            GodDomain.Hearth => new[] { "SpellOf_Heater", "SpellOf_Ignite", "SpellOf_Extinguish" },
            GodDomain.Wild => new[] { "SpellOf_Rain", "SpellOf_Sunlight" },
            GodDomain.Waters => new[] { "SpellOf_Rain", "SpellOf_SnapFreeze", "SpellOf_Extinguish" },
            GodDomain.Machine => new[] { "SpellOf_EMP", "SpellOf_PowerNode" },
            GodDomain.Void => new[] { "SpellOf_SiphonMana", "SpellOf_SmokeCloud" },
            _ => new string[0]
        };

        /// <summary>A RimWorld of Magic gifted trait for one who casts (mana) or fights (stamina), each with its own cost built in.</summary>
        public static string[] ManaTraits(bool fights) => fights ? new[] { "TM_GiantsBloodTD", "TM_BoundlessTD" } : new[] { "TM_ManaWellTD", "TM_ArcaneConduitTD", "TM_FaeBloodTD", "TM_EnlightenedTD" };

        /// <summary>Vanilla Psycasts Expanded's paths fitting a domain, as the mods name them (the player's: the main mod, the Hemosage, the Puppeteer).</summary>
        public static string[] PathsFor(GodDomain d) => d switch
        {
            GodDomain.Harvest => new[] { "VPE_Wildspeaker", "VPE_Harmonist" },
            GodDomain.Storm => new[] { "VPE_Staticlord", "VPE_Frostshaper" },
            GodDomain.Hunt => new[] { "VPE_Wildspeaker", "VPE_Nightstalker" },
            GodDomain.Healing => new[] { "VPE_Empath", "VPE_Protector" },
            GodDomain.War => new[] { "VPE_Warlord", "VPE_Conflagrator" },
            GodDomain.Craft => new[] { "VPE_Technomancer", "VPE_Conflagrator" },
            GodDomain.Way => new[] { "VPE_Skipmaster", "VPE_Chronopath" },
            GodDomain.Dead => new[] { "VPE_Necropath", "VPEH_Hemosage" },
            GodDomain.Night => new[] { "VPE_Nightstalker", "VPE_Chronopath" },
            GodDomain.Hearth => new[] { "VPE_Protector", "VPE_Harmonist" },
            GodDomain.Wild => new[] { "VPE_Wildspeaker" },
            GodDomain.Waters => new[] { "VPE_Frostshaper", "VPE_Harmonist" },
            GodDomain.Machine => new[] { "VPE_Technomancer", "VPE_Archotechist" },
            GodDomain.Void => new[] { "VPEP_Puppeteer", "VPE_Necropath" },
            _ => new string[0]
        };

        /// <summary>The god's blessing on someone's body: its hediff (Defs/GodDefs/Rimfolk_GodsWorld_Gifts.xml), one a domain.</summary>
        public static string HandOf(GodDomain d) => d == GodDomain.None ? null : "Rimfolk_GodGift_" + d;

        /// <summary>The beasts a god of the domain may give (tamed and bonded), when its own sacred animals can't be: a wolf for the hunt and war, a dog for the hearth.</summary>
        public static string[] BeastsFor(GodDomain d) => d switch
        {
            GodDomain.Hunt => new[] { "Wolf_Timber", "Wolf_Arctic" },
            GodDomain.War => new[] { "Wolf_Timber", "Warg" },
            GodDomain.Wild => new[] { "Wolf_Timber", "Bear_Grizzly" },
            GodDomain.Hearth => new[] { "Husky", "LabradorRetriever" },
            GodDomain.Night => new[] { "Cat" },
            GodDomain.Way => new[] { "Horse", "Dromedary" },
            _ => new string[0]
        };

        /// <summary>The beasts a god's anger sends (predators of the land, the game's own): wolves first, a warg for war.</summary>
        public static string[] WrathBeasts(GodDomain d) => d == GodDomain.War ? new[] { "Warg", "Wolf_Timber" } : new[] { "Wolf_Timber", "Wolf_Arctic" };

        /// <summary>
        /// Which gift, by what can be given now (each a weight, 0 when it can't): a blessing on the body always can; a beast
        /// when the domain has one; a class to one with none (A RimWorld of Magic); a spell or a gifted trait to one who
        /// casts; a path with Royalty and Vanilla Psycasts Expanded to one with no psylink yet; the relic when there is one.
        /// The rarest gifts weigh least.
        /// </summary>
        public static GodGiftKind PickGift(bool hand, bool beast, bool cls, bool spell, bool mana, bool path, bool relic, float roll)
        {
            var w = new List<(GodGiftKind k, float w)>();
            if (hand) w.Add((GodGiftKind.Hand, 3f));
            if (beast) w.Add((GodGiftKind.Beast, 2f));
            if (cls) w.Add((GodGiftKind.Class, 1.5f));
            if (spell) w.Add((GodGiftKind.Spell, 2f));
            if (mana) w.Add((GodGiftKind.Mana, 1.5f));
            if (path) w.Add((GodGiftKind.Path, 1.5f));
            if (relic) w.Add((GodGiftKind.Relic, 1f));
            if (w.Count == 0) return GodGiftKind.None;
            return GodsMath.Weighted(w, x => x.w, float.IsNaN(roll) ? 0f : Clamp(roll, 0f, 0.9999f)).k;
        }

        /// <summary>
        /// The price a god asks, by its nature and kind: the Hunger takes years, the dead's and the grieving years or a mark,
        /// war and the wrathful blood, the jealous make the colony's other gods angry, the generous and the patient ask an
        /// offering, the proud a mark; an archotech asks only goods (an exact term).
        /// </summary>
        public static GodPrice PriceOf(GodNature n, GodKind k, GodDomain d, float roll)
        {
            if (k == GodKind.Hunger) return GodPrice.Years;
            if (k == GodKind.Archotech) return GodPrice.Offering;
            if (d == GodDomain.War && roll < 0.6f) return GodPrice.Blood;
            switch (n)
            {
                case GodNature.Grieving: return roll < 0.5f ? GodPrice.Years : GodPrice.Mark;
                case GodNature.Wrathful: return roll < 0.6f ? GodPrice.Blood : GodPrice.Mark;
                case GodNature.Jealous: return roll < 0.7f ? GodPrice.Rival : GodPrice.Offering;
                case GodNature.Proud: return roll < 0.6f ? GodPrice.Mark : GodPrice.Offering;
                case GodNature.Capricious: return roll < 0.33f ? GodPrice.Blood : roll < 0.66f ? GodPrice.Mark : GodPrice.Offering;
                case GodNature.Playful: return roll < 0.5f ? GodPrice.Mark : GodPrice.Offering;
                case GodNature.Cold: return GodPrice.Offering;
                default: return roll < 0.8f ? GodPrice.Offering : GodPrice.Mark;
            }
        }

        /// <summary>The years a gift costs: three to six, more for the Hunger.</summary>
        public static int PriceYears(GodKind k, float roll) => (k == GodKind.Hunger ? 5 : 3) + (int)(Clamp(roll, 0f, 0.999f) * 4f);

        /// <summary>The silver an offering costs, by the gift (a class and a relic the dearest) and the colony's wealth, rounded to ten.</summary>
        public static int PriceSilver(GodGiftKind g, float wealthFactor)
        {
            float b = g switch
            {
                GodGiftKind.Class => 600f,
                GodGiftKind.Relic => 500f,
                GodGiftKind.Path => 450f,
                GodGiftKind.Spell => 300f,
                GodGiftKind.Mana => 300f,
                GodGiftKind.Beast => 200f,
                _ => 250f
            };
            return Math.Max(100, (int)Math.Round(b * Clamp(wealthFactor, 0.5f, 2.5f) / 10f) * 10);
        }

        /// <summary>The rivals' anger a jealous god's gift costs (before their natures), on each rival the colony knows.</summary>
        public const float RivalPrice = -10f;

        /// <summary>Taking the gift pleases the god; turning it down displeases a proud or jealous one a little and others not at all.</summary>
        public const float GiftTaken = 5f;

        public static float GiftRefused(GodNature n) => n == GodNature.Proud || n == GodNature.Jealous || n == GodNature.Wrathful ? -4f : 0f;

        /// <summary>The colonist stands out under the sky this long to receive the gift (ticks), and the gift is given at most this long after they were sent.</summary>
        public const int GiftKeepTicks = 2 * HourTicks, GiftWaitTicks = 6 * HourTicks;

        // ================= The trickster and the rival =================

        /// <summary>
        /// A trickster's gift turns (a blessing to a curse, or back): not before <see cref="TricksterFirstDays"/>, then each day
        /// by this chance; it turns back a little more readily than it first turned. At most <see cref="TricksterTurns"/> turns.
        /// </summary>
        public static bool TricksterTurnsToday(int daysSince, bool cursed, int turns, float roll)
        {
            if (turns >= TricksterTurns || daysSince < TricksterFirstDays || float.IsNaN(roll)) return false;
            return roll < (cursed ? 0.15f : 0.08f);
        }

        public const int TricksterFirstDays = 4, TricksterTurns = 3;

        /// <summary>After a gift of a god with a rival the colony knows: the rival's avatar comes to undo it this many days after at the soonest, once, by this chance a day while it may.</summary>
        public const int UndoAfterDays = 3, UndoUntilDays = 20;
        public const float UndoDayChance = 0.03f;

        /// <summary>The rival's coming answered: the gift let go (the rival pleased, the giver displeased), the avatar sent off (the rival's anger), or the colony stood between them (the giver pleased).</summary>
        public const float UndoLetGo = 6f, UndoGiverLoss = -5f, UndoSentOff = -8f, UndoStood = 6f;

        // ================= The drama =================

        /// <summary>Rival gods whose quarrel shows in the weather: one of the sky's against one of the dry land's or the water's.</summary>
        public static bool WeatherQuarrel(GodDomain a, GodDomain b) =>
            a == GodDomain.Storm && (b == GodDomain.Harvest || b == GodDomain.Waters || b == GodDomain.Hearth)
            || b == GodDomain.Storm && (a == GodDomain.Harvest || a == GodDomain.Waters || a == GodDomain.Hearth);

        /// <summary>A quarrel lasts this many days, a turn each this many days (the storm's, then the dry spell's).</summary>
        public const int QuarrelLastsDays = 12, QuarrelTurnDays = 2;

        /// <summary>Whose turn it is on a day of the quarrel: 0 the storm's side, 1 the other.</summary>
        public static int QuarrelTurn(int daysIn) => daysIn < 0 ? 0 : daysIn / QuarrelTurnDays % 2;

        /// <summary>A side taken: the god chosen pleased, the other displeased (before their natures).</summary>
        public const float SideTaken = 6f, SideSpurned = -5f;

        /// <summary>The god's ask (to slight a rival or to hide one the rival hunts) answered.</summary>
        public const float AskDone = 7f, AskRivalLoss = -7f, AskRefused = -5f;
        public const int AskRivalGoodwill = -5;

        /// <summary>One the rival hunts stays this many days when hidden.</summary>
        public const int HiddenDays = 3;

        /// <summary>
        /// How far a myth has come by the history of two gods' tie (the acts of it the colony remembers): the old tale at
        /// first, then the quarrel or the turn, then what came after. Lines are tagged myth:0, myth:1, myth:2.
        /// </summary>
        public static int MythStage(int tieActs) => tieActs <= 0 ? 0 : tieActs == 1 ? 1 : 2;

        /// <summary>A myth told at supper: an evening's chance at a home, and the days between two.</summary>
        public const float MythEveningChance = 0.12f;
        public const int MythGapDays = 3;

        /// <summary>The hours of supper (local).</summary>
        public static bool SupperHour(int hour) => hour >= 18 && hour <= 20;

        // ================= The Hunger =================

        /// <summary>
        /// How desperate someone or the colony is, for the Hunger (0 none, 3 the most): dying (bleeding out within hours,
        /// or an illness about to win), at the edge of a breakdown, or the colony out of food.
        /// </summary>
        public static int Desperation(bool dying, bool breaking, bool starving) => Math.Min(3, (dying ? 3 : 0) + (starving ? 2 : 0) + (breaking ? 1 : 0));

        /// <summary>The Hunger hears desperation: a look's chance by how desperate (once a day at a home at most), and the days between two bargains (shorter the more it has had).</summary>
        public static float HungerHears(int desperation) => desperation <= 0 ? 0f : desperation >= 3 ? 0.35f : desperation == 2 ? 0.2f : 0.08f;

        public static float HungerGapDays(int bargains) => Math.Max(15f, 60f - 10f * Math.Max(0, bargains));

        /// <summary>
        /// What the Hunger takes next, by how many times it has taken: first a mark of the void on the one who bargained,
        /// then years of their life, then their humanity (Anomaly's inhumanized; without it, their mind for a while), then a
        /// beast of the colony's into the dark and the dark's own come for more. It always takes more.
        /// </summary>
        public static string HungerTake(int taken) => taken <= 0 ? "mark" : taken == 1 ? "years" : taken == 2 ? "mind" : "beast";

        /// <summary>It comes to take this many days after it paid, sooner each time.</summary>
        public static int HungerTakeAfterDays(int taken) => Math.Max(1, 5 - Math.Max(0, taken));

        /// <summary>The years the Hunger takes.</summary>
        public const int HungerYears = 6;

        /// <summary>A refusal: the Hunger (jealous) displeased; a bargain struck: it is pleased, and the Choir of the Spire, who know it, warmer for the colony's honesty with them.</summary>
        public const float HungerStruck = 8f, HungerRefused = -4f;
        public const int ChoirGoodwill = 3;

        // ================= The ones who don't fit =================

        /// <summary>An archotech's exact term: how many of the goods (by their worth) and how many days to deliver them.</summary>
        public static int TermCount(float unitValue, float wealthFactor)
        {
            if (float.IsNaN(unitValue) || unitValue <= 0f) return 0;
            float worth = 400f * Clamp(wealthFactor, 0.5f, 2.5f);
            return Math.Max(1, (int)Math.Round(worth / unitValue));
        }

        public const int TermDays = 6;

        /// <summary>A term kept: the archotech pleased; broken: cold displeasure (a broken word counts half again for the cold).</summary>
        public const float TermKept = 8f, TermBroken = -12f;

        /// <summary>What an archotech gives for a term kept is worth this many times the goods.</summary>
        public const float TermReward = 1.6f;

        /// <summary>The dead god's mark found near an old thing: once a home, and how much the dead god (who hardly hears) is pleased by it being seen.</summary>
        public const float DeadMarkFavour = 6f;

        /// <summary>A fading god's nature decides its way: gentle (it asks to be remembered and blesses), or bitter (it demands, and curses a refusal).</summary>
        public static bool FadingGentle(GodNature n) => n == GodNature.Generous || n == GodNature.Patient || n == GodNature.Grieving || n == GodNature.Playful;

        public const float FadingRemembered = 10f, FadingForgotten = -6f;

        /// <summary>The Sleeper's door: three asks, each a step; the third opens it a crack.</summary>
        public const int SleeperSteps = 3;

        public const float SleeperStep = 8f, SleeperRefused = -3f;
        public const int VaultGoodwill = -6, VaultOpened = -15;

        /// <summary>A newborn god's signs as the colony's devotion grows it: one at each quarter of the way (GodsMath.NewbornGrown), each once.</summary>
        public static int NewbornSign(float devotion, float grown, int told)
        {
            if (float.IsNaN(devotion) || grown <= 0f) return -1;
            int stage = (int)(4f * devotion / grown);
            if (stage > 3) stage = 3;
            return stage > told ? stage : -1;
        }

        // ================= Words =================

        /// <summary>The words a piece of news opens with, by how real the gods are: "the colony believes", "some say", or none (the key's ending, CC_GW_Voice_*).</summary>
        public static string Voice(GodRealness r) => GodsMath.Voice(r);

        public static float Clamp(float v, float lo, float hi) => float.IsNaN(v) ? lo : Math.Max(lo, Math.Min(hi, v));
    }
}
