# Dilemma balance

Written by `python3 Tools/dilemma_balance.py` (do not edit by hand). How the answers are scored:

```
Reads every DilemmaDef in Defs/ (with the ThoughtDefs, HediffDefs and ThingDefs they use, Name/ParentName inheritance
as the game does it) and scores each answer on comparable scales, in "points": one point is about one colonist feeling
+1 mood for one day. The conversions are rough on purpose and the same for every dilemma, so answers can be compared
with each other; the numbers are not a prediction of a real game.

  mood       a memory's mood x its days x the people it reaches (a colony of 8: A and B are 1 each, "everyone" 8,
             "everyone else" 6, A's friends 2, those who dislike A 1, A's family 1.5, those close to A 3; a trait filter
             keeps about 1 in 10 per trait listed, a degree filter about 1 in 8; an effect's chance multiplies). Higher
             standards count as -0.6 per point of standards (0.05 = -3) per person: later moods are smaller.
  trust      1.5 points per point of trust per person (it decays slowly and colours every later wish and answer).
  relations  an opinion offset x its days x people x 0.1; a bond in the social web 0.2 per point.
  wellbeing  recreation 1 point per 10% of the bar, strain 0.3 per point eased, an inspiration 8, an illness eased
             0.3 per 1% of severity, a day off +3 (recreation) per person.
  work       a work speed change x its days x 16 waking hours x 0.5 a colonist-hour (so +10% for 3 days = 2.4); a
             faster rest fall 4 per day per 100%; a raised break threshold 0.25 per 1% per day; a mental break -20; a
             fight -15; a day off -6 per person; a work party +3.
  skill      1 point per 100 XP; a mentorship 5; a lesson its XP an hour for 2 hours.
  goods      1 point per 10 silver of market value (silver itself, raw food 1.3 a unit, wood 1.2, medicine 25: more than
             its price, since the last doses can save a life; the rest from the def's MarketValue or the base game's);
             feast meals 0.5 each (the finest are served).
  people     someone joining the colony +60, someone leaving it -60; a prisoner set for recruiting +15 (an animal taken
             in counts as goods, 15).
  filters    an effect for the colony's faithful 0.6 of the group, those outside it 0.4, a faith's precept 0.25, the sick
             0.3, the children 0.25.
  outside    goodwill 1 per point; a camp's tension -0.5 per point raised; a raid later -40.
  ways       the colony's ways (mercy, generosity, tradition, openness, toil) are flavour: listed, never scored.
  play-outs  gatherings, visits, walks, vigils and the like are the words made real; they cost the colony's time (work
             -0.25 a colonist-hour: half a working hour, since evenings overlap recreation) and what they really take or
             give (meals, wood, finds).

Each answer gets benefit (the sum of what it gives), cost (the sum of what it takes) and risk (the expected loss of its
gamble: chance x how much worse the risky outcome is than the plain one), and net = benefit - cost - risk.

Verdicts:
  dominated  another answer of the same dilemma is at least as good on every scale (mood, trust, relations, wellbeing,
             work, skill, goods, people, outside, risk) and better on one, by more than 1 point.
  easy       the best answer's net is ahead of the second best by at least 10 points and half the second's size, with
             no more risk than the others: the choice makes itself.
An answer that needs something (a skill, goods in the stores) is marked "gated": it can't always be given. A dilemma
with an effect the scales don't measure (a faith's certainty, a guardian named, a wedding planned) is "partly scored":
it gets no verdict, and is read by hand.
```

208 dilemmas, 708 answers. Flagged: 58 dilemmas, 86 answers (first pass: 58 dilemmas; second pass: 0).

## First pass: the dilemmas rebalanced in this round

### Council_Dilemmas.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| a thief in the stores (`Council_Dilemma_Thief`) | 1 | Let A off with a warning | 29 | 38 | 0 | -9 | mood -12, trust +3 | trade-off |
|  | 2 | Make A work it off | 8 | 22 | 0 | -13 | mood -20, trust +7 | trade-off |
|  | 3 | Punish A publicly | 50 | 94 | 3 (25%) | -47 | mood -32, trust +12, relations -22, work -5 | trade-off |
| a forgotten stash (`Council_Dilemma_Stash`) | 1 | Share it out tonight | 27 | 12 | 0 | 15 | mood +1, work -5, goods +19 | trade-off |
|  | 2 | Lock it away for trading | 22 | 6 | 0 | 16 | mood -6, trust +3, goods +19 | trade-off |
|  | 3 | Destroy it | 5 | 15 | 0 | -10 | mood -15, trust +5 | trade-off |
| a feud (`Council_Dilemma_Feud`) | 1 | Side with A | 32 | 68 | 4 (30%) | -40 | mood -10, trust -3, relations -22, work -4 | trade-off |
|  | 2 | Side with B | 32 | 68 | 4 (30%) | -40 | mood -10, trust -3, relations -22, work -4 | trade-off |
|  | 3 | Make them work side by side | 48 | 18 | 25 (40%) | 5 | mood -18, relations +27, work -4 | trade-off |
|  | 4 | Stay out of it | 0 | 33 | 8 (50%) | -40 | mood -24, trust -9, work -8 | trade-off |
| the cook's feast (`Council_Dilemma_Feast`) | 1 | Let A cook the feast | 107 | 52 | 0 | 55 | mood +58, trust +9, wellbeing +2, work -6, goods -8 | trade-off |
|  | 2 | Save the food | 0 | 18 | 0 | -18 | mood -12, trust -6 | trade-off |
|  | 3 | A smaller feast | 51 | 9 | 0 | 42 | mood +48, trust +3, work -4, goods -6 | trade-off |
| a confession (`Council_Dilemma_Confession`) | 1 | Give A a few easy days | 56 | 13 | 0 | 43 | mood +30, trust +12, wellbeing +9, work -8 | trade-off |
|  | 2 | Tell A to get back to work | 1 | 49 | 2 (10%) | -50 | mood -35, trust -11, wellbeing -2, work -2 | trade-off |
|  | 3 | Ask B to look after A | 62 | 4 | 0 | 58 | mood +35, trust +8, relations +16, wellbeing +4, work -4 | trade-off |

### Council_Dilemmas2.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| a gambling debt (`Council_Dilemma_Wager`) | 1 | Make B pay up | 19 | 26 | 3 (20%) | -10 | mood -8, trust +1, work -3 | trade-off |
|  | 2 | Call the bet off | 26 | 72 | 0 | -46 | mood -17, trust -6, relations -22 | trade-off |
|  | 3 | Ban gambling in the colony | 15 | 66 | 0 | -51 | mood -44, trust -7 | trade-off |
|  | 4 | Double or nothing | 15 | 69 | 0 (50%) | -54 | mood -40, trust -7, wellbeing +3, work -3 | trade-off |
| the hoarder (`Council_Dilemma_Hoarder`) | 1 | Confiscate the lot | 43 | 47 | 5 (25%) | -9 | mood -12, trust +2, work -5, goods +6 | trade-off |
|  | 2 | Let A keep it | 24 | 37 | 0 | -13 | mood -13 | trade-off |
|  | 3 | Return half to the stores | 3 | 24 | 0 | -21 | mood -19, trust -4, goods +3 | trade-off |
| a bad omen (`Council_Dilemma_Omen`) | 1 | Double the watch | 32 | 36 | 0 | -4 | mood -7, trust +12, work -9 | trade-off |
|  | 2 | Laugh it off at dinner | 2 | 63 | 5 (25%) | -66 | mood -52, trust -9, wellbeing +2, work -7 | trade-off |
|  | 3 | Let A hold a ceremony | 87 | 17 | 5 (25%) | 65 | mood +63, trust +9, work -2, goods -5 | trade-off |
|  | 4 | Tell A to keep it quiet | 0 | 29 | 0 | -29 | mood -23, trust -6 | trade-off |
| a debt to old friends (`Council_Dilemma_OldFriends`) | 1 | Send them 100 silver | 50 | 15 | 11 (15%) | 23 | mood +16, trust +9, goods -10, outside +8 | trade-off |
|  | 2 | Send 25 silver as a token | 6 | 8 | 0 | -2 | mood -6, trust +3, goods -2, outside +3 | trade-off |
|  | 3 | Send them food instead | 44 | 17 | 0 | 27 | mood +19, trust +8, goods -6, outside +7 | trade-off |
|  | 4 | We need it more than they do | 1 | 46 | 0 | -45 | mood -37, trust -8 | trade-off |
| a broken keepsake (`Council_Dilemma_Keepsake`) | 1 | B apologizes publicly | 28 | 58 | 17 (25%) | -47 | mood -52, trust +1, relations +6, work -2 | trade-off |
|  | 2 | Pay A 50 silver | 46 | 62 | 0 | -15 | mood -21, trust +10, goods -5 | trade-off |
|  | 3 | Have B make a new one | 52 | 4 | 37 (35%) | 11 | mood -19, trust -2, relations +29, goods +3 | trade-off |
|  | 4 | Call it an accident | 0 | 71 | 0 | -71 | mood -52, trust -8, relations -11 | trade-off |
| asleep on watch (`Council_Dilemma_Watch`) | 1 | Punish B with extra shifts | 39 | 45 | 2 (15%) | -9 | mood -7, trust +7, relations -9, work +1 | trade-off |
|  | 2 | Warn B privately | 26 | 18 | 19 (25%) | -11 | mood -4, trust -7 | trade-off |
|  | 3 | Put A in charge of the watch | 39 | 67 | 0 | -28 | mood -5, trust +9, relations -22, work -9 | trade-off |
|  | 4 | Let it go this time | 0 | 30 | 4 (30%) | -34 | mood -19, trust -8, work -8 | trade-off |
| an unflattering likeness (`Council_Dilemma_Sculpture`) | 1 | Smash it | 28 | 67 | 4 (20%) | -43 | mood -10, trust -2, relations -22, work -4, goods -5 | trade-off |
|  | 2 | Keep it on display | 31 | 75 | 3 (20%) | -46 | mood -23, trust -3, relations -22, wellbeing +5, work -3 | trade-off |
|  | 3 | Make A carve an apology piece | 46 | 4 | 28 (30%) | 13 | relations +9, wellbeing +1, goods +3 | trade-off |
| music at midnight (`Council_Dilemma_Music`) | 1 | Ban the night music | 29 | 37 | 0 | -8 | mood -5, trust -3 | trade-off |
|  | 2 | Set quiet hours after midnight | 9 | 12 | 8 (20%) | -11 | mood -12, trust +6, relations -4, work -1 | trade-off |
|  | 3 | Tell B to live with it | 28 | 70 | 4 (20%) | -46 | mood -10, trust -3, relations -22, wellbeing +2, work -12 | trade-off |
| a duel of honor (`Council_Dilemma_Duel`) | 1 | Allow the duel | 47 | 22 | 0 | 25 | mood +14, trust +6, relations +20, work -15 | trade-off |
|  | 2 | Forbid it | 1 | 44 | 5 (20%) | -48 | mood -32, trust -13, work -3 | trade-off |
|  | 3 | Make them shake hands in public | 45 | 20 | 25 (45%) | 0 | mood -18, relations +25, wellbeing +1, work -8 | trade-off |
|  | 4 | Settle it with a hauling race | 23 | 0 | 7 (45%) | 16 | relations +18, work -2 | trade-off |
| a seat at the table (`Council_Dilemma_Seat`) | 1 | Grant A a title | 96 | 48 | 14 (30%) | 33 | mood +18, trust +15, work +1 | trade-off |
|  | 2 | Refuse the request | 1 | 49 | 0 | -48 | mood -36, trust -11, work -1 | trade-off |
|  | 3 | Put A in charge of the storeroom | 38 | 24 | 11 (20%) | 2 | mood -2, trust +2, work +2 | trade-off |
|  | 4 | Let A sit in on one meeting | 8 | 4 | 6 (35%) | -1 | mood -9, trust +7 | trade-off |
| not pulling weight (`Council_Dilemma_Slacker`) | 1 | Side with A: double shifts for B | 30 | 45 | 8 (20%) | -23 | mood -16, trust +1, relations -4, work -3 | trade-off |
|  | 2 | Let B keep B own pace | 29 | 71 | 0 | -43 | mood -17, trust -3, relations -22 | trade-off |
|  | 3 | Pair them up on every shift | 7 | 18 | 19 (40%) | -30 | mood -23, trust -1, relations -9, work +2 | trade-off |
|  | 4 | Stay out of it | 0 | 18 | 0 (40%) | -18 | mood -12, trust -6 | trade-off |
| an old face (`Council_Dilemma_OldFace`) | 1 | The past is past | 33 | 178 | 4 (20%) | -149 | mood -73, trust -2, relations -70, work -4 | trade-off |
|  | 2 | A earns A place | 15 | 77 | 0 | -62 | mood -29, trust -4, relations -28 | trade-off |
|  | 3 | Let B have one swing | 54 | 94 | 0 | -41 | trust +3, relations -28, work -15 | trade-off |
|  | 4 | Keep it between them | 29 | 42 | 48 (20%) | -61 | mood -19, trust -8, relations -33 | trade-off |

### Council_Dilemmas_ComingOfAge.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| a path: the workshop (`Council_Dilemma_Path_Craft`) | 1 | Apprentice A to B in crafting | 46 | 7 | 0 | 39 | mood +24, relations +8, work -0, skill +8 | trade-off |
|  | 2 | Put A on the defenses | 30 | 16 | 0 | 14 | mood -16, skill +30 | trade-off |
|  | 3 | Let A choose | 52 | 0 | 0 | 52 | mood +40, trust +9, wellbeing +3 | trade-off |
| a path: the healer (`Council_Dilemma_Path_Medicine`) | 1 | Apprentice A to B in medicine | 46 | 0 | 0 | 46 | mood +30, relations +8, work -0, skill +8 | trade-off |
|  | 2 | The fields need hands more | 30 | 20 | 0 | 10 | mood -16, trust -4, skill +30 | trade-off |
|  | 3 | Let A choose | 49 | 0 | 0 | 49 | mood +40, trust +9 | trade-off |
| a path: the scholar (`Council_Dilemma_Path_Scholar`) | 1 | Apprentice A to B in study | 46 | 0 | 0 | 46 | mood +30, relations +8, work -0, skill +8 | trade-off |
|  | 2 | Hands before books | 30 | 16 | 0 | 14 | mood -16, skill +30 | trade-off |
|  | 3 | Let A choose | 49 | 0 | 0 | 49 | mood +40, trust +9 | trade-off |
| a path: the wall (`Council_Dilemma_Path_Warrior`) | 1 | Apprentice A to B at arms | 46 | 4 | 0 | 42 | mood +26, relations +8, work -0, skill +8 | trade-off |
|  | 2 | Not yet | 0 | 22 | 0 | -22 | mood -16, trust -6 | dominated by answer 1; dominated by answer 3 |
|  | 3 | Let A choose | 49 | 0 | 0 | 49 | mood +40, trust +9 | trade-off |

### Council_Dilemmas_Goals.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| time for a dream (`Council_Dilemma_Goal_Excused`) | 1 | Let A off the chores for a season | 44 | 18 | 6 (25%) | 20 | mood +14, trust +6, wellbeing +6, work -6 | trade-off |
|  | 2 | One free afternoon in three | 7 | 12 | 0 | -5 | mood -6, trust +3, wellbeing +4, work -6 | trade-off |
|  | 3 | The chores come first | 1 | 26 | 0 (20%) | -25 | mood -20, trust -5 | trade-off |
| a dream on credit (`Council_Dilemma_Goal_Purse`) | 1 | Give A 150 silver | 61 | 20 | 21 (20%) | 20 | mood +23, trust +8, wellbeing +3, goods -15 | trade-off |
|  | 2 | Give 50 silver | 4 | 11 | 0 | -7 | mood -6, trust +3, wellbeing +1, goods -5 | trade-off |
|  | 3 | Not from the colony's purse | 1 | 26 | 0 | -25 | mood -20, trust -5 | trade-off |
|  | 4 | Give the silver, and send our best haggler along | 60 | 12 | 18 (20%) | 31 | mood +32, trust +9, wellbeing +2, goods -12 | trade-off (gated) |
| a teacher for a dream (`Council_Dilemma_Goal_Mentor`) | 1 | Ask B to make the time | 55 | 13 | 21 (20%) | 22 | mood +8, trust +3, relations +7, wellbeing +2, work -0, skill +3 | trade-off |
|  | 2 | Free up B's evenings | 72 | 42 | 0 | 30 | mood +12, relations +16, work -0, skill +3 | trade-off |
|  | 3 | That's between the two of them | 1 | 15 | 0 | -14 | mood -12, trust -2 | dominated by answer 2 |
| a foolish dream (`Council_Dilemma_Goal_Foolish`) | 1 | Stand behind A's dream | 32 | 36 | 6 (25%) | -10 | mood -10, trust +5, relations -6, wellbeing +2, work -2 | trade-off |
|  | 2 | Hear B out | 24 | 40 | 5 (25%) | -20 | mood -12, trust -3, work -5 | trade-off |
|  | 3 | Settle it with a wager | 48 | 3 | 8 (30%) | 37 | mood +24, trust +3, relations +11, wellbeing -1 | trade-off |
|  | 4 | Stay out of it | 0 | 42 | 4 (30%) | -47 | mood -36, trust -6, work -4 | dominated by answer 2 |
| two dreams, one colony (`Council_Dilemma_Goal_TwoDreams`) | 1 | A's dream comes first | 56 | 67 | 0 | -11 | relations -11 | trade-off |
|  | 2 | B's dream comes first | 56 | 67 | 0 | -11 | relations -11 | trade-off |
|  | 3 | Split everything down the middle | 6 | 12 | 0 | -6 | mood -12, trust +6 | trade-off |
|  | 4 | Make them help each other | 86 | 1 | 53 (35%) | 32 | mood +31, trust +2, relations +5, work -6 | trade-off |
| the road out (`Council_Dilemma_Goal_Caravan`) | 1 | Ask A to stay | 60 | 0 | 1 (25%) | 59 | mood +48, trust +11 | trade-off |
|  | 2 | Give A the council's blessing | 61 | 0 | 0 | 61 | mood +49, trust +9, wellbeing +3 | trade-off |
|  | 3 | The colony needs every hand | 1 | 65 | 0 | -64 | mood -55, trust -8, work -1 | dominated by answer 1; dominated by answer 2; dominated by answer 4 |
|  | 4 | Let A decide alone | 0 | 26 | 0 | -26 | mood -22, trust -4 | dominated by answer 1; dominated by answer 2 |
| a dream fading (`Council_Dilemma_Goal_GivingUp`) | 1 | Rally the colony behind A | 54 | 2 | 17 (25%) | 35 | mood +28, trust +5, wellbeing +3, work -2 | trade-off |
|  | 2 | A few easy days to think it over | 42 | 13 | 0 | 30 | mood +30, trust +4, wellbeing +3, work -8 | trade-off |
|  | 3 | There's no shame in letting it go | 2 | 12 | 5 (25%) | -16 | mood -12, trust +2, work -5 | trade-off |
|  | 4 | Tell A to toughen up | 0 | 50 | 0 (30%) | -50 | mood -42, trust -8, wellbeing +2 | trade-off |
| if I don't make it (`Council_Dilemma_Goal_Remember`) | 1 | Write it into the colony's records | 91 | 2 | 0 | 89 | mood +79, trust +12, work -2 | trade-off |
|  | 2 | B promises to carry it on | 129 | 2 | 0 | 127 | mood +80, trust +9, relations +40, wellbeing -2 | trade-off |
|  | 3 | A private promise from the council | 40 | 0 | 0 | 40 | mood +35, trust +4 | trade-off |
|  | 4 | Don't talk like that | 0 | 24 | 0 | -24 | mood -20, trust -4 | dominated by answer 3 |

### Council_Dilemmas_Life.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| the last of the medicine (`Council_DlmL_LastMedicine`) | 1 | Treat A with the medicine | 20 | 4 | 6 (30%) | 10 | mood +5, trust +3, wellbeing +3, skill +2, goods -2 | trade-off (gated) |
|  | 2 | Keep the medicine; rest and broth instead | 0 | 8 | 0 | -8 | mood -5, work -2 | trade-off |
|  | 3 | Send a forager out for wild herbs | 13 | 1 | 6 (35%) | 7 | mood +3, wellbeing +2, work -1, skill +2 | trade-off (gated) |
| keeping apart (`Council_DlmL_Quarantine`) | 1 | Move A to a bed apart | 0 | 10 | 0 | -10 | mood -9, trust -2 | trade-off |
|  | 2 | Keep A where A is | 10 | 5 | 3 (30%) | 2 | mood -4, trust +6 | trade-off |
|  | 3 | Have someone nurse A apart from the rest | 14 | 3 | 0 (20%) | 11 | mood +9, relations +1, wellbeing +1, work -3, skill +2 | trade-off (gated) |
| a cold in the kitchen (`Council_DlmL_SickCook`) | 1 | Out of the kitchen; someone else cooks until it passes | 4 | 6 | 0 | -2 | mood -4, work -2, skill +4 | trade-off (gated) |
|  | 2 | Let A cook, with clean hands and a cloth over the face | 0 | 4 | 0 | -4 | mood -4 | trade-off |
|  | 3 | Cold suppers for a few days | 8 | 13 | 0 | -5 | mood -5, wellbeing +2, work -2 | trade-off |
| an old remedy (`Council_DlmL_OldRemedy`) | 1 | Let B try the old remedy | 16 | 1 | 6 (40%) | 10 | mood +8, relations +1, wellbeing +1, work -1 | trade-off |
|  | 2 | Medicine from the stores, and no remedy | 4 | 8 | 0 | -4 | mood -6, wellbeing +4, goods -2 | trade-off (gated) |
|  | 3 | Both: the remedy at night, the doctor by day | 18 | 4 | 0 | 14 | mood +13, wellbeing +3, goods -2 | trade-off (gated) |
| medicine for a stranger (`Council_DlmL_StrangerMedicine`) | 1 | Give X a dose | 10 | 4 | 0 | 6 | mood +5, goods -2, outside +4 | trade-off |
|  | 2 | Have A look at X, but keep the medicine | 3 | 0 | 0 | 3 | skill +2, outside +1 | trade-off |
|  | 3 | We can't spare anything | 0 | 10 | 0 | -10 | mood -8, outside -2 | dominated by answer 2 |
| a strange cure (`Council_DlmL_OddCure`) | 1 | Let X try the cure | 15 | 0 | 8 (50%) | 6 | mood +4, wellbeing +2, outside +1 | trade-off |
|  | 2 | Let X try it, with our doctor watching | 8 | 1 | 2 (40%) | 6 | wellbeing +1, work -1, skill +5 | trade-off (gated) |
|  | 3 | Thank X, and decline | 0 | 2 | 0 | -2 | mood -1 | trade-off |
| fever at the camp (`Council_DlmL_CampSick`) | 1 | Send them medicine | 23 | 7 | 0 | 16 | mood +8, work -0, goods -5, outside +14 | trade-off |
|  | 2 | Send our doctor to tend them | 29 | 3 | 6 (30%) | 20 | mood +8, work -3, skill +5, outside +9 | trade-off (gated) |
|  | 3 | Food and blankets, not medicine | 5 | 2 | 0 | 3 | goods -2, outside +5 | trade-off |
|  | 4 | Keep our distance | 0 | 12 | 0 | -12 | mood -5, outside -7 | trade-off |
| an empty pot at the camp (`Council_DlmL_CampHungry`) | 1 | Fill their pot | 16 | 5 | 0 | 11 | mood +3, goods -3, outside +11 | trade-off |
|  | 2 | Have our best tracker show their hunters the game | 16 | 0 | 4 (30%) | 12 | mood +4, skill +3, outside +5 | trade-off (gated) |
|  | 3 | Food for work: they can earn it | 7 | 3 | 0 | 5 | goods +2, outside +2 | trade-off |
|  | 4 | We have nothing to spare | 0 | 10 | 0 | -10 | mood -5, outside -5 | dominated by answer 3 |
| a child missing from the camp (`Council_DlmL_CampLostChild`) | 1 | Send our best tracker to search | 42 | 4 | 10 (30%) | 27 | mood +17, work -4, outside +15 | trade-off (gated) |
|  | 2 | Lend them lights and let them search | 5 | 0 | 0 | 5 | outside +5 | trade-off |
|  | 3 | Keep the gate lit and the bell ringing all night | 9 | 11 | 0 | -2 | work -11, outside +9 | trade-off |
|  | 4 | It's their child and their search | 0 | 14 | 0 | -14 | mood -5, outside -9 | dominated by answer 2 |
| the empty chair (`Council_DlmL_EmptyChair`) | 1 | Hear A out at the next meeting | 10 | 2 | 0 | 8 | mood +4, trust +4 | trade-off |
|  | 2 | What was decided stands | 0 | 14 | 0 | -14 | mood -9, trust -4 | dominated by answer 1; dominated by answer 3 |
|  | 3 | From now on, meetings wait for those at work | 9 | 3 | 0 | 6 | mood +3, trust +3 | dominated by answer 1 |
|  | 4 | Have a good talker tell A the whole of it | 10 | 0 | 7 (30%) | 3 | mood +1, trust +1, relations +1 | dominated by answer 1; dominated by answer 3 (gated) |
| a day off, now (`Council_DlmL_DayOffBadTime`) | 1 | Give A the days | 22 | 13 | 0 | 10 | mood +4, trust +4, wellbeing +9, work -8 | trade-off |
|  | 2 | Lighter work until the rush is over | 2 | 1 | 0 | 2 | wellbeing +2, work -1 | trade-off |
|  | 3 | Ask A to hold on until the rush is over | 0 | 14 | 6 (30%) | -20 | mood -9, trust -3, wellbeing -2, work -6 | dominated by answer 2 |
|  | 4 | Everyone takes a half day, in turn | 23 | 18 | 0 | 5 | mood -4, wellbeing +23, work -14 | trade-off |
| it's gone too far (`Council_DlmL_FrictionGrown`) | 1 | Sit them down and make them talk it out | 34 | 1 | 24 (35%) | 9 | mood -6, trust +2, relations +14 | trade-off (gated) |
|  | 2 | Separate chores, separate tables | 0 | 30 | 0 | -30 | mood -30 | trade-off |
|  | 3 | One week, one job, together | 21 | 18 | 14 (30%) | -10 | mood -18, relations +4, work +3 | trade-off |
|  | 4 | Leave them to it | 0 | 37 | 16 (35%) | -53 | mood -37, relations -16 | dominated by answer 2; dominated by answer 3 |
| putting down the tools (`Council_DlmL_ElderRest`) | 1 | Let A rest; A has earned it | 37 | 26 | 0 | 11 | mood +18, trust +16, wellbeing +3, work -26 | trade-off |
|  | 2 | Ask A to teach B instead | 28 | 7 | 0 | 20 | mood +9, trust +3, work -1, skill +10 | trade-off |
|  | 3 | Lighter work: mending and minding the stores | 0 | 1 | 0 | -1 | work -1 | dominated by answer 2 |
|  | 4 | Not yet; we need every hand | 0 | 20 | 0 | -20 | mood -16, trust -4 | dominated by answer 3 |
| a child's question (`Council_DlmL_ChildQuestion`) | 1 | Answer A straight | 7 | 0 | 0 | 7 | mood +4, skill +3 | trade-off |
|  | 2 | Tell A a story instead | 3 | 2 | 0 | 1 | wellbeing +1, work -2, skill +2 | dominated by answer 1 |
|  | 3 | Ask our most learned to answer it | 18 | 0 | 4 (25%) | 13 | mood +6, relations +1, skill +6 | trade-off (gated) |
|  | 4 | That's a question for when you're older | 0 | 4 | 0 | -4 | mood -4 | dominated by answer 1 |
| a dead traveler's journal (`Council_DlmL_FoundJournal`) | 1 | Send it home to {FACTION} | 12 | 0 | 0 | 12 | mood +6, outside +6 | trade-off |
|  | 2 | Read it aloud over supper | 16 | 3 | 0 | 13 | mood -1, wellbeing +16, work -2 | trade-off |
|  | 3 | Have our best scholar study it | 13 | 0 | 0 | 13 | skill +13 | trade-off (gated) |
|  | 4 | Bury it with a marker | 0 | 1 | 0 | -1 | work -1 | dominated by answer 1; dominated by answer 3 |
| a promise not kept (`Council_DlmL_BrokenPromise`) | 1 | Make it up to A | 18 | 4 | 0 | 14 | mood +12, trust +6, goods -4 | trade-off |
|  | 2 | Apologize at the table | 10 | 2 | 0 | 8 | mood +6, trust +4, work -2 | trade-off |
|  | 3 | Have our best talker explain what happened | 9 | 0 | 7 (35%) | 1 | mood +1, trust +1 | trade-off (gated) |
|  | 4 | The council owes nothing for it | 0 | 30 | 0 | -30 | mood -20, trust -10 | trade-off |
| the same bowl again (`Council_DlmL_SameMeal`) | 1 | Have our best cook try something new each week | 26 | 9 | 8 (30%) | 8 | mood +14, work -4, skill +3, goods -5 | trade-off (gated) |
|  | 2 | Send a forager out for something fresh | 14 | 1 | 4 (30%) | 9 | mood +2, work -1, skill +3, goods +5 | trade-off (gated) |
|  | 3 | Food is food | 1 | 4 | 0 | -3 | mood -4, trust +1 | trade-off |
| the same work every day (`Council_DlmL_SameWork`) | 1 | Give A a week of something different | 9 | 0 | 0 | 9 | mood +6, trust +3 | trade-off |
|  | 2 | Have someone teach A a new trade | 20 | 11 | 0 | 9 | mood -3, work -0, skill +12 | trade-off (gated) |
|  | 3 | Rotate everyone's chores once a week | 5 | 4 | 0 | 1 | mood -4, trust +5 | trade-off |
|  | 4 | Someone has to do it | 0 | 9 | 0 | -9 | mood -6, trust -3 | dominated by answer 1; dominated by answer 2; dominated by answer 3 |
| elbow to elbow (`Council_DlmL_Crowded`) | 1 | Give A a corner of the storeroom | 11 | 7 | 0 | 4 | mood +1, trust +3 | trade-off |
|  | 2 | Supper in two sittings | 4 | 0 | 0 | 4 | wellbeing +4 | trade-off |
|  | 3 | Have a builder mark out a new room | 9 | 1 | 0 | 8 | mood +6, work -1, skill +3 | trade-off (gated) |
|  | 4 | Everyone is crowded | 0 | 6 | 0 | -6 | mood -6 | dominated by answer 1; dominated by answer 2; dominated by answer 3 |
| small hands at the workbench (`Council_DlmL_ChildAtTheBench`) | 1 | B takes A on as an apprentice | 23 | 16 | 0 | 6 | mood +2, relations -6, work -0, skill +11 | trade-off |
|  | 2 | Small jobs only, under B's eye | 13 | 0 | 0 | 12 | mood +6, work -0, skill +7 | trade-off |
|  | 3 | Let A make one thing, start to finish | 30 | 1 | 11 (40%) | 18 | mood +11, skill +7 | trade-off |
|  | 4 | Play is a child's work | 3 | 10 | 0 | -7 | mood -4, wellbeing +3, work -6 | trade-off |
| the doctor needs sleep (`Council_DlmL_TiredDoctor`) | 1 | B sleeps; someone else sits up | 19 | 6 | 0 | 13 | mood +4, wellbeing +8, work -6, skill +8 | trade-off (gated) |
|  | 2 | B keeps watch; others cover B work | 12 | 7 | 0 | 6 | mood +9, relations +2, work -5 | trade-off |
|  | 3 | Teach the basics to everyone who's awake | 15 | 2 | 0 | 13 | mood +2, wellbeing +2, work -2, skill +11 | trade-off |

### Council_Dilemmas_Mods_Hospitality.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| a guest who wants to stay (`Council_DlmH_GuestWantsToStay`) | 1 | Take X in | 112 | 8 | 0 | 104 | mood +48, trust +4, people +60, outside -8 | trade-off |
|  | 2 | Take X in, with a gift to {FACTION} | 114 | 17 | 0 | 97 | mood +48, trust +6, goods -15, people +60, outside -2 | trade-off |
|  | 3 | Ask X to come back another season | 2 | 6 | 0 | -4 | mood -6, outside +2 | trade-off |
|  | 4 | No | 0 | 33 | 0 | -33 | mood -30, trust -3 | dominated by answer 3 |
| a guest's complaint (`Council_DlmH_GuestComplaint`) | 1 | Give back the fee, and a good meal | 3 | 6 | 0 | -2 | goods -6, outside +3 | trade-off |
|  | 2 | Have our best talker smooth it over | 4 | 0 | 3 (40%) | 1 | mood -2, skill +4, outside -1 | trade-off (gated) |
|  | 3 | They can take it or leave it | 0 | 3 | 0 | -3 | outside -3 | trade-off |

### Council_Dilemmas_Pets.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| the eaten dinner (`Council_Dilemma_PetDinner`) | 1 | A cooks B a new meal | 2 | 11 | 0 | -10 | mood -6, trust -2, goods -2 | trade-off |
|  | 2 | It's only an animal | 23 | 39 | 0 | -16 | mood +8, trust -2, relations -22 | trade-off |
|  | 3 | Turn {PET_nameDef} loose in the wild | 24 | 242 | 0 | -218 | mood -167, trust -18, relations -22, goods -10 | dominated by answer 1; dominated by answer 2 |
| an animal in the kitchen (`Council_Dilemma_PetKitchen`) | 1 | Keep {PET_nameDef} out of the kitchen | 20 | 34 | 0 | -14 | mood -10, trust -4 | trade-off |
|  | 2 | The kitchen is {PET_nameDef}'s too | 23 | 39 | 0 | -16 | mood -15, trust -2 | trade-off |
|  | 3 | Let them sort it out | 0 | 24 | 4 (30%) | -28 | mood -24, work -4 | trade-off |

### Council_Dilemmas_Reputation.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| sanctuary (`Council_DlmR_Sanctuary`) | 1 | Take X in | 114 | 20 | 0 | 94 | mood +49, trust +4, people +60, outside -20 | easy: best by 94 |
|  | 2 | Tell {FACTION} what X asked | 5 | 37 | 0 | -32 | mood -31, trust -6, outside +5 | trade-off |
|  | 3 | A night's rest, then the road | 0 | 1 | 0 | -1 | mood -1 | trade-off |
| counting on our mercy (`Council_DlmR_CountingOnMercy`) | 1 | Show X the colony's other side | 9 | 4 | 0 | 5 | mood +2, trust +3 | trade-off |
|  | 2 | Let X go, then | 6 | 14 | 0 | -7 | mood -8, trust -3, outside +4 | trade-off |
|  | 3 | Treat X like any other prisoner | 2 | 0 | 0 | 2 | outside +2 | trade-off |
| one act of mercy (`Council_DlmR_OneMercy`) | 1 | Send food, and ask nothing | 58 | 9 | 0 | 48 | mood +36, trust +8, goods -6, outside +10 | easy: best by 33 |
|  | 2 | Send silver instead | 23 | 8 | 0 | 15 | mood +8, trust +3, goods -8, outside +12 | trade-off |
|  | 3 | Let them talk | 0 | 35 | 0 | -35 | mood -29, trust -6 | trade-off |
| someone else's thief (`Council_DlmR_TheirThief`) | 1 | Flog X | 7 | 17 | 0 | -10 | mood -16, outside +6 | trade-off |
|  | 2 | Pardon X | 11 | 7 | 0 | 4 | mood +7, outside -3 | trade-off |
|  | 3 | Not our thief, not our business | 0 | 1 | 0 | -1 | outside -1 | trade-off |
| always a bowl (`Council_DlmR_Handouts`) | 1 | Keep the gate open and the pot full | 10 | 14 | 0 | -4 | mood -9, goods -5, outside +10 | trade-off |
|  | 2 | Food for work | 14 | 2 | 0 | 12 | mood +6, trust +3, goods +2, outside +1 | easy: best by 16 |
|  | 3 | Close the gate to beggars | 0 | 19 | 0 | -19 | mood -12, trust -2, outside -5 | dominated by answer 2 |
| a joke at our expense (`Council_DlmR_StingyMockery`) | 1 | Send X off with a gift | 24 | 9 | 0 | 16 | mood +11, trust +4, goods -5, outside +5 | easy: best by 25 |
|  | 2 | Let X laugh | 0 | 15 | 0 | -15 | mood -12, trust -3 | trade-off |
|  | 3 | Charge X for the well water, then | 2 | 11 | 0 | -9 | mood -8, goods +2, outside -3 | trade-off |
| the way it's done here (`Council_DlmR_SkipCustom`) | 1 | The custom stands, for everyone | 20 | 20 | 0 | 0 | nothing scored | trade-off |
|  | 2 | Let A choose | 20 | 20 | 0 | 0 | nothing scored | trade-off |
|  | 3 | Have B tell A why it matters | 45 | 0 | 25 (30%) | 20 | relations +25, work -5 | trade-off |
| how it used to be (`Council_DlmR_OldCustom`) | 1 | Bring it back, the way it was | 91 | 12 | 0 | 79 | mood +85, trust +6, work -6, goods -6 | easy: best by 56 |
|  | 2 | Bring it back, with a few changes | 33 | 10 | 0 | 23 | mood +27, trust +2, wellbeing +5, work -6, goods -4 | trade-off |
|  | 3 | Let the old ways rest | 0 | 28 | 0 | -28 | mood -24, trust -4 | trade-off |
| a guest too comfortable (`Council_DlmR_Overstay`) | 1 | Guests are guests | 2 | 15 | 0 | -13 | mood -12, trust -3, outside +2 | trade-off |
|  | 2 | Put X to work | 12 | 0 | 4 (25%) | 8 | mood +4, trust +2, goods +2, outside -1 | trade-off |
|  | 3 | Ask the {FACTION} visitors to move on | 2 | 5 | 0 | -4 | mood -1, wellbeing +2, outside -4 | trade-off |
| a friend at the gate (`Council_DlmR_FriendAtTheGate`) | 1 | Open the gate for X | 32 | 5 | 0 | 27 | mood +19, trust +6, work -1, outside +2 | trade-off |
|  | 2 | The rule is the rule | 0 | 36 | 0 | -36 | mood -30, trust -6 | dominated by answer 1 |
|  | 3 | A can go out to X | 20 | 2 | 0 | 18 | mood +12, trust +8, work -2 | trade-off |
| one day off (`Council_DlmR_DayOfRest`) | 1 | Give A the day | 34 | 9 | 0 | 25 | mood +25, trust +6, wellbeing +3, work -9 | trade-off |
|  | 2 | A day of rest for everyone | 99 | 62 | 0 | 37 | mood +54, trust +8, wellbeing +24, work -48 | trade-off |
|  | 3 | The work won't wait | 0 | 24 | 5 (25%) | -30 | mood -20, trust -4, work -5 | trade-off |
| carrying everyone (`Council_DlmR_CarryingEveryone`) | 1 | Everyone pulls their weight | 33 | 11 | 0 | 21 | mood +15, trust +6 | trade-off |
|  | 2 | Thank A at supper | 25 | 2 | 0 | 23 | mood +15, trust +9, work -1 | trade-off |
|  | 3 | Tell A to slow down too | 3 | 25 | 0 | -22 | mood -12, trust -4, wellbeing +3, work -8 | trade-off |

### Council_Dilemmas_Secrets.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| blood on old hands (`Council_Dilemma_Secret_BloodPast`) | 1 | Keep A under watch for a season | 4 | 65 | 2 (30%) | -62 | mood -57, trust -5 | dominated by answer 2; dominated by answer 3 |
|  | 2 | Let A work off the past | 24 | 17 | 0 | 6 | mood -2, trust +6, work +3 | trade-off |
|  | 3 | What's past is past | 33 | 33 | 0 | -0 | mood -5, trust -2, relations +7 | trade-off |
|  | 4 | Have A tell the whole story | 73 | 5 | 80 (25%) | -12 | mood -12, trust +2, relations +1, work -4 | trade-off |
| the hunters' offer (`Council_Dilemma_Secret_Bounty`) | 1 | Take the silver and tell them | 31 | 422 | 0 | -391 | mood -360, trust -58, work -2, goods +30 | trade-off |
|  | 2 | Sell them a false trail | 103 | 0 | 24 (35%) | 78 | mood +59, trust +6, wellbeing +2, work -4, goods +15 | trade-off |
|  | 3 | Refuse, and tell them the gate stays shut | 93 | 11 | 0 | 82 | mood +73, trust +11, work -2 | trade-off |
| the doctor's hands (`Council_Dilemma_Secret_DoctorsHands`) | 1 | A keeps doctoring, as before | 26 | 27 | 1 (25%) | -2 | mood +13, trust +6, relations -20, work -1 | trade-off |
|  | 2 | Only with someone else watching | 9 | 20 | 0 | -10 | mood -18, trust +3, skill +5 | trade-off |
|  | 3 | Take A instruments away | 1 | 87 | 0 | -86 | mood -77, trust -8, work -1 | dominated by answer 2 |
| the loose tongue (`Council_Dilemma_Secret_LooseTongue`) | 1 | Order B to drop the subject | 24 | 16 | 11 (30%) | -3 | mood +8, relations -11 | trade-off |
|  | 2 | Make B apologize at supper | 27 | 51 | 2 (30%) | -26 | mood -3, trust -1, relations -16, work -6 | trade-off |
|  | 3 | Sit them down until they sort it out | 27 | 19 | 14 (35%) | -6 | mood -18, relations +18, work -6 | trade-off |
|  | 4 | It's out. People will talk | 20 | 16 | 0 | 4 | mood +8, trust -4 | trade-off |
| the pirate lord's due (`Council_Dilemma_Secret_PirateDue`) | 1 | Pay it, all of it | 94 | 60 | 0 | 34 | mood +62, trust +12, goods -40 | easy: best by 47 |
|  | 2 | Pay it, and A works it off | 27 | 40 | 0 | -13 | mood +15, trust +9, work +3, goods -40 | trade-off |
|  | 3 | Send half, and haggle | 3 | 26 | 20 (30%) | -43 | mood -24, trust +3, work -2, goods -20 | trade-off |
|  | 4 | Not one coin | 1 | 65 | 4 (30%) | -68 | mood -55, trust -7, work -6 | trade-off |
| a kindness found out (`Council_Dilemma_Secret_KindnessFound`) | 1 | Raise a cup to A at supper | 76 | 17 | 2 (25%) | 57 | mood +62, trust +8, work -6, goods -7 | trade-off |
|  | 2 | Thank A in private | 28 | 0 | 0 | 28 | mood +24, trust +4 | trade-off |
|  | 3 | Make it everyone's job from now on | 38 | 19 | 0 | 18 | mood +11, trust +4, work +3 | trade-off |
| the sickness question (`Council_Dilemma_Secret_Sickness`) | 1 | A room apart, until it's certain | 3 | 49 | 6 (30%) | -52 | mood -43, trust -3, work -6 | trade-off |
|  | 2 | B examines everyone | 50 | 15 | 6 (20%) | 29 | mood +21, trust +9, relations -6, skill +6 | trade-off |
|  | 3 | No quarantine, no fuss | 26 | 22 | 0 | 4 | mood +13, trust +6, relations -15 | trade-off |
| one more bout (`Council_Dilemma_Secret_OneMoreBout`) | 1 | One bout, by the rules | 23 | 50 | 0 | -27 | mood -8, trust -4, work -15 | trade-off |
|  | 2 | Let A teach fighting instead | 51 | 24 | 3 (20%) | 24 | mood +7, trust +4, work -4, skill +16 | trade-off |
|  | 3 | No fighting. The pits are behind A | 49 | 35 | 4 (25%) | 10 | mood +5, trust +9, work -4 | trade-off |
| using again (`Council_Dilemma_Secret_UsingAgain`) | 1 | Time off, with B beside A | 104 | 27 | 2 (50%) | 75 | mood +48, trust +8, relations +32, wellbeing +6, work -19 | easy: best by 99 |
|  | 2 | Search A room and burn what's found | 4 | 68 | 7 (35%) | -71 | mood -55, trust -5, work -8, goods -3 | trade-off |
|  | 3 | Let A handle it alone | 0 | 15 | 9 (40%) | -24 | mood -12, trust -4, work -8 | trade-off |
| what came out (`Council_Dilemma_Secret_WhatCameOut`) | 1 | Talk it through one evening | 67 | 5 | 32 (30%) | 30 | mood +10, trust +2, relations +22, work -4 | trade-off |
|  | 2 | The subject is closed | 18 | 7 | 0 | 11 | mood +8, trust +3 | trade-off |
|  | 3 | Let A decide | 36 | 0 | 7 (30%) | 29 | mood +30, trust +6, relations -7 | trade-off |

### Council_Dilemmas_Social.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| a line down the middle (`Council_Dilemma_Soc_SwornRivals`) | 1 | Settle it in the yard, by the rules, once | 41 | 22 | 11 (35%) | 8 | mood +5, trust +4, relations +15, work -15 | trade-off |
|  | 2 | Keep them apart: separate shifts, separate meals | 0 | 53 | 4 (25%) | -57 | mood -41, trust -12, work -4 | trade-off |
|  | 3 | Make them teach each other their trades | 33 | 12 | 17 (35%) | 4 | mood -12, relations +12, work -6, skill +10 | trade-off |
|  | 4 | Tell them to grow up | 0 | 52 | 13 (40%) | -66 | mood -43, trust -16, work -6 | dominated by answer 2 |
| inseparable (`Council_Dilemma_Soc_Inseparable`) | 1 | Let them work together | 143 | 14 | 3 (30%) | 126 | mood +32, trust +12, relations +80, work +2 | trade-off |
|  | 2 | Give them the night watch, together | 57 | 9 | 0 | 48 | trust +9, relations +48, work -9 | trade-off |
|  | 3 | Keep the roster as it is | 2 | 33 | 6 (25%) | -37 | mood -28, trust -8 | dominated by answer 1 |
|  | 4 | Split them up on purpose | 1 | 53 | 0 (35%) | -53 | mood -31, trust -20, relations +28, work -4 | trade-off |
| the cover story (`Council_Dilemma_Soc_CoverUp`) | 1 | Punish them both | 51 | 59 | 0 | -8 | mood -47, trust -4, relations +40, work +3 | trade-off |
|  | 2 | Punish A for the mistake | 4 | 45 | 0 | -41 | mood -39, trust -2 | trade-off |
|  | 3 | Punish B for the lie | 44 | 55 | 0 | -11 | mood -47, trust -3, relations +40 | dominated by answer 1 |
|  | 4 | Let it go | 49 | 41 | 15 (25%) | -7 | mood -9, trust +2 | trade-off |
| the remembrance (`Council_Dilemma_Soc_Remembrance`) | 1 | Hold the remembrance night | 181 | 26 | 0 | 155 | mood +96, trust +15, relations +70, work -23, goods -3 | trade-off |
|  | 2 | Allow speeches, but no feast | 15 | 17 | 0 | -2 | mood -12, trust -3, relations +15, work -2 | trade-off |
|  | 3 | Only those who fought may come | 159 | 40 | 0 | 119 | mood +11, relations +110, goods -2 | trade-off |
|  | 4 | No. The stores are for the living | 2 | 72 | 0 (30%) | -70 | mood -10, trust -15, relations +9, goods -1 | dominated by answer 3 |
| a rival's word (`Council_Dilemma_Soc_Accusation`) | 1 | Search A's things | 2 | 60 | 0 (30%) | -58 | mood -25, trust -7, relations -22, wellbeing +1, goods +1 | trade-off |
|  | 2 | Take B's word for it | 26 | 120 | 0 | -94 | mood -53, trust -18, relations -22 | dominated by answer 1 |
|  | 3 | Throw the case out | 24 | 60 | 14 (25%) | -50 | mood -19, trust -8, relations -22, goods -1 | trade-off |
|  | 4 | Make B take it back in public | 28 | 54 | 4 (25%) | -29 | trust -3, relations -22, wellbeing +2, work -6 | trade-off |
| a friend left behind (`Council_Dilemma_Soc_LeftBehind`) | 1 | Ask A to make time for B | 101 | 14 | 52 (35%) | 35 | mood +5, trust -1, relations +31 | trade-off |
|  | 2 | Bring B into the new crowd | 64 | 17 | 34 (30%) | 14 | mood -9, trust +3, relations +20, work -1 | trade-off |
|  | 3 | Keep B busy | 1 | 25 | 0 (25%) | -24 | mood -25, wellbeing +2, work +1 | trade-off |
|  | 4 | Leave them to it | 0 | 43 | 4 (20%) | -47 | mood -37, trust -6, work -4 | dominated by answer 3 |
| the closed circle (`Council_Dilemma_Soc_ClosedCircle`) | 1 | Order them to make room for A | 0 | 66 | 0 (30%) | -66 | mood -19, trust -3, relations -2 | trade-off |
|  | 2 | Mix up the work crews | 26 | 106 | 0 | -80 | mood -32, trust -3, relations -45 | dominated by answer 1 |
|  | 3 | Have A host a supper for everyone | 60 | 12 | 35 (35%) | 14 | mood +28, trust +2, relations -8, work -4, goods -5 | trade-off |
|  | 4 | Let people choose their own friends | 23 | 28 | 5 (25%) | -10 | mood -1, trust -4, work -5 | trade-off |
| whose work it was (`Council_Dilemma_Soc_WhoseWork`) | 1 | Credit A | 32 | 69 | 4 (25%) | -40 | mood -11, trust -3, relations -22, work -4 | trade-off |
|  | 2 | Credit B | 32 | 69 | 4 (25%) | -40 | mood -11, trust -3, relations -22, work -4 | trade-off |
|  | 3 | Carve both their names on it | 19 | 12 | 10 (30%) | -3 | mood -12, trust +3, relations +11, work -4 | trade-off |
|  | 4 | Credit the colony as a whole | 3 | 33 | 14 (30%) | -44 | mood -24, trust -6, relations -14 | trade-off |
| a plea for a friend (`Council_Dilemma_Soc_Plea`) | 1 | Show mercy | 90 | 26 | 0 | 64 | mood +10, trust +14, relations +40 | trade-off |
|  | 2 | Punish A as the rules say | 8 | 57 | 0 | -49 | mood -43, trust -5 | dominated by answer 1; dominated by answer 3 |
|  | 3 | Halve it, if B takes the other half | 89 | 20 | 0 | 69 | mood +16, trust +10, relations +40, work +3 | trade-off |
|  | 4 | Double it, for trying to sway the council | 2 | 134 | 5 (25%) | -137 | mood -92, trust -25, relations -14, work -5 | dominated by answer 1; dominated by answer 2; dominated by answer 3 |
| the quartermaster's friends (`Council_Dilemma_Soc_Favoritism`) | 1 | Audit the stores | 4 | 70 | 4 (35%) | -70 | mood -42, trust -4, relations -22 | trade-off |
|  | 2 | Back the quartermaster | 28 | 80 | 0 | -52 | mood -22, trust -8, relations -22 | trade-off |
|  | 3 | Make B keep the ledger | 34 | 42 | 5 (35%) | -13 | mood -8, work -5 | trade-off |
|  | 4 | Hand out the good things by lot | 28 | 73 | 0 | -45 | mood -45 | trade-off |

### Rimfolk_Dilemmas_Animals.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| the herd or the stores (`Council_DlmA_FamineCull`) | 1 | Cull {COUNT} head | 12 | 20 | 0 | -8 | mood -19, trust +2, work -1, goods +10 | trade-off |
|  | 2 | Tighten everyone's belts | 8 | 14 | 6 (25%) | -12 | mood -3, trust -3, relations -6 | dominated by answer 4 |
|  | 3 | Turn the weakest out to forage | 0 | 45 | 0 | -45 | mood -30, goods -15 | dominated by answer 1; dominated by answer 4 |
|  | 4 | Let A decide | 40 | 12 | 0 | 28 | mood +18, goods +10 | easy: best by 36 |
| a raider in the crops (`Council_DlmA_CropRaider`) | 1 | Send the hunters | 20 | 7 | 0 | 13 | mood +14, work -1 | partly scored (CropsEaten): read by hand |
|  | 2 | Watch the fields at night | 0 | 6 | 0 (30%) | -6 | work -6 | partly scored (CropsEaten): read by hand |
|  | 3 | Let B try to tame it | 12 | 0 | 2 (40%) | 10 | mood +10 | partly scored (CropsEaten): read by hand |
|  | 4 | Let it eat | 0 | 6 | 0 | -6 | mood -6 | partly scored (CropsEaten): read by hand |
| medicine for an animal (`Council_DlmA_SickAnimal`) | 1 | The good medicine for {PET_nameDef} | 15 | 7 | 0 | 8 | mood +8 | partly scored (AnimalCare): read by hand |
|  | 2 | Herbs, not the good medicine | 0 | 6 | 0 | -6 | mood -6 | partly scored (AnimalCare): read by hand |
|  | 3 | A nurses {PET_nameDef} through | 20 | 4 | 15 (35%) | 0 | mood +5, work -4 | partly scored (AnimalCare): read by hand |
|  | 4 | Let nature take its course | 0 | 30 | 0 | -30 | mood -24, trust -6 | partly scored (AnimalCare): read by hand |
| something at the pens (`Council_DlmA_PenStalker`) | 1 | Hunt it down | 20 | 0 | 0 | 20 | mood +20 | easy: best by 28 |
|  | 2 | Keep watch at the pens | 0 | 9 | 0 | -9 | work -9 | dominated by answer 1 |
|  | 3 | Bring the herd in close | 3 | 6 | 5 (25%) | -8 | mood -8, work +3, goods -4 | trade-off |
|  | 4 | Leave it be | 0 | 12 | 15 (45%) | -27 | mood -20, goods -7 | dominated by answer 1; dominated by answer 3 |
| a bond against the ideoligion (`Council_DlmA_BondFaith`) | 1 | A keeps {PET_nameDef}, and that's the end of it | 20 | 10 | 0 | 10 | mood +14, trust -4 | partly scored (PassAnimal): read by hand |
|  | 2 | {PET_nameDef} keeps out of B's way | 0 | 12 | 0 | -12 | mood -12 | partly scored (PassAnimal): read by hand |
|  | 3 | {PET_nameDef} goes to someone else | 3 | 48 | 0 | -44 | mood -40, trust -4 | partly scored (PassAnimal): read by hand |
|  | 4 | Ask B to bless {PET_nameDef} | 6 | 1 | 17 (40%) | -12 | mood -6, trust +4, relations -9, work -1 | partly scored (PassAnimal): read by hand |
| the lambing watch (`Council_Dilemma_LambingWatch`) | 1 | Keep the lambing watch together | 12 | 4 | 0 | 9 | trust +4, work -4, skill +8 | easy: best by 13 |
|  | 2 | A sits up alone | 0 | 4 | 0 | -4 | work -4 | dominated by answer 1 |
|  | 3 | The herd can manage | 0 | 4 | 0 | -4 | mood -4 | trade-off |
| shearing day (`Council_Dilemma_ShearingDay`) | 1 | Make a day of it | 80 | 6 | 0 | 74 | mood +72, trust +4, work -2, goods -1 | easy: best by 68 |
|  | 2 | Just get it done | 6 | 0 | 0 | 6 | work +3, skill +3 | trade-off |
|  | 3 | No time this year | 0 | 4 | 0 | -4 | mood -4 | dominated by answer 2 |

### Rimfolk_Dilemmas_Animals2.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| bringing the herd home (`Council_Dilemma_HerdHome`) | 1 | Bring the herd home together | 18 | 0 | 0 | 18 | trust +3, work +3, skill +12 | easy: best by 12 |
|  | 2 | A brings them in alone | 6 | 0 | 0 | 6 | work +3, skill +3 | dominated by answer 1 |
|  | 3 | They'll find their own way in | 0 | 6 | 0 | -6 | mood -6 | dominated by answer 1; dominated by answer 2 |

### Rimfolk_Dilemmas_Beast.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| the beast that stays (`Council_DlmA_ResidentBeast`) | 1 | Leave food out for it | 0 | 0 | 0 | 0 | nothing scored | partly scored (DriveOff, FeedBeast): read by hand |
|  | 2 | Keep everyone close until it moves on | 0 | 33 | 0 | -33 | mood -32, work -1 | partly scored (DriveOff, FeedBeast): read by hand |
|  | 3 | Send the hunters after X | 0 | 9 | 0 | -9 | mood -8, work -1 | partly scored (DriveOff, FeedBeast): read by hand |
|  | 4 | Let A try to tame it | 12 | 0 | 0 | 12 | mood +12 | partly scored (DriveOff, FeedBeast): read by hand |
|  | 5 | Let it be | 0 | 0 | 0 | 0 | nothing scored | partly scored (DriveOff, FeedBeast): read by hand |
|  | 6 | Drive it off | 0 | 0 | 0 | -0 | nothing scored | partly scored (DriveOff, FeedBeast): read by hand |

### Rimfolk_Dilemmas_Choices.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| the mess after the fight (`Council_DlmC_AfterTheRaid`) | 1 | Everyone clears up, now | 24 | 23 | 0 | 1 | mood +11, trust +1, wellbeing -14, work +3 | trade-off |
|  | 2 | Rest first; it can wait until tomorrow | 82 | 81 | 0 | 1 | mood +5, wellbeing +43, work -48 | trade-off |
|  | 3 | Three clear what can't wait; the rest rest | 8 | 17 | 0 | -8 | mood -17, wellbeing +5, work +3 | trade-off |
| who sits up with the sick (`Council_DlmC_NightNurse`) | 1 | B sits up with A tonight | 12 | 4 | 0 | 8 | mood +9, wellbeing +3, work -4 | trade-off |
|  | 2 | A dose from the stores, and everyone sleeps | 10 | 4 | 0 | 5 | mood +4, wellbeing +4, goods -2 | trade-off |
|  | 3 | Take turns through the night | 12 | 2 | 0 | 10 | mood +11, wellbeing +1, work -2 | trade-off |
| lessons or walls (`Council_DlmC_TeachOrBuild`) | 1 | Lessons now | 16 | 15 | 0 | 1 | mood -6, trust +4, work -1, skill +4 | trade-off |
|  | 2 | Learn on the job, building beside B | 6 | 6 | 0 | -0 | mood -6, work +3, skill +2 | trade-off |
|  | 3 | After the rush | 6 | 12 | 0 | -6 | mood -12, trust +3, work +3 | trade-off |
| seed for the neighbours (`Council_DlmC_NeighboursSeed`) | 1 | Send it, and ask nothing | 17 | 11 | 0 | 6 | mood +1, trust +1, goods -5, outside +10 | trade-off |
|  | 2 | Sell it to them at a fair price | 14 | 5 | 0 | 9 | mood +6, goods -1, outside +4 | trade-off |
|  | 3 | The colony can't spare it | 0 | 13 | 0 | -13 | mood -9, outside -4 | trade-off |

### Rimfolk_Dilemmas_Customs_New.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| spring cleaning (`Council_Dilemma_SpringCleaning`) | 1 | All hands for a day | 107 | 5 | 0 | 102 | mood +98, trust +3, work +1 | trade-off |
|  | 2 | The morning only | 79 | 0 | 0 | 79 | mood +76, work +3 | trade-off |
|  | 3 | Not this year | 5 | 26 | 0 | -21 | mood -26, trust +5 | trade-off |
| the bounds walk (`Council_Dilemma_BoundsWalk`) | 1 | Everyone walks the bounds | 79 | 6 | 0 | 73 | mood +72, trust +3, work -6, skill +4 | trade-off |
|  | 2 | A walks it with one other | 61 | 9 | 0 | 52 | mood +53, work -1 | trade-off |
|  | 3 | Not this year | 3 | 4 | 0 | -1 | mood -4, trust +3 | trade-off |
| the star night (`Council_Dilemma_StarNight`) | 1 | Everyone sits out late | 76 | 17 | 0 | 60 | mood +64, trust +4, wellbeing +8, work -17 | trade-off |
|  | 2 | An hour under the stars, then bed | 46 | 2 | 0 | 44 | mood +45, trust +2, work -2 | trade-off |
|  | 3 | Bed as usual | 1 | 4 | 0 | -3 | mood -4, trust +1 | trade-off |
| the fishing match (`Council_Dilemma_FishingMatch`) | 1 | A match and a fish supper | 56 | 10 | 0 | 45 | mood +48, trust +4, work -2, goods -6 | trade-off |
|  | 2 | A quiet afternoon for two | 32 | 0 | 0 | 32 | mood +12, trust +2, relations +16, work +3 | trade-off |
|  | 3 | Not now | 3 | 4 | 0 | -1 | mood -4, trust +3 | trade-off |
| the autumn hunt (`Council_Dilemma_AutumnHunt`) | 1 | A big hunt, and supper after | 12 | 8 | 0 | 4 | trust +4, wellbeing +8, work -4, goods -4 | trade-off |
|  | 2 | One hunter, one beast | 2 | 1 | 0 | 2 | trust +2, wellbeing +1, work -1 | trade-off |
|  | 3 | No hunt this year | 3 | 4 | 0 | -1 | mood -4, trust +3 | trade-off |
| telling the bees (`Council_Dilemma_TellingTheBees`) | 1 | The family walks round the hives with A | 3 | 2 | 0 | 1 | trust +3, work -2 | trade-off |
|  | 2 | A goes round alone | 0 | 0 | 0 | -0 | nothing scored | trade-off |
|  | 3 | It isn't done here | 3 | 7 | 0 | -4 | mood -7, trust +3 | trade-off |

### Rimfolk_Dilemmas_Faith.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| ideoligions at odds (`Council_DlmFaith_Clash`) | 1 | Share the {ALTAR}: the ideoligions take turns | 0 | 0 | 0 | -0 | nothing scored | partly scored (FaithClash): read by hand |
|  | 2 | A corner for each ideoligion | 3 | 0 | 0 | 3 | work +3 | partly scored (FaithClash): read by hand |
|  | 3 | Name {FAITH} the colony's ideoligion | 0 | 0 | 0 | -0 | nothing scored | partly scored (FaithClash): read by hand |
| an ideoligion evening (`Council_DlmFaith_Evening`) | 1 | Hold the evening, and all are welcome | 3 | 0 | 0 | 3 | trust +3 | partly scored (FaithEvening, FaithSteady): read by hand |
|  | 2 | An evening for those of {FAITH} only | 2 | 0 | 0 | 1 | trust +2 | partly scored (FaithEvening, FaithSteady): read by hand |
|  | 3 | Not now | 0 | 3 | 0 | -3 | trust -3 | partly scored (FaithEvening, FaithSteady): read by hand |

### Rimfolk_Dilemmas_Family.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| who raises them (`Council_DlmFam_Guardian`) | 1 | B raises A as B own | 120 | 0 | 0 | 120 | mood +120 | partly scored (Adopt): read by hand |
|  | 2 | Let A choose | 120 | 0 | 0 | 120 | mood +120 | partly scored (Adopt): read by hand |
|  | 3 | The whole colony raises A | 12 | 2 | 0 | 10 | mood +12, work -2 | partly scored (Adopt): read by hand |
| one name or two (`Council_DlmFam_SharedName`) | 1 | A takes the {LASTB} name | 0 | 0 | 0 | 0 | nothing scored | partly scored (SharedName): read by hand |
|  | 2 | B takes the {LASTA} name | 0 | 0 | 0 | 0 | nothing scored | partly scored (SharedName): read by hand |
|  | 3 | Each keeps their own name | 0 | 0 | 0 | 0 | nothing scored | partly scored (SharedName): read by hand |
| the wedding (`Council_DlmFam_WeddingPlans`) | 1 | A wedding feast for the whole colony | 24 | 0 | 0 | 24 | mood +24 | partly scored (PlanWedding): read by hand |
|  | 2 | A small wedding, and a toast with their friends | 24 | 0 | 0 | 24 | mood +24 | partly scored (PlanWedding): read by hand |
|  | 3 | Ask them to wait for a calmer time | 0 | 48 | 0 | -48 | mood -48 | partly scored (PlanWedding): read by hand |

### Rimfolk_Dilemmas_Guests.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| a guest to be taken in (`Council_DlmGuest_Invite`) | 1 | Take X in all the same | 60 | 35 | 0 | 25 | mood -32, trust -3, people +60 | easy: best by 16 |
|  | 2 | X stays a guest for now | 9 | 0 | 0 | 9 | mood +9 | trade-off |
| family come to take them home (`Council_DlmGuest_KinAsks`) | 1 | Let A go home with X | 10 | 94 | 0 | -84 | mood -32, work -2, people -60, outside +10 | partly scored (GoHomeWith): read by hand |
|  | 2 | A stays, and X goes home with {COUNT} silver | 0 | 70 | 0 | -70 | mood -40, trust -4, goods -20, outside -5 | partly scored (GoHomeWith): read by hand |
|  | 3 | A stays | 0 | 64 | 0 | -64 | mood -40, trust -9, outside -15 | partly scored (GoHomeWith): read by hand |

### Rimfolk_Dilemmas_Lives.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| a broken window (`Council_DlmLv_Mischief`) | 1 | A mends it, with B's help | 8 | 0 | 0 | 8 | work -0, skill +8 | trade-off |
|  | 2 | Say sorry to everyone at supper | 0 | 4 | 0 | -4 | mood -2, work -2 | dominated by answer 1; dominated by answer 3 |
|  | 3 | Children break things. Let it go. | 3 | 2 | 1 (30%) | 1 | trust +1 | dominated by answer 1 |
|  | 4 | A stern word from the council | 0 | 9 | 0 | -9 | mood -6, trust -3 | dominated by answer 1; dominated by answer 3 |
| afraid after the fighting (`Council_DlmLv_Frightened`) | 1 | B sits up with A tonight | 17 | 1 | 0 | 16 | relations +14, wellbeing +2, work -1 | partly scored (ArcEffect_Quirk): read by hand |
|  | 2 | Stories round the fire for all the children | 9 | 7 | 0 | 2 | wellbeing +9, work -7 | partly scored (ArcEffect_Quirk): read by hand |
|  | 3 | Show A the walls and the watch | 0 | 0 | 0 | -0 | work -0 | partly scored (ArcEffect_Quirk): read by hand |
|  | 4 | Children get over these things | 0 | 15 | 0 | -15 | mood -12, trust -3 | partly scored (ArcEffect_Quirk): read by hand |
| a child's grief (`Council_DlmLv_ChildGrief`) | 1 | B takes A under B wing | 17 | 1 | 0 | 16 | relations +14, wellbeing +3, work -1 | easy: best by 15 |
|  | 2 | A goodbye at the grave, with A | 4 | 2 | 0 | 2 | wellbeing +4, work -2 | dominated by answer 1 |
|  | 3 | Keep A busy with lessons | 9 | 8 | 0 | 0 | mood -8, work -0, skill +9 | trade-off |
|  | 4 | Time will do it | 0 | 15 | 0 | -15 | mood -15 | dominated by answer 1; dominated by answer 3 |
| lessons for the children (`Council_DlmLv_Schooling`) | 1 | Morning lessons, with B | 44 | 1 | 0 | 43 | mood +30, relations +8, work -1, skill +6 | easy: best by 44 |
|  | 2 | They learn by working beside us | 2 | 3 | 0 | -0 | trust -3, skill +2 | dominated by answer 1 |
|  | 3 | Let them run free a while yet | 4 | 6 | 0 | -2 | wellbeing +4, work -6 | trade-off |
| whose den is it (`Council_DlmLv_ChildrenQuarrel`) | 1 | They share it, and that's that | 3 | 0 | 1 (30%) | 1 | relations +1, wellbeing +1 | trade-off |
|  | 2 | Help them build a second one | 6 | 2 | 0 | 3 | wellbeing +2, skill +4, goods -2 | trade-off |
|  | 3 | Let them sort it out themselves | 4 | 0 | 1 (40%) | 3 | relations -1, skill +4 | trade-off |
| a youngster who wants to see the world (`Council_DlmLv_TeenRoad`) | 1 | A goes with the next journey out | 3 | 3 | 0 | 0 | mood -3, trust +3 | trade-off |
|  | 2 | Not this year | 0 | 20 | 0 | -20 | mood -15, trust -4 | dominated by answer 1; dominated by answer 3 |
|  | 3 | A long walk past the fields with B, first | 2 | 1 | 0 | 1 | relations +1, wellbeing +1, work -1 | trade-off |
| one last look at the hills (`Council_DlmLv_ElderWalk`) | 1 | B goes with A for the day | 6 | 2 | 0 | 5 | relations +6, work -2 | trade-off |
|  | 2 | Let A go alone, as A asks | 0 | 1 | 2 (30%) | -2 | mood -2, work -1 | trade-off |
|  | 3 | It's too far; stay where it's safe | 0 | 20 | 0 | -20 | mood -15, trust -4 | trade-off |
| won't see the doctor (`Council_DlmLv_ElderDoctor`) | 1 | Insist: B sees to A today | 4 | 2 | 0 | 1 | mood -2, wellbeing +4 | dominated by answer 3 |
|  | 2 | Respect A's wishes | 3 | 3 | 0 | 0 | nothing scored | dominated by answer 3 |
|  | 3 | Strike a bargain: a check-up for a favor | 3 | 0 | 0 | 3 | wellbeing +3 | trade-off |
| the old one's tale (`Council_DlmLv_ElderTale`) | 1 | Everyone gathers to hear it | 0 | 4 | 0 | -4 | work -4 | partly scored (ArcEffect_Quirk): read by hand |
|  | 2 | B writes it all down | 4 | 1 | 0 | 3 | work -1, skill +4 | partly scored (ArcEffect_Quirk): read by hand |
|  | 3 | Another time | 0 | 8 | 0 | -8 | mood -8 | partly scored (ArcEffect_Quirk): read by hand |
| the old one and the little one (`Council_DlmLv_ElderAndChild`) | 1 | Let A teach B | 22 | 0 | 0 | 22 | relations +8, work -0, skill +14 | easy: best by 14 |
|  | 2 | Ask B's family first | 7 | 0 | 0 | 7 | skill +7 | dominated by answer 1 |
|  | 3 | Children belong with children | 0 | 8 | 0 | -8 | mood -8 | dominated by answer 1; dominated by answer 2 |
| cold in the bones (`Council_DlmLv_ElderCold`) | 1 | Burn extra wood for A | 4 | 5 | 0 | -1 | wellbeing +4, goods -5 | trade-off |
|  | 2 | B gives up the warmer room | 5 | 8 | 0 | -3 | mood -8, relations +2, wellbeing +4 | trade-off |
|  | 3 | B sits with A by the fire in the evenings | 10 | 1 | 0 | 9 | relations +8, wellbeing +2, work -1 | easy: best by 10 |
|  | 4 | Everyone's cold. A will manage. | 0 | 15 | 0 | -15 | mood -12, trust -3 | dominated by answer 2; dominated by answer 3 |
| the old way and the new (`Council_DlmLv_OldWayNewWay`) | 1 | The old way: A has done it longer | 2 | 6 | 0 | -4 | mood -6, trust +2 | trade-off |
|  | 2 | The new way: let B try it | 2 | 6 | 0 | -4 | mood -6, trust +2 | trade-off |
|  | 3 | Work it out together | 22 | 0 | 7 (30%) | 15 | relations +5, skill +10 | trade-off |
| the worrier's watch (`Council_DlmLv_WorrierWatch`) | 1 | Set a night watch for a while | 2 | 4 | 0 | -2 | mood -3, wellbeing +2, work -1 | dominated by answer 2 |
|  | 2 | B talks it through with A | 8 | 0 | 0 | 7 | relations +5, wellbeing +3, work -0 | trade-off |
|  | 3 | The walls are fine. Get some sleep. | 0 | 6 | 0 | -6 | mood -6 | dominated by answer 1; dominated by answer 2 |
| the colony's record (`Council_DlmLv_RecordKeeper`) | 1 | Make A keeper of the colony's record | 6 | 2 | 0 | 4 | trust +2, skill +3 | trade-off |
|  | 2 | Keep it, but keep it private | 0 | 2 | 0 | -2 | mood -2 | dominated by answer 1 |
|  | 3 | Read out the good parts at supper | 0 | 2 | 0 | -2 | work -2 | dominated by answer 1 |

### Rimfolk_Dilemmas_Lives_Customs.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| the snow day (`Council_Dilemma_SnowDay`) | 1 | A snow day for everyone | 35 | 54 | 0 | -19 | wellbeing +35, work -54 | trade-off |
|  | 2 | The children can play; the rest of us work | 4 | 6 | 0 | -2 | wellbeing +4, work -6 | trade-off |
|  | 3 | Snow means more work, not less | 3 | 4 | 0 | -1 | mood -4, work +3 | trade-off |
| the children's race (`Council_Dilemma_ChildrensRace`) | 1 | Everyone comes out to cheer | 7 | 4 | 0 | 4 | wellbeing +7, work -4 | trade-off |
|  | 2 | The children can race on their own | 1 | 0 | 0 | 1 | wellbeing +1 | trade-off |
|  | 3 | No time for races this summer | 0 | 4 | 0 | -4 | mood -4 | dominated by answer 2 |
| the children's gleaning (`Council_Dilemma_Gleaning`) | 1 | The gleanings are the children's | 3 | 2 | 0 | 0 | work -2, skill +3 | dominated by answer 2 |
|  | 2 | Glean, but it goes to the stores | 3 | 0 | 0 | 3 | skill +3 | trade-off |
|  | 3 | The animals can have the stubble | 0 | 4 | 0 | -4 | mood -4 | dominated by answer 2 |
| a tree for every child (`Council_Dilemma_ChildTrees`) | 1 | Every child plants a tree | 2 | 2 | 0 | 0 | work -2, skill +2 | trade-off |
|  | 2 | Just A's tree, this year | 0 | 0 | 0 | -0 | nothing scored | trade-off |
|  | 3 | Trees take room from the crops | 0 | 4 | 0 | -4 | mood -4 | dominated by answer 2 |

### Rimfolk_Dilemmas_MovingOn.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| family elsewhere (`Council_DlmMove_Family`) | 1 | Let A go to {RELATIVE} | 10 | 94 | 0 | -84 | mood -32, work -2, people -60, outside +10 | trade-off |
|  | 2 | Send {COUNT} silver to {RELATIVE} instead | 0 | 40 | 0 | -40 | mood -40 | trade-off |
|  | 3 | Send for {RELATIVE} to come here, with 300 silver for the road | 60 | 38 | 0 | 22 | goods -30, people +60, outside -8 | easy: best by 62 |
|  | 4 | Ask A to stay | 0 | 52 | 0 | -52 | mood -40, trust -12 | dominated by answer 2 |
| a love from outside (`Council_DlmMove_Love`) | 1 | A goes to be with X | 10 | 94 | 0 | -84 | mood -32, work -2, people -60, outside +10 | trade-off |
|  | 2 | Ask X to join the colony | 120 | 10 | 0 | 110 | mood +60, people +60, outside -10 | easy: best by 159 |
|  | 3 | Things stay as they are | 0 | 49 | 0 | -49 | mood -40, trust -9 | trade-off |
| no room (`Council_DlmMove_Crowded`) | 1 | Promise A a room of A own | 8 | 40 | 0 | -32 | mood -32 | partly scored (PromiseRoom): read by hand |
|  | 2 | Let A move on | 0 | 94 | 0 | -94 | mood -32, work -2, people -60 | partly scored (PromiseRoom): read by hand |
|  | 3 | Ask A to make do | 0 | 49 | 0 | -49 | mood -40, trust -9 | partly scored (PromiseRoom): read by hand |
| still longing for family (`Council_DlmMove_AgainFamily`) | 1 | Let A go to {RELATIVE} | 5 | 94 | 0 | -89 | mood -32, work -2, people -60, outside +5 | trade-off |
|  | 2 | Send for {RELATIVE} to come here, with {COUNT} silver for the road | 60 | 38 | 0 | 22 | goods -30, people +60, outside -8 | easy: best by 111 |
|  | 3 | Insist that A stays | 0 | 218 | 0 | -218 | mood -200, trust -18 | trade-off |
| apart too long (`Council_DlmMove_AgainLove`) | 1 | A goes to be with X | 5 | 94 | 0 | -89 | mood -32, work -2, people -60, outside +5 | trade-off |
|  | 2 | Ask X to join the colony | 120 | 10 | 0 | 110 | mood +60, people +60, outside -10 | easy: best by 199 |
|  | 3 | Insist that A stays | 0 | 218 | 0 | -218 | mood -200, trust -18 | trade-off |
| still no room (`Council_DlmMove_AgainRoom`) | 1 | Let A move on | 0 | 94 | 0 | -94 | mood -32, work -2, people -60 | partly scored (GiveRoom): read by hand |
|  | 2 | Give A someone's room | 0 | 60 | 0 | -60 | mood -60 | partly scored (GiveRoom): read by hand |
|  | 3 | Insist that A stays | 0 | 218 | 0 | -218 | mood -200, trust -18 | partly scored (GiveRoom): read by hand |

### Rimfolk_Dilemmas_New.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| who gets the new part (`Council_DlmV_NewLimb`) | 1 | Fit it to A, whose {SKILL} needs it most | 54 | 26 | 0 | 28 | mood +10, trust +3, wellbeing +15 | trade-off |
|  | 2 | Fit it to B | 68 | 49 | 0 | 18 | trust +14, relations -10, wellbeing +15 | trade-off |
|  | 3 | Keep it until there's one for each | 6 | 16 | 0 | -10 | mood -16, trust +6 | trade-off |
| a bad batch (`Council_DlmV_BadBatch`) | 1 | Scrub the kitchen top to bottom | 16 | 0 | 0 | 16 | mood +9, trust +4, work +3 | trade-off |
|  | 2 | B cooks for a few days | 11 | 20 | 0 | -9 | mood -20, trust +8, skill +3 | trade-off |
|  | 3 | Blame the meat | 26 | 19 | 0 | 7 | mood +1, trust +6 | trade-off |
| the precept broken (`Council_DlmV_PreceptBroken`) | 1 | A quiet word from B | 38 | 20 | 0 | 18 | mood +16, trust +3 | partly scored (Faith): read by hand |
|  | 2 | Penance: A cleans the altar room | 8 | 16 | 0 | -8 | mood -16, trust +5, work +3 | partly scored (Faith): read by hand |
|  | 3 | The council stands by A | 30 | 52 | 0 | -23 | mood -21, trust -1 | partly scored (Faith): read by hand |
| the doctor is the patient (`Council_DlmV_DoctorPatient`) | 1 | A tends A own wounds | 6 | 6 | 0 | 0 | mood -6, trust +3, wellbeing +3 | trade-off |
|  | 2 | B tends A, talked through it | 6 | 6 | 0 | -0 | mood -6, skill +6 | trade-off |
|  | 3 | The best medicine for A | 17 | 26 | 0 | -10 | mood +8, wellbeing +8, goods -25 | trade-off |
| the heat wave's hours (`Council_DlmV_HeatWave`) | 1 | Work by night, sleep through the heat | 51 | 8 | 3 (30%) | 40 | mood +32, trust +3, wellbeing +16, work -11 | trade-off |
|  | 2 | A midday rest in the shade | 80 | 36 | 0 | 44 | mood +44, wellbeing +32, work -32 | trade-off |
|  | 3 | Carry on | 3 | 29 | 0 | -26 | mood -29, trust +3 | trade-off |
| so close (`Council_DlmV_SoClose`) | 1 | Let A work through tonight | 10 | 2 | 1 (35%) | 7 | mood +8, wellbeing -2, work +1 | trade-off |
|  | 2 | Send A to bed | 3 | 12 | 0 | -9 | mood -6, wellbeing +3, work -6 | trade-off |
|  | 3 | A second pair of eyes | 15 | 1 | 0 | 14 | mood +6, relations +6, work -1, skill +3 | trade-off (gated) |
| lessons through the bars (`Council_DlmV_CellLessons`) | 1 | Lessons at the cell | 24 | 18 | 0 | 5 | mood -2, skill +6, outside +2 | trade-off |
|  | 2 | Recruit X first | 20 | 6 | 0 | 14 | mood -6, trust +4, people +15 | trade-off |
|  | 3 | No contact with the prisoner | 7 | 16 | 0 | -9 | mood -16, trust +7 | trade-off |
| the drug bench (`Council_DlmV_DrugBench`) | 1 | Stop the hard drugs | 30 | 15 | 0 | 14 | mood +20, trust +4, goods -10 | trade-off |
|  | 2 | Stop every drug but medicine | 54 | 37 | 0 | 16 | mood +28, trust +9, goods -20 | trade-off |
|  | 3 | Keep making them | 1 | 31 | 0 | -30 | mood -25, trust -5 | trade-off |
| a slave's freedom (`Council_DlmV_SlaveFreedom`) | 1 | Free X into the colony | 20 | 24 | 0 | -4 | mood +6, work -10 | trade-off |
|  | 2 | Free X to go home | 30 | 74 | 0 | -44 | mood +2, people -60, outside +15 | trade-off |
|  | 3 | Not now | 2 | 15 | 0 | -13 | mood -12, trust -1 | trade-off |

### Rimfolk_Dilemmas_Pokemon.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| the Pokémon that keeps coming (`Council_DlmA_ResidentPokemon`) | 1 | Leave food out for it | 0 | 0 | 0 | 0 | nothing scored | partly scored (CatchPokemon, DriveOff, FeedBeast): read by hand |
|  | 2 | Keep the stores shut, and a watch on them | 0 | 33 | 0 | -33 | mood -32, work -1 | partly scored (CatchPokemon, DriveOff, FeedBeast): read by hand |
|  | 3 | Let A try to catch it | 1 | 0 | 0 | 1 | wellbeing +1 | partly scored (CatchPokemon, DriveOff, FeedBeast): read by hand |
|  | 4 | Let it be | 0 | 0 | 0 | 0 | nothing scored | partly scored (CatchPokemon, DriveOff, FeedBeast): read by hand |
|  | 5 | Drive it off | 0 | 0 | 0 | -0 | nothing scored | partly scored (CatchPokemon, DriveOff, FeedBeast): read by hand |

## Second pass

These files were being written to by others in the same round; their findings wait for a second pass after the merge.

### Council_Dilemmas3.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| the young hunter (`Council_Dilemma_YoungHunter`) | 1 | Teach A to shoot | 54 | 18 | 4 (20%) | 31 | mood +16, trust +14, work -1, skill +3 | trade-off |
|  | 2 | A carries the game bags, nothing more | 5 | 9 | 0 | -4 | mood -5, work -1, skill +2 | trade-off |
|  | 3 | Not until A is older | 4 | 16 | 0 (25%) | -12 | mood -8, trust +3, work -1 | trade-off |
|  | 4 | Let B show A the messy part | 6 | 13 | 0 (40%) | -7 | mood -10, trust +2, wellbeing +1, skill +4 | trade-off |
| where the dead go (`Council_Dilemma_WhereTheDeadGo`) | 1 | Say the dead go somewhere warm | 36 | 7 | 0 | 29 | mood +13, trust +4, relations +12 | trade-off |
|  | 2 | Tell the plain truth | 5 | 23 | 0 (30%) | -18 | mood -13, trust +1, wellbeing +1, skill +2 | trade-off |
|  | 3 | Hold a funeral for the animal | 51 | 12 | 0 | 40 | mood +25, trust +4, relations +14, work -2, goods -1 | trade-off |
|  | 4 | Change the subject | 7 | 10 | 6 (35%) | -9 | mood -15, trust +6 | trade-off |
| a friend past the wall (`Council_Dilemma_WildFriend`) | 1 | Let A keep feeding it | 33 | 36 | 4 (25%) | -7 | mood -12, trust +8, work -1, goods -2 | trade-off |
|  | 2 | Chase it off for good | 20 | 68 | 0 | -48 | mood -20, trust -6, relations -22 | trade-off |
|  | 3 | Let B deal with it | 7 | 142 | 5 (20%) | -139 | mood -111, trust -5, relations -22, work -0 | trade-off |
|  | 4 | Say nothing | 25 | 16 | 11 (30%) | -3 | mood +3, trust -4, work -1, goods -1 | trade-off |
| the bite (`Council_Dilemma_PetBite`) | 1 | A pays 30 silver | 20 | 12 | 0 | 8 | mood +10, trust +2, goods -3 | trade-off |
|  | 2 | Keep {PET_nameDef} penned for a while | 24 | 9 | 5 (25%) | 10 | mood +14, trust +2, relations -6, wellbeing +1 | trade-off |
|  | 3 | Turn {PET_nameDef} loose | 34 | 229 | 0 | -195 | mood -167, trust +5, relations -22, goods -10 | trade-off |
|  | 4 | Let it go | 20 | 29 | 3 (15%) | -12 | mood +6, trust -7, relations -11 | trade-off |
| an animal in decline (`Council_Dilemma_PetFading`) | 1 | Nurse {PET_nameDef} | 28 | 6 | 21 (40%) | 1 | mood +2, trust +6, work -5, goods -2 | trade-off |
|  | 2 | Take {PET_nameDef} on one last walk | 3 | 115 | 0 | -111 | mood -100, trust -1, work -0, goods -10 | trade-off |
|  | 3 | Let B decide | 94 | 1 | 114 (50%) | -21 | mood -23, trust -1, relations +9, goods -6 | trade-off |
|  | 4 | Let {PET_nameDef} be | 0 | 33 | 0 | -33 | mood -30, trust -3 | trade-off |
| a hungry look (`Council_Dilemma_PetPot`) | 1 | {PET_nameDef} is family | 26 | 36 | 5 (20%) | -15 | mood -10, relations -4, work -1 | trade-off |
|  | 2 | Feed B extra rations instead | 10 | 24 | 0 | -13 | mood -16, trust +4, goods -2 | trade-off |
|  | 3 | Fetch the pot | 1 | 274 | 0 | -273 | mood -219, trust -22, relations -22, wellbeing +1, goods -10 | trade-off |
|  | 4 | B apologizes to {PET_nameDef} | 28 | 26 | 9 (25%) | -7 | mood +4, trust -3, relations -6, wellbeing +3, work -6 | trade-off |
| a love letter (`Council_Dilemma_LoveLetter`) | 1 | Make B apologize at dinner | 6 | 66 | 6 (25%) | -67 | mood -60, relations -6, wellbeing +1, work -2 | trade-off |
|  | 2 | Give B the worst chores for a week | 29 | 94 | 0 | -65 | mood -55, trust -2, relations -11, work +3 | trade-off |
|  | 3 | Let A read the rest aloud | 46 | 2 | 51 (40%) | -7 | trust +1, relations -9, wellbeing +3, work -2 | trade-off |
|  | 4 | Leave it be | 3 | 97 | 0 | -94 | mood -67, trust -8, relations -22, wellbeing +3 | trade-off |
| a funeral in advance (`Council_Dilemma_OwnFuneral`) | 1 | Build A a coffin now | 51 | 11 | 0 | 40 | mood +34, trust +8, goods -2 | trade-off |
|  | 2 | Hold a living funeral | 73 | 7 | 29 (30%) | 37 | mood +36, trust +4, wellbeing +2, work -4, goods -1 | trade-off |
|  | 3 | Let A pick a grave site | 18 | 5 | 0 | 13 | mood +10, trust +3 | trade-off |
|  | 4 | Say nothing and hope it passes | 7 | 15 | 4 (20%) | -12 | mood -12, trust +4, work -4 | trade-off |
| the sleepwalker (`Council_Dilemma_Sleepwalker`) | 1 | Bolt A's door at night | 24 | 38 | 3 (25%) | -16 | mood -12, trust -2, work -3 | trade-off |
|  | 2 | B sits up with A at night | 62 | 4 | 26 (30%) | 32 | mood +24, trust +1, relations +9, wellbeing +1, work -4 | trade-off |
|  | 3 | Tie a bell to A's ankle | 4 | 15 | 4 (30%) | -15 | mood -12, trust -2, wellbeing +3, work -4 | trade-off |
|  | 4 | A can't help it | 20 | 22 | 5 (25%) | -6 | mood +8, trust -6, work -8 | trade-off |
| a side deal (`Council_Dilemma_SideDeal`) | 1 | Confiscate the {THING} | 35 | 44 | 8 (20%) | -17 | mood -15, trust +1, relations -4, work -3, goods +4 | trade-off |
|  | 2 | Take the {THING} and put A to work | 32 | 65 | 0 | -33 | mood -7, trust -8, relations -22, goods +4 | trade-off |
|  | 3 | Make A the colony's trader | 39 | 77 | 0 (30%) | -38 | mood +2, trust +1, relations -16, wellbeing +1 | trade-off |
|  | 4 | Let the deal stand | 20 | 34 | 16 (25%) | -30 | mood -18, trust -12 | trade-off |
| who saved us (`Council_Dilemma_Credit`) | 1 | It was A | 45 | 74 | 4 (25%) | -32 | mood -6, relations -22, work -4 | trade-off |
|  | 2 | It was B | 45 | 74 | 4 (25%) | -32 | mood -6, relations -22, work -4 | trade-off |
|  | 3 | Ask the witnesses | 25 | 24 | 17 (35%) | -16 | mood -19, trust -8, relations +15, wellbeing +2, work -5 | trade-off |
|  | 4 | Credit everyone who fought | 3 | 18 | 0 | -15 | mood -12, trust -3 | trade-off |
| a letter home (`Council_Dilemma_LetterHome`) | 1 | Send word properly: 60 silver | 56 | 13 | 33 (25%) | 11 | mood +11, trust +7, work -1, goods -6 | trade-off |
|  | 2 | A short note, the cheap way: 20 silver | 3 | 8 | 20 (40%) | -25 | mood -23, goods -2 | trade-off |
|  | 3 | No: a message can be followed back | 1 | 64 | 4 (20%) | -67 | mood -55, trust -8, work -4 | trade-off |

### Council_Dilemmas_Customs.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| the planting day (`Council_Dilemma_PlantingBlessing`) | 1 | Make a day of the planting | 24 | 2 | 0 | 22 | trust +4, work +1, skill +16 | partly scored (PlantingSkipped): read by hand |
|  | 2 | A sows the first row, the rest in its turn | 6 | 0 | 0 | 6 | trust +2, work +3, skill +2 | partly scored (PlantingSkipped): read by hand |
|  | 3 | The seed goes in like any other work | 0 | 3 | 0 | -3 | trust -3 | partly scored (PlantingSkipped): read by hand |
| the remembrance walk (`Council_Dilemma_RemembranceWalk`) | 1 | Walk to the graves together | 105 | 2 | 0 | 103 | mood +86, trust +4, wellbeing +14, work -2 | trade-off |
|  | 2 | Those who want to can go | 71 | 0 | 0 | 70 | mood +69, trust +2 | trade-off |
|  | 3 | The living have work to do | 5 | 6 | 0 | -1 | mood -4, trust +3 | trade-off |
| the midsummer swim (`Council_Dilemma_MidsummerSwim`) | 1 | Everyone to the water | 106 | 6 | 0 | 100 | mood +80, trust +6, wellbeing +20, work -6 | trade-off |
|  | 2 | A swim once the day's work is done | 75 | 4 | 0 | 72 | mood +64, trust +2, wellbeing +10, work -4 | trade-off |
|  | 3 | Keep everyone working | 0 | 17 | 0 | -17 | mood -14, trust -3 | trade-off |
| the harvest dance (`Council_Dilemma_HarvestDance`) | 1 | Dance until the fire burns down | 138 | 24 | 0 | 114 | mood +93, trust +6, wellbeing +36, work -21 | trade-off |
|  | 2 | One dance, then bed | 90 | 2 | 0 | 87 | mood +80, trust +2, wellbeing +8, work -2 | trade-off |
|  | 3 | There's work in the morning | 0 | 32 | 0 | -32 | mood -29, trust -3 | trade-off |
| first-frost soup (`Council_Dilemma_FirstFrostSoup`) | 1 | One big pot for everyone | 70 | 9 | 0 | 61 | mood +64, trust +6, work -4, goods -6 | trade-off |
|  | 2 | Soup for those working outside | 42 | 0 | 0 | 41 | mood +42 | trade-off |
|  | 3 | Everything goes to the winter stores | 4 | 6 | 0 | -2 | mood -4, trust +2 | trade-off |
| lantern night (`Council_Dilemma_LanternNight`) | 1 | Lanterns along the walls | 102 | 6 | 0 | 96 | mood +96, trust +6, work -2, goods -4 | trade-off |
|  | 2 | One lantern at the gate | 67 | 1 | 0 | 66 | mood +67, work -1, goods -1 | trade-off |
|  | 3 | Save the fuel for the cold | 4 | 16 | 0 | -12 | mood -14, trust +2 | trade-off |

### Council_Dilemmas_Customs2.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| the sun's return (`Council_Dilemma_SunsReturn`) | 1 | Everyone up to greet the sun | 86 | 5 | 0 | 81 | mood +72, trust +4, wellbeing +10, work -4, goods -2 | trade-off |
|  | 2 | Whoever's awake can watch it | 54 | 0 | 0 | 54 | mood +54 | trade-off |
|  | 3 | The sun comes back without our help | 5 | 6 | 0 | -1 | mood -4, trust +3 | trade-off |
| the rain welcome (`Council_Dilemma_RainWelcome`) | 1 | Everyone out in the rain | 18 | 2 | 0 | 16 | trust +6, wellbeing +12, work -2 | trade-off |
|  | 2 | Catch it in every pot, then back to work | 8 | 0 | 0 | 8 | trust +2, wellbeing +6 | trade-off |
|  | 3 | It's only rain | 5 | 6 | 0 | -1 | mood -4, trust +3 | trade-off |
| the blessing of the waters (`Council_Dilemma_WaterBlessing`) | 1 | Everyone to the water's edge | 24 | 2 | 0 | 22 | trust +24, work -2, goods -1 | partly scored (Faith): read by hand |
|  | 2 | A can bless it alone | 4 | 0 | 0 | 4 | trust +4 | partly scored (Faith): read by hand |
|  | 3 | Bread is for eating | 0 | 7 | 0 | -7 | mood -4, trust -3 | partly scored (Faith): read by hand |
| the marsh lights (`Council_Dilemma_MarshLights`) | 1 | Everyone out with the torches | 54 | 4 | 0 | 50 | mood +48, trust +6, work -2, goods -2 | trade-off |
|  | 2 | One torch at each door | 36 | 1 | 0 | 35 | mood +36, goods -1 | trade-off |
|  | 3 | Keep the wood for the fires | 4 | 6 | 0 | -2 | mood -4, trust +2 | trade-off |
| the high watch (`Council_Dilemma_HighWatch`) | 1 | Everyone up to the high ground | 14 | 2 | 0 | 12 | trust +8, wellbeing +6, work -2 | trade-off |
|  | 2 | Those who want to can climb | 5 | 1 | 0 | 3 | trust +2, wellbeing +3, work -1 | trade-off |
|  | 3 | We can see enough from here | 0 | 7 | 0 | -7 | mood -4, trust -3 | trade-off |
| the night vigil (`Council_Dilemma_NightVigil`) | 1 | Keep the vigil together | 80 | 5 | 0 | 75 | mood +65, trust +6, wellbeing +10, work -5 | partly scored (Faith): read by hand |
|  | 2 | A short vigil, then bed | 10 | 2 | 0 | 9 | mood +9, trust +2, work -2 | partly scored (Faith): read by hand |
|  | 3 | Everyone needs their sleep | 0 | 8 | 0 | -8 | mood -4, trust -4 | partly scored (Faith): read by hand |
| the tree-singing (`Council_Dilemma_TreeSinging`) | 1 | Everyone out to sing | 16 | 4 | 0 | 12 | trust +6, wellbeing +10, work -4 | partly scored (Faith): read by hand |
|  | 2 | A song at dusk, before supper | 5 | 1 | 0 | 4 | trust +2, wellbeing +4, work -1 | partly scored (Faith): read by hand |
|  | 3 | Trees don't listen | 0 | 8 | 0 | -8 | mood -4, trust -4 | partly scored (Faith): read by hand |
| the ancestors' supper (`Council_Dilemma_AncestorsSupper`) | 1 | A place at the table for every one of them | 83 | 9 | 0 | 74 | mood +65, trust +6, wellbeing +12, work -4, goods -6 | partly scored (Faith): read by hand |
|  | 2 | Say their names at supper | 27 | 2 | 0 | 26 | mood +26, trust +2, work -2 | partly scored (Faith): read by hand |
|  | 3 | The dead don't need feeding | 0 | 8 | 0 | -8 | mood -4, trust -4 | partly scored (Faith): read by hand |

### Council_Dilemmas_Health.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| a day away (`Council_DlmH_DayAway`) | 1 | Give A the day | 20 | 8 | 0 | 12 | mood +12, trust +3, wellbeing +5, work -8 | trade-off |
|  | 2 | The colony needs its doctor | 0 | 15 | 0 | -15 | mood -12, trust -2, wellbeing -2 | trade-off |
|  | 3 | B keeps A company for the day | 20 | 14 | 0 | 6 | mood +4, relations +7, wellbeing +9, work -14 | trade-off |
| back too soon (`Council_DlmH_BackTooSoon`) | 1 | Let A back to light work | 12 | 0 | 5 (30%) | 7 | mood +4, trust +2 | partly scored (Convalescence): read by hand |
|  | 2 | Bed until the strength comes back | 0 | 7 | 0 | -7 | mood -7 | partly scored (Convalescence): read by hand |
|  | 3 | Give A something to do from bed | 8 | 0 | 0 | 8 | mood +6, skill +2 | partly scored (Convalescence): read by hand |
| a say in the operation (`Council_DlmH_FamilySay`) | 1 | Go ahead as planned | 4 | 15 | 0 | -11 | mood -8, trust -3 | partly scored (PutOffSurgery): read by hand |
|  | 2 | Put it off until they've talked it through | 12 | 2 | 0 | 10 | mood +7, trust +3, work -0 | partly scored (PutOffSurgery): read by hand |
|  | 3 | B stays at A's side through it | 25 | 0 | 0 | 25 | mood +21, relations +4, work -0 | partly scored (PutOffSurgery): read by hand |
| the midwife's apprentice (`Council_DlmH_MidwifeApprentice`) | 1 | B learns from A | 35 | 5 | 0 | 30 | mood +24, wellbeing -2, work -3, skill +11 | trade-off |
|  | 2 | Not now | 3 | 4 | 0 | -1 | mood -4, trust +3 | trade-off |
|  | 3 | B watches the next birth | 21 | 0 | 0 | 21 | mood +15, skill +6 | trade-off |

### Council_Dilemmas_Seasons.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| midsummer night (`Council_Dilemma_Midsummer`) | 1 | Light the bonfire | 112 | 40 | 0 | 72 | mood +80, trust +8, work -11, goods -5 | trade-off |
|  | 2 | Save the wood this year | 0 | 33 | 0 | -33 | mood -29, trust -4 | trade-off |
|  | 3 | A small fire, no drinking | 56 | 5 | 0 | 51 | mood +48, wellbeing +8, work -4, goods -1 | trade-off |
| harvest home (`Council_Dilemma_HarvestHome`) | 1 | A harvest feast for everyone | 153 | 43 | 0 | 110 | mood +115, trust +9, work -7, goods -7 | trade-off |
|  | 2 | Store every scrap for winter | 0 | 47 | 0 | -47 | mood -41, trust -6 | trade-off |
|  | 3 | A modest supper | 89 | 9 | 0 | 81 | mood +86, trust +3, work -4, goods -5 | trade-off |
| the longest night (`Council_Dilemma_Midwinter`) | 1 | Extra rations for the long night | 102 | 33 | 0 | 69 | mood +77, trust +6, work -7, goods -7 | trade-off |
|  | 2 | Stories by the fire, no feast | 59 | 7 | 0 | 52 | mood +48, trust +3, wellbeing +8, work -7 | trade-off |
|  | 3 | Just another night | 0 | 26 | 0 | -26 | mood -26 | trade-off |
| naming the year (`Council_Dilemma_NameTheYear`) | 1 | The year of A | 57 | 16 | 0 | 41 | mood +32, trust +9 | trade-off |
|  | 2 | Name it for those we lost | 86 | 5 | 0 | 82 | mood +86, trust -3, work -2 | trade-off |
|  | 3 | The year of the muffalo | 72 | 4 | 0 | 68 | mood +72, trust -4 | trade-off |
| the spoils (`Council_Dilemma_Spoils`) | 1 | A earned first pick | 22 | 14 | 0 | 8 | mood +1, trust +8 | trade-off |
|  | 2 | Share everything equally | 9 | 6 | 0 | 3 | mood -6, trust +9 | trade-off |
|  | 3 | The fighters get the spoils | 9 | 14 | 0 | -5 | mood -14, trust +6, work +3 | trade-off |
|  | 4 | Let the quartermaster share it out | 14 | 0 | 13 (35%) | 1 | mood -10, trust +11 | trade-off (gated) |
| the enemy dead (`Council_Dilemma_EnemyDead`) | 1 | Give them graves | 15 | 19 | 0 | -5 | mood -12, trust +8 | trade-off |
|  | 2 | Burn them | 12 | 10 | 0 | 3 | mood -2, trust +4 | trade-off |
|  | 3 | Drag them to the treeline | 9 | 102 | 0 | -93 | mood -102, trust +9 | trade-off |
|  | 4 | One grave for all of them | 12 | 0 | 0 | 12 | mood +9, trust +3 | trade-off |
|  | 5 | Into the crematorium | 6 | 10 | 0 | -3 | mood -10, trust +6 | trade-off |
| after the battle (`Council_Dilemma_VictoryFeast`) | 1 | A victory feast | 108 | 28 | 0 | 80 | mood +82, trust +12, work -7, goods -7 | trade-off |
|  | 2 | Repairs first, celebrations later | 4 | 29 | 0 | -24 | mood -29, trust +2, work +3 | trade-off |
|  | 3 | A quick hot meal, then the walls | 48 | 7 | 0 | 41 | mood +48, work -2, goods -5 | trade-off |
|  | 4 | Have the best cook stretch half the food | 100 | 12 | 24 (50%) | 64 | mood +72, trust +2, work -5, goods -5 | trade-off (gated) |

### Council_Dilemmas_Strangers.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| a reason to stay (`Council_DlmS_WantsToStay`) | 1 | Welcome X in | 114 | 15 | 0 | 99 | mood +48, trust +6, people +60, outside -15 | trade-off |
|  | 2 | Pay {FACTION} to let X go | 116 | 27 | 0 | 89 | mood +44, trust +8, goods -20, people +60, outside -3 | trade-off |
|  | 3 | X leaves with X people | 0 | 34 | 4 (20%) | -38 | mood -30, trust -4, work -4 | trade-off |
|  | 4 | Ask our best talker to square it with {FACTION} | 114 | 4 | 8 (50%) | 102 | mood +45, trust +6, people +60, outside -10 | trade-off (gated) |
| a guest's tongue (`Council_DlmS_RudeGuest`) | 1 | Demand an apology | 20 | 0 | 14 (35%) | 6 | mood +10, trust +3, work -5, outside -2 | trade-off |
|  | 2 | Let it go, they're guests | 0 | 22 | 0 | -22 | mood -17, trust -4 | trade-off |
|  | 3 | Throw the lot of them out | 26 | 14 | 0 | 12 | mood +16, trust +6, outside -10 | trade-off |
|  | 4 | Ask our best talker to settle it | 29 | 0 | 20 (40%) | 9 | mood +8, trust +2, outside -1 | trade-off (gated) |
| the trader's eye (`Council_DlmS_HeirloomOffer`) | 1 | Sell it | 27 | 36 | 0 | -9 | mood -32, trust -3, work -1, goods +27 | trade-off |
|  | 2 | Haggle until it hurts | 38 | 38 | 0 (35%) | -0 | mood -18, trust -4, goods +24, outside -1 | trade-off |
|  | 3 | It stays with A | 11 | 14 | 0 | -3 | mood -6, trust +3 | trade-off |
| a voice through the door (`Council_DlmS_PrisonerPlea`) | 1 | Let X go home | 35 | 10 | 0 | 25 | mood +14, trust +6, outside +5 | trade-off |
|  | 2 | Hear X out properly | 22 | 0 | 0 | 21 | trust +2, relations +11, work -0, skill +4, outside +5 | trade-off |
|  | 3 | Keep the door locked | 9 | 12 | 0 | -3 | mood -12, trust +9 | trade-off |
| the one still breathing (`Council_DlmS_FallenPlea`) | 1 | Take X prisoner | 1 | 4 | 0 | -3 | mood -4, trust +1 | partly scored (Capture, EndIt): read by hand |
|  | 2 | End it quickly | 6 | 33 | 0 | -27 | mood -25, trust -2 | partly scored (Capture, EndIt): read by hand |
|  | 3 | Leave X where X lies | 0 | 11 | 0 | -11 | mood -10, trust -1 | partly scored (Capture, EndIt): read by hand |
| hungry at the gate (`Council_DlmS_Beggars`) | 1 | Fill their sacks | 15 | 10 | 0 | 5 | mood -5, goods -5, outside +15 | trade-off |
|  | 2 | Give them silver instead | 8 | 8 | 0 | -0 | mood -2, goods -6, outside +8 | trade-off |
|  | 3 | Turn them away | 0 | 13 | 0 | -13 | mood -10, trust -1, outside -2 | trade-off |
|  | 4 | Send them home with seed, and a grower's advice | 16 | 2 | 0 | 14 | mood +12, work -0, goods -2, outside +4 | trade-off (gated) |
| teeth marks (`Council_DlmS_PetBit`) | 1 | Pay for the bite | 10 | 11 | 0 | -1 | mood -6, goods -5, outside +10 | trade-off |
|  | 2 | Stand by {PET_definite} | 20 | 12 | 0 | 8 | mood +15, trust +4, outside -12 | trade-off |
|  | 3 | Hand {PET_definite} over to them | 12 | 112 | 0 | -100 | mood -90, trust -12, goods -10, outside +12 | trade-off |
| the den in the brush (`Council_DlmS_PredatorDen`) | 1 | Hunt it | 5 | 8 | 0 | -2 | mood -6, trust +4 | trade-off |
|  | 2 | Let B try to tame it | 14 | 6 | 0 | 7 | mood +6, trust +2 | trade-off |
|  | 3 | Leave it alone | 5 | 13 | 0 (30%) | -8 | mood -13, trust +5 | trade-off |
| can I keep it (`Council_DlmS_ChildPet`) | 1 | Let A keep it | 40 | 2 | 0 | 37 | mood +18, trust +4, goods +15 | trade-off |
|  | 2 | Let the handlers try, properly | 29 | 0 | 0 | 29 | mood +16, trust +3, skill +10 | trade-off |
|  | 3 | No, and that's final | 8 | 18 | 5 (25%) | -14 | mood -16, trust +7, work -5 | trade-off |
| a titled critic (`Council_DlmS_NobleSneer`) | 1 | Say nothing and smile | 6 | 23 | 0 | -17 | mood -20, trust -3, outside +6 | trade-off |
|  | 2 | Let A answer back | 24 | 8 | 0 | 16 | mood +20, trust +4, outside -8 | trade-off |
|  | 3 | Ask what would please X | 10 | 3 | 0 | 7 | mood -3, skill +8, outside +2 | trade-off |
| the miracle tonic (`Council_DlmS_Tonic`) | 1 | Buy A the tonic | 20 | 10 | 18 (50%) | -8 | wellbeing +1, work +1, goods -10 | trade-off |
|  | 2 | Make X drink some first | 22 | 6 | 8 (40%) | 8 | mood +9, work +1, goods -4, outside +2 | trade-off |
|  | 3 | Keep the silver | 0 | 1 | 0 | -1 | mood -1 | trade-off |
|  | 4 | Have our doctor mix a sleeping draught | 13 | 2 | 0 | 11 | mood +12, work +1, goods -2 | trade-off (gated) |
| a face from before (`Council_DlmS_OldAcquaintance`) | 1 | Pay for X silence | 22 | 19 | 4 (20%) | -1 | mood +8, trust +6, goods -15 | partly scored (RevealSecret): read by hand |
|  | 2 | Let X talk | 0 | 30 | 0 | -30 | mood -24, trust -6 | partly scored (RevealSecret): read by hand |
|  | 3 | Threaten X | 16 | 14 | 11 (40%) | -8 | mood +5, trust +3, work -6, outside -10 | partly scored (RevealSecret): read by hand |
| the beast at the tree line (`Council_DlmS_BeastReturns`) | 1 | Hunt it down now | 12 | 4 | 4 (30%) | 4 | mood +4 | trade-off |
|  | 2 | Keep everyone inside | 3 | 43 | 0 | -40 | mood -43, trust +3 | trade-off |
|  | 3 | Try to tame it | 0 | 22 | 0 (35%) | -22 | mood -22 | trade-off |
| the one in the cell (`Council_DlmS_NemesisInChains`) | 1 | Execute X | 28 | 25 | 0 | 4 | mood +1, trust +3 | trade-off |
|  | 2 | Let X rot in there | 5 | 10 | 0 | -5 | mood -10, trust +5 | trade-off |
|  | 3 | Try to recruit X | 15 | 84 | 0 | -70 | mood -80, trust -4, people +15 | trade-off |
| a fair offer (`Council_DlmS_BuyTheAnimal`) | 1 | Sell {PET_objective} | 12 | 35 | 0 | -23 | mood -17, trust -8, goods +2 | trade-off |
|  | 2 | Name a price X won't pay | 25 | 35 | 0 (60%) | -10 | mood +1, trust -3, goods +6, outside -1 | trade-off |
|  | 3 | Not for sale | 15 | 27 | 0 | -12 | mood -10, trust +3, outside -5 | trade-off |
| the traveling healer (`Council_DlmS_Healer`) | 1 | Pay the healer | 29 | 9 | 9 (30%) | 11 | mood +12, work -1, skill +7, goods -8 | trade-off |
|  | 2 | Pay X to teach B | 31 | 13 | 0 | 18 | mood +9, trust +3, work -1, skill +13, goods -6 | trade-off |
|  | 3 | Keep the silver | 0 | 8 | 0 | -8 | mood -6, trust -2 | trade-off |
|  | 4 | Let B treat it, with our own medicine | 37 | 6 | 20 (50%) | 11 | mood +13, work -1, skill +4, goods -5 | trade-off (gated) |
| family in the cells (`Council_DlmS_KinInChains`) | 1 | Let X go home | 34 | 4 | 0 | 31 | mood +21, trust +4, outside +5 | trade-off |
|  | 2 | Let A talk X round | 42 | 0 | 0 | 42 | mood +12, trust +3, work -0, people +15, outside +12 | trade-off |
|  | 3 | Treat X like any other prisoner | 7 | 32 | 0 | -24 | mood -30, trust +6 | trade-off |

### Rimfolk_Dilemmas_Camps.xml

| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |
|---|---|---|---|---|---|---|---|---|
| one of them wants to stay (`Rimfolk_DlmC_StayBehind`) | 1 | Take X in, whatever the camp says | 60 | 12 | 0 | 48 | people +60, outside -12 | trade-off |
|  | 2 | Have A speak to the camp for X | 65 | 0 | 28 (35%) | 37 | mood -4, skill +1, people +39, outside +1 | trade-off |
|  | 3 | Pay the camp for the lost hands ({COUNT} silver) | 63 | 12 | 0 | 51 | goods -12, people +60, outside +3 | trade-off |
|  | 4 | Tell X to go on with X people | 3 | 12 | 0 | -9 | mood -12, outside +3 | trade-off |
| missing from the camp (`Rimfolk_DlmC_Theft`) | 1 | Search the colony for it ourselves | 4 | 1 | 0 | 3 | trust +2, work -1, outside +3 | trade-off |
|  | 2 | Pay them what it was worth ({COUNT} silver) | 5 | 15 | 0 | -10 | mood -9, goods -6, outside +5 | trade-off |
|  | 3 | Our people don't steal | 3 | 9 | 0 | -6 | trust +3, outside -9 | trade-off |
|  | 4 | Let A go and answer for it | 10 | 0 | 9 (35%) | 1 | mood -4, trust +4, outside +2 | trade-off |
| one of theirs in the storeroom (`Rimfolk_DlmC_CaughtInStores`) | 1 | Let X keep the sack, and add to it | 7 | 4 | 0 | 3 | mood -1, goods -3, outside +7 | trade-off |
|  | 2 | X works it off | 3 | 1 | 0 | 2 | goods +3, outside -1 | trade-off |
|  | 3 | Take it to their elders | 2 | 0 | 0 | 2 | outside +2 | trade-off |
|  | 4 | Keep the camp out of the colony | 4 | 16 | 0 | -12 | mood -5, trust +4, outside -12 | trade-off |
| their beasts in the fields (`Rimfolk_DlmC_HerdInFields`) | 1 | Drive them out now | 0 | 3 | 0 | -3 | outside -3 | partly scored (CampGives, CampHerd): read by hand |
|  | 2 | Have B walk them back with their people | 6 | 0 | 0 | 5 | skill +2, outside +3 | partly scored (CampGives, CampHerd): read by hand (gated) |
|  | 3 | Make them pay for the damage | 0 | 5 | 0 | -5 | outside -5 | partly scored (CampGives, CampHerd): read by hand |
|  | 4 | Let them graze; it's a few rows | 5 | 3 | 0 | 2 | mood -3, outside +5 | partly scored (CampGives, CampHerd): read by hand |
| an old quarrel at the camp (`Rimfolk_DlmC_Feud`) | 1 | Our land, our guests: tell {ENEMY} so | 10 | 6 | 0 | 4 | outside +4 | partly scored (CampMovesOn): read by hand |
|  | 2 | Bring both sides to the colony's table | 12 | 10 | 6 (35%) | -4 | work -6, skill +2, goods -4, outside +4 | partly scored (CampMovesOn): read by hand (gated) |
|  | 3 | Ask the camp to move on before there's trouble | 4 | 3 | 0 | 1 | outside +1 | partly scored (CampMovesOn): read by hand |
|  | 4 | It's their quarrel, not ours | 0 | 6 | 0 | -6 | outside -6 | partly scored (CampMovesOn): read by hand |
| a friend from the camp (`Rimfolk_DlmC_Playmates`) | 1 | Let them play | 2 | 1 | 0 | 1 | wellbeing +2, work -1 | partly scored (CampMood): read by hand |
|  | 2 | Have X's family to supper | 4 | 10 | 0 | -6 | wellbeing +2, work -6, goods -4, outside +2 | partly scored (CampMood): read by hand |
|  | 3 | Keep the children apart | 0 | 8 | 0 | -8 | mood -6, outside -2 | partly scored (CampMood): read by hand |
| their feast day (`Rimfolk_DlmC_Festival`) | 1 | Open the hall to them | 10 | 10 | 0 | 0 | work -6, goods -4, outside +10 | trade-off |
|  | 2 | Send food down and go to theirs | 8 | 8 | 0 | 0 | work -5, goods -3, outside +8 | trade-off |
|  | 3 | Whoever wants to can go down | 4 | 4 | 0 | 0 | work -4, outside +4 | trade-off |
|  | 4 | Not this year | 0 | 2 | 0 | -2 | outside -2 | trade-off |
| the fee falls short (`Rimfolk_DlmC_FeeShort`) | 1 | Take a beast instead | 1 | 0 | 0 | 1 | outside +1 | partly scored (CampFee, CampMovesOn, SpawnAnimal): read by hand |
|  | 2 | Take a day's work instead | 5 | 0 | 0 | 5 | goods +4, outside +1 | partly scored (CampFee, CampMovesOn, SpawnAnimal): read by hand |
|  | 3 | Forgive it this once | 6 | 0 | 0 | 6 | outside +6 | partly scored (CampFee, CampMovesOn, SpawnAnimal): read by hand |
|  | 4 | The terms are the terms | 0 | 5 | 1 (30%) | -6 | outside -6 | partly scored (CampFee, CampMovesOn, SpawnAnimal): read by hand |
