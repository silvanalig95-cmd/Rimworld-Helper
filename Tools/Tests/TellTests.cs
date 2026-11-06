using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Rimfolk's one voice to the player (UI/Tell.cs, UI/TellMath.cs): the day's caps by colony, kind and channel, the same
/// thing told once within a few hours, a new day and a clock gone back, the kind a folder's messages count under, a
/// letter as a line of the evening's news, the settings' defaults; and in the source, every message or letter that
/// doesn't go through Tell is an answer to the player's own click (an input message, a debug file, or a
/// "// direct:" comment saying why).
/// </summary>
public static class TellTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "tell: " + what);
        }

        const int H = Sched.HourTicks, D = Sched.DayTicks;
        const int People = TellMath.KindPeople, Health = TellMath.KindHealth, Colony = TellMath.KindColony, World = TellMath.KindWorld, Land = TellMath.KindLand;
        const int Flavor = TellWeight.Flavor, Notable = TellWeight.Notable, Important = TellWeight.Important;
        const int Oak = 7, Elm = 9;

        // ---------------- The day's count, one for each colony ----------------

        var book = new TellBook();
        int t = D * 5 + H * 8;
        for (int i = 0; i < 3; i++)
            C(book.Judge("m" + i, Oak, Colony, false, Notable, 6, t + i) == TellVerdict.Show, $"the colony's message {i + 1} is shown");
        C(book.Judge("m3", Oak, World, false, Notable, 6, t + 3) == TellVerdict.Show, "the world's comes on a busy day too");
        C(book.Count(Oak, false, t + 3) == 4, "every kind counts toward one count for the colony (the caps of each kind folded in)");
        C(book.Judge("m4", Oak, People, false, Notable, 6, t + 4) == TellVerdict.Held, "half the day's shown: a person's news waits for the evening");
        C(book.Judge("m5", Oak, Health, false, Notable, 6, t + 5) == TellVerdict.Held && book.Judge("m6", Oak, Land, false, Notable, 6, t + 6) == TellVerdict.Held,
            "and so do health and the land");
        C(book.Judge("m7", Oak, Colony, false, Notable, 6, t + 7) == TellVerdict.Show && book.Judge("m8", Oak, Colony, false, Notable, 6, t + 8) == TellVerdict.Show,
            "the colony's still come until the day's count");
        C(book.Judge("m9", Oak, Colony, false, Notable, 6, t + 9) == TellVerdict.Held, "past the count even the colony's wait for the evening");
        C(book.Judge("m10", Oak, People, false, Important, 6, t + 10) == TellVerdict.Show, "something hard to miss is always shown");
        C(book.Judge("m11", Elm, People, false, Notable, 6, t + 11) == TellVerdict.Show, "another colony has its own count");
        C(book.Judge("l1", Oak, People, true, Notable, 2, t + 12 + TellMath.BusyTicks) == TellVerdict.Show, "letters are counted apart from messages");
        C(book.Judge("m12", TellMath.NoColony, People, false, Notable, 6, t + 13) == TellVerdict.Show, "news of no colony is counted on its own");
        C(book.Judge("m13", Oak, People, false, Notable, 6, D * 6 + 10) == TellVerdict.Show, "a new day starts the count again");
        C(book.Count(Oak, false, D * 6 + 10) == 1, "and counts from one");
        C(book.Tally(Oak, "message", D * 6 + 10) == 1, "the day's tally starts again too");

        // ---------------- A busy day ----------------

        book.Clear();
        t = D * 20 + H * 9;
        C(book.Judge("raid", Oak, World, true, Important, 4, t) == TellVerdict.Show, "a raid's letter is shown");
        C(book.Busy(Oak, 0, t + H), "the hours after something hard to miss are busy");
        C(book.Judge("bond", Oak, People, false, Notable, 12, t + H) == TellVerdict.Held, "a new friendship waits for the evening then");
        C(book.Judge("wish", Oak, People, true, Notable, 4, t + H * 2) == TellVerdict.Held, "and so does a person's letter");
        C(book.Judge("decree", Oak, Colony, false, Notable, 12, t + H * 2) == TellVerdict.Show, "the colony's own news still comes");
        C(!book.Busy(Oak, 0, t + TellMath.BusyTicks), "the busy hours pass");
        C(book.Judge("bond2", Oak, People, false, Notable, 12, t + TellMath.BusyTicks) == TellVerdict.Show, "and a person's news is shown again");
        book.Clear();
        book.Important(Oak, t - H, t);
        C(book.Busy(Oak, 0, t) && book.Judge("p", Oak, People, false, Notable, 12, t) == TellVerdict.Held, "the storyteller's raid an hour ago makes the day busy too");
        book.Clear();
        book.Important(Oak, t - TellMath.BusyTicks - 1, t);
        C(!book.Busy(Oak, 0, t), "a raid long past doesn't");
        C(book.Judge("f", Oak, People, false, Flavor, 12, t) == TellVerdict.Show, "colour that reaches the teller is weighed as worth knowing is");

        // ---------------- The same thing once ----------------

        book.Clear();
        t = D * 10;
        C(book.Judge("same", Oak, People, false, Notable, 20, t) == TellVerdict.Show, "a message is shown");
        C(book.Judge("same", Oak, People, false, Notable, 20, t + H) == TellVerdict.Merged, "the same an hour later is merged");
        C(book.Judge("same", Oak, People, false, Notable, 20, t + TellMath.MergeTicks - 1) == TellVerdict.Merged, "and just inside the window");
        C(book.Judge("same", Oak, People, false, Notable, 20, t + TellMath.MergeTicks) == TellVerdict.Show, "the window over, it's told again");
        C(book.Count(Oak, false, t + TellMath.MergeTicks) == 2, "a merged one isn't counted");
        C(book.Judge(null, Oak, People, false, Notable, 20, t + 1) == TellVerdict.Show && book.Judge(null, Oak, People, false, Notable, 20, t + 2) == TellVerdict.Show,
            "without a key nothing is the same as anything");
        book.Clear();
        book.Judge("said", Oak, People, false, Notable, 20, t);
        C(book.Told("said", t + H) && !book.Told("other", t + H) && book.Told("other", t + H * 2), "the evening's news asks whether a message said it already, and notes its own");
        book.Clear();
        C(book.Judge("x1", Oak, Colony, false, Notable, 1, t) == TellVerdict.Show && book.Judge("x2", Oak, Colony, false, Notable, 1, t + 1) == TellVerdict.Held,
            "past the count it's held");
        C(book.Judge("x2", Oak, Colony, false, Notable, 1, t + 2) == TellVerdict.Merged, "and the same again is merged, not held twice");
        book.Clear();
        C(book.Judge("later", Oak, People, false, Notable, 20, t + D) == TellVerdict.Show, "told in a later game");
        C(book.Judge("later", Oak, People, false, Notable, 20, t) == TellVerdict.Show, "a clock gone back (another game loaded) isn't the same window");
        // The memory of what was told stays small: what's older than the window is forgotten.
        book.Clear();
        for (int i = 0; i < 500; i++) book.Judge("k" + i, Oak, Colony, false, Important, 0, t + i * 10);
        book.Judge("last", Oak, Colony, false, Important, 0, t + 5000 + TellMath.MergeTicks + H);
        C(book.Remembered <= 2, $"old keys are forgotten ({book.Remembered} kept)");

        // ---------------- The evening ----------------

        C(TellMath.EveningKeep(new[] { 1, 2, 3 }, 12).All(k => k), "an ordinary evening carries every line");
        var scores = new List<int>();
        for (int i = 0; i < 16; i++) scores.Add(TellMath.EveningScore(Notable, false, i % 2 == 0 ? People : Colony));
        scores[15] = TellMath.EveningScore(Important, false, Land);
        scores[14] = TellMath.EveningScore(Notable, true, People);
        var keep = TellMath.EveningKeep(scores, 10);
        C(keep.Count(k => k) == 10, "a long evening carries the most it may");
        C(TellMath.EveningRoom(0) == TellMath.EveningLines && TellMath.EveningRoom(2) == TellMath.EveningLines - 2
          && TellMath.EveningRoom(40) == TellMath.EveningLeast && TellMath.EveningRoom(-3) == TellMath.EveningLines,
            "the evening has less room the busier the day was, never less than the least");
        C(keep[15] && keep[14], "the weightiest and what the day held back are carried");
        C(Enumerable.Range(0, 14).Where(i => i % 2 == 1).All(i => keep[i]), "the colony's news before one person's");
        C(!keep[12] && keep[0], "among equals the earlier first");
        C(TellMath.EveningScore(Important, false, People) > TellMath.EveningScore(Notable, true, Colony)
          && TellMath.EveningScore(Notable, true, People) > TellMath.EveningScore(Notable, false, Colony)
          && TellMath.EveningScore(Notable, false, Colony) > TellMath.EveningScore(Notable, false, People), "the order of weight, held, kind");
        C(TellMath.Weighty(Colony) && TellMath.Weighty(World) && !TellMath.Weighty(People) && !TellMath.Weighty(Health) && !TellMath.Weighty(Land),
            "the colony's and the world's weigh more");
        C(TellMath.BusyAt(12) == 6 && TellMath.BusyAt(2) == 2, "busy at half the day's messages");

        // ---------------- Kinds, folders and letter lines ----------------

        string news = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "UI", "News.cs"));
        var kinds = Regex.Match(news, @"enum NewsKind\s*\{([^}]*)\}").Groups[1].Value;
        var kindNames = Regex.Matches(Regex.Replace(kinds, @"///[^\n]*", ""), @"\b([A-Z]\w*)\b").Select(m => m.Groups[1].Value).ToHashSet();
        C(kindNames.SetEquals(new[] { "People", "Health", "Colony", "World", "Land" }), "the kinds of news: " + string.Join(", ", kindNames));
        var folders = Directory.GetDirectories(Path.Combine(root, "Source", "ColonyCouncil")).Select(Path.GetFileName).ToList();
        C(folders.All(f => kindNames.Contains(TellMath.KindOfFolder(f))), "every source folder counts under a kind of news");
        C(TellMath.KindOfFolder("World") == "World" && TellMath.KindOfFolder("Journeys") == "World" && TellMath.KindOfFolder("Outsiders") == "World",
            "the world's folders are world news");
        C(TellMath.KindOfFolder("Social") == "People" && TellMath.KindOfFolder("Secrets") == "People" && TellMath.KindOfFolder("Faith") == "People",
            "people's folders are their news");
        C(TellMath.KindOfFolder("Health") == "Health" && TellMath.KindOfFolder("Animals") == "Land" && TellMath.KindOfFolder("Fields") == "Land",
            "health and the land");
        C(TellMath.KindOfFolder("Building") == "Colony" && TellMath.KindOfFolder("") == "Colony" && TellMath.KindOfFolder(null) == "Colony",
            "the rest is the colony's");
        C(TellMath.FolderOf("/home/x/Source/ColonyCouncil/World/WorldSituations.cs") == "World", "a folder from a path");
        C(TellMath.FolderOf(@"C:\mods\Source\ColonyCouncil\Social\Family.cs") == "Social", "a folder from a Windows path");
        C(TellMath.FolderOf("/_/Notify.cs") == "_" && TellMath.FolderOf("Notify.cs") == "" && TellMath.FolderOf(null) == "", "a file with no folder");
        C(TellMath.LetterLine("The herd's leader", "Bessie leads now.\n\n\"She knows the way,\" Tom says.") == "The herd's leader: Bessie leads now. \"She knows the way,\" Tom says.",
            "a letter as one line of the evening's news");
        C(TellMath.LetterLine("", "Only text.") == "Only text." && TellMath.LetterLine("Only a title", "") == "Only a title", "a letter with only one part");

        // ---------------- The defaults ----------------

        string settings = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "CouncilSettings.cs"));
        C(settings.Contains("public int messagesADay = " + TellMath.DefaultMessagesADay + ";"), "the day's messages default to " + TellMath.DefaultMessagesADay);
        C(settings.Contains("public int lettersADay = " + TellMath.DefaultLettersADay + ";"), "the day's letters default to " + TellMath.DefaultLettersADay);
        C(settings.Contains("Scribe_Values.Look(ref messagesADay, \"messagesADay\"") && !settings.Contains("\"tellMessagesADay\""),
            "a new key, so an older settings file's cap of each kind doesn't stand for the colony's whole day");
        C(TellMath.DefaultMessagesADay >= TellMath.MinMessagesADay && TellMath.DefaultMessagesADay <= TellMath.MaxMessagesADay
          && TellMath.DefaultLettersADay >= TellMath.MinLettersADay && TellMath.DefaultLettersADay <= TellMath.MaxLettersADay, "the defaults inside the sliders");
        C(TellMath.DefaultMessagesADay >= 8 && TellMath.DefaultLettersADay >= 3, "an ordinary day's telling fits the count (the save's busiest day: four letters that only tell)");
        C(TellMath.MergeHours >= 2 && TellMath.MergeHours <= 8, "the same thing is told once within a few hours");
        C(TellMath.EveningLines >= 7 && TellMath.EveningLeast >= 3, "a quiet evening's letter carries an ordinary day (the save's: two to seven lines), a busy one the few that weigh most");
        string kindEnum = Regex.Match(news, @"enum NewsKind\s*\{([^}]*)\}").Groups[1].Value;
        var order = Regex.Matches(Regex.Replace(kindEnum, @"///[^\n]*", ""), @"\b([A-Z]\w*)\b").Select(m => m.Groups[1].Value).ToList();
        C(order.IndexOf("People") == TellMath.KindPeople && order.IndexOf("Health") == TellMath.KindHealth && order.IndexOf("Colony") == TellMath.KindColony
          && order.IndexOf("World") == TellMath.KindWorld && order.IndexOf("Land") == TellMath.KindLand, "the kinds as numbers follow NewsKind's order");

        // ---------------- The teller in the source ----------------

        string tell = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "UI", "Tell.cs"));
        string comp = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "CouncilGameComponent.cs"));
        C(comp.Contains("public void AddChronicle(ChronicleEntry entry) => Tell.Chronicle(entry);") && tell.Contains("History.FromChronicle(entry);"),
            "every chronicle line goes through the teller, which writes the history");
        C(Regex.IsMatch(tell, @"Book\.Judge\(""M\|"" \+ colony[^;]*item\.weight, MessagesADay, Now\)") && Regex.IsMatch(tell, @"Book\.Judge\(""L\|""[^;]*WeightOf\(importance\), LettersADay, Now\)"),
            "messages and letters are weighed by importance and kind against the colony's day");
        C(tell.Contains("if (Book.Told(\"M|\" + colony + \"|\" + text, Now)) return;"), "the evening's news doesn't repeat a message just shown");
        C(tell.Contains("LastThreatBigTick") && tell.Contains("Book.Important(colony, threat, Now)"), "a raid on the home makes its day busy");
        string newsCode = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "UI", "News.cs"));
        C(newsCode.Contains("TellMath.EveningKeep(") && newsCode.Contains("Tell.EveningRoom") && tell.Contains("TellMath.EveningRoom(Book.ShownToday(Now))") && newsCode.Contains("News.Archive(lines[i])"),
            "the evening's letter carries the weightiest lines and puts the rest in the History tab");
        C(newsCode.Contains("Scribe_Values.Look(ref weight, \"weight\", TellWeight.Notable);"), "a line waiting for the evening keeps its weight through a save (an older save's are worth knowing)");

        // ---------------- What doesn't go through Tell ----------------

        // A direct message is an answer to the player's own click: an input message (a rejection, a button's confirmation,
        // a caution), a debug file's, or one with a "// direct:" comment just above saying why. A letter that only tells
        // goes through Tell.Letter; a choice letter (one argument) through Tell.Ask, or directly.
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        var inputs = new HashSet<string> { "RejectInput", "SilentInput", "CautionInput", "TaskCompletion" };
        int direct = 0, told = 0, letters = 0;
        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(src, file).Replace('\\', '/');
            if (rel == "UI/Tell.cs" || rel == "UI/News.cs") continue;
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.TrimStart().StartsWith("//")) continue;
                told += Regex.Matches(line, @"\bTell\.(Message|Minor|Letter|Ask)\(").Count;
                bool msg = line.Contains("Messages.Message(");
                bool letter = line.Contains("SendStandardLetter(") || line.Contains("LetterStack.ReceiveLetter(") && !OneArgument(lines, i);
                if (!msg && !letter) continue;
                if (msg) direct++;
                else letters++;
                string stmt = Statement(lines, i);
                bool commented = Enumerable.Range(Math.Max(0, i - 3), Math.Min(3, i)).Any(j => Regex.IsMatch(lines[j], @"//\s*direct:"));
                bool debug = rel.EndsWith("Debug.cs");
                var types = Regex.Matches(stmt, @"MessageTypeDefOf\.(\w+)").Select(m => m.Groups[1].Value).ToList();
                bool input = msg && types.Count > 0 && types.All(inputs.Contains);
                if (!commented && !debug && !input) offenders.Add(rel + ":" + (i + 1));
            }
        }
        C(offenders.Count == 0, "every direct message or letter answers the player's own click (input type, debug file, or a // direct: comment)"
            + (offenders.Count > 0 ? ": " + string.Join(", ", offenders.Take(12)) : ""));
        C(told >= 200, $"the mod tells the player through Tell ({told} calls besides News.Post and Notify.Message; {direct} direct messages and {letters} direct letters left)");

        return n;
    }

    /// <summary>The statement starting at a line (up to the line that ends it), for the message's types.</summary>
    private static string Statement(string[] lines, int i)
    {
        var sb = new System.Text.StringBuilder();
        for (int j = i; j < Math.Min(lines.Length, i + 6); j++)
        {
            sb.Append(lines[j]).Append('\n');
            if (lines[j].TrimEnd().EndsWith(";")) break;
        }
        return sb.ToString();
    }

    /// <summary>A ReceiveLetter(letter) with a single argument: a letter made beforehand (a choice letter, the news).</summary>
    private static bool OneArgument(string[] lines, int i)
    {
        string s = Statement(lines, i);
        int at = s.IndexOf("ReceiveLetter(", StringComparison.Ordinal) + "ReceiveLetter".Length;
        int depth = 0;
        for (int k = at; k < s.Length; k++)
        {
            char c = s[k];
            if (c == '(' || c == '[' || c == '{') depth++;
            else if (c == ')' || c == ']' || c == '}')
            {
                depth--;
                if (depth == 0) return true;
            }
            else if (c == ',' && depth == 1) return false;
        }
        return true;
    }
}
