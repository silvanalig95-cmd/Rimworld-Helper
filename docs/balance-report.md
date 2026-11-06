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
| Rimfolk's mood, well run | +3 to +6 | +6.4 | +5.7 | +5.6 |
| Rimfolk's mood, average | 0 to +3 | +2.6 | +2.0 | +2.5 |
| Rimfolk's mood, neglectful | -4 to -10 | -7.2 | -6.4 | -7.0 |
| Rimfolk's good moods at a high point (P99), well run | well short of the top | +17.9 | +16.6 | +17.5 |
| Time with +12 or more from Rimfolk's good moods, well run | rare | 21% | 12% | 16% |
| Questions (decision letters) per day, average | 1 every 2-3 days (0.33-0.5) | 0.50 | 0.53 | 0.49 |
| Questions and wishes voiced per day, average | (for reference) | 0.97 | 1.05 | 0.97 |
| Questions within 12 hours of another, average | few | 8% | 10% | 8% |
| Time with an everyday ailment, average | 10-15% | 15% | 14% | 14% |
| Time with an everyday ailment, well run | under 10-15% | 9% | 10% | 9% |
| The colony's whole mood, well run (for reference) | as before | 69% | 69% | 69% |
| The colony's whole mood, average (for reference) | as before | 64% | 64% | 64% |
| The colony's whole mood, neglectful (for reference) | as before | 39% | 40% | 39% |
| Time under the minor-break line, neglectful (letdowns as moods: 43%) | no more than before | 34% | 29% | 34% |
| The heart's day off a colonist-year, neglectful (letdowns as moods: 0.39) | under one | 0.72 | 0.78 | 0.73 |
| Steady / in good heart / in high spirits, well run (for reference) | a well-run colony's reward | 48% / 28% / 3% | 48% / 27% / 3% | - |
| Steady / in good heart / in high spirits, average (for reference) | a well-run colony's reward | 2% / 0% / 0% | 2% / 0% / 0% | - |
| Steady / in good heart / in high spirits, neglectful (for reference) | a well-run colony's reward | 0% / 0% / 0% | 0% / 0% / 0% | - |

## Before and after the heart's good side

The simulator on the code and defs as they were before the heart's good side (6 November 2026: strain alone, nothing
on the other side; `Tools/Balance/before.json`), and as they are now (default settings).

| | 6 before | 6 after | 12 before | 12 after |
|---|---|---|---|---|
| Rimfolk's mood, well run | +5.6 | +6.4 | +4.6 | +5.7 |
|   good memories (raw), well run | +8.7 (+8.7) | +8.7 (+8.7) | +7.1 (+7.1) | +7.2 (+7.2) |
|   bad memories and steady bad, well run | -7.4 | -8.5 | -5.7 | -6.7 |
| Rimfolk's mood, average | +2.5 | +2.6 | +1.9 | +2.0 |
|   good memories (raw), average | +6.1 (+6.1) | +6.1 (+6.1) | +5.0 (+5.0) | +5.0 (+5.0) |
|   bad memories and steady bad, average | -5.1 | -5.1 | -4.5 | -4.4 |
| Rimfolk's mood, neglectful | -7.0 | -7.2 | -6.5 | -6.4 |
|   good memories (raw), neglectful | +3.5 (+3.5) | +3.5 (+3.5) | +3.1 (+3.1) | +3.1 (+3.1) |
|   bad memories and steady bad, neglectful | -11.2 | -11.3 | -10.1 | -10.1 |
| Rimfolk's good moods at a high point (P99), well run | +17.5 | +17.9 | +15.8 | +16.6 |
| Time with +12 or more from Rimfolk's good moods, well run | 16% | 21% | 8% | 12% |
| Trust, year's average, average play | +13 | +12 | +9 | +9 |
| Questions per day, average play | 0.49 | 0.50 | 0.52 | 0.53 |
| Questions within 12 hours of another, average play | 8% | 8% | 10% | 10% |
| Days with two or more questions, average play | 6% | 6% | 7% | 7% |
| Wishes voiced per day, average play | 0.48 | 0.47 | 0.52 | 0.52 |
| Letters that only tell, per day, average play | 0.39 | 0.41 | 0.47 | 0.47 |
| News lines per day (one evening letter), average play | 1.71 | 1.69 | 2.34 | 2.32 |
| Time with an everyday ailment, well run | 9% | 9% | 9% | 10% |
| Time with an everyday ailment, average | 14% | 15% | 14% | 14% |
| Time with an everyday ailment, neglectful | 31% | 32% | 31% | 31% |
| Ailment days per colonist per year, average play | 8.7 | 8.9 | 8.5 | 8.4 |

## Rimfolk's mood per colonist

Good memories are as given (small joys as their moments); "raw" is the same. Steady moods are trust, the colony's
ways, ailments and strain, and what good things growing ordinary takes off Rimfolk's good moods. P10 and P90: a hard
and a good moment for one colonist.

| Case | Good memories (raw) | Bad memories | Steady good | Steady bad | **Net** | P10 / P90 | Good moods on the mind | Mood | Standards |
|---|---|---|---|---|---|---|---|---|---|
| 6 colonists, well run | +8.7 (+8.7) | -2.3 | +6.2 | -6.3 | **+6.4** | +0.4 / +12.3 | 2.3 | 69% | 0.88 |
| 6 colonists, average | +6.1 (+6.1) | -3.3 | +1.6 | -1.7 | **+2.6** | -5.0 / +10.2 | 1.6 | 64% | 0.31 |
| 6 colonists, neglectful | +3.5 (+3.5) | -5.1 | +0.6 | -6.2 | **-7.2** | -15.7 / +1.0 | 1.0 | 39% | 0.00 |
| 12 colonists, well run | +7.2 (+7.2) | -1.8 | +5.1 | -4.9 | **+5.7** | +0.4 / +10.9 | 1.9 | 69% | 0.87 |
| 12 colonists, average | +5.0 (+5.0) | -2.9 | +1.3 | -1.5 | **+2.0** | -4.9 / +9.1 | 1.4 | 64% | 0.26 |
| 12 colonists, neglectful | +3.1 (+3.1) | -4.6 | +0.5 | -5.5 | **-6.4** | -14.4 / +1.5 | 0.9 | 40% | 0.00 |

Over the year (net, in five 10-day stretches plus one; good and bad in brackets), 6 colonists:

| Play | days 0-9 | days 10-19 | days 20-29 | days 30-39 | days 40-49 | days 50-59 |
|---|---|---|---|---|---|---|
| well run | +6.2 (+13.9 / -7.8) | +6.0 (+14.2 / -8.1) | +6.5 (+14.9 / -8.4) | +6.6 (+15.5 / -8.9) | +6.6 (+15.5 / -8.9) | +6.7 (+15.8 / -9.0) |
| average | +2.5 (+7.4 / -4.9) | +2.5 (+7.5 / -5.0) | +2.5 (+7.7 / -5.1) | +2.5 (+7.7 / -5.2) | +2.4 (+7.5 / -5.2) | +3.0 (+8.1 / -5.1) |
| neglectful | -6.3 (+4.0 / -10.3) | -6.8 (+4.1 / -10.8) | -7.2 (+4.2 / -11.4) | -7.6 (+3.8 / -11.4) | -7.6 (+4.2 / -11.8) | -8.0 (+4.1 / -12.1) |

Where it comes from (6 colonists): what each source adds to a colonist's mood on average.

| Source | well run | average | neglectful |
|---|---|---|---|
| good things grown ordinary | -5.82 | -0.57 | -0.00 |
| trust | +2.67 | +0.37 | -2.22 |
| strain | -0.00 | -0.28 | -2.35 |
| wishes | +2.19 | +0.95 | +0.26 |
| the good side | +1.95 | +0.04 | +0.00 |
| meetings | +1.86 | +1.36 | +0.34 |
| life and honours | +1.30 | +1.10 | +0.81 |
| pressures (bad) | -0.21 | -0.69 | -1.24 |
| ailments | -0.22 | -0.34 | -0.94 |
| the colony's ways | +0.91 | +0.23 | -0.38 |
| wishes (bad) | -0.31 | -0.56 | -0.85 |
| moments | +0.72 | +0.66 | +0.59 |
| setbacks (lasting) (bad) | -0.28 | -0.39 | -0.70 |
| life goals | +0.67 | +0.45 | +0.28 |
| dilemmas | +0.57 | +0.42 | +0.18 |
| pressures | +0.55 | +0.20 | +0.02 |
| setbacks (bad) | -0.23 | -0.32 | -0.54 |
| social events | +0.50 | +0.50 | +0.49 |
| frictions (bad) | -0.44 | -0.48 | -0.41 |
| dilemmas (bad) | -0.21 | -0.23 | -0.36 |
| stories | +0.31 | +0.33 | +0.32 |
| meetings (bad) | -0.14 | -0.20 | -0.33 |
| calendar | +0.25 | +0.22 | +0.24 |
| dreams (bad) | -0.13 | -0.13 | -0.24 |
| events | +0.21 | +0.19 | +0.17 |
| nuisances (bad) | -0.04 | -0.11 | -0.20 |
| social events (bad) | -0.17 | -0.14 | -0.13 |
| health | -0.05 | -0.07 | -0.11 |
| encounters | +0.09 | +0.09 | +0.07 |
| encounters (bad) | -0.06 | -0.05 | -0.04 |

What good things growing ordinary takes off (6 colonists):

| Play | Net with the rule | Net without it | Good memories with | without |
|---|---|---|---|---|
| well run | +6.4 | +12.4 | +8.7 | +9.0 |
| average | +2.6 | +3.0 | +6.1 | +6.1 |
| neglectful | -7.2 | -7.1 | +3.5 | +3.5 |

## The heart

Each colonist's strain, played hour by hour (`HeartSim.cs`): the life of each play style (fights, losses, bad nights,
hunger, low spirits, decrees; sleep, quiet hours, recreation, gatherings and talks), set so that it alone gives the
shares the report always assumed, and on top what small letdowns and joys bring (`SmallMomentMath`): what Rimfolk's
own weigh beyond their moment, and the pile of every small mood on a colonist's mind, the game's (a share of its moods,
`Rates.GameSmallBad`, `GameSmallGood`) and Rimfolk's. "As moods" is the same with letdowns and joys as moods of their
own and nothing to the heart (as before phase 3). Strain built and eased is per colonist over the 60-day year.

| Case | Strained / worn / near breaking | Day off a year | Built | of it: life / Rimfolk's letdowns / small moods (the game's) / (Rimfolk's) | The game's small moods' share | Eased by small joys (the game's / Rimfolk's) |
|---|---|---|---|---|---|---|
| 6 colonists, well run | 0% / 0% / 0% | 0.00 | 18 | 17 / 1 / 0 / 0 | 0% | 2 / 2 |
| 6 colonists, average | 6% / 1% / 0% | 0.03 | 207 | 176 / 19 / 7 / 4 | 4% | 15 / 15 |
| 6 colonists, neglectful | 29% / 17% / 8% | 0.72 | 450 | 365 / 39 / 27 / 18 | 6% | 7 / 20 |
| 12 colonists, well run | 0% / 0% / 0% | 0.00 | 18 | 17 / 1 / 0 / 0 | 0% | 2 / 2 |
| 12 colonists, average | 6% / 2% / 0% | 0.04 | 203 | 173 / 19 / 7 / 4 | 4% | 14 / 15 |
| 12 colonists, neglectful | 28% / 17% / 10% | 0.78 | 456 | 370 / 41 / 28 / 17 | 6% | 8 / 20 |
| 6 colonists, well run, as moods | 0% / 0% / 0% | 0.00 | 23 | 23 / 0 / 0 / 0 | 0% | 0 / 0 |
| 6 colonists, average, as moods | 6% / 1% / 0% | 0.03 | 160 | 160 / 0 / 0 / 0 | 0% | 0 / 0 |
| 6 colonists, neglectful, as moods | 23% / 10% / 4% | 0.39 | 366 | 366 / 0 / 0 / 0 | 0% | 0 / 0 |

The colony's mood: the whole of it, the game's moods included, and what good things growing ordinary takes off the
game's own good moods (it comes off every good mood by how ordinary each is: the game's comforts mostly).

| Case | Whole mood (P10 / P90) | Under the minor-break line | Taken off the game's good moods | Taken off Rimfolk's |
|---|---|---|---|---|
| 6 colonists, well run | 69% (64% / 74%) | 0% | -13.7 | -5.8 |
| 6 colonists, average | 64% (56% / 72%) | 0% | -1.7 | -0.6 |
| 6 colonists, neglectful | 39% (27% / 50%) | 34% | -0.0 | -0.0 |
| 12 colonists, well run | 69% (64% / 74%) | 0% | -12.8 | -4.5 |
| 12 colonists, average | 64% (56% / 71%) | 0% | -1.3 | -0.4 |
| 12 colonists, neglectful | 40% (28% / 51%) | 29% | -0.0 | -0.0 |
| 6 colonists, well run, without growing ordinary | 88% (78% / 100%) | 0% | +-0.0 | +0.0 |
| 6 colonists, average, without growing ordinary | 66% (56% / 77%) | 0% | +-0.0 | +0.0 |
| 6 colonists, neglectful, without growing ordinary | 39% (27% / 50%) | 33% | +-0.0 | +0.0 |
| 6 colonists, well run, letdowns and joys as moods | 68% (63% / 73%) | 0% | -13.5 | -6.3 |
| 6 colonists, average, letdowns and joys as moods | 63% (54% / 72%) | 0% | -1.1 | -0.4 |
| 6 colonists, neglectful, letdowns and joys as moods | 36% (25% / 47%) | 43% | -0.0 | -0.0 |

## The heart's good side

The other side of strain (`HeartMath`, "The good side"; `HeartSim.cs`): hardship spends it before strain builds; what a
gathering, a talk or a small joy eases beyond the strain there is fills it at its share, with friends close by and a
purpose (the heart's needs well met, drawn a day at a time per play style), deeds the colony speaks well of and a life
goal moving; it fades slowly. Steady from 20, in good heart from 45, in high spirits from 65 (+2, +3 and +5 mood); in high spirits, now and then, an inspiration or a kind word for someone close.
Filled and spent are per colonist over the 60-day year; "without" is the same seeds with its setting off.

| Case | Steady | In good heart | In high spirits | Strain at nothing (without) | Inspirations / kind words a colonist-year | Filled by | Spent by |
|---|---|---|---|---|---|---|---|
| 6 colonists, well run | 48% | 28% | 3% | 99% (70%) | 0.12 / 0.11 | purpose 57, belonging 44, good talks 42, small joys 24 | life 86, Rimfolk's letdowns 22, small moods 3 |
| 6 colonists, average | 2% | 0% | 0% | 66% (46%) | 0.00 / 0.00 | purpose 23, good talks 22, belonging 15, small joys 11 | life 51, Rimfolk's letdowns 16, small moods 10 |
| 6 colonists, neglectful | 0% | 0% | 0% | 8% (7%) | 0.00 / 0.00 | good talks 3, small joys 2, purpose 1, gatherings 1 | life 4, Rimfolk's letdowns 1, small moods 1 |
| 12 colonists, well run | 48% | 27% | 3% | 98% | 0.10 / 0.10 | purpose 57, belonging 44, good talks 42, small joys 25 | life 86, Rimfolk's letdowns 20, small moods 2 |
| 12 colonists, average | 2% | 0% | 0% | 65% | 0.00 / 0.00 | purpose 23, good talks 22, belonging 15, small joys 11 | life 51, Rimfolk's letdowns 16, small moods 9 |
| 12 colonists, neglectful | 0% | 0% | 0% | 8% | 0.00 / 0.00 | good talks 3, small joys 1, purpose 1, gatherings 0 | life 4, Rimfolk's letdowns 1, small moods 1 |
| 6 colonists, well run, lightTouch | 46% | 34% | 6% | 99% | 0.20 / 0.21 | purpose 57, belonging 44, good talks 42, small joys 26 | life 90, Rimfolk's letdowns 8, small moods 0 |
| 6 colonists, average, lightTouch | 5% | 0% | 0% | 71% | 0.00 / 0.00 | purpose 25, good talks 24, belonging 16, small joys 13 | life 61, Rimfolk's letdowns 12, small moods 5 |
| 6 colonists, neglectful, lightTouch | 0% | 0% | 0% | 9% | 0.00 / 0.00 | good talks 3, small joys 2, purpose 1, gatherings 1 | life 5, small moods 2, Rimfolk's letdowns 1 |
| 6 colonists, well run, storyRich | 47% | 28% | 3% | 99% | 0.10 / 0.11 | purpose 57, belonging 44, good talks 42, small joys 24 | life 85, Rimfolk's letdowns 22, small moods 4 |
| 6 colonists, average, storyRich | 2% | 0% | 0% | 66% | 0.00 / 0.00 | purpose 24, good talks 23, belonging 15, small joys 11 | life 51, Rimfolk's letdowns 16, small moods 10 |
| 6 colonists, neglectful, storyRich | 0% | 0% | 0% | 10% | 0.00 / 0.00 | good talks 3, small joys 2, purpose 1, gatherings 1 | life 5, Rimfolk's letdowns 1, small moods 1 |
| 6 colonists, well run, demanding | 48% | 23% | 2% | 98% | 0.06 / 0.06 | purpose 57, belonging 45, good talks 42, small joys 21 | life 83, Rimfolk's letdowns 26, small moods 4 |
| 6 colonists, average, demanding | 1% | 0% | 0% | 64% | 0.00 / 0.00 | purpose 23, good talks 22, belonging 15, small joys 9 | life 47, Rimfolk's letdowns 17, small moods 9 |
| 6 colonists, neglectful, demanding | 0% | 0% | 0% | 8% | 0.00 / 0.00 | good talks 3, small joys 1, purpose 1, gatherings 1 | life 4, Rimfolk's letdowns 1, small moods 1 |

What it changes (6 colonists, the same seeds with and without it):

| Play | Rimfolk's mood | Its own mood | Good moods at a high point (P99) | +12 or more | Strained / worn / near breaking | Under the minor-break line | Day off a year | Whole mood |
|---|---|---|---|---|---|---|---|---|
| well run | +5.6 to +6.4 | +1.9 | +17.5 to +17.9 | 16% to 21% | 1%/0%/0% to 0%/0%/0% | 0% to 0% | 0.00 to 0.00 | 69% to 69% |
| average | +2.5 to +2.6 | +0.0 | +20.0 to +20.0 | 17% to 16% | 8%/2%/0% to 6%/1%/0% | 0% to 0% | 0.05 to 0.03 | 64% to 64% |
| neglectful | -7.0 to -7.2 | +0.0 | +16.0 to +15.3 | 5% to 5% | 30%/17%/8% to 29%/17%/8% | 34% to 34% | 0.73 to 0.72 | 39% to 39% |

## Trust

Average trust in the council (-100 to 100; trusting from 25, devoted from 60, doubtful from -15, bitter from -45).

| Case | Start of year | End of year | Year's average | Trusting or devoted | Doubtful or bitter | By stretch |
|---|---|---|---|---|---|---|
| 6 colonists, well run | +35 | +70 | +56 | 88% | 0% | +39, +47, +54, +61, +66, +69 |
| 6 colonists, average | +8 | +16 | +12 | 26% | 8% | +9, +10, +12, +13, +13, +15 |
| 6 colonists, neglectful | -16 | -41 | -30 | 0% | 81% | -18, -24, -29, -33, -37, -40 |
| 12 colonists, well run | +20 | +46 | +35 | 67% | 0% | +23, +29, +34, +38, +42, +45 |
| 12 colonists, average | +5 | +11 | +9 | 15% | 5% | +6, +7, +8, +10, +11, +11 |
| 12 colonists, neglectful | -10 | -26 | -19 | 0% | 59% | -12, -15, -18, -21, -24, -26 |

## Letters and messages per day

Questions are choice letters (a dilemma, an event with answers, a calendar day, a meeting's vote, a mission
offered, a world event's question). Wishes are letters that ask without buttons. Letters only tell. Messages show
on screen as they happen; the evening's colony news gathers the rest into one letter a day. Flavour news (the
smaller encounters, moments and dreams) isn't shown at the default message level and isn't counted.

**6 colonists, average play**: 0.50 questions, 0.47 wishes voiced, 0.41 letters, 0.63 messages and 1.69 news lines a day.

| System | Questions | Wishes | Letters | Messages | News lines |
|---|---|---|---|---|---|
| wishes | 0.00 | 0.38 | 0.04 | 0.00 | 0.43 |
| event wishes | 0.00 | 0.09 | 0.00 | 0.00 | 0.00 |
| dilemmas | 0.10 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (after a raid) | 0.04 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (strangers) | 0.07 | 0.00 | 0.00 | 0.00 | 0.00 |
| events | 0.07 | 0.00 | 0.04 | 0.00 | 0.00 |
| encounters | 0.00 | 0.00 | 0.00 | 0.00 | 0.27 |
| social events | 0.00 | 0.00 | 0.00 | 0.00 | 0.31 |
| ailments | 0.00 | 0.00 | 0.02 | 0.00 | 0.15 |
| health | 0.00 | 0.00 | 0.01 | 0.00 | 0.11 |
| calendar | 0.10 | 0.00 | 0.03 | 0.00 | 0.00 |
| stories | 0.00 | 0.00 | 0.04 | 0.00 | 0.12 |
| camps | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| world events | 0.05 | 0.00 | 0.02 | 0.00 | 0.00 |
| missions | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| play-outs | 0.00 | 0.00 | 0.00 | 0.33 | 0.00 |
| meetings | 0.06 | 0.00 | 0.03 | 0.00 | 0.03 |
| decrees | 0.00 | 0.00 | 0.00 | 0.13 | 0.00 |
| dreams | 0.00 | 0.00 | 0.00 | 0.00 | 0.01 |
| people | 0.00 | 0.00 | 0.12 | 0.00 | 0.25 |
| colony | 0.00 | 0.00 | 0.04 | 0.17 | 0.00 |
| world | 0.00 | 0.00 | 0.01 | 0.00 | 0.00 |

**12 colonists, average play**: 0.53 questions, 0.52 wishes voiced, 0.47 letters, 0.65 messages and 2.32 news lines a day.

| System | Questions | Wishes | Letters | Messages | News lines |
|---|---|---|---|---|---|
| wishes | 0.00 | 0.32 | 0.04 | 0.00 | 0.48 |
| event wishes | 0.00 | 0.19 | 0.00 | 0.00 | 0.00 |
| dilemmas | 0.10 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (after a raid) | 0.04 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (strangers) | 0.08 | 0.00 | 0.00 | 0.00 | 0.00 |
| events | 0.06 | 0.00 | 0.04 | 0.00 | 0.00 |
| encounters | 0.00 | 0.00 | 0.00 | 0.00 | 0.27 |
| social events | 0.00 | 0.00 | 0.00 | 0.00 | 0.50 |
| ailments | 0.00 | 0.00 | 0.02 | 0.00 | 0.29 |
| health | 0.00 | 0.00 | 0.01 | 0.00 | 0.18 |
| calendar | 0.13 | 0.00 | 0.05 | 0.00 | 0.00 |
| stories | 0.00 | 0.00 | 0.07 | 0.00 | 0.19 |
| camps | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| world events | 0.04 | 0.00 | 0.02 | 0.00 | 0.00 |
| missions | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| play-outs | 0.00 | 0.00 | 0.00 | 0.35 | 0.00 |
| meetings | 0.06 | 0.00 | 0.03 | 0.00 | 0.03 |
| decrees | 0.00 | 0.00 | 0.00 | 0.13 | 0.00 |
| dreams | 0.00 | 0.00 | 0.00 | 0.00 | 0.02 |
| people | 0.00 | 0.00 | 0.15 | 0.00 | 0.36 |
| colony | 0.00 | 0.00 | 0.04 | 0.17 | 0.00 |
| world | 0.00 | 0.00 | 0.01 | 0.00 | 0.00 |

By play style (6 colonists):

| Play | Questions | Wishes | Letters | Messages | News lines | Questions within 12 h of another | Days with two or more questions |
|---|---|---|---|---|---|---|---|
| well run | 0.50 | 0.64 | 0.48 | 0.67 | 1.73 | 8% | 7% |
| average | 0.50 | 0.47 | 0.41 | 0.63 | 1.69 | 8% | 6% |
| neglectful | 0.47 | 0.53 | 0.39 | 0.56 | 1.97 | 8% | 5% |

## Ailments

Everyday ailments and what they leave behind (a lingering cough), per colonist over the year. "Not quiet" leaves
out the smallest (a stiff neck, hiccups, a smoke cough), which only a player who wants every message hears about.

| Case | Ailment days per colonist | Time with one | Time with one, not quiet | Caught, per colony | Circumstance, per colony | Serious, per colony |
|---|---|---|---|---|---|---|
| 6 colonists, well run | 5.7 | 9% | 8% | 1.2 | 30.8 | 0.2 |
| 6 colonists, average | 8.9 | 15% | 13% | 1.3 | 27.7 | 0.4 |
| 6 colonists, neglectful | 19.1 | 32% | 28% | 1.3 | 40.4 | 1.7 |
| 12 colonists, well run | 5.9 | 10% | 8% | 1.3 | 61.2 | 0.2 |
| 12 colonists, average | 8.4 | 14% | 12% | 1.2 | 56.7 | 0.8 |
| 12 colonists, neglectful | 18.8 | 31% | 28% | 1.2 | 80.2 | 3.3 |

What fills the time (days per colonist per year, 6 colonists):

| Ailment | well run | average | neglectful |
|---|---|---|---|
| Lice | 0.0 | 0.3 | 4.1 |
| Convalescent | 0.3 | 0.7 | 2.7 |
| RimrotCough | 0.0 | 0.3 | 1.7 |
| Insomnia | 0.1 | 0.3 | 1.5 |
| HeadCold | 0.5 | 0.6 | 0.7 |
| ChestCold | 0.4 | 0.6 | 0.7 |
| StrainedBack | 0.4 | 0.5 | 0.5 |
| Worms | 0.0 | 0.2 | 0.9 |
| SmokeCough | 0.0 | 0.2 | 0.9 |
| ColdSore | 0.1 | 0.2 | 0.6 |
| ChappedHands | 0.3 | 0.3 | 0.3 |
| Toothache | 0.1 | 0.3 | 0.4 |
| BadNights | 0.2 | 0.3 | 0.3 |
| Chilblains | 0.1 | 0.2 | 0.3 |
| Dreamfever | 0.2 | 0.3 | 0.2 |
| SoreHands | 0.2 | 0.2 | 0.3 |

## The presets

The style presets of the settings window, applied as the window applies them (6 colonists).

| Preset | Play | Net | Good memories | Bad + steady bad | Questions per day | Wishes per day | Time ill |
|---|---|---|---|---|---|---|---|
| default | well run | +6.4 | +8.7 | -8.5 | 0.50 | 0.64 | 9% |
| default | average | +2.6 | +6.1 | -5.1 | 0.50 | 0.47 | 15% |
| default | neglectful | -7.2 | +3.5 | -11.3 | 0.47 | 0.53 | 32% |
| lightTouch | well run | +7.2 | +6.7 | -5.1 | 0.45 | 0.30 | 5% |
| lightTouch | average | +4.8 | +5.1 | -1.8 | 0.45 | 0.24 | 7% |
| lightTouch | neglectful | -1.6 | +3.0 | -5.1 | 0.44 | 0.30 | 15% |
| storyRich | well run | +6.9 | +9.8 | -9.2 | 0.58 | 0.64 | 9% |
| storyRich | average | +3.1 | +6.8 | -5.4 | 0.57 | 0.47 | 14% |
| storyRich | neglectful | -6.5 | +4.3 | -11.4 | 0.54 | 0.52 | 32% |
| demanding | well run | +2.3 | +6.3 | -8.7 | 0.49 | 0.69 | 12% |
| demanding | average | -2.9 | +4.3 | -8.4 | 0.49 | 0.52 | 17% |
| demanding | neglectful | -14.9 | +2.5 | -17.9 | 0.47 | 0.64 | 37% |

## How often things happen (per colony per year)

| What | 6, well run | 6, average | 6, neglectful | 12, well run | 12, average | 12, neglectful |
|---|---|---|---|---|---|---|
| after-effects | 0.5 | 0.8 | 2.0 | 0.8 | 1.2 | 3.7 |
| ailments | 30.8 | 27.7 | 40.4 | 61.2 | 56.7 | 80.2 |
| ambitions reached | 2.8 | 2.0 | 0.5 | 3.0 | 2.0 | 0.6 |
| calendar days | 8.0 | 8.0 | 8.0 | 11.0 | 11.0 | 11.0 |
| camps | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 |
| catching ailments | 1.2 | 1.3 | 1.3 | 1.3 | 1.2 | 1.2 |
| convalescence | 2.6 | 3.6 | 5.7 | 5.1 | 8.1 | 12.2 |
| days of one faith for the colony | 2.3 | 3.0 | 2.6 | 2.2 | 2.9 | 2.8 |
| decree requests | 8.2 | 8.0 | 7.7 | 7.5 | 7.8 | 7.7 |
| dilemmas | 13.6 | 13.1 | 12.1 | 13.6 | 13.3 | 11.7 |
| dreams | 109.1 | 108.4 | 108.2 | 213.4 | 215.5 | 215.6 |
| encounters | 54.9 | 52.0 | 47.8 | 54.0 | 51.6 | 47.2 |
| evenings of faith | 1.4 | 1.6 | 1.7 | 1.4 | 1.8 | 1.4 |
| events | 6.0 | 6.1 | 6.1 | 5.8 | 6.0 | 5.7 |
| friction moments | 203.9 | 264.7 | 361.2 | 406.2 | 535.2 | 721.9 |
| meetings | 3.7 | 3.8 | 3.8 | 3.8 | 3.7 | 3.8 |
| missions | 0.2 | 0.2 | 0.1 | 0.2 | 0.2 | 0.2 |
| moments | 710.6 | 663.2 | 590.0 | 1419.1 | 1320.7 | 1183.5 |
| nuisances | 26.2 | 82.4 | 140.4 | 52.2 | 164.8 | 282.1 |
| personal touches | 9.9 | 10.1 | 10.1 | 9.9 | 10.0 | 10.1 |
| play-outs | 5.6 | 4.7 | 2.6 | 5.3 | 4.3 | 2.7 |
| quiet troubles | 3.6 | 0.7 | 0.0 | 6.4 | 1.3 | 0.0 |
| serious illnesses | 0.2 | 0.4 | 1.7 | 0.2 | 0.8 | 3.3 |
| small joys | 257.4 | 125.6 | 54.5 | 513.2 | 250.5 | 111.3 |
| small letdowns | 644.1 | 1243.8 | 2186.8 | 1209.8 | 2428.0 | 4360.2 |
| social events | 71.3 | 71.2 | 71.2 | 117.5 | 117.6 | 117.7 |
| stories | 3.3 | 3.3 | 3.3 | 5.1 | 5.1 | 5.2 |
| wishes granted | 31.5 | 14.5 | 4.0 | 33.7 | 16.2 | 4.9 |
| wishes lapsed | 6.4 | 13.7 | 27.6 | 6.7 | 14.7 | 34.9 |
| wishes made | 38.5 | 28.3 | 31.7 | 40.1 | 31.1 | 40.0 |
| world events | 1.0 | 1.1 | 1.0 | 1.1 | 1.0 | 1.1 |

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
