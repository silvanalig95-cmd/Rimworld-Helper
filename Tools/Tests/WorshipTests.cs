using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Worship of the gods (docs/design/gods.md, stage 2 "Worship"; Source/ColonyCouncil/Gods/Worship/): the pure rules
/// (WorshipMath: what a god likes offered and how much favour a gift moves, an offering's fate at each step, who prays and
/// how often an answer comes, crises of faith, festivals' dates, vows, dreams, scoffers, RimWorld of Magic's mana), and in
/// the source and defs: the core's API and seams only, a god's sign falling back to a plain card, the hooks into the
/// region's hour and day, the dreams, the calendar and the offices, the per-god worship def, every key and pool, spoken
/// lines of one sentence, and the Almanac page.
/// </summary>
public static class WorshipTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "worship: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string dir = Path.Combine(src, "Gods", "Worship");
        var files = Directory.GetFiles(dir, "*.cs").ToDictionary(Path.GetFileName, File.ReadAllText);
        string all = string.Join("\n", files.Values);

        // ================= Places =================

        C(WorshipMath.ShrineWord(0) == "Shrine" && WorshipMath.ShrineWord(1) == "Chapel" && WorshipMath.ShrineWord(2) == "Church" && WorshipMath.ShrineWord(3) == "Terminal",
            "every age its place: a tribe's shrine, a medieval chapel, an industrial church, a spacer's terminal");
        C(WorshipMath.IsTemple(true, 60f, 20) && !WorshipMath.IsTemple(false, 90f, 40) && !WorshipMath.IsTemple(true, 40f, 40) && !WorshipMath.IsTemple(true, 80f, 8),
            "a temple: indoors, fine enough and big enough");

        // ================= Offerings =================

        C(WorshipMath.Likes(GodDomain.Hunt)[0] == OfferingKind.Pelt && WorshipMath.Likes(GodDomain.Harvest)[0] == OfferingKind.Plant
          && WorshipMath.Likes(GodDomain.Machine)[0] == OfferingKind.Component && WorshipMath.Likes(GodDomain.Hearth)[0] == OfferingKind.Meal,
            "each domain's liking: a pelt for the hunt, the field's fruit for the harvest, parts for the machine, a meal for the hearth");
        C(GodsMath.Domains.All(d => WorshipMath.Likes(d).Length >= 3), "every domain likes three kinds of gift at least");
        C(WorshipMath.Fit(OfferingKind.Pelt, GodDomain.Hunt, GodDomain.None, GodKind.Ordinary) > WorshipMath.Fit(OfferingKind.Silver, GodDomain.Hunt, GodDomain.None, GodKind.Ordinary)
          && WorshipMath.Fit(OfferingKind.Silver, GodDomain.Hunt, GodDomain.None, GodKind.Ordinary) == WorshipMath.Unliked,
            "a gift of its liking suits a god better; anything else is taken, not loved");
        C(WorshipMath.Fit(OfferingKind.Silver, GodDomain.Hunt, GodDomain.Way, GodKind.Ordinary) > WorshipMath.Unliked
          && WorshipMath.Fit(OfferingKind.Silver, GodDomain.Hunt, GodDomain.Way, GodKind.Ordinary) < WorshipMath.Fit(OfferingKind.Silver, GodDomain.Way, GodDomain.None, GodKind.Ordinary),
            "its second domain's liking counts, less than its first's");
        C(WorshipMath.Fit(OfferingKind.Meal, GodDomain.Machine, GodDomain.None, GodKind.Archotech) == 0f && WorshipMath.Fit(OfferingKind.Meal, GodDomain.Dead, GodDomain.None, GodKind.Dead) == 0f
          && !WorshipMath.TakesOfferings(GodKind.Archotech) && WorshipMath.TakesOfferings(GodKind.Hunger),
            "archotechs take no offerings (they judge deeds), nor does a dead god");
        C(WorshipMath.Fit(OfferingKind.Other, GodDomain.Void, GodDomain.None, GodKind.Hunger) == 1f && WorshipMath.Fit(OfferingKind.Meat, GodDomain.Void, GodDomain.None, GodKind.Hunger) > 1f,
            "the Hunger takes anything, and meat gladly");
        C(WorshipMath.FitFrom(new[] { (OfferingKind.Meal, 1.6f) }, OfferingKind.Meal, GodKind.Ordinary) == 1.6f
          && WorshipMath.FitFrom(new[] { (OfferingKind.Meal, 1.6f) }, OfferingKind.Weapon, GodKind.Ordinary) == WorshipMath.Unliked
          && WorshipMath.FitFrom(new[] { (OfferingKind.Meal, 1.6f) }, OfferingKind.Meal, GodKind.Archotech) == 0f,
            "a god's own table of likes (a hand-made god's) in place of its domain's");
        C(WorshipMath.NatureFit(GodNature.Proud, OfferingKind.Craft, 5, 0.5f) > 1f && WorshipMath.NatureFit(GodNature.Proud, OfferingKind.Craft, 0, 0.5f) < 1f
          && WorshipMath.NatureFit(GodNature.Generous, OfferingKind.Meal, -1, 0.5f) > 1f && WorshipMath.NatureFit(GodNature.Playful, OfferingKind.Art, -1, 0.5f) > 1f
          && WorshipMath.NatureFit(GodNature.Patient, OfferingKind.Art, -1, 0.5f) == 1f,
            "a nature weighs a gift: the proud want the best made, the generous food shared, the playful art");
        C(WorshipMath.NatureFit(GodNature.Capricious, OfferingKind.Meal, -1, 0f) < 1f && WorshipMath.NatureFit(GodNature.Capricious, OfferingKind.Meal, -1, 0.99f) > 1f,
            "a capricious god takes it as its fancy takes it");
        float meal = WorshipMath.OfferingFavour(20f, 1f, 1f, false, false), hundred = WorshipMath.OfferingFavour(100f, 1f, 1f, false, false),
            fortune = WorshipMath.OfferingFavour(5000f, 1f, 1f, false, false);
        C(meal > 1.5f && meal < hundred && hundred < fortune && fortune <= 6f, $"an offering's favour grows with its worth, never past 6 ({meal:0.0}, {hundred:0.0}, {fortune:0.0})");
        C(WorshipMath.OfferingFavour(100f, 1f, 1f, true, false) > hundred && WorshipMath.OfferingFavour(100f, 1f, 1f, false, true) > hundred
          && WorshipMath.OfferingFavour(100f, 0f, 1f, true, true) == 0f, "more for the god honoured this year and from the priest; nothing where it isn't taken");
        C(WorshipMath.OfferingValueCap(1) < WorshipMath.OfferingValueCap(20) && WorshipMath.OfferingValueCap(100) == 250f
          && WorshipMath.OfferingValueTarget(6) < WorshipMath.OfferingValueCap(6), "a fair gift, not a fortune: the cap grows with the colony and stops at 250");
        C(WorshipMath.OfferingChance(0, false, false) > 0.1f && WorshipMath.OfferingChance(3, true, true) <= 0.5f
          && WorshipMath.OfferingChance(3, true, false) > WorshipMath.OfferingChance(0, false, false), "an offering a few days apart at a place, more with the devout and a priest, half a day's chance at most");

        // The fates at each step.
        C(!WorshipMath.GodTakesNow(GodRealness.Believed, 2, 40f), "Believed: the god never takes it");
        C(WorshipMath.GodTakesNow(GodRealness.Walking, 14, 1f) && !WorshipMath.GodTakesNow(GodRealness.Walking, 14, 0.5f), "Walking: taken at the next look, before everyone's eyes");
        C(WorshipMath.GodTakesNow(GodRealness.Felt, 3, 8f) && !WorshipMath.GodTakesNow(GodRealness.Felt, 14, 8f) && !WorshipMath.GodTakesNow(GodRealness.Felt, 3, 1f),
            "Felt: gone in the small hours, never by day, never at once");
        C(WorshipMath.BelievedFate(true, true, false, 0.2f) == OfferingFate.Animal && WorshipMath.BelievedFate(false, true, true, 0.2f) == OfferingFate.Passerby
          && WorshipMath.BelievedFate(true, false, false, 1f) == OfferingFate.Waiting && WorshipMath.BelievedFate(true, false, false, WorshipMath.SpoilDays) == OfferingFate.Spoiled
          && WorshipMath.BelievedFate(false, false, false, WorshipMath.ReturnDays) == OfferingFate.Returned,
            "Believed: an animal eats it, someone passing takes it, or it spoils or goes back to the stores");

        // ================= Prayer =================

        C(WorshipMath.PrayChance(false, false, true, true) == 0f && WorshipMath.PrayChance(true, false, false, false) > WorshipMath.PrayChance(false, false, false, false)
          && WorshipMath.PrayChance(false, false, false, false) > WorshipMath.PrayChance(false, true, false, false)
          && WorshipMath.PrayChance(false, false, false, true) > WorshipMath.PrayChance(false, false, false, false) && WorshipMath.PrayChance(true, false, false, true) <= 0.9f,
            "who prays: the devout most, the wavering little, a scoffer never; more for someone close");
        C(WorshipMath.Comfort(true, false) > WorshipMath.Comfort(false, false) && WorshipMath.Comfort(false, true) > WorshipMath.Comfort(false, false)
          && WorshipMath.PrayerFavour(true) > WorshipMath.PrayerFavour(false), "a prayer comforts more at a god's place, and pleases it more there");
        C(WorshipMath.AnswerChance(GodRealness.Believed, GodKind.Ordinary, 1f, FavourWord.Beloved, true, true, true) == 0f,
            "Believed: no prayer is ever answered");
        C(WorshipMath.AnswerChance(GodRealness.Walking, GodKind.Ordinary, 0.5f, FavourWord.Noticed, false, false, false)
          > WorshipMath.AnswerChance(GodRealness.Felt, GodKind.Ordinary, 0.5f, FavourWord.Noticed, false, false, false), "the gods answer more plainly when they walk");
        C(WorshipMath.AnswerChance(GodRealness.Felt, GodKind.Ordinary, 0.5f, FavourWord.Wrathful, true, true, true) == 0f
          && WorshipMath.AnswerChance(GodRealness.Felt, GodKind.Ordinary, 0.5f, FavourWord.Favoured, false, false, false)
          > WorshipMath.AnswerChance(GodRealness.Felt, GodKind.Ordinary, 0.5f, FavourWord.Displeased, false, false, false), "a wrathful god never answers; a favouring one more often");
        C(WorshipMath.AnswerChance(GodRealness.Felt, GodKind.Ordinary, 0.5f, FavourWord.Pleased, true, true, false)
          > WorshipMath.AnswerChance(GodRealness.Felt, GodKind.Ordinary, 0.5f, FavourWord.Pleased, false, false, false), "a priest and a god's own place make an answer likelier");
        C(WorshipMath.AnswerChance(GodRealness.Felt, GodKind.Archotech, 1f, FavourWord.Beloved, true, true, true) == 0f
          && WorshipMath.AnswerChance(GodRealness.Walking, GodKind.Archotech, 1f, FavourWord.Beloved, false, false, false)
          < WorshipMath.AnswerChance(GodRealness.Walking, GodKind.Ordinary, 1f, FavourWord.Beloved, false, false, false), "an archotech judges deeds, not prayers: seldom, and only when the gods walk");
        C(WorshipMath.AnswerChance(GodRealness.Felt, GodKind.Hunger, 0.5f, FavourWord.Unknown, false, false, false)
          > WorshipMath.AnswerChance(GodRealness.Felt, GodKind.Ordinary, 0.5f, FavourWord.Unknown, false, false, false)
          && WorshipMath.AnswerChance(GodRealness.Felt, GodKind.Dead, 1f, FavourWord.Beloved, true, true, true) == 0f, "the Hunger answers anyone; a dead god never");
        C(GodsMath.Natures.All(nat => WorshipMath.AnswerChance(GodRealness.Walking, GodKind.Ordinary, 1f, FavourWord.Beloved, true, true, true) <= 0.6f), "an answer is never sure");
        C(WorshipMath.SicknessEase(GodRealness.Walking) > WorshipMath.SicknessEase(GodRealness.Felt) && WorshipMath.SicknessEase(GodRealness.Believed) == 0f,
            "a sickness eased a little when the gods are felt, plainly when they walk, never when they're only believed in");
        C(WorshipMath.Drought(6, true, true) && !WorshipMath.Drought(5, true, true) && !WorshipMath.Drought(9, false, true) && !WorshipMath.Drought(9, true, false),
            "a drought: six dry days in the growing season with a field sown");
        C(WorshipMath.Turns(2, 0.1f) && !WorshipMath.Turns(1, 0f) && !WorshipMath.Turns(3, 0.9f), "two crises of faith in a month, and they may turn to another god");
        C(WorshipMath.CrisisChance > 0.2f && WorshipMath.CrisisChance < 0.5f, "an unanswered prayer is a crisis about a third of the time");

        // ================= Scoffers =================

        C(WorshipMath.Scoffer(true, true, false, 1f, 1) && WorshipMath.Scoffer(false, true, true, 0.5f, 1) && !WorshipMath.Scoffer(false, true, true, 0.9f, 1)
          && WorshipMath.Scoffer(false, true, false, 0.2f, 1) && !WorshipMath.Scoffer(false, true, false, 0.7f, 1),
            "a scoffer: a faith that despises religion, a godless faith not held devoutly, or far from sure");
        int scoffers = Enumerable.Range(1000, 7000).Count(i => WorshipMath.Scoffer(false, false, false, 1f, i));
        C(scoffers > 700 && scoffers < 1300, $"without Ideology, about one in seven scoffs ({scoffers} of 7000), the same each time");

        // ================= Festivals =================

        C(WorshipMath.FestivalWhen(GodDomain.Harvest).season == 2 && WorshipMath.FestivalWhen(GodDomain.Night) == (3, 7) && WorshipMath.FestivalWhen(GodDomain.Dead).season == 2
          && WorshipMath.FestivalWhen(GodDomain.Wild).season == 0, "the harvest's thanksgiving in the fall, the longest night at midwinter, the dead's day as the fall turns, the green in spring");
        C(GodsMath.Domains.All(d => WorshipMath.FestivalWhen(d).day >= 0 && WorshipMath.FestivalWhen(d).day <= 14 && WorshipMath.FestivalWhen(d).season >= 0 && WorshipMath.FestivalWhen(d).season <= 3),
            "every domain's festival falls within a season");
        C(WorshipMath.FestivalDay(3, 7, 0) == 52 && WorshipMath.FestivalDay(3, 14, 1) == 1 && WorshipMath.FestivalDay(0, 1, 1) == 3, "a festival's day of the year, a step on for a clash, round the year's end");
        C(WorshipMath.FestivalKept > 0f && WorshipMath.FestivalPassed < 0f && WorshipMath.MaxFestivals == 3, "a festival kept pleases, let pass displeases; three gods' at most");

        // ================= Vows =================

        C(WorshipMath.VowDays(VowKind.NoMeat) == 20 && WorshipMath.VowDays(VowKind.BeastSpared) == 15 && WorshipMath.VowDays(VowKind.Temple) == 0 && WorshipMath.VowDays(VowKind.Pilgrimage) == 0,
            "no meat for twenty days at most, a beast spared for a season; a temple and a pilgrimage as long as the life goal");
        C(WorshipMath.WrathAfterBroken(GodNature.Wrathful, 0.1f) && !WorshipMath.WrathAfterBroken(GodNature.Wrathful, 0.9f) && !WorshipMath.WrathAfterBroken(GodNature.Generous, 0f)
          && WorshipMath.WrathAfterBroken(GodNature.Patient, 0.1f) && WorshipMath.WrathAfterBroken(GodNature.Cold, 0.1f), "sometimes wrath: a god that takes a broken word hard, half the time");

        // ================= The council =================

        C(WorshipMath.DrySpellDays(GodRealness.Believed, false) == 0 && WorshipMath.DrySpellDays(GodRealness.Walking, false) > WorshipMath.DrySpellDays(GodRealness.Felt, false)
          && WorshipMath.DrySpellDays(GodRealness.Felt, true) < WorshipMath.DrySpellDays(GodRealness.Felt, false), "the rain kept away: never in Believed, longer when the gods walk, shorter for a lesser gift");
        C(WorshipMath.BeastGiven > WorshipMath.LesserGiven && WorshipMath.BeastRefused < 0f && WorshipMath.HonourFactor > 1f, "the beast given pleases most; refused, the god is displeased");

        // ================= Dreams =================

        C(WorshipMath.GodDreamShare(false, true, true, true, false) == 0f && WorshipMath.GodDreamShare(true, true, false, false, false) > WorshipMath.GodDreamShare(true, false, false, false, false)
          && WorshipMath.GodDreamShare(true, false, true, false, false) > WorshipMath.GodDreamShare(true, false, false, false, false)
          && WorshipMath.GodDreamShare(true, false, false, false, true) < WorshipMath.GodDreamShare(true, false, false, false, false) && WorshipMath.GodDreamShare(true, true, true, true, false) <= 0.5f,
            "a god's dreams: only with a god, more for a priest or a psychic, less for a scoffer");
        C(WorshipMath.PickDream(true, false, false, false, 0.5f) == GodDreamKind.Warning && WorshipMath.PickDream(false, true, false, false, 0.5f) == GodDreamKind.Demand
          && WorshipMath.PickDream(false, false, true, false, 0.5f) == GodDreamKind.Name && WorshipMath.PickDream(false, false, false, false, 0.5f) == GodDreamKind.Vision,
            "a warning only of a real danger, a demand only with a place, a name only of an unknown god");

        // ================= RimWorld of Magic =================

        C(WorshipMath.ManaFactor(FavourWord.Beloved, GodRealness.Believed) == 1f && WorshipMath.ManaFactor(FavourWord.Wrathful, GodRealness.Believed) == 1f, "Believed: mana untouched");
        C(WorshipMath.ManaFactor(FavourWord.Beloved, GodRealness.Walking) == 1.5f && WorshipMath.ManaFactor(FavourWord.Wrathful, GodRealness.Walking) == 0f
          && WorshipMath.ManaFactor(FavourWord.Unknown, GodRealness.Walking) == 1f && WorshipMath.ManaFactor(FavourWord.Displeased, GodRealness.Felt) > WorshipMath.ManaFactor(FavourWord.Displeased, GodRealness.Walking),
            "mana by favour: faster when beloved, cut off when wrathful (Walking), most of the way when the gods are felt");
        C(WorshipMath.ClassDomain("Priest") == GodDomain.Healing && WorshipMath.ClassDomain("Druid") == GodDomain.Wild && WorshipMath.ClassDomain("StormBorn") == GodDomain.Storm
          && WorshipMath.ClassDomain("Necromancer") == GodDomain.Dead && WorshipMath.ClassDomain("Technomancer") == GodDomain.Machine && WorshipMath.ClassDomain("Ranger") == GodDomain.Hunt
          && WorshipMath.ClassDomain("Shooter") == GodDomain.None, "a RimWorld of Magic class serves its domain's gods (the design's gifts)");
        string rom = Path.Combine(root, "docs", "player-mods", "traits.tsv");
        if (File.Exists(rom))
        {
            var romTraits = File.ReadAllLines(rom).Select(l => l.Split('\t')).Where(c => c.Length > 2 && c[1] == "torann.arimworldofmagic").Select(c => c[0]).ToHashSet();
            var named = new[] { "Priest", "Paladin", "TM_Brightmage", "Druid", "Ranger", "StormBorn", "Necromancer", "Technomancer", "TM_Bard", "Gladiator", "Geomancer", "Enchanter" };
            C(romTraits.Count == 0 || named.All(romTraits.Contains), "the classes named are RimWorld of Magic's own traits (docs/player-mods)");
        }

        // ================= The source: the core's API and seams only =================

        C(!Regex.IsMatch(all, @"\bGodBook\.Instance\b") && !Regex.IsMatch(all, @"\.standings\b") && !Regex.IsMatch(all, @"\bPantheon\."),
            "worship reaches the gods through Gods only: never the core's book, standings or pantheon maker");
        C(files["Worship.cs"].Contains("GodPresenceSeam.Call(spec)") && files["Worship.cs"].Contains("Tell.Letter(") && files["Worship.cs"].Contains("Tell.Minor("),
            "a god's sign goes to the presence part, and falls back to a plain card or news while it isn't there");
        C(files["Worship.cs"].Contains("PresenceSpec.For(g, map, kind, cell)"), "the sign's moment veiled by how real the gods are (PresenceSpec.For)");
        C(Regex.Matches(all, @"\bMessages\.Message\(").Count <= 4 && !files["Prayers.cs"].Contains("Messages.Message(\"CC_Wsh_Answer"),
            "the player is told through Tell and the news; a direct message only answers their own click or a dev action");
        C(!Regex.IsMatch(all, @"MoteMaker\.ThrowText|PlayLog\.Add\("), "speech only through Speech.Say and Speech.Logged");
        C(files["FaithHeart.cs"].Contains("Together.Can(scoffer, p)") && files["FaithHeart.cs"].Contains("Together.Can(a, b)"), "a scoffer and a couple only when they're together");
        C(files["Prayers.cs"].Contains("Interplay.Ease(p, WorshipMath.Comfort("), "a prayer's comfort counts in the heart's daily cap (Interplay.Ease)");
        C(files["Vows.cs"].Contains("Beliefs.Forbids(p, Belief.NoMeat)") && files["Vows.cs"].Contains("Beliefs.Forbids(p, Belief.NoHunting)"), "vows respect beliefs and diets");
        C(files["Offerings.cs"].Contains("FleshTypeDefOf.Insectoid") && files["Offerings.cs"].Contains("Humanlike"), "never the flesh of people or insects offered");
        C(files["WorshipMore.cs"].Contains("AccessTools.TypeByName(\"TorannMagic.Need_Mana\")") && files["WorshipMore.cs"].Contains("ModsConfig.IsActive(\"torann.arimworldofmagic\")")
          && files["WorshipMore.cs"].Contains("StandsDownWhy"), "A RimWorld of Magic by name only, standing down without it");
        C(files["WorshipMore.cs"].Contains("Inbox.Register(\"worship-vows\", InboxSection.UnderWay"), "This week lists vows and demands under way");
        C(all.Contains("GodWorshipDef.Fit(g, k)") && all.Contains("GodWorshipDef.Likes(g)") && files["Festivals.cs"].Contains("GodWorshipDef.For(g)?.festivalName")
          && files["Festivals.cs"].Contains("GodWorshipDef.For(g)?.chants"), "a god's own worship def first (likes, festival, chant), the domain's tables after");
        C(!Regex.IsMatch(all, @"FavourValue\([^)]*\)\.ToString|ToStringPercent\(\)[^;]*Favour"), "favour never shown as a number");

        // Hooks into the shared systems, small and local.
        C(Read("World/Region/RegionBook.cs").Contains("Worship.Hourly(now)") && Read("World/Region/RegionBook.cs").Contains("Worship.Daily(now)"), "worship runs in the region's hour and day");
        C(Read("Dreams/Dreams.cs").Contains("GodDreams.TryDream(comp, p)"), "a god's dream in the dreams' own hour");
        C(Read("Calendar/CalendarDay.cs").Contains("GodFestival") && Read("Calendar/CalendarLived.cs").Contains("WatchGodFestivals(map, today, year)")
          && Read("Calendar/CalendarKeeping.cs").Contains("Festivals.Kept(d, map, home)") && Read("Calendar/CalendarKeeping.cs").Contains("Festivals.Passed(d, map)"),
            "festivals on the calendar: written down daily, kept and let pass through the calendar's own keeping");
        C(Read("Offices/Offices.cs").Contains("(!needsGods || Worship.PriestSeatOpen)") && files["Worship.cs"].Contains("chaplain.Available"),
            "the priest's seat only with the gods, and never beside the chaplain");
        C(Read("CouncilSettings.cs").Contains("public bool godWorship = true;") && Read("UI/SettingsCatalog.cs").Contains("Toggle(\"godWorship\""), "the setting, on by default, under The gods");

        // ================= Defs, keys and lines =================

        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => (f, XDocument.Load(f))).ToList();
        var names = new HashSet<string>(defs.SelectMany(d => d.Item2.Root.Elements()).Select(e => e.Element("defName")?.Value).Where(x => x != null));
        foreach (var d in new[]
                 {
                     "Rimfolk_Worship_Dedicate", "Rimfolk_Worship_Pray", "Rimfolk_Worship_Rite", "Rimfolk_Worship_Offer", "Rimfolk_Worship_Pilgrimage", "Rimfolk_Worship_Prayed",
                     "Rimfolk_Worship_Answered", "Rimfolk_Worship_Unanswered", "Rimfolk_Worship_Offered", "Rimfolk_Worship_Mocked", "Rimfolk_Worship_Bickered",
                     "Rimfolk_Worship_VowKept", "Rimfolk_Worship_VowBroken", "Rimfolk_Dream_God", "Rimfolk_Office_Priest", "Rimfolk_OfficeWords_Priest", "Rimfolk_Asp_Pilgrimage",
                     "Rimfolk_Asp_Temple", "Rimfolk_Wish_VowMeals", "Rimfolk_Dlm_GodOfTheYear", "Rimfolk_Dlm_Temple", "Rimfolk_Dlm_GodAsks", "Rimfolk_Dlm_RivalGods",
                     "Council_Alm_C_Worship", "Council_Cal_Letter_GodFestival", "Council_Cal_Pass_GodFestival", "Council_Journal_Cal_GodFestival", "Council_Cal_Talk_GodFestival"
                 })
            C(names.Contains(d), "def " + d);
        var pools = defs.SelectMany(d => d.Item2.Root.Elements().Where(e => e.Name.LocalName.EndsWith("FlavorPoolDef"))).ToDictionary(e => e.Element("defName").Value, e => e);
        foreach (Match m in Regex.Matches(all, "\"(Rimfolk_Pool_Worship_[A-Za-z]+)\""))
            C(pools.ContainsKey(m.Groups[1].Value), "pool " + m.Groups[1].Value);
        var spoken = new[]
        {
            "Rimfolk_Pool_Worship_Dedicate", "Rimfolk_Pool_Worship_Offer", "Rimfolk_Pool_Worship_Pray", "Rimfolk_Pool_Worship_Thanks", "Rimfolk_Pool_Worship_Crisis",
            "Rimfolk_Pool_Worship_Scoff", "Rimfolk_Pool_Worship_ScoffReply", "Rimfolk_Pool_Worship_Bicker", "Rimfolk_Pool_Worship_BickerReply", "Rimfolk_Pool_Worship_Vow",
            "Rimfolk_Pool_Worship_PilgrimOut", "Rimfolk_Pool_Worship_PilgrimThere", "Rimfolk_Pool_Worship_Rite", "Rimfolk_Pool_Worship_RiteWords", "Rimfolk_Pool_Worship_GiveBeast"
        };
        var sentence = new Regex(@"[.?!][""']?\s+[A-Z{]");
        foreach (var s in spoken)
        {
            var lines = pools.TryGetValue(s, out var el) ? el.Descendants("li").Select(l => Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", "")).ToList() : new List<string>();
            C(lines.Count > 0 && lines.All(l => !sentence.IsMatch(l)), "one sentence a spoken line: " + s);
            C(lines.All(l => l.Contains("{GOD}") || l.Contains("{OTHER") || s.EndsWith("GiveBeast") || s.EndsWith("PilgrimOut") || s.EndsWith("Crisis") || s.EndsWith("ScoffReply")),
                "spoken to or of the god by name: " + s);
        }
        string keyed = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var keys = new HashSet<string>(Regex.Matches(keyed, @"<([A-Za-z0-9_]+)>").Cast<Match>().Select(m => m.Groups[1].Value));
        foreach (Match m in Regex.Matches(all, "\"(CC_Wsh_[A-Za-z_]+[A-Za-z])\"(?!\\s*\\+)"))
            C(keys.Contains(m.Groups[1].Value), "key " + m.Groups[1].Value);
        // The families of keys built from a step, a kind or a domain.
        foreach (var step in new[] { "Believed", "Felt", "Walking" })
            foreach (var fam in new[] { "CC_Wsh_Dedicated_", "CC_Wsh_Answer_Sickness_", "CC_Wsh_Answer_Death_", "CC_Wsh_Answer_Raid_", "CC_Wsh_Answer_Drought_", "CC_Wsh_HungerDue_",
                         "CC_Wsh_Wrath_", "CC_Wsh_DryBegins_", "CC_Wsh_DryPass_", "CC_Wsh_BeastGiven_", "CC_Wsh_Taken_Taken_" })
                C(keys.Contains(fam + step), "key " + fam + step);
        foreach (var kind in Enum.GetNames(typeof(OfferingKind))) C(keys.Contains("CC_Wsh_Kind_" + kind), "key CC_Wsh_Kind_" + kind);
        foreach (var k in Enum.GetNames(typeof(VowKind)))
            foreach (var fam in new[] { "CC_Wsh_Vow_What_", "CC_Wsh_Vowed_", "CC_Wsh_VowKept_", "CC_Wsh_VowBroken_", "CC_Wsh_Week_Vow_", "CC_Wsh_Week_VowTip_", "CC_Wsh_Why_GreatVow_" })
                C(keys.Contains(fam + k), "key " + fam + k);
        foreach (var k in Enum.GetNames(typeof(PrayerKind))) C(keys.Contains("CC_Wsh_Crisis_" + k) && keys.Contains("CC_Wsh_Why_Credit_" + k), "keys for a " + k + " prayer");
        foreach (var d in Enum.GetNames(typeof(GodDomain))) C(keys.Contains("CC_Wsh_Festival_" + d) && keys.Contains("CC_Wsh_Dishes_" + d), "a festival's name and dishes: " + d);
        foreach (var f in new[] { "Animal", "Passerby", "Spoiled", "Returned" }) C(keys.Contains("CC_Wsh_Taken_" + f), "key CC_Wsh_Taken_" + f);
        foreach (var a in new[] { "Dedicate", "Pray", "Rite" }) C(keys.Contains("CC_Wsh_Report_" + a) && keys.Contains("CC_Wsh_Report_" + a + "_NoGod"), "a job's report: " + a);
        C(keys.Contains("CC_God_KnownFrom_dream") && keys.Contains("CC_Cal_Origin_GodFestival") && keys.Contains("CC_Settings_GodWorship"), "the colony learns a god from a dream; the calendar's origin; the setting");
        C(!Regex.IsMatch(Regex.Replace(keyed, @"(?s)<!--.*?-->", ""), @"<CC_Wsh_[^>]*>[^<]*(—|!)"), "no em dashes or exclamation marks in worship's words");

        // The Almanac page.
        string alm = File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Worship.xml"));
        C(alm.Contains("Council_Alm_C_Worship") && alm.Contains("<seeAlso><li>Council_Alm_C_Gods</li></seeAlso>") && !alm.Contains("—"), "the Almanac page, beside the gods'");
        return n;
    }
}
