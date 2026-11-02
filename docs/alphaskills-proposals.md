# Alpha Skills and Rimfolk: conditional passions

A list to pick from, as the rules ask: nothing here is built, except the drink before work at the top, which the
player asked for. Read from Alpha Skills' 1.6 folder (the 1.7 load uses it too): `Defs/PassionDefs`, the DLC folders
`Mods/Biotech`, `Mods/Ideology` and `Mods/Royalty`, `Patches/` and the source in `Source/AlphaSkills`. Every passion
that switches is a pair (or a set) of Vanilla Skills Expanded passion defs, and the switch writes the other def's index
into the skill's `passion` field, so the learning speed changes at once. The checks run in a hediff's `CompPostTick`
every 1000 ticks (0.4 in-game hours), 6000 ticks (2.4 hours) or 60000 ticks (a day), most only while the colonist is
on a map. Learning speeds are the defs' `learnRateFactor` (1.0 is an ordinary interest, 1.5 a burning passion).

Effort: S (a check, a hint or a small hook), M (a planning row, a worker or a new hook), L (a new system).
"Behaviour" means colonists act differently on the map; "advice" means Rimfolk only shows or suggests, and the player
does the step. Everything matches by defName or type name and does nothing without Alpha Skills.

## Built: a drink before work (drunken passion)

**Condition (code).** `Patches/AlcoholHighPatch.xml` adds `HediffCompProperties_DrunkPassions` to the game's
`AlcoholHigh` hediff. `CompPostPostAdd` turns every skill with `AS_DrunkenPassion` (1.0) into `AS_DrunkenPassion_Active`
(2.5); `CompPostPostRemoved` (and death) turns it back. So "drunk" is the AlcoholHigh hediff at any severity, from the
moment it's added until it wears off. One beer (severity 0.15, falling about 0.75 to 1 a day) keeps it awake for about
four hours. Teetotalers never get the passion (`blockingTraitsWithDegree DrugDesire -1`).

**As built** (`Integrations/DrinkBeforeWork.cs`, `DrinkBeforeWorkMath.cs`, tested in `DrinkBeforeWorkTests`; setting
`drinkBeforeWork`, on by default; pool `Rimfolk_Pool_DrinkBeforeWork`). A postfix on `JobGiver_Work.TryIssueJobPackage`,
only between jobs (no current job, no emergency, nothing queued, never a job the player ordered): when the work
trains a skill with the drunken passion asleep (a bill's `workSkill`, else the work type's skills; fetching, hauling
and refuelling don't count), once in eight hours the colonist chooses, yes 70% of the time. Yes: the game's own
`Ingest` job on the nearest drink in a stockpile, at most 25 cells away and 35 from the work, and the work job waits
first in their queue. As the cup is raised, half the time a one-sentence line. Safeguards:

- The drink: a drug whose ingesting gives AlcoholHigh for certain and nothing but that and its tolerance (beer, and
  other mods' drinks that work the same way), never one that can overdose, never one that can start an addiction on a
  first drink, nothing hard. Alpha Skills' comp must be on AlcoholHigh, or nothing is patched.
- The colonist: an adult (18 or older), free, at home, awake, not drafted, downed or in a mental state, not a
  teetotaler, not addicted to anything or just clean of a drug, not pregnant, hungover, overdosed, bleeding or needing
  rest for their health, not already drunk, no enemy within 40 cells and no high danger on the map.
- Their control: their drug policy allows that drink for fun, or a scheduled dose of it is due; their beliefs allow it
  (Rimfolk's `Beliefs`: precepts against drugs, social drugs or alcohol, unwilling by ideology, a bad memory).
- Their health: the drink leaves them in one of AlcoholHigh's first two stages (warm or tipsy, by their body size),
  and only a stage with no vomiting, mental breaks, extra fights or danger; their tolerance stays under 60% of where an
  addiction can start or the tolerance starts doing harm, and never above 0.15 (beer: addiction from 0.25, cirrhosis
  from 0.5).
- The work: making, building, mining, growing, research, art; cooking only if the stage doesn't raise food poisoning;
  social only as a talk with a prisoner. Never tending, surgery, hunting, fighting or animals.

## The other conditional passions, best first

**P1. A joint before work: stoned passion (S, behaviour).** Condition: `Patches/SmokeleafHighPatch.xml` puts
`HediffCompProperties_StonedPassions` on `SmokeleafHigh`; while that hediff is there, `AS_StonedPassion` (1.0) is
`AS_StonedPassion_Active` (1.8), exactly like the drink. Teetotalers never get it. Proposal: the drink's code with a
second row (passion, hediff, drug): a smokeleaf joint from the stores before that work, under every rule above (policy
for fun or a due dose, beliefs, adults, no addiction, tolerance well under smokeleaf's addiction point, a mild stage,
the same safe work), and its own setting. Smokeleaf slows work more than a beer, so the stage check decides; the line
would be about the smoke, not a cup.

**P2. Rainy day: that work first while it rains (S to M, behaviour within the player's priorities).** Condition:
`HediffComp_RainyDayPassion`, every 1000 ticks while on a map: the map's current weather has `rainRate > 0` (rain,
thunderstorms, foggy rain); `AS_RainyDayPassion` (1.0) becomes `_Active` (2.0). It doesn't ask whether the colonist is
outdoors, so work indoors counts as much. (The Almanac page said "outdoors"; corrected in this round.) Proposal: while
it rains, among work givers of the same priority number, that skill's come first for them (the same hook as the clean
sweep, `CleanSweepWork.WorkGivers`, so the player's priorities still decide), and Rimfolk's forecast line names them
("Rain tomorrow: a good day for Ana's crafting"). Nobody is sent out into the wet.

**P3. Night: a night shift suggested (M, advice with one click).** Condition: `HediffComp_NightPassion`, every 1000
ticks while on a map: the local hour is 20 or later, or 5 or earlier (8 in the evening until 6 in the morning);
`AS_NightPassion` (1.0) becomes `_Active` (2.5). Proposal: a planning row "Mira learns crafting 2.5 times as fast at
night: work hours 20 to 6?", one click to set her schedule (sleep moved to the day), "Not this" to keep it; with it,
a lamp for her bench when the room is dark at night. Rimfolk never changes a schedule by itself.

**P4. Moody: today's mood shown, and that work first on a good day (S to M, advice and behaviour).** Condition:
`HediffComp_MoodyPassion`, once a day while on a map: each moody skill is drawn again from five states with equal
chances: interested 1.0, apathetic 0.1, no passion 0.35, very interested 1.5, passionate 2.0. Proposal: the hover card
and the skills' passion line say today's state ("feeling it for art today"); on a passionate or very interested day,
that work comes first within the same priority (as in P2); on an apathetic day, nothing changes.

**P5. Competitive: a rival on the map, and Alpha Skills' flip (S, advice; a fix only if the player wants one).**
Condition: `HediffComp_CompetitivePassion`, every 6000 ticks while on a map: if any other free colonist spawned on the
same map has a higher level in that skill, `AS_CompetitivePassion` (0.25) becomes `_Active` (2.5); being the best
keeps it at 0.25. Working beside them doesn't matter, only living on the same map. A flaw in Alpha Skills: the next
check sends an active passion back to 0.25 even while someone is still better (the `else if` doesn't look at the
levels), so it flips every 2.4 hours and is awake about half the time. Proposal: a hint "Bob learns crafting fast while
someone better (Ana, 14) lives here; as the best he learns at a quarter"; and, if the player asks, a small prefix by
type name that keeps it awake while a better colonist is on the map (that changes Alpha Skills' own rule, so only on
request). Rimfolk's friendly contests could pair the two. Never by keeping anyone's skill down.

**P6. Vengeful: working through a loss (S to M, behaviour within priorities, and a note).** Condition: a postfix on
`PawnDiedOrDownedThoughtsUtility.TryGiveThoughts(victim, dinfo, kind)`: when the victim is of the colony's faction,
every other living colonist anywhere (maps, caravans, transporters) with the passion gets three days (180000 ticks,
saved) of `AS_VengefulPassion_Active` (3.0) instead of 1.0, the count starting again with each new loss. The kind isn't
read, so a colonist downed, banished, lost, or a colony animal dying or released to the wild wakes it as well as a
death. Proposal: for those three days that skill's work comes first within the same priority, never during the
funeral or a Rimfolk wake; the journal may say so ("Since Tomas died, Ana has been at the forge every hour she can").
Nothing about it ever brings a loss nearer.

**P7. Nomadic: the right people on the road (M, advice).** Condition: `HediffComp_NomadicPassion`, every 1000 ticks:
active (2.25 instead of 1.0) while the colonist has no map (a caravan, travelling) or is on a map that isn't a player
home (a quest site, another settlement, a temporary map). Proposal: the caravan and mission choices (Rimfolk's
missions and camp visits) list colonists with a nomadic passion in the skills the trip uses first, with the reason; on
a temporary map their work there counts. The player picks who goes.

**P8. Ideological: rituals wake it (S, advice).** Condition (Ideology): a postfix on
`RitualOutcomeEffectWorker_FromQuality.Apply` and every subclass: everyone present at a ritual with a quality outcome
gets three days (180000 ticks) at 1.75 instead of 1.0. Rimfolk's own gatherings are lords of their own, not such rituals, and don't wake
it. Proposal: a calendar note and the planning helper say who would be woken ("Ana's ideological crafting
wakes for three days after a ritual she attends"), so the player can time a big crafting order after one.

**P9. Sanguine: the hemogen target (S, advice).** Condition (Biotech, needs the Hemogenic gene): every 1000 ticks while
on a map: hemogen above 50% gives 2.0 instead of 1.0. If the gene is no longer active, the passion is set to none for
good and its hediff removed. Proposal: a hint when such a colonist's hemogen target is at or under 50% ("set it above
half and she learns twice as fast"); the game's own hemogen pack rule then does the rest. A warning before a xenogerm
change that would end the gene.

**P10. Transhumanist: prosthetics counted (S, advice).** Condition (Ideology, given with the "body modification:
approved" precept): every 6000 ticks: `ThoughtWorker_Precept_HasProsthetic_Count.ProstheticsCount` above 0 gives 1.5
instead of 0.5. Proposal: the advisor's implant suggestions rank such a colonist first, with the reason (three times
the learning with any one prosthetic). Never surgery by itself.

**P11. Psychic: sensitivity (S, advice).** Condition (Royalty): every 6000 ticks while on a map, by psychic
sensitivity: under 50% nullified (0.1, a bad passion), up to 100% minor (0.5), up to 150% psychic (1.0), up to 200%
major (1.5), above that critical (2.0). An ordinary colonist at 100% learns that skill at half speed. Proposal: a hint
naming what the colony has unlocked that raises sensitivity (eltex apparel, a psychic sensitizer), and that the
psychically dull or deaf traits lower it.

**P12. Intimate: a note only (S, advice).** Condition: a postfix on `JobDriver_Lovin.GenerateRandomMinTicksToNextLovin`
(when lovin' ends): a day (60000 ticks) at 1.75 instead of 0.75. Proposal: a line in the passion's tooltip and the
Almanac; no behaviour (Rimfolk stays out of the bedroom).

**P13. Toxic: a note only (S, advice).** Condition (Biotech, needs the pollution stimulus gene `PollutionRush`): every
1000 ticks while on a map: the cell they stand on is polluted, 2.0 instead of 1.0; without the gene the passion is set
to none for good. Proposal: a hint when such a colonist's bench stands on clean ground; Rimfolk never spreads pollution.

**P14. Nudist: a note only (S, advice).** Condition (needs the nudist trait): every 6000 ticks: nothing worn that counts
as clothing covers the torso or legs; 2.0 instead of 0.5. Proposal: a hint for warm, indoor work only; Rimfolk never
takes clothes off anyone (cold, armour and the player's outfits stay as they are).

**P15. Pain-driven and blind: notes only (S, advice).** Pain-driven (Ideology, given with the precept that idealizes pain): every
1000 ticks while on a map, total pain above 30% gives 2.0 instead of 1.0. Blind, elevated and blind, sublime
(Ideology, given with those blindness precepts): every 6000 ticks, biologically blind gives 1.5 instead of 0.5 and 2.0
instead of 1.0. Proposal: a line in the passion's tooltip that it is awake now. Never cause pain or blindness;
Rimfolk's recovery rest still comes first for someone hurt.

## Passions that react to the work itself

**P16. Mood hints and a comfort (S hints, M behaviour).** Dunce (`AS_DuncePassion`, learns nothing): +25 "doing
enjoyable work" while the current job's active skill is that skill (checked every 1000 ticks). Traumatic
(`AS_TraumaticPassion`): -6 "traumatic work" while doing that skill's job. Forbidden (`AS_ForbiddenPassion`, 1.75,
adults): -5 "forbidden passion" all the time while they have it. Proposal: mood hints for the three thoughts (XML, like
`Rimfolk_MoodHints_AlphaGenes.xml`: "lower the priority of that work" for traumatic); and, as behaviour, someone with a
dunce passion near a mental break turns to that work first within the same priority, for the +25.

**P17. How Rimfolk counts a switching passion (S, a choice).** `People.PassionLevel` judges a passion by its learning
speed now, so Alo's drunken crafting counts as an interest sober and as burning while drunk, and wishes or goals that
ask for a passion can come and go with a beer. Option: judge a switching pair by its awake form. Like-minded
(+15 opinion of anyone with any passion in that skill), youth (3.0, an interest from age 15) and the fixed ones need
nothing.
