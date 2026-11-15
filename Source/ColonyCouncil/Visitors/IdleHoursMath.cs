using System;

namespace ColonyCouncil
{
    /// <summary>What a visitor may do with an idle hour of their own (Visitors/IdleHours.cs): never work for the colony.</summary>
    public enum IdleDoing
    {
        None,
        /// <summary>A talk with one of their own, the two standing together.</summary>
        Chat,
        /// <summary>A while beside one of the colony's people at their leisure (Rimfolk's outsider talk may start).</summary>
        Company,
        /// <summary>Sitting by a lit fire, in the ring round it.</summary>
        Fire,
        /// <summary>In the cold: right by a lit fire, warming their hands.</summary>
        Warm,
        /// <summary>A seat of the colony's (a bench, a chair) nobody of the colony is using or about to.</summary>
        Seat,
        /// <summary>Out of the rain or snow, under a roof near their spot.</summary>
        Shelter,
        /// <summary>A bite from their own pack at a meal hour.</summary>
        Snack,
        /// <summary>A nap in the shade of a tree, at midday in fair weather.</summary>
        Nap,
        /// <summary>One of the colony's games while nobody of the colony wants it (horseshoes, chess, cards, the screen).</summary>
        Game,
        /// <summary>The colony's instrument while nobody of the colony wants it.</summary>
        Music,
        /// <summary>The colony's telescope on a clear night.</summary>
        Stars,
        /// <summary>A look at the colony's art.</summary>
        Art,
        /// <summary>A prayer at a shrine of their god, or an altar of their faith.</summary>
        Pray,
        /// <summary>Seeing to one of their own animals.</summary>
        Animal,
        /// <summary>A child running about at play.</summary>
        Play,
        /// <summary>A smoke or a drink of their own, from their own pack.</summary>
        OwnDrug,
        /// <summary>A pipe of their own leaf.</summary>
        Pipe,
        /// <summary>Whittling a bit of wood of their own.</summary>
        Carve,
        /// <summary>A book: one of their own from their pack, or one of the colony's where it lies, put back where it was after.</summary>
        Read,
        /// <summary>A walk round the colony's paths, ending at a garden, a grave or a view.</summary>
        Walk,
        /// <summary>One of the colony's pets petted, or a dog played fetch with; never tamed or trained.</summary>
        Pet,
        /// <summary>A letter, or their journal, written at one of the colony's tables.</summary>
        Write,
        /// <summary>Training at one of the colony's training targets or dummies, or shadow sparring in the open.</summary>
        Train,
        /// <summary>Archery practice with their own bow at the colony's archery target.</summary>
        Archery,
        /// <summary>Fishing at the water's edge, the catch their own.</summary>
        Fish,
        /// <summary>Mending a worn piece of their own clothes.</summary>
        Mend,
        /// <summary>Seeing to their own weapon: a blade sharpened, a bow oiled and restrung, a gun cleaned.</summary>
        Sharpen,
        /// <summary>A game of their own with one of their own: cards, dice, or a game of stones.</summary>
        Cards,
        /// <summary>A song of their own, sung quietly to themselves.</summary>
        Sing,
        /// <summary>A night's sleep on the ground near their spot, when tired.</summary>
        Sleep
    }

    /// <summary>The kind of group a visitor is with, for their idle hours.</summary>
    public enum IdleGroup
    {
        None,
        /// <summary>The game's visitors (and Rimfolk's errands that come the game's way).</summary>
        GameVisit,
        /// <summary>A trade caravan's people.</summary>
        Caravan,
        /// <summary>Rimfolk's own visits at their spot, out of doors (Visitors/VisitorLord.cs).</summary>
        RimfolkVisit,
        /// <summary>Rimfolk's envoys staying the season.</summary>
        Envoys,
        /// <summary>Hospitality's guests: its own recreation first.</summary>
        Hospitality
    }

    /// <summary>What someone is in their group: the trader keeps near the trade, a caravan's guard stays on watch.</summary>
    public enum IdleRole
    {
        Plain,
        Trader,
        Guard
    }

    /// <summary>The colony's joy things by how they are used: watched or thrown at, sat beside, stood at, played.</summary>
    public enum JoyShape
    {
        Watch,
        Adjacent,
        Cell,
        Instrument
    }

    /// <summary>What a game throws, as the game's own playing shows it.</summary>
    public enum JoyThrow
    {
        None,
        Horseshoe,
        Stone
    }

    /// <summary>A visitor's smoke or drink of their own, by what it is.</summary>
    public enum OwnDrugKind
    {
        None,
        Smoke,
        Drink
    }

    /// <summary>One visitor's idle moment as the rules see it (IdleHours.LookFor fills it).</summary>
    public sealed class IdleFacts
    {
        public IdleGroup group;
        public IdleRole role;
        /// <summary>Their people's age: 0 tribal, 1 medieval, 2 industrial, 3 spacer, 4 beyond (<see cref="IdleHoursMath.AgeOf"/>).</summary>
        public int age = 2;
        public bool child, titled, devout, scoffer;
        /// <summary>The calling of one of Rimfolk's peoples ("herders", "pilgrims"), or null.</summary>
        public string calling;
        public int hour = 12;
        /// <summary>The warmth where they stand.</summary>
        public float temperature = 20f;
        /// <summary>Rain or snow falling.</summary>
        public bool rain;
        /// <summary>Standing under the open sky.</summary>
        public bool underSky;
        public bool clearSky = true;
        public float food = 1f, rest = 1f;
        public bool ownFood, ownDrug, drugsAllowed = true;
        public bool canTalk = true, canHandle = true, canSee = true;
        // What is about, by distance only: the way there is checked once one is picked.
        public bool fire, seat, roof, shade, game, music, stars, art, shrine, animal, companion, colonist;
        /// <summary>One of the colony's books where it lies (or in its bookcase), and one of their own in their pack.</summary>
        public bool book, ownBook;
        /// <summary>Somewhere to walk to: a garden, a grave, a view, the colony's paths.</summary>
        public bool walk;
        /// <summary>One of the colony's pets, calm and idle.</summary>
        public bool pet;
        /// <summary>One of the colony's tables with a free seat at it, to write at.</summary>
        public bool table;
        /// <summary>One of the colony's training targets or dummies, free; open ground to spar on in fair weather.</summary>
        public bool trainTarget, openGround;
        /// <summary>The colony's archery target, free, and a bow of their own in hand.</summary>
        public bool archeryTarget, bow;
        /// <summary>Water with fish in it at the edge of their reach, off the colony's own fishing spots; their faith allows fishing.</summary>
        public bool water, fishingAllowed = true;
        /// <summary>A worn piece of their own clothes to mend.</summary>
        public bool worn;
        /// <summary>Their own weapon, by what seeing to it is (<see cref="IdleHoursMath.WeaponKind"/>): 0 none, 1 a blade, 2 a bow, 3 a gun.</summary>
        public int weaponKind;
    }

    /// <summary>
    /// The plain rules for a visitor's idle hours (the player's request, 14 November 2026: "guests should spend more of their
    /// time doing things. i don't want them to generate value for me"): which things of their own fit whom, by their people's
    /// age and ways, the hour and the weather, and what is free of the colony; how long each lasts, how far they go for it,
    /// and when they look again. No game types, so the tests can check them.
    /// </summary>
    public static class IdleHoursMath
    {
        public static readonly IdleDoing[] All = (IdleDoing[])Enum.GetValues(typeof(IdleDoing));

        // ---------------- The hours ----------------

        public static bool Night(int hour) => hour >= 23 || hour < 5;

        public static bool Evening(int hour) => hour >= 17 && hour < 23;

        public static bool Day(int hour) => hour >= 7 && hour < 21;

        /// <summary>Breakfast, the midday meal, supper.</summary>
        public static bool MealHour(int hour) => hour == 7 || hour == 8 || hour == 12 || hour == 13 || hour == 18 || hour == 19;

        /// <summary>Morning and evening prayers.</summary>
        public static bool PrayerHour(int hour) => hour >= 5 && hour < 9 || hour >= 17 && hour < 21;

        /// <summary>The hours for the colony's games and instruments: from the morning to mid-evening, never when people sleep.</summary>
        public static bool GameHour(int hour) => hour >= 8 && hour < 22;

        /// <summary>The telescope's hours: the dark.</summary>
        public static bool StarHour(int hour) => hour >= 20 || hour < 4;

        /// <summary>The midday nap's hours.</summary>
        public static bool NapHour(int hour) => hour >= 12 && hour < 16;

        // ---------------- The weather ----------------

        /// <summary>Cold enough to warm one's hands at a fire, however they're dressed.</summary>
        public const float ColdBelow = 5f;

        /// <summary>Chilly: a fire draws people.</summary>
        public const float ChillyBelow = 12f;

        public static bool Cold(float temperature) => temperature < ColdBelow;

        public static bool Chilly(float temperature) => temperature < ChillyBelow;

        /// <summary>Warm enough and not too hot to doze under a tree.</summary>
        public static bool NapWeather(float temperature, bool rain) => !rain && temperature >= 12f && temperature <= 32f;

        /// <summary>The game's level under which someone lying down falls asleep.</summary>
        public const float SleepsBelow = 0.74f;

        /// <summary>Peckish: a bite from the pack at a meal hour.</summary>
        public const float SnackBelow = 0.75f;

        // ---------------- Ages ----------------

        /// <summary>
        /// The age of a tech level, as the game numbers them (0 undefined, 1 animal, 2 neolithic, 3 medieval, 4 industrial,
        /// 5 spacer, 6 ultra, 7 archotech): 0 tribal (and none), 1 medieval, 2 industrial, 3 spacer, 4 beyond.
        /// </summary>
        public static int AgeOf(int techLevel)
        {
            if (techLevel <= 2) return 0;
            if (techLevel >= 6) return 4;
            return techLevel - 2;
        }

        /// <summary>A people's age: its tech level's, an industrial one for someone of no known people.</summary>
        public static int TheirAge(int techLevel) => techLevel <= 0 ? 2 : AgeOf(techLevel);

        /// <summary>
        /// The age of one of the colony's things: the latest of its own tech level, the research it needs, and what it is
        /// made of or runs on (components or power make it industrial at least, spacer parts spacer).
        /// </summary>
        public static int ThingAge(int defTech, int researchTech, bool industrialParts, bool spacerParts, bool powered)
        {
            int age = Math.Max(AgeOf(defTech), AgeOf(researchTech));
            if (industrialParts || powered) age = Math.Max(age, 2);
            if (spacerParts) age = Math.Max(age, 3);
            return age;
        }

        /// <summary>A thing fits a people's ways when it's of their age or older: a tribal visitor plays horseshoes and Ur, not chess at a spacer table.</summary>
        public static bool Fits(int theirAge, int thingAge) => thingAge <= theirAge;

        // ---------------- The colony's joy things ----------------

        /// <summary>
        /// A joy giver of the game's or a mod's that visitors leave alone: bathing and swimming (the colony's water and
        /// heat), shooting, archery, darts, sparring and training (the game shows the throw or the blow, which a visitor's
        /// stint doesn't), a stage that needs a performer, a washing machine, the market's stalls and auctions, and
        /// Rimfolk's own habits.
        /// </summary>
        public static bool Excluded(string giverDef, string joyKind, string thingDef)
        {
            foreach (var s in new[] { giverDef, joyKind, thingDef })
            {
                if (string.IsNullOrEmpty(s)) continue;
                foreach (var word in ExcludedWords)
                    if (s.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static readonly string[] ExcludedWords =
        {
            "Hydrotherapy", "HotTub", "Sauna", "Bath", "Therma", "PoolTile", "SwimmingPool", "Swim", "Washing", "Shoot", "Archery", "Arrow", "Dart",
            "MartialArts", "Dummy", "Punching", "Strength", "Stage", "Performance", "RimMarket", "Auction", "Council_", "Rimfolk_", "Lounger", "Sunbath",
            "Therapy", "Corpse"
        };

        /// <summary>
        /// Played or listened to as music: one of the game's instruments (the harp, the lyre, the piano of Royalty), or a
        /// thing of high culture played at its own spot (Vanilla Furniture Expanded's piano). A radio or a screen of high
        /// culture is watched and listened to like a screen, so it counts as watching.
        /// </summary>
        public static bool IsMusic(bool instrument, string joyKind, bool watch) => instrument || joyKind == "HighCulture" && !watch;

        /// <summary>
        /// A fire to sit round: the game's fires (a campfire, a brazier, a hearth, a large fire) but not a light (a torch, a
        /// lamp, a candle), nothing that runs on or makes power (a wood-fired generator), no stove or smithy (a workbench
        /// bigger than a campfire), and nothing bigger than four cells.
        /// </summary>
        public static bool SitByFire(bool isFire, string defName, bool power, bool workTable, int cells)
        {
            if (!isFire || power || cells > 4 || workTable && cells > 1) return false;
            if (string.IsNullOrEmpty(defName)) return true;
            foreach (var w in NotFireWords)
                if (defName.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return true;
        }

        private static readonly string[] NotFireWords = { "Torch", "Lamp", "Candle", "Sconce", "Lantern", "GravField" };

        /// <summary>How the joy thing is used, by the game's joy giver for it.</summary>
        public static JoyShape ShapeOf(bool instrument, bool watch, bool sitAdjacent, bool interactionCellGiver, bool hasInteractionCell)
        {
            if (instrument) return JoyShape.Instrument;
            if (watch) return JoyShape.Watch;
            if (sitAdjacent) return JoyShape.Adjacent;
            if (interactionCellGiver || hasInteractionCell) return JoyShape.Cell;
            return JoyShape.Adjacent;
        }

        /// <summary>What the game's own playing of it throws, by its job driver.</summary>
        public static JoyThrow ThrowOf(string driverClass)
        {
            if (driverClass == null) return JoyThrow.None;
            if (driverClass.EndsWith("JobDriver_PlayHorseshoes")) return JoyThrow.Horseshoe;
            if (driverClass.EndsWith("JobDriver_PlayHoopstone")) return JoyThrow.Stone;
            return JoyThrow.None;
        }

        /// <summary>The telescope is for the stars, at night; the rest are games.</summary>
        public static bool IsTelescope(string joyKind, string thingDef) =>
            joyKind == "Telescope" || !string.IsNullOrEmpty(thingDef) && thingDef.IndexOf("Telescope", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// A joy thing is free for a visitor: nobody of the colony has it or is on their way to it, the player hasn't
        /// forbidden or switched it off, it has its power, and there's room for one more of the visitors at it.
        /// </summary>
        public static bool Free(bool colonyHasIt, bool forbidden, bool switchedOff, bool unpowered, int visitorsAtIt, int participants) =>
            !colonyHasIt && !forbidden && !switchedOff && !unpowered && visitorsAtIt < Math.Max(1, participants);

        // ---------------- What they carry ----------------

        private static readonly string[] SmokeWords = { "Joint", "Smoke", "Cigar", "Cigarette", "Pipe", "Tobacco", "Hookah" };

        private static readonly string[] DrinkWords =
            { "Beer", "Ale", "Wine", "Mead", "Cider", "Tea", "Coffee", "Vodka", "Whisk", "Rum", "Gin", "Sake", "Liquor", "Brandy", "Spirit", "Moonshine", "Kvass", "Juice" };

        /// <summary>
        /// A social drug of their own is a smoke (the game's joint smoke, or a smoke by its name) or a drink (a fluid, or a drink
        /// by its name); anything else (a pill, a fruit) isn't had idly.
        /// </summary>
        public static OwnDrugKind DrugKindOf(string defName, string ingestEffect, bool fluid)
        {
            if (!string.IsNullOrEmpty(ingestEffect) && ingestEffect.IndexOf("Smoke", StringComparison.OrdinalIgnoreCase) >= 0) return OwnDrugKind.Smoke;
            if (!string.IsNullOrEmpty(defName))
                foreach (var w in SmokeWords)
                    if (defName.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return OwnDrugKind.Smoke;
            if (fluid) return OwnDrugKind.Drink;
            if (!string.IsNullOrEmpty(defName))
                foreach (var w in DrinkWords)
                    if (defName.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return OwnDrugKind.Drink;
            return OwnDrugKind.None;
        }


        // ---------------- Their own things ----------------

        /// <summary>
        /// What seeing to their own weapon is (<see cref="IdleFacts.weaponKind"/>): a blade sharpened (1), a bow oiled and
        /// restrung (2), a gun cleaned (3); a club or a fist has nothing to see to (0).
        /// </summary>
        public static int WeaponKind(bool has, bool melee, bool sharp, bool arrows)
        {
            if (!has) return 0;
            if (melee) return sharp ? 1 : 0;
            return arrows ? 2 : 3;
        }

        /// <summary>A bow or a crossbow: it shoots arrows or bolts of the old kind, or is called a bow.</summary>
        public static bool IsBow(string weaponDef, string projectileDef)
        {
            if (!string.IsNullOrEmpty(projectileDef) && projectileDef.IndexOf("Arrow", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return !string.IsNullOrEmpty(weaponDef) && weaponDef.IndexOf("Bow", StringComparison.OrdinalIgnoreCase) >= 0
                                                    && weaponDef.IndexOf("Elbow", StringComparison.OrdinalIgnoreCase) < 0;
        }

        /// <summary>A piece of clothing worn enough to mend: below four fifths of its hit points.</summary>
        public static bool Worn(int hitPoints, int maxHitPoints) => maxHitPoints > 0 && hitPoints < maxHitPoints * 0.8f;

        /// <summary>What a stint of mending gives back: a tenth of the piece's hit points, at least one, never past its full.</summary>
        public static int MendBy(int hitPoints, int maxHitPoints) => Math.Max(0, Math.Min(maxHitPoints - hitPoints, Math.Max(1, (int)Math.Round(maxHitPoints * 0.1f))));

        /// <summary>
        /// The colony's training things in the player's mods (docs/player-mods, active): a target or a dummy to strike (Misc.
        /// Training's martial arts target, Vanilla Factions Expanded - Medieval 2's training dummy), and a target to shoot
        /// arrows at (Misc. Training's archery target, the same Medieval 2's). By name, so a missing mod costs nothing.
        /// </summary>
        public static readonly string[] SparTargets = { "MartialArtsTarget", "VFEM2_TrainingDummy" };

        public static readonly string[] ArcheryTargets = { "ArcheryTarget", "VFEM2_ArcheryTarget" };

        public static bool IsSparTarget(string defName) => Array.IndexOf(SparTargets, defName) >= 0;

        public static bool IsArcheryTarget(string defName) => Array.IndexOf(ArcheryTargets, defName) >= 0;

        /// <summary>
        /// One of the colony's animals a visitor may pet: a small one (a dog, a cat, a guinea pig; not a horse or a yak), a pet
        /// by the game's own measure (its petness) or one that nuzzles. Tame, it doesn't turn on them, predator or not.
        /// </summary>
        public static bool IsPet(float petness, float nuzzleMtbHours, float bodySize) =>
            bodySize <= 1.3f && (petness >= 0.3f || nuzzleMtbHours > 0f);

        private static readonly string[] FetchWords =
            { "Dog", "Husky", "Retriever", "Labrador", "Shepherd", "Hound", "Collie", "Terrier", "Corgi", "Poodle", "Mastiff", "Spaniel", "Alaunt", "Wolf" };

        /// <summary>A dog (or a tame wolf), by its kind's name: one that runs after a thrown stick.</summary>
        public static bool PlaysFetch(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return false;
            foreach (var w in FetchWords)
                if (defName.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>A book they don't read idly: one that can break the reader's mind (a dark tome).</summary>
        public static bool BookOk(bool readable, float mentalBreakChancePerHour) => readable && mentalBreakChancePerHour <= 0f;

        // ---------------- Who may do what ----------------

        /// <summary>
        /// Hospitality's guests get Rimfolk's doings too, alongside its own recreation (which comes first: Rimfolk fills only
        /// the moments Hospitality would have them stand or wander). Their nights are Hospitality's, in its guest beds.
        /// </summary>
        public static bool ForHospitality(IdleDoing d) => d != IdleDoing.None && d != IdleDoing.Sleep;

        /// <summary>Why this doing isn't for them at all, whatever is about, or null: their group, their role, a child or a titled one.</summary>
        public static string NotFor(IdleDoing d, IdleFacts f)
        {
            if (d == IdleDoing.None) return "nothing";
            if (f.group == IdleGroup.None) return "no group";
            if (f.group == IdleGroup.Hospitality && !ForHospitality(d)) return "Hospitality's own";
            bool grown = !f.child;
            switch (d)
            {
                case IdleDoing.Chat:
                case IdleDoing.Company:
                    return f.canTalk ? null : "can't talk";
                case IdleDoing.Nap:
                    if (f.role != IdleRole.Plain) return "on duty";
                    return f.titled ? "a titled one doesn't doze on the ground" : null;
                case IdleDoing.Game:
                case IdleDoing.Music:
                case IdleDoing.Stars:
                    if (!grown) return "a child";
                    if (f.role == IdleRole.Guard) return "on watch";
                    return d == IdleDoing.Music && !f.canHandle ? "can't play" : null;
                case IdleDoing.Art:
                    return f.canSee ? null : "can't see";
                case IdleDoing.Pray:
                    if (!grown) return "a child";
                    return f.scoffer ? "a scoffer" : null;
                case IdleDoing.Animal:
                    return f.canHandle ? null : "can't handle an animal";
                case IdleDoing.Play:
                    return f.child ? null : "grown";
                case IdleDoing.OwnDrug:
                    if (!grown) return "a child";
                    if (f.role != IdleRole.Plain) return "on duty";
                    return f.drugsAllowed ? null : "their faith forbids it";
                case IdleDoing.Pipe:
                    if (!grown) return "a child";
                    return f.drugsAllowed ? null : "their faith forbids it";
                case IdleDoing.Carve:
                    if (!grown) return "a child";
                    if (f.titled) return "a titled one";
                    return f.canHandle ? null : "can't handle a knife";
                case IdleDoing.Read:
                    if (!grown) return "a child";
                    if (f.role == IdleRole.Guard) return "on watch";
                    return f.canSee ? null : "can't see";
                case IdleDoing.Walk:
                    if (!grown) return "a child (they run about instead)";
                    return f.role != IdleRole.Plain ? "on duty" : null;
                case IdleDoing.Pet:
                    if (f.role == IdleRole.Guard) return "on watch";
                    return f.canHandle ? null : "can't use their hands";
                case IdleDoing.Write:
                    if (!grown) return "a child";
                    if (f.age <= 0) return "not their people's way (a tribal people keeps no letters)";
                    if (f.role == IdleRole.Guard) return "on watch";
                    return f.canHandle && f.canSee ? null : "can't write";
                case IdleDoing.Train:
                case IdleDoing.Archery:
                    if (!grown) return "a child";
                    if (f.role != IdleRole.Plain) return "on duty";
                    if (!f.canHandle) return "can't use their hands";
                    return d == IdleDoing.Archery && !f.canSee ? "can't see" : null;
                case IdleDoing.Fish:
                    if (!grown) return "a child";
                    if (f.role != IdleRole.Plain) return "on duty";
                    if (!f.fishingAllowed) return "their faith forbids fishing";
                    return f.canHandle ? null : "can't use their hands";
                case IdleDoing.Mend:
                    if (!grown) return "a child";
                    if (f.titled) return "a titled one";
                    return f.canHandle ? null : "can't use their hands";
                case IdleDoing.Sharpen:
                    if (!grown) return "a child";
                    return f.canHandle ? null : "can't use their hands";
                case IdleDoing.Cards:
                    if (!grown) return "a child";
                    if (f.role == IdleRole.Guard) return "on watch";
                    return f.canHandle ? null : "can't use their hands";
                case IdleDoing.Sing:
                    if (f.role == IdleRole.Guard) return "on watch";
                    return f.canTalk ? null : "can't sing";
                case IdleDoing.Sleep:
                    // The game's visitors and a caravan's people sleep on the ground; Rimfolk's visits, envoys and Hospitality's
                    // guests have beds or a bedtime of their own.
                    if (f.group != IdleGroup.GameVisit && f.group != IdleGroup.Caravan) return "their own beds or bedtime";
                    return f.role != IdleRole.Plain ? "on duty (the trader stays up for the trade, a guard on watch)" : null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// How much each idle doing fits them now (0: not now), by what is about, the hour, the weather, their people's age
        /// and calling and who they are. Indexed by <see cref="IdleDoing"/>.
        /// </summary>
        public static float[] Weights(IdleFacts f)
        {
            var w = new float[All.Length];
            foreach (var d in All)
                if (NotFor(d, f) == null)
                    w[(int)d] = Weight(d, f);
            return w;
        }

        public static float Weight(IdleDoing d, IdleFacts f)
        {
            int h = f.hour;
            bool night = Night(h);
            bool fair = !f.rain && !Cold(f.temperature) && f.temperature <= 35f;
            float x;
            switch (d)
            {
                case IdleDoing.Chat:
                    if (!f.companion) return 0f;
                    x = night ? 0.3f : 1.2f;
                    break;
                case IdleDoing.Company:
                    if (!f.colonist || night) return 0f;
                    x = f.child ? 0.3f : 0.8f;
                    break;
                case IdleDoing.Fire:
                    if (!f.fire) return 0f;
                    x = 0.6f;
                    if (Evening(h)) x *= 3f;
                    if (Chilly(f.temperature)) x *= 1.5f;
                    if (night) x *= 0.5f;
                    break;
                case IdleDoing.Warm:
                    return f.fire && Cold(f.temperature) ? 2.5f : 0f;
                case IdleDoing.Seat:
                    if (!f.seat) return 0f;
                    x = night ? 0.2f : 0.9f;
                    break;
                case IdleDoing.Shelter:
                    return f.rain && f.underSky && f.roof ? 4f : 0f;
                case IdleDoing.Snack:
                    return f.ownFood && MealHour(h) && f.food < SnackBelow ? 2f : 0f;
                case IdleDoing.Nap:
                    return f.shade && NapHour(h) && NapWeather(f.temperature, f.rain) && f.rest < SleepsBelow ? 1.5f : 0f;
                case IdleDoing.Game:
                    if (!f.game || !GameHour(h)) return 0f;
                    x = 1.2f;
                    break;
                case IdleDoing.Music:
                    if (!f.music || !GameHour(h) || h >= 21) return 0f;
                    x = 0.5f;
                    break;
                case IdleDoing.Stars:
                    return f.stars && StarHour(h) && f.clearSky && !f.rain ? 1f : 0f;
                case IdleDoing.Art:
                    if (!f.art || !Day(h)) return 0f;
                    x = 0.7f;
                    if (f.titled) x *= 1.5f;
                    break;
                case IdleDoing.Pray:
                    if (!f.shrine || night) return 0f;
                    x = PrayerHour(h) ? 1.2f : 0.3f;
                    if (f.devout) x *= 1.8f;
                    break;
                case IdleDoing.Animal:
                    if (!f.animal || night || h < 6) return 0f;
                    x = 0.9f;
                    if (f.child) x *= 1.5f;
                    break;
                case IdleDoing.Play:
                    return Day(h) && h < 20 ? 3f : 0f;
                case IdleDoing.OwnDrug:
                    if (!f.ownDrug || night || h < 10) return 0f;
                    x = Evening(h) ? 1.2f : 0.3f;
                    break;
                case IdleDoing.Pipe:
                    if (night || h < 8) return 0f;
                    x = f.age <= 2 ? 0.4f : 0.15f;
                    if (Evening(h)) x *= 1.5f;
                    break;
                case IdleDoing.Carve:
                    if (night) return 0f;
                    x = f.age <= 1 ? 0.8f : f.age == 2 ? 0.3f : 0.1f;
                    break;
                case IdleDoing.Read:
                    // A tribal people reads only what they carry themselves; the colony's books are for the lettered ages.
                    if (!(f.ownBook || f.book && f.age >= 1) || night || h < 6) return 0f;
                    x = 0.9f;
                    if (f.ownBook) x *= 1.3f;
                    if (Evening(h)) x *= 1.2f;
                    break;
                case IdleDoing.Walk:
                    if (!f.walk || !Day(h) || h >= 20 || !fair) return 0f;
                    x = 0.7f;
                    if (h < 10 || h >= 17) x *= 1.4f;
                    break;
                case IdleDoing.Pet:
                    if (!f.pet || night || h < 6) return 0f;
                    x = f.child ? 1.6f : 0.7f;
                    break;
                case IdleDoing.Write:
                    if (!f.table || night || h < 7) return 0f;
                    x = 0.5f;
                    if (Evening(h)) x *= 1.4f;
                    break;
                case IdleDoing.Train:
                    if (!Day(h) || h >= 19) return 0f;
                    if (f.trainTarget) x = 0.8f;
                    else if (f.openGround && fair) x = 0.2f;
                    else return 0f;
                    break;
                case IdleDoing.Archery:
                    if (!f.bow || !f.archeryTarget || !Day(h) || h >= 19 || f.rain) return 0f;
                    x = 1.4f;
                    break;
                case IdleDoing.Fish:
                    if (!f.water || h < 5 || h >= 21) return 0f;
                    x = 0.8f;
                    if (h < 9) x *= 1.4f;
                    break;
                case IdleDoing.Mend:
                    if (!f.worn || night) return 0f;
                    x = 0.6f;
                    if (Evening(h)) x *= 1.3f;
                    break;
                case IdleDoing.Sharpen:
                    if (f.weaponKind == 0 || night) return 0f;
                    x = 0.35f;
                    if (Evening(h)) x *= 1.3f;
                    break;
                case IdleDoing.Cards:
                    if (!f.companion || night || h < 9) return 0f;
                    x = 0.7f;
                    if (Evening(h)) x *= 1.8f;
                    break;
                case IdleDoing.Sing:
                    if (night || h < 9) return 0f;
                    x = Evening(h) ? 0.4f : 0.12f;
                    break;
                case IdleDoing.Sleep:
                    // The night is for sleep: a tired visitor lies down near their spot rather than standing about till they drop.
                    return (night || h >= 22) && f.rest < SleepsBelow ? 3f : 0f;
                default:
                    return 0f;
            }
            return x * CallingFactor(d, f.calling);
        }

        /// <summary>What a people's calling draws them to: herders and hunters their animals, pilgrims and seers their prayers, scholars the stars, the art and books.</summary>
        public static float CallingFactor(IdleDoing d, string calling)
        {
            switch (calling)
            {
                case "herders":
                case "tamers":
                    return d == IdleDoing.Animal ? 2f : d == IdleDoing.Pet ? 1.5f : 1f;
                case "hunters":
                    return d == IdleDoing.Animal || d == IdleDoing.Archery ? 2f : d == IdleDoing.Train || d == IdleDoing.Sharpen ? 1.5f : d == IdleDoing.Fish ? 1.3f : 1f;
                case "pilgrims":
                case "seers":
                case "sextons":
                case "listeners":
                case "grovekeepers":
                    return d == IdleDoing.Pray || d == IdleDoing.Sing ? 2f : d == IdleDoing.Music || d == IdleDoing.Walk ? 1.3f : 1f;
                case "scholars":
                case "editors":
                    return d == IdleDoing.Stars || d == IdleDoing.Art || d == IdleDoing.Read || d == IdleDoing.Write ? 2f : d == IdleDoing.Game ? 1.3f : 1f;
                case "artisans":
                    return d == IdleDoing.Art ? 2f : d == IdleDoing.Carve || d == IdleDoing.Mend ? 1.5f : 1f;
                case "traders":
                case "brokers":
                    return d == IdleDoing.Game || d == IdleDoing.Company || d == IdleDoing.Cards ? 1.5f : d == IdleDoing.Write ? 1.3f : 1f;
                case "mercenaries":
                    return d == IdleDoing.Game || d == IdleDoing.OwnDrug || d == IdleDoing.Cards ? 1.5f : d == IdleDoing.Train || d == IdleDoing.Sharpen ? 2f : 1f;
                case "healers":
                    return d == IdleDoing.Company ? 1.3f : d == IdleDoing.Read ? 1.3f : 1f;
                case "fishers":
                    return d == IdleDoing.Fish ? 3f : d == IdleDoing.Mend ? 1.3f : 1f;
                default:
                    return 1f;
            }
        }

        /// <summary>The doing that comes up for a roll in [0, 1) among the weights, or None when nothing fits.</summary>
        public static IdleDoing Pick(float[] weights, float roll)
        {
            float sum = 0f;
            foreach (var x in weights) sum += Math.Max(0f, x);
            if (sum <= 0f) return IdleDoing.None;
            float at = Math.Max(0f, Math.Min(0.999999f, roll)) * sum;
            for (int i = 0; i < weights.Length; i++)
            {
                float x = Math.Max(0f, weights[i]);
                if (x <= 0f) continue;
                if (at < x) return (IdleDoing)i;
                at -= x;
            }
            for (int i = weights.Length - 1; i >= 0; i--)
                if (weights[i] > 0f) return (IdleDoing)i;
            return IdleDoing.None;
        }

        /// <summary>
        /// How often an idle moment becomes something of their own: nine in ten by day, six in ten at night (quieter things
        /// then: the fire, a seat, sleep); Hospitality's guests nearly as often, its own recreation having come first.
        /// </summary>
        public static float FillChance(IdleGroup g, int hour)
        {
            if (g == IdleGroup.None) return 0f;
            float x = Night(hour) ? 0.6f : 0.9f;
            return g == IdleGroup.Hospitality ? x * 0.9f : x;
        }

        /// <summary>
        /// How far from their spot they go for it: the game's visitors about as far as they wander, Rimfolk's visits keep
        /// close, envoys about the colony, and Hospitality's guests over the colony's rooms and grounds (their guest area,
        /// when one is set, decides where).
        /// </summary>
        public static float Reach(IdleGroup g, IdleRole r)
        {
            if (r == IdleRole.Trader) return 9f;
            switch (g)
            {
                case IdleGroup.GameVisit: return 24f;
                case IdleGroup.Caravan: return 16f;
                case IdleGroup.RimfolkVisit: return 14f;
                case IdleGroup.Envoys: return 30f;
                case IdleGroup.Hospitality: return HospitalityReach;
                default: return 0f;
            }
        }

        /// <summary>How far over the colony a guest of Hospitality's goes from where they stand.</summary>
        public const float HospitalityReach = 50f;

        /// <summary>
        /// How good a place is for them, lower better: near rather than far (by their reach), somewhere other than the room
        /// they are in (Hospitality's guests most: so they don't all sit in the one room), away from others of the visitors
        /// already there, and a little chance, so not everyone picks the same one.
        /// </summary>
        public static float PlaceScore(float distance, float reach, bool sameRoom, int othersNear, bool hospitality, float roll)
        {
            float s = Math.Max(0f, distance) / Math.Max(1f, reach);
            if (sameRoom) s += hospitality ? 0.8f : 0.25f;
            s += 0.3f * Math.Max(0, othersNear);
            return s + 0.4f * Math.Max(0f, Math.Min(1f, roll));
        }

        /// <summary>How long one stint lasts, in ticks (an hour is 2500); a night's sleep lasts till they are rested.</summary>
        public static (int min, int max) Stint(IdleDoing d)
        {
            switch (d)
            {
                case IdleDoing.Chat: return (800, 1600);
                case IdleDoing.Company: return (600, 1200);
                case IdleDoing.Fire: return (1800, 3200);
                case IdleDoing.Warm: return (700, 1400);
                case IdleDoing.Seat: return (1500, 3000);
                case IdleDoing.Shelter: return (2000, 3500);
                case IdleDoing.Snack: return (400, 700);
                case IdleDoing.Nap: return (1500, 3500);
                case IdleDoing.Game: return (1800, 3400);
                case IdleDoing.Music: return (1500, 2800);
                case IdleDoing.Stars: return (1500, 3000);
                case IdleDoing.Art: return (500, 1100);
                case IdleDoing.Pray: return (700, 1400);
                case IdleDoing.Animal: return (700, 1400);
                case IdleDoing.Play: return (1200, 2400);
                case IdleDoing.OwnDrug: return (600, 900);
                case IdleDoing.Pipe: return (1000, 1800);
                case IdleDoing.Carve: return (1500, 3000);
                case IdleDoing.Read: return (2000, 3500);
                case IdleDoing.Walk: return (1500, 3000);
                case IdleDoing.Pet: return (500, 1100);
                case IdleDoing.Write: return (1500, 3000);
                case IdleDoing.Train: return (1200, 2400);
                case IdleDoing.Archery: return (1500, 3000);
                case IdleDoing.Fish: return (2000, 3500);
                case IdleDoing.Mend: return (1200, 2400);
                case IdleDoing.Sharpen: return (900, 1800);
                case IdleDoing.Cards: return (1500, 3000);
                case IdleDoing.Sing: return (600, 1200);
                case IdleDoing.Sleep: return (GenDateTicksPerHour * 4, GenDateTicksPerHour * 8);
                default: return (600, 1200);
            }
        }

        private const int GenDateTicksPerHour = 2500;

        /// <summary>
        /// Ticks to the next look: at once after a stint (the moment it ends, so they go straight on to something else),
        /// longer when nothing came of a look or the chance said no.
        /// </summary>
        public static (int min, int max) NextLook(bool found) => found ? (0, 0) : (300, 700);

        /// <summary>A stint that ended almost as it began (the place was taken, the way blocked): the next look waits, so nobody starts and stops over and over.</summary>
        public const int ShortStint = 250;

        /// <summary>The wait to the next look once a stint of <paramref name="ran"/> ticks is over.</summary>
        public static (int min, int max) NextLookAfter(int ran) => NextLook(ran >= ShortStint);

        /// <summary>The longest stretch of standing or wandering counted as one idle moment, for the log's share of time filled.</summary>
        public const int IdleMomentCap = 1000;

        /// <summary>The share of their idle time filled with something of their own, from the ticks at stints and the ticks left standing about.</summary>
        public static float FilledShare(int stintTicks, int idleTicks) => stintTicks + idleTicks <= 0 ? 0f : (float)Math.Max(0, stintTicks) / (Math.Max(0, stintTicks) + Math.Max(0, idleTicks));

        /// <summary>Hours a visitor leaves a joy thing alone after giving way to someone of the colony.</summary>
        public const int GiveWayHours = 4;

        /// <summary>How near (in cells) someone of the colony must stay for a visitor to keep them company.</summary>
        public const float CompanyNear = 4f;

        // ---------------- The forms a doing takes ----------------

        /// <summary>How many forms a doing has (its job's count): a book their own or the colony's, a walk's end, petting or fetch, a letter or a journal, a target or shadow sparring, a blade, a bow or a gun, cards, dice or stones.</summary>
        public static int Forms(IdleDoing d)
        {
            switch (d)
            {
                case IdleDoing.Read:
                case IdleDoing.Pet:
                case IdleDoing.Write:
                case IdleDoing.Train:
                    return 2;
                case IdleDoing.Sharpen:
                case IdleDoing.Cards:
                    return 3;
                case IdleDoing.Walk:
                    return 4;
                default:
                    return 1;
            }
        }

        /// <summary>The forms of a read: the colony's book where it lies, or one of their own.</summary>
        public const int ReadColony = 0, ReadOwn = 1;

        /// <summary>The ends of a walk: round the paths, to a garden, to a grave, to a view.</summary>
        public const int WalkPaths = 0, WalkGarden = 1, WalkGrave = 2, WalkView = 3;

        /// <summary>The forms of a game of their own by their people's age: a tribal people's game of stones, dice among the medieval most, cards.</summary>
        public static int CardsForm(int age, float roll)
        {
            if (age <= 0) return 2;
            if (age == 1) return roll < 0.5f ? 1 : 0;
            return roll < 0.2f ? 1 : 0;
        }

        /// <summary>Seeing to their weapon, by its kind: a blade sharpened (0), a bow oiled and restrung (1), a gun cleaned (2).</summary>
        public static int SharpenForm(int weaponKind) => weaponKind == 2 ? 1 : weaponKind == 3 ? 2 : 0;

        // ---------------- What's said ----------------

        /// <summary>
        /// The moment a line is picked for (Rimfolk_Pool_VisitorIdle), or null when nothing is said. <paramref name="drink"/>:
        /// their own is a drink, not a smoke; <paramref name="watch"/>: the colony's thing is watched or listened to (a screen,
        /// a radio), not played; <paramref name="form"/>: which form of the doing (<see cref="Forms"/>).
        /// </summary>
        public static string Moment(IdleDoing d, bool drink, bool watch = false, int form = 0)
        {
            switch (d)
            {
                case IdleDoing.Company: return "company";
                case IdleDoing.Fire: return "fire";
                case IdleDoing.Warm: return "warm";
                case IdleDoing.Seat: return "seat";
                case IdleDoing.Shelter: return "shelter";
                case IdleDoing.Snack: return "snack";
                case IdleDoing.Game: return watch ? "watch" : "game";
                case IdleDoing.Music: return "music";
                case IdleDoing.Stars: return "stars";
                case IdleDoing.Art: return "art";
                case IdleDoing.Pray: return "pray";
                case IdleDoing.Animal: return "animal";
                case IdleDoing.Play: return "play";
                case IdleDoing.OwnDrug: return drink ? "drink" : "smoke";
                case IdleDoing.Pipe: return "pipe";
                case IdleDoing.Carve: return "carve";
                case IdleDoing.Read: return form == ReadOwn ? "readown" : "read";
                case IdleDoing.Walk: return form == WalkGarden ? "garden" : form == WalkGrave ? "grave" : form == WalkView ? "view" : "walk";
                case IdleDoing.Pet: return form == 1 ? "fetch" : "pet";
                case IdleDoing.Write: return form == 1 ? "journal" : "letter";
                case IdleDoing.Train: return form == 1 ? "spar" : "train";
                case IdleDoing.Archery: return "archery";
                case IdleDoing.Fish: return "fish";
                case IdleDoing.Mend: return "mend";
                case IdleDoing.Sharpen: return form == 1 ? "bow" : form == 2 ? "gun" : "sharpen";
                case IdleDoing.Cards: return form == 1 ? "dice" : form == 2 ? "stones" : "cards";
                case IdleDoing.Sing: return "sing";
                default: return null;
            }
        }

        /// <summary>The chance of a line as a stint begins (a greeting to someone of the colony more often, a word to a pet, nothing asleep).</summary>
        public static float LineChance(IdleDoing d)
        {
            switch (d)
            {
                case IdleDoing.Company: return 0.5f;
                case IdleDoing.Pet: return 0.4f;
                case IdleDoing.Chat:
                case IdleDoing.Nap:
                case IdleDoing.Sleep:
                    return 0f;
                default: return 0.2f;
            }
        }

        /// <summary>The chance of a word when a fishing stint ends with a catch.</summary>
        public const float CatchLineChance = 0.4f;

        /// <summary>The chance of a word on giving a joy thing up to someone of the colony.</summary>
        public const float GiveWayLineChance = 0.5f;

        /// <summary>Ticks between lines from the same visitor, and between any two on one map.</summary>
        public const int LinePawnGap = 6 * 2500, LineMapGap = 2500 / 2;

        /// <summary>A line may be said now: the speaker and the map have been quiet long enough.</summary>
        public static bool MaySpeak(int pawnLast, int mapLast, int now) =>
            (pawnLast < 0 || now - pawnLast >= LinePawnGap || now < pawnLast) && (mapLast < 0 || now - mapLast >= LineMapGap || now < mapLast);
    }
}
