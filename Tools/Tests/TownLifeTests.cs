using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// A town the colony was in its own life (Colonies/TownLifeMath.cs; docs/design/stage3-many-colonies.md, section 2, "The
/// bad side", "Taking it back" and "Its growth"; release 5, the player's decisions 11B and 15B): a raid on it by the two
/// sides' points, what an unanswered raid costs and when one of the colony's former people can die in it; the town asking
/// to come back in hard times, refused twice its people at the gate, the former colonists first; its goodwill cooling with
/// the colony's wrongs and a cool head; the region's plagues and famines; and the code's seams with the game (the world
/// event, the defeat check, the raid letter, growth only at Faction Territories' pace, the words, the balance).
/// </summary>
public static class TownLifeTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "town life: " + what);
        }

        const int Day = TownLifeMath.DayTicks;

        // ---------------- A raid on the town: the two sides' points ----------------

        C(TownLifeMath.DefendPoints(1400f, -1f) == 1400f, "no fortune known: the game's own defenders");
        C(Math.Abs(TownLifeMath.DefendPoints(1400f, 50f) - 1400f) < 0.01f, "a steady town (50): the game's own");
        C(Math.Abs(TownLifeMath.DefendPoints(1000f, 0f) - 700f) < 0.01f && Math.Abs(TownLifeMath.DefendPoints(1000f, 100f) - 1300f) < 0.01f,
            "a struggling town stands with 0.7 of them, a flourishing one with 1.3");
        C(TownLifeMath.DefendPoints(1000f, 10f) < TownLifeMath.DefendPoints(1000f, 60f), "the poorer, the fewer");
        C(Math.Abs(TownLifeMath.RaidPoints(1000f, 0f) - 750f) < 0.01f && Math.Abs(TownLifeMath.RaidPoints(1000f, 1f) - 1500f) < 0.01f,
            "raiders 0.75 to 1.5 of the town's strength");
        C(TownLifeMath.Ratio(1500f, 1000f) == 1.5f && TownLifeMath.Ratio(10f, 0f) == 9f, "their strength to the town's");
        C(Math.Abs(TownLifeMath.HoldChance(1000f, 1000f) - 0.5f) < 0.001f, "even sides, an even chance");
        C(TownLifeMath.HoldChance(100000f, 1f) == 0.1f && TownLifeMath.HoldChance(1f, 100000f) == 0.9f, "never surely either way");
        C(TownLifeMath.HoldChance(1500f, 1000f) < TownLifeMath.HoldChance(800f, 1000f), "the stronger the raiders, the less likely it holds");

        // Alone: held on the roll, else hurt, else sacked when much the stronger.
        C(TownLifeMath.Alone(800f, 1000f, 0.1f) == TownRaidEnd.Held, "unanswered, held on a good roll");
        C(TownLifeMath.Alone(1100f, 1000f, 0.95f) == TownRaidEnd.Hurt, "lost to raiders a little the stronger: hurt");
        C(TownLifeMath.Alone(1300f, 1000f, 0.95f) == TownRaidEnd.Sacked, "lost to raiders a quarter the stronger: sacked");
        // Over many raids, the share held follows the points.
        var rng = new Random(5);
        int held = 0;
        for (int i = 0; i < 4000; i++)
            if (TownLifeMath.Alone(1000f, 1000f, (float)rng.NextDouble()) == TownRaidEnd.Held)
                held++;
        C(Math.Abs(held / 4000f - 0.5f) < 0.03f, $"even sides hold about half the time ({held / 40f:0}%)");

        // On the map: the fight the player saw.
        C(TownLifeMath.OnMap(0, 3, 2, false) == TownRaidEnd.Defended, "every raider down or fled: defended");
        C(TownLifeMath.OnMap(0, 0, 0, true) == TownRaidEnd.Defended, "the raiders gone before the map closed: defended");
        C(TownLifeMath.OnMap(4, 0, 0, false) == TownRaidEnd.Sacked, "nobody standing against the raiders: sacked");
        C(TownLifeMath.OnMap(4, 5, 2, false) == TownRaidEnd.None, "both still up: the fight goes on");
        C(TownLifeMath.OnMap(2, 5, 0, true) == TownRaidEnd.Held, "the colony's fighters left, the town the stronger: held");
        C(TownLifeMath.OnMap(6, 5, 0, true) == TownRaidEnd.Hurt, "the colony's fighters left, the raiders a little the stronger: hurt");
        C(TownLifeMath.OnMap(8, 5, 0, true) == TownRaidEnd.Sacked && TownLifeMath.OnMap(3, 0, 1, true) == TownRaidEnd.Sacked, "much the stronger, or nobody of the town up: sacked");

        // What it costs.
        C(TownLifeMath.FortuneLoss(TownRaidEnd.Defended) == 0f && TownLifeMath.GoodsLost(TownRaidEnd.Defended) == 0f && TownLifeMath.OthersLost(TownRaidEnd.Defended) == 0,
            "defended with the colony: nothing lost");
        C(TownLifeMath.FortuneLoss(TownRaidEnd.Held) < TownLifeMath.FortuneLoss(TownRaidEnd.Hurt) && TownLifeMath.FortuneLoss(TownRaidEnd.Hurt) < TownLifeMath.FortuneLoss(TownRaidEnd.Sacked),
            "held, hurt, sacked: each costs more fortune");
        C(TownLifeMath.GoodsLost(TownRaidEnd.Held) < TownLifeMath.GoodsLost(TownRaidEnd.Hurt) && TownLifeMath.GoodsLost(TownRaidEnd.Sacked) <= 0.7f, "and more goods, never all");
        C(TownLifeMath.OthersLost(TownRaidEnd.Held) == 0 && TownLifeMath.OthersLost(TownRaidEnd.Hurt) == 1 && TownLifeMath.OthersLost(TownRaidEnd.Sacked) == 2, "and some of its other people");

        // 11B: a former colonist dies only in a raid that really came and was lost.
        C(TownLifeMath.FormerDeathChance(TownRaidEnd.Defended, 3f) == 0f && TownLifeMath.FormerDeathChance(TownRaidEnd.Held, 3f) == 0f, "never in a raid held");
        C(TownLifeMath.FormerDeathChance(TownRaidEnd.Sacked, 1.5f) > TownLifeMath.FormerDeathChance(TownRaidEnd.Sacked, 1.0f), "likelier the stronger the raiders");
        C(TownLifeMath.FormerDeathChance(TownRaidEnd.Sacked, 9f) <= 0.4f && TownLifeMath.FormerDeathChance(TownRaidEnd.Hurt, 9f) <= 0.15f, "a raid's chance has a ceiling");
        C(TownLifeMath.FormerDeathChance(TownRaidEnd.Hurt, 1.1f) < TownLifeMath.FormerDeathChance(TownRaidEnd.Sacked, 1.1f), "a town hurt loses fewer than one sacked");
        // The rate over a year's raids: the raids a town meets alone, held about half the time, take a former colonist rarely.
        var r2 = new Random(11);
        int deaths = 0, raids = 4000;
        for (int i = 0; i < raids; i++)
        {
            float defend = TownLifeMath.DefendPoints(1150f + 450f * (float)r2.NextDouble(), 50f);
            float raiders = TownLifeMath.RaidPoints(defend, (float)r2.NextDouble());
            var e = TownLifeMath.Alone(raiders, defend, (float)r2.NextDouble());
            if (r2.NextDouble() < TownLifeMath.FormerDeathChance(e, TownLifeMath.Ratio(raiders, defend))) deaths++;
        }
        C(deaths / (float)raids > 0.03f && deaths / (float)raids < 0.15f, $"an unanswered raid takes one of the colony's former people about one time in {raids / Math.Max(1, deaths)}");

        C(TownLifeMath.CallMeans(WorldAge.Tribal) == "runner" && TownLifeMath.CallMeans(WorldAge.Medieval) == "rider" && TownLifeMath.CallMeans(WorldAge.Industrial) == "radio"
          && TownLifeMath.CallMeans(WorldAge.Spacer) == "comms", "the town calls by its own means, every age");
        C(TownLifeMath.MessengerComes(WorldAge.Tribal) && TownLifeMath.MessengerComes(WorldAge.Medieval) && !TownLifeMath.MessengerComes(WorldAge.Industrial),
            "the old ways send someone to the gate");
        C(TownLifeMath.Answers(2, true) && !TownLifeMath.Answers(1, true) && !TownLifeMath.Answers(4, false), "two grown colonists or more, bound for the town or there");

        // ---------------- Hard times: the town asks to come back ----------------

        int handed = 10 * Day, now = handed + 40 * Day;
        C(TownLifeMath.AsksBack(true, 70, false, false, 0, handed, -1, now), "struggling long, goodwill 70, at peace: it asks");
        C(!TownLifeMath.AsksBack(false, 90, false, false, 0, handed, -1, now), "not struggling: it doesn't");
        C(!TownLifeMath.AsksBack(true, 59, false, false, 0, handed, -1, now) && TownLifeMath.AsksBack(true, 60, false, false, 0, handed, -1, now), "goodwill 60 or more");
        C(!TownLifeMath.AsksBack(true, 70, true, false, 0, handed, -1, now), "never at war");
        C(!TownLifeMath.AsksBack(true, 70, false, true, 0, handed, -1, now), "not while its yes stands already");
        C(!TownLifeMath.AsksBack(true, 70, false, false, 2, handed, -1, now) && TownLifeMath.AsksBack(true, 70, false, false, 1, handed, -1, now), "refused twice: no more asking");
        C(!TownLifeMath.AsksBack(true, 70, false, false, 0, handed, -1, handed + 10 * Day), "never in its first season");
        C(!TownLifeMath.AsksBack(true, 70, false, false, 1, handed, now - 10 * Day, now) && TownLifeMath.AsksBack(true, 70, false, false, 1, handed, now - 15 * Day, now),
            "a season between its asks");
        C(!TownLifeMath.GivesUp(1) && TownLifeMath.GivesUp(2), "refused twice, its people give it up");
        C(TownLifeMath.LastTown(1) && !TownLifeMath.LastTown(2), "the town its people's only one: the people is no more once it is given up");
        // The region's own rule for struggling decides the ask (RiseFallMath.Struggling): a month under 15, or two blows under 25.
        C(RiseFallMath.Struggling(12f, now - 31 * Day, 0, -1, now) && !RiseFallMath.Struggling(12f, now - 10 * Day, 0, -1, now) && RiseFallMath.Struggling(22f, -1, 2, -1, now),
            "long struggling is the region's own rule");
        C(!RiseFallMath.MayGiveUp(true, 1, true, false, true), "the region never gives a people's seat up: only its own people give the town up");

        // Who comes to the gate: the former colonists first, then up to three others who would join.
        var gate = TownLifeMath.Gate(new[] { false, true, false, true, false, false, false }, new[] { 90f, 0f, 10f, 0f, 60f, 40f, 30f });
        C(gate.Take(2).SequenceEqual(new[] { 1, 3 }), "the colony's own first");
        C(gate.Skip(2).SequenceEqual(new[] { 0, 4, 5 }), "then the others who would join, the best first, three at most (one under the line stays)");
        var many = TownLifeMath.Gate(Enumerable.Repeat(true, 12).ToList(), Enumerable.Repeat(0f, 12).ToList());
        C(many.Count == TownLifeMath.GateMax, "eight at most");

        // ---------------- Turning against the colony ----------------

        C(TownLifeMath.Cooling(0f, 0, 50) == 0, "no wrongs and a head who thinks well of the colony: nothing taken");
        C(TownLifeMath.WrongsPart(30f, 0) == 15 && TownLifeMath.WrongsPart(200f, 0) == 40, "the colony's wrongs, half a point each, 40 at most");
        C(TownLifeMath.WrongsPart(0f, 1) == 10 && TownLifeMath.WrongsPart(0f, 9) == 30, "a grudge on the ledger 10, 30 at most");
        C(TownLifeMath.HeadPart(10) == 0 && TownLifeMath.HeadPart(-50) == 20 && TownLifeMath.HeadPart(-100) == 30, "a cool head cools the town, 30 at most");
        C(TownLifeMath.Cooling(40f, 1, -50) == -(20 + 10 + 20), "both together");
        // With the bond's +30 while a former colonist heads it, heavy wrongs and a cool head bring the town's natural goodwill below
        // nothing, which the game's drift follows (outside 50 of it, 10 points each 50 days).
        int natural = HandOverMath.Bond(false, false) + TownLifeMath.Cooling(80f, 3, -80);
        C(natural < -50, $"a second who thinks little of the colony after its wrongs: natural goodwill {natural}, far below the bond's");
        C(TownLifeMath.CoolHead < 0, "a new head is told only when they think little of the colony");

        // ---------------- The region's hardships (11B) ----------------

        C(TownLifeMath.HardshipKind("sickness") == TownLifeMath.Plague && TownLifeMath.HardshipKind("hunger") == TownLifeMath.Famine && TownLifeMath.HardshipKind("drought") == TownLifeMath.Famine
          && TownLifeMath.HardshipKind("war") == TownLifeMath.NoHardship && TownLifeMath.HardshipKind(null) == TownLifeMath.NoHardship, "a plague or a famine, nothing else");
        C(TownLifeMath.HardshipReaches(true, -1f) && TownLifeMath.HardshipReaches(false, 20f) && !TownLifeMath.HardshipReaches(false, 21f) && !TownLifeMath.HardshipReaches(false, -1f),
            "its own people's, or within 20 tiles");
        C(TownLifeMath.HardshipDeathChance(TownLifeMath.NoHardship, true, 90) == 0f, "no hardship, no death");
        C(Math.Abs(TownLifeMath.HardshipDeathChance(TownLifeMath.Plague, false, 30) - 0.06f) < 0.0001f && Math.Abs(TownLifeMath.HardshipDeathChance(TownLifeMath.Famine, false, 30) - 0.04f) < 0.0001f,
            "6% a plague, 4% a famine");
        C(TownLifeMath.HardshipDeathChance(TownLifeMath.Plague, true, 70) > TownLifeMath.HardshipDeathChance(TownLifeMath.Plague, false, 30)
          && TownLifeMath.HardshipDeathChance(TownLifeMath.Plague, true, 90) <= TownLifeMath.MaxHardDeath, "the struggling and the old likelier, 25% at most");
        C(TownLifeMath.HardshipDeathChance(TownLifeMath.Plague, false, 30, true) < TownLifeMath.HardshipDeathChance(TownLifeMath.Plague, false, 30), "the colony's help through it halves it");
        C(TownLifeMath.WhoDies(new[] { 0.1f, 0.1f }, new[] { 0.05f, 0.01f }) == 0 && TownLifeMath.WhoDies(new[] { 0.1f, 0.1f }, new[] { 0.5f, 0.05f }) == 1
          && TownLifeMath.WhoDies(new[] { 0.1f }, new[] { 0.5f }) == -1, "one death at most, the first in order whose roll falls under");
        // Six former colonists, a plague: the chance one dies is about a third.
        var r3 = new Random(3);
        int hit = 0;
        for (int i = 0; i < 4000; i++)
        {
            var ch = Enumerable.Range(0, 6).Select(k => TownLifeMath.HardshipDeathChance(TownLifeMath.Plague, false, k == 0 ? 68 : 40)).ToList();
            if (TownLifeMath.WhoDies(ch, ch.Select(_ => (float)r3.NextDouble()).ToList()) >= 0) hit++;
        }
        C(hit / 4000f > 0.2f && hit / 4000f < 0.45f, $"six of the colony's former people through a plague: one dies about {hit / 40f:0}% of the time");

        // ---------------- The code's seams with the game ----------------

        string root = Root();
        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string raid = Code("Colonies/TownRaid.cs"), life = Code("Colonies/TownLife.cs"), patches = Code("Colonies/TownLifePatches.cs"), letters = Code("Colonies/TownLifeLetters.cs");
        C(raid.Contains("class SituationWorker_TownRaid : WorldSituationWorker") && raid.Contains("protected override bool CanHappen(Map map) => TownLife.On")
          && Regex.IsMatch(raid, @"public static bool Fits\(LedgerFormer rec\)[\s\S]*?rec\.Standing"), "the raid is a world event that can only come for a town the colony was that stands");
        C(raid.Contains("TownLayouts.DefendPoints = s.Num(\"defend\")") && Code("Colonies/TownLayout.cs").Contains("DefendPoints > 0f ? DefendPoints : SymbolResolver_Settlement.DefaultPawnsPoints"),
            "its defenders on its map are the raid's own reckoning");
        C(raid.Contains("ResetCountdown()") && raid.Contains("new LordJob_AssaultColony(s.ctx.enemy, false, true"), "no countdown to the game's own raids there; the raiders the game's own assault, kidnapping nobody");
        C(raid.Contains("f.HostileTo(rec.people) || !f.HostileTo(Faction.OfPlayer)"), "the raiders are at war with both, so the colony's fighters can fight them");
        C(File.ReadAllText(Path.Combine(root, "Defs", "WorldSituationDefs", "Rimfolk_WS_TownRaid.xml")).Contains("<hideIf><li>noMap</li></hideIf>")
          && raid.Contains("if (!RimfolkPatches.On) s.flags.Add(\"noMap\")"), "without the deeper hooks no map of the town is made, and nobody is asked to go");
        C(Regex.IsMatch(raid, @"if \(onMap\)\s*\{\s*foreach \(var p in rec\.formers\)\s*if \(p != null && p\.Dead && s\.Num\(""f"""), "on the map, a former colonist's death is one the player saw");
        C(raid.Contains("FormerDeathChance(e, ratio)") && raid.Contains("x != rec.people.leader"), "off it, one at most in a raid lost, never its head");
        C(patches.Contains("typeof(SettlementDefeatUtility), nameof(SettlementDefeatUtility.CheckDefeated)") && patches.Contains("f.HostileTo(Faction.OfPlayer) || !HandOverPeoples.IsOurs(f)) return true"),
            "the game's defeat check stands down only for a town the colony was at peace with");
        C(patches.Contains("typeof(IncidentWorker_RaidEnemy), \"GetLetterText\"") && patches.Contains("rec.formers.Contains(p)"), "a raid of theirs names a former colonist among the raiders");
        C(life.Contains("RiseFallMath.Struggling(t.prosperity, t.lowSince, RiseFall.Blows(rec.people, now), -1, now)"), "hard times by the region's own rule");
        C(life.Contains("RiseFall.MakeRuin(town, ledgerTown, false)") && life.Contains("people.temporary = true") && life.Contains("people.defeated = true"),
            "given up: the region's ruin, and with no other town the people is no more");
        C(Regex.IsMatch(life, @"if \(last\)\s*\{\s*people\.leader = null;"), "its leader steps aside first, so the game makes no stranger to lead it");
        C(life.Contains("GetNaturalGoodwillOffset") && life.Contains("-(r.cooledWrongs + r.cooledHead)"), "the colony's wrongs and a cool head in its natural goodwill");
        C(Code("World/Region/NewTowns.cs").Contains("&& !HandOverPeoples.IsOurs(f)"), "15B: Rimfolk's own new towns are never this people's");
        string wm = Code("Integrations/World/WorldMechanics.cs");
        C(Regex.IsMatch(wm, @"public static void Founded\([^)]*\)\s*\{[\s\S]{0,200}TownLife\.Founded\(people, tile\)") && wm.Contains("TownLife.Built(people, town);"),
            "15B: its growth is Faction Territories' own, told through the hooks on its expansion");
        C(Code("Colonies/TownsAfter.cs").Contains("TownLife.Hourly(now);") && Code("Colonies/TownsAfter.cs").Contains("TownLife.Daily(now);")
          && !life.Contains("Sched.") && !raid.Contains("Sched."), "no schedule slot: the region's hour and day");
        C(Code("Colonies/TownTakeBack.cs").Contains("rec.end == TownEnd.GivenUp) return;"), "a town given up by its people is never taken for one the colony beat");
        C(Code("World/Region/RaidersCamp.cs").Contains("public static bool CanOwn(Faction f)") && !Code("World/Region/RaidersCamp.cs").Contains("HandOverPeoples"),
            "the raiders' camps can pick it by their own rule, nothing kept out");
        C(Code("Dilemmas/AnswerMath.cs").Contains("(\"townLife\", ChronicleKind.World, \"ChoiceLetter_TownLife\")") && letters.Contains("this, \"townLife\""), "its questions' answers in the answers' log");
        C(Code("World/FactionMemory.cs").Contains("[\"townDefended\"] = K(DeedSide.Favor"), "the town remembers who stood on its walls");
        C(Regex.IsMatch(Code("World/Ledger/LedgerFormers.cs"), @"Scribe_Values\.Look\(ref refusals, ""refusals""\)") && !Regex.IsMatch(life + raid + letters, @"Scribe_Collections"),
            "the town's life saves values only, no new collection");

        // The world event's words and data.
        var def = XDocument.Load(Path.Combine(root, "Defs", "WorldSituationDefs", "Rimfolk_WS_TownRaid.xml")).Root!.Elements().Single();
        string defText = def.ToString();
        C(def.Element("workerClass")?.Value == "ColonyCouncil.SituationWorker_TownRaid" && def.Element("battle")?.Value == "true" && def.Element("bad")?.Value == "true",
            "a fight of its own: only with world-event battles on");
        foreach (var means in new[] { "runner", "rider", "radio", "comms" })
            C(def.Element("texts")!.Elements("li").Any(l => l.Value.StartsWith("[" + means)), "the call by its means: " + means);
        C(defText.Contains("<requires>runner</requires>") && defText.Contains("<requires>rider</requires>"), "a runner or a rider at the gate, by the old ways");
        var endings = def.Element("endings")!.Elements("li").Select(e => e.Element("key")?.Value).ToList();
        C(new[] { "defended", "fell", "held", "hurt", "sacked", "gone" }.All(endings.Contains), "every way it can end has its words");
        C(def.Element("endings")!.Elements("li").Where(e => new[] { "defended", "fell", "hurt", "sacked" }.Contains(e.Element("key")?.Value))
              .All(e => e.Element("texts")!.Elements("li").Any(l => l.Value.StartsWith("[died")) && e.Element("texts")!.Elements("li").Any(l => l.Value.Contains("{DEAD}"))),
            "11B: a former colonist killed is named in the letter");
        C(def.Descendants("li").Where(l => !l.HasElements).Select(l => Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", "")).All(b => !b.Contains("—") && !b.Contains("!")) && !Regex.IsMatch(defText, @"\bin \d+ days?\b|\{DAYS\}"),
            "no em dashes, no exclamation marks, no countdowns");

        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_TownLife.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        C(keyed.Values.All(v => !v.Contains("—") && !v.Contains("!")), "the words: no em dashes, no exclamation marks");
        C(keyed.Values.All(v => !Regex.IsMatch(v, @"\{DAYS\}|\bin \d+ (days?|hours?)\b|\bdays? (left|more)\b")), "no countdowns");
        foreach (var means in new[] { "runner", "rider", "radio", "comms" }) C(keyed.ContainsKey("CC_TownLife_Means_" + means), "word comes by the town's means: " + means);
        C(keyed.ContainsKey("CC_TownLife_HardshipText") && keyed["CC_TownLife_HardshipText"].Contains("{PAWN}") && keyed["CC_TownLife_HardshipText"].Contains("{WHAT}"),
            "11B: a death of the region's hardships named, and what took them");
        C(keyed.ContainsKey("CC_Memory_Deed_townDefended") && keyed.ContainsKey("CC_Memory_Deed_townDefended_Name"), "the deed's words");
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_TownLife.xml")).Root!.ToString();
        C(misc.Contains("ColonyCouncil.ChoiceLetter_TownLife") && misc.Contains("ColonyCouncil.GoodwillSituationWorker_TownCooled"), "its letter kind and its goodwill situation");

        // The rate: the sim's colony has no town of its own handed over, so the raid on one is not among its world events.
        string balance = File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Defs.cs"));
        C(balance.Contains("SituationWorker_TownRaid"), "the balance model leaves the raid on a town the colony was out: world events a year unchanged without one");
        var almanac = XDocument.Load(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_TownLife.xml")).Root!.Elements().First();
        C(almanac.Element("label")?.Value == "the town's life" && !almanac.ToString().Contains("—"), "the Almanac page, a page of its own");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
