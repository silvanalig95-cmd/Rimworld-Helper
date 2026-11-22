# The portrait library: the plan (21 November 2026)

Superseded in its steps by `FRAMEWORK.md` (22 November 2026: the pillars and the process) and `SETUP-ComfyUI.md` (making
the pictures on the player's own computer, an RTX 4070 Ti SUPER, with `Tools/PortraitArt/Run-PortraitBatch.ps1`). The
coverage and the three ways below still hold.

The player: "we will, just basically have a large image library for as many ages and sexes and races and hairstyles and
whatever combined, kind of have everything accurately described, what each image describes. So you can always select the
most appropriate one to the pawn ... it's really important to have a lot of variety. So if it requires a ton of token and
work, I'm ready for it ... maybe even same characters. Also, when they're injured, they might have an eye less ... I want
to cover as many races as possible that are already in the game ... the portrait angle ... they don't have to always be in
the same angle looking ... So I don't have to copy paste every single prompt to Gemini if it can be done another way."

## How it works in the game

- Every picture has a record (a def): what it shows, read by the lead from the picture itself, not from the prompt. So a
  picture that came out different from its prompt is still described right.
- Each pawn gets the closest picture by its xenotype or race, sex, age, skin, hair, beard, build and what it wears, keeps it
  for good, and moves to a variant of the same face when its body changes (a lost eye, a bad scar, a burn, a bionic eye,
  growing old).
- Pictures face different ways (front, a little left, a little right, three-quarter); the game mirrors them where two
  people face each other or the portrait should face its text.
- A pawn with nothing close enough keeps the game's own face, so the library can grow in waves.

## What a picture's record says

xenotype or race; sex; age (child, young, adult, old); skin (or fur or scales) colour family; hair colour; hair (bald,
short, medium, long, braids, bun, tied); beard (none, stubble, short, full, moustache); build (slim, average, heavy,
broad); dress (tribal, medieval, industrial, spacer; and its kind: rags, work clothes, fine, robes, armour); headwear (none
by default); features (pointed or long ears, horns, tusks, fur, scales, gills, a tail tip, glowing eyes, skin markings);
marks (eye patch or lost eye, scar, burn, bionic eye, missing ear); expression; facing; the character it belongs to and
which variant of it.

## Coverage, by what the player's world holds

From League of Toba (day 138): baseliners 223 pawns, dark elves 48 (the Stormriders), hussars 19, neanderthals 12, impids
12, genies 10, wasters, saurids, sanguophages and pigskins 8 each, starjacks 7, high elves and avaloi 5, yttakin and
lesser fairies 4, wood elves 3, and one or two each of gauranlenkin, boarskin, stoneborn, gorillas, dirtmoles, bonobos,
taukai, drakonori, coastal merren, marshmallow, korg, pixie, chimp, mind devourer and animusen. 79 xenotypes are active
in the mod list. So, in waves:

| Wave | Who | About |
|---|---|---|
| 1 | Baseliners, every age, both sexes, all skins and hair, four ages of dress | 600 |
| 2 | Dark elves; then hussars, neanderthals, impids, genies, wasters, sanguophages, pigskins, starjacks, yttakin, dirtmoles, highmates | 100 + 30 each |
| 3 | Saurids, high and wood elves, avaloi, fairies, stoneborn, the Vanilla Races Expanded kinds, Alpha Genes' kinds met | 15 to 20 each |
| 4 | The rest of the 79, and the awakened apes and Paws of Interest | 8 each |
| 5 | Variants of the same faces: a lost eye or patch, a scar, a burn, a bionic eye, older | one or two per character |

Before each wave the lead writes a look sheet per kind from the mods' own files (its genes: ears, horns, skin and hair
colours, fur, tails, eyes, size) so the prompts describe it right.

## Making them at scale: three ways

1. **Free, the Gemini app, nine at a time.** One prompt asks for a 3 by 3 sheet of nine different people in the style; the
   lead cuts the sheet into nine portraits, cleans and describes each. Then "nine more, all different, same style" and so
   on: a few clicks per nine portraits, no copying of prompts after the first. The app has a daily limit; spread it over
   days. Each portrait is about 340 pixels, enough for the game's sizes.
2. **Paid, Google's API, no clicking at all.** A script made by the lead sends the prompt book and saves every picture.
   About 3 to 4 US cents a picture by Google's pricing page (check it): 1,500 pictures about 45 to 60 dollars. Either the
   player runs the script on their computer with their key, or saves the key in this session's secure key form (never in
   the chat) and the lead runs it here.
3. **Free and unlimited on the player's own computer** (a recent NVIDIA card with 8 GB or more): ComfyUI or Fooocus with a
   good model, fed the prompt book in one batch.

## Steps

1. **The style** (the player, 15 minutes): in the Gemini app, try the style lines (`README.md`), keep the 3 to 5 pictures
   you like best and send them. They become the style references.
2. **The pilot** (the player 20 minutes, the lead after): ten sheets of nine (90 portraits) of wave 1 by way 1. The lead
   cuts, cleans, lines up, describes and puts them in the game in a release; the player looks at them in the game.
3. **Waves 1 to 5**, by the way the player picks; the lead sends a prompt book per wave and releases each batch.
