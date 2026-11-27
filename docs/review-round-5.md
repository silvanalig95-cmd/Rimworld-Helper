# Review of content round 5, before it meets the player

Releases 0.317.0 and 0.318.0 added eight social events, six shared memories, six quirks, six missions and eight
encounters, built quickly by parallel builders. Every static check and test passed, but none of it had run in the game.
This review read the round's code and defs for what would show in play, as the review of rounds 2 to 4 did
(`docs/review-rounds-2-to-4.md`): exceptions, saving and loading, jobs and play-outs that can't run, wrong game calls, a
mission that can be taken but never ends or pays wrongly, words that say what the code doesn't do, harm outside the shared
budget, the unseen gods, and things that fire too often or never. The player's rule: bug fixes come before features.

Read: `Social/Round5Together.cs`, `Social/Round5TogetherMath.cs`, the round's edits to `Social/SocialEvents.cs` (the
`scene` field, `FitsLife` and `Fire`) and `Immersion/HabitBubbles.cs`; `Missions/Round5Away.cs`,
`Missions/Round5AwayMath.cs`; their defs (social events, shared memories and their talk, quirks, thoughts, dreams, bubbles,
knacks, missions, encounters) and words; and the frames they stand on where the round relies on them
(`Missions/Workers_MissionDelivery.cs`, `Workers_MissionNeighbours4.cs`, `Workers_MissionRoad.cs`, `Missions.cs`'s goods,
`Strangers/Encounters.cs`, `Harm/WildHurts.cs`'s wild one too close, `Flavor.Pick`).

The fixes are kept by `Tools/Tests/ReviewRound5Tests.cs`.

## Fixed

Most serious first.

1. **Cloth or leather didn't count leather** (`Missions/Missions.cs`, `Fits`, the `textiles` goal). A delivery's goal of
   "cloth or leather" counted only what the game files directly under textiles (cloth, synthread, devilstrand, hyperweave),
   not the leathers and wools, which sit in the textiles' own sub-categories. So a colony with plenty of leather in its
   stores could not fill it: the drum for a neighbour's watch (R5A-M6, "the hide and wood to make it", the tribal colony's
   form) came short or was missed, with goodwill lost, and so could the older deliveries that ask for cloth or leather (a
   wedding's goods, the rebuilding, the provisions, the stores for the road, two of the peoples' own). Now a delivery counts
   the textiles and everything under them, as its words say. This changes what the older deliveries count too, to match
   their words.
2. **The bell's words and title for a drum, a siren or a beacon** (`Defs/MissionDefs/Rimfolk_Round5_Missions.xml`,
   R5A-M6). A mission's kind is one of the tags its lines are picked by (`Missions.Tags`), and the bell's kind was `bell`,
   the same as the medieval colony's form. So the bell's lines were open to every colony: a tribal colony asked for cloth
   and wood could get the title "A bell for ..." and the words about metal for the bell and timber for its frame, and at the
   end "the bell will hang in the tower". The kind is now `alarm`, and the bell's lines come with the bell only.
3. **The autumn round-up's words in spring and summer** (the same file, R5A-M1). The same with the round-up: its kind was
   `roundup`, the autumn form's tag, so in spring or summer the request could say the herds come down "before the snow" or
   are brought in "before winter" while the colony was asked for the lambing or the shearing. The kind is now `herding`.
   (Neither kind is shown anywhere; it only keeps the missions of a kind apart between offers.)
4. **The merchants' order could ask for mortar shells** (`Missions/Round5Away.cs`, `R5AOrders.Sellable`, R5A-M3). The game
   does not count a mortar shell as a weapon, so a colony keeping ten or more of its own shells could be asked to sell them
   all as "goods". The order is never for weapons; now never for shells either. The groups' goods are also left out by their
   category or one under it (leathers and wools under the textiles), so no one thing of the colony's is ordered apart from
   the group that already counts it. This changes what the order may pick (no shells).

## Looked at and left

- **The order can ask for more than the colony keeps, and for all of it** (R5A-M3, `Round5AwayMath.OrderCount`): about as
  much as the colony has, a third less to a third more, of whatever it picks: its meals, its medicine, its chemfuel or its
  kibble too. That is the round's design ("about as much of it as the colony keeps, give or take a third"); the player sees
  the goods named before taking the order on. Left.
- **Stock someone has claimed counts toward the order** (`Missions.Count` and `Take`, every delivery): a meal a colonist is
  about to eat or an ingredient a cook has claimed counts as ready, while the carts take only what nobody has claimed, so a
  delivery that looked ready can come a little short and pay as part of it. Shared by every delivery, not the round's. Left.
- **The wild one in the weather is words only** (R5A-E3): "went up closer than was wise" is not walked on the map, and the
  harm rolls only within eight cells of the animal while the encounter can be met from sixteen, so the hurt comes less often
  than the list's "about one in twenty". The same as the older encounters with the wild one too close
  (`Council_EncC_GreatBeastClose`); the harm itself goes through `Harm.Roll` and `HarmKinds.TooClose` and is shown. Left.
- **The beacon's "steel where there is none"** (R5A-M6): the delivery frame's `instead` only takes over when the game has
  no such thing or a research is not done, so a spacer colony with no plasteel in its stores is still asked for plasteel.
  Only the list says otherwise; the words in the game name what is asked. Left as the frame's rule.
- **Old and small hands at the hauling or the cleaning** (`Rimfolk_R5T_Soc_OldAndSmallHands`): the game's hauling and
  cleaning have no skill, so the child learns nothing there (at the fields they learn plants). The words name no skill.
  Left.
- **The captive let go "with nothing"** (R5A-M5, `[released]`): the one walked home is made by the game with their people's
  usual clothes and gear. A small difference in the words; making them come empty-handed would be new work. Left.
- **The order looks through the stores six times when the storyteller asks** (`HappeningWorker_R5AOrder.Possible`): five
  groups and the things, only while no mission is open; never on a tick. Left.
- Checked and sound: the four scenes read from the map (`Round5Together.Fits`/`Fill`: both sowing, both a year at the
  colony by the game's record, the same game building by its id, a child's loss in five days by the game's own memory of
  the death, naming someone dead and of the colony), the cast only between two near each other (awake, not downed,
  drafted or in a break), the shared memories' labels ({YEAR} and {SEASON} by the map's calendar, a land of one season
  named as the others, {THING} and {X_nameDef} filled before the effects run), the six words over the head (hourly, at a
  one in four chance, through the habits' own rest and `Speech.Say`, only where the quirk fits), the quirks' and dreams'
  thoughts and tags, nothing ticked or saved and no static kept between games (the order's list of what recipes make is
  of the defs, the same in every game); the five missions on the frames there are (their forms, seasons and ages, every
  ending written, the order's goods only unforbidden and in storage, as the delivery counts them, its pay half again their
  plain worth and its endings the delivery's own, so it always ends by its day); the eight encounters (their strangers,
  lands, seasons and moments, the loom's buildings from the player's active mods, the grace's visit at a table), no god
  named and no countdown anywhere, and the one hurt of the round through the shared budget.
