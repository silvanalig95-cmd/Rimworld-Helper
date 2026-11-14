# The gods, next: proposals (14 November 2026)

The player, the same day: "for the gods, I still kind of want you to create an image or a symbol or use one for each
god, because when I look at the gods page now, I honestly, for most, I don't really understand what they're supposed to
represent ... And then I want to know what's the kind of status for the whole God's implementation ... each of the gods
should feel really unique in what it does and whether or how it interacts with the world ... I'm still a bit confused
about the altars and what they do exactly ... buffalo wool is, I don't know, donated ... pawns should have and develop
kind of a relationship in a way, or rather a belief to gods ... perhaps instead of these being the gods that are
currently registered to an ideology, this could, for example, be some sort of saints or important characters."

This is a proposal, as the rules ask: nothing here is built until the player says so. Every idea was checked against
"The gods are unseen": the player learns only what the pawns know (seen, told, believed, possibly wrong), no favour,
wants or rules are shown, and a god is named only when the colony is sure. The draft sheet of signs is
`docs/gods-symbols-draft.png`, drawn by `Tools/make_god_signs.py`.

## How it is now, in short

**What every god shares today** (the same machine, only the name and colours change):
1. **The presence** in the bottom right corner is one condition for all fourteen (`Rimfolk_GodPresence`). Its label is
   "the presence of Hollin" when the colony is sure, else "a strange stillness", and its text is the same for every god
   ("The sky has a color nobody has a word for, and the wind has died."). It does two things: the sky takes the god's
   colour (darker for anger) and the wind dies, for one to five hours. Nothing else in the world changes.
2. **The moment** is the same for every god: the game drops to normal speed, the camera jumps, the music fades, a sting
   and a track play, everyone turns to look, the devout pray, calm animals lie down, a gold card arrives.
3. **The card's words** fall back to the same seven texts ("came onto the land", "and the light", "and the dark") when
   the event gives none.
4. **The look** is mostly by domain: Ennu and Ducat share one look, and Hollin's is the same "wild" look Bellwether has
   as a god; only Smoulder's dragon, Bellwether's beast, Mog's cat and Murmur's toad have looks of their own.
5. **The omens:** five any god may send (birds leave, a calm, two colonists dream the same dream, a light won't stay
   lit, the animals are restless). Mog, Bellwether and Ennu have only these (three each); Selene, Helios, Murmur and Undertow mix one in.
6. **Offerings** run the same way for every god and end with the same line ("The offering at Ennu's chapel was gone by
   morning: 16 muffalo wool. Nobody saw it go."). Only the list of liked kinds differs.
7. **Prayers** have the same four hard moments (sickness, a death, a raid, a drought) and the same four answers (a fever
   eased, grief eased, a steady hand, rain) for every god. **Vows** are the same four kinds for all.
8. **Kindness and anger** are mostly the game's own events with the game's own letters (blight, heat wave, cold snap,
   flashstorm, short circuit, solar flare, meteorite, a trader). Only a colonist's bubble ties them to a god.
9. **Mood:** "said a prayer" and "left an offering" give no mood at all.

**What each god has of its own today** (the facts behind section C):

| God | What it is | Its own today |
|---|---|---|
| Hollin | the mother of roots and the green | Signs: leaves drift to one place, an anima tree hums, dryads stand still. Rarely rises as a shape of roots for an hour. Kindness: wild food and healroot near the walls. Anger: blight. A grudge: wolves or bears after one colonist. Gifts: A RimWorld of Magic's Druid, Vanilla Psycasts Expanded's Wildspeaker, a "green hand". Festival: the Greening. |
| Smoulder | a dragon asleep under the mountain | Signs: a winged shadow at noon (Dragons Descent's sprites), steam, the rock rumbling over deep mines. Mining under thick rock makes him turn (dust, a warm floor, from the third time stone may fall); past every warning he wakes: the deep working closes, and when drawn near the true dragon (Dragons Descent) comes up for hours. Kindness: a silver or steel vein. Anger: heat wave. Gift: gold beside a dreamer, "miner's luck". |
| Bellwether | the first thrumbo | Walks the map as a great white thrumbo, 1 to 3 thrumbos following. Colonists can sit near him (+6, a child who touches the horn +10); let pass, "a strange peace" (+4, 15 days). Harming him or his kin: every people near thinks less of the colony. Signs: only shared ones (restless animals, a calm, a shared dream). Gift: a passion for the youngest. |
| Morel | the mother of rot and mushrooms | Signs: a ring of mushrooms overnight around a grave or a body, a smell of earth, white threads on a shut room's floor. Kindness: a fever broken in the small hours. Anger: a freezer fails at the worst hour. Gifts: the Necromancer, a "soft hand". |
| Ennu | the Guest | The only god that comes as a person: the stranger at the gate (a bed and table, turned away, or the gate), stays a day or two with small tells, judges the welcome (a gift to the kindest, a fever on one who struck it), may show itself as it leaves, remembers past welcomes. Gift: "a guest's warmth". Signs: only shared ones (a shared dream, a light that won't stay lit, restless animals). |
| Pip | a young spirit in machines | Signs: lights flicker in a pattern, the comms console breathes static, an unpowered lamp glows. Kindness: a machine mended or a battery filled overnight. Anger: short circuit. Gifts: Technomancer, EMP, power node, "machine-tongue". |
| Cirrus | the vast thing in the high sky | Signs: a shadow too large at noon, a star that moves the wrong way, birds leave. Answers in aurora, eclipse, thunder. Kindness: a meteorite. Anger: flashstorm. A grudge: lightning on the roof over one colonist. Gifts: Storm Born, lightning trap, Staticlord. |
| Hush | winter, cold and silence | Signs: frost in lines like writing, a silence, a white figure at the edge of the night. Answers in snow and cold. Kindness: a cold snap ends early. Anger: cold snap. Gifts: Heart of Frost, snap freeze, Frostshaper, "winter in the blood". |
| Undertow | the Hunger in the dark | Signs: a dark no light reaches, a sweet voice to the most desperate. The bargain: someone dying, breaking, or the colony starving hears an offer; yes pays at once and then takes more, sooner each time; Hollin, Bellwether and Ennu turn away, Pip goes quiet. Never answers prayers. |
| Helios | the sun | Signs: the sun stands still (shadows hold), a heat haze. Gives to all alike: long clear days; too much: heat wave, drought, solar flare, whatever the colony does. Takes no offerings; worship doesn't move him. |
| Selene | the moon | Signs: a huge moon all night (by her face), a shared dream. Around the full moon she returns a standout deed of the day: something by the bed, a good dream, a wound healed, or a nightmare, a hard frost, an animal at the door. The eclipse stops everyone outside. Gifts: Chronomancer, mage light, "moon-sleep". |
| Mog | a gray cat who is a god | Walks the map as a gray cat with too-bright eyes, cats following; chooses one colonist for a season (that one is shielded from some harm); can't be tamed (marked, she leaves); a cat harmed: the cats leave in one night and the rats come. Stares at Ennu. Signs: only shared ones (restless animals, a calm, a shared dream). No festival. |
| Murmur | a huge toad whose song brings rain | Signs: a deep croak from wells and marshes. Rarely surfaces in wet ground as Odyssey's colossus toad. Answers in rain and thunder. Kindness: rain. Anger: drought, then a flood (Odyssey). Asks the council for the finest beast. Gift: the rain spell, "rain-ear". |
| Ducat | gold that wants to move | Signs: a glint, a golden beetle across a table, a coin where none was dropped. Kindness: a trader at the right time. Anger: silver missing from a hoard of 500 or more. Watches the colony's debts. Gift: "the glint". |

## A. A symbol for each god

**What it is:** the colony's own holy sign for the god, the thing its people draw, carve or stitch for it. It is a
belief made visible, never a portrait of the hidden god. The draft of all fourteen is `docs/gods-symbols-draft.png`.

| God | The sign | Why |
|---|---|---|
| Hollin | a tree whose roots are as wide as its crown | what grows under the world is as large as what grows above |
| Smoulder | a mountain with a coil asleep under it, its eye shut | the dragon under the rock |
| Bellwether | one spiralled horn rising from grass | the first thrumbo's horn, the grass he leaves greener |
| Morel | a mushroom whose threads run down into the earth | rot and what grows from it |
| Ennu | an open door with a light left inside | the welcome it asks for |
| Pip | a spark inside a small cog | the spirit in the machine |
| Cirrus | an eye over drifting cloud, a star apart | the one that watches from too high up |
| Hush | a frost star on a tall thin standing body | the white figure at the edge of the snow |
| Undertow | a whirl pulling down into a hole | the pull from under the world |
| Helios | a sun whose rays all point outward | he gives and never looks back |
| Selene | a crescent holding a small full disc | the moon giving back the light she was given |
| Mog | a seated cat facing you, eyes cut bright | the gray cat; suits the Egyptian remake too (Bastet's pose) |
| Murmur | a toad over ripples, rain falling | the song that calls the rain |
| Ducat | a holed coin with motion lines | gold that will not stay |

**Where it shows** (only for a god the colony has heard of; nothing for the others):
1. **The Gods' book:** beside the chapter's name, once the colony has learned the sign. Learning it is a verse of its
   own under "Of how it is known" ("Its sign is a tree rooted as deep as it is tall; so Mirel of the League drew it in
   the dust at our gate"). A visitor, a pilgrim, the priest, a shrine dedicated or a tale can teach it.
2. **Its shrines:** the sign drawn over the dedicated place, small and in the god's colours, and in the inspect line.
3. **Cards:** on a god's card only when the colony is sure it was that god (`Gods.CardName`); a veiled card shows no sign.
4. **On the map:** the sign above a god walking plainly (the label over it already follows the colony's knowledge).
5. **Every age:** the same shape in the age's own form: scratched on a stone (tribal), painted on a shield or a banner
   (medieval), stamped on a badge (industrial), lit in a ring (spacer). The sheet's last cell shows Hollin's four.
6. **Two names, one god:** each name's chapter shows the sign only once someone has linked the sign to that name, so a
   god known under two names may show its sign under one of them only, as the colony knows it.

**Could the game's or a mod's icons serve instead?**
- Ideology's own 94 ideoligion icons (`IdeoIconDef`) are for faiths, not gods; a few come close (`Treeflat` for Hollin,
  `Skull`, `Ouroboros`, `FireLeaves`, `Orbit`, `TechAtom`), but most gods have no fit, and the player would recognise
  them as the faith icons from the ideoligion screen.
- Vanilla Ideology Expanded - Icons and Symbols (active, 220 icons) adds general sets (animals, elements, circles,
  stars); again faith icons, generic, and not one of them says "the toad who sings the rain".
- Recommendation: Rimfolk's own drawn signs, white on transparent and tinted in game, as the mailbag, the thermometer
  and the gods' auras already are. The script writes them as textures when asked (`--textures`).

## B. Plain words

A one-line plain meaning at the top of each chapter of the Gods' book, set apart from the verses and in the keeper's
hand ("As we understand it, so Avila keeps it:"). Shown only once the colony has a verse of "what it is" for that god;
a god never heard of has no chapter at all. It is the colony's understanding, so it can be wrong when the colony was
told wrongly (a belief under the wrong name stays wrong).

1. **Hollin:** The mother of all roots and growing things. She guards the trees and the green, and remembers every tree cut.
2. **Smoulder:** A dragon asleep under the mountains. His dreams push up ore and heat; he loves silver and hates being woken.
3. **Bellwether:** The first thrumbo, a great white beast that walks the whole world. Let him pass in peace; never hurt his kin.
4. **Morel:** The mother of rot and mushrooms. Everything that dies goes back to her; she hates what is kept from rotting.
5. **Ennu:** A god who comes to the door as a stranger asking for shelter, and remembers how it was treated.
6. **Pip:** A young spirit living in machines and wires. It mends them when it is fond of you and breaks them when it sulks.
7. **Cirrus:** Something vast in the high sky that hardly ever looks down. Storms, stars and falling rocks are its moods.
8. **Hush:** The spirit of winter, cold and silence. It takes the weak first and spares those who prepare.
9. **Undertow:** Something in the dark under the world. It grants the desperate whatever they ask, and always takes more back.
10. **Helios:** The sun. He gives light, warmth and harvest to everyone alike, and sometimes far too much.
11. **Selene:** The moon. At night she gives back what people did by day, good or bad, as much as her face is full.
12. **Mog:** A gray cat who is a god. She chooses one person to bring luck to, and leaves if anyone harms a cat.
13. **Murmur:** A huge old toad in the wells and marshes. When it sings, it rains; when it falls silent, the land goes dry.
14. **Ducat:** The spirit in gold that wants to keep moving. It loves trade and giving, and hates a hoard.

Also plain: the verses people say (`whatLines`, `seenLines`, sayings) were written to sound like old speech. A pass over
the fourteen's lines in the same plain voice as these fourteen sentences would help the book read at once.

## C. Each god its own

For each god: its own presence (replacing the shared `Rimfolk_GodPresence` and its shared words), its own happenings,
what it changes, and how its walking form behaves. A presence has two labels, as now: named when the colony is sure
("Hollin listening"), else what anyone can see ("the green leaning"). Its description says only what is seen. Where it
changes numbers (plant growth, temperature), they show where the game shows any condition's numbers, under the
condition's own label, never as a god's favour. Harm always goes through Rimfolk's shared harm budget, at the game's
rate, and is shown. Mod content is by name or `MayRequire` (active rows of `docs/player-mods`), with a vanilla fallback.

### 1. Hollin, the Rootmother
- **Her presence: "the green leaning"** (named: "Hollin listening"). Every plant on the land leans toward one place (the
  anima tree, else the oldest tree), leaves and seeds drift there, anima trees hum, dryads stand still. Plants grow
  faster on the whole map while it lasts (about a third). A tree felled under it leaves its feller uneasy ("the axe felt
  wrong"). Mods: Alpha Biomes' petal storm weather (`AB_PetalStorms`) in spring, A RimWorld of Magic's green twinkles,
  Vanilla Ideology Expanded - Dryads' dryads gathering.
- **Her happenings:**
  1. **The roots remember.** A colony that fells many trees in a season and plants few finds roots breaking through one
     floor tile a morning in the rooms nearest the cleared ground, for a few days (the tile becomes bare earth with a
     root mound); whoever finds it says so.
  2. **A sapling where the offering lay.** Raw food left at her shrine sinks into the soil overnight, and a young tree of
     the land stands there in the morning.
  3. **The dryads' word.** With an anima or gauranlen tree near: the dryads stop, turn to one colonist, and one walks to
     them; the colonist says they heard one word ("Plant", "Wait", "Spare"), and the word points at something real
     coming (a blight in the fields, a hard frost).
  4. **The green walk.** Her rising, extended: she rises, stands, sinks, and rises again twice across the map, leaving a
     line of new grass and wildflowers; animals follow quietly.
  5. **The old tree spared.** The colony's oldest tree left standing through a hard year: healroot in a ring at its foot
     next spring.
  6. **Wild at the edges.** A stripped land: brambles and wild growth creep into the field edges each spring (a chore,
     never harm).
- **What she changes:** plants, trees, fields, dryads, the floors of a colony that cuts too much.
- **Walking form:** she never walks; she rises as a shape of roots taller than a tree (Rimfolk's drawn shape, as now),
  and now may rise in a line across the land, the plants turning to follow her.
- **Replaces:** the shared presence; the shared "gone by morning" offering line; the rain answer (rain becomes Murmur's
  alone, she answers in growth).

### 2. Smoulder, the Coil Under the Mountain
- **His presence: "warm stone"** (named: "Smoulder breathing"). The ground warms near the mountain (outdoors a few
  degrees, rooms under the rock more), snow melts off the rock, steam puffs from geysers and from cracks at the foot of
  the rock, a faint ash falls (Alpha Biomes' volcanic ash weather, softly; Odyssey's `HeatVentHeat` as the pattern), and
  ore in the rock faces glints. Mining yields more while it lasts; anyone sleeping under the mountain dreams of a warm
  voice.
- **His happenings:**
  1. **The bargain at the shaft.** A miner dreams of a warm, reasonable voice offering a bargain: silver down the
     deepest shaft, gold back. The dreamer asks the colony. Agreed, someone carries silver to a mined cell under thick
     rock and drops it in; days later a gold vein shows in the rock nearby. Kept to the letter: if less was thrown than
     was promised, the vein is steel with a skin of gold (the tale of the chief and the gilded lead).
  2. **Praise with his proper name.** An artist finishing a sculpture may name it for "Aurelian the Ninth"; the next
     day a silver seam glints in the nearest rock face.
  3. **The heat from below.** Before stone ever falls in a deep mine (as now), the room's floor grows hot for hours:
     a warning anyone can feel.
  4. **The shadow at noon** (as now), and that night every miner dreams the same dream of something stretching.
  5. **Silver kept from the deep.** A colony that hoards silver and never pays the deep: a miner wakes from a warm
     dream with a small burn on the hand (shared harm budget) and says the voice was not pleased.
- **What he changes:** rock, ore, heat under the mountain, the miners' dreams.
- **Walking form:** with Dragons Descent, the true dragon rises out of the mountain, circles the colony once, lands on
  the high rock and looks for hours; whoever calls him by his proper name gets a warm look and a memory; harmed, he
  fights as the game's dragon does. Without the mod: the quake and the shadow.
- **Replaces:** the shared presence; the heat wave as his anger (the heat wave stays Helios's), his own heat from
  below instead; the shared offering line (the silver goes down a crack and the floor is warm after).

### 3. Bellwether, the Old Horn
- **His presence: "the white beast grazing"** (named: "the Old Horn passing"), only while he is on the land. Wild
  animals stop fleeing and graze calmly, no animal turns manhunter, quarrels among the colonists are rarer (the strange
  peace), grass springs back greener behind him, and other thrumbos follow far off.
- **His happenings:**
  1. **The road through the colony.** His path crosses the land edge to edge; a fence across it makes him stop and
     wait, and those who see him wait feel ashamed until someone opens a gate.
  2. **The children and the horn** (sitting near him, as now), extended: a child who touched the horn grows up looking
     at the sky before rain, and says so an hour before the weather turns (a real foresight; the tale ties them to Cirrus).
  3. **The one who sat by him** (as now), made visible: a fear gone (a cowardly or nervous trait eased), a passion
     found, or a grief softened (a mourning memory shortened).
  4. **A hurt thrumbo at the edge.** A wild thrumbo, wounded, comes to the colony's edge and lies down; tended, it goes
     its way and the strange peace follows.
  5. **The last of something.** When thrumbos pass (the game's event), someone dreams of a white beast searching for
     the rest of its kind.
- **What he changes:** beasts, the colony's peace, grass, the children.
- **Walking form:** as now, never hurried; new: he stops at the colony's edge at dusk, lowers his head toward the
  youngest, and leaves by the far edge at dawn.
- **Replaces:** the five shared omens with his own: every thrumbo on the land facing one way, grass bowed in a long
  line, a hoofprint the size of a table at dawn; the shared presence.

### 4. Morel, the Soft Mother
- **Her presence: "spore-fall"** (named: "Morel close"). Pale spores drift (Odyssey's `BioluminescentSpores` by night,
  softly), mushrooms come up in rings around graves and fallen things. Food outside cold storage rots faster, corpses go
  to earth faster, fungus crops grow faster (nutrifungus, Vanilla Plants Expanded - Mushrooms), and sicknesses pass
  faster. Displeased, the darker form: Vanilla Landmarks Expanded's rotten stench (`VEE_RottenStench`) as the pattern.
- **Her happenings:**
  1. **The fairy ring** (as now), pointing at something: a body left unburied, rotting stores, or a small thing buried
     there that someone digs up.
  2. **The mourner at the graveyard** (her lore, not built yet): at dusk a figure of pale threads stands at the edge of
     the graves, still, and at dawn there is a ring of mushrooms where it stood. Those who saw it remember it together.
  3. **White threads before the freezer fails.** Her anger (a freezer failing, as now), given a warning: white threads
     across the freezer's floor the day before.
  4. **The turning under.** Food left at her shrine rots overnight into rich soil, with a mushroom on it; rot is her
     taking, not a failure.
  5. **Spores in the breath.** Someone walking through a ring hears her for a moment, soft and too close (one sentence,
     a small uneasy memory).
- **What she changes:** rot, the dead, soil, sickness, fungus.
- **Walking form:** the mourner of threads (above); Vanilla Races Expanded - Fungoid's fungoid as its body, by name,
  disguised as a mourner, where the mod is loaded.
- **Replaces:** the shared presence; the shared offering line; blight as her sign stays the game's, read by believers.

### 5. Ennu, the Guest
- **Its presence: "a warm evening"** (named: "the Guest under our roof"), only while the stranger stays. Hearths burn
  longer and warmer, windows glow, colonists' talk is kinder (fewer insults), animals follow the stranger, and all
  visitors on the map are happier.
- **Its happenings:**
  1. **The stranger at the gate** (as now), with a different small test each time: it asks for a story at the fire, the
     cook's name, to see the graves, a coat in winter.
  2. **The bowl set out.** A bowl left out on the Open Door night, or on any night with a spare bed: eaten by morning,
     and the cook thanked by name in chalk on the table.
  3. **The teller of tales.** While it stays, it tells a piece of another god's story at the fire (the tales' machinery,
     `GodTales.Tell`); it carries news between the gods.
  4. **The guest turned out.** Turned away in a storm: travellers avoid the colony for a season, and the next one to
     come carries an old bruise nobody can explain.
  5. **The kindest carries something** (the gift, as now), shown: visitors linger with that colonist and tell them news.
- **What it changes:** guests, the colony's name among travellers, stories.
- **Walking form:** the stranger (as now); never the same face, always the same habit: it thanks the cook by name.
- **Replaces:** the shared omens with its own: a knock at the gate with nobody there, footprints in the snow to the
  door and none away, the dogs silent; the shared "way" look it has with Ducat (its own: lamplight and a travelling
  cloak); the shared presence.

### 6. Pip, the Hum
- **Its presence: "a hum in the wires"** (named: "Pip awake"). Every powered light pulses softly in a pattern, machines
  run quietly, batteries hold their charge while it lasts, work at powered benches goes a little faster; and one door
  will not open for one person until someone else opens it. Sulking, the darker form: Vanilla Landmarks Expanded's
  geomagnetic storm (`VEE_GeomagneticStorm`) as the pattern.
- **Its happenings:**
  1. **Mended overnight** (as now), shown: the machine's light winks three times as it comes back.
  2. **The machine that would not stop.** A machine switched off keeps running an hour, to show it can.
  3. **Calls to the parents.** At night the comms console turns itself on and a half-written message to the stars is
     on its screen in the morning; with an ancient terminal in the colony, it hums back.
  4. **The turret that held fire.** In a fight a turret holds its fire for a breath as a colonist crosses its line,
     and the colonist says so.
  5. **Sulking** (the short circuit, as now), with a warning: the lights flicker slow and sad the day before.
- **What it changes:** machines, power, doors, turrets.
- **Walking form:** a spark running along the power conduits from machine to machine, stopping at a broken one; in a
  colony with mechanoids, it may ride a small one that wanders off humming for an hour. A colony with no machines
  never meets it.
- **Replaces:** the shared presence; the shared offering line (the part is gone and a broken machine nearby works).

### 7. Cirrus, Who Drifts Above
- **Its presence: "a sky too high"** (named: "Cirrus looking down"). The sky clears and seems higher, the weather does
  not change while it lasts, a vast shadow drifts over (as its sign now), birds leave, lightning flickers far off with
  no rain. Those who look up (a watch, a roof, a telescope) get a "felt watched" memory; skygazing gives more joy
  (Vanilla Landmarks Expanded's `VEE_SkygazingSpot` as the pattern).
- **Its happenings:**
  1. **The star at the door** (the meteorite, as now), only for a colony that watches the sky (a telescope used, a
     rooftop vigil, its festival kept).
  2. **The wrong-way star** (as now), and whoever saw it dreams where something fell; a pod lands near there next day.
  3. **The aurora on the coldest night** for those outdoors.
  4. **Dry thunder** when it is bored: thunder and lightning over the far hills with no rain, never on the colony.
  5. **The lightning grudge** (as now).
- **What it changes:** sky, clear or storm, stars, falling things.
- **Walking form:** none; a shape in the high cloud (Rimfolk's drawn shape) slowly turning as if to look back.
- **Replaces:** the shared presence; the shared storm look (its own: pale high blue, a feathered cloud trail); its
  offerings (see D: today it can only ever be given cloth).

### 8. Hush, the Long White
- **Its presence: "a white silence"** (named: "Hush near"). Nobody talks (no social talk while it lasts), the cold
  deepens a few degrees, frost forms by the walls, a fine snow falls where the land has snow (Seasonal Weather's
  `FogSnow`), nothing rots out of doors and the dead keep. The old, the sick and babies feel the cold sooner.
- **Its happenings:**
  1. **The white figure** (as now); new: whoever walks toward it finds footprints that stop in the open snow.
  2. **The word on the window.** One frosted window shows a single word in the morning ("Warm", "Bread", "Count"),
     pointing at something real (the stores low, someone cold).
  3. **The old man's fire.** In a cold snap, a colonist who sits up keeping the old and the sick warm: the cold breaks
     the next morning (the thaw, as now, tied to the vigil).
  4. **Frost on the grave.** After a death in the lean months, if food was wasted, frost lies on the new grave and a
     cold snap follows (as its lore says, now visible).
  5. **Kept whole.** Its presence keeps the dead, which Morel resents: frost on a grave melts into a ring of mushrooms
     (a new quarrel shown, Hush against Morel).
- **What it changes:** cold, silence, the weak, the food stores, the dead.
- **Walking form:** the tall white figure at the edge; on the longest night it walks the colony's edge and leaves a line
  of frost, always moving away when anyone comes within twenty steps.
- **Replaces:** the shared presence; the shared night look (its own: white and blue, frost trail); the cold snap stays
  the game's event, read by believers.

### 9. Undertow, the Hunger
- **Its presence: "the dark"** (never named, as now). Lamps reach less far, one corner of the colony stays dark however
  it is lit, the most desperate hear a sweet voice (one sentence), and minds are more open to the psychic (Vanilla
  Psycasts Expanded's `VPE_IntenseShadows` as the pattern). After a bargain, Anomaly's gray pall, softly.
- **Its happenings:**
  1. **It always says yes** (the bargain, as now), and **the price comes** (as now).
  2. **The others turn away, seen:** after a bargain the dryads keep away from the one who bargained, Mog leaves, the
     anima tree is silent.
  3. **The bones of the old ruins.** Someone exploring old ruins (Real Ruins, Vanilla Quests Expanded - Ancients) comes
     back with "a hollow ear" nobody asked for.
  4. **The shuttered night** (its festival, as now): with shutters closed and every lamp lit, the dark corner cannot form.
- **What it changes:** the desperate, the dark, minds, the price of a wish.
- **Walking form:** it never comes itself; a darkness that stands drifts slowly toward the most desperate colonist at
  night and is gone at dawn.
- **Replaces:** the shared presence only (its looks and sounds are already its own).

### 10. Helios, the Giver
- **His presence: "the long noon"** (named: "Helios staring"). The shadows hold (as his sign now), daylight lasts
  longer, solar panels give more, crops grow faster, the air warms; those who shun light (night owls, sanguophages)
  feel it (Rimfolk's `Council_WarmSpell` as the pattern).
- **His happenings:**
  1. **The long clear season** and **too much giving** (as now).
  2. **He looks back.** The day after an eclipse is the hottest of the season (the tale "why he burns so hot after").
  3. **The first fruit in the sun.** His festival made visible: everyone eats the first fruit out of doors at noon; a
     shared memory.
  4. **Nothing can be given him.** Something left at his shrine is never taken: at noon the light lies on it and it is
     still there at dusk; the colony learns by seeing it, not by being told.
- **What he changes:** light, heat, growth, power, drought.
- **Walking form:** none; the sun standing still.
- **Replaces:** the shared presence; the shared harvest look (his own: white-gold, a heat shimmer).

### 11. Selene, the Mirror
- **Her presence: "a moon too close"** (named: "Selene watching"). The night as light as dusk, as much as her face is
  full; sleepers heal faster and dream more; the night watch sees as far as by day. Rimfolk's moon already matches
  Moonlight Continued's phases (full around days 8 to 10 of each season), so the bright night is the game's bright night.
- **Her happenings:**
  1. **What comes back** (as now), with a plain morning card of what was found and by whose bed.
  2. **The eclipse** (as now).
  3. **A true dream at the full moon.** A dream told at breakfast that foretells something real (a raid's side of the
     map, a visitor), told as the dreamer's, proven later.
  4. **The dark face.** On a night with no moon nothing returns, and Undertow's signs come easier (as now), now said aloud
     by someone who keeps the night.
  5. **The watch she notices** (as now), shown: "the moon kept me company" and a steadier aim that night.
- **What she changes:** the night, dreams, healing in sleep, deeds coming back.
- **Walking form:** none; the moon itself (as now) and, at the full moon, a pale shimmer on still water.
- **Replaces:** the shared presence; the shared healing look (her own: silver, a cool glow).

### 12. Mog, the Cat
(The to-do of 13 November, Mog remade as an Egyptian cat god, is still held; everything below fits either.)
- **Her presence: "a lazy warmth"** (named: "the Gray Lady about"), only while she is on the land. The warmest room
  stays warmer, cats follow her, rats and small vermin leave the land, colonists nap better, her chosen one is lucky.
- **Her happenings:**
  1. **She chooses** (as now). 2. **The stare** (as now).
  3. **Two places in one day.** She is seen in two rooms within the hour, and someone looks twice.
  4. **The saucer.** Fish or milk left out at night: at dawn a cat drinks from it and the cats gather.
  5. **The cats leave in one night, the rats come back** (as now).
  6. **A stray at the door** (as now).
- **What she changes:** cats, luck, vermin, warmth, one person's season.
- **Walking form:** the gray cat (as now); if remade: a temple cat with gold eyes, Alpha Memes' sphinx (`AM_Sphynx`)
  and Kemetic slab as her shrine, Odyssey's scimitar cat as kin.
- **Replaces:** the three shared omens she has now with her own: every cat on the land sitting facing one door, a warm
  spot on a cold floor, paw prints across a table.

### 13. Murmur, Old Croak
- **His presence: "a croaking in the wells"** (named: "Murmur singing"). A deep croak felt in the chest every few hours,
  frogs (Odyssey's bullfrogs) out on the wet ground, water standing at the well, rain within hours (Odyssey's torrential
  rain when he wakes furious), wet ground more fertile, fish biting (Vanilla Fishing Expanded).
- **His happenings:**
  1. **The song before rain** (as now).
  2. **The drained marsh** (drought then flood, as now).
  3. **Sung back to.** At the Singing at the Wells, colonists sing at the well; rain within a day.
  4. **Wells kept clean.** With Dubs Bad Hygiene's wells and water: a fouled well goes dry for days; kept clean, it runs
     full in a drought.
  5. **The eye in the flooded field** (the rising, as now).
  6. **The finest beast** (the council's question, as now).
- **What he changes:** rain, wells, marshes, frogs, fish.
- **Walking form:** Odyssey's colossus toad surfacing (as now): sits, croaks, frogs gather, rain starts, he sinks.
- **Replaces:** the shared presence; rain as Hollin's voice too (rain is his alone).

### 14. Ducat, the Restless Gold
- **Its presence: "a glint"** (named: "Ducat passing"). Coins glint in the stores, a golden beetle crosses a table,
  traders pay better while it lasts, and a few coins in a store trail toward the gate.
- **Its happenings:**
  1. **The beetle on the ledger** (as now), now leading to where a coin lies; a trader the next day (as now).
  2. **The miser's chest** (silver missing, as now), shown: a trail of single coins out the gate onto the road.
  3. **The open purse.** At its festival someone presses a coin into a visitor's hand (a job), and their people think
     better of the colony.
  4. **Gold that won't stay.** What is meant for Ducat is carried to a visitor or a trader and given, not left at a
     shrine (see D).
  5. **Debts kept** (as now).
- **What it changes:** trade, silver, debts, visitors' goodwill.
- **Walking form:** a small golden beetle (the game's megascarab or Alpha Animals' black scarab, tinted gold, harmless,
  never tamed) walking from the store to the gate.
- **Replaces:** the shared "way" look it has with Ennu (its own: gold glints, a coin trail); the shared presence.

**Shared pieces that stay shared:** the moment's weight (normal speed, the camera, the music falling silent, the
sting), the gold card's frame, the colony's book, the hands in the game's events. They are the frame; what happens
inside it becomes each god's own.

## D. Shrines and offerings made clear

**How it works now, step by step:**
1. Any of the colony's altars, ritual places (Ideology's, Alpha Memes', Vanilla Ideology Expanded's), sculptures and
   statues, Royalty's nature shrines, an ancient terminal, or anything called an altar or shrine, gets a button,
   "Dedicate to a god". The menu lists the gods the colony knows ("prayed to here", "heard of").
2. A colonist (the priest, else one who holds that god, else anyone free, never a scoffer) walks over and says the
   words. The news says "Mira gave the statue to Hollin, and it is Hollin's chapel now." The inspect line says whose it
   is (shrine, chapel, church or terminal by the colony's age; a temple indoors in a fine room).
3. **Offerings:** each day a dedicated place has a chance (18%, more with devout colonists, a priest, or the god the
   council honours; at most 2 a day at a home) that someone carries something from the stores and leaves it beside the
   place, forbidden, with a word to the god.
4. **Why muffalo wool:** the carrier takes the best thing the stores can spare that the god likes. Wool counts as
   cloth. Gods who like cloth: Ennu, Mog, Cirrus (and Undertow takes anything), so the shrine was one of theirs. The
   thing must be spare (the colony keeps three times as much), cheap (worth about 20 silver plus 3 a colonist), fresh,
   and not a minified thing. Wool is cheap, piles up from muffalo, and is nearly always spare, so it wins whenever the
   colony has fewer spare meals than it keeps for itself. For Cirrus it is worse: its best liking is "art", but stored
   sculptures are minified and never offered, so Cirrus is only ever given cloth.
5. Between one and four in the morning the god takes it: it vanishes, and the news says "gone by morning: 16 muffalo
   wool. Nobody saw it go." When the god is drawn near and someone is there, it goes in a breath of light with the full
   moment. An animal eating it or a passer-by taking it is told too, and counts the same.
6. **What comes of it:** hidden favour. Over the following weeks favour makes the god's kindness likelier, prayers
   likelier answered, a dream gift possible, a weather answer possible, and draws the god nearer. None of it is linked to
   the shrine or the offering where the player can see it.
7. **Prayers** at a shrine comfort more (5 strain instead of 3) and are likelier answered; the god's festival rite is
   kept there; a temple vow is kept by a shrine in a fine room.

**What is unclear now:** why that thing (the bubble has no line for cloth), what the god made of it (nothing to see),
what came of it (nothing tied to it), and what the shrine is for (no plain word on dedication). Leaving an offering
gives no mood.

**Proposals:**
1. **Say what a shrine is for, plainly, once:** on dedication, a card in people's words: "From now on the colony leaves
   gifts for the Guest here, prays here in hard times, and keeps the Open Door here in winter." That is what the
   colony will do, not the god's rules.
2. **The shrine shows whose it is:** its sign over it (A), and a small life of its own that grows while it is tended:
   moss and small flowers round Hollin's, warm glowing stones at Smoulder's, a ring of mushrooms at Morel's, a lamp that
   stays lit at Pip's, frost at Hush's, a puddle and frogs at Murmur's, a glint at Ducat's, a silver glow by night at
   Selene's, birds perched at Cirrus's, a cat asleep on Mog's, nothing at all at Helios's. Neglected, it gathers dust.
   This is the colony's care, a pawn fact, never favour.
3. **The inspect line, plain:** "Ennu's chapel, given by Mira. Last left here: 16 muffalo wool, three days ago, gone by
   morning. People here hold that Ennu loves a bowl and a warm bed (so Mira holds)." The last part comes from the book's
   "Of what it loves", so it is attributed and can be wrong.
4. **The thing chosen by what the colony believes the god likes,** and the carrier says why, one sentence: "Wool for
   the Guest's bed, whoever comes tonight." A line for every kind each god takes (cloth has none now).
5. **Each god's own way of taking** (replacing the one line for all):
   - Hollin: it sinks into the soil and a sapling stands there by morning.
   - Smoulder: silver dropped down a crack in the rock; the floor is warm after.
   - Bellwether: sweet grass left at the edge of the land, grazed by morning, a hoofprint beside it.
   - Morel: it rots overnight into rich soil with a mushroom on it.
   - Ennu: a bowl and a bed made up; footprints in to it and none away.
   - Pip: a part left at a machine; it is gone and a broken machine nearby works.
   - Cirrus: not a thing but a night on the roof, looking up (a vigil); fixes the cloth-only bug.
   - Hush: warm clothes set out are frosted over by morning, then gone; or given to the coldest colonist.
   - Undertow: whatever is left is gone, and something else is missing too.
   - Helios: never taken (C, 10).
   - Selene: silver set out under the moon, gone only on a night she shows her face.
   - Mog: a cat eats it at dawn: that is her taking, not a theft.
   - Murmur: poured or dropped into a well or the marsh, with a croak from below.
   - Ducat: given to a visitor or a trader, never left lying.
6. **What comes of it, seen and attributed:** in the days after, a small sign of the god near the shrine (a sapling,
   a glint in the rock, a cat asleep on the step), and someone says "It was the wool, I'd bet." Possibly wrong, never the
   god's mind. The book keeps "Mira left wool at the Guest's chapel" and what someone believed came of it.
7. **A small mood:** leaving an offering, +1 for a day for one who holds that god; praying at a shrine, +2.
8. **Further:** shrines by age from the mod pool (Alpha Memes' megalith, obelisk, urn and Kemetic slabs, Vanilla
   Furniture Expanded - Art's statues, plinths and reliefs, Dragons Descent's dragon statue and altar for Smoulder,
   Royalty's nature shrine for Hollin); two shrines of rival gods side by side set their keepers bickering; a shrine
   wrecked in a raid is mourned by those who held its god; pilgrims of a people come to a famous shrine.

## E. Pawns and their gods

**How it is now:** each colonist has one god, decided by their faith's gods (none, since Ideology's deities are
ignored), else the colony's gods picked by the pawn's id number, or by the moment's domain (the healer's god for a
sickness). It changes only when two crises of faith turn them to another god. Devout and scoffer come from Ideology's
certainty or a pawn's character (one in seven are scoffers by who they are). Nothing shows a colonist's god except their
job report ("praying to Hollin"), their bubbles and a journal line.

**Proposal: a belief per pawn in each god they know,** as a pawn fact, never the god's favour.
1. **What draws them to a god:**
   - **Character:** Kind and Wanderlust to Ennu; Greedy and Tycoon to Ducat; Night Owl and Insomniac to Selene;
     Ascetic, Tough and Cold-inclined to Hush; Pyromaniac, Heat-inclined and Child of the Mountain to Smoulder;
     Too Smart and Transhumanist to Pip; Ecologist and Body Purist to Hollin; Animal Lover and Menagerist to Bellwether;
     Cat Person to Mog; Child of the Sea to Murmur; Psychic Sensitivity and Tortured Artist to Cirrus; Cannibal and
     Psychopath open to Undertow (Vanilla Traits Expanded's by name).
   - **Work they love:** plants to Hollin or Murmur, mining to Smoulder, animals to Bellwether or Mog, medicine to
     Morel or Selene, crafting to Pip, social to Ennu or Ducat, art to Cirrus.
   - **Body and kind:** phytokin to Hollin, fungoid to Morel, lycans to Selene, androids to Pip, the merren to Murmur
     (Vanilla Races Expanded and the Merren, by name).
   - **History:** survived a plague (Morel), taken in as a stranger (Ennu), nearly froze (Hush), struck by lightning
     (Cirrus), bonded with a cat (Mog), sat by the white thrumbo (Bellwether).
   - **What they saw or were told:** a sign they witnessed raises their belief in the god they took it for, even the
     wrong one; a tale heard, a sermon, a dream.
   - **Where they came from:** a recruited pawn brings their people's gods; a pawn of the colony's faith leans as its
     memes do (F).
   - Never from parents or family lines: children learn the gods from whoever tells them stories, the colony as a whole.
2. **How it grows:** a prayer, an answered prayer (more), an offering carried, a vow kept (more), a festival kept, a
   sign seen, a dream of the god, the priest's words.
3. **How it fades or turns:** slowly with time unused; an unanswered prayer (a crisis, as now); a vow broken (guilt);
   a scoffer's mockery (a little); a god's anger that hurt them or someone close can turn belief into dread (they still
   believe, now in fear); two crises turn them to another god (as now, now through these beliefs).
4. **What it does:**
   - whom they pray to and whom they swear by (replacing the id-number pick);
   - who carries offerings to which shrine;
   - their mood when their god's sign is seen (+), its shrine wrecked (-), its festival kept (+);
   - a firm believer may talk a doubter round to their god (a social talk, "spoke of Hollin");
   - believers of rival gods quarrel (Hush's and Smoulder's people over a warm winter; the rain quarrel, as now);
   - **their own little rites,** a habit seen on the map now and then: Hollin's touch a tree trunk in passing,
     Smoulder's drop a coin into a crack, Bellwether's leave the grass long, Morel's bury scraps, Ennu's set an extra
     bowl, Pip's tap a machine twice, Cirrus's look up at dusk, Hush's save the last crumb, Selene's tell their dream at
     breakfast, Mog's feed the cats, Murmur's hum at the well, Ducat's flip a coin to a visitor, Helios's face the
     sunrise, Undertow's whisper at night (seen only by someone awake).
5. **How it shows:** on the colonist's Culture tab (Rimfolk's, where their faith already is), a section "Their gods":
   each god they know by the name they use, and how they hold it in words: doubts it, a little, holds to it, devoted,
   fervent, in dread of it. In the Gods' book, under each chapter, "Who holds to it here: Mira (devoted), Bram (a
   little)". In their journal when it turns. Never a number, never the god's view of them.

## F. Ideology

**How it is now:**
- The "gods registered to an ideology" are Ideology's own deities on the ideoligion screen: names and titles the game
  generates ("Aranea, goddess of the hunt"), a gender, a generic icon, a related meme. Rimfolk ignores them
  (`Gods.OfIdeo` returns nothing, "step 1 of the design's Ideology plan"), so the player sees two unrelated sets of gods.
- One place mixes them: a neighbour's sermon in a letter names its faith's first deity (`{DEITY}`).
- Ideology itself uses its deities for its precept and ritual names ("{keyDeity}"), its name and description, and turns
  meditation for joy into praying when a faith has any deity.
- Rimfolk uses from Ideology: certainty (devout, scoffer), altars and ideograms as shrines, the chaplain as priest, a
  faith's lost relic as a god's gift. All 23 of Rimfolk's peoples have a fixed ideoligion (name, description, memes) but
  no preset deities, so theirs are random too.

**What the 1.6 game code allows** (`IdeoFoundation_Deity`):
- A deity is a name, a title (`type`), a gender, an icon path and a related meme, all saved with the ideoligion.
- The editor's deity dialog changes only name, title and gender, never the icon. Its buttons: randomize, add, remove,
  regenerate. The deities are also regenerated when the structure meme changes, when the culture changes, and when a
  reform randomizes the ideoligion.
- How many: set by the structure meme: none for Animist, Ideological and Archist; 1 to 4 for Theist Abstract; 2 to 4
  for Theist Embodied; 1 for the Christian and Islamic origins; 4 fixed for the Hindu. Code can set any list
  (`SetDeities`), but the editor's add button stops at the meme's limit.
- A faction def can preset its deities (`deityPresets`: name, title, gender, icon) when the faction is made.
- Renaming triggers new precept names and a new description.
- An icon path to Rimfolk's texture shows on the ideoligion screen, but if Rimfolk is ever removed the game logs a
  missing texture for it. Anything touching this must also pass the 1.7 check (`Tools/ApiDiff`).

**The three ways:**
1. **(a) The colony's gods become its faith's deities.** Feasible in code (set the list, rename, give Rimfolk's sign as
   icon). Against it: at most four fit, and a medieval colony holds nine; the editor and a reform randomize them away
   (Rimfolk would have to put them back each time); a full list of the gods with their titles on a menu screen is the
   "fancy list" the player asked to remove on 11 November; and it shows gods the colony may not have heard of.
2. **(b) The faith's deities kept as saints** (the player's idea). The deities stay Ideology's, and Rimfolk reads them as
   the faith's holy (or unholy) people: legends between the gods and the colonists, each tied to one of the fourteen by
   its own words (Rimfolk already has the pure code for this, `GodsMath.DeityDomains`: a deity's name, title and meme
   to a domain, then to the god of that domain). "Aranea, goddess of the hunt" becomes, in Rimfolk's words, "Saint
   Aranea, who first followed the Old Horn". Feasible with no edit to the ideoligion at all; survives the editor (a
   randomized deity is simply a new saint, the old one remembered as a saint no longer kept); works for every faith,
   NPC peoples included; without Ideology, the colony's own saints (below) still work.
3. **(c) The deities as the faith's local names for the fourteen** (the old plan's step 2). Feasible, but the
   generated titles rarely fit the fourteen ("god of war" has no god here), so the player would still see names that
   don't match.

**Recommendation: (b), saints.** Concretely:
1. The ideoligion's deities are the faith's saints. Their related meme and title pick which god each one served
   (shown as the faith's belief: "the faith holds that Aranea first followed the Old Horn").
2. Saints are people, so they can be named plainly; the gods behind them stay unseen. A colonist prays to the saint
   ("Saint Aranea, ask the Old Horn for me"), the faith's rituals named after the saint become the saint's feast days.
3. Unholy saints: a faith whose memes lean to the dark (raider, cannibal, Alpha Memes' and Vanilla Ideology Expanded's
   darker memes) holds its deities as the damned who bargained with the Hunger.
4. **The colony's own saints:** a colonist who died keeping a vow, sheltering the stranger, or in a god's great moment
   may be named a saint by the council (a question; the colony is the family, so saints come from its people, never a
   bloodline). Added to the faith's deities while the meme allows one more, else kept in the Gods' book only.
5. A saint's relic (Ideology's relic precept, Vanilla Ideology Expanded - Relics and Artifacts), a saint's statue at a
   shrine (Pawns on Display, G), a saint's day on the calendar.
6. Optional, only if the player wants it: the ideoligion screen's header "Deities" relabelled "Saints" (a Rimfolk
   language override of the game's own word; affects every faith), and new deities' titles written as saints' titles.
7. The faith's memes and precepts lean its believers toward gods (E), read only: Tree Connection and Nature Primacy to
   Hollin, Animal Personhood to Bellwether, Darkness to Selene and Undertow, Transhumanist to Pip, Raider and Cannibal
   to Undertow, Loyalist to Ennu, a precept against tree cutting pleasing Hollin's believers. Rimfolk already has the
   meme table (`GodsMath`).
8. Later, if wanted: a precept "the god our faith honours" with one choice per god the colony knows, as the faith's own
   creed. Held back for now: the editor would list gods the colony has not heard of unless Rimfolk hides them.

## G. Mods that would help

**Already in the player's list (active), to use more:**
| Mod | Workshop | Version, kept up | Use |
|---|---|---|---|
| Alpha Memes | [2661356814](https://steamcommunity.com/sharedfiles/filedetails/?id=2661356814) | 1.6, 1.7 | shrines (megalith, obelisk, urn, skull, cave paintings, Kemetic slabs, sphinx `AM_Sphynx`, mummies for the Mog remake), chants |
| Vanilla Furniture Expanded - Art | [1968134023](https://steamcommunity.com/sharedfiles/filedetails/?id=1968134023) | 1.5 to 1.7 | statues, obelisk, plinth, relief as shrines carrying the sign |
| Dragons Descent | [2026992161](https://steamcommunity.com/sharedfiles/filedetails/?id=2026992161) | tags to 1.6; a player reports 1.7 works | Smoulder's true form; its dragon statue and altar as his shrine |
| A RimWorld of Magic | [1201382956](https://steamcommunity.com/sharedfiles/filedetails/?id=1201382956) | tags to 1.6, updated Dec 2025; comments report trouble on 1.7 | motes, holy light, `DivineBlessing`, `DarkClouds`, healing rain, golems |
| Alpha Animals | [1541721856](https://steamcommunity.com/sharedfiles/filedetails/?id=1541721856) | 1.6, 1.7 | angel moths (Selene), black scarab tinted gold (Ducat), frostbound behemoth (Hush) |
| Vanilla Events Expanded | [1938420742](https://steamcommunity.com/sharedfiles/filedetails/?id=1938420742) | 1.5 to 1.7 | long night, whiteout, swelter, as presences' patterns |
| Vanilla Landmarks Expanded | [3656316229](https://steamcommunity.com/sharedfiles/filedetails/?id=3656316229) | 1.6, 1.7 | skygazing, no wind, rotten stench, geomagnetic storm, fertile rains |
| Alpha Biomes | [1841354677](https://steamcommunity.com/sharedfiles/filedetails/?id=1841354677) | 1.5 to 1.7 | petal storms (Hollin), volcanic ash (Smoulder) |
| Seasonal Weather (Continued) | [2045114229](https://steamcommunity.com/sharedfiles/filedetails/?id=2045114229) | to 1.7 | fog-snow (Hush), double rainbow (after Murmur's rain) |
| Vanilla Ideology Expanded - Dryads / Anima Theme | [2720631512](https://steamcommunity.com/sharedfiles/filedetails/?id=2720631512), [2666998627](https://steamcommunity.com/sharedfiles/filedetails/?id=2666998627) | 1.5 to 1.7 | Hollin's dryads and anima looks |
| Vanilla Races Expanded - Fungoid, Phytokin, Lycanthrope | [3042690053](https://steamcommunity.com/sharedfiles/filedetails/?id=3042690053), [2927323805](https://steamcommunity.com/sharedfiles/filedetails/?id=2927323805), [3114453100](https://steamcommunity.com/sharedfiles/filedetails/?id=3114453100) | 1.5 to 1.7 | who is drawn to Morel, Hollin, Selene; Morel's mourner's body |
| Moonlight Continued | [3261311563](https://steamcommunity.com/sharedfiles/filedetails/?id=3261311563) | tags to 1.6, updated Jun 2025 | its dark and bright nights match Rimfolk's moon; keep them aligned |
| Odyssey (DLC) | | | colossus toad, bullfrog, scimitar cat, crow, statue, spores, deep freeze, torrential rain |

**To add:**
1. **Pawns on Display** ([3446605621](https://steamcommunity.com/sharedfiles/filedetails/?id=3446605621); tags 1.5,
   1.6, 1.7; updated 12 October): statues that copy a colonist's or a colony animal's look (on Odyssey's statue). Use:
   a saint's statue at a shrine (F), Mog's statue, the beast that sat by the colony. Worth adding.

**Looked at, not recommended:**
- Greek Sculptures (Continued) ([3157382550](https://steamcommunity.com/sharedfiles/filedetails/?id=3157382550); to 1.6,
  updated July 2025): statues of real-world gods (Aphrodite and others) would blur the colony's own gods.
- Gods of Elona: a second gods-and-altars system; it would compete with Rimfolk's.
- Kemetic Decorations ([2836144432](https://steamcommunity.com/sharedfiles/filedetails/?id=2836144432)): tags 1.3 and
  1.4 only, last updated 2022; Alpha Memes already has the Kemetic pieces.
- Music: no sacred or ambient music pack could be confirmed as kept up on 1.6 or 1.7 (the Music Expanded family has
  reports of trouble on 1.6). The gods keep using the game's and the DLCs' tracks, and the optional folder
  `Sounds/Rimfolk/Gods/<domain>/` stays the way for the player to add their own; Music Manager (active) shows what plays.

## H. Questions for the player, and the order of building

**Questions:**
1. **The signs:** do the fourteen read right (`docs/gods-symbols-draft.png`)? Any to change? May they show on the god's
   card when the colony is sure, on shrines, and over a god walking plainly?
2. **Plain words:** may each chapter of the Gods' book open with the one-line meaning (B), in the keeper's hand?
3. **Presences with real effects:** is it right that each god's presence changes the world a little (plants grow under
   Hollin's, the cold deepens under Hush's), shown as the game shows any condition, under what was seen?
4. **Ideology:** saints (recommended), the colony's gods as the faith's deities, or both? May Rimfolk relabel
   "Deities" as "Saints" on the ideoligion screen? May the council name a fallen colonist a saint?
5. **A pawn's belief:** shown on the Culture tab and in the book (recommended), or only through what they do and say?
6. **Mog:** still to be remade as the Egyptian cat god (the held to-do)? The drafted sign fits both.
7. **How strong:** gentle flavour, or noticeable effects (the numbers above)?
8. **Pawns on Display:** add it to the list?

**The lead's recommended order** (bug fixes first, then what makes the gods understandable, then what makes them unique):
1. **Round 0, fixes:** Cirrus can only ever be given cloth (art is never offered, since stored art is minified); no
   bubble line for cloth; leaving an offering gives no mood; a colonist's god is picked by id number.
2. **Round 1, understood:** the signs (A) in the book, on shrines and cards; the plain lines (B); shrines made clear
   (D 1 to 4 and 7).
3. **Round 2, each god's presence:** the fourteen presences (C) replacing the shared one, each god's own way of taking
   an offering (D 5, 6), each god's own omens where it has only shared ones (Bellwether, Mog, Ennu).
4. **Round 3, pawns and their gods** (E).
5. **Round 4, each god's happenings** (C), in two batches of seven: Hollin, Smoulder, Bellwether, Morel, Ennu, Pip,
   Cirrus; then Hush, Undertow, Helios, Selene, Mog, Murmur, Ducat.
6. **Round 5, Ideology's saints** (F).
7. Held to-dos, each when the player asks: Mog remade as the Egyptian cat god (fold its sign and presence in), more of
   Old Powers' kind, quests and storylines with the gods.

## Status

**Built:**
- 0.215.0, the core: a generated pantheon, patrons and a Gods page (retired in 0.217.0; the domain tables stay as
  fallbacks).
- 0.216.0, the presence: how a god looks and comes (looks, the avatar, the moment, the card, the sky's condition, auras).
- 0.217.0, the fourteen hand-made gods as defs, unseen; the colony's own book; their hands in the game's events; the
  stories told in pieces.
- 0.218.0, on the map: Ennu at the gate, Bellwether and Mog walking, Hollin's and Murmur's risings, Smoulder turning and
  waking, the sun standing still, the moon's face and what it returns, weather as a voice, 26 own signs, marks.
- 0.219.0, the world: letters, pilgrims and the seer, kindness and anger, dream gifts, the Hunger's bargain, quarrels
  between gods.
- 0.220.0, worship: shrines, offerings, prayer, festivals, vows, the priest and four council questions, dreams, crises
  of faith, scoffers.
- 0.221.0, Mog's luck and her stare.
- 0.224.0, one way (felt), each god as near as what happens draws it, and the pace that keeps the gods varied.
- 0.226.0, the Gods' book as a holy book.
- 0.227.0, the storytellers (Old Powers weighs the gods more).
- Not released yet (14 November 2026, the player's go): rounds 0 and 1 of section H. A packed-away work of art can be
  given, every kind of gift has its words, a prayer and a gift give a small mood; the signs in the book and on a card
  that names the god (`Textures/ColonyCouncil/Gods/Signs/`, every age's form); each chapter's plain line; shrines made
  clear (the dedication card, the inspect line, the carrier's why, a gift noticed gone by day). DESIGN.md "The gods
  understood". A colonist's god is still picked as before, for round 3.

**Still open (defined, not built):**
- Mog remade as the Egyptian cat god (to-do, 13 November 2026, held).
- More of Old Powers' kind, with the gods and A RimWorld of Magic (to-do, 13 November 2026, held).
- Quests and storylines across quests, the gods among them (to-do, 13 November 2026, held).
- Ideology, step 2 of the old plan (the faith's deities as its names for the gods): never built; F proposes saints
  instead.
- From the gods' own lore, never built: Morel's mourner at the graveyard, Hollin speaking through her dryads a word at a
  time, Smoulder's bargains in dreams.
- Set aside in the pantheon brainstorm (section E): the Going, the Walking Fire, the Silent Ones, Seven, the Echo, the
  Watching, the Queen Below, the Song, the Bones, the Wind That Laughs, the Small Ones, and a god born from the colony's
  own faith.
- Not yet seen in a real game (the releases' own notes): the true dragon and the colossus toad with their mods, the
  pace over a long game, the book's look at small window sizes.
- New today: everything in sections A to F, a proposal until the player says to build it.
