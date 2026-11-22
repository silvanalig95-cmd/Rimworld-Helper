# Rimfolk portraits: the framework

The player, 22 November 2026: "a plan, process, and a framework on how to create these pawn images ... represent as many
possible combinations as possible. All the different races, ages, genders, perhaps, even injuries ... haircuts ... expand
that continuously ... applicable to all characters ... that could even appear in the game ... The test examples should be
my actual pawns ... It should not be too realistic ... not like photographic style ... still fit a bit to the RimWorld
theme ... rugged survivalist, post apocalyptic ... If a pawn ... looks super poor and survived a lot of stuff, they
should ... look more rugged than if it's like a royal member who's super rich ... but it should always kind of follow
more or less the same style."

The framework has five pillars. Each is a fixed list of words that both the prompts and the library's records use, so a
picture made today and one made in a year describe themselves the same way, and new entries only ever add to a list.

## Pillar 1: the style bible (one look for every picture)

The direction (the player, 22 November 2026, after the first test: style C liked best but "way too realistic ... it
should fit the RimWorld style much more ... look at the actual RimWorld loading screens ... the DLC images ... the
storytellers", with the game's own key art, the Biotech and the Ideology covers as references): the game's own comic
key-art look, described in words (no picture of the game's is copied or fed in):
- **Linework**: clean bold black ink outlines of varied weight; inked detail on faces (eye bags, cheek and nose lines,
  stubble, scars, wrinkles); hatching only in deep shadow.
- **Shading**: cel shading in two or three hard-edged tones, a subtle gradient inside the shadow shapes.
- **Colour and light**: rich but grounded colours, earthy for the poor and the tribal, cleaner for spacers; a cool cyan
  or blue rim light from behind, a warm key light from the front left.
- **Faces**: angular and characterful, slightly exaggerated (a strong nose, defined cheekbones, real asymmetry), never the
  generic pretty face: every person gets features of their own (pillar 2, face).
- **Frame**: a medium close-up cut by the picture's bottom edge at the belt (1024 x 1280), the head near the top and about
  a third of the picture's height, the body and arms running past the edges (so no picture ends in a cut-out bust; "from
  the waist up" alone gave nearly the whole figure, legs too), one person, bare-headed, on one flat, even dark blue-grey
  colour that is cut away afterwards (see "Backgrounds"). Said only positively: FLUX reads "no border, no hat" as a call
  for a border and a hat. Poses keep to what fits that frame (a staff gripped at chest height, a bow over the shoulder).
- **Holding the style**: every prompt starts with the style and ends with it repeated (`style_end`), so a reptile, an
  elf or a child is drawn the same way as a baseliner. FLUX ignores the negative prompt at its usual settings, so what
  matters is said positively. Once 20 to 40 pictures are chosen, a small style LoRA trained on them on the player's card
  holds the look for every later picture.
- **Chosen** (the player, 22 November 2026, after the LoRA tests: "this Henry Daubrez one, the style L9S looks really,
  really cool ... these paintings ... already have a lot of character just from the style. So that is the one I want to
  go with"): the Henry Daubrez painterly LoRA (Civitai 696050, `FLUX-daubrez-DB4RZ.safetensors`, 0.9) with the SameFace
  Fix at -0.7, loose painted brushwork in muted colours; `batches/style.json`. Earlier candidates (`style-A` to `L9s`)
  stay for reference.
- **Children and babies**: the same style, as fits their age, never made to look older; plain, real children's faces.
- **Eyes**: small and in proportion to the face, for everyone (the player, 22 November 2026: "I don't like it when the
  people have these huge "pretty face" eyes compared to the rest of their body and face. they can surely have
  exagaratted features, but this eye and face thing is such a classic AI giveaway. it's much more prominent on babies,
  kids and females in genera,l"). The style line says "natural proportions with small eyes in proportion to the face";
  grown-ups' prompts add "the eyes of ordinary size for the face", children's "a plain, real child's face with small
  eyes"; no "big round eyes", "wide-eyed" or "soft, rounded features" anywhere.
- **What varies, inside the style**: wealth and hardship (pillar 4), the age of their dress, the kind of person, the angle,
  the pose, the expression and the face. Never the brushwork, light or frame.

## Pillar 2: the person (what every picture's record says)

| Axis | Words |
|---|---|
| kind | a xenotype or race, by its defName (`Baseliner`, `DevDesigner_XenotypeDarkElves`, `VRESaurids_Saurid` ...) |
| sex | male, female |
| age | how old they look: baby (0 to 1), toddler (1 to 3), child (4 to 12), teen (13 to 17), young (18 to 29), adult (30 to 44), middle (45 to 59), old (60 to 74), elder (75+), the grown-up years for a baseliner, stretched for long-lived kinds |
| build | slim, average, heavy, muscular |
| skin | a colour (and for scales, fur or bark the same) |
| hair colour | a colour |
| hair | bald, buzzed, short, medium, long, braids, tied, bun, topknot, mohawk, curly, dreadlocks |
| face hair | none, stubble, short beard, full beard, moustache, goatee |
| features | from the kind: pointed ears, long ears, fins, frills, horns, tusks, snout, fur, scales, gills, glowing eyes, markings, extra eyes ... |
| marks | lost eye, eye patch, bionic eye, scar, burn, missing ear (the game reads these from the body; a missing arm and a face tattoo are proposed: `catalogue/marks.csv`) |
| looks | the catalogue's look words (`catalogue/looks.csv`): faces and bearing from traits, A RimWorld of Magic's classes, psycasters, implants, genes that show, and the special looks (conjoined, wings, extra limbs, ghouls, the undead) |
| role | scholar, doctor, soldier, warrior, hunter, scout, builder, miner, crafter, smith, farmer, herder, cook, artist, trader, leader, priest, labourer (the game reads it from the adult backstory, else the best skill with a passion; none for children; `catalogue/roles.csv` has what shows each in the four ages of dress) |
| expression | calm, warm, stern, weary, wary, proud, cheerful, grim |
| face shape | oval, round, square, long, narrow, heart, diamond, gaunt, broad |
| nose | straight, hooked, crooked, broad, flat, snub, upturned, bulbous, long, broken |
| eyes | deep-set, hooded, heavy-lidded, wide-set, close-set, narrow, round, almond, with dark circles, crinkled |
| brows | thick, sparse, arched, straight, heavy, scarred, joined |
| mouth and jaw | thin lips, full lips, wide mouth, small mouth, lopsided grin, overbite, gap teeth, weak chin, strong jaw, cleft chin |
| skin detail | freckles, moles, acne scars, pockmarks, wrinkles, crow's feet, sunburn, stubble shadow, birthmark |
| facing | front, left, right, three-quarter left, three-quarter right |
| pose | arms crossed, arms folded, hands clasped, a hand on the hip, a hand on a weapon, a weapon on the shoulder, the tool of their trade in hand, holding something of their own up to the chest, leaning forward, hunched, standing square; the head tilted or the chin raised |

Every new person in a batch gets two or three face words drawn at random (and kept for that character), so no two
read alike and none falls back to the generic face. The pose (the player, 22 November 2026: "the poses ... should maybe
also allow some variety ... give character ... more fitting to the pawn and the traits and how they look like") comes
from the person: their traits, work and bearing (a guarded scout with arms crossed and a sidelong look, a calm surgeon
with hands clasped, a warrior with a sword on the shoulder); it goes in the batch list's `pose` column. Hands show now
and FLUX gets some wrong: those pictures are thrown out.

## Pillar 3: the kinds (any race or xenotype, including ones not met yet)

Every kind has a look sheet: what it adds to a human picture (or replaces), in plain words for prompts, and which words of
pillar 2 it uses, and how it ages (the player: "the age range should always be corresponding to the xenotype ... some
... have a much longer age expectancy than others ... some even can live forever technically ... Not that an ... elf
that's still young with a certain age is shown as an old person"). A picture's age is always how old the person looks,
and its prompt says so ("looks about 30"), never their years: the game reads the band from the pawn's biological age,
stretched by its race's life expectancy and its genes' lifespan factor; an ageless pawn (Biotech's Ageless, the elves'
ageless gene) still grows older in looks, slowly, as if it lived ten times as long (the player: "ageless pawns can also
look ... old, middle-aged, or even elderly over time if they are like really super old"): young at 200, adult at 300,
middle-aged around 500, old around 700, elderly past 800. The picture never holds the years, only the look, so one
picture of "an elf who looks about 30" serves an elf of 30 and an ageless one of 300 alike. The player's active mods change ageing with: Ageless,
Deathless, DevDesigner_Ageless, DV_LifespanLong, AG_ExtraordinaryLifespan, AG_SlowerAging, AG_FasterAging,
VRE_SlowAging, VRE_FastAging, VRE_RapidAging, GS_RapidAge. The look sheets are the rows of `catalogue/kinds.csv` (82 kinds:
every active xenotype and the three humanlike races besides the human), written from the kinds' genes in the player's save
where it holds them and from the mods' gene names and descriptions otherwise (marked `guessed`); a kind new to the game
gets a row the same way (`catalogue/README.md`, "Adding a new mod's kinds"). Until a kind has pictures of its own, its people
keep the game's own drawing (the player: "it should just show no image ... not take another xenotype's image. That will
also immediately inform me that I need to generate images for that xenotype"), and `missing.csv` lists the kind. The order of work follows the player's world (League of Toba, day 138): the waves of `catalogue/kinds.csv`, by
each kind's weight (how many of its people the save holds, and the factions that bring it): wave 1 baseliners and dark
elves; wave 2 hussars, neanderthals, impids, pigskins, wasters and saurids; wave 3 genies, starjacks, yttakin,
sanguophages and the high and wood elves; wave 4 the kinds the save holds a few times; wave 5 the rest.

## Character, and only what they have

The player, 22 November 2026: "in the prompt description, you can also include, more eccentric and noticeable and
special visuals also ... there can definitely be a lot of variety and range, especially when it comes to the NPCs ...
but it should always be thematically fitting and based on an actual combination and only applied in this combination
of pawn characteristics ... maybe it's holding a flame or something because it's a fire mage ... As long as you don't
get like someone doing ... a fire spell for someone who can't even do magic ... Otherwise, it should ... rather choose a
default one that doesn't have special things like that." And: "the profession can and should also be reflected in how
they look like ... science ... a scientist ... soldiers being soldiers".
- **Defaults first**: every kind, sex and age band gets plain pictures (no look, no role, nothing held that a skill
  would need) before any showy one, varied in face, expression, hair, build and bearing.
- **A look or a role only for those who have it**: the game gives a picture with a look (a pyromaniac's lit match, a fire
  mage's flame, a brawler's taped knuckles) or a role (a soldier's rifle, a scholar's book) only to people who have that
  trait, class, condition, gene or role; plain pictures go to anyone of their kind, sex and age.
- **Special looks both ways**: conjoined twins, wings, extra limbs and the like only get pictures made for them, and
  those pictures go to no one else.
- **Range**: within a row's real characteristics the subject can be eccentric, funny, intense, shy, proud, weathered or
  wild-haired; the rarer kinds and the later rounds get the widest range. A prompt never adds a prop, a power or gear
  the row's looks and role don't stand for; anything it adds that the game should check goes into the row's `looks` or
  `role`.

## Pillar 4: life on them (wealth and hardship, in the same style)

Two short phrases a prompt adds, read in the game from the pawn (its clothes' worth and quality, a royal title, a slave's
or prisoner's state, its mood and health, its backstory):

| Wealth | phrase |
|---|---|
| destitute | ragged, patched clothing, a gaunt face, grime |
| poor | worn, mended practical clothing |
| common | practical clothing, clean but well used |
| prosperous | good tailored clothing, well groomed |
| noble | rich fabrics with fine trim and embroidery, jewellery, an air of command |

| Hardship | phrase |
|---|---|
| sheltered | smooth skin, rested eyes |
| ordinary | lightly weathered |
| hard | wind-burned, weathered skin, tired eyes |
| brutal | deep lines, old scars, a hard stare |

And the age of their dress: tribal (hide, fur, woven fibre, bone, beads), medieval (wool, linen, leather, mail),
industrial (canvas, denim, wool coats, flak vests), spacer (synthetic flight suits, sleek armour, soft-glowing seams).

## Pillar 5: characters and variants

A picture is a character. The same character can have variants made from it (with the same seed or from the picture
itself): a lost eye or eye patch, a fresh scar, a burn, a bionic eye; older (one age step); richer or poorer. A pawn keeps
its character for life and moves between its variants as its body and life change.

## Pillar 6, later: animals and other creatures

The player: "babies/kids should also have images, later also animals & other creatures". Babies and children are part of
pillar 2 from the start (a baby or toddler as a head-and-shoulders picture in a blanket, sling or small clothes). Animals,
mechanoids, insectoids and the other creatures get a library of their own after the people: one look sheet per species
(the game's and every mod's animals, read from their defs the same way as the kinds), pictures by species, young or grown,
sex where it shows, colour or coat variant, and marks (a lost eye, a scar, a bionic part), in the same style bible with a
"creature" framing (head and chest, the animal's own pose). A bonded or named animal keeps its picture like a colonist.

## Backgrounds

The player, 22 November 2026: "Is there like a plan to have transparent backgrounds and kind of fill this with something
else? Or ... background images that can be applied to each of the kind of character images ... It shouldn't ... distract
too much, but just having a blank color can also be a bit ... boring", and then: "I will want to have hundreds, if not
thousands of different images ... So I don't know if removing a background for each of them anyways is a sustainable
solution. I mean, I cannot upload that many images to you to edit."

- **Cut out on the player's computer, in the same run**: a background remover in the ComfyUI workflow (BiRefNet, from
  the ComfyUI-RMBG node pack; `SETUP-ComfyUI.md` step 6) saves a transparent copy of every picture next to it
  (`..._cut.png`), with no work per picture. Tested here on the first five Daubrez pictures: clean edges even on the
  loose, brushy hair, and the painted seal the style sometimes adds in a corner goes with the background.
- **Backdrops, in the same style**: `batches/backdrops.csv` with `style-backdrop` (no cut-out): twelve kinds of room in
  the four ages of the colony (tribal, medieval, industrial, spacer: bedroom, barracks, hospital, prison cell, throne
  room, dining room, common room, kitchen, workshop, study, shrine, storeroom), sixteen outdoor looks that the player's
  56 biomes fall into (temperate and boreal forest, tundra, ice, grassland, shrubland, desert, jungle, swamp, coast,
  wasteland, volcanic, glowforest, cave, space, mountain), and three plain painted grounds. 67 places, two of each.
  Framed like a camera's view of part of a larger place (the player, 22 November 2026: "There's always something right
  at the border and it revolves around the space between the borders, which doesn't always make sense, like in a cave
  or a canyon it might do, but not like in an open plain ... It should rather be imagining kind of as if it's a camera
  that photographs a specific spot that just happened to be, and the camera only takes up the specific rectangle"):
  the place carries on past every edge, nothing arranged around the borders (the first style asked for "the detail
  towards the edges", which caused it).
- **In the game** (to build once the backdrops are made): behind the cut-out person, the backdrop of where they are: the
  room's kind and the colony's age indoors, the biome outdoors; day or night and the weather as a tint; drawn softer,
  darker and out of focus, with a light shadow behind the figure, so the face stays first. A plain painted ground is the
  fallback, and a setting chooses backdrops, plain, or the picture's own background.
- **How they go together** (tried on the player's first pictures, 22 November 2026, the player: "whether the
  backgrounds are then too noisy or ... distracting from the pawn ... think about how to best kind of put it
  together"): the backdrop as painted is too busy (a stone wall fights a saurid's quills); far back or only its colours
  loses the place. The player then: "I don't mind the as is painted version because the background images look really,
  really cool and I don't mind seeing more of it ... maybe ... a very, very small, minor, softened effect ... Perhaps
  there could be a little bit of a border around the colonists, kind of a line around it that fits, that highlights them
  a bit without losing details on the background." So: the backdrop's middle band behind the head, kept almost as
  painted (a blur of 0.3% of the panel, 86% brightness, 92% colour, a light darkening at the corners), and the person
  set off by its own edge: a soft dark halo just round them, a faint pale edge (which keeps dark hair apart from dark
  beams) and a thin dark ink line, all following the cut-out's shape. A pale line on its own looks like a sticker and is
  left out. Optional: the person tinted lightly (about a fifth) towards the room's light. The crop
  follows the panel's size: at 96 pixels the head fills 45% of it, at 360 pixels 30%, so a small panel shows the face
  and a large one the pose; the head is found from the cut-out's own shape (its top and the neck).
- **Nothing of this is baked into the pictures** (the player: "if we do have to regenerate every single image again ...
  Could this be adjusted also later on, even with a large library"): the library keeps each person as a plain cut-out
  and each backdrop as painted; the game picks the backdrop, softens it as the setting says, draws the halo, pale edge
  and ink line from the cut-out's own shape, crops by the panel's size, and keeps the result until the pawn's place, the
  panel's size or a setting changes. So the backdrop's softening (as painted, light, softened), the line round the
  person (off, subtle, stronger) and the light tint are settings, changed in play for every picture at once. The pack
  stores pictures large enough for the 360-pixel panel; the full-size pictures from ComfyUI stay with the player as the
  archive, so even the pack's size can change by packing again, never by making the pictures again.

## The process

1. **Set up** the image tool on the player's computer (`SETUP-ComfyUI.md`).
2. **Find the style**: the player's ten colonists (`batches/colonists.csv`) in five candidate styles
   (`batches/style-A.json` to `style-E.json`), two pictures each. The player picks; the lead writes the final style bible
   into `batches/style.json`, which every later batch uses.
3. **The colonists' own portraits**: the ten again in the chosen style, four each; the player keeps the best; the lead puts
   them in the game tied to those pawns.
4. **The starter set**: the planner's first lists (`Tools/PortraitArt/plan_batches.py`, `catalogue/README.md`): wave 1
   (baseliners and dark elves: plain pictures of every sex and age band across their skins and hair, one per common
   role, the looks) and wave 2's plain pictures, 728 rows (`batches/people-01` to `04`, mixed so kinds, sexes and ages take turns).
   Beside them the characters lists (`batches/characters-01` to `03`, 599 rows; `catalogue/README.md`, "The characters
   lists"): every kind of all five waves as distinct people, each face its shape, an ordinary skin and one to three
   features of its own, no beauty words, other kinds in their own shape.
5. **Waves**, by `catalogue/kinds.csv`'s waves, each the same way (plain, roles, looks, then more variety where a kind is
   common), then variants (pillar 5) for the characters most used.
6. **Growing for good**: the census (`Tools/PortraitArt/save_census.py`) reads each save the player sends into the
   catalogue, and the planner compares the library's `records.xml` and the lists with it, writes `catalogue/COVERAGE.md`
   (the gaps first) and the next lists, `missing.csv`'s needs before everything else. A new mod's kind gets a row in
   `kinds.csv` and its first rows the same way. Lists are never rewritten: new rows go into new lists, and a row's name
   says what it shows and never changes.

Each round: the player runs a batch (`Tools/PortraitArt/Run-PortraitBatch.ps1`); ComfyUI makes each picture and its
cut-out. For the first rounds the player zips the output folder with "Zip for upload" and sends it, and the lead checks
the look. For the hundreds and thousands after, every step per picture runs on the player's computer by itself (the player: "I
cannot upload that many images to you"): the chosen pictures go into the library folder, `Update-PortraitLibrary.ps1`
writes a record for each from its batch row, and Rimfolk reads the folder directly (`SETUP-ComfyUI.md` step 7).

**An evolving library** (the player: "a constantly evolving database of images for backgrounds, pawns, and also maybe
animals and creatures at a later stage ... include what is missing and not ... go through the entire list another
time"): the batch lists only ever grow; Rimfolk writes `library/missing.csv` (the kinds, ages, rooms and biomes it met
without a picture); the lead turns it into new rows; the batch tool makes only the rows the library has no picture of.
A new kind of thing (animals, creatures) gets a batch list and record columns of its own the same way.
