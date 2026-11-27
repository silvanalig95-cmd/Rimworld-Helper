# Review of content rounds 2 to 4, before they meet the player

Releases 0.308.0 to 0.316.0 added about 130 events, camps, world events, council questions, happenings of the gods,
personal stories and wishes, built quickly by parallel builders. Every static check and test passed, but none of it had
run in the game. This review read the code of the three rounds (and the shared edits they made) for what would show in
play: exceptions, saving and loading, jobs and play-outs that can't run, wrong game calls, the player's rules (harm through
the shared budget and shown, answers that play out, the unseen gods), and things that fire too often or never. The player's
rule: bug fixes come before features.

Read: `Camps/CampRound2.cs`, `CampRound3.cs`, `CampRound4.cs`; `World/Variety/WorldRound2.cs`, `WorldRound3.cs`,
`WorldRound4.cs` and their math; `Gods/OnMap/Between/BetweenFaith.cs`, `BetweenFaithHome.cs`, `BetweenFaithSigns.cs`,
`BetweenRound2Gods.cs` (with `Patch_BetweenMachineLost` and `Patch_BetweenMechLost`), `OwnBetweenFaithMath.cs`,
`Gods/OnMap/OwnRound3.cs`; `Happenings/Workers_Round2.cs`, `Workers_Round3.cs`, `Workers_Round4.cs` and their math;
`Dilemmas/DilemmaWorkers_Round2.cs`, `_Round3.cs`, `_Round4.cs`; `Arcs/Round4Lives.cs`; the shared edits in
`PlayOut/OnMapTimed.cs`, `OwnHappeningsMath.cs`, `Between.cs`, `Workers_Storyteller.cs` and `Social/FamilyInheritance.cs`.

The fixes are kept by `Tools/Tests/ReviewRounds2to4Tests.cs`.

## Fixed

Most serious first.

1. **The ordeal at the hearth burned people with no ordeal held** (`Dilemmas/DilemmaWorkers_Round2.cs`, `OrdealFire.Step`).
   When the answer's gathering could not be set going (play-outs off, nobody free), when it was still waiting past its
   time, or when it had ended, the step went straight on to the ordeal: in the older ages each of the two could take a
   burn across the hands wherever they were, and the message said they "carried the hot stone across the hearth before the
   colony". Now the ordeal is held only while its gathering runs with both of them at it; otherwise it ends with nothing
   done and nobody burned. (With play-outs off the answer's card already showed no ordeal and no risk.)
2. **The clothes from the stores sent the downed, the drafted and those in a mental break** (`Happenings/Workers_Round3.cs`,
   `DilemmaEffect_R3Clothes.Spares`). The answer started the game's own "wear" job on everyone in worn clothes, including
   a colonist lying downed (the game logs an error when a downed pawn is made to walk), one the player had drafted (taken
   out of the player's hands) and one in a break. Now only those who can walk to the stores are listed and sent.
3. **An offering taken in the night could be taken from someone's hands** (`Gods/OnMap/Between/BetweenFaithHome.cs`,
   `FaithTakenOffering.Step`). The taker's step only checked that the offering was still waiting, not that it still lay at
   the god's place; one already picked up or carried off would be put into the taker's pockets while still in another
   container (errors, and the thing in two places). Now the scene ends when the offering is no longer lying on the map.
4. **The stone-raisers' carved stone used up their stone and stood on people** (`Camps/CampRound2.cs`,
   `CampWorker_StoneRaisers.Stand` and `DayJob`). For a stone carved in place, the blocks they brought were destroyed before
   the ground was checked, so on ground not clear the blocks vanished and the stone never stood; and the carvers' own work
   spots could be on the stone's ground, so the finished stone could be set down on a carver (stuck inside it until the
   game moved them out). Now the ground is checked first, nobody may stand on it, and the carvers work beside it.
5. **The one who hid could stay on the land for good** (`Happenings/Workers_Round2.cs`, `Hidden.End`). When the arrest was
   given up (the arrester drafted or busy), its time ran out, or the cap of things kept on the map let it go, the raider,
   nobody's and without a lord, was left wandering the colony's land with no end. Now, when it ends with them still about,
   free and nobody's, they slip away off the land.
6. **The arrest of the one who hid could be ordered of someone down or gone** (`DilemmaEffect_Hidden`). The answer was
   offered while the one who found them was downed, in a break, dead or off the map, and then gave that pawn the arrest
   job. Now the answer is greyed out with the reason, and nothing is ordered of them.
7. **The round 4 caches outlived the game they were made in** (`Arcs/Round4Lives.cs`). The coldest month of a map, the
   deep snow at home, when a wish's visit was last arranged and the benches' "something new to make" list were kept in
   static tables by map and wish number, which start again in every game: after loading a save or starting a new game the
   old game's values were read (a winter that isn't, a wish's visit held back half a day, a reason naming a bench of the old
   game). They are now kept for one game only (`R4LGameCache`).
8. **What the storm threw up stayed forbidden when it ended early** (`Happenings/Workers_Round4.cs`, `R4EMap.End`). The
   heaps on the shore are held back from the haulers until the answer; when the thing kept on the map ended before the sea
   took them back (the cap), they stayed forbidden and rotted there. Now they are let go to the haulers then.
9. **A wild animal gone mad wasn't counted in the shared harm budget** (`WrongBeast.Mad`). The game's own madness came at
   the budget's roll, but was never recorded, so the budget did not hold back the next hurt of its kind as it does for a
   beast that turns on someone elsewhere (`SurfaceScouts`). Now it is recorded.

## Looked at and left

- **The field let go in a drought is not sown again** (`DilemmaEffect_FieldWater`, `letgo`): its sowing is switched off
  and stays off until the player turns it back on in the zone. It is what "let go" says and is shown on the zone; left as
  designed, noted for the player.
- **The bad air's gas is not recorded in the harm book** (`HappeningWorker_BadAir.Begin`): the game's own tox gas, its
  amount scaled by the budget's share for the dangers; the event itself only comes with hurts on. Left.
- **The guest at the burying is not counted as one of the gods' strangers at the home** (`EnnuBurying`): it checks for
  another, but doesn't mark itself, so another god's stranger could come while it stands at the grave. Rare and harmless;
  left.
- **"Came through a near death" counts moderate blood loss** (`OwnHappeningsMath.BledFrom` 0.3): a design threshold, not
  a bug; may come more often after fights than the words suggest.
- **Dinner in the field marks who ate by their place in the list** (`Round3Dinner`): a load that loses one of them shifts
  the marks. Very rare and only changes who gets the good memory; left.
- **The instrument-makers' playing after a load** (`JobDriver_R4KPlay`): the driver's own note that it started the music
  isn't saved, so after a load mid-tune the driver won't stop it; the game's instrument then plays on until its player
  stops. Could not be checked against the game's own instrument without running it; left.
- Checked and sound: the `Building.Destroy` and `Pawn.Kill` patches (cheap early exits, inside try), the gods' cards (by
  `OwnHappenings.CardOf`, the god named only where the colony knows it), the new `OnMapTimed` kinds (dispatch and saving),
  the happenings' "last" list growing with the enum, the storyteller's new memo kinds, and the round 2 to 4 hurts (frost,
  bites, slips, burns, the snake's bite) through `Harm.Roll`, `Harm.Ail` and `Harm.Injure` with their risk named on the
  answer.
