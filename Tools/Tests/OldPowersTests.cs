using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The old powers' own strange happenings (Source/ColonyCouncil/OldPowers/; the player, 13 and 14 November 2026: "i like the
/// old powers storyteller and want you to do some more more events in this style, including the gods and using ressources
/// from rimworld of magic", "do ... more old powers style events"). The pure rules (the gods' pace each keeps, the spacing,
/// the ways' lever, what calls each, the seer's true words, the finds, the beast's word), a model of how often they come
/// under any storyteller and under Old Powers, the defs (the lines said aloud, one plain sentence each and every moment
/// covered in every age; the small moods; another sky), the words (every card's key written, plain, the unseen gods), the
/// harm through the shared budget, the seams (the world events' hour, the lever, the storyteller), and every mod's def named
/// in the code found in the player's active mods.
/// </summary>
public static class OldPowersTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the old powers: " + what);
        }
        var N = GodPaceMath.Trigger.None;
        var O = GodPaceMath.Trigger.Ordinary;
        var S = GodPaceMath.Trigger.Strong;

        // ================= The pace =================

        var all = OldPowersMath.All;
        C(all.Length == 12 && all.Select(OldPowersMath.Name).Distinct().Count() == 12, "twelve happenings, each its own name");
        foreach (var e in all)
        {
            var p = OldPowersMath.PaceOf(e);
            C(p.gap >= 30f && p.mean > 0f && p.ramp >= 20f, e + ": a long gap, a mean and a ramp of its own");
            int w = OldPowersMath.WindowHours(e);
            C(w >= 2 && w <= 14, e + ": looked at a few hours a day (" + w + ")");
            C(Enumerable.Range(0, 24).Count(h => OldPowersMath.InHours(e, h)) == w, e + ": its hours as counted");
            C(OldPowersMath.DayChance(e, p.gap - 0.5f, S, 3f) == 0f, e + ": never within its gap");
            C(OldPowersMath.DayChance(e, p.gap + p.ramp + 10f, N, 3f) == 0f, e + ": never without a reason (no firing when it is merely free)");
            float first = OldPowersMath.DayChance(e, p.gap, O, 1f), later = OldPowersMath.DayChance(e, p.gap + p.ramp, O, 1f);
            C(first > 0f && later > first * 3f, e + ": its chance starts small after the gap and grows over the ramp");
            C(OldPowersMath.DayChance(e, p.gap + p.ramp, S, 1f) > later, e + ": a strong reason likelier than an ordinary one");
            C(OldPowersMath.DayChance(e, p.gap + p.ramp, O, 3f) > later * 2f, e + ": three times the lever, much likelier");
            C(OldPowersMath.CheckChance(e, p.gap + p.ramp, O, 1f) < later, e + ": shared out over its hours");
            C(OldPowersMath.AssumedShare(e) > 0f && OldPowersMath.AssumedShare(e) < 0.3f, e + ": the model's share of looks its call holds");
        }
        C(OldPowersMath.InHours(OldPower.OneWay, 23) && OldPowersMath.InHours(OldPower.OneWay, 0) && !OldPowersMath.InHours(OldPower.OneWay, 1) && !OldPowersMath.InHours(OldPower.OneWay, 12),
            "a window over midnight wraps");
        C(OldPowersMath.Lever(9f) == StoryWaysMath.MaxLever && OldPowersMath.Lever(0f) == StoryWaysMath.Floor && OldPowersMath.Lever(float.NaN) == 1f, "the lever made safe");
        C(!OldPowersMath.Spaced(OldPowersMath.SpacingDays - 0.1f) && OldPowersMath.Spaced(OldPowersMath.SpacingDays) && OldPowersMath.SpacingDays >= 4f, "none within a few days of another");

        // A model: one colony, every hour of 3000 days, each call holding at its assumed share of the looks.
        float Mean(float lever, int seed)
        {
            var rng = new Random(seed);
            var last = all.ToDictionary(e => e, _ => -9999f);
            float lastAny = -9999f;
            int count = 0;
            const int days = 3000;
            for (int d = 0; d < days; d++)
            for (int h = 0; h < 24; h++)
            {
                float now = d + h / 24f;
                if (!OldPowersMath.Spaced(now - lastAny)) continue;
                foreach (var e in all.OrderBy(_ => rng.Next()))
                {
                    if (!OldPowersMath.InHours(e, h) || rng.NextDouble() >= OldPowersMath.AssumedShare(e)) continue;
                    var why = rng.NextDouble() < 0.25 ? S : O;
                    if (rng.NextDouble() >= OldPowersMath.CheckChance(e, now - last[e], why, lever)) continue;
                    last[e] = now;
                    lastAny = now;
                    count++;
                    break;
                }
            }
            return count == 0 ? 9999f : days / (float)count;
        }
        float plain = (Mean(1f, 11) + Mean(1f, 12) + Mean(1f, 13)) / 3f, old = (Mean(3f, 11) + Mean(3f, 12) + Mean(3f, 13)) / 3f;
        C(plain >= 12f && plain <= 30f, $"under any storyteller about one in two to four weeks ({plain:0.0} days apart)");
        C(old >= 5f && old <= 12f, $"under Old Powers or its direction about one a week ({old:0.0} days apart)");
        C(plain / old >= 1.8f, $"Old Powers leans to them ({plain / old:0.0} times as often)");

        // ================= What calls each, and how each goes =================

        C(OldPowersMath.SeerTrigger(12, true) == N && OldPowersMath.SeerTrigger(13, false) == O && OldPowersMath.SeerTrigger(14, true) == S && OldPowersMath.SeerTrigger(0, true) == N,
            "the seer: a season's last two days, strong with a hard one ahead");
        C(OldPowersMath.HardAhead(5f, -3f) && OldPowersMath.HardAhead(20f, 35f) && !OldPowersMath.HardAhead(-5f, -8f) && !OldPowersMath.HardAhead(15f, 18f), "a hard season ahead: a frost coming on, or the heat");
        C(OldPowersMath.NextSeasonTwelfths(0).SequenceEqual(new[] { 3, 4, 5 }) && OldPowersMath.NextSeasonTwelfths(2).SequenceEqual(new[] { 3, 4, 5 })
          && OldPowersMath.NextSeasonTwelfths(11).SequenceEqual(new[] { 0, 1, 2 }) && OldPowersMath.NextSeasonTwelfths(7).SequenceEqual(new[] { 9, 10, 11 }), "the next season's twelfths");
        var W = OldPowersMath.SeerWords(-5f, 10f, false);
        C(W.SequenceEqual(new[] { OldPowersMath.SeerWord.Frost, OldPowersMath.SeerWord.Bare, OldPowersMath.SeerWord.StoresShort }), "the seer: a frost, nothing grows, the stores short");
        W = OldPowersMath.SeerWords(15f, 50f, false);
        C(W.SequenceEqual(new[] { OldPowersMath.SeerWord.Mild, OldPowersMath.SeerWord.Grows, OldPowersMath.SeerWord.StoresEnough }), "the seer: mild, it grows, the stores last");
        W = OldPowersMath.SeerWords(36f, 5f, true);
        C(W.SequenceEqual(new[] { OldPowersMath.SeerWord.Heat, OldPowersMath.SeerWord.Grows, OldPowersMath.SeerWord.Blades }), "the seer: the heat, and blades when a fight is coming");
        C(OldPowersMath.SeerWords(25f, 20f, false).SequenceEqual(new[] { OldPowersMath.SeerWord.Warm, OldPowersMath.SeerWord.Grows }) && OldPowersMath.SeerWords(float.NaN, 5f, true).Count == 0,
            "the seer: nothing said of the stores when it isn't plain, nothing at all without the season");
        C(OldPowersMath.SeerWords(5f, 5f, false)[0] == OldPowersMath.SeerWord.Cold, "cold below ten");

        C(OldPowersMath.OneWayTrigger(10f, false, false) == S && OldPowersMath.OneWayTrigger(40f, true, true) == O && OldPowersMath.OneWayTrigger(40f, true, false) == N
          && OldPowersMath.OneWayTrigger(float.NaN, false, true) == N, "everything one way: the night after a death, or a clear night with no moon");
        C(OldPowersMath.Compass(0, 5) == 0 && OldPowersMath.Compass(5, 0) == 2 && OldPowersMath.Compass(0, -5) == 4 && OldPowersMath.Compass(-5, 0) == 6 && OldPowersMath.Compass(3, 3) == 1
          && OldPowersMath.Compass(-3, 3) == 7 && OldPowersMath.Points.Length == 8, "the compass from north, clockwise");
        C(OldPowersMath.StoneTrigger(true, false) == S && OldPowersMath.StoneTrigger(false, true) == O && OldPowersMath.StoneTrigger(false, false) == N, "the stone: a psychic sky or a storm");
        C(OldPowersMath.ScrollTrigger(true, false) == O && OldPowersMath.ScrollTrigger(true, true) == N && OldPowersMath.ScrollTrigger(false, false) == N, "the scroll: a room of the ruins not searched yet");
        C(OldPowersMath.CasterOf(true, true, 2f) == OldPowersMath.Caster.Mage && OldPowersMath.CasterOf(false, true, 0.5f) == OldPowersMath.Caster.Psycaster
          && OldPowersMath.CasterOf(false, false, 1.5f) == OldPowersMath.Caster.Sensitive && OldPowersMath.CasterOf(false, false, 1f) == OldPowersMath.Caster.None, "who a sky of power fills");
        C(OldPowersMath.ChargedTrigger(true, false, 0) == N && OldPowersMath.ChargedTrigger(true, true, 1) == S && OldPowersMath.ChargedTrigger(false, true, 2) == O
          && OldPowersMath.ChargedTrigger(false, false, 2) == N, "the casters: nobody to feel it, nothing");
        C(OldPowersMath.BurnChance <= 0.15f && OldPowersMath.BurnHigh <= 4f, "the burn small and rare, before the shared budget");
        C(OldPowersMath.EnchanterTrigger(1, false, false, true, true) == O && OldPowersMath.EnchanterTrigger(1, false, true, true, true) == S && OldPowersMath.EnchanterTrigger(1, true, true, true, true) == N
          && OldPowersMath.EnchanterTrigger(5, false, true, true, true) == N && OldPowersMath.EnchanterTrigger(0, false, true, false, true) == N
          && OldPowersMath.EnchanterTrigger(0, false, true, true, false) == N, "the enchanter: a season's first days, never in winter, only with a piece and the silver");
        C(Enumerable.Range(0, 5).All(OldPowersMath.Enchantable) && !OldPowersMath.Enchantable(5) && !OldPowersMath.Enchantable(6) && !OldPowersMath.Enchantable(-1), "never a masterwork or a legend");
        C(Enumerable.Range(0, 4).All(q => OldPowersMath.EnchantPrice(q + 1) > OldPowersMath.EnchantPrice(q)) && OldPowersMath.EnchantPrice(0) > 0, "the price rises with the grade");
        C(OldPowersMath.BuriedTrigger(6, false) == S && OldPowersMath.BuriedTrigger(4, false) == O && OldPowersMath.BuriedTrigger(1, true) == O && OldPowersMath.BuriedTrigger(2, false) == N,
            "under the ground: a wet run or the thaw");
        bool noMagic = true, sawAll = true;
        var seen = new HashSet<OldPowersMath.BuriedFind>();
        for (float r = 0f; r < 1f; r += 0.01f)
        {
            var f = OldPowersMath.PickFind(false, false, r);
            if (f == OldPowersMath.BuriedFind.Magicyte || f == OldPowersMath.BuriedFind.Waker) noMagic = false;
            seen.Add(OldPowersMath.PickFind(true, true, r));
        }
        sawAll = seen.Count == 4;
        C(noMagic && sawAll, "the finds: magicyte only with the mod, the waking dead only with Anomaly and harm on");
        C(OldPowersMath.DeadRoadTrigger(20f, false, false, false) == S && OldPowersMath.DeadRoadTrigger(100f, true, true, false) == O && OldPowersMath.DeadRoadTrigger(100f, true, false, true) == O
          && OldPowersMath.DeadRoadTrigger(100f, true, false, false) == N && OldPowersMath.DeadRoadTrigger(100f, false, true, true) == N, "the dead: after a death, or a dark night with the void awake or the dead unburied");
        C(OldPowersMath.DeadRoadPoints(100f) == 60f && OldPowersMath.DeadRoadPoints(10000f) == 220f && OldPowersMath.DeadRoadPoints(1000f) == 150f, "the shamblers a small share of the threat points");
        C(OldPowersMath.PilgrimsTrigger(1, StoryWaysMath.Spring, false) == O && OldPowersMath.PilgrimsTrigger(1, StoryWaysMath.Summer, false) == N && OldPowersMath.PilgrimsTrigger(9, StoryWaysMath.Winter, true) == S
          && OldPowersMath.PilgrimsTrigger(4, StoryWaysMath.Autumn, false) == N, "the pilgrims: spring's and autumn's first days, or a sign in the sky");
        C(Enumerable.Range(0, 100).Select(i => OldPowersMath.PilgrimCount(i / 100f)).All(c => c >= 3 && c <= 5) && OldPowersMath.PilgrimCount(0.99f) == 5, "three to five pilgrims");
        C(OldPowersMath.OtherSkyTrigger(true, StoryWaysMath.Spring, 1, true) == S && OldPowersMath.OtherSkyTrigger(true, StoryWaysMath.Spring, 1, false) == N
          && OldPowersMath.OtherSkyTrigger(false, StoryWaysMath.Winter, 7, true) == O && OldPowersMath.OtherSkyTrigger(false, StoryWaysMath.Spring, 7, true) == N, "another sky: clear nights only");
        C(OldPowersMath.BeastTrigger(4, false) == S && OldPowersMath.BeastTrigger(1, true) == O && OldPowersMath.BeastTrigger(3, false) == N, "the beast: the hunted, or a full moon");
        C(OldPowersMath.BeastWordOf(5, true, true, 0f, 0f, 0f) == OldPowersMath.BeastWord.Hunted && OldPowersMath.BeastWordOf(0, true, true, 0f, 0f, 0f) == OldPowersMath.BeastWord.Coming
          && OldPowersMath.BeastWordOf(0, false, true, 0f, 0f, 0f) == OldPowersMath.BeastWord.Sick && OldPowersMath.BeastWordOf(0, false, false, 0.1f, 0f, 0f) == OldPowersMath.BeastWord.Hungry
          && OldPowersMath.BeastWordOf(0, false, false, 0.5f, 0.1f, 0f) == OldPowersMath.BeastWord.Tired && OldPowersMath.BeastWordOf(0, false, false, 0.5f, 0.5f, 0.2f) == OldPowersMath.BeastWord.Sad
          && OldPowersMath.BeastWordOf(0, false, false, 0.5f, 0.5f, 0.5f) == OldPowersMath.BeastWord.Watched, "the beast's word: the first true thing");
        C(OldPowersMath.GodPassesTrigger(0.9f, true, true) == N && OldPowersMath.GodPassesTrigger(0.75f, false, false) == S && OldPowersMath.GodPassesTrigger(0.55f, true, false) == O
          && OldPowersMath.GodPassesTrigger(0.55f, false, false) == N && OldPowersMath.GodPassesTrigger(0.3f, true, false) == N, "a god going by: drawn near, never while the gods are busy");
        C(OldPowersMath.PassSignAt(0) == 22 && OldPowersMath.PassSignAt(1) == 0 && OldPowersMath.PassSignAt(3) == 4 && OldPowersMath.PassSignsAtMost == 4, "its signs from ten at night, two hours apart");
        C(OldPowersMath.Age(2) == 0 && OldPowersMath.Age(3) == 1 && OldPowersMath.Age(4) == 2 && OldPowersMath.Age(5) == 3 && OldPowersMath.Age(7) == 3, "the colony's age as the words take it");

        // ================= The code =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string dir = Path.Combine(src, "OldPowers");
        var files = Directory.GetFiles(dir, "*.cs").ToDictionary(Path.GetFileName, File.ReadAllText);
        string code = string.Join("\n", files.Where(kv => kv.Key != "OldPowersDebug.cs").Select(kv => kv.Value));
        string bare = Regex.Replace(code, @"//[^\n]*", "");
        string frame = files["OldPowers.cs"];
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        C(Read("Happenings/Happenings.cs").Contains("OldPowers.Hourly(now)"), "looked at in the world events' hour, no pass of its own");
        C(!Regex.IsMatch(string.Join("\n", files.Values), @"Sched\.(Rare|Hourly|Every|Slice)") && Regex.Match(files["OldPowersBook.cs"], @"public override void GameComponentTick\(\)\s*\{(.*?)\n        \}", RegexOptions.Singleline)
            .Groups[1].Value.Contains("if (now < nextDue) return;"), "its steps on their own due tick, nothing at all with none under way");
        C(frame.Contains("StoryDirection.Factor(StoryWaysMath.Uncanny)") && frame.Contains("OldPowersMath.CheckChance(e, since, why, lever)") && frame.Contains("OldPowersMath.Spaced("),
            "the lever, the pace and the spacing read where they come");
        C(frame.Contains("MapFacts.ThreatOn(map)") && frame.Contains("if (b.play != null)"), "never on a fight, one under way at a time");
        C(!bare.Contains("MoteMaker.ThrowText") && !bare.Contains("Messages.Message(") && !bare.Contains("ReceiveLetter(") && bare.Contains("Speech.Say("), "speech through Speech.Say, cards and lines through the teller");
        C(!Regex.IsMatch(bare, @"Speech\.Say\([^;]*,[^;]*,\s*(?!null\b)\w"), "a line said to nobody in particular, never naming a second person");
        // Harm only through the shared budget, and shown.
        var injures = Regex.Matches(bare, @"Harm\.Injure\(").Count;
        C(injures == 1 && bare.Contains("Harm.Roll(hurt, HarmKinds.Uncanny, OldPowersMath.BurnChance)") && bare.Contains("Harm.Roll(HarmKinds.Uncanny, OldPowersMath.DeadWalkChance)")
          && bare.Contains("Harm.Roll(HarmKinds.Uncanny, OldPowersMath.WakerChance)") && !Regex.IsMatch(bare, @"TakeDamage\("), "harm only through the shared budget's rolls, its own kind");
        C(HarmKinds.Uncanny.system == "oldPowers" && HarmKinds.All.Contains(HarmKinds.Uncanny) && HarmKinds.Uncanny.weight < 0.01f && Read("Harm/Harm.cs").Contains("case \"oldPowers\": return s.worldEvents;"),
            "the uncanny turn a kind of the shared budget, small, on with the world events");
        C(bare.Contains("LetterDefOf.ThreatSmall") && bare.Contains("customLetterText = OldPowers.T(\"DeadRoad_Walkers\""), "the dead that come for the colony told as a threat, the game's shamblers with the old powers' words");
        // The gods unseen: named only where the colony knows it was that god.
        C(files["OldPowersFinds.cs"].Contains("GodsOnMapWords.Title(g, map, false,") && files["OldPowersFinds.cs"].Contains("Gods.CardName(g, map)") && files["OldPowersFinds.cs"].Contains("GodsOnMapWords.Saw("),
            "a god going by: its card titled by its name only where the colony knows it, the colony's book keeping what its witness saw");
        C(files["OldPowersFinds.cs"].Contains("gb.omens.Add(new GodOmenNote") && files["OldPowersFinds.cs"].Contains("GodComings.Busy(gb)") && files["OldPowersFinds.cs"].Contains("GodPresenceOwn.On(map)"),
            "a god going by shows the god's own signs through the gods' own omens, never on top of a coming or a presence");

        // ================= The storyteller and the direction =================

        var st = XDocument.Load(Path.Combine(root, "Defs", "StorytellerDefs", "Rimfolk_Storytellers.xml")).Root!.Elements("StorytellerDef").ToList();
        var oldPowers = st.FirstOrDefault(d => d.Element("defName")?.Value == "Rimfolk_Story_OldPowers");
        C(oldPowers?.Descendants("levers").First().Element("uncanny")?.Value == "3", "Old Powers (and its direction) three times as often");
        C(st.Where(d => d != oldPowers).All(d => d.Descendants("levers").All(l => l.Element("uncanny") == null)), "every other way leaves them as they are");
        C(StoryWaysMath.IsKey(StoryWaysMath.Uncanny) && !StoryWaysMath.IsHarm(StoryWaysMath.Uncanny) && new StoryLevers().Get(StoryWaysMath.Uncanny) == 1f, "the uncanny a lever of the ways");
        C(oldPowers?.Element("description")?.Value.Contains("old powers' own strange happenings") == true, "Old Powers' description says so");

        // ================= The lines =================

        var defsDir = Path.Combine(root, "Defs", "IncidentDefs");
        var linesDoc = XDocument.Load(Path.Combine(defsDir, "Rimfolk_OldPowers_Lines.xml"));
        var pool = linesDoc.Root!.Elements("ColonyCouncil.FlavorPoolDef").FirstOrDefault(d => d.Element("defName")?.Value == "Rimfolk_Pool_OldPowersSaid");
        var lines = pool?.Descendants("li").Select(l => l.Value).ToList() ?? new List<string>();
        C(lines.Count >= 90, $"the lines said aloud ({lines.Count})");
        string[] eras = { "tribal", "medieval", "industrial", "spacer", "lowtech", "!lowtech" };
        foreach (var l in lines)
        {
            var tags = Tags(l);
            string body = Regex.Replace(l, @"^\[[^\]]*\]\s*", "");
            C(tags.Count > 0, "'" + body + "' is tagged with its moment");
            C(FirstSentence(body) == body && body.Length <= 90 && !body.StartsWith("\"") && !body.Contains("!") && !body.Contains("—"), "'" + body + "' is one short sentence, plainly said");
            C(!body.Contains("{GOD}") || tags.Contains("named"), "'" + body + "' names a god only where the colony is sure");
        }
        // Every moment the code asks for has a line in every age (OldPowers.Line never says another moment's line).
        var moments = Regex.Matches(bare, @"""([a-z]+(?:,[a-z:]+)+)""").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        moments.AddRange(Enum.GetNames(typeof(OldPowersMath.SeerWord)).Select(w => "seer,word:" + w.ToLowerInvariant()));
        moments.AddRange(Enum.GetNames(typeof(OldPowersMath.BeastWord)).Select(w => "beast,word:" + w.ToLowerInvariant()));
        moments.AddRange(new[] { "charged,mage", "charged,psycaster", "charged,sensitive", "godpasses,dawn,named", "godpasses,dawn,scoffer" });
        moments = moments.Where(m => !m.EndsWith(":") && !m.EndsWith(",")).Distinct().ToList();
        C(moments.Count >= 35, $"the moments said ({moments.Count})");
        foreach (var m in moments)
        {
            var want = m.Split(',');
            foreach (var era in new[] { "tribal", "medieval", "industrial", "spacer" })
            {
                var ctx = new HashSet<string>(want) { era };
                if (era == "tribal" || era == "medieval") ctx.Add("lowtech");
                bool any = lines.Any(l =>
                {
                    var t = Tags(l);
                    if (want.Any(w => !t.Contains(w))) return false;
                    return t.All(x => ctx.Contains(x) || x.StartsWith("!") && !ctx.Contains(x.Substring(1)) || x.Contains('|') && x.Split('|').Any(ctx.Contains));
                });
                C(any, m + ": a line in a " + era + " colony");
            }
        }

        // ================= The moods, the condition, the letter =================

        var defs = XDocument.Load(Path.Combine(defsDir, "Rimfolk_OldPowers.xml")).Root!;
        string rates = File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Rates.cs"));
        foreach (var t in defs.Elements("ThoughtDef"))
        {
            string name = t.Element("defName")!.Value;
            int mood = int.Parse(t.Descendants("baseMoodEffect").First().Value);
            float days = float.Parse(t.Element("durationDays")!.Value, System.Globalization.CultureInfo.InvariantCulture);
            C(Math.Abs(mood) <= 4 && mood != 0 && days > 0f && days <= 3f && bare.Contains("\"" + name + "\""), name + ": a small mood (" + mood + ") for a few days at most, given by the code");
            C(rates.Contains("(\"" + name + "\""), name + " in the balance model");
            string desc = t.Descendants("description").First().Value;
            C(!desc.Contains("—") && !desc.Contains("!"), name + ": its words plain");
        }
        C(defs.Elements("ThoughtDef").Count() == 9, "nine small moods");
        var sky = defs.Elements("GameConditionDef").FirstOrDefault(d => d.Element("defName")?.Value == "Rimfolk_OldP_OtherSky");
        C(sky?.Element("conditionClass")?.Value == "ColonyCouncil.GameCondition_OtherSky" && files["OldPowersNight.cs"].Contains("class GameCondition_OtherSky : GameCondition")
          && files["OldPowersNight.cs"].Contains("public override SkyTarget? SkyTarget(Map map)"), "another sky: its own colours at night");
        var letter = defs.Elements("LetterDef").FirstOrDefault(d => d.Element("defName")?.Value == "Rimfolk_OldPowersLetter");
        C(letter?.Element("letterClass")?.Value == "ColonyCouncil.ChoiceLetter_OldPowers" && files["OldPowersBook.cs"].Contains("class ChoiceLetter_OldPowers : RimfolkLetter"), "the enchanter's question a letter of its own");
        C(files["OldPowersVisitors.cs"].Contains("Tell.CanAsk") && files["OldPowersVisitors.cs"].Contains("Tell.Ask(letter, map)") && files["OldPowersVisitors.cs"].Contains("StartTimeout("),
            "the enchanter asks only when the letters' gap allows, and walks on unanswered");

        // ================= The words =================

        string keyedText = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_OldPowers.xml"));
        var keyed = Regex.Matches(keyedText, @"<(CC_OldP_[A-Za-z0-9_]+)>(.*?)</\1>").Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        var used = new HashSet<string>();
        foreach (Match m in Regex.Matches(bare, @"OldPowers\.T\(""([A-Za-z_]+)""")) used.Add("CC_OldP_" + m.Groups[1].Value);
        foreach (Match m in Regex.Matches(bare, @"OldPowers\.K\(""([A-Za-z_]+)""\)")) used.Add("CC_OldP_" + m.Groups[1].Value);
        foreach (Match m in Regex.Matches(bare, @"OldPowers\.Aged\(""([A-Za-z_]+)""")) for (int a = 0; a < 4; a++) used.Add("CC_OldP_" + m.Groups[1].Value + "_" + a);
        foreach (Match m in Regex.Matches(bare, @"OldPowers\.Card\(map, ""([A-Za-z]+)""")) used.Add("CC_OldP_" + m.Groups[1].Value + "_Title");
        foreach (var p in OldPowersMath.Points) used.Add("CC_OldP_Point_" + p);
        foreach (var w in Enum.GetNames(typeof(OldPowersMath.SeerWord))) used.Add("CC_OldP_Seer_Word_" + w);
        foreach (var k in new[] { "Mage", "Psycaster", "Sensitive" }) used.Add("CC_OldP_Charged_" + k);
        foreach (var k in new[] { "Cache", "Relic", "Magicyte" }) used.Add("CC_OldP_Buried_" + k);
        foreach (var k in new[] { "Graves", "Tree", "Middle" }) used.Add("CC_OldP_Pilgrims_Place_" + k);
        used.Add("CC_OldP_Enchanter_No");
        C(used.Count >= 70, $"the words the code uses ({used.Count})");
        foreach (var k in used) C(keyed.ContainsKey(k), k + " is written");
        foreach (var kv in keyed)
        {
            C(!kv.Value.Contains("—") && !kv.Value.Contains("!"), kv.Key + ": no em dash and no exclamation mark");
            C(kv.Key.EndsWith("TitleNamed") == kv.Value.Contains("{GOD}"), kv.Key + ": a god named only in a named title");
        }
        foreach (var k in new[] { "Seer_Cast", "Scroll_Ruin", "Enchanter_Who", "OtherSky_Text" })
            C(Enumerable.Range(0, 4).Select(a => keyed.GetValueOrDefault("CC_OldP_" + k + "_" + a)).Distinct().Count() == 4, k + ": its own form in every age");

        // ================= The player's mods =================

        var pool2 = new Dictionary<string, bool>();
        using (var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress))
        using (var r = new StreamReader(gz))
        {
            r.ReadLine();
            string row;
            while ((row = r.ReadLine()) != null)
            {
                var c = row.Split('\t');
                if (c.Length < 4) continue;
                pool2[c[1]] = pool2.TryGetValue(c[1], out bool was) && was || c[3] == "yes";
            }
        }
        var own = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories)
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"<defName>([^<]+)</defName>").Cast<Match>().Select(m => m.Groups[1].Value)));
        var named = Regex.Matches(bare, @"""([A-Z][A-Za-z0-9]+(?:_[A-Za-z0-9]+)*)""").Cast<Match>().Select(m => m.Groups[1].Value)
            .Where(s => !s.StartsWith("CC_") && !s.StartsWith("Rimfolk_") && s != "Thunder").Distinct().ToList();
        C(named.Count >= 40, $"the mods' and the game's defs named in the code ({named.Count})");
        foreach (var name in named) C(pool2.TryGetValue(name, out bool active) && active, name + " is in the game or the player's active mods");
        foreach (var name in Regex.Matches(bare, @"""(Rimfolk_[A-Za-z0-9_]+)""").Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
            C(own.Contains(name), name + " is one of Rimfolk's defs");
        foreach (var rom in new[] { "Mote_Casting", "Mote_Enchanting", "Mote_Shadow", "Mote_Ghost", "Mote_Twinkle", "Mote_ManaPuff", "TM_Gong", "ItemEnchanted", "TM_Powerup", "RawMagicyte", "TM_Grimoire",
                     "Chronomancer", "Enchanter", "Seers", "ManaSurge", "TM_ManaStorm" })
            C(named.Contains(rom), "A RimWorld of Magic's " + rom + " used by name");
        return n;
    }

    private static List<string> Tags(string line)
    {
        var m = Regex.Match(line, @"^\[([^\]]*)\]");
        return m.Success ? m.Groups[1].Value.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList() : new List<string>();
    }

    private static string FirstSentence(string s)
    {
        for (int i = 12; i < s.Length; i++)
            if (s[i] == '.' || s[i] == '!' || s[i] == '?')
                return s.Substring(0, i + 1);
        return s;
    }
}
