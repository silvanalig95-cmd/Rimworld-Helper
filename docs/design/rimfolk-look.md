# Rimfolk's own look: windows you can move and size, and a clearer, modern style

The player (8 November 2026): "for the rimfolk uis, please also dont be withheld by the vanilla ui, for example for the
diplomacy screen, i'd love to move the window around, make it larger etc. you really don't have to stick to the rimworld
design theme if you can otherwise make all uis clearer, better to use, better looking, modern etc." Before that they had
seen the diplomacy screen's mockup (https://claude.ai/artifact/HihYZRcRr7jkKdbzasU2Tm) and asked whether the game would
look the same; this is the answer: as near as the game's interface code allows.

## What changes

1. **Every window of Rimfolk's moves and sizes.** The region window (map, peoples, here and away, history), the
   council's windows (Council, Planning, Defenses), the trade window, the Almanac, letters and dialogs of Rimfolk's own:
   - dragged by their title bar, resized from their edges and corner, with a sensible minimum;
   - a button to make one large (most of the screen) and back, and double-clicking the title does the same;
   - each remembers where it was and how big, between openings and between games (the mod's settings);
   - the main tab buttons still open them, but as floating windows rather than panels fixed at the screen's foot;
   - layouts follow the size: more columns and more shown when wider, nothing clipped when narrow.
2. **A clearer, modern style (Rimfolk's own, not the game's).**
   - A calm dark ground, cards with soft rounded corners and a faint edge, more space between things, one accent colour
     for what is chosen or yours, the stance colours (allied, neutral, hostile) with a word or icon beside them always.
   - Buttons, chips, tabs and toggles drawn flat with clear hover and pressed states; tabs that read as tabs; a selected
     row clearly lit; focus on text fields visible.
   - Type: the game's fonts (the only ones the interface can load), but a clear hierarchy by size and ink, never bold
     smeared, numbers right-aligned.
   - Built once in `UiKit` (colours, radii, spacing, the drawn parts), generated at load, nothing made per frame.
3. **A setting**: the look, "Rimfolk's own" (the default) or "the game's", and the windows' movable/resizable behaviour
   on its own switch, so the classic look stays for anyone who wants it.

## Order of building

After the diplomacy screen's step B (it is working in the same windows): first the window frame and the style kit with
the region window and the trade window, then the council's windows, the Almanac and Rimfolk's dialogs.

## Stage 1: built (8 November 2026)

The window frame and the style kit, on the region window and the trade window (DESIGN.md, "Rimfolk's own look, stage 1"
has all of it).

- **The frame** (`UI/WindowFrame.cs`, `UI/WindowFrameMath.cs`): a title bar of Rimfolk's own with the window's icon,
  title, tools, large and close; dragged by it, sized from every edge and corner down to the window's least, large
  (about 92% of the screen) with the button or a double-click on the title and back; each window's place and size kept
  in the mod's settings by its key ("Region", "Trade"), fitted onto the screen it opens on. `FramedWindow` wires it to a
  plain window.
- **The region window floats** (`UI/Window_Region.cs`): its button opens it as a plain window of the game's interface
  layer, which a click on the map or a colonist picked leaves open (a main tab can't stay open beside play: the game
  closes it on any map click and any selection). The tab is what opens with "Rimfolk's windows move and resize" off.
  `Pages.Open` addresses, the drawer, Escape, the corner turning pages and other mods' region pages work in both.
- **The kit** (`UI/UiKitSkin.cs`, `UI/SkinMath.cs`): rounded fills, edges, tab tops, corner masks and a soft shadow as
  textures made once at load and drawn as 9-slices; flat buttons (normal, primary, quiet), a square icon button, a text
  field and the game's quick search with their focus, a switch, tabs that read as tabs, a lit row with the accent's
  edge, scrollbars that blend in. The tokens take the mockup's palette in Rimfolk's look (the stances in blue and orange,
  one amber accent, every text colour at least 4.5:1), the game's values otherwise.
- **Applied**: the region window (its tabs, the map with its lens bar, List, Peoples with its list, page and acts, Here
  and away, History, the drawer) and the trade window (the top, the search and chips, the switch, the table with its
  numbers right-aligned, the side, the silver box, the foot's buttons). Wider windows show more: two columns of
  sections in Here and away and Faces, a wider Peoples list, three columns of acts.
- **Settings**: "Rimfolk's own look" and "Rimfolk's windows move and resize" (both on), and "Put Rimfolk's windows back".

## Stage 2: what it needs

- **The council's windows, Planning and Defenses** (`MainTabWindow_Council`, `MainTabWindow_Planning`,
  `MainTabWindow_Defenses`): each as the region window, a floating `FramedWindow` while the windows move, its tab
  otherwise. The pattern is `Window_Region` and `MainTabWindow_World.DrawRegion`: move the tab's `DoWindowContents`
  into a static draw both call, give the bar button a worker like `MainButtonWorker_Region`, and teach `Pages.Showing`,
  `Show`, `Close` and `Current` the floating window (`Window_Region.Showing` and its `Shut` today are the region's only:
  a small registry of the four floating windows by `RimfolkWindow` is the next step, so `Pages` asks it rather than
  naming each). `RimfolkTabWindow.PreOpen` closes the floating region window today; with four floating windows the
  rule "one of the four at a time" becomes "opening one closes the other floating three", in `Pages.Show`. The window
  keys: "Council", "Planning", "Defense". Their tabs draw `WindowTabs.DrawOwn` in the look, the corner in the title bar.
- **Calls that close "the tab that's open"**: about twenty callers use `Find.MainTabsRoot.EscapeCurrentTab(false)` before
  a jump or a designator (DefensePanel, the planners, NextSteps, PeopleTable, WishesPanel, BarBadges, WeekModel and
  others). With the council's windows floating each of those is `Pages.LeaveForMap()`, which closes the floating ones
  too. Search for `EscapeCurrentTab` once the windows float.
- **The pages' own drawing**: the council's pages draw with the kit and the tokens already, so they take the look in a
  framed window; what still draws the game's look directly: `Widgets.ButtonText` in the dialogs and a few pages (the
  journey planner, the camp's dialog, the letter's window in `World/NeighboursPanel.cs`), `Widgets.TextField` in the
  planners and the journey's counts, `Widgets.CheckboxLabeled` in the journey dialog and settings rows,
  `Widgets.DrawHighlightIfMouseover` in lists, cached colours captured at load (`MoodView`, `HeartView`, `DecreePanel`,
  `AlmanacPanel`, `DefenseLinesPanel` hold `static readonly Color` copies of tokens, which stay in the game's look:
  make them properties). Swap each for `UiKit.Button`, `TextField`, `Toggle`, `RowHover`, `RowLit`: in the game's look
  they draw exactly what was there.
- **The Almanac** (`Almanac/Window_Almanac.cs`): already a movable window of its own; give it the frame (key
  "Almanac") and the look, its text sizes kept.
- **Rimfolk's dialogs** (the letter's window, the journey planner, the camp's dialog, the trade market, the stall
  counter, the founding window, `Window_Card`): each a `FramedWindow` with its own key and least size.
- **Previews and the on-screen panels** draw outside any framed window (immediate windows, the map's layer), so in the
  game's look today. A preview could begin the look itself when the window under its link is in it.
- **Risks to watch in the game** (none of stage 1 could be run here): the drag and size by polling the mouse, the
  scrollbars' styles swapped and put back, the floating window beside the game's tabs and the inspect pane, focus in
  the text fields with the game's key handling.
