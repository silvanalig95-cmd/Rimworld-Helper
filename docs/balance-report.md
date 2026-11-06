# Rimfolk balance report

Written by the colony-year simulator in `Tools/Balance` (run it from the repository root with
`dotnet run -c Release --project Tools/Balance -- . --before Tools/Balance/before.json`). It plays a settled colony through one
60-day year (30 days of warm-up first, so standards and trust have settled) 120 times for each case, without the game:
the moods, wishes, dilemmas, events and ailments come from the def XML, the rates from the code (read from the source
where it can, see the last section), and the mood rules from the mod's own `GoodLifeMath`, `SmallMomentMath`, `HeartMath`,
`PressureMath`, `SetbackMath`, `AilmentMath` and `ArcMath`, compiled in as they are. Each colonist's heart is played hour by
hour (`HeartSim.cs`). What it can't know (how often the storyteller
sends visitors, how a player plays) is in `Tools/Balance/Rates.cs`, one documented table. Treat the numbers as
a model's: good for comparing settings and changes, not a promise about one game.

**Rimfolk's mood** below is everything Rimfolk adds to a colonist's mood, per colonist, averaged over the year:
its good memories (small joys as their short moments), its bad memories (small letdowns as their short moments: setbacks,
frictions, nuisances, quiet troubles, the pressures' and lasting setbacks' moments; a lapsed wish, a nightmare), and its
steady moods (trust, the colony's ways, ailments, strain, and what good things growing ordinary takes off Rimfolk's own
good moods). The game's own moods aren't in it; what growing ordinary takes off them is in "The colony's mood" below.

## Against the targets

The targets are in DESIGN.md ("Balance targets").

| | Target | 6 colonists | 12 colonists | 6 colonists before |
|---|---|---|---|---|
| Rimfolk's mood, well run | +3 to +6 | +5.9 | +5.0 | +5.6 |
| Rimfolk's mood, average | 0 to +3 | +2.5 | +1.9 | +2.5 |
| Rimfolk's mood, neglectful | -4 to -10 | -7.2 | -6.3 | -7.0 |
| Rimfolk's good moods at a high point (P99), well run | well short of the top | +17.5 | +16.2 | +17.5 |
| Time with +12 or more from Rimfolk's good moods, well run | rare | 18% | 10% | 16% |
| Questions (decision letters) per day, average | 1 every 2-3 days (0.33-0.5) | 0.50 | 0.52 | 0.49 |
| Questions and wishes voiced per day, average | (for reference) | 0.97 | 1.04 | 0.97 |
| Questions within 12 hours of another, average | few | 8% | 10% | 8% |
| Time with an everyday ailment, average | 10-15% | 14% | 14% | 14% |
| Time with an everyday ailment, well run | under 10-15% | 10% | 9% | 9% |
| The colony's whole mood, well run (for reference) | as before | 69% | 69% | 69% |
| The colony's whole mood, average (for reference) | as before | 64% | 64% | 64% |
| The colony's whole mood, neglectful (for reference) | as before | 39% | 40% | 39% |
| Time under the minor-break line, neglectful (letdowns as moods: 43%) | no more than before | 34% | 29% | 34% |
| The heart's day off a colonist-year, neglectful (letdowns as moods: 0.46) | under one | 0.75 | 0.74 | 0.73 |
| In good heart / in high spirits, well run (for reference) | a well-run colony's reward | 34% / 6% | 35% / 7% | - |
| In good heart / in high spirits, average (for reference) | a well-run colony's reward | 0% / 0% | 0% / 0% | - |
| In good heart / in high spirits, neglectful (for reference) | a well-run colony's reward | 0% / 0% | 0% / 0% | - |

## Before and after the heart's good side

The simulator on the code and defs as they were before the heart's good side (6 November 2026: strain alone, nothing
on the other side; `Tools/Balance/before.json`), and as they are now (default settings).

| | 6 before | 6 after | 12 before | 12 after |
|---|---|---|---|---|
| Rimfolk's mood, well run | +5.6 | +5.9 | +4.6 | +5.0 |
|   good memories (raw), well run | +8.7 (+8.7) | +8.6 (+8.6) | +7.1 (+7.1) | +7.0 (+7.0) |
|   bad memories and steady bad, well run | -7.4 | -7.9 | -5.7 | -6.1 |
| Rimfolk's mood, average | +2.5 | +2.5 | +1.9 | +1.9 |
|   good memories (raw), average | +6.1 (+6.1) | +6.0 (+6.0) | +5.0 (+5.0) | +5.0 (+5.0) |
|   bad memories and steady bad, average | -5.1 | -5.0 | -4.5 | -4.4 |
| Rimfolk's mood, neglectful | -7.0 | -7.2 | -6.5 | -6.3 |
|   good memories (raw), neglectful | +3.5 (+3.5) | +3.5 (+3.5) | +3.1 (+3.1) | +3.2 (+3.2) |
|   bad memories and steady bad, neglectful | -11.2 | -11.3 | -10.1 | -10.0 |
| Rimfolk's good moods at a high point (P99), well run | +17.5 | +17.5 | +15.8 | +16.2 |
| Time with +12 or more from Rimfolk's good moods, well run | 16% | 18% | 8% | 10% |
| Trust, year's average, average play | +13 | +12 | +9 | +9 |
| Questions per day, average play | 0.49 | 0.50 | 0.52 | 0.52 |
| Questions within 12 hours of another, average play | 8% | 8% | 10% | 10% |
| Days with two or more questions, average play | 6% | 6% | 7% | 7% |
| Wishes voiced per day, average play | 0.48 | 0.47 | 0.52 | 0.52 |
| Letters that only tell, per day, average play | 0.39 | 0.39 | 0.47 | 0.48 |
| News lines per day (one evening letter), average play | 1.71 | 1.71 | 2.34 | 2.31 |
| Time with an everyday ailment, well run | 9% | 10% | 9% | 9% |
| Time with an everyday ailment, average | 14% | 14% | 14% | 14% |
| Time with an everyday ailment, neglectful | 31% | 31% | 31% | 31% |
| Ailment days per colonist per year, average play | 8.7 | 8.6 | 8.5 | 8.7 |

## Rimfolk's mood per colonist

Good memories are as given (small joys as their moments); "raw" is the same. Steady moods are trust, the colony's
ways, ailments and strain, and what good things growing ordinary takes off Rimfolk's good moods. P10 and P90: a hard
and a good moment for one colonist.

| Case | Good memories (raw) | Bad memories | Steady good | Steady bad | **Net** | P10 / P90 | Good moods on the mind | Mood | Standards |
|---|---|---|---|---|---|---|---|---|---|
| 6 colonists, well run | +8.6 (+8.6) | -2.2 | +5.1 | -5.6 | **+5.9** | -0.0 / +11.7 | 2.3 | 69% | 0.87 |
| 6 colonists, average | +6.0 (+6.0) | -3.4 | +1.5 | -1.7 | **+2.5** | -5.0 / +10.2 | 1.6 | 64% | 0.30 |
| 6 colonists, neglectful | +3.5 (+3.5) | -5.0 | +0.6 | -6.2 | **-7.2** | -15.8 / +1.2 | 1.0 | 39% | 0.00 |
| 12 colonists, well run | +7.0 (+7.0) | -1.9 | +4.1 | -4.3 | **+5.0** | -0.4 / +10.4 | 1.9 | 69% | 0.85 |
| 12 colonists, average | +5.0 (+5.0) | -2.9 | +1.3 | -1.5 | **+1.9** | -4.9 / +9.1 | 1.4 | 64% | 0.25 |
| 12 colonists, neglectful | +3.2 (+3.2) | -4.6 | +0.5 | -5.5 | **-6.3** | -14.4 / +1.8 | 0.9 | 40% | 0.00 |

Over the year (net, in five 10-day stretches plus one; good and bad in brackets), 6 colonists:

| Play | days 0-9 | days 10-19 | days 20-29 | days 30-39 | days 40-49 | days 50-59 |
|---|---|---|---|---|---|---|
| well run | +5.6 (+12.7 / -7.1) | +5.7 (+13.4 / -7.6) | +5.9 (+13.9 / -8.0) | +5.9 (+13.9 / -8.0) | +6.1 (+14.1 / -8.0) | +6.2 (+14.5 / -8.3) |
| average | +2.9 (+7.7 / -4.8) | +2.6 (+7.6 / -5.0) | +2.4 (+7.5 / -5.1) | +2.3 (+7.4 / -5.1) | +2.3 (+7.4 / -5.1) | +2.5 (+7.6 / -5.1) |
| neglectful | -6.0 (+4.3 / -10.2) | -6.5 (+3.9 / -10.4) | -7.0 (+4.2 / -11.2) | -7.6 (+3.9 / -11.5) | -7.9 (+4.0 / -11.9) | -8.3 (+4.1 / -12.4) |

Where it comes from (6 colonists): what each source adds to a colonist's mood on average.

| Source | well run | average | neglectful |
|---|---|---|---|
| good things grown ordinary | -5.17 | -0.52 | -0.00 |
| trust | +2.66 | +0.39 | -2.19 |
| strain | -0.00 | -0.28 | -2.34 |
| wishes | +2.18 | +0.98 | +0.24 |
| meetings | +1.80 | +1.24 | +0.41 |
| life and honours | +1.30 | +1.11 | +0.80 |
| pressures (bad) | -0.20 | -0.69 | -1.24 |
| ailments | -0.23 | -0.34 | -0.97 |
| the colony's ways | +0.91 | +0.23 | -0.38 |
| wishes (bad) | -0.30 | -0.56 | -0.86 |
| the good side | +0.85 | +0.00 | +0.00 |
| moments | +0.71 | +0.65 | +0.59 |
| setbacks (lasting) (bad) | -0.28 | -0.38 | -0.70 |
| life goals | +0.65 | +0.45 | +0.27 |
| pressures | +0.55 | +0.20 | +0.02 |
| setbacks (bad) | -0.23 | -0.32 | -0.54 |
| dilemmas | +0.53 | +0.39 | +0.19 |
| social events | +0.49 | +0.51 | +0.49 |
| frictions (bad) | -0.44 | -0.48 | -0.41 |
| dilemmas (bad) | -0.18 | -0.24 | -0.34 |
| stories | +0.32 | +0.32 | +0.31 |
| meetings (bad) | -0.15 | -0.21 | -0.32 |
| calendar | +0.25 | +0.23 | +0.24 |
| dreams (bad) | -0.13 | -0.13 | -0.24 |
| events | +0.23 | +0.21 | +0.15 |
| nuisances (bad) | -0.04 | -0.11 | -0.20 |
| social events (bad) | -0.17 | -0.14 | -0.12 |
| health | -0.05 | -0.08 | -0.11 |
| encounters | +0.10 | +0.09 | +0.07 |
| encounters (bad) | -0.06 | -0.05 | -0.04 |

What good things growing ordinary takes off (6 colonists):

| Play | Net with the rule | Net without it | Good memories with | without |
|---|---|---|---|---|
| well run | +5.9 | +11.2 | +8.6 | +8.9 |
| average | +2.5 | +2.9 | +6.0 | +6.1 |
| neglectful | -7.2 | -7.1 | +3.5 | +3.6 |

## The heart

Each colonist's strain, played hour by hour (`HeartSim.cs`): the life of each play style (fights, losses, bad nights,
hunger, low spirits, decrees; sleep, quiet hours, recreation, gatherings and talks), set so that it alone gives the
shares the report always assumed, and on top what small letdowns and joys bring (`SmallMomentMath`): what Rimfolk's
own weigh beyond their moment, and the pile of every small mood on a colonist's mind, the game's (a share of its moods,
`Rates.GameSmallBad`, `GameSmallGood`) and Rimfolk's. "As moods" is the same with letdowns and joys as moods of their
own and nothing to the heart (as before phase 3). Strain built and eased is per colonist over the 60-day year.

| Case | Strained / worn / near breaking | Day off a year | Built | of it: life / Rimfolk's letdowns / small moods (the game's) / (Rimfolk's) | The game's small moods' share | Eased by small joys (the game's / Rimfolk's) |
|---|---|---|---|---|---|---|
| 6 colonists, well run | 0% / 0% / 0% | 0.00 | 21 | 19 / 1 / 0 / 0 | 0% | 2 / 2 |
| 6 colonists, average | 6% / 1% / 0% | 0.04 | 209 | 178 / 19 / 8 / 4 | 4% | 15 / 16 |
| 6 colonists, neglectful | 28% / 17% / 8% | 0.75 | 450 | 365 / 40 / 27 / 18 | 6% | 7 / 20 |
| 12 colonists, well run | 0% / 0% / 0% | 0.00 | 18 | 16 / 1 / 0 / 0 | 0% | 2 / 2 |
| 12 colonists, average | 6% / 1% / 0% | 0.03 | 202 | 172 / 18 / 7 / 4 | 4% | 14 / 15 |
| 12 colonists, neglectful | 29% / 17% / 9% | 0.74 | 450 | 365 / 40 / 28 / 17 | 6% | 8 / 20 |
| 6 colonists, well run, as moods | 0% / 0% / 0% | 0.00 | 21 | 21 / 0 / 0 / 0 | 0% | 0 / 0 |
| 6 colonists, average, as moods | 5% / 1% / 0% | 0.04 | 155 | 155 / 0 / 0 / 0 | 0% | 0 / 0 |
| 6 colonists, neglectful, as moods | 23% / 11% / 5% | 0.46 | 370 | 370 / 0 / 0 / 0 | 0% | 0 / 0 |

The colony's mood: the whole of it, the game's moods included, and what good things growing ordinary takes off the
game's own good moods (it comes off every good mood by how ordinary each is: the game's comforts mostly).

| Case | Whole mood (P10 / P90) | Under the minor-break line | Taken off the game's good moods | Taken off Rimfolk's |
|---|---|---|---|---|
| 6 colonists, well run | 69% (64% / 74%) | 0% | -13.3 | -5.2 |
| 6 colonists, average | 64% (56% / 71%) | 0% | -1.6 | -0.5 |
| 6 colonists, neglectful | 39% (27% / 50%) | 34% | -0.0 | -0.0 |
| 12 colonists, well run | 69% (64% / 73%) | 0% | -12.3 | -3.9 |
| 12 colonists, average | 64% (56% / 71%) | 0% | -1.3 | -0.4 |
| 12 colonists, neglectful | 40% (28% / 51%) | 29% | -0.0 | -0.0 |
| 6 colonists, well run, without growing ordinary | 87% (77% / 98%) | 0% | +-0.0 | +0.0 |
| 6 colonists, average, without growing ordinary | 66% (56% / 77%) | 0% | +-0.0 | +0.0 |
| 6 colonists, neglectful, without growing ordinary | 39% (27% / 50%) | 34% | +-0.0 | +0.0 |
| 6 colonists, well run, letdowns and joys as moods | 68% (63% / 73%) | 0% | -13.3 | -5.9 |
| 6 colonists, average, letdowns and joys as moods | 63% (54% / 71%) | 0% | -1.1 | -0.4 |
| 6 colonists, neglectful, letdowns and joys as moods | 36% (25% / 47%) | 43% | -0.0 | -0.0 |

## The heart's good side

The other side of strain (`HeartMath`, "The good side"; `HeartSim.cs`): hardship spends it before strain builds; what a
gathering, a talk or a small joy eases beyond the strain there is fills it at its share, with friends close by and a
purpose (the heart's needs well met, drawn a day at a time per play style), deeds the colony speaks well of and a life
goal moving; it fades slowly. In good heart from 40, in high spirits from 60; in high spirits, now and then, an inspiration or a kind word for someone close.
Filled and spent are per colonist over the 60-day year; "without" is the same seeds with its setting off.

| Case | In good heart | In high spirits | Strain at nothing (without) | Inspirations / kind words a colonist-year | Filled by | Spent by |
|---|---|---|---|---|---|---|
| 6 colonists, well run | 34% | 6% | 98% (70%) | 0.14 / 0.12 | purpose 56, belonging 44, good talks 42, small joys 24 | life 85, Rimfolk's letdowns 21, small moods 3 |
| 6 colonists, average | 0% | 0% | 65% (46%) | 0.00 / 0.00 | purpose 23, good talks 22, belonging 15, small joys 11 | life 50, Rimfolk's letdowns 16, small moods 10 |
| 6 colonists, neglectful | 0% | 0% | 9% (7%) | 0.00 / 0.00 | good talks 3, small joys 2, purpose 1, gatherings 1 | life 4, Rimfolk's letdowns 1, small moods 1 |
| 12 colonists, well run | 35% | 7% | 98% | 0.15 / 0.14 | purpose 57, belonging 45, good talks 42, small joys 25 | life 86, Rimfolk's letdowns 20, small moods 2 |
| 12 colonists, average | 0% | 0% | 66% | 0.00 / 0.00 | purpose 23, good talks 22, belonging 16, small joys 11 | life 51, Rimfolk's letdowns 16, small moods 9 |
| 12 colonists, neglectful | 0% | 0% | 8% | 0.00 / 0.00 | good talks 3, small joys 2, purpose 1, belonging 1 | life 4, Rimfolk's letdowns 1, small moods 1 |
| 6 colonists, well run, lightTouch | 40% | 13% | 99% | 0.31 / 0.25 | purpose 57, belonging 44, good talks 42, small joys 26 | life 89, Rimfolk's letdowns 8, small moods 0 |
| 6 colonists, average, lightTouch | 0% | 0% | 70% | 0.00 / 0.00 | purpose 25, good talks 24, belonging 17, small joys 13 | life 61, Rimfolk's letdowns 12, small moods 5 |
| 6 colonists, neglectful, lightTouch | 0% | 0% | 9% | 0.00 / 0.00 | good talks 3, small joys 2, purpose 1, gatherings 1 | life 5, small moods 2, Rimfolk's letdowns 1 |
| 6 colonists, well run, storyRich | 34% | 6% | 98% | 0.15 / 0.14 | purpose 57, belonging 44, good talks 42, small joys 24 | life 84, Rimfolk's letdowns 22, small moods 4 |
| 6 colonists, average, storyRich | 0% | 0% | 65% | 0.00 / 0.00 | purpose 23, good talks 22, belonging 15, small joys 11 | life 50, Rimfolk's letdowns 16, small moods 10 |
| 6 colonists, neglectful, storyRich | 0% | 0% | 9% | 0.00 / 0.00 | good talks 3, small joys 2, purpose 1, gatherings 1 | life 5, Rimfolk's letdowns 1, small moods 1 |
| 6 colonists, well run, demanding | 29% | 5% | 97% | 0.10 / 0.11 | purpose 57, belonging 44, good talks 41, small joys 21 | life 82, Rimfolk's letdowns 25, small moods 4 |
| 6 colonists, average, demanding | 0% | 0% | 63% | 0.00 / 0.00 | purpose 22, good talks 21, belonging 15, small joys 9 | life 47, Rimfolk's letdowns 16, small moods 9 |
| 6 colonists, neglectful, demanding | 0% | 0% | 7% | 0.00 / 0.00 | good talks 3, small joys 1, purpose 1, gatherings 1 | life 4, Rimfolk's letdowns 1, small moods 1 |

What it changes (6 colonists, the same seeds with and without it):

| Play | Rimfolk's mood | Its own mood | Good moods at a high point (P99) | +12 or more | Strained / worn / near breaking | Under the minor-break line | Day off a year | Whole mood |
|---|---|---|---|---|---|---|---|---|
| well run | +5.6 to +5.9 | +0.9 | +17.5 to +17.5 | 16% to 18% | 1%/0%/0% to 0%/0%/0% | 0% to 0% | 0.00 to 0.00 | 69% to 69% |
| average | +2.5 to +2.5 | +0.0 | +20.0 to +20.0 | 17% to 16% | 8%/2%/0% to 6%/1%/0% | 0% to 0% | 0.05 to 0.04 | 64% to 64% |
| neglectful | -7.0 to -7.2 | +0.0 | +16.0 to +16.0 | 5% to 5% | 30%/17%/8% to 28%/17%/8% | 34% to 34% | 0.73 to 0.75 | 39% to 39% |

## Trust

Average trust in the council (-100 to 100; trusting from 25, devoted from 60, doubtful from -15, bitter from -45).

| Case | Start of year | End of year | Year's average | Trusting or devoted | Doubtful or bitter | By stretch |
|---|---|---|---|---|---|---|
| 6 colonists, well run | +36 | +70 | +56 | 89% | 0% | +40, +48, +55, +60, +65, +68 |
| 6 colonists, average | +8 | +15 | +12 | 26% | 8% | +9, +10, +12, +13, +14, +15 |
| 6 colonists, neglectful | -16 | -42 | -30 | 0% | 81% | -18, -23, -28, -32, -37, -41 |
| 12 colonists, well run | +21 | +45 | +35 | 66% | 0% | +24, +29, +34, +37, +40, +43 |
| 12 colonists, average | +6 | +12 | +9 | 16% | 5% | +7, +8, +9, +10, +11, +11 |
| 12 colonists, neglectful | -10 | -26 | -19 | 0% | 58% | -12, -15, -18, -21, -23, -25 |

## Letters and messages per day

Questions are choice letters (a dilemma, an event with answers, a calendar day, a meeting's vote, a mission
offered, a world event's question). Wishes are letters that ask without buttons. Letters only tell. Messages show
on screen as they happen; the evening's colony news gathers the rest into one letter a day. Flavour news (the
smaller encounters, moments and dreams) isn't shown at the default message level and isn't counted.

**6 colonists, average play**: 0.50 questions, 0.47 wishes voiced, 0.39 letters, 0.63 messages and 1.71 news lines a day.

| System | Questions | Wishes | Letters | Messages | News lines |
|---|---|---|---|---|---|
| wishes | 0.00 | 0.37 | 0.04 | 0.00 | 0.43 |
| event wishes | 0.00 | 0.10 | 0.00 | 0.00 | 0.00 |
| dilemmas | 0.10 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (after a raid) | 0.04 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (strangers) | 0.07 | 0.00 | 0.00 | 0.00 | 0.00 |
| events | 0.07 | 0.00 | 0.03 | 0.00 | 0.00 |
| encounters | 0.00 | 0.00 | 0.00 | 0.00 | 0.28 |
| social events | 0.00 | 0.00 | 0.00 | 0.00 | 0.31 |
| ailments | 0.00 | 0.00 | 0.02 | 0.00 | 0.15 |
| health | 0.00 | 0.00 | 0.01 | 0.00 | 0.12 |
| calendar | 0.10 | 0.00 | 0.03 | 0.00 | 0.00 |
| stories | 0.00 | 0.00 | 0.04 | 0.00 | 0.13 |
| camps | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| world events | 0.05 | 0.00 | 0.02 | 0.00 | 0.00 |
| missions | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| play-outs | 0.00 | 0.00 | 0.00 | 0.33 | 0.00 |
| meetings | 0.06 | 0.00 | 0.03 | 0.00 | 0.03 |
| decrees | 0.00 | 0.00 | 0.00 | 0.13 | 0.00 |
| dreams | 0.00 | 0.00 | 0.00 | 0.00 | 0.01 |
| people | 0.00 | 0.00 | 0.11 | 0.00 | 0.25 |
| colony | 0.00 | 0.00 | 0.04 | 0.17 | 0.00 |
| world | 0.00 | 0.00 | 0.01 | 0.00 | 0.00 |

**12 colonists, average play**: 0.52 questions, 0.52 wishes voiced, 0.48 letters, 0.65 messages and 2.31 news lines a day.

| System | Questions | Wishes | Letters | Messages | News lines |
|---|---|---|---|---|---|
| wishes | 0.00 | 0.33 | 0.04 | 0.00 | 0.48 |
| event wishes | 0.00 | 0.19 | 0.00 | 0.00 | 0.00 |
| dilemmas | 0.10 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (after a raid) | 0.04 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (strangers) | 0.08 | 0.00 | 0.00 | 0.00 | 0.00 |
| events | 0.06 | 0.00 | 0.04 | 0.00 | 0.00 |
| encounters | 0.00 | 0.00 | 0.00 | 0.00 | 0.27 |
| social events | 0.00 | 0.00 | 0.00 | 0.00 | 0.50 |
| ailments | 0.00 | 0.00 | 0.02 | 0.00 | 0.28 |
| health | 0.00 | 0.00 | 0.02 | 0.00 | 0.18 |
| calendar | 0.13 | 0.00 | 0.05 | 0.00 | 0.00 |
| stories | 0.00 | 0.00 | 0.06 | 0.00 | 0.19 |
| camps | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| world events | 0.04 | 0.00 | 0.02 | 0.00 | 0.00 |
| missions | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| play-outs | 0.00 | 0.00 | 0.00 | 0.35 | 0.00 |
| meetings | 0.06 | 0.00 | 0.03 | 0.00 | 0.03 |
| decrees | 0.00 | 0.00 | 0.00 | 0.13 | 0.00 |
| dreams | 0.00 | 0.00 | 0.00 | 0.00 | 0.02 |
| people | 0.00 | 0.00 | 0.14 | 0.00 | 0.35 |
| colony | 0.00 | 0.00 | 0.04 | 0.17 | 0.00 |
| world | 0.00 | 0.00 | 0.01 | 0.00 | 0.00 |

By play style (6 colonists):

| Play | Questions | Wishes | Letters | Messages | News lines | Questions within 12 h of another | Days with two or more questions |
|---|---|---|---|---|---|---|---|
| well run | 0.50 | 0.64 | 0.47 | 0.66 | 1.72 | 8% | 6% |
| average | 0.50 | 0.47 | 0.39 | 0.63 | 1.71 | 8% | 6% |
| neglectful | 0.48 | 0.53 | 0.39 | 0.57 | 1.96 | 8% | 6% |

## Ailments

Everyday ailments and what they leave behind (a lingering cough), per colonist over the year. "Not quiet" leaves
out the smallest (a stiff neck, hiccups, a smoke cough), which only a player who wants every message hears about.

| Case | Ailment days per colonist | Time with one | Time with one, not quiet | Caught, per colony | Circumstance, per colony | Serious, per colony |
|---|---|---|---|---|---|---|
| 6 colonists, well run | 5.9 | 10% | 8% | 1.2 | 31.2 | 0.2 |
| 6 colonists, average | 8.6 | 14% | 12% | 1.1 | 28.3 | 0.5 |
| 6 colonists, neglectful | 18.7 | 31% | 27% | 1.3 | 40.3 | 1.9 |
| 12 colonists, well run | 5.8 | 9% | 8% | 1.3 | 61.1 | 0.2 |
| 12 colonists, average | 8.7 | 14% | 12% | 1.4 | 55.3 | 0.9 |
| 12 colonists, neglectful | 18.8 | 31% | 28% | 1.1 | 80.8 | 3.5 |

What fills the time (days per colonist per year, 6 colonists):

| Ailment | well run | average | neglectful |
|---|---|---|---|
| Convalescent | 0.4 | 0.7 | 3.2 |
| Lice | 0.0 | 0.3 | 3.5 |
| RimrotCough | 0.0 | 0.3 | 1.9 |
| ChestCold | 0.5 | 0.6 | 0.8 |
| Insomnia | 0.1 | 0.4 | 1.4 |
| HeadCold | 0.4 | 0.6 | 0.6 |
| SmokeCough | 0.0 | 0.2 | 0.9 |
| ColdSore | 0.1 | 0.2 | 0.8 |
| Worms | 0.0 | 0.1 | 0.9 |
| StrainedBack | 0.3 | 0.4 | 0.3 |
| Toothache | 0.2 | 0.4 | 0.4 |
| ChappedHands | 0.2 | 0.4 | 0.3 |
| BadNights | 0.2 | 0.2 | 0.3 |
| Chilblains | 0.2 | 0.3 | 0.3 |
| SoreHands | 0.2 | 0.2 | 0.3 |
| Sunburn | 0.2 | 0.2 | 0.2 |

## The presets

The style presets of the settings window, applied as the window applies them (6 colonists).

| Preset | Play | Net | Good memories | Bad + steady bad | Questions per day | Wishes per day | Time ill |
|---|---|---|---|---|---|---|---|
| default | well run | +5.9 | +8.6 | -7.9 | 0.50 | 0.64 | 10% |
| default | average | +2.5 | +6.0 | -5.0 | 0.50 | 0.47 | 14% |
| default | neglectful | -7.2 | +3.5 | -11.3 | 0.48 | 0.53 | 31% |
| lightTouch | well run | +6.8 | +6.9 | -4.6 | 0.45 | 0.34 | 5% |
| lightTouch | average | +4.6 | +4.9 | -1.8 | 0.45 | 0.27 | 7% |
| lightTouch | neglectful | -1.4 | +3.1 | -5.1 | 0.44 | 0.36 | 16% |
| storyRich | well run | +6.4 | +9.9 | -8.6 | 0.59 | 0.64 | 9% |
| storyRich | average | +2.9 | +6.8 | -5.5 | 0.56 | 0.47 | 14% |
| storyRich | neglectful | -6.7 | +4.2 | -11.5 | 0.53 | 0.53 | 31% |
| demanding | well run | +2.0 | +6.4 | -8.2 | 0.50 | 0.68 | 9% |
| demanding | average | -2.8 | +4.4 | -8.3 | 0.49 | 0.52 | 14% |
| demanding | neglectful | -14.8 | +2.5 | -17.8 | 0.47 | 0.64 | 32% |

## How often things happen (per colony per year)

| What | 6, well run | 6, average | 6, neglectful | 12, well run | 12, average | 12, neglectful |
|---|---|---|---|---|---|---|
| after-effects | 0.6 | 0.8 | 2.2 | 0.8 | 1.7 | 3.9 |
| ailments | 31.2 | 28.3 | 40.3 | 61.1 | 55.3 | 80.8 |
| ambitions reached | 2.6 | 1.8 | 0.6 | 2.7 | 1.8 | 0.6 |
| calendar days | 8.0 | 8.0 | 8.0 | 11.0 | 11.0 | 11.0 |
| camps | 0.1 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 |
| catching ailments | 1.2 | 1.1 | 1.3 | 1.3 | 1.4 | 1.1 |
| convalescence | 2.8 | 3.9 | 5.7 | 5.3 | 7.4 | 11.8 |
| days of one faith for the colony | 2.2 | 2.5 | 2.4 | 2.2 | 3.1 | 2.4 |
| decree requests | 7.8 | 7.9 | 8.0 | 8.0 | 7.8 | 7.7 |
| dilemmas | 13.3 | 13.2 | 12.0 | 13.7 | 13.3 | 12.0 |
| dreams | 108.8 | 107.7 | 107.0 | 214.6 | 213.6 | 217.7 |
| encounters | 55.3 | 52.9 | 48.2 | 53.3 | 52.8 | 48.4 |
| evenings of faith | 1.4 | 1.5 | 1.6 | 1.5 | 2.0 | 1.5 |
| events | 6.4 | 6.0 | 6.1 | 5.6 | 5.9 | 6.0 |
| friction moments | 202.4 | 267.6 | 359.7 | 407.2 | 535.5 | 718.6 |
| meetings | 3.7 | 3.8 | 3.8 | 3.7 | 3.8 | 3.8 |
| missions | 0.3 | 0.2 | 0.2 | 0.2 | 0.2 | 0.2 |
| moments | 713.0 | 661.2 | 591.5 | 1418.6 | 1323.1 | 1185.6 |
| nuisances | 26.8 | 81.5 | 140.1 | 53.0 | 165.0 | 280.9 |
| personal touches | 9.9 | 10.1 | 10.1 | 10.1 | 10.1 | 10.0 |
| play-outs | 5.4 | 4.7 | 2.6 | 4.9 | 4.1 | 2.4 |
| quiet troubles | 3.4 | 0.8 | 0.0 | 5.7 | 1.2 | 0.0 |
| serious illnesses | 0.2 | 0.5 | 1.9 | 0.2 | 0.9 | 3.5 |
| small joys | 257.7 | 127.4 | 56.7 | 516.1 | 254.3 | 112.8 |
| small letdowns | 642.9 | 1242.8 | 2189.1 | 1212.2 | 2432.9 | 4354.8 |
| social events | 71.3 | 71.2 | 71.3 | 117.8 | 117.6 | 117.8 |
| stories | 3.2 | 3.3 | 3.2 | 5.1 | 5.2 | 5.2 |
| wishes granted | 31.5 | 14.7 | 3.7 | 33.0 | 16.3 | 5.1 |
| wishes lapsed | 6.4 | 13.4 | 27.7 | 6.9 | 14.8 | 34.7 |
| wishes made | 38.2 | 28.3 | 31.5 | 40.0 | 31.2 | 39.9 |
| world events | 1.1 | 1.1 | 1.1 | 1.0 | 1.0 | 1.1 |

## What the model reads and assumes

From the defs: 1897 moods, 216 dilemmas, 232 events and missions, 163 encounters,
97 social events, 131 moments, 257 dreams, 24 nuisances, 111 wishes,
69 ailments and illnesses, 13 catching ones, 64 world events (542 defs for other mods left out).

From the code, read from the source each run:

| Constant | File | Value | Found |
|---|---|---|---|
| wishes wait for a new colony (days) | `CouncilGameComponent.cs` | 3 | yes |
| trust for a wish granted | `CouncilDynamics.cs` | 12 | yes |
| trust for a promised wish granted | `CouncilDynamics.cs` | 18 | yes |
| trust lost for a wish let lapse | `CouncilDynamics.cs` | 9 | yes |
| trust lost for a broken promise | `CouncilDynamics.cs` | 18 | yes |
| trust for a promise made | `CouncilDynamics.cs` | 4 | yes |
| trust kept each day (drift back to neutral) | `Standing.cs` | 0.985 | yes |
| trust: devoted from | `Standing.cs` | 60 | yes |
| trust: trusting from | `Standing.cs` | 25 | yes |
| trust: doubtful at or below | `Standing.cs` | 15 | yes |
| trust: bitter at or below | `Standing.cs` | 45 | yes |
| standards rise per day from 85% mood | `Standards.cs` | 0.06 | yes |
| standards rise per day from 70% mood | `Standards.cs` | 0.04 | yes |
| standards fall per day under 40% mood | `Standards.cs` | 0.06 | yes |
| standards fall per day otherwise | `Standards.cs` | 0.01 | yes |
| a discomfort becomes a wish, per hour | `CouncilDynamics.cs` | 0.35 | yes |
| dilemmas wait for a new colony (days) | `Dilemmas/Dilemmas.cs` | 5 | yes |
| a dilemma after a raid, per hour | `Dilemmas/Dilemmas.cs` | 0.08 | yes |
| Rimfolk's most of the storyteller's Misc pool | `Happenings/Happenings.cs` | 0.5 | yes |
| the same kind of event twice running | `Happenings/Happenings.cs` | 0.5 | yes |
| encounter chance per hour: Visitor | `Strangers/Encounters.cs` | 0.1 | yes |
| stranger question per hour: Visitor | `Strangers/Encounters.cs` | 0.02 | yes |
| encounter chance per hour: Trader | `Strangers/Encounters.cs` | 0.1 | yes |
| stranger question per hour: Trader | `Strangers/Encounters.cs` | 0.02 | yes |
| encounter chance per hour: Lodger | `Strangers/Encounters.cs` | 0.05 | yes |
| stranger question per hour: Lodger | `Strangers/Encounters.cs` | 0.02 | yes |
| encounter chance per hour: Prisoner | `Strangers/Encounters.cs` | 0.03 | yes |
| stranger question per hour: Prisoner | `Strangers/Encounters.cs` | 0.004 | yes |
| encounter chance per hour: Fallen | `Strangers/Encounters.cs` | 0.25 | yes |
| stranger question per hour: Fallen | `Strangers/Encounters.cs` | 0.08 | yes |
| encounter chance per hour: Pet | `Strangers/Encounters.cs` | 0.02 | yes |
| stranger question per hour: Pet | `Strangers/Encounters.cs` | 0.002 | yes |
| encounter chance per hour: Wild | `Strangers/Encounters.cs` | 0.015 | yes |
| stranger question per hour: Wild | `Strangers/Encounters.cs` | 0.002 | yes |
| encounters per day at most | `Strangers/Encounters.cs` | 3 | yes |
| social events per day: colonists divided by | `Social/SocialEvents.cs` | 5 | yes |
| social events per day at least | `Social/SocialEvents.cs` | 0.4 | yes |
| social events per day at most | `Social/SocialEvents.cs` | 2 | yes |
| a social event, per waking hour | `Social/SocialEvents.cs` | 0.3 | yes |
| hours between one colonist's moments | `Social/Moments.cs` | 6 | yes |
| nuisance tries per colonist per hour | `Strangers/Nuisances.cs` | 0.04 | yes |
| nuisance tries per hour at most | `Strangers/Nuisances.cs` | 0.5 | yes |
| hours between one colonist's nuisances | `Strangers/Nuisances.cs` | 12 | yes |
| a dream on waking | `Dreams/Dreams.cs` | 0.3 | yes |
| hours between dreams | `Dreams/Dreams.cs` | 18 | yes |
| days between catching ailments (a map) | `Ailments/AilmentTracker.cs` | 50 | yes |
| days between serious illnesses (a map) | `Ailments/AilmentTracker.cs` | 300 | yes |
| everyday ailments at once | `Ailments/AilmentTracker.cs` | 2 | yes |
| world events at once | `World/WorldSituations.cs` | 2 | yes |
| a decree request, per day | `Decrees/Decrees.cs` | 0.35 | yes |
| hours between Rimfolk's questions that can wait | `Lean/LetterGap.cs` | 12 | yes |

Mirrored in `Tools/Balance/Rates.cs` (estimates, not read from anywhere):

- The storyteller sends an everyday event every 4.8 days (Cassandra); the base game's own weigh 20, and Rimfolk's events share the rest up to the code's cap.
- A big threat every 9 days. Strangers at home: visitor 12% of hours, trader 8% of hours, lodger 3% of hours, prisoner 20% of hours, fallen 2% of hours, pet 85% of hours, wild 95% of hours.
- 60% of picked events can be cast; dilemmas, encounters and social events are cast 80% of the time times how often the conditions they name hold (a tie, a memory of a death, an illness: `Rates.Gates`). Each trait a filter names reaches 7% of colonists.
- Colonists talk 0.6 times a waking hour; the moments that fit (their weight times how often their conditions hold) compete with chitchat's 1.
- A settled calendar holds 8 days (6 colonists) or 11 (12); a personal story runs 8-20 days; an ambition 8-18.
- What colonists do all day (builders, growers, crafters, cooks, researchers, hunters, doctors) and the share of days that bring each ailment's cause, by season.
- The play styles: the game's own moods around Rimfolk, how many wishes are granted and how soon, which answers are chosen, how ailments are tended, how often pressures and setbacks come:

| | well run | average | neglectful |
|---|---|---|---|
| The game's own moods (good / bad) | +34 / -8 | +26 / -13 | +18 / -22 |
| Wishes granted | 80% | 50% | 12% |
| Promises made / kept | 20% / 95% | 12% / 70% | 5% / 30% |
| Lasting discomforts that voice a wish, per colonist | 0.1 | 0.5 | 1.8 |
| Answers: best for the colony / left to the default | 70% / 5% | 35% / 20% | 10% / 65% |
| Ambitions reached | 75% | 50% | 15% |
| Ailments tended (quality) | 85% (85%) | 55% (65%) | 20% (50%) |
| Setbacks (factor on the table) | x0.6 | x1.0 | x1.6 |
| A varied table | 55% | 20% | 2% |
| The same meal again (stage 1 / 2 / 3) | 6% / 0% / 0% | 28% / 10% / 0% | 45% / 20% / 0% |
| The same work again (stage 1 / 2 / 3) | 9% / 3% / 0% | 18% / 7% / 3% | 27% / 14% / 7% |
| Strained (-3 / -5 / -8) from life alone (the heart's life is set to it) | 2% / 0% / 0% | 8% / 2% / 0% | 20% / 8% / 2% |
| The colony's ways (-4 / -2 / +2 / +4) | 0% / 8% / 35% / 10% | 2% / 13% / 20% / 4% | 5% / 20% / 10% / 0% |

Not modelled: quirks, heirlooms and the colony's trait pride (small steady moods either way), decrees' own moods
(the player's choice), secrets, vendettas and nemeses' moods (rare), the base game's moods themselves (a fixed
good and bad per play style, of which a share counts as small for the heart), battles from events, the heart's trait
factors and its feelings (fear, grief, despair: in the life), and other mods' content.
The links between systems (`Interplay/`: strain eased, bonds, wishes made likelier by a run of setbacks, stories
moved on) give no mood of their own and aren't modelled; the social events, encounters and dreams they brought
are read from the defs like any other.

The "before" column is the simulator on the code and defs as they were before the heart's good side (6 November
2026, right after phase 3 of the review); the presets' rows always use the presets as they are now. Phase 3's own
before and after are in DESIGN.md ("Balance targets", "One heart").
