# Rimfolk's peoples

New peoples (factions) for Rimfolk, built mostly from what the game, the DLCs and the player's mods already draw. The
player's words, 7 November 2026: "we can start with a few factions and see how it goes, and then further expand on
those. reuse and create at your own discretion. they should also be able to arise during play so that i will see new
factions in an existing run", and "use all dlc and mods to your availability".

Two releases. **Release A** (this one, 7 November 2026): three peoples of peaceful callings the world lacked, made when
a new world is made. **Release B** (next): peoples that arise during play, so an existing save sees new ones, built on
release A's templates and code.

## What was built (release A)

Three FactionDefs in `Defs/PeopleDefs/`, each on the game's own abstract parent, with its pawn kinds, traders, names,
fixed faith, xenotype share, colours and a `PeopleExtension` (the template Rimfolk reads); emblems and town icons
drawn in code; their towns' plans; and their places in Rimfolk's camps, asks, callers, letters, talk and world events.
Code in `Source/ColonyCouncil/Peoples/`: `PeoplesMath.cs` (pure, tested: `Tools/Tests/PeoplesTests.cs`),
`PeopleDefs.cs` (the template and the plan defs), `Peoples.cs` (the registry and every question Rimfolk asks),
`PeopleTowns.cs` (a town's map from its plan), `PeoplesAsks.cs` (an ask from a people of a calling),
`PeoplesDebug.cs` (dev actions).

| | The road league | The hospitaller order | The herding folk |
|---|---|---|---|
| defName | `Rimfolk_League` | `Rimfolk_Order` | `Rimfolk_Herders` |
| Calling | traders | healers | herders |
| Age | Industrial (outlander parent) | Medieval (tribal parent) | Neolithic (tribal parent) |
| Look | dusters dyed teal, cowboy hats; outriders in flak and helmets; the factor's bowler, the league master's top hat (Royalty) | ivory robes and hoods; sworn guards in plate and helmets; lay archers | russet hides; fur tail caps (Ideology), broadwraps (Ideology), war veils |
| Weapons | the game's gun tags | knives, maces, swords; recurve bows | clubs, spears, ikwas; recurve and great bows |
| Leader | warden of the roads | prior or prioress | first herder |
| Town head, heir, envoy, trader | waymaster, second waymaster, league factor, caravan master | prior or prioress, subprior or subprioress, almoner, cellarer | camp elder, second elder, rider, drover |
| Messengers | road riders | postulants | riders |
| Towns | few (weight 0.4): walled waystations | sickhouses (0.8) | camps (0.6) |
| Caravans | three times as likely as another's; rich and varied stock | an almoner's caravan of medicine and plain goods | half again as likely; a herd of 8 to 14 for sale walks with it |
| Raids | 0.35 of another people's commonality | 0.25 | 0.5 |
| Faith (Ideology) | The Long Road: collectivist (and VE Memes' trader); ideological or abstract; rustic style | The Open Hand: guilty, flesh purity (and VE Memes' healthcare); abstract or Christian origin; Christian style | The Long Grass: rancher (and VE Memes' nomad); animist or embodied; animalist style |
| Kinds (Biotech) | hussar, genie, highmate, starjack (Odyssey) | genie | yttakin, pigskin, neanderthal, Alpha Genes' animusen |
| Colours | teal | ivory | russet |

**Gear** is the game's own apparel and weapon tags (`IndustrialBasic`, `IndustrialMilitaryBasic`, `Neolithic`, `Gun`,
`MedievalMeleeDecent`...), which the VE apparel, armour and weapons sets add to when loaded, plus a few required pieces
of the base game's (the duster, the robe, plate) and the DLCs' (with `MayRequire`), all dyed the people's colour
(`apparelColor`). An ordinary member forces no skill (a joiner made from one is no healer or trader by force), except
the herders' herding.

**At a world's making**: each is listed once among the world's peoples (`startingCountAtWorldCreation` 1,
`maxConfigurableAtWorldCreation` 9999), the game's usual way; never required (`requiredCountAtGameStart` 0). The player
can remove them or add more on the world's page.

**Raids**: never natural or permanent enemies, no sieges, a low raid commonality. They raid only when the colony has
turned them hostile, by the game's own rules.

**Emblems** (`Tools/make_emblems.py`, the shape helpers of `Tools/make_icons.py`, drawn at 512 and scaled to 128, white
on transparent, tinted by the game): the league's scales hung from a waystone, the order's open hand with a lamp's
flame in its palm, the herders' crook across a horn. Town icons for the world map: a walled waystation with its arched
gate and pennant, a gabled sickhouse with its lantern tower, a round felt house between two tents. Preview:
`Tools/emblems_preview.png`. Files: `Textures/ColonyCouncil/Peoples/League.png`, `LeagueTown.png`, `Order.png`,
`OrderTown.png`, `Herders.png`, `HerdersTown.png`.

**Their towns' maps** (`Defs/PeopleDefs/Rimfolk_TownPlans.xml`, `TownPlanDef`; `Peoples/PeopleTowns.cs`): rows of
characters from the north, each a key (a building, its material or the town's wall material, its turn, the floor, a
roof, what the cell is for). Read into the kept-layout grid (`PeoplesMath.Grid`, the game's footprint rule
`PeoplesMath.Footprint`) and placed by `TownLayouts.Place` from the same prefix on the game's settlement step that
places a town the colony was (with the deeper game hooks on), near the map's middle where at most 6% of the cells it
uses are bad ground (water, marsh, impassable). Then the game's own loot in the storehouse, the people's own goods, the
herd in the pens, the garden's healroot, and its people at home: its notables, then the game's settlement group under
its defend-the-town lord. The league's waystation (33 by 17): a walled yard with market stalls and a fire, two rooms of
beds, a storehouse, the waymaster's hall, a gate and two pens. The order's sickhouse (31 by 18): a ward of sixteen beds,
a cloister yard with herb beds round a fire, a storehouse of medicine, a refectory, the hospitallers' own cells. The
herders' camp (37 by 23): five round houses, a fire, a roofed shelter on four posts for hay and hides, two great pens.
Only the base game's buildings. Vanilla Base Generation Expanded's layouts stand down for these towns (its settlement
step is the game's kind); where the land won't take the plan, its layout or the game's own base is made.

**In Rimfolk's systems**:
- Notables (`NotableBook.TitleOf`, `RegionFaces.TitleOf`): titled by their people's ways.
- Letters by their means: their answers in their own words (`Rimfolk_Pool_NeighbourWrites`, tagged `their:CALLING`),
  their messengers by their own word (`CourierWords.Noun`) and lines (`Rimfolk_CourierLines`).
- Talk: the facts service gives `their:traders`, `their:healers`, `their:herders`; nine pieces of talk in their voices.
  The same tag in the asks', the camps', the events', the bubbles', the visitors' and the world events' lines.
- Camps: each favours some of Rimfolk's kinds (four times as likely from them): the league's market folk, salt
  caravans, far merchants and road wardens; the order's healers, lone healer, herb-gatherers, pilgrims and penitents;
  the herders' drovers, shearers and beast-catchers. One new kind only theirs: the herders' **winter herd**
  (`Rimfolk_Camp_WinterHerd`, fall and winter, a herd of up to twelve head and a dog, eight to thirteen days, hay of
  their own; `CampKindDef.calling`, `seasons`, `maxAnimals`).
- Asks (`MissionPeople` on a mission: `only`, `prefers`, `whileKind`): the order's **medicine for the sickhouse** in a
  plague year (a delivery: their carts come for it); the league's **factor to see home** (the traveller's escort); the
  healer lent to a sickhouse likelier from the order and the fodder for a herd from the herders (by patch). An ask only
  theirs takes no share of the missions' draw in a world without them (`MissionPool`).
- Callers (`HappeningSetting.people` `calling:CALLING`): the order's **hospitaller at the gate** (free care for a
  colonist with an ailment, medicine sent back, the colony's doctor learning); the league's **outrider** (the league's
  own caravan turns aside to the colony when bidden; only when it can come now, the need `caravan`).
- World events (`SituationFactionNeed.calling`, by patch): the drove roads and the great herds prefer the herders; a
  great caravan, a trading season, market days, the road in use, a wardens' post and a new road the league.
- The game's caravans (`Patch_RiseFallCaravans`): each people's `caravanWeight` in the game's choice of whose comes.
- Sickness brought home: a sickhouse of the order sees to the colony's lodgers as medicine in their packs would.
- The hand-over's peoples never borrow these peoples' kinds (`HandOverPeoples.Lend`).
- An Almanac page: *the road league, the hospitallers and the herders*.

Nothing new is saved and nothing runs on a clock of its own.

## Softness

| Missing | What happens |
|---|---|
| Ideology | No fixed faith, no style; the game's own faith for a people without it. Tail caps and broadwraps aren't worn; the work-camp groups aren't there. |
| Royalty | The league master wears no top hat. |
| Biotech | Humans only; no children in their peaceful groups. |
| Odyssey | No starjacks with the league. |
| VE Memes | Their faiths have only the game's memes (one or two each). |
| Alpha Genes | No animusen with the herders. |
| VE apparel, armour, weapons | The base game's gear under the same tags. |
| Vanilla Base Generation Expanded | Nothing changes: their towns are made from their plans either way. |
| RimMarket | Their camps don't buy at the colony's stalls; nothing else changes. |
| The deeper game hooks (setting) | The game's own bases for their towns, and their caravans at the game's even chance. |
| Rimfolk itself | A save holding one needs Rimfolk, as for any mod's people (the game drops a people whose def is gone). |

Not used: Vanilla Races Expanded's xenotypes (their defNames couldn't be checked here), the Sophian style (not
checked), Vanilla Base Generation Expanded's layouts for these towns (Rimfolk's own plans instead).

## For release B (peoples that arise during play)

What release B can reuse:

- **The templates**: every FactionDef with a `PeopleExtension` (`Peoples.Templates`): `Rimfolk_League`,
  `Rimfolk_Order`, `Rimfolk_Herders`. A people that arises is one of these (a splinter of a league, a new order, a herd
  folk come down from the mountains), or a new def written the same way: a FactionDef on the game's parent with the
  template; Rimfolk's systems then know it by its calling with no code.
- **Making one**: `Peoples.Make(def, nearMap)` makes a people of a template the game's way
  (`FactionGenerator.CreateFactionAndAddToManager`: its fixed faith, leader, relations, one town) and moves its town a
  few days from the home. Release B adds when and why (a town's split, a ruin resettled, a caravan that stays), the
  news, and the relations a new people starts with.
- **The callings**: `PeoplesMath.Callings` and their tags. A new calling is a new string there, its lines in the pools
  (`their:CALLING`), and its own words in its template.
- **The emblem pipeline**: a function in `Tools/make_emblems.py` and a line in `GLYPHS`; the faction def's
  `factionIconPath` and `settlementTexturePath` name the files.
- **The plan pipeline**: a `TownPlanDef` (checked by `PeoplesTests` for every plan in `Defs/PeopleDefs/`) named in the
  template.
- **The asks and callers**: `MissionPeople` (`only`, `prefers`, `whileKind`) on any mission def, `calling:CALLING` on
  any caller, `calling` on any camp kind or world event's people.
- **The dev actions**: "Peoples: a people of a template now, near this home (testing)", "Peoples: Rimfolk's peoples in
  this world", "Peoples: one of their camps here now", "Peoples: lay a town's plan at the mouse (testing)".

Templates for release B to draw from (ideas, not built): a salt or river league (traders, the league's parent, a
river-town plan), a mountain hospice (healers, a stone hospice plan), a reindeer folk of the cold (herders, yaks and
the cold animals), a pilgrim order (healers' parent, pilgrim camps), a caravan town of former raiders turned traders.

## What could come next

- Their own world events: a league's toll war, an order's plague year with the colony's help asked by name, a great
  drove to the far market.
- A league trading post in the colony's region (Rimfolk's trading posts) and the league's book of fair dealers shown on
  the Peoples page.
- A visitor of the order who stays a season at the colony's hospital.
- The herders' winter herd leaving a calf or a lamb as thanks.
- Their notables' own small stories (a waymaster's caravan lost, a prior's illness).
