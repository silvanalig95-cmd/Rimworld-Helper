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
| Rimfolk's mood, well run | +3 to +6 | +6.5 | +5.5 | +5.6 |
| Rimfolk's mood, average | 0 to +3 | +2.2 | +1.6 | +2.5 |
| Rimfolk's mood, neglectful | -4 to -10 | -8.4 | -7.8 | -7.0 |
| Rimfolk's good moods at a high point (P99), well run | well short of the top | +18.1 | +16.6 | +17.5 |
| Time with +12 or more from Rimfolk's good moods, well run | rare | 22% | 12% | 16% |
| Questions (decision letters) per day, average | 1 every 2-3 days (0.33-0.5) | 0.49 | 0.53 | 0.49 |
| Questions and wishes voiced per day, average | (for reference) | 0.96 | 1.05 | 0.97 |
| Questions within 12 hours of another, average | few | 8% | 9% | 8% |
| Time with an everyday ailment, average | 10-15% | 15% | 14% | 14% |
| Time with an everyday ailment, well run | under 10-15% | 10% | 9% | 9% |
| The colony's whole mood, well run (for reference) | as before | 69% | 69% | 69% |
| The colony's whole mood, average (for reference) | as before | 64% | 63% | 64% |
| The colony's whole mood, neglectful (for reference) | as before | 37% | 38% | 39% |
| Time under the minor-break line, neglectful (letdowns as moods: 47%) | no more than before | 39% | 35% | 34% |
| The heart's day off a colonist-year, neglectful (letdowns as moods: 1.14) | under one | 1.77 | 1.58 | 0.73 |
| Steady / in good heart / in high spirits, well run (for reference) | a well-run colony's reward | 43% / 34% / 5% | 44% / 33% / 5% | - |
| Steady / in good heart / in high spirits, average (for reference) | a well-run colony's reward | 5% / 0% / 0% | 5% / 0% / 0% | - |
| Steady / in good heart / in high spirits, neglectful (for reference) | a well-run colony's reward | 0% / 0% / 0% | 0% / 0% / 0% | - |

## Before and after the heart's good side

The simulator on the code and defs as they were before the heart's good side (6 November 2026: strain alone, nothing
on the other side; `Tools/Balance/before.json`), and as they are now (default settings).

| | 6 before | 6 after | 12 before | 12 after |
|---|---|---|---|---|
| Rimfolk's mood, well run | +5.6 | +6.5 | +4.6 | +5.5 |
|   good memories (raw), well run | +8.7 (+8.7) | +8.9 (+8.9) | +7.1 (+7.1) | +7.1 (+7.1) |
|   bad memories and steady bad, well run | -7.4 | -8.7 | -5.7 | -6.9 |
| Rimfolk's mood, average | +2.5 | +2.2 | +1.9 | +1.6 |
|   good memories (raw), average | +6.1 (+6.1) | +6.0 (+6.0) | +5.0 (+5.0) | +5.0 (+5.0) |
|   bad memories and steady bad, average | -5.1 | -5.5 | -4.5 | -4.8 |
| Rimfolk's mood, neglectful | -7.0 | -8.4 | -6.5 | -7.8 |
|   good memories (raw), neglectful | +3.5 (+3.5) | +3.5 (+3.5) | +3.1 (+3.1) | +3.1 (+3.1) |
|   bad memories and steady bad, neglectful | -11.2 | -12.4 | -10.1 | -11.4 |
| Rimfolk's good moods at a high point (P99), well run | +17.5 | +18.1 | +15.8 | +16.6 |
| Time with +12 or more from Rimfolk's good moods, well run | 16% | 22% | 8% | 12% |
| Trust, year's average, average play | +13 | +13 | +9 | +9 |
| Questions per day, average play | 0.49 | 0.49 | 0.52 | 0.53 |
| Questions within 12 hours of another, average play | 8% | 8% | 10% | 9% |
| Days with two or more questions, average play | 6% | 6% | 7% | 7% |
| Wishes voiced per day, average play | 0.48 | 0.47 | 0.52 | 0.52 |
| Letters that only tell, per day, average play | 0.39 | 0.39 | 0.47 | 0.46 |
| News lines per day (one evening letter), average play | 1.71 | 1.71 | 2.34 | 2.37 |
| Time with an everyday ailment, well run | 9% | 10% | 9% | 9% |
| Time with an everyday ailment, average | 14% | 15% | 14% | 14% |
| Time with an everyday ailment, neglectful | 31% | 30% | 31% | 30% |
| Ailment days per colonist per year, average play | 8.7 | 9.1 | 8.5 | 8.7 |

## Rimfolk's mood per colonist

Good memories are as given (small joys as their moments); "raw" is the same. Steady moods are trust, the colony's
ways, ailments and strain, and what good things growing ordinary takes off Rimfolk's good moods. P10 and P90: a hard
and a good moment for one colonist.

| Case | Good memories (raw) | Bad memories | Steady good | Steady bad | **Net** | P10 / P90 | Good moods on the mind | Mood | Standards |
|---|---|---|---|---|---|---|---|---|---|
| 6 colonists, well run | +8.9 (+8.9) | -2.2 | +6.4 | -6.5 | **+6.5** | +0.6 / +12.3 | 2.3 | 69% | 0.89 |
| 6 colonists, average | +6.0 (+6.0) | -3.3 | +1.7 | -2.1 | **+2.2** | -5.6 / +10.0 | 1.6 | 64% | 0.29 |
| 6 colonists, neglectful | +3.5 (+3.5) | -5.0 | +0.6 | -7.4 | **-8.4** | -16.8 / +0.0 | 1.0 | 37% | 0.00 |
| 12 colonists, well run | +7.1 (+7.1) | -1.8 | +5.3 | -5.0 | **+5.5** | +0.1 / +10.9 | 1.9 | 69% | 0.86 |
| 12 colonists, average | +5.0 (+5.0) | -2.9 | +1.4 | -2.0 | **+1.6** | -5.7 / +9.0 | 1.4 | 63% | 0.24 |
| 12 colonists, neglectful | +3.1 (+3.1) | -4.5 | +0.5 | -7.0 | **-7.8** | -15.8 / +0.2 | 0.9 | 38% | 0.00 |

Over the year (net, in five 10-day stretches plus one; good and bad in brackets), 6 colonists:

| Play | days 0-9 | days 10-19 | days 20-29 | days 30-39 | days 40-49 | days 50-59 |
|---|---|---|---|---|---|---|
| well run | +6.2 (+14.0 / -7.8) | +6.3 (+14.7 / -8.3) | +6.6 (+15.4 / -8.8) | +6.6 (+15.5 / -9.0) | +6.8 (+16.1 / -9.2) | +6.7 (+16.0 / -9.2) |
| average | +2.1 (+7.3 / -5.2) | +2.6 (+8.0 / -5.4) | +1.9 (+7.5 / -5.6) | +2.1 (+7.6 / -5.5) | +2.0 (+7.5 / -5.6) | +2.3 (+7.8 / -5.5) |
| neglectful | -7.6 (+3.9 / -11.5) | -8.0 (+4.0 / -12.1) | -8.2 (+4.2 / -12.5) | -8.5 (+4.1 / -12.6) | -8.9 (+4.0 / -12.9) | -9.0 (+4.0 / -13.0) |

Where it comes from (6 colonists): what each source adds to a colonist's mood on average.

| Source | well run | average | neglectful |
|---|---|---|---|
| good things grown ordinary | -5.98 | -0.52 | -0.00 |
| strain | -0.10 | -0.72 | -3.79 |
| trust | +2.65 | +0.41 | -2.19 |
| wishes | +2.16 | +0.95 | +0.23 |
| the good side | +2.15 | +0.09 | +0.00 |
| meetings | +1.93 | +1.23 | +0.40 |
| life and honours | +1.32 | +1.09 | +0.80 |
| pressures (bad) | -0.20 | -0.68 | -1.23 |
| the colony's ways | +0.91 | +0.23 | -0.38 |
| wishes (bad) | -0.31 | -0.53 | -0.84 |
| moments | +0.71 | +0.67 | +0.59 |
| ailments | -0.21 | -0.35 | -0.71 |
| setbacks (lasting) (bad) | -0.27 | -0.39 | -0.68 |
| life goals | +0.64 | +0.46 | +0.27 |
| dilemmas | +0.60 | +0.42 | +0.18 |
| pressures | +0.56 | +0.20 | +0.02 |
| setbacks (bad) | -0.23 | -0.32 | -0.54 |
| social events | +0.50 | +0.51 | +0.50 |
| frictions (bad) | -0.44 | -0.48 | -0.39 |
| stories | +0.35 | +0.34 | +0.35 |
| meetings (bad) | -0.12 | -0.21 | -0.32 |
| dilemmas (bad) | -0.21 | -0.24 | -0.32 |
| calendar | +0.25 | +0.23 | +0.24 |
| dreams (bad) | -0.13 | -0.13 | -0.24 |
| events | +0.22 | +0.15 | +0.12 |
| nuisances (bad) | -0.04 | -0.12 | -0.20 |
| social events (bad) | -0.17 | -0.14 | -0.13 |
| health | -0.04 | -0.07 | -0.12 |
| encounters | +0.10 | +0.09 | +0.07 |
| encounters (bad) | -0.06 | -0.05 | -0.04 |

What good things growing ordinary takes off (6 colonists):

| Play | Net with the rule | Net without it | Good memories with | without |
|---|---|---|---|---|
| well run | +6.5 | +12.4 | +8.9 | +8.9 |
| average | +2.2 | +2.5 | +6.0 | +6.0 |
| neglectful | -8.4 | -8.2 | +3.5 | +3.5 |

## The heart

Each colonist's strain, played hour by hour (`HeartSim.cs`): the life of each play style (fights, losses, bad nights,
hunger, low spirits, decrees; sleep, quiet hours, recreation, gatherings and talks), set so that it alone gives the
shares the report always assumed, and on top what small letdowns and joys bring (`SmallMomentMath`): what Rimfolk's
own weigh beyond their moment, and the pile of every small mood on a colonist's mind, the game's (a share of its moods,
`Rates.GameSmallBad`, `GameSmallGood`) and Rimfolk's. "As moods" is the same with letdowns and joys as moods of their
own and nothing to the heart (as before phase 3). Strain built and eased is per colonist over the 60-day year.

| Case | Strained / worn / near breaking | Day off a year | Built | of it: life / Rimfolk's letdowns / small moods (the game's) / (Rimfolk's) | The game's small moods' share | Eased by small joys (the game's / Rimfolk's) |
|---|---|---|---|---|---|---|
| 6 colonists, well run | 3% / 0% / 0% | 0.00 | 181 | 148 / 29 / 2 / 2 | 1% | 15 / 21 |
| 6 colonists, average | 14% / 4% / 1% | 0.16 | 317 | 260 / 35 / 14 / 8 | 4% | 23 / 21 |
| 6 colonists, neglectful | 31% / 26% / 20% | 1.77 | 453 | 370 / 38 / 27 / 18 | 6% | 5 / 14 |
| 12 colonists, well run | 3% / 0% / 0% | 0.00 | 179 | 149 / 27 / 2 / 2 | 1% | 14 / 20 |
| 12 colonists, average | 14% / 5% / 2% | 0.17 | 313 | 256 / 35 / 14 / 8 | 4% | 22 / 20 |
| 12 colonists, neglectful | 27% / 24% / 28% | 1.58 | 449 | 366 / 39 / 27 / 16 | 6% | 6 / 15 |
| 6 colonists, well run, as moods | 4% / 0% / 0% | 0.00 | 146 | 146 / 0 / 0 / 0 | 0% | 0 / 0 |
| 6 colonists, average, as moods | 13% / 4% / 1% | 0.14 | 250 | 250 / 0 / 0 / 0 | 0% | 0 / 0 |
| 6 colonists, neglectful, as moods | 30% / 21% / 13% | 1.14 | 388 | 388 / 0 / 0 / 0 | 0% | 0 / 0 |

The colony's mood: the whole of it, the game's moods included, and what good things growing ordinary takes off the
game's own good moods (it comes off every good mood by how ordinary each is: the game's comforts mostly).

| Case | Whole mood (P10 / P90) | Under the minor-break line | Taken off the game's good moods | Taken off Rimfolk's |
|---|---|---|---|---|
| 6 colonists, well run | 69% (64% / 74%) | 0% | -13.8 | -6.0 |
| 6 colonists, average | 64% (55% / 71%) | 0% | -1.5 | -0.5 |
| 6 colonists, neglectful | 37% (26% / 48%) | 39% | -0.0 | -0.0 |
| 12 colonists, well run | 69% (64% / 74%) | 0% | -12.7 | -4.5 |
| 12 colonists, average | 63% (55% / 71%) | 0% | -1.2 | -0.4 |
| 12 colonists, neglectful | 38% (27% / 49%) | 35% | -0.0 | -0.0 |
| 6 colonists, well run, without growing ordinary | 88% (78% / 100%) | 0% | +-0.0 | +0.0 |
| 6 colonists, average, without growing ordinary | 66% (55% / 77%) | 0% | +-0.0 | +0.0 |
| 6 colonists, neglectful, without growing ordinary | 38% (26% / 49%) | 38% | +-0.0 | +0.0 |
| 6 colonists, well run, letdowns and joys as moods | 68% (62% / 73%) | 0% | -13.5 | -6.4 |
| 6 colonists, average, letdowns and joys as moods | 63% (54% / 71%) | 0% | -1.2 | -0.5 |
| 6 colonists, neglectful, letdowns and joys as moods | 35% (25% / 46%) | 47% | -0.0 | -0.0 |

## The heart's good side

The other side of strain (`HeartMath`, "The good side"; `HeartSim.cs`): hardship spends it before strain builds; what a
gathering, a talk or a small joy eases beyond the strain there is fills it at its share, with friends close by and a
purpose (the heart's needs well met, drawn a day at a time per play style), deeds the colony speaks well of and a life
goal moving; it fades slowly. Steady from 20, in good heart from 45, in high spirits from 65 (+2, +3 and +5 mood); in high spirits, now and then, an inspiration or a kind word for someone close.
Filled and spent are per colonist over the 60-day year; "without" is the same seeds with its setting off.

| Case | Steady | In good heart | In high spirits | Strain at nothing (without) | Inspirations / kind words a colonist-year | Filled by | Spent by |
|---|---|---|---|---|---|---|---|
| 6 colonists, well run | 43% | 34% | 5% | 69% (63%) | 0.18 / 0.19 | purpose 59, belonging 46, good talks 35, small joys 19 | life 63, Rimfolk's letdowns 16, small moods 2 |
| 6 colonists, average | 5% | 0% | 0% | 40% (35%) | 0.00 / 0.00 | purpose 31, belonging 20, good talks 20, small joys 9 | life 49, Rimfolk's letdowns 15, small moods 9 |
| 6 colonists, neglectful | 0% | 0% | 0% | 3% (3%) | 0.00 / 0.00 | purpose 6, good talks 5, small joys 3, belonging 2 | life 11, Rimfolk's letdowns 4, small moods 4 |
| 12 colonists, well run | 44% | 33% | 5% | 67% | 0.16 / 0.16 | purpose 59, belonging 46, good talks 35, small joys 20 | life 63, Rimfolk's letdowns 15, small moods 2 |
| 12 colonists, average | 5% | 0% | 0% | 38% | 0.00 / 0.00 | purpose 32, belonging 20, good talks 20, small joys 9 | life 49, Rimfolk's letdowns 15, small moods 9 |
| 12 colonists, neglectful | 0% | 0% | 0% | 2% | 0.00 / 0.00 | purpose 6, good talks 5, small joys 3, belonging 2 | life 10, Rimfolk's letdowns 4, small moods 3 |
| 6 colonists, well run, lightTouch | 42% | 35% | 7% | 68% | 0.23 / 0.24 | purpose 58, belonging 45, good talks 35, small joys 21 | life 64, Rimfolk's letdowns 5, small moods 0 |
| 6 colonists, average, lightTouch | 8% | 0% | 0% | 41% | 0.00 / 0.00 | purpose 32, good talks 20, belonging 20, small joys 11 | life 54, Rimfolk's letdowns 11, small moods 5 |
| 6 colonists, neglectful, lightTouch | 0% | 0% | 0% | 3% | 0.00 / 0.00 | purpose 5, good talks 5, small joys 4, belonging 2 | life 9, small moods 4, Rimfolk's letdowns 4 |
| 6 colonists, well run, storyRich | 44% | 32% | 5% | 68% | 0.16 / 0.16 | purpose 59, belonging 46, good talks 35, small joys 19 | life 62, Rimfolk's letdowns 16, small moods 3 |
| 6 colonists, average, storyRich | 4% | 0% | 0% | 39% | 0.00 / 0.00 | purpose 32, belonging 20, good talks 20, small joys 10 | life 49, Rimfolk's letdowns 15, small moods 10 |
| 6 colonists, neglectful, storyRich | 0% | 0% | 0% | 3% | 0.00 / 0.00 | purpose 6, good talks 5, small joys 3, belonging 2 | life 11, Rimfolk's letdowns 4, small moods 4 |
| 6 colonists, well run, demanding | 45% | 29% | 3% | 67% | 0.10 / 0.13 | purpose 59, belonging 46, good talks 35, small joys 17 | life 61, Rimfolk's letdowns 19, small moods 3 |
| 6 colonists, average, demanding | 3% | 0% | 0% | 38% | 0.00 / 0.00 | purpose 32, belonging 20, good talks 20, small joys 8 | life 48, Rimfolk's letdowns 16, small moods 9 |
| 6 colonists, neglectful, demanding | 0% | 0% | 0% | 3% | 0.00 / 0.00 | purpose 6, good talks 5, small joys 2, belonging 2 | life 10, Rimfolk's letdowns 4, small moods 3 |

What it changes (6 colonists, the same seeds with and without it):

| Play | Rimfolk's mood | Its own mood | Good moods at a high point (P99) | +12 or more | Strained / worn / near breaking | Under the minor-break line | Day off a year | Whole mood |
|---|---|---|---|---|---|---|---|---|
| well run | +5.5 to +6.5 | +2.2 | +17.6 to +18.1 | 17% to 22% | 5%/0%/0% to 3%/0%/0% | 0% to 0% | 0.00 to 0.00 | 69% to 69% |
| average | +2.0 to +2.2 | +0.1 | +20.0 to +20.0 | 17% to 17% | 15%/5%/1% to 14%/4%/1% | 0% to 0% | 0.17 to 0.16 | 64% to 64% |
| neglectful | -8.1 to -8.4 | +0.0 | +16.0 to +16.0 | 5% to 5% | 31%/25%/19% to 31%/26%/20% | 38% to 39% | 1.63 to 1.77 | 38% to 37% |

## Trust

Average trust in the council (-100 to 100; trusting from 25, devoted from 60, doubtful from -15, bitter from -45).

| Case | Start of year | End of year | Year's average | Trusting or devoted | Doubtful or bitter | By stretch |
|---|---|---|---|---|---|---|
| 6 colonists, well run | +34 | +70 | +56 | 88% | 0% | +38, +46, +54, +60, +66, +69 |
| 6 colonists, average | +9 | +16 | +13 | 27% | 8% | +10, +12, +13, +14, +14, +16 |
| 6 colonists, neglectful | -16 | -41 | -30 | 0% | 82% | -18, -24, -29, -33, -36, -40 |
| 12 colonists, well run | +21 | +46 | +35 | 66% | 0% | +23, +29, +34, +38, +42, +45 |
| 12 colonists, average | +6 | +11 | +9 | 16% | 5% | +6, +9, +10, +10, +10, +11 |
| 12 colonists, neglectful | -10 | -26 | -19 | 0% | 57% | -12, -15, -18, -21, -23, -25 |

## Letters and messages per day

Questions are choice letters (a dilemma, an event with answers, a calendar day, a meeting's vote, a mission
offered, a world event's question). Wishes are letters that ask without buttons. Letters only tell. Messages show
on screen as they happen; the evening's colony news gathers the rest into one letter a day. Flavour news (the
smaller encounters, moments and dreams) isn't shown at the default message level and isn't counted.

**6 colonists, average play**: 0.49 questions, 0.47 wishes voiced, 0.39 letters, 0.61 messages and 1.71 news lines a day.

| System | Questions | Wishes | Letters | Messages | News lines |
|---|---|---|---|---|---|
| wishes | 0.00 | 0.37 | 0.03 | 0.00 | 0.44 |
| event wishes | 0.00 | 0.10 | 0.00 | 0.00 | 0.00 |
| dilemmas | 0.10 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (after a raid) | 0.04 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (strangers) | 0.07 | 0.00 | 0.00 | 0.00 | 0.00 |
| events | 0.07 | 0.00 | 0.03 | 0.00 | 0.00 |
| encounters | 0.00 | 0.00 | 0.00 | 0.00 | 0.27 |
| social events | 0.00 | 0.00 | 0.00 | 0.00 | 0.30 |
| ailments | 0.00 | 0.00 | 0.01 | 0.00 | 0.17 |
| health | 0.00 | 0.00 | 0.01 | 0.00 | 0.11 |
| calendar | 0.10 | 0.00 | 0.03 | 0.00 | 0.00 |
| stories | 0.00 | 0.00 | 0.04 | 0.00 | 0.12 |
| world events | 0.04 | 0.00 | 0.02 | 0.00 | 0.00 |
| missions | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| play-outs | 0.00 | 0.00 | 0.00 | 0.32 | 0.00 |
| meetings | 0.06 | 0.00 | 0.03 | 0.00 | 0.03 |
| decrees | 0.00 | 0.00 | 0.00 | 0.13 | 0.00 |
| dreams | 0.00 | 0.00 | 0.00 | 0.00 | 0.01 |
| people | 0.00 | 0.00 | 0.12 | 0.00 | 0.25 |
| colony | 0.00 | 0.00 | 0.04 | 0.16 | 0.00 |
| world | 0.00 | 0.00 | 0.01 | 0.00 | 0.00 |

**12 colonists, average play**: 0.53 questions, 0.52 wishes voiced, 0.46 letters, 0.65 messages and 2.37 news lines a day.

| System | Questions | Wishes | Letters | Messages | News lines |
|---|---|---|---|---|---|
| wishes | 0.00 | 0.33 | 0.04 | 0.00 | 0.48 |
| event wishes | 0.00 | 0.20 | 0.00 | 0.00 | 0.00 |
| dilemmas | 0.10 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (after a raid) | 0.04 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (strangers) | 0.07 | 0.00 | 0.00 | 0.00 | 0.00 |
| events | 0.07 | 0.00 | 0.04 | 0.00 | 0.00 |
| encounters | 0.00 | 0.00 | 0.00 | 0.00 | 0.28 |
| social events | 0.00 | 0.00 | 0.00 | 0.00 | 0.51 |
| ailments | 0.00 | 0.00 | 0.02 | 0.00 | 0.34 |
| health | 0.00 | 0.00 | 0.01 | 0.00 | 0.17 |
| calendar | 0.13 | 0.00 | 0.05 | 0.00 | 0.00 |
| stories | 0.00 | 0.00 | 0.07 | 0.00 | 0.19 |
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
| well run | 0.50 | 0.64 | 0.48 | 0.65 | 1.70 | 8% | 7% |
| average | 0.49 | 0.47 | 0.39 | 0.61 | 1.71 | 8% | 6% |
| neglectful | 0.48 | 0.53 | 0.37 | 0.57 | 1.99 | 8% | 6% |

## Ailments

Everyday ailments and what they leave behind (a lingering cough), per colonist over the year. "Not quiet" leaves
out the smallest (a stiff neck, hiccups, a smoke cough), which only a player who wants every message hears about.

| Case | Ailment days per colonist | Time with one | Time with one, not quiet | Caught, per colony | Circumstance, per colony | Serious, per colony |
|---|---|---|---|---|---|---|
| 6 colonists, well run | 5.9 | 10% | 8% | 1.1 | 31.5 | 0.3 |
| 6 colonists, average | 9.1 | 15% | 13% | 0.9 | 29.2 | 0.5 |
| 6 colonists, neglectful | 18.2 | 30% | 26% | 1.1 | 42.1 | 0.8 |
| 12 colonists, well run | 5.6 | 9% | 8% | 1.0 | 62.7 | 0.4 |
| 12 colonists, average | 8.7 | 14% | 12% | 1.1 | 59.4 | 0.5 |
| 12 colonists, neglectful | 17.9 | 30% | 26% | 0.9 | 85.8 | 1.1 |

What fills the time (days per colonist per year, 6 colonists):

| Ailment | well run | average | neglectful |
|---|---|---|---|
| Lice | 0.0 | 0.4 | 3.4 |
| Convalescent | 0.4 | 0.9 | 1.4 |
| Insomnia | 0.1 | 0.6 | 1.7 |
| RimrotCough | 0.0 | 0.3 | 2.1 |
| HeadCold | 0.5 | 0.5 | 0.5 |
| ChestCold | 0.4 | 0.4 | 0.5 |
| ColdSore | 0.1 | 0.3 | 0.9 |
| StrainedBack | 0.3 | 0.4 | 0.6 |
| SmokeCough | 0.0 | 0.2 | 0.9 |
| Worms | 0.0 | 0.2 | 0.9 |
| Toothache | 0.1 | 0.3 | 0.4 |
| SoreHands | 0.2 | 0.3 | 0.3 |
| BadNights | 0.2 | 0.2 | 0.3 |
| ChappedHands | 0.2 | 0.2 | 0.3 |
| SoreElbow | 0.2 | 0.2 | 0.3 |
| Splinter | 0.1 | 0.2 | 0.4 |

"Serious, per colony" above is Rimfolk's serious illnesses as the settings ship, sharing the game's own rate. Those and
the game's own diseases, with the budget and without it, over six years a colony, are
`dotnet run -c Release --project Tools/Balance -- --illness` (IllnessSim.cs; DESIGN.md, "The shared illness budget"),
measured on 9 November 2026 for the soft budget (600 colony-years a case, the same seeds and the same hours of the game's
own with it and without; written here by hand, kept when the report is written again). `IllnessBudgetTests` reads these
tables against the targets in `IllnessBudgetMath` (Rimfolk's share 35% to 45% in an average colony, in all at most a
fifth above the game's own kept well or fairly, well below no budget's neglected, two at once rare).

A temperate forest at Strive to survive (the game's own every 50 days, 1.2 a year; held back three times in four, in all
at most about a quarter above the game's own):

| Case | The game's own | Without: Rimfolk's | Without: in all | With: Rimfolk's | With: the game's | With: in all | Above the game's own | Rimfolk's share | Held back | Came anyway | Put off | Two at once, without / with | The game's deadly, without / with |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 6 colonists, well run | 1.17 | 0.19 | 1.36 | 0.31 | 0.95 | 1.27 | +8% | 25 % | 0.22 | 0.06 | 0.20 | 0.12 / 0.09 | 0.81 / 0.67 |
| 6 colonists, average | 1.21 | 0.43 | 1.64 | 0.50 | 0.86 | 1.36 | +13% | 37 % | 0.35 | 0.13 | 0.46 | 0.17 / 0.09 | 0.78 / 0.56 |
| 6 colonists, neglectful | 1.22 | 1.11 | 2.33 | 0.83 | 0.59 | 1.42 | +17% | 58 % | 0.63 | 0.18 | 2.88 | 0.20 / 0.06 | 0.80 / 0.39 |
| 12 colonists, well run | 1.25 | 0.30 | 1.54 | 0.38 | 0.98 | 1.36 | +9% | 28 % | 0.27 | 0.09 | 0.32 | 0.23 / 0.16 | 0.80 / 0.63 |
| 12 colonists, average | 1.25 | 0.63 | 1.89 | 0.62 | 0.78 | 1.39 | +11% | 44 % | 0.48 | 0.11 | 0.99 | 0.26 / 0.14 | 0.82 / 0.50 |
| 12 colonists, neglectful | 1.16 | 1.40 | 2.56 | 0.91 | 0.55 | 1.46 | +26% | 62 % | 0.61 | 0.23 | 6.17 | 0.30 / 0.09 | 0.77 / 0.38 |

Two serious illnesses at once on one colonist, a colony-year, by which came on which:

| Case | Without: Rimfolk's on the game's | Without: the game's on Rimfolk's | Without: the game's own two | With: Rimfolk's on the game's | With: the game's on Rimfolk's | With: the game's own two |
|---|---|---|---|---|---|---|
| 6 colonists, well run | 0.008 | 0.008 | 0.105 | 0.000 | 0.010 | 0.080 |
| 6 colonists, average | 0.033 | 0.028 | 0.105 | 0.000 | 0.018 | 0.068 |
| 6 colonists, neglectful | 0.065 | 0.058 | 0.080 | 0.003 | 0.007 | 0.045 |
| 12 colonists, well run | 0.020 | 0.028 | 0.185 | 0.003 | 0.008 | 0.147 |
| 12 colonists, average | 0.045 | 0.025 | 0.190 | 0.002 | 0.005 | 0.137 |
| 12 colonists, neglectful | 0.100 | 0.058 | 0.140 | 0.013 | 0.005 | 0.068 |

Side by side, serious cases in all a colony-year: the game's own alone, 0.177.0's strict budget (as measured for it),
the soft budget now, and no budget:

| Case | The game's own | 0.177.0: in all | 0.177.0: Rimfolk's share | Now: in all | Now: above the game's own | Now: Rimfolk's share | Without the budget: in all |
|---|---|---|---|---|---|---|---|
| 6 colonists, well run | 1.17 | 1.18 | 8 % | 1.27 | +8% | 25 % | 1.36 |
| 6 colonists, average | 1.21 | 1.22 | 14 % | 1.36 | +13% | 37 % | 1.64 |
| 6 colonists, neglectful | 1.22 | 1.20 | 29 % | 1.42 | +17% | 58 % | 2.33 |
| 12 colonists, well run | 1.25 | 1.25 | 12 % | 1.36 | +9% | 28 % | 1.54 |
| 12 colonists, average | 1.25 | 1.25 | 21 % | 1.39 | +11% | 44 % | 1.89 |
| 12 colonists, neglectful | 1.16 | 1.16 | 32 % | 1.46 | +26% | 62 % | 2.56 |

At Adventure story's interval (`--interval 1.5`: every 75 days, 0.8 a year; Rimfolk's catching clock and the land's
serious illnesses lean with the interval as in the game, which 0.177.0's measure didn't):

| Case | The game's own | Without: Rimfolk's | Without: in all | With: Rimfolk's | With: the game's | With: in all | Above the game's own | Rimfolk's share | Held back | Came anyway | Put off | Two at once, without / with | The game's deadly, without / with |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 6 colonists, well run | 0.78 | 0.14 | 0.92 | 0.15 | 0.69 | 0.84 | +7% | 18 % | 0.09 | 0.03 | 0.06 | 0.06 / 0.03 | 0.54 / 0.48 |
| 6 colonists, average | 0.80 | 0.34 | 1.14 | 0.32 | 0.60 | 0.92 | +14% | 35 % | 0.21 | 0.07 | 0.22 | 0.07 / 0.04 | 0.53 / 0.39 |
| 6 colonists, neglectful | 0.84 | 1.09 | 1.92 | 0.59 | 0.44 | 1.03 | +23% | 57 % | 0.40 | 0.15 | 1.84 | 0.12 / 0.04 | 0.54 / 0.27 |
| 12 colonists, well run | 0.84 | 0.22 | 1.06 | 0.20 | 0.72 | 0.92 | +9% | 22 % | 0.13 | 0.05 | 0.12 | 0.11 / 0.08 | 0.53 / 0.45 |
| 12 colonists, average | 0.81 | 0.61 | 1.41 | 0.40 | 0.52 | 0.92 | +14% | 43 % | 0.29 | 0.08 | 0.55 | 0.11 / 0.05 | 0.54 / 0.35 |
| 12 colonists, neglectful | 0.77 | 1.41 | 2.18 | 0.64 | 0.34 | 0.99 | +28% | 65 % | 0.43 | 0.18 | 3.81 | 0.17 / 0.05 | 0.51 / 0.21 |

Two serious illnesses at once on one colonist, a colony-year, by which came on which:

| Case | Without: Rimfolk's on the game's | Without: the game's on Rimfolk's | Without: the game's own two | With: Rimfolk's on the game's | With: the game's on Rimfolk's | With: the game's own two |
|---|---|---|---|---|---|---|
| 6 colonists, well run | 0.008 | 0.002 | 0.048 | 0.000 | 0.000 | 0.030 |
| 6 colonists, average | 0.020 | 0.005 | 0.045 | 0.002 | 0.003 | 0.032 |
| 6 colonists, neglectful | 0.060 | 0.023 | 0.040 | 0.003 | 0.000 | 0.038 |
| 12 colonists, well run | 0.010 | 0.013 | 0.083 | 0.000 | 0.005 | 0.072 |
| 12 colonists, average | 0.018 | 0.018 | 0.070 | 0.000 | 0.003 | 0.043 |
| 12 colonists, neglectful | 0.052 | 0.032 | 0.090 | 0.000 | 0.005 | 0.047 |

Side by side, serious cases in all a colony-year: the game's own alone, 0.177.0's strict budget (as measured for it),
the soft budget now, and no budget:

| Case | The game's own | 0.177.0: in all | 0.177.0: Rimfolk's share | Now: in all | Now: above the game's own | Now: Rimfolk's share | Without the budget: in all |
|---|---|---|---|---|---|---|---|
| 6 colonists, well run | 0.78 | 0.79 | 15 % | 0.84 | +7% | 18 % | 0.92 |
| 6 colonists, average | 0.80 | 0.83 | 22 % | 0.92 | +14% | 35 % | 1.14 |
| 6 colonists, neglectful | 0.84 | 0.83 | 34 % | 1.03 | +23% | 57 % | 1.92 |
| 12 colonists, well run | 0.84 | 0.85 | 17 % | 0.92 | +9% | 22 % | 1.06 |
| 12 colonists, average | 0.81 | 0.82 | 30 % | 0.92 | +14% | 43 % | 1.41 |
| 12 colonists, neglectful | 0.77 | 0.74 | 38 % | 0.99 | +28% | 65 % | 2.18 |

## The presets

The style presets of the settings window, applied as the window applies them (6 colonists).

| Preset | Play | Net | Good memories | Bad + steady bad | Questions per day | Wishes per day | Time ill |
|---|---|---|---|---|---|---|---|
| default | well run | +6.5 | +8.9 | -8.7 | 0.50 | 0.64 | 10% |
| default | average | +2.2 | +6.0 | -5.5 | 0.49 | 0.47 | 15% |
| default | neglectful | -8.4 | +3.5 | -12.4 | 0.48 | 0.53 | 30% |
| lightTouch | well run | +7.1 | +6.7 | -5.1 | 0.46 | 0.30 | 4% |
| lightTouch | average | +4.2 | +4.8 | -2.3 | 0.45 | 0.24 | 7% |
| lightTouch | neglectful | -4.3 | +3.1 | -7.8 | 0.44 | 0.31 | 16% |
| storyRich | well run | +7.0 | +10.0 | -9.5 | 0.58 | 0.65 | 10% |
| storyRich | average | +2.6 | +6.8 | -5.9 | 0.57 | 0.47 | 14% |
| storyRich | neglectful | -7.7 | +4.3 | -12.5 | 0.55 | 0.52 | 29% |
| demanding | well run | +2.3 | +6.3 | -9.0 | 0.50 | 0.70 | 12% |
| demanding | average | -3.5 | +4.2 | -8.9 | 0.50 | 0.51 | 18% |
| demanding | neglectful | -16.0 | +2.5 | -19.0 | 0.48 | 0.65 | 36% |

## How often things happen (per colony per year)

| What | 6, well run | 6, average | 6, neglectful | 12, well run | 12, average | 12, neglectful |
|---|---|---|---|---|---|---|
| after-effects | 0.5 | 0.8 | 1.0 | 0.8 | 0.9 | 1.4 |
| ailments | 31.5 | 29.2 | 42.1 | 62.7 | 59.4 | 85.8 |
| ambitions reached | 2.8 | 1.8 | 0.6 | 2.8 | 1.9 | 0.6 |
| calendar days | 8.0 | 8.0 | 8.0 | 11.0 | 11.0 | 11.0 |
| camps | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 |
| catching ailments | 1.1 | 0.9 | 1.1 | 1.0 | 1.1 | 0.9 |
| convalescence | 2.4 | 3.9 | 6.0 | 5.2 | 7.5 | 12.3 |
| days of one faith for the colony | 2.3 | 2.6 | 2.7 | 2.3 | 3.0 | 2.3 |
| decree requests | 7.7 | 7.7 | 7.8 | 7.4 | 8.0 | 7.9 |
| dilemmas | 13.6 | 13.1 | 11.9 | 13.5 | 13.0 | 11.9 |
| dreams | 109.7 | 108.2 | 108.3 | 213.8 | 217.4 | 215.9 |
| encounters | 53.4 | 51.6 | 48.2 | 53.9 | 52.6 | 48.1 |
| evenings of faith | 1.4 | 1.8 | 1.7 | 1.4 | 1.9 | 1.6 |
| events | 6.3 | 5.9 | 6.4 | 6.0 | 6.8 | 6.0 |
| friction moments | 202.3 | 266.3 | 358.5 | 405.3 | 534.4 | 721.0 |
| meetings | 3.8 | 3.8 | 3.8 | 3.8 | 3.8 | 3.8 |
| missions | 0.2 | 0.1 | 0.2 | 0.1 | 0.1 | 0.1 |
| moments | 711.4 | 662.3 | 592.1 | 1421.1 | 1322.4 | 1184.9 |
| nuisances | 27.3 | 82.9 | 141.3 | 53.5 | 164.3 | 281.7 |
| personal touches | 10.0 | 9.9 | 10.1 | 10.0 | 9.9 | 10.0 |
| play-outs | 5.4 | 4.3 | 2.7 | 5.4 | 4.2 | 2.6 |
| quiet troubles | 4.0 | 0.9 | 0.0 | 6.7 | 1.4 | 0.0 |
| serious illnesses | 0.3 | 0.5 | 0.8 | 0.4 | 0.5 | 1.1 |
| serious illnesses put off | 0.2 | 0.4 | 2.6 | 0.3 | 0.9 | 4.7 |
| small joys | 261.3 | 125.3 | 55.4 | 515.1 | 252.4 | 112.2 |
| small letdowns | 640.9 | 1244.7 | 2179.8 | 1206.3 | 2434.6 | 4333.4 |
| social events | 71.3 | 70.9 | 71.5 | 117.8 | 117.7 | 117.8 |
| stories | 3.3 | 3.3 | 3.3 | 5.2 | 5.1 | 5.2 |
| the game's deadly diseases | 0.8 | 0.6 | 0.4 | 0.7 | 0.5 | 0.4 |
| the game's diseases | 1.0 | 0.9 | 0.6 | 1.0 | 0.7 | 0.6 |
| the game's diseases came anyway | 0.1 | 0.1 | 0.2 | 0.1 | 0.1 | 0.2 |
| the game's diseases held back | 0.2 | 0.3 | 0.5 | 0.2 | 0.4 | 0.8 |
| two at once: the game's on Rimfolk's | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 |
| two at once: the game's own two | 0.1 | 0.1 | 0.0 | 0.2 | 0.1 | 0.1 |
| two serious illnesses at once | 0.1 | 0.1 | 0.1 | 0.2 | 0.2 | 0.1 |
| wishes granted | 31.6 | 14.7 | 3.7 | 33.4 | 16.1 | 5.4 |
| wishes lapsed | 6.4 | 13.4 | 27.8 | 6.7 | 14.9 | 34.3 |
| wishes made | 38.3 | 28.3 | 31.7 | 40.3 | 31.4 | 40.0 |
| world events | 1.0 | 1.0 | 1.1 | 1.0 | 1.0 | 1.1 |

## What the model reads and assumes

From the defs: 2034 moods, 240 dilemmas, 337 events and missions, 163 encounters,
123 social events, 131 moments, 272 dreams, 24 nuisances, 119 wishes,
80 ailments and illnesses, 13 catching ones, 71 world events (546 defs for other mods left out).

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
| what every mission offer weighs together in the events' draw | `Missions/NeighboursFourMath.cs` | 6.8 | yes |
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
| days between catching ailments (a map) | `Ailments/AilmentTracker.cs` | 60 | yes |
| days between serious illnesses (a map) | `Ailments/AilmentTracker.cs` | 300 | yes |
| everyday ailments at once | `Ailments/AilmentTracker.cs` | 2 | yes |
| days between two serious illnesses (the colony) | `Ailments/AilmentTracker.cs` | 30 | yes |
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
