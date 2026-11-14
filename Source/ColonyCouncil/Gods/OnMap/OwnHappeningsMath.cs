using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Each god's own happenings (docs/gods-next-proposals.md, section C; round 4, batch 1: Hollin, Smoulder, Bellwether,
    /// Morel, Ennu, Pip and Cirrus). The name starts with the god's key, so a happening knows whose it is.
    /// </summary>
    public enum OwnHappening
    {
        /// <summary>A colony that fells many trees and plants few: roots break up through a floor, a tile a morning.</summary>
        HollinRoots,
        /// <summary>The dryads stop and turn to one colonist, one walks over, and they hear one word that points at something real.</summary>
        HollinDryads,
        /// <summary>Her rising, twice more along a line across the land, new grass and flowers between, the beasts following.</summary>
        HollinWalk,
        /// <summary>The colony's oldest tree spared through a year of felling: healroot in a ring at its foot in spring.</summary>
        HollinOldTree,
        /// <summary>A miner dreams of a warm voice's bargain: silver down the deepest shaft, gold back; kept to the letter.</summary>
        SmoulderBargain,
        /// <summary>An artist names a sculpture for Aurelian the Ninth; the next day a seam of silver glints in the nearest rock.</summary>
        SmoulderName,
        /// <summary>Before stone ever falls in the deep working, its floor grows hot for hours.</summary>
        SmoulderHeat,
        /// <summary>After the winged shadow at noon, every miner dreams the same dream of something stretching.</summary>
        SmoulderStretch,
        /// <summary>Silver hoarded and never paid to the deep: a miner wakes from a warm dream with a burn on the hand.</summary>
        SmoulderBurn,
        /// <summary>The white beast stops at the colony's edge at dusk, and at dawn takes his road through it; a fence makes him wait.</summary>
        BellwetherRoad,
        /// <summary>A child who touched the horn looks at the sky and says the rain is coming, and it comes.</summary>
        BellwetherHorn,
        /// <summary>A wounded thrumbo lies down at the colony's edge; tended, it goes its way and the strange peace follows.</summary>
        BellwetherHurt,
        /// <summary>After thrumbos pass, someone dreams of a white beast searching for the rest of its kind.</summary>
        BellwetherLast,
        /// <summary>At dusk after a death, a figure of pale threads stands at the edge of the graves; at dawn a ring of mushrooms.</summary>
        MorelMourner,
        /// <summary>A ring of mushrooms overnight round something real: a body left lying, rotting stores, a small thing buried.</summary>
        MorelRing,
        /// <summary>White threads across the freezer's floor the day before her anger breaks it.</summary>
        MorelThreads,
        /// <summary>Someone walking through one of her rings hears her for a moment, soft and too close.</summary>
        MorelBreath,
        /// <summary>The stranger at the gate asks one small thing: a story at the fire, the cook's name, the graves, a coat.</summary>
        EnnuTest,
        /// <summary>A bowl set out at dusk is empty by morning, and the cook thanked by name in chalk on the table.</summary>
        EnnuBowl,
        /// <summary>While it stays, the stranger tells a piece of another god's story at the fire.</summary>
        EnnuTales,
        /// <summary>Visitors linger with the one the Guest found kindest and tell them the road's news.</summary>
        EnnuKindest,
        /// <summary>A machine switched off keeps running an hour, then stops on its own.</summary>
        PipWontStop,
        /// <summary>The comms console comes on in the night; by morning a half-written message to the stars is on its screen.</summary>
        PipStars,
        /// <summary>In a fight a turret holds its fire for a breath as a colonist crosses its line.</summary>
        PipTurret,
        /// <summary>The lights flicker slow and sad the day before Pip's short circuit.</summary>
        PipSulk,
        /// <summary>The falling star comes only to a colony that watches the sky, and the watcher saw it fall.</summary>
        CirrusStar,
        /// <summary>Whoever saw the wrong-way star dreams where something fell; the next day pods come down there.</summary>
        CirrusFall,
        /// <summary>The aurora on the coldest night, for those out of doors.</summary>
        CirrusAurora,
        /// <summary>Thunder and lightning over the far hills on a clear day, no rain, never on the colony.</summary>
        CirrusThunder
    }

    /// <summary>The one word Hollin's dryads give, each pointing at something real on the land.</summary>
    public enum DryadWord
    {
        /// <summary>Trees marked to be cut.</summary>
        Spare,
        /// <summary>A frost coming while the fields are growing.</summary>
        Wait,
        /// <summary>A field left bare in the growing season.</summary>
        Plant,
        /// <summary>Blight in the fields.</summary>
        Burn
    }

    /// <summary>The Guest's small test: what it asks of the colony this time.</summary>
    public enum GuestTest
    {
        /// <summary>A story at the fire in the evening.</summary>
        Story,
        /// <summary>The name of whoever cooked the meal.</summary>
        Cook,
        /// <summary>To be shown the graves.</summary>
        Graves,
        /// <summary>A coat, in the cold.</summary>
        Coat
    }

    /// <summary>What Morel's ring is round: a body left lying, the rotting stores, or a small thing buried in open ground.</summary>
    public enum RingAt
    {
        Body,
        Stores,
        Buried
    }

    /// <summary>
    /// The rules of each god's own happenings (Gods/OnMap/OwnHappenings.cs and the gods' own files beside it), pure so
    /// Tools/Tests reads the same: when each may come, how strongly, and what comes of it. Every happening keeps the
    /// gods' pace (GodPaceMath): its own gap from the last, a chance that grows after it, never on a condition that is
    /// nearly always true, a little more often the nearer its god is drawn (GodNearMath.WorldFactor); the ones that answer
    /// something on the map (a rising, a sign, a fight, a death) come only with it.
    /// </summary>
    public static class OwnHappeningsMath
    {
        /// <summary>The gods of this batch, by key.</summary>
        public static readonly string[] Gods = { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus" };

        public static readonly OwnHappening[] All = (OwnHappening[])Enum.GetValues(typeof(OwnHappening));

        /// <summary>The key of the god a happening is of ("Hollin").</summary>
        public static string GodOf(OwnHappening h)
        {
            string n = h.ToString();
            foreach (var g in Gods)
                if (n.StartsWith(g, StringComparison.Ordinal))
                    return g;
            return null;
        }

        /// <summary>The happening's own name without its god ("Roots").</summary>
        public static string Name(OwnHappening h)
        {
            string g = GodOf(h);
            string n = h.ToString();
            return g == null ? n : n.Substring(g.Length);
        }

        /// <summary>A god's own happenings, in order.</summary>
        public static List<OwnHappening> Of(string god)
        {
            var list = new List<OwnHappening>();
            foreach (var h in All)
                if (GodOf(h) == god)
                    list.Add(h);
            return list;
        }

        // ================= The pace =================

        /// <summary>
        /// A happening's pace: never within <c>gap</c> days of its last; about every <c>mean</c> days once it is free and
        /// called for (0: it comes only with what answers it, at its own chance); its chance growing over <c>ramp</c> days.
        /// </summary>
        public static (float gap, float mean, float ramp) PaceOf(OwnHappening h)
        {
            switch (h)
            {
                case OwnHappening.HollinRoots: return (30f, 6f, 20f);
                case OwnHappening.HollinDryads: return (20f, 10f, 20f);
                case OwnHappening.HollinWalk: return (40f, 0f, 0f);
                case OwnHappening.HollinOldTree: return (50f, 0f, 0f);
                case OwnHappening.SmoulderBargain: return (45f, 20f, 30f);
                case OwnHappening.SmoulderName: return (20f, 0f, 0f);
                case OwnHappening.SmoulderHeat: return (0f, 0f, 0f);
                case OwnHappening.SmoulderStretch: return (15f, 0f, 0f);
                case OwnHappening.SmoulderBurn: return (30f, 20f, 30f);
                case OwnHappening.BellwetherRoad: return (0f, 0f, 0f);
                case OwnHappening.BellwetherHorn: return (2f, 0f, 0f);
                case OwnHappening.BellwetherHurt: return (45f, 40f, 40f);
                case OwnHappening.BellwetherLast: return (20f, 0f, 0f);
                case OwnHappening.MorelMourner: return (30f, 5f, 20f);
                case OwnHappening.MorelRing: return (14f, 8f, 20f);
                case OwnHappening.MorelThreads: return (0f, 0f, 0f);
                case OwnHappening.MorelBreath: return (8f, 0f, 0f);
                case OwnHappening.EnnuTest: return (0f, 0f, 0f);
                case OwnHappening.EnnuBowl: return (20f, 0f, 0f);
                case OwnHappening.EnnuTales: return (0f, 0f, 0f);
                case OwnHappening.EnnuKindest: return (3f, 0f, 0f);
                case OwnHappening.PipWontStop: return (12f, 0f, 20f);
                case OwnHappening.PipStars: return (25f, 12f, 30f);
                case OwnHappening.PipTurret: return (6f, 0f, 0f);
                case OwnHappening.PipSulk: return (0f, 0f, 0f);
                case OwnHappening.CirrusStar: return (0f, 0f, 0f);
                case OwnHappening.CirrusFall: return (20f, 0f, 0f);
                case OwnHappening.CirrusAurora: return (30f, 0f, 0f);
                case OwnHappening.CirrusThunder: return (25f, 30f, 30f);
                default: return (30f, 0f, 0f);
            }
        }

        /// <summary>Its gap from the last is past.</summary>
        public static bool Free(OwnHappening h, float daysSince) => !float.IsNaN(daysSince) && daysSince >= PaceOf(h).gap;

        /// <summary>
        /// A day's chance of a drawn happening (one with a <c>mean</c>): none within its gap or without a reason, its share
        /// growing after the gap (GodPaceMath.DayChance), more for a strong reason, a little more the nearer its god is.
        /// </summary>
        public static float DayChance(OwnHappening h, float daysSince, GodPaceMath.Trigger why, float near)
        {
            var p = PaceOf(h);
            float w = GodPaceMath.Weight(why);
            if (p.mean <= 0f || w <= 0f) return 0f;
            return Clamp01(GodPaceMath.DayChance(daysSince, p.gap, p.mean, p.ramp) * w * GodNearMath.WorldFactor(near));
        }

        /// <summary>The same shared out over <paramref name="checks"/> looks in a day (24 for the hour).</summary>
        public static float CheckChance(OwnHappening h, float daysSince, GodPaceMath.Trigger why, float near, float checks) =>
            GodPaceMath.PerCheck(DayChance(h, daysSince, why, near), checks);

        // ================= Hollin =================

        /// <summary>Trees felled in a season from which the roots remember, and a strong reason from this many with hardly any planted.</summary>
        public const int RootsFelledFrom = 15, RootsStrongFrom = 40;

        /// <summary>The roots remember a season of felling with little planted: none below the line or with a third planted back.</summary>
        public static GodPaceMath.Trigger RootsTrigger(int felled, int planted)
        {
            if (felled < RootsFelledFrom || planted * 3 >= felled) return GodPaceMath.Trigger.None;
            return felled >= RootsStrongFrom && planted * 6 < felled ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;
        }

        /// <summary>How many mornings a tile breaks (one a morning), and how far from the cleared ground a room may be (cells).</summary>
        public const int RootsMornings = 3;
        public const float RootsReach = 40f;

        /// <summary>Trees marked for cutting that make "spare" a word.</summary>
        public const int SpareFrom = 3;

        /// <summary>
        /// The words the dryads may give now, each only for something real on the land: trees marked to be cut, a frost coming
        /// while fields grow, a field left bare in the growing season, blight in the fields.
        /// </summary>
        public static List<DryadWord> Words(int treesMarked, bool frostAhead, bool fieldsGrowing, bool bareField, int blighted)
        {
            var list = new List<DryadWord>();
            if (treesMarked >= SpareFrom) list.Add(DryadWord.Spare);
            if (frostAhead && fieldsGrowing) list.Add(DryadWord.Wait);
            if (bareField) list.Add(DryadWord.Plant);
            if (blighted > 0) list.Add(DryadWord.Burn);
            return list;
        }

        /// <summary>The dryads speak only with something to point at, more readily beside an anima tree.</summary>
        public static GodPaceMath.Trigger DryadsTrigger(int words, int dryads, bool anima) =>
            words <= 0 || dryads <= 0 ? GodPaceMath.Trigger.None : anima ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;

        /// <summary>A frost ahead: the next season's mean below this (degrees) while it is above it now.</summary>
        public const float FrostBelow = 2f;

        public static bool FrostAhead(float nowTemp, float nextSeasonMean) => !float.IsNaN(nowTemp) && !float.IsNaN(nextSeasonMean) && nextSeasonMean < FrostBelow && nowTemp > FrostBelow;

        /// <summary>Her rising walks on: half her risings in the growing season, more the nearer she is drawn; never out of it.</summary>
        public static float WalkChance(bool growing, float near) => growing ? Clamp01(0.45f + 0.35f * GodNearMath.Strength(near)) : 0f;

        /// <summary>She rises this many more times along her line, each this far from the last (cells), an hour apart.</summary>
        public const int WalkSteps = 2;
        public const float WalkSpacingMin = 18f, WalkSpacingMax = 28f;

        /// <summary>Of the cells along her line, about this share spring up green.</summary>
        public const float WalkGreen = 0.7f;

        /// <summary>The oldest tree is looked at again after this many days, a year of felling round it being this many trees, and its ring this many healroot.</summary>
        public const float OldTreeDays = 50f;
        public const int OldTreeFelledFrom = 20, HealrootRing = 6;

        /// <summary>The old tree spared: still standing in spring after a long while of felling round it.</summary>
        public static bool OldTreeSpared(float daysNoted, int felledSince, bool standing, bool spring) =>
            standing && spring && daysNoted >= OldTreeDays && felledSince >= OldTreeFelledFrom;

        // ================= Smoulder =================

        /// <summary>Silver the colony must hold before the voice offers a bargain.</summary>
        public const int BargainMinHeld = 600;

        /// <summary>What the voice asks for: about a seventh of the silver held, in tens, from 150 to 400; none below the line.</summary>
        public static int BargainSilver(int held)
        {
            if (held < BargainMinHeld) return 0;
            float want = Math.Max(150f, Math.Min(400f, held * 0.15f));
            return (int)Math.Round(want / 10f) * 10;
        }

        /// <summary>A bargain is offered only to a colony holding silver with someone mining or sleeping under the mountain, the deep stirring makes it likelier.</summary>
        public static GodPaceMath.Trigger BargainTrigger(int held, bool underRock, float deepStir) =>
            held < BargainMinHeld || !underRock ? GodPaceMath.Trigger.None : deepStir >= 0.3f ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;

        /// <summary>Kept to the letter: at least what was promised went down.</summary>
        public static bool Kept(int thrown, int promised) => promised > 0 && thrown >= promised;

        /// <summary>A kept bargain's gold comes back worth this much of what was thrown.</summary>
        public const float GoldReturn = 1.6f;

        /// <summary>The most cells a vein comes up in.</summary>
        public const int MaxVein = 8;

        /// <summary>Gold cells for what was thrown (each worth <paramref name="perCell"/> in silver): about GoldReturn times it, one at least.</summary>
        public static int GoldCells(int thrown, float perCell) =>
            thrown <= 0 || float.IsNaN(perCell) || perCell <= 0f ? 0 : Math.Max(1, Math.Min(MaxVein, (int)Math.Round(thrown * GoldReturn / perCell)));

        /// <summary>A bent bargain's vein: steel with a skin of gold, worth about what was thrown and no more.</summary>
        public static int SteelCells(int thrown, float perCell) =>
            thrown <= 0 || float.IsNaN(perCell) || perCell <= 0f ? 0 : Math.Max(1, Math.Min(MaxVein, (int)Math.Round(thrown / perCell)));

        /// <summary>The vein shows this many days after the silver went down.</summary>
        public const float VeinDaysMin = 2f, VeinDaysMax = 4f;

        /// <summary>The god's favour (hidden) for a bargain kept, bent, or refused.</summary>
        public const float KeptFavour = 3f, BentFavour = -3f, RefusedFavour = -0.5f;

        /// <summary>
        /// The chance a finished sculpture is named for Aurelian the Ninth: none unless the artist holds him or the colony has
        /// heard of him, more for one who holds him, more for good work, a little more the nearer he is drawn.
        /// </summary>
        public static float NameChance(bool artistHolds, bool known, bool good, float near)
        {
            if (!artistHolds && !known) return 0f;
            return Clamp01((artistHolds ? 0.3f : 0.1f) * (good ? 1.5f : 1f) * GodNearMath.WorldFactor(near));
        }

        /// <summary>The seam of silver the praise brings: this many cells, the next day.</summary>
        public const int NameSeamCells = 3;

        /// <summary>The heat from below comes on the turn before stone may first fall (one warning short of it).</summary>
        public static bool HeatFirst(int warnings, int stoneFrom) => stoneFrom > 1 && warnings == stoneFrom - 1;

        /// <summary>How long the deep floor stays hot, and how much heat it gives each half hour.</summary>
        public const int HeatHours = 4;
        public const float HeatPush = 18f;

        /// <summary>A miner, for the stretching dream: Mining at this level, or a passion for it.</summary>
        public const int MinerFrom = 6;

        /// <summary>Silver hoarded (held) and how long unpaid before the warm dream burns.</summary>
        public const int HoardFrom = 2000;
        public const float UnpaidDays = 60f;

        /// <summary>A burn for a bargain bent (strong), else for a hoard long unpaid; nothing otherwise.</summary>
        public static GodPaceMath.Trigger BurnTrigger(int silver, float daysSincePaid, bool bent) =>
            bent ? GodPaceMath.Trigger.Strong : silver >= HoardFrom && daysSincePaid >= UnpaidDays ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;

        /// <summary>The base chance (before the harm setting and its shared budget) the dream leaves a burn, and how deep.</summary>
        public const float BurnChance = 0.6f, BurnLow = 1.5f, BurnHigh = 4f;

        // ================= Bellwether =================

        /// <summary>Dusk, when he stops at the colony's edge, and dawn, when he takes his road (hours).</summary>
        public const int DuskFrom = 17, DuskTo = 21, DawnFrom = 5, DawnTo = 9;

        /// <summary>His last evening: he stops at the edge only when his stay ends within this many hours.</summary>
        public const float LastEveningHours = 30f;

        /// <summary>The longest he waits at a fence (hours) before he turns back; those this near who see him wait are ashamed.</summary>
        public const float WaitHoursMost = 10f, ShameRange = 25f;

        /// <summary>Where his road is: the line from where he stands, through the colony's middle, to the far edge.</summary>
        public const float RoadStep = 1.5f;

        /// <summary>A child who touched the horn speaks this near to the weather's turn (hours), each this many days apart at most, this often.</summary>
        public const float HornWithinHours = 1.2f, HornGapDays = 6f, HornChance = 0.6f;

        /// <summary>The children who touched the horn the colony remembers at most.</summary>
        public const int HornKept = 8;

        /// <summary>A horn child says so: the weather turns wet within the hour, their own gap past, at its chance.</summary>
        public static bool HornSpeaks(float hoursLeft, bool wet, float daysSince, float roll) =>
            wet && hoursLeft > 0f && hoursLeft <= HornWithinHours && daysSince >= HornGapDays && !float.IsNaN(roll) && roll < HornChance;

        /// <summary>A wounded thrumbo comes only where thrumbos can live and none was killed lately; strongly after he was let pass in peace.</summary>
        public static GodPaceMath.Trigger HurtTrigger(bool canLive, float daysSinceKinKilled, float daysSincePeace)
        {
            if (!canLive || daysSinceKinKilled < KinKilledDays) return GodPaceMath.Trigger.None;
            return daysSincePeace < PeaceDays ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;
        }

        public const float KinKilledDays = 60f, PeaceDays = 60f;

        /// <summary>How long it lies at the edge waiting (hours), and how long the one sent has to reach it.</summary>
        public const float HurtLieHours = 20f, TendWalkHours = 4f;

        /// <summary>The dream after thrumbos pass, its chance.</summary>
        public const float LastChance = 0.7f;

        // ================= Morel =================

        /// <summary>The mourner comes in the days after a death of the colony's, and is likelier after two.</summary>
        public const float MournerAfterDays = 12f;

        public static GodPaceMath.Trigger MournerTrigger(int deathsLately, int graves) =>
            deathsLately <= 0 || graves <= 0 ? GodPaceMath.Trigger.None : deathsLately >= 2 ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary;

        /// <summary>Her ring answers something real: a body left lying (strong), the stores rotting, else only when she is drawn near.</summary>
        public static GodPaceMath.Trigger RingTrigger(bool unburied, bool rotting, float near) =>
            unburied ? GodPaceMath.Trigger.Strong : rotting || near >= RingNear ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;

        public const float RingNear = 0.4f;

        /// <summary>What the ring is round, the body first.</summary>
        public static RingAt RingPlace(bool unburied, bool rotting) => unburied ? RingAt.Body : rotting ? RingAt.Stores : RingAt.Buried;

        /// <summary>A body lying this long (days) unburied is one she rings.</summary>
        public const float UnburiedDays = 1f;

        /// <summary>The warning comes this many hours before her anger.</summary>
        public const float LeadHoursMin = 18f, LeadHoursMax = 30f;

        /// <summary>Someone in a ring hears her at this chance, once in this many days each; a ring is remembered this long.</summary>
        public const float BreathChance = 0.35f, BreathPawnDays = 60f, RingDays = 6f;

        // ================= Ennu =================

        /// <summary>The tests the Guest may ask now: the cold's coat only in the cold, a fire's story only with a fire, the graves only with graves, the cook's name only with a cook.</summary>
        public static List<GuestTest> Tests(bool cold, bool fire, bool graves, bool cook)
        {
            var list = new List<GuestTest>();
            if (fire) list.Add(GuestTest.Story);
            if (cook) list.Add(GuestTest.Cook);
            if (graves) list.Add(GuestTest.Graves);
            if (cold) list.Add(GuestTest.Coat);
            return list;
        }

        /// <summary>The cold that asks for a coat (degrees out of doors).</summary>
        public const float CoatBelow = 0f;

        /// <summary>How the test counts toward the colony's welcome (the stay's tally), and how long the colony has to answer it (hours).</summary>
        public const int TestCredit = 3;
        public const float TestHours = 12f;

        /// <summary>The bowl: on the Open Door night (strong), or in the days after a kind welcome; never otherwise.</summary>
        public static GodPaceMath.Trigger BowlTrigger(bool openDoorNight, float daysSinceKindWelcome) =>
            openDoorNight ? GodPaceMath.Trigger.Strong : daysSinceKindWelcome <= KindWelcomeDays ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;

        public const float KindWelcomeDays = 10f;

        /// <summary>A night's chance of the bowl: every Open Door night, a quarter of the nights after a kind welcome.</summary>
        public static float BowlChance(GodPaceMath.Trigger why) => why == GodPaceMath.Trigger.Strong ? 1f : why == GodPaceMath.Trigger.Ordinary ? 0.25f : 0f;

        /// <summary>The evening's hours for the bowl, the tales and the fire.</summary>
        public const int EveningFrom = 18, EveningTo = 22;

        /// <summary>The tales a stay at most, and an evening's chance of one.</summary>
        public const int TalesAStay = 2;
        public const float TalesChance = 0.5f;

        /// <summary>How long visitors linger with the Guest's kindest after it went (days), and an hour's chance of it while visitors are here.</summary>
        public const float KindestDays = 15f, KindestChance = 0.35f;

        // ================= Pip =================

        /// <summary>A switch-off's chance to be refused: none unless Pip is known or held, a quarter of its share as felt, half near.</summary>
        public static float FlickChance(float daysSince, bool known, float near)
        {
            if (!known) return 0f;
            var p = PaceOf(OwnHappening.PipWontStop);
            return GodPaceMath.Ramp(daysSince, p.gap, p.ramp) * (0.25f + 0.25f * GodNearMath.Strength(near));
        }

        /// <summary>How long the machine runs on after it was switched off (ticks: an hour).</summary>
        public const int WontStopTicks = 2500;

        /// <summary>The console calls only for a reason: an old terminal on the land to answer it (strong), the console used lately, or Pip drawn near.</summary>
        public static GodPaceMath.Trigger StarsTrigger(bool console, float daysSinceUsed, bool terminal, float near)
        {
            if (!console) return GodPaceMath.Trigger.None;
            if (terminal) return GodPaceMath.Trigger.Strong;
            return daysSinceUsed <= ConsoleUsedDays || near >= StarsNear ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;
        }

        public const float ConsoleUsedDays = 3f, StarsNear = 0.3f;

        /// <summary>The small hours the console comes on in, and the morning the message is read.</summary>
        public const int NightFrom = 1, NightTo = 4, MorningFrom = 6, MorningTo = 11;

        /// <summary>
        /// Someone stands across a turret's line: within <see cref="LineWidth"/> cells of the line from the turret (t) to its
        /// target (g), between them (past a tenth of the way, short of nine tenths).
        /// </summary>
        public static bool CrossesLine(float tx, float tz, float gx, float gz, float px, float pz)
        {
            float dx = gx - tx, dz = gz - tz;
            float len2 = dx * dx + dz * dz;
            if (len2 < 1f) return false;
            float t = ((px - tx) * dx + (pz - tz) * dz) / len2;
            if (t <= 0.1f || t >= 0.9f) return false;
            float cx = tx + t * dx - px, cz = tz + t * dz - pz;
            return cx * cx + cz * cz <= LineWidth * LineWidth;
        }

        public const float LineWidth = 1.1f;

        /// <summary>A crossing's chance to be held for, and the breath it holds (ticks).</summary>
        public const float TurretChance = 0.5f;
        public const int HoldTicks = 90;

        // ================= Cirrus =================

        /// <summary>A colony watches the sky: looked up (a telescope, skygazing) at least this often in the last days, or kept its festival lately.</summary>
        public const float WatchDays = 10f, FestivalDays = 15f;
        public const int WatchesFrom = 2;

        public static bool Watches(int watches, float daysSinceFestival) => watches >= WatchesFrom || daysSinceFestival <= FestivalDays;

        /// <summary>The wrong-way star's dream, its chance; the pods come the next day between these hours.</summary>
        public const float FallChance = 0.6f;
        public const int PodFrom = 10, PodTo = 16;

        /// <summary>The coldest night: colder than any in the days kept (at least ten of them), and below this.</summary>
        public const float AuroraBelow = -5f;
        public const int ColdDaysKept = 10;

        public static bool Coldest(float tonight, float coldestBefore, int daysKept) =>
            !float.IsNaN(tonight) && daysKept >= ColdDaysKept && tonight <= AuroraBelow && tonight < coldestBefore;

        /// <summary>The aurora's chance on the coldest night with someone out of doors, and how long it lasts (hours).</summary>
        public const float AuroraChance = 0.4f;
        public const int AuroraHours = 5;

        /// <summary>Dry thunder only after a long clear spell with nothing of the sky's lately.</summary>
        public static GodPaceMath.Trigger ThunderTrigger(int dryDays, float daysSinceSky) =>
            dryDays >= ThunderDryDays && daysSinceSky >= ThunderQuietDays ? GodPaceMath.Trigger.Ordinary : GodPaceMath.Trigger.None;

        public const int ThunderDryDays = 6;
        public const float ThunderQuietDays = 20f;

        /// <summary>The far thunder's flashes and how long it rolls (ticks).</summary>
        public const int ThunderFlashes = 6, ThunderTicks = 5000;

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : Math.Max(0f, Math.Min(1f, v));
    }
}
