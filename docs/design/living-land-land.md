# The living land: the land's part (B1, B3, B5)

The player's wish (28 November 2026, `docs/HANDOFF.md`, "nature, the biosphere, weather, climate and geology made dynamic
and meaningful"): "I really like when there's kind of ecological dynamics going on ... how it develops also ... maybe
there is one, say, sort of disease going around that kills all kinds of plants on the map ... dynamic, realistic,
immersive, and yeah, meaningful and noticeable." From the proposal page they picked every idea; this is the land's part,
built beside the climate (`LandClimate`, in `Weather/`) and the ground (`Nature/Ground*`):

- **B1 Land that recovers and changes.** Burned or cleared ground comes back in stages over seasons (grass, bushes, young
  trees, woods); a felled wood left alone regrows; overgrazed land thins. A small pass each day over a few hundred cells.
- **B3 Sicknesses of wild plants and trees** (no mod for 1.6 or 1.7 adds them): a wilt from tree to tree of one kind, a
  rust that thins the grass (fewer grazers), a rot in the berry bushes. It spreads over days; the colony can fell or burn
  what is sick, or let it run; the land regrows after. Shown with the game's blighted look, a leafless state, a message,
  a line in the Almanac.
- **B5 Water that rises and falls.** Ponds and marsh edges swell in wet seasons and dry to mud, then soil, in droughts;
  fish follow. Rivers and the sea are left to Living Waters and Waves; only still water at its edges moves, and what was
  the map's own comes back.

Code: `Source/ColonyCouncil/Nature/Land*.cs`. Rules without game types: `LandMath.cs` (tested in
`Tools/Tests/LivingLandTests.cs`). Defs: `Defs/DilemmaDefs/Rimfolk_Land_Dilemmas.xml`,
`Defs/FlavorPoolDefs/Rimfolk_Land_Pools.xml`, `Defs/Misc/Rimfolk_Land_Misc.xml`. Words:
`Languages/English/Keyed/Rimfolk_Land.xml`. Almanac: "The land over the seasons" in
`Defs/AlmanacArticleDefs/Almanac_Council_Weather.xml`.

## How it runs

`LandBook` is a map component on each home map. On its own 250-tick slot (`Sched.RareLand` = 31, timed as
`Pass.RareLand`) it runs one of four parts a quarter of a day apart (`LandMath.PartAt`: the 24th cycle of each quarter,
each map two cycles later than the one before), so each part runs once a day:

| Part | File | Cells looked at, at most |
|---|---|---|
| The still water | `LandWater.cs` | 60 to find the edge, 100 edge cells worked (with their four neighbours) |
| The sickness | `LandSickness.cs` | every sick plant (140 at most) and 60 cells round them for the spread; 160 plants sampled when one begins |
| The patches coming back | `LandRegrowth.cs` | 160, shared among the patches |
| The pasture | `LandPasture.cs` | 4 grazing blocks, 36 cells each |

Every pass stays within `LandMath.PassCap` (200 cells); the tests check each. Burns and fellings are noted as they
happen by two cheap hooks (`LandPatches.cs`): `Plant.Kill` with the game's flame damage, and `Plant.PlantCollected` for
a tree cut by one of the colony's people; each joins or opens a patch (a box, at most 24 cells across) in a loop over ten
patches at most. With every setting off the passes stop, except to give back what the water and the grazing changed and
to end a sickness going on.

What is kept (saved as short lines; models in `Tools/save_estimate.py`): the patches (10 a map), the sickness going on
(one) with its sick plants by cell (140 at most), the cells the water left or came over with what each was (360), the
cells grazed bare with what each was (240), the grazing blocks (12). Everything the land changes in the terrain is kept,
so it can always be put back as the map's own; terrain is changed only through `map.terrainGrid.SetTerrain`, as the gods'
rich soil and Murmur's spring do.

## B1: land that comes back

The land counts **growing days**: days the weather record kept at 6 °C or more on average (the advisor's own winter,
`WeatherMath.ColdBelow`); before the record has a day, the land's usual for the time of year.

| Stage | From growing day | What the pass sets on bare ground in the patch (a pass, at most) |
|---|---|---|
| Grass | 2 | 14 grasses of the land's own (growth 25 to 60%) |
| Brush | 12 | 6 plants, brush and grass |
| Young trees | 25 (where wood stood) | 4 plants: young trees (5 to 15% grown, none within 2 cells of a tree or a building), brush, grass |
| Woods | 60 (where wood stood) | done: the news and the chronicle |

Where no wood stood (three trees or more lost), the patch is done after the brush, at 25 growing days. In a temperate
land (45 growing days a year) a burnt wood is green within days, brushy by the end of its first year and woods again in
its second (the tests: between 60 and 120 days). The young trees are of the kind that stood there (seven times in ten,
where the land grows it wild); after a wilt, of the other kinds. The plants are the land's own: the map's wild plant
spawner's list (the biome's, the tile's features', every mod's that gives this land a plant), by the game's weight for
each (`LandPlants.cs`; grass, brush and trees sorted by kind and name). Nothing is set in the home area, a zone, on a
floor or bridge, under a roof, on a marked cell or in snow; the game's own wild plants come up there too, as always.

A patch counts once enough is lost in it: a burn of a dozen plants, six trees the colony felled close together outside
its fields, three trees a wilt killed, ten grasses a rust killed; one that doesn't count within ten days is let go.

The news (flavour, the colony news): grass coming up, brush growing back, young trees coming up, and the woods grown back
(notable, and in the chronicle). **The Mossborn** (`Peoples`, the grove-keepers): a felled wood let grow back is told to
each Mossborn people at peace with the colony, 3 goodwill each (`GoodwillWhy.Ledger`). Their ledger
(`MossLedger.Notify_Felled`) no longer counts a sick wild tree the colony felled to stop a wilt. **Hollin**: a believer
who holds her may say she gave the wood back (`LandGods`, kept in the colony's book as their belief).

**Overgrazed land thins** (`LandPasture.cs`): the colony's grazing animals counted by block of 16 by 16 cells; in the
four most crowded (three animals or body size four), 36 cells sampled. Overgrazed when the grazers need more grass than
the block's good ground can keep up (about 12 cells to a body size of 1) and the grass covers under a third of it. Six
overgrazed days (one off for each day of rest) and the block wears: a few bare cells a growing day (5 at most) turn to
packed earth (rich soil to soil, soil and moss to packed dirt, marshy ground to mud), a quarter of the good ground at
most. Rested 20 growing days with no crowding, the worn cells come back as they were, 8 a day. Never a zone, a floor or a
bridge. The news says when a pasture wears and when its grass is back.

## B3: sicknesses of the wild plants

| | Wilt | Rust | Rot |
|---|---|---|---|
| Takes | wild trees of one kind (15 or more of it on the map) | wild grasses (60 or more) | wild berry bushes of one kind (6 or more) |
| Chosen | 4 in 10 | 3.5 in 10 | 2.5 in 10 (among those the land has hosts for) |
| Reach from a sick plant | 6 cells (roots) | 3 (the wind) | 5 |
| Chance a day to a plant in reach | 20% | 35% (49% after rain) | 25% (35% after rain) |
| New sick a day, at most | 5 | 16 | 3 |
| Sick in all, at most | 40 | 140 | 20 |
| A sick plant dies after | 10 days (bare after 2) | 5 days | 7 days |

How often: about once in 90 growing days (`LandMath.SickPerDay`), three times as often while the region has a blight
(`Rimfolk_WS_Blight`, a world event), half again in a wet, warm spell (a wet day in the last three, yesterday 12 °C or
more); never in a colony's first 20 days on the map, within 30 days of the last, or two at once. In a temperate land
that is one every year or two (the tests: 4 to 20 in twenty years). Each pass looks round each sick plant at 3 cells
within reach; nothing spreads on a cold day. It ends when nothing sick is left, after 45 days, or after three cold days
running; the plants still sick then pull through (the blight taken off them). The news says how it went (died, cut out
or burnt, pulled through), and the chronicle keeps it when five or more were lost.

**Shown on the map**: a sick plant carries the game's own blight (its blighted look; nothing it would yield). The game's
blight doesn't spread from a wild plant on its own (`Patch_LandWildBlight` on `Blight.TryReproduceNow`); the land's
rules spread it, among its kind and within its caps. A wilting tree stands bare (the game's leafless look, kept each day).
A tree that dies leaves its stump; what died comes back as a patch (B1). With 30 grasses sick, four wild grazers at most
move off the map ("fewer grazers"). When it begins: a message where it is, the colonist nearest it (within 30 cells)
says what they see, and a believer who holds **Morel** (rot, mould and blight, as the colony tells it) may say they take
it for his doing.

**The council** is asked once four are sick, within six days (`Rimfolk_Land_SickWood`, a council question in the flavour
style; A the best grower, B the next):

- *Cut out what is sick, and what grows beside it*: the sick and every plant of their kind within two cells marked for
  cutting (the game's marks), and a work party (the best grower and two others) cuts them;
- *Burn it out*: on the first dry daylight hour with no danger on the map, A (or the best able then) walks to the sick
  plant furthest from the colony that is at least 15 cells from its home area, buildings and zones, holds fire to it
  (`JobDriver_LandBurn`, a glow and smoke, a bar) and sets it and two sick beside it alight. The fire is the game's from
  there; what burns comes back as a patch. Greyed out when no sick plant stands that clear. Given up after three days
  with no chance;
- *Let it run its course*: nothing done; A minds it for a few days.

## B5: water that rises and falls

How wet the season has been comes from one method, `LandWet.SeasonWet(map)` (the seam): the weather record's last two
weeks, wet days (snow as half) against the land's usual share for its rain (`LandMath.UsualShare`: a desert about one
day in ten, a temperate land about three in ten), a dry run of ten days, and any drought (Odyssey's, Vanilla Events
Expanded's, the region's) or the region's floods. 0.25 or less is a dry spell, 0.75 or more a wet one. When the
climate's own season character (`LandClimate`, being built in `Weather/`) is there, this is the one method to change.

| | Dry spell | Wet spell |
|---|---|---|
| Starts moving after | 4 days of it | 4 days of it |
| What changes | shallow water and marsh at the edge to mud; the outer mud to soil as the mud moves in | open ground (soil, rich soil, moss, marshy ground, gravel, grassland soil, sand) beside the water to mud; the ring next to the water to marsh after 8 days |
| How far | 3 cells in from the old edge | 2 cells out |
| A day | 10 cells | 12 cells |

What one spell left goes back first when the other comes (20 cells a day, the furthest ring first, through mud), and
slowly while the season is as usual (6 a day); with the setting off, 20 a day until it is all back. Never touched: deep
water, rivers, the sea, hot springs, floods, and any water body joined to them (Odyssey's water bodies: freshwater only,
none with river or sea in them, and no cell within 2 of running water); frozen water; a bridge; a roof; a zone; anything
built. The swelling water never comes into the home area or within 2 cells of a building, nor over a tree. A cell the
colony builds on or zones afterwards is left as it is. The edge is found from Odyssey's water bodies a little at a time
(or from cells looked at here and there without it) and kept unsaved.

**Fish**: a pond the drought shrank holds no more fish than it can (Odyssey's stocks follow its size), and one the rain
filled gains a twentieth of what it holds. Vanilla Fishing Expanded fishes from Odyssey's stocks, so it follows too.

The news: when the water draws back ("mud shows along its edge"), when it rises over its banks, and when it is back at
its old edge.

## Settings

Under "The land" on the What's on page, each on by default: `landRecovers` (B1 and the pasture), `landSickness` (B3;
needs the deeper game hooks), `landWater` (B5).

## Not tried in the game

Built and checked by the build, the def validator, the tests and the text checks only. Worth watching in a real save:
the regrowth's plants in a modded biome (the grass and brush are sorted by name: a biome whose grass isn't called grass,
moss, sedge or reed gets no grass stage); whether the game's blight on a wild tree shows as clearly as on a crop; how a
burn behaves in a dry, windy summer; Odyssey's water bodies after a pond is cut in two by drying; the packed dirt in a
busy pen.

## Mods that would help

- **Living Waters and Waves** (the player plans to add them): rivers and the sea that move; nothing here touches them.
- **Quiet Wonders** (on the player's list to add): puddles that dry, autumn leaves and the like; cosmetic, it doesn't
  touch the terrain.
- **Better Trees** (active): its autumn colours and snow on the trees show the seasons on the woods that come back.
- **Vanilla Events Expanded** (active) has a burnt forest soil (`VEE_BurntForestSoil`); a later round could lay it on a
  fresh burn and let the grass stage take it back.
- No 1.6 or 1.7 mod adds plant sicknesses; the look is the game's own blight and leafless state.
