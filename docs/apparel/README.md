# Armour and apparel, rated

Every piece of armour and apparel in the player's active mods, put on a colonist by the game's own rules and rated from 1.0
to 10.0 for what it is for, with what it is good and weak at, its tags, the situations it suits and a line in plain words.
Built by `Tools/ApparelReview` from the numbers `Tools/apparel_extract.py` reads out of the player's own def files (the game,
its five DLCs and every active mod, as RimWorld 1.7 loads them for the player's mod list, `MayRequire` and `MayRequireAnyOf`
honoured as the game honours them), with the same arithmetic the mod will use in the game
(`Source/ColonyCouncil/Apparel/ApparelRateMath.cs` and `ApparelTagMath.cs`). It is the apparel side of the weapon review
(`docs/weapons/README.md`): the same armour roll, the same scale's shape, the same six tag kinds and colours. The design and
the player's request are in `docs/design/apparel.md`; `apparel.json` beside this file holds everything below for a tier-list
page and for recommending apparel later.

**512 apparel defs**: 509 rated, 3 left out (worn only by what they were made for:
they vanish when taken off; listed at the end). 63 of the rated ones are marked *(rough)*: part of what they do is a
mod's own code the review can name but not value (see "Where the numbers are less sure").

## How to read a rating

- **The rating** says how much a piece does for whoever wears it, against the other pieces of its kind: body armour against
  body armour, helmets against helmets, a parka against cold-weather gear. 5.5 is the middle of each kind; near it, a point up
  is a good deal more of what the kind is for, and the scale flattens toward both ends, so a mod's super-armour lands at
  9-something rather than every strong piece sitting at 10. A piece that costs more than it gives is 1.0.
- **What it is for** (its role) decides what counts: armour is judged mostly on the fight, a parka on the cold, a hat on
  warmth, shade, looks and work, a belt on whatever it does. The roles and how they weigh each area are below.
- **Rated alone**, on a colonist wearing nothing else, at normal quality, in its usual stuff: fabric in devilstrand, metal in
  steel, leather in plain leather, wood, stone in granite (the tables name it). Stuff matters far more for apparel than for
  weapons, so each stuffed piece is also rated in the common stuffs of its kind (cloth, synthread, devilstrand and hyperweave;
  plain leather, bearskin and thrumbofur; steel, plasteel and uranium), shown in the tables and `apparel.json`.
- **Sub-ratings** are on scales of their own, the same for every piece: against guns, melee (blades, fists and animals),
  fire and mechanoids, how much longer the wearer lasts; cold and heat, the degrees of insulation; then toxins, vacuum,
  psychic (for a psycaster), psychic shelter, work, social and trade, mechanitor, magic, hunting and carrying where a piece
  does any of it. A head piece's protection and insulation are rated against head pieces: a marine helmet's 6.1 against
  guns means it does for the head what a body piece of 6.1 does for the body.
- **Two wearers.** Every piece is rated for a shooter and for a melee fighter (the tables' "Shooter / melee" column,
  `ratingShooter` and `ratingMelee` in `apparel.json`). A piece that clearly suits one of them is rated for that one and
  tagged "For shooters" or "For melee fighters"; the others are rated for a mix of the two (below, "Who it is for").
- **Situations**: for a shooter, for a melee fighter, in the cold and in the heat (the overall with that climate's
  insulation counting in full and the other's not at all), while working, for social or trade, and the sub-ratings.
- *(rough)* after a rating: part of what the piece does is a mod's own code the review can't read, so it is rated on what it
  can read and the rest is named. Trust those numbers less.

## How the rating works

### Protection: the game's armour roll on the human body

- **Where hits land.** The game picks the part a hit lands on by its own coverage (its share of its parent, less its
  children's), from the human body (`Bodies_Humanlike`): a bullet or an arrow can land on any part, the heart and the brain
  among them, since it sets no depth; a melee blow or a blast lands on an outside part; a blunt blow never on an eye. A
  piece covers a part when one of its body part groups is the part's. A hit on the head counts double: the brain (10 hit
  points) and the eyes are there, and one hit can kill or blind.
- **The armour roll** (`ArmorUtility`): each layer that covers the part, outside in, rolls its armour less the attack's
  piercing: under half of it the hit is deflected, under all of it halved, from twice it nothing gets through. A sharp hit
  halved goes on as blunt, so the next layer meets it with its blunt armour. Armour is held at 200% as the game holds it.
  Sharp, blunt and heat each meet their own armour.
- **How much longer the wearer lasts** (toughness): the damage a bare colonist takes over the damage they take with the
  piece on, against each family of enemies; the families pooled by the damage they deal and how often a colony meets them
  after its first year: guns 45%, melee 15%, animals 15%, mechanoids 20%, fire 5%.

The reference enemies' attacks, from their defs (the guns' projectiles, the melee weapons in steel as the weapon review has
them, the animals' and mechanoids' tools by the game's own choice of attack, its best three times in four):

| Family | Attack | Damage | Piercing | Kind | Lands | Share of the family |
|---|---|---|---|---|---|---|
| guns | autopistol | 10 | 15% | sharp | anywhere | 10% |
| guns | revolver | 12 | 18% | sharp | anywhere | 5% |
| guns | machine pistol | 6 | 9% | sharp | anywhere | 7% |
| guns | heavy SMG | 12 | 18% | sharp | anywhere | 8% |
| guns | pump shotgun | 18 | 14% | sharp | anywhere | 8% |
| guns | bolt-action rifle | 18 | 27% | sharp | anywhere | 10% |
| guns | assault rifle | 11 | 16% | sharp | anywhere | 20% |
| guns | LMG | 12 | 18% | sharp | anywhere | 10% |
| guns | sniper rifle | 25 | 38% | sharp | anywhere | 4% |
| guns | charge rifle | 16 | 35% | sharp | anywhere | 6% |
| guns | recurve bow | 14 | 21% | sharp | anywhere | 8% |
| guns | frag grenade blast | 27.4 | 5% | sharp | outside | 4% |
| melee | fists | 8.2 | 12% | blunt | outside | 15% |
| melee | knife | 12 | 18% | sharp | outside | 15% |
| melee | ikwa | 15 | 22% | sharp | outside | 10% |
| melee | spear | 23 | 50% | sharp | outside | 15% |
| melee | longsword | 23 | 34% | sharp | outside | 20% |
| melee | club | 14 | 21% | blunt | outside | 10% |
| melee | mace | 15.7 | 24% | blunt | outside | 15% |
| animals | timber wolf's claw | 10.9 | 16% | sharp | outside | 3% |
| animals | timber wolf's claw | 10.9 | 16% | sharp | outside | 3% |
| animals | timber wolf's bite | 12 | 18% | sharp | outside | 19% |
| animals | warg's claw | 13 | 20% | sharp | outside | 6% |
| animals | warg's claw | 13 | 20% | sharp | outside | 6% |
| animals | warg's razorfangs | 15 | 22% | sharp | outside | 4% |
| animals | grizzly bear's claw | 17 | 26% | sharp | outside | 6% |
| animals | grizzly bear's claw | 17 | 26% | sharp | outside | 6% |
| animals | grizzly bear's bite | 23.6 | 35% | sharp | outside | 4% |
| animals | cougar's claw | 11 | 16% | sharp | outside | 6% |
| animals | cougar's claw | 11 | 16% | sharp | outside | 6% |
| animals | cougar's bite | 16 | 24% | sharp | outside | 4% |
| animals | megaspider's head claw | 12 | 18% | sharp | outside | 15% |
| animals | thrumbo's horn | 23 | 34% | sharp | outside | 4% |
| animals | thrumbo's bite | 28 | 42% | sharp | outside | 0% |
| animals | thrumbo's foot | 19 | 28% | blunt | outside | 0% |
| animals | thrumbo's foot | 19 | 28% | blunt | outside | 0% |
| animals | muffalo's head | 13 | 20% | blunt | outside | 2% |
| animals | muffalo's hoof | 10 | 15% | blunt | outside | 2% |
| animals | muffalo's hoof | 10 | 15% | blunt | outside | 2% |
| animals | muffalo's bite | 10 | 15% | sharp | outside | 2% |
| mechanoids | lancer's charge lance | 30 | 45% | sharp | anywhere | 25% |
| mechanoids | scyther's blades | 20 | 30% | sharp | outside | 20% |
| mechanoids | centipede's heavy charge blaster | 15 | 22% | sharp | anywhere | 15% |
| mechanoids | centipede's inferno cannon | 10 | 0% | heat | outside | 5% |
| mechanoids | pikeman's needle gun | 15 | 35% | sharp | anywhere | 15% |
| mechanoids | militor's mini-shotgun | 10 | 12% | sharp | anywhere | 15% |
| mechanoids | termite's thump cannon | 9 | 14% | sharp | outside | 5% |
| fire | molotov or incendiary burst | 10 | 0% | heat | outside | 50% |
| fire | burning | 5 | 0% | heat | outside | 50% |

Where hits land, by the body's zones (a zone is the parts that share their body part groups):

| Body part groups | Shots | Blows and blasts | Blunt blows |
|---|---|---|---|
| Torso | 0.4266 | 0.2421 | 0.2454 |
| Legs | 0.2520 | 0.3163 | 0.3207 |
| Arms | 0.1589 | 0.1968 | 0.1995 |
| Shoulders | 0.0336 | 0.0542 | 0.0550 |
| Feet | 0.0280 | 0.0452 | 0.0458 |
| FullHead, HeadAttackTool, UpperHead | 0.0174 | 0.0281 | 0.0285 |
| Hands | 0.0166 | 0.0267 | 0.0271 |
| Neck | 0.0150 | 0.0242 | 0.0245 |
| Eyes, FullHead, UpperHead | 0.0108 | 0.0000 | 0.0000 |
| FullHead, Mouth, Teeth | 0.0090 | 0.0145 | 0.0147 |
| Eyes, FullHead | 0.0084 | 0.0136 | 0.0000 |
| FullHead, UpperHead | 0.0084 | 0.0136 | 0.0137 |
| FullHead | 0.0060 | 0.0097 | 0.0098 |
| Hands, LeftHand | 0.0036 | 0.0059 | 0.0059 |
| Hands, RightHand | 0.0036 | 0.0059 | 0.0059 |
| Hands, LeftHand, MiddleFingers | 0.0010 | 0.0017 | 0.0017 |
| Hands, MiddleFingers, RightHand | 0.0010 | 0.0017 | 0.0017 |

### The fight: toughness, output and feet

A piece's worth in a fight is how much longer its wearer lasts, times how much more they deal, times their feet:

- **Output.** The weapon review's reference fighter (`WeaponRateMath`): a shooter with an assault rifle (Shooting 8, hits
  96 % a cell) and a melee fighter with a steel longsword (Melee 8, 74 % melee hit). Shooting accuracy and melee hit a piece
  changes are skill levels, as the game adds them before its curve; aiming time and cooldown go into the rifle's cycle;
  melee damage into the sword's blows; sight and manipulation a piece's hediffs change scale both.
- **Feet.** A slower or quicker walk counts half its share for a shooter and all of it for a melee fighter, as in the
  weapon review (a colonist walks 4.6 cells a second).
- **Toughness besides armour.** Melee dodge (in skill levels, the game's curve), damage taken, and pain: a higher pain
  shock threshold keeps the wearer up longer, counted at half (pain is about half of how a colonist falls).
- **Shields.** A shield takes shots and blasts (the game's `CompShield` takes ranged and explosive damage): from full, its
  capacity, then its recharge each second, until fire of 6 damage a second for 30 seconds breaks it, and it is down for its reset;
  at most 85% of the fight's shots (an EMP shot drops it at once, a heavy volley overwhelms it). The shield belt takes
  33 damage and recharges 3.9 a second.
- **Powers** a piece's verbs give: a jump turns a melee fighter's 20-cell charge under fire into a one-second jump, twice a
  fight, and puts them to work sooner; smoke cuts shots by 30% (`ShotReport`'s smoke) for 20 seconds; a low-shield stops two
  shots in three for its 30 seconds (its wearer stays inside it most of the time); firefoam halves a fight's fire; grenades and
  launched shots are rated by the weapon review's own ability rule; a psychic lance takes one enemy out (counted as the
  60 damage it takes to down one, half again for berserk), an orbital strike three, a summoned turret or drone one, a tox
  pack half. A power with charges is used once a fight (a jump twice); one gone for good when its charges are spent counts
  at half. Powers add at most half again to the wearer's output, the weapon review's own cap.
- **Who it is for.** Every piece is rated twice, on its role's scale: for the shooter and for the melee fighter. It
  clearly suits one of them when it blocks its wearer's shots or is an off-hand shield (a melee fighter's), or when what it
  does for one (how much longer they last times how much more they deal) is a tenth more than for the other. The walk is
  left aside there: a slower walk costs every wearer, a melee fighter more, so plate armour isn't a shooter's for being
  heavy. A piece that clearly suits one is rated for that one (its overall is that wearer's rating) and tagged "For
  shooters" or "For melee fighters", with what it costs the other ("Spoils your aim", "Blocks shooting", "Clumsy in
  melee") among its first weaknesses. Any other piece suits both: three colonists in four fight with guns, so its
  overall weighs the shooter's fight three to one over the melee fighter's.

### The other areas: a share of what a colonist gets done

Everything else a piece does is a share of a colonist: ten degrees of insulation is a tenth (in the climate it is worn
for), and each stat a piece changes counts at a share per point:

| Stat | Area | Share per point |
|---|---|---|
| CarryingCapacity | carry | 0.003 |
| VEF_MassCarryCapacity | carry | 0.003 |
| HuntingStealth | hunting | 0.2 |
| MechBandwidth | mech | 0.083 |
| MechControlGroups | mech | 0.1 |
| WorkSpeedGlobalOffsetMech | mech | 0.3 |
| EatingSpeed | mind | 0.05 |
| GlobalLearningFactor | mind | 0.3 |
| ImmunityGainSpeed | mind | 0.2 |
| InjuryHealingFactor | mind | 0.2 |
| MentalBreakThreshold | mind | -1.5 |
| RestFallRateFactor | mind | -0.2 |
| RestRateMultiplier | mind | 0.2 |
| MeditationFocusGain | psychic | 0.66 |
| PsychicEntropyMax | psychic | 0.017 |
| PsychicEntropyRecoveryRate | psychic | 0.926 |
| NegotiationAbility | social | 0.4 |
| SocialImpact | social | 0.3 |
| SuppressionPower | social | 0.2 |
| TradePriceImprovement | social | 0.6 |
| ToxicEnvironmentResistance | toxic | 0.15 |
| ToxicResistance | toxic | 0.15 |
| VacuumResistance | vacuum | 0.3 |
| AnimalGatherSpeed | work | 0.1 |
| AnimalGatherYield | work | 0.1 |
| ButcheryFleshEfficiency | work | 0.2 |
| ButcheryFleshSpeed | work | 0.1 |
| CleaningSpeed | work | 0.1 |
| ConstructionSpeed | work | 0.33 |
| ConstructSuccessChance | work | 0.33 |
| CookSpeed | work | 0.33 |
| DrugCookingSpeed | work | 0.1 |
| DrugSynthesisSpeed | work | 0.1 |
| EntityStudyRate | work | 0.1 |
| FixBrokenDownBuildingSuccessChance | work | 0.1 |
| FoodPoisonChance | work | -0.5 |
| ForagedNutritionPerDay | work | 0.1 |
| GeneralLaborSpeed | work | 0.33 |
| MedicalOperationSpeed | work | 0.1 |
| MedicalPotency | work | 0.2 |
| MedicalSurgerySuccessChance | work | 0.33 |
| MedicalTendQuality | work | 0.33 |
| MedicalTendSpeed | work | 0.2 |
| MiningSpeed | work | 0.33 |
| PlantHarvestYield | work | 0.33 |
| PlantWorkSpeed | work | 0.33 |
| ResearchSpeed | work | 0.33 |
| SmoothingSpeed | work | 0.1 |
| WorkSpeedGlobal | work | 1 |

Psychic sensitivity counts in full for a psycaster; lower sensitivity shelters the mind from psychic drones and lances at
0.15 a point. A slower or quicker walk counts at half in work outside fights. A RimWorld of Magic's enchantments count at half
for a mage (more mana, regeneration, shorter cooldowns and cheaper spells in full, experience and arcane damage at half,
arcane resistance at a quarter). Work a piece bars costs 15%, learning nothing 30%.

### Roles

A piece's role is judged as made in plain stuff (cloth, plain leather, steel, wood, granite), so a duster is hot-weather
gear whatever it is made of, then rated in its usual stuff:

- an off-hand shield (Vanilla Expanded's); a belt, pack or accessory (the belt, hip and artifact layers) is utility;
- a psychic, magic or royal piece whose psychic, magic or royal worth outweighs its fight is ceremonial or psychic;
- on the head: combat headgear (a helmet) when its sharp armour is 40% or more, it is made as armour (its stuff's armour
  counting at 45% or more) or it makes its wearer a tenth better in a fight (a gunlink); else a hat, mask or hood;
- on the body: armour when its sharp armour is 40% or more, it is made as armour, or its protection alone lasts its wearer
  a fifth longer; else a vacuum or hazard suit when keeping out toxins and vacuum is worth more than its fight; else
  cold-weather (or hot-weather) gear when its cold (or heat) insulation is 14 °C or more, a third above the other and worth
  at least what the piece does for work, talks, the mind, carrying and hunting; else everyday clothing; pieces made only
  for children have their own table.

How much each area counts, by role, and the scale's middle (the worth that rates 5.5) and width:

| Role | fight | cold | heat | work | social | mind | psychic | mech | magic | toxic | vacuum | carry | hunting | Middle | Width |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| body armour | 1 | 0.15 | 0.15 | 0.5 | 0.3 | 0.5 | 0.3 | 0.5 | 0.3 | 0.2 | 0.15 | 0.3 | 0.3 | 0.42 | 0.85 |
| helmet | 1 | 0.15 | 0.15 | 0.5 | 0.3 | 0.5 | 0.3 | 0.5 | 0.3 | 0.2 | 0.15 | 0.3 | 0.3 | 0.04 | 1.1 |
| off-hand shield | 1 | 0.15 | 0.15 | 0.5 | 0.3 | 0.5 | 0.3 | 0.5 | 0.3 | 0.2 | 0.15 | 0.3 | 0.3 | 0.42 | 0.85 |
| utility | 1 | 0.5 | 0.5 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 0.2 | 0.75 |
| everyday clothing | 0.4 | 0.6 | 0.6 | 1 | 1 | 1 | 0.5 | 0.5 | 0.5 | 0.5 | 0.5 | 0.5 | 0.5 | 0.08 | 0.6 |
| headgear | 0.4 | 0.6 | 0.6 | 1 | 1 | 1 | 0.5 | 0.5 | 0.5 | 0.5 | 0.5 | 0.5 | 0.5 | 0.06 | 0.8 |
| cold-weather gear | 0.4 | 1 | 0.2 | 0.5 | 0.5 | 0.5 | 0.3 | 0.3 | 0.3 | 0.5 | 0.5 | 0.5 | 0.5 | 0.28 | 0.45 |
| hot-weather gear | 0.4 | 0.2 | 1 | 0.5 | 0.5 | 0.5 | 0.3 | 0.3 | 0.3 | 0.5 | 0.5 | 0.5 | 0.5 | 0.22 | 0.45 |
| hazard gear | 0.4 | 0.3 | 0.3 | 0.5 | 0.5 | 0.5 | 0.3 | 0.3 | 0.3 | 1 | 1 | 0.5 | 0.5 | 0.3 | 0.6 |
| ceremonial or psychic | 0.4 | 0.4 | 0.4 | 0.6 | 1 | 1 | 1 | 1 | 1 | 0.5 | 0.5 | 0.5 | 0.5 | 0.3 | 0.6 |

### What the mods' own code does, and what is counted

The player's mods' code (their DLLs, decompiled to read, never run) was read wherever a piece's effect lives there:

- **Vanilla Expanded Framework**: its off-hand shields (Vanilla Factions Expanded's, the Elves', the Demigryphs') roll their
  own armour on the groups the shield covers before any apparel, while the wearer holds a weapon under 1.65 kg that Dual
  Wield allows in the off hand (`ShieldUtility.UsableWithShields`), so they are rated for a fighter with a sword or a
  pistol; its shield bubble absorbs shots and blasts like a shield belt without stopping its wearer's own (its energy in
  damage, recharging its rate times its energy each second); its shield field covers its wearer and those beside them
  (0.033 energy a point of damage, like the game's shield); its apparel extension's stat factors, barred work and no
  learning count; camouflage counts as hunting stealth; its apparel hediffs count by their stages; quivers' and the ammo
  pack's ranged cooldown factor shortens the cooldown between shots (a quiver's for bows and other neolithic and medieval
  weapons only).
- **A RimWorld of Magic**: enchantments (mana, regeneration, cooldowns, spell cost, experience, arcane damage and
  resistance) count for a mage; its whole-body armour offsets (amulets) are a layer on every part; its alignment armour
  meets only its own Light and Dark damage and isn't counted; its arcane spectre and phantom shift, the orbs' abilities and
  the rings' actions are its own code, named.
- **Vanilla Apparel Expanded - Accessories**: the resurrector belt brings its wearer back once when they would die (counted
  as a second life, then gone); the explosive belt blows up when its wearer dies (named).
- **Field Medic**: the medic bag lets its wearer stop someone's bleeding in the field (counted as a little work).
- **Dubs Rimatomics**: its radiation suits' own class adds nothing: they protect by their toxic resistance.
- **Vanilla Gravship Expanded**: oxygen packs keep their wearer alive in vacuum while their oxygen lasts; the astrorig jumps
  on its oxygen; the gravship bombardment targeter aims the colony's gravship guns (counted as an orbital strike, rough).

Not read, so named and marked *(rough)*: Fairycore Apparel 2's quality-scaled bonuses (its extension isn't in the uploaded
DLL; read at normal quality), PokeWorld's balls (its code isn't in the upload), Vanilla Factions Expanded - Deserters' bomb
pack, invisibility engulfer and declassifier, Vanilla Factions Expanded - Empire's techfriar crown's research refresh,
Rimsenal Security's fortification pack, Vanilla Psycasts Expanded's psyring (its psycast is chosen when it is made), Melee
Animation's lassos, and a few others whose effect is a hediff of their own.

Not counted: quality (all at normal), wear, other pieces worn with it (each piece is rated alone; worn together, every
layer rolls, outside in), the colony's climate (the cold and heat ratings stand for it), a faith's or a title's rules (named:
which titles and roles a piece meets is in its tags and `apparel.json`), apparel colour moods, dead man's apparel and human
leather (no piece is rated in it; a colonist minds wearing it). Patches from other mods are not applied, as in the mod pool;
the ones that touch a piece's numbers are listed below.

## Tags

Every piece carries a few tags, given by rules on its own numbers (never by hand), each with a line saying why, the most
important first, at most seven, keeping its role, its first strength and its first weakness. The six kinds, their colours
and symbols are the weapon review's own (one table: `WeaponTagMath.Categories`), readable at 4.5:1 or better on Rimfolk's
window, panel and card grounds:

| Symbol | Category | Colour | What it says |
|---|---|---|---|
| ▲ | Strength | `#9db89f` | what it is good at |
| ▼ | Weakness | `#f27466` | what it is bad at |
| ⚠ | Risk | `#e08a45` | what can go wrong with it |
| ✦ | Special | `#b4a7e2` | what it does besides protect and insulate |
| ◆ | Role | `#6aa6dc` | what it is for, and who it suits |
| ● | Needs | `#a9b1ba` | what it asks of its wearer or the colony |

Every tag and its rule:

| Tag | Category | Rule |
|---|---|---|
| ▲ Stops bullets | Strength | rates 7 or more against guns |
| ▲ Turns blades | Strength | rates 7 or more against blades, fists and animals |
| ▲ Holds off mechs | Strength | rates 7 or more against mechanoids |
| ▲ Warm | Strength | cold insulation of 20 °C or more on a coat or armour, 10 °C next to the skin, 6 °C on the head |
| ▲ Fireproof | Strength | rates 7 or more against fire |
| ▲ Cool | Strength | heat insulation of 15 °C or more on a coat or armour, 10 °C next to the skin, 6 °C on the head |
| ▲ Full cover | Strength | armour over 80% or more of where shots land |
| ▲ Quick feet | Strength | speeds its wearer up |
| ▲ Steadier aim | Strength | better shooting accuracy or quicker aim (with both a gain and a cost, by what a shooter deals in the end) |
| ▲ Light | Strength | armour of 5 kg or less that doesn't slow its wearer |
| ▲ Melee edge | Strength | better melee hit, damage or dodge |
| ▲ Shrugs off pain | Strength | pain shock threshold up by 10% or more |
| ▲ Steady nerves | Strength | a lower mental break threshold |
| ▲ Carries more | Strength | raises what its wearer can carry |
| ▲ Heals faster | Strength | faster immunity or wound healing |
| ▲ Good impression | Strength | raises social impact or negotiation |
| ▲ Hard-wearing | Strength | armour with 300 hit points or more |
| ▼ Blinds | Weakness | takes its wearer's sight |
| ▼ Blocks shooting | Weakness | its shield stops its wearer's own shots |
| ▼ Slows you | Weakness | slows its wearer |
| ▼ Bars work | Weakness | its wearer can't do some work, or learns nothing |
| ▼ Spoils your aim | Weakness | lower shooting accuracy or slower aim (first among the weaknesses on a piece for melee fighters) |
| ▼ Thin cover | Weakness | armour over less than half of where shots land |
| ▼ Slows work | Weakness | lower work speed |
| ▼ Clumsy in melee | Weakness | lower melee hit or dodge (first among the weaknesses on a piece for shooters) |
| ▼ Weak to blunt | Weakness | armour whose blunt rating is under a third of its sharp: fists, clubs and paws get through |
| ▼ Mechs pierce it | Weakness | armour that rates 2 or more lower against mechanoids than against guns |
| ▼ Blocks psycasts | Weakness | lowers psychic sensitivity: bad for a psycaster |
| ▼ No warmth | Weakness | body armour with under 10 °C of cold insulation: useless in a blizzard |
| ▼ Off-putting | Weakness | lowers social impact or negotiation |
| ▼ Won't stop a bullet | Weakness | clothing over a fifth or more of where shots land that rates 2.5 or less against guns |
| ▼ Flammable | Weakness | burns readily: flammability of 100% or more and heat armour under 20% |
| ▼ Little warmth | Weakness | clothing, a helmet or other headgear with under 4 °C of insulation either way (5 °C on the head) |
| ⚠ Hurts its wearer | Risk | pain while worn |
| ⚠ Calls enemies | Risk | brings hostile mechanoids down where it is aimed |
| ⚠ Runs out | Risk | gone when its charges are used |
| ⚠ Angers factions | Risk | using it on someone costs goodwill with their faction |
| ⚠ Tattered mood | Risk | body armour over 30% or more of where shots land, under 200 hit points: a quarter of each hit's damage wears it, so a few fights tatter it |
| ⚠ Explodes on death | Risk | blows up when its wearer dies |
| ⚠ Wears fast | Risk | under 60 days of wear to half its hit points, when the tattered mood starts |
| ⚠ Counts as naked | Risk | covers the torso or legs but doesn't count as clothing |
| ⚠ Wrong-gender mood | Risk | made for one gender: the other is unhappy wearing it |
| ✦ Shield | Special | an energy shield that takes shots and blasts |
| ✦ Second life | Special | brings its wearer back from death once |
| ✦ Jump | Special | jumps over walls and enemies |
| ✦ Psychic lance | Special | shocks or maddens one enemy from afar |
| ✦ Orbital strike | Special | calls a strike or a tornado from orbit |
| ✦ Smoke | Special | pops a smoke cloud that spoils shots |
| ✦ Low shield | Special | deploys a low shield that stops shots from outside for 30 seconds |
| ✦ Grants: (ability) | Special | throws or launches something, named |
| ✦ Vacuum-proof | Special | vacuum resistance of 60% or more, or an oxygen supply |
| ✦ Mech bandwidth | Special | adds a mechanitor's bandwidth or control groups |
| ✦ Extra gun | Special | a gun of its own that fires beside its wearer |
| ✦ Shields allies | Special | a shield field or low-shield that covers those beside its wearer |
| ✦ Summons help | Special | deploys a turret, a drone or the risen dead |
| ✦ Psychic shield | Special | lowers psychic sensitivity: shelters from drones and lances |
| ✦ Enchanted | Special | an enchantment of A RimWorld of Magic |
| ✦ Firefoam | Special | puts out fires around its wearer |
| ✦ Psychic | Special | raises psychic sensitivity, psychic heat or focus |
| ✦ Toxin-proof | Special | toxic resistance of 50% or more, or immune to tox gas |
| ✦ Camouflage | Special | hides its wearer while still |
| ✦ Wards the body | Special | adds armour to its wearer's whole body |
| ✦ Stops bleeding | Special | its wearer bleeds less or not at all |
| ✦ Its own effect | Special | part of what it does is its mod's own code: named, not counted |
| ◆ Combat armour | Role | body armour: sharp armour of 40% or more as made, or a fifth longer in a fight |
| ◆ Helmet | Role | on the head, with armour that matters (sharp 40% or more, or a tenth longer in a fight) |
| ◆ Headgear | Role | on the head, for warmth, shade, looks or work |
| ◆ Off-hand shield | Role | carried in the off hand (Vanilla Expanded's shields) |
| ◆ Utility | Role | a belt, pack or accessory |
| ◆ Everyday wear | Role | clothing for every day: no real armour, no strong insulation one way |
| ◆ Cold-weather | Role | its cold insulation in plain stuff is 14 °C or more and a third above its heat insulation |
| ◆ Hot-weather | Role | its heat insulation in plain stuff is 14 °C or more and a third above its cold insulation |
| ◆ Hazard gear | Role | a body piece whose toxic and vacuum worth is 5% or more and above its fight worth |
| ◆ Ceremonial | Role | worn for a title, a faith or its looks more than for a fight |
| ◆ Psycaster's | Role | its psychic or magic worth outweighs its fight |
| ◆ For melee fighters | Role | rated for a melee fighter: it blocks shooting, is an off-hand shield, or does a tenth more for a melee fighter than for a shooter (how much longer they last times how much more they deal, the walk aside) |
| ◆ For shooters | Role | rated for a shooter: it does a tenth more for a shooter than for a melee fighter (how much longer they last times how much more they deal, the walk aside) |
| ◆ Made as armour | Role | not rated as body armour or a helmet, but made as armour (its stuff's armour counts at 40% or more): named for what it covers, armour for the hands, the feet or the head, an armoured cloak |
| ◆ Royal | Role | meets a royal title's apparel rule |
| ◆ Work clothes | Role | makes its wearer's work 5% more or better |
| ◆ Trader's | Role | makes its wearer's talks and trade 5% better or more |
| ◆ Spacefarer's | Role | vacuum resistance of 30% or more, or an oxygen supply |
| ◆ Builder's | Role | better construction, smoothing or repairs |
| ◆ Researcher's | Role | faster research or entity study |
| ◆ Cook's | Role | faster cooking or butchering, less food poisoning |
| ◆ Grower's | Role | faster plant work or a bigger harvest |
| ◆ Handler's | Role | faster or bigger animal gathering |
| ◆ Miner's | Role | faster mining |
| ◆ Faith's apparel | Role | a faith's role or meme asks for it |
| ◆ Medic's | Role | better tending or surgery |
| ◆ Hunter's | Role | hunting stealth 20% or more, or camouflage |
| ◆ For slaves | Role | slave apparel, or raises a slave's suppression |
| ◆ Coat | Role | an outer layer over the torso |
| ◆ Top | Role | covers the torso, not the legs |
| ◆ Legwear | Role | covers the legs, not the torso |
| ◆ Full outfit | Role | covers the torso and the legs |
| ◆ Footwear | Role | covers the feet |
| ◆ Handwear | Role | covers the hands |
| ◆ Cloak | Role | an outer layer over the neck and shoulders only |
| ◆ Hat | Role | worn on the upper head |
| ◆ Mask or hood | Role | covers the face, the mouth or the whole head |
| ◆ Eyewear | Role | worn over the eyes |
| ◆ Worn for looks | Role | it does little else: worth under 2% of a colonist in every area, and not made as armour |
| ● Hard to find | Needs | no recipe in the player's game makes it and no faction's people wear it |
| ● Children only | Needs | made for children |
| ● Mechanitor only | Needs | its bandwidth needs a mechanitor |
| ● Needs a mage | Needs | its enchantment needs a mage or a might user |
| ● Light weapon only | Needs | works only with a weapon under 1.65 kg that Dual Wield allows in the off hand |
| ● Needs a psylink | Needs | its psychic gains need a psycaster |
| ● Women only | Needs | only one gender can wear it (or is meant to) |
| ● Needs a title | Needs | royal apparel: worth most to someone whose title asks for it |
| ● Needs (fuel) | Needs | its charges are refilled with something the colony must make or buy, named |
| ● Stuff matters | Needs | its rating moves by 1.5 or more between the common stuffs it can be made of |
| ● Advanced research | Needs | made only after spacer or ultra research |
| ● Biocodable | Needs | can be biocoded to one colonist |
| ● Belt slot | Needs | takes a utility slot (a belt, an artifact, a hip): one piece in it at a time |

## The anchors

Normal quality, in the stuff named, the reference colonist. These are checked in `Tools/Tests/ApparelRateTests.cs`, so the
scale can't drift: body armour from the flak pieces through plate, recon and marine to cataphract armour, helmets from the
simple helmet to the cataphract helmet, the shield belt above the jump pack among utility, the parka and tribalwear low as
armour and strong for cold. Three more check the two wearers and what clothing made as armour is called: the gunlink
(a shooter's), VFE Medieval 2's heavy plate helmet (a melee fighter's) and Vanilla Armour Expanded's plate gloves.

| Piece | Role | Rated for | Rating | Shooter / melee | Against guns / melee / fire / mechanoids | Cold / heat | In the cold / in the heat |
|---|---|---|---|---|---|---|---|
| flak vest | body armour | anyone | **3.9** | 3.9 / 3.8 | 3.2 / 2.0 / 1.4 / 2.6 | 1.6 / 1.0 | 4.0 / 3.9 |
| flak pants | body armour | anyone | **2.1** | 2.1 / 1.9 | 1.5 / 1.4 / 1.2 / 1.3 | 2.4 / 1.7 | 2.6 / 2.2 |
| flak jacket | body armour | anyone | **3.2** | 3.3 / 3.1 | 2.3 / 1.7 / 1.3 / 1.7 | 4.8 / 2.5 | 4.4 / 3.3 |
| plate armor | body armour | anyone | **4.5** | 4.7 / 3.7 | 5.0 / 3.5 / 4.0 / 3.7 | 2.3 / 1.0 | 4.7 / 4.5 |
| recon armor | body armour | anyone | **6.8** | 6.8 / 6.8 | 6.1 / 4.1 / 3.5 / 4.6 | 8.2 / 4.3 | 7.8 / 6.8 |
| marine armor | body armour | anyone | **7.1** | 7.2 / 7.1 | 7.1 / 5.2 / 4.0 / 5.9 | 8.3 / 4.5 | 7.9 / 7.1 |
| cataphract armor | body armour | anyone | **7.5** | 7.5 / 7.3 | 7.8 / 6.2 / 4.5 / 7.0 | 8.3 / 5.0 | 8.1 / 7.5 |
| simple helmet | helmet | anyone | **3.5** | 3.5 / 3.5 | 2.0 / 1.7 / 2.3 / 1.5 | 1.5 / 1.0 | 3.9 / 3.4 |
| flak helmet | helmet | anyone | **4.3** | 4.3 / 4.3 | 2.6 / 2.3 / 2.8 / 2.2 | 1.5 / 1.0 | 4.6 / 4.3 |
| recon helmet | helmet | anyone | **7.1** | 7.1 / 7.1 | 5.3 / 5.2 / 4.5 / 4.7 | 4.2 / 2.8 | 7.4 / 7.2 |
| marine helmet | helmet | anyone | **7.2** | 7.2 / 7.2 | 6.1 / 6.0 / 5.1 / 5.5 | 4.2 / 2.8 | 7.6 / 7.4 |
| cataphract helmet | helmet | anyone | **7.4** | 7.4 / 7.4 | 6.5 / 6.6 / 5.6 / 6.2 | 4.2 / 2.8 | 7.7 / 7.5 |
| shield belt | utility | a melee fighter | **7.6** | 1.0 / 7.6 | 7.2 / 1.0 / 4.7 / 5.6 | 1.0 / 1.0 | 7.6 / 7.6 |
| jump pack | utility | a melee fighter | **7.2** | 4.3 / 7.2 | 2.8 / 1.0 / 1.0 / 2.4 | 1.0 / 1.0 | 7.2 / 7.2 |
| smokepop pack | utility | anyone | **3.5** | 3.3 / 4.3 | 2.6 / 1.0 / 1.0 / 2.2 | 1.0 / 1.0 | 3.5 / 3.5 |
| firefoam pop pack | utility | anyone | **1.7** | 1.7 / 1.7 | 1.0 / 1.0 / 6.7 / 1.0 | 1.0 / 1.0 | 1.7 / 1.7 |
| low-shield pack | utility | anyone | **5.9** | 5.9 / 5.9 | 4.0 / 1.0 / 1.0 / 3.2 | 1.0 / 1.0 | 5.9 / 5.9 |
| parka | cold-weather gear | anyone | **7.1** | 7.1 / 7.1 | 1.3 / 1.1 / 2.9 / 1.1 | 7.5 / 1.0 | 8.6 / 1.1 |
| tribalwear | everyday clothing | anyone | **7.5** | 7.5 / 7.5 | 1.4 / 1.1 / 3.1 / 1.2 | 4.2 / 5.3 | 7.8 / 8.1 |
| duster | hot-weather gear | anyone | **6.5** | 6.5 / 6.5 | 2.2 / 1.6 / 7.0 / 1.6 | 4.4 / 6.6 | 4.9 / 8.0 |
| jacket | cold-weather gear | anyone | **4.2** | 4.2 / 4.2 | 1.8 / 1.4 / 4.2 / 1.4 | 5.1 / 3.8 | 6.5 / 2.7 |
| T-shirt | everyday clothing | anyone | **4.0** | 4.0 / 4.0 | 1.2 / 1.1 / 2.0 / 1.1 | 2.6 / 2.3 | 5.4 / 3.9 |
| button-down shirt | everyday clothing | anyone | **4.5** | 4.5 / 4.5 | 1.3 / 1.1 / 2.9 / 1.1 | 2.9 / 2.3 | 6.1 / 4.1 |
| pants | everyday clothing | anyone | **3.6** | 3.6 / 3.6 | 1.2 / 1.1 / 2.1 / 1.1 | 2.5 / 2.1 | 5.0 / 3.4 |
| tuque | headgear | anyone | **5.6** | 5.6 / 5.6 | 1.4 / 1.2 / 3.5 / 1.2 | 7.1 / 1.0 | 7.6 / 1.5 |
| cowboy hat | headgear | anyone | **7.0** | 7.0 / 7.0 | 1.4 / 1.2 / 3.5 / 1.2 | 2.8 / 7.5 | 5.7 / 8.0 |
| psychic foil helmet | headgear | anyone | **7.5** | 7.5 / 7.5 | 1.0 / 1.0 / 2.2 / 1.0 | 2.8 / 2.0 | 7.6 / 7.5 |
| eltex skullcap | ceremonial or psychic | anyone | **7.0** | 7.0 / 7.0 | 1.0 / 1.0 / 1.0 / 1.0 | 1.0 / 1.0 | 7.0 / 7.0 |
| gunlink | helmet | a shooter | **7.9** | 7.9 / 1.0 | 1.0 / 1.0 / 1.0 / 1.0 | 1.0 / 1.0 | 7.9 / 7.9 |
| heavy plate helmet | helmet | a melee fighter | **1.0** | 1.0 / 1.0 | 2.9 / 2.5 / 3.6 / 2.1 | 1.5 / 1.0 | 1.0 / 1.0 |
| plate gloves | everyday clothing | anyone | **1.0** | 1.0 / 1.0 | 1.0 / 1.0 / 1.1 / 1.0 | 1.3 / 1.0 | 1.0 / 1.0 |

The flak vest (3.9) rates above the flak jacket (3.2): its 100% sharp armour over the torso stops more
than the jacket's 55% over the torso and arms; the jacket's warmth and its place on the shell layer are its own gains (a
flak vest goes under a duster or a parka). Plate armour's steel stops nearly as much as recon armour, but its 0.8 cells a
second slower walk costs a fifth of a melee fighter's fight.

The heavy plate helmet is a melee fighter's: 8 levels off shooting leave a shooter at x0.43 of their fight, a melee
fighter at x0.94. It still rates 1.0 for them: its 2 levels off melee hit (74% to 68% a blow, about 8% less
damage dealt) cost more than its armour gives: a steel simple helmet's and a tenth more, over the whole head, lasting its
wearer about 2% longer. By the game's roll a helmet's armour counts for little: the head takes 6% of shots and 8% of
blows, and a longsword's 35% piercing leaves 15% of the helmet's 50% sharp armour to roll against.

## Surprises

Pieces rated far from what their age suggests (two points or more from the middle of their role and age).

- **Bracers of the Pacifist** (A RimWorld of Magic, medieval utility) rates 8.0, 5.7 above the middle of its role and age (2.3): An excellent utility piece; enchanted for a mage; steadies the nerves; for a researcher.
- **Cat Eye Pendant** (A RimWorld of Magic, medieval utility) rates 7.4, 5.1 above the middle of its role and age (2.3): A strong utility piece for a shooter; speeds up work.
- **ranged shield belt** (Vanilla Apparel Expanded - Accessories, medieval utility) rates 7.1, 4.8 above the middle of its role and age (2.3): A strong utility piece; takes shots and blasts on its shield.
- **Ring of Keen Intellect** (A RimWorld of Magic, medieval utility) rates 6.7, 4.4 above the middle of its role and age (2.3): A strong utility piece; enchanted for a mage; lifts a psycaster; speeds up work.
- **elven lord cloak (T,M,F)** (ↁ Elves (Continued), medieval everyday clothing) rates 9.2, 4.1 above the middle of its role and age (5.1): A superb armoured cloak; sharpens a fighter; keeps its wearer standing through pain; steadies the nerves.
- **Mantle of Protection** (A RimWorld of Magic, medieval everyday clothing) rates 9.1, 4.0 above the middle of its role and age (5.1): A superb everyday garment; warm in a blizzard; enchanted for a mage; steadies the nerves.
- **Dragon Slayer Plate** (A RimWorld of Magic, medieval body armour) rates 8.4, 4.0 above the middle of its role and age (4.4): An excellent body armour; stops most bullets; turns blades and claws; useless in a blizzard.
- **EMP grenade belt** (Vanilla Weapons Expanded - Grenades, industrial utility) rates 7.6, 3.9 above the middle of its role and age (3.7): An excellent utility piece; its EMP grenade at hand.
- **Ring of Labor** (A RimWorld of Magic, medieval utility) rates 6.2, 3.9 above the middle of its role and age (2.3): A solid utility piece; speeds up work; carries more.
- **Ring of the Eternal Blue** (A RimWorld of Magic, medieval utility) rates 6.1, 3.8 above the middle of its role and age (2.3): A solid utility piece; lifts a psycaster.
- **Black Guard Cloak(T,M,F)** (ↁ Elves (Continued), medieval everyday clothing) rates 8.9, 3.8 above the middle of its role and age (5.1): A superb cloak; keeps its wearer standing through pain; steadies the nerves.
- **Cold One Knight Cloak (T,M,F)** (ↁ Elves (Continued), medieval everyday clothing) rates 8.9, 3.8 above the middle of its role and age (5.1): A superb cloak; keeps its wearer standing through pain; steadies the nerves.
- **burka** (Ideology, neolithic everyday clothing) rates 1.0, 6.4 below the middle of its role and age (7.4): A poor everyday garment; a faith asks for it; slows its wearer; won't stop a bullet.
- **heavy leather helmet** (Vanilla Factions Expanded - Medieval 2, medieval helmet) rates 1.0, 6.0 below the middle of its role and age (7.0): A poor helmet for a melee fighter; warm in a blizzard; shrugs off fire; spoils the aim.
- **heavy plate helmet** (Vanilla Factions Expanded - Medieval 2, medieval helmet) rates 1.0, 6.0 below the middle of its role and age (7.0): A poor helmet for a melee fighter; for little weight; spoils the aim; clumsy in melee.
- **knuckle dusters** (Det's Xenotypes - Boglegs, industrial everyday clothing) rates 1.0, 5.8 below the middle of its role and age (6.8): A poor everyday garment; little warmth.
- **orbital mech cluster targeter** (Royalty, spacer utility) rates 1.0, 5.4 below the middle of its role and age (6.4): A poor utility piece; calls enemies down; gone once its charges are spent.
- **bomb pack** (Vanilla Factions Expanded - Deserters, spacer utility) rates 1.0, 5.4 below the middle of its role and age (6.4): A poor utility piece.
- **invisibility engulfer** (Vanilla Factions Expanded - Deserters, spacer utility) rates 1.0, 5.4 below the middle of its role and age (6.4): A poor utility piece.
- **deserter declassifier** (Vanilla Factions Expanded - Deserters, spacer utility) rates 1.0, 5.4 below the middle of its role and age (6.4): A poor utility piece.
- **fortification pack** (Rimsenal - Security Pack, spacer utility) rates 1.0, 5.4 below the middle of its role and age (6.4): A poor utility piece.
- **omnifoam pack** (Rimsenal Faction Pack - Spacer, spacer utility) rates 1.0, 5.4 below the middle of its role and age (6.4): A poor utility piece.
- **marine boots** (Vanilla Armour Expanded, spacer body armour) rates 2.0, 5.2 below the middle of its role and age (7.2): A poor body armour; quickens the step; for little weight; covers little.
- **heat pearls** (Vanilla Psycasts Expanded, spacer utility) rates 1.4, 5.0 below the middle of its role and age (6.4): A poor utility piece; its heat pearl at hand; gone once its charges are spent.

## The review

Sorted by rating within each table. Shooter / melee: the piece's rating for a shooter and for a melee fighter, where they
differ by half a point or more (the rating is the one it is made for, marked, or the mix when it suits both).
Protection: against guns / melee / fire / mechanoids (how much longer the wearer
lasts, on the sub-ratings' scale). In the cold / heat: the overall in that climate. By stuff: the piece in each common stuff
of its kind. Tags in the order of importance; the line in plain words is the mod's own.

### Body armour (92)

| Rating | Shooter / melee | Piece | Mod | Age | Stuff | Guns / melee / fire / mech | Cold / heat °C | In the cold / heat | By stuff | Tags | In plain words |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **8.7** |  | archoplate | Vanilla Races Expanded - Archon | archotech |  | 9.7 / 6.1 / 9.0 / 9.2; vacuum 4.5, psychic 8.5 | 38 / 22 | 8.8 / 8.7 |  | ◆ Combat armour, ✦ Shield, ▲ Stops bullets, ▲ Holds off mechs, ▲ Warm, ▲ Fireproof, ▲ Cool | A superb body armour; takes shots and blasts on its shield; stops most bullets; holds up against mechanoids. |
| **8.7** | 1.0 / **8.7** | close combat armor | Rimsenal: Murder diversified | spacer |  | 9.7 / 6.1 / 8.8 / 9.2; vacuum 4.5 | 68 / 10 | 8.8 / 8.7 |  | ◆ Combat armour, ◆ For melee fighters, ▼ Blocks shooting, ✦ Shield, ▲ Stops bullets, ▲ Holds off mechs, ▲ Warm | A superb body armour for a melee fighter; takes shots and blasts on its shield; stops most bullets; its wearer can't shoot out. |
| **8.7** |  | mech breaker armor | Alpha Mechs | spacer |  | 9.7 / 6.1 / 8.4 / 9.3; vacuum 4.5, mechanitor 8.6 | 35 / 18 | 8.8 / 8.8 |  | ◆ Combat armour, ✦ Shield, ▲ Stops bullets, ● Mechanitor only, ▲ Holds off mechs, ▼ Slows you, ▲ Warm | A superb body armour; takes shots and blasts on its shield; stops most bullets; slows its wearer. |
| **8.5** |  | assault armor | Rimsenal: Murder diversified | spacer |  | 8.4 / 7.5 / 5.4 / 8.1; vacuum 4.5 | 68 / 10 | 8.7 / 8.5 |  | ◆ Combat armour, ▲ Stops bullets, ▲ Turns blades, ▲ Holds off mechs, ▼ Slows you, ✦ Grants: emp grenade, ▲ Warm | A superb body armour; stops most bullets; turns blades and claws; slows its wearer. |
| **8.5** | 8.0 / **8.5** | forsaken armor | Alpha Genes | ultra |  | 8.1 / 7.0 / 5.4 / 7.5; vacuum 4.5, work 8.5, carrying 8.4 | 36 / 20 | 8.6 / 8.6 |  | ◆ Combat armour, ◆ For melee fighters, ▲ Stops bullets, ▲ Turns blades, ▲ Holds off mechs, ▲ Warm, ▲ Cool | A superb body armour for a melee fighter; stops most bullets; turns blades and claws; holds up against mechanoids. |
| **8.5** |  | prestige siegebreaker armor | Vanilla Armour Expanded | spacer |  | 9.7 / 6.1 / 8.4 / 9.2; vacuum 4.5, psychic 3.1 | 70 / 12 | 8.7 / 8.5 |  | ◆ Combat armour, ✦ Shield, ▲ Stops bullets, ▲ Holds off mechs, ▼ Slows you, ▲ Warm, ▲ Fireproof | A superb body armour; takes shots and blasts on its shield; stops most bullets; slows its wearer. |
| **8.5** |  | siegebreaker armor | Vanilla Armour Expanded | spacer |  | 9.7 / 6.1 / 8.4 / 9.2; vacuum 4.5 | 70 / 12 | 8.7 / 8.5 |  | ◆ Combat armour, ✦ Shield, ▲ Stops bullets, ▲ Holds off mechs, ▼ Slows you, ▲ Warm, ▲ Fireproof | A superb body armour; takes shots and blasts on its shield; stops most bullets; slows its wearer. |
| **8.4** |  | Dragon Slayer Plate | A RimWorld of Magic | medieval | steel | 7.7 / 7.1 / 7.7 / 7.2; toxins 5.5, work 4.4, magic 7.3 | 5.8 / 10 | 8.5 / 8.5 | steel 8.4, plasteel 8.6, uranium 8.6 | ◆ Combat armour, ▲ Stops bullets, ▲ Turns blades, ▲ Holds off mechs, ● Needs a mage, ▲ Fireproof, ▼ No warmth | An excellent body armour; stops most bullets; turns blades and claws; useless in a blizzard. |
| **8.3** | 7.8 / **8.3** | drop suit | Rimsenal: Murder diversified | spacer |  | 7.7 / 4.7 / 5.9 / 6.7; vacuum 5.5, work 7.3 | 70 / 20 | 8.5 / 8.3 |  | ◆ Combat armour, ◆ For melee fighters, ▲ Stops bullets, ✦ Jump, ▲ Warm, ▲ Cool, ▲ Full cover | An excellent body armour for a melee fighter; stops most bullets; jumps over walls and enemies; warm in a blizzard. |
| **8.0** |  | heavy crypto armor | Vanilla Quests Expanded - Cryptoforge | ultra |  | 7.8 / 6.2 / 8.3 / 7.1; vacuum 4.5 | 78 / 16 | 8.4 / 8.0 |  | ◆ Combat armour, ▲ Stops bullets, ▲ Holds off mechs, ▲ Warm, ▲ Fireproof, ▲ Cool, ▲ Full cover | An excellent body armour; stops most bullets; holds up against mechanoids; warm in a blizzard. |
| **8.0** |  | mech master suit | Alpha Mechs | spacer |  | 7.4 / 5.6 / 4.9 / 6.3; vacuum 4.5, mechanitor 8.2 | 38 / 16 | 8.2 / 8.1 |  | ◆ Combat armour, ▲ Stops bullets, ● Mechanitor only, ▼ Slows you, ▲ Warm, ✦ Mech bandwidth, ▲ Cool | An excellent body armour; stops most bullets; warm in a blizzard; slows its wearer. |
| **8.0** | 7.4 / **8.0** | strike suit | Rimsenal: Murder diversified | spacer |  | 5.8 / 3.2 / 3.1 / 4.3; vacuum 5.0, work 8.5 | 65 / 15 | 8.3 / 8.0 |  | ◆ Combat armour, ◆ For melee fighters, ✦ Jump, ▲ Warm, ▲ Cool, ▲ Full cover, ▲ Quick feet | An excellent body armour for a melee fighter; jumps over walls and enemies; warm in a blizzard; keeps its wearer cool. |
| **7.7** |  | phoenix armor | Royalty | spacer |  | 7.6 / 5.8 / 5.9 / 6.7; vacuum 4.5 | 70 / 100 | 8.1 / 8.3 |  | ◆ Combat armour, ▲ Stops bullets, ▼ Slows you, ✦ Grants: launch incendiary, ▲ Warm, ▲ Cool, ▲ Full cover | An excellent body armour; stops most bullets; its launch incendiary at hand; slows its wearer. |
| **7.7** |  | riot gear | Rimsenal Faction Pack - Federation | spacer |  | 8.1 / 6.2 / 5.9 / 7.5; vacuum 4.5 | 68 / 10 | 8.2 / 7.7 |  | ◆ Combat armour, ▲ Stops bullets, ▲ Holds off mechs, ▼ Slows you, ▲ Warm, ▲ Full cover, ▼ Weak to blunt | An excellent body armour; stops most bullets; holds up against mechanoids; slows its wearer. |
| **7.6** |  | crypto armor | Vanilla Quests Expanded - Cryptoforge | ultra |  | 7.1 / 5.2 / 8.1 / 6.1; vacuum 4.5 | 64 / 16 | 8.1 / 7.7 |  | ◆ Combat armour, ▲ Stops bullets, ▲ Warm, ▲ Fireproof, ▲ Cool, ▲ Full cover, ✦ Stops bleeding | An excellent body armour; stops most bullets; warm in a blizzard; shrugs off fire. |
| **7.6** | 7.0 / **7.6** | locust armor | Royalty | spacer |  | 7.2 / 3.8 / 3.2 / 5.8; vacuum 4.5 | 64 / 9 | 8.1 / 7.6 |  | ◆ Combat armour, ◆ For melee fighters, ▲ Stops bullets, ✦ Jump, ▲ Warm, ▲ Full cover, ◆ Spacefarer's | An excellent body armour for a melee fighter; stops most bullets; jumps over walls and enemies; warm in a blizzard. |
| **7.5** |  | absolver armor | Vanilla Factions Expanded - Empire | spacer |  | 7.1 / 5.2 / 4.0 / 5.9; vacuum 4.5, work 4.9, hunting 4.2 | 32 / 9 | 7.8 / 7.5 |  | ◆ Combat armour, ▲ Stops bullets, ▲ Warm, ▲ Full cover, ▲ Quick feet, ◆ Work clothes, ◆ Spacefarer's | An excellent body armour; stops most bullets; warm in a blizzard; covers nearly everything. |
| **7.5** |  | cataphract armor | Royalty | spacer |  | 7.8 / 6.2 / 4.5 / 7.0; vacuum 4.5 | 70 / 12 | 8.1 / 7.5 |  | ◆ Combat armour, ▲ Stops bullets, ▲ Holds off mechs, ▼ Slows you, ▲ Warm, ▲ Full cover, ◆ Spacefarer's | An excellent body armour; stops most bullets; holds up against mechanoids; slows its wearer. |
| **7.5** |  | grenadier armor | Royalty | spacer |  | 6.8 / 4.7 / 3.7 / 5.4; vacuum 4.5 | 68 / 10 | 8.1 / 7.5 |  | ◆ Combat armour, ▼ Slows you, ✦ Grants: frag grenade, ▲ Warm, ▲ Full cover, ◆ Spacefarer's, ▲ Hard-wearing | An excellent body armour; its frag grenade at hand; warm in a blizzard; slows its wearer. |
| **7.5** |  | hazard carapace | Rimsenal: Murder diversified | spacer |  | 6.3 / 4.3 / 7.9 / 5.0; toxins 5.5, vacuum 5.5, psychic shelter 7.9 | 150 / 150 | 8.4 / 8.4 |  | ◆ Combat armour, ▼ Slows you, ▲ Warm, ▲ Fireproof, ▲ Cool, ✦ Psychic shield, ✦ Toxin-proof | An excellent body armour; warm in a blizzard; shrugs off fire; slows its wearer. |
| **7.5** | 6.7 / **7.5** | mechlord suit | Biotech | spacer |  | 6.1 / 4.1 / 3.5 / 4.6; vacuum 4.5, mechanitor 7.5 | 64 / 9 | 8.0 / 7.4 |  | ◆ Combat armour, ◆ For melee fighters, ▼ Spoils your aim, ● Mechanitor only, ▼ Slows you, ▲ Warm, ✦ Mech bandwidth | An excellent body armour for a melee fighter; warm in a blizzard; more mechs for a mechanitor; spoils the aim. |
| **7.5** |  | prestige cataphract armor | Royalty | spacer |  | 7.8 / 6.2 / 4.5 / 7.0; vacuum 4.5, psychic 3.1 | 70 / 12 | 8.1 / 7.5 |  | ◆ Combat armour, ▲ Stops bullets, ▲ Holds off mechs, ▼ Slows you, ▲ Warm, ● Needs a psylink, ✦ Psychic | An excellent body armour; stops most bullets; holds up against mechanoids; slows its wearer. |
| **7.5** |  | reflactor carapace | Rimsenal: Murder diversified | spacer |  | 6.7 / 5.0 / 8.9 / 5.6; vacuum 4.5 | 75 / 150 | 8.0 / 8.4 |  | ◆ Combat armour, ▼ Slows you, ▲ Warm, ▲ Fireproof, ▲ Cool, ▲ Full cover, ◆ Spacefarer's | An excellent body armour; warm in a blizzard; shrugs off fire; slows its wearer. |
| **7.4** |  | deserter armor | Vanilla Factions Expanded - Empire | spacer |  | 7.3 / 5.4 / 4.5 / 6.1; vacuum 4.5, psychic 6.1 | 42 / 14 | 7.9 / 7.5 |  | ◆ Combat armour, ▲ Stops bullets, ▲ Warm, ● Needs a psylink, ✦ Psychic, ▲ Full cover, ◆ Spacefarer's | A strong body armour; stops most bullets; warm in a blizzard; lifts a psycaster. |
| **7.4** |  | forsaken cloak | Alpha Genes | spacer |  | 7.6 / 5.7 / 3.0 / 6.5; hunting 2.8 | 24 / 8 | 7.7 / 7.5 |  | ◆ Combat armour, ▲ Stops bullets, ▲ Warm, ▲ Full cover, ◆ Royal, ▲ Light, ▲ Melee edge | A strong body armour; stops most bullets; warm in a blizzard; covers nearly everything. |
| **7.4** |  | privateer armor | Vanilla Factions Expanded - Pirates | spacer |  | 7.5 / 4.7 / 3.7 / 6.4; vacuum 4.5 | 34 / 10 | 7.8 / 7.5 |  | ◆ Combat armour, ◆ For melee fighters, ▲ Stops bullets, ✦ Jump, ▼ Slows you, ▲ Warm, ▲ Full cover | A strong body armour for a melee fighter; stops most bullets; jumps over walls and enemies; slows its wearer. |
| **7.2** |  | artillery armor | Rimsenal: Murder diversified | spacer |  | 5.8 / 4.0 / 3.8 / 4.4; vacuum 4.5 | 68 / 10 | 8.0 / 7.2 |  | ◆ Combat armour, ▼ Slows you, ✦ Grants: crow feeder, ▲ Warm, ▲ Full cover, ▼ Clumsy in melee, ◆ Spacefarer's | A strong body armour; its crow feeder at hand; warm in a blizzard; slows its wearer. |
| **7.2** |  | prestige marine armor | Royalty | spacer |  | 7.1 / 5.2 / 4.0 / 5.9; vacuum 4.5, psychic 3.1 | 68 / 10 | 8.0 / 7.2 |  | ◆ Combat armour, ▲ Stops bullets, ▼ Slows you, ▲ Warm, ● Needs a psylink, ✦ Psychic, ▲ Full cover | A strong body armour; stops most bullets; warm in a blizzard; slows its wearer. |
| **7.1** |  | marine armor | Core | spacer |  | 7.1 / 5.2 / 4.0 / 5.9; vacuum 4.5 | 68 / 10 | 7.9 / 7.1 |  | ◆ Combat armour, ▲ Stops bullets, ▼ Slows you, ▲ Warm, ▲ Full cover, ◆ Spacefarer's, ▲ Hard-wearing | A strong body armour; stops most bullets; warm in a blizzard; slows its wearer. |
| **6.9** |  | modular Medic armor | Rimsenal: Murder diversified | spacer |  | 6.5 / 4.5 / 3.5 / 5.0; work 8.1 | 25 / 10 | 7.3 / 7.0 |  | ◆ Combat armour, ▼ Slows you, ▲ Warm, ▲ Full cover, ◆ Work clothes, ◆ Medic's, ▲ Hard-wearing | A strong body armour; warm in a blizzard; covers nearly everything; slows its wearer. |
| **6.9** |  | modular pioneer armor | Rimsenal: Murder diversified | spacer |  | 6.5 / 4.5 / 3.5 / 5.0; work 8.2 | 25 / 10 | 7.4 / 7.1 |  | ◆ Combat armour, ▼ Slows you, ▲ Warm, ▲ Full cover, ◆ Work clothes, ◆ Builder's, ▲ Hard-wearing | A strong body armour; warm in a blizzard; covers nearly everything; slows its wearer. |
| **6.8** | **6.8** / 3.7 | fire support armor | Rimsenal: Murder diversified | spacer |  | 5.8 / 4.0 / 3.8 / 4.4; vacuum 4.5 | 68 / 10 | 7.8 / 6.8 |  | ◆ Combat armour, ◆ For shooters, ▼ Clumsy in melee, ▼ Slows you, ▲ Warm, ▲ Full cover, ▲ Steadier aim | A strong body armour for a shooter; warm in a blizzard; covers nearly everything; clumsy in melee. |
| **6.8** |  | prestige recon armor | Royalty | spacer |  | 6.1 / 4.1 / 3.5 / 4.6; vacuum 4.5, psychic 3.1 | 64 / 9 | 7.8 / 6.8 |  | ◆ Combat armour, ▲ Warm, ● Needs a psylink, ✦ Psychic, ▲ Full cover, ◆ Royal, ◆ Spacefarer's | A strong body armour; warm in a blizzard; lifts a psycaster; covers nearly everything. |
| **6.8** |  | recon armor | Core | spacer |  | 6.1 / 4.1 / 3.5 / 4.6; vacuum 4.5 | 64 / 9 | 7.8 / 6.8 |  | ◆ Combat armour, ▲ Warm, ▲ Full cover, ◆ Spacefarer's, ● Advanced research, ● Biocodable | A strong body armour; warm in a blizzard; covers nearly everything. |
| **6.7** |  | feral exosuit | Rimsenal Faction Pack - Feral | spacer |  | 6.8 / 4.9 / 2.5 / 5.4; vacuum 4.5 | 65 / 8 | 7.7 / 6.6 |  | ◆ Combat armour, ▼ Slows you, ▲ Warm, ▼ Slows work, ▲ Full cover, ◆ Spacefarer's, ▲ Hard-wearing | A strong body armour; warm in a blizzard; covers nearly everything; slows its wearer. |
| **6.6** |  | Elven Lord Armor (T,M,F) | ↁ Elves (Continued) | medieval | steel | 4.0 / 2.7 / 3.0 / 3.1; social 7.2 | 4.5 / 0 | 6.8 / 6.6 | steel 6.6, plasteel 7.1, uranium 7.0 | ◆ Combat armour, ▼ Slows you, ◆ Royal, ▲ Shrugs off pain, ▲ Steady nerves, ▼ No warmth, ◆ Trader's | A strong body armour; fit for a title; keeps its wearer standing through pain; slows its wearer. |
| **6.6** |  | padded armor | Vanilla Factions Expanded - Medieval 2 | medieval | devilstrand | 6.0 / 3.8 / 8.9 / 4.7 | 24 / 4.8 | 7.1 / 6.6 | cloth 2.4, synthread 4.7, devilstrand 6.6, hyperweave 7.8 | ◆ Combat armour, ▲ Warm, ▲ Fireproof, ▲ Full cover, ▼ Weak to blunt, ▲ Light, ⚠ Tattered mood | A strong body armour; warm in a blizzard; shrugs off fire; blunt blows get through. |
| **6.5** |  | ancient astrosuit | Vanilla Races Expanded - Fungoid | spacer |  | 5.3 / 3.9 / 7.1 / 4.0; vacuum 4.7 | 75 / 40 | 7.7 / 7.2 |  | ◆ Combat armour, ▼ Slows you, ▲ Warm, ▲ Fireproof, ▲ Cool, ▲ Full cover, ◆ Spacefarer's | A strong body armour; warm in a blizzard; shrugs off fire; slows its wearer. |
| **6.5** |  | Armor of the Valiant | A RimWorld of Magic | medieval | steel | 5.5 / 4.0 / 4.0 / 4.2; social 5.1, magic 2.4 | 7.3 / 5 | 6.7 / 6.6 | steel 6.5, plasteel 7.2, uranium 7.0 | ◆ Combat armour, ● Needs a mage, ✦ Enchanted, ▲ Full cover, ▲ Steadier aim, ▲ Melee edge, ▼ No warmth | A strong body armour; enchanted for a mage; covers nearly everything; useless in a blizzard. |
| **6.5** |  | heavy flak armor | Rimsenal - Augmented Vanilla Pack | industrial |  | 7.1 / 5.2 / 2.8 / 5.8 | 25 / 0 | 7.1 / 6.4 |  | ◆ Combat armour, ▲ Stops bullets, ▼ Slows you, ▲ Warm, ▲ Full cover, ▲ Hard-wearing | A strong body armour; stops most bullets; warm in a blizzard; slows its wearer. |
| **6.5** |  | marksman gear | Rimsenal Faction Pack - Federation | spacer |  | 5.4 / 3.5 / 2.0 / 4.0; vacuum 4.5 | 25 / 15 | 7.1 / 6.8 |  | ◆ Combat armour, ▲ Warm, ▲ Cool, ▲ Full cover, ▲ Steadier aim, ▼ Weak to blunt, ⚠ Tattered mood | A strong body armour; warm in a blizzard; keeps its wearer cool; blunt blows get through. |
| **6.4** |  | combat vacsuit | Vanilla Gravship Expanded - Chapter 2 | spacer |  | 5.7 / 3.9 / 6.1 / 4.3; vacuum 4.8 | 90 / 15 | 7.9 / 6.5 |  | ◆ Combat armour, ▼ Slows you, ▲ Warm, ▲ Cool, ▲ Full cover, ◆ Spacefarer's, ● Advanced research | A solid body armour; warm in a blizzard; keeps its wearer cool; slows its wearer. |
| **6.2** |  | modular security armor | Rimsenal: Murder diversified | spacer |  | 5.6 / 3.8 / 3.1 / 4.2 | 25 / 10 | 6.9 / 6.4 |  | ◆ Combat armour, ▼ Slows you, ▲ Warm, ▲ Full cover, ▲ Hard-wearing | A solid body armour; warm in a blizzard; covers nearly everything; slows its wearer. |
| **6.1** |  | prestige trooper armor | Vanilla Armour Expanded | spacer |  | 3.6 / 2.7 / 3.0 / 2.7; vacuum 4.0, psychic 3.1, work 5.1 | 20 / 10 | 6.7 / 6.3 |  | ◆ Combat armour, ▲ Warm, ● Needs a psylink, ✦ Psychic, ▲ Full cover, ▲ Quick feet, ◆ Royal | A solid body armour; warm in a blizzard; lifts a psycaster; covers nearly everything. |
| **6.0** |  | trooper armor | Vanilla Armour Expanded | spacer |  | 3.6 / 2.7 / 3.0 / 2.7; vacuum 4.0, work 5.1 | 20 / 10 | 6.6 / 6.3 |  | ◆ Combat armour, ▲ Warm, ▲ Full cover, ▲ Quick feet, ▲ Steady nerves, ◆ Work clothes, ● Advanced research | A solid body armour; warm in a blizzard; covers nearly everything; quickens the step. |
| **6.0** |  | unity guard curaiss | Rimsenal Faction Pack - Federation | spacer |  | 5.8 / 3.4 / 1.9 / 4.6 | 0 / 0 | 6.0 / 6.0 |  | ◆ Combat armour, ▼ Slows you, ▼ Weak to blunt, ▼ No warmth, ● Advanced research | A solid body armour; slows its wearer; blunt blows get through; useless in a blizzard. |
| **5.9** |  | modular nomad armor | Rimsenal: Murder diversified | spacer |  | 3.9 / 2.8 / 2.5 / 3.0; hunting 2.6 | 35 / 20 | 6.9 / 6.5 |  | ◆ Combat armour, ▲ Warm, ▲ Cool, ⚠ Tattered mood, ▲ Heals faster, ◆ Hunter's | A solid body armour; warm in a blizzard; keeps its wearer cool; tatters after a few fights. |
| **5.4** |  | king’s robes | Vanilla Factions Expanded - Medieval 2 | medieval | devilstrand | 4.1 / 2.7 / 7.2 / 3.3; social 7.2 | 7 / 4.8 | 5.8 / 5.7 | cloth 2.4, synthread 4.0, devilstrand 5.4, hyperweave 6.9, plainleather 3.6, bearskin 4.5, thrumbofur 6.9 | ◆ Combat armour, ▲ Fireproof, ▼ Weak to blunt, ◆ Royal, ▲ Light, ⚠ Tattered mood, ▼ No warmth | A fair body armour; shrugs off fire; fit for a title; blunt blows get through. |
| **5.3** |  | tactical vest | Rimsenal - Augmented Vanilla Pack | spacer |  | 4.0 / 2.3 / 1.3 / 3.2; work 5.2 | 1 / 0 | 5.3 / 5.2 |  | ◆ Combat armour, ▼ Thin cover, ▲ Quick feet, ▲ Steadier aim, ▼ Weak to blunt, ▲ Light, ◆ Work clothes | A fair body armour; quickens the step; steadies the aim; covers little. |
| **5.1** |  | Shadow Warrior Armor (T,M,F) | ↁ Elves (Continued) | medieval | steel | 3.0 / 2.2 / 2.5 / 2.3; psychic shelter 2.8, social 5.5 | 3.3 / 0 | 5.3 / 5.1 | steel 5.1, plasteel 5.9, uranium 5.7 | ◆ Combat armour, ✦ Psychic shield, ▲ Melee edge, ▲ Shrugs off pain, ▼ Blocks psycasts, ▲ Steady nerves, ▼ No warmth | A fair body armour; shields the mind from psychic attacks; sharpens a fighter; dulls psycasts. |
| **5.0** |  | Witch Armor (F) | ↁ Elves (Continued) | medieval | steel | 3.0 / 2.2 / 2.5 / 2.3; social 3.9 | 3.3 / 0 | 5.2 / 5.0 | steel 5.0, plasteel 5.8, uranium 5.6 | ◆ Combat armour, ▲ Melee edge, ▲ Steady nerves, ▼ No warmth, ▲ Good impression, ▲ Hard-wearing | A fair body armour; sharpens a fighter; steadies the nerves; useless in a blizzard. |
| **4.8** | 5.0 / 4.1 | heavy leather armor | Vanilla Factions Expanded - Medieval 2 | medieval | plainleather | 5.2 / 2.9 / 8.3 / 3.9 | 32 / 0 | 6.5 / 4.4 | plainleather 4.8, bearskin 6.9, thrumbofur 8.3 | ◆ Combat armour, ▼ Slows you, ▲ Warm, ▲ Fireproof, ▲ Full cover, ▼ Clumsy in melee, ▼ Weak to blunt | A fair body armour; warm in a blizzard; shrugs off fire; slows its wearer. |
| **4.7** | 4.8 / 4.2 | feral mail | Rimsenal Faction Pack - Feral | industrial |  | 5.1 / 4.0 / 1.0 / 3.7 | 20 / 5 | 5.9 / 4.8 |  | ◆ Combat armour, ▼ Slows you, ▲ Warm, ▼ Slows work, ▲ Full cover, ⚠ Tattered mood, ▼ Flammable | A fair body armour; warm in a blizzard; covers nearly everything; slows its wearer. |
| **4.7** | 4.9 / 3.9 | prestige plate armor | Vanilla Psycasts Expanded | medieval | steel | 5.0 / 3.5 / 4.0 / 3.7; psychic 3.2 | 3 / 0 | 4.9 / 4.7 | steel 4.7, plasteel 6.3, uranium 6.0, wood 2.0 | ◆ Combat armour, ▼ Slows you, ● Needs a psylink, ✦ Psychic, ▲ Full cover, ◆ Royal, ▼ No warmth | A fair body armour; lifts a psycaster; covers nearly everything; slows its wearer. |
| **4.6** |  | Black Guard Armor (T,M,F) | ↁ Elves (Continued) | medieval | steel | 4.0 / 2.7 / 3.0 / 3.1; social 7.2 | 4.5 / 0 | 4.9 / 4.6 | steel 4.6, plasteel 5.8, uranium 5.5 | ◆ Combat armour, ▼ Slows you, ▼ No warmth, ◆ Trader's, ▲ Good impression, ▲ Hard-wearing | A fair body armour; helps in talks and trade; makes a good impression; slows its wearer. |
| **4.6** |  | Dragon Prince Armor (T,M,F) | ↁ Elves (Continued) | medieval | steel | 4.0 / 2.7 / 3.0 / 3.1; social 7.2 | 4.5 / 0 | 4.9 / 4.6 | steel 4.6, plasteel 5.8, uranium 5.5 | ◆ Combat armour, ▼ Slows you, ◆ Royal, ▼ No warmth, ◆ Trader's, ▲ Good impression, ▲ Hard-wearing | A fair body armour; fit for a title; helps in talks and trade; slows its wearer. |
| **4.6** |  | leather armor | Vanilla Factions Expanded - Medieval 2 | medieval | plainleather | 2.8 / 1.9 / 7.3 / 2.0 | 24 / 0 | 6.0 / 4.3 | plainleather 4.6, bearskin 5.8, thrumbofur 7.9 | ◆ Combat armour, ▼ Slows you, ▲ Warm, ▲ Fireproof, ▲ Full cover, ▼ Weak to blunt, ▲ Shrugs off pain | A fair body armour; warm in a blizzard; shrugs off fire; slows its wearer. |
| **4.6** |  | White Lion Armor (T,M,F) | ↁ Elves (Continued) | medieval | steel | 4.0 / 2.7 / 3.0 / 3.1; social 7.2 | 4.5 / 0 | 4.9 / 4.6 | steel 4.6, plasteel 5.8, uranium 5.5 | ◆ Combat armour, ▼ Slows you, ▼ No warmth, ◆ Trader's, ▲ Good impression, ▲ Hard-wearing | A fair body armour; helps in talks and trade; makes a good impression; slows its wearer. |
| **4.5** |  | janissary cuirass | Vanilla Factions Expanded - Empire | spacer |  | 3.1 / 2.0 / 1.9 / 2.5; vacuum 4.5 | 18 / 6 | 5.6 / 4.7 |  | ◆ Combat armour, ▼ Thin cover, ◆ Spacefarer's, ⚠ Counts as naked, ● Advanced research, ● Biocodable | A fair body armour; covers little. |
| **4.5** | 4.7 / 3.7 | plate armor | Core | medieval | steel | 5.0 / 3.5 / 4.0 / 3.7 | 3 / 0 | 4.7 / 4.5 | steel 4.5, plasteel 6.2, uranium 5.9, wood 1.4 | ◆ Combat armour, ▼ Slows you, ▲ Full cover, ▼ No warmth, ● Stuff matters | A fair body armour; covers nearly everything; slows its wearer; useless in a blizzard. |
| **4.4** | 4.6 / 4.1 | Cold One Knight Armor (T,M,F) | ↁ Elves (Continued) | medieval | steel | 4.0 / 2.7 / 3.0 / 3.1; social 7.2 | 4.5 / 0 | 4.8 / 4.4 | steel 4.4, plasteel 5.7, uranium 5.4 | ◆ Combat armour, ▼ Slows you, ◆ Royal, ▼ No warmth, ◆ Trader's, ▲ Good impression, ▲ Hard-wearing | A modest body armour; fit for a title; helps in talks and trade; slows its wearer. |
| **4.4** | 4.8 / 3.1 | heavy plate armor | Vanilla Factions Expanded - Medieval 2 | medieval | steel | 6.1 / 3.7 / 4.4 / 4.5 | 3 / 0 | 4.6 / 4.4 | steel 4.4, plasteel 6.5, uranium 6.1 | ◆ Combat armour, ▼ Slows you, ▲ Full cover, ▼ Clumsy in melee, ▼ No warmth, ▲ Hard-wearing, ● Stuff matters | A modest body armour; covers nearly everything; slows its wearer; clumsy in melee. |
| **4.4** |  | legionnaire armor | Vanilla Factions Expanded - Classical | medieval | steel | 3.4 / 2.4 / 3.1 / 2.5 | 2.7 / 0 | 4.6 / 4.3 | steel 4.4, plasteel 5.5, uranium 5.2, wood 2.9 | ◆ Combat armour, ▼ Slows you, ▲ Full cover, ▲ Steady nerves, ▼ No warmth, ● Stuff matters | A modest body armour; covers nearly everything; steadies the nerves; slows its wearer. |
| **4.3** |  | centurion armor | Vanilla Factions Expanded - Classical | medieval | steel | 3.7 / 2.7 / 3.3 / 2.7 | 2.9 / 0 | 4.5 / 4.3 | steel 4.3, plasteel 5.6, uranium 5.3, wood 2.5 | ◆ Combat armour, ▼ Slows you, ▲ Full cover, ▼ No warmth, ● Stuff matters | A modest body armour; covers nearly everything; slows its wearer; useless in a blizzard. |
| **4.1** |  | light plate armor | Vanilla Armour Expanded | medieval | steel | 3.4 / 2.4 / 2.6 / 2.7 | 0.4 / 0 | 4.1 / 4.1 | steel 4.1, plasteel 5.1, uranium 4.9 | ◆ Combat armour, ▼ Slows you, ▼ No warmth | A modest body armour; slows its wearer; useless in a blizzard. |
| **3.9** |  | flak vest | Core | industrial |  | 3.2 / 2.0 / 1.4 / 2.6 | 1 / 0 | 4.0 / 3.9 |  | ◆ Combat armour, ▼ Slows you, ▼ Thin cover, ▼ No warmth | A modest body armour; slows its wearer; covers little; useless in a blizzard. |
| **3.7** |  | riot armor | Vanilla Armour Expanded | industrial |  | 2.3 / 2.6 / 2.7 / 1.6 | 14.4 / 6 | 4.8 / 4.0 |  | ◆ Combat armour, ▼ Slows you, ▲ Full cover | A modest body armour; covers nearly everything; slows its wearer. |
| **3.6** |  | ancient tight parka | Vanilla Quests Expanded - The Generator | spacer |  | 1.7 / 1.3 / 1.9 / 1.3 | 68 / 0 | 7.0 / 2.5 |  | ◆ Combat armour, ● Hard to find, ▲ Warm, ▼ Weak to blunt, ▲ Light, ⚠ Tattered mood | A modest body armour; warm in a blizzard; for little weight; blunt blows get through. |
| **3.3** |  | elven chainmail (T,M,F) | ↁ Elves (Continued) | medieval | steel | 2.3 / 1.7 / 2.3 / 1.7 | 1.7 / 0 | 3.4 / 3.3 | steel 3.3, plasteel 4.0, uranium 3.9 | ◆ Combat armour, ▲ Light, ⚠ Tattered mood, ▼ No warmth | A weak body armour; for little weight; tatters after a few fights; useless in a blizzard. |
| **3.3** |  | elven scalemail (T,M,F) | ↁ Elves (Continued) | medieval | steel | 2.3 / 1.7 / 2.3 / 1.7 | 2 / 0 | 3.5 / 3.3 | steel 3.3, plasteel 4.0, uranium 3.9 | ◆ Combat armour, ▲ Light, ⚠ Tattered mood, ▼ No warmth | A weak body armour; for little weight; tatters after a few fights; useless in a blizzard. |
| **3.3** |  | Morathi Pauldrons (F) | ↁ Elves (Continued) | medieval | steel | 1.1 / 1.1 / 1.2 / 1.1; social 3.9 | 1.3 / 0 | 3.4 / 3.3 | steel 3.3, plasteel 3.4, uranium 3.4 | ◆ Combat armour, ▼ Thin cover, ▲ Light, ▲ Steady nerves, ▼ No warmth, ▲ Good impression, ▲ Hard-wearing | A weak body armour; for little weight; steadies the nerves; covers little. |
| **3.2** |  | flak jacket | Core | industrial |  | 2.3 / 1.7 / 1.3 / 1.7 | 14.4 / 3 | 4.4 / 3.3 |  | ◆ Combat armour, ▼ Slows you, ▼ Weak to blunt | A weak body armour; slows its wearer; blunt blows get through. |
| **3.2** |  | scrap armor | Rimsenal Faction Pack - Feral | industrial |  | 2.9 / 2.2 / 1.0 / 2.2 | 0 / 0 | 3.2 / 3.2 |  | ◆ Combat armour, ▼ Slows you, ▼ Slows work, ⚠ Tattered mood, ▼ No warmth, ▼ Flammable | A weak body armour; slows its wearer; slows work; tatters after a few fights. |
| **3.1** |  | chestplate | Vanilla Armour Expanded | medieval | steel | 2.6 / 1.7 / 1.8 / 2.1 | 0.4 / 0 | 3.2 / 3.1 | steel 3.1, plasteel 3.8, uranium 3.7 | ◆ Combat armour, ▼ Slows you, ▼ Thin cover, ▼ No warmth | A weak body armour; slows its wearer; covers little; useless in a blizzard. |
| **3.0** |  | Elven Lord Pauldrons (T,M,F) | ↁ Elves (Continued) | medieval | steel | 1.1 / 1.1 / 1.2 / 1.1; social 5.1 | 1.3 / 0 | 3.1 / 2.9 |  | ◆ Combat armour, ▼ Thin cover, ▲ Light, ▲ Steady nerves, ▼ No warmth, ▲ Good impression, ▲ Hard-wearing | A weak body armour; for little weight; steadies the nerves; covers little. |
| **3.0** |  | MOPP suit(Desert) | Dubs Rimatomics | industrial |  | 2.2 / 1.6 / 2.2 / 1.5; toxins 4.7 | 25 / 6 | 5.0 / 3.2 |  | ◆ Combat armour, ▼ Slows you, ▲ Warm, ▼ Slows work, ▲ Full cover, ▼ Weak to blunt, ▼ Off-putting | A weak body armour; warm in a blizzard; covers nearly everything; slows its wearer. |
| **3.0** |  | MOPP suit(Snow) | Dubs Rimatomics | industrial |  | 2.2 / 1.6 / 2.2 / 1.5; toxins 4.7 | 25 / 6 | 5.0 / 3.2 |  | ◆ Combat armour, ▼ Slows you, ▲ Warm, ▼ Slows work, ▲ Full cover, ▼ Weak to blunt, ▼ Off-putting | A weak body armour; warm in a blizzard; covers nearly everything; slows its wearer. |
| **3.0** |  | MOPP suit(Woodland) | Dubs Rimatomics | industrial |  | 2.2 / 1.6 / 2.2 / 1.5; toxins 4.7 | 25 / 6 | 5.0 / 3.2 |  | ◆ Combat armour, ▼ Slows you, ▲ Warm, ▼ Slows work, ▲ Full cover, ▼ Weak to blunt, ▼ Off-putting | A weak body armour; warm in a blizzard; covers nearly everything; slows its wearer. |
| **2.7** |  | chainmail | Vanilla Armour Expanded | medieval | steel | 2.1 / 1.6 / 2.3 / 1.5 | 0.2 / 0 | 2.8 / 2.7 | steel 2.7, plasteel 3.5, uranium 3.3 | ◆ Combat armour, ▼ Slows you, ▲ Full cover, ▼ No warmth | A weak body armour; covers nearly everything; slows its wearer; useless in a blizzard. |
| **2.6** | 2.9 / 1.0 | Dreadspear Armor (T,M,F) | ↁ Elves (Continued) | medieval | steel | 3.0 / 2.1 / 2.5 / 2.3; social 6.1 | 3.3 / 0 | 3.0 / 2.5 | steel 2.6, plasteel 3.8, uranium 3.5 | ◆ Combat armour, ▼ Slows you, ▼ No warmth, ◆ Trader's, ▲ Good impression, ▲ Hard-wearing | A weak body armour; helps in talks and trade; makes a good impression; slows its wearer. |
| **2.6** | 2.9 / 1.0 | Spearman Armor (T,M,F) | ↁ Elves (Continued) | medieval | steel | 3.0 / 2.1 / 2.5 / 2.3; social 6.1 | 3.3 / 0 | 3.0 / 2.5 | steel 2.6, plasteel 3.8, uranium 3.5 | ◆ Combat armour, ▼ Slows you, ▼ No warmth, ◆ Trader's, ▲ Good impression, ▲ Hard-wearing | A weak body armour; helps in talks and trade; makes a good impression; slows its wearer. |
| **2.6** | 2.9 / 1.0 | Stag Knight Armor (T,M,F) | ↁ Elves (Continued) | medieval | steel | 3.0 / 2.1 / 2.5 / 2.3; social 6.1 | 3.3 / 0 | 3.0 / 2.5 | steel 2.6, plasteel 3.8, uranium 3.5 | ◆ Combat armour, ▼ Slows you, ▼ No warmth, ◆ Trader's, ▲ Good impression, ▲ Hard-wearing | A weak body armour; helps in talks and trade; makes a good impression; slows its wearer. |
| **2.6** |  | war tribalwear | Vanilla Factions Expanded - Tribals | neolithic | wood | 1.8 / 1.4 / 1.3 / 1.4 | 1 / 0 | 2.7 / 2.6 |  | ◆ Combat armour, ▼ Thin cover, ▲ Light, ⚠ Tattered mood, ▼ No warmth, ▼ Flammable | A weak body armour; for little weight; covers little; tatters after a few fights. |
| **2.4** |  | Black Guard Pauldrons (T,M,F) | ↁ Elves (Continued) | medieval | steel | 1.1 / 1.1 / 1.2 / 1.1; social 3.9 | 1.3 / 0 | 2.6 / 2.4 |  | ◆ Combat armour, ▼ Thin cover, ▲ Light, ▲ Steady nerves, ▼ No warmth, ▲ Good impression, ▲ Hard-wearing | A poor body armour; for little weight; steadies the nerves; covers little. |
| **2.4** |  | Dragon Prince Pauldrons (T,M,F) | ↁ Elves (Continued) | medieval | steel | 1.1 / 1.1 / 1.2 / 1.1; social 3.9 | 1.3 / 0 | 2.6 / 2.4 |  | ◆ Combat armour, ▼ Thin cover, ▲ Light, ▲ Steady nerves, ▼ No warmth, ▲ Good impression, ▲ Hard-wearing | A poor body armour; for little weight; steadies the nerves; covers little. |
| **2.3** | 2.4 / 1.7 | marine gloves | Vanilla Armour Expanded | spacer |  | 1.1 / 1.1 / 1.2 / 1.1 | 2 / 4 | 2.5 / 2.8 |  | ◆ Combat armour, ▼ Thin cover, ▲ Steadier aim, ▲ Light, ▼ No warmth, ● Advanced research | A poor body armour; steadies the aim; for little weight; covers little. |
| **2.1** |  | flak pants | Core | industrial |  | 1.5 / 1.4 / 1.2 / 1.3 | 3.5 / 1 | 2.6 / 2.2 |  | ◆ Combat armour, ▼ Slows you, ▼ Thin cover, ▼ Weak to blunt, ▼ No warmth | A poor body armour; slows its wearer; covers little; blunt blows get through. |
| **2.1** | 2.2 / 1.7 | wooden armor | Vanilla Armour Expanded | neolithic | wood | 1.9 / 1.4 / 1.0 / 1.5 | 1 / 0 | 2.2 / 2.1 |  | ◆ Combat armour, ▼ Slows you, ▼ Thin cover, ⚠ Tattered mood, ▼ No warmth, ▼ Flammable | A poor body armour; slows its wearer; covers little; tatters after a few fights. |
| **2.0** |  | marine boots | Vanilla Armour Expanded | spacer |  | 1.1 / 1.1 / 1.2 / 1.1 | 2 / 4 | 2.2 / 2.5 |  | ◆ Combat armour, ▼ Thin cover, ▲ Quick feet, ▲ Light, ▼ No warmth, ● Advanced research | A poor body armour; quickens the step; for little weight; covers little. |
| **1.9** |  | Stag Knight Pauldrons (T,M,F) | ↁ Elves (Continued) | medieval | steel | 1.2 / 1.2 / 1.2 / 1.1; social 3.9 | 1.6 / 0 | 2.2 / 1.9 | steel 1.9, plasteel 2.0, uranium 2.0 | ◆ Combat armour, ▼ Thin cover, ▲ Light, ▼ No warmth, ▲ Good impression, ▲ Hard-wearing | A poor body armour; for little weight; makes a good impression; covers little. |
| **1.7** |  | Shadow Warrior Pauldrons (T,M,F) | ↁ Elves (Continued) | medieval | steel | 1.1 / 1.1 / 1.1 / 1.1; social 3.9 | 1 / 0 | 1.9 / 1.7 |  | ◆ Combat armour, ▼ Thin cover, ▲ Light, ▼ No warmth, ▲ Good impression, ▲ Hard-wearing | A poor body armour; for little weight; makes a good impression; covers little. |
| **1.4** |  | plate shoulderpads | Vanilla Armour Expanded | medieval | steel | 1.1 / 1.2 / 1.2 / 1.1 | 0.3 / 0 | 1.5 / 1.4 | steel 1.4, plasteel 1.6, uranium 1.5 | ◆ Combat armour, ▼ Slows you, ▼ Thin cover, ▼ No warmth, ◆ Worn for looks | A poor body armour; slows its wearer; covers little; useless in a blizzard. |

### Helmets (75)

| Rating | Shooter / melee | Piece | Mod | Age | Stuff | Guns / melee / fire / mech | Cold / heat °C | In the cold / heat | By stuff | Tags | In plain words |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **9.0** |  | mech breaker helmet | Alpha Mechs | spacer |  | 6.5 / 6.5 / 4.7 / 6.1; vacuum 7.0, mechanitor 8.6 | 4 / 2 | 9.0 / 9.0 |  | ◆ Helmet, ● Mechanitor only, ✦ Vacuum-proof, ✦ Mech bandwidth, ◆ Royal, ▲ Light, ▼ Little warmth | A superb helmet; keeps its wearer alive in vacuum; more mechs for a mechanitor; little warmth. |
| **8.9** |  | mech master helmet | Alpha Mechs | spacer |  | 6.1 / 6.0 / 5.1 / 5.5; vacuum 7.0, mechanitor 8.2 | 4 / 2 | 8.9 / 8.9 |  | ◆ Helmet, ● Mechanitor only, ✦ Vacuum-proof, ✦ Mech bandwidth, ◆ Royal, ▲ Light, ▼ Little warmth | A superb helmet; keeps its wearer alive in vacuum; more mechs for a mechanitor; little warmth. |
| **8.7** *(rough)* | 8.2 / **8.7** | mechlord helmet | Biotech | spacer |  | 5.3 / 5.2 / 4.5 / 4.7; vacuum 7.0, mechanitor 7.5 | 4 / 2 | 8.7 / 8.7 |  | ◆ Helmet, ◆ For melee fighters, ▼ Spoils your aim, ● Mechanitor only, ✦ Vacuum-proof, ✦ Mech bandwidth, ▲ Light | A superb helmet for a melee fighter; keeps its wearer alive in vacuum; more mechs for a mechanitor; spoils the aim. |
| **8.6** *(rough)* | **8.6** / 7.7 | absolver helmet | Vanilla Factions Expanded - Empire | spacer |  | 6.1 / 6.0 / 5.1 / 5.5; vacuum 7.1, hunting 4.2 | 4 / 2 | 8.7 / 8.6 |  | ◆ Helmet, ◆ For shooters, ✦ Vacuum-proof, ▲ Steadier aim, ▲ Light, ◆ Spacefarer's, ▼ Little warmth | A superb helmet for a shooter; keeps its wearer alive in vacuum; steadies the aim; little warmth. |
| **8.5** |  | forsaken helmet | Alpha Genes | ultra |  | 6.7 / 7.0 / 6.6 / 6.6; vacuum 7.1, psychic shelter 6.5 | 8 / 6 | 8.6 / 8.6 |  | ◆ Helmet, ◆ For shooters, ▲ Turns blades, ▲ Warm, ✦ Vacuum-proof, ▲ Cool, ▼ Blocks psycasts | A superb helmet for a shooter; turns blades and claws; warm in a blizzard; dulls psycasts. |
| **8.4** | **8.4** / 7.4 | dropsuit helmet | Rimsenal: Murder diversified | medieval |  | 5.2 / 5.1 / 5.6 / 4.6; toxins 5.5, vacuum 7.1 | 10 / 5 | 8.5 / 8.5 |  | ◆ Helmet, ◆ For shooters, ▲ Warm, ✦ Vacuum-proof, ✦ Toxin-proof, ▲ Steadier aim, ▲ Light | An excellent helmet for a shooter; warm in a blizzard; keeps its wearer alive in vacuum; keeps toxins out. |
| **8.3** *(rough)* | **8.3** / 7.2 | deserter helmet | Vanilla Factions Expanded - Empire | spacer |  | 5.9 / 5.7 / 4.5 / 5.3; vacuum 7.1 | 4 / 2 | 8.4 / 8.3 |  | ◆ Helmet, ◆ For shooters, ✦ Vacuum-proof, ▲ Steadier aim, ▲ Light, ◆ Spacefarer's, ▼ Little warmth | An excellent helmet for a shooter; keeps its wearer alive in vacuum; steadies the aim; little warmth. |
| **8.3** |  | hazard helmet | Rimsenal: Murder diversified | spacer |  | 5.3 / 5.1 / 7.7 / 4.8; toxins 8.4, vacuum 7.2, psychic shelter 7.9 | 45 / 45 | 8.8 / 8.8 |  | ◆ Helmet, ▲ Warm, ✦ Vacuum-proof, ▲ Fireproof, ▲ Cool, ✦ Psychic shield, ▼ Blocks psycasts | An excellent helmet; warm in a blizzard; keeps its wearer alive in vacuum; dulls psycasts. |
| **8.3** *(rough)* |  | mechcommander helmet | Biotech | spacer |  | 4.0 / 3.7 / 3.7 / 3.3; vacuum 7.1, mechanitor 5.5 | 3 / 1.5 | 8.3 / 8.3 |  | ◆ Helmet, ● Mechanitor only, ✦ Vacuum-proof, ✦ Mech bandwidth, ▲ Light, ◆ Spacefarer's, ▼ Little warmth | An excellent helmet; keeps its wearer alive in vacuum; more mechs for a mechanitor; little warmth. |
| **8.2** |  | bladedancer helmet | ↁ Elves (Continued) | medieval | steel | 2.2 / 1.8 / 2.9 / 1.6 | 0.5 / 0 | 8.3 / 8.2 | steel 8.2, plasteel 8.3, uranium 8.3 | ◆ Helmet, ▲ Light, ▲ Shrugs off pain, ▲ Steady nerves, ▼ Little warmth | An excellent helmet; for little weight; keeps its wearer standing through pain; little warmth. |
| **8.2** |  | heavy crypto helmet | Vanilla Quests Expanded - Cryptoforge | ultra |  | 6.5 / 6.6 / 7.5 / 6.3; vacuum 7.0 | 16 / 2 | 8.5 / 8.2 |  | ◆ Helmet, ▲ Warm, ✦ Vacuum-proof, ▲ Fireproof, ✦ Stops bleeding, ◆ Royal, ▲ Light | An excellent helmet; warm in a blizzard; keeps its wearer alive in vacuum; shrugs off fire. |
| **8.0** |  | crypto helmet | Vanilla Quests Expanded - Cryptoforge | ultra |  | 6.1 / 6.0 / 7.3 / 5.6; vacuum 7.0 | 16 / 2 | 8.4 / 8.0 |  | ◆ Helmet, ▲ Warm, ✦ Vacuum-proof, ▲ Fireproof, ✦ Stops bleeding, ◆ Royal, ▲ Light | An excellent helmet; warm in a blizzard; keeps its wearer alive in vacuum; shrugs off fire. |
| **8.0** | **8.0** / 6.6 | nomad helmet | Rimsenal: Murder diversified | spacer |  | 4.4 / 4.2 / 4.1 / 3.7 | 10 / 7 | 8.3 / 8.2 |  | ◆ Helmet, ◆ For shooters, ▲ Warm, ▲ Cool, ▲ Steadier aim, ▲ Light | An excellent helmet for a shooter; warm in a blizzard; keeps its wearer cool; steadies the aim. |
| **8.0** |  | pioneer helmet | Rimsenal: Murder diversified | spacer |  | 5.1 / 4.8 / 6.6 / 4.5 | 7 / 2 | 8.2 / 8.0 |  | ◆ Helmet, ▲ Warm, ▲ Light, ▲ Shrugs off pain | An excellent helmet; warm in a blizzard; for little weight; keeps its wearer standing through pain. |
| **7.9** | 4.9 / **7.9** | close combat helmet | Rimsenal: Murder diversified | spacer |  | 5.2 / 5.3 / 6.8 / 4.6; toxins 5.5, vacuum 7.1 | 5 / 5 | 8.1 / 8.1 |  | ◆ Helmet, ◆ For melee fighters, ▼ Spoils your aim, ✦ Vacuum-proof, ✦ Toxin-proof, ◆ Royal, ▲ Light | An excellent helmet for a melee fighter; keeps its wearer alive in vacuum; keeps toxins out; spoils the aim. |
| **7.9** | **7.9** / 1.0 | gunlink | Royalty | spacer |  | 1.0 / 1.0 / 1.0 / 1.0 | 0 / 0 | 7.9 / 7.9 |  | ◆ Helmet, ◆ For shooters, ▲ Steadier aim, ▼ Little warmth, ● Biocodable | An excellent piece of combat headgear for a shooter; steadies the aim; little warmth. |
| **7.9** |  | Shadow Warrior Helmet | ↁ Elves (Continued) | medieval | steel | 3.9 / 4.3 / 4.7 / 3.4; psychic shelter 2.8, social 7.7, hunting 2.8 | 0.8 / 0 | 7.9 / 7.9 |  | ◆ Helmet, ✦ Psychic shield, ✦ Camouflage, ▲ Light, ▲ Melee edge, ▲ Shrugs off pain, ▼ Blocks psycasts | An excellent helmet; shields the mind from psychic attacks; hides its wearer; dulls psycasts. |
| **7.9** *(rough)* |  | techfriar crown | Vanilla Factions Expanded - Empire | spacer | steel | 2.3 / 2.2 / 4.1 / 1.7; vacuum 7.1, work 8.9 | 4 / 2 | 8.1 / 8.0 |  | ◆ Helmet, ✦ Vacuum-proof, ▲ Light, ◆ Work clothes, ◆ Spacefarer's, ✦ Its own effect, ▼ Little warmth | An excellent helmet; keeps its wearer alive in vacuum; for little weight; little warmth. |
| **7.8** | **7.8** / 4.6 | cap | Vanilla Factions Expanded - Medieval 2 | medieval | devilstrand | 1.4 / 1.2 / 3.5 / 1.2 | 2 / 12 | 7.8 / 8.2 | cloth 7.7, synthread 7.7, devilstrand 7.8, hyperweave 7.8, plainleather 7.7, bearskin 7.7, thrumbofur 7.8 | ◆ Helmet, ◆ For shooters, ▲ Cool, ▲ Steadier aim | An excellent helmet for a shooter; keeps its wearer cool; steadies the aim. |
| **7.8** | 1.0 / **7.8** | Demigyryph Champion Helm | pphhyy's Demigryphs Continued | medieval | steel | 6.2 / 6.2 / 6.3 / 5.7; social 5.1 | 0.5 / 0 | 7.8 / 7.8 |  | ◆ Helmet, ◆ For melee fighters, ▼ Spoils your aim, ▲ Light, ▲ Shrugs off pain, ▲ Good impression, ▲ Hard-wearing | An excellent helmet for a melee fighter; for little weight; keeps its wearer standing through pain; spoils the aim. |
| **7.8** |  | Demigyryph Hunter Helm | pphhyy's Demigryphs Continued | medieval | steel | 6.5 / 6.6 / 6.6 / 6.1; social 2.6 | 0.5 / 0 | 7.8 / 7.8 |  | ◆ Helmet, ▲ Light, ▲ Shrugs off pain, ▲ Good impression, ▲ Hard-wearing, ▼ Little warmth | An excellent helmet; for little weight; keeps its wearer standing through pain; little warmth. |
| **7.8** |  | forsaken hood | Alpha Genes | spacer |  | 4.4 / 4.2 / 3.2 / 3.9; vacuum 7.0, psychic shelter 4.2 | 12 / 4 | 8.2 / 7.9 |  | ◆ Helmet, ▲ Warm, ✦ Vacuum-proof, ✦ Psychic shield, ◆ Royal, ▲ Light, ▼ Blocks psycasts | An excellent helmet; warm in a blizzard; keeps its wearer alive in vacuum; dulls psycasts. |
| **7.8** *(rough)* |  | prestige siegebreaker helmet | Vanilla Armour Expanded | spacer |  | 6.5 / 6.5 / 4.7 / 6.1; vacuum 7.1, psychic 3.1 | 4 / 2 | 7.9 / 7.8 |  | ◆ Helmet, ✦ Vacuum-proof, ● Needs a psylink, ✦ Psychic, ◆ Royal, ▲ Light, ▼ Little warmth | An excellent helmet; keeps its wearer alive in vacuum; lifts a psycaster; little warmth. |
| **7.8** |  | Reflactor helmet | Rimsenal: Murder diversified | spacer |  | 5.5 / 5.5 / 8.0 / 5.0; toxins 5.5, vacuum 7.1 | 5 / 50 | 7.7 / 8.8 |  | ◆ Helmet, ✦ Vacuum-proof, ▲ Fireproof, ▲ Cool, ✦ Toxin-proof, ▲ Light, ◆ Spacefarer's | An excellent helmet; keeps its wearer alive in vacuum; shrugs off fire; keeps its wearer cool. |
| **7.8** |  | Witch Elf Crown | ↁ Elves (Continued) | medieval |  | 1.0 / 1.0 / 1.0 / 1.0; social 3.9 | 0 / 0 | 7.8 / 7.8 |  | ◆ Helmet, ◆ Royal, ▲ Steady nerves, ▲ Good impression, ▼ Little warmth | An excellent piece of combat headgear; fit for a title; steadies the nerves; little warmth. |
| **7.7** | 1.0 / **7.7** | Demigyryph Gilded Helm | pphhyy's Demigryphs Continued | medieval | steel | 6.2 / 6.2 / 6.3 / 5.7; social 6.8 | 0.5 / 0 | 7.7 / 7.7 | steel 7.7, plasteel 7.8, uranium 7.8 | ◆ Helmet, ◆ For melee fighters, ▼ Spoils your aim, ▲ Light, ▲ Shrugs off pain, ◆ Trader's, ▲ Good impression | An excellent helmet for a melee fighter; for little weight; keeps its wearer standing through pain; spoils the aim. |
| **7.7** | 1.0 / **7.7** | Demigyryph Raider Helm | pphhyy's Demigryphs Continued | medieval | steel | 6.2 / 6.2 / 6.3 / 5.7; social 7.5 | 0.5 / 0 | 7.8 / 7.7 | steel 7.7, plasteel 7.8, uranium 7.8 | ◆ Helmet, ◆ For melee fighters, ▼ Spoils your aim, ▲ Light, ▲ Shrugs off pain, ◆ Trader's, ▲ Hard-wearing | An excellent helmet for a melee fighter; for little weight; keeps its wearer standing through pain; spoils the aim. |
| **7.7** | **7.7** / 1.0 | fire support helmet | Rimsenal: Murder diversified | spacer |  | 4.6 / 4.3 / 6.2 / 4.0; toxins 5.5, vacuum 7.1 | 4 / 2 | 7.9 / 7.8 |  | ◆ Helmet, ◆ For shooters, ▼ Clumsy in melee, ▼ Slows you, ✦ Vacuum-proof, ✦ Toxin-proof, ▲ Steadier aim | An excellent helmet for a shooter; keeps its wearer alive in vacuum; keeps toxins out; clumsy in melee. |
| **7.6** *(rough)* |  | janissary helmet | Vanilla Factions Expanded - Empire | spacer |  | 5.3 / 5.2 / 4.5 / 4.7; toxins 8.6, vacuum 7.1 | 4 / 2 | 7.8 / 7.7 |  | ◆ Helmet, ✦ Vacuum-proof, ✦ Toxin-proof, ▲ Light, ◆ Spacefarer's, ✦ Its own effect, ▼ Little warmth | An excellent helmet; keeps its wearer alive in vacuum; keeps toxins out; little warmth. |
| **7.6** |  | prestige cataphract helmet | Royalty | spacer |  | 6.5 / 6.6 / 5.6 / 6.2; vacuum 7.1, psychic 3.1 | 4 / 2 | 7.8 / 7.7 |  | ◆ Helmet, ✦ Vacuum-proof, ● Needs a psylink, ✦ Psychic, ◆ Royal, ▲ Light, ▼ Little warmth | An excellent helmet; keeps its wearer alive in vacuum; lifts a psycaster; little warmth. |
| **7.6** *(rough)* |  | siegebreaker helmet | Vanilla Armour Expanded | spacer |  | 6.5 / 6.5 / 4.7 / 6.1; vacuum 7.1 | 4 / 2 | 7.8 / 7.7 |  | ◆ Helmet, ✦ Vacuum-proof, ▲ Light, ◆ Spacefarer's, ✦ Its own effect, ▼ Little warmth, ● Advanced research | An excellent helmet; keeps its wearer alive in vacuum; for little weight; little warmth. |
| **7.6** | 7.8 / 7.0 | strikesuit helmet | Rimsenal: Murder diversified | medieval |  | 4.2 / 3.9 / 3.6 / 3.5; toxins 5.5, vacuum 7.1 | 2 / 4 | 7.7 / 7.9 |  | ◆ Helmet, ✦ Vacuum-proof, ✦ Toxin-proof, ▲ Steadier aim, ▲ Light, ◆ Spacefarer's, ▼ Little warmth | An excellent helmet; keeps its wearer alive in vacuum; keeps toxins out; little warmth. |
| **7.5** |  | combat vacsuit helmet | Vanilla Gravship Expanded - Chapter 2 | spacer |  | 5.1 / 4.9 / 6.6 / 4.5; toxins 8.0, vacuum 7.0 | 6 / 4 | 7.9 / 7.7 |  | ◆ Helmet, ▲ Warm, ✦ Vacuum-proof, ✦ Toxin-proof, ▲ Light, ◆ Spacefarer's, ● Advanced research | An excellent helmet; warm in a blizzard; keeps its wearer alive in vacuum; keeps toxins out. |
| **7.5** |  | Elven Lord Helmet | ↁ Elves (Continued) | medieval | steel | 4.4 / 4.2 / 4.9 / 3.8; social 7.2 | 0.8 / 0 | 7.6 / 7.5 | steel 7.5, plasteel 7.7, uranium 7.6 | ◆ Helmet, ◆ Royal, ▲ Light, ▲ Steady nerves, ◆ Trader's, ▲ Good impression, ▼ Little warmth | An excellent helmet; fit for a title; for little weight; little warmth. |
| **7.5** |  | prestige marine helmet | Royalty | spacer |  | 6.1 / 6.0 / 5.1 / 5.5; vacuum 7.1, psychic 3.1 | 4 / 2 | 7.7 / 7.6 |  | ◆ Helmet, ✦ Vacuum-proof, ● Needs a psylink, ✦ Psychic, ◆ Royal, ▲ Light, ▼ Little warmth | An excellent helmet; keeps its wearer alive in vacuum; lifts a psycaster; little warmth. |
| **7.4** |  | assault helmet | Rimsenal: Murder diversified | spacer |  | 6.8 / 7.1 / 5.9 / 6.8; toxins 5.5, vacuum 7.1 | 5 / 2 | 7.8 / 7.5 |  | ◆ Helmet, ▲ Turns blades, ▼ Slows you, ✦ Vacuum-proof, ✦ Toxin-proof, ◆ Spacefarer's, ▲ Hard-wearing | A strong helmet; turns blades and claws; keeps its wearer alive in vacuum; slows its wearer. |
| **7.4** |  | cataphract helmet | Royalty | spacer |  | 6.5 / 6.6 / 5.6 / 6.2; vacuum 7.1 | 4 / 2 | 7.7 / 7.5 |  | ◆ Helmet, ✦ Vacuum-proof, ▲ Light, ◆ Spacefarer's, ▼ Little warmth, ● Advanced research, ● Biocodable | A strong helmet; keeps its wearer alive in vacuum; for little weight; little warmth. |
| **7.4** | 1.0 / **7.4** | Demigyryph Helm | pphhyy's Demigryphs Continued | medieval | steel | 5.2 / 5.1 / 5.6 / 4.6; social 3.9 | 0.5 / 0 | 7.5 / 7.4 | steel 7.4, plasteel 7.6, uranium 7.6 | ◆ Helmet, ◆ For melee fighters, ▼ Spoils your aim, ▲ Light, ▲ Shrugs off pain, ▲ Good impression, ▲ Hard-wearing | A strong helmet for a melee fighter; for little weight; keeps its wearer standing through pain; spoils the aim. |
| **7.4** | 7.6 / 6.7 | marksmangear helmet | Rimsenal Faction Pack - Federation | spacer |  | 4.4 / 3.9 / 2.6 / 3.6; vacuum 7.0 | 5 / 5 | 7.7 / 7.7 |  | ◆ Helmet, ✦ Vacuum-proof, ▲ Steadier aim, ▼ Weak to blunt, ▲ Light, ▼ Off-putting, ◆ Spacefarer's | A strong helmet; keeps its wearer alive in vacuum; steadies the aim; blunt blows get through. |
| **7.4** |  | Medic helmet | Rimsenal: Murder diversified | spacer |  | 5.1 / 4.8 / 6.6 / 4.5; work 8.1 | 7 / 2 | 7.9 / 7.5 |  | ◆ Helmet, ▼ Slows you, ▲ Warm, ◆ Work clothes, ◆ Medic's | A strong helmet; warm in a blizzard; speeds up work; slows its wearer. |
| **7.4** |  | prestige vacsuit helmet | Vanilla Gravship Expanded - Chapter 1 | spacer |  | 3.0 / 2.6 / 5.9 / 2.4; toxins 8.0, vacuum 7.0, psychic 3.1 | 6 / 4 | 7.8 / 7.7 |  | ◆ Helmet, ▲ Warm, ✦ Vacuum-proof, ● Needs a psylink, ✦ Psychic, ✦ Toxin-proof, ◆ Royal | A strong helmet; warm in a blizzard; keeps its wearer alive in vacuum; lifts a psycaster. |
| **7.3** |  | Paragon's Helm | A RimWorld of Magic | medieval | steel | 5.4 / 5.4 / 5.6 / 4.7; social 5.1, magic 2.8 | 1.6 / 1 | 7.5 / 7.4 | steel 7.3, plasteel 7.5, uranium 7.5 | ◆ Helmet, ● Needs a mage, ✦ Enchanted, ▲ Steadier aim, ▲ Light, ▲ Melee edge, ▼ Little warmth | A strong helmet; enchanted for a mage; steadies the aim; little warmth. |
| **7.3** |  | prestige recon helmet | Royalty | spacer |  | 5.3 / 5.2 / 4.5 / 4.7; vacuum 7.0, psychic 3.1 | 4 / 2 | 7.6 / 7.5 |  | ◆ Helmet, ✦ Vacuum-proof, ● Needs a psylink, ✦ Psychic, ◆ Royal, ▲ Light, ▼ Little warmth | A strong helmet; keeps its wearer alive in vacuum; lifts a psycaster; little warmth. |
| **7.3** *(rough)* |  | prestige trooper helmet | Vanilla Armour Expanded | spacer |  | 3.1 / 2.7 / 3.3 / 2.7; vacuum 6.8, psychic 3.1 | 2 / 4 | 7.4 / 7.6 |  | ◆ Helmet, ✦ Vacuum-proof, ● Needs a psylink, ✦ Psychic, ◆ Royal, ▲ Light, ▼ Little warmth | A strong helmet; keeps its wearer alive in vacuum; lifts a psycaster; little warmth. |
| **7.3** |  | riot gear helmet | Rimsenal Faction Pack - Federation | spacer |  | 6.5 / 6.3 / 6.2 / 6.2; vacuum 7.1 | 5 / 5 | 7.7 / 7.7 |  | ◆ Helmet, ✦ Vacuum-proof, ▼ Weak to blunt, ▲ Light, ▼ Off-putting, ◆ Spacefarer's, ▲ Hard-wearing | A strong helmet; keeps its wearer alive in vacuum; for little weight; blunt blows get through. |
| **7.2** |  | marine helmet | Core | spacer |  | 6.1 / 6.0 / 5.1 / 5.5; vacuum 7.1 | 4 / 2 | 7.6 / 7.4 |  | ◆ Helmet, ✦ Vacuum-proof, ▲ Light, ◆ Spacefarer's, ▼ Little warmth, ● Advanced research, ● Biocodable | A strong helmet; keeps its wearer alive in vacuum; for little weight; little warmth. |
| **7.2** | **7.2** / 1.0 | smart visor | Rimsenal Faction Pack - Spacer | spacer |  | 1.0 / 1.0 / 1.0 / 1.0 | 0 / 0 | 7.2 / 7.2 |  | ◆ Helmet, ◆ For shooters, ▲ Steadier aim, ▼ Little warmth, ● Biocodable | A strong piece of combat headgear for a shooter; steadies the aim; little warmth. |
| **7.2** *(rough)* |  | vacsuit helmet | Odyssey | spacer |  | 3.0 / 2.6 / 5.9 / 2.4; toxins 8.0, vacuum 7.1 | 6 / 4 | 7.7 / 7.5 |  | ◆ Helmet, ▲ Warm, ✦ Vacuum-proof, ✦ Toxin-proof, ▲ Light, ◆ Spacefarer's, ✦ Its own effect | A strong helmet; warm in a blizzard; keeps its wearer alive in vacuum; keeps toxins out. |
| **7.1** |  | recon helmet | Core | spacer |  | 5.3 / 5.2 / 4.5 / 4.7; vacuum 7.0 | 4 / 2 | 7.4 / 7.2 |  | ◆ Helmet, ✦ Vacuum-proof, ▲ Light, ◆ Spacefarer's, ▼ Little warmth, ● Advanced research, ● Biocodable | A strong helmet; keeps its wearer alive in vacuum; for little weight; little warmth. |
| **7.1** |  | ritual mask | Anomaly | neolithic | bioferrite | 1.9 / 1.6 / 2.2 / 1.4 | 0.5 / 0 | 7.1 / 7.1 |  | ◆ Helmet, ▲ Shrugs off pain, ▼ Little warmth | A strong helmet; keeps its wearer standing through pain; little warmth. |
| **7.0** |  | elven armored hood | ↁ Elves (Continued) | medieval | devilstrand | 5.3 / 4.9 / 8.0 / 4.8; hunting 2.0 | 3 / 7.2 | 7.2 / 7.6 | cloth 5.0, synthread 6.3, devilstrand 7.0, hyperweave 7.4, plainleather 6.0, bearskin 6.6, thrumbofur 7.4 | ◆ Helmet, ▲ Fireproof, ▲ Cool, ▼ Weak to blunt, ▲ Light, ◆ Hunter's, ● Stuff matters | A strong helmet; shrugs off fire; keeps its wearer cool; blunt blows get through. |
| **7.0** |  | feral exosuit helmet | Rimsenal Faction Pack - Feral | spacer |  | 5.8 / 5.8 / 3.3 / 5.2; vacuum 7.0 | 3 / 5 | 7.3 / 7.5 |  | ◆ Helmet, ▼ Slows you, ✦ Vacuum-proof, ◆ Spacefarer's, ● Advanced research | A strong helmet; keeps its wearer alive in vacuum; slows its wearer. |
| **7.0** *(rough)* |  | trooper helmet | Vanilla Armour Expanded | spacer |  | 3.1 / 2.7 / 3.3 / 2.7; vacuum 6.8 | 2 / 4 | 7.2 / 7.4 |  | ◆ Helmet, ✦ Vacuum-proof, ▲ Light, ▲ Steady nerves, ◆ Spacefarer's, ✦ Its own effect, ▼ Little warmth | A strong helmet; keeps its wearer alive in vacuum; for little weight; little warmth. |
| **6.9** |  | Helm of the Valiant | A RimWorld of Magic | medieval | steel | 4.3 / 4.1 / 4.5 / 3.6; social 5.1, magic 2.0 | 1.3 / 1 | 7.0 / 7.0 | steel 6.9, plasteel 7.1, uranium 7.0 | ◆ Helmet, ● Needs a mage, ✦ Enchanted, ▲ Steadier aim, ▲ Light, ▲ Melee edge, ▼ Little warmth | A strong helmet; enchanted for a mage; steadies the aim; little warmth. |
| **6.8** | 7.0 / 6.4 | tactical helmet | Rimsenal - Augmented Vanilla Pack | medieval |  | 4.9 / 4.5 / 1.9 / 4.4; work 2.8 | 1 / 0 | 7.0 / 6.8 |  | ◆ Helmet, ▲ Steadier aim, ▼ Weak to blunt, ▲ Light, ▼ Little warmth | A strong helmet; steadies the aim; for little weight; blunt blows get through. |
| **6.8** |  | unity guard helmet | Rimsenal Faction Pack - Federation | spacer |  | 6.2 / 5.9 / 3.3 / 5.7 | 5 / 5 | 7.3 / 7.3 |  | ◆ Helmet, ▼ Weak to blunt, ▲ Light, ▼ Off-putting, ● Advanced research | A strong helmet; for little weight; blunt blows get through; puts people off. |
| **6.5** |  | Black Guard Helmet | ↁ Elves (Continued) | medieval | steel | 3.9 / 3.6 / 4.4 / 3.2; social 7.2 | 0.8 / 0 | 6.6 / 6.4 | steel 6.5, plasteel 6.8, uranium 6.7 | ◆ Helmet, ◆ Royal, ▲ Light, ◆ Trader's, ▲ Good impression, ▼ Little warmth | A strong helmet; fit for a title; for little weight; little warmth. |
| **6.5** |  | Cold One Knight Helmet | ↁ Elves (Continued) | medieval | steel | 3.9 / 3.6 / 4.4 / 3.2; social 7.2 | 0.8 / 0 | 6.6 / 6.4 | steel 6.5, plasteel 6.8, uranium 6.7 | ◆ Helmet, ◆ Royal, ▲ Light, ◆ Trader's, ▲ Good impression, ▼ Little warmth | A strong helmet; fit for a title; for little weight; little warmth. |
| **6.5** |  | Dragon Prince Helmet | ↁ Elves (Continued) | medieval | steel | 3.9 / 3.6 / 4.4 / 3.2; social 7.2 | 0.8 / 0 | 6.6 / 6.4 | steel 6.5, plasteel 6.8, uranium 6.7 | ◆ Helmet, ◆ Royal, ▲ Light, ◆ Trader's, ▲ Good impression, ▼ Little warmth | A strong helmet; fit for a title; for little weight; little warmth. |
| **6.5** |  | security helmet | Rimsenal: Murder diversified | spacer |  | 4.9 / 4.9 / 4.5 / 4.3 | 7 / 2 | 7.4 / 6.7 |  | ◆ Helmet, ▲ Warm, ▲ Light | A strong helmet; warm in a blizzard; for little weight. |
| **6.5** |  | White Lion Helmet | ↁ Elves (Continued) | medieval | steel | 3.9 / 3.6 / 4.4 / 3.2; social 7.2 | 0.8 / 0 | 6.6 / 6.4 | steel 6.5, plasteel 6.8, uranium 6.7 | ◆ Helmet, ▲ Light, ◆ Trader's, ▲ Good impression, ▼ Little warmth | A strong helmet; for little weight; helps in talks and trade; little warmth. |
| **6.0** |  | legionnaire helmet | Vanilla Factions Expanded - Classical | medieval | steel | 3.6 / 3.2 / 4.1 / 2.9 | 2.7 / 0 | 6.7 / 5.9 | steel 6.0, plasteel 6.5, uranium 6.4, wood 5.4 | ◆ Helmet, ▲ Light, ▲ Steady nerves, ▼ Little warmth | A solid helmet; for little weight; steadies the nerves; little warmth. |
| **5.9** | 6.1 / 5.1 | heavy flak helmet | Rimsenal - Augmented Vanilla Pack | medieval |  | 6.0 / 5.8 / 3.8 / 5.4 | 5 / 5 | 6.9 / 6.9 |  | ◆ Helmet, ▼ Slows you, ▲ Hard-wearing | A solid helmet; slows its wearer. |
| **5.9** |  | MOPP mask(Desert) | Dubs Rimatomics | industrial |  | 2.6 / 2.3 / 2.9 / 2.2; toxins 4.7, psychic shelter 6.8 | 4 / 0 | 6.9 / 5.7 |  | ◆ Helmet, ▼ Slows you, ✦ Psychic shield, ▼ Blocks psycasts, ▼ Off-putting, ▼ Little warmth | A solid helmet; shields the mind from psychic attacks; slows its wearer; dulls psycasts. |
| **5.9** |  | MOPP mask(Snow) | Dubs Rimatomics | industrial |  | 2.6 / 2.3 / 2.9 / 2.2; toxins 4.7, psychic shelter 6.8 | 4 / 0 | 6.9 / 5.7 |  | ◆ Helmet, ▼ Slows you, ✦ Psychic shield, ▼ Blocks psycasts, ▼ Off-putting, ▼ Little warmth | A solid helmet; shields the mind from psychic attacks; slows its wearer; dulls psycasts. |
| **5.9** |  | MOPP mask(Woodland) | Dubs Rimatomics | industrial |  | 2.6 / 2.3 / 2.9 / 2.2; toxins 4.7, psychic shelter 6.8 | 4 / 0 | 6.9 / 5.7 |  | ◆ Helmet, ▼ Slows you, ✦ Psychic shield, ▼ Blocks psycasts, ▼ Off-putting, ▼ Little warmth | A solid helmet; shields the mind from psychic attacks; slows its wearer; dulls psycasts. |
| **5.6** |  | centurion helmet | Vanilla Factions Expanded - Classical | medieval | steel | 3.8 / 3.5 / 4.4 / 3.1 | 2.9 / 0 | 6.5 / 5.4 | steel 5.6, plasteel 6.2, uranium 6.1, wood 4.7 | ◆ Helmet, ▲ Light, ▼ Little warmth, ● Stuff matters | A solid helmet; for little weight; little warmth. |
| **5.5** |  | Dreadspear Helmet | ↁ Elves (Continued) | medieval | steel | 2.9 / 2.5 / 3.6 / 2.1; social 6.1 | 0.5 / 0 | 5.7 / 5.5 | steel 5.5, plasteel 6.0, uranium 5.9 | ◆ Helmet, ▲ Light, ◆ Trader's, ▲ Good impression, ▼ Little warmth | A solid helmet; for little weight; helps in talks and trade; little warmth. |
| **5.5** |  | Spearman Helmet | ↁ Elves (Continued) | medieval | steel | 2.9 / 2.5 / 3.6 / 2.1; social 6.1 | 0.5 / 0 | 5.7 / 5.5 | steel 5.5, plasteel 6.0, uranium 5.9 | ◆ Helmet, ▲ Light, ◆ Trader's, ▲ Good impression, ▼ Little warmth | A solid helmet; for little weight; helps in talks and trade; little warmth. |
| **4.9** |  | riot helmet | Vanilla Armour Expanded | industrial |  | 2.3 / 3.0 / 3.6 / 1.7 | 4 / 2 | 6.4 / 5.6 |  | ◆ Helmet, ▲ Light, ▼ Little warmth | A fair helmet; for little weight; little warmth. |
| **4.3** |  | flak helmet | Core | industrial | steel | 2.6 / 2.3 / 2.8 / 2.2 | 0.5 / 0 | 4.6 / 4.3 | steel 4.3, plasteel 4.9, uranium 4.8 | ◆ Helmet, ▲ Light, ▼ Little warmth, ◆ Worn for looks | A modest helmet; for little weight; little warmth. |
| **3.9** | 4.1 / 3.1 | feral helmet | Rimsenal Faction Pack - Feral | industrial |  | 3.8 / 3.6 / 1.0 / 3.0; psychic shelter 4.2 | 0 / 0 | 3.9 / 3.9 |  | ◆ Helmet, ▼ Slows you, ✦ Psychic shield, ▼ Blocks psycasts, ▼ Off-putting, ▼ Little warmth | A modest helmet; shields the mind from psychic attacks; slows its wearer; dulls psycasts. |
| **3.5** |  | simple helmet | Core | industrial | steel | 2.0 / 1.7 / 2.3 / 1.5 | 0.5 / 0 | 3.9 / 3.4 | steel 3.5, plasteel 4.1, uranium 3.9 | ◆ Helmet, ▲ Light, ▼ Little warmth, ◆ Worn for looks | A modest helmet; for little weight; little warmth. |
| **1.0** |  | heavy leather helmet | Vanilla Factions Expanded - Medieval 2 | medieval | plainleather | 2.8 / 2.3 / 7.2 / 2.1 | 32 / 0 | 8.2 / 1.0 | plainleather 1.0, bearskin 3.9, thrumbofur 7.2 | ◆ Helmet, ◆ For melee fighters, ▼ Spoils your aim, ▲ Warm, ▲ Fireproof, ▼ Clumsy in melee, ▼ Weak to blunt | A poor helmet for a melee fighter; warm in a blizzard; shrugs off fire; spoils the aim. |
| **1.0** |  | heavy plate helmet | Vanilla Factions Expanded - Medieval 2 | medieval | steel | 2.9 / 2.5 / 3.6 / 2.1 | 0.5 / 0 | 1.0 / 1.0 |  | ◆ Helmet, ◆ For melee fighters, ▼ Spoils your aim, ▼ Clumsy in melee, ▲ Light, ▼ Little warmth | A poor helmet for a melee fighter; for little weight; spoils the aim; clumsy in melee. |

### Shields carried in the off hand (18)

| Rating | Shooter / melee | Piece | Mod | Age | Stuff | Guns / melee / fire / mech | Cold / heat °C | In the cold / heat | By stuff | Tags | In plain words |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **4.9** | 1.9 / **4.9** | Demigyryph Banner | pphhyy's Demigryphs Continued | medieval | devilstrand | 3.2 / 2.3 / 4.1 / 2.5 | 0 / 0 | 4.9 / 4.9 |  | ◆ Off-hand shield, ◆ For melee fighters, ▼ Spoils your aim, ● Light weapon only, ▲ Light, ▲ Shrugs off pain | A fair shield for a melee fighter; for little weight; keeps its wearer standing through pain; spoils the aim. |
| **4.9** | 1.9 / **4.9** | Demigyryph Champion Banner | pphhyy's Demigryphs Continued | medieval | devilstrand | 3.2 / 2.3 / 4.1 / 2.5 | 0 / 0 | 4.9 / 4.9 |  | ◆ Off-hand shield, ◆ For melee fighters, ▼ Spoils your aim, ● Light weapon only, ▲ Light, ▲ Shrugs off pain | A fair shield for a melee fighter; for little weight; keeps its wearer standing through pain; spoils the aim. |
| **4.9** | 1.9 / **4.9** | Demigyryph Gilded Banner | pphhyy's Demigryphs Continued | medieval | devilstrand | 3.2 / 2.3 / 4.1 / 2.5 | 0 / 0 | 4.9 / 4.9 |  | ◆ Off-hand shield, ◆ For melee fighters, ▼ Spoils your aim, ● Light weapon only, ▲ Light, ▲ Shrugs off pain | A fair shield for a melee fighter; for little weight; keeps its wearer standing through pain; spoils the aim. |
| **4.9** | 1.9 / **4.9** | Demigyryph Hunter Banner | pphhyy's Demigryphs Continued | medieval | devilstrand | 3.2 / 2.3 / 4.1 / 2.5 | 0 / 0 | 4.9 / 4.9 |  | ◆ Off-hand shield, ◆ For melee fighters, ▼ Spoils your aim, ● Light weapon only, ▲ Light, ▲ Shrugs off pain | A fair shield for a melee fighter; for little weight; keeps its wearer standing through pain; spoils the aim. |
| **4.9** | 1.9 / **4.9** | Demigyryph Raider Banner | pphhyy's Demigryphs Continued | medieval | devilstrand | 3.2 / 2.3 / 4.1 / 2.5 | 0 / 0 | 4.9 / 4.9 |  | ◆ Off-hand shield, ◆ For melee fighters, ▼ Spoils your aim, ● Light weapon only, ▲ Light, ▲ Shrugs off pain | A fair shield for a melee fighter; for little weight; keeps its wearer standing through pain; spoils the aim. |
| **4.4** | 1.0 / **4.4** | heavy shield | Vanilla Factions Expanded - Classical | neolithic | plainleather | 4.4 / 3.2 / 5.6 / 3.3 | 0 / 0 | 4.4 / 4.4 |  | ◆ Off-hand shield, ◆ For melee fighters, ▼ Spoils your aim, ▼ Slows you, ● Light weapon only, ▲ Full cover, ▼ Clumsy in melee | A modest shield for a melee fighter; covers nearly everything; spoils the aim; slows its wearer. |
| **3.2** | 1.0 / **3.2** | Demigyryph Champion Shield | pphhyy's Demigryphs Continued | medieval | steel | 3.2 / 2.3 / 4.1 / 2.5 | 0 / 0 | 3.2 / 3.2 |  | ◆ Off-hand shield, ◆ For melee fighters, ▼ Spoils your aim, ▼ Slows you, ● Light weapon only, ▲ Shrugs off pain, ▲ Hard-wearing | A weak shield for a melee fighter; keeps its wearer standing through pain; spoils the aim; slows its wearer. |
| **3.1** | 1.0 / **3.1** | heater shield | Vanilla Factions Expanded - Medieval 2 | medieval |  | 3.1 / 1.8 / 3.7 / 2.4 | 0 / 0 | 3.1 / 3.1 |  | ◆ Off-hand shield, ◆ For melee fighters, ▼ Spoils your aim, ● Light weapon only, ▼ Clumsy in melee, ▲ Light, ⚠ Tattered mood | A weak shield for a melee fighter; for little weight; spoils the aim; clumsy in melee. |
| **2.7** | 1.0 / **2.7** | Demigyryph Gilded Shield | pphhyy's Demigryphs Continued | medieval | steel | 3.2 / 2.3 / 4.1 / 2.5; social 6.8 | 0 / 0 | 2.7 / 2.7 |  | ◆ Off-hand shield, ◆ For melee fighters, ▼ Spoils your aim, ▼ Slows you, ● Light weapon only, ◆ Trader's, ▲ Good impression | A weak shield for a melee fighter; helps in talks and trade; makes a good impression; spoils the aim. |
| **2.6** | 1.0 / **2.6** | dragon shield | ↁ Elves (Continued) | medieval | steel | 2.1 / 1.0 / 1.8 / 1.4 | 0 / 0 | 2.6 / 2.6 | steel 2.6, plasteel 3.3, uranium 3.1 | ◆ Off-hand shield, ◆ For melee fighters, ● Light weapon only, ▼ Clumsy in melee, ▲ Light | A weak shield for a melee fighter; for little weight; clumsy in melee. |
| **2.6** | 1.0 / **2.6** | elven shield | ↁ Elves (Continued) | medieval | steel | 2.1 / 1.0 / 1.8 / 1.4 | 0 / 0 | 2.6 / 2.6 | wood 1.5, steel 2.6, plasteel 3.3, uranium 3.1 | ◆ Off-hand shield, ◆ For melee fighters, ● Light weapon only, ▼ Clumsy in melee, ▲ Light, ● Stuff matters | A weak shield for a melee fighter; for little weight; clumsy in melee. |
| **2.6** | 1.0 / **2.6** | elven shield | ↁ Elves (Continued) | medieval | steel | 2.1 / 1.0 / 1.8 / 1.4 | 0 / 0 | 2.6 / 2.6 | wood 1.5, steel 2.6, plasteel 3.3, uranium 3.1 | ◆ Off-hand shield, ◆ For melee fighters, ● Light weapon only, ▼ Clumsy in melee, ▲ Light, ● Stuff matters | A weak shield for a melee fighter; for little weight; clumsy in melee. |
| **2.6** | 1.0 / **2.6** | elven tower shield | ↁ Elves (Continued) | medieval | steel | 2.1 / 1.0 / 1.8 / 1.4 | 0 / 0 | 2.6 / 2.6 | wood 1.5, steel 2.6, plasteel 3.3, uranium 3.1 | ◆ Off-hand shield, ◆ For melee fighters, ● Light weapon only, ▼ Clumsy in melee, ▲ Light, ● Stuff matters | A weak shield for a melee fighter; for little weight; clumsy in melee. |
| **2.3** | 1.0 / **2.3** | Demigyryph Hunter Shield | pphhyy's Demigryphs Continued | medieval | steel | 3.2 / 2.3 / 4.1 / 2.5 | 0 / 0 | 2.3 / 2.3 |  | ◆ Off-hand shield, ◆ For melee fighters, ▼ Spoils your aim, ▼ Slows you, ● Light weapon only, ▲ Hard-wearing | A poor shield for a melee fighter; spoils the aim; slows its wearer. |
| **2.3** | 1.0 / **2.3** | Demigyryph Raider Shield | pphhyy's Demigryphs Continued | medieval | steel | 3.2 / 2.3 / 4.1 / 2.5 | 0 / 0 | 2.3 / 2.3 |  | ◆ Off-hand shield, ◆ For melee fighters, ▼ Spoils your aim, ▼ Slows you, ● Light weapon only, ▲ Hard-wearing | A poor shield for a melee fighter; spoils the aim; slows its wearer. |
| **2.3** | 1.0 / **2.3** | Demigyryph Shield | pphhyy's Demigryphs Continued | medieval | steel | 3.2 / 2.3 / 4.1 / 2.5 | 0 / 0 | 2.3 / 2.3 |  | ◆ Off-hand shield, ◆ For melee fighters, ▼ Spoils your aim, ▼ Slows you, ● Light weapon only | A poor shield for a melee fighter; spoils the aim; slows its wearer. |
| **2.3** | 1.0 / **2.3** | riot shield | Vanilla Armour Expanded | industrial |  | 2.3 / 1.9 / 6.8 / 1.6 | 0 / 0 | 2.3 / 2.3 |  | ◆ Off-hand shield, ◆ For melee fighters, ▼ Spoils your aim, ● Light weapon only, ▲ Full cover, ▼ Clumsy in melee, ▲ Light | A poor shield for a melee fighter; covers nearly everything; for little weight; spoils the aim. |
| **1.0** |  | round shield | Vanilla Factions Expanded - Medieval 2 | medieval |  | 1.6 / 1.0 / 2.5 / 1.1 | 0 / 0 | 1.0 / 1.0 |  | ◆ Off-hand shield, ◆ For melee fighters, ▼ Spoils your aim, ● Light weapon only, ▼ Clumsy in melee, ⚠ Tattered mood | A poor shield for a melee fighter; spoils the aim; clumsy in melee; tatters after a few fights. |

### Utility: belts, packs and accessories (87)

| Rating | Shooter / melee | Piece | Mod | Age | Stuff | Sub-ratings | Cold / heat °C | In the cold / heat | By stuff | Tags | In plain words |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **8.9** |  | mech crypto-pack | Alpha Mechs | spacer |  | guns 1.0, mechanitor 8.3 | 0 / 0 | 8.9 / 8.9 |  | ◆ Utility, ● Mechanitor only, ✦ Mech bandwidth, ● Advanced research, ● Belt slot | A superb utility piece; more mechs for a mechanitor. |
| **8.8** |  | voidlink pack | Alpha Mechs | spacer |  | guns 1.0, mechanitor 8.2 | 0 / 0 | 8.8 / 8.8 |  | ◆ Utility, ● Mechanitor only, ✦ Mech bandwidth, ● Advanced research, ● Belt slot | A superb utility piece; more mechs for a mechanitor. |
| **8.7** |  | cerebrex node | Odyssey | spacer |  | guns 1.0, mechanitor 8.0 | 0 / 0 | 8.7 / 8.7 |  | ◆ Utility, ● Hard to find, ● Mechanitor only, ✦ Mech bandwidth, ✦ Its own effect, ● Belt slot | A superb utility piece; more mechs for a mechanitor. |
| **8.5** |  | beamcaster pack | Alpha Mechs | spacer |  | guns 1.0, mechanitor 7.6 | 0 / 0 | 8.5 / 8.5 |  | ◆ Utility, ● Mechanitor only, ✦ Mech bandwidth, ● Advanced research, ● Belt slot | A superb utility piece; more mechs for a mechanitor. |
| **8.1** *(rough)* |  | bandwidth pack | Biotech | spacer |  | guns 1.0, mechanitor 6.8 | 0 / 0 | 8.1 / 8.1 |  | ◆ Utility, ● Mechanitor only, ✦ Mech bandwidth, ✦ Its own effect, ● Belt slot | An excellent utility piece; more mechs for a mechanitor. |
| **8.0** *(rough)* |  | Bracers of the Pacifist | A RimWorld of Magic | medieval | devilstrand | guns 1.2, work 3.7, magic 6.9 | 1 / 1.2 | 8.0 / 8.0 |  | ◆ Utility, ● Needs a mage, ✦ Enchanted, ▲ Steady nerves, ◆ Researcher's, ✦ Its own effect, ● Artifact slot | An excellent utility piece; enchanted for a mage; steadies the nerves; for a researcher. |
| **7.9** *(rough)* | 6.9 / **7.9** | astrorig | Vanilla Gravship Expanded - Chapter 2 | spacer |  | guns 2.8, toxins 7.0, vacuum 6.3 | 0 / 0 | 7.9 / 7.9 |  | ◆ Utility, ◆ For melee fighters, ✦ Jump, ✦ Vacuum-proof, ✦ Toxin-proof, ◆ Spacefarer's, ✦ Its own effect | An excellent utility piece for a melee fighter; jumps over walls and enemies; keeps its wearer alive in vacuum; keeps toxins out. |
| **7.6** |  | EMP grenade belt | Vanilla Weapons Expanded - Grenades | industrial |  | guns 1.0 | 0 / 0 | 7.6 / 7.6 |  | ◆ Utility, ✦ Grants: EMP grenade, ● Needs grenade emp, ● Belt slot | An excellent utility piece; its EMP grenade at hand. |
| **7.6** |  | EMP surge pack | Rimsenal Xenotype Pack - Askbarn | spacer |  | guns 1.0 | 0 / 0 | 7.6 / 7.6 |  | ◆ Utility, ✦ Grants: EMP surge, ● Needs steel, ● Belt slot | An excellent utility piece; its EMP surge at hand. |
| **7.6** *(rough)* |  | gravship bombardment targeter | Vanilla Gravship Expanded - Chapter 2 | spacer |  | guns 1.0 | 0 / 0 | 7.6 / 7.6 |  | ◆ Utility, ✦ Orbital strike, ✦ Its own effect, ● Advanced research, ● Belt slot | An excellent utility piece; calls a strike from orbit. |
| **7.6** | 1.0 / **7.6** | shield belt | Core | spacer |  | guns 7.2 | 0 / 0 | 7.6 / 7.6 |  | ◆ Utility, ◆ For melee fighters, ▼ Blocks shooting, ✦ Shield, ▲ Stops bullets, ● Advanced research, ● Belt slot | An excellent utility piece for a melee fighter; takes shots and blasts on its shield; stops most bullets; its wearer can't shoot out. |
| **7.4** | **7.4** / 6.7 | Cat Eye Pendant | A RimWorld of Magic | medieval |  | guns 1.0, work 8.4 | 0 / 0 | 7.4 / 7.4 |  | ◆ Utility, ◆ For shooters, ● Hard to find, ◆ Work clothes, ● Artifact slot | A strong utility piece for a shooter; speeds up work. |
| **7.2** | 4.3 / **7.2** | jump pack | Royalty | industrial |  | guns 2.8 | 0 / 0 | 7.2 / 7.2 |  | ◆ Utility, ◆ For melee fighters, ✦ Jump, ● Needs chemfuel, ● Belt slot | A strong utility piece for a melee fighter; jumps over walls and enemies. |
| **7.1** |  | ranged shield belt | Vanilla Apparel Expanded - Accessories | medieval |  | guns 6.0 | 0 / 0 | 7.1 / 7.1 |  | ◆ Utility, ✦ Shield, ● Advanced research, ● Belt slot | A strong utility piece; takes shots and blasts on its shield. |
| **6.7** |  | battle banner | Vanilla Apparel Expanded - Accessories | neolithic | devilstrand | guns 1.0 | 0 / 0 | 6.7 / 6.7 |  | ◆ Utility, ▲ Shrugs off pain, ● Belt slot | A strong utility piece; keeps its wearer standing through pain. |
| **6.7** |  | Ring of Keen Intellect | A RimWorld of Magic | medieval |  | guns 1.0, psychic 4.5, work 6.5, magic 2.2 | 0 / 0 | 6.7 / 6.7 |  | ◆ Utility, ● Needs a mage, ✦ Enchanted, ● Needs a psylink, ✦ Psychic, ◆ Work clothes, ◆ Researcher's | A strong utility piece; enchanted for a mage; lifts a psycaster; speeds up work. |
| **6.6** |  | resurrector belt | Vanilla Apparel Expanded - Accessories | ultra |  | guns 1.0 | 0 / 0 | 6.6 / 6.6 |  | ◆ Utility, ✦ Second life, ⚠ Runs out, ● Belt slot | A strong utility piece; brings its wearer back from death once; gone once its charges are spent. |
| **6.4** |  | orbital bombardment targeter | Core | spacer |  | guns 1.0 | 0 / 0 | 6.4 / 6.4 |  | ◆ Utility, ● Hard to find, ✦ Orbital strike, ⚠ Runs out, ● Belt slot | A solid utility piece; calls a strike from orbit; gone once its charges are spent. |
| **6.4** |  | orbital power beam targeter | Core | spacer |  | guns 1.0 | 0 / 0 | 6.4 / 6.4 |  | ◆ Utility, ● Hard to find, ✦ Orbital strike, ⚠ Runs out, ● Belt slot | A solid utility piece; calls a strike from orbit; gone once its charges are spent. |
| **6.4** |  | psychic insanity lance | Core | archotech |  | guns 1.0 | 0 / 0 | 6.4 / 6.4 |  | ◆ Utility, ● Hard to find, ✦ Psychic lance, ⚠ Runs out, ⚠ Angers factions, ● Belt slot | A solid utility piece; takes one enemy out from afar; gone once its charges are spent; angers its target's faction. |
| **6.4** |  | shard insanity lance | Anomaly | archotech |  | guns 1.0 | 0 / 0 | 6.4 / 6.4 |  | ◆ Utility, ✦ Psychic lance, ⚠ Runs out, ⚠ Angers factions, ● Belt slot | A solid utility piece; takes one enemy out from afar; gone once its charges are spent; angers its target's faction. |
| **6.4** |  | tornado generator | Core | spacer |  | guns 1.0 | 0 / 0 | 6.4 / 6.4 |  | ◆ Utility, ● Hard to find, ✦ Orbital strike, ⚠ Runs out, ● Belt slot | A solid utility piece; calls a strike from orbit; gone once its charges are spent. |
| **6.3** | **6.3** / 1.0 | ammo pack | Vanilla Apparel Expanded - Accessories | industrial |  | guns 1.0 | 0 / 0 | 6.3 / 6.3 |  | ◆ Utility, ◆ For shooters, ▲ Steadier aim, ● Belt slot | A solid utility piece for a shooter; steadies the aim. |
| **6.2** |  | Ring of Labor | A RimWorld of Magic | medieval |  | guns 1.0, work 6.5, carrying 6.5 | 0 / 0 | 6.2 / 6.2 |  | ◆ Utility, ● Hard to find, ◆ Work clothes, ▲ Carries more, ● Artifact slot | A solid utility piece; speeds up work; carries more. |
| **6.1** *(rough)* |  | Ring of the Eternal Blue | A RimWorld of Magic | medieval |  | guns 1.0, psychic 6.3 | 0 / 0 | 6.1 / 6.1 |  | ◆ Utility, ● Needs a psylink, ✦ Psychic, ✦ Its own effect, ● Artifact slot | A solid utility piece; lifts a psycaster. |
| **5.9** |  | low-shield pack | Royalty | industrial |  | guns 4.0 | 0 / 0 | 5.9 / 5.9 |  | ◆ Utility, ✦ Low shield, ✦ Shields allies, ⚠ Runs out, ● Advanced research, ● Belt slot | A solid utility piece; a low shield for a stand; gone once its charges are spent. |
| **5.8** |  | oxygen pack | Vanilla Gravship Expanded - Chapter 1 | industrial |  | guns 1.0, toxins 7.0, vacuum 6.3 | 0 / 0 | 5.8 / 5.8 |  | ◆ Utility, ✦ Vacuum-proof, ✦ Toxin-proof, ◆ Spacefarer's, ● Needs oxygen canister, ● Advanced research, ● Belt slot | A solid utility piece; keeps its wearer alive in vacuum; keeps toxins out. |
| **5.6** *(rough)* |  | mini-turret pack | Vanilla Apparel Expanded - Accessories | industrial |  | guns 1.0 | 0 / 0 | 5.6 / 5.6 |  | ◆ Utility, ✦ Extra gun, ✦ Its own effect, ● Belt slot | A solid utility piece; a gun of its own beside its wearer. |
| **5.3** |  | frag grenade belt | Vanilla Weapons Expanded - Grenades | industrial |  | guns 1.0 | 0 / 0 | 5.3 / 5.3 |  | ◆ Utility, ✦ Grants: frag grenade, ● Needs grenade frag, ● Belt slot | A fair utility piece; its frag grenade at hand. |
| **5.3** |  | psychic shock lance | Core | archotech |  | guns 1.0 | 0 / 0 | 5.3 / 5.3 |  | ◆ Utility, ● Hard to find, ✦ Psychic lance, ⚠ Runs out, ⚠ Angers factions, ● Belt slot | A fair utility piece; takes one enemy out from afar; gone once its charges are spent; angers its target's faction. |
| **5.3** |  | shard shock lance | Anomaly | archotech |  | guns 1.0 | 0 / 0 | 5.3 / 5.3 |  | ◆ Utility, ✦ Psychic lance, ⚠ Runs out, ⚠ Angers factions, ● Belt slot | A fair utility piece; takes one enemy out from afar; gone once its charges are spent; angers its target's faction. |
| **4.9** | **4.9** / 1.0 | quiver | Vanilla Apparel Expanded - Accessories | neolithic |  | guns 1.0 | 0 / 0 | 4.9 / 4.9 |  | ◆ Utility, ◆ For shooters, ▲ Steadier aim, ● Belt slot | A fair utility piece for a shooter; steadies the aim. |
| **4.6** |  | backpack | Vanilla Apparel Expanded - Accessories | neolithic | devilstrand | guns 1.0, carrying 7.8 | 0 / 0 | 4.6 / 4.6 |  | ◆ Utility, ▲ Carries more, ● Belt slot | A fair utility piece; carries more. |
| **4.6** |  | exoskeleton frame | Vanilla Weapons Expanded - Heavy Weapons | industrial |  | guns 1.0, work 6.1, carrying 4.4 | 0 / 0 | 4.6 / 4.6 |  | ◆ Utility, ▲ Quick feet, ◆ Work clothes, ▲ Carries more, ● Belt slot | A fair utility piece; quickens the step; speeds up work; carries more. |
| **4.3** *(rough)* | 4.0 / 4.9 | Quicksilver Pendant | A RimWorld of Magic | medieval |  | guns 1.0, work 5.1 | 0 / 0 | 4.3 / 4.3 |  | ◆ Utility, ● Hard to find, ▲ Quick feet, ▲ Melee edge, ◆ Work clothes, ✦ Its own effect, ● Artifact slot | A modest utility piece; quickens the step; sharpens a fighter; speeds up work. |
| **4.1** |  | medic bag | Vanilla Apparel Expanded - Accessories | industrial |  | guns 1.0, work 7.4 | 0 / 0 | 4.1 / 4.1 |  | ◆ Utility, ◆ Work clothes, ◆ Medic's, ● Belt slot | A modest utility piece; speeds up work; for the colony's doctor. |
| **3.9** |  | tool belt | Vanilla Apparel Expanded - Accessories | industrial |  | guns 1.0, work 7.1 | 0 / 0 | 3.9 / 3.9 |  | ◆ Utility, ◆ Work clothes, ◆ Builder's, ● Belt slot | A modest utility piece; speeds up work; for a builder. |
| **3.8** |  | control pack | Biotech | spacer |  | guns 1.0, mechanitor 2.2 | 0 / 0 | 3.8 / 3.8 |  | ◆ Utility, ● Mechanitor only, ✦ Mech bandwidth, ● Belt slot | A modest utility piece; more mechs for a mechanitor. |
| **3.7** |  | deadlife pack | Anomaly | industrial |  | guns 1.0 | 0 / 0 | 3.7 / 3.7 |  | ◆ Utility, ✦ Summons help, ⚠ Runs out, ● Belt slot | A modest utility piece; brings help of its own; gone once its charges are spent. |
| **3.7** |  | hunter pack | Odyssey | industrial |  | guns 1.0 | 0 / 0 | 3.7 / 3.7 |  | ◆ Utility, ✦ Summons help, ⚠ Runs out, ● Belt slot | A modest utility piece; brings help of its own; gone once its charges are spent. |
| **3.7** |  | tox pack | Biotech | industrial |  | guns 1.0 | 0 / 0 | 3.7 / 3.7 |  | ◆ Utility, ✦ Grants: tox pack, ● Needs chemfuel, ● Belt slot | A modest utility piece; its tox pack at hand. |
| **3.7** |  | turret pack | Anomaly | industrial |  | guns 1.0 | 0 / 0 | 3.7 / 3.7 |  | ◆ Utility, ✦ Summons help, ⚠ Runs out, ● Belt slot | A modest utility piece; brings help of its own; gone once its charges are spent. |
| **3.5** *(rough)* |  | Ring of Authority | A RimWorld of Magic | medieval |  | guns 1.0, social 7.1 | 0 / 0 | 3.5 / 3.5 |  | ◆ Utility, ● Hard to find, ◆ Trader's, ▲ Good impression, ✦ Its own effect, ● Artifact slot | A modest utility piece; helps in talks and trade; makes a good impression. |
| **3.5** | 3.3 / 4.3 | smokepop pack | Core | industrial |  | guns 2.6 | 0 / 0 | 3.5 / 3.5 |  | ◆ Utility, ✦ Smoke, ● Needs chemfuel, ● Belt slot | A modest utility piece; smoke on demand. |
| **3.2** |  | Ring of Knowledge | A RimWorld of Magic | medieval |  | guns 1.0 | 0 / 0 | 3.2 / 3.2 |  | ◆ Utility, ● Hard to find, ● Needs a mage, ✦ Enchanted, ● Artifact slot | A weak utility piece; enchanted for a mage. |
| **3.0** | 3.5 / 1.0 | arrow quiver | ↁ Elves (Continued) | neolithic |  | guns 1.0 | 0 / 0 | 3.0 / 3.0 |  | ◆ Utility, ▲ Steadier aim, ● Belt slot | A weak utility piece; steadies the aim. |
| **3.0** | 3.5 / 1.0 | bolt quiver | ↁ Elves (Continued) | neolithic |  | guns 1.0 | 0 / 0 | 3.0 / 3.0 |  | ◆ Utility, ▲ Steadier aim, ● Belt slot | A weak utility piece; steadies the aim. |
| **2.8** |  | medic bag | Field Medic [1.0-1.6] | industrial |  | guns 1.0, work 4.9 | 0 / 0 | 2.8 / 2.8 |  | ◆ Utility, ◆ Work clothes, ● Belt slot | A weak utility piece; speeds up work. |
| **2.8** *(rough)* |  | Ring of the Pathfinder | A RimWorld of Magic | medieval |  | guns 1.0, work 4.9 | 0 / 0 | 2.8 / 2.8 |  | ◆ Utility, ● Hard to find, ◆ Work clothes, ✦ Its own effect, ● Artifact slot | A weak utility piece; speeds up work. |
| **2.7** *(rough)* |  | Bracers of Defense | A RimWorld of Magic | medieval | steel | guns 1.3 | 0.2 / 0 | 2.8 / 2.7 | steel 2.7, plasteel 3.1, uranium 3.0 | ◆ Utility, ● Needs a mage, ✦ Enchanted, ✦ Its own effect, ● Artifact slot | A weak utility piece; enchanted for a mage. |
| **2.5** *(rough)* |  | Bracers of Deflection | A RimWorld of Magic | medieval | steel | guns 1.3 | 0.2 / 0 | 2.5 / 2.4 | steel 2.5, plasteel 2.8, uranium 2.7 | ◆ Utility, ● Needs a mage, ✦ Enchanted, ✦ Its own effect, ● Artifact slot | A weak utility piece; enchanted for a mage. |
| **2.3** *(rough)* |  | Charm of Health | A RimWorld of Magic | medieval |  | guns 1.0 | 0 / 0 | 2.3 / 2.3 |  | ◆ Utility, ● Hard to find, ▲ Heals faster, ✦ Its own effect, ● Artifact slot | A poor utility piece; heals its wearer faster. |
| **2.3** |  | molotov belt | Vanilla Weapons Expanded - Grenades | industrial |  | guns 1.0 | 0 / 0 | 2.3 / 2.3 |  | ◆ Utility, ✦ Grants: throw molotov, ● Needs grenade molotov, ● Belt slot | A poor utility piece; its throw molotov at hand. |
| **1.7** |  | firefoam pop pack | Core | industrial |  | guns 1.0 | 0 / 0 | 1.7 / 1.7 |  | ◆ Utility, ✦ Firefoam, ● Needs chemfuel, ● Belt slot | A poor utility piece; puts fires out. |
| **1.4** |  | heat pearls | Vanilla Psycasts Expanded | spacer |  | guns 1.0 | 0 / 0 | 1.4 / 1.4 |  | ◆ Utility, ● Hard to find, ✦ Grants: heat pearl, ⚠ Runs out, ● Belt slot | A poor utility piece; its heat pearl at hand; gone once its charges are spent. |
| **1.4** |  | torch belt | Vanilla Factions Expanded - Medieval 2 | neolithic |  | guns 1.0 | 0 / 0 | 1.4 / 1.4 |  | ◆ Utility, ✦ Grants: throw torch, ● Needs wood log, ● Belt slot | A poor utility piece; its throw torch at hand. |
| **1.3** *(rough)* |  | Amulet of Protection | A RimWorld of Magic | medieval |  | guns 1.0 | 0 / 0 | 1.3 / 1.3 |  | ◆ Utility, ● Hard to find, ✦ Wards the body, ✦ Its own effect, ◆ Worn for looks, ● Artifact slot | A poor utility piece; wards the whole body. |
| **1.2** *(rough)* |  | Amulet of Minor Protection | A RimWorld of Magic | medieval |  | guns 1.0 | 0 / 0 | 1.2 / 1.2 |  | ◆ Utility, ● Hard to find, ✦ Wards the body, ✦ Its own effect, ◆ Worn for looks, ● Artifact slot | A poor utility piece; wards the whole body. |
| **1.0** *(rough)* |  | Apricorn Ball belt | PokéWorld Updated [1.6] [ALPHA] | neolithic |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ⚠ Runs out, ✦ Its own effect, ● Belt slot | A poor utility piece; gone once its charges are spent. |
| **1.0** *(rough)* |  | biomutation lance | Anomaly | archotech |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ⚠ Runs out, ✦ Its own effect, ● Belt slot | A poor utility piece; gone once its charges are spent. |
| **1.0** *(rough)* |  | bomb pack | Vanilla Factions Expanded - Deserters | spacer |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ● Hard to find, ✦ Its own effect, ◆ Worn for looks, ● Belt slot | A poor utility piece. |
| **1.0** *(rough)* |  | Charm of Rest | A RimWorld of Magic | medieval |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ● Hard to find, ✦ Its own effect, ◆ Worn for looks, ● Artifact slot | A poor utility piece. |
| **1.0** *(rough)* |  | deserter declassifier | Vanilla Factions Expanded - Deserters | spacer |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ● Hard to find, ✦ Its own effect, ◆ Worn for looks, ● Belt slot | A poor utility piece. |
| **1.0** |  | Devilstrand Lasso | Melee Animation | neolithic |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ✦ Its own effect, ● Hip slot | A poor utility piece. |
| **1.0** *(rough)* |  | disruptor flare pack | Anomaly | industrial |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ✦ Its own effect, ● Needs bioferrite, ● Belt slot | A poor utility piece. |
| **1.0** |  | explosive belt | Vanilla Apparel Expanded - Accessories | industrial |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ⚠ Explodes on death, ◆ Worn for looks, ● Belt slot | A poor utility piece; blows up when its wearer dies. |
| **1.0** *(rough)* |  | fortification pack | Rimsenal - Security Pack | spacer |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ✦ Its own effect, ◆ Worn for looks, ● Belt slot | A poor utility piece. |
| **1.0** *(rough)* |  | Great Ball belt | PokéWorld Updated [1.6] [ALPHA] | industrial |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ⚠ Runs out, ✦ Its own effect, ● Belt slot | A poor utility piece; gone once its charges are spent. |
| **1.0** |  | Hyperweave Lasso | Melee Animation | industrial |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ✦ Its own effect, ● Hip slot | A poor utility piece. |
| **1.0** *(rough)* |  | invisibility engulfer | Vanilla Factions Expanded - Deserters | spacer |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ● Hard to find, ✦ Its own effect, ◆ Worn for looks, ● Belt slot | A poor utility piece. |
| **1.0** |  | Lasso | Melee Animation | neolithic |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ✦ Its own effect, ● Hip slot | A poor utility piece. |
| **1.0** *(rough)* |  | Master Ball belt | PokéWorld Updated [1.6] [ALPHA] | ultra |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ● Hard to find, ⚠ Runs out, ✦ Its own effect, ● Belt slot | A poor utility piece; gone once its charges are spent. |
| **1.0** *(rough)* |  | Mind Ward Pendant | A RimWorld of Magic | medieval |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ● Hard to find, ✦ Its own effect, ◆ Worn for looks, ● Artifact slot | A poor utility piece. |
| **1.0** *(rough)* |  | Necrotic Orb | A RimWorld of Magic | medieval |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ⚠ Wears fast, ✦ Its own effect, ◆ Worn for looks, ● Artifact slot | A poor utility piece; wears out fast. |
| **1.0** *(rough)* |  | omnifoam pack | Rimsenal Faction Pack - Spacer | spacer |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ✦ Its own effect, ● Needs omnifoam, ● Advanced research, ● Belt slot | A poor utility piece. |
| **1.0** *(rough)* |  | Orb Of Conviction | A RimWorld of Magic | medieval |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ● Hard to find, ✦ Its own effect, ● Artifact slot | A poor utility piece. |
| **1.0** *(rough)* |  | Orb of Souls (empty) | A RimWorld of Magic | medieval |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ✦ Its own effect, ● Artifact slot | A poor utility piece. |
| **1.0** *(rough)* |  | Orb of Souls (filled) | A RimWorld of Magic | medieval |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ● Hard to find, ✦ Its own effect, ● Artifact slot | A poor utility piece. |
| **1.0** *(rough)* |  | orbital mech cluster targeter | Royalty | spacer |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ● Hard to find, ⚠ Calls enemies, ⚠ Runs out, ✦ Its own effect, ● Belt slot | A poor utility piece; calls enemies down; gone once its charges are spent. |
| **1.0** *(rough)* |  | Pendant of Mending | A RimWorld of Magic | medieval |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ● Hard to find, ✦ Its own effect, ◆ Worn for looks, ● Artifact slot | A poor utility piece. |
| **1.0** *(rough)* |  | Poké Ball belt | PokéWorld Updated [1.6] [ALPHA] | industrial |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ⚠ Runs out, ✦ Its own effect, ● Belt slot | A poor utility piece; gone once its charges are spent. |
| **1.0** *(rough)* |  | Ring of Arcane Recovery | A RimWorld of Magic | medieval |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ● Hard to find, ✦ Its own effect, ◆ Worn for looks, ● Artifact slot | A poor utility piece. |
| **1.0** *(rough)* |  | Ring of Blood | A RimWorld of Magic | medieval |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ✦ Its own effect, ◆ Worn for looks, ● Artifact slot | A poor utility piece. |
| **1.0** *(rough)* |  | Ring of Malice | A RimWorld of Magic | medieval |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ✦ Its own effect, ◆ Worn for looks, ● Artifact slot | A poor utility piece. |
| **1.0** *(rough)* |  | skip beacon | MigCorp - Skiptech | ultra |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ✦ Its own effect, ◆ Worn for looks, ● Advanced research, ● Belt slot | A poor utility piece. |
| **1.0** *(rough)* |  | tackle box | Vanilla Ideology Expanded - Memes and Structures | industrial |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ✦ Its own effect, ◆ Worn for looks, ● Belt slot | A poor utility piece. |
| **1.0** *(rough)* |  | Ultra Ball belt | PokéWorld Updated [1.6] [ALPHA] | industrial |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Utility, ⚠ Runs out, ✦ Its own effect, ● Belt slot | A poor utility piece; gone once its charges are spent. |

### Cold-weather gear (10)

| Rating | Shooter / melee | Piece | Mod | Age | Stuff | Sub-ratings | Cold / heat °C | In the cold / heat | By stuff | Tags | In plain words |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **7.4** |  | warm jacket | Vanilla Christmas Expanded | neolithic | devilstrand | guns 1.3 | 44 / 0 | 8.8 / 1.1 | cloth 6.9, synthread 7.6, devilstrand 7.4, hyperweave 8.1 | ◆ Cold-weather, ▲ Warm, ▼ Won't stop a bullet, ◆ Coat, ◆ Top | A strong piece of cold-weather gear; warm in a blizzard; won't stop a bullet. |
| **7.1** |  | parka | Core | neolithic | devilstrand | guns 1.3 | 40 / 0 | 8.6 / 1.1 | cloth 6.6, synthread 7.3, devilstrand 7.1, hyperweave 7.9, plainleather 6.2, bearskin 7.0, thrumbofur 8.4 | ◆ Cold-weather, ▲ Warm, ▼ Won't stop a bullet, ◆ Coat, ◆ Top, ● Stuff matters | A strong piece of cold-weather gear; warm in a blizzard; won't stop a bullet. |
| **4.6** | 4.8 / 4.1 | straitjacket | Vanilla Anomaly Expanded - Insanity | medieval | devilstrand | guns 1.3 | 32 / 4.8 | 7.8 / 1.0 | cloth 3.9, synthread 5.0, devilstrand 4.6, hyperweave 6.4, plainleather 3.3, bearskin 4.5, thrumbofur 7.5 | ◆ Cold-weather, ▼ Slows you, ▲ Warm, ▼ Won't stop a bullet, ◆ For slaves, ◆ Coat, ● Stuff matters | A fair piece of cold-weather gear; warm in a blizzard; slows its wearer; won't stop a bullet. |
| **4.3** |  | heavy tribalwear | Vanilla Factions Expanded - Tribals | neolithic | devilstrand | guns 1.8 | 17 / 6 | 6.7 / 2.5 | cloth 3.5, synthread 4.2, devilstrand 4.3, hyperweave 5.8, plainleather 3.3, bearskin 4.0, thrumbofur 6.7 | ◆ Cold-weather, ▲ Warm, ▼ Won't stop a bullet, ◆ Full outfit, ● Stuff matters | A modest piece of cold-weather gear; warm in a blizzard; won't stop a bullet. |
| **4.2** |  | jacket | Core | medieval | devilstrand | guns 1.8 | 16 / 7.2 | 6.5 / 2.7 | cloth 3.3, synthread 4.1, devilstrand 4.2, hyperweave 5.6, plainleather 3.2, bearskin 3.9, thrumbofur 6.5 | ◆ Cold-weather, ▼ Won't stop a bullet, ◆ Coat, ◆ Top, ● Stuff matters | A modest piece of cold-weather gear; won't stop a bullet. |
| **4.2** |  | prestige robe | Royalty | medieval | devilstrand | guns 1.5, toxins 3.1 | 16 / 6 | 6.5 / 2.5 | cloth 3.6, synthread 4.2, devilstrand 4.2, hyperweave 5.5, plainleather 3.3, bearskin 4.0, thrumbofur 6.4 | ◆ Cold-weather, ◆ Royal, ▼ Won't stop a bullet, ◆ Coat, ● Stuff matters | A modest piece of cold-weather gear; fit for a title; won't stop a bullet. |
| **4.1** |  | stellic robe | Vanilla Factions Expanded - Empire | medieval | devilstrand | guns 1.5 | 17 / 6.7 | 6.6 / 2.4 | cloth 3.5, synthread 4.2, devilstrand 4.1, hyperweave 5.4, plainleather 3.2, bearskin 3.9, thrumbofur 6.4 | ◆ Cold-weather, ◆ Royal, ▼ Won't stop a bullet, ◆ Coat, ● Stuff matters | A modest piece of cold-weather gear; fit for a title; won't stop a bullet. |
| **3.9** |  | robe | Core | medieval | devilstrand | guns 1.5 | 16 / 6 | 6.4 / 2.3 | cloth 3.3, synthread 4.0, devilstrand 3.9, hyperweave 5.2, plainleather 3.1, bearskin 3.7, thrumbofur 6.2 | ◆ Cold-weather, ▼ Won't stop a bullet, ◆ Faith's apparel, ◆ Coat, ● Stuff matters | A modest piece of cold-weather gear; a faith asks for it; won't stop a bullet. |
| **3.5** |  | federation jumpsuit | Rimsenal Faction Pack - Federation | spacer |  | guns 1.1 | 15 / 5 | 5.9 / 1.9 |  | ◆ Cold-weather, ▲ Warm, ▼ Won't stop a bullet, ◆ Full outfit | A modest piece of cold-weather gear; warm in a blizzard; won't stop a bullet. |
| **3.4** |  | pelt coat | Vanilla Apparel Expanded | neolithic | plainleather | guns 1.3 | 14.4 / 1.6 | 5.8 / 1.4 | plainleather 3.4, bearskin 4.3, thrumbofur 7.1 | ◆ Cold-weather, ▼ Won't stop a bullet, ◆ Coat, ◆ Full outfit, ● Stuff matters | A weak piece of cold-weather gear; won't stop a bullet. |

### Hot-weather gear (3)

| Rating | Shooter / melee | Piece | Mod | Age | Stuff | Sub-ratings | Cold / heat °C | In the cold / heat | By stuff | Tags | In plain words |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **6.5** |  | cape | Core | medieval | devilstrand | guns 2.2 | 12 / 20.4 | 4.9 / 8.0 | cloth 4.5, synthread 5.7, devilstrand 6.5, hyperweave 7.4, plainleather 4.3, bearskin 5.5, thrumbofur 7.2 | ◆ Hot-weather, ▲ Fireproof, ▲ Cool, ◆ Royal, ▼ Won't stop a bullet, ◆ Faith's apparel, ● Stuff matters | A strong piece of hot-weather gear; shrugs off fire; keeps its wearer cool; won't stop a bullet. |
| **6.5** |  | duster | Core | medieval | devilstrand | guns 2.2 | 12 / 20.4 | 4.9 / 8.0 | cloth 4.5, synthread 5.7, devilstrand 6.5, hyperweave 7.4, plainleather 4.3, bearskin 5.5, thrumbofur 7.2 | ◆ Hot-weather, ▲ Fireproof, ▲ Cool, ▼ Won't stop a bullet, ◆ Coat, ◆ Full outfit, ● Stuff matters | A strong piece of hot-weather gear; shrugs off fire; keeps its wearer cool; won't stop a bullet. |
| **5.5** |  | light tribalwear | Vanilla Factions Expanded - Tribals | neolithic | devilstrand | guns 1.2 | 5 / 20.4 | 2.2 / 7.8 | cloth 4.2, synthread 5.0, devilstrand 5.5, hyperweave 6.1, plainleather 3.8, bearskin 4.7, thrumbofur 5.5 | ◆ Hot-weather, ▲ Cool, ▼ Won't stop a bullet, ⚠ Counts as naked, ◆ Top, ● Stuff matters | A solid piece of hot-weather gear; keeps its wearer cool; won't stop a bullet. |

### Vacuum and hazard suits (5)

| Rating | Shooter / melee | Piece | Mod | Age | Stuff | Sub-ratings | Cold / heat °C | In the cold / heat | By stuff | Tags | In plain words |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **7.2** |  | HAZMAT suit | Vanilla Armour Expanded | industrial | devilstrand | guns 1.0, toxins 8.4, psychic shelter 7.9 | 40 / 48 | 8.0 / 8.2 | cloth 7.0, synthread 7.2, devilstrand 7.2, hyperweave 7.4 | ◆ Hazard gear, ▼ Slows you, ▲ Warm, ▲ Cool, ✦ Psychic shield, ✦ Toxin-proof, ▼ Blocks psycasts | A strong hazard suit; warm in a blizzard; keeps its wearer cool; slows its wearer. |
| **6.1** | 6.3 / 5.7 | prestige vacsuit | Vanilla Gravship Expanded - Chapter 1 | spacer |  | guns 2.8, vacuum 4.7, psychic 3.1 | 90 / 15 | 8.6 / 4.7 |  | ◆ Hazard gear, ▼ Slows you, ▲ Warm, ◆ Made as armour, ▲ Cool, ● Needs a psylink, ✦ Psychic | A solid hazard suit; warm in a blizzard; keeps its wearer cool; slows its wearer. |
| **5.9** | 6.1 / 5.4 | vacsuit | Odyssey | spacer |  | guns 2.8, vacuum 4.7 | 90 / 15 | 8.6 / 4.4 |  | ◆ Hazard gear, ▼ Slows you, ▲ Warm, ◆ Made as armour, ▲ Cool, ◆ Spacefarer's | A solid hazard suit; warm in a blizzard; keeps its wearer cool; slows its wearer. |
| **1.9** |  | radiation suit | Dubs Rimatomics | industrial |  | guns 1.0, toxins 4.7 | 15 / 0 | 4.1 / 1.0 |  | ◆ Hazard gear, ▼ Slows you, ▼ Slows work, ▼ Off-putting, ▼ Won't stop a bullet, ◆ Coat, ◆ Full outfit | A poor hazard suit; slows its wearer; slows work; puts people off. |
| **1.8** | 2.0 / 1.3 | rumsuit | Vanilla Factions Expanded - Pirates | industrial |  | guns 1.0, vacuum 4.5 | 12 / 0 | 3.6 / 1.1 |  | ◆ Hazard gear, ▼ Slows you, ◆ Spacefarer's, ▼ Won't stop a bullet, ◆ Coat | A poor hazard suit; slows its wearer; won't stop a bullet. |

### Everyday clothing (85)

| Rating | Shooter / melee | Piece | Mod | Age | Stuff | Sub-ratings | Cold / heat °C | In the cold / heat | By stuff | Tags | In plain words |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **9.2** |  | elven lord cloak (T,M,F) | ↁ Elves (Continued) | medieval | devilstrand | guns 1.0 | 13 / 13.2 | 9.2 / 9.2 |  | ◆ Armoured cloak, ▲ Melee edge, ▲ Shrugs off pain, ▲ Steady nerves, ◆ Cloak | A superb armoured cloak; sharpens a fighter; keeps its wearer standing through pain; steadies the nerves. |
| **9.1** |  | Mantle of Protection | A RimWorld of Magic | medieval | devilstrand | guns 1.0, toxins 3.5, work 6.5, social 6.6, magic 2.6 | 25 / 10.8 | 9.3 / 9.1 |  | ◆ Everyday wear, ● Needs a mage, ▲ Warm, ✦ Enchanted, ▲ Steady nerves, ◆ Work clothes, ◆ Trader's | A superb everyday garment; warm in a blizzard; enchanted for a mage; steadies the nerves. |
| **8.9** |  | Black Guard Cloak(T,M,F) | ↁ Elves (Continued) | medieval | devilstrand | guns 1.0 | 13 / 13.2 | 9.0 / 9.0 |  | ◆ Everyday wear, ▲ Shrugs off pain, ▲ Steady nerves, ◆ Cloak | A superb cloak; keeps its wearer standing through pain; steadies the nerves. |
| **8.9** |  | Cold One Knight Cloak (T,M,F) | ↁ Elves (Continued) | medieval | devilstrand | guns 1.0 | 13 / 13.2 | 9.0 / 9.0 |  | ◆ Everyday wear, ▲ Shrugs off pain, ▲ Steady nerves, ◆ Cloak | A superb cloak; keeps its wearer standing through pain; steadies the nerves. |
| **8.9** |  | industrial exo-frame | Rimsenal - Augmented Vanilla Pack | medieval |  | guns 1.5, work 8.5 | 18 / 0 | 9.2 / 8.6 |  | ◆ Everyday wear, ▲ Quick feet, ◆ Work clothes, ▼ Won't stop a bullet, ▼ Flammable, ◆ Coat, ● Advanced research | A superb everyday garment; quickens the step; speeds up work; won't stop a bullet. |
| **8.9** |  | wood elven cloak (T,M,F) | ↁ Elves (Continued) | medieval | devilstrand | guns 1.0, hunting 2.8 | 13 / 13.2 | 9.0 / 9.0 | cloth 8.8, synthread 8.9, devilstrand 8.9, hyperweave 9.0, plainleather 8.8, bearskin 8.9, thrumbofur 9.0 | ◆ Everyday wear, ▲ Shrugs off pain, ▲ Steady nerves, ◆ Hunter's, ◆ Cloak | A superb cloak; keeps its wearer standing through pain; steadies the nerves; for a hunter. |
| **8.8** |  | White Lion Pelt (T,M,F) | ↁ Elves (Continued) | medieval | devilstrand | guns 1.0, hunting 2.0 | 13 / 13.2 | 9.0 / 9.0 |  | ◆ Everyday wear, ▲ Melee edge, ▲ Shrugs off pain, ▲ Steady nerves, ◆ Hunter's, ◆ Cloak | A superb cloak; sharpens a fighter; keeps its wearer standing through pain; steadies the nerves. |
| **8.7** |  | spacer bodysuit | Rimsenal: Murder diversified | spacer |  | guns 1.4 | 30 / 25 | 9.0 / 8.9 |  | ◆ Everyday wear, ▲ Warm, ▲ Cool, ▼ Won't stop a bullet, ◆ Full outfit | A superb everyday garment; warm in a blizzard; keeps its wearer cool; won't stop a bullet. |
| **8.7** |  | Witch undergarment (F) | ↁ Elves (Continued) | medieval | devilstrand | guns 1.6, social 5.1 | 7 / 3.6 | 8.8 / 8.7 | cloth 8.6, synthread 8.7, devilstrand 8.7, hyperweave 8.8 | ◆ Everyday wear, ● Women only, ▲ Melee edge, ▲ Steady nerves, ▼ Won't stop a bullet, ▲ Good impression, ⚠ Wrong-gender mood | A superb everyday garment; sharpens a fighter; steadies the nerves; won't stop a bullet. |
| **8.6** |  | general's insignia | Vanilla Quests Expanded - Deadlife | industrial |  | guns 1.0 | 0 / 0 | 8.6 / 8.6 |  | ◆ Everyday wear, ● Hard to find, ▲ Steady nerves, ▼ Flammable, ▼ Little warmth, ◆ Cloak | A superb cloak; steadies the nerves; catches fire easily; little warmth. |
| **8.5** |  | elven corset (F) | ↁ Elves (Continued) | medieval | plainleather | guns 1.2, social 3.9 | 4 / 2.4 | 8.6 / 8.5 | plainleather 8.5, bearskin 8.6, thrumbofur 8.8 | ◆ Everyday wear, ● Women only, ▲ Melee edge, ▲ Steady nerves, ▼ Won't stop a bullet, ▲ Good impression, ⚠ Wrong-gender mood | A superb everyday garment; sharpens a fighter; steadies the nerves; won't stop a bullet. |
| **8.3** |  | dark elven cape (T,M,F) | ↁ Elves (Continued) | medieval | devilstrand | guns 1.0 | 13 / 13.2 | 8.5 / 8.6 | cloth 8.1, synthread 8.3, devilstrand 8.3, hyperweave 8.5, plainleather 8.0, bearskin 8.2, thrumbofur 8.5 | ◆ Everyday wear, ▲ Melee edge, ▲ Shrugs off pain, ▲ Steady nerves, ◆ Cloak | An excellent cloak; sharpens a fighter; keeps its wearer standing through pain; steadies the nerves. |
| **8.3** |  | high elven cloak (T,M,F) | ↁ Elves (Continued) | medieval | devilstrand | guns 1.0 | 13 / 13.2 | 8.5 / 8.6 | cloth 8.1, synthread 8.3, devilstrand 8.3, hyperweave 8.5, plainleather 8.0, bearskin 8.2, thrumbofur 8.5 | ◆ Everyday wear, ▲ Melee edge, ▲ Shrugs off pain, ▲ Steady nerves, ◆ Cloak | An excellent cloak; sharpens a fighter; keeps its wearer standing through pain; steadies the nerves. |
| **8.1** |  | military jacket | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.8 | 4 / 2.4 | 8.2 / 8.1 | cloth 7.7, synthread 7.9, devilstrand 8.1, hyperweave 8.3, plainleather 7.8, bearskin 7.9, thrumbofur 8.4 | ◆ Everyday wear, ▲ Steady nerves, ▼ Won't stop a bullet, ◆ Coat, ◆ Top | An excellent everyday garment; steadies the nerves; won't stop a bullet. |
| **7.8** |  | apron | Vanilla Apparel Expanded | medieval | devilstrand | guns 1.6, work 7.7 | 3.6 / 1.2 | 8.0 / 7.7 | cloth 7.5, synthread 7.6, devilstrand 7.8, hyperweave 8.0, plainleather 7.5, bearskin 7.6, thrumbofur 8.1 | ◆ Everyday wear, ◆ Work clothes, ▼ Won't stop a bullet, ▼ Little warmth, ◆ Full outfit | An excellent everyday garment; speeds up work; won't stop a bullet; little warmth. |
| **7.8** |  | lab coat | Anomaly | industrial | devilstrand | guns 1.3, work 3.3 | 10 / 14.4 | 7.9 / 8.4 | cloth 7.3, synthread 7.7, devilstrand 7.8, hyperweave 8.1, plainleather 7.1, bearskin 7.5, thrumbofur 8.2 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Researcher's, ◆ Faith's apparel | An excellent everyday garment; for a researcher; a faith asks for it; won't stop a bullet. |
| **7.8** |  | military coat | Vanilla Quests Expanded - Deadlife | industrial |  | guns 1.7 | 14 / 14 | 8.2 / 8.2 |  | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Coat, ◆ Full outfit | An excellent everyday garment; won't stop a bullet. |
| **7.7** | **7.7** / 6.7 | elven gloves | ↁ Elves (Continued) | medieval | plainleather | guns 1.0, work 4.9 | 5.6 / 5.6 | 7.9 / 7.9 | plainleather 7.7, bearskin 7.9, thrumbofur 8.2 | ◆ Everyday wear, ◆ For shooters, ▲ Steadier aim, ◆ Work clothes | An excellent everyday garment for a shooter; steadies the aim; speeds up work. |
| **7.7** |  | military uniform | Vanilla Apparel Expanded | industrial | devilstrand | guns 2.7 | 4.4 / 4.8 | 7.8 / 7.9 | cloth 6.0, synthread 7.0, devilstrand 7.7, hyperweave 8.4, plainleather 6.5, bearskin 7.2, thrumbofur 8.4 | ◆ Everyday wear, ▲ Fireproof, ▲ Shrugs off pain, ◆ Hunter's, ◆ Full outfit, ● Stuff matters | An excellent everyday garment; shrugs off fire; keeps its wearer standing through pain; for a hunter. |
| **7.7** |  | shirt and tie | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.3, social 7.8 | 5.2 / 2.4 | 8.0 / 7.6 | cloth 7.5, synthread 7.6, devilstrand 7.7, hyperweave 7.9, plainleather 7.4, bearskin 7.6, thrumbofur 8.0 | ◆ Everyday wear, ◆ Royal, ◆ Trader's, ▼ Won't stop a bullet, ▲ Good impression | An excellent everyday garment; fit for a title; helps in talks and trade; won't stop a bullet. |
| **7.7** |  | suit jacket | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.5, social 7.8 | 5.2 / 2.4 | 8.1 / 7.7 | cloth 7.5, synthread 7.6, devilstrand 7.7, hyperweave 8.0, plainleather 7.5, bearskin 7.6, thrumbofur 8.1 | ◆ Everyday wear, ◆ Royal, ◆ Trader's, ▼ Won't stop a bullet, ▲ Good impression | An excellent everyday garment; fit for a title; helps in talks and trade; won't stop a bullet. |
| **7.7** |  | toga | Vanilla Factions Expanded - Classical | neolithic | devilstrand | guns 1.6 | 12 / 14.4 | 8.0 / 8.3 | cloth 7.0, synthread 7.5, devilstrand 7.7, hyperweave 8.2, plainleather 6.8, bearskin 7.4, thrumbofur 8.3 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Coat, ◆ Full outfit, ● Stuff matters | An excellent everyday garment; won't stop a bullet. |
| **7.7** |  | unisuit | Rimsenal Faction Pack - Spacer | medieval | devilstrand | guns 1.5, vacuum 2.4 | 11 / 13.2 | 8.0 / 8.2 | cloth 7.1, synthread 7.5, devilstrand 7.7, hyperweave 8.1, plainleather 6.9, bearskin 7.4, thrumbofur 8.2 | ◆ Everyday wear, ▲ Warm, ▲ Cool, ▼ Won't stop a bullet, ◆ Full outfit | An excellent everyday garment; warm in a blizzard; keeps its wearer cool; won't stop a bullet. |
| **7.6** |  | ghillie suit | Vanilla Armour Expanded | industrial |  | guns 1.0, hunting 5.0 | 6 / 0 | 8.1 / 7.1 |  | ◆ Everyday wear, ✦ Camouflage, ▲ Steadier aim, ▼ Won't stop a bullet, ◆ Hunter's, ▼ Flammable, ◆ Coat | An excellent everyday garment; hides its wearer; steadies the aim; won't stop a bullet. |
| **7.5** |  | lab coat | Vanilla Apparel Expanded | industrial |  | guns 1.0, work 5.1 | 12 / 6 | 8.2 / 7.3 |  | ◆ Everyday wear, ◆ Work clothes, ▼ Won't stop a bullet, ◆ Researcher's, ▼ Flammable | An excellent everyday garment; speeds up work; for a researcher; won't stop a bullet. |
| **7.5** |  | shaman cloak (F) | ↁ Elves (Continued) | medieval | devilstrand | guns 1.0 | 13 / 13.2 | 8.0 / 8.0 | cloth 7.0, synthread 7.5, devilstrand 7.5, hyperweave 7.8, plainleather 6.7, bearskin 7.3, thrumbofur 8.0 | ◆ Everyday wear, ● Women only, ⚠ Wrong-gender mood, ◆ Cloak | An excellent cloak. |
| **7.5** |  | tribalwear | Core | neolithic | devilstrand | guns 1.4 | 11 / 13.2 | 7.8 / 8.1 | cloth 6.8, synthread 7.3, devilstrand 7.5, hyperweave 7.9, plainleather 6.5, bearskin 7.2, thrumbofur 8.0 | ◆ Everyday wear, ▲ Warm, ▲ Cool, ▼ Won't stop a bullet, ◆ Full outfit, ● Stuff matters | An excellent everyday garment; warm in a blizzard; keeps its wearer cool; won't stop a bullet. |
| **7.4** |  | tribal poncho | Vanilla Apparel Expanded | neolithic | devilstrand | guns 1.2 | 11 / 13.2 | 7.8 / 8.1 | cloth 6.8, synthread 7.3, devilstrand 7.4, hyperweave 7.8, plainleather 6.5, bearskin 7.1, thrumbofur 7.9 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Coat, ◆ Top | A strong everyday garment; won't stop a bullet. |
| **7.3** |  | doctor scrubs | Vanilla Apparel Expanded | industrial |  | guns 1.0, work 7.1 | 6 / 2 | 7.9 / 7.1 |  | ◆ Everyday wear, ◆ Work clothes, ▼ Won't stop a bullet, ◆ Medic's, ▼ Flammable | A strong everyday garment; speeds up work; for the colony's doctor; won't stop a bullet. |
| **7.3** |  | jumpsuit | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.8, work 4.9 | 6 / 4.8 | 7.7 / 7.5 | cloth 6.4, synthread 6.9, devilstrand 7.3, hyperweave 7.9, plainleather 6.4, bearskin 6.9, thrumbofur 8.0 | ◆ Everyday wear, ◆ Work clothes, ▼ Won't stop a bullet, ◆ Full outfit, ● Stuff matters | A strong everyday garment; speeds up work; won't stop a bullet. |
| **7.2** |  | builder's jacket | Vanilla Apparel Expanded | industrial |  | guns 1.6, work 6.3 | 6 / 1 | 7.9 / 6.7 |  | ◆ Everyday wear, ◆ Work clothes, ▼ Won't stop a bullet, ◆ Builder's | A strong everyday garment; speeds up work; for a builder; won't stop a bullet. |
| **7.2** |  | elven tunic (T,M,F) | ↁ Elves (Continued) | medieval | devilstrand | guns 1.4 | 13 / 8.4 | 8.1 / 7.3 | cloth 6.5, synthread 7.1, devilstrand 7.2, hyperweave 7.8, plainleather 6.2, bearskin 6.9, thrumbofur 8.0 | ◆ Everyday wear, ▲ Warm, ▼ Won't stop a bullet, ◆ Top, ● Stuff matters | A strong everyday garment; warm in a blizzard; won't stop a bullet. |
| **7.2** |  | great dress | Vanilla Factions Expanded - Settlers | medieval | devilstrand | guns 1.4, social 6.1 | 2 / 9.6 | 6.5 / 8.1 | cloth 6.7, synthread 7.0, devilstrand 7.2, hyperweave 7.6 | ◆ Everyday wear, ● Women only, ◆ Trader's, ▼ Won't stop a bullet, ▲ Good impression, ⚠ Wrong-gender mood, ◆ Full outfit | A strong everyday garment; helps in talks and trade; makes a good impression; won't stop a bullet. |
| **7.1** |  | chef's uniform | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.3, work 6.0 | 4 / 4.8 | 7.3 / 7.5 | cloth 6.6, synthread 6.9, devilstrand 7.1, hyperweave 7.4 | ◆ Everyday wear, ◆ Work clothes, ▼ Won't stop a bullet, ◆ Cook's | A strong everyday garment; speeds up work; for a cook; won't stop a bullet. |
| **7.0** |  | elven gambeson (T,M,F) | ↁ Elves (Continued) | medieval | devilstrand | guns 2.6 | 7 / 3.6 | 7.7 / 6.9 | plainleather 4.9, bearskin 6.2, thrumbofur 8.2, cloth 4.3, synthread 5.9, devilstrand 7.0, hyperweave 8.1 | ◆ Everyday wear, ▲ Fireproof, ◆ Full outfit, ● Stuff matters | A strong everyday garment; shrugs off fire. |
| **6.8** |  | fleece shirt | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.2, work 4.4 | 2 / 9.6 | 5.6 / 7.9 | cloth 6.2, synthread 6.6, devilstrand 6.8, hyperweave 7.1, plainleather 6.0, bearskin 6.4, thrumbofur 7.0 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Grower's, ◆ Handler's | A strong everyday garment; for a grower; for an animal handler; won't stop a bullet. |
| **6.8** |  | sheriff shirt | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.3, social 6.1 | 5.2 / 2.4 | 7.4 / 6.6 | cloth 6.2, synthread 6.6, devilstrand 6.8, hyperweave 7.2, plainleather 6.2, bearskin 6.5, thrumbofur 7.4 | ◆ Everyday wear, ◆ Trader's, ▼ Won't stop a bullet, ▲ Good impression, ◆ Top | A strong everyday garment; helps in talks and trade; makes a good impression; won't stop a bullet. |
| **6.6** |  | overalls | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.4, work 4.4, carrying 4.2 | 4 / 1.9 | 7.2 / 6.5 | cloth 6.0, synthread 6.4, devilstrand 6.6, hyperweave 7.1, plainleather 6.0, bearskin 6.4, thrumbofur 7.3 | ◆ Everyday wear, ▲ Carries more, ▼ Won't stop a bullet, ◆ Grower's, ◆ Handler's | A strong everyday garment; carries more; for a grower; won't stop a bullet. |
| **6.4** |  | vest | Vanilla Factions Expanded - Settlers | medieval | devilstrand | guns 1.2, social 3.9 | 4 / 7.2 | 6.3 / 7.4 | cloth 5.7, synthread 6.2, devilstrand 6.4, hyperweave 6.9, plainleather 5.5, bearskin 6.1, thrumbofur 6.9 | ◆ Everyday wear, ▼ Won't stop a bullet, ▲ Good impression, ◆ Top | A solid everyday garment; makes a good impression; won't stop a bullet. |
| **6.0** |  | elven boots | ↁ Elves (Continued) | medieval | plainleather | guns 1.0, work 2.5 | 5.6 / 5.6 | 6.7 / 6.7 | plainleather 6.0, bearskin 6.5, thrumbofur 7.2 | ◆ Everyday wear, ▲ Quick feet, ◆ Footwear | A solid everyday garment; quickens the step. |
| **5.8** |  | elven lioncloth (F) | ↁ Elves (Continued) | medieval | devilstrand | guns 1.6 | 7 / 3.6 | 7.1 / 5.5 | cloth 4.3, synthread 5.2, devilstrand 5.8, hyperweave 6.9, plainleather 4.2, bearskin 5.1, thrumbofur 7.2 | ◆ Everyday wear, ● Women only, ▼ Won't stop a bullet, ⚠ Wrong-gender mood, ◆ Full outfit, ● Stuff matters | A solid everyday garment; won't stop a bullet. |
| **5.8** |  | shaman robe (F) | ↁ Elves (Continued) | medieval | devilstrand | guns 1.6 | 7 / 3.6 | 7.1 / 5.6 | cloth 4.3, synthread 5.2, devilstrand 5.8, hyperweave 7.0, plainleather 4.2, bearskin 5.2, thrumbofur 7.3 | ◆ Everyday wear, ● Women only, ▼ Won't stop a bullet, ⚠ Wrong-gender mood, ◆ Full outfit, ● Stuff matters | A solid everyday garment; won't stop a bullet. |
| **5.8** |  | skirt | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.0, social 4.4 | 2.8 / 5.3 | 5.6 / 6.8 | cloth 5.3, synthread 5.7, devilstrand 5.8, hyperweave 6.1, plainleather 5.1, bearskin 5.5, thrumbofur 6.2 | ◆ Everyday wear, ▼ Won't stop a bullet, ▲ Good impression, ◆ Legwear | A solid everyday garment; makes a good impression; won't stop a bullet. |
| **5.4** |  | elven undergarment (F) | ↁ Elves (Continued) | medieval | devilstrand | guns 1.4 | 7 / 3.6 | 6.9 / 5.1 | cloth 4.3, synthread 5.0, devilstrand 5.4, hyperweave 6.5, plainleather 4.1, bearskin 4.9, thrumbofur 6.8 | ◆ Everyday wear, ● Women only, ▼ Won't stop a bullet, ⚠ Wrong-gender mood, ◆ Top, ● Stuff matters | A fair everyday garment; won't stop a bullet. |
| **5.4** | **5.4** / 2.8 | heavy bandolier | Biotech | industrial | devilstrand | guns 1.0 | 2 / 2.4 | 5.7 / 5.9 | cloth 5.1, synthread 5.4, devilstrand 5.4, hyperweave 5.7, plainleather 5.0, bearskin 5.3, thrumbofur 5.8 | ◆ Everyday wear, ◆ For shooters, ▼ Won't stop a bullet, ⚠ Counts as naked, ▼ Little warmth, ◆ Coat | A fair everyday garment for a shooter; won't stop a bullet; little warmth. |
| **5.1** |  | gloves | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.0 | 8 / 1.9 | 7.5 / 3.1 | cloth 4.7, synthread 5.3, devilstrand 5.1, hyperweave 5.9, plainleather 4.5, bearskin 5.0, thrumbofur 6.4 | ◆ Everyday wear, ◆ Handwear, ● Stuff matters | A fair everyday garment. |
| **5.1** |  | quilted vest | Vanilla Armour Expanded | medieval | devilstrand | guns 1.7 | 2.4 / 5 | 4.8 / 6.5 | plainleather 3.5, bearskin 4.3, thrumbofur 6.4, cloth 3.3, synthread 4.2, devilstrand 5.1, hyperweave 6.3 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Top, ● Stuff matters | A fair everyday garment; won't stop a bullet. |
| **5.1** |  | trousers | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.2, social 3.9 | 4 / 1.9 | 6.3 / 4.9 | cloth 4.6, synthread 5.0, devilstrand 5.1, hyperweave 5.8, plainleather 4.5, bearskin 4.9, thrumbofur 6.1 | ◆ Everyday wear, ▼ Won't stop a bullet, ▲ Good impression, ◆ Legwear, ● Stuff matters | A fair everyday garment; makes a good impression; won't stop a bullet. |
| **5.0** |  | corset | Royalty | medieval | devilstrand | guns 1.2 | 8 / 2.4 | 7.1 / 3.8 | cloth 4.2, synthread 4.9, devilstrand 5.0, hyperweave 6.1, plainleather 4.0, bearskin 4.7, thrumbofur 6.6 | ◆ Everyday wear, ● Women only, ◆ Royal, ▼ Won't stop a bullet, ⚠ Wrong-gender mood, ◆ Top, ● Stuff matters | A fair everyday garment; fit for a title; won't stop a bullet. |
| **5.0** |  | formal vest | Royalty | medieval | devilstrand | guns 1.2 | 8 / 2.4 | 7.1 / 3.8 | cloth 4.2, synthread 4.9, devilstrand 5.0, hyperweave 6.1, plainleather 4.0, bearskin 4.7, thrumbofur 6.6 | ◆ Everyday wear, ● Men only, ◆ Royal, ▼ Won't stop a bullet, ⚠ Wrong-gender mood, ◆ Top, ● Stuff matters | A fair everyday garment; fit for a title; won't stop a bullet. |
| **5.0** |  | gambeson | Vanilla Armour Expanded | medieval | devilstrand | guns 1.8 | 4.2 / 2.4 | 6.2 / 5.0 | plainleather 3.4, bearskin 4.3, thrumbofur 6.8, cloth 3.2, synthread 4.1, devilstrand 5.0, hyperweave 6.6 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Top, ● Stuff matters | A fair everyday garment; won't stop a bullet. |
| **5.0** |  | jeans | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.4 | 6.4 / 2.4 | 6.8 / 4.3 | cloth 3.8, synthread 4.6, devilstrand 5.0, hyperweave 6.2, plainleather 3.8, bearskin 4.5, thrumbofur 6.6 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Legwear, ● Stuff matters | A fair everyday garment; won't stop a bullet. |
| **5.0** |  | shoes | Vanilla Apparel Expanded | industrial | plainleather | guns 1.0, social 3.9 | 4.8 / 1.9 | 6.5 / 4.6 | plainleather 5.0, bearskin 5.5, thrumbofur 6.5 | ◆ Everyday wear, ▲ Good impression, ◆ Footwear, ● Stuff matters | A fair everyday garment; makes a good impression. |
| **5.0** |  | shorts | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.0, work 2.3 | 2 / 6 | 4.2 / 6.7 | cloth 4.4, synthread 4.8, devilstrand 5.0, hyperweave 5.3, plainleather 4.2, bearskin 4.6, thrumbofur 5.3 | ◆ Everyday wear, ▲ Quick feet, ▼ Won't stop a bullet, ◆ Legwear | A fair everyday garment; quickens the step; won't stop a bullet. |
| **4.8** |  | hoodie | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.3 | 6.4 / 2.4 | 6.7 / 4.1 | cloth 3.8, synthread 4.5, devilstrand 4.8, hyperweave 6.0, plainleather 3.7, bearskin 4.4, thrumbofur 6.5 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Top, ● Stuff matters | A fair everyday garment; won't stop a bullet. |
| **4.7** |  | boots | Vanilla Apparel Expanded | medieval | plainleather | guns 1.0, work 2.1 | 6.4 / 1.3 | 6.8 / 3.4 | plainleather 4.7, bearskin 5.2, thrumbofur 6.6 | ◆ Everyday wear, ▲ Quick feet, ◆ Footwear, ● Stuff matters | A fair everyday garment; quickens the step. |
| **4.6** |  | elven leather armor (T,M,F) | ↁ Elves (Continued) | medieval | plainleather | guns 1.4 | 5.6 / 2.4 | 6.4 / 4.1 | plainleather 4.6, bearskin 5.8, thrumbofur 7.8 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Coat, ◆ Full outfit, ● Stuff matters | A fair everyday garment; won't stop a bullet. |
| **4.6** |  | elven leather armor (T,M,F) | ↁ Elves (Continued) | medieval | plainleather | guns 1.4 | 5.6 / 2.4 | 6.4 / 4.1 | plainleather 4.6, bearskin 5.8, thrumbofur 7.8 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Coat, ◆ Full outfit, ● Stuff matters | A fair everyday garment; won't stop a bullet. |
| **4.5** |  | blouse | Vanilla Apparel Expanded | medieval | devilstrand | guns 1.0 | 2 / 7.7 | 3.2 / 6.9 | cloth 3.8, synthread 4.3, devilstrand 4.5, hyperweave 5.0, plainleather 3.6, bearskin 4.1, thrumbofur 4.9 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Top | A fair everyday garment; won't stop a bullet. |
| **4.5** |  | broadwrap | Ideology | neolithic | devilstrand | guns 1.2 | 4 / 4.8 | 5.1 / 5.7 | cloth 3.7, synthread 4.3, devilstrand 4.5, hyperweave 5.3 | ◆ Everyday wear, ◆ Faith's apparel, ● Stuff matters | A fair everyday garment; a faith asks for it. |
| **4.5** |  | button-down shirt | Core | medieval | devilstrand | guns 1.3 | 5.2 / 2.4 | 6.1 / 4.1 | cloth 3.5, synthread 4.1, devilstrand 4.5, hyperweave 5.6, plainleather 3.4, bearskin 4.0, thrumbofur 6.1 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Top, ● Stuff matters | A fair everyday garment; won't stop a bullet. |
| **4.5** |  | tank top | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.2 | 2 / 5.3 | 3.9 / 6.3 | cloth 3.7, synthread 4.2, devilstrand 4.5, hyperweave 5.2, plainleather 3.6, bearskin 4.1, thrumbofur 5.2 | ◆ Everyday wear, ▲ Quick feet, ▲ Melee edge, ▼ Won't stop a bullet, ◆ Top, ● Stuff matters | A fair everyday garment; quickens the step; sharpens a fighter; won't stop a bullet. |
| **4.4** |  | mantle | Vanilla Factions Expanded - Medieval 2 | medieval | devilstrand | guns 1.3 | 4.8 / 3.4 | 5.7 / 4.7 | cloth 3.6, synthread 4.2, devilstrand 4.4, hyperweave 5.4, plainleather 3.4, bearskin 4.0, thrumbofur 5.8 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Coat, ◆ Top, ● Stuff matters | A modest everyday garment; won't stop a bullet. |
| **4.2** |  | formal shirt | Royalty | medieval | devilstrand | guns 1.3 | 4.4 / 2.4 | 5.6 / 4.1 | cloth 3.2, synthread 3.8, devilstrand 4.2, hyperweave 5.4, plainleather 3.2, bearskin 3.8, thrumbofur 5.8 | ◆ Everyday wear, ◆ Royal, ▼ Won't stop a bullet, ◆ Top, ● Stuff matters | A modest everyday garment; fit for a title; won't stop a bullet. |
| **4.2** |  | tunic | Vanilla Apparel Expanded | medieval | devilstrand | guns 1.2 | 6.4 / 1.2 | 6.6 / 2.8 | cloth 3.5, synthread 4.1, devilstrand 4.2, hyperweave 5.3, plainleather 3.4, bearskin 4.0, thrumbofur 5.9 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Top, ● Stuff matters | A modest everyday garment; won't stop a bullet. |
| **4.1** |  | tribal kilt | Vanilla Apparel Expanded | neolithic | devilstrand | guns 1.2 | 4 / 3.6 | 5.0 / 4.7 | cloth 3.4, synthread 3.9, devilstrand 4.1, hyperweave 4.9, plainleather 3.3, bearskin 3.8, thrumbofur 5.2 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Legwear, ● Stuff matters | A modest everyday garment; won't stop a bullet. |
| **4.0** |  | casual T-shirt | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.2 | 4.4 / 2.4 | 5.4 / 3.9 | cloth 3.2, synthread 3.7, devilstrand 4.0, hyperweave 4.9, plainleather 3.1, bearskin 3.7, thrumbofur 5.3 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Top, ● Stuff matters | A modest everyday garment; won't stop a bullet. |
| **4.0** |  | T-shirt | Core | medieval | devilstrand | guns 1.2 | 4.4 / 2.4 | 5.4 / 3.9 | cloth 3.2, synthread 3.7, devilstrand 4.0, hyperweave 4.9, plainleather 3.1, bearskin 3.7, thrumbofur 5.3 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Top, ● Stuff matters | A modest everyday garment; won't stop a bullet. |
| **3.9** |  | unity guard cloak | Rimsenal Faction Pack - Federation | spacer |  | guns 1.0 | 5 / 3 | 5.5 / 3.9 |  | ◆ Everyday wear, ▼ Flammable, ◆ Cloak | A modest cloak; catches fire easily. |
| **3.6** |  | pants | Core | medieval | devilstrand | guns 1.2 | 4 / 1.9 | 5.0 / 3.4 | cloth 3.0, synthread 3.5, devilstrand 3.6, hyperweave 4.4, plainleather 2.9, bearskin 3.4, thrumbofur 4.7 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ Legwear, ● Stuff matters | A modest everyday garment; won't stop a bullet. |
| **3.2** |  | patient gown | Vanilla Quests Expanded - Ancients | industrial |  | guns 1.0 | 4 / 1 | 5.0 / 2.5 |  | ◆ Everyday wear, ● Hard to find, ▼ Won't stop a bullet, ▼ Flammable, ◆ Full outfit | A weak everyday garment; won't stop a bullet; catches fire easily. |
| **3.0** |  | patched shirt | Rimsenal Faction Pack - Feral | medieval | plainleather | guns 1.0 | 5 / 0 | 5.5 / 1.0 |  | ◆ Everyday wear, ▼ Won't stop a bullet, ▼ Flammable, ◆ Top | A weak everyday garment; won't stop a bullet; catches fire easily. |
| **2.8** |  | sash | Biotech | industrial | devilstrand | guns 1.0 | 2 / 2.4 | 3.2 / 3.5 | cloth 2.5, synthread 2.8, devilstrand 2.8, hyperweave 3.2, plainleather 2.4, bearskin 2.7, thrumbofur 3.3 | ◆ Everyday wear, ▼ Won't stop a bullet, ⚠ Counts as naked, ▼ Little warmth, ◆ Top | A weak everyday garment; won't stop a bullet; little warmth. |
| **2.8** |  | slave body strap | Ideology | medieval | devilstrand | guns 1.0 | 2 / 2.4 | 3.2 / 3.5 | cloth 2.5, synthread 2.8, devilstrand 2.8, hyperweave 3.2, plainleather 2.4, bearskin 2.7, thrumbofur 3.3 | ◆ Everyday wear, ▼ Won't stop a bullet, ◆ For slaves, ▼ Little warmth, ◆ Coat | A weak everyday garment; won't stop a bullet; little warmth. |
| **2.2** |  | rags | Vanilla Ideology Expanded - Hats and Rags | medieval | devilstrand | guns 1.0 | 1 / 1.2 | 2.4 / 2.6 | cloth 1.9, synthread 2.1, devilstrand 2.2, hyperweave 2.5, plainleather 1.9, bearskin 2.0, thrumbofur 2.6 | ◆ Everyday wear, ▼ Won't stop a bullet, ▼ Little warmth, ◆ Coat, ◆ Full outfit | A poor everyday garment; won't stop a bullet; little warmth. |
| **2.0** |  | tabard | Vanilla Factions Expanded - Medieval 2 | medieval |  | guns 1.0 | 0 / 0 | 2.0 / 2.0 |  | ◆ Everyday wear, ▼ Won't stop a bullet, ▼ Flammable, ▼ Little warmth, ◆ Coat, ◆ Top | A poor everyday garment; won't stop a bullet; catches fire easily; little warmth. |
| **1.5** |  | dame dress | Vanilla Factions Expanded - Medieval 2 | medieval | devilstrand | guns 1.8, social 6.1 | 6 / 4.8 | 3.4 / 2.3 | cloth 1.0, synthread 1.0, devilstrand 1.5, hyperweave 4.3, plainleather 1.0, bearskin 1.0, thrumbofur 4.9 | ◆ Everyday wear, ▼ Bars work, ● Women only, ◆ Trader's, ▼ Won't stop a bullet, ▲ Good impression, ⚠ Wrong-gender mood | A poor everyday garment; helps in talks and trade; makes a good impression; bars some work. |
| **1.2** |  | beads | Vanilla Ideology Expanded - Hats and Rags | medieval | steel | guns 1.0 | 0.3 / 0 | 1.4 / 1.0 | wood 1.6, granite blocks 1.0, steel 1.2, plasteel 1.2, uranium 1.2 | ◆ Everyday wear, ▼ Little warmth, ◆ Worn for looks | A poor everyday garment; little warmth. |
| **1.0** |  | burka | Ideology | neolithic | devilstrand | guns 1.6 | 2 / 4.8 | 1.0 / 3.1 | cloth 1.0, synthread 1.0, devilstrand 1.0, hyperweave 2.9 | ◆ Everyday wear, ▼ Slows you, ▼ Won't stop a bullet, ◆ Faith's apparel, ◆ Coat, ● Stuff matters | A poor everyday garment; a faith asks for it; slows its wearer; won't stop a bullet. |
| **1.0** |  | jester outfit | Vanilla Factions Expanded - Medieval 2 | medieval | devilstrand | guns 1.5, social 6.8 | 4 / 2.4 | 1.0 / 1.0 |  | ◆ Everyday wear, ▼ Bars work, ◆ Trader's, ▼ Won't stop a bullet, ▲ Good impression, ◆ Full outfit | A poor everyday garment; helps in talks and trade; makes a good impression; bars some work. |
| **1.0** *(rough)* |  | knuckle dusters | Det's Xenotypes - Boglegs | industrial | steel | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Everyday wear, ✦ Its own effect, ▼ Little warmth, ◆ Handwear, ◆ Worn for looks | A poor everyday garment; little warmth. |
| **1.0** |  | plate boots | Vanilla Armour Expanded | medieval | steel | guns 1.0 | 0.5 / 0 | 1.0 / 1.0 |  | ◆ Armour for the feet, ▼ Slows you, ▼ Little warmth, ◆ Footwear | A poor piece of armour for the feet; slows its wearer; little warmth. |
| **1.0** |  | plate gloves | Vanilla Armour Expanded | medieval | steel | guns 1.0 | 0.5 / 0 | 1.0 / 1.0 |  | ◆ Armour for the hands, ▼ Slows work, ▼ Little warmth, ◆ Handwear | A poor piece of armour for the hands; slows work; little warmth. |
| **1.0** *(rough)* |  | psyring | Vanilla Psycasts Expanded | ultra |  | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Everyday wear, ● Hard to find, ✦ Its own effect, ▼ Little warmth, ◆ Worn for looks | A poor everyday garment; little warmth. |
| **1.0** |  | slave collar | Ideology | medieval | steel | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Everyday wear, ◆ For slaves, ▼ Little warmth, ◆ Worn for looks | A poor everyday garment; little warmth. |

### Hats, masks and other headgear (89)

| Rating | Shooter / melee | Piece | Mod | Age | Stuff | Sub-ratings | Cold / heat °C | In the cold / heat | By stuff | Tags | In plain words |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **8.6** |  | integrator headset | Biotech | spacer |  | guns 1.0, mechanitor 6.8 | 0 / 0 | 8.6 / 8.6 |  | ◆ Headgear, ● Mechanitor only, ✦ Mech bandwidth, ▼ Little warmth, ◆ Hat, ● Biocodable | A superb piece of headgear; more mechs for a mechanitor; little warmth. |
| **8.4** |  | general’s cap | Vanilla Quests Expanded - Deadlife | industrial |  | guns 1.0, social 8.9 | 3 / 3 | 8.4 / 8.4 |  | ◆ Headgear, ● Hard to find, ◆ Trader's, ▲ Good impression, ▼ Little warmth, ◆ Hat | An excellent piece of headgear; helps in talks and trade; makes a good impression; little warmth. |
| **8.4** |  | HAZMAT hood | Vanilla Armour Expanded | industrial | devilstrand | guns 1.0, toxins 5.1, psychic shelter 4.9 | 24 / 33.6 | 8.5 / 8.8 | cloth 8.2, synthread 8.4, devilstrand 8.4, hyperweave 8.6 | ◆ Headgear, ▲ Warm, ▲ Cool, ✦ Psychic shield, ▼ Slows work, ▼ Blocks psycasts, ◆ Eyewear | An excellent piece of headgear; warm in a blizzard; keeps its wearer cool; slows work. |
| **8.3** |  | hunter visage | Det's Xenotypes - Venators | spacer |  | guns 1.3, toxins 5.5, social 8.7 | 0 / 0 | 8.3 / 8.3 |  | ◆ Headgear, ✦ Toxin-proof, ◆ Trader's, ▼ Flammable, ▼ Little warmth, ◆ Mask or hood | An excellent piece of headgear; keeps toxins out; helps in talks and trade; catches fire easily. |
| **8.2** |  | array headset | Biotech | spacer |  | guns 1.0, mechanitor 5.5 | 0 / 0 | 8.2 / 8.2 |  | ◆ Headgear, ● Mechanitor only, ✦ Mech bandwidth, ▼ Flammable, ▼ Little warmth, ◆ Hat | An excellent piece of headgear; more mechs for a mechanitor; catches fire easily; little warmth. |
| **8.1** *(rough)* |  | Cowl of the Soulreaper | A RimWorld of Magic | medieval | devilstrand | guns 1.9, work 5.8, magic 3.2 | 22 / 4.4 | 8.7 / 7.4 | cloth 8.0, synthread 8.1, devilstrand 8.1, hyperweave 8.2, plainleather 8.0, bearskin 8.0, thrumbofur 8.3 | ◆ Headgear, ● Needs a mage, ▲ Warm, ✦ Enchanted, ◆ Work clothes, ▼ Off-putting, ◆ Researcher's | An excellent piece of headgear; warm in a blizzard; enchanted for a mage; puts people off. |
| **7.6** |  | balaclava | Vanilla Armour Expanded | industrial | devilstrand | guns 1.7 | 11 / 2.9 | 8.2 / 7.1 | cloth 7.4, synthread 7.6, devilstrand 7.6, hyperweave 7.8, plainleather 7.4, bearskin 7.5, thrumbofur 7.9 | ◆ Headgear, ▲ Warm, ▲ Steady nerves, ◆ Mask or hood | An excellent piece of headgear; warm in a blizzard; steadies the nerves. |
| **7.6** |  | chef's toque | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.4, work 6.9 | 2 / 9.6 | 7.2 / 8.1 | cloth 7.4, synthread 7.6, devilstrand 7.6, hyperweave 7.7 | ◆ Headgear, ▲ Cool, ◆ Work clothes, ◆ Cook's | An excellent piece of headgear; keeps its wearer cool; speeds up work; for a cook. |
| **7.6** |  | Hood of Protection | A RimWorld of Magic | medieval | devilstrand | guns 1.6 | 13 / 12 | 8.0 / 7.9 | cloth 7.4, synthread 7.5, devilstrand 7.6, hyperweave 7.7, plainleather 7.3, bearskin 7.5, thrumbofur 7.7 | ◆ Headgear, ● Needs a mage, ▲ Warm, ▲ Cool, ✦ Enchanted, ◆ Mask or hood | An excellent piece of headgear; warm in a blizzard; keeps its wearer cool; enchanted for a mage. |
| **7.6** |  | masked hood | ↁ Elves (Continued) | medieval | devilstrand | guns 2.4, social 6.1 | 9 / 7.7 | 7.9 / 7.8 | cloth 7.4, synthread 7.6, devilstrand 7.6, hyperweave 7.8, plainleather 7.3, bearskin 7.5, thrumbofur 7.9 | ◆ Headgear, ▲ Warm, ▲ Cool, ◆ Trader's, ▲ Good impression, ◆ Mask or hood | An excellent piece of headgear; warm in a blizzard; keeps its wearer cool; helps in talks and trade. |
| **7.6** |  | masked hood | ↁ Elves (Continued) | medieval | devilstrand | guns 2.4, social 6.1 | 9 / 7.7 | 7.9 / 7.8 | cloth 7.4, synthread 7.6, devilstrand 7.6, hyperweave 7.8, plainleather 7.3, bearskin 7.5, thrumbofur 7.9 | ◆ Headgear, ▲ Warm, ▲ Cool, ◆ Trader's, ▲ Good impression, ◆ Mask or hood | An excellent piece of headgear; warm in a blizzard; keeps its wearer cool; helps in talks and trade. |
| **7.6** |  | Wizard Hat | A RimWorld of Magic | medieval | devilstrand | guns 1.4, work 2.8, social 2.6, carrying 5.9 | 8 / 4.8 | 8.0 / 7.6 | cloth 7.5, synthread 7.6, devilstrand 7.6, hyperweave 7.8, plainleather 7.4, bearskin 7.6, thrumbofur 7.9 | ◆ Headgear, ● Needs a mage, ▲ Warm, ✦ Enchanted, ⚠ Wears fast, ▲ Carries more, ▲ Good impression | An excellent piece of headgear; warm in a blizzard; enchanted for a mage; wears out fast. |
| **7.5** |  | data glasses | Rimsenal Faction Pack - Spacer | spacer |  | guns 1.0, work 6.5, social 6.8 | 0 / 0 | 7.5 / 7.5 |  | ◆ Headgear, ◆ Work clothes, ◆ Trader's, ▲ Good impression, ▼ Little warmth, ● Biocodable | An excellent piece of headgear; speeds up work; helps in talks and trade; little warmth. |
| **7.5** |  | psychic foil helmet | Core | medieval |  | guns 1.0, psychic shelter 8.2 | 2 / 1 | 7.6 / 7.5 |  | ◆ Headgear, ✦ Psychic shield, ▼ Blocks psycasts, ▼ Little warmth, ◆ Hat | An excellent piece of headgear; shields the mind from psychic attacks; dulls psycasts; little warmth. |
| **7.5** |  | shaman crown | ↁ Elves (Continued) | medieval | steel | guns 1.8, psychic 4.5, social 6.8 | 0.5 / 0 | 7.5 / 7.5 | steel 7.5, plasteel 7.5, uranium 7.5, wood 7.6, granite blocks 7.4 | ◆ Headgear, ● Needs a psylink, ✦ Psychic, ◆ Trader's, ▲ Good impression, ▼ Little warmth, ◆ Mask or hood | An excellent piece of headgear; lifts a psycaster; helps in talks and trade; little warmth. |
| **7.4** |  | elven masked hood | ↁ Elves (Continued) | medieval | devilstrand | guns 2.0, hunting 2.8 | 7 / 7.2 | 7.6 / 7.7 | cloth 7.1, synthread 7.3, devilstrand 7.4, hyperweave 7.6, plainleather 7.0, bearskin 7.2, thrumbofur 7.7 | ◆ Headgear, ▲ Warm, ▲ Cool, ◆ Hunter's, ◆ Mask or hood | A strong piece of headgear; warm in a blizzard; keeps its wearer cool; for a hunter. |
| **7.4** | 3.8 / **7.4** | leather helmet | Vanilla Factions Expanded - Medieval 2 | medieval | plainleather | guns 1.4 | 24 / 0 | 8.6 / 1.5 | plainleather 7.4, bearskin 7.8, thrumbofur 8.4 | ◆ Headgear, ◆ For melee fighters, ▼ Spoils your aim, ▲ Warm, ◆ Hat | A strong piece of headgear for a melee fighter; warm in a blizzard; spoils the aim. |
| **7.4** |  | shaman crown | ↁ Elves (Continued) | medieval |  | guns 1.0, psychic 4.5, social 6.8 | 0 / 0 | 7.4 / 7.4 |  | ◆ Headgear, ● Needs a psylink, ✦ Psychic, ◆ Trader's, ▼ Flammable, ▲ Good impression, ▼ Little warmth | A strong piece of headgear; lifts a psycaster; helps in talks and trade; catches fire easily. |
| **7.3** |  | radiation mask | Dubs Rimatomics | industrial |  | guns 1.0, toxins 4.7, psychic shelter 6.8 | 15 / 0 | 8.3 / 4.8 |  | ◆ Headgear, ▼ Slows you, ▲ Warm, ✦ Psychic shield, ▼ Blocks psycasts, ▼ Off-putting, ◆ Hat | A strong piece of headgear; warm in a blizzard; shields the mind from psychic attacks; slows its wearer. |
| **7.2** |  | airwire headset | Biotech | spacer |  | guns 1.0, mechanitor 3.5 | 0 / 0 | 7.2 / 7.2 |  | ◆ Headgear, ● Mechanitor only, ✦ Mech bandwidth, ▼ Flammable, ▼ Little warmth, ◆ Hat | A strong piece of headgear; more mechs for a mechanitor; catches fire easily; little warmth. |
| **7.1** |  | gas mask | Biotech | industrial |  | guns 1.2, toxins 8.0 | 1.5 / 0 | 7.3 / 6.9 |  | ◆ Headgear, ✦ Toxin-proof, ▼ Flammable, ▼ Little warmth, ◆ Mask or hood | A strong piece of headgear; keeps toxins out; catches fire easily; little warmth. |
| **7.0** |  | bowler hat | Core | medieval | devilstrand | guns 1.4, social 5.1 | 2 / 9.6 | 6.2 / 7.9 | cloth 6.7, synthread 6.9, devilstrand 7.0, hyperweave 7.2, plainleather 6.6, bearskin 6.8, thrumbofur 7.1 | ◆ Headgear, ▲ Cool, ◆ Faith's apparel, ▲ Good impression, ◆ Hat | A strong piece of headgear; keeps its wearer cool; a faith asks for it; makes a good impression. |
| **7.0** |  | cowboy hat | Core | medieval | devilstrand | guns 1.4, social 3.9 | 2 / 12 | 5.7 / 8.0 | cloth 6.6, synthread 6.9, devilstrand 7.0, hyperweave 7.2, plainleather 6.5, bearskin 6.8, thrumbofur 7.1 | ◆ Headgear, ▲ Cool, ◆ Faith's apparel, ▲ Good impression, ◆ Hat | A strong piece of headgear; keeps its wearer cool; a faith asks for it; makes a good impression. |
| **7.0** |  | elven hood | ↁ Elves (Continued) | medieval | devilstrand | guns 1.6, hunting 2.8 | 3 / 7.2 | 6.7 / 7.6 | cloth 6.7, synthread 6.9, devilstrand 7.0, hyperweave 7.2, plainleather 6.6, bearskin 6.8, thrumbofur 7.2 | ◆ Headgear, ▲ Cool, ◆ Hunter's, ◆ Hat | A strong piece of headgear; keeps its wearer cool; for a hunter. |
| **6.9** |  | hood | Core | medieval | devilstrand | guns 1.4, social 6.1 | 2 / 6 | 6.6 / 7.5 | cloth 6.7, synthread 6.9, devilstrand 6.9, hyperweave 7.1, plainleather 6.6, bearskin 6.8, thrumbofur 7.0 | ◆ Headgear, ▲ Cool, ◆ Trader's, ◆ Faith's apparel, ▲ Good impression | A strong piece of headgear; keeps its wearer cool; helps in talks and trade; a faith asks for it. |
| **6.9** |  | ladies hat | Royalty | medieval | devilstrand | guns 1.0, social 6.1 | 2 / 6 | 6.6 / 7.5 | cloth 6.7, synthread 6.9, devilstrand 6.9, hyperweave 7.0, plainleather 6.6, bearskin 6.8, thrumbofur 7.0 | ◆ Headgear, ▲ Cool, ● Women only, ◆ Royal, ◆ Trader's, ▲ Good impression, ⚠ Wrong-gender mood | A strong piece of headgear; keeps its wearer cool; fit for a title; helps in talks and trade. |
| **6.9** |  | top hat | Royalty | medieval | devilstrand | guns 1.0, social 6.1 | 2 / 6 | 6.6 / 7.5 | cloth 6.7, synthread 6.9, devilstrand 6.9, hyperweave 7.0, plainleather 6.6, bearskin 6.8, thrumbofur 7.0 | ◆ Headgear, ▲ Cool, ● Men only, ◆ Royal, ◆ Trader's, ▲ Good impression, ⚠ Wrong-gender mood | A strong piece of headgear; keeps its wearer cool; fit for a title; helps in talks and trade. |
| **6.8** |  | chaperon | Vanilla Factions Expanded - Medieval 2 | medieval | devilstrand | guns 1.4, social 3.1 | 3 / 10.8 | 5.9 / 7.8 | cloth 6.4, synthread 6.7, devilstrand 6.8, hyperweave 7.0, plainleather 6.2, bearskin 6.6, thrumbofur 7.0 | ◆ Headgear, ▲ Cool, ▲ Good impression, ◆ Hat | A strong piece of headgear; keeps its wearer cool; makes a good impression. |
| **6.8** |  | jester hat | Vanilla Factions Expanded - Medieval 2 | medieval | devilstrand | guns 1.2, social 5.1 | 2 / 7.2 | 6.2 / 7.6 | cloth 6.5, synthread 6.7, devilstrand 6.8, hyperweave 6.9, plainleather 6.3, bearskin 6.6, thrumbofur 6.9 | ◆ Headgear, ▲ Cool, ▲ Good impression, ◆ Hat | A strong piece of headgear; keeps its wearer cool; makes a good impression. |
| **6.8** |  | sombrero | Vanilla Factions Expanded - Settlers | industrial | devilstrand | guns 1.4 | 2 / 14.4 | 4.3 / 8.1 | cloth 6.2, synthread 6.6, devilstrand 6.8, hyperweave 7.0, plainleather 5.9, bearskin 6.5, thrumbofur 6.9 | ◆ Headgear, ▲ Cool, ▲ Steadier aim, ◆ Hat | A strong piece of headgear; keeps its wearer cool; steadies the aim. |
| **6.8** |  | surgical mask | Vanilla Apparel Expanded | industrial |  | guns 1.0, work 5.8 | 4 / 2 | 7.2 / 6.7 |  | ◆ Headgear, ◆ Work clothes, ◆ Medic's, ▼ Flammable, ▼ Little warmth | A strong piece of headgear; speeds up work; for the colony's doctor; catches fire easily. |
| **6.5** |  | warm hat | Vanilla Christmas Expanded | medieval | devilstrand | guns 1.4 | 15 / 0 | 8.1 / 1.5 | cloth 6.3, synthread 6.7, devilstrand 6.5, hyperweave 7.1 | ◆ Headgear, ▲ Warm, ◆ Hat | A strong piece of headgear; warm in a blizzard. |
| **6.4** |  | ceremonial hood | Anomaly | neolithic | devilstrand | guns 3.3 | 10 / 2.4 | 7.7 / 5.0 | cloth 5.7, synthread 6.4, devilstrand 6.4, hyperweave 7.0 | ◆ Headgear, ▲ Warm, ◆ Armour for the head, ▲ Fireproof, ◆ Faith's apparel | A solid piece of headgear; warm in a blizzard; shrugs off fire; a faith asks for it. |
| **6.4** |  | dame hat | Vanilla Factions Expanded - Medieval 2 | medieval | devilstrand | guns 1.1, social 5.1 | 2 / 4.8 | 6.2 / 7.1 | cloth 6.2, synthread 6.4, devilstrand 6.4, hyperweave 6.6, plainleather 6.1, bearskin 6.3, thrumbofur 6.6 | ◆ Headgear, ● Women only, ▲ Good impression, ⚠ Wrong-gender mood, ▼ Little warmth, ◆ Hat | A solid piece of headgear; makes a good impression; little warmth. |
| **6.4** |  | military beret | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.4 | 2 / 12 | 4.2 / 7.8 | cloth 5.8, synthread 6.3, devilstrand 6.4, hyperweave 6.7, plainleather 5.5, bearskin 6.1, thrumbofur 6.5 | ◆ Headgear, ▲ Cool, ▲ Steadier aim, ◆ Hat | A solid piece of headgear; keeps its wearer cool; steadies the aim. |
| **6.4** |  | shadecone | Ideology | medieval | devilstrand | guns 1.4 | 2 / 12 | 4.1 / 7.8 | cloth 5.7, synthread 6.2, devilstrand 6.4, hyperweave 6.7, plainleather 5.4, bearskin 6.0, thrumbofur 6.5 | ◆ Headgear, ▲ Cool, ◆ Faith's apparel, ◆ Hat | A solid piece of headgear; keeps its wearer cool; a faith asks for it. |
| **6.3** |  | flat cap | Vanilla Factions Expanded - Settlers | industrial | devilstrand | guns 1.4, social 3.1 | 3 / 7.2 | 5.9 / 7.3 | cloth 5.9, synthread 6.2, devilstrand 6.3, hyperweave 6.6, plainleather 5.7, bearskin 6.1, thrumbofur 6.6 | ◆ Headgear, ▲ Cool, ▲ Good impression, ◆ Hat | A solid piece of headgear; keeps its wearer cool; makes a good impression. |
| **6.2** |  | baseball cap | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.4 | 2 / 9.6 | 4.6 / 7.6 | cloth 5.6, synthread 6.1, devilstrand 6.2, hyperweave 6.5, plainleather 5.3, bearskin 5.9, thrumbofur 6.4 | ◆ Headgear, ▲ Cool, ▲ Quick feet, ◆ Hat | A solid piece of headgear; keeps its wearer cool; quickens the step. |
| **6.2** |  | tribal headdress | Core | neolithic | devilstrand | guns 1.4, social 5.1 | 2 / 3.6 | 6.2 / 6.8 | cloth 6.0, synthread 6.2, devilstrand 6.2, hyperweave 6.4 | ◆ Headgear, ◆ Faith's apparel, ▲ Good impression, ▼ Little warmth, ◆ Hat | A solid piece of headgear; a faith asks for it; makes a good impression; little warmth. |
| **6.1** |  | hardhat | Vanilla Apparel Expanded | industrial |  | guns 1.0, work 4.8 | 0 / 4 | 5.1 / 7.0 |  | ◆ Headgear, ◆ Builder's, ▼ Little warmth, ◆ Hat | A solid piece of headgear; for a builder; little warmth. |
| **6.1** *(rough)* |  | plague mask | Vanilla Factions Expanded - Medieval 2 | medieval |  | guns 1.0, toxins 3.1 | 4 / 6 | 6.2 / 6.9 |  | ◆ Headgear, ▲ Cool, ▼ Flammable, ◆ For slaves, ✦ Its own effect, ◆ Mask or hood | A solid piece of headgear; keeps its wearer cool; catches fire easily. |
| **6.0** |  | deer skull | Vanilla Ideology Expanded - Hats and Rags | medieval | devilstrand | guns 1.0 | 6 / 6 | 6.7 / 6.7 | cloth 5.5, synthread 6.0, devilstrand 6.0, hyperweave 6.4, plainleather 5.2, bearskin 5.8, thrumbofur 6.6 | ◆ Headgear, ▲ Warm, ▲ Cool, ◆ Hat | A solid piece of headgear; warm in a blizzard; keeps its wearer cool. |
| **6.0** |  | fez | Vanilla Ideology Expanded - Hats and Rags | medieval | devilstrand | guns 1.2 | 0 / 12 | 1.3 / 7.8 | cloth 5.2, synthread 5.8, devilstrand 6.0, hyperweave 6.2, plainleather 4.9, bearskin 5.5, thrumbofur 5.8 | ◆ Headgear, ▲ Cool, ◆ Hat | A solid piece of headgear; keeps its wearer cool. |
| **6.0** |  | scarf | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.1 | 12 / 0 | 7.8 / 1.2 | cloth 5.7, synthread 6.2, devilstrand 6.0, hyperweave 6.6, plainleather 5.4, bearskin 6.0, thrumbofur 7.1 | ◆ Headgear, ▲ Warm, ◆ Mask or hood, ● Stuff matters | A solid piece of headgear; warm in a blizzard. |
| **5.7** |  | Stag Knight Helmet | ↁ Elves (Continued) | medieval | steel | guns 1.8, social 6.1 | 0.5 / 0 | 5.9 / 5.6 | steel 5.7, plasteel 5.8, uranium 5.8 | ◆ Headgear, ◆ Trader's, ▲ Good impression, ▼ Little warmth, ◆ Mask or hood | A solid piece of headgear; helps in talks and trade; makes a good impression; little warmth. |
| **5.6** |  | feral gas mask | Rimsenal Faction Pack - Feral | medieval | plainleather | guns 1.0, toxins 6.8, psychic shelter 4.9 | 1.6 / 4 | 5.2 / 6.6 | plainleather 5.6, bearskin 5.9, thrumbofur 6.3 | ◆ Headgear, ▼ Slows you, ✦ Psychic shield, ✦ Toxin-proof, ▼ Blocks psycasts, ▼ Off-putting, ▼ Little warmth | A solid piece of headgear; shields the mind from psychic attacks; keeps toxins out; slows its wearer. |
| **5.6** |  | flophat | Ideology | medieval | devilstrand | guns 1.4 | 10 / 0 | 7.6 / 1.5 | cloth 5.2, synthread 5.8, devilstrand 5.6, hyperweave 6.3 | ◆ Headgear, ▲ Warm, ◆ Hat | A solid piece of headgear; warm in a blizzard. |
| **5.6** |  | tuque | Core | medieval | devilstrand | guns 1.4 | 10 / 0 | 7.6 / 1.5 | cloth 5.2, synthread 5.8, devilstrand 5.6, hyperweave 6.3 | ◆ Headgear, ▲ Warm, ◆ Hat | A solid piece of headgear; warm in a blizzard. |
| **5.5** |  | beret | Royalty | medieval | devilstrand | guns 1.0, social 3.9 | 1.4 / 3.6 | 5.1 / 6.4 | cloth 5.2, synthread 5.5, devilstrand 5.5, hyperweave 5.7, plainleather 5.1, bearskin 5.3, thrumbofur 5.7 | ◆ Headgear, ◆ Royal, ◆ Faith's apparel, ▲ Good impression, ▼ Little warmth | A solid piece of headgear; fit for a title; a faith asks for it; little warmth. |
| **5.5** |  | coronet | Royalty | medieval | steel | guns 1.0, social 6.1 | 0 / 0 | 5.5 / 5.5 |  | ◆ Headgear, ◆ Royal, ◆ Trader's, ▲ Good impression, ▼ Little warmth | A solid piece of headgear; fit for a title; helps in talks and trade; little warmth. |
| **5.5** |  | crown | Royalty | medieval | steel | guns 1.0, social 6.1 | 0 / 0 | 5.5 / 5.5 |  | ◆ Headgear, ◆ Royal, ◆ Trader's, ▲ Good impression, ▼ Little warmth | A solid piece of headgear; fit for a title; helps in talks and trade; little warmth. |
| **5.5** |  | headscarf | Vanilla Ideology Expanded - Hats and Rags | medieval | devilstrand | guns 1.2 | 10 / 0 | 7.6 / 1.3 | cloth 5.2, synthread 5.8, devilstrand 5.5, hyperweave 6.2, plainleather 4.9, bearskin 5.5, thrumbofur 6.8 | ◆ Headgear, ▲ Warm, ◆ Hat, ● Stuff matters | A solid piece of headgear; warm in a blizzard. |
| **5.5** *(rough)* |  | stellic crown | Royalty | medieval | steel | guns 1.0, social 6.1 | 0 / 0 | 5.5 / 5.5 |  | ◆ Headgear, ◆ Royal, ◆ Trader's, ▲ Good impression, ✦ Its own effect, ▼ Little warmth | A solid piece of headgear; fit for a title; helps in talks and trade; little warmth. |
| **5.4** |  | fedora | Vanilla Apparel Expanded | industrial | devilstrand | guns 1.4, social 3.9 | 2 / 2.4 | 5.7 / 5.9 | cloth 5.1, synthread 5.4, devilstrand 5.4, hyperweave 5.7, plainleather 5.0, bearskin 5.3, thrumbofur 5.8 | ◆ Headgear, ▲ Good impression, ▼ Little warmth, ◆ Hat | A fair piece of headgear; makes a good impression; little warmth. |
| **5.3** *(rough)* |  | Mask of the Soulreaper | A RimWorld of Magic | medieval | devilstrand | guns 1.1, work 3.7, magic 2.4 | 4 / 3.4 | 6.1 / 5.7 | cloth 5.0, synthread 5.3, devilstrand 5.3, hyperweave 5.5, plainleather 4.9, bearskin 5.2, thrumbofur 5.6 | ◆ Headgear, ● Needs a mage, ✦ Enchanted, ▼ Off-putting, ◆ Researcher's, ✦ Its own effect, ▼ Little warmth | A fair piece of headgear; enchanted for a mage; for a researcher; puts people off. |
| **5.3** |  | veil | Core | neolithic | devilstrand | guns 1.7, toxins 4.3 | 1 / 1.2 | 5.4 / 5.5 | cloth 5.0, synthread 5.2, devilstrand 5.3, hyperweave 5.5 | ◆ Headgear, ◆ Faith's apparel, ▼ Little warmth, ◆ Mask or hood | A fair piece of headgear; a faith asks for it; little warmth. |
| **5.3** |  | wide hat | Vanilla Ideology Expanded - Hats and Rags | medieval | devilstrand | guns 1.0 | 2 / 7.2 | 4.1 / 7.0 | cloth 4.7, synthread 5.2, devilstrand 5.3, hyperweave 5.6, plainleather 4.4, bearskin 4.9, thrumbofur 5.5 | ◆ Headgear, ▲ Cool, ◆ Hat | A fair piece of headgear; keeps its wearer cool. |
| **5.2** |  | bandana | Vanilla Factions Expanded - Settlers | industrial | devilstrand | guns 1.1 | 6 / 0 | 7.0 / 3.0 | cloth 4.9, synthread 5.3, devilstrand 5.2, hyperweave 5.7, plainleather 4.8, bearskin 5.1, thrumbofur 6.2 | ◆ Headgear, ▲ Warm, ◆ Mask or hood | A fair piece of headgear; warm in a blizzard. |
| **5.2** |  | gag | Vanilla Anomaly Expanded - Insanity | medieval | devilstrand | guns 1.0 | 4 / 4.8 | 5.7 / 6.1 | cloth 4.7, synthread 5.2, devilstrand 5.2, hyperweave 5.6 | ◆ Headgear, ⚠ Wears fast, ◆ For slaves, ▼ Little warmth, ◆ Mask or hood | A fair piece of headgear; wears out fast; little warmth. |
| **5.2** |  | horse mask | Vanilla Ideology Expanded - Hats and Rags | medieval | devilstrand | guns 1.1 | 4 / 4.8 | 5.7 / 6.2 | cloth 4.7, synthread 5.2, devilstrand 5.2, hyperweave 5.7, plainleather 4.4, bearskin 4.9, thrumbofur 5.8 | ◆ Headgear, ▼ Little warmth, ◆ Mask or hood | A fair piece of headgear; little warmth. |
| **5.2** |  | padded helmet | Vanilla Factions Expanded - Medieval 2 | medieval | devilstrand | guns 2.1 | 8 / 0 | 7.3 / 2.0 | cloth 4.7, synthread 5.3, devilstrand 5.2, hyperweave 6.0 | ◆ Headgear, ▲ Warm, ◆ Hat | A fair piece of headgear; warm in a blizzard. |
| **4.9** |  | authority cap | Ideology | medieval | devilstrand | guns 1.4, social 3.1 | 2 / 2.4 | 5.2 / 5.5 | cloth 4.6, synthread 4.9, devilstrand 4.9, hyperweave 5.2, plainleather 4.5, bearskin 4.8, thrumbofur 5.4 | ◆ Headgear, ◆ Faith's apparel, ▼ Little warmth, ◆ Hat | A fair piece of headgear; a faith asks for it; little warmth. |
| **4.9** |  | commissar hat | Vanilla Ideology Expanded - Hats and Rags | medieval | devilstrand | guns 1.4, social 3.1 | 2 / 2.4 | 5.2 / 5.5 | cloth 4.6, synthread 4.9, devilstrand 4.9, hyperweave 5.2, plainleather 4.5, bearskin 4.8, thrumbofur 5.4 | ◆ Headgear, ▼ Little warmth, ◆ Hat | A fair piece of headgear; little warmth. |
| **4.9** |  | tailcap | Ideology | medieval | plainleather | guns 1.1 | 8 / 0 | 7.2 / 1.2 | plainleather 4.9, bearskin 5.5, thrumbofur 6.8 | ◆ Headgear, ▲ Warm, ◆ Faith's apparel, ◆ Hat, ● Stuff matters | A fair piece of headgear; warm in a blizzard; a faith asks for it. |
| **4.7** |  | face mask | Core | neolithic | devilstrand | guns 1.1, toxins 5.5 | 0.4 / 0.5 | 4.7 / 4.8 |  | ◆ Headgear, ✦ Toxin-proof, ⚠ Wears fast, ▼ Little warmth, ◆ Mask or hood | A fair piece of headgear; keeps toxins out; wears out fast; little warmth. |
| **4.4** |  | headbag | Vanilla Ideology Expanded - Hats and Rags | medieval | devilstrand | guns 1.0 | 4 / 2.4 | 5.7 / 4.4 | cloth 4.1, synthread 4.5, devilstrand 4.4, hyperweave 4.9, plainleather 3.9, bearskin 4.3, thrumbofur 5.2 | ◆ Headgear, ▼ Little warmth, ◆ Mask or hood | A modest piece of headgear; little warmth. |
| **4.3** |  | ghillie hood | Vanilla Armour Expanded | industrial |  | guns 1.0 | 3 / 0 | 5.8 / 3.2 |  | ◆ Headgear, ▼ Spoils your aim, ◆ Hunter's, ▼ Flammable, ▼ Little warmth, ◆ Hat | A modest piece of headgear; for a hunter; spoils the aim; catches fire easily. |
| **4.2** |  | sunglasses | Vanilla Apparel Expanded | industrial |  | guns 1.0, social 4.3 | 0 / 0 | 4.2 / 4.2 |  | ◆ Headgear, ▼ Flammable, ▲ Good impression, ▼ Little warmth, ◆ Eyewear | A modest piece of headgear; makes a good impression; catches fire easily; little warmth. |
| **4.1** |  | feral hood | Rimsenal Faction Pack - Feral | industrial | plainleather | guns 1.0 | 1.6 / 4 | 3.6 / 5.7 | plainleather 4.1, bearskin 4.6, thrumbofur 5.2 | ◆ Headgear, ▼ Flammable, ▼ Little warmth, ◆ Hat | A modest piece of headgear; catches fire easily; little warmth. |
| **3.9** |  | bishop hat | Vanilla Ideology Expanded - Hats and Rags | neolithic | devilstrand | guns 1.6 | 2 / 2.4 | 4.2 / 4.6 | cloth 3.4, synthread 3.8, devilstrand 3.9, hyperweave 4.3 | ◆ Headgear, ▼ Little warmth, ◆ Hat | A modest piece of headgear; little warmth. |
| **3.9** |  | headwrap | Ideology | neolithic | devilstrand | guns 1.6 | 2 / 2.4 | 4.2 / 4.6 | cloth 3.4, synthread 3.8, devilstrand 3.9, hyperweave 4.3 | ◆ Headgear, ◆ Faith's apparel, ▼ Little warmth, ◆ Hat | A modest piece of headgear; a faith asks for it; little warmth. |
| **3.9** |  | trilense glasses | Det's Xenotypes - Avaloi | industrial |  | guns 1.0, social 3.9 | 0 / 0 | 3.9 / 3.9 |  | ◆ Headgear, ▼ Flammable, ▲ Good impression, ▼ Little warmth, ◆ Eyewear | A modest piece of headgear; makes a good impression; catches fire easily; little warmth. |
| **3.9** |  | war mask | Core | neolithic | wood | guns 1.1 | 0.4 / 0.2 | 4.0 / 3.8 |  | ◆ Headgear, ▲ Shrugs off pain, ◆ Faith's apparel, ▼ Flammable, ▼ Little warmth, ◆ Mask or hood | A modest piece of headgear; keeps its wearer standing through pain; a faith asks for it; catches fire easily. |
| **3.8** |  | brimless hat | Vanilla Ideology Expanded - Hats and Rags | medieval | devilstrand | guns 1.4 | 2 / 2.4 | 4.1 / 4.5 | cloth 3.4, synthread 3.7, devilstrand 3.8, hyperweave 4.2, plainleather 3.2, bearskin 3.6, thrumbofur 4.3 | ◆ Headgear, ▼ Little warmth, ◆ Hat | A modest piece of headgear; little warmth. |
| **3.8** |  | military cap | Vanilla Ideology Expanded - Hats and Rags | medieval | devilstrand | guns 1.4 | 2 / 2.4 | 4.1 / 4.5 | cloth 3.4, synthread 3.7, devilstrand 3.8, hyperweave 4.2, plainleather 3.2, bearskin 3.6, thrumbofur 4.3 | ◆ Headgear, ▼ Little warmth, ◆ Hat | A modest piece of headgear; little warmth. |
| **2.9** |  | glasses | Vanilla Apparel Expanded | industrial |  | guns 1.0, psychic shelter 2.8 | 0 / 0 | 2.9 / 2.9 |  | ◆ Headgear, ✦ Psychic shield, ▼ Blocks psycasts, ▼ Flammable, ▼ Little warmth, ◆ Eyewear, ◆ Worn for looks | A weak piece of headgear; shields the mind from psychic attacks; dulls psycasts; catches fire easily. |
| **2.5** | 2.6 / 2.1 | stone war mask | Vanilla Armour Expanded | neolithic | granite blocks | guns 1.5 | 0 / 0 | 2.5 / 2.5 |  | ◆ Headgear, ▼ Slows you, ▲ Shrugs off pain, ▼ Little warmth, ◆ Mask or hood | A weak piece of headgear; keeps its wearer standing through pain; slows its wearer; little warmth. |
| **1.9** |  | plate helmet | Vanilla Armour Expanded | medieval | steel | guns 2.1 | 0.5 / 0 | 2.4 / 1.6 | steel 1.9, plasteel 2.3, uranium 2.2 | ◆ Headgear, ◆ Armour for the head, ▼ Spoils your aim, ▼ Little warmth, ◆ Mask or hood | A poor piece of headgear; spoils the aim; little warmth. |
| **1.7** |  | visage mask | Ideology | neolithic | steel | guns 1.6 | 0.2 / 0 | 1.9 / 1.6 | wood 1.8, steel 1.7, plasteel 1.9, uranium 1.9 | ◆ Headgear, ◆ Faith's apparel, ▼ Little warmth, ◆ Mask or hood | A poor piece of headgear; a faith asks for it; little warmth. |
| **1.5** |  | spiked skullcap | Vanilla Ideology Expanded - Hats and Rags | medieval | steel | guns 1.0 | 0.3 / 0 | 1.9 / 1.1 | wood 2.2, steel 1.5, plasteel 1.5, uranium 1.5 | ◆ Headgear, ▼ Little warmth, ◆ Hat, ◆ Worn for looks | A poor piece of headgear; little warmth. |
| **1.1** |  | skullcap | Vanilla Ideology Expanded - Hats and Rags | medieval | devilstrand | guns 1.0 | 0 / 0 | 1.1 / 1.1 | cloth 1.0, synthread 1.0, devilstrand 1.1, hyperweave 1.1, plainleather 1.1, bearskin 1.1, thrumbofur 1.1 | ◆ Headgear, ▼ Little warmth, ◆ Hat, ◆ Worn for looks | A poor piece of headgear; little warmth. |
| **1.1** |  | slicecap | Ideology | medieval | steel | guns 1.0 | 0 / 0 | 1.1 / 1.1 | wood 1.6, steel 1.1, plasteel 1.1, uranium 1.1 | ◆ Headgear, ◆ Faith's apparel, ▼ Little warmth, ◆ Hat | A poor piece of headgear; a faith asks for it; little warmth. |
| **1.0** |  | blindfold | Ideology | medieval | devilstrand | guns 1.0 | 7 / 0 | 1.0 / 1.0 |  | ◆ Headgear, ◆ For melee fighters, ▼ Blinds, ▲ Warm, ◆ Eyewear | A poor piece of headgear for a melee fighter; warm in a blizzard; its wearer can't see. |
| **1.0** |  | eyepatch | Alpha Memes | medieval |  | guns 1.0 | 1.8 / 7 | 1.0 / 1.0 |  | ◆ Headgear, ◆ For melee fighters, ▲ Cool, ▼ Flammable, ◆ Eyewear | A poor piece of headgear for a melee fighter; keeps its wearer cool; catches fire easily. |
| **1.0** |  | feral hood | Rimsenal Faction Pack - Feral | medieval | plainleather | guns 1.0 | 1.6 / 4 | 1.0 / 1.8 |  | ◆ Headgear, ▼ Off-putting, ▼ Flammable, ▼ Little warmth, ◆ Mask or hood | A poor piece of headgear; puts people off; catches fire easily; little warmth. |
| **1.0** |  | feral hood | Rimsenal Faction Pack - Feral | medieval | plainleather | guns 1.0 | 1.6 / 4 | 1.0 / 1.8 |  | ◆ Headgear, ▼ Off-putting, ▼ Flammable, ▼ Little warmth, ◆ Mask or hood | A poor piece of headgear; puts people off; catches fire easily; little warmth. |
| **1.0** |  | feral hood | Rimsenal Faction Pack - Feral | medieval | plainleather | guns 1.0 | 1.6 / 4 | 1.0 / 1.8 |  | ◆ Headgear, ▼ Off-putting, ▼ Flammable, ▼ Little warmth, ◆ Mask or hood | A poor piece of headgear; puts people off; catches fire easily; little warmth. |
| **1.0** |  | torture crown | Ideology | medieval | steel | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Headgear, ⚠ Hurts its wearer, ◆ Faith's apparel, ▼ Little warmth, ◆ Hat | A poor piece of headgear; a faith asks for it; hurts to wear; little warmth. |
| **1.0** |  | wreath | Vanilla Factions Expanded - Classical | medieval | steel | guns 1.0 | 0 / 0 | 1.0 / 1.0 |  | ◆ Headgear, ▼ Little warmth, ◆ Hat, ◆ Worn for looks | A poor piece of headgear; little warmth. |

### Ceremonial and psychic pieces (36)

| Rating | Shooter / melee | Piece | Mod | Age | Stuff | Sub-ratings | Cold / heat °C | In the cold / heat | By stuff | Tags | In plain words |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **8.7** |  | Robes of the Archmage | A RimWorld of Magic | medieval | devilstrand | guns 4.9, work 8.4, social 8.3, magic 7.2 | 20 / 22 | 8.8 / 8.8 | cloth 8.5, synthread 8.6, devilstrand 8.7, hyperweave 8.9, plainleather 8.5, bearskin 8.6, thrumbofur 8.9 | ◆ Psycaster's, ● Needs a mage, ▲ Warm, ◆ Made as armour, ▲ Fireproof, ▲ Cool, ✦ Enchanted | A superb ceremonial piece; warm in a blizzard; shrugs off fire; keeps its wearer cool. |
| **8.4** |  | Dreamcatcher Buckskin | A RimWorld of Magic | medieval | devilstrand | guns 5.9, psychic 6.3, work 6.8, social 6.1, magic 5.6 | 14 / 13.2 | 8.5 / 8.5 | cloth 8.1, synthread 8.2, devilstrand 8.4, hyperweave 8.6, plainleather 8.2, bearskin 8.3, thrumbofur 8.6 | ◆ Psycaster's, ● Needs a mage, ◆ Made as armour, ▲ Fireproof, ✦ Enchanted, ● Needs a psylink, ✦ Psychic | An excellent ceremonial piece; shrugs off fire; enchanted for a mage; lifts a psycaster. |
| **8.4** |  | forsaken psycaster helmet | Alpha Genes | ultra |  | guns 6.7, vacuum 7.1, psychic 7.9 | 8 / 6 | 8.5 / 8.4 |  | ◆ Psycaster's, ◆ For shooters, ▲ Turns blades, ✦ Vacuum-proof, ▲ Warm, ◆ Armour for the head, ● Needs a psylink | An excellent ceremonial piece for a shooter; turns blades and claws; keeps its wearer alive in vacuum; warm in a blizzard. |
| **8.0** |  | Mage Robe | A RimWorld of Magic | medieval | devilstrand | guns 1.8, psychic 7.5, magic 5.9 | 20 / 24 | 8.2 / 8.3 | cloth 7.8, synthread 7.9, devilstrand 8.0, hyperweave 8.1, plainleather 7.8, bearskin 7.9, thrumbofur 8.1 | ◆ Psycaster's, ● Needs a mage, ▲ Warm, ▲ Cool, ✦ Enchanted, ● Needs a psylink, ▼ Won't stop a bullet | An excellent ceremonial piece; warm in a blizzard; keeps its wearer cool; won't stop a bullet. |
| **8.0** |  | Mask of Serenity | A RimWorld of Magic | medieval | devilstrand | guns 2.8, psychic 7.3, work 7.9, magic 3.5 | 15 / 12.6 | 8.2 / 8.1 |  | ◆ Psycaster's, ● Needs a mage, ▲ Warm, ▲ Fireproof, ▲ Cool, ✦ Enchanted, ▼ Off-putting | An excellent ceremonial piece; warm in a blizzard; shrugs off fire; puts people off. |
| **7.8** |  | Cloak of Supremacy | A RimWorld of Magic | medieval | devilstrand | guns 1.1, work 2.9, magic 7.1 | 22 / 26 | 8.1 / 8.2 | cloth 7.7, synthread 7.8, devilstrand 7.8, hyperweave 7.9, plainleather 7.7, bearskin 7.8, thrumbofur 8.0 | ◆ Psycaster's, ● Needs a mage, ▲ Warm, ▲ Cool, ✦ Enchanted, ▲ Quick feet, ▲ Melee edge | An excellent ceremonial piece; warm in a blizzard; keeps its wearer cool; enchanted for a mage. |
| **7.8** |  | Disciple Robe | A RimWorld of Magic | medieval | devilstrand | guns 2.9, toxins 2.2, work 8.0, magic 4.4 | 20 / 24 | 8.1 / 8.2 | cloth 7.6, synthread 7.7, devilstrand 7.8, hyperweave 8.0, plainleather 7.6, bearskin 7.7, thrumbofur 8.0 | ◆ Psycaster's, ● Needs a mage, ▲ Warm, ▲ Cool, ✦ Enchanted, ▲ Quick feet, ● Needs a title | An excellent ceremonial piece; warm in a blizzard; keeps its wearer cool; enchanted for a mage. |
| **7.8** *(rough)* |  | Shroud of the Soulreaper | A RimWorld of Magic | medieval | devilstrand | guns 5.0, work 5.8, magic 6.6 | 20 / 4.4 | 8.2 / 7.7 | cloth 7.3, synthread 7.6, devilstrand 7.8, hyperweave 8.1, plainleather 7.5, bearskin 7.7, thrumbofur 8.2 | ◆ Psycaster's, ● Needs a mage, ▲ Warm, ◆ Made as armour, ▲ Fireproof, ✦ Enchanted, ▼ Off-putting | An excellent ceremonial piece; warm in a blizzard; shrugs off fire; puts people off. |
| **7.7** |  | Wanderer's Cloak | A RimWorld of Magic | medieval | devilstrand | guns 1.0, psychic 6.3, work 2.1, social 5.5, magic 3.8 | 23 / 8.8 | 8.2 / 7.7 | cloth 7.7, synthread 7.7, devilstrand 7.7, hyperweave 7.8, plainleather 7.7, bearskin 7.7, thrumbofur 7.8 | ◆ Psycaster's, ● Needs a mage, ▲ Warm, ✦ Enchanted, ● Needs a psylink, ✦ Psychic, ▲ Quick feet | An excellent ceremonial piece; warm in a blizzard; enchanted for a mage; lifts a psycaster. |
| **7.4** |  | Dreamcatcher Coronet | A RimWorld of Magic | medieval | devilstrand | guns 7.8, psychic 4.5, work 6.4, social 3.9, magic 4.2 | 9 / 10 | 7.6 / 7.6 | cloth 7.3, synthread 7.4, devilstrand 7.4, hyperweave 7.4, plainleather 7.3, bearskin 7.4, thrumbofur 7.5 | ◆ Psycaster's, ▲ Stops bullets, ▲ Turns blades, ▲ Holds off mechs, ● Needs a mage, ▲ Warm, ▲ Fireproof | A strong ceremonial piece; stops most bullets; turns blades and claws; holds up against mechanoids. |
| **7.2** |  | Ancient Mail | A RimWorld of Magic | medieval | steel | guns 3.0, psychic 7.9 | 10.8 / 10 | 7.5 / 7.5 | steel 7.2, plasteel 7.4, uranium 7.4 | ◆ Psycaster's, ▼ Slows you, ● Needs a mage, ◆ Made as armour, ✦ Enchanted, ● Needs a psylink, ✦ Psychic | A strong ceremonial piece; enchanted for a mage; lifts a psycaster; slows its wearer. |
| **7.1** |  | Arcane Hood | A RimWorld of Magic | medieval | devilstrand | guns 1.6, psychic 5.5, magic 3.0 | 15 / 16 | 7.4 / 7.5 |  | ◆ Psycaster's, ● Needs a mage, ▲ Warm, ▲ Cool, ✦ Enchanted, ● Needs a psylink, ✦ Psychic | A strong ceremonial piece; warm in a blizzard; keeps its wearer cool; enchanted for a mage. |
| **7.0** |  | eltex skullcap | Royalty | ultra |  | guns 1.0, psychic 7.8 | 0 / 0 | 7.0 / 7.0 |  | ◆ Psycaster's, ● Needs a psylink, ✦ Psychic, ● Needs a title, ◆ Royal, ▼ Little warmth, ◆ Hat | A strong ceremonial piece; lifts a psycaster; fit for a title; little warmth. |
| **7.0** |  | Hood of Madness | A RimWorld of Magic | medieval | devilstrand | guns 2.2, psychic 7.5, work 7.9, magic 3.0 | 12 / 13.6 | 7.3 / 7.4 | cloth 6.9, synthread 6.9, devilstrand 7.0, hyperweave 7.1, plainleather 6.8, bearskin 6.9, thrumbofur 7.1 | ◆ Psycaster's, ● Needs a mage, ▲ Warm, ▲ Cool, ✦ Enchanted, ● Needs a psylink, ▼ Off-putting | A strong ceremonial piece; warm in a blizzard; keeps its wearer cool; puts people off. |
| **6.8** *(rough)* |  | blue laced dress | Fairycore - Apparel 2 | medieval | devilstrand | guns 2.8, psychic 6.5, social 3.9 | 12.1 / 9 | 7.2 / 7.1 | cloth 6.4, synthread 6.7, devilstrand 6.8, hyperweave 7.1, plainleather 6.6, bearskin 6.7, thrumbofur 7.1 | ◆ Psycaster's, ▲ Warm, ● Needs a psylink, ✦ Psychic, ● Women only, ▲ Good impression, ⚠ Wrong-gender mood | A strong ceremonial piece; warm in a blizzard; lifts a psycaster; makes a good impression. |
| **6.8** *(rough)* |  | orange scarf dress | Fairycore - Apparel 2 | medieval | devilstrand | guns 2.8, psychic 6.5, social 3.9 | 12.1 / 9 | 7.2 / 7.1 | cloth 6.4, synthread 6.7, devilstrand 6.8, hyperweave 7.1, plainleather 6.6, bearskin 6.7, thrumbofur 7.1 | ◆ Psycaster's, ▲ Warm, ● Needs a psylink, ✦ Psychic, ● Women only, ▲ Good impression, ⚠ Wrong-gender mood | A strong ceremonial piece; warm in a blizzard; lifts a psycaster; makes a good impression. |
| **6.8** *(rough)* |  | purple travel robe | Fairycore - Apparel 2 | medieval | devilstrand | guns 2.8, psychic 6.5, social 3.9 | 12.1 / 9 | 7.2 / 7.1 | cloth 6.4, synthread 6.7, devilstrand 6.8, hyperweave 7.1, plainleather 6.6, bearskin 6.7, thrumbofur 7.1 | ◆ Psycaster's, ▲ Warm, ● Needs a psylink, ✦ Psychic, ● Women only, ▲ Good impression, ⚠ Wrong-gender mood | A strong ceremonial piece; warm in a blizzard; lifts a psycaster; makes a good impression. |
| **6.8** *(rough)* |  | rust travel dress | Fairycore - Apparel 2 | medieval | devilstrand | guns 2.8, psychic 6.5, social 3.9 | 12.1 / 9 | 7.2 / 7.1 | cloth 6.4, synthread 6.7, devilstrand 6.8, hyperweave 7.1, plainleather 6.6, bearskin 6.7, thrumbofur 7.1 | ◆ Psycaster's, ▲ Warm, ● Needs a psylink, ✦ Psychic, ● Women only, ▲ Good impression, ⚠ Wrong-gender mood | A strong ceremonial piece; warm in a blizzard; lifts a psycaster; makes a good impression. |
| **6.8** *(rough)* |  | white-blue dress | Fairycore - Apparel 2 | medieval | devilstrand | guns 2.8, psychic 6.5, social 3.9 | 12.1 / 9 | 7.2 / 7.1 | cloth 6.4, synthread 6.7, devilstrand 6.8, hyperweave 7.1, plainleather 6.6, bearskin 6.7, thrumbofur 7.1 | ◆ Psycaster's, ▲ Warm, ● Needs a psylink, ✦ Psychic, ● Women only, ▲ Good impression, ⚠ Wrong-gender mood | A strong ceremonial piece; warm in a blizzard; lifts a psycaster; makes a good impression. |
| **6.7** |  | Disciple Hood | A RimWorld of Magic | medieval | devilstrand | guns 2.0, work 4.9, magic 4.5 | 13 / 12 | 7.1 / 7.0 | cloth 6.6, synthread 6.6, devilstrand 6.7, hyperweave 6.7, plainleather 6.6, bearskin 6.6, thrumbofur 6.8 | ◆ Psycaster's, ● Needs a mage, ▲ Warm, ▲ Cool, ✦ Enchanted, ▲ Steady nerves, ◆ Work clothes | A strong ceremonial piece; warm in a blizzard; keeps its wearer cool; enchanted for a mage. |
| **6.7** *(rough)* |  | rose witch hat | Fairycore - Apparel 2 | medieval | devilstrand | guns 2.3, psychic 6.5, social 3.9 | 4.6 / 29.2 | 6.2 / 7.8 | cloth 6.6, synthread 6.7, devilstrand 6.7, hyperweave 6.8, plainleather 6.6, bearskin 6.7, thrumbofur 6.7 | ◆ Psycaster's, ▲ Cool, ● Needs a psylink, ✦ Psychic, ▲ Good impression, ✦ Its own effect, ◆ Hat | A strong ceremonial piece; keeps its wearer cool; lifts a psycaster; makes a good impression. |
| **6.6** |  | Alarielle Armor (F) | ↁ Elves (Continued) | medieval | steel | guns 3.0, psychic 6.4, social 6.1 | 3.3 / 0 | 6.8 / 6.5 | steel 6.6, plasteel 6.9, uranium 6.8 | ◆ Psycaster's, ◆ Made as armour, ● Needs a psylink, ✦ Psychic, ● Needs a title, ▲ Good impression, ▼ Little warmth | A strong ceremonial piece; lifts a psycaster; makes a good impression; little warmth. |
| **6.6** |  | Morathi Armor (F) | ↁ Elves (Continued) | medieval | steel | guns 3.0, psychic 6.4, social 6.1 | 3.3 / 0 | 6.8 / 6.5 | steel 6.6, plasteel 6.9, uranium 6.8 | ◆ Psycaster's, ◆ Made as armour, ● Needs a psylink, ✦ Psychic, ● Needs a title, ▲ Good impression, ▼ Little warmth | A strong ceremonial piece; lifts a psycaster; makes a good impression; little warmth. |
| **6.2** |  | Alarielle Robe (F) | ↁ Elves (Continued) | medieval | devilstrand | guns 1.6, psychic 6.4, social 5.1 | 7 / 3.6 | 6.6 / 6.3 | cloth 5.9, synthread 6.1, devilstrand 6.2, hyperweave 6.5 | ◆ Psycaster's, ● Needs a psylink, ✦ Psychic, ● Women only, ● Needs a title, ▼ Won't stop a bullet, ▲ Good impression | A solid ceremonial piece; lifts a psycaster; makes a good impression; won't stop a bullet. |
| **6.2** |  | Morathi Robe (F) | ↁ Elves (Continued) | medieval | devilstrand | guns 1.6, psychic 6.4, social 5.1 | 7 / 3.6 | 6.6 / 6.3 | cloth 5.9, synthread 6.1, devilstrand 6.2, hyperweave 6.5, plainleather 6.0, bearskin 6.1, thrumbofur 6.6 | ◆ Psycaster's, ● Needs a psylink, ✦ Psychic, ● Women only, ● Needs a title, ▼ Won't stop a bullet, ▲ Good impression | A solid ceremonial piece; lifts a psycaster; makes a good impression; won't stop a bullet. |
| **6.2** |  | Sorceress Robe (F) | ↁ Elves (Continued) | medieval | devilstrand | guns 1.6, psychic 6.4, social 5.1 | 7 / 3.6 | 6.6 / 6.3 | cloth 5.9, synthread 6.1, devilstrand 6.2, hyperweave 6.5 | ◆ Psycaster's, ● Needs a psylink, ✦ Psychic, ● Women only, ● Needs a title, ▼ Won't stop a bullet, ▲ Good impression | A solid ceremonial piece; lifts a psycaster; makes a good impression; won't stop a bullet. |
| **6.0** |  | eltex robe | Royalty | ultra |  | guns 1.0, psychic 6.6 | 15 / 1 | 7.0 / 5.4 |  | ◆ Psycaster's, ● Needs a psylink, ✦ Psychic, ● Needs a title, ◆ Royal, ▼ Won't stop a bullet, ◆ Coat | A solid ceremonial piece; lifts a psycaster; fit for a title; won't stop a bullet. |
| **5.8** |  | Alarielle Crown | ↁ Elves (Continued) | medieval | steel | guns 3.6, psychic 6.0, social 6.8 | 0.8 / 0 | 5.8 / 5.7 |  | ◆ Psycaster's, ◆ Armour for the head, ● Needs a psylink, ✦ Psychic, ● Needs a title, ▲ Good impression, ▼ Little warmth | A solid ceremonial piece; lifts a psycaster; makes a good impression; little warmth. |
| **5.8** |  | Morathi Crown | ↁ Elves (Continued) | medieval | steel | guns 3.6, psychic 6.0, social 6.8 | 0.8 / 0 | 5.8 / 5.7 |  | ◆ Psycaster's, ◆ Armour for the head, ● Needs a psylink, ✦ Psychic, ● Needs a title, ▲ Good impression, ▼ Little warmth | A solid ceremonial piece; lifts a psycaster; makes a good impression; little warmth. |
| **4.8** |  | Sorceress Crown | ↁ Elves (Continued) | medieval |  | guns 1.0, psychic 5.4, social 5.1 | 0 / 0 | 4.8 / 4.8 |  | ◆ Psycaster's, ● Needs a psylink, ✦ Psychic, ● Needs a title, ◆ Royal, ▲ Good impression, ▼ Little warmth | A fair ceremonial piece; lifts a psycaster; fit for a title; little warmth. |
| **4.6** |  | eltex helmet | Royalty | ultra |  | guns 1.0, psychic 5.7 | 4 / 1 | 5.1 / 4.5 |  | ◆ Psycaster's, ● Needs a psylink, ✦ Psychic, ● Needs a title, ◆ Royal, ▼ Little warmth, ◆ Hat | A fair ceremonial piece; lifts a psycaster; fit for a title; little warmth. |
| **4.5** |  | eltex vest | Royalty | ultra |  | guns 1.0, psychic 5.4 | 6 / 1 | 5.3 / 4.3 |  | ◆ Psycaster's, ● Needs a psylink, ✦ Psychic, ● Needs a title, ◆ Royal, ▼ Won't stop a bullet, ◆ Top | A fair ceremonial piece; lifts a psycaster; fit for a title; won't stop a bullet. |
| **4.2** |  | eltex cape | Vanilla Psycasts Expanded | medieval |  | guns 1.6, psychic 4.6 | 6 / 1 | 4.9 / 4.0 |  | ◆ Psycaster's, ● Needs a psylink, ✦ Psychic, ● Needs a title, ◆ Royal, ▼ Won't stop a bullet, ◆ Faith's apparel | A modest ceremonial piece; lifts a psycaster; fit for a title; won't stop a bullet. |
| **4.0** |  | king's crown | Vanilla Factions Expanded - Medieval 2 | medieval | steel | guns 1.2, psychic 3.0, social 8.4 | 2 / 4 | 4.1 / 4.5 |  | ◆ Ceremonial, ▼ Bars work, ● Needs a psylink, ✦ Psychic, ● Needs a title, ◆ Royal, ▲ Good impression | A modest ceremonial piece; lifts a psycaster; fit for a title; bars some work. |
| **3.9** |  | eltex mask | Vanilla Psycasts Expanded | medieval |  | guns 1.1, psychic 4.6 | 4 / 1 | 4.3 / 3.8 |  | ◆ Psycaster's, ● Needs a psylink, ✦ Psychic, ● Needs a title, ◆ Royal, ▼ Little warmth, ◆ Mask or hood | A modest ceremonial piece; lifts a psycaster; fit for a title; little warmth. |
| **3.6** |  | eltex shirt | Royalty | ultra |  | guns 1.0, psychic 4.2 | 5 / 1 | 4.2 / 3.5 |  | ◆ Psycaster's, ● Needs a psylink, ✦ Psychic, ● Needs a title, ◆ Royal, ▼ Won't stop a bullet, ◆ Top | A modest ceremonial piece; lifts a psycaster; fit for a title; won't stop a bullet. |

### For children (9)

| Rating | Shooter / melee | Piece | Mod | Age | Stuff | Sub-ratings | Cold / heat °C | In the cold / heat | By stuff | Tags | In plain words |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **7.8** |  | forsaken kid hood | Alpha Genes | spacer |  | guns 4.4, vacuum 7.0, psychic shelter 4.2 | 12 / 4 | 8.2 / 7.9 |  | ◆ Helmet, ● Children only, ▲ Warm, ✦ Vacuum-proof, ✦ Psychic shield, ◆ Royal, ▼ Blocks psycasts | An excellent helmet; warm in a blizzard; keeps its wearer alive in vacuum; dulls psycasts. |
| **7.4** |  | forsaken kid cloak | Alpha Genes | spacer |  | guns 7.6, hunting 2.8 | 24 / 8 | 7.7 / 7.5 |  | ◆ Combat armour, ▲ Stops bullets, ● Children only, ▲ Warm, ▲ Full cover, ◆ Royal, ▲ Light | A strong body armour; stops most bullets; warm in a blizzard; covers nearly everything. |
| **7.3** |  | kid tribalwear | Biotech | neolithic | devilstrand | guns 1.4 | 10 / 12 | 7.6 / 8.0 | cloth 6.5, synthread 7.1, devilstrand 7.3, hyperweave 7.8, plainleather 6.2, bearskin 6.9, thrumbofur 7.9 | ◆ Everyday wear, ● Children only, ▲ Warm, ▲ Cool, ▼ Won't stop a bullet, ◆ Full outfit, ● Stuff matters | A strong everyday garment; warm in a blizzard; keeps its wearer cool; won't stop a bullet. |
| **6.0** |  | kid parka | Biotech | neolithic | devilstrand | guns 1.3 | 30 / 0 | 8.1 / 1.1 | cloth 5.3, synthread 6.3, devilstrand 6.0, hyperweave 7.1, plainleather 4.8, bearskin 5.9, thrumbofur 7.8 | ◆ Cold-weather, ● Children only, ▲ Warm, ▼ Won't stop a bullet, ◆ Coat, ◆ Top, ● Stuff matters | A solid piece of cold-weather gear; warm in a blizzard; won't stop a bullet. |
| **5.9** | 6.1 / 5.4 | kid vacsuit | Odyssey | spacer |  | guns 2.8, vacuum 4.7 | 90 / 15 | 8.6 / 4.4 |  | ◆ Hazard gear, ● Children only, ▼ Slows you, ▲ Warm, ◆ Made as armour, ▲ Cool, ◆ Spacefarer's | A solid hazard suit; warm in a blizzard; keeps its wearer cool; slows its wearer. |
| **4.5** |  | kid romper | Biotech | medieval | devilstrand | guns 1.5 | 4.4 / 2.4 | 5.9 / 4.4 | cloth 3.3, synthread 3.9, devilstrand 4.5, hyperweave 6.0, plainleather 3.2, bearskin 3.9, thrumbofur 6.3 | ◆ Everyday wear, ● Children only, ▼ Won't stop a bullet, ◆ Full outfit, ● Stuff matters | A fair everyday garment; won't stop a bullet. |
| **4.0** |  | kid shirt | Biotech | medieval | devilstrand | guns 1.2 | 4.4 / 2.4 | 5.4 / 3.9 | cloth 3.2, synthread 3.7, devilstrand 4.0, hyperweave 4.9, plainleather 3.1, bearskin 3.7, thrumbofur 5.3 | ◆ Everyday wear, ● Children only, ▼ Won't stop a bullet, ◆ Top, ● Stuff matters | A modest everyday garment; won't stop a bullet. |
| **3.6** |  | kid pants | Biotech | medieval | devilstrand | guns 1.2 | 4 / 1.9 | 5.0 / 3.4 | cloth 3.0, synthread 3.5, devilstrand 3.6, hyperweave 4.4, plainleather 2.9, bearskin 3.4, thrumbofur 4.7 | ◆ Everyday wear, ● Children only, ▼ Won't stop a bullet, ◆ Legwear, ● Stuff matters | A modest everyday garment; won't stop a bullet. |
| **3.5** |  | kid helmet | Biotech | industrial | steel | guns 2.0 | 0.5 / 0 | 3.9 / 3.4 | steel 3.5, plasteel 4.1, uranium 3.9 | ◆ Helmet, ● Children only, ▲ Light, ▼ Little warmth, ◆ Worn for looks | A modest helmet; for little weight; little warmth. |

## Where the numbers are less sure

63 pieces do part of what they do through a mod's own code or a patch; each is rated on what can be read and marked
*(rough)*. The reasons, by piece:

- **Amulet of Minor Protection** (A RimWorld of Magic): its enchantment works by A RimWorld of Magic's own code (an action or an ability), named, not counted.
- **Amulet of Protection** (A RimWorld of Magic): its enchantment works by A RimWorld of Magic's own code (an action or an ability), named, not counted.
- **Bracers of Defense** (A RimWorld of Magic): its hediff Blocking works by its mod's own code.
- **Bracers of Deflection** (A RimWorld of Magic): its hediff Deflective Blocking works by its mod's own code.
- **Bracers of the Pacifist** (A RimWorld of Magic): its hediff Arcane Recovery works by its mod's own code.
- **Charm of Health** (A RimWorld of Magic): its hediff Improved Health works by its mod's own code.
- **Charm of Rest** (A RimWorld of Magic): its hediff Renewal works by its mod's own code.
- **Cowl of the Soulreaper** (A RimWorld of Magic): its hediff Shroud of Undeath works by its mod's own code.
- **Mask of the Soulreaper** (A RimWorld of Magic): its hediff Shroud of Undeath works by its mod's own code.
- **Mind Ward Pendant** (A RimWorld of Magic): its hediff Mind Ward works by its mod's own code.
- **Necrotic Orb** (A RimWorld of Magic): its enchantment works by A RimWorld of Magic's own code (an action or an ability), named, not counted.
- **Orb Of Conviction** (A RimWorld of Magic): its enchantment works by A RimWorld of Magic's own code (an action or an ability), named, not counted; its AbilityItem is A RimWorld of Magic's own code, named, not counted.
- **Orb of Souls (empty)** (A RimWorld of Magic): its enchantment works by A RimWorld of Magic's own code (an action or an ability), named, not counted; its AbilityItem is A RimWorld of Magic's own code, named, not counted.
- **Orb of Souls (filled)** (A RimWorld of Magic): its enchantment works by A RimWorld of Magic's own code (an action or an ability), named, not counted; its AbilityItem is A RimWorld of Magic's own code, named, not counted.
- **Pendant of Mending** (A RimWorld of Magic): its hediff Mending works by its mod's own code.
- **Quicksilver Pendant** (A RimWorld of Magic): its enchantment works by A RimWorld of Magic's own code (an action or an ability), named, not counted.
- **Ring of Arcane Recovery** (A RimWorld of Magic): its hediff Arcane Recovery works by its mod's own code.
- **Ring of Authority** (A RimWorld of Magic): its enchantment works by A RimWorld of Magic's own code (an action or an ability), named, not counted.
- **Ring of Blood** (A RimWorld of Magic): its hediff Blood Boost works by its mod's own code.
- **Ring of Malice** (A RimWorld of Magic): its hediff Hate Boost works by its mod's own code.
- **Ring of the Eternal Blue** (A RimWorld of Magic): its hediff Psionic Boost works by its mod's own code.
- **Ring of the Pathfinder** (A RimWorld of Magic): its hediff Pathfinding works by its mod's own code.
- **Shroud of the Soulreaper** (A RimWorld of Magic): its hediff Shroud of Undeath works by its mod's own code.
- **biomutation lance** (Anomaly): its biomutation lance (Verb_CastTargetEffectBiomutationLance) isn't valued.
- **disruptor flare pack** (Anomaly): its deploy flare (Verb_LaunchProjectileStaticPsychic) isn't valued.
- **bandwidth pack** (Biotech): Rimsenal Faction Pack - Spacer patches its comps (rated from before the patch).
- **mechcommander helmet** (Biotech): Rimsenal Faction Pack - Spacer patches its comps (rated from before the patch); Vanilla Gravship Expanded - Chapter 1 patches its equippedStatOffsets/VacuumResistance (rated from before the patch).
- **mechlord helmet** (Biotech): Vanilla Gravship Expanded - Chapter 1 patches its equippedStatOffsets (rated from before the patch).
- **knuckle dusters** (Det's Xenotypes - Boglegs): its hediff knuckle dusters works by its mod's own code.
- **blue laced dress** (Fairycore - Apparel 2): its quality-scaled bonuses (Fairycore's own code, not in the upload) are read at normal quality.
- **orange scarf dress** (Fairycore - Apparel 2): its quality-scaled bonuses (Fairycore's own code, not in the upload) are read at normal quality.
- **purple travel robe** (Fairycore - Apparel 2): its quality-scaled bonuses (Fairycore's own code, not in the upload) are read at normal quality.
- **rose witch hat** (Fairycore - Apparel 2): its quality-scaled bonuses (Fairycore's own code, not in the upload) are read at normal quality.
- **rust travel dress** (Fairycore - Apparel 2): its quality-scaled bonuses (Fairycore's own code, not in the upload) are read at normal quality.
- **white-blue dress** (Fairycore - Apparel 2): its quality-scaled bonuses (Fairycore's own code, not in the upload) are read at normal quality.
- **skip beacon** (MigCorp - Skiptech): what it does is its mod's own code (not in the upload).
- **vacsuit helmet** (Odyssey): Vanilla Gravship Expanded - Chapter 1 patches its equippedStatOffsets/VacuumResistance (rated from before the patch).
- **Apricorn Ball belt** (PokéWorld Updated [1.6] [ALPHA]): its Apricorn Ball (Verb_LaunchPokeball) isn't valued; PokeWorld's own code isn't in the upload.
- **Great Ball belt** (PokéWorld Updated [1.6] [ALPHA]): its Great Ball (Verb_LaunchPokeball) isn't valued; PokeWorld's own code isn't in the upload.
- **Master Ball belt** (PokéWorld Updated [1.6] [ALPHA]): its Master Ball (Verb_LaunchPokeball) isn't valued; PokeWorld's own code isn't in the upload.
- **Poké Ball belt** (PokéWorld Updated [1.6] [ALPHA]): its Poké Ball (Verb_LaunchPokeball) isn't valued; PokeWorld's own code isn't in the upload.
- **Ultra Ball belt** (PokéWorld Updated [1.6] [ALPHA]): its Ultra Ball (Verb_LaunchPokeball) isn't valued; PokeWorld's own code isn't in the upload.
- **fortification pack** (Rimsenal - Security Pack): its DefensePack is Rimsenal Security's own code, named, not counted.
- **omnifoam pack** (Rimsenal Faction Pack - Spacer): its launch omnifoam canister (Verb_LaunchProjectileStatic) isn't valued.
- **orbital mech cluster targeter** (Royalty): its Verb_MechCluster (Verb_MechCluster) isn't valued.
- **stellic crown** (Royalty): Vanilla Factions Expanded - Empire patches its statBases (rated from before the patch).
- **mini-turret pack** (Vanilla Apparel Expanded - Accessories): its gun fires by Vanilla Expanded's own turret code.
- **prestige siegebreaker helmet** (Vanilla Armour Expanded): Vanilla Gravship Expanded - Chapter 1 patches its equippedStatOffsets/VacuumResistance (rated from before the patch).
- **prestige trooper helmet** (Vanilla Armour Expanded): Vanilla Gravship Expanded - Chapter 1 patches its equippedStatOffsets/VacuumResistance (rated from before the patch).
- **siegebreaker helmet** (Vanilla Armour Expanded): Vanilla Gravship Expanded - Chapter 1 patches its equippedStatOffsets/VacuumResistance (rated from before the patch).
- **trooper helmet** (Vanilla Armour Expanded): Vanilla Gravship Expanded - Chapter 1 patches its equippedStatOffsets/VacuumResistance (rated from before the patch).
- **bomb pack** (Vanilla Factions Expanded - Deserters): its Deployable is VFE Deserters's own code, named, not counted.
- **deserter declassifier** (Vanilla Factions Expanded - Deserters): its IntelExtractor is VFE Deserters's own code, named, not counted.
- **invisibility engulfer** (Vanilla Factions Expanded - Deserters): its InvisibilityEngulfer is VFE Deserters's own code, named, not counted.
- **absolver helmet** (Vanilla Factions Expanded - Empire): Vanilla Gravship Expanded - Chapter 1 patches its equippedStatOffsets/VacuumResistance (rated from before the patch).
- **deserter helmet** (Vanilla Factions Expanded - Empire): Vanilla Gravship Expanded - Chapter 1 patches its equippedStatOffsets/VacuumResistance (rated from before the patch).
- **janissary helmet** (Vanilla Factions Expanded - Empire): Vanilla Gravship Expanded - Chapter 1 patches its equippedStatOffsets/VacuumResistance (rated from before the patch).
- **techfriar crown** (Vanilla Factions Expanded - Empire): its ResearchRefresh is VFE Empire's own code, named, not counted; Vanilla Gravship Expanded - Chapter 1 patches its equippedStatOffsets/VacuumResistance (rated from before the patch).
- **plague mask** (Vanilla Factions Expanded - Medieval 2): its hediff plague mask works by its mod's own code.
- **astrorig** (Vanilla Gravship Expanded - Chapter 2): its ApparelVerbOwnerOxygen is Vanilla Gravship Expanded's own code, named, not counted.
- **gravship bombardment targeter** (Vanilla Gravship Expanded - Chapter 2): it aims the colony's gravship guns (Vanilla Gravship Expanded's own code): counted as an orbital strike.
- **tackle box** (Vanilla Ideology Expanded - Memes and Structures): what it does is its mod's own code (not in the upload); Vanilla Ideology Expanded - Memes and Structures patches its equippedStatOffsets (rated from before the patch).
- **psyring** (Vanilla Psycasts Expanded): a psyring carries a psycast chosen when it is made (Vanilla Psycasts Expanded).

## Patches the review doesn't apply

Patches from the active mods that touch an apparel def (its stats, what it changes on its wearer, its comps, its tags or
layers). The ratings are from before them:

- **ↁ Elves (Continued)** changes its tags (what tags and titles read; no change to the numbers rated): pants.
- **Rimsenal Faction Pack - Spacer** adds or changes its parts: bandwidth pack, mechcommander helmet.
- **Vanilla Apparel Expanded - Accessories** changes its apparel fields (what tags and titles read; no change to the numbers rated): shield belt.
- **Vanilla Armour Expanded** changes what it can be made of: plate armor.
- **Vanilla Factions Expanded - Empire** changes its stats (statBases): stellic crown.
- **Vanilla Factions Expanded - Medieval 2** changes its tags (what tags and titles read; no change to the numbers rated): button-down shirt, pants.
- **Vanilla Factions Expanded - Settlers** changes its tags (what tags and titles read; no change to the numbers rated): great dress, vest.
- **Vanilla Gravship Expanded - Chapter 1** changes what it does to its wearer (equippedStatOffsets): mechlord helmet.
- **Vanilla Gravship Expanded - Chapter 1** changes what it does to its wearer (equippedStatOffsets/VacuumResistance): absolver helmet, deserter helmet, janissary helmet, mechcommander helmet, prestige siegebreaker helmet, prestige trooper helmet, siegebreaker helmet, techfriar crown, trooper helmet, vacsuit helmet.
- **Vanilla Ideology Expanded - Memes and Structures** changes what it does to its wearer (equippedStatOffsets): tackle box.

## Left out

- **worn only by what it was made for: it vanishes when taken off (a mechanoid's, a power's)** (3): disposable oxygen pack (Vanilla Gravship Expanded - Chapter 2), heavy shield unit (Biotech), mechanite shield unit (Alpha Mechs).
- No apparel in the player's active mods is made for animals or other bodies: every piece left is worn by people (the
  children's pieces have their own table).

## Rebuilding this page

```
python3 Tools/apparel_extract.py <the def collection: Data, Mods, mods.txt, ModsConfig.xml> docs/player-mods/apparel_numbers.json.gz
dotnet run -c Release --project Tools/ApparelReview
```
