using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The Story tab at a glance (docs/design/rimfolk-look.md, stage 3, point 2; the player, 9 November 2026: "story tab
/// sounds great", to "at a glance, life goal with a progress bar, current wish, quirks, standing in the colony, closest
/// friends and rivals; the full history below in folding sections"). The pure rules first (who is a close friend or a
/// rival and in what order, how many portraits fit, the trust meter, the rows at every width from a narrow card to a wide
/// window), then the seams in the code: what the glance shows and from what, the sections in their order and remembered,
/// built twice a second at most and nothing translated while drawn, every piece the tab showed before still there, and
/// the words.
/// </summary>
public static class StoryGlanceTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the Story tab at a glance: " + what);
        }

        Rules(C);
        Seams(root, C);
        return n;
    }

    private static void Rules(Action<bool, string> C)
    {
        // Friends and rivals: the game's own lines, and Rimfolk's bonds as well.
        C(StoryGlanceMath.FriendAt == 20f && StoryGlanceMath.RivalAt == -20f, "a friend from 20, a rival from -20, the game's own lines");
        var plain = new StoryGlanceMath.Other(1, 20f);
        C(StoryGlanceMath.IsFriend(plain) && !StoryGlanceMath.IsRival(plain), "an opinion of 20 is a friend");
        C(!StoryGlanceMath.IsFriend(new StoryGlanceMath.Other(1, 19f)) && !StoryGlanceMath.IsRival(new StoryGlanceMath.Other(1, 19f)), "19 is neither");
        C(StoryGlanceMath.IsRival(new StoryGlanceMath.Other(1, -20f)) && !StoryGlanceMath.IsRival(new StoryGlanceMath.Other(1, -19f)), "-20 is a rival, -19 isn't");
        C(StoryGlanceMath.IsFriend(new StoryGlanceMath.Other(1, 5f, friendTie: true)), "bound as friends at +5: a friend");
        C(!StoryGlanceMath.IsFriend(new StoryGlanceMath.Other(1, -5f, friendTie: true)), "bound as friends but against them now: not a friend");
        C(StoryGlanceMath.IsRival(new StoryGlanceMath.Other(1, -5f, rivalTie: true)) && !StoryGlanceMath.IsRival(new StoryGlanceMath.Other(1, 5f, rivalTie: true)),
            "bound as rivals at -5: a rival; at +5, not");
        C(StoryGlanceMath.IsFriend(new StoryGlanceMath.Other(1, 30f, rivalTie: true)), "an opinion past the friend line wins over an old rivalry");
        C(StoryGlanceMath.IsRival(new StoryGlanceMath.Other(1, -30f, friendTie: true)) && !StoryGlanceMath.IsFriend(new StoryGlanceMath.Other(1, -30f, friendTie: true)),
            "an opinion past the rival line wins over a friendship");
        bool never = true;
        for (float o = -100f; o <= 100f; o += 2.5f)
            foreach (bool f in new[] { false, true })
                foreach (bool r in new[] { false, true })
                {
                    var x = new StoryGlanceMath.Other(1, o, f, r);
                    if (StoryGlanceMath.IsFriend(x) && StoryGlanceMath.IsRival(x)) never = false;
                }
        C(never, "never both a friend and a rival, at any opinion and any bond");

        var others = new List<StoryGlanceMath.Other>
        {
            new StoryGlanceMath.Other(10, 45f), new StoryGlanceMath.Other(11, -60f), new StoryGlanceMath.Other(12, 45f, true, false, 70f),
            new StoryGlanceMath.Other(13, 5f), new StoryGlanceMath.Other(14, -25f), new StoryGlanceMath.Other(15, 80f), new StoryGlanceMath.Other(16, 8f, true, false, 30f),
            new StoryGlanceMath.Other(17, -2f, false, true, 50f), new StoryGlanceMath.Other(9, 45f)
        };
        var friends = StoryGlanceMath.Friends(others).Select(i => others[i].id).ToList();
        var rivals = StoryGlanceMath.Rivals(others).Select(i => others[i].id).ToList();
        C(friends.SequenceEqual(new[] { 15, 12, 9, 10, 16 }), "friends closest first: by opinion, then the bond, then the longest in the game (" + string.Join(",", friends) + ")");
        C(rivals.SequenceEqual(new[] { 11, 14, 17 }), "rivals the worst first, a bond of rivalry at -2 last (" + string.Join(",", rivals) + ")");
        C(!friends.Contains(13) && !rivals.Contains(13), "+5 with no bond is neither");
        C(StoryGlanceMath.Friends(new List<StoryGlanceMath.Other>()).Count == 0 && StoryGlanceMath.Friends(null).Count == 0, "nobody: no friends");
        C(StoryGlanceMath.Shown == 3, "the glance names three friends and three rivals at most");

        // How many portraits fit in a row.
        var w3 = new List<float> { 100f, 100f, 100f };
        C(StoryGlanceMath.Fits(312f, w3, 6f, 20f) == 3, "three that fit exactly need no count");
        C(StoryGlanceMath.Fits(311f, w3, 6f, 20f) == 2, "a hair short: two, and room for \"+1\"");
        var w4 = new List<float> { 100f, 100f, 100f, 100f };
        C(StoryGlanceMath.Fits(1000f, w4, 6f, 20f) == 3, "never more than three, the rest counted");
        C(StoryGlanceMath.Fits(330f, w4, 6f, 20f) == 2, "three would leave no room for the count of the rest: two");
        C(StoryGlanceMath.Fits(0f, w3, 6f, 20f) == 0 && StoryGlanceMath.Fits(100f, new List<float>(), 6f, 20f) == 0, "no room or nobody: none");
        C(StoryGlanceMath.Fits(100f, new List<float> { 100f }, 6f, 20f) == 1, "one alone that fits needs no count");
        for (float room = 0f; room <= 600f; room += 13f)
        {
            int fit = StoryGlanceMath.Fits(room, w4, 6f, 20f);
            float used = fit * 100f + Math.Max(0, fit - 1) * 6f + (fit < w4.Count ? 6f + 20f : 0f);
            C(fit == 0 || used <= room + 0.01f, $"at {room}: the portraits and the count stay in the row");
        }

        // Trust's meter: its words change where PawnStanding.LevelFor says.
        C(StoryGlanceMath.TrustTicks.SequenceEqual(new[] { -45f, -15f, 25f, 60f }), "trust's ticks at -45, -15, 25 and 60");
        C(StoryGlanceMath.TrustFill(-100f) == 0f && StoryGlanceMath.TrustFill(100f) == 1f && Math.Abs(StoryGlanceMath.TrustFill(0f) - 0.5f) < 0.0001f,
            "trust from -100 at the left to 100 at the right, neutral in the middle");
        C(StoryGlanceMath.TrustFill(-300f) == 0f && StoryGlanceMath.TrustFill(300f) == 1f, "kept on the bar");

        // The rows at every width, from a narrow card to a wide window.
        for (float w = 200f; w <= 900f; w += 17f)
        {
            var (bar, words) = StoryGlanceMath.GoalLine(w, 140f);
            C(bar >= Math.Min(StoryGlanceMath.MinBar, w - StoryGlanceMath.IconW) - 0.01f && StoryGlanceMath.IconW + bar + (words > 0f ? 6f + words : 0f) <= w + 0.01f,
                $"the life goal's bar and its progress fit at {w}");
            var (wordsT, barT) = StoryGlanceMath.TrustLine(w, 130f);
            C(StoryGlanceMath.IconW + wordsT + 8f + barT <= w + 0.01f && barT >= Math.Min(StoryGlanceMath.MinBar, w - StoryGlanceMath.IconW - 8f) - 0.01f,
                $"trust's words and bar fit at {w}, the bar kept");
            var (under, text) = StoryGlanceMath.WishLine(w, 118f);
            C(under ? text <= w - StoryGlanceMath.IconW + 0.01f : StoryGlanceMath.IconW + text + 6f + 118f <= w + 0.01f && text >= 180f - 0.01f,
                $"the wish's acts beside its words or under them at {w}");
        }
        // The inspect pane's width: the Story tab is 460 wide, its scroll view 424 inside.
        C(!StoryGlanceMath.WishLine(424f, 118f).under, "at the Story tab's width the wish's acts sit beside it");
        C(StoryGlanceMath.WishLine(300f, 118f).under, "in a narrow card they go under it");
        C(StoryGlanceMath.Fits(424f - StoryGlanceMath.IconW - StoryGlanceMath.LabelW, new List<float> { 110f, 110f, 110f }, 6f, 20f) == 3,
            "three friends with short names fit at the Story tab's width");
    }

    private static void Seams(string root, Action<bool, string> C)
    {
        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string tab = Code("UI/ITab_Pawn_Story.cs"), glance = Code("UI/StoryGlance.cs"), culture = Code("UI/ITab_Pawn_Culture.cs");
        var keyed = Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements())
            .GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value);

        // Who they are in one line, under their name.
        C(tab.Contains("UiCache.Get(p, 41, () => StoryView.Who(comp, p), 2f)") && tab.Contains("UiKit.LabelFit(line, who.line, UiKit.HeaderColor, GameFont.Tiny);"),
            "who they are in one line under the name, kept a couple of seconds");
        string who = Between(tab, "public static WhoLine Who(", "private static string Origin(");
        C(who.Contains("comp.epithets.NameOf(p)") && who.Contains("OfficeTracker.HeldBy(p)") && who.Contains("p.story?.TitleShortCap") && who.Contains("\"CC_People_Age\"")
          && who.Contains("Origin(comp, p,"), "the name they're known by, their office or title, their age and where they come from");
        string origin = Between(tab, "private static string Origin(", "public static void Draw(");
        C(origin.Contains("IsFounder(p)") && origin.Contains("\"CC_SGl_BornHere\"") && origin.Contains("CultureView.RecruitedBy(p, out _)") && origin.Contains("defaultFactionDef")
          && culture.Contains("internal static string RecruitedBy("), "where they come from as the Culture tab tells it: a founder, born here, won over, the people they lived among");

        // The glance: built from the story's model, drawn first.
        C(tab.Contains("cache.sections = Build(comp, p, parts, socialElsewhere, out cache.glance);") && tab.Contains("time - cache.at > 0.5f"),
            "the glance is built with the story's model, twice a second at most");
        int glanceAt = tab.IndexOf("y += glance.Draw(0f, y, view.width);", StringComparison.Ordinal), sectionsAt = tab.IndexOf("foreach (var section in sections)", StringComparison.Ordinal);
        C(glanceAt > 0 && sectionsAt > glanceAt, "the glance at the top, the sections under it");
        string build = Between(tab, "private static List<Section> Build(", "private static void WishLines(");
        C(build.Contains("g.BuildGoal(comp, p);") && build.Contains("g.story = ArcView.Now(") && build.Contains("g.BuildWish(comp, p);") && build.Contains("g.tags.Add(")
          && build.Contains("g.BuildPeople(comp, p);") && build.Contains("g.BuildRecent(p);") && build.Contains("g.words = words.lines[0];"),
            "it has their character in words, life goal, story, wish, quirks, standing, friends and rivals and the newest line");
        string draw = Between(glance, "public float Draw(", "// ---------------- Words, translated once");
        C(!Regex.IsMatch(draw, @"\.Translate\("), "nothing translated while the glance is drawn");
        C(!Regex.IsMatch(draw, @"History\.|OpinionOf\(|comp\.wishes|aspirations\.Active"), "nothing worked out while drawn: only the model is read");
        C(draw.Contains("UiKit.Bar(new Rect(tx, y + LineH + 6f, bar, 7f), goalFill, UiKit.Gold)") && draw.Contains("StoryGlanceMath.GoalLine("),
            "the life goal with a progress bar and its progress in words");
        C(glance.Contains("goalHeld = \"CC_SGl_Held\"") && glance.Contains("a.def.Worker.ProgressText(a)"), "how long they've held it, and what's next in its own words");
        C(glance.Contains("\"CC_SGl_Waiting\"") && glance.Contains("wsh.Worker.FulfilledWhen(wsh)") && glance.Contains("MainTabWindow_Council.Jump(w0.Worker.LookAt(w0))")
          && glance.Contains("CouncilGameComponent.Instance?.Promise(w0);") && glance.Contains("promiseWhyNot = wsh.promised ?"),
            "the wish: how long they've waited, what fulfils it, and the wish cards' own acts (show me, promise; greyed once promised)");
        C(!Regex.IsMatch(glance, @"TicksLeft|expiresTick"), "no time left on the wish in words: answers play out on the map");
        C(glance.Contains("QuirkEffects") || build.Contains("QuirkEffects.Tip(q)"), "each quirk's tag says what it does in its tooltip");
        C(draw.Contains("UiKit.Pills(x, y, w, tags)"), "quirks as tags in the kit's chips");
        C(glance.Contains("comp.standings.For(p, false)") && draw.Contains("StoryGlanceMath.TrustFill(trust)") && draw.Contains("StoryGlanceMath.TrustTicks")
          && glance.Contains("OfficeTracker.HeldBy(p)") && glance.Contains("\"CC_SGl_LikedBy\""), "standing: trust on a meter with its bands, the office, how the colony regards them");
        C(glance.Contains("p.relations.OpinionOf(o)") && glance.Contains("StoryGlanceMath.Friends(others)") && glance.Contains("StoryGlanceMath.Rivals(others)")
          && glance.Contains("PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists"), "friends and rivals from the game's opinions and Rimfolk's bonds, among the colony's people");
        C(draw.Contains("UiKit.Portrait(new Rect(r.x + 1f, r.y + 1f, Face, Face), o.pawn,") && Code("UI/UiKit.cs").Contains("PortraitsCache.Get("),
            "small portraits, through the game's portrait cache");
        C(glance.Contains("if (select && (who.pawn.Spawned || who.pawn.GetCaravan() != null)) CameraJumper.TryJumpAndSelect(who.pawn);") && glance.Contains("else Cards.Open(who.pawn);"),
            "a click selects them in the Story tab (as the game's Social tab does), opens their card elsewhere");
        C(glance.Contains("SocialColumn.PairNote(subject, who.pawn)") && glance.Contains("TooltipHandler.TipRegion(r, () =>"), "Rimfolk's line about the two only when hovered");
        C(glance.Contains("History.Of(Refs.Of(p), null, int.MinValue, 8)") && glance.Contains("e.folded") && draw.Contains("LinkText.Line("),
            "the newest line of their history, names as links, a season's summary passed over");
        C(glance.Contains("UiKit.RowAct(") && glance.Contains("UiKit.RowHover(") && !Regex.IsMatch(Regex.Replace(glance, @"//[^\n]*", ""), @"Widgets\.(ButtonText|DrawHighlight)"),
            "drawn with the kit, in the game's look and Rimfolk's own");

        // The sections, in order of use, folding and remembered.
        C(tab.Contains("Folds.Remembered(\"story.sections\", StoryView.FoldedInTab)"), "the tab's sections fold and are remembered between sessions");
        var folded = Regex.Match(tab, @"FoldedInTab =\s*\{([^}]*)\}").Groups[1].Value;
        C(!folded.Contains("\"story\"") && !folded.Contains("\"wishes\"") && folded.Contains("\"life\"") && folded.Contains("\"people\"") && folded.Contains("\"journal\""),
            "their story and their wishes open at first, the rest folded under the glance");
        string[] order = { "key = \"story\"", "key = \"wishes\"", "key = \"life\"", "list.Add(quirks)", "key = \"people\"", "key = \"family\"", "key = \"dreams\"", "key = \"character\" + cs.order",
            "key = \"years\"", "key = \"secrets\"", "key = \"journal\"" };
        int last = -1;
        bool inOrder = true;
        foreach (var o in order)
        {
            int at = build.IndexOf(o, StringComparison.Ordinal);
            if (at <= last) inOrder = false;
            last = at;
        }
        C(inOrder, "story, wishes, life goals, quirks, friends and rivals, family, dreams, character, the years, secrets, journal");
        C(build.Contains("History.Of(who, null, int.MinValue, HistoryShown)") && build.Contains("WorldHistoryPage.ShowFor(who, Ref.None)"), "their story: the history, and the way to the whole of it");
        C(build.Contains("History.Of(who, WishKinds, int.MinValue, WishesShown)") && build.Contains("w.Involves(p)"), "every wish: those before the council and what came of the earlier ones");
        C(build.Contains("comp.aspirations.Fulfilled(p)") && build.Contains("GoalRewards.MarksOf(p)") && build.Contains("g.goalWhy"), "life goals: the one under way in full, those reached, the knacks");
        C(build.Contains("QuirkEffects.Effects(q)") && build.Contains("QuirkEffects.Lasts(q)"), "quirks in full: what each does and how long it lasts");
        C(build.Contains("friends = g.friends, rivals = g.rivals") && tab.Contains("StoryGlance.Clickable(rr, p, o, select);"), "every friend and rival in their section, each clickable");
        // Everything the tab showed before is still there.
        foreach (var piece in new[] { "comp.legacy.heirlooms", "comp.vendettas.vendettas", "comp.nemeses.Of(p)", "HealthTracker.StoryTag(p", "ArcView.Past(p", "Family.Tree(p, out int further)",
                     "FamilyNames.StoryLine(p)", "FamilyLife.NamedAfterLine(p)", "FamilyYears.StoryLines(p)", "comp.social.Describe(p)", "comp.social.lost", "comp.secrets.Of(p)",
                     "comp.dreams.recent", "comp.journals.For(p)", "new Dialog_Journal(j)", "CharacterView.Sections(p)" })
            C(tab.Contains(piece) || glance.Contains(piece), "still shown: " + piece);
        C(!tab.Contains("DrawOverview(") && !tab.Contains("class Overview"), "the old overview gave way to the glance");

        // The words.
        var used = Regex.Matches(tab + glance, "\"(CC_SGl_[A-Za-z0-9_]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
        var file = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_StoryGlance.xml")).Root!.Elements().ToList();
        C(used.All(keyed.ContainsKey), "every word the glance says is keyed");
        C(file.All(e => used.Contains(e.Name.LocalName)), "every keyed word of the glance is used");
        C(file.All(e => !e.Value.Contains('—') && !e.Value.Contains('!')), "no em dashes or exclamation marks");
    }

    private static string Between(string text, string from, string to)
    {
        int a = text.IndexOf(from, StringComparison.Ordinal);
        if (a < 0) return "";
        int b = text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
        return b < 0 ? text.Substring(a) : text.Substring(a, b - a);
    }
}
