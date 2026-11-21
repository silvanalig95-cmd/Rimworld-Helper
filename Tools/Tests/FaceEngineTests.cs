using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// The face engine and the large portrait at the inspect pane (the player's go, 20 November 2026: "a good face engine,
/// so to say, that does the face well and bigger already is probably a huge improvement", "the larger image of the pawn
/// can be in the bottom left corner, attached to the pawn default screen when you select it ... perhaps you can rescale
/// this window with drag and drop also"): framing by the head (a child, a grown-up, a big race and another mod's race
/// alike), the backdrop in their colour, the name band's words, the size and its drag, where it stands beside the pane,
/// an open tab and the gizmos on every screen, and the seams (every portrait through the engine, the steady face's
/// settings, the hooks, the hover card's corner).
/// </summary>
public static class FaceEngineTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "portraits: " + what);
        }
        bool Near(float a, float b, float eps = 0.001f) => Math.Abs(a - b) <= eps;
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";

        // ---------------- Framing by the head ----------------
        // A grown-up as the game draws one: the head's middle 0.34 above the pawn's, on a mesh 1.5 across.
        var grown = FaceMath.Frame(FaceFrame.Face, 0f, FaceMath.GrownHeadZ, FaceMath.HeadMesh, 40f, 40f);
        C(Near(grown.z, 0.28f, 0.01f) && Math.Abs(grown.zoom - 1.75f) < 0.1f, $"a grown-up's round portrait stays as it was before the engine (camera 0.28 up, zoom 1.75; now {grown.z:0.000}, {grown.zoom:0.000})");
        C(grown.x == 0f && grown.headU == 0.5f, "the front view: the head in the middle across");
        foreach (var frame in new[] { FaceFrame.Face, FaceFrame.Bust, FaceFrame.Figure })
        {
            var a = FaceMath.Frame(frame, 0f, 0.34f, 1.5f, 120f, 120f);
            // A child (a smaller head, lower), a big xenotype or race (larger, higher), a race whose head sits to one side.
            foreach (var (what, x, z, size) in new[] { ("a child", 0f, 0.22f, 1.15f), ("a toddler", 0f, 0.12f, 0.9f), ("a big race", 0f, 0.62f, 2.2f), ("a race's offset head", 0.12f, 0.4f, 1.6f) })
            {
                var b = FaceMath.Frame(frame, x, z, size, 120f, 120f);
                C(Near(a.headV, b.headV, 0.0005f) && Near(a.headShare, b.headShare, 0.001f), $"{frame}: {what} is framed as a grown-up is (the head at {b.headV:0.000} down, {b.headShare:0.000} of the height)");
                C(Near(b.zoom * size, a.zoom * 1.5f, 0.01f), $"{frame}: {what}'s zoom follows the size of the head");
                C(Near(b.x, x), $"{frame}: {what}: the camera across on the head");
            }
            // The same framing at every size: the head lands in the same place.
            var big = FaceMath.Frame(frame, 0f, 0.34f, 1.5f, 360f, 360f);
            C(Near(a.headV, big.headV) && Near(a.headShare, big.headShare) && Near(a.z, big.z, 0.0005f), $"{frame}: the same framing at 120 and 360 across");
        }
        var bust = FaceMath.Frame(FaceFrame.Bust, 0f, 0.34f, 1.5f, 192f, 192f);
        C(bust.headV > 0.35f && bust.headV < 0.48f, $"the bust: the face about two fifths down ({bust.headV:0.00})");
        float bustTop = bust.z + 1f / bust.zoom;
        C(bustTop >= 0.34f + 0.3f * 1.5f - 0.001f, $"the bust keeps the hair's room above the head ({bustTop:0.00})");
        // Bigger and centred (the player, 21 November 2026: "The pawn could be centered further, and be a bit bigger"):
        // closer than in 0.289.0 (1.05 head sizes across, the top 0.44 above the head), the head at least a fifth larger.
        var bustBefore = FaceMath.Frame(FaceFrame.Bust, 0f, 0.34f, 1.5f, 192f, 192f);
        C(FaceMath.BustSpan <= 0.85f && FaceMath.BustTop <= 0.34f && bust.headShare >= 1.2f * (1f / 1.05f) && bust.headV <= 0.42f,
            $"the bust bigger and centred: the head {bust.headShare:0.00} of the height (was {1f / 1.05f:0.00}), its middle {bust.headV:0.00} down");
        C(bust.z - 1f / bust.zoom < 0f, "the bust reaches below the shoulders to the chest");
        var face = FaceMath.Frame(FaceFrame.Face, 0f, 0.34f, 1.5f, 192f, 192f);
        C(face.headShare > bust.headShare, "the round portraits are closer on the face than the bust");
        // The colonist card's tile (100 by 140, less its frame): the whole of a grown-up, the hair to the feet.
        var fig = FaceMath.Frame(FaceFrame.Figure, 0f, 0.34f, 1.5f, 94f, 134f);
        float figTop = fig.z + 1f / fig.zoom, figFoot = fig.z - 1f / fig.zoom;
        C(figTop >= 0.34f + 0.4f * 1.5f - 0.001f && figFoot <= -0.6f, $"the card's figure: from above the hair ({figTop:0.00}) to below the feet ({figFoot:0.00})");
        C(Near(fig.headU, 0.5f) && fig.headV < 0.4f, "the card's figure: the head in its top part");
        // A drawing the engine can't read is framed as a grown-up's.
        var plain = FaceMath.Frame(FaceFrame.Face, 0f, FaceMath.GrownHeadZ, FaceMath.HeadMesh, 64f, 64f);
        foreach (var (what, x, z, size) in new[] { ("no size", 0f, 0.34f, 0f), ("a NaN", float.NaN, 0.34f, 1.5f), ("far too large", 0f, 0.34f, 40f), ("far off", 9f, 0.34f, 1.5f), ("an infinity", 0f, float.PositiveInfinity, 1.5f) })
        {
            var f = FaceMath.Frame(FaceFrame.Face, x, z, size, 64f, 64f);
            C(Near(f.z, plain.z) && Near(f.zoom, plain.zoom) && f.x == 0f, $"a head read as {what} is framed as a grown-up's");
        }
        var zero = FaceMath.Frame(FaceFrame.Bust, 0f, 0.34f, 1.5f, 0f, 0f);
        C(!float.IsNaN(zero.zoom) && !float.IsInfinity(zero.zoom) && zero.zoom > 0f, "a picture of no size gives no NaN");
        C(FaceMath.Plausible(0f, 0.34f, 1.5f) && FaceMath.Plausible(0.1f, 0.2f, 0.9f) && FaceMath.Plausible(0f, 0.9f, 3f), "the game's heads, a child's and a big race's are read");
        C(Near(FaceMath.FallbackHeadSize(1f), 1.5f) && Near(FaceMath.FallbackHeadSize(0.8f), 1.2f) && Near(FaceMath.FallbackHeadSize(0f), 1.5f) && Near(FaceMath.FallbackHeadSize(float.NaN), 1.5f),
            "the head's size before the game has drawn them: the mesh by the life stage's head size, else a grown-up's");
        // An animal or a mechanoid: its whole drawing.
        var husky = FaceMath.FrameBody(1.5f, 1.5f, 40f, 40f);
        var elephant = FaceMath.FrameBody(4.5f, 4.5f, 40f, 40f);
        C(husky.x == 0f && husky.z == 0f && Near(husky.zoom, 2f / (FaceMath.BodySpan * 1.5f), 0.001f), "an animal: its whole drawing in the middle");
        C(Near(elephant.zoom * 4.5f, husky.zoom * 1.5f, 0.01f), "an elephant fills the circle as a husky does");
        C(FaceMath.FrameBody(0f, 0f, 40f, 40f).zoom > 0f && FaceMath.FrameBody(float.NaN, 1f, 40f, 40f).zoom > 0f, "an animal's size unread: no NaN");
        C(FaceMath.RenderPixels(192f, 1f) == 240 && FaceMath.RenderPixels(192f, 2f) == 480, "rendered at the size shown, the game's supersampling and the interface scale on it (192 at 2x is 480 pixels)");

        // ---------------- Their colour behind them ----------------
        uint[] grounds = { SkinMath.Raised, 0x1f2124 };
        uint[] colours = { 0xffffff, 0xffff00, 0xff0000, 0x00ff00, 0x0000ff, 0xff88cc, 0x00ffff, 0x800080, 0x808080, 0x000000, 0xff8800, 0xd9c48a };
        foreach (var g in grounds)
        {
            foreach (var c in colours)
            {
                uint back = FaceMath.Backdrop(c, g), glow = FaceMath.Glow(c, g);
                C(SkinMath.Luminance(back) <= FaceMath.BackdropMost + 1e-6, $"#{c:x6} on #{g:x6}: the backdrop stays dark ({SkinMath.Luminance(back):0.000})");
                C(SkinMath.Luminance(glow) <= FaceMath.GlowMost + 1e-6, $"#{c:x6} on #{g:x6}: the light behind the head stays soft ({SkinMath.Luminance(glow):0.000})");
                if (c != 0x000000 && c != 0x808080) C(SkinMath.Luminance(glow) >= SkinMath.Luminance(back) - 1e-6, $"#{c:x6}: the light is brighter than the ground");
            }
            C(FaceMath.Backdrop(0xff0000, g) != FaceMath.Backdrop(0x0000ff, g) && FaceMath.Backdrop(0x00ff00, g) != FaceMath.Backdrop(0x0000ff, g), "two people's colours still tell apart");
            C(FaceMath.Backdrop(0xff0000, g) != g && FaceMath.Backdrop(0xffff00, g) != g, "a bright colour still shows (less of it)");
        }
        C(FaceMath.Mix(0x000000, 0xffffff, 0.5f) == 0x808080 && FaceMath.Mix(0x123456, 0xabcdef, 0f) == 0x123456 && FaceMath.Mix(0x123456, 0xabcdef, 1f) == 0xabcdef, "mixing colours");
        // The name band: its words read over any clothes, white to black, in either look.
        foreach (uint under in new uint[] { 0xffffff, 0xd0d0d0, 0x000000 })
        {
            uint band = FaceMath.Mix(under, SkinMath.WindowGround, FaceMath.BandAlpha);
            foreach (var (what, ink) in new[] { ("the name (Rimfolk's look)", SkinMath.Ink), ("the name (the game's look)", 0xffffffu), ("the earned name (Rimfolk's look)", SkinMath.Heading), ("the earned name (the game's look)", 0xf2d999u) })
                C(SkinMath.Contrast(ink, band) >= SkinMath.TextContrast, $"{what} over #{under:x6}: {SkinMath.Contrast(ink, band):0.0}:1");
        }

        // ---------------- The size and its drag ----------------
        C(FaceMath.SizeLeast == 96f && FaceMath.SizeMost == 360f && FaceMath.SizeFirst == 192f && FaceMath.SizeStep == 8f, "96 to 360, 192 at first, in steps of 8");
        C(FaceMath.Snap(192f) == 192f && FaceMath.Snap(50f) == 96f && FaceMath.Snap(1000f) == 360f && FaceMath.Snap(float.NaN) == 192f && FaceMath.Snap(203f) == 200f, "the size kept to its steps and range");
        for (float s = 0f; s <= 500f; s += 7f) C(FaceMath.Snap(s) % 8f == 0f && FaceMath.Snap(s) >= 96f && FaceMath.Snap(s) <= 360f, $"snapped {s}");
        C(FaceMath.Fit(200f, 1000f) == 200f && FaceMath.Fit(200f, 150f) == 144f && FaceMath.Fit(200f, 90f) == 0f && FaceMath.Fit(360f, 96f) == 96f && FaceMath.Fit(200f, float.NaN) == 0f,
            "the size fits the room there is, else none");
        C(FaceMath.Dragged(192f, 40f, 0f) == 232f && FaceMath.Dragged(192f, 0f, -40f) == 232f && FaceMath.Dragged(192f, 10f, -30f) == 224f, "dragging the top right corner right or up makes it larger");
        C(FaceMath.Dragged(192f, -40f, 40f) == 152f && FaceMath.Dragged(192f, -500f, 500f) == 96f && FaceMath.Dragged(192f, 900f, 0f) == 360f, "left or down smaller, never past the least or the most");

        // ---------------- Where it stands ----------------
        FaceMath.PaneScene Vanilla(float sw, float sh, float paneH = 165f, float paneTopY = float.NaN)
        {
            float y = sh - 35f - paneH;
            return new FaceMath.PaneScene
            {
                screenW = sw, screenH = sh, pane = new FrameRect(0f, y, 432f, paneH), paneTop = float.IsNaN(paneTopY) ? y : paneTopY, tabs = true, gizmoTop = sh - 35f - 80f
            };
        }
        var v = Vanilla(1920f, 1080f);
        C(FaceMath.Place(v, 192f, out var r) == FaceMath.Placed.OnPane && r.Near(new FrameRect(0f, 658f, 192f, 192f)),
            $"no tab open: on the tab buttons at the pane's left, 192 across ({r})");
        // Any tab open (Bio, Gear, Health, Log): it gives way to the tab (the player, 21 November 2026).
        var gear = v;
        gear.tabOpen = true;
        C(FaceMath.Place(gear, 192f, out r) == FaceMath.Placed.None && !Enum.GetNames(typeof(FaceMath.Placed)).Any(x => x.Contains("Tab")),
            "a tab open: not shown, never beside or above the tab");
        // RimHUD's taller pane, read from its window, whether or not it moves the tabs' top too.
        var hud = Vanilla(1920f, 1080f, 265f);
        C(FaceMath.Place(hud, 192f, out r) == FaceMath.Placed.OnPane && r.YMax == 780f - FaceMath.TabRowH, $"a taller pane (RimHUD): on its tab buttons ({r})");
        hud.paneTop = 880f;
        C(FaceMath.Place(hud, 192f, out r) == FaceMath.Placed.OnPane && r.YMax == 750f, "a taller pane whose tabs' top the game wasn't told: still above its window");
        var small = Vanilla(960f, 540f);
        C(FaceMath.Place(small, 360f, out r) == FaceMath.Placed.OnPane && r.y >= FaceMath.TopClear && r.w == 208f, $"interface scale 2 on 1080p: as large as fits below the colonist bar ({r})");
        // On every screen, with every tab and the gizmos as they come: never over the pane, its tab buttons, the open tab
        // or the gizmos, always on the screen below the room kept clear at its top, in whole steps no larger than asked.
        int placed = 0, waited = 0;
        foreach (var (sw, sh) in new[] { (1280f, 720f), (1920f, 1080f), (2560f, 1440f), (3840f, 2160f), (960f, 540f), (1366f, 768f) })
        foreach (float paneH in new[] { 165f, 265f })
        foreach (bool tabOpen in new[] { false, true })
        foreach (float gizmoRows in new[] { 0f, 1f, 3f, 5f })
        foreach (float size in new[] { 96f, 192f, 300f, 360f })
        {
            var s = Vanilla(sw, sh, paneH);
            s.gizmoTop = gizmoRows == 0f ? sh : sh - 35f - gizmoRows * 80f;
            float baseY = s.pane.y - FaceMath.TabRowH;
            s.tabOpen = tabOpen;
            var how = FaceMath.Place(s, size, out r);
            string at = $"{sw}x{sh}, pane {paneH}, a tab {(tabOpen ? "open" : "closed")}, {gizmoRows} gizmo rows, {size}";
            if (how == FaceMath.Placed.None)
            {
                waited++;
                C(s.tabOpen || baseY - FaceMath.TopClear < FaceMath.SizeLeast, $"{at}: only waits for a tab or a screen with no room");
                continue;
            }
            C(!s.tabOpen, $"{at}: never shown while a tab is open");
            placed++;
            var row = new FrameRect(s.pane.x, baseY, s.pane.w, FaceMath.TabRowH);
            var giz = new FrameRect(s.pane.XMax, s.gizmoTop, Math.Max(0f, sw - s.pane.XMax), Math.Max(0f, sh - s.gizmoTop));
            C(!FaceMath.Overlap(r, s.pane) && !FaceMath.Overlap(r, row), $"{at}: off the pane and its tab buttons ({r})");
            C(!FaceMath.Overlap(r, giz), $"{at}: off the gizmos ({r})");
            C(r.x >= 0f && r.XMax <= sw && r.y >= FaceMath.TopClear && r.YMax <= baseY, $"{at}: on the screen ({r})");
            C(r.w == r.h && r.w % FaceMath.SizeStep == 0f && r.w >= FaceMath.SizeLeast && r.w <= FaceMath.Snap(size), $"{at}: square, in steps, no larger than asked ({r})");
            C(how != FaceMath.Placed.OnPane || (r.x == s.pane.x && r.YMax == baseY), $"{at}: with no tab, standing on the tab buttons at the left");
        }
        C(placed > 150 && waited >= placed, $"placed {placed} times, waited {waited} times");

        // ---------------- The seams ----------------
        string engine = Read("UI/Faces/FaceEngine.cs"), inspect = Read("UI/Faces/InspectPortrait.cs"), kit = Read("UI/UiKit.cs"), card = Read("UI/ColonistCard/ColonistCardWindow.cs");
        C(engine.Contains("PortraitsCache.Get(p, size, RotFor(turn), new Vector3(cam.x, 0f, cam.z), cam.zoom, true, true, Headgear, Clothes)"),
            "the game's portrait cache, framed by the face, supersampled at the interface's scale, the steady face's headgear and clothes as set");
        C(engine.Contains("tree.TryGetNodeByTag(PawnRenderNodeTagDefOf.Head, out var node)") && engine.Contains("tree.TryGetMatrix(node, parms, out var m)") && engine.Contains("node.GetMesh(parms)"),
            "the head read from the game's own drawing: its node's matrix and mesh");
        C(engine.Contains("BaseHeadOffsetAt(rot)") && engine.Contains("Fallback(p, Rot4.South,") && engine.Contains("CurLifeStage?.headSizeFactor"), "until the game has drawn them: the body type's head offset and the life stage's head size");
        C(engine.Contains("story?.favoriteColor") && engine.Contains("f.def.FactionIcon") && engine.Contains("c = f.Color"), "their favourite colour, else their people's; the game's faction icon in its colour");
        C(engine.Contains("[HarmonyPatch(typeof(PortraitsCache), nameof(PortraitsCache.SetDirty))]") && engine.Contains("FaceEngine.Forget(pawn)"), "read again when the game marks their portrait dirty");
        C(kit.Contains("FaceEngine.DrawRound(inner, p, FaceEngine.PoseOf(p, turn, r.width >= FacePoseMath.PoseFrom), alpha)") && !kit.Contains("PortraitsCache.Get("), "the round portraits (letters, cards, lists) through the engine");
        C(card.Contains("FaceEngine.Draw(tile.ContractedBy(3f), pawn, FaceFrame.Figure, null, 1f, true)") && !card.Contains("PortraitsCache.Get("), "the colonist card's portrait through the engine, its tile's size kept");
        // Every portrait of a person in the mod goes through the engine; the founding window keeps the game's own pawn picker's look.
        foreach (var f in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(src, f).Replace('\\', '/');
            if (rel == "UI/Faces/FaceEngine.cs" || rel == "Colonies/NewcomersWindow.cs") continue;
            C(!File.ReadAllText(f).Contains("PortraitsCache.Get("), $"{rel}: no portrait of its own outside the engine");
        }
        C(inspect.Contains("[HarmonyPatch(typeof(MainTabWindow_Inspect), nameof(MainTabWindow_Inspect.ExtraOnGUI))]") && inspect.Contains("if (!RimfolkPatches.On) return;")
          && inspect.Contains("RimfolkPatches.On && (ColonyCouncilMod.Settings?.inspectPortrait ?? true)"), "the portrait at the inspect pane: a deeper game hook with its own setting");
        C(inspect.Contains("Find.WindowStack.ImmediateWindow(WindowId, rect, WindowLayer.GameUI, DrawAction, false, false, 0f)"),
            "a window the size of the portrait, taking no clicks around it");
        C(inspect.Contains("pane.windowRect") && inspect.Contains("pane.PaneTopY") && inspect.Contains("pane.OpenTabType") && inspect.Contains("pane.CurTabs")
          && !inspect.Contains("\"TabRect\"") && inspect.Contains("GizmoGridDrawer.HeightDrawnRecently"),
            "the pane, whether a tab is open, and the gizmos read as they are on the screen (no open tab's place needed any more)");
        C(inspect.Contains("sel.NumSelected != 1") && inspect.Contains("p.RaceProps.Humanlike"), "one person selected, anyone humanlike");
        C(inspect.Contains("FaceEngine.Draw(inner, p, FaceFrame.Bust, new Vector2(rs, rs), 1f, true)") && inspect.Contains("if (!dragging) render = rect.width;"),
            "drawn at its size, and while dragged the picture of the start is shown, no new one at every step");
        C(inspect.Contains("Input.GetMouseButton(0)") && inspect.Contains("s.inspectPortraitSize = FaceMath.Snap(now)") && inspect.Contains("s.Write()"), "let go, the size is kept in the settings");
        C(inspect.Contains("KnownAs.Short(p)") && inspect.Contains("Cards.Open(p)"), "the name the colony knows them by; a click opens their card");
        C(Read("Hud/HudHoverCard.cs").Contains("InspectPortrait.Showing(out var portrait)"), "the hover card in the corner keeps above the portrait");
        string settings = Read("CouncilSettings.cs"), catalog = Read("UI/SettingsCatalog.cs");
        C(settings.Contains("public bool inspectPortrait = true;") && settings.Contains("public float inspectPortraitSize = 192f;") && settings.Contains("public bool portraitHeadgear = false;")
          && settings.Contains("public bool portraitClothes = true;"), "the settings: the portrait on, 192, headgear off, clothes on");
        C(catalog.Contains("Slider(\"inspectPortraitSize\", \"CC_Settings_InspectPortraitSize\", \"CC_SetNote_InspectPortraitSize\", FaceMath.SizeLeast, FaceMath.SizeMost, FaceMath.SizeStep"),
            "the size's slider the drag's range and steps");
        var sec = SettingsCatalog.Find("inspectPortrait");
        C(sec != null && sec.page == SettingsPage.HowTold && sec.section == "CC_SetSec_Portraits" && SettingsCatalog.Find("portraitHeadgear")?.section == "CC_SetSec_Portraits"
          && SettingsCatalog.Find("inspectPortraitSize")?.Parent == "inspectPortrait", "the settings together under Portraits on How you're told");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Portraits.xml"));
        foreach (var k in new[] { "CC_Portrait_Tip", "CC_Portrait_GripTip", "CC_SetSec_Portraits" })
            C(keyed.Contains("<" + k + ">"), $"the words: {k}");
        var words = System.Xml.Linq.XDocument.Parse(keyed).Root!.Elements().Select(e => e.Value).ToList();
        C(words.Count >= 10 && words.All(w => !Regex.IsMatch(w, "[!\\u2014]")), "the words: no exclamation marks or em dashes");
        return n;
    }
}
