# Batch D: nature, animals, land and farming

This batch covers the living world of the medieval modlist: what roams the map (Alpha Animals, Vanilla Animals Expanded
and its Royal and Endangered add-ons, Dinosauria, Megafauna, the Monterey Bay animals), how animals behave and work
(Animals Logic, Giddy-Up 2, Hunt for Me, animal beds, Misc. Training), what the land looks like and does (Alpha Biomes,
More Vanilla Biomes, ReGrowth 2 and Aspen, Geological Landforms and Biome Transitions, Grand Rivers Reborn, Beautiful
Outdoors), the sky (Seasonal Weather, Vanilla Temperature Expanded), what grows (Vanilla Plants Expanded and three
add-ons, Vanilla Furniture Expanded - Farming, Tilled Soil, Gauranlen Tree Expanded, Alpha Bees) and what is dug up
(Medieval Overhaul Deep Vein Mining, Mine Sight, Dismantle Ancient Junk). For Rimfolk this is the raw material of the
"Strangers, beasts and pets" layer: wild encounters (`EncounterDef` with `StrangerKind.Wild`/`Pet`), beast nemeses
(`NemesisTracker`, any wild `Faction == null` animal that downs, maims or kills a colonist and gets away), the hunt,
beast, companion, pet-bed, hurt-animal, hay, honey, beehive, flowers and bath wishes, and the advisor's season, food and
game-condition checks. None of these mods is referenced by package id anywhere in `Source/` today; every current
interaction comes from Rimfolk's generic matching (body size, wildness, predator flag, plant purpose, names such as
"beehouse", "dummy", "honey", "barrel"). Research was done from Steam Workshop descriptions (via the Steam Web API) and,
where a public repository exists, from the mods' own XML and C# (shallow clones, read only).

Key Rimfolk facts used below (from `Source/ColonyCouncil`):
- `Strangers.IsMajestic`: wild animal with `baseBodySize >= 2.5` or Wildness `>= 0.95`; used by the "great beast"
  encounters (`Council_EncC_GreatBeast`, `Council_EncC_GreatBeastClose`). `Council_EncC_HugeTracks` needs body size 2.4+.
- `NemesisTracker.IsFoe`: humanlike hostile faction pawns, or animals with `Faction == null`. Faction animals and
  insects (Black Hive, vanilla insectoids) never become nemeses. A beast that gets away calm gets a name from
  `Council_Pool_BeastNames` and `WishWorker_Beast` lets the victim, friends (opinion 30+) or passionate fighters ask to
  hunt it; promising it adds a Hunt designation.
- `WishWorker_Hunt` (Council_Wish_Hunt): needs a ranged weapon; prey = wild, not predator, body size 0.4+, manhunter on
  damage chance <= 5%; fulfilled only by the colonist's own `KillsAnimals` record.
- `WishWorker_Companion`: wild animals with Wildness < 0.85 and a trainability; self-help marks the nearest one for
  taming, with no predator or tame-fail filter.
- `Council_DlmS_PredatorDen`: any wild predator within 40 cells; the "Hunt it" option designates it for hunting.
- `WishWorker_Flowers`: counts plants with `PlantPurpose.Beauty`, growth > 0.3, in the home area (wild ones too).
- `Council_Wish_Beehive`: buildings whose defName/label contains beehive, beehouse, apiary or "bee house".
- `Council_Wish_Honey`: 25 items whose name contains "honey" (not "honeydew"); gourmands only.
- `Council_Wish_Bath`: buildings named bath, tub or sauna (terrain does not count).
- `Council_Wish_TrainingDummy`: buildings named dummy, archery, practice target, training target or marksmanship.
- `Avail.IsAnimalBed`: any bed that is not humanlike; used by the pet-bed wish and the barn room plan role `animalbed`.
- `Avail.IsHydroponics`: any `Building_PlantGrower` whose sow tag is not DecorativePlant (planter boxes count).
- `Avail.IsFermenter`: vanilla barrels, or item/advanced-resource processors named barrel, ferment, brew, keg, cask, still.
- `Flavor` weather tags and the storm quirk: defName contains "Fog" -> fog; "Thunder" or "Storm" (case-sensitive) -> storm.
- `AdvisorWorker_Conditions`: knows only vanilla conditions (ToxicFallout, VolcanicWinter, SolarFlare, HeatWave,
  ColdSnap, Flashstorm, PsychicDrone, NoxiousHaze, Drought, Eclipse, Aurora).

---

## Alpha Animals (`sarg.alphaanimals`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1541721856 (source: https://github.com/juanosarg/AlphaAnimals, wiki https://rimworldbestiary.wiki.gg/)
- What it adds: 100+ "vanilla-friendly but weird" creatures (139 race ThingDefs in the 1.6 core folder, defNames
  `AA_*`), each with a mechanic: walking tanks, living resource farms, night stalkers, giant spiders, plant monsters.
  Requires Harmony and Vanilla Expanded Framework. Adds the Black Hive insect faction (`AA_BlackHive`, raid incident
  `AA_Incident_BlackHive`). Map incidents: `AA_IncidentStalkingArcticLion` (ThreatBig: an arctic lion stalks one
  colonist, nearly invisible until it strikes; drafted, alert colonists and turrets can detect it),
  `AA_IncidentFeraliskClutchMother` / `Blizzarisk` / `Dunealisk` (a huge egg-carrying spider wanders in),
  `AA_IncidentSummitCrab` (ThreatBig: a boulder-sized bouldermit, `AA_SummitCrab`, body size 15, nearly impervious to
  gunfire, wrecks what is in its path), `AA_Incident_Gallatross` (`AA_Gallatross`, body size 6, wildness 0.99),
  `AA_BehemothPasses` (a herd of fire-breathing behemoths, `AA_Behemoth`, body size 8), `AA_Incident_Aerofleets`,
  `AA_Incident_Bumbledrones`, `AA_IncidentAnimaliskEnters` (Royalty), `AA_IncidentFungalHusk`, `AA_Eclipse`, a resource
  pod crash and a wanderer quest. Mod options toggle each event (Black Hive raids, stalking lions, clutch mothers,
  gallatross, summit crab, behemoth, animalisk) and each animal's spawning. Optional "cute" retextures.
- In play: animals spawn by biome; spawn rates were balanced without other animal mods, so with VAE, Dinosauria and
  Megafauna each mod's animals appear less often. Several new pack animals (`AA_NightMule`, `AA_SpinedGow`,
  `AA_ChameleonYak`, `AA_Gigantelope`, `AA_MammothWorm`, `AA_CrepuscularBeetle`, `AA_Bobeene`). Giddy-Up compatible;
  the Meadow Ave and Mammoth Worm get improved stats as mounts. Black Hive raids need the faction added when VEF prompts
  on an existing save. Compatible with VPE (adds psycasts), VAE Achievements, Alpha Biomes. Pitfall: many predators
  have wildness below 0.85 and a high revenge chance (e.g. `AA_SandProwler` wildness 0.65, manhunter on damage 1.0;
  `AA_Atispec` body size 7, tame-fail manhunter 0.5).
- Rimfolk today: no explicit tie-in. Generic effects: dozens of animals qualify as "majestic" (body size 2.5+ or
  wildness 0.95+, e.g. gallatross, behemoth, cinderlisk 0.985, blizzarisk/dunealisk 0.985, wildpod 0.95), so the great-beast
  encounters fire more often; mauling predators become named beast nemeses; Black Hive insects never become nemeses
  (faction pawns that are not humanlike are not foes).
- Hook ideas:
  - Stalking arctic lion (`AlphaBehavioursAndEvents.IncidentWorker_StalkingArcticLion`): it targets one colonist, so
    Rimfolk could pre-announce a "hunted" nemesis or give that colonist a dread mood/encounter ("something watches me
    from the snow") and a dream omen before it strikes.
  - Gallatross, behemoth and summit crab passes: a dilemma "let the giant pass or try to bring it down" (meat and
    trophies vs risk), plus a "known far and wide" style chronicle line if it is killed; candidate for an ambition
    "the great hunt".
  - Clutch mothers: an urgent dilemma (burn the eggs now vs wait), tie to the beast nemesis system if it mauls someone.
  - Companion wish self-help and the predator-den dilemma should skip animals with high `manhunterOnTameFailChance`
    or body size above a threshold, now that such animals are common.
  - Black Hive raids: a vendetta-like "the hive" grudge is impossible today (no humanlike faction); could get its own
    insect-specific fear quirk.
- Confidence: high (Workshop page, repo XML for incidents, sizes, wildness and settings).

## Alpha Bees, formerly RimBees (`sarg.rimbees`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1558161673 (source: https://github.com/juanosarg/AlphaBees)
- What it adds: beekeeping. Wild bees are found in all biomes (wild hive items `RB_WildHive_*`); beehouses
  (`RB_Beehouse`, `RB_BunkerBeehouse`, `RB_ClimatizedBeehouse`, powered `RB_AdvancedBeehouse`,
  `RB_AdvancedClimatizedBeehouse`, `RB_SpacerBeehouse`) hold two bee species and produce combs; a brood chamber
  (`RB_BroodChamber`) multiplies bees and a hybridization chamber (`RB_HybridizationChamber`) breeds new species (more
  than 100). A honey centrifuge (`RB_Honey_Centrifuge`) turns combs into honey and wax; it is powered but works without
  electricity at 30% speed. Honeys `RB_BaseHoney`, `RB_TastyHoney`, `RB_PsychotropicHoney` and others; honey meals
  (`RB_SimpleHoney`, `RB_FineHoney`, `RB_LavishHoney`), honey-preserved meat and vegetables; mead (`RB_Mead`, a Social
  drug, "slightly stronger than beer") from mead must in a `RB_MeadFermentingBarrel` (25 must to 25 mead over 6 days,
  safe at 7 to 32 °C); wax candles (`RB_WaxCandle`) and wax as a building material; bee research table.
- In play: research `RB_BasicBeekeeping` (Medieval, 300), `RB_BeeBreeding` and `RB_BeeHybridizing` (Medieval, 500
  each), `RB_MeadBrewing` (Medieval, 300, needs Brewing), `RB_BeeswaxLighting` (Medieval, 300); climatized and
  advanced beehouses are Industrial, spacer beehouse Spacer. A working beehouse pollinates crops in a 6-tile radius:
  harvested plants have a 25% chance to leave a partly grown offshoot (bees must be active at harvest). Bees need the
  right average temperature on the whole map, even if the beehouse is indoors (a "greenhouse bees" option removes
  this). One beehouse does little; build several. Many bees from other mods (Alpha Animals, RimWorld of Magic, Alpha
  Genes, VCE, VFE Insectoids 2...).
- Rimfolk today: no explicit tie-in, but strong generic ones. `Council_Wish_Beehive` matches every Alpha Bees
  beehouse by the word "beehouse" and suggests `RB_Beehouse` once researched. `Council_Wish_Honey` counts every item
  with "honey" in its name: all honeys, and also honey meals and honey-preserved food. The mead barrel counts as a
  fermenter (`Avail.IsFermenter`: advanced resource processor named "barrel"/"ferment") for brewing wishes and plans.
  Mead is a Social drug, so it fills the "a little something to drink" wish (`WishWorker_LittleSomething`).
- Hook ideas: honey wish could prefer the `RB_Honeys` thing category over name matching; a "beekeeper" epithet from
  combs harvested; a summer encounter "stung at the hives"; a dilemma when a dangerous bee hybrid appears; mead in
  the midsummer feast tradition; candle-lit rooms counting for the light advice; luciferium-producing bees as a
  temptation dilemma.
- Confidence: high (Workshop page, repo XML/C#).

## Alpha Biomes (`sarg.alphabiomes`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1841354677 (source: https://github.com/juanosarg/AlphaBiomes)
- What it adds: 12 biomes: `AB_IdyllicMeadows` (giant flowers, easy, few predators), `AB_MycoticJungle` (huge mushrooms,
  little wood), `AB_GelatinousSuperorganism` (no rock, everything edible), `AB_TarPits` (slow tar, bumbledrone hives),
  `AB_OcularForest` (little wood), `AB_GallatrossGraveyard` (giant bones), `AB_MechanoidIntrusion` (almost no food),
  `AB_PropaneLakes` (colder than sea ice), `AB_RockyCrags` "forsaken crags" (permanent darkness), `AB_MiasmicMangrove`
  (diseases: spores allergy, bacterial gangrene, viral abasia, intestinal parasites), `AB_FeraliskInfestedJungle`
  (predatory spiders), `AB_PyroclasticConflagration` (lava, fire). Biome buildings under Furniture -> Biome buildings
  (bone drill, slime compressor, tar pump, propane stove/smithy/smelter/heater, exotic walls and doors, gallatross
  bones). Weathers include `AB_PetalStorms`, `AB_RedFog`, volcanic ash, forsaken fog/rain/thunderstorm, acid rain; game
  conditions `AB_AcidRainCondition`, `AB_VolcanicHeatWave`. Odyssey content (design doc linked on the page).
- In play: needs a new world; biomes have unusual temperature/rainfall so they may not appear on every planet. Native
  compatibility with Alpha Animals, Dinosauria, Megafauna and VAE. Several biomes are very hard for a pre-industrial
  colony (little wood or food); Idyllic Meadows is the easy one. Dub's Bad Hygiene pawns may try to wash in propane and
  lava (visual only).
- Rimfolk today: no explicit tie-in. Weather tags: `AB_PetalStorms` and `AB_ForsakenThunderstorm` count as storms for
  flavor lines and for colonists with the storm-dread quirk; `AB_RedFog` and forsaken fog count as fog. Its conditions
  are unknown to the advisor.
- Hook ideas: add `AB_AcidRainCondition` and `AB_VolcanicHeatWave` to `AdvisorWorker_Conditions`; biome-flavored
  encounter and dream lines (keyed on `map.Biome.defName`); a dilemma in Miasmic Mangrove about a sick colonist; the
  forsaken crags' darkness for fear-of-the-dark quirks; a gallatross-bone trophy room plan.
- Confidence: high (Workshop page, repo XML).

## Animals of Monterey Bay (`ezfi.montereybayaquariumanimals`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3565659222
- What it adds: two animals made for a 2025 Monterey Bay Aquarium charity contest: bone-eating worms (most temperate
  and tropical coastal biomes; they eat desiccated corpses) and sea otters (temperate and moderately cold coasts,
  intermediate trainability). Planned but not yet done at last check: taming the worms, VAE Endangered integration,
  a Royalty "foster pups" quest.
- In play: small, coastal only; harmless flavor. Requires Harmony.
- Rimfolk today: nothing yet (sea otters can be the subject of pet encounters like any tame animal).
- Hook ideas: a coastal encounter (otters floating on their backs, a colonist watching); worms cleaning old bones for a
  grave-related line.
- Confidence: medium (Workshop page only; package id matches the author "ezfi").

## Animals Logic (`oblitus.animalslogic`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1098354593 (source: https://github.com/quicksilverfox/RimworldMods, folder AnimalsLogic)
- What it adds: Harmony-only quality-of-life for animals: production info in the animal's info window
  (milk/eggs/wool/reproduction); explosive animals do not explode when anesthetized (safe "euthanize by cut" with
  herbal medicine); optional animal use of nutrient paste dispensers; medical emergency alerts for animals;
  configurable hauling wander, tameness decay threshold and training decay speed; wool from butchered corpses;
  predators hunting your pawns are treated as hostile by your faction (recognized as a threat, no forced response);
  animals stop eating random non-food items; optional "generic meat becomes chicken". Settings class
  `AnimalsLogic.Settings` (`hostile_predators`, `medical_alerts`, `shear_corpses`, `prevent_eating_stuff`...).
- In play: safe to add and remove. Incompatible with Hardworking Animals. Known issue: furniture that is internally an
  animal bed is treated as one. The current GitHub source (commits newer than the Nov 2025 Workshop build) also has a
  colonist "hunting spree" major mental break (`AL_HuntingSpree`, state `AL_HuntingWildAnimal`), playing with animals
  as recreation (`AL_PlayWithAnimal`), easier taming of young animals (`taming_age_factor`) and obedient animals
  coming when called for training (`come_when_called`); none of these is confirmed in the Workshop build.
- Rimfolk today: nothing explicit. The animal medical alert pairs naturally with Rimfolk's "hurt animal" wish
  (a bonded animal below 70% health or needing tending).
- Hook ideas: if the hunting spree ships, a spree that kills a named beast could count as the victim's revenge; a
  colonist mauled during a spree is a natural beast nemesis; "play with animals" could feed pet encounters.
- Confidence: high for Workshop features, low for the GitHub-only ones.

## Dinosauria (`spincrus.dinosauria`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1136958577 (Ludeon thread linked there)
- What it adds: 32 dinosaurs and pterosaurs that spawn across biomes (tyrannosaurus, spinosaurus, brachiosaurus,
  triceratops, stegosaurus, ankylosaurus, utahraptor, velociraptor, quetzalcoatlus...). Rare overall; theropods harder
  to tame than thrumbos; stegosaurus, triceratops, magyarosaurus and ankylosaurus are pack animals with big carrying
  capacity; all lay eggs, some lay unfertilized eggs for farming; triceratops horns and T-rex skull drop as exotic
  items; built-in patches for A Dog Said and Giddy-Up.
- In play: maintained for new versions but no new features (the author points to Biomes! Prehistoric). Modded biomes
  need patches for spawns; More Vanilla Biomes and Alpha Biomes ship them.
- Rimfolk today: no explicit tie-in. Large species are "majestic" (great-beast encounters, huge-tracks encounter at
  2.4+); a theropod that mauls a colonist and leaves becomes a named beast nemesis; predator-den dilemma can pick a
  raptor or a T-rex.
- Hook ideas: dinosaur egg harvest as a stock wish; a "trophy" wish for the T-rex skull; a beast nemesis special line
  when the beast is a theropod; pack dinosaurs for caravan wishes.
- Confidence: medium-high (Workshop page; no repo checked, defNames not verified).

## Megafauna (`spino.megafauna`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1055485938 (wiki http://megafaunarimworld.wikia.com)
- What it adds: 38 prehistoric animals that spawn naturally (woolly mammoth, paraceratherium, titanoboa, smilodon,
  short-faced bear, megaloceros, aurochs, gigantopithecus, deinotherium, purussaurus...). Many are pack animals
  (paraceratherium "your new best caravan buddy"); woolly mammoth, elasmotherium and josephoartigasia are shearable;
  several lay eggs; sivatherium, macrauchenia, aurochs cows and uintatherium are milkable. Options to disable any animal,
  change spawn commonality, or disable vanilla animal spawns. Auto-patches A Dog Said and Giddy-Up (load after them).
  Some rare ones are not sold by traders (paraceratherium, titanoboa, elasmotherium, megalania, gomphotaria,
  short-faced bear).
- In play: safe to add mid-save (update butcher bills). Rare and dangerous to tame or slay.
- Rimfolk today: no explicit tie-in; same generic effects as Dinosauria (majestic encounters, beast nemeses).
- Hook ideas: "the mammoth hunt" as a seasonal tradition or ambition; aurochs as a medieval-flavored livestock line.
- Confidence: medium-high (Workshop page; defNames not verified).

## Vanilla Animals Expanded (`vanillaexpanded.vanillaanimalsexpanded`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2871933948 (source: https://github.com/Vanilla-Expanded/VanillaAnimalsExpanded)
- What it adds: the merged VAE biome packs: dozens of vanilla-style animals for every biome (51 race defs load with
  Odyssey active; 12 more, such as hippo, walrus, musk ox, moose, gorilla, alligator, tiger, seal and penguin, load
  only without Odyssey, which ships its own). Dog breeds (`AEXP_GreatDane`, `AEXP_GermanShepherd`, `AEXP_Rottweiler`,
  `AEXP_Corgi`, `AEXP_Beagle`...) and cat breeds (`AEXP_CatMaineCoon`, `AEXP_CatPersian`, `AEXP_CatSiamese`...), all
  wildness 0 (trader/tame animals). Big ones: `AEXP_Giraffe` (4.0), `AEXP_MegaWolverine` (3.8, predator, wildness
  0.97), `AEXP_IndianElephant` (3.3, pack), `AEXP_Anaconda`, `AEXP_Camel`, `AEXP_Zebra`. River animals
  (`RiverNeedingAnimalDefs`: crocodile, beaver) and a beaver dam building (`AEXP_BeaverDam`). Mod options disable any
  animal. Requires VEF.
- In play: most spawn in Temperate Forest, others in their biomes. Wiki and a balance spreadsheet are linked.
- Rimfolk today: no explicit tie-in. Dogs and cats are ideal pets for Rimfolk's pet encounters, pet beds and
  farewells; the megawolverine counts as majestic; the crocodile (predator, wildness 0.84) passes the companion wish's
  wildness filter.
- Hook ideas: breed-specific pet lines (a corgi herding chickens); river animals as encounters on river maps; beaver
  dam as a "they built a dam on our river" moment.
- Confidence: high (Workshop page, repo XML).

## Vanilla Animals Expanded — Royal Animals (`vanillaexpanded.vaeroy`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2858079457 (source: https://github.com/Vanilla-Expanded/VanillaAnimalsExpanded-RoyalAnimals)
- What it adds: 9 Empire animals, only from Imperial traders and quests (needs Royalty): orangutan (`VAERoy_Orangutan`:
  conversations boost social recreation and give a little XP in a random skill), crane (`VAERoy_Crane`: with 5 or more
  cranes, every titled pawn gets +5 mood), megachicken (`VAERoy_Megachicken`: grows through five stages at 0, 3, 8, 12
  and 18 years), pheasant (`VAERoy_Pheasant`: when fleeing, a once-a-day psychic shockwave in 6 cells gives your pawns
  brief psychic invisibility), royal tiger (`VAERoy_RoyalTiger`, flees danger), angora rabbit (`VAERoy_AngoraRabbit`:
  its bonded master gets better neural heat dissipation; the rabbit dies of grief if the master dies). Quail (meat turns
  simple or fine meals into lavish), peacock (nuzzles raise psychic sensitivity 25% for 12 hours, stacking to 8) and swan
  (builds one high-beauty nest as its sleeping spot): with Odyssey active these patch Odyssey's own birds instead of
  adding duplicates. Thoughts for tutor animals dying or lost (`VAERoy_TutorDied`...), `VAERoy_CraneThought`.
- In play: noble-focused; needs an Empire in the game (Medieval Empire Overhaul in this list keeps one).
- Rimfolk today: nothing explicit. A swan nest is a non-humanlike bed, so Rimfolk's `IsAnimalBed` would count it.
- Hook ideas: a noble's wish for cranes (5 needed) next to "a room befitting their title"; the angora rabbit's death of
  grief as a special pet farewell; orangutan tutors in the coming-of-age apprenticeship.
- Confidence: high (Workshop page, repo XML and patches).

## Vanilla Animals Expanded — Endangered (`vanillaexpanded.vaeendandext`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2366589898 (source: https://github.com/Vanilla-Expanded/VanillaAnimalsExpanded-Endangered)
- What it adds: 11 endangered or extinct animals that never spawn wild: black rhino, giant panda, moa, quagga (pack),
  thylacine, African wild dog, bonobo, Tasmanian devil, black-footed ferret, giant pangolin, rockhopper penguin. Event
  `AEXP_ExtintAnimalsPasses` ("extinct animals pass", Misc, base chance 0.7, refire 13 days): a male and a female of one
  species whose climate fits walk toward the map center and leave after 1.5 to 2.5 days. Gizmo "Release to the wild" /
  "release to repopulate" (`JobDriver_GoToWild`): the animal walks off the map and every faction whose def is vanilla
  `OutlanderCivil`, `OutlanderRough`, `TribeCivil` or `TribeRough` gains +2 goodwill. An option adds the button to all
  animals.
- In play: tame the pair before they leave, breed, release offspring for goodwill. Pitfall in this modlist: the
  goodwill only reaches those four vanilla faction types; medieval faction mods' factions get nothing.
- Rimfolk today: nothing explicit. The pair can trigger the companion wish (if their wildness is below 0.85).
- Hook ideas: a dilemma when the pair arrives ("tame them, or let them pass"), with the colony's merciful/open ways;
  releasing an animal as a mercy-way deed and a chronicle line; a life goal "save a species".
- Confidence: high (repo C# for the event, release job and faction list).

## Giddy-Up 2 - Continued (`memegoddess.giddyup`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3674332861 (source: https://github.com/MemeGoddess/GiddyUp2, wiki on GitHub)
- What it adds: riding. Ride and Roll (pawns mount an animal for long trips; "no mount" and "drop animal" areas;
  `JobDefs` Mount, Dismount, WaitForRider), Battle Mounts (melee and shooting from the saddle at reduced accuracy,
  the mount fights too; enemies and visitors can arrive mounted), Caravans (leave the map mounted), Mechanoids
  (Biotech), SaddleUp button for drafted pawns. Settings in `GiddyUp.ModSettings_GiddyUp`: pre-industrial enemy mount
  chance 33% (industrial 15%), same for visitors; a raider needs Animals skill above 3 to spawn mounted
  (`minHandlingLevel`); `noMountedHunting`; `minAutoMountDistance` 120. Data: `GiddyUp.ExtendedDataStorage`
  (WorldComponent; static `isMounted` HashSet of thingIDNumbers), `ExtendedPawnData.Mount`.
- In play: in a medieval world nearly every faction is pre-industrial, so about a third of eligible raiders ride in.
  Pawns ignore mounts slower than walking or too weak to carry them. Known incompatibility listed on the page: "Allies
  are helpful" (in this modlist). Uninstall the original Giddy-Up if present.
- Rimfolk today: nothing explicit. A named or bonded mount is a pet like any other (farewell when it dies; the "hero"
  farewell during or right after a battle). Whether a mount fighting under its rider registers as "fought beside" depends
  on the mount's `mindState.lastAttackTargetTick` being set by its attacks (likely but untested).
- Hook ideas: a beast nemesis or raider nemesis that "rode in"; a mounted raider who flees as a natural nemesis; a
  colonist wish for a horse of their own (bonded mount) or for a stable; encounter lines on horseback; a council decree
  "cavalry drills".
- Confidence: high (Workshop page, repo C#).

## Animal beds (`animal.bedsnew`)
- Link: most likely "More animal beds", https://steamcommunity.com/sharedfiles/filedetails/?id=3107972819 (by
  RavenousEye with FarmerJoe, Sovereign and DetVisor). Alternative candidate: "Large Animal Beds" (id 1111387020, by
  cucumpear). The package id could not be confirmed from a source.
- What it adds (More animal beds): bigger luxury animal beds in a standard and a prestige version; both give animals
  a "well rested" buff, the prestige one slightly stronger.
- In play: furniture only; safe.
- Rimfolk today: any non-humanlike bed counts for Rimfolk's pet-bed wish and the barn room plan's animal-bed slots,
  so these beds are accepted (and can be swapped in the planner).
- Hook ideas: a "spoiled pet" encounter when a pet sleeps in a prestige bed; barn plan preferring the best bed.
- Confidence: medium (identity of the mod), high for the Rimfolk behavior.

## Misc. Training (`haplo.miscellaneous.training`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=717575199 (medieval look: "Misc. Training Medieval
  Retexture", `serek.misctrainingmedievalretexture`, in this modlist)
- What it adds: shooting target (trains shooting), martial arts dummy (trains melee), archery target (recreation by
  shooting arrows). This variant includes a training work task. The medieval retexture changes the look and the
  accepted materials (existing dummies keep their old material).
- Rimfolk today: `Council_Wish_TrainingDummy` matches the martial arts dummy ("dummy") and the archery target
  ("archery") by name, so building either fulfils it and the council can suggest them. The shooting target's name
  matches none of the words, so it is not suggested and does not count on its own (unverified defName).
- Hook ideas: count training-task hours toward the "drills" decree; a sparring encounter between rivals at the dummy;
  epithet "Deadeye" from archery-target practice.
- Confidence: medium-high (Workshop page; defNames not checked).

## Hunt for Me (`arandomkiwi.huntforme`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1593245720 (source: https://github.com/aRandomKiwi/Hunt-For-Me)
- What it adds: a "hunting" trainable (`HuntingTraining`: Advanced trainability, prerequisites Haul and Obedience,
  body size 0.4+, 7 steps). Trained predators hunt alone or in packs (up to 16 by default), haul the corpse to the
  nearest stockpile, avoid big prey in "safe hunting" mode, can be limited to designated targets ("supervised"
  mode), can be forced to hunt every X hours; cats bring "gifts" of small prey (`HuntForMe_CatGift`). Animals marked
  for taming are skipped. Option: all animals can hunt.
- In play: the cat list is a fixed defName list (vanilla `Cat` and the akaNEKO cats); VAE's cat breeds
  (`AEXP_Cat*`) are not on it unless added in the settings list. Alpha Animals' WillPods can down a whole pack.
- Rimfolk today: nothing explicit. Rimfolk's hunt wish counts only kills by the colonist (`KillsAnimals` record), so
  an animal's kill does not fulfil it. Rimfolk's beast wish and predator-den "Hunt it" add a Hunt designation, which
  supervised-mode hunters treat as a target (likely; not tested).
- Hook ideas: a "Beastfriend" or "Huntmaster" epithet from animal kills by your animals; a pet encounter when a
  trained hunter brings down something; the cat's gift as a real event behind Rimfolk's "dead rat as a gift" encounter.
- Confidence: high (repo XML/C#).

## ReGrowth 2 (`regrowth.botr.core`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2260097569 (source: https://github.com/Helixien/ReGrowth2)
- What it adds: atmosphere and nature for every biome (in 1.6 the old biome modules are merged in): many wild plants,
  including wild flowers with `PlantPurpose.Beauty` (`RG_Plant_BlueDandelion`, `RG_Plant_LupineIceland`,
  `RG_Plant_Saxifraga`, `RG_Plant_Cathedralis`, `RG_Plant_Dervish`, tundra cotton), berry bushes and mushrooms (research
  `RG_BerryCultivation` 600 and `RG_MushroomCultivation` 500, both Neolithic), boulders, ancient ruins, a caravan camp
  site (`RG_CaravanCamp`), creatures (`RG_WoollyStegosaurus` body size 4, `RG_Rimclaw` predator, hemolope and
  neutrolope, kiwi, rabbit), weathers (`RG_Windy`, `RG_Drizzle`, `RG_FogStorm`, `RG_FoggySnow`), incident `RG_DevilDusts`
  (dust devils that damage pawns and animals). Bathing as recreation (`RG_BatheJoyGiver`) with thoughts: bathed in
  water +4 for half a day, bathed in a hot spring +6 for a day (hot spring terrain `RG_HotSpringSand`), wet -1, extremely
  wet, sandy. More wood on maps. Not part of the VE series.
- In play: most options need a restart. Built-in support for VEF, VPE, VPE Mushrooms, VAE, VPsycastsE, Vehicle
  Framework; Medieval Overhaul has world-map beautification built in; a separate "Medieval Overhaul ReGrowth 2 Patch"
  exists on the Workshop.
- Rimfolk today: nothing explicit. Wild beauty flowers growing in the home area count toward Rimfolk's "flowers around
  the colony" wish. `RG_FogStorm` counts as both fog and storm. Hot springs do not satisfy the bath wish (it wants a
  built bath, tub or sauna).
- Hook ideas: count `RG_HotSpringBathing` / `RG_BathingWater` memories (or hot spring terrain near home) toward the
  bath wish; a summer encounter at the river ("a swim after work"); dust devils in the advisor; woolly stegosaurus as a
  majestic encounter.
- Confidence: high (repo XML, 1.6 folder).

## ReGrowth 2: Aspen (`regrowth.botr.aspenforest`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2545774148
- What it adds: the Aspen Forest biome (`RG_AspenForest`, Skyrim-inspired: genetically engineered aspens, new flowers
  and grass). No special mechanics. ReGrowth 2 adds raspberry, boreal fern and tundra pine to it when both are active.
- In play: needs a new world to appear. Supported by VE Fishing, VAE, More Vanilla Biomes, Beautiful Outdoors.
- Rimfolk today: nothing yet (generic: its flowers can count toward the flowers wish if they are Beauty plants;
  unverified for this biome's own plants).
- Hook ideas: biome-keyed flavor (golden autumn leaves for fall lines).
- Confidence: medium-high (Workshop page; ReGrowth 2 compatibility patch).

## Beautiful Outdoors (`meltup.beautifuloutdoors`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2011794898 (source: https://github.com/Meltup/Beautiful-Outdoors)
- What it adds: patches beauty of plants and terrain (Beauty and BeautyOutdoors). From the 1.6 patch: rose 20,
  daylily 26, dandelion and astragalus 8, clivia 7; trees mostly 2 to 4 (oak and teak 5); non-edible plant base 1;
  healroot and berry bushes 2; rafflesia -12; soil 0 outdoors (-1 indoors), rich soil 1 outdoors; mud -4 (-2 outdoors),
  marsh -6 (-4 outdoors); shallow water 3, deep water 4, moving shallow 4, moving chest-deep 5, deep ocean 6; rock
  chunks -4; stone blocks -6. Patches for VPE (fruit trees, cherry 8, tilled soil 0), VPE Succulents (9 each), ReGrowth
  modules, Alpha Biomes and more. (The Workshop text still says chunks -1; the 1.6 patch sets -4.)
- In play: safe to add/remove; load late if a patch fails.
- Rimfolk today: nothing explicit; raises the payoff of Rimfolk's flowers and "time outside" style wishes and of the
  advisor's beauty-related advice.
- Hook ideas: an outdoor-beauty encounter (a colonist resting by the river) when outdoor beauty is high.
- Confidence: high (repo patches).

## Geological Landforms (`m00nl1ght.geologicallandforms`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2773943594 (source: https://github.com/m00nl1ght-dev/GeologicalLandforms, wiki there)
- What it adds: 43 biome-independent landforms, in 1.6 as `TileMutatorDef`s `GL_*` (archipelago, atoll, badlands,
  caldera, canyon, cave entrance, cirque, cliff variants, coast variants, cove, crater, desert plateau, dry lake, fjord,
  glacier, gorge, ice oasis, island, lake, landbridge, lone mountain, oasis, peninsula, rift, river variants, secluded
  cove, secluded valley, sinkhole, skerry, surface cave, swamp hill, tombolo, valley). 14 overlap with Odyssey and are
  disabled with it; its river landforms replace Odyssey's by default. Node-based landform editor; "Search for landform
  nearby" button in the world Terrain tab. Disables roof collapse on Impassable Mountains tiles (cave landforms).
- In play: islands and archipelagos are remote with little stone; canyons and rifts are easy to defend with ores but
  little food, wood, light or space. Optimized for 250x250 maps.
- Rimfolk today: nothing explicit. Cave and canyon colonies trigger Rimfolk's "time outside" wish (cooped up
  indoors) and its overhead-mountain insect warning more often.
- Hook ideas: read the tile's mutators (`map.TileInfo.Mutators`) for landform-aware flavor ("down in the valley", "on
  the island") and advice (island: plan stone; canyon: farmland); a secluded-valley "hidden home" colony character.
- Confidence: high (Workshop page, repo).

## Biome Transitions (`m00nl1ght.geologicallandforms.biometransitions`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2814391846
- What it adds: maps with up to seven biomes when you settle on a border; plants grow by the biome at their cell and
  animals wander in from neighboring biomes. Replaces Odyssey's two-biome "Mixed Biome" feature. Temperature and
  weather still come from the world tile. API: `GeologicalLandforms.BiomeGrid` (`map.BiomeGrid()`, `BiomeAt(IntVec3)`).
- In play: safe to add/remove (existing maps revert to one biome for spawning on removal). Yayo's Nature overrides it.
- Rimfolk today: nothing explicit.
- Hook ideas: encounter lines keyed on the biome at the colonist's position (`BiomeAt`), e.g. "at the edge of the
  desert"; more varied wild animals for the companion wish.
- Confidence: high (Workshop page, repo C#).

## Grand Rivers Reborn (`meltup.grandrivers.reborn`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1675451818 (source: https://github.com/Meltup/Grand-Rivers-Reborn)
- What it adds: wider rivers on the colony map (patch of `RiverDef.widthOnMap`): river 6 -> 15, large river 14 -> 23,
  creek 4 -> 7, huge river unchanged at 30. World map unchanged; no movement slowdown.
- In play: more water, more bridges needed; water is a natural moat. Feeds VPE More Plants' aquatic zones and VAE's
  river animals (crocodile, beaver).
- Rimfolk today: nothing yet.
- Hook ideas: river-side encounters (fishing, swimming, a flood), a "build a bridge" ambition goal.
- Confidence: high (repo patch).

## More Vanilla Biomes (`zylle.morevanillabiomes`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1931453053 (source: https://github.com/Zylleon/MoreVanillaBiomes)
- What it adds: 10 biomes (`ZBiome_GlacialShield`, `ZBiome_Badlands`, `ZBiome_CloudForest`, `ZBiome_Marsh`,
  `ZBiome_Grasslands` "stormy savanna", `ZBiome_CoastalDunes`, `ZBiome_Sandbar_NoBeach`, `ZBiome_AlpineMeadow`,
  `ZBiome_DesertOasis`, `ZBiome_Iceberg_NoBeach` "ice floes"), wild flowers `ZBiome_Plant_WildRose` and
  `ZBiome_Plant_WildDaylily` (children of the vanilla rose and daylily, not sowable). Load-folder patches for Alpha
  Animals, Dinosauria, Megafauna (`Spino.Megafauna`), Medieval Overhaul, ReGrowth Core and Aspen, VPE, VAE core and
  more.
- In play: needs a new world. Configurable Maps breaks sandbars; Terra Project incompatible.
- Rimfolk today: nothing explicit. Alpine meadow wild roses and daylilies inside the home area count toward the
  flowers wish (they inherit the Beauty purpose).
- Hook ideas: biome flavor (cloud forest fog lines, glacial shield cold); the alpine meadow "few predators" as a calm
  colony start for encounters.
- Confidence: high (Workshop page, repo XML).

## Seasonal Weather (Continued) (`mlie.seasonalweather`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2045114229 (source: https://github.com/emipa606/SeasonalWeather)
- What it adds: weather commonality per season; its patches cover only the 11 vanilla biomes (Alpha Biomes, More
  Vanilla Biomes and ReGrowth Aspen are not patched, so they most likely keep their normal weather).
  New weathers `Windy`, `Cloudy`, `FogSnow`, `Dust`, `DryDuststorm` (lightning, no rain), `DoubleRainbow`. Disasters:
  `Earthquake` (incident base chance 0.1, refire 15 days) and `Wildfire` (0.15, refire 30 days), each switchable
  (`EnableEarthquakes`, `EnableWildfires`, both on by default). Earthquake tremors walk a fault line across the map:
  every destroyable building on it that is not a wall or natural rock is destroyed (fueled ones start a fire, powered
  ones too), pawns on it are hurt (worse under a roof), roofs on the line collapse unless held by a wall, and floors are
  stripped. Wildfire seeds fires until no large fire remains (Workshop: at least 5 days if left alone, about half a
  day if suppressed, at most a quadrum). Plants grow 20% faster in full rain (growth x (1 + rainRate x 0.2)).
  Thought `WeatherDrone` "weather soothe" +7 ("I am a leaf on the wind") from a rare weather emanation.
- In play: medieval bases full of torches, braziers and campfires are exposed: a tremor through a lit brazier starts a
  fire. Wildfires need firefighters and firebreaks.
- Rimfolk today: `FogSnow` counts as fog and snow, `DryDuststorm` as a storm (storm detection is case-insensitive).
  `Wildfire` and `Earthquake` are in the advisor's condition table with an Almanac link. Since 0.15: firebreak advice
  on a dry, windy day in summer or fall with brush up to the home area (one button marks a 3-cell strip for cutting),
  and after an earthquake a work party goes round the roofs and walls, the chronicle remembers it and the annals count
  the quakes (`Weather/WeatherDisasters.cs`).
- Hook ideas: quirks from surviving an earthquake ("never sleeps under stone again"); a dilemma after a wildfire
  (rebuild vs move fields); double rainbow encounter/dream.
- Confidence: high (repo XML/C#).

## Vanilla Plants Expanded (`vanillaexpanded.vplantse`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2134308522 (source: https://github.com/Vanilla-Expanded/VanillaPlantsExpanded)
- What it adds: crops with a twist each (onion, pumpkin, pepper, peas, eggplant, cabbage, beet, tomato, agave),
  biome-locked fruit trees (apple, peach, cherry, plum, pear, banana, orange, opuntia, bearberry; two per biome, same
  nutrition), grass and tall grass for grazers (`VCE_Grass`, `VCE_TallGrass`). Plowing (`VCE_TilledSoil`, an Orders
  designation, Plants 1 needed): turns normal soil into 140% fertility tilled soil that reverts to soil after a
  harvest. Fruit trees need Tree Sowing and cannot be reinstalled. VCE condiment recipes with Vanilla Cooking Expanded.
- In play: plowing is manual micromanagement; beets cannot be planted in winter but keep growing.
- Rimfolk today: nothing explicit; crops feed the food advisor like any crop.
- Hook ideas: a "plow before spring" seasonal advisor tip; an orchard ambition (fruit trees are permanent).
- Confidence: high (repo XML).

## Vanilla Plants Expanded - More Plants (`vanillaexpanded.vplantsemore`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2748889667
- What it adds: grains (barley, rye, oats, sorghum, buckwheat), vegetables (lettuce, carrot, garlic, celery, radish,
  cassava, sweet potato, peanut, okra, beans, chickpea, bell pepper, sunflower, pineapple, blueberry), healshroom,
  canola, avocado/olive/almond/mango/coconut trees. Three new growing zones: flower garden (`Zone_GrowingFlowerGarden`,
  flowers `VCE_Lavender`, `VCE_FragrantbloomRose`, `VCE_Hyacinth`, `VCE_Jasmine`, `VCE_Lily`, `VCE_Orchid`,
  `VCE_Plumeria`, `VCE_Tulip`, `VCE_Vanilla`, `VCE_Violet`, `VCE_YlangYlang`, all Beauty purpose, 12 to 18 beauty),
  sandy zone (sand: melon, cucumber, rye, butternut squash, aloe) and aquatic zone (shallow and chest-deep moving water,
  marsh: taro, watercress, lotus, water chestnut). Sandy and aquatic crops ignore fertility but take long to sow and
  harvest. Vegetable milk research.
- In play: hard incompatibility of aquatic/sand plants with Grow Anywhere.
- Rimfolk today: flower-garden flowers are Beauty plants, so a flower garden in the home area fulfils the flowers wish.
- Hook ideas: count flower-garden zones in the advisor's beauty advice; a spring wish for a flower garden; grains as
  brewing inputs for a brewery ambition.
- Confidence: high (repo XML/C#).

## Vanilla Plants Expanded - Mushrooms (`vanillaexpanded.vplantsemushrooms`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3006389281
- What it adds: mushrooms for underground colonies (mushroom growing tag `VPE_Mushroom`): timbercap (wood-like
  harvest), healtruffle (herbal medicine; needs rotstink gas nearby), stinker (makes rotstink, edible fungus, dies in
  sunlight), bileceps (meaty harvest from bile of decaying bodies), mycothread (fabric, very slow), glowcap (light, too
  weak for crops), redcap (Beauty purpose, short-lived, pretty), zapcap (power on conduits), lullabloom (calming, by a
  bed, flower pot only), thermoregulus (pushes a room toward 16 °C, flower pot), foampuffer (bursts into firefoam near
  fire), blisscap (bliss and drowsiness, raises psychic sensitivity), toxpuff. Some appear wild in caves.
- In play: pairs well with Geological Landforms cave and canyon maps.
- Rimfolk today: redcaps are Beauty plants and count toward the flowers wish.
- Hook ideas: a mountain-colony "mushroom farm" ambition; lullabloom as a personal touch for poor sleepers.
- Confidence: high (repo XML).

## Vanilla Plants Expanded - Succulents (`vanillaexpanded.vplantsesucculents`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2198652536
- What it adds: 10 decorative succulents (`VCE_Plant_AloeVera`, `VCE_Plant_JadePlant`, `VCE_Plant_SnakePlant`...),
  Beauty purpose, beauty 8 (9 with Beautiful Outdoors), grow 3 days, live 36 days (about half a flower's beauty, three
  times the lifespan), research `VCE_SucculentSowing` (Neolithic, 1000). Some spawn wild in deserts, arid shrubland and
  tropical biomes.
- Rimfolk today: they are Beauty plants, so they count toward the flowers wish and suit the "a plant" bedroom wish
  and personal touches (plant pots).
- Hook ideas: prefer succulents when suggesting a plant pot to a busy colony.
- Confidence: high (repo XML).

## Vanilla Furniture Expanded - Farming (`vanillaexpanded.vfefarming`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1957158779 (source: https://github.com/Vanilla-Expanded/VanillaFurnitureExpanded-Farming)
- What it adds: planter box (`VFE_PlanterBox`, no research, no power, 95% fertility, hydroponic sow tag) and a
  draggable "tilable" planter box; scarecrow (`VFE_Scarecrow`, research `VFE_Res_FarmingTechniques`, Medieval, 300):
  every 600 ticks it scares wild animals of body size 2.1 or less within 10 cells away; predators whose prey can be
  human-sized are unaffected; tamed predators are never scared; a gizmo decides whether colony animals are affected.
  Sprinkler (Industrial, powered, flat growth boost, not for trees or hydroponics), draggable hydroponics and
  artificial ecosystems (Spacer, powered, 350% fertility).
- In play: in a medieval colony the planter box and scarecrow are the useful parts; planter boxes still need light
  (outdoors, or a sun lamp). A downed animal near a scarecrow spams errors (known).
- Rimfolk today: `Avail.IsHydroponics` accepts any non-decorative plant grower, so a planter box counts as
  "hydroponics" for Rimfolk; with no power and no research it is what the greenhouse wish and the food advisor would
  suggest in a pre-industrial colony, even indoors without light.
- Hook ideas: require a light source (sun lamp or unroofed cells) before suggesting planter boxes indoors; scarecrows
  in the advisor's crop-protection advice (deer and hares eating crops; Rimfolk's "crop thief" encounter).
- Confidence: high (repo XML/C#).

## Tilled Soil (`gt.sam.tilledsoil`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=725747149 (old source: https://github.com/sam23694/TilledSoil)
- What it adds: a buildable floor "tilled soil" (`GT_SoilTilled`) with 200% fertility, built on soil (GrowSoil);
  research "terrain rehabilitation" (`terrainRehab`, Medieval in the old source) lets you lay soil (`Dirt`) on smooth
  stone and other natural floors for underground fields.
- In play: permanent, unlike VPE's plowing (140% that reverts after harvest). Both are called "tilled soil"; the
  Tilled Soil one sits in the Floors menu, the VPE one under Orders.
- Rimfolk today: nothing explicit; Rimfolk's room plans for greenhouses do not lay floors.
- Hook ideas: a farming ambition "rich fields" counting fertile tiles; greenhouse plan option to lay tilled soil.
- Confidence: medium (Workshop page for 1.6 numbers; repo is from 1.0).

## Gauranlen Tree Expanded (Continued) (`mlie.gauranlentreeexpanded`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3025108023 (source: https://github.com/emipa606/GauranlenTreeExpanded)
- What it adds: replaces the Gauranlen tree's connection comp (`CompProperties_TreeConnectionExpanded`): up to 4
  connected pawns per tree (setting `MaxConnectedPawns`), dryad bonus up to 150% with more connected pawns
  (`MaxBonusDryad`), replanting (tree extraction), a "tear connection" ritual (`GauranlenTreeExpanded_TearConnection`)
  that gives "disconnected from tree" -10 mood for 5 days, a "protect the sanctuary" gizmo calling dryads to guard the
  tree, option to stop wild dryads. Custom defs `ConnectToTreeExpanded`, `PruneGauranlenTreeExpanded`,
  `DryadExpanded`, `TreeConnectionExpanded`.
- In play: incompatible with existing connections (cut connected trees or remove connections before adding) and with
  other Gauranlen mods. Needs Ideology for the tree itself.
- Rimfolk today: none in code. The Almanac's own Gauranlen pages (`Council_Alm_7_12`, `Council_Alm_7_13`) describe
  vanilla rules that this mod changes.
- Hook ideas: a "sacred grove" colony character; a dilemma before the tear ritual (who gives up the bond); dryad
  guardians as pets in Rimfolk's pet system.
- Confidence: high (repo XML/C#).

## Vanilla Temperature Expanded (`vanillaexpanded.temperature`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3202046258 (source: https://github.com/Vanilla-Expanded/VanillaTemperatureExpanded)
- What it adds: central climate control: air ducts, valves, subterranean ducts (`VTE_AirDuct`...), AC control unit,
  AC units, compressors (research `VTE_HVAC`, Industrial, after Air Conditioning), network efficiency (more devices
  than compressors support raises power use); industrial heater and cooler, wall heaters, wall-mounted heater and
  cooler, standing cooler, ceiling fans (Electricity or Air Conditioning). The only unpowered piece is the wall-mounted
  vent (`VTE_WallMountedVent`, Complex Furniture, 20 steel), built on a wall to share temperature between two rooms.
- In play: almost nothing for a pre-industrial colony except the vent.
- Rimfolk today: the council never suggests powered buildings before Electricity, so these stay out of its advice in a
  medieval run.
- Hook ideas: once industrial, recognize AC networks in the advisor's heat advice.
- Confidence: high (repo XML).

## Medieval Overhaul Deep Vein Mining (`phant0m5.modeepveinmines`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2942453140 (by Phant0m5)
- What it adds: a patch (requires Medieval Overhaul): the vanilla deep drill becomes a "deep vein mineshaft" and the
  ground-penetrating scanner a "prospecting desk", both medieval technologies, otherwise identical. Both still need 1W,
  supplied by a "mining bird" (separate from MO's messenger birds). Removes gold, silver, plasteel and steel from deep
  results (MO's own ores appear instead). Soft incompatibility with VTE Variations (scanner texture). Uses MO's textures,
  so the build menu shows doubles.
- Rimfolk today: nothing yet.
- Hook ideas: a "deep vein" find as a chronicle moment; miner epithet; a mining ambition.
- Confidence: high for content (Workshop page), creator confirmed as Phant0m5.

## Mine Sight (`rabiosus.minesight`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3769804600
- What it adds: highlights mineable ores while the Mine designator is active; Shift-click (configurable) designates a
  whole ore vein.
- Rimfolk today: nothing (UI only).
- Hook ideas: none needed.
- Confidence: high (Workshop page).

## Dismantle Ancient Junk (`proxyer.dismantleancientjunk`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2871064871
- What it adds: every Ideology ancient junk piece can be deconstructed, giving twice the resources of destroying it;
  patches for Biotech (ancient mech parts, not the exostrider midsection), Anomaly, Odyssey (life support units,
  ancient lights claimable and movable), VFE Ancients and Medieval Overhaul (ancient ruined battering rams, siege
  towers, doors). Incompatible with No Ancient Ruins, Worthless Junk and Buildable Ancient Ruins.
- Rimfolk today: nothing yet.
- Hook ideas: a salvage encounter ("found something in the old ruins"); thrifty-way credit for salvaging.
- Confidence: high (Workshop page).

---

### Cross-mod notes for Rimfolk design
- With Alpha Animals, Dinosauria, Megafauna, VAE and ReGrowth's creatures together, "majestic" wild animals and big
  predators are far more common than in vanilla. Rimfolk's beast systems (great-beast encounters, beast nemeses,
  predator-den dilemma, companion wish self-help) were tuned against vanilla wildlife; consider caps by body size or
  revenge chance for the riskier choices.
- Storm detection by defName ("thunder"/"storm", case-insensitive) catches Alpha Biomes' petal storm and forsaken
  thunderstorm, ReGrowth's fog storm and Seasonal Weather's dry duststorm.
- The advisor's condition table has Wildfire, Earthquake (Seasonal Weather), AB_AcidRainCondition and
  AB_VolcanicHeatWave (Alpha Biomes), with Almanac links.
- Flowers wish: wild Beauty plants (ReGrowth, More Vanilla Biomes) inside the home area quietly count; flower-garden
  zones (VPE More Plants), redcaps and succulents count too.

---

### Weather mods not in the modlist, safe to add to a running save
Surveyed October 2026 (Workshop pages, and the mods' files where there is no public repository). Rimfolk meets the
first six where it already meets the weather (`Integrations/WeatherMods.cs`, `Weather/WeatherDisasters.cs`); each
of these says it can be added to a running save, or adds nothing that would break one.
- SS Lightning Rod (Continued) (`Mlie.SSLightningRod`, 2358261479): `LightningRodBasic` (Smithing, no power) and
  `LightningRod` (Machining) draw strikes. Rimfolk: a strike drawn down by a rod gets a word from someone in view.
  Overlaps Stormproof's storm spire.
- Weather is kinda cozy (`Rocam.CozyUnderRoof`, 3680003162): trait `Pluviophile`, thoughts for sleeping through rain.
  Rimfolk: a pluviophile never comes to dread storms.
- Disasters: Tornado (Continued) (`zal.disasterstornado`, 3329542193): incident `Tornado` spawns the game's `Tornado`.
  Rimfolk: a tornado on a home map goes in the chronicle, and a journal line once it's gone.
- Stormproof (`AzraelGodKing.Stormproof`, 3760021127): conditions `Stormproof_IonStorm`, `_DryLightning`,
  `_HeatDome`, `_PolarFront`, `_ToxicSurge`, with industrial and spacer counters. Rimfolk: heat dome and polar front
  are talked about like a heat wave and a cold snap; all five send a camp on its way and are in the advisor's
  condition table. Best for an industrial colony (a medieval one gets the storms without the counters).
- Quiet Wonders (`choco.quietwonders`, 3822357701): cosmetic weather and season effects. Rimfolk's firefly nights
  leave the fireflies to it.
- Seasonal Weather is already in the modlist (above).
- Snow Covers All (`Mlie.SnowCoversAll`, 3380299912), Dynamic Weather Work (`darren.dynamicweatherwork`, 3764479976),
  WeatherControl (`Nightmare.WeatherControl`, 3047040031) and [CAP] Thunder Snow (`CAP.ThunderSnow`, 3590107592) are
  fine to add and need nothing from Rimfolk (Thunder Snow's weather is already a storm and snow to Rimfolk). With
  Seasonal Weather, WeatherControl's commonality edits only stick on biomes Seasonal Weather doesn't patch, and Thunder
  Snow appears almost only on cold bogs.
- Not recommended: Water Freezes (its author says not with Odyssey); Celsius (no Odyssey, overlaps Vanilla Temperature
  Expanded); Climate Cycle mods (new game only, and the weather record would call every season unusual); Dynamic
  Flooding and Sandstorms (1.5 or older).
