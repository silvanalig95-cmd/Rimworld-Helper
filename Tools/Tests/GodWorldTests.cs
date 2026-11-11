using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The gods in the wider world (docs/design/gods.md, stage 2's "The world": brainstorm items 17, 18, 19, 21 and 22, and the
/// drama between the gods; the nine of docs/gods-pantheon-brainstorm.md; "The gods are unseen"): the pure rules (how often
/// each thing comes by how real the gods are, what a letter tells of a god, the slight, shared gods and the feud, holy
/// places, the shapes of anger and kindness, the gifts with A RimWorld of Magic's classes, spells and gifted traits and
/// Vanilla Psycasts Expanded's paths, the prices, the quarrel's sides and turns, the Hunger's bargains and takings), the
/// defs (the nine's ways, the gifts on a body, the camps, the lines and the letters' pieces), the unseen rule in the words
/// (no god named on a card, no standing in words, no Almanac page), and the seams: it calls only the core's Gods API and
/// the presence seam, keeps its questions to Tell.Ask, its hurts to the shared budget, and its hooks small.
/// </summary>
public static class GodWorldTests
{
    /// <summary>The fourteen, by short name (docs/gods-pantheon-brainstorm.md: the nine, Helios and Selene, Mog, Murmur, Ducat).</summary>
    private static readonly string[] Nine =
        { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus", "Hush", "Undertow", "Helios", "Selene", "Mog", "Murmur", "Ducat" };

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
                     ("a quarrel", GodWorldMath.QuarrelDays, GodWorldMath.QuarrelGap), ("one hunted at the gate", GodWorldMath.AskDays, GodWorldMath.AskGap)
                 })
            C(mean >= 60f && gap >= 30f && gap <= mean, $"{name} is rare: every {mean} days or so, never within {gap}");

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
          && GodWorldMath.SiteKind(GodDomain.Dead) == "grave" && GodWorldMath.SiteKind(GodDomain.Hearth) == "geyser", "an old tree, a spring or a bank, a crash site, the graves, a steam geyser");
        C(world.Contains("case \"geyser\":") && world.Contains("\"SteamGeyser\"") && world.Contains("\"CC_GW_Site_Geyser\""), "a steam geyser is found and named on the map");

        // ================= Health and wrath =================

        C(GodWorldMath.BlessingChance(FavourWord.Unknown) == 0f && GodWorldMath.BlessingChance(FavourWord.Displeased) == 0f && GodWorldMath.BlessingChance(FavourWord.Noticed) > 0f
          && GodWorldMath.BlessingChance(FavourWord.Beloved) > GodWorldMath.BlessingChance(FavourWord.Pleased) && GodWorldMath.BlessingChance(FavourWord.Beloved) <= 0.2f,
            "a fever broken now and then by the colony's standing, never below noticed");
        C(GodWorldMath.BlessingEase > 0f && GodWorldMath.BlessingEase < 0.3f && GodWorldMath.BlessingFloor > 0f && GodWorldMath.BlessingHour(2) && !GodWorldMath.BlessingHour(12),
            "it eases, never cures outright, in the small hours");
        C(GodWorldMath.BlessingDays(FavourWord.Noticed) == 0f && GodWorldMath.BlessingDays(FavourWord.Pleased) > GodWorldMath.BlessingDays(FavourWord.Beloved)
          && GodWorldMath.BlessingDays(FavourWord.Beloved) >= 20f && GodWorldMath.BlessingGap >= 15f, "a god's own kindness only while the colony stands pleased or better, and rarely");
        C(GodWorldMath.WrathOf(GodDomain.Storm, GodNature.Wrathful) == GodWrath.Lightning && GodWorldMath.WrathOf(GodDomain.Harvest, GodNature.Proud) == GodWrath.Blight
          && GodWorldMath.WrathOf(GodDomain.Wild, GodNature.Jealous) == GodWrath.Beasts && GodWorldMath.WrathOf(GodDomain.Night, GodNature.Grieving) == GodWrath.Eclipse
          && GodWorldMath.WrathOf(GodDomain.Storm, GodNature.Patient) == GodWrath.None && GodWorldMath.WrathOf(GodDomain.War, GodNature.Cold) == GodWrath.None,
            "anger by domain, the fallback: lightning, blight, beasts, the sun put out; a patient or cold god waits");
        C(GodWorldMath.WrathDays(FavourWord.Unknown, GodNature.Wrathful) == 0f && GodWorldMath.WrathDays(FavourWord.Displeased, GodNature.Patient) == 0f
          && GodWorldMath.WrathDays(FavourWord.Displeased, GodNature.Wrathful) < GodWorldMath.WrathDays(FavourWord.Displeased, GodNature.Generous)
          && Near(GodWorldMath.WrathDays(FavourWord.Wrathful, GodNature.Proud), GodWorldMath.WrathDays(FavourWord.Displeased, GodNature.Proud) / 2f, 1e-4f)
          && GodWorldMath.WrathDays(FavourWord.Wrathful, GodNature.Wrathful) >= 10f,
            "a god displeased acts now and then by its nature, a wrathful one soonest, never above displeased");
        C(GodWorldMath.WrathDays(FavourWord.Displeased, GodNature.Patient, 60f) == 60f && GodWorldMath.WrathDays(FavourWord.Wrathful, GodNature.Cold, 60f) == 30f
          && GodWorldMath.WrathDays(FavourWord.Pleased, GodNature.Wrathful, 60f) == 0f, "a god's own temper overrules its nature, still never above displeased");
        C(GodWorldMath.BeastCount(GodNature.Wrathful) <= 3 && GodWorldMath.BeastCount(GodNature.Patient) >= 2 && GodWorldMath.BeastsAfterDays >= 1 && GodWorldMath.BeastsDayChance < 0.5f
          && GodWorldMath.GrudgeDays <= 20, "the beasts: two or three, after a day or two, while the grudge lasts");
        C(GodWorldMath.LightningClear >= 3f && GodWorldMath.LightningHourChance < 0.2f, "the lightning lands clear of anyone");
        C(GodWorldMath.ShunnedGoodwill < 0 && GodWorldMath.ShunnedGoodwill >= -6 && GodWorldMath.ShunnedFavour < 0f, "Bellwether's kin harmed: every people a little colder, every god too");
        foreach (var (shape, incident) in new[]
                 {
                     ("Blight", "CropBlight"), ("Eclipse", "Eclipse"), ("ColdSnap", "ColdSnap"), ("HeatWave", "HeatWave"), ("Flashstorm", "Flashstorm"),
                     ("ShortCircuit", "ShortCircuit"), ("Flare", "SolarFlare")
                 })
            C(world.Contains($"case GodWrath.{shape}: return Incident(g, map, \"{incident}\""), $"{shape} is the game's own incident, with the game's own letter");
        C(world.Contains("Incident(g, map, \"MeteoriteImpact\", \"meteor\")") && world.Contains("Sprout(map, centre, 5)") && world.Contains("Vein(map, centre, out var ore)")
          && world.Contains("Notify_Repaired()") && world.Contains("cold.TicksLeft = Mathf.Min(cold.TicksLeft, GenDate.TicksPerHour)"),
            "kindness on the map: a meteorite, wild growth, a vein, a machine mended, a cold snap broken");
        C(world.Contains("FirstOrDefault(x => x is Building_Cooler)"), "a freezer fails first, at the worst hour");
        C(world.Contains("Incident(g, map, \"TraderCaravanArrival\", \"trader\")") && world.Contains("Incident(g, map, \"SeasonalFlooding\", \"flood\")")
          && world.Contains("Incident(g, map, \"Drought\", \"drought\")") && world.Contains("DisableRainFor("), "a trader at the right time, a drought and then a flood, the rain held off");
        C(GodWorldMath.MissingSilver(GodWorldMath.MissingFloor - 1) == 0 && GodWorldMath.MissingSilver(1000) == 50 && GodWorldMath.MissingSilver(100000) == GodWorldMath.MissingMost,
            "silver goes missing only from a hoard, a little, never much");
        C(Enumerable.Range(0, 20).All(i => GodWorldMath.RatCount(i / 20f) >= 4 && GodWorldMath.RatCount(i / 20f) <= 8) && GodWorldMath.CoinsFound(0f) == 1 && GodWorldMath.CoinsFound(0.99f) == 3
          && GodWorldMath.SilverSunk(0) == 0 && GodWorldMath.SilverSunk(10000) == 30, "a few rats, a coin or three, a little silver sunk");
        C(GodWorldMath.GrowingWeather(20f) && !GodWorldMath.GrowingWeather(-5f) && !GodWorldMath.GrowingWeather(float.NaN), "long clear days only in the growing season");
        C(GodWorldMath.DebtKept > 0f && GodWorldMath.DebtBroken < -GodWorldMath.DebtKept, "a debt kept counts, a debt broken counts more");

        // ================= Helios gives, Selene returns =================

        C(GodWorldMath.MoonFace(7) == 1f && GodWorldMath.MoonFace(0) == 0f && GodWorldMath.MoonFace(14) < 0.2f && GodWorldMath.MoonFace(22) == 1f, "the moon's face: full mid-quadrum, dark at its turn");
        C(GodWorldMath.MirrorChance(0f, 9, 0) == 0f && GodWorldMath.MirrorChance(1f, 3, 3) == 0f && GodWorldMath.MirrorChance(1f, 4, 0) == 1f
          && GodWorldMath.MirrorChance(0.5f, 0, 1) < GodWorldMath.MirrorChance(1f, 0, 1), "she returns the day as much as her face shows, nothing on an even day or a dark moon");
        C(GodWorldMath.DarkOfTheMoon(0f) == 1.5f && GodWorldMath.DarkOfTheMoon(1f) == 1f, "the Hunger's voice comes easier in the dark of the moon");
        C(GodWorldMath.MirrorHour(2) && !GodWorldMath.MirrorHour(14) && GodWorldMath.BlindBlessingDays >= 30f, "the return at night; the gift on its own rate");
        C(world.Contains("GodWays.Blind(g) ? GodWays.WrathDays(g)") && world.Contains("GodWays.Blind(g) ? GodWorldMath.BlindBlessingDays") && world.Contains("Where(x => !GodWays.Mirror(x))"),
            "Helios gives and overflows whatever the colony does; Selene answers the day, not her standing");
        C(Read("World/FactionMemory.cs").Contains("GodHealth.Deed(k.side == DeedSide.Favor, k.side == DeedSide.Wrong);") && world.Contains("DamageDefOf.ExecutionCut && (victim.IsPrisonerOfColony"),
            "the day's kindness and cruelty: a good turn or a wrong to a people, a prisoner executed");
        C(Read("Diplomacy/FavoursOwed.cs").Contains("GodPeoples.DebtClosed(ColonyOwes(t), state == Tie.State.Kept, PeopleOf(t), HomeOf(t));"), "a debt the colony owed, kept or broken, is noticed");

        // ================= Gifts =================

        var design = new Dictionary<GodDomain, string[]>
        {
            [GodDomain.Healing] = new[] { "Priest", "Paladin" }, [GodDomain.Wild] = new[] { "Druid" }, [GodDomain.Hunt] = new[] { "Ranger" },
            [GodDomain.Dead] = new[] { "Necromancer", "DeathKnight" }, [GodDomain.Void] = new[] { "Necromancer", "DeathKnight" }, [GodDomain.Storm] = new[] { "StormBorn" },
            [GodDomain.Craft] = new[] { "Enchanter" }, [GodDomain.Machine] = new[] { "Technomancer" }, [GodDomain.Night] = new[] { "TM_Bard" },
            [GodDomain.War] = new[] { "Gladiator", "TM_Monk" }
        };
        foreach (var kv in design)
            C(GodWorldMath.ClassesFor(kv.Key, GodDomain.None).SequenceEqual(kv.Value), $"the fallback class for {kv.Key}: {string.Join(" or ", kv.Value)}");
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
          && GodWorldMath.PickGift(false, false, true, false, false, false, false, 0f) == GodGiftKind.Class
          && GodWorldMath.PickGift(false, false, false, false, false, false, false, true, false, 0.5f) == GodGiftKind.Passion
          && GodWorldMath.PickGift(false, false, false, false, false, false, false, false, true, 0.5f) == GodGiftKind.Hoard, "only what can be given is picked");
        var picks = Enumerable.Range(0, 400).Select(i => GodWorldMath.PickGift(true, true, true, true, true, true, true, true, true, i / 400f)).ToList();
        C(picks.Count(k => k == GodGiftKind.Hand) > picks.Count(k => k == GodGiftKind.Relic) && Enum.GetValues(typeof(GodGiftKind)).Cast<GodGiftKind>().Where(k => k != GodGiftKind.None).All(picks.Contains),
            "every gift can come, a blessing on the body likelier than a relic");
        C(GodWorldMath.PriceOf(GodNature.Patient, GodKind.Hunger, GodDomain.Void, 0.9f) == GodPrice.Years
          && GodWorldMath.PriceOf(GodNature.Generous, GodKind.Ordinary, GodDomain.War, 0.1f) == GodPrice.Blood && GodWorldMath.PriceOf(GodNature.Jealous, GodKind.Ordinary, GodDomain.Harvest, 0.1f) == GodPrice.Rival,
            "every gift has a price, the fallback by nature and kind: the Hunger years, war blood, a jealous god its rivals' anger");
        C(Enumerable.Range(0, 20).All(i => GodWorldMath.PriceYears(GodKind.Ordinary, i / 20f) >= 3 && GodWorldMath.PriceYears(GodKind.Ordinary, i / 20f) <= 6)
          && GodWorldMath.PriceYears(GodKind.Hunger, 0f) > GodWorldMath.PriceYears(GodKind.Ordinary, 0f), "a few years, more for the Hunger");
        C(GodWorldMath.PriceSilver(GodGiftKind.Class, 1f) > GodWorldMath.PriceSilver(GodGiftKind.Hand, 1f) && GodWorldMath.PriceSilver(GodGiftKind.Hand, 0.1f) >= 100
          && GodWorldMath.PriceSilver(GodGiftKind.Relic, 1f) % 10 == 0, "an offering's silver by the gift and the colony's wealth");
        C(GodWorldMath.HoardGold(250, 10f) == 38 && GodWorldMath.HoardGold(1, 1000f) == 1 && GodWorldMath.HoardGold(100, 0f) == 0 && GodWorldMath.HoardReturn > 1f,
            "silver thrown in, more gold back");
        C(GodWorldMath.GiftRefused(GodNature.Proud) < 0f && GodWorldMath.GiftRefused(GodNature.Generous) == 0f && GodWorldMath.GiftTaken > 0f, "a gift refused galls only a proud, jealous or wrathful god");
        C(GodWorldMath.UndoAfterDays < GodWorldMath.UndoUntilDays && GodWorldMath.UndoDayChance < 0.1f && GodWorldMath.UndoLetGo > 0f && GodWorldMath.UndoGiverLoss < 0f,
            "a rival wants a gift back rarely, within a few weeks of it");
        C(world.Contains("private static string Pay(GodDoing d, Pawn p)") && world.Contains("string paid = Pay(d, p);"), "the price on the body is paid in the night, with the gift, without a word");

        // ================= The drama =================

        foreach (var (a, b) in new[] { ("storm", "dry"), ("cold", "warm"), ("cold", "rain"), ("growth", "rot"), ("sun", "spark"), ("flow", "hoard") })
            C(GodWorldMath.Opposed(a, b) && GodWorldMath.Opposed(b, a) && GodWorldMath.Sides.Contains(a) && GodWorldMath.Sides.Contains(b), $"{a} quarrels with {b}");
        C(!GodWorldMath.Opposed("storm", "cold") && !GodWorldMath.Opposed("growth", "growth") && !GodWorldMath.Opposed(null, "rot"), "only opposed sides quarrel");
        C(GodWorldMath.InTheSky("cold") && GodWorldMath.InTheSky("storm") && GodWorldMath.InTheSky("sun") && GodWorldMath.InTheSky("rain") && !GodWorldMath.InTheSky("growth")
          && !GodWorldMath.InTheSky("rot") && !GodWorldMath.InTheSky("flow") && !GodWorldMath.InTheSky("hoard") && !GodWorldMath.InTheSky("spark"), "the sky's sides in the weather, the rest on the ground");
        C(world.Contains("what = mine + \"|\" + theirs") && world.Contains("\"SolarFlare\"") && world.Contains("GodHealth.Coins(map, centre") && world.Contains("GodHealth.Sink(map)"),
            "each quarrel keeps the sides it began with; a flare once, a coin found, silver sunk");
        C(GodWorldMath.QuarrelTurn(0) == 0 && GodWorldMath.QuarrelTurn(GodWorldMath.QuarrelTurnDays) == 1 && GodWorldMath.QuarrelTurn(2 * GodWorldMath.QuarrelTurnDays) == 0
          && GodWorldMath.QuarrelLastsDays >= 4 * GodWorldMath.QuarrelTurnDays, "one side, then the other, several turns each");
        C(GodWorldMath.SupperHour(19) && !GodWorldMath.SupperHour(12) && GodWorldMath.MythEveningChance < 0.3f && GodWorldMath.MythGapDays >= 2, "a story at supper, now and then");
        C(!world.Contains("QuarrelAnswers") && !world.Contains("SlightAnswers"), "a quarrel is seen, never asked about; no god asks the colony to slight another");

        // ================= The Hunger =================

        C(GodWorldMath.Desperation(false, false, false) == 0 && GodWorldMath.Desperation(true, false, false) == 3 && GodWorldMath.Desperation(false, true, true) == 3
          && GodWorldMath.Desperation(false, true, false) == 1, "desperation: dying, starving, at the edge");
        C(GodWorldMath.HungerHears(0) == 0f && GodWorldMath.HungerHears(1) < GodWorldMath.HungerHears(2) && GodWorldMath.HungerHears(2) < GodWorldMath.HungerHears(3) && GodWorldMath.HungerHears(3) < 0.5f,
            "it hears the desperate, the more desperate the likelier, never always");
        C(GodWorldMath.HungerGapDays(0) > GodWorldMath.HungerGapDays(3) && GodWorldMath.HungerGapDays(99) >= 15f, "its next whisper comes sooner each time, never sooner than a fortnight");
        C(GodWorldMath.HungerTake(0) == "mark" && GodWorldMath.HungerTake(1) == "years" && GodWorldMath.HungerTake(2) == "mind" && GodWorldMath.HungerTake(5) == "beast"
          && GodWorldMath.HungerTakeAfterDays(0) > GodWorldMath.HungerTakeAfterDays(3) && GodWorldMath.HungerTakeAfterDays(9) >= 1, "it always takes more, and sooner");
        C(GodWorldMath.HungerShunned < 0f && GodWorldMath.QuietDays >= 15f && world.Contains("GodWays.Shuns(g)") && world.Contains("GodVoid.Quiet(b, g)"),
            "a bargain turns the gods that fear it away, and one goes quiet a while");

        // ================= The nine's ways =================

        var ways = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodsWorld_Ways.xml"));
        var gifts = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodsWorld_Gifts.xml"));
        var hediffs = gifts.Root.Elements("HediffDef").Select(e => e.Element("defName")?.Value).Where(x => x != null).ToList();
        XElement Ways(string god) => ways.Root.Elements().FirstOrDefault(e => e.Element("gods")?.Elements("li").Any(li => li.Value == god) == true);
        List<string> List(XElement w, string tag) => w?.Element(tag)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        C(ways.Root.Elements().All(e => List(e, "gods").All(Nine.Contains)) && ways.Root.Elements().Count() == Nine.Length, "the ways are the fourteen's, keyed by short name, one each");
        foreach (var god in Nine)
        {
            var w = Ways(god);
            C(w != null, $"{god} has ways of its own");
            if (w == null) continue;
            foreach (var t in List(w, "classes")) C(PeoplesGearMath.MagicSetting(t) != null, $"{god}: a class with its switch in A RimWorld of Magic's settings, {t}");
            foreach (var s in List(w, "spells")) C(s.StartsWith("SpellOf_"), $"{god}: a spell scroll, {s}");
            foreach (var p in List(w, "paths")) C(Regex.IsMatch(p, "^(VPE|VPEH|VPEP)_"), $"{god}: a path, {p}");
            var hand = w.Element("hand")?.Value;
            C(hand == null || hediffs.Contains(hand), $"{god}: its blessing on a body is a hediff");
            var site = w.Element("site")?.Value;
            C(site == null || GodWorldMath.SiteKinds.Contains(site), $"{god}: a holy place of a kind there is");
            var side = w.Element("side")?.Value;
            C(side == null || GodWorldMath.Sides.Contains(side), $"{god}: a side there is");
            C(!string.IsNullOrEmpty(w.Element("feast")?.Value), $"{god}: a feast of its own name");
            bool gives = List(w, "gifts").Any(g => g != "None");
            string dream = w.Element("dream")?.Value ?? "";
            C(!gives || dream.Length > 20 && Nine.All(x => dream.IndexOf(x, StringComparison.OrdinalIgnoreCase) < 0) && !dream.EndsWith("."),
                $"{god}: comes in a dream without its name, as a clause the breakfast table can hear");
        }
        foreach (var (god, cls) in new[] { ("Hollin", "Druid"), ("Cirrus", "StormBorn"), ("Hush", "HeartOfFrost"), ("Morel", "Necromancer"), ("Pip", "Technomancer") })
            C(List(Ways(god), "classes").Contains(cls), $"{god} gives {cls}");
        C(List(Ways("Undertow"), "gifts").SequenceEqual(new[] { "None" }) && List(Ways("Undertow"), "shuns").OrderBy(x => x).SequenceEqual(new[] { "Bellwether", "Ennu", "Hollin" })
          && List(Ways("Undertow"), "quiets").SequenceEqual(new[] { "Pip" }), "Undertow gives only by its bargains; Hollin, Bellwether and Ennu turn away, Pip goes quiet");
        C(List(Ways("Smoulder"), "gifts").Contains("Hoard") && Ways("Smoulder").Element("offering")?.Value == "silver" && Ways("Smoulder").Element("site")?.Value == "geyser",
            "Smoulder: silver thrown in and gold back, at the steam geyser");
        C(List(Ways("Bellwether"), "gifts").SequenceEqual(new[] { "Passion" }) && Ways("Bellwether").Element("chooses")?.Value == "youngest"
          && List(Ways("Bellwether"), "kin").Contains("Thrumbo") && Ways("Bellwether").Element("kinWrath")?.Value == "Shunned", "Bellwether: a passion for the young, and his kin");
        C(Ways("Ennu").Element("chooses")?.Value == "kindest" && Ways("Ennu").Element("wrath") == null, "Ennu: the kindest's lasting gift, and no anger shown");
        C(Ways("Cirrus").Element("grudge")?.Value == "Lightning" && Ways("Hush").Element("wrath")?.Value == "ColdSnap" && Ways("Smoulder").Element("wrath")?.Value == "HeatWave"
          && Ways("Hollin").Element("wrath")?.Value == "Blight" && Ways("Morel").Element("wrath")?.Value == "Breakdown" && Ways("Pip").Element("wrath")?.Value == "ShortCircuit",
            "each one's anger its own: blight, a freezer failing, a short circuit, a flashstorm and lightning, a cold snap, a heat wave");
        C(Ways("Morel").Element("blessing")?.Value == "Fever" && Ways("Hollin").Element("blessing")?.Value == "Growth" && Ways("Smoulder").Element("blessing")?.Value == "Ore"
          && Ways("Pip").Element("blessing")?.Value == "Mend" && Ways("Cirrus").Element("blessing")?.Value == "Meteor" && Ways("Hush").Element("blessing")?.Value == "Thaw",
            "each one's kindness its own");
        C(Ways("Hush").Element("side")?.Value == "cold" && Ways("Smoulder").Element("side")?.Value == "warm" && Ways("Hollin").Element("side")?.Value == "growth"
          && Ways("Morel").Element("side")?.Value == "rot", "Hush and Smoulder contend over the high valleys, Hollin and Morel bicker");
        C(Ways("Murmur").Element("side")?.Value == "rain" && Ways("Pip").Element("side")?.Value == "spark" && Ways("Helios").Element("side")?.Value == "sun"
          && Ways("Ducat").Element("side")?.Value == "flow" && List(Ways("Smoulder"), "sides").Contains("hoard"),
            "Murmur against Hush, Helios's flares against Pip, Ducat's flow against Smoulder's hoard");
        C(Ways("Helios").Element("blind")?.Value == "true" && List(Ways("Helios"), "gifts").SequenceEqual(new[] { "None" })
          && new[] { "HeatWave", "Drought", "Flare" }.All(List(Ways("Helios"), "wraths").Contains) && Ways("Helios").Element("blessing")?.Value == "Clear",
            "Helios gives blindly: long clear days, and too much giving (a heat wave, a drought, a flare); no gift to one person, no bargain");
        C(Ways("Selene").Element("mirror")?.Value == "true" && Ways("Selene").Element("trueDreams")?.Value == "true" && Ways("Selene").Element("wrath")?.Value == "Nightmares"
          && Ways("Selene").Element("blessing")?.Value == "Rest", "Selene returns the day: rest and things found for kindness, bad dreams for cruelty; her dreams come true");
        C(List(Ways("Mog"), "kin").Contains("Cat") && Ways("Mog").Element("kinWrath")?.Value == "Rats" && Ways("Mog").Element("blessing")?.Value == "Stray" && Ways("Mog").Element("chooses")?.Value == "any",
            "Mog: luck for whom she chooses, a cat wandering in, the rats when cats are harmed");
        C(Ways("Murmur").Element("wrath")?.Value == "Drought" && Ways("Murmur").Element("blessing")?.Value == "Rain" && List(Ways("Murmur"), "spells").Contains("SpellOf_Rain"),
            "Murmur: rain, or a drought and then a flood");
        C(Ways("Ducat").Element("wrath")?.Value == "Missing" && Ways("Ducat").Element("blessing")?.Value == "Trader" && Ways("Ducat").Element("debts")?.Value == "true",
            "Ducat: traders at the right time, silver gone from a hoard, the colony's debts watched");
        C(Read("Gods/World/GodWays.cs").Contains("public static string Key(God g) => g?.name;") && Regex.Matches(world, @"GodWays\.(Classes|Spells|Paths|Beasts|WrathBeasts|Hand|Wrath|Grudge|Blessing|Price|Offering|Site|Feast|Side|Dream|Kin|Tags)\(").Count >= 20,
            "every way of a god is read through its ways: by the god's short name, then its domain");
        C(!world.Contains("GodKind.Trickster") && !world.Contains("GodKind.Archotech") && !world.Contains("GodKind.Newborn") && !world.Contains("GodKind.Imprisoned")
          && !world.Contains("GodKind.Dead") && !File.Exists(Path.Combine(src, "Gods", "World", "GodOutliers.cs")),
            "no trickster, archotech, newborn, sleeper, fading or dead god: the nine only, and Undertow is the Hunger");

        // ================= The defs =================

        foreach (var d in D) C(hediffs.Contains(GodWorldMath.HandOf(d)), $"the blessing for {d} is a hediff");
        foreach (var h in new[]
                 {
                     "Rimfolk_GodMark", "Rimfolk_VoidCalm", "Rimfolk_BadDreams", "Rimfolk_GodGift_Smoulder", "Rimfolk_GodGift_Morel", "Rimfolk_GodGift_Ennu", "Rimfolk_GodGift_Hush",
                     "Rimfolk_GodGift_Selene", "Rimfolk_GodGift_Mog", "Rimfolk_GodGift_Murmur", "Rimfolk_GodGift_Ducat"
                 })
            C(hediffs.Contains(h), "the hediff " + h);
        C(gifts.Root.Elements("HediffDef").Where(e => (e.Element("defName")?.Value ?? "").StartsWith("Rimfolk_GodGift_")).All(e => e.Descendants("statOffsets").Elements().All(s => Math.Abs(float.Parse(s.Value, System.Globalization.CultureInfo.InvariantCulture)) <= 10f)),
            "the blessings are small");
        C(gifts.Root.Elements("HediffDef").All(e => !Regex.IsMatch(e.Element("description")?.Value ?? "", @"\bgods?\b", RegexOptions.IgnoreCase)),
            "a blessing on the body says what it is like, never who gave it");
        var camps = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodsWorld_Camps.xml"));
        foreach (var (def, worker) in new[] { ("Rimfolk_Camp_GodPilgrims", "CampWorker_GodPilgrims"), ("Rimfolk_Camp_GodSeer", "CampWorker_GodSeer") })
        {
            var k = camps.Root.Elements().FirstOrDefault(e => e.Element("defName")?.Value == def);
            C(k != null && k.Element("workerClass")?.Value == "ColonyCouncil." + worker && k.Element("weight")?.Value == "0", $"{def}: its own ways, never on the camps' own draw");
            C(k != null && k.Element("asks")?.Elements("li").All(a => a.Element("effects")?.Elements().Any(e => Regex.IsMatch(e.Attribute("Class")?.Value ?? "", "Gathering|Visit|CarryOut")) == true) == true,
                $"{def}: its asks play out on the map");
            C(k != null && k.Descendants("tip").All(t => !Regex.IsMatch(t.Value, @"god will|stands with|standing with each god", RegexOptions.IgnoreCase)), $"{def}: no tip spells out what a god will do");
        }
        C(!Regex.IsMatch(camps.Root.Elements().First(e => e.Element("defName")?.Value == "Rimfolk_Camp_GodSeer").ToString(), @"\{GOD"), "the seer names no god");
        C(File.ReadAllText(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodsWorld_Letters.xml")).Contains("<letterClass>ColonyCouncil.ChoiceLetter_GodWorld</letterClass>"), "one letter for every question");

        // ================= The unseen gods, in the words =================

        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_GodsWorld.xml")).Root.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        string[] cards =
        {
            "CC_GW_Beasts_", "CC_GW_Breakdown_", "CC_GW_Lightning_", "CC_GW_Bless_", "CC_GW_Growth_", "CC_GW_Ore_", "CC_GW_Mend_", "CC_GW_Battery_", "CC_GW_Thaw_",
            "CC_GW_Shunned_Label", "CC_GW_Shunned_Text", "CC_GW_Shunned_Chronicle", "CC_GW_Dream_", "CC_GW_Gift_Label", "CC_GW_Gift_Text", "CC_GW_Gift_Nothing", "CC_GW_Gift_Chronicle",
            "CC_GW_Morning_", "CC_GW_Paid_", "CC_GW_Undo_Label", "CC_GW_Undo_Text", "CC_GW_Undo_Pay", "CC_GW_Undo_LetGo", "CC_GW_Undo_Done", "CC_GW_Hunger_", "CC_GW_Hide_",
            "CC_GW_Seer_", "CC_GW_Seen_", "CC_GW_Clear_", "CC_GW_Rain_", "CC_GW_Stray_", "CC_GW_Rest_", "CC_GW_Found_", "CC_GW_Dry_", "CC_GW_Missing_",
            "CC_GW_Nightmares_", "CC_GW_Rats_"
        };
        foreach (var kv in keyed.Where(kv => cards.Any(c => kv.Key.StartsWith(c)) && !kv.Key.Contains("Why")))
            C(!Regex.IsMatch(kv.Value, @"\{(GOD|GODEPITHET|GODOF|OTHER)\}") && Nine.All(x => kv.Value.IndexOf(x, StringComparison.Ordinal) < 0), $"{kv.Key}: no god named on a card or a sign");
        foreach (var kv in keyed.Where(kv => !kv.Key.Contains("Why")))
            C(!Regex.IsMatch(kv.Value, @"\b(favou?r(ed|s)?|displeased|wrathful|beloved|is pleased|is angry)\b|the price (is|was)", RegexOptions.IgnoreCase), $"{kv.Key}: no god's standing, mood or price in words");
        C(!Regex.IsMatch(string.Join("\n", keyed.Values), @"\{(FAVOUR|FAVOR|VALUE|WORD|NATURE)\}|\d+ favou?r"), "a god's standing never in words, never a number");
        C(!File.Exists(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_GodsWorld.xml"))
          && !Directory.GetFiles(Path.Combine(root, "Defs", "AlmanacArticleDefs"), "*.xml").Any(f => File.ReadAllText(f).Contains("Council_Alm_C_GodsWorld")),
            "no Almanac page for the gods in the world");
        C(!world.Contains("CC_GW_Voice_") && !Regex.IsMatch(world, @"GodWords\.(Word|NatureWord)\("), "no card in the voice of a god's standing, no standing or nature in words");
        C(Regex.Matches(world, @"GodWorld\.Remember\(|Gods\.Remember\(").Count >= 14 && world.Contains("\"CC_GW_Told_Supper\"") && world.Contains("\"CC_GW_Told_Seer\"")
          && world.Contains("\"CC_GW_Told_\" + tells") && world.Contains("(\"CC_GW_Seen_\" + tag)"), "what is seen and told is kept for the colony's book, in the world's voice");
        foreach (var k in new[] { "miracle", "curse", "holywar", "myth" }) C(keyed.ContainsKey("CC_GW_Told_" + k) && keyed["CC_GW_Told_" + k].Contains("{WRITER}"), $"a letter's {k} kept with its writer's name");

        // ================= The lines =================

        var lines = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodsWorld_Lines.xml"));
        List<string> Pool(string def) => lines.Root.Elements().First(e => e.Element("defName")?.Value == def).Descendants("li").Select(x => x.Value).ToList();
        foreach (var pool in new[] { "Rimfolk_Pool_GodWorld", "Rimfolk_Pool_GodMyth" })
            foreach (var l in Pool(pool))
            {
                string text = Regex.Replace(Regex.Replace(l, @"^\[[^\]]*\]\s*", ""), @"\{[A-Z_]+\}", "Xxxxxxxx");
                C(text.Length <= 90 && !Regex.IsMatch(text, @"[.!?]\s+\S") && !text.StartsWith("\"") && !text.Contains("!"), $"one sentence of 90 characters at most: {text}");
            }
        var moments = Regex.Matches(world, @"GodWorld\.(?:Say|Remark)\([^;]*?""([a-z]+)""").Cast<Match>().Select(m => m.Groups[1].Value).Where(t => t != "quarrel").ToList();
        moments.AddRange(new[]
        {
            "blight", "eclipse", "coldsnap", "heatwave", "flashstorm", "shortcircuit", "meteor", "growth", "ore", "mend", "battery", "thaw", "drought", "flood", "dry",
            "missing", "flare", "found", "nightmares", "clear", "rain", "stray", "trader", "rest", "debtpaid", "debtbroken"
        });
        moments.AddRange(GodWorldMath.Sides.Select(s => "quarrel" + s));
        foreach (var tag in moments.Distinct())
            C(Pool("Rimfolk_Pool_GodWorld").Any(l => Regex.IsMatch(l, @"^\[" + tag + @"(\]|,)")), $"lines for the moment {tag}");
        C(Pool("Rimfolk_Pool_GodWorld").All(l => l.StartsWith("[")), "every line of the moments has its moment, so none borrows another's");
        C(!Regex.IsMatch(string.Join("\n", Pool("Rimfolk_Pool_GodWorld").Concat(Pool("Rimfolk_Pool_GodMyth"))), @"\b(Wake|Dial|Noon|Lull|Ardent|Wane)\b"), "no old name for the sun and the moon");
        foreach (var god in Nine)
            C(Pool("Rimfolk_Pool_GodMyth").Count(l => l.Contains("godkey:" + god.ToLowerInvariant())) >= 3, $"stories of {god} at supper, in its own words");
        C(Pool("Rimfolk_Pool_GodMyth").All(l => !l.Contains("myth:") && !l.Contains("{MYTH")) && Pool("Rimfolk_Pool_GodMyth").Where(l => l.StartsWith("[tied]")).All(l => l.Contains("{OTHER}"))
          && Pool("Rimfolk_Pool_GodMyth").Any(l => !l.StartsWith("[")), "no lines made from the ties' kinds; a tied god as {OTHER}; a few for any god");
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
        C(world.Contains("GodPresenceSeam.Call(spec)") && world.Contains("ColonyCouncil.Tell.Letter(NewsKind.World, importance") && !world.Contains("GodLookSeam.Make"),
            "a god's moment through the presence seam, a plain card when it isn't there; no avatar of its own (only Ennu takes a person's shape, and that is the core's)");
        C(!Regex.IsMatch(world, @"\bDilemmaDef\b|ChoiceLetter_Dilemma|DilemmaTracker") && world.Contains("Tell.Ask(letter, home)"), "its questions through Tell.Ask, not the dilemmas");
        C(world.Contains("Force || Tell.CanAsk") && Regex.Matches(world, @"GodWorld\.CanAsk").Count >= 5, "questions that can wait wait for Tell.CanAsk");
        C(Regex.Matches(world, @"// apart:").Count >= 4, "what happens apart says so");
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
        C(Read("UI/InboxProviders.cs").Contains("case ChoiceLetter_GodWorld g: return g.Waiting"), "a question waiting is in This week");
        string model = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        foreach (var coll in new[] { "gifts", "doings", "blessed", "lasts" })
            C(model.Contains("\"Gods/World/GodWorldBook.cs:" + coll + "\""), $"the saved {coll} modelled in the save estimate");
        string book = Read("Gods/World/GodWorldBook.cs");
        C(book.Contains("public const int MaxGifts = 60, MaxDoings = 40, MaxBlessed = 40;") && !book.Contains("sleeperStep") && !book.Contains("public List<int> raids"), "everything it keeps is bounded");
        C(Read("Gods/World/GodWorldDebug.cs").Contains("Gods in the world: show what is under way (log)") && Read("Gods/World/GodWorldDebug.cs").Contains("a god's anger in its own shape now...")
          && Read("Gods/World/GodWorldDebug.cs").Contains("the moon returns a cruel day tonight")
          && Read("Gods/World/GodWorldDebug.cs").Contains("a god's kindness in its own shape now..."), "dev actions");
        return n;
    }

    private static bool Near(float a, float b, float eps) => Math.Abs(a - b) <= eps;
}
