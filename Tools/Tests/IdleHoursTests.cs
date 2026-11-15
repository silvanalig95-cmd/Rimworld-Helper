using System;
using System.IO;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Visitors' idle hours (Source/ColonyCouncil/Visitors/IdleHoursMath.cs, IdleHours.cs, IdleHoursJobs.cs): what fits whom by
/// their people's age, the hour, the weather, their calling and who they are; the colony's joy things by the game's joy
/// givers, free of the colony's people and of the visitor's age, the player's mod pool's games in and its baths and
/// shooting ranges out; Hospitality's guests filled as the others, after its own recreation and within their guest area;
/// the spread over the colony; the new doings (a book, a walk, the pets, a letter, training, archery, fishing, mending, a
/// weapon seen to, cards, a song, a night's sleep) and the quieter night; the stints, the reach, the next look at once and
/// the words; and the code's seams: the hook after the market's, nothing reserved and giving way, the trader's pack, the
/// leftovers, the book put back, the catch their own, the pets never trained, the jobs, the pool and the setting.
/// </summary>
public static class IdleHoursTests
{
    private static HashSet<string> pool;

    /// <summary>Every def name of the player's enabled mods (docs/player-mods/defs.tsv.gz, the active rows).</summary>
    private static HashSet<string> Pool(string root)
    {
        if (pool != null) return pool;
        pool = new HashSet<string>();
        using var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress);
        using var r = new StreamReader(gz);
        string line;
        while ((line = r.ReadLine()) != null)
        {
            var t = line.Split('\t');
            if (t.Length >= 4 && t[3] == "yes") pool.Add(t[1]);
        }
        return pool;
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "visitors' idle hours: " + what);
        }

        IdleFacts F(Action<IdleFacts> set = null)
        {
            var f = new IdleFacts { group = IdleGroup.GameVisit, role = IdleRole.Plain, age = 2, hour = 14, temperature = 20f };
            set?.Invoke(f);
            return f;
        }
        float W(IdleDoing d, IdleFacts f) => IdleHoursMath.Weights(f)[(int)d];

        // ---------- The hours ----------
        C(IdleHoursMath.MealHour(7) && IdleHoursMath.MealHour(12) && IdleHoursMath.MealHour(18) && !IdleHoursMath.MealHour(10) && !IdleHoursMath.MealHour(15), "meals at breakfast, midday and supper");
        C(IdleHoursMath.PrayerHour(6) && IdleHoursMath.PrayerHour(18) && !IdleHoursMath.PrayerHour(12) && !IdleHoursMath.PrayerHour(23), "prayers morning and evening");
        C(IdleHoursMath.StarHour(22) && IdleHoursMath.StarHour(2) && !IdleHoursMath.StarHour(12), "the telescope in the dark");
        C(IdleHoursMath.NapHour(13) && !IdleHoursMath.NapHour(17) && !IdleHoursMath.NapHour(9), "the nap at midday");
        C(IdleHoursMath.GameHour(9) && IdleHoursMath.GameHour(21) && !IdleHoursMath.GameHour(23) && !IdleHoursMath.GameHour(3), "games by day and into the evening, never when people sleep");
        C(IdleHoursMath.Night(23) && IdleHoursMath.Night(3) && !IdleHoursMath.Night(5) && !IdleHoursMath.Night(22), "the night");

        // ---------- Ages ----------
        C(IdleHoursMath.AgeOf(2) == 0 && IdleHoursMath.AgeOf(3) == 1 && IdleHoursMath.AgeOf(4) == 2 && IdleHoursMath.AgeOf(5) == 3 && IdleHoursMath.AgeOf(6) == 4
          && IdleHoursMath.AgeOf(7) == 4 && IdleHoursMath.AgeOf(0) == 0, "the game's tech levels as ages");
        C(IdleHoursMath.TheirAge(0) == 2 && IdleHoursMath.TheirAge(2) == 0, "someone of no known people counts as industrial");
        int horseshoes = IdleHoursMath.ThingAge(0, 0, false, false, false), chess = IdleHoursMath.ThingAge(0, 3, false, false, false),
            telescope = IdleHoursMath.ThingAge(0, 0, true, false, false), tv = IdleHoursMath.ThingAge(0, 4, false, false, true),
            megascreen = IdleHoursMath.ThingAge(0, 4, false, true, true);
        C(horseshoes == 0 && chess == 1 && telescope == 2 && tv == 2 && megascreen == 3, "the colony's things by their research, parts and power (horseshoes, chess, the telescope, a screen)");
        C(IdleHoursMath.Fits(0, horseshoes) && !IdleHoursMath.Fits(0, chess), "a tribal visitor plays horseshoes, not chess");
        C(IdleHoursMath.Fits(1, chess) && !IdleHoursMath.Fits(1, telescope), "a medieval visitor plays chess, but doesn't use a telescope");
        C(IdleHoursMath.Fits(2, telescope) && IdleHoursMath.Fits(2, horseshoes) && !IdleHoursMath.Fits(2, megascreen), "an industrial one uses the telescope and plays the old games, not spacer screens");
        C(IdleHoursMath.Fits(3, megascreen) && IdleHoursMath.Fits(4, megascreen), "a spacer uses everything");

        // ---------- The colony's joy things, from the player's mod pool (docs/player-mods, active rows) ----------
        var inPool = new (string giver, string kind, string thing)[]
        {
            ("Play_Horseshoes", "Gaming_Dexterity", "HorseshoesPin"), ("Play_Hoopstone", "Gaming_Dexterity", "HoopstoneRing"), ("Play_Chess", "Gaming_Cerebral", "ChessTable"),
            ("Play_GameOfUr", "Gaming_Cerebral", "GameOfUrBoard"), ("Play_Poker", "Gaming_Cerebral", "PokerTable"), ("Play_Billiards", "Gaming_Dexterity", "BilliardsTable"),
            ("WatchTelevision", "Television", "TubeTelevision"), ("UseTelescope", "Telescope", "Telescope"), ("Play_MusicalInstrument", "HighCulture", "Harp"),
            ("Play_Arcade", "Gaming_Cerebral", "Joy_Arcade"), ("Play_Roulette", "Gaming_Cerebral", "Joy_RouletteTable"), ("Play_Piano", "HighCulture", "Joy_Piano"),
            ("Play_Faro", "Gaming_Cerebral", "TableFaro"), ("Play_FiveFingerFillet", "Gaming_Dexterity", "TableFiveFingerFilet"), ("DV_Play_Tongo", "Gaming_Cerebral", "DV_TongoTable"),
            ("VQEA_Play_AncientFoosballTable", "Gaming_Cerebral", "VQEA_AncientFoosballTable"), ("VQE_Play_WargamingTable", "VQE_Gaming_StrategicPlay", "VQE_AncientWargamingTable"),
            ("VFES_UseInteractiveTable", "Television", "Table_interactive_1x1c"), ("VFE_Play_Computer", "VFE_ComputerUsage", "Joy_ModernComputer"),
            ("VFE_ListenToMusic", "HighCulture", "Radio_Industrial")
        };
        foreach (var (giver, kind, thing) in inPool) C(!IdleHoursMath.Excluded(giver, kind, thing), $"{giver} ({thing}) is one of the colony's games for visitors");
        var leftOut = new (string giver, string kind, string thing)[]
        {
            ("UseHotTub", "Hydrotherapy", "HotTub"), ("UseDBHSauna", "Hydrotherapy", "DBHSaunaHeaterLog"), ("UseDBHSwimmingPool", "Hydrotherapy", "DBHSwimmingPool"),
            ("UseDBHPoolTiles", "Hydrotherapy", "DBHPoolTile"), ("VFEC_Relax_Thermaebath", "VFEC_Relaxing_Bath", "VFEC_Thermaebath"), ("WatchWashingMachine", "Meditative", "WashingMachine"),
            ("ShootSomeArrows", "Gaming_Shooting", "ArcheryTarget"), ("PracticeShooting", "Gaming_Shooting", "ShootingRangeTarget"), ("PracticeMartialArts", "Gaming_MartialArts", "MartialArtsTarget"),
            ("Play_DartsBoard", "Gaming_Dexterity", "Joy_DartsBoard"), ("VFEM2_Play_Archery", "Gaming_Dexterity", "VFEM2_ArcheryTarget"),
            ("VFEM2_Play_TrainingDummy", "Gaming_Dexterity", "VFEM2_TrainingDummy"), ("Play_PunchingBag", "VFE_StrengthPlay", "Joy_PunchingBag"),
            ("VFEC_Stage_Performance", "Television", "VFEC_TheatreStage"), ("RimMarket_WatchAuction", "RimMarket_Market", "RimMarket_AuctionBlock"),
            ("VFE_Play_Sunbathing", "Meditative", "VFE_Lounger")
        };
        foreach (var (giver, kind, thing) in leftOut) C(IdleHoursMath.Excluded(giver, kind, thing), $"{giver} ({thing}) is left to the colony: water and heat, a throw or a blow the game shows, a stage, the market");
        C(IdleHoursMath.ShapeOf(true, true, false, false, true) == JoyShape.Instrument && IdleHoursMath.ShapeOf(false, true, false, false, false) == JoyShape.Watch
          && IdleHoursMath.ShapeOf(false, false, true, false, false) == JoyShape.Adjacent && IdleHoursMath.ShapeOf(false, false, false, true, true) == JoyShape.Cell
          && IdleHoursMath.ShapeOf(false, false, false, false, true) == JoyShape.Cell && IdleHoursMath.ShapeOf(false, false, false, false, false) == JoyShape.Adjacent,
            "how a joy thing is used, by the game's joy giver for it");
        C(IdleHoursMath.ThrowOf("RimWorld.JobDriver_PlayHorseshoes") == JoyThrow.Horseshoe && IdleHoursMath.ThrowOf("RimWorld.JobDriver_PlayHoopstone") == JoyThrow.Stone
          && IdleHoursMath.ThrowOf("RimWorld.JobDriver_SitFacingBuilding") == JoyThrow.None && IdleHoursMath.ThrowOf(null) == JoyThrow.None, "horseshoes and stones thrown as the game throws them");
        C(IdleHoursMath.IsTelescope("Telescope", "Telescope") && IdleHoursMath.IsTelescope(null, "VFE_Telescope") && !IdleHoursMath.IsTelescope("Gaming_Cerebral", "ChessTable"), "the telescope is for the stars");
        C(IdleHoursMath.IsMusic(true, "HighCulture", false) && IdleHoursMath.IsMusic(false, "HighCulture", false) && !IdleHoursMath.IsMusic(false, "HighCulture", true)
          && !IdleHoursMath.IsMusic(false, "Gaming_Cerebral", false), "the harp and the piano are played as music, a radio is listened to like a screen, chess is a game");
        C(!IdleHoursMath.Excluded("Play_Billiards", "Gaming_Dexterity", "VFE_PoolTable") && IdleHoursMath.Excluded("ReceiveTherapy", "Social", "TherapyCouch")
          && IdleHoursMath.Excluded("AM_VisitCorpseContainer", "Morbid", "AM_CorpseDisplay"), "a pool table is a game; a therapist's couch and a corpse on show are not for visitors");
        // A fire to sit round, from the player's buildings (docs/player-mods/buildings.tsv, active rows).
        C(IdleHoursMath.SitByFire(true, "Campfire", false, true, 1) && IdleHoursMath.SitByFire(true, "Brazier", false, false, 1) && IdleHoursMath.SitByFire(true, "DarklightBrazier", false, false, 1)
          && IdleHoursMath.SitByFire(true, "VFEM2_Hearth", false, false, 2) && IdleHoursMath.SitByFire(true, "VFET_LargeFire", false, false, 4) && IdleHoursMath.SitByFire(true, "VCE_Grill", false, true, 1),
            "a campfire, a brazier, a hearth, a tribal large fire and a grill are fires to sit round");
        C(!IdleHoursMath.SitByFire(true, "TorchLamp", false, false, 1) && !IdleHoursMath.SitByFire(true, "Darktorch", false, false, 1) && !IdleHoursMath.SitByFire(true, "RB_WaxCandle", false, false, 1)
          && !IdleHoursMath.SitByFire(true, "ChemlampPost", false, false, 1) && !IdleHoursMath.SitByFire(true, "Maal_GravFieldExtender", false, false, 1),
            "torches, lamps, candles and the grav field extender are lights, not fires to sit round");
        C(!IdleHoursMath.SitByFire(true, "FueledStove", false, true, 3) && !IdleHoursMath.SitByFire(true, "FueledSmithy", false, true, 3)
          && !IdleHoursMath.SitByFire(true, "VQE_Genetron_WoodFired", true, false, 36) && !IdleHoursMath.SitByFire(false, "Campfire", false, true, 1),
            "a stove, a smithy and a wood-fired generator are no fires to sit round, nor anything that isn't a fire");
        C(IdleHoursMath.Free(false, false, false, false, 0, 1), "a joy thing nobody has is free");
        C(!IdleHoursMath.Free(true, false, false, false, 0, 2), "one of the colony's people has it or is on the way: not free");
        C(!IdleHoursMath.Free(false, true, false, false, 0, 2) && !IdleHoursMath.Free(false, false, true, false, 0, 2) && !IdleHoursMath.Free(false, false, false, true, 0, 2),
            "forbidden, switched off or without power: left alone");
        C(IdleHoursMath.Free(false, false, false, false, 1, 2) && !IdleHoursMath.Free(false, false, false, false, 2, 2) && !IdleHoursMath.Free(false, false, false, false, 1, 0),
            "two visitors at a chess table, one at a telescope");

        // ---------- What they carry ----------
        C(IdleHoursMath.DrugKindOf("SmokeleafJoint", "Smoke_Joint", false) == OwnDrugKind.Smoke && IdleHoursMath.DrugKindOf("VBE_Cigar", null, false) == OwnDrugKind.Smoke, "a joint and a cigar are a smoke");
        C(IdleHoursMath.DrugKindOf("Beer", null, true) == OwnDrugKind.Drink && IdleHoursMath.DrugKindOf("Beer", null, false) == OwnDrugKind.Drink
          && IdleHoursMath.DrugKindOf("PsychiteTea", null, false) == OwnDrugKind.Drink, "beer and tea are a drink");
        C(IdleHoursMath.DrugKindOf("Ambrosia", null, false) == OwnDrugKind.None && IdleHoursMath.DrugKindOf("Yayo", null, false) == OwnDrugKind.None, "nothing else is had idly");

        // ---------- Who may do what ----------
        foreach (var d in IdleHoursMath.All)
            C(IdleHoursMath.ForHospitality(d) == (d != IdleDoing.None && d != IdleDoing.Sleep),
                $"{d}: Hospitality's guests get Rimfolk's doings too, alongside its own recreation, their nights in its beds");
        C(IdleHoursMath.NotFor(IdleDoing.Game, F(f => f.group = IdleGroup.Hospitality)) == null && IdleHoursMath.NotFor(IdleDoing.Warm, F(f => f.group = IdleGroup.Hospitality)) == null
          && IdleHoursMath.NotFor(IdleDoing.Sleep, F(f => f.group = IdleGroup.Hospitality)) != null,
            "Hospitality's guest plays the colony's games and warms their hands by Rimfolk's rules, and sleeps in Hospitality's beds");
        C(IdleHoursMath.NotFor(IdleDoing.Game, F(f => f.child = true)) != null && IdleHoursMath.NotFor(IdleDoing.Play, F(f => f.child = true)) == null
          && IdleHoursMath.NotFor(IdleDoing.Play, F()) != null, "children play, the grown play the colony's games");
        C(IdleHoursMath.NotFor(IdleDoing.Game, F(f => f.role = IdleRole.Guard)) != null && IdleHoursMath.NotFor(IdleDoing.Nap, F(f => f.role = IdleRole.Guard)) != null
          && IdleHoursMath.NotFor(IdleDoing.Nap, F(f => f.role = IdleRole.Trader)) != null && IdleHoursMath.NotFor(IdleDoing.OwnDrug, F(f => f.role = IdleRole.Trader)) != null,
            "a guard stays on watch and the trader keeps to the trade");
        C(IdleHoursMath.NotFor(IdleDoing.Seat, F(f => f.role = IdleRole.Guard)) == null && IdleHoursMath.NotFor(IdleDoing.Warm, F(f => f.role = IdleRole.Trader)) == null,
            "but either may sit down or warm their hands");
        C(IdleHoursMath.NotFor(IdleDoing.Nap, F(f => f.titled = true)) != null && IdleHoursMath.NotFor(IdleDoing.Carve, F(f => f.titled = true)) != null
          && IdleHoursMath.NotFor(IdleDoing.Art, F(f => f.titled = true)) == null, "a titled visitor doesn't doze on the ground or whittle, but looks at the art");
        C(IdleHoursMath.NotFor(IdleDoing.OwnDrug, F(f => f.drugsAllowed = false)) != null && IdleHoursMath.NotFor(IdleDoing.Pipe, F(f => f.drugsAllowed = false)) != null,
            "a teetotaller, or a faith against drugs: no smoke or drink");
        C(IdleHoursMath.NotFor(IdleDoing.Pray, F(f => f.scoffer = true)) != null, "a scoffer doesn't pray");
        C(IdleHoursMath.NotFor(IdleDoing.Chat, F(f => f.canTalk = false)) != null && IdleHoursMath.NotFor(IdleDoing.Animal, F(f => f.canHandle = false)) != null,
            "one who can't talk doesn't chat; one who can't use their hands doesn't brush an animal");
        C(IdleHoursMath.Weights(F(f => f.group = IdleGroup.None)).All(x => x == 0f), "nobody of no visitor group");

        // ---------- What fits, by what is about, the hour and the weather ----------
        var bare = IdleHoursMath.Weights(F());
        C(IdleHoursMath.All.Where(d => bare[(int)d] > 0f).All(d => d == IdleDoing.Pipe || d == IdleDoing.Carve || d == IdleDoing.Sing),
            "with nothing about, only what they have themselves: a pipe, a bit of whittling, a song");
        C(W(IdleDoing.Shelter, F(f => { f.rain = true; f.underSky = true; f.roof = true; })) == 4f && W(IdleDoing.Shelter, F(f => { f.underSky = true; f.roof = true; })) == 0f
          && W(IdleDoing.Shelter, F(f => { f.rain = true; f.underSky = false; f.roof = true; })) == 0f, "out of the rain when it rains on them, and only then");
        var wet = IdleHoursMath.Weights(F(f => { f.rain = true; f.underSky = true; f.roof = true; f.fire = true; f.seat = true; f.game = true; }));
        C(IdleHoursMath.All.All(d => d == IdleDoing.Shelter || wet[(int)d] < wet[(int)IdleDoing.Shelter]), "in the rain, shelter first");
        C(W(IdleDoing.Warm, F(f => { f.fire = true; f.temperature = -8f; })) > 0f && W(IdleDoing.Warm, F(f => f.fire = true)) == 0f, "hands warmed in the cold, not on a mild day");
        C(W(IdleDoing.Fire, F(f => { f.fire = true; f.hour = 19; })) > W(IdleDoing.Fire, F(f => f.fire = true)) * 2f, "the fire draws them of an evening");
        C(W(IdleDoing.Fire, F(f => { f.fire = true; f.temperature = 6f; })) > W(IdleDoing.Fire, F(f => f.fire = true)), "and on a chilly day");
        C(W(IdleDoing.Snack, F(f => { f.ownFood = true; f.food = 0.6f; f.hour = 12; })) > 0f && W(IdleDoing.Snack, F(f => { f.ownFood = true; f.food = 0.6f; f.hour = 15; })) == 0f
          && W(IdleDoing.Snack, F(f => { f.ownFood = true; f.food = 0.9f; f.hour = 12; })) == 0f && W(IdleDoing.Snack, F(f => { f.food = 0.6f; f.hour = 12; })) == 0f,
            "a bite from their own pack at a meal hour, when peckish, and only with food of their own");
        var nap = F(f => { f.shade = true; f.hour = 13; f.rest = 0.6f; });
        C(W(IdleDoing.Nap, nap) > 0f, "a midday nap in the shade when a little tired");
        C(W(IdleDoing.Nap, F(f => { f.shade = true; f.hour = 13; f.rest = 0.9f; })) == 0f && W(IdleDoing.Nap, F(f => { f.shade = true; f.hour = 9; f.rest = 0.6f; })) == 0f
          && W(IdleDoing.Nap, F(f => { f.shade = true; f.hour = 13; f.rest = 0.6f; f.rain = true; })) == 0f
          && W(IdleDoing.Nap, F(f => { f.shade = true; f.hour = 13; f.rest = 0.6f; f.temperature = 4f; })) == 0f,
            "not when rested, not in the morning, not in the rain or the cold");
        C(W(IdleDoing.Game, F(f => f.game = true)) > 0f && W(IdleDoing.Game, F(f => { f.game = true; f.hour = 23; })) == 0f, "games in their hours");
        C(W(IdleDoing.Music, F(f => f.music = true)) > 0f && W(IdleDoing.Music, F(f => { f.music = true; f.hour = 21; })) == 0f, "an instrument, never late in the evening");
        C(W(IdleDoing.Stars, F(f => { f.stars = true; f.hour = 22; })) > 0f && W(IdleDoing.Stars, F(f => f.stars = true)) == 0f
          && W(IdleDoing.Stars, F(f => { f.stars = true; f.hour = 22; f.clearSky = false; })) == 0f, "the telescope on a clear night");
        C(W(IdleDoing.Art, F(f => f.art = true)) > 0f && W(IdleDoing.Art, F(f => { f.art = true; f.hour = 2; })) == 0f
          && W(IdleDoing.Art, F(f => { f.art = true; f.titled = true; })) > W(IdleDoing.Art, F(f => f.art = true)), "the art by day, a titled one more often");
        C(W(IdleDoing.Pray, F(f => { f.shrine = true; f.hour = 6; })) > W(IdleDoing.Pray, F(f => f.shrine = true))
          && W(IdleDoing.Pray, F(f => { f.shrine = true; f.hour = 6; f.devout = true; })) > W(IdleDoing.Pray, F(f => { f.shrine = true; f.hour = 6; }))
          && W(IdleDoing.Pray, F(f => { f.shrine = true; f.hour = 6; f.calling = "pilgrims"; })) > W(IdleDoing.Pray, F(f => { f.shrine = true; f.hour = 6; }))
          && W(IdleDoing.Pray, F()) == 0f, "prayers at a shrine of their god, morning and evening, the devout and the pilgrims most");
        C(W(IdleDoing.Animal, F(f => { f.animal = true; f.calling = "herders"; })) == 2f * W(IdleDoing.Animal, F(f => f.animal = true))
          && W(IdleDoing.Animal, F(f => { f.animal = true; f.hour = 2; })) == 0f, "herders see to their animals most, nobody at night");
        C(W(IdleDoing.Play, F(f => { f.child = true; f.hour = 10; })) > 0f && W(IdleDoing.Play, F(f => { f.child = true; f.hour = 21; })) == 0f, "children play by day");
        C(W(IdleDoing.OwnDrug, F(f => { f.ownDrug = true; f.hour = 19; })) > W(IdleDoing.OwnDrug, F(f => { f.ownDrug = true; f.hour = 11; }))
          && W(IdleDoing.OwnDrug, F(f => { f.ownDrug = true; f.hour = 8; })) == 0f && W(IdleDoing.OwnDrug, F(f => f.hour = 19)) == 0f,
            "a drink of their own of an evening, never in the morning, and only what they carry");
        C(W(IdleDoing.Carve, F(f => f.age = 0)) > W(IdleDoing.Carve, F(f => f.age = 2)) && W(IdleDoing.Carve, F(f => f.age = 2)) > W(IdleDoing.Carve, F(f => f.age = 3)),
            "whittling by the old ways most");
        C(W(IdleDoing.Pipe, F(f => f.age = 1)) > W(IdleDoing.Pipe, F(f => f.age = 3)), "a pipe more among the older ages");
        C(W(IdleDoing.Chat, F(f => f.companion = true)) > W(IdleDoing.Chat, F(f => { f.companion = true; f.hour = 2; })) && W(IdleDoing.Chat, F()) == 0f,
            "a talk with one of their own when one is idle near them");
        C(W(IdleDoing.Company, F(f => f.colonist = true)) > 0f && W(IdleDoing.Company, F(f => { f.colonist = true; f.hour = 2; })) == 0f, "one of the colony's people at leisure, kept company by day");
        var hosp = IdleHoursMath.Weights(F(f =>
        {
            f.group = IdleGroup.Hospitality; f.fire = true; f.temperature = -5f; f.seat = true; f.game = true; f.art = true; f.companion = true; f.colonist = true; f.animal = true;
        }));
        C(IdleHoursMath.All.Where(d => hosp[(int)d] > 0f).All(IdleHoursMath.ForHospitality) && hosp[(int)IdleDoing.Warm] > 0f && hosp[(int)IdleDoing.Animal] > 0f
          && hosp[(int)IdleDoing.Game] > 0f && hosp[(int)IdleDoing.Seat] > 0f && hosp[(int)IdleDoing.Chat] > 0f && hosp[(int)IdleDoing.Company] > 0f,
            "Hospitality's guest: the fire in the cold, their animals, the colony's games, a seat, a talk, as the others");

        // ---------- The pick ----------
        C(IdleHoursMath.Pick(new float[IdleHoursMath.All.Length], 0.5f) == IdleDoing.None, "nothing fits: nothing picked");
        var one = new float[IdleHoursMath.All.Length];
        one[(int)IdleDoing.Art] = 0.4f;
        C(IdleHoursMath.Pick(one, 0f) == IdleDoing.Art && IdleHoursMath.Pick(one, 0.99f) == IdleDoing.Art && IdleHoursMath.Pick(one, 5f) == IdleDoing.Art, "one that fits is the one picked");
        var two = new float[IdleHoursMath.All.Length];
        two[(int)IdleDoing.Chat] = 1f;
        two[(int)IdleDoing.Fire] = 3f;
        C(IdleHoursMath.Pick(two, 0.1f) == IdleDoing.Chat && IdleHoursMath.Pick(two, 0.5f) == IdleDoing.Fire && IdleHoursMath.Pick(two, -1f) == IdleDoing.Chat, "by the weights");
        int fires = 0;
        for (int i = 0; i < 1000; i++)
            if (IdleHoursMath.Pick(two, (i + 0.5f) / 1000f) == IdleDoing.Fire)
                fires++;
        C(fires >= 740 && fires <= 760, $"a three-to-one weight picks three times in four ({fires} of 1000)");

        // ---------- How often, how far, how long ----------
        C(IdleHoursMath.FillChance(IdleGroup.None, 12) == 0f && IdleHoursMath.FillChance(IdleGroup.GameVisit, 12) > IdleHoursMath.FillChance(IdleGroup.GameVisit, 2)
          && IdleHoursMath.FillChance(IdleGroup.Hospitality, 12) < IdleHoursMath.FillChance(IdleGroup.GameVisit, 12), "filled most of the day, less at night and for Hospitality's guests");
        C(IdleHoursMath.FillChance(IdleGroup.GameVisit, 12) >= 0.85f && IdleHoursMath.FillChance(IdleGroup.GameVisit, 2) >= 0.5f
          && IdleHoursMath.FillChance(IdleGroup.Hospitality, 12) >= 0.8f, "most of their waiting time filled: nine looks in ten by day, Hospitality's guests near the others, more than half at night");
        C(IdleHoursMath.Reach(IdleGroup.Caravan, IdleRole.Trader) < IdleHoursMath.Reach(IdleGroup.Caravan, IdleRole.Plain)
          && IdleHoursMath.Reach(IdleGroup.RimfolkVisit, IdleRole.Plain) < IdleHoursMath.Reach(IdleGroup.GameVisit, IdleRole.Plain)
          && IdleHoursMath.Reach(IdleGroup.GameVisit, IdleRole.Plain) < IdleHoursMath.Reach(IdleGroup.Envoys, IdleRole.Plain)
          && IdleHoursMath.Reach(IdleGroup.Hospitality, IdleRole.Plain) >= 40f && IdleHoursMath.Reach(IdleGroup.Hospitality, IdleRole.Plain) > IdleHoursMath.Reach(IdleGroup.Envoys, IdleRole.Plain)
          && IdleHoursMath.Reach(IdleGroup.None, IdleRole.Plain) == 0f,
            "the trader near the trade, Rimfolk's visits close to their spot, envoys about the colony, Hospitality's guests over the colony's rooms and grounds");
        foreach (var d in IdleHoursMath.All.Where(d => d != IdleDoing.Sleep))
        {
            var (lo, hi) = IdleHoursMath.Stint(d);
            C(lo >= 120 && lo < hi && hi <= 3600, $"{d}: a stint of {lo} to {hi} ticks, an hour and a half at most");
        }
        C(IdleHoursMath.Stint(IdleDoing.Sleep).min >= 4 * 2500 && IdleHoursMath.Stint(IdleDoing.Sleep).max <= 10 * 2500, "a night's sleep lasts hours (till they're rested, the game's own waking)");
        C(IdleHoursMath.Stint(IdleDoing.Fire).max >= 3000 && IdleHoursMath.Stint(IdleDoing.Read).max >= 3000 && IdleHoursMath.Stint(IdleDoing.Fish).max >= 3000,
            "longer stints where it fits: an evening at the fire, a book, fishing");
        C(IdleHoursMath.NextLook(true).max < IdleHoursMath.NextLook(false).min + 500 && IdleHoursMath.NextLook(true).min < IdleHoursMath.NextLook(false).min, "a short wander between stints, a longer one when nothing came of a look");
        C(IdleHoursMath.NextLook(true) == (0, 0), "a look at once when a doing ends");
        C(IdleHoursMath.NextLookAfter(IdleHoursMath.ShortStint - 1) == IdleHoursMath.NextLook(false) && IdleHoursMath.NextLookAfter(1500) == IdleHoursMath.NextLook(true),
            "a stint that ended as it began waits for the next look, so nobody starts and stops over and over");
        C(Math.Abs(IdleHoursMath.FilledShare(3000, 1000) - 0.75f) < 0.001f && IdleHoursMath.FilledShare(0, 0) == 0f && IdleHoursMath.FilledShare(0, 500) == 0f,
            "the log's share of idle time filled");

        // ---------- Spread over the colony ----------
        C(IdleHoursMath.PlaceScore(10f, 50f, false, 0, true, 0.5f) < IdleHoursMath.PlaceScore(10f, 50f, true, 0, true, 0.5f),
            "a Hospitality guest goes to another room rather than staying in the one they're in");
        C(IdleHoursMath.PlaceScore(30f, 50f, false, 0, true, 0.5f) < IdleHoursMath.PlaceScore(3f, 50f, true, 2, true, 0.5f),
            "the far side of the colony beats the crowded dining room");
        C(IdleHoursMath.PlaceScore(5f, 20f, false, 0, false, 0.5f) < IdleHoursMath.PlaceScore(15f, 20f, false, 0, false, 0.5f), "otherwise nearer first");
        C(IdleHoursMath.PlaceScore(5f, 20f, false, 0, false, 0.5f) < IdleHoursMath.PlaceScore(5f, 20f, false, 2, false, 0.5f), "and away from the others");
        C(IdleHoursMath.PlaceScore(5f, 20f, true, 0, true, 0f) - IdleHoursMath.PlaceScore(5f, 20f, true, 0, false, 0f) > 0.4f, "Hospitality's guests leave their room most");

        // ---------- The new doings ----------
        C(W(IdleDoing.Read, F(f => { f.book = true; f.age = 1; })) > 0f && W(IdleDoing.Read, F(f => { f.book = true; f.age = 0; })) == 0f
          && W(IdleDoing.Read, F(f => { f.ownBook = true; f.age = 0; })) > 0f, "the colony's books for the lettered ages, their own book for anyone who carries one");
        C(W(IdleDoing.Read, F(f => { f.book = true; f.child = true; })) == 0f && W(IdleDoing.Read, F(f => { f.book = true; f.hour = 2; })) == 0f
          && W(IdleDoing.Read, F(f => { f.book = true; f.calling = "scholars"; })) > W(IdleDoing.Read, F(f => f.book = true)), "not the children, not at night, scholars most");
        C(W(IdleDoing.Walk, F(f => f.walk = true)) > 0f && W(IdleDoing.Walk, F(f => { f.walk = true; f.rain = true; })) == 0f && W(IdleDoing.Walk, F(f => { f.walk = true; f.temperature = -5f; })) == 0f
          && W(IdleDoing.Walk, F(f => { f.walk = true; f.role = IdleRole.Trader; })) == 0f && W(IdleDoing.Walk, F(f => { f.walk = true; f.hour = 22; })) == 0f,
            "a walk by day in fair weather, never for the one minding the trade");
        C(W(IdleDoing.Walk, F(f => { f.walk = true; f.hour = 8; })) > W(IdleDoing.Walk, F(f => f.walk = true)), "a morning or an evening walk most");
        C(W(IdleDoing.Pet, F(f => f.pet = true)) > 0f && W(IdleDoing.Pet, F(f => { f.pet = true; f.child = true; })) > W(IdleDoing.Pet, F(f => f.pet = true))
          && W(IdleDoing.Pet, F(f => { f.pet = true; f.hour = 2; })) == 0f && W(IdleDoing.Pet, F(f => { f.pet = true; f.canHandle = false; })) == 0f,
            "the colony's pets petted by day, the children most");
        C(W(IdleDoing.Write, F(f => { f.table = true; f.age = 1; })) > 0f && W(IdleDoing.Write, F(f => { f.table = true; f.age = 0; })) == 0f && W(IdleDoing.Write, F(f => f.age = 1)) == 0f,
            "a letter at a table from the lettered ages on");
        C(W(IdleDoing.Train, F(f => f.trainTarget = true)) > W(IdleDoing.Train, F(f => f.openGround = true)) && W(IdleDoing.Train, F(f => f.openGround = true)) > 0f
          && W(IdleDoing.Train, F(f => { f.openGround = true; f.rain = true; })) == 0f && W(IdleDoing.Train, F()) == 0f,
            "training at the colony's target, or shadow sparring on open ground in fair weather");
        C(W(IdleDoing.Train, F(f => { f.trainTarget = true; f.calling = "mercenaries"; })) > W(IdleDoing.Train, F(f => f.trainTarget = true))
          && W(IdleDoing.Train, F(f => { f.trainTarget = true; f.child = true; })) == 0f && W(IdleDoing.Train, F(f => { f.trainTarget = true; f.role = IdleRole.Guard; })) == 0f,
            "the mercenaries train most; not the children, not a guard on watch");
        C(W(IdleDoing.Archery, F(f => { f.bow = true; f.archeryTarget = true; })) > 0f && W(IdleDoing.Archery, F(f => f.archeryTarget = true)) == 0f
          && W(IdleDoing.Archery, F(f => f.bow = true)) == 0f && W(IdleDoing.Archery, F(f => { f.bow = true; f.archeryTarget = true; f.rain = true; })) == 0f
          && W(IdleDoing.Archery, F(f => { f.bow = true; f.archeryTarget = true; f.calling = "hunters"; })) > W(IdleDoing.Archery, F(f => { f.bow = true; f.archeryTarget = true; })),
            "archery with their own bow at the colony's target, the hunters most");
        C(W(IdleDoing.Fish, F(f => f.water = true)) > 0f && W(IdleDoing.Fish, F(f => { f.water = true; f.calling = "fishers"; })) == 3f * W(IdleDoing.Fish, F(f => f.water = true))
          && W(IdleDoing.Fish, F(f => { f.water = true; f.fishingAllowed = false; })) == 0f && W(IdleDoing.Fish, F(f => { f.water = true; f.hour = 23; })) == 0f,
            "fishing where there's water, the river folk most, never against their faith or after dark");
        C(W(IdleDoing.Mend, F(f => f.worn = true)) > 0f && W(IdleDoing.Mend, F()) == 0f && W(IdleDoing.Mend, F(f => { f.worn = true; f.titled = true; })) == 0f,
            "a worn piece of their own clothes mended, not by a titled one");
        C(W(IdleDoing.Sharpen, F(f => f.weaponKind = 1)) > 0f && W(IdleDoing.Sharpen, F()) == 0f && W(IdleDoing.Sharpen, F(f => { f.weaponKind = 1; f.role = IdleRole.Guard; })) > 0f,
            "their own weapon seen to, a guard's too");
        C(W(IdleDoing.Cards, F(f => f.companion = true)) > 0f && W(IdleDoing.Cards, F(f => { f.companion = true; f.hour = 19; })) > W(IdleDoing.Cards, F(f => f.companion = true))
          && W(IdleDoing.Cards, F()) == 0f && W(IdleDoing.Cards, F(f => { f.companion = true; f.child = true; })) == 0f && W(IdleDoing.Cards, F(f => { f.companion = true; f.role = IdleRole.Guard; })) == 0f,
            "a game of their own with one of their own, of an evening most");
        C(W(IdleDoing.Sing, F(f => f.hour = 19)) > W(IdleDoing.Sing, F()) && W(IdleDoing.Sing, F(f => f.hour = 2)) == 0f && W(IdleDoing.Sing, F(f => f.canTalk = false)) == 0f,
            "a song of their own of an evening, never when people sleep");
        var tired = F(f => { f.hour = 23; f.rest = 0.6f; });
        C(W(IdleDoing.Sleep, tired) > 0f && W(IdleDoing.Sleep, F(f => { f.hour = 14; f.rest = 0.6f; })) == 0f && W(IdleDoing.Sleep, F(f => f.hour = 23)) == 0f,
            "a tired visitor sleeps at night, not by day, not when rested");
        foreach (var g in new[] { IdleGroup.Hospitality, IdleGroup.RimfolkVisit, IdleGroup.Envoys })
            C(IdleHoursMath.NotFor(IdleDoing.Sleep, F(f => { f.group = g; f.hour = 23; f.rest = 0.6f; })) != null, $"{g}: their own beds or bedtime, not the ground");
        C(IdleHoursMath.NotFor(IdleDoing.Sleep, F(f => f.group = IdleGroup.Caravan)) == null, "a caravan's people sleep on the ground");
        var night = IdleHoursMath.Weights(F(f =>
        {
            f.hour = 2; f.rest = 0.6f; f.fire = true; f.seat = true; f.companion = true; f.colonist = true; f.game = true; f.book = true; f.age = 2; f.walk = true; f.pet = true;
            f.table = true; f.trainTarget = true; f.water = true; f.worn = true; f.weaponKind = 1; f.animal = true; f.art = true; f.ownDrug = true;
        }));
        var quiet = new[] { IdleDoing.Fire, IdleDoing.Warm, IdleDoing.Seat, IdleDoing.Shelter, IdleDoing.Stars, IdleDoing.Chat, IdleDoing.Sleep };
        C(IdleHoursMath.All.Where(d => night[(int)d] > 0f).All(d => quiet.Contains(d)) && night[(int)IdleDoing.Sleep] > night[(int)IdleDoing.Fire],
            "the night is quieter: they sleep, or sit by the fire, on a seat, under the stars, or talk a little");
        var day = IdleHoursMath.Weights(F(f => { f.seat = true; f.walk = true; f.age = 1; }));
        C(day.Sum() > 1.5f, "an ordinary afternoon has plenty for them (a seat, a walk, a pipe, whittling)");

        // ---------- Their own things ----------
        C(IdleHoursMath.WeaponKind(true, true, true, false) == 1 && IdleHoursMath.WeaponKind(true, true, false, false) == 0 && IdleHoursMath.WeaponKind(true, false, false, true) == 2
          && IdleHoursMath.WeaponKind(true, false, false, false) == 3 && IdleHoursMath.WeaponKind(false, false, false, false) == 0, "a blade sharpened, a club left alone, a bow oiled, a gun cleaned");
        C(IdleHoursMath.SharpenForm(1) == 0 && IdleHoursMath.SharpenForm(2) == 1 && IdleHoursMath.SharpenForm(3) == 2, "each seen to its own way, with its own words");
        C(IdleHoursMath.IsBow("Bow_Recurve", "Arrow_Recurve") && IdleHoursMath.IsBow("Bow_Short", null) && IdleHoursMath.IsBow("DankPyon_Crossbow", "Bolt")
          && !IdleHoursMath.IsBow("Gun_Revolver", "Bullet_Revolver") && !IdleHoursMath.IsBow("ElbowBlade", null), "bows and crossbows by their arrows or their names");
        C(IdleHoursMath.Worn(70, 100) && !IdleHoursMath.Worn(85, 100) && !IdleHoursMath.Worn(0, 0), "clothes below four fifths worn enough to mend");
        C(IdleHoursMath.MendBy(50, 100) == 10 && IdleHoursMath.MendBy(95, 100) == 5 && IdleHoursMath.MendBy(100, 100) == 0 && IdleHoursMath.MendBy(3, 5) == 1,
            "a stint of mending gives back a tenth, never past full");
        C(IdleHoursMath.IsPet(1f, -1f, 0.9f) && IdleHoursMath.IsPet(0f, 4f, 0.2f) && !IdleHoursMath.IsPet(0.6f, -1f, 2.4f) && !IdleHoursMath.IsPet(0.1f, -1f, 1f),
            "a dog, a cat or a nuzzler is a pet; a horse or a yak isn't");
        C(IdleHoursMath.PlaysFetch("Husky") && IdleHoursMath.PlaysFetch("LabradorRetriever") && IdleHoursMath.PlaysFetch("YorkshireTerrier") && !IdleHoursMath.PlaysFetch("Cat")
          && !IdleHoursMath.PlaysFetch(null), "a dog runs after a stick, a cat doesn't");
        C(IdleHoursMath.BookOk(true, 0f) && !IdleHoursMath.BookOk(true, 0.02f) && !IdleHoursMath.BookOk(false, 0f), "no dark tome read idly");
        C(IdleHoursMath.CardsForm(0, 0.9f) == 2 && IdleHoursMath.CardsForm(1, 0.1f) == 1 && IdleHoursMath.CardsForm(1, 0.9f) == 0 && IdleHoursMath.CardsForm(3, 0.9f) == 0,
            "a tribal people's game of stones, dice among the medieval, cards");
        foreach (var t in IdleHoursMath.SparTargets.Concat(IdleHoursMath.ArcheryTargets))
            C(Pool(root).Contains(t), $"{t}: a training target in the player's mods (docs/player-mods, active)");
        C(Pool(root).Contains("Mote_ArcheryArrow"), "Misc. Training's arrow, to show the arrows flying, is in the player's mods");
        C(IdleHoursMath.IsSparTarget("MartialArtsTarget") && IdleHoursMath.IsArcheryTarget("ArcheryTarget") && !IdleHoursMath.IsSparTarget("ArcheryTarget"), "a target to strike, a target to shoot at");

        // ---------- The words ----------
        string poolFile = Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Pool_VisitorIdle.xml");
        C(File.Exists(poolFile), "the pool of what visitors say exists");
        var lines = File.Exists(poolFile) ? XDocument.Load(poolFile).Descendants("li").Select(li => li.Value).ToList() : new System.Collections.Generic.List<string>();
        int Said(string moment) => lines.Count(l => Regex.IsMatch(l, @"^\[" + moment + @"(,|\])"));
        foreach (var d in IdleHoursMath.All)
        {
            foreach (bool drink in new[] { false, true })
                for (int form = 0; form < IdleHoursMath.Forms(d); form++)
                {
                    string m = IdleHoursMath.Moment(d, drink, false, form);
                    if (d == IdleDoing.None || d == IdleDoing.Chat || d == IdleDoing.Nap || d == IdleDoing.Sleep) C(m == null, $"{d}: nothing said (the game's own chats, asleep)");
                    else C(m != null && Said(m) >= 2, $"{d}, form {form}: a moment of its own ({m}) with lines for it");
                }
        }
        C(Said("giveway") >= 2, "a word to one of the colony's people they give way to");
        C(Said("catch") >= 2, "a word now and then when a fish comes out");
        C(IdleHoursMath.Moment(IdleDoing.Read, false, false, IdleHoursMath.ReadOwn) == "readown" && IdleHoursMath.Moment(IdleDoing.Walk, false, false, IdleHoursMath.WalkGrave) == "grave"
          && IdleHoursMath.Moment(IdleDoing.Cards, false, false, 2) == "stones" && IdleHoursMath.Moment(IdleDoing.Sharpen, false, false, 2) == "gun",
            "each form its own words: their own book, a grave, a game of stones, a gun cleaned");
        C(IdleHoursMath.LineChance(IdleDoing.Sleep) == 0f && IdleHoursMath.LineChance(IdleDoing.Pet) > IdleHoursMath.LineChance(IdleDoing.Read), "a word to a pet more often; nothing said asleep");
        // Every line one plain sentence, no exclamation marks or dashes (the player's rule on plain text, ImmersionTests).
        foreach (var l in lines)
        {
            string text = Regex.Replace(l, @"^\[[^\]]*\]\s*", "");
            C(!text.Contains("!") && !text.Contains("—") && Regex.Matches(text.TrimEnd('.', '?'), @"[.?]\s").Count == 0, $"one plain sentence: \"{text}\"");
        }
        C(IdleHoursMath.Moment(IdleDoing.OwnDrug, true) == "drink" && IdleHoursMath.Moment(IdleDoing.OwnDrug, false) == "smoke", "a drink or a smoke, by what it is");
        C(IdleHoursMath.Moment(IdleDoing.Game, false, true) == "watch" && IdleHoursMath.Moment(IdleDoing.Game, false) == "game" && Said("watch") >= 2,
            "a screen or a radio is watched, with words of its own, not played");
        C(IdleHoursMath.LineChance(IdleDoing.Company) > IdleHoursMath.LineChance(IdleDoing.Fire) && IdleHoursMath.LineChance(IdleDoing.Chat) == 0f && IdleHoursMath.LineChance(IdleDoing.Nap) == 0f,
            "a greeting to one of the colony's people more often than a remark at the fire");
        C(IdleHoursMath.MaySpeak(-1, -1, 1000), "the first line may be said");
        C(!IdleHoursMath.MaySpeak(1000, -1, 1000 + IdleHoursMath.LinePawnGap - 1) && IdleHoursMath.MaySpeak(1000, -1, 1000 + IdleHoursMath.LinePawnGap), "one line from a visitor in six hours");
        C(!IdleHoursMath.MaySpeak(-1, 1000, 1000 + IdleHoursMath.LineMapGap - 1) && IdleHoursMath.MaySpeak(-1, 1000, 1000 + IdleHoursMath.LineMapGap), "one on a map in half an hour");
        C(IdleHoursMath.MaySpeak(5000, 5000, 100), "time turned back (a reload): lines may be said");

        // ---------- The code's seams ----------
        string dir = Path.Combine(root, "Source", "ColonyCouncil", "Visitors");
        string jobs = File.ReadAllText(Path.Combine(dir, "IdleHoursJobs.cs"));
        string idle = File.ReadAllText(Path.Combine(dir, "IdleHours.cs"));
        C(jobs.Contains("[HarmonyPatch(typeof(ThinkNode_Duty), nameof(ThinkNode_Duty.TryIssueJobPackage))]") && jobs.Contains("[HarmonyPriority(Priority.Low)]"),
            "the hook is on the duty's job, after the market's");
        C(jobs.Contains("job.def == JobDefOf.GotoWander || job.def == JobDefOf.Wait_Wander") && jobs.Contains("pawn.Faction.IsPlayer"),
            "only an idle wander becomes an idle stint, and never a colonist's");
        C(jobs.Contains("job.def == JobDefOf.StandAndBeSociallyActive") && idle.Contains("if (why == null && standing && group != IdleGroup.Hospitality) return null;"),
            "Hospitality's standing about (its own idle moment, after its recreation) is filled for its guests only");
        C(idle.Contains("AccessTools.Field(guestComp, \"guestArea_int\")") && idle.Contains("(l.area == null || c.InBounds(l.map) && l.area[c])"),
            "Hospitality's guest area (found by name, as the market finds it) decides where its guests may go");
        C(idle.Contains("IdleHoursMath.PlaceScore(") && idle.Contains("Spread(l, l.seats") && idle.Contains("Spread(l, list,"), "the seats, the games and the rest picked over the colony by the spread");
        C(jobs.Contains("PutBookAway(") && jobs.Contains("bc.GetDirectlyHeldThings()") && jobs.Contains("ThingPlaceMode.Direct"),
            "a book of the colony's goes back into its bookcase or onto the cell it lay on");
        C(!Regex.IsMatch(jobs, @"OnBookReadTick|BookUtility|\.Learn\(|GainResearch"), "a book read idly gives nothing to the colony (no research, no lessons)");
        C(Regex.IsMatch(jobs, @"void Catch\(\)[\s\S]*?pawn\.inventory\.innerContainer\.TryAdd\(t\)") && !Regex.IsMatch(jobs, @"void Catch\(\)[\s\S]*?Notify_Fished"),
            "the catch goes into their own pack, the colony's fish stock left as it was");
        C(!Regex.IsMatch(jobs, @"Tame|TrainableUtility|training\.Train|\.training\."), "the colony's pets are never tamed or trained, only petted or thrown a stick");
        C(Regex.IsMatch(jobs, @"void Fetch\(\)[\s\S]*?JobDefOf\.Goto"), "a dog fetches by the game's own going somewhere");
        C(jobs.Contains("IdleHours.ArrowMote") && idle.Contains("GetNamedSilentFail(\"Mote_ArcheryArrow\")") && idle.Contains("if (f.weaponKind == 2 && ArrowMote != null)"),
            "no archery where nothing would show the arrows (Misc. Training's arrow)");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_VisitorIdle.xml"));
        // Whole keys only; a key with its form added (Walk0 to Walk3 and the like) is checked below.
        foreach (Match m in Regex.Matches(jobs, "\"(CC_Idle_Report_\\w+)\"(?!\\s*\\+)"))
            C(keyed.Contains("<" + m.Groups[1].Value + ">"), $"{m.Groups[1].Value}: the words are there");
        foreach (var k in new[] { "Walk0", "Walk1", "Walk2", "Walk3", "Sharpen0", "Sharpen1", "Sharpen2", "Cards0", "Cards1", "Cards2" })
            C(keyed.Contains("<CC_Idle_Report_" + k + ">"), $"CC_Idle_Report_{k}: the words for each form");
        C(jobs.Contains("idle.lord = job.lord;") && jobs.Contains("idle.dutyTag = job.dutyTag;"), "the stint keeps the lord's own lord and duty tag");
        C(Regex.IsMatch(jobs, @"class JobDriver_VisitIdle[\s\S]*?TryMakePreToilReservations\(bool errorOnFailed\) => true;"), "nothing reserved: the colony's people find it all free");
        C(jobs.Contains("GiveWayIfWanted()") && idle.Contains("rm.TryGetReserver(t, player, out who)") && idle.Contains("FirstReserverOf(c, player)"),
            "a visitor gives way the moment one of the colony's people reserves the thing or the place, or walks to it");
        C(idle.Contains("if (l.facts.role == IdleRole.Trader || p.inventory?.innerContainer == null) return;"), "a trader's pack is the trade's goods, never eaten or drunk");
        C(jobs.Contains("inv.TryAdd(taken)") && !jobs.Contains("GenPlace.TryPlaceThing"), "a portion not finished goes back into the pack, never onto the colony's ground");
        C(idle.Contains("KeepOut.KeptFrom(") && idle.Contains("RoomOk("), "kept rooms, cells, sickrooms and bedrooms are no place to idle");
        C(idle.Contains("if (!spot && l.places != null && l.places.workCells.Contains(map.cellIndices.CellToIndex(c))) return false;"),
            "nobody idles on a cook's or a crafter's spot (the campfire's, the bench's)");
        C(Regex.IsMatch(jobs, @"class JobDriver_VisitNap[\s\S]*?public override bool LookForOtherJobs => false;"), "a nap isn't cut short by the duty's wander");
        C(idle.Contains("IdleHoursMath.SitByFire(Sauna.IsFire(def)") && idle.Contains("art.Props.canBeEnjoyedAsArt"), "fires and art told as the rules and the game's own looking at art tell them");
        C(idle.Contains("VisitorTrade.HeldByStory(p)"), "a story's visitor is left to the story");
        C(idle.Contains("public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.visitorsIdle ?? true);"), "under the setting and the deeper game hooks");
        string settings = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "CouncilSettings.cs"));
        C(settings.Contains("public bool visitorsIdle = true;") && settings.Contains("Scribe_Values.Look(ref visitorsIdle, \"visitorsIdle\", true);"), "the setting is kept");

        // ---------- The jobs ----------
        var defs = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_VisitorIdle.xml")).Root!;
        var jobDefs = defs.Elements("JobDef").Where(j => (string)j.Attribute("Abstract") != "True").ToList();
        var baseDef = defs.Elements("JobDef").FirstOrDefault(j => (string)j.Attribute("Name") == "Rimfolk_IdleBase");
        C(baseDef?.Element("casualInterruptible")?.Value == "true", "every stint can be interrupted casually: the trader can still trade, anyone be talked to");
        var doings = jobDefs.SelectMany(j => j.Descendants("doing")).Select(x => x.Value).ToList();
        foreach (var x in doings) C(Enum.TryParse<IdleDoing>(x, out _), $"the job for {x} names a doing");
        C(doings.Count == doings.Distinct().Count(), "each doing has one job");
        foreach (var d in IdleHoursMath.All.Where(d => d != IdleDoing.None && d != IdleDoing.Nap && d != IdleDoing.Play))
            C(doings.Contains(d.ToString()), $"{d} has a job");
        var names = jobDefs.Select(j => j.Element("defName")?.Value).ToHashSet();
        foreach (Match m in Regex.Matches(idle, @"public static JobDef (Rimfolk_Idle\w+);"))
            C(names.Contains(m.Groups[1].Value), $"{m.Groups[1].Value} exists");
        C(names.Contains("Rimfolk_IdleNap") && names.Contains("Rimfolk_IdlePlay"), "the nap and the children's play have their own jobs");
        foreach (var j in jobDefs.Where(j => j.Element("driverClass") != null))
            C(jobs.Contains("class " + j.Element("driverClass")!.Value.Replace("ColonyCouncil.", "") + " "), $"{j.Element("defName")?.Value}: its driver exists");
        foreach (var e in defs.Descendants("effecter"))
            C(e.Value == "Smoke_Joint" || defs.Elements("EffecterDef").Any(x => x.Element("defName")?.Value == e.Value), $"the effect {e.Value} is the game's or defined here");
        return n;
    }
}
