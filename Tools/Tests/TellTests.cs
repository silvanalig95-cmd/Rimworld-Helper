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
        const int People = 0, World = 3;
        const int Oak = 7, Elm = 9;

        // ---------------- The caps ----------------

        var book = new TellBook();
        int t = D * 5 + H * 8;
        for (int i = 0; i < 3; i++)
            C(book.Judge("m" + i, Oak, People, false, 3, t + i) == TellVerdict.Show, $"message {i + 1} of 3 is shown");
        C(book.Judge("m3", Oak, People, false, 3, t + 3) == TellVerdict.Held, "the fourth waits for the evening");
        C(book.Count(Oak, People, false, t + 3) == 3, "only those shown are counted");
        C(book.Judge("m4", Elm, People, false, 3, t + 4) == TellVerdict.Show, "another colony has its own cap");
        C(book.Judge("m5", Oak, World, false, 3, t + 5) == TellVerdict.Show, "another kind has its own cap");
        C(book.Judge("l1", Oak, People, true, 3, t + 6) == TellVerdict.Show, "letters are counted apart from messages");
        C(book.Judge("m6", Oak, People, false, 0, t + 7) == TellVerdict.Show, "no cap (an important message) is never held");
        C(book.Judge("m7", TellMath.NoColony, People, false, 3, t + 8) == TellVerdict.Show, "news of no colony is counted on its own");
        C(book.Judge("m8", Oak, People, false, 3, D * 6 + 10) == TellVerdict.Show, "a new day starts the count again");
        C(book.Count(Oak, People, false, D * 6 + 10) == 1, "and counts from one");

        // ---------------- The same thing once ----------------

        book.Clear();
        t = D * 10;
        C(book.Judge("same", Oak, People, false, 20, t) == TellVerdict.Show, "a message is shown");
        C(book.Judge("same", Oak, People, false, 20, t + H) == TellVerdict.Merged, "the same an hour later is merged");
        C(book.Judge("same", Oak, People, false, 20, t + TellMath.MergeTicks - 1) == TellVerdict.Merged, "and just inside the window");
        C(book.Judge("same", Oak, People, false, 20, t + TellMath.MergeTicks) == TellVerdict.Show, "the window over, it's told again");
        C(book.Count(Oak, People, false, t + TellMath.MergeTicks) == 2, "a merged one isn't counted");
        C(book.Judge(null, Oak, People, false, 20, t + 1) == TellVerdict.Show && book.Judge(null, Oak, People, false, 20, t + 2) == TellVerdict.Show,
            "without a key nothing is the same as anything");
        book.Clear();
        C(book.Judge("x1", Oak, People, false, 1, t) == TellVerdict.Show && book.Judge("x2", Oak, People, false, 1, t + 1) == TellVerdict.Held,
            "past the cap it's held");
        C(book.Judge("x2", Oak, People, false, 1, t + 2) == TellVerdict.Merged, "and the same again is merged, not held twice");
        book.Clear();
        C(book.Judge("later", Oak, People, false, 20, t + D) == TellVerdict.Show, "told in a later game");
        C(book.Judge("later", Oak, People, false, 20, t) == TellVerdict.Show, "a clock gone back (another game loaded) isn't the same window");
        // The memory of what was told stays small: what's older than the window is forgotten.
        book.Clear();
        for (int i = 0; i < 500; i++) book.Judge("k" + i, Oak, People, false, 0, t + i * 10);
        book.Judge("last", Oak, People, false, 0, t + 5000 + TellMath.MergeTicks + H);
        C(book.Remembered <= 2, $"old keys are forgotten ({book.Remembered} kept)");

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
        C(settings.Contains("public int tellMessagesADay = " + TellMath.DefaultMessagesADay + ";"), "the messages' cap defaults to " + TellMath.DefaultMessagesADay);
        C(settings.Contains("public int tellLettersADay = " + TellMath.DefaultLettersADay + ";"), "the letters' cap defaults to " + TellMath.DefaultLettersADay);
        C(TellMath.DefaultMessagesADay >= 15 && TellMath.DefaultLettersADay >= 5, "the caps are there for floods, not ordinary days");
        C(TellMath.MergeHours >= 2 && TellMath.MergeHours <= 8, "the same thing is told once within a few hours");

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
