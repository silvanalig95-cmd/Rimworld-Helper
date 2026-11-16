using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The six quests at home (quests, round 1; the player's go of 16 November 2026: docs/quests-proposals.md Q1, Q2, Q5, Q7,
/// Q9 and Q10): the pure rules (Missions/HomeQuestsMath.cs: the reward by the asking people's age and the colony's wealth,
/// the days, the odds, what counts as done), their share of the missions' one draw, and the defs and code: every age's
/// words and the Empire's, plain words, an ending for every way each can end, everything on the colony's own map, the
/// game's own prisoner, insult, wounds, fight and taming, the outlaw within the shared harm budget, and the dev actions.
/// </summary>
public static class HomeQuestsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "quests at home: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        string Cls(XElement e) => ((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", "");
        float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        bool Plain(string t)
        {
            string body = Regex.Replace(t ?? "", @"^\[[^\]]*\]\s*", "");
            return !body.Contains('—') && !body.Contains('!');
        }

        // ================= The reward =================

        int T = HomeQuestsMath.Tribal, M = HomeQuestsMath.Medieval, I = HomeQuestsMath.Industrial, S = HomeQuestsMath.Spacer;
        C(HomeQuestsMath.AgeFactor(T, false) < HomeQuestsMath.AgeFactor(M, false) && HomeQuestsMath.AgeFactor(M, false) < HomeQuestsMath.AgeFactor(I, false)
          && HomeQuestsMath.AgeFactor(I, false) < HomeQuestsMath.AgeFactor(S, false) && HomeQuestsMath.AgeFactor(M, true) == HomeQuestsMath.AgeFactor(S, false),
            "a tribe has least to give, a spacer people most, the Empire as a spacer people");
        C(HomeQuestsMath.InGoods(T, false) && !HomeQuestsMath.InGoods(M, false) && !HomeQuestsMath.InGoods(S, false) && !HomeQuestsMath.InGoods(T, true),
            "a tribe pays in goods of its own; every other people, and the Empire, in silver");
        foreach (HomeQuest q in Enum.GetValues(typeof(HomeQuest)))
        {
            HomeQuestsMath.Base(q, out float min, out float max);
            int lo = HomeQuestsMath.Reward(q, M, false, 1f, 0f), hi = HomeQuestsMath.Reward(q, M, false, 1f, 1f);
            C(min >= 100f && max > min && max <= 340f && lo == HomeQuestsMath.Tens(min) && hi == HomeQuestsMath.Tens(max) && lo % 10 == 0 && hi % 10 == 0,
                $"{q}: its own range, {lo} to {hi} silver from a medieval people, rounded to tens");
            C(HomeQuestsMath.Reward(q, T, false, 1f, 0.5f) < HomeQuestsMath.Reward(q, S, false, 1f, 0.5f), $"{q}: worth more from a spacer people than a tribe");
            C(HomeQuestsMath.Reward(q, M, false, 5f, 1f) == HomeQuestsMath.Reward(q, M, false, 2f, 1f) && HomeQuestsMath.Reward(q, M, false, 0.1f, 0f) == HomeQuestsMath.Reward(q, M, false, 0.6f, 0f),
                $"{q}: the colony's wealth counts between 0.6 and 2");
            C(HomeQuestsMath.Goodwill(q) >= 8 && HomeQuestsMath.Goodwill(q) <= 12 && HomeQuestsMath.Penalty(q) >= 4 && HomeQuestsMath.Penalty(q) <= 8, $"{q}: goodwill for it, a cost for failing it");
            int d0 = HomeQuestsMath.Days(q, 0f), d1 = HomeQuestsMath.Days(q, 0.9999f);
            C(d0 >= 2 && d1 <= 9 && d0 <= d1, $"{q}: whole days, {d0} to {d1}");
        }
        C(HomeQuestsMath.Reward(HomeQuest.Dispute, T, false, 0.6f, 0f) >= 30, "never less than thirty");
        C(HomeQuestsMath.OutlawPay(200, true) == 300 && HomeQuestsMath.OutlawPay(200, false) == 200 && HomeQuestsMath.CaptureFactor == 1.5f, "an outlaw alive is worth half as much again");
        C(HomeQuestsMath.Days(HomeQuest.Prisoner, 0f) == 4 && HomeQuestsMath.Days(HomeQuest.Prisoner, 0.9999f) == 7 && HomeQuestsMath.Days(HomeQuest.Dispute, 0.5f) == 2,
            "the prisoner held four to seven days, the quarrel settled within two");
        C(HomeQuestsMath.ComeMin > 0f && HomeQuestsMath.ComeMax <= 1f, "their people come within a day");

        // ================= The odds =================

        C(HomeQuestsMath.Insults(Ruling.First) == 3 && HomeQuestsMath.Insults(Ruling.Second) == 3 && HomeQuestsMath.Insults(Ruling.Split) == 0 && HomeQuestsMath.Insults(Ruling.None) == 1,
            "the loser's words, three tries of the game's own insult (and its own odds of a fight); none after a split");
        C(HomeQuestsMath.RulingGoodwill(Ruling.Split, 8, 4) == 8 && HomeQuestsMath.RulingGoodwill(Ruling.First, 8, 4) == 6 && HomeQuestsMath.RulingGoodwill(Ruling.None, 8, 4) == -4,
            "a split pleases their town most, a side taken a little less, no judgement costs");
        C(HomeQuestsMath.Tag(Ruling.First) == "first" && HomeQuestsMath.Tag(Ruling.Second) == "second" && HomeQuestsMath.Tag(Ruling.Split) == "split" && HomeQuestsMath.Tag(Ruling.None) == "none",
            "each ruling's tag for its words");
        C(HomeQuestsMath.OutlawPower(0f) == 45f && HomeQuestsMath.OutlawPower(10000f) == 180f && Math.Abs(HomeQuestsMath.OutlawPower(300f) - 105f) < 0.01f,
            "the outlaw scaled to the colony's threat points, between an ordinary fighter and a hardened one");
        C(HomeQuestsMath.FightChance >= 0.5f && HomeQuestsMath.FightChance < 1f, "found, the outlaw usually fights, sometimes runs");
        C(HomeQuestsMath.FoundRange >= 6f && HomeQuestsMath.FoundRange <= 15f && HomeQuestsMath.HideMin >= 25f, "hidden well out, found in sight within a few steps");
        C(HomeQuestsMath.WoundsMin >= 1 && HomeQuestsMath.WoundsMax <= 3 && HomeQuestsMath.WoundShareMax < 0.5f && HomeQuestsMath.MinBleedDays >= 1f,
            "the messenger's wounds: two or three, never half a limb, and time to be treated");
        C(HomeQuestsMath.Covers(0, 0) && HomeQuestsMath.Covers(2, 2) && !HomeQuestsMath.Covers(3, 0) && !HomeQuestsMath.Covers(2, 3), "one look covers the ground close around it");
        C(HomeQuestsMath.Wet(0.5f, 0f) && HomeQuestsMath.Wet(0f, 0.5f) && !HomeQuestsMath.Wet(0f, 0f) && !HomeQuestsMath.Wet(0.05f, 0.05f), "rain or snow counts, a drizzle's edge doesn't");
        C(!HomeQuestsMath.Spoiled(HomeQuestsMath.WetHoursToSpoil - 1) && HomeQuestsMath.Spoiled(HomeQuestsMath.WetHoursToSpoil) && HomeQuestsMath.WetHoursToSpoil == 6,
            "six hours of rain or snow ruin the keepsake, and the terms say six");
        // A search of the whole area, a look at a time at the nearest spot not yet covered: some hours of work, never endless.
        int r = (int)Math.Ceiling(HomeQuestsMath.AreaRadius);
        var area = new List<(int x, int z)>();
        for (int x = -r; x <= r; x++)
            for (int z = -r; z <= r; z++)
                if (x * x + z * z <= HomeQuestsMath.AreaRadius * HomeQuestsMath.AreaRadius) area.Add((x, z));
        var looks = new List<(int x, int z)>();
        var at = (x: 0, z: 0);
        while (true)
        {
            var next = area.Where(c => !looks.Any(l => HomeQuestsMath.Covers(c.x - l.x, c.z - l.z))).OrderBy(c => (c.x - at.x) * (c.x - at.x) + (c.z - at.z) * (c.z - at.z)).ToList();
            if (next.Count == 0 || looks.Count > 60) break;
            at = next[0];
            looks.Add(at);
        }
        C(area.Count >= 120 && area.Count <= 200 && looks.Count >= 6 && looks.Count <= 40, $"the area ({area.Count} cells) searched in full in {looks.Count} looks of about an hour");
        C(HomeQuestsMath.AreaMargin >= 1f && HomeQuestsMath.AreaMargin < HomeQuestsMath.AreaRadius && HomeQuestsMath.LookTicks >= 1500 && HomeQuestsMath.LookTicks <= 2500,
            "the keepsake lies inside the area, a look takes about an hour");

        // ================= What counts as done =================

        C(HomeQuestsMath.PrisonerOutcome(false, false, false, true, true) == PrisonerEnd.Held && HomeQuestsMath.PrisonerOutcome(false, false, false, true, false) == PrisonerEnd.Holding
          && HomeQuestsMath.PrisonerOutcome(false, false, false, false, true) == PrisonerEnd.Escaped && HomeQuestsMath.PrisonerOutcome(true, true, true, false, true) == PrisonerEnd.Died
          && HomeQuestsMath.PrisonerOutcome(false, true, true, true, true) == PrisonerEnd.Kept && HomeQuestsMath.PrisonerOutcome(false, false, true, true, true) == PrisonerEnd.Released,
            "the prisoner: held the days on the colony's map is done; dead, taken in, let go or gone is not, in that order");
        C(HomeQuestsMath.MessengerOutcome(false, false, true) == MessengerEnd.Delivered && HomeQuestsMath.MessengerOutcome(true, false, true) == MessengerEnd.Died
          && HomeQuestsMath.MessengerOutcome(false, true, false) == MessengerEnd.Lost && HomeQuestsMath.MessengerOutcome(false, false, false) == MessengerEnd.Waiting,
            "the messenger: the letter collected is done; the messenger dead or the letter gone is not");
        C(HomeQuestsMath.OutlawOutcome(true, false, false, false) == OutlawEnd.Killed && HomeQuestsMath.OutlawOutcome(false, false, true, true) == OutlawEnd.Captured
          && HomeQuestsMath.OutlawOutcome(false, true, true, true) == OutlawEnd.Kept && HomeQuestsMath.OutlawOutcome(false, false, false, false) == OutlawEnd.Fled
          && HomeQuestsMath.OutlawOutcome(false, false, false, true) == OutlawEnd.Hiding, "the outlaw: captured or killed is done; taken in or off the land is not");
        C(HomeQuestsMath.TameOutcome(false, true, true, false) == TameEnd.Tamed && HomeQuestsMath.TameOutcome(false, true, true, true) == TameEnd.Bonded
          && HomeQuestsMath.TameOutcome(true, true, true, false) == TameEnd.Killed && HomeQuestsMath.TameOutcome(false, false, false, false) == TameEnd.Gone
          && HomeQuestsMath.TameOutcome(false, false, true, false) == TameEnd.Wild, "the animal: tamed (bonded or not) is done; killed or gone is not");
        C(HomeQuestsMath.Kinds.Length == 6 && HomeQuestsMath.Kinds.Distinct().Count() == 6 && HomeQuestsMath.KindOf(HomeQuest.TameFor) == "tamefor", "six kinds of their own");

        // ================= Their share of the missions' one draw =================

        string file = Path.Combine(root, "Defs", "MissionDefs", "Rimfolk_Missions_Home.xml");
        var defs = XDocument.Load(file).Root!.Elements("IncidentDef").ToList();
        var quests = defs.Where(d => F((string)d.Element("baseChance")) > 0f).ToList();
        C(quests.Count == 6, $"six quests drawn by the storyteller ({quests.Count})");
        float total = 0f;
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs", "MissionDefs"), "*.xml"))
            foreach (var d in XDocument.Load(f).Root!.Elements("IncidentDef"))
                if (((string)d.Element("workerClass") ?? "").Contains("HappeningWorker_Mission")) total += F((string)d.Element("baseChance") ?? "0");
        C(quests.All(d => F((string)d.Element("baseChance")) == HomeQuestsMath.BaseChance && int.Parse((string)d.Element("minRefireDays")) == HomeQuestsMath.RestDays),
            "each a small share of the draw, and not again for a season");
        C(Math.Abs(NeighboursFourMath.PoolFactor(total) * total - NeighboursFourMath.MissionPoolWeight) < 0.001f && Math.Abs(quests.Count * HomeQuestsMath.BaseChance - 1.8f) < 0.001f,
            $"they take 1.8 of the one draw, and the missions still weigh {NeighboursFourMath.MissionPoolWeight} in all ({total:0.0} scaled): no more missions than before");
        var draw = Code("Missions/Workers_MissionNeighbours4.cs");
        C(draw.Contains("StoryDirection.Factor(StoryWaysMath.Missions)"), "the way to play leans them as it leans every mission");

        // ================= The defs =================

        var code = Code("Missions/Workers_MissionHome.cs") + Code("Missions/Workers_MissionHome2.cs");
        var outcomesOf = new Dictionary<string, (string[] done, string[] fail)>
        {
            ["HappeningWorker_MissionPrisoner"] = (new[] { "held" }, new[] { "escaped", "died", "kept", "released", "harmed", "noprison", "ended" }),
            ["HappeningWorker_MissionDispute"] = (new[] { "first", "second", "split" }, new[] { "none", "harmed", "ended" }),
            ["HappeningWorker_MissionMessenger"] = (new[] { "delivered" }, new[] { "died", "lost", "ended" }),
            ["HappeningWorker_MissionOutlaw"] = (new[] { "captured", "killed" }, new[] { "fled", "unfound", "kept", "ended" }),
            ["HappeningWorker_MissionKeepsake"] = (new[] { "returned" }, new[] { "spoiled", "theyfound", "toolate", "lost", "ended" }),
            ["HappeningWorker_MissionTameFor"] = (new[] { "handed", "bonded" }, new[] { "killed", "gone", "untamed", "ended" }),
        };
        var keyed = Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements()).GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value);
        var ages = new[] { "tribal", "medieval", "industrial", "spacer" };
        foreach (var d in quests)
        {
            string name = (string)d.Element("defName");
            string worker = ((string)d.Element("workerClass") ?? "").Replace("ColonyCouncil.", "");
            C(outcomesOf.ContainsKey(worker) && code.Contains("class " + worker + " : "), $"{name}: its worker ({worker})");
            var ext = d.Descendants("li").First(x => Cls(x) == "HappeningExtension");
            var who = d.Descendants("li").FirstOrDefault(x => Cls(x) == "MissionPeople");
            C(who != null && PeoplesMath.IsCalling((string)who.Element("prefers")) && who.Element("only") == null,
                $"{name}: any people at peace may ask, one of a fitting calling likelier ({(string)who?.Element("prefers")})");
            var text = d.Descendants("li").First(x => Cls(x) == "MissionText");
            var texts = Lis(ext, "texts");
            foreach (var age in ages)
            {
                bool lowtech = age == "tribal" || age == "medieval";
                C(texts.Any(t => Regex.IsMatch(t, @"^\[[^\]]*their:" + age + @"\b") || Regex.IsMatch(t, @"^\[[^\]]*their:" + (lowtech ? "lowtech" : "hightech") + @"\b")
                                 || !Regex.IsMatch(t, @"^\[[^\]]*their:")), $"{name}: asked in a {age} people's own form");
            }
            C(texts.Any(t => Regex.IsMatch(t, @"^\[[^\]]*their:empire")) || texts.All(t => !t.Contains("their:")), $"{name}: the Empire asks in its own way");
            C(texts.Where(t => Regex.IsMatch(t, @"^\[[^\]]*their:(spacer|hightech)")).All(t => t.Contains("!their:empire")), $"{name}: a spacer people's words never the Empire's");
            var options = ext.Element("options")!.Elements("li").ToList();
            C(options.Count == 2 && (string)ext.Element("defaultOption") == "1", $"{name}: taken on or declined, declined unanswered");
            var terms = Lis(text, "terms");
            C(terms.Count > 0 && terms.All(t => t.Contains("{ITEM}") && Regex.IsMatch(t, @"\{FACTION\} will (think less|hold it|be disappointed)")),
                $"{name}: its terms say what the colony gets and what failing costs");
            C(terms.All(t => t.Contains("{DAYS}")) || worker == "HappeningWorker_MissionDispute", $"{name}: the days stated in its terms");
            var all = texts.Concat(terms).Concat(Lis(text, "accepted")).Concat(Lis(text, "ready")).Concat(Lis(text, "done")).Concat(Lis(text, "fail")).Concat(Lis(text, "cancelled"))
                .Concat(Lis(ext, "titles")).Concat(Lis(text, "doneTitles")).Concat(Lis(text, "failTitles")).Concat(options.Select(o => (string)o.Element("outcome") ?? "")).ToList();
            C(all.All(Plain), $"{name}: plain prose, no em dashes or exclamation marks");
            C(Lis(text, "accepted").Concat(Lis(text, "done")).Concat(Lis(text, "fail")).Concat(Lis(text, "cancelled")).Concat(texts).All(t => !t.Contains("{DAYS}")),
                $"{name}: no countdown outside the terms (the Quests tab shows the time)");
            var ends = Lis(text, "done").Concat(Lis(text, "fail")).ToList();
            var (done, fail) = outcomesOf.GetValueOrDefault(worker, (Array.Empty<string>(), Array.Empty<string>()));
            foreach (var o in done.Concat(fail))
            {
                C(ends.Any(t => Regex.IsMatch(t, @"^\[([^\]]*,)?" + o + @"(,[^\]]*)?\]")), $"{name}: words for how it ended ({o})");
                C(code.Contains("\"" + o + "\""), $"{name}: the code ends it as {o}");
            }
            foreach (var o in done) C(Lis(text, "done").Any(t => t.Contains("{ITEM}") || o == "bonded"), $"{name}: a done ending says what was paid");
            C(Lis(text, "cancelled").Count > 0 && Lis(text, "accepted").Count > 0, $"{name}: its taking on and its calling off");
        }
        var card = defs.Single(d => (string)d.Element("defName") == "Rimfolk_Mission_DisputeJudgement");
        var cardExt = card.Descendants("li").First(x => Cls(x) == "HappeningExtension");
        var cardOptions = cardExt.Element("options")!.Elements("li").ToList();
        C(F((string)card.Element("baseChance")) == 0f && cardOptions.Count == 4 && (string)cardExt.Element("defaultOption") == "3",
            "the judgement's card: never drawn; for one, the other, split, or none, and none when nobody answers");
        C(Lis(cardExt, "texts").Count >= 5 && Lis(cardExt, "texts").All(Plain) && cardOptions.All(o => string.IsNullOrEmpty((string)o.Element("outcome"))),
            "the card tells each side's claim plainly; the answer plays out at the gate, not in a line");
        C(Code("Missions/Workers_MissionHome.cs").Contains("public const string CardDef = \"Rimfolk_Mission_DisputeJudgement\";"), "the quarrel asks with its card");

        // ================= What happens on the map =================

        var q1 = Code("Missions/Workers_MissionHome.cs");
        var q2 = Code("Missions/Workers_MissionHome2.cs");
        var shared = Code("Missions/HomeQuests.cs");
        C(!Regex.IsMatch(q1 + q2 + shared, @"CaravansAt|MissionJourney|FormCaravan|CaravanMaker"), "nothing sends the colony's people away: every quest plays out on its own map");
        C(q1.Contains("x.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner)") && q1.Contains("PrisonBed(map, x)?.GetRoom()") && !q1.Contains("StartPrisonBreak"),
            "the prisoner is walked into the prison and held as the game holds a prisoner, breaking out at the game's own odds");
        C(q1.Contains("PrisonerInteractionModeDefOf.MaintainOnly") && q1.Contains("HomeQuests.FetchPrisoner(m, map, m.x, m.reward)"), "kept, not recruited, and collected by their people");
        C(q1.Contains("TryInteractWith(won, InteractionDefOf.Insult)") && q1.Contains("Together.Can(lost, won)") && q1.Contains("MentalStateDefOf.SocialFighting"),
            "the loser's anger is the game's own insult, face to face, and a fight only by the game's own odds, shown");
        C(q1.Contains("Together.When(\"homequest:dispute|\" + m.id, judge, m.x,") && q1.Contains("HomeQuests.Pay(m, map,"), "the judge says it to their faces, and their town pays");
        C(shared.Contains("HediffMaker.MakeHediff(") && shared.Contains("HomeQuestsMath.MinBleedDays") && shared.Contains("\"Gunshot\"") && shared.Contains("\"Bite\""),
            "the messenger's wounds are the game's own, by what attacked them, never past saving");
        C(q1.Contains("x.guest?.SetGuestStatus(Faction.OfPlayer)") && q1.Contains("RestUtility.FindPatientBedFor(x)") && q1.Contains("Couriers.CollectThings(map, new List<Thing> { letter }"),
            "the messenger is the colony's guest in a bed, and their people collect the letter");
        C(q2.Contains("Harm.Roll(HarmKinds.Outlaw, HomeQuestsMath.FightChance)") && q2.Contains("Harm.ScaleFor(HarmKinds.Outlaw)") && q2.Contains("new LordJob_DefendPoint(m.cell"),
            "the outlaw: drawn and fighting within the shared harm budget, the game's own fight at the hiding place");
        C(q2.Contains("HomeQuestsMath.OutlawPower(StorytellerUtility.DefaultThreatPointsNow(map))") && shared.Contains("PawnGroupKindDefOf.Combat"),
            "the outlaw one of their people's own fighters, scaled to the colony");
        C(HarmKinds.All.Contains(HarmKinds.Outlaw) && HarmKinds.Outlaw.system == "quests" && Code("Harm/Harm.cs").Contains("case \"quests\": return s.worldEvents && s.worldEventBattles;"),
            "the outlaw's fight is a kind of the shared harm budget, off with the world's battles");
        C(shared.Contains("class JobDriver_SearchKeepsake") && shared.Contains("class FloatMenuOptionProvider_SearchKeepsake") && shared.Contains("GenDraw.DrawFieldEdges(cells"),
            "the keepsake: an area drawn on the map, a search of its own ordered by right-click");
        C(q2.Contains("HomeQuestsMath.Wet(w.RainRate, w.SnowRate)") && q2.Contains("m.cell.Roofed(map)"), "only rain or snow falling on it in the open ruins it");
        C(q2.Contains("TameUtility.CanTame(a)") && q2.Contains("BiomeAnimals.Of(map.Biome)") && q2.Contains("Couriers.Fetch(map, x, m.faction,"),
            "the animal: one of the land's own (every mod's), tamed the game's way, fetched by their handler");
        C(!Regex.IsMatch(q1 + q2, @"\bTakeDamage\(") && !Regex.IsMatch(q1 + q2, @"Harm\.Injure\("), "no hurt of their own beyond the messenger's wounds and the game's own fights");

        // ================= Words, things and the dev tools =================

        foreach (Match k in Regex.Matches(q1 + q2 + shared, "\"(CC_HomeQ_[A-Za-z0-9_]*[A-Za-z0-9])\""))
            C(keyed.ContainsKey(k.Groups[1].Value), $"{k.Groups[1].Value} is in the words");
        foreach (var dir in new[] { "E", "NE", "N", "NW", "W", "SW", "S", "SE" }) C(keyed.ContainsKey("CC_HomeQ_Dir_" + dir), $"which way: {dir}");
        C(keyed.Where(kv => kv.Key.StartsWith("CC_HomeQ_")).All(kv => Plain(kv.Value)), "the messages in plain words");
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_Missions_Home_Misc.xml")).Root!;
        foreach (var t in misc.Elements("ThingDef").Where(t => t.Element("defName") != null))
        {
            string tex = (string)t.Element("graphicData")?.Element("texPath");
            C(tex != null && File.Exists(Path.Combine(root, "Textures", tex + ".png")), $"{(string)t.Element("defName")}: its sprite (Tools/make_quest_items.py)");
        }
        C(misc.Elements("JobDef").Any(j => (string)j.Element("defName") == "Rimfolk_SearchKeepsake") && misc.Elements("ThoughtDef").Any(j => (string)j.Element("defName") == "Rimfolk_HomeQ_Quarrel"),
            "the search's job and the quarrel's feeling");
        var pools = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_HomeQuests.xml")).Root!.Descendants("li").Select(l => Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", "")).ToList();
        C(pools.Count >= 20 && pools.All(l => Regex.Matches(l, @"[.?]").Count == 1 && (l.EndsWith(".") || l.EndsWith("?")) && Plain(l)), "every line over a head one plain sentence");
        foreach (Match p in Regex.Matches(q1 + q2, "\"(Rimfolk_Pool_HomeQ_[A-Za-z]+)\""))
            C(XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_HomeQuests.xml")).ToString().Contains(">" + p.Groups[1].Value + "<"), $"{p.Groups[1].Value} has its lines");
        var debug = Code("Missions/HomeQuestsDebug.cs");
        C(debug.Contains("Quests at home: offer one now...") && debug.Contains("Quests at home: end the open one as...") && debug.Contains("Quests at home: accept the open one"),
            "dev mode: offer each now, accept it, end it with each outcome");
        foreach (var w in outcomesOf.Keys) C(debug.Contains(w + ".DebugOutcomes"), $"dev mode: {w}'s outcomes");
        var missions = Code("Missions/Missions.cs");
        C(missions.Contains("Scribe_Values.Look(ref cell, \"cell\", IntVec3.Invalid);") && missions.Contains("Scribe_References.Look(ref thing, \"thing\");")
          && missions.Contains("Scribe_References.Look(ref other, \"other\");") && missions.Contains("HomeQuests.AddTags(m, tags);"),
            "the mission keeps a quest at home's places, its thing and the other people, and their tags (a small change to the shared file)");
        return n;
    }
}
