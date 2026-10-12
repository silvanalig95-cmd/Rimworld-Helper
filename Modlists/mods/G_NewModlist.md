# Batch G: the new modlist (industrial and spacer)

The new main modlist (`Modlists/current-main.txt`, RimWorld 1.6, all DLCs) is an industrial and spacer game. It
drops Medieval Overhaul, Rimedieval, Outland, Medieval Total Rim and the other medieval mods, and adds 87 mods. This
note covers the ones that matter to Rimfolk: home and food systems (Bunk Beds, RimFridge, Gastronomy with Cash
Register, Therapy, Vanilla Nutrient Paste Expanded, Stuffed Floors), travel (Set Up Camp, Gravcamp, Vanilla Vehicles
Expanded, Skiptech), people (Humanoid Alien Races, Conjoined Twins, Det's and Rimsenal xenotypes, Fairycore),
factions and storytellers (five VFE modules, Rimsenal, Perry Persistent, Igor Invader), power (Rimatomics), weapons
(the Vanilla Weapons Expanded family, Rimsenal, Advanced Armaments, Unique Weapons Unbound, Dual Wield), Alpha mods,
animals and wastelands. Purely visual, performance and library mods get one line at the end.

How this was researched: the Steam Workshop text of every mod (Steam Web API `GetPublishedFileDetails`, Workshop
search for the ids), and shallow sparse clones (XML and C# only) of the public GitHub repos: Vanilla-Expanded
(Settlers, Classical, Deserters, Empire, Insectoids 2, Vehicles, VFE Spacer, VNPE, the six VWE repos, Perry
Persistent), juanosarg (Alpha Crafts, Implants, Mechs, Prefabs, Props Parks and Gardens), DetVisor (Avaloi, Boglegs,
Venators), Zaljerem (the 1.6 Gastronomy, Cash Register and Therapy), Unjust-Harry (RimFridge with shelves, which uses
the package id `rimfridge.kv.rw`), Alias44 (Set Up Camp), erdelf (Humanoid Alien Races), fluffy-mods (Stuffed
Floors), MemeGoddess (Dual Wield, Smart Medicine), TheMiggiestMig (Skiptech), Dubwise56 (Rimatomics, XML only),
emipa606 (Drugs+, Animals Forage, ReGrowth Extinct Animals), jecrell (Doors Expanded, up to 1.5), Woolstrand (Real
Ruins). No source was found for Bunk Beds (the defs come from a mirror of the 1.5 XML), Gravcamp, Conjoined Twins
and MultiMind, Fairycore, Paws of Interest, Rimsenal, Advanced Armaments, Unique Weapons Unbound or Possessed Weapons;
those sections rest on the Workshop text and are marked. Rimfolk's code was read where a mod could touch it (`Avail`,
`RoomUtil`, `People`, the advisor, wish workers, room plans).

Markers used below:
- **[core system]**: a mod of the kind a player keeps in every run, like Dubs Bad Hygiene (it changes how a basic
  system works instead of adding optional content). These matter most for Rimfolk.
- **Unconfirmed**: could not be checked in source; based on the Workshop text or inference.

---

## Home, food and comfort

### Bunk Beds (`darknote.bunkbeds`) [core system]
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2961752749 (code by Taranchuk, art by DetVisor)
- What it adds: `BB_BunkBedDouble` (2 sleepers) and `BB_BunkBedTriple` (3 sleepers), both 1x2, from `BasicBedBase`
  (thingClass `Building_Bed`, `bed_humanlike`), with comp `BunkBeds.CompProperties_BunkBed` (`pawnCount`). Assembly
  `BunkBeds`; helpers `BunkBeds.CompBunkBed` and `BunkBeds.Utils.IsBunkBed(ThingWithComps)`. Two mod settings, both on
  by default: bunk beds count as barracks, and bunk-mates get the shared-bed mood penalty. Vanilla Gravship Expanded
  reuses the same comp for its gravship bunk bed. Works with Hospitality.
- In play: three colonists in two cells. A room of bunk beds is a barracks, so the barracks mood applies.
- For Rimfolk:
  - `Avail.IsSingleBed` (1 wide, 2 long, humanlike) is true for both bunk beds, so room plans can offer a bunk bed in a
    private bedroom's bed slot, and `BestBuildable(IsSingleBed, ...)` could pick one when the vanilla bed isn't
    available.
  - **Share-a-bed wish is fulfilled wrongly.** `WishWorker_ShareBed.IsFulfilled` only checks that both partners own
    the same bed; two lovers on two levels of one bunk bed pass. Its self-help (`TrySelfHelp`) claims a free slot in
    the partner's bed, so a colonist "moves in" to a bunk. Check `Avail.IsDoubleBed` and exclude beds with a
    `CompBunkBed` (match the comp class name, the mod is optional).
  - Own room, barracks advice and the "separate rooms" wish read rooms and `OwnedRoom`, which stays consistent with the
    mod's barracks setting. Nothing to change there.
  - Hook: the barracks room plan could use bunk beds when researched (fits more sleepers).
- Confidence: high for defs and settings (1.5 XML mirror, Workshop text, BedOwnershipTools interop code); the 1.6 defs
  are assumed unchanged.

### RimFridge (`rimfridge.kv.rw`) [core system]
- Link: three Workshop versions share this package id: https://steamcommunity.com/sharedfiles/filedetails/?id=2898411376
  ("Now with Shelves", 1.6), ?id=2878183338 ("Updated [1.6]"), ?id=1180721235 (the original). Source read: the shelves
  version (https://github.com/Unjust-Harry/rimworld-rimfridge-now-with-shelves).
- What it adds: powered fridges `RimFridge_SingleRefrigerator` (1x1), `RimFridge_Refrigerator` (2x1),
  `RimFridge_QuadRefrigerator` (2x2), wall fridges `RimFridge_SingleWallRefrigerator` and `RimFridge_WallRefrigerator`
  (thingClasses `RimFridge.RimFridge_Building`, `RimFridge_WallBuilding`, `RimFridge_MultiSidedWallBuilding`), comp
  `RimFridge.CompRefrigerator` (props `CompProperties_Refrigerator`), `CompPowerTrader`. Research Air Conditioning.
  Patches `Thing.AmbientTemperature` so stored items read as cold, feeds paste dispensers as a hopper, and gives a
  `FrostyBeer` mood for chilled beer.
- In play: food stays frozen anywhere, so many colonies never build a freezer room.
- For Rimfolk: already handled. `Avail.IsFridge` matches the comp class name and `RoomUtil.HasFreezer` counts a powered
  fridge as a freezer (freezer wish, freezer ambition goal, freezer advice). The spoilage advice and the vermin event
  read `AmbientTemperature`, which RimFridge patches. One small fridge satisfies the freezer wish for the whole colony,
  which is fine. Hook: offer a fridge in the kitchen and dining room plans when researched.
- Confidence: high.

### Gastronomy and Cash Register (`orion.gastronomy`, `orion.cashregister`) [core system with Hospitality]
- Links: https://steamcommunity.com/sharedfiles/filedetails/?id=3509488152 , ?id=3509487668 (1.6 continuations by
  Zaljerem, https://github.com/Zaljerem/Gastronomy , https://github.com/Zaljerem/CashRegister ; package ids kept).
- What it adds: any table can be toggled into a dining table; each chair spot gets a `Gastronomy_DiningSpot` (an
  ethereal thing whose class `Gastronomy.Dining.DiningSpot` derives from `Building_NutrientPasteDispenser`). A cash
  register (`CashRegister_CashRegister`, `CashRegister.Building_CashRegister`, a `Building_TableTop` placed on a table,
  `IThingHolder` that keeps the takings) sets shifts, menu, prices, who may order and a radius. Work type
  `Gastronomy_Waiting` ("Wait"), jobs `Gastronomy_Dine`, `Gastronomy_TakeOrder`, `Gastronomy_Serve`,
  `Gastronomy_MakeTable`, `Gastronomy_StandBy`. Map component `RestaurantsManager`, `RestaurantController` per register.
  Thoughts: `Gastronomy_HadToWait` (-1, 2 days), `Gastronomy_ServicedMood` (-2 to +3, 1 day),
  `Gastronomy_BoughtFood` (+4 free to -6 very expensive), `Gastronomy_Serviced` (opinion of the waiter, 5 days).
- In play: during shifts colonists and Hospitality guests sit and order; guests pay silver into the register.
- For Rimfolk: no clash found. Silver inside a register counts for the treasury wish through
  `WishWorker_Stock.HeldInBuildings` (any `IThingHolder` building), which is right. Dining spots are paste-dispenser
  subclasses: any future Rimfolk check written as `is Building_NutrientPasteDispenser` must skip them. Hook ideas: the
  restaurant as a council meeting venue, waiter service lines in journals, a "renowned hosts" ambition counting
  register takings.
- Confidence: high (1.6 source).

### Therapy (`orion.therapy`) [core system]
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3509488682 (1.6 continuation, https://github.com/Zaljerem/Therapy)
- What it adds: research `Therapy` (Industrial, after Complex Furniture), the therapy couch `TherapyCouch`
  (`Therapy.Building_Couch`, 1x2, needs a chair nearby, assigned to all colonists), room role `TherapyOffice`
  (`Therapy.RoomRoleWorker_TherapyOffice`: any couch plus any seat scores 30), work type `Therapist` (Social skill),
  jobs `ReceiveTherapy` and `PerformTherapy`, joy giver `JoyGiver_ReceiveTherapy`, thought `TherapyRelieved`
  (+15, 1.5 days). A session ages the patient's worst negative memory faster (`Thought_Memory.age +=`, any visible
  memory with a negative mood and a duration).
- In play: distressed colonists lie on the couch in free time; a good therapist in a nice room clears bad memories.
- For Rimfolk: Rimfolk's own timed memories (refused wishes, broken promises, dreams) are aged off like any others,
  which is intended. Trust, decrees and quirks are situational thoughts and untouched. Side effect: a couch and chair
  in a small dining or rec room can turn it into a "therapy office" (a flat score of 30; vanilla scores a dining room
  by its eating tables, so a room with only a few tables probably loses, unconfirmed), and features that read
  `RoomUtil.DiningRoom` then lose that room. The Workshop page lists Colony Manager as incompatible
  (works on a new game, untested with the 1.6 fork in this list). Hooks: a Mood tab hint ("a session on the therapy
  couch") for long negative memories when a couch exists; a therapy office room plan; the physician office could favor
  a skilled therapist.
- Confidence: high.

### Vanilla Nutrient Paste Expanded (`vanillaexpanded.vnutriente`) [core system]
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2920385763 , https://github.com/Vanilla-Expanded/VanillaNutrientPasteExpanded
- What it adds: a VEF pipe network for paste: grinders `VNPE_NutrientPasteGrinder` (raw food in), vats
  `VNPE_NutrientPasteVat` (`PipeSystem.CompProperties_ResourceStorage`; contents spoil fast without power), taps
  `VNPE_NutrientPasteTap`, droppers for beds `VNPE_NutrientPasteDripper`, feeders `VNPE_NutrientPasteFeeder`, valves
  and pipes; pipes also feed growth vats. Meat in the grinder contaminates the whole supply.
- For Rimfolk: **food stored as paste is invisible to the winter forecast.** `AdvisorWorker_Food` uses
  `resourceCounter.TotalHumanEdibleNutrition`, which counts items, not the paste in a pipe net, so a colony that grinds
  its harvest into vats gets a false "not enough food for winter" warning. Check: add the stored amount of
  `VNPE_NutrientPasteNet` (via reflection on the PipeSystem net, or a rough count of vats) or soften the warning when
  vats exist. Paste meals give the vanilla `AteNutrientPasteMeal` thought, so the better-food wish already reacts.
- Confidence: high for defs; the counting gap is inferred from Rimfolk's code (not tested in game).

### Stuffed Floors (`fluffy.stuffedfloors`) [core system]
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3245941356 (unofficial 1.5/1.6 update of Fluffy's mod,
  https://github.com/fluffy-mods/StuffedFloors)
- What it adds: `FloorTypeDef` templates that generate one terrain per material, named `<Stuff>_<FloorType>` (for
  example `WoodLog_StuffedWoodFloorVanilla`, `BlocksGranite_StuffedSmoothTiles`), grouped in one dropdown each. Each
  type lists the floors it makes obsolete, and those get `designationCategory = null`: `WoodPlankFloor` (replaced by
  `StuffedWoodFloorVanilla`), `TileSandstone`/`TileGranite`/`TileMarble`... (by `StuffedSmoothTiles`), the vanilla
  flagstones, metal tiles, the Ideology/Royalty stone floors, and More Floors. `SterileTile` is kept.
- For Rimfolk: **room plans lose their default floor.** `RoomTemplateDef.ResolveFloor` walks the template's `floors`
  list (mostly `WoodPlankFloor` and `TileX`), and `Avail.CanBuild` fails for all of them because they are no longer
  player-buildable, so bedrooms, dining rooms and workshops come with no floor unless the player picks one;
  hospital and kitchen fall back to sterile tile only after its research. Check: when no listed floor is buildable,
  try the stuffed replacement (`<stuff>_StuffedWoodFloorVanilla`, `<stuff>_StuffedSmoothTiles`) or the cheapest floor
  in the same dropdown family from `RoomPlanConfig.FloorOptions`. The floor picker itself works (it groups by
  `designatorDropdown`); the generated floors are not `MadeFromStuff`, so the material choice is the floor choice.
- Confidence: high for the mechanism (Fluffy's source); the 1.6 continuation is assumed to keep the same obsoletes list.

### Doors Expanded (`jecrell.doorsexpanded`) [core system]
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3532342422 (1.6 continuation; source read up to 1.5)
- What it adds: 2x1 and 3x1 doors and autodoors, tribal curtains, jail doors, gates, blast doors, remote garage
  doors with buttons (`DoorsExpanded.Building_DoorExpanded`, invisible `HeronInvisibleDoor` parts of class
  `Building_DoorRegionHandler`, `Building_DoorRemoteButton`).
- For Rimfolk: nothing. The invisible door parts are doors, so `IsCover` and similar checks skip them.

### Vanilla Furniture Expanded - Spacer Module (`vanillaexpanded.vfespacer`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2028381079
- What it adds: research `SpacerFurniture`, `SpacerElectronics` (Spacer). Advanced beds `Bed_AdvBed`,
  `Bed_AdvDoubleBed`, illuminated end table, dresser and wardrobe (glowers and bed facilities), interactive tables,
  ultrascreen and wall TVs, modern couch and armchair, spacer lamps, an air purifier (`VFES_FreshAir` +1), a repair
  rack.
- For Rimfolk: works through the generic role and light checks (the illuminated end tables count as end tables and
  as lights). Nothing to change.

### Alpha Props - Parks and Gardens (`sarg.alphapropsparks`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3146268928
- What it adds: about 150 silver-bought props (`AP_` defs, parents `VFEPD_ColourableFurnitureBase` /
  `VFEPD_NonColourableFurnitureBase`, thingClass `VFEProps.Building_SubstractsSilver`): fountains, flower beds, hedges,
  planters, fake plants (`VFEProps.Building_FakePlant5x5`), plus some props built with materials.
- For Rimfolk: covered. `IsProp` in `WishWorkers_Generic` follows the thingClass chain, so the `AP_` fountains and
  benches don't fulfil the fountain or bench wish; the flowers wish counts real plants only.

### Alpha Prefabs (`sarg.alphaprefabs`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3070780021
- What it adds: a catalog of 200+ ready-made structures bought with silver and delivered (or placed as blueprints with
  the Blueprints fork).
- For Rimfolk: nothing wrong; prefab furniture is real furniture and wishes are rechecked hourly. It overlaps with
  Rimfolk's room plans in purpose, not in code.

### Alpha Crafts (`sarg.alphacrafts`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3382446150
- What it adds: 65 artisan trade goods (butter, cheeses, oils, kimchi, sake, kvass, liquors, kombucha...) from an
  artisan workshop, oil press, dairy station, grinder, bottling station (all `Building_WorkTable`) and two PipeSystem
  processors, `AC_VinegarCask` and `AC_Infuser`; 9 research projects (two Medieval, seven Industrial); a periodic
  trade quest `AC_ArtisanTradeRequest`. Nine goods are `DrugCategory.Social`.
- For Rimfolk: `Avail.IsFermenter` already rejects the vinegar cask (the "vinegar" exclusion). The social drinks count
  for the drink stock wishes, which is right for sake, kvass and liquors. Nothing to change.

---

## Care and work helpers

### Smart Medicine (`memegoddess.smartmedicine`) [core system]
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3526251680 , https://github.com/MemeGoddess/RimWorld-SmartMedicine
- What it adds: doctors use medicine from anyone's inventory and stock up a set amount in their own; minimal medicine
  for minor wounds; field tending; best medicine for surgery; per-injury care.
- For Rimfolk: **carried medicine isn't counted.** `AdvisorWorker_Medical` and `WishWorker_Infirmary.Medicine` count
  spawned medicine only (`listerThings.ThingsInGroup(Medicine)`), so medicine in doctors' pockets can raise the "low
  on medicine" advice and keep the stocked-infirmary wish open. Vanilla's own inventory stock does the same, this mod
  just makes it common. Check: add medicine in the inventories of colonists on the map.

### Dual Wield (`memegoddess.dualwield`) [core system for combat players]
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3630049419 , https://github.com/MemeGoddess/DualWield
- What it adds: an off-hand weapon, stored in `pawn.equipment` with an extended-data flag (`TryGetOffHandEquipment`),
  not as `Primary`. Configurable which weapons qualify; NPCs dual wield too.
- For Rimfolk: **an heirloom in the off hand counts as not carried.** `LegacyTracker.HeirloomCarriedBy` checks
  `equipment.Primary`, worn apparel and inventory; `WishWorker_TraderGoods.Find` checks `Primary` too. Check:
  use `equipment.AllEquipmentListForReading` in both.

### Defensive Positions (`unlimitedhugs.defensivepositions`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=761219125 (official), unofficial 1.6 fork ?id=3519114948
- What it adds: saved defensive spots per colonist and squad hotkeys. Needs HugsLib.
- For Rimfolk: nothing.

### Everybody Gets One (`memegoddess.everybodygetsone`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3530806680
- What it adds: bill targets "one per colonist +X" and "X per colonist", with TD Find Lib filters.
- For Rimfolk: nothing.

### Smart Farming (`owlchemist.smartfarming`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3220129183 (continuation, same package id)
- What it adds: smart sowing against frost, zone priorities, harvest-if-dying, cut-if-blighted, harvest now, per-zone
  food estimates.
- For Rimfolk: nothing wrong. Its auto-harvest and blight removal act before Rimfolk's crop advice does, which is fine.

### Animals Forage (`mlie.animalsforage`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3667046888 (XML only)
- What it adds: some animals now and then produce food items: raw fish (`AEXP_RawFish`, rots in 2 days), insect meat,
  berries, fungus, for penguins, otters, bears and many modded animals.
- For Rimfolk: nothing. The items can show up in "items rotting outside" counts now and then.

### Drugs+ (`mlie.drugsplus`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2018224594 , https://github.com/emipa606/DrugsPlus
- What it adds: four Medical-category drugs made at the drug lab: Cyludapam (motor skill down), Hypomorphone
  (painkiller), Tristapram (mood +25), Flux (Spacer, mood +35, "perfect luciferium"). The repo only shows old version
  folders; no addiction comps were seen there (unconfirmed for 1.6).
- For Rimfolk: nothing. They are Medical, so they don't count as social drinks.

---

## Travel, camps and colonists away from home

### Set Up Camp (`alias.setupcamp`) [core system]
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3234938357 , https://github.com/Alias44/SetUpCamp
- What it adds: settings for 1.6's vanilla caravan camps: map size, how long abandoned camps stay, raid timer, camp
  resources, and **persistent camps** that stay without colonists (comp `SetUpCamp.WorldObjectCompProperties_PermaCamp`
  patched onto the vanilla `Camp` world object). Adds no defs of its own.
- For Rimfolk: vanilla camps are temporary incident maps, not player homes. Colonists in a camp stay "ours"
  (`People.StillOurs`), but Rimfolk treats them as away: no wishes voiced or granted, no advice for that map, and a
  homecoming entry when they return by caravan after two days or more. With persistent camps a group can live in a
  camp for seasons and the council goes quiet for them. Nothing is wrong (they are not counted as gone). Hook: journal
  "letters from the camp", or treat a long-lived persistent camp as a second home for wishes.
- Confidence: high.

### Gravcamp (`maal.gravcamp`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3816083162 (released October 2026, no source found)
- What it adds: turns Odyssey's gravship into a nomadic camp you move across the world; camp substructure that shows
  the ground, "camp animal" thrusters, feedstock-fuelled tanks, camp banners, two scenarios (Nomads, Convoy).
  Designed for Medieval Overhaul or Vanilla Vehicles Expanded textures; no vacuum support (don't go to orbit); the
  gravship cutscene must be turned off. Conflicts with Vanilla Gravship Expanded.
- For Rimfolk: gravship flight is already handled (passengers stay ours). Two risks, both **unconfirmed**:
  - Era: if the nomad scenarios start with Odyssey's gravtech research finished and two or more of those projects are
    Spacer, `Avail.ResearchedTech` reads Spacer and a medieval camp loses its low-tech text. The Era setting fixes it by
    hand; a code check could ignore the gravtech research tab when computing the era.
  - Moving the home often leaves graves, Rimfolk's plans and personal touches behind on the old map; grave visits and
    the mourners event find no grave.
- Confidence: low (Workshop text only).

### Vanilla Vehicles Expanded (`oskarpotocki.vanillavehiclesexpanded`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3014906877 , https://github.com/Vanilla-Expanded/VanillaVehiclesExpanded
- What it adds: cars, trucks, boats and planes on Vehicle Framework, research `VVE_BasicVehicles`,
  `VVE_ComplexVehicles`, `VVE_AerialVehicles`, `VVE_CombatVehicles` (Industrial), garages, wrecks to restore, Ideology
  precept moods for driving, flying and sailing (`VVE_Driving_Required` and so on).
- For Rimfolk: passengers aboard vehicles in flight or on the world map are already kept as colonists. Vehicles are
  `ToolUser` pawns with `MetalVehicle` flesh, so they are neither people nor animals to Rimfolk (no pet or handling
  counts). A colonist sitting in a parked vehicle is despawned and skipped by the advisor's colonist counts for that
  time, which is harmless.

### MigCorp - Skiptech (`migcorp.skiptech`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3626074566 , https://github.com/TheMiggiestMig/MigCorp-Skiptech
- What it adds: powered skipdoors (`MigcorpSkipdoor`, archotech `ArchotechSkipdoor`) that pawns use automatically to
  shorten trips on the same map; research `MigCorp_SkipNet` through `MigCorp_SkipRift` (all Spacer); skip beacons;
  `MigcorpSkipShock` when a door is underpowered. Enemies and animals can use them (setting).
- For Rimfolk: nothing. Same-map teleport only; a world-map skipgate is announced but not released.

### Real Ruins (`woolstrand.realruins`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1552146295
- What it adds: ruins of other players' real bases on your map and the world (downloaded from a server; it uploads
  snapshots of your base too), and two events (caravan finds ruins, pristine ruins raced for by other factions).
- For Rimfolk: nothing wrong. Ruin buildings are unowned, so they don't count as the colony's. Corpses in ruins inside
  the home area do trigger the "bodies lying around" advice, correctly. Note for the modlist: VFE Insectoids 2's page
  says Real Ruins "has been confirmed to mess things up" with it.

---

## Power and tech

### Dubs Rimatomics (`dubwise.rimatomics`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1127530465 , https://github.com/Dubwise56/Rimatomics (XML)
- What it adds: nuclear reactors scaling to megawatts, turbines, cooling, energy weapons, railguns, missile defense,
  ICBMs (`ICBMfission` world object). Its own research system: one vanilla project `RimatomicsActivate` (Industrial),
  then 27 `Rimatomics.RimatomicResearchDef` projects and `ResearchStepDef` steps on a separate screen, with research
  failures. Conditions `RadioactiveFallout` (meltdown) and `NuclearFallout` (blast); radiation hediffs
  (`RimatomicsRadiation`, `RadiationIncurable`, `FatalRad`); incidents `TransformerExplosion` and a joke "?" incident
  (`notNothing`, an alien abduction gag with the `Abducted` -14 memory; unconfirmed whether anyone leaves the map).
- For Rimfolk: the era is unaffected (its projects are not `ResearchProjectDef`s). The advisor doesn't know the two
  fallout conditions, so it gives no stay-indoors advice for them. Hook: treat them like toxic fallout in the
  conditions advice.
- Confidence: high for defs.

---

## People: races, xenotypes, shared bodies

### Humanoid Alien Races (`erdelf.humanoidalienraces`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=839005762 , https://github.com/erdelf/AlienRaces
- What it adds: the race framework (`AlienRace.ThingDef_AlienRace`). Which mod in this list needs it wasn't
  established (the elves, merfolk and apes in the list are Biotech xenotypes).
- For Rimfolk: the Story tab and lifespan-relative ages are already handled. Worth knowing for future races:
  `alienRace.generalSettings.validBeds` and `canLayDown` (a race may not use every bed), `raceRestriction`
  (`CanBuild`, `CanEat`, `CanWear`, `whiteBuildingList`; HAR already hides colony-wide restricted buildings from the
  build menu, which `Avail.InBuildMenu` respects, but not per-pawn restrictions), `thoughtSettings`
  (`cannotReceiveThoughts`, `cannotReceiveThoughtsAtAll`, `ReplaceIfApplicable`). A wish for a bed, meal or garment the
  race can't use would never be fulfilled; checking `CanUseBed`/`CanEat`/`CanWear` by reflection when HAR is loaded
  would avoid that.

### Conjoined Twins (`multimind.conjoinedtwins`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3808663984 , requires MultiMind Framework
  https://steamcommunity.com/sharedfiles/filedetails/?id=3804966993 (which is **not in the modlist file**; it may be
  missing, or the list omitted it). No source found.
- What it adds: one physical Human pawn holding two persistent "mind profiles". MultiMind swaps the active profile's
  name, nickname, backstories, favorite color, skills, passions, supported traits, work capabilities and priorities onto
  the pawn when the controlling twin changes (manual, or adaptive switching for skilled work); both twins keep their
  own opinions and talk separately; romance belongs to the body. Separate head and brain injuries; one twin can die
  while the body lives. Separation surgery (Medical 14) "creates two ordinary Humans"; "remove deceased twin" saves the
  survivor. Natural spawning among Humans at 0.5% by default (starting colonists too).
- For Rimfolk: **two people in one Pawn.** Everything Rimfolk keeps per pawn (journal, trust, standards, life goal,
  quirks, secrets, epithets, office seat, wishes, votes) mixes both twins, and traits and backstory read at different
  times belong to different twins (a wish voiced by the greedy twin judged with the other's traits; a "past catches up"
  event aimed at whichever backstory is active). Separation may replace the pawn with two new ones; if the original
  pawn is discarded rather than killed, Rimfolk forgets its history and may count it as vanished (**unconfirmed**).
  Check: detect MultiMind (assembly or package `multimind.framework`, its public API exposes profiles, the current
  controller and change notifications), then either key Rimfolk's person data by profile or at least pause wishes and
  journals while the controller is not the one they belong to; on separation, carry history to the successor.
- Confidence: medium (Workshop text of both mods, detailed).

### Det's Xenotypes: Avaloi, Boglegs, Venators (`det.avaloi`, `det.boglegs`, `det.venators`)
- Links: https://steamcommunity.com/sharedfiles/filedetails/?id=3199692643 , ?id=3146564944 , ?id=3140248688
  (GitHub DetVisor/AvaloiXenotype, BoglegsXenotype, VenatorsXenotype)
- What they add:
  - Avaloi (`DV_Avaloi`): artists and thinkers from the glitterworlds; `ChemicalDependency_Alcohol`, terrible tox
    resistance, weak immunity, fast learning, strong Artistic and Intellectual aptitude, high libido;
    `DV_PsychicInebriation` (`Avaloi.Gene_DrunkPsycaster`: alcohol raises psychic sensitivity). Spawn mostly as
    Empire members, Ancients, outlanders.
  - Boglegs (`DV_Bogleg`): greedy merchants; `DV_Materialistic` forces the Greedy trait, `DV_Silvertongued`,
    `DV_IntimidatingPresence`, `DV_FatStorage` (`Boglegs.Gene_Fat`, blubber that prevents starvation), `DV_MuckSpit`,
    remarkable Social, poor Shooting. Adds the Tongo table (`DV_TongoTable`, joy, Complex Furniture).
  - Venators (`DV_Venator`): hunters; `DV_Farsighted`, `DV_HunterInstincts`, `DV_ParalyzingFangs`, robust digestion,
    terrible Cooking, Sleepy, psychically deaf.
- For Rimfolk: all are Biotech xenotypes with normal moods. The Avaloi alcohol need is a gene dependency
  (`Hediff_ChemicalDependency`), not a `Hediff_Addiction`, so the "quit a drug" wish correctly ignores it; the "steady
  supply" wish also ignores it (hook: include gene dependencies there). Greedy Boglegs ask for the treasury more,
  naturally.

### Paws of Interest (`nialation.pawsofinterest`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3764120996
- What it adds: 13 mostly cosmetic xenotypes after Pokemon and other games (Eevee line, Absol, Goodra, Lopunny...).
- For Rimfolk: nothing.

### Fairycore: Spirit Wings, Apparel 2, Carillon, Magic Wand (`homebrew.fairycore`, `homebrew.fairycoreapparel2`, `homebrew.fairycorecarillon`, `homebrew.fairycorepixiewands`)
- Links: https://steamcommunity.com/sharedfiles/filedetails/?id=3812515333 (Spirit Wings, the base), ?id=3812924898,
  ?id=3812921116 , ?id=3814383460 (all work in progress, October 2026; no source found)
- What they add: fairy xenotypes (Monarch, Court and Lesser fairy, Pixie) built on a gene, Venenum: glowing wings,
  true flight, psychic powers that build "neural heat". Past the safe limit comes Psychic Delirium: abilities at 25,
  50 and 75%, a berserk frenzy at 90% ("you completely lose control"), and at 100% the fairy explodes, "leaving
  nothing to resurrect"; "Fairy's Gate" is a voluntary self-destruct. A fairy treated badly may refuse to use its
  powers; quality time cures delirium. Carillon: five ultratech psychic swords (Fabrication). Magic Wand: five
  elemental wands for psycasters; Pixies can't equip normal weapons and need a wand. Apparel 2: five dresses and a
  hat. The author says it is meant for very hard enemy mods, not a vanilla game.
- For Rimfolk: fairies are normal colonists with moods. Possible problems, **unconfirmed**: if the final explosion
  removes the pawn without `Pawn.Kill`, Rimfolk misses the death (no grief, eulogy or annals line) and later treats
  the colonist as vanished; the "a decent weapon" wish could ask for a gun a Pixie can't hold (check
  `EquipmentUtility.CanEquip` before voicing it). Hook: a fairy's willingness ("treated well") fits Rimfolk's trust.

### Rimsenal Xenotype Pack - Askbarn (`rimsenal.askbarn`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2918812858
- What it adds: xenotypes Askbarn and Uredd, anti-mechanoid super soldiers (Happy, Delicate, Tinderskin, high
  libido, weak melee...), new genes and gear.
- For Rimfolk: nothing.

---

## Factions, threats and storytellers

### Vanilla Factions Expanded - Settlers (`oskarpotocki.vanillafactionsexpanded.settlersmodule`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2052918119 , https://github.com/Vanilla-Expanded/VanillaFactionsExpanded-Settlers
- What it adds: western factions `SettlerCivil`, `SettlerRough`, bandits `SettlerSavage` (permanent enemy), player
  faction "New Hideout" (`VFES_PlayerSettlement`, Industrial) and a bandit start; storyteller Diego Dire (`VFES_DD`,
  hard start that eases, then restarts the cycle); protection fee demands (`ProtectionFee`); bounty hunting and caravan
  raid quests; chemshine (`Chemshine`, a Social alcohol made from chemfuel in `Building_ChemshineBarrel`, a PipeSystem
  processor, BiofuelRefining); chemlamps, saloon doors, faro and five-finger fillet tables, a windmill generator
  (Electricity), western guns.
- For Rimfolk: the chemshine barrel counts as a fermenter (`IsFermenter`: processor comp plus "barrel"), which is
  right; chemshine counts as a drink. The windmill counts as a real generator, correctly. Rimfolk's "tribute" event
  sits next to the protection fee demand without clashing.

### Vanilla Factions Expanded - Classical (`oskarpotocki.vfe.classical`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2787850474 , https://github.com/Vanilla-Expanded/VanillaFactionsExpanded-Classical
- What it adds: three republics (`VFEC_CentralRepublic`, `VFEC_WesternRepublic`, `VFEC_EasternRepublic`) whose
  senators you win over for tribute, perks and technologies (world component `WorldComponent_Senators`); player
  faction "New Empire" (`VFEC_NewEmpire`, Neolithic); storyteller Neron Neolithic; thermae bath (`VFEC_Thermaebath`,
  joy kind `VFEC_Relaxing_Bath`); road building (`VFEC_RoadBuilding`); outposts built on Vanilla Outposts
  (`Outpost_Farming`, `Outpost_Hunting`, `Outpost_Logging`, `Outpost_Mining`); opium (`VFEC_Chemical_Opium`).
- For Rimfolk: the thermaebath has "bath" in its name, so it fulfils the bath wish (right). Its outposts are Vanilla
  Outposts objects, so colonists posted there stay ours. Nothing to change.

### Vanilla Factions Expanded - Deserters (`oskarpotocki.vfe.deserters`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3025493377 , https://github.com/Vanilla-Expanded/VanillaFactionsExpanded-Deserters
- What it adds (Royalty): an anti-Empire campaign. Harbor an imperial deserter (`VFED_GiveQuest_ChasedDeserter`) and
  become a deserter colony for good; a visibility system that scales imperial patrols (`VFED_ImperialPatrol`),
  absolver raids (`VFED_Raid_Absolver`) and propaganda drops; intel as currency; deserter shuttles; contraband;
  storyteller Damocles Deserter; an ending where both the Empire and the Deserters cease to exist.
- For Rimfolk: Rimfolk's own "deserter asks for shelter" event overlaps in theme only. Factions that vanish at the
  ending are treated like defeated factions. Nothing to change.

### Vanilla Factions Expanded - Empire (`oskarpotocki.vfe.empire`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2938820380 , https://github.com/Vanilla-Expanded/VanillaFactionsExpanded-Empire
- What it adds (Royalty): an Empire overhaul with new titles, the Great Hierarchy, vassals, honors, noble visits,
  balls and art exhibitions (rituals with moods), room roles `VFEE_Ballroom` and `VFEE_Gallery` that titles require
  (`VFEE_TitleNoBallroom`, `VFEE_TitleNoGallery`, -8 each; `VFEE_LackingNobleCourt` -10), a hidden Ultra faction
  `VFEE_Deserters`, player faction "New Family", storyteller Ariadne Archduchess, an endgame (`VFEE_GiveQuest_EndGame_Stellarch`).
- For Rimfolk: the royal room wish covers only the vanilla bedroom and throne room requirements, so a noble can still
  be unhappy about a missing ballroom after it is granted (not wrong, just incomplete). Hook: a ballroom and a gallery
  room plan, and a royal wish variant driven by the `VFEE_Title*` thought workers.

### Vanilla Factions Expanded - Insectoids 2 (`oskarpotocki.vfe.insectoid2`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3309003431 , https://github.com/Vanilla-Expanded/VanillaFactionsExpanded-Insectoids2
- What it adds: insect raids (`VFEI_HordeWaveRaid`, `VFEI_BurrowSiege`, `VFEI_LargeInfestation`, roaming insects,
  infested crashes), bosses summoned with thumpers, creep terrain, a player-run hive (Hivetech research: basic and
  standard are Medieval, exotic Industrial) with artificial hives, glow pods, heat pods, hive doors and walls, royal
  jelly (a chemical need), storytellers Empress Evil and Han Horde-mode, a spacer squad start (`VFEI2_PlayerOutpost`).
- For Rimfolk: nothing wrong. Bred insects are colony animals like any others. Known conflict with Real Ruins (see
  there).

### Rimsenal Faction Packs: Federation, Feral, Spacer (`rimsenal.federation`, `rimsenal.feral`, `rimsenal.spacer`)
- Links: https://steamcommunity.com/sharedfiles/filedetails/?id=736172213 , ?id=736207111 , ?id=3086137468 (no source)
- What they add: the Federation Peacekeeper Corps, a hostile high-tech faction that "never negotiates": Bion
  bio-drones (a separate race, butchered for crucible weapon materials), conscripts, suicide bombers, Federator mech
  suits, heat-damage crucible weapons. Ferals: savage raiders with crude bayonet guns made from slag, war beasts and
  mutants. Spacers: well-equipped spacer colonists with mechanoids and buildings.
- For Rimfolk: Rimfolk's raids and visitors pick hostile humanlike factions and make pawns from the faction's
  `basicMemberKind`. If a faction's basic member is a non-human race (a Bion), a Rimfolk deserter or tribute
  messenger could be one (**unconfirmed**). Check: require `kind.RaceProps.Humanlike` before using a faction's basic
  kind in Rimfolk's events.

### Rimsenal - Storytellers Pack (`rimsenal.storyteller`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=725952322
- What it adds: Accipiter Accidental (gentle, then ruthless), Sereniz Sadistic (many small incidents, fewer raids),
  Hildegarde Heroic (more big threats).
- For Rimfolk: nothing.

### Vanilla Storytellers Expanded - Perry Persistent (`vse.perrypersistent`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2149702069 , https://github.com/Vanilla-Expanded/VanillaStorytellersExpanded-PerryPersistent
- What it adds: storyteller `VSE_PerryPersistent`: events almost every day, regular faction quests, double the usual
  population target, and a rare "man in the coat" joiner (`VSE_ManInTheCoatJoin`).
- For Rimfolk: Rimfolk's world events come on top of a very busy storyteller; the frequency setting covers it.

### Vanilla Storytellers Expanded - Igor Invader (`zal.vseigorinvader`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3582195822 (continuation of ?id=2249879548; defName not
  checked, the VFE Mechanoid version is `VFEMech-IgorInvader`)
- What it adds: raids every 2 to 3 days, "Reinforcements!" raids when a colonist dies, double weapon damage.
- For Rimfolk: annoying rather than wrong. Rimfolk's own event battles add to constant raids, and every fight ends in
  a victory story, journal lines and a "we held" mood. Check: when the storyteller's defName contains "IgorInvader",
  skip or thin out Rimfolk's battles and victory moods.

---

## Weapons and gear

### Rimsenal: Murder Diversified, Augmented Vanilla Pack, Security Pack, Sophian and Urbworld styles (`rimsenal.core`, `rimsenal.evp`, `rimsenal.security`, `rimsenal.sophian`, `rimsenal.urb`)
- Links: https://steamcommunity.com/sharedfiles/filedetails/?id=725947920 (the core weapons pack, renamed from
  "Rimsenal - Core"), ?id=849231601 (the old "Enhanced Vanilla Pack", now "Augmented Vanilla Pack"; the id match is
  inferred from the author and name), ?id=736139206 , ?id=2655926624 , ?id=2908039338
- What they add: four corporate weapon lines with their own mechanics, mid-tier vanilla-style guns, defensive
  structures (grenade lobber turrets, a heavy rotary turret, a cannon, decoy dummies, explosive fuel barrels), and two
  Ideology styles reskinning vanilla weapons (space Byzantine, dieselpunk).
- For Rimfolk: nothing. The fuel barrels are not processors, so `IsFermenter` ignores them; automatic turrets count
  for the defense advice.

### Vanilla Weapons Expanded family (`vanillaexpanded.vwe`, `vwec`, `vweg`, `vwel`, `vwenl`, `vweq`)
- Links: https://steamcommunity.com/sharedfiles/filedetails/?id=1814383360 (base), ?id=2375692535 (Coilguns),
  ?id=2194472657 (Grenades), ?id=1989352844 (Laser), ?id=2454918354 (Non-Lethal), ?id=1906780517 (Quickdraw);
  Vanilla-Expanded GitHub repos.
- What they add: the base pack's guns and melee from Neolithic to Spacer (research `VWE_TrenchWarfare`,
  `VWE_HeavyWeapons`, `VWE_LaserTargetingSystems`), coilguns (`VWE_MassDrivers`, Spacer), grenades, laser weapons
  (`VWE_LaserWeapons`; the weapons are Ultra), non-lethal taser, rubber-bullet pistol, dart gun, tear gas launcher and
  stun baton (`VWE_NonLethal`), and quickdraw sidearms (`VWE_Quickdraw`).
- For Rimfolk: nothing. Non-lethal weapons mean more downed raiders and prisoners, which Rimfolk's prisoner wishes
  already handle.

### Advanced Armaments, Unique Weapons Unbound, Possessed Weapons (`turkler.advanced.armaments`, `shunter.uniqueweaponsunbound`, `botchjob.possessedweapons`)
- Links: https://steamcommunity.com/sharedfiles/filedetails/?id=3350091075 , ?id=3707263149 , ?id=2982391372
- What they add: a powered tier of firearms between charge and conventional (with gestatable mech variants); trait
  editing for Odyssey's unique weapons at workbenches (add, remove, recolor, rename, three research tiers); ten
  "possessed" persona weapons as quest rewards, with fantasy names.
- For Rimfolk: nothing. Persona weapons are already excluded from Rimfolk's finds; a renamed unique weapon stays the
  same heirloom.

### Alpha Mechs, Alpha Implants (`sarg.alphamechs`, `sarg.alphaimplants`)
- Links: https://steamcommunity.com/sharedfiles/filedetails/?id=2973169158 , ?id=3275091331
- What they add: 7 hostile mechs, and with Biotech 26 more (utility, combat, bosses), mech armor and buildings;
  animal implants in four tiers, sized by body size (shambler-like, draftable or exploding animals).
- For Rimfolk: nothing.

---

## Animals and land

### ReGrowth: Extinct Animals, pphhyy's Demigryphs, Sanguinary Animals (`mlie.regrowthextinctanimals`, `pphhyy.demigryphsmod`, `pphhyy.sanguinaryanimals`)
- Links: https://steamcommunity.com/sharedfiles/filedetails/?id=3602926791 , ?id=3540496928 (continuation),
  ?id=2923829125 (original; a "Reborn" continuation exists at ?id=3521331935, which one owns the package id wasn't
  checked)
- What they add: 11 rare extinct animals (all tamable, toggles); nine Demigryph war-mount variants with eggs,
  crossbreeding, leather, helms, shields and banners; three nocturnal vampire-themed animals (the Vargulf mount, blood
  scarabs that give blood with Biotech, a vampire deer).
- For Rimfolk: nothing.

### ReGrowth 2: Wastelands (`local.regrowth2.wastelands`)
- Link: a local copy; the Workshop continuation is https://steamcommunity.com/sharedfiles/filedetails/?id=3803650100
  (an unofficial Chinese port for ReGrowth 2)
- What it adds: temperate, boreal and arid wasteland biomes with high pollution, barren terrain, toxic plants and
  animals, wasteland ruins, toxic rain and toxic thunderstorms that hurt anyone outdoors (roofs and tox resistance
  protect).
- For Rimfolk: the advisor doesn't explain toxic rain. Hook: add it to the weather and conditions advice ("stay under
  a roof").

---

## Visual, performance and library mods (no Rimfolk relevance)

Facial Animation (`nals.facialanimation`, `andromeda.facialanimation`), Nice Hands (`andromeda.nicehands`), Show Me
Your Hands (`mlie.showmeyourhands`), smoke and splash effects (`owlchemist.simplefx.smoke2`,
`owlchemist.simplefx.splashes`), Moonlight (`owlchemist.moonlight`), Scattered Flames (`owlchemist.scatteredflames`),
Door Clearance (`owlchemist.doorclearance`), Rimsenal hair (`rimsenal.hair`), Colored Deep Resources
(`kikohi.coloreddeepresources`), Dubs Paint Shop (`dubwise.dubspaintshop`), Dubs Performance Analyzer
(`dubwise.dubsperformanceanalyzer.steam`), TD Find Lib (`memegoddess.tdfindlib`), TD's bugfixes
(`memegoddess.tdsbugfixes`), Power Alerts (`diddily.poweralerts`), Trade Helper (`timmyliang.tradehelper`), Colony
Manager fork (`heaven.colonymanagerfork`; see Therapy for a reported conflict). Not researched: All Bamba Melee
(`bamba.allbambamelee.tiered`, a melee weapons pack).

---

## Summary

### (1) Where Rimfolk could be wrong or annoying with this modlist (by priority)

1. **Stuffed Floors [core system]: room plans come without a floor.** The mod makes `WoodPlankFloor`, the vanilla
   stone tiles, flagstones and metal tiles unbuildable (`designationCategory = null`) and replaces them with generated
   `<Stuff>_StuffedWoodFloorVanilla`, `<Stuff>_StuffedSmoothTiles` and so on. `RoomTemplateDef.ResolveFloor` finds
   none of its listed floors buildable and returns null for most templates. Check: when nothing in `floors` is
   buildable, look for the stuffed replacement of each listed floor, or take the cheapest floor in the same dropdown
   family from `RoomPlanConfig.FloorOptions`.
2. **Bunk Beds [core system]: the share-a-bed wish is fulfilled wrongly.** `WishWorker_ShareBed.IsFulfilled` accepts
   two partners on one bunk bed, and its self-help moves a colonist into a free bunk slot. `Avail.IsSingleBed` also
   accepts bunk beds, so a private bedroom plan can get one. Check: `Avail.IsDoubleBed` and no comp whose class name is
   `CompBunkBed`, for both the wish and the bed roles of private bedrooms.
3. **Vanilla Nutrient Paste Expanded [core system]: false "not enough food for winter".** Paste in vats isn't an item,
   so `resourceCounter.TotalHumanEdibleNutrition` misses it. Check: add the paste stored in the `VNPE_NutrientPasteNet`
   pipe nets, or soften the food advice when vats exist.
4. **Conjoined Twins (MultiMind): two people in one pawn.** Journals, trust, standards, life goals, quirks, offices,
   wishes and votes are kept per pawn, while MultiMind swaps name, backstories, traits and skills when the controlling
   twin changes. Separation surgery may replace the pawn (unconfirmed), which Rimfolk would read as someone vanishing.
   Check: detect MultiMind and key person data by profile (or pause wishes and journals for the inactive twin); carry
   history over on separation. Note that MultiMind Framework itself is missing from the modlist file.
5. **Smart Medicine [core system]: false "low on medicine" and an infirmary wish that stays open.** Medicine carried by
   doctors isn't counted (`AdvisorWorker_Medical`, `WishWorker_Infirmary.Medicine` count spawned things only). Check:
   add medicine in the inventories of colonists on the map.
6. **Dual Wield: an heirloom in the off hand counts as not carried.** `LegacyTracker.HeirloomCarriedBy` and the trader-goods
   wish look at `equipment.Primary` only. Check: `equipment.AllEquipmentListForReading`.
7. **Gravcamp (unconfirmed): era misread and a home that keeps moving.** If its nomad scenarios grant Odyssey's
   gravtech research and two of those projects are Spacer, a medieval camp is read as Spacer. Check: leave the gravtech
   projects out of `Avail.ResearchedTech` (or point players to the Era setting); expect graves and plans left behind
   after each move.
8. **Igor Invader: too many fights.** Raids every 2 to 3 days plus Rimfolk's own event battles and a victory story and
   mood after each. Check: skip or thin Rimfolk's battles and victory moods when the storyteller's defName contains
   "IgorInvader".
9. **Fairycore (unconfirmed): deaths without a corpse and Pixies without weapons.** If the delirium explosion removes
   the pawn without `Pawn.Kill`, the death is missed and the colonist later vanishes. The weapon wish could ask for a
   gun a Pixie can't equip. Check: `EquipmentUtility.CanEquip` before a weapon wish; consider treating a colonist
   destroyed while alive as a death.
10. **Rimsenal factions (unconfirmed): non-human event pawns.** Rimfolk makes deserters, messengers and visitors from a
    faction's `basicMemberKind`; for the Federation that may be a Bion. Check: require a humanlike kind. Related, for
    every xenotype mod here: "lost family" relatives come from the player faction's kind, so a Bogleg's sibling arrives
    as a baseliner. Check: copy the colonist's xenotype (and race) into the relative's generation request.
11. **Therapy [core system]: a couch steals a room.** A therapy couch and a chair turn a small dining or rec room into
    a "therapy office", so dining-room features (meeting venue, dining advice) lose it. Check: count a room as a dining
    room if it has eating tables even when its role is `TherapyOffice`, or warn in the room plan tips.
12. **Humanoid Alien Races: wishes a race can't use.** Per-pawn restrictions on beds (`validBeds`), food, apparel and
    buildings are not checked. Check by reflection when HAR is loaded (`CanUseBed`, `RaceRestrictionSettings.CanEat`,
    `CanWear`, `CanBuild`). Low priority while no HAR race is confirmed in the list.

Mods that look like "core system" mods a player keeps every run, as with Dubs Bad Hygiene: RimFridge, Bunk Beds,
Gastronomy with Cash Register (with Hospitality), Therapy, Vanilla Nutrient Paste Expanded, Stuffed Floors, Smart
Medicine, Doors Expanded, Set Up Camp, and for combat players Dual Wield. Of these, RimFridge is already handled.

### (2) Best soft integrations (small, only when the mod is active)

- **Therapy:** a Mood tab hint for long negative memories ("a session on the therapy couch"), a therapy office room
  plan (couch plus chair), and a nod to skilled therapists in the physician's office.
- **RimFridge:** a fridge slot in the kitchen and dining room plans once Air Conditioning is researched.
- **Bunk Beds:** bunk beds in the barracks plan to fit more sleepers.
- **Gastronomy:** the restaurant as a council meeting venue; service moods (`Gastronomy_ServicedMood`) as everyday
  journal material; a hosting ambition counting register takings.
- **Rimatomics and ReGrowth Wastelands:** stay-indoors advice for `RadioactiveFallout`, `NuclearFallout` and toxic
  rain, like toxic fallout.
- **VFE Empire:** ballroom and gallery room plans and a royal wish variant from the `VFEE_Title*` thoughts.
- **Det's Avaloi:** let the "steady supply" wish see gene dependencies (`Hediff_ChemicalDependency`), so an Avaloi
  asks for alcohol.
- **Set Up Camp:** "letters from the camp" journal entries for colonists in a persistent camp.
