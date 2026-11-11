using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The gods in the wider world (docs/design/gods.md, stage 2's "The world": brainstorm items 17, 18, 19, 21 and 22, and the
/// drama between the gods): the pure rules (how often each thing comes by how real the gods are, what a letter tells of a
/// god, the slight, shared gods and the feud, holy places, the shapes of anger and kindness by domain, nature and kind,
/// the gifts by domain with A RimWorld of Magic's classes, spells and gifted traits and Vanilla Psycasts Expanded's paths,
/// the prices, the trickster's turns, the quarrel's turns, the myths' stages, the Hunger's bargains and takings, an
/// archotech's terms, the fading god, the newborn's signs), the defs (the gifts on a body, the camps, the ways keyed on a god,
/// the lines and the letters' pieces), and the seams: it calls only the core's Gods API and the two presence seams, keeps
/// its questions to Tell.Ask, its hurts to the shared budget, and its hooks small.
/// </summary>
public static class GodWorldTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the gods in the world: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string world = string.Join("\n", Directory.GetFiles(Path.Combine(src, "Gods", "World"), "*.cs").Select(File.ReadAllText));
        var D = GodsMath.Domains;

        // ================= How often =================

        C(GodWorldMath.Realness(GodRealness.Believed) < GodWorldMath.Realness(GodRealness.Felt) && GodWorldMath.Realness(GodRealness.Felt) < GodWorldMath.Realness(GodRealness.Walking),
            "the gods act more often the more real they are");
        C(!GodWorldMath.Supernatural(GodRealness.Believed) && GodWorldMath.Supernatural(GodRealness.Felt) && GodWorldMath.Supernatural(GodRealness.Walking)
          && GodWorldMath.Factor(GodRealness.Believed, true) == 0f && GodWorldMath.Factor(GodRealness.Believed, false) > 0f,
            "what only a real god does never comes while they are only believed");
        C(GodWorldMath.DayChance(0f) == 0f && GodWorldMath.DayChance(-3f) == 0f && GodWorldMath.DayChance(float.NaN) == 0f
          && GodWorldMath.DayChance(10f) > GodWorldMath.DayChance(100f) && Near(GodWorldMath.DayChance(100f), 1f - (float)Math.Exp(-0.01), 1e-5f),
            "a day's chance of something that comes every so many days");
        C(GodWorldMath.Gap(100, -1, 30f) && !GodWorldMath.Gap(29 * GodWorldMath.DayTicks, 0, 30f) && GodWorldMath.Gap(30 * GodWorldMath.DayTicks, 0, 30f), "gaps between two of a kind");
        foreach (var (name, mean, gap) in new[]
                 {
                     ("an invitation", GodWorldMath.InviteDays, GodWorldMath.InviteGap), ("a temple's envoy", GodWorldMath.EnvoyDays, GodWorldMath.EnvoyGap),
                     ("a holy war", GodWorldMath.HolyWarDays, GodWorldMath.HolyWarGap), ("pilgrims", GodWorldMath.PilgrimDays, GodWorldMath.PilgrimGap),
                     ("a seer", GodWorldMath.SeerDays, GodWorldMath.SeerGap), ("a gift", GodWorldMath.GiftDays, GodWorldMath.GiftGap),
                     ("a quarrel", GodWorldMath.QuarrelDays, GodWorldMath.QuarrelGap), ("a god's ask", GodWorldMath.AskDays, GodWorldMath.AskGap),
                     ("an archotech's terms", GodWorldMath.BargainDays, GodWorldMath.BargainGap), ("the Sleeper", GodWorldMath.SleeperDays, GodWorldMath.SleeperGap)
                 })
            C(mean >= 60f && gap >= 30f && gap <= mean, $"{name} is rare: every {mean} days or so, never within {gap}");
        C(GodWorldMath.ChampionShare > 0f && GodWorldMath.ChampionShare <= 0.25f && GodWorldMath.ChampionGap >= 10f, "a champion leads only some raids");

        // ================= Letters and diplomacy =================

        C(GodWorldMath.LetterTells(true, true, true, true, 0.1f) == "holywar" && GodWorldMath.LetterTells(true, true, false, true, 0.1f) == "curse"
          && GodWorldMath.LetterTells(true, false, false, true, 0.1f) == "miracle" && GodWorldMath.LetterTells(false, false, false, true, 0.1f) == "myth"
          && GodWorldMath.LetterTells(false, false, false, false, 0.1f) == null && GodWorldMath.LetterTells(true, true, true, true, 0.99f) == null,
            "a letter tells of a holy war, a curse, a sign or a myth by what is true at their town, and not every letter");
        C(!GodWorldMath.Slights(true, true, true, 0f) && GodWorldMath.Slights(true, false, false, 0.3f) && !GodWorldMath.Slights(false, true, false, 0.3f)
          && GodWorldMath.Slights(false, true, false, 0.1f) && !GodWorldMath.Slights(false, false, false, 0f),
            "the colony's letter slights a god only from a rival's faithful or a scoffer, now and then, never in condolences");
        C(GodWorldMath.SlightGoodwill < 0 && GodWorldMath.SlightFavour < 0f, "a slight costs goodwill and the god's favor");
        C(GodWorldMath.SharedGoodwill(FavourWord.Displeased) == 0 && GodWorldMath.SharedGoodwill(FavourWord.Noticed) == 1 && GodWorldMath.SharedGoodwill(FavourWord.Beloved) == 2
          && GodWorldMath.FeudGoodwill(FavourWord.Pleased) == 0 && GodWorldMath.FeudGoodwill(FavourWord.Favoured) < 0 && GodWorldMath.FeudFloor >= 0,
            "peoples who share a god get on better; a feud that is the gods' cools them a little, never into war");
        C(GodWorldMath.HolyWarStand > GodWorldMath.HolyWarHelp && GodWorldMath.HolyWarHelp > 0 && GodWorldMath.HolyWarOut < 0 && GodWorldMath.HolyWarEnemy < 0,
            "a holy war: standing with them weighs most, help less, staying out costs a little");
        C(GodWorldMath.Gift(1f, 120) == 120 && GodWorldMath.Gift(10f, 120) == 300 && GodWorldMath.Gift(0.1f, 120) == 60 && GodWorldMath.Gift(1f, 10) == 50,
            "a gift's silver by the colony's wealth, within bounds, rounded to ten");
        C(GodWorldMath.RelicGiven > GodWorldMath.RelicLent && GodWorldMath.RelicRefused < 0, "a relic given, prayed over, refused");

        // ================= Peoples and camps =================

        foreach (var d in D)
        {
            string s = GodWorldMath.SiteKind(d);
            C(s == null || GodWorldMath.SiteKinds.Contains(s), $"a holy place of a kind there is for {d}");
        }
        C(GodWorldMath.SiteKind(GodDomain.Wild) == "tree" && GodWorldMath.SiteKind(GodDomain.Waters) == "water" && GodWorldMath.SiteKind(GodDomain.Machine) == "wreck"
          && GodWorldMath.SiteKind(GodDomain.Dead) == "grave", "an old tree, a spring or a bank, a crash site, the graves");
        C(GodWorldMath.ChampionGodTakes(GodNature.Proud) > 0f && GodWorldMath.ChampionGodTakes(GodNature.Patient) < 0f && GodWorldMath.ChampionFell > 0f,
            "a champion's fall: its rivals pleased, a proud war god impressed, others displeased");

        // ================= Health and wrath =================

        C(GodWorldMath.BlessingChance(FavourWord.Unknown, GodKind.Ordinary) == 0f && GodWorldMath.BlessingChance(FavourWord.Displeased, GodKind.Ordinary) == 0f
          && GodWorldMath.BlessingChance(FavourWord.Noticed, GodKind.Ordinary) > 0f
          && GodWorldMath.BlessingChance(FavourWord.Beloved, GodKind.Ordinary) > GodWorldMath.BlessingChance(FavourWord.Pleased, GodKind.Ordinary)
          && GodWorldMath.BlessingChance(FavourWord.Beloved, GodKind.Ordinary) <= 0.2f
          && GodWorldMath.BlessingChance(FavourWord.Beloved, GodKind.Dead) < GodWorldMath.BlessingChance(FavourWord.Beloved, GodKind.Ordinary),
            "a fever broken now and then by the colony's standing, never below noticed, the dead hardly answering");
        C(GodWorldMath.BlessingEase > 0f && GodWorldMath.BlessingEase < 0.3f && GodWorldMath.BlessingFloor > 0f && GodWorldMath.BlessingHour(2) && !GodWorldMath.BlessingHour(12),
            "it eases, never cures outright, in the small hours");
        C(GodWorldMath.WrathOf(GodDomain.Storm, GodNature.Wrathful) == GodWrath.Lightning && GodWorldMath.WrathOf(GodDomain.Harvest, GodNature.Proud) == GodWrath.Blight
          && GodWorldMath.WrathOf(GodDomain.Wild, GodNature.Jealous) == GodWrath.Beasts && GodWorldMath.WrathOf(GodDomain.Night, GodNature.Grieving) == GodWrath.Eclipse
          && GodWorldMath.WrathOf(GodDomain.Storm, GodNature.Patient) == GodWrath.None && GodWorldMath.WrathOf(GodDomain.War, GodNature.Cold) == GodWrath.None,
            "anger by domain: lightning, blight, beasts, the sun put out; a patient or cold god waits");
        C(GodWorldMath.WrathDays(FavourWord.Unknown, GodNature.Wrathful, GodKind.Ordinary) == 0f && GodWorldMath.WrathDays(FavourWord.Displeased, GodNature.Patient, GodKind.Ordinary) == 0f
          && GodWorldMath.WrathDays(FavourWord.Displeased, GodNature.Wrathful, GodKind.Ordinary) < GodWorldMath.WrathDays(FavourWord.Displeased, GodNature.Generous, GodKind.Ordinary)
          && Near(GodWorldMath.WrathDays(FavourWord.Wrathful, GodNature.Proud, GodKind.Ordinary), GodWorldMath.WrathDays(FavourWord.Displeased, GodNature.Proud, GodKind.Ordinary) / 2f, 1e-4f)
          && GodWorldMath.WrathDays(FavourWord.Wrathful, GodNature.Wrathful, GodKind.Ordinary) >= 10f
          && GodWorldMath.WrathDays(FavourWord.Wrathful, GodNature.Wrathful, GodKind.Archotech) == 0f && GodWorldMath.WrathDays(FavourWord.Wrathful, GodNature.Wrathful, GodKind.Dead) == 0f,
            "a god displeased acts now and then by its nature, a wrathful one soonest, never above displeased, never a cold mind or a dead god");
        C(GodWorldMath.BeastCount(GodNature.Wrathful) <= 3 && GodWorldMath.BeastCount(GodNature.Patient) >= 2 && GodWorldMath.BeastsAfterDays >= 1 && GodWorldMath.BeastsDayChance < 0.5f
          && GodWorldMath.GrudgeDays <= 20, "the beasts: two or three, after a day or two, while the grudge lasts");
        C(GodWorldMath.LightningClear >= 3f && GodWorldMath.LightningHourChance < 0.2f, "the lightning lands clear of anyone");

        // ================= Gifts =================

        var design = new Dictionary<GodDomain, string[]>
        {
            [GodDomain.Healing] = new[] { "Priest", "Paladin" }, [GodDomain.Wild] = new[] { "Druid" }, [GodDomain.Hunt] = new[] { "Ranger" },
            [GodDomain.Dead] = new[] { "Necromancer", "DeathKnight" }, [GodDomain.Void] = new[] { "Necromancer", "DeathKnight" }, [GodDomain.Storm] = new[] { "StormBorn" },
            [GodDomain.Craft] = new[] { "Enchanter" }, [GodDomain.Machine] = new[] { "Technomancer" }, [GodDomain.Night] = new[] { "TM_Bard" },
            [GodDomain.War] = new[] { "Gladiator", "TM_Monk" }
        };
        foreach (var kv in design)
            C(GodWorldMath.ClassesFor(kv.Key, GodDomain.None).SequenceEqual(kv.Value), $"the design's class for {kv.Key}: {string.Join(" or ", kv.Value)}");
        C(GodWorldMath.ClassesFor(GodDomain.Craft, GodDomain.Dead).SequenceEqual(new[] { "Geomancer" }), "the Deep Mother (craft and the dead) makes geomancers");
        foreach (var d in D)
        {
            foreach (var t in GodWorldMath.ClassesFor(d, GodDomain.None))
                C(PeoplesGearMath.MagicSetting(t) != null && PeoplesGearMath.Might(t) == (t == "Ranger" || t == "DeathKnight" || t == "Gladiator" || t == "TM_Monk" || t == "TM_Wayfarer"),
                    $"{t} has its switch in A RimWorld of Magic's settings, and fights or casts as the mod has it");
            C(GodWorldMath.SpellsFor(d).Length > 0 && GodWorldMath.SpellsFor(d).All(x => x.StartsWith("SpellOf_")), $"spells for {d}");
            C(GodWorldMath.PathsFor(d).Length > 0 && GodWorldMath.PathsFor(d).All(x => Regex.IsMatch(x, "^(VPE|VPEH|VPEP)_")), $"Vanilla Psycasts Expanded paths for {d}");
            C(GodWorldMath.HandOf(d) == "Rimfolk_GodGift_" + d, $"a blessing on a body for {d}");
        }
        C(GodWorldMath.HandOf(GodDomain.None) == null && GodWorldMath.ManaTraits(false).Length >= 3 && GodWorldMath.ManaTraits(true).Length >= 2
          && GodWorldMath.ManaTraits(false).Concat(GodWorldMath.ManaTraits(true)).All(t => t.StartsWith("TM_") && t.EndsWith("TD")), "the gifted traits for those who cast and those who fight");
        C(GodWorldMath.PickGift(false, false, false, false, false, false, false, 0.5f) == GodGiftKind.None
          && GodWorldMath.PickGift(true, false, false, false, false, false, false, 0.99f) == GodGiftKind.Hand
          && GodWorldMath.PickGift(false, false, true, false, false, false, false, 0f) == GodGiftKind.Class, "only what can be given is picked");
        var picks = Enumerable.Range(0, 200).Select(i => GodWorldMath.PickGift(true, true, true, true, true, true, true, i / 200f)).ToList();
        C(picks.Count(k => k == GodGiftKind.Hand) > picks.Count(k => k == GodGiftKind.Relic) && Enum.GetValues(typeof(GodGiftKind)).Cast<GodGiftKind>().Where(k => k != GodGiftKind.None).All(picks.Contains),
            "every gift can come, a blessing on the body likeliest and a relic rarest");
        C(GodWorldMath.PriceOf(GodNature.Patient, GodKind.Hunger, GodDomain.Void, 0.9f) == GodPrice.Years && GodWorldMath.PriceOf(GodNature.Wrathful, GodKind.Archotech, GodDomain.War, 0.1f) == GodPrice.Offering
          && GodWorldMath.PriceOf(GodNature.Generous, GodKind.Ordinary, GodDomain.War, 0.1f) == GodPrice.Blood && GodWorldMath.PriceOf(GodNature.Jealous, GodKind.Ordinary, GodDomain.Harvest, 0.1f) == GodPrice.Rival,
            "every gift has a price by nature and kind: the Hunger years, an archotech goods, war blood, a jealous god its rivals' anger");
        C(Enumerable.Range(0, 20).All(i => GodWorldMath.PriceYears(GodKind.Ordinary, i / 20f) >= 3 && GodWorldMath.PriceYears(GodKind.Ordinary, i / 20f) <= 6)
          && GodWorldMath.PriceYears(GodKind.Hunger, 0f) > GodWorldMath.PriceYears(GodKind.Ordinary, 0f), "a few years, more for the Hunger");
        C(GodWorldMath.PriceSilver(GodGiftKind.Class, 1f) > GodWorldMath.PriceSilver(GodGiftKind.Hand, 1f) && GodWorldMath.PriceSilver(GodGiftKind.Hand, 0.1f) >= 100
          && GodWorldMath.PriceSilver(GodGiftKind.Relic, 1f) % 10 == 0, "an offering's silver by the gift and the colony's wealth");
        C(GodWorldMath.GiftRefused(GodNature.Proud) < 0f && GodWorldMath.GiftRefused(GodNature.Generous) == 0f && GodWorldMath.GiftTaken > 0f, "a gift refused galls only a proud, jealous or wrathful god");
        C(!GodWorldMath.TricksterTurnsToday(1, false, 0, 0f) && GodWorldMath.TricksterTurnsToday(GodWorldMath.TricksterFirstDays, false, 0, 0f)
          && !GodWorldMath.TricksterTurnsToday(30, true, GodWorldMath.TricksterTurns, 0f) && !GodWorldMath.TricksterTurnsToday(30, false, 0, 0.5f),
            "a trickster's gift turns after a few days, now and then, a few times at most");
        C(GodWorldMath.UndoAfterDays < GodWorldMath.UndoUntilDays && GodWorldMath.UndoDayChance < 0.1f && GodWorldMath.UndoSentOff < 0f && GodWorldMath.UndoLetGo > 0f,
            "a rival comes to undo a gift rarely, within a few weeks of it");

        // ================= The drama =================

        C(GodWorldMath.WeatherQuarrel(GodDomain.Storm, GodDomain.Harvest) && GodWorldMath.WeatherQuarrel(GodDomain.Waters, GodDomain.Storm) && !GodWorldMath.WeatherQuarrel(GodDomain.Harvest, GodDomain.Waters)
          && !GodWorldMath.WeatherQuarrel(GodDomain.Storm, GodDomain.Storm), "a quarrel in the weather: the sky's god against the fields', the waters' or the hearth's");
        C(GodWorldMath.QuarrelTurn(0) == 0 && GodWorldMath.QuarrelTurn(GodWorldMath.QuarrelTurnDays) == 1 && GodWorldMath.QuarrelTurn(2 * GodWorldMath.QuarrelTurnDays) == 0
          && GodWorldMath.QuarrelLastsDays >= 4 * GodWorldMath.QuarrelTurnDays, "storm and dry spell in turns, several turns each");
        C(GodWorldMath.MythStage(0) == 0 && GodWorldMath.MythStage(1) == 1 && GodWorldMath.MythStage(5) == 2, "a myth moves on as the ties' history grows");
        C(GodWorldMath.SupperHour(19) && !GodWorldMath.SupperHour(12) && GodWorldMath.MythEveningChance < 0.3f && GodWorldMath.MythGapDays >= 2, "myths at supper, now and then");

        // ================= The Hunger =================

        C(GodWorldMath.Desperation(false, false, false) == 0 && GodWorldMath.Desperation(true, false, false) == 3 && GodWorldMath.Desperation(false, true, true) == 3
          && GodWorldMath.Desperation(false, true, false) == 1, "desperation: dying, starving, at the edge");
        C(GodWorldMath.HungerHears(0) == 0f && GodWorldMath.HungerHears(1) < GodWorldMath.HungerHears(2) && GodWorldMath.HungerHears(2) < GodWorldMath.HungerHears(3) && GodWorldMath.HungerHears(3) < 0.5f,
            "it hears the desperate, the more desperate the likelier, never always");
        C(GodWorldMath.HungerGapDays(0) > GodWorldMath.HungerGapDays(3) && GodWorldMath.HungerGapDays(99) >= 15f, "its next whisper comes sooner each time, never sooner than a fortnight");
        C(GodWorldMath.HungerTake(0) == "mark" && GodWorldMath.HungerTake(1) == "years" && GodWorldMath.HungerTake(2) == "mind" && GodWorldMath.HungerTake(5) == "beast"
          && GodWorldMath.HungerTakeAfterDays(0) > GodWorldMath.HungerTakeAfterDays(3) && GodWorldMath.HungerTakeAfterDays(9) >= 1, "it always takes more, and sooner");

        // ================= The ones who don't fit =================

        C(GodWorldMath.TermCount(10f, 1f) == 40 && GodWorldMath.TermCount(1000f, 1f) == 1 && GodWorldMath.TermCount(0f, 1f) == 0 && GodWorldMath.TermCount(10f, 2f) > GodWorldMath.TermCount(10f, 1f),
            "an archotech's exact terms by the goods' worth");
        C(GodWorldMath.TermKept > 0f && GodWorldMath.TermBroken < -GodWorldMath.TermKept && GodWorldMath.TermReward > 1f, "kept, it rewards exactly; broken, it remembers");
        C(GodWorldMath.FadingGentle(GodNature.Grieving) && !GodWorldMath.FadingGentle(GodNature.Proud), "a fading god is gentle or bitter by its nature");
        C(GodWorldMath.NewbornSign(10f, 100f, 0) == -1 && GodWorldMath.NewbornSign(30f, 100f, 0) == 1 && GodWorldMath.NewbornSign(30f, 100f, 1) == -1 && GodWorldMath.NewbornSign(99f, 100f, 1) == 3,
            "a newborn's signs, one at each quarter of its growing, each once");
        C(GodWorldMath.SleeperSteps == 3 && GodWorldMath.VaultOpened < GodWorldMath.VaultGoodwill, "the Sleeper's door: three steps, the vault furious at the last");

        // ================= The defs =================

        var gifts = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodsWorld_Gifts.xml"));
        var hediffs = gifts.Root.Elements("HediffDef").Select(e => e.Element("defName")?.Value).Where(x => x != null).ToList();
        foreach (var d in D) C(hediffs.Contains(GodWorldMath.HandOf(d)), $"the blessing for {d} is a hediff");
        foreach (var h in new[] { "Rimfolk_GodCurse", "Rimfolk_GodMark", "Rimfolk_GodChampion", "Rimfolk_VoidCalm" }) C(hediffs.Contains(h), "the hediff " + h);
        C(gifts.Root.Elements("HediffDef").Where(e => (e.Element("defName")?.Value ?? "").StartsWith("Rimfolk_GodGift_")).All(e => e.Descendants("statOffsets").Elements().All(s => Math.Abs(float.Parse(s.Value, System.Globalization.CultureInfo.InvariantCulture)) <= 3f)),
            "the blessings are small");
        var ways = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodsWorld_Ways.xml"));
        var patrons = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_Pantheon_Patrons.xml")).Root.Elements().Select(e => e.Element("defName")?.Value).ToList();
        foreach (var w in ways.Root.Elements())
        {
            string name = w.Element("defName")?.Value;
            C(w.Element("gods")?.Elements("li").All(li => patrons.Contains(li.Value)) ?? w.Element("domain") != null, $"{name}: keyed on gods that are");
            foreach (var t in w.Element("classes")?.Elements("li").Select(x => x.Value) ?? Enumerable.Empty<string>()) C(PeoplesGearMath.MagicSetting(t) != null, $"{name}: a class with its switch, {t}");
            var site = w.Element("site")?.Value;
            C(site == null || GodWorldMath.SiteKinds.Contains(site), $"{name}: a holy place of a kind there is");
        }
        C(Read("Gods/World/GodWays.cs").Contains("public static IEnumerable<string> Keys(God g)") && Regex.Matches(world, @"GodWays\.(Classes|Spells|Paths|Beasts|WrathBeasts|Hand|Wrath|Price|Site|Feast|Weather|Tags)\(").Count >= 14,
            "every way of a god is read through its ways: by the god, then its domain");
        var camps = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodsWorld_Camps.xml"));
        foreach (var (def, worker) in new[] { ("Rimfolk_Camp_GodPilgrims", "CampWorker_GodPilgrims"), ("Rimfolk_Camp_GodSeer", "CampWorker_GodSeer") })
        {
            var k = camps.Root.Elements().FirstOrDefault(e => e.Element("defName")?.Value == def);
            C(k != null && k.Element("workerClass")?.Value == "ColonyCouncil." + worker && k.Element("weight")?.Value == "0", $"{def}: its own ways, never on the camps' own draw");
            C(k != null && k.Element("asks")?.Elements("li").All(a => a.Element("effects")?.Elements().Any(e => Regex.IsMatch(e.Attribute("Class")?.Value ?? "", "Gathering|Visit|CarryOut")) == true) == true,
                $"{def}: its asks play out on the map");
        }
        C(File.ReadAllText(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodsWorld_Letters.xml")).Contains("<letterClass>ColonyCouncil.ChoiceLetter_GodWorld</letterClass>"), "one letter for every god's question");

        // ================= The lines =================

        var lines = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodsWorld_Lines.xml"));
        List<string> Pool(string def) => lines.Root.Elements().First(e => e.Element("defName")?.Value == def).Descendants("li").Select(x => x.Value).ToList();
        foreach (var pool in new[] { "Rimfolk_Pool_GodWorld", "Rimfolk_Pool_GodMyth" })
            foreach (var l in Pool(pool))
            {
                string text = Regex.Replace(Regex.Replace(l, @"^\[[^\]]*\]\s*", ""), @"\{[A-Z_]+\}", "Xxxxxxxx");
                C(text.Length <= 90 && !Regex.IsMatch(text, @"[.!?]\s+\S") && !text.StartsWith("\"") && !text.Contains("!"), $"one sentence of 90 characters at most: {text}");
            }
        foreach (Match m in Regex.Matches(world, @"GodWorld\.(?:Say|Remark)\([^;]*?""([a-z]+)"""))
        {
            string tag = m.Groups[1].Value;
            C(Pool("Rimfolk_Pool_GodWorld").Any(l => Regex.IsMatch(l, @"^\[[^\]]*\b" + tag + @"\b")), $"lines for the moment {tag}");
        }
        foreach (var kind in Enum.GetNames(typeof(GodTieKind)))
            C(Pool("Rimfolk_Pool_GodMyth").Any(l => l.Contains("myth:" + kind.ToLowerInvariant())), "myths of " + kind.ToLowerInvariant());
        string story = File.ReadAllText(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Letters_Story.xml"));
        foreach (var sign in new[] { "miracle", "curse", "myth" })
            foreach (var part in new[] { "st:open", "st:turn", "st:end" })
            {
                C(Regex.Matches(story, @"<li>\[" + part + @",st:god,god:" + sign + @"\]").Count >= 2, $"a story of their god's {sign}: {part}");
                C(Regex.IsMatch(story, @"<li>\[olde," + part + @",st:god,god:" + sign + @"\]"), $"and in medieval English: {sign}, {part}");
            }
        string chron = File.ReadAllText(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Letters_Chronicle.xml"));
        string sermon = File.ReadAllText(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Letters_Sermon.xml"));
        foreach (var sign in new[] { "miracle", "curse", "holywar" })
        {
            C(chron.Contains("chs:god,god:" + sign), "a chronicle's item on their god's " + sign);
            C(sermon.Contains("god:" + sign + "]"), "a sermon on their god's " + sign);
        }

        // ================= The seams =================

        C(!Regex.IsMatch(world, @"\bGodBook\b") && !Regex.IsMatch(world, @"\bPantheon\.") && !Regex.IsMatch(world, @"\bGodsMath\.(Scale|Drift|Word)\("),
            "the world calls only the core's Gods API, never its book or its builder");
        C(world.Contains("GodPresenceSeam.Call(spec)") && world.Contains("GodLookSeam.Make(p, g, disguised)") && world.Contains("ColonyCouncil.Tell.Letter(NewsKind.World, importance"),
            "a god's moment through the presence seam, a plain card when it isn't there; an avatar through the look seam");
        C(!Regex.IsMatch(world, @"\bDilemmaDef\b|ChoiceLetter_Dilemma|DilemmaTracker") && world.Contains("Tell.Ask(letter, home)"), "its questions through Tell.Ask, not the dilemmas");
        C(world.Contains("Force || Tell.CanAsk") && Regex.Matches(world, @"GodWorld\.CanAsk").Count >= 8, "questions that can wait wait for Tell.CanAsk");
        C(Regex.Matches(world, @"// apart:").Count >= 5, "what happens apart says so");
        C(world.Contains("Harm.Roll(p, HarmKinds.GodBeasts") && world.Contains("Harm.Roll(HarmKinds.GodLightning") && world.Contains("Harm.Injure(p, HurtKind.Cut, HurtWhere.Arm, 3f, 6f, HarmKinds.GodPrice)")
          && world.Contains("Harm.Note(p, HarmKinds.GodBeasts)"), "its hurts through the shared harm budget");
        string budget = Read("Harm/HarmBudgetMath.cs");
        C(budget.Contains("\"godBeasts\"") && budget.Contains("\"godLightning\"") && budget.Contains("\"godPrice\"") && Read("Harm/Harm.cs").Contains("case \"gods\": return Gods.On;"),
            "its three kinds in the harm budget, on with the gods");
        C(world.Contains("GodWorld.Factor(true)") && world.Contains("GodWorldMath.Supernatural(Gods.Realness)"), "gifts of power and the void only where the gods are more than believed");
        C(Read("World/Region/RegionBook.cs").Contains("GodWorld.Daily(now)") && Read("World/Region/RegionBook.cs").Contains("GodWorld.Hourly(now)"), "kept in the region's day and hour");
        C(Read("Happenings/Happenings.cs").Contains("GodHealth.Killed(victim, dinfo, note.map);"), "a sacred beast's death heard");
        C(Read("Letters/LetterFormShapes.cs").Contains("GodLetters.Shape(s, pawn, f);") && Read("Letters/LetterFormShapes.cs").Contains("GodLetters.ShapeOurs(s, l, n, writer);")
          && Read("Letters/LetterForm.cs").Contains("GodLetters.Answered(s, l, n);"), "the letters' hooks: theirs, the colony's, and their answer to a slight");
        C(Read("UI/InboxProviders.cs").Contains("case ChoiceLetter_GodWorld g: return g.Waiting"), "a god's question waiting is in This week");
        string model = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        foreach (var coll in new[] { "gifts", "doings", "blessed", "lasts", "told", "raids" })
            C(model.Contains("\"Gods/World/GodWorldBook.cs:" + coll + "\""), $"the saved {coll} modelled in the save estimate");
        string book = Read("Gods/World/GodWorldBook.cs");
        C(book.Contains("public const int MaxGifts = 60, MaxDoings = 40, MaxBlessed = 40, MaxRaids = 20, MaxTold = 60;"), "everything it keeps is bounded");
        C(!Regex.IsMatch(File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_GodsWorld.xml")), @"\{(FAVOUR|FAVOR|VALUE)\}|\d+ favou?r"), "favor never a number in its words");
        C(Read("Gods/World/GodWorldDebug.cs").Contains("Gods in the world: show what is under way (log)"), "dev actions");
        return n;
    }

    private static bool Near(float a, float b, float eps) => Math.Abs(a - b) <= eps;
}
