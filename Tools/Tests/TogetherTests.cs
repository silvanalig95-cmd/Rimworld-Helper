using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Moments between colonists happen where they really are (Social/TogetherMath.cs): the waiting list keeps each moment
/// once, gives up on time, makes room for new ones, and lets one go and find the other only while there's time for it.
/// And Rimfolk speaks one way (Immersion/SpeechMath.cs, Speech.cs): bubbles only when asked for and Interaction Bubbles
/// is there, a moment between two kept in the log without a second bubble, and no other code throwing text or adding
/// to the play log on its own.
/// </summary>
public static class TogetherTests
{
    private const int H = TogetherMath.TicksPerHour;

    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "together: " + what);
        }

        // ---------- The waiting list ----------
        var q = new PendingQueue<string>(3);
        var first = q.Add("confide|1|2", "first", 0, 10 * H);
        Check(first != null && q.Count == 1 && q.Contains("confide|1|2"), "a moment waits under its key");
        Check(q.Add("confide|1|2", "second", H, 20 * H) == null && q.Count == 1 && q.Entries[0].item == "first", "the same moment never waits twice: the first stands");
        Check(q.Add("late", "x", 5 * H, 5 * H) == null && q.Add("later", "x", 5 * H, 4 * H) == null && q.Count == 1, "nothing already out of time is added");
        Check(q.Find(first.id) == first && q.Find(first.id + 99) == null, "found by its id (the walk over carries it)");
        var b = q.Add("lesson|3|4", "b", 0, 30 * H, 2 * H);
        var c = q.Add("rounds|5|6", "c", 0, 2 * H);
        Check(q.Count == 3 && b.Seeks && !first.Seeks && b.id != first.id && c.id != b.id, "ids of their own; only some go and find the other");
        // Full: the one closest to giving up makes way, not the oldest.
        var d = q.Add("thanks|7|8", "d", H, 40 * H);
        Check(q.Count == 3 && d != null && !q.Contains("rounds|5|6") && q.Contains("confide|1|2"), "full: the one closest to giving up makes way");
        // Running out of time.
        var gone = q.Expire(10 * H);
        Check(gone.Count == 1 && gone[0].key == "confide|1|2" && q.Count == 2, "a moment out of time is dropped (at the tick it gives up)");
        Check(q.Expire(10 * H).Count == 0 && q.Count == 2, "nothing more to drop at the same tick");
        gone = q.Expire(100 * H);
        Check(gone.Count == 2 && gone[0].key == "lesson|3|4" && gone[1].key == "thanks|7|8" && q.Count == 0, "dropped oldest first, and then the list is empty");
        q.Add("a", "a", 0, H);
        Check(q.RemoveKey("a") && !q.RemoveKey("a") && q.Count == 0, "taken out by key once");
        var e = q.Add("e", "e", 0, H);
        Check(q.Remove(e) && !q.Remove(e) && !q.Remove(null), "taken out by entry once");
        q.Add("f", "f", 0, H);
        q.Clear();
        Check(q.Count == 0 && !q.Contains("f"), "cleared (a new game or a load)");
        Check(new PendingQueue<int>(0).Max == 1, "room for at least one");

        // ---------- How long a moment waits, and when one may go to the other ----------
        Check(TogetherMath.Expires(100, 24f) == 100 + 24 * H, "a day's wait");
        Check(TogetherMath.Expires(0, 0.1f) == H && TogetherMath.Expires(0, 500f) == 48 * H, "at least an hour, at most two days");
        Check(TogetherMath.SeekFrom(50, -1f) == -1 && TogetherMath.SeekFrom(50, 0f) == 50 && TogetherMath.SeekFrom(0, 6f) == 6 * H, "when one may go and find the other");
        Check(!TogetherMath.MaySeek(10 * H, -1, 20 * H), "never, for moments that only come by chance");
        Check(!TogetherMath.MaySeek(5 * H, 6 * H, 20 * H) && TogetherMath.MaySeek(6 * H, 6 * H, 20 * H), "not before the wait for a chance meeting is over");
        Check(!TogetherMath.MaySeek(20 * H - TogetherMath.SeekMarginTicks, 0, 20 * H) && TogetherMath.MaySeek(20 * H - TogetherMath.SeekMarginTicks - 1, 0, 20 * H),
            "no walk started too close to giving up");
        Check(TogetherMath.WalkTicks >= H && TogetherMath.WalkTicks <= 2 * H && TogetherMath.TalkTicks >= 120 && TogetherMath.TalkTicks <= 600, "a walk and a word of sensible length");

        // ---------- A day of hourly looks ----------
        // Moments waiting a day; the pair of each meets (or not) at some hour. Each happens at the first look they're
        // together, at most once, and a pair that never meets is dropped when its day is out.
        var sim = new PendingQueue<int>(40);
        var meets = new Dictionary<string, int> { ["early"] = 3, ["late"] = 23, ["never"] = -1, ["tomorrow"] = 30 };
        foreach (var k in meets.Keys) sim.Add(k, meets[k], 0, TogetherMath.Expires(0, 24f));
        var happened = new Dictionary<string, int>();
        for (int hour = 1; hour <= 36; hour++)
        {
            int now = hour * H;
            sim.Expire(now);
            foreach (var en in sim.Entries.ToList())
                if (en.item == hour)
                {
                    sim.Remove(en);
                    happened[en.key] = hour;
                }
        }
        Check(happened.TryGetValue("early", out int he) && he == 3, "it happens the hour they're together");
        Check(happened.TryGetValue("late", out int hl) && hl == 23, "late in the day still counts");
        Check(!happened.ContainsKey("never") && !happened.ContainsKey("tomorrow") && sim.Count == 0, "a pair that doesn't meet within the day: it doesn't happen");

        // ---------- How speech shows ----------
        Check(SpeechMath.Shows(SpeechMode.Bubbles, true) == SpeechMode.Bubbles, "bubbles when asked for and Interaction Bubbles is there");
        Check(SpeechMath.Shows(SpeechMode.Bubbles, false) == SpeechMode.Floating, "floating text when it isn't");
        Check(SpeechMath.Shows(SpeechMode.Floating, true) == SpeechMode.Floating && SpeechMath.Shows(SpeechMode.Floating, false) == SpeechMode.Floating, "floating text when asked for, either way");
        Check(SpeechMath.Shows(SpeechMode.Off, true) == SpeechMode.Off && SpeechMath.Shows(SpeechMode.Off, false) == SpeechMode.Off, "off is off");

        // ---------- How much of a line shows ----------
        Check(SpeechMath.Shorten("Still no banner. I asked twice.") == "Still no banner. I asked twice.", "two short sentences that fit both show");
        string longer = "The kitchen floor will be spotless, and my knees will be ruined. I'll be scrubbing until the frost comes, you'll see.";
        Check(SpeechMath.Shorten(longer) == "The kitchen floor will be spotless, and my knees will be ruined.", "too long: the sentences that fit, never half of one");
        string three = "Cold again. Fires out by dusk. Coats on indoors, and nobody says a word about it to the council either.";
        Check(SpeechMath.Shorten(three) == "Cold again. Fires out by dusk.", "as many whole sentences as fit: " + SpeechMath.Shorten(three));
        string run = new string('a', 120) + ".";
        Check(SpeechMath.Shorten(run).Length == 90 && SpeechMath.Shorten(run).EndsWith("…"), "a first sentence too long alone is cut with an ellipsis");
        Check(SpeechMath.Shorten("\"Bed. Now.\"") == "Bed. Now.", "quotes round the whole line go");
        Check(SpeechMath.Shorten("It costs 3.5 silver a head, he says. " + new string('b', 80) + ".") == "It costs 3.5 silver a head, he says.", "a point inside a number doesn't end a sentence");
        Check(SpeechMath.Shorten("He told me \"go home now.\" " + new string('c', 80) + ".") == "He told me \"go home now.\"", "a closing quote goes with its sentence");
        Check(SpeechMath.Shorten(longer, 120) == longer, "a message's longer limit shows the whole of it (117 characters)");
        foreach (SpeechMode setting in Enum.GetValues(typeof(SpeechMode)))
        {
            foreach (bool loaded in new[] { true, false })
            {
                var shows = SpeechMath.Shows(setting, loaded);
                foreach (bool show in new[] { true, false })
                {
                    bool through = SpeechMath.ThroughLog(shows, loaded, show);
                    bool floats = SpeechMath.Floats(shows, show);
                    bool bubble = loaded && through;
                    string what = $"{setting}, bubbles {(loaded ? "loaded" : "not loaded")}, {(show ? "shown" : "log only")}";
                    // Exactly one way a moment between two shows, or none.
                    Check(!(bubble && floats), $"{what}: never a bubble and floating text at once");
                    Check((bubble || floats) == (show && shows != SpeechMode.Off), $"{what}: shown exactly when it should be");
                    Check(!loaded || !through || shows == SpeechMode.Bubbles, $"{what}: a bubble only when speech shows as bubbles");
                    Check(loaded || through, $"{what}: without Interaction Bubbles it goes through the log as always");
                }
            }
        }
        Check(Enum.GetValues(typeof(SpeechMode)).Length == 3 && (SpeechMode)0 == SpeechMode.Bubbles, "three choices, bubbles first (the default)");

        // ---------- One way of speaking, in the code ----------
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        var throwers = new List<string>();
        var loggers = new List<string>();
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            string text = Regex.Replace(File.ReadAllText(file), @"//[^\n]*", "");
            string rel = Path.GetRelativePath(src, file).Replace('\\', '/');
            if (text.Contains("MoteMaker.ThrowText(")) throwers.Add(rel);
            if (Regex.IsMatch(text, @"PlayLog\.Add\(|AllEntries\.Insert\(")) loggers.Add(rel);
        }
        Check(throwers.All(f => f == "Immersion/Speech.cs" || f == "Moods/RewardInstead.cs"),
            "text over heads only from Speech (and the reward mark, which isn't speech): " + string.Join(", ", throwers));
        Check(loggers.All(f => f == "Immersion/Speech.cs"), "only Speech adds to the play log: " + string.Join(", ", loggers));
        string speak = File.ReadAllText(Path.Combine(src, "CouncilGameComponent.cs"));
        Check(Regex.IsMatch(speak, @"public static void Speak\(Pawn p, string line, Color color\) => Speech\.Say\("), "the council's Speak goes through Speech");
        Check(speak.Contains("Together.Hourly(this)") && speak.Contains("Together.Reset()"), "the council's hour looks at what waits, and a new game forgets it");
        string arrivals = File.ReadAllText(Path.Combine(src, "Happenings", "HappeningHelpers.cs"));
        Check(Regex.IsMatch(arrivals, @"public static void Say\(Pawn p, string text\) => Speech\.Say\("), "events' Say goes through Speech");
        string log = File.ReadAllText(Path.Combine(src, "Integrations", "SocialLog.cs"));
        Check(log.Contains("!Near(a, b)) return;") && log.Contains("Speech.Logged("), "a moment between two is logged only when they're near, and shown through Speech");

        // ---------- The defs and words it needs ----------
        var defs = new List<XElement>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories))
            defs.AddRange(XDocument.Load(file).Root!.Elements());
        var talk = defs.FirstOrDefault(x => x.Name.LocalName == "JobDef" && (string)x.Element("defName") == "Council_TalkTo");
        Check(talk != null && (string)talk.Element("driverClass") == "ColonyCouncil.JobDriver_CouncilTalk", "the walk over to someone has its job");
        var said = defs.FirstOrDefault(x => x.Name.LocalName == "InteractionDef" && (string)x.Element("defName") == "Council_Speech");
        Check(said != null && (string)said.Element("workerClass") == "ColonyCouncil.InteractionWorker_LogOnly", "a line one person says has its own entry, never picked as a chat");
        var keyed = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                keyed[el.Name.LocalName] = el.Value;
        var used = new HashSet<string>();
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
            foreach (Match m in Regex.Matches(File.ReadAllText(file), "\"(CC_Together_\\w+)\""))
                used.Add(m.Groups[1].Value);
        Check(used.Count >= 8, $"the moments that now happen face to face have their lines ({used.Count})");
        foreach (var k in used)
        {
            Check(keyed.ContainsKey(k), $"{k} exists");
            if (keyed.TryGetValue(k, out var line)) Check(!line.Contains('!') && line.Length <= 140, $"{k}: one plain line");
        }
        return cases;
    }
}
