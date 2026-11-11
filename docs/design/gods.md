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

## Drama between the gods, and the ones who don't fit (the player, the same day)

The player: "when I think of gods as kind of one reference, you know, it's the Greek pantheon. In the sense that ...
there's drama between the gods. I'm not saying that every god must be Greek or must fit to this ... reference, but ...
the general idea. But there could definitely be also outliers that work differently or have really different way of
interacting, being".

- **Ties between gods** (core: saved in `GodBook`, shown on the Gods page):
  - rivals (Storm and Harvest over the rain, the Hunter and the Herdmother over the beasts);
  - lovers, and lovers estranged;
  - kin: parent and child, siblings, twins;
  - allies, and old wrongs (one cast the other down, one stole the other's people).

  The ties come from the domains (a table of natural rivals and allies), from the faiths that share or fight over them,
  and from chance, and they shift slowly. Favour with a god a little displeases its rivals and a little pleases its
  allies. The colony can be caught between two gods.
- **Each god's nature**: proud, jealous, generous, capricious, wrathful, patient, playful, cold or grieving. It decides
  how fast favour moves, what offends and how loudly the god answers.
- **The drama, played out** (stage 2):
  - two gods' quarrel seen in the weather, a storm against a drought in turns;
  - a god asking the colony to slight a rival, or to hide someone a rival hunts;
  - a rival's avatar coming to undo a blessing;
  - a feud between peoples that is really their gods';
  - a reconciliation the colony brokers at a festival;
  - myths of the gods told at suppers, in letters and at camps, as the history of their ties grows.
- **The ones who don't fit** (core marks them; stage 2 gives them their ways):
  - **The archotechs:** cold and exact. They bargain in terms, keep every term, and care nothing for offerings, only for
    deeds that serve their aims.
  - **The Hunger** (the void): answers anyone, always pays, and always takes more.
  - **A trickster:** lies, swaps blessings for curses and back, and loves a joke at a priest's expense.
  - **A dead god:** worshipped by a few, silent, its mark found in old places. Its faithful hope.
  - **A new god born from the colony's own faith:** small and weak, growing with the colony's devotion.
  - **A fading god:** no worshippers left, bitter or gentle.
  - **A god imprisoned:** the Sleeper in the Sunken Vault, who wants out.

## A RimWorld of Magic, heavily used (the player, the same day)

The player: "the rim world of magic models, of course, lend itself as an essential component ... with all these spells
and effects ... and sounds and sprites and stuff, so you should really heavily use this ... it also, of course, fits
thematically as well." It is active in their game (`torann.arimworldofmagic`). Its 1.6 defs and DLL are in the
scratchpad from their earlier upload (`moddefs/upload/x/Mods/1201382956/v1.6/Defs`, `modcode/Mods/1201382956/v1.6`).

- **The presence:** its 131 motes, effecters, weathers and arcane conditions, by name, softly, with a vanilla fallback:
  holy light, summoning and arcane circles, glowing runes, light barriers, twinkles, flowers, spirit wolves, ghosts and
  bone dust, shadow cloud, lightning, frost, regeneration.
- **Gifts (stage 2):** a god grants a RoM class fitting its domain:
  - Priest or Paladin for the Healer;
  - Druid for the Green;
  - Ranger for the Hunter;
  - Geomancer for the Deep Mother;
  - Necromancer or Death Knight for the Ferryman and the Hunger;
  - Storm Born for the Storm-father;
  - Enchanter for the Smith;
  - Technomancer for the Machine;
  - Bard for the Night;
  - Gladiator or Monk for War.

  Or a single spell, or mana and a magic power upgrade as a blessing. Its golems and summons can be the shapes some
  avatars take.
- **Worship (stage 2):** RoM's priests and their spells take part in the rites and prayers. A god's favour can raise a
  caster's mana regeneration or cut it off.

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

## The patrons of Rimfolk's peoples (retired with the generated pantheon: who reveres whom is now the gods' defs, last section)

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

**Core: the pantheon** (`Source/ColonyCouncil/Gods/`, `Defs/GodDefs/Rimfolk_Pantheon*.xml`; not `Gods/Presence/`). Built as 0.215.0; its generated pantheon, patrons, Gods page and symbols are retired: the core is now the gods as defs, unseen, and the colony's own book (last section).
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
- (Retired: the Gods page and the symbols below; the colony's own book replaces the page, and the gods have no symbols.) A **Gods page** in the Region window's Peoples tab (or a tab of its own): every known god with its symbol, its people
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
  - its own card: dark paper and gold, no symbol (retired: the god in words), no "good news" or "bad news" tag; plain or "some say" by the
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

## The gods (the player's choice, 11 November 2026): the contract for the rework

The player approved the fourth brainstorm with its new names ("alright, great! you can go ahead with full
implementation"), then, the same day, made the gods an unseen presence and the book a thing of the colonists' own:
- "the gods should also feel like an ominous presence. To me, as the player as well, and there should be more kind of
  seen as a sort of storyteller style aspect of the game, giving impact, and generating stuff and appearing or not or
  reacting, but not in a way that I see what's going on or can, read about it ... if I look at the gods in like a fancy
  list in the menu, it kind of takes out some of the their ominous presence";
- "information about the gods should only ever be like from the point of view of a the pawn. what they know,
  experienced, heard, believe, feel like doing etc. in that case a book or overview can exist and develop, but I just
  never want this 'meta' level player information ... the gods should be above me as the player as well as the pawns,
  and what i learn and see about them is the same as my pawns do";
- "I mean in events etc the god can be named, if we know it's them that caused something";
- "if the gods are on the map then i definetely want to see them and they should have a godly presence, unless they are
  actually hiding".

Five more joined the nine: the sun and the moon (Helios and Selene, the Gift and the Return), a cat (Mog), the rain toad
(Murmur) and the restless gold (Ducat). Their characters are `docs/gods-pantheon-brainstorm.md`. The generated pantheon
(gods from Ideology's deities and Rimfolk's name makers, the patrons, the Gods page) is retired.

What every part builds to:
- **The rule: the gods are unseen, like a storyteller.** The player sees what happens on the map and what people say,
  never what a god thinks, wants or feels about the colony. No page or list of the gods, no favour words in the news, an
  inspect line, a tooltip or This week, no news line naming a god's mind, no Almanac page of their ways (the page "the
  gods" is one paragraph on the settings). Favour, ties, natures, kinds and what the colony knows are hidden state that
  drives what the gods do.
- **A god is a def.** `ColonyCouncil.GodDef`, defNames `Rimfolk_God_Hollin`, `_Smoulder`, `_Bellwether`, `_Morel`,
  `_Ennu`, `_Pip`, `_Cirrus`, `_Hush`, `_Undertow`, `_Helios`, `_Selene`, `_Mog`, `_Murmur`, `_Ducat`. A `God` record
  keeps its runtime state (favour per home, acts, known), and `God.name` is the short name ("Hollin"). `God.def` reaches
  the def, and `Gods.ByKey("Hollin")` finds a god by its short name.
- **The `Gods` API stays.** The same signatures, fed from the defs:
  - each def sets a domain and a second domain (the stage 2 fallbacks only), a nature and a kind;
  - its ties are authored, each with words of its own (one sentence, as people say it);
  - Undertow is the Hunger kind, and the rest are Ordinary;
  - Helios and Selene are tied as the gift and the return (`GodTieKind.Return`): favour meant for Helios reaches
    Selene, and Helios (`unmoved`) is never moved by favour.

  Domains as the fallback key: Hollin Wild and Harvest; Smoulder Craft and Hearth; Bellwether Hunt and Wild; Morel Dead
  and Harvest; Ennu Way and Hearth; Pip Machine and Craft; Cirrus Storm and Night; Hush Night and Dead; Undertow Void;
  Helios Hearth and Harvest; Selene Night and Healing; Mog Hearth and Hunt; Murmur Waters and Harvest; Ducat Way and
  Craft.
- **Who reveres whom** is the defs': Rimfolk's 23 peoples and some of the player's mods' factions by name (a people a
  def names reveres only the gods that name it), the rest by kind (Tribal, Medieval, Industrial, Spacer, Raiders, Empire,
  Cannibal). The colony holds the gods of its own age's kind. `Gods.Of(faction)` and `ColonyGods(home)` read it.
- **Per-god data in the stage 2 parts is keyed by the short name** (`Hollin` ...) in their own defs (`GodShapeDef`,
  `GodWaysDef`, `GodWorshipDef`), with the domain tables as a fallback only. Each part writes its entries from the
  brainstorm's characters, and in people's words:
  - who can come as a stranger: only Ennu takes human shapes;
  - who has a body: Bellwether, Mog (a gray cat on the map, never owned or tamed), Murmur (rarely, a house-sized toad in a
    marsh, Odyssey's `ColossusToad` larger, softly) and Smoulder (a shadow only);
  - who acts only through plants, rot, machines, sky, frost, light, gold or the dark;
  - what each one likes, refuses, gives and does.
- **Named only when the colony knows.** `Gods.CardName(god, home, sign)` returns the colony's name for a god when the
  colony knows something was its doing, else null (unnamed, or "some say" where it is only suspected). It knows when:
  - the gods walk openly (the realness setting Walking);
  - it saw the god act plainly: a manifestation witnessed, a disguised god revealing itself (`GodLore.MarkActing`, done
    by `Gods.Moment`);
  - it learned that sign as the god's own: `GodLore.LearnSign(god, sign)`, called by a part when a teller explains it,
    and done when a story that teaches it (`GodStory.teaches`) is heard whole.

  Events, cards and letters use it. The name is the colony's (its page in the colony's book).
- **The colony's own book** (`GodLore`; the region window's "Gods' book", address `Gods`). Every entry is something a
  colonist saw, was told, believes or felt, dated, with its witnesses (colonists by name) or its teller (a visitor, a
  neighbour, a colonist), in the voice of the book's keeper (the chaplain, else Ideology's moral guide, else the most
  devout, else the most lettered; with none, short and plain). Beliefs sit side by side and contradict; mistakes are
  written as believed and the truth is never marked. A god appears only under the names the colony heard it by, each a
  page, until a colonist works out or a teller says that two names are one. What feeds it, and the API the parts call:
  - `GodLore.Saw(pawns, god-or-null, words)`, `GodLore.Heard(teller, god-or-null, words)`,
    `GodLore.Believes(pawn, god, words)`, `GodLore.Dreamt`, `GodLore.Prayed`;
  - every act given to `Gods.Remember` with no witnesses gets them (`GodAct.witnesses`, `GodAct.teller`): the colonists
    awake and in sight within 20 cells of its cell, or everyone awake on the map for a sign with no place. An act nobody
    saw stays in the hidden history and never reaches the book;
  - a god's moment through `GodPresenceSeam.Call` reaches it as those who saw it saw it.
- **The stories reach the colony through people, in pieces** (`GodTales`): one-sentence pieces, in order, never all at
  once, at the evening fire or at supper (a believer to someone near; the keeper's as a sermon), from a visitor near a
  colonist (by their own people's names for their gods), in a neighbour's letter (a story retold whole, `{GODTALE}`; a
  god's hand at the colony heard of, `{GODHAND}`), only once the words are really in the letter. `GodTales.Tell(teller,
  listener, gods, how)` lets a part (a priest, a dream told) have someone tell the next piece.
- **The gods' hands in the game's events** (the brainstorm's section C; `GodHands`, `GodHandsMath`): people's words only.
  A believer's one-sentence bubble naming whose hand they take it for (maybe wrongly; a god they know of, one they
  confuse it with, or "something"), a scoffer's answer while the gods are felt, nobody doubting it when the colony knows
  (`CardName`); the act in the hidden history and the book; now and then a neighbour's letter later. The game's own
  letter is never altered, and no news line is told. Rare: a god keeps quiet 20 days after, a home 4 days, and each event
  has its own chance. Selene's hand is seen only at night.
- **No symbols for the gods**, anywhere: not in the book and not on the god's card. A god is shown in words. On the map
  a god that isn't hiding keeps its full presence, and the label over it follows the colony's knowledge: its name in the
  book once someone has heard of it, else what is seen of it ("a great white thrumbo", `GodDef.unheard`). A disguised god
  shows only the name it gives. The card is cryptic, in the world's voice, with no why, no favour and no rules; a felt
  god's card is unnamed unless the colony knows (`CardName`).
- **The realness setting** stays (Believed, Felt, Walking); the dev actions stay (dev mode only).
