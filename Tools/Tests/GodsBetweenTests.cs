using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The gods between the colony's people (Gods/OnMap/Between/; the player, 20 November 2026: "additional God events ... the
/// relations between pawns, the kind of events that generated from it"): two or more happenings of each of the fifteen
/// gods' own that set its people toward or against each other. Their pace, the pure rules of who stands where and how each
/// ends between two people, each one's code, card in three wordings, book line, spoken lines and dev action, the unseen
/// gods (named only on a card's named title, never the Hunger), no harm, saved small; and the older happening cards in three
/// wordings too (C31).
/// </summary>
public static class GodsBetweenTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the gods between the colony's people: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string dir = Path.Combine(src, "Gods", "OnMap", "Between");
        string frame = Read("Gods/OnMap/Between/Between.cs");
        var files = Directory.GetFiles(dir, "Between*.cs").ToDictionary(Path.GetFileNameWithoutExtension, File.ReadAllText);
        string code = string.Join("\n", files.Values);
        string debug = Read("Gods/OwnBetweenDebug.cs");
        string keyedAll = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var keyed = Regex.Matches(keyedAll, @"^\s*<([A-Za-z0-9_]+)>(.*?)</\1>\s*$", RegexOptions.Multiline).Cast<Match>()
            .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f).Root!).ToList();
        var pool = defs.SelectMany(d => d.Elements().Where(e => e.Name.LocalName == "ColonyCouncil.FlavorPoolDef" && e.Element("defName")?.Value == OwnBetweenPool))
            .SelectMany(e => e.Descendants("li")).Select(l => l.Value).ToList();
        string Tags(string l) => Regex.Match(l, @"^\[([^\]]*)\]").Groups[1].Value;
        string Body(string l) => Regex.Replace(l, @"^\[[^\]]*\]\s*", "");
        var T = GodPaceMath.Trigger.None;
        var O = GodPaceMath.Trigger.Ordinary;
        var S = GodPaceMath.Trigger.Strong;
        HashSet<string> Ph(string s) => new HashSet<string>(Regex.Matches(s ?? "", @"\{([A-Z_a-z0-9]+)\}").Cast<Match>().Select(m => m.Groups[1].Value));

        // ================= The happenings and their pace =================

        var mine = OwnHappeningsMath.All.Where(OwnHappeningsMath.IsBetween).ToList();
        C(mine.Count == 31 && mine.Min(h => (int)h) > (int)OwnHappening.RevelCup, $"thirty-one, added after every older one so a save's list keeps its order ({mine.Count})");
        foreach (var g in OwnHappeningsMath.Gods)
            C(mine.Count(h => OwnHappeningsMath.GodOf(h) == g) >= 2, g + ": two or more of its own");
        C(mine.Count(h => OwnHappeningsMath.GodOf(h) == "Helios") == 3 && mine.Contains(OwnHappening.HeliosSun), "Helios's three, out into the sun (C9) among them");
        C(mine.All(h => OwnHappeningsMath.GodOf(h) != null) && mine.Select(OwnHappeningsMath.Name).Distinct().Count() == mine.Count, "each its own, of one god");
        foreach (var h in mine)
        {
            var p = OwnHappeningsMath.PaceOf(h);
            C(p == OwnHappeningsMath.BetweenPaceOf(h) && p.gap >= 3f, h + ": its own pace, a gap of " + p.gap + " days");
            C(OwnHappeningsMath.DayChance(h, 999f, T, 1f) == 0f, h + ": nothing without a reason in the world");
            if (p.mean <= 0f)
            {
                C(OwnHappeningsMath.FollowChance(h, p.gap - 0.5f, 1f, 1f) == 0f && OwnHappeningsMath.FollowChance(h, p.gap, 0.3f, 0f) > 0f
                  && OwnHappeningsMath.FollowChance(h, p.gap, 0.3f, 1f) >= OwnHappeningsMath.FollowChance(h, p.gap, 0.3f, 0f),
                    h + ": it follows what calls it, never within its gap, a little likelier the nearer its god");
                continue;
            }
            C(p.ramp > 0f && OwnHappeningsMath.DayChance(h, p.gap - 0.5f, S, 1f) == 0f && OwnHappeningsMath.DayChance(h, p.gap, O, 0f) < OwnHappeningsMath.DayChance(h, p.gap + p.ramp, O, 0f),
                h + ": never within its gap, growing over its ramp");
        }

        // ================= Who stands where, and how it ends =================

        C(OwnHappeningsMath.Stance(true, true, true, true) == BetweenStance.Holds && OwnHappeningsMath.Stance(false, false, false, true) == BetweenStance.Doubts
          && OwnHappeningsMath.Stance(false, true, true, false) == BetweenStance.Rival && OwnHappeningsMath.Stance(false, false, true, false) == BetweenStance.Other
          && OwnHappeningsMath.Stance(false, false, false, false) == BetweenStance.Neither, "holding the god first, then doubting, a rival's, another's, none");
        C(OwnHappeningsMath.Shared(BetweenStance.Holds, BetweenStance.Neither) == BetweenOutcome.Closer && OwnHappeningsMath.Shared(BetweenStance.Holds, BetweenStance.Doubts) == BetweenOutcome.Quarrel
          && OwnHappeningsMath.Shared(BetweenStance.Rival, BetweenStance.Holds) == BetweenOutcome.Quarrel && OwnHappeningsMath.Shared(BetweenStance.Neither, BetweenStance.Other) == BetweenOutcome.None
          && OwnHappeningsMath.Shared(BetweenStance.Doubts, BetweenStance.Neither) == BetweenOutcome.None,
            "a sign shared: closer with one who holds it and nobody against it, a quarrel when one holds it and one is set against it");
        C(OwnHappeningsMath.Mend(BetweenStance.Neither, BetweenStance.Holds) == BetweenOutcome.MadeUp && OwnHappeningsMath.Mend(BetweenStance.Holds, BetweenStance.Doubts) == BetweenOutcome.Quarrel,
            "two at odds make it up, unless one scoffs at the sign");
        C(OwnHappeningsMath.Shown(BetweenStance.Holds) == BetweenOutcome.Ashamed && OwnHappeningsMath.Shown(BetweenStance.Neither) == BetweenOutcome.Ashamed
          && OwnHappeningsMath.Shown(BetweenStance.Doubts) == BetweenOutcome.Quarrel && OwnHappeningsMath.Shown(BetweenStance.Rival) == BetweenOutcome.Quarrel,
            "one shown their own deed is ashamed, unless they doubt it or hold a rival god");
        C(OwnHappeningsMath.Laugh(OwnHappeningsMath.GoodSportFrom) == BetweenOutcome.Closer && OwnHappeningsMath.Laugh(0f) == BetweenOutcome.Laughed, "a good sport laughs along");
        C(OwnHappeningsMath.Sorry(OwnHappeningsMath.SorryAcceptFrom) == BetweenOutcome.MadeUp && OwnHappeningsMath.Sorry(-80f) == BetweenOutcome.Quarrel, "a sorry taken unless the hurt runs too deep");
        C(OwnHappeningsMath.SameDream(10f, 5f) == BetweenOutcome.Closer && OwnHappeningsMath.SameDream(10f, -20f) == BetweenOutcome.Wary, "the same dream: closer for friends, uneasy otherwise");
        C(OwnHappeningsMath.Helped(true) == BetweenOutcome.Kind && OwnHappeningsMath.Helped(false) == BetweenOutcome.None, "a kindness remembered only when it came");

        // ================= Each god's rules =================

        C(OwnHappeningsMath.StumpsTrigger(11, 0) == T && OwnHappeningsMath.StumpsTrigger(15, 5) == T && OwnHappeningsMath.StumpsTrigger(15, 2) == O && OwnHappeningsMath.StumpsTrigger(30, 2) == S,
            "Hollin's brambles: many trees felled by one, few planted back");
        C(OwnHappeningsMath.BoundChance > 0f && OwnHappeningsMath.BoundChance < 0.5f && OwnHappeningsMath.BoundFightDays <= 2f, "Hollin's saplings: a fight lately, now and then");
        C(OwnHappeningsMath.PraiseChance(4) == 0f && OwnHappeningsMath.PraiseChance(5) > 0f && OwnHappeningsMath.PraiseChance(6) > OwnHappeningsMath.PraiseChance(5),
            "Smoulder's forge: a masterwork sometimes, a legend nearly always, nothing less");
        C(OwnHappeningsMath.NearMissTrigger(false, 1f) == T && OwnHappeningsMath.NearMissTrigger(true, 0f) == O && OwnHappeningsMath.NearMissTrigger(true, 0.5f) == S,
            "Smoulder's stones: two under the mountain, likelier when the deep stirs");
        C(OwnHappeningsMath.ToldTrigger(false, 5, true) == T && OwnHappeningsMath.ToldTrigger(true, 0, true) == T && OwnHappeningsMath.ToldTrigger(true, 3, false) == O
          && OwnHappeningsMath.ToldTrigger(true, 3, true) == S, "Bellwether's tale at the edge: an old one, a child and grazers on the land");
        C(OwnHappeningsMath.MaySpare(20f, false) && !OwnHappeningsMath.MaySpare(5f, false) && !OwnHappeningsMath.MaySpare(20f, true), "a beast let go only with food enough, never by a scoffer");
        C(OwnHappeningsMath.TwoHandsTrigger(2f, false) == T && OwnHappeningsMath.TwoHandsTrigger(6f, true) == T && OwnHappeningsMath.TwoHandsTrigger(6f, false) == O
          && OwnHappeningsMath.TwoHandsTrigger(20f, false) == S, "Pip's two hands: a machine broken for hours with nobody at it");
        C(OwnHappeningsMath.WhisperTrigger(-60f, 0.5f, false) == T && OwnHappeningsMath.WhisperTrigger(0f, 0.5f, true) == T && OwnHappeningsMath.WhisperTrigger(-60f, 0.5f, true) == O
          && OwnHappeningsMath.WhisperTrigger(-60f, 0.1f, true) == S, "the whisperer: a deep grudge or despair, at night");
        C(OwnHappeningsMath.SunNoon(12, 22f, true, false) && !OwnHappeningsMath.SunNoon(12, 22f, true, true) && !OwnHappeningsMath.SunNoon(12, 22f, false, false)
          && !OwnHappeningsMath.SunNoon(17, 22f, true, false) && !OwnHappeningsMath.SunNoon(12, 5f, true, false) && OwnHappeningsMath.SunEase > 0f && OwnHappeningsMath.SunEase <= 0.1f,
            "out into the sun (C9): a clear, warm noon, never a heat wave, the illness eased a little");
        C(OwnHappeningsMath.MoonEnough(2) && OwnHappeningsMath.MoonEnough(3) && !OwnHappeningsMath.MoonEnough(1), "the sorry after a night under a moon half full or more");
        C(OwnHappeningsMath.FirstWins(10, 5, 0.6f) && !OwnHappeningsMath.FirstWins(5, 10, 0.6f) && OwnHappeningsMath.FirstWins(7, 7, 0.4f), "the riddles: the better wit mostly, luck between equals");
        C(OwnHappeningsMath.WaitHour(23) && OwnHappeningsMath.WaitHour(1) && !OwnHappeningsMath.WaitHour(12) && OwnHappeningsMath.NightHour(2) && !OwnHappeningsMath.NightHour(14)
          && OwnHappeningsMath.NoiseHour(23) && OwnHappeningsMath.WagerHour(22) && OwnHappeningsMath.DeepNight(2) && !OwnHappeningsMath.DeepNight(22), "each at its own hours");
        C(OwnHappeningsMath.CoinHands >= 2 && OwnHappeningsMath.MiserHeaps >= 3 && OwnHappeningsMath.MiserSilverFrom > 0, "the coin passed on, the miser's silver in heaps");

        // ================= Each one on the map, and its words =================

        string pc(OwnHappening h) => h.ToString().ToLowerInvariant();
        foreach (var h in mine)
        {
            string god = OwnHappeningsMath.GodOf(h) == "Mog" ? "Tamiu" : OwnHappeningsMath.GodOf(h);
            C(Regex.IsMatch(code, @"public class " + h + @" : BetweenKind") && frame.Contains("new " + h + "()"), h + ": its own kind, in the frame's list");
            C(Regex.IsMatch(debug, @"DebugAction\(""Rimfolk"", ""Gods: " + god + @"'s [^""]+""[^\n]*\n[^\n]*Fire\(OwnHappening\." + h + @"\)"), h + ": a dev action brings it now");
            C(keyed.ContainsKey("CC_GodB_Saw_" + h) && keyed["CC_GodB_Saw_" + h].Contains("{HOME}"), h + ": the colony's book keeps it (CC_GodB_Saw_)");
            C(keyed.ContainsKey("CC_GodB_" + h + "_What"), h + ": what it was, in a few words for the news");
            // Every text the card may use, in three wordings with the same placeholders.
            var texts = keyed.Keys.Where(k => k.StartsWith("CC_GodB_" + h + "_Text") && !Regex.IsMatch(k, @"_\d$")).ToList();
            C(texts.Count >= 1, h + ": a card");
            foreach (var k in texts)
                C(keyed.ContainsKey(k + "_2") && keyed.ContainsKey(k + "_3") && Ph(keyed[k + "_2"]).SetEquals(Ph(keyed[k])) && Ph(keyed[k + "_3"]).SetEquals(Ph(keyed[k]))
                  && !Ph(keyed[k]).Contains("GOD"), k + ": three wordings, the same placeholders, never the god's name");
            // The words its code says: each part has lines, one sentence each.
            var cls = Regex.Match(code, @"public class " + h + @" : BetweenKind(.*?)(?=\n    public class |\z)", RegexOptions.Singleline).Groups[1].Value;
            var said = Regex.Matches(cls, @"OwnBetween\.Say\([^;]*?""([a-z]+)""").Cast<Match>().Select(m => m.Groups[1].Value)
                .Concat(Regex.Matches(cls, @"\? ""([a-z]+)"" : ""([a-z]+)""").Cast<Match>().SelectMany(m => new[] { m.Groups[1].Value, m.Groups[2].Value }))
                .Concat(Regex.Matches(cls, @": ""([a-z]+)"", g").Cast<Match>().Select(m => m.Groups[1].Value)).Distinct().ToList();
            C(said.Count >= 2, h + ": two or more speak in it (" + string.Join(", ", said) + ")");
            foreach (var part in said.Where(p => p.Length > 2))
                C(pool.Any(l => Tags(l).Split(',').Select(t => t.Trim()).SequenceEqual(new[] { pc(h) + "_" + part })), h + ": lines for its part \"" + part + "\"");
        }
        var sentence = new Regex(@"[.?!][""']?\s+[A-Z{]");
        C(pool.Count >= 150, $"the spoken lines ({pool.Count})");
        foreach (var l in pool)
        {
            var tags = Tags(l).Split(',').Select(t => t.Trim()).ToList();
            C(!sentence.IsMatch(Body(l)) && Body(l).Length <= 90 && !Body(l).Contains("!") && !Body(l).Contains("—"), "one sentence, plain: " + Body(l));
            C(Body(l).Contains("{GOD}") == tags.Contains("named"), "the god named only on a line for when the colony is sure: " + Body(l));
            C(!(tags[0].StartsWith("undertow") && tags.Contains("named")), "the Hunger never named: " + Body(l));
            C(mine.Any(h => tags[0].StartsWith(pc(h) + "_")), "every line is one of theirs: " + tags[0]);
        }

        // ================= How it ends, said plainly =================

        foreach (var o in Enum.GetValues(typeof(BetweenOutcome)).Cast<BetweenOutcome>().Where(o => o != BetweenOutcome.None))
        {
            C(keyed.ContainsKey("CC_GodB_Out_" + o) && keyed.ContainsKey("CC_GodB_Out_" + o + "_2") && keyed.ContainsKey("CC_GodB_Out_" + o + "_3"), o + ": told in the news in three wordings");
            C(keyed.ContainsKey("CC_GodB_Journal_" + o) && Regex.IsMatch(frame, @"case BetweenOutcome\." + o + ":"), o + ": kept in a journal, and done");
        }
        foreach (Match m in Regex.Matches(frame, @"""(Rimfolk_Between_[A-Za-z]+)"""))
            C(defs.Any(d => d.Elements().Any(e => e.Name.LocalName == "ThoughtDef" && e.Element("defName")?.Value == m.Groups[1].Value)), m.Groups[1].Value + ": the memory is a def");
        C(frame.Contains("PawnGods.Move(a, ga, BeliefEvent.Bickered)") && frame.Contains("RemoveMemoriesOfDefWhereOtherPawnIs"), "a quarrel holds each to their god the harder; making it up lets the old quarrel go");
        C(frame.Contains("OwnHappenings.Saw(g, map, at, kind, witness, \"CC_GodB_Saw_\" + h, more)") && frame.Contains("GodLore.Disputed(doubter, g, line)"),
            "the colony's book keeps it as its witness saw it (and those who saw it feel it, GodTouch), a doubter's word set down beside it");
        C(GodTouchMath.InOwnEffectKeys("CC_GodB_Saw_HollinBound") && GodTouchMath.HasOwnEffect("CC_GodB_Saw_RevelWager"), "each carries what it does between two people, so no effect of the touch's comes on top");

        // ================= The unseen gods, no harm, saved small =================

        C(frame.Contains("h == OwnHappening.UndertowWhisper || h == OwnHappening.UndertowLampOut ? null : g") && !keyed.Where(kv => kv.Key.StartsWith("CC_GodB_Undertow")).Any(kv => kv.Value.Contains("{GOD}")),
            "the Hunger's cards never named");
        C(keyed.Where(kv => kv.Key.StartsWith("CC_GodB_") && kv.Key.Contains("TitleNamed")).All(kv => kv.Value.Contains("{GOD}") || kv.Key.Contains("Undertow"))
          && keyed.Where(kv => kv.Key.StartsWith("CC_GodB_") && Regex.IsMatch(kv.Key, @"_Text")).All(kv => !kv.Value.Contains("{GOD}")),
            "a card's words never name the god; only a letter's title, where the colony knows");
        C(!Regex.IsMatch(code + frame, @"Harm\.(Injure|Ail|Fight)|TakeDamage|DamageInfo|\.Kill\(|StartSocialFight"), "nobody is hurt: no wound, no fight");
        C(frame.Contains("while (scenes.Count > MaxScenes)") && frame.Contains("while (welcomed.Count > WelcomedKept)") && frame.Contains("while (answered.Count > AnsweredKept)")
          && frame.Contains("while (sealedSeen.Count > SealedKept)") && frame.Contains("OwnHappeningsMath.FellersKept"), "saved small: each list kept in bounds");
        string save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        foreach (var l in new[] { "scenes", "fellers", "welcomed", "answered", "sealedSeen" })
            C(save.Contains("\"Gods/OnMap/Between/Between.cs:" + l + "\""), l + ": modelled in the save estimate");
        C(Read("Gods/OnMap/OwnHappenings.cs").Contains("ExposeBetween();") && Read("Gods/OnMap/OwnSeven.cs").Contains("OwnBetween.Hourly(b, map, now)")
          && Read("Gods/OnMap/OwnSeven.cs").Contains("OwnBetween.Steps(b, now)") && Read("Gods/OnMap/OwnHappenings.cs").Contains("HollinStumps.Felled(b, plant, by);"),
            "in the frame: saved with the book, the gods' hour and steps, the trees felled");
        C(frame.Contains("if (b.between.scenes.Any(s => s.map == map && s.stage >= 0)) return;") && frame.Contains("Kinds.InRandomOrder()"), "one at a time at a home, no god first every hour");
        C(Regex.IsMatch(code, @"Patch_BetweenFinePiece") && code.Contains("if (!RimfolkPatches.On || !Gods.On"), "the fine piece's hook stands down with the deeper game hooks");
        C(code.Contains("FirstPot.WillDrink(s.b, cup.def)") && code.Contains("DefDatabase<PawnKindDef>.GetNamedSilentFail(OwnMurmur.FrogKind)") && code.Contains("RevelGoods.HeldDrink(map)"),
            "the cup only as their drug policy and faith allow; the frogs only with Odyssey's; the drink the colony's own");

        // ================= C31: every older happening card in three wordings =================

        var cardFiles = new[] { "Rimfolk_GodOwnHappenings", "Rimfolk_GodHappenings_Ducat", "Rimfolk_GodHappenings_Helios", "Rimfolk_GodHappenings_Hush", "Rimfolk_GodHappenings_Murmur",
            "Rimfolk_GodHappenings_Revel", "Rimfolk_GodHappenings_Selene", "Rimfolk_GodHappenings_Tamiu", "Rimfolk_GodHappenings_Undertow", "Rimfolk_GodsOnMap", "Rimfolk_GodsOnMap_Mog", "Rimfolk_GodFeasts" };
        var cards = cardFiles.SelectMany(f => Regex.Matches(File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", f + ".xml")), @"^\s*<(CC_[A-Za-z0-9_]*_Text[A-Za-z0-9]*)>", RegexOptions.Multiline)
            .Cast<Match>().Select(m => m.Groups[1].Value)).Distinct().ToList();
        C(cards.Count >= 90, $"the older happening cards ({cards.Count})");
        foreach (var k in cards)
            C(keyed.ContainsKey(k + "_2") && keyed.ContainsKey(k + "_3") && Ph(keyed[k + "_2"]).SetEquals(Ph(keyed[k])) && Ph(keyed[k + "_3"]).SetEquals(Ph(keyed[k]))
              && !keyed[k + "_2"].Contains("!") && !keyed[k + "_3"].Contains("!"), k + ": three wordings, the same placeholders");
        string onMap = string.Join("\n", Directory.GetFiles(Path.Combine(src, "Gods", "OnMap"), "*.cs").Select(File.ReadAllText));
        C(onMap.Contains("TextVariants.Vary(key + \"_Text\")") && onMap.Contains("TextVariants.Vary(\"CC_GodHelios_LookBack_Text\")") && onMap.Contains("TextVariants.Vary(K(\"Smoulder\", \"Text\"))")
          && onMap.Contains("TextVariants.Vary((IsMog(s) ? \"CC_Mog_Came_Text\" : \"CC_GodM_Animal_Text\") + (kin ? \"Kin\" : \"\"))"), "each is told in one of its wordings at random");
        foreach (var k in cards.Where(k => onMap.Contains("\"" + k + "\"")))
            C(Regex.IsMatch(onMap, @"TextVariants\.Vary\([^;]*""" + k + @""""), k + ": through the wordings");

        return n;
    }

    private const string OwnBetweenPool = "Rimfolk_Pool_GodBetween_Say";
}
