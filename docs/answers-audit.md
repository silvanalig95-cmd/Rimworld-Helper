# Answers against the map: the audit

The player's rule (CLAUDE.md): events and answers show on the map as accurately as possible, so what the words say
happens is what the player sees. This audit (27 October 2026, at 0.17.0) went through every answer's words and what
its effects really do on the map. Line numbers are as of 0.17.0.

Done since: B1, B2, B3 and B5 in 0.17.1 (feasts cooked first, risks held for the scene, errands in loads, missed
visits say so); B4 in 0.17.2 (made at the bench first, trees felled and saplings planted by hand, the room staked at the
walk's end, the four storyteller events' results after the work), which also covers ranked gaps 1 (feasts), 4, 5, 7,
10, 11 (saplings; the seeds of Story_Seeds are still open) and 18. 0.17.4 closed gaps 2 (sit-down visits, PlaySit.cs), 3 (guests at the table, PlayGuests.cs) and 6 (departures after the send-off, PlayDepart.cs). Next: the other ranked gaps by visibility (8 fed strangers, 9 bonfires, 12 work parties, 13 the sculpture, 14 burials and markers, 15 to 21).


**Path shorthand:** code paths are under Source/ColonyCouncil/, and def paths are under Defs/.

**Effort:** S = a def change or a small hook; M = a new job or a change to the engine; L = a new system.

## Ranked gaps (most visible and most common first)

1. **Feasts nobody cooks.** 21 feast answers also take raw food when the answer is given.
   - Examples: Council_Dilemma_Feast "Let A cook the feast"; PetDinner 0 "A cooked B a replacement dinner"; VictoryFeast 3 "B made a feast out of half the food"; SameMeal 0; WanderingCook "Give X the kitchen"; TasteOfHome; FirstFrostSoup 0 "The soup bubbled all afternoon".
   - On the map: `DilemmaEffect_Consume` deletes the raw food straight away (Dilemmas/Dilemmas.cs:789). The feast then serves whatever cooked meals the stores already hold (PlayOut/PlayOutLord.cs:213-240), or none. So nobody cooks, and the colony pays twice.
   - Fix: drop the Consume. A few hours before the feast, put a temporary vanilla Bill_Production on the colony's best stove (or a campfire), for the best meal recipe it has unlocked, restricted to the cook; the feast then serves those meals. **M.**

2. **"Sat down together" visits that trail someone through their work.** About 30 Person or Stranger visits.
   - Examples: Secret_LooseTongue 2 "sat down with a pot of tea… stay put until it was settled"; DlmL_FrictionGrown 0 "at the same table with a mediator"; Evt_Peacemaker 0 "at the kitchen table… until well past midnight"; EmptyChair 3, WorrierWatch 1, OldDebt 3, FavorCalled 2 "bent over the table"; Gambler 1 "sat across from X with the colony crowded round".
   - On the map: `JobDriver_PlayOutVisit` makes the visitor follow the other person wherever they go (PlayOut/PlayOutJobs.cs:116-121) while that person keeps working.
   - Fix: a `sit` mode. Both pawns take two chairs at a dining or rec table, the other gets a matching hold job, and they face each other; the same at a fire for "by the fire". **M.**

3. **Guests asked to the colony's table never come, including entertainers the colony paid.**
   - Examples: Rimfolk_DlmC_Festival 0 "The camp's people came up at dusk with their drum"; DlmC_Playmates 1 "X's family came up to supper"; DlmC_Feud 1 "sat X and a rider of ENEMY at the colony's table"; the Refugees and FarMerchants suppers; Minstrels and Players hired for silver ("will play at the colony's table tonight"); Evt_Amends 1 "X stayed for supper".
   - On the map: gatherings invite only colonists (PlayOut/PlayOutEffects.cs:122). A stranger joins only as the speaker at words that have no organizer (PlayOut/PlayOutTracker.cs:256-258).
   - Fix: a `guests` option. Bring camp members with the existing `DilemmaEffect_CampComes` machinery, timed to the start (doing "play" for the musicians); a lone visitor gets a duty to the spot. Guests are served a meal like everyone else. **M.**

4. **Things finished before anyone makes them.** `DilemmaEffect_Made` (14 uses) creates the finished item at the maker's feet when the answer is given (PlayOut/OnMapThings.cs:71-110), and only the handing over plays out.
   - Examples: Sculpture 2 "A carved a second B"; Keepsake 2 "B spent three evenings on a replacement"; Camp_Smith "the knife would be ready by evening" (the knife lies beside X at once); Evt_Scribe 1 (the bound copy exists before the evening's telling); ApprenticeBench 1 "X spent the day on a stool".
   - Fix: the maker first works at the colony's matching station (sculpting table, crafting spot, smithy) or at the camp fire, with the station's work effect and sound, and the thing appears there when the time is up. **M.**

5. **Trees felled in an instant.** `DilemmaEffect_Felled` (11 uses) turns trees into stumps and logs when the answer is given, with nobody there (PlayOut/OnMapThings.cs:245-290).
   - Examples: HiredHands 0/1 "worked the treeline all afternoon"; ThirstyHerd 2 "the drovers went at the trees… with their axes"; FeverMended 0 "took an axe to the treeline and didn't stop until the light went"; Overstay 1; CampHungry 2; CaughtInStores 1; FeeShort 1.
   - Fix: the camp already has its people fell trees with the game's own `JobDefOf.CutPlant`, no designation needed (Camps/CampWork.cs:365-371). Reuse that for camp folk, and give a visitor CutPlant jobs on the chosen trees one by one. The logs stay the colony's. **M.**

6. **Departures that happen before the send-off** (also broken, see B1).
   - Evt_CalledHome 2 "the colony saw A to the gate… turned twice on the road to wave": `DilemmaEffect_LeaveForGood` banishes A when the answer is given (Happenings/HappeningEffects_Leave.cs:33). The "seeing A off" walk comes hours later, without A.
   - Evt_LoversKin 2 "X was walked back to the road" and Evt_LostChild 0 "X is walked back toward X's people": `DilemmaEffect_SendAway` sets X walking off the map at once (Strangers/StrangerEffects.cs:55). The visit trails X to the edge and ends short.
   - PetFading 1 "walked PET out past the walls… came back alone", PetDinner 2, Evt_Stray 1 "walks the THING to the edge of the fields", FamineCull 2 "the weakest… were walked out past the fields": `SetFaction(null)` runs at once (Dilemmas/Dilemmas.cs:807, Animals/AnimalDilemmas.cs:225), and A walks out alone.
   - Fix: run the departure at the walk's far end (a PlayAfter hook). A goes with the procession and leaves from the gate; X walks beside the visitor; animals go on a rope (HerdHome) and are let go there. **M.**

7. **Risk fights and walk-outs come before the scene they describe.** Every risk effect runs when the answer is given (Dilemmas/Dilemmas.cs:1005-1007). The fight starts on the spot (Dilemmas.cs:660-669) and the shift or apology happens later.
   - Examples: Watch 0 "took the punishment, then took a swing at A"; Duel 3 "raced each other hauling stone… came to blows"; Soc_Accusation "got three words in, and turned it into a punch"; PetPot; Omen 0 "The joke went round the table at dinner. A… walked out" (A wanders off and then misses that dinner); Sculpture 0 risk.
   - Where the risk replaces the scene (Thief, Feud, the Duel handshake, LooseTongue, SwornRivals, SkipCustom), the fight happens wherever the two stand, with no supper, table or onlookers.
   - Fix: hold Fight and MentalState risk effects on the play-out and fire them when its words begin, or after the first job of a work party. **S–M.**

8. **Strangers who are fed never eat, and never stay the night.** Food is put into X's inventory, or handed to a courier who leaves at once (PlayOut/PlayGroups.cs:661-676).
   - Examples: Evt_BurnedOut 0/1 "ate it standing up… slept in the lee of its wall"; HiredHands 2; Evt_Pilgrims 0; the world-event "Feed them and send them on" beats (Famine "eat at the gate, sleep under the eaves", Abandoned and Exiles "a night in the barn", IceRoad, Moot "the guest corner").
   - Skip-list entries this stricter standard overturns: ProphetHill [0], After_SpringReturn [0], BanditRoad [robbed/0], SpiritSinger [2], MidwinterGuest [1].
   - Fix: after the hand-over, give each of them the game's Ingest job on food from their pack. For "the night", the courier lord holds a spot by the wall until dawn, with a LayDown on the ground. **M.**

9. **Bonfires that lay themselves.** With no campfire on the map, one is spawned when the gathering starts (PlayOut/PlaySpots.cs:71, `TryLayFire`). This affects up to 20 bonfire answers (Midsummer "Light the bonfire", HarvestDance, Omen 2…).
   - Fix: a campfire blueprint that the organizer finishes (the game's frame plus FinishFrame, as PlayBurial does for graves), then a lighting toil like the pyre's, then the gathering. **S–M.**

10. **Event results made before anyone goes out.**
    - Evt_StarIron 0 "works the star-iron at the forge" and 1 "broken up where it fell": the chunk is destroyed and the blade or steel placed when the answer is given (Happenings/Workers_Storyteller_Land.cs:252-271).
    - Evt_StruckTree 0/1 "splits out the best of the heartwood": the tree becomes logs at once (same file, 143-153).
    - Evt_WildBees 0 "smokes the swarm… cuts out the comb": the honey lands at A's feet first (176-188), and the visit goes to "Outdoors", not the bee tree.
    - Evt_CrashedDrone 0/1 "comes apart bolt by bolt" (Happenings/Workers_Storyteller.cs:1125-1137).
    - Fix: a visit to the place itself (the tree gets a real CutPlant; the wreck and the star get the game's ConstructMetal effect; the blade is worked at the colony's smithy), with the conversion done when the visit ends. **M.**

11. **Saplings and seeds.** `DilemmaEffect_Plant` spawns the sapling and its stone when the answer is given, at an edge cell of its own choosing (PlayOut/OnMapThings.cs:345-380). The visit or the words happen later, somewhere else.
    - Examples: ChildTrees 0 "every child planted a sapling" (the children do nothing); ChildTrees 1; DeathNews 2 "A dug a hole… set a young tree in it". Story_Seeds 0 "plants the seeds… puts a little fence round them" is only a Visit to the fields; nothing is planted at all.
    - Fix: pick the cells up front. Each planter walks there and does a short sow toil, and the sapling and stone appear then. The seeds become a small patch of a crop the colony already grows, with core fence blueprints round it. **S–M.**

12. **Work parties looser than their words.** The party's Haul is `WorkGiver_HaulGeneral` (hauling anything), and its Build only touches blueprints that already exist (PlayOut/PlayWork.cs:38-42).
    - Evt_Peacemaker 1 "rebuild the woodpile together".
    - Duel 3 "hauled stone… all afternoon".
    - Evt_LongRains 0 "patch every roof and dig proper drains" / "got up on every roof" gets Repair, Clean and Haul.
    - Secret_BloodPast 1 "the corpses and the butcher's floor" cleans near the middle of the colony instead.
    - Fix: a haul filter (wood logs to a wood stockpile; stone chunks); a Roof task using the game's build-roof area over any unroofed cells in rooms; `near` the butcher table's room. **S–M.**

13. **The sculpture that isn't there.** Council_Dilemma_Sculpture 0 "The council had the sculpture smashed. B swept up the pieces".
    - On the map: the dilemma isn't tied to any sculpture (no `things`, the default worker; DilemmaDefs/Council_Dilemmas2.xml:481-514). The WorkParty sweeps whatever filth is near the colony's middle.
    - Fix: require an art piece by A on the map and store it as the dilemma's place. B walks to it, it's destroyed with rubble left behind, the clean-up happens there, and option 2's piece is placed beside it. **M.**

14. **Burials and markers.**
    - Evt_OldGrave 0 "The bones go into fresh ground… a stone at the head. A few colonists stay by the grave": one colonist visits an existing grave.
    - Evt_Headstones 0 "X worked through the evening… a marker for every grave" (tribal variant: "raised a cairn"): X does nothing and no markers appear.
    - DlmL_FoundJournal 3 "buried the journal… under a flat stone": a walk to a random spot, with nothing placed.
    - Fix: reuse PlayBurial (PlayOut/PlayDead.cs) and set a chunk at the head; X walks grave to grave setting a stone chunk at each (the stone-picking code in `DilemmaEffect_Plant`); a chunk is set at the walk's far end. **S–M.**

15. **Smudge fires and torches that never show.** WS_Locusts 0 "smudge fires burn along the field edges day and night" is a Vigil with nothing burning; WS_Locusts 2 "The smudge fires are lit" shows nothing either; Dilemma_MarshLights 0 "A line of smoking torches wound round the colony's edge" is a gathering standing in one place.
    - Fix (no real fire): smoke flecks and the mod's glow along the field edge; MarshLights becomes a walk round the edge using `PlayVigils.Route`, with a glow on each walker. **S.**

16. **Snowmen not built.** SnowDay 0 "a snowman as tall as the door" and SnowDay 2 "built a very small snowman by the path".
    - Fix: give A (and the children) the game's own `JobDefOf.BuildSnowman` (core) during it. **S.**

17. **The lost child never appears.** DlmL_CampLostChild 0 "found the child asleep… carried the child back" and 2 "the child came walking out of the dark… leading the goat". The search finds nothing and the gate watch sees nothing.
    - Fix: require a child at the camp. The search route ends where the child is and B walks it home; for option 2, it walks in at dawn. **M–L.**

18. **The new room is marked out before anyone walks the ground.** DlmL_Crowded 2 "walked the ground… drove stakes in". `DilemmaEffect_PlanRoom` places the blueprints when the answer is given (PlayOut/OnMapThings.cs:459+), and the walk goes to any open ground.
    - Fix: walk to the plan's middle and place the blueprints when they arrive. **S.**

19. **Soup rounds carry nothing.** FirstFrostSoup 1 "A smaller pot went out to the ones working in the cold". PlayRounds only stops beside each person and says a line (PlayOut/OnMapPlays.cs:483-493).
    - Fix: the cook carries meals and hands one over with an Ingest job at each stop. **S–M.**

20. **The children's race isn't run.** ChildrensRace 0 "cheered the children round it" and 1 "The children ran their race": a gathering standing in one place, or a visit to the fields.
    - Fix: the children jog a loop of the field edge, with the gathering at the finish. **S–M.**

21. **The mushroom supper.** Evt_MushroomFlush 1 "B fried the mushrooms… ate them straight from the pan". It is only a gathering: nothing is picked, cooked or eaten.
    - Fix: a short search for raw fungus, then the cooking step from item 1. **S** once item 1 exists.

22. **Silver "paid" to a colonist just vanishes.** Keepsake "A put the coins in the same pouch", PetBite "counting it out coin by coin", Goal_Purse: Consume deletes the silver from the stores.
    - Fix: carry the stack into the recipient's inventory (a Give with silver). **S.**

## Clearly broken

- **B1. Outcome text lost (silent).** A Visit or Lesson that misses its deadline falls back silently (PlayOut/PlayOutTracker.cs:177, 578). The answer's outcome, held for the end, is only told in `Done` (lines 527-531), so it is never shown: no message at all, only the dilemma log keeps it. This is near-certain for the visits whose target leaves when the answer is given (item 6).
  - Related: `TellWhenDone` hangs the outcome on one play-out only (the gathering first). If that one falls through, the outcome is lost even when the answer's other play-out happens (DlmC_Festival 1: the carry still goes).
- **B2. Risk effects fire before their scene** (item 7).
- **B3. Feasts pay twice:** the raw food is deleted, and stored meals are eaten too (item 1).
- **B4. Results before the play-out:** Made, Plant, Felled, PlanRoom and the events' resolve code all change the map when the answer is given, ahead of the scene the words describe (items 4, 5, 10, 11, 18).
- **B5. Errand goods destroyed in the carrier's hands.** An errand to the gate with nobody to receive it destroys the carried stack (PlayOut/PlayGroups.cs:676), and any rest is taken from the stores unseen (line 681).

Nothing found that leaves things forbidden: every `SetForbidden(true)` on a gift has a matching unforbid or fall-through.

