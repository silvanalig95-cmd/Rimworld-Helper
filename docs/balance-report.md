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
| Rimfolk's mood, well run | +3 to +6 | +6.5 | +5.7 | +5.6 |
| Rimfolk's mood, average | 0 to +3 | +2.6 | +2.0 | +2.5 |
| Rimfolk's mood, neglectful | -4 to -10 | -7.1 | -6.3 | -7.0 |
| Rimfolk's good moods at a high point (P99), well run | well short of the top | +17.9 | +16.6 | +17.5 |
| Time with +12 or more from Rimfolk's good moods, well run | rare | 21% | 13% | 16% |
| Questions (decision letters) per day, average | 1 every 2-3 days (0.33-0.5) | 0.50 | 0.53 | 0.49 |
| Questions and wishes voiced per day, average | (for reference) | 0.97 | 1.04 | 0.97 |
| Questions within 12 hours of another, average | few | 8% | 10% | 8% |
| Time with an everyday ailment, average | 10-15% | 14% | 14% | 14% |
| Time with an everyday ailment, well run | under 10-15% | 9% | 9% | 9% |
| The colony's whole mood, well run (for reference) | as before | 69% | 69% | 69% |
| The colony's whole mood, average (for reference) | as before | 64% | 64% | 64% |
| The colony's whole mood, neglectful (for reference) | as before | 39% | 40% | 39% |
| Time under the minor-break line, neglectful (letdowns as moods: 43%) | no more than before | 34% | 29% | 34% |
| The heart's day off a colonist-year, neglectful (letdowns as moods: 0.39) | under one | 0.72 | 0.79 | 0.73 |
| Steady / in good heart / in high spirits, well run (for reference) | a well-run colony's reward | 48% / 28% / 3% | 48% / 28% / 3% | - |
| Steady / in good heart / in high spirits, average (for reference) | a well-run colony's reward | 2% / 0% / 0% | 2% / 0% / 0% | - |
| Steady / in good heart / in high spirits, neglectful (for reference) | a well-run colony's reward | 0% / 0% / 0% | 0% / 0% / 0% | - |

## Before and after the heart's good side

The simulator on the code and defs as they were before the heart's good side (6 November 2026: strain alone, nothing
on the other side; `Tools/Balance/before.json`), and as they are now (default settings).

| | 6 before | 6 after | 12 before | 12 after |
|---|---|---|---|---|
| Rimfolk's mood, well run | +5.6 | +6.5 | +4.6 | +5.7 |
|   good memories (raw), well run | +8.7 (+8.7) | +8.8 (+8.8) | +7.1 (+7.1) | +7.2 (+7.2) |
|   bad memories and steady bad, well run | -7.4 | -8.5 | -5.7 | -6.8 |
| Rimfolk's mood, average | +2.5 | +2.6 | +1.9 | +2.0 |
|   good memories (raw), average | +6.1 (+6.1) | +6.0 (+6.0) | +5.0 (+5.0) | +5.0 (+5.0) |
|   bad memories and steady bad, average | -5.1 | -5.1 | -4.5 | -4.4 |
| Rimfolk's mood, neglectful | -7.0 | -7.1 | -6.5 | -6.3 |
|   good memories (raw), neglectful | +3.5 (+3.5) | +3.6 (+3.6) | +3.1 (+3.1) | +3.2 (+3.2) |
|   bad memories and steady bad, neglectful | -11.2 | -11.2 | -10.1 | -10.1 |
| Rimfolk's good moods at a high point (P99), well run | +17.5 | +17.9 | +15.8 | +16.6 |
| Time with +12 or more from Rimfolk's good moods, well run | 16% | 21% | 8% | 13% |
| Trust, year's average, average play | +13 | +14 | +9 | +9 |
| Questions per day, average play | 0.49 | 0.50 | 0.52 | 0.53 |
| Questions within 12 hours of another, average play | 8% | 8% | 10% | 10% |
| Days with two or more questions, average play | 6% | 6% | 7% | 7% |
| Wishes voiced per day, average play | 0.48 | 0.47 | 0.52 | 0.52 |
| Letters that only tell, per day, average play | 0.39 | 0.39 | 0.47 | 0.48 |
| News lines per day (one evening letter), average play | 1.71 | 1.68 | 2.34 | 2.33 |
| Time with an everyday ailment, well run | 9% | 9% | 9% | 9% |
| Time with an everyday ailment, average | 14% | 14% | 14% | 14% |
| Time with an everyday ailment, neglectful | 31% | 33% | 31% | 31% |
| Ailment days per colonist per year, average play | 8.7 | 8.6 | 8.5 | 8.7 |

## Rimfolk's mood per colonist

Good memories are as given (small joys as their moments); "raw" is the same. Steady moods are trust, the colony's
ways, ailments and strain, and what good things growing ordinary takes off Rimfolk's good moods. P10 and P90: a hard
and a good moment for one colonist.

| Case | Good memories (raw) | Bad memories | Steady good | Steady bad | **Net** | P10 / P90 | Good moods on the mind | Mood | Standards |
|---|---|---|---|---|---|---|---|---|---|
| 6 colonists, well run | +8.8 (+8.8) | -2.2 | +6.2 | -6.3 | **+6.5** | +0.6 / +12.2 | 2.3 | 69% | 0.88 |
| 6 colonists, average | +6.0 (+6.0) | -3.3 | +1.6 | -1.8 | **+2.6** | -5.0 / +10.4 | 1.6 | 64% | 0.31 |
| 6 colonists, neglectful | +3.6 (+3.6) | -5.0 | +0.6 | -6.2 | **-7.1** | -15.6 / +1.3 | 1.0 | 39% | 0.00 |
| 12 colonists, well run | +7.2 (+7.2) | -1.8 | +5.2 | -5.0 | **+5.7** | +0.3 / +11.0 | 2.0 | 69% | 0.87 |
| 12 colonists, average | +5.0 (+5.0) | -2.9 | +1.3 | -1.5 | **+2.0** | -4.9 / +9.1 | 1.4 | 64% | 0.25 |
| 12 colonists, neglectful | +3.2 (+3.2) | -4.5 | +0.5 | -5.5 | **-6.3** | -14.5 / +2.0 | 1.0 | 40% | 0.00 |

Over the year (net, in five 10-day stretches plus one; good and bad in brackets), 6 colonists:

| Play | days 0-9 | days 10-19 | days 20-29 | days 30-39 | days 40-49 | days 50-59 |
|---|---|---|---|---|---|---|
| well run | +6.0 (+13.7 / -7.7) | +6.3 (+14.4 / -8.0) | +6.6 (+15.2 / -8.6) | +6.6 (+15.4 / -8.9) | +6.6 (+15.5 / -8.9) | +6.7 (+15.7 / -9.0) |
| average | +2.5 (+7.3 / -4.9) | +2.4 (+7.4 / -5.0) | +2.8 (+7.9 / -5.1) | +2.6 (+7.7 / -5.1) | +2.4 (+7.7 / -5.2) | +2.6 (+7.8 / -5.2) |
| neglectful | -6.4 (+4.0 / -10.4) | -6.5 (+4.3 / -10.7) | -6.9 (+4.3 / -11.2) | -7.1 (+4.1 / -11.2) | -7.6 (+4.1 / -11.7) | -8.0 (+4.1 / -12.1) |

Where it comes from (6 colonists): what each source adds to a colonist's mood on average.

| Source | well run | average | neglectful |
|---|---|---|---|
| good things grown ordinary | -5.82 | -0.58 | -0.00 |
| trust | +2.66 | +0.44 | -2.07 |
| strain | -0.00 | -0.27 | -2.38 |
| wishes | +2.20 | +1.02 | +0.28 |
| the good side | +1.95 | +0.05 | +0.00 |
| meetings | +1.89 | +1.32 | +0.39 |
| life and honours | +1.30 | +1.05 | +0.81 |
| pressures (bad) | -0.20 | -0.69 | -1.25 |
| ailments | -0.23 | -0.37 | -1.03 |
| the colony's ways | +0.91 | +0.23 | -0.38 |
| wishes (bad) | -0.31 | -0.52 | -0.82 |
| setbacks (lasting) (bad) | -0.27 | -0.38 | -0.71 |
| moments | +0.70 | +0.65 | +0.60 |
| life goals | +0.65 | +0.42 | +0.26 |
| dilemmas | +0.55 | +0.40 | +0.20 |
| pressures | +0.55 | +0.20 | +0.02 |
| setbacks (bad) | -0.23 | -0.32 | -0.54 |
| social events | +0.51 | +0.52 | +0.51 |
| frictions (bad) | -0.44 | -0.49 | -0.41 |
| stories | +0.33 | +0.34 | +0.32 |
| dilemmas (bad) | -0.19 | -0.25 | -0.34 |
| meetings (bad) | -0.13 | -0.20 | -0.32 |
| calendar | +0.25 | +0.22 | +0.24 |
| dreams (bad) | -0.13 | -0.13 | -0.24 |
| events | +0.21 | +0.17 | +0.15 |
| nuisances (bad) | -0.04 | -0.12 | -0.20 |
| social events (bad) | -0.17 | -0.13 | -0.12 |
| health | -0.05 | -0.07 | -0.11 |
| encounters | +0.09 | +0.09 | +0.07 |
| encounters (bad) | -0.06 | -0.05 | -0.05 |

What good things growing ordinary takes off (6 colonists):

| Play | Net with the rule | Net without it | Good memories with | without |
|---|---|---|---|---|
| well run | +6.5 | +12.4 | +8.8 | +8.9 |
| average | +2.6 | +3.0 | +6.0 | +6.1 |
| neglectful | -7.1 | -7.0 | +3.6 | +3.5 |

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
| 6 colonists, average | 6% / 1% / 0% | 0.04 | 206 | 176 / 19 / 7 / 4 | 4% | 15 / 15 |
| 6 colonists, neglectful | 29% / 17% / 8% | 0.72 | 450 | 365 / 40 / 27 / 18 | 6% | 7 / 20 |
| 12 colonists, well run | 0% / 0% / 0% | 0.00 | 18 | 17 / 1 / 0 / 0 | 0% | 2 / 2 |
| 12 colonists, average | 6% / 2% / 0% | 0.04 | 202 | 173 / 18 / 7 / 4 | 4% | 14 / 15 |
| 12 colonists, neglectful | 29% / 17% / 10% | 0.79 | 455 | 370 / 41 / 28 / 17 | 6% | 8 / 20 |
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
| 12 colonists, well run | 69% (64% / 74%) | 0% | -12.8 | -4.6 |
| 12 colonists, average | 64% (56% / 71%) | 0% | -1.3 | -0.4 |
| 12 colonists, neglectful | 40% (28% / 51%) | 29% | -0.0 | -0.0 |
| 6 colonists, well run, without growing ordinary | 88% (78% / 100%) | 0% | +-0.0 | +0.0 |
| 6 colonists, average, without growing ordinary | 66% (56% / 77%) | 0% | +-0.0 | +0.0 |
| 6 colonists, neglectful, without growing ordinary | 39% (28% / 50%) | 33% | +-0.0 | +0.0 |
| 6 colonists, well run, letdowns and joys as moods | 68% (63% / 73%) | 0% | -13.6 | -6.4 |
| 6 colonists, average, letdowns and joys as moods | 63% (54% / 72%) | 0% | -1.2 | -0.5 |
| 6 colonists, neglectful, letdowns and joys as moods | 36% (26% / 47%) | 43% | -0.0 | -0.0 |

## The heart's good side

The other side of strain (`HeartMath`, "The good side"; `HeartSim.cs`): hardship spends it before strain builds; what a
gathering, a talk or a small joy eases beyond the strain there is fills it at its share, with friends close by and a
purpose (the heart's needs well met, drawn a day at a time per play style), deeds the colony speaks well of and a life
goal moving; it fades slowly. Steady from 20, in good heart from 45, in high spirits from 65 (+2, +3 and +5 mood); in high spirits, now and then, an inspiration or a kind word for someone close.
Filled and spent are per colonist over the 60-day year; "without" is the same seeds with its setting off.

| Case | Steady | In good heart | In high spirits | Strain at nothing (without) | Inspirations / kind words a colonist-year | Filled by | Spent by |
|---|---|---|---|---|---|---|---|
| 6 colonists, well run | 48% | 28% | 3% | 99% (70%) | 0.11 / 0.11 | purpose 57, belonging 44, good talks 42, small joys 25 | life 86, Rimfolk's letdowns 22, small moods 3 |
| 6 colonists, average | 2% | 0% | 0% | 66% (46%) | 0.00 / 0.00 | purpose 23, good talks 22, belonging 15, small joys 11 | life 51, Rimfolk's letdowns 16, small moods 10 |
| 6 colonists, neglectful | 0% | 0% | 0% | 8% (7%) | 0.00 / 0.00 | good talks 3, small joys 2, purpose 1, gatherings 1 | life 4, Rimfolk's letdowns 1, small moods 1 |
| 12 colonists, well run | 48% | 28% | 3% | 98% | 0.10 / 0.11 | purpose 57, belonging 44, good talks 42, small joys 25 | life 86, Rimfolk's letdowns 20, small moods 2 |
| 12 colonists, average | 2% | 0% | 0% | 65% | 0.00 / 0.00 | purpose 23, good talks 22, belonging 15, small joys 11 | life 51, Rimfolk's letdowns 16, small moods 9 |
| 12 colonists, neglectful | 0% | 0% | 0% | 8% | 0.00 / 0.00 | good talks 3, small joys 2, purpose 1, gatherings 0 | life 4, Rimfolk's letdowns 1, small moods 1 |
| 6 colonists, well run, lightTouch | 45% | 34% | 6% | 99% | 0.22 / 0.21 | purpose 57, belonging 44, good talks 42, small joys 26 | life 90, Rimfolk's letdowns 8, small moods 0 |
| 6 colonists, average, lightTouch | 5% | 0% | 0% | 71% | 0.00 / 0.00 | purpose 25, good talks 24, belonging 16, small joys 13 | life 61, Rimfolk's letdowns 12, small moods 5 |
| 6 colonists, neglectful, lightTouch | 0% | 0% | 0% | 9% | 0.00 / 0.00 | good talks 3, small joys 2, purpose 1, gatherings 1 | life 5, small moods 2, Rimfolk's letdowns 1 |
| 6 colonists, well run, storyRich | 47% | 28% | 3% | 98% | 0.10 / 0.11 | purpose 57, belonging 44, good talks 42, small joys 24 | life 85, Rimfolk's letdowns 22, small moods 4 |
| 6 colonists, average, storyRich | 2% | 0% | 0% | 66% | 0.00 / 0.00 | purpose 24, good talks 23, belonging 16, small joys 11 | life 51, Rimfolk's letdowns 16, small moods 10 |
| 6 colonists, neglectful, storyRich | 0% | 0% | 0% | 10% | 0.00 / 0.00 | good talks 3, small joys 2, purpose 1, gatherings 1 | life 5, small moods 1, Rimfolk's letdowns 1 |
| 6 colonists, well run, demanding | 48% | 23% | 2% | 98% | 0.08 / 0.05 | purpose 57, belonging 45, good talks 42, small joys 21 | life 83, Rimfolk's letdowns 26, small moods 4 |
| 6 colonists, average, demanding | 1% | 0% | 0% | 63% | 0.00 / 0.00 | purpose 23, good talks 22, belonging 15, small joys 9 | life 47, Rimfolk's letdowns 17, small moods 9 |
| 6 colonists, neglectful, demanding | 0% | 0% | 0% | 8% | 0.00 / 0.00 | good talks 3, small joys 1, purpose 1, gatherings 1 | life 4, Rimfolk's letdowns 1, small moods 1 |

What it changes (6 colonists, the same seeds with and without it):

| Play | Rimfolk's mood | Its own mood | Good moods at a high point (P99) | +12 or more | Strained / worn / near breaking | Under the minor-break line | Day off a year | Whole mood |
|---|---|---|---|---|---|---|---|---|
| well run | +5.5 to +6.5 | +1.9 | +17.2 to +17.9 | 15% to 21% | 1%/0%/0% to 0%/0%/0% | 0% to 0% | 0.00 to 0.00 | 69% to 69% |
| average | +2.2 to +2.6 | +0.0 | +19.8 to +20.0 | 16% to 16% | 8%/2%/0% to 6%/1%/0% | 0% to 0% | 0.05 to 0.04 | 64% to 64% |
| neglectful | -7.1 to -7.1 | +0.0 | +15.0 to +16.0 | 5% to 5% | 30%/17%/8% to 29%/17%/8% | 33% to 34% | 0.74 to 0.72 | 39% to 39% |

## Trust

Average trust in the council (-100 to 100; trusting from 25, devoted from 60, doubtful from -15, bitter from -45).

| Case | Start of year | End of year | Year's average | Trusting or devoted | Doubtful or bitter | By stretch |
|---|---|---|---|---|---|---|
| 6 colonists, well run | +34 | +70 | +56 | 88% | 0% | +38, +47, +55, +61, +65, +68 |
| 6 colonists, average | +8 | +17 | +14 | 28% | 7% | +9, +11, +13, +15, +16, +17 |
| 6 colonists, neglectful | -16 | -39 | -28 | 0% | 78% | -18, -22, -27, -31, -35, -38 |
| 12 colonists, well run | +21 | +47 | +36 | 69% | 0% | +24, +30, +35, +39, +43, +46 |
| 12 colonists, average | +6 | +11 | +9 | 15% | 5% | +6, +7, +8, +9, +10, +11 |
| 12 colonists, neglectful | -10 | -25 | -19 | 0% | 57% | -11, -15, -18, -20, -22, -25 |

## Letters and messages per day

Questions are choice letters (a dilemma, an event with answers, a calendar day, a meeting's vote, a mission
offered, a world event's question). Wishes are letters that ask without buttons. Letters only tell. Messages show
on screen as they happen; the evening's colony news gathers the rest into one letter a day. Flavour news (the
smaller encounters, moments and dreams) isn't shown at the default message level and isn't counted.

**6 colonists, average play**: 0.50 questions, 0.47 wishes voiced, 0.39 letters, 0.62 messages and 1.68 news lines a day.

| System | Questions | Wishes | Letters | Messages | News lines |
|---|---|---|---|---|---|
| wishes | 0.00 | 0.37 | 0.04 | 0.00 | 0.43 |
| event wishes | 0.00 | 0.10 | 0.00 | 0.00 | 0.00 |
| dilemmas | 0.10 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (after a raid) | 0.04 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (strangers) | 0.08 | 0.00 | 0.00 | 0.00 | 0.00 |
| events | 0.06 | 0.00 | 0.04 | 0.00 | 0.00 |
| encounters | 0.00 | 0.00 | 0.00 | 0.00 | 0.27 |
| social events | 0.00 | 0.00 | 0.00 | 0.00 | 0.31 |
| ailments | 0.00 | 0.00 | 0.02 | 0.00 | 0.15 |
| health | 0.00 | 0.00 | 0.01 | 0.00 | 0.11 |
| calendar | 0.10 | 0.00 | 0.03 | 0.00 | 0.00 |
| stories | 0.00 | 0.00 | 0.04 | 0.00 | 0.12 |
| camps | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| world events | 0.04 | 0.00 | 0.02 | 0.00 | 0.00 |
| missions | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| play-outs | 0.00 | 0.00 | 0.00 | 0.33 | 0.00 |
| meetings | 0.06 | 0.00 | 0.03 | 0.00 | 0.03 |
| decrees | 0.00 | 0.00 | 0.00 | 0.13 | 0.00 |
| dreams | 0.00 | 0.00 | 0.00 | 0.00 | 0.01 |
| people | 0.00 | 0.00 | 0.11 | 0.00 | 0.24 |
| colony | 0.00 | 0.00 | 0.04 | 0.17 | 0.00 |
| world | 0.00 | 0.00 | 0.01 | 0.00 | 0.00 |

**12 colonists, average play**: 0.53 questions, 0.52 wishes voiced, 0.48 letters, 0.66 messages and 2.33 news lines a day.

| System | Questions | Wishes | Letters | Messages | News lines |
|---|---|---|---|---|---|
| wishes | 0.00 | 0.32 | 0.04 | 0.00 | 0.48 |
| event wishes | 0.00 | 0.19 | 0.00 | 0.00 | 0.00 |
| dilemmas | 0.10 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (after a raid) | 0.04 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (strangers) | 0.07 | 0.00 | 0.00 | 0.00 | 0.00 |
| events | 0.06 | 0.00 | 0.04 | 0.00 | 0.00 |
| encounters | 0.00 | 0.00 | 0.00 | 0.00 | 0.27 |
| social events | 0.00 | 0.00 | 0.00 | 0.00 | 0.50 |
| ailments | 0.00 | 0.00 | 0.02 | 0.00 | 0.31 |
| health | 0.00 | 0.00 | 0.01 | 0.00 | 0.18 |
| calendar | 0.13 | 0.00 | 0.05 | 0.00 | 0.00 |
| stories | 0.00 | 0.00 | 0.07 | 0.00 | 0.19 |
| camps | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| world events | 0.05 | 0.00 | 0.02 | 0.00 | 0.00 |
| missions | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| play-outs | 0.00 | 0.00 | 0.00 | 0.36 | 0.00 |
| meetings | 0.06 | 0.00 | 0.03 | 0.00 | 0.03 |
| decrees | 0.00 | 0.00 | 0.00 | 0.13 | 0.00 |
| dreams | 0.00 | 0.00 | 0.00 | 0.00 | 0.02 |
| people | 0.00 | 0.00 | 0.14 | 0.00 | 0.35 |
| colony | 0.00 | 0.00 | 0.04 | 0.17 | 0.00 |
| world | 0.00 | 0.00 | 0.01 | 0.00 | 0.00 |

By play style (6 colonists):

| Play | Questions | Wishes | Letters | Messages | News lines | Questions within 12 h of another | Days with two or more questions |
|---|---|---|---|---|---|---|---|
| well run | 0.50 | 0.65 | 0.48 | 0.65 | 1.72 | 8% | 6% |
| average | 0.50 | 0.47 | 0.39 | 0.62 | 1.68 | 8% | 6% |
| neglectful | 0.48 | 0.53 | 0.39 | 0.57 | 1.99 | 8% | 6% |

## Ailments

Everyday ailments and what they leave behind (a lingering cough), per colonist over the year. "Not quiet" leaves
out the smallest (a stiff neck, hiccups, a smoke cough), which only a player who wants every message hears about.

| Case | Ailment days per colonist | Time with one | Time with one, not quiet | Caught, per colony | Circumstance, per colony | Serious, per colony |
|---|---|---|---|---|---|---|
| 6 colonists, well run | 5.6 | 9% | 8% | 0.9 | 30.8 | 0.3 |
| 6 colonists, average | 8.6 | 14% | 12% | 1.1 | 28.2 | 0.6 |
| 6 colonists, neglectful | 19.8 | 33% | 29% | 1.0 | 41.0 | 2.1 |
| 12 colonists, well run | 5.8 | 9% | 8% | 1.0 | 62.7 | 0.3 |
| 12 colonists, average | 8.7 | 14% | 12% | 1.2 | 56.8 | 0.9 |
| 12 colonists, neglectful | 19.0 | 31% | 28% | 1.1 | 81.2 | 3.8 |

What fills the time (days per colonist per year, 6 colonists):

| Ailment | well run | average | neglectful |
|---|---|---|---|
| Convalescent | 0.4 | 0.8 | 3.3 |
| Lice | 0.0 | 0.5 | 3.9 |
| Insomnia | 0.1 | 0.4 | 1.7 |
| RimrotCough | 0.0 | 0.3 | 1.8 |
| ChestCold | 0.5 | 0.4 | 0.7 |
| HeadCold | 0.3 | 0.5 | 0.5 |
| StrainedBack | 0.3 | 0.4 | 0.5 |
| Worms | 0.0 | 0.2 | 1.0 |
| SmokeCough | 0.0 | 0.3 | 0.9 |
| Toothache | 0.2 | 0.3 | 0.5 |
| ColdSore | 0.1 | 0.2 | 0.7 |
| ChappedHands | 0.2 | 0.2 | 0.3 |
| Typhus | 0.0 | 0.1 | 0.7 |
| SoreHands | 0.2 | 0.2 | 0.3 |
| BadNights | 0.2 | 0.3 | 0.2 |
| Chilblains | 0.2 | 0.3 | 0.2 |

"Serious, per colony" above is Rimfolk's serious illnesses as measured before the shared illness budget. Those and the
game's own diseases, with the budget and without it, over six years a colony, are
`dotnet run -c Release --project Tools/Balance -- --illness` (IllnessSim.cs; DESIGN.md, "The shared illness budget"),
measured on 8 November 2026 (600 colony-years a case, the same seeds with it and without; written here by hand until
the report is next written).

A temperate forest at Strive to survive (the game's own every 50 days, 1.2 a year; Rimfolk's share at most 35%):

| Case | Without: Rimfolk's | Without: the game's | Without: together | With: Rimfolk's | With: the game's | With: together | Held back | Put off | Rimfolk's share | The game's deadly, without / with |
|---|---|---|---|---|---|---|---|---|---|---|
| 6 colonists, well run | 0.19 | 1.17 | 1.36 | 0.10 | 1.09 | 1.18 | 0.08 | 0.10 | 8 % | 0.81 / 0.76 |
| 6 colonists, average | 0.44 | 1.21 | 1.65 | 0.17 | 1.05 | 1.22 | 0.16 | 0.32 | 14 % | 0.78 / 0.67 |
| 6 colonists, neglectful | 1.05 | 1.22 | 2.27 | 0.34 | 0.85 | 1.20 | 0.36 | 1.66 | 29 % | 0.80 / 0.56 |
| 12 colonists, well run | 0.28 | 1.25 | 1.53 | 0.15 | 1.10 | 1.25 | 0.14 | 0.17 | 12 % | 0.80 / 0.70 |
| 12 colonists, average | 0.63 | 1.25 | 1.88 | 0.27 | 0.98 | 1.25 | 0.27 | 0.58 | 21 % | 0.82 / 0.64 |
| 12 colonists, neglectful | 1.40 | 1.16 | 2.56 | 0.37 | 0.78 | 1.16 | 0.38 | 3.63 | 32 % | 0.77 / 0.53 |

At Adventure story's interval (`--interval 1.5`: every 75 days, 0.8 a year; Rimfolk's share at most 40%):

| Case | Without: Rimfolk's | Without: the game's | Without: together | With: Rimfolk's | With: the game's | With: together | Held back | Put off | Rimfolk's share | The game's deadly, without / with |
|---|---|---|---|---|---|---|---|---|---|---|
| 6 colonists, well run | 0.19 | 0.78 | 0.97 | 0.12 | 0.68 | 0.79 | 0.11 | 0.09 | 15 % | 0.54 / 0.47 |
| 6 colonists, average | 0.43 | 0.80 | 1.23 | 0.19 | 0.64 | 0.83 | 0.16 | 0.32 | 22 % | 0.53 / 0.42 |
| 6 colonists, neglectful | 1.11 | 0.84 | 1.95 | 0.28 | 0.54 | 0.83 | 0.29 | 1.86 | 34 % | 0.54 / 0.36 |
| 12 colonists, well run | 0.30 | 0.84 | 1.14 | 0.15 | 0.70 | 0.85 | 0.14 | 0.14 | 17 % | 0.53 / 0.43 |
| 12 colonists, average | 0.63 | 0.81 | 1.44 | 0.24 | 0.58 | 0.82 | 0.23 | 0.56 | 30 % | 0.54 / 0.39 |
| 12 colonists, neglectful | 1.40 | 0.77 | 2.17 | 0.28 | 0.46 | 0.74 | 0.31 | 3.78 | 38 % | 0.51 / 0.30 |

## The presets

The style presets of the settings window, applied as the window applies them (6 colonists).

| Preset | Play | Net | Good memories | Bad + steady bad | Questions per day | Wishes per day | Time ill |
|---|---|---|---|---|---|---|---|
| default | well run | +6.5 | +8.8 | -8.5 | 0.50 | 0.65 | 9% |
| default | average | +2.6 | +6.0 | -5.1 | 0.50 | 0.47 | 14% |
| default | neglectful | -7.1 | +3.6 | -11.2 | 0.48 | 0.53 | 33% |
| lightTouch | well run | +7.2 | +6.7 | -5.1 | 0.45 | 0.31 | 5% |
| lightTouch | average | +4.7 | +5.0 | -1.8 | 0.45 | 0.24 | 7% |
| lightTouch | neglectful | -1.6 | +3.1 | -5.1 | 0.43 | 0.30 | 15% |
| storyRich | well run | +7.0 | +10.0 | -9.3 | 0.60 | 0.64 | 10% |
| storyRich | average | +3.2 | +6.9 | -5.4 | 0.57 | 0.48 | 14% |
| storyRich | neglectful | -6.8 | +4.2 | -11.5 | 0.53 | 0.53 | 32% |
| demanding | well run | +2.6 | +6.4 | -8.7 | 0.50 | 0.70 | 11% |
| demanding | average | -2.7 | +4.4 | -8.3 | 0.49 | 0.52 | 18% |
| demanding | neglectful | -14.9 | +2.6 | -17.9 | 0.48 | 0.65 | 37% |

## How often things happen (per colony per year)

| What | 6, well run | 6, average | 6, neglectful | 12, well run | 12, average | 12, neglectful |
|---|---|---|---|---|---|---|
| after-effects | 0.5 | 0.8 | 2.3 | 0.9 | 1.3 | 4.2 |
| ailments | 30.8 | 28.2 | 41.0 | 62.7 | 56.8 | 81.2 |
| ambitions reached | 2.7 | 2.0 | 0.6 | 3.0 | 1.8 | 0.6 |
| calendar days | 8.0 | 8.0 | 8.0 | 11.0 | 11.0 | 11.0 |
| camps | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 |
| catching ailments | 0.9 | 1.1 | 1.0 | 1.0 | 1.2 | 1.1 |
| convalescence | 2.6 | 3.8 | 5.6 | 5.0 | 7.6 | 11.6 |
| days of one faith for the colony | 2.3 | 2.8 | 2.6 | 2.1 | 3.2 | 2.3 |
| decree requests | 8.0 | 7.7 | 7.8 | 7.8 | 8.0 | 7.7 |
| dilemmas | 13.7 | 13.4 | 11.9 | 13.4 | 13.2 | 12.0 |
| dreams | 107.6 | 108.7 | 108.2 | 216.2 | 215.1 | 216.8 |
| encounters | 53.8 | 52.7 | 48.2 | 54.8 | 52.9 | 48.3 |
| evenings of faith | 1.5 | 1.6 | 1.6 | 1.5 | 2.0 | 1.5 |
| events | 5.8 | 6.2 | 6.4 | 5.9 | 6.1 | 6.2 |
| friction moments | 203.2 | 266.7 | 359.9 | 405.7 | 535.9 | 717.4 |
| meetings | 3.8 | 3.8 | 3.8 | 3.8 | 3.7 | 3.8 |
| missions | 0.3 | 0.2 | 0.2 | 0.2 | 0.2 | 0.2 |
| moments | 712.8 | 660.2 | 591.9 | 1422.2 | 1323.4 | 1185.1 |
| nuisances | 27.1 | 82.4 | 139.3 | 53.5 | 163.6 | 282.7 |
| personal touches | 10.1 | 10.2 | 10.1 | 10.0 | 10.0 | 10.0 |
| play-outs | 5.0 | 4.5 | 2.9 | 5.2 | 4.5 | 2.8 |
| quiet troubles | 3.7 | 0.9 | 0.0 | 6.5 | 1.3 | 0.0 |
| serious illnesses | 0.3 | 0.6 | 2.1 | 0.3 | 0.9 | 3.8 |
| small joys | 257.8 | 127.1 | 55.4 | 515.6 | 252.6 | 112.3 |
| small letdowns | 638.8 | 1242.4 | 2202.9 | 1211.7 | 2429.7 | 4357.2 |
| social events | 71.5 | 71.6 | 71.3 | 117.5 | 117.6 | 117.5 |
| stories | 3.3 | 3.2 | 3.3 | 5.1 | 5.1 | 5.2 |
| wishes granted | 32.0 | 15.1 | 4.4 | 33.8 | 16.1 | 5.2 |
| wishes lapsed | 6.5 | 13.3 | 27.1 | 7.2 | 14.8 | 34.6 |
| wishes made | 38.8 | 28.3 | 31.8 | 41.3 | 30.9 | 40.0 |
| world events | 1.0 | 1.0 | 1.1 | 1.1 | 1.1 | 1.2 |

## What the model reads and assumes

From the defs: 1993 moods, 238 dilemmas, 263 events and missions, 163 encounters,
123 social events, 131 moments, 271 dreams, 24 nuisances, 118 wishes,
80 ailments and illnesses, 13 catching ones, 70 world events (542 defs for other mods left out).

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
