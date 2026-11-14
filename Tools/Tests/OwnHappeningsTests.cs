using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Each god's own happenings, round 4, batch 1 (the player, 14 November 2026: "each of the gods should feel really unique in
/// what it does and whether or how it interacts with the world, what the impact is, how these things play out on the map";
/// "gods effect should be noticeable"): Hollin, Smoulder, Bellwether, Morel, Ennu, Pip and Cirrus. The pure rules (the
/// pace every happening keeps and each one's own reason, strength and outcome), the defs (the two cards, the thoughts, the
/// lines), the words (every card and book line the code reaches, named only where the colony knows), and the seams (the
/// gods' day and hour, each god's own hooks, the shared harm budget, nothing growing in the save, a dev action for each).
/// </summary>
public static class OwnHappeningsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "each god's own happenings: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        XDocument Def(string rel) => XDocument.Load(Path.Combine(root, "Defs", rel));
        string keyedAll = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var keyed = Regex.Matches(keyedAll, @"^\s*<([A-Za-z0-9_]+)>(.*?)</\1>\s*$", RegexOptions.Multiline).Cast<Match>()
            .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
        var T = GodPaceMath.Trigger.None;
        var O = GodPaceMath.Trigger.Ordinary;
        var S = GodPaceMath.Trigger.Strong;

        // ================= Whose, and how many =================

        var all = OwnHappeningsMath.All;
        C(all.All(h => OwnHappeningsMath.GodOf(h) != null) && OwnHappeningsMath.Gods.Length == 14
          && OwnHappeningsMath.Gods.Take(7).SequenceEqual(new[] { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus" }),
            "every happening is one of the fourteen gods' (this batch's seven first, then batch 2's: OwnHappeningsSevenTests)");
        // Tamiu (key Mog) has two more beside the stay, riddle, keepers and wrapped dead she already had (MogsWays).
        C(OwnHappeningsMath.Gods.All(g => OwnHappeningsMath.Of(g).Count >= (g == "Mog" ? 2 : 3) && OwnHappeningsMath.Of(g).Count <= 5),
            "three to five each: " + string.Join(", ", OwnHappeningsMath.Gods.Select(g => g + " " + OwnHappeningsMath.Of(g).Count)));
        C(all.Select(OwnHappeningsMath.Name).Distinct().Count() == all.Length, "no two gods share a happening's name: each is its own");
        C(all.Contains(OwnHappening.HollinDryads) && all.Contains(OwnHappening.SmoulderBargain) && all.Contains(OwnHappening.MorelMourner)
          && all.Contains(OwnHappening.BellwetherHorn) && all.Contains(OwnHappening.EnnuTest) && all.Contains(OwnHappening.EnnuTales)
          && all.Contains(OwnHappening.PipWontStop) && all.Contains(OwnHappening.PipStars) && all.Contains(OwnHappening.CirrusStar),
            "the lore never built and the asked ones are in: her dryads' word, the bargain at the shaft, the mourner, the horn child, the small tests and tales, the machine that won't stop and its message, the sky watchers' star");

        // ================= The pace =================

        foreach (var h in all)
        {
            var p = OwnHappeningsMath.PaceOf(h);
            if (p.mean <= 0f) continue;
            C(OwnHappeningsMath.DayChance(h, p.gap - 0.5f, S, 1f) == 0f && OwnHappeningsMath.DayChance(h, p.gap + 100f, T, 1f) == 0f,
                $"{h}: never within its gap ({p.gap} days), never without a reason");
            float o = OwnHappeningsMath.DayChance(h, p.gap + p.ramp, O, 0f), s = OwnHappeningsMath.DayChance(h, p.gap + p.ramp, S, 0f);
            float first = OwnHappeningsMath.DayChance(h, p.gap, O, 0f), nearer = OwnHappeningsMath.DayChance(h, p.gap + p.ramp, O, 1f);
            C(o > 0f && s > o && first < o && nearer > o && nearer < o * 1.6f,
                $"{h}: its chance starts small after the gap and grows, more for a strong reason, a little more the nearer its god is ({o:P1} a day once free)");
        }
        C(all.Count(h => OwnHappeningsMath.PaceOf(h).mean > 0f) >= 8 && OwnHappeningsMath.PaceOf(OwnHappening.SmoulderBargain).gap >= 40f
          && OwnHappeningsMath.PaceOf(OwnHappening.BellwetherHurt).gap >= 40f && OwnHappeningsMath.PaceOf(OwnHappening.MorelMourner).gap >= 30f,
            "the rare ones far apart: the bargain, the wounded thrumbo, the mourner");
        C(OwnHappeningsMath.Free(OwnHappening.PipTurret, 6f) && !OwnHappeningsMath.Free(OwnHappening.PipTurret, 5.9f) && !OwnHappeningsMath.Free(OwnHappening.HollinWalk, float.NaN),
            "the ones that answer something on the map keep their own gap too");

        // ================= Hollin =================

        C(OwnHappeningsMath.RootsTrigger(14, 0) == T && OwnHappeningsMath.RootsTrigger(30, 10) == T && OwnHappeningsMath.RootsTrigger(20, 2) == O
          && OwnHappeningsMath.RootsTrigger(45, 3) == S && OwnHappeningsMath.RootsTrigger(45, 10) == O,
            "the roots remember a season of felling with little planted back, strongly a great deal of it");
        var words = OwnHappeningsMath.Words(3, true, true, true, 1);
        C(words.SequenceEqual(new[] { DryadWord.Spare, DryadWord.Wait, DryadWord.Plant, DryadWord.Burn }) && OwnHappeningsMath.Words(2, true, false, false, 0).Count == 0,
            "the dryads' words each point at something real: trees marked to cut, frost on growing fields, a bare field, blight; nothing to point at, no word");
        C(OwnHappeningsMath.DryadsTrigger(0, 3, true) == T && OwnHappeningsMath.DryadsTrigger(2, 0, true) == T && OwnHappeningsMath.DryadsTrigger(1, 2, false) == O
          && OwnHappeningsMath.DryadsTrigger(1, 2, true) == S, "the dryads speak only with something to say and a dryad to say it, more readily by an anima tree");
        C(OwnHappeningsMath.FrostAhead(12f, -3f) && !OwnHappeningsMath.FrostAhead(12f, 8f) && !OwnHappeningsMath.FrostAhead(-4f, -10f) && !OwnHappeningsMath.FrostAhead(float.NaN, -3f),
            "a frost ahead is the next season colder than the fields can stand while it is warm now");
        C(OwnHappeningsMath.WalkChance(false, 1f) == 0f && OwnHappeningsMath.WalkChance(true, 0f) > 0.4f && OwnHappeningsMath.WalkChance(true, 1f) > OwnHappeningsMath.WalkChance(true, 0f)
          && OwnHappeningsMath.WalkSteps >= 2 && OwnHappeningsMath.WalkSpacingMax > OwnHappeningsMath.WalkSpacingMin,
            "her rising walks on in the growing season, more often the nearer she is, twice more along a line");
        C(!OwnHappeningsMath.OldTreeSpared(60f, 30, false, true) && !OwnHappeningsMath.OldTreeSpared(60f, 30, true, false) && !OwnHappeningsMath.OldTreeSpared(30f, 30, true, true)
          && !OwnHappeningsMath.OldTreeSpared(60f, 5, true, true) && OwnHappeningsMath.OldTreeSpared(60f, 30, true, true),
            "the old tree spared: standing in spring after a long while of felling round it");

        // ================= Smoulder =================

        C(OwnHappeningsMath.BargainSilver(599) == 0 && OwnHappeningsMath.BargainSilver(600) == 150 && OwnHappeningsMath.BargainSilver(2000) == 300 && OwnHappeningsMath.BargainSilver(50000) == 400
          && OwnHappeningsMath.BargainSilver(1234) % 10 == 0, "the voice asks a seventh of the silver held, in tens, 150 to 400, nothing from a colony with little");
        C(OwnHappeningsMath.BargainTrigger(500, true, 1f) == T && OwnHappeningsMath.BargainTrigger(1000, false, 1f) == T && OwnHappeningsMath.BargainTrigger(1000, true, 0f) == O
          && OwnHappeningsMath.BargainTrigger(1000, true, 0.5f) == S, "a bargain only for silver held and someone under the mountain, likelier when the deep stirs");
        C(OwnHappeningsMath.Kept(200, 200) && OwnHappeningsMath.Kept(250, 200) && !OwnHappeningsMath.Kept(100, 200) && !OwnHappeningsMath.Kept(0, 0), "kept to the letter: all that was promised went down");
        float gold = 400f, steel = 76f;
        bool keptPays = true, bentDoesnt = true;
        foreach (int thrown in new[] { 150, 220, 300, 400 })
        {
            float back = OwnHappeningsMath.GoldCells(thrown, gold) * gold;
            keptPays &= back >= thrown && back <= thrown * 3f;
            int half = thrown / 2;
            bentDoesnt &= OwnHappeningsMath.SteelCells(half, steel) * steel <= Math.Max(steel, half * 1.5f) && OwnHappeningsMath.SteelCells(half, steel) >= 1;
        }
        C(keptPays && bentDoesnt && OwnHappeningsMath.GoldCells(0, gold) == 0 && OwnHappeningsMath.GoldCells(100000, gold) == OwnHappeningsMath.MaxVein && OwnHappeningsMath.GoldCells(100, float.NaN) == 0,
            "a kept bargain's gold is worth more than the silver thrown (not past three times it); a bent one's steel no more than about what went down");
        C(OwnHappeningsMath.VeinDaysMin >= 1f && OwnHappeningsMath.VeinDaysMax >= OwnHappeningsMath.VeinDaysMin && OwnHappeningsMath.KeptFavour > 0f && OwnHappeningsMath.BentFavour < 0f,
            "the vein shows days later; a bent bargain displeases him (hidden)");
        C(OwnHappeningsMath.NameChance(false, false, true, 1f) == 0f && OwnHappeningsMath.NameChance(true, false, false, 0f) > OwnHappeningsMath.NameChance(false, true, false, 0f)
          && OwnHappeningsMath.NameChance(true, true, true, 0f) > OwnHappeningsMath.NameChance(true, true, false, 0f) && OwnHappeningsMath.NameChance(true, true, true, 1f) <= 1f,
            "a sculpture named for his proper name only by one who holds him or knows of him, likelier for good work");
        C(OwnHappeningsMath.HeatFirst(GodsOnMapMath.StoneFallsFrom - 1, GodsOnMapMath.StoneFallsFrom) && !OwnHappeningsMath.HeatFirst(GodsOnMapMath.StoneFallsFrom, GodsOnMapMath.StoneFallsFrom)
          && !OwnHappeningsMath.HeatFirst(0, 1) && OwnHappeningsMath.HeatHours >= 2, "the heat from below comes on the turn before stone may first fall, for hours");
        C(OwnHappeningsMath.BurnTrigger(100, 0f, true) == S && OwnHappeningsMath.BurnTrigger(5000, 10f, false) == T && OwnHappeningsMath.BurnTrigger(1000, 99f, false) == T
          && OwnHappeningsMath.BurnTrigger(5000, 90f, false) == O, "a burn for a bargain bent, else for a hoard long unpaid; nothing for silver given back lately");
        C(OwnHappeningsMath.BurnChance < 1f && OwnHappeningsMath.BurnHigh <= 5f && OwnHappeningsMath.BurnLow > 0f, "the burn is small, and may not come at all");

        // ================= Bellwether =================

        C(OwnHappeningsMath.HornSpeaks(1f, true, 10f, 0f) && !OwnHappeningsMath.HornSpeaks(1f, false, 10f, 0f) && !OwnHappeningsMath.HornSpeaks(3f, true, 10f, 0f)
          && !OwnHappeningsMath.HornSpeaks(1f, true, 2f, 0f) && !OwnHappeningsMath.HornSpeaks(1f, true, 10f, 0.99f) && !OwnHappeningsMath.HornSpeaks(0f, true, 10f, 0f),
            "a horn child speaks within the hour before the weather turns wet, each their own days apart");
        C(OwnHappeningsMath.HornKept >= 4 && OwnHappeningsMath.HornKept <= 12, "the children who touched the horn are kept, a few at most");
        C(OwnHappeningsMath.HurtTrigger(false, 999f, 999f) == T && OwnHappeningsMath.HurtTrigger(true, 10f, 999f) == T && OwnHappeningsMath.HurtTrigger(true, 999f, 999f) == O
          && OwnHappeningsMath.HurtTrigger(true, 999f, 5f) == S, "a wounded thrumbo only where they live and none was killed lately, strongly after he passed in peace");
        C(OwnHappeningsMath.WaitHoursMost >= 4f && OwnHappeningsMath.DuskTo > OwnHappeningsMath.DuskFrom && OwnHappeningsMath.DawnTo > OwnHappeningsMath.DawnFrom && OwnHappeningsMath.DawnTo <= 12,
            "his road: the edge at dusk, through at dawn, a long wait at a fence before he turns back");

        // ================= Morel =================

        C(OwnHappeningsMath.MournerTrigger(0, 5) == T && OwnHappeningsMath.MournerTrigger(1, 0) == T && OwnHappeningsMath.MournerTrigger(1, 2) == O && OwnHappeningsMath.MournerTrigger(2, 2) == S,
            "the mourner comes only after a death of the colony's, with graves to stand by");
        C(OwnHappeningsMath.RingTrigger(true, false, 0f) == S && OwnHappeningsMath.RingTrigger(false, true, 0f) == O && OwnHappeningsMath.RingTrigger(false, false, 0.1f) == T
          && OwnHappeningsMath.RingTrigger(false, false, 0.9f) == O, "her ring answers a body left lying, rot in the stores, or her drawing near; nothing else");
        C(OwnHappeningsMath.RingPlace(true, true) == RingAt.Body && OwnHappeningsMath.RingPlace(false, true) == RingAt.Stores && OwnHappeningsMath.RingPlace(false, false) == RingAt.Buried,
            "the ring points at the body first, then the rot, else at something buried");
        C(OwnHappeningsMath.LeadHoursMin >= 12f && OwnHappeningsMath.LeadHoursMax <= 36f && OwnHappeningsMath.BreathChance < 1f && OwnHappeningsMath.BreathPawnDays >= 30f,
            "the warning comes the day before; her breath is heard now and then, once in a long while each");

        // ================= Ennu =================

        C(OwnHappeningsMath.Tests(false, false, false, false).Count == 0
          && OwnHappeningsMath.Tests(true, true, true, true).SequenceEqual(new[] { GuestTest.Story, GuestTest.Cook, GuestTest.Graves, GuestTest.Coat })
          && OwnHappeningsMath.Tests(false, true, false, false).SequenceEqual(new[] { GuestTest.Story }),
            "the Guest asks only what the colony can answer: a story with a fire, a name with a cook, graves to show, a coat in the cold");
        C(OwnHappeningsMath.BowlTrigger(true, 999f) == S && OwnHappeningsMath.BowlTrigger(false, 3f) == O && OwnHappeningsMath.BowlTrigger(false, 30f) == T
          && OwnHappeningsMath.BowlChance(S) == 1f && OwnHappeningsMath.BowlChance(O) < 0.5f && OwnHappeningsMath.BowlChance(T) == 0f,
            "the bowl every Open Door night, now and then after a kind welcome, never otherwise");
        C(OwnHappeningsMath.TestCredit > 0 && OwnHappeningsMath.TalesAStay >= 1 && OwnHappeningsMath.TalesAStay <= 3 && OwnHappeningsMath.KindestDays >= 10f,
            "a test answered counts toward the welcome; a tale or two a stay; the kindest sought out for a season");

        // ================= Pip =================

        C(OwnHappeningsMath.FlickChance(99f, false, 1f) == 0f && OwnHappeningsMath.FlickChance(5f, true, 1f) == 0f && OwnHappeningsMath.FlickChance(99f, true, 1f) > OwnHappeningsMath.FlickChance(99f, true, 0f)
          && OwnHappeningsMath.FlickChance(99f, true, 1f) <= 0.5f && OwnHappeningsMath.WontStopTicks == 2500, "a machine runs on now and then when switched off, only where Pip is known, an hour");
        C(OwnHappeningsMath.StarsTrigger(false, 0f, true, 1f) == T && OwnHappeningsMath.StarsTrigger(true, 99f, false, 0f) == T && OwnHappeningsMath.StarsTrigger(true, 1f, false, 0f) == O
          && OwnHappeningsMath.StarsTrigger(true, 99f, false, 0.5f) == O && OwnHappeningsMath.StarsTrigger(true, 99f, true, 0f) == S,
            "the console calls the stars only for a reason: used lately, Pip drawn near, an old terminal to answer");
        C(OwnHappeningsMath.CrossesLine(0, 0, 10, 0, 5, 0.5f) && !OwnHappeningsMath.CrossesLine(0, 0, 10, 0, 5, 2f) && !OwnHappeningsMath.CrossesLine(0, 0, 10, 0, 0.5f, 0f)
          && !OwnHappeningsMath.CrossesLine(0, 0, 10, 0, 9.5f, 0f) && !OwnHappeningsMath.CrossesLine(0, 0, 0.5f, 0, 0.2f, 0f) && OwnHappeningsMath.CrossesLine(0, 0, 0, 10, 0.3f, 6f),
            "across a turret's line is near the line, between the turret and its target");
        C(OwnHappeningsMath.HoldTicks >= 60 && OwnHappeningsMath.HoldTicks <= 180 && OwnHappeningsMath.TurretChance < 1f, "a turret holds its fire a breath, now and then");

        // ================= Cirrus =================

        C(!OwnHappeningsMath.Watches(1, 99f) && OwnHappeningsMath.Watches(2, 99f) && OwnHappeningsMath.Watches(0, 5f), "a colony watches the sky: looked up lately, or its festival kept");
        C(OwnHappeningsMath.Coldest(-12f, -8f, 12) && !OwnHappeningsMath.Coldest(-12f, -14f, 12) && !OwnHappeningsMath.Coldest(-12f, -8f, 5) && !OwnHappeningsMath.Coldest(-2f, 5f, 20),
            "the coldest night: colder than any for days, and properly cold");
        C(OwnHappeningsMath.ThunderTrigger(2, 99f) == T && OwnHappeningsMath.ThunderTrigger(8, 5f) == T && OwnHappeningsMath.ThunderTrigger(8, 30f) == O,
            "dry thunder only after a long clear spell with nothing of the sky's lately");
        C(OwnHappeningsMath.PodTo > OwnHappeningsMath.PodFrom && OwnHappeningsMath.FallChance < 1f && OwnHappeningsMath.AuroraHours >= 2, "the pods come by day; the aurora lasts hours");

        // ================= The defs =================

        var defs = Def("GodDefs/Rimfolk_GodOwnHappenings.xml").Root!;
        var incidents = defs.Elements("IncidentDef").ToDictionary(d => d.Element("defName")!.Value);
        foreach (var (name, worker, options) in new[] { ("Rimfolk_Evt_GodBargain", "ColonyCouncil.HappeningWorker_GodBargain", 3), ("Rimfolk_Evt_GodHurtBeast", "ColonyCouncil.HappeningWorker_GodHurtBeast", 2) })
        {
            var inc = incidents.TryGetValue(name, out var x) ? x : null;
            var ext = inc?.Element("modExtensions")?.Element("li");
            var texts = ext?.Element("texts")?.Elements("li").Select(l => l.Value).ToList() ?? new List<string>();
            C(inc != null && inc.Element("baseChance")?.Value == "0" && inc.Element("workerClass")?.Value == worker && ext.Element("options")!.Elements("li").Count() == options
              && Enumerable.Range(0, 4).All(a => texts.Any(t => t.StartsWith("[age" + a + "]"))),
                $"{name}: fired only by its god's own, its answers, every age's words");
            C(texts.All(t => !t.Contains("Smoulder") && !t.Contains("Bellwether") && !t.Contains("{GOD}")), $"{name}: the card never names the god");
        }
        string skips = File.ReadAllText(Path.Combine(root, "Tools", "playout_skips.txt"));
        C(skips.Contains("Rimfolk_Evt_GodBargain [option 0]") && skips.Contains("Rimfolk_Evt_GodHurtBeast [option 0]"), "the answers play out by the events' own code on the map (the play-out audit knows)");
        var thoughts = defs.Elements("ThoughtDef").ToDictionary(d => d.Element("defName")!.Value);
        foreach (var t in new[] { "Rimfolk_Thought_GodOwnDryadWord", "Rimfolk_Thought_GodOwnBurn", "Rimfolk_Thought_GodOwnMadeHimWait", "Rimfolk_Thought_GodOwnBreath", "Rimfolk_Thought_GodOwnThanked",
                     "Rimfolk_Thought_GodOwnBowl", "Rimfolk_Thought_GodOwnTurretHeld" })
            C(thoughts.TryGetValue(t, out var d) && d.Element("durationDays") != null && Math.Abs(float.Parse(d.Descendants("baseMoodEffect").First().Value)) <= 3f,
                $"{t}: a small memory of what they saw or did");
        var pool = defs.Elements("ColonyCouncil.FlavorPoolDef").Single(d => d.Element("defName")?.Value == "Rimfolk_Pool_GodOwnSaid").Descendants("li").Select(l => l.Value).ToList();
        var bare = pool.Select(l => Regex.Replace(l, @"^\[[^\]]*\]\s*", "")).ToList();
        C(bare.All(l => Regex.Matches(l, @"[.!?](\s|$)").Count == 1 && !l.Contains('!') && !l.Contains('—')), "every spoken line is one sentence, no exclamations, no em dashes");
        var poolTags = new HashSet<string>(pool.Select(l => Regex.Match(l, @"^\[([^\]]*)\]").Groups[1].Value).SelectMany(t => t.Split(',')).Select(t => t.Trim()));
        string code = string.Join("\n", Directory.GetFiles(Path.Combine(src, "Gods", "OnMap"), "Own*.cs").Select(File.ReadAllText));
        var said = Regex.Matches(code, @"OwnHappenings\.Say\([^,]+, ""([a-z,:]+)""").Cast<Match>().Select(m => m.Groups[1].Value.Split(',')[0]).Distinct().ToList();
        said.AddRange(Regex.Matches(code, @"Found\([^""]*""([a-z]+)""").Cast<Match>().Select(m => m.Groups[1].Value));
        C(said.Count >= 40 && said.All(poolTags.Contains), "someone has words for every happening the code speaks of: " + string.Join(" ", said.Where(s => !poolTags.Contains(s)).Distinct()));
        C(new[] { "spare", "wait", "plant", "burn" }.All(w => poolTags.Contains("word:" + w)) && new[] { "story", "cook", "graves", "coat" }.All(w => poolTags.Contains("test:" + w)),
            "a line for each of the dryads' words and each of the Guest's tests");

        // ================= The words =================

        var cards = Regex.Matches(code, @"OwnHappenings\.Card\([^,]+, [^,]+, (?:[^?,]+\? )?""([A-Za-z]+)""(?: : ""([A-Za-z]+)"")?, (true|false|burned)").Cast<Match>()
            // A card sent as a letter only one way (burned ? "Burn" : "BurnDream", burned): the second is a message.
            .SelectMany(m => new[] { (m.Groups[1].Value, m.Groups[3].Value), (m.Groups[2].Value, m.Groups[3].Value == "burned" ? "false" : m.Groups[3].Value) })
            .Where(x => x.Item1.Length > 0).Distinct().ToList();
        foreach (var w in new[] { "Body", "Stores", "Buried" }) cards.Add(("Ring" + w, "false"));
        C(cards.Count >= 22, $"the cards the code sends ({cards.Count})");
        foreach (var (part, letter) in cards)
        {
            bool has = keyed.TryGetValue("CC_GodO_" + part + "_Text", out var text);
            bool titles = letter == "false" || keyed.TryGetValue("CC_GodO_" + part + "_Title", out var title) && keyed.TryGetValue("CC_GodO_" + part + "_TitleNamed", out var named)
                          && !title.Contains("{GOD}") && named.Contains("{GOD}");
            C(has && titles && !text.Contains("{GOD}"), $"{part}: its card says what was seen; a letter's title names the god only in its named form");
        }
        var saws = Regex.Matches(code, @"""(CC_GodO_Saw_[A-Za-z]+)""").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        saws.AddRange(new[] { "Body", "Stores", "Buried" }.Select(w => "CC_GodO_Saw_Ring" + w));
        saws.AddRange(new[] { "Story", "Cook", "Graves", "Coat" }.Select(w => "CC_GodO_Saw_Test" + w));
        C(saws.Distinct().Count() >= 40 && saws.All(k => keyed.TryGetValue(k, out var v) && v.Contains("{WHO}") && v.Contains("{HOME}") && !v.Contains("{GOD}")),
            "the colony's book keeps every one in its witness's words, never the god's mind: " + string.Join(" ", saws.Where(k => !keyed.ContainsKey(k))));
        var ownKeys = keyed.Where(kv => kv.Key.StartsWith("CC_GodO_")).ToList();
        C(ownKeys.Count > 100 && ownKeys.All(kv => !kv.Value.Contains('!') && !kv.Value.Contains('—')), "the words have no exclamations and no em dashes");
        C(Enumerable.Range(0, 6).All(i => keyed.ContainsKey("CC_GodO_StarsMessage_" + i)) && new[] { "Spare", "Wait", "Plant", "Burn" }.All(w => keyed.ContainsKey("CC_GodO_DryadWord_" + w))
          && new[] { "North", "South", "East", "West", "NorthEast", "NorthWest", "SouthEast", "SouthWest" }.All(w => keyed.ContainsKey("CC_GodO_Way_" + w)),
            "the messages to the stars, the dryads' words and the ways from the colony");
        C(!Regex.IsMatch(string.Join(" ", ownKeys.Select(kv => kv.Value)), @"\bin \d+ (days|hours)\b|\btomorrow\b"), "no countdowns in the words");

        // ================= The seams =================

        string book = Read("Gods/OnMap/GodsOnMapBook.cs");
        C(book.Contains("OwnHappenings.Daily(now)") && book.Contains("OwnHappenings.Hourly(now)"), "they run in the gods' own day and hour, with no clock of their own");
        string own = Read("Gods/OnMap/OwnHappenings.cs");
        C(!Regex.IsMatch(code, @"Sched\.(Rare|Hourly|Every|Slice)") && Regex.Match(own, @"public override void GameComponentTick\(\)\s*\{(.*?)\n        \}", RegexOptions.Singleline).Groups[1].Value.Contains("if (now < nextDue) return;"),
            "no Sched slot of their own; their steps only on a due tick while something is under way");
        C(Read("Gods/OnMap/GodRising.cs").Contains("OwnHollin.Rose(map, god, at);") && Read("Gods/OnMap/GodWaking.cs").Contains("OwnSmoulder.Turned(god, map, at, miner, b.deepWarnings);")
          && Read("Gods/OnMap/GodOmens.cs").Contains("OwnHappenings.Sign(god, map, at, kind, speaker);"),
            "the round 2 seams: her rising walks on, the deep warns with heat, a god's own sign sets its happening going");
        string health = Read("Gods/World/GodHealth.cs");
        C(health.Contains("if (OwnHappenings.Forewarn(g, map, wrath)) return true;") && health.Contains("OwnCirrus.Watches(map) && Incident(g, map, \"MeteoriteImpact\", \"meteor\") && OwnCirrus.StarFell(g, map)")
          && health.Contains("OwnPip.Wink(mended);") && health.Contains("OwnBellwether.KinKilled();"),
            "the existing anger and kindness, not forked: Morel's and Pip's anger forewarned, the star only for watchers, the mended machine winks");
        C(own.Contains("bypass = true;") && own.Contains("GodHealth.Wrath(g, map, b.warnWrath);") && own.Contains("g.Key == \"Morel\" && wrath == GodWrath.Breakdown") && own.Contains("g.Key == \"Pip\" && wrath == GodWrath.ShortCircuit"),
            "a forewarned anger still comes, the game's own breakdown or short circuit, the day after its sign");
        string sacred = Read("Gods/OnMap/SacredAnimal.cs");
        C(sacred.Contains("else if (OwnBellwether.Road(s, a, now)) { }") && sacred.Contains("OwnBellwether.Touched(p);") && sacred.Contains("OwnBellwether.LetPass();"),
            "the white beast's stay: his road, the horn child remembered, his peace remembered");
        C(Read("Gods/OnMap/GodGifts.cs").Contains("OwnEnnu.Judged(god, map, t, kindest);") && Read("Gods/Worship/Festivals.cs").Contains("OwnHappenings.FestivalKept(g, map);"),
            "the stranger's judgement and the festivals kept reach the Guest's and the sky's own");
        string ennu = Read("Gods/OnMap/OwnEnnu.cs");
        C(ennu.Contains("s.Credit(by, OwnHappeningsMath.TestCredit);") && ennu.Contains("s.kindnesses++;") && ennu.Contains("GodTales.Tell(x, listener, others, \"Fire\")") && ennu.Contains("Together.When(\"own-ennu-test|\""),
            "a test answered is part of the welcome the stranger judges; its tales go through the stories' own telling; whoever answers walks over to it");
        string smoulder = Read("Gods/OnMap/OwnSmoulder.cs");
        C(smoulder.Contains("Harm.Roll(p, HarmKinds.GodBurn, OwnHappeningsMath.BurnChance)") && smoulder.Contains("Harm.Injure(p, HurtKind.Burn, HurtWhere.Hand")
          && HarmKinds.GodBurn.system == "gods" && HarmKinds.All.Contains(HarmKinds.GodBurn) && !Regex.IsMatch(code.Replace(smoulder, ""), @"Harm\.Injure|TakeDamage\([^)]*Pawn"),
            "the only hurt to the colony is the warm dream's burn, at the shared harm budget's rate, shown on its card");
        C(smoulder.Contains("ThingDefOf.Silver") && smoulder.Contains("OwnHappenings.Carry(by, stack, shaft, want)") && smoulder.Contains("\"MineableGold\"") && smoulder.Contains("\"MineableSteel\""),
            "the bargain plays out: the silver carried to the shaft and gone, gold or steel in the rock after");
        string pip = Read("Gods/OnMap/OwnPip.cs");
        C(pip.Contains("\"burstCooldownTicksLeft\"") && own.Contains("[HarmonyPatch(typeof(Building_TurretGun), \"TryStartShootSomething\")]") && own.Contains("[HarmonyPatch(typeof(CompFlickable), nameof(CompFlickable.DoFlick))]")
          && own.Contains("[HarmonyPatch(typeof(CompArt), nameof(CompArt.JustCreatedBy))]") && own.Contains("[HarmonyPatch(typeof(Plant), nameof(Plant.PlantCollected))]"),
            "their hooks into the game: the turret's shot, a switch, a work of art made, a tree felled, each off with the deeper hooks");
        C(Regex.Matches(own, @"if \(!RimfolkPatches\.On").Count >= 4 && own.Contains("if (!__result || !RimfolkPatches.On || !Gods.On) return;"), "every hook stands down with the deeper game hooks setting");
        C(Read("Weather/WeatherDynamics.cs").Contains("public static WeatherDef Foreseen(Map map, int within, out int left)") && Read("Gods/OnMap/OwnBellwether.cs").Contains("WeatherDynamics.Foreseen("),
            "the horn child's rain is the weather's own next turn, held to as a forecast is, so what they say comes true");
        string sky = Read("Gods/OnMap/GodSky.cs");
        C(sky.Contains("Mourner") && sky.Contains("case SkySign.Mourner:") && Read("Gods/OnMap/OwnMorel.cs").Contains("GodSky.Stay(map, SkySign.Mourner"),
            "the mourner is drawn at the graves from dusk to dawn");
        C(code.Contains("PresenceSpec.For(g, map, PresenceKind.Appear, at)") && code.Contains("GodPresenceSeam.Call(spec)"), "the mourner's coming is a god's moment (the round 2 seam: her own presence after it)");
        // Saved: the one-a-happening list, the horn children and the sky's watches, each kept in bounds.
        var lists = Regex.Matches(own, @"Scribe_Collections\.Look\(ref (\w+)").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        C(lists.SequenceEqual(new[] { "last", "horned", "watches" }) && code.Contains("while (b.horned.Count > OwnHappeningsMath.HornKept)") && code.Contains("while (b.watches.Count > OwnHappeningsBook.MaxWatches)"),
            "nothing grows in the save: one entry a happening, a few horn children, a few watches of the sky");
        string save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(lists.All(l => save.Contains("\"Gods/OnMap/OwnHappenings.cs:" + l + "\"")), "every saved list is modelled in the save estimate");
        // Batch 1's dev actions are in one file, batch 2's in another (Tamiu's by the name she is said by); what each brings
        // about is named in its own god's file.
        string dbg = Read("Gods/OwnHappeningsDebug.cs") + Read("Gods/OwnSevenDebug.cs") + string.Join("\n", new[] { "Hush", "Undertow", "Helios", "Selene", "Murmur", "Ducat", "Tamiu" }
            .Select(g => Read("Gods/OnMap/Own" + g + ".cs")));
        foreach (var h in all)
            C(Regex.IsMatch(dbg, @"DebugAction\(""Rimfolk"", ""Gods: " + (OwnHappeningsMath.GodOf(h) == "Mog" ? "Tamiu" : OwnHappeningsMath.GodOf(h)) + @"'s [^""]+""")
              && dbg.Contains("OwnHappening." + h),
                $"{h}: a dev action brings it now");
        C(Regex.Matches(dbg, @"DebugAction\(").Count >= all.Length, "a dev action for each happening");
        C(Read("Gods/OnMap/OwnCirrus.cs").Contains("\"Skygaze\", \"UseTelescope\", \"VSIE_Skygaze\"") && Read("Gods/OnMap/OwnHollin.cs").Contains("p.RaceProps?.Dryad == true"),
            "the player's mods softly: any mod's dryads (Vanilla Ideology Expanded - Dryads), Vanilla Social Interactions Expanded's skygazing, A RimWorld of Magic's motes by name");
        return n;
    }
}
