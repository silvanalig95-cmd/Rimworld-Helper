# The player's mod pool

What every mod the player has subscribed to adds, read from their own files (7 November 2026: the player ran
`Collect-ModDefs.ps1` on their computer and uploaded the result; 444 mods subscribed, 297 of them active in their mod
list, plus the game's own data and all five DLCs). Built by `python3 Tools/mod_pool.py <collected folder>
docs/player-mods <save.rws>`; run it again when the player sends a new collection.

**Build from the active rows.** Each table has a column `active`: yes when the mod adding the def is in the player's
mod list (`ModsConfig.xml`; the game's own data and the DLCs always yes). The player's rule: "always consider the
actual mods i have enabled in my save and mod list ... but of course include all of them in the catalog, compatible
with 1.6 or 1.7". So:
- Content that picks things (gear, weapons, items, xenotypes and races, animals, magic and abilities) draws on the rows
  with `active` yes, softly (`MayRequire` or by name in code) with a vanilla fallback.
- The inactive rows are there for reference, never as a dependency.
- Only mods that support 1.6 or 1.7 are catalogued; `mods.tsv` lists the others with `catalogued` no.

The tables (one row per def, after resolving `ParentName` inheritance; patches are not applied, so a tag a mod adds to
another mod's apparel by patch isn't seen). `MayRequire` and `MayRequireAnyOf` are honoured as the game honours them for
the player's mod list (the game and its five DLCs always count as loaded; package ids compared without case or a
`_steam` suffix): a def that names a package not loaded is left out (Melee Weapons For All's tenebral, kwazaari and solar
weapons need BOR), so is every element inside a def that does (a weapon tag, a comp, a recipe's research), and an
abstract parent with such a `MayRequire` is not there to inherit from (9 November 2026; until then all of them were read). Only the folders RimWorld 1.7 loads are read: a mod's `LoadFolders.xml` for
1.7 (or the newest version before it), its `IfModActive`, `IfModActiveAll` and `IfModNotActive` read against the
player's list, else its root, `Common` and its newest version folder; the mods in the player's load order, so a later def
of the same name wins as in the game, and a def of a mod not in their list never stands for one that is (Combat
Extended's javelins are not the game's pila). Until 7 November 2026 every version folder was read, so a mod's 1.4 or 1.5
defs could stand for its 1.6 ones (Vanilla Weapons Expanded's bows and guns had their old tags, Rimsenal's old guns were
listed).

| File | What | Columns besides defName, mod, active |
|---|---|---|
| mods.tsv | every mod subscribed | name, folder, active, load order, in the uploaded save, supported versions, catalogued |
| apparel.tsv | clothing and armour | label, techLevel, apparel tags, layers, body part groups, stuff, market value, trade tags, how often the game picks it (`generateCommonality`, blank for 1) |
| weapons.tsv | weapons | label, techLevel, weapon tags, ranged or melee, stuff, market value, trade tags, how often the game picks it, tradeability, comps, `madeTech`, the tools' capacities (cut, stab, blunt) |
| items.tsv | everything else that is an item | label, techLevel, thing categories, trade tags, market value, and what Rimfolk's things by role read (Things/ThingRoles.cs): category, stack limit, tradeability, food preferability and type, drug category, meat's source, nutrition, chemical, teetotaler, days to rot, comps, stuff kinds and beauty, medical potency, what it is made of, size, designation, minified, `madeTech`, work to make, terror |
| races.tsv | humanlike races, animals | label, kind, body size, food type, predator, herd animal, max prey size |
| xenotypes.tsv | xenotypes | label, inheritable, gene count, description |
| genes.tsv | genes | label, display category, metabolism, complexity |
| traits.tsv | traits | degree labels |
| abilities.tsv | abilities of any def type (psycasts, A RimWorld of Magic's, genes') | def type, label, category |
| pawnkinds.tsv | pawn kinds | label, race, faction, combat power, apparel tags, weapon tags |
| factions.tsv | factions | label, techLevel, hidden, permanent enemy, basic member kind |
| memes.tsv | Ideology memes | label, category, exclusion tags |
| buildings.tsv | buildings by what they are for (8 November 2026, for the camps: Camps/CampKit.cs): beds, bedrolls, tents, sleeping spots, fires, hearths, braziers, torches and lamps, tables, seats, work spots and benches, walls, doors, fences, storage, spots and hitching spots | label, techLevel, role (bed, bedroll, tent, sleepingspot, doublebed, medical, animalbed, crib; fire, hearth, brazier, torch, lamp; table, seat; workspot, bench; wall, door, fence; storage; spot, picket), size, stuff, stuffCount, cost, research, researchTech (the age of the research it needs, `Undefined` for none), minifiable, designation, thingClass, power, fuel, glow, heat, holdsRoof, passability, work (the work done at a bench: butcher, cook, craft, art, medicine), workToBuild |

The buildings table was added on 8 November 2026 from the same collection, with the save of that day: every other table
came out byte for byte the same, but for `mods.tsv`'s column inSave for Harvest and Haul (in the 7 November save, not in
this one).

`weapon_numbers.json.gz` (9 November 2026, for the weapon rating: `docs/weapons`) holds every weapon of the active mods
with the numbers the game resolves for it: its verbs and each verb's projectile (damage and armour piercing by the game's
own rules, stopping power, explosion, fuse, extra damages), its melee tools with each capacity's damage, its stat bases and
the stats it changes on its wielder, its comps and mod extensions (every simple field), the abilities it grants (resolved
AbilityDefs), the stuff it is rated in, the research its recipe needs, why it is left out if a colonist can't wield it,
and the active mods' patches that touch it (a `PatchOperationFindMod` followed as the player's mod list decides; patches
still not applied). Built by `python3 Tools/weapon_extract.py <collected folder> docs/player-mods/weapon_numbers.json.gz`,
which reads the collection through `mod_pool.py`'s own loader (its `Pool`); `Tools/WeaponReview` rates it.

`madeTech` is the age of the easiest way the player's active mods make or grow the thing: the tech level of the
research its recipes (or recipe maker, or a sown crop) need, `Undefined` for one that needs none, blank when nothing
makes it (a fermenting barrel's or a mod's processor's product, a ruined grill).

The uploaded files themselves stay out of the repository (they are other modders' work and about 470 MB unpacked).
