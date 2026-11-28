# Review of the camps and the world

The player asked, on 28 November 2026: "regularly reviewing what you have already added to the games, all the different
events, visitor world maps, etc. Just testing whether everything works properly as intended, whether it actually has also
impacts on the game and it's not just flavor, whether it take the existing content like events and dilemmas and whatnot can
be just further enhanced. And then after, or also on the side, just add even more variety." After the events and the
council, the player chose the camps and the world. The standing rule from 27 November: more content for the existing
mechanics, no new mechanics.

This review covers every camp kind (102 `CampKindDef`s and their 263 asks and requests), every world event (107
`WorldSituationDef`s and their 535 answers), the 117 missions and the 293 encounters. It was done in three steps: measure
what each answer does (`Tools/world_impact_audit.py`), read the code behind what the measure flags and what looked
suspicious, then fix and give a real effect where the words already promised one, in the existing frames only.

The fixes and changes are kept by `Tools/Tests/ReviewCampsWorldTests.cs`.

## How the measure works

`python3 Tools/world_impact_audit.py` reads the defs of the four areas and sorts every effect an answer carries into a kind.
Real game effects: goods, silver, goodwill, skill, health, work done on the map, building, animal, people (joining,
leaving, guests, prisoners), trade, region (a people's towns and fortunes, another world event), threat, research, camp,
event; spend is a cost and never counts as a gain. Flavour: mood, words (a journal, chronicle or life-goal line), scene (a
play-out that only shows the words), soft (a camp's tension or warmth, what a people remembers, trust, faith). An effect
with a mode (the peoples' camps' asks) is read from what its own impact sheet names for that mode; a world event answer is
followed through the flags it sets, the beat it brings on and the ending it calls, so a gain that comes later counts; a
mission's gain is read from its worker's code. Rows read by hand and left are in `Tools/world_impact_skips.txt`, each with
why; a stale row is named.

    python3 Tools/world_impact_audit.py            # the lists, as printed below
    python3 Tools/world_impact_audit.py --counts   # the counts only
    python3 Tools/world_impact_audit.py --write    # the generated part of this file, between the markers below
    python3 Tools/world_impact_audit.py --root DIR # the counts of another copy (an older commit's Defs and Source)

## The counts, before and after

| Area | Answers before | Real gain before | Flavour only before | Answers after | Real gain after | Flavour only after |
|---|---|---|---|---|---|---|
| Camps (asks and requests) | 262 | 182 | 80 | 263 | 202 | 61 |
| World events (answers) | 533 | 339 | 194 (169 refusals) | 535 | 342 | 193 (169 refusals) |
| Missions (endings) | 117 | 117 | 0 | 117 | 117 | 0 |
| Encounters | 293 | 99 | 194 | 293 | 100 | 193 |

Words that promise more than the effects do: camps 1, world 6, encounters 23 before; camps 1, world 6, encounters 22 after
(every one of them read and left, in the skip list: told moments, a gift the worker hands over, "the meadow is the
colony's"). Every flavour-only answer left is in the skip list with why: faith (the gods are unseen, so a prayer is never
shown to work), recreation and company that are the point of the ask, a refusal or the middle answer, or a real effect
that comes from the camp's or the event's own worker (the peoples' camps' parting goodwill, the healer's teaching, the
rat-catcher's dog). The 169 world event refusals are saying no, meant to cost and give little.

What the measure showed: the world events and the missions are in good shape (nearly every flavour-only answer is a
refusal or has its effect in its worker), and so are the peoples' camps (their workers give the parting goodwill, the
lessons and the gifts). The gaps were in the older and the data-only camps, whose charity asks and requests cost the
colony its meals, medicine or steel and gave only a lower tension and a line in their people's memory, though their words
say their people remember or thank the colony.

## Fixed

Most serious first.

1. **Goods the colony bought from a camp could be lost after it paid** (`Camps/CampCarry.cs`). An ask that buys a camp's
   goods (the hides, the road stores, the smugglers' goods) is paid at once, and one of the camp carries the goods to the
   colony's gate. When that carry was let go (the bearer downed or gone, no way to the gate, twelve hours past, or the camp
   leaving), it was dropped: the colony had paid and nothing came. Now what the colony paid for is set out at the camp's
   edge instead, as the ask says happens with nobody to bring it (`CampAskMath.SetOutWhenLetGo`: all of it when not yet
   taken up, else what the bearer still has), and the colony hears where (`CC_CampBring_SetOut`).
2. **A camp with no trader paid nothing where its answer said it paid** (`Camps/CampRequests.cs`, `Camp.GiveGoods`).
   Silver a camp gives came only out of its trader's purse, so most camps paid nothing: the herd in the fields' "Make them
   pay for the damage" showed +40 silver on the card and said "They paid, and set the silver out at the edge of their camp",
   and nothing was set out for the drovers', the herders' and every other herd camp without a trader. Now the purse first,
   the rest from their own, as their fee is paid (`CampAskMath.FromPurse`).
3. **The colony's load on the road was lost when the road wardens gave up their post** (`Camps/CampKindsLand.cs`,
   `LandLeaving`). A load carried down to the wardens' post goes on with the carters, and its price comes up when they come
   back through. When the wardens left first (a death among them, grief, a move), neither the load nor the price came back.
   Now the price is sent up the road as the post is given up (`PayRoad`), unless they left at war with the colony.
4. **What a camp set out was counted after it was set down** (`Camp.Place`). A stack that merged into one already lying
   there read nothing, so the share of a test hole the prospectors dug, or the purse a camp left, went untold. Now counted
   first.

Read and found already right, though the measure flagged it: the kind new head's gift of their own, which the [kind]
words of all three gate answers promise, is handed over at the gate by the event's worker (`World/Region/NewHeads.cs`); a
gift written into the answers would have given it twice.

## Answers given a real effect

Each in the existing frames (`DilemmaEffect_Goodwill`, `WorldEffect_Goodwill`, `DilemmaEffect_Felled`,
`DilemmaEffect_PutBy`, `DilemmaEffect_Learn`), modest beside what the answer costs, where the words already said so.

5. to 17. **Charity a camp's people remember** (data-only camps, no worker of their own and no parting goodwill): a little
   goodwill with their people, as their words say they will tell their people or remember it. The mourners' food for the
   road +2; the novices' food for the road +2; a hot meal for the stranded carters +2; a sack of food for the gleaners +3;
   food for the road home for the outlaws walking home +3; a coat for the winter pilgrims' eldest +3 ("Their people
   remember it"); a meal for the god's pilgrims +2 ("Their people will remember the welcome"); a hot supper for the
   woodcutters +2, the shearers +2 ("the crew will remember the colony at the next farm") and the teacher's camp +2;
   food for the beast-catchers' beasts +2; a hot supper for the wounded fighters +2 and medicine for their wounded +3.
18. to 20. **A night under the colony's roof in a storm** (the watchers, the snowbound caravan, the winter pilgrims): +2,
   the night's shelter remembered.
21. **The pilgrims' hostel's bread each morning** (`Rimfolk_Camp_PilgrimHostel`): +4, a cost every morning while it stands
   and no parting goodwill in its worker.
22. **Meat for the Wardens' hounds** (`Rimfolk_Camp_LanternPost`): +2.
23. **The deserters work for the colony's steel, as they offered** (`Council_Camp_Deserters`, request "gear": "They'd work
   for it, if the colony likes"): a few of them fell 30 logs for the colony near their camp; the tip and the outcome say so.
24. **The run's catch salted down for the winter** (`Rimfolk_WS_Run`): a bill to put it by as pemmican at the colony's
   stove, the game's own cooking (the existing put-by of the fish running), where it was the status quo before.
25. **The expedition's survivor fed and sent home** (`Rimfolk_WS_Expedition`, "A meal, and send them on"): +2 with their
   people, the middle answer between taking them in and sending them off hungry.
26. **Working beside an agrihand** (encounter `Council_EncM_RowTalk`: an hour in the next row, "a trick or two about
   spacing"): Plants 150.
27. **War stories with a stranger** (encounter `Council_EncP_WarStories`: how their fighters brace, where to hit, battle lines
   drawn in the dirt): Melee 120.

## Added where a camp or event was thin

28. **The young hands on the road** (`Rimfolk_Camp_YoungHands`) had no ask, though every one of their texts says they would
   work a day for their supper. Now "Give them supper for a day's work": four of the colony's meals carried to their fire,
   and a few of them fell 40 logs for the colony near their camp.
29. **The strange herds** (`Council_WS_StrangeHerds`, Alpha Animals) had no question at all. Now the hunters of a
   neighbour's town ask for the colony's best with animals (Animals 5 or more) to look the beasts on their grazing over:
   sent there and back as a caravan of one (`WorldEffect_Away`, two days at the town), home with Animals 1500 and +6 with
   the town's people; or kept home (the default).

## Looked at and left

- **The faith asks** (the pilgrims' prayers at the graves and the relic, the procession's icon and hymn, the penitents', the
  tree-singers', the god's pilgrims', the novices', the seer's): the gods are unseen, so a prayer is never shown to work;
  the walk, the gathering and the words are the point. The procession is the one camp kind that brings nothing of its own
  over a stay; a warm stay may still leave a gift at the gate, as every camp's may.
- **Recreation and company** (the evenings at a camp's fire, the players' and the minstrels' shows bought with silver, the
  vigils, the snow figures, the instrument-makers' music): recreation for those who come, by design.
- **The peoples' camps' charity requests** (the sickhouse's medicine and cloth, the dry herders' hay and grazing, the calving
  camp's hay, the school's food and books, the flooded families', the field of the fallen's, the circuit ring's, the yard's,
  the burnt-out families', the line crew's, the picket's): their workers give the parting goodwill after a good stay, which
  these keep; a second goodwill on the ask would count it twice.
- **The world events' middle answers that feed someone and send them on** (a deserter, a fugitive, exiles, a priest of a
  god put away, one fleeing a rising): their own people would not thank the colony for it; the choice is the colony's
  conscience, and the costlier "take them in" carries the real effects.
- **The encounters' told moments** (a song, a dice game, a story, a way with tea, advice for the desert, the river ford):
  words, by design; only where every line of the encounter teaches the same thing is a skill given (the two above).
- **The hermit's meal and medicine**: the hermit's thanks is the carving left at a warm parting, which they bring nearer.
- **The missions**: every one ends with silver, goodwill, goods or people in its worker's code; their words were not read
  line by line in this review.
- Checked and sound: the camps' packs and store of 0.307.0 (`CampDayMath.Repacks`, `TakeFirst`, `Camp.TakeGoods`: each stack
  to the carrier with the most room, split where one can carry only part, what nobody can carry left free to take); the
  bought beast handed over before the price is taken back (`HandOverBeast`); the beds lent to a camp in a storm given back
  at its parting (`LandLeaving`, `Finish`); the world events' default answers (always possible, the cheap or the middle
  answer); the far errands (`WorldEffect_Away`: who may go, the road, the homecoming's effects); the joiners
  (`WorldEffect_Join`: a family's names and ties, their animal); no harm added, no god named, no countdown.

<!-- world_impact_audit: start -->

### Counts

| Area | Answers | With a real gain | Flavour only | Of them refusals | Words that promise more |
|---|---|---|---|---|---|
| camps | 263 | 202 | 61 | 0 | 1 |
| world | 535 | 342 | 193 | 169 | 6 |
| missions | 117 | 117 | 0 | 0 | 0 |
| encounters | 293 | 100 | 193 | 0 | 22 |

Camp kinds: 102; bringing nothing of their own over a stay: 1; whose asks are all flavour: 5. World events: 107; whose answers are all flavour: 5.

Effect kinds over all answers: goods 131, silver 169, goodwill 426, skill 169, health 27, work 34, building 1, animal 7, people 69, trade 17, region 1, threat 128, research 8, camp 9, event 8, spend 322, mood 342, words 159, scene 186, soft 294.

### Camps: flavour-only answers (0; 61 more read and left (the skip list))

None.

### Camps: flavour-only refusals (saying no is meant to cost and give little) (0)

None.

### Camps: words that promise more than the effects do (0; 1 more read and left (the skip list))

None.

### World: flavour-only answers (0; 24 more read and left (the skip list))

None.

### World: flavour-only refusals (saying no is meant to cost and give little) (169)

`Council_WS_Plague:refugees/2` Turn them away; `Council_WS_Plague:orphans/2` The colony can't take them; `Council_WS_Plague:doctor/2` Not now; `Council_WS_Famine:food/3` Nothing to spare; `Council_WS_Famine:hungry/2` Turn them away; `Council_WS_Famine:orphan/2` The colony can't keep her; `Council_WS_HardWinter:wood/2` Keep it; `Council_WS_HardWinter:snowbound/1` Point them to {PLACE}; `Council_WS_Locusts:prepare/3` Leave it to luck; `Council_WS_Flood:refugees/2` Turn them away; `Council_WS_Flood:timber/2` Keep it; `Council_WS_Healer:offer/1` Not now; `Council_WS_Migration:hunters/2` Keep them off the land; `Council_WS_StrangeHerds:hunters/1` The colony can't spare anyone; `Council_WS_WildMagic:study/1` Not this time; `Council_WS_ImperialDeserters:fugitive/2` Feed the deserter and send them on; `Council_WS_RepublicVotes:canvass/2` Stay out of it; `Council_WS_GoldFever:claim/1` Send him on; `Council_WS_NewTown:welcome/2` Wish them well; `Council_WS_Abandoned:refugees/2` Point them to {ITEM}; `Council_WS_Abandoned:salvage/1` Leave it; `Council_WS_TradeBoom:toll/2` Leave it; `Council_WS_Fair:cart/1` Not this year; `Council_WS_Fair:contest/1` Not this time; `Council_WS_Pilgrimage:camp/2` Ask them to move on; `Council_WS_SilverRush:claim/2` Keep them off the land; `Council_WS_SilverRush:stay/1` Not now; `Council_WS_Claimants:sides/2` Stay out of it; `Council_WS_HolyWar:preachers/3` Keep out of it; `Council_WS_PeaceTalks:ask/1` Decline; `Council_WS_Mercenaries:offer/2` Send them on; `Council_WS_BanditRoad:guards/1` Wish them luck; `Council_WS_BanditRoad:robbed/1` Send them on; `Council_WS_Drought:well/2` It's their well; `Council_WS_Drought:food/2` Nothing to spare; `Council_WS_NewRoad:timber/2` We need our wood; `Council_WS_NewRoad:builder/1` We can't spare {A_objective}; `Council_WS_HolyYear:alms/1` Send them on; `Council_WS_HolyYear:vow/1` Not now, there's work; `Council_WS_BanditLordFalls:hoard/2` Send the bandit away; `Council_WS_Murrain:herdsman/1` Keep {A_objective} home; `Council_WS_Murrain:food/2` Nothing to spare; `Council_WS_ShipDown:salvage/1` Not today; `Council_WS_ShipDown:survivor/2` Send them on; `Council_WS_ShipDown:systems/1` Not our business; `Council_WS_GreatFire:timber/2` The colony needs its wood; `Council_WS_GreatFire:burned/2` Turn them away; `Council_WS_GreatFire:roofs/2` {A_nameDef} is needed here; `Council_WS_Feud:fugitive/1` Send him on, with food; `Council_WS_Feud:mediate/1` It isn't the colony's quarrel; `Council_WS_Feud:price/1` They'll have to find it themselves; `Council_WS_Comet:watch/1` Everyone to bed: there's work tomorrow; `Council_WS_Comet:scholar/1` She can watch from the road; `Council_WS_Comet:fragment/1` Leave it in the hills; `Council_WS_Bumper:grain/1` The colony has enough; `Council_WS_Bumper:hands/1` The colony has its own fields; `Council_WS_Bumper:supper/1` Not this year; `Council_WS_Drovers:graze/2` Keep them off the meadow; `Council_WS_Drovers:dispute/1` Send them on to {PLACE}; `Council_WS_Mourning:gift/2` Send nothing; `Council_WS_Mourning:road/1` Let them pass; `Council_WS_Songs:visit/1` The colony has work to do; `Council_WS_Toll:petition/1` Stay out of it; `Council_WS_Toll:backpath/1` Keep the lane closed; `Council_WS_IronScarce:buy/2` Keep the steel; `Council_WS_IronScarce:mend/1` {A_nameDef} is needed here; `Council_WS_Exiles:ask/1` Feed them and send them on; `Council_WS_Exiles:plea/1` Let it lie; `Council_WS_Exiles:mother/1` The colony can't spare it; `Council_WS_Rebuilding:timber/2` The colony can't spare it; `Council_WS_Rebuilding:builder/1` {A_nameDef} is needed here; `Council_WS_Rebuilding:homecoming/1` Let them pass; `Council_WS_RouteReopens:contract/1` The colony isn't a kitchen; `Council_WS_RouteReopens:rest/1` Keep the road at a distance; `Rimfolk_WS_GrownTown:name/2` Let it keep its own name; `Rimfolk_WS_GrownTown:market/1` Not this time; `Rimfolk_WS_Legend:bounty/1` Leave it to others; `Council_WS_LordsProgress:herald/2` Keep to ourselves; `Council_WS_ClansGather:envoy/2` Refuse, politely; `Council_WS_MarketDays:pitch/2` No room for them; `Council_WS_MarketDays:quarrel/3` Leave them to it; `Council_WS_OrbitalLanes:agent/2` Not for sale; `Council_WS_Assize:court/2` Keep out of it; `Rimfolk_WS_NewTownMet:welcome/2` Wish them well; `Rimfolk_WS_GivenUp:refugees/2` Point them to {ITEM}; `Rimfolk_WS_GivenUp:salvage/1` Leave it; `Rimfolk_WS_GodPutAway:priest/1` A meal, and send {X_objective} on; `Rimfolk_WS_GodPutAway:priest/2` Turn {X_objective} away; `Rimfolk_WS_DancingSickness:dancers/2` Send them on; `Rimfolk_WS_RestlessDead:question/0` Leave them as they lie; `Rimfolk_WS_BadCoin:changer/1` Send the changer on; `Rimfolk_WS_Rising:gate/1` A meal, and send {X_objective} on; `Rimfolk_WS_MineFall:help/3` The colony can't spare anything; `Rimfolk_WS_Expedition:supplies/2` Wish them well; `Rimfolk_WS_Expedition:survivor/2` Send {X_objective} on; `Rimfolk_WS_Ransom:collector/2` Nothing to spare; `Rimfolk_WS_Rats:catcher/1` Send {X_objective} on; `Rimfolk_WS_NewWay:showGrowing/2` Not now; `Rimfolk_WS_NewWay:showCrafting/2` Not now; `Rimfolk_WS_WarmSprings:send/2` Not now; `Rimfolk_WS_Rafts:sell/2` Not today; `Rimfolk_WS_Beacons:ask/2` The colony keeps its own watch; `Rimfolk_WS_ArmsSold:seller/2` Send them on; `Rimfolk_WS_MissingCaravan:ask/2` Nothing seen here; `Rimfolk_WS_Blight:buyers/2` Keep it against the blight; `Rimfolk_WS_Blight:hungry/1` Nothing to spare; `Rimfolk_WS_MedicineScarce:buy/2` Keep it for the colony's own; `Rimfolk_WS_NeighborsWar:food/2` Keep out of it; `Rimfolk_WS_NeighborsWar:steel/1` Not for a war; `Rimfolk_WS_NeighborsWar:worn/2` Send them on; `Rimfolk_WS_ColdYear:early/1` Trust the weather; `Rimfolk_WS_ColdYear:flock/2` The meadow is the colony's; `Rimfolk_WS_ColdYear:buyers/2` Keep it against the winter; `Rimfolk_WS_GreatWork:timber/2` The colony needs its wood; `Rimfolk_WS_GreatWork:builder/1` {A_nameDef} is needed here; `Rimfolk_WS_GreatWork:fall/1` The colony has none to spare; `Rimfolk_WS_DryLaw:thirsty/1` Not against their law; `Rimfolk_WS_DryLaw:smuggler/1` Send him on; `Rimfolk_WS_GreatCaravan:outriders/2` The meadow is the colony's; `Rimfolk_WS_GreatCaravan:hills/1` The colony knows no other way; `Rimfolk_WS_Trek:water/2` Not at the colony's water; `Rimfolk_WS_Trek:slow/2` He must go on; `Rimfolk_WS_Trek:stay/1` Send them on to their kin; `Rimfolk_WS_ClothScarce:buy/2` Keep the cloth; `Rimfolk_WS_ClothScarce:teach/1` Thank them and send them on; `Rimfolk_WS_TimberScarce:buy/2` The colony needs its wood; `Rimfolk_WS_TimberScarce:apprentice/1` {A_nameDef} is needed here; `Rimfolk_WS_Games:gamesDay/1` Not this year; `Rimfolk_WS_Pardon:meal/1` Send them on; `Rimfolk_WS_HiringFair:seeker/1` Send them on to the fair; `Rimfolk_WS_MudRoads:load/1` The colony has enough; `Rimfolk_WS_MudRoads:shelter/1` Send them on; `Rimfolk_WS_FallingStars:starIron/1` Let it go to {PLACE}; `Rimfolk_WS_FallingStars:starMetal/1` Let it go to {PLACE}; `Rimfolk_WS_GreatBurying:linen/1` The colony needs its cloth; `Rimfolk_WS_GreatBurying:ourDead/1` The colony sees to its own ground; `Rimfolk_WS_BridgeDown:timber/2` The colony needs its wood; `Rimfolk_WS_BridgeDown:rest/1` Send them on; `Rimfolk_WS_SonGate:ask/5` Send him away; `Rimfolk_WS_SonExile:shelter/1` A few nights' rest, then the road; `Rimfolk_WS_SonExile:shelter/2` Turn him away; `Rimfolk_WS_SonRiders:work/2` Send them on; `Council_WS_Prophet:followers/2` The gate stays shut; `Council_WS_Prophet:sermon/1` Give her a bed for the night, no more; `Council_WS_Prophet:sermon/2` Ask her to move on; `Council_WS_Vault:writing/2` {A_nameDef} is needed here; `Council_WS_Vault:dealer/1` Not today; `Council_WS_Vault:door/2` It isn't the colony's door; `Council_WS_Embargo:cloth/1` Send him on his way; `Council_WS_Embargo:smuggler/1` Send him on; `Council_WS_Embargo:talks/1` Let them sort it out; `Council_WS_HouseFails:stock/1` The colony doesn't need it; `Council_WS_HouseFails:driver/1` Pay him a day's wage and send him on; `Council_WS_HouseFails:driver/2` No work here; `Council_WS_HouseFails:loan/2` The colony doesn't lend; `Council_WS_HouseFails:wagon/1` The colony can't spare anyone; `Council_WS_IceRoad:poles/2` The colony needs its wood; `Council_WS_IceRoad:through/2` Send him on as he is; `Council_WS_IceRoad:caravan/2` Nothing to spare; `Council_WS_IceRoad:thaw/1` Leave it to {FACTION}; `Council_WS_Moot:herald/2` The colony will stay home; `Council_WS_Moot:delegates/1` Point them down the road; `Council_WS_Moot:pastures/3` Abstain; `Council_WS_OldBeast:ridge/1` Keep the colony off the high ground; `Council_WS_OldBeast:watch/1` The colony can't spare anyone; `Council_WS_RivalColony:landing/2` Hear them out, and promise nothing; `Council_WS_RivalColony:turning/3` Let things stay as they are; `Council_WS_RoadTraffic:robbed/2` Send him on; `Council_WS_Crossroads:name/1` Let it keep its own name.

### World: words that promise more than the effects do (0; 6 more read and left (the skip list))

None.

### Encounters: moments with only a mood or words (by design; read for words that promise more) (193)

`Council_EncC_DoorGreeting`; `Council_EncC_WarmFeet`; `Council_EncC_PlateThief`; `Council_EncC_DeadGift`; `Council_EncC_KnewIWasSad`; `Council_EncC_ChewedUp`; `Council_EncC_LarderRaid`; `Council_EncC_ChildAndAnimal`; `Council_EncC_AdoptedBy`; `Council_EncC_ToldItEverything`; `Council_EncC_AnimalGrudge`; `Council_EncC_WorkShadow`; `Council_EncC_OldFriend`; `Council_EncC_NewLitter`; `Council_EncC_StoodBetween`; `Council_EncC_MistMorning`; `Council_EncC_HerdPassing`; `Council_EncC_Birdsong`; `Council_EncC_Stalker`; `Council_EncC_CropThief`; `Council_EncC_HugeTracks`; `Council_EncC_BeastSighted`; `Council_EncC_SmallTheft`; `Council_EncC_NightWalker`; `Council_EncC_RutFight`; `Council_EncL_DesertShade`; `Council_EncL_DesertTraveler`; `Council_EncL_DesertNight`; `Council_EncL_FrostBreath`; `Council_EncL_ColdTrick`; `Council_EncL_Undergrowth`; `Council_EncL_Reeds`; `Council_EncL_MarshLights`; `Council_EncL_Ridge`; `Council_EncL_HighPass`; `Council_EncL_CaveMouth`; `Council_EncL_TideLine`; `Council_EncL_ShellGift`; `Council_EncL_SurfWatch`; `Council_EncS_FirstGreen`; `Council_EncS_ReadyForWinter`; `Council_EncS_SummerWade`; `Council_EncW_RainEaves`; `Council_EncW_FogShape`; `Council_EncW_FirstSnow`; `Council_EncA_Wings`; `Council_EncA_Grazers`; `Council_EncA_Mits`; `Council_EncA_Drifters`; `Council_EncA_NightEyes`; `Council_EncA_Gallatross`; `Council_EncA_Graveyard`; `Council_EncA_Meadow`; `Council_EncA_Mushrooms`; `Council_EncA_OcularForest`; `Council_EncF_CourtNews`; `Council_EncF_SenateTalk`; `Council_EncF_SettlerTales`; `Council_EncH_RoomPraised`; `Council_EncH_RoomFaulted`; `Council_EncH_HomeStories`; `Council_EncH_LentAHand`; `Council_EncL_CanyonEcho`; `Council_EncL_ValleyMist`; `Council_EncL_ValleyTraveler`; `Council_EncL_IslandShore`; `Council_EncL_CaveDusk`; `Council_EncL_CliffFoot`; `Council_EncL_OasisDrink`; `Council_EncL_CraterTale`; `Council_EncW_WindyDay`; `Council_EncW_DustShelter`; `Council_EncM_CheeseTasting`; `Council_EncM_WineTalk`; `Council_EncM_ByTheHearth`; `Council_EncM_FreshBread`; `Council_EncM_Beekeeping`; `Council_EncP_RoadNews`; `Council_EncP_AdmiredWalls`; `Council_EncP_FireSongs`; `Council_EncP_Flirt`; `Council_EncP_Reputation`; `Council_EncP_StaringGuard`; `Council_EncP_VisitorDog`; `Council_EncP_Dice`; `Council_EncP_HaggleLoss`; `Council_EncP_FarPlaces`; `Council_EncP_Cheated`; `Council_EncP_WorkTraveled`; `Council_EncP_CaravanGossip`; `Council_EncP_FreeSample`; `Council_EncP_RefugeeThanks`; `Council_EncP_Taunts`; `Council_EncP_FoodCritic`; `Council_EncP_Bribe`; `Council_EncP_Begging`; `Council_EncP_Cursing`; `Council_EncP_LastWords`; `Council_EncP_SpatOn`; `Council_EncP_FarewellPostponed`; `Council_EncR_ChildrenClose`; `Council_EncR_OwnBread`; `Council_EncR_ElderPraise`; `Council_EncR_NothingWhereItWas`; `Council_EncR_HandsVisible`; `Council_EncR_AdmiredFields`; `Council_EncR_WhatForFun`; `Council_EncR_EnviesPace`; `Council_EncR_SecondLunch`; `Council_EncK_HelpedGreeted`; `Council_EncO_EastbornTea`; `Council_EncO_MotzDusk`; `Council_EncO_VathunCaste`; `Council_EncM_KnightOnTheRoad`; `Council_EncPW_Watching`; `Council_EncPW_Shiny`; `Council_EncPW_ChildAndWild`; `Council_EncPW_Partner`; `Council_EncD_WaterBirds`; `Council_EncD_ShoreBeasts`; `Council_EncD_OldBeasts`; `Council_EncD_Merren`; `Council_EncM_ChildNames`; `Council_EncM_Lifter`; `Council_EncE_Unease`; `Rimfolk_EncPE_LeagueBook`; `Rimfolk_EncPE_LeagueFord`; `Rimfolk_EncPE_OrderOldInjury`; `Rimfolk_EncPE_OrderLamp`; `Rimfolk_EncPE_HerdersTamsa`; `Rimfolk_EncPE_PilgrimsGrave`; `Rimfolk_EncPE_PilgrimsShells`; `Rimfolk_EncPE_ScholarsHesk`; `Rimfolk_EncPE_EnclaveFigure`; `Rimfolk_EncPE_EnclaveManifest`; `Rimfolk_EncPE_RiverKnotNight`; `Rimfolk_EncPE_WardensLantern`; `Rimfolk_EncPE_BellGraves`; `Rimfolk_EncPE_BellFields`; `Rimfolk_EncPE_TamerSash`; `Rimfolk_EncPE_WheelFire`; `Rimfolk_EncPE_GlassSky`; `Rimfolk_EncPE_GlassVial`; `Rimfolk_EncPE_MossGrove`; `Rimfolk_EncPE_BannerScars`; `Rimfolk_EncPE_BannerRoll`; `Rimfolk_EncPE_GuildBridge`; `Rimfolk_EncPE_LineFirstMast`; `Rimfolk_EncPE_UnionVote`; `Rimfolk_EncPE_UnionBook`; `Rimfolk_EncPE_RepublicCount`; `Rimfolk_EncPE_RepublicPostmaster`; `Rimfolk_EncPE_FleetLog`; `Rimfolk_EncPE_FleetOrder`; `Rimfolk_EncPE_HelixConsent`; `Rimfolk_EncPE_HelixVats`; `Rimfolk_EncPE_LatticeField`; `Rimfolk_EncPE_ChoirHum`; `Rimfolk_EncPE_ChoirQuiet`; `Rimfolk_EncPE_VaultSky`; `Council_EncM_ColdShoulder`; `Council_EncM_DebtMentioned`; `Council_EncN_Aurora`; `Council_EncN_Eclipse`; `Council_EncN_FallingStar`; `Council_EncN_MoonTale`; `Council_EncW_HeatLightning`; `Council_EncW_FogNight`; `Council_EncW_FirstFrost`; `Council_EncW_Blizzard`; `Council_EncL_LakeMorning`; `Council_EncL_LakeIce`; `Council_EncL_LakeLines`; `Council_EncL_RiverFord`; `Council_EncL_RiverDawn`; `Council_EncL_RiverHigh`; `Council_EncL_TideWrack`; `Council_EncL_TidePools`; `Rimfolk_EncWH_PlagueDistance`; `Rimfolk_EncWH_PlagueAsks`; `Rimfolk_EncWH_FaminePocket`; `Rimfolk_EncWH_FamineGrain`; `Rimfolk_EncWH_DroughtWater`; `Rimfolk_EncWH_PilgrimKneels`; `Rimfolk_EncWH_PilgrimPrayer`; `Rimfolk_EncWH_WarFire`; `Rimfolk_EncWH_CometWatch`; `Rimfolk_EncWH_FairRibbon`; `Rimfolk_EncWH_DroverDog`; `Rimfolk_EncWH_IceRoadWarm`; `Rimfolk_EncWH_SongHum`; `Rimfolk_EncWH_MourningNames`; `Rimfolk_R5A_Enc_Grace`.

### Encounters: words that promise more than the effects do (0; 22 more read and left (the skip list))

None.

### Camp kinds that bring nothing of their own over a stay (0; 1 more read and left (the skip list))

None.

### Camp kinds whose asks are all flavour (0; 5 more read and left (the skip list))

None.

### What each camp kind brings of its own

- `Council_Camp_Deserters`: good turns (wood); asks; requests
- `Council_Camp_Drovers`: good turns (milk, tales); asks; requests
- `Council_Camp_FarMerchants`: a trader; good turns (tales, goods); asks
- `Council_Camp_Healers`: good turns (heal); asks; requests
- `Council_Camp_Hermit`: good turns (medicine, meat); a gift left behind; may settle; asks
- `Council_Camp_Hunters`: good turns (meat); asks; requests
- `Council_Camp_LoneHealer`: good turns (heal); asks
- `Council_Camp_Market`: a trader; asks; requests
- `Council_Camp_Minstrels`: good turns (tales); asks
- `Council_Camp_Mourners`: good turns (tales); a gift left behind; asks
- `Council_Camp_Penitents`: good turns (tales); asks
- `Council_Camp_Pilgrims`: good turns (medicine); asks
- `Council_Camp_Players`: a gift left behind; asks
- `Council_Camp_Procession`: nothing of its own
- `Council_Camp_Prospectors`: a trader; asks; requests
- `Council_Camp_Refugees`: good turns (wood); may settle; asks; requests
- `Council_Camp_SaltCaravan`: a trader; good turns (goods, tales); asks; requests
- `Council_Camp_Smith`: a trader; good turns (mend, repair); asks; requests
- `Council_Camp_Surveyors`: good turns (survey, wood); asks; requests
- `Council_Camp_Trainers`: a trader; good turns (wood); asks
- `Council_Camp_TreeSingers`: good turns (heal); a gift left behind; asks
- `Council_Camp_WarParty`: a trader; asks
- `Council_Camp_Wardens`: good turns (wood); may settle; asks
- `Rimfolk_Camp_AfterFire`: good turns (tales); asks; ways of its own (CampWorker_AfterFire)
- `Rimfolk_Camp_BannerSquad`: work on the land (contract); asks; requests
- `Rimfolk_Camp_BeastShow`: good turns (tales); asks
- `Rimfolk_Camp_Beekeepers`: a trader; good turns (mend); asks; ways of its own (CampWorker_Beekeepers)
- `Rimfolk_Camp_BoatBuilders`: good turns (wood, tales); asks; ways of its own (CampWorker_BoatBuilders)
- `Rimfolk_Camp_Catchers`: good turns (tales); work on the land (catch); asks; requests
- `Rimfolk_Camp_Cheesemakers`: a trader; good turns (milk, tales); asks; ways of its own (CampWorker_R4KCheesemakers)
- `Rimfolk_Camp_CircuitRing`: good turns (tales); asks; ways of its own (CampWorker_CircuitRing)
- `Rimfolk_Camp_CompanyMarch`: asks; requests; ways of its own (CampWorker_CompanyMarch)
- `Rimfolk_Camp_Diggers`: good turns (wood); asks; ways of its own (CampWorker_Diggers)
- `Rimfolk_Camp_DryWater`: good turns (milk, tales); a gift left behind; asks; ways of its own (CampWorker_DryWater)
- `Rimfolk_Camp_Dyers`: a trader; good turns (tales); asks; ways of its own (CampWorker_R3Dyers)
- `Rimfolk_Camp_EnclavePad`: a trader; asks; requests; ways of its own (CampWorker_EnclavePad)
- `Rimfolk_Camp_FieldLab`: good turns (tales); asks; ways of its own (CampWorker_FieldLab)
- `Rimfolk_Camp_FieldOfFallen`: good turns (tales); ways of its own (CampWorker_FieldOfFallen)
- `Rimfolk_Camp_FieldSickhouse`: good turns (medicine, tales); asks; requests; ways of its own (CampWorker_Sickhouse)
- `Rimfolk_Camp_FireWatchers`: good turns (tales); asks; ways of its own (CampWorker_R4KFireWatchers)
- `Rimfolk_Camp_Fishers`: good turns (fish, tales); work on the land (fish); asks
- `Rimfolk_Camp_FloodedOut`: good turns (fish, mend); work on the land (fish); asks; ways of its own (CampWorker_FloodedOut)
- `Rimfolk_Camp_Gamblers`: good turns (tales); asks; ways of its own (CampWorker_R3Gamblers)
- `Rimfolk_Camp_GamesTeam`: good turns (meat); asks
- `Rimfolk_Camp_Gleaners`: good turns (wood); asks; requests
- `Rimfolk_Camp_GodPilgrims`: good turns (tales); asks; ways of its own (CampWorker_GodPilgrims)
- `Rimfolk_Camp_GodSeer`: good turns (tales); ways of its own (CampWorker_GodSeer)
- `Rimfolk_Camp_HarvestHands`: good turns (tales); work on the land (reap); asks; requests
- `Rimfolk_Camp_HerbGatherers`: good turns (medicine); asks; requests
- `Rimfolk_Camp_HerdApart`: good turns (tales); asks; ways of its own (CampWorker_HerdApart)
- `Rimfolk_Camp_HerdFollowers`: good turns (meat, tales); asks; requests
- `Rimfolk_Camp_HiredGuard`: work on the land (contract); requests
- `Rimfolk_Camp_IceFishers`: good turns (fish, tales); work on the land (icefish); asks
- `Rimfolk_Camp_InstrumentMakers`: good turns (tales); asks; ways of its own (CampWorker_R4KInstruments)
- `Rimfolk_Camp_Kennel`: good turns (tales); a gift left behind; asks; ways of its own (CampWorker_R3Kennel)
- `Rimfolk_Camp_LandingParty`: good turns (tales); asks; requests; ways of its own (CampWorker_LandingParty)
- `Rimfolk_Camp_LanternPost`: good turns (tales); asks; requests; ways of its own (CampWorker_LanternPost)
- `Rimfolk_Camp_LeagueWinter`: a trader; good turns (goods, tales); a gift left behind; asks
- `Rimfolk_Camp_LineCrew`: good turns (tales); asks; ways of its own (CampWorker_LineCrew)
- `Rimfolk_Camp_ListeningRing`: good turns (tales); asks; ways of its own (CampWorker_ListeningRing)
- `Rimfolk_Camp_LordsHunt`: good turns (meat); requests; ways of its own (CampWorker_LordsHunt)
- `Rimfolk_Camp_MechYard`: good turns (tales); asks; ways of its own (CampWorker_MechYard)
- `Rimfolk_Camp_MushroomPickers`: a trader; good turns (medicine); asks; requests; ways of its own (CampWorker_MushroomPickers)
- `Rimfolk_Camp_Novices`: good turns (tales); asks; ways of its own (CampWorker_Novices)
- `Rimfolk_Camp_Picket`: good turns (tales); asks; requests; ways of its own (CampWorker_Picket)
- `Rimfolk_Camp_PilgrimHostel`: good turns (medicine, tales); asks; requests; ways of its own (CampWorker_Hostel)
- `Rimfolk_Camp_PutAwayPriest`: good turns (tales); ways of its own (CampWorker_PutAwayPriest)
- `Rimfolk_Camp_RagPickers`: good turns (mend); asks; ways of its own (CampWorker_R3RagPickers)
- `Rimfolk_Camp_RainCallers`: ways of its own (CampWorker_R3RainCallers)
- `Rimfolk_Camp_RationPost`: good turns (tales); asks; requests; ways of its own (CampWorker_RationPost)
- `Rimfolk_Camp_RoadGang`: good turns (tales); asks; requests; ways of its own (CampWorker_RoadGang)
- `Rimfolk_Camp_Salvagers`: good turns (scrap, tales); work on the land (strip); asks
- `Rimfolk_Camp_Scholars`: good turns (tales, book); asks; requests
- `Rimfolk_Camp_ScholarsMeasure`: good turns (tales); ways of its own (CampWorker_Measure)
- `Rimfolk_Camp_Settlers`: good turns (wood)
- `Rimfolk_Camp_Shearers`: good turns (tales); work on the land (shear); asks; requests
- `Rimfolk_Camp_Smugglers`: a trader; asks; ways of its own (CampWorker_Smugglers)
- `Rimfolk_Camp_SnowCarvers`: good turns (tales); asks; ways of its own (CampWorker_R4KSnowCarvers)
- `Rimfolk_Camp_SnowboundCaravan`: a trader; good turns (goods, tales); asks; requests
- `Rimfolk_Camp_Spacers`: good turns (repair, tales); work on the land (hull); asks
- `Rimfolk_Camp_SpringCalving`: good turns (milk, tales); asks; ways of its own (CampWorker_Calving)
- `Rimfolk_Camp_StarWatchers`: good turns (mend); asks; ways of its own (CampWorker_StarWatchers)
- `Rimfolk_Camp_StoneRaisers`: requests; ways of its own (CampWorker_StoneRaisers)
- `Rimfolk_Camp_Stonecutters`: good turns (scrap); work on the land (cut); asks; requests
- `Rimfolk_Camp_StormTower`: good turns (tales); asks; ways of its own (CampWorker_StormTower)
- `Rimfolk_Camp_StrandedCarters`: a trader; good turns (mend); asks
- `Rimfolk_Camp_SummerSchool`: good turns (tales); asks; ways of its own (CampWorker_SummerSchool)
- `Rimfolk_Camp_SurfaceScouts`: good turns (tales); asks; ways of its own (CampWorker_SurfaceScouts)
- `Rimfolk_Camp_Teacher`: good turns (tales); asks; requests
- `Rimfolk_Camp_Trappers`: good turns (meat, tales); asks; requests
- `Rimfolk_Camp_WalkingHome`: good turns (wood); asks
- `Rimfolk_Camp_Watchers`: good turns (tales); work on the land (watch); asks; requests
- `Rimfolk_Camp_WheelYard`: a trader; good turns (mend, tales); asks; ways of its own (CampWorker_WheelYard)
- `Rimfolk_Camp_Wildfowlers`: good turns (meat, tales); asks; ways of its own (CampWorker_R4KWildfowlers)
- `Rimfolk_Camp_WinterHerd`: good turns (milk, tales); a gift left behind; asks
- `Rimfolk_Camp_WinterPilgrims`: good turns (tales); a gift left behind; asks; requests
- `Rimfolk_Camp_WitchFinders`: good turns (tales); ways of its own (CampWorker_R3WitchFinders)
- `Rimfolk_Camp_Woodcarvers`: good turns (tales); asks; ways of its own (CampWorker_R4KWoodcarvers)
- `Rimfolk_Camp_Woodcutters`: good turns (wood); asks; requests
- `Rimfolk_Camp_WorkThatStays`: good turns (tales); asks; ways of its own (CampWorker_WorkThatStays)
- `Rimfolk_Camp_Wounded`: good turns (tales); asks; requests
- `Rimfolk_Camp_YoungHands`: good turns (wood); asks

### World events whose answers are all flavour (0; 5 more read and left (the skip list))

None.

### Missions: what they end with (read from their workers' code)

117 missions; 0 with no gain found in their code:



<!-- world_impact_audit: end -->
