using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// The region in four views (Source/ColonyCouncil/UI/RegionViewsMath.cs, docs/design/interface.md, UI 2): six lenses at
/// most, each mark's tone under each lens, the lines each lens draws and how (a pact solid, a truce dashed, a favour owed
/// dotted, protection double, a camp's raids), the ledger's ties as lines, the dashes along a line, and the order of each
/// lens's List; then that the views draw what the old pages drew, and what the window, the map and the lists are made of.
/// </summary>
public static class RegionViewsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "region views: " + what);
        }

        // ---------------- The lenses ----------------
        C(RegionViewsMath.Lenses.Length == 6 && RegionViewsMath.Lenses.Length <= RegionViewsMath.MaxLenses, "six lenses, and no more than six");
        C(RegionViewsMath.Lenses.SequenceEqual(new[] { Lens.Land, Lens.Stance, Lens.Fortunes, Lens.Threats, Lens.Trade, Lens.Ours }),
            "land, stance and ties, fortunes, threats, trade, ours, in that order");
        C(Enum.GetValues(typeof(Lens)).Length == RegionViewsMath.Lenses.Length, "every lens is on the bar");
        C(RegionViewsMath.TryLens("trade", out var tl) && tl == Lens.Trade && !RegionViewsMath.TryLens("Roads", out _) && !RegionViewsMath.TryLens(null, out _), "a lens by its name, in any case");

        // ---------------- Stance ----------------
        C(RegionViewsMath.StanceOf(RegionMapMath.Standing.Allies) == Stance.Warm && RegionViewsMath.StanceOf(RegionMapMath.Standing.Friendly) == Stance.Warm,
            "allies and friends are warm");
        C(RegionViewsMath.StanceOf(RegionMapMath.Standing.Fair) == Stance.Cool && RegionViewsMath.StanceOf(RegionMapMath.Standing.Unfriendly) == Stance.Cool
          && RegionViewsMath.StanceOf(RegionMapMath.Standing.NoDealings) == Stance.Cool, "fair, cool, unfriendly and no dealings are cool");
        C(RegionViewsMath.StanceOf(RegionMapMath.Standing.Hostile) == Stance.Sour && RegionViewsMath.StanceOf(RegionMapMath.Standing.Enemies) == Stance.Sour, "hostile and enemies are sour");
        C(RegionViewsMath.StanceOf(RegionMapMath.Standing.Own) == Stance.Ours && RegionViewsMath.ToneOf(Stance.Ours) == Hue.Gold, "ours is gold");

        // ---------------- Marks ----------------
        PlaceFacts P(PlaceKind k, Stance s = Stance.Cool, int fortune = -1, bool threatened = false, bool trades = false, bool ev = false) =>
            new PlaceFacts { kind = k, stance = s, fortune = fortune, threatened = threatened, trades = trades, eventHere = ev };
        foreach (PlaceKind k in Enum.GetValues(typeof(PlaceKind)))
            C(RegionViewsMath.MarkTone(Lens.Land, P(k)) == Hue.Plain, $"the land keeps a {k}'s own colours");
        C(RegionViewsMath.MarkTone(Lens.Stance, P(PlaceKind.Town, Stance.Warm)) == Hue.Warm && RegionViewsMath.MarkTone(Lens.Stance, P(PlaceKind.Town, Stance.Sour)) == Hue.Sour
          && RegionViewsMath.MarkTone(Lens.Stance, P(PlaceKind.Post, Stance.Cool)) == Hue.Cool, "stance: towns and posts by their people's stance");
        C(RegionViewsMath.MarkTone(Lens.Stance, P(PlaceKind.Home)) == Hue.Gold && RegionViewsMath.MarkTone(Lens.Stance, P(PlaceKind.Caravan)) == Hue.Dim, "stance: homes gold, the rest dim");
        C(RegionViewsMath.MarkTone(Lens.Fortunes, P(PlaceKind.Town, fortune: (int)Fortune.Struggling)) == Hue.Sour
          && RegionViewsMath.MarkTone(Lens.Fortunes, P(PlaceKind.Town, fortune: (int)Fortune.Poor)) == Hue.Soon
          && RegionViewsMath.MarkTone(Lens.Fortunes, P(PlaceKind.Town, fortune: (int)Fortune.Steady)) == Hue.Cool
          && RegionViewsMath.MarkTone(Lens.Fortunes, P(PlaceKind.Town, fortune: (int)Fortune.Thriving)) == Hue.Warm
          && RegionViewsMath.MarkTone(Lens.Fortunes, P(PlaceKind.Town, fortune: (int)Fortune.Flourishing)) == Hue.Warm, "fortunes: from struggling (sour) to flourishing (warm)");
        C(RegionViewsMath.MarkTone(Lens.Fortunes, P(PlaceKind.Town)) == Hue.Dim && RegionViewsMath.MarkTone(Lens.Fortunes, P(PlaceKind.Town, ev: true)) == Hue.World,
          "fortunes: a town with no fortune kept is dim, unless a world event runs there");
        C(RegionViewsMath.MarkTone(Lens.Fortunes, P(PlaceKind.Ruin)) == Hue.Cool && RegionViewsMath.MarkTone(Lens.Fortunes, P(PlaceKind.Building)) == Hue.Cool, "fortunes: ruins and towns being built");
        C(RegionViewsMath.MarkTone(Lens.Threats, P(PlaceKind.Town, Stance.Sour)) == Hue.Sour && RegionViewsMath.MarkTone(Lens.Threats, P(PlaceKind.Town, Stance.Warm)) == Hue.Dim
          && RegionViewsMath.MarkTone(Lens.Threats, P(PlaceKind.Camp)) == Hue.Sour, "threats: hostile towns and camps, nothing else");
        C(RegionViewsMath.MarkTone(Lens.Threats, P(PlaceKind.Home, threatened: true)) == Hue.Sour && RegionViewsMath.MarkTone(Lens.Threats, P(PlaceKind.Home)) == Hue.Gold,
          "threats: a home with a threat now is sour");
        C(RegionViewsMath.MarkTone(Lens.Trade, P(PlaceKind.Post)) == Hue.Warm && RegionViewsMath.MarkTone(Lens.Trade, P(PlaceKind.Town, trades: true)) == Hue.Cool
          && RegionViewsMath.MarkTone(Lens.Trade, P(PlaceKind.Town)) == Hue.Dim, "trade: posts, then towns that trade");
        C(RegionViewsMath.MarkTone(Lens.Ours, P(PlaceKind.Hamlet)) == Hue.Gold && RegionViewsMath.MarkTone(Lens.Ours, P(PlaceKind.Caravan)) == Hue.Gold
          && RegionViewsMath.MarkTone(Lens.Ours, P(PlaceKind.Town, Stance.Warm)) == Hue.Dim, "ours: homes, hamlets and caravans");
        C(RegionViewsMath.MovedThisSeason(1, 100, 200, 1000) == 1 && RegionViewsMath.MovedThisSeason(-1, 100, 200, 1000) == -1, "a fortune that moved this season shows its arrow");
        C(RegionViewsMath.MovedThisSeason(1, 100, 2000, 1000) == 0 && RegionViewsMath.MovedThisSeason(0, 100, 200, 1000) == 0 && RegionViewsMath.MovedThisSeason(1, -1, 200, 1000) == 0,
          "no arrow for an older move, no move, or no cause");

        // ---------------- Lines ----------------
        C(RegionViewsMath.Look(LineKind.Pact).dash == Dash.Solid && RegionViewsMath.Look(LineKind.Truce).dash == Dash.Dashed
          && RegionViewsMath.Look(LineKind.FavourOwedUs).dash == Dash.Dotted && RegionViewsMath.Look(LineKind.FavourWeOwe).dash == Dash.Dotted
          && RegionViewsMath.Look(LineKind.Fealty).dash == Dash.Double, "a pact solid, a truce dashed, a favour owed dotted, protection double");
        C(RegionViewsMath.Look(LineKind.Raids).tone == Hue.Sour && RegionViewsMath.Look(LineKind.Grudge).tone == Hue.Sour, "a camp's raids and a grudge are sour");
        C(RegionViewsMath.Look(LineKind.FavourOwedUs).tone == Hue.Warm && RegionViewsMath.Look(LineKind.FavourWeOwe).tone == Hue.Soon, "owed to the colony warm, owed by it soon");
        foreach (LineKind k in Enum.GetValues(typeof(LineKind)))
        {
            C(RegionViewsMath.Look(k).width > 0f, $"a {k} line has a width");
            C(RegionViewsMath.Lenses.Any(l => RegionViewsMath.Draws(l, k)), $"some lens draws a {k} line");
        }
        C(RegionViewsMath.LinesOf(Lens.Land).Count == 0 && RegionViewsMath.LinesOf(Lens.Fortunes).Count == 0, "the land and the fortunes draw no lines");
        C(RegionViewsMath.LinesOf(Lens.Stance).SequenceEqual(new[] { LineKind.Pact, LineKind.Truce, LineKind.FavourOwedUs, LineKind.FavourWeOwe, LineKind.Fealty, LineKind.Grudge }),
          "stance and ties draws the ties: pacts, truces, favours each way, protection, grudges");
        C(RegionViewsMath.Draws(Lens.Threats, LineKind.Raids) && RegionViewsMath.Draws(Lens.Trade, LineKind.Post) && RegionViewsMath.Draws(Lens.Ours, LineKind.Holding)
          && !RegionViewsMath.Draws(Lens.Trade, LineKind.Raids), "threats the raids, trade the posts, ours the holdings");
        C(RegionViewsMath.TieLine(Tie.Kind.Pact, false) == LineKind.Pact && RegionViewsMath.TieLine(Tie.Kind.Truce, true) == LineKind.Truce, "a pact and a truce, either way");
        C(RegionViewsMath.TieLine(Tie.Kind.Favour, false) == LineKind.FavourOwedUs && RegionViewsMath.TieLine(Tie.Kind.Favour, true) == LineKind.FavourWeOwe, "a favour by who owes it");
        C(RegionViewsMath.TieLine(Tie.Kind.Fealty, true) == LineKind.Fealty && RegionViewsMath.TieLine(Tie.Kind.Fealty, false) == LineKind.Fealty, "fealty either way is protection");
        C(RegionViewsMath.TieLine(Tie.Kind.Grudge, false) == LineKind.Grudge && RegionViewsMath.TieLine(Tie.Kind.Grudge, true) == null, "a grudge held against the colony; one it holds is a colonist's own");
        C(RegionViewsMath.TieLine(Tie.Kind.Regard, false) == null && RegionViewsMath.TieLine(Tie.Kind.Claim, false) == null && RegionViewsMath.TieLine(Tie.Kind.Promise, true) == null,
          "regard, claims and promises draw no line");
        // Dashes: solid and double are one stroke; the rest repeat to the end and never past it.
        C(RegionViewsMath.DashAt(Dash.Solid, 100f, 0, out float s0, out float e0) && s0 == 0f && e0 == 100f && !RegionViewsMath.DashAt(Dash.Solid, 100f, 1, out _, out _), "a solid line is one stroke");
        foreach (var d in new[] { Dash.Dashed, Dash.Dotted, Dash.Short })
        {
            int count = 0;
            float last = -1f;
            bool inside = true, ordered = true;
            for (int i = 0; RegionViewsMath.DashAt(d, 100f, i, out float s, out float e); i++)
            {
                count++;
                inside &= s >= 0f && e <= 100f && e > s;
                ordered &= s > last;
                last = s;
            }
            C(count > 3 && inside && ordered, $"a {d} line repeats along its length and stays on it ({count})");
        }
        C(!RegionViewsMath.DashAt(Dash.Dashed, 0f, 0, out _, out _) && !RegionViewsMath.DashAt(Dash.Dashed, 10f, -1, out _, out _), "no length, no dash");

        // ---------------- The List ----------------
        C(RegionViewsMath.Group(Lens.Threats, RegionViewsMath.RowKind.Camp) < RegionViewsMath.Group(Lens.Threats, RegionViewsMath.RowKind.Fight)
          && RegionViewsMath.Group(Lens.Threats, RegionViewsMath.RowKind.Fight) < RegionViewsMath.Group(Lens.Threats, RegionViewsMath.RowKind.Town),
          "threats: camps, then fights on their way, then hostile towns");
        C(RegionViewsMath.Group(Lens.Trade, RegionViewsMath.RowKind.Post) < RegionViewsMath.Group(Lens.Trade, RegionViewsMath.RowKind.Town)
          && RegionViewsMath.Group(Lens.Trade, RegionViewsMath.RowKind.Camp) < 0, "trade: posts, then towns; no camps");
        C(RegionViewsMath.Group(Lens.Ours, RegionViewsMath.RowKind.Home) < RegionViewsMath.Group(Lens.Ours, RegionViewsMath.RowKind.Hamlet)
          && RegionViewsMath.Group(Lens.Ours, RegionViewsMath.RowKind.Hamlet) < RegionViewsMath.Group(Lens.Ours, RegionViewsMath.RowKind.Caravan)
          && RegionViewsMath.Group(Lens.Ours, RegionViewsMath.RowKind.Town) < 0, "ours: homes, hamlets, caravans; no one else's towns");
        C(RegionViewsMath.Group(Lens.Stance, RegionViewsMath.RowKind.Town) == 0 && RegionViewsMath.Group(Lens.Stance, RegionViewsMath.RowKind.Caravan) < 0
          && RegionViewsMath.Group(Lens.Land, RegionViewsMath.RowKind.Town) == 0, "land and stance list the towns");
        C(RegionViewsMath.Group(Lens.Fortunes, RegionViewsMath.RowKind.Ruin) > RegionViewsMath.Group(Lens.Fortunes, RegionViewsMath.RowKind.Town), "fortunes: towns, then ruins and towns being built");
        var near = new RegionViewsMath.RowOrder(0, 0f, 3f, "Brassgate");
        var far = new RegionViewsMath.RowOrder(0, 0f, 9f, "Ashford");
        C(RegionViewsMath.Compare(near, far) < 0, "nearest first");
        C(RegionViewsMath.Compare(new RegionViewsMath.RowOrder(0, 0f, 3f, "B"), new RegionViewsMath.RowOrder(0, 0f, 3f, "A")) > 0, "then by name");
        C(RegionViewsMath.Compare(new RegionViewsMath.RowOrder(1, 0f, 1f, "A"), new RegionViewsMath.RowOrder(0, 5f, 30f, "Z")) > 0, "the group first");
        float struggling = RegionViewsMath.Key(Lens.Fortunes, RegionViewsMath.RowKind.Town, (int)Fortune.Struggling, 0, 0f, false);
        float steadyMoved = RegionViewsMath.Key(Lens.Fortunes, RegionViewsMath.RowKind.Town, (int)Fortune.Steady, -1, 0f, false);
        float steady = RegionViewsMath.Key(Lens.Fortunes, RegionViewsMath.RowKind.Town, (int)Fortune.Steady, 0, 0f, false);
        float none = RegionViewsMath.Key(Lens.Fortunes, RegionViewsMath.RowKind.Town, -1, 0, 0f, false);
        C(struggling < steadyMoved && steadyMoved < steady && steady < none, "fortunes from struggling to flourishing, a town that moved before its band, no fortune last");
        C(RegionViewsMath.Key(Lens.Threats, RegionViewsMath.RowKind.Fight, -1, 0, 2f, false) < RegionViewsMath.Key(Lens.Threats, RegionViewsMath.RowKind.Fight, -1, 0, 5f, false),
          "the fight that comes first is first");
        C(RegionViewsMath.Key(Lens.Ours, RegionViewsMath.RowKind.Home, -1, 0, 0f, true) < RegionViewsMath.Key(Lens.Ours, RegionViewsMath.RowKind.Home, -1, 0, 0f, false), "the seat before other homes");

        // ---------------- The views in the game ----------------
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string window = Code("UI/MainTabWindow_World.cs");
        C(Regex.Matches(window, @"Own\(RegionView\.\w+").Count == 4 && !window.Contains("Own(\"Neighbours\""), "the window builds four views and no old page");
        C(window.Contains("PagesMath.TryAddress(name, out var a)") && window.Contains("RegionView.Apply(a)"), "an address sets the view's lens and mode");
        C(window.Contains("Nav.Observe(RimfolkWindow.World, current.name, RegionView.Within(current.name)"), "a lens or a mode chosen is a step of the way back");
        C(Code("UI/Cards/Nav.cs").Contains("Pages.Open(s.mode == null ? s.page : s.page + \"/\" + s.mode)"), "going back puts the lens and the mode back");
        C(Code("UI/Pages.cs").Contains("MainTabWindow_World.Select(name);"), "Pages.Open passes the whole address to the region window");
        string map = Code("UI/RegionMapView.cs");
        C(map.Contains("UiKit.DashedLine(") && map.Contains("lens.colours.TryGetValue(m.obj.ID") && map.Contains("UiKit.LegendStrip("),
            "the map draws the lens's lines, its marks' colours and its legend at its foot each frame");
        C(Regex.Matches(map, @"RegionMap\.Build\(").Count == 1 && !Code("UI/Region/LensModel.cs").Contains("RegionMap.Build("), "the land's picture is made once, never per lens");
        string model = Code("UI/Region/LensModel.cs");
        C(model.Contains("now - builtAt < 1f") && model.Contains("WorldLedger.LiveTies(k)") && model.Contains("WorldLedger.Today()"),
            "a lens's model at most once a second, its ties from the ledger");
        string mapView = Code("UI/Region/MapView.cs");
        C(mapView.Contains("Drawer.Open(RimfolkWindow.World, row.thing, true)") && mapView.Contains("Cards.ShowOnMap(row.place)") && mapView.Contains("y + RowH >= scroll.y"),
            "every row opens its card in the drawer, Show picks it on the map, only the rows in sight are drawn");
        C(Code("UI/Region/PeoplesView.cs").Contains("LettersPage.KnownSection(") && Code("UI/Region/PeoplesView.cs").Contains("WorldPanel.OutThereSection(y, w, comp, OutGroup.Foes"),
            "Faces: the people known by name, old foes, those who left");
        string away = Code("UI/Region/HereAndAwayView.cs");
        C(away.Contains("CampPanel.Section(") && away.Contains("WorldPanel.ComingSection(") && away.Contains("LettersPage.UnderWaySection(") && away.Contains("MissionPanel.Draw("),
            "Here and away: the camp, what's coming, letters on the road, missions");
        string kit = Code("UI/UiKitLenses.cs");
        foreach (var part in new[] { "public static int Modes(Rect r", "public static int LensBar(Rect r", "public static float LegendStrip(Rect r", "public static void DashedLine(Vector2 a" })
            C(kit.Contains(part), $"the component {part.Split('(')[0].Split(' ').Last()} takes a rect and plain data, for any window");
        // The views' headers are the one view header (UI/UiKitViews.cs), with Peoples' modes and every view's "?".
        foreach (var view in new[] { "UI/Region/PeoplesView.cs", "UI/Region/HereAndAwayView.cs", "UI/WorldPages.cs" })
            C(Code(view).Contains("UiKit.ViewHeader(") && Code(view).Contains("ViewHeadModel") && Code(view).Contains("PagesMath.ArticleOf(RegionView."),
                $"{view}: the view's header with its \"?\"");
        C(Code("UI/Region/MapView.cs").Contains("UiKit.HelpButton(") && Code("UI/Region/PeoplesView.cs").Contains("m.labels.AddRange(RegionView.PeopleModes"),
            "the map's \"?\" beside its lens bar; Peoples' modes in its header");
        return n;
    }
}
