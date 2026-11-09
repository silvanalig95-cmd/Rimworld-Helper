using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// A colonist's card (docs/design/rimfolk-look.md, stage 3, point 4; the player, 9 November 2026: "colonist card would be
/// cool!"; Source/ColonyCouncil/UI/ColonistCard/). The pure rules first: its columns from its least width to a 4K screen and
/// which part goes where, how often its model is built, the arrows' step past cards already open, where a second card opens,
/// the heart's line under the portrait, the skills' bars and what a colonist is best at, the slot a worn piece goes in,
/// whether they are dressed for the season, and the foot's rows. Then the seams: every colonist's card in Rimfolk opens it,
/// the small card kept for everyone else, several open at once each in a frame of its own, the ways in from the colonist bar,
/// the inspect pane and the event cards that leave the game's own clicks alone, nothing worked out or translated while it is
/// drawn, what it reuses, the settings and the words.
/// </summary>
public static class ColonistCardTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the colonist card: " + what);
        }

        Columns(C);
        Rules(C);
        Seams(root, C);
        return n;
    }

    // ---------------- Columns, from the least to large ----------------

    private static void Columns(Action<bool, string> C)
    {
        C(ColonistCardMath.Columns(0f) == 1 && ColonistCardMath.Columns(float.NaN) == 1 && ColonistCardMath.Columns(-5f) == 1, "no width: one column");
        float least = ColonistCardMath.ColumnLeast, gap = ColonistCardMath.ColumnGap;
        for (int k = 1; k <= 4; k++)
        {
            float exact = k * least + (k - 1) * gap;
            C(ColonistCardMath.Columns(exact) == k, $"{k} columns from {exact:0} wide");
            if (k > 1) C(ColonistCardMath.Columns(exact - 1f) == k - 1, $"a hair short of {exact:0}: {k - 1}");
        }
        C(ColonistCardMath.Columns(10000f) == ColonistCardMath.MostColumns, "never more than four columns");
        for (float w = 100f; w <= 3600f; w += 37f)
        {
            int cols = ColonistCardMath.Columns(w);
            float cw = ColonistCardMath.ColumnWidth(w, cols);
            if (cols > 1) C(cw >= least - 0.01f, $"at {w:0}: {cols} columns each keep their least ({cw:0})");
            float right = ColonistCardMath.ColumnX(w, cols, cols - 1) + cw;
            C(Math.Abs(right - w) < 0.01f || cols == 1 && Math.Abs(cw - w) < 0.01f, $"at {w:0}: the columns fill the width exactly ({right:0})");
        }
        // Which part goes where: each once, in the order they are read, how they are now first.
        for (int cols = 0; cols <= 6; cols++)
        {
            var parts = ColonistCardMath.Parts(cols);
            var all = parts.SelectMany(c => c).ToList();
            C(all.Count == 4 && all.Distinct().Count() == 4, $"in {cols} columns every part shows once");
            C(parts.Length == Math.Max(1, Math.Min(4, cols)), $"in {cols} columns: {parts.Length} columns of parts");
            C(parts[0][0] == CardPart.Now, $"in {cols} columns how they are now comes first");
            var order = new[] { CardPart.Now, CardPart.Skills, CardPart.Kit, CardPart.Life };
            C(parts.All(c => c.SequenceEqual(c.OrderBy(p => Array.IndexOf(order, p)))), $"in {cols} columns each column reads in order");
        }
        C(ColonistCardMath.Parts(1)[0].SequenceEqual(new[] { CardPart.Now, CardPart.Skills, CardPart.Kit, CardPart.Life }), "narrow: stacked in their order");
        C(ColonistCardMath.Parts(2)[1].Contains(CardPart.Kit) && ColonistCardMath.Parts(2)[1].Contains(CardPart.Life), "two columns: their kit and life on the right");
        // The card at its sizes: stacked at its least, side by side as it first opens and made large.
        float BodyColumns(float windowW) => ColonistCardMath.Columns(WindowFrameMath.BodyWidth(windowW, true) - 16f);
        C(BodyColumns(ColonistCardMath.LeastW) == 1, "at its least the parts stack in one column");
        C(BodyColumns(ColonistCardMath.FirstW) == 4, "as it first opens, the four parts side by side");
        C(BodyColumns(1766f) == 4 && BodyColumns(3532f) == 4, "made large on 1920 and 4K: four columns");
        C(BodyColumns(800f) == 3 && BodyColumns(600f) == 2, "between, three and two");
        C(ColonistCardMath.ColumnWidth(WindowFrameMath.BodyWidth(ColonistCardMath.FirstW, true) - 16f, 4) >= 250f, "as it first opens each column is 250 wide or more");
        // The header and the foot at the least size.
        float bodyW = WindowFrameMath.BodyWidth(ColonistCardMath.LeastW, true), bodyH = WindowFrameMath.BodyHeight(ColonistCardMath.LeastH, true);
        C(bodyW - ColonistCardMath.PortraitW - 14f >= 260f, "at its least the header keeps 260 for the name, who they are and the chips beside the portrait");
        var footW = new List<float> { 64f, 64f, 64f, 64f, 92f, 64f, 100f, 64f };
        int footRows = ColonistCardMath.FootRows(footW, bodyW, ColonistCardMath.FootGap);
        C(footRows <= 2, $"at its least the foot's eight buttons take {footRows} rows");
        C(ColonistCardMath.FootRows(footW, WindowFrameMath.BodyWidth(ColonistCardMath.FirstW, true), ColonistCardMath.FootGap) == 1, "as it first opens the foot is one row");
        float foot = footRows * ColonistCardMath.FootButtonH + (footRows - 1) * ColonistCardMath.FootGap;
        C(bodyH - ColonistCardMath.HeaderH - 10f - foot - 8f >= 120f, $"at its least the parts keep {bodyH - ColonistCardMath.HeaderH - 10f - foot - 8f:0} to scroll in");
        C(ColonistCardMath.FootRows(new List<float>(), 300f, 6f) == 0 && ColonistCardMath.FootRows(new List<float> { 500f }, 300f, 6f) == 1, "no buttons: no rows; one too wide still takes one");
    }

    // ---------------- The rules ----------------

    private static void Rules(Action<bool, string> C)
    {
        // How often it is built.
        C(ColonistCardMath.LiveEvery == 0.5f && ColonistCardMath.HeavyEvery >= 2f && ColonistCardMath.HeavyEvery <= 5f, "the live parts twice a second, the heavy ones every few seconds");
        C(ColonistCardMath.Due(5f, -1f, 0.5f), "never built: due");
        C(!ColonistCardMath.Due(5f, 4.6f, 0.5f) && ColonistCardMath.Due(5f, 4.5f, 0.5f), "due once its time has passed, not before");
        C(ColonistCardMath.Due(5f, 9f, 3f), "a clock gone back: due");

        // The arrows.
        Func<int, bool> none = _ => false;
        C(ColonistCardMath.Step(0, 0, 1, none) == -1 && ColonistCardMath.Step(5, 1, 0, none) == -1, "nobody, or no way: nowhere to step");
        C(ColonistCardMath.Step(5, 1, 1, none) == 2 && ColonistCardMath.Step(5, 1, -1, none) == 0, "the next and the previous");
        C(ColonistCardMath.Step(5, 4, 1, none) == 0 && ColonistCardMath.Step(5, 0, -1, none) == 4, "round the ends");
        C(ColonistCardMath.Step(5, 1, 1, i => i == 2 || i == 3) == 4, "past colonists whose card is open already");
        C(ColonistCardMath.Step(5, 1, -1, i => i == 0) == 4, "past one open, round the end");
        C(ColonistCardMath.Step(3, 1, 1, i => i != 1) == -1, "everyone else's card open: none");
        C(ColonistCardMath.Step(1, 0, 1, none) == -1, "alone: none");
        C(ColonistCardMath.Step(4, -1, 1, none) == 0 && ColonistCardMath.Step(4, -1, -1, none) == 3, "someone not in the bar steps to the first or the last");
        bool everyOther = true;
        for (int count = 1; count <= 9; count++)
        for (int at = -1; at < count; at++)
        foreach (int dir in new[] { 1, -1 })
        {
            int i = ColonistCardMath.Step(count, at, dir, k => k % 3 == 0);
            if (i < -1 || i >= count || (i >= 0 && (i == at || i % 3 == 0))) everyOther = false;
        }
        C(everyOther, "never to themselves, never off the list, never to a card already open");

        // Several cards: each title bar shows, on any screen.
        var start = new FrameRect(200f, 120f, 1100f, 700f);
        C(ColonistCardMath.Cascaded(start, new List<FrameRect>(), 1920f, 1080f, 460f, 440f, 35f).Near(start), "the first opens where the last was left");
        var one = ColonistCardMath.Cascaded(start, new List<FrameRect> { start }, 1920f, 1080f, 460f, 440f, 35f);
        C(Math.Abs(one.x - start.x - ColonistCardMath.Cascade) < 0.5f && Math.Abs(one.y - start.y - ColonistCardMath.Cascade) < 0.5f, $"a second steps down and right by a title bar ({one})");
        var two = ColonistCardMath.Cascaded(start, new List<FrameRect> { start, one }, 1920f, 1080f, 460f, 440f, 35f);
        C(Math.Abs(two.y - start.y - 2f * ColonistCardMath.Cascade) < 0.5f, $"a third steps again ({two})");
        bool onScreen = true, apart = true;
        foreach (var (sw, sh) in new[] { (1024f, 768f), (1366f, 768f), (1920f, 1080f), (3840f, 2160f) })
        {
            var open = new List<FrameRect>();
            var place = new FrameRect(sw - 700f, sh - 600f, 600f, 500f);
            for (int k = 0; k < 8; k++)
            {
                var r = ColonistCardMath.Cascaded(place, open, sw, sh, 460f, 440f, 35f);
                if (r.x < 0f || r.y < 0f || r.XMax > sw + 0.5f || r.YMax > sh - 35f + 0.5f) onScreen = false;
                if (open.Any(o => Math.Abs(o.x - r.x) < 1f && Math.Abs(o.y - r.y) < 1f)) apart = false;
                open.Add(r);
            }
        }
        C(onScreen, "eight cards open on every screen, each on the screen above the bottom bar");
        C(apart, "eight cards open, no two at the same place");

        // The heart's line under the portrait, as the colonist bar draws it.
        C(ColonistCardMath.HeartLine(BarBadgeMath.HeartLineFrom - 0.5f, 100f) == 0f && ColonistCardMath.HeartLine(float.NaN, 100f) == 0f, "below the bar's threshold, nothing");
        C(Math.Abs(ColonistCardMath.HeartLine(50f, 100f) - 50f) < 0.01f && ColonistCardMath.HeartLine(150f, 100f) == 100f, "as long as it is full, out of 100");

        // Skills.
        C(ColonistCardMath.SkillFill(0) == 0f && Math.Abs(ColonistCardMath.SkillFill(10) - 0.5f) < 0.001f && ColonistCardMath.SkillFill(20) == 1f && ColonistCardMath.SkillFill(25) == 1f
          && ColonistCardMath.SkillFill(-3) == 0f, "a skill's bar is its level out of 20");
        var levels = new List<int> { 3, 14, 9, 14, 20, 12, 2 };
        var passions = new List<int> { 0, 1, 0, 2, 0, 0, 2 };
        var off = new List<bool> { false, false, false, false, true, false, false };
        var best = ColonistCardMath.Best(levels, passions, off);
        C(best.SequenceEqual(new[] { 3, 1, 5 }), $"best at: highest first, a burning passion before an interest at the same level, never one they can't do ({string.Join(",", best)})");
        C(ColonistCardMath.Best(new List<int> { 1, 2, 5 }, null, null).Count == 0, "nothing at 6 or more: no best");
        C(ColonistCardMath.Best(Enumerable.Repeat(15, 10).ToList(), null, null).SequenceEqual(new[] { 0, 1, 2 }), "at most three, the game's order between equals");
        C(ColonistCardMath.Best(null, null, null).Count == 0, "no skills: no best");

        // Kit.
        C(ColonistCardMath.SlotOf(true, true, true, true, true, true) == WornSlot.Belt, "a belt's layer first (a shield belt, a pack)");
        C(ColonistCardMath.SlotOf(false, true, true, false, false, false) == WornSlot.Body, "a duster or a suit is clothing for the body");
        C(ColonistCardMath.SlotOf(false, false, true, false, false, false) == WornSlot.Legs && ColonistCardMath.SlotOf(false, false, false, true, false, false) == WornSlot.Head,
            "trousers on the legs, a hat on the head");
        C(ColonistCardMath.SlotOf(false, true, false, true, false, false) == WornSlot.Body, "a suit with its helmet (covering both) is listed with the body");
        C(ColonistCardMath.SlotOf(false, false, false, false, true, false) == WornSlot.Hands && ColonistCardMath.SlotOf(false, false, false, false, false, true) == WornSlot.Feet
          && ColonistCardMath.SlotOf(false, false, false, false, false, false) == WornSlot.Other, "gloves, boots, the rest");
        C(ColonistCardMath.SlotOrder.Length == Enum.GetValues(typeof(WornSlot)).Length && ColonistCardMath.SlotOrder.Distinct().Count() == ColonistCardMath.SlotOrder.Length
          && ColonistCardMath.SlotOrder[0] == WornSlot.Head, "every slot listed once, head to feet");
        C(ColonistCardMath.Weather(-10f, 30f, -5f, 25f) == 0, "dressed for the season");
        C(ColonistCardMath.Weather(-10f, 30f, -20f, 25f) == -1, "the season's coldest below their least comfortable: too cold (no warm coat)");
        C(ColonistCardMath.Weather(-10f, 30f, -5f, 40f) == 1 && ColonistCardMath.Weather(-10f, 30f, -20f, 40f) == 2, "too hot; both");
        C(ColonistCardMath.Weather(-10f, 30f, -10.5f, 30.5f) == 0, "a degree's leeway either side");
    }

    // ---------------- The seams ----------------

    private static void Seams(string root, Action<bool, string> C)
    {
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => Regex.Replace(File.ReadAllText(Path.Combine(src, rel)), @"//[^\n]*", "");
        string window = Code("UI/ColonistCard/ColonistCardWindow.cs"), model = Code("UI/ColonistCard/ColonistCardModel.cs"), entry = Code("UI/ColonistCard/ColonistCardEntry.cs");
        string math = Code("UI/ColonistCard/ColonistCardMath.cs"), cards = Code("UI/Cards/Cards.cs"), frame = Code("UI/WindowFrame.cs");

        // Every colonist's card in Rimfolk opens it; everyone else keeps the small card.
        int takes = cards.IndexOf("ColonistCardWindow.Takes(r, out var colonist)", StringComparison.Ordinal), drawer = cards.IndexOf("var host = Drawer.Active;", StringComparison.Ordinal);
        C(takes > 0 && drawer > takes && cards.Contains("ColonistCardWindow.Open(colonist);"), "a colonist's name or portrait anywhere in Rimfolk (Cards.Open) opens their colonist card");
        C(window.Contains("ThingRefs.CardOf(r) != CardKind.Colonist") && window.Contains("p.Faction != null && p.Faction.IsPlayer") && window.Contains("!p.Dead")
          && window.Contains("public static bool On => ColonyCouncilMod.Settings?.colonistCard ?? true;"),
            "only the colony's own living colonists and slaves, with the setting on; prisoners, the dead and everyone else keep the small card");
        C(cards.Contains("MainTabWindow_Council.OpenPerson(pawn)") && Code("UI/MainTabWindow_Council.cs").Contains("Drawer.Open(RimfolkWindow.Council, ThingRefs.Of(p));"),
            "their whole story beside the People table still opens in the drawer");
        C(cards.Contains("m.Act(T(\"CC_CCard_Open\"), null, () => ColonistCardWindow.Open(pawn));"), "the small card (the drawer's) leads to the colonist card");
        C(window.Contains("case FootAct.WholeStory:") && window.Contains("MainTabWindow_Council.OpenPerson(p);"), "the card leads to their whole story");

        // Several at once, each in a frame of its own.
        C(window.Contains("onlyOneOfTypeAllowed = false;") && window.Contains(": base(\"ColonistCard\", true)"), "several cards open at once, each in a frame of its own");
        C(FramedWindowsMath.TryKey("ColonistCard", out var key) && key.leastW == ColonistCardMath.LeastW && key.leastH == ColonistCardMath.LeastH && key.keepsBar,
            "its key and least size in the frame's table; it stands beside play, above the bottom bar");
        C(frame.Contains("public static WindowFrame Own(") && frame.Contains("foreach (var f in owned) f.known = false;") && frame.Contains("owned.Remove(this);")
          && Code("UI/WindowFrame.cs").Contains("frame = ownFrame ? WindowFrame.Own("), "a frame of its own moves and sizes alone; where the last was left is kept under the key, forgotten with the rest");
        C(window.Contains("ColonistCardMath.Cascaded(") && window.Contains("public override void PostOpen()"), "a second card opens a title bar down from the first");
        C(window.Contains("Find.WindowStack.Notify_ClickedInsideWindow(open);") && window.Contains("if (windows[i] is ColonistCardWindow c && c.pawn == p)"),
            "one card per colonist: opening it again brings it to the front");
        C(window.Contains("ColonistCardMath.Step(") || model.Contains("ColonistCardMath.Step("), "the arrows step through the colonist bar's order");
        C(model.Contains("Find.ColonistBar?.GetColonistsInOrder()") || window.Contains("Find.ColonistBar?.GetColonistsInOrder()"), "in the colonist bar's own order");
        C(window.Contains("if (!Fits(p) || p == pawn || For(p) != null) return;"), "never to a colonist whose card is open in another window");

        // The ways in from the game's own interface, leaving its clicks alone.
        string bar = Between(entry, "public static void BarClick(", "private static string paneTip;");
        C(entry.Contains("[HarmonyPatch(typeof(ColonistBarColonistDrawer), nameof(ColonistBarColonistDrawer.HandleClicks))]") && entry.Contains("public static void Prefix(Rect rect, Pawn colonist)"),
            "the colonist bar: a prefix on the game's click handling that never skips it");
        C(bar.Contains("ev.type != EventType.MouseDown") && bar.Contains("ev.button != 0") && bar.Contains("!(ev.control || ev.command)") && bar.Contains("ev.Use();"),
            "only a left press with Ctrl is taken; a click, a double-click, Shift and the right-drag stay the game's");
        C(entry.Contains("[HarmonyPatch(typeof(MainTabWindow_Inspect), nameof(MainTabWindow_Inspect.DoInspectPaneButtons))]") && entry.Contains("public static void Postfix(Rect rect, ref float lineEndWidth)")
          && entry.Contains("lineEndWidth += 24f;") && entry.Contains("rect.width - lineEndWidth - 24f"), "the inspect pane: a button left of the game's own, the name giving it room");
        C(entry.Contains("Find.Selector.NumSelected != 1") && entry.Contains("ColonistCardWindow.Fits(p)"), "only for one colonist selected alone");
        C(Code("UI/EventCards/EventCardWindow.cs").Contains("ColonistCardEntry.RowButton(r, p.pawn);"), "an event card's \"who is involved\" has the card's button on each colonist");
        C(entry.Contains("RimfolkPatches.On && ColonistCardWindow.On && (ColonyCouncilMod.Settings?.colonistCardBar ?? true)")
          && entry.Contains("RimfolkPatches.On && ColonistCardWindow.On && (ColonyCouncilMod.Settings?.colonistCardPane ?? true)"), "each way in has its own setting and needs the deeper hooks");

        // Built rarely, drawn only from the model.
        C(model.Contains("ColonistCardMath.Due(now, heavyAt, ColonistCardMath.HeavyEvery)") && model.Contains("ColonistCardMath.Due(now, liveAt, ColonistCardMath.LiveEvery)"),
            "the heavy parts every few seconds, the live parts twice a second");
        C(model.Contains("staled != StoryView.Staled") && Code("UI/ITab_Pawn_Story.cs").Contains("Staled++;"), "a promise on the card builds its life again at once");
        C(window.Contains("if (Fits(pawn)) m.Refresh(pawn);"), "the model is made before the window draws, once a frame at most");
        string drawing = Between(window, "protected override void DoFramedContents(", "private static class Words");
        C(!Regex.IsMatch(drawing, @"\.Translate\("), "nothing translated while it is drawn");
        C(!Regex.IsMatch(drawing, @"MoodInsight\.|WeaponLive\.Of\(|ApparelLive\.Of\(|StoryView\.Glance\(|WeaponPick\.Standing\(|ForColonist\(|History\.|OpinionOf\(|GetStatValue\("),
            "nothing worked out while it is drawn: the mood, ratings, kit, life and weather are the model's");

        // What it reuses.
        C(model.Contains("MoodInsight.MoodOf(p)") && model.Contains("MoodGlance.For(p, mood)") && window.Contains("MoodView.DrawBar(") && window.Contains("m.glance.Draw(x, y, w)"),
            "Now: the Mood tab's bar with its break lines and its glance (the heart's meters with the day off, what weighs and lifts most, the bands' effects)");
        C(model.Contains("StoryView.Glance(comp, pawn, false)") && window.Contains("m.story.Draw(x, y, w)"), "Life: the Story tab's glance (life goal, wish with Show me and Promise, quirks, standing, friends and rivals)");
        C(model.Contains("StoryView.Who(comp, p)"), "who they are in the Story tab's one line");
        C(model.Contains("WeaponLive.ForColonist(p, r)") && model.Contains("WeaponLive.CantUse(p, t, r)") && model.Contains("ApparelLive.ForColonist(p, r, a)")
          && window.Contains("WeaponUi.Badge(") && window.Contains("UiKit.ItemPortrait("), "Kit: each item's picture, its badge and its rating for them, as the gear tab and the cards rate it");
        C(model.Contains("WeaponMods.OffHand(p)") && model.Contains("WeaponMods.Carried(p)") && model.Contains("WeaponMods.Remembered(p, t)"), "the weapon in hand, the off hand and the sidearms carried");
        C(model.Contains("WeaponPick.Standing(p)") && model.Contains("picks.Kit(p)") && model.Contains("WeaponPick.DoConfirmed(picks, pick)"), "the kit's gaps and better weapons, with the gear tab's acts");
        C(model.Contains("ApparelLive.Climate(p)") && model.Contains("StatDefOf.ComfyTemperatureMin") && model.Contains("ColonistCardMath.Weather("), "whether they are dressed for the season (no warm coat)");
        C(window.Contains("WeaponCardWindow.Open(t, pawn)") && window.Contains("ApparelCardWindow.Open(t, pawn)") && window.Contains("new Dialog_InfoCard(t)"),
            "a click on an item opens its card: Rimfolk's where it rates it, else the game's");
        C(model.Contains("public static void KitLines(string id, Func<Pawn, IEnumerable<ColonistCardLine>> lines)"), "a hook for the apparel suggestions to come");
        C(model.Contains("p.skills?.skills") && model.Contains("s.TotallyDisabled") && model.Contains("People.PassionLevel(s)") && model.Contains("SkillUI.PassionMajorIcon")
          && model.Contains("\"VSE.Passions.PassionManager\""), "the game's own skills and passions, Vanilla Skills Expanded's marks where it is loaded");
        C(window.Contains("PortraitsCache.Get(p, ") && window.Contains("ColonistCardMath.HeartLine("), "the portrait from the game's cache, the heart's line under it");
        C(model.Contains("HeartView.BandColor(band)") && model.Contains("HeartView.GoodColor"), "in the colonist bar's heart line colours");
        C(model.Contains("CardSections.registry.Of(CardKind.Colonist)") && model.Contains("CardActs.registry.In(CardKind.Colonist, CardSlot.Acts)")
          && model.Contains("\"heart-good\", \"heart-dayoff\", \"knownas\""), "what other parts add to a colonist's card (a ward, a ransom, a match) is kept, less what the card shows itself");
        C(model.Contains("ModsConfig.RoyaltyActive ? p.royalty?.MostSeniorTitle") && model.Contains("ModsConfig.IdeologyActive && p.Ideo != null"),
            "the Empire's title and their role in the ideoligion, each only with its DLC");
        C(window.Contains("typeof(ITab_Pawn_Mood)") && window.Contains("typeof(ITab_Pawn_Story)") && window.Contains("typeof(ITab_Pawn_Gear)") && window.Contains("typeof(ITab_Pawn_Health)")
          && window.Contains("InspectPaneUtility.OpenTab(tab)") && window.Contains("CameraJumper.TryJump(p)") && window.Contains("CameraJumper.TryJumpAndSelect(p)"),
            "the foot: the game's tabs, going to them and selecting them");
        C(!Regex.IsMatch(window + model + entry, @"Widgets\.ButtonText\("), "drawn with the kit, in Rimfolk's look or the game's");
        C(!Regex.IsMatch(math, @"using (Verse|RimWorld|UnityEngine)"), "its rules free of game types, tried here");

        // The settings.
        string settings = Code("CouncilSettings.cs"), catalog = Code("UI/SettingsCatalog.cs");
        foreach (var s in new[] { "colonistCard", "colonistCardBar", "colonistCardPane" })
            C(settings.Contains("public bool " + s + " = true;") && settings.Contains("Scribe_Values.Look(ref " + s + ", \"" + s + "\", true);") && catalog.Contains("Toggle(\"" + s + "\""),
                $"the setting {s}: on at first, kept, on the settings page");
        C(catalog.Contains("\"colonistCard,deepHooks\""), "the colonist bar's and the inspect pane's ways in need the card and the deeper hooks");

        // The words.
        var file = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_ColonistCard.xml")).Root!.Elements().ToList();
        var keyed = file.ToDictionary(e => e.Name.LocalName, e => e.Value);
        string all = window + model + entry + cards + catalog;
        var used = Regex.Matches(all, "\"(CC_CCard_[A-Za-z0-9_]+)\"").Select(m => m.Groups[1].Value).Where(k => !k.EndsWith("_")).ToHashSet();
        foreach (var slot in Enum.GetNames(typeof(WornSlot))) used.Add("CC_CCard_Slot_" + slot);
        foreach (var k in used.Where(k => k.StartsWith("CC_CCard_Foot_")).ToList()) used.Add(k + "_Tip");
        foreach (var s in new[] { "ColonistCard", "ColonistCardBar", "ColonistCardPane" })
        {
            used.Add("CC_Settings_" + s);
            used.Add("CC_Settings_" + s + "_Tip");
            used.Add("CC_SetNote_" + s);
        }
        foreach (var k in used) C(keyed.ContainsKey(k), "keyed: " + k);
        foreach (var e in file) C(used.Contains(e.Name.LocalName), "used: " + e.Name.LocalName);
        C(file.All(e => !e.Value.Contains('—') && !e.Value.Contains('!')), "no em dashes or exclamation marks");
        C(model.Contains("\"CC_CCard_Slot_\" + slot") && model.Contains("FootKeys[i] + \"_Tip\""), "the slots' and the foot's words made from their names");
    }

    private static string Between(string text, string from, string to)
    {
        int a = text.IndexOf(from, StringComparison.Ordinal);
        if (a < 0) return "";
        int b = text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
        return b < 0 ? text.Substring(a) : text.Substring(a, b - a);
    }
}
