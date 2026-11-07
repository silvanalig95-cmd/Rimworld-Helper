# The player's mod list (7 November 2026)

The player started a new game with a new mod list (their `ModsConfig.xml`, RimWorld 1.7.5000 rev1275). Kept here
for reference when building: what Rimfolk meets, what it can lean on, and what overlaps. 303 active mods, all five
DLCs (Royalty, Ideology, Biotech, Anomaly, Odyssey). Rimfolk itself is `silvanalig.colonycouncil`, loaded late, after
Real Ruins and before RimSaves.

## Rimfolk's soft integrations that are active

| Mod | Package id | What Rimfolk does with it |
|---|---|---|
| Hospitality | `orion.hospitality` | guests and visitors (settings page "Your other mods") |
| RimMarket | `forti.rimmarket` | stall goods in the journey planner and visitors' trade |
| Alpha Skills | `sarg.alphaskills` | a drink or a smoke before work |
| Alpha Genes | `sarg.alphagenes` | a covered face hides looks |
| A RimWorld of Magic | `torann.arimworldofmagic` | golem repair, its need rules |
| Faction Territories | `jaeger972.factionterritories` | territory news, the town's growth (15B), tile facts |
| Move the Patient | `konstantynopolitaneczka.movethepatient` | medical triage |
| PokéWorld Updated [1.6] | `themoonitself.pokeworld` | Pokémon in Rimfolk's animal events, sounds (0.125.0, 0.126.1). Found by its species, so its new package id needs nothing. |
| Giddy-Up | `memegoddess.giddyup` | the wish for a mount |
| Vehicle Framework | `smashphil.vehicleframework` | a vehicle caravan stopped at a place may settle there |
| Vanilla Base Generation Expanded | `vanillaexpanded.basegeneration` | stood down for a town the colony was, whose map is its kept layout (0.128.0) |
| Alpha Animals | `sarg.alphaanimals` | the strange creature event |
| Vanilla Expanded Outposts | `vanillaexpanded.outposts` | read per home |
| Humanoid Alien Races | `erdelf.humanoidalienraces` | its races count as people wherever Rimfolk reads `Humanlike` |

Also read by name somewhere in Rimfolk's defs or code (each optional): Vanilla Factions Expanded Settlers, Classical
and Empire, Alpha Memes, Alpha Biomes, Alpha Props and Parks, the VE skills, traits, social interactions, cooking,
brewing, ideology memes and expanded animals, Dubs Bad Hygiene, Gastronomy, Therapy, Simple Sidearms, Seasonal Weather,
Geological Landforms, Snap Out, RimHUD, Bubbles, Factional War, Diplomatic Marriage Plus, Dinosauria, Megafauna, More
Vanilla Biomes, the elves, merfolk and the Avaloi, Boglegs, Stoneborn and Venators races, Regrowth.

## Changed since the last list Rimfolk was built against

- **Medieval Overhaul is gone**; **Vanilla Factions Expanded – Medieval 2** (`oskarpotocki.vfe.medieval2`) is there.
  The hand-over's medieval people (0.127.0) lends the pawn groups of any loaded medieval people, so VFE Medieval 2's
  kingdom is lent in its place.
- **PokéWorld** is now `themoonitself.pokeworld` (PokéWorld Updated [1.6] [ALPHA], workshop 3540803945).

## The world's peoples with this list (for new factions)

Many peoples already: the five DLCs' own; Vanilla Factions Expanded's Settlers, Pirates, Insectoids 2, Empire, Tribals,
Medieval 2, Classical and Deserters; Rimsenal's Federation, Feral, Spacer, Sophian, Urb and Askbarn; the races with
peoples of their own (Vanilla Races Expanded's Archon, Fungoid, Genie, Highmate, Hussar, Lycanthrope, Phytokin, Pigskin,
Sanguophage, Saurid, Starjack and Waster, the elves, merfolk, Avaloi, Boglegs, Stoneborn and Venators, and the
androids). Factional War lets them destroy each other; Faction Territories draws their lands; Diplomatic Marriage Plus
offers marriages between leaders (the player's choice; Rimfolk ties no mechanics of its own to family lines).

So new Rimfolk peoples fill what is missing rather than repeat these: peaceful callings (traders, healers, herders;
then pilgrims, scholars, a spacer enclave that lives on the ground, unlike Odyssey's orbital Traders Guild, and river
folk), and peoples that arise during play.

## Art Rimfolk can draw on

VE Apparel, Armour, Weapons (and its Coilguns, Heavy Weapons, Grenades, Quickdraw, Laser, Bows, Tribal and Medieval
sets), Rimsenal's hair, the VE ideology icons and symbols, the Sophian style, VE Props and Decor, Alpha Props and
Parks, More Floors, the VRE xenotypes' genes, and Alpha Genes. A new people's look is built from these (each softly:
only when loaded), with the base game's and DLCs' as the fallback.

## Animals with a call of their own

The player uploaded the animal and sound defs of every installed mod (7 November 2026). Of the active ones, 66 real
animals have grown ones with a call of their own rather than the base game's again: Alpha Animals (43), ReGrowth 2 (4),
ReGrowth: Boiling (1), Vanilla Animals Expanded (8) and its Endangered (3) and Waste (2) packs, and Vanilla Genetics
Expanded (5, all reusing Alpha Animals' calls). They are listed in `docs/player-animals.tsv`, which PokéWorld's voices
(`PokeWorldSoundsMath.Voices`) and their tests read. Megafauna, Dinosauria, VFE Insectoids 2 and the rest reuse the base
game's calls; the demigryphs and Dinosauria's hunters have no grown call at all.

## Worth knowing

- Two facial animation mods are active (`nals.facialanimation` and `andromeda.facialanimation`). If faces look
  doubled or flicker, check that one is a texture add-on for the other.
- Real Ruins, Vanilla Quests Expanded and VE Outposts add world objects Rimfolk's region reads as places, not towns.
