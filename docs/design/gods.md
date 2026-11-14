# Gods: the design (11 November 2026)

> **Read this first (11 November 2026).** Most of this file describes the first design: a generated pantheon from
> Ideology's deities, domain patrons ("the Storm-father", "the Ferryman"), and a Gods page. All of that is retired.
> What holds now:
> - the nine of `docs/gods-pantheon-brainstorm.md` (Hollin, Smoulder, Bellwether, Morel, Ennu, Pip, Cirrus, Hush,
>   Undertow);
> - the sections at the end of this file: "The nine ... the contract for the rework", "The gods are unseen" and "The
>   colony's book".
>
> The parts in between are kept for the machinery they describe (favour as hidden state, how near each god is, the presence's look
> and moment, the stage 2 parts). Wherever they show the player a god's favour, symbol, name label or rules, the later
> sections overrule them.


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

- **How real: one way, felt; each god as near as what happens draws it** (the player, 12 November 2026: "yes, let's
  stick to one level and use felt as primary reference. i just want the 'walking'-exclusive options also to be able to
  appears, like smoulders true form. i guess the level could be dynamic, some gods might become more present depending
  on situations, relations, actions etc. also the 'cards' level on felt is great, the god should only be named if it's
  sure it was them"). The setting "How real are the gods" (Believed, Felt, Walking) is retired, with its branches and
  line tags.
  - The gods are felt: they act, but never so plainly that nobody could doubt it. Avatars come, mostly in disguise or
    as sacred animals, and leave before anyone can be sure. The card says "some say" and colonists disagree.
  - Each god's nearness (`GodNear`, `GodNearMath`; hidden, never shown) moves on its own, by the situation on the land
    (its shape's `nearWhen`: a drought for Murmur, a deep mine for Smoulder, a hard winter for Hush), its relations (a
    quarrel it is in, a tied god stirred) and what the colony does (offerings, harm, its book, a strong standing).
  - At nothing a god is exactly felt. Nearer, what only walking gods did can happen: a god seen plainly (and so named,
    as the colony is sure), its true form (Smoulder as Dragon's Descent's true dragon, Murmur as Odyssey's colossus
    toad), a stronger answer, an offering gone in a breath of light, less disguise. One god can walk plainly while
    another stays hidden.
  - Cards stay as felt: a god is named only where the colony is sure it was that god (`Gods.CardName`), else unnamed
    or "some say".
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
  - how near each god is to the colony: `GodNear.Of(god, home)` (the realness setting retired 12 November 2026);
  - `Remember(GodAct)`, the history of acts;
  - `Name`, `Epithet`, `SwearBy(pawn)` (which god a pawn would swear by), and placeholders `{GOD}`, `{GODEPITHET}`,
    `{GODOF}` for text (taught to the DefValidator).
- (Retired: the Gods page and the symbols below; the colony's own book replaces the page, and the gods have no symbols.) A **Gods page** in the Region window's Peoples tab (or a tab of its own): every known god with its symbol, its people
  and faiths, its domain, the colony's favour in words, and the acts remembered. Also an Almanac page.
- The **symbols**: one per domain, drawn by `Tools/make_god_symbols.py` (white on transparent, tinted in code), as the
  mailbag and the thermometer were.
- A master setting "Gods" (on by default). (The setting "How real are the gods" is retired: one way, felt.)
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
  - a short presence condition tints the sky and stills the wind: the god's own where its def declares one (round 2,
    below), the shared one otherwise;
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
  Helios Hearth and Harvest; Selene Night and Healing; Mog (Tamiu) Dead and Hearth; Murmur Waters and Harvest; Ducat Way and
  Craft.
- **Who reveres whom** is the defs': Rimfolk's 23 peoples and some of the player's mods' factions by name (a people a
  def names reveres only the gods that name it), the rest by kind (Tribal, Medieval, Industrial, Spacer, Raiders, Empire,
  Cannibal). The colony holds the gods of its own age's kind. `Gods.Of(faction)` and `ColonyGods(home)` read it.
- **Per-god data in the stage 2 parts is keyed by the short name** (`Hollin` ...) in their own defs (`GodShapeDef`,
  `GodWaysDef`, `GodWorshipDef`), with the domain tables as a fallback only. Each part writes its entries from the
  brainstorm's characters, and in people's words:
  - who can come as a stranger: only Ennu takes human shapes;
  - who has a body: Bellwether, Mog (remade as Tamiu: a great sand-gold cat on the map, never owned or tamed), Murmur (rarely, a house-sized toad in a
    marsh, Odyssey's `ColossusToad` larger, softly) and Smoulder (a shadow only);
  - who acts only through plants, rot, machines, sky, frost, light, gold or the dark;
  - what each one likes, refuses, gives and does.
- **Named only when the colony knows.** `Gods.CardName(god, home, sign)` returns the colony's name for a god when the
  colony knows something was its doing, else null (unnamed, or "some say" where it is only suspected). It knows when:
  - it saw the god act plainly: a manifestation witnessed, a god drawn near enough to show itself (`GodNear.Plain`), a
    disguised god revealing itself (`GodLore.MarkActing`, done by `Gods.Moment`);
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
- **No symbols for the gods**, anywhere: not in the book and not on the god's card. A god is shown in words. (Since 14
  November 2026, the player's pick: the colony's own signs, its people's drawing of what they believe and never a
  portrait, show in the god's chapter of the book once it tells what the god is, and on a card that names the god; not
  on shrines, not on the map, and not for a god the colony hasn't heard of: `Gods/GodSigns.cs`, DESIGN.md "The gods
  understood".) On the map
  a god that isn't hiding keeps its full presence, and the label over it follows the colony's knowledge: its name in the
  book once someone has heard of it, else what is seen of it ("a great white thrumbo", `GodDef.unheard`). A disguised god
  shows only the name it gives. The card is cryptic, in the world's voice, with no why, no favour and no rules; a felt
  god's card is unnamed unless the colony knows (`CardName`).
- **One way, felt** (since 12 November 2026; the realness setting retired): each god as near as what happens draws it
  (`GodNear`); the dev actions stay (dev mode only).

## The gods are unseen (the player's rule, 11 November 2026, binding for every part)

The player, the same day, on the book: "I don't even know if there should be a God menu like that. So please abandon
this for the time being and remove it. Because, yeah, the gods should also feel like an ominous presence. To me, as the
player as well, and there should be more kind of seen as a sort of storyteller style aspect of the game, giving impact,
and generating stuff and appearing or not or reacting, but not in a way that I see what's going on or can, read about
it ... if I look at the gods in like a fancy list in the menu, it kind of takes out some of the their ominous presence."

- **Never shown:**
  - what a god thinks, wants or feels about the colony;
  - no gods menu, page, book, list or Almanac page;
  - no favour words in the news, inspect lines, tooltips or This week;
  - no rules or prices spelled out.
- **What the player meets instead:**
  - what happens on the map and in the world;
  - what people say about it: one-sentence bubbles, supper talk, neighbours' letters, visitors' tales, a priest's
    line, a scoffer's doubt;
  - the stories, in pieces, only through people.
- **A manifestation's card** says what was seen and heard, eerily, with no why. At Felt the god may stay unnamed.
- **Favour, ties, natures, kinds and how near each god is** are hidden state that drives what the gods do. The dev
  actions stay.
- The Book of the Gods described above is withdrawn.

### The colony's book: only what the pawns know (the player, the same day)

The player: "what I like though is how you described 'the book of the gods' ... information about the gods should only
ever be like from the point of view of a the pawn. what they know, experienced, heard, believe, feel like doing etc. in
that case a book or overview can exist and develop, but I just never want this 'meta' level player information ... the
gods should be above me as the player as well as the pawns, and what i learn and see about them is the same as my pawns
do, that makes them more authentic and mysterious".

So the colony keeps a book of the gods, its own tab in the Region window, written by its keeper (the priest, else the
most devout or most literate colonist, in their voice):
- **Every line is an attributed entry,** dated: seen by named colonists, told by a named visitor, notable or
  letter-writer, believed by a named colonist, or felt after a prayer.
- **Beliefs sit side by side** and contradict. The colony can be wrong, and the truth is never marked.
- **A god appears only once someone has heard of it,** under the name the teller used. Two names for one god stay
  apart until someone connects them.
- **Stories are written as heard,** in pieces, and differ between tellers.
- **Never in it:** favour, rules, or anything the colony couldn't know.

**A holy book, not a log** (the player, 12 November 2026, with a picture of Ducat's page: "the god's book should not only
be a log of events, but more like a wiki page basically building each up based on gained knowledge, only a more
mysterious and ominous wiki, you know like a religious text-style wiki"). Each name's page is a chapter, numbered as the
name was first heard, whose parts grow only as the colony learns (`Gods/GodLoreVerses.cs` keeps the verses,
`Gods/GodBookMath.cs` lays a chapter out and chooses its words, `Gods/GodLorePage.cs` draws it). A verse is set down
only when something happens in play that someone of the colony saw, was told or came to believe; nothing comes from a
god's def alone (its lines are only the words people say):
- **Of its names:** a name used by anyone whose words reach the book, once a people (once for the colony's own), with that
  people's age in words; two names worked out to be one. A name the book has with no verse (heard in a tale, or kept
  before the verses) as first heard.
- **Of what it is:** a line told at the fire, at supper or at the gate (the def's `whatLines`, in order, one sentence,
  by the teller's name for the god); a scoffer's answer to a believer beside it as a doubt ("Some say: ... Others hold,
  with Bram: ...").
- **Of how it is known:** a line on how it is seen told the same way (`seenLines`); a sign learned from a tale heard whole
  or retold in a letter, with the tale and its teller; what the colony saw of it plainly, with who saw it.
- **Of what it loves and what it hates:** what a believer came to hold after a god's hand they saw (a gift or an anger,
  in the game's events or the gods' own doings), set down under the god they took it for, as one of the things the
  hand's own god cares for; taken for the wrong god, the belief sits under the wrong name, and nothing says so. A
  second holder beside the first: "Others hold, with ...".
- **The sayings** as said aloud (by a colonist at supper or a visitor at the gate), **the tales** piece by piece as told
  (a piece not yet heard a gap, none once a letter retold the tale whole), **its kin and its enemies** (a word on two
  gods at supper, a tale that names another, each in both chapters, with a link to the other's), **its feast and its
  rites** (a rite kept here with someone there to see it; a neighbour's invitation to their god's feast).
- **Said as theirs:** every verse ends with who said it and where ("so Mirel of the League told it at our gate", "so
  Avila says", "so Avila holds", "so their letter said"), or, said through more than one mouth of one people, "so they
  say among the people of ..."; "who told it is not written" for what an older book kept without its teller.
- **What is still dark:** one empty part is a silence, two or more together a torn page; a tale never heard is not in
  it at all.
- **Scripture's look,** from layout alone (the game has no fonts of its own): the chapter's number and the parts in
  small capitals by size, the name set large, a drawn ornament, verses numbered in the gutter, the first with a drop
  capital in the book's gold, the margin keeping each verse's date, a change of the keeper's hand and the keeper's
  notes in their own voice, the dated witnesses last; the page a card in either of Rimfolk's looks.

### Four more (the player, the same day)

"There should also be a god of the sun & moon, a cat god, the rain god, a god of gold." Added to the build in progress
as gods 10 to 13 of `docs/gods-pantheon-brainstorm.md`:
- `Rimfolk_God_Helios` and `Rimfolk_God_Selene`: the sun and the moon, Helios the Giver and Selene the Mirror, the Gift
  and the Return. The concept went through one god (Dial, Wake), two torn halves (Noon and Lull, Ardent and Wane, the
  nameless pair, Helios and Selene as the Torn Light), and then, at the player's word ("A new concept please with these
  names"), this:
  - Helios gives without looking and can't be bargained with.
  - Selene, who has no light of her own, returns at night what was done by day, as much as her face shows.
  - She loves him, and nobody knows if he knows.
  - The eclipse is her stepping between him and the world.
- `Rimfolk_God_Mog`: a grey cat on the map, plainly (remade as Tamiu, the Keeper of the Threshold, on 14 November 2026:
  the last section).
- `Rimfolk_God_Murmur`: a vast toad of the wells and marshes whose song calls the rain.
- `Rimfolk_God_Ducat`: gold that wants to move.

Fallback domains: Helios Hearth and Harvest, Selene Night and Healing; Mog Hearth and Hunt (Tamiu since 14 November 2026: Dead and Hearth); Murmur Waters and Harvest; Ducat Way and Craft. Helios takes the solar flare
and heat wave from Cirrus and Pip; the eclipse is Helios and Selene meeting.

### Naming a god in events (the player, the same day)

"I mean in events etc the god can be named, if we know it's them that caused something."
- **Named:** an event, card or letter names the god plainly, by the name the colony knows it by, when the colony knows
  it was that god: it was seen acting, a disguised god revealed itself, the colony has learned that sign as the god's
  own (a god drawn near enough shows itself plainly, and is named from then on).
- **Unnamed:** when the colony only suspects, it's "some say". When it has never heard of the god, nothing is named.
- **Never:** the god's mind, its favour or its rules.

### Tamiu, the keeper of the dead: Mog remade (the player, 13 and 14 November 2026)

"the cat card we have. Let's remake it into something new. Still a cat card, but kind of one inspired by, you know,
Egyptian mythology and the Sphinx, you know, with an Egyptian name ... maybe the cat god could be, you know, with mummies
and like Egyptian gear wearing zombies style followers ... and there's also some sort of cult behind it"; "yes, remake mog
and create a corresponding sign for it". DESIGN.md, "Tamiu, the Keeper of the Threshold", has the whole of it. For the
contract:
- Her defName and key stay `Rimfolk_God_Mog` and `Mog` (saves, the parts, the textures); the name people say is her def's
  label, Tamiu (`God.FromDef`: a god's name is its label, its key for the others). Parts key a god by `God.Key`, never by
  the name it is said by.
- She is the pantheon's keeper of the dead, apart from both others of death: Morel is the rot that takes the body back,
  Undertow the hunger under the world; Tamiu keeps the dead whole and lies across their door (Morel's rival, the Hunger's).
- The unseen rule holds in all of it: her riddle is "heard in no voice at all" by whoever goes near her, her keeper is
  a person of a people that holds her and says only what their people do, the wrapped dead's card says what they do on
  the map, and she is named on a card or in her presence only where the colony knows it is her (`GodsOnMapWords.Title`,
  `Gods.CardName`).
- Harm: the cuff of the paw for a wrong answer and the wrapped dead standing guard are both at the shared harm budget's
  rate (`HarmKinds.GodRiddle`, `GodDead`), both from something on the map, both on a card that says so; the wrapped dead
  need Anomaly and never come with hurts switched off.
- Every age: her names, her riddles and the words of her keeper and of the wrapped dead each by the colony's age.
- Her presence on the land (`Rimfolk_GodPresence_Mog`) was her own condition at first; round 2 took it in as one of the
  gods' own presences (effect `Watch`, below), from her coming to her going as before.

### Each god its own: presences, ways of taking, signs (the player, 14 November 2026; round 2)

"gods effect should be noticeable"; "each of the gods should feel really unique in what it does and whether or how it
interacts with the world, what the impact is, how these things play out on the map". DESIGN.md, "The gods each their
own", has the whole of it. For the contract:
- Each god's own presence, way of taking and signs are declared per god, never by name in code: `GodDef.presence` (a
  `GameCondition_GodPresence` def with an `OwnPresence`: its god, one `PresenceEffect`, its names and words, motes and
  sounds), `GodWorshipDef.taking` (an `OfferingWay`), `GodShapeDef.omens`. A god that declares none keeps the shared
  presence (the sky and the still wind) and the shared way (gone by morning, nobody saw it go), so a new or remade god
  plugs in by its defs alone, its effect or way given code only when it is a new kind.
- The unseen rule holds: a presence is named only when the colony is sure whose it is (and the Hunger's never), else by
  what anyone can see ("the green leaning", "a warm evening"); its numbers are shown under its label, as the game shows
  any condition's (in the condition's tooltip and the stats' breakdowns), never as favour, wants or rules; what people say
  of it is what they see, named only where the colony is sure; a gift's going is told as what was found where it lay.
- Harm only at the game's own rate and always shown: a few degrees of cold or heat for a few hours (never past a cold
  snap's or a heat wave's), the weak feeling Hush's cold sooner (their comfort, in its breakdown), faster rot under Morel,
  Pip's prank (an hour at most, ended at once for need or danger), the Hunger's extra taking (a few of a stack, never all).
- Every age: the effects are the land's and the colony's own things (plants, rock, beasts, hearths, lamps, machines,
  wells, coins), so each takes the age's form by what the colony has: a tribal colony's Pip has no wires to hum in and
  its prank has no door if it has none.

### Each one's gods: a colonist's own belief (the player, 14 November 2026; round 3)

"pawns should have and develop kind of a relationship in a way, or rather a belief to gods ... the more they pray and
interact to a god, that they kind of, yeah, get a stronger belief in that god"; "pawn belief should be reflected in book
and culture tab". DESIGN.md, "Each one's gods", has the whole of it. For the contract:
- A belief is the pawn's own, a pawn fact the unseen rule allows: how strongly a colonist holds a god and dreads it,
  shown only in words ("devoted to Hollin", "leans toward Ducat", "in dread of the Hunger", "doubts them all"), with a
  line of why. Never a number, never the god's favour, wants or rules; it never reads or moves favour.
- It is held in the god they took a thing for (`GodLoreMath.Attribute`, as the book is written), so it can be wrong, and
  shown by the colony's name for that god (its first chapter in the book). A god the colony hasn't heard of is named
  nowhere; a second name not yet worked out to be the same has nobody under it.
- Never inherited: drawn from who they are (traits, passions, xenotype, their past, the people they came from, their
  faith, a bond with an animal), what they do and what they see. Nothing reads their parents or kin.
- `Worship.GodOf` and `Gods.SwearBy` read it first (`PawnGods.Held`): the god someone prays to and swears by is their own,
  else the moment's domain's, else their work's. Parts ask those two, never the belief record directly.
- Growth comes through the seams that are already there (prayer, offerings, vows, festivals, dreams, tales, a god's
  hand, `Gods.Remember` and `Gods.Moment`, a god's own presence ending, a gift found taken in its own way), so a part
  that goes through them moves belief with no code of its own.
- Only the colony's own people (`Worship.Ours`); no harm but the rare scuffle in a quarrel at the game's insult's rate.

### The saints: a faith's deities and the colony's own (the player, 14 November 2026; round 5)

"perhaps instead of these being the gods that are currently registered to an ideology, this could, for example, be some
sort of saints or important characters. So kind of a middle ground between God and the pawns and the ideology, kind of
saints or holy men or unholy men or whatever"; "let's go with saints then". DESIGN.md, "The saints", has the whole of it.
For the contract:
- Ideology's deities are read as the faith's saints or its damned (`Saints.Of`), never changed: no part writes to an
  ideoligion's deities, name, memes or precepts. A deity is tied to one of the gods by its own words
  (`GodsMath.DeityDomains`, `SaintsMath.GodFor`), as the faith's belief; the damned only to the dark gods, a saint never
  to the Hunger.
- The unseen rule holds: a saint is a person, named plainly; "of" a god (on the ideoligion screen, a statue's or a grave's
  inspect line, the card, the book) only by the colony's name for it and only where the colony knows it (`Saints.OfGod`);
  else nothing is said of the god at all. The book lists saints only under the chapter the colony keeps the god under.
- The colony is the family: a saint is one person the council honoured; nothing of it passes to kin, no mood or mechanic
  reads a family, and the card never speaks of one.
- Every age: the word is the faith's by age and culture (ancestors, saints, exemplars; the venerated, martyrs; the
  forsaken, the damned, the fallen), the relic's form the colony's age's.
- No harm: the saints only add places to pray, a day kept and small good moods; the damned leave a believer uneasy, never
  more.
