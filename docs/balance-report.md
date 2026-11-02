# Rimfolk balance report

Written by the colony-year simulator in `Tools/Balance` (run it from the repository root with
`dotnet run -c Release --project Tools/Balance -- . --before Tools/Balance/before.json`). It plays a settled colony through one
60-day year (30 days of warm-up first, so standards and trust have settled) 120 times for each case, without the game:
the moods, wishes, dilemmas, events and ailments come from the def XML, the rates from the code (read from the source
where it can, see the last section), and the mood rules from the mod's own `GoodTimesMath`, `GoodLifeMath`,
`PressureMath`, `SetbackMath`, `AilmentMath` and `ArcMath`, compiled in as they are. What it can't know (how often the storyteller
sends visitors, how a player plays) is in `Tools/Balance/Rates.cs`, one documented table. Treat the numbers as
a model's: good for comparing settings and changes, not a promise about one game.

**Rimfolk's mood** below is everything Rimfolk adds to a colonist's mood, per colonist, averaged over the year:
its good memories (after the shared budget and fading sooner in good times), its bad memories (setbacks, a lapsed
wish, frictions, a nightmare, a quiet trouble), and its steady moods (trust, the colony's ways, everyday pressures,
ailments, a wish still waiting, strain, and what being used to the good life takes back of all the good moods,
the game's included). The game's own moods aren't in it.

## Against the targets

The targets are in DESIGN.md ("Balance targets").

| | Target | 6 colonists | 12 colonists | 6 colonists before |
|---|---|---|---|---|
| Rimfolk's mood, well run | +3 to +6 | +2.4 | +2.1 | +2.9 |
| Rimfolk's mood, average | 0 to +3 | +0.6 | +0.2 | -0.3 |
| Rimfolk's mood, neglectful | -4 to -10 | -8.9 | -7.9 | -11.3 |
| Rimfolk's good moods at a high point (P99), well run | well short of the top | +15.5 | +14.4 | +15.0 |
| Time with +12 or more from Rimfolk's good moods, well run | rare | 11% | 5% | 10% |
| Questions (decision letters) per day, average | 1 every 2-3 days (0.33-0.5) | 0.50 | 0.52 | 0.53 |
| Questions and wishes voiced per day, average | (for reference) | 0.95 | 1.02 | 0.98 |
| Questions within 12 hours of another, average | few | 9% | 9% | 18% |
| Time with an everyday ailment, average | 10-15% | 14% | 14% | 24% |
| Time with an everyday ailment, well run | under 10-15% | 9% | 9% | 17% |

## Before and after the balance pass

The same simulator on the code and defs as they were before the pass, and as they are now (default settings).

| | 6 before | 6 after | 12 before | 12 after |
|---|---|---|---|---|
| Rimfolk's mood, well run | +2.9 | +2.4 | +2.5 | +2.1 |
|   good memories (raw), well run | +4.7 (+5.5) | +4.6 (+5.2) | +4.1 (+4.6) | +3.9 (+4.3) |
|   bad memories and steady bad, well run | -5.3 | -5.7 | -4.6 | -4.7 |
| Rimfolk's mood, average | -0.3 | +0.6 | -0.5 | +0.2 |
|   good memories (raw), average | +5.9 (+6.6) | +5.6 (+6.1) | +5.2 (+5.7) | +4.9 (+5.2) |
|   bad memories and steady bad, average | -8.1 | -6.8 | -7.4 | -6.3 |
| Rimfolk's mood, neglectful | -11.3 | -8.9 | -10.1 | -7.9 |
|   good memories (raw), neglectful | +4.1 (+4.4) | +3.9 (+4.1) | +3.7 (+3.9) | +3.6 (+3.8) |
|   bad memories and steady bad, neglectful | -16.0 | -13.4 | -14.4 | -12.1 |
| Rimfolk's good moods at a high point (P99), well run | +15.0 | +15.5 | +14.4 | +14.4 |
| Time with +12 or more from Rimfolk's good moods, well run | 10% | 11% | 6% | 5% |
| Trust, year's average, average play | +13 | +13 | +10 | +9 |
| Questions per day, average play | 0.53 | 0.50 | 0.56 | 0.52 |
| Questions within 12 hours of another, average play | 18% | 9% | 19% | 9% |
| Days with two or more questions, average play | 9% | 6% | 10% | 7% |
| Wishes voiced per day, average play | 0.45 | 0.45 | 0.50 | 0.50 |
| Letters that only tell, per day, average play | 0.41 | 0.39 | 0.49 | 0.47 |
| News lines per day (one evening letter), average play | 1.66 | 1.66 | 2.29 | 2.31 |
| Time with an everyday ailment, well run | 17% | 9% | 17% | 9% |
| Time with an everyday ailment, average | 24% | 14% | 23% | 14% |
| Time with an everyday ailment, neglectful | 44% | 30% | 44% | 30% |
| Ailment days per colonist per year, average play | 14.4 | 8.3 | 13.8 | 8.4 |

## Rimfolk's mood per colonist

Good memories are what they count for after the shared budget (the two biggest in full, then 60%, 45%, 35%, 25%)
and fading sooner above 75% mood; "raw" is the same memories without the budget. Steady moods are trust, the
colony's ways, pressures, ailments, waiting and strain. P10 and P90: a hard and a good moment for one colonist.

| Case | Good memories (raw) | Bad memories | Steady good | Steady bad | **Net** | P10 / P90 | Good moods on the mind | Mood | Standards |
|---|---|---|---|---|---|---|---|---|---|
| 6 colonists, well run | +4.6 (+5.2) | -3.1 | +3.5 | -2.6 | **+2.4** | -3.2 / +7.4 | 2.3 | 69% | 0.91 |
| 6 colonists, average | +5.6 (+6.1) | -3.5 | +1.8 | -3.3 | **+0.6** | -7.0 / +8.2 | 1.7 | 63% | 0.16 |
| 6 colonists, neglectful | +3.9 (+4.1) | -4.7 | +0.6 | -8.7 | **-8.9** | -17.0 / -1.2 | 1.2 | 37% | 0.00 |
| 12 colonists, well run | +3.9 (+4.3) | -2.7 | +2.9 | -2.0 | **+2.1** | -3.1 / +6.9 | 1.9 | 69% | 0.88 |
| 12 colonists, average | +4.9 (+5.2) | -3.1 | +1.6 | -3.1 | **+0.2** | -6.7 / +7.3 | 1.5 | 62% | 0.13 |
| 12 colonists, neglectful | +3.6 (+3.8) | -4.2 | +0.6 | -7.9 | **-7.9** | -15.3 / -0.5 | 1.2 | 38% | 0.00 |

Over the year (net, in five 10-day stretches plus one; good and bad in brackets), 6 colonists:

| Play | days 0-9 | days 10-19 | days 20-29 | days 30-39 | days 40-49 | days 50-59 |
|---|---|---|---|---|---|---|
| well run | +2.3 (+7.8 / -5.5) | +2.3 (+8.0 / -5.6) | +2.3 (+8.1 / -5.8) | +2.3 (+8.1 / -5.8) | +2.6 (+8.3 / -5.8) | +2.5 (+8.3 / -5.9) |
| average | +0.9 (+7.4 / -6.5) | +0.7 (+7.6 / -6.9) | +0.8 (+7.6 / -6.8) | +0.5 (+7.4 / -6.8) | +0.0 (+7.0 / -7.0) | +0.8 (+7.7 / -6.9) |
| neglectful | -8.2 (+4.3 / -12.5) | -8.4 (+4.6 / -12.9) | -8.8 (+4.4 / -13.3) | -9.2 (+4.2 / -13.5) | -9.4 (+4.6 / -14.0) | -9.6 (+4.7 / -14.3) |

Where it comes from (6 colonists): what each source adds to a colonist's mood on average. Good memories are
shown as they count after the budget.

| Source | well run | average | neglectful |
|---|---|---|---|
| pressures | +0.69 | -0.94 | -2.38 |
| trust | +1.49 | +0.39 | -2.21 |
| setbacks (bad) | -1.06 | -1.22 | -1.74 |
| setbacks (lasting) | -0.46 | -0.70 | -1.35 |
| used to the good life | -1.30 | -0.05 | -0.00 |
| meetings | +1.13 | +1.15 | +0.29 |
| strain | -0.06 | -0.36 | -1.14 |
| wishes | +1.13 | +0.81 | +0.25 |
| moments | +0.57 | +0.99 | +1.05 |
| life and honours | +0.71 | +0.99 | +0.79 |
| ailments | -0.17 | -0.32 | -0.87 |
| wishes (bad) | -0.30 | -0.49 | -0.83 |
| frictions (bad) | -0.79 | -0.81 | -0.68 |
| dilemmas | +0.39 | +0.48 | +0.28 |
| social events | +0.22 | +0.41 | +0.47 |
| life goals | +0.36 | +0.43 | +0.25 |
| the colony's ways | +0.42 | +0.18 | -0.38 |
| dilemmas (bad) | -0.23 | -0.26 | -0.36 |
| stories | +0.15 | +0.28 | +0.34 |
| meetings (bad) | -0.11 | -0.20 | -0.34 |
| nuisances (bad) | -0.05 | -0.16 | -0.27 |
| dreams (bad) | -0.15 | -0.15 | -0.26 |
| calendar | +0.09 | +0.16 | +0.21 |
| events | +0.10 | +0.19 | +0.13 |
| social events (bad) | -0.18 | -0.13 | -0.14 |
| health | -0.05 | -0.07 | -0.12 |
| quiet troubles (bad) | -0.10 | -0.01 | +0.00 |
| encounters | +0.05 | +0.08 | +0.07 |
| encounters (bad) | -0.05 | -0.04 | -0.05 |

What the shared budget and fading sooner take off (6 colonists):

| Play | Net with both rules | Net without either | Good memories with | without |
|---|---|---|---|---|
| well run | +2.4 | +2.7 | +4.6 | +5.2 |
| average | +0.6 | +0.8 | +5.6 | +5.9 |
| neglectful | -8.9 | -8.7 | +3.9 | +4.1 |

## Trust

Average trust in the council (-100 to 100; trusting from 25, devoted from 60, doubtful from -15, bitter from -45).

| Case | Start of year | End of year | Year's average | Trusting or devoted | Doubtful or bitter | By stretch |
|---|---|---|---|---|---|---|
| 6 colonists, well run | +34 | +73 | +57 | 89% | 0% | +38, +47, +55, +62, +68, +72 |
| 6 colonists, average | +8 | +16 | +13 | 27% | 6% | +9, +11, +13, +15, +15, +16 |
| 6 colonists, neglectful | -17 | -41 | -30 | 0% | 83% | -19, -25, -29, -33, -37, -40 |
| 12 colonists, well run | +21 | +46 | +35 | 67% | 0% | +23, +29, +34, +38, +42, +45 |
| 12 colonists, average | +5 | +11 | +9 | 15% | 5% | +6, +7, +9, +9, +10, +11 |
| 12 colonists, neglectful | -10 | -25 | -19 | 0% | 58% | -11, -15, -18, -21, -23, -25 |

## Letters and messages per day

Questions are choice letters (a dilemma, an event with answers, a calendar day, a meeting's vote, a mission
offered, a world event's question). Wishes are letters that ask without buttons. Letters only tell. Messages show
on screen as they happen; the evening's colony news gathers the rest into one letter a day. Flavour news (the
smaller encounters, moments and dreams) isn't shown at the default message level and isn't counted.

**6 colonists, average play**: 0.50 questions, 0.45 wishes voiced, 0.39 letters, 0.66 messages and 1.66 news lines a day.

| System | Questions | Wishes | Letters | Messages | News lines |
|---|---|---|---|---|---|
| wishes | 0.00 | 0.36 | 0.03 | 0.00 | 0.42 |
| event wishes | 0.00 | 0.09 | 0.00 | 0.00 | 0.00 |
| dilemmas | 0.10 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (after a raid) | 0.04 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (strangers) | 0.08 | 0.00 | 0.00 | 0.00 | 0.00 |
| events | 0.07 | 0.00 | 0.04 | 0.00 | 0.00 |
| encounters | 0.00 | 0.00 | 0.00 | 0.00 | 0.27 |
| social events | 0.00 | 0.00 | 0.00 | 0.00 | 0.30 |
| ailments | 0.00 | 0.00 | 0.02 | 0.00 | 0.14 |
| health | 0.00 | 0.00 | 0.01 | 0.00 | 0.11 |
| calendar | 0.10 | 0.00 | 0.03 | 0.00 | 0.00 |
| stories | 0.00 | 0.00 | 0.04 | 0.00 | 0.12 |
| camps | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| world events | 0.05 | 0.00 | 0.02 | 0.00 | 0.00 |
| missions | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| play-outs | 0.00 | 0.00 | 0.00 | 0.37 | 0.00 |
| meetings | 0.06 | 0.00 | 0.03 | 0.00 | 0.03 |
| decrees | 0.00 | 0.00 | 0.00 | 0.13 | 0.00 |
| dreams | 0.00 | 0.00 | 0.00 | 0.00 | 0.01 |
| people | 0.00 | 0.00 | 0.11 | 0.00 | 0.25 |
| colony | 0.00 | 0.00 | 0.04 | 0.17 | 0.00 |
| world | 0.00 | 0.00 | 0.01 | 0.00 | 0.00 |

**12 colonists, average play**: 0.52 questions, 0.50 wishes voiced, 0.47 letters, 0.68 messages and 2.31 news lines a day.

| System | Questions | Wishes | Letters | Messages | News lines |
|---|---|---|---|---|---|
| wishes | 0.00 | 0.31 | 0.04 | 0.00 | 0.46 |
| event wishes | 0.00 | 0.19 | 0.00 | 0.00 | 0.00 |
| dilemmas | 0.10 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (after a raid) | 0.04 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (strangers) | 0.07 | 0.00 | 0.00 | 0.00 | 0.00 |
| events | 0.06 | 0.00 | 0.04 | 0.00 | 0.00 |
| encounters | 0.00 | 0.00 | 0.00 | 0.00 | 0.28 |
| social events | 0.00 | 0.00 | 0.00 | 0.00 | 0.51 |
| ailments | 0.00 | 0.00 | 0.02 | 0.00 | 0.29 |
| health | 0.00 | 0.00 | 0.01 | 0.00 | 0.17 |
| calendar | 0.13 | 0.00 | 0.05 | 0.00 | 0.00 |
| stories | 0.00 | 0.00 | 0.07 | 0.00 | 0.19 |
| camps | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| world events | 0.05 | 0.00 | 0.02 | 0.00 | 0.00 |
| missions | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| play-outs | 0.00 | 0.00 | 0.00 | 0.39 | 0.00 |
| meetings | 0.06 | 0.00 | 0.03 | 0.00 | 0.03 |
| decrees | 0.00 | 0.00 | 0.00 | 0.13 | 0.00 |
| dreams | 0.00 | 0.00 | 0.00 | 0.00 | 0.03 |
| people | 0.00 | 0.00 | 0.14 | 0.00 | 0.36 |
| colony | 0.00 | 0.00 | 0.04 | 0.17 | 0.00 |
| world | 0.00 | 0.00 | 0.01 | 0.00 | 0.00 |

By play style (6 colonists):

| Play | Questions | Wishes | Letters | Messages | News lines | Questions within 12 h of another | Days with two or more questions |
|---|---|---|---|---|---|---|---|
| well run | 0.50 | 0.65 | 0.48 | 0.72 | 1.72 | 8% | 6% |
| average | 0.50 | 0.45 | 0.39 | 0.66 | 1.66 | 9% | 6% |
| neglectful | 0.48 | 0.53 | 0.40 | 0.59 | 1.96 | 8% | 5% |

## Ailments

Everyday ailments and what they leave behind (a lingering cough), per colonist over the year. "Not quiet" leaves
out the smallest (a stiff neck, hiccups, a smoke cough), which only a player who wants every message hears about.

| Case | Ailment days per colonist | Time with one | Time with one, not quiet | Caught, per colony | Circumstance, per colony | Serious, per colony |
|---|---|---|---|---|---|---|
| 6 colonists, well run | 5.4 | 9% | 7% | 1.0 | 30.4 | 0.1 |
| 6 colonists, average | 8.3 | 14% | 12% | 1.3 | 27.8 | 0.4 |
| 6 colonists, neglectful | 18.0 | 30% | 26% | 1.1 | 38.7 | 1.7 |
| 12 colonists, well run | 5.6 | 9% | 8% | 1.3 | 62.3 | 0.3 |
| 12 colonists, average | 8.4 | 14% | 12% | 1.2 | 57.0 | 0.7 |
| 12 colonists, neglectful | 18.2 | 30% | 27% | 1.1 | 77.0 | 3.7 |

What fills the time (days per colonist per year, 6 colonists):

| Ailment | well run | average | neglectful |
|---|---|---|---|
| Lice | 0.0 | 0.4 | 3.7 |
| Convalescent | 0.1 | 0.5 | 2.7 |
| RimrotCough | 0.0 | 0.4 | 2.1 |
| ChestCold | 0.5 | 0.7 | 0.6 |
| HeadCold | 0.4 | 0.6 | 0.5 |
| Insomnia | 0.1 | 0.3 | 1.1 |
| StrainedBack | 0.3 | 0.4 | 0.5 |
| SmokeCough | 0.0 | 0.2 | 0.9 |
| Worms | 0.0 | 0.1 | 0.9 |
| ColdSore | 0.1 | 0.3 | 0.5 |
| Toothache | 0.1 | 0.2 | 0.5 |
| ChappedHands | 0.2 | 0.3 | 0.3 |
| Chilblains | 0.2 | 0.2 | 0.4 |
| BadNights | 0.2 | 0.2 | 0.3 |
| Dreamfever | 0.2 | 0.3 | 0.2 |
| Sunburn | 0.2 | 0.2 | 0.2 |

## The presets

The style presets of the settings window, applied as the window applies them (6 colonists).

| Preset | Play | Net | Good memories | Bad + steady bad | Questions per day | Wishes per day | Time ill |
|---|---|---|---|---|---|---|---|
| default | well run | +2.4 | +4.6 | -5.7 | 0.50 | 0.65 | 9% |
| default | average | +0.6 | +5.6 | -6.8 | 0.50 | 0.45 | 14% |
| default | neglectful | -8.9 | +3.9 | -13.4 | 0.48 | 0.53 | 30% |
| lightTouch | well run | +4.8 | +3.6 | -1.9 | 0.45 | 0.34 | 5% |
| lightTouch | average | +4.0 | +4.7 | -2.3 | 0.45 | 0.26 | 7% |
| lightTouch | neglectful | -0.8 | +3.6 | -5.0 | 0.43 | 0.36 | 15% |
| storyRich | well run | +2.4 | +4.9 | -6.2 | 0.60 | 0.65 | 9% |
| storyRich | average | +0.9 | +6.2 | -7.2 | 0.58 | 0.45 | 14% |
| storyRich | neglectful | -8.7 | +4.6 | -13.9 | 0.54 | 0.53 | 31% |
| demanding | well run | -0.2 | +4.0 | -7.5 | 0.50 | 0.69 | 9% |
| demanding | average | -3.6 | +4.8 | -10.0 | 0.49 | 0.51 | 14% |
| demanding | neglectful | -14.8 | +3.3 | -18.6 | 0.47 | 0.65 | 30% |

## How often things happen (per colony per year)

| What | 6, well run | 6, average | 6, neglectful | 12, well run | 12, average | 12, neglectful |
|---|---|---|---|---|---|---|
| after-effects | 0.3 | 0.7 | 2.0 | 0.8 | 1.2 | 4.2 |
| ailments | 30.4 | 27.8 | 38.7 | 62.3 | 57.0 | 77.0 |
| ambitions reached | 3.0 | 1.8 | 0.5 | 2.7 | 1.8 | 0.6 |
| calendar days | 8.0 | 8.0 | 8.0 | 11.0 | 11.0 | 11.0 |
| camps | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 |
| catching ailments | 1.0 | 1.3 | 1.1 | 1.3 | 1.2 | 1.1 |
| convalescence | 2.6 | 3.9 | 5.9 | 5.2 | 7.3 | 12.0 |
| days of one faith for the colony | 2.4 | 2.5 | 2.8 | 2.6 | 3.3 | 2.3 |
| decree requests | 7.8 | 7.8 | 7.8 | 7.4 | 7.5 | 8.1 |
| dilemmas | 13.6 | 13.2 | 12.1 | 13.6 | 12.8 | 11.9 |
| dreams | 107.3 | 108.3 | 107.8 | 215.4 | 216.6 | 217.8 |
| encounters | 54.6 | 51.7 | 49.2 | 53.6 | 52.2 | 49.1 |
| evenings of faith | 1.5 | 1.8 | 1.6 | 1.4 | 1.8 | 1.6 |
| events | 6.1 | 6.5 | 6.0 | 6.1 | 6.1 | 6.2 |
| friction moments | 203.1 | 266.9 | 358.8 | 405.5 | 537.9 | 720.7 |
| meetings | 3.8 | 3.8 | 3.8 | 3.8 | 3.8 | 3.8 |
| missions | 0.2 | 0.2 | 0.2 | 0.2 | 0.2 | 0.2 |
| moments | 712.5 | 661.2 | 591.7 | 1420.5 | 1320.7 | 1184.0 |
| nuisances | 26.7 | 82.8 | 140.9 | 53.4 | 164.5 | 284.2 |
| personal touches | 10.0 | 10.0 | 10.0 | 10.0 | 9.9 | 10.0 |
| play-outs | 7.1 | 5.8 | 3.4 | 7.4 | 5.6 | 3.5 |
| quiet troubles | 3.5 | 0.5 | 0.0 | 6.5 | 0.7 | 0.0 |
| serious illnesses | 0.1 | 0.4 | 1.7 | 0.3 | 0.7 | 3.7 |
| social events | 71.4 | 70.9 | 71.7 | 117.6 | 117.4 | 117.6 |
| stories | 3.2 | 3.3 | 3.3 | 5.0 | 5.1 | 5.1 |
| wishes granted | 32.5 | 14.4 | 4.2 | 33.4 | 15.6 | 5.5 |
| wishes lapsed | 6.3 | 12.9 | 27.5 | 6.7 | 14.3 | 34.5 |
| wishes made | 39.2 | 27.0 | 31.8 | 40.5 | 30.1 | 40.0 |
| world events | 1.0 | 1.0 | 1.1 | 1.0 | 1.1 | 1.1 |

## What the model reads and assumes

From the defs: 1864 moods, 206 dilemmas, 231 events and missions, 163 encounters,
97 social events, 131 moments, 226 dreams, 24 nuisances, 111 wishes,
66 ailments and illnesses, 13 catching ones, 55 world events (540 defs for other mods left out).

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
| Strained (-3 / -5 / -8) | 2% / 0% / 0% | 8% / 2% / 0% | 20% / 8% / 2% |
| The colony's ways (-4 / -2 / +2 / +4) | 0% / 8% / 35% / 10% | 2% / 13% / 20% / 4% | 5% / 20% / 10% / 0% |

Not modelled: quirks, heirlooms and the colony's trait pride (small steady moods either way), decrees' own moods
(the player's choice), secrets, vendettas and nemeses' moods (rare), the base game's moods themselves (a fixed
good and bad per play style, under the standards' share), battles from events, and other mods' content.
The links between systems (`Interplay/`: strain eased, bonds, wishes made likelier by a run of setbacks, stories
moved on) give no mood of their own and aren't modelled; the social events, encounters and dreams they brought
are read from the defs like any other.

The "before" column is the same simulator on the code and defs as they were before the balance pass (the
presets' rows always use the presets as they are now).
