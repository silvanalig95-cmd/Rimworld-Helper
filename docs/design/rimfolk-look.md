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

## Stage 2: built (8 November 2026)

The council's windows float, the Almanac and Rimfolk's dialogs are in the frame, and the pages draw with the kit
(DESIGN.md, "Rimfolk's own look, stage 2" has all of it).

- **The four windows float** (`UI/FloatingWindows.cs`): the council, Planning and Defenses as the region window does,
  each a `RimfolkFloatingWindow` (keys "Council", "Planning", "Defense", "Region") drawing the same contents as its tab
  (`MainTabWindow_Council.DrawCouncil`, `MainTabWindow_Planning.DrawPlanning`, `MainTabWindow_Defenses.DrawDefenses`,
  `MainTabWindow_World.DrawRegion`), opened by one worker on all four buttons (`MainButtonWorker_RimfolkWindow`; the
  council's and the region's keep their names). A registry, `FloatingWindows`, makes each once and is what
  `Pages.Showing`, `Show`, `Close`, `OpenOrClose`, `OpenOrCloseSection` and `LeaveForMap` ask; one of the four at a time
  (`FramedWindowsMath.ToClose`: opening one closes the other three), a window opening for the first time taking the
  place of the one it turns the page from. The tabs (with "Rimfolk's windows move and resize" off) share one frame in
  the look (`RimfolkTabWindow`), the corner in the title bar.
- **Off to the map**: every caller that closed "the tab open" before a jump, a plan put down or a pick on the map
  (twenty-three of them: the planners and designators, the defense page and its lines, This week, the people table,
  wishes, making, the Almanac's room plans, the mood and bar badges' jumps) goes through `Pages.LeaveForMap`, which
  closes the floating windows and the tab, the inspect pane kept. A page sending the player to the game's research,
  quests or work tab gives way to it (`Pages.GoToGameTab`), as the tab did.
- **The kit everywhere**: Rimfolk's pages and dialogs draw their buttons, text fields, labelled checkboxes and row
  hovers with `UiKit.Button`, `TextField` (now with the game's length and validator overloads), `Toggle` (disabled, near
  its word), `RowHover`, and the new `ListRow` and `Section`; in the game's look each draws exactly the game's widget
  the page called before. The `static readonly Color` copies of tokens (69 of them: MoodView, HeartView, DecreePanel,
  AlmanacPanel, DefenseLinesPanel, the world's panels, the on-screen panels and others) are properties, so they follow
  the look.
- **The Almanac** (`Almanac/Window_Almanac.cs`, key "Almanac", least 560 by 400): in the frame, first beside Rimfolk's
  windows, its pages at their own text sizes; its search, switch, lists and buttons the kit's.
- **Rimfolk's dialogs**, each a `FramedWindow` with its key and least size (`FramedWindowsMath.Keys`): the letter
  window, the journey planner and its picker, the camp, the market, the stall counter, the founding window, the floating
  card, and every other: the feast, the journal, the news, a calendar day, the clock, the self check, the bar's
  arranger, the guest check, the newcomers, the landing, the hand-over, the scenario start, the two naming dialogs, a
  shelf's reserves and its picker, the floor picker, the ground plan, rearranging a room, a gift for an outsider, a
  panel's options. Their titles in the title bar (`TitleTop`, `TitleAndLine`, `UiKit.DialogHeader`: never said twice),
  the game's close cross, close button, dragging and sizing giving way to the frame's while it shows.
- **Wider shows more**: This week in three columns from a page of 1500; the journey planner's and the founding window's
  list of who goes up to 640 and 600 from 1400; the hand-over's and the scenario list likewise; the camp's acts up to
  half again as wide in a camp window made twice as wide; the market's goods' names take a wider window's room.
- **Previews and the search**: a name's preview takes Rimfolk's look when the window its link is in draws in it, and so
  does the corner's search; a card's model is kept for each look.
- **Left as they were**: the on-screen panels and their edit bar (the map's layer), the room and defense planners docked
  above the architect menu (sized to their content and placed each frame), the search (a popup under its icon), the
  game's own windows and panes (the mod settings, the inspect pane's tabs, the game's trade screen, choice letters).

## Risks to watch in the game

None of stages 1 or 2 could be run here: the drag and size by polling the mouse, the scrollbars' styles swapped and put
back, the floating windows beside the game's tabs and the inspect pane (which is on top; Escape to the topmost), focus
in the text fields with the game's key handling, the game's close button drawn at a framed dialog's foot (its room is
the dialog's own), a dialog's least size where its contents don't scroll (the letter window), a preview's look following
its link.

## Stage 3: approved (9 November 2026)

The player, on "which other UI aspects could we potentially enhance accordingly?" and the list proposed: "like the
events, assuming they still pop up the same way like normal game events do? i mean on the right side. story tab sounds
great. on-screens overall look great now, but if you can improve them further, go ahead! perhaps keep the current setup
as a backuip somewhere. tooltips on pawns i don't really use i noticed. colonist card would be cool! also like item card
for everything, although i fear that would take a lot of ressources?"

1. **Event cards.** Rimfolk's letters still arrive on the right side as the game's do (the letter stack, its sounds and
   colours unchanged). Opening one (dilemmas, Rimfolk's choice letters, its event letters) shows a Rimfolk card in the
   frame and the kit: a picture of who or what it is about (portraits, the visitor, the thing), the text, each choice as
   a card with its impact hints, who is involved with their mood and heart, what the council leans to. With "Rimfolk's
   own look" off, the game's letter window as now.
2. **The Story tab at a glance**, as the Mood tab: life goal with its progress, wish, quirks, standing in the colony,
   closest friends and rivals at the top; the whole story below in sections that fold.
3. **The on-screen panels further refined** in the look (cards, bars, icons, the heart and weapon marks), with the current
   panels kept as they are behind a setting ("Classic on-screen panels") as the player's backup.
4. **A colonist card**: one framed window a colonist, portrait, heart's meters, mood, skills and passions, kit (weapons
   and armour with their ratings), wishes, life goal, standing, side by side; from the colonist bar and Rimfolk's windows.
5. **An item card for everything**, on the weapon card's pattern (and the apparel card's): the item's picture, what it is
   good for in Rimfolk (feasts, gifts, treatment, trade, its roles), its tags, the game's own numbers a button away.
   Built when opened and cached, nothing drawn or computed while it is closed, so it costs the game nothing in play; done
   by kind of item, food, drink, drugs and medicine first, then materials, art and the rest.

Not taken now: pawn tooltips as cards (the player doesn't use them), the settings window and the docked planners.

Order: 1, 2 and 3 side by side (unrelated areas) as places free up; 4 after the Mood tab's meters; 5 after the apparel
card.
