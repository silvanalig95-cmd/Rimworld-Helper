using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What an offering is, as a god sees it (WorshipMath.Likes): the colony's things sorted by what they are.</summary>
    public enum OfferingKind
    {
        Meal,
        /// <summary>Raw plant food: grain, roots, fruit.</summary>
        Plant,
        Meat,
        Fish,
        /// <summary>A hide or a pelt: leather of any kind.</summary>
        Pelt,
        /// <summary>Beer, wine, mead, spirits.</summary>
        Drink,
        /// <summary>Smoke and dreams: the other social drugs.</summary>
        Drug,
        Medicine,
        Weapon,
        Apparel,
        Art,
        /// <summary>Silver and gold.</summary>
        Silver,
        Metal,
        Wood,
        Component,
        Textile,
        /// <summary>Anything else made well by hand.</summary>
        Craft,
        Other
    }

    /// <summary>A hard moment someone prays in (docs/design/gods.md, "Worship": prayer).</summary>
    public enum PrayerKind
    {
        /// <summary>Sick, or someone close sick.</summary>
        Sickness,
        /// <summary>Someone of the colony died.</summary>
        Death,
        /// <summary>Raiders at the gate.</summary>
        Raid,
        /// <summary>No rain for days in the growing season.</summary>
        Drought
    }

    /// <summary>What a colonist vowed to a god (Gods.Vow's words; Worship/Vows.cs keeps what it is).</summary>
    public enum VowKind
    {
        /// <summary>No meat until someone sick recovers.</summary>
        NoMeat,
        /// <summary>A pilgrimage on foot to the god's holy place (a life goal).</summary>
        Pilgrimage,
        /// <summary>A temple raised to the god (a life goal).</summary>
        Temple,
        /// <summary>A beast of the god's spared: no animal of its sacred kind hunted for a season.</summary>
        BeastSpared
    }

    /// <summary>What became of an offering left at a shrine.</summary>
    public enum OfferingFate
    {
        Waiting,
        /// <summary>The god took it (Felt: gone in the night; Walking: before everyone's eyes).</summary>
        Taken,
        /// <summary>Believed: an animal came and ate it.</summary>
        Animal,
        /// <summary>Believed: someone passing took it with them.</summary>
        Passerby,
        /// <summary>Believed: it lay there and spoiled.</summary>
        Spoiled,
        /// <summary>Believed: nobody came for it, and it went back to the stores.</summary>
        Returned
    }

    /// <summary>What a god says in a dream.</summary>
    public enum GodDreamKind
    {
        /// <summary>A danger that is real now: a sacred beast marked for the hunt, a sickness, the god's own anger.</summary>
        Warning,
        /// <summary>An offering asked for at its shrine.</summary>
        Demand,
        /// <summary>The name of a god the colony didn't know.</summary>
        Name,
        /// <summary>A priest's or a psychic's clearer sight: the gods' ties, what pleases the god.</summary>
        Vision
    }

    /// <summary>
    /// The pure rules of worship (docs/design/gods.md, stage 2 "Worship"; Source/ColonyCouncil/Gods/Worship/), with no game
    /// types so Tools/Tests can check them: what a shrine is called in each age; what each domain likes offered, how its
    /// nature weighs a gift, and how much favour it moves; when an offering is taken at each step of how real the gods are;
    /// who prays in a hard moment, how much it comforts, and how often an answer comes; when an unanswered prayer becomes a
    /// crisis of faith and a turning to another god; the festivals' dates by domain; vows; dreams; scoffers; RimWorld of
    /// Magic's mana by favour; and the council's choices.
    /// </summary>
    public static class WorshipMath
    {
        // ================= Shrines =================

        /// <summary>What a dedicated place is called in the colony's age (0 tribal to 3 spacer): the key's ending, CC_Wsh_Shrine_*.</summary>
        public static string ShrineWord(int age) => age <= 0 ? "Shrine" : age == 1 ? "Chapel" : age == 2 ? "Church" : "Terminal";

        /// <summary>A dedicated place is a temple: indoors, in a room of this impressiveness and size at least.</summary>
        public const float TempleImpressiveness = 50f;
        public const int TempleCells = 16;

        public static bool IsTemple(bool indoors, float impressiveness, int cells) => indoors && impressiveness >= TempleImpressiveness && cells >= TempleCells;

        /// <summary>Dedicating a place pleases its god this much (a rite); taking it from one god for another displeases the first this much.</summary>
        public const float DedicateFavour = 3f, RededicateFavour = -2f;

        /// <summary>How long a dedication waits for someone free to walk over and say the words, before it is let go.</summary>
        public const int DedicateWaitDays = 2;

        // ================= Offerings =================

        private static readonly Dictionary<GodDomain, (OfferingKind k, float w)[]> LikesTable = new Dictionary<GodDomain, (OfferingKind, float)[]>
        {
            [GodDomain.Harvest] = new[] { (OfferingKind.Plant, 1.5f), (OfferingKind.Meal, 1.2f), (OfferingKind.Drink, 1.1f) },
            [GodDomain.Storm] = new[] { (OfferingKind.Drink, 1.3f), (OfferingKind.Meat, 1.1f), (OfferingKind.Weapon, 1f) },
            [GodDomain.Hunt] = new[] { (OfferingKind.Pelt, 1.6f), (OfferingKind.Meat, 1.3f), (OfferingKind.Weapon, 1f) },
            [GodDomain.Healing] = new[] { (OfferingKind.Medicine, 1.5f), (OfferingKind.Textile, 1f), (OfferingKind.Plant, 0.9f) },
            [GodDomain.War] = new[] { (OfferingKind.Weapon, 1.5f), (OfferingKind.Apparel, 1.1f), (OfferingKind.Meat, 1f) },
            [GodDomain.Craft] = new[] { (OfferingKind.Craft, 1.4f), (OfferingKind.Metal, 1.2f), (OfferingKind.Art, 1.2f), (OfferingKind.Apparel, 1f) },
            [GodDomain.Way] = new[] { (OfferingKind.Silver, 1.5f), (OfferingKind.Craft, 1f), (OfferingKind.Meal, 0.9f) },
            [GodDomain.Dead] = new[] { (OfferingKind.Art, 1.1f), (OfferingKind.Drink, 1f), (OfferingKind.Textile, 1f), (OfferingKind.Meal, 0.8f) },
            [GodDomain.Night] = new[] { (OfferingKind.Drug, 1.4f), (OfferingKind.Drink, 1.2f), (OfferingKind.Art, 1f) },
            [GodDomain.Hearth] = new[] { (OfferingKind.Meal, 1.5f), (OfferingKind.Drink, 1f), (OfferingKind.Textile, 1f) },
            [GodDomain.Wild] = new[] { (OfferingKind.Plant, 1.4f), (OfferingKind.Wood, 1.2f), (OfferingKind.Medicine, 0.8f) },
            [GodDomain.Waters] = new[] { (OfferingKind.Fish, 1.6f), (OfferingKind.Silver, 1f), (OfferingKind.Meat, 0.8f) },
            [GodDomain.Machine] = new[] { (OfferingKind.Component, 1.5f), (OfferingKind.Metal, 1.2f), (OfferingKind.Craft, 0.9f) },
            [GodDomain.Void] = new[] { (OfferingKind.Meat, 1.2f), (OfferingKind.Art, 1f), (OfferingKind.Drug, 1f) }
        };

        /// <summary>What a domain likes offered, best first.</summary>
        public static OfferingKind[] Likes(GodDomain d)
        {
            if (!LikesTable.TryGetValue(d, out var t)) return new OfferingKind[0];
            var list = new OfferingKind[t.Length];
            for (int i = 0; i < t.Length; i++) list[i] = t[i].k;
            return list;
        }

        /// <summary>How well an offering of a kind suits a god of these domains: the first domain's liking, the second's at 60%, else 0.4 (taken, not loved).</summary>
        public const float Unliked = 0.4f, SecondShare = 0.6f;

        public static float Fit(OfferingKind k, GodDomain d, GodDomain second, GodKind kind)
        {
            if (!TakesOfferings(kind)) return 0f;
            // The Hunger takes anything, and meat gladly.
            if (kind == GodKind.Hunger) return k == OfferingKind.Meat ? 1.2f : 1f;
            float best = Unliked;
            if (LikesTable.TryGetValue(d, out var t))
                foreach (var (x, w) in t)
                    if (x == k) best = Math.Max(best, w);
            if (second != GodDomain.None && LikesTable.TryGetValue(second, out var t2))
                foreach (var (x, w) in t2)
                    if (x == k) best = Math.Max(best, w * SecondShare);
            return best;
        }

        /// <summary>How well an offering suits a god by a table of its own (a hand-made god's likes): its weight there, else 0.4; the Hunger and the kinds that take none as in <see cref="Fit"/>.</summary>
        public static float FitFrom((OfferingKind k, float w)[] likes, OfferingKind k, GodKind kind)
        {
            if (!TakesOfferings(kind)) return 0f;
            if (kind == GodKind.Hunger) return k == OfferingKind.Meat ? 1.2f : 1f;
            float best = Unliked;
            if (likes != null)
                foreach (var (x, w) in likes)
                    if (x == k) best = Math.Max(best, w);
            return best;
        }

        /// <summary>Archotechs care nothing for offerings, and a dead god takes nothing; the rest take them.</summary>
        public static bool TakesOfferings(GodKind kind) => kind != GodKind.Archotech && kind != GodKind.Dead;

        /// <summary>
        /// How a god's nature weighs a gift: a proud god wants the best made (quality 0 awful to 6 legendary, -1 none), a
        /// generous one food shared, a playful one art and good cheer, a wrathful one weapons, a grieving one what remembers,
        /// a capricious one whatever takes its fancy that day (<paramref name="roll"/> in [0, 1)).
        /// </summary>
        public static float NatureFit(GodNature n, OfferingKind k, int quality, float roll)
        {
            switch (n)
            {
                case GodNature.Proud:
                    if (quality >= 4) return 1.3f;
                    if (quality >= 0 && quality <= 1) return 0.6f;
                    return 1f;
                case GodNature.Generous:
                    return k == OfferingKind.Meal || k == OfferingKind.Plant || k == OfferingKind.Fish ? 1.2f : 1f;
                case GodNature.Playful:
                    return k == OfferingKind.Art || k == OfferingKind.Drink || k == OfferingKind.Drug ? 1.3f : 1f;
                case GodNature.Wrathful:
                    return k == OfferingKind.Weapon ? 1.3f : 1f;
                case GodNature.Grieving:
                    return k == OfferingKind.Art || k == OfferingKind.Drink || k == OfferingKind.Textile ? 1.2f : 1f;
                case GodNature.Capricious:
                    return 0.6f + 0.9f * Clamp01(roll);
                default:
                    return 1f;
            }
        }

        /// <summary>The most an offering is worth (market value), for a colony of this many people; the least worth leaving.</summary>
        public static float OfferingValueCap(int colonists) => Math.Min(250f, 40f + 8f * Math.Max(1, colonists));

        public const float OfferingValueMin = 6f;

        /// <summary>What an offering aims at in market value: a fair gift, not the colony's stores.</summary>
        public static float OfferingValueTarget(int colonists) => Math.Min(OfferingValueCap(colonists), 20f + 3f * Math.Max(1, colonists));

        /// <summary>
        /// The favour an offering moves, before the god's nature and kind (Gods.AddFavour with FavourSource.Offering scales
        /// it): grows with its worth (a meal about 2, a hundred silver's worth about 3.5, five times that about 5), times
        /// how well it suits the god and its nature, a quarter more for the god the council honours this year, a fifth more
        /// when the priest brings it.
        /// </summary>
        public static float OfferingFavour(float marketValue, float fit, float natureFit, bool honoured, bool priest)
        {
            if (fit <= 0f || float.IsNaN(marketValue)) return 0f;
            float worth = 1.5f + 1.2f * (float)Math.Log(1.0 + Math.Max(0f, marketValue) / 20.0);
            worth = Math.Min(6f, worth);
            return worth * fit * natureFit * (honoured ? HonourFactor : 1f) * (priest ? PriestRiteFactor : 1f);
        }

        /// <summary>
        /// The day's chance that someone carries an offering to a dedicated place: more with more of the devout at home and a
        /// priest, more for the god the council honours, never past half a day's chance.
        /// </summary>
        public static float OfferingChance(int devout, bool priest, bool honoured)
        {
            float c = 0.18f + 0.06f * Math.Min(3, Math.Max(0, devout)) + (priest ? 0.1f : 0f) + (honoured ? 0.08f : 0f);
            return Math.Min(0.5f, c);
        }

        /// <summary>Believed: an offering of food left this long, nobody having eaten it, has spoiled; anything else this long goes back to the stores.</summary>
        public const int SpoilDays = 2, ReturnDays = 3;

        /// <summary>
        /// An offering's fate in Believed: food goes to an animal that comes for it, anything to someone passing, else food
        /// spoils and the rest goes back to the stores once it has waited its time.
        /// </summary>
        public static OfferingFate BelievedFate(bool food, bool animalNear, bool passerby, float daysWaited)
        {
            if (food && animalNear) return OfferingFate.Animal;
            if (passerby) return OfferingFate.Passerby;
            if (food) return daysWaited >= SpoilDays ? OfferingFate.Spoiled : OfferingFate.Waiting;
            return daysWaited >= ReturnDays ? OfferingFate.Returned : OfferingFate.Waiting;
        }

        /// <summary>
        /// The god takes it now (Felt and Walking): Walking at the next look once it is set down, before anyone's eyes; Felt
        /// in the small hours (an hour of the day from 0 to 23), when nobody saw it go. Believed: never (WorshipMath.BelievedFate).
        /// </summary>
        public static bool GodTakesNow(GodRealness r, int hourOfDay, float hoursWaited)
        {
            if (r == GodRealness.Walking) return hoursWaited >= 1f;
            if (r == GodRealness.Felt) return hoursWaited >= 3f && hourOfDay >= 1 && hourOfDay <= 4;
            return false;
        }

        // ================= Prayer =================

        /// <summary>
        /// The chance someone prays when a hard moment touches them: the devout most, the wavering little, a scoffer never;
        /// half again when it is someone close (their partner, kin, a friend) who is sick or gone.
        /// </summary>
        public static float PrayChance(bool devout, bool wavering, bool scoffer, bool close)
        {
            if (scoffer) return 0f;
            float c = devout ? 0.6f : wavering ? 0.15f : 0.3f;
            if (close) c *= 1.5f;
            return Math.Min(0.9f, c);
        }

        /// <summary>One prayer of a kind a person each this many days, and at most this many prayers a home a day.</summary>
        public const float PrayGapDays = 2f;
        public const int PrayersADay = 4;

        /// <summary>Strain a prayer eases (Interplay.Ease, in the heart's daily cap): where they stand, more at a shrine, a little more for the devout.</summary>
        public static float Comfort(bool atShrine, bool devout) => (atShrine ? 5f : 3f) + (devout ? 1f : 0f);

        /// <summary>A prayer's own favour with its god (a rite), more at a dedicated place.</summary>
        public static float PrayerFavour(bool atShrine) => atShrine ? 1.5f : 0.8f;

        /// <summary>
        /// The chance a prayer is answered (a sign at the strength of the step): never in Believed; in Felt now and then, in
        /// Walking more; by how loudly the god answers (Gods.Loudness), the colony's standing with it, a priest leading it, a
        /// dedicated place, and the god the council honours this year. An archotech judges deeds, not prayers (Walking only,
        /// and seldom); the Hunger answers anyone; a dead god never; a newborn or a fading one weakly.
        /// </summary>
        public static float AnswerChance(GodRealness r, GodKind kind, float loudness, FavourWord w, bool priest, bool atShrine, bool honoured)
        {
            if (r == GodRealness.Believed || kind == GodKind.Dead) return 0f;
            float c = r == GodRealness.Walking ? 0.25f : 0.12f;
            c *= 0.6f + Clamp01(loudness);
            c *= WordFactor(w);
            if (priest) c *= 1.5f;
            if (atShrine) c *= 1.3f;
            if (honoured) c *= 1.2f;
            switch (kind)
            {
                case GodKind.Archotech: c = r == GodRealness.Walking ? c * 0.25f : 0f; break;
                case GodKind.Hunger: return Math.Min(0.8f, Math.Max(c, 0.2f) * 2f);
                case GodKind.Newborn:
                case GodKind.Fading: c *= 0.5f; break;
            }
            return Math.Min(0.6f, c);
        }

        /// <summary>How the colony's standing weighs a god's will to answer.</summary>
        public static float WordFactor(FavourWord w) => w switch
        {
            FavourWord.Wrathful => 0f,
            FavourWord.Displeased => 0.3f,
            FavourWord.Unknown => 0.7f,
            FavourWord.Noticed => 1f,
            FavourWord.Pleased => 1.3f,
            FavourWord.Favoured => 1.6f,
            _ => 2f
        };

        /// <summary>A god answers at most once in this many days at a home.</summary>
        public const float AnswerGapDays = 5f;

        /// <summary>How much of a sickness an answer eases (its severity), by the step: a little in Felt, plainly in Walking.</summary>
        public static float SicknessEase(GodRealness r) => r == GodRealness.Walking ? 0.15f : r == GodRealness.Felt ? 0.08f : 0f;

        /// <summary>How many days a prayer waits to see how things turn out, by its kind.</summary>
        public static int ResolveDays(PrayerKind k) => k switch
        {
            PrayerKind.Sickness => 12,
            PrayerKind.Death => 3,
            PrayerKind.Raid => 2,
            _ => 5
        };

        /// <summary>A prayer that came good on its own (the sick got well, the rain came, the raid was beaten) is credited to the god this much.</summary>
        public const float CreditFavour = 2.5f;

        /// <summary>No rain or snow for this many days running, outdoors warm enough to grow, with fields sown: a drought to pray in.</summary>
        public const int DroughtDays = 6;

        public static bool Drought(int dryDays, bool growing, bool fields) => growing && fields && dryDays >= DroughtDays;

        /// <summary>
        /// An unanswered prayer (the sick died, the rain never came, someone fell in the raid) is a crisis of faith this often;
        /// with Ideology it shakes their certainty this much; two crises within this many days and they may turn to another god.
        /// </summary>
        public const float CrisisChance = 0.35f, CrisisShake = 0.06f, TurnChance = 0.5f;
        public const int CrisesToTurn = 2, CrisisWindowDays = 30;

        /// <summary>A crisis turns them to another god: two crises inside the window, and the roll.</summary>
        public static bool Turns(int crisesInWindow, float roll) => crisesInWindow >= CrisesToTurn && roll < TurnChance;

        // ================= Scoffers =================

        /// <summary>
        /// A scoffer: someone whose faith despises religion, whose faith has no gods and who isn't devout, or who is far from
        /// sure of it (with Ideology); without Ideology, about one in seven by who they are (the same each time).
        /// </summary>
        public static bool Scoffer(bool noReligion, bool ideology, bool godlessFaith, float certainty, int id)
        {
            if (noReligion) return true;
            if (ideology) return godlessFaith && certainty < 0.85f || certainty < 0.3f;
            uint h = unchecked((uint)id * 2654435761u);
            return (h >> 16) % 7 == 0;
        }

        /// <summary>A scoffer's mockery offends the believer's god this much (Scoffing), once in this many days at a home; a couple bickers once in this many.</summary>
        public const float MockFavour = -1.5f, MockGapDays = 3f, BickerGapDays = 5f;

        // ================= Festivals =================

        /// <summary>
        /// When a domain's festival comes: the season (0 spring, 1 summer, 2 fall, 3 winter) and the day within it (0 to 14):
        /// the harvest's thanksgiving late in the fall, the longest night in the middle of winter, the dead's day as the fall
        /// turns to winter, the first green in spring, the storms at the start of summer.
        /// </summary>
        public static (int season, int day) FestivalWhen(GodDomain d) => d switch
        {
            GodDomain.Harvest => (2, 11),
            GodDomain.Night => (3, 7),
            GodDomain.Dead => (2, 14),
            GodDomain.Storm => (1, 2),
            GodDomain.Hunt => (2, 4),
            GodDomain.Healing => (0, 6),
            GodDomain.War => (1, 10),
            GodDomain.Craft => (3, 2),
            GodDomain.Way => (0, 12),
            GodDomain.Hearth => (3, 12),
            GodDomain.Wild => (0, 1),
            GodDomain.Waters => (1, 6),
            GodDomain.Machine => (2, 1),
            GodDomain.Void => (3, 14),
            _ => (1, 7)
        };

        /// <summary>A festival's day of the year (0 to 59): its season's quadrum (where that season falls in this land), its day, and a step on for each festival already on that day.</summary>
        public static int FestivalDay(int quadrumOfSeason, int dayInSeason, int clashes) => Wrap(quadrumOfSeason * 15 + Math.Max(0, Math.Min(14, dayInSeason)) + 2 * Math.Max(0, clashes));

        public static int Wrap(int day) => ((day % 60) + 60) % 60;

        /// <summary>At most this many of the colony's gods have a festival on its calendar.</summary>
        public const int MaxFestivals = 3;

        /// <summary>A festival kept pleases its god (a rite); let pass, it displeases (neglect).</summary>
        public const float FestivalKept = 6f, FestivalPassed = -4f;

        /// <summary>A people that holds the god comes to its festival this often (a small group of visitors).</summary>
        public const float FestivalVisitorsChance = 0.5f;

        // ================= Vows =================

        /// <summary>How long a vow runs at most, in days (0: as long as the life goal it belongs to).</summary>
        public static int VowDays(VowKind k) => k switch
        {
            VowKind.NoMeat => 20,
            VowKind.BeastSpared => 15,
            _ => 0
        };

        /// <summary>The chance a prayer for someone close who is gravely ill becomes a vow of no meat; a hunter's look at a sacred beast a vow to spare its kind.</summary>
        public const float NoMeatVowChance = 0.3f, BeastVowChance = 0.25f;

        /// <summary>
        /// "Sometimes wrath": a broken vow brings the god's anger on top of its displeasure when its nature takes a broken word
        /// worst (patient, cold) or its temper is short (wrathful, proud), half the time; a capricious god a quarter.
        /// </summary>
        public static bool WrathAfterBroken(GodNature n, float roll) => n switch
        {
            GodNature.Patient or GodNature.Cold or GodNature.Wrathful or GodNature.Proud => roll < 0.5f,
            GodNature.Capricious => roll < 0.25f,
            _ => false
        };

        /// <summary>The wrath of a broken vow, on top of the core's own loss (Gods.EndVow).</summary>
        public const float WrathFavour = -8f;

        /// <summary>A kept temple or pilgrimage is a greater thing than a short vow: this much more favour, a rite.</summary>
        public const float GreatVowFavour = 6f;

        // ================= The priest and the council =================

        /// <summary>A priest's prayers and rites weigh more.</summary>
        public const float PriestRiteFactor = 1.2f;

        /// <summary>The god the council honours this year: a quarter more for offerings, prayers and its festival.</summary>
        public const float HonourFactor = 1.25f;

        /// <summary>Honouring a god for the year pleases it this much (a rite).</summary>
        public const float HonourFavour = 8f;

        /// <summary>The god's asking: the beast given, refused, or something lesser offered instead.</summary>
        public const float BeastGiven = 15f, BeastRefused = -10f, LesserGiven = 4f;

        /// <summary>A refusal keeps the rain away this many days (Felt), more when the gods walk; the lesser gift half as long, half the time.</summary>
        public static int DrySpellDays(GodRealness r, bool lesser) => r == GodRealness.Believed ? 0 : (r == GodRealness.Walking ? 8 : 5) / (lesser ? 2 : 1);

        /// <summary>Two rival gods: the one honoured is pleased this much, both honoured each this much (jealous ones take it as a slight).</summary>
        public const float RivalChosen = 6f, RivalBoth = 2f, RivalJealous = -2f;

        // ================= Dreams =================

        /// <summary>
        /// Of the dreams someone has, the share that are a god's: only with a god of their own; twice for a priest, half again
        /// for one with a psychic gift, a little more for the devout, a third for a scoffer.
        /// </summary>
        public static float GodDreamShare(bool hasGod, bool priest, bool psychic, bool devout, bool scoffer)
        {
            if (!hasGod) return 0f;
            float s = 0.12f;
            if (priest) s *= 2f;
            if (psychic) s *= 1.5f;
            if (devout) s *= 1.3f;
            if (scoffer) s *= 0.3f;
            return Math.Min(0.5f, s);
        }

        /// <summary>The kinds of god dream that can come now, by weight: a warning where there's a real danger, a demand where there's a shrine and none asked, a name where a god is unknown, a vision for those who see clearly.</summary>
        public static GodDreamKind PickDream(bool danger, bool demand, bool unknownGod, bool clear, float roll)
        {
            float w = danger ? 3f : 0f, d = demand ? 1.5f : 0f, n = unknownGod ? 1f : 0f, v = clear ? 1f : 0f;
            float total = w + d + n + v;
            if (total <= 0f) return GodDreamKind.Vision;
            float r = Clamp01(roll) * total;
            if (r < w) return GodDreamKind.Warning;
            r -= w;
            if (r < d) return GodDreamKind.Demand;
            r -= d;
            if (r < n) return GodDreamKind.Name;
            return GodDreamKind.Vision;
        }

        /// <summary>A demand waits this many days; met, it pleases the god this much, let go, it displeases.</summary>
        public const int DemandDays = 3;
        public const float DemandMet = 6f, DemandIgnored = -4f;

        // ================= A RimWorld of Magic =================

        /// <summary>
        /// A caster's mana coming back, by the colony's standing with their god: beloved half again, favoured a third more,
        /// pleased, noticed a little; displeased slower, wrathful cut off. Nothing in Believed; in Felt only most of the way.
        /// </summary>
        public static float ManaFactor(FavourWord w, GodRealness r)
        {
            if (r == GodRealness.Believed) return 1f;
            float f = w switch
            {
                FavourWord.Beloved => 1.5f,
                FavourWord.Favoured => 1.3f,
                FavourWord.Pleased => 1.15f,
                FavourWord.Noticed => 1.05f,
                FavourWord.Displeased => 0.6f,
                FavourWord.Wrathful => 0f,
                _ => 1f
            };
            return r == GodRealness.Felt ? 1f + (f - 1f) * 0.6f : f;
        }

        /// <summary>The domain a RimWorld of Magic class serves (its trait's defName), or None (docs/design/gods.md, "Gifts").</summary>
        public static GodDomain ClassDomain(string trait) => trait switch
        {
            "Priest" or "Paladin" or "TM_Brightmage" or "TM_Apothecary" => GodDomain.Healing,
            "Druid" or "TM_Shaman" => GodDomain.Wild,
            "Ranger" or "TM_Sniper" => GodDomain.Hunt,
            "Geomancer" or "Enchanter" or "Summoner" or "TM_Golemancer" => GodDomain.Craft,
            "Necromancer" or "DeathKnight" or "Lich" => GodDomain.Dead,
            "StormBorn" or "HeartOfFrost" => GodDomain.Storm,
            "Technomancer" or "TM_SuperSoldier" => GodDomain.Machine,
            "TM_Bard" or "Arcanist" or "Chronomancer" or "TM_Psionic" or "Faceless" or "TM_TheShadow" => GodDomain.Night,
            "Gladiator" or "TM_Monk" or "Bladedancer" or "TM_Commander" or "BloodMage" => GodDomain.War,
            "InnerFire" => GodDomain.Hearth,
            "Warlock" or "Succubus" or "ChaosMage" => GodDomain.Void,
            "TM_Wanderer" or "TM_Wayfarer" => GodDomain.Way,
            _ => GodDomain.None
        };

        // ================= Small helpers =================

        public static float Clamp01(float x) => float.IsNaN(x) ? 0f : Math.Max(0f, Math.Min(1f, x));
    }
}
