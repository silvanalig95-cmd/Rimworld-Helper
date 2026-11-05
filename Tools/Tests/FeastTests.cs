using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Stage 2's diplomacy, release 2b.1, feasts, hunts, games and contests with guests (docs/design/stage2-diplomacy.md,
/// item 5; Diplomacy/FeastMath.cs): one a season at a home, who may be invited, their answer, the day, the weather, what
/// each people hopes for, each contest's scoring, the bad sides at the design's rates under the harm rule's shared cap,
/// the welcome, the steward's suggestion, the colony's own at a neighbour's feast, the facts' tags; and the mod's side:
/// the hooks in the shared code, the entry point for 2b.2's weddings, the words, the lines, the defs.
/// </summary>
public static class FeastTests
{
    private const int D = FeastMath.DayTicks;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "feasts with guests: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));

        // ---------------- One a season, and who may come ----------------

        C(FeastMath.MayPlan(false, -1, 0) && !FeastMath.MayPlan(true, -1, 0), "one feast at a time");
        C(!FeastMath.MayPlan(false, 0, 15 * D - 1) && FeastMath.MayPlan(false, 0, 15 * D), "one a season at a home, from the last one held");
        C(FeastMath.MaxGuests == 3 && FeastMath.MaxContests == 4 && FeastMath.MaxRetinue == 2, "three guests, four contests, a retinue of one or two");
        C(FeastMath.MayInvite(false, 0, 3f, true) && !FeastMath.MayInvite(true, 50, 1f, true) && !FeastMath.MayInvite(false, -1, 1f, true)
          && !FeastMath.MayInvite(false, 10, 3.1f, true) && !FeastMath.MayInvite(false, 10, 1f, false),
            "a notable at peace, regard 0 or more, within three days' travel, free to travel");

        // ---------------- Their answer ----------------

        for (int r = -100; r <= 100; r += 10)
            foreach (float days in new[] { 0.5f, 1f, 2f, 3f })
            {
                float c = FeastMath.AcceptChance(r, 0, false, days);
                C(c >= 0.1f && c <= 0.95f, $"their answer's chance stays between a tenth and nine in ten (regard {r}, {days} days)");
            }
        C(FeastMath.AcceptChance(60, 0, false, 1f) > FeastMath.AcceptChance(0, 0, false, 1f), "more the better they think of the colony");
        C(FeastMath.AcceptChance(0, 0, true, 1f) > FeastMath.AcceptChance(0, 0, false, 1f), "more when what they hope for is offered");
        C(FeastMath.AcceptChance(0, 0, false, 3f) < FeastMath.AcceptChance(0, 0, false, 1f), "less the longer the road");
        C(FeastMath.AcceptChance(0, 80, false, 1f) > FeastMath.AcceptChance(0, 40, false, 1f), "more from a friend of the colony");
        C(!FeastMath.Accepts(-1, 100, true, 0.5f, 0f), "never below regard 0");
        int yes = 0;
        var rng = new Random(7);
        for (int i = 0; i < 20000; i++)
            if (FeastMath.Accepts(0, 20, false, 1f, (float)rng.NextDouble()))
                yes++;
        C(Math.Abs(yes / 20000f - FeastMath.AcceptChance(0, 20, false, 1f)) < 0.02f, $"they say yes at the chance named ({yes / 200f:0.#}%)");
        C(FeastMath.ComeDays(2f, false) == 2.5f && FeastMath.ComeDays(0.1f, false) == 1f && FeastMath.ComeDays(2f, true) == 3f,
            "they can come once their answer has travelled back and half a day to set out (a call: a day to set out and the road)");

        // ---------------- The day and the weather ----------------

        C(FeastMath.FeastDay(10, 9f) == 10 && FeastMath.FeastDay(10, 12f) == 10 && FeastMath.FeastDay(10, 13f) == 11, "a guest due by noon comes that day, else the next");
        C(FeastMath.Fine(0f, 0f, 20f, false) && !FeastMath.Fine(0.5f, 0f, 20f, false) && !FeastMath.Fine(0f, 0.5f, 20f, false) && !FeastMath.Fine(0f, 0f, -20f, false)
          && !FeastMath.Fine(0f, 0f, 45f, false) && !FeastMath.Fine(0f, 0f, 20f, true), "fine: no rain or snow, not too cold or hot, no poisoned air");
        C(FeastMath.ArriveNow(10, 10, 9, true, false) && !FeastMath.ArriveNow(9, 10, 9, true, false) && !FeastMath.ArriveNow(10, 10, 7, true, false)
          && !FeastMath.ArriveNow(10, 10, 15, true, false) && !FeastMath.ArriveNow(10, 10, 9, false, false) && !FeastMath.ArriveNow(10, 10, 9, true, true),
            "they walk in on the day (or after), in the morning to early afternoon, when it's fine and safe");
        C(!FeastMath.GiveUp(11, 10, 20) && !FeastMath.GiveUp(12, 10, 14) && FeastMath.GiveUp(12, 10, 15) && FeastMath.GiveUp(13, 10, 0),
            "two days of waiting for a fine one, then called off");
        C(FeastMath.Leave(6, 6 * FeastMath.HourTicks) && !FeastMath.Leave(5, 9 * FeastMath.HourTicks) && !FeastMath.Leave(7, 2 * FeastMath.HourTicks),
            "they go home at first light, once they've had the night");
        C(FeastMath.ContestsFrom == 12 && FeastMath.FeastFrom == 16 && FeastMath.FeastTo == 21 && FeastMath.OutdoorBy == 18, "contests in the afternoon, the feast in the evening");

        // ---------------- Hopes, by their ways ----------------

        C(FeastMath.Hope(WorldAge.Tribal, false) == FeastHope.Hunt && FeastMath.Hope(WorldAge.Medieval, false) == FeastHope.HuntOrTourney
          && FeastMath.Hope(WorldAge.Industrial, false) == FeastHope.RaceOrMatch && FeastMath.Hope(WorldAge.Spacer, false) == FeastHope.Exhibition
          && FeastMath.Hope(WorldAge.Medieval, true) == FeastHope.MealAndMusic, "a tribe hopes for a hunt, the middle ages a hunt or a tournament, an industrial people a race or a match, spacers an exhibition, the Empire a fine meal and music");
        C(FeastMath.Answers(FeastHope.Hunt, ContestKind.Hunt) && !FeastMath.Answers(FeastHope.Hunt, ContestKind.Race)
          && FeastMath.Answers(FeastHope.RaceOrMatch, ContestKind.Shooting) && FeastMath.Answers(FeastHope.Exhibition, ContestKind.Tables)
          && !FeastMath.Answers(FeastHope.MealAndMusic, ContestKind.Tables), "which contests answer which hope");
        C(FeastMath.Offered(FeastHope.MealAndMusic, new List<ContestKind>(), true) && !FeastMath.Offered(FeastHope.MealAndMusic, new[] { ContestKind.Race }, false)
          && FeastMath.Offered(FeastHope.HuntOrTourney, new[] { ContestKind.Race, ContestKind.Wrestling }, false), "the Empire's hope offered by an instrument, the others by a contest");

        // ---------------- The contests ----------------

        C(Enum.GetNames(typeof(ContestKind)).SequenceEqual(new[] { "Wrestling", "Race", "Hunt", "Tables", "Shooting" }), "the five contests of decision 7");
        C(!FeastMath.Outdoor(ContestKind.Tables) && FeastMath.Outdoor(ContestKind.Race) && FeastMath.Outdoor(ContestKind.Hunt), "only the tables are indoors");
        foreach (ContestKind k in Enum.GetValues(typeof(ContestKind))) C(FeastMath.MaxTicks(k) > 0 && FeastMath.MaxTicks(k) <= 5 * FeastMath.HourTicks, $"{k} ends within hours");
        C(Math.Abs(FeastMath.HardBlowChance - 0.04f) < 0.0001f && Math.Abs(FeastMath.HardBlowChance - OutsiderMath.SparHardBlowChance(0, 0)) < 0.0001f,
            "a bout's real blow one time in 25, the sparring rate");
        int hard = 0;
        rng = new Random(11);
        for (int i = 0; i < 50000; i++)
            if (FeastMath.HardBlow((float)rng.NextDouble(), true))
                hard++;
        C(Math.Abs(hard / 50000f - 0.04f) < 0.004f, $"over many bouts, a real blow in about one in 25 ({hard / 500f:0.##}%)");
        C(!FeastMath.HardBlow(0f, false) && !FeastMath.HardBlow(0f, true, 0f) && FeastMath.HardBlow(0.05f, true, 2f) && !FeastMath.HardBlow(0.05f, true, 1f),
            "none for one who may not be hurt; the everyday harm scale moves it");
        C(FeastMath.MayBeHurt(false, false, 3f) && !FeastMath.MayBeHurt(true, false, 30f) && !FeastMath.MayBeHurt(false, true, 30f) && !FeastMath.MayBeHurt(false, false, 2.9f),
            "the harm rule's shared cap: never a child, never one with child, nobody hurt in the last three days");
        C(FeastMath.BoutChampion(new[] { 1, 2, 2 }, new[] { 9, 3, 5 }) == 2 && FeastMath.BoutChampion(new[] { 0, 0 }, new[] { 4, 4 }) == -1, "the wrestler with most bouts won, then most blows landed");
        C(FeastMath.RaceWinner(new[] { -1, 900, 850, 851 }) == 2 && FeastMath.RaceWinner(new[] { -1, -1 }) == -1, "the first home wins the race; nobody home, no winner");
        int stumbles = 0;
        for (int i = 0; i < 24000; i++)
            if (FeastMath.Stumbles((float)rng.NextDouble()))
                stumbles++;
        C(Math.Abs(stumbles / 24000f - 1f / 12f) < 0.01f && FeastMath.StumbleTicks(0f) == 90 && FeastMath.StumbleTicks(1f) == 150, "a runner stumbles one time in twelve, a moment lost");
        C(FeastMath.HuntWinner(new[] { 1, 2, 2 }, new[] { 5, 9, 7 }) == 2 && FeastMath.HuntWinner(new[] { 0, 0 }, new[] { -1, -1 }) == -1, "the most kills, then the first to make one");
        C(FeastMath.HuntGame(1) == 2 && FeastMath.HuntGame(4) == 5 && FeastMath.HuntGame(20) == 8, "game for the hunt: one more than the hunters, two to eight");
        C(Math.Abs(FeastMath.TableChance(8, 8) - 0.5f) < 0.001f && FeastMath.TableChance(20, 0) == 0.9f && FeastMath.TableChance(0, 20) == 0.1f
          && FeastMath.TableChance(12, 6) > 0.5f && Math.Abs(FeastMath.TableChance(12, 6) + FeastMath.TableChance(6, 12) - 1f) < 0.001f,
            "a game at the tables by the game's own skill, a tenth to nine in ten, even between equals");
        C(FeastMath.ShootingWinner(new[] { 3, 4, 4 }, new[] { 15, 8, 10 }) == 2 && FeastMath.ShootingWinner(new[] { 0, 0 }, new[] { 10, 10 }) == -1, "the most hits, then the better shot");
        C(FeastMath.Shots == 5, "five shots each");
        C(FeastMath.IsTarget("ShootingTarget", "shooting target") && FeastMath.IsTarget("CT_ArcheryTarget", "archery target") && !FeastMath.IsTarget("MartialArtsDummy", "martial arts dummy")
          && !FeastMath.IsTarget("Bed", "bed") && !FeastMath.IsTarget("TrainingDummy", "training dummy target"), "targets read by name, never a dummy");
        C(FeastMath.Champion(new[] { 1, 2, 1 }) == 1 && FeastMath.Champion(new[] { 2, 2 }) == 0 && FeastMath.Champion(new[] { 0 }) == -1, "the champion won the most, the first of them on a tie");

        // ---------------- The night ----------------

        C(Math.Abs(FeastMath.SourNightChance(0, false) - 1f / 15f) < 0.0001f && FeastMath.SourNightChance(0, true) == 0f
          && FeastMath.SourNightChance(2, false) > FeastMath.SourNightChance(0, false) && FeastMath.SourNightChance(4, false) <= 2f / 15f + 0.0001f
          && FeastMath.SourNightChance(-2, false) < FeastMath.SourNightChance(0, false), "a night gone sour one guest night in 15, a hot temper more (twice at most), never a child");
        int sour = 0;
        for (int i = 0; i < 30000; i++)
            if (FeastMath.SourNight(0, false, (float)rng.NextDouble()))
                sour++;
        C(Math.Abs(sour / 30000f - 1f / 15f) < 0.006f, $"over many guest nights, about one in 15 ({sour / 300f:0.##}%)");

        // ---------------- The welcome ----------------

        float best = FeastMath.Welcome(true, true, true, 4, true, true, false, false, false, false);
        float worst = FeastMath.Welcome(false, false, false, 0, false, false, true, true, true, true);
        C(best >= 30f && best <= 40f && worst <= -10f && worst >= -30f, $"a welcome from about -25 to +35 ({worst} to {best})");
        C(FeastMath.Welcome(false, false, false, 0, false, true, false, false, false, false) < 0f, "an empty plate is a slight");
        C(FeastMath.Welcome(true, false, false, 0, false, true, false, false, false, false) > 0f, "a plain meal and a bed is a fair welcome");
        C(FeastMath.Welcome(true, false, false, 0, false, true, false, false, false, true) < FeastMath.Welcome(true, false, false, 0, false, true, false, false, false, false),
            "the Empire expects a fine meal");
        C(FeastMath.Welcome(true, false, false, 0, true, true, false, false, false, false) - FeastMath.Welcome(true, false, false, 0, false, true, false, false, false, false) == 6f,
            "the prize is worth six");
        C(FeastMath.Welcome(true, false, false, 9, false, true, false, false, false, false) == FeastMath.Welcome(true, false, false, 3, false, true, false, false, false, false),
            "contests count to three");
        C(FeastMath.DeedWeight(10f) == 8 && FeastMath.DeedWeight(25f) == 12 && FeastMath.HeadShare(10f) == 5f && FeastMath.HeadShare(-4f) == 0f,
            "the deed's size by the welcome, the head's share half and never worse");
        C(FeastMath.WentWell(12f, false) && !FeastMath.WentWell(12f, true) && FeastMath.WentPoorly(5f, true) && FeastMath.WentPoorly(-1f, false) && !FeastMath.WentPoorly(4f, false),
            "how their guests went home, for the talk at their town");

        // ---------------- The steward and the colony's own away ----------------

        C(!FeastMath.Suggests(true, 0, 10 * D, true, true, true, 0f) && FeastMath.Suggests(true, 0, 15 * D, true, true, true, 0f)
          && !FeastMath.Suggests(false, -1, 0, true, true, true, 0f) && !FeastMath.Suggests(true, -1, 0, false, true, true, 0f)
          && !FeastMath.Suggests(true, -1, 0, true, false, true, 0f) && !FeastMath.Suggests(true, -1, 0, true, true, false, 0f)
          && !FeastMath.Suggests(true, -1, 0, true, true, true, 0.06f), "the steward suggests one once a season at most, fed and quiet, a friend near, one day in twenty");
        C(FeastMath.MealsWanted(10, 3, 6) == 19, "a meal for each of the colony's, each guest and those with them");
        C(FeastMath.AwayWinChance(0) == 0.2f && FeastMath.AwayWinChance(20) == 0.5f && FeastMath.AwayWinChance(10) > 0.2f, "at their feast, a fifth to a half by skill");

        // ---------------- Codes and tags ----------------

        C(FeastMath.Topic(12) == "feast:12" && FeastMath.ParseTopic("feast:12", out int home) && home == 12 && !FeastMath.ParseTopic("pact:trade:3", out _)
          && !FeastMath.ParseTopic(null, out _), "the invitation's topic names the home");
        C(FeastMath.Tags.Distinct().Count() == FeastMath.Tags.Length && FeastMath.Tags.All(t => t.StartsWith("feast:") || t == "them:feastguest"), "the facts' tags, each once");

        // ---------------- The mod's side ----------------

        foreach (var f in new[] { "FeastMath.cs" })
            C(!Regex.IsMatch(Code("Diplomacy/" + f), @"using (Verse|RimWorld|UnityEngine)"), $"{f} is pure");
        string feasts = Code("Diplomacy/Feasts.cs");
        C(feasts.Contains("public static GuestFeast Host(Map home, Faction people, List<Notable> guests, FeastPurpose purpose, int when, string tag")
          && feasts.Contains("public static event Action<GuestFeast, Map, FeastStage> StageChanged"), "the entry point for 2b.2's weddings: hosting guests who agreed, and the stage's news");
        C(Enum.GetNames(typeof(FeastPurpose)).SequenceEqual(new[] { "Feast", "Wedding" }), "a feast, or a wedding's guests");
        string hub = Code("Diplomacy/Diplomacy.cs");
        C(hub.Contains("Feasts.Hourly(now)") && hub.Contains("Feasts.Daily(now)"), "the hour and the day ride on diplomacy's");
        string book = Code("Diplomacy/DiplomacyBook.cs");
        C(book.Contains("Scribe_Deep.Look(ref feast, \"feast\")") && book.Contains("Scribe_Deep.Look(ref lastFeast, \"lastFeast\")") && book.Contains("Scribe_Collections.Look(ref tales, \"tales\"")
          && book.Contains("Feasts.HomeLost(h)") && book.Contains("h.feast != null"), "the home's part keeps the feast, the last one, the tales; a home lost ends its feast");
        C(Code("World/NeighbourLetters.cs").Contains("FeastLetters.Read(l, n, speaker ?? l.writer, ref tone)") && Code("World/NeighbourLetters.cs").Contains("visit = feast ?? PlanVisit(l, n, now)"),
            "the invitation in the existing letters: their answer and its tone");
        C(Code("PlayOut/PlayGuests.cs").Contains("case Feast:") && feasts.Contains("guests = PlayGuests.Feast"), "the feast is a gathering with the guests at the table");
        C(Code("Diplomacy/FeastBouts.cs").Contains("public static OutsiderBout FeastBout") && Code("Diplomacy/FeastContests.cs").Contains("FeastMath.HardBlow(Rand.Value, Feasts.MayBeHurt(c) && Feasts.MayBeHurt(x)"),
            "wrestling by the outsiders' own bouts, the real blow at the design's rate under the shared cap");
        C(feasts.Contains("if (!sour.Add(p.id) || !RimfolkPatches.On) return;") && feasts.Contains("FeastMath.SourNight(Outsiders.Temper(x), false, Rand.Value, HarmScale)"),
            "a night gone sour only with the deeper hooks (a brawl isn't an attack), at the design's rate");
        C(Regex.IsMatch(Code("Diplomacy/FeastContests.cs"), @"case ContestKind\.Race:[\s\S]*?Pregnant") && Code("Diplomacy/FeastContests.cs").Contains("!p.DevelopmentalStage.Juvenile() && !p.WorkTagIsDisabled(WorkTags.Violent)"),
            "children run races only; nobody with child runs or wrestles");
        string settings = Code("CouncilSettings.cs");
        C(settings.Contains("public bool diplomacyFeasts = true;") && settings.Contains("Scribe_Values.Look(ref diplomacyFeasts, \"diplomacyFeasts\", true);")
          && Code("UI/SettingsCatalog.cs").Contains("Toggle(\"diplomacyFeasts\""), "the setting, in the section Diplomacy");
        C(Regex.IsMatch(Code("World/FactionMemory.cs"), "\\[\"feasted\"\\] = K\\(DeedSide\\.Favor, 8"), "their people remember a feast as the colony's good turn");
        C(Code("Text/Facts/FactsLedger.cs").Contains("FeastFacts(s, f);"), "the facts ask the feast's");
        C(Code("Calendar/CalendarPanel.cs").Contains("FeastPanels.CalendarRow(") && Code("Diplomacy/DiplomacyModel.cs").Contains("FeastPanels.Rows(f, home)")
          && Code("Diplomacy/DiplomacyModel.cs").Contains("FeastPanels.HoldAct(f)") && Code("Diplomacy/DiplomacyModel.cs").Contains("Feasts.HereAs(p)"),
            "the calendar's row, the Peoples view's rows and act, Here and away");
        string panels = Code("Diplomacy/FeastPanels.cs");
        C(panels.Contains("CardKind.Event") && panels.Contains("Inbox.Register(\"feast-coming\", InboxSection.Coming") && panels.Contains("Inbox.Register(\"feast-under-way\", InboxSection.UnderWay"),
            "an Event card for each feast, a feast day in Coming, its steps in Under way");
        C(Code("Missions/Workers_MissionRemembered.cs").Contains("FeastAway.Attended(m, here);"), "who of the colony's won something at their feast, told when home");
        C(!feasts.Contains("Messages.Message(") && !Code("Diplomacy/FeastContests.cs").Contains("Messages.Message(") && !Code("Diplomacy/FeastPanels.cs").Contains("Messages.Message("),
            "everything told through Tell");

        // The words.
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Feasts.xml"));
        foreach (ContestKind k in Enum.GetValues(typeof(ContestKind)))
            foreach (var pre in new[] { "CC_Feast_Contest_", "CC_Feast_ContestThe_", "CC_Feast_Dlg_Contest_", "CC_Feast_Dlg_ContestTip_", "CC_Feast_Msg_Begins_", "CC_Feast_Result_", "CC_Feast_Result_None_" })
                C(keyed.Contains("<" + pre + k + ">"), $"{pre}{k} has its words");
        foreach (var age in new[] { "Tribal", "Medieval", "Industrial", "Spacer" }) C(keyed.Contains("<CC_Feast_Name_" + age + ">the "), $"the feast's name in the {age} age, with \"the\"");
        foreach (FeastHope h in Enum.GetValues(typeof(FeastHope))) C(h == FeastHope.None || keyed.Contains("<CC_Feast_Hope_" + h + ">"), $"the hope {h} has its words");
        C(keyed.Contains("<CC_Memory_Deed_feasted>") && keyed.Contains("<CC_Memory_Deed_feasted_Name>"), "the deed's words");
        C(!keyed.Contains("—") && !Regex.IsMatch(keyed, @">[^<]*!\s*<"), "no em dashes or exclamation marks in the words");
        C(Regex.Matches(keyed, @"<CC_Feast_\w+_2>").Count >= 8, "lines told often have fellows");
        C(keyed.Contains("one time in 25") && keyed.Contains("one guest night in 15"), "the risks named before the choice (the setting, the plan)");

        // The lines: every moment the code says has lines; talk pieces about feasts.
        var lines = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_Feasts.xml"));
        var says = lines.Root!.Elements("ColonyCouncil.FlavorExtensionDef").First(e => e.Element("pool")?.Value == "Rimfolk_Pool_OutsiderSays").Descendants("li").Select(l => l.Value).ToList();
        string code = string.Concat(Directory.GetFiles(Path.Combine(src, "Diplomacy"), "Feast*.cs").Select(File.ReadAllText));
        var moments = Regex.Matches(code, "\"(feast[a-z]+)\"").Select(m => m.Groups[1].Value).Where(m => m != "feast" && m != "feasted").Distinct().ToList();
        C(moments.Count >= 12, $"the day's bubbles ({moments.Count} moments)");
        foreach (var m in moments) C(says.Any(l => l.StartsWith("[" + m + ",") || l.StartsWith("[" + m + "]")), $"the moment {m} has lines");
        foreach (var l in says)
        {
            string body = Regex.Replace(l, @"^\[[^\]]*\]\s*", "");
            C(body.Length <= 90 && body.Count(ch => ch == '!') <= 1 && !body.Contains('—'), $"'{body}' is short and plain");
        }
        var writes = lines.Root!.Elements("ColonyCouncil.FlavorExtensionDef").First(e => e.Element("pool")?.Value == "Rimfolk_Pool_NeighbourWrites").Descendants("li").Count();
        C(writes >= 20, $"their words in letters ({writes})");
        var talks = XDocument.Load(Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_Feasts.xml")).Root!.Elements("ColonyCouncil.OutsiderTalkDef").ToList();
        C(talks.Count >= 20 && talks.Count <= 40 && talks.All(t => t.Element("topic")?.Value == "feast"), $"talk pieces about feasts ({talks.Count})");
        foreach (var tag in new[] { FeastMath.HereTag, FeastMath.SoonTag, FeastMath.GuestTag, FeastMath.TheyWonTag, FeastMath.WeWonTag })
            C(talks.Any(t => (t.Element("when")?.Value ?? "").Contains(tag)), $"something is said on {tag}");

        // The defs.
        string defs = File.ReadAllText(Path.Combine(root, "Defs", "Misc", "Rimfolk_Feasts.xml"));
        foreach (var d in new[] { "Rimfolk_FeastSuggest", "Rimfolk_FeastRun", "Rimfolk_FeastShoot", "Rimfolk_FeastPrize", "Rimfolk_FeastGuest", "Rimfolk_FeastIndoors" })
            C(defs.Contains("<defName>" + d + "</defName>") && feasts.Contains("public static " + (d.EndsWith("Suggest") ? "LetterDef " : d.EndsWith("Guest") || d.EndsWith("Indoors") ? "DutyDef " : "JobDef ") + d + ";"),
                $"{d} is defined and in FeastDefOf");
        string est = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        foreach (var f in new[] { "Diplomacy/FeastBook.cs:retinue", "Diplomacy/FeastBook.cs:guests", "Diplomacy/FeastBook.cs:contests", "Diplomacy/FeastBook.cs:results", "Diplomacy/DiplomacyBook.cs:tales" })
            C(est.Contains("\"" + f + "\""), $"the save model knows {f}");
        return n;
    }
}
