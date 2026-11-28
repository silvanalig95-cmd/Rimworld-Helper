# Review of the magic integrations (28 November 2026)

The player, 28 November 2026: "great, can you also review all the rimworld of magic integrations, as these perhaps never
properly worked with the outdated mod files".

Rimfolk's integrations with A RimWorld of Magic were written against the original mod (Torann, Workshop 1201382956,
2.6.x, game versions up to 1.6). The player now runs **A RimWorld of Magic (Continued)** by HUGINN (Workshop 3823345404,
1.6 and 1.7), with **RWoM Core (Ability Framework)** (`huginn.rwom.core`, 3833946439), **RWoM Integrations**
(`huginn.rwom.integrations`, 3833946677) and **JecsLite - A RimWorld of Magic** (`zal.jecsliterwom`, 3312959701). The
continuation keeps the package id `torann.arimworldofmagic`, so every `MayRequire` and every id check still switches on;
the question was whether what Rimfolk reads and calls still exists and still means the same.

Read: the player's own Workshop files (defs only) of the continuation, the integrations, JecsLite and the original; the
player's catalogue `docs/player-mods` (rebuilt today from those files); the continuation's public source (modVersion 3.0,
which may be older than the Workshop build); RWoM Core's source and its DLL (read with a metadata reader); the original's
source 2.6.4.4; and the player's save of 26 November (Eastern Menistan), which shows their mages.

The fixed rules are kept by `Tools/Tests/ReviewMagicTests.cs` (and `MagicAutoCastTests`, `PeoplesMagicTests`).

## What the player's save shows

| Colonist | Traits | The continuation's class progression | Knowledge store |
|---|---|---|---|
| Caitlin (Cait) | Beauty, **warrior** (`TM_VocationWarrior`) | super soldier (might, foundation), learned fresh: no class trait | rwom.tm_cqc |
| Denis (Myst) | Fast learner, **warrior-mage** (`TM_VocationWarriorMage`) | the Shadow (magic, foundation) and its shadow walker (might, master), imported from the trait `TM_TheShadow`, which he no longer has | 7 spells (arcane bolt, cooler, summon, soul bond, shadow strike, nightshade, veil of shadows) |
| Aelanna (Anna), a slave | Kind, Beauty, `TM_Empath` | empath (magic, master), imported, the trait kept | none |

So the player has the continuation's new class progression switched on (a beta in its settings), and both of their own
mages hold their class through it, not through a class trait. Everything of Rimfolk's that asked "which class is this?"
by trait saw neither of them as a mage or a fighter.

## The integrations

37 places, read one by one. "Sound" means it reads or calls something that exists in the continuation and means the same.

| # | Where | What it reads or calls | In the continuation |
|---|---|---|---|
| 1 | `Peoples/Magic/PeoplesMagic.cs` (`PeoplesMagic`) | `TorannMagic.CompAbilityUserMagic` (is the mod there), class traits (`PeoplesGearMath.MagicClasses`), the class garb things | Sound; a class held through a discipline was not seen: **fixed** (`Craft` reads disciplines) |
| 2 | `PeoplesMagic.cs` (`RomSpells`) | `IsMagicUser`, `AbilityData.Powers`, JecsLite's `PawnAbility` (`Def`, `CooldownTicksLeft`, `CanCastPowerCheck`, `GetJob`, `Verb`, `Pawn`), `AbilityContext` value 1, `Settings.Instance.AICasting`, the might comp (`IsMightUser`, `AbilityData`, `Stamina`, `ActualStaminaCost`), `Mana`, `ActualManaCost`, `TMAbilityDef.manaCost`/`staminaCost`, the cast jobs | Sound, but the learned mark now lives in the knowledge store: **fixed** (knowledge first), the AI context read by name |
| 3 | `Peoples/Magic/PeoplesMagicWatch.cs` | the peace spells (`TM_AdvancedHeal`, `TM_Purify`, `TM_Heal`, `TM_CureDisease`, `TM_Regenerate`, `TM_Rainmaker`) cast by the mod's AI-context job | Names sound; on 1.7 the continuation refused every one: **fixed** (below) |
| 4 | `Peoples/Magic/PeoplesMagicPatches.cs` | `TM_Calc.HasResourcesForAbility`, `MagicAbility`/`MightAbility.CanCastPowerCheck`; the 25 spells kept out | Sound |
| 5 | `Peoples/Magic/PeoplesMagicDebug.cs` | the above, for the dev log | Sound |
| 6 | `Integrations/MagicAutoCast*.cs` | the 11 damage spells and their levels, `Projectile_ShadowStrike`, the cast jobs, Shadow Strike's draft and return | Written against the continuation; three gaps **fixed** (knowledge store, another system casting, Spite's hate) |
| 7 | `Integrations/MagicFamiliars*.cs` | `TM_Mana`, `TM_ArcaneWeakness`, `Faceless`, the package id | Sound (a vocation mage has the mana need) |
| 8 | `Integrations/MagicGolems.cs` | `Golems.TMPawnGolem`, `CompGolem.golem`/`initialized`/`CompTick`, `TM_Golem.needs`, `TM_GolemEnergy` | Sound |
| 9 | `Integrations/MagicNeeds.cs` | `TM_Travel` (still switched off by the mod), `TM_Mana`, `TM_Stamina`, `TM_UndeadHD` | Sound |
| 10 | `Integrations/MagicPatchRescue.cs` | HugsLib's patching, the original's two broken patch signatures | The continuation fixed both and applies its own patches; the rescue finds nothing and does nothing (checked: no double patching, `AppliedBy` of the package id is empty) |
| 11 | `Integrations/Magic.cs` (`MagicClasses.Level`, the class-level life goals) | `MagicUserLevel`, `MightUserLevel` | Sound; reading it for a non-caster threw inside the mod every time: **fixed** (`IsMagicUser`/`IsMightUser` first) |
| 12 | `Integrations/MagicPowers.cs` (`MagicCraft.Mastery`, the craft life goals) | `MagicData.AllMagicPowers`, `MightData.AllMightPowers`, each power's `learned`, `level` | The continuation lists every scroll-learned spell twice: **fixed** (each power once, knowledge first) |
| 13 | `Integrations/ModStory.cs` (the long discipline) | `MagicClasses.Level` | Sound (vocation pawns have levels) |
| 14 | `Integrations/ModLinks.cs` | arcane weakness, mana sickness, regeneration, spell mending, inner healing, the mod's thoughts | Sound |
| 15 | `Integrations/PeopleTags.cs`, `Defs/IntegrationDefs/Council_People_TagGroups.xml` | the mage, fighter, healer and gifted tags by class trait | Vocations, disciplines and the new weather mage were missing: **fixed** |
| 16 | `Gods/OnMap/GodGifts.cs` | `SpellOf_` scrolls, class traits, the class switches in `Settings`, `initializedIsMagicUser`, `customIndex` | Sound; a vocation colonist could be given a class trait on top: **fixed** |
| 17 | `Gods/World/GodWorldGifts.cs`, `GodWorldMath.cs` | classes, spells, the mana traits | Sound (it already asked the level, which a vocation pawn has) |
| 18 | `Gods/Worship/WorshipMore.cs`, `WorshipMath.cs` | `Need_Mana.GainNeed`, the class's domain | Sound; disciplines and the weather mage had no domain: **fixed** |
| 19 | `Gods/Worship/Worship.cs` | the priestly classes (by tag), two motes | Sound (the tag now sees disciplines) |
| 20 | `Gods/Presence/GodReactions.cs` | the devout classes | **Fixed** (disciplines) |
| 21 | `Gods/Belief/PawnGods.cs`, `PawnGodsMath.cs` | a class trait's draw toward a god | **Fixed** (a discipline draws as its class) |
| 22 | `Gods/GodsMath.cs` | the meme `TM_Transcendent` | Sound |
| 23 | `Animals/AnimalVoices.cs` | druids and rangers; the mod loaded | **Fixed** (disciplines) |
| 24 | `Happenings/Workers_Living.cs` (starfall) | `RawMagicyte`, `TM_MagicUserHD` | Sound (the hediff still marks every caster) |
| 25 | `Happenings/Workers_Storyteller.cs` (the arcane tome) | the `BookOf` items, `TM_Gifted`, `PhysicalProdigy` | Sound: a gifted colonist without a vocation still reads a class book the classic way |
| 26 | `Harm/Contests.cs`, `Harm/Stories.cs` | the fighter tag (sparring), a working gone wrong | Sound (the tag now sees disciplines) |
| 27 | `OldPowers/` (finds, night, visitors) | `TM_Scrolls`, `TM_Grimoire`, the enchanting stones, sounds, motes, the mana skies, `Need_Mana`, `Seers`, two classes given | Sound; a mage of any class now counts as one under a sky of power (by the mana need) |
| 28 | `Peoples/PeopleGifts.cs`, `PeoplesGearMath.cs` | class traits, the class switches and the faction shares in `Settings` | Sound |
| 29 | `Peoples/Foreign/ForeignPeoples.cs` | class gifts for another mod's peoples | Sound |
| 30 | `Peoples/Events10/ArtHandedOn.cs`, `PeopleEvents10Math.cs` | the `BookOf` items, the gift traits | Sound |
| 31 | `People.cs` | `TM_UndeadHD`, the trait `Undead` | Sound |
| 32 | `Letters/LetterCultureMath.cs` | the spirit race `TM_SpiritTD` | Sound |
| 33 | `Apparel/` readers | `Enchantment.CompProperties_EnchantedItem`, the `TM_Artifact` and `TM_Cloak` layers | Sound |
| 34 | `Weapons/WeaponReadMath.cs` | the wands' verbs and projectiles | Sound |
| 35 | Defs with `MayRequire="torann.arimworldofmagic"` (encounters, moments, wishes, life goals, dreams, pools, storytellers, the gods' looks, the peoples' class gifts, the land's situations, the heart's thoughts) | 295 names | Every one is in the continuation (none renamed or removed); the class-based ones fixed through the tags |
| 36 | `Defs/AlmanacArticleDefs/Almanac_Mods_C_PeopleMagic.xml` | the mod's page | The class progression added; the Harmony 3 note now says it is the original's |
| 37 | `UI/SettingsCatalog.cs` | the package id for the four settings | Sound |

## What was broken, and the fix (most serious first)

1. **The player's own mages were neither mages nor fighters to Rimfolk.** With the continuation's class progression a
   class is a vocation trait and disciplines (kept in its `CompClassProgression`), not a class trait. Myst and Cait had
   no tag `mage` or `fighter`, so no class moment, encounter, wish, dream, life goal, spar, worship domain, god's draw or
   devotion reached them. Now `Integrations/MagicModel.cs` reads a pawn's disciplines (RWoM Core's public
   `PawnDisciplines.TryRead` first, else the continuation's `ClassIdentity.TryGetState`) and `MagicModelMath.Disciplines`
   maps each of the 37 to the class trait of its art, from the continuation's own defs (`ClassDisciplineDef.legacyTrait`;
   the martial disciplines by name; the wanderer by its domain). The tags count them (`trait:Druid` and the groups), the
   mage tag lists `TM_VocationMage`, `TM_VocationWarriorMage` and the new `TM_WeatherMage`, the fighter tag the warrior and
   warrior-mage vocations. Myst is now a mage and a fighter, the Shadow; Cait a fighter, the super soldier.
2. **A visitor's helpful spell in peace never happened on 1.7.** The continuation checks every AI-context cast job as its
   own automatic casting (`Compatibility.AbilityCastSafety.IsAutomaticCast`, then `CanAutocast`): someone not of the
   colony may cast that way only in a fight, and only from where they stand. Every healing, cure and rain Rimfolk asked of
   a visitor ended before it began. `Patch_PeoplesMagicPeaceCast` reads that one job as asked for while Rimfolk follows it
   (the visitor walks into range by the mod's own job and pays their own mana); with the deeper hooks off, or without that
   check, no visitor is asked (`Patch_PeoplesMagicPeaceCast.Open`). The player context was not used instead: the
   framework's job driver sets the player's targeter for it, and JecsLite may refuse it for a pawn not of the colony.
3. **A god could give a class trait to a colonist who already had a class by vocation** (`GodGifts.HasClass` read traits
   only). On top of a vocation the trait does nothing but confuse the mod's own checks. Now a vocation, a discipline or a
   caster's or fighter's level counts as a class.
4. **The craft life goals counted every scroll-learned spell twice.** The continuation puts `MagicPowersStandalone` into
   `AllMagicPowersWithSkills` and again into `AllMagicPowers`. Each power is counted once now, and whether it is learned is
   read from the knowledge store first (one read of the whole store per count).
5. **A spell learned is now read where the continuation keeps it.** `RomSpells.HeldBy` and `RomSpells.Ability` (the
   colony's own casting and the peoples' casters) skip an ability the knowledge store marks unlearned: RWoM Core's
   `PawnSpellKnowledge.Read` first (an entry found: `Available` or `MissingDefinition`), else the continuation's
   `AbilityKnowledgeComp.Find` (when `ReadyForQuery`), else the ability list as before, as the continuation's own casting
   decides it (`AutocastPolicy.KnowledgeLearnedAllows`). The spell's id is the continuation's `SpellIds.Of`, else its
   convention (`rwom.` and the def's name in lower case; Kure's `TM_Shadowstep` is `kure.tm_shadowstep`), which matches
   the save (`rwom.tm_arcanebolt`, `rwom.tm_cqc`). Rimfolk reads no auto-cast mark, old or new.
6. **Casting on their own could cut in on another system's cast.** It started its job with `InterruptForced`. Now nobody is
   asked while RWoM Core's `CastOwnership.IsCasting` says another system casts for them (Vanilla Psycasts Expanded's
   warm-ups, Ability AI, as RWoM Integrations registers them).
7. **Spite for a death knight of the class progression** spends 20 hate in its own verb
   (`ClassProgression.Verb_DeathKnightSpite`) and does nothing without it, so a cast asked for fizzled and lost the
   500-tick gap. It now waits for the hate (`MagicAutoCastMath.SpiteReady`); a death knight by the classic trait casts as
   before.
8. **A class level was read by letting the mod throw** for everyone without one (`MagicUserLevel` reads a null
   `MagicData`), a caught exception per call for every colonist the life goals and gifts looked at. `IsMagicUser` and
   `IsMightUser` are read first.
9. Smaller: the framework's AI context is read by its name (`AbilityContext.AI`), its value 1 kept as the fallback; a
   colonist under a sky full of power counts as a mage by the mod's mana need, so a warlock, a blood mage, an empath or a
   vocation mage counts (only the classes the peoples give counted before); the weather mage serves the storm's god; the
   Almanac's page says how the class progression is read, and that the Harmony 3 rescue is for the original only.

Each read fails quietly: a member not found leaves the caller with what it read before, an error is logged once per
place (`CouncilLog.ErrorOnce`), and one warning says when the continuation is there but RWoM Core's way in is not.

## Checked and sound

- **Every def name.** All 295 names of the mod in Rimfolk's code and defs are in the continuation (none renamed or
  removed); the continuation only adds (the vocations, the weather mage and its spells, the careers' techniques, the
  master tomes and torn books). `ReviewMagicTests` checks each against the player's active catalogue.
- **The auto-cast's spells** (`MagicAutoCastMath.Spells`), against the player's defs: Arcane Bolt 0.25 mana, Firebolt
  0.15, Icebolt 0.20, Lightning Bolt 0.20, Frost Ray 0.20 to 0.34 (levels I to III), Magic Missile 0.24 to 0.32 (I to III),
  Poison 0.15 (mana); Headshot 0.40, Throwing Knife 0.30, Spite 0.20 (I to III), Shadow Strike 0.65 (stamina). Names,
  levels, costs, pools, ranges and projectiles are as the original had them; only Spite's verb changed (above). Each is a
  `MagicAbility` or a `MightAbility` as its pool says (the catalogue's `abilities.tsv`). Shadow Strike's return (300 ticks)
  and its draft are unchanged. `AllowsContext` refuses the AI context only for Summon, Meteor, Orbital Strike and the brands,
  none of them cast by Rimfolk.
- **The costs** the reserve is kept against are the mod's own `ActualManaCost` and `ActualStaminaCost`, which take the
  efficiency ranks from its old skill lists (and mirror them into the knowledge store); Rimfolk reads no rank itself.
- **The colony's casts on 1.7.** The continuation's automatic-cast check asks a colonist for no fight (`RequiresCombat`
  is for those not of the colony), and for the target in range and sight from where they stand, which Rimfolk checks
  before it asks. Myst's Arcane Bolt and Shadow Strike are in his magic and might components, as Rimfolk reads them.
- **The comps and members** `CompAbilityUserMagic`/`Might` (`IsMagicUser`, `IsMightUser` now by the vocation,
  `MagicUserLevel`, `MightUserLevel`, `MagicData`, `MightData`, `Mana`, `Stamina`, `ActualManaCost`, `ActualStaminaCost`,
  `initializedIsMagicUser`, `initializedIsMightUser`, `customIndex`), `TMAbilityDef` costs, `ModOptions.Settings`
  (`Instance`, `AICasting`, every class switch Rimfolk names, `FactionFighterSettings`, `FactionMageSettings`),
  `TM_Calc.HasResourcesForAbility`, `MagicAbility`/`MightAbility.CanCastPowerCheck`, the golems' types and fields,
  `Need_Mana.GainNeed`, the enchanting comp and the wands' verbs: all in the continuation's source, the same shapes.
- **The old power lists** (`learned`, `level`, `abilityDef`) are still written by the continuation beside its knowledge
  store (`GrimoireKnowledge.ChangeTier`, `AutocastPolicy.SyncAutocast`), so a reader of either agrees for the player's
  pawns (the save: the same seven spells for Myst both ways).
- **The needs rules.** The continuation still switches the travel need off and still keeps mana and stamina from the
  undead; Rimfolk's last word on those holds. (It also gives its spirits only their own need and mana; Rimfolk doesn't
  re-apply that one.)
- **The peoples' class gifts.** A classic class trait given to a new pawn of the peoples stays classic until the next
  load, when the continuation imports it into its progression (`TM_LegacyRecovery`, on `PostLoadInit`) and keeps the trait,
  so the peoples' crafts read the same either way.
- **The rescue of the original's patches** finds nothing in the continuation (both patch signatures are fixed, and it
  applies its patches itself, one class at a time) and does nothing.

## What only the DLLs could settle

The player has not sent the continuation's DLLs yet (`Tools/Collect-ModCode.ps1` collects them now). Everything below
fails softly either way.

- **JecsLite's `PawnAbility` and `AbilityData`** (`Powers`, `Def`, `Pawn`, `Verb`, `CooldownTicksLeft`,
  `CanCastPowerCheck(AbilityContext, out string)`, `GetJob(AbilityContext, LocalTargetInfo)`): the continuation's source
  calls each, but JecsLite's own source isn't public. Rimfolk checks every one's type before it calls (`RomSpells.Ready`).
- **The Workshop build of the continuation** may be newer than the public source (3.0): whether
  `Compatibility.AbilityCastSafety.IsAutomaticCast` is still there with the same rule (if not, the hook stands down and
  says why in the hooks list, and the peace spells are asked as before); and why Myst's `TM_TheShadow` trait is gone
  while the source keeps an imported trait.
- **RWoM Core's DLL**: the copy read here references .NET 10's `System.Runtime`, not the game's framework; the Workshop
  build will differ. Rimfolk reads `PawnDisciplines`, `PawnSpellKnowledge` and `CastOwnership` by name and falls back to
  the continuation's own components when they're missing (with one warning in the log).
- **Whether JecsLite refuses the player context for a pawn not of the colony**, which is why the peace spells keep the AI
  context with Rimfolk's hook rather than switching context.

## To check in the game

The dev action *Magic: casting on their own, the colonists here (log)* for Myst (Arcane Bolt and Shadow Strike known);
the colonist card's tags (Myst a mage and a fighter, Cait a fighter); a visit by a priest or druid of the peoples when a
colonist is hurt (the dev actions of `PeoplesMagicDebug`), who should now walk over and cast; the hooks list in Rimfolk's
settings, where `Patch_PeoplesMagicPeaceCast` should show as applied on 1.7.

## Mods that would help

From the player's own catalogue first (`docs/player-mods/mods.tsv`): RWoM Core, RWoM Integrations, JecsLite and the
continuation are all active and loaded in the order the continuation asks (JecsLite, Core, the continuation, then the
integrations); Fairycore - Magic Wand and Vanilla Psycasts Expanded are active and need nothing. The original A RimWorld
of Magic (1201382956) is still subscribed but not loaded; with the same package id as the continuation it is safest
unsubscribed, so the game can never pick the wrong copy.

Nothing else is needed for these integrations. RWoM Integrations names Ability AI (package `saintwacko.AbilityAI`) as an
optional mod that takes over the mod's own auto-cast; Rimfolk's casting on their own would then stand down whenever it
casts for a colonist. Its Workshop page couldn't be found here, so it is not recommended until it can be checked.
