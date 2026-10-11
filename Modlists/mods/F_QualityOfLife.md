# Batch F: quality of life, combat helpers, interface and libraries

This batch is the plumbing of the medieval modlist: mods that change how colonists haul, clean, switch weapons and
take orders (Allow Tool, Pick Up And Haul, the user's own Harvest and Haul, Common Sense, Better Pawn Control, Replace
Stuff), small combat helpers that matter a lot with bows and blades (Simple Sidearms, Melee Animation, Run and Gun,
Avoid Friendly Fire, Take Cover, Snap Out!), interface mods that put information in sight (RimHUD, the Nice tabs,
Dubs Mint menus and minimap, mood bar colors, Interaction Bubbles, quality colors, YART), save, music and loading
helpers, and the libraries everything else stands on (Harmony, HugsLib, XML Extensions, Map Mode Framework). Few of
them add content; most change friction. For Rimfolk the interesting ones are those that touch the same things the
council watches (cleaning, items left outside, research, schedules and work, mental breaks, heirlooms, social-log
talk) or that expose UI surfaces Rimfolk could plug into (RimHUD widgets, Interaction Bubbles).

Almanac: `Defs/AlmanacArticleDefs/Almanac_Mods_F_QualityOfLife.xml` (13 articles, order 605-690). Own articles:
Allow Tool, Pick Up And Haul (with Haul to Stack), Harvest and Haul, Common Sense, Better Pawn Control, Replace Stuff,
Simple Sidearms, Melee Animation. Grouped: "archers and shooters" (Run and Gun, Avoid Friendly Fire, Take Cover),
"orders, care and calm" (Better Workbench Management, I Clearly Have Enough, Cleaning Area, Move the Patient, Not My
Fault, Snap Out!), "reading your colonists" (RimHUD, Nice Health Tab, Nice Inventory Tab, Color Coded Mood Bar,
Interaction Bubbles, Injury Texture Variety, Quality Colors), "menus, maps, saves and music" (Dubs Mint Menus, Dubs
Mint Minimap, Nice Plants Menu, YART, RimSaves, Music Manager, Loading Progress), plus "the frameworks under your
mods" (Harmony, HugsLib, XML Extensions, Map Mode Framework and the other batches' libraries). Reference-only: none
(the four libraries of this batch are covered by the frameworks overview article).

Rimfolk itself uses no Harmony and patches nothing (README "Playing with other mods"), so every tie-in below is
either reading game state or optional detection by package id (`ModLister.GetActiveModWithIdentifier`).

Sources: Steam Workshop descriptions fetched through the Steam Web API (`ISteamRemoteStorage/GetPublishedFileDetails`),
GitHub sources cloned read-only (Allow Tool, Simple Sidearms, Snap Out!, Melee Animation, Harvest and Haul, Avoid
Friendly Fire About.xml, Tabula Rasa and Outland About.xml), and Rimfolk's own code (`Source/ColonyCouncil`).

---

## Libraries

## Harmony (`brrainz.harmony`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077 (source: github.com/pardeike/HarmonyRimWorld)
- What it adds: the shared runtime patching library (`0Harmony.dll`, namespace `HarmonyLib`). Nothing visible.
- In play: must be first in the list (it is, line 2 of `medieval-main.txt`, above Core). Almost every code mod in the
  list declares it as a dependency (Medieval Overhaul, VEF, Rimedieval, Hospitality, Vehicle Framework, Simple
  Sidearms, Melee Animation, Harvest and Haul...).
- Rimfolk today: nothing, deliberately. Rimfolk has no Harmony reference and no dependency; README promises "no
  Harmony, no edits to vanilla defs". Keep it that way unless a feature truly cannot be done otherwise.
- Hook ideas: none needed. If a future feature ever requires a patch, it should be an optional, separately loaded
  assembly so the "no dependencies" promise survives.
- Confidence: high (Workshop text, About.xml dependency lists of the cloned repos).

## HugsLib (`unlimitedhugs.hugslib`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=818773962
- What it adds: a shared library (mod settings handles, update news dialogs via `UpdateFeatureDef`, a quick-restart
  dev helper) plus the Log Publisher: Ctrl+F12 uploads the game log and gives a shareable link.
- In play: its author asks for it "right after Core". In the user's list it sits at line 18, after Core, the DLCs and a
  few early mods; fine in practice. Required by Allow Tool. A RimWorld of Magic declares loadAfter HugsLib.
- Rimfolk today: nothing.
- Hook ideas: Rimfolk's README asks players to share `[Rimfolk]` log lines. When HugsLib is active, the Almanac's
  troubleshooting text (or a settings-page hint) can say "press Ctrl+F12 and send the link". Detect with
  `ModLister.GetActiveModWithIdentifier("unlimitedhugs.hugslib", true)`.
- Confidence: high.

## XML Extensions (`imranfish.xmlextensions`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2574315206 (wiki: github.com/15adhami/XmlExtensions)
- What it adds: extra patch operations (conditionals, loops, math, variables), mod settings written in XML, and
  stack traces for failed XML patches.
- In play: invisible except for settings menus that other XML mods build with it. Load after Core and DLCs, before
  dependents (user's list: line 16). Its patch-error traces make the log more useful when something breaks.
- Rimfolk today: nothing.
- Hook ideas: none for gameplay. When debugging def errors from other mods in this list, its traces name the failing
  patch, useful for compatibility notes in the Almanac.
- Confidence: high.

## Map Mode Framework (`nozome.mapmodeframework`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3296654393 (community fork on Codeberg:
  ColossalFossilGames/MapModeFrameworkForked)
- What it adds: switchable world map modes plus C# utilities for other mods. Default modes: Default, Biome (dropdown),
  Temperature, Elevation, Rainfall, Growing Period, Animal Commonality, Plant Commonality, Features (landforms).
  Patches for Geological Landforms (in this list) and ReGrowth Core (in this list) so their textures follow the
  "Draw Hills" toggle.
- In play: genuinely useful for picking settlement and outpost sites (growing period, temperature). Its author built it
  for a faction-territory mod; whether Faction Territories and Vassalage (`jaeger972.factionterritories`, right below
  it in the list) uses it was not confirmed.
- Rimfolk today: nothing.
- Hook ideas: a "colony memory" world map mode would be possible (vendetta factions, where nemeses came from, where
  caravans went), but it needs C# against MMF's API (a hard or reflection-based dependency). Low priority.
- Confidence: high for features (Workshop text), low for which other mods depend on it.

---

## Work and hauling

## Allow Tool (`unlimitedhugs.allowtool`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=761421485 (source: github.com/UnlimitedHugs/RimworldAllowTool)
- What it adds: designators (`AllowTool.ThingDesignatorDef`): `AllowDesignator`, `ForbidDesignator`,
  `AllowAllDesignator`, `SelectSimilarDesignator`, `HaulUrgentlyDesignator`, `FinishOffDesignator`,
  `HarvestFullyGrownDesignator`, `StripMineDesignator`; right-click context menus that apply a tool map-wide, to the
  visible area or the home area; a Drafted hunt (party hunt) gizmo. Work types `HaulingUrgent` (shown as "Haul+") and
  `FinishingOff`; jobs `HaulUrgently`, `FinishOff`, `FinishOffPawn`; designations `HaulUrgentlyDesignation`,
  `FinishOffDesignation`. Alert "No storage space" for urgent hauls with nowhere to go. Needs HugsLib.
- In play: finishing off a *friendly* pawn calls `ThoughtUtility.GiveThoughtsForPawnExecuted(..., GenericBrutal)`;
  finishing off a prisoner records the `ExecutedPrisoner` tale; finishing off an animal increments the
  `AnimalsSlaughtered` record. Finish off can require a melee skill (setting) and unforbids drops (setting).
- Rimfolk today: nothing explicit. Side effects that are real: finished-off animals count toward Rimfolk's
  `Council_Epithet_Butcher` ("the Butcher", record `AnimalsSlaughtered`). A raider who downed or maimed a colonist and
  is finished off before Rimfolk's hourly nemesis check never becomes a nemesis; captured instead, `NemesisTracker`
  announces them as captured and gives the victim `Council_Nemesis_Captured`.
- Hook ideas: the advisor's "stacks deteriorating outside" signal (`AdvisorWorker_Deterioration`) could add a hint
  "select them and Haul urgently" when Allow Tool is active; the "wounded raider who's still breathing" dilemma could
  mention finishing off as the cold option; a quirk or dream for a colonist who finishes off many downed enemies.
- Confidence: high (source code and English keys).

## Pick Up And Haul (`mehni.pickupandhaul`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1279012058
- What it adds: haulers load several items into their inventory, carry one more, haul to storage and unload. Unload
  triggers: intervals when carrying several items, mass limit, hauling something else to a stockpile, going idle.
  Search distance grows after a long trip. "Pick up and keep" for items a pawn should carry (field medicine).
- In play: only player-owned humanlikes. Corpses need a stockpile that accepts corpses. Author: works with Allow Tool's
  Haul urgently and with Combat Extended. Makes Haul to Stack inert (see below).
- Rimfolk today: nothing. Indirect: fewer items outside means fewer `AdvisorWorker_Deterioration` signals. Note that
  `LegacyTracker.HeirloomCarriedBy` treats any heirloom *weapon* in a pawn's inventory as carried, so a hauler briefly
  carrying an heirloom weapon in their pockets would count; harmless edge case.
- Hook ideas: none needed. If Rimfolk ever counts "carried items" (gear wishes, heirlooms) it should ignore inventory
  items that PUAH is merely hauling.
- Confidence: high (Workshop text).

## Haul to Stack (`jkluch.haultostack`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=949498803 (source: github.com/jkluch/HaulToStack)
- What it adds: haulers prefer an existing stack in a stockpile; haul destinations aren't reserved, so several pawns
  can haul to one tile.
- In play: its author says that since 1.4 (maybe a late 1.3 update of PUAH), with Pick Up And Haul installed "my mod no
  longer does anything", with no harm in keeping both. The user runs both, so expect no effect. Workshop notes a
  "standing around" bug thread.
- Rimfolk today: nothing.
- Hook ideas: none.
- Confidence: high for the PUAH note (author's own words).

## Harvest and Haul (`silvanalig95.harvestandhaul`) - the user's own mod
- Link: https://github.com/silvanalig95-cmd/Rimworld-harvest-and-haul (About.xml packageId
  `silvanalig95.HarvestAndHaul`, version 1.1.0, 1.6 only, depends on Harmony). Not to be confused with a different
  Workshop mod of the same name (id 3563060965, another author, 5x5 scoop radius and an "unload" gizmo).
- What it adds: pawns put what their own work drops into their inventory and deliver it themselves. Sources (each with
  a setting): plants (harvest, chop, cut, including cutting as part of another job), mining, deep drilling, shearing
  and milking, deconstruction and floor removal, fishing (Odyssey); stone/slag chunks off by default. One JobDef:
  `HarvestAndHaul_DeliverHarvest` (driver `HarvestAndHaul.JobDriver_DeliverHarvest`, uses vanilla `Toils_Haul`).
  `HarvestAndHaul.HarvestTracker` (GameComponent, `HarvestTracker.Instance.GetLoad(pawn)` returns a `HarvestLoad`
  with `owed` ThingDef counts and the skills worked). Settings (`HarvestAndHaulSettings`): `enabled`, per-source
  switches, `fillPercent` 1.0, `minTripMass` 5 kg, `maxHoldHours` 8, `deliverBeforeNeeds` true, `includeMechanoids`
  true.
- In play: pawns keep doing the same kind of work (same relevant skill) while there is room; deliver before other work,
  eating, sleeping, recreation or idling; urgent needs, drafting, mental states, emergencies, rituals, caravans and
  player orders take priority. Delivery works even for pawns incapable of hauling. Harmony prefixes/postfixes only
  (`Plant.PlantCollected`, `Mineable.DestroyMined`, `CompDeepDrill.TryProducePortion`,
  `CompHasGatherableBodyResource.Gathered`, `JobDriver_Deconstruct.FinishedRemoving`, `JobDriver_RemoveFloor.DoEffect`,
  `FishingUtility.GetCatchesFor`, `Pawn_JobTracker.TryFindAndStartJob`/`DetermineNextJob`,
  `JobGiver_DropUnusedInventory.Drop`). Compatible with PUAH by design. With Medieval Overhaul 1.6 (source checked):
  trees yield `DankPyon_RawWood`/`DankPyon_RawDarkWood` as their `harvestedThingDef`, and MO patches `Plant_Grass`,
  `Plant_TallGrass` to yield Hay and `Plant_Bush` to yield WoodLog, so clearing ground also collects hay and wood.
- Rimfolk today: nothing. Indirect: less produce lying outside for `AdvisorWorker_Deterioration`.
- Hook ideas (both mods are the user's, so a direct soft integration is realistic):
  - Rimfolk could read `HarvestTracker.Instance.GetLoad(p)` by reflection to avoid counting a grower's pockets as "their
    gear" anywhere Rimfolk inspects inventory (heirlooms already only look at weapons).
  - Epithets: "Greenhand" (`PlantsHarvested`) and "the Delver" (`CellsMined`) are unaffected; whether H&H deliveries
    increment `ThingsHauled` ("the Mule") was not verified.
  - A dilemma or wish about a hard-working grower or miner who "always brings it all in" (trait Industrious + Plants or
    Mining passion) could be phrased around this behavior when the mod is active.
- Confidence: high (the user's source and README).

## Common Sense (`avilmask.commonsense`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1561769193
- What it adds (all optional, a few off by default): clean the room before operating a building (research, cooking,
  crafting, sleeping) and between hauling ingredients and crafting; clean after tending; prefer hauling whole
  ingredient stacks to a stockpile for bills; gather all ingredients in inventory first; prioritize items that spoil
  sooner; don't stack food with "bad" ingredients (human/insect meat) with normal food; seek outdoor recreation for the
  outdoors need; consider mood in drug policies; give sick pawns allowed social drugs or joy food; stop taking more
  sweets than they can eat; put items back in inventory if interrupted; less snow clearing while it snows or rains;
  better "can't sleep" handling; recipe screen shows the full ingredient list. Off by default: clean where they plan
  to eat, avoid bad weather when idle, random neutral ingredients in non-crafted meals, mood drifting during sleep.
- In play: safe to add or remove, but pawns can get stuck right after; draft and undraft fixes it. Same author as
  Hospitality's loadAfter `avilmask.NonUnoPinata`.
- Rimfolk today: nothing explicit. Indirect: helps the advisor's `AdvisorWorker_Kitchen` (dirty kitchen, cleanliness
  below -1.5) and `AdvisorWorker_Hygiene` (dirty hospital, nobody cleaning), and the "time outside" wish
  (`Council_Wish_Outside`, fulfilled by time under the open sky).
- Hook ideas: when Common Sense is active, the dirty-kitchen advice text could mention that cooks now clean first, so
  persistent dirt means the room is too big or too busy. Low effort, text only.
- Confidence: high (Workshop feature list).

## Better Pawn Control (`voult.betterpawncontrol`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1541460369 (source: github.com/voult2/BetterPawnControl;
  packageId `VouLT.BetterPawnControl`)
- What it adds: named policies for the Work, Schedule, Assign and Animals tabs (areas, outfits, drugs, food, work
  priorities, schedules, mech settings), an Emergency button that switches all configured policies at once, defaults
  for new colonists and prisoners. 1.6: handles maps when the gravship moves the home.
- In play: Workshop "Known issues" list "NOT compatible with Rimworld of Magic mod" (the user runs
  `torann.arimworldofmagic`), "Generally NOT compatible with mods that expand jobs (except Work Tab)", Pawnmorpher
  unsupported, ghouls unsupported. No `incompatibleWith` in About.xml.
- Rimfolk today: nothing. Real interplay: `WishWorker_DayOff` is fulfilled by at least 4 Recreation hours in the
  timetable; `WishWorker_LetMeWork` is fulfilled when a passion's work type is on, and its self-help sets priority 3.
  A later BPC policy switch can silently undo either.
- Hook ideas: when BPC is active, the granted-wish letter for a schedule or work wish could add "update your Better
  Pawn Control policy too"; decrees (drills, long hours, festival days) could suggest switching to a matching BPC
  policy; the advisor's heat-wave/cold-snap advice could mention the Emergency button.
- Confidence: high for features, medium for how the RWoM conflict shows itself (not described by the author).

## Replace Stuff (`memegoddess.replacestuff`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3526354009 (source: github.com/MemeGoddess/RimWorld-ReplaceStuff;
  original by Uuugggg, id 1372003680; Uuugggg passed away and MemeGoddess continues his mods)
- What it adds: Replace tool (Architect > Structure) and placing a building of another stuff over the old one;
  upgrades (walls, doors, beds to other beds, coolers, fueled to electric stove/tailor bench, fences, tables); the old
  building stays until the new is done (rooms stay sealed, freezers cold, bench bills kept, bed owners kept);
  build over rock (mined first) and water (bridges first); auto-rebuilt buildings remember settings (bills, cooler
  temperature, pot plant choice, storage filters); over-wall coolers; pawns don't block construction. Vanilla 1.6 only
  allows blueprints over walls, without the single job or the settings carry-over.
- In play: big for medieval wood-to-stone progression.
- Rimfolk today: nothing explicit. Rimfolk's room planner (`TemplatePlanner`) and personal touches call
  `GenConstruct.CanPlaceBlueprintAt` and, for "replace own buildings in the way", plan deconstruction plus new
  blueprints. Replace Stuff patches blueprint placement, so the planner may see more cells as placeable; untested.
- Hook ideas: the fire-risk advice (`AdvisorWorker_Fire`, "Rebuild the walls ... in stone") could, with Replace Stuff
  active, offer one click that places stone replacement blueprints over the wooden walls of the risky rooms; room
  plans could prefer in-place replacement over deconstruct-and-rebuild when Replace Stuff is active (keeps freezers
  sealed). Test planner behavior with the mod first.
- Confidence: high for features; low for the planner interplay (not tested).

## Better Workbench Management (`falconne.bwm`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=935982361
- What it adds (cut down for 1.6 now that vanilla has some features): count equipped items even when colonists are
  away; Copy All and paste for a bench's bills; Paste Link (linked bills mirror changes); bills tab opens on selecting
  a bench (toggle); "do until X" can count extra products; resume level shown; store-mode toggle per bill; drag to
  reorder; navigation arrows; Paste Into for compatible settings; default store mode setting; counts items without
  quality or hit points outside stockpiles; a bench-wide restriction.
- In play: pure bill management.
- Rimfolk today: nothing.
- Hook ideas: none significant.
- Confidence: high.

## I Clearly Have Enough! (Continued) (`mlie.iclearlyhaveenough`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2023661266 (source: github.com/emipa606/IClearlyHaveEnough;
  original by ChippedChaps, 1544107509)
- What it adds: the resource readout shown when placing blueprints, and the architect cost window, count all resources
  on the map (not minified buildings), updated every 204 ticks; changes the "not enough" message. Uses a MapComponent,
  so removing it gives a one-time load error.
- In play: blueprint placement stops complaining about materials lying unstored.
- Rimfolk today: nothing. Contrast worth knowing: Rimfolk's room planner (`RoomPlanConfig`) and floor picker
  (`Window_FloorPicker`) use `map.resourceCounter`, which counts stored resources only, so they may show "short" where
  this mod says there is enough.
- Hook ideas: optionally count map-wide resources in the planner's cost display when this mod is active, or label the
  cost as "in storage".
- Confidence: high.

## CleaningAreaTemp (`s1.cleaningareatemp`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3229108269 (by Side1iner, a temporary 1.5/1.6 build of
  CleaningArea, 870089952; there is also a separate "Cleaning Area Relaunched", 3586697885)
- What it adds: a broom button in the bottom-right controls; pick an area and cleaning is restricted to it (home area
  by default). Per-map setting. Save compatible. Not compatible with Prison Labor (not in this list).
- In play: stops cleaners wandering the whole home area.
- Rimfolk today: nothing. Interplay: `AdvisorWorker_Kitchen` and `AdvisorWorker_Hygiene` judge rooms by cleanliness,
  so a cleaning area that leaves out the kitchen or hospital will trigger those warnings.
- Hook ideas: when active, the dirty-kitchen and dirty-hospital advice could add "check that the room is inside your
  cleaning area". Reading the chosen area needs a look at the mod's MapComponent (not inspected).
- Confidence: high for features; the author offers no support.

## Move the Patient (`konstantynopolitaneczka.movethepatient`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3574898541
- What it adds: manually move any pawn (colonists, prisoners, guests, raiders, downed) between beds via the nearest
  capable colonist; medical bed priorities (Low, Normal, High, Critical) with automatic severity calculation; optional
  auto-assignment of the best bed; surgery beds; visual indicators; settings. Moving a guest or raider into a prisoner
  bed captures them (goodwill warning). Follows vanilla rules for prisoner/slave/colonist beds. 1.6; safe to add and
  remove (finish transfers first).
- In play: better hospital use; capturing downed raiders by moving them.
- Rimfolk today: nothing.
- Hook ideas: a Physician office perk could be "the physician assigns patients to the best beds" (only meaningful if
  the mod is active); Almanac hospital articles could mention surgery beds.
- Confidence: high.

## Not My Fault (`vesper.notmyfault`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2870045856
- What it adds: removes the vanilla -5 goodwill penalty when visitors die on your map tile from events you didn't
  control (animals, fire, their own mental breaks). Intentional harm still counts. Replaces the number in code.
- In play: fewer goodwill losses from visiting caravans; with Hospitality guests, fewer surprises.
- Rimfolk today: nothing. Rimfolk's strangers system raises or lowers goodwill through encounters; independent.
- Hook ideas: none.
- Confidence: high.

---

## Combat

## Simple Sidearms (`petetimessix.simplesidearms`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=927155256 (source: github.com/PeteTimesSix/SimpleSidearms;
  packageId `PeteTimesSix.SimpleSidearms`)
- What it adds: carry extra weapons in inventory and switch automatically. `CompSidearmMemory` (ThingComp on pawns:
  `rememberedWeapons` list of `ThingDefStuffDefPair`, forced/preferred weapon fields, `primaryWeaponMode`);
  `Gizmo_SidearmsList`; jobs `EquipSecondary`, `EquipSecondaryCombat`, `ReequipSecondary...`; `JobGiver_RetrieveWeapon`;
  alerts `Alert_MissingSidearm` ("Colonist missing sidearm") and missing primary. Settings presets from "inventory
  extension only" to "bring all of it", limits by slots or mass (absolute or relative), separate melee/ranged rules,
  auto-switch to melee when attacked in melee, range-appropriate ranged switching, tool auto-switch for jobs, NPC
  sidearm spawn chance and budget, a neolithic extension for tribals.
- In play: archers with knives; pawns may drop or retrieve weapons depending on DropMode.
- Rimfolk today: yes. `LegacyTracker.HeirloomCarriedBy` (Source/ColonyCouncil/Lives/Legacy.cs) counts an heirloom
  weapon in the inventory as carried, explicitly for Simple Sidearms; README mentions it.
- Hook ideas: the "a decent weapon" and "proper armor" gear wishes could count a remembered sidearm; a quirk "never
  without their knife" for someone saved by a sidearm switch; heirloom loss logic could check `rememberedWeapons` so a
  temporarily dropped heirloom sidearm (to be retrieved) doesn't count as lost.
- Confidence: high (source).

## Melee Animation (`co.uk.epicguru.meleeanimation`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2944488802 (source: github.com/Epicguru/AnimationMod)
- What it adds: animated melee attacks; executions (death, down or injury from skill, armor, pen, damage type);
  duels decided by `AM_DuelAbility`, the winner executes the loser; lassos (`AM_LassoCloth`, `AM_LassoDevilstrand`,
  `AM_LassoHyperwave`, worn in an `AM_Hip` slot, made at a tailor bench); unique skills (e.g. needing psylink and
  melee levels); friendly duels at a duel spot (`AM_DuelSpot`, Misc tab) as recreation and training: joy givers
  `AM_DoFriendlyDuelJoyGiver`, `AM_SpectateFriendlyDuelJoyGiver`, joy kind `AM_FriendlyDuelJoyKind`, thoughts
  `AM_FriendlyDuel_Win` (+4) and `AM_FriendlyDuel_Lose` (+3). Stats: `AM_Lethality`, `AM_DuelAbility`,
  `AM_GrappleRadius`, `AM_GrappleSpeed`, `AM_GrappleCooldown`, `AM_ExecutionCooldown`. Jobs `AM_DoFriendlyDuel`,
  `AM_SpectateFriendlyDuel`, `AM_GrapplePawn`, `AM_WalkToExecution`. Assign tab columns for lasso/execution use.
- In play: weapons need tweak data. The repo's compatible list includes Medieval Overhaul (80 weapons), its Barbarians
  submod, RimFantasy MO edition and House Doyle, Outland factions, A RimWorld of Magic, Medieval Fantasy Quest Rewards,
  Divine Order, VTE and others in this list. Not found in that list: Medieval Total Rim armory (`athelas.*`), the al9000
  packs (Fire and Steel, Shining Armor, High Medieval, Sengoku), and the continued Elves package id
  (`devdesigner.elves.continued`; the list has `devdesigner.elves`). Not compatible with Multiplayer; soft issue with
  dual wield; recommends Simple Sidearms.
- Rimfolk today: nothing. `Council_Wish_TrainingDummy` ("somewhere to train") matches building names containing
  dummy/archery/practice target/training target/marksmanship, so a duel spot doesn't fulfil it.
- Hook ideas (promising):
  - Add "duel spot" to the training wish's accepted names (or a defName check for `AM_DuelSpot`) for Melee-passion
    fighters.
  - Rivals' "contest" social event could become a friendly duel at the duel spot when one exists; the winner gains
    standing, a rivalry may soften (Social web).
  - Nemesis: when a colonist wins a duel against their nemesis, use the duel as the "settled the score" moment; an
    epithet for duel wins ("the Duelist").
  - Dreams and quirks after being executed-at but surviving, or after a first execution.
- Confidence: high (source, Workshop text).

## RunAndGun - Continued (`memegoddess.runandgun`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3562365100 (source: github.com/MemeGoddess/RunAndGun;
  original by roolo, 1204108550)
- What it adds: a per-pawn toggle for drafted pawns holding a ranged weapon to shoot while moving; accuracy penalty
  while moving, movement penalty while shooting/cooling down (heavier weapons slower); NPCs run and gun too (also
  while fleeing, chance configurable); weapons can be excluded; fixes the old "instant melee" bug.
- In play: needs fire at will; manual targeting stops the pawn. Confirmed compatible with Avoid Friendly Fire 1.6.
  The original page says it works well with Giddy-up battle mounts (Giddy-up is in this list as `memegoddess.giddyup`;
  not verified for that version).
- Rimfolk today: nothing.
- Hook ideas: none.
- Confidence: high.

## Avoid Friendly Fire - 1.6 (`falconne.aff`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3532871145 (fork by christophkogler,
  github.com/christophkogler/AvoidFriendlyFire; About.xml keeps packageId `falconne.AFF`; original by falconne,
  1134165362)
- What it adds: an "Avoid Friendly Fire" toggle per colonist (on by default); shots whose path, including miss radius,
  could hit a friendly or neutral pawn are not taken; drafted shooters seek a clean target or wait, hunters
  reposition; blocked shooters' names in cyan, the blocking friend in green; forced shots show "Cannot hit target";
  shield belt wearers above 10% energy are ignored; option to re-enable the toggle on undraft; a faster
  "capsule-only" mode.
- In play: archers straight behind a melee line will often hold fire; flank them. Performance cost with many pawns
  (the fork claims big optimizations).
- Rimfolk today: nothing.
- Hook ideas: none. (Friendly-fire injuries are rarer with it, so any future "shot by a friend" dilemma would fire
  less often.)
- Confidence: high (fork's About.xml and Workshop text).

## TakeCover (`rabiosus.takecover`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3749200746 (by Rabiosus, 1.6)
- What it adds: select drafted pawns, hold Ctrl, right-drag toward a threat; pawns spread into firing positions
  preferring nearby cover and valid shooting lines.
- In play: a formation shortcut. The Workshop text is two lines; nothing else is documented.
- Rimfolk today: nothing.
- Hook ideas: none.
- Confidence: medium (very short description).

## Snap Out! (`weilbyte.snapout`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1319782555 (source: github.com/Weilbyte/SnapOut, 1.6
  build in `dist/1.6`)
- What it adds: `SnapOut_CalmPawnDownWorkgiver` (work type Warden, needs Manipulation and Talking), job
  `SnapOut_CalmDownJob`, interaction `SnapOut_CalmDownInteraction` (logged to the social log, Social XP 30), thought
  `SnapOut_HelpedSomeoneThought` (+3, half a day), follow-up jobs `SnapOut_GoToSafetyJob` (a 65% chance to send a pawn
  with a bedroom there), `SnapOut_SnappingOutJob`, `SnapOut_ActuallyRecoverJob`. Chance = base + negotiation ability
  (capped) x weight + opinion of the calmer x weight, with an extra penalty for negative opinion. Excluded states:
  RunWild, PanicFlee, HuntingTrip, SocialFighting, Fighter, Giggling, Crying. Settings: calm aggressive pawns,
  prisoners, traders, cooldown on failure, catharsis toggle. Patches `Wander_Sad` to not be colonists-only.
- In play: a sociable warden talks people out of breaks; failed attempts on aggressive pawns can get the calmer hit.
- Rimfolk today: nothing explicit. Real interplay: on Demanding stakes `CouncilDynamics.MaybeSulk` starts
  `InsultingSpree` or `Wander_Sad`, and some dilemma/wish effects start mental states
  (`DilemmaEffect_MentalState`, `WishEffect_MentalState`); Snap Out! can calm these (aggressive ones only if enabled).
- Hook ideas (promising): treat a successful calm-down as a social-web event (friendship gain, "talked me down" memory
  in the journal); calming a bitter colonist who announced they're leaving could count toward winning them back; an
  epithet ("the Calm") or a life goal ("talk five people down"). Detection: watch the play log for
  `SnapOut_CalmDownInteraction` or the `SnapOut_HelpedSomeoneThought` memory.
- Confidence: high (source).

---

## Interface and information

## RimHUD (`jaxe.rimhud`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1508850027 (wiki: github.com/Jaxe-Dev/RimHUD/wiki)
- What it adds: detailed pawn info in the inspect pane (resizable) or a floating dockable window; warnings for
  life-threatening conditions, wounds needing tending, unstable minds; shows modded needs and skills; options from a
  gear icon on hover; food restrictions for prisoners and guests. Multiplayer ready. Designed for 1600x900 and up.
- In play: safe to add or remove anytime.
- Rimfolk today: nothing. Vanilla Skills Expanded declares loadAfter `jaxe.rimhud`.
- Hook ideas (promising): RimHUD "supports integrated layout presets and custom widgets from your mod" (wiki). A
  Rimfolk widget could show trust level, the open wish, office and life goal progress right in the HUD. Needs a look
  at the wiki's integration API (likely reflection or a small XML/registration file).
- Confidence: high for features, medium for the integration mechanism.

## Nice Health Tab (`andromeda.nicehealthtab`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3328729902
- What it adds: redesigned health tab with a body doll (male, female, fat, hulk, child) showing injuries, implants,
  missing parts; grouped injuries; right-click a body part or entry to create an operation; XML add-on system for
  xenotypes and races; partial HAR support; animals and mechs get the vanilla tab; a toggle to Compact Hediffs.
- In play: medical overview at a glance.
- Rimfolk today: nothing. Rimfolk's trust effects show as health-tab conditions (README); how this tab groups them was
  not checked.
- Hook ideas: none; check that Rimfolk's trust hediffs display sensibly in it.
- Confidence: high for features.

## Nice Inventory Tab (`andromeda.niceinventorytab`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3609897594
- What it adds: redesigned gear tab: stat bars with hover breakdowns and penalty/boost indicators, optimal-distance
  calculation, apparel layer slots showing what still fits, right-click to change clothes, create a crafting bill
  that auto-equips on completion, warnings for unsuitable apparel (nudists, corpse clothing, ranged with shields), dev
  spawn. CE add-on exists. Not compatible with RPG inventory.
- In play: gearing up for winter and war.
- Rimfolk today: nothing. Gear wishes are fulfilled by what the pawn wears (a hat or helmet, clothing with strong cold
  insulation, excellent-or-better quality, jewelry, armor with enough sharp armor), so the tab's queue-a-bill-and-equip
  helps fulfil them.
- Hook ideas: none needed.
- Confidence: high.

## CM Color Coded Mood Bar [1.1+] (`crashm.colorcodedmoodbar.11`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2006605356
- What it adds: colonist-bar mood colors (red extreme break, orange major, yellow minor, gray neutral, light blue
  content, green happy), break box outline, optional schedule box, icons for illness, pregnancy, bleeding (4 sizes by
  time to death), hypothermia/heatstroke, gender, cryptosleep, lovers/married. Replaces a vanilla colonist bar method.
- In play: spot break risk at a glance. Only conflicts with other colonist-bar mood renderers.
- Rimfolk today: nothing. Rimfolk moods carry their cause in the tooltip ("Why am I feeling this?").
- Hook ideas: none.
- Confidence: high.

## Interaction Bubbles (`jaxe.bubbles`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1516158345
- What it adds: social interactions from the play log shown as speech bubbles above pawns; fade with game time; hover
  makes them transparent and click-through; a toggle in the play settings.
- In play: makes the social log visible.
- Rimfolk today: yes, indirectly and by design. Rimfolk's conversations are InteractionDefs that go to the social
  log: `Council_Talk_*` (wish talk), `Council_Talk_Ambition`, `Council_Gossip_Bitter/Devoted`,
  `Council_Decree_Gripe/Argue`, `Council_DreamTalk_*`, council meeting interactions (`CouncilMeeting`). README
  recommends Bubbles. Separately, `CouncilGameComponent.Speak` throws floating text motes (setting `speechText`,
  "Colonists speak (floating text)"), which can double up with bubbles.
- Hook ideas (promising): route more Rimfolk moments (encounters, eulogy lines, petitions, dilemma reactions) through
  InteractionDefs or `PlayLogEntry_Interaction` so they appear as bubbles; when `jaxe.bubbles` is active, default
  `speechText` to off on first load or show a hint in settings.
- Confidence: high.

## Injury Texture Variety (`keshash.injurytextures`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3561610873 (by Keshash)
- What it adds: replaces vanilla's 3 human injury overlays with about 93 new ones (bite, bruise, burn, chemical
  burn, electric burn, crack, cut, fragment, frostbite, stab, gunshot; CE blast and venom arrow). No blood color
  support.
- In play: visual only.
- Rimfolk today: nothing.
- Hook ideas: none.
- Confidence: high.

## Quality Colors (Continued) (`dawnsglow.qualcolor`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3513846773 (by DawnsGlow; update of Quality Colors
  2420141361 via a 1.5 fork; code by legodude17)
- What it adds: item labels colored by quality in the trade window; color in the gear tab when forced or locked; in
  the mouseover readout; on durability percentages in the inspect pane and info card; presets (Default, Legacy, RPG
  Inventory, WoW, Colorblind, FF14) and a color picker; caching for performance.
- In play: visual only.
- Rimfolk today: nothing. Interplay: the "something fine" wish wants clothing of excellent quality or better; the
  trader wish wants a specific item from a visiting trader.
- Hook ideas: none.
- Confidence: high.

## Dubs Mint Menus (`dubwise.dubsmintmenus`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1446523594
- What it adds: searchable build menu (grid or list, sort by any stat, pin to compare, hotkey or default architect
  tab), the Mint wheel (radial designators), up to 10 designator bars, searchable bills (clone a bench's bill stack,
  global bill menu), a research menu (search by project, mod, tech level or unlocks; queue handling techprints,
  Anomaly studies etc.), gear drag and drop, plant menu, health bills search, bed and building assignment menu, pinned
  resources. Every menu can be switched off.
- In play: essential with 200+ mods. Its research menu overlaps with YART (YART supports only one research tree mod).
- Rimfolk today: nothing. The advisor's idle-research signal (`AdvisorWorker_Research`) reads
  `Find.ResearchManager.GetProject()`.
- Hook ideas: none.
- Confidence: high.

## Dubs Mint Minimap (`dubwise.dubsmintminimap`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1662119905 (issues: github.com/Dubwise56/Dubs-Mint-Minimap)
- What it adds: draggable minimap with markers (colonists, downed pulse, prisoners, animals, wildlife, predators,
  hostiles by faction, fires, projectiles, drop pods), attack pings, spyglass highlight of same-type things, anchor,
  coordinates, fog of war respected, modes (free, comms console, Rimatomics TACS), multiplayer ping tool.
- In play: navigation on big maps.
- Rimfolk today: nothing.
- Hook ideas: none.
- Confidence: high.

## Nice Plants Menu (`andromeda.niceplantsmenu`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3685058533
- What it adds: plant selection summary: will it mature before first frost, approximate maturity time, yield and
  profit, how many colonists the harvest roughly feeds (as simple meals), whether pen grass feeds the animals and how
  many more cells are needed, cave/indoor-light/polluted-soil suitability. A "Custom Zone Fix" add-on exists.
- In play: crop planning, complements the Almanac's growing-season articles.
- Rimfolk today: nothing. The advisor also forecasts food against winter and warns about ripe crops before frost
  (`AdvisorWorker_Food`, `AdvisorWorker_Crops`).
- Hook ideas: none.
- Confidence: high.

## Yet Another Research Tree (YART) (`seohyeon.yart`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3745434121 (by SeoHyeon, 1.6). Note: the batch brief
  called this "Yet Another Relations Tab"; it is a research tree.
- What it adds: replaces the research tree: era bands (Neolithic, Medieval, Industrial, Spacer...), per-tab or unified
  view, research queue, parallel research channels (Anomaly, Vanilla Gravship Expanded), fast suffix-array search by
  mod, item, plant or name, colorblind support, Multiplayer adaptor; supports World Tech Level and Semi Random
  Research; only one research tree mod at a time.
- In play: big help with hundreds of modded projects; pair with Rimedieval's tech limits.
- Rimfolk today: nothing. `AdvisorWorker_Research` fires "The research bench is idle" when no project is selected and
  suggests the cheapest in-era project (`SuggestNext`); a YART queue keeps a project running.
- Hook ideas: when YART is active, the idle-research advice could say "add it to your research queue".
- Confidence: high.

## RimSaves (`arandomkiwi.rimsaves`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1713367505
- What it adds: save menu with previews (map screenshot and metadata), folders (autosaves go to the selected folder),
  sorting, duplicate/rename, quicksave Ctrl+Home and quickload Ctrl+End (or F5/F9 after unbinding vanilla keys).
- In play: if the menu doesn't appear, the author suggests moving RimSaves up near Core or HugsLib (the user has it at
  line 210). Remember to re-select the colony's folder after browsing others, or autosaves land in the wrong folder.
- Rimfolk today: nothing.
- Hook ideas: none.
- Confidence: high.

## Music Manager (Continued) (`zal.musicmanager`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3220120457 (continuation of Fluffy's Music Manager,
  2229205672)
- What it adds: a music player widget (play/pause, next; "previous" restarts the current song for now), a song list to
  play any track or change when songs may play, interval sliders. Not fully compatible with Anomaly's music sequences
  yet; an update merging several add-ons was announced.
- In play: cosmetic.
- Rimfolk today: nothing.
- Hook ideas: none.
- Confidence: medium-high (the package id `zal.` matches the continuation's author account by inference).

## Loading Progress (`ilyvion.loadingprogress`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3535481557
- What it adds: a loading window listing each startup stage with heap and process memory; also during world and map
  generation, loading saves, settling and map mesh regeneration; optional load-time impact tracking per mod (off by
  default; click the load time text on the main menu; HTML export; history). Incompatible with RimThemes and Startup
  Impact Continued (neither in this list). A "patching of initialization code" setting is the first thing to disable
  if problems appear.
- In play: placed second in the user's list, right after Harmony.
- Rimfolk today: nothing.
- Hook ideas: none; useful for measuring Rimfolk's own startup cost.
- Confidence: high.

---

## Notes for the frameworks article (`Council_AlmMod_F_Frameworks`)

The article lists eleven ids. Verified while writing it:
- `oskarpotocki.vanillafactionsexpanded.core` is Vanilla Expanded Framework (About.xml in the cloned repo). Required by
  Medieval Overhaul (with `syrchalis.processor.framework`), Vanilla Outposts Expanded, Vanilla Skills Expanded, Alpha
  Skills, Outland Core and the VFE furniture add-ons.
- `vanillaexpanded.vfecore` is **Vanilla Furniture Expanded**, a content mod, not a library (About.xml on
  github.com/Vanilla-Expanded/VanillaFurnitureExpanded). The VFE add-ons (Art, Farming, Production, Security) depend on
  VEF, not on it.
- `vanillaexpanded.vcef` is **Vanilla Fishing Expanded**, a content mod (About.xml on
  github.com/Vanilla-Expanded/VanillaFishingExpanded). Medieval Overhaul's processors reference its fish categories
  with `MayRequire`; MO's 1.6 compatibility folder for it loads only when Odyssey is NOT active, so in this modlist
  (Odyssey on) that folder is skipped. Vanilla Outposts' fishing folder exists only for 1.4/1.5.
- `neronix17.toolbox` is **Tabula Rasa** (formerly "[O21] Toolbox", github.com/Outpost-21/Tabula-Rasa), required by
  Outland Core together with VEF.
- `ebsg.framework` is EBSG Framework (Workshop 3112549163): shared C# from the Expanded Biotech Style Genes series and
  more. Which mods in this list need it was not checked. (An untracked copy of its source currently sits in
  `Source/ColonyCouncil/EBSG/`; it is not Rimfolk code and was not touched by this batch.)
- `syrchalis.processor.framework` is [SYR] Processor Framework (Continued) (3210544395): processors such as barrels; it
  upgrades RimBees and some VFE mods to its system; "load early".
- `smashphil.vehicleframework` is Vehicle Framework (3014915404): no vehicles of its own; required by Medieval
  Overhaul: Air Vehicles (`gayuki.medievaloverhaulairvehicles`, hot air balloon and airship).

## The five most promising Rimfolk hooks from this batch

1. **Interaction Bubbles**: route more Rimfolk moments through InteractionDefs so they appear as bubbles, and switch
   Rimfolk's own floating text off by default when `jaxe.bubbles` is active.
2. **RimHUD widget**: RimHUD accepts custom widgets and layout presets from other mods; show trust, the open wish,
   office and life goal in the HUD.
3. **Melee Animation duels**: count `AM_DuelSpot` for the "somewhere to train" wish; turn rival contests into friendly
   duels; use a duel win over a nemesis as "settled the score"; a duelist epithet.
4. **Snap Out! calm-downs as social events**: a successful `SnapOut_CalmDownInteraction` builds a bond and a journal
   line; talking down a bitter colonist who announced they're leaving could count toward winning them back.
5. **Replace Stuff for the fire-risk advice and room plans**: one click to place stone replacement blueprints over the
   wooden walls the advisor flags; prefer in-place replacement in room plans so rooms stay sealed (test first).

Runner-up: Better Pawn Control awareness for schedule and work wishes (a granted "day off" can be undone by a policy
switch), and HugsLib's Ctrl+F12 log upload in Rimfolk's troubleshooting text.
