# Weapons you can judge at a glance: a rating, a review of every weapon, and a clearer card

The player (9 November 2026): "something that I struggle with is weapons, or rather judging how good a weapon
actually is. When I look at a weapon, kind of the details page, there's a lot of different values, and I have a very
hard time grasping, how does this end up in reality in a simple way? Which weapon is better than which, and which one
should I choose? ... both for melee and for ranged weapons. So I'd like you to do a deep review of each weapon in the
game, vanilla, all my mods ... some sort of a tier list in a sense, or a rating ... not just like 4 or 5 categories, but
like a real rating, maybe a number like 5.6 or whatever, 1 to 10 ... all the values added up, so to say, or averaging
out, or simulated how much damage it would do ... Also consider all the special effects that each weapon has and keep
that in mind and note that down. Maybe you can, redo the whole details tab for weapons. So I could easier, better see
what kind of a weapon does, how good is it, then also maybe ways that help me select the best weapon for each pawn,
maybe recommended weapons on the pawn inventory page, something like that perhaps, or that the pawns sometimes propose.
Yeah, I don't know, okay, never mind about that. Keep also, of course, all the weapons added through mods in mind, but
also my weapon mods themselves, like the inventory mod I'm using, the sidearms mod, the offhand mod, so that they are
all integrated and compatible with what you're creating."

## What the player runs (docs/player-mods, active rows)

The game's own combat (no Combat Extended). 768 active weapon defs: the game and its DLCs, the Vanilla Weapons
Expanded family (core, Quickdraw, Laser, Heavy Weapons, Coilguns, Frontier, Grenades, Non-Lethal, Bioferrite,
Makeshift, Tribal), Vanilla Persona Weapons Expanded, Melee Weapons For All Post-Medieval Tech Levels, Rimsenal's packs,
the Vanilla Factions Expanded weapons, A RimWorld of Magic, Alpha Mechs and Alpha Genes, Elves, Possessed Weapons,
Unique Weapons Unbound and more. The weapon mods to fit with: **Simple Sidearms** (`petetimessix.simplesidearms`),
**Dual Wield - Continued** (`memegoddess.dualwield`, the offhand), **Nice Inventory Tab** (`andromeda.niceinventorytab`,
the gear tab), **RunAndGun - Continued** (`memegoddess.runandgun`), **Melee Animation** (`co.uk.epicguru.meleeanimation`).

## Step 1: the rating and the review of every weapon

**Built (9 November 2026, not released).** `Source/ColonyCouncil/Weapons/WeaponRateMath.cs` (the rating, pure math) and
`WeaponTagMath.cs` (the colour-coded tags), tested in `Tools/Tests/WeaponRateTests.cs` against the base game's weapons;
`Tools/weapon_extract.py` reads every active weapon's numbers (`docs/player-mods/weapon_numbers.json.gz`) and
`Tools/WeaponReview` rates them all into `docs/weapons/README.md` (how it works, the anchors, the tag legend, every table)
and `docs/weapons/weapons.json` (everything, for the tier-list page and step 2). The mods' own code was read for the
effects that live there (Vanilla Expanded Framework, A RimWorld of Magic, VFE Medieval 2, VWE Bioferrite and the rest);
what still can't be valued is named and marked rough. Folded in as asked on the way: abilities and passive effects count
in the rating, situational scores and "best for / weak when" for step 2's recommendations, the player's tags request
("some kind of tags/labels that help me grasp their strengths and weaknesses ... color coded").

1. **One rating from 1.0 to 10.0**, with a decimal, for every weapon, from what it really does in a fight by the game's
   own rules: damage per second after the chance to hit (the weapon's accuracy at each distance and a shooter's or
   fighter's skill), armour (the game's armour roll against the weapon's penetration), warm-up, burst and cooldown, for
   ranged weapons at close, medium and long distance, for melee weapons by the game's choice among its tools.
2. **What it is good at, in a few sub-ratings**: close, medium and long range (ranged), against no armour, armour and
   heavy armour or mechanoids, and its reach. The overall rating weighs them by what the weapon is for, so a sniper
   rifle isn't marked down for being poor at five cells.
3. **Special effects named and counted**: fire, EMP, stun, toxic, explosions and their size, one use only, stopping power,
   extra damage, beams, slowing the wielder, biocoding and personas, unique traits.
4. **A review of all 768**, by kind, tech level and mod, with each weapon's rating, sub-ratings, a line in plain words
   ("deadly within ten cells, falls off past twenty; shrugged off by heavy armour") and its specials; a tier list page to
   look through, sort and filter.

## Step 2: in the game

**Built (9 November 2026, not released).** DESIGN.md "Weapons you can judge at a glance" has all of it. The review and
the game read a weapon by one file (`Source/ColonyCouncil/Weapons/WeaponReadMath.cs`: the review's `Inputs.cs` is now
only its JSON to facts, and regenerating the review gives `docs/weapons/` byte for byte); `Weapons/WeaponLive.cs` fills
the same facts from the live def and the weapon in hand, so a weapon at normal quality in steel rates in the game as in
`weapons.json` (tested for eight of the base game's weapons). Choices made on the way:

- The rating is kept by def, stuff and quality, and by the weapon itself only for unique and persona traits; a
  colonist's rating an hour. A def's card (the research tab, a trader's list) rates it in its default stuff at normal
  quality.
- The colonist's rating reads their real accuracy and melee hit chance back through the game's curves, the weapon in
  their hands taken out, and explains itself in steps that add up: their skill, each trait, their body and gear. Tough
  matters where a Vanilla Expanded heavy weapon needs it, so the card says what lets them carry one.
- The info card shows the tags as coloured words (a symbol's glyph may be missing from the game's font); Rimfolk's card
  draws each tag's mark as an icon.
- Dual Wield's pair is both hands' fight values added when both are guns or both blades; a gun and a blade fight their
  own fights and are shown apart.
- Melee Animation's lasso is apparel, not a weapon's, so the card names only what is per weapon: animated or not, its
  execution moves at the colonist's Melee, duels for a long weapon.
- The tags' labels and reasons and the line in plain words stay English from the math (one place, shared with the
  review); the card's own words are keyed.

5. **The weapon card, redone**: the rating and sub-ratings at the top of the game's info card (so it shows wherever the
   card opens, the gear tab and the inventory mod's included), and Rimfolk's own weapon card in Rimfolk's look: a chart
   of damage by distance, against each armour, the specials in words, how it compares with what the selected colonist
   holds, and the rating for that colonist's own skill.
6. **The exact weapon in hand**: its quality, material and unique or persona traits change its rating.
7. **The weapon mods**: Simple Sidearms (each carried sidearm rated, a sensible pair shown), Dual Wield (as an offhand,
   with its penalties), Nice Inventory Tab (the rating beside each weapon), RunAndGun (shooting on the move noted),
   Melee Animation (its moves noted), all soft: nothing breaks without them.

## Nuance (the player, the same day)

"it just should be nuanced, you know, the rating and the description and recommendations that they keep, not just the
blank values in mind, but also like special abilities, passive effects, situational use, etc." So:

- **Abilities a weapon grants count in its rating** (equippable abilities of Royalty, Vanilla Persona Weapons Expanded,
  Odyssey's unique weapons, A RimWorld of Magic's staffs), read from what the ability does; one whose effect lives in a
  mod's code is named and marked as not counted.
- **Passive effects count**, plus or minus, each named: what the weapon does to its wielder (move speed, dodge, aiming,
  hit chance, psychic sensitivity, pain, mood), persona and bladelink traits, biocoding, drawbacks.
- **Situational use**: each weapon's "best for" and "weak when" (mechanoids, crowds, heavy armour, a single strong
  enemy or animal, a choke point or wall, kiting, sieges, hunting where explosives ruin the meat, capturing alive,
  indoors where explosions and fire hit friends, a sidearm or an offhand), with situational sub-ratings where they make
  sense, weighed into the overall by what the weapon is for.
- **Plain lines as a seasoned player would say them** ("a fine sidearm; the EMP makes it the answer to mechanoids; too
  slow to stop a charge"), not a list of stats.

## Tags you can read at a glance

The player (9 November 2026): "what would be cool if all the weapons and great could have some kind of tags/labels that
help me grasp their strengths and weaknesses, special abilities etc. the lables could be like color coded, kind of like
for many of today's games. for example "great for snipers", "risk of fire" etc., you surely have ideas".

- **A fixed set of tags**, each a word or three with a line saying why, made from the weapon's own numbers by documented
  rules (never picked by hand), about three to seven a weapon, the most telling first.
- **Six kinds, each with its colour and its own mark** so colour is never the only cue: strengths (green: long reach,
  sniper's pick, shreds armour, mech killer, crowd breaker...), weaknesses (red: poor up close, slow to aim, bounces off
  armour, heavy to carry...), risks (orange: fire risk, hits friends, ruins the hunt, one use...), specials (purple: EMP,
  sets on fire, stuns, bleeds, grants an ability, persona bond, non-lethal...), roles (blue: sniper, assault, close
  quarters, sidearm, offhand, hunting, capture alive, duelist...) and needs (grey: needs a good shot, needs a strong arm,
  biocoded...).
- The same tags and colours in the review, the tier-list page, the weapon card in the game and, later, for armour and
  apparel.

## Step 3: recommendations (the player said yes)

**Built (9 November 2026, not released).** DESIGN.md "Step 3: the best weapon for each colonist, and colonists who ask"
has all of it. The choice is pure (`Source/ColonyCouncil/Weapons/WeaponPickMath.cs`, tested in
`Tools/Tests/WeaponPickTests.cs`); the game side is `WeaponPick.cs` (what the colony has, each colonist's own rating, the
orders), `WeaponPickUi.cs` (the gear tab, Nice Inventory Tab, the Defenses page), `WeaponAsks.cs` (point 9),
`WeaponSwapJob.cs` (a trade between two colonists), `WeaponPickBook.cs` (the raids met, "not now", the asks; the clock).
Choices made on the way:

- **The threshold** is 0.6 of a point (about a fifth more damage for a gun, a ninth for a blade); a colonist asks only
  for twice that. Another kind than the one in hand asks 0.5 more, a bonded persona weapon left unused 1.0 more, and its
  cost is said.
- **The nuance** is in the score: the colonist's own rating already counts the weapon's abilities, passives and
  specials (step 1), and their skill, traits and body (step 2); on top come their passion, the colony's fights (its
  situational sub-ratings, weighed by the raids met lately: mechanoids, armour, crowds, sieges, a gate held), their work
  (a hunter's, a doctor's or crafter's) and their faith. The reasons are drawn from the same parts, two at most, the
  strongest first, with what it costs.
- **The colony's record of raids** is Rimfolk's own (no system kept one with what came: faction memory notes only who,
  and leaves mechanoids out), the last 12, with the game's raid letters for what it lacks.
- **One assignment**: greedy by gain, then exchanges while the colony's total rises (pairs swapping, a better leftover, a
  weapon moved on so a second colonist can have one), then trades better for both, then sidearms and off hands. Near the
  brute-force best on random colonies (tested).
- **Defense posts**: the defense lines assign no posts to colonists, so a line drawn counts as a gate the colony holds
  rather than a post per colonist.
- **Simple Sidearms' button** uses its own job (`EquipSecondary`) and its own check (`CanPickupSidearmInstance`), both by
  name; a weapon on an outfit stand is offered only in hand (its job takes from the ground). Dual Wield's off hand by its
  own job (`DW_EquipOffhand`). A trade is a job of Rimfolk's own, walked out on the map.

8. **The best weapon for each colonist** on the gear tab (Nice Inventory Tab's too): from what the colony has, by the
   colonist's skills and passions and traits (Shooting or Melee, Brawler, Trigger-happy, Careful shooter, Tough, Nimble,
   Wimp), their role, what they already carry (Simple Sidearms' memory, Dual Wield's offhand) and the situations the
   colony meets most (mechanoids, tribal crowds, armoured raiders), with the reason in a line; one click equips it.
9. **Colonists sometimes ask** for a better weapon they've seen in the stockpile, one sentence in their own voice, rarely,
   never when the difference is small.

## Rimfolk's weapon card first (the player, 9 November 2026)

"can the rimworld weapon card be the default screen when opening stats/info of a weapon? since it's much better than
vanilla, which could be opened via some button or so"

**Built (not released).** "Open Rimfolk's weapon card first" (on): a new game info card about a weapon the card can show
opens as Rimfolk's weapon card instead, wherever it is opened; navigation inside the game's own card stays in it; the
weapon card's "All the game's stats" opens the game's card, whose "Rimfolk's weapon card" button goes back. A kind (the
research or architect tab, a list) is rated as made, normal quality in its usual stuff, and says so.
`Source/ColonyCouncil/Weapons/WeaponCardFirst.cs`; DESIGN.md has the rules.

## After: the same for armour and apparel

The player (9 November 2026): "And maybe after you can do the whole thing, the same thing for armor and apparel. But
that's for to do after." A rating, a review and a clearer card for every piece of armour and apparel, with its specials,
passive effects and situations, once the weapons are done.

## What the player sends for the code

The collected defs are XML only. The weapons, armour and gear that work through a mod's own code, and the mods Rimfolk
fits to, need those mods' DLLs: `Tools/Collect-ModCode.ps1` collects them (by Steam Workshop id, the folders 1.7 loads),
their settings (`Mod_*.xml`) and the game's own `Assembly-CSharp.dll`, into `RimfolkModCode.zip`.
