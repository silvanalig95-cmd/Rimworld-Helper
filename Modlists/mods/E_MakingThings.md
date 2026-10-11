# Batch E: making things (food, drink, furniture, clothes, books, medicine, storage, floors)

This batch is everything the colony makes, keeps and lives among: the Vanilla Cuisine family (Vanilla Cooking Expanded
with Stews and Sushi, Vanilla Fishing Expanded, Vanilla Brewing Expanded with Coffees and Teas), DetVisor's Stoneborn
Delights and Epochs (incense, pottery), the whole Vanilla Furniture Expanded line (base, Production, Power, Security,
Art, Props and Decor, Medical, Architect), clothing and armor (VE Apparel, Accessories, Armour, Layered Apparel), books
(VE Books, [sbz] Bookcase), doctoring (Field Medic, Quality Surgeon Reborn, Deafness and Blindness, FEB's medicine
retexture), Dubs Bad Hygiene, Dark Ages: Crypts and Tombs, Deep Storage and Deep Storage Plus, floors (More Floors,
Floors Are (Absolutely) Worthless), building helpers (QualityBuilder, MinifyEverything, Tucked Furniture, Medieval
Overhaul Extra Signs, No Burn Metal), food helpers (Food Alert, Food Poisoning Stack Fix), looks (Vanilla Textures
Expanded and Variations, Animal Variety Coats) and one framework (Processor Framework). Two ids the batch list called
"framework modules" are content mods: `vanillaexpanded.vfecore` is **Vanilla Furniture Expanded** (the base furniture
mod) and `vanillaexpanded.vcef` is **Vanilla Fishing Expanded** (prefix VCEF_). For Rimfolk this batch feeds the
"Stores", "Home", "Work" and "Gear" wishes, the room plans and floor picker, and the brewery/kitchen/storeroom/crypt
templates. Nothing in `Source/` names any of these package ids; every current interaction comes from Rimfolk's generic
matching: `Avail.IsFermenter` (vanilla barrel, or any building whose thingClass contains "ItemProcessor" or a comp
whose class contains "Processor", with "barrel/ferment/brew/keg/cask/still" in defName or label), the role predicates
in `RoomTemplateDef.RolePredicate` (bed, endtable, dresser, heater, storage, grave, fermenter, medical...),
`WishParams.nameContains` (bath/tub/sauna, bookcase/bookshelf, book, statue/sculpture, banner, bench, weapon rack,
fountain, smoker, beehive...), `WishWorker_Stock.HeldInBuildings` (items inside an `IThingHolder` building such as a
bookcase count for stock wishes), `WishWorker_LittleSomething.SocialDrugs` (`DrugCategory.Social`), and
`WishWorker_BetterFood.MealStock` (meals by `FoodPreferability`). Research was done from the mods' own XML and C#
(shallow, read-only sparse clones of the public GitHub repos) and the Steam Workshop descriptions (Steam Web API
`GetPublishedFileDetails`), plus web search for ids and changelogs.

Medieval relevance in one line each: most of VCE's grill, cheese, stews and condiments, VBE's liquors, tea and coffee,
Stoneborn's dwarven cask, Epochs, VFE base (basic and artisan furniture), VFE Art, VFE Security's siege equipment,
VFE Production's cabinets and fueled smelter, VFE Medical's operating table, VFE Architect's floors and walls, the
neolithic/medieval pieces of VE Apparel/Armour/Accessories, VE Books' writer's table, DBH's primitive and Plumbing tier,
DA Crypts, Deep Storage and the helpers all work without electricity. Soups, deep frying, canning, the bar and
cocktails, VFE Power, most of VFE Medical and most VE Apparel need Industrial research or power.

---

## Vanilla Cooking Expanded (`vanillaexpanded.vcooke`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2134308519 , https://github.com/Vanilla-Expanded/VanillaCookingExpanded
- What it adds: new meal families by technique. Grill (`VCE_Grill`, 1x1, wood-fueled, research `VCE_Grilling`
  Medieval, needs Smithing): simple/fine/lavish/gourmet grills (`VCE_SimpleGrill`...`VCE_GourmetGrill`) that turn into
  "ruined" variants when kept cold (FAQ: below 10 C). Cheese press (`VCE_CheesePress`, 2x1, `VCE_CheeseMaking`
  Medieval, 750 research): milk or insect jelly into `VCE_Cheese` (preferability MealSimple, rots after 120 days), which
  gains quality and value the longer it stays in. Condiment prep table (`VCE_CondimentPrepTable`,
  `VCE_CondimentsResearch` Neolithic, 500): salt, mayo, agave nectar, chocolate syrup, insect jelly preserves,
  smokeleaf butter; condiments go into gourmet meals (`VCE_MealGourmet`, MealLavish) that give 12-hour effects and stack
  by ingredient list. Bakes (flour, x2 or x8 at stoves), desserts (fruit, DesperateOnly preferability, eaten for joy).
  Industrial (Electricity): electric pot soups (`VCE_ElectricPot`, `VCE_SoupCooking`), deep fryer (`VCE_DeepFrier`,
  chemfuel), canning machine. Plants: sugarcane, allspice, wheat. Processor buildings use VEF PipeSystem
  `CompProperties_AdvancedResourceProcessor` with `PipeSystem.ProcessDef`s (`VCE_MilkIntoCheese`, `VCE_*SoupRecipe`).
- In play: in a medieval run the grill, cheese press, condiments, bakes and desserts are the useful parts; soups,
  frying and canning are out of reach without power. Grilled meals lose their bonus in a freezer, so cook them to eat
  soon. Medieval Overhaul has its own cheese press and cheese (`DankPyon_Presser`); both exist side by side. Patches
  for VFE Medieval, VGP, RimCuisine.
- Rimfolk today: grill recipes produce meals, so `Avail.IsCookingStation` treats the grill as a cooking station
  (kitchen hygiene check). Bakes, grills, stews, sushi and cheese count in `WishWorker_BetterFood.MealStock` by their
  preferability (cheese counts as a simple meal). `CanCook` sees the grill's fine/lavish recipes. The cheese press is
  a processor but not a "fermenter" (name has none of the keywords).
- Hook ideas: a "feast" dilemma variant that asks for a gourmet meal (`VEF.Cooking.GourmetMeal` thingClass) or a
  legendary cheese; a gourmand wish for a specific family (grill, bake, dessert) using `thingClass`/defName prefixes
  `VCE_`; advisor note "grilled meals in the freezer will be ruined" by counting `VCE_Ruined*Grill`; colony character
  trait "cheesemakers" from cheese press count; almanac link from the Food tab.
- Confidence: high (repo XML, Steam description).

## Vanilla Cooking Expanded - Stews (`vanillaexpanded.vcookestews`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2134312965 , https://github.com/Vanilla-Expanded/VanillaCookingExpanded-Stews
- What it adds: research `VCE_StewCooking` (Medieval, 300). Uncooked simple/fine/lavish stews are prepared at the
  campfire or a stove, then cooked in the wood-fueled stew pot (`VCE_StewPot`, 1x1, AdvancedResourceProcessor): one
  uncooked stew becomes 10 cooked stews after 180,000 ticks (3 days). Process defs set `noPowerDestroysProgress`, and
  VEF treats an empty fuel tank the same way: if the pot runs out of wood the batch is ruined after a short warning
  (`rareTicksToDestroy` 10). Uncooked stew rots in 4 days. The description says stews remove the debuffs of insect and
  human meat.
- In play: excellent winter food for a medieval colony; keep wood stocked near the pot.
- Rimfolk today: cooked stews count as simple/fine/lavish meals for the better-food wish. The stew pot is not a
  workbench, so it is not a cooking station in Rimfolk's eyes.
- Hook ideas: advisor warning when a stew pot's fuel is low with a batch inside (`CompRefuelable` + processor comp);
  cannibal-friendly dilemma ("sneak it into the stew") gated on this mod; winter-tradition "stew night".
- Confidence: high (repo XML, VEF PipeSystem source).

## Vanilla Cooking Expanded - Sushi (`vanillaexpanded.vcookesushi`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2158539170 , https://github.com/Vanilla-Expanded/VanillaCookingExpanded-Sushi
- What it adds: sushi prep table (`VCE_SushiPrepTable`, 3x1, no research) making sushi meals from fish and rice
  (chirashizushi and norimaki simple, uramaki and nigiri fine, temaki and futomaki lavish, gunkanmaki and oshizushi
  gourmet), soybeans and wasabi crops, wasabi paste (condiment table), soy sauce fermenter (`VCE_SoyFermenter`, 2x2,
  `VCE_SoyFermentation` Medieval 500): 30 soybeans become 15 soy sauce in 600,000 ticks (10 days). Loads a
  `1.6Odyssey` folder when Odyssey is active (uses Odyssey's fish), else `1.6NotOdyssey`.
- In play: a good use for Odyssey fishing in a river or coastal base; needs rice.
- Rimfolk today: sushi meals count for the better-food wish. Quirk: `Avail.IsFermenter` matches the soy sauce
  fermenter ("ferment" in its name, PipeSystem processor comp), so it fulfils the brewery wish
  (`WishWorker_Brewery.IsFulfilled` = any fermenter).
- Hook ideas: exclude non-alcohol processors from `IsFermenter` by checking the process products' `IsDrug`/
  `DrugCategory.Social`; the sushi prep table fits the kitchen plan's 3x1 stove slot as a `cook` variant.
- Confidence: high.

## Vanilla Fishing Expanded (`vanillaexpanded.vcef`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1914064942
- What it adds: originally fishing zones and fish by biome in three sizes. With Odyssey (active in this list) it adapts:
  its fish are added to Odyssey's fishing system, it adds mod options to tune Odyssey's fishing, new stats (and VE
  Skills expertises), and acts as the framework for other VE fishing content. Fishing zones need Odyssey's research.
  Defs use the `VCEF_` prefix (`VCEF_Fishing` work type, `VCEF_RawFishCategory`).
- In play: the "framework" label in the batch list is wrong; this is a content mod. Medieval Overhaul loads its
  smoked/dried fish patches only when Odyssey is off, Vanilla Outposts adds a fishing outpost with it.
- Rimfolk today: nothing yet (no fishing wishes). Existing almanac pages on fishing apply.
- Hook ideas: a "go fishing" wish for colonists with the Animals passion near water; fish in `MealStock`-style
  supply checks; a fisher's dream pool.
- Confidence: medium-high (Steam description; MO and VOE load folders confirm the id).

## Vanilla Brewing Expanded (`vanillaexpanded.vbrewe`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2186560858 , https://github.com/Vanilla-Expanded/VanillaBrewingExpanded
- What it adds: `VBE_LiquorBrewing` (Medieval): mashes at the vanilla brewery, aged in their own processors
  (PipeSystem): whiskey (25 corn per 5 mash; `VBE_WhiskeyCask` 2x2; 12 days; "whiskey high" +15% global work speed;
  re-aged another 12 days into double-aged whiskey, +20% work speed and +20% trade pricing), vodka (potatoes;
  `VBE_VodkaDrum` 1x1; 5 days; melee accuracy buff), gin (berries; `VBE_GinStill` 1x1; 15 days; social impact),
  tequila (agave plus insect meat; `VBE_TequilaBarrel` 1x1; 10 days; "excel at extended combat"). Ambrandy
  (`VBE_AmbrandyDistillery` 2x2, from ambrosia; archotech-tier, quality, beauty, masterwork/legendary gives an
  artistic inspiration; best quality takes 120 days). Tea and coffee (`VBE_MakeTea`, `VBE_MakeCoffee` at campfire or
  stoves after vanilla Brewing; tea boosts immunity; caffeine: +15% learning, -15% rest fall, addiction; hot coffee
  helps against hypothermia, iced coffee (put coffee in a fridge) against heatstroke). Tobacco: cigarettes (crafting
  spot or drug lab), cigars (drug lab). Plants: coffee, tea grass, tobacco. Mixology (Industrial): bar
  (`VBE_Bar`), cocktails (gourmet meals with drug effects), soda fountain, energy drinks, juice. DBH patch makes
  drinks count as water for thirst.
- In play: liquors are strong work and combat buffs with addiction risk; set drug policies. Tea and coffee are
  medieval-friendly. Wine, mead and cider come from MO/VFE Medieval/RimBees, not this mod.
- Rimfolk today: `IsFermenter` matches the whiskey cask ("cask"), tequila barrel ("barrel"), gin still ("still") and
  ambrandy distillery ("di*still*ery"), not the vodka drum or soda fountain. The brewery room plan's fermenter slots
  are 1x1, so only the tequila barrel and gin still can replace the vanilla barrel there; the 2x2 whiskey cask still
  satisfies the brewery wish. All VBE liquors, tea, coffee, cigarettes and cigars are `DrugCategory.Social`, so they
  count for the "little something" stock (10) and the brewery wish's "5 drinks in stock" alternative. The soda fountain
  has "fountain" in its name, so it fulfils `Council_Wish_Fountain`.
- Hook ideas: decree "temperance" or a dilemma about a drunk worker using `VBE_*High` hediffs; office perk for a
  bartender (Industrial only); a "barrel" quirk; detect "vodka drum" (add "drum" for VBE, or check process outputs);
  ambrandy as a legendary heirloom/treasure; caffeine addiction as a "quit" wish source.
- Confidence: high.

## Vanilla Brewing Expanded - Coffees and Teas (`vanillaexpanded.vbrewecandt`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2275449762
- What it adds: 8 coffees and 9 teas with their own effects (lattes, pumpkin spice, kombucha, "cannibal coffee"...),
  lemonade (from Trahs' mod); more with VCE (mint) and VPE (lemons). Brewed at campfires and stoves.
- In play: small comfort drinks; the warm ones help in winter. Addiction to caffeine still applies.
- Rimfolk today: the new drinks are social drugs if they follow VBE's pattern (not verified per def), so they would
  count for the drink stock wishes.
- Hook ideas: a "tea ceremony" tradition; morning-coffee scene lines.
- Confidence: medium (Steam description only; no public repo found).

## Stoneborn - Delights (`det.sbdelights`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3106413957 , https://github.com/DetVisor/StonebornDelights
- What it adds: a Stoneborn research tab: `SD_DwarvenDrugPlants` "underground herbs" (Neolithic, 1000),
  `SD_DwarvenBrewing` "dwarven fermentation" (Neolithic, 1400), `SD_ProcessedDrugs` "complex concoctions" (Industrial,
  1200). The dwarven cask (`SD_ShroomAleBarrel`, 2x2, 120 wood + 30 steel, Processor Framework `CompProperties_Processor`,
  capacity 30) brews shroom ale from stoutcap (15 days), sweetwine from glimmerquartz chunks (30 days, uses quality),
  hive mead from insect jelly (18 days), and from industrial mushes forge liquor (25 days) and an elixir (15 days).
  Cave-type plants stoutcap, bladeroot, bull's horns. Glimmerquartz is a mineable resource rock scattered on maps
  (`SD_GlimmerquartzOre`, chunk `ChunkGlimmer`), cut into a hard stimulant drug. Other drugs: bullgrog (from
  browbulls), bladeroot cigar, miner's delight, rage elixir, redspice (cures the hangovers dwarven drinks cause),
  alkahest, beard wax, feywing joints (fey creatures can spawn from a cask when a drink is finished; they are fast).
  Requires VEF and Processor Framework.
- In play: author says almost everything is medieval-friendly except beard wax and alkahest. The cask needs steel,
  which in MO means smelting. Hangovers are a real downside; keep redspice.
- Rimfolk today: the dwarven cask is a fermenter (`CompProcessor` + "cask"/"Barrel"), so it fulfils the brewery wish.
  Shroom ale, sweetwine, forge liquor, hive mead, bullgrog, bladeroot cigar, beard wax and feywing joints are Social
  drugs and count for drink stock wishes.
- Hook ideas: fey spawning as a small encounter ("something glittering flew out of the cask"); a Stoneborn xenotype
  tradition; hangover as a decree cost.
- Confidence: high (repo XML).

## Epochs - Incense (`det.epochsincense`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3072579620 , https://github.com/DetVisor/EpochsIncense
- What it adds: incense burner (`MA_IncenseBurner`, not an edifice, can sit on other buildings) and incense tower
  (`MA_StandingIncenseBurner`), no research, stone or metal plus 5 wood. Each gives "pleasant smell" +2 mood within 4
  cells (VEF `ThoughtWorker_ThoughtFromNearbyThingDef`, `DistanceToGiveThought` 4) and is an Artistic/Natural
  meditation focus. Smoke effects in the room.
- In play: cheap mood for temples, bedrooms and the hospital; no fuel. (A search snippet mentioned six aromas; the 1.6
  repo has only these two buildings, so that is likely an older or different version.)
- Rimfolk today: nothing specific. Both have `CompProperties_MeditationFocus`, so they qualify for the `meditation`
  role predicate if a template slot's footprint matches.
- Hook ideas: shrine wish could accept incense; "a quiet place" wish for the stressed; tradition "burn incense for the
  dead" at funerals.
- Confidence: high (repo XML).

## Epochs - Pottery (`det.epochspottery`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3092351095 , https://github.com/DetVisor/EpochsPottery
- What it adds: clay (`DV_Clay`, 40 from one stone chunk at a crafting spot or stonecutter's table) and clay bricks
  (`DV_ClayBricks`, 10 from 20 clay at the stonecutter; Stony stuff), potter's wheel (`DV_ClayTurningTable`, needs
  Stonecutting) and electric clay-turning table, pots and big pots (decorative furniture with quality and an art tab),
  raw clay, cobb clay and carved clay walls.
- In play: tribal-friendly; big pots need a turning table.
- Rimfolk today: clay bricks are a Stony material, so the room planner offers them for walls and stony furniture when in
  stock (`Avail.AllowedStuffs`, `BestStuff` prefers the material you have most of).
- Hook ideas: potter passion space; pottery as a trade-goods ambition.
- Confidence: high.

## [SYR] Processor Framework (`syrchalis.processor.framework`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3210544395 (Continued, used for 1.6) ; original
  https://steamcommunity.com/sharedfiles/filedetails/?id=2633514537 , https://github.com/Syrchalis/ProcessorFramework
- What it adds: a library for buildings that turn ingredients into products over time without a worker
  (successor to Universal Fermenter). `ProcessorFramework.CompProperties_Processor` / `CompProcessor` (an
  `IThingHolder` comp holding `activeProcesses` of `ActiveProcess`), `ProcessDef` (ingredientFilter, processDays,
  capacityFactor, temperatureSafe -1..32, temperatureIdeal 7..32, ruinedPerDegreePerHour, sun/rain/snow/wind factors,
  usesQuality + qualityDays, bonusOutputs, destroyChance). Player side: process tab on the building, product shown on
  it, HD barrel, option to replace vanilla barrels. Patches VFE Medieval, Settlers, RimBees and others to use it.
- In play: Medieval Overhaul uses it for the fermenting barrel, tanning drum, cauldron, drying racks, smoker, cheese
  press, paper press, charcoal pile, smelter, kiln and more; Stoneborn Delights uses it for the dwarven cask.
- Rimfolk today: `Avail.IsFermenter` detects PF processors by comp class name plus keywords. Ingredients sit in the
  comp, not the building, so `HeldInBuildings` does not count them (good: no wort counted as stock).
- Hook ideas: an advisor check for ruined processes (`CompProcessor.AnyRuined`, temperature outside
  `temperatureSafe`), "the wine is turning" dilemma; legendary-target barrels (`ActiveProcess.TargetQuality`) as a
  patience tradition; count finished products waiting to be emptied (`AnyComplete`).
- Confidence: high (source).

## Vanilla Furniture Expanded (`vanillaexpanded.vfecore`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1718190143 , https://github.com/Vanilla-Expanded/VanillaFurnitureExpanded
- What it adds: VFE 2: 30+ furniture pieces across eras with modular research: `MF_BasicFurniture` (Neolithic),
  `MF_RoyalFurniture` "artisan furniture" (Medieval, 800, needs Complex Furniture), `MF_ModernFurniture` and
  `MF_WasteDisposal` (Industrial). Neolithic/medieval: firepit and dark firepit (`Stone_Campfire`, stone, wood fuel,
  heatPerSecond 21, fire overlay, meditation focus), chiseled and simple beds (single and double), sitting cushion,
  bench (`Seat_Bench`), table (1x1), counter, darts board and punching bag (Complex Furniture), weapon rack
  (`Shelf_WeaponRack`, storage shelf for weapons), animal sarcophagus (`VFE_AnimalSarcophagus`, Building_Sarcophagus),
  wardrobe. Artisan: royal poster bed (`Bed_Kingsize`, 2x2), royal end table, royal dresser, royal wardrobe, royal chair
  and armchair, royal tables 1x1 to 3x3, royal standing lamp (needs 30 W power). Industrial: computers, radios,
  arcade, piano, roulette, loungers, couches, bins, lamps, ergonomic furniture.
- In play: the artisan set is the medieval noble's furniture. The royal lamp needs power, so it is useless before
  electricity. Not a library: the batch list mislabels it.
- Rimfolk today (by function): royal end table (bed-head facility, 1x1, item surface) passes `Avail.IsEndTable`; royal
  dresser (2x1 facility, maxDistance 6) passes `Avail.IsDresser`; the poster bed passes `IsDoubleBed`; firepit passes
  `IsHeater` and has a fire overlay, so it fulfils `Council_Wish_Hearth`; `Seat_Bench` matches the garden-seat wish
  ("bench"); `Shelf_WeaponRack` matches the weapon-rack wish; darts and punching bag are new joy kinds for the
  recreation-variety wish. Quirk: the animal sarcophagus is a `Building_Grave`, so `Avail.IsGrave` counts it for the
  burial wish and the crypt plan's grave role (footprint permitting).
- Hook ideas: noble bedroom template using the artisan set; exclude animal graves from `IsGrave`
  (`building.bed_humanlike`-style check is not available; use `Building_Sarcophagus` + defName/label "animal" or
  `GraveUtility`/`HoldsAnimalCorpse` checks); "royal furniture" luxury wish for spoiled standards.
- Confidence: high (repo XML).

## Vanilla Furniture Expanded - Production (`vanillaexpanded.vfeproduction`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1880253632 , https://github.com/Vanilla-Expanded/VanillaFurnitureExpanded-Production
- What it adds: linkable cabinets, each +10% `WorkTableWorkSpeedFactor` for one bench type and stacking with tool
  cabinets, no power: kitchen sink cabinet, tailor cabinet, drug cabinet (Complex Furniture), smithing cabinet
  (Smithing), machining and fabrication cabinets. Fueled smelter (`VFE_FueledSmelter`, wood, Smithing: smelt slag and
  weapons). Electric butcher, drug lab and stonecutter; assembly bench (components); `VFE_Manufacturing` (Industrial)
  large machining table, large electric smithy, large stove, large tailoring table (1.5x speed per description,
  inheriting other mods' recipes when loaded late).
- In play: cabinets are cheap medieval speed; the fueled smelter recycles slag before electricity.
- Rimfolk today: no specific use. Large tables could appear as `craft`/`cook` role variants only if the footprint fits a
  template slot (most do not).
- Hook ideas: workshop room plan with cabinet slots (`CompProperties_Facility` linked to the slot's bench); "a proper
  workshop" wish asking for a cabinet next to the colonist's favorite bench.
- Confidence: high.

## Vanilla Furniture Expanded - Power (`vanillaexpanded.vfepower`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2062943477 , https://github.com/Vanilla-Expanded/VanillaFurnitureExpanded-Power
- What it adds: power plants from portable and large industrial generators to spacer green energy and a nuclear
  reactor (radiation uses toxic buildup), new batteries, lightning rod and utilities, helixien gas (a combustible gas
  with pipes, tanks and gas-powered buildings immune to solar flares).
- In play: nothing for a pre-electric colony. Any power plant built sets Rimfolk's electricity flag.
- Rimfolk today: `Avail.HasElectricity` turns on when the colony runs any `CompPowerPlant`; these generators count.
- Hook ideas: none for the medieval run; advisor could read gas tanks for the power section.
- Confidence: medium-high (Steam description; repo cloned but not studied in depth).

## Vanilla Furniture Expanded - Security (`vanillaexpanded.vfesecurity`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1845154007 , https://github.com/Vanilla-Expanded/VanillaFurnitureExpanded-Security
- What it adds: `VFES_SiegeEquipment` (Medieval, 900, needs Smithing): catapult (`VFES_Turret_Catapult`, manned,
  wood or stone, loads stone chunks, fires boulders without line of sight, min range about 15), ballista
  (`VFES_Turret_Ballista`, manned, reloads with wood "bolts" that splinter on impact), cavalry spikes
  (`VFES_CavalrySpikes`, `Building_TrapDamager`). Bear trap (`VFES_BearTrap`, Complex Furniture, `Building_TrapBear` :
  `Building_TrapDamager`, re-armable, cannot sit next to other traps). Industrial/spacer: trenches, dugouts, razor wire,
  military/flamer/shotgun turrets, artillery, searchlight, HMG/minigun complexes, charge turrets, railgun, tesla.
- In play: the medieval colony's real defenses. Manned engines need a colonist each.
- Rimfolk today: bear traps and cavalry spikes pass `Avail.IsTrap` (Building_Trap subclass): counted by the advisor's
  defense score (+25 each up to 20) and by the defense wish's trap count. Catapult and ballista are mannable
  `Building_TurretGun`s: not `IsAutoTurret`, counted as `mannable` (+40 each) in `AdvisorCore.Assess`.
- Hook ideas: "man the walls" decree (drafted crews near manned turrets); a siege dilemma; artillery-captain office.
- Confidence: high.

## Vanilla Furniture Expanded - Art (`vanillaexpanded.vfeart`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1968134023 , https://github.com/Vanilla-Expanded/VanillaFurnitureExpanded-Art
- What it adds: research `VFE_Res_Pottery`, `VFE_Res_Obelisk`, `VFE_Res_WallCarvings` (Neolithic);
  `VFE_Res_ArtfulDisplay`, `VFE_Res_Statues`, `VFE_Res_Monuments`, `VFE_Res_Rugs` (Medieval); wallpapers (Industrial),
  holograms (Spacer). Pottery (crafting spot, sellable art), small/large/grand statues, carving, relief (3x1), pillar,
  obelisk, monument (3x3), plinth (`Building_Storage`, displays an item such as ambrandy), decorative plant pot,
  rugs (tailoring bench; small, large 3x3, grand 3x5), wallpaper, holograms (power), archotech sculptures (quest
  reward).
- In play: gives artists real work in every era; rugs dress up floors without changing them.
- Rimfolk today: statues match `Council_Wish_MoreArt` ("statue"); every sculpture-based piece is art, so it fulfils
  `Council_Wish_ArtInRoom`; the art-studio room plan uses the vanilla sculpting bench.
- Hook ideas: "a monument for the fallen" memorial wish (monument/obelisk); artist ambition for a grand statue;
  plinth as a trophy display for nemesis kills.
- Confidence: high.

## Vanilla Furniture Expanded - Props and Decor (`vanillaexpanded.vfepropsanddecor`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2102143149 , https://github.com/Vanilla-Expanded/VanillaFurnitureExpanded-Props
- What it adds: about 350+ purely decorative props in a separate Props Palette (Build Props button in the Furniture
  tab), placed via `Designator_Build` with WorkToBuild 0 and paid in silver (`VFEProps.Building_SubstractsSilver`,
  cost multiplier in settings; free in god mode). `VFEProps.PropDef` -> ThingDefs `VFEPD_*` (no designationCategory).
  Props do nothing (a sink prop is not a DBH sink, a shelf prop is not storage); they block movement or burn where
  sensible. Many mod-specific sets (Royalty, Odyssey, Anomaly, Alpha Memes, VE Memes, VBE...).
- In play: decoration and storytelling; containers can be abused to wall in raiders (the authors will not fix it).
- Rimfolk today: props are not `BuildableByPlayer`, so Rimfolk never suggests one, but placed props are colonist
  buildings and `WishWorker_Have` counts by name. In this modlist props named "banner" (`VFEPD_Banner`), "statue" (VE
  Memes set), "bookshelf" (`VFEPD_SteelBookshelf`), "empty weapon rack", "cracked bathtub" (Odyssey set) and "relic
  altar" (Alpha Memes set) therefore fulfil the banner, more-art, bookcase, weapon-rack, bath and shrine wishes.
- Hook ideas: exclude `VFEProps.Building_SubstractsSilver` (or defName prefix `VFEPD_`) from name-based wish counts;
  or embrace it as "a make-believe" cheap grant with a smaller mood.
- Confidence: high for the mechanism (source), medium that props spawn with the player faction (not traced in code).

## Vanilla Furniture Expanded - Medical Module (`vanillaexpanded.vfemedical`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1718191613 , https://github.com/Vanilla-Expanded/VanillaFurnitureExpanded-Medical
- What it adds: operating table (`Bed_OperatingTable`, 1x2 medical bed, Complex Furniture, metal or stone, no power:
  surgery success x1.25, tend quality +5%, immunity gain x0.9, rest effectiveness 0.6), IV drip stand (Hospital Bed
  research, +10% immunity gain for an adjacent hospital bed or operating table), vitals centre, research counter,
  sterile walls with colored stripes, cryptosleep suspender.
- In play: the operating table is the one medieval piece: a surgery bed, not a recovery bed.
- Rimfolk today: the operating table is a single humanlike 1x2 bed, so the hospital room plan offers it as a variant
  for its bed slots (role `bed`); with `MedicalTendQualityOffset` > 0 it also passes the `medical` role.
- Hook ideas: hospital template slot for an operating table; advisor tip "operate on the table, recover in a bed".
- Confidence: high.

## Vanilla Furniture Expanded - Architect (`vanillaexpanded.vfearchitect`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2608762624 , https://github.com/Vanilla-Expanded/VanillaFurnitureExpanded-Architect
- What it adds: wood-colored floors (walnut, birch, mahogany; dropdown `VFEArch_WoodFloor`), wooden parquets, fine
  wooden floors (20 wood, beauty 3, Royalty), cobblestone/polished/slab floors in each stone, packed dirt, grass,
  lawn, asphalt, concrete foundation, stone bridges, log wall (`VFEArch_LogWall`, 5 wood), hedge wall, fine wall,
  picket/brick/low/chain-link fences, animal gate, single door, cell wall and door (Smithing), armored wall and door
  (Fabrication). Wood can be built in four colors from the same wood.
- In play: lots of medieval-looking floors and walls; cannot be removed from a save (null terrain).
- Rimfolk today: every player-buildable floor shows in the floor picker; dropdown members share a family row, and
  floors without a dropdown are grouped when their labels differ only in the first word ("granite cobblestone floor",
  "marble cobblestone floor"). The picker shows each floor's beauty and cleanliness.
- Hook ideas: a "proper floor" wish for rooms with bare soil using these; walls of the planner could offer the log wall
  as a cheap medieval variant.
- Confidence: high.

## Vanilla Apparel Expanded (`vanillaexpanded.vappe`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1814987817 , https://github.com/Vanilla-Expanded/VanillaApparelExpanded
- What it adds: about 40 apparel pieces. Neolithic (no research, crafting spot): pelt coat (warm shell), tribal poncho,
  tribal kilt. Medieval (Complex Clothing): tunic, blouse, apron (+10% global work speed, +10% general labor speed),
  boots (+0.1 move speed). Everything else is Industrial behind `VAE_CasualWear`, `VAE_FormalWear`,
  `VAE_WorkAttire`, `VAE_SterileAttire`, `VAE_MilitaryClothing` (jeans, suits, chef's uniform, scrubs, hardhat...).
  Items spawn on NPCs.
- In play: aprons and boots are cheap medieval upgrades for workers.
- Rimfolk today: the pelt coat helps meet the warm-cloak wish (`minColdInsulation` 25); all pieces count for the new
  clothes wish.
- Hook ideas: "work clothes" wish for crafters (apron); uniform decree.
- Confidence: high.

## Vanilla Apparel Expanded - Accessories (`vanillaexpanded.vaeaccessories`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2521176396 , https://github.com/Vanilla-Expanded/VanillaApparelExpanded-Accessories
- What it adds: belt-layer utility items. Neolithic: quiver (30 leather + 15 wood at a crafting spot or smithy;
  `VEF_RangedCooldownFactor` 0.5, which halves the wearer's ranged cooldown), backpack (much higher carry weight),
  battle banner ("fighting spirit and confidence"). Industrial: medic bag (Medicine Production, drug lab), tool belt,
  ammo pack, explosive belt, mini-turret pack, ranged shield belt; ultra: resurrector belt.
- In play: the quiver is a big archer buff; it takes the belt slot.
- Rimfolk today: nothing specific (the battle banner is apparel, not a building, so it does not count for the banner
  wish).
- Hook ideas: archer pride wish ("a quiver of my own"); standard-bearer office.
- Confidence: high.

## Vanilla Armour Expanded (`vanillaexpanded.varme`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1814988282 , https://github.com/Vanilla-Expanded/VanillaArmourExpanded
- What it adds: Neolithic: wooden armor (crafting spot, fixed sharp armor 0.55), stone war mask. Medieval: gambeson and
  quilted vest (`VAE_LeatherTanning`, leather or fabric), and with Plate Armor research at a smithy: chainmail (on-skin),
  chestplate, light plate armor, plate shoulderpads, plate helmet, plate boots, plate gloves (metallic). Industrial and
  spacer: riot gear, bulletproof vests, ghillie suits, marine variants.
- In play: lets a medieval soldier layer chainmail, gambeson and plate.
- Rimfolk today: `WishWorker_Armor` wants worn sharp armor >= 0.35 and any makeable `ApparelArmor`/`ArmorHeadgear`
  recipe; wooden armor makes armor "makeable" from day one, and steel or iron plate easily passes the bar. Plate
  helmets are Overhead, so they also satisfy the hat wish.
- Hook ideas: "knighting" ceremony wish (full plate set); armor upkeep advisor (tattered armor).
- Confidence: high.

## Vanilla Textures Expanded (`vanillaexpanded.vtexe`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2016436324 , https://github.com/Vanilla-Expanded/VanillaTexturesExpanded
- What it adds: 450+ vanilla textures redrawn at 2x; mod option for the bottom menu bar (icons, text or both). No
  gameplay change. Other texture mods should load after it.
- Rimfolk today: nothing. The Council button's look follows the vanilla bar.
- Hook ideas: none.
- Confidence: high.

## Vanilla Textures Expanded - Variations (`vanillaexpanded.vtexvariations`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2493234474
- What it adds: 150+ random graphics for 35 vanilla buildings; buttons to randomize or choose a graphic; options in VE
  Framework's settings. Ideology styles are respected.
- Rimfolk today: nothing.
- Hook ideas: none.
- Confidence: high (Steam description).

## Vanilla Books Expanded (`vanillaexpanded.vbookse`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2193152410 , https://github.com/Vanilla-Expanded/VanillaBooksExpanded
- What it adds: `VBE_Writing` (Medieval) writer's table (`VBE_WritersTable`) and `VBE_Printing` (Industrial) typewriter
  table. Write a skill book for any of 12 skills from 100 cloth; the product is the vanilla `TextBook`
  (`VanillaBooksExpanded.RecipeWorker_CreateSkillBook` sets the skill; writer's skill drives quality; very long work).
  Newspapers (printing; last 24 hours; quick recreation). Blueprints and maps from traders (a blueprint, read long
  enough, finishes a research project). No bookshelves (vanilla's are used).
- In play: turns cloth and an intellectual's time into textbooks to train others.
- Rimfolk today: textbooks match "book" for `Council_Wish_Books` (stock of 4 in storage or the home area, and
  books inside bookcases count via `HeldInBuildings`).
- Hook ideas: "write my memoir" life goal for elders (a book about their best skill); mentor-to-book link with the
  social mentorship; a library room plan.
- Confidence: high.

## Animal Variety Coats (`cucumpear.azrael.varietycoats`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1511926373
- What it adds: random coat colors and patterns for many vanilla animals (alpaca, boomalope, boomrat, caribou, cat,
  chinchilla, cougar, dromedary, grizzly, hare, husky, iguana, labrador, megascarab, megaspider, muffalo, pig, rat,
  rhino, spelopede, squirrel, thrumbo, timberwolf, warg, wild boar); 1.6 removed bears and capybaras for now. Only
  newly spawned animals change; leather is unchanged. Conflicts with mods that add variants or override these textures.
- In play: purely visual. Not apparel despite the "coats" in the batch label. Package id matched by author (cucumpear)
  and collaborator (Azrael).
- Rimfolk today: nothing.
- Hook ideas: encounter text could mention an animal's coat color if the graphic variant is exposed.
- Confidence: medium-high (Steam description; package id inferred from author names).

## Layered Apparel 2.0 (`costel.layeredapparel`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3632480044
- What it adds: a cosmetic wardrobe ("transmog"): build a look from any apparel without changing stats; separate
  everyday, drafted, vacuum (Odyssey) and exosuit looks; color picker, size/position/rotation per facing; link a
  cosmetic to a type of worn equipment or to automatic conditions (layer, coverage, armor rating); Cosmetic Manager with
  presets to apply a uniform to many colonists. Supports CE, Exosuit Framework, Dead Man's Switch. Needs Harmony.
- In play: a medieval "livery" for the whole house without stat loss.
- Rimfolk today: wear wishes read `pawn.apparel.WornApparel`, so cosmetics never fulfil "new clothes", "a hat" or
  "something fine".
- Hook ideas: a "colony colors" tradition or decree that the player sets via a preset; vanity wish satisfied by a
  cosmetic (would need a soft dependency).
- Confidence: medium-high (Steam description; author Cosmosteller, id prefix "costel" inferred).

## Korg Xenotype (probably) (`sovereign.kork`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3793104547 (candidate)
- What it adds (if this is the mod): a combat xenotype by Sovereign (art and text by Elseud), orc-like, very short
  lifespan and fast reproduction, spawning with the Pirate faction; needs Expanded Biotech Style Genes (ebsg.framework,
  present in this list). Some features (a death-spawn mechanic) unfinished.
- Identification: no page shows the package id. Sovereign's workshop has no mod named "Kork"; "Korg Xenotype" is the
  only close name, and the list's alphabetical placement (between "Interaction Bubbles" and "Layered Apparel") and the
  EBSG dependency fit. It is not apparel. Kemetic Apparel (also Sovereign) is 1.4-only and unlikely.
- Rimfolk today: nothing. Not given an Almanac article.
- Hook ideas: none until identified; a stranger encounter for a short-lived warrior race could use lifespan data.
- Confidence: low.

## FEB's Medicine Retexture (`feb.medicineretexture`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3735458216
- What it adds: new textures for herbal, industrial and glitterworld medicine, plus Basic Medicine, Tribal Medicine,
  Medieval Medicines and Ancient Relics items (one consistent style). Load order decides between it and Medieval
  Fantasy Quest Rewards' "exalted" medicine texture.
- Rimfolk today: nothing.
- Hook ideas: none.
- Confidence: high (Steam description; note `V1024.MedTextureRestore` is a different mod).

## Field Medic (`flyfire2002.fieldmedic`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1541287769 , https://github.com/flyfire2002/RimworldFieldMedic
- What it adds: a "Medic Bag" (30 cloth, 5 medicine, 2 components, tailoring bench after Medicine Production; crafter
  needs Crafting 5 and Medical 4; traders may stock it, raiders never carry it). A wearer can stabilize bleeding parts
  of another pawn in the field (ported from Combat Extended); each use costs 3-8% of the bag's hit points; the patch
  wears off, so proper treatment is still needed. In 1.6 the patient no longer has to be downed and may be in bed.
  Incompatible with Combat Extended (not in this list).
- In play: a bag usually has to be bought in a medieval run (components and an Industrial research). Not the same item
  as VE Accessories' medic bag.
- Rimfolk today: nothing.
- Hook ideas: "field medic" epithet or office perk; a battle-dream about stopping a friend's bleeding.
- Confidence: high.

## Quality Surgeon Reborn (`gideon.reborn.qualitysurgeon`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3226013991 (reupload of Hatti's QualitySurgeon,
  https://steamcommunity.com/sharedfiles/filedetails/?id=857350593)
- What it adds: only the doctor with the best surgery success chance performs operations. Known issue: if that doctor is
  the patient or cannot manipulate, you must order another doctor manually. Mech surgeons (Biotech) can be mishandled.
- Rimfolk today: nothing. Note `Council_Wish_LetMeWork` (passionate colonists wanting their work) is unaffected.
- Hook ideas: a jealousy beat when the second-best doctor is always passed over.
- Confidence: high (Steam description).

## Deafness and Blindness (Continued) (`mlie.deafnessblindness`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2019740307 , https://github.com/emipa606/DeafnessBlindness
- What it adds: `Blindness` and `Deafness` hediffs (disease-like, 8 stages from negligible to profound, part
  efficiency -5% to almost -100%, severity grows slowly) given by `HediffGiver_Birthday` to both eyes or both ears of
  older pawns (chance rises with age fraction of life expectancy; deafness about twice as likely). The patch also
  removes two entries from vanilla's `OrganicStandard` giver set (positional `li[9]` twice; most likely vanilla's
  cataract and hearing loss, not verified).
- In play: old colonists slowly lose sight or hearing; prosthetics or bionics fix it, so a medieval colony mostly
  lives with it.
- Rimfolk today: the prosthetic wish looks at missing parts, not efficiency loss, so it does not react.
- Hook ideas: elder life events ("my eyes are going"), a journal pool for failing senses, a "read to me" social beat.
- Confidence: medium-high (repo XML).

## Dubs Bad Hygiene (`dubwise.dubsbadhygiene`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=836308268 , https://github.com/Dubwise56/Dubs-Bad-Hygiene
- What it adds: needs `Hygiene`, `Bladder` and optional `DBHThirst`; sewage, water and heating networks. No research:
  latrine (`PitLatrine`, emptied by hand or plumbed), water tub (`WashBucket`, label "water tub": washing, drinking with
  thirst on, refilled by hand or rain), primitive well (water hauled to the tub), burn pit (burns fecal sludge, also
  cremates; standing near it can make colonists sick), water trough and bowl, litter box, bath mat. Medieval research:
  Plumbing (pipes, basin, bathtub, water well, wind pump, water tower, water butt, sewage outlet), Septic Tanks,
  Heating (log boiler, hot water tank), Saunas (fueled sauna heater heats a room to 60 C, sauna seating), Swimming
  Pools, Sewage Sludge Composting. Industrial: toilets, showers, kitchen sink, fountain, hot tub, washing machine,
  irrigation, fire sprinklers, central heating, radiators, air conditioning, walk-in freezer. Odyssey: vacuum toilets,
  dump ports, water recovery, ice extraction. Room roles `PublicBathroom`, `PrivateBathroom`, `SaunaRoom`. Lite mode in
  settings (strips defs after restart). Hard to remove from a save.
- In play: a constant chore system; DBH for Medieval (batch A, `eldersign.dbhformedieval`) adds powerless pumps,
  toilets, baths and water purification. Deep Storage Plus adds a water barrel, Tucked Furniture and VFE Production
  patch for it, VBE drinks quench thirst.
- Rimfolk today (checked): there is **no bathroom room plan** (no template, no hygiene role in `RolePredicate`), and
  nothing reads DBH needs. `Council_Wish_Bath` (`WishWorker_Have`, nameContains bath/tub/sauna, notContains bathroom)
  matches the bathtub, hot tub, fueled sauna heater and sauna seating, but also the primitive **water tub** and the
  **bath mat**; `BestBuildable` picks the cheapest buildable match by WorkToBuild, so with DBH the bath wish usually
  suggests (and is met by) a water tub (WorkToBuild 250). `Council_Wish_Fountain` matches DBH's fountain (Industrial).
  `Avail.IsHeater` (heatPerSecond >= 10) counts the burn pit (21) and fueled sauna heater (34) as heaters: they count
  in heater statistics, and the 1x1 burn pit could be a fallback `heater` variant (the bedroom/hearth suggestions prefer
  Heater, Brazier, Campfire first; the hearth wish also requires a fire overlay, which the burn pit lacks). The
  `AdvisorWorker_Hygiene` is about cleaning, not DBH.
- Hook ideas: a bathroom template (latrine or toilet, basin, bathtub, stall doors) and a sauna template; tighten the
  bath wish (`notContains` "water tub", "bath mat", or a `defNames` preference for `BathtubStuff`, `HotTub`,
  `DBHSaunaHeaterLog`); exclude `DubsBadHygiene.Building_BurnPit` from `IsHeater`; advisor checks for low hygiene or a
  full latrine (`Need` defs `Hygiene`/`Bladder`); dilemmas about latrine duty (Rimfolk already jokes about latrines);
  "a hot bath" grant mood using `SaunaRoom`.
- Confidence: high (repo XML + Rimfolk source).

## Dark Ages: Crypts and Tombs (`van.dacrypts`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2963826335
- What it adds (from changelog and FAQ; most of the description is images): crypt furniture and decor: sepulchral
  brazier (patched to count for Royalty throne-room brazier requirements, fixed again April 2025), candles and
  candelabra (MO patch for tallow), ossuary walls, sepulchral statue, demon statue (2x2), a reliquary that holds a relic
  (right-click it or the relic) and glows, wall tomb (added November 2024; C# by Sovereign), wall torch (needs VEF),
  bloodflame and darklight variants (sanguophages get a mood buff from bloodflame with VEF). Author: Sir Van. Not part
  of Medieval Overhaul but meant to sit beside it.
- In play: makes a crypt or chapel feel lived-in; candles need tallow.
- Rimfolk today: "sepulchral statue" and "demon statue" match the more-art wish by name. The crypt room plan uses
  sarcophagi and graves; DA pieces are added by hand. Whether the wall tomb is a `Building_Grave` is not verified.
- Hook ideas: funeral tradition with candles in the crypt; relic in a reliquary as a colony treasure for the
  character page; eulogy location flavor.
- Confidence: medium (changelog, FAQ, search snippets; no repo).

## [sbz] Bookcase (`sbz.bookcase`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3789304104
- What it adds: four bookcases that work exactly like the vanilla bookcase but layer correctly: 2-tier bookcase (2x1)
  and narrow 2-tier (1x1), up to 12 items per cell; bookshelf (2x1) and small bookshelf (1x1, can sit on tables), up to
  6 per cell. Furniture tab, or Storage with Neat Storage. Author seobongzu.
- Rimfolk today: labels contain "bookcase"/"bookshelf", so they fulfil `Council_Wish_Bookcase`; books held inside are
  counted by `WishWorker_Stock.HeldInBuildings` for the books wish (vanilla bookcase behavior assumed).
- Hook ideas: library template; MO's schematic-in-bookcase rule as an advisor tip.
- Confidence: medium-high (Steam description).

## LWM's Deep Storage (`lwm.deepstorage`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3532608331 (1.6 version by SP4RTAN, same mod id);
  original https://steamcommunity.com/sharedfiles/filedetails/?id=1617282896 , https://github.com/lilwhitemouse/RimWorld-LWM.DeepStorage
- What it adds: storage buildings that hold several stacks per cell: double shelf and tall shelves (general, 2x1),
  meal tray rack, food basket (no research), meat hook (Smithing), hayloft, hampers (clothes), pallets, skip,
  medicine cabinet, clothing rack, weapons cabinet (Complex Furniture); safe and weapons locker (Machining). Most have
  fixed storage filters. Storing takes time; animals cannot haul to deep storage by default; other mods' storage moves
  to the Storage tab; all configurable in settings.
- In play: the medieval storeroom compactor. Hampers do not hold fabric.
- Rimfolk today: the storeroom plan's `storage` role takes `Building_Storage` without `fixedStorageSettings`, so the
  double shelf and tall shelves (2x1) are offered in place of the vanilla shelf; the fixed-filter units are not. Items
  on deep storage cells are in storage, so stock wishes (firewood, hay, honey, books, silver) count them.
- Hook ideas: "a tidy storeroom" wish; advisor "items outdoors" could suggest a skip or pallet.
- Confidence: high.

## Deep Storage Plus (Continued) (`mlie.deepstorageplus`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2589072911 , https://github.com/emipa606/DeepStoragePlus
- What it adds: barrel (120 wood + 25 metal, Complex Furniture; up to 8 stacks of one liquid: beer, wort, milk,
  chemfuel, VBE drinks...; a water barrel with DBH), firewood rack (60 wood + 150 stuff, Complex Furniture; 14 stacks of
  wood, 7 per tile, stops deterioration), bin (Machining; loose raw food, plants, textiles...), organ fridge (Machining +
  air conditioning). Patches for VFE Medieval, DBH, VBE.
- Rimfolk today: the firewood rack keeps the firewood wish's logs in storage; the barrel is storage, not a fermenter
  (no processor comp).
- Hook ideas: firewood advisor could suggest the rack in autumn.
- Confidence: high (Steam description).

## [T] MoreFloors 1.6 (`telkir.tmods.morefloors`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2878200208 (1.6 update by Rain); original
  https://steamcommunity.com/sharedfiles/filedetails/?id=1543561093 , https://github.com/telkir/T-MoreFloors
- What it adds: six wooden floor styles, eleven stone styles in five stones (stone floors need Stonecutting), eight
  chequered stone floors, six new cloth carpet colors and a chequered carpet (moved to the vanilla Floors tab under the
  1.6 carpet system), wool carpets from woven wool (loom workbench), plasteel and jade floors. Floors that could be made
  paintable are paintable.
- Rimfolk today: shown in the floor picker, grouped into families by dropdown or by label stem.
- Hook ideas: none beyond the picker.
- Confidence: medium-high (Steam description; repo is the 2017 original).

## Floors Are (Absolutely) Worthless (`hawkwood.floorsarealmostworthless`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2960126462
- What it adds: sets the market value of all constructed floors to 0 by patching the floor abstracts (works for most
  modded floors; not smoothed stone; NANAME floors incompatible). Floors still add beauty but no wealth, so floors no
  longer raise raids, and impressiveness from floors alone is harder. Safe to add or remove. Load after floor mods
  (VFE Architect). Renamed from "Almost" when the value went from 1 to 0; the id changed in March 2024.
- Rimfolk today: none directly; Rimfolk's raid-wealth advice and room wealth reading use vanilla stats, so they reflect
  the lower wealth automatically.
- Hook ideas: almanac note on wealth; advisor wealth tips could mention floors are free here.
- Confidence: high.

## Medieval Overhaul Extra Signs (`mewn.extra.signs`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2967087032 (most likely); standalone successor "Medieval
  Signs" https://steamcommunity.com/sharedfiles/filedetails/?id=3488857567
- What it adds: a few extra signboards in Medieval Overhaul's style (a quarry sign added in 2023) and a patch making all
  signs reinstallable, including MO's own. Needs MO, load after it. The standalone Medieval Signs has 14 signs (inn,
  flophouse, tavern, stables, armory, archery, library, magic shop, butcher, tailor, quarry, blacksmith, apothecary,
  shop) with three looks per sign chosen by rotation.
- Rimfolk today: nothing.
- Hook ideas: room plans could drop a matching sign by the door (purely decorative).
- Confidence: medium (id matched by name; both are by Mewn Goddess).

## Tucked Furniture (`keno.tuckedfurniture`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3808739466
- What it adds: gizmos to offset furniture toward walls and visually shrink it, lowering its path cost; chairs tucked
  under tables pull out automatically. Works on chairs, tables, beds, 3x1 workstations, thrones, braziers, standing
  lamps, flower pots, storage, columns. Settings for offset cap and how much offset clears path cost. Patches for DBH,
  Neat Storage, Sense of Depth; incompatible with Perspective: Buildings. Safe to add or remove. Author Avericat.
- Rimfolk today: nothing; room plans place furniture at normal positions.
- Hook ideas: none.
- Confidence: medium-high (Steam description; id prefix not confirmed).

## QualityBuilder (`hatti.qualitybuilder`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=754637870
- What it adds: designators (Architect, Orders) so only the best constructor builds things with quality; others haul.
  A minimum quality per blueprint: a result below it is deconstructed and rebuilt in place with the same stuff. Workshop
  tags list up to 1.5; it is in this 1.6 list, so assume the author's or a compatible build.
- Rimfolk today: nothing; room plans place ordinary blueprints, which the mod can then flag.
- Hook ideas: a "master builder" epithet when a legendary piece is built; a perfectionist quirk.
- Confidence: medium-high.

## MinifyEverything (`erdelf.minifyeverything`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=872762753 , https://github.com/erdelf/MinifyEverything
- What it adds: makes every building minifiable (uninstall and reinstall); experimental: sometimes reinstall fails and
  you have to uninstall and install again.
- Rimfolk today: nothing.
- Hook ideas: none.
- Confidence: high.

## No Burn Metal (`unon.noburnmetal`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1923990111 , https://github.com/Nonunon/NoBurnMetal
- What it adds: steel, silver, gold, power conduits and switches stop burning; supported mod metals include Medieval
  Overhaul's iron, VFE Classical bronze, Alpha Animals sky steel, Expanded Materials metals, Cupro's alloys. 1.6:
  per-item settings (unknown modded metals start off). Safe to add or remove.
- Rimfolk today: `AdvisorWorker_Fire` counts walls with flammability above 0.5, so metal walls were never flagged; no
  change needed.
- Hook ideas: none.
- Confidence: high.

## Food Poisoning Stack Fix (Continued) (`mlie.foodpoisoningstackfix`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2843483188 , https://github.com/emipa606/FoodPoisoningStackFix
- What it adds: tracks how many poisoned meals are in a stack instead of spreading the chance over the whole stack, so
  picking up a meal gives the true chance of getting a tainted one.
- Rimfolk today: food poisoning boosts `Council_Wish_BetterFood` (trigger "sick"), and the kitchen advisor watches dirt
  and butcher tables in kitchens; behavior unchanged.
- Hook ideas: a "who poisoned the stew" dilemma could name the cook of the tainted meal.
- Confidence: high.

## Food Alert (Continued) (`mlie.foodalert`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2017538067 , https://github.com/emipa606/FoodAlert
- What it adds: a persistent readout of food in storage and days it will last; settings for the lowest food grade
  counted (DesperateOnly to MealLavish), counting meal ingredients, update interval.
- Rimfolk today: none; Rimfolk's Priorities tab separately forecasts food against the coming winter.
- Hook ideas: none (avoid duplicating the readout).
- Confidence: high.

---

## The five most promising hooks from this batch
1. **Bathroom and sauna room plans + a cleaner bath wish** for Dubs Bad Hygiene: today the bath wish is met by a
   water tub or bath mat, and there is no bathroom template.
2. **Processor awareness** (VEF PipeSystem `CompAdvancedResourceProcessor` and PF `CompProcessor`): warn when a stew pot
   runs out of wood, a barrel is ruined by heat, or legendary wine is ready; and fix `IsFermenter` false positives (soy
   sauce fermenter) and misses (vodka drum) by checking what the process produces.
3. **Name-matching guard for props**: VFE Props' silver-bought props (banner, statue, bookshelf, weapon rack, cracked
   bathtub, relic altar) quietly fulfil wishes; exclude `VFEProps.Building_SubstractsSilver` or the `VFEPD_` prefix.
4. **Noble and library templates** using VFE's artisan set (poster bed, royal end table/dresser/wardrobe) and VE Books'
   writer's table with sbz bookcases; plus a memoir life goal (write a textbook of your best skill).
5. **Defense literacy**: credit VFE Security's catapults, ballistas, bear traps and cavalry spikes in a medieval
   "man the walls" decree or siege dilemma (they already count in the advisor's defense score).
