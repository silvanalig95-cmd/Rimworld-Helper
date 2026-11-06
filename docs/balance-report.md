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
| Rimfolk's mood, well run | +3 to +6 | +5.6 | +4.6 | +2.3 |
| Rimfolk's mood, average | 0 to +3 | +2.5 | +1.8 | +0.3 |
| Rimfolk's mood, neglectful | -4 to -10 | -7.2 | -6.5 | -9.1 |
| Rimfolk's good moods at a high point (P99), well run | well short of the top | +17.3 | +15.9 | +15.4 |
| Time with +12 or more from Rimfolk's good moods, well run | rare | 16% | 8% | 10% |
| Questions (decision letters) per day, average | 1 every 2-3 days (0.33-0.5) | 0.49 | 0.53 | 0.50 |
| Questions and wishes voiced per day, average | (for reference) | 0.97 | 1.05 | 0.95 |
| Questions within 12 hours of another, average | few | 8% | 10% | 8% |
| Time with an everyday ailment, average | 10-15% | 15% | 15% | 14% |
| Time with an everyday ailment, well run | under 10-15% | 9% | 10% | 9% |
| The colony's whole mood, well run (for reference) | as before | 69% | 69% | 69% |
| The colony's whole mood, average (for reference) | as before | 64% | 64% | 62% |
| The colony's whole mood, neglectful (for reference) | as before | 39% | 40% | 37% |
| Time under the minor-break line, neglectful (letdowns as moods: 42%) | no more than before | 34% | 30% | - |
| The heart's day off a colonist-year, neglectful (letdowns as moods: 0.40) | under one | 0.74 | 0.80 | - |

## Before and after phase 3 (one heart)

The simulator on the code and defs as they were before phase 3 of the review (four rules softening good moods, small
letdowns as moods of their own, strain as a fixed share per play style), and as they are now (default settings).

| | 6 before | 6 after | 12 before | 12 after |
|---|---|---|---|---|
| Rimfolk's mood, well run | +2.3 | +5.6 | +2.1 | +4.6 |
|   good memories (raw), well run | +4.5 (+5.0) | +8.8 (+8.8) | +3.9 (+4.2) | +7.2 (+7.2) |
|   bad memories and steady bad, well run | -5.7 | -7.4 | -4.7 | -5.8 |
| Rimfolk's mood, average | +0.3 | +2.5 | +0.2 | +1.8 |
|   good memories (raw), average | +5.4 (+5.8) | +6.0 (+6.0) | +4.8 (+5.2) | +5.0 (+5.0) |
|   bad memories and steady bad, average | -6.9 | -5.1 | -6.2 | -4.5 |
| Rimfolk's mood, neglectful | -9.1 | -7.2 | -7.8 | -6.5 |
|   good memories (raw), neglectful | +3.7 (+3.9) | +3.5 (+3.5) | +3.5 (+3.7) | +3.1 (+3.1) |
|   bad memories and steady bad, neglectful | -13.5 | -11.2 | -11.9 | -10.1 |
| Rimfolk's good moods at a high point (P99), well run | +15.4 | +17.3 | +14.4 | +15.9 |
| Time with +12 or more from Rimfolk's good moods, well run | 10% | 16% | 5% | 8% |
| Trust, year's average, average play | +11 | +13 | +10 | +9 |
| Questions per day, average play | 0.50 | 0.49 | 0.53 | 0.53 |
| Questions within 12 hours of another, average play | 8% | 8% | 9% | 10% |
| Days with two or more questions, average play | 6% | 6% | 7% | 7% |
| Wishes voiced per day, average play | 0.45 | 0.48 | 0.50 | 0.52 |
| Letters that only tell, per day, average play | 0.39 | 0.39 | 0.47 | 0.48 |
| News lines per day (one evening letter), average play | 1.70 | 1.71 | 2.31 | 2.39 |
| Time with an everyday ailment, well run | 9% | 9% | 9% | 10% |
| Time with an everyday ailment, average | 14% | 15% | 14% | 15% |
| Time with an everyday ailment, neglectful | 31% | 31% | 30% | 31% |
| Ailment days per colonist per year, average play | 8.5 | 9.0 | 8.3 | 9.0 |

## Rimfolk's mood per colonist

Good memories are as given (small joys as their moments); "raw" is the same. Steady moods are trust, the colony's
ways, ailments and strain, and what good things growing ordinary takes off Rimfolk's good moods. P10 and P90: a hard
and a good moment for one colonist.

| Case | Good memories (raw) | Bad memories | Steady good | Steady bad | **Net** | P10 / P90 | Good moods on the mind | Mood | Standards |
|---|---|---|---|---|---|---|---|---|---|
| 6 colonists, well run | +8.8 (+8.8) | -2.3 | +4.3 | -5.2 | **+5.6** | -0.3 / +11.4 | 2.3 | 69% | 0.87 |
| 6 colonists, average | +6.0 (+6.0) | -3.4 | +1.5 | -1.8 | **+2.5** | -5.1 / +10.3 | 1.6 | 64% | 0.31 |
| 6 colonists, neglectful | +3.5 (+3.5) | -5.0 | +0.6 | -6.2 | **-7.2** | -15.7 / +1.2 | 1.0 | 39% | 0.00 |
| 12 colonists, well run | +7.2 (+7.2) | -1.8 | +3.2 | -4.0 | **+4.6** | -0.8 / +10.1 | 1.9 | 69% | 0.84 |
| 12 colonists, average | +5.0 (+5.0) | -2.9 | +1.3 | -1.6 | **+1.8** | -5.0 / +9.0 | 1.4 | 64% | 0.25 |
| 12 colonists, neglectful | +3.1 (+3.1) | -4.6 | +0.5 | -5.5 | **-6.5** | -14.4 / +1.4 | 0.9 | 40% | 0.00 |

Over the year (net, in five 10-day stretches plus one; good and bad in brackets), 6 colonists:

| Play | days 0-9 | days 10-19 | days 20-29 | days 30-39 | days 40-49 | days 50-59 |
|---|---|---|---|---|---|---|
| well run | +5.4 (+12.1 / -6.8) | +5.6 (+12.8 / -7.2) | +5.5 (+12.9 / -7.4) | +5.6 (+13.2 / -7.6) | +5.7 (+13.4 / -7.7) | +5.9 (+13.7 / -7.9) |
| average | +2.4 (+7.3 / -4.9) | +2.5 (+7.6 / -5.1) | +2.4 (+7.5 / -5.0) | +2.5 (+7.7 / -5.2) | +2.3 (+7.5 / -5.2) | +2.7 (+7.8 / -5.2) |
| neglectful | -6.4 (+4.1 / -10.5) | -6.3 (+4.3 / -10.6) | -6.9 (+4.0 / -11.0) | -7.5 (+3.8 / -11.4) | -8.0 (+3.8 / -11.8) | -8.0 (+4.1 / -12.1) |

Where it comes from (6 colonists): what each source adds to a colonist's mood on average.

| Source | well run | average | neglectful |
|---|---|---|---|
| good things grown ordinary | -4.78 | -0.53 | -0.00 |
| trust | +2.67 | +0.40 | -2.13 |
| strain | -0.03 | -0.37 | -2.38 |
| wishes | +2.13 | +0.97 | +0.25 |
| world events (bad) | -0.47 | -1.07 | -1.94 |
| meetings | +1.93 | +1.34 | +0.36 |
| life and honours | +1.31 | +1.06 | +0.82 |
| ailments | -0.16 | -0.32 | -0.96 |
| the colony's ways | +0.91 | +0.23 | -0.38 |
| wishes (bad) | -0.31 | -0.53 | -0.82 |
| moments | +0.71 | +0.65 | +0.59 |
| life goals | +0.62 | +0.46 | +0.28 |
| dilemmas | +0.59 | +0.39 | +0.17 |
| world events | +0.57 | +0.24 | +0.05 |
| setbacks (bad) | -0.23 | -0.32 | -0.54 |
| social events | +0.51 | +0.50 | +0.51 |
| frictions (bad) | -0.45 | -0.49 | -0.41 |
| stories | +0.32 | +0.35 | +0.31 |
| dilemmas (bad) | -0.19 | -0.26 | -0.32 |
| meetings (bad) | -0.13 | -0.19 | -0.32 |
| dreams (bad) | -0.14 | -0.15 | -0.26 |
| calendar | +0.25 | +0.22 | +0.24 |
| events | +0.21 | +0.18 | +0.11 |
| nuisances (bad) | -0.04 | -0.12 | -0.20 |
| social events (bad) | -0.18 | -0.15 | -0.13 |
| health | -0.05 | -0.08 | -0.11 |
| encounters | +0.10 | +0.10 | +0.07 |
| encounters (bad) | -0.06 | -0.05 | -0.05 |

What good things growing ordinary takes off (6 colonists):

| Play | Net with the rule | Net without it | Good memories with | without |
|---|---|---|---|---|
| well run | +5.6 | +10.6 | +8.8 | +9.1 |
| average | +2.5 | +2.8 | +6.0 | +6.0 |
| neglectful | -7.2 | -7.2 | +3.5 | +3.5 |

## The heart

Each colonist's strain, played hour by hour (`HeartSim.cs`): the life of each play style (fights, losses, bad nights,
hunger, low spirits, decrees; sleep, quiet hours, recreation, gatherings and talks), set so that it alone gives the
shares the report always assumed, and on top what small letdowns and joys bring (`SmallMomentMath`): what Rimfolk's
own weigh beyond their moment, and the pile of every small mood on a colonist's mind, the game's (a share of its moods,
`Rates.GameSmallBad`, `GameSmallGood`) and Rimfolk's. "As moods" is the same with letdowns and joys as moods of their
own and nothing to the heart (as before phase 3). Strain built and eased is per colonist over the 60-day year.

| Case | Strained / worn / near breaking | Day off a year | Built | of it: life / Rimfolk's letdowns / small moods (the game's) / (Rimfolk's) | The game's small moods' share | Eased by small joys (the game's / Rimfolk's) |
|---|---|---|---|---|---|---|
| 6 colonists, well run | 1% / 0% / 0% | 0.00 | 239 | 188 / 44 / 3 / 3 | 1% | 26 / 35 |
| 6 colonists, average | 8% / 2% / 0% | 0.05 | 359 | 277 / 51 / 20 / 12 | 6% | 33 / 30 |
| 6 colonists, neglectful | 30% / 17% / 8% | 0.74 | 464 | 374 / 42 / 29 / 19 | 6% | 7 / 20 |
| 12 colonists, well run | 1% / 0% / 0% | 0.00 | 236 | 189 / 42 / 2 / 2 | 1% | 24 / 32 |
| 12 colonists, average | 9% / 2% / 1% | 0.06 | 356 | 275 / 51 / 20 / 11 | 5% | 32 / 29 |
| 12 colonists, neglectful | 29% / 17% / 10% | 0.80 | 468 | 378 / 43 / 30 / 18 | 6% | 8 / 21 |
| 6 colonists, well run, as moods | 2% / 0% / 0% | 0.00 | 189 | 189 / 0 / 0 / 0 | 0% | 0 / 0 |
| 6 colonists, average, as moods | 9% / 2% / 1% | 0.05 | 278 | 278 / 0 / 0 / 0 | 0% | 0 / 0 |
| 6 colonists, neglectful, as moods | 23% / 11% / 4% | 0.40 | 381 | 381 / 0 / 0 / 0 | 0% | 0 / 0 |

The colony's mood: the whole of it, the game's moods included, and what good things growing ordinary takes off the
game's own good moods (it comes off every good mood by how ordinary each is: the game's comforts mostly).

| Case | Whole mood (P10 / P90) | Under the minor-break line | Taken off the game's good moods | Taken off Rimfolk's |
|---|---|---|---|---|
| 6 colonists, well run | 69% (64% / 74%) | 0% | -13.1 | -4.8 |
| 6 colonists, average | 64% (56% / 72%) | 0% | -1.6 | -0.5 |
| 6 colonists, neglectful | 39% (27% / 50%) | 34% | -0.0 | -0.0 |
| 12 colonists, well run | 69% (64% / 73%) | 0% | -12.0 | -3.5 |
| 12 colonists, average | 64% (56% / 71%) | 0% | -1.2 | -0.4 |
| 12 colonists, neglectful | 40% (28% / 51%) | 30% | -0.0 | -0.0 |
| 6 colonists, well run, without growing ordinary | 87% (76% / 98%) | 0% | +-0.0 | +0.0 |
| 6 colonists, average, without growing ordinary | 66% (56% / 77%) | 0% | +-0.0 | +0.0 |
| 6 colonists, neglectful, without growing ordinary | 39% (27% / 50%) | 34% | +-0.0 | +0.0 |
| 6 colonists, well run, letdowns and joys as moods | 68% (62% / 73%) | 0% | -12.9 | -5.3 |
| 6 colonists, average, letdowns and joys as moods | 63% (54% / 72%) | 0% | -1.1 | -0.4 |
| 6 colonists, neglectful, letdowns and joys as moods | 37% (26% / 47%) | 42% | -0.0 | -0.0 |

## Trust

Average trust in the council (-100 to 100; trusting from 25, devoted from 60, doubtful from -15, bitter from -45).

| Case | Start of year | End of year | Year's average | Trusting or devoted | Doubtful or bitter | By stretch |
|---|---|---|---|---|---|---|
| 6 colonists, well run | +35 | +69 | +56 | 89% | 0% | +39, +48, +54, +60, +65, +68 |
| 6 colonists, average | +8 | +17 | +13 | 26% | 7% | +9, +10, +12, +14, +15, +16 |
| 6 colonists, neglectful | -16 | -40 | -29 | 0% | 80% | -18, -23, -27, -32, -36, -39 |
| 12 colonists, well run | +21 | +46 | +35 | 66% | 0% | +23, +29, +34, +38, +42, +45 |
| 12 colonists, average | +5 | +11 | +9 | 16% | 5% | +6, +8, +8, +10, +10, +11 |
| 12 colonists, neglectful | -10 | -26 | -19 | 0% | 58% | -12, -15, -18, -21, -24, -26 |

## Letters and messages per day

Questions are choice letters (a dilemma, an event with answers, a calendar day, a meeting's vote, a mission
offered, a world event's question). Wishes are letters that ask without buttons. Letters only tell. Messages show
on screen as they happen; the evening's colony news gathers the rest into one letter a day. Flavour news (the
smaller encounters, moments and dreams) isn't shown at the default message level and isn't counted.

**6 colonists, average play**: 0.49 questions, 0.48 wishes voiced, 0.39 letters, 0.61 messages and 1.71 news lines a day.

| System | Questions | Wishes | Letters | Messages | News lines |
|---|---|---|---|---|---|
| wishes | 0.00 | 0.38 | 0.04 | 0.00 | 0.44 |
| event wishes | 0.00 | 0.10 | 0.00 | 0.00 | 0.00 |
| dilemmas | 0.10 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (after a raid) | 0.04 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (strangers) | 0.08 | 0.00 | 0.00 | 0.00 | 0.00 |
| events | 0.06 | 0.00 | 0.04 | 0.00 | 0.00 |
| encounters | 0.00 | 0.00 | 0.00 | 0.00 | 0.28 |
| social events | 0.00 | 0.00 | 0.00 | 0.00 | 0.30 |
| ailments | 0.00 | 0.00 | 0.02 | 0.00 | 0.15 |
| health | 0.00 | 0.00 | 0.01 | 0.00 | 0.12 |
| calendar | 0.10 | 0.00 | 0.03 | 0.00 | 0.00 |
| stories | 0.00 | 0.00 | 0.05 | 0.00 | 0.12 |
| camps | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| world events | 0.04 | 0.00 | 0.02 | 0.00 | 0.00 |
| missions | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| play-outs | 0.00 | 0.00 | 0.00 | 0.32 | 0.00 |
| meetings | 0.06 | 0.00 | 0.03 | 0.00 | 0.03 |
| decrees | 0.00 | 0.00 | 0.00 | 0.13 | 0.00 |
| dreams | 0.00 | 0.00 | 0.00 | 0.00 | 0.01 |
| people | 0.00 | 0.00 | 0.11 | 0.00 | 0.25 |
| colony | 0.00 | 0.00 | 0.04 | 0.17 | 0.00 |
| world | 0.00 | 0.00 | 0.01 | 0.00 | 0.00 |

**12 colonists, average play**: 0.53 questions, 0.52 wishes voiced, 0.48 letters, 0.65 messages and 2.39 news lines a day.

| System | Questions | Wishes | Letters | Messages | News lines |
|---|---|---|---|---|---|
| wishes | 0.00 | 0.33 | 0.04 | 0.00 | 0.48 |
| event wishes | 0.00 | 0.19 | 0.00 | 0.00 | 0.00 |
| dilemmas | 0.10 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (after a raid) | 0.04 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (strangers) | 0.07 | 0.00 | 0.00 | 0.00 | 0.00 |
| events | 0.07 | 0.00 | 0.04 | 0.00 | 0.00 |
| encounters | 0.00 | 0.00 | 0.00 | 0.00 | 0.29 |
| social events | 0.00 | 0.00 | 0.00 | 0.00 | 0.52 |
| ailments | 0.00 | 0.00 | 0.02 | 0.00 | 0.30 |
| health | 0.00 | 0.00 | 0.01 | 0.00 | 0.19 |
| calendar | 0.13 | 0.00 | 0.05 | 0.00 | 0.00 |
| stories | 0.00 | 0.00 | 0.07 | 0.00 | 0.19 |
| camps | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| world events | 0.04 | 0.00 | 0.02 | 0.00 | 0.00 |
| missions | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| play-outs | 0.00 | 0.00 | 0.00 | 0.35 | 0.00 |
| meetings | 0.06 | 0.00 | 0.03 | 0.00 | 0.03 |
| decrees | 0.00 | 0.00 | 0.00 | 0.14 | 0.00 |
| dreams | 0.00 | 0.00 | 0.00 | 0.00 | 0.03 |
| people | 0.00 | 0.00 | 0.15 | 0.00 | 0.36 |
| colony | 0.00 | 0.00 | 0.04 | 0.17 | 0.00 |
| world | 0.00 | 0.00 | 0.01 | 0.00 | 0.00 |

By play style (6 colonists):

| Play | Questions | Wishes | Letters | Messages | News lines | Questions within 12 h of another | Days with two or more questions |
|---|---|---|---|---|---|---|---|
| well run | 0.50 | 0.63 | 0.47 | 0.66 | 1.73 | 8% | 7% |
| average | 0.49 | 0.48 | 0.39 | 0.61 | 1.71 | 8% | 6% |
| neglectful | 0.47 | 0.53 | 0.39 | 0.56 | 1.97 | 9% | 5% |

## Ailments

Everyday ailments and what they leave behind (a lingering cough), per colonist over the year. "Not quiet" leaves
out the smallest (a stiff neck, hiccups, a smoke cough), which only a player who wants every message hears about.

| Case | Ailment days per colonist | Time with one | Time with one, not quiet | Caught, per colony | Circumstance, per colony | Serious, per colony |
|---|---|---|---|---|---|---|
| 6 colonists, well run | 5.7 | 9% | 8% | 1.2 | 31.5 | 0.2 |
| 6 colonists, average | 9.0 | 15% | 13% | 1.2 | 28.4 | 0.4 |
| 6 colonists, neglectful | 18.6 | 31% | 27% | 1.2 | 40.8 | 2.0 |
| 12 colonists, well run | 6.0 | 10% | 8% | 1.4 | 62.2 | 0.3 |
| 12 colonists, average | 9.0 | 15% | 13% | 1.3 | 57.0 | 0.9 |
| 12 colonists, neglectful | 18.6 | 31% | 27% | 1.1 | 80.3 | 3.4 |

What fills the time (days per colonist per year, 6 colonists):

| Ailment | well run | average | neglectful |
|---|---|---|---|
| Convalescent | 0.2 | 0.6 | 3.0 |
| Lice | 0.0 | 0.3 | 3.2 |
| RimrotCough | 0.0 | 0.3 | 1.8 |
| Insomnia | 0.1 | 0.5 | 1.5 |
| HeadCold | 0.5 | 0.5 | 0.6 |
| ChestCold | 0.4 | 0.6 | 0.4 |
| StrainedBack | 0.2 | 0.4 | 0.6 |
| Worms | 0.0 | 0.2 | 1.0 |
| ColdSore | 0.1 | 0.3 | 0.7 |
| SmokeCough | 0.0 | 0.2 | 0.9 |
| Toothache | 0.1 | 0.3 | 0.5 |
| Chilblains | 0.2 | 0.3 | 0.3 |
| ChappedHands | 0.2 | 0.3 | 0.3 |
| Typhus | 0.0 | 0.1 | 0.7 |
| SoreElbow | 0.2 | 0.3 | 0.2 |
| BadNights | 0.2 | 0.2 | 0.2 |

## The presets

The style presets of the settings window, applied as the window applies them (6 colonists).

| Preset | Play | Net | Good memories | Bad + steady bad | Questions per day | Wishes per day | Time ill |
|---|---|---|---|---|---|---|---|
| default | well run | +5.6 | +8.8 | -7.4 | 0.50 | 0.63 | 9% |
| default | average | +2.5 | +6.0 | -5.1 | 0.49 | 0.48 | 15% |
| default | neglectful | -7.2 | +3.5 | -11.2 | 0.47 | 0.53 | 31% |
| lightTouch | well run | +6.2 | +6.9 | -4.1 | 0.44 | 0.33 | 5% |
| lightTouch | average | +4.7 | +5.1 | -1.9 | 0.44 | 0.27 | 7% |
| lightTouch | neglectful | -1.7 | +3.0 | -5.2 | 0.43 | 0.36 | 16% |
| storyRich | well run | +5.9 | +9.6 | -8.0 | 0.58 | 0.64 | 10% |
| storyRich | average | +3.0 | +6.8 | -5.5 | 0.57 | 0.48 | 14% |
| storyRich | neglectful | -6.6 | +4.3 | -11.4 | 0.53 | 0.53 | 31% |
| demanding | well run | +1.7 | +6.3 | -7.8 | 0.50 | 0.68 | 9% |
| demanding | average | -3.0 | +4.3 | -8.5 | 0.49 | 0.52 | 14% |
| demanding | neglectful | -14.7 | +2.5 | -17.6 | 0.47 | 0.65 | 30% |

## How often things happen (per colony per year)

| What | 6, well run | 6, average | 6, neglectful | 12, well run | 12, average | 12, neglectful |
|---|---|---|---|---|---|---|
| after-effects | 0.4 | 0.7 | 2.2 | 1.0 | 1.4 | 3.9 |
| ailments | 31.5 | 28.4 | 40.8 | 62.2 | 57.0 | 80.3 |
| ambitions reached | 2.9 | 2.0 | 0.5 | 2.9 | 1.8 | 0.5 |
| calendar days | 8.0 | 8.0 | 8.0 | 11.0 | 11.0 | 11.0 |
| camps | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 |
| catching ailments | 1.2 | 1.2 | 1.2 | 1.4 | 1.3 | 1.1 |
| convalescence | 2.6 | 4.0 | 5.9 | 5.1 | 8.0 | 12.3 |
| days of one faith for the colony | 2.4 | 2.8 | 2.5 | 2.2 | 2.8 | 2.2 |
| decree requests | 8.0 | 7.8 | 7.8 | 7.9 | 8.1 | 8.0 |
| dilemmas | 13.5 | 13.4 | 11.9 | 13.6 | 13.3 | 11.8 |
| dreams | 107.2 | 108.8 | 107.9 | 215.6 | 216.1 | 216.5 |
| encounters | 54.3 | 53.2 | 48.4 | 54.2 | 53.3 | 49.2 |
| evenings of faith | 1.4 | 1.7 | 1.6 | 1.6 | 2.0 | 1.6 |
| events | 6.2 | 5.8 | 5.7 | 6.0 | 6.1 | 6.3 |
| friction moments | 204.4 | 268.4 | 362.1 | 406.8 | 532.4 | 721.6 |
| meetings | 3.8 | 3.7 | 3.8 | 3.8 | 3.7 | 3.8 |
| missions | 0.2 | 0.2 | 0.2 | 0.1 | 0.2 | 0.3 |
| moments | 709.4 | 660.9 | 590.7 | 1422.0 | 1325.1 | 1184.4 |
| nuisances | 26.8 | 82.8 | 140.6 | 52.3 | 165.5 | 282.9 |
| personal touches | 10.0 | 10.1 | 10.0 | 10.0 | 9.9 | 10.1 |
| play-outs | 5.4 | 4.3 | 2.6 | 5.3 | 4.4 | 2.8 |
| quiet troubles | 3.3 | 0.9 | 0.0 | 5.2 | 1.3 | 0.0 |
| serious illnesses | 0.2 | 0.4 | 2.0 | 0.3 | 0.9 | 3.4 |
| small joys | 258.0 | 127.3 | 55.5 | 515.5 | 253.8 | 111.7 |
| small letdowns | 637.9 | 1243.4 | 2192.8 | 1211.8 | 2433.0 | 4359.0 |
| social events | 71.7 | 71.0 | 71.1 | 117.6 | 117.6 | 117.6 |
| stories | 3.3 | 3.2 | 3.3 | 5.1 | 5.1 | 5.1 |
| wishes granted | 30.6 | 14.9 | 4.0 | 33.1 | 16.3 | 5.2 |
| wishes lapsed | 6.6 | 13.6 | 27.3 | 6.8 | 15.1 | 34.7 |
| wishes made | 37.6 | 28.6 | 31.6 | 40.2 | 31.5 | 40.0 |
| world events | 1.1 | 1.1 | 1.1 | 1.0 | 1.0 | 1.1 |

## What the model reads and assumes

From the defs: 1889 moods, 212 dilemmas, 231 events and missions, 163 encounters,
97 social events, 131 moments, 226 dreams, 24 nuisances, 111 wishes,
69 ailments and illnesses, 13 catching ones, 62 world events (542 defs for other mods left out).

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

The "before" column is the simulator on the code and defs as they were before phase 3 of the review (6 November
2026), with strain as a fixed share per play style; the presets' rows always use the presets as they are now.
