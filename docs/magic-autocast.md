# Casting on their own (A RimWorld of Magic)

The player, 28 November 2026: "I'm a bit overwhelmed with the magic spells ... I'd like to integrate some more
auto-casting ... at least for the base damage spells ... he doesn't use it for hunting", and "for example, shadow
strike, this one ability would also be great if you used it for hunting". Their answers to the proposal: build it,
Shadow Strike only on safe prey, no fire spells on hunts, keep a 30% reserve, never area spells, and leave the magic
mod's own per-spell auto-cast marks alone.

Code: `Source/ColonyCouncil/Integrations/MagicAutoCast.cs` (the pass, the button, the book),
`MagicAutoCastMath.cs` (the rules, tested in `Tools/Tests/MagicAutoCastTests.cs`), `MagicAutoCastDebug.cs` (a dev log),
and the reflection in `RomSpells` (`Peoples/Magic/PeoplesMagic.cs`). Words: `Languages/English/Keyed/Rimfolk_MagicAutoCast.xml`.

## What it does

- **One switch per colonist.** A colonist of the colony's own (free, not a slave) who knows at least one of the spells
  below gets a button, *Casts on their own*, off until the player turns it on. Its description lists the spells that
  colonist would use. The ids switched on are saved (`MagicAutoCastBook.on`, 60 at most, let go once a day when someone
  is no longer one of the colony's free people). The setting *Mages may cast damage spells on their own* (Your other
  mods, A RimWorld of Magic; on by default, under the deeper hooks) turns the whole thing off and hides the button.
- **The pass.** Every 250 ticks (`Sched.RareMagicAutoCast` = 103, timed as `Pass.RareMagicAutoCast`), only with the
  mod, the setting on and someone switched on (or a cast still followed), each switched-on colonist on any map who is
  up, awake, capable of violence, not in a mental state, not stunned and not casting already is looked at.
- **The reserve.** A spell is cast only if at least 30% of the pool it is paid from is left after its cost: mana for
  the magic spells, stamina for the fighters' techniques. The cost is the mod's own (`ActualManaCost`,
  `ActualStaminaCost`, with the colonist's efficiency skills). Myst (mana 0.75): Arcane Bolt from a full pool, again
  from two thirds, not a third time; Shadow Strike's 0.65 stamina only from a nearly full pool.
- **On a hunt** (their job is the game's Hunt): once per prey (remembered a day), the hunt opens with a spell. Never a
  fire spell. Shadow Strike only on safe prey, and then it comes first: the game's chance that the animal turns on its
  hunter from up close (`PawnUtility.GetManhunterOnDamageChance(prey, hunter, 1)`) under 0.015 (the hunt designator's
  warning), not a predator, not a person, and at most half again the hunter's body size. Nobody but the prey may stand
  within the spell's reach of it, or in the line (other wild animals too: a stray hit could turn them).
- **In a fight.** Drafted with fire at will on (not while manning a turret or walking where they were sent), or not
  drafted while already fighting, or with "attack" as their response to danger and nothing to break off (not fleeing,
  rescuing, capturing, tending, eating, sleeping, carrying, or on an order of the player's). The target is the enemy
  they fight (their attack's target, else the one their mind is set on), else the nearest pawn attacking the colony that
  they see within their spells' range. Enemies are those hostile to them and an active threat to the colony: raiders,
  manhunters, the berserk. Shadow Strike only on an enemy with no other enemy within 6 cells of it. No friend (anyone
  not hostile to the colony, the downed included) within the spell's reach of the target or in the line. At most one
  cast every 500 ticks, besides the spell's own cooldown.
- **The choice.** Of the spells that the mod says can be cast now (cooldown, cost, weapon), keep the reserve, reach the
  target in sight (the spell's own verb) and are clear, the hardest-hitting.
- **The cast.** By the mod's own job (`PawnAbility.GetJob` with the AI context, as the mod's own auto-cast for colonists
  does), so its effects, costs, cooldowns and checks are its own. The job starts at once; what they were doing is put
  back at the head of their queue when the game lets it wait, and a hunt that can't wait is queued again in its place.
  After the cast the hunt goes on: if they aren't on it again, the hunt is given again for the prey while it is still
  marked for hunting, or for its body (the game's hunt carries it home).
- **Shadow Strike.** The mod moves the caster behind the target, strikes four times and, if the target is still
  standing, drafts the colonist and sends them at it in melee; 300 ticks later it brings them back where they cast from.
  Rimfolk waits for that (the mod's projectile gone from the map), then lets go of the draft for one who wasn't drafted
  before (while they are still at the mod's orders: fighting that target or standing by), and a colonist who was
  drafted goes back to what they did (the mod's melee order on that target ended, unless they were already fighting
  hand to hand).

## The spells

Read in A RimWorld of Magic (Continued) 3.0's defs and code; every level (`_I`, `_II`, `_III`) is the same spell.
*Reach* is how far from the target, in cells, nobody it shouldn't hit may stand.

| Spell | Paid with | Reach | Notes |
|---|---|---|---|
| Headshot (`TM_Headshot`) | stamina | 1 | the sniper's shot; heaviest |
| Lightning Bolt (`TM_LightningBolt`) | mana | 3 | small sparks up to two cells and their neighbours; doesn't set fires |
| Icebolt (`TM_Icebolt`) | mana | 5 | shards fall up to three cells from where it lands, which may be a cell and a half off |
| Firebolt (`TM_Firebolt`) | mana | 2 | sets things alight: fights only |
| Frost Ray (`TM_FrostRay`) | mana | 1 | a ray at one target |
| Shadow Strike (`TM_ShadowStrike`) | stamina | 1 | the caster steps behind the target; safe prey on hunts, a lone enemy in fights |
| Spite (`TM_Spite`) | stamina | 1 | the death knight's bolt |
| Throwing Knife (`TM_ThrowingKnife`) | stamina | 1 | |
| Arcane Bolt (`TM_ArcaneBolt`) | mana | 4 | a few bolts that wander up to about two cells and burst a cell wide |
| Magic Missile (`TM_MagicMissile`) | mana | 5 | many missiles scattered up to four and a half cells |
| Poison (`TM_Poison`) | mana | 1 | over the heads; hurts as the target moves |

Left out: **Shadow Bolt** (`TM_ShadowBolt`): its orb hurts anyone not of the caster's faction along its way and then
cleaves everyone, friends included, in three cones past the target. Never cast on their own: the mod's area spells
(`MagicAutoCastMath.AreaSpells`: its own list of splash damage, the storms, blasts, rains, Fireball, Firestorm,
Fireclaw, Blizzard, Snowball, Meteor, Death Bolt, Blade Spin, Buckshot, Suppressing Fire and the like).

## What it leaves to the mod

- The mod's own auto-cast stays as it is: its setting, its thresholds (70% mana undrafted, 20% drafted with fire at
  will), and its mark on each spell (`MagicPower.autocast`, or the knowledge entry's `autocast` in the continuation)
  are never read or written. A spell with both on may be cast by either; turning the mod's mark off leaves only
  Rimfolk's rules (and its reserve).
- Whether a spell can be cast at all (`CanCastPowerCheck`: cooldown, mana, stamina, the weapon it needs), its warm-up,
  range and line of sight, what it does when it lands, and its cost.
- The mod's checks on an automatic cast (the continuation's `AbilityCastSafety`): Rimfolk only asks when the target is
  already in range and in sight, as those checks want.
- Since the review of 28 November 2026 (`docs/review-magic.md`): a spell counts as known only when the continuation's
  knowledge store doesn't mark it unlearned (RWoM Core's `PawnSpellKnowledge`, else its `AbilityKnowledgeComp`; the
  ability list when there's no entry), as the mod's own casting reads it; nobody is asked to cast while another system
  casts for them (RWoM Core's `CastOwnership.IsCasting`: a psycast's warm-up, Ability AI); and Spite for a death knight of
  the class progression (its discipline `rwom.deathknight`) waits for the 20 hate its own verb spends (without it the cast
  fizzles and the 500-tick gap is lost).
- Without the mod nothing runs: every type is found by name (`TorannMagic.CompAbilityUserMagic`, `CompAbilityUserMight`,
  `AbilityUser.PawnAbility`), the jobs by `GetNamedSilentFail`, and the button stays away.

## Not tried in the game

Written from the mod's public sources (the Workshop build may differ; everything is read softly). To check in the
game: the button on Myst; a hunt opening with Shadow Strike on a deer and the hunter carrying the body home; a hunt on
a boar or a bear opening with Arcane Bolt instead; Myst undrafted after Shadow Strike; a drafted Myst casting from where
he stands and staying there. The dev action *Magic: casting on their own, the colonists here (log)* says what each one
would cast now, or why not.
