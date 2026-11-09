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
- **Left as they were**: the on-screen panels and their edit bar (the map's layer; the panels take the look in stage 3,
  point 3), the room and defense planners docked
  above the architect menu (sized to their content and placed each frame), the search (a popup under its icon), the
  game's own windows and panes (the mod settings, the inspect pane's tabs, the game's trade screen, choice letters; Rimfolk's
  own letters open as event cards since stage 3).

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

1. **Event cards** (built, below). Rimfolk's letters still arrive on the right side as the game's do (the letter stack,
   its sounds and colours unchanged). Opening one (dilemmas, Rimfolk's choice letters, its event letters) shows a Rimfolk card in the
   frame and the kit: a picture of who or what it is about (portraits, the visitor, the thing), the text, each choice as
   a card with its impact hints, who is involved with their mood and heart, what the council leans to. With "Rimfolk's
   own look" off, the game's letter window as now.
2. **The Story tab at a glance**, as the Mood tab: life goal with its progress, wish, quirks, standing in the colony,
   closest friends and rivals at the top; the whole story below in sections that fold.
3. **The on-screen panels further refined** in the look (cards, bars, icons, the heart and weapon marks), with the current
   panels kept as they are behind a setting ("Classic on-screen panels") as the player's backup (built, below).
4. **A colonist card**: one framed window a colonist, portrait, heart's meters, mood, skills and passions, kit (weapons
   and armour with their ratings), wishes, life goal, standing, side by side; from the colonist bar and Rimfolk's windows
   (built, below).
5. **An item card for everything**, on the weapon card's pattern (and the apparel card's): the item's picture, what it is
   good for in Rimfolk (feasts, gifts, treatment, trade, its roles), its tags, the game's own numbers a button away.
   Built when opened and cached, nothing drawn or computed while it is closed, so it costs the game nothing in play; done
   by kind of item, food, drink, drugs and medicine first, then materials, art and the rest (steps 1 and 2 built, below).

Not taken now: pawn tooltips as cards (the player doesn't use them), the settings window and the docked planners.

Order: 1, 2 and 3 side by side (unrelated areas) as places free up; 4 after the Mood tab's meters; 5 after the apparel
card.

## Stage 3, point 1: built (9 November 2026)

The event cards (DESIGN.md, "Rimfolk's own look, stage 3, point 1: event cards" has all of it). Released 0.190.0.

- **Which letters**: every choice letter class of Rimfolk's (28) derives from `RimfolkLetter`, whose `OpenLetter` opens
  the card while the cards are on and the game's own window otherwise; Rimfolk's plain letters (the game's
  `StandardLetter`, from the teller and Rimfolk's world-news defs) through one patch on `ChoiceLetter.OpenLetter` that
  acts only on that plain class and only on Rimfolk's (its def, or its ID kept by the teller in `EventCardsBook`). The
  game's own letters and other mods' are untouched; the colony news with lines keeps its own window.
- **The card** (`UI/EventCards/EventCardWindow.cs`, key "EventCard", least 520 by 440): a header with the picture
  (portraits, the thing, the people's emblem, else the kind's mark), the title, the kind beside the letter's colour, the
  people with their stance, the jump to the place; the text; each answer as a card with the line under it, its costs and
  risks, why it can't be chosen; who is involved with their mood and the heart's band, a click to go to them; what the
  council leans to where it voted (the meeting, a hand-over); the links, the quest, postpone and close at the foot. Wider,
  a side column and two or three columns of answers.
- **The game's behaviour kept**: the letter's own choices, made once as the game's window makes them, owned by a hidden
  `Dialog_NodeTree` of the game's so its own `DiaOption.Activate` runs; pausing the game; the History tab; quests; a
  letter's last tick wanting an answer (no close then).
- **Setting**: "Rimfolk's event cards" (`eventCards`, on), under "Rimfolk's own look"; with either off, the game's window.
- **Not run here**: every letter's pages through the hidden tree, the card's look in the game, the portraits of people off
  the map, a plain letter from an older save (it opens in the game's window).

## Stage 3, point 2: built (9 November 2026)

The Story tab at a glance, on the Mood tab's pattern (DESIGN.md, "Rimfolk's own look, stage 3, point 2: the Story tab
at a glance" has all of it). Released 0.191.0.

- **Who they are, in one line** under their name: the name the colony knows them by, their office or the game's title,
  their age, where they come from (a founder, born here, won over by someone, the people they lived among); each in full
  in its tooltip.
- **At a glance** (`UI/StoryGlance.cs`, rules in `UI/StoryGlanceMath.cs`): their character in words; their life goal
  with a gold bar, its progress in its own words and how long they've held it; the story they're living; their wish
  before the council (how long they've waited, what fulfils it, "Show me" and "Promise", the wish cards' own acts);
  quirks as tags with what each does; standing (trust on a small meter with its bands, office, health, how many think
  well or ill of them); up to three closest friends and three rivals with small portraits and their opinion, a click
  selecting them; the newest line of their history.
- **Below**, folding and remembered between sessions, in order of use: their story (the history, the way to the whole of
  it, the stories lived through), wishes (now and what came of earlier ones), life goals, quirks and keepsakes in full,
  every friend and rival, family, dreams, the character page's sections, the years, secrets and the journal. Nothing the
  tab showed was lost; the family's portraits at the top gave way to the friends and rivals (the colony is the family),
  the family tree keeps everyone.
- **Both looks** through the kit: the game's in the inspect pane, Rimfolk's own on a colonist's card, which draws the same
  page. Built at most twice a second, drawn only from the model.

## Stage 3, point 3: built (9 November 2026)

The on-screen panels in the look (DESIGN.md, "Rimfolk's own look, stage 3, point 3: the on-screen panels" has all of it). Released 0.194.0.

- **The backup**: "Classic on-screen panels" (`hudClassic`, off, under On-screen panels): on, every panel and the hover
  card draw through the code they drew with before, unchanged; with "Rimfolk's own look" off they always do. Each panel
  keeps its classic drawing in its own file and branches to the look first (`Hud/Look/`, partial classes); places, sizes,
  options, clicks, the edit bar and saved layouts are shared.
- **Cards**: the look's rounded fill as solid as the panels' look says (its edge under the mouse), a title row inside
  with the panel's icon and name whose right end holds the bar's buttons while the mouse is on it (compact panels keep
  the bar over them, quieter), consistent padding, everything on whole pixels.
- **Bars, not numbers**: the mood with its break lines (its number small beside it), the heart's strain and good side in
  the heart's meters' colours and marks, health, food, rest and recreation, days of food and medicine against their
  lines, the research under way, trust either side of nothing.
- **Severity by form**: a stripe down an urgent row (pulsing for life and limb) and its icon in the level's colour, no
  wash under the words; alerts with the warning icon on critical and high ones.
- **Rimfolk's newer marks**: the heart's band, a day off or a bad afternoon under way, an empty weapon slot (from the
  colony's kept choice, never worked out by the panels) on a colonist's row; the weapon rating's badge on the inventory.
- **Hover card** in the same style. Five new icons (Trend, Vitals, Stock, DayOff, KitGap). Every text colour at 4.5:1 on
  the card over the map (tested).
- **Speed**: every word and number made when a panel gathers, widths measured once, no delegate or string a frame, words
  drawn on repaint only, the layout pass skipped; the debug action "On-screen panels: time the classic and refined
  drawing (log)" times both looks in the game.
- **Not run here**: the look in the game at every scale and look, the timing, the title row's buttons and dragging, the
  layout pass skipped, the day off and kit marks on real colonists, the rating badge (where the game counts weapons in
  the stock), the hover card's link in the corner.

## Stage 3, point 4: built (9 November 2026)

The colonist card (DESIGN.md, "Rimfolk's own look, stage 3, point 4: the colonist card" has all of it). Released 0.197.0.

- **One window per colonist** (`UI/ColonistCard/`, key "ColonistCard", least 460 by 440): several open at once, each
  moving and sizing on its own; they share one remembered place (where the last was left), and a card opened while
  others are open steps a title bar down from them. Arrows in the title bar step to the previous or next colonist in the
  colonist bar's order, past those whose card is open already.
- **The header**: their whole figure as dressed from the game's portrait cache, the heart's line under it in the colonist
  bar's colours, their full name, who they are in the Story tab's one line, chips for a break, downed, drafted, their
  ideoligion's role, the Empire's title and where they are.
- **At a glance**, in up to four columns that fit the width (stacked when narrow): **Now** (the mood bar with its break
  lines and where it heads, the heart's two meters with the day off's place, what weighs and lifts most, what the bands
  do, the top advice), **Skills** (each a bar with its level and passion, the game's or Vanilla Skills Expanded's marks,
  what they're best at, those they can't do marked), **Kit** (weapons in hand, off hand and carried, apparel by slot,
  each with its picture, rating badge and rating for them, a click opening its card; the kit's gaps and better weapons
  with their acts; dressed for the season or not; a hook for the apparel suggestions to come) and **Life** (the Story
  tab's glance: life goal, wish with Show me and Promise, quirks, standing, friends and rivals).
- **The foot**: the game's Mood, Story, Gear and Health tabs, go to them, select them, their whole story beside the People
  table, and what else other parts let the colony do.
- **The ways in**: wherever a colonist's card opened before (every name and portrait in Rimfolk's windows, panels and
  tabs: `Cards.Open` sends the colony's own to it, the small card kept for everyone else and for their whole story);
  Ctrl and a click on a portrait in the colonist bar; a small card button by a selected colonist's name on the inspect
  pane; a button on each colonist in an event card's "who is involved". Settings "Rimfolk's colonist card", "Ctrl-click
  on the colonist bar opens the card", "A card button on the inspect pane" (How you're told, Rimfolk's buttons and windows).
- **Speed**: built when opened, the live parts twice a second, the heavy parts every three seconds; drawn only from that.
- **Not run here**: the card's look in the game, the portrait from the cache, the inspect pane button's place beside the
  game's own, the Ctrl-click with every other click of the bar unchanged, several cards and their places, Vanilla Skills
  Expanded's passion marks, the rows and suggestions on real colonists, the event card's button.

## Stage 3, point 5, step 1: built (9 November 2026)

The item card for food, drink, drugs and medicine (DESIGN.md, "Rimfolk's own look, stage 3, point 5, step 1: the item card
for food, drink, drugs and medicine" has all of it). Released 0.198.0. Materials, art and the rest are the next
steps.

- **The card** (`Items/ThingCardWindow.cs` on `UI/ItemCardWindow.cs`, whose head draws without a rating's badge now): the
  thing's own picture large, its label with the stack and quality, its kind and worth, "All the game's stats", a line in
  plain words on what it is good for in this colony ("Fine meal: lifts the mood (+5), keeps 4 days, enough for 2
  colonists' supper."), its tags in the shared six categories, each from its own numbers or Rimfolk's rules with why.
- **By kind**: food (nutrition and colonist-days, how long it keeps here and in the cold, the game's mood, food poisoning,
  the road, a feast's dish, who at home eats it or won't and why); drinks and drugs (effects and how long they last,
  addiction, tolerance, overdose, the toast, each colonist's policy, beliefs and addiction, each faith's precepts);
  medicine (potency, the care that allows it, what Rimfolk's medicine by what is treated gives it to, each doctor's
  tending with it and without).
- **The colony's stock** (held, days of food or medicine, short or spare by the trade advice's rules) and where it is made
  (a bill on a bench, a click going to it; a bench without one; the research it waits on; a field that grows it); **trade**
  (worth, the traders who deal in it, Vanilla Trading Expanded's market, the colony's own last prices).
- **Opens first** for these kinds through the weapons' one window hook ("Open Rimfolk's item card first", How you're
  told, Items), with the same pass-through, and a button back on the game's card.
- **Speed**: a kind's numbers read once and kept; the model built when the card opens, its live parts read at most once a
  second and the colony's every ten seconds; nothing read while it is closed or while drawing.
- **Not run here**: the card in the game at its least width; the drug effects' words and durations for every high; a
  kind's thoughts with no thing in hand; a fridge mod's stack; the traders' price types; the jumps to a bench's bills and to
  a field; the first card's reading time with the player's mods.

## Stage 3, point 5, step 2: built (9 November 2026)

The item card for materials and art (DESIGN.md, "Rimfolk's own look, stage 3, point 5, step 2: the item card for materials
and art" has all of it). Not released yet. The rest (books, body parts, techprints, trainers, genepacks, one-use artifacts,
shells and the like) is step 3, and adds its kinds the same way (`ItemSort`, `ItemCardMath.KindOf`).

- **Which things**: materials are what the game files as raw or manufactured resources, stuff and chunks (metal, wood,
  stone blocks, cloth, leather, wool, the mods' own, stone chunks and slag, components, chemfuel, neutroamine, silver and
  gold), read from the defs so the player's mods' materials come in on their own; art is anything with the game's art comp,
  held, minified or placed (a grave whose art shows only once full, only when full). Weapons, apparel, food, drink, drugs
  and medicine keep their cards; whatever is to be used, read, installed or fired stays the game's.
- **A material's card**: the line in plain words ("Plasteel: the sturdiest metal there is, light and fireproof, but slow to
  work; 340 in store, the blueprints need 60 more."), its tags, what it does to what is made of it against steel and wood or
  the colony's most held of its kind (the game's stuff powers, stat factors and damage multipliers, in a small table), what
  the colony can make of it now (buildings and floors, apparel, weapons, other things, the best first, and what research
  would open), the stock and what waits on it (blueprints and frames, bills with a click to the bench, repairs for
  components, the burners and the days a fuel lasts, the trade advice's verdict), where it comes from here (rocks on the
  map with a click to one, a deep drill, trees, crops, the colony's and wild animals, deconstruction, the recipes and their
  benches) and trade.
- **A work of art's card**: the line ("Grand sculpture, excellent: beauty 300 in the dining room, worth $1,400; selling it
  would make the room merely slightly impressive."), its beauty and worth (by quality, in its stuff), the tale it shows (the
  title, the artist, the event and its date, the game's own description), where it stands and what it does there, keep or
  sell, and the rules that care (a noble's throne room or bedroom, a faith's precepts on art, the artistic meditation focus).
- **The room it stands in**: a placed piece is judged in the room its own cell belongs to. The room with it is the game's
  own numbers (wealth, beauty, space, cleanliness, impressiveness) and stages; without it, the game's sums with the piece's
  beauty over the room's beauty divisor (the game's own curve) and its worth taken out, applied as a difference to the
  game's number. Outdoors or in storage it lifts no room, and the card names the colony's rooms whose impressiveness gives a
  mood where it would do most, a click going to each. Nothing is moved.
- **Opens first** through the same hook and setting ("Open Rimfolk's item card first"), the setting's words naming the new
  kinds.
- **Speed**: a kind's numbers and everything made of it, what yields it and what burns it read once a session; the model
  built when the card opens, its live parts at most once a second, the colony's (stock, demand, deposits, rooms) every ten
  seconds; the card draws only its model.
- **Not run here**: the card in the game for every material and piece of art; the rocks' count from the map's things list;
  the deep scanner's spots; the room numbers against the game's own with a piece taken out; a noble's room; the art comp's
  tale through the game's private field; the first material card's reading time with the player's mods.
