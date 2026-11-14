using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Each god's own happenings, the second seven (round 4 of docs/gods-next-proposals.md, section C; the player, 14 November
/// 2026: "each God's events and interactions and effects ... feel unique to the god", "gods effect should be noticeable"):
/// Hush, Undertow, Helios, Selene, Murmur, Ducat, and two more for Tamiu. The pure rules (the pace each keeps, what calls
/// each, how each plays out), the defs (each god's own lines, one sentence each, named only where the colony is sure; the
/// small moods and the two hediffs), the words (every card and book line, the unseen rule, every age), the seams (the gods'
/// hour, a festival kept, silver gone from a hoard, the colony's book and belief, saved and small), and the mods named.
/// </summary>
public static class OwnHappeningsSevenTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "each god's own happenings, the second seven: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string dir = Path.Combine(src, "Gods", "OnMap");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string[] gods = { "Hush", "Undertow", "Helios", "Selene", "Murmur", "Ducat", "Tamiu" };
        var code = gods.ToDictionary(g => g, g => File.ReadAllText(Path.Combine(dir, "Own" + g + ".cs")));
        string shared = File.ReadAllText(Path.Combine(dir, "OwnSeven.cs"));
        string frame = File.ReadAllText(Path.Combine(dir, "OwnHappenings.cs"));
        string all = string.Join("\n", code.Values) + "\n" + shared;
        string keyedAll = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var keyed = Regex.Matches(keyedAll, @"^\s*<([A-Za-z0-9_]+)>(.*?)</\1>\s*$", RegexOptions.Multiline).Cast<Match>()
            .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
        var T = GodPaceMath.Trigger.None;
        var O = GodPaceMath.Trigger.Ordinary;
        var S = GodPaceMath.Trigger.Strong;

        // ================= The happenings and their pace =================

        var seven = new[] { "Hush", "Undertow", "Helios", "Selene", "Murmur", "Ducat", "Mog" };
        var mine = OwnHappeningsMath.All.Where(h => seven.Contains(OwnHappeningsMath.GodOf(h))).ToList();
        C(mine.Count == 22 && seven.All(OwnHappeningsMath.Gods.Contains) && mine.Select(OwnHappeningsMath.Name).Distinct().Count() == 22,
            "twenty-two happenings in the shared list (OwnHappening), each its own, of the seven's keys (Tamiu's is Mog)");
        C(seven.Where(g => g != "Mog").All(g => OwnHappeningsMath.Of(g).Count >= 3 && OwnHappeningsMath.Of(g).Count <= 5) && OwnHappeningsMath.Of("Mog").Count == 2,
            "three to five for each of the six, two more for Tamiu");
        C(mine.Min(h => (int)h) > (int)OwnHappening.CirrusThunder, "added after batch 1's, so a save's list of when each last came keeps its order");
        foreach (var h in mine)
        {
            var p = OwnHappeningsMath.PaceOf(h);
            bool follows = p.mean <= 0f;
            C(follows || p.gap >= 5f && p.ramp > 0f, h + ": its own pace (" + (follows ? "comes only with what calls it" : "a gap of " + p.gap + " days") + ")");
            C(OwnHappeningsMath.DayChance(h, 999f, T, 1f) == 0f, h + ": nothing at all without a reason in the world, however long since and however near its god");
            if (follows)
            {
                C(OwnHappeningsMath.DayChance(h, 999f, S, 1f) == 0f, h + ": no chance of its own, it follows what calls it");
                continue;
            }
            C(OwnHappeningsMath.DayChance(h, p.gap - 0.5f, S, 1f) == 0f, h + ": never within its gap");
            float first = OwnHappeningsMath.DayChance(h, p.gap, O, 0f), later = OwnHappeningsMath.DayChance(h, p.gap + p.ramp, O, 0f);
            C(first > 0f && first < later, h + ": the least likely right after its gap, more over its ramp");
            C(OwnHappeningsMath.DayChance(h, p.gap + p.ramp, S, 0f) >= later && OwnHappeningsMath.DayChance(h, p.gap + p.ramp, O, 1f) >= later,
                h + ": a strong reason and a near god make it no less likely");
            C(OwnHappeningsMath.CheckChance(h, p.gap, O, 0f, 24f) < OwnHappeningsMath.DayChance(h, p.gap, O, 0f), h + ": looked at hourly, a day's chance shared out");
        }
        C(new[] { OwnHappening.UndertowShun, OwnHappening.UndertowEar, OwnHappening.HeliosLookBack, OwnHappening.HeliosFruit, OwnHappening.SeleneMorning, OwnHappening.DucatTrail }
              .All(h => OwnHappeningsMath.PaceOf(h).mean <= 0f),
            "the ones that follow something (a bargain, a room of ruins, an eclipse, his feast, the moon's return, silver gone) have no chance of their own");
        C(OwnHappeningsMath.PaceOf(OwnHappening.HushEdge).gap >= 30f && OwnHappeningsMath.PaceOf(OwnHappening.HeliosDawn).gap >= 30f
          && OwnHappeningsMath.PaceOf(OwnHappening.MurmurFrogs).gap >= 20f && OwnHappeningsMath.PaceOf(OwnHappening.UndertowEar).gap >= 10f,
            "the rare ones far apart: the walk round the edge and the sun's return once a winter, the frogs once in a long while");

        // ================= Hush =================

        C(OwnHappeningsMath.Word(5f, true, true, 0f, true, 0f, true).word == OwnHappeningsMath.FrostWord.None, "no word in the frost in a thaw");
        C(OwnHappeningsMath.Word(-5f, false, false, 30f, false, 500f, false).trigger == T, "no word when nothing is wrong");
        C(OwnHappeningsMath.Word(-5f, true, false, 1f, true, 0f, true) == (OwnHappeningsMath.FrostWord.Warm, S), "WARM first for someone with hypothermia");
        C(OwnHappeningsMath.Word(-5f, false, true, 1f, false, 500f, false) == (OwnHappeningsMath.FrostWord.Bread, S), "BREAD, strongly, with the stores nearly bare");
        C(OwnHappeningsMath.Word(-5f, false, true, 10f, false, 500f, false) == (OwnHappeningsMath.FrostWord.Warm, O), "WARM for a cold bedroom");
        C(OwnHappeningsMath.Word(-5f, false, false, 3f, false, 500f, false) == (OwnHappeningsMath.FrostWord.Bread, O), "BREAD for the stores short");
        C(OwnHappeningsMath.Word(-5f, false, false, 10f, true, 20f, false) == (OwnHappeningsMath.FrostWord.Wood, O)
          && OwnHappeningsMath.Word(-5f, false, false, 10f, false, 20f, false).word != OwnHappeningsMath.FrostWord.Wood,
            "WOOD only where the colony burns wood for warmth and has little left");
        C(OwnHappeningsMath.Word(-5f, false, false, 10f, false, 500f, true) == (OwnHappeningsMath.FrostWord.Count, O), "COUNT for food let spoil");
        C(Math.Abs(OwnHappeningsMath.FoodDays(16f, 5) - 2f) < 0.01f && OwnHappeningsMath.FoodDays(10f, 0) > 50f, "the days of food: nutrition over the people's daily eating");
        C(OwnHappeningsMath.Frail(70f, false) && OwnHappeningsMath.Frail(1f, false) && OwnHappeningsMath.Frail(30f, true) && !OwnHappeningsMath.Frail(30f, false),
            "the frail kept warm: the old, a baby, the sick abed");
        C(OwnHappeningsMath.VigilTrigger(true, true) == S && OwnHappeningsMath.VigilTrigger(false, true) == T && OwnHappeningsMath.VigilTrigger(true, false) == T,
            "a vigil only in a cold snap with the frail abed");
        C(OwnHappeningsMath.VigilKept(7000, 10000) && !OwnHappeningsMath.VigilKept(6000, 10000) && !OwnHappeningsMath.VigilKept(5, 0),
            "the cold breaks only when the keeper sat through most of the night");
        C(OwnHappeningsMath.LongestNight(true, 7) && !OwnHappeningsMath.LongestNight(false, 7) && !OwnHappeningsMath.LongestNight(true, 2),
            "the longest night: the middle of winter");
        C(OwnHappeningsMath.RingTrigger(true, 5f) == S && OwnHappeningsMath.RingTrigger(false, -25f) == O && OwnHappeningsMath.RingTrigger(false, -5f) == T,
            "the walk on the longest night, or a bitter one, never on an ordinary cold night");
        C(OwnHappeningsMath.RingHour(23) && OwnHappeningsMath.RingHour(1) && !OwnHappeningsMath.RingHour(12), "the walk begins at night");
        C(OwnHappeningsMath.RingRadius(0f) == OwnHappeningsMath.RingMin && OwnHappeningsMath.RingRadius(200f) == OwnHappeningsMath.RingMax && OwnHappeningsMath.RingRadius(20f) == 27,
            "it walks out past the colony's farthest building, within bounds");
        C(OwnHappeningsMath.RingSteps(30) * OwnHappeningsMath.RingStepTicks < 2500 * 8 && OwnHappeningsMath.RingSteps(1) >= 8, "it goes all the way round within a night");
        C(OwnHappeningsMath.RingReach == 20f && OwnHappeningsMath.RingFlee >= 2, "it moves on when anyone comes within twenty steps");
        C(OwnHappeningsMath.GraveTrigger(1f, true, 30f, true) == S && OwnHappeningsMath.GraveTrigger(1f, false, 2f, true) == S
          && OwnHappeningsMath.GraveTrigger(1f, true, 30f, false) == T && OwnHappeningsMath.GraveTrigger(1f, false, 30f, true) == T
          && OwnHappeningsMath.GraveTrigger(5f, true, 2f, true) == T,
            "frost on a new grave only in lean months with food let rot");
        C(OwnHappeningsMath.MeltDaysMin >= 1f && OwnHappeningsMath.MeltDaysMax >= OwnHappeningsMath.MeltDaysMin, "the frost melts into the ring days later");

        // ================= Undertow =================

        C(OwnHappeningsMath.DriftTrigger(true, false) == S && OwnHappeningsMath.DriftTrigger(false, true) == O && OwnHappeningsMath.DriftTrigger(false, false) == T,
            "the dark drifts toward someone at the edge of breaking, never for an ordinary low mood");
        C(OwnHappeningsMath.Drift(1f, false, false) == OwnHappeningsMath.DriftEnd.Reached && OwnHappeningsMath.Drift(1f, true, false) == OwnHappeningsMath.DriftEnd.HeldByLight
          && OwnHappeningsMath.Drift(1f, true, true) == OwnHappeningsMath.DriftEnd.HeldByCompany && OwnHappeningsMath.Drift(10f, false, false) == OwnHappeningsMath.DriftEnd.Drifting,
            "a light beside them stops it, someone sitting with them stops it first, else it reaches them");
        C(OwnHappeningsMath.DriftHour(23) && OwnHappeningsMath.DriftHour(2) && !OwnHappeningsMath.DriftHour(10) && OwnHappeningsMath.DarkMoonFactor > 1f,
            "it comes at night, easier with no moon");
        C(OwnHappeningsMath.ShunDays >= 5f && OwnHappeningsMath.ShunDays <= 10f && OwnHappeningsMath.ShunFlee > OwnHappeningsMath.ShunNear, "the beasts keep away for days, and walk well off");
        C(OwnHappeningsMath.EarChance > 0f && OwnHappeningsMath.EarChance < 1f && OwnHappeningsMath.EarDays == 10f && OwnHappeningsMath.RuinsHeldAtMost <= 30,
            "a hollow ear sometimes, the first time in a room of old ruins, for ten days");

        // ================= Helios =================

        C(OwnHappeningsMath.LookBackDue(10, 11) && !OwnHappeningsMath.LookBackDue(10, 10) && !OwnHappeningsMath.LookBackDue(10, 13) && !OwnHappeningsMath.LookBackDue(-1, 5),
            "he looks back the day after an eclipse, only then");
        C(OwnHappeningsMath.FruitFrom >= 11 && OwnHappeningsMath.FruitTo <= 15 && OwnHappeningsMath.FruitMost >= 4, "the first fruit at noon, the colony together");
        C(OwnHappeningsMath.DawnTrigger(true, false) == S && OwnHappeningsMath.DawnTrigger(false, true) == S && OwnHappeningsMath.DawnTrigger(false, false) == T,
            "the sun comes back the morning after the longest night or the walk round the edge, no other morning");

        // ================= Selene =================

        C(OwnHappeningsMath.WaterTrigger(true, true, true, true) == O && OwnHappeningsMath.WaterTrigger(false, true, true, true) == T
          && OwnHappeningsMath.WaterTrigger(true, false, true, true) == T && OwnHappeningsMath.WaterTrigger(true, true, false, true) == T,
            "her face in the water only at the full moon, on a clear night, by still water, with someone up");
        C(OwnHappeningsMath.WatchTrigger(true, true, true, true) == S && OwnHappeningsMath.WatchTrigger(true, true, true, false) == O
          && OwnHappeningsMath.WatchTrigger(false, true, true, true) == T && OwnHappeningsMath.WatchTrigger(true, false, true, true) == T,
            "the watch kept company under a moon at least half full, a watch under arms the more");
        C(OwnHappeningsMath.DarkFaceTrigger(true, true) == O && OwnHappeningsMath.DarkFaceTrigger(false, true) == T, "the dark face kept only on a night with no moon");

        // ================= Murmur =================

        C(OwnHappeningsMath.SongTrigger(6, false, 2) == O && OwnHappeningsMath.SongTrigger(12, false, 3) == S && OwnHappeningsMath.SongTrigger(1, true, 2) == S
          && OwnHappeningsMath.SongTrigger(10, true, 1) == T && OwnHappeningsMath.SongTrigger(3, false, 5) == T,
            "sung to in a dry spell by two or more who hold him");
        C(OwnHappeningsMath.SongAnswered(true, 0f) > OwnHappeningsMath.SongAnswered(false, 0f) && OwnHappeningsMath.SongAnswered(false, 1f) > OwnHappeningsMath.SongAnswered(false, 0f)
          && OwnHappeningsMath.SongAnswered(true, 9f) <= 1f && OwnHappeningsMath.SongAnswered(false, 0f) < 1f,
            "the song is often answered, not always");
        C(OwnHappeningsMath.GiveBackTrigger(3, false) == S && OwnHappeningsMath.GiveBackTrigger(2, false) == O && OwnHappeningsMath.GiveBackTrigger(1, false) == T,
            "the water gives back only after a wet run");
        C(OwnHappeningsMath.FrogsTrigger(8, false) == S && OwnHappeningsMath.FrogsTrigger(3, false) == T && OwnHappeningsMath.FrogsTrigger(0, true) == S,
            "the frogs come with the rain that breaks a long dry spell");
        C(OwnHappeningsMath.Frogs(10) == 6 && OwnHappeningsMath.Frogs(250 * 250) <= 16 && OwnHappeningsMath.Frogs(250 * 250) >= 6, "a dozen or so frogs, by the land's size");
        C(OwnHappeningsMath.SilverBackMin > 0 && OwnHappeningsMath.SilverBackMax <= 50 && OwnHappeningsMath.OldThingWorth <= 300f && OwnHappeningsMath.OldThingWorn < 0.5f,
            "what the water gives back is small: a little silver, an old worn thing");

        // ================= Ducat =================

        C(OwnHappeningsMath.TrailCoins(0) == 0 && OwnHappeningsMath.TrailCoins(10) == 5 && OwnHappeningsMath.TrailCoins(500) == OwnHappeningsMath.TrailMost,
            "a trail of single coins from what was taken, never more than it");
        C(OwnHappeningsMath.PurseTrigger(true, false, true) == S && OwnHappeningsMath.PurseTrigger(false, true, true) == O && OwnHappeningsMath.PurseTrigger(false, false, true) == T
          && OwnHappeningsMath.PurseTrigger(true, true, false) == T,
            "the open purse at its feast or by one devoted to it, and only with a visitor to give to");
        C(OwnHappeningsMath.PurseMax <= 5 && OwnHappeningsMath.PurseGoodwill > 0 && OwnHappeningsMath.PurseGoodwill <= 3, "a few coins, a little goodwill");
        C(OwnHappeningsMath.BeetleTrigger(true, 0) == S && OwnHappeningsMath.BeetleTrigger(false, 2000) == O && OwnHappeningsMath.BeetleTrigger(false, 200) == T,
            "the beetle leads to a coin with traders on the land or a large hoard");

        // ================= Tamiu =================

        C(OwnHappeningsMath.ThresholdTrigger(true) == S && OwnHappeningsMath.ThresholdTrigger(false) == T, "a cat at the threshold only for someone near death");
        C(OwnHappeningsMath.LostTrigger(20f, 40f, false) == S && OwnHappeningsMath.LostTrigger(5f, 40f, false) == T && OwnHappeningsMath.LostTrigger(20f, 10f, false) == T
          && OwnHappeningsMath.LostTrigger(20f, 40f, true) == T,
            "a cat finds a body of the colony's left lying far out, half a day or more");

        // ================= The defs: lines, moods, hediffs =================

        var docs = gods.ToDictionary(g => g, g => XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodHappenings_" + g + ".xml")));
        var tagsUsed = new Dictionary<string, string[]>
        {
            ["Hush"] = new[] { "word", "vigil", "thaw", "walk", "grave", "melt" },
            ["Undertow"] = new[] { "shun", "shunself", "drift", "voice", "ear" },
            ["Helios"] = new[] { "lookback", "fruit", "dawn" },
            ["Selene"] = new[] { "morning", "water", "watch", "darkface" },
            ["Murmur"] = new[] { "song", "rain", "giveback", "frogs", "frogsheard" },
            ["Ducat"] = new[] { "trail", "purse", "beetle" },
            ["Tamiu"] = new[] { "threshold", "died", "lost" }
        };
        foreach (var g in gods)
        {
            var pool = docs[g].Root!.Elements("ColonyCouncil.FlavorPoolDef").FirstOrDefault();
            string poolName = pool?.Element("defName")?.Value;
            C(poolName == "Rimfolk_Pool_God" + g + "_Say" && code[g].Contains("\"" + poolName + "\""), g + ": its own pool of lines, used by its code");
            var lines = pool?.Descendants("li").Select(l => l.Value).ToList() ?? new List<string>();
            foreach (var t in tagsUsed[g])
            {
                int plain = lines.Count(l => Tags(l).Contains(t) && !Tags(l).Contains("named") && !Tags(l).Contains("scoffer"));
                C(plain >= 1 && code[g].Contains("\"" + t + "\""), g + ": lines for '" + t + "', said by its code");
            }
            foreach (var l in lines)
            {
                string body = Regex.Replace(l, @"^\[[^\]]*\]\s*", "");
                var tags = Tags(l);
                C(tags.Count > 0, g + ": '" + body + "' is tagged");
                C(FirstSentence(body) == body && body.Length <= 90 && !body.StartsWith("\"") && body.Count(c => c == '!') == 0 && !body.Contains("—"),
                    g + ": '" + body + "' is one short sentence, plainly said");
                C(!body.Contains("{GOD}") || tags.Contains("named"), g + ": '" + body + "' names the god only where the colony is sure (tagged named)");
            }
            if (g == "Undertow") C(lines.All(l => !Tags(l).Contains("named") && !l.Contains("{GOD}")), "Undertow: never named in anything said");
        }
        C(docs["Hush"].Root!.Descendants("li").Count(l => Regex.IsMatch(l.Value, @"\[vigil,age[0-3]\]")) == 4, "Hush: the vigil said in every age's form");

        var thoughts = new Dictionary<string, (int min, int max)>
        {
            ["Rimfolk_Thought_Hush_SatUp"] = (1, 5), ["Rimfolk_Thought_Hush_SatWith"] = (1, 5), ["Rimfolk_Thought_Undertow_StoodOver"] = (-5, -1),
            ["Rimfolk_Thought_Undertow_KeptOff"] = (1, 5), ["Rimfolk_Thought_Undertow_KeptOffMe"] = (1, 5), ["Rimfolk_Thought_Helios_FirstFruit"] = (1, 5),
            ["Rimfolk_Thought_Helios_SunBack"] = (1, 5), ["Rimfolk_Thought_Selene_FaceInWater"] = (1, 5), ["Rimfolk_Thought_Selene_KeptCompany"] = (1, 5),
            ["Rimfolk_Thought_Selene_KeptDarkFace"] = (1, 5), ["Rimfolk_Thought_Murmur_Sang"] = (1, 5), ["Rimfolk_Thought_Tamiu_CatWentAway"] = (1, 5)
        };
        var thoughtDefs = docs.Values.SelectMany(d => d.Root!.Elements("ThoughtDef")).ToDictionary(t => t.Element("defName")!.Value);
        string rates = File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Rates.cs"));
        foreach (var (t, range) in thoughts)
        {
            bool has = thoughtDefs.TryGetValue(t, out var def);
            int mood = has ? int.Parse(def!.Descendants("baseMoodEffect").First().Value) : 0;
            float days = has ? float.Parse(def!.Element("durationDays")!.Value, System.Globalization.CultureInfo.InvariantCulture) : 0f;
            C(has && mood >= range.min && mood <= range.max && days > 0f && days <= 3f && all.Contains("\"" + t + "\""), t + ": a small mood (" + mood + ") for a few days at most, given by the code");
            C(rates.Contains("(\"" + t + "\""), t + " in the balance model");
        }
        var hediffs = docs.Values.SelectMany(d => d.Root!.Elements("HediffDef")).ToDictionary(h => h.Element("defName")!.Value);
        C(hediffs.TryGetValue("Rimfolk_HollowEar", out var ear) && ear.Descendants("PsychicSensitivity").Any() && ear.Descendants("disappearsAfterTicks").First().Value.StartsWith("600000"),
            "the hollow ear: a mind more open for ten days, gone by itself");
        C(hediffs.TryGetValue("Rimfolk_MoonlitWatch", out var watch) && watch.Descendants("AimingDelayFactor").Any() && watch.Descendants("capacity").Any(c => c.Value == "Sight"),
            "the moonlit watch: sees further and aims steadier till dawn");

        // ================= The words =================

        var used = Regex.Matches(all, "\"(CC_God(?:Hush|Undertow|Helios|Selene|Murmur|Ducat|Tamiu)_[A-Za-z_]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
        var dynamic = new List<string>();
        foreach (var w in new[] { "Warm", "Bread", "Wood", "Count" }) dynamic.Add("CC_GodHush_Word_" + w);
        for (int a = 0; a < 4; a++) dynamic.Add("CC_GodHush_Warmth_" + a);
        foreach (var k in new[] { "Found", "GoodDream", "Healed", "Nightmare", "Frost", "AnimalBack" }) dynamic.Add("CC_GodSelene_Morning_" + k);
        foreach (var k in used.Where(k => !k.EndsWith("_")).Concat(dynamic))
            C(keyed.ContainsKey(k), k + " is written");
        var myKeys = keyed.Where(kv => Regex.IsMatch(kv.Key, "^CC_God(Hush|Undertow|Helios|Selene|Murmur|Ducat|Tamiu)_")).ToList();
        C(myKeys.Count >= 80, $"the seven's words ({myKeys.Count})");
        foreach (var kv in myKeys)
        {
            C(!kv.Value.Contains("—") && !kv.Value.Contains("!"), kv.Key + ": no em dash and no exclamation mark");
            bool title = kv.Key.Contains("_Title");
            if (title) C(kv.Key.EndsWith("Named") == kv.Value.Contains("{GOD}"), kv.Key + ": a title names the god only in its named form");
            else C(!kv.Value.Contains("{GOD}"), kv.Key + ": a card's text never names the god (only its title, where the colony is sure)");
        }
        C(!keyed.Keys.Any(k => k.StartsWith("CC_GodUndertow_") && k.EndsWith("Named")) && Regex.Matches(code["Undertow"], @"OwnHappenings\.CardOf\((\w+),").Cast<Match>().All(m => m.Groups[1].Value == "null"),
            "Undertow is never named on a card: no named titles, every card unnamed");
        C(code["Undertow"].Contains("SayFromWith(seer, Pool, null") && code["Undertow"].Contains("SayFrom(p, Pool, null, \"ear\")"), "Undertow: its lines are said with no god to name");
        C(keyed.TryGetValue("CC_GodHush_Warmth_0", out var w0) && keyed.TryGetValue("CC_GodHush_Warmth_3", out var w3) && w0 != w3, "Hush: keeping someone warm by the colony's age");

        // ================= On the map, and nothing told that wasn't shown =================

        C(code["Hush"].Contains("OwnHappenings.Rime(") && code["Hush"].Contains("SkySign.Figure") && code["Hush"].Contains("FleckMaker.PlaceFootprint") && code["Hush"].Contains("snap.End()"),
            "Hush: rime written by the door, the figure walking with footprints that stop, the cold snap broken after the vigil");
        C(code["Hush"].Contains("\"CC_GodHush_Grave_TextQuiet\"") && code["Hush"].Contains("\"CC_GodHush_Walk_Part\""), "Hush: the card says the cold came only when it did, the walk round only as far as it went");
        C(code["Undertow"].Contains("SkySign.Dark") && code["Undertow"].Contains("GodOmens.Play(") && code["Undertow"].Contains("ShyFrom(") && code["Undertow"].Contains("OwnSelene.KeptTonight"),
            "Undertow: the dark drawn drifting, the lamps it passes burning low, the beasts walking off, held at a door kept on a night with no moon");
        C(code["Undertow"].Contains("driftDimmed") && code["Undertow"].Contains("PawnGods.Fear("), "Undertow: the lamps told only where they dimmed; dread for the one it stood over");
        C(code["Helios"].Contains("PresenceKind.Wrath") && code["Helios"].Contains("JobDefOf.TakeInventory") && code["Helios"].Contains(".Ingested(") && code["Helios"].Contains("IntVec3.East"),
            "Helios: his hard long noon, fruit taken from the stores and eaten out in the field, the sunrise faced to the east");
        C(code["Selene"].Contains("SkySign.Moon") && code["Selene"].Contains("GodsOnMapBook.Instance") && code["Selene"].Contains("lastReturn") && code["Selene"].Contains("OuterDoor"),
            "Selene: her light on the water and over the watch, the morning after the moon's own return, the door kept");
        C(code["Murmur"].Contains("TransitionTo(") && code["Murmur"].Contains("\"DoubleRainbow\"") && code["Murmur"].Contains("\"Bullfrog\"") && code["Murmur"].Contains("OldThing()")
          && code["Murmur"].Contains("\"Music\"") && code["Murmur"].Contains("SendOff("),
            "Murmur: the song's notes, the rain and the rainbow after it, the old thing from the whole of the mods, the frogs gone at first light");
        C(code["Ducat"].Contains("GenSight.PointsOnLineOfSight") && code["Ducat"].Contains("inventory.innerContainer.TryAdd") && code["Ducat"].Contains("StoryChains.Goodwill")
          && code["Ducat"].Contains("GodSky.Crawl"),
            "Ducat: single coins laid out along the way, coins pressed into a visitor's hand, the beetle crawling to the coin");
        C(code["Tamiu"].Contains("ColonyCat(") && code["Tamiu"].Contains("StrayCat(") && code["Tamiu"].Contains("SetForbidden(false") && code["Tamiu"].Contains("\"Pawn_Cat_Call\""),
            "Tamiu: a cat of the colony's or a stray, crying by the body until it is found and can be brought home");
        C(!Regex.IsMatch(all, @"Harm\.(Injure|Ail|Roll|Fight)\("), "no hurt: none of the seven's happenings injures or sickens anyone");

        // ================= The seams =================

        C(Read(Path.Combine("Gods", "OnMap", "GodsOnMapBook.cs")).Contains("OwnHappenings.Hourly(now)"), "the gods' hour looks at the seven's happenings");
        C(Read(Path.Combine("Gods", "Worship", "Festivals.cs")).Contains("OwnHappenings.FestivalKept(g, map)") && shared.Contains("OwnHelios.Festival(") && shared.Contains("OwnMurmur.Festival(") && frame.Contains("Seven(b, now)") && frame.Contains("SevenSteps(b, now)") && frame.Contains("SevenFestival(Book, god, map)")
          && shared.Contains("OwnDucat.Festival("),
            "a feast kept: the first fruit (Helios), the singing at the wells (Murmur), the open purse (Ducat)");
        C(Read(Path.Combine("Gods", "World", "GodHealth.cs")).Contains("OwnDucat.Trail(g, map, at, take - left)"), "silver gone from a hoard leaves its trail");
        C(Regex.Matches(all, @"OwnHappenings\.Saw\(").Count >= 20 && frame.Contains("GodsOnMapWords.Saw(") && all.Contains("Worship.Booked("),
            "what was seen goes to the colony's book and each witness's belief (GodsOnMapWords.Saw), the colony's own rites as kept (Worship.Booked)");
        C(all.Contains("PawnGods.Gift(") && all.Contains("PawnGods.RiteLed(") && all.Contains("PawnGods.Fear(") && all.Contains("PawnGods.HoldOf("),
            "belief: a gift from the water, the rite of the purse and the song, dread of the dark, who holds whom");
        C(shared.Contains("Gods.CardName(g, map, sign)") && shared.Contains("Gods.CardName(g, p.Map)") && code["Helios"].Contains("\"Eclipse\"") && code["Murmur"].Contains("\"Council_Evt_LongRains\""),
            "a card is named only as Gods.CardName allows (the colony sure, or the sign learned as the god's)");
        foreach (var h in mine)
            C(Regex.IsMatch(all, @"OwnHappenings\.Came\((b|OwnHappenings\.Book), OwnHappening\." + h + @", OwnHappenings\.Now\b"), h + ": marked as come when it comes (its gap starts)");
        string debug = Read(Path.Combine("Gods", "OwnSevenDebug.cs"));
        int devs = Regex.Matches(debug, "\\[DebugAction\\(\"Rimfolk\", \"Gods: ").Count;
        C(devs >= 22, $"a dev action for each happening ({devs})");
        C(shared.Contains("partial class OwnHappeningsBook") && shared.Contains("Scribe_Collections.Look(ref ruins") && shared.Contains("Scribe_Deep.Look(ref hush")
          && frame.Contains("ExposeSeven();") && frame.Contains("Scribe_Collections.Look(ref last"),
            "saved with the shared book: when each last came (its one list), each god's happening under way, the ruins' rooms heard");
        string est = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(est.Contains("\"Gods/OnMap/OwnHappenings.cs:last\": (\"fixed\", 51,") && est.Contains("Gods/OnMap/OwnSeven.cs:ruins"), "the saved lists modelled in the save estimate (one a happening, both batches)");
        C(!Regex.IsMatch(all, @"\bSched\."), "no clock slot of their own: the gods' hour and their own due steps");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj")).Contains("OwnSevenMath.cs"), "the pure rules linked for the tests");

        // ================= The player's mods, by name, softly =================

        string catalogue = Defs(root);
        foreach (var name in new[] { "Bullfrog", "Pawn_Frog_Call", "AG_Pawn_Frog_Call", "DoubleRainbow", "Mote_WhiteIce", "Mote_Ice", "Mote_ShadowCloud", "Music", "Cat", "Pawn_Cat_Call",
                     "NightOwl", "Glowstool", "Rain", "SnowGentle" })
            C(Regex.IsMatch(catalogue, @"\t" + Regex.Escape(name) + @"\t") && all.Contains("\"" + name + "\""), name + " is in the player's enabled mods (or the game) and named softly");
        return n;
    }

    private static HashSet<string> Tags(string line)
    {
        var m = Regex.Match(line, @"^\[([^\]]*)\]");
        return m.Success ? new HashSet<string>(m.Groups[1].Value.Split(',').Select(t => t.Trim())) : new HashSet<string>();
    }

    private static string FirstSentence(string s)
    {
        for (int i = 12; i < s.Length; i++)
            if (s[i] == '.' || s[i] == '!' || s[i] == '?')
                return s.Substring(0, i + 1);
        return s;
    }

    private static string defsCache;

    private static string Defs(string root)
    {
        if (defsCache != null) return defsCache;
        using var gz = new System.IO.Compression.GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), System.IO.Compression.CompressionMode.Decompress);
        using var r = new StreamReader(gz);
        defsCache = string.Join("\n", r.ReadToEnd().Split('\n').Where(l => l.EndsWith("\tyes") || l.EndsWith("\tyes\r")));
        return defsCache;
    }
}
