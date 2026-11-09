using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Rimfolk's event cards (docs/design/rimfolk-look.md, stage 3, point 1; Source/ColonyCouncil/UI/EventCards/): the kind of
/// every letter Rimfolk sends and its words, what each of a letter's options becomes on the card, when the card may be
/// closed without an answer, what the council leans to by its votes, the layout from the card's least to a card made large
/// on a 4K screen, the plain letters kept by their ID, and the seams (every Rimfolk choice letter opens through the one
/// override, the plain letters through the one patch on the game's plain class only, the game's own Activate for every
/// option, the setting and its words, nothing read per frame).
/// </summary>
public static class EventCardTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "event cards: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => Regex.Replace(File.ReadAllText(Path.Combine(src, rel)), @"//[^\n]*", "");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_EventCards.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);

        // ---------------- The kinds ----------------

        var defs = new List<(string name, string cls)>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories))
        {
            var doc = XDocument.Load(f);
            foreach (var d in doc.Root!.Elements("LetterDef"))
                defs.Add((d.Element("defName")?.Value ?? "", d.Element("letterClass")?.Value ?? "StandardLetter"));
        }
        C(defs.Count >= 30, $"Rimfolk's letter defs are read ({defs.Count})");
        foreach (var (name, cls) in defs)
            C(EventCardMath.Kinds.ContainsKey(name), $"{name} ({cls}) has a kind on its card");
        foreach (var kv in EventCardMath.Kinds)
            C(EventCardMath.KindNames.Contains(kv.Value), $"{kv.Key}: its kind {kv.Value} is one of the kinds");
        foreach (var k in EventCardMath.KindNames)
        {
            C(keyed.TryGetValue("CC_EventCard_Kind_" + k, out var word) && word.Length > 0 && word.Length <= 40, $"the kind {k} has its word, short");
            C(!Regex.IsMatch(keyed.GetValueOrDefault("CC_EventCard_Kind_" + k) ?? "", "medieval|knight|castle|lord", RegexOptions.IgnoreCase), $"the kind {k}'s word fits every age");
        }
        C(EventCardMath.KindNames.Distinct().Count() == EventCardMath.KindNames.Length, "each kind once");
        foreach (var g in new[] { "ThreatBig", "ThreatSmall", "NegativeEvent", "PositiveEvent", "NeutralEvent", "Death" })
            C(EventCardMath.Kinds.ContainsKey(g), $"the game's {g}, as Rimfolk's plain letters use it, has a kind");
        C(EventCardMath.KindOf("SomeModsLetter", true) == "Answer" && EventCardMath.KindOf("SomeModsLetter", false) == "Told" && EventCardMath.KindOf(null, false) == "Told",
            "a def not in the table: a question with answers, else a letter that tells");
        C(EventCardMath.KindOf("Council_DilemmaLetter", false) == "Question", "the table's kind whatever the page shows");

        // ---------------- The options ----------------

        var leave = new List<string> { "Close", "Postpone", "Reject" };
        C(EventCardMath.RoleOf("Anything", true, false, true, leave, "Jump to location", "View related quest") == CardOptionRole.Link, "an info card's link is a link");
        foreach (var w in leave)
            C(EventCardMath.RoleOf(w, true, false, false, leave, "Jump to location", "View related quest") == CardOptionRole.Leave, $"the letter's own {w} goes to the foot");
        C(EventCardMath.RoleOf("Jump to location", true, false, false, leave, "Jump to location", "View related quest") == CardOptionRole.Jump, "the jump to the place goes to the header");
        C(EventCardMath.RoleOf("View related quest: The lost caravan", true, false, false, leave, "Jump to location", "View related quest") == CardOptionRole.Quest,
            "the quest's page, its name after it, goes to the foot");
        C(EventCardMath.RoleOf("Take them in", true, false, false, leave, "Jump to location", "View related quest") == CardOptionRole.Answer, "an option that closes the letter is an answer");
        C(EventCardMath.RoleOf("Call a seat's vote off", false, true, false, leave, "Jump to location", "View related quest") == CardOptionRole.Answer, "an option that leads on is an answer");
        C(EventCardMath.RoleOf("Look at Ada", false, false, false, leave, "Jump to location", "View related quest") == CardOptionRole.Act, "an option that does neither is something to do beside answering");
        C(EventCardMath.RoleOf("Closeness", true, false, false, leave, "Jump to location", "View related quest") == CardOptionRole.Answer
          && EventCardMath.RoleOf("", true, false, false, new List<string> { "", null! }, null!, null!) == CardOptionRole.Answer
          && EventCardMath.RoleOf(null!, true, false, false, null!, null!, null!) == CardOptionRole.Answer,
            "only the whole word is the letter's own; missing words never match");

        // ---------------- Closing without an answer ----------------

        C(EventCardMath.Closable(false, 0, 500) && EventCardMath.Closable(true, 900, 500) && EventCardMath.Closable(true, 502, 500),
            "a letter with time left, or none, can be put off: the card closes and it stays on the stack");
        C(!EventCardMath.Closable(true, 501, 500) && !EventCardMath.Closable(true, 500, 500), "on its last tick, when the game itself opens it, it wants an answer");

        // ---------------- What the council leans to ----------------

        C(EventCardMath.Lean(new[] { 3, 1, 0 }) == 0 && EventCardMath.Lean(new[] { 0, 1 }) == 1 && EventCardMath.Lean(new[] { 1, 4, 2 }) == 1, "the answer most are for");
        C(EventCardMath.Lean(new[] { 2, 2 }) == -1 && EventCardMath.Lean(new[] { 2, 1, 2 }) == -1 && EventCardMath.Lean(new[] { 0, 0 }) == -1 && EventCardMath.Lean(Array.Empty<int>()) == -1
          && EventCardMath.Lean(null!) == -1, "a split room or no votes leans nowhere");
        C(EventCardMath.Lean(new[] { 2, 2, 3 }) == 2, "a tie below the most doesn't split it");
        var votes = EventCardMath.Votes(new[] { 0, 2, 2, 5, -1, 1 }, 3);
        C(votes.SequenceEqual(new[] { 1, 1, 2 }) && EventCardMath.Votes(null!, 2).SequenceEqual(new[] { 0, 0 }) && EventCardMath.Votes(new[] { 0 }, -1).Length == 0,
            "the votes counted from the voters' choices, out of range left out");
        C(EventCardMath.LeanForAgainst(true, 5, 2) == 0 && EventCardMath.LeanForAgainst(true, 1, 4) == 1 && EventCardMath.LeanForAgainst(true, 3, 3) == -1
          && EventCardMath.LeanForAgainst(false, 5, 0) == -1, "a hand-over's vote: for going ahead, against, even, or not held");

        // ---------------- The layout from the least to large ----------------

        C(FramedWindowsMath.TryKey("EventCard", out var key) && key.leastW == EventCardMath.LeastW && key.leastH == EventCardMath.LeastH && !key.keepsBar,
            "the card's key, its least as its rules say, a dialog over the bottom bar");
        C(EventCardMath.OpenW >= EventCardMath.LeastW && EventCardMath.OpenHMax >= EventCardMath.LeastH, "it first opens no smaller than its least");
        foreach (var (w, label) in new[] { (EventCardMath.LeastW, "the least"), (EventCardMath.OpenW, "as it opens"), (900f, "wider"), (1280f, "large on a small screen"),
                     (1766f, "1920 made large"), (3532f, "4K made large") })
        {
            float body = WindowFrameMath.BodyWidth(w, true) - 16f;
            float side = EventCardMath.SideWidth(body), main = EventCardMath.MainWidth(body);
            C(main >= 380f, $"{label} ({w:0}): the text and the answers keep {main:0}");
            C(side == 0f || side >= 240f && side <= 340f, $"{label}: who is involved beside the text {side:0} wide, or under it");
            C(side == 0f ? main == body : Math.Abs(main + side + EventCardMath.SideGap - body) < 0.01f, $"{label}: the columns fill the body");
            for (int answers = 1; answers <= 6; answers++)
            {
                int cols = EventCardMath.AnswerColumns(main, answers);
                float aw = EventCardMath.AnswerWidth(main, cols);
                C(cols >= 1 && cols <= answers && cols <= 3 && aw >= 280f, $"{label}, {answers} answers: {cols} columns of {aw:0}, each card room for its words");
                C(Math.Abs(cols * aw + (cols - 1) * EventCardMath.AnswerGap - main) < 0.01f, $"{label}, {answers} answers: the cards fill the column");
            }
            float pic = EventCardMath.PictureSizeFor(body);
            float pictureAndTitle = EventCardMath.PictureWidth(EventCardMath.PictureMost, pic) + EventCardMath.HeadGap + EventCardMath.TitleLeast + EventCardMath.JumpMost + 8f
                                    + 2f * EventCardMath.HeadPad;
            C(pictureAndTitle <= body, $"{label}: the header holds three portraits, the title and the jump button ({pictureAndTitle:0} of {body:0})");
            C(pic == (body < EventCardMath.PictureFullFrom ? EventCardMath.PictureSmall : EventCardMath.PictureSize), $"{label}: the picture {pic:0} large");
        }
        C(EventCardMath.SideWidth(EventCardMath.SideFrom - 1f) == 0f && EventCardMath.SideWidth(EventCardMath.SideFrom) >= 240f, "the side column from its width on");
        C(EventCardMath.AnswerColumns(619f, 4) == 1 && EventCardMath.AnswerColumns(620f, 4) == 2 && EventCardMath.AnswerColumns(1080f, 4) == 3 && EventCardMath.AnswerColumns(2000f, 2) == 2,
            "one column, two, three, never more than the answers");
        C(EventCardMath.PictureWidth(0) == EventCardMath.PictureSize && EventCardMath.PictureWidth(1) == EventCardMath.PictureSize
          && EventCardMath.PictureWidth(5) == EventCardMath.PictureWidth(EventCardMath.PictureMost) && EventCardMath.PictureWidth(2) < 2f * EventCardMath.PictureSize,
            "the header's picture: one tile, a few portraits overlapping, never more than its most");
        C(EventCardMath.InvolvedMost >= EventCardMath.PictureMost && EventCardMath.CostLinesMost >= 1 && EventCardMath.RiskLinesMost >= 1, "more listed than pictured; a cost and a risk line at least");

        // ---------------- The plain letters kept ----------------

        var kept = EventCardMath.Keep(new[] { 9, 3, 3, 7, 1 }, new HashSet<int> { 1, 3, 9 });
        C(kept.SequenceEqual(new[] { 1, 3, 9 }), "only those still on the stack or in the History tab, each once, in order");
        C(EventCardMath.Keep(Enumerable.Range(1, 600), null, 400).SequenceEqual(Enumerable.Range(201, 400)), "the newest kept when there are too many");
        C(EventCardMath.Keep(null!, null).Count == 0 && EventCardMath.MaxPlain >= 200, "none kept from nothing; room for the History tab's letters");

        // ---------------- The seams ----------------

        var letters = new Dictionary<string, string>();
        var direct = new List<string>();
        foreach (var f in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(src, f).Replace('\\', '/');
            if (rel.StartsWith("bin/") || rel.StartsWith("obj/")) continue;
            string text = File.ReadAllText(f);
            foreach (Match m in Regex.Matches(text, @"class (\w+) : (ChoiceLetter|RimfolkLetter|StandardLetter|LetterWithTimeout|Letter)\b"))
            {
                if (m.Groups[2].Value == "RimfolkLetter") letters[m.Groups[1].Value] = rel;
                else if (m.Groups[1].Value != "RimfolkLetter") direct.Add(rel + ":" + m.Groups[1].Value);
            }
        }
        C(letters.Count >= 28, $"Rimfolk's choice letters open through its own override ({letters.Count})");
        C(direct.Count == 0, $"no letter class of Rimfolk's opens past it ({string.Join(", ", direct)})");
        string cards = Code("UI/EventCards/EventCards.cs");
        C(Regex.IsMatch(cards, @"public abstract class RimfolkLetter : ChoiceLetter\s*\{\s*public override void OpenLetter\(\)\s*\{\s*if \(EventCards\.TryOpen\(this\)\) return;\s*base\.OpenLetter\(\);"),
            "the override: the card, else the game's own window as before");
        C(Code("UI/News.cs").Contains("base.OpenLetter();") && Code("UI/News.cs").Contains("new Dialog_News(this)"), "the colony news keeps its own window, a card when it has no lines");
        C(cards.Contains("[HarmonyPatch(typeof(ChoiceLetter), nameof(ChoiceLetter.OpenLetter))]") && cards.Contains("__instance.GetType() != typeof(StandardLetter)")
          && cards.Contains("!RimfolkPatches.On") && cards.Contains("EventCards.IsRimfolks(__instance) && EventCards.TryOpen(__instance)"),
            "one patch, on the game's plain letter class only, for Rimfolk's plain letters only, off with the deep hooks");
        C(Regex.Matches(cards, @"\[HarmonyPatch").Count == 1 && !Directory.GetFiles(Path.Combine(src, "UI", "EventCards"), "*.cs").Any(f => !f.EndsWith("EventCards.cs") && File.ReadAllText(f).Contains("[HarmonyPatch")),
            "and no other patch of the game for the cards");
        C(cards.Contains("letter.def.modContentPack == ColonyCouncilMod.Pack") && cards.Contains("EventCardsBook.Instance?.Has(letter.ID)"),
            "a plain letter is Rimfolk's by its own def, or kept when the teller sent it");
        C(cards.Contains("WindowFrame.OwnLookOn && (ColonyCouncilMod.Settings?.eventCards ?? true)"), "the cards only with Rimfolk's own look and their setting on");
        C(cards.Contains("Scribe_Collections.Look(ref plain, \"plainLetters\", LookMode.Value);") && cards.Contains("Trim(Present());") && cards.Contains("EventCardMath.Keep("),
            "the plain letters kept by ID, let go when gone from the stack and the History tab");
        string tell = Code("UI/Tell.cs");
        C(Regex.IsMatch(tell, @"var letter = LetterMaker\.MakeLetter\(label \?\? """", body, def, look, faction\);\s*if \(letter == null\) return;\s*EventCards\.Mark\(letter\);\s*Find\.LetterStack\.ReceiveLetter\(letter\);"),
            "the teller's plain letters are the game's own, kept as Rimfolk's");
        // The window.
        string win = Code("UI/EventCards/EventCardWindow.cs");
        C(win.Contains("public class EventCardWindow : FramedWindow") && win.Contains("base(\"EventCard\")"), "the card is a framed window with its key");
        C(win.Contains("forcePause = true;") && win.Contains("absorbInputAroundWindow = true;") && win.Contains("SoundDefOf.CommsWindow_Open") && win.Contains("SoundDefOf.RadioComms_Ambience"),
            "it pauses, holds the map and sounds as the game's letter window does");
        C(win.Contains("class EventCardTree : Dialog_NodeTree") && win.Contains("public override void Close(bool doCloseSound = true)") && win.Contains("internal DiaNode Node => curNode;"),
            "the game's own letter window owns the options, never shown: closing it closes the card, its next page shows on it");
        C(win.Contains("AccessTools.Method(typeof(DiaOption), \"Activate\")") && win.Contains("o.dialog = tree;") && win.Contains("activate(o);"),
            "an option chosen runs the game's own Activate");
        C(win.Contains("o.hyperlink.ActivateHyperlink()") && win.Contains("protected override bool FrameClosable"), "links open their info card; the close button only when it can wait");
        C(Code("UI/WindowFrame.cs").Contains("protected virtual bool FrameClosable => true;") && Code("UI/WindowFrame.cs").Contains("toolsDrawer : null, FrameClosable);"),
            "a framed window may go without its close button");
        // Read once, drawn each frame.
        C(Regex.Matches(win, @"\.Choices\b").Count == 1 && Regex.IsMatch(win, @"private void Start\(ChoiceLetter l\)[^}]*l\.Choices"), "the letter's choices made once, when it opens");
        C(Regex.Matches(win, @"EventCardModel\.Build\(").Count == 1 && win.Contains("m.Page(next, false);"), "the card's model built when it opens, and again only for a page an option leads to");
        C(Regex.IsMatch(win, @"if \(Mathf\.Abs\(w - laidW\) > 0\.5f\) Measure\(w\);"), "the heights measured once a width");
        C(win.Split('\n').Where(l => l.Contains(".Translate(")).All(l => l.Contains("??=")), "the window's own words made once; the letter's in its model when it opens");
        foreach (var heavy in new[] { "Heart.ForView(", "needs?.mood", "DipWords.Stance(", ".Translate(TextUtil", "LookTargets", "Describe(" })
            C(!win.Contains(heavy), $"nothing read per frame in the window: {heavy}");
        string model = Code("UI/EventCards/EventCardModel.cs");
        C(model.Contains("Heart.ForView(p)") && model.Contains("HeartView.BandLabel(") && model.Contains("BreakThresholdMinor") && model.Contains("CameraJumper.CanJump("),
            "the mood, the heart's band and whether the camera can go to them, read when the card opens");
        C(win.Contains("UiKit.Portrait(") && win.Contains("UiKit.ItemPortrait(") && win.Contains("UiKit.Picture(") && win.Contains("HeartView.BandColor(") && win.Contains("HeartView.GoodColor"),
            "portraits from the game's cache, the thing's own picture, the people's emblem, the heart in the colonist bar's colours");
        // The answers' words from the impact.
        string impact = Code("Dilemmas/DilemmaImpact.cs");
        C(impact.Contains("public readonly string bare;") && impact.Contains("public readonly OptionImpact impact;") && impact.Contains("new DiaOption_Impact(label, Flavour(r, seed, answer), HintFull(r), r)"),
            "an answer keeps its own words and what it does, for the card");
        C(model.Contains("di.bare") && model.Contains("di.compact") && model.Contains("di.full") && model.Contains("ImpactHints.Rows(r, DilemmaImpact.Words)") && model.Contains("DilemmaImpact.Mode == ImpactShow.Exact"),
            "its card shows the line under it, the full list on hover, and its costs and risks in the setting's words");
        C(Code("Dilemmas/Answers.cs").Contains("internal static string TextOf(DiaOption o)"), "an option's words read as the answers' log reads them");
        // Who and what the letters tell their card.
        C(Code("Dilemmas/Dilemmas.cs").Contains("facts.Custom(ctx);") && Code("Ambitions/Ambitions.cs").Contains("EventCardMath.Lean(EventCardMath.Votes(voterChoice, options.Count))")
          && Code("Colonies/FoundingLetters.cs").Contains("EventCardMath.LeanForAgainst(plan.voted, plan.voteFor, plan.voteAgainst)"),
            "the colony's custom, the meeting's votes and the hand-over's vote: the only views the council has");
        C(Code("Happenings/Happenings.cs").Contains("public override void Describe(EventCardFacts facts) => facts.Context(ctx);")
          && Code("World/WorldSituation.cs").Contains("public override void Describe(EventCardFacts facts) => facts.Context(ctx);"), "events and the world's questions name who they are about");
        // The setting and its words.
        string settings = Code("CouncilSettings.cs");
        C(settings.Contains("public bool eventCards = true;") && settings.Contains("Scribe_Values.Look(ref eventCards, \"eventCards\", true);"), "the setting, on at first, saved");
        C(Code("UI/SettingsCatalog.cs").Contains("Toggle(\"eventCards\", \"CC_Settings_EventCards\", \"CC_SetNote_EventCards\", \"ownLook\");"), "on the settings page, under Rimfolk's own look");
        foreach (var k in new[] { "CC_Settings_EventCards", "CC_Settings_EventCards_Tip", "CC_SetNote_EventCards" }) C(keyed.ContainsKey(k), $"its words: {k}");
        foreach (var kv in keyed)
            C(!kv.Value.Contains("—") && !kv.Value.Contains("!") && !kv.Value.Contains("<b>"), $"{kv.Key}: plain words, no dash, no exclamation, no bold");
        // The rules are pure and linked.
        string math = File.ReadAllText(Path.Combine(src, "UI", "EventCards", "EventCardMath.cs"));
        C(!Regex.IsMatch(math, @"using (Verse|RimWorld|UnityEngine)"), "the rules have no game types");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj")).Contains("UI/EventCards/EventCardMath.cs"), "and are linked into the tests");
        return n;
    }
}
