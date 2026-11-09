# The heart, felt: strain and feelings you can see

The player (9 November 2026): "i feel like this impact of strain and all the emotions is currently neglible, or hardly
noticeable. i really like the idea of it but it's not really noticeable. can we think about this?" On the proposal
below: "sounds great!! also the good side should also be able to develop at the same time, not only when strain is at
0. perhaps it's more difficult, but pawns should still be happy about certain things even if others go wrong."

## Where things stood (the player's save, day 35, four colonists after the plague)

| Colonist | Strain | Good side |
|---|---|---|
| Ian (plague 0.91, in pain) | 20 | 0 |
| Bautista | 15 | 0 |
| Avila | 9 | 0 |
| Guido | 5 | 0 |

- Below 30 strain does nothing at all; Ian, near death, had no mood, slowdown or dream from it.
- Rest drains about 10 a day (a comfy bed 0.6 an hour asleep, quiet hours 0.15 an hour, recreation 0.5 an hour, times
  1.25 to 1.5 in a good mood: `HeartLogic`, "Relief"), and a day builds at most 25 (`HeartMath.DailyCap`): a terrible
  day is gone in two or three. No episode (85) and no release has ever happened in the save.
- The good side fills only while strain is at nothing (`GoodHeart.Fill`), and the everyday trickle keeps strain at 5 to
  20, so all four sat at 0. Neither side showed.
- The bands, when reached, are -3, -5 and -8 mood among moods of 40 either way; work -5% and -10% only from 55.
- The 26 feelings (`Defs/HeartDefs/Council_Feelings*.xml`) only change odds (wishes, warm moments, friction about a
  third), which nobody can see.
- The heart has no voice: no spoken pool for strain, the bands or the feelings, only the high spirits' kind word.

## Release 1: let it rise and stay, the two sides at once, and show it on the map

**Built** (9 November 2026, not released yet: DESIGN.md "The heart, felt" has all of it). Points 1 to 8 as below, with
these choices: the weight is split in two, what happened (a loss, a downing: lifting slowly for 3.5 days and a day more
for every 12 of weight, then 7 a day) and what lasts while it lasts (a sickness in the house, a fight going badly: lifted
to its weight each hour, lifting a day and a half after it ends); rest eases in full up to 30 and 70% from 90 (milder than
first thought: a much slower rest fed on itself in a neglected colony); good things send 35% of themselves to the good
side while there is strain, and hardship 35% of itself to the good side's cushion. Not built: eating alone and sleeping
in (point 4) and bringing a partner a meal (point 6), which the game can't show without patching where they eat and
sleep; a strained colonist sits or looks out instead, and love sits beside the partner. Measured in
`Tools/Tests/HeartFeltTests.cs`: a calm colony steady on day 6 and in good heart on day 18; a friend's death strains
those close 7.2 days, a partner 9.8, the rest not at all; a plague season like the save's leaves the whole colony worn
three days or more, nobody near breaking; real disasters bring someone near breaking every time and about one day off
each; the good side still fills 2.4 a day while strained (3.8 otherwise).

### A. Balance

1. **Heavy things leave a weight that eases over days.** Losing someone close, being downed, a sickness in the house
   (one of their own or someone close badly ill), a raid gone badly (ours downed or killed) leave a lasting weight
   under strain that eases over days, not overnight: rest can't take strain below what is left of it. A death of
   someone close keeps friends and kin strained for about a week; a plague season like the save's leaves most worn.
2. **Rest eases less under real strain.** Sleep, quiet hours and recreation ease strain at full rate at the low end
   (so 5 to 20 still settles to nothing) and less the higher it is (worn and breaking ease slowly unless something
   good happens: gatherings, good talks, small joys, a friend's kind word).
3. **The two sides at once** (the player's change). The good side grows at any strain, slower the more strain there is
   (harder, never shut): full while strain is low, then less through strained, worn and breaking. Good things ease
   strain and fill the good side together (a share each), no longer one after the other. Hardship spends part of the
   good side as a cushion and builds strain with the rest, no longer all of the good side first. Both band moods apply
   at once, and both are shown: someone can be worn down by the plague and still in good heart for their friends.
   The good side's sources stay mostly what can't be bought (friends, purpose, deeds, gatherings, talks, joys), the
   comforts of home only a little.

**Targets**, measured in a test simulation over days of play like the save's (`FairCouncilTests` is the model):

- a calm, well-run colony: most colonists steady within about a week, in good heart in two to three;
- one death of a friend or kin: those close to them strained for about a week, the rest barely touched;
- a plague season like the save's: most worn for its length;
- breaking only after a real disaster (several losses, a long siege, a plague with deaths);
- the good side still growing while strained, slower;
- the day off (85) rare but possible in a disaster, never in a calm colony.

### B. On the map

4. **Small acts by band**, each played out with a one-sentence bubble (`Speech.Say` / `Together`), in their free time
   or at a break in their work, never in danger, never drafted, at a rate that reads as life and never interrupts
   urgent work:
   - strained: sitting a while on a bench or by the fire, staring at nothing, eating alone, sleeping in;
   - worn: pacing at night, leaving a job for a few minutes to sit outside, a short word at someone;
   - in good heart: humming at work, staying at the table to talk after a meal;
   - in high spirits: singing at work, dancing at a gathering.
5. **They say how they feel**: a line when their band changes and now and then while it holds, naming its cause where
   there is one ("I keep setting a place for Ana."), in their own voice (the speech pools' rules) and their age.
6. **Strong feelings act**, so a feeling is something seen, not only odds (the odds stay as the small part): grief
   visits the grave or sits on the bed of the one lost; fear keeps them indoors after dark and near others; anger
   slams a door or says a sharp word; love brings a partner a meal or sits with them; homesickness talks of home at the
   fire; joy shares it with a friend. Each a short act with its line, at a sensible rate, never in danger.
7. **Seen at a glance**: a small icon over the head for a strong feeling (as the game shows its own), and a thin heart
   line under each portrait on the colonist bar (strain one colour, the good side another, both at once when both);
   each with its own setting.
8. **A message** when someone becomes worn or breaking, or in good heart, saying why ("Bautista is worn down: the
   plague, and losing Ana."), and when they come back from it.

## Release 2: make it count (after the player has seen release 1 in play)

The player, having played release 1 (9 November 2026): "i think you can go ahead with this, if the strain is not
noticeable it doesn't make too much sense. also include recommendations and advice on potential actions i can take to
reduce strain. for example certain decrees. or i could for example also just grant a single stained pawn a break day or
stuff like this". So points 9 to 12 as written, and 13 and 14 below.

9. **Stronger bands**: strain -4, -8 and -14 mood; the good side +3, +6 and +10 (still growing ordinary like any good
   mood). These go through the game's mood, so worn colonists break more often, as they should.
10. **Mild effects beyond mood**: worn learn slower, sleep longer and recover a little slower; in good heart a little
    faster learning and recovery; work from the first band. Healing and immunity within 5 to 10% at most.
11. **A bad day from worn** (not only at 85): the afternoon off, a walk, a cry in their room, a drink if they drink, a
    visit to a grave, under the day off's safety rules, easing a good share.
12. **Hearts spread a little**: someone worn wears on those close to them, someone in high spirits lifts them.
13. **What would help, as advice you can act on**: for each strained colonist, the few things that would ease them most,
    from what is wearing on them now and what the colony has: a proper meal or a better bed, recreation they like, a
    gathering or party, time with a friend, keeping them out of the next fight, treating their pain, a decree that eases
    strain (the colony's own rest or leisure decrees, named with what each costs), a day off (14). Shown on the heart's
    page, the colonist's card, the message when someone becomes worn, and the council's Moods page for the whole colony,
    each with a button where Rimfolk can do it (propose the decree, give the day off, call the gathering).
14. **A day off for one colonist**: the player grants a strained colonist a day of rest from their card, the heart's page
    or a gizmo; they leave work for the day and rest, sleep in, take their recreation, walk and see friends, played out on
    the map, and it eases strain well beyond an ordinary day; the colony feels the work missed; at most once in a while
    for each colonist so it stays a choice.

## Rules that hold

- The safety rule of the heart (DESIGN.md "Layer 7"): the game's mood need, its mental breaker and its break picker
  stay untouched; the heart reaches the game through its situational thoughts, stat parts and acts of its own.
- Harm at the game's rate and shown; words match what is seen; one-sentence bubbles; every age (the acts and lines take
  the form of the colony's age and the speaker's ways).
- The colony is the family: closeness is friends, partners, kin and those they work and live beside, never a house.

## Order of building

Release 1 (A and B together, the two sides at once with them), then release 2 once the player has played it (approved
9 November 2026, with points 13 and 14).
