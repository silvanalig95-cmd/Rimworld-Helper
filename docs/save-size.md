# Save size over a long game (estimate)

Written by `Tools/save_estimate.py` (run `python3 Tools/save_estimate.py` from the repository root; `--strict` fails
on a saved collection with no model, or one that grows without end). Every collection Rimfolk saves, the rule that
keeps it in bounds, and how many entries it holds after one, two and five in-game years (60-day years) in a
colony of 20, with the game keeping 30 more people it knows and 5 more each year. Rates are
generous guesses; sizes are rough XML sizes. Caps named as constants are read from the source. "Counted under" rows are
kept inside another record (a journal's lines, a heart's lists) and are already in that record's size.

Saved collections found: 282; modelled: 245; growing without end: 0.
Rimfolk's part of the save, roughly: **1,269 KB** after a year, **1,519 KB** after two, **1,581 KB** after five.

Not modelled (add them to MODEL in the script): `Ailments/AilmentTracker.cs:carrying`, `Ailments/AilmentTracker.cs:healing`, `Calendar/CalendarWatch.cs:awaySince`, `Calendar/CalendarWatch.cs:huntKills`, `Calendar/CalendarWatch.cs:knownCrops`, `Calendar/CalendarWatch.cs:pendingCrops`, `Camps/Camp.cs:asksMade`, `Character/ColonyTraits.cs:today`, `Decrees/Decrees.cs:nightHours`, `Defense/DefenseLine.cs:done`, `Defense/DefenseLine.cs:downed`, `Defense/DefenseLine.cs:lessons`, `Defense/DefenseLine.cs:plans`, `Defense/DefenseLine.cs:raids`, `Defense/DefenseLine.cs:skipped`, `Defense/DefenseLine.cs:span`, `Defense/DefenseLines.cs:lines`, `Defense/DefenseLinesWatch.cs:downedSeen`, `Defense/DefenseLinesWatch.cs:running`, `Epithets/Epithets.cs:founders`, `Health/HealthTracker.cs:worst`, `Persona/PawnPersona.cs:d`, `Persona/PawnPersona.cs:l`, `Persona/PawnPersona.cs:marks`, `Persona/PawnPersona.cs:stances`, `Persona/PersonaTracker.cs:list`, `Persona/PersonaTracker.cs:memories`, `PlayOut/PlayOutTracker.cs:results`, `PlayOut/PlayOuts.cs:game`, `PlayOut/PlayOuts.cs:hands`, `PlayOut/PlayOuts.cs:named`, `PlayOut/PlayOuts.cs:route`, `PlayOut/PlayOuts.cs:tasks`, `Scenarios/ScenarioParts.cs:days`, `Scenarios/ScenarioParts.cs:offices`, `World/FactionMemory.cs:notes`, `World/WorldSituation.cs:followUps`

| Collection | Rule | Kind | 1 year | 2 years | 5 years | KB at 5 years |
|---|---|---|---|---|---|---|
| `Journal.cs:journals` | the living's, and the 30 departed written in most lately | cap | 30 | 50 | 50 | 468.8 |
| `Social/Gossip/Deed.cs:heard` | who knows each deed (30 at most a deed), with its deed | cap | 2,000 | 2,000 | 2,000 | 214.8 |
| `CouncilGameComponent.cs:chronicle` | Caps.Chronicle; the newest 200 always, milestones longest | cap | 300 | 300 | 300 | 102.5 |
| `Social/Gossip/ReputationTracker.cs:deeds` | 10 per colonist, 220 in all, faded ones dropped | cap | 200 | 200 | 200 | 58.6 |
| `Journal.cs:everydaySeen` | 24 days | window | 480 | 480 | 480 | 42.2 |
| `World/FactionMemory.cs:deeds` | 12 deeds per people (12 peoples), fading out | fixed | 144 | 144 | 144 | 28.1 |
| `Heart/HeartTracker.cs:hearts` | one per colonist, the gone dropped hourly | colonists | 20 | 20 | 20 | 27.3 |
| `Aspirations/Aspirations.cs:all` | goals of the dead and departed end; finished ones of the dead kept 3 years | known | 55 | 60 | 75 | 25.6 |
| `Social/SocialWeb.cs:ties` | ties of the dead dropped daily, of the gone faded out | pairs | 57 | 57 | 57 | 22.3 |
| `Ailments/AilmentTracker.cs:last` | last ailments 120 days, known pawns only | window | 120 | 240 | 240 | 21.1 |
| `Aspirations/Aspirations.cs:counters` | counters of known pawns | known | 165 | 180 | 225 | 19.8 |
| `Dreams/Dreams.cs:recent` | 80 recent dreams | cap | 80 | 80 | 80 | 19.5 |
| `Standing.cs:log` | 8 lines per colonist | colonists | 160 | 160 | 160 | 18.8 |
| `Arcs/ArcTracker.cs:past` | ArcTracker.MaxPast | cap | 18 | 36 | 90 | 17.6 |
| `Pressures/Pressures.cs:people` | one per colonist, the gone dropped daily | colonists | 20 | 20 | 20 | 17.6 |
| `Setbacks/SetbackTracker.cs:last` | 20 days | window | 200 | 200 | 200 | 17.6 |
| `UI/WeekMemory.cs:dilemmas` | DilemmaLog.MaxRecords | cap | 30 | 60 | 60 | 17.6 |
| `Strangers/Encounters.cs:lastTick` | one per encounter def | defs | 191 | 191 | 191 | 16.8 |
| `Calendar/CalendarTracker.cs:days` | 12 the colony's, 8 the player's, traditions and the founding | cap | 12 | 24 | 26 | 15.2 |
| `Social/Groups/GroupTracker.cs:groups` | groups of 3 to 9, the dead and departed dropped daily, ended below two | cap | 6 | 6 | 6 | 14.6 |
| `Dilemmas/Dilemmas.cs:lastTick` | one per dilemma def | defs | 166 | 166 | 166 | 14.6 |
| `CouncilGameComponent.cs:history` | a sample a day, 120 days | window | 60 | 120 | 120 | 14.1 |
| `Annals.cs:written` | Caps.AnnalsYears written years | cap | 1 | 2 | 5 | 12.2 |
| `CouncilGameComponent.cs:wishes` | open wishes only (one per colonist at most) | cap | 20 | 20 | 20 | 11.7 |
| `Setbacks/SetbackTracker.cs:built` | what colonists built, while it stands and is recent | window | 60 | 60 | 60 | 11.7 |
| `Social/Moments.cs:pairs` | pair tones forgotten after 4 quiet days or when one is gone | pairs | 95 | 95 | 95 | 11.1 |
| `Ailments/AilmentTracker.cs:had` | known pawns only | known | 165 | 180 | 225 | 11.0 |
| `Happenings/Happenings.cs:memos` | memos 3 years (children's 10), used ones a day | window | 18 | 36 | 54 | 10.5 |
| `Standing.cs:asked` | per colonist, one per wish def asked for | colonists | 300 | 300 | 300 | 10.3 |
| `Quirks/Quirks.cs:quirks` | 2 per colonist, the dead and discarded dropped | colonists | 40 | 40 | 40 | 7.8 |
| `Secrets/Secrets.cs:secrets` | one per colonist, ended with their death | colonists | 20 | 20 | 20 | 7.8 |
| `Setbacks/SetbackTracker.cs:sown` | fields sown, until harvested or lost | window | 40 | 40 | 40 | 7.8 |
| `Social/Groups/GroupTracker.cs:work` | one work log per colonist, 6 kinds each | colonists | 20 | 20 | 20 | 7.8 |
| `UI/News.cs:items` | per news letter; the game's archive keeps 200 letters | cap | 40 | 40 | 40 | 7.8 |
| `UI/News.cs:waiting` | the evening's news, 40 at most, sent each evening | cap | 40 | 40 | 40 | 7.8 |
| `Journal.cs:griefSeen` | cleared at 500 | cap | 30 | 60 | 150 | 7.3 |
| `World/WorldSituations.cs:history` | 40 past | cap | 6 | 12 | 30 | 7.3 |
| `Dreams/Dreams.cs:seen` | dream counts of known pawns | known | 55 | 60 | 75 | 6.6 |
| `Health/HealthTracker.cs:healers` | known pawns only | known | 22 | 24 | 30 | 5.9 |
| `Missions/StoriesComponent.cs:chains` | chains 120 days after they end | window | 6 | 12 | 12 | 5.9 |
| `Secrets/Vendettas.cs:vendettas` | 40 settled, avengers dead or discarded dropped | cap | 3 | 6 | 15 | 5.9 |
| `Standing.cs:standings` | one per colonist, the gone dropped daily | colonists | 20 | 20 | 20 | 5.9 |
| `Ambitions/Ambitions.cs:lastOffered` | one per ambition def | defs | 66 | 66 | 66 | 5.8 |
| `Epithets/Epithets.cs:holders` | one per title | defs | 63 | 63 | 63 | 5.5 |
| `CouncilGameComponent.cs:cooldowns` | dropped once over | window | 60 | 60 | 60 | 5.3 |
| `Arcs/ArcTracker.cs:active` | stories under way | colonists | 10 | 10 | 10 | 4.9 |
| `PlayOut/PlayOutTracker.cs:plays` | waiting ones capped, done ones dropped | cap | 8 | 8 | 8 | 4.7 |
| `Social/Groups/GroupTracker.cs:past` | 12 past groups, members the game still knows | cap | 3 | 6 | 12 | 4.7 |
| `Building/CouncilPlanQueue.cs:queue` | blueprints waiting to be placed | while | 30 | 30 | 30 | 4.4 |
| `Character/Character.cs:recent` | 30 recent shifts | cap | 30 | 30 | 30 | 4.4 |
| `Strangers/Nemeses.cs:all` | 30 that have ended | cap | 3 | 6 | 15 | 4.4 |
| `Dilemmas/Dilemmas.cs:traditionCount` | per tradition | defs | 50 | 50 | 50 | 4.4 |
| `World/WorldSituations.cs:lastEnded` | one per situation def | defs | 46 | 46 | 46 | 4.0 |
| `CouncilGameComponent.cs:everWished` | one per wish def | defs | 115 | 115 | 115 | 3.9 |
| `Persona/Marks.cs:fights` | the last 8 fights won per person, a year at most | colonists | 160 | 160 | 160 | 3.9 |
| `Integrations/SocialInteractions.cs:bestFriends` | Caps.BestFriendPairs, known pawns | cap | 12 | 24 | 60 | 3.5 |
| `Social/SocialWeb.cs:lost` | 40 lost bonds | cap | 3 | 6 | 15 | 2.9 |
| `Wishes/Kept.cs:watches` | granted wishes still watched (grace days) | window | 8 | 8 | 8 | 2.9 |
| `World/WorldSituations.cs:active` | 2 running | fixed | 2 | 2 | 2 | 2.9 |
| `Aspirations/Aspirations.cs:events` | sparks, 30 days | window | 30 | 30 | 30 | 2.6 |
| `Decrees/Decrees.cs:readyAt` | one per decree def | defs | 30 | 30 | 30 | 2.6 |
| `Health/HealthTracker.cs:lastDoctor` | tends 3 days | window | 30 | 30 | 30 | 2.6 |
| `Health/HealthTracker.cs:lastTend` | tends 3 days | window | 30 | 30 | 30 | 2.6 |
| `Health/HealthTracker.cs:visits` | 30 days | window | 30 | 30 | 30 | 2.6 |
| `Pressures/Pressures.cs:firstSeen` | plans while they stand | while | 30 | 30 | 30 | 2.6 |
| `Pressures/Pressures.cs:lastProgress` | frames while they stand | while | 30 | 30 | 30 | 2.6 |
| `Pressures/Pressures.cs:lastWork` | frames while they stand | while | 30 | 30 | 30 | 2.6 |
| `Secrets/Secrets.cs:rolled` | known pawns only | known | 55 | 60 | 75 | 2.6 |
| `Calendar/CalendarTracker.cs:restHours` | days taken off by hand: only the player's clicks | colonists | 20 | 20 | 20 | 2.3 |
| `Happenings/Happenings.cs:departed` | the departed 2 years | window | 3 | 6 | 6 | 2.3 |
| `Persona/Marks.cs:watches` | one per person watched; known pawns only, empty ones dropped | colonists | 20 | 20 | 20 | 2.3 |
| `Quirks/Quirks.cs:luckyPast` | LuckyItems.MaxPast | cap | 3 | 6 | 15 | 2.2 |
| `Aspirations/Aspirations.cs:inspiredBy` | known pawns only | known | 16 | 18 | 22 | 2.0 |
| `Building/PersonalTouches.cs:pending` | until built or dropped | cap | 10 | 10 | 10 | 2.0 |
| `Dreams/Dreams.cs:omens` | until they come | cap | 10 | 10 | 10 | 2.0 |
| `Happenings/Happenings.cs:raids` | until they come | cap | 3 | 5 | 5 | 2.0 |
| `Setbacks/SetbackTracker.cs:friends` | pairs of friends today | pairs | 38 | 38 | 38 | 1.9 |
| `Ailments/AilmentTracker.cs:freshDay` | known pawns only | colonists | 20 | 20 | 20 | 1.8 |
| `Ailments/AilmentTracker.cs:seenDay` | known pawns only | colonists | 20 | 20 | 20 | 1.8 |
| `Calendar/CalendarTracker.cs:harvested` | the year's harvest, per colonist; cleared yearly | colonists | 20 | 20 | 20 | 1.8 |
| `Camps/Camp.cs:felled` | trees felled during one stay | while | 60 | 60 | 60 | 1.8 |
| `CouncilGameComponent.cs:lastAtHome` | colonists still ours | colonists | 20 | 20 | 20 | 1.8 |
| `Happenings/Happenings.cs:visits` | visits a month | window | 6 | 6 | 6 | 1.8 |
| `Integrations/ModLinks.cs:lastMend` | today and yesterday only | colonists | 20 | 20 | 20 | 1.8 |
| `Integrations/ModLinks.cs:lastWater` | today and yesterday only | colonists | 20 | 20 | 20 | 1.8 |
| `Lives/Legacy.cs:heirlooms` | until the heir dies or is discarded, or the thing is lost | cap | 1 | 2 | 6 | 1.8 |
| `Quirks/Quirks.cs:battleTick` | within the longest quirk window | colonists | 20 | 20 | 20 | 1.8 |
| `Strangers/Nuisances.cs:last` | 10 days | window | 20 | 20 | 20 | 1.8 |
| `UI/WeekMemory.cs:trustValues` | 8 days of trust each | colonists | 20 | 20 | 20 | 1.7 |
| `Ambitions/CouncilMeeting.cs:invites` | while a meeting is called | while | 20 | 20 | 20 | 1.6 |
| `Advisor/AdvisorCore.cs:wealthHistory` | 60 days of wealth | window | 60 | 60 | 60 | 1.5 |
| `Camps/Camp.cs:felledTicks` | with the above | while | 60 | 60 | 60 | 1.5 |
| `Happenings/Happenings.cs:seenQuests` | quests the game still keeps | fixed | 60 | 60 | 60 | 1.5 |
| `Health/HealthTracker.cs:cases` | cases while the patient lives | cap | 2 | 4 | 5 | 1.5 |
| `Dilemmas/Dilemmas.cs:traditionBreaks` | per tradition | defs | 17 | 17 | 17 | 1.5 |
| `Ambitions/CouncilMeeting.cs:remarks` | while a meeting is called | while | 10 | 10 | 10 | 1.2 |
| `Happenings/Happenings.cs:grudges` | grudges 2 years | window | 3 | 6 | 6 | 1.2 |
| `Social/Gossip/ReputationTracker.cs:told` | one packed number per colonist, the gone dropped daily | colonists | 20 | 20 | 20 | 1.2 |
| `Strangers/Pets.cs:known` | animals known: the dead get their farewell, the discarded are dropped | fixed | 30 | 30 | 30 | 1.2 |
| `Offices/Offices.cs:holders` | one per office | defs | 12 | 12 | 12 | 1.1 |
| `Setbacks/SetbackTracker.cs:bestAt` | one per skill | fixed | 12 | 12 | 12 | 1.1 |
| `Arcs/ArcTracker.cs:eulogies` | until given | cap | 3 | 5 | 5 | 1.0 |
| `Camps/Camp.cs:owned` | the camp's goods, pruned of the gone at saving | while | 40 | 40 | 40 | 1.0 |
| `CouncilGameComponent.cs:recentWishDefs` | recent wishes, 20 days | window | 40 | 40 | 40 | 1.0 |
| `CouncilGameComponent.cs:recentWishTicks` | with the above | window | 40 | 40 | 40 | 1.0 |
| `Secrets/Vendettas.cs:eulogies` | until given | cap | 3 | 5 | 5 | 1.0 |
| `Ailments/AilmentTracker.cs:doctors` | only hediffs colonists still have | colonists | 10 | 10 | 10 | 0.9 |
| `Ailments/AilmentTracker.cs:sweets` | faded below 0.05 dropped | colonists | 10 | 10 | 10 | 0.9 |
| `Persona/Marks.cs:once` | skills mastered; known pawns only, 800 at most | known | 16 | 18 | 22 | 0.9 |
| `Quirks/Quirks.cs:downedTick` | within the longest quirk window | colonists | 10 | 10 | 10 | 0.9 |
| `Pressures/Pressures.cs:apartSince` | pairs apart now | pairs | 10 | 10 | 10 | 0.8 |
| `Character/ColonyTraits.cs:last` | one per colony trait def | defs | 9 | 9 | 9 | 0.8 |
| `Character/ColonyTraits.cs:rates` | one per colony trait def | defs | 9 | 9 | 9 | 0.8 |
| `Ambitions/Ambitions.cs:voterPawns` | while the letter waits | while | 20 | 20 | 20 | 0.8 |
| `Ambitions/CouncilMeeting.cs:attendees` | while a meeting is called | while | 20 | 20 | 20 | 0.8 |
| `Ambitions/CouncilMeeting.cs:voters` | while a meeting is called | while | 20 | 20 | 20 | 0.8 |
| `Annals.cs:present` | the year in progress | colonists | 20 | 20 | 20 | 0.8 |
| `Camps/Camp.cs:kills` | while the camp stays (a stay is days, not years) | while | 20 | 20 | 20 | 0.8 |
| `Camps/Camp.cs:plans` | while the camp stays | while | 4 | 4 | 4 | 0.8 |
| `Health/HealthTracker.cs:known` | known pawns only | colonists | 20 | 20 | 20 | 0.8 |
| `PlayOut/PlayOutLord.cs:fed` | while the gathering lasts | while | 20 | 20 | 20 | 0.8 |
| `PlayOut/PlayOutLord.cs:served` | while the gathering lasts | while | 20 | 20 | 20 | 0.8 |
| `Social/Autonomy.cs:pawns` | who acted when: the last 2 days | window | 20 | 20 | 20 | 0.8 |
| `Social/SocialWeb.cs:fighters` | while a fight lasts | while | 20 | 20 | 20 | 0.8 |
| `UI/WeekMemory.cs:trustKeys` | a trail per colonist, forgotten after 10 days away | colonists | 20 | 20 | 20 | 0.8 |
| `World/WorldSituation.cs:beats` | per situation | fixed | 4 | 4 | 4 | 0.8 |
| `Social/Groups/GroupTracker.cs:newcomers` | newcomers, 40 days at most | cap | 5 | 5 | 5 | 0.7 |
| `Annals.cs:tiesFormed` | the year in progress | yearly | 12 | 12 | 12 | 0.7 |
| `Camps/Camp.cs:warmth` | while the camp stays | while | 8 | 8 | 8 | 0.7 |
| `PlayOut/PlayOutLord.cs:lines` | while the gathering lasts | while | 6 | 6 | 6 | 0.7 |
| `Dilemmas/Dilemmas.cs:traditions` | per tradition | defs | 17 | 17 | 17 | 0.6 |
| `Ambitions/CouncilMeeting.cs:pitches` | while a meeting is called | while | 3 | 3 | 3 | 0.6 |
| `Secrets/Vendettas.cs:known` | known pawns only | known | 11 | 12 | 15 | 0.6 |
| `Ailments/AilmentTracker.cs:announced` | only hediffs colonists still have | colonists | 20 | 20 | 20 | 0.5 |
| `Ailments/AilmentTracker.cs:woundsRolled` | only hediffs colonists still have | colonists | 20 | 20 | 20 | 0.5 |
| `Ambitions/Ambitions.cs:voterChoice` | while the letter waits | while | 20 | 20 | 20 | 0.5 |
| `Social/Autonomy.cs:lastActed` | with the above | window | 20 | 20 | 20 | 0.5 |
| `Annals.cs:decrees` | the year in progress | yearly | 12 | 12 | 12 | 0.5 |
| `Offices/Offices.cs:elected` | one per office | defs | 12 | 12 | 12 | 0.5 |
| `PlayOut/PlayOuts.cs:lines` | per play-out | fixed | 4 | 4 | 4 | 0.5 |
| `PlayOut/PlayOuts.cs:moods` | per play-out | fixed | 4 | 4 | 4 | 0.5 |
| `Decrees/Decrees.cs:active` | decrees in force (3 at most) | fixed | 3 | 3 | 3 | 0.4 |
| `Decrees/Decrees.cs:requests` | one per decree at most | fixed | 3 | 3 | 3 | 0.4 |
| `Calendar/CalendarTracker.cs:forgotten` | days put away by hand: only the player's clicks | fixed | 10 | 10 | 10 | 0.4 |
| `Camps/Camp.cs:trees` | while the camp stays | while | 10 | 10 | 10 | 0.4 |
| `Setbacks/SetbackTracker.cs:grantWho` | 20 days | window | 10 | 10 | 10 | 0.4 |
| `Social/Autonomy.cs:plans` | plans until built | cap | 10 | 10 | 10 | 0.4 |
| `Standing.cs:recentGrants` | per colonist, 30 days | window | 15 | 15 | 15 | 0.4 |
| `Annals.cs:goals` | the year in progress | yearly | 6 | 6 | 6 | 0.4 |
| `Annals.cs:omens` | the year in progress | yearly | 6 | 6 | 6 | 0.4 |
| `Health/HealthTracker.cs:bedHours` | known pawns only, while laid up | colonists | 4 | 4 | 4 | 0.4 |
| `Health/HealthTracker.cs:lastTrustLine` | 20 days | window | 4 | 4 | 4 | 0.4 |
| `World/WorldSituation.cs:numbers` | per situation | fixed | 4 | 4 | 4 | 0.4 |
| `Camps/Camp.cs:members` | the camp's people, while it stays | while | 8 | 8 | 8 | 0.3 |
| `Camps/Camp.cs:willing` | while the camp stays | while | 8 | 8 | 8 | 0.3 |
| `Happenings/Happenings.cs:group` | per raid or visit (counted with them) | fixed | 1 | 1 | 1 | 0.3 |
| `Lives/Legacy.cs:children` | known pawns only | known | 6 | 6 | 8 | 0.3 |
| `Pressures/Pressures.cs:mealTicks` | per colonist | fixed | 12 | 12 | 12 | 0.3 |
| `Health/HealthTracker.cs:partsSeen` | missing parts colonists still have | colonists | 10 | 10 | 10 | 0.2 |
| `Setbacks/SetbackTracker.cs:grantTicks` | 20 days | window | 10 | 10 | 10 | 0.2 |
| `Setbacks/SetbackTracker.cs:grantVoiced` | 20 days | window | 10 | 10 | 10 | 0.2 |
| `Annals.cs:joined` | the year in progress | yearly | 6 | 6 | 6 | 0.2 |
| `Annals.cs:traditionsKept` | the year in progress | yearly | 6 | 6 | 6 | 0.2 |
| `Camps/Camp.cs:prey` | while the camp stays | while | 6 | 6 | 6 | 0.2 |
| `Persona/Marks.cs:addictions` | the addictions they have now | colonists | 6 | 6 | 6 | 0.2 |
| `Pressures/Pressures.cs:workKinds` | per colonist | fixed | 6 | 6 | 6 | 0.2 |
| `Heart/HeartData.cs:recentRelief` | per heart | fixed | 8 | 8 | 8 | 0.2 |
| `Heart/HeartData.cs:today` | per heart | fixed | 8 | 8 | 8 | 0.2 |
| `Missions/StoriesComponent.cs:missions` | missions 2 days after they end | window | 0 | 0 | 0 | 0.2 |
| `Social/SocialWeb.cs:battleFallen` | while a fight lasts | while | 5 | 5 | 5 | 0.2 |
| `Strangers/Pets.cs:fought` | animals in the last fight | fixed | 5 | 5 | 5 | 0.2 |
| `Annals.cs:secretsOut` | the year in progress | yearly | 3 | 3 | 3 | 0.2 |
| `Character/ColonyTraits.cs:known` | traits the colony grew into | defs | 4 | 4 | 4 | 0.2 |
| `Decrees/Decrees.cs:useDefs` | uses within the fatigue window | window | 4 | 4 | 4 | 0.2 |
| `Health/HealthTracker.cs:lastSetback` | 20 days | window | 2 | 2 | 2 | 0.2 |
| `Health/HealthTracker.cs:setbacks` | convalescences colonists still have | colonists | 2 | 2 | 2 | 0.2 |
| `Annals.cs:died` | the year in progress | yearly | 4 | 4 | 4 | 0.2 |
| `Calendar/CalendarDay.cs:knew` | per day on the calendar | fixed | 4 | 4 | 4 | 0.2 |
| `Health/HealthTracker.cs:convalescing` | known pawns only | colonists | 4 | 4 | 4 | 0.2 |
| `Heart/HeartData.cs:feelingSinceKeys` | per heart: feelings felt now | fixed | 4 | 4 | 4 | 0.2 |
| `Missions/Missions.cs:group` | per mission | fixed | 4 | 4 | 4 | 0.2 |
| `PlayOut/PlayOutLord.cs:abstained` | while the gathering lasts | while | 4 | 4 | 4 | 0.2 |
| `UI/WeekMemory.cs:pinnedPawns` | pins: only the player's clicks, the gone dropped | fixed | 4 | 4 | 4 | 0.2 |
| `World/WorldSituation.cs:deeds` | per situation | fixed | 4 | 4 | 4 | 0.2 |
| `World/WorldSituation.cs:held` | per situation | fixed | 4 | 4 | 4 | 0.2 |
| `World/WorldSituation.cs:sufferers` | per situation | fixed | 4 | 4 | 4 | 0.2 |
| `Pressures/Pressures.cs:workCounts` | per colonist | fixed | 6 | 6 | 6 | 0.1 |
| `Character/Character.cs:tiers` | five ways | fixed | 5 | 5 | 5 | 0.1 |
| `Character/Character.cs:values` | five ways | fixed | 5 | 5 | 5 | 0.1 |
| `Ambitions/Ambitions.cs:options` | the meeting letter's options, while it waits | while | 3 | 3 | 3 | 0.1 |
| `Ambitions/Ambitions.cs:proposers` | while the letter waits | while | 3 | 3 | 3 | 0.1 |
| `Annals.cs:gone` | the year in progress | yearly | 3 | 3 | 3 | 0.1 |
| `Annals.cs:quit` | the year in progress | yearly | 3 | 3 | 3 | 0.1 |
| `Annals.cs:traditionsBroken` | the year in progress | yearly | 3 | 3 | 3 | 0.1 |
| `Camps/Camp.cs:joined` | while the camp stays | while | 3 | 3 | 3 | 0.1 |
| `Happenings/Happenings.cs:things` | per departed record | fixed | 3 | 3 | 3 | 0.1 |
| `Happenings/Happenings.cs:touched` | per visit | fixed | 3 | 3 | 3 | 0.1 |
| `PlayOut/PlayOuts.cs:spots` | per play-out | fixed | 2 | 2 | 2 | 0.1 |
| `Scenarios/Scenarios.cs:events` | a scenario part's events | fixed | 3 | 3 | 3 | 0.1 |
| `Wishes/Kept.cs:cells` | per watch | fixed | 4 | 4 | 4 | 0.1 |
| `Decrees/Decrees.cs:useTicks` | with the above | window | 4 | 4 | 4 | 0.1 |
| `Ailments/AilmentTracker.cs:bitesRolled` | only hediffs colonists still have | colonists | 4 | 4 | 4 | 0.1 |
| `Ambitions/Ambitions.cs:baselines` | per ambition's goals | fixed | 4 | 4 | 4 | 0.1 |
| `Arcs/Arc.cs:heldOr` | per story | fixed | 4 | 4 | 4 | 0.1 |
| `Heart/HeartData.cs:feelingSinceTicks` | per heart | fixed | 4 | 4 | 4 | 0.1 |
| `Heart/HeartData.cs:needLack` | per heart: the four needs | fixed | 4 | 4 | 4 | 0.1 |
| `Setbacks/SetbackTracker.cs:lastGathering` | one per home map | fixed | 1 | 1 | 1 | 0.1 |
| `Ambitions/Ambitions.cs:absentProposers` | while the letter waits | while | 2 | 2 | 2 | 0.1 |
| `Calendar/CalendarDay.cs:kin` | per day on the calendar | fixed | 2 | 2 | 2 | 0.1 |
| `Camps/Camp.cs:arrested` | while the camp stays | while | 2 | 2 | 2 | 0.1 |
| `Setbacks/SetbackTracker.cs:ticks` | per sown field | fixed | 3 | 3 | 3 | 0.1 |
| `Annals.cs:traditionsBorn` | the year in progress | yearly | 2 | 2 | 2 | 0.1 |
| `UI/DilemmaLog.cs:started` | per dilemma record | fixed | 2 | 2 | 2 | 0.0 |
| `UI/WeekMemory.cs:pinnedWishes` | pins of open wishes | fixed | 2 | 2 | 2 | 0.0 |
| `Lives/Legacy.cs:cameOfAgeTick` | 6 days | window | 0 | 0 | 0 | 0.0 |
| `Lives/Legacy.cs:cameOfAge` | 6 days | window | 0 | 0 | 0 | 0.0 |
| `Arcs/Arc.cs:told` | per story (counted under active and past) | fixed | 6 | 6 | 6 | (in its record) |
| `Building/CustomRooms.cs:pieces` | per saved room, in the mod's settings file | settings | 30 | 30 | 30 | (settings file) |
| `Calendar/CalendarDay.cs:fallen` | per day on the calendar (counted under days) | fixed | 2 | 2 | 2 | (in its record) |
| `CouncilSettings.cs:councilPageState` | the mod's settings file, not the save | settings | 30 | 30 | 30 | (settings file) |
| `CouncilSettings.cs:planMaterials` | the mod's settings file, not the save | settings | 30 | 30 | 30 | (settings file) |
| `CouncilSettings.cs:savedRooms` | the mod's settings file, not the save: rooms the player saved | settings | 10 | 10 | 10 | (settings file) |
| `Happenings/Workers_Chains2.cs:flags` | per story chain (counted under chains) | fixed | 4 | 4 | 4 | (in its record) |
| `Health/HealthTracker.cs:losses` | per healer: 30 days (counted under healers) | fixed | 3 | 3 | 3 | (in its record) |
| `Health/HealthTracker.cs:tends` | per case: who tended it (counted under cases) | fixed | 4 | 4 | 4 | (in its record) |
| `Heart/HeartData.cs:recentBuild` | per heart: one per strain source (counted under hearts) | fixed | 8 | 8 | 8 | (in its record) |
| `Journal.cs:entries` | per journal: 60 lines (counted under journals) | fixed | 60 | 60 | 60 | (in its record) |
| `Missions/Missions.cs:goals` | per mission (counted under missions) | fixed | 3 | 3 | 3 | (in its record) |
| `PlayOut/PlayOuts.cs:invited` | per play-out (counted under plays) | fixed | 8 | 8 | 8 | (in its record) |
| `Pressures/Pressures.cs:meals` | per colonist: 12 meals (counted under people) | fixed | 12 | 12 | 12 | (in its record) |
| `Secrets/Secrets.cs:knowers` | per secret (counted under secrets) | fixed | 3 | 3 | 3 | (in its record) |
| `Setbacks/SetbackTracker.cs:sowers` | per sown field (counted under sown) | fixed | 3 | 3 | 3 | (in its record) |
| `Social/Groups/SocialGroup.cs:counts` | per work log (counted under work) | fixed | 6 | 6 | 6 | (in its record) |
| `Social/Groups/SocialGroup.cs:formerNames` | per group: 10 names (counted under groups) | fixed | 10 | 10 | 10 | (in its record) |
| `Social/Groups/SocialGroup.cs:history` | per group: 14 lines (counted under groups) | fixed | 14 | 14 | 14 | (in its record) |
| `Social/Groups/SocialGroup.cs:kinds` | per work log: 6 kinds (counted under work) | fixed | 6 | 6 | 6 | (in its record) |
| `Social/Groups/SocialGroup.cs:members` | per group, and per past group by ID (counted under groups) | fixed | 6 | 6 | 6 | (in its record) |
| `Social/Groups/SocialGroup.cs:norms` | per group: 3 ways (counted under groups) | fixed | 3 | 3 | 3 | (in its record) |
| `Social/Groups/SocialGroup.cs:rivals` | per group: rivalries with groups still there (counted under groups) | fixed | 3 | 3 | 3 | (in its record) |
| `Social/Groups/SocialGroup.cs:views` | per group: 3 views (counted under groups) | fixed | 3 | 3 | 3 | (in its record) |
| `Social/SocialWeb.cs:history` | per tie (counted under ties) | fixed | 10 | 10 | 10 | (in its record) |
| `UI/DilemmaLog.cs:plays` | per dilemma record (counted under dilemmas) | fixed | 4 | 4 | 4 | (in its record) |
| `Wishes/Kept.cs:things` | per watch (counted under watches) | fixed | 2 | 2 | 2 | (in its record) |
| `Wishes/Wish.cs:coSigners` | per open wish (counted under wishes) | fixed | 2 | 2 | 2 | (in its record) |
| `World/WorldSituation.cs:flags` | per situation (counted under active and history) | fixed | 4 | 4 | 4 | (in its record) |

