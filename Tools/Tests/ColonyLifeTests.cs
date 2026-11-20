using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Colony life's integrations, part 2 (Source/ColonyCouncil/ColonyLife/, the content update of 20 November 2026): a place for
/// the group (A10), the wished-for thing tried out (A12), a wish talked up finds backers (A13), an ambition reached seen on
/// the map and kept on the calendar (A14), Vanilla Aspirations Expanded's aims (A17), the year's first supper (A27) and the
/// year read aloud (A29). The pure rules (ColonyLifeMath), and the defs, words and seams they rest on.
/// </summary>
public static class ColonyLifeTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "colony life: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));

        // ================= A12: tried out =================

        C(TryOutMath.Act(true, true, true, true) == TryAct.Room, "a room is walked into, whatever is in it");
        C(TryOutMath.Act(false, true, true, false) == TryAct.Lie, "a bed is lain on");
        C(TryOutMath.Act(false, false, true, true) == TryAct.Sit, "a seat is sat in");
        C(TryOutMath.Act(false, false, false, true) == TryAct.Warm, "a fire or heater is warmed at");
        C(TryOutMath.Act(false, false, false, false) == TryAct.Look, "anything else is looked over");
        foreach (var k in new[] { "thing", "bed", "room", "ownroom" }) C(TryOutMath.TriedOut(k), "a " + k + " is tried out");
        foreach (var k in new[] { "stock", "pet", "assignment", null }) C(!TryOutMath.TriedOut(k), "a " + (k ?? "nothing") + " is not");
        C(TryOutMath.StillWaits(0, TryOutMath.WithinTicks - 1) && !TryOutMath.StillWaits(0, TryOutMath.WithinTicks), "it waits for their free time within the day, then the thanks are said where they are");
        foreach (TryAct a in Enum.GetValues(typeof(TryAct))) C(TryOutMath.StayTicks(a) >= 300 && TryOutMath.StayTicks(a) <= 900, "a short stay with it: " + a);

        // ================= A13: talked up =================

        C(Math.Abs(TalkBackMath.JoinChance(0f) - 0.5f) < 0.001f, "half the time for someone neither close nor at odds");
        C(TalkBackMath.JoinChance(60f) > TalkBackMath.JoinChance(0f) && TalkBackMath.JoinChance(-60f) < TalkBackMath.JoinChance(0f), "a friend joins more often, someone at odds less");
        C(TalkBackMath.JoinChance(500f) <= 0.9f && TalkBackMath.JoinChance(-500f) >= 0.1f - 0.001f, "never certain, never quite never");
        C(Math.Abs(TalkBackMath.Bond(30f) - 1.5f) < 0.001f, "the wish roll's own bond");
        C(TalkBackMath.MayGrow(5, 4) && !TalkBackMath.MayGrow(5, 5) && TalkBackMath.MayGrow(0, -1), "a petition grows by talk once a day at most");

        // ================= A10: a place for the group =================

        C(GroupPlaceMath.KindOf("fire") == GroupPlaceKind.Fire && GroupPlaceMath.KindOf("read") == GroupPlaceKind.Books && GroupPlaceMath.KindOf("rec") == GroupPlaceKind.Games
          && GroupPlaceMath.KindOf("music") == GroupPlaceKind.Music && GroupPlaceMath.KindOf("prayer") == GroupPlaceKind.Shrine, "the five habits with a place of their own");
        foreach (var w in new[] { "table", "work", "night", "outdoors", "any", "sickbed", null }) C(GroupPlaceMath.KindOf(w) == GroupPlaceKind.None, "no place asked for: " + (w ?? "nothing"));
        C(GroupPlaceMath.FirstWanting(new[] { GroupPlaceKind.None, GroupPlaceKind.None, GroupPlaceKind.Books }) == 2, "the first of its habits that wants a place (the scholars: talk, games night... reading)");
        C(GroupPlaceMath.FirstWanting(new[] { GroupPlaceKind.None, GroupPlaceKind.Games, GroupPlaceKind.Music }) == 1, "in the group's own order");
        C(GroupPlaceMath.FirstWanting(new[] { GroupPlaceKind.None, GroupPlaceKind.None }) == -1 && GroupPlaceMath.FirstWanting(null) == -1, "none wanted: nothing asked");
        C(GroupPlaceMath.MayAsk(GroupMath.CloseAt, GroupPlaceMath.MinAtHome) && !GroupPlaceMath.MayAsk(GroupMath.CloseAt - 1f, 5) && !GroupPlaceMath.MayAsk(80f, 2),
            "a close group with its anchor and two more at home");
        C(GroupPlaceMath.SeatsWanted(GroupPlaceKind.Fire) == 2 && GroupPlaceMath.SeatsWanted(GroupPlaceKind.Books) == 1 && GroupPlaceMath.SeatsWanted(GroupPlaceKind.Games) == 0,
            "a fire pit with seats round it, a bookcase with a chair");
        C(GroupPlaceMath.CalledAtPlace(GroupPlaceKind.Music, true) && !GroupPlaceMath.CalledAtPlace(GroupPlaceKind.Music, false) && !GroupPlaceMath.CalledAtPlace(GroupPlaceKind.None, true),
            "called to its place while the place stands");

        // ================= A14: the ambition's day =================

        C(AmbitionDayMath.Weight(1f, 30f) > AmbitionDayMath.Weight(0f, 30f) && AmbitionDayMath.Weight(0.5f, 30f) > AmbitionDayMath.Weight(0.5f, 5f), "more for one built from little, and for a longer one");
        C(AmbitionDayMath.Weight(1f, 400f) < CalendarMath.FoundingWeight && AmbitionDayMath.Weight(0f, 0f) >= CalendarMath.NewCropWeight && AmbitionDayMath.Weight(1f, 40f) <= CalendarMath.HarvestWeight + 2f,
            "between a new crop and the first harvest, never as much as the founding");

        // ================= A17: the mod's aims =================

        C(!ModAimsMath.Toasted("VAspirE_KillSomeone") && !ModAimsMath.Toasted("VAspirE_Overdose") && !ModAimsMath.Toasted("VAspirE_ConsumeHumanMeat"), "the aims the colony wouldn't toast");
        C(ModAimsMath.Toasted("VAspirE_HeldConcert") && ModAimsMath.Toasted("VAspirE_BecomeParent") && !ModAimsMath.Toasted(null), "the rest it would");
        C(ModAimsMath.Bare("VAspirE_GrowOld") == "GrowOld" && ModAimsMath.Bare("GrowOld") == "GrowOld", "the mod's prefix");
        var labels = new[] { "become", "become a parent", "hold a concert" };
        C(ModAimsMath.Match("Barr has fulfilled the aspiration: Become a parent.", labels) == 1, "the letter's words name the aim, the longest label first");
        C(ModAimsMath.Match("Barr held a concert", labels) == -1 && ModAimsMath.Match("", labels) == -1, "an aim not named is not guessed");
        C(Math.Abs(ModAimsMath.Purpose(0.5f)) < 0.001f && Math.Abs(ModAimsMath.Purpose(1f) - 10f) < 0.001f && Math.Abs(ModAimsMath.Purpose(-3f) + 10f) < 0.001f,
            "the fulfilment need counts -10 to +10 toward purpose");
        C(Math.Abs(GoalMath.ModAimsFactor(true) - 2f / 3f) < 0.001f && GoalMath.ModAimsFactor(false) == 1f, "Rimfolk's own life goals a third less often beside the mod's aims");
        var aims = Path.Combine(root, "docs", "player-mods", "defs.tsv.gz");
        if (File.Exists(aims))
        {
            using var gz = new System.IO.Compression.GZipStream(File.OpenRead(aims), System.IO.Compression.CompressionMode.Decompress);
            using var reader = new StreamReader(gz);
            var names = new HashSet<string>();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var cols = line.Split('\t');
                if (cols.Length >= 4 && cols[0] == "AspirationDef" && cols[1].StartsWith(ModAimsMath.Prefix)) names.Add(ModAimsMath.Bare(cols[1]));
            }
            foreach (var bad in ModAimsMath.NotToasted) C(names.Count == 0 || names.Contains(bad), "a real aim of the mod's kept from the toast: " + bad);
        }

        // ================= A27: the year's first supper =================

        C(FirstSupperMath.InFirstDays(0) && FirstSupperMath.InFirstDays(FirstSupperMath.LastDayOfYear) && !FirstSupperMath.InFirstDays(FirstSupperMath.LastDayOfYear + 1), "the year's first days");
        C(FirstSupperMath.Backs(20) && !FirstSupperMath.Backs(19), "who backs an aim said at the table");
        C(FirstSupperMath.Outcome(true, true, false) == AimOutcome.Reached && FirstSupperMath.Outcome(false, true, true) == AimOutcome.Died
          && FirstSupperMath.Outcome(false, false, true) == AimOutcome.StillAt && FirstSupperMath.Outcome(false, false, false) == AimOutcome.LetGo, "how an aim said turned out");
        C(GoalMath.SupperSpark == "supper", "a goal chosen at the supper is sparked by it");

        // ================= A29: the year read aloud =================

        var read = YearReadMath.Pick(new[] { "a", null, "b", "c", "d", "e", "f" }, new[] { "x", "y" });
        C(read.Count == YearReadMath.MaxLines && read[0] == "a" && read[4] == "e", "five lines at most, in the letter's order");
        read = YearReadMath.Pick(new[] { "a" }, new[] { null, "x", "y", "z" });
        C(read.Count == YearReadMath.MinLines && read[1] == "x" && read[2] == "y", "made up to three with the colony as it stands");
        C(YearReadMath.Pick(null, null).Count == 0, "nothing to read, nothing read");
        C(YearReadMath.Due(3, 2) && !YearReadMath.Due(3, 3), "each year read once");
        C(ReadingMath.GapTicks >= 300 && ReadingMath.GivenUp(ReadingMath.MissedLooks) && !ReadingMath.GivenUp(ReadingMath.MissedLooks - 1), "a pause between lines; a writer gone from the table let go after a while");

        // ================= Defs =================

        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => (f, XDocument.Load(f))).ToList();
        var byName = new Dictionary<string, XElement>();
        foreach (var e in defs.SelectMany(d => d.Item2.Root.Elements()))
        {
            var dn = e.Element("defName")?.Value;
            if (dn != null) byName[dn] = e;
        }
        foreach (var d in new[]
                 {
                     "Rimfolk_TryOut", "Rimfolk_Wish_GroupPlace", "Rimfolk_Granted_GroupPlace", "Rimfolk_Thought_FirstSupper", "Rimfolk_Thought_NoFirstSupper",
                     "Council_Dilemma_FirstSupper", "Rimfolk_Mem_YearRead", "Rimfolk_TL_Mem_YearRead_Open", "Rimfolk_TL_Mem_YearRead_Reply", "Rimfolk_TL_Mem_YearRead_Close",
                     "Council_Cal_Letter_Ambition", "Council_Cal_Talk_Ambition", "Council_Cal_Pass_Ambition", "Council_Journal_Cal_Ambition", "Rimfolk_Journal_ModAim",
                     "Rimfolk_Journal_SaidAim", "Rimfolk_Journal_YearRead", "Rimfolk_Pool_AmbitionShow"
                 })
            C(byName.ContainsKey(d), "def " + d);
        var offices = new HashSet<string>(byName.Values.Where(e => e.Name.LocalName.EndsWith("OfficeDef")).Select(e => e.Element("defName").Value));
        var ambitions = byName.Values.Where(e => e.Name.LocalName.EndsWith("AmbitionDef")).ToList();
        C(ambitions.Count >= 60, "the ambitions read");
        foreach (var a in ambitions)
        {
            string dn = a.Element("defName").Value;
            C(a.Element("achievedEffects")?.Elements().Any() ?? false, "every ambition shows something on the map when reached: " + dn);
            C(!(a.Element("dayLabel")?.Value).NullOrEmptyString() && a.Element("dayLabel").Value.StartsWith("the "), "its day on the calendar has a name: " + dn);
            var office = a.Element("sceneOffice")?.Value;
            C(office == null || offices.Contains(office), "its seat exists: " + dn);
            // The one leading is B in the scene's own words; a scene that names B has a seat or falls back to the one who proposed it (AmbitionScenes.Run).
            foreach (var label in a.Element("achievedEffects")?.Descendants("label") ?? Enumerable.Empty<XElement>())
                C(!label.Value.Contains("{X_") && !label.Value.Contains("{PAWN_"), "the scene's words name only A or B: " + dn);
        }
        var supper = byName["Council_Dilemma_FirstSupper"];
        C(supper.Descendants("lastDayOfYear").Any(e => e.Value == FirstSupperMath.LastDayOfYear.ToString()), "the supper's custom comes in the year's first days");
        C(supper.Element("traditionOption")?.Value == "0" && supper.Descendants("li").Any(e => (string)e.Attribute("Class") == "ColonyCouncil.DilemmaEffect_FirstSupper" && e.Element("chooseNew")?.Value == "true"),
            "everyone says it, and those without a goal may choose one there");
        var keeper = byName["Council_DlmLv_RecordKeeper"];
        C(keeper.Descendants("li").Any(e => (string)e.Attribute("Class") == "ColonyCouncil.DilemmaEffect_RecordKeeper"), "the record keeper's answer makes them the year's writer");
        var wish = byName["Rimfolk_Wish_GroupPlace"];
        foreach (var k in new[] { "fire", "books", "games", "music", "shrine" })
            C(wish.Element("request").Elements().SelectMany(t => t.Elements("li")).Count(l => l.Value.StartsWith("[place:" + k + "]")) >= 3, "the group's words for its place: " + k);
        var pools = defs.SelectMany(d => d.Item2.Root.Elements().Where(e => e.Name.LocalName.EndsWith("FlavorPoolDef"))).ToDictionary(e => e.Element("defName").Value, e => e);
        foreach (var f in Directory.GetFiles(Path.Combine(src, "ColonyLife"), "*.cs"))
            foreach (Match m in Regex.Matches(File.ReadAllText(f), "\"((?:Rimfolk|Council)_(?:Pool|Journal)_[A-Za-z_]+)\""))
                C(pools.ContainsKey(m.Groups[1].Value), "pool " + m.Groups[1].Value);
        var keyed = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        foreach (var k in new[] { "Fire", "Books", "Games", "Music", "Shrine", "None" }) C(keyed.Contains("<CC_Life_GroupPlace_When_" + k + ">"), "fulfilled when: " + k);
        foreach (var k in new[] { "Reached", "StillAt", "LetGo", "Died" }) C(keyed.Contains("<CC_Year_SupperAim_" + k + ">"), "the year letter's readback: " + k);
        foreach (TryAct a in Enum.GetValues(typeof(TryAct))) C(keyed.Contains("<CC_Life_TryOut_Report_" + a + ">"), "what they're doing: " + a);
        // No em dash or exclamation mark in the new words.
        foreach (var file in new[] { "Defs/FlavorPoolDefs/Rimfolk_Lines_ColonyLife.xml", "Defs/WishDefs/Rimfolk_Wishes_ColonyLife.xml", "Defs/DilemmaDefs/Rimfolk_Dilemmas_Customs_ColonyLife.xml",
                     "Defs/FlavorPoolDefs/Rimfolk_Talk_Lines_ColonyLife.xml", "Languages/English/Keyed/Rimfolk_ColonyLife2.xml" })
        {
            string text = File.ReadAllText(Path.Combine(root, file));
            C(!text.Contains("—") && !Regex.IsMatch(text, @"!\s*<"), "plain words, no em dashes or exclamation marks: " + file);
        }

        // ================= Seams =================

        C(Read("PlayOut/PlayOutTracker.cs").Contains("LifeBook.Tick(now);") && Read("PlayOut/PlayOutTracker.cs").Contains("LifeBook.PlayDone(p, took);"), "the readings and try-outs ride on the play-outs' own pass");
        C(Read("CouncilDynamics.cs").Contains("TryOuts.Begin(w, who, thanks)"), "a met wish's thanks wait for the try-out");
        C(Read("Interactions.cs").Contains("TalkedUp(initiator, recipient)"), "the wish talked up in the game's own conversation");
        C(Read("Social/Groups/GroupHabits.cs").Contains("GroupPlaces.CallAt(g, map, out var place)"), "a group called to its own place");
        C(Read("Ambitions/Ambitions.cs").Contains("AmbitionScenes.Run(a, map, piece)") && Read("Ambitions/Ambitions.cs").Contains("AmbitionScenes.Remember("), "an ambition's scene and day");
        C(Read("World/YearLetter.cs").Contains("YearRead.Plan(") && Read("World/YearLetter.cs").Contains("FirstSupper.YearLines("), "the year read aloud, and the aims read back");
        C(Read("Annals.cs").Contains("YearRead.Keeper(comp, now)"), "the record keeper writes the year");
        C(Read("Heart/HeartNeeds.cs").Contains("ModAims.Purpose(p)") && Read("Aspirations/Aspirations.cs").Contains("GoalMath.ModAimsFactor(ModAims.HasAims(p))"), "the mod's aims in the heart and the life goals");
        C(Read("ColonyLife/TalkBacking.cs").Contains("public bool WouldBack(Wish w, Pawn p)") && Read("CouncilGameComponent.cs").Contains("internal void CoSign(Wish w, Pawn p)"),
            "the seam for the meeting's any other business: who would back a wish, and the co-signing");
        return n;
    }

    private static bool NullOrEmptyString(this string s) => string.IsNullOrEmpty(s);
}
