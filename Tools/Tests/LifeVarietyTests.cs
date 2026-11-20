using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Colony life's variety, part 2 (Source/ColonyCouncil/LifeVariety/, the content update of 20 November 2026): coming of age's
/// four more paths (A8), a toy for the little one (A11), three ambitions of the colony's people (A15), life goals for the other
/// seats (A16), marks from the colony's own life (A22), deeds and names of the council's life (A23), group habits from the
/// colony's pastimes (A24), the council's seasons in talk (A25), the giving day (A26), the elders' day (A28), twelve events of
/// the colony's everyday life and six personal stories. The pure rules (LifeVarietyMath), and the defs, words and seams.
/// </summary>
public static class LifeVarietyTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "colony life 2: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));

        // ================= A11: a toy =================

        C(ToyMath.ChildAge(3f) && ToyMath.ChildAge(12f) && !ToyMath.ChildAge(2.9f) && !ToyMath.ChildAge(12.5f), "a toy for a child of three to twelve");
        C(ToyMath.MayAsk(true, 30f, 0) && ToyMath.MayAsk(false, 65f, 0) && ToyMath.MayAsk(false, 30f, ToyMath.FondAtLeast), "a parent, an elder or a grown-up fond of the child");
        C(!ToyMath.MayAsk(false, 30f, ToyMath.FondAtLeast - 1), "not someone who barely knows the child");
        C(!ToyMath.MayAsk(true, 30f, -5) && !ToyMath.MayAsk(false, 70f, -1), "never someone at odds with the child, parent or not: nothing follows family lines");
        C(ToyMath.Weight(80) > ToyMath.Weight(30) && ToyMath.Weight(500) <= 1.3f + 0.001f, "fonder, a little likelier");
        C(ToyMath.GivenLately(0, 60000 * (ToyMath.AgainAfterDays - 1), 60000) && !ToyMath.GivenLately(0, 60000 * ToyMath.AgainAfterDays, 60000) && !ToyMath.GivenLately(-1, 5, 60000),
            "a toy given lately waits forty days");

        // ================= A15: three ambitions =================

        C(ColonyAmbitionMath.Outside(false, false) && !ColonyAmbitionMath.Outside(true, false) && !ColonyAmbitionMath.Outside(false, true), "outside: no group and no friend");
        C(ColonyAmbitionMath.HasFriend(ColonyAmbitionMath.FriendOpinion, 0f) && ColonyAmbitionMath.HasFriend(-50, ColonyAmbitionMath.FriendTie) && !ColonyAmbitionMath.HasFriend(39, 29f),
            "a friend by both opinions or by the social web");
        C(ColonyAmbitionMath.NobodyOutside(5, 0) == 1f && ColonyAmbitionMath.NobodyOutside(5, 1) < 1f && ColonyAmbitionMath.NobodyOutside(5, 2) < ColonyAmbitionMath.NobodyOutside(5, 1),
            "nobody left out in stages, met only with nobody outside");
        C(ColonyAmbitionMath.NobodyOutside(0, 0) == 0f, "nobody at home is nothing");
        C(ColonyAmbitionMath.StreakBroken(3, 2) && !ColonyAmbitionMath.StreakBroken(2, 2) && !ColonyAmbitionMath.StreakBroken(1, 2), "a promise broken starts the season again");
        C(ColonyAmbitionMath.KeptProgress(15f, 2) >= 0.999f, "fifteen days and two promises kept: met");
        C(ColonyAmbitionMath.KeptProgress(15f, 0) == 0f && ColonyAmbitionMath.KeptProgress(0f, 5) == 0f, "both the days and the promises kept are needed");
        C(Math.Abs(ColonyAmbitionMath.KeptProgress(7.5f, 1) - 0.5f) < 0.001f, "halfway is halfway on both");
        C(ColonyAmbitionMath.Worn(ColonyAmbitionMath.WornAt) && !ColonyAmbitionMath.Worn(ColonyAmbitionMath.WornAt - 0.1f), "worn down at the heart's 55");

        // ================= A16: the seats' sparks =================

        var seatsSeen = new HashSet<string>();
        foreach (var s in SeatSparkMath.All)
        {
            C(!string.IsNullOrEmpty(SeatSparkMath.RecordOf(s)), "a record behind " + s);
            C(SeatSparkMath.SeatOf(s)?.StartsWith("Council_Office_") ?? false, "a seat for " + s);
            seatsSeen.Add(SeatSparkMath.SeatOf(s));
            C(SeatSparkMath.Sparks(s, SeatSparkMath.Needed(s)) && !SeatSparkMath.Sparks(s, SeatSparkMath.Needed(s) - 0.5f), "what sparks it: " + s);
        }
        C(seatsSeen.Count == 7, "seven seats, each with its own work");
        C(SeatSparkMath.Needed(SeatSparkMath.Harvest) >= 10f, "a real day's harvest, not a plant or two");

        // ================= A23: names =================

        foreach (var seat in CouncilNameMath.Seats) C(CouncilNameMath.DaysRecord(seat)?.StartsWith("Council_DaysAs") ?? false, "a record of days for " + seat);
        C(CouncilNameMath.DaysRecord("Council_Office_Physician") == null && CouncilNameMath.DaysRecord("Council_Office_Marshal") == null, "the physician's and the marshal's keep their own");
        C(CouncilNameMath.NameDays == 120 && CouncilNameMath.WordKeptPromises == 2, "two years in a seat, two promises kept");

        // ================= A24: pastimes =================

        C(PastimeMath.FishingJob("Fish") && PastimeMath.FishingJob("PW_Fish") && !PastimeMath.FishingJob("FishAnimal"), "fishing: Odyssey's and PokéWorld's, not an animal's");
        C(PastimeMath.BathJob("anything", "Hydrotherapy") && PastimeMath.BathJob("RG_Bathe", null) && !PastimeMath.BathJob("Wait", "Social"), "the hot water: Dubs Bad Hygiene and ReGrowth");
        C(PastimeMath.GameJob("Play_Poker") && PastimeMath.GameJob("Play_Chess") && PastimeMath.GameJob("Play_DartsBoard") && !PastimeMath.GameJob("Play_Billiards"),
            "cards, chess and darts");

        // ================= A25: the vote =================

        C(CouncilSeasonMath.LeanTag(true, false, false) == "lean:self" && CouncilSeasonMath.LeanTag(false, true, false) == "lean:holder"
          && CouncilSeasonMath.LeanTag(false, false, false) == "lean:other" && CouncilSeasonMath.LeanTag(true, true, true) == "undecided", "the lean said as the vote leans");
        C(CouncilSeasonMath.IsUndecided(3f, 2.8f) && !CouncilSeasonMath.IsUndecided(3f, 1f), "two within a hair: undecided");

        // ================= A26: the giving day =================

        for (int people = 2; people <= 12; people++)
            for (int seed = 0; seed < 25; seed++)
            {
                var to = GivingMath.Draw(people, seed);
                bool ok = to.Length == people && to.Distinct().Count() == people && Enumerable.Range(0, people).All(i => to[i] != i && to[i] >= 0 && to[i] < people);
                if (!ok) C(false, $"names drawn: everyone gives one and gets one, nobody themselves ({people} people, seed {seed})");
            }
        C(true, "names drawn: everyone gives one and gets one, nobody themselves (2 to 12 people)");
        C(GivingMath.Draw(1, 3).Length == 0 && GivingMath.Draw(0, 3).Length == 0, "fewer than two: no gifts");
        C(GivingMath.Order(true, false, false, false)[0] == GiftKind.Toy && GivingMath.Order(true, false, false, true).IndexOf(GiftKind.Drink) < 0, "a child a toy first, and never a drink");
        C(GivingMath.Order(false, true, false, true)[0] == GiftKind.Warm, "someone bare-headed in winter something warm first");
        C(GivingMath.Order(false, false, true, true)[0] == GiftKind.Book, "a reader a book first");
        C(GivingMath.Order(false, false, false, false).IndexOf(GiftKind.Drink) < 0, "nobody who may not drink is given a drink");
        C(GivingMath.Order(false, false, false, true).Count >= 4, "something to fall back on");
        var fond = new List<int[]> { new[] { 50, 10 }, new[] { 60, 5 }, new[] { -1, -1 } };
        var giver = GivingMath.ForChildren(fond);
        C(giver[0] == 0 && giver[1] == 1 && giver[2] == -1, "each child the fondest grown-up who has given least, and none from anyone at odds");
        C(GivingMath.SameDay(5, 5) && !GivingMath.SameDay(5, 6) && !GivingMath.SameDay(-1, -1), "the giving day and midwinter on one day: one set of presents");
        C(GivingMath.TakeFrom < GivingMath.GiveFrom && GivingMath.GiveFrom <= GivingMath.EveningEnds, "taken in the afternoon, handed round after supper");

        // ================= A28: the elders' day =================

        C(EldersMath.IsElder(60f, false) && EldersMath.IsElder(30f, true) && !EldersMath.IsElder(59f, false), "an elder: sixty, or the elder's seat");
        C(EldersMath.MayAsk(2, false) && EldersMath.MayAsk(1, true) && !EldersMath.MayAsk(1, false) && !EldersMath.MayAsk(0, true), "two elders, or the seat held");
        C(EldersMath.Young(29f) && !EldersMath.Young(30f), "someone young asks");
        C(EldersMath.ElderFor(0, 2) == 0 && EldersMath.ElderFor(1, 2) == 1 && EldersMath.ElderFor(2, 2) == 0 && EldersMath.ElderFor(0, 0) == -1, "the children's pieces go round the elders");

        // ================= Events and stories =================

        C(ColonyEventMath.AtOdds(-20, -15) && !ColonyEventMath.AtOdds(-20, 0), "a quarrel needs both at odds");
        C(ColonyStoryMath.CustomStillGrowing(1, false) && ColonyStoryMath.CustomStillGrowing(2, false) && !ColonyStoryMath.CustomStillGrowing(3, false) && !ColonyStoryMath.CustomStillGrowing(1, true),
            "a custom of their own while it grows toward a tradition");
        C(ColonyStoryMath.AimFresh(3, 3, 10) && !ColonyStoryMath.AimFresh(2, 3, 10) && !ColonyStoryMath.AimFresh(3, 3, 30), "the year's aim while the year is young");

        // ================= The defs =================

        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => (f, XDocument.Load(f))).ToList();
        var byName = new Dictionary<string, XElement>();
        foreach (var e in defs.SelectMany(d => d.Item2.Root.Elements()))
        {
            var dn = e.Element("defName")?.Value;
            if (dn != null) byName[dn] = e;
        }
        foreach (var d in new[]
                 {
                     "Rimfolk_Dilemma_Path_Fields", "Rimfolk_Dilemma_Path_Kitchen", "Rimfolk_Dilemma_Path_Animals", "Rimfolk_Dilemma_Path_Building", "Rimfolk_Wish_Toy",
                     "Rimfolk_Thought_ToyGiven", "Rimfolk_Thought_ToyHanded", "Rimfolk_Amb_NobodyOutside", "Rimfolk_Amb_KeptPromises", "Rimfolk_Amb_LighterSeason",
                     "Rimfolk_Habit_Fishing", "Rimfolk_Habit_Bath", "Rimfolk_Habit_Cup", "Rimfolk_Habit_Cards", "Rimfolk_Topic_Vote", "Rimfolk_Talk_Vote",
                     "Rimfolk_Dilemma_GivingDay", "Rimfolk_Dilemma_EldersDay", "Rimfolk_Deed_SeatPromiseKept", "Rimfolk_Deed_SeatPromiseBroken", "Rimfolk_Deed_SpokeUp",
                     "Rimfolk_Epithet_OldKeys", "Rimfolk_Epithet_OldFurrow", "Rimfolk_Epithet_Drover", "Rimfolk_Epithet_Host", "Rimfolk_Epithet_OldVoice",
                     "Rimfolk_Epithet_WordKept", "Council_SeatPromisesKept"
                 })
            C(byName.ContainsKey(d), "def " + d);
        var paths = new[] { "Rimfolk_Dilemma_Path_Fields", "Rimfolk_Dilemma_Path_Kitchen", "Rimfolk_Dilemma_Path_Animals", "Rimfolk_Dilemma_Path_Building" };
        foreach (var p in paths)
        {
            var e = byName[p];
            C((string)e.Attribute("ParentName") == "Council_PathBase", "a coming-of-age path like the others: " + p);
            C(e.Descendants("li").Any(l => (string)l.Attribute("Class") == "ColonyCouncil.DilemmaEffect_Mentorship")
              && e.Descendants("li").Any(l => (string)l.Attribute("Class") == "ColonyCouncil.DilemmaEffect_Lesson"), "a real mentorship and a first lesson on the map: " + p);
        }
        C(byName["Rimfolk_Dilemma_Path_Fields"].Element("seat")?.Value == "Council_Office_Reeve" && byName["Rimfolk_Dilemma_Path_Animals"].Element("seat")?.Value == "Council_Office_Herdmaster",
            "the reeve teaches the fields, the herdmaster the animals");
        var seatGoals = byName.Values.Where(e => e.Element("defName")?.Value.StartsWith("Rimfolk_Asp_Seat_") ?? false).ToList();
        C(seatGoals.Count == 7, "seven seats' life goals");
        var offices = new HashSet<string>(byName.Values.Where(e => e.Name.LocalName.EndsWith("OfficeDef")).Select(e => e.Element("defName").Value));
        foreach (var g in seatGoals)
        {
            string dn = g.Element("defName").Value;
            C(offices.Contains(g.Element("office")?.Value ?? ""), "its seat exists: " + dn);
            C(g.Element("sparks")?.Elements("li").Any(l => l.Value == "thought:Council_LostElection") ?? false, "losing an election sparks it: " + dn);
            C(g.Element("sparks")?.Elements("li").Any(l => SeatSparkMath.All.Contains(l.Value)) ?? false, "the seat's own work sparks it: " + dn);
        }
        foreach (var f in new[] { "GoalReached", "IdeaBuilt", "CustomTradition", "SeatYearKept", "StoryEndedWell" })
            C(byName.Values.Any(e => e.Name.LocalName.EndsWith("PersonaMarkDef") && e.Descendants("family").Any(x => x.Value == f)), "a mark of the family " + f);
        foreach (var h in new[] { "Rimfolk_Habit_Fishing", "Rimfolk_Habit_Bath", "Rimfolk_Habit_Cup", "Rimfolk_Habit_Cards" })
        {
            C(PastimeMath.Wheres.Contains(byName[h].Element("where")?.Value), "a pastime's own where: " + h);
            C(byName.Values.Any(e => e.Name.LocalName.EndsWith("GroupShareDef") && e.Element("habit")?.Value == h), "a friendship group may be named for it: " + h);
            C(byName.Values.Any(e => e.Name.LocalName.EndsWith("GroupKindDef") && (e.Element("habits")?.Elements("li").Any(l => l.Value == h) ?? false)), "a kind of group keeps it: " + h);
        }
        var councilEvents = byName.Values.Where(e => e.Name.LocalName.EndsWith("SocialEventDef") && e.Element("council") != null).ToList();
        foreach (var k in CouncilSeasonMath.Kinds) C(councilEvents.Any(e => e.Element("council").Value == k), "a social event of the council's seasons: " + k);
        foreach (var name in new[] { "Rimfolk_Dilemma_GivingDay", "Rimfolk_Dilemma_EldersDay" })
        {
            var e = byName[name];
            C(e.Element("traditionOption")?.Value == "0" && !string.IsNullOrEmpty(e.Element("traditionLabel")?.Value), "a custom that becomes a tradition: " + name);
            C(e.Element("texts").Elements("li").Count(l => !l.Value.StartsWith("[")) >= 3, "several ways to ask it: " + name);
        }
        C(byName["Rimfolk_Dilemma_GivingDay"].Element("seasons").Elements("li").Any(l => l.Value == "Winter"), "the giving day in winter");
        C(byName["Rimfolk_Dilemma_EldersDay"].Element("seasons").Elements("li").Any(l => l.Value == "Fall"), "the elders' day in autumn");
        var events = defs.Where(d => d.f.EndsWith("Rimfolk_Events_ColonyLife.xml")).SelectMany(d => d.Item2.Root.Elements("IncidentDef")).ToList();
        var plays = new HashSet<string>
        {
            "ColonyCouncil.DilemmaEffect_Words", "ColonyCouncil.DilemmaEffect_GroupEvening", "ColonyCouncil.DilemmaEffect_Visit", "ColonyCouncil.DilemmaEffect_Toast",
            "ColonyCouncil.DilemmaEffect_WorkParty", "ColonyCouncil.DilemmaEffect_Bonfire", "ColonyCouncil.DilemmaEffect_Lesson", "ColonyCouncil.DilemmaEffect_WatchAtPlace",
            "ColonyCouncil.DilemmaEffect_Vigil", "ColonyCouncil.DilemmaEffect_PromiseWish", "ColonyCouncil.DilemmaEffect_Forage"
        };
        C(events.Count == 12, $"twelve events of the colony's everyday life ({events.Count})");
        foreach (var e in events)
        {
            string dn = e.Element("defName").Value;
            var ext = e.Descendants("li").First(l => (string)l.Attribute("Class") == "ColonyCouncil.HappeningExtension");
            C(ext.Element("options")?.Elements("li").Count() >= 2, "a real choice: " + dn);
            C(ext.Descendants("li").Any(l => plays.Contains((string)l.Attribute("Class") ?? "")),
                "an answer that plays out on the map: " + dn);
            C(!ext.Descendants().Any(x => x.Value.Contains("{OFFICE}")), "no seat the event's card couldn't name: " + dn);
            C(!ext.Descendants("li").Any(l => ((string)l.Attribute("Class") ?? "").Contains("Fight") || ((string)l.Attribute("Class") ?? "").Contains("HarmRisk")), "no harm: " + dn);
        }
        var stories = defs.Where(d => d.f.EndsWith("Rimfolk_Arcs_ColonyLife.xml")).SelectMany(d => d.Item2.Root.Elements()).ToList();
        C(stories.Count == 6, "six personal stories");

        // No em dash or exclamation mark in the new words.
        foreach (var file in new[]
                 {
                     "Defs/DilemmaDefs/Rimfolk_Dilemmas_ComingOfAge_More.xml", "Defs/WishDefs/Rimfolk_Wishes_LifeVariety.xml", "Defs/AmbitionDefs/Rimfolk_Ambitions_LifeVariety.xml",
                     "Defs/AspirationDefs/Rimfolk_Aspirations_Seats.xml", "Defs/PersonaDefs/Rimfolk_PersonaMarks_ColonyLife.xml", "Defs/DeedDefs/Rimfolk_Deeds_Council.xml",
                     "Defs/EpithetDefs/Rimfolk_Epithets_Council.xml", "Defs/GroupDefs/Rimfolk_GroupHabits_Pastimes.xml", "Defs/SocialEventDefs/Rimfolk_SocialEvents_CouncilSeasons.xml",
                     "Defs/TopicDefs/Rimfolk_Topics_Vote.xml", "Defs/DilemmaDefs/Rimfolk_Dilemmas_Customs_LifeVariety.xml", "Defs/IncidentDefs/Rimfolk_Events_ColonyLife.xml",
                     "Defs/ArcDefs/Rimfolk_Arcs_ColonyLife.xml", "Languages/English/Keyed/Rimfolk_LifeVariety.xml"
                 })
        {
            string text = File.ReadAllText(Path.Combine(root, file));
            C(!text.Contains('—') && !Regex.IsMatch(text, @"!\s*<"), "plain words, no em dashes or exclamation marks: " + file);
        }
        var pools = defs.SelectMany(d => d.Item2.Root.Elements().Where(e => e.Name.LocalName.EndsWith("FlavorPoolDef"))).ToDictionary(e => e.Element("defName").Value, e => e);
        foreach (var f in Directory.GetFiles(Path.Combine(src, "LifeVariety"), "*.cs"))
            foreach (Match m in Regex.Matches(File.ReadAllText(f), "\"((?:Rimfolk|Council)_Pool_[A-Za-z_]+)\""))
                C(pools.ContainsKey(m.Groups[1].Value), "pool " + m.Groups[1].Value);

        // ================= Seams =================

        C(Read("Lives/Legacy.cs").Contains("Interplay.Holds(p, def.seat.defName)"), "a path's seat holder teaches it");
        C(Read("Social/Groups/GroupHabits.cs").Contains("case PastimeMath.Fish:") && Read("Social/Groups/GroupHabits.cs").Contains("case PastimeMath.Cup:"), "the pastimes kept as they come");
        C(Read("Social/SocialEvents.cs").Contains("CouncilSeasons.Fits(def.council, a, b)") && Read("Social/SocialEvents.cs").Contains("CouncilSeasons.Fill(def.council, ctx)"), "the council's seasons in the social events");
        C(Read("Aspirations/Aspirations.cs").Contains("LifeMarks.GoalReached(p, a.Goal)") && Read("Ambitions/Ambitions.cs").Contains("LifeMarks.IdeaBuilt(a.proposer, a.def.label)")
          && Read("Arcs/ArcTracker.cs").Contains("LifeMarks.StoryEndedWell(p, arc.def.label)") && Read("Dilemmas/Dilemmas.cs").Contains("LifeMarks.CustomBecameTradition(def, label)")
          && Read("Dilemmas/Dilemmas.cs").Contains("comp.lifeVariety?.NoteFirstAsker(def, ctx.a)"), "the five marks' hooks");
        C(Read("Offices/SeatRecords.cs").Contains("LifeDeeds.Judged(race.holder, race.office, race.verdict == PromiseVerdict.Kept)")
          && Read("Social/BanishDilemmas.cs").Contains("LifeDeeds.SpokeUp(objector, a)") && Read("Epithets/NameRecords.cs").Contains("LifeDeeds.SeatDays(c.offices)"), "the deeds' and names' hooks");
        C(Read("Gods/Among/Midwinter.cs").Contains("if (GivingDay.KeptToday(map)) return;"), "midwinter hands no presents of its own on the giving day");
        C(Read("Epithets/NameRecords.cs").Contains("GivingDay.AfterFestiveFeast(job.Ritual, job.lord?.Map)"), "the festive feast brings the giving day's gifts");
        C(Read("PlayOut/PlayOutTracker.cs").Contains("LifeVariety.Tick(now);") && Read("CouncilGameComponent.cs").Contains("LifeVariety.Hourly(this)")
          && Read("CouncilDynamics.cs").Contains("LifeVariety.Daily(this)") && Read("CouncilGameComponent.cs").Contains("ExposeLifeVariety();"), "its passes and its book");
        C(Read("ColonyLife/Readings.cs").Contains("EldersDay.TellNext(r, here)") && Read("ColonyLife/Readings.cs").Contains("EldersDay.Over(r, p, took)"), "the elders' tales said in turn at the supper");
        C(Read("Offices/Offices.cs").Contains("typeof(AspirationWorker_Office).IsAssignableFrom(goal.workerClass)"), "a seat's goal of the new kind still counts in the vote");
        return n;
    }
}
