# Batch A: Medieval Overhaul and its circle

This batch is the backbone of the medieval modlist (`Modlists/medieval-main.txt`): **Medieval Overhaul** (MO) and the
mods built on it or around it. MO supplies the medieval tech tree, the production chains (timber, hides, cloth, metal),
food and drink, rustic furniture, noble-house factions, monsters and medieval sieges. **Rimedieval** keeps every other
mod's factions, pawns, research, quests and events at a medieval level. **Medieval Total Rim** adds Total-War-style
nations with their own ideologies, armor (bridging Tasty Armory to MO's stats), animals, quests and prosthetics.
**Divine Order**, **RimFantasy** and the **Medieval Fantasy** retheme mods add a fantasy layer; **Medieval Medicines**,
**DBH for Medieval** and **Medieval Talents** fill gaps in medicine, hygiene and traits; the rest are styles, walls,
wardrobe and a retexture. **Vehicle Framework** is a library (used here by MO: Air Vehicles).

Almanac articles for this batch: `Defs/AlmanacArticleDefs/Almanac_Mods_A_MedievalOverhaul.xml` (15 articles,
defNames `Council_AlmMod_A_*`, order 100-150).

Research notes: MO facts were read from the current source (ViralReaction/MedievalOverhaul master, commit of
2026-06-10, `1.6/`), which matches the Workshop item's last update (Jun 10). Rimedieval, Processor Framework, Giddy-Up 2,
the two Accurex add-ons, Dwarven Style and Vehicle Framework were read from their GitHub repos. Everything else comes
from the Workshop descriptions (fetched through the Steam `GetPublishedFileDetails` API, since the Workshop pages
themselves were not reachable). Package ids for Medieval Total Rim modules were matched to Workshop titles by name
(the Workshop text does not print package ids).

### Cross-cutting findings for Rimfolk (read these first)

1. **Era detection is fooled by MO's watermill.** `DankPyon_WaterMill` has a `CompProperties_Power` with
   `compClass MedievalOverhaul.CompPowerWaterWheel`, which derives from vanilla `CompPowerPlantWater`
   (`basePowerConsumption -1`, `transmitsPower false`); it only measures water flow to speed up milling.
   `Avail.RefreshCache` sets `hasElectricity` when any colony building has a `CompPowerPlant`, so building one watermill
   flips `Avail.IsPreIndustrial` to false in Auto era mode. Fix idea: ignore power plants whose props have
   `transmitsPower == false`, or whose comp class is not in the `RimWorld`/`Verse` namespaces, or require the
   `Electricity` research for the building heuristic when the faction tech is Medieval. The Almanac tells the player to
   set Era to Pre-industrial meanwhile.
2. **Firewood wish counts the wrong wood.** `Council_Wish_Firewood` wants `WoodLog`. With MO's timber chain (on by
   default) trees drop `DankPyon_RawWood` ("raw wood") and `WoodLog` is relabeled "wood plank"; campfires, hearths and
   stoves burn raw wood too. Add `DankPyon_RawWood` (and maybe the `DankPyon_RawWood` thing category) to the wish.
3. **Jewelry wish misses rings.** MO's jewelry is rings and necklaces (`amber ring`, `ruby necklace`, magic
   `ring of haste`...). `Council_Wish_Jewelry` matches necklace/bracelet/earring/amulet/pendant/circlet/brooch/tiara,
   so rings never count. Adding "ring" needs a guard (it matches "earring", "string", "ring mail").
4. **Hearth wish misses MO's hearth.** `Council_Wish_Hearth` needs `fire` (a `CompProperties_FireOverlay`).
   `DankPyon_RusticHearth` has no fire overlay comp (it is a `Building_WorkTable` with glower, refuelable and heat
   pusher), so it never counts; `DankPyon_WoodBurningStove` has `MedievalOverhaul.CompProperties_FireOverlaySouth`
   (a subclass of `CompProperties_FireOverlay`) and does.
5. **Bookcase wish over-matches.** `Council_Wish_Bookcase` matches "bookshelf", which catches MO's
   `DankPyon_RusticBookshelf`/`DankPyon_RusticBookshelf1x2c` ("small/large research shelf") and
   `DankPyon_RoyalBookshelf`, which are research-speed facilities, not `Building_Bookcase`. Conversely MO needs a real
   `Building_Bookcase` (vanilla bookcase) for schematics. Match `typeof(Building_Bookcase)` instead.
6. **Fermenters work.** Processor Framework (`syrchalis.processor.framework`) soft-removes the vanilla
   `FermentingBarrel` from the build menu and supplies `BarrelProcessor` (label "fermenting barrel",
   `ProcessorFramework.CompProperties_Processor`); MO adds its ale/wine/mead processes to it and sets its cost to
   10 `DankPyon_IronIngot`. `Avail.IsFermenter` recognizes it (comp class contains "Processor", name contains "barrel").
   MO's drinks (`DankPyon_Ale`, `DankPyon_Wine`, `DankPyon_Wine_Mulberry`, `DankPyon_Wine_Griffon`, `DankPyon_Mead`,
   `DankPyon_Cider`) are `DrugCategory.Social`, so `WishWorker_LittleSomething.SocialDrugs` counts them.
7. **Name-matched wishes that MO satisfies:** smokehouse (`DankPyon_Smoker`, `DankPyon_DryingRack_Small/Big`),
   beehive (`DankPyon_Apiary`), honey (`DankPyon_Honey` category items; note "hornet honey" also matches), banner
   (buildable `DankPyon_Banner*`), fountain (`DankPyon_Fountain1x1c/2x2c`), weapon rack (`DankPyon_RackMixed`,
   label "weapon rack"; the sword and spear racks do not match), more art (`DankPyon_ArmorStatue`, the grand
   sculptures), books (MO's `DankPyon_Schematic_*` items match "schematic"; MO books match "book"/"tome").

---

## Medieval Overhaul (`dankpyon.medieval.overhaul`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3219596926 (current upload by ViralReaction);
  source https://github.com/ViralReaction/MedievalOverhaul (older 1.5 repo: SirLalaPyon/MedievalOverhaul).
  Requires Harmony, Vanilla Expanded Framework, [SYR] Processor Framework.
- What it adds:
  - **Research:** its own "Medieval" tab (`DankPyon_MedievalResearchTab`) with ~50 projects: woodworking
    (`DankPyon_Lumber`), rustic/royal furniture, storage, presser, carrier birds, exploration, leather tanning, textile
    spinning, silk, candles, jewelry, engineering, mining, windmill, watermill, crossbow/arbalest, ballista, trebuchet,
    ballista repeater, alchemy, steel (`DankPyon_Steel`, 2000, needs Alchemy), mithril (`DankPyon_Plasteel`, 4000),
    tar, gunpowder, protective clothing, chain armor, bows, three tiers each of polearms/maces/blades, agriculture tiers,
    griffon berry, plowed soil, cooking tiers, smoker, grill, stews, baking, adorned armor.
  - **Schematics:** `MedievalOverhaul.RequiredSchematic` mod extension on research (16 `DankPyon_Schematic_*` scrolls,
    e.g. crossbow, war bow, military/noble weapons, tar, gunpowder, steel, intermediate/advanced cooking). A Harmony
    postfix on `ResearchProjectDef.CanBeResearchedAt` requires the scroll to be held in a `Building_Bookcase` in the same
    room as the bench. Schematics come from ruins/loot and caravans (`tradeTag DankPyon_Schematic`).
    `requiredResearchBuilding DankPyon_AdvancedResearchBench` (unlocked by `DankPyon_Engineering`) gates mining,
    windmill, watermill, ballista, trebuchet, repeater, mithril.
  - **Production chains** (settings `leatherChain`, `woodChain`, `clothChain`, `metalChain`, all on by default):
    trees drop `DankPyon_RawWood`; `WoodLog` becomes "wood plank" made at `DankPyon_Trestle`/`DankPyon_SawTable` or by the
    windmill/watermill; animals drop hides (`DankPyon_Hides` category, per-animal hide defs generated in code) dried on
    `DankPyon_RawhideRack` into rawhide or tanned on `DankPyon_TanningRack` (fuel `DankPyon_TanningLiquor`, boiled from
    10 wood at campfire/stove into 6); flax/cotton/wool spun into linen/cloth (`DankPyon_SpinningWheel`, loom process);
    ore (`DankPyon_IronOre`, silver, gold, raw mithril) smelted at `DankPyon_Furnace` into `DankPyon_IronIngot` etc.,
    iron refined to steel after Steel research; `DankPyon_Smelter` processor (needs Steel).
  - **Mining:** `DankPyon_MiningSpot` (iron/silver ore, coal, salt), `DankPyon_DiggingSpot` (clay), `DankPyon_MineShaft`
    (stone chunks, coal, salt, tar, gunpowder). Mineables: coal, salt, iron, gold, citrine, amber, onyx, emerald,
    sapphire, ruby, tar pits. Vanilla steel/plasteel/component mineables removed unless `vanillaMine`.
  - **Food:** wheat (12 days) -> flour (millstone, windmill, watermill) -> bread at ovens; many meals with taste
    thoughts (`DankPyon_Ate*`); `DankPyon_Smoker` (smoked meat, 1 day, keeps 30 days, +2 taste) and drying racks (dried
    meat, 2.5 days, MeatRaw ingredient, keeps 60 days); cheese press (`DankPyon_Presser`: cheese, goat/sheep cheese,
    apple juice); apiary (`DankPyon_Apiary`, `CompProperties_ThingProducer`: honeycomb every 300000 ticks, 13-37 °C,
    rests at night); slop pots (`DankPyon_SlopPot` slop/MealAwful with ateEvent `DankPyon_AteSlop`,
    `DankPyon_SlopPot_Stew` stew/MealSimple, `DankPyon_SlopPot_Fondue` fondue/MealFine +3); ice (`DankPyon_IceBlock_Maker`
    freezes waterskins at -20..-1 °C in 2 days; `DankPyon_IceChest` passive cooling); well and water spot.
  - **Drink:** at the vanilla Brewery bench: 25 wheat -> 5 `DankPyon_WortAle`; 25 grapes/mulberries/griffon berries ->
    5 must; apple mince -> pressed juice -> "ferment apple juice" -> `DankPyon_JuiceAppleUnfermented`, which
    transforms into `DankPyon_Cider` over time. Processor Framework barrel: ale 6 days; wine/mead use quality aging
    (`qualityDays` 1/3/8/14/20/50/120 for awful..legendary; player sets a target quality). Safe -1..32 °C, ideal 7..32,
    ruins 2.5%/degree/hour outside. Wine/mead/cheese give `DankPyon_DrankWine`/`DankPyon_AteCheese` by quality via
    `VEF.Cooking.CompProperties_IngestedThoughtFromQuality`: -7/-3/+1/+3/+7/+10/+15, 1 day. Ale taste thought +4.
  - **Alchemy:** cauldron (`DankPyon_Cauldron`) brews happy powder, battle elixir, vigor pill, miracle, paregoric, luhix
    and potion of knowledge; alchemy bench; herb table; drugs and alchemy plants (mindwort, poppy, fleawort, fly agaric).
  - **Buildings:** rustic/royal furniture (log, wicker, straw, fur, rustic and royal tudor beds; tables, chairs,
    benches, dressers, end tables, chests, research shelves), torches/oil lamps/candles/lamp posts, rugs (patched,
    royal, animal-hide rugs made on the tanning rack), banners (house and plain), market stalls and tents, signs, vases,
    fountains, sculptures, ice sculptures, stairs, doors and gates, log/tudor/castle walls, palisades and embrasures,
    trenches, wooden cavalry spikes (`Building_TrapDamager`), mannable refuelable turrets (scorpio, ballista, ballista
    repeater) and the trebuchet (artillery), messenger table (`DankPyon_ScribeTable`, class
    `MedievalOverhaul.Building_ScribeTable : Building_CommsConsole`, needs a linked `DankPyon_BirdPost`), explorer's
    workbench (`DankPyon_QuestFinder`, `Building_QuestScanner`, reads `DankPyon_TornNote_Hideout` to reveal cultist
    hideouts), cleaning bench (clean loot such as gold skulls and chalices for display), mending bench, jewelry bench,
    book scribe table, advanced research bench, windmill (`DankPyon_WindMill`) and watermill (`DankPyon_WaterMill`).
  - **Factions:** noble houses `DankPyon_NobleHouseFierce` (House Amboise), `DankPyon_NobleHouseCivil2` (House Soren,
    merchants), `DankPyon_NobleHouseCivil` (House Oswin, ruin delvers), `DankPyon_NobleHouseSavage` (House Hesse,
    `naturalEnemy`); hidden permanent enemies `DankPyon_BrigandFaction` (ruins) and `DankPyon_ShadowSect` (Cultists,
    hideouts; description says do not deactivate); hidden `DankPyon_UlrikFaction` (knights of Ulrik); animal factions
    `DankPyon_Forest_Faction`, `DankPyon_Witch_Faction`, `DankPyon_Hornets`, `DankPyon_SnakeCave_Faction`. Player
    factions `DankPyon_PlayerKingdom`, `DankPyon_LoneWolf`, `DankPyon_MercenaryCompany` (scenarios Tavern Owner, Lone
    Wolf, Battle Brothers). All share culture `DankPyon_OldWorlder`. Noble houses and brigands use balloon drop pods
    (`DankPyon_ActiveBalloon`) and `MedievalOverhaul.FactionSiegeExtension medievalSiege` -> raid strategy
    `DankPyon_MedievalSiege` (lord job builds a line of `DankPyon_CavalrySpike` from raw wood and trebuchets tagged
    `ArtilleryMedieval_BaseDestroyer`; medieval-tech factions only). MO's Royalty patch adds its player factions to the
    Empire's `permanentEnemyToEveryoneExcept`.
  - **Creatures:** farm breeds (hampshire, gloucestershire, northern boar, ravelder, angus), daer, dire boar, direwolf,
    hyena, griffon, rox, lindwurms, large cobras and constrictors, ancient troll (`Comp_TrollRegen`), unholds, ghouls
    (nachzehrer), golems (iron, silver, gold, steel), webknecht, deathstinger, mimics, schrat, alp. Biome
    `DankPyon_DarkForest`.
  - **Incidents/quests:** `DankPyon_DaerPasses`, `DankPyon_LindwurmPasses`, `DankPyon_GriffonPasses`,
    `DankPyon_TrollPasses` (Misc, baseChance 0.7, minRefireDays 30, leave after a while), `DankPyon_GolemImpactor`
    (ThreatBig, min 500 points, dormant golems), `DankPyon_Mineshaftfestation` via
    `MedievalOverhaul.StorytellerCompProperties_MineshaftInfestation` (baseMtbDaysPerDrill 20). Golem rocks
    (`DankPyon_GolemRock_*_MapGen`, "unusual iron formation, you hear something moving within") release golems when
    mined. Quests: `DankPyon_OpportunitySite_SmallRuin`/`MediumRuin`/`Snake_BigRuin`, cultist hideouts in three tiers
    (`DankPyon_Cultist_1Tier`.. started from the explorer's workbench). The Alp Eclipse condition/quest is commented out
    in 1.6.
  - **Traits:** `DankPyon_Fighter`, `DankPyon_Veteran`.
  - **Settings** (`MedievalOverhaulSettings`): production chains, soil wear (`soilWear`, `autoPlow`,
    `soilWearChance` 0.5, `plowedFertility` 1.25), map generation (remove vanilla junk, exostrider remains, hornet
    nests; restore vanilla mineables), misc (no fuel for torches, boomalope tar, meal retexture, Biotech schematics,
    slop dispenser, component repair), settlement preference, experimental chemfuel/component replace.
  - **Compat folders that load in this modlist:** Royalty, Ideology, Biotech, Odyssey (dried fish, fishing rewards;
    MO's VFE Fishing folder is skipped when Odyssey is active), Processor Framework, VE Plants, VE Cooking, VE Memes,
    Dubs Bad Hygiene, VE Outposts (compat DLL), VFE Architect, Hospitality (slop pots become vending machines). Its
    Giddy-Up folder is gated on `owlchemist.giddyup`, but this list runs `memegoddess.giddyup` (Giddy-Up 2 Continued), so
    MO's mount tweaks (griffon/daer offsets, farm animals not mountable) probably do not load (unverified in game).
    Its library room (`DankPyon_Library`) is only added when Vanilla Books Expanded is absent; VBE is in this list.
- In play: everything is slower and deeper than vanilla. Pitfalls: forgetting the hide and wood chains (hides pile up,
  planks run out), schematics not in a bookcase in the research room, barrels outside 7-32 °C, hornet nests and golem
  rocks on new maps, trebuchet sieges, the Cultists faction disabled at world creation.
- Rimfolk today: `Avail.IsFermenter` comment and code name MO's barrel; `Avail` recognizes MO furniture by function
  (lights, beds, tables, chairs, heaters, storage, workstations); README "Playing with other mods" mentions MO barrels.
  See the cross-cutting findings above for the watermill era bug and the firewood, jewelry, hearth and bookcase wish
  mismatches.
- Hook ideas:
  - Era: fix the watermill false positive (finding 1).
  - Wishes: "a proper cellar" (barrels in a room held at 7-32 °C), "a legendary vintage" (keep a barrel aiming at
    legendary; Processor Framework `CompProcessor.activeProcesses`, `ActiveProcess.TargetQuality/CurrentQuality`),
    "a tannery" (`DankPyon_TanningRack` + liquor stock), "a mill" (windmill/watermill), "a library" (bookcases near
    research), "a guest slop pot" for Hospitality.
  - Dilemmas: open the legendary cask for the wedding now or keep aging it; a cultist torn note found on a body (hunt the
    hideout or burn it); House Hesse envoy demands tribute; slop for prisoners vs real meals; a trebuchet crew offers to
    defect.
  - Ambitions: "the vintners" (N wines at excellent+), "the old roads" (clear all ruin and hideout quests),
    "iron age" (finish Steel and Mithril), "a castle" (castle walls + royal furniture).
  - Advisor: hides rotting unprocessed (`DankPyon_Hides`), tanning rack out of liquor, schematic held but no bookcase in
    the research room, barrels too cold/hot, plowed soil worn out, golem formation near a mining designation, a
    trebuchet on the map (siege warning), ice chest as a freezer substitute in pre-industrial colonies.
  - Encounters/strangers: lines keyed to faction defs (noble houses, brigands, cultists); a griffon or troll passing
    by (`DankPyon_*Passes` incidents) as a dream omen; nemeses for trolls, lindwurms and direwolves.
  - Room plans: tannery, mill, smokehouse (smoker + drying racks), apothecary (alchemy bench + cauldron + herb table),
    tavern (tables, ale barrels, `DankPyon_Sign_Tavern`), scriptorium/library, messenger room (scribe table + bird post).
  - Almanac: link the advisor's "idle research bench" to the schematic rule; the MO articles already exist.
- Confidence: high (source code and defs of the current version), medium for in-game behavior of the Giddy-Up folder.

## Medieval Overhaul: Toggleable Stuff (`accurex.medievaloverhaul.stuff`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2898440531 ; https://github.com/EvilEyeGuy/MedievalOverhaul-ToggableStuff
- What it adds: switchable tweaks to MO, each in its own settings menu (restart needed): flames clipped inside the
  stove/grill/furnace/hearth (on), thermostat for wood burning stove and hearth (off), loading cart that works like a
  trade beacon (on; replaced by MO QoL's cart if present), deconstructible MO scrap/ruins (on), flickable fireplaces
  (off; adds `CompProperties_Flickable` to `DankPyon_WoodBurningStove` and others), disable research schematics (off;
  removes `MedievalOverhaul.RequiredSchematic` and `requiredAnalyzed`, adjusts costs), wooden and stony vents (needs
  BetterVents), rugs as door mats (needs Door Mat).
- In play: the loading cart is the practical one; the schematic toggle changes MO's research gating.
- Rimfolk today: nothing yet.
- Hook ideas: if `MOTS_RemoveSchematics` is active, skip any schematic advice; a thermostat stove changes heating
  advice (fuel use).
- Confidence: high (Workshop description and repo patches).

## Medieval Overhaul: Royalty (`accurex.medievalempireoverhaul`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2646321064 ; https://github.com/EvilEyeGuy/MedievalOverhaul-Royalty
  (formerly "Medieval Empire Overhaul"). Requires Royalty and MO.
- What it adds: medieval Empire units (peasants, mercenaries, light/heavy infantry, grenadiers, crusaders, praetorian
  guard), mounted crusader squad with Giddy-Up, fortress settlements, medieval imperial traders; permits: removes call
  glitter medicine, aerodrone strike and salvo, adds request herbal medicine (and Paresian medicine with Medieval
  Medicines); MO armor counts for noble apparel (full plate variants, armets, great helms, adorned pieces, zweihander
  gear); settings toggle each part and show a rank overview of throne room and bedroom requirements. Works with VFE
  Empire.
- In play: titles stay achievable without spacer apparel; the Empire is a medieval great power.
- Rimfolk today: nothing specific; Rimfolk's Royalty features (title-room wish, throne room plan, royalty Almanac pages)
  apply.
- Hook ideas: noble apparel wish could name MO plate; the throne room plan could pick MO's `DankPyon_RoyalThrone`,
  `DankPyon_RoyalColumn`, royal rugs.
- Confidence: high for features (Workshop text, repo), medium for the Paresian permit condition (it links Nuno's
  Medieval Medicines; whether the MO edition triggers it is unverified).

## Medieval Overhaul: Barbarians (`pphhyy.barbariansmosubmod`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3216459199 (tagged 1.4-1.6). A continued fork exists:
  https://steamcommunity.com/sharedfiles/filedetails/?id=3601347953 (Zaljerem, 1.6).
- What it adds: a barbarian faction and player way of life; loot enemy gear and convert it into barbarian gear with
  skulls (a full outfit up to 18 skulls); 7 armors, 2 under-armors, a tribal outfit, 14 weapons (spinning throwing axe),
  shields, banners, an upgrade system, the northern horse. Requires MO. Author recommends a tribal start.
- In play: a skull economy; skulls come from Ideology/vanilla 1.5+ skull extraction.
- Rimfolk today: nothing yet (its banners count for the banner wish by name).
- Hook ideas: a "trophy" quirk or epithet for skull collectors; dilemmas about desecrating enemy dead for skulls.
- Confidence: medium (Workshop text only; def names not checked).

## Medieval Overhaul: Air Vehicles (`gayuki.medievaloverhaulairvehicles`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3302419308 . Requires MO and Vehicle Framework.
- What it adds: hot air balloon (300 cloth, 125 wood, 25 steel, 5 basic components; pilot + 1 passenger + 150 kg;
  1.0 tiles/s, 1.5 coal per tile) and airship (2000 cloth, 750 wood, 250 steel, 50 basic components; pilot + 10
  passengers + 1000 kg; 3.0 tiles/s, 3.0 coal per tile). Research after Engineering; the airship needs a special book.
- In play: fast medieval travel; coal logistics.
- Rimfolk today: colonists aboard vehicles are meant to stay "ours" (README). Vehicle Framework's
  `AerialVehicleInFlight` exposes `AllPawnsAboard`, not `AllPawns`, so `People.OffMapColonists` reflection does not
  see passengers in flight; check that `PawnsFinder.AllMapsWorldAndTemporary_Alive` covers them.
- Hook ideas: "see the world from the sky" wish for the restless; a balloon flight as a travel-wish fulfilment.
- Confidence: medium-high (author's numbers from the Workshop text).

## RimFantasy - Medieval Overhaul Edition (`sierra.rf.medievaloverhaul`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2836791007 (MapleSierrap; "RF" = RimFantasy).
- What it adds: gem-based magic: magical weapons with powers, magical furniture for lighting and for temperature,
  magical defenses. Rebalanced to MO standards; supersedes the original RimFantasy series (incompatible together).
  Listed incompatibilities: VFE Empire's New Family scenario (not in this list); moderate with Simple Sidearms (in this
  list): breaks the no-bond mechanics of arcane weapons; workaround "Allow use of blocked weapons".
- In play: gems become strategic; magical lights/heaters.
- Rimfolk today: nothing specific; lights and heaters are recognized by function if they use glower/heat pusher comps
  (unverified for these defs).
- Hook ideas: arcane weapons as heirlooms (Rimfolk heirloom system), a "magic" flavor tag for dreams.
- Confidence: medium (Workshop text; defs not checked).

## RimFantasy - House Doyle (`sierra.rf.housedoyle`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2903293603
- What it adds: amethyst resource, amethyst jewelry, player-craftable heraldic weapons and armor of House Doyle (a fallen
  minor noble house of gem mages), a line of amethyst arcane weapons, more arcane weapons found only through exploration.
  Same Simple Sidearms caveat.
- Rimfolk today: jewelry wish matches necklaces etc. by name (not rings).
- Hook ideas: House Doyle lore for secrets/backstory flavor ("bears Doyle colors").
- Confidence: medium.

## Rimedieval (`ogam.rimedieval`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2516523040 ; https://github.com/OgamLab/Rimedieval
- What it adds (from 1.6 source): Harmony transpilers re-equip every pawn/faction to medieval or neolithic gear;
  research limited to Medieval (setting "Allow pre-industrial tech" = `restrictTechToPreIndustrialOnly` allows up to
  Industrial except `MicroelectronicsBasics` and dependents); Odyssey defs are always allowed; buildings too advanced
  lose their `designationCategory`; `disableMechanoids` (default true) destroys mechanoids and patches
  `FactionUtility.HostileTo`; removes incidents `DefoliatorShipPartCrash`, `PsychicEmanatorShipPartCrash`,
  `MechCluster`, `PsychicSoothe`, `PsychicDrone`, `ToxicFallout`, `VolcanicWinter`, `ShortCircuit`, `ShipChunkDrop`,
  `OrbitalTraderArrival`, `GiveQuest_EndGame_ShipEscape`, `ProblemCauser`, mechanite diseases, `WastepackInfestation`;
  removes quests (ship escape, mineral scanner lump, mech pod rewards, ancient complexes, spacedrone hacking, mechanitor
  and pollution quests) but re-allows the ancient complex/relic ones when
  `BotchJob.medievalfantasythemedrelicquests` is active; removes ancient junk/mech remains gen steps; comms console
  becomes a signal fire (research `RM_SignalFire`); plasteel and compacted machinery do not spawn as ore; drop pods and
  shuttles retextured; techprints removed from quests. The old "new city" end-game quest is not in the 1.6 build.
- In play: the reason the whole list stays medieval. Author: load near the bottom; do not add its factions when VEF
  asks; not compatible with Medieval - Vanilla.
- Rimfolk today: `People.MechanoidsAround` treats Rimedieval's friendly mechanoids as none; dream omens check the
  incident still exists (`Dreams.OmenCanHappen`); hidden buildings are never suggested (`BuildableByPlayer`); README
  documents all three.
- Hook ideas: if `restrictTechToPreIndustrialOnly` is on, expect Electricity to be researchable (era may legitimately
  flip); the signal fire as the "comms" building for advisor/almanac text; read `RimedievalMod.settings` via reflection
  to tailor text.
- Confidence: high (source).

## Medieval Total Rim: Core (`athelas.medievaltotalrim.core`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3806242927 (Athelas; remaster of Medieval Kingdoms).
- What it adds: shared code, memes, fixed ideologies, common backstories and world symbols for the nation packs; the
  Teutonic Order as a permanent rogue military order (needed so certain quests initialize); VEF patches that make
  Tasty Armory shields usable without CE. Requires all Tasty Armory modules (in this list: `al9000.tastycore`,
  `tastysteppe`, `shiningarmor`, `fireandsteel`, `highmedieval`, `sengoku`). CE values included. Recommends MO and
  Giddy-Up. Planned later: Japanese clans, Wallachia, England, Denmark.
- Rimfolk today: nothing yet.
- Hook ideas: faction-specific stranger lines and vendetta flavor (Teutonic Order as the "permanent enemy");
  ideology-aware dilemmas (a guest of another faith).
- Confidence: medium (Workshop text; defs not checked).

## Medieval Total Rim: Armory (`athelas.medievaltotalrim.armory`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3806247184
- What it adds: 4 engraved Tasty Armory armor variants, 7 hats, 23 helmets (Byzantine, Moorish, Turkish, Russian
  focus); patches every Tasty Armory module to MO-equivalent stats (bridge between the two series). Standalone.
- Rimfolk today: nothing (hats count for the hat wish via the Overhead layer).
- Hook ideas: none specific.
- Confidence: medium.

## Medieval Total Rim: Animals (`athelas.medievaltotalrim.animals`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3806262056
- What it adds: medieval dog, cat and horse breeds; exotic wild animals (caracal, Iberian lynx) in their biomes;
  domestic breeds (Anatolian Shepherd, Chartreux) and horse breeds (Andalusian, Thessalian) only from their nations'
  traders; Giddy-Up mount integration for nation rosters.
- Rimfolk today: pets/companion wishes work with any animal.
- Hook ideas: a "fine horse from the trader" wish keyed to trader stock.
- Confidence: medium.

## Medieval Total Rim: Quests and Incidents (`athelas.medievaltotalrim.events`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3806263602 (id matched by elimination).
- What it adds: five quests with real choices (approach reflected in the reward screen), letting the player take part in
  factions' wars, diplomacy and politics; two random incidents with mood boosts or trade opportunities that may spread
  disease.
- Rimfolk today: nothing.
- Hook ideas: let the council vote on the side to take in these quests (the closest thing to a ready-made council
  dilemma in the list); record the choice in the chronicle.
- Confidence: medium-low (no defs or quest names found).

## Medieval Total Rim: Pre-Industrial Prosthetics (`athelas.medievaltotalrim.hediff`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3806277474 (id matched by elimination).
- What it adds: three prosthetic sets with research, between the peg leg/wooden hand and industrial prosthetics.
  Overlaps with Medieval Medicines' four prosthetic tiers.
- Rimfolk today: `WishWorker_Prosthetic` finds any craftable `isTechHediff` install for the missing part, so these count.
- Hook ideas: none beyond the existing wish.
- Confidence: medium.

## Medieval Total Rim nation packs
Each pack: "new unit types, backstories, a unique culture, a custom ideology, scenarios to play, and a new fully Medieval
Overhaul compatible faction", inspired by Medieval II: Total War. Requires Core and the Tasty Armory modules. Rimfolk
today: nothing specific. Hook ideas for all: per-faction flavor lines for visitors and raiders, vendetta text using the
faction's name, scenario-aware life goals (a crusader backstory wanting a holy war). Confidence: medium (Workshop text).

### Medieval Total Rim: The Kingdom of France (`athelas.medievaltotalrim.france`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3806249061 . French culture and ideology; sells the
  Chartreux cat (Animals module).

### Medieval Total Rim: The Kingdom of Poland (`athelas.medievaltotalrim.poland`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3806251054 . Polish culture and ideology.

### Medieval Total Rim: The Mongolian Khaganate (`athelas.medievaltotalrim.mongols`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3806253198 . Mongolian culture and ideology.

### Medieval Total Rim: The Ottoman Empire (`athelas.medievaltotalrim.turks`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3806254466 . Turkish culture and ideology; sells the
  Anatolian Shepherd.

### Medieval Total Rim: Grand Principality of Moscow (`athelas.medievaltotalrim.russia`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3806258934 . Russian culture and ideology.

### Medieval Total Rim: The Eastern Roman Empire (`athelas.medievaltotalrim.byzantium`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3806255936 . Byzantine culture and ideology; the faction
  is also Royalty compatible; Thessalian horses come from Greek traders.

### Medieval Total Rim: The Holy Roman Empire (`athelas.medievaltotalrim.hre`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3806257340 . German culture and ideology.

### Medieval Total Rim: Reconquista (`athelas.medievaltotalrim.iberia`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3806259927 . Three factions (Spain, Portugal, the
  Moors), three Iberian cultures, two ideologies; Andalusian horses from Spanish/Portuguese traders; Iberian lynx wild.

## Divine Order (`botchjob.divineorder`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3017163907 (Botch_Job; last update 2025-07-17).
- What it adds: the Divine Order faction (zealots on a mission to cleanse corruption, distrustful but open to uneasy
  alliances); scenario "Divine Order defectors" (four defectors with a stolen sacred tablet); weapons and apparel by
  research (extra damage vs Anomaly entities with EBSG Framework, which is in this list); sacred tablet (study for the
  elite armor research, crafting component; from trade, crusaders/inquisitors/heralds, rare quest reward); altar,
  sculptures, ritual effect, styles; a warhorse (larger, stronger, faster; sold by the Order and merchant caravans).
  Optional links: VEF (shields), Possessed Weapons, Medieval Fantasy Quest Rewards (elite loadouts), Rare Resources,
  VIE Memes and Structures (Violent Conversion meme). Rimedieval may change their loadouts. A Steam comment reported
  VEF 1.6 shield-comp errors after a VEF update (may be fixed).
- Rimfolk today: altar matches the "place to pray" wish (any "altar"/"shrine" name).
- Hook ideas: a secret "deserter from the Order" for colonists from the defectors scenario; dilemma when the Order
  demands the stolen tablet back; vendetta flavor for inquisitors.
- Confidence: medium (Workshop text; defs not checked).

## Medieval Fantasy Themed Quest Rewards (`botchjob.medievalfantasyquestrewards`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2955864975
- What it adds: renames/retextures only: psychic insanity/shock lances -> staffs of madness/paralysis; triple and
  doomsday launchers -> fireball staffs; orbital bombardment/power beam targeters, tornado generator, low-shield,
  smokepop and firefoam packs -> spell scrolls; healer/resurrector serum -> healing/rebirth potion; luciferium -> demon
  blood potion; monosword/plasmasword/zeushammer -> runic longsword/magma blade/stormhammer; persona weapons ->
  possessed weapons; eltex gear -> arcane gear; skilltrainer -> skill tome; psytrainer -> spell tome; psylink
  neuroformer -> attunement crystal; techprof subpersona core -> scroll of knowledge; glitterworld medicine -> exalted
  medicine; animal/soothe pulsers -> frenzy/serene orb; shield belt -> shield talisman. Tech level set to medieval so
  Rimedieval keeps them (shield belt and low-shield pack still filtered as rewards).
- Rimfolk today: the Books wish matches "tome", so skill tomes and spell tomes count as books. Rimfolk's own text
  (Almanac, advisor) uses vanilla names (glitterworld medicine, luciferium).
- Hook ideas: read display labels (`ThingDef.label`) when naming items in advice so renames flow through.
- Confidence: high for the rename list (author's text).

## Medieval Fantasy Themed Rare Resources (`botchjob.medievalfantasyrareresources`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2942661554
- What it adds: renames plasteel -> mithril, uranium -> adamantite, synthread -> damask, hyperweave -> runeweave (and
  derived item names). MO also calls its plasteel research "Mithril".
- Rimfolk today: nothing; room plans show stuff labels from the game, so they read correctly.
- Hook ideas: none.
- Confidence: high.

## Medieval Fantasy Themed Relic Quests (`botchjob.medievalfantasythemedrelicquests`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3035624471
- What it adds: retheme of Ideology relic and ancient complex quests: terminals -> ancient texts/tablet, cryptosleep pod
  -> sarcophagus, hermetic/security crates -> treasure chest/lockbox, comms console -> beacon, fuel node -> explosive
  barrel, spacedrone -> stolen airship, enemy terminal -> suspicious pedestal; complexes -> "ancient outposts";
  hacking -> investigating; shuttles and drop pods -> airships and balloons (everywhere). Rimedieval re-enables the
  complex quests when this mod is active (verified in Rimedieval's `DefCleaner`).
- Rimfolk today: nothing.
- Hook ideas: relic quests as an ambition goal for devout colonies.
- Confidence: high.

## Medieval Medicines Medieval Overhaul Edition (`joedaly.medmeds.mo`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2883228057 (JoeOwnage; full content in the pinned
  discussion https://steamcommunity.com/workshop/filedetails/discussion/2883228057/581683203528478591/). Requires MO.
- What it adds: prosthetics in four tiers (crude, artificial, mechanical: max 80% efficiency; Paresian: emerald,
  alchemy bench, equal to natural; sensory Paresian parts restore sight/hearing/smell), stuffable recipes; medicines in
  three tiers (early medieval: herbal medicine + cloth/linen at crafting spot/workbench; late medieval: alcohol + early
  + cloth at alchemy bench, accepts MO drinks; Paresian: neutroamine (or Stoneborn glimmerquartz) + late + mithril +
  cloth), ranging from slightly better than herbal to slightly better than industrial; Aether Bloom plant; lesser/minor
  healing potions; lesser/minor mana potions (Royalty; psyfocus); Entosomnia (medieval cryptosleep). Renames healer and
  resurrector serums to greater/supreme healing potion with Quest Rewards. Compatibility: Mod Medicines Patch if pawns
  ignore the medicines; Research Reinvented conflicts.
- Rimfolk today: medicines count via `ThingRequestGroup.Medicine` for the infirmary wish; prosthetics count for
  `WishWorker_Prosthetic`.
- Hook ideas: the "physician" office could prefer colonists who can craft Paresian medicine; advisor tip to make
  medicine from surplus ale.
- Confidence: medium-high (author's text).

## DBH for Medieval (`eldersign.dbhformedieval`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2926675339 (ElderSign Senpai). Requires Dubs Bad
  Hygiene (full, not Lite).
- What it adds: medieval pumps, toilets, baths, water purification; pipes connect to primitive wells. With MO: log boiler
  and fueled sauna heater burn coal, wind pumps need MO's Windmill research, wind pump costs basic components, and
  steel/components in sub-medieval costs become MO iron ingots/basic components. With Processor Framework the biosolids
  composter uses it. Possible conflicts with mods changing water fetching/drinking job drivers. MO itself also patches
  DBH (water barrel, water well, log boiler, hot water tank, burn pit, septic tank textures/defs).
- Rimfolk today: the Bath wish matches bath/tub/sauna (not "bathroom").
- Hook ideas: hygiene-aware wishes (a private bath for the spoiled), advisor note on wells near latrines.
- Confidence: medium-high.

## Misc. Training Medieval Retexture (`serek.misctrainingmedievalretexture`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3271602770 (SereQ). Needs Misc. Training
  (`haplo.miscellaneous.training`, in this list) loaded before it.
- What it adds: medieval textures for training dummies and targets, changed allowed materials (existing dummies may
  look odd when added mid-save).
- Rimfolk today: the Training Dummy wish matches dummy/archery/practice target names.
- Hook ideas: none.
- Confidence: high.

## Medieval Walls (`kongkim.medievalwalls`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2883913520
- What it adds: medieval walls and embrasures (twice the cost of normal walls, a bit more HP; embrasures 75% cover),
  cellar ditches (slow enemies a lot, buildable on any floor).
- Rimfolk today: embrasures may count as cover in `Avail.IsCover` if they are pass-through 1x1 (not checked).
- Hook ideas: defense advice could suggest ditches before gates in pre-industrial colonies.
- Confidence: medium-high.

## RimTraits - Medieval Talents (`sierra.rt.medievaltalents`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1916352291 (MapleSierrap; v2.3, 1.6).
- What it adds: 35 mutually exclusive talent traits, guaranteed one per pawn, higher tiers rarer; talents also buff
  temperature, mood, mental break threshold and pain tolerance; balanced for 50+ colonist medieval colonies. Sister
  mods `sierra.rt.generaltraits` and `sierra.rt.vanillatraitcolors` are in this list (other batch).
- Rimfolk today: nothing.
- Hook ideas: map talents to skills so life goals ("master a craft") and lessons favor talented pupils; epithets for
  rare talents; dreams that reveal a hidden talent. Trait def names not checked.
- Confidence: medium.

## Stoneborn - Dwarven Style Pack (`det.dwarvenstyle`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3172496453 ; https://github.com/DetVisor/StonebornStyle
  Requires Ideology.
- What it adds: style category `DV_Dwarven`, culture `DV_Grottalik`, meme `DV_Structure_Dwarven`; styled furniture,
  altars, sculptures, swords, (fantasy-looking) guns, helmets, toys; `DV_Dwarven_HallPillar`, `DV_HallDoor`,
  `DV_HallDoorTimber`; dwarven floors (`DV_Floor_Dwarven_Tile`, `Tile_Dwarven*` per stone); custom ritual. Safe to add,
  not to remove.
- Rimfolk today: altars count for the shrine wish.
- Hook ideas: room plans could offer dwarven tiles as a fine-floor choice (the floor picker already lists buildable
  floors).
- Confidence: high (repo).

## Gerrymon's Greekcore Style (`gerrymon.greekcorestyle`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3666883698
- What it adds: Hellenic style with civilian apparel (chitons, himations, five laurel wreaths); klinai (couch usable as
  a bed that does not make a bedroom), carved stone fence, marbled tile (counts as fine tile).
- Rimfolk today: nothing; klinai might be read as a bed by `Avail.IsSingleBed` (not checked).
- Hook ideas: a symposium-style meeting venue (klinai) for council meetings.
- Confidence: medium.

## The Wild Fields - apparel and weapons (`serek.thewildfields`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2941661586 (SereQ).
- What it adds: 7 garments and 4 weapons inspired by Polish szlachta and Cossacks; with Royalty, noble apparel in the
  Noble Apparel research and satisfies noble needs; with MO, weapons fit MO tiers and apparel uses MO's tailoring
  research; rarely spawns on pawns; no faction. Load folders for MO and Royalty integration.
- Rimfolk today: fine clothes wish may pick these up (quality-based).
- Hook ideas: none.
- Confidence: medium.

## Vehicle Framework (`smashphil.vehicleframework`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3014915404 ; https://github.com/SmashPhil/Vehicle-Framework
  (cloned in scratchpad as `repos/Vehicles`). Library: adds no vehicles itself; used here by MO: Air Vehicles.
- What it adds: `VehiclePawn` (component health, own pathing, turrets, role handlers; passengers in
  `VehiclePawn.AllPawnsAboard`, `Passengers`), `VehicleCaravan : Caravan` (`AllPawnsAndVehiclePassengers`),
  `AerialVehicleInFlight` world object (`AllPawnsAboard`), mod settings per vehicle.
- Rimfolk today: README says colonists aboard a vehicle stay part of the colony (`People.StillOurs`,
  `Vendettas` comment). `People.OffMapColonists` only reflects a property named `AllPawns`, which
  `AerialVehicleInFlight` does not have.
- Hook ideas: also reflect `AllPawnsAboard` / `AllPawnsAndVehiclePassengers` in `OffMapColonists`.
- Confidence: high (source).
