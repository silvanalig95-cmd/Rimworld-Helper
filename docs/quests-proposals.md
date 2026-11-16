# Quests and storylines (proposal and plan, 16 November 2026)

The player, 13 November 2026: "the next thing to include will be more quests, and quests that are storylines across
multiple quests". On 16 November 2026: "go ahead with quests and storylines, make sure they are written in the more clear
style i mentioned earlier on", then "and while quests that require you to go out of your base are interesting, i hardly
ever reach the stage where I can consistently go out of base and it's worth it, so keep these quests to a minimum",
"unless it's like within only very few tiles".

## What there is today

- **Missions.** About 50 quests in the game's Quests tab (`Source/ColonyCouncil/Missions/`, `Defs/MissionDefs/`). A
  neighbour asks, the colony accepts or lets it expire, and the mission ends in success or failure. Each one stands
  alone: a feast, a runaway, a shrine, hands lent, goods to make, a guest of rank, refugees, a lost child, and so on.
- **Story chains.** Ten stories told over weeks in letters and event cards (`Happenings/Workers_Chains2.cs`): the door
  in the hill, the prophet, the feud, the debt, the fever, the apprentice, the seeds, the play, the letters and the
  expedition. They are not quests: nothing shows in the Quests tab, and there are no terms or deadlines.
- **Places on the world map.** A few things already make sites: a region's legend beast, the raiders' camp and a
  thief's trail.

## How the new quests work (the rules for all of them)

1. **At home first.** Nearly every quest plays out on the colony's own map. People come to the colony, and the
   colony makes, keeps, hosts, guards, finds, builds or decides something. Goods are collected by the people who
   asked, as the delivery missions already do.
2. **Away only when it is close, and rarely.** A quest that sends colonists out is offered only when the place is at
   most 3 tiles from the colony (about a day on foot), and at most one in ten quests is like that. Where a storyline
   would go further, the neighbours go themselves and the colony helps from home (supplies, a lent hand, a warning).
3. **A storyline is a run of quests.** Each part is its own quest in the Quests tab, titled "Part 2: ..." with the
   storyline's name. How the last part ended (done, done well, failed, refused, or the choice made in it) decides
   which part comes next and how the storyline ends. Each part's description opens with one or two plain sentences
   on what happened before.
4. **Choices inside a quest.** Some parts reach a moment where a card asks what to do (hand the thief over or let him
   go). The answer is the branch, and it plays out on the map.
5. **Pace.** One storyline runs at a time; a second can start only after the first has been running for a season.
   The next part comes a few days to two weeks after the last one ends. The storyteller's direction (Ways to play)
   applies as it does for all Rimfolk events.
6. **Every age.** Who comes and what they want takes the form of the giver's age and the colony's means: a tribal
   herder's ewes, an industrial depot's crates and a spacer's sealed cases. Nothing is held back for the age.
7. **Harm.** Fights and illness come only where a part says so. They happen on the map, are shown, and stay within
   the shared harm and illness budgets.
8. **Plain words.** Titles, terms, letters and cards say what is asked, who asks and why, what the colony gets, and
   what happens if it fails, in ordinary words (CLAUDE.md "Plain, neutral text", `docs/text-style-samples.md`).

## Storylines (multi-part)

Each storyline lists who asks, then its parts with their branches, and how it ends.

**S1. The factor's goods** (the road league; any trading people without one). Built, released 0.262.0 (DESIGN.md, "Quests and storylines"). 1. A factor asks the colony to keep
their crates for a season; the crates must stay dry and must not be stolen or spent. 2a. If they are kept well, a
rival trader comes and offers to buy them behind the factor's back. A card gives three choices: sell, refuse, or tell
the factor. 2b. If some are lost, the colony has ten days to make good what was lost. 3. Endings: refused or told,
and the factor stops by every season with a share of the trade. Sold, and the factor finds out: a debt, or their
guards come to the gate. Not made good, and the league stops sending traders for a year.

**S2. The sickness in the next town** (the hospitaller order; any healing people). 1. The town asks for medicine.
2. Three to six of their sick are brought to be cared for in the colony's beds for some days. A colonist who tends them
may catch it, at the game's rate and shown. 3. If all recover, the order sends a healer who stays and teaches the
colony's doctor. If some die, their kin come for the bodies, and the colony chooses to bury them here or hand them
over.

**S3. The strayed herd** (the herding folk; any herding people). 1. Their animals have strayed onto the colony's
land. Pen them before predators take them or they wander off the map. 2. When their herders come, they ask the colony
to keep part of the herd over the winter, fed. 3. In spring they collect the herd and leave some of the young with the
colony. If animals die, they ask to be paid, or the colony can buy the rest.

**S4. The beast coming down the valley** (the Lantern Wardens; any hunting people. Uses the region's legend beasts). Built, released 0.262.0.
1. The Wardens warn that a great beast is coming the colony's way, and ask the colony to get ready: build a wall or
traps by a date and keep the animals in. 2. Signs of it appear at the edge of the map, such as tracks and a killed
animal. 3. The beast comes onto the map. If goodwill is good, the Wardens' hunters come to help. If it is killed, the
colony keeps the trophy and the Wardens honour the colony. If it is driven off, it comes back next season (part 3
again). A real fight, shown, at the game's rate.

**S5. The pilgrims' relic** (the pilgrim fellowship; any people of a faith). 1. Pilgrims carrying a holy thing ask to
rest at the colony for some days. 2. In the night the relic is taken, by one of their own or by a passing stranger.
The thief is somewhere on the map; catch them before they get away. 3a. If the thief is caught, the pilgrims ask the
colony to judge them (a card: hand them over, punish them here, or let them go). 3b. If the thief got away, the
pilgrims blame the colony. If the thief's camp is within 3 tiles, the colony may go after them; otherwise the
pilgrims go themselves. Ending: with the relic back, they ask to leave it at the colony's shrine for a year, and
pilgrims come to visit it.

**S6. A dream of the gods** (the colony's own faith; the gods stay unseen). 1. A devout colonist dreams of a place
on the colony's ground and of something to build there for a god. The quest is the dreamer's, named as they would tell
it ("Ines's dream"), and the god is named only as the colony knows it. 2. Something happens at what was built, one of
that god's own happenings, seen. 3. The dream comes back and asks for an offering of a kind. Given, it goes into the
Gods' book as the dreamer believes it. Refused, the dreamer is troubled for a while. No favour or number is shown.

**S7. Two towns' quarrel** (any two peoples at peace with the colony and at odds with each other). 1. The colony is
the witness at their truce (built on today's witness mission). 2. One side leaves someone at the colony as a pledge
for some days; keep them safe and fed. 3. Fighters of one side cross the colony's map to strike the other. A card
gives three choices: let them pass, warn the other side, or stop them at the gate. Endings: the peace holds, the
colony is thanked by one and resented by the other, or the two go to war (their goodwill toward each other falls,
which the world page shows).

**S8. The children from the burned village** (any people at peace). Built, released 0.262.0 (needs Biotech, for the children). 1. Children whose village burned come to the gate
with one adult; host them for some days. 2. A card gives three choices: take them in (they join the colony), find
them a place with a neighbour (their new people come to fetch them), or send them on with food. 3. Seasons later, one
of them comes back grown, as a visitor, a trader or someone who asks to join.

**S9. Raiders settling nearby** (the region; uses the raiders' camp). 1. The neighbours ask the colony to build
defences by a date and keep watch. 2. The raiders test the colony with a small raid. 3. If their camp is within
3 tiles, the neighbours ask the colony to join a strike on it. Otherwise the neighbours go, and the colony sends food
and medicine for the fighters. Endings: the camp is gone, or the raiders demand payment each season (a card: pay or
fight).

**S10. The town's well ran dry** (the Deepwell Union; any town). 1. The town asks for water or food for some days.
2. They ask for a builder or driller to be lent; the colonist goes for some days and comes back with more skill.
3. Endings: with the new well, the town grows and remembers the help. If the well isn't found, its people come to the
colony as refugees, and the colony takes them in or turns them away.

**S11. The vault's sealed case** (the Sunken Vault; any spacer people). 1. Power cells for the vault (built on today's
mission). 2. One of their engineers stays and works at the colony's bench and needs components. 3. They leave a
sealed case in the colony's care. A card asks whether to open it or hand it back. Opened, it holds a sleeper who
joins or flees, a machine that has to be fought, or old goods. Handed back, the vault owes the colony a favour.

**S12. The lost founder's grave** (the region; any old people). 1. A people's elders say one of their founders is
buried on the colony's land. They ask the colony to find the grave on the map and mark it (a search area on the map).
2. On a set day, mourners come to the grave; host them for the day. 3. Endings: kept, and they come each year and
remember the colony well. Built over, or the colony refused, and they hold it against the colony.

## Single quests (one part each, at home)

- **Q1. Their prisoner kept.** A neighbour's prisoner is held at the colony for some days; they may try to escape.
  *Built in round 1 (released 0.261.0): DESIGN.md "Quests at home, round 1".*
- **Q2. A dispute judged.** Two people of a neighbour bring their quarrel to the colony; a card for the judgement,
  then each side's reaction on the map. *Built in round 1 (released 0.261.0).*
- **Q3. A young fighter trained.** Their young fighter stays and trains with the colony's best shot or fighter for
  some days; both gain skill.
- **Q4. A piece for a wedding.** Make one piece of good quality or better (a sculpture, clothes, furniture) by a date;
  they come to collect it.
- **Q5. A wounded rider.** A wounded messenger reaches the gate. Treat them, and keep their message safe until
  someone of theirs comes for it. *Built in round 1 (released 0.261.0), as "a wounded messenger": they come in on foot.*
- **Q6. A sick animal tended.** Their animal stays to be tended back to health.
- **Q7. An outlaw in the hills.** An outlaw with a price on their head hides on the colony's map. Capture them (a
  bigger reward) or kill them. A fight, shown. *Built in round 1 (released 0.261.0).*
- **Q8. Guard their camp for a night.** A caravan camps at the edge of the colony's map; something may come for it in
  the night.
- **Q9. A lost heirloom.** They lost a keepsake on the colony's land; it lies somewhere in a marked area. Find it
  before the rain spoils it or someone else picks it up. *Built in round 1 (released 0.261.0): when the colony doesn't
  find it in time, one of their own comes and does.*
- **Q10. Tame one for them.** A particular wild animal on the colony's map, tamed and handed over. *Built in round 1
  (not released yet).*
- **Q11. Seed grain kept cold.** Store their seed grain over winter, cold and untouched.
- **Q12. Their book copied.** A scholar stays while the colony's researcher copies their book at the research bench
  for some hours.
- **Q13. Musicians for a night.** Host their musicians for a night of music; it works as a party for the colony.
- **Q14. A cache in old ruins** (away, only within 3 tiles). Their old map shows a cache close by. Go and bring it back.

## Rounds

1. **The storyline frame and the first ones.** Parts as quests with branches, the "what happened before" lines, the
   pacing, the World page and chronicle lines, and a choice card that can sit inside a part. With S1 (the factor's
   goods), S4 (the beast) and S8 (the children). Alongside: Q1, Q2, Q5, Q7, Q9 and Q10 on today's mission system. (Q1, Q2,
   Q5, Q7, Q9 and Q10 released, 0.261.0; the frame with S1, S4 and S8 released, 0.262.0:
   `Source/ColonyCouncil/Storylines/`.)
2. S2, S3, S5, S7 and the remaining single quests (Q3, Q4, Q6, Q8, Q11, Q12, Q13). Being built (the player, 16 November
   2026: "great, do some more").
3. S6, S9, S10, S11, S12 and Q14 (the one away quest).

The player can strike or change anything in rounds 2 and 3 before they start.
