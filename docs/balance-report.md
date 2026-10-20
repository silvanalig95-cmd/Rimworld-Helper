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

| | Target | 6 colonists | 12 colonists |
|---|---|---|---|
| Rimfolk's mood, well run | +3 to +6 | +3.5 | +3.0 |
| Rimfolk's mood, average | 0 to +3 | +0.7 | +0.2 |
| Rimfolk's mood, neglectful | -4 to -10 | -8.9 | -8.0 |
| Rimfolk's good moods at a high point (P99), well run | well short of the top | +15.1 | +14.2 |
| Time with +12 or more from Rimfolk's good moods, well run | rare | 9% | 5% |
| Questions (decision letters) per day, average | 1 every 2-3 days (0.33-0.5) | 0.49 | 0.52 |
| Questions and wishes voiced per day, average | (for reference) | 0.93 | 1.00 |
| Questions within 12 hours of another, average | few | 8% | 9% |
| Time with an everyday ailment, average | 10-15% | 17% | 18% |
| Time with an everyday ailment, well run | under 10-15% | 11% | 11% |

## Rimfolk's mood per colonist

Good memories are what they count for after the shared budget (the two biggest in full, then 60%, 45%, 35%, 25%)
and fading sooner above 75% mood; "raw" is the same memories without the budget. Steady moods are trust, the
colony's ways, pressures, ailments, waiting and strain. P10 and P90: a hard and a good moment for one colonist.

| Case | Good memories (raw) | Bad memories | Steady good | Steady bad | **Net** | P10 / P90 | Good moods on the mind | Mood | Standards |
|---|---|---|---|---|---|---|---|---|---|
| 6 colonists, well run | +4.4 (+5.0) | -3.0 | +3.5 | -1.5 | **+3.5** | -2.6 / +9.2 | 2.3 | 70% | 0.93 |
| 6 colonists, average | +5.7 (+6.1) | -3.5 | +1.9 | -3.3 | **+0.7** | -6.9 / +8.4 | 1.7 | 63% | 0.16 |
| 6 colonists, neglectful | +3.9 (+4.1) | -4.6 | +0.6 | -8.8 | **-8.9** | -16.9 / -1.0 | 1.2 | 37% | 0.00 |
| 12 colonists, well run | +3.8 (+4.2) | -2.6 | +3.0 | -1.2 | **+3.0** | -2.6 / +8.4 | 1.9 | 69% | 0.91 |
| 12 colonists, average | +4.9 (+5.2) | -3.0 | +1.6 | -3.2 | **+0.2** | -6.8 / +7.4 | 1.5 | 62% | 0.13 |
| 12 colonists, neglectful | +3.5 (+3.7) | -4.1 | +0.5 | -8.0 | **-8.0** | -15.4 / -0.7 | 1.1 | 38% | 0.00 |

Over the year (net, in five 10-day stretches plus one; good and bad in brackets), 6 colonists:

| Play | days 0-9 | days 10-19 | days 20-29 | days 30-39 | days 40-49 | days 50-59 |
|---|---|---|---|---|---|---|
| well run | +3.4 (+7.7 / -4.3) | +3.3 (+7.8 / -4.5) | +3.6 (+8.0 / -4.4) | +3.5 (+7.9 / -4.4) | +3.4 (+8.0 / -4.5) | +3.7 (+8.2 / -4.4) |
| average | +0.5 (+7.4 / -6.9) | +0.9 (+7.7 / -6.8) | +1.0 (+7.8 / -6.7) | +0.6 (+7.4 / -6.8) | +0.5 (+7.4 / -6.9) | +0.6 (+7.5 / -6.9) |
| neglectful | -7.9 (+4.7 / -12.5) | -8.2 (+4.7 / -12.9) | -8.9 (+4.5 / -13.4) | -9.1 (+4.5 / -13.5) | -9.4 (+4.4 / -13.8) | -9.8 (+4.5 / -14.2) |

Where it comes from (6 colonists): what each source adds to a colonist's mood on average. Good memories are
shown as they count after the budget.

| Source | well run | average | neglectful |
|---|---|---|---|
| pressures | +0.68 | -0.95 | -2.38 |
| trust | +1.45 | +0.42 | -2.07 |
| setbacks (bad) | -0.97 | -1.12 | -1.56 |
| setbacks (lasting) | -0.52 | -0.72 | -1.44 |
| meetings | +1.05 | +1.17 | +0.35 |
| strain | -0.06 | -0.35 | -1.17 |
| ailments | -0.24 | -0.40 | -1.08 |
| wishes | +1.06 | +0.83 | +0.25 |
| moments | +0.55 | +0.99 | +1.04 |
| life and honours | +0.72 | +0.99 | +0.80 |
| frictions (bad) | -0.79 | -0.81 | -0.68 |
| wishes (bad) | -0.30 | -0.49 | -0.80 |
| dilemmas | +0.37 | +0.50 | +0.21 |
| social events | +0.22 | +0.40 | +0.49 |
| life goals | +0.33 | +0.43 | +0.26 |
| the colony's ways | +0.42 | +0.16 | -0.34 |
| dilemmas (bad) | -0.19 | -0.24 | -0.35 |
| stories | +0.16 | +0.29 | +0.34 |
| dreams (bad) | -0.19 | -0.19 | -0.32 |
| meetings (bad) | -0.12 | -0.20 | -0.32 |
| nuisances (bad) | -0.05 | -0.16 | -0.28 |
| events | +0.13 | +0.21 | +0.18 |
| calendar | +0.09 | +0.17 | +0.21 |
| social events (bad) | -0.17 | -0.13 | -0.12 |
| health | -0.05 | -0.08 | -0.11 |
| encounters | +0.06 | +0.10 | +0.10 |
| encounters (bad) | -0.10 | -0.08 | -0.08 |

What the shared budget and fading sooner take off (6 colonists):

| Play | Net with both rules | Net without either | Good memories with | without |
|---|---|---|---|---|
| well run | +3.5 | +4.1 | +4.4 | +5.1 |
| average | +0.7 | +1.1 | +5.7 | +6.0 |
| neglectful | -8.9 | -8.6 | +3.9 | +4.2 |

## Trust

Average trust in the council (-100 to 100; trusting from 25, devoted from 60, doubtful from -15, bitter from -45).

| Case | Start of year | End of year | Year's average | Trusting or devoted | Doubtful or bitter | By stretch |
|---|---|---|---|---|---|---|
| 6 colonists, well run | +35 | +71 | +57 | 88% | 0% | +40, +48, +56, +62, +66, +70 |
| 6 colonists, average | +9 | +18 | +14 | 30% | 7% | +9, +12, +14, +15, +17, +17 |
| 6 colonists, neglectful | -15 | -40 | -28 | 0% | 78% | -17, -22, -27, -31, -35, -38 |
| 12 colonists, well run | +22 | +47 | +37 | 70% | 0% | +25, +31, +36, +40, +43, +46 |
| 12 colonists, average | +8 | +12 | +11 | 16% | 3% | +8, +10, +11, +11, +11, +12 |
| 12 colonists, neglectful | -10 | -26 | -19 | 0% | 58% | -12, -16, -18, -20, -23, -25 |

## Letters and messages per day

Questions are choice letters (a dilemma, an event with answers, a calendar day, a meeting's vote, a mission
offered, a world event's question). Wishes are letters that ask without buttons. Letters only tell. Messages show
on screen as they happen; the evening's colony news gathers the rest into one letter a day. Flavour news (the
smaller encounters, moments and dreams) isn't shown at the default message level and isn't counted.

**6 colonists, average play**: 0.49 questions, 0.45 wishes voiced, 0.42 letters, 0.63 messages and 1.69 news lines a day.

| System | Questions | Wishes | Letters | Messages | News lines |
|---|---|---|---|---|---|
| wishes | 0.00 | 0.35 | 0.03 | 0.00 | 0.42 |
| event wishes | 0.00 | 0.10 | 0.00 | 0.00 | 0.00 |
| dilemmas | 0.10 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (after a raid) | 0.04 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (strangers) | 0.08 | 0.00 | 0.00 | 0.00 | 0.00 |
| events | 0.06 | 0.00 | 0.04 | 0.00 | 0.00 |
| encounters | 0.00 | 0.00 | 0.00 | 0.00 | 0.29 |
| social events | 0.00 | 0.00 | 0.00 | 0.00 | 0.31 |
| ailments | 0.00 | 0.00 | 0.03 | 0.00 | 0.15 |
| health | 0.00 | 0.00 | 0.01 | 0.00 | 0.11 |
| calendar | 0.10 | 0.00 | 0.03 | 0.00 | 0.00 |
| stories | 0.00 | 0.00 | 0.05 | 0.00 | 0.12 |
| camps | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| world events | 0.04 | 0.00 | 0.02 | 0.00 | 0.00 |
| missions | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| play-outs | 0.00 | 0.00 | 0.00 | 0.34 | 0.00 |
| meetings | 0.06 | 0.00 | 0.03 | 0.00 | 0.03 |
| decrees | 0.00 | 0.00 | 0.00 | 0.13 | 0.00 |
| dreams | 0.00 | 0.00 | 0.00 | 0.00 | 0.01 |
| people | 0.00 | 0.00 | 0.12 | 0.00 | 0.25 |
| colony | 0.00 | 0.00 | 0.04 | 0.17 | 0.00 |
| world | 0.00 | 0.00 | 0.01 | 0.00 | 0.00 |

**12 colonists, average play**: 0.52 questions, 0.49 wishes voiced, 0.48 letters, 0.66 messages and 2.32 news lines a day.

| System | Questions | Wishes | Letters | Messages | News lines |
|---|---|---|---|---|---|
| wishes | 0.00 | 0.31 | 0.03 | 0.00 | 0.45 |
| event wishes | 0.00 | 0.18 | 0.00 | 0.00 | 0.00 |
| dilemmas | 0.10 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (after a raid) | 0.04 | 0.00 | 0.00 | 0.00 | 0.00 |
| dilemmas (strangers) | 0.07 | 0.00 | 0.00 | 0.00 | 0.00 |
| events | 0.06 | 0.00 | 0.04 | 0.00 | 0.00 |
| encounters | 0.00 | 0.00 | 0.00 | 0.00 | 0.28 |
| social events | 0.00 | 0.00 | 0.00 | 0.00 | 0.50 |
| ailments | 0.00 | 0.00 | 0.03 | 0.00 | 0.30 |
| health | 0.00 | 0.00 | 0.02 | 0.00 | 0.17 |
| calendar | 0.13 | 0.00 | 0.05 | 0.00 | 0.00 |
| stories | 0.00 | 0.00 | 0.07 | 0.00 | 0.19 |
| camps | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| world events | 0.04 | 0.00 | 0.02 | 0.00 | 0.00 |
| missions | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| play-outs | 0.00 | 0.00 | 0.00 | 0.36 | 0.00 |
| meetings | 0.06 | 0.00 | 0.03 | 0.00 | 0.03 |
| decrees | 0.00 | 0.00 | 0.00 | 0.13 | 0.00 |
| dreams | 0.00 | 0.00 | 0.00 | 0.00 | 0.03 |
| people | 0.00 | 0.00 | 0.14 | 0.00 | 0.35 |
| colony | 0.00 | 0.00 | 0.04 | 0.17 | 0.00 |
| world | 0.00 | 0.00 | 0.01 | 0.00 | 0.00 |

By play style (6 colonists):

| Play | Questions | Wishes | Letters | Messages | News lines | Questions within 12 h of another | Days with two or more questions |
|---|---|---|---|---|---|---|---|
| well run | 0.49 | 0.64 | 0.48 | 0.69 | 1.72 | 7% | 6% |
| average | 0.49 | 0.45 | 0.42 | 0.63 | 1.69 | 8% | 5% |
| neglectful | 0.47 | 0.53 | 0.39 | 0.57 | 1.98 | 8% | 5% |

## Ailments

Everyday ailments and what they leave behind (a lingering cough), per colonist over the year. "Not quiet" leaves
out the smallest (a stiff neck, hiccups, a smoke cough), which only a player who wants every message hears about.

| Case | Ailment days per colonist | Time with one | Time with one, not quiet | Caught, per colony | Circumstance, per colony | Serious, per colony |
|---|---|---|---|---|---|---|
| 6 colonists, well run | 7.0 | 11% | 9% | 1.7 | 37.3 | 0.2 |
| 6 colonists, average | 10.4 | 17% | 14% | 1.7 | 31.7 | 0.6 |
| 6 colonists, neglectful | 23.2 | 38% | 34% | 1.5 | 40.8 | 1.9 |
| 12 colonists, well run | 6.9 | 11% | 9% | 1.8 | 75.4 | 0.3 |
| 12 colonists, average | 10.7 | 18% | 15% | 1.8 | 62.9 | 0.9 |
| 12 colonists, neglectful | 22.7 | 38% | 33% | 1.7 | 82.5 | 3.4 |

What fills the time (days per colonist per year, 6 colonists):

| Ailment | well run | average | neglectful |
|---|---|---|---|
| Lice | 0.0 | 0.6 | 6.7 |
| Convalescent | 0.3 | 0.9 | 3.0 |
| RimrotCough | 0.0 | 0.5 | 3.4 |
| Insomnia | 0.3 | 0.7 | 1.7 |
| SmokeCough | 0.0 | 0.5 | 1.8 |
| ChestCold | 0.8 | 0.7 | 0.6 |
| StrainedBack | 0.5 | 0.7 | 0.8 |
| HeadCold | 0.7 | 0.6 | 0.7 |
| ColdSore | 0.2 | 0.3 | 1.1 |
| Heartburn | 0.9 | 0.4 | 0.1 |
| Toothache | 0.2 | 0.4 | 0.7 |
| Splinter | 0.1 | 0.4 | 0.6 |
| Chilblains | 0.3 | 0.3 | 0.4 |
| SoreHands | 0.3 | 0.3 | 0.4 |
| PlantRash | 0.3 | 0.3 | 0.3 |
| Typhus | 0.0 | 0.1 | 0.7 |

## The presets

The style presets of the settings window, applied as the window applies them (6 colonists).

| Preset | Play | Net | Good memories | Bad + steady bad | Questions per day | Wishes per day | Time ill |
|---|---|---|---|---|---|---|---|
| default | well run | +3.5 | +4.4 | -4.4 | 0.49 | 0.64 | 11% |
| default | average | +0.7 | +5.7 | -6.8 | 0.49 | 0.45 | 17% |
| default | neglectful | -8.9 | +3.9 | -13.4 | 0.47 | 0.53 | 38% |
| lightTouch | well run | +5.6 | +3.6 | -0.9 | 0.44 | 0.35 | 6% |
| lightTouch | average | +4.0 | +4.7 | -2.3 | 0.45 | 0.27 | 9% |
| lightTouch | neglectful | -0.7 | +3.8 | -5.1 | 0.43 | 0.36 | 20% |
| storyRich | well run | +3.7 | +4.8 | -4.7 | 0.58 | 0.64 | 10% |
| storyRich | average | +0.9 | +6.1 | -7.1 | 0.57 | 0.45 | 17% |
| storyRich | neglectful | -8.7 | +4.6 | -13.8 | 0.53 | 0.52 | 38% |
| demanding | well run | +0.7 | +4.0 | -6.5 | 0.50 | 0.70 | 11% |
| demanding | average | -3.5 | +4.8 | -10.0 | 0.49 | 0.51 | 16% |
| demanding | neglectful | -14.8 | +3.2 | -18.5 | 0.47 | 0.65 | 39% |

## How often things happen (per colony per year)

| What | 6, well run | 6, average | 6, neglectful | 12, well run | 12, average | 12, neglectful |
|---|---|---|---|---|---|---|
| after-effects | 0.6 | 0.9 | 2.2 | 0.9 | 1.5 | 4.2 |
| ailments | 37.3 | 31.7 | 40.8 | 75.4 | 62.9 | 82.5 |
| ambitions reached | 2.9 | 1.8 | 0.5 | 2.8 | 1.8 | 0.5 |
| calendar days | 8.0 | 8.0 | 8.0 | 11.0 | 11.0 | 11.0 |
| camps | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 |
| catching ailments | 1.7 | 1.7 | 1.5 | 1.8 | 1.8 | 1.7 |
| convalescence | 2.6 | 3.9 | 5.8 | 5.3 | 7.5 | 12.0 |
| decree requests | 7.8 | 7.9 | 7.9 | 7.9 | 7.9 | 7.8 |
| dilemmas | 13.5 | 13.1 | 11.9 | 13.3 | 13.0 | 11.8 |
| dreams | 107.6 | 107.9 | 109.5 | 214.7 | 214.8 | 216.7 |
| encounters | 54.8 | 52.3 | 48.4 | 53.8 | 51.4 | 48.2 |
| events | 5.8 | 6.1 | 5.9 | 5.9 | 6.1 | 5.9 |
| friction moments | 203.1 | 267.0 | 361.2 | 405.3 | 534.1 | 716.5 |
| meetings | 3.7 | 3.8 | 3.7 | 3.8 | 3.8 | 3.8 |
| missions | 0.3 | 0.2 | 0.2 | 0.3 | 0.3 | 0.1 |
| moments | 711.9 | 662.4 | 590.9 | 1423.7 | 1321.1 | 1184.1 |
| nuisances | 26.0 | 83.3 | 141.4 | 52.6 | 166.1 | 282.1 |
| personal touches | 10.0 | 10.0 | 10.0 | 10.0 | 10.1 | 10.2 |
| play-outs | 7.1 | 5.6 | 3.5 | 7.4 | 5.7 | 3.6 |
| serious illnesses | 0.2 | 0.6 | 1.9 | 0.3 | 0.9 | 3.4 |
| social events | 71.2 | 71.3 | 71.2 | 117.3 | 117.7 | 117.6 |
| stories | 3.3 | 3.3 | 3.3 | 5.2 | 5.1 | 5.3 |
| wishes granted | 31.3 | 14.4 | 4.2 | 33.5 | 15.2 | 5.4 |
| wishes lapsed | 6.6 | 12.6 | 27.5 | 6.7 | 14.1 | 33.9 |
| wishes made | 38.2 | 27.0 | 31.8 | 40.3 | 29.2 | 39.3 |
| world events | 1.1 | 1.0 | 1.1 | 1.1 | 1.0 | 1.0 |

## What the model reads and assumes

From the defs: 1614 moods, 163 dilemmas, 185 events and missions, 134 encounters,
97 social events, 131 moments, 181 dreams, 22 nuisances, 105 wishes,
58 ailments and illnesses, 13 catching ones, 40 world events (515 defs for other mods left out).

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
| days between catching ailments (a map) | `Ailments/AilmentTracker.cs` | 35 | yes |
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
