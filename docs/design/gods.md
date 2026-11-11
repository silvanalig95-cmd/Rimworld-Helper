# Gods: the design (11 November 2026)

The player's words, in order:
1. "the concept of gods within RimWorld ... like a pantheon of gods ... Maybe some nature or animal god could also be
   actually on the map, or like a god that is acting like a guest and then reveal himself and have some humanoid form ...
   that they actually are and interact and are into the lore and get referred to, and pawns react to gods. And I'm sure
   there's many ways to create interactions with all the different systems that we have created so far."
2. "they should then always carry some kind of weight and ominosity with them ... like a godly presence ... imposing
   sound effects or really kind of epic ones when a god appears or interacts or does something ... I don't necessarily
   want to have some, you know, cheap generated soundtrack."
3. "if I look on the map ... and I see a regular pawn and a god next to it, the god should ... clearly have something
   about it that shows its godly presence, unless it's actually trying to hide as such ... So not that I just have a
   regular pawn on the map and when I click him I coincidentally see ... oh, he's a god. Not even a regular guest."
4. "I just want you to get going ... I have confidence in your initial draft ... go for it. And you can really spend
   effort and tokens into the subject."

The brainstorm is `docs/gods-brainstorm.md` (items 1 to 34). This file is the design the agents build to: the open
questions answered by the lead's defaults, the parts, who builds what, and the contract between the parts.

## Decisions (the lead's defaults; the player can change them later)

- **How real** (brainstorm 1): a setting "How real are the gods", with Believed, Felt (default) and Walking.
  - Believed: nothing supernatural; prayers, rites, omens read into chance, and no avatars.
  - Felt: gods act, but never so plainly that nobody could doubt it. Avatars come, mostly in disguise or as sacred
    animals, and leave before anyone can be sure. The card says "some say" and colonists disagree.
  - Walking: gods manifest openly, are named on the card, can be spoken with, and miracles are plain.
  Every feature reads `Gods.Realness` and takes the form of the step.
- **One pantheon per world, many faiths** (brainstorm 2 to 4):
  - Each ideoligion's deities are real figures, with a domain from the deity's own words and the faith's memes and
    precepts. Without Ideology, each faction gets two or three gods from Rimfolk's own pantheon makers.
  - Rimfolk's own peoples each have a patron by their lore (the list below). The patron is one of their faith's gods
    where it fits, else added to it.
  - Gods take the form of their people's age: spirits for tribes, old gods and saints for the medieval, half-forgotten
    gods and gods of the machine for the industrial, archotechs for spacers (RimWorld's own god-like minds).
- **The colony doesn't pick a patron.** Favour follows what it does: offerings, rites, kept vows, deeds in a god's
  domain, and harm to its sacred things. The god it stands best with is "the colony's patron" in words. A shrine can be
  dedicated to any god, which counts as choosing.
- **Favour is words, never numbers, in play.** Each god has a standing with each of the colony's homes, in words: wrathful,
  displeased, unknown, noticed, pleased, favoured, beloved. Favour drifts slowly back toward unknown.
- **Weight** (brainstorm 23 to 28): a god's coming is rare (a few times a year at most), built up with omens, and made
  an occasion. The music stops, then a sting and a chosen track; the sky tints, the wind dies, everyone turns, the game
  drops to normal speed and the camera goes there; it has its own card; it leaves a mark on the land and in the history.
- **Sound and music** come only from the game's own and the player's mods' assets (the defs in `docs/player-mods`, the
  active rows), plus an optional folder the player fills (`Sounds/Rimfolk/Gods/<domain>/`). No generated music.
- **A god always looks like one** (brainstorm 29 to 34) unless it hides on purpose:
  - a little taller, coloured in its own colours, in recoloured robes, crowns and masks no mortal wears;
  - a pulsing aura and rising motes in its colour, with a moving light;
  - the ground answering where it walks, the world turning toward it, and its name in its own colour.

  A god in disguise shows small tells to the observant, and transforms at the reveal.
- **Rules as everywhere** (CLAUDE.md):
  - Harm only at the game's own rate, through Rimfolk's shared harm and illness budgets, and always shown.
  - Every age.
  - The colony is the family: no dynasties.
  - Soft integrations: Ideology, Anomaly, Royalty, Biotech, Odyssey, RimWorld of Magic, Vanilla Psycasts Expanded,
    Alpha Memes, Hospitality and Giddy-Up are all optional.
  - Words match the map, speech goes through `Speech.Say`, and the full mod pool is used.

## Domains

Harvest (and rain), Storm, Hunt (and beasts), Healing (and sickness), War, Craft (the forge), Way (roads, trade and
travellers), Dead, Night (moon and dreams), Hearth (fire and home), Wild (the green and growing things), Waters (rivers
and sea), Machine, Void. Each has:
- two colours;
- a symbol;
- an avatar form (human, or an animal kind);
- a trail;
- a sound mix;
- tracks;
- sacred things: an animal kind, a plant, a place, a deed.

## The patrons of Rimfolk's peoples

| People | Patron |
|---|---|
| Road league | the Way |
| Hospitaller order | the Healer |
| Herding folk | the Herdmother (Wild and Hunt) |
| Pilgrim fellowship | the Far Light (Way and Night) |
| Learned society | the Lamp (Craft and Night) |
| Spacer enclave | an archotech, the Broker (Machine) |
| River folk | the River (Waters) |
| Lantern Wardens | the Hunter |
| Quiet Bell | the Ferryman (Dead) |
| Circuit of Tamers | the Herdmother |
| The Wheel | the Tinker (Craft and Machine) |
| Stormglass Conclave | the Storm-father |
| Mossborn | the Green (Wild) |
| Ashen Banner | the Red Captain (War) |
| Kiln Guild | the Smith (Craft) |
| Signal Line | the Voice in the Wire (Machine) |
| Deepwell Union | the Deep Mother (Craft and Dead) |
| Last Republic | the people's own Lady of the Vote (Hearth); an industrial people half-forgets her |
| Hollow Fleet | the Silent Captain, an archotech (Machine and Void) |
| Helix Works | the Weaver, an archotech (Healing and Machine) |
| Lattice | the Lattice-mind (Machine) |
| Choir of the Spire | the Hunger (Void) |
| Sunken Vault | the Sleeper, an archotech dreaming in the vault (Machine and Night) |

The core agent may tune the names to the peoples' lore files.

## The parts and who builds what

### Stage 1 (two agents side by side)

**Core: the pantheon** (`Source/ColonyCouncil/Gods/`, `Defs/GodDefs/Rimfolk_Pantheon*.xml`; not `Gods/Presence/`).
- `GodDomain` (the enum above), `God` (a saved record):
  - id, name, epithet ("the Storm-father", "Who Walks the Dead"), domain and a second domain;
  - the age form, the people or faiths that hold it (faction and ideoligion refs), the Ideology deity it came from;
  - two colours, a symbol, the look's defName (`Rimfolk_GodLook_<Domain>`, which the presence part defines).
- `GodBook`, saved with the game: the pantheon, built once per world (and for an older save on load), and kept as
  factions and ideoligions come and go. The colony's favour per god per home, its history of the gods' acts, and its
  vows.
- `Gods`, the public face every other part uses:
  - `All`, `Of(Faction)`, `OfIdeo(Ideo)`, `ColonyGods(Map home)`, `Patron(Map home)`;
  - `Favour(God, home)` in words, and `AddFavour(God, home, amount, why)`, which the history records;
  - `Realness`, the setting;
  - `Remember(GodAct)`, the history of acts;
  - `Name`, `Epithet`, `SwearBy(pawn)` (which god a pawn would swear by), and placeholders `{GOD}`, `{GODEPITHET}`,
    `{GODOF}` for text (taught to the DefValidator).
- A **Gods page** in the Region window's Peoples tab (or a tab of its own): every known god with its symbol, its people
  and faiths, its domain, the colony's favour in words, and the acts remembered. Also an Almanac page.
- The **symbols**: one per domain, drawn by `Tools/make_god_symbols.py` (white on transparent, tinted in code), as the
  mailbag and the thermometer were.
- The setting "How real are the gods" and a master setting "Gods" (on by default).
- **Seams the later parts call**, to be defined by Core:
  - `Gods/GodPresenceSeam.cs`: `public static Func<PresenceSpec, bool> Manifest` (null until the presence part sets it)
    and the plain `PresenceSpec` class: god, map, cell, kind (Appear, Act, Reveal, Depart, Blessing, Wrath, Omen),
    avatar pawn or null, card title and text, plain or veiled. Calls do nothing when Manifest is null.
  - `GodLookSeam`: `public static Action<Pawn, God, bool disguised> MakeAvatar`, set by the presence part likewise.
- Tests, the Almanac page, DESIGN.md, README.

**Presence: how a god looks and comes** (`Source/ColonyCouncil/Gods/Presence/`, `Defs/GodDefs/Rimfolk_GodLooks.xml`,
`Textures/ColonyCouncil/Gods/`, `Tools/make_god_auras.py`). It depends on nothing of Core's yet: it builds against its
own `GodLookDef`, and the stage 2 merge wires it to Core's seams.
- `GodLookDef`, one per domain (`Rimfolk_GodLook_<Domain>`) and an age variant where it matters:
  - two colours, a glow colour and radius, a size factor;
  - the apparel to wear, recoloured: the game's own robes, crowns and masks, and the player's mods' first;
  - a trail (flowers, frost, scorch, rain, ash, sparks, water), motes, sky colour, wind;
  - a sound mix (game SoundDefs: stings, thunder, pulses, Royalty's bestowing, Alpha Memes' chants, Anomaly's) and
    tracks (game SongDefs: Anomaly's, Odyssey's, Ideology's), each soft;
  - the avatar animal kinds for the domain (stag, wolf, bear, raven, thrumbo; the player's mods' animals first).
- The **avatar**: any pawn or animal made a god's, by a hediff and comp (`Rimfolk_GodAvatar`) that keeps its look:
  - drawn larger;
  - recoloured: skin, hair and eyes, or an animal's whole body;
  - an aura drawn under and behind it (a soft halo and ring, pulsing, from the aura textures), rising motes, a moving
    light (a glower that follows);
  - its trail on the ground it walks;
  - animals and colonists near it stopping to face it, with one-sentence bubbles;
  - its name over it in its colour, a stately walk;
  - never sold, stripped, recruited, tamed, eaten or arrested.

  A disguised avatar shows none of that but its tells: no footprints in snow, animals following, a shimmer at night,
  remarked on by a colonist with good sight or a psychic gift. `Reveal` transforms it in front of everyone.
- The **moment** (`GodPresence.Manifest(spec)`):
  - the build-up: an `Omen` kind, small and spread over days by the caller;
  - the music fades, a beat of silence, the sting, then the track;
  - a short presence condition tints the sky and stills the wind;
  - everyone on the map stops and turns toward it (a short job), the devout kneel, animals lie down or flee;
  - the game drops to normal speed, the camera jumps there;
  - its own card: dark paper and gold, the god's symbol, no "good news" or "bad news" tag; plain or "some say" by the
    step;
  - the mark it leaves: a ring of scorched or flowering ground, a spring, a tree, a standing stone. The mark is the
    game's own things, never a new building that needs art.
- Dev actions to make an avatar of each look and play each kind of moment.
- Tests, DESIGN.md, README.

### Stage 2 (three agents, after both stage 1 parts are merged and wired)

- **On the map:**
  - the stranger at the gate (brainstorm 7: a Hospitality guest when it's loaded, else Rimfolk's own visitor);
  - the sacred animal (8, on the legend-beast trigger of the peoples' round 1);
  - weather as a voice (9);
  - the omens' build-up (23);
  - the marks (26) in the history and at suppers.
- **Worship:**
  - shrines and offerings (11, on the game's altars and sculptures and Ideology's shrines);
  - prayer (13);
  - festivals in the calendar (14, on Rimfolk's feasts);
  - vows (15, wishes and life goals);
  - a priest office and the council's decisions and dilemmas (16);
  - dreams and visions (10);
  - the heart: faith, a crisis of faith, scoffers and converts (20).
- **The world:**
  - letters and diplomacy (17);
  - peoples and camps (18): pilgrims to a sacred site, a hermit seer, a war god's champion;
  - health and wrath at the game's rate (19);
  - relics and gifts, with RimWorld of Magic's classes and spells and Vanilla Psycasts Expanded's paths (21);
  - the void as a dark god with Anomaly (22).

Each stage 2 part calls only `Gods` and the two seams, so they run side by side.
