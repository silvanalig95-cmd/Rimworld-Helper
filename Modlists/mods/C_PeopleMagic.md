# Batch C: people, races and magic (`Modlists/medieval-main.txt`)

This batch is about who the colonists are and what they can do: new kinds of people (the ageless elves of Elves
(Continued), Det's Stoneborn dwarves, Goji's merfolk "Merren", Awakened Apes and the gene soup of Alpha Genes), two
magic systems (A RimWorld of Magic's classes and Vanilla Psycasts Expanded's psycaster paths), Ideology add-ons (Alpha
Memes, Memes and Structures, Splits and Schisms, icons and relics), the trait and skill layer (Vanilla Traits
Expanded, RimTraits, People Can Change, Vanilla Skills Expanded with Alpha Skills, Level Up!), social life (Vanilla
Social Interactions Expanded), a medieval go-juice, small readability and looks mods, and two libraries (EBSG
Framework, JecsLite for RWoM). For Rimfolk this is the richest batch for "who are these people" hooks: class traits,
passions, expertise, xenotype genes and ideoligion memes are all things the council could read. What Rimfolk does
today: `People.HasInnerLife` skips mutants and RWoM's raised undead (`Undead` trait, `TM_UndeadHD` hediff);
`People.PassionLevel` judges modded passions by `SkillRecord.LearnRateFactor(true)` (under 0.9 = none, 1.25 or more =
burning), cached per passion value; `Flavor.HumanAge` scales biological age by `RaceProps.lifeExpectancy` (alien
races only; xenotypes use the human 80). Everything else in the batch is untouched by Rimfolk's code.

Sources: Steam Workshop pages (fetched through the Steam API), the RimSort Steam Workshop database (package id to
Workshop id), mod GitHub repos cloned into the scratchpad (Elves, Stoneborn, StagzMerfolk, RWoM, VPE, Alpha Genes,
Alpha Memes, VGE, VIE Memes, VIE Icons, VIE Relics, VTE, VSE, Alpha Skills, VSIE, VHE, Level Up, EBSG, VRE Custom
Icons), and a decompile of `VSE.dll` for the passion learn-rate code. Rimfolk code checked: `People.cs`, `Flavor.cs`,
`Wishes/WishWorkers_*.cs`, `CouncilDynamics.cs`, `Secrets/*.cs`, nemesis code, `Defs/*`.

## Notable finding for Rimfolk (bug candidate)

`People.PassionLevel` caches the level per passion value, from the first pawn it sees. With Vanilla Skills Expanded,
`LearnRateFactor(true)` is not just the passion's own factor: `VSE.Passions.LearnRateFactorCache.GetValueFor`
multiplies it by the `learnRateFactorOther` of every other skill's passion when the VSE setting
`CriticalEffectPassions` is on (it is on by default) or the passion is `isBad`. A critical passion has
`learnRateFactorOther` 0.25, Alpha Skills' obsessive 0.9 and synergistic 1.1. So if the first pawn Rimfolk checks with,
say, a natural passion (2.0) also has a critical passion elsewhere, natural reads 0.5 and is cached as "no passion"
for everyone until the game restarts. Fix idea: read the passion's own `learnRateFactor` (and `isBad`) from
`VSE.Passions.PassionManager.PassionToDef(passion)` by reflection, or cache per pawn and skill, or skip the cache.
Also note `isBad` passions with a normal rate (Alpha Skills' traumatic, rate 1.0) count as "interested" today.

---

## ↁ Elves (Continued) (`devdesigner.elves.continued`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3272603920 (GitHub: https://github.com/MisterM-1951/Elves-Continued-).
  Not to be confused with "Lord of the Rims - Elves" (a HAR race by Jecrell, continued by others), which is a different mod.
- What it adds: Biotech xenotypes `DevDesigner_XenotypeWoodElves`, `DevDesigner_XenotypeDarkElves`,
  `DevDesigner_XenotypeHighElves` (all from abstract `DevDesigner_Elves`: Beauty_Pretty, long hair only, no beard,
  pale/white skin, `DevDesigner_DryadEars`, `DevDesigner_Ageless`). Dark elves add Aggression_Aggressive; wood and
  high elves MinTemp_SmallIncrease. `DevDesigner_Ageless` uses `biologicalAgeTickFactorFromAgeCurve` (13:1, 18:0.1,
  30:0), so biological age slows from 13 and stops at 30 (gene hidden via VEF GeneExtension). Six medieval NPC
  factions: `DevDesigner_WoodElvesFaction`, `DevDesigner_DarkElvesFaction`, `DevDesigner_HighElvesFaction` (not
  permanent enemies) and `DevDesigner_SavageWoodElvesFaction`, `DevDesigner_RoughDarkElvesFaction`,
  `DevDesigner_RoughHighElvesFaction` (natural enemies); base `requiredCountAtGameStart` 1, tribal backstories,
  `canSiege` false. Player factions: wood elf clan, high elf colony, dark elf coven; scenario `BaseWoodElvesScenario`.
  Research: `DevDesigner_ElvenEquipment_WoodElves/DarkElves/HighElves` (medieval, 700 base each) gate each house's
  weapons and armor (bows, greatbows, crossbow, swords, halberd, shields, quivers; named sets such as stag knight,
  bladedancer, black guard, dragon prince, white lion). Animals: `DevDesigner_AncientDeer` (wool, milk) and
  `DevDesigner_ColdOne` (egg-laying reptile, venom fangs). Six tattoos. Needs Biotech, VEF, XML Extensions.
- In play: elves are humans with a gene set, so everything human applies (romance, genes, Xenotype Summary).
  Savage/rough elves raid. Giddy-up patches target the old mod name "Giddy-up! Core"; whether they apply under
  `memegoddess.giddyup` is unverified. Combat Extended folder exists (not used here).
- Rimfolk today: nothing specific. Because Rimfolk uses biological age, ageless elves never get "old" lines
  (`HumanAge >= 60`) and stay eligible for the Biotech "have a child" life goal (`HumanAge < 45`).
- Hook ideas: encounters with elven visitors keyed on `DevDesigner_*Faction` (gift exchange, a hunt for the ancient
  deer); a dilemma when a savage elf prisoner is taken; wish "elven gear" once the house research is done; a quirk or
  dream for long-lived colonists outliving friends (check `DevDesigner_Ageless` gene); a life goal "see a
  hundred winters" for ageless pawns; almanac link from the bestiary to the Cold One.
- Confidence: high (repo defs + Workshop page).

## Det's Xenotypes - Stoneborn (`det.stoneborn`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2888722722 (GitHub: https://github.com/DetVisor/StonebornXenotype)
- What it adds: xenotype `Stoneborn` with MoveSpeed_Slow, small temperature range shifts, Nearsighted, Beard_Always,
  AptitudeTerrible_Social, AptitudePoor_Animals, AptitudeStrong_Mining, AptitudeStrong_Construction,
  AptitudeRemarkable_Crafting, Robust, PsychicAbility_Deaf, Libido_Low and custom genes: `DV_Stonecall` (ability
  `DV_Stonecall`: hediff `DV_StonecallBuff`, +0.3 ConstructionSpeed and MiningSpeed offsets for 35,000 ticks, cooldown
  360,000 ticks = 6 days), `DV_LifespanLong` (LifespanFactor 1.5), `DV_Undergrounder` (forces the vanilla
  Undergrounder trait), `DV_Aggression_Irascible` (socialFightChanceFactor 1.5), `DV_MightyLiver` (cirrhosis
  immunity), `DV_ExtendedMemory` (skill loss x0.55, thought durations x1.25), `DV_BushyEyebrows`. Faction
  `OutlanderRoughStoneborn` "stoneborn clan": dwarves, leader title "underlord", Rustican culture, requires the
  Tunneler meme with Ideology, `permanentEnemyToEveryoneExceptPlayer` true (neutral start with you, at war with all
  other factions). A different faction file loads if `det.stonebornfaction` is active (not in this list).
- In play: superb miners, builders and crafters; poor at social work and animals; slow. Companion mods in this list:
  `det.dwarvenstyle` (Stoneborn - Dwarven Style Pack) and `det.sbdelights` (Stoneborn - Delights), covered by other batches.
- Rimfolk today: indirect. The forced Undergrounder trait means `WishWorker_Outside` never fires for them, the
  "harvest push" decree (`Council_Decree_Harvest`) lists Undergrounder as Hate, and the mountain life goal
  (`Council_Asp2_Mountain`, needs a Mining passion) has an Undergrounder line. `Flavor.HumanAge` ignores the
  LifespanFactor stat, so a 70-year-old stoneborn gets "old" flavor although their lifespan is 1.5x.
- Hook ideas: scale `HumanAge` by the pawn's `LifespanFactor` stat (covers every long-life gene); a wish for a forge or
  a mountain hall from stoneborn; Stonecall used as a "work frenzy" granted-wish reward; irascible gene as a rivalry
  accelerant in the social web; stoneborn clan encounters (trading ale, a feud with another faction they hate).
- Confidence: high (repo defs + Workshop).

## Goji's Fantasy Race: Merren (`arquebus.stagzmerfolk`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3026235711 (GitHub: https://github.com/LinnielDW/StagzMerfolk).
  "Stagz Merfolk" is the project's internal name; the package id belongs to this mod (About.xml checked).
- What it adds: xenotypes `Stagz_Merfolk_Coastal` (siren song, webbed hands, rain veil, fish tail, swift swim, psychic
  bonding, `Stagz_Charming`, AptitudeStrong_Animals, Pretty, `Stagz_Nimbleness`, terrible mining and cooking) and
  `Stagz_Merfolk_Abyssal` (siren song, `Stagz_Raincaller`, DarkVision, Nearsighted, Aggressive, UVSensitivity_Intense,
  PsychicAbility_Extreme). Gene `Stagz_Aquatic` adds need `Stagz_NeedAquatic` ("aquatic hydration": refilled by rain,
  marsh, rivers; vacuum resistance slows the fall; at zero hediff `Stagz_Dehydration` can kill), with job giver
  `StagzMerfolk.JobGiver_SeekHydration` patched into the think tree and work giver `Stagz_HydrateAquaticWorkGiver`.
  Abilities `Stagz_SirenCall` (psychic song vs hostiles, scales with psychic sensitivity and hearing) and
  `Stagz_SirenCallCharm` ("charm person": recruits a human or animal). Charm outcomes (`StagzMerfolk.CharmOutcomeDef`,
  equal weights): `Stagz_Trustworthy`, `Stagz_Depart` (leaves after 7 to 15 days), `Stagz_Aggressive` (turns
  hostile after 7 to 15 days, also while imprisoned). Joiner incidents run from a storyteller comp:
  `Stagz_ArielSummoned` (a wayward merren rescues a colonist lying wounded on a shore; letter lets you accept or send
  away) and `Stagz_VirtuosoSummoned` (Royalty: a merren drawn by a colonist's recital). Meditation focus
  `Stagz_Water`, building `Stagz_WaterCandle` (floating lantern, wood-fueled), squid dryad with Ideology,
  waterborne plants with VE Plants More (`vanillaexpanded.vplantsemore` is in this list), dye to recolor tails,
  Dubs Bad Hygiene synergy (bathing refills hydration) that is off by default in settings. Thirteen backstories
  (for example "seashell trader", "pearl diver", "shaman of the sea").
- In play: merren must live near water or rain; vanilla pathing still treats water as costly, so they walk out of it
  (author's FAQ). Charm is powerful but two thirds of charmed people eventually leave or turn on you.
- Rimfolk today: nothing specific. Merren are humans, so full Rimfolk people. The "seashell trader" backstory contains
  "trader", a word that makes the pirate's-debt secret likelier.
- Hook ideas: a dilemma or secret around a charm-recruited colonist ("the song is fading": release them before the
  outcome triggers); a wish for a pond, river access or a rain dance; dreams of the sea for merren; an encounter when
  a wayward merren arrives (Rimfolk's "visitor who wants to stay" pattern); a MoodHintDef for `Stagz_DehydrationThought`.
- Confidence: high (repo defs + Workshop).

## Awakened Apes (`onlymonkey.awakenedapes`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3646217940 (community update by JonRonStein; credits
  "Original: OnlyMonkey"; earlier version "Monkey Xenotypes" https://steamcommunity.com/sharedfiles/filedetails/?id=3566230987)
- What it adds (from the Workshop text; no repo found): five playable ape xenotypes. Gorillas (strong gentle haulers,
  herbivore, peaceful until provoked), chimpanzees (fast learners, deadly shooters, violent), bonobos (kind, bisexual
  peacekeepers), orangutans (genius artisans, psychic sensitivity, little sleep), monkeys (small, anxious
  sharpshooters, good at plants and combat). All speak broken English (social impact halved) and breed only within
  their species. Needs Biotech; VEF optional (body size); not compatible with Combat Extended. The older version says
  its genes came from DetVisor's mods (Det, Feldoh, Alite, SmartKar).
- In play: specialists rather than all-rounders; keep them off social jobs.
- Rimfolk today: nothing specific; they are humanlike with moods, so full Rimfolk people.
- Hook ideas: species-specific wish flavor (gorillas want fodder fields, orangutans a workshop); the halved social
  impact could lower their pull in council votes or in persuading petitions; bonobo "peacekeeper" could make rivalries
  around them soften faster in the social web.
- Confidence: medium (Workshop text only; package id matched by name and credited author, not by About.xml).

## A RimWorld of Magic (`torann.arimworldofmagic`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1201382956 (GitHub: https://github.com/TorannD/RWoM;
  wiki: https://rwom.fandom.com/wiki/RimWorld_of_Magic_Wiki)
- What it adds: classes as traits. Mage classes (need `TM_Mana`, hediff `TM_MagicUserHD`): InnerFire (fire mage),
  HeartOfFrost, StormBorn, Arcanist, Druid, Paladin, Summoner, Necromancer, Lich, Priest, TM_Bard, Succubus,
  Warlock, Geomancer, Technomancer, BloodMage, Enchanter, Chronomancer, ChaosMage, TM_TheShadow, TM_Brightmage,
  TM_Shaman, TM_Golemancer, TM_Empath, TM_Wanderer. Fighter classes (need `TM_Stamina`, `TM_MightUserHD`):
  Gladiator, Ranger, TM_Sniper, Bladedancer, Faceless, TM_Psionic, DeathKnight, TM_Monk, TM_Commander,
  TM_SuperSoldier, TM_Apothecary, TM_Wayfarer. Latent traits `TM_Gifted` ("magically gifted") and `PhysicalProdigy`
  ("physically adept") let a pawn learn a class from an item: arcane tomes (`BookOfInnerFire` "Arcane Tome: Fire" ...)
  and combat manuals (`BookOfRanger` "Combat Style: Ranger" ...). Other traits: `Undead`, `TM_Possessed`,
  `TM_Possessor` (spirit), `TM_ManaWellTD`, `TM_ArcaneConduitTD`, `TM_FaeBloodTD`, `TM_GiantsBloodTD`, `TM_CursedTD`.
  Magic research tab (26 projects; all neolithic or medieval except `TM_MechaGolemRD` and its attachments, which are
  industrial): arcane fundamentals, arcane facilities, magic circle, portals, mana storage, arcane forge, enchanting,
  scribing, golem theory and golems. Buildings: gemcrafting table, scribing table, arcane forge, magic circle,
  magical defense pylon, `TM_Portal`, `TM_ManaMine`, `TM_ArcaneCapacitor`. Raw magicyte: each mining strike on any
  stone has a chance to uncover it; deep drilling does not. Enchanting needs a mage holding an enchanting gem.
  Incidents/conditions: mana drain, mana surge, mana storm, mana sickness, elemental assault, demon attack, wandering
  lich, dark clouds, violent storm, divine blessing, enemy arcane raiding party, arcane bandit camp, arcane caravan
  request. Faction `Seers` ("arcane fold"). Summoned/raised races (skeletal, demons, elementals, minions, spirit
  animals, golems). Mod settings for AI difficulty, class frequency, leveling speed.
- In play: a fantasy layer that fits medieval tech perfectly. Enemy classes use their spells. Needs HugsLib and (for
  1.6) JecsLite. Works next to VPE (VPE's FAQ says they don't overlap).
- Rimfolk today: `People.IsRaisedDead` excludes pawns with the `Undead` trait or `TM_UndeadHD` hediff (they keep a
  mood need pinned at full), so no wishes, goals, quirks, dreams or bonds for them. README says spirits are excluded
  too (they fail `HasInnerLife`). Enemy undead also can't become nemeses (nemesis code requires `HasInnerLife`).
- Hook ideas: read class traits for wishes ("a fire mage wants an arcane forge", "a priest wants a shrine",
  "an enchanter wants magicyte"); offices (physician favors Priest/Druid/Apothecary classes, marshal favors fighter
  classes); dilemmas around necromancy (`TM_RaiseUndead` on a dead colonist's body; a necromancer in the council);
  secrets ("Cursed", "possessed"); life goal "master your class" (class level); dream omens tied to mana storms or
  demon attacks (only if those incidents exist); room plans for a mage tower (gemcrafting table, scribing table,
  arcane forge, magic circle); the almanac's advisor could flag magicyte for enchanting.
- Confidence: high (repo defs, in-game tutor text, Workshop).

## Vanilla Psycasts Expanded (`vanillaexpanded.vpsycastse`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2842502659 (GitHub: https://github.com/Vanilla-Expanded/VanillaPsycastsExpanded)
- What it adds: replaces Royalty psycasts with a leveled skill tree (`VanillaPsycastsExpanded.PsycasterPathDef`).
  15 active paths: `VPE_Empath`, `VPE_Skipmaster`, `VPE_Conflagrator`, `VPE_Protector`, `VPE_Warlord`,
  `VPE_Chronopath`, `VPE_Archotechist` (needs an Imperial backstory), `VPE_Necropath`, `VPE_Staticlord`,
  `VPE_Archon`, `VPE_Frostshaper`, `VPE_Wildspeaker` (`requiredFocus` Natural, "Tribals only"), `VPE_Harmonist`,
  `VPE_Technomancer`, `VPE_Nightstalker` (Animat and Puppeteer are commented out; Puppeteer is a separate add-on).
  Experience from meditation, no level cap, points for abilities or psychic stats, autocast by right-click.
  Psycaster raids (new raid strategy for tribals and the Empire, with a warning). Research `VPE_CasterGear`
  (medieval, eltex cape/mask recipes) and `VPE_EltexGear` (spacer). Items: eltex, psyring, prestige plate armor.
  Can't be removed from a save; adding it converts existing psycasts into levels.
- In play: psylinks still come from Royalty sources (titles, neuroformers). In this list the Empire is made medieval
  by Medieval Empire Overhaul. Rimedieval hides spacer research, so eltex gear is out of reach.
- Rimfolk today: nothing specific. Enemy psycasters are ordinary hostile humanlikes, so they can become nemeses.
- Hook ideas: a wish for a meditation spot/focus object from psycasters; life goal "reach psycaster level X";
  Archon path as a natural "speaker" office candidate; Empath abilities as an alternative fix for a mood crisis the
  advisor flags; dilemma when an enemy psycaster is captured (drain them with Archon vs recruit); almanac advice per path.
- Confidence: high (repo defs + Workshop).

## Alpha Genes (`sarg.alphagenes`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2891845502 (GitHub: https://github.com/juanosarg/AlphaGenes)
- What it adds: over 300 genes (the Workshop's count; 442 GeneDef entries in the 1.6 files), more than a dozen
  xenotypes (`AG_Fleetkind`, `AG_Helixien`, `AG_Animusen`, `AG_Lapis`, `AG_Wretch`, `AG_Taukai`, `AG_Hiveling`,
  `AG_MindDevourer`, `AG_Efreet`, `AG_Drakonori`, `AG_Nereid`, `AG_Forsaken`, `AG_RandomCustom` and more), factions
  `AG_OutlanderCivilUnion` (civil xenohuman union) and `AG_XenohumanPirates` (xenohuman pirate gang) that use player
  custom xenotypes, abandoned biotech lab quests, gene tools (gene remover, endogenefier, xenogenefier, germline
  mutator, xenotype injector, xenotype nullifier, genepack tweaker and archotech versions; each person-targeted
  tool puts the target in a 3-day coma), alphapacks/mixedpacks. Extra genes with Alpha Animals and Alpha Biomes
  (both in this list).
- In play: in a medieval-capped game, gene machines are probably out of reach (vanilla genetics research is above
  medieval; not checked in Rimedieval's code), so contact is via xenotypes you meet, quests and tools.
- Rimfolk today: nothing specific.
- Hook ideas: a "germline" or "strange blood" secret for pawns with rare xenotypes; dilemma when a gene tool is found
  (who gets the coma?); xenotype-aware flavor tags (`[xeno:...]`) for dreams and wishes; reputation reactions for
  xenohuman pirate visitors.
- Confidence: high for content (repo), medium for the medieval-reach note.

## Alpha Memes (`sarg.alphamemes`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2661356814 (GitHub: https://github.com/juanosarg/AlphaMemes)
- What it adds: memes (`AM_DeepDevotion`, `AM_PainIsDivine`, `AM_NakedTruth` at impact 4; `AM_Gladiator`,
  `AM_Sharpshooter`, `AM_Bulwark`, `AM_Monastic`, `AM_Artist`, `AM_Madness`, `AM_Sadist`, `AM_Cowboys`,
  `AM_WaterPrimacy`, `AM_Deforestation`, `AM_GauranlenSupremacy`, `AM_Iconoclast`, biological defilers/
  reconstructors/corruptors), structures (atheism, Jewish, Jain, Sikh, Kemetic, Mesoamerican, neolithic, corsair,
  scavenger, steampunk revival, alienism), hundreds of style items, rituals (baptism, flagellation, sparring match,
  rodeo, maddening chant, relic destruction, heart extraction, ocular warping, colony tantrum, scrapping) and the
  Funeral Framework (cremation, mummification, sky burial, pyramid burial, hive burial, rum burial and others).
  Buildings: pyramids, sphinx, megalith, obelisks, grand gate, baptismal font, relic smashing altar, crude stone and
  wooden tables, stone-framed bedrolls, grog dispenser. Mod option for more than four memes.
- In play: memes are picked at ideoligion creation; adding mid-save has no effect without dev-mode editing.
  Gladiator treats ranged weapons as shameful, which clashes with archer-heavy medieval play.
- Rimfolk today: indirect. `WishWorker_Burial.IsFulfilled` is true once the friend's corpse is in a `Building_Grave`
  or destroyed, so a cremation funeral settles a burial wish.
- Hook ideas: eulogies spoken at Alpha Memes funerals (hook the funeral ritual outcome); sparring match as a
  "drills" decree flavor or a rivals' contest outlet; monastic meme to bias the "austerity" decree reactions;
  pyramid and megalith as ambition goals ("stone and mortar"); traditions from repeated rituals.
- Confidence: high (repo defs + Workshop).

## Vanilla Genetics Expanded (`vanillaexpanded.vgeneticse`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2801160906 (GitHub: https://github.com/Vanilla-Expanded/VanillaGeneticsExpanded)
- What it adds: animal genome extraction and hybrid creatures (successor of Genetic Rim). Research `GR_GeneticAlteration`,
  `GR_GeneticEngineering`, `GR_GeneticDuplication`, `GR_HybridImplantology`, `GR_GeneticCompatibility` (industrial)
  and `GR_GeneticMechahybridization` (spacer). Hostile faction `GR_RoamingMonstrosities`; incident
  `GR_ManhunterMonstrosities` (hybrid manhunters, letter links greater scaria). Quests `Script_AbandonedLab`,
  `Script_BiomechanicalLabComplex`. Quests and raids run outside the storyteller with their own frequency options.
  Full hybrid families for Alpha Animals, Dinosauria and Megafauna (all in this list); genome extraction for VE
  Animals and others.
- In play: Rimedieval limits player research to medieval, so the labs can't be built; the raids and lab quests remain.
- Rimfolk today: nothing specific. Hybrid manhunters have no faction, so one that mauls someone and gets away can
  become a named nemesis (nemesis code accepts `RaceProps.Animal && Faction == null`).
- Hook ideas: advisor/almanac warning for scaria-carrying monstrosities; a dilemma about a hybrid found in an
  abandoned lab (keep it, kill it, sell it); encounter "a monstrosity at the tree line".
- Confidence: high (repo + Workshop); medium on how Rimedieval treats the quests.

## Vanilla Ideology Expanded - Memes and Structures (`vanillaexpanded.vmemese`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2636329500 (GitHub: https://github.com/Vanilla-Expanded/VanillaIdeologyExpanded-Memes)
- What it adds: memes such as `VME_Aristocratic`, `VME_Royal`, `VME_Republic` (second and third triumvir roles),
  `VME_GodEmperor`, `VME_ExaltedPriesthood`, `VME_CraftCulture`, `VME_CityBuilders`, `VME_Nomad`, `VME_Bushido`,
  `VME_BloodCourt`, `VME_FireWorship`, `VME_Light`, `VME_Elders` (trait `VME_Elder` is defined separately; link to the meme not seen in XML), `VME_Trader`,
  `VME_PartyLife`, `VME_Pacifist`, `VME_Vegan`, `VME_Egalitarian`, `VME_Emancipation`, `VME_Nationalist`,
  `VME_Healthcare`, `VME_Cultured`, `VME_SweetTeeth`, `VME_Angler`, `VME_Astrology`, `VME_Gestalt`,
  `VME_ViolentConversion`, `VME_HolyDiseases`, `VME_Scrapper`, `VME_Anonymity`, `VME_PsychicFocus`,
  `VFEA_Isolationist`, insectoid/mechanoid supremacy; structures (serketism, esotericism, hedonism, eldritch cult,
  eschatologism, authoritarian, cult of personality, pantheism, omnism, agnosticism, secular spirituality, Shinto/Tao
  origin, corporate). Rituals: bonfire, wicker man burning, divine the stars, incantation, insectoid hymn, leader
  conversion, leadership challenge, orgy, plague festival, slave emancipation, trading fair, violent conversion,
  ceremonial suicide. Needs `VME_Corruption` (eldritch cult structure: falls 12/day, obelisks pause, incantations raise;
  100 = mental break, 0 = easy conversion) and anonymity. Buildings: bonfire, wicker man, ominous obelisk, mechanoid
  effigy, insect nest, many ancient props.
- In play: picked at ideoligion creation; many medieval-flavored choices.
- Rimfolk today: nothing; Rimfolk never reads ideology except to name a precept in mood tooltips (`MoodInsight.Why`).
  Council offices and elections are separate from ideoligion roles and republics.
- Hook ideas: map Republic/Aristocratic/Royal memes onto council rules (elections vs appointments, a triumvirate as the
  council); trading fair as an ambition step; fire worship and the fire-risk advisor; Elders meme to weight older
  colonists' votes; Party Life to tilt the "festival days" decree reactions; leadership challenge as an election
  alternative.
- Confidence: high (repo defs + Workshop).

## Vanilla Ideology Expanded - Icons and Symbols (`vanillaexpanded.ideo.iconsandsymbols`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2552609458 (GitHub: https://github.com/Vanilla-Expanded/VanillaIdeologyExpanded-IconsAndSymbols)
- What it adds: over 200 ideoligion icons (`IdeoSymbolDefs` only), tagged so they fit matching memes. Only affects new
  ideoligions.
- In play: cosmetic.
- Rimfolk today: nothing.
- Hook ideas: none worth building; at most use the ideoligion icon in council banners.
- Confidence: high.

## Vanilla Ideology Expanded - Relics and Artifacts (`vanillaexpanded.ideo.relicsandartifacts`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2564895018 (GitHub: https://github.com/Vanilla-Expanded/VanillaIdeologyExpanded-RelicsAndArtifacts)
- What it adds: over 40 inert relics (scrolls, statuettes, idols, sword shards, gauntlets, mechanoid heads, tech
  parts), a patch letting apparel be relics, a reworked relic picker with search.
- In play: only matters when an ideoligion is created; a helmet or crown can be the colony's relic.
- Rimfolk today: nothing.
- Hook ideas: extend the "trader wants an heirloom" encounter to relics; a relic theft dilemma; relic recovered as an
  ambition goal.
- Confidence: high.

## Vanilla Ideology Expanded - Splits and Schisms (`vanillaexpanded.viesas`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2573138902 (no public repo found; id confirmed in the RimSort database)
- What it adds: the ideological schism event: in a large enough colony a new ideoligion emerges in part of the
  colonists, similar to the original but with one or two different memes; the two compete until one vanishes, then
  another schism can occur. Extensive mod options. Safe to add mid-save. Code by Taranchuk.
- In play: a social threat for big colonies; conversion work becomes a priority.
- Rimfolk today: nothing; council votes go by skill, suitability and friendship, not creed.
- Hook ideas: a council dilemma when a schism fires (tolerate, persecute, debate); circles that split along creed;
  a "unity" ambition; journals and annals noting the schism. Needs the event's defName (not verified; check the mod's Defs).
- Confidence: medium (Workshop text only).

## Vanilla Backgrounds Expanded (`vanillaexpanded.backgrounds`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2775017012 (GitHub: https://github.com/Vanilla-Expanded/VanillaBackgroundsExpanded)
- What it adds: main menu and (optionally) loading screen background images and videos with options to cycle them;
  `BackgroundDef` for modders. Note: "backgrounds" means menu art, not pawn backstories.
- In play: cosmetic; no gameplay effect.
- Rimfolk today: nothing.
- Hook ideas: none (Rimfolk could ship a `BackgroundDef` with its preview art if it ever wants a menu background).
- Confidence: high.

## People Can Change (`battlemage64.peoplecanchange`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1257561308
- What it adds: random trait gains, losses and degree shifts over time (max traits 4 by default, min configurable,
  per-trait allow lists in settings, works with modded traits). Event-driven changes: sustained happiness or
  near-break mood gives optimist or depressive (about a once-per-quadrum check, 1 in 2; can be disabled); eating
  human meat 1 in 100 (cooked) or 1 in 300 (raw) to become cannibal; body purist 1 in 3 per quadrum to lose it, then
  1 in 3 to turn transhumanist. Safe to add or remove.
- In play: colonists drift; combined with VSIE's "obtaining new traits", traits can change a lot.
- Rimfolk today: nothing specific, but Rimfolk reads traits live (decree reactions, dilemma `onlyTrait`/`exceptTrait`,
  secrets), so changes take effect immediately.
- Hook ideas: journal lines and a chronicle entry when a trait changes ("she has grown kinder"); let Rimfolk's
  experiences (trust, granted wishes, grief) nudge PCC-like changes; a quirk becoming a trait.
- Confidence: high (Workshop text with exact odds).

## RimTraits - General Traits (`sierra.rt.generaltraits`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2206957172
- What it adds: 46 minor attribute traits in tiers of increasing rarity, some mutually exclusive. Supports [SYR] Trait
  Value. Same author: RimTraits - Medieval Talents (`sierra.rt.medievaltalents`, in this list, other batch).
- In play: more varied pawns; mostly small stat traits.
- Rimfolk today: nothing (Rimfolk keys reactions to vanilla trait defNames).
- Hook ideas: read the trait list (defNames not checked; no repo found) and map attribute traits onto wish/office
  suitability; flavor tags for strong/weak bodies.
- Confidence: medium (Workshop text only).

## Vanilla Traits Expanded (`vanillaexpanded.vanillatraitsexpanded`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2296404655 (GitHub: https://github.com/Vanilla-Expanded/VanillaTraitsExpanded)
- What it adds: 53 traits in the 1.6 files (Workshop says 55): `VTE_AbsentMinded`, `VTE_Academian`,
  `VTE_AnimalHater`, `VTE_AnimalLover`, `VTE_Anxious`, `VTE_BigBoned`, `VTE_Brave`, `VTE_CatPerson`,
  `VTE_ChildOfMountain`, `VTE_ChildOfSea`, `VTE_Clumsy`, `VTE_ColdInclined`, `VTE_CouchPotato`, `VTE_Coward`,
  `VTE_Desensitized`, `VTE_DogPerson`, `VTE_DrunkenMaster`, `VTE_Dunce`, `VTE_Eccentric`, `VTE_Ecologist`,
  `VTE_FunLoving`, `VTE_Gastronomist`, `VTE_Groundbreaker`, `VTE_HeatInclined`, `VTE_HeavySleeper`,
  `VTE_Insatiable`, `VTE_Insomniac`, `VTE_IronStomach`, `VTE_Kleptomaniac`, `VTE_Lush`, `VTE_MadSurgeon`,
  `VTE_MartialArtist`, `VTE_Menagerist`, `VTE_Neat`, `VTE_Perfectionist`, `VTE_Prodigy`, `VTE_Prude`, `VTE_Rebel`,
  `VTE_RefinedPalate`, `VTE_Schizoid`, `VTE_Slob`, `VTE_Snob`, `VTE_Squeamish`, `VTE_Stoner`, `VTE_Submissive`,
  `VTE_Technophobe`, `VTE_ThickSkinned`, `VTE_ThinSkinned`, `VTE_Tycoon`, `VTE_Vengeful`, `VTE_Wanderlust`,
  `VTE_Workaholic`, `VTE_WorldWeary`. Options can zero the commonality of any trait (vanilla or modded); existing
  holders keep it. Load above other trait mods.
- In play: noticeably more varied colonists; some negative traits.
- Rimfolk today: nothing; none of these defNames is referenced.
- Hook ideas: decree reactions (`VTE_Workaholic` likes long hours, `VTE_CouchPotato`/`VTE_FunLoving` like festival
  days, `VTE_Rebel` hates every decree), wishes (`VTE_Wanderlust` caravan wish, `VTE_Snob` and `VTE_RefinedPalate`
  fine-meal/fine-room wishes, `VTE_Neat` clean colony, `VTE_AnimalLover`/`VTE_Menagerist` animal wishes,
  `VTE_Academian` books), secrets (`VTE_Kleptomaniac` thief dilemma is a perfect fit), vendettas (`VTE_Vengeful`),
  quirks and dreams (`VTE_Anxious`, `VTE_Insomniac`), epithets. All via `GetNamedSilentFail` or XML `trait` refs with MayRequire.
- Confidence: high (repo + Workshop).

## Vanilla Skills Expanded (`vanillaexpanded.skills`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3400246558 (GitHub: https://github.com/Vanilla-Expanded/VanillaSkillsExpanded).
  The older upload 2854967442 is outdated.
- What it adds: `VSE.Passions.PassionDef` passions with learn rates: None 0.35 (isBad), Minor 1.0, Major 1.5,
  `VSE_Apathy` 0.25 (forget x1.25, isBad), `VSE_Natural` 2.0 (forget x3), `VSE_Critical` 3.0 (never forgets;
  `learnRateFactorOther` 0.25 slows every other skill while the default setting `CriticalEffectPassions` is on).
  Expertise (`ExpertiseDefs`, one per pawn, permanent, player pawns only; the Workshop says it needs a passion and
  more than 15 levels): Sharpshooting, Aiming, Sniping, Piercing, Dueling, Striking, Taming, Ranching, Hunting,
  Handling, Art Quality/Quantity, Flooring, Repairing, Architecture, Building, Smoothing, Butchering, Food Hygiene,
  Tailoring, Weaponsmithing, Surgery, First Aid, Nursing, Operating, Drilling, Prospecting, Tunnelling, Geology,
  Researching, Writing, Dark study, Foraging and more.
- In play: one permanent specialization per pawn; natural passions need constant practice.
- Rimfolk today: `People.PassionLevel` judges modded passions by `LearnRateFactor(true)` (below 0.9 none, 1.25+
  burning): apathy = none, natural and critical = burning. See the bug note at the top (other-skill multipliers and
  per-passion caching).
- Hook ideas: fix the cache (read `PassionDef.learnRateFactor` and `isBad`); a life goal "earn an expertise"; epithets
  from expertise ("the Weaponsmith"); offices weighting expertise (physician: Surgery, quartermaster: trade-related);
  "lessons from the colony's best" wishes keyed on expertise; a natural-passion colonist wishing to practice before
  the skill fades.
- Confidence: high (repo defs + decompiled VSE.dll).

## Alpha Skills (`sarg.alphaskills`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3448953006 (GitHub: https://github.com/juanosarg/AlphaSkills)
- What it adds (needs VSE): 26 passions (Workshop count), many as inactive/active pairs that swap the pawn's passion
  value: drunken (1.0 / 2.5), stoned (1.0 / 1.8), night (1.0 / 2.5), vengeful (1.0 / 3.0 for three days after a colony
  loss), nomadic (1.0 / 2.25 on caravans), rainy day (1.0 / 2.0), competitive (0.25 / 2.5 while the colony's best),
  nudist (0.5 / 2.0), intimate (0.75 / 1.75), moody (0.1 to 2.0, rerolled daily); fixed ones: youth 3.0 (fades with
  age), dedicated 1.25, obsessive 1.25 (others x0.9), forbidden 1.75, synergistic 1.0 (others x1.1), like-minded 1.0,
  frozen 0 and dunce 0 (isBad), traumatic 1.0 (isBad); DLC ones in `Mods/` (sanguine, transhumanist, toxic...).
  19 expertises (Blasting, Mortaring, Firestarter, Cleaving, Evading, Enduring, Crafting quality/yield, Smelting,
  Panning, Salvaging, Trafficking, Influencing, Psycasting, Quelling, Piloting, Vacuum Resilience, Ranged Dodging,
  Mindfulness). Passions can be disabled in VSE's options.
- In play: passions depend on conditions; schedules, weather and drugs matter for learning.
- Rimfolk today: through `PassionLevel` by learn rate. Because active and inactive variants are different passion
  values, Rimfolk sees the current state: a dormant competitive (0.25), nudist (0.5) or intimate (0.75) passion
  counts as none, dedicated and obsessive (1.25) count as burning, traumatic counts as interested.
- Hook ideas: wishes from conditional passions ("rainy day" asks for a covered work spot outside; "night" asks for a
  night shift), vengeful passion activation as a vendetta flavor, competitive passion feeding Rimfolk's rival
  contests, moody passion as a quirk-like journal line.
- Confidence: high (repo defs).

## Level Up! (`krafs.levelup`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1701592470 (GitHub: https://github.com/krafs/LevelUp)
- What it adds: a notification when a colonist gains (or loses) a skill level, with settings and a cooldown so
  vanilla skill decay around levels 10 and 20 doesn't spam it.
- In play: UI only.
- Rimfolk today: nothing.
- Hook ideas: none needed; Rimfolk's own mentorship graduation (level 11) will show next to Level Up's notice.
- Confidence: high.

## Vanilla Social Interactions Expanded (`vanillaexpanded.vanillasocialinteractionsexpanded`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2439736083 (GitHub: https://github.com/Vanilla-Expanded/VanillaSocialInteractionsExpanded)
- What it adds: memories, inspiration changes (redefines vanilla `Frenzy_Work`, `Frenzy_Shoot`, `Inspired_*` and adds
  `VSIE_Inspired_Planting`, `VSIE_Party_Frenzy`, `VSIE_Inspired_Research`, `VSIE_Inspired_Mining`,
  `VSIE_Learning_Frenzy`, `VSIE_Inspired_Cooking`, `VSIE_Flirting_Frenzy`, `VSIE_Melee_Frenzy`), aspirations,
  obtaining new traits, venting, friend and best friend (`VSIE_BestFriend` plus grief relations), teaching, many
  hands make light work, discord, gatherings (`VSIE_MealTogether`, `VSIE_GoingForAWalk`, `VSIE_GrabbingBeer`,
  `VSIE_Skygazing`, `VSIE_ViewingArtTogether`, `VSIE_BuildingSnowmen`, `VSIE_MovieNight`, `VSIE_BingeParty`,
  `VSIE_OutdoorParty`, `VSIE_Funeral`, `VSIE_BirthdayParty`), dating, united we stand, hostile environment, one
  night stand (`VSIE_OneNightStand`). Every mechanic can be disabled.
- In play: livelier downtime; overlaps with People Can Change on trait gains.
- Rimfolk today: designed to coexist (README): VSIE best friends and teaching sit beside Rimfolk's bonds and
  mentorships; VSIE aspirations are short inspiration tasks, not life goals. Rimfolk grants the inspiration that
  fits a wish and otherwise falls back to `GetRandomAvailableInspirationDef`, which can pick VSIE's inspirations.
- Hook ideas: count VSIE best friends as strong bonds in Rimfolk's social web; let a VSIE funeral gathering host
  Rimfolk's eulogy; `VSIE_BirthdayParty` fulfilling the birthday wish; pick VSIE inspirations deliberately for
  matching wishes (mining wish → `VSIE_Inspired_Mining`, kitchen → `VSIE_Inspired_Cooking`).
- Confidence: high (repo + README).

## JecsLite - A RimWorld of Magic (`zal.jecsliterwom`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3312959701 (author Zaljerem)
- What it adds: library. A trimmed JecsTools containing only what RWoM needs: AbilityUser, AbilityUserAI,
  CompDeflector, CompLumbering, CompOversizedWeapon (with small fixes). RWoM's 1.6 About.xml lists it as the
  dependency instead of full JecsTools. Replaces JecsTools for RWoM only; HugsLib is still required.
- In play: nothing visible; must load before RWoM.
- Rimfolk today: nothing.
- Hook ideas: none (RWoM abilities are `AbilityUser` based, which matters only if Rimfolk ever wants to read them).
- Confidence: high (Workshop page + RWoM About.xml).

## Medieval Go-juice (`rince.gaulishgojuice`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2969927060 (author Rince; not in the RimSort database,
  matched by author and the "Gaulish potion" description)
- What it adds: a "Gaulish potion" go-juice brewed at a fueled cooking station, no research, by a pawn with Cooking
  10 and Intellectual 10 (lowered from 15), using psychoid leaves; short shelf life (7 days since a July 2025 update,
  which also halved the work). Can be made in Medieval Overhaul's stew pot. A weak psychite joint (no research).
  Made so medieval and tribal Hussars (go-juice dependent) can be supplied.
- In play: a combat drug for medieval colonies; the brewer is the bottleneck.
- Rimfolk today: nothing verified. If the potion's drug comp uses the GoJuice chemical, Rimfolk's addiction-supply
  wish (`WishWorker_Fix` counts any drug with the addiction's chemical) would count it (not checked; no source).
- Hook ideas: "the druid" epithet for whoever brews it; a dilemma about drugging the militia before a raid; advisor
  hint for Hussars without go-juice.
- Confidence: medium (Workshop text; defs not inspected).

## Xenotype Summary (`zhuma.xenotypesummary`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3710012135 (author shown as Waggish Pluto; package id not
  confirmed from About.xml, matched by name)
- What it adds: a "Summary" button on the gene tab, info cards, xenotype creator and gene assembler: all gene
  descriptions in one scroll, summed stat offsets/factors, capacities, damage factors, disabled work, nullified
  thoughts, abilities and forced traits, with search. Needs Biotech and Harmony.
- In play: UI only.
- Rimfolk today: nothing.
- Hook ideas: none.
- Confidence: medium (package id).

## EBSG Framework (`ebsg.framework`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=3112549163 (GitHub: https://github.com/KonradHeinser/EBSGFramework, wiki there)
- What it adds: library for gene/xenotype mods (Expanded Biotech Style Genes and others): resource genes like hemogen
  in XML, equipment locks by gene/xenotype/hediff, deathrest/pregnancy/blood stats, tree-desire thoughts, colony-size
  mood curves, part-removing hediffs, hive minds, many comps. No hard DLC requirement.
- In play: nothing on its own.
- Rimfolk today: nothing.
- Hook ideas: none; if Rimfolk ever reads gene-driven needs, EBSG resource genes are `Gene_Resource` style.
- Confidence: high.

## Vanilla Hair Expanded (`vanillaexpanded.vhe`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1888705256 (GitHub: https://github.com/Vanilla-Expanded/VanillaHairExpanded)
- What it adds: about a hundred vanilla-style hairstyles (114 HairDef entries in 1.6, color-grouped files plus a
  viking set) and a barber's table (Misc tab; 75 wood or metal plus 20 steel) to change hair, hair color and beard.
- In play: cosmetic, plus a barber's table.
- Rimfolk today: nothing.
- Hook ideas: a "vain colonist wants a haircut" wish fulfilled at the barber's table; barber's table as a room plan
  swap option.
- Confidence: high.

## Vanilla Hair Retextured (`neronix17.retexture.vanillahair`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2748834409
- What it adds: higher-resolution, more consistent textures for every vanilla and DLC hairstyle.
- In play: cosmetic.
- Rimfolk today: nothing. Hook ideas: none.
- Confidence: high.

## Vanilla Races Expanded - Custom Icons (`vanillaracesexpanded.customicons`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2917311689 (GitHub: https://github.com/Vanilla-Expanded/VanillaRacesExpanded-CustomIcons)
- What it adds: hundreds of extra icons for custom xenotypes in the xenotype creator (Biotech).
- In play: cosmetic; icons never appear randomly.
- Rimfolk today: nothing. Hook ideas: none.
- Confidence: high.

## Trait and Backstory Icons (`superniquito.traiticons`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2873494547
- What it adds: icons next to traits and backstories in the bio tab, consistent trait label capitalization, optional
  source-mod icons. Covers vanilla, DLCs, VTE (53/53), RimTraits General Traits (46/46), Medieval Overhaul, Alpha
  Memes, Alpha Genes, VPE, VME; one icon for all RWoM and RimTraits Medieval Talents traits; falls back to
  RimTraits Vanilla Trait Colors' color for unsupported mods.
- In play: UI only.
- Rimfolk today: nothing. Hook ideas: Rimfolk's Story tab could reuse these icons when listing traits (optional soft dependency).
- Confidence: high.

## RimTraits - Vanilla Trait Colors (`sierra.rt.vanillatraitcolors`)
- Link: https://steamcommunity.com/sharedfiles/filedetails/?id=2244594116
- What it adds: recolors trait labels by commonality (RimTraits scheme) for vanilla and supported mods (RimTraits
  General Traits, Vanilla Traits Expanded, Vanilla Fishing Expanded, Medieval Overhaul, Additional Traits).
- In play: UI only; color means rarity, not good or bad.
- Rimfolk today: nothing. Hook ideas: none.
- Confidence: high.
