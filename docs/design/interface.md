# The interface for the long run (design, 4 November 2026)

The player's words: "on the new world menu i noticed that there's now a ton of tabs. really think in general, but
especially as we add more and more features, how to best keep the interface clean, easy to navigate, interactive
(e.g. things referencing each other being clickable). think of the best possible way how everything is ordered,
layered, expanded etc., use other games with similar mechanics etc as reference, and you don't necessarily have to
stick with the standard rimworld style, if other design philosophies would make it better, easier and clearer, while
still looking solid". Then: "prioritize the ui". This is the design: today's interface counted, what other games
teach, one structure and one way of moving through it for all of Rimfolk now and as stages 2 and 3 arrive, the look,
what it takes in the game's interface code, and five releases that each play as before. It builds on DESIGN.md,
"Rimfolk's windows" (0.70.0: four bar buttons, one page list, `Pages.Open`), and places every piece of
`docs/design/stage2-diplomacy.md` and `docs/design/stage3-many-colonies.md`. The visual mockup is
`docs/design/interface-mockup.html`. The game's side was read in the 1.6 reference assembly; every game name used
below is in 1.7's too.

## Summary

- **Fewer places, each with few views.** The four bar buttons stay (the player's word). Inside them, 34 pages become
  13 views, never more than five to a window: Council (This week, People, Council, Colony), Region (Map, Peoples,
  Here and away, History), Defense (Defenses), Planning (Plan, Base, Making, Rooms). What made more tabs before now
  makes a mode, a lens, a filter, a section or a row. The Almanac moves to its own movable window, reached by a "?"
  everywhere.
- **One model for things.** A colonist, a person outside, a place (town, post, hamlet, ruin, camp, a colony), a people,
  a house, a tie, an event and a letter each have one card. Every name of one, in any panel, list, history line or
  letter text Rimfolk draws, is a link to its card; hovering it shows a small preview that stays when the mouse enters
  it. A card opens in a drawer at the right of the window the player is in (the region map's panel is that drawer), so
  following a name never throws the player out of what they were doing.
- **Moving like a browser.** Back and forward arrows in every window's corner keep the trail across windows and cards;
  a search box finds any person, place, people, view or Almanac page; any thing can be followed (pinned) and shows at
  the top of This week and, if wanted, in an on-screen panel.
- **The region map is the hub.** Six lenses (Land, Stance and ties, Fortunes, Threats, Trade, Ours) recolour the same
  map and give the same map a list. Trading posts, raiders' camps and hamlets stop being pages and become what the
  Trade, Threats and Ours lenses show. A town's whole story lives in its card: now, its people, its ties, what was
  heard, its story, and what the colony can do there.
- **One inbox.** This week becomes the one place where everything waiting on the player is: answers, asks, offers,
  calls, promises at risk, plus what is under way (in steps, never a countdown) and what is coming. Its count is the
  same in every window's corner and on the council button.
- **A rule for every new feature**, written for the builders who come later: a new thing gets rows, sections, acts,
  lens marks and inbox rows; a new view only when nothing else fits; a new window never.
- **A visual language** close to the game's frame but stricter: a 4 pixel grid, three type sizes, colour by meaning
  only (warm, cool and sour stance; soon and danger; ours; done; links), and fifteen components drawn the same way
  everywhere.
- **Five releases** before stage 2a.1, each playing as before with every old page address forwarding: names that lead
  somewhere (links, cards, previews, back), the region in four views, This week as the inbox with search, the council
  in four views with the Almanac window, one look across everything. Seven decisions at the end.

## What there is today

### The count

| What the player can open | How many | Where |
|---|---|---|
| Bar buttons | 4 windows, and More for the ones put away | `Defs/Misc/Council_Misc.xml` |
| Windows from those buttons | 4 | `MainTabWindow_Council`, `_World` (labelled "region"), `_Defenses`, `_Planning` |
| Pages | 34 | Council 18 in four sections (Council 5, People 7, Colony 5, Almanac 1), Region 10, Defense 1, Planning 5 |
| Rows of tabs | 2 in the council window, 1 in the others | sections, then each section's sub-tabs |
| Region map panel sections | 14 from 8 files | `NeighbourPanel.Sections` 7 (a town), `PlaceSections` 4, `HomeSections` 3 |
| Inspect tabs and additions | 3 tabs (Story, Culture, Mood), the Social column, a line in Needs, the guest checklist on Hospitality's tab | `ITab_Pawn_*`, `SocialColumn`, `GuestCheck` |
| Colonist bar badges | 6 kinds | `ColonistBar/BarBadges.cs` |
| On-screen panels | 8 and the hover card, with their switch in the play settings | `Hud/` |
| Dialogs and planner windows | 20, and 3 for checks and development | journey planner and picker, the letter writer, the gift, a calendar day, a camp, news, the defense and room planners, ground plan, floors, shelves, names, the guest checklist, a journal, a panel's options, arranging the bar |
| Letters with buttons | 13 kinds of Rimfolk's own; 91 calls through `Tell.Letter` and `Tell.Ask` | dilemmas, elections, meetings, camps, calendar days, journeys, world events, happenings, raiders' camps, hamlets, news, welcome, what's new |
| Right-click acts | about fifteen under "Talk with X..." | `Outsiders/` |
| Settings | 7 pages with search and presets | `SettingsWindow` |
| Almanac | 463 articles on one page with search | `Defs/AlmanacArticleDefs` |
| Keys | 14 of Rimfolk's | page keys, next and previous page, the region map |

### Where each thing shows, and where a click on it goes

| Thing | Shown in | A click on it today |
|---|---|---|
| A colonist | Roster, Everyone, Bonds, Faiths, Moods, Dreams, Journals, This week, the panels, the bar, the inspect tabs, letters, talk | Their chip opens their page in the Roster (`UiKit.PawnChip`): the one model already working. Their name in a sentence: nothing. |
| A person outside (a notable, a known face) | Neighbours page, the town's panel (the same rows), Letters page, talk, letters, the history | Nothing. The tooltip lists what they remember; "Write" opens the letter writer. |
| A place (town, post, hamlet, ruin, camp) | The region map, Neighbours' town cards, the Trading posts, Hamlets and Raiders' camps pages, This week, the history's filter | On the map, its panel. Elsewhere "Show" leaves Rimfolk for the game's world view; the name in a sentence: nothing; the town card's heading only folds it. |
| A people | Peoples page, faction chips everywhere | The chip: a tooltip only. "Their history" on the Peoples page. |
| A colony | `ColonyView`'s header and "Another colony" button, the region map's home | The home on the map shows its panel. |
| A house (a family) | A town's panel, "founding families: ..." | Nothing: plain text. |
| A tie (favour, word, grudge, fealty) | What the peoples remember (`FactionMemoryPanel`), a notable's tooltip | Nothing. No place shows a tie as itself. |
| A world event | Events page, Peoples rows, a town's panel, its letter | Nothing. |
| A history entry or deed | World history, Chronicle, a notable's notes | Filter buttons on the History page only. |
| A letter of the colony's | Letters page, the notable's row | Nothing. |
| What waits on the player | This week (twelve cards), counts on two windows' tabs, the game's letter stack, the region pages' lines | This week's rows open a page, a letter or the world view. |

### What is wrong with it

1. **Too many tabs at one level.** The region window has ten. In its 974 pixels less the corner's 128, a tab is about
   94 pixels wide, so most show only their icon or only their name; stage 2's Diplomacy and Household make twelve,
   about 80 pixels each. The council window has two rows, and its People section seven sub-tabs.
2. **Tabs that come and go.** Trading posts, Raiders' camps, Hamlets, Letters and Faiths show only when their feature
   is on or has something, so the tabs after them move and the hand learns nothing.
3. **One thing in many places, each with a different part of it.** A town shows on the map, in Neighbours, and on the
   posts, hamlets and camps pages, each with other lines and no way between them. Notables twice. Fights coming twice
   (This week, Neighbours). The advisor's next steps twice (Colony → Priorities, Planning → Plan). Room plans twice
   (Planning → Build, Planning → Rooms). History in four pages (Chronicle, World history, Dreams, Journals).
4. **Dead ends.** Names in sentences lead nowhere. A people's chip and a notable's portrait only have tooltips. The
   "Show" buttons drop the player in the game's world view, out of Rimfolk, where nothing of Rimfolk is.
5. **Asks scattered.** This week, the counts on the council's section tabs, the region's tab counts, the letter stack,
   and lines on three region pages.
6. **Reference mixed with live state.** The Almanac, 463 articles, is the council window's fourth section.
7. **No way back.** `Pages.Open` jumps; the player can't return to where they were.
8. **Growth by tabs.** `MainTabWindow_World.Register` makes a tab the default answer: every stage 1 release added one,
   stage 2 adds two, stage 3 a page, and the Later list has more.

### How it grows

Stage 2 brings favours, pacts and truces, ransom, envoys, feasts and contests, marriages, wards and hostages, a new
office and council tasks, fealty and vassals, schemes, house renown and stress against one's nature; its design puts
them on two new region pages, the town panel, the right-click menu, letters and council questions. Stage 3 brings
several colonies (a Colonies page, the pages following the colony in view), the hand-over window and a founding
party. Kept as it is, the region window reaches twelve tabs, the council window nineteen pages, and every tie and
person of stage 2 is named in text that can't be clicked. The structure below gives each of them a place without a tab.

## What other games do, and what Rimfolk takes

- **Crusader Kings III.** Every name (a character, a title, a faith, a culture, a game concept) is a link, and its
  tooltip can be locked (wait, or press the middle button) and hovered inside, tooltip within tooltip. One window per
  thing: the character window is the hub of a person, the realm window of a realm; the encyclopedia explains
  concepts from the tooltips. *Takes:* every name a link; one card per thing; previews that stay when entered; concepts
  linked to the Almanac. *Doesn't fit:* nesting without end (two levels are enough in a real-time colony game, and
  RimWorld's tooltip can't be entered at all, so Rimfolk draws its own); a full-screen character window over the map.
- **Victoria 3.** Nested tooltips; map lenses that recolour the map and change the list beside it; the outliner of what
  the player pinned. *Takes:* lenses on the region map, each with its list; following things. *Doesn't fit:* its
  density of numbers; Rimfolk says things in words.
- **Stellaris.** The outliner, a sidebar of everything owned in collapsible groups; the situation log, where an
  ongoing event shows its stage and what comes next. *Takes:* "Under way" in steps (written, on the road, arrived,
  answered), which also keeps the player's rule of no countdowns. *Doesn't fit:* an always-open sidebar on the map;
  here it is an optional panel.
- **Old World.** Event texts with linked characters and concepts, an encyclopedia reached from them. *Takes:* links in
  letters and event texts, the "?" to the Almanac.
- **Dwarf Fortress, Legends.** Every figure, site and civilisation has a page made of the events it took part in,
  each linked. *Takes:* history as the spine of every card ("its story", the newest lines and the whole history one
  click on). *Doesn't fit:* walls of text without filters.
- **Football Manager.** Everything is clickable, back and forward like a browser, a search box over everything, and
  an inbox as the home where decisions wait. *Takes:* back and forward, search, the inbox as home. *Doesn't fit:* menus
  three levels deep.
- **Civilization VI.** The Civilopedia apart from play and reached from what it explains; notifications that stack
  until dealt with; lenses. *Takes:* the Almanac in its own window with "?" links; lenses. *Doesn't fit:* blocking the
  next turn until notifications are dealt with; RimWorld has no turns, the inbox never blocks.
- **Frostpunk and Against the Storm.** Lean panels, a few big actions, a building's panel holding everything about it.
  *Takes:* the place card as the one place a town's story lives, its acts at its foot; lean views.
- **Paradox's pins.** Pin a character or a war and it stays in sight. *Takes:* following.
- **RimWorld itself.** One bar button, one window; the inspect pane with tabs; letters in a stack, each leading to
  its target; the world map with its own inspect pane; the info card (`Dialog_InfoCard`) with links at its foot
  (`Widgets.HyperlinkWithIcon`); the quick search field (`QuickSearchWidget`). *Takes:* the bar's few buttons, the
  game's tab look for the views (five fit with icon and name), the info card's habit of linking, its search field,
  the letter stack left as the game's. *Doesn't fit:* the info card's links open only defs; a window opened on top of
  a window for each thing.

## The proposal

### Three kinds of thing on screen

- **Places** are the windows and their views: where the player browses collections and acts on many things at once.
- **Things** are the nouns of the game: each has one card, reachable from wherever its name is.
- **Asks** are what waits on the player: one inbox, This week.

Two more stand beside them: **the history**, one timeline that every card reads its story from, and **the Almanac**,
the reference, reached by "?" from anywhere.

### The windows and their views

Four bar buttons, as the player decided. Inside each window at most five views, as the game's tabs with icon and
name (at five a tab is about 140 pixels wide beside the corner, so names always show). Inside a view at most five modes (the same
things shown another way), as a row of small buttons in the view's header, not as tabs. Filters are chips; groups are
sections with a count that fold.

| Window | Views (modes) | What folds in from today |
|---|---|---|
| Council | **This week** (the inbox) | This week; the advisor's top lines; the counts that sat on the section tabs |
| | **People** (Table, Moods, Bonds, Faiths, Houses) | Roster and Everyone become one list with the person's card in the drawer; Moods, Bonds and Faiths become modes; Dreams and Journals become sections of the person's card and filters of the Chronicle; Houses comes with stage 2d |
| | **Council** (Offices and decrees, Ambition, Ways, Wishes, Dilemmas) | the Council section's five pages as modes |
| | **Colony** (Overview, Calendar, Chronicle) | Priorities moves to Planning → Plan, where its cards are acted on and which already shares its "Not this" |
| Region | **Map** (lenses Land, Stance and ties, Fortunes, Threats, Trade, Ours; Map or List) | Map; Neighbours' towns (the List under Stance); Trading posts (Trade); Raiders' camps (Threats); Hamlets (Ours); the camp and fights coming (Threats and This week) |
| | **Peoples** (Peoples, Faces) | Peoples (since 8 November 2026 a list of peoples beside each one's page: below, "The Peoples view"); stage 2's Diplomacy page; Neighbours' "who's out there" (Faces: notables, known faces, old foes, those who left) |
| | **Here and away** | Journeys (who of ours is away), Letters under way (messengers on the road), stage 2's Household page (who is here from outside, tasks, schemes) |
| | **History** (scope: this colony, the region, the world) | World history; Events (world events running, as the strip at the top); letters lately (a filter) |
| Defense | **Defenses** | as today |
| Planning | **Plan**, **Base**, **Making**, **Rooms** (List, At a glance) | Priorities joins Plan; Build and Rooms are one view with two modes |

That is 34 pages to 13 views. The Almanac leaves the council window for a window of its own (decision 4); the
Chronicle and the world history are one timeline component with two doors, the Chronicle opening it on this colony and
History on the region.

**The window's chrome.** One row at the top of every window: the views as the game's tabs on the left; on the right
the corner, from left to right: back and forward, search, This week with its count, following, then the four windows'
icons as today. A view's header carries its title, one line saying what it is, the stat on the right, its modes and a
"?" to its Almanac article.

### Things and their cards

Eight kinds of card, each drawn by one builder:

| Card | For | Opens with | Its "open in full" |
|---|---|---|---|
| Colonist | the colony's own people | the Roster's page of them today (`StoryView`) | People, with them chosen |
| Person | anyone outside: a notable, a known face, a nemesis, an envoy, a ward, a captive | new | Peoples → Faces, chosen |
| Place | a town, a post, a hamlet, a ruin, a raiders' camp, a town being built, a bare tile, a colony | the region map's panel today | Map, picked |
| People | a faction | new | Peoples, chosen |
| House | a family, ours or theirs | new | People → Houses, or its town's card |
| Tie | a favour, pact, truce, marriage, ward, hostage, fealty, grudge, secret, scheme | new | the Peoples card of its people |
| Event | a world event, a history entry, a deed | new | History, filtered to it |
| Letter | a letter or call of the colony's or to it | new | Here and away, on the road |

Every card has the same slots in the same order, so the player learns one layout:

1. **Head**: the picture (portrait, icon, a people's banner), the name, what it is in one line ("head of Tallbrook",
   "a market post of the Ashfall Clans"), its chips (the people, the town, the house; each a link), the follow pin,
   and for a place a small "world view" button (the old "Show").
2. **Now**: what is happening to it, and any ask about it with its act ("They ask for medicine. Send some.").
3. **Who**: its people (a town's notables, a people's head and towns, a house's members).
4. **Ties**: its ties in its age's words, with since when and on what terms (stage 2).
5. **Words**: what they remember of the colony, what was heard from them lately, letters to and from.
6. **Story**: how it began (founded by whom, from where, why), the three newest lines of its history, and its whole
   history one click on.
7. **Acts**: a bar at the card's foot with what the colony can do (write, plan a journey, call in a favour), each
   greyed with why not, as the right-click menu does already; the rest under "More".

Sections fold, with their count in the heading ("People 4"); their folds are kept per kind of card for the session.
A slot with nothing is not drawn. Features add to a card through `CardSections.Register(kind, slot, order, draw)`
and `CardActs.Register(kind, label, whyNot, act)`, which the region map's three `NeighbourPanel` lists become (each
of today's fourteen sections goes into a slot; the list stays as a forwarding shim for a release).

**Where a card opens.** In a drawer at the right of the window the player is in, 380 pixels wide, beside the view where
the window is wide enough (a content area of 900 pixels or more) and over it, with a shadow, where it isn't. Its head
shows the trail of cards opened in it ("Tallbrook > Edda Marsh > the Marsh house"), each a link back. Escape closes
the drawer first, the window second. On the region map the drawer is the map's panel: picking a town opens its card.
From outside Rimfolk's windows (the inspect tabs, a panel, one of Rimfolk's letters) a link opens the same card in a
small floating window with "Open in its window".

### Links and previews

**What is a link.** Any name of a thing, wherever Rimfolk draws text: chips (people, colonist, place), table cells,
history lines, the town card's sentences, This week's rows, the Almanac's mentions of a page. Not on the map's
speech bubbles (they pass), not in the game's own windows (they are the game's), not in messages at the top left
(the game's jump already serves them).

**How it looks.** A name that is a link is drawn in link ink (a light parchment, a little warmer than the text) with a
fine dotted line under it; under the mouse the line becomes solid and the ink brightens. Colour tells what a thing is
(a stance dot, a fortune chip) beside the name, never the link's own ink, so a link always looks like a link.

**Previews.** Resting the mouse on a link for a short pause (0.35 seconds) shows its preview under the link: the
picture, the name, what it is, one line of stance or state, and two or three lines that matter most now ("Owes the
colony a favour", "A letter from you is on the road"). It stays while the mouse is on the link or on the preview, and
half a second after it leaves; moving into the preview keeps it, and a link inside it can show one more preview, two
levels at most. A click on the link or the preview's name opens the card. No preview while dragging or scrolling, and
a setting sets the pause or turns previews off. Buttons and icons keep the game's plain tooltips.

**How links get into text.** Text is filled from facts that know what each placeholder names (`Texts.Fill`,
`Facts`, `History.Text` with its actors and places as `Ref`s). A caller that draws in Rimfolk's own windows asks for
linked text (`Fill(..., links: true)`), and the filler wraps each named thing in a marker only Rimfolk's `LinkText`
reads. Everything that goes to the game (letters, messages, bubbles, the play log) is filled without markers, as
today, so nothing can show a raw marker in the game's windows.

### Back, forward, search, following

- **Back and forward.** A trail of where the player was: window, view, mode or lens, filters that matter, and the card
  open. Opening a view, a card or a lens adds a step; scrolling and folding don't. The arrows are in every window's
  corner; the mouse's side buttons work where the game reports them; two keys can be bound. The trail is kept for the
  session, at most 50 steps, and survives closing a window.
- **Search.** A field in the corner (and a key). It finds colonists, persons, places, peoples, houses, views and modes
  by name ("Bonds"), and Almanac articles, grouped by kind, the first chosen with Enter. The index is made when the field
  is clicked: some 42 notables, 40 towns per home, the peoples, the colonists, 13 views and their modes, 463 article
  titles; filtered as the player types.
- **Following.** A pin in every card's head. What is followed shows at the top of This week (the "Pinned" card, which
  already pins colonists and wishes, takes any thing), each with its newest change in one line, and in an optional
  on-screen panel "Following" beside the other eight. At most twelve; saved with the game (a list of refs).

### The region map as the hub

The region window opens on the map, as today. Over the map, the lens bar; at its right, Map or List and the reach.

**Lenses.** One at a time; each recolours the marks, may draw lines, changes the legend at the map's foot, and gives the
List its rows. The land's picture is made once, as today; a lens changes only what is drawn on it each frame.

| Lens | Marks | Lines | List | Replaces |
|---|---|---|---|---|
| Land | today's marks by stance, the land's legend | roads and rivers as drawn | towns nearest first | the map as it is |
| Stance and ties | towns by their people's stance (warm, cool, sour), the head's regard as a ring | from home to each town with a tie: pact solid, truce dashed, favour owed dotted, fealty double (stage 2) | towns with their notables (today's Neighbours) | Neighbours' town cards |
| Fortunes | towns by fortune (struggling to flourishing) with an arrow when it moved this season, world events where they are, ruins, towns being built | none | towns by fortune, what moved each | the Events page's places |
| Threats | hostile towns, raiders' camps found, war bands, a bandit road, the home with a threat now | from a camp to the home it raids | camps with their raids so far, their price and Pay | the Raiders' camps page |
| Trade | trading posts and their keepers, towns by what they trade, caravans on the road | from each post to its town | posts with keeper, warning, last trade | the Trading posts page |
| Ours | the colony's homes, hamlets, vassals, towns the colony was (a colony handed over), outposts, our caravans and journeys | from home to hamlet and vassal | each holding with its people, its next need; colonies of the past | the Hamlets page; stage 3's Colonies page |

A seventh lens comes only by replacing one: a lens is a new way of reading the whole region, not a feature's page.

**The place card: a town's whole story.** Picking a town opens its card in the drawer:

- Head: "Tallbrook", "a town of the Ashfall Clans", chips for its people (stance word and dot), its fortune, the
  distance by caravan ("three days by caravan"), the pin and the world view button.
- Now: its fortune in words with what moved it and a small line of the last thirty days; the world events at it; a
  post's warning; a camp's raids; a hamlet's ask; a mission there.
- Who: its notables with portrait, name, title and regard in words, which colonists they know (links), a letter on the
  road shown as steps; its founding families as house links.
- Ties (stage 2): each tie, its terms and since when.
- Words: Heard lately (today's `OutsiderHeard`), what its head remembers.
- Story: founded when, by whom, from which town, why; the three newest history lines; "Its whole history (14)".
- Acts: Write to someone there, Plan a journey here, and from stage 2 Ask a favour, Speak of a pact, Send the envoy,
  each greyed with why not.

The same card opens from anywhere a town is named; "Show" on This week and the lists opens it on the map instead of
the world view.

### The Peoples view: a list beside a page (built 8 November 2026)

Step A of `docs/design/diplomacy-screen.md` (the player: "the diplomacy screen could be better displayed", "i also want
to quickly see and sort by who's closest"). The view's Peoples mode is a list on the left and the chosen people's page
on the right, in this grammar: the list is a search, filter chips with counts (component 15), a sort as modes
(component 2: Closest, the default, Regard, Name, Age), groups as folding section headings with counts (component 4:
allies, at peace, at war), and rows (component 5, 40 high) with the people's icon, a state's dot and word (component 7),
a meter and the nearest town's days in Tiny at the right; "All peoples" heads it, the web of who stands with whom (dots
and lines, a legend in words) ends it. The page is a head (a name as a link, an age chip, a stance chip, the regard
meter and a line of this season's trend), the five tabs as modes (Overview, Towns, People, Dealings, History), and the
tab's sections. "Do something with..." on the Overview is card acts with their why-not (component 9) for one of three
targets (the people, a town, a person), each act with one line of what it brings and one of how it travels and costs.
Widths: the list 320 beside a page of 618 at the standard window, 200 with compact rows beside 346 with the drawer open,
320 beside 888 in the large window; the web fixed at the list's foot at 1080 and in its scroll at 720
(`PeoplesViewMath`). Faces stays the second mode. Choosing a people lights its towns on the map; the map's List has a
"Closest first" chip. DESIGN.md, "The diplomacy screen: the peoples beside their page", has the whole of it.

### This week: one inbox

This week already gathers most of what waits on the colony in twelve cards. It becomes the one inbox:

- **Following**: what the player follows, with each one's newest change.
- **Needs you**: wishes and petitions, letters waiting for an answer, promises at risk, the council's questions, and
  from the region and stage 2 every offer, ask and call (a favour asked, an offer for a captive, tribute expected, a
  call to arms), each row with the thing linked, one sentence, and its act ("Answer", "Decide", "Look", "Pay").
- **Under way**: letters on the road, journeys and caravans, missions taken on, an envoy staying, council tasks,
  schemes of ours, settlers on the road to a hamlet; each in steps (written, on the road, arrived, answered), never a
  countdown, as the player's rule asks.
- **Coming**: the calendar's days by date, fights promised, missions offered, feast days.
- **How people are**: strain, health, trust and the mood balance, as today.
- **The plan**: the advisor's two most pressing steps, leading to Planning → Plan.

Each card's heading opens its view; "and 3 more" opens the rest in place. The count of what needs the player (the
`NeedsCount` of today, with stage 2's asks) is one number shown in three places: This week's tab, the inbox icon in
every window's corner, and the council button on the bar (a setting). The game's letter stack stays the game's; This
week lists the letters in it that wait for an answer. Rows about places open the place's card on the region map.

Features add to it with `Inbox.Register(provider)`, the twelve cards of `WeekModel` being the first providers.

### Where stage 2 and stage 3 go

| Item | Its cards | Where it shows | Where the player acts | In This week |
|---|---|---|---|---|
| Favours owed and called in | Tie; Person, People, Place (Ties) | Peoples: a strip of what is owed each way; Stance lens dotted lines | the card's acts, "Talk with...", letters | a favour asked of us |
| Pacts and truces | Tie | Stance lens lines; Threats lens dims a truce partner | People card acts, letters | a call to arms; a truce broken |
| Ransom | Person (the captive), Tie | the captive's card | the offer's act, letters | an offer for a captive |
| The envoy | Person | Here and away (here; ours away) | Person card, "Talk with..." | the envoy's asks; the stay under way |
| Feasts and contests | Event | Calendar, Here and away (guests) | the calendar day, the council question | a feast day coming |
| Marriage alliances | Tie, House, Person | Person and House cards | council question, "Talk with..." | the match proposed |
| Wards and hostages | Tie, Person | Here and away | Person card | a decision on a ward or hostage |
| Keeper of secrets, council tasks (dropped, 5 November 2026) | Colonist, Event | Council → Offices; Here and away (tasks) | the task's acts | a step that needs the player; tasks under way |
| Fealty | Tie | Stance lens (a line to the liege), People card | People card acts, council question | tribute expected, levies called |
| Vassals of our own | Place, Tie | Ours lens | Place card acts | a vassal's asks |
| Schemes | Tie, Event | Here and away (ours; found out against us) | Person card acts | a scheme found out |
| House renown | House | People → Houses; House cards | none (earned) | none |
| Stress against one's nature | Colonist | the colonist's card (the heart) | none | strain, as today |
| Many colonies (stage 3) | Place (a colony) | Ours lens with the colonies' list and the colonies of the past; a colony chip in every view's header for the colony in view | the colony card's acts: go there, make it the seat, hand it over, found a new colony | a threat at another home |
| Handing a colony over | Place | the hand-over window, a one-off action | from the colony card and the game's world button | the meeting's vote |
| A fresh start | Place | the founding targeter's panel | Ours lens: "Found a new colony" | the party under way |

No item makes a tab. Stage 2's two pages are the Peoples view and Here and away; stage 3's Colonies page is the Ours
lens with colony cards (decision 7).

### Where a new thing goes (the rule)

For the builders who come later. Ask in this order and stop at the first yes:

1. **Does it wait on the player?** An answer, an offer, a call, a warning: an inbox row through `Inbox.Register`, its
   act on the card of the thing it is about. Never a page.
2. **Is it a thing?** A person, place, people, house, tie, event or letter: it uses that kind of card. A new kind of
   card only for a new noun the game has never had.
3. **Is it about a place?** A section in one of the place card's slots, and if it can be seen on the land, marks in the
   lens that reads it. A new lens only by replacing one.
4. **Is it a new way to look at things already listed?** A filter chip, or a mode of an existing view (five at most).
5. **Is it something the colony does?** A card act with its why-not, and in "Talk with..." if it is done face to face.
6. **Is it a record of what happened?** A history kind in its family: it then shows in the timeline and in the
   story of every thing it names.
7. **Is it an explanation?** An Almanac article, linked from the view's "?".
8. **None of these, and the player will open it every week?** A view, in the window it belongs to, if that window has
   fewer than five. A new window never.

In code: `MainTabWindow_World.Register` stays only to forward old addresses; new code uses `Lenses.Register`,
`CardSections.Register`, `CardActs.Register`, `Inbox.Register`, `Search.Register` and history kinds.

### The look

Rimfolk's windows have a look of their own since 8 November 2026 (the player: "you really don't have to stick to the
rimworld design theme if you can otherwise make all uis clearer, better to use, better looking, modern etc.";
`docs/design/rimfolk-look.md`): a frame of Rimfolk's own (a drawn title bar with the title, the window's tools, large
and close; moved by its title bar, sized from its edges, large at about 92% of the screen, remembered between games),
a calm dark slate ground, rounded raised cards with a faint edge and a soft shadow, flat buttons with hover and
pressed states, tabs that read as tabs, segmented modes, pills for chips, a switch, a text field whose focus shows, a
chosen row lit by the accent's edge, slim scrollbars, one amber accent and the stances in blue and orange, always with
their word. The game's look stays as a setting ("Rimfolk's own look" off), drawn exactly as before. Stage 1 (built) is
the frame and the kit on the region and trade windows; stage 2 the council's windows, Planning, Defenses, the Almanac
and Rimfolk's dialogs. Underneath, one stricter grammar either way: Rimfolk already had most of the parts (`UiKit`'s
cards, chips, pills, meters, section titles, empty states, `Folds`); this names them, gives each one measure, and
draws them the same way everywhere.

#### Spacing and sizes

- A 4 pixel grid. Steps of 4, 8, 12, 16, 24, 32.
- Window and drawer padding 12 (as `ContractedBy(12)` now). Between sections 16, between cards 8, inside a card 10.
- Row heights: 22 for a dense list, 28 for a standard row and buttons, 40 for a row with a portrait, 56 for a card's
  head. Icons 14 in a dense row, 18 in a row, 24 in the corner and on a card's head.
- The drawer 380 wide (340 at the narrowest), the map's list rows 44.

#### Type

Three sizes, the game's own: Medium for one title per view or card, Small for everything read, Tiny for what is
glanced at (dates, distances, a count's word). No bold in running text (the game's fonts have none and Unity's faux
bold blurs at Tiny); hierarchy comes from size and ink. `TextScale` sets the Almanac's reading size and could scale a
card's title, never a single word. Numbers right-aligned in columns.

#### Colour

Colour carries meaning only, and always beside a word or an icon, never alone (for colour blindness):

| Token | Meaning | Today's |
|---|---|---|
| Ink, Faint, Dim | text, secondary, quiet | `Color.white`, `UiKit.Faint`, `UiKit.Dim` |
| Gold | ours, chosen, the colony | `UiKit.Gold` |
| Link | a name that leads to its card | new: a light parchment with a dotted line under it |
| Warm, Cool, Sour | a stance: friendly or allied, neutral or wary, hostile | `UiKit.Good`, a new slate blue, `UiKit.Bad` |
| Soon, Danger | needs the player soon; harm now | `UiKit.Warn`, a brighter red with the warning icon |
| Done | finished, kept, answered | new: a muted green-grey with a tick |
| World | world events and news from afar | `UiKit.WorldColor` |
| Surfaces | card, hover, chosen, border, divider | `UiKit.CardFill` and the rest, as today |

The page accents (the Peoples page's violet, the posts' tan) give way to these: a view needs no colour of its own when
its icon and title say what it is.

In Rimfolk's own look each token takes the mockup's palette (`UI/SkinMath.cs`; every text colour at least 4.5:1 on the
window, a panel and a card, tested):

| Token | Rimfolk's own |
|---|---|
| Ink, Faint, Dim, Heading | #ece8df, #b0aa9c, #9c968a, #e8d5a6 |
| Gold (the one accent) | #e0a845; a primary button's word on it #17191b |
| Link, LinkHot | #eed8a4, #fff1cf |
| Warm, Cool, Sour | allied and friendly blue #6aa6dc, neutral #a9b1ba, hostile orange #e08a45 |
| Soon, Danger, Done, World | #e6c35c, #f27466, #9db89f, #b4a7e2 |
| Surfaces | the window #17191b, a panel #212427, a card #2a2e32 (under the mouse #33383d, held #1b1d20), edges #3a3f44 (#4f565c under the mouse) |

#### Components

Each in `UiKit` with a measure and a draw, so layout is worked out once per width:

1. **View header**: icon, title, one line, stat on the right, modes, "?".
2. **Modes**: up to five small buttons in a row, one lit.
3. **Lens bar**: icon and name per lens, one lit, the legend at the map's foot.
4. **Section heading**: name, count, fold caret, an optional act on the right.
5. **Row**: picture, the line with links, a second line in Tiny, a note or an act at the right.
6. **Thing chip**: picture and name, a link with a preview.
7. **State chip**: a dot and a word ("friendly", "thriving").
8. **Link**: inline in a sentence.
9. **Act button with why-not**: greyed with the reason as its tooltip, and in a card one line of the reason under it.
10. **Steps**: three to five dots with words for what is under way; no time left.
11. **Line of days**: a small line of 7 or 30 days with its area filled and its last point marked, and a word ("rising").
12. **Empty state**: an icon and one sentence of what will show here and what brings it.
13. **Drawer**: the trail, the card, its acts at the foot.
14. **Preview**: a small card at a link, entered to keep it.
15. **Filter chips** and **count badge**.

Rimfolk's own look adds (`UI/UiKitSkin.cs`, `UI/WindowFrame.cs`; each draws the game's look when the setting is off):

16. **Window frame**: the title bar (36 high: icon, title, the window's tools, large, close), the edges' grips (6, a
    corner 16), the body inset 10; its rules in `WindowFrameMath`.
17. **Surfaces**: a rounded fill, edge and box (radii 4, 6, 8, 10), a tab's rounded top, a corner mask, a soft shadow,
    a card (`OwnCard`).
18. **Button**: normal, primary (the accent) and quiet; hover, held, greyed with why; `IconPlate` for a square one.
19. **Segmented control** (the modes), **tabs** (`OwnTab`, the lit one joined to its panel), **switch** (`Toggle`),
    **text field** and **search field** (the accent's edge while focused), **lit row** (`RowLit`, the accent's edge) and
    `RowHover`, **scrollbars** that blend in.

#### Icons

Today's set (about seventy white silhouettes, tinted) stays. New ones: six lenses, back, forward, search, following,
the inbox, close, More. Drawn at 64 pixels, shown at 14 to 24.

### What it takes in the game's interface code

All of it is the game's immediate interface (`Verse.Widgets`, `Text`, `GUI`, `Window`, `TooltipHandler`), as Rimfolk
is drawn today.

- **Things.** `ThingRef` wraps the ledger's `Ref` (persons, towns, peoples, colonies, houses) and adds colonists, ties,
  history entries, letters and pages. A card's model is built once a second while shown (`UiCache`), from the ledger,
  the history and the facts.
- **Link text.** `LinkText` reads the markers into runs (plain or a link with its ref), lays the words out for a width
  once (one `Text.CalcSize` per word, kept), and draws each run with `Widgets.Label`; each link run keeps its rect for
  the mouse. The layout is pure arithmetic (`LinkTextMath`, tested). History's 300 lines are laid out only as they
  scroll into sight, as the History page already draws only the rows in sight.
- **Previews.** The game's tooltip is one at a time, follows the mouse and takes no clicks, so it can't be entered or
  nested. A preview is Rimfolk's own: an immediate window (`Find.WindowStack.ImmediateWindow`, the layer the game's
  tooltips use) anchored at the link, drawn while alive, taking the mouse when entered.
- **The drawer** is a rect in the window with its own scroll view; over the view it draws a shadow and takes the
  clicks inside it.
- **Lenses** recolour the marks drawn each frame (as today) and draw lines with `Widgets.DrawLine`, some forty towns at
  most; no new picture per lens. A tint of territory (Faction Territories) would need a second picture made when the
  lens is chosen: later, if wanted.
- **The trail** is a short list of small records; nothing made per frame.
- **Addresses.** `PagesMath` becomes the list of views and modes; an address is window, view, mode or lens, and card.
  `Moved` gains every old page name (Neighbours, Letters, Journeys, Events, TradingPosts, RaidersCamps, Hamlets,
  WorldHistory, Roster, Everyone, Dreams, Journals, Priorities, Build, Almanac and the rest) with the address it is now,
  so every caller, key and other mod that asks `Pages.Open` for an old name lands in the right place.
- **Saved.** Only the followed things (at most twelve refs); a model goes into `Tools/save_estimate.py`. Lens, modes,
  folds and the drawer's state are kept in the settings file like `PageMemory`, or for the session.

- **The frame and the floating window.** `WindowFrame` is held by a window and called from its hooks: it places the
  window in `SetInitialSizeAndPosition` (the remembered place fitted to this screen) and `Notify_ResolutionChanged`,
  follows the mouse in `WindowOnGUI` before the game's `GUI.Window` (polling the button and the mouse, so a fast drag
  never loses the window), and draws the title bar and the grips in the window's contents with margin 0 and no game
  background. The region window floats as a plain `Window` (the interface layer, not closed by clicks outside it)
  rather than a main tab, because the game closes a main tab on any click on the map and whenever something is
  selected; it stays one of the four windows through `Pages` (DESIGN.md, "Rimfolk's own look, stage 1").
- **Textures.** The rounded shapes are a few white textures made once at load (two texels a unit) and drawn as 9-slices
  with `GUI.DrawTextureWithTexCoords`, tinted by `GUI.color`: nine draws a shape, nothing made per frame. The scrollbars
  are the game's own styles with their thumb and track swapped while a framed window draws, put back after. The
  tokens are properties choosing the game's value or the own look's, set around a framed window's contents.

#### Performance

Nothing is built while the windows are closed. While open: each view's model at most once a second (twice for the
counts), cards when shown, previews when they show, the search index when the field is clicked. Layout per width,
drawing only what is in sight. The lens lines and marks are a few dozen draws.

#### Screens and interface scale

The windows are 1010 by 720 or the screen less its edges, whichever is smaller (`WindowTabs.Size`). At 1920 by 1080 and
scale 1 the map is 560 wide beside a 380 drawer. At 1080p with scale 1.5, or 4K with scale 3, the window is 1010 by
about 665 and the drawer still fits beside the map (a content area of 950). Below a 900 content width the drawer
slides over the view. The chrome fits at 1010: five tabs of about 140 and a corner of about 310 (back and forward, search, the inbox with
its count, following, the four windows). A layout test like
`TabFitMath`'s checks screens from 768 to 2160 pixels high and scales up to 2. Larger windows on large screens are
decision 6.

#### What is costly or risky

- **Previews' input.** An immediate window that takes the mouse must give it back cleanly to the window under it and
  to the game's own tooltips; checked by hand in the game, with a setting to turn previews off.
- **Markers leaking.** A marker reaching the game would show as raw text. Links are asked for by the drawing code only,
  and a test fills every keyed text both ways and checks the plain one has no marker.
- **The mouse's side buttons.** Unity's interface events may not report them on every system; the arrows and keys
  always work.
- **Rimfolk's own letters with links.** Rimfolk's thirteen letter kinds open the game's dialog; drawing them in a
  Rimfolk window with linked names means doing their choices as the game does. Later and only if it plays cleanly.
- **Other mods' links into Rimfolk.** Old addresses forward; `MainTabWindow_World.Register` keeps working, its page
  shown in the History view's "More" until it is moved.

## The order to build it in

Each release plays as before, old addresses forward through `Pages.Open`, and stage 2a.1 waits until the fifth is out
(the player: "prioritize the ui").

| Release | What is built | What the player sees |
|---|---|---|
| UI 1, names that lead somewhere | `ThingRef`, the eight card builders (Colonist reuses the Roster's page, Place the map's panel), card slots with the fourteen panel sections moved in, the drawer in the region window, `LinkText` in the world history, the place card and the Neighbours, Peoples and Letters rows; chips as links (a people's chip, a notable's portrait, a town's name); previews one level deep; back and forward in every corner; "Show" opens the place on the region map | Every name in the region window and its history opens its card beside what they were looking at; a pause on a name shows who or what it is; the back arrow |
| UI 2, the region in four | Map with six lenses and Map or List; Peoples with Faces; Here and away; History with the scope chips and the events strip; the ten old pages forward | Four tabs instead of ten; posts, camps and hamlets as lenses with their lists; a town's whole story in its card |
| UI 3, one inbox and search | `Inbox` providers, Under way in steps, Following for any thing (and its panel), the corner's inbox and count in all four windows, the count on the council button, search in the corner | This week as the one place where things wait; a count that is the same everywhere; find anyone by name |
| UI 4, the council in four | This week, People (modes, the person's card in the drawer, dreams and journals in it), Council (modes), Colony; Priorities into Plan; Build and Rooms as one view; the Almanac in its own window with "?" on every view | One row of tabs in the council window; the Almanac beside whatever it explains |
| UI 5, one look | the tokens and components across every window, Defense and Planning in the same grammar, names in the inspect tabs and the Social column as links, the second level of previews, the layout test across screens | Everything reads alike; links everywhere Rimfolk draws a name |

Stage 2a.1 then adds the Ties slot, the owed strip on Peoples and its inbox providers, and makes no tab.

## Tests and checks

- `PagesTests`: every old page name and every `Moved` name resolves to a view, mode or lens; no window has more than
  five views or a view more than five modes; lenses at most six.
- `LinkTextTests`: markers parse; the layout of runs for a width matches `Text`'s wrapping on samples; the plain text of
  every keyed text has no marker (with `FactsTests`' fill over every placeholder text).
- `NavTrailTests`: back, forward, the bound of 50, a step dropped when its thing is gone.
- `InboxTests`: the count equals today's `NeedsCount` for the same state; rows sorted as This week sorts.
- `CardSlotTests`: each registered section has a slot; the place card's order.
- A layout test for the chrome, the drawer and the map at screens from 768 to 2160 pixels and scales up to 2.
- The save model for following; the self-check lists the new patches if any (none planned: all of it is Rimfolk's own
  windows).
- In the game: previews with the game's tooltips around them, the drawer at 1080p scale 1.5, the side mouse buttons,
  every old page key.

## Decisions for the player

1. **The region in four views.** Map (with lenses), Peoples, Here and away, History; trading posts, raiders' camps and
   hamlets become the Trade, Threats and Ours lenses with their lists; stage 2's Diplomacy and Household pages become
   Peoples and Here and away. Or keep pages and only group the tabs? Recommended: the four views.
2. **Where a card opens.** In a drawer at the right of the window the player is in (the region map's panel being that
   drawer), a floating card only from outside Rimfolk's windows; or always in the region or council window as a full
   page? Recommended: the drawer.
3. **Previews.** After a short pause, kept when the mouse enters them, two levels at most, with a setting; or the game's
   plain tooltips only? Recommended: previews.
4. **The Almanac.** Its own movable window, reached by "?" on every view and in the corner, so it can sit beside what it
   explains; or the council window's fourth section as now? Recommended: its own window.
5. **The council window in four views.** This week, People, Council, Colony, with modes; Dreams and Journals in the
   person's card and the Chronicle; Priorities in Planning; or keep the two rows of tabs? Recommended: four views.
6. **Window size.** The four windows grow on large screens up to 1280 by 860 (a setting: standard or large), or stay
   1010 by 720? Recommended: the setting, standard by default.
7. **The colonies (stage 3).** In the Ours lens with a card per colony and a colony chip in every view's header, rather
   than a page in the council's Colony section? Recommended: the Ours lens.

## The player's decisions (4 November 2026)
All seven as recommended ("Go for your recommended ui suggestions"): the region in four views with lenses; cards in
the drawer; previews that stay when entered, two levels at most, with a setting; the Almanac in its own window; the
council in four views; the window size setting, standard by default; the colonies of stage 3 in the Ours lens. The
last two change where stage 3's design and today's Colony section put things: Priorities moves to Planning → Plan, and
stage 3's Colonies page becomes the Ours lens.

## Beyond Rimfolk's windows (the player, 4 November 2026)
The player's word: if Rimfolk's windows come out as the mockup shows, more of the game's own interface may be reworked
in the same language later. So that the work of UI 1 to UI 5 carries over:
- **The kit stands on its own.** `UiKit`/`UiKitCards`, `LinkText`, the drawer, previews, cards and the trail take a rect
  and a model and nothing of Rimfolk's windows, so they can be drawn inside one of the game's windows or a patch on it.
- **Cards for the game's own things.** `ThingRef` and the card kinds leave room for more nouns (an item, an animal, a
  building, a faction leader the game knows, a quest); the game's own info card stays reachable from a card's head.
- **Each reworked screen behind its own setting**, off falling back to the game's own, so a player (or another mod)
  can keep the vanilla screen; the game's hotkeys and its own behaviour are kept.
- **Living beside other interface mods.** The player uses RimHUD (the guest checklist already makes room for it);
  any rework reads what such a mod changed and steps aside where both would draw.
- **What could come**, only if the player asks: the inspect pane and its tabs, the colonist bar's hover, the letter
  stack, the world map's inspect pane, the work and assign tabs, the trade window. Each would be designed first, with
  the player's choices, as this one was.

