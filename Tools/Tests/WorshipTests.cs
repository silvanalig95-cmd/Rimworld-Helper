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
/// lines of one sentence, and the unseen rule: no favour, standing or god's rule anywhere the player reads, and no page of
/// its own in the Almanac.
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

        // When the god takes it: as the gods are felt, or plainly when it is drawn near.
        C(WorshipMath.GodTakesNow(true, 14, 1f) && !WorshipMath.GodTakesNow(true, 14, 0.5f), "drawn near: taken at the next look, before everyone's eyes");
        C(WorshipMath.GodTakesNow(false, 3, 8f) && !WorshipMath.GodTakesNow(false, 14, 8f) && !WorshipMath.GodTakesNow(false, 3, 1f),
            "felt: gone in the small hours, never by day, never at once");
        string offerings = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Gods", "Worship", "Offerings.cs"));
        C(offerings.Contains("Rand.ValueSeeded(o.chosen") && !offerings.Contains("BelievedFate"), "one offering is taken plainly or not the same at every look; no Believed fates");

        // ================= Prayer =================

        C(WorshipMath.PrayChance(false, false, true, true) == 0f && WorshipMath.PrayChance(true, false, false, false) > WorshipMath.PrayChance(false, false, false, false)
          && WorshipMath.PrayChance(false, false, false, false) > WorshipMath.PrayChance(false, true, false, false)
          && WorshipMath.PrayChance(false, false, false, true) > WorshipMath.PrayChance(false, false, false, false) && WorshipMath.PrayChance(true, false, false, true) <= 0.9f,
            "who prays: the devout most, the wavering little, a scoffer never; more for someone close");
        C(WorshipMath.Comfort(true, false) > WorshipMath.Comfort(false, false) && WorshipMath.Comfort(false, true) > WorshipMath.Comfort(false, false)
          && WorshipMath.PrayerFavour(true) > WorshipMath.PrayerFavour(false), "a prayer comforts more at a god's place, and pleases it more there");
        C(WorshipMath.AnswerChance(1f, GodKind.Ordinary, 0.5f, FavourWord.Noticed, false, false, false)
          > WorshipMath.AnswerChance(0f, GodKind.Ordinary, 0.5f, FavourWord.Noticed, false, false, false) && WorshipMath.AnswerChance(0f, GodKind.Ordinary, 0.5f, FavourWord.Noticed, false, false, false) > 0f,
            "a god answers now and then as the gods are felt, more when drawn near");
        C(WorshipMath.AnswerChance(0f, GodKind.Ordinary, 0.5f, FavourWord.Wrathful, true, true, true) == 0f
          && WorshipMath.AnswerChance(0f, GodKind.Ordinary, 0.5f, FavourWord.Favoured, false, false, false)
          > WorshipMath.AnswerChance(0f, GodKind.Ordinary, 0.5f, FavourWord.Displeased, false, false, false), "a wrathful god never answers; a favouring one more often");
        C(WorshipMath.AnswerChance(0f, GodKind.Ordinary, 0.5f, FavourWord.Pleased, true, true, false)
          > WorshipMath.AnswerChance(0f, GodKind.Ordinary, 0.5f, FavourWord.Pleased, false, false, false), "a priest and a god's own place make an answer likelier");
        C(WorshipMath.AnswerChance(0f, GodKind.Archotech, 1f, FavourWord.Beloved, true, true, true) == 0f
          && WorshipMath.AnswerChance(1f, GodKind.Archotech, 1f, FavourWord.Beloved, false, false, false) > 0f
          && WorshipMath.AnswerChance(1f, GodKind.Archotech, 1f, FavourWord.Beloved, false, false, false)
          < WorshipMath.AnswerChance(1f, GodKind.Ordinary, 1f, FavourWord.Beloved, false, false, false), "an archotech judges deeds, not prayers: seldom, and only when drawn near");
        C(WorshipMath.AnswerChance(0f, GodKind.Hunger, 0.5f, FavourWord.Unknown, false, false, false)
          > WorshipMath.AnswerChance(0f, GodKind.Ordinary, 0.5f, FavourWord.Unknown, false, false, false)
          && WorshipMath.AnswerChance(0f, GodKind.Dead, 1f, FavourWord.Beloved, true, true, true) == 0f, "the Hunger answers anyone; a dead god never");
        C(GodsMath.Natures.All(nat => WorshipMath.AnswerChance(1f, GodKind.Ordinary, 1f, FavourWord.Beloved, true, true, true) <= 0.6f), "an answer is never sure");
        C(WorshipMath.SicknessEase(1f) > WorshipMath.SicknessEase(0f) && WorshipMath.SicknessEase(0f) > 0f,
            "a sickness eased a little as the gods are felt, more plainly when the god is drawn near");
        C(WorshipMath.Drought(6, true, true) && !WorshipMath.Drought(5, true, true) && !WorshipMath.Drought(9, false, true) && !WorshipMath.Drought(9, true, false),
            "a drought: six dry days in the growing season with a field sown");
        C(WorshipMath.Turns(2, 0.1f) && !WorshipMath.Turns(1, 0f) && !WorshipMath.Turns(3, 0.9f), "two crises of faith in a month, and they may turn to another god");
        C(WorshipMath.CrisisChance > 0.2f && WorshipMath.CrisisChance < 0.5f, "an unanswered prayer is a crisis about a third of the time");

        // ================= Scoffers =================

        C(WorshipMath.Scoffer(true, true, 1f, 1) && WorshipMath.Scoffer(false, true, 0.2f, 1) && !WorshipMath.Scoffer(false, true, 0.7f, 1)
          && !WorshipMath.Scoffer(false, false, 0.1f, 1),
            "a scoffer: a faith that despises religion, or with Ideology far from sure of their faith (its deities play no part)");
        int scoffers = Enumerable.Range(1000, 7000).Count(i => WorshipMath.Scoffer(false, false, 1f, i));
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

        C(WorshipMath.DrySpellDays(0f, false) == 5 && WorshipMath.DrySpellDays(1f, false) > WorshipMath.DrySpellDays(0f, false)
          && WorshipMath.DrySpellDays(0f, true) < WorshipMath.DrySpellDays(0f, false), "the rain kept away: five days as felt, longer when the god is drawn near, shorter for a lesser gift");
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

        C(WorshipMath.ManaFactor(FavourWord.Beloved, 1f) == 1.5f && WorshipMath.ManaFactor(FavourWord.Wrathful, 1f) == 0f
          && WorshipMath.ManaFactor(FavourWord.Unknown, 0f) == 1f && WorshipMath.ManaFactor(FavourWord.Displeased, 0f) > WorshipMath.ManaFactor(FavourWord.Displeased, 1f)
          && Math.Abs(WorshipMath.ManaFactor(FavourWord.Beloved, 0f) - 1.3f) < 0.001f,
            "mana by favour: faster when beloved, slower when angry, most of the way as the gods are felt, all of it when the god is drawn near");
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
        C(!all.Contains("GodSymbols") && !all.Contains("GodWords.Word(") && !Regex.IsMatch(all, @"\.Named\(""WORD""\)"), "the unseen rule: no favour word or symbol on worship's buttons, lines or cards");
        C(files["Worship.cs"].Contains("Gods.CardName(g, map, sign)") && files["Worship.cs"].Contains("CC_GW_Hand_Plain") && files["Worship.cs"].Contains("CC_GW_Hand_Felt")
          && files["Worship.cs"].Contains(": Hand(g, map, cell);") && files["Worship.cs"].Contains("if (!spec.veiled && !GodWorld.Witnessed(map, cell)) spec.veiled = true;"),
            "the god behind a sign named only as the core allows (Gods.CardName) or when it showed itself plainly before someone, else \"some say\" when someone saw it, else unnamed");
        C(!all.Contains("Worship.CardName(") && !all.Contains("MarkActing("), "no stand-in for the core's naming, and worship never decides the colony saw a god act");
        C(files["Worship.cs"].Contains("Gods.Remember(new GodAct") && files["Worship.cs"].Contains("GodLore.Saw(who, g, words)"),
            "the gods' history through Gods.Remember, and the colony's book through what its people saw (GodLore.Saw)");
        C(files["Prayers.cs"].Contains("GodLore.Prayed(p, g, words)") && files["FaithHeart.cs"].Contains("GodLore.Prayed(p, g, words)") && files["GodDreams.cs"].Contains("GodLore.Dreamt(p, g, text)"),
            "a prayer and what followed, and a dream told, in the colony's book in the words of the one it happened to");
        foreach (var f in new[] { "Shrines.cs", "Vows.cs", "Festivals.cs", "WorshipCouncil.cs" })
            C(files[f].Contains("Worship.Booked("), "what people saw of their own worship goes into the colony's book: " + f);
        C(files["Prayers.cs"].Contains("g.kind == GodKind.Hunger || g.baseKind == GodKind.Hunger) return;") && !files["Prayers.cs"].Contains("HungerDue"),
            "the Hunger is the world part's (GodVoid's bargain): worship never answers it or takes its due");

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
                     "Council_Cal_Letter_GodFestival", "Council_Cal_Pass_GodFestival", "Council_Journal_Cal_GodFestival", "Council_Cal_Talk_GodFestival"
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
        foreach (var step in new[] { "Felt", "Plain" })
            foreach (var fam in new[] { "CC_Wsh_Dedicated_", "CC_Wsh_Answer_Sickness_", "CC_Wsh_Answer_Death_", "CC_Wsh_Answer_Raid_", "CC_Wsh_Answer_Drought_",
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

        foreach (var k in Enum.GetNames(typeof(PrayerKind)))
            C(keys.Contains("CC_Wsh_Book_Credit_" + k) && keys.Contains("CC_Wsh_Book_Unanswered_" + k), "the colony's book after a " + k + " prayer");
        C(keys.Contains("CC_Wsh_UnknownGod") && keys.Contains("CC_Wsh_Book_Offered") && keys.Contains("CC_Wsh_Book_Festival") && keys.Contains("CC_Wsh_Book_FestivalPassed"),
            "the colony's book: an offering, a festival kept or let pass, and a god not known by name");

        // ================= The unseen rule =================

        // Nothing the player reads tells a god's favour, standing, mind or rules: the gods' own hidden log (Why_) aside.
        var shown = Regex.Matches(Regex.Replace(keyed, @"(?s)<!--.*?-->", ""), @"<(CC_Wsh_[A-Za-z_]+)>([^<]*)<").Cast<Match>()
            .Where(m => !m.Groups[1].Value.StartsWith("CC_Wsh_Why_")).ToList();
        var favourWords = new Regex(@"\b(favour|favours|favoured|standing|pleased|displeased|wrath|angry|anger|answers|answered)\b", RegexOptions.IgnoreCase);
        foreach (var m in shown.Where(m => favourWords.IsMatch(m.Groups[2].Value)))
            C(false, "the unseen rule, no god's favour or mind in " + m.Groups[1].Value);
        C(shown.Count > 150, $"worship's words read ({shown.Count})");
        C(!File.Exists(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Worship.xml"))
          && !defs.Any(d => d.Item2.ToString().Contains("Council_Alm_C_Worship")), "no Almanac page of worship's own: the gods are learned in play");
        var worshipDefs = defs.Where(d => Path.GetFileName(d.f).StartsWith("Rimfolk_Worship")).ToList();
        foreach (var d in worshipDefs)
            foreach (var e in d.Item2.Descendants().Where(e => e.Name.LocalName == "fix" || e.Name.LocalName == "description" || e.Name.LocalName == "text"
                                                             || e.Parent?.Name.LocalName == "duties" || e.Parent?.Name.LocalName == "texts"))
                C(!Regex.IsMatch(e.Value, @"\b(favour|standing|pleased|displeased|weigh more|counted twice)\b", RegexOptions.IgnoreCase), "the unseen rule in " + Path.GetFileName(d.f) + ": " + e.Value.Substring(0, Math.Min(50, e.Value.Length)));

        // ================= The gods' own worship (Rimfolk_Worship_Gods.xml) =================

        var godDefs = defs.SelectMany(d => d.Item2.Root.Elements().Where(e => e.Name.LocalName.EndsWith("GodWorshipDef"))).ToList();
        XElement Of(string god) => godDefs.FirstOrDefault(e => e.Element("gods")?.Elements("li").Any(l => l.Value == god) == true);
        bool Flag(string god, string field) => Of(god)?.Element(field)?.Value == "true";
        string[] Likes(string god) => Of(god)?.Element("likes")?.Elements("li").Select(l => l.Element("kind")?.Value).ToArray() ?? new string[0];
        var fourteen = new[] { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus", "Hush", "Undertow", "Helios", "Selene", "Mog", "Murmur", "Ducat" };
        foreach (var g in fourteen) C(Of(g) != null, "the worship of " + g + ", keyed by its short name");
        var kinds = new HashSet<string>(Enum.GetNames(typeof(OfferingKind)));
        var vowKinds = new HashSet<string>(Enum.GetNames(typeof(VowKind)));
        foreach (var e in godDefs)
        {
            string name = e.Element("defName")?.Value;
            var liked = e.Element("likes")?.Elements("li").Select(l => l.Element("kind")?.Value).ToList() ?? new List<string>();
            var refused = e.Element("refuses")?.Elements("li").Select(l => l.Value).ToList() ?? new List<string>();
            C(liked.All(kinds.Contains) && refused.All(kinds.Contains) && !liked.Intersect(refused).Any(), name + ": offering kinds that exist, none both liked and refused");
            C((e.Element("vows")?.Elements("li").Select(l => l.Value) ?? new string[0]).All(vowKinds.Contains), name + ": vows that exist");
            int season = int.Parse(e.Element("festivalSeason")?.Value ?? "-1"), day = int.Parse(e.Element("festivalDay")?.Value ?? "-1");
            C(season <= 3 && day <= 14 && (season >= 0) == (day >= 0), name + ": a festival's season 0 to 3 and day 0 to 14");
            var fest = e.Element("festivalName")?.Value ?? "";
            C(!fest.Contains("—") && !fest.Contains("!") && !(e.Element("festivalDishes")?.Value ?? "").Contains("—"), name + ": plain words");
        }
        C(Flag("Helios", "takesNoOfferings") && Flag("Helios", "unmoved") && Likes("Helios").Length == 0, "Helios takes nothing and gives to all alike");
        C(Likes("Smoulder").FirstOrDefault() == "Silver", "Smoulder takes silver first");
        C(Likes("Ducat").FirstOrDefault() == "Silver" && Flag("Ducat", "givenAway"), "Ducat wants gold given away, not kept");
        C(Flag("Mog", "noFestival") && Flag("Mog", "asksNothing"), "Mog keeps no calendar and never asks: a bowl she didn't ask for");
        C(Flag("Morel", "spoiledIsTaken"), "what rots at Morel's place is hers");
        C(float.Parse(Of("Selene")?.Element("nightFactor")?.Value ?? "1", System.Globalization.CultureInfo.InvariantCulture) > 1f
          && float.Parse(Of("Selene")?.Element("dreamFactor")?.Value ?? "1", System.Globalization.CultureInfo.InvariantCulture) > 1f, "Selene hears by night and in dreams told");
        C(Likes("Undertow").Length == kinds.Count, "Undertow takes anything");
        C(Flag("Murmur", "rainKeeper") && Flag("Cirrus", "rainKeeper") && !Flag("Helios", "rainKeeper"), "the rain is Murmur's and Cirrus's to keep, as people believe");
        C(files["GodDreams.cs"].Contains("asksNothing") && files["Offerings.cs"].Contains("givenAway"), "a god that never asks, and gifts that must go on, in the code");
        return n;
    }
}
