# Batch B: factions, diplomacy and the wider world

This batch covers the mods in the medieval modlist that shape *who else lives on the planet and how the colony deals
with them*: the Outland fantasy series (Core, four faction modules, Genetics, furniture, floors, terrain) and its
companion Forgotten Realms (fantasy xenotypes), the Tasty Armory item packs (historical arms and armor), faction and
diplomacy mods (Factional War, Faction Territories and Vassalage, Diplomatic Marriage +, Allies are Helpful), the
world-map and visitor systems (Vanilla Outposts Expanded, Hospitality, Tavern, Go Explore!), raid and event flavor
(Sometimes Raids Go Wrong, Ugh You Got Me, Animal Husbandry Raids, Vanilla Events Expanded), Vanilla Achievements
Expanded, and two libraries (Vanilla Expanded Framework, Neronix17's Tabula Rasa). The two that matter most to Rimfolk
are **Hospitality** (its guests are Rimfolk "visitors") and **Vanilla Outposts Expanded** (its occupants stay "ours"
while away). In-game articles: `Defs/AlmanacArticleDefs/Almanac_Mods_B_FactionsWorld.xml` (defNames
`Council_AlmMod_B_*`).

Sources: Steam Workshop descriptions fetched through the Steam Web API (`ISteamRemoteStorage/GetPublishedFileDetails`)
and workshop comment pages; GitHub repositories read as data (Outland modules O21-Outland/*, Neronix17/Forgotten-Realms,
SerinaCE/TastyArmory-*, llunak/rimworld-FactionalWar, Ionfrigate12345/Rimworld_DiplomaticMarriagePlus,
alt44s/allies-are-helpful, emipa606/AnimalHusbandryRaids, Vanilla-Expanded/VanillaEventsExpanded and
VanillaAchievementsExpanded, plus the earlier clones of OrionFive/Hospitality, VanillaOutpostsExpanded and
VanillaExpandedFramework). Note: the Outland GitHub repos stop at 1.5 (Terrain has 1.6) while Steam is on 1.6, so
defNames from them are "as of 1.5" and should be re-checked in game.

---

## Outland - Core (`neronix17.outland.core`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2755501685 (GitHub: https://github.com/O21-Outland/Outland-Core)
- What it adds: the shared base of the Outland medieval-fantasy series. Requires VE Framework and Tabula Rasa
  (`neronix17.toolbox`). Buildings in its own architect category `Outland_Outland` with Tabula Rasa subcategories.
  - Production: `Outland_BasicWorkbench` (crafts what the crafting spot does), `Outland_Grindstone` (a facility; near the
    forge it raises quality), `Outland_Forge` (smithing bench for Outland weapons and armor; links to the workbench and
    grindstone), `Outland_DiggingSpot` (on mud or soil next to water: `Outland_MakeClay`, `Outland_MakeAdobeBricks`),
    `Outland_Kiln` (+ `Outland_KilnInput`, `Outland_KilnCombination`; `Outland_FireClayBricks`), `Outland_FormingWorkbench`.
    Also defines `DankPyon_ResearchingSpot` (same defName as Medieval Overhaul's: whichever loads last wins).
  - Materials/items: `Outland_Clay`, `Outland_ClayBrick` (fireproof, weaker than stone), `Outland_AdobeBrick`,
    `Outland_Bones`, `Outland_ComponentMedieval`, `Outland_RationPack` (never rots, deteriorates outside), `Outland_FockattFur`.
  - Defense: manned siege engines `Outland_BoltThrower`, `Outland_Ballista` (loads `Outland_Ammo_BallistaBolt`),
    `Outland_Mangonel` (loads `Outland_Ammo_RoundShot`, long minimum range like a mortar), research
    `Outland_DefencePlatforms`; traps `Outland_FloorTrapChemfuel`, `Outland_FloorTrapSmoke`, `Outland_FloorTrapKnockout`
    (hidden, not adjacent to other traps, calm animals sense them), `Outland_RoofCollapseTrap`, research `Outland_DefenceStructures`.
  - Structure: `Outland_TimberFrameWall`, `Outland_TentWall` (can't hold a roof) and `Outland_TentDoor`, medieval and wide
    doors. Weapons: `Outland_Crossbow` (research `Outland_Crossbows`). Apparel: tribal robes, masks, hoods, backpacks.
    Animal: fockatt (bear-like; forest, grey, polar variants). Research tab `Outland_ResearchTab`, start project `Outland_ResearchStart`.
- In play: doesn't lock any tech. The author lists Medieval Overhaul as "overlapping mechanics and features, not hard
  incompatible but won't be supported specifically" and suggests Resource Dictionary to unify costs; expect duplicate
  materials (clay, bones, components) and benches next to MO's. Not CE-supported. Advises against adding to an existing save.
- Rimfolk today: nothing specific. Generic recognition applies: `Avail.IsWorkstationFor` sees the basic workbench and forge
  as Crafting workstations by their recipes' work skill.
- Hook ideas: room plans could offer a "clay works" (digging spot + kiln) and a "forge with grindstone" smithy variant (the
  grindstone must be adjacent/linked); advisor tip "wooden colony near water: dig clay for fireproof bricks"; a defense
  ambition "man the walls" counting manned siege engines (`CompMannable` turrets); a wish from a passionate crafter for a
  grindstone by the forge.
- Confidence: medium (Steam description + 1.5 GitHub defs).

## Outland - Eastborn Empire (`neronix17.outland.eastbornempire`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2755502128 (GitHub: https://github.com/O21-Outland/Outland-Eastborn-Empire)
- What it adds: faction `Outland_Empire` ("eastborn empire", medieval tech, leader title emperor, pawns "citizens") of
  xenotype `Outland_Elf` (fragile but long-lived), culture `Outland_EastbornCulture`, style `Outland_StyleEastborn`, player
  faction `Outland_PlayerEmpire` and a scenario. Weapons (made at `Outland_Forge`): katana, odachi, nagamaki, naginata,
  su-yari, jumonji-yari, ono, masakari, kanabo-like ararebo, jitte, sai, tanto, sasumata, yumi, matchlock
  (`Outland_EastbornMatchlock`). Apparel: shogun armor, ninja garb, hakama, haygrass hat. Buildings: `Outland_Kamado`,
  `Outland_Hokora`, `Outland_ToriiGate`, `Outland_WideDoorShoji`, `Outland_Sashimono`, `Outland_Toro`, wall box lanterns,
  `Outland_KatanaDisplay`. The faction and xenotype load only with Outland - Genetics active (it is).
- In play: an ordinary (not permanently hostile) faction with neolithic-style caravan and visitor kinds. Steam description
  is marked "out of date, being redone".
- Rimfolk today: nothing specific; its pawns are normal humanlike strangers (encounters, nemeses, vendettas).
- Hook ideas: faction-flavored encounter lines keyed by faction def (`Outland_Empire`: formal courtesy, tea, honor); a
  dilemma "a visiting samurai asks to duel your best fighter"; recognize `Outland_Hokora`/`Outland_ToriiGate` as shrine
  pieces for "a place to pray" wishes.
- Confidence: medium.

## Outland - Motz Coalition (`neronix17.outland.motzcoalition`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2839780788 (GitHub: https://github.com/O21-Outland/Outland-Motz-Coalition)
- What it adds: faction `Outland_MotzCoalition` (medieval, leader title councillor) of moth-like `Outland_Motz` who abhor
  light and can fly short distances; culture `Outland_SubsannaCulture` (prefers Subsanna biomes if present); player factions
  `Outland_MotzPlayerColony`/`Outland_MotzPlayerTribe`. Ancient-Greek style gear: rapier, machaira, labrys, dory spear,
  xiphos, epsilon axe, kopis; Motz cuirass, tunic, helmets, pauldrons. Animal: `Outland_Oxect` (insect-like, larva to adult
  life stages) and an oxect sculpture group.
- In play: dark-loving people: a Motz recruit may dislike well-lit rooms (check genes).
- Rimfolk today: nothing specific.
- Hook ideas: Rimfolk's light-related advice ("rooms without light", fear-of-dark quirk) should not nag about darkness for a
  colonist whose genes prefer the dark (check `GeneDef`s from Outland Genetics for a darkness preference before warning).
- Confidence: medium.

## Outland - Redburn Pact (`neronix17.outland.redburnpact`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2839780900 (GitHub: https://github.com/O21-Outland/Outland-Redburn-Pact)
- What it adds: faction `Outland_RedburnPact` (medieval, leader title ascensionist), described as orc-led raiders with a
  large goblinoid presence: xenotypes `Outland_Goblin` (most common), `Outland_Varag`, `Outland_Hobgoblin`, `Outland_Orc`
  (rarest). Gear: pact fork, spear, obsidian sword, pact crossbow, sword, axe, club, dagger; furs, harness, warlord armor.
  Buildings: giant skull decorations, `Outland_PactCatapult` turret, `Outland_PactBanner`. Player factions (goblinoid colony/tribe).
- In play: in the 1.5 defs it is not flagged `permanentEnemy`, so goodwill works normally.
- Rimfolk today: nothing specific.
- Hook ideas: faction-flavored taunts in the "fallen enemies" encounters; a nemesis named with a goblin epithet.
- Confidence: medium.

## Outland - Vathun Nobles (`neronix17.outland.vathunnobles`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3024247203 (GitHub: https://github.com/O21-Outland/Outland-Vathun-Nobles)
- What it adds: faction `Outland_VathunNobles` (medieval, leader title lord, pawns "nobles") of `Outland_Faun` (deer-like,
  small horns; strict caste system, forest culture). Axe-heavy gear: war axes, heavy war axes, long axes, bardiches,
  military pick, kitchen cleaver, Vathun sword and bow, an axe-gun; peasant/guard/noble helmets, pauldrons, cuirasses,
  surcoat, cloak. Buildings: rabbit and vortex sculptures, reliefs, `Outland_VathunBanner`.
- In play: ordinary medieval faction; caste flavor only.
- Rimfolk today: nothing specific.
- Hook ideas: the caste theme fits Rimfolk's "standards"/nobility: Vathun visitors could react to the colony's bedrooms or
  throne room in encounters.
- Confidence: medium.

## Outland - Genetics (`neronix17.outland.genetics`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2910172297 (GitHub: https://github.com/O21-Outland/Outland-Genetics)
- What it adds: a very large gene library (mostly cosmetic, some functional, some archite and deliberately overpowered but
  opt-in: they never spawn unless another mod puts them on pawns). Patches vanilla gene categories. Requires Biotech, VEF
  and Tabula Rasa. Required by Forgotten Realms; the Outland faction modules only load their factions/xenotypes with it.
  The author's Tweaks Galore controls which genes appear in genepacks (not in this modlist).
- In play: overlap with other gene mods (Alpha Genes, VE Genetics in this list) is expected.
- Rimfolk today: nothing specific.
- Hook ideas: read gene-driven traits (darkness preference, long life, fragility) when choosing wish/advice lines; a
  "coming of age" or dream variant for unusual xenotypes.
- Confidence: medium (description; gene list not audited).

## Outland - Furniture & Decor (`neronix17.outland.furnitureanddecor`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2897435739 (GitHub: https://github.com/O21-Outland/Outland-Furniture-Decor)
- What it adds: wood and stone furniture sets (much of the wood set based on Gloomy Furniture): `Outland_BedSingle`,
  `Outland_BedDouble`, tables `Outland_1x1Table` to `Outland_3x3Table` and `Outland_3x3TableHearth` (wood-fueled, glows,
  heat 21/s), chairs, dining chairs, benches, stools, end tables (with and without lamp), dressers, wardrobes, bookshelves,
  shelves, wine racks, rugs, barrels, buckets, pots, gravestone, borders; stone variants (`Outland_Stone*`). Lights:
  `Outland_OilLantern`, `Outland_OilStreetLamp`, `Outland_WallLantern`, `Outland_OilWallLanterns`, `Outland_WallSconce`
  (refuelable with WoodLog or Chemfuel, glow radius 9, small heat; Ideology darklight variants).
- In play: no power needed for lights; all in the Outland architect tab behind subcategory buttons.
- Rimfolk today: generic recognition: `Avail.IsLight` (glower radius at least 4, not a work table) accepts its lanterns and
  sconces for the bedroom-light wish, dark-room advisor signal and personal touches; `Avail.IsHeater` (heat at least 10/s)
  would count the hearth table as a heater; beds, eating tables and chairs by `IsHumanBed`/`IsEatingTable`/`IsChair`.
- Hook ideas: room plans could prefer fuel lights in pre-industrial colonies (`Outland_WallSconce` frees floor space); a
  "hearth hall" dining-room variant using `Outland_3x3TableHearth`.
- Confidence: medium (1.5 defs).

## Outland - More Floors (`neronix17.outland.morefloors`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2873199953 (GitHub: https://github.com/O21-Outland/Outland-More-Floors)
- What it adds: buildable wooden and stone floors (split out of Outland - Terrain in 1.4). Standalone. Not patched for
  Stuffed Floors. Note the modlist also has `telkir.tmods.morefloors` (a different "More Floors").
- In play: decor/beauty only.
- Rimfolk today: nothing specific; the room planner's floor picker lists any buildable floor the colony can make.
- Hook ideas: none beyond the floor picker; could tag "fine floor" candidates by beauty stat for noble rooms.
- Confidence: medium (description; defs not listed).

## Outland - Terrain (`neronix17.outland.terrain`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2850840457 (GitHub: https://github.com/O21-Outland/Outland-Terrain)
- What it adds: a retexture of natural terrain plus some new terrains; settings for alternatives (including static water).
  Load after ReGrowth Core, VTE. Safe to add to existing saves (as of 1.4).
- In play: visual only.
- Rimfolk today: nothing.
- Hook ideas: none.
- Confidence: high (description; 1.6 present on GitHub).

## Forgotten Realms - Biotech Edition (`neronix17.forgottenrealms`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3068979395 (GitHub: https://github.com/Neronix17/Forgotten-Realms)
- What it adds: D&D-inspired peoples as Biotech xenotypes, all prefixed `Forgotten_`: dwarves (`ShieldDwarf`, `GoldDwarf`,
  `ArcticDwarf`, `WildDwarf`, `Duergar`), elves (`HighElf`, `WoodElf`, `MoonElf`, `SunElf`, `WildElf`, `SnowElf`, `Drow`),
  gnomes (deep, forest, rock), `Halfling`, tieflings (infernal, abyssal, chthonic), `Aasimar`, `Cambion`, `Deva`,
  `Planetar`, genasi (air, earth, fire, water), dragonborn (nine colors), `Goliath`, `Firbolg`, `Satyr`, `Tabaxi`,
  `Kobold`, `HalfOrc`, `Orc`, `Goblin`, `Hobgoblin`, `Bugbear`, `Gith`, `ThriKreen`, `Minotaur`, `Killoren`, `Chitine`,
  `Illithid`. `FactionDispersalPatches.xml` adds most of them (weight 0.1 each; dragonborn 0.015) to
  `OutlanderFactionBase` and to `TribeCivil`/`TribeRough`/`TribeSavage`; some (orc, goblinoids, minotaur, illithid,
  chitine) are commented out of the dispersal. Illithids: `elder brain` item (quest reward, exotic traders) -> elder
  brain pool building -> `illithid tadpole`; implant gives hediff "illithid ceremorphosis" (about three days,
  unstoppable, keeps sense of self, an elder brain pool forces them into your faction). Warforged need Asimov
  (`Neronix17.Asimov`, not in the modlist). Requires Outland - Genetics.
- In play: no factions of its own; visitors, raiders and recruits from vanilla-based outlander/tribal factions get very
  varied. Medieval-mod factions not built on those bases don't get them.
- Rimfolk today: nothing specific (they're humanlike with moods, so all Rimfolk systems apply).
- Hook ideas: a stranger dilemma around the illithid tadpole (implant a prisoner? the victim's friends react; a secret
  "they put something in my head"); xenotype-aware flavor tags for encounters (a dwarf haggling, a tiefling insulted);
  a quirk for a ceremorphosed colonist ("dark dreams of the elder brain") via the dreams system.
- Confidence: medium-high (1.6 defs on GitHub).

## Neronix17's Toolbox / Tabula Rasa (`neronix17.toolbox`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1660622094 (GitHub: https://github.com/Outpost-21/Tabula-Rasa)
- What it adds: library (formerly "[O21] Toolbox", now "Tabula Rasa") for Outpost 21 / Outland mods. Only user-facing
  feature: a dev-mode button that opens the xenotype editor. Provides e.g. `TabulaRasa.DefModExt_SubCategoryBuilding`
  (architect subcategories used by Outland), `TabulaRasa.CompProperties_AdvFireOverlay`.
- In play: nothing on its own. Not CE-supported.
- Rimfolk today: nothing.
- Hook ideas: if Rimfolk ever needs to place or describe Outland buildings, their architect subcategory comes from
  `DefModExt_SubCategoryBuilding` (useful for "where to find it" hints in room plans).
- Confidence: high (description + About.xml: packageId `neronix17.toolbox`, name "Tabula Rasa").

## Tasty Armory - Core (`al9000.tastycore`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3469863391 (GitHub: https://github.com/SerinaCE/TastyArmory-Core)
- What it adds: shared base defs, apparel layer and research for the Tasty Armory packs by Serina (al9000), a few shared
  items (maille shirt, quiver, gambeson coat, capelets and cloaks), research `Vanilla_GunpowderWeaponry` ("Gunpowder
  Weaponry", 1000, medieval, requires Smithing) and a patch that improves metal stuff colors.
- In play: "It's all just items": no factions, buildings or systems. Recipes on vanilla `FueledSmithy`/`ElectricSmithy`,
  `HandTailoringBench`/`ElectricTailoringBench`, `CraftingSpot`; research gates are vanilla (`Smithing`,
  `ComplexClothing`, `LongBlades`, `PlateArmor`) plus Gunpowder Weaponry. Balanced for CE first; author invites better
  vanilla values. Alternate apparel modes via VEF (`CompSwitchApparel`-style switching). Safe to add midgame; removal
  gives harmless one-time errors.
- Rimfolk today: generic: `WishWorker_Armor` is fulfilled when the colonist wears any apparel with ArmorRating_Sharp of
  at least 0.35, and `CanMakeArmor` looks for available recipes producing `ApparelArmor`/`ArmorHeadgear` items at a bench
  the colony has or can build, so these packs feed armor wishes automatically.
- Hook ideas: an heirloom-quality wish ("a masterwork arming sword"); an ambition "arm the militia" counting fighters in
  armor at or above the wish threshold; era-aware flavor (gunpowder lines only if `Vanilla_GunpowderWeaponry` is done).
- Confidence: high.

## Tasty Armory - Steppe Warriors (`al9000.tastysteppe`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3472989392 (GitHub: https://github.com/SerinaCE/TastyArmory-SteppeWarriors)
- What it adds: steppe cultures (Mongol, Tatar, Cuman, Jurchen, Manchu, Timurid): kaftan, noble kaftan, nomad fur hood,
  noble hat; lamellar aventail, brigandine, masked and iron pagoda helmets, fur cap; padded and iron degel, lamellar vest
  and heavy lamellar; rattan shield, riding boots, bazubands, saber scabbard; steppe warbow, lance, mace, axe, saber.
  Re-release with changed defNames (not save-swappable with the old version).
- In play / Rimfolk today / Hook ideas: see Tasty Armory - Core.
- Confidence: high (description).

## Tasty Armory - In Shining Armor (`al9000.shiningarmor`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3482266407 (GitHub: https://github.com/SerinaCE/TastyArmory-InShiningArmor)
- What it adds: late-medieval western Europe: arming doublet, fur coat, chaperon; open-face helmets, armet, bascinet,
  sallet; reinforced gambeson; pollaxes, falchion, messer, pike, horseman's axe, warhammer, halberd, voulge, guisarme,
  fauchard, couteau de breche; composite and steel heavy crossbows, handgonne, siege harquebus (gunpowder research).
- In play / Rimfolk today / Hook ideas: see Tasty Armory - Core.
- Confidence: high.

## Tasty Armory - Fire and Steel (`al9000.fireandsteel`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3469863838 (GitHub: https://github.com/SerinaCE/TastyArmory-FireNSteel)
- What it adds: c. 1520 to 1650 Europe and Ottoman pike and shot: longcoat, cassock, doublets, many hats; morion,
  cabasset, burgonets, close helmet, lobstertail, hussar and sipahi helmets; buffcoats, padded doublet, gorget, plate
  cuirass, pikeman's corselet, three-quarter harness, yushman, krug, gauntlets; bardiche, boarding axe, halberd, hanger,
  hussar saber, pike, rapier, sidesword, war pick, yatagan, zweihander; arquebus, miquelet, matchlock musket, snaphaunce,
  wheellock pistols (cannon and hand mortar are CE-only).
- In play: the most gunpowder-heavy pack; may feel late for an early-medieval colony.
- Rimfolk today / Hook ideas: see Tasty Armory - Core.
- Confidence: high.

## Tasty Armory - High Medieval (`al9000.highmedieval`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3480089301 (GitHub: https://github.com/SerinaCE/TastyArmory-HighMedieval)
- What it adds: high-medieval Europe (Norse, Frankish, Danish): tunic, dress, cloak, nordic fur hat, headscarf; nasal
  helm, spectacle helm, great helm, maille and padded coifs; gambeson coat, aketon, lamellar vest, maille shirt, coat of
  plates, maille hose and mittens; scabbard, arrow bag, crossbowman's belt; one-handed spear, billhook, greataxe, hand
  axe, arming sword; composite and steel heavy crossbows. No Medieval Overhaul patch yet (author FAQ).
- In play / Rimfolk today / Hook ideas: see Tasty Armory - Core.
- Confidence: high.

## Tasty Armory - Sengoku (`al9000.sengoku`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3494429497 (GitHub: https://github.com/SerinaCE/TastyArmory-Sengoku)
- What it adds: late Sengoku Japan: kimono, yukata, straw hat; jingasa, kabuto; samurai and ashigaru armor, armored
  sleeves, shoulder plates; obi, sword belt, cartridge box, yumi quiver, rice ration bundle, sashimono banner; kanabo,
  ararebo, wakizashi, katana, yari, nagae-yari, naginata; yumi, ban-zutsu, tan-zutsu, samurai-zutsu, o-zutsu. Clan
  insignia variants usable as faction ideo style.
- In play: overlaps thematically with Outland Eastborn Empire gear.
- Rimfolk today / Hook ideas: see Tasty Armory - Core.
- Confidence: high.

## [SR]Factional War (fork) (`sr.modrimworld.factionalwarcontinued`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3423264477 (GitHub: https://github.com/llunak/rimworld-FactionalWar);
  original: https://steamcommunity.com/sharedfiles/filedetails/?id=2534328163
- What it adds: four incidents (category Misc): `SrFactionWar` (Faction Assault: two rival factions fight on your map),
  `SrFactionWarShellingSiteGenerate` (Faction Bombardment: they bombard each other from map corners; site
  `SrSiteFactionWarShelling`), `SrFactionWarContentionSiteGenerate` (Faction Contention: cargo near a mechanoid cluster;
  site `SrSiteFactionWarContention`), `SrFactionWarTempCampSiteGenerate` (Faction Defense: a rival's camp under attack;
  site `SrSiteFactionWarTempCamp`). Custom duties/AI (`SrAssaultFactionFirst`, `SrKillHostileFactionMember`,
  `SrPlunderFaction`, `SrClearBattlefield`, `SrRetreat`), arrival mode `SrTwoFactionsEdgeWalkIn`. The package id is the
  "Continued" one; the fork (llunak) supersedes the abandoned Continued (3220197628).
- In play: the factions ignore you and fight each other; afterward they self-tend, execute downed enemies, rescue their
  own, loot, and can kidnap colonists of a faction they're hostile to. Fork changes: forces a fight if one side is
  attacked; a fight on your map is a minor (not big) threat. Vanilla rule: an ally dying on your map can cost goodwill.
  "Medieval-related mods may prevent high-tech faction pawns from being generated." Contention relies on a mechanoid
  cluster: with Rimedieval's mechanoid settings that event may misbehave (unverified). Known comment: war tab errors after
  one faction wipes out another.
- Rimfolk today: generic. Downed hostile soldiers are `StrangerKind.Fallen` (checked before the quest-reservation test).
  `NemesisTracker` records hostile humanlike attackers of colonists; vendettas fire for kills/kidnaps by a hostile faction.
  Because the fork marks the fight a minor threat, `SeedReturns` (keyed on `LastThreatBigTick`) won't slip a nemesis into
  these battles. Friendly soldiers are `StrangerKind.None` (not visiting/trading), so no encounters with them.
- Hook ideas: a council dilemma when `SrFactionWar` fires ("ride out for our allies, open the gates to the wounded, or
  bar the doors"), with goodwill and openness/mercy effects; an encounter "a wounded soldier of a friendly faction asks for
  shelter"; colony character: repeatedly helping allies builds a "loyal" reputation; annals line "we watched X and Y fight
  in our fields".
- Confidence: high (GitHub defs + descriptions).

## Faction Territories and Vassalage (`jaeger972.factionterritories`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3626725895
- What it adds: a world map mode (requires Map Mode Framework, `nozome.mapmodeframework`, in the list) drawing each
  faction's territory and contested ground; more caravans and ambushes from a faction inside its territory; faction
  patrols joining encounter maps in their territory; friendly/enemy raids when visiting sites in territory; vassalising
  defeated faction bases, recruiting vassal pawns, tithing vassals for items or silver; factions invading each other and
  your vassals, with warzones you can join. Integrations: Vanilla Outposts Expanded, Rim War scan range and power, Empire
  mod, More Faction Interaction, Roads of the Rim, Dynamic Diplomacy. Planned: worksite spawns, taxation when settling in
  territory, diplomatic envoys. Custom faction colors via a settlement gizmo. Save compatible.
- In play: comments report a faction that loses all settlements can keep raiding ("phantom faction"), and no territories
  on Worldbuilder maps. No GitHub source, so class and def names are unknown.
- Rimfolk today: nothing.
- Hook ideas: vassal tithes as council decisions (demand the full tithe vs. forgive it in a bad year: generosity/mercy
  ways); an ambition "a seat of power" variant "hold N vassals"; annals counting vassals gained/lost; a wish from an
  ambitious colonist to govern a vassal. Needs reflection on the mod's world component (names to be discovered from the
  assembly).
- Confidence: medium (description and comments only).

## Diplomatic Marriage + (`ionfrigate12345.diplomaticmarriageplus`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3222768517 (GitHub: https://github.com/Ionfrigate12345/Rimworld_DiplomaticMarriagePlus)
- What it adds: incidents `DMP_Proposal` (worker `DiplomaticMarriagePlus.Controller.ProposalEventController`, baseChance
  50, refire 30 days), `DMP_RandomVisit`, `DMP_Rimcities_PACombinedAssault`, `DMP_Rimcities_PACombinedDefense`,
  `DMP_DynamicDiplomacy_AllianceAgainstPA`; model classes `DiplomaticMarriagePlus.Model.PermanentAlliance` and
  `TemporaryStay`. Conditions: Ideology on (not classic mode), an ideoligion leader with an adult unmarried direct child,
  an NPC ally at goodwill 75 or more; the ally proposes. Accepting brings the NPC spouse with guards (wedding on the spot
  if the child is present); the child is moved to the ally faction (`SetFaction`). Permanent alliance: daily goodwill gain
  (scaled by the couple's Social), chance to convert the ally's ideology, "Caravan Refuge" and "Homesick" visits (both
  spouses temporarily `SetFaction(Faction.OfPlayer)`), joint settlement takeover via comms, APFP/Rimcities/Dynamic
  Diplomacy integrations (not in this list). Ends on death of either spouse, divorce, low goodwill, faction defeat, or a
  new leader who isn't the child's parent. Settings: daily goodwill, Social factor, threat multiplier, conversion chance,
  warning when the couple is on your map, homesick timings.
- In play: the author warns never to recruit the couple when they come as Hospitality guests; Better Relationship Value
  and EdB Prepare Carefully are listed incompatible.
- Rimfolk today: generic, and worth knowing: when the child leaves (faction change), `Vendettas.WatchLosses` treats it as a
  loss (closest friends get the aspiration "loss" nudge; no vendetta, since nobody kidnapped them), `Annals` lists them under
  `CC_Annals_Gone` ("Gone from us, but not dead as far as we know"), and their journal moves to the departed. During a
  homesick visit both spouses are full player-faction colonists (not quest lodgers), so Rimfolk counts them (wishes,
  trust, possibly elections) and registers a second "loss" when they leave.
- Hook ideas: recognize a DMP marriage (child's faction is the permanent ally, read via reflection on
  `PermanentAlliance`) and write it as "married into X" in annals/journals instead of "gone"; treat the homesick visitors
  as lodgers (exclude from elections and quitting); a council vote on the proposal itself (the leader's family vs. the
  colony); a life goal "see my child wed well"; a farewell eulogy-like letter from the parent.
- Confidence: high (GitHub source + description).

## Allies are Helpful (`ninagoblin.alliesarehelpful`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3534920369 (1.6 re-upload by Nina Goblin of alt4s's mod
  https://steamcommunity.com/sharedfiles/filedetails/?id=3235428952; source https://github.com/alt44s/allies-are-helpful)
- What it adds: allied faction pawns tend downed/injured colonists and allies and carry them to an empty medical bed,
  after combat and only when no enemies are near (danger detection with radius and line-of-sight options). Settings in
  the original source: tend colonists on, tend allies on, minimum Medicine skill 0, rescue colonists on, rescue allies off,
  danger system on, radii 10 cells. Harmony patches on pawn jobs.
- In play: keep a medical bed free; allies may carry colonists into your hospital mid-battle aftermath.
- Rimfolk today: nothing. Allied reinforcements are `StrangerKind.None` (not visitors), so no encounters with them.
- Hook ideas: when an ally rescues or tends a colonist (detect a job by a non-player humanlike targeting a colonist),
  give the colonist a gratitude memory and a journal line, and maybe a "comrades" bond with that ally; a dream about the
  stranger who carried them.
- Confidence: medium (1.6 build is a re-upload; defaults read from the 1.5 source).

## Vanilla Outposts Expanded (`vanillaexpanded.outposts`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2688941031 (content in scratchpad clone; the outpost code
  lives in VE Framework's `Outposts` assembly)
- What it adds: world objects (`WorldObjectDef`, parent `OutpostBase`): `Outpost_Farming` (Plants 10, growing season, no
  desert; chosen crop; production every 3,600,000 ticks = 60 days), `Outpost_Artillery` (mortar strikes, range 15),
  `Outpost_Defensive` (3 armed pawns min, range 15; intercepts raids on the colony, reducing raid points up to 50% from
  combined Shooting+Melee; can send drop pods), `Outpost_Drilling` (desert only, Construction 20, chemfuel by Mining),
  `Outpost_Encampment` (caravan rest stop, pack time 17,500 ticks), `Outpost_Hunting` (chosen animal; Ideology event
  `VOE_JoinHuntingOutpost`), `Outpost_Logging` (3 pawns, 225 wood per pawn per 60 days; event `VOE_JoinLoggingOutpost`),
  `Outpost_Mining` (hills/mountains, Mining 10; stone blocks of the tile's rocks; resources by cumulative Mining: steel 15,
  jade 20, uranium 25, silver 30, gold 35, plasteel 40, components 50), `Outpost_Production` (Crafting 10; components),
  `Outpost_Scavenging` (60 days), `Outpost_Science` (Intellectual 30; research during the day), `Outpost_Town` (5 pawns,
  3 settlements within 10 tiles; travelers may join), `Outpost_Trading` (Social 10; silver = 30 per Social level). Building
  `VEF_OutpostDeliverySpot`. The 1.6 Fishing and Factory folders are disabled. Framework settings (`OutpostsMod.Settings`):
  delivery method (default Teleport to the delivery spot or the map edge facing the outpost; also pack animal, store,
  pods), production and time multipliers, raid toggle, frequency and difficulty, per-outpost overrides. Defaults: 15 days
  per production, 7 days to pack.
- In play: no upkeep; occupants eat the outpost's provided food, rest at night, heal, age, have chemical needs filled and
  addictions zeroed; FAQ: no skill gain, no illness, no social fights, no mental breaks. Can't be placed on or next to a
  settlement or another outpost. Raids generate a map; all occupants dead means "Outpost lost". With no occupants it's
  abandoned. Slaves and prisoners can be posted. Known issue: outposts sometimes vanish after battles (third-party mods).
- Rimfolk today: substantial. `People.OffMapColonists()` reflects on any world object's public `AllPawns`
  (`Outposts.Outpost.AllPawns => occupants`) and yields player-faction pawns; `People.StillOurs` keeps them "ours"
  (alive, player faction, not prisoner/kidnapped). Used by `Vendettas.WatchLosses` (away at an outpost is not a loss: no
  aspiration "loss" nudge, no eulogy), `Annals` (not listed as gone), `People.OursIds` (journals stay in the present list,
  not "departed"), `CouncilGameComponent.KnownPawnIds` (records not pruned). A death at the outpost is processed as a
  normal loss (killers from the combat log, eulogy, nemesis notify). Away colonists can't voice wishes
  (`CanVoiceWishes` needs a spawned pawn on a home map), attend meetings, or be candidates/voters (`Offices.Eligible` =
  spawned free colonists), so an officer posted away keeps the seat (`Offices.Valid` doesn't require spawned) until the
  next yearly election, where they can't be re-elected. A bitter colonist can't announce leaving while away, and a
  pending departure waits until they're home (`Quit` defers).
- Hook ideas: a decree or dilemma "post them to the outpost" (send a bitter or quarrelsome colonist away: rivals relieved,
  friends hurt); the travel wish (`WishWorker_Travel`) already completes in the caravan that founds an outpost; letters
  "from the outpost" into journals on each delivery (hook `Outpost.Produce`/delivery letter); annals counting outposts and
  deliveries; an ambition "hold the frontier" (N outposts for a year); exempt absent officers from the yearly election or
  let the colony vote by letter; room plan for the delivery spot inside walls.
- Confidence: high (1.6 defs and framework source).

## Hospitality (`orion.hospitality`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=753498552 (GitHub: https://github.com/OrionFive/Hospitality;
  author no longer actively developing, accepts PRs)
- What it adds: its own visit lord job `Hospitality.LordJob_VisitColony` (namespace `Hospitality`), visitor incidents
  `VisitorGroup`, `VisitorGroupMax`, `VisitorGroupSelectFaction` (fired by its own logic, baseChance 0 in the storyteller
  defs), spacer variants `VisitorGroupSpacerCruise`/`VisitorGroupSpacerLuxury`, `HappyGuestJoins` (a guest joins as an
  ambassador without hurting relations). Guest beds: any bed gets a "For guests" toggle (`Building_GuestBed`, fee in
  steps of 10 silver; thought `GuestClaimedBed` from "free bed" +2 to "extremely overpriced bed" -10); room role
  `GuestRoom`. Guests tab (`MainTab`) with per-guest allowed area and shopping area, "Make default", recruit, force
  recruit, send away; map settings: allow new guests, refuse until guest beds, free food, guests ask if safe. Work type
  `Diplomat` (labelShort "Entertain"; work givers `Recruiter`, `Diplomat`), interactions `CharmGuestAttempt`,
  `GuestDiplomacy`, `ScroungeFoodAttempt`; joy givers `BuyStuff`, `BuyFood`; vending machines (nutrient paste dispenser
  only); goodwill events `VisitBetterThanExpected`/`VisitWorseThanExpected`; thoughts `GuestAngered`,
  `GuestRecruitmentForced` (-20), `GuestPleasedRelationship`, `GuestOffendedRelationship`. Faction dialog option "Invite
  for a visit" (patch on `FactionDialogMaker`). Recruiting: friends required = 1 for 3 or fewer colonists, else about
  0.887 x colonists^0.646 (rounded), plus the guest's enemies in the colony; relatives count 2; titled guests need royal
  friends' seniority instead. Mod settings: guests helping (min skill 7), art/craft/operations/tending switches, gifts,
  group size, max groups per 3 days (recommended 5), silver multiplier.
- In play: visits end with a score letter (awful to amazing) and a "will visit again" estimate. Known incompatibilities:
  mods giving guests animals or slaves, Colony Manager, RimSlaves, GeneticRim, ClosedDoors. Load late.
- Rimfolk today: substantial. `Strangers.IsHospitalityGuest(x)` (lord job namespace is "Hospitality") makes them
  `StrangerKind.Visitor` in `Strangers.KindOf`, so all visitor encounters and stranger dilemmas can pick them (e.g.
  `Council_DlmS_WantsToStay`, `Council_DlmS_RudeGuest`); encounter memories such as `Council_EncP_GoodCompany` are social
  thoughts on the guest about the colonist, which raises the opinion Hospitality counts toward recruit friends.
  `DilemmaEffect_Recruit.Unavailable` disables recruit options for Hospitality guests with key
  `CC_Stranger_HospitalityRecruit` ("invite them to stay the Hospitality way, from their guest tab"); `Apply` also refuses.
  `DilemmaEffect_SendAway` returns without acting for Hospitality lords. Gap: in `Council_DlmS_RudeGuest` the "Throw the
  lot of them out" option still applies its -10 goodwill and thoughts even though the party isn't sent away (consider an
  `Unavailable` for it too, or calling Hospitality's own send-away). Guests are never council members (not player faction).
- Hook ideas: a "guest wing" room template using beds flagged for guests (detect `Building_GuestBed` by type name) and the
  `GuestRoom` role; wishes from Social/kind colonists for better guest rooms; an ambition "renowned hosts" counting visits
  that ended in `VisitBetterThanExpected`; encounter lines when a guest buys a colonist's craft (pride), or scrounges
  food; a dilemma when `HappyGuestJoins` fires (the guest's chosen friend vouches for them); the speaker or quartermaster
  office boosting `RecruitEffectivity`-style outcomes; colony character "openness" rising with each good visit.
- Confidence: high (1.6 source).

## Tavern (`ods.tavern`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3775694305
- What it adds: "a large set of medieval furniture for furnishing your tavern", plus a home meal and kitchen set.
  Requires VE Framework (graphic choice via `CompGraphicCustomization`-style "choose graphic"). From the author's replies
  and player comments (may be outdated): upholstered beds (comfort comparable to VFE ergonomic beds, wool needed), sturdy
  closets, dressers and end tables that link to beds (with per-bed limits), counters and cabinets (seamless joining is
  fiddly), banners and flags, wall candleholders and trophies, rugs, decor tables with their own recipes, its own
  architect tab, candles fueled by tallow/wax rendered from rotted carcasses (patch accepts Medieval Overhaul and Epochs
  tallow), a home/tavern oven (reported not to need fuel, behind Machining research, and its room not detected as a
  kitchen), home meals (rice buns etc.) weaker than fine meals. Reported: the upholstered double bed didn't satisfy
  royal "double bed" requirements.
- In play: purely furniture; pair with Hospitality for actual guests.
- Rimfolk today: generic recognition by function (beds, eating tables, chairs; lights only if their glow radius is 4+;
  unverified for its candles). Its oven room might not count as a kitchen for Rimfolk either (Rimfolk checks room roles
  for some advice; unverified).
- Hook ideas: once its defNames are known (download needed), a "tavern hall" room plan (dining + rec room hybrid) and a
  wish for "a proper tavern" from social drinkers; treat its candles as lights when glow qualifies.
- Confidence: low-medium (no source files; description, author replies and comments).

## Go Explore! (`albion.goexplore`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1814100216 (no public source found)
- What it adds: events/sites: Lost City (bombarded, infested, toxic), Ship core startup (take over or scrap a ship guarded
  by its owners), Ambrosia-addicted animals defending a large ambrosia sprout, New settlement (a faction expands near
  you), Research request (send your best researcher to a non-hostile faction for goodwill and research points to a random
  unfinished project), Prison camp (free a non-hostile faction's prisoners), Intercepted message (loot or ambush). Safe on
  existing saves; CE compatible.
- In play: all are optional trips. The bombarded city and ship core are sci-fi; Rimedieval's incident removal list does
  not include them. Comments: infested-city loot can be grabbed by reforming the caravan instantly; SOS2 breaks the ship
  event (not in this list).
- Rimfolk today: generic: `WishWorker_Travel` (to see something of the world) completes when the colonist is a caravan
  member, so these trips can grant it.
- Hook ideas: Rimfolk "omens" or dreams before a Research request; a life-goal "explore a lost city"; the researcher
  sent away writes journal entries on return; dilemmas around the prison camp (risk colonists for strangers: mercy vs.
  safety).
- Confidence: medium (description; defNames unknown).

## Sometimes Raids Go Wrong (`marvinkosh.sometimesraidsgowrong`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1551336515 (source zip on the author's Dropbox)
- What it adds: storyteller twists on raids: Rapid ageing (military mechanites; treat captured raiders with good
  medicine), Manhunters, Pod malfunction (drop pods crash), Meteorite shower, Drunk Drop (drinkers arrive intoxicated),
  Burning Men (some raiders arrive burning and injured, chased by mechanoids), Run Wild (some raiders become wild
  humanoids that can be tamed). Each enabled and weighted in settings (global, persists across saves).
- In play: "Doesn't work with RimWar" (not in this list). Known issue: with Odyssey, burning men might not be chased.
  In a medieval world, pod and mechanoid twists may not fit or misfire.
- Rimfolk today: nothing specific. Run-wild raiders (no faction) are `StrangerKind.None`.
- Hook ideas: encounter/dream lines after a raid went wrong ("the raiders were drunker than our cook"), a nemesis who
  "ran wild" and is later tamed; annals line for the funniest raid of the year.
- Confidence: high (description).

## Ugh You Got Me (`marvinkosh.ughyougotme`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1542424705
- What it adds: social fights stop when a pawn is badly hurt or in a lot of pain; masochists ignore pain; bloodlust pawns
  continue until the opponent gives in, dies or is downed; romantic partners may stop if one likes the other; fights that
  would end at once never start; bleeding missing parts stop the fight.
- In play: fewer deaths and maimings from social fights.
- Rimfolk today: generic: `DilemmaEffect_Fight` (used by dilemmas, encounters and social events) calls vanilla
  `StartSocialFight`, so these rules govern Rimfolk brawls too, and a brawl may simply not start if it would end at once.
- Hook ideas: none needed; a "made up after the fight" social event could key off the fight ending early.
- Confidence: high.

## Vanilla Events Expanded (`vanillaexpanded.vee`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1938420742 (GitHub: https://github.com/Vanilla-Expanded/VanillaEventsExpanded)
- What it adds (1.6 incidents): purple chains `VEE_Whiteout`, `VEE_Scorch`, `VEE_LongNight`, `VEE_PsychicBloom` (game
  conditions of the same names), purple variants `RaidEnemyPurple`, `ManhunterPackPurple`, `InfestationPurple`,
  `AnimalInsanityMassPurple`, `VEE_WhiteoutRefugees` (accept: they join; refuse: they attack), `VEE_PsychicRain`
  (condition `PsychicRain`), `VEE_Drought` (condition `VEE_Drought`: outdoor crops and wild plants nearly stop and die
  off, fire spreads faster, hydroponics unaffected), `VEE_SpaceBattle` (orbital ship crash), `VEE_MeteoriteShower`,
  `VEE_CropSprout`, `VEE_AnimalPodCrash`, `VEE_ShuttleCrash`, `VEE_Earthquake` (ThreatBig; structural damage near the
  epicenter), `VEE_Cargopodsweapons`/`VEE_Cargopodsapparel`, `VEE_CaravanAnimalsWanderIn`, `VEE_BattleAnimalsWanderIn`,
  `VEE_WildMenWanderIn`, `VEE_HuntingParty` (peaceful hunters leave after one kill each), `VEE_WandererJoinsTraitor`
  (quest; the joiner is an agent of a hostile faction and turns), `VEE_ManhunterSingleArrival`, `VEE_Crashlanded`,
  `VEE_PsychicStimulation`/`VEE_PsychicOverdrive`/`VEE_PsychicHum`, `VEE_VisitorGroupRaid` ("visitors turned out to be a
  deceptive raid"). FAQ: purple events only after day 180, then none for 300 days; every event's chance adjustable to 0.
- In play: Rimedieval doesn't remove these; switch off space-flavored ones by hand for a medieval run.
- Rimfolk today: `AdvisorWorker_Conditions` knows vanilla `Drought` (Odyssey), `HeatWave`, `ColdSnap`, `Eclipse`, etc.,
  but not `VEE_Drought`, `VEE_Whiteout`, `VEE_Scorch`, `VEE_LongNight`, `VEE_PsychicBloom`. Disguised raiders and
  traitors are ordinary hostile pawns once revealed (nemesis/vendetta apply); while disguised as visitors they may be
  picked for visitor encounters.
- Hook ideas: add the VEE conditions to `AdvisorWorker_Conditions` (with `CC_Adv_Cond_VEE_*` keys and Almanac links);
  seasonal dilemma for whiteout refugees (mercy vs. food); the traitor as a secret ("a borrowed name") the colony uncovers,
  and an automatic nemesis if they escape; encounter lines with hunting parties (currently `StrangerKind.None`: their lord
  isn't a visit); earthquake repairs as a "stone and mortar" ambition trigger.
- Confidence: high (1.6 defs).

## Animal Husbandry Raids (Continued) (`mlie.animalhusbandryraids`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2052156867 (GitHub: https://github.com/emipa606/AnimalHusbandryRaids)
- What it adds: a Harmony postfix on `PawnGroupMakerUtility.GeneratePawns` adds domestic animals to raids (not sieges)
  for factions carrying its `FactionAnimalList` mod extension: vanilla `Empire` (10% of the raid, 50% chance),
  `OutlanderCivil`/`OutlanderRough` (30%, 90%), `TribeCivil`/`TribeRough`/`TribeSavage` (20%, always), `Pirate` (20%,
  80%), and VFE Medieval, Settlers and Vikings bases (20%). Animal lists per faction type, editable through
  `AnimalHusbandryRaids_<Type>.additions/.deletions` files in the config folder. Setting "Raids use venerated animals if
  possible" (`OnlyVeneratedAnimals`) instead uses any raiding faction's primary-ideo venerated animals (20%). Animals are
  removed from raid loot generation.
- In play: factions from Medieval Overhaul, Outland, Medieval Total Rim etc. get no animals unless they inherit from a
  patched base or the venerated setting is on.
- Rimfolk today: generic: raider animals have a faction, so `NemesisTracker.IsFoe` (wild animals only) won't make them
  named beasts; `Vendettas.Killers` does credit a kill by a raider's animal to its hostile faction.
- Hook ideas: a "brave dog" tame-after-raid encounter (a downed raider hound) and a pet dilemma; let a faction's war
  animal become a beast nemesis if it maims someone (extend `IsFoe` to hostile-faction animals).
- Confidence: high (source).

## Vanilla Achievements Expanded (`vanillaexpanded.achievements`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2288125657 (GitHub: https://github.com/Vanilla-Expanded/VanillaAchievementsExpanded)
- What it adds: an achievements main tab; achievements as `AchievementsExpanded.AchievementDef` with `tracker` classes
  (`RecordEventTracker`, `KillTracker`, `IncidentTracker`, `BuildingTracker`, `ItemCraftTracker`, `QuestTracker`,
  `MentalBreakTracker`, `WealthTracker`, `SkillTracker`, ...), `points` and `texPath`; well over a hundred in the base
  game folders plus DLC folders. Rewards (`Rewards.xml`): `EnemyRaidReward` 10, `TradeCaravanReward` 15,
  `RandomEventReward` 25, `CargoPodsReward` 25, `RandomRewardQuest` 50, `RandomRewardQuestItem` 100, `ManInBlackReward` 150.
  Per save; partly retroactive; dev menu can unlock manually. Load high in the list (it is).
- In play: light flavor; rewards ask the game whether the event is possible right now.
- Rimfolk today: nothing.
- Hook ideas: ship Rimfolk achievements as a `MayRequire="vanillaexpanded.achievements"` XML file (e.g. first election,
  a tradition kept two years, a life goal achieved, a nemesis brought down) using `IncidentTracker`-style trackers or a
  small custom tracker class that reads Rimfolk's chronicle; Almanac link to the achievements tab.
- Confidence: high (source).

## Vanilla Expanded Framework (`oskarpotocki.vanillafactionsexpanded.core`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2023507013 (GitHub: https://github.com/Vanilla-Expanded/VanillaExpandedFramework)
- What it adds: library for all Vanilla Expanded mods and many others (Outland, Tasty Armory, Tavern, Outposts). 1.6
  assemblies: `VEF.dll` (modules AI, Abilities, Apparels incl. `CompSwitchApparel`/`CompApparelHediffs`, Buildings,
  Cooking, Factions, Genes, Graphics incl. `CompGraphicCustomization`, Hediffs, Maps, Memes, Plants, Research,
  Storyteller, Things, Weapons, Weathers), `KCSG.dll` (structure generation), `MVCF.dll` (multiple verbs),
  `PipeSystem.dll` (resource networks and `CompResourceProcessor`/`CompAdvancedResourceProcessor`), `Outposts.dll` (the
  outpost framework: `Outposts.Outpost`, `OutpostExtension`, `VEF_OutpostDeliverySpot`, settings), `0ModSettingsFramework.dll`.
- In play: nothing on its own.
- Rimfolk today: `Avail.IsFermenter` recognizes processor buildings (thing class name containing "ItemProcessor", or any
  comp class containing "Processor", such as PipeSystem's resource processors) whose name says barrel/ferment/brew/keg/
  cask/still, for brewing wishes and room plans; `People.OffMapColonists` reads `Outposts.Outpost.AllPawns`.
- Hook ideas: read `CompGraphicCustomization` to keep a colonist's chosen furniture style in personal touches; KCSG
  structure layouts could inspire room plans; faction extensions (`VEF.Factions`) to flavor encounters by faction.
- Confidence: high (source).
