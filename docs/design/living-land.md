# The living land: the climate part

The player's picks of 28 November 2026 (docs/HANDOFF.md, "nature, the biosphere, weather, climate and geology made
dynamic and meaningful"): A1 seasons with their own character, A2 rare extremes and once in a lifetime, A3 weathers that
come by no biome given a place, A4 climate that drifts, E3 a record of the land (the climate part), E4 one setting for
strength. This note is for the parts built after it (the land, the ground, the life, the ties): what the climate decides
and how to read it.

## Where it lives

- `Source/ColonyCouncil/Weather/ClimateMath.cs`: the pure rules (no game types, tested in `Tools/Tests/LivingClimateTests.cs`).
- `Source/ColonyCouncil/Weather/LandClimate.cs`: the game side and the public API. The one place a season's character and
  the drift are decided and applied.
- `Source/ColonyCouncil/Weather/ClimateDrift.cs`: a `GameComponent`, the world's drift (when the record began, the pushes).
- `Source/ColonyCouncil/Weather/ClimateDebug.cs`: dev actions ("Climate: this land's next eight seasons", "Climate: once in
  a lifetime now...", "Climate: the season's forecast now").
- It runs inside the weather record (`WeatherBook`): its hour (`Sched.Weather`) and its 250-tick pass
  (`Sched.RareWeather`). No clock of its own, nothing per tick but one eased number.
- Settings: `climateStrength` (Off, Gentle, Usual, Dramatic) and `climateDrift` (Off, Random, Warmer, Colder, Drier,
  Wetter), What's on page, under "The weather"; both need the deeper game hooks.

## How it decides

- A season's character is a hash of the tile, the local year and the quadrum (`ClimateMath.SeasonOf`), so nothing is
  saved for it and it is the same every time it is asked, past or future. As designed about half the seasons are
  ordinary; the rest are wet, dry, warm or cold, and where the quadrum's usual mean is 3 °C or less, snowy or snowless
  instead of wet or dry; about one in ten is extreme. A hard winter runs 4 to 8 °C colder; a wet season rains about half
  again as often (measured in the tests' simulation).
- A year's own extreme (`YearKind`): about one year in twelve, a year without a summer, a dry year or a year of heavy
  rain; it takes over the seasons it belongs to. Never on land that is always frozen.
- Once in a lifetime (`RareKind`): about once in twenty years where it can come, in one season of the year, only what the
  land's own weather lists never bring (snow where there is no snow weather, a frost on warm land, a great heat on cold
  land, a blizzard, a dust storm, heavy rain on dry land, a great storm off the water), with the temperature to match for
  one to six days. It brings its weather with `WeatherManager.TransitionTo` when the temperature allows; the great storm
  only within the shared harm budget (`HarmKinds.Dangers`).
- The weathers no biome has (`SpecialKind`, `ClimateMath.SpecialDefs`) come with seasons of a fitting kind, red rain as a
  sign before a hard season, and in the region's drought and hard winter. `ClimateMath.Never` lists the event weathers it
  never brings (blood rain, the death pall, psychic rain, the long night, the inferno, the psycasts' and space's).
- The drift: one way for the whole game (random by the world, or the way chosen), 1 to 3 °C a decade as designed, capped;
  pushes from a volcanic winter and a comet; the colony's own pollution and lost trees per map.
- Where it acts: the season's lean on the weather's choice is `WeatherNow.seasonWet` and `seasonSnow`
  (`WeatherDynamicsMath.Character`, only on weathers the land already has); the temperature is one eased push added in
  `Patch_WeatherTemperature` (`WeatherDynamics.OffsetOf`), held over the fields' forecast (`PushAhead`).

## Reading it from other parts

```csharp
SeasonCharacter c = LandClimate.SeasonNow(map);      // kind, extreme, temp (°C), wet, snow, role (1 spring .. 4 winter),
                                                     // snowSeason, yearKind, rare and its hours, special
SeasonCharacter later = LandClimate.SeasonAt(map, year, quadrum);   // any season, the same every time
YearCharacter y = LandClimate.YearNow(map);          // the year's lean (wet, temp) and its own extreme (kind)
var (temp, wet) = LandClimate.DriftNow(map);         // the drift so far: °C, and the lean wetter (above 0) or drier
ClimateExtreme x = LandClimate.ExtremeNow(map);      // once in a lifetime, a year's or a season's extreme, or null
float push = LandClimate.TempNow(map);               // the climate's push on the outdoor temperature now (°C)

LandClimate.SeasonBegan += (map, c) => { ... };      // a season began on a home map
LandClimate.ExtremeBegan += (map, x) => { ... };     // an extreme was told (it shows on the map by then)
LandClimate.ExtremeEnded += (map, x) => { ... };     // it is over
```

- All of these give an ordinary, empty answer with the climate off, off a home map, or where `LandClimate.Works(map)`
  is false. Listeners are called inside a try; keep them cheap.
- `ExtremeBegan` comes when the land shows it (a cold or warm season a day in, a wet or dry one from its fifth day if the
  days agree, once in a lifetime when its weather has begun or the cold or heat is most of the way in), never before.
- `ClimateExtreme`: `scope` (Season, Year, Rare), `seasonKind`, `yearKind`, `rareKind`, `year`, `quadrum`, `temp`,
  `weather` (a def name or null), `label` (in words).
- The land's record (`ClimateYear`, forty years a map) and its trends (`ClimateMath.Trends`) are in `LandClimate.State`;
  the annals line is `LandClimate.AnnalsPart`, the Overview lines `LandClimate.OverviewLines`. Other parts that add to the
  record of the land (newcomers, trees lost, animals) should add their own lines to the annals beside it, not change it.

## What it shows

- At a season's start: the best reader of weather says what kind of season it looks like (may be wrong), over their head
  and in the news; the Overview's Weather card keeps it.
- An extreme: a message when it shows, the chronicle (once in a lifetime as a world milestone), a believer's word on whose
  sign it is (`GodHands.Show`, a sign), the region's matching world event started where it can be.
- The annals: the year's own extreme, its seasons that had a character and showed it, once in a lifetime, and what the
  record shows since the colony came; the elders say the same early in a season.
- The Almanac's weather page, "Seasons with a character of their own" and the four sections after it.

## Saved

Per home map, in the weather record: the season last begun, the eased push, the season's forecast, what was told or
brought (24 keys), the extremes that came (24), the land's record (40 years), the tree counts. For the world: when the
record began and up to six pushes. Models in `Tools/save_estimate.py`.
