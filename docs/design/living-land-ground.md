# The ground that stays and the mountain that wakes

The player's picks D1 and D2 of 28 November 2026, from the nature proposal (`docs/HANDOFF.md`, the entry "nature, the
biosphere, weather, climate and geology made dynamic and meaningful"): "Geology that stays" and "A mountain that wakes".
Built in the ground's worktree beside the climate (`Weather/`, `LandClimate`) and the land (`Nature/Land*`); nothing here
reads either, so the three can be merged in any order. Rimfolk's own: the player did not pick the Natural Disasters mod.

Code: `Source/ColonyCouncil/Nature/GroundMath.cs` (the rules, pure, tested), `GroundMap.cs` (the map's record and its
look, the hook on the game's events), `GroundMarks.cs` (D1), `GroundWaking.cs` (D2), `GroundDebug.cs` (dev actions).
Words: `Languages/English/Keyed/Rimfolk_Ground.xml`. Tests: `Tools/Tests/LivingGroundTests.cs`. The Almanac: "Rimfolk
events", the sections "The ground that stays" and "A mountain that wakes". No defs.

## What was there

- Rimfolk's rockslide (`Council_Evt_Rockslide`: rubble and an ore seam in hilly land) and warm vent
  (`Council_Evt_WarmVent`: a new steam geyser after a night tremor), the dangers on the map (the creak at the dig, the
  ground going soft by a door after rain), Smoulder's sleeper under deep mines (`Gods/OnMap/GodWaking.cs`: rumbles, stone
  falling on the miner, the working closing), the gods' hands in quakes and volcanic events (`Gods/GodHandsMath.cs`:
  Earthquake, VEE_Earthquake, DrillQuake, VolcanicWinter, VolcanicAsh, the rockslide and the warm vent read as
  Smoulder's by believers), the work party after Seasonal Weather's and Vanilla Events Expanded's earthquakes
  (`Weather/AfterStorm.cs`).
- Missing: anything a quake leaves behind, rain bringing a hillside down, the ground sinking, a mountain of the land
  itself stirring, ash that improves the soil.

## D1: the ground that stays

A mark is a few to a few dozen cells (`GroundMath.Cells`, `MaxCells` 40), always out on the land: never on or within 8
cells (`ColonyGap`) of anything of the colony's (buildings, blueprints, frames), never in its home area or a zone, never
where someone stands (`GroundMath.CellAllowed`; `GroundMarks.Clearance` builds the grid once per mark). No two marks on
a map within 60 days (`MarkGapDays`). The stone is the map's own: chunks of the face's rock (`building.mineableThing`),
its rough ground (`building.naturalTerrain`).

| Mark | When | What stays |
|---|---|---|
| Cave | after an earthquake or Rimfolk's rockslide | 3 to 7 cells carved into a rock face of the map's own rock with plenty of rock behind it (20 cells within 4), each cell keeping 3 of its 8 neighbours rock (`CaveCellHolds`) so every roof rests on rock; the rock's own roof kept, thin rock where there was none; 1 or 2 chunks on its floor; one time in three a seam of ore (`StorytellerEvents.Seam`). Nobody of the colony within 10 cells as it opens. |
| Spring | after an earthquake, the rockslide or the warm vent's tremor | 3 to 7 cells of the game's shallow water on open ground; with Odyssey, its hot spring water one time in three (always after the warm vent). |
| Fissure | after an earthquake or the warm vent's tremor | a jagged line of 5 to 9 cells of the land's rough rock across open ground, 2 to 4 chunks thrown up beside it. |
| Scree | after an earthquake | 6 to 14 cells of stony soil (the game's gravel) fanned out at the foot of a rock face, a chunk of the face's rock on every third. |
| Landslide | a day of rain on hilly land after 3 wet days running (the weather record): 4 such days in 100, 8 in a storm or torrent | 10 to 24 cells at the foot of a rock face: 2 to 5 of the game's collapsed rocks against the face (never on anyone or anything; mined away), then mud, chunks on every third. |
| Sinkhole | the end of a season with 8 or more wet days: 12 in 100, 24 where the land's rock is limestone, marble or chalk | the sign first (loose ground over 9 or 13 cells, a message), 8 to 16 hours later the ground gives way: shallow water in the middle (deep in the very middle of a large one where nobody stands), mud round the edge. Anything of the colony's put there since keeps it from opening. |

Which quakes: any event whose name or worker says earthquake (Seasonal Weather's `Earthquake`, Vanilla Events Expanded's
`VEE_Earthquake` at its `VEE_EarthquakeEpicenter`, Rimefeller's `DrillQuake` by name; Vanilla Psycasts Expanded's time
quake is not one), any condition by that name going on, Rimfolk's rockslide and warm vent (`GroundMath.SourceOf`). The
hook is a postfix on `IncidentWorker.TryExecute` (off with the deeper hooks); Seasonal Weather's condition is also watched
hourly. The mark is rolled an hour after the shaking stops: 30 in 100 for an earthquake, 20 for the rockslide, 15 for the
warm vent (which leave their own), so with the quakes the player's list brings (Seasonal Weather's and Vanilla Events
Expanded's, about one or two a year between them) a few marks in a decade.

Shown and told: dust, the camera shake, rubble, a splash; a plain message with the place ("to the north") to look at; a
line in the chronicle; a word from the nearest colonist up and about (one sentence). A believer may take the landslide
for Smoulder's doing and the sinkhole for Murmur's and say so, as rarely as the gods' hands are remarked on
(`GroundMarks.SomeSay`, `GodHands.Show`: their words only, maybe wrong, a scoffer's answer); an earthquake already has its
own remark through the hands' table.

Harm: someone of the colony within a step of a landslide as it comes down (a crush on the leg, 3 to 8; 60 in 100 before
the scale) or on the ground as a sinkhole opens (a bruise on the leg, 2 to 6; 50 in 100), through the shared budget
(`HarmKinds.Ground`, the system "ground", on while either setting is). Otherwise they get clear, and the message says so.

## D2: a mountain that wakes

Which lands (`GroundMath.LandOf`): lava first (the biome `LavaField` or `AB_PyroclasticConflagration`; a feature, the
tile's mutators or landmark, by name: Odyssey's lava caves, crater, flow and lake and obsidian deposits, Alpha Biomes'
magma vents, magmatic quagmire, geothermal hotspots and obsidian, Geological Landforms' caldera, Vanilla Landmarks
Expanded's volcano, calderas, lush lava fields, volcanic soil, sand and obsidian, or any name with volcan, lava or
magma), then mountains (the tile mountainous or impassable, or a feature with mountain in its name: the game's own,
Geological Landforms' lone mountain, Vanilla Landmarks Expanded's). Flat land and hills never wake. All by name, so a
mod's land counts when it is loaded and nothing breaks when it is not.

How often: the day's chance is one in 12 years on mountains, one in 6 on lava, twice that while the sleeper under the
colony's deep workings has turned in the last 60 days (`GodsOnMapBook.lastDeep`); not before day 60, never within 5 years
of the last waking. That comes to about once in 17 years on mountains and 11 on lava (`MeanYearsBetween`). Not while a
threat is on the map.

Its stages (`GroundMath.StageHours`, `GroundWaking`):

1. **Tremors, 3 to 5 days.** The letter "Tremors" (where the mountain is: the rock face with the most rock behind it,
   else the map's edge it lies beyond). About 5 tremors a day: the camera shakes, dust and rubble come off rock faces
   near the mountain; a few wild animals leave the land at the first. In each later tremor a stone may strike one
   colonist standing right at a rock face (a bruise, 2 to 6; 15 in 100 before the scale; the shared budget). 12 hours
   before the end, one or two warm vents open (the game's steam geysers, clear of the colony and of old vents).
2. **Ash, 2 to 3 days.** The letter "Ash from the mountain": smoke over the mountain (on the map, at the face or the
   edge), the game's `VolcanicWinter` for 4 to 8 days (darker, colder, slower growth) and, with Odyssey, its
   `VolcanicAsh` for a day or two (the ash in the air, its own filth and breathing hediff); ash (Odyssey's, else the
   game's) on 20 to 40 cells in 2 to 4 patches of open natural soil, towards the mountain, and here and there over the
   land.
3. **Settling, 5 to 9 days.** Then those cells, where still open natural soil, turn rich: Vanilla Landmarks Expanded's
   `VEE_VolcanicSoilRich` where it is loaded, else the game's `SoilRich`. A message, the chronicle, a word.

Odyssey's own volcanic ash (its incident on a lava field) leaves the same rich soil 5 to 9 days after it settles.

The gods are unseen: at the first tremor a believer may say whose stirring they take it for (Smoulder's, as a sign), at the
ash whose anger (as a wrath), through the hands' rules; the letters say only what is seen and what "people who know
mountain country say". The colony's book keeps it as they saw it.

## Settings, clock, save

- "The ground keeps marks" (`groundMarks`) and "A mountain that wakes" (`wakingMountain`), both on, under Rimfolk events.
  Off: nothing is left half done (a waiting quake, a sinkhole's sign, the waking and its ash are let go).
- The clock: `Sched.RareGround` 206 (61, the number first given, is the calendar's hourly tick 1311 on the 250-tick
  cycle, which LeanTests forbids), `Pass.RareGround`. Each 250 ticks only while the mountain is awake; the quakes, the
  sinkhole and the ash once an hour; the rain at its first wet hour of a day; the season and the waking once a day.
- Saved per home map (`GroundMap`): a few numbers and the ash's cells (`GroundMath.MaxCells` at most, modelled in
  `Tools/save_estimate.py`).
- Dev actions: "Ground: leave a mark now", "Ground: a quake noted now", "Ground: the mountain's next stage now",
  "Ground: what the ground remembers here".

## Not tried in the game

Built and tested by the rules and the code's shape only: the carving of a cave and the fog and roofs it touches, the
look of a fissure in rough rock, Odyssey's hot spring water and volcanic ash started by code, the game's volcanic winter
registered without its incident, and the mark placed at Vanilla Events Expanded's epicentre want a look in a game.
