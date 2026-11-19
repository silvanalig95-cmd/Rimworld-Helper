using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Everything from a god told at once, and the mood saying what and whose (the player, 17 November 2026, with their save
/// League_of_Toba_6: Revel's stash of green tea moved the camera but its words waited for the evening's news, and the mood
/// "my god was good to us" named neither god nor deed; "any information from a god should come with its own dedicated pop
/// up on the right"; "if they don't know who it was, then, they should guess or say they don't know").
/// </summary>
public static class GodWordsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "a god's words: " + what);
        }

        // ---------- The pure parts ----------
        C(GodTouchMath.Why("Lorena found green tea x8 hidden far out on the land.", "Bautista holds it was Undertow's doing.")
          == "Lorena found green tea x8 hidden far out on the land. Bautista holds it was Undertow's doing.", "what was seen, then whose doing they hold it was");
        C(GodTouchMath.Why("A ring of mushrooms came up", "Webb doesn't know which god it was.") == "A ring of mushrooms came up. Webb doesn't know which god it was.",
            "words without an end get a full stop");
        C(GodTouchMath.Why(null, "Webb doesn't know which god it was.") == "Webb doesn't know which god it was." && GodTouchMath.Why("Frost.", null) == "Frost."
          && GodTouchMath.Why(null, null) == null, "either alone, or nothing");
        C(GodLoreMath.WithoutStop("Lorena found green tea x8.") == "Lorena found green tea x8" && GodLoreMath.WithoutStop("They waited...") == "They waited..."
          && GodLoreMath.WithoutStop("Who left it?") == "Who left it?" && GodLoreMath.WithoutStop(null) == null, "the book's line never ends in two full stops");

        // ---------- At once, as a message ----------
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string tell = File.ReadAllText(Path.Combine(src, "UI", "Tell.cs"));
        C(tell.Contains("public static void FromGod(NewsKind kind, Map map, string text, LookTargets look, MessageTypeDef type = null) =>\n            Message(kind, Importance.Notable, map, text, look, type);"),
            "a god's words are a message as it happens, at least notable");
        int off = tell.IndexOf("case NewsMode.Off:", StringComparison.Ordinal);
        C(off > 0 && tell.IndexOf("if (ByCamera(map, look)) break;", off, StringComparison.Ordinal) > off, "with the colony news off, a camera's words still come with it");
        string gods = Path.Combine(src, "Gods");
        C(File.ReadAllText(Path.Combine(gods, "OnMap", "OwnSeven.cs")).Contains("Tell.FromGod(NewsKind.Land, map, text, look);"), "every god's own happening's line");
        foreach (var (file, what) in new[] { ("Worship/Worship.cs", "a god's sign"), ("Worship/WorshipCouncil.cs", "a god keeping the rain away"),
                     ("Worship/Offerings.cs", "an offering taken or left"), ("Worship/GodDreams.cs", "a god's demand ignored") })
            C(File.ReadAllText(Path.Combine(gods, file)).Contains("Tell.FromGod("), what + " is told at once");
        foreach (var dir in new[] { "OnMap", "Touch", "Presence" })
            foreach (var f in Directory.GetFiles(Path.Combine(gods, dir), "*.cs"))
            {
                string code = File.ReadAllText(f);
                if (code.Contains("Tell.Minor(")) C(false, Path.GetFileName(f) + " hands a god's words to the evening's news");
                // The camera first, then the words, as everywhere else (the stash and the vision had it the other way).
                if (Regex.IsMatch(code, @"OwnHappenings\.Line\([^\n]*\n\s*GodPresence\.Look\("))
                    C(false, Path.GetFileName(f) + " hands over the words before the camera moves");
            }
        string revel = File.ReadAllText(Path.Combine(gods, "OnMap", "OwnRevel.cs"));
        C(revel.Contains("GodPresence.Look(new LookTargets(placed));\n            OwnHappenings.Line(map, text, new LookTargets(placed));"), "the stash: the camera, then the words");
        C(revel.Contains("\"CC_GodRevel_Saw_Stash\", placed.Label.Named(\"WHAT\")"), "the stash's book line in lower case mid-sentence");

        // ---------- The mood says what and whose ----------
        string touch = File.ReadAllText(Path.Combine(gods, "Touch", "GodTouch.cs"));
        C(touch.Contains("CouncilGameComponent.GiveThought(x, def, stage: stage, cause: WhyFelt(x, p));"), "the mood's Why");
        C(touch.Contains("string name = p.taken != null ? GodLore.NameOf(p.taken, x) : null;") && !touch.Contains("Gods.Name(p.taken)") && !touch.Contains("Gods.Name(p.god)"),
            "the god they took it for, by their own name for it; never the true god's name");
        C(touch.Contains("act.text);") && touch.Contains("GodMarkMath.WithLeft(spec.text, spec.left));"),
            "the words of the act or the moment carried to the mood (a moment's with what it left after them)");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_GodTouch.xml"));
        C(keyed.Contains("<CC_GodTouch_Why_Guess>{PAWN_nameDef} holds it was {GOD}'s doing.</CC_GodTouch_Why_Guess>")
          && keyed.Contains("<CC_GodTouch_Why_Unknown>{PAWN_nameDef} doesn't know which god it was.</CC_GodTouch_Why_Unknown>"), "a guess, or that they don't know");
        C(File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_GodHappenings_Revel.xml")).Contains("hidden at {PAWN_possessive} feet"),
            "the stash's words fit one person");
        C(File.ReadAllText(Path.Combine(src, "Gods", "GodLore.cs")).Contains("words = GodLoreMath.WithoutStop(words);"), "the book's lines take the words' own full stop off");
        return n;
    }
}
