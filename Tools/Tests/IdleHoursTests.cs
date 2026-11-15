using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Visitors' idle hours (Source/ColonyCouncil/Visitors/IdleHoursMath.cs, IdleHours.cs, IdleHoursJobs.cs): what fits whom by
/// their people's age, the hour, the weather, their calling and who they are; the colony's joy things by the game's joy
/// givers, free of the colony's people and of the visitor's age, the player's mod pool's games in and its baths and
/// shooting ranges out; Hospitality's guests filled only where its recreation has nothing; the stints, the reach, the
/// next look and the words; and the code's seams: the hook after the market's, nothing reserved and giving way, the
/// trader's pack, the leftovers, the jobs, the pool and the setting.
/// </summary>
public static class IdleHoursTests
{
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
            C(IdleHoursMath.ForHospitality(d) == (d == IdleDoing.Warm || d == IdleDoing.Shelter || d == IdleDoing.Pray || d == IdleDoing.Animal),
                $"{d}: for Hospitality's guests only where its recreation has nothing");
        C(IdleHoursMath.NotFor(IdleDoing.Game, F(f => f.group = IdleGroup.Hospitality)) != null && IdleHoursMath.NotFor(IdleDoing.Warm, F(f => f.group = IdleGroup.Hospitality)) == null,
            "Hospitality's guest plays by Hospitality's rules, warms their hands by Rimfolk's");
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
        C(IdleHoursMath.All.Where(d => bare[(int)d] > 0f).All(d => d == IdleDoing.Pipe || d == IdleDoing.Carve), "with nothing about, only what they carry themselves: a pipe, a bit of whittling");
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
        C(IdleHoursMath.All.Where(d => hosp[(int)d] > 0f).All(IdleHoursMath.ForHospitality) && hosp[(int)IdleDoing.Warm] > 0f && hosp[(int)IdleDoing.Animal] > 0f,
            "Hospitality's guest: only the fire in the cold and their animals here, the rest is Hospitality's");

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
        C(IdleHoursMath.Reach(IdleGroup.Caravan, IdleRole.Trader) < IdleHoursMath.Reach(IdleGroup.Caravan, IdleRole.Plain)
          && IdleHoursMath.Reach(IdleGroup.RimfolkVisit, IdleRole.Plain) < IdleHoursMath.Reach(IdleGroup.GameVisit, IdleRole.Plain)
          && IdleHoursMath.Reach(IdleGroup.GameVisit, IdleRole.Plain) < IdleHoursMath.Reach(IdleGroup.Envoys, IdleRole.Plain)
          && IdleHoursMath.Reach(IdleGroup.Hospitality, IdleRole.Plain) <= 6f && IdleHoursMath.Reach(IdleGroup.None, IdleRole.Plain) == 0f,
            "the trader near the trade, Rimfolk's visits close to their spot, envoys about the colony, Hospitality's guests nearly where they stand");
        foreach (var d in IdleHoursMath.All)
        {
            var (lo, hi) = IdleHoursMath.Stint(d);
            C(lo >= 120 && lo < hi && hi <= 3600, $"{d}: a stint of {lo} to {hi} ticks, an hour and a half at most");
        }
        C(IdleHoursMath.NextLook(true).max < IdleHoursMath.NextLook(false).min + 500 && IdleHoursMath.NextLook(true).min < IdleHoursMath.NextLook(false).min, "a short wander between stints, a longer one when nothing came of a look");

        // ---------- The words ----------
        string poolFile = Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Pool_VisitorIdle.xml");
        C(File.Exists(poolFile), "the pool of what visitors say exists");
        var lines = File.Exists(poolFile) ? XDocument.Load(poolFile).Descendants("li").Select(li => li.Value).ToList() : new System.Collections.Generic.List<string>();
        int Said(string moment) => lines.Count(l => Regex.IsMatch(l, @"^\[" + moment + @"(,|\])"));
        foreach (var d in IdleHoursMath.All)
        {
            foreach (bool drink in new[] { false, true })
            {
                string m = IdleHoursMath.Moment(d, drink);
                if (d == IdleDoing.None || d == IdleDoing.Chat || d == IdleDoing.Nap) C(m == null, $"{d}: nothing said (the game's own chats, asleep)");
                else C(m != null && Said(m) >= 2, $"{d}: a moment of its own ({m}) with lines for it");
            }
        }
        C(Said("giveway") >= 2, "a word to one of the colony's people they give way to");
        C(IdleHoursMath.Moment(IdleDoing.OwnDrug, true) == "drink" && IdleHoursMath.Moment(IdleDoing.OwnDrug, false) == "smoke", "a drink or a smoke, by what it is");
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
        C(jobs.Contains("job.def != JobDefOf.GotoWander && job.def != JobDefOf.Wait_Wander") && jobs.Contains("pawn.Faction.IsPlayer"),
            "only an idle wander becomes an idle stint, and never a colonist's");
        C(jobs.Contains("idle.lord = job.lord;") && jobs.Contains("idle.dutyTag = job.dutyTag;"), "the stint keeps the lord's own lord and duty tag");
        C(Regex.IsMatch(jobs, @"class JobDriver_VisitIdle[\s\S]*?TryMakePreToilReservations\(bool errorOnFailed\) => true;"), "nothing reserved: the colony's people find it all free");
        C(jobs.Contains("GiveWayIfWanted()") && idle.Contains("rm.TryGetReserver(t, player, out who)") && idle.Contains("FirstReserverOf(c, player)"),
            "a visitor gives way the moment one of the colony's people reserves the thing or the place, or walks to it");
        C(idle.Contains("if (l.facts.role == IdleRole.Trader || p.inventory?.innerContainer == null) return;"), "a trader's pack is the trade's goods, never eaten or drunk");
        C(jobs.Contains("inv.TryAdd(taken)") && !jobs.Contains("GenPlace.TryPlaceThing"), "a portion not finished goes back into the pack, never onto the colony's ground");
        C(idle.Contains("KeepOut.KeptFrom(") && idle.Contains("RoomOk("), "kept rooms, cells, sickrooms and bedrooms are no place to idle");
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
