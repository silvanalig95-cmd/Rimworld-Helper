# Content round 3: twelve council questions

The player's go of 27 November 2026: more content inside the mechanics there are, no new mechanics. Twelve council
questions (`DilemmaDef`s), spread over everyday colony life, work and food, the seasons, guests and the colony's
neighbours, children and elders, crime and fairness, and faith (two of faith). Built in
`Defs/DilemmaDefs/Rimfolk_Round3_Council.xml`, `Languages/English/Keyed/Rimfolk_Round3_Council.xml`,
`Source/ColonyCouncil/Dilemmas/DilemmaWorkers_Round3.cs` (with the pure rules in `Round3CouncilMath.cs`) and
`Tools/Tests/Round3CouncilTests.cs`.

What every question keeps to:
- Council questions, so the flavour style of the council's own texts (CLAUDE.md, the player's 20 November 2026 rule); the
  gods' words in the old voice, bubble lines one sentence each.
- Each answer plays out on the map with the play-outs that exist (`Tools/playout_audit.py`), and what the words say is
  what happens. No countdowns in the words.
- Triggers read only what the game and Rimfolk already know: the game's own moods and memories, beds and rooms, the
  stores, hediffs, seasons, a world event running, Rimfolk's deeds and its gods' and faiths' records.
- No new saved collection, no new `Sched` offset, no new window or setting. Every context is one that exists
  (`DilemmaContext`, `DilemmaContextMore`, `Round2Context`, the world's `WorldQuestionContext`).
- Harm: none of the twelve hurts anyone.
- Every age: each takes the colony's own means (a campfire or a stove, a long hut or a barracks); the words have
  `[lowtech]` forms where the older ages see it differently.
- The full mod pool: what is picked is read from the def's own properties at run time (any insect's meat, any recipe
  that makes kibble, any wild food plant, any bonded animal), so the player's active mods count with the game's.

New code, kept small: ten workers for their triggers (a friend among the visitors uses the strangers' `DilemmaWorker_Stranger`,
a verse of our own the world's `DilemmaWorker_WorldSeason`) and
five effects that are each a thin piece over what exists: two colonists trading beds (`Round3CouncilSwapBeds`, the
game's own bed ownership), an empty grave given to an animal (`Round3CouncilPetGrave`, the game's own burying), a bill at
a bench (`Round3CouncilBill`, the game's own bills, as `CookIt` is), a supper for those who need it most first
(`Round3CouncilFeastFirst`, the feast engine with its guests chosen), and a colonist's own work handed over for a
season (`Round3CouncilHandOver`, `AnswerShifts` as `HandOver` uses it).

---

## Everyday colony life

**1. The light sleeper** (`Rimfolk_DlmR3_LightSleeper`)
- **Asked when:** someone (A) has woken with the game's own "disturbed sleep" in the last day, and sleeps in a shared
  room in the bed nearest its door, while another of the room (B) has a bed at least two steps farther from it. Single
  beds both, and B is not A's partner.
- **A** the one who wakes; **B** the one at the far wall.
- **Answers:** A and B trade beds (the game's own ownership: each sleeps in the other's bed from tonight); A sleeps late
  tomorrow (the timetable's late start, `Hours`, put back after); a shared room is a shared room (moods only).
- **Ages:** a long hut by the fire in the older ages, a barracks after.
- Not a duplicate of elbow to elbow (crowding: a corner, two sittings, a new room planned), the new room (a free
  bedroom given), a cold bedroom (the warmer room traded) or music at midnight (a noisy hobby).

**2. A grave for an animal** (`Rimfolk_DlmR3_AnimalGrave`)
- **Asked when:** a colonist's bonded animal died in the last two days (the game's own "bonded animal died" memory
  about it), and its body still lies on the map, not yet buried, butchered or rotting.
- **A** the one it was bonded to; **B** the colony's eldest other grown-up, who says the graves are for people.
- **Answers:** the empty grave given to the animal (one of the colony's empty graves assigned to it; the game's own
  haulers lay it there; greyed out while there is no empty grave); an hour beside the animal, A and one other, and then
  the usual way (a quiet hour at the body, `VigilAtPlace`); an animal is an animal (moods only).
- **Ages:** the same in every age. **Mods:** any bonded animal, the game's or a mod's.
- Not a duplicate of an animal in decline (a sick old animal nursed or let go), a child's grief (a child grieving any
  death) or the animal left behind (an animal whose master died).

## Work and food

**3. What the insects left** (`Rimfolk_DlmR3_HiveMeat`)
- **Asked when:** the stores hold insect meat (meat whose source is an insect, the game's or a mod's) enough for eight
  simple meals or more, and a fire or stove of the colony's cooks simple meals from it.
- **A** the best cook; **B** the best hunter (who brought it in), else another grown-up.
- **Answers:** cook it into the meals (a cooking bill at the stove for simple meals of it and nothing else, `CookIt`; the
  eaters get the game's own mood for insect meat in a meal); kibble for the animals (a bill at the stove for kibble, of
  it and plant food, by the game's kibble recipe or a mod's; greyed out with no animal that eats kibble or no stove that
  makes it); keep it for a hungry day (nothing done).
- **Ages:** a fire pit in the older ages, a stove after; the words say so.
- Not a duplicate of the food that won't keep (any raw food about to rot, cooked), a bad batch (food poisoning) or the
  good meals (fine meals eaten or kept).

## The seasons

**4. The picking day** (`Rimfolk_DlmR3_PickingDay`), a custom of the calendar
- **Asked when:** summer, with at least six wild food plants ripe within reach of the colony (the same reading as the
  foraging days, `YearFour.Forage`). Its first answer makes it a custom on the colony's calendar, and three years running
  a tradition, as every custom does (`DilemmaWorker_Custom`).
- **A** whoever asks for it.
- **Answers:** everyone out to pick (the wild food marked for the game's harvest and a work party bringing in what is
  ripe, wild and sown); a morning of it for those who can be spared (fewer marked, a smaller party); the work comes first
  (moods only).
- **Ages:** the old village's day in the older ages; agave in the desert lands (`[land:desert]`).
- Not a duplicate of the children's gleaning (children in the harvested fields), the foraging days (a decree) or short in
  the stores (an event of a hungry colony).
- The Almanac's table of customs (`Almanac_Council.xml`, "The customs") has its row.

**5. The hungry gap** (`Rimfolk_DlmR3_HungryGap`)
- **Asked when:** winter or spring, fewer than four days of food in the stores (the advisor's own count), cooked meals in
  them, and someone at home who needs food most: a child, someone sixty or older, or someone ill.
- **A** the best cook; **B** the best at the hard outdoor work (mining, building, the fields), not a child or an elder.
- **Answers:** those who need it most eat first (a supper at the table for the children, the old and the ill, the cook
  bringing the meals, through the feast engine with only them invited); those at the hardest work first (the cook's rounds
  with a meal for each of those working in the open, `Rounds` with `serve`); everyone alike (moods only).
- **Ages:** the pots and the fire in the older ages.
- Not a duplicate of a second helping (one who takes two), the herd or the stores (animals culled in a famine) or short in
  the stores (hands sent to bring food in).

## Guests and the colony's neighbours

**6. A friend among the visitors** (`Rimfolk_DlmR3_VisitorFriend`)
- **Asked when:** visitors or traders of a people at peace with the colony are on the map, and one of them (X) and one of
  the colony (A) each think well of the other (opinion 15 and more both ways).
- **A** the friend; **B** another grown-up, who says the stores are not for every friendly visitor. Through the strangers'
  own `DilemmaWorker_Stranger` (a visitor, each thinking well of the other), so no worker of its own.
- **Answers:** A takes X food for the road (two meals from the stores carried out and put into X's pack, `CarryOut`; a
  little goodwill); an hour together before the visitors go (`Visit` to X; greyed out once X has gone); visitors come and
  go (moods only).
- **Ages:** the evening fire in the older peoples' words (`[theirLowtech]`).
- Not a duplicate of a friend at the gate (an old friend kept outside the walls), a reason to stay (a visitor who wants to
  join) or a visitor at supper (the visiting group fed).

**7. A verse of our own** (`Rimfolk_DlmR3_OurVerse`), a question the world brings home
- **Asked when:** the region's season of songs runs (`Council_WS_Songs`: a famous singer on the road, a song about the
  colony going round), through the world's `DilemmaWorker_WorldSeason`.
- **A** the one who wants the verse; **B** another, who thinks it a waste of an evening.
- **Answers:** an evening at the fire making the verse together (a bonfire with music and its own lines); A and B work it
  out at the table one evening (a visit, the two sitting down together); the singing is left to the singer (moods only).
- **Ages:** the fire in every age; the words have a `[lowtech]` form.
- Not a duplicate of the season of songs' own beats (a purse for the singer, the singer asked in), the fair day at home
  or the winter tales.

## Children and elders

**8. The forgetting** (`Rimfolk_DlmR3_Forgetting`)
- **Asked when:** a grown-up of the colony has the game's dementia or Alzheimer's and still does a skilled work of the
  colony's ({WORK}, the work they give first place to of those with a skill).
- **A** the one forgetting; **B** the one closest to A who can do that work, else the best at it.
- **Answers:** B sits down with A and goes over the colony's people one by one (a visit, the two at the table); the work
  goes to B for a season (A's work handed over and put back after, `AnswerShifts`); A keeps on as before (moods, trust).
- **Ages:** the fire in the older ages.
- Not a duplicate of putting down the tools (an elder asks to stop the heavy work), won't see the doctor (an illness
  hidden) or cold in the bones.

**9. The crying in the night** (`Rimfolk_DlmR3_CryingNight`), Biotech
- **Asked when:** a baby of the colony has kept someone who sees to the children (A, childcare on) up at night: the game's
  own crying-baby mood in the last day, and A's rest under half.
- **A** the tired one; **B** another grown-up who can see to children, the fondest of A; the baby is named ({THIRD}).
- **Answers:** B sees to the baby for a few days (A's childcare handed to B and put back after, `HandOver`); A sleeps the
  afternoon (`Rest` with sleep); babies cry (moods only).
- **Ages:** the same in every age; a `[lowtech]` form.
- Not a duplicate of who raises them (an orphan's guardian) or the midwife's apprentice.

## Crime and fairness

**10. After the tantrum** (`Rimfolk_DlmR3_AfterTantrum`)
- **Asked when:** one of the colony (A) broke things in a tantrum in the last two days (Rimfolk's deeds keep it:
  `Rimfolk_Deed_Tantrum`), is themselves again, can do building work, and the colony has damage to mend (the game's own
  repairs).
- **A** the one who broke things; **B** one who knows of it, the least fond of A.
- **Answers:** A mends the damage (a work party of one, A at the colony's repairs and cleaning); A says sorry at supper
  (words at the table, A speaking); everyone has a bad day (moods only).
- **Ages:** a `[lowtech]` form; the same answers.
- Not a duplicate of a feud, it's gone too far, a broken window (a child's mischief) or trouble at every turn (one who
  keeps doing harm, perhaps sent away).

The hungry gap (5) is the other question of fairness here: who eats first.

## Faith

**11. The scoffer's grave** (`Rimfolk_DlmR3_ScofferGrave`)
- **Asked when:** one of the colony who never held to any god (a scoffer) died in the last six days and lies in a grave
  at home; the gods and their worship are on.
- **A** one who holds a god (the priest, else the most devout), {GOD} the god A holds; **B** the one who thought best of
  the dead.
- **Answers:** A says {GOD}'s words at the grave at dusk with whoever comes (words at the grave, the rite's words and
  the old voice); B says a few plain words of B's own there (words at the grave, B speaking); nothing said, as the dead
  would have wanted (moods only). No god acts: only what the living believe and feel.
- **Ages:** a `[lowtech]` form.
- Not a duplicate of how to lay them down (a believer's way against another's), the feast after a death, the room nobody
  will sleep in or where the dead go.

**12. The table without meat** (`Rimfolk_DlmR3_NoMeatTable`), Ideology
- **Asked when:** some of the colony hold a faith whose precepts forbid meat, one of them (A) ate meat in the last two days
  (the game's own moods for it), and a stove of the colony's can cook Rimfolk's simple meal without meat.
- **A** the one who ate it; **B** the best cook other than A.
- **Answers:** a pot without meat for the faithful (a bill at the stove for meals without meat, first, enough for them for
  a while); A learns to cook it at B's side (a lesson in cooking); the colony's pot is the colony's pot (moods, trust).
- **Ages:** the cooking fire in the older ages.
- Not a duplicate of the precept broken (the council's judgement on one who broke the faith's ways), the same bowl again
  or doubts in the faith.

---

The twelve, by where they belong: everyday life 1 and 2; work and food 3 (and 5, 12); the seasons 4 and 5; guests and
the neighbours 6 and 7; children and elders 8 and 9 (and 5); crime and fairness 10 and 5; faith 11 and 12.

Considered and left: a coat before the snow (the colony's wish for clothes for the season already covers it), the
grower's winter (the same work every day covers it), music from the camp (their feast day covers it), the woodpile run
low in winter (the woodpile day covers it), a vow in the fight (a vow in the storm covers it).
