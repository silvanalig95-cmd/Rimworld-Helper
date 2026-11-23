# The picture check's free answers: what the game could use

From the player's `checked.csv` of 23 November 2026: 624 pictures (characters-01 238, characters-02 199, characters-03
186, colonists 1), all asked the whole form (version 3, so no limbs line yet) by the 4B Qwen3.5 model. The raw answers
hold nothing beyond the parsed columns. Compared with the batch rows in `docs/portrait-art/batches` (588 rows, 10 of the
pictures' rows no longer in the lists) and with the check's readers redone in Python (scripts next to this file). The
counts of corrections are close, not exact: `records.xml` and the in-game corrections are not here.

## In short

- "unusual" names something in 76 of 624 pictures (12%). About 60 of those restate the person's own kind (an animal
  head, pig ears, reptilian, horns, wings, fangs, a fungal crest, leaves): the kind already decides who gets them. Only
  six name something personal, each once (tattoos, pregnant, freckles, jewellery, two heads, a metal mouth).
- The model does not volunteer what it is not asked. The prompts asked a scar in 18 pictures, earrings or studs in 91,
  jewellery in 64, freckles in 13; it named them 0, 0, 1 and 1 times. No answer mentions text, a signature, a frame, a
  second figure or extra fingers. So the free line can't feed new looks: a thing worth recognising needs a line of its
  own in the form, as the limbs line now has.
- The most useful finds are misreadings by the check's own readers (work tools as weapons, clothes as armour, antennae,
  tusks and ear fins as horns, fins as pointed ears, red eyes as glowing, the twins' "two heads"). Since ask lines are
  now applied without approval, they change records: about 100 pictures would be left out by the feature comparisons
  alone, most of them wrongly.

## 1. What recurs

Pictures per normalised thing (an answer naming two things counts for both). "Own kind" means the row's kind has it.

| Line | Thing | Pictures | Examples | Where it comes from |
|---|---|---|---|---|
| unusual | animal head ("anthropomorphic", "dragon/pig/reptilian head") | 15 | "anthropomorphic" (p_absol_f_adult_char_whitefur_bald_01); "dragon head" (p_goodra_f_adult_char_slimepurple_bald_01) | own kind |
| unusual | pig or boar ears and snout | 15 | "pig ears and snout" (p_pigskin_m_teen_char_dark_black_01); "pig-like features" (p_boarskin_m_middle_char_hazelnut_lightbrown_01) | own kind |
| unusual | horns | 13 | "horns, orange skin" (p_impid_m_young_cook_orange_orange_01) | own kind, also in the horns line |
| unusual | skin or hair colour restated | 10 | "red skin, horns, orange hair" (p_impid_m_adult_stooped_deepred_orange_01) | core lines |
| unusual | wings (and a bare "yes" on fairies, 3) | 8 + 3 | "wings" (p_courtfairy_f_middle_char_tan_green_01) | own kind, also in the wings line |
| unusual | pointed ears | 6 | "pointed ears" (p_darkelf_m_middle_char_white_black_01) | own kind, also in the ears line |
| unusual | fangs | 4 | "fangs" (p_sanguophage_m_young_char_light_auburn_01); "vampire fangs" (p_sanguophage_m_elder_char_brown_darkbrown_01) | own kind |
| unusual | fungal growth on the head | 4 | "mushrooms on head" (p_mycomorph_f_middle_char_mottled_bald_01); "pink head growths" (p_fungoid_m_adult_char_fungalgrey_rosetaupe_01) | own kind |
| unusual | reptilian features | 3 | "reptilian features" (p_saurid_m_old_char_lightgrey_grey_01) | own kind |
| unusual | animal ears (fox, cat) | 3 | "fox ears and tail" (p_animusen_f_adult_char_veryfair_blond_01); "cat ears" (p_lowmate_f_young_char_brown_auburn_01) | own kind (longEars) |
| unusual | leaves or a plant on the head | 2 (+1 in hair) | "small plant on head" (p_leafeon_f_young_char_creamfur_bald_01) | own kind |
| unusual | fur collar, fur vest | 2 | "horns, pointed ears, fur vest" (p_insector_m_middle_char_chitinpale_bald_01) | clothes |
| unusual | once each | 1 each | tattoos (p_drakonori_m_young_char_palered_darkbrown_01), pregnant (p_strigoi_f_middle_char_deadgrey_white_01, prompt: "a big belly"), skin spots (p_baseliner_f_adult_char_brown_black_01, prompt: freckles), gold necklace, watch and rings (p_fungoid_f_middle_char_fungalgrey_rosetaupe_01, prompt: jewellery), two heads (p_conjoined_m_old_char_tan_grey_01), metal mouth (p_bruxa_m_middle_char_fair_sanguine_01, prompt: fangs), long tongue (minddevourer: its face tentacles), crystal on forehead (glaceon), slime on face (ocularkin), wrinkles (starjack), artificial eye (also in eye marks) | |
| hands | a gesture, nothing held (fists, waving, thumbs up, touching hair, holding head) | 13 | "fists" (p_baseliner_m_child_char_light_auburn_01); "thumbs up" (p_baseliner_f_young_char_dark_black_03) | pose |
| hands | a belt | 2 | "holding a belt" (p_neanderthal_f_young_char_dark_black_01) | pose: thumbs in the belt |
| hands | a staff | 4 | "holding a staff" (p_impid_f_adult_herder_deepred_orange_01) | herder's tool, a stooped man's stick |
| hands | a small or wooden object | 4 | "holding a small object" (p_nereid_m_middle_char_bluefish_blond_01) | unknown |
| hands, tools | a magnifying glass | 3 | "magnifying glass" (p_hussar_f_young_crafter_veryfair_blond_01) | crafter rows |
| hands | once each | 1 each | a mug (drinker row), a small bottle (apothecary row), a bundle of grass (farmer row), a cigarette (p_baseliner_f_middle_bloodmage_fair_auburn_02: the blood mage's "ribbon of blood" read as smoke) | |
| tools | a hammer; a flashlight | 3; 1 | "hammer" (p_baseliner_m_adult_builder_fair_red_01); "flashlight" (p_saurid_m_adult_miner_lightgrey_darkbrown_01) | builder, miner rows |
| weapons | a work tool (hammer 3, axe 4, staff 1, bat 1) | 9 | "axe" (p_darkelf_m_young_farmer_white_black_01); "hammer" (p_pigskin_f_adult_builder_light_auburn_01) | builder, miner, farmer, herder rows |
| armour | clothes (overalls 2, fur-lined tunic, leather straps, military uniform) | 5 | "overalls" (p_impid_m_young_cook_orange_orange_01); "leather straps" (p_hussar_m_young_drinker_veryfair_blond_01) | work clothes |
| armour | shoulder pad | 2 | "shoulder pad" (p_venator_m_elder_char_dustyred_saltpepper_01) | one on a warrior row |
| headwear | hood 8, headband 2, scarf 1, dragon head 1 | 12 | "hood" (p_bonobo_m_adult_char_slategrey_brown_01); "scarf" (p_hussar_m_young_drinker_veryfair_blond_01, prompt: wound round the neck) | hoods not asked by any prompt |
| eyes | glowing | 26 | "glowing, red" (p_hussar_m_adult_char_brown_darkbrown_01, prompt: "vivid red eyes") | 18 on rows without glowing eyes |
| eyes | odd eyes ("one blue, one dark", heterochromia) | 5 | "normal, one blue, one dark" (p_genie_m_young_char_light_bald_01, row: lost eye); "heterochromia (yellow and blue)" (p_marshmallow_f_adult_char_whitefur_bald_01) | 3 are lost-eye or eye-patch rows |
| magic | glowing light | 1 | p_pixie_f_middle_char_light_blond_01 | a fairy's glow |

## 2. Decisions

### a. Exists already: words and readings for the check

| # | What | Pictures | Existing word | Proposal |
|---|---|---|---|---|
| a1 | Work tools read as weapons | 9 (7 would get a fighter role) | roles builder, smith, miner, farmer, herder ($ToolRoles) | A weapons answer that is the row's own work tool is not a weapon: hammer for builder or smith, axe or pick for miner, farmer, builder or labourer (the miners' "axe" is likely their pickaxe), staff, crook, stick or bat for herder. Today any non-"none" weapons answer counts. |
| a2 | Clothes read as armour | 5 (4 would get a fighter role) | dress armour, roles soldier, warrior | Read the armour line by words, not by "anything": $ArmourWords plus plate, shoulder pads, tactical or ballistic vest, helmet, mail. Overalls, tunic, straps, uniform, coat or a plain vest are clothes. |
| a3 | Antennae, tusks, ear fins and crests read as horns | 17 of the 19 horn hits on rows without horns | look `antennae`, feature `tusks` | "Horns" is antennae when the row's prompt or looks name antennae (hiveling 2, insector 2, goodra 1); tusks on a row with the tusks feature (boarskin 3, pigskin 1); nothing when the prompt names fins, spines or a crest frill (saurid 4, uredd, askbarn, abyssal merren) or the nereid's lure on a stalk: 8. The two left (one red horn on a sanguophage, small pink horns on a dark elf) are real strays and stay out. |
| a4 | Ear fins and big ears read as pointed ears | 69 pictures say pointed or long on rows with no ear feature | features `pointedEars`, `longEars` | 18 have fins, frills or spines "in place of ears" in the prompt (saurid 12, merren 4, nereid 2): not pointed ears, no line. 18 have ears that stick out, large, stubby or a single ear in the prompt: report only, not out. 33 have nothing about ears in the prompt, 32 of them kinds drawn with pointed ears that their row doesn't list (impid 4, fairies and pixies 5, drakonori, lapis, avaloi, forsaken, gauranlenkin, poluxkin, waster 2 each...): a question for `kinds.csv` (give the kind pointedEars if its own art has them), not a reason to drop the picture. |
| a5 | Coloured eyes read as glowing | 18 glow hits on rows without glowing eyes, 10 explained | look `redEyes` (gene Eyes_Red), mark `bionicEye` | "Glowing" plus the colour the prompt asks ("vivid red", "amber", "yellow" eyes: hussar 3, umbreon 2, saurid 2, wolfman, ekkimian) is that colour, not glowing; on a row with redEyes or a red-eyed kind the same. A glowing eye on a bionicEye row is its lens (1). The other 8 (lapis 2, waster 2, strigoi, absol, goodra, vaporeon) stay as asked. |
| a6 | Odd eyes on lost-eye rows | 3 of the 5 odd-eye answers | marks `lostEye`, `eyePatch` | The eye-marks line said none on all 7 lost-eye and eye-patch rows it missed; on 3 the eyes line says "one blue, one dark", "one yellow", "one white one black". An eyes answer with "one ..., one ..." or "one dark/closed/white" counts as the lost eye seen: no remove:lostEye. |
| a7 | The conjoined twins' "two heads" | 1 | look `conjoined` | People said 1 and unusual "two heads", so the check writes remove:conjoined (applied: the twins' picture would go to anyone). "Two heads", "second head", "conjoined" in unusual count as two people for a twins look. |
| a8 | Held things for looks and roles | 3 + 1 + 1 + 1 | role `crafter`, looks `drinker`, `stoner`, `gourmand`, role `farmer`, `miner` | Add magnifying glass, magnifier, loupe to crafter; grass, hay, straw, sheaf to farmer; flashlight, lantern, headlamp to miner. A small table of held things for looks, marked ask: mug, tankard, ale, beer, goblet to drinker; cigarette, smoke, pipe, joint to stoner (the one cigarette here was a misread blood ribbon, so never yes); bread or food to gourmand (the model missed the gourmand's bread). Where the row already has the look or role, nothing. |
| a9 | A kind's own body named in unusual | about 60 | looks `fangs`, `fungalGrowth`, `foliageHair`, `faceTentacles`, `antennae`; features `longEars`, `snout` | Nothing to do for these rows. For a future row of a kind without it: fangs, mushrooms or fungal growth, leaves or plants on the head, tentacles or a long tongue would give out (ask), as a body its kind doesn't have; "anthropomorphic" or an animal head on a kind that is not far from human (baseliners, hussars) is the wrong kind, out. None in this scan. |
| a10 | Headwear words that aren't headwear | 2 | headwear | "Dragon head" (goodra's own head) is not headwear; "scarf" only with head, over the head or headscarf (the prompt had it round the neck). The 8 hoods, asked by no prompt, rightly give headwear. |
| a11 | Gestures read as held things | 15 | Held's empty words | Add fist, fists, waving, wave, thumbs up, touching hair, holding head, belt, pockets to the words for empty hands, so a work role showing none of its work is still noticed. |

### b. New looks or marks the game can read

The scan gives little evidence for new looks: each candidate was seen once.

| # | What | Pictures | What the game would read | Note |
|---|---|---|---|---|
| b1 | A face or body tattoo | 1 (no prompt asked one) | `p.style.FaceTattoo` other than NoTattoo_Face: Ideology's Face_Cobweb, Face_Eclipse, Face_Headshot, Face_Line, Face_Love, Face_Oracle, Face_Pantheon, Face_Spider, Face_Stars, Face_Tear, Face_Tiger, Face_Warpaint, Face_Worried, and the Elves' DevDesigner_WoodElves_TattooFaceA/B/C; body: `p.style.BodyTattoo`, Ideology's 16 Body_* and DevDesigner_WoodElves_TattooBodyA/B/C (all active in docs/player-mods) | Already proposed as the mark `tattoo` in marks.csv; 53 of 405 pawns in the player's save (13%) have a face tattoo, the most common mark the game doesn't read yet. Needs a form line to be checked (the free line found one). |
| b2 | Pregnancy | 1 | hediff PregnantHuman (Biotech) | It came from a prompt asking "a big belly", so it was a misread. Only worth a look if the player wants pregnancy pictures. |
| b3 | Freckles | 1 of 13 freckled prompts | Nice Facial Animation's skin type SkinFreckles (andromeda.facialanimation, active), read softly through that mod by name | No vanilla def. VRE_FacialSpots (Archon) is another kind of spots. Low value. |

No heterochromia gene exists in the player's active mods, and no fangs gene for Biotech's sanguophages (their kind is the gate).

### c. Notes only

| What | Pictures | Why |
|---|---|---|
| A kind's own body restated (animal head, pig features, horns, wings, pointed or animal ears, reptilian, colours, crystal, slime, wrinkles) | about 60 | The kind decides already. |
| Jewellery (gold necklace, watch, rings), fur collar or vest | 3 | Prompt flavour; no look. A RimWorld of Magic's rings and amulets are magic gear, not something a portrait should gate on. |
| A small or wooden object in the hands | 4 | Can't be told. |
| Metal mouth (bruxa) | 1 | The prompt asked fangs. Look at the picture: if it shows a metal jaw, the look `bionicJaw` (BionicJaw, VREA_ArtificialJaw) fits; else nothing. |
| A bare "yes" in unusual | 3 | Says nothing (fairies; their wings). |
| The pixie's "glowing light" | 1 | A fairy's glow; the check would write `looks add:` with no word. |
| Picture defects | 0 | No answer named text, a signature or watermark, a frame or border, a second face or figure, extra fingers or a cut. The one picture with two people is a conjoined row. The free line doesn't find defects; only a line asking for one does (limbs now; text or a frame only if the player has seen them). |

### Lines worth adding to the form

The free answers miss what the game already reads, so the form could ask it:
- **face marks**: "any scar, burn mark, tattoo, war paint or missing ear on the face, or none". The game reads scar, burn and
  missingEar now; the model named none of the 8 scar rows, 2 burn rows and 1 missing-ear row, nor any of the 18
  scars the prompts asked. It would also give b1 something to check.
- **eye marks**: add "an eye closed over by a scar" to the choices. The model named 0 of the 6 lost-eye rows (7 with the
  mind devourer's patch read as "one black eye"), so the check writes remove:lostEye for all 7, and remove:bionicEye
  for 2 of the 3 bionic-eye rows.

## 3. Core answers against the row names

- **Sex**: 50 of the 621 pictures past toddler (8%) differ, nearly all one way: 38 women called men, 12 men called women.
  Old women worst: 14 of 33 old and 7 of 20 elder women called men, no old or elder man called a woman. Teen boys: 5 of
  22 called women. Animal-faced kinds are mostly called men (gorilla, wolfman, flareon, glaceon, orangutan, chimp).
  Each is a sex correction marked ask, which is now applied: the check's own rule ("the sex is never changed without
  asking") no longer holds. Worth not writing sex lines from this model for old and elder women (21), teens (5) and far-from-human
  kinds (8), or marking them no; 16 others remain.
- **Age**: the model never answers "teen" (35 teen pictures: 30 young adult, 5 adult) and only once "old" (73 old
  pictures: 52 elderly). 56 pictures are two or more bands from the row (ask, applied) and 9 three or more (yes): 4
  young wasters with the white hair their row asks for, called elderly; a grey-haired adult waster; a wolfman, a gorilla,
  a leafeon and a blue saurid (animal faces). Worth leaving the age alone for far-from-human kinds and where the row
  asks white or grey hair below middle age; that covers all 9.
- **Features**: the ears, horns, glow, scales and fur comparisons give an out line to about 100 pictures (97 rows,
  up to 106 pictures, about 16% of the scan), most of them the misreadings in a3 to a5; with ask applied, they leave the game. Also 5 drakonori and
  nereid pictures with scales and 6 lycan, yttakin, goodra and espeon pictures with fur were seen without.
- **Arms**: all 624 "normal"; no row in the scan has an artificial arm, so nothing to judge.

## Recommended first

1. Stop the feature misreadings that now drop pictures (a3, a4, a5): ear fins and big ears are not pointed ears,
   antennae, tusks, fins and crests are not horns, a named eye colour or a bionic lens is not glowing. About 60 pictures
   for certain, and 32 more once the kinds' ears are settled in kinds.csv.
2. Keep the eye marks the model can't see: no remove:lostEye or remove:bionicEye from a plain "none", and odd eyes count
   as the lost eye (a6). 9 pictures.
3. Hold back sex and age corrections where this model is plainly weak: old and elder women, teens, far-from-human kinds,
   white or grey hair asked below middle age (section 3). 34 of the 50 sex lines and all 9 age lines changed without asking.
4. The twins' "two heads" (a7): 1 picture, but it would give a conjoined picture to anyone.
5. Work tools are not weapons, work clothes are not armour (a1, a2): 11 false fighter roles.
6. A face-marks line in the form (scar, burn, tattoo, missing ear), then the tattoo mark from Ideology's TattooDefs (b1).
7. Held-thing words for existing roles and looks (a8), and gestures as empty hands (a11).
