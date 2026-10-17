# Rimfolk balance report

Written by the colony-year simulator in `Tools/Balance` (run it from the repository root with
`dotnet run -c Release --project Tools/Balance -- . --before Tools/Balance/before.json`). It plays a settled colony through one
60-day year (30 days of warm-up first, so standards and trust have settled) 120 times for each case, without the game:
the moods, wishes, dilemmas, events and ailments come from the def XML, the rates from the code (read from the source
where it can, see the last section), and the mood rules from the mod's own `GoodTimesMath`, `PressureMath`,
`SetbackMath`, `AilmentMath` and `ArcMath`, compiled in as they are. What it can't know (how often the storyteller
sends visitors, how a player plays) is in `Tools/Balance/Rates.cs`, one documented table. Treat the numbers as
a model's: good for comparing settings and changes, not a promise about one game.

**Rimfolk's mood** below is everything Rimfolk adds to a colonist's mood, per colonist, averaged over the year:
its good memories (after the shared budget and fading sooner in good times), its bad memories (setbacks, a lapsed
wish, frictions, a nightmare), and its steady moods (trust, the colony's ways, everyday pressures, ailments, a wish
still waiting, strain). The game's own moods aren't in it.

## Against the targets

The targets are in DESIGN.md ("Balance targets").

| | Target | 6 colonists | 12 colonists | 6 colonists before |
|---|---|---|---|---|
| Rimfolk's mood, well run | +3 to +6 | +3.2 | +2.7 | +2.9 |
| Rimfolk's mood, average | 0 to +3 | -0.2 | -0.2 | -0.3 |
| Rimfolk's mood, neglectful | -4 to -10 | -10.7 | -9.8 | -11.3 |
| Rimfolk's good moods at a high point (P99), well run | well short of the top | +14.9 | +14.0 | +15.0 |
| Time with +12 or more from Rimfolk's good moods, well run | rare | 9% | 5% | 10% |
| Questions (decision letters) per day, average | 1 every 2-3 days (0.33-0.5) | 0.49 | 0.51 | 0.53 |
| Questions and wishes voiced per day, average | (for reference) | 0.94 | 1.00 | 0.98 |
| Questions within 12 hours of another, average | few | 8% | 9% | 18% |
| Time with an everyday ailment, average | 10-15% | 17% | 16% | 24% |
| Time with an everyday ailment, well run | under 10-15% | 11% | 11% | 17% |

## Before and after the balance pass

The same simulator on the code and defs as they were before the pass, and as they are now (default settings).

| | 6 before | 6 after | 12 before | 12 after |
|---|---|---|---|---|
| Rimfolk's mood, well run | +2.9 | +3.2 | +2.5 | +2.7 |
|   good memories (raw), well run | +4.7 (+5.5) | +4.5 (+5.2) | +4.1 (+4.6) | +3.8 (+4.3) |
|   bad memories and steady bad, well run | -5.3 | -4.8 | -4.6 | -4.1 |
| Rimfolk's mood, average | -0.3 | -0.2 | -0.5 | -0.2 |
|   good memories (raw), average | +5.9 (+6.6) | +5.6 (+6.2) | +5.2 (+5.7) | +5.0 (+5.4) |
|   bad memories and steady bad, average | -8.1 | -7.6 | -7.4 | -6.9 |
| Rimfolk's mood, neglectful | -11.3 | -10.7 | -10.1 | -9.8 |
|   good memories (raw), neglectful | +4.1 (+4.4) | +4.0 (+4.3) | +3.7 (+3.9) | +3.5 (+3.7) |
|   bad memories and steady bad, neglectful | -16.0 | -15.3 | -14.4 | -13.9 |
| Rimfolk's good moods at a high point (P99), well run | +15.0 | +14.9 | +14.4 | +14.0 |
| Time with +12 or more from Rimfolk's good moods, well run | 10% | 9% | 6% | 5% |
| Trust, year's average, average play | +13 | +15 | +10 | +11 |
| Questions per day, average play | 0.53 | 0.49 | 0.56 | 0.51 |
| Questions within 12 hours of another, average play | 18% | 8% | 19% | 9% |
| Days with two or more questions, average play | 9% | 5% | 10% | 7% |
| Wishes voiced per day, average play | 0.45 | 0.46 | 0.50 | 0.49 |
| Letters that only tell, per day, average play | 0.41 | 0.40 | 0.49 | 0.49 |
| News lines per day (one evening letter), average play | 1.66 | 1.67 | 2.29 | 2.27 |
| Time with an everyday ailment, well run | 17% | 11% | 17% | 11% |
| Time with an everyday ailment, average | 24% | 17% | 23% | 16% |
| Time with an everyday ailment, neglectful | 44% | 39% | 44% | 37% |
| Ailment days per colonist per year, average play | 14.4 | 10.1 | 13.8 | 9.7 |

## Rimfolk's mood per colonist

Good memories are what they count for after the shared budget (the two biggest in full, then 50%, 35%, 25%, 20%)
and fading sooner above 75% mood; "raw" is the same memories without the budget. Steady moods are trust, the
colony's ways, pressures, ailments, waiting and strain. P10 and P90: a hard and a good moment for one colonist.

| Case | Good memories (raw) | Bad memories | Steady good | Steady bad | **Net** | P10 / P90 | Good moods on the mind | Mood | Standards |
|---|---|---|---|---|---|---|---|---|---|
| 6 colonists, well run | +4.5 (+5.2) | -2.9 | +3.5 | -1.9 | **+3.2** | -3.0 / +8.9 | 2.4 | 70% | 0.93 |
| 6 colonists, average | +5.6 (+6.2) | -3.7 | +1.9 | -3.9 | **-0.2** | -8.0 / +7.6 | 1.7 | 62% | 0.14 |
| 6 colonists, neglectful | +4.0 (+4.3) | -5.4 | +0.6 | -10.0 | **-10.7** | -19.0 / -2.0 | 1.2 | 35% | 0.00 |
| 12 colonists, well run | +3.8 (+4.3) | -2.5 | +3.0 | -1.6 | **+2.7** | -3.0 / +8.1 | 1.9 | 69% | 0.90 |
| 12 colonists, average | +5.0 (+5.4) | -3.1 | +1.7 | -3.8 | **-0.2** | -7.3 / +7.0 | 1.6 | 62% | 0.12 |
| 12 colonists, neglectful | +3.5 (+3.7) | -4.9 | +0.5 | -9.0 | **-9.8** | -18.0 / -2.0 | 1.1 | 36% | 0.00 |

Over the year (net, in five 10-day stretches plus one; good and bad in brackets), 6 colonists:

| Play | days 0-9 | days 10-19 | days 20-29 | days 30-39 | days 40-49 | days 50-59 |
|---|---|---|---|---|---|---|
| well run | +3.2 (+7.8 / -4.6) | +3.1 (+7.8 / -4.7) | +3.3 (+8.1 / -4.9) | +3.1 (+7.9 / -4.8) | +3.1 (+8.0 / -4.9) | +3.3 (+8.2 / -4.9) |
| average | -0.3 (+7.4 / -7.7) | +0.2 (+7.6 / -7.4) | +0.1 (+7.6 / -7.5) | -0.4 (+7.3 / -7.7) | -0.1 (+7.5 / -7.6) | -0.3 (+7.3 / -7.7) |
| neglectful | -9.8 (+4.7 / -14.5) | -10.3 (+4.6 / -14.9) | -10.5 (+4.8 / -15.3) | -10.9 (+4.5 / -15.4) | -11.1 (+4.7 / -15.8) | -11.8 (+4.4 / -16.2) |

Where it comes from (6 colonists): what each source adds to a colonist's mood on average. Good memories are
shown as they count after the budget.

| Source | well run | average | neglectful |
|---|---|---|---|
| pressures | +0.34 | -1.54 | -3.43 |
| trust | +1.48 | +0.44 | -2.10 |
| setbacks (bad) | -1.06 | -1.22 | -1.69 |
| setbacks (lasting) | -0.56 | -0.75 | -1.46 |
| frictions (bad) | -0.72 | -0.93 | -1.31 |
| meetings | +1.09 | +1.17 | +0.47 |
| strain | -0.06 | -0.35 | -1.16 |
| ailments | -0.29 | -0.39 | -1.15 |
| wishes | +1.09 | +0.84 | +0.26 |
| moments | +0.52 | +0.97 | +1.02 |
| life and honours | +0.71 | +0.99 | +0.79 |
| wishes (bad) | -0.31 | -0.48 | -0.81 |
| social events | +0.21 | +0.40 | +0.48 |
| dilemmas | +0.37 | +0.45 | +0.21 |
| the colony's ways | +0.42 | +0.16 | -0.34 |
| life goals | +0.36 | +0.40 | +0.26 |
| dreams (bad) | -0.18 | -0.19 | -0.36 |
| dilemmas (bad) | -0.16 | -0.23 | -0.35 |
| stories | +0.15 | +0.28 | +0.34 |
| meetings (bad) | -0.12 | -0.21 | -0.31 |
| nuisances (bad) | -0.05 | -0.16 | -0.28 |
| calendar | +0.08 | +0.16 | +0.20 |
| events | +0.13 | +0.19 | +0.19 |
| social events (bad) | -0.17 | -0.14 | -0.12 |
| encounters (bad) | -0.10 | -0.08 | -0.08 |
| encounters | +0.06 | +0.10 | +0.10 |
| health | -0.05 | -0.06 | -0.09 |
| world events | +0.01 | +0.06 | +0.02 |

What the shared budget and fading sooner take off (6 colonists):

| Play | Net with both rules | Net without either | Good memories with | without |
|---|---|---|---|---|
| well run | +3.2 | +3.9 | +4.5 | +5.2 |
| average | -0.2 | +0.3 | +5.6 | +6.2 |
| neglectful | -10.7 | -10.7 | +4.0 | +4.1 |

## Trust

Average trust in the council (-100 to 100; trusting from 25, devoted from 60, doubtful from -15, bitter from -45).

| Case | Start of year | End of year | Year's average | Trusting or devoted | Doubtful or bitter | By stretch |
|---|---|---|---|---|---|---|
| 6 colonists, well run | +35 | +73 | +58 | 89% | 0% | +40, +49, +57, +63, +67, +71 |
| 6 colonists, average | +9 | +18 | +15 | 29% | 5% | +10, +13, +15, +16, +17, +17 |
| 6 colonists, neglectful | -16 | -39 | -29 | 0% | 79% | -18, -23, -28, -31, -34, -38 |
| 12 colonists, well run | +22 | +47 | +37 | 69% | 0% | +25, +31, +36, +40, +43, +46 |
| 12 colonists, average | +7 | +14 | +11 | 18% | 3% | +8, +9, +10, +12, +13, +14 |
| 12 colonists, neglectful | -11 | -26 | -19 | 0% | 59% | -12, -15, -18, -21, -23, -25 |

## Letters and messages per day

Questions are choice letters (a dilemma, an event with answers, a calendar day, a meeting's vote, a mission
offered, a world event's question). Wishes are letters that ask without buttons. Letters only tell. Messages show
on screen as they happen; the evening's colony news gathers the rest into one letter a day. Flavour news (the
smaller encounters, moments and dreams) isn't shown at the default message level and isn't counted.

**6 colonists, average play**: 0.49 questions, 0.46 wishes voiced, 0.40 letters, 0.61 messages and 1.67 news lines a day.

| System | Questions | Wishes | Letters | Messages | News lines |
|---|---|---|---|---|---|
| wishes | 0.00 | 0.36 | 0.03 | 0.00 | 0.43 |
| event wishes | 0.00 | 0.10 | 0.00 | 0.00 | 0.00 |
| dilemmas | 0.10 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (after a raid) | 0.04 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (strangers) | 0.07 | 0.00 | 0.00 | 0.00 | 0.00 |
| events | 0.06 | 0.00 | 0.04 | 0.00 | 0.00 |
| encounters | 0.00 | 0.00 | 0.00 | 0.00 | 0.29 |
| social events | 0.00 | 0.00 | 0.00 | 0.00 | 0.31 |
| ailments | 0.00 | 0.00 | 0.04 | 0.00 | 0.12 |
| health | 0.00 | 0.00 | 0.01 | 0.00 | 0.11 |
| calendar | 0.10 | 0.00 | 0.03 | 0.00 | 0.00 |
| stories | 0.00 | 0.00 | 0.04 | 0.00 | 0.13 |
| camps | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| world events | 0.04 | 0.00 | 0.02 | 0.00 | 0.00 |
| missions | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| play-outs | 0.00 | 0.00 | 0.00 | 0.32 | 0.00 |
| meetings | 0.06 | 0.00 | 0.03 | 0.00 | 0.03 |
| decrees | 0.00 | 0.00 | 0.00 | 0.13 | 0.00 |
| dreams | 0.00 | 0.00 | 0.00 | 0.00 | 0.02 |
| people | 0.00 | 0.00 | 0.11 | 0.00 | 0.25 |
| colony | 0.00 | 0.00 | 0.04 | 0.17 | 0.00 |
| world | 0.00 | 0.00 | 0.01 | 0.00 | 0.00 |

**12 colonists, average play**: 0.51 questions, 0.49 wishes voiced, 0.49 letters, 0.65 messages and 2.27 news lines a day.

| System | Questions | Wishes | Letters | Messages | News lines |
|---|---|---|---|---|---|
| wishes | 0.00 | 0.31 | 0.03 | 0.00 | 0.46 |
| event wishes | 0.00 | 0.18 | 0.00 | 0.00 | 0.00 |
| dilemmas | 0.10 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (after a raid) | 0.04 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (strangers) | 0.08 | 0.00 | 0.00 | 0.00 | 0.00 |
| events | 0.05 | 0.00 | 0.04 | 0.00 | 0.00 |
| encounters | 0.00 | 0.00 | 0.00 | 0.00 | 0.29 |
| social events | 0.00 | 0.00 | 0.00 | 0.00 | 0.51 |
| ailments | 0.00 | 0.00 | 0.03 | 0.00 | 0.23 |
| health | 0.00 | 0.00 | 0.02 | 0.00 | 0.17 |
| calendar | 0.13 | 0.00 | 0.05 | 0.00 | 0.00 |
| stories | 0.00 | 0.00 | 0.07 | 0.00 | 0.20 |
| camps | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| world events | 0.04 | 0.00 | 0.02 | 0.00 | 0.00 |
| missions | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| play-outs | 0.00 | 0.00 | 0.00 | 0.35 | 0.00 |
| meetings | 0.06 | 0.00 | 0.03 | 0.00 | 0.03 |
| decrees | 0.00 | 0.00 | 0.00 | 0.13 | 0.00 |
| dreams | 0.00 | 0.00 | 0.00 | 0.00 | 0.03 |
| people | 0.00 | 0.00 | 0.15 | 0.00 | 0.35 |
| colony | 0.00 | 0.00 | 0.04 | 0.17 | 0.00 |
| world | 0.00 | 0.00 | 0.01 | 0.00 | 0.00 |

By play style (6 colonists):

| Play | Questions | Wishes | Letters | Messages | News lines | Questions within 12 h of another | Days with two or more questions |
|---|---|---|---|---|---|---|---|
| well run | 0.49 | 0.65 | 0.51 | 0.69 | 1.69 | 8% | 6% |
| average | 0.49 | 0.46 | 0.40 | 0.61 | 1.67 | 8% | 5% |
| neglectful | 0.47 | 0.53 | 0.41 | 0.54 | 1.96 | 9% | 6% |

## Ailments

Everyday ailments and what they leave behind (a lingering cough), per colonist over the year. "Not quiet" leaves
out the smallest (a stiff neck, hiccups, a smoke cough), which only a player who wants every message hears about.

| Case | Ailment days per colonist | Time with one | Time with one, not quiet | Caught, per colony | Circumstance, per colony | Serious, per colony |
|---|---|---|---|---|---|---|
| 6 colonists, well run | 6.9 | 11% | 9% | 2.2 | 38.0 | 0.2 |
| 6 colonists, average | 10.1 | 17% | 14% | 2.2 | 30.3 | 0.4 |
| 6 colonists, neglectful | 23.6 | 39% | 34% | 1.9 | 40.7 | 1.8 |
| 12 colonists, well run | 6.6 | 11% | 8% | 1.8 | 76.2 | 0.2 |
| 12 colonists, average | 9.7 | 16% | 13% | 1.8 | 61.7 | 1.0 |
| 12 colonists, neglectful | 22.6 | 37% | 33% | 1.9 | 80.9 | 3.4 |

What fills the time (days per colonist per year, 6 colonists):

| Ailment | well run | average | neglectful |
|---|---|---|---|
| Lice | 0.0 | 0.6 | 6.9 |
| RimrotCough | 0.0 | 0.6 | 3.4 |
| Convalescent | 0.3 | 0.6 | 3.0 |
| Insomnia | 0.3 | 0.8 | 2.4 |
| ChestCold | 0.8 | 0.9 | 0.9 |
| HeadCold | 0.9 | 0.8 | 0.9 |
| SmokeCough | 0.0 | 0.5 | 1.8 |
| StrainedBack | 0.4 | 0.7 | 0.8 |
| ColdSore | 0.2 | 0.4 | 1.1 |
| Heartburn | 1.1 | 0.5 | 0.1 |
| Toothache | 0.3 | 0.4 | 0.8 |
| Chilblains | 0.3 | 0.4 | 0.5 |
| SoreHands | 0.3 | 0.4 | 0.4 |
| Dreamfever | 0.3 | 0.5 | 0.3 |
| Splinter | 0.1 | 0.3 | 0.5 |
| LingeringCough | 0.3 | 0.3 | 0.3 |

## The presets

The style presets of the settings window, applied as the window applies them (6 colonists).

| Preset | Play | Net | Good memories | Bad + steady bad | Questions per day | Wishes per day | Time ill |
|---|---|---|---|---|---|---|---|
| default | well run | +3.2 | +4.5 | -4.8 | 0.49 | 0.65 | 11% |
| default | average | -0.2 | +5.6 | -7.6 | 0.49 | 0.46 | 17% |
| default | neglectful | -10.7 | +4.0 | -15.3 | 0.47 | 0.53 | 39% |
| lightTouch | well run | +5.4 | +3.5 | -1.0 | 0.44 | 0.34 | 6% |
| lightTouch | average | +3.9 | +4.7 | -2.5 | 0.45 | 0.26 | 9% |
| lightTouch | neglectful | -0.9 | +3.7 | -5.1 | 0.43 | 0.37 | 20% |
| storyRich | well run | +3.3 | +4.8 | -5.0 | 0.57 | 0.64 | 11% |
| storyRich | average | +0.2 | +6.3 | -8.0 | 0.56 | 0.44 | 17% |
| storyRich | neglectful | -10.7 | +4.6 | -15.8 | 0.53 | 0.52 | 39% |
| demanding | well run | +0.5 | +3.9 | -6.6 | 0.49 | 0.69 | 12% |
| demanding | average | -4.0 | +4.9 | -10.6 | 0.48 | 0.50 | 16% |
| demanding | neglectful | -17.7 | +3.3 | -21.5 | 0.46 | 0.65 | 40% |

## How often things happen (per colony per year)

| What | 6, well run | 6, average | 6, neglectful | 12, well run | 12, average | 12, neglectful |
|---|---|---|---|---|---|---|
| after-effects | 0.8 | 0.9 | 2.3 | 1.4 | 2.0 | 4.3 |
| ailments | 38.0 | 30.3 | 40.7 | 76.2 | 61.7 | 80.9 |
| ambitions reached | 3.0 | 1.9 | 0.7 | 2.8 | 1.9 | 0.6 |
| calendar days | 8.0 | 8.0 | 8.0 | 11.0 | 11.0 | 11.0 |
| camps | 0.0 | 0.1 | 0.0 | 0.0 | 0.0 | 0.0 |
| catching ailments | 2.2 | 2.2 | 1.9 | 1.8 | 1.8 | 1.9 |
| convalescence | 2.4 | 3.2 | 4.8 | 4.8 | 7.0 | 10.0 |
| decree requests | 7.8 | 7.6 | 7.6 | 7.4 | 7.7 | 7.7 |
| dilemmas | 13.4 | 12.7 | 12.0 | 13.5 | 13.0 | 11.8 |
| dreams | 106.8 | 107.4 | 107.3 | 213.9 | 215.5 | 214.0 |
| encounters | 54.0 | 52.5 | 47.6 | 53.7 | 53.0 | 48.3 |
| events | 5.9 | 6.2 | 6.2 | 5.9 | 5.7 | 5.8 |
| friction moments | 212.4 | 275.6 | 374.5 | 425.3 | 553.0 | 744.4 |
| meetings | 3.8 | 3.8 | 3.7 | 3.8 | 3.7 | 3.8 |
| missions | 0.2 | 0.2 | 0.3 | 0.2 | 0.2 | 0.2 |
| moments | 696.5 | 648.0 | 573.9 | 1389.2 | 1293.6 | 1150.6 |
| nuisances | 27.0 | 81.8 | 141.2 | 53.7 | 166.2 | 282.7 |
| personal touches | 10.0 | 10.1 | 10.0 | 10.1 | 10.0 | 9.9 |
| play-outs | 6.8 | 5.0 | 2.7 | 7.0 | 5.3 | 2.6 |
| serious illnesses | 0.2 | 0.4 | 1.8 | 0.2 | 1.0 | 3.4 |
| social events | 71.4 | 71.6 | 71.5 | 117.4 | 117.8 | 117.6 |
| stories | 3.3 | 3.3 | 3.3 | 5.1 | 5.2 | 5.1 |
| wishes granted | 32.2 | 14.7 | 4.2 | 33.5 | 15.5 | 5.0 |
| wishes lapsed | 6.5 | 12.7 | 27.5 | 6.6 | 13.9 | 34.1 |
| wishes made | 39.1 | 27.5 | 31.7 | 40.4 | 29.5 | 39.2 |
| world events | 1.1 | 1.1 | 1.1 | 1.0 | 1.1 | 1.1 |

## What the model reads and assumes

From the defs: 1559 moods, 162 dilemmas, 184 events and missions, 131 encounters,
96 social events, 105 moments, 161 dreams, 22 nuisances, 101 wishes,
49 ailments and illnesses, 13 catching ones, 40 world events (510 defs for other mods left out).

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
| days between catching ailments (a map) | `Ailments/AilmentTracker.cs` | 30 | yes |
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
| The same meal again (stage 1 / 2 / 3) | 12% / 0% / 0% | 32% / 13% / 0% | 45% / 20% / 0% |
| The same work again (stage 1 / 2 / 3) | 13% / 5% / 0% | 25% / 10% / 5% | 30% / 20% / 10% |
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
