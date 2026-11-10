using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The letter carried to the mailbag and read from its message (the player, 10 November 2026: "i think i instructed a pawn
/// to send a letter, the text appeared above but I can't find it in the mailbag? do the pawns actually bring the letters to
/// the bag also? i should be able to read the letter directly by clicking the pop-up"; Post/PostMath.cs,
/// Letters/LetterClickMath.cs): how a letter just written leaves and what its message says, the writer's walk to the
/// mailbag and its fallback (never lost, never stuck), the mailbag's sections (in it, being brought, waiting at the gate),
/// the pop-up's click and its hint, the player's own save end to end, and the seams.
/// </summary>
public static class LetterCarryTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void C(bool ok, string what)
        {
            cases++;
            check(ok, "the letter carried and read: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var keys = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var e in XDocument.Load(f).Root!.Elements())
                keys[e.Name.LocalName] = e.Value;
        Written(C, keys);
        Bringing(C);
        Sections(C, keys);
        Click(C, keys);
        TheirSave(C, keys);
        Seams(root, Code, C, keys);
        return cases;
    }

    private static string Fill(string text, params (string key, string value)[] args)
    {
        foreach (var (k, v) in args) text = text.Replace("{" + k + "}", v);
        return text;
    }

    // ---------------- Written: how it leaves, and its message ----------------

    private static void Written(Action<bool, string> C, Dictionary<string, string> keys)
    {
        C(PostMath.Written(true, false) == PostMath.WrittenWay.Post, "at peace with the post on: by the post, carried to the mailbag");
        C(PostMath.Written(true, true) == PostMath.WrittenWay.GoBetween && PostMath.Written(false, true) == PostMath.WrittenWay.GoBetween,
            "their people at war: a go-between at the gate, the post on or off");
        C(PostMath.Written(false, false) == PostMath.WrittenWay.Messenger, "the post off: a messenger of theirs at the gate");
        C(PostMath.WrittenKey(PostMath.WrittenWay.Post, true) == null, "by the post nothing is said as it is written: the message comes as it goes in the mailbag");
        C(PostMath.WrittenKey(PostMath.WrittenWay.GoBetween, true) == "CC_Letter_Msg_WrittenWar" && PostMath.WrittenKey(PostMath.WrittenWay.GoBetween, false) == "CC_Letter_Msg_WrittenWarNoPost"
          && PostMath.WrittenKey(PostMath.WrittenWay.Messenger, false) == "CC_Letter_Msg_Written", "each way not by the post has its message");
        foreach (var w in new[] { PostMath.WrittenWay.GoBetween, PostMath.WrittenWay.Messenger })
            foreach (bool post in new[] { true, false })
            {
                string key = PostMath.WrittenKey(w, post);
                C(keys.TryGetValue(key, out var text) && text.StartsWith("{PAWN} has written to {TO}.") && !text.Contains("!") && !text.Contains("—"),
                    $"{key}: who wrote to whom, then why it isn't in the mailbag, plainly");
            }
        string war = keys["CC_Letter_Msg_WrittenWar"];
        C(war.Contains("at war") && war.Contains("can't go with the post") && war.Contains("go-between under a white flag") && war.Contains("to the gate"),
            "at war with the post on: it says why it can't go with the post and who comes to the gate");
        string off = keys["CC_Letter_Msg_Written"];
        C(off.Contains("post is off") && off.Contains("doesn't go in the mailbag") && off.Contains("messenger of theirs") && !off.Contains("go-between"),
            "the post off: it says so, and a messenger of theirs comes");
        C(keys["CC_Letter_Msg_WrittenWarNoPost"].Contains("go-between") && !keys["CC_Letter_Msg_WrittenWarNoPost"].Contains("the post"),
            "at war with the post off: the go-between, no word of a post that isn't running");
        foreach (var k in new[] { "CC_Mail_Msg_InBagLate", "CC_Mail_Msg_AtGateLate" })
            C(keys.TryGetValue(k, out var t) && t.StartsWith("{PAWN}'s letter to {TO}") && t.Contains("now") && !t.Contains("{PAWN} put"),
                $"{k}: in without anyone walking it over, said without saying who did");
        C(keys["CC_Mail_Msg_InBag"].StartsWith("{PAWN} put the letter"), "brought by the writer: they put it in");
    }

    // ---------------- The writer's walk, and its fallback ----------------

    private static void Bringing(Action<bool, string> C)
    {
        int fall = PostMath.BringFallbackTicks;
        C(fall >= 5000 && fall <= 15000, $"the writer has a few hours to bring it ({fall / 2500f} hours)");
        C(PostMath.PutInTicks > 0 && PostMath.PutInTicks <= 180, "the put-in is short");
        C(PostMath.Bring(false, true, true, 0) == PostMath.BringStep.Walk, "a free writer walks it over");
        C(PostMath.Bring(false, true, false, 0) == PostMath.BringStep.Wait, "a writer busy with something that can't wait (drafted, downed, a raid on) is waited for");
        C(PostMath.Bring(true, true, true, 0) == PostMath.BringStep.Wait && PostMath.Bring(true, true, false, fall - 1) == PostMath.BringStep.Wait,
            "a writer on the way with it is left to it");
        C(PostMath.Bring(true, true, false, fall) == PostMath.BringStep.Wait && PostMath.Bring(true, true, false, fall * 2) == PostMath.BringStep.In,
            "a writer on the way past the hours finishes the walk, but never past twice the hours");
        C(PostMath.Bring(false, false, false, 0) == PostMath.BringStep.In, "a writer no longer the colony's (dead, gone): it goes in at once");
        C(PostMath.Bring(false, true, false, fall - 1) == PostMath.BringStep.Wait && PostMath.Bring(false, true, false, fall) == PostMath.BringStep.In,
            "a writer kept from it the whole while: in after the hours all the same");
        C(PostMath.Bring(false, true, true, fall) == PostMath.BringStep.In, "past the hours it goes in rather than starting a walk then");
        // Never stuck: whatever the writer does (free or not, walks cut off at any point), every letter is in by twice the hours.
        var rng = new Random(11);
        int worst = 0;
        bool always = true;
        for (int run = 0; run < 2000; run++)
        {
            int waited = 0, walking = -1;
            bool inBag = false;
            while (waited <= fall * 3 && !inBag)
            {
                if (walking >= 0)
                {
                    walking += 250;
                    if (rng.NextDouble() < 0.1) walking = -1; // cut off: drafted, downed
                    else if (walking >= 1500) inBag = true; // at the mailbag: put in
                }
                if (inBag) break;
                switch (PostMath.Bring(walking >= 0, rng.NextDouble() > 0.002, rng.NextDouble() < 0.3, waited))
                {
                    case PostMath.BringStep.In:
                        inBag = true;
                        break;
                    case PostMath.BringStep.Walk:
                        walking = 0;
                        break;
                }
                waited += 250;
            }
            always &= inBag;
            worst = Math.Max(worst, waited);
        }
        C(always && worst <= fall * 2 + 250, $"never stuck: 2000 writers drafted, downed and cut off at random, every letter in by {worst / 2500f:0.#} hours");
    }

    // ---------------- The mailbag's sections ----------------

    private static void Sections(Action<bool, string> C, Dictionary<string, string> keys)
    {
        C(PostMath.SectionOf(true, true, false, false) == PostMath.BagSection.Bringing, "written for the post and in its writer's hand: listed as being brought");
        C(PostMath.SectionOf(true, false, false, false) == PostMath.BagSection.In, "in the mailbag: listed as in it");
        C(PostMath.SectionOf(false, false, true, false) == PostMath.BagSection.AtGate, "waiting for a go-between or a messenger: listed at the gate");
        C(PostMath.SectionOf(false, false, true, true) == PostMath.BagSection.None, "to be handed to someone of theirs here: not at the gate");
        C(PostMath.SectionOf(false, false, false, false) == PostMath.BagSection.None, "on the road, answered or still to be written: not in the mailbag's list");
        C(PostMath.MayTake(true, false, false), "a carrier going its way takes a letter in the mailbag");
        C(!PostMath.MayTake(true, true, false), "no carrier takes a letter still in its writer's hand");
        C(!PostMath.MayTake(true, false, true) && !PostMath.MayTake(false, false, false), "nor one kept for the colony's own rider, nor one not in the outbox");
        C(PostMath.GateHeadKey(1, 0) == "CC_Mail_Gate_HeadWar" && PostMath.GateHeadKey(0, 2) == "CC_Mail_Gate_HeadMessenger" && PostMath.GateHeadKey(1, 1) == "CC_Mail_Gate_HeadBoth",
            "the gate's heading by who comes: a go-between, a messenger, or both");
        foreach (var k in new[] { "CC_Mail_Gate_HeadWar", "CC_Mail_Gate_HeadMessenger", "CC_Mail_Gate_HeadBoth" })
            C(keys.TryGetValue(k, out var t) && t.StartsWith("Waiting at the gate for a"), $"{k}: \"Waiting at the gate for...\"");
        C(keys["CC_Mail_Gate_HeadWar"] == "Waiting at the gate for a go-between:", "the player's own words for the section");
        foreach (bool hostile in new[] { true, false })
            foreach (bool here in new[] { true, false })
            {
                string k = PostMath.GateLineKey(hostile, here);
                C(keys.TryGetValue(k, out var t) && t.StartsWith("  - To {TO}, {PURPOSE}, written by {PAWN}: ") && (t.Contains("go-between") == hostile)
                  && (here == t.Contains("is at the gate")), $"{k}: to whom, what for, who wrote it, and who comes for it or is there");
            }
        C(keys["CC_Mail_Line_Bringing"].EndsWith("{PAWN} is bringing it."), "the line of a letter being brought: \"Guido is bringing it\"");
        C(keys.ContainsKey("CC_Mail_Status_Bringing") && keys.ContainsKey("CC_Mail_Status_BringingGate") && keys.ContainsKey("CC_Mail_Gizmo_LetterGate"),
            "being brought on the letters page, and a letter at the gate in the read list, in words");
    }

    // ---------------- The pop-up's click ----------------

    private static void Click(Action<bool, string> C, Dictionary<string, string> keys)
    {
        C(LetterClickMath.HintKey(false, true, true) == null, "a letter not written out in full: no hint, no click");
        C(LetterClickMath.HintKey(true, true, false) == "CC_LetterRead_MsgClick" && LetterClickMath.HintKey(true, true, true) == "CC_LetterRead_MsgClick",
            "written out and the click works: \"Click to read it.\"");
        C(LetterClickMath.HintKey(true, false, true) == "CC_LetterRead_Msg" && LetterClickMath.HintKey(true, false, false) == "CC_LetterRead_MsgPeople",
            "the click not working: where it can be read instead (the mailbag while it is in it)");
        C(keys["CC_LetterRead_MsgClick"] == "Click to read it.", "the hint is short");
        C(LetterClickMath.Opens(true, 0, true, true, true), "a left click on a letter's message opens it");
        C(!LetterClickMath.Opens(true, 1, true, true, true), "a right click still dismisses it");
        C(!LetterClickMath.Opens(false, 0, true, true, true) && !LetterClickMath.Opens(true, 0, false, true, true), "only the mouse going down on the message itself");
        C(!LetterClickMath.Opens(true, 0, true, false, true), "a message with no letter is the game's (the camera jump)");
        C(!LetterClickMath.Opens(true, 0, true, true, false), "only while playing");
        var map = new ClickMap<string>();
        string a = new string('a', 3), b = new string('b', 3), sameAsA = new string('a', 3);
        map.Note(a, 11);
        map.Note(b, 12);
        C(map.Of(a) == 11 && map.Of(b) == 12, "each message keeps its own letter");
        C(map.Of(sameAsA) == -1, "kept by the message itself, never by its words");
        map.Note(a, 13);
        C(map.Of(a) == 13 && map.Count == 2, "a message noted again takes the newer letter");
        map.Note(null, 5);
        map.Note("x", -1);
        C(map.Count == 2 && map.Of(null) == -1, "nothing kept for no message or no letter");
        map.Prune(m => !ReferenceEquals(m, b));
        C(map.Of(b) == -1 && map.Of(a) == 13, "a message gone from the screen is let go");
        var many = new ClickMap<object>();
        var first = new object();
        many.Note(first, 1);
        for (int i = 0; i < LetterClickMath.MaxKept + 5; i++) many.Note(new object(), 100 + i);
        C(many.Count == LetterClickMath.MaxKept && many.Of(first) == -1, $"at most {LetterClickMath.MaxKept} kept, the oldest let go");
    }

    // ---------------- The player's save ----------------

    private static void TheirSave(Action<bool, string> C, Dictionary<string, string> keys)
    {
        // Letter 11: Guido's warning to Will, mayor of Kur'Fokin Covenant, whose people is at war with the colony, the post on.
        var way = PostMath.Written(true, true);
        string text = Fill(keys[PostMath.WrittenKey(way, true)!], ("PAWN", "Guido"), ("TO", "Will, mayor of Kur'Fokin Covenant"));
        C(way == PostMath.WrittenWay.GoBetween
          && text == "Guido has written to Will, mayor of Kur'Fokin Covenant. With their people at war with us it can't go with the post: a go-between under a white flag will come to the gate for it.",
            "their save: the message says why the warning isn't in the mailbag and who comes for it");
        string hint = keys[LetterClickMath.HintKey(true, true, false)!];
        C((text + " " + hint).EndsWith("for it. Click to read it."), "their save: the message ends with how to read it, and a click opens it");
        // Waiting for the go-between (stage AwaitMessenger, no one of theirs here to carry it): in the mailbag's gate section.
        var section = PostMath.SectionOf(false, false, true, false);
        string head = keys[PostMath.GateHeadKey(1, 0)];
        string line = Fill(keys[PostMath.GateLineKey(true, false)], ("TO", "Will, mayor of Kur'Fokin Covenant"), ("PURPOSE", "a warning"), ("PAWN", "Guido"));
        C(section == PostMath.BagSection.AtGate && head == "Waiting at the gate for a go-between:"
          && line == "  - To Will, mayor of Kur'Fokin Covenant, a warning, written by Guido: their people are at war with us, so a go-between under a white flag will come for it.",
            "their save: the warning is listed in the mailbag under \"Waiting at the gate for a go-between\"");
        C(!PostMath.MayTake(false, false, false), "their save: no carrier going its way takes it, only the go-between");
        // The same letter to a people at peace: carried before it is in the bag.
        C(PostMath.Written(true, false) == PostMath.WrittenWay.Post, "at peace: by the post");
        C(PostMath.SectionOf(true, true, false, false) == PostMath.BagSection.Bringing && !PostMath.MayTake(true, true, false)
          && PostMath.Bring(false, true, true, 0) == PostMath.BringStep.Walk, "at peace: written, it is in Guido's hand, listed as being brought, and he walks it over");
        string bringing = Fill(keys["CC_Mail_Line_Bringing"], ("TO", "Will"), ("PURPOSE", "a warning"), ("PAWN", "Guido"));
        C(bringing == "  - To Will, a warning, written by Guido: Guido is bringing it.", "at peace: the mailbag's list says \"Guido is bringing it\"");
        C(PostMath.SectionOf(true, false, false, false) == PostMath.BagSection.In && PostMath.MayTake(true, false, false),
            "at peace: put in, it is in the bag and the next carrier going that way may take it");
    }

    // ---------------- The seams ----------------

    private static void Seams(string root, Func<string, string> Code, Action<bool, string> C, Dictionary<string, string> keys)
    {
        C(!Regex.IsMatch(Code("Letters/LetterClickMath.cs"), @"using (RimWorld|Verse|UnityEngine)"), "the click's rules use no game types");
        string letters = Code("World/NeighbourLetters.cs");
        int written = letters.IndexOf("public static void Written(NeighbourLetter l, Pawn p)", StringComparison.Ordinal);
        string body = written >= 0 ? letters.Substring(written, Math.Min(4000, letters.Length - written)) : "";
        C(body.Contains("var way = PostMath.Written(PostBook.On, l.hostile || NotableBook.Hostile(n));") && body.Contains("Outbox.Bring(l, p);"),
            "a written letter for the post is carried to the mailbag, not put in at once");
        int pickup = body.IndexOf("Pickup(l, n);", StringComparison.Ordinal), said = body.IndexOf("PostMath.WrittenKey(way, PostBook.On)", StringComparison.Ordinal);
        C(pickup >= 0 && said > pickup && body.Contains("if (!l.Open) return;"), "the go-between's message comes once one is on the way (none can come: the letter is let go, with why)");
        C(letters.Contains("Scribe_Values.Look(ref bringing, \"bringing\");"), "being brought is saved");
        C(letters.Contains("else if (l.stage == LetterStage.Outbox && l.bringing) Outbox.Bringing(l, now);") && letters.Contains("l.stage == LetterStage.Outbox && l.bringing);"),
            "the letters' 250-tick pass looks after a letter being brought");
        C(letters.Contains("if (l.ownRider && !l.bringing && PostBook.Instance?.RideFor(l.id) == null)"), "the colony's own rider sets out once it is in, not before");
        foreach (var k in new[] { "\"CC_Letter_Msg_WrittenFor\"", "\"CC_Letter_Msg_HandedBearer\"", "PostMath.WrittenKey(way, PostBook.On)" })
        {
            int at = letters.IndexOf(k, StringComparison.Ordinal);
            int call = at >= 0 ? letters.LastIndexOf("Notify.", at, StringComparison.Ordinal) : -1;
            C(call >= 0 && letters.Substring(call, at - call).StartsWith("Notify.LetterMessage(") && !letters.Substring(call, at - call).Contains(";"),
                $"{k}: the message opens the letter when clicked");
        }
        string outbox = Code("Post/Outbox.cs");
        C(outbox.Contains("PostMath.MayTake(l.stage == LetterStage.Outbox, l.bringing, l.ownRider)"), "no carrier takes a letter still being brought");
        C(outbox.Contains("switch (PostMath.Bring(OnTheWay(p), ours, ours && Free(p, l.map), now - l.stageTick))"), "the walk and its fallback follow the rule");
        C(outbox.Contains("JobMaker.MakeJob(PostDefOf.Rimfolk_PostPutIn, spot)") && outbox.Contains("p.jobs.jobQueue.EnqueueFirst(job, JobTag.Misc)"),
            "the writer walks it over as their next job from the table");
        C(outbox.Contains("l.bringing = false;") && outbox.Contains("Notify.LetterMessage(key.Translate("), "put in, it is no longer being brought, and its message opens it when clicked");
        C(outbox.Contains("public static bool Any(Map map) => Book?.letters.Any(l => l.stage == LetterStage.Outbox && !l.bringing"),
            "the post's watch counts only letters in the bag");
        C(outbox.Contains("PostMath.SectionOf(l.stage == LetterStage.Outbox, l.bringing, l.stage == LetterStage.AwaitMessenger, l.bearer != null && l.tripId < 0)"),
            "the mailbag's sections by the rule");
        string jobs = Code("Post/PostJobs.cs");
        C(jobs.Contains("public class JobDriver_PostPutIn : JobDriver") && jobs.Contains("this.FailOn(() => !Outbox.Carrying(pawn));") && jobs.Contains("Outbox.BroughtIn(pawn);")
          && jobs.Contains("Toils_General.Wait(PostMath.PutInTicks"), "the put-in job: walk, a short put-in with its bar, then in; ends if the letter isn't theirs to bring");
        string defs = File.ReadAllText(Path.Combine(root, "Defs", "Misc", "Rimfolk_Post.xml"));
        C(Regex.IsMatch(defs, @"<defName>Rimfolk_PostPutIn</defName>\s*<driverClass>ColonyCouncil\.JobDriver_PostPutIn</driverClass>"), "the job's def");
        string bag = Code("Post/Building_Mailbag.cs");
        C(bag.Contains("PostWords.GateLines(Map, GateLines)") && bag.Contains(".Concat(Outbox.AtGate(Map))"), "the mailbag's pane lists the letters at the gate, and they can be read from it");
        string words = Code("Post/PostWords.cs");
        C(words.Contains("PostMath.GateHeadKey(war, gate.Count - war)") && words.Contains("PostMath.GateLineKey(AtWar(l), MessengerHere(l))") && words.Contains("\"CC_Mail_Line_Bringing\""),
            "the gate's heading and lines, and the line of a letter being brought");
        C(Code("Post/OwnRiders.cs").Contains("if (l.stage == LetterStage.Outbox && !l.bringing) SetOut(l);"), "the colony's own rider waits for the letter to be in");
        // The click.
        string click = Code("Letters/LetterClick.cs");
        C(click.Contains("[HarmonyPatch(typeof(Message), nameof(Message.Draw))]") && click.Contains("public static class Patch_LetterMessageClick"),
            "the click is a small patch on the game's message, a class of its own");
        C(click.Contains("e.type != EventType.MouseDown") && click.Contains("e.Use();") && click.Contains("if (!LetterClick.Open(id)) return;"),
            "it opens the letter on the mouse going down and uses the click; a letter no longer kept leaves the click to the game");
        C(click.Contains("RimfolkPatches.Applied.Contains(nameof(Patch_LetterMessageClick))") && click.Contains("RimfolkPatches.On"),
            "\"Click to read it\" only while the patch is applied and the deeper hooks are on");
        C(click.Contains("shown.Prune(Messages.IsLive)"), "the map from a message to its letter lets go of messages gone from the screen");
        C(!Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil", "Post"), "*.cs").Any(f => File.ReadAllText(f).Contains("[HarmonyPatch")), "the post itself stays free of patches");
        string news = Code("UI/News.cs"), tell = Code("UI/Tell.cs"), notify = Code("Notify.cs");
        C(news.Contains("LetterClick.Make(item.text, item.read, item.type, item.look, out clickable)") && news.Contains("LetterClick.Shown(letter, item.read)"),
            "the message is made with its hint as it is shown, and kept with its letter");
        C(news.Contains("public int read = -1;") && !Regex.IsMatch(news, @"Scribe_Values\.Look\(ref read"), "the letter a message opens is not saved with the evening's news");
        C(tell.Contains("item.read = read;") && notify.Contains("public static void LetterMessage(") && notify.Contains("read: LetterClick.Readable(letterId) ? letterId : -1"),
            "a letter's message goes through the teller like any other, with its letter");
        C(Regex.Matches(string.Join("\n", Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText)),
            @"LetterClick\.Make\(").Count == 1, "only a message on screen gets \"Click to read it\" (never the evening's news)");
        foreach (var k in new[] { "CC_LetterRead_MsgClick", "CC_LetterRead_Msg", "CC_LetterRead_MsgPeople", "CC_Mail_Report_PutIn" })
            C(keys.ContainsKey(k), "a word is written: " + k);
        C(File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")).Contains("bringing"), "the saved flag is in the save's estimate");
    }
}
