# The diplomacy screen, and dealing with a people or a town as a whole

The player (8 November 2026): "further work on the whole map/diplomacy and including aspects ui and ux. i like your
map, but the diplomacy screen could be better displayed. also i want to be able to do some interactions with another
base/faction as a whole, rather than contacting a single person. like some business is just targeted at their whole base
and they can receive it as such, rather than wanting to exchange something with unique people". On the proposal: "very
good, all of it! i also want to quickly see and sort by who's closest".

Where things stood: the region window's Peoples view (`UI/Region/PeoplesView.cs`) is one long scroll: what is owed each
way, a card per people (stance, kind, wars and events, its head, ties, what is under way, the acts that fit), then what
the peoples remember; Faces lists the people known by name. Acts sit inside the cards. Everything addressed to a people
went through one of its people (a notable, an envoy, a messenger).

Builds after the post's step 1 (`post-and-letters.md`): both touch the town cards and the letters.

## Step A: the screen

*Built (8 November 2026), points 1 to 5, in `Source/ColonyCouncil/UI/Region/` (`PeoplesView`, `PeoplesList`, `PeoplePage`,
`PeopleActs`, `PeoplesModel`; the pure rules `PeoplesViewMath`, `PeopleActMath`; DESIGN.md "The diplomacy screen: the
peoples beside their page"). The distance is the post's reach rule (days on the road at the colony's means over the
land's own way); "all peoples" heads the list with what is owed each way and what each remembers; Faces stays the view's
second mode. Every act runs through its own code path; town-level dealings are step B.*

1. **A list of peoples on the left**: grouped allies, neutral, at war; search; filters (neighbours, unread mail, has
   dealings); **sort by distance (closest first), by regard, by name or by age**, distance the default the player can
   change. Each row: name, a small regard bar, stance icon, **its nearest town's distance** (in days by the colony's
   usual way of travel), unread mail.
2. **A page per people with tabs**:
   - Overview: age and heading (0.169.0), regard with its trend this season and the reasons, what they want and fear,
     and how to reach them now (the post's reach line).
   - Towns: each town with distance, size and how to reach it, closest first.
   - People: their notables (today's Faces for this people).
   - Dealings: pacts, contracts, what is owed each way, what is under way.
   - History: their timeline with the colony.
3. **"Do something with…"**: three targets, the people as a whole, one of their towns, one person. Every act says what
   it brings, how it travels (post, rider, radio) and what it costs; the ones not open now are greyed with why.
4. **On the region map**: choosing a people lights its towns and shows each one's distance; the map's own list of towns
   can be sorted by distance too.
5. **A small web** of who is allied with or at war with whom.

## Step B: dealing with a people or a town as a whole

A town's council or a people's leaders receive these, not one person; they travel by the post, the colony's rider or
radio as letters do, and the answer comes the same way.

*Built (8 November 2026), points 6 to 13, in `Source/ColonyCouncil/Dealings/` (`Dealings`, `DealingLetters`, `DealingActs`,
`DealingBook`, `DealingFestival`, `DealingPatches`; the pure rules `DealingMath`, `Tools/Tests/DealingTests.cs`; DESIGN.md
"Dealings with a whole people or town, pen friends and the world writing first"). One letter purpose (`Dealing`) to the
town's head for its council, carried and answered as letters are; the answer by their regard and ways; each plays out on
the map when the answer is in (their porters at the gate, their caravans and visitors, a camp of theirs, a road on the
world map, word of a raid walked in). Point 9's builders and caravan escort were left out: a camp can't build the
colony's blueprints and the game's caravans take only the colony's own; the crew is harvest hands, the guards the Ashen
Banner's contract for any people. Point 11: a road is the game's own road on the world (caravans and the post go faster
by the game's path), a well or a bridge betters their town and leaves them owing, a watchtower's watch sends word of
raids from that side.*

6. **Supply contracts** with a town: the colony delivers goods each season for silver, or they deliver to the colony;
   kept or broken, the whole people remembers.
7. **Relief** for a town in hard times the colony has heard of (famine, plague, a burned quarter): food or medicine sent
   to the town, remembered by all its people.
8. **Proclamations**: a fair or market day at the colony, friendship declared, the colony's trade terms posted; whole
   towns answer, and more visitors and traders come.
9. **Hiring from a town**: a work crew (builders, harvest hands), a guard detail or a caravan escort, as a group, for a
   season.
10. **Petitions to their council**: about raiders from their people, for reparations, to settle refugees with them or
    take theirs in.
11. **Joint works**: a road between, a shared well, a bridge, a border watchtower; each side brings its share, both towns
    benefit.
12. **Market rights**: a trading post or stall in their town, better prices, their goods coming more often.
13. **Invite a whole town** to the colony's feast or fair, or send the colony's entertainers to their festival.

## Order of building

Step A, then step B (same area), after the post's step 1.
