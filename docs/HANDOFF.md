# Handoff: start here

Read this first; it is the whole state. CLAUDE.md has the binding rules and the check commands. CHANGELOG.md (written
from `Defs/AlmanacArticleDefs/Almanac_WhatsNew.xml`) has every round in the player's words. The sections under
"Older history" at the bottom are kept for reference only.

## State (5 November 2026)

- 0.121.0, new wishes, goals and names (the player's request, 6 November 2026): "do another big content update to existing
  mechanics"; this part the colony's own people, what they want, strive for and are known for (the other parts are
  other agents'). New content in the existing frameworks at their existing rates; no round of its own, About.xml,
  the What's new page and CHANGELOG untouched. Pure rules in `Lives/PeopleFourMath.cs` and `GoalMath` (`PeopleFourTests`);
  DESIGN.md "What the colony's people want, strive for and are known for: the fourth round". Each piece, what starts it,
  every age's form, how often:
  - Two sparks for life goals (`Aspirations/AspirationWorkers_People4.cs`, `GoalSparks`, read in the tracker's
    `Sparked`): `hurt`, a hurt from everyday life lately (whatever the harm service gave them: a danger, a work hurt, an
    ailment; `Harm.DaysSince`), and `war`, a bad world event of the war kind running (the warlord, the clans, a war
    between neighbours; looked at once an hour). A war is a spark of fighting (`GoalMath.FightSpark`, pull 0.6 like a
    foe): the one-at-a-time rule holds it as it holds a fight. Neither is big enough to change a goal held. No new goal
    is sparked by a fight. `AspirationTracker.When` gives a spark's last tick.
  - Twelve life goals (`Defs/AspirationDefs/Rimfolk_Aspirations_People4.xml`), each with lines for what sparked it and
    in six tones: a nurse's hands (Medicine to 8, anyone; an illness pulled through, a hurt, a loss; lines for the four
    ages' means), sure on their feet again (30 days without another hurt or fall, only for someone hurt in the last
    10, the clock starting again at each: `AspirationWorker_SureFooted`, with hurts on), keep the colony mended (40
    repairs, builders), a trader's name (6 deals, the sociable), a friend of the road (5 evenings at camps' fires; an
    evening there, its stories), a good neighbor (2 of the neighbours' calls answered; a war, a good turn on the road),
    keep the colony's days (2 kept; a new colony's first night, the founding day, a loss), a peacemaker (3 quarrels
    mended; a fight of their own, a loss), lead the rites (3, Ideology; an omen, a loss), see the new colony grow (its
    own people to 8, 2 more at least, with two homes: `AspirationWorker_HomeGrows`; its first night, a birth), the
    journey to the other colony (standing on another of the colony's homes, someone they're fond of living there:
    `AspirationWorker_Visit`; a parting, being sent away), ready for what comes (Shooting to 8 for anyone who can
    fight; a war; lines for bow, crossbow, rifle). Goals on Rimfolk's own records come only while their feature is on
    (`AspirationWorker_FeatureRecord`, `counter` names it: camps, worldSituations, colonyCalendar, socialWeb, ideology).
  - Seven wishes (`Defs/WishDefs/Rimfolk_Wishes_People4.xml`, thanks in `Rimfolk_Thoughts_People4.xml`, +3 or +4 for 4
    or 5 days): cloth put by for mending (80 textiles of any kind, crafters, fall and winter), steel put by (150,
    builders and crafters), spare parts for the machines (6 components, industrial and spacer only), milk for the little
    one (20 milk or baby food, parents of a baby), shelves for the stores (2), and two that play out when promised
    (`Wishes/WishWorkers_People4.cs`): the trees by the walls felled (3 grown trees or more in the home area beside the
    colony's walls or doors, each marked for the game's cutting; never for one whose faith minds felling trees) and the
    snow cleared from the doors (deep snow on 12 cells or more within two steps of the doors, put in the game's snow
    clearing area). No triggers: they take a share of the wish roll, which comes as often as ever. The balance model's
    gates for the two (`Tools/Balance/Rates.cs`, 0.2 and 0.1).
  - Eight quirks (`Defs/QuirkDefs/Rimfolk_Quirks_People4.xml`, moods in `Rimfolk_Thoughts_People4.xml`, words over a
    head in `Defs/FlavorPoolDefs/Rimfolk_Quirks_People4Bubbles.xml` through `HabitBubbles`), with two new triggers on
    `QuirkDef`: `afterHurt` (a hurt within its days, once up) and `whileIll` (hediff defNames, while ill), and their
    causes "after getting hurt", "from an illness". Watches every step (a hurt, 0.02 a day for 4 days), taps the roof
    before digging (a hurt, only from mining practice, +1 at the face), dries their boots by every fire (trench foot,
    +1 by a fire), boils everything they drink (a serious illness pulled through), keeps an ember of the first fire (a
    new colony's first night, +1 by a fire), looks back down the road (a parting or being sent away, -1 late at night,
    60 days), sings a song from the strangers' fire (a camp's evening, +1 outdoors), indoors by dusk (sandfly fever, -2
    outside after dark). Their moments press 0.58 quirks a colonist-year on the test's generous model, against 8.4 the
    fights already bring, under the cap of two. Dreams of their own (`Defs/DreamDefs/Rimfolk_Dreams_People4.xml`, three).
  - Eight names and six deeds. Six deeds the colony talks about count a record each, each time they're recorded,
    merged or not (`PeopleFourMath.DeedRecord`, in `Deeds.Record`; records in `Defs/Misc/Rimfolk_Records_People4.xml`):
    people carried out of a fight (the Shield, from 3), night watches (Night-Watch, 4), contests won (the Champion, 4),
    masterworks (Master-Hand, 3), rounds of the sick (the Lamp, 4), kindnesses to strangers (Open-Hand, 4); students
    taught to stand alone are counted where a mentorship ends (`SocialWeb.End`; the Teacher, 2), votes lost were already
    (the Hopeful, 4, dubious). `Defs/EpithetDefs/Rimfolk_Epithets_People4.xml`; words every age has; renown's known-for
    and reputation's sides for them (`RenownMath.Epithets`, `Reputations.EpithetSides`). The one-name rules are
    untouched. Four new deeds (`Defs/DeedDefs/Rimfolk_Deeds_People4.xml`, with their log topics), met where the game
    notices them: the prisoners let out, a binge on the colony's drugs, a body dug up, running off into the wild (the
    game's breaks). At the merge, a wedding and a proposal turned down were dropped: a deed moves how the colony sees
    someone (its facets), and the colony is the family, with no mechanics on family life.
  - Rates. Life goals (`PeopleFourTests`, a model of the real pool of grown-ups' goals with the tracker's rules: twelve
    colonists over four years of sixty days, sixty runs, the same seeds and the same colonists fitting the same old goals
    before and after): 70 goals before, 80 after (the two that need a second home left out); started 0.94 a
    colonist-year before and 0.96 after; from fights 8.4% and 6.4% (a war 0.8%, a hurt 6.7% on the model's generous
    two hurts a colonist-year); never two on one fighting goal from fights or a war at once. `Tools/Balance` (120 runs a
    setup, before on the start tree, after on this one; reports kept outside the repo): 6 colonists average play, wishes
    voiced 0.47 and 0.48 a day (7.1 and 7.2 a season), letters 0.39 and 0.40; wishes made a year (well, average,
    neglect) 39.1, 27.9, 31.4 and 38.5, 28.8, 31.6; mood 69/64/39% either way; strain (strained, worn, breaking) 5.8,
    1.4, 0.3% and 5.8, 1.5, 0.3% (average); net 2.6 and 2.7; the good side's steady/in good heart/in high spirits rows the
    same. `--nature` and `--social` print the same, `--scenes` (10 colonists, 200 runs) as built 23.9, 23.4, 23.6 and
    24.0, 23.5, 23.5 staged a year (2.8 a week either way); no new answer gathers people. Names hold and pass as before
    (NameMath unchanged): the eight new ones are for deeds that come a few times a year, so a colony earns each in a year
    or two; the save holds one more hold and holder per name (`save_estimate`: 71, about 2 KB).
  - Shared code changed outside the new files: `Aspirations/Aspirations.cs` (the two sparks, `When`, the fighting tally by
    `FightSpark`), `GoalMath.cs` (`FightSpark`, the war's pull), `Quirks/Quirks.cs` and `Practice.cs` (the two triggers,
    two causes), `Immersion/HabitBubbles.cs` (eight bubbles), `Social/Gossip/Deeds.cs` (the deed's record),
    `Social/SocialWeb.cs` (students taught), `Epithets/CouncilRecords.cs` (`StudentsTaught`), `Diplomacy/RenownMath.cs`,
    `Social/Gossip/Reputations.cs`, `Tools/Balance/Rates.cs`, `Tools/Tests/FairCouncilTests.cs` (its code check reads
    `FightSpark`), `ImmersionTests.cs` (the eight bubble pools). Docs: the Almanac's lists (wishes, life goals), its names
    and quirks pages (rows at the end of their tables, a paragraph; keywords unchanged), README (lists and counts: 128
    wishes, 97 life goals, 69 quirks, 71 names, 51 deeds), DESIGN.md.
  - Needs a look in the game: trees against the walls marked and felled after a promise; the ways by the doors put in
    the snow clearing area and shovelled; a goal "after getting hurt" after a danger's wound, and the thirty days
    starting again at another; the journey to the other colony reached on arrival by caravan; the new names in the
    Records tab after a raid's rescues and a feast's contests; a mood from taps-the-roof at the rock face.
- 0.120.0, new questions, events and dangers (the player's request, 6 November 2026): "drop some new additional dilemmas, events,
  world events, camps, illnesses/ailments (can also be some artificial ones), dangers etc., for further gameplay
  variety"; this part the council's questions, Rimfolk's events and dangers on the map (illnesses, world events and camps
  are other agents'). New content in the existing frameworks at their existing rates; no round of its own, About.xml
  untouched. Pure rules in `Dilemmas/VarietyThreeMath.cs` (`VarietyThreeTests`). Each piece, what plays out, what lasts
  or hurts, how often:
  - The inspired hands (`Rimfolk_DlmE_Inspired`, `DilemmaWorker_Inspired`, `DilemmaEffect_ArtBill`): the game's
    creativity inspiration on A, an art bench, a fine stuff three times the plain one's worth. A bill at the bench for
    the biggest piece both can pay for, A alone, in the fine stuff or the plain one, first on the bench; or the work
    comes first. The piece (masterwork at least, the game's), the stuff spent; weight 1.3, 30 days apart.
  - The shakes (`Rimfolk_DlmE_Withdrawal`, `DilemmaEffect_Dose`, `DilemmaEffect_RideItOut`): the game's withdrawal (never
    luciferium or anything whose withdrawal kills) with the drug in the stores; B the doctor. One dose from the stores
    (the game's ingest job, the habit kept), the colony's strictest drug policy that never gives it (the habit broken as
    the game has it; it stays until the player sets another), or their own business. Weight 1.3, 25 days.
  - The food that won't keep (`Rimfolk_DlmE_Spoiling`, `DilemmaEffect_CookIt`): raw food in the stores rotting within a
    day and a half where it lies (5 nutrition at least), a stove with simple meals. A bill for all of it (2 to 20 meals)
    or one supper's, that food alone, first on the stove; or let it go. Weight 1.3, 20 days.
  - The stores in the rain (`Rimfolk_DlmE_StoresInRain`, `DilemmaEffect_RoofStores`, `DilemmaEffect_GiveUpOpen`): rain on
    2 of the last 5 days, a stockpile with 6 open cells and 3 worn stacks. Its open cells in the game's build-roof area
    where a wall or column can carry a roof, or taken out of the stockpile (the haulers move what lay there; the rest kept
    whole, `CheckContiguous`); or left. Weight 1.2, 30 days.
  - The old walls (`Rimfolk_DlmE_OldWalls`, `DilemmaEffect_OldWalls`): a ruin of 6 walls or more of nobody's, of stuff,
    within 60 cells. Marked to come down (deconstruct) or claimed as the colony's; or left. Weight 1, 60 days.
  - The new room (`Rimfolk_DlmE_NewRoom`, `DilemmaEffect_ClaimBed`): a bedroom with one bed nobody owns, two grown-ups in
    a shared room or with no bed who'd sleep 10 impressiveness better. The bed claimed by one (the game's ownership), or
    whoever lies down first. Weight 1.3, 25 days.
  - What to learn next (`Rimfolk_DlmE_NextStudy`, `DilemmaEffect_Study`): no study chosen, a research bench, two or more
    open. The nearest or a big one set as the game's research; or the benches wait. Weight 1.2, 20 days.
  - The hurt beast (`Rimfolk_DlmE_HurtBeast`, `DilemmaEffect_MarkBeast`): a wild animal downed within 60 cells. Marked for
    the hunters, or for taming (the handlers try once it can stand: the game won't tame a downed animal), the game's
    failed-tame risk named; or left. Weight 1.2, 20 days.
  - The wild one (`Rimfolk_Evt_WildOne`, `HappeningWorker_WildOne`): the game's wild man or woman on the land. Marked for
    taming (risk named), let be, or driven off (`DilemmaEffect_DriveOff`). A colonist or not; base chance 0.7, 30 days.
  - The fence down (`Rimfolk_Evt_FenceDown`, `DilemmaEffect_MendFence`): a storm in the last 2 days, a closed pen with two
    animals or more: a piece of its fence gone (destroyed quietly, so the game's own auto-rebuild doesn't put it back),
    splinters and a little of its stuff. The same fence's blueprint, or the gap left for grazing (the crops within
    reach). Base 0.6, 40 days.
  - The ambrosia ripe (`Rimfolk_Evt_Ambrosia`, `DilemmaEffect_PickAmbrosia`): three ripe wild bushes or more; B a
    teetotaller if any. All or a night's (one a person) marked for harvest, the game's pull named; or left. Base 0.6, 40.
  - A clutch in the grass (`Rimfolk_Evt_Clutch`, `DilemmaEffect_Clutch`): spring or summer, the fertilized eggs of a bird
    that lives wild on the land (3 to 6), set down forbidden. Raised (the hatcher's faction the colony's, hauled in), the
    kitchen (the same bird's unfertilized eggs in their place), or left to hatch wild. Base 0.6, 40.
  - Dangers on the map (`Happenings/Dangers.cs`, `Rimfolk_Events_Dangers.xml`; DESIGN.md "Dangers on the map"): Rimfolk
    events, only with `Harm.On` and no threat, a quick letter (0.12 to 0.75 days) whose words say what can happen, each
    answer's hurt a Health line under it, every hurt `Harm.Injure` (via `MapDangers.Wound`) on `Harm.Chance`, told
    Important in two wordings with a line said (`lines` tagged warn, go, work, carry, fire, out, clear, gone, fell,
    hurt); kept on the map as new `OnMapTimed` kinds (the sign until the answer, then its watch; `MapDangers.Step`). When
    the cap holds someone back, the rock still falls or the ground still gives and they get clear. No mood, no fight.
    - The creak at the dig (`Rimfolk_Evt_DigCreak`, `DilemmaEffect_DigFace`): someone mining a face under a rock roof;
      grit and rubble, the digging within 4 cells stopped and its marks kept, the miner steps back. A column of the
      cheapest stuff marked beside it and the digging put back; dug on (the next to dig there: 1 in 4, a crush of 4 to 9
      on the torso or an arm, rubble and a chunk); or the face left. Base 0.6, 45 days.
    - The leaning tree (`Rimfolk_Evt_LeaningTree`, `DilemmaEffect_LeaningTree`): rain on 3 of 6 days, a grown tree in the
      home area beside a wall or door; heaved ground at its foot. Felled (the game's cut), or watched 6 days: 8% an hour
      in a wind (1.5 and up), 0.4% in still air; it falls on the wall (60% of its hit points, never the last), logs and
      rubble, a crush of 4 to 8 for whoever stands beside it on its side. Base 0.6, 50 days.
    - Smoke in the dry grass (`Rimfolk_Evt_Smoulder`, `DilemmaEffect_Smoulder`): summer, 26 degrees and up, no rain for 5
      days, not raining; a wild flammable plant 10 to 28 cells out, outside the home area. Smoke until the answer. Stamped
      out by A or the nearest free within 6 hours (12%: a burn of 2 to 5 on a hand or foot), else it catches; left, the
      game's fire starts there in 1 to 3 hours. Base 0.6, 40 days.
    - The old shell (`Rimfolk_Evt_OldShell`, `DilemmaEffect_OldShell`): someone sowing or harvesting a growing zone; the
      game's high-explosive shell set down forbidden beside them. Carried 25 to 60 cells out (the game's haul to a cell,
      forbidden there), set off by B from 9 cells with nobody within 7 (the shell's own fuse; 15%: a cut of 2 to 5 from a
      stone), to the stores with a mortar built, or left (a fire sets it off anywhere, named). Base 0.5, 80 days.
    - The twitching scrap (`Rimfolk_Evt_TwitchingScrap`, `DilemmaEffect_Scrap`): a mechanoid's corpse lying outside the
      stores within 60 cells; sparks and machine bits. Broken up by B in half an hour (15%: a burn of 3 to 6 on a hand or
      arm; steel by its size, 10 to 60), or left 3 days (the first to handle it, 25%). Base 0.6, 40 days.
    - The ground giving (`Rimfolk_Evt_SoftGround`, `DilemmaEffect_SoftGround`): rain on 4 of 6 days, 3 to 5 natural soft
      cells by a colony door; heaved ground. A floor marked where one can go (stone flags first, else the cheapest the
      colony can build and pay for; cells where none can go stay watched), or watched 4 days: each of the first three to
      cross may put a foot through (30%: mud from then on, a bruise of 3 to 6 on the foot or one time in four a crack).
      Base 0.6, 50 days.
    - Their rate: each comes in its moment only, a few times a year at most (a mountain colony's dig, a forest colony's
      tree), and hurts only on the answer that risks it: at most about a fifth of a hurt a year each at the default
      (`VarietyThreeMath.HurtsAYear`), times the setting and the storyteller's lean, under the one-in-three-days cap.
  - Rates, from `Tools/Balance` (400 runs a setup, before on the start tree, after on this one): 6 colonists, average
    play, questions a year 13.2 and 13.1, events 6.1 and 6.1 (with missions and camps 6.3 and 6.3), world events 1.1 and
    1.0, decisions a day 0.50 and 0.49; across all 24 setups the differences scatter both ways. The events' count can't
    rise: Rimfolk's share of the storyteller's draw is capped (`ShareFactor`, counted over every def), and in the game a
    new def thins every other's weight while it can fire only in its moment. `--scenes` (10 colonists, 200 runs): well run
    53.6 and 53.3 staged a year, as built 24.3 and 24.1 (2.9 and 2.8 gatherings and outings a week); average as built
    23.7 and 23.6 (2.8 a week either way); no new answer gathers people (the dilemmas that may: 24% of 185 before, 23% of
    193 after; events 23% of 208, 22% of 216). Tests: `VarietyThreeTests` (461 checks). The play-out audit counts the 19
    new effects (0 candidates): 706 answers of 191 questions and 195 events, stories and world events play out. Docs: the
    Almanac's council page (the questions), its Rimfolk events page ("From what the colony is doing": the events and the dangers), its
    ailments page (the dangers among the hurts), README (lists, counts, the setting), DESIGN.md. The def validator knows
    {PLAIN}, {STUDY1}, {STUDY2}. Not touched: the harm review's queued proposals (2.4.1, 2.2.1, 2.1.1, 3.1.1, 1.4.1,
    3.3.1, 3.5.1).
  - Needs a look in the game: the shell set off (the wick and blast of an item shell, the setter's walk to the stand-off),
    a smoulder left to catch on a hot day, the leaning tree's fall onto a wall in a wind, a stockpile drawn in under its
    roof, the bill at an art bench for the inspired one, ambrosia and clutches on a temperate map, a wild man's taming.

  At the merge: its tests are `Tools/Tests/VarietyDangersTests.cs` (the world events and camps branch had taken the name
  VarietyThreeTests), README's count is 719 answers of 191 dilemmas and 200 events, stories and world events (from
  `playout_audit.py --counts` on the merged tree), and `docs/balance-report.md` was written again on the merged tree.
- 0.119.0, new illnesses and ailments (the player's request, 6 November 2026)
  ("drop some new additional ... illnesses/ailments (can also be some artificial ones) ... for further gameplay
  variety"). Not released: no round, About.xml untouched.
  Eight everyday ailments and three serious illnesses, each from something the hourly look finds on the map
  (`AilmentChecks.Sample`, whose counted causes are the bits of a long now; the pure rules in
  `Ailments/AilmentCauseMath.cs`), each the game's own hediff with stages and tending, the serious ones its immunity race
  with a hidden incubation (`Rimfolk_Hediffs_Ailments_Land.xml`, `Rimfolk_Hediffs_Illnesses_Land.xml`; moods, hints and
  lines in the `_Land` files of ThoughtDefs, MoodHintDefs and FlavorPoolDefs), named by the colony's age where natural
  (`names`, `AilmentTracker.NameOf`; the health tab through `Hediff_Ailment`, the message, letter, journal and the
  doctor's record). Each in a line (name by age; cause; course; prevention; ages; rate):
  - swimmer's itch (pond itch): an hour's swim (`GoSwimming`) in still water, a lake, a pond or a marsh, never a river or
    the sea; 2 to 4 days, a wash shortens it; swim in the river; every age; 4% a day, children half again (mild).
  - trench foot (marsh foot): 4 hours outdoors in water, marsh or mud below 10 °C (their cold shift moves it); 1 to 2
    weeks, less in bed, tending helps; a bridge or path over the wet, short shifts; every age; 1.5% a day, grown-ups.
  - sandfly fever (three-day fever): 2 hours outdoors in a desert or dry scrubland from 18:00 to 5:00 at 20 °C or more;
    about 3 days whatever is done; indoors after dusk; every age, arid lands; 3% a day, not again for 30 days (mild).
  - jungle rot: a cut, scratch, stab or bite untended for 6 hours in a swamp, jungle or rainforest at 25 °C or more;
    about two weeks, a few days tended, industrial best; wounds tended the same day; every age, wet hot lands; 8% a day
    with such a wound (looked at in the one hour it turns six hours old, `RotWindow`), grown-ups.
  - rabbit fever (hunter's fever): 2 hours butchering a small wild animal (body 0.5 or less: hares, squirrels, rats);
    about a week, a few days tended, industrial best; a skilled butcher (Cooking); every age; 1.2% a day, grown-ups.
  - metal fume fever (founder's ague): 4 hours smithing or smelting in a closed room of 60 cells or fewer; 1 to 2 days,
    less in bed; a big or open smithy; wherever there's a forge (medieval on); 2.5% a day, grown-ups.
  - stump sores: 8 hours walking or at heavy or bench work on a limb of the old kinds (weaker than natural, made before
    the spacer age: a peg leg, a wooden foot, a simple prosthetic); 3 to 6 days, much less in bed; a lighter day after a
    long one; tribal to industrial (a bionic limb never rubs); 3% a day (mild).
  - mechanite fever (machine fever): a part or implant of the spacer age put in while one of the colony, or a first
    dose of luciferium, the roll waiting until they're up, within 3 days (`FreshMechanites`; once a part, its load ID with
    the bites); 1 to 3 days, less in bed; nothing prevents it; any colony that uses spacer parts; 1 in 4, grown-ups.
  - anthrax (black bane), serious: an hour butchering an animal that had begun to rot or was sick when it died; hidden
    1 to 6 days, kills in about 7 untreated, 4 to 5 days with industrial medicine, 8 to 9 with herbal; burn the
    carcass, keep rotten bodies off the bill; every age; 4% such a day.
  - valley fever (desert fever), serious: 4 hours building, mining in the open, sowing or clearing in an arid land with
    no rain falling; hidden 7 to 20 days, kills in about 12 untreated, about 10 days with industrial medicine, 16 with
    herbal; dig after rain, turns; every age, arid lands; 0.3% a day, the old half again.
  - bird fever, serious: an hour cleaning a closed room where birds (the game's Bird body) are kept; hidden 5 to 14
    days, kills in 7 to 8 untreated, 4 to 5 days with industrial medicine, about 9 with herbal; birds in an open pen;
    every age with birds; 0.25% a day, the old half again.
  Each is a Rimfolk-made hurt (`HediffCompProperties_Ailment.capped`, `hurt`): `CanGet` asks `Harm.May`, `Give` goes
  through `Harm.Ail` (one in three days, none in a sickbed or while downed, none for babies, children only the three
  mild ones; "Hurts from everyday life" at nothing stops them), told at `Importance.Important` with the doctor's note,
  and said over their head as it shows (`AilmentTracker.SayOnset`, from `Council_Pool_SayAilment`); the serious ones by
  their letter. The serious ones lean with the storyteller's disease setting as the catching clock does
  (`AilmentCauseMath.DiseaseLean`, none with diseases off), and keep the serious illnesses' room (one kind at a time,
  30 days apart). Balance (`Tools/Balance`, `Rates.cs` models the temperate ones; `docs/balance-report.md` regenerated):
  the new everyday ones fill 0.15, 0.20 and 0.23 days of a colonist's year and take that share from the illnesses that
  go round, the catching clock 50 to 60 days. Time ill on 480 runs against the code before, 6 colonists: well run 9.5%
  to 9.1%, average 14.0% to 14.2%, neglected 31.5% to 31.7% (12 colonists 9.2/14.1/30.9% to 9.0/13.8/31.3%); on the
  report's 120: 9.3/14.6/31.6% to 9.2/14.5/32.0%. Serious illnesses a colony-year (6) 0.24/0.46/1.80 to 0.26/0.53/2.00.
  Rimfolk's mood within two tenths. Tests: `AilmentLandTests` (each cause, the cap's classes, the lean, the names in
  every age, the rates against the existing ones', the words), `AilmentTests` (the new files; parents across files).
  Docs: DESIGN.md ("Ailments of the land, the work and the age", "Balance targets"), the Almanac's ailments page (rows
  in its four tables, the medicine, the cap and the setting; keywords unchanged), README. Left for the player's word:
  the older ailments stay outside the cap (under it, every cold would hold back a drill's blow or a slip for three
  days); the harm review's sickness from a journey (3.1.1) is untouched. To check in game: a swim in a pond in summer
  (dev: a colonist swimming), a forge in a small room, a peg leg on a long hauling day, a bionic arm installed (the
  fever once up), a rotten carcass allowed on the butcher's bill, the names in a tribal start.
- 0.118.0, new world events and camps (the player's request, 6 November 2026): "drop some new additional dilemmas, events, world
  events, camps, illnesses/ailments (can also be some artificial ones), dangers etc., for further gameplay variety".
  This part: world events across the region and camp kinds, in the existing frameworks and pools at their rates; released as
  0.118.0. The player's examples that were there already (a bandit lord's
  tolls, a great fair, a migration of beasts, a holy year, a cold winter) were left; the cold year is a summer that
  never came. Each, what it is, how it reaches the colony, how it ends, how often:
  - A war between neighbors (`Rimfolk_WS_NeighborsWar`, data): two neighbors already at war in the game, both at peace
    with the colony (never with Factional War); caravans and visitors 0.8. Their quartermaster's cart at the gate for
    food (half again or given), the other's armourer for steel (twice, `WorldEffect_Sell` to the visitor), a rider
    asking leave for a column (an armed band by the road, `holdBeat` then a message), two of the other side's fighters
    at the gate for medicine and a night by the wall. Ends by whom the colony supplied: that side wins 3 in 4 (goodwill,
    its towns +6 at once and the loser's -6, `WorldEffect_Fortune`), both supplied leaves both cool, else it wears out.
    Weight 0.6, 200 days apart, 14 to 22 days, kind war (so never with the warlord), prosperity -8 both sides.
  - A year without a summer (`Rimfolk_WS_ColdYear`): spring and summer only, kind weather (never with a hard winter);
    the game's cold snap x3, heat wave x0.2, growing and warm spells rarer. The ripe crops marked to bring in early
    (`WorldEffect_Harvest`), a neighbor's flock come down from the snow grazing the far meadow a night (as neighbors or
    for a fee), food sold or given against the winter. Helped, goodwill and the town +4. Weight 0.5, 300 days apart, 15
    to 22 days, prosperity -8.
  - A great work (`Rimfolk_WS_GreatWork`, kind build): a neighbor raises a longhouse, a hall, a dam, a signal tower
    (named by their age); travelers x1.4 and labourers passing on the road. Timber at half again or given, the colony's
    best builder teaching their master builder at the gate one evening (`DilemmaEffect_Lesson` A to X, a night by the
    wall), medicine after a fall. Helped (2), it stands with the colony's name on a stone (+6); else it stands 6 in 10
    (+3) or is left half built (-6). Weight 0.6, 240 days apart, 16 to 24 days, prosperity +4.
  - A ban on drink (`Rimfolk_WS_DryLaw`, kind law): a neighbor's town goes dry (the elders, a council, the Empire's
    edict); visitors x1.2. Two young people and a smuggler at the gate buy the colony's beer at three and two times its
    worth (the wardens find out 1 in 4 and 1 in 3: -6, -10), the smuggler reported, a promise to keep their law or not.
    It falls if the beer got in (+4) or holds if the colony kept it (goodwill, -2). Weight 0.6, 240 days apart, 16 to 24
    days, prosperity -4.
  - A great caravan (`Rimfolk_WS_GreatCaravan`, kind trade, so never with a trading season): bound for a neighbor's
    town; caravans x1.3. Its lead train on the far meadow a night (a `caravan` visit, then a rider brings a small gift,
    or for a fee), the hill road told to its rider at the gate (`DilemmaEffect_Visit`). Warned, it arrives safe (+6);
    else robbed half the time (-6) or arrives (+4). Weight 0.6, 200 days apart, 10 to 16 days.
  - Households on the move (`Rimfolk_WS_Trek`, kind trek): a neighbor's far villages walk to their kin's town; travelers
    x1.6, two `trek` visits along the road. Water for their herds (a herders' night at the colony's water), two nights
    by the wall and a meal for an old man, two young ones who would stay (`WorldEffect_Join`, the two at the gate; -3).
    Helped (2) they come through (+5); else 7 in 10 they arrive (+2), else worn out (-4). Weight 0.6, 220 days apart, 12
    to 18 days.
  - Stonecutters (`Rimfolk_Camp_Stonecutters`, tribal to industrial): a new land need `stone` (12 rock chunks clear of
    the colony at least, some together: `CampLand.StoneTargets`); they pitch by the scatter and dress chunks into the
    game's own blocks (`butcherProducts`), 1 a day and 1 more for every two hands (`work cut`, `Rimfolk_CampCut`),
    carried to their stash. Asks: a load of their stone at its worth (paid at the gate, `DilemmaEffect_CampStoneSale`,
    the catch sale's way), their craft at the bench (Crafting); their request to work the rock nearest the colony
    (`clearNear`, outside the home area). Their good turn a quarter of their pile. Weight 0.5 among the kinds that fit,
    oftener in a great work, a rebuilding or a fire.
  - Shearers (`Rimfolk_Camp_Shearers`): land need `fleece` (4 of the colony's grown beasts with their wool half grown);
    once their camp stands they offer to shear the flock for one fleece in ten (the harvest hands' way: turned down,
    they go on); taken on, they shear in the colony's pens by day with the game's own gathering
    (`Rimfolk_CampShearFlock`), the wool left for its haulers, a tenth in their packs; with nothing left they finish
    within 3 hours. Weight 0.6, oftener in a cold year or the drove season.
  - Beast-catchers (`Rimfolk_Camp_Catchers`, tribal to industrial): land need `wildHerd` (3 wild beasts of one kind that
    could be tamed and live on the land, clear of the colony, never a predator or one the colony marked); they pitch
    near it and rope one or two in their stay, one a day at most (`Rimfolk_CampCatch`, the beast held as for milking):
    theirs, kept by their picket, led away when they go (`Camp.caught`, saved). Asks: one of them at four fifths of its
    worth (paid at once, handed over first by `HandOverBeast`), their way with beasts (Animals); their request feed.
    Weight 0.5, oftener with the great herds or the drove season.
  - A traveling teacher (`Rimfolk_Camp_Teacher`): land need `children` (Biotech); a story-keeper, a hedge-school master,
    a schoolteacher or a tutor by their people's age. Asks: a lesson at the camp for up to three children (30 silver,
    Intellectual, `DilemmaEffect_CampChildrenLesson`), a colonist sitting in; their request a supper. Weight 0.5.
  - Wounded fighters (`Rimfolk_Camp_Wounded`, any land): two or three come hurt the first time they come
    (`CampKindDef.wounded`: a cut bandaged on the road and a fresh bruise on another limb, the game's own injuries,
    never the one who speaks for them or a child). Asks: the colony's doctor (`DilemmaEffect_CampTendWounded`), a hot
    supper; their request medicine. Weight 0.15, x5 while a war runs (any kind war).
  - Every camp kind has words in its people's ages, bubbles of one sentence, farewells told as they set off, talk pieces
    with its tag (`them:camp:Stonecutters`, `Shearers`, `Catchers`, `Teacher`, `Wounded` in `FactsMath.RegionTags`; 11
    pieces in `Rimfolk_OutsiderTalks_CampFolk3.xml`), the stay's shape as any camp (the ask at a day and a half, trouble
    midway on stays of three days, the parting at dusk), and leaves no gift that marks the calendar. Also: world events'
    lines can ask for `theirEmpire`/`enemyEmpire` (the Empire keeps its titles and honor in the war, the great work and
    the ban); visitor messages `CC_Visit_Come_caravan`, `_trek`.
  - Rates, from `Tools/Balance` (120 runs a setup, before and after, reports kept outside the repo): world events a year
    1.0, 1.1, 1.0 (6 colonists well run, average, neglectful) and 1.1, 1.1, 1.0 after; 1.1, 1.0, 1.1 with 12 either way;
    questions a day 0.50 and 0.50 (6, average); letters 0.41 and 0.39 a day. World events come on their own clock (one
    every 30 to 60 days, the model's pool of 64 defs without other mods', now 70, picked by weight) and none of the six
    brings another world event, a camp or a fight. Camps come by their own clock too (the daily try, about one in 35
    days), unchanged: only the kind is picked among those that fit, and no new world event raises the camp event's
    weight.
  - Files: `Defs/WorldSituationDefs/Rimfolk_WS_Variety3.xml`, `Rimfolk_WS_Variety3_Roads.xml`,
    `Defs/CampDefs/Rimfolk_CampKinds_Variety3.xml`, `Defs/OutsiderTalkDefs/Rimfolk_OutsiderTalks_CampFolk3.xml`,
    `Languages/English/Keyed/Rimfolk_CampsLand3.xml`, `Camps/CampKindsLand3.cs` (new), `CampKindsLand.cs` (the needs,
    sites, work, asks; `CampLand` now partial; the clear-near ask for `cut` too), `CampLandMath.cs`, `CampKindDef.cs`
    (`wounded`), `CampLook.cs` (a caught beast's place and sale), `CampAsks.cs` (a caught beast paid at once),
    `CampCarry.cs`, `World/SituationEffects.cs` (`WorldEffect_Fortune`), `World/Region/RegionFortunes.cs` (`Move`),
    `World/WorldSituationWorker.cs`, `Text/Facts/FactsMath.cs`. Tests: `VarietyThreeTests` (187 checks: the camps'
    arithmetic, both ways and an end by what happened in each world event, no added world event or camp, the land needs
    and work in code, the talk by tag). The play-out audit 684 to 697 answers, 0 candidates; the save estimate models
    `caught`. Docs: the Almanac's world events and camps pages (no keyword changed), README (lists, and the counts:
    answers on the map 682 of 190, camp asks 80 and requests 29, fortunes moved by 40 world events), DESIGN (fortunes).
  - Found and left: README's "88 beats of 41 world events" with visitors and "77 errands" are older than the code (114
    of 56 and 138 by a count at the start of this work), not touched.
  - Needs a look in the game: the shearers at the colony's pens (fence gates), a roped beast at the catchers' picket and
    its sale, the stonecutters' blocks hauled to their stash and sold at the gate, the wounded's dressings and the
    doctor's visit, the teacher's lesson with children, the war's column on the road and the two fighters by the wall,
    the caravan's lead train on the far meadow.
- 0.117.0, stage 3 release 1: a fresh start (6 November 2026, the player's decisions 1A, 2A, 3A, 4A and 5B of the day;
  `docs/design/stage3-many-colonies.md`, release 1, now marked built with what differs; DESIGN.md "A fresh start" under
  Many homes). Released as 0.117.0. Built in `Source/ColonyCouncil/Colonies/`:
  (1) The place: "Found a new colony" on a colony's card (a home on the ground, one founding at a time) opens the game's
  world targeter with Rimfolk's facts of the tile under the cursor (`FoundingPlace.Facts`: whose land with Faction
  Territories, the towns within 12 tiles and their stance, how remote by the region's rule, days by caravan from each
  home, the land, the game's own tile check and the proximity goodwill), then the founding window (`Dialog_Founding`).
  (2) Who goes (2A): the home's people, grown-ups first, children following their parents unless moved; each grown-up's
  wish from their own ties (`FoundingMath.Pull`/`WishOf`/`Reasons`: their own ±3, at odds +2 by the council's counted
  fights, friends ±1 and rivals the other way, a council seat, an own room and old age -1, youth +1; the line 1.5), shown
  with why; nobody refuses; sent or kept against it, `Rimfolk_Uprooted` (-4, 15 days) and, with the old colony kept, one
  question between days 3 and 15 to move to the colony they wanted (`FoundingAsk`, `ChoiceLetter_Founding`: a journey of
  one, or asked to stay for 2 trust); close bonds split, `Rimfolk_Parted` (-3, 15 days, the three closest). (3) How they
  go: the journey planner's own plan (`Foundings.PlanJourney`, `JourneyPlan.founding`: one way, food for the road and
  fifteen days, the founding kit by age from the stores, fitted to 90% of the party's room, fewer food days first), the
  planner opening on it, a parting's farewell (`Rimfolk_Pool_FoundingSays`); or the colony's own pods or shuttle, or the
  gravship, the game's way. A caravan of theirs stopped within two tiles is asked "Settle here?" (the game's own
  `SettleInEmptyTileUtility.Settle` behind its proximity warning); any home made since the start at the place (anywhere
  for the gravship) is the new colony. Nobody going: the newcomers alone, `SettleUtility.AddNewHome` and
  `GetOrGenerateMap` in the game's long events. (4) Newcomers (3A, 4A): `Dialog_Newcomers`, the game's
  `Dialog_ChooseNewWanderers` rebuilt with its own `StartingPawnUtility` calls (1 to 6, the character card and its
  randomize, Biotech's options, possessions, the skills summary), the start data cleared however it closes; at once with
  nobody going, else asked for once the colony stands (let go after 10 days unanswered); they walk in from the edge with
  their possessions. Free with the old colony abandoned in the same act; kept, once a year (`Homes.newcomersPaidAt`), the
  passage about their kit's worth, at least 150 each, up to half in food (`FoundingMath.Passage`/`Pay`), named at the
  window's foot, Confirm held back when the stores can't pay. (5) The colony stands (`Foundings.Founded`): `LedgerColony`
  records for it and its mother (`World/Ledger/LedgerColonies.cs`, written only once there are two, 24 at most), the
  milestone `colony:founded`, a letter (the party by its age form: `AgeFeature.Party`), the seat if ticked, the moods, the
  parting day on the old colony's calendar (`CalendarKind.Parting`, a remembrance, `Calendar/CalendarParting.cs`), and the
  first night: a bonfire play-out at 17 to 21h (`Rimfolk_Pool_FirstNight`, the seat holder or eldest organizing,
  `Rimfolk_FirstNight` +4 for 4 days held until it is over). (6) The old colony kept, or abandoned once the new one stands
  and nobody of the party is still on its map (a straggler holds it, told once): the game's `MapParent.Abandon` at the next
  frame and `GameEnder.CheckOrUpdateGameOver`, its council carried to the new home (`FoundingMath.CarryCouncil`), its
  record ended with the game's ruin, `colony:abandoned`, and its founding day kept on the new calendar. A home on the
  record lost any other way ends there (`Foundings.HomesLost`). (7) The game's colony limit (5B): `Prefs.MaxNumberOfPlayerSettlements`
  raised by one as the founding starts when the new colony needs it, never past five (`LimitFor`), said in the window and
  the first letter with where to lower it; set back by one after an abandon or a call-off while still Rimfolk's value
  (`LimitBack`). (8) The seat (1A, `ColonySeat.cs`): "Make this the seat" on a colony's card, ground homes of several,
  once a season (`Homes.seatMovedAt`); what reads `Homes.Seat` moves with it (the world's attention, the seat's council
  and its speaker's letters, world days and nemeses from then on, the seat-anchored notables and world events, the region
  map's default); the old seat's people -3 trust; `colony:seat`; its word by age (`AgeFeature.Seat`). (9) The Ours lens
  (`ColonyCards.cs`): every home however far, its row its grown-ups and children, speaker, mood and strain with its card's
  lines on hover (founded when and from where, the seat, wealth, its next day by name and date, a threat now); a founding
  on its way; the colonies of the past (group 3) with cards from their record; the card's acts go there, make this the
  seat, found a new colony, plan the party's journey, choose the newcomers, call it off. A colony chip in every view's
  header with more than one colony (`ColonyView.Chip` in `UiKit.ViewHeader`), the council pages' own line gone. Settings:
  "Many colonies" (Fresh starts, Newcomers under it). New: `AgeFeature.Seat`, `Party`, `Call` (twenty features now),
  `AnswerMath` source "founding", `Rimfolk_FoundingLetter`, three thoughts with mood hints, the Almanac page "a fresh start"
  (`Council_Alm_C_FreshStart`). Saves of one colony are unchanged (every new list is written only when it holds
  something; new values sit at their defaults); nothing per tick (the council's hour, after the homes, and only while a
  founding or a question waits). Tests: `FoundingTests` (wishes and reasons, against a wish, asking to move, close
  bonds, who may go and the party's checks, the means, the place's reach, the price and its payment, once a year, the
  seat once a season, the limit raised by one and set back over every setting, the kit by age, the records' fold, the
  council carried, the game's calls and the seams), `WorldMechanicsTests` (twenty features). To try in game (dev mode
  helps: a second home needs the limit, which the window raises): a fresh start by caravan with the old colony kept and
  the seat ticked (the planner opens on the plan, the farewell, "Settle here?" at the place, the letter, the seat moving,
  uprooted and parted moods, the first night's fire, the parting day on the old calendar, an event of no home then
  coming to the new seat); the same abandoned (the old colony waits for anyone of the party left, then the game's
  abandon and ruin, the council carried, the limit set back in Options); newcomers only (the window, closed with the X,
  then confirmed: they walk in on a fresh map) and with a party (the letter once it stands, the passage taken from the
  stores, once a year); pods and a gravship; the Ours lens and its acts, the chip in each window's header, a colony of
  the past's card. At the merge: a fire laid for a gathering also takes unforbidden wood lying within 20 cells of
  the colony's middle (`PlayFire.Wood`), so a party's carried wood lays the first night's fire on a map with no stockpile
  or home area yet; a gathering's spoken lines get [fire] only with a lit fire at its spot and [speaker] for whoever
  leads (`LordJob_PlayOut.Tags`, `PlaySpots.FireNear`), the first night's three fire lines are tagged [fire], and its
  mood, hint and Almanac no longer promise a fire (an industrial party brings steel, not wood). Unconfirmed: the
  planner's own recompute keeping the founding's days, and the old colony's map removal from a long event during play. Left for later releases: release 2
  the hand-over (the founding window's third choice and a card act, marked `HandOver`; the meeting's vote is its), release
  3 a scenario's start parts for the newcomers (`Dialog_Newcomers.Request`, marked), release 4 the notables and world
  events near every home, the pages following the colony in view, merging councils and This week listing a founding,
  release 5 the town's life; the content pass for a medieval group's cart, newcomers by pods or shuttle, vehicles and the
  Empire's shuttle permit as means.
- 0.116.2, names that pass, and fights that spark goals (the player's feedback, 6 November 2026, on 0.110.0 and 0.112.0: "titles
  should be able to change if someone surpasses the current owner. also some lifegoals should be sparked by fights, no?
  not 0."). Not released: no round, About.xml untouched. Both rules overshot; the aim was calmer, not frozen. (1) Names
  (`Epithets/NameMath.cs`): a name holds 20 days (`HoldDays`, was 30), then passes to the best other who is a fifth
  ahead on the count (`ClearLead` 1.2, was 1.25; exactly a fifth counts, `Ahead` allows the float's whisker) every day
  of a week (`ChallengeDays` 7, was 15) and over that week did more of the work than the holder (`Outdid`; it was half
  again as much, or more when twice ahead: `OutdoShare` and `FarAhead` are gone); a holder still at the work as much
  keeps it and the week starts again; the one who lost it can't take it back for 30 days (`ReturnDays`, was 60). A
  condition broken still loses a name at once, the chronicle says a change once, the journal a loss once, and the
  "known by one name" rules are untouched. Measured (`OneNameTests`, 0.110.0's model of the save's two doctors, Harper
  given the Healer on day 46 at 40 wounds tended to Alo's 30): taking turns a season each, never in 80 days (the old 15%
  three times); turns of 5 to 30 days, either first, five starting counts, over a year: 0.28 moves a year and never more
  than one in any 60 days (turns of 45 days can pass it twice in 60, each a real lead; so can the work going to one or
  the other in random runs of a week or two, in about one year in twelve); Alo taking over the sickbeds (3 a day to
  Harper's 0.3) has it 7 days after Harper's 20 (day 73), both at it with Alo doing twice as much 10 days after; a
  holder who stops loses it once in five years. (2) Life goals (`Aspirations/GoalMath.cs`): a fight sparks those who
  fought hardest in it (`Hardest`: the three who brought down the most, `FightTop`, the first noted of equals; any of
  its fighters downed since it began, from the goals' own "downed" event, `AspirationTracker.HappenedSince`; anyone of
  ours at that home who carried someone out, `FightSides.Carried`; read in `SocialWeb.HardestIn` before the count is
  reset and the fight's sides cleared); a fight pulls 0.7 (`SparkPull`, was 0.4); a goal others at home hold still
  weighs half; and a fight never sparks a goal someone else at home took up after a fight and is still at
  (`FightMaySpark`, from the held goals' `spark`), so one fighting goal is never chosen by several at once. Changes of
  heart are as they were (a brush with death or a loss, after 20 days, under a third done, once). Measured
  (`FairCouncilTests`, the model rebuilt: twelve colonists over 88 days fighting as often as the save, whose tales hold
  21 major threats in 128 days, so a fight every six days with nine fighting, one downed and another carrying them out;
  goals reached in 15 to 90 days and 20 days before the next; the rest of life's sparks set so the rules before 0.112.0
  give the save's own split, half from fights and a third from the rest of life; a colony under way, fifty runs): goals
  from fights 53% before 0.112.0 (the save's own 23 of 47), 3% on 0.112.0's rule, 19% now (a fresh colony's first 88
  days 11%: its first goals come before its first fight); at most one holding a goal from a fight at once (six before),
  never two taking up one goal after the same fight; changes of heart 0.3 a run (16.5 before), never two for one
  colonist in 25 days. Without the never-after-a-fight rule the share is about the same (20%), but up to three held one
  fighting goal from fights at once and two took up the same one after one fight. Old saves: names held stay with
  their holders, held from their history line or first look under the new timings (the save's Alo the Healer since day
  113: held to day 133, so the earliest the Healer can move is day 140, and only to one a fifth ahead of Alo that whole
  week and doing more at the sickbeds in it); goals held stay, a goal taken up after a battle counting for the new
  rule. Tests: `OneNameTests` (the timings, the week, outdoing, the month's bar, the doctors in turns, the clear
  surpasser), `FairCouncilTests` (the pull, `Hardest`, `FightMaySpark`, the rebuilt model on three rule sets, the code
  paths). Docs: DESIGN.md ("One name", "Life goals that last", the links table), README (names, life goals), the
  Almanac's names and life goals pages (their numbers kept: "15 wounds tended against the holder's 12", 30 days for a
  lost name), the names setting's tooltip. To check in game: the next name moved (not
  before its 20 days and a week; the card's line, the chronicle's one line); a raid's end with the three who brought
  down the most, one downed and one who carried someone out (a goal chosen "after a battle" within a few days by one of
  them with no goal), and two of them free after the same fight never on the same fighting goal.
- 6 November 2026, the player's stage 3 decisions: 1A, 2A, 3A, 4A, 5B, 6B, 7A, 8A, 9A, 10A, 11B, 12A, 13A, 14A, 15B
  (recorded at the top of `docs/design/stage3-many-colonies.md` with how the four not as recommended are built).
  Release 1 (a fresh start) built (the entry above); release 2, the hand-over, next; harm parts 2 and 3 and schemes wait.
- 0.116.1, the review's phases, checked over together (6 November 2026, the review recommended in the entry below: how the
  parallel phases 0.106.1 to 0.116.0 meet, read against each other, the player's save Atia at day 128 taken through each
  release's load path in order, and `Tools/perf_audit.py` run on the tree at 0.106.1 and now). Not released: no round,
  About.xml untouched. Fixed: (1) a feast with guests counted up to four times in the colony's week: once for each
  people's group as it came to the gate (`LordJob_FeastGuests`, seen as a gathering lord) and again for its feast's
  play-out, so after it the colony's own evenings waited a week; now counted once, as the guests come
  (`Feasts.Arrive`, `Stage.Held`), its feast not again (`Stage.Began(..., counted)`, `p.guestsFrom ==
  PlayGuests.Feast`), the guests' groups not a gathering of their own, and the guests' day still kept clear of the
  colony's own (counting only at the feast would have let a family supper or a group's evening take the evening
  first). (2) The teller counted
  every chronicle line "beyond the homes" in its day ("Teller: what was told today"): the history lets go of who a line
  is about as it writes it (`History.FromChronicle`), and `Tell.Chronicle` read them after; now read before. (3) The
  setting "Petitions" ("Colonists who want the same thing back one wish instead of each asking") off still joined shared
  wants into one petition (0.114.0's `ShareWith` came after the setting and never read it); now off, each asks on
  their own (`Wishes/Petitions.cs`). (4) A custom kept, a day on the calendar since the festive year, never went on the
  record of days kept (`Council_DaysKept`), which the keeper of days' seat work ("kept 3 days"), the Daykeeper's name
  and the promises judged by days kept read ("the colony's traditions kept properly": on the save, its traditions are
  customs, the harvest dance and the midwinter supper); now the keeper of days at home, else the one the question was
  about, else a grown colonist of the colony's own, has it, as a calendar day's keeper does (`DilemmaTracker.Tradition`,
  with the calendar on). Checked and sound:
  every path to the chronicle goes through `Tell.Chronicle` into the history once (answers through `Answers.Record`,
  the election's promises and verdicts into the holder's history only), no letter or message of these releases goes
  around the teller (the petitions' Promise and Refuse, the dev menu and the seat appointed are answers to a click); the
  good side's kind word is no gathering, its inspiration the game's own letter; the hustings speak at the count with the
  verdicts judged at the announcement, before the new promise goes in; the heart's two sides are never both above
  nothing on any path (`Spend` before every build, `Fill` only at nothing), the small moods' pile leaves the band's mood
  out (`Council_Heart_`), a group's evening eases within the gathering cap; every new setting is read, on its page, the
  presets reach it; the save's load: the chronicle list already in the history, names held from their history lines,
  the week counted from the load, decrees carried on once, the groups told once, the good side at nothing, open wishes
  alone, letters on the road keeping their days, its seven customs carried onto the calendar quietly and once
  (`customsMoved`), nothing due within its grace days at day 128, no year letter (the landing's first three days are
  past); new collections all null-safe on load. Performance: new since 0.106.1 only the pace's look every 250 ticks
  (`Stage.Tick`, small) and the answers' watch; the memory hook and the heart's hour a little dearer, nothing per frame.
  Unconfirmed, left for the player's word: (a) the save's customs carried onto the calendar land two to a season, the
  midsummer fire (about day 21) and the swim (28) in summer, the harvest dance (35, a tradition) and the high watch (43)
  in fall, and each comes on its day (`AskCustom` forces its question), against "one season, one custom"; holding the
  second back by the pace's rule would leave it on the calendar never asked and never a tradition (under 0.111.0's rule
  the harvest dance already kept the high watch from every fall), so it was left; new play can't put two in a season.
  (b) A life goal's cup goes to the next festive gathering, a family's supper too (`Stage.Festive` takes every feast),
  bringing the honoured and the one raising it to that family's table though the supper raises no cup of its own.
  (c) The year's letter is a letter worth knowing, not one hard to miss: past the colony's four letters that day it
  would come as one line of the evening's news; it is written in the first hour of the landing's day, so unlikely.
  (d) An older save loaded exactly six days after a custom's date says "came and went this year" at the first day's
  look (true, but on load). Tests: `StageTests` (the feast with guests counted once), `TellTests` (the chronicle's
  tally), `LettersPetitionsTests` (the setting), `FestiveYearTests` (a custom on the record of days kept). Docs:
  DESIGN.md (the pace's "Counted", shared wants' joining, what reads a custom). To check in game: a feast with guests of
  two peoples (Dev: "Pace: the week's gatherings" shows one), a family supper waiting after it as before; the
  Petitions setting off with two fighters wanting armor (two wishes); a custom kept, then the keeper of days' card.
- 6 November 2026, the review's six phases are done (0.106.1 to 0.116.0, with the heart's good side 0.113.0/0.113.1
  and the text and event variety 0.108.0 to 0.109.2). Built in parallel and merged one by one, none of it yet seen in
  the game. Next, in the player's order (decision 17): harm parts 2 and 3 (2.4.1, 2.2.1, 2.1.1, 3.1.1; 1.4.1, 3.3.1,
  3.5.1), schemes (theirs only), then stage 3 (its 15 revised decisions go to the player first:
  `docs/design/stage3-many-colonies.md`). Recommended before them: the player's play test of the reworked whole, and a
  review of how the parallel phases meet (the pace, the teller, the one record, one heart and its good side, one name,
  the fairer council, the festive year, letters and petitions, the five settings pages).
- 0.116.0, phase 6 of the review, part 2: one way in (the player's decisions 15, 16 and 14 of 6 November 2026: "settings on five
  pages (how much happens, how hard, what's on, how you're told, your other mods), presets covering every part", "the
  on-screen council panel reads the inbox", "fealty and vassals: their pages and settings fold into pacts; the mechanics
  stay"). Not released: no round in Almanac_WhatsNew.xml, About.xml untouched. (1) Settings on five pages
  (`UI/SettingsCatalog.cs`, `SettingsPage`: HowMuch, HowHard, WhatsOn, HowTold, OtherMods; DESIGN.md "The settings:
  five pages"). 264 rows: How much happens 17 (the colony's pace; the council's asks; people's lives; visitors, events
  and the region: every rate that was spread over six pages), How hard 26 (stakes; moods and pressures; feelings and
  strain with the good side; ailments and illnesses; realistic ways to die; hurts, a scenario's fights, events that
  end in a fight), What's on 133 (the quick setup; the council; people's lives; bonds; the colony's story; strangers;
  Rimfolk's events; world events; the region; bonds with other peoples; neighbours and letters; then the tools:
  journeys, weather, personal touches, build menu, stores, defense, planner, automations, planning ahead; the hooks,
  the era, checks and reset; the Advanced page is gone into its foot), How you're told 73 (messages, on the map and in
  the game's tabs, alerts, buttons and windows, names and cards, panels, hover card, colonist bar), Your other mods 15
  (Hospitality, RimMarket, Alpha Skills, Alpha Genes, RimWorld of Magic, Faction Territories, Move the Patient, faiths
  from other mods). Every row keeps its field and so its saved key and value: `Tools/Tests/SettingsKeys.txt` is the
  settings as they stood at 4cc8cb8 (field, key, type, default) and `SettingsTests` checks each is still saved so; a
  later setting needs no line, a line is never removed. A row's needs still grey it; it hangs indented only under a
  parent on its own page (`SettingEntry.Indented`, worked out once); a label that goes on from its parent's reads with
  the parent's name where it stands alone ("Rimfolk events › including ones that can end in a fight",
  `SettingsWindow.Standalone`, "and" dropped); the wishes' own switches hang under Wishes. Your other mods shows only
  the mods loaded (`SettingEntry.mods`, `SettingsAccess.ModsLoaded` with `People.ModActive`), its foot naming the others;
  the search still finds those rows, greyed "Only with RimMarket loaded." Page keys and headings in
  `Rimfolk_Settings_Pages.xml`; lines elsewhere that named a page (the plan off, the "more" button, reserved slots, the
  automations, a decree needing the heart, the Almanac's text size, planning ahead) point to new keys there, the old
  ones left in place unused (textcheck keeps shipped lines). (2) Presets covering every part. Light touch, story-rich and
  demanding between them set every rate on How much happens and every knob on How hard (tested), and say how you're
  told. Light touch adds: one gathering a week, half the outsiders' chatter, travellers and raiders' camps, wishes and
  ambitions at 125% of the time, the heart's good side on, a scenario's fights at 75%, no strain from diplomacy against
  someone's nature, 8 messages and 3 letters a day. Story-rich turns on every system of the colony's life and its world
  (`SettingsCatalog.Systems`: the What's on sections in `SystemSections`, 89 switches with the region's, diplomacy's,
  the calendar, answers and goods on the map), only what is on by default (tested), and adds personal touches every 4
  days, chatter and travellers at 150%, raiders' camps at 125%, the good side on, 16 messages and 6 letters a day; its
  wishes and meetings stay at the default (they would have moved its moods). Demanding adds a short grace for what was
  given and is gone, ailments, everyday hurts and a scenario's fights at 125%, against their nature on. The quick setup
  (Everything on, Council only, Advisor only) reaches the same 89: everything on turns on every one, the switches under
  them too; council only keeps the council and the colony's story; advisor only the advisor and its plan; a part's own
  switch goes off and those under it keep their values. The tooltips say what each covers (`_TipWhole` keys). Measured
  (`Tools/Balance`, `docs/balance-report.md` regenerated: only the presets' rows moved): light touch +7.3, +4.6, -1.7
  to +7.2, +4.8, -1.6 (well run, average, neglected); story-rich +6.9, +3.2, -6.7 to +6.9, +3.1, -6.5; demanding +2.5,
  -2.8, -14.8 to +2.3, -2.9, -14.9, time ill 9/15/32% to 12/17/37%; all within their targets and tooltips.
  (3) The council panel reads the inbox (DESIGN.md "What waits, one list"). `Inbox.Waiting` (`InboxWaiting`, at most
  once a second) reads Needs you's providers, as This week does, and indexes the rows by colonist (a petition's other
  names too, `WeekRow.others`). Rows say what they are about
  (`WeekRow.about`), how pressing on the panels' scale (`rank`, `HudRank.Wish`/`Question`, 270 or 230 for a promise),
  their panel kind (`part`, else `InboxProvider.part`, else the other asks; `InboxMath.PanelPart`) and whether they wait
  (`waits`, false for the news row). `HudCouncil` draws a line a kind (wishes, petitions, promises at risk, letters
  waiting, a new part "asks" for the region's and the peoples' asks) from those rows, ranked by the most pressing
  (`InboxMath.MostPressing`, `PanelScore`), its tooltip the rows and that row's own, its click that row's; its own walks
  over wishes, petitions, promises and the letter stack are gone (a promised wish no longer counts twice; no countdowns
  in its values now, only who waited longest). The bar's wish and promise badges read the colonist's rows
  (`Inbox.Waiting.Of`), a petitioner's own want in a petition of several; ring, strain, faith and leaving read the
  person as before. The game's alerts stay the game's. Fixed with it: a promise taken back past its days for amends no
  longer waits (`AtRisk`), and the count counts wishes and petitions as the rows list them (a petition of several once,
  as 0.114.0's rows and panel already did, no wish without an asker). (4) Fealty and vassals as pacts of their own kind:
  on a people's card "A pact of fealty with ..., the colony sworn to them" and "A pact of protection with ..., the
  colony's vassal" (`CC_Fealty_RowPact`, `CC_Vassal_RowPact`, `_RowPactHamlet`, `CC_Fealty_RowAwaitedPact`) among the
  truces and pacts; the tie's card "A pact of fealty" or "A pact of protection" (`CardWords.TieName`); Needs you lists
  them right after the pacts (32.5); the setting "... and pacts of fealty" under "Pacts and truces" in "Bonds with other
  peoples", and `Fealty.On` reads `Pacts.On` (pacts off, no new pact of any kind; oaths and vassals standing run on);
  the Almanac's diplomacy page tells them as "Pacts of fealty" and "Pacts of protection", its Pacts and Settings
  sections point there. Mechanics untouched. (5) Words: the Almanac's Start here ("Too much at once?": the five pages and
  what the presets reach) and every page that named a settings page (ailments, the council, stories, the colonist bar,
  panels, seeing and hearing, visitors, weather, defending, the good side, factions' mods, one heart), README's
  Settings, panels, badges and fealty, DESIGN.md. New keys in `Rimfolk_Settings_Pages.xml` and `Rimfolk_OneWayIn.xml`.
  Tests: `SettingsTests` (five pages in order with their counts, the rates and knobs where they belong, other mods'
  rows on their page with their mods, fealty under pacts, indents, the keys before the five pages, presets reaching
  every slider of How much and How hard, the region, diplomacy, the heart and its good side, hurts and the gatherings,
  story-rich only switching on what is on, the quick setup over every system), `InboxTests` (the panel's kinds, ranks,
  the most pressing, the lines adding up to the one count, the panel and badges reading Needs you), `DiplomacyTests`
  (the section's new heading). Left: the old page keys (`CC_SetPage_Council` and the rest) and the lines replaced stay
  unused in their files; the colonist bar's leaving badge reads the standing (a warning, not something in Needs you);
  a player who had switched the council panel's "petitions" part off keeps it off, and the new "asks" part is on. To
  check in game: the settings window (five pages, the search across them, Your other mods with and without RimMarket
  or Hospitality loaded, "Rimfolk events › including ones..." on How hard), each preset's list of changes, the quick
  setup's lists, the council panel beside This week (the same wishes, letters and asks, one petition of several as one
  line), a badge for each petitioner, a pact of fealty on a people's card and its tie card, pacts switched off greying
  the fealty row.
- 0.115.0, phase 5 of the review: one festive year (the player's decision 7 of 6 November 2026, "a custom becomes a tradition
  after three years running (one rule)", and the phase in `docs/review-2026-11/whole.md`: customs on the calendar, one
  tradition rule, one feast engine). Not released: no round in Almanac_WhatsNew.xml, About.xml untouched. (1) Customs on
  the calendar (`Calendar/CalendarCustoms.cs`, a part of `ColonyCalendar`). Every custom (a dilemma with a
  `traditionOption`) goes on the colony calendar the first time the colony keeps it (`AddCustom` from
  `DilemmaTracker.Tradition`; `CalendarKind.Custom`, `DayWay.Custom`, its question in `about`, a news line
  `CC_Fest_CustomOn`). A seasonal one keeps the date it was kept on: its question comes round on that day each year
  (`AskCustoms` in the calendar's hour, through `DilemmaTracker.AskCustom`, the council's own letter and answers, forced
  as traditions were), or within five days for its weather or someone to ask for it (`CalendarMath.CustomDue`,
  `CustomGraceDays`), else its year is spent with "came and went this year" (`MarkCustomMissed`). One asked in its
  moment (the planting day, the herd home, the victory feast, burying the enemy dead, the remembrance night, telling the
  bees) is on it "with its occasion" (`CalendarDay.moment`, saved; `Dated` false: the first snow's way) and comes as it
  always did. Customs never make way for the colony's own days (`Fixed`, not `Auto`). A season that holds a custom's
  day is asked no other seasonal custom (`CustomSeasonHeld`), and a dated custom is never one of the council's ordinary
  questions (`BringsCustom`); with the calendar off, customs come as before. The Calendar page shows customs among the
  days (date or "with its occasion", when it next comes, a "custom" pill, years kept or tradition, where it came from
  with the answer that keeps it: `CC_Fest_Origin_*`); its separate "Seasonal customs" list is gone (the old keys stay,
  unused), the intro is `CC_Fest_Intro`; a custom's way and date can't be changed, taking it off loses its years.
  Readers of dates skip customs with an occasion (`CalendarDay.Dated`: This week's coming days, the World page, the
  advisor's days tomorrow, talk of tomorrow, an ambition's days ahead, the eve's talk also skips dated customs since
  their question may wait); a custom's gathering keeps its own shared times (`SharedTimes.DayOf`). (2) One tradition
  rule (`CalendarMath`): three years running for days and customs alike (`TraditionAfter`; `DilemmaDef.traditionAfter`
  is gone); a custom steps once a year (`KeepInYear`); let pass before it is a tradition its years start again and it
  leaves the calendar (`Pass`, `OffCalendar`, `LetCustomGo`, `CC_Fest_CustomOff`); a tradition broken costs as before
  and lapses the second time in a row, leaving the calendar. `IsTradition`, `TraditionCount`, `Traditions` and
  `AddCustomTags` read the calendar (`DilemmaTracker.CustomDay`); the council's forced traditions skip those the
  calendar brings on their day; the answer's sheet says "chosen 1 of the 3 times it takes" and, for a custom on the
  calendar not yet a tradition, that another answer takes it off (`CC_Fest_Impact_OffCalendar`). Customs now count
  among the calendar's traditions (the founding letter, the keeper of days' promise, the long-memoried colony trait).
  Older saves (`DilemmaTracker.MoveCustoms`, once, the first hour with dilemmas on; `customsMoved`, saved): each custom
  the council counted goes on the calendar dated the last time it was asked (that year spent), by
  `CalendarMath.CarryCustom`: a tradition stays one (a break kept), one counted under the old two-year rule keeps its
  count and needs the third year; the tracker's lists are emptied. (3) One feast engine (`PlayOut/PlayFeast.cs`):
  `PlayFeast.Stage(FeastOrder)` is the only place a play-out of the kind Feast is made, with the reason (`FeastWhy`:
  Answer, Day, Guests, Wedding, Supper): an answer's, a decree's, an ambition's or an event's (`DilemmaEffect_Feast.Create`,
  now virtual in `DilemmaEffect_PlayOut`, with the answer's own `Fill`), a calendar feast (`CalendarKeeping.FeastDay`,
  replacing `PlayDay`'s feast: keeper as host, the grieving sitting out, "a feast day" held), a feast with guests and
  the steward's suggestion (`Feasts.StartFeast`, `FeastWhy.Guests`; contests, champion and welcome unchanged), a planned
  wedding's feast (`FeastWhy.Wedding`, the toast "To Alo and Penguin, and many years.") and a family's supper
  (`FeastWhy.Supper`, no cup). The table first, the meal from the stores, the guests and the moods as before; new, the
  host's cup a while in (`PlayOut.toast`, `toastLine`, saved; `GatheringTouches`: the table and the guests near raise
  theirs; `CC_Fest_Toast_Day`, `CC_Fest_Toast_Guests`, else a [raise] line of `Council_Pool_PlayToast`). What each
  reason does when no feast can be set going stays with its caller. (4) The pace as phase 2 left it: calendar days
  within three days as one, a day within two days of another gathering at supper, one seasonal custom a season. The
  player answered the open question the same day: a custom does not also stay two days clear of other gatherings, so
  no constant or setting was added for it. Keys in `Rimfolk_FestiveYear.xml`; no shipped key changed; the defs' "two
  years running" (two mood hints, five files' comments) read three. Tests: `FestiveYearTests` (new: a custom's day and
  grace days, once a year, three running, off the calendar, an older save's counts, one rule and one feast engine in
  the source, the cups' words one sentence), `CustomYearsTests` (the one rule). Save model: the calendar's cap holds
  the customs. Measured (`Tools/Balance --scenes`, 800 colony-years each; the model now holds a third-year colony's
  two kept customs, `Rates.SceneKeptCustoms`, coming before as the council's traditions did, now on their day,
  `CalendarMath` linked): gatherings and outings as their own scene a year, floor evenings included, well run 24.3
  before and 24.3 after, average 23.8 and 23.8, neglectful 23.7 and 23.6 (2.8 a week); every gathering before the pace
  42.6, 38.7, 35.9 before and 42.2, 38.1, 35.7 after; customs asked 3.9, 3.2, 2.3 before (a tradition and the season's
  question could share a season) and 3.0, 2.4, 1.7 after. The old model, without kept customs: 24.2, 23.6 and 22.9.
  Docs: DESIGN.md ("One festive year", the pace's one custom a season, the play-outs and the guests' feast), the
  Almanac's calendar and traditions pages (no keyword change), README (traditions, the calendar, play-outs, the pace,
  feasts with guests). Dev menu: "Calendar: a day comes round now..." asks a custom's question. To check in game: a
  seasonal custom kept (the news line, the custom on the Calendar page with its date and answer), its question on its
  day next year (or a few days on, for a swim's warm day), let pass (off the calendar), kept a third year (the
  tradition's letter); the player's save at load (its customs on the calendar, a tradition still one, the remembrance
  walk or harvest dance with its count); a feast of each kind with the host's cup raised (the harvest day, an answer's
  feast, a feast with guests, a wedding's), and a family supper with none.
- 0.114.0, phase 6 of the review, part 1: letters and petitions (the player's decisions 12 and 11 of 6 November 2026: "letters to
  notables arrive in days and an answer brings something concrete", "shared wants come to the council as one
  petition"). Not released: no round in Almanac_WhatsNew.xml, About.xml untouched. (1) Letters by the colony's means
  (`World/LetterMath.cs`, pure). A leg went at the other people's walking pace, twelve days at most, and the save's towns
  are over fifty tiles off: Stace's greetings to Blash were answered 26 days after they were written. Now each leg goes
  by the colony's age (`Ages.Ours`, `MeansOf`): runners, a rider, a courier, a courier by shuttle, half a day plus 0.09,
  0.06, 0.04 or 0.015 days a tile, at most 6, 5, 4 or 2 days (`LegDays`), there and back (`NeighbourLetters.Handed`,
  `backDays`); a letter carried home by one of theirs and a call's "how far" at their means. At 10, 25, 50, 80 tiles:
  tribal 4.4, 10.4, 12, 12 days before, 1.4, 2.8, 5, 6 now; medieval 3.7, 8.7, 12, 12 before, 1.1, 2, 3.5, 5; industrial
  2.6, 5.9, 11.4, 12 before, 0.9, 1.5, 2.5, 3.7; spacer 1.6, 3.4, 6.4, 10 before, 0.65, 0.9, 1.25, 1.7. The courier
  tracker still brings the answer to the gate. The letters page says by which means (`CC_LetterRoad_Status_*`), never
  when. Letters on the road in an older save keep their `due` and `backDays`. (2) What an answer brings
  (`LetterMath.Brings`, `NeighbourLetters.Read`, `Body`, `Brought`): greetings warmly, a token of 50 to 100 silver's worth
  in the messenger's pack; kindly or coolly, how their town fares (`RegionFortunes.Line`, else a piece of news, else the
  roads quiet); condolences warmly or kindly, remembered by their people ("mourned", now said); a warning kindly or
  coolly, remembered; warmly, a debt of honour (the "warned" deed at weight 10, made when the answer reaches the colony:
  `NeighbourLetter.owed`, saved, `Owed` at the trip's end or at once for a call, through the new `FavoursOwed.Quietly` so
  the answer's words tell it once; else remembered where debts aren't kept). Help, news, a visit and stage 2's purposes
  bring what they did. Every answer ends with its regard moved in words from before and after (`LetterMath.Move`,
  `CC_LetterRegard_*`: "civil before, friendly now", a good deal, a shade, nothing, a shade less), the writer on a warm
  one, their people's goodwill when it rose; the old tone line (`CC_Letter_Outcome_*`) is no longer used. A curt answer or
  a refusal says why (`LetterMath.Why`: war, their regard, the writer, the words, their nature, hard times on business,
  lately, a call, a great deal asked, a bad day), and so does an invitation not taken up or taken up with nobody free to
  travel; a truce, a ransom, an envoy, a ward, a match and an oath keep their own reasons (`OwnReasons`). Bug fix on the
  way: a feast declined by letter now says so in their own words, not "won't be coming". (3) Shared wants as one
  petition (`Wishes/Petitions.cs`, a partial of the component; pure `Wishes/PetitionMath.cs`). A want of one's own
  (`allowPetition` off, not for someone else) that someone else in the colony already waits for joins theirs in
  `CreateWish` (every path: the roll, triggers, dreams, envy): same def, subject key and thing; a petition under way first,
  then the oldest (`ShareWith`, `Join`). Each want stays its own `Wish`, linked by `Wish.petition` (saved, -1 alone); a
  message and a line over their head instead of a letter, the joiners' bond warmed, one chronicle line. One deadline for
  all (two days more a name, never past a fresh wish's full time: `Deadline`), the promise and stage shared, counted once
  (`OpenWishCount`, `LeadsPetition`: the Wishes page, the inbox, the on-screen panel, the tab and overview counts), one
  voice for the reminder and last call. Promise, Refuse and Not now answer all of it, told once (`ForPetition`,
  `PromiseOne`, `RefuseOne`, `DismissOne`). Each want is granted as it is met on the map (own mood, trust, watch); while
  others wait its thanks are a news line naming who still waits (`MetInPetition`, `Wish.petitionMet`, saved, modelled in
  `save_estimate.py`), and the last one met brings the letter naming everyone. At the shared deadline the first to lapse
  tells it once with every name (`LapseTogether`), the others quietly. Co-signed wishes (`allowPetition`) are untouched;
  wishes open in an older save stay on their own, new ones join them. The save's armor (days 82 to 128): six wishes
  become three petitions, five given up two lapses. Keys in `Rimfolk_Letters_Bring.xml` and `Rimfolk_Wishes_Petitions.xml`
  (no shipped line changed). Tests: `LettersPetitionsTests` (means and legs by age, before and after, the save's letter;
  what each answer brings; regard moved; why; joining, deadline, leads, count; the save's armor replayed; the wiring; every
  key built from these rules' names). Docs: DESIGN.md ("Shared wants as one petition", "Letters that arrive in days and
  bring something"), the Almanac's neighbours and wishes pages, README. To check in game: a letter's leg in days by the
  colony's means and the answer's last line in words; a warm warning's debt on the Peoples page once its answer is in; two
  fighters wanting armor as one card with both names, one lapse letter or the letter at the last one met.
- 0.113.1, the good side's bands as the player approved them (the player's decision, 6 November 2026: "As I approved": three
  bands that each give a mood, steady +2, in good heart +3, in high spirits +5, against 0.113.0's two, where steady gave
  nothing and the others +2 and +3). Not released: no round, About.xml untouched. (1) Below steady, nothing to speak of
  yet: `GoodBand.None`, a new first value (the band is never saved, always read from the value, so no position moved
  anything); steady from 20 (`HeartMath.SteadyAt`), in good heart from 45 and in high spirits from 65 as before.
  (2) Moods +2, +3, +5 (`GoodMood`; `Council_Heart_GoodSide` with three stages, steady, in good heart, in high spirits,
  `ThoughtWorker_HeartGood` mapping each band to its stage, nothing yet inactive). Steady gives only its mood: its dream
  and recovery factors stay at 1. Everywhere `GoodBand.Steady` meant "nothing yet" now reads `GoodBand.None`, so steady
  shows like the other two: the meter and its tooltip, the colonist's card, the inspector line and RimHUD ("Heart:
  steady · proud"), and the council's Moods page counts it. (3) Words: new keys in `Rimfolk_GoodHeart_Bands.xml`
  (`CC_GoodSide_Band_*` with nothing yet's and steady's own label and description, `CC_GoodSide_Explain`,
  `CC_GoodSide_ColonySummary`), the code pointed at them and the old band words, explanation and count left unused in
  `Rimfolk_GoodHeart.xml`; the setting's tooltip rewritten in place with its numbers kept ("+2, +3 and +5"), so
  `SettingsCatalog.cs` is untouched; steady's stage has a line of its own for every age; the DefValidator knows the new
  keys. Docs: DESIGN.md (Layer 7's summary, the bands, the words, Balance targets), the Almanac's good-side page and the
  heart's page (rows rewritten keeping their numbers, "+3 mood in place of steady's +2"; no keyword change), README.
  (4) Balance (`Tools/Balance` now counts steady: `goodBandHours` by `GoodBand`, `Summary.steady`, a Steady column in
  the report's good-side table; 120 runs, the same seeds, before = 0.113.0; `before.json` kept as the code before the
  good side): 6 colonists, Rimfolk's mood well run +6.0 to +6.4 (12 colonists +5.0 to +5.7), average +2.5 to +2.6,
  neglected -7.2 to -7.2 (identical: the good side never reaches 20 there); the band mood well run +0.7 to +2.0 before
  growing ordinary, which takes back 5.2 to 5.8; the whole mood 69, 64, 39% as before. Time in each band, well run:
  steady 48%, in good heart 28%, in high spirits 3% (the same values before; only steady's mood is new), average steady
  2% and nothing higher, neglected none; light touch well run 45/34/6%, story-rich 48/28/2%, demanding 48/23/2%. Over,
  as the 0.113.0 builder warned and the player chose: Rimfolk's good moods at a high point, well run, P99 +17.6 to +17.9
  and +12 or more 18% to 21% (12 colonists +16.1 to +16.6, 9% to 12%), and well run now above its target (+3 to +6);
  at 480 runs the same: well run +6.0 to +6.5, P99 +17.7 to +18.1, +12 or more 18% to 22%, average +2.4 and
  neglected -7.2 both. Nothing else retuned. Strain bands unchanged (strained/worn/near breaking average 5.9/1.5/0.3%, neglected
  29.2/17.3/7.7%), the day off 0.03 and 0.72 a colonist-year as before, time under the minor-break line 34% neglected
  as before. (5) Old saves: nothing saved
  changes meaning (the good side is a number); a colonist above 20 is steady at once. Files: `Heart/HeartMath.cs`,
  `GoodHeart.cs`, `HeartView.cs`, `HeartCard.cs` (a comment); `Rimfolk_Thoughts_GoodHeart.xml`,
  `Rimfolk_GoodHeart_Bands.xml` (new), `Rimfolk_GoodHeart.xml`, `Almanac_GoodHeart.xml`, `Almanac_Council.xml`;
  `Tools/Balance` (`Sim.cs`, `Runner.cs`, `Report.cs`, `Program.cs`), `Tools/DefValidator/Program.cs`. Tests:
  `GoodHeartTests` (the three bands from 20, 45 and 65 and nothing yet, the moods +2, +3, +5, each smaller than
  strain's across from it, the three stages and the worker's mapping, steady's factors at 1, the band never saved, the
  new words and the old keys unused, steady shown and counted like the others). textcheck against 4cc8cb8 reports the
  thought's third stage (label, description, mood) as "added outside an existing list", on purpose: the check is for
  prose passes. To look at in game: a colonist with strain at nothing and the good side at 20 to 44 (dev mode: fill by
  20): the meter and its tooltip say steady, the Mood tab's "steady" +2, the card's line, the inspector "Heart: steady";
  below 20 the meter says nothing yet; the Moods page's count.
- the heart's good side: in good heart (the player's request, 6 November 2026: "there should perhaps also be a
  counterweight/side to strain, right? If they have positive impacts over an extended period"; approved as proposed,
  `docs/review-2026-11/decisions.md`, built right after phase 3). Not released: no round, About.xml untouched. DESIGN.md
  "The heart's good side" under Layer 7 and its paragraph in "Balance targets"; `docs/balance-report.md` regenerated
  against the code just before it (`Tools/Balance/before.json` replaced: phase 3's own before and after stay in
  DESIGN.md). One heart with two sides, never both above nothing (`PawnHeart.good`, 0 to 100). (1) Hardship spends it
  before strain builds: each point a source would build, after its caps, spends half a point (`HeartMath.Spend`,
  `SpendShare`), in the hour's `Build`, `HeartLogic.Build` from outside and `HeartLogic.Change`; strain builds only once
  it's gone. (2) Good things ease strain before they fill it: the hour applies rest first, then gatherings, talks,
  small joys and the pile of small good moods in turn (`Memories` now hands gatherings and talks back by `GoodSource`,
  `SmallMoments.Hour` returns the pile apart, `out comforts`), each easing what strain is left, the rest filling the good
  side at its share (45%, 60%, 60%, 15%); `EaseJoys` from outside the hour the same. (3) What fills it, only while
  strain is at nothing, within a cap each and 4 a day in all (`GoodCap`, `GoodDailyCap`, `goodToday`): belonging and
  purpose well met by the hour (0.035 to 0.07, 1.2 a day each), a life goal moving since yesterday (0.6), a deed the
  colony speaks well of that someone else knows of (1 to 2, read from the reputations once a day, `RepMath.Valence`),
  gatherings 2.5, talks 1.5, small joys 1.2, the comforts of home 0.4; it fades 3% a day. (4) Bands, the player's names:
  steady, in good heart from 45, in high spirits from 65: `Council_Heart_GoodSide` (+2, +3; situational like strain's,
  it grows ordinary like any good mood, the pile leaves it out), brighter dreams (`Heart.DreamFactor`: sweet 1.2 and
  1.4, nightmares 0.85 and 0.7), a quicker recovery (rest 1.15 and 1.3 times as fast, read from `goodLately`, how high
  it stood lately). No work speed. (5) The release (`Heart/GoodRelease.cs`): in high spirits and safe, about once in four
  days held there, once in 10 days a colonist, once a day in the colony: one of the game's inspirations by their
  passions and its rules (`GetRandomAvailableInspirationDef`, `TryStartInspiration`, its own letter with Rimfolk's
  reason, a bubble from `Rimfolk_Pool_GoodHeart_Inspired`), 12 off; or a kind word for someone close, the one under the
  most strain first (`Together.When` with a walk over, `Speech.Say` face to face from `Rimfolk_Pool_GoodHeart_KindWord`
  by who hears it, the memory `Council_Heart_KindWord`, a small joy for them with who came over as its cause), 6 off.
  (6) Words: the heart's page has a second meter "Good heart" with what filled and spent it and the last release
  (`HeartView`, `GoodHeart.Tip`), the inspector line and RimHUD read the good band ("Heart: in good heart · proud"), the
  strain tooltip names the other side, the colonist's card one line (`HeartCard`, Now slot), the Moods page counts
  them, the band mood says what filled it; keys `Rimfolk_GoodHeart.xml`, mood hints `Rimfolk_MoodHints_GoodHeart.xml`,
  the Almanac page "the heart's good side" (`Almanac_GoodHeart.xml`) and two rows and a pointer on the heart's page.
  (7) Setting "The heart's good side" (`heartGoodSide`, on) beside the heart's own; off, nothing fills it, the bands
  give nothing, strain as before. Dev mode: fill by 20, overflow now. (8) Saves: values on the heart and three short
  lists (`goodToday`, `goodRecent`, `goodSpent`, modelled in `save_estimate.py`); an older save starts at nothing and
  the first look at deeds and the goal only takes note. (9) Balance (`Tools/Balance`: `HeartSim.cs` plays it with the
  good side's own draws, the needs a day at a time with each colonist's lean; a "without the good side" case on the
  same seeds and a section of its own in the report): 6 colonists, in good heart and in high spirits well run 28% and
  3%, average and neglected almost never (their good side stays under 20, but it takes average colonists' knocks:
  strain at nothing 46% to 66%, strained/worn/near breaking 8/2/0% to 6/1/0%, the day off 0.05 to 0.03); inspirations
  0.11 and kind words 0.13 a colonist-year, well run; Rimfolk's mood +5.6 to +6.0, +2.5 to +2.5, -7.0 to -7.2 (noise at
  480 runs: -7.1 and -7.2, under the minor-break line 33.6% and 33.5%, the day off 0.78 both); the whole mood 69, 64,
  39% as before. Tuned down from the proposal: the bands from 45 and 65, +2 and +3 (about +2, +3, +5 proposed). Over:
  Rimfolk's good moods at a high point, well run, P99 +17.5 to +17.6 and +12 or more 16% to 18% (+1 and +2 lifted it as
  much). Files: `Heart/HeartMath.cs`, `GoodHeart.cs` (new), `GoodRelease.cs` (new), `HeartCard.cs` (new),
  `HeartLogic.cs`, `HeartData.cs`, `HeartTracker.cs`, `HeartView.cs`, `Heart.cs`, `HeartDebug.cs`;
  `Moods/SmallMoments.cs`; `CouncilSettings.cs`, `UI/SettingsCatalog.cs`; defs `Rimfolk_Thoughts_GoodHeart.xml`,
  `Rimfolk_Pool_GoodHeart.xml`, `Rimfolk_MoodHints_GoodHeart.xml`, `Almanac_GoodHeart.xml`, `Almanac_Council.xml`;
  `Tools/Balance` (`HeartSim.cs`, `Sim.cs`, `Runner.cs`, `Report.cs`, `Program.cs`, `before.json`). Tests:
  `GoodHeartTests` (new: the bands, filling and spending, never both at once, the caps, the fade, the release, the
  setting, old saves, the words), `SmallMomentTests` (the hour's new shape), `ImmersionTests` (the two pools). Found and
  left: a kind word chosen is set going even if the two never meet within ten hours (the colonist's cooldown is spent);
  a small gift and a hand with the work weren't built (nothing honest to show on the map); the people table's strain
  cell and the bar badge show strain alone; the sim's needs of belonging and purpose are assumed per play style, not
  measured. To look at in game: a colonist's Heart section after a quiet week with friends, the meter filling while
  strain is at nothing and spent by a raid; the inspector line in good heart; the band mood's tooltip; a kind word
  walked over and said; an inspiration's letter with its reason; the setting off.
- 0.112.0, phase 4 of the review, part 2: a fairer council (the player's decisions 8, 9, 10 and 13 of 6 November 2026: "life
  goals that last", "elections on a record", "office names by the colony's age", "overlapping decree pairs merge;
  groups form around real divides"). Not released: no round in Almanac_WhatsNew.xml, About.xml untouched. (1) Elections
  on a record. Ten of twelve seats had one promise that always fit, and three physicians promised word for word that
  nobody hurt would wait for a doctor. Each seat now has three or more that always fit (23 more, with their canvass and
  hustings lines, in `Rimfolk_OfficeWords.xml` and `Rimfolk_Election_PlankLines.xml`, merged into the offices at startup
  by `OfficeNames.Apply`); every promise says what it promises and how it is judged (`OfficePlank.promise`, `test`).
  Each candidate runs on their own record (`OfficeTracker.RecordOf`, `RecordMath.Record`): their work in the seat since
  their promise (the seat's records, `OfficeWordsDef.measure`: tends, plants sown and brought in, kills, trades, quarrels
  mended, days kept, animals tamed, rites led, camps greeted), else that work in all from ten, else the name the colony
  knows them by (`KnownAs.Short`), else a goal reached; said after the promise in the announcement and on the election
  panel's tooltip. The winner's promise goes into the history at the count (`office:promise`, a milestone with words
  and values: what the map showed then, the work count), and the next yearly election judges it when it's called
  (`JudgeRace`, `RecordMath.Judge`: trust, rivalries, the colony's dead by the game's count, food days, medicine each,
  sickbeds, mood, the herd, days kept, the faithful's share, or the holder's work, which only ever keeps a promise),
  said in the announcement, at the hustings (held at the count since phase 2, before the votes are read; the holder
  runs on a kept one, `Rimfolk_Pool_HustingsKept`; a non-backer throws a broken one back, `_HustingsBroken`), on the
  panel and in the results ("At the last election Toni promised a proper watch at night, and the colony has lost nobody
  since: a promise kept."), and kept in the holder's history (`office:kept`, `office:broken`). At the vote +1.5 kept,
  -1.75 broken, +0.75 for work in the seat (`RecordLean`, one term of `Leaning`). The office family never reaches the
  chronicle (`ChronicleMath.Shows`). By-elections judge nothing; the instant vote has no promises. The 0.106.1 count
  (every seat accounted for) is untouched. (2) Office names by age (`OfficeWordsDef` tribal/medieval/industrial/spacer;
  medieval is the label): first voice, speaker, chair, coordinator; war leader, marshal, captain of the guard, security
  chief; keeper of stores, quartermaster, quartermaster, logistics officer; healer, physician, doctor, medical officer;
  feast-giver, steward, welfare officer, morale officer; seed-keeper, reeve, farm foreman, agronomist; peace-weaver,
  peacemaker, arbiter, mediator; season-caller, keeper of days, master of ceremonies, ceremonies officer; herd-watcher,
  herdmaster, head stockman, husbandry chief; spirit-speaker, chaplain (three ages); guest-friend, warden of guests,
  gatekeeper, liaison officer; elder (three ages), senior councillor. Every code site that named a seat says
  `OfficeDef.Name` (22 files); duties, description, the offices' own lines (`Interplay.Line`, `Interplay.Journal`), a
  seat's life goals, the elected line and the keyed lines about the colony's own seats (`OfficeNames.Key`: renamed
  before they're filled in) say a shipped seat word in the age's name (`OfficeNames.InAge`, pure
  `OfficeNamesMath.Rename`: whole words, plurals, possessives, a/an); the seats' marks in the health tab too
  (`Patch_OfficeHediffLabel`, a postfix on `Hediff.LabelBase`). (3) Life goals that last (`GoalMath`): a fight sparks
  only the one who brought down the most (`Foremost`, in `SocialWeb` before the count resets); a fight's spark pulls 0.4
  (a nemesis 0.6) in both the chance to choose at once and the pick; a goal others at home hold weighs half, a third;
  the strongest spark counts first; a change of heart after twenty days only, under a third done, for a brush with death
  or a loss, once per goal (`Aspiration.turned`, saved; goals held stay). Measured in `FairCouncilTests` on twelve
  colonists over the save's 88 days: fighting goals 39 of 79 before, 0 of 15 now; most changes of heart within 25 days
  3 before, 1 now. (4) Decrees and groups. One idea, one decree (`DecreeMath.Ideas`, `DecreeWork.FormNow`, hidden
  otherwise): drills by age (militia drills tribal and medieval, drills industrial and spacer), rationing by the moment
  (famine bread while a famine or drought runs and the stores aren't low). Can't run together (`DecreeMath.Apart`,
  `CC_Decree_Overlaps` in `CannotEnact`): festival days and a feast day, quiet hours and days of rest, on guard and a
  curfew. Rest when ill and quarantine kept apart and allowed together. Older saves (`DecreeTracker.CarryOn`, first hour
  after a load): a form in force no longer listed carries on as the listed one for what was left, never longer than
  it runs; both forms in force, the listed goes on; rationing's forms only lose a duplicate; petitions for an unlisted
  form are let go quietly (and daily). Both drills keep `hurts` Drills and Muster. Groups: formed, joined and kept by what
  binds some more than the rest (`GroupMath.Distinct`, `GroupFacts.distinct`; cohesion still reads the plain pull), at
  most 45% of the colony (`MaxFor`: five of twelve; the loosest goes, one a day; merges stop there), and whoever is
  outside every group shows as outside with why (`GroupText.OutsideWhy`, `GroupMath.WhyOutside`: at odds, new, their
  own way) on the People table, the Bonds page and the character page. An older save regroups over ten days at the usual
  pace, quietly, told once (`CC_Group_Regroup`, `GroupTracker.regroup`, saved); the 0.101.2 names stay. Keys in
  `Rimfolk_Election_Record.xml`, `Rimfolk_Decrees_Distinct.xml`, `Rimfolk_Groups_Divides.xml`; one shipped line changed in
  place (militia drills' description no longer compares it with drills). DefValidator knows {PROMISE} and {EVIDENCE}.
  Tests: `FairCouncilTests` (new: names in every age, renaming, judging, records, the history kinds kept out of the
  chronicle, the defs' promises and words, goal rules and the 88-day colony, groups on the save's shape with Barr
  outside, decree forms, overlaps and carrying on), `ElectionTests` (the office words' planks and pool extensions
  counted). Docs: DESIGN.md ("Elections on a record", "Seats named by the colony's age", "Life goals that last", groups'
  "Real divides", "Decrees that mean distinct things", the history's kinds, the links table), the Almanac's offices,
  decrees, life goals and groups pages, README. Left: shipped lines elsewhere that name a seat in passing (dilemmas' "the
  quartermaster's friends", "sent the speaker to the gate", the steward's feast suggestion, a truce or an oath sworn by
  the speaker) keep the medieval word (renaming them would rename other
  peoples' elders too: new age-tagged variants would do it); the review's evening drill on the map each day a drills
  decree runs and a newcomer's wait before standing are still the player's word. To check in game: the player's save at
  the next new year (each candidate's record after the promise, distinct promises; the save holds no promises yet, so
  that election records the winners' and the one after judges them in the letter, at the hustings and in the results;
  the industrial names: chair, captain of the guard, doctor, farm foreman), a raid (one goal at most, for
  the one who brought down most), the groups' one message and Barr shown outside, the Decrees page (one drills, militia
  in force carried on as drills).
- 0.111.0, phase 2 of the review: one teller, one pace (the player's decisions 1 and 2 of 6 November 2026). Not released: no
  round in Almanac_WhatsNew.xml, About.xml untouched. (1) The pace (`PlayOut/Stage.cs`, pure `StageMath.cs`): one count
  of gatherings per colony, saved with the play-outs (`PlayOutTracker.stage`, a `StageColony` a home: the start of each
  gathering of the last ten days, the last of any kind, the season's custom, the toasts waiting). Every gathering lord
  on a home is counted as it starts (`LordJob_Joinable_Gathering`, the game's marriage ceremony, feasts with guests: the
  game's parties and rituals, play-outs, meetings, the election's), and every outing where it starts (`PlayGroups`, not
  an errand or a gift); lords on at a load were counted before it. What the player chose always happens and counts. The
  colony's own (`PlayOut.routine`, saved, false in an older save: a group's call, a family supper, a growth birthday's
  toast, the floor's evening) start only with room (`StageMath.Room`: fewer gatherings in the last seven days than the
  setting allows, two by default, and none in the last 36 hours), wait evening after evening up to three days and are
  let go quietly; the tracker steps the player's plans first. A camp's show and song are offered only with room (their
  letter says tonight). Calendar days (`CalendarKeeping`): days kept with a feast, games or a fire that come round within
  three days ahead are named in the first one's letter and kept with its one gathering, each its tradition step, let
  pass together (`KeptWith`, `ChoiceLetter_CalendarDay.withIds`/`withYears`, their `lastYear` spent by the letter); a day
  that gathers people (those, faith, a remembrance) within two days of another gathering, or with a player's gathering
  pending, is kept at supper (`atSupper`, saved on the letter; "Keep it at supper"; the keeper's word over the meal
  through `Together` in supper hours, the day's memory then, a news line `CC_Cal_KeptAtSupper`/`_RememberedAtSupper`).
  One seasonal custom a season (`Dilemmas.IsSeasonalCustom`: seasons and a tradition option; `Stage.CustomMayCome`,
  `CustomAsked`; traditions forced as before). A life goal's toast is raised at the next festive gathering
  (`Stage.Cup`/`Started`/`CupsAt`/`RaiseCups`, `GoalRewards.Toast` makes no evening of its own; both are asked to it, the
  begin message says who has a cup to raise, the cups go up a while in, `LordJob_PlayOut.cupsRaised`), at supper after
  two days, let go after five; a gathering that falls through hands its cups on. The hustings join the count
  (`Election.speakAtCount`: `TryHustings` skips the evening, `CountTalk` lets each candidate there speak and be cheered or
  heckled before the votes are read; the election texts say so; a hustings under way at a load runs as before). The
  floor: from seven days without a gathering the colony's own come at once, from eight with nothing of its own on the
  way the colony plans "an evening together" (the game's party with answers off). Fixes with it: a plan that falls
  through gives nobody its moods (`Fallback` gives them only when the setting was switched off; "People took heart from
  it all the same" is gone, the keys say "never came about"); a player's gathering kept from its evening by another
  (`collided`) takes the next evening once (`movedOn`); a bonfire's fire is laid in the hour before its evening
  (`PlayFire.Ahead`), and one still unlit two hours before the plan gives up is let go for a gathering without a fire, in
  the open or at the rec room or table (`PlayFire.GiveUp`). The setting `gatheringsAWeek` (People, How often; light touch
  1, story-rich 3). Keys in `Rimfolk_Pace.xml`. Measured (`Tools/Balance --scenes`, a new "As built" table on the very
  rules, `StageMath.cs` linked): average play 4.7 gatherings and outings a week before, 2.7 after (well run 5.1 to 2.8),
  the busiest week 14 to 9, days with two 8 to 2.2 a year, under a day apart 46% to 24%, the longest gap 9 days.
  (2) The teller (`UI/Tell.cs`, pure `TellMath.cs`/`TellBook`): the channel by weight, kind and the colony's day. One count
  a day per colony for messages (12) and letters (4), all kinds together (the per-kind caps of 20 and 6 folded in; new
  setting keys `messagesADay`, `lettersADay`, so an older settings file's numbers don't stand); something hard to miss
  always shown and the day busy for six hours (so is a raid on the home, `LastThreatBigTick`); on a busy day (that, or
  half the messages) one person's, health and the land's news wait for the evening (`TellMath.Weighty`); the evening's
  news has room by the day (`EveningRoom`: 8 lines down to 3, one fewer for each letter, message or question), keeps the
  weightiest (`EveningScore`, `NewsItem.weight` saved) and puts the rest in the History tab; the same words shown as a
  message aren't the evening's too (`TellBook.Told`). `AddChronicle` goes through `Tell.Chronicle`, which writes the
  history. Dev menu: "Teller: what was told today", "Pace: the week's gatherings". Measured (`Tools/Balance --tell`, new:
  the simulator's stream, old rules against the teller and the pace): letters and messages 1.92 a day to 1.64 (gathering
  messages 0.67 to 0.39), the busiest day 12 to 8; on the save's archive (13 days; Rimfolk's messages aren't archived, so
  only its letters and questions count toward the day) the evening's news 3.9 lines a day to 3.3. Tests: `StageTests`
  (new), `TellTests` (the colony's day, the busy day, the evening), `HistoryTests` (the chronicle through the teller).
  Save models: the count, its gatherings and toasts, the letter's days kept together. Docs: DESIGN.md (Telling the
  player, the play-outs' moods and safety, "Pace: how often the colony gathers", the save table), README (the toast, the
  calendar, Colony news and the teller, "The colony's pace", the settings), the Almanac (answers on the map, elections,
  groups, birthdays, the calendar, life goals, customs, the guide's news). Left for the player's word: "within two days"
  for customs beside a gathering (the review's proposal had it, the decision didn't); presets for the rest (phase 6). To
  check in game: a week with a group's call waiting for room (This week shows it waiting), two calendar days three days
  apart (one letter), a feast two days after a gathering (kept at supper, the word said at supper), a life goal reached
  (the cup at the next gathering), the election's count with speeches, a bonfire with nobody free to lay its fire, a
  busy raid day's messages.
- 0.110.0, phase 4 of the review, part 1: one name (the player's decision 6 of 6 November 2026: "one slow 'known as' per colonist
  and one 'known for' per colony; reputation's six sides stay as details"). Not released: no round in
  Almanac_WhatsNew.xml, About.xml untouched. The save at day 128: "the Healer" went Harper, Alo, Harper, Alo in 70 days on
  the old 15% margin, Harper held six names in 80 days, Hector's card read "Hector, the Builder, Beastfriend, Tinker" with
  renown's "Known as the one who ..." as a second line, and the colony was told by four systems in four places with their
  own chronicle lines. (1) A name holds (`Epithets/NameMath.cs`, pure; `EpithetTracker.Names`): nobody can take it for 30
  days (`HoldDays`); then the best other must be clearly ahead on the count (`ClearLead`, a quarter, or the name's own
  margin) every day of a season (`ChallengeDays` 15; a day not ahead ends it, a new best starts their own) and do half again
  as much of the work as the holder over it (`OutdoShare`; twice the count and more of the work also does, `FarAhead`); a
  holder still at the work keeps it and the season starts again; the one who lost it can't take it back for a year
  (`ReturnDays`). On a model of the save's two doctors taking turns a season each, the old margin passed the name three
  times in 80 days, the new rule never; one who stops loses it once. A condition broken (the Unshaken) still loses it at
  once. The one who lost the name they were known by writes it once in their journal (`Rimfolk_Journal_EpithetLost`,
  `Rimfolk_Journal_OneName.xml`). Saved packed, one line a name held (`holds` through the field `holdsSaved`,
  `NameMath.Pack`; modelled in `Tools/save_estimate.py`). (2) One "known as" per colonist (`Epithets/KnownAs.cs`, the one
  place): the short form is one of the names they hold (`EpithetTracker.Chief`: a proud one before a dubious one, then the
  one held longest; `NameOf`, `AlsoOf`), the line says it with what the peoples know them for and how far it carries
  (`Renown.Own`, read from the history and the peoples' memory; `NameMath.LineKey`, keys `CC_Name_Own_*`): "Known as the
  Healer, the one who won the wrestling at the feast with the Lenlia, talked about in the next town."; "Known in the
  colony as the Healer." while nobody outside speaks of them. Under it the other names ("Also called ...") and
  reputation's sides (`Reputations.SidesLine`, also the character page's first line). The card's title and Who lines
  (renown's own colonist line went), the Story tab, the People table, the wishes' tooltip, the annals, the eulogy tags
  (`NameOf`, was `TitlesOf` joining every name), the death letter (`Chief`), renown's "the one the colony calls ..." in
  talk and letters (`Renown.Held` names the chief) and People → Renown ("Known by name": each of the colony's people by it)
  read it; the chronicle's "X became known as Y." only when it is the name they are known by now (a second name is the news
  and the journal). Every name held still counts where systems count (`Of`: a ballot, renown, the heart, the events asking
  for one by name). The names setting's tooltip says how a name moves (`CC_Name_Settings_Epithets_Tip`). (3) One "known
  for" per colony (`Character/ColonyName.cs`, pure `ColonyNameMath.cs`): one card read from the ways, the traits, renown
  and the peoples' memory, whose data stays where everything reads it: what people call it (ways and traits in one list,
  a way known far and wide first, `Order`) and how far its name carries beyond its walls and for what, in one sentence
  (`Main`, `CardKey`, keys `CC_Name_Card_*`); then the deeds behind it (renown's, `RenownPanels.From`; the council's latest
  three choices behind its ways), what is held against it, what each people remembers (a line each, four at most), the
  notables and what it does (`RenownPanels.Reach`; `RenownPanels` registers nothing now). On Council → Ways ("The
  colony's name" at the top, the ways and the rest below), a home's card (in renown's place), the Overview's line, the
  Culture tab, the founding letter and the press article (`CalledWords`). What it is known for abroad holds while still
  enough by renown's rule (`HeldKind`). One chronicle sentence when it changes (`Look`, `Key`, `Diff`, `Clauses`,
  `CC_Name_Changed_*`; the key `CharacterTracker.nameKey`): after the ways move and at the end of the council's day; the
  ways and the traits write no chronicle line of their own (their letters and news stay); a way fallen from far and wide to
  known says nothing in the chronicle and its news says the neighbours still call it that (`CC_Name_Ways_Narrower`, "people
  have stopped calling the colony merciful" was untrue of it); a band move stays renown's own sentence and the key takes it
  quietly (`ColonyName.Settle`). (4) Older saves: names held stay; a name with no hold is held from the newest history line
  that gave it ("Alo became known as the Healer.", `NameMath.SameLine`), else from its first look, so nothing moves at once
  (a challenge needs a season) and nothing is told; the colony's key is set at its first look without a word, and a part not
  read (the ways off, renown not yet looked at) is never a change. On the player's save: Hector the Builder (also
  Beastfriend and Tinker), Harper the Vault (also the Old Hand and the Bottomless), Alo the Healer since day 113 (also
  Goodhands; held to day 143, so the earliest the Healer can move is day 158, and only to one who outdid Alo at the
  sickbeds all that season), Penguin the Cook, Stace the Mender, Barr the Sower, Macy Greenhand; the colony "traditional,
  generous, learned, devout, artful and walled"; its renown's band was never told (none saved), so the card reads renown as
  it stands and what it is known for abroad joins the name's key quietly at the first diplomacy day. Keys in
  `Rimfolk_OneName.xml`. Tests: `OneNameTests` (new: the hold, the
  season, outdoing, the year's bar, the two doctors and the one who stops, packing, the one name, the line, the one list,
  the key and what changed, the sentence's clauses, the card's line, where each is read), `RenownTests` (the card lines
  moved). Docs: DESIGN.md ("One name" after "One history", renown's "Where it shows", the save table), README (names,
  Our ways, renown, the People row), the Almanac's names and annals page, the colony's character page and the diplomacy
  page. Left: colonist and kin memory as one, deeds kept once, and office names by age (the holistic review's phase 4,
  the other part's and later); the envy trouble's name (`Setbacks/Troubles.cs`, the heart's part) still reads the first
  proud name rather than the chief. To check in game: a colonist's card on the player's save (one name in the title, the
  line, also called, the sides); People → Renown; Council → Ways (the card at the top); a home's card in the region window;
  the chronicle after a way or a trait is gained (one sentence); the next name moved (not before its hold and a season).

- 0.109.2, text pass: the world's voices (6 November 2026, the player's "please launch one or more text enhancement tasks, adding
  more immersion and variety": visitors and callers, the camps, the region, the outsiders' talk). Not released: no
  round, About.xml untouched. First the words against the map, each case checked in the code and fixed where it was
  wrong; shipped lines keep their text and tags (textcheck against 05c4efa clean), narrowed or retired through
  `OutsiderTalkMath.AlsoWhen` and `ShippedLines` as round d's. Fixed: (1) a camp's lines over their heads (hourly,
  `CampLife`) were picked by the era alone, so "Praise to {IDEO}." and "{IDEO} keep us." showed "{IDEO}" unfilled and
  "It wasn't our war." or "Have you heard the word?" were said by any camp: `Camp.Bubble` picks by the camp's tags and the
  speaker's moment (hour, weather, season) with the line memory, and the show and the song use it too; "Their archers
  were a joke." only after a fight with a people of bows (a war party's `foe:` tags from its enemy), three bubbles that
  name an hour only then; (2) a camp's farewell is told as they pack up and set off (`CampLife.Leave`): 17 lines that
  told of a wave, a bow or a word at a gate nobody walked to (only the hermit, the healers and the mourners walk there),
  a sound heard after they were out of sight, or ash and bones round a fire that is gone are retired, and 14 that name
  the hour (dawn, first light, noon, in the night) are said only then (`CampLook.FarewellText` tags the hour, their
  animals and a camp come back); (3) the parting question came at any hour of the stay's last day saying someone "came
  to the gate at dusk" and the camp would "go on in the morning": it comes at dusk now (`CampStoryMath.PartingHour`),
  sent rather than brought, in new words (`CC_Camp_Goodbye...`, nine wordings, `Rimfolk_CampWords_Goodbye.xml`), the old
  keys left unused; (4) a chat's opening "I don't think we've talked yet, have we?" and "glad to finally meet you" were
  said however often the two had talked, and "I'm glad you came back" to a new face: `Outsiders.Line` tags first, met,
  returning and known (and the outsider's people by age, their:tribal... their:empire) and the three ask for them; (5) the
  shared meal praised whatever was on the plate, paste too: the meal tags each plate (`fare:awful`, `simple`, `fine`,
  `lavish`; `OutsiderOutcomes.Fare`) and three lines ask !fare:awful; (6) `did:KIND` is set by any asking, refused or
  not, and eleven talk pieces spoke of a meal eaten together, a gift kept, a lesson given or a bout fought: `shared:meal`,
  `drink`, `lesson`, `gift`, `bout` read the outsider's own memory of it (`Facts.Shared`, the game's mood memories) and
  the eleven ask for it; (7) "Who's camped out on your land, then?" was asked by one of that very camp: `them:camp` and
  `them:camp:KIND` (`RegionNearFacts.TheirCamp`, `FactsMath.CampTag`: the kind's defName without its prefix), and the
  piece asks !them:camp; (8) callers: a captive's kin spoke of a sister and a dog the game never made, an undertaker
  called every dead raider "he", the animal doctor spoke of a leg whatever was hurt: retired. Then variety, every line
  said only on facts its providers set, each checked in code. Bubbles at the gate and the table, 323
  (`Rimfolk_Lines_OutsiderSays_Voices.xml`): the basic moments of a talk with an outsider had 2 to 6 lines each and now
  have 4 to 33 (greet 6 -> 32, chat 11 -> 33, the meal's ask, yes, no, talk and end 2 or 3 -> 7 to 19, drinks, gifts,
  news of home, a good word, letters, lessons, asks, courting, faith, bouts, a talk's end), by their people's ways (the
  Empire's court apart) and how well the two know each other. Camps, 307 (`Rimfolk_CampWords_Voices.xml`, a
  FlavorExtensionDef with `camp`, joined at startup by `CampKindDef.AddWords`): bubbles 217 (each kind 6 to 13 -> 11 to
  23: the hour, the weather, their people's age, the world event that brought them, a camp come back, a war party's foe)
  and farewells 90 (each kind had 1 to 4, some of them untrue; now 3 to 7 true ones, the settlers' one aside). Talk
  pieces, 116: with a camp's travelers 69
  (`Rimfolk_OutsiderTalks_CampFolk.xml`: two for most of the 31 kinds, their trade and their road, six for any camp's
  people, four for a visitor on the camp at the colony; before, one piece spoke of a camp at all) and the library's thin
  spots 47 (`Rimfolk_OutsiderTalks_WorldVoices.xml`: the colony's news a visitor heard, a harvest, a decree, research, a
  project, a newcomer, a quiet month, a feast day today or just past, machines, insects or beasts at the walls, the
  speaker chosen 12; what went through 5; the colonist as leader, healer, old hand, newcomer or parent, an old or married
  traveler 7; their town short of fuel or hands, their caravan and visitors on the road, its fortune poor, steady or
  flourishing by their ways 13; their wars long, new, fought on the colony's land, with the colony's friend, a battle on
  its land 5; fog, storm, an early start 3; a guest and a lodger 2). Callers 38 (`Rimfolk_Pool_CallerSays.xml`: the
  collector, the barber, the animal doctor, the undertakers, a captive's kin, a wedding party, drawn by `Callers.Say`
  beside the event's own three to six, with the memory). The region's news and letters 39 numbered fellows
  (`Rimfolk_RegionVariants_Voices.xml`: far news from a camp's traveler 15, a raiders' camp seen and its raids 8, towns
  emptied, faded, held on to and founded 4, trading posts closed, sacked and razed 5, a hamlet's asks and endings 7). The
  talk library 1,630 -> 1,746 pieces; the variety report (which now counts a camp's words from a file of their own with
  its kind) 63,897 -> 64,992 lines in 8,354 -> 8,471 pools; its draw estimates for the outsiders' bubbles and the camps'
  lines are the rare default, so their "seen a year" stays near nought before and after, and its top three (the
  ailment, new-goal and elected journals, 5.31, 3.46, 2.93) are outside this area and unchanged. Files:
  `Camps/Camp.cs`, `CampKindDef.cs`, `CampKinds.cs`, `CampLife.cs`, `CampLook.cs`, `CampStory.cs`, `CampStoryMath.cs`,
  `Flavor.cs` (`HourTag`), `FlavorExtension.cs`, `Lean/ShippedLines.cs`, `Outsiders/OutsiderOutcomes.cs`,
  `OutsiderTalkMath.cs`, `Outsiders.cs`, `Text/Facts/FactsMath.cs`, `FactsWho.cs`, `Visitors/Callers.cs`,
  `World/Region/RegionNearFacts.cs`, the talk format's header (`Rimfolk_OutsiderTalks_Greetings.xml`, a comment),
  `Tools/variety_report.py`. Tests: `RenownTests` (the shipped lines' other tags set where they're picked),
  `ImmersionTests` (the callers' pool, a camp's words from an extension one sentence each), `OutsiderTalkTests` (a tag
  for each kind of camp and none for a kind that isn't, the `shared:` tags), `CampStoryTests` (the parting at dusk).
  Found and left: the visitors' trips pool (`Rimfolk_VisitorLines`, 316 lines, rarely drawn) untouched; a camp's letters
  still say someone "came to the gate" for an ask nobody walks to (the sick one's "before dawn" though asked between 9
  and 18), and a few settled texts and bubbles claim what isn't shown ("a lookout in the nearest tree", "Carts coming,
  two of them."); the meal's "fare" is the speaker's own plate, a drink none; `shared:` lasts as long as the mood memory
  (8 to 20 days). To look at in game: a trader greeted the first time and the third; a meal of paste and one of fine
  meals at the table; a camp's bubbles at night and in the rain; a farewell at dawn and one at dusk; the parting word at
  dusk; a camp's traveler talking of their trade.
- 0.109.1, text pass: dreams, ailments, stories and animals (the player's request of 6 November 2026, "text enhancement tasks,
  adding more immersion and variety": the colonists' inner and everyday words outside the systems being rebuilt, after
  the systems review's findings on dreams, ailments, personal stories, quirks and animals). Not released: no round, no
  version. 1,070 new lines in seven new files, every one said only on facts its system sets (checked in the code);
  shipped lines keep their text and tags (`textcheck --base 05c4efa` clean). Dreams, 31 new and 272 lines
  (`Rimfolk_Dreams_OwnDays.xml`): of what really happened to the dreamer, from what the dream system already reads:
  carried out of a fight or tended under fire (`Council_SavedMe`, the one who came as {OTHER}), a caravan home after two
  days (`Council_HomeAgain`), a wedding (GotMarried, about the partner), a baby on the way and the worry of it (the
  father's `Council_Fam_BabyComing`, `_Worried`, the mother as {OTHER}), a break let out (Catharsis), a funeral, music
  and a festival, the herd's first young, the colony's first days, a fine supper and a meal with no table (weight 0.3),
  a hive or a mech cluster brought down; the body: a drink (AlcoholHigh, Hangover), a smoke, hunger (Malnutrition),
  carrying a child (PregnantHuman), four ailment kinds (an upset stomach, sore eyes, burning skin, sore feet); a fight
  two days gone for one who can fight, by the colony's age; who they are: a passion for the fields (spring and summer),
  the kitchen, the rock, building, learning or art, and sixty or older. `Dreams.cs`: a dream with nothing of the
  dreamer's own (`DreamDef.Generic`: no happening, nobody, no feeling, camp or region, no season, no dreamer's role, no
  omen) is half as likely, and one they've had is 0.6 as likely a time to three (0.6 once before, however often: the
  save's "best party there ever was" five times). Ailments, 275 (`Rimfolk_Lines_Ailments_Voices.xml`): the first day
  159, the end 75, a serious illness 41, true to each ailment's course as its description tells it; the voices' fallback
  lines are untagged, so a tone with no line for this ailment draws from several ([everyday] lines took the tagged pick
  from the ailment's own); the age's remedies by the colony's tech. Getting better, 99
  (`Rimfolk_Lines_Health_Voices.xml`): convalescing 30, the first after hunger ([hunger]) and the weather ([exposure],
  sources the code had and no line had), a setback 9, strength back 9, a sickbed visit 10, said while convalescing 11,
  said while ill 30 (three everyday and one serious a voice: the one [everyday] line a voice had was every cheerful
  colonist's). Personal stories, 228 (`Rimfolk_Arcs_MoreWords.xml`): the long discipline 42, somewhere to belong 34, a
  calling 34, passing it on 33, the apprentice's masterwork 32, a long grief 31, getting clean 22 (it can run again for
  the same person): two more chronicle lines and three to five more of the colonist's own a stage, the other's where the
  stage has them, words for an early end; a flavour extension can name a story now (`FlavorExtension.cs`: `arc`,
  `ArcStageWords`, `AddToArc`; the other's and the third's lines only where the stage has some). Animals, 90
  (`Rimfolk_Pools_Animals_Kinds.xml`): what's said to an animal at work with it and to one at the heels is told its body
  now (`AnimalLife.cs`, `HerdStoryMath.BodyTag` as the herd's journal; the herd leader's death too, `HerdStory.Lost`):
  words for birds (20) and for insects, reptiles and other mods' creatures (18) to the animal, 6 at the heels, and the
  herd's leader 10, its words aloud 4, its last season 9 (the keeper sits with it that evening, as staged), its death
  11, spring's first born 12 (looking forward to the visit staged later that day). Eulogies, 27
  (`Rimfolk_Lines_Eulogies_More.xml`). Quirks shown on the map, 79 (`Rimfolk_Lines_Quirk_Habits_More.xml`): nine small
  habits and three of the events' quirks, said by the same person every time: lines for any moment (one tagged for the
  season or for being old took most picks while it held) and children's own. Words found untrue and fixed: (1) the long
  discipline's art taken away was told as "lost", which no line was tagged for, so "I've let the practice slide. The
  wall can wait." told it: the reason is "taken", as its line was tagged (`ArcWorker_MagicDiscipline.Broken`,
  `Integrations/ModStory.cs`); that line, the chronicle's "put training aside, with the wall still standing" (untrue
  then too) and "{OTHER} died before {PAWN} could get past the wall" (the teacher's death read as its cause, the
  review's finding) are retired (`ShippedLines.Retired`), with words for each end beside; (2) spring's first born: three
  shipped lines told the keeper's visit to the newborn, staged later that day, as done ("I stood by it", "I held my
  breath until it stood", "Went to look"), the 0.106.1 pass's "found and left": retired; (3) two eulogy lines said
  anyone, a newcomer or a child, hauled the stone for most of the walls and stayed through the raids and the hungry
  seasons: retired, their fellows ask [longtime]. Variety (`Tools/variety_report.py`, before and after measured the same
  way: the report gives the ailment journals, what the ill say and the animal pools their callers' tags now, weighs the
  filler dreams as the code does and joins a story's extension to its pools): dreams with no cause 15.3% of dream draws
  to 7.2%, of what happened 31.6% to 39.8%; the ailment's first day seen 3.61 times a line a year to 1.82, one person's
  own lines 6.6 to 16.5, its "thin" flag gone; its end 8.4 to 10.2 own; what the ill say 1.1 to 4.2 own; a sickbed visit
  1.5 to 3.4, strength back 1.2 to 2.1; the herd's leader 2.0 to 5.7 own, its last season 1.7 to 4.9, its death 2.0 to
  4.6, spring's first 2.8 to 5.8; at the heels 2.8 to 6.3; eulogies 3.9 to 4.4; the habits 1.2 to 1.9 own (whittling,
  stones, the sun, the snow) to 3.4 to 4.4; the seven stories' chronicle lines a stage 1 or 2 to 3 or 4, the colonist's
  own 7 to 11 to 10 to 17. What's said to an animal went 10.2 to 5.6 own: a hen hears the 20 bird lines now, not all 54
  general ones, which is the point. Nothing in the area repeats a person's own line past one time in ten, before or
  after (the speaker memory). Found and left: the other 31 stories have their stage lines as they were (two chronicle
  lines or one); untagged words to an animal that suit a beast best ("Hold still, you great lump") are still said to a
  chick now and then; some eulogy lines give the dead habits the game doesn't know ("complained about the food at every
  meal"); a quirk's journal lines when gained are one a voice, drawn with the plain ones; secrets are one a person by
  nature. To look at in game: a dream the night after a drink, a wedding or a raid; a cheerful colonist's colds over a
  season; the long discipline a second time; a hen at feeding time; the herd's leader's last evening.
- 0.109.0, phase 3 of the review: one heart (the player's decisions 4 and 5 of 6 November 2026, with their words on the day:
  "the letdowns also make sense as mood impact, no? But I like strain too though!", "the positive mood buffs should also
  be adjusted accordingly and balanced, not that only the negative ones get adjusted" and "then we might also have to
  look at non-rimfolk moods, right"). Not released: no round in Almanac_WhatsNew.xml, About.xml untouched. DESIGN.md
  "One heart" under Layer 7, the character section's "one reading of who someone is", and "Balance targets" (the pass
  and its numbers); `docs/balance-report.md` regenerated against the code before the phase (`Tools/Balance/before.json`).
  (1) Small letdowns: Rimfolk's five kinds (pressures, setbacks, quiet troubles, nuisances, frictions; told apart by
  class, `SetbackExtension` or name, `SmallMoments.LetdownKind`) are each a short, small mood at the moment they happen
  (`SmallMomentMath`: 3 points and half a day at most), and what they would weigh beyond it wears on the heart
  (`StrainSource.Letdowns`, the end of the enum, 0.6 a mood-day, 1.5 a day at most at 100%, their traits counted). It
  happens in the hook every memory passes (`Patch_GoodTimesFade`'s prefix, `SmallMoments.Take`): a moment
  (`Thought_SmallMoment`, saved with its words, size, cause and who it was about) is given, and the memory goes on with
  `moodPowerFactor` 0 for what else it does (a friction's opinion, what dilemmas, arcs, speech and journals look for);
  the heart's memory rules skip it (`SmallMoments.Converted`). A lasting one (a `Thought_Pressure`, a `Thought_Setback`)
  gives no mood: a moment when it begins, gets worse or holds another day, and strain every hour it holds
  (`SmallMoments.Hour` from `HeartLogic.Strain`; what the hour last saw is unsaved, and the first look after a load gives
  no moment). (2) Small joys, the mirror: Rimfolk's good memories no council answer gave (unscaled), of 5 points and 2
  days at most as written, not a life goal or a dream, and the varied table; relief through `StrainRelief.Joys` (the end
  of its enum), 3 a day at most (`PawnHeart.todayJoys`, saved). (3) One row a side: two defs, `Rimfolk_SmallLetdowns`
  and `Rimfolk_SmallJoys` (`Rimfolk_Thoughts_SmallMoments.xml`, stacking 1, so the row adds up), on the Mood tab and the
  game's Needs tab; the row's tooltip lists each one (`SmallMoments.Describe`), its "why" says what they were, its advice
  is the fixes of the letdowns behind it (`SmallMoments.Advice`, `MoodInsight.Advice`), else its own hint
  (`Rimfolk_MoodHints_SmallMoments.xml`). Feelings read each moment's own memory at the moment's size and a lasting one at
  its full size while it holds (`Feelings.Feed`); a quiet trouble's moment passes sooner in company too. (4) Every small
  mood, anyone's: each hour the heart adds up the small moods on a colonist's mind, the game's and other mods' too, by
  size and length (6 points, 3 days, or lasting: `SmallMomentMath.IsSmall`), past 8 points of bad ones 0.02 strain an
  hour a point and past 12 of good ones 0.02 relief, through the same source and relief and their caps; the heavy kinds
  with sources of their own are left out (`SmallMoments.Heavy`: the heart's memory rules, hunger, rest, recreation, pain,
  the dark, its own strain mood, moods that feed a feeling that wears, `Feelings.FeedsWearingFeeling`). The game's and the
  mods' moods are never changed and keep their own rows. The heart's page names what wore on them and eased them
  (`PawnHeart.letdownsWhy`, `joysWhy`, saved words like `natureWhy`; `HeartView.SourceText`, `ReliefText`, the help line
  `CC_Heart_Help_Letdowns`). (5) Good things grow ordinary, one rule in place of four (`Moods/GoodLife.cs`, which
  `GoodTimes.cs`, `SmallJoys.cs` and `GoodTimesMath.cs` were folded into and removed): no standards cut on rewards or the
  game's comforts (`Standards.PenaltyFactor` for letdowns alone; `CouncilGameComponent.GiveThought`, `DilemmaImpact`),
  no fading sooner, no budget; the line 45, 35, 25 and 15 by standards (`GoodLifeMath.LineStep` 10, was 5), the spread by
  how ordinary each is as before. One line in a good mood's tooltip (`GoodLife.Note`, `CC_Ordinary_Note`), one sum under
  the Mood tab (`GoodLife.Readout`). (6) Character as the one nature: the colony's ways read the eight sides
  (`PersonaMath.WayLean` in `CharacterTracker.Affinity`), so who minds an answer follows character and its marks; what the
  ways' table read and character didn't is in `Rimfolk_PersonaWays.xml` (the industrious, a bloodthirsty temper, the
  workaholic); a mod's personal trait leans the ways only through character where a nature def reads it
  (`ModPeople.OnlyTraits`); the outsiders' fighting temper reads character too (`PersonaMath.FightTemper`, replacing
  `OutsiderMath.Temper`). (7) Settings: one toggle, "Good things grow ordinary" (`goodGrowsOrdinary`), merged quietly
  from the four (`smallJoysDiminish`, `standardsCoverComforts`, `goodTimesFade`, `usedToGoodLife`): on if any of them
  was on, off only if all four were (`GoodLifeMath.MergedSetting`, done once, `ordinaryMerged`); the old keys' words stay
  in the language files unused. The Demanding preset presses a step harder (downsides 85% more, rewards 70%, were 15%
  and 80%). Old saves: letdowns and joys given before keep their moods and run out as they were; lasting ones become
  moments at the next hour; the heart's lists grow by a source and a relief; memories shortened by the old fading keep
  their time (`MoodInsight.MoodTicks`). With the heart off, the deeper game hooks off or Advisor only, every letdown and
  joy is a mood of its own as before. (8) Balance (`Tools/Balance`): it now plays each colonist's heart hour by hour
  (`HeartSim.cs`, NatureSim's calibrated life, shared), with the moments, the lasting ones, the pile (the game's small
  share in `Rates.GameSmallBad`, `GameSmallGood`) and the one rule split between the game's moods and Rimfolk's; a
  comparison case with letdowns and joys as moods, `BALANCE_QUICK=1` (and `BALANCE_PRESET`) for tuning. Default, 6
  colonists, before and after: Rimfolk's mood well run +2.3 to +5.6, average +0.3 to +2.5, neglected -9.1 to -7.2
  (targets +3 to +6, 0 to +3, -4 to -10); the whole mood 69/62/37% to 69/64/39%; strained, worn, near breaking 1/0/0,
  8/2/0 and 30/17/8% (as moods on the same model 2/0/0, 9/2/0, 23/11/4); the day off 0, 0.05 and 0.74 a colonist-year
  (as moods 0.40 neglected); under the minor-break line, neglected, 34% (as moods 42%); the game's small moods 1, 6 and
  6% of the strain built. Over: Rimfolk's good moods' P99, well run, +17.3 (was +15.4). Files: `Moods/SmallMoments.cs`,
  `SmallMomentMath.cs`, `GoodLife.cs`, `GoodLifeMath.cs`, `MoodInsight.cs`, `ITab_Pawn_Mood.cs`, `Milestone.cs`,
  `RewardInstead.cs`; `Heart/HeartMath.cs`, `HeartLogic.cs`, `HeartData.cs`, `HeartView.cs`, `Feelings.cs`;
  `Pressures/PressureThoughts.cs`, `Pressures.cs`; `Setbacks/SetbackThoughts.cs`, `Setbacks.cs`, `Troubles.cs`,
  `SetbackCouncil.cs`; `Patches/Patch_GoodTimes.cs`; `Causes.cs`, `Standards.cs`, `Standing.cs`, `CouncilSettings.cs`,
  `UI/SettingsCatalog.cs`, `CouncilGameComponent.cs`, `Dilemmas/DilemmaImpact.cs`, `Dreams/Dreams.cs`,
  `Character/Character.cs`, `ColonyTraits.cs`, `Integrations/ModPeople.cs`, `Outsiders/Outsiders.cs`, `OutsiderMath.cs`,
  `Persona/PersonaMath.cs`; defs `Rimfolk_Thoughts_SmallMoments.xml`, `Rimfolk_MoodHints_SmallMoments.xml`,
  `Rimfolk_PersonaWays.xml`, `Almanac_OneHeart.xml` (two pages: small letdowns and small joys, good things grow
  ordinary); keys `Rimfolk_OneHeart.xml`; the standards, heart, pressures, setbacks and ways pages rewritten (the old
  rules kept as "until November 2026"). Tests: `SmallMomentTests` (new), `GoodLifeTests` (the line, the merged setting),
  `PersonaTests` (the ways from character), `OutsiderTests` (the fighting temper), `NatureTests` and `ThoughtClassTests`
  (the new kinds and class). Found and left: a lasting letdown's first moment after a load waits for it to change or a
  day to pass; Rimfolk's good moods at a high point are a little higher than before though the whole mood isn't; feelings
  now see a converted letdown at its moment's size, so frustration from setbacks is a little milder. To look at in game:
  the two rows and their tooltips (and the game's Needs tab), a run of the same meal on the heart's page, a well-run
  colony's good moods each saying "grown ordinary", and the colony's ways' mood after a mark moves someone.
- 0.108.0, event variety: new questions, events, situations and camps (the player's request, 6 November 2026): "you can add more
  'event variety' to existing systems that are more flexible to new additions". New content in the existing frameworks
  only, at their existing rates; no version of its own (its points go with the next round). Each piece, what it plays
  out, what lasts, how often:
  - The cold snap's hours (`Rimfolk_DlmE_ColdSnap`, `DilemmaWorker_ColdSnap`): the game's cold snap, freezing outside,
    two or more out working in it by day, A the one with the coldest bedroom. Short days (a new timetable plan,
    `AnswerShiftMath.Cold`: abed 5 to 8, to bed 19 to 21, put back when the cold snap ends), a fire kept in A's room
    (40 wood, `DilemmaEffect_KeepFire`), or carry on (the cold is the game's own). Lasts until the cold snap ends; weight
    1.4, 20 days apart, only in a cold snap.
  - Blight in the fields (`Rimfolk_DlmE_Blight`, `DilemmaWorker_Blight`, `DilemmaEffect_CutBlight`): the game's crop
    blight in two or more plants of the colony's fields. The blighted plants marked to cut (the game's designation), or
    that and the rest of those fields that can come in now marked to harvest, or let it spread as the game has it. The
    crop saved or lost; weight 1.5, 15 days apart, only while there is blight.
  - The good weapon (`Rimfolk_DlmE_GoodWeapon`, `DilemmaWorker_GoodWeapon`, `DilemmaEffect_TakeUp`, `StoredWeapons`): a
    good-or-better weapon in storage two colonists would be better armed with (`VarietyTwoMath.Upgrade`, skilled at its
    kind, able to equip it). The one chosen walks to the stores and takes it up (the game's equip job), or it stays.
    Who carries it from then on; weight 1.2, 40 days apart.
  - The animal left behind (`Rimfolk_DlmE_MasterGone`, `DilemmaWorker_MasterGone`, `DilemmaEffect_NewMaster`): an
    obedience-trained animal whose bonded person died in the last 12 days, no living master. A new master (the one
    closest to the dead, or the best handler), whom it keeps at the heels of a day (`TimedKind.Follow`), or the colony's.
    The master stays; weight 1.3, 30 days apart. Six small memories in `Rimfolk_Thoughts_Variety2.xml`, the rest reused.
  - The herd crossing (`Rimfolk_Evt_HerdCrossing`, `HappeningWorker_HerdCrossing`, `DilemmaEffect_HuntHerd`): a herd of
    four or more of one kind crossing for an edge (the game's herd migration). A few of the grown ones (a third, two to
    four) or the biggest marked to hunt, or let them cross; the game's own risk that a hunted beast turns is named in
    every letter and under the answer. Meat or nothing; base chance 0.8 in Rimfolk's capped share of the storyteller's
    pool, 20 days apart, only while a herd is on the map (rare).
  - A blight in the region (`Rimfolk_WS_Blight`, data only): spring to fall, the game's crop blight 2.5 times as likely
    at home, a neighbour's food bought at twice its worth or its plain worth, smoke over their town, a burned-out family
    fed at the gate; ends by what the colony did. Weight 0.7, 150 days apart, 10 to 16 days, kind hunger.
  - Medicine gone scarce (`Rimfolk_WS_MedicineScarce`, data only): medicine sold at a scarce year's price or given, a
    herb-seller's healroot bought dear, hedge healers oftener and travelling doctors rarer. Weight 0.6, 200 days apart,
    12 to 18 days, kind scarcity (never with iron gone scarce).
  - Woodcutters (`Rimfolk_Camp_Woodcutters`): a new land need, `woods` (`CampKindsLand.WoodsTargets`, 30 grown wild trees
    clear of the colony at least, `CampLandMath.WoodsEnough`); they pitch by the thickest stand and fell 1.5 trees a day.
    Asks: their wood (60 logs to the gate), the axe (Plants lesson); their request a hot supper (4 meals to the camp);
    their woodpile left for the colony in peace (goods, not a gift, so no calendar day). Weight 0.6 among the kinds that
    fit, oftener in a hard winter, a fire or a rebuilding.
  - Herb-gatherers (`Rimfolk_Camp_HerbGatherers`): wild herbs gathered at dawn and a share left (help "medicine"). Asks:
    their herbs (4 herbal medicine to the gate), the herbs (Medicine lesson); their request to walk the colony's fields
    (`CampComes` to the fields, `CC_CampErrand_Arrived_herbwalk`, then their gathering). Weight 0.6, oftener while
    medicine is scarce or sickness is about. Every age by the people's own (root-diggers to a survey party).
  - Rates, from `Tools/Balance` (120 runs a setup, 6 colonists, average play, before and after): questions a year 13.3
    and 12.9, events 6.5 and 6.5, world events 1.1 and 1.1, questions a day 0.50 and 0.49 (within the runs' noise);
    `--scenes` (10 colonists, well run): 52.5 and 52.4 staged a year, 5.1 gatherings and outings a week either way; no
    new answer gathers people (the dilemmas that may: 24% of the rest before and after). Camps come on their own clock
    (about one in 35 days), unchanged: a kind is picked among those that fit. Tests: `VarietyTwoTests`; the play-out
    audit counts the four new effects (0 candidates); README and the Almanac's council, world events and camps pages.
  - Needs a look in the game: the herd crossing (rare: the herd is on the map for hours), a fire laid in a cold room in a
    spacer colony, the woodcutters' stand on a small map with a big home area.
- 0.107.0, phase 1 of the review: one record (the player's decision 3 of 6 November 2026: "the chronicle becomes the colony's
  view of the history, every question records how it went, and the year is told once"). Not released: no round in
  Almanac_WhatsNew.xml, About.xml untouched. (1) The chronicle is the history's view. The save at day 128 showed the
  council window's chronicle (a list of its own, 300 lines) back to day 41 only while the region's history still had
  day 1, the same lines kept twice (64 KB and 111 KB). Now `AddChronicle` writes the history and nothing else (the one
  call every one of the 72 files that tell the chronicle already used); Colony → Chronicle reads the history
  (`Chronicle.Lines`, built again when it changes): every line told for it, every answer, and the colony's own
  milestones written elsewhere with words (a brigands' hold found, a pact); never a deed, a tie or a far town's news.
  Which and how is `ChronicleMath` (pure): the chronicle's twenty kinds, a situation's `world:began`/`ended` (the
  region's other `world:` kinds stay its own), an answer under its question's kind. The history's own keeping applies:
  the last sixty days line for line, older seasons' small lines as one each in words of their own ("Harper made 5
  wishes. (Day 12 to Day 26)", `CC_ChronFold_*`), milestones as told. On the save's history the view holds 394 lines
  from day 3 (32 before day 41), against the old list's 300 from day 41, and adds the brigands' hold Macy saw. Every
  reader moved: the overview's lately, the Wishes and Ambition pages' earlier lines, the annals, the year's letter, the
  calendar's backfill (an answer's record found by its question), the storyteller's tale, the travellers' reason to
  visit, the faith's "told already" (`History.Of`), the clock window and the self check. `Caps.Chronicle` is gone.
  Save handling: the old list is read on load and never written (`CouncilGameComponent.chronicle`, like the guest kin);
  at every load the ledger takes it into the history (`HistoryBook.TakeChronicle`) and lets it go: a line comes in
  unless the history has it (the same tick and words, as often as it is there, or a season's summary of its kind and
  person whose days hold it, `HistoryBook.Holds`, so nothing is counted twice); no error is logged; `chronicleRead` is no
  longer saved. On the player's save every line of the list is in the history already, so nothing comes in and the next
  save is 64 KB smaller. (2) Every question records how it went (`Dilemmas/Answers.cs`, pure `AnswerMath.cs`): the 23
  question letters (the review counted 21: dilemma, event, world event, camp, calendar day, meeting, election,
  brigands' hold, hamlet, journey, and the 13 of diplomacy) write an `answer:<source>` event: the question, the answer,
  the follow-up's first sentence, how it went, a word on it, and whether nobody answered; its line is the chronicle's
  ("{QUESTION}: the council chose "{ANSWER}"", one wording for dilemmas and events; a meeting's choice and a kept day
  keep their own lines, which become the answer's). What the answer set going on the map is watched (`AnswerWatch` in
  `WeekMemory`, the dilemma log's way: every 250 ticks while one is): it took place (with the play-out's note), never
  came about, or the gamble went wrong, and the chronicle line says so ("It took place."; hover for the whole record).
  Small answers (an event that was never chronicled, letting the vote run, waiting an hour at the gate, letting a day
  pass) are kept but not chronicled. The letters' options are wrapped (`Answers.Recorded`, `RecordedAll`; the stock
  close and put-off pass), the rest call `Answers.Record` where they resolve; backing a candidate or calling a seat off
  is recorded where it's done (the letter or the Decrees page). Council → Dilemmas lists every answer, the dilemmas
  from their own log (unchanged, with what each answer was to do) and the rest from the history, by day, with the same
  filters. The old "chose" chronicle lines are gone (the record is the line). (3) The year is told once
  (`World/YearLetter.cs`, `YearLetterMath`): one letter on the landing's anniversary, the founding day, written by the
  annalist: the people (the annals' figures, `AnnalsTracker.PeoplePart`, now counted from one landing day to the next),
  the land and the world (the year letter's part), the founding (`HappeningWorker_Founding.YearFacts`: here since the
  first days, the dead, the colony's name, its customs), the author's last word. The annals no longer write at the
  calendar's new year; the founding event keeps only who keeps the day tonight (the last of the first, the Founder, the
  first child grown); one chronicle line; the Chronicle's header has "The second year" (an older save's "Annals of 5501"
  stay, `AnnalsEntry.landYear`). A year counted from part of the way through (an older save whose annals ran by the
  calendar, the mod added midway) says since when. (4) Less noise: a fight's comradeship told only when something
  changed (new comrades, who carried whom out; the roster after every raid is gone, `SocialWeb.Comrades`); an election
  one line with every seat and everyone seated in its history (`Offices.ChronicleSeats`); the same years marked on the
  same day one line; a day kept from its letter one line, and the founding event's answer the founding day's. Keys in
  `Rimfolk_OneRecord.xml`. Save estimate: 2,447 / 2,904 / 3,079 KB after one, two, five years before, 2,377 / 2,818 /
  2,992 after (the list gone, the history a little fuller with answers). Tests: `OneRecordTests` (new: what the
  chronicle shows, the season's words, a season and more reaching day 1 with every line counted, every letter's
  source, the values, what came of it, the year's day and parts, the founding lines shared out with none twice, the
  noise), `HistoryTests` (the view over five years, nothing the old list showed lost, an older list taken in where the
  history already folded). Docs: DESIGN.md ("One history": the chronicle is its view, an older save, the answers' log,
  the year told once, less noise; the save table), README (the year's letter, the Chronicle), the Almanac's names and
  annals page and the dilemmas page. Left: "daily numbers renamed" (`comp.history`, the overview's graphs) from the
  holistic review's phase 1; the office templates and "4.1 days" in journals (the systems review's record list). To
  check in game: the Chronicle on the player's save (day 3 at the bottom, older seasons summed, the brigands' hold);
  a dilemma and an event answered (one line each, "It took place." once the feast is held); a pact or truce answered
  (on the Dilemmas page and in the chronicle); an election (one line); a raid with a rescue (one line, only then); the
  next founding day (one letter in three parts, the founding event's letter without the year, one chronicle line).
- 6 November 2026, the player's decisions on the review: all seventeen as recommended, with one nuance on the
  letdowns (both a short mood at the moment and strain for the grind): `docs/review-2026-11/decisions.md`. The
  reports are beside it (`staged-scenes.md`, `systems.md`, `whole.md`). Phases 1 (one record) and 3 (one heart) run
  first, side by side; 2 and 4 follow phase 1; then 5 and 6; then harm parts 2 and 3, schemes and stage 3.
- 0.106.1, phase 0 of the review: what the save showed (the systems review and the holistic review's phase 0, 6 November 2026,
  against the player's save Atia at day 128: the plain fixes, `[fix]`; what needs the player's word is left for them).
  No version of its own: its points go with the next round. (1) The election results left seats out: the 5502 letter
  named 7 of 11, and Barr, "standing unopposed for quartermaster", was never seated, so a by-election four days later
  made Stakius quartermaster a day and a half after she joined. Barr held keeper of days; the candidates are chosen seat
  by seat with a likely winner out of the running for the next, so keeper went unraced while Barr stood elsewhere, and
  the count put everyone holding a seat not voted on out of the running: the seat Barr stood for had nobody to win it
  and was dropped without a word (Macy, steward, likewise lost reeve and marshal). Now only one who doesn't stand in
  this election is out (`ElectionMath.OutOfRunning`, `OfficesElection.CountOf`), as the instant vote always had it: the
  seat they win is theirs and the old one is let go once every seat is filled. The results name every open seat
  (`ElectionMath.UnvotedSeat`, `Offices.Finish`, the instant vote's too): a holder who keeps a seat nobody stood for, a
  seat empty because its holder won another, and the seats nobody stood for in one line ("three seats among the
  elders"); a by-election names only its empty seats, and one for several seats says seats
  (`CC_Elect_AnnounceByTextMany`, `_CeremonyByTextMany`, `CC_Election_ByTextMany`, `CC_PM_Appoint_ByTextMany`). Keys in
  `Rimfolk_Election_Seats.xml`. The wait before a newcomer can stand is the player's word. (2) Answers that didn't play
  out: 9 of the save's last 30 (`PlayOutTracker.results`). The shearing day was asked a day after the sheep were shorn
  (their wool at 0.79 a week later, ten days a coat) and its party found nothing to shear: a herd custom that shears or
  milks now needs an animal nearly full (`HerdConditions`, `AnimalFacts.ReadyToShear`, `ReadyToMilk`,
  `AnimalMath.ReadyToGather`, nine tenths). The planting day's first row found "nothing that needed doing": a work party
  asked only the forty cells nearest its spot, so a field sown near the spot hid those still waiting for seed; now the
  forty nearest that could hold the work at all (`PlayWork.MayNeed`: a cell without its crop, a ripe plant, an animal
  ready for the shears), and a sowing whose fields were sown already says so (`CC_Play_Why_Sown`). The reasons told:
  "something else had everyone's attention" stood for a fight, half the colony under arms and someone bleeding as much
  as for another gathering (`MeetingAttendance.Bar`, `PlayOutMath.GatherWhyKey`, `CC_Play_Why_Drafted`, `_Bleeding`),
  and "nowhere to hold it" for a place in the open the weather kept them from, in a desert (`PlaySpots.KeptByWeather`,
  `PlayOutMath.PlaceWhyKey`, the gathering's and the visit's). Not bugs: an organizer never free (a speaker the words
  name, kept by work the whole evening), the fire still being laid when the time ran out, and nobody free are waits that
  ran out, their words true; which answer each of the nine was isn't kept (the results hold ids only), so two "nowhere"
  and two "something else" are read from the code. Keys in `Rimfolk_PlayOut_Why.xml`. (3) Words untrue or broken: the
  officers' `elected` lines said acts nobody saw ("went straight out to the gate", "walked the fields the same
  afternoon" when the count is in the evening, "spent the evening at the altar"): rewritten in place as what they mean
  to do (26 lines, textcheck clean). The herd called a chick "the first calf of spring" and an old chicken "gray at the
  muzzle": the young's word from the kind's first life stage (`HerdStoryMath.YoungWord`, "spring's first born" for a
  kind with none), hatched or born by whether the kind lays eggs, the leader's ways and old age by the body
  (`HerdStoryMath.BodyOf`: beast, bird, other; `CC_Herd_Leader_Pens`, `CC_Herd_LastSeason_Bird`, `_Other`), the journal
  tagged `body:` and `hatched`/`born`; three shipped journal lines retired (`ShippedLines.Retired`: the calf twice, and
  "I sat with ... tonight" written before the sitting), new ones in `Rimfolk_Pools_HerdStory_Kinds.xml`, the thought
  "the first born of spring"; keys in `Rimfolk_HerdStory_Kinds.xml`. A life goal's spark quoted a memory's label (after
  "ate fine meal"): words for each of the 25 memories that spark goals (`CC_Asp_Spark_thought_DEFNAME`,
  `Rimfolk_Aspirations_Sparks.xml`), "after what happened" for any other. A wish in mid line was capitalized ("wished
  for To see something of the world"): `Wish.LabelInline` in the chronicle and the messages that put it mid-sentence,
  and the three wish labels that began with "to" are things to wish for ("a trip out into the world", "a bed shared
  with", "some mining to do"). "(s)" in story text: the chronicle's anniversaries and passed-over, the annals' raids,
  wishes and questions, the year's letter's beast and road are said for one and for many (`Texts.CountKey`,
  `_One`/`_Many` in `Rimfolk_StoryWords.xml`); the year's letter's "29 chronicle line(s)" and "0 of them born" have
  words of their own. Chronicle lines begin with a capital whatever label leads them (`ChronicleEntry`); our ways and
  the colony's traits put a sentence in the chronicle, not the letter's label ("Known as Walled":
  `CC_Ways_KnownChronicle`, `_RenownedChronicle`); "A silver rush ended in over" was the ending's key as a word: its own
  word where it has one, else "is over" (`YearLetter.WorldOne`). A conversion was told twice a day apart (the faith's
  line, then the mark's): the mark keeps its journal line and leaves the news and the chronicle to the faith when it
  told it (`Faiths.ToldConversion`, read from the chronicle). (4) People repeating their own lines: 24 of 630 journal
  lines were the writer's own again, up to 49 entries apart; the line memory was per pool, shared by everyone and lost
  at a load. Now each speaker has a part (`LineMemory`): their last twenty picks of any pool, and the pool's lines still
  in their journal, read back from the journal itself (`LineMemory.Wrote`: the line's own words in order, anything where
  it names someone, a name's colour passed over, at least 12 letters of its own; worked out again only when the journal
  changes), so nothing new is saved and a load forgets nothing. `ToneLines.Pick` takes the speaker (`Flavor.FromPool`
  and every call that picked by someone's tone), the conversations' `PickLine` too (their last twenty); a journal never
  takes the very same words twice while it holds the first (`JournalTracker.WriteText`). A tone of one or two lines
  draws together with the plain ones (`LineMemory.ToneDrawsAlone`): the cheerful "Laid low by a bug" was every cheerful
  colonist's cold. (5) The marks' last fight was day 114 though the colony fought on day 124: not the load bug (the
  marks' tracker saves its fight itself). The raid on day 124 came from the brigands' hold (`RaidersCamp.TryRaid`),
  which isn't told to the storyteller, and a fight counted as won only after the storyteller's big threat
  (`Battle.WasThreat`): no victory, no marks, no deeds, no calendar day for any raiders' camp's raid. Now every raid
  Rimfolk sends is noted on the home's fight (`Raids.Send`, `SocialWeb.Notify_RaidSent`, `Battle.raidSent`, saved) and
  counts (`BondsMath.WasThreat`). (6) The variety report measures one person over a year: "own" (the effective lines one
  person draws from, the unlucky one of ten), "again" (the share of their lines that repeat one of their own with no
  memory) and "left" (the same with the speaker's memory: a repeat only when every line that fits is theirs already),
  `--by-speaker` to rank by it, flagged "repeats" past one time in ten; its pick follows the tone rule; the draws of the
  journal pools the save shows most of are read from it (they were "rare" by hand, and that is where the repeats were).
  At the top now: the elected, new-goal and ailment journals (thin for the colony, 3 to 11% again with no memory, none
  left with it). Files: `Offices/ElectionMath.cs`, `OfficesElection.cs`, `Offices.cs`, the office defs;
  `PlayOut/PlayWork.cs`, `PlayGroups.cs`, `PlayWords.cs`, `PlayOutMath.cs`, `PlaySpots.cs`, `PlayOutTracker.cs`,
  `PlayOuts.cs`, `Ambitions/MeetingAttendance.cs`, `Animals/AnimalDilemmas.cs`, `AnimalFacts.cs`, `AnimalMath.cs`,
  `HerdStory.cs`, `HerdStoryMath.cs`; `LineMemory.cs`, `Tone.cs`, `Flavor.cs`, `Journal.cs`, `Social/Talk/Talk.cs` and
  the callers that pick by tone; `Social/Battle.cs`, `SocialWeb.cs`, `BondsMath.cs`, `Happenings/HappeningHelpers.cs`;
  `Text/Fill.cs`, `Chronicle.cs`, `CouncilDynamics.cs`, `CouncilGameComponent.cs`, `Annals.cs`, `World/YearLetter.cs`,
  `Character/Character.cs`, `ColonyTraits.cs`, `Persona/Marks.cs`, `Faith/Faiths.cs`, `Aspirations/Aspirations.cs`,
  `Wishes/Wish.cs`, `WishEffects.cs`, three wish defs, `Lean/ShippedLines.cs`; `Tools/variety_report.py`. Tests:
  `ElectionTests` (every seat counted and told), `BondsTests` (a raid of Rimfolk's own is a real fight), the line
  memory's in `Program.cs` (a speaker's twenty, the journal read back, the very same words, the tone rule, another
  game), `HerdStoryTests` (bodies, the young's word), `FellThroughTests` (new: the shearing, the sowing, the reasons),
  `SaveWordsTests` (new: counts, sparks, wishes, the chronicle, the year's letter, the officers' lines). Docs: DESIGN.md
  ("Elections: every seat accounted for", "Context- aware writing", the marks' fight won, the work party and "Why one
  didn't happen, said as it was", "Words by the animal's kind" and "Story text in words" under Facts), README (the
  election letter). Left for the player: a newcomer's wait before standing; running on a record and promises judged; one
  name for the colony; one year letter; epithets and goals that churn; one petition for a shared want. Found and left:
  the first-born journal's shipped warm, plain and blunt lines still speak of the visit to the pens before it happens
  (they're written as the letter comes); a gathering whose fire can't be laid for want of wood is held in the open air
  under words that speak of the fire; the camps' diary line on the region page keeps its "(s)". To look at in game: the
  next election's results letter (every seat, a holder keeping one, one moved, the empty ones in a line); a shearing day
  only once the coats are full; a planting day's party on a field half sown near its spot; a cheerful colonist's colds
  over a season; a raiders' camp's raid seen off (the victory's line, marks, deeds); the year's letter.
- 0.106.0, harm, part 1: the shared rules and work's hurts (the player's pick, 6 November 2026): "my top ten, then more" from
  `docs/harm-review-proposals.md`. Built: the review's "Rules every proposal keeps" as one service, and 1.2.1, 1.2.2 and
  3.2.1 (marked built there). DESIGN.md, "Harm from everyday life" (the rules, the service's table for the next builders,
  each of the three). Not released: no round in Almanac_WhatsNew.xml, About.xml untouched. The rules are
  `Harm/HarmMath.cs` (pure, `HarmTests`): the setting "Hurts from everyday life" (`everydayHurts`, Heart & health under its
  own heading, 0 to 200%, 100 by default; the light-touch preset 50%) times the storyteller's lean (`Lean`: the geometric
  mean of a threat term, 0.35 at no threats to 1.25 at twice, and a disease term, one over the interval between a half
  and 1.25, diseases off a half; no threats and diseases half as often about two fifths, the hardest a quarter more); the cap (`Bar`): one
  Rimfolk-made hurt per colonist in three days, none while downed, in a sickbed or in need of one (`Harm.Sickbed`: a
  medical bed or resting, bleeding, or ill with something that can kill; a strain still to tend is not), pregnant or
  already fighting (drafted, a fight or a rage, at an attack, hurt or attacking within the hour), babies never, children
  only `HurtClass.Mild`; a wound never takes more than a part has left but one point (`SafeSeverity`). The game's side is
  `Harm/Harm.cs`: `Harm.Scale`, `Chance`, `Roll` (with a pawn, the cap first), `May`/`Bar`, `Injure(pawn, HurtKind,
  HurtWhere, low, high, why, cls)` (the game's own Bruise, Cut, Burn, Crush, Crack or Scratch on a hand, arm, foot, leg or
  torso, a crack on one of their bones, never a vital part, the head or the neck; sized by `IncomingDamageFactor`, so the
  tough take half; the red flash, blood or a puff of smoke), `Ail(pawn, hediff, severity, why, cls, replaces)`,
  `Fight(a, b, why)` (the game's social fight under the cap for both), `Note(pawn, why)` (a hurt that came another way),
  `DecreeLine(def)`. `HarmBook` (a GameComponent) saves `lastHurt` (thingIDNumber to tick, pruned past three days) and
  `drillDay` (a home's last evening drill), both modelled in `Tools/save_estimate.py`, and runs the hour at
  `Sched.RareHarm` 67 (moved from 53 at the merge, which triage took first; a tenth of the colonists each 250 ticks, the colony's part on the first slice; `Pass.RareHarm`,
  `CC_Clock_Pass_RareHarm`, DESIGN.md's ticking table). The feasts read it (`Feasts.HarmScale` is `Harm.Scale`,
  `Feasts.MayBeHurt` asks `Harm.May` for a bout and keeps its old no-fresh-wound rule); a feast's hard blow, a guest's
  sour night and the outsiders' practice bout's hard blow are counted with `Harm.Note`. Decrees carry `hurts`
  (`DecreeHurt`: Drills, Muster, TiredHands) and `hurtsAt` (`WorkHurtKind`), and their card (`UI/DecreePanel.cs`) and
  letter (`CouncilDynamics.Notify_DecreeEnacted`) name it in words (`CC_Harm_Decree_*`). (1.2.1) Drills that bruise,
  `Harm/DrillBouts.cs`: with "Answers play out on the map" on, the drills decree pairs everyone of the colony who can
  fight (grown-ups, free, not pressed, no lord, Melee not disabled) each evening from five to eight, once a day at a home,
  tried again each hour until a pair begins; the militia one pair three evenings in four (`MusterEvening`). Not in danger
  or weather that spoils being out. Places side by side near the yard (`PlaySpots.OpenCell`), the job
  `Rimfolk_DrillBout` (`JobDriver_DrillBout`: to the place, wait for the other, a word, six pulled swings each with the
  game's lunge and sound, a moment after; either leaving or danger ends it for both). One bout in 25 (a marshal halves
  it), decided at the start on one of the pair `Harm.May` lets take a bout: a bruise or cut of 6 to 9 by the age
  (`DrillBouts.BlowOf`: tribal a bruise anywhere, medieval and industrial a bruise or a cut on an arm, spacer a baton's
  burn on the torso); the hurt one's line, the other's sorry, `CC_Harm_DrillBlow`/`CC_Harm_MusterBlow` (three and two
  wordings). The pools `Rimfolk_Pool_DrillCall`, `DrillBegin`, `DrillHit`, `DrillSorry`, `DrillEnd`
  (`Defs/FlavorPoolDefs/Rimfolk_Harm_Lines.xml`, tagged by age and wound, in `ImmersionTests.SpokenPools`). The militia
  is declared in every age now (its `eras` gone, its description and a spacer and an industrial line for it, the
  Almanac's two "not for a spacer colony" gone). (1.2.2) Tired hands, `Harm/WorkHurts.cs`: hours found at the work a
  `TiredHands` decree drives (`HarmMath.WorkOf`: the stove and forge, the bench, building and mining, hauling, the
  field; long hours any, a harvest push the field, a building season the building); from the sixth in a day, the first
  hour found right at it (`WorkHurts.AtIt`: carrying the load, or standing at the work, not walking to it) has one roll, one in 150, twice below 30% rest, once more for a second work decree, half for the tough, half in a spacer
  colony; a wound of 3 to 6 (5 to 8 spacer) by `HarmMath.WorkSlip` (a cut at the bench, a crushed hand at an industrial
  machine, a fabricator's burn, a burn at the heat, a crushed foot, a dropped load, a sickle's cut), the job ended, a
  load dropped, `Rimfolk_Pool_WorkSlip`, `CC_Harm_Slip_<work>` (two wordings each). The review's "one or two per decree
  in a colony of ten" was a miscount against its own anchor; the anchored rate was kept (about a third of a slip per
  five-day decree in a colony of ten, rested; twice with nobody sleeping enough), and said so in the review and DESIGN.md.
  (3.2.1) Working through it, the same file, with the everyday ailments on: a strained back or a sprained ankle,
  untended and out of bed, counts its heavy hours each day; from the fourth, the first hour right at it has one roll for it (once each, session memory):
  one in ten, never while the rest-when-ill decree holds. A back tears (`Council_Ail_TornBack`, `Rimfolk_Hediffs_Harm.xml`:
  a new everyday ailment with no causes, 3 to 5 weeks, sharper pain, half the carrying; its mood in place of the strain's,
  `Council_Ail_TornBack_Mood`, hinted with the strains; ROADMAP's count 56); an ankle gives (a bruise of 3 to 5 on the leg,
  or a crack of 2 to 4 on its bone one time in three; the sprain stays). The load drops, a short `Wait_MaintainPosture`
  (sitting down hard), `Rimfolk_Pool_WorkedThrough`, the colony's best doctor within talking range says
  `Rimfolk_Pool_DoctorsWord`, and `CC_Harm_TornBack`/`AnkleFall`/`AnkleCrack` with the ailment's doctor's note. Almanac:
  "Decrees" gains "Decrees that can hurt", "Ailments and illnesses" gains "Working through it, and hurts from everyday
  life" and a torn back row (no keyword changed). Dropped: nothing of the three leaned on council tasks or the keeper of
  secrets; "the line" as a place for the drills is the yard only (the defence lines are left for 4.2.1's musters). For the
  next builders (2.4.1, 2.2.1, 2.1.1, 3.1.1; 1.4.1, 3.3.1, 3.5.1): roll the base rate with `Harm.Roll(pawn, rate, cls)`,
  give it through `Injure`, `Ail` or `Fight` (or `Note` what the game gave), say it in one sentence and tell it through
  `Tell` in two wordings; a fight is `HurtClass.Fight`, never a child. To check in the game: the drills' pairs in the
  yard at five, the swings, a real blow by dev rolls (the hurt one walking to the doctor), the militia's one pair, the
  decree card's line; a slip at the bench on a long-hours day, the dropped load, the message; a strained back worked
  through hauling turning into a torn back, the sit-down and the doctor's word; the setting at 0% stopping all of it.
- medical triage with Move the Patient (the player's request, 6 November 2026, with a save): "i have a hospital bed with
  high criticality and set as surgery bed. when a pawn is injured they always take one of the other two beds though,
  even though the hospital bed is free and better. i want them to always take the best bed, and once a patient with a
  more serious injury arrives, the previous pawn should be put into another bed via 'transfer to bed' feature to free up
  the best bed". Read in the mod (`konstantynopolitaneczka.movethepatient`, assembly `PatientBedTransfer`, its 1.6 and
  1.7 DLLs the same): `MedicalBedSelector.Pick` keeps surgery beds for patients with a pending operation, so an ordinary
  patient never gets one while another medical bed is free (and a non-downed one is turned out of it with
  `blockSleepInSurgeryBeds`); `PatientTriage.ChooseByPriority` gives a patient of severity S the lowest-priority bed at
  least S (higher beds kept for worse cases); `CalculatePatientSeverity`: Critical for heavy bleeding, near death,
  health under 15%, a life-threatening condition or downed and not awake, High for bleeding, health under 35% or wounds
  needing tending, Normal under 75%, else Low; its transfer is the job `TransferPatientToBed` (patient, bed), which
  forces the patient downed for the carry and tucks them in; `SurgeryTransferScheduler` carries one waiting for an
  operation to a free surgery bed; nothing ever moves a lighter patient out for a worse one. Built:
  `Integrations/MedicalTriage.cs` (by name, every call guarded; anything missing, one warning and no patch; without the
  mod its four patch classes stand down in `Prepare` and the pass is one remainder each 250 ticks), its pure rules
  `MedicalTriageMath.cs` (`MedicalTriageTests`). (1) Best bed first: postfixes on `RestUtility.FindBedFor` (five
  arguments) and `FindPatientBedFor`, the methods the mod's own postfixes adjust, after them (`HarmonyAfter`
  `patientbedtransfer.mod`, `Priority.Low`), only on a medical result for one of the colony's people (not an enemy
  unless a prisoner, not a baby, not a slab-bed faith), standing aside in the mod's rerun without surgery beds
  (`MedicalBedSelector.Busy`): the best free bed they may use by the player's priority (Normal for all with the mod's
  priority system off), then `Quality` (the bed's `MedicalTendQualityOffset`, its `ImmunityGainSpeedFactor` above 1,
  the room's `InfectionChanceFactor` below 1, in steps of 0.005), then the nearest; each candidate passes the call's own
  `IsValidBedFor`, the mod's xenotype rule and no more danger than the bed chosen; a surgery bed counts for an ordinary
  patient only while nobody waiting for an operation could use it (`SurgeryBedOpen`); one waiting keeps the surgery bed
  the mod gave them; someone already in a medical bed that counts keeps it; a bed kept by a move is the worse one's
  alone. A patient given a lesser bed than the best of their kind marks the map to be looked at every 250 ticks for an
  hour. (2) Making room: `GameComponent_MedicalTriage` at `Sched.RareTriage` (53, new: the hour's look at tick 53 of
  the hour, `Pass.Triage`, and the 250-tick one only while a move or a marked map waits, `Pass.RareTriage`; LeanTests'
  lists have it). Patients in the colony's medical beds and those downed on the floor, the worst first by the mod's
  severity, an operation first among equals; for each, the beds ranked for them (`CompareFor`: a surgery bed first for
  an operation), and a better one than they have or would be carried to, held by someone they outrank (`Outranks`:
  Critical over High over Normal over Low, never an equal; in a surgery bed an operation over an ordinary patient, and
  nobody over one waiting for theirs) who may be moved (`MayMove`: not operated on, tended or with a doctor on the way,
  not in a mental state, labour or carried, another free medical bed of their kind, `SameKind`, not moved in 4 hours,
  `CooldownTicks`): the nearest awake colonist with doctoring on, not busy (drafted, a lord's duty, carrying, tending,
  rescuing, firefighting, an operation, a player's order, an uninterruptible job), who passes the mod's strict
  `CanPawnPerformTransfer` and can reach and reserve both, gets the mod's job to the best other free bed as its
  "Transfer to bed" gives it (`playerForced`, `MarkTransferActive`). One move per map; the bed kept for the worse one
  until they lie in it, no longer need medical rest, or 4 hours (`GiveUpTicks`). Once the lighter one lies in the new
  bed, the worse one comes in by the game's rescue (best bed first sends them there), by a transfer from a lesser bed
  or the floor with nobody coming, or on their own feet; everyone carried by triage is held 4 hours. Nothing saved
  (a load forgets a move under way and who was moved; the carry itself is the game's job and goes on). (3) Shown: the
  carrier says one sentence as they set off (`Rimfolk_Pool_TriageRoom`, `_TriageSurgery`, 17 and 15 lines by the
  colony's age, `ImmersionTests`); `Tell.Minor` (Health) once the lighter one lies in the new bed,
  `CC_Triage_Moved_<Age>` and `CC_Triage_MovedOp_<Age>` in two wordings each, with the room they went to in the age's
  word when it is a hospital (`CC_Triage_Ward_<Age>`: the healer's hut, the infirmary, the ward, the sickbay); the bed
  kept (`CC_Triage_Kept`, `KeptOp`) and the patient moved (`CC_Triage_MadeRoom`, `MadeRoomOp`, 6 hours) say why under
  their inspect text (postfixes on `Building_Bed` and `Pawn.GetInspectString`, a dictionary look only while a line
  exists). Keys in `Rimfolk_MedicalTriage.xml`; setting `medicalTriage` (Heart & health, Recovery, on by default, needs
  the deeper hooks); the mod's "by distance" mode stands triage down. The player's case: with the surgery mark, the
  injured now take the hospital bed (High) first while nobody waits for an operation; a newcomer worse off (open wounds
  against someone tended, downed and unconscious or bleeding out) has the one in it carried to a plain bed if one is
  free, then takes it; someone waiting for an operation has an ordinary patient carried out of it. Without the mark the
  same by severity alone; an operation has no claim of its own. Both plain beds taken: nobody moves. Limits in the mod:
  its "Recommended" bed on the "Transfer to bed" gizmo is still its reservation scheme's; its own surgery transfer only
  fills a free surgery bed (triage is what frees it); its severity is all there is to go on (a tended patient often
  reads Normal or Low however long they still need the bed); the transfer forces the patient downed for the carry and
  ends their job. Docs: DESIGN.md ("Medical triage with Move the Patient", the ticking table at 53), README (Recovery,
  doctors and midwives; Optional mods; the settings), the Almanac's page `Council_AlmMod_H_MoveThePatient` (Mods, with
  the mod only), the compatibility table (`Council_Support_MoveThePatient`). To look at in game: the player's save, an
  injured colonist going to the hospital bed; a second, worse, arriving (the carrier's line, the plain bed, the news,
  the inspect lines on the bed and the patient, the second brought in); an operation bill on someone while an ordinary
  patient lies in the surgery bed; no plain bed free (nobody moves); the self-check lists the four patches applied.
- 0.104.3, every colonist's character lost at each load (found by the systems review, 6 November 2026, and confirmed in
  the player's save: the seven elected on day 123.7 had no "elected" mark, the three elected after the last load did,
  and only those three had any nature). `PersonaTracker.ExposeData` saved `people` through a local list: the load
  read it in one pass, and the PostLoadInit pass made the local afresh from the still empty dictionary and built the
  dictionary from that, so marks, the lasting and drifting nature and the stances were gone after every load since
  27 October (dd498d6). Now through a field (`peopleSaved`), cleared after saving and after loading. A test scans the
  whole source for a collection loaded into a local and read only in a later pass (`Tools/Tests/ScribeTests.cs`); this
  was the only one. A save made with the bug has nothing to bring back: characters build again from here.
- 0.104.2, vendettas by closeness, hostages by standing (the player's decision, 6 November 2026): two of the things found outside
  0.104.0's list and named for the player there, approved (CLAUDE.md: the colony is the family). No version of its own:
  its points go with the next round. (1) A vendetta left open at its avenger's death passes to whoever was closest to
  them, a partner, a friend or a relative, by how close they really were, never to blood kin first (DESIGN.md, "A
  vendetta passed on"). The rule is `Secrets/VendettaMath.cs` (pure, `VendettaTests`): closeness is the game's opinion of
  the dead, the social web's warmth between the two (`SocialWeb.Warmth`) and a partner (40) or a relative by blood (20),
  added; close enough at 60 (an opinion of 60 alone, the old friend's mark), the kind at twice that and the bloodthirsty at
  two thirds (`Willing`: their nature only ranked them before); the closest of those who would (`Heir`; was blood kin
  300, a partner 250, a strong tie 150, opinion 60 100, plus opinion). Who may, as before (`VendettaEnds.Candidates`: a
  grown colonist with an inner life, never a lodger, never one already sworn on the same people; only while the war with
  the killers goes on). Fixed with it: the day's look let the social web's bonds with the dead go (`SocialWeb.Daily`)
  before the vendettas passed on (`vendettas.Daily`), so the old strong tie never counted; now the closest is noted in the
  tracker's hour as the death is seen, after those who swear for the dead (`VendettaEnds.NoteHeir`, `Vendetta.heirId`,
  saved: -1 not yet noted, 0 nobody close enough), and passed on at the day's look if they still can (`HeirNow`; else,
  and for an older save, worked out from what is known then). Words: the told line's tag is what they were to the dead
  (`VendettaMath.Tag`: partner, then kin, then friend; it was kin first); the two shipped lines that made it "the family's
  oath" and "a family debt" are retired (`ShippedLines.Retired`), and a relative who was close has words of their own
  (`Rimfolk_Lines_VendettaCloseness.xml`: three told, two in the journal, kin and close besides); the Almanac's vendettas
  (Council, "secrets, vendettas and eulogies") and README say whoever was closest. (2) Their hostage is anyone of
  standing among their people, never by kinship (DESIGN.md, "Wards and hostages"): `Hostages.MakeHostage(f, out
  standing)` makes one of their traders (`TraderKinds`: the trader kinds of the game's trader groups, their caravans) or of
  their seasoned fighters (`FighterKinds`: the stronger half by the game's combat power of the kinds their war bands
  bring, `WardMath.Seasoned`), never a leader's own kind or one that asks a royal title, whichever their people have and
  by a roll where they have both (`WardMath.Standing`); one of their people (`Arrivals.PersonKind`) where they have
  neither or the kind can't be made. By the game's own generation (`Arrivals.Make`), 20 to 50 (`WardMath.StandingAge`,
  never under the hostage's 18), a name of their own, nobody's child (it was their notable's child where the ages fit,
  of the notable's name: `WardMath.CanBeParent` gone). The notable who sends them stays (`Wards.Sender`, the record's
  `notable`, now "who sent them and answers for them"), and why none can be given says so (`CC_Hostage_Why_NoSender`;
  it was the ward's `CC_Ward_Why_NoKin`, "who would send a ward"). The record keeps what they are (`WardStay.standing`,
  `HostageStanding`): Unknown for a hostage given before, who stays as they were, parent and name too. Words: the terms'
  answer (`CC_Hostage_Given_TheirsStanding`, "{HOSTAGE}, one of the traders of {PEOPLE}, will be brought to the gate...";
  the shipped `CC_Hostage_Given_Theirs` loses "one of their kin" in place and is said for one of their people), the letter
  at the gate (`CC_Hostage_Arrives_Standing`), their card (a line, "One of the seasoned fighters of ..."), the talk
  (`them:hostage:trader`, `:fighter`, `WardMath.StandingTags`, set on their hostage spoken to; five pieces in
  `Rimfolk_OutsiderTalks_HostageStanding.xml`: their trader at the colony and missing the road, their fighter watched and
  after a raid, and the custom by their ways); all in `Rimfolk_Hostages_Standing.xml`. The tribes' rite "one of the
  chief's kin given" is "one of the clan given to another fire" (`CC_Form_Hostage_Tribal_Rite`, in place: it also called
  the colony's own hostage at a tribal town the chief's kin), and the custom's tribal line that said so is retired
  (`OutsiderTalkMath.RetiredLines`). The Almanac's diplomacy page and README say one of standing. Left as they are and
  named for the player: a forfeit hostage the colony keeps was their notable's kin, so always wanted back by their people
  (`Ransoms.Notable`, `RansomMath.WantBack`) and a reason for them to hear a truce (`Truces.Held`); one given now is
  wanted back by the roll like any captive (six in ten at peace, three in ten at war) and is no reason; the swearing
  itself (blood kin and partners of the one killed always swear, friends by a tie or opinion: `VendettaTracker.Close`);
  docs/design/stage2-diplomacy.md's forms table still has the old tribal rite (a design doc another agent was revising).
  Unchanged, as asked: a dead colonist's life goal taken up by someone close (`Aspirations.cs`); the escort who walks a
  hostage in (one of their own family, as a ward's). Tests: `VendettaTests` (new: the rule, who may, noted at the death,
  the words), `WardTests` (the standing, the seasoned half, the kinds, the record, the words, the retired custom),
  `OutsiderTalkTests` (the standing tags). Checks: textcheck against 8546bdc reports only the three new files (no
  placeholder dropped in the two lines rewritten in place); almanaccheck against HEAD and 8546bdc clean. To look at in
  game: the dev action "Hostages: a hostage for a live truce or pact" with a people who have caravans and war bands (the
  answer says which; one of their traders or fighters walks in by their kind, the gate's letter, their card's line, a
  talk with them), an older save with a hostage of theirs here (no line on their card, nothing else changed); a colonist
  with an open vendetta killed in a fight with another people, whose sister thinks little of them and whose friend much
  (the next day the friend takes it up, the line and the journal), and one whose closest is their brother (a [kin] line).
- 0.104.1, camps on a tight map (the player's report, 6 November 2026: "i still haven't had one"): the save's camp
  diary (`CampTracker` home `diary`) held 23 tries since day 108, every one "noroom". The player's map is 78% under
  roof (mountain) and 55% fogged; `CampSite.TryFind` tried 260 random cells over the whole map, and only about 30
  cells there meet its rules (open, unroofed, unfogged, 28 to 80 cells from the colony, four fifths of the radius
  open, no zone or home area in it), so it missed nearly every day. Now it lists the open ground first (every second
  cell each way, `OpenCells`) and weighs up to 400 of those, then on a tight map tries again nearer the colony and a
  little less open (`MinFromColonyTight` 20, seven tenths open). Measured from the save's grids (a script outside the
  repo; water, standability and reachability not modelled): about 30 cells pass the first try, about 400 the second.
  Every caller (camps, the war band's camp, the hamlet's escort) gets the same search.
- 6 November 2026, the player's priority: once the work under way lands (harm part 1, vendettas and hostages, medical
  triage with Move the Patient, the stage 3 design revision), nothing new is built until the existing systems are
  reviewed, balanced, improved and integrated: variety, flavour, immersion, writing, impacts, and how often things are
  staged on the map ("gatherings by the fire, a toast to someone": count them across every system, give them a budget,
  make each worth its place). Two audits run first (staged scenes and attention; each system's value and writing);
  their proposals go to the player. Harm parts 2 and 3 (2.4.1, 2.2.1, 2.1.1, 3.1.1; 1.4.1, 3.3.1, 3.5.1), schemes
  (theirs only, the player's choice) and the stage 3 build wait until after.
- stage 3 design revised (the colony is the family; tasks and keeper gone; release order for a fresh start first): 6
  November 2026, docs only, nothing built (docs/design/stage3-many-colonies.md, its revision line at the top). The
  player is close to wanting something new without giving up the colony they have, and may go on in this save or start
  a new game; the design now says what each path gets ("This save or a new game": in this save a fresh start with the
  old colony kept or abandoned, then handed over; a new game needs nothing from stage 3, and Rimfolk carries nothing
  between two worlds). Recast for the colony is the family: the town a handed-over colony becomes is bound to it as a
  colony (`TownKind.Former`, `KeepFor.Former`, the facts `town:former` and `them:former`, the goodwill situation "once
  of [colony]"; no "kin" in its names), its next head is the head's second by office or standing (the code's
  `NotableRole.Heir`, titled by age as the headman's second, the reeve's deputy, the deputy mayor, the deputy
  administrator), set as the people's leader in the game's own succession (`Faction.TryGenerateNewLeader`, which would
  otherwise make a stranger; read in the 1.6 code and added to the API diff list); the town has no founding families;
  a colonist's own people in the town (a spouse, a child, a close friend) is a reason of its own beside the moving-on
  questions. Renown: the colony's name stays one across its homes; a handed-over home's deeds stay in it (`Renown`
  reads only `Homes.All` today, so the colony's own deeds there are to be read under every colony on the ledger's
  record), what those who stay hold now leaves with them, and a former colonist's own name is said in their town's
  letters and talk (`Renown.Own` reads only the colony's people today); founders carry their own names and the colony's
  names for them from one colony's days (`EpithetMath.ForColony`). Banishment: each home's council banishes its own
  (six people at that home), the hand-over waits while one plays out there, and the council's count of fights shows in
  a wish. The design never used the council's tasks or the keeper; the scenario parts carried into a fresh start are
  now all of Rimfolk's but Bad Blood's two quarrelling families (`ScenPart_CouncilFamilies`, a feud along family
  lines), the grant of land without its warm goodwill (goodwill is the whole colony's). Checked against the code at
  0.104.0 and fixed: the Colonies page is the region map's Ours lens with a card per colony and a colony chip in every
  view's header (the interface decision 7 of 4 November; `ColonyView.Header` heads four pages today); the notables are
  made two an hour at most, not two a day; `HomeMap` has about fifty-five callers; the region never gives up a people's
  seat (`RiseFallMath.MayGiveUp`), so a struggling town the colony was asks to come back instead; the notables' old age
  is rolled only for those Rimfolk made and never for a leader, so it is widened to the town's people; the colony's
  ties at a home handed over are read at the seat already (`Diplomacy.HomeOf`); the region's towns in detail are per
  home already. The game's own fresh start, the archonexus ending (`MoveColonyUtility.MoveColonyAndReset`), resets
  research and every people's relations and is not used. The releases: 1 a fresh start (the place, a party and
  newcomers, the arrival, the records, the old colony kept or abandoned, the seat's button, the colonies on the Ours
  lens), 2 handing a colony over (also in the same act as a fresh start), 3 a scenario's start, 4 many colonies in play
  (merging councils, the pages following the colony in view, notables and world events near every home, the character
  per colony), 5 the town's life. The decisions, 15 of the 17, in the order the releases need them: the hand-over's say
  and "against their wish" are one (2), the newcomers' window and scenario starts are one (3), Faction Territories'
  vassalage at the hand-over is now what the town owes the colony with stage 2 built (9: free or a pact, fealty later
  by stage 2's own rule); none added. ROADMAP's stage 3 lines, its wave row and stage 2's status line follow; the
  interface design's Ours row names the towns the colony was; DESIGN.md's Ages says colony renown. Found outside the
  design and left as they are: Bad Blood's feud between two families in the game today (`ScenPart_CouncilFamilies`),
  the region's heirs made with their head's surname (`Notables.Make`, other peoples' ways), `AgeFeature.Renown`'s
  comment ("A house's renown"). Waiting with the player: the fifteen decisions.
- 0.104.0, diplomacy reshaped from family to colony (the player's decision, 5 November 2026): "we just need to make the shift from
  family to colony. family dynamics are fun and interesting, but it's never going to be a dynastic game like ck3, rather
  the colony is the 'family' so to say, so mechanics should revolve around that" (CLAUDE.md: the colony is the family).
  The player approved the list. (1) House renown is the
  colony's renown (DESIGN.md, "Colony renown"; docs/design/stage2-diplomacy.md, item 12, "As reshaped"): the colony's name
  among the peoples, read for the hour (`Renown.Read`) from every event of the history's diplomacy kinds the colony is a
  party to, once each, by the role of the one of its people it names (`RenownMath.OursIn`: a contest's champion only when
  the colony won it), so the gone count with the living; every mark the peoples keep of any colonist
  (`ColonistMemory.AllMarks`); what its living people hold (`Renown.People`: free colonists, never a ward or hostage of
  theirs living here as a lodger, and those away on its errand as lent); its own oaths and word at every home. The bands,
  known-for, the sources with their points, caps and fading, and the bounded effects are as they were, read for the
  colony as a whole (regard up to 15, down to -10, for any of its people; good words half again; matches up to twice,
  never certain; events a quarter to three quarters more by band; a feast's guests three tenths). The band is saved on
  the diplomacy book (`DiplomacyBook.renownBand`) and told as before (`dip:renown`, the colony its actor; `Tell.Minor` in
  five wordings of each move, `Rimfolk_Renown_Colony.xml`; the bubble from one of its people on the seat's map). Each
  colonist has a name of their own for their deeds (`Renown.Own`, an `OwnName`): the same sources from what they alone
  did, the same bands, said by their largest good deed from a town's band (two contests won make one), their worst when
  infamous (`RenownMath.KnownAs`), with the people it was done with or for and what it was (a contest's kind, now the
  `dip:contest` entry's key; the colony's name for them; the office; the Empire's title; a notable): "Known as the one who
  won the wrestling at the feast with the Lenlia, spoken of in the next town's market." on their card, and `us:knownas`,
  `us:knownas:KIND`, `{KNOWNAS}` in talk and letters where the listener's people know it (`RenownMath.TheyKnow`: across the
  region, or at a town's band the people it was done with). Personal effects, small: the champion challenges one known by
  name as a fighter six times in ten (`Renown.KnownFighter`, was a fighters' house), and one known by name for something
  draws its events a quarter more at most (`RenownMath.OwnFactor`, the best of that and the colony's, never both).
  People → Renown (`UI/RenownPage.cs`) is where People → Houses was (`PagesMath.Moved`: Houses opens it; its "?" the
  diplomacy page): the colony's lines (`RenownPanels.ColonyLines`, also on a home's card) and its people known by name.
  The houses went: `LedgerRenown.cs`, `UI/HousesPanel.cs`, the colony's fields on `LedgerHouse` (`ours`, `seat`, `folk`,
  `folkNames`, `lastSeen`, `band`), House cards (`Cards.House` now opens the house's town's card; one of the colony's
  shows as gone), houses in the search and as links on the region panel (the towns' founding families stay as their
  story, unlinked), Here and away's houses (now "Bound by marriage", `MatchPanels.BondRows`), the region's houses'
  renown (the sources `founding`, `head`, `leader`, `colonyTie`, `thriving`, `struggling`, `skill`), `them:house*` and
  the houses' tags, `{HOUSE}`, `{THEIRHOUSE}`. (2) Feuds aren't handed down: a grudge for a spouse wronged is their
  people's (`Matches.Wronged` gives it to their people; `Feuds`, `MatchMath.PassesDown` and the feud's letters and note
  are gone); grudges stay their people's against the colony (`tie:grudge`) or against a colonist by name (`you:foe`).
  (3) Marriage alliances (DESIGN.md, "Marriage alliances"; item 6, "As reshaped"): a bond between the colony and their
  people through one marriage. One of a town its head speaks for, made for a match, is nobody's child or kin, with a name
  of their own (`Matches.MakeOfTown`, was `MakeKin`, the head's child of the head's name; `WhyNotOfTown`; the letter's
  `match:OURS:h:NOTABLE:G` topic as it was); a spouse's death ends it, a child of the two or not (`ChildOf` gone,
  `CC_Match_Child*`, `CC_Match_Card_Child` and `match:child`'s words gone; `MatchMath.GrudgeAtDeath`: a grudge only when
  the colony was to blame); the season's gift is their town's (`TownGift`, `CC_Match_KinGift*` rewritten in place); "in-laws"
  are their people ("Write to their people", the raiders' camp warning from "{TOWN}, bound to the colony by marriage",
  the wedding's guests "notables of {TOWN}", as they always were in the code, the party at the gate "the party from
  {PEOPLE}"; the spouse's own family who walks in with them stays). The match's word by the colony's age: a match between
  clans, a marriage pact between two peoples, a marriage between two settlements, a partnership contract between
  settlements (ROADMAP's forms table with it, and its renown row the colony's). (4) Wards (item 7, "As reshaped"): a
  youth of their town (`Wards.MakeYouth`, nobody's child, a name of their own; `Wards.Sender`, was `Kin`, the notable who
  sends and answers for them; their offer `CC_Ward_OfferTown` in five wordings names the town); the youth's own family
  walks them in and comes for them; the colony's own youth goes in return as before; the parents' feelings stay
  (`Nature.WardGoes` untouched). Hostages unchanged. (5) Words: the houses' talk went with their facts
  (`Rimfolk_OutsiderTalks_Renown.xml`, `_RenownMore.xml`, 127 pieces; the letters of `Rimfolk_Lines_Renown.xml` and
  `_RenownMore.xml` and their house-tagged bubbles; `Rimfolk_Renown_Variants.xml`, `_VariantsMore.xml`; the keyed house news,
  cards and parts; the six match:child pieces and three lines); the shipped bubbles that say "our family" are retired
  (`ShippedLines.Retired`, with nine match and four ward lines that make the alliance a family's or a house's), those
  that name a band ask the colony's (`ShippedLines.AlsoWhen`); nine match pieces are retired whole (`OutsiderTalkMath.Retired`,
  new: a retired piece never fits) and four lines of pieces (`OutsiderTalkMath.RetiredLines`, new: skipped as the piece is
  read). New words: `Rimfolk_OutsiderTalks_ColonyRenown.xml` (83 pieces: the colony's name by band in the ways of tribes,
  the middle ages, the machine age, spacers and the Empire's court, known for each kind, infamous, not yet known, 13 on a
  colonist's own name, 35 callbacks), `Rimfolk_Lines_ColonyRenown.xml` (46 letter lines, 31 bubbles at every band and age,
  good words), `Rimfolk_OutsiderTalks_ColonyBonds.xml` (10 pieces) and `Rimfolk_Lines_ColonyBonds.xml` (26 lines) for the
  bond between the peoples, `Rimfolk_Renown_Colony.xml` (105 keys: the news, the names, the page, the bonds' rows, the
  ward's offer). In place: the settings' notes and tips ("Colony renown"; the stale "council's business carried abroad"
  gone from its tip), the match and wedding letters, the Almanac's diplomacy page (the renown paragraph is "Colony renown",
  marriages and wards reworded, no keyword or number lost) and its guide's People, then Renown. The generated talks'
  callbacks (FactsTests, floor 30 of 1,500) read 32 (31 before). (6) As it was: family names and everyday family life
  (`Social/FamilyNames.cs`: the Boyds, suppers, couples taking one name), now tied to no renown or diplomacy; debts of
  honour, truces, pacts, ransom, envoys, feasts, hostages, fealty, vassals, against their nature. Found outside the list,
  left as they are and named for the player: a colonist's vendetta left open at their death passes to their blood kin
  first, then a partner or a friend (`Secrets/VendettaEnds.cs`, "passed to an heir"); a dead colonist's aspiration taken
  up by someone close (`Aspirations.cs`, "inherited"); a hostage is still one of their notable's kin (`Hostages.MakeHostage`,
  the ways of hostages); two Empire pieces say "my house" of a titled household (the Empire keeps its own). Older saves:
  the colony's houses (`LedgerHouse` with no town) are dropped as the ledger loads, their nodes unread; the colony's band
  is set at the first day's look without a word (`RenownMath.NotRead`, `MovedFrom`), its renown and each colonist's own
  worked out from the history and memory the save already holds; an alliance held through a child ends at the day's look
  without a word; a spouse grudge held by a head is made their people's quietly (`Matches.PeoplesGrudges`); a match
  under way with a head's child made for it goes on; an older `dip:renown` line keeps its words. The save model drops
  the houses' lists (`docs/save-size.md`: 504 collections). Tests: `RenownTests` (rewritten: the colony's reading, own
  names, their words and who knows them, the personal effects, the houses gone and an older save, the colony's talk,
  letters and bubbles, the shipped lines), `MatchTests` (the town's gift, the death, the people's grudge and an older
  save's, one of their town, the retired pieces, the bonds' words), `WardTests` (a youth of their town, the offer),
  `OutsiderTalkTests` (the colony's tags, `KNOWNAS`, retired pieces), `PagesTests`, `LookTests`; the DefValidator knows
  `KNOWNAS` and the Renown tab, not `HOUSE` or `THEIRHOUSE`. Checks: textcheck against e62d56b reports the removals
  (whole files, the keyed house words, the renown letters' defs, the match:child pieces and lines, each shifting the
  positions after it in its file), no placeholder dropped in a line rewritten in place; almanaccheck against HEAD and
  e62d56b clean. To look at in game: an older save with houses (no red log; People → Houses opens Renown; the House cards
  gone, a founding family's name in the region panel unlinked; no message on the first day), the dev action "Renown: a
  contest won at a feast, for..." twice for one colonist and "the colony's and its people's to the log" (their card's
  line, People → Renown, a visitor of the guests' people calling them by it), the colony's band moving (the news, the
  bubble), a marriage whose spouse dies (the letter, no grudge), a spouse arrested (their people's grudge), their town's
  season's gift, their offer of a ward (the youth's town named), Here and away's "Bound by marriage".
- 0.103.0, banishment by the council (the player's request, 5 November 2026): "everyone in my colony seems to hate him, and they
  are regularly starting social fights ... the council should also be able to decide to banish a colonist ... if they
  are not liked by anyone and only causing trouble" (Barr, save Atia). Built. Added at the merge: those glad to see them go lose the game's fresh "colonist
  banished" memory unless they were sent out to die (`Banishment.TryLeave`, `RemoveMemoriesOfDefIf`), so Rimfolk's
  shame (`Council_Feelings.xml`) and the dream of judgment (`Council_Dreams2.xml`), which read it, don't fire for them.
  DESIGN.md, "Banishment by the council". The rules are
  `Social/BanishMath.cs` (pure, `BanishTests`): the trouble over the last 30 days is the social fights they were in,
  each counted once however often it is seen (Rimfolk's note as it starts, which knows who threw the first punch, and
  both fighters' `HadAngeringFight`/`HadCatharticFight` memories, which the game keeps 20 days), 1 for a fight they
  started, 0.6 for one they joined, 0.25 an insult they threw (the insulted person's `Insulted` memory), insults 1 at
  most. The first question needs 2 fights and 1.6; the second, after the council stepped in (a word said, or the nights
  apart) and at least 3 days on, 1.6 since with a fight; a step is remembered 60 days, "let it be" holds 20. The person:
  grown, a free colonist of the colony at home, never a child, prisoner, slave, quest lodger or guest, never held under
  a tie (`Wards.Of`, a ward's guardian, `Envoys.Holds`, `EnvoysAbroad.Holds`, `Matches.TieOf`/`PlanOf`, the ledger's
  ward, hostage, envoy, lent and spouse reasons); 6 people or more at home (babies and lodgers aside); 60% of the others
  (3 at least) at opinion -10 or below and at most one standing by them (opinion 20, or a partner or kin not at -10).
  `Social/Banishment.cs` (`BanishTracker`, a GameComponent: `records` capped 24, `fights` noted by
  `Patch_BanishFightNoted` on `StartSocialFight`, 35 days, 200 at most) plays it out in the council's people pass each
  hour: the word through `Together.When` (the speaker, else the faith's leader with its own role name, else another of
  the council, else the eldest, walks over; one sentence each, `Rimfolk_Pool_BanishWord`/`WordReply`; let go unsaid
  after 4 days, said so in the news); a banishment told face to face the same way (whoever is with them after 2 days),
  then they walk to their own bed's room and wait there an hour or so (`CC_Banish_Gathering`), then, when they can walk
  out (as quitting waits), Rimfolk's moods (`Rimfolk_Banish_Glad` for opinion -20, `Grief` for friends, `GriefKin` for
  a partner or kin, 30 days, trust -4 for both, `HadToTell`), the letter with their last words, the chronicle,
  `PawnBanishUtility.Banish(p, true)` and a walk to the nearest edge (`exitMapOnArrival`). Questions:
  `Rimfolk_Dilemma_Trouble` (a word, the night hours: `AnswerShiftMath.Apart`, 22 to 5 worked and 10 to 17 slept for 5
  days, or let it be) and `Rimfolk_Dilemma_Banish` (the age's banishment: cast out from the fire, sent into exile,
  expelled from the colony, struck off the colony's roster; a last word; the night hours; let it be, the default), both
  timely; the second's letter carries up to three of the council by their own opinion, a partner's, kin's or friend's
  objection by name, and their own say (`BanishContext.voices`). The answer's hints: who would grieve and who'd be glad,
  the game's own `ColonistBanished` for everyone or `ColonistBanishedToDie` when `WouldBeLeftToDie` (none when they're
  marked guilty), the skills the colony loses (its best hand from level 6, a good hand from 10), a council seat given
  up, and the risks. After: one with a grudge (30%) may ride with a hostile humanlike raid at a home, 12% a raid, 15
  days on at least, once (`Patch_BanishRaidJoin`, the raid letter names them); the existing `Council_Evt_Prodigal`
  ([banished] text) brings one sent away to the gate asking to come home; the card says "once one of the colony, cast
  out from the fire by the council", the world panel likewise. Setting `councilBanishment` (Council page). The game's
  own moods on a banishment: everyone of the colony gets the same small "colonist banished" memory whatever they
  thought of them (none for one marked guilty); only a banishment to die gives kin, friends and rivals the game's
  death moods. Nothing of the game's is changed: Rimfolk's `Glad` sits beside it for those who couldn't stand them.
  Barr in the save: two fights in the last three days (he started the one with MJ, 2.8 days ago; Macy started the one
  with him, 1.9 days ago, after his insult), nothing older in anyone's memory or Rimfolk's notes; four rivalries in the
  web (Toni, Penguin, MJ, Macy). By the memories alone MJ, Penguin, Toni, Macy and Alo think less of him and Harper and
  Stace a little better; his looks (Alpha Genes' beauty trait at -2, through the game's own "ugly" opinion when it
  lowers his beauty) and, if the colony's faith frowns on other faiths, his other ideoligion (Ideo_2 among Ideo_28) add
  the game's situational opinions, which the save doesn't hold. If six of the ten others are at -10 or below, the
  council asks the first question now: a word, never banishment. To check in the game: the word walked over and said,
  the nights apart on the schedule tab and put back after five days, the second question's voices, the walk to the
  room and to the edge, the game's own mood and the letter, the card and the world panel afterwards.
- 0.102.0, council tasks and the keeper of secrets removed (the player's decision, 5 November 2026): "drop council tasks for
  sure" and "remove keeper of secrets", with the rule set the same day (CLAUDE.md: the colony is the family, never a
  dynasty). Built in 0.96.0 (2c.1), reviewed in 0.97.1, worded
  in 0.98.0 and touched by 0.99.0's fixes; now out of the game. DESIGN.md, "The keeper of secrets and council tasks
  (dropped)", has what an older save keeps; docs/design/stage2-diplomacy.md, item 8, is marked dropped (with the build
  order, the decisions 13 and 14, and a note in item 11 that schemes now have no keeper's help, watch or "Sound out");
  ROADMAP's forms table loses its council tasks row (nine features, `AgeFeature.CouncilTasks` gone: not saved) and its
  offices row says twelve; README loses the tasks' paragraph and the keeper's sentence. What went: every task of the
  eight offices (the speaker winning over a head, the quartermaster's trade and samples and the dues, the marshal's dawn
  patrol and drill, the physician's medicine and the flu brought home, the warden's letters about the colony's own held
  and their kin in its cells, the chaplain's faith letters, the steward's feast and its welcome half again) with the
  officers' visits by the journey planner, the task lines and acts on the officer's card, the Decrees page's line under
  each seat, the people card's "Council task..." and its lines, Here and away's tasks section and the officer away, the
  inbox's `task` and `task-under-way` (This week's rows), the dev actions (`TaskDebug.cs`), the setting `diplomacyTasks`; the keeper of secrets as a whole (its office
  `Council_Office_SecretKeeper` with its election and planks, the hediff's edge, the slip factor: the colony's own
  secrets slip at their base rate again in `SecretTracker.Step`, as before 0.96.0; the watch after dark with guests;
  "Sound out" (`OutsiderAct.SoundOut` no longer in `DiplomacyActs` or the menu); learning a notable's secret by talks,
  informer or visit; pressing it by letter or face to face ("Press what we know" and the lines on a notable's and a
  Secret tie's card); being found out); `SecretTies.cs` whole (nothing else made or
  read Secret ties; `Tie.Kind.Secret` stays reserved for schemes, its rules and bounds as they were). Code: `CouncilTasks`,
  `TaskBook`, `TaskMath`, `TaskResults`, `TaskVisits`, `TaskLetters`, `TaskPanels`, `TaskWords`, `TaskJobs`, `TaskDebug`,
  `FactsTasks`, `KeeperOfSecrets`, `KeeperMath`, `SecretTies` deleted; the hooks out of `Diplomacy` (the hour and the
  day), `DiplomacyActs`, `DiplomacyLetters`, `DiplomacyBook`, `DiplomacyModel`, `Feasts.End`, `Ransoms` (the warden's
  `DemandFor`, their kin sooner and dearer), `Pacts.Arrive` (the drilled band), `RaidersCamps.SpotByPatrol`,
  `CourierTracker` (`Awaits`), `FactsLedger`, `Secrets`, `DecreePanel`, `HereAndAwayView`, `SettingsCatalog`,
  `NeighboursPanel`, `RenownMath` (the sources `task` and `told`, `OfficerRole`: an older save's `dip:task` lines give no
  renown, and `dip:task` is still the kind of a vassal's ask answered), `CouncilSettings`. Words: the files wholly theirs
  (`Rimfolk_OutsiderTalks_Tasks.xml`, `_TasksMore.xml`, `Rimfolk_Lines_Tasks.xml`, `_TasksMore.xml`, `Rimfolk_Tasks.xml`,
  `_Variants.xml`, `_VariantsMore.xml`, the office's and hediff's defs, `Defs/Misc/Rimfolk_Tasks.xml`), and elsewhere
  the lines said only on their facts: the four talk pieces pressed face to face (`Rimfolk_OutsiderTalks_LeviesPressed.xml`),
  the `pressmoot`, `taskmoot` and pressed-face letter lines (`Rimfolk_Lines_Overtaken.xml`), the
  `Rimfolk_OT_Keeper_Pressed` entry of `OutsiderTalkMath.AlsoWhen` (its piece gone), the ages' `CC_Form_CouncilTasks_*`,
  "Sound out" and its note, renown's two parts, the dues' keyed words in the fealty files; the def validator no longer
  knows `{OFFICER}` and `{OFFICE}` as talk placeholders. The Almanac: the offices page without the keeper's seat and its
  life (seven more offices, no task sentence), the diplomacy page without the tasks' and the keeper's paragraphs, the dues
  in fealty and vassals, and their setting's clause; no keyword was ever theirs, so none changed. What stayed for an older
  save (`Diplomacy/Retired.cs`): a home's `tasks`, `drillPeople`, `drillAt`, a vassal's `duesAt` and the setting are left
  unread (Scribe passes over a node nothing reads, so no stand-in type); the keeper's seat is let go with its holder and
  an election winner (`Offices.ExposeData`, `Gone`), its race in an election read with `CouncilScribe.LookDef` and dropped,
  a new keeper's story about the seat (`ArcWorker_Office`) ended without a word (`ArcTracker.Retired`); the game reads
  the keeper's hediff and the patrol's job by name, so both defs stay as stand-ins (`Defs/Misc/Rimfolk_Retired.xml`):
  `HediffComp_Retired` takes the hediff away at the next health tick, `JobDriver_TaskPatrol` ends at its first look;
  `LetterPurpose.Task` and `OutsiderAct.SoundOut` keep their places (a talk keeps its act by number in the job's count),
  a letter of the council's business not yet read is called off without a word (`NotableMath.Retired`,
  `NeighbourLetters.Step`; an answer already on its way back comes as written), a sounding out under way ends
  (`OutsiderMath.Retired`, `JobDriver_OutsiderTalk`); the keeper's Secret ties (origin `keeper`) end at the day's look,
  a pressed one too (called in, it had no end of its own); an officer's visit planned or on the road goes on as the
  colony's own journey there and back; a favour won by letter keeps its words (`CC_Dip_For_wonOver`, moved to
  `Rimfolk_Diplomacy.xml`), and so does a letter's purpose (`CC_Letter_Purpose_Task`); the history's `dip:task` lines and
  the notables' memory lines stay as text. Fealty and vassals: nothing came only through the task but a vassal's
  season's silver, which the design's vassals (item 10) never had: a vassal's tribute is its harvest (its carter at the
  gate by the council's level, as before), the liege's collector takes the season's tribute at the gate in two kinds of
  goods at most (four only with the task), and what the stores couldn't cover is paid by "Send what is owed"
  (`Fealty.PayOwed`); the liege's head no longer warms 3 for a punctual season; the place card's "Collect their dues..."
  and its words are gone. Tests: `TaskTests.cs` deleted, `TaskMath` and `KeeperMath` unlinked; `FealtyTests`,
  `RenownTests`, `DiplomacyTests` (with a section for the older save's handling), `OutsiderTalkTests` (the tags and the
  placeholders `{OFFICER}` and `{OFFICE}` gone), `OutsiderTests` and `WorldMechanicsTests` changed with it. The
  generated talks' callbacks (`FactsTests`, floor 30 of 1,500, unchanged) read 31 (36 before): over 4,500 talks the
  library picks up an earlier talk in 1.9% of them either way, the four task callback pieces' share small; the floor
  is near, as before. The save model drops the tasks (`docs/save-size.md`: 506 collections). Checks: textcheck against 7e1d7d6 reports only the
  removals (whole files, keys and lines) and the overtaken file's lines compared by position; almanaccheck against
  7e1d7d6 reports only the numbers of the removed paragraphs (the overheard 15%, the speaker's 1 to 6 and the keeper's
  35%), against HEAD nothing. To look at in game: an older save with a task under way, a keeper seated and a secret
  pressed (no red log, the seat gone from the Decrees page, the hediff gone, the letter quietly off, the secrets gone from
  the cards the next day), a liege's season's tribute short and paid by "Send what is owed", a vassal's harvest.
- 0.101.2, groups explained and friendship groups renamed (the player's report, 5 November 2026): "there seem to be regular
  gatherings for 'stace's friends', I'm not sure what the point is and why for stace's friends specifically". The
  player approved two fixes. (1) The gathering says what it is: a group's gathering (the anchor's call, `GroupHabits`)
  began as "An evening at the fire for Stace's friends: the fire is lit, and the colony gathers by the fire.", though
  only the group's people are invited; now its own words (`GroupHabits.BeginText`, `Does`;
  `Rimfolk_Groups_Gatherings.xml`): "An evening at the fire for the fireside circle: the fire is lit, and their anchor,
  Stace, gathers them by the fire. They are friends, and keep their evenings at the fire together. Those who come feel
  they belong, and their strain eases a little." (the anchor named only when they lead it on the map). This week's
  row adds its people and "Every week or two, while the group is close, their anchor, Stace, calls them all together
  like this: ..." (`GroupHabits.Tip` in `PlayOutText.Body`); its end says "... is over: 4 of them came, and the group is
  closer for it." (`play.outcome`, as the faith evenings do); the founding news adds once what keeping habits does and
  whom the anchor calls to what (`GroupHabits.Formed`); a habit's pill on the card says what keeping it does and
  whether the anchor calls them to it (`HabitTip`); the anchor's tip is `CC_GroupRoleTip_AnchorCalls` (the shipped
  one said "the group often bears their name"). The label on the map stays short ("{HABIT} for {GROUP}"). The words
  are the same in every age; the age shows in the names. (2) Friendship groups are named for what they share, never
  after one of them: `GroupShareDef` (`Rimfolk_GroupShares.xml`, 29: nine habits with their place, the night hours,
  ten works, eight sides of character no kind is named for, friendship; 3 to 8 names each, by era) ranked by the pure
  `GroupNaming` (the wip-groups branch's start, kept and finished: "most of them" is more than half for hours and
  work, two in three for a side; a name a kind or share gives is nobody's, so the medieval hunters' "the huntsmen's
  table" is never taken for a name after someone). Examples: tribal "the fire circle", "the shared pot", "the
  moon-watchers", "the spear circle"; medieval "the hearth circle", "the bench end", "the fellowship", "the reading
  circle"; industrial "the fire-pit crowd", "the mess corner", "the graveyard shift", "the rifle circle"; spacer as
  industrial and "the galley table"; any age "the fireside circle", "the long table", "the night birds", "the harvest
  circle", "the calm circle", "the old friends". A friendship group's card line is its share's ("They are friends, and
  most of them keep the night hours."). A kind whose own names are all taken is named the same way (it was "Hector's
  hunters", `CC_Group_NameOf`). Older saves: a group whose name reads as one after someone (the friends kind's old
  names, kept in the def only to find them; `CC_Group_NameOf`; `CC_Clique_Name`) is renamed once on load
  (`GroupTracker.LoadedGame`, and the day's pass if groups were off then: `GroupLife.RenameOld`), quietly: no news, no
  history line, its why, roles, history and everything else unchanged (past groups, journals and memories keep the
  name they had). The older circles (social life off, rebuilt daily) take a friendship name the same way
  (`GroupText.CircleName`, stable while their leader leads them). (3) The frequency: the code called a group at
  cohesion 35, which the card calls "loose" (25 to 49), where the Almanac, the defs and the code's own comment said a
  close group; now `GroupMath.MayCall` asks the card's "close" (`CloseAt` 50) and otherwise nothing changed: 8 days at
  least between two of a group's gatherings, then 1 in 5 a day (about 12 days on average, `DaysBetweenCalls`), one
  group at a time, three of them at home. A new group starts at 52 or more (its pull 0.42 or more; the other side of a
  split may start lower), so mostly it is a group that has loosened that is no longer called. "Regular gatherings for Stace's friends" is the design: a friendship group's first habit with a play
  kind is always the fire. Files: `Social/Groups/GroupNaming.cs`, `GroupDefs.cs`, `GroupFacts.cs` (`Shares`,
  `DoesWork`), `GroupLife.cs` (`UniqueName`, `RenameOld`), `GroupHabits.cs`, `GroupMath.cs`, `GroupText.cs`,
  `GroupTracker.cs`, `GroupView.cs`, `Social/SocialWeb.cs`, `PlayOut/PlayOutTracker.cs`, `UI/PlayOutText.cs`,
  `Defs/GroupDefs/Rimfolk_GroupShares.xml` (new), the kinds' and habits' comments,
  `Languages/English/Keyed/Rimfolk_Groups_Gatherings.xml` (new), the Almanac's "Groups", "Roles in a group" and social
  life pages (no keyword changed), DESIGN.md "Groups". Tests: `GroupTests` (ranking and thresholds, a pick kept and
  never another group's, the old names found with a number or two words and a kind's own name not, keyed names as
  patterns, a close group called every week or two and a loose one never, every share's names for any era, none after
  someone or a kind's own, no two shares alike, every habit can name a group). Settled at the merge: the why
  ("They are friends... their strain eases a little.") is said as a gathering begins only at a group's first two
  (`SocialGroup.gatherings`, counted where the anchor calls it, `GroupHabits.ExplainedGatherings`; an old save's groups
  start at 0, so the player sees it twice more), and This week's tooltip and the card keep it always; a group named for
  a habit is named for the one its anchor calls it to (`GroupFacts.Shares` puts the first habit that plays out first),
  so "an evening at the fire for the long table" can't be told. "talking late
  for the late talkers" can't happen (a friendship group is always called to the fire), but a healers' group called to
  talk late reads "Talking late for the healers" as before. To look at in game: the player's save (Stace's friends
  renamed on load, the card and This week), a group's gathering as it begins and ends, a new friendship group's
  founding news, a medieval and a tribal colony's names.
- 0.101.1, the player's report that the turret mood still reached everyone (save Atia, 5 November): the colony's faith
  (Ideo_28, "Astro-Genetics") holds Genetic Rim's `GR_AutonomousWeapons_Scorned` (issue `AutonomousWeapons`), whose mood
  `GR_HasAutomatedTurrets_Scorned` uses the game's `ThoughtWorker_Precept_HasAutomatedTurrets` (Genetic Rim's
  `1.7/Mods/Ideology/Defs/PreceptDefs/Precepts_AutonomousWeapons.xml`). 0.93.1 found the moods by the precept's or issue's
  name holding "Turret", so it found none, `Patch_TurretFaith.Prepare` stood down and nothing was patched. Now
  `TurretFaith.Thoughts` takes every ThoughtDef whose worker is (or derives from) the game's turret check, plus the
  situational moods of precepts whose name, issue or thought says turrets. `TurretOn` counts what the game's check counts
  (a turret building by `building.IsTurret` without `CompMannable`, and with Biotech a colony mech it controls), and the
  postfix touches only the colony's own people (the game weighs another people's by their own faction). The hatch's
  pocket map (`AncientStockpile`, uniqueID 1) holds the hacked `Turret_AncientArmoredTurret` of the colony; the game
  keeps a pocket map loaded once entered (`MapPortal`), and `ColonistBar.CheckRecacheEntries` gives every loaded map a
  group, an empty one a placeholder: the empty group in the player's bar is the game's own, not colonists left behind.
- 5 November 2026, a play-test point after 0.101.0: stage 2 is built, reviewed and given its text pass except 2c.3
  schemes (round d: 0.99.0 house renown, 0.99.1 its review, 0.100.0 against their nature, 0.100.1 its review, 0.101.0
  the round's text pass), none of round c or d yet seen in the game. Nothing new is built until the player has played
  and given word. Waiting with the player: the harm review's picks (3.5.1 rival sabotage gates schemes), stage 3's 17
  decisions, and the Move the Patient mod's folder for the medical triage they asked for (best bed first, lighter
  patients moved out by the mod's transfer). Whether 0.100.0 should have been 1.0 is the player's call (asked).
- 0.101.0, text pass: diplomacy round d (the content pass beside 0.99.0 and 0.100.0, house renown and against their nature, after the
  0.99.1 and 0.100.1 reviews). First the words against the
  code, each case checked in the code and fixed where it was wrong; a shipped line's text and tags stay (textcheck against
  the start commit clean), so a shipped line found untrue in a case its tags let through now asks for more where it is
  picked: a conversation piece by `OutsiderTalkMath.AlsoWhen` (as round c's), a flavour pool's line by the new
  `Lean/ShippedLines.cs` (pure: its text as written and the condition it needs, or retired; `Flavor.Match` asks it once
  per line and keeps it on `LineTags.Parsed.also`), the better words in new files. Fixed: (1) renownup's "a song with our
  name in it" (medieval), "made the papers" (industrial), "a nod in the markets", "the other camps ... at their fires" and
  "known well beyond the colony" were said as a name first reached a town nearby, whose words are the next town's market,
  the next town, the nearest camp's fire: they ask `house:renowned` now, and the town's band has lines of its own by age;
  (2) renowndown's "The singers have found someone else to sing about" was said of a name only ever talked of in the next
  town: the bubble carries `house:wasrenowned` for a name fallen from the region's band or wider (`RenownMath.BubbleTag`,
  passed by `Renown.Told` through `Outsiders.Say`'s new `also`), and the singers ask it; (3) {RENOWN} to the Empire's
  people was in their tech age's words ("carried on every channel in the region"): the bands' table has the Empire's row
  (`CC_Renown_Band_*_Empire`: "known to the nearest town's nobility", "spoken of with honor in the region's noble
  houses", "honored at court and far beyond it", "spoken of without honor at court"; `RenownMath.BandKey`'s `empire`,
  `FactsRenown` by `Ages.IsEmpire`), and the unused `RenownWords.Form` is gone; (4) their answer to the colony's
  condolences from one of a renowned house said "A loss to {HOUSE} is felt here too", where the loss is theirs (the
  letter's topic): retired, its better words beside; (5) "My family hasn't forgotten", "a grudge between my house and your
  colony" (two talk pieces) and "our house" (two letter lines) were said by any of them with the feud's tag, which is set
  too for one who heads no house (their people's grudge about one of the colonist's house): only with `them:house` now,
  and words for one with none ("my people", "us"); (6) the tribal "Then ours is still earning" (a name not yet known) was
  said of a house known for its wrongs: `!house:infamous`, as its plain fellow; (7) `CC_Renown_Up_3` "have started to
  speak of" was told for a name already known moving to a wider band: rewritten ("More people ... than before"); (8) a
  hostage's close kin and a ward's parents "saw" them given or sent as they walk off the map, which nothing shows them
  watching: "had to let Bran go to the Lenlia as the colony's hostage" (rewritten in place, the page's too); (9) a truce
  sworn by word with no envoy at the gate read "saw the colony swear a truce" for the avenger at home: `Truces.Swear`
  passes it (`Nature.TruceWithFoe`'s `byWord`, the role `Word`: "heard the colony had sworn a truce with the Lenlia by
  word"); (10) a pact's call refused by the council's answer read "broke the colony's word" for the speaker who only sent
  the refusal: the role `Call` ("sent the colony's refusal when the Lenlia called on the pact"); (11) two marks' news,
  told as the act happens: "has been quieter since swearing" (no time has passed) and "seems more at ease among
  strangers" (they have just walked off the map), rewritten in place; (12) the setting's tooltip and the Almanac's page
  said "their close kin" (the parents, the captive's kin, the avenger) without "there": now "there to see them go",
  "there", "at that home"; the tooltip names the speaker who sent a pact call's refusal and the challenger still there,
  the Almanac's renown says the bubble only as a name carries further or fades and the Empire's court words. Then
  variety, every line said only on facts the providers set (FactsRenown, the ties, FactsColony's deeds), each checked in
  code. Talk, 77 pieces (`Rimfolk_OutsiderTalks_RenownMore.xml`; the round's own file keeps its fifty): a name known in a
  town nearby 5, across the region 5, among every people 5, each by their ways (tribes, the middle ages, industrial,
  spacers, the Empire's court, the spacer pieces never to the Empire), not yet known 4, by who they are 4 (a trader, their
  envoy, a first meeting, a titled colonist to the Empire), known for each of the six by two to four peoples' ways 21,
  known for beside what their people remember (deed:feasted, tended, traded, levies) 4, known for its wrongs by their
  ways 4, their house (by their ways, both families, a colonist of no house, a renowned house) 7, feuds 5 (three from one
  who heads no house, never "my house"), callbacks on hunger, cold, ill, leader, head, trade, work, faith, family,
  situation and war 13; "your family's {KNOWNFOR}" only for the kinds whose words are nouns there. Letters, 43
  (`Rimfolk_Lines_RenownMore.xml`, their words): greetings by band and ways, known-for, infamy, to a house's head and in a
  feud 28, news 3, warnings 3, condolences as their own loss 3, help 3 and invitations 3 (promised only in a granting
  tone). Bubbles, 42: renownup 27 (the town's band, the region's and the peoples' in each age's words, by known-for, kind),
  renowndown 12 (from the next town's talk and from the region's, in each age's words), wordyes 3. Keyed, 91: against their
  nature 75 (`Rimfolk_Nature_Variants.xml`: every act and role three wordings 34, the sides 9, the frame 4, the news six
  wordings of a weight and five of an ease 6, the council's questions three each 12, the marks' news three each 8; the two
  new roles in `Rimfolk_Nature.xml`), house renown 16 (`Rimfolk_Renown_VariantsMore.xml`, each news line five or six
  wordings 12; the Empire's four bands). Code for the words: the act, side and frame read with `T` and kept on the heart as
  before (`Nature.Why`), a question's line by `TextVariants.VaryBy` (new: one wording kept per colonist and act, so the
  letter's answer and the Dilemmas page agree), a mark's news among its keyed fellows (`PersonaMarks.NewsLine`,
  `CC_Mark_News_DEFNAME_2`..., for any mark), the def validator knows the two roles. Files: `Lean/ShippedLines.cs`,
  `Lean/LineTags.cs`, `Flavor.cs`, `Outsiders/Outsiders.cs`, `Outsiders/OutsiderTalkMath.cs`, `Diplomacy/RenownMath.cs`,
  `RenownWords.cs`, `FactsRenown.cs`, `Renown.cs`, `Nature.cs`, `Truces.cs`, `CallsToArms.cs`, `Text/TextVariants.cs`,
  `Persona/Marks.cs`, `Tools/DefValidator/Program.cs`, the word files above, `Rimfolk_Nature.xml`, `Rimfolk_Renown.xml`,
  `Rimfolk_Renown_Variants.xml`, `Rimfolk_PersonaMarks_Nature.xml`, the Almanac's diplomacy page; DESIGN.md says each
  changed rule (and "Shipped words found untrue" under Facts). Tests: `RenownTests` (the court's row, the bubble's tag,
  every shipped line still in the defs word for word with a known condition, the feud and "still earning", the new talk
  by band and ways, the bubbles by band and age, no song for the town's band, the letters' granting tones, condolences
  as their loss), `NatureTests` (every act, role, side, frame, question and mark's news in two wordings or more with the
  original's placeholders, no guessed pronoun, the kin had to let them go, a truce by word heard of, a pact's call
  refused, the marks' news as it happens). Not confirmed, left as they are: the feud's tag cannot tell a grudge the one
  spoken to holds from their people's about one of the colonist's house, so with `them:house` "my house" is said for
  both; the shipped `Rimfolk_OT_Renown_Spacer` ("comes up on our channels") is still said to the Empire's people (they are
  of the spacer age) beside their court piece, its words true of them; `Rimfolk_OT_Renown_Town` ("heard of {HOUSE} back
  home") is said by a visitor from any town, though the band is a town nearby (the new medieval one asks `town:near`);
  `ToneLines.Pick`'s last fallback (any line when none fits) could give a retired line, but both pools fixed here are
  picked only through `PickTagged`; "in front of everyone" and "with everyone watching" for a challenge faced, the
  contest's crowd not seen in game; the marks' "seems less afraid" and "seems to trust words less", told as the act
  happens, kept as a mark's change is told elsewhere. Still thin, and facts that would help: the feud's holder as a tag
  (`them:house:feud:own`); letters of stage 2's purposes (a match, a ward, an envoy, a pact, a truce) with a house's name,
  whose tones mean different outcomes by purpose (to read each before writing); which town knows a name at the town's
  band. To look at in game: a house's name reaching a town nearby in a medieval colony (the bubble names the next town's
  market, never a song) and fading again (no singers); talk with a visitor of the Empire about a renowned house (the
  court's words for {RENOWN}); a feud's visitor who heads no house; a hostage given with a warm brother at home (the heart's
  page: "had to let Bran go"); a truce sworn by word with an avenger at home; a pact's call refused; a proud speaker's
  oath and its mark's news; the council's oath question twice for the same speaker (the same wording in the letter and on
  the Dilemmas page).
- 0.100.1, against their nature, checked over (the release 0.100.0, 2d.2, read against its code, every place it hooks into and the
  1.6 game). Fixed, each checked in the code and the game's
  source: the strain mood and an episode's letter put the act's words into their own sentence (`HeartView.SourcesText`
  fed both), so the mood read "built up by fighting and swore the colony's oath to the Lenlia, against her pride" and
  the letter "carrying too much for too long: ... swore the colony's oath ..."; they name the source now ("acts against
  their nature"), and the heart's page keeps the act's words (`SourcesDetailed`). Close kin (`Nature.CloseKinOf`) were
  read from the direct relations only, and a child and a brother or sister are the game's implied relations (its
  `PawnRelationWorker_Child` and `_Sibling`, never in `DirectRelations`), so a hostage's children, brothers and sisters,
  and a ransomed captive's, never felt it (only parents, a spouse, a betrothed, a lover); every relation between the two
  is read now (the game's `GetRelations`). Those whose words say they saw or heard it ("saw Bran given to the Lenlia as
  the colony's hostage", "saw the colony turn down Ada's challenge", "saw the colony swear a truce", "heard the colony
  haggle over his freedom") felt it wherever they were: a parent away with a caravan or abroad as the colony's envoy or
  hostage (a lent world pawn is still a free colonist), an avenger out on the road (`Homes.Of` named their home); now
  only those on the map where it happens (`Nature.There`: the hostage's or the ward's, the captive's home, the truce's
  home, as the truce's objection already asked its avenger, the challenge's). A challenge turned down weighed on the
  champion even when the challenger had already gone (an answer given late, or by default): `DilemmaEffect_SendAway`
  felt it before asking whether anyone was left to send away, while its sheet named it only with the challenger here;
  now after. An arrest broke a truce or a pact with nobody named: the game weighs an arrest as a capture the moment it
  is tried (`Pawn.CheckAcceptArrest` calls `Faction.Notify_MemberCaptured` itself, before `CapturedBy`, whose own
  weighing then finds nothing left to break), so whoever arrested a pact partner's trader or a truce people's visitor
  never felt it; the new `Patch_MemoryArrestedBy` (a prefix and a finalizer on `CheckAcceptArrest(Pawn arrester)`, as
  the captor's) names them. A youth sent to serve as a page in a titled household of the Empire was "sent to live with
  the Empire as a ward" on the parents' page and in the news; a page has its own words now (`CC_Nature_Act_WardSent_Page`,
  the role "Page"). A match pressed was felt ("was promised to ...") before `Matches.Agree`, which stands down when the
  home or either of the two is gone; it is felt once agreed. A call to arms refused from its letter with no home left
  asked `TruceParley.Speaker(null)`, which throws, after the pact was broken; no speaker then. Two marks' news: "has set
  out for the Lenlia a second time this year" when the first season was among another people, and "this year" for twice
  within sixty days across the year's turn; now "a second season away within a year" and "a second challenger within a
  year". Files: `Heart/HeartView.cs`, `Diplomacy/Nature.cs`, `WardsAbroad.cs`, `Ransoms.cs`, `Matches.cs`,
  `CallsToArms.cs`, `Strangers/StrangerEffects.cs`, `World/Patch_FactionMemory.cs`,
  `Languages/English/Keyed/Rimfolk_Nature.xml`, `Defs/PersonaDefs/Rimfolk_PersonaMarks_Nature.xml`,
  `Tools/DefValidator/Program.cs`; DESIGN.md says each. Tests: `NatureTests` (the mood's sources, close kin by every
  relation, the acts whose words say saw or heard and who is there for them, the arrest's captor, the page, the
  challenge with its challenger here, the match once agreed, the call with no home, the marks' news). Looked at and found
  sound: the patches against the 1.6 game (`Notify_MemberTookDamage(Pawn member, DamageInfo dinfo)`,
  `CapturedBy(Faction by, Pawn byPawn)`, the parameter names exact) and 1.7 (ApiDiff); their cost where damage is dealt
  all the time (a settings read and a cast before the game's own method, the finalizer's try; nothing allocated); the
  doer cleared on every path (`By` restores the one before in a finally; each patch's finalizer clears on a throw; the
  notes made inside one, the colony's own "brokeTruce", "wordBroken" and "renounced", are no breach, so nobody feels a
  second time); never one of another people or an animal (`KinMemory.Ours` before anything, and a turret's or a trap's
  blow names nobody); the `BrokenByUs` returns on every branch (a truce not yet sworn ends with no deed and false, each
  still called through `|=`, so their callers do as before); the saves (the four values with their defaults, both kinds
  at the end of their lists, the heart's lists padded on load); the setting off (`On` asked first everywhere, the
  questions' lines too; what was felt drains as any strain does); one a day (set only by an act that moved the heart; a
  later tick never blocks); the marks (their chance, a turning point once in its keeping, the persona's own limits, the
  act before read from the heart's saved record, kept while one is lent abroad); each act once (the oath once a home, a
  break once since it ends what it breaks, the walk off the map, the answer, the contest's `done`); the words by the
  game's pronouns; the questions' people (the oath's speaker as the one sent out, the avenger asked, the colonist matched,
  the champion). Not confirmed, left as they are: the council's question names an act's weight on someone who already
  felt one that day (`Would` asks no day's rule; the answer may come the next day); a truce sworn by word, no envoy at
  the gate, is still "saw the colony swear a truce" to the avenger at home; the hostage, the ward's parents and the envoy
  feel it just before the game makes their caravan, so a throw there leaves it felt and them at the edge; the first of a
  caravan that fell on theirs is the game's look pawn (`PawnsListForReading[0]`), whoever that is; the Almanac's page and
  the setting's tip say "their close kin" without "there to see it" (the text pass's to look at). To look at in game: a
  hostage given with a brother at home and a sister away with a caravan (only the brother feels it, on his page and in
  the news); a pact partner's trader arrested (the arrester's heart page, if trusting); a challenge turned down after
  the challenger left (nothing felt); the strain mood's tooltip with an oath on the heart ("acts against their nature").
- 0.100.0, stage 2 release 2d.2, against their nature (docs/design/stage2-diplomacy.md, item 13, with its "As built"; the
  player's decision 19 as recommended: strain through the heart only, no new mood, easing when someone acts with their
  nature, marks for repeated acts). DESIGN.md,
  "Diplomacy" → "Against their nature" has the whole of it. Pure and tested (`Tools/Tests/NatureTests.cs`, 125 checks;
  `DiplomacyTests` and `MatchTests` moved with it): `Diplomacy/NatureMath.cs` (`SwitchedOn` true; the rules as written;
  `Floor` 0.4, tuned from a quarter so an act just past where a side shows brings about 5 and stays on the heart's page
  for days; `Felt`, `Pick` (two acts at once count as one, the one that weighs most), `RuleOf`, `Scale` (the "Life's ups
  and downs" setting by `PressureMath.Factor`, read whatever the stakes since it is no mood, and `HardTimesPressure`),
  `MayFeel` (one a day; a last act from a later tick never blocks), `CloseKin` (parent, child, sibling, spouse, fiance,
  lover), `WardParent`, `BreakerFeels`, `MarkFamily`, `ActKey`/`SideKey`/`SideKeys`, `Wired` (all but the dark scheme)).
  The heart: `StrainSource.Nature` and `StrainRelief.Nature` at the end of their lists, the source's cap 12
  (`HeartMath.SourceCap`), `HeartLogic.Build` (strain from outside the hour as the hour builds it: strength setting,
  traits, the source's cap and the day's 25; yesterday's counts don't hold back an act before the hour's new day) and
  `HeartLogic.Relieve` (their nature's way of letting go, never more than they hold; not `Interplay.Ease`, whose cap is the
  friends'), four values on `PawnHeart` (`natureTick`, `natureAct`, `natureEased`, `natureWhy`; no new list, so nothing for
  the save model), the heart's page (`HeartView.SourceText`/`ReliefText`: "swore the colony's oath to the Lenlia, against
  her pride (+7)"; `NatureHelp`: "A quiet evening with friends would help." while the strain is under the general advice;
  `HelpFor` names it above 30). The service (`Diplomacy/Nature.cs`): `On` (switched on, the setting `diplomacyNature`
  "Against their nature" in Diplomacy, needing the heart), `Felt` (one of the colony's with a heart; one a day; the act's
  words by the game's own pronouns; `Tell.Minor` in the colony's news, three wordings and two eased; a mark through
  `PersonaMarks.Give`), `Would` and `Impact` (the council's questions), `CloseKinOf`, `ParentsOf`, `HostageGoes`,
  `WardGoes`, `TruceWithFoe`. Wired where each act happens: the oath in `Fealty.Swear` (the speaker who walked out of
  `JobDriver_FealtyOath`); a truce, pact, oath or protection broken in `PactsHooks.Noted` on whoever the game names for the
  deed (`MemoryHooks.Doer`, set for the length of the game's call: the blow's hand by the new `Patch_MemoryDamageBy` on
  `Faction.Notify_MemberTookDamage`, the captor by the new `Patch_MemoryCapturedBy` on `Pawn_GuestTracker.CapturedBy`, the
  seller (`TradeSession.playerNegotiator`), the first of a caravan that attacked theirs (the game's own look pawn), the
  killer (`dinfo.Instigator`); `Truces`/`Pacts`/`Fealty`/`Vassals.BrokenByUs` now say whether they broke one) and a pact's
  call refused by the council's answer in `CallsToArms.Refused` (the speaker); a hostage and a ward at
  `WardsAbroad.AtEdge` as they walk off the map (the hostage timid or set in their ways, one act; their warm close kin; a
  ward's warm parents); a match in `Matches.Pressed`; a challenge turned down in `DilemmaEffect_SendAway` (the happening's
  `contest`: the travelling champion and the rematch) and faced in `HappeningWorker_Champion.Contest`; the envoy at
  `EnvoysAbroad.AtEdge`; haggling in `Ransoms.AskMore` (the speaker, and the captive's own close kin among the colony);
  the truce with one's foe in `Truces.Swear` (each avenger of the home, from the vendettas). The council's questions name
  it on the answer that asks it (`FealtyObjection`: the speaker's pride; `TruceObjection`: the avenger's vengeance;
  `MatchObjection`: the colonist's heart; the champion's and rematch's accept and decline), and the oath's objection has
  two new `[proud]` lines ("too proud to kneel to anyone", appended so no shipped line moved). Marks
  (`Defs/PersonaDefs/Rimfolk_PersonaMarks_Nature.xml`): `Rimfolk_Mark_Humbled` (`NatureOath`, a turning point with its
  journal, half the time, pride -8), `Rimfolk_Mark_WordBrokenTwice` (trust -7), `Rimfolk_Mark_FacedChallengers` (nerve +6),
  `Rimfolk_Mark_WorldSeen` (openness +6), the last three for the same act twice within a year, six times in ten. Words:
  `Languages/English/Keyed/Rimfolk_Nature.xml` (the setting, the heart's, each act and side, the news, the questions); the
  Almanac's diplomacy page (a paragraph and a clause in Settings, no keyword changed); README; the def validator knows the
  new keys. The balance (`Tools/Balance -- --nature`, `NatureSim.cs`, printed; `docs/balance-report.md` unchanged since its
  model didn't change): the heart hour by hour with the mod's caps and episode odds, the rest of life set to the report's
  strain shares per style (2/0/0, 8/2/0, 20/8/2 per cent; the model gives 2.2/0.2/0, 7.8/2.1/0.4, 18.6/7.5/2.4), natures
  from the vanilla traits; a colony deep in diplomacy (about 5.5 acts a year, generous) feels about 0.8 a year, each about
  6 strain (on the heart's page some five days), 0.1 to 0.4 per cent of all strain (15th of 17 sources), episodes a
  colonist-year 0.044 against 0.042 for an average colony, the act the main cause of none. Not wired, and why: the dark
  scheme (schemes, 2c.3, are not built and wait for the player's harm pick; its "three in a year hardens a kind one" mark
  is not defined); a town attacked or destroyed, and a harmful ability or item, break a truce with nobody named, since
  the game names nobody for them; the feasts' contests (planned by the colony, nothing to refuse); no bubbles (each act
  already shows where it happens). To look at in game: a proud speaker swearing fealty (the heart's page line and its
  advice, the news, a mark maybe), the oath's question with a proud colonist objecting (the new lines, the speaker's
  pride named on "swear it all the same"), a timid champion facing the travelling champion and a proud one declined (the
  answers' sheets), a hostage given with a warm sister at home, an envoy who is set in their ways and one who is curious
  (eased), a colonist striking one of a pact partner's people or capturing one of a truce people's (the one the game names
  feels it), the setting off.
- 0.99.1, house renown, checked over (the release 0.99.0, 2d.1, read against its code, every place it hooks into and the 1.6
  game; no version of its own: its points go with the next round). Fixed, each checked in the code and the game's
  source: one who took another's name on marrying (the game's ceremony, or the council's shared name, `FamilyNames.Take`)
  is in both houses' `folk`, and the hour's index put them in whichever house the ledger listed last, so their renown,
  what they hold, their card's line and Here and away's row for their family could be the old house's, and the other
  house's card said they were no longer with the colony; the index now puts each in the house of the name they carry,
  else the newest that remembers them (`RenownMath.HouseFor`, `Renown.Put`), and the card's gone leaves out anyone still
  one of the colony's (`Renown.StillOurs`). One of a house away at another people's town on the colony's errand (its
  envoy a season, its youth a year, its hostage a pact year: world pawns kept as lent) was not of it: its card said they
  were no longer with the colony, and what they hold (names, offices, terms, a title, the notables' regard, up to fifty
  points) left the reading, so the band could fall and be told ("fewer speak of...") while they spoke for the colony
  abroad, and rise and be told again at their return; they are of the house while `KeepFor.Lent` holds (`Renown.Index`).
  The band's bubble: renownup was said as a bad name mended ("strangers know our name" of a name not yet known) and
  renowndown as a name went bad or only narrowed ("people out there have stopped talking about us" of a name cursed in
  the region's halls, or still spoken of in the next town); `RenownMath.Bubble` says renownup only as the name carries
  further and renowndown only as it fades from the talk (the history and the news tell the rest), and the one who says
  it is awake and free (the day's look runs in the game's first hour, often night at home, and a sleeping colonist
  spoke). The colony's hostage home after the term never counted: `WardsAbroad.Back` named them About where the youth
  and the envoy home are named as having done it; now Did, and `RenownMath.SourceOf` takes a hostage's deed only when it
  was for the colony (a hostage of theirs who stayed and joined served no term of the colony's). Every line of a council
  task naming its officer counted as the council's business carried abroad: the task set, a visit's step, one called
  off (three points each, the source's cap of twelve reached by one task), the keeper of secrets found out (the only
  `TaskStage.Failed`) and a secret learned; only a task's milestone counts now (`SourceOf`), and `CouncilTasks.Write`
  names the officer as one with a task failed or the keeper's secret watch (`RenownMath.OfficerRole`, as the guardian's
  role: a secret learned is heard of by nobody, as one pressed and paid). A secret pressed and told never fell on the
  keeper's house: the press's history (`SecretTies.Answer`) named only the colony and the notable; it names the one who
  learned it now, as its line does. A house fallen to one was let go two years after it fell to one though that one
  still lived at the colony (`Renown.Sync` passed nobody here); it is kept while one of it is with the colony, its two
  years counted from when they go. The House card named its gone only from the ledger's person records, which last only
  while something names them, so a colonist buried at home went unnamed and a house with only its dead listed nobody;
  each of the colony's houses keeps its people's names beside their refs while they live (`LedgerHouse.folkNames`,
  saved for the colony's houses only; an older save's filled in for the living at the next day; the save model). Files:
  `Diplomacy/Renown.cs`, `RenownMath.cs`, `RenownPanels.cs`, `World/Ledger/LedgerRegion.cs`, `Diplomacy/WardsAbroad.cs`,
  `CouncilTasks.cs`, `SecretTies.cs`, `Tools/save_estimate.py`; DESIGN.md says each. Tests: `RenownTests` (the house of a
  name, the bubble, the hostage's and the task's sources, the officer's role, the code paths, the names and their save).
  Looked at and found sound: the saves (the new fields' defaults, an older save's houses, refs kept as text, the towns'
  houses with a band); the reading kept for the in-game hour (every hook reads `Renown.Read`'s hourly cache and the
  hour's index; the House card, People → Houses and Here and away build at most once a second; nothing per frame or
  per tick); the bands (the hold of three, infamy held to -12, a band told once: set as it is told, a new house's set
  untold); the bounds (regard up to 15 and down to -10, a good word half again, a match accepted at most 0.95 and offered
  at twice the day's chance, events a quarter, half and three quarters more by band, a feast's guests three tenths and at
  most 0.95, the champion six in ten); the Empire's titles read only (`Seniority`, guarded); every age's words (bands and
  known-for in four ages, the bubbles by the colony's age); a colonist with no family name (in no house, no line); an
  empty house; no home (no bubble, the news without a target). Not confirmed, left as they are: a house with nobody
  left still shares the colony's deeds and its moves are told, so the day after the last of a family died its band can
  fall ("faded from the talk") as what they held leaves the reading, and an oath sworn later can raise it (the design
  shares the colony's deeds with each of its houses and tells every move of one); two unrelated families of one name are
  one house (houses are keyed by name; People → Houses shows them apart, both linking to it); their house's dealings with
  the colony count every diplomacy and tie event between its head and the colony, a grudge their head holds and a secret
  pressed on them included; a band shows on the cards and in talk up to a day before the day's look tells it (read by
  the hour, told by the day); an older save's lines of a failed task and the keeper's watch still name the officer as
  having done it (three each, capped), and its secrets pressed and hostages home stay uncounted; the first band's
  renownup lines in the middle ages ("a song with our name in it") and the industrial age ("made the papers") are the
  region band's words (the text pass's to look at); {RENOWN} to the Empire's people is in their tech age's words, not
  the court's (`RenownWords.Form`, the Renown age form, is unused); `Matches.Offers` now makes its pick (each colonist's
  candidates) every day for each town that may offer, before the day's roll (it rolled first; daily, not per tick); their
  House card shows no marriages or wards (the ties name the colony's people and their people, the house only its head);
  one of the colony's held by them (a hostage when the pact broke) or carried off is gone from the house, and what they
  held leaves its reading. To look at in game: a couple of two houses where one takes the other's name (both House
  cards, the colonist's line); a house's envoy away a season (still listed, the band holds); a house gone bad and mending
  (no bubble, the news); the colony's hostage home (its house's "terms served as the colony's hostage"); a task set (no
  renown) and done; a secret pressed and told (the keeper's house: "a secret pressed and told"); a house whose people
  all died (its card names them).
- 0.99.0, stage 2 release 2d.1, house renown (docs/design/stage2-diplomacy.md, item 12; the player's decision 17 as recommended:
  earned and shown in words with a "known for", nothing spent; decision 9 for the Empire: titles read, never given; no
  version of its own here: its points go with the round). DESIGN.md, "Diplomacy" → "House renown" has the whole of it.
  First, the round c text pass's six words-versus-code mismatches (the 0.98.0 entry below), each fixed in the code with
  the corrected words under new tags in new files (`Rimfolk_OutsiderTalks_LeviesPressed.xml`, `Rimfolk_Lines_Overtaken.xml`;
  no shipped line's tags changed, textcheck against the start commit clean): (1) the levies' history now says how a call
  ended (their people For it when the levies came, Against when refused), and `FactsFealty` sets `fealty:levies` (whose
  older talk and letter lines say they came) and `fealty:levies:sent` only when they came, `fealty:levies:refused` when
  refused; a call still open says neither (`FealtyMath.LevyOutcome`); (2) a plea for help is quoted by what really comes
  with the answer (`NotableMath.HelpQuoteTone`, `NeighbourLetters.Quote`'s `helpSent`): with help, in its tone; without, in
  the refusal's words, so `[help,cool,fealty:oath]` is never said (help is never sent on a cool answer; its words are back
  as a cordial line); (3) an oath letter overtaken by events (`Fealty.SwearWhyNot`, or not their people) is answered cool
  (it changes little, where it was a refusal costing regard) under `fealtymoot` in place of `fealty`
  (`DiplomacyLetters.Moot`/`IsMoot`, a mark set while the answer is read, nothing saved; `FealtyMootTag`); (4) a press no
  longer pressed when read answers under `pressmoot` in place of `pressed` (`SecretTies.PressRead`'s `moot`), and, the same
  case found in passing, a task over before its letter is read under `taskmoot` in place of its kind's code
  (`TaskLetters.TaskMootTag`, `PressMootTag`); (5) a notable's memory keeps how they were pressed
  (`KeeperMath.PressedKeys`: `task:pressed` and `task:pressedletter` or `task:pressedface`), the facts give `them:pressed`
  either way with `them:pressed:letter` or `them:pressed:face`, and `Rimfolk_OT_Keeper_Pressed` ("your colony's letter")
  asks `them:pressed:letter` through a new code table for shipped pieces found false in a case (`OutsiderTalkMath.AlsoWhen`,
  applied in `Parse`, so the tests see it); `them:pressed` stays both ways, as `!them:pressed` guards the pieces that name
  the one whose secret was told (a save from before keeps only `task:pressed`: their letter piece then stays unsaid); (6)
  the fealty talk header (fealty:oath holds the whole oath, its weight higher for ten days); the headers of the fealty and
  tasks files say what the tags mean now. New words: 8 talk pieces (a refusal by their ways, pressed face to face), 18
  letter lines (fealtymoot, pressmoot, taskmoot, the cordial help line, a refusal of the levies, one pressed face to face).
  Then the release. Pure and tested (`Tools/Tests/RenownTests.cs`, with the six fixes): `Diplomacy/RenownMath.cs`
  (`KnownFor` (Fighters, Healers, Hosts, Traders, Makers, Faithful), `RenownBand` (Infamous, Unknown, Town, Region,
  Peoples); the sources with points, kind, fading and cap; `SourceOf` from the history's kind and role, `SourceOfMark`,
  `Epithets`, `Offices`, `TitlePoints`, `SkillFor`, `FortuneSource`; `Fade` (full a year, a tenth a season to three
  tenths); `Read`; bands at 10, 30, 60 and -15 with a hold of 3; `KnownForOf` (six points and three tenths); `RegardBonus`
  (up to 15, down to -10), `GoodWordFactor` (half again), `MatchFactor`/`MatchChance` (twice, never certain), `Events` and
  `EventFactor` (a quarter, half, three quarters more by band), `GuestFactor`, `ChallengesFighter`/`ChallengeShare` (six in
  ten); the houses' rules (twelve, two or more, twelve people remembered, kept two years); the words' keys; the facts'
  tags). Game side: `Renown.cs` (the colony's houses from `FamilyNames.Families()` as `LedgerHouse` records with `ours`,
  `seat`, `folk` (refs, saved as text), `lastSeen`, `band`, made and kept by the day (`Diplomacy`'s day), a new house's band
  set untold; the reading, kept for the in-game hour: of ours, the history's `dip:contest`, `dip:wedding`, `dip:ward`
  (the guardian is named in a ward's year's end since now, `Wards.Reckon`), `dip:envoy`, `dip:hostage`, `dip:parley`,
  `dip:task` by each person's role, the peoples' marks by name (`ColonistMemory.MarksOf`), the colony's names for them,
  years as physician or marshal, terms as speaker, the Empire's titles, notables whose own regard is 20 or more, and the
  colony's deeds shared (`deed:swore`, `keptWord`, `levies`, `tie:pact:kept`; `brokeTruce`, `brokePact`, `renounced`,
  `schemed`, `refused`, `tributeShort`, `wordBroken`); of theirs, founding, a head's or leader's house, the head's best
  skill, the town's fortune, the history between its head and the colony; a band moved of ours: `dip:renown` (the house its
  actor, a milestone), a `renownup`/`renowndown` bubble by one of it on the map to nobody, `Tell.Minor`; theirs move
  quietly; what it does: `RegardBonus`, `GoodWordFactor`, `MatchFactor`, `EventFactor`, `GuestFactor`, `FightersHouse`,
  `AddTags` for bubbles, `Feuds`), `RenownWords.cs`, `RenownPanels.cs` (the House card of one of the colony's: its people as
  rows, the gone by name, its colony; every House card's renown section (how far and for what, the deeds it came from and
  those held against it, who knows it, what it does) and ties (marriages, wards, feuds); a colonist card's line; the lists'
  line), `FactsRenown.cs` (in the ties' provider: `house:ours`, `:known`, `:renowned`, `:wide`, `:infamous`,
  `house:knownfor:KIND`, `them:house`, `them:house:renowned`, `them:house:feud`; `{HOUSE}`, `{KNOWNFOR}` by the house's age,
  `{RENOWN}` by the ways of the one spoken to, `{THEIRHOUSE}`), `RenownDebug.cs` (dev actions: the day's look now, every
  house's renown to the log, a band told a step up or down, a contest won for someone of a house),
  `World/Ledger/LedgerRenown.cs`. Shared, small: `LedgerHouse` (the fields; a town-less house of ours kept on load),
  `Cards.House` (ours to `RenownPanels.OurHouse`), `HousesPanel` (each family's name a link to its house, its renown under
  it; the region's houses with theirs, the colony's own left out there), `DiplomacyModel` (Here and away's houses link to
  the house, renown beside the repute), `Notables.OpinionOf`, `OutsiderOutcomes.GoodWord`, `Matches.Hear` and `Offers`
  (the roll now after the pick), `Feasts.Answer`, `Happenings.Weight`, `IncidentWorker_Happening.ChanceFactorNow`,
  `HappeningWorker_Champion.Cast`, `Outsiders.Line`, `FactsLedger.Ties`, `FamilyNames.Families`, `ColonistMemory.MarksOf`,
  the setting `diplomacyRenown` ("House renown", Diplomacy). Content: 50 talk pieces (`Rimfolk_OutsiderTalks_Renown.xml`:
  a name known by their ways and the Empire's court, in a town, among every people; known for each of the six; infamous by
  tone; not yet known; their house, its totem and arms; a feud; 16 callbacks on the war, sickness, ill, leader, head,
  harvest, trade, work, family and situation marks, weight 1.2; the rest 0.6), 24 letter lines and 20 bubbles
  (`Rimfolk_Lines_Renown.xml`: by tone and purpose, help and invitations promised only when granting; renownup by the
  colony's age, renowndown, wordyes for a renowned house), keyed words with fellows (`Rimfolk_Renown.xml`,
  `Rimfolk_Renown_Variants.xml`); the Almanac's diplomacy page (a paragraph in its description, a clause in Settings; no
  keyword changes); README, DESIGN.md, the save model (`folkText`, houses with twelve more). Tests changed with it:
  `OutsiderTalkTests` (the tags and placeholders), `FealtyTests` (the levies' tags spoken of in either file), the
  DefValidator knows `HOUSE`, `KNOWNFOR`, `RENOWN`, `THEIRHOUSE`. The generated talks' callbacks (FactsTests, floor 30 of
  1,500) move with every new tag the simulation draws: the renown file's callbacks bring them back over it.
  Left out, and why: feuds' amends (a gift, an apology letter, a marriage between the houses ending a grudge) and their end
  after two generations: new acts and a new letter purpose, to be proposed first; renown from a raid's leader brought down
  and a duel won (no record of either names the colonist; the peoples' marks and the colony's names, the Defender and the
  rest, carry fighters' renown instead); a band moved of the region's houses told or written (eighty houses would crowd
  the history's milestones); Diplomatic Marriage+'s alliance counted once (its alliance is a view without a person to name);
  the matchyes bubble for a renowned house (the colonist who speaks for a match may speak for another). To look at in
  game: a family of two kept as a house (People → Houses: its name a link, its renown under it; the House card), the
  dev action "Renown: a contest won at a feast, for..." and then "the day's look now" (the history line, the bubble, the
  news), a notable's regard of a renowned house's member, the champion's challenge to a fighters' house, talk with a
  visitor that names the house and what it is known for, a letter from one of a renowned house, an oath letter overtaken
  by events (cool, fealtymoot), a refused levies call's talk, a notable pressed face to face.
- 0.98.0, text pass: diplomacy round c (the content pass beside 0.96.0 and 0.97.0, after the 0.97.1 review and merged with it; no
  version of its own: its points go with the next round). Words in new files beside the round's own (its tests cap the
  round's talk files at 40), each said only on facts the providers set (FactsTasks, FactsFealty, the ties, FactsColony's
  deeds, the hamlets'), and in letters only the ties (a letter asks no deeds). Talk pieces, 148: the keeper and the tasks
  92 (`Rimfolk_OutsiderTalks_TasksMore.xml`: the keeper's watch after dark 10, found out 9, pressed 6, a secret of
  theirs told 6 (deed:schemed, wrongbest:schemed with {WRONGNAME}, never to the one told of); the speaker's letters 9, the
  quartermaster's trade 7, the marshal's drill 5, the physician's medicine 4, the warden of guests 6, the chaplain 8, the
  steward's feast 5, the quartermaster's dues 3, any task by the colony's own words (the elders' errands, the council's
  offices, the committee's portfolios, the board's remits), theirs and the tone 10, callbacks on the war, hunger, ill and
  faith marks 4; a `task:you` alternative wherever the officer is spoken of, so the officer is never told about
  themselves), fealty and vassals 56 (`Rimfolk_OutsiderTalks_FealtyMore.xml`: the oath by both sides' ages and who is
  spoken to 14, the collector 2, tribute short 4, levies 5, their band 4, renouncing 6 (deed:renounced, never a clean
  one), callbacks on the head and war marks 2; the colony's own vassals 19: the harvest come, the share heavy or fair,
  their levies, help sent, the hamlet sworn, the colony's protection by its age and theirs by theirs). No new marks.
  Letters (`Rimfolk_Lines_TasksMore.xml`, `Rimfolk_Lines_FealtyMore.xml`): 95 of their words, answers to each kind of
  officer's letter by the tones it can take with a refusal for each (winover 9, trade 7, bringhome 5, theirkin 4,
  keepfaith 4, speakfaith 4, host 5, the quartermaster's dues 7 (new: warm, cordial or cool, true on time or halved),
  pressed 4), the oath offered 8, letters of any purpose while a task runs 13, to one who caught the keeper or was pressed
  3, while sworn 15 (help and invitations promised only in a granting tone), while a town of theirs is the colony's 7.
  Bubbles, 70: soundask 7, soundtalk 8, pressyes 4, pressno 4, keepercaught 4, keeperwatch 6, patrol by the weather 7,
  fealtyoath by the colony's age 7, fealtyterms 6, fealtysworn 6, and 11 for the new moment `taskhome` (an officer home
  from a visit, said to nobody as they come back onto the map; the visit may have been cut short, so it never says how it
  went). Keyed fellows, 67 (`Rimfolk_Tasks_VariantsMore.xml` 29, `Rimfolk_Fealty_VariantsMore.xml` 38): every letter and
  message of the two releases read with `T` has two wordings at least (the eight that had one: `CC_Fealty_Ans_Moot`,
  `_Failed_Friend`, `_BrokenByUs`, `_Harmed`, `CC_Vassal_Ask_Defend`, `_Released_Hamlet`, `_BrokenByUs`, `_Band_Late`),
  the seasonal ones three or four, and they differ by age through {WORD} and {RITE} (`CC_Arms_Fealty`'s {WORD} is the
  levies' word, not the oath's). Code, two lines: `TaskVisits.Back` says the `taskhome` bubble (`Outsiders.Say` to
  nobody), and the keeper's learned-nothing line is read with `T` (`KeeperOfSecrets.Step`), with two fellows each. A
  short tribute can come with nobody at the gate (the review), so none of the new words says their collector came.
  Still thin, and facts that would help: `fealty:levies` is set for a call refused as well as one answered (the history's
  `dip:levy` is written for both), so the new lines that say the levies came ask `deed:levies`, and a tag for each
  (`fealty:levies:sent`, `:refused`) would let talk thank or reproach; their band failing to come this season
  (`fealty:failed`, `Fealty.FailedSeason`); a vassal's town by name and whether the one spoken to lives there
  (`{VASSALTOWN}`, `vassal:here`: the talk says "our kin"), a light share, an ask open or answered, a release given or
  refused (nothing is noted after it); whether a pressed notable paid or refused (`them:pressed` covers both); whether
  the one spoken to is the task's own target (`task:here`: the target may be another town's head, so no line names
  {HEAD}); samples sent; the drill and the medicine after the visit (a visit-only task ends at home, so `task:visited`
  for them holds only on the road back). No hook says the officer setting out (the journey's own `leave` line), the
  collector or a vassal's carter at the gate (couriers carry no purpose tag: `CourierWords.Say` reads goods and
  bring/take only), or the levies leaving (the caravan is the player's own). Told with one wording because the code
  reads them with `P`: the visit's day letters (`CC_Task_Visit_WinOver` and the others, `_Homeward`, `_Back`,
  `_CutShort`, `_CalledOff`, `_TurnedBack`), the samples' message, the tasks' results, the keeper's how-learned and
  learned lines, the informer's message, the learned-nothing line after "Sound out" (its own site, still `P`), the
  fealty messages (`CC_Fealty_Msg_*`, `_Speaker_Goes`), `CC_Vassal_Msg_War`, the dues' and tribute's results.
  Words that disagree with the code, found by the pass, for the next review: `Rimfolk_OT_Fealty_Levies` and two
  `fealty:levies` letter lines in `Rimfolk_Lines_Fealty.xml` say the levies came, also after a refusal (above);
  `[help,cool,fealty:oath]` promises help a cool answer never sends; an oath letter overtaken by events is answered in
  the refusal's tone, so a `[fealty,refusal]` line ("We have enough to protect") is quoted with it; a press answered moot
  is curt with the `pressed` tag, so "You will have your silver" is quoted with no silver coming; `Rimfolk_OT_Keeper_Pressed`
  and the round's pressed lines speak of the colony's letter, though a secret can be pressed face to face; the round's
  talk header says `fealty:oath` is new for ten days, where it holds the whole oath (only its heat is higher then). To
  look at in game: an officer home from a visit (the bubble at the map's edge), the keeper's learned-nothing messages
  over a season, the new talk with a liege's visitor, a vassal's trader, one who caught the keeper.
- 0.97.1, stage 2 round c review: fixes (the releases 0.96.0 and 0.97.0, the keeper of secrets and council tasks, fealty and
  vassals, read against the 1.6 game and Faction Territories' source; no version of its own: its points go with the next
  round). Fixed, each checked in the code and the source: the courier tracker lets a trip go in the pass its people leave
  the map (`CourierTracker`: done and all gone), so the three new readers of a finished trip's tally found nothing. The
  quartermaster's cart for what is owed to the liege was never counted: the debt stood, the cart went again every three
  hours while the stores lasted, "Send what is owed" stayed open beside it (paid twice), and the oath broke at the next
  season's day though paid; the cart now clears the debt when it is sent, as `Fealty.PayOwed` does when the goods are
  carried out (`Vassals.LiegeDues`). An informer paid never gave the keeper's roll (the silver gone, nothing learned, and
  payable again), and samples fetched never moved the trade; the tracker keeps a finished trip a live task waits on
  (`CouncilTasks.Awaits`) until it is read, or two days on, and the samples are looked at every hour. A vassal's dues never
  came: `Vassals.DuesStep` moves the next look on three hours before `Collected` checked it, so after their answer ("their
  carter will bring it") the task ran on a year with nobody at the gate. A vassal's levies could be called, and walk in,
  after their people turned on the colony and before the day's look ended the protection, a band of a people at war
  spawned as the colony's help (`LeviesWhyNot`, `Vassals.Hourly`). With the liege's lord at the gate the speaker was sent
  out every hour even while a raid was on, and drafting the speaker only sent the next best talker; they now wait it out
  (the lord waits up to 32 hours, as before). Two homes sworn to the same people: attacking them broke only the first
  home's oath (`Fealty.BrokenByUs`). Settled, the round b review's open point, by the default the main session chose (the
  player was asked and hasn't answered): a hostage given for a defense pact goes home at the end of each pact year, even
  when the pact is renewed, by their letter or by a marriage alliance (which renews it five days before its end); the
  pact year they were given for is noted at the giving (`WardStay.until`, kept through joining and arriving; a hostage
  given before this takes the pact's year as it stands at the first look), `Hostages.EndOf` reads it
  (`WardMath.EndOf(..., yearOver)`, `WardMath.YearOver`, `YearEndTick`), and the term is kept (`hostageReturned`, the
  tie kept). Their renewal letter says so when the pact had a hostage (`CC_Pact_Renew_Hostage`) and may take a new hostage
  either way as a term of the next year (`CC_Pact_Opt_RenewHostage`, one option for each of `Hostages.PactOptions`, greyed
  with why not; their offer stands, so no roll as in the colony's own letter); a hostage whose term is served no longer
  stands for the pact in `Hostages.Fits`, while the four records a home stay the bound. A marriage's renewal asks nothing
  (it has no letter). DESIGN.md, the design (item 7) and the Almanac's hostage sentence say so; the dev action "Hostages:
  the pact's year out now (their renewal)". Tests: `WardTests` (the year's end, served records, the code paths, the
  words), `FealtyTests` and `TaskTests` (the fixes' code paths). Words: two keyed lines in `Rimfolk_Pacts.xml`. Looked at
  and found sound: no new Harmony targets; Faction Territories read by name as its source has it
  (`Utility.FindForVassalOutpost(FactionTerritories_VassalOutpost)` returning an `Invasion` with `resolved` and
  `attackerFactionLoadId`), its outposts world objects and not settlements, so their heads have no town and are kept by
  `Vassals.KeepsHead` while the outpost stands, then let go by the notables' own rule; nothing of its points, purchases or
  tribute touched; the retinue's lord (the game's `TravelArrived` memo, a defend point at the gate, home on the memo or 32
  hours, fleeing only when the colony harms them, gone in the hour their people turn hostile); the liege's band and the
  vassals' levies through `Pacts.Arrive` (one war band at a time, late after the fight); the liege's levies through the
  calls to arms (kept, or refused at 10 goodwill with the oath standing); the Empire never offered fealty nor taken under
  protection; tribute in silver first, else goods the stores hold, never more than they hold when it is collected; the
  saves (tasks and vassals made on an older save, gone towns, peoples and officers let go by the day's sync and the task's
  hour; no new keep reasons: an outpost's head is a notable); an officer's visit (one away a home with a planned visit
  counted, the planner's own departure, turned back, carried off, waiting on an ambush's map, cut short, the road home,
  a companion never an officer on a task); the keeper's walks and talks only when free; the hours' costs (the mod's
  holdings and the levies alert read once a 250-tick stretch). Not confirmed, left as they are: "Send what is owed" in
  the hours after a short collection counts goods the first errands are still to carry (the errand takes what is left,
  the debt cleared on paper); a struggling town's 30 days are the ledger's days under 15 (`RegionMath.Low`), not under 20;
  the raids that make a colony struggling are every home's, not the one's; with two homes, one home's keeper can press a
  secret another home learned; the truce parley (round a) sends its speaker during a raid as the oath did; the short
  tribute's letter says their collector came when the stores gave nothing (no errand, so nobody walked to the gate); a
  colonist named the colony's hostage can be picked as a visit's companion the same day; the keeper's night is counted by
  the game's clock and after dark by the home's, so far east or west one night can hold two watches. To look at in game:
  the quartermaster's dues with tribute owed (one cart, the debt cleared, "Send what is owed" greyed), a vassal's dues
  (their carter at the gate some hours after their answer), an informer paid (the roll after they leave), samples sent
  (the trade's way moving), their lord at the gate during a raid (the speaker waits), a pact hostage at the year's end
  (the dev action: they go home, their renewal letter with the hostage options).
- 0.97.0, stage 2 release 2c.2, fealty and vassals (docs/design/stage2-diplomacy.md, items 9 and 10; the player's decisions 9, 11
  and 12 as recommended; built on 0.96.0; no version of its own here: its points go with the round). DESIGN.md,
  "Diplomacy" → "Fealty and vassals" has the whole of it. Pure and tested (`Tools/Tests/FealtyTests.cs`, 110 checks):
  `Diplomacy/FealtyMath.cs` (`TributeLevel`, `VassalKind`, `VassalAsk` (Help, Judgement, Camp, Defend, Release,
  Protection), `FealtyObjector`; who may swear (goodwill 40, two towns in reach, never the Empire, one liege), their
  answer (half the time at a mean of 40 between goodwill and their head's regard), their offer (raided twice in a season
  with five fighters or fewer, once a year a home), the objector's order (an avenger, pride 30, a devout one of another
  faith), tribute by wealth (150 at 15,000 to 600 at 150,000, in tens), the owed and broken clock (20 goodwill), levies
  (PactMath's two fighters within five days; refused 10 goodwill), protection (once a season, nine in ten, never against
  their friends), renouncing (30, nothing after a failed season); protected towns (under 20 for 30 days, at peace with the
  colony and at war elsewhere, two a home, one ask a year), produce by level (six tenths, as it comes, eight fifths),
  heavy's season (3 goodwill, 4 regard), light's (2 regard), a protected town's produce, the quartermaster's dues (40 to
  100 by fortune and level, half and explained under 20), a vassal's levies (a third of the threat, once a season, 6
  regard a death to 20), one ask a season and which (`AskOf`), the camp's distance (8 tiles), each answer's regard, release
  (-20 for a season; refused, 15 goodwill), the facts' tags). Game side, all in `Diplomacy/` unless named: `Fealty.cs`
  (the liege's tie, origin `fealty`, key `liege` or `liege:failed`, number the next tribute's day, count the silver owed
  with the tie called in, days the band's last day, goods PactMath's `band:` and `arms:` codes; `SwearWhyNot`, `Hear`,
  `Agreed` and the oath awaited on `DiplomacyHome` (`oathPeople`, `oathDue`, `oathAt`, `oathOffered`, `fealtyOfferAt`,
  `protectAskAt`), `Swear` (a scenario's grant from the same people ends), their offer (`ChoiceLetter_FealtyOffer`), the
  hour: protection, their band through `Pacts.Arrive(..., "fealty", words "fealty")`, the levies through
  `CallsToArms.PactHour`, tribute carried out to their collector (`CollectTribute`, `DilemmaEffect_CarryOut`), owed, paid
  (`PayOwed`, `Paid`), broken; levies came or refused; `Renounce`; `BrokenByUs` from `PactsHooks`; `Titled` for the
  Empire's own), `FealtyOath.cs` (`LordJob_FealtyOath`, `JobDriver_FealtyOath`, their head or one of their people and a
  guard at the gate, the speaker), `FealtyObjection.cs` (the objector, `Rimfolk_DlmFealty_Swear` and
  `Rimfolk_DlmFealty_Renounce` with their workers, context and effects), `FealtyLetters.cs` (`LetterPurpose.Fealty`,
  last in the enum, topic `fealty:swear:ID`), `Vassals.cs` (`VassalTerms` on `DiplomacyHome.vassals`, three a home: the
  hamlet once sworn, synced daily, and protected towns with their own tie, origin `vassal`, key `protected`; the day, the
  harvest, struggling towns' asks and `Take`, the mod's outposts' faces and asks, `ChoiceLetter_VassalAsk` and `Answer`,
  promises as Promise ties of origin `vassal` with their day in the terms, the tribute level, the quartermaster's dues
  (`DuesWhyNot`, `DuesStep`, `DuesRead`, `QuartermasterOn`, `Punctual`, `DuesTarget`, `DuesTown`), levies (`LeviesWhyNot`,
  `CallLevies`, the hour, `LeviesHome`), `Release`, `BreakAway`, `BrokenByUs`, `Regard`), `FealtyPanels.cs` (the dues'
  hooks set, the tie's, people's and place cards' lines and acts, the inbox `fealty`, `fealty-under-way`,
  `fealty-coming`, `Alert_VassalLevies`), `FealtyWords.cs`, `FactsFealty.cs`, `FealtyDebug.cs` (dev actions: their
  offer, an oath agreed with their lord due now, sworn at once, the tribute due, owed and due, their call on the levies,
  protection looked for, renouncing asked; a town's ask for protection, a town taken at once, an ask of each kind, a
  protected town's harvest, levies due, a season passed), `World/VassalNotables.cs` (`StaffVassal`, `StaffOutpost`),
  `World/Region/HamletVassal.cs` (`ReleaseOath`, `BreakAway`). Shared, small: `Pacts.Arrive` (words and strength),
  `CallsToArms` (a liege's call: its words, kept, refused, `WaitingOnLevies`, `KeptLately` reads `dip:levy` too),
  `PactsHooks` (breaches of a liege's or protected town's people; Factional War names the liege, `CC_War_Liege`, and a
  liege's enemy fought is `sidedWith`), `WarCamps.Thank` (a vassal's dead through `Vassals.LeviesHome`; a liege's fallen
  as a pact's), `Notables.Look` (a vassal's head kept), `FavourCalls.HeadOf` (an outpost's head never a people's head),
  `HamletLife.Harvest` (`Vassals.HamletProduce`), `TaskResults.LetterRead` and `CouncilTasks.PeopleTargets` (Dues),
  `TerritoriesAdapter` (`InvasionAtOutpost` by `FindForVassalOutpost`), `DiplomacyModel.Card` (rows and acts; the sworn
  hamlet's tie not twice), `LensModel` (Ours: vassals in gold, lines to the mod's outposts), `DiplomacyBook`, `Diplomacy`
  (the hour and the day), `FactsLedger`, `DiplomacyLetters`, `NotableMath` (`LetterPurpose.Fealty`), the setting
  `diplomacyFealty`. Defs: `Defs/Misc/Rimfolk_Fealty.xml` (two letters, the oath's job), `Defs/DilemmaDefs/
  Rimfolk_Dilemmas_Fealty.xml`. Content: 31 talk pieces (`Rimfolk_OutsiderTalks_Fealty.xml`, three of them callbacks to
  the war, raid and harvest marks; weights 1.2 or less, no new marks: new marks with nothing to pick them up pushed the
  generated talks' callbacks under their floor), 27 letter lines and 16 bubbles (`Rimfolk_Lines_Fealty.xml`), keyed words
  (`Rimfolk_Fealty.xml`) with 32 fellows (`Rimfolk_Fealty_Variants.xml`); the Almanac's diplomacy page (two paragraphs
  in its description, a clause in Settings; no keyword changes, no new sections); README, DESIGN.md, the save model
  (`vassals`), the play-out skip for the oath's question (the speaker's walk is its own job). Tests changed with it:
  `DiplomacyTests` (Fealty after Task in the purposes), `TaskTests` (the purposes' order, the band's strength),
  `OutsiderTalkTests` (the tags); the DefValidator knows `WORD` in a dilemma. Words: "their lord" is said only through
  {LORD} (the one who came); every other line says their head or one of theirs, so a tribe's chief reads true.
  Entry points for 2c.3 (schemes): `Fealty.Of(home)`, `Vassals.All()`, `Vassals.Regard(town, shift, why)` for a scheme's
  effect on a vassal's head, `FealtyObjection.CouncilBusy`. For 2d.1 (renown): the deeds `swore`, `levies`, `tribute`,
  `tributeShort`, `renounced`, `protected`, and `dip:tribute`, `dip:levy`.
  Left out, and why: their tribute by their ways in kind (a tribe taking furs, a company taking silver): silver first,
  else the colony's most plentiful goods, for every people; "levies for up to five days" read as set out within five
  days (the calls to arms), how long they stay being the far fight's; a vassal's "help in hard times" as a letter with
  goods carried to the gate, not the help missions (their missions are the neighbours', not a vassal's); a Faction
  Territories outpost whose first people are at war with the colony gets no head (it would be a hostile notable: silent
  letters, a sour regard) and says so on its card; a vassal's judgment names the neighbouring town without a quarrel of
  the world's own (there is none to read); renouncing by letter (a council question, its word sent with the answer);
  the quartermaster's dues of Faction Territories' vassals (decision 12). To look at in game: "Swear fealty..." on a
  people's card (the terms and who will object in the dialog, the letter), their answer, their head (or one of theirs)
  and a guard walking to the gate by day, an objection before the council, the speaker's walk and the three bubbles, the
  sworn letter; their offer after two raids; a season's collection (silver, or goods carried out by the gate's errand,
  their courier taking it with "goods come and go" on), short and owed, "Send what is owed", the oath broken a season on;
  a raid with their band walking in, and a failed season; their call on the levies and the caravan's far fight; renouncing
  clean and costly; the Empire's card with a titled colonist; a hamlet sworn (the tribute level on its card, a heavier
  harvest), a struggling town's ask and the town under protection (its head named at once), a vassal's ask of each kind,
  "Call their levies" and the alert during a raid, their dead on their head's card, a release asked and refused; the
  quartermaster's dues with a vassal (the letter, the carter) and with the liege (the cart); with Faction Territories,
  an outpost's head, its card and an invasion's ask; the Ours and Stance lenses; This week's rows at 768p.
- 0.96.0, stage 2 release 2c.1, the keeper of secrets and council tasks (docs/design/stage2-diplomacy.md, item 8; the player's decisions
  13 and 14 as recommended, 9 for the Empire; built on 0.95.0, merged in before the work began). DESIGN.md, "Diplomacy" →
  "The keeper of secrets and council tasks" has the whole of it. Pure and tested (`Tools/Tests/TaskTests.cs`):
  `Diplomacy/TaskMath.cs` (`TaskKind` (WinOver, Secrets, Scout, Drill, Trade, Dues reserved, Tend, BringHome, TheirKin,
  KeepFaith, SpeakFaith, Host), `TaskStage`, `TaskStep`, `VisitStage`, `TaskTarget`, `SecretRoll`; the eight offices and
  their kinds, targets, who writes and who visits, one away a home and a visit a season, the letters' four to six days,
  giving up after a year or a season, `RegardUp`, `ToneShare`, the win at 60 and its result, `TradeGain` and its result,
  the patrol at dawn and `SpotChance`, `LevyFactor` 1.2 for a season, the physician's medicine and `SickBackChance` 0.2,
  `PriceFactor`, `KinSooner`/`KinHigher`, `Shaken`, `Welcome` 1.5, This week's steps, the facts' tags) and
  `Diplomacy/KeeperMath.cs` (`SlipFactor` 0.5, `CatchFactor` 2, the watch after dark, `LearnChance`, `Roll` (found out a
  tenth), their secrets' kinds and weights, twenty live a year, `ComplyChance`, `Hush`, the regards, the tags). Game
  side, all in `Diplomacy/`: `TaskBook.cs` (`CouncilTask`), `CouncilTasks.cs` (reading, why not, targets, the choosers
  (an office's kinds, a people's tasks), the council's question, `Set`, `CallOff`, `End`, the hour (visit, why it can't
  go on, `Finished`, the year's and season's ends, the kind's step), letters (`LetterHour`, the speaker's invitation),
  the marshal's patrol, the quartermaster's samples, `Free`, `SendCart`, `HomeLost`; the 2c.2 hooks `DuesWhyNot`,
  `DuesStep`), `TaskResults.cs` (each kind's letter read, a visit's day, `Finished`, won over (`Pacts.Offer`, a trade
  pact in the pacts' own letter with its own words, a favour), the trade open (a trade pact, their trader's caravan
  queued, a favour), the physician's day with the game's flu one visit in five; `Stewards`, `KinTask`, `LevyStrength`
  read by Feasts, Ransoms and Pacts), `TaskVisits.cs` (the journey plan for the officer and a companion, its steps
  Planned → Road → There → Homeward, why not), `TaskLetters.cs` (`LetterPurpose.Task`, hidden from the dialog; `Send`,
  their words' tags, `Read`), `KeeperOfSecrets.cs` (the guard, the watch after dark with the game's `FollowClose`,
  overhearing, hard words owed, the task's hour (a talk once a day, the informer at the gate), `Step`'s roll, "Sound out"),
  `SecretTies.cs` (`Learn`, reading, pressing by letter and face to face, the answer, the day), `TaskJobs.cs`
  (`JobDriver_TaskPatrol`, `TaskDefOf`), `TaskPanels.cs` (the officer's, notable's, Secret tie's and people's cards, the
  Offices row, Here and away, the inbox `task` and `task-under-way`), `TaskWords.cs`, `FactsTasks.cs`, `TaskDebug.cs`
  (dev actions: a task set now, every task's next step, a task's end, the caravan at the town, the day there over, a
  secret learned, the keeper found out, the watch, a pressed secret answered, the patrol). Shared, small: `NotableMath`
  (`LetterPurpose.Task` last, `WaitDays(Task)` 3, `PurposeBase(Task)` 0), `OutsiderAct.SoundOut` last (the menu after
  "Offer an exchange", an ask), `DiplomacyActs` and `DiplomacyLetters` delegating (`DiplomacyActs.Chose` for the keeper's
  own talks), `NeighboursPanel` (the dialog skips Task), `DiplomacyBook` (`tasks`, `drillPeople`/`drillAt`, `AnyWaiting`,
  homes lost), `Diplomacy.Hourly` (the tasks' hour, the keeper's hour, both days), `DiplomacyModel` (`tasks`, the officer
  away), `HereAndAwayView` (the tasks' section), `DecreePanel` (one line under each seat), `Secrets.cs` (the slip factor),
  `Feasts.End` (the welcome), `Ransoms` (`DemandFor`; their kin's offers), `Pacts.Arrive` and `WarCamps.MakeFighters`
  (the band's strength), `RaidersCamps.SpotByPatrol`, `FactsLedger` (one call), setting `diplomacyTasks` (with offices).
  Defs: `Defs/OfficeDefs/Rimfolk_Offices_Keeper.xml` (its planks reuse the pools' `gate` and `watch`),
  `Defs/HediffDefs/Rimfolk_Hediffs_Keeper.xml`, `Defs/Misc/Rimfolk_Tasks.xml` (the patrol job). Content: 28 talk pieces
  (`Rimfolk_OutsiderTalks_Tasks.xml`, three of them callbacks to the existing trade, sickness and head marks), 32 letter
  lines and 25 bubbles (`Rimfolk_Lines_Tasks.xml`), keyed words (`Rimfolk_Tasks.xml`) with 24 fellows
  (`Rimfolk_Tasks_Variants.xml`); the act's words beside the others in `Rimfolk_Outsiders.xml`; the Almanac's offices
  page (the seats' table and the colony's life, a clause in the description) and diplomacy page (two paragraphs in its
  description, a clause in Settings; no keyword changes, no new sections); README, DESIGN.md, ROADMAP's offices row, the
  save model (`tasks`). Tests changed with it: `DiplomacyTests` (the purposes and acts in the order built, Secret ties
  in use, schemes not yet), `OutsiderTalkTests` (the ask, the tags, the placeholders), `PactTests` (the band's call); the
  DefValidator knows `OFFICER`, `OFFICE`. The talk pieces' weights stay at 1.2 or less: more weight pushed the generated
  talks' callbacks (FactsTests) under their floor.
  Entry points for 2c.2 (fealty and vassals): `TaskKind.Dues` is in the enum and the quartermaster's kinds but not offered
  until `CouncilTasks.DuesWhyNot` (why a vassal can't pay now, null when it can) and `CouncilTasks.DuesStep` (the hour of
  a dues task: the carter, the goods, what came of it) are set; `CouncilTasks.SendCart` carries goods to a people by
  courier; the record's `asked`, `progress`, `tripId` and `carts` are free for it; the words `CC_Task_*_Dues` exist. For
  2c.3 (schemes): `KeeperOfSecrets.Watching(map)` (the keeper walking with guests tonight) with `KeeperMath.CatchFactor`;
  `SecretTies.Learn(Ref knower, Ref about, kind, by)` makes their spy's Secret tie about a colonist (any knower), `Of` and
  `About` read them, and pressing is the ledger's called in → kept or broken (`SecretTies.Answer` is the colony's side;
  theirs needs its own demand); `KeeperMath.Kinds` are theirs (a colonist's secrets are `SecretDef`s, for a spy to learn).
  Left out, and why: the speaker's "speak for us" in a dispute world event (its mediation stage already puts the colony's
  best talker between the families: the speaker would be the same person most of the time); the marshal's "strangers
  seen" on patrol (nothing on the map to see that the game doesn't show already); the quartermaster's goods ordered for
  their next caravan (their caravan sent instead: an order would need the player to pick goods with no window for it);
  the warden's lower price for an exchange (their price for ransom only; an exchange is weighed by worth and regard, and
  the warden's letters raise their head's regard); the chaplain's notables' certainty shown anywhere but their answer
  (the game keeps it on a world pawn); "guests received" for tasks but the speaker's; a visit's officer and companion
  chosen by the player beforehand (the journey planner changes who goes). To look at in game: setting a task from an
  office's row, an officer's card and a people's card (the council's question with its risks); the speaker's letters and
  call and the head's answers (their words, the regard on the town panel and the memory line), the invitation and the
  head's visit, the offer at 60; the quartermaster's samples fetched at the gate and their caravan; the marshal's dawn
  patrol round the edges and a hidden camp seen; a visit planned for the next morning (the planner showing it, the
  night before, packing, the gate), the caravan at the town for a day, the letter of the day, home again; the physician's
  medicine handed over and the flu brought home; the warden's price at the gate; the steward's feast with the welcome;
  the keeper's watch after dark, "Sound out" with a visitor and with a notable whose secret the colony knows, the informer,
  a secret pressed by letter (paid, or refused and told), found out and the hard words at the next visit; the Decrees
  page's rows at 768p with eight lines under the seats.
- 0.95.0, text pass: diplomacy round b (the content pass beside 0.92.0, 0.93.0 and 0.94.0, after the 0.94.1 review and merged
  with it; no version of its own: its points go with the next round). Words in new files beside the round's own (its
  tests cap the round's talk files at 40), each said only on facts the providers set (FactsFeast, FactsWards,
  FactsMatch, FactsColony's deeds, FactsWho's kin) and, in letters, only the ties and the history (a letter asks no
  deeds). Talk pieces, 127: feasts 40 (`Rimfolk_OutsiderTalks_FeastsMore.xml`: the day by the hour and the weather, the
  retinue, seats and prayers, the Empire's music and a titled host, a guest hurt, a cool cup and a sour word; before the
  day; after it by how it went and who won, an empty plate, a quiet feast without contests; deed:feasted, deed:feast
  (theirs attended), deed:absent), wards 36 (`Rimfolk_OutsiderTalks_WardsMore.xml`: lessons and mistakes, homesick,
  settling and happy, a child ward's own, the colony's hunger, plenty, a birth, a wedding, rain, winter; their people
  about theirs; the colony's youth by their ways and the Empire's page; the grown ward back as heir, head, or cool),
  hostages 17 (`Rimfolk_OutsiderTalks_HostagesMore.xml`: theirs at work, watched, asked about walking home, at night,
  in a raid, the defence pact they stand for, friends at the term's end, forfeit by tone; their people about theirs and
  the colony's, held for a price; the custom by their ways), marriages 34 (`Rimfolk_OutsiderTalks_MatchesMore.xml`:
  lovers and sweethearts, ages, the Empire's court; a match agreed by their ways, naming nobody; the wedding lately;
  in-laws, kin:you, one of the couple; the pact called, the Empire's household, a raiders' camp watched, a raid, a
  death; held through a child; the grudge, its ways, a head who holds it; a marriage over). Letters
  (`Rimfolk_Lines_FeastsMore.xml`, `_WardsMore.xml`, `_MatchesMore.xml`): 91 of their words, answers by the tones each
  can take (feast 10, ward 16 with the page, match 8) and letters of any purpose while the thing stands (feast 17, ward 11,
  hostage 9, marriage 20; the marriage ones name their purpose, as the round's own `[tie:marriage]` lines don't, so
  those are said only when no line of the purpose fits). Bubbles, 84: feasts 46 (the welcome, the guest's answer, the
  race, the hunt, the tables, the shooting, toasts as the champion is named, the prize, a sour night; and 7 for the
  new moment `weddingarrive`), wards 17, hostages 5 (the colony's hostage as the terms are sworn), marriages 16 (Speak
  for a match, written to hold whether the colonist speaks for themselves or another: the one spoken to is always the
  one matched; the farewell at the gate). Keyed fellows,
  111 (`Rimfolk_Feasts_Variants.xml` 23, `Rimfolk_Wards_VariantsMore.xml` 56 with 14 for hostages and three more
  letters home for each age and the page, `Rimfolk_Matches_VariantsMore.xml` 32, three of them for 0.94.1's
  `CC_Match_WedQuiet`, `_Agreed_ColonyAlone`, `_WrongedNoPact`): every letter and message of the round read with `T` now
  has two wordings at least. Variants are picked at random, so they differ by age only through {WORD}, {RITE}, {FEAST}
  and the keys the code builds by age (the letters home). Code, two lines: a wedding's guests answer the welcome at the
  gate with their own moment (`Feasts.Greet`: `weddingarrive`, where they said a feast's "our best runners"; named
  outside FeastTests' `feast*` moments, whose lines it looks for only in the round's own file, where textcheck lets no
  new tag in; `MatchTests` checks the moment and its lines); the in-laws' letter about a raiders' camp has its own words
  (`MatchWarnings.cs`: the suffix `Kin`, so `CC_RaidCamp_Found_Text_Kin` and its history line; `MatchTests` pins the new
  literal, where it pinned "Letter", and the key). Fixed in passing:
  `[feastwelcome,us,!lowtech]` promised contests that afternoon, also at weddings and contest-less feasts;
  `CC_Match_End_Death_2` said nobody was to blame, before the untended or open death is added to it; hostage fellows
  avoid {WORD}, which is "a hostage for the pact" in the middle ages even for a truce.
  Still thin, and facts that would help: whether the colonist speaking is the ward's guardian (`us:guardian`), one of a
  match agreed (`us:matched`, `them:matched`: the agreed match's talk names nobody), or the colony's own who married
  away visiting as one of theirs (`them:spouseof`); the wedding's guests on the day (`match:today`: the wedding feast
  sets no feast tags, so its talk is match:agreed's); a feast under way by stage (the contests, the feast, the night) and
  whether the one spoken to is a guest at it (only role:notable tells the invited from their retinue); the ward's days
  here or their year nearly over, and their skill by name (no {SKILL} in talk); the colonist the council pressed into a
  match (`us:pressed`); a hostage's term by what it guarantees (truce or pact) in talk. Told with one wording because the
  code reads them with `P`: the contests' start (`CC_Feast_Msg_Begins_*`), the ward and the hostage on the way
  (`CC_Ward_Msg_Comes`, `CC_Hostage_Msg_Comes`), their kin coming for the ward (`CC_Ward_Msg_KinCome`), the colony's
  youth home or setting out, the pacts' paragraphs in the match letters (with and without a pact). The in-laws' camp
  letter is read with `Translate`, so it has one wording of its own. To look at in game: the wedding guests' word at the
  gate; the in-laws' camp letter; a ward's letters home over a year in each age.
- 0.94.1, stage 2 round b review: fixes (the releases 0.92.0, 0.93.0 with 0.93.1, and 0.94.0: feasts and contests with guests,
  wards and hostages, marriage alliances, read against the 1.6 game; no version of its own: its points go with the next
  round). Fixed, each checked in the code and the game's source: the marriage agent's doubt, a wedding late in the day
  and the evening's feast. The game's ceremony is one lord through a party before the vows (5,000 ticks at least and
  both at the spot), the vows (2,500) and a party after them (7,500), and while it lasts no other gathering begins
  (`LordJob_Joinable_MarriageCeremony.AllowStartNewGatherings`); the feast's play-out waits on any such lord
  (`MeetingAttendance.WhyNotNow` → `GatheringsUtility.AnyLordJobPreventsNewGatherings`) for its hours (to 21:00, or two
  hours on) and three more, then falls through. `Matches.WeddingSpot` let the feast begin the moment the two were
  married (the plan is gone once wed), so a ceremony begun from about 15:00 on left the guests with "the feast fell
  through" and no table, against the arrival letter's word; and a ceremony tried in the 16:00 hour raced the feast begun
  then (either one keeps the other from starting). Now the guests stay by the couple, and the feast waits, through the
  party after the vows too (a live marriage of the guests' people whose colonist is in a game ceremony on the map), and
  with the guests here the ceremony is begun only before the feast's hour (`MatchMath.CeremonyHour`; otherwise the next
  morning); a feast begun late keeps its existing hours (to 23:00) and the gathering's def ignores the timetable. Settled
  as sound: no colonist is held between two lords (a pawn has one; the game's voluntary joining moves only to a higher
  priority, the fiancés' 100 over the guests' and gatherings' 20), the feast's guests never join the ceremony (its
  guests are the couple's own people; theirs is `LordJob_FeastGuests`, not joinable) and leave on their own memo, and
  neither gathering calls the other off. The wedding's letter said kin were among the guests when the one married in
  walked in alone or the guests had gone home (`CC_Match_WedQuiet`), and that the defense pact runs on with pacts off
  or none made (`CC_Match_Wed_WhatNoPact`). The agreement's letter promised the wedding's guests and the evening's
  feast when none of their notables was free to come (`Host` sets `alone` before it is written:
  `CC_Match_Agreed_ColonyAlone`); it and their offer said the alliance answers calls "as a defense pact does" for the
  Empire (no pact, decision 9) and with pacts off (`CC_Match_Agreed_RisksNoPact`, `CC_Match_WhatNoPact`), and the wronged
  letter said "the pact with it" where none stood (`CC_Match_WrongedNoPact`). A spouse married into the colony and
  carried off by raiders goes over to their captors in time (the game's `KidnappedPawnsTracker`, a 30-day MTB), and the
  alliance's daily look took that for a sale: the marriage broken, `spouseWronged`, a grudge, the pact broken and a
  hostage given for it held; `MatchMath.SoldAway` leaves out a people at war with the colony. Tests: `MatchTests` (the
  ceremony's hours, the sale, the code paths, the letters' keys). Words: six keyed lines in `Rimfolk_Matches.xml`, new
  keys only (the text pass beside this review writes in new files). Looked at and found sound: the Harmony targets in
  reach (`Pawn.Kill` for wards and spouses with `dinfo`, `MarriageCeremonyUtility.Married`); the guests' lords (never
  of a people at war, out at the edge on a memo, fleeing only when harmed by the colony: `Trigger_PawnHarmed` wants the
  colony's instigator, a bout's blow has none and a brawl is excused with the deeper hooks, which the sour night needs;
  a guest downed leaves the group by the game's rule and the look sees them dead, held or gone; two days at most); the
  kin's and the escort's parties (guests of a people at peace with both for a people at war); wards and hostages as the
  game's lodgers (its arrest, death and leaving: `QuestPart_ExtraFaction.Notify_PawnKilled`,
  `LeaveQuestPartUtility.MakePawnLeave`), walking out alone as the colony's lodger; the contests (every job fails or ends
  without its run, so a reload frees them; the hunt only at wild, calm, unbonded, unvenerated game of 2.5 body size or
  less with a low manhunter chance, never the colony's; the race's turn in bounds, open, reachable; the shooting match
  fires nothing; the bad sides at the design's rates, never a child, the pregnant or one hurt in three days, children in
  races only); the saves (lists made on an older save, discarded refs dropped, `ward`, `hostage`, `spouse` and `lent` in
  the ledger's daily check, the lodger quest kept by the game); nothing kept forever but by its own rule (a forfeit
  hostage slips away after five days, the colony's held one joins their captors in time and is let go); a ward of a
  people turned hostile stays the year and walks out alone, the colony's youth with them comes home; a truce run out in
  war sends both hostages home, broken by them makes theirs forfeit; the looks every 250 ticks (one in 200 a home), the
  escort's every 60. Not confirmed, left as they are: a hostage given for a defense pact stays while the pact is renewed,
  by letter or by a marriage with the same people (which renews its pact while it holds), "for the term" read as the
  pact's life (the colony can end the pact by letter); the one to be married joins the colony where the guests walk in
  (the map's edge), not at the gate the letter names; a colonist's stray shot hitting a guest in the hunt ends the feast
  as an insult (the game's own harm trigger, as for its visitors; only bouts and brawls are excused); a party before the
  vows long enough to carry the one after past midnight leaves the guests at the gate, without beds, until the next
  evening's feast; with "Answers play out on the map" off, a wedding feast begun after 21:00 serves nobody; a ward or
  spouse killed by a colonist's stray shot or in a berserk counts as at the colony's hand (a feud); a hunt broken up by
  a reload leaves its game marked for the colony's hunters; a hostage or ward still walks in when what they were given
  for ended in the hours before, joins and walks out again (or is forfeit at once). To look at in game: a wedding whose
  guests come at 14:00 and a ceremony begun at 15:00 (the guests by the couple through the party after the vows, then
  the feast at 21:00 to 22:00, first light); a wedding not begun by 16:00 (the feast that evening, the ceremony the next
  morning and its quiet letter); a match agreed when no notable of theirs is free; a match with the Empire's titled.
- 0.94.0, stage 2 release 2b.2, marriage alliances (docs/design/stage2-diplomacy.md, item 6; the player's decisions 8 and 9 as
  recommended: where the couple lives chosen in each match, a child keeps the alliance after a death, the Empire's titled
  marry only the titled; built beside 2b.3 and merged with 0.93.1 at 5061918, both kept). DESIGN.md, "Diplomacy" →
  "Marriage alliances" has the whole of it. Pure and tested (`Tools/Tests/MatchTests.cs`, 308 checks):
  `Diplomacy/MatchMath.cs` (`MatchWay`, `MatchHome`, `MatchStage`, `Consent`, `ConsentWhy`, `MatchWrong`; `WhyNot` by the
  game's answers, `EmpireFits`, `ConsentOf`, `AcceptChance`/`Accepts`, `MayOffer` (regard 40, a year, three a people),
  `TheyPropose`, `LeadDays`, `ArriveNow`, `RoadDays`, `GiftValue`/`GiftCount`, `RegardBonus` 15, `KinGiftDue`, `Warns`,
  `PactAgain`, `EndsAtDeath`, `DiedRegard`, `WrongOf`, `PassesDown`, the regards, the topic, ask and feast-tag codes,
  the facts' tags). Game side, all in `Diplomacy/`: `MatchBook.cs` (`MatchPlan`), `Matches.cs` (reading, who may
  marry, the colonist asked first, candidates (heirs, envoys, faces the colony knows, a son or daughter of a head's
  house made by `MakeKin` when they agree), `Hear`, `Begin`, `Agree` (fiancé, gifts both ways by `Couriers.Exchange`,
  the day), `Pressed`/`Withdrawn`, their offer (`Offers`, `Pick`, `Offer`, `Accept`, `Decline`), `Off`, `HereAs`,
  `MatchDefOf`), `MatchDays.cs` (the hour: hosting the wedding's guests through `Feasts.Host`, `OnStage`, the one to be
  married joining at the gate, the game's ceremony 5 to 16 with the guests by the couple (`WeddingSpot`), the council's
  wedding plan after three days; their kin for the colony's own (`SendEscort`, `EscortArrived`, `TakeLeave`/`Departs`),
  the wedding at their town; `Married`, `Wed`), `MatchAlliance.cs` (`RegardBonus`, the day: a death and a child,
  wrongs, theirs gone home, parted, the kin's gift, the camp warning, the pact kept and made again; `Wronged` with the
  grudge, `Feuds`; `Concerns`/`Dying`/`Died` for the kill hook), `MatchParty.cs` (`LordJob_MatchEscort`,
  `JobDriver_MatchLeave`), `MatchHooks.cs` (the feasts' event and focus; `Patch_MatchKilled` with the deeper hooks),
  `MatchObjection.cs` (`Rimfolk_DlmMatch_Against` in `Defs/DilemmaDefs/Rimfolk_Dilemmas_Match.xml`: `MatchContext`,
  `DilemmaWorker_MatchAgainst`, `DilemmaEffect_MatchPress`, `DilemmaEffect_MatchWithdraw`), `MatchLetters.cs`
  (`LetterPurpose.Match`; `ChoiceLetter_MatchOffer`), `MatchActs.cs` ("Speak for a match"), `MatchPanels.cs` (Tie, Person,
  House, People and Colonist cards; rows, `SeekAct`, `AwayRows`, `InlawRows`; the inbox `match`, `match-under-way`,
  `match-coming`), `MatchWords.cs`, `MatchWarnings.cs` (`RaidersCamps.WarnedByKin`, a partial of the camps),
  `FactsMatch.cs`, `MatchDebug.cs` (dev actions: their offer now, a match agreed now, their kin due now, the wedding at
  their town now, each marriage's day, a spouse wronged). Shared, small: `LetterPurpose.Match` (after Ward) and
  `OutsiderAct.SpeakMatch` at the ends of their enums (in the menu after "Speak of a pact"), `OutsiderTalkMath.Asks`,
  `DiplomacyActs`/`DiplomacyLetters` delegating, `DiplomacyBook` (`matches`, `AnyWaiting`, homes lost), `Diplomacy.Hourly`
  (the hour and the day), `DiplomacyModel` (rows, act, away, in-laws, `WhyHere`), `KeepFor.Spouse` and its daily check,
  `NotableBook.Baseline` (+15 while it holds, not while at war), `Patch_Married` (one line), `Feasts` (`HereFocus`: the
  guests stand by the couple, and the feast waits while the ceremony runs), `FactsLedger` (one call), setting
  `diplomacyMarriage` (section Diplomacy). Defs: `Defs/Misc/Rimfolk_Matches.xml` (the offer's letter, the leaving job),
  `Rimfolk_MatchPressed` (-6, 20 days; rare: only when the council presses a match) with its mood hint. Content: 28 talk
  pieces (`Rimfolk_OutsiderTalks_Matches.xml`), 25 letter lines and 27 spoken lines (`Rimfolk_Lines_Matches.xml`),
  keyed words (`Rimfolk_Matches.xml`) with 24 fellows (`Rimfolk_Matches_Variants.xml`), the Almanac's diplomacy page
  (two paragraphs in its description and a clause in Settings; no keyword changes), README ("Marriage alliances"),
  DESIGN.md, the save model (`matches`). Tests changed with it: `DiplomacyTests` (the purposes and acts in the order
  built, the menu), `OutsiderTalkTests` (the ask, the tags, the placeholders); the DefValidator knows `INLAW`,
  `SPOUSEOF`, `LIVES` and the new dynamic keys.
  Left out, and why: challenges at the gate in a feud carried down (the harm review's 2.9.1 and 2.9.2, not picked); a
  marriage counted toward the colony's renown, Diplomatic Marriage+'s once (round d); a spouse's house as the tie's
  side (round d: the tie runs from their people, the grudge from their head); the in-laws visiting in person each season
  (their gift comes by their courier); the heart's belonging for a spouse among strangers (the game's own moods carry
  it); with the deeper hooks off, a death by the colony's hand is seen as a death, not a wrong (prison, slavery, sale and
  banishment are still seen by the day's look); the Empire's honor (decision 9 names two other places). To look at in
  game: "Speak for a match" with a lover of the outsiders' courting and with a notable at the colony (the menu's
  feelings, their answer, the agreement's letter and the gifts at the gate); a letter from a people's card ("Seek a
  match") and a notable's ("Propose a match"), a house's son or daughter made on their yes; their offer by letter;
  a colonist against it before the council, pressed and withdrawn; the wedding's guests at the gate, the spouse joining,
  the ceremony with the guests by the couple and the feast after it; a ceremony that can't start (danger), and the
  council's own plan after three days; their kin at the gate for the colony's own, the walk out, the farewell, leaving,
  and the letter from their town some days on; the kin's gift a season later, a raiders' camp found by their letter, the
  pact on the people card and called by fire or console; a spouse's death with and without a child; a divorce; a spouse
  arrested (the grudge's letter, the pact broken), and the grudge passing when their head dies; the Tie, House, Person,
  People and Colonist cards, Here and away, the inbox and the Peoples view; Diplomatic Marriage+'s alliance as a row.
- 0.93.1, two of the player's reports: (1) an ideology mod's precept "has automated turrets: scorned"
  (`GR_HasAutomatedTurrets_Scorned` in the player's save) gave its mood to everyone because a hacked terminal in a
  hatch's pocket map handed the colony that map's ancient armored turret; `Integrations/TurretFaith.cs` finds every
  precept whose own or issue defName says Turret, patches its situational thought's worker (`ShouldHaveThought` or
  `CurrentStateInternal`, found at start) with a postfix that drops the mood when the pawn's map has no automated
  turret of the colony's (`Building_Turret` without `CompMannable`, cached per map per 250 ticks); setting
  `faithTurretsHere` (deeper hooks). (2) Babies carried the decrees' and offices' colony-wide hediffs: `Decrees.SyncHediffs`
  and `Offices.SyncHediffs` skip `DevelopmentalStage.Baby()`. Also answered (no change): a baby lying awake in a crib
  is the game's own rule (babies sleep only involuntarily, never while starving); hers was hungry and crying.
- 0.93.0, stage 2 release 2b.3, wards and hostages (docs/design/stage2-diplomacy.md, item 7, and item 2's hostage for a truce's
  price; the player's decisions 9 and 10 as recommended; built beside 2b.1, feasts and contests, by another agent, and
  merged with it at 0.92.0 (0d6cedc): both sides kept in `DiplomacyBook.cs` (`DiplomacyHome`'s feast and wards, the wait
  on either), `Diplomacy.cs` (the hour and day), `DiplomacyModel.cs`, `FactsLedger.cs`, the settings, the tests'
  registries, the save model, the DefValidator's placeholders, README, DESIGN.md and the Almanac page; 2b.2, marriage
  alliances, is built from 0.92.0 by another agent, so expect the same small merges there). DESIGN.md, "Diplomacy" → "Wards and
  hostages" has the whole of it. First, round a's three words-versus-map mismatches (the 0.90.0 entry below): a pact
  ended by the colony's letter is answered under its own tag (`DiplomacyLetters.QuoteTags` swaps `pact` for `pactend`,
  and the tone stays cool or curt), so a refused offer's `[pact,refusal]` lines never fit; the trap at an exchange is its
  own deed, `trapped` (TheirWrong, 12; words, two talk pieces in `Rimfolk_OutsiderTalks_Trap.xml`), so
  `Rimfolk_OT_Honour_BrokeWord` and its memory words ("refused the colony ... though they owed it") are said only of a
  debt refused; a truce agreed where no people at peace with both can vouch for their envoy (`Truces.ByWord`) is answered
  under `trucebyword` (lines in `Rimfolk_Lines_PactEnds.xml`; a refusal keeps the truce's own), and the keyed answer,
  their offer, the accepted message and the pending row have ByWord fellows that promise no envoy at the gate. No old
  line's tags changed: the new words are under their own tags in new files (textcheck against the branch's start is
  clean). Pure and tested: `Diplomacy/WardMath.cs` (`WardKind`, `WardStage`, `WardMood`, `WardResult`; their offer,
  asking, the ward's age and kin, `KnownFor`, the lesson's day and pace, `Mood`, `Fed`, `Year` and what it does, a death
  in the colony's care and the feud, the colony's youth by age and their people's skills, letters, the page, hostages'
  age and scores, `PactRoll`, `EndOf`, `WouldJoin`, the records' bounds `Fits`, the letters' codes, the facts' tags;
  `Tools/Tests/WardTests.cs`, 1,327 checks, the three fixes among them). Game side, all in `Diplomacy/`: `Wards.cs`
  (records, guardians and the skill known for, why not, their offer (`ChoiceLetter_WardOffer`), `HearAsk`, `Agree` with
  `MakeYouth`, the hour for every record, arriving with a kinsman (`LordJob_WardParty`) and joining at the gate, the
  lodger quest (`StartQuest`: `Rimfolk_LodgerQuest` with the game's `QuestPart_ExtraFaction`, home faction their people,
  added before `QuestManager.Add`, accepted from the start), the day's tally, the lesson (`Together.When`), the year's
  end with their kin (`JobDriver_WardToKin`) or alone (`JobDriver_WardLeave`, out at the edge as the colony's lodger),
  `Reckon`, `Died`/`Taken`/`Wronged`, picking a guardian, sending home early, `Grown` and `HeirFor` for the grown ward
  (a Regard tie of theirs toward the colony, origin `ward`, key `fostered`), `Clear`, `HomeLost`, `HereAs`),
  `WardsAbroad.cs` (the colony's youth or hostage away: the youths, why not, `HearSend`, `Agree`, getting ready with road
  food, `JobDriver_WardSetOut`, the caravan, at the town, letters by age or as a page, home with the skill, a friend and
  `taught`, `Recall`), `Hostages.cs` (who could go, the letter's options, given for a truce (`Truces.Pay`) or a defence
  pact (`DiplomacyLetters.PactAnswer`), `MakeHostage`, the term read from its tie, forfeit (`ChoiceLetter_HostageForfeit`:
  home anyway, arrest by a warden, ask to stay, or they slip away), returned, held (`KidnappedPawnsTracker.Kidnap`)),
  `WardLord.cs` (the party lord, the jobs, both letters, `Patch_WardKilled` on `Pawn.Kill` for a death at a colonist's
  hand, `WardDefOf`), `WardLetters.cs` (`LetterPurpose.Ward`: `ward:ask`, `ward:send`), `WardPanels.cs` (cards: the
  colonist's for a ward or hostage of theirs (lodgers are the colony's) and for the colony's own away, a guardian's, a
  grown ward's Person card, the people card's acts, a town's lines, the Ward and Hostage tie cards; Peoples' rows; Here
  and away; the inbox `ward` in Needs you and `ward-under-way`), `WardWords.cs`, `FactsWards.cs` (`tie:ward`,
  `tie:hostage`, `them:ward` with how they are, `them:hostage`, `them:fostered`; `{WARD}`, `{HOSTAGE}`, `{GUARDIAN}`),
  `WardDebug.cs` (dev actions: their offer, their ward arriving now, today's lesson, the year's end, a youth of ours to
  them, ours at their town, a letter from ours, ours home, a hostage for a live truce or pact, their side breaking the
  terms, the colony breaking its word, the term over). Shared, small: `WardStay` and `DiplomacyHome.wards` (four at most,
  `wardOfferAt`, `wardOfferPeople`) in `DiplomacyBook.cs`, `LetterPurpose.Ward` (the letter dialog 26 taller),
  `TrucePrice.TheirHostage`/`OurHostage` and their scores (`PactMath`), `PactWords.Price` naming the colony's hostage,
  `DiplomacyLetters` (owns Ward, the truce's and a defence pact's hostage asks, `QuoteTags`), `DiplomacyModel` (rows, acts,
  Here and away, `WhyHere`), `DiplomacyMath` (`dip:ward`, `dip:hostage`), `KeepFor.Ward`/`Hostage` (and `Lent` for the
  colony's ward or hostage away) with their daily checks, `Notables.Add` (`Wards.Grown`) and the heir's making
  (`Wards.HeirFor`), `FactsLedger` (one call), the setting `diplomacyWards` (section Diplomacy), the ward's rites in
  `Rimfolk_Ages.xml` now the ward's side only (a youth in their household, an apprenticeship under contract, a posting on
  agreed terms). Defs: `Defs/Misc/Rimfolk_Wards.xml` (the lodger quest, two letters, five jobs). Content: 29 talk pieces
  (`Rimfolk_OutsiderTalks_Wards.xml`: with their ward, by how they are, the time of day, a raid, a death, their ways, a
  child ward; others of theirs asking after them; the colony's youth and page with them; one fostered here once; their
  hostage and others asking after one, forfeit and held, returned), 29 letter lines and 20 bubbles
  (`Rimfolk_Lines_Wards.xml`: their answers to asking and offering by tone, the Empire's page; letters of any purpose
  while a ward or hostage is with either side; the lesson, the guardian met and left, their kin at the gate, the colony's
  hostage as the terms are sworn), keyed words with fellows (`Rimfolk_Wards.xml`, `Rimfolk_Wards_Variants.xml`, 37
  fellows), the Almanac's diplomacy page extended inside its own elements (the description's three new paragraphs, the
  truces' price and sworn by word, breaking them, the settings; no keyword changes, no new sections, so textcheck stays
  clean), README ("Wards and hostages"), DESIGN.md, the save model for `wards`. Tests changed with it: `DiplomacyTests`
  (the letters' purposes in the order built, Ward after Envoy), `RansomTests` (the lent check widened),
  `OutsiderTalkTests` (the tags and placeholders); the DefValidator knows `WARD`, `HOSTAGE`, `GUARDIAN`. The lodger
  assumption held, with two conditions found in the game's code: the extra-faction part must be in the quest before
  `QuestManager.Add` (its cached list of extra-faction parts is filled there and on load), and the quest must be under
  way (`QuestUtility.GetExtraFactionsFromQuestParts` reads only ongoing quests). Left out, and why: hostages for a trade
  pact (its letter's one ask is the cart) and in their own offers of truces and pacts; their hostage at a truce sworn by
  word (with nobody to vouch for them on the road the price lapses, and the letter says so); the colony's youth's
  objection before the council and a hostage's (the letter is the colony's decision; the hostage says a line as the terms
  are sworn); a forfeit hostage's own ransom offer at once (once arrested they are their people's prisoner, and the
  ransom offers of 2a.3 come by their own rules); a ward who becomes a notable other than as their town's heir (the heads
  are the game's leaders); homesickness as a thought of its own (it is their own mood, read as a word: no new mood). To
  look at in game: their offer and the colony's ask; the ward and their kinsman walking to the gate, the ward joining
  (on the colonist bar, in the Quests tab as a lodger, home faction their people), the guardian's lesson an hour by day
  and the ward walking over to find them, a child with Biotech at the game's lessons, the card's lines; the year's end
  (`Wards: their ward's year over now`): two kin at the gate, the ward walking out to them and leaving with them, the
  letter; the ward downed in a raid and dying under a roof and in the open; the colony's youth setting out, at the town,
  their letters, home with the skill and a friend; the Empire's page; a truce with a hostage either way (theirs walking in
  some hours after the oath as guests of a people at peace with both, ours setting out), the term's end, their side
  breaking it (the letter's three answers, the warden's arrest, a hostage who would stay), the colony breaking it (ours
  held, then the game's ransom demand or their messenger's); a defence pact with a hostage either way; the letter dialog
  at 768p with the new purpose.
- 5 November 2026, a play-test point after 0.90.0: the new interface (0.82.0 to 0.88.1) and stage 2 round a with its
  review and text pass (0.81.0, 0.88.0 to 0.90.0) are in, none of it yet seen in the game. Round b (2b.1 feasts and
  contests, 2b.2 marriage alliances, 2b.3 wards and hostages) waits for the player's word after playing. Stage 3's
  17 decisions and the harm review's picks are still with the player.
- 0.92.0, stage 2 release 2b.1, feasts and contests (docs/design/stage2-diplomacy.md, item 5; the player's decisions 7 and 9 as
  recommended; built beside 2b.3, wards and hostages, which another agent builds at the same time: expect small merges
  where both add to `DiplomacyHome`, `Diplomacy.Hourly`, `DiplomacyModel` (rows, acts, `WhyHere`), the settings'
  Diplomacy section and the Almanac's diplomacy page). DESIGN.md, "Diplomacy" → "Feasts with guests" has the whole of
  it. Pure and tested (`Tools/Tests/FeastTests.cs`, 288 checks): `Diplomacy/FeastMath.cs` (`ContestKind`,
  `FeastPurpose`, `FeastStage`, `GuestState`, `FeastHope`; one a season (`MayPlan`), who may be invited (`MayInvite`:
  at peace, regard 0, three days' travel), `AcceptChance`/`Accepts`, `ComeDays`, `FeastDay`, `Fine`, `ArriveNow`,
  `GiveUp`, `Leave`, hopes by age (`Hope`, `Answers`, `Offered`), the contests' scoring (`BoutChampion`, `RaceWinner`,
  `HuntWinner`, `TableChance`, `ShootingWinner`, `Champion`, `HuntGame`, `IsTarget` by name), the bad sides
  (`HardBlow` 1/25 under `MayBeHurt`: never a child, with child, or hurt in three days; `SourNight` 1/15 by temper,
  never a child), `Welcome`, `DeedWeight`, `HeadShare`, `Suggests`, `MealsWanted`, `AwayWin`, the topic `feast:HOME`, the
  facts' tags). Game side, all in `Diplomacy/`: `FeastBook.cs` (`GuestFeast`, `FeastGuest`, `FeastResult`,
  `FeastTale`; the play-outs already own the name `FeastPlan`), `Feasts.cs` (the hub: planning and why not, the
  invitations through `NeighbourLetters.Send`, their answer, the day, the arrival with a retinue to the gate, the day's
  look from the guests' group, the hosting rules, the contests in turn, the feast as a play-out of the kind Feast, the
  champion and the prize, the sour night, first light and the welcome, lost, hurt, insulted, called off, the steward's
  suggestion `ChoiceLetter_FeastSuggest`, `Host` and `StageChanged` for 2b.2, `FeastDefOf`), `FeastLord.cs`
  (`LordJob_FeastGuests`, `LordToil_FeastGuests`, `JobDriver_FeastRun`, `JobDriver_FeastShoot`, `JobDriver_FeastPrize`),
  `FeastContests.cs` (the five contests, who takes part, where, their steps and results, the watchers; not saved),
  `FeastBouts.cs` (`Outsiders.FeastBout` and `FeastBrawl` in the outsiders' own partial class), `FeastLetters.cs`
  (`FeastLetters.Read`; `FeastAway`, the colony's own at a neighbour's feast), `FeastDialog.cs` (`Dialog_Feast`),
  `FeastPanels.cs` (the Event card, a notable's card, the people card, the calendar's row, the Peoples view's rows and
  act, the inbox's `feast-coming` and `feast-under-way`), `FeastWords.cs`, `FactsFeast.cs`, `FeastDebug.cs` (dev
  actions: a feast set for today with a people's notables, the guests now, the contests now, a contest ended, the feast
  now, first light, the steward's suggestion, a tale from away). Shared, small: `DiplomacyHome` (`feast`, `lastFeast`,
  `feastHeld`, `feastSuggested`, `tales`; `AnyWaiting`; homes lost), `Diplomacy.Hourly` (the hour and the day),
  `NeighbourLetters.Read` (three lines: the feast's answer sets the tone and the visit line), `PlayGuests` (`Feast`),
  `DiplomacyModel` (rows, the act, `WhyHere`), `CalendarPanel` (the row), `FactsLedger` (one call), `FactionMemory`
  (the deed `feasted`, Favor 8), `ColonyTraitMath.Hosting` (`feasted`), `Workers_MissionRemembered` (one call:
  `FeastAway.Attended`), setting `diplomacyFeasts` (section Diplomacy). Defs: `Defs/Misc/Rimfolk_Feasts.xml` (the
  steward's letter, three jobs, two duties). Content: 27 talk pieces (`Rimfolk_OutsiderTalks_Feasts.xml`), 24 letter
  lines and 47 spoken lines (`Rimfolk_Lines_Feasts.xml`), keyed words with 17 variants (`Rimfolk_Feasts.xml`), the
  Almanac's diplomacy page (two paragraphs in its description and a line in Settings: new sections there trip textcheck;
  no keyword changes), README ("Feasts with guests"), DESIGN.md, the save models (`guests`, `retinue`, `contests`,
  `results`, `tales`). Tests changed with it: `OutsiderTalkTests` (the tags and placeholders); the DefValidator knows
  `FEAST`, `CHAMPION` and the new dynamic keys.
  The entry point for 2b.2's weddings: `Feasts.Host(home, people, notables, FeastPurpose.Wedding, whenTick, tag)`
  brings guests who already agreed on the first fine day from that tick (no letters, no contests unless given), to wait
  at the gate, sit at the evening's feast with the colony (`PlayGuests.Feast`), stay the night and go home at first
  light, their welcome counted as a feast's; `Feasts.StageChanged` (an event: plan, map, stage) says when they are at the
  gate (`FeastStage.Here`: hold the ceremony then) and gone (`FeastStage.Done`); `GuestFeast.tag` carries the wedding's
  own key; a second `Host` takes pawns who aren't notables (a bride's kin made as people of the world by the caller), and
  This week, the calendar and Here and away already speak of wedding guests (`CC_Feast_Week_Wedding`,
  `CC_Feast_Arrive_Wedding`, `CC_Feast_HereAs_Wedding`).
  Left out, and why: a sore loser's challenge on a later visit (the harm review's 2.8.1, not picked); renown for the
  champion's house (round d); Empire honor for a feast (decision 9 names only two places); the race's marker as a thing
  on the map (runners go to a cell; a flag would need a building of Rimfolk's own); the Empire's concert without
  Royalty (the feast's notes over heads stand in); the "Hurts from everyday life" scale (`Feasts.HarmScale` is 1 until
  the harm review's setting is built). The feast's contests run in the afternoon and the feast in the evening (the
  design's order reads the other way; the light and the hunt want the day). To look at in game: the plan from a people's
  card, the calendar row and the steward's letter (guests greyed with why not, contests with their risks, the prize, the
  meals line); the invitations as letters and calls and their answers; the day set on the calendar and in This week;
  the guests at the gate and the greeting; each contest (a bout in the ring with watchers, the runners at the line, the
  sprint and a stumble, the hunt and its kills, two games at the tables with a guest who has no recreation need, a
  shooting match with Misc. Training's target), the feast with the guests served, the Empire's guests and a concert,
  the prize carried to a guest and to a colonist, a night in Hospitality's guest beds and on the floor, a sour night with
  the deeper hooks, first light and the letter; a raid during the day, rain at noon, a guest dying, "Call it off"; a
  neighbour's invitation attended and the tale told at home; 2b.2 calling `Feasts.Host`.
- 0.91.0, journey planner: who to bring and what to pack (the player's request, with a screenshot of "Plan a journey": a better
  overview of the colonists to judge who to bring, their state, what to bring, the recommended picked for them; and,
  the same release, RimMarket's stall goods listed and taken out of the stock for the road). DESIGN.md, "Rimfolk's
  windows" → "The journey planner: who to bring and what to pack" has the whole of it. Pure and tested:
  `Journeys/JourneyAdviceMath.cs` (`JourneyAim`, `JourneyDanger`, `AdviceGroup`, `AdviceWhy`, `HomeWarning`,
  `JourneyWants`; `DangerOf`, `KeepHome`, `MostToSend`, `PartySize`, `WantsDoctor`, `FighterScore`, `BetterNot`,
  `HomeCost`, `Score`, `HomeLeft`, `Pick`, `Best`/`IsBest`, `PickBeasts`, `MedicineFor`, `SilverFor`, `PickGoods`,
  `TakeFrom`, `StillToAdd`; `Tools/Tests/JourneyAdviceTests.cs`, 1,700 checks). Game side: `Journeys/JourneyAdvice.cs`
  (`Purpose` from the destination: an active mission at the town, a call to arms there, settlers waiting for the
  hamlet's place, then the site (a raiders' camp's points, else the site's against the home's; distress signals and
  hidden parts unknown), peace talks, the town (hostile, trading, a trading post, a trade request, a visit); each
  colonist read and placed with chips; animals; `Items` for "Recommended for this journey"; `Apply`), the dialog
  (`UI/JourneyDialog.cs`: the purpose strip under the title with "Pick the recommended", Who goes as rows grouped
  Recommended, Can go, Better not with folds and counts, the animals the same, the recommended things above the list
  with Add and Add the recommended, three meters at the foot; the window 1120 by 820 at most, the overview 45% of it),
  the picker (`UI/JourneyPick.cs`: stall goods beside the stores, "(N on a stall)"). Weapons for the unarmed are a new
  line kind `arm` (group "arms", after drugs): the unarmed take one up at the end of their Dress step
  (`JourneyRun.ArmJob`, marked in `plan.done` as `ThingID:Arm:defName`, counted as packed by `JourneyRun.Armed` in
  `JourneyPack.Missing`). RimMarket (`Integrations/RimMarketJourney.cs`, found by name, `JourneyReady`): `JourneyStock`,
  `OnStall`, `TakeForJourney` (out of `StockOwner` into the traveller's pack, the line lowered through RimMarket's own
  `SetOrder` as `Notify_Sold` would, a refill line kept, no takings, demand or sale); `JourneyRun.StallJob` at the end
  of the Pack step (what the stores and packs don't cover, less what others fetch now, `plan.stallTaking`, not saved;
  as much as they have room for, all that's left when nobody has room), `JobDriver_JourneyStall` and
  `Rimfolk_JourneyStall` in `Defs/Misc/Rimfolk_JourneyStall.xml`. The advice model is built when the planner opens and at
  most once a second while open (the press builds it at once); nothing runs while it is shut; nothing is saved but the
  `arm` lines and their marks. Keys in `Languages/English/Keyed/Rimfolk_JourneyAdvice.xml` (and the arms group in
  `Rimfolk_Journeys.xml`). Docs: the Almanac's journeys page (the description, the last hours' closing text, "What they
  take" and "How long it lasts") and RimMarket's (no keyword changes), README ("Journeys planned ahead"), DESIGN.md.
  Left out, and why: clothes to pack for the weather (the planner already changes travellers into the stores' clothes
  before they go; what the stores can't cover is a note, and a caravan doesn't change clothes on its own); trade
  goods from the stores besides art (what the colony needs isn't known; anything else is one click in "Add from the
  stores"); a speed penalty for slow animals (a caravan's speed on the world map doesn't depend on its slowest
  animal); stall goods put back on the stall when a journey is called off (they go to the stores like any pack, the
  line stays lowered); mods' mounts beyond the game's own `CaravanRidingSpeedFactor`. To look at in game: the planner
  at 1080p and 768p (the overview's rows, two lines of pills, the folds; the purpose strip's sentence wrapping beside
  the button); a distress signal, a raiders' camp, a hostile town, a trading town with a trade request, peace talks, a
  call to arms, settlers waiting, a pilgrim's escort, a harvest, an apprentice, nowhere in particular; "Pick the
  recommended" from nothing picked and from the player's own picks (who goes replaced, items added, set numbers kept);
  the home's only doctor or cook passed over and the line under Who goes for one's own picks; a mount and pack animals
  by the load; the arms line taken up in the last hours and not loaded again at the gate; with RimMarket, a line taken
  partly off a stall (the traveller walking to it, the counter and its line lowered, a refill line kept), the picker's
  "(N on a stall)".
- 0.90.0, text pass: diplomacy round a (the content pass beside 0.81.0, 0.88.0 and 0.89.0, as docs/design/stage2-diplomacy.md, "The
  order to build it in", asks; no version of its own: its points go with the next round). Words only, in new files beside
  the round's own (the round's tests cap its own files' counts). Talk pieces, 156, each said only on facts the providers
  set (FactsLedger, FactsPacts, FactsRansom, the deeds in FactsColony): debts of honor 37
  (`Rimfolk_OutsiderTalks_Debts.xml`: owed each way by their ways, in person, called in either way, kept, refused, gone
  back on, their fighters come and fallen), truces 22 (`Rimfolk_OutsiderTalks_Truces.xml`: a guest of theirs while one
  holds, by their ways, its end near, sworn and kept, broken by them or by the colony, the colony's word broken as a third
  people heard it), pacts 31 (`Rimfolk_OutsiderTalks_PactsMore.xml`: what a pact means by their ways, the defense pact and
  its call by fire or console, their band and their dead, the trade pact's cart and caravan, who offered it, one broken),
  captives 22 (`Rimfolk_OutsiderTalks_Captives.xml`: one of theirs held, one of the colony's held, an exchange to be made,
  a former captive back as a guest, a bargain settled, one let go for nothing), envoys 44
  (`Rimfolk_OutsiderTalks_EnvoysMore.xml`: theirs at the table and through the weather, a raid, a death, a birth, a
  decree, an election, hunger, plenty, the cells; by their ways and the Empire's room and fine meals; others of theirs
  asking after them; the colony's envoy at their town; how a stay went), warm, cool and sour among them. Letters and
  bubbles (`Defs/FlavorPoolDefs/Rimfolk_Lines_DiplomacyMore.xml`): 101 of their answers (favour 16, pact 15, truce 9,
  ransom 10, envoy 9, each by the tones its answer can really take, which the file's header lists; letters of any
  purpose while a debt 14, a pact 10, a truce 6, a captive 6 or an envoy 6 stands) and 68 bubbles (each ask face to
  face, the parley, the ransom at the gate, the exchange's handover said to the colony's own, the envoy's runner, the
  signal fire). Keyed variants, 119 (`Rimfolk_Diplomacy_Variants.xml` 72, `Rimfolk_Ransom_Variants.xml` 47): the good
  turn's debt line, calling in and their answers, their calls and needs, truce offers, answers, the envoy at the gate,
  the oath, its ends and breaches, pact offers, answers and renewals, the call and the band, a trade season, calls to
  arms, ransom offers, the grudge, the trap, the game's demand, exchanges, the envoy's offer, answers, arrival, letters
  home, end and death, the colony's envoy at the town, its letters and the road home. Code, two lines: the one who lights
  a signal fire says a line as it catches (`JobDriver_SignalFire`: `Outsiders.Say(pawn, "signalfire", null)`), and the
  envoy's letter-home news is picked with `RansomWords.T` so its variants show (`Envoys.cs`). Fixed in passing: four
  keyed lines opened a sentence on a lowercase value ("a ceasefire with...": `CC_Truce_End_Peace_2`,
  `CC_Truce_End_RanOut_2`, `CC_Pact_Renewed`, `CC_Pact_BrokenByUs`); 17 older lines of the round tagged theirLowtech
  spoke as a tribe ("the elders", "green branches", "by the fire"), though that tag is also every people of the middle
  ages and the Empire, in letters and bubbles alike: they now speak of their leaders and a sign of parley
  (`Rimfolk_Lines_Pacts.xml` 7, `Rimfolk_Lines_Ransom.xml` 9, `Rimfolk_Lines_Diplomacy.xml` 1, tags kept). README's
  three counts. Still thin: nobody at war with
  the colony talks with it, so tie:truce is said only by one of theirs who visits once their goodwill has come round, and
  truce talk leans on what is remembered after; a letter's facts ask no ages (`FactsMath.Letter`: ties and history), so
  their ways in a letter (and a bubble's) are only theirLowtech and theirHightech, and a theirLowtech line must suit a
  tribe, a town of the middle ages and the Empire alike; nothing is said when the warden walks a captive out (the game's own
  release job) or when a band walks in under a pact, and the envoy's table talk is the talk pieces with them:envoy; texts
  told often with one wording because the code reads them with `P`: the ransom messenger's messages (`CC_Ransom_Msg_*`),
  the call to arms' outcomes (`CC_Arms_Late`, `CC_Arms_NotOurs`, `CC_Arms_Fought`), a pact made (`CC_Pact_Made`), the
  colony's envoy home (`CC_Envoy_Msg_Back`); `Rimfolk_OT_Honour_BrokeWord` says their people refused the colony's call,
  but deed:brokeWord is also the trap at an exchange. Facts that would help: the envoy's stay as a tag (its mood, a night
  on the floor), a pact's call answered or put off, a truce ended in peace apart from other kept words, the trap apart
  from a refused debt, their ages in a letter. To look at in game: the signal fire's bubble; the envoy's letters home
  over a season.
  Words that disagree with the code, found by the pass, for the next review: a pact ended by the colony's own letter
  can be answered with `[pact,refusal]` lines meant for a refused offer (`PactAnswer` only raises the tone to Cool);
  `Rimfolk_OT_Honour_BrokeWord` says their people refused the colony's call, but the trap at an exchange leaves the
  same memory; older truce lines promise an envoy at the gate where a truce is sworn by word (no people to vouch).
- 0.89.1, stage 2 round a review: fixes (the three releases 0.81.0, 0.88.0 and 0.89.0, diplomacy's base and favours, pacts and
  truces, ransom and the envoy, read against the 1.6 game and Faction Territories' decompiled source; no version of its
  own: its points go with the next round). Fixed, each checked in the code and the game's source: their envoy downed in
  a fight leaves the stay's group by the game's own rule, so a death or an arrest after that never reached
  `LordJob_EnvoyStay`: the stay ended without a letter, regard or deed, or went on with the envoy in a cell; the hour
  now sees a dead envoy as a death at the colony (`Envoys.Died`, shared with `Lost`), a prisoner as an arrest, and an
  envoy being carried to a bed as still here (`MapHeld`), where before an hour falling during the carry ended the stay
  with a "leaves" letter (a prisoner keeps the bed the wardens gave them). The colony's envoy ambushed on the road was
  put on the fight's map and the plan called off without a word; it now waits and sends the caravan on to the town once
  it sets off again (called off back at a home or once carried off). Needs you counted every truce offer, pact offer and
  call to arms on a pact left in the letter stack, moot or kept ones too: each letter says whether it still waits
  (`ChoiceLetter_TruceOffer.Waiting`, `ChoiceLetter_PactOffer.Waiting`, `ChoiceLetter_CallToArms.WaitingOnPact`). With
  Faction Territories, a caravan that took the mod's own "Join fight" to a partner's town entered the town's map at once,
  no caravan was left to be seen arriving, and the call was taken as unanswered and the pact broken (a debt refused)
  while its people fought there: two grown colonists on the town's map answer it now (`CallsToArms.OnTownMap`). With the
  deeper game hooks off, the firebrands' raid came and the truce stayed sworn although their letter said it was broken:
  Rimfolk breaks it as it sends the raid when the hook didn't. A call to arms came once per attack only by a session's
  set, so a reload during the same war brought the letter again, and leaving it broke the pact: a call kept there against
  the same enemy within the season (the history's `dip:arms`) counts as asked (`KeptLately`). The avenger's objection was
  marked heard only for the session, so a reload before the oath asked the council again: kept on `LordJob_Parley.heard`.
  One of a partner's band killed by the colony (a turret's or a mortar's stray shot in the fight the pact brought them
  to) was the memory's "killed" and broke every pact with them: `PactMath.BreaksPact` leaves out a killing while their
  war band is on a home, as the breaches already leave out a blow struck. Tests: `PactTests` (BreaksPact, the review's
  fixes in the code), `RansomTests` (the envoy's hour, the ambushed envoy). Looked at and found sound: every Harmony target
  and argument name (`FactionCanBeGroupSource`, `PostProcessSpawnedPawns`, `CreateLordJob`, the ransom demand's
  `CanFireNowSub` and `TryExecuteWorker`; names in the 1.7 reference too), each patch with its setting off, every
  postfix inside a try; the parley party as guests of a people at peace with both (out of the turrets' targets through
  `AttackTargetsCache`, colonists by `GenHostility`); the messengers, runners and go-betweens of peoples at peace; the
  saved records on an older save (the book is made empty, lists made on load), refs to pawns and peoples gone, the keep
  reasons `envoy` and `lent` in the ledger's daily check; the colony's envoy at the town a world pawn (caravan pawns are,
  needs frozen there); the lords' memos never sent from their own transitions where it mattered; the trap's chance the one
  named; the firebrands, trap and band at the design's rates; the Empire's no to pacts; the settings switching each part
  off; the inbox counts against their rows; the alert and the cards' costs. Not confirmed, left as they are: a captive
  still downed ten hours after an accepted ransom or an exchange is let go where they lie (the game's warden won't carry
  a downed prisoner out; in 1.6 they may crawl), and the offer is dropped a day later, so their healthy exit afterwards
  can make a debt of honour; captives returned as a truce's price and leaving healthy make one too; a favour's fighters
  asked by letter mostly come after the fight (the ask is checked when written, not when read); with the deeper hooks
  off a truce people's raid that comes anyway doesn't break the truce (only the firebrands' now does); their envoy
  notable spawned elsewhere when due (Arrive doesn't look again); a call in progress cleared by a reload (`Pacts.callers`
  is a session's) and a renewal letter dismissed offered again after one; a call to arms' "too late" message when the
  fight beside the town couldn't be set up for another reason; Factional War's battle bringing a truce partner may break
  the truce by `Patch_TruceRaided` (not checked against its source); no Empire honour for a favour called in (decision
  9's honour part isn't built). To look at in game: an envoy downed in a raid, carried to bed, then dying or recovering;
  the colony's envoy ambushed on the road; a pact's call to arms with Faction Territories answered by "Join fight"; a
  firebrands' raid with the deeper hooks off; a reload during a long war at a partner's town; a mortar's stray shell on a
  called band.
- 0.89.0, stage 2 release 2a.3, ransom and the envoy (docs/design/stage2-diplomacy.md, items 3 and 4; the player's decisions 4, 5,
  6 and 9 as recommended; built beside the interface review, merged in at 1e2d428: this release adds through the
  registration APIs only). DESIGN.md, "Diplomacy" → "Ransom and the envoy" has the whole of it. Pure and tested
  (`Tools/Tests/RansomTests.cs`, 757 checks): `Diplomacy/RansomMath.cs` (`RansomKind`, `RansomStage`, `TrapOdds`; two to
  six days, who is wanted back by a steady roll (`SteadyRoll`, so nothing is saved for those not wanted), `Price`, goods
  for a tribe, `MoreChance`/`Raised`, three refusals a grudge, `AgainDays`, the game's `Fee` and day, `ReturnHours`,
  `ExchangeChance`, `NegotiationFactor` (the peace talks' curve), `TrapChance` 5% times it, `TrapPoints` 0.35, the topics
  `ransom:ID` and `exchange:OURS:THEIRS`, the tags), `Diplomacy/EnvoyMath.cs` (`EnvoyNight`, `EnvoyMood`, `EnvoyStage`;
  `MayOffer` (goodwill 30, a year per people, a season per home, one stay per home), `SendChance`, the day and night
  scores, `Talks`, `Hurt`, `Mood` by the score a day, the letter's and the end's regard, `LeavesFavour`/`DeedWeight`,
  `DiedRegard`, `LetterDue` every five days; the colony's envoy: `CanGo` (Social 6), `AbroadRegard`, `SocialXp`,
  `PartingGift`, `RoadDays`). Game side, all in `Diplomacy/`: `DiplomacyBook.cs` (the first records that aren't ties:
  `DiplomacyHome` per colony by `ColonyBook`, `RansomOffer` six a home, `EnvoyStay`, `EnvoyAbroad`; homes lost through
  `ColonyStateListener`, the day's upkeep in diplomacy's day), `Ransoms.cs` (offers noticed hourly, their word by a call,
  their messenger or a go-between of a people at peace with both (`Bearer`, `TruceParley.Escort`); `ChoiceLetter_RansomOffer`:
  accept (the game's release, the price set down or by pod, `ransomed`, `dip:ransomed`), ask for more, free (`forgave`),
  keep (a Grudge tie of origin `ransom`, key `ransomRefused`, at the third); `PaidFor` keeps a paid captive's
  `released` from making a debt of honour (`FavoursOwed.Noted`); the game's demand by messenger (`Demand`,
  `ChoiceLetter_RansomByHand`, the speaker's `JobDriver_RansomPay`, the colonist home from the edge or by pod; the silver
  from the stores when it never reached them); exchanges (`HearExchange`, their party with the colony's own walking
  beside them, `dip:exchanged`); the trap (`Raids.Send` from the edge nearest the gate, `brokeWord`; `forceTrap` for the
  dev action); `RansomDefOf`), `RansomParty.cs` (`LordJob_Messenger`: to the gate, wait, home, run if hurt; its errand
  done by a flag set at its arrival (`done`, `Trigger_Custom`), never a memo from inside its own transition;
  `JobDriver_RansomPay`), `RansomHooks.cs` (`Patch_RansomByHand_CanFire`, `Patch_RansomByHand_Execute` on the game's
  `IncidentWorker_RansomDemand`, only without a powered console), `RansomLetters.cs` (`LetterPurpose.Ransom` and
  `Envoy` for `DiplomacyLetters`; `ExchangeActs` for "Offer an exchange..."), `Envoys.cs` (their offer
  (`ChoiceLetter_EnvoyOffer`), asking by letter, `Agree`, the arrival with an attendant and food for the season, the
  hour's tally, the missions' hosting rules for meals and beds, `Regroup` after being downed, a seat at a council meeting
  in session, the runner every five days, `End` (regard, `envoyHonored` or `slighted`, `dip:envoy`), `Lost` (dead:
  sheltered or not), `Harmed` (they flee), `ChooseBed`, `SeeOff`, `HereAs`), `EnvoyLord.cs` (`LordJob_EnvoyStay`,
  `LordToil_EnvoyStay` with `Rimfolk_EnvoyStay` and `Rimfolk_EnvoyIndoors`, `JobDriver_EnvoyLetter`, `JobDriver_EnvoySitIn`),
  `EnvoysAbroad.cs` (who can go, the council's question, food from the stores (`TakeInventory`), `JobDriver_EnvoySetOut`
  to the edge and a caravan of their own, kept at the town with `KeepFor.Lent`, letters with the town's real news
  (`Facts.For(FactScene.Letter(...)).news`), the notables' regard, provisions and a parting gift for the road home,
  `Recall`, `HomeLost`), `RansomPanels.cs` (cards: Colonist (the captive, the held colonist, the envoy away), Person
  (their envoy), People, Place (a town), Tie (the grudge); acts with why not; the inbox `ransom` and `envoy` in Needs you
  with their counts (through `Inbox.Count`), `ransom-under-way` and `envoy-under-way` in steps; `Known` hides an offer the
  colony hasn't heard of yet; rows for `DiplomacyModel`), `RansomWords.cs` (forms by age, the trap's odds, an exchange
  labelled with its odds where it can bring one), `FactsRansom.cs` (`ransom:theirs`, `ransom:held`, `them:wascaptive`
  `{CAPTOR}`, `envoy:here` `{ENVOY}`, `them:envoy`, `envoy:ours` `{OURENVOY}`), `RansomDebug.cs` (dev actions: their offer
  for a prisoner now, the game's demand now, an exchange due now, the next exchange a trap, their envoy's offer, their
  envoy arriving now, a letter home due, the stay's end, ours at their town now, a letter from ours, ours home now).
  Shared, small: `LetterPurpose.Ransom`/`Envoy` and `OutsiderAct.OfferExchange` at the ends of their enums (the menu
  after SpeakPact, an ask in `OutsiderTalkMath.Asks`), `DiplomacyLetters` and `DiplomacyActs` delegating to the above,
  `DiplomacyModel` (Peoples' rows and acts, Here and away: `WhyHere`, `AwayRows`), `FavoursOwed.Noted` (`PaidFor`),
  `KeepFor.Envoy`/`Lent` with their daily checks, `ColonyState.cs` (homes lost), `FactsLedger` (one call), settings
  `diplomacyRansom`, `diplomacyEnvoys` (section Diplomacy). Defs: `Defs/Misc/Rimfolk_Ransom.xml` (three letters, four
  jobs, two duties). Content: 16 talk pieces (`Rimfolk_OutsiderTalks_Ransom.xml`), 24 letter lines and 23 spoken lines
  (`Rimfolk_Lines_Ransom.xml`), keyed words with variants (`Rimfolk_Ransom.xml`), the Almanac's diplomacy page extended
  (five sections after "Breaking them", no keyword changes), README, DESIGN.md, the save models for `homes` and
  `offers`. Tests changed with it: `DiplomacyTests` (the built purposes and acts at the ends of their enums,
  `DiplomacyBook.cs` the one Diplomacy file that saves, the menu's order), `OutsiderTalkTests` (the ask, the tags, the
  placeholders); the DefValidator knows `CAPTOR`, `ENVOY`, `OURENVOY` and the new dynamic keys. textcheck against the
  branch's base reports `Almanac_WhatsNew.xml`'s index shifts from the merged 0.88.1 round and the diplomacy page's five
  new sections as "added outside an existing list" (the page was extended on purpose). Left out: the Empire's
  envoy as one of its titled kinds (their envoy is the notable the colony knows; the Empire's expectations of a room and
  fine meals are scored and said, not their letters home), "Offer for ransom" as a gizmo on the prisoner (it is the
  captive card's act and a letter, as the interface design places it), the envoy spying (round c), a forfeit hostage's
  ransom (2b.3). To look at in game: a prisoner's people's messenger walking to the gate (and a go-between of a third
  people for a people at war), their letter, accept with the warden walking the captive out and the price set down,
  ask for more, let go, keep (again within a season, the grudge at the third); a call by console and their pod; the
  game's demand without a console by messenger, the speaker carrying the silver out, the colonist walking in hours later
  (a pod from a people of the machine ages); an exchange by letter and face to face, their party with the colony's own,
  the release at the gate; the trap from the gate's side (the dev action); their envoy's offer and arrival, the bed set
  aside from their card, meals at the table, a night in a spare bed and on the floor, indoors in a raid, a council
  meeting sat in, the runner at the gate every five days, the end's letter by mood, seeing them off, an envoy downed and
  getting up again; the colony's envoy picked, taking pemmican, walking to the edge and setting out, the caravan at the
  town, the letters with the town's news, the road home and the parting gift; each on the cards, Here and away, the
  inbox and the Peoples view.

- 0.88.1, interface review: fixes (the six interface releases 0.82.0 to 0.87.0, and 2a.2's inbox rows, card rows and acts, read
  against the 1.6 game; no version of its own: its points go with the next round). Fixed, each checked in the code and the
  game's source: Enter in the search (`Window_Search`) closed the window whose corner opened it and took nothing (the game
  hands its accept key to the topmost window that takes it before any window draws, and the search had
  `closeOnAccept = false`, so the council or region window under it closed): it now catches the key
  (`forceCatchAcceptAndCancelEventEvenIfUnfocused`, `OnAcceptKeyPressed` takes the result lit). Enter while typing in
  the Almanac's search closed the Almanac's window (`Window_Almanac.OnAcceptKeyPressed` keeps it while a text field has
  the keyboard; Enter elsewhere still closes it, as the game's windows do). The search's colonists and persons looked
  for their pawn among every pawn of the world on every frame (`Refs.Pawn` falls back to
  `PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead` for anyone the ledger doesn't keep, colonists mostly): found once
  per index now (`Search.portraits`). This week's Needs you heading counted its rows, the news letters' row among them,
  so it said one more than the stat above it, the tab, the corner and the council button: it says the one count
  (`Inbox.Count`) now. A colonist's card in a window's drawer and another in the floating card shared one story model
  (`StoryParts.Card`), so each draw of either built it again: two card models now (`StoryView.cardCaches`). Tests:
  `InboxTests` (the heading's count, the search's Enter, the portraits once per index). Looked at and found sound: the
  drawer (Escape first, the trail, gone things, the mouse hidden from the view under it), previews (ids, levels, the
  mouse inside one, clicks absorbed, hidden on close and load), the way back (dead steps dropped, settling after a step,
  filters, the side buttons once a frame), cards (models once a second, gone things, acts greyed), link markers (only
  LinkText reads them; letters, messages, bubbles, tooltips, float menus and the corner's list get plain words), every
  window's Begin/End pairs (no early exit leaves a scroll view or group open), the corner, the council button's count,
  following (the saved list cleaned, old pins taken over), the page addresses and the settings' old page names, the
  on-screen panels' name links (left click the card, right click the menu, the row's own click after), the region views,
  2a.2's rows, acts and alert. Not confirmed, left as they are: whether keyboard focus stays on the search's field after
  it closes (Rimfolk's own keys wait for `GUIUtility.keyboardControl == 0`); the mouse wheel over a preview scrolling the
  view under it (Unity's scroll view tests the rect, not the window); a card's why-not line naming an act that sits under
  "More"; the pact alert's word staying up to 250 ticks after a call made from the card; small per-frame allocations
  (`PagesMath.TryAddress` from `ArticleOf`/`SectionOf`, the planning window's label keys, `RegionView.Within`). To look
  at in game: Enter in the search from a window's corner and from its key; Enter in the Almanac's search; This week's
  Needs you with news letters waiting; a colonist's card in the floating card while another shows in the council
  window's drawer.

  At the merge: the search window gives the keyboard back when it closes (`UI.UnfocusCurrentControl` in `PostClose`), one of
  the review's unconfirmed doubts (Rimfolk's keys waiting on a field that kept focus).
- 0.88.0, stage 2 release 2a.2, pacts and truces (docs/design/stage2-diplomacy.md, item 2, and item 1's fighters through a favour
  both ways, left for this release; the player's decisions 1, 2, 3, 9 and 18 as recommended; built beside UI 5b, which
  restyles the pages and was merged in at 0445b20: this release adds rows, sections, acts and inbox rows through the
  registration APIs, its rows drawn with the kit's `RowAct` and `WhyNotLine`, its colours the design's tokens).
  DESIGN.md, "Diplomacy" → "Pacts and truces" has the whole of it. Pure and tested (`Tools/Tests/PactTests.cs`, 597
  checks): `Diplomacy/PactMath.cs` (`PactKind`, `TruceTerm`, `TrucePrice`, `TruceWhy`, `PactAnswer`; who may offer a truce,
  `TruceScore` with the 2a.1 `peace` promise read as 20, `TruceChance`, `PriceSilver`, `SwornGoodwill`, their offer
  (`TheyOffer`, a quarter of beaten raids, a season apart), the firebrands (10%, 15% below natural goodwill -50, once per
  truce, `FirebrandDay`), `Peace`/`FromPeace`/`Due`; `MayPropose` (goodwill 20, never the Empire), `PactScore`/`PactChance`,
  `MayOfferPact` (regard 50, an enemy in common, a year apart), `Renews`; `MayCall` (a season), `Answer` (a friend of the
  attacker declines, nine in ten come), `BandHours`; `CartCount`/`CartPay`; `Answers` (two grown colonists, five days);
  `OurBreaches`, `PactBreaches`, `TheirBreaches`; the terms' codes; the facts' `Tags`). `FavourMath`: `FavourAsk.Fighters`,
  `FavourNeed.Fighters`. Game side, all in `Diplomacy/`: `Truces.cs` (Truce ties of origin `truce`, their ends in their
  terms and reached in diplomacy's hour, the ledger's until unset; `KeepsAway`, a set built once a 250-tick stretch;
  `Bars` for `Raids.CanRaid(f, home)`; why it can be offered, `Hear`, `MakePending`, their offer after a raid beaten
  (`WatchRaids`, `ChoiceLetter_TruceOffer`), `Swear` (term, price paid, firebrands rolled, `RaidersCamps.TruceSworn`,
  `dip:parley`), the firebrands' raid past it (`letting`), its end (a peace, kept, or run out), broken by them (a raid, an
  ambush, a colonist killed: `truceBroken`) or by the colony (`brokeTruce`, `wordBroken` in reach), who broke it kept in its
  terms for the inbox), `TruceParley.cs` (their envoy and a guard as guests of a people at peace with both, so neither
  side's fighters raise a hand; `LordJob_Parley`: to the gate, wait, home; harmed by the colony, they run and the colony
  broke its word; `JobDriver_Parley`: the speaker walks out with the silver and they swear it; no escort people: sworn by
  word), `TruceObjection.cs` (`Rimfolk_DlmTruce_Avenger` in `Defs/DilemmaDefs/Rimfolk_Dilemmas_Truce.xml`: the avenger's
  objection before the speaker goes out; the vendettas' own thoughts, no new mood), `Pacts.cs` (Pact ties of origin
  `pact`; propose, `Hear`, `Make`, the cart's goods, their offer and renewal (`ChoiceLetter_PactOffer`), the call
  (`Call`, `CallMade`, `CallWhyNot`), their band as a war band (`WarCamps.MakeFighters`/`Start`, `WarBand.called`: only
  their fallen leave a debt, as "protected" for a pact, "aid" for a favour), a debt's band (`FavourBands`), a trade pact's
  season (their caravan, the cart carried out to the gate and paid for), broken by the colony, ended by its letter;
  `PactsDefOf`), `PactCalls.cs` (the comms console or a signal fire by the gate, `JobDriver_PactCall`,
  `JobDriver_SignalFire`, done in danger), `CallsToArms.cs` (their town attacked: a Faction Territories invasion or a bad
  war world event within 10 tiles; their letter `ChoiceLetter_CallToArms` for a pact or a debt; a caravan of two grown
  colonists bound there; the fight on the game's caravan battle map beside the town, or the mod's own invasion fight;
  refused or late: the pact broken, the debt refused), `PactsHooks.cs` (`Noted` from `FactionMemory.Note`;
  `Patch_TruceRaids`, `Patch_TruceRaided`, `Patch_TruceAmbush`; Factional War's battles naming a partner, `sidedWith`;
  Faction Territories' invasion's end told), `PactsPanels.cs` (rows on people and town cards, the tie's card, the people
  card's acts, the inbox: `pacts` in Needs you, `pacts-under-way`, `truces-broken` in Coming; `Alert_PactCall`),
  `FactsPacts.cs` (`tie:pact`, `tie:truce`, `:ours`, `:theirs`, `:due`, a pact's `:defence`/`:trade`, `{PACTWITH}`,
  `{TRUCEWITH}`), `PactWords.cs`, `PactsDebug.cs` (dev actions: their truce offer, one agreed with the envoy due, the
  firebrands now, a truce's end, a pact made, their pact offer, their band now, their call to arms, a trade season).
  Shared, small: `LetterPurpose.Pact`/`Truce` and `OutsiderAct.SpeakPact` at the ends of their enums; `DiplomacyLetters`
  (topics `truce:TERM:PEOPLE`, `pact:KIND:PEOPLE`, `pactend:ID`; asks `price:`, `cart:`), `DiplomacyActs` ("Speak of a
  pact..."), `FavourCalls`/`FavourAsks` (fighters both ways, `AnyLetterFor`), `DiplomacyModel` (the rows and acts),
  `DiplomacyMath` (`dip:truceOffer`, `dip:pactOffer`, `dip:parley`, `dip:band`, `dip:arms`), `Raids.CanRaid(f, home)`,
  `RaidersCamp` (a truce breaks the camp up and none is set up while it holds), `WarCamp` (`called`),
  `WorldMechanics.Battle`, `TerritoriesAdapter` (`InvasionAt`, `JoinFight`, the end told), `RegionViewsMath`/`LensModel`
  (`PlaceFacts.truce` dims the Threats lens), `FactsLedger` (calls `PactTies`), the letter dialog 60 taller, settings
  `diplomacyPacts` and `truceRaids` (with the deeper hooks) in Diplomacy. Defs: `Defs/Misc/Rimfolk_Pacts.xml` (three
  letters, three jobs). Content: 14 talk pieces (`Rimfolk_OutsiderTalks_Pacts.xml`), 29 letter lines and 20 spoken lines
  (`Rimfolk_Lines_Pacts.xml`), keyed words with variants (`Rimfolk_Pacts.xml`), the Almanac's diplomacy page extended
  (five sections after Settings, no keyword changes), README, DESIGN.md. Tests changed with it: `DiplomacyTests` (the
  new purposes and act at the ends of their enums, `Topics(n, p)`, fighters an ask now, Debug files), `OutsiderTalkTests`
  (the ask, the tags, the placeholders); the DefValidator knows `PACTWITH`, `TRUCEWITH`. textcheck reports the Almanac
  page's five new sections as "added outside an existing list" (the check is for prose passes; the page was extended on
  purpose). Left out: hostages as a truce's price (round b's), the harm review's 3.8.1 (an avenger picking a fight with
  their trader, not picked), the envoy hurt or killed at the gate as its own case (2a.3's envoy; now the truce lapses).
  To look at in game: a truce offered by letter after a raid beaten and by their letter; the envoy and guard walking to
  the gate untouched by turrets and colonists, the speaker walking out with the silver, the oath's bubbles, the letter,
  the raiders' camp struck; the storyteller sending no raid of theirs to that home for the term; the firebrands' raid and
  its letter; the truce's end as a peace with goodwill raised to 0; an avenger's objection before the council; a pact
  proposed by letter and face to face, their offer, the renewal letter at the year's end; "Call them" by the comms console
  and by a signal fire by the gate during a raid, their band walking in some hours later, put off and declined; a
  trade season's caravan and the cart carried to their porters; their call to arms and a caravan sent, the fight beside
  their town, and with Faction Territories the invasion joined; Factional War naming a partner; each on the people card,
  the town card, the tie's card, the inbox and the Stance and Threats lenses.

- 0.87.0, UI 5b, one look across every window (docs/design/interface.md, "The look", the second and last part of its fifth
  release; built beside stage 2a.2, which another agent builds at the same time: expect small merges where both touch a
  page, in `DiplomacyPanels` (only its `Accent` line here), `PeoplesView` (one button), `InboxUnderWay` (two rows'
  links), `WeekPanel`, `HudLines`). DESIGN.md, "Rimfolk's windows" → "One look across every window" has the whole of it.
  The tokens are one block in `UI/UiKit.cs` (Ink, Faint, Dim, Heading, Gold, Link, Warm, Cool, Sour, Soon, Danger, Done,
  World and the surfaces); the old names (Good, Bad, Warn, Info, Neutral, Quote, HeaderColor, DreamColor, LossColor,
  StrangerColor, WorldColor) are those tokens, so stage 2's code that reads them draws the set (new code: the design's
  names); `Quieter`, `Wash`, `LevelColor`; every colour helper by token; no page makes a colour (pictures' own colours
  and the game's stay). The grid's measures in `UiKit` (Padding 12, SectionGap 16, CardGap 8, CardInset 10, rows 22, 28,
  40, a card's head 56, icons 14, 18, 24); This week's cards and rows on them (`CouncilLayoutMath`). The components in
  `UI/UiKitLook.cs` beside UiKit and UiKitCards: one `HeadingLine` behind SectionTitle (now with a count), FoldTitle and
  SectionHeading (now with an act on its right); pills as state chips; steps with the current one gold; the empty state
  in a dashed frame and `EmptyLine`; `Row`, `RowAct`, `WhyNotLine`, `DaysLine`/`DaysLineWithWord`, filter chips with a
  count, `NameLink`. Every page of the council window (This week, People's five modes, Council's five, Colony's three),
  the planning window (Plan, Priorities, Base, Making, Rooms' two), the defense window, the region views' old pieces,
  the cards and the Almanac drawn through them: acts greyed with why not (a card and the defense lines with a line of
  the reason under them; nothing refused after the click), filters as chips with counts, view switches as modes,
  section headings with counts, numbers right-aligned in the tables, lines of days on the overview's tiles and the
  colony's mood, the page accents gone. Names as links: the on-screen panels (`HudPanel.NameLink`/`LinkWords`; moods,
  health, work: the name opens the card, the rest of the row still jumps; Following's lines via `HudLine.thing`), the
  hover card in the corner (where the mouse can reach it), This week's journey and plays, the council's pages (decrees'
  askers, offices' holders, wishes' askers and tags, dilemmas' answerers, the chronicle's colonists through the shared
  `ChroniclePanel.TextOf`, bonds, vendettas, nemeses, secrets, faiths' asks and sways, calendar days, every colonist chip
  with `link: true`). Keys in `Languages/English/Keyed/Rimfolk_Look.xml`. `Tools/Tests/LookTests.cs` (301 checks: the
  token block, no colour made outside it in UiKit or anywhere under UI/ and the pages elsewhere, the grid, the
  components, the pages through them, names as links, no bold). Docs: DESIGN.md, README, the Almanac's start page (no
  keyword changes). Left out, and why: the Almanac's own buttons keep the game's button at the Almanac's text size
  (`UiKit.ActButton` draws at the windows' size); the calendar's day dialog keeps its tabs (a dialog, not a page); the
  hover card's name is a link only in the corner (beside the mouse the card moves with it and can't be clicked); the
  colonist bar's badges keep the game's tooltips (they can't hold a link); dialogs (journeys, the stall counter, the
  settings) read the tokens but keep their own layout; map overlays and speech colours are the map's, not a page's;
  the region map's and the little maps' land colours are pictures. To look at in game: every page at 1080p standard and
  large (the section headings with their counts, the tags as dots and words, the greyed acts and their lines, the
  filter chips with counts, the empty states' dashed frames); the overview's tiles' lines and the Moods page's line of
  the week (rising, falling, steady); This week's sections; Decrees' enact greyed with its line and Force below, the
  offices' heading with "Call an election"; the People table's chips and its right-aligned Rimfolk column; the planning
  tables' numbers; a plan the colony can't build in Rooms; the defense lines' greyed steps; the mood board's, health's
  and work's names (a click opens the card, a click beside it still jumps, right-click still the menu) and the Following
  panel's lines; the hover card in the corner with its name clicked; a preview over a panel's name with the panel's
  tooltip giving way.

- 0.86.0, UI 3, one inbox and search (docs/design/interface.md, the third of its five releases; built beside UI 5a and merged
  with it at 12b9521: the corner is 5a's `WindowLayoutMath.CornerWidth()`, 316, and `WindowTabs.LinksWidth` reads it).
  DESIGN.md, "Rimfolk's windows" → "One inbox and search" has the whole of it. Pure and tested
  (`Tools/Tests/InboxTests.cs`, about 1,500 checks): `UI/InboxMath.cs` (`InboxSection` and `Sections` in the design's order,
  `Cap`/`Shown` (six rows, the plan two, "and 3 more" opened in place), `Count`/`Before` (the one count; with no new
  asks it is 0.83's NeedsCount), `Order` (provider by provider, each by its rows' order, equals as found),
  `CaravanStep`, `MissionStep`, `SettlerStep`, `JourneyStep`, `ShowsTimeLeft`, `When` (today, tomorrow, a date),
  `InboxRegistry`), `UI/FollowMath.cs` (`Max` 12, `Toggle`, `Clean`, `FromThingId`, `TakeOldPins`), `UI/SearchMath.cs`
  (`SearchKind`, `SearchItem`, `Fold`, `Score`, `Find` grouped best group first, six a kind, `First`). Game side:
  `UI/Inbox.cs` (`Inbox.Register(provider)` or `Register(id, section, order, rows, needs, footer)`, `Inbox.In`,
  `Inbox.Count` at most twice a second, `Dirty`), `UI/InboxProviders.cs` (Following: `following`, `pinned-wishes`;
  Needs you: `wishes`, `answers`, `asks` (letters still open of the raiders' camps' terms, hamlets, the election and a
  journey at the gate, through each letter class's new `Waiting`), `favours` (`DiplomacyPanels.WeekRows`/`WeekCount`),
  `settlers` (`HamletPage.WeekRows`/`WeekCount`), `promises`; Coming: `calendar`, `meetings`, `fights`, `stories`,
  `offers`, `posts` (`TradingPostPage.WeekRows`), `raiders` (`RaidersCampPage.WeekRows`, Pay while offered); How people
  are: `strain`, `health`, `trust` (with the colony's trend as a row), `balance` (footer); the plan: `plan`, `proposed`
  (footer); `InboxWords`: dates, today, tomorrow, the acts' words), `UI/InboxUnderWay.cs` (`letters`, `journey`,
  `caravans`, `missions`, `hamlet-road`, `camp-price`, `plays` with what is kept on the map, `meeting`, `camp`, `guests`
  (`GuestsWeek.AddRows`), each in steps, never a time left), `WeekModel` (the sections from the providers; `WeekRow`
  gained `sub`, `steps`/`step`, `act`/`actRun`/`actWhy`/`actTip`, `follow`, `faction`; `WeekCard` is a section with
  `count`, `footer`, `opened`; `NeedsCount` is `Inbox.Count`), `WeekPanel` (rows of one sentence with links, 26 or 40
  high, the act's small button, "and 3 more"/"Show fewer" in place, kept for the session), `UiKit.StepsLine` and
  `UiKit.CountBadge` (`UI/UiKitCards.cs`), `UI/FollowWords.cs` (each followed thing's newest change, once a second),
  `WeekMemory.following` (saved; `IsFollowed`, `ToggleFollow`, `Followed`; `IsPinned(Pawn)`/`TogglePin(Pawn)` now
  follow; `pinnedPawns` read from an older save only and followed), the pin on every card's head (`CardView.Head`,
  `FollowTips`), `Hud/HudFollowing.cs` (the ninth panel, `HudIds.Following`, off at first, second column at 60%;
  the settings' panel buttons five to a row), `UI/Search.cs` (`Search.Index` made when the field opens, `Register`,
  `Open`, `Toggle`, the key `Council_Key_Search` in `Defs/Misc/Rimfolk_SearchKeys.xml`; `Window_Search`), the corner
  (`WindowTabs`: search, the inbox with its count, the following list with the panel's switch), `UI/CouncilButton.cs`
  (`MainButtonWorker_Council`, the count on the council button; `Council_MainButton`'s `workerClass`; setting
  `inboxCountOnButton`, on at first, Interface page), icons `Inbox` and `Search`. Keys in
  `Languages/English/Keyed/Rimfolk_Inbox.xml`; This week's keys rewritten in place (none removed). Docs: DESIGN.md,
  README, the Almanac's start page, "the council's tabs", the advisor, meetings and play-out pages (no keyword
  changes). Tests changed with it: `HudPanelTests` (the Following panel's size), and the trading posts', hamlets' and
  diplomacy's checks now find their rows among the providers. textcheck reports one error, on purpose:
  `Council_Misc.xml`'s new `workerClass` on the council button ("added outside an existing list": the check is for prose
  passes; the DefValidator confirms the class). Not built, by the design's order or because nothing feeds them yet:
  stage 2's offers, calls, envoys, council tasks and schemes as providers (2a.2 on), feast days in Coming, war bands on
  the road; the inbox's rows reading the 5b tokens; previews on the search's results. To look at in game: This week with
  each section full and empty, "and 3 more" opening in place, the acts (Answer opens the letter, Decide the Decrees
  page, Pay the camp's confirmation, Put away the news), a letter's and a caravan's steps moving, Coming's dates
  (today, tomorrow, a date); the count the same on the tab, the corner and the council button (and gone with the
  setting off), on the bar both wide and minimized; the pin on a card's head (a town, a people, a debt), a thirteenth
  refused with its message, Following's newest changes, a save from before with pinned colonists coming back followed;
  the corner's following list and the Following panel switched on from it; the search from the corner and from its key
  (with and without a window open), "Bonds", a town, a notable, an Almanac page, Enter and the arrows, a second click on
  the icon shutting it, typing in it never moving the camera.

- 0.85.0, UI 5a, one look: the first part (docs/design/interface.md, the fifth of its five releases, on the player's decisions 3
  and 6; built beside UI 3, which another agent builds at the same time: expect keep-both merges in `WindowTabs.cs`
  (only `Size` changed here), `CouncilSettings.cs`/`SettingsCatalog.cs` (`windowSize` after `linkPreviewDelay`, its
  choice after the Almanac's text size), `Tools/Tests/Program.cs` and `Tests.csproj` (two lines each) and the Almanac's
  start page (its names, previews and defense bullets)). DESIGN.md, "Rimfolk's windows" → "One look, the first part" has
  the whole of it. Pure and tested: `UI/WindowLayoutMath.cs` (`WindowSizeChoice`, `Size` standard 1010 by 720 or large up
  to 1280 by 860 within the screen, `ContentWidth`/`ContentHeight`, `CornerWidth` with the corner as it will be after UI 3:
  back, forward, search, inbox, following, the book and the four windows, 316; `TabsRoom`, `TabWidth`, `NameFits`,
  `IconFits`, `RegionMap`; `DrawerMath.WindowSize` and `ContentWidth` now read it; `Tools/Tests/LayoutTests.cs`, 2,207
  checks over fourteen screens from 768 to 2160 high at scales 1 to 2, standard and large), `UI/Cards/PreviewMath.cs`
  (`ScreenBox`, `PreviewMath.Place`/`Beside`/`Fit`, `PreviewStack`: a preview per level, two at most, the second beside
  the first, kept while the mouse is in either; `Tools/Tests/OneLookTests.cs`). Game side: `Previews` draws both levels
  as immediate windows (`WindowId + level`), knows which level it is drawing so a link there asks for the next, and keeps
  a click on a preview's empty part (`GenUI.AbsorbClicksInRect`). The defense window draws its view through `ViewHead`
  (the "?" to `Rimfolk_AlmDef_Overview`, `PagesMath.Articles["Defenses"]`) and `Drawer.Around` with its own drawer, and
  the way back keeps its card; `DefensePanel` keeps what it did and reads as sections (`UiKit.SectionHeading`, folds for
  the session, counts on the lines, what to do and the plans), facts as rows, acts through `UiKit.ActButton` (Plan it
  here and a plan the colony can't build greyed with why; At the suggested spot greyed where the survey found no spot),
  an empty state for no advice, names as links (the best shot and fighter, the marshal and their portrait, the hostile
  peoples); the Almanac button at the foot of the facts went (the "?" opens that page); `DefenseLinesPanel.Draw` takes
  the folds. Names as links in the game's panes (`UI/Cards/PaneLinks.cs`: `Mark` for a pawn, pawns, a people, a name and
  thing; `ColonyPeople`, `MarkColony` for a character section): Story (`StoryView`: the family's chips, `UiKit.PawnChip(...,
  link: true)`; vendetta and nemesis tags, `UiKit.PillSpec.thing`; the family tree's names; family, years, bonds and
  secrets lines), Culture (a prisoner's people, who won them over), the Social column (bonds, secrets, the colony's
  people in groups, what people say and shared memories), Mood (the one a mood is about in its why line,
  `MoodView.Texts.whyShown`, `why` kept plain for the tooltips); `PawnPane` rows and `CharacterView.DrawSection` draw
  through `LinkText`; a row's or chip's tooltip gives way while a preview shows. The talks (the play log) and the
  game's thought labels stay plain; the Needs line names no one. Keys in `Languages/English/Keyed/Rimfolk_OneLook.xml`.
  Docs: README (the windows' size, the defense window, links and previews), the Almanac's start page and "planning the
  colony's defense" (no keyword changes). Left for UI 5b, once UI 3 is in: the tokens and components across every
  window's pages (council, planning, This week as the inbox), the page accents giving way, `WindowTabs.LinksWidth` read
  from `WindowLayoutMath` (the test assumes UI 3's corner), links in the on-screen panels and the colonist bar's hover.
  To look at in game: the defense window with a card beside (1080p scale 1, the page then in one column) and over it
  (scale 1.5 at 1366 by 768), its folds, the greyed acts and their reasons, the marshal's portrait; the setting
  "Window size" on Large at 1080p and 1440p (all four windows, the region map, the People table, the Almanac's window
  then in the middle where there's no room beside); a second preview from a name inside a preview (beside it, both
  kept while the mouse moves between, the game's tooltips around them, clicks on a preview's empty part); names in the
  Story tab (family chips, tree, tags), Culture (a prisoner's people), the Social column (a bond, a secret, a group) and
  a mood's why line opening the floating card, and the same story in a colonist's card in the drawer.

- 0.84.0, UI 2, the region in four views (docs/design/interface.md, the second of its five releases, on the player's decision
  1; built beside UI 4 and merged with it: the region's views are sections of their own in `PagesMath.All`, so the
  five-views check holds for the region window too). DESIGN.md, "Rimfolk's windows" → "The region in four views" has
  the whole of it. Pure and tested: `UI/RegionViewsMath.cs` (`Lens`, six at most; `Hue`; `LineKind`, `Dash`,
  `LineLook`; `Stance`; `PlaceKind`, `PlaceFacts`; `MarkTone`, `Look`, `Draws`, `LinesOf`, `TieLine`, `DashAt`; the
  List's `RowKind`, `Group`, `Key`, `RowOrder`, `Compare`; `Tools/Tests/RegionViewsTests.cs`), `PagesMath.TryAddress`
  with `Address`, `Modes`, `Lenses`, `MaxLenses` (addresses like `Map/Trade/List`, `Peoples/Faces`; every old region
  name in `Moved`), `WorldHistoryMath.InScope` with `HistoryScope` (`PagesTests`). Game side, `UI/Region/`:
  `RegionView` (the lens, Map or List, Peoples or Faces, kept for the session; `Apply(address)`, `Within` for the way
  back), `LensModel` (once a second while the Map view shows: each mark's colour, arrows, event rings, a head's regard
  ring, event tiles, the lines from the ledger's live pacts, truces, favours, fealty and grudges plus other mods' fealty
  views, the camps' raids, posts to their towns, homes to hamlets; the legend; the List's rows in groups with what came
  before), `MapView` (lens bar, Map or List, "?", the List's rows), `PeoplesView`, `HereAndAwayView`; History is
  `WorldHistoryPage` (UI/WorldPages.cs) with the strip of world events running, the scope chips, "Letters lately" and
  More for other mods' pages. `RegionMapView` draws the lens (`Draw(rect, LensModel)`, `DrawLines`,
  `DrawEventTiles`, the legend strip, `LandLegend`, the panel's legend per lens); `RegionMap.MarksVersion`.
  Components: `UI/UiKitLenses.cs` (`ToneColor`, `KeptWidth`, `Modes`, `LensBar`, `LegendItem`, `LegendStrip`,
  `LegendList`, `DashedLine`, `FilterChip`), beside UI 4's view header (`UiKit.ViewHeader` with `ViewHeadModel`),
  which Peoples, Here and away and History use. Icons `Away`, `LensLand` to `LensOurs`, `ModeMap`, `ModeList`. The old
  pages' code: `WorldPanel` is sections (what's coming without fights, calendar and meetings; who's out there by
  `OutGroup`), `NeighboursPanel` only the town card's Who, `LettersPage` sections (`UnderWaySection`, `LatelyRows`,
  `KnownSection`), `PeoplesPage`, `JourneysPage`, `EventsPage` gone; the posts', camps' and hamlets' pages register
  nothing (their This week rows, card sections and words stay; `RaidersCampPage.AskToPay` is the Threats list's Pay;
  `HamletPage.Title`/`Target` the settlers' rows); `DiplomacyPanels` registers no page and gives `Owed`, `OwedStat`,
  `PeopleBody`, `Section`, `RowHeight`. `MainTabWindow_World.Register` still adds another mod's page, under History's
  More. `Nav.Observe(w, page, mode, card)` keeps a view's lens and mode; `CardWords.DistanceShort`; `Cards.FullPage`
  points at the views. Mood hints (`councilTab` now an address, e.g. `Map/Threats/List`), This week, the advisor and
  the overview lead to the new views; the Almanac, README, keyed strings and letters name the views and lenses. Not
  built, by the design's order or because nothing feeds them yet: war bands and a bandit road on Threats, towns by what
  they trade on Trade, vassals' and kin towns' marks and colonies of the past on Ours (stage 3), pacts and truces (the
  code reads them when stage 2a.2 makes them), tab counts (they move to UI 3's inbox), lens and mode kept in the
  settings file. textcheck reports nine mood hints' changed `councilTab` values (identifiers, deliberately changed from
  `Neighbours`, which now forwards to the Stance list, to where camps, missions, stories, foes and threats are; the
  `Events` hints keep their old name, which forwards to History). To look at in game: each lens on a real region (colours, lines between
  the right places, the legend at the foot and in the panel), the lens bar at 1080p scale 1.5 (names or icons), the
  List with a card open beside it and over it, Pay on a camp's row, Peoples' cards with a debt and their acts, Faces'
  Write, Here and away with a camp on the map, History's strip and scopes, every old page key and link, another mod's
  page under More.
  At the merge: textcheck flags nine mood hints whose page target moved from `Neighbours` to the view that now holds
  their subject (Here and away, Peoples → Faces, the Threats list); changed on purpose, since `Neighbours` forwards to
  the Stance list.
- 0.83.0, UI 4, the council in four views (docs/design/interface.md, the fourth of its five releases, on the player's
  decisions 4 and 5; built beside UI 2, which another agent builds at the same time: expect keep-both merges in
  `PagesMath`/`Pages` and `Tools/Tests`). DESIGN.md, "Rimfolk's windows" → "The council in four views" has the whole
  of it. `PagesMath` (pure): a window of more than one section is made of views (`HasViews`), its sections the views,
  its pages their modes (at most five each, `MaxViews`/`MaxModes`); the council window's views Week, People (Table,
  Moods, Bonds, Faiths, Houses), Council (Decrees, Ambition, Ways, Wishes, Dilemmas), Colony (Overview, Calendar,
  Chronicle); the planning window's Plan (Plan, Priorities), Base, Making, Rooms (Build, Rooms); `Moved` gains
  Roster and Everyone (Table), Dreams and Journals (Chronicle, with `MovedFilters` setting its filter through
  `Nav.SetFilter`), Offices (Decrees); `Almanac`/`IsAlmanac` an address of its own window; a view's name resolves to
  its first mode (`Pages.Open` takes the last looked at); `Articles` gives each page its "?". `PagesTests` checks every
  old name lands in its view and mode, no window over five views or view over five modes (the region window joins
  that check once UI 2 makes its pages more than one section: the skip is one line), each "?" an existing article,
  the view header's layout and the People table beside the drawer at the game's screens and scales. Game side:
  `MainTabWindow_Council` and `MainTabWindow_Planning` rebuilt around page names (no `Tab` enum), every view drawn in
  `Drawer.Around` and `ViewHead.Draw`; `UI/UiKitViews.cs` (`ViewHeadModel`, `ViewHead`, `UiKit.ViewHeader`,
  `ModeButtons`, `HelpButton`) and `UI/ViewHeadMath.cs` (pure): the modes are a row under each page's own header,
  which `UiKit.PageHeader` draws now through `ViewHeader`, the "?" left of the page's own buttons; a page with no
  header of its own gets one from the window. `Drawer` is generalized: `DrawerHost` (one drawer's trail and card),
  `Drawer.Of(window)`, `Drawer.Make()` for any other window, `Drawer.Around(host, ...)`, `Drawer.Active` (the one
  drawn last) where `Cards.Open` puts a card; the region window's calls unchanged. Pieces independent of the tab
  windows (they take a rect and a model), so the game's own windows could use them later. `Almanac/Window_Almanac.cs`:
  movable, resizable, at the top right beside Rimfolk's windows where there is room, its place and size kept for the
  session; `AlmanacPanel.Draw(rect, headerInset)`, each page's reading place kept for the session, `lastAlmanacPage`
  saved by the window now; the book in every corner (`WindowTabs`, the corner 30 wider). People: `PeopleTable` is
  the list (title "People"), a row click opens the colonist's card in the drawer (the row lit), a right click shows
  them on the map; `PeoplePanel` (the Roster) removed, its keys left in place (textcheck refuses removed keys); the
  colonist card's subtitle has their years asleep. `UI/HousesPanel.cs`: the colony's families from blood and
  marriage among its people, and the houses the world ledger knows. The Chronicle's filters Dreams and Journals draw
  `HistoryPanels.DreamsBody`/`JournalsBody` (what were the pages), its colonist lines link their names. Colonist
  portraits and chips in the council window open the card in the drawer (`Cards.Open`), `OpenPerson` from outside
  opens People's table with it. `RoomsPanel` has a page header now (the modes need one). Texts: keyed strings,
  mood hint and key labels, the welcome letter, the bar buttons' descriptions, the Almanac's start page and nine
  more pages, README; new keys in `Languages/English/Keyed/Rimfolk_Views.xml`; the DefValidator knows the new
  dynamic keys (`CC_View_`, `CC_View_Tip_`, `CC_Mode_`). Left out: the view header across the full width above the
  drawer (each page draws its own header, so the drawer runs the content's full height beside the view); the
  Houses mode is the colony's families now, its renown waits for stage 2d; This week's cards as they were (UI 3 makes
  it the inbox); the defense window keeps the floating card (not part of this release). To look at in game: the
  modes' row under every view's title and its place on a narrow window, the "?" beside the stat and the Almanac's
  window opening beside the council window (and its size and place coming back), the People table beside a card at
  1080p scale 1 and 1.5 and over it below, a click on a colonist's portrait on This week and in Wishes, the
  Chronicle's Dreams and Journals, Houses with a married pair or a family, the page keys (Everyone's opens People,
  the Almanac's opens and closes its window), back and forward across views and cards, the old council page
  restored from a settings file of before (Roster, Almanac).

- 0.82.0, UI 1, names that lead somewhere (docs/design/interface.md, the first of its five releases, on its recommended
  decisions 2 and 3: the drawer and previews). All in `Source/ColonyCouncil/UI/Cards/`; DESIGN.md, "Rimfolk's windows",
  "Names that lead somewhere" has the whole of it. Pure and tested (`Tools/Tests/CardsTests.cs`, with `PagesTests`):
  `ThingRefMath.cs` (`ThingRef` over the ledger's `Ref` plus sites `s`, ties `y`, history entries `e`, letters `l`;
  `CardKind`, `CardSlot`, `CardRegistry`), `LinkTextMath.cs` (the marker, three private use characters; `Parse`,
  `Plain`, `Wrap`, `MarkNames`, `Lay`), `NavTrailMath.cs` (`NavStep`, `NavTrail` bounded at 50, `DrawerMath`). Game side:
  `ThingRefs` (resolve, exists and names, kept a few seconds), `Cards` (the eight builders, models at most once a second
  while shown; `Cards.Open(ThingRef)` and overloads, `Cards.ShowOnMap`, `Cards.OpenInWindow`), `CardModel`,
  `CardSections`/`CardActs` (`Register`, `RegisterLines`, and the place card's `Town`, `TownLines`, `PlaceLines`,
  `HomeLines`), `CardWords` (the history's lines with links, a card's story, letters' steps, distance), `CardView`,
  `Drawer` (the region window's, the map's panel; Escape closes it first), `Window_Card` (the floating card from
  elsewhere), `LinkText`, `Previews` (an immediate window on the tooltips' layer, asked for each frame from
  `WeekMemory.GameComponentOnGUI`), `Nav` (the corner's arrows, keys `Council_Key_Back`/`Forward` in
  `Defs/Misc/Rimfolk_NavKeys.xml`, side buttons 3 and 4); `UI/UiKitCards.cs` (tokens and components); icons Back and
  Forward (`Tools/make_icons.py`). The region map panel's fourteen sections are the place card's now (twelve sections
  in its slots and "Plan a journey here" as an act); `NeighbourPanel`'s lists are a shim for a release (a town's in Ties,
  the rest in Now). 2a.1 wired: `DiplomacyLinks.Open` opens cards, its town ties are the place card's Ties
  (`CardSections.Town(CardSlot.Ties, ...)`), `MainTabWindow_World.ShowOnMap` and `RegionMapView.Pick` forward to the
  drawer; the Diplomacy and Household pages stay dev-only. Links in: the world history (names and days), the place
  card, the Neighbours page's towns and people, the Peoples rows and their wars, the Letters rows, a people's chip
  (`UiKit.FactionChip(..., link: true)`), a notable's portrait, a town's name; `Texts.Fill(text, values, things, links)`
  with `FactSet.things` (TOWN, PEOPLE) for later callers; `Tell` and `Speech.Say` strip markers. "Show" on the posts,
  hamlets, camps and journeys pages, This week's place rows (`WeekModel.Go`) and missions open the region map.
  Settings `linkPreviews`, `linkPreviewDelay`. `StoryView.Draw` takes `StoryParts.Card` and an `after` drawer. Left
  for later releases, by the design's order: the drawer in the council, defense and planning windows (they open the
  floating card), the second level of previews, links in the inspect tabs, the Social column, This week and the
  council's pages (UI 4 and 5), the follow pin. To look at in game: a preview beside the game's tooltips (a row's
  tooltip drawn after a link may still show with it), clicks inside a preview, the drawer over the view below a
  content area of 900 (1280 by 720 at scale 1.5), the side mouse buttons on Windows and Linux, a marker never showing
  in a letter or a bubble, Escape on the map with a town picked.
- 0.81.1, the player's report (outdoor gatherings always at the same spot): `PlayYard.Best` returned the top-ranked
  cell every time and `PlayFire.Cell` the nearest free cell to the colony's middle. Now the yard keeps the best 400
  cells, finds up to six distinct places among them (`YardMath.Places`, 8 cells apart), and a gathering picks one by
  `YardMath.Weight` (nearer the best score likelier, none twice `Spread` below, the last three used held back), held
  three hours per map so a gathering, its walk and its fire share it; `PlayFire.Cell` lays the fire at that place
  (else as before). `YardTests` cover the places and the pick.
- 0.81.0, stage 2 release 2a.1, diplomacy's base and favours (docs/design/stage2-diplomacy.md, item 1 and the base): built
  while the player plays, with the design's decisions left alone (no fighters through a favour either way: 2a.2's war
  bands and decision 3; no Empire honour for a favour: decision 9). `Source/ColonyCouncil/Diplomacy/`: `DiplomacyMath.cs`,
  `FavourMath.cs`, `NatureMath.cs` (pure, `DiplomacyTests`, 675 checks), `Diplomacy.cs` (the hub; its hour and day ride
  on the neighbours' pass at `Sched.Neighbours`, no slot of its own), `FavoursOwed.cs` (debts of honour as Favour ties of
  origin `debts`: from a deed of size 10+ in `FavourMath.OurGoodTurns`/`TheirGoodTurns`, hooked in `FactionMemory.Note`;
  personal from a notable helped in person, hooked in `KinMemory.Credit`/`Note`; one open each way per people and colony,
  renewed, 120 days; twelve personal; the line appended to the letter that told the good turn within two hours, else
  `Tell.Minor`), `FavourCalls.cs` (the colony calls in: goods by fortune and age, a word with a third people (+10, their
  own letter, `dip:word`), a colonist they hold walked from the edge to the gate (`KidnappedPawnsTracker.RemoveKidnappedPawn`,
  `dip:freed`), help toward peace (a Promise tie, key `peace`, number 20, a year, `dip:peace`: release 2a.2's truce
  answer should read it as that people thinking 20 better of the colony); honoured by regard 20+/0+/below 0 at
  1/0.7/0.4; refused: deed `brokeWord`), `FavourAsks.cs` (their calls: daily, 1 in 90, a season apart, only with a need
  the world shows; medicine and food by `ChoiceLetter_FavourAsk` (`Rimfolk_FavourAsk`), sent as a `DilemmaEffect_CarryOut`
  to the gate; harvest and refugees by firing the existing missions for them (`MissionPeoples.Only`), kept by the
  mission's deed, broken by its other ends (`Missions.Finish` hook); a dispute (feud, contested seat) by the speaker's
  letter to their head (`dip:side`, deeds `sidedWith`, `tookSides`); overdue or refused: broken, `refused`, -5 goodwill;
  kept: `keptWord`), `DiplomacyLetters.cs` (the letter purposes' frame: `LetterPurpose.Favour`, topics `tie:ID` and
  `side:ID`, the ask in the letter's help field, hooks in `NeighbourLetters.WhyNot`/`Topics`/`Read` and the dialog),
  `DiplomacyActs.cs` (the right-click frame: `OutsiderAct.AskFavour` after Invite, hooks in `Outsiders`), `DiplomacyModel.cs`
  and `DiplomacyPanels.cs` (the Diplomacy page, order 35, and Household, 36, as panels over one cached model, rows as
  pieces; the town section; This week's line), `DiplomacyLinks.cs` (every click on a name; opens the region map with the
  town picked: `RegionMapView.Pick`, `MainTabWindow_World.ShowOnMap`), `DiplomacyWords.cs`, `Nature.cs` (no effect until
  round d), `DiplomacyDebug.cs` (dev actions: make a debt, call one in, they call one in). Shared: `TieRules.Kinds` 14
  (Secret, Scheme reserved; 1,080 ties; the save model reads the kinds), the twenty new deeds in `FactionMemory.Kinds`,
  `AgeFeature` Truce, Hostage, Envoy, Levy (18 features), setting `diplomacyFavours` (section Diplomacy). Facts: a debt
  speaks as `tie:honour` (`:ours :theirs :you :due`, `{HONOURTO}`, `{FAVOURWHY}`), apart from `tie:favour`, whose
  existing talk is about a thing to bring next visit (a deviation from the design's wording, so those lines don't
  misfire). Content: 14 talk pieces (`Rimfolk_OutsiderTalks_Honour.xml`), 15 letter lines and 16 bubble lines
  (`Rimfolk_Lines_Diplomacy.xml`), keyed words with variants (`Rimfolk_Diplomacy.xml`), Almanac `Council_Alm_C_Diplomacy`.
  To check in game: a debt's line in the letter of a prisoner released, a mission done; the Diplomacy page's cards and
  "Call it in" opening the letter with the debt and its asks; an answer bringing goods with the messenger, a word's
  letter from the third people, a kidnapped colonist walking in; their medicine letter and the carry-out to their
  messenger; a harvest mission they ask; the right-click ask and the bubbles; the town section on the region map; the
  Household page. Thin: the household's council tasks, schemes and envoys wait for their releases; their ask names no
  messenger at the gate before the answer (the carry-out brings one).

  At the merge: the Diplomacy and Household pages show in development mode only (`Prefs.DevMode`) until UI 2
  places their panels in the Peoples and Here and away views (the player asked for fewer region tabs); the town panel,
  letters, right-click and This week carry favours meanwhile.
- 4 November 2026, paused for the player's play test (the player's word): after 0.79.0 nothing new is built until the
  player has played and given feedback. Stage 2's first release (2a.1, diplomacy's base and favours) is next and
  waits for it; 2a.2 and 2a.3 wait also for the stage 2 decisions 1 to 6, stage 3 for its 17 decisions, the harm
  review for the player's picks. Bug reports first, as always.
- 0.80.0, stage 1 polish: the review's open doubts of 0.77.1, each
  checked in the code and the 1.6 game, and the facts the 0.79.0 text pass found missing, with their talk.
  - A raiders' camp's raid whose side had no edge cell reaching the colony came from anywhere (`EdgeWalkIn` falls back
    to `TryFindRandomPawnEntryCell`) while its letter named the camp's side: fixed. `RaidersCampMath.Sides` tries the
    camp's own side (the game's `GetRotFromTo`), the side next to it nearer the heading, the other one, the far side,
    then any open cell on its own side (the game's own fallback); when the side used is not one the camp's eight-way
    direction names (`SideFits`), the letter adds `CC_RaidCamp_RaidRound` ("...coming in from the east").
  - A warlord's son taken (a prisoner, a slave, one of the colony) or dead still named his camp, and one held where he
    wasn't spawned (a caravan's prisoner) still had his combat power taken off its raids (`Raids.Send` subtracted it,
    then `Lead` refused him): fixed by the son event's own words ("His band will not stay a band for long without
    him"). `RaidersCamps.LeaderLost`; the camp breaks up once no raid of theirs is on the map and no price is on its way
    (`RaidersCampMath.BreaksUp`), ends as given up with `CC_RaidCamp_End_GaveUp_Leader` naming him, and is not put back
    after an attack that took him; `Raids.CanLead` now decides both the subtraction and the walk-in.
  - A post given up for hard times (rise and fall's struggling rule, or the town-abandoned event) ended with no line of
    its own on the posts page: `PostEnd.HardTimes` writes one `world:post:closed` line (`CC_Post_H_HardTimes`) when the
    post's town is gone and the region keeps its ruin as given up; no message (the giving up was told).
  - With the new towns' faces off, foundings whose towns stood waited for good (up to 6, founders kept): the day's look
    (`RegionMath.FoundingDay`) lets them go with their founders, as it already did for those finished while off.
  - `FarNews` used `town.Name` of a town with no name: far towns must have one (`HasName`).
  - A hostile town given up quietly brought another people's refugee family (`CampTracker.StartKind` falls back to a
    friendly people): a family comes only of the town's own people (`RiseFallEffects.SendsOwn`; a sacked post's too).
  - Facts (`World/Region/RegionNearFacts.cs`, from `Facts.Region`, each part in its own try/catch): `region:camp`
    (`:theirfoe`) with `{RAIDCAMP}` `{RAIDCAMPPEOPLE}` for a camp the colony knows of; `region:ruin` (`:theirs` or
    `:other`, `:fight`) with `{RUINNAME}` `{RUINPEOPLE}` for a ruin on the map within the home's reach; `them:seeking` and
    `them:seeking:REASON` with `{SEEK}` for a traveler's camp; `town:resettled:own` or `:newcomers` in a resettled town's
    first year (from the history's `world:resettled` entry at the town); `their:givenup` with `{GIVENUP}` (their people's
    town given up in the last 30 days, from the region's ruins); `town:situation` (`:bad`, `:good`) with
    `{TOWNSITUATION}`, the running world event whose place is their town (not its giving up). In `FactsMath`, the
    DefValidator and `OutsiderTalkTests`. Talk: 60 pieces in `Rimfolk_OutsiderTalks_RegionNear.xml` (a camp near 11, a
    ruin 11, travelers' reasons 16, old walls 6, a town given up 8, the event at their town 8), every age, warm, cool
    and sour. Tests in `RaidersCampTests`, `TradingPostTests`, `RegionTests`, `RiseFallTests`, `TravelTests`.
  - Not changed: the camp's found letter still says its raids come "from the {DIR}" (true unless that side has no way
    in, which the raid's own letter now says). To look at in game: a raid from a camp beyond a mountain or a lake edge,
    the son taken in a raid and his camp breaking up, the new facts in talk with travelers and a resettled town's people.
- 3 November 2026, the player's rule change (binding): harm is part of the game. Features may hurt pawns (injuries,
  illness, attacks, fights) when flavourful or interesting, at a rate comparable to vanilla's chance of something bad
  happening; never gratuitous, always from something on the map. It replaces "nobody is hurt for flavour" in
  CLAUDE.md, DESIGN.md, docs/briefs/agents.md. Existing features that say "nobody is hurt" describe how they work
  today; they change only when a feature is reworked or the player asks.
- Queued after the current to-dos (the player's request): review every existing mechanic under the harm rule and
  give each its negative side where it fits, preventable or not ("everything should be possible"); propose per
  system first. In ROADMAP.md. The right-click interactions (queued after notables and letters) include a sparring
  bout, insult or provoke, and a duel of honour where a people's ways fit.
- 0.79.1, the player's report (Toni got "building work by my bed" while working across the base): the nuisance
  `BuildingWork` (`Strangers/Nuisances.cs`) fired for any frame of ours within 6 cells of the bed, through walls, worked
  on or not, wherever the colonist was. Now: they are in their bed's proper room, and the frame stands in that room or
  against it (`InOrBy`) with someone of ours on `FinishFrame` at it now (`BeingBuilt`).
- 0.79.0, text pass: the region's voices (the words around stage 1's features, every age). Talk: 132 new pieces in six new
  files, each on facts the providers set: `Rimfolk_OutsiderTalks_PostsMore.xml` 34 (posts 14 -> 48: the post's people by
  age, season and tone, the keeper as `role:head`, the warning, their people's and others' posts, `deed:postRazed`,
  the colonists' own means of getting there by the colony's age), `_HamletsMore.xml` 30 (hamlets 14 -> 44: by age and
  season, its head and founder, `deed:hamletHarvest`/`hamletHelped`/`hamletRefused`, fealty by their rite, a grown
  hamlet), `_RaidersCamps.xml` 22 (new: `deed:brokeCamp`, `burnedCamp`, and the camp's own people's `camped`,
  `campBroken`, `campFailed`, `paidOff`, `campStruck`), `_Ruins.xml` 16 (new: `ws:Rimfolk_WS_GivenUp` while it runs and
  just after, `town:lately:world:resettled`, `town:why:ruin` and `:refugees`), `_FollowUps.xml` 13 (new: the grown town
  6, the warlord's son 7 at the gate, in exile, with riders, after), `_RememberedMore.xml` 17 (remembered 7 -> 24:
  known by name before they've met, {FRIEND} and {FOE} by kind, `you:foe` cool and sour). Letters and news: a keyed
  text may have numbered fellows (`CC_X_2` to `CC_X_6`), picked at random by `TextVariants.Vary`
  (`Text/TextVariants.cs`); 84 in `Languages/English/Keyed/Rimfolk_RegionVariants.xml` (posts 15, hamlets 10, rise and
  fall 23 with the resettled and renamed by age, new towns begun and built 7, raiders' camps 12 with the raid letter and
  a raid beaten off, far news and travelers 17). Code, one line each where the words are read: the `T` helpers of
  `FarNews`, `HamletWords`, `RiseFallWords`, `TradingPostWords`; `RegionWords.Begun`/`Built`; `RaidersCamp` (the
  sighting line, the raid text, a raid beaten off, the ending); `CampSeekers` (why they came, the prospectors'
  quarrel). Only keys with a `_2` vary, so panels and history lines read as before; the hamlet's ask (retold when the
  place changes) has no variants on purpose. Bubbles: 9 of the keyed above (a camp sighted, a visitor's warning, far
  news, the quarrel), and `Patches/Rimfolk_SeekerLines.xml` gives the prospectors and surveyors `[seek:ore]` and
  `[seek:ruins]` moment lines for their walk out (a conditional add: textcheck refuses a new list in an old def), the
  scholars two more. World events: a town given up 3 texts more, the town that grew 4, the son's endings 5. Thin still:
  no fact names a raiders' camp standing near the colony or a ruin in the region (only deeds after the fact), so nobody
  remarks on one while it is there; no fact for a camp of travelers who sought out the remote (`seeking` on the camp is
  not in the facts), so their talk is the far news only; a ruin's own people coming back and newcomers read the same
  (the history knows, the facts don't); `{SITUATION}` of a town given up reads "a town given up", so its pieces use
  `{SITUATIONPLACE}`; the son's band can't be talked of (his people are at war). Not a text fix: `RaidersCamp.Found`
  translates `CC_RaidCamp_Say_Letter`, a key that doesn't exist, when a neighbour's letter finds a camp (nobody says it,
  but the missing key is looked up).
  Fixed at the merge: a camp found through a neighbour's letter no longer looks up the missing key
  `CC_RaidCamp_Say_Letter` (the line is made only when someone on the map says it).
- 0.78.0, text pass: everyday colony life (words only, no code). 1,151 new lines in seven files, all in pools the game
  already draws on: talk 643 (`Rimfolk_Talk_Lines_Everyday_Closes.xml`, `_Any.xml`, `_Times.xml`: closing words for
  all 23 topics 343, any topic's close, reply and last word 95, comfort 103, the three jokes 58, eleven memories'
  closes 44; extra `TalkLinesDef`s that `Talk.BuildLines` joins to the pool of the same topic or memory and turn), plays
  on the map 113 (`Rimfolk_Lines_Everyday_PlayOut.xml`: gift, burn, fetch, rounds, the enemy dead, a secret told),
  weather and habits 183 (`Rimfolk_Lines_Everyday_Weather.xml`: forecasts 61, records, warm spell and lightning 35,
  small habits 43, the herd's leader, the beast and a following animal 34, hail and a fight won 10), the social log
  196 (`Rimfolk_LogLines_Everyday.xml`: three more `gossip_topic` phrases for each of 41 deeds, 73 rules for nine
  moments and frictions and bitter council talk), nuisances 16 (draught at the bed, butcher's smell, in place).
  `Tools/variety_report.py` now counts talk-line defs the game joins as one pool. Top seen-per-line 3.00 to 1.89.
  Still thin: the any-topic reply, close and last word and the comfort pools (about 1.8: drawn most, and comfort's
  lines split eight ways by feeling), the common topics' openings and replies (council, food, newcomer, raid, 1.6 to
  1.9; `Rimfolk_TL_Drugs_Open` flagged), topic closes (about 1.5, split by outcome and side), the tease pools (plain
  and cheerful only). The report can't see a caller's own tags (give, burn, fetch; the herd's first, line, new), so
  it undercounts those pools' tagged lines.
- 0.77.1, stage 1 review: fixes. The two hooks on the game's
  `SettlementDefeatUtility.CheckDefeated` (`Patch_RiseFallDefeated`, `Patch_TradingPostRazed`) stand down at once for a
  town with no map: the game calls it for every town every 15 ticks (every tick with the world open), and the posts'
  hook walked every home's posts each time (`RiseFallTests` checks both). Raiders' camps switched off (0%): a hidden
  camp is no longer found (`TryFind`, `TryWarn`), so no threat letter promises raids that never come; it goes quietly
  after its 60 days. A trading post whose home's records were carried far away (a gravship landed elsewhere, the colony
  settled anew: `ColonyBook`'s carried entry) ends as a post in the region's day (`TradingPostMath.StaysNear`: the home's
  layer and the region map's reach), instead of blocking that home's posts for good and being told as near. A hamlet's
  settlers who give up before it stands while their people are not at war with the colony (the place taken and no other
  found, their people fallen apart) are told so (`CC_Hamlet_End_NoPlace`), not "their people and the colony are at war".
  A settler the colony took prisoner, enslaved or took in is no longer one of the hamlet's settlers (`HamletHome.Living`):
  before, the caravan's pickup took a prisoner out of the colony's cells and off the map with the rest, and such a
  leader could found the hamlet and head it from a cell. A follow-up whose day failed with an error lets its person go
  from the world's keep too (it stayed kept for good).
  Not changed, to look at in game: a raid from a camp whose side has no edge cell to walk in from comes from anywhere
  while its letter names the camp's side; a post given up for hard times by rise and fall ends its record without a
  closing of its own in the history; a recruited warlord's son still names his camp and still counts against its raids'
  points.
- 0.77.0, stage 1 release 6, the hamlet (item 6, decisions 4 and 9 to 11): `World/Region/HamletMath.cs` (pure, `HamletTests`),
  `Hamlet.cs` (`HamletHome` on `RegionHome.hamlet`, `Hamlets`: day and hour, looks, the ways in, ends, `KeepFor.Hamlet`),
  `HamletPlace.cs` (6 to 10 tiles by the posts' rules, `WhyNotHere` for the world targeter), `HamletAsk.cs`
  (`ChoiceLetter_Hamlet` for the settlers' ask, help, oath and war; `WorldEffect_Hamlet` in `Rimfolk_WS_GivenUp`'s
  refugees beat), `HamletEscort.cs` (the settlers' camp `Rimfolk_Camp_Settlers` from the settlers themselves or the
  holding, the caravan within 3 tiles bound for the place, kept beside it, the road's one roll of `CaravanDemand` or
  `Ambush`, alone a third turned back), `HamletFound.cs` (the game's `Settlement` of their people, kind `Hamlet`, 25 or
  12, founder and families, `StaffHamlet`, Fealty tie key `protection`, `world:hamlet:founded`, deed `hamlet`),
  `HamletLife.cs` (harvest carter, hard season asks carried out at the gate, two refusals in 15 days break the tie,
  fealty after 60 days at 40: Faction Territories' `ExecuteVassalisation` by name or the tie's key `oath`; war: families
  join or stay), `HamletWords.cs`, `HamletPage.cs` (page `Hamlets`, This week while waiting), `HamletFacts.cs`,
  `HamletNotables.cs`, `HamletPatches.cs` (`Patch_HamletCaravanLine`, `Patch_HamletFight`), `HamletDebug.cs`;
  `AgeFeature.Hamlet` and `Oath` (14 features); deeds hamlet, hamletHarvest, hamletHelped, hamletRefused; facts
  `town:hamlet`, `town:hamlet:sworn`, `their:hamlet`, `region:hamlet`, `{HAMLET}` `{HAMLETWORD}` `{HAMLETPEOPLE}`; 14 talk
  pieces; settings `hamlets`, `smallTownMaps`; Almanac sections in the region page. Release 7's hooks are called:
  `ColonistMemory.WalkedSettlers` when it stands, `GrownTowns.Note(..., "hamlet")` when its second year ends (a grown
  hamlet's record stays a town and the hamlet stays the colony's). Decision 4: `SmallTowns.cs` (`Patch_SmallTownMap` on
  `SymbolStack.Push` for `settlement` and Vanilla Expanded Framework's `kcsg_settlement`, read in its source: the
  ground shrunk, defenders and stores fewer; Empire and gravcore bases keep their ground). Left: the warlord son's
  settlers branch; a staged defense at the hamlet (decision 11: only Faction Territories' invasions fight there); a
  hamlet's own letters beyond its asks. To check in game: the ask's look and the targeter, the settlers' camp and
  pickup by a caravan (the panel line), an ambush at the place with the settlers fighting, the founding letter and the
  region map panel, the harvest carter, help carried to the gate, the oath with and without Faction Territories, a small
  town's map with and without Vanilla Base Generation Expanded.
- 0.76.0, stage 1 release 7, follow-ups (decisions 12 and 13): `World/Region/FollowUpsMath.cs` (pure: the son's branch by
  the warlord's ending, the window, his age, what he asks; the grown town's boost; `CampWarnMath`), `FollowUps.cs`
  (`FollowUp`, `FollowUpsHome` on `RegionHome.followUps`, six waiting a home; due 60 to 180 days after, started in the
  region's day when `SlotFree`, one a day, let go 60 days past the window; keep reason `followup`), `WarlordSon.cs`
  (`WarlordSons.Remember`/`Watch` hooked in `WarlordWorker`; `SituationWorker_Son` and four defs in
  `Rimfolk_WS_WarlordSon.xml`, kind `son`: Band via `RaidersCamps.SetUp(home, people, son, "son")`, Gate asking for his
  father (`DilemmaEffect_KinFree`), to meet him, a token on his grave, his weapon (thing id noted while the warlord was at
  a home) or a peace (ledger Promise `warlordson`), Exile (shelter; hunters at war raid 30%), Riders; his parent's
  child by `PawnRelationDefOf.Parent` while the game has the parent, else by name), `GrownTown.cs` (`GrownTowns.Remember`
  hooked in the other settlers' `Ended`, `GrownTowns.Note` for a hamlet's second year; `Rimfolk_WS_GrownTown`: kind
  Town, prosperity +15 to 90, `NotableBook.MakeHead`, `world:grown`, a name by their age, the first market's carter,
  `first:FLAG` lines), `RaidersCampWarnings.cs` (partial `RaidersCamps`: the leader kept hourly, on the camp's map at
  `MapMade`, `fought` noted; a hidden camp found by a visitor's warning or a neighbour's letter), `World/ColonistMemory.cs`
  and `ColonistMemoryMath.cs` (one ledger Regard tie per people and colonist, origin `colonist`, marks in its key, six a
  people, 48 in all; kinds campBroken, foughtBeside, tradedPost, walkedSettlers, killedRaid/Field/Peace, executed,
  harvested, plus every deed `KinMemory.Name` gives; read by `KinMemory.Friend`/`AddTags`, the facts' `{FOE}`,
  `you:foe`, `you:remembered`, `Notable.OpinionOf`/`Known`, and `FactionUtility.CanTradeWith` at -12). Hooks for later
  releases: `ColonistMemory.TradedAtPost(f, negotiator, value)` (posts: wired at the merge, `MemoryHooks.Traded` when
  `TradeSession.trader` is a post's town), `ColonistMemory.WalkedSettlers(f, escorts)`
  and `GrownTowns.Note(home, town, leader, firstYear, tale)` (hamlets). Bug fix: the ledger's daily check now knows a
  camp leader's `raidcamp` reason (it dropped it, so a leader could be let go). New deed `brokeCamp`; 7 talk pieces
  (`Rimfolk_OutsiderTalks_Remembered.xml`); Almanac: world events (the son, the town that grew), what the world
  remembers (by name, for good or ill), raiders' camps. `FollowUpTests`. Dev actions (`FollowUpsDebug.cs`): the son by
  an ending and a people, a town grown now, every follow-up due now. To check in game: the trade option greyed with
  the reason (`CanTradeWith` is what the float menu and a caravan's negotiator use), the son on the camp's map, the
  father walked out to his son, the grown town's rename in the notables and the region map. Left: the sword is the
  weapon's kind from the stores (any of that def), settlers as a son's branch and the hamlets' calls wait for their
  release, talk pieces for given-up or resettled towns (the writing pass has fortunes).
- 0.75.0, stage 1 release 4, trading posts (decision 6, a real town of theirs): `World/Region/TradingPostMath.cs` (pure,
  `TradingPostTests`), `TradingPost.cs` (`TradingPost`, `TradingPostHome` on `RegionHome.posts`, `TradingPosts`),
  `TradingPostWords.cs`, `TradingPostPage.cs` (region page `TradingPosts`, This week while warned), `TradingPostFacts.cs`,
  `TradingPostNotables.cs` (`NotableBook` is now partial: `StaffPost`), `TradingPostPatches.cs`
  (`Patch_TradingPostRazed`), `TradingPostDebug.cs`; `AgeFeature.Post` (word: the colony's means; rite: the post's kind);
  `TownKind.Post` on the ledger. Who: at peace, goodwill 40 or allied, three `deed:traded` in 120 days, nearest town 15+
  tiles, `baseTraderKinds`, a naming rule (the Empire too). A look every 15 days per home in the region's day, 20%, two
  a home, one per people, 60 days after an opening or closing. Where: flood fill to 12 tiles, 6+ from the home and
  >6.5 from every colony settlement, 3 from towns, valid and empty, not in others' territory; toward their nearest
  town within 60 degrees when any (`AngleAt`), x3 road, x2 river. Name `{TOWN} Trading Camp/Market/Depot/Ground
  Station`, prosperity 40 (cap 70), keeper (trader kind; Empire: a titled kind) and trader notables at once, keeper
  titled trader/factor/manager/quartermaster (`TradingPosts.KeeperTitle` in `NotableBook.TitleOf`), letter, history
  `world:post:opened` (family `world:post`: warned, mended, closed, sacked, razed). Caravans x1.5 (in
  `Patch_RiseFallCaravans`), bandit road x2 (in `WorldSituations.TryStart`), both behind Systems talk to each other.
  Sour 10 days: warning; 10 more: closed through `RiseFall.MakeRuin` when `SafeToClose` (else waits); war: closed
  within a day; sacked 5% every 15 days with an enemy town within 20 tiles (fortunes on): -30 or given up under 10
  with a refugee camp; razed: deed `postRazed` (30). Facts `town:post`, `town:post:warned`, `their:post`,
  `region:post`, `{POST}` `{POSTKIND}` `{POSTPEOPLE}` `{KEEPER}`; 14 talk pieces; setting `tradingPosts`; Almanac
  section in the region page. Left: the factor's own letters beyond the opening, warning and mending (the colony's
  letters to the keeper and trader travel to the post as to any notable); the post's heir and envoy are the notables'
  usual staffing. To check in game: a post opening (its tile and heading, the letter's look, the keeper's title in the
  panel), trading there, the warning and closing, a sack, razing one, an Empire post's keeper.
- 0.74.0, the stage 1 writing pass: 168 talk pieces in four new files (`Rimfolk_OutsiderTalks_Ties.xml` 61,
  `_Lately.xml` 41, `_Ages.xml` 33, `_Fortunes.xml` 33) and 51 letter lines (`Rimfolk_Lines_NeighbourWrites_Ties.xml`,
  a pool extension of `Rimfolk_Pool_NeighbourWrites`); new placeholder `{FORTUNEWHY}` (`FactsRegion.cs`, the world
  event a town's panel names as the cause of its fortune, recent and fitting its band). Thin still: ties the game
  never makes (a favor owed by the colony, a grudge coming due); `world:battle`, `world:heard`, `world:founded` never
  reach a town's lately; no placeholder for the world event at their town (`{SITUATION}` is their people's newest);
  `{FORTUNEWHY}` names world events only; a favor's goods unnamed.
- 0.73.0, stage 1 release 3, rise and fall: `World/Region/RiseFallMath.cs` (pure), `RiseFall.cs`, `NewTowns.cs`,
  `RiseFallLedger.cs` (`LedgerRuin`), `RiseFallSituation.cs` (`Rimfolk_WS_GivenUp`), `RiseFallWords.cs`,
  `RiseFallEffects.cs`, `RiseFallPatches.cs` (`SettlementDefeatUtility.CheckDefeated` postfix notes a fall); given up
  (under 15 for 30 days, or under 25 with two bad events in 60, or a new town under 10 in its first 15; not a seat,
  its people keep two other towns, `SituationWorker_Abandoned.Safe`); the game's `AbandonedSettlement` as a ruin; a
  fight's ruin once only a caravan or nothing stands (30 days at most, six waiting); resettled after 120 days (own
  people within 25 tiles at 50+, else a people at peace at 60+); Rimfolk's own towns from one flourishing 60 days, 6 to
  15 tiles out, not in others' territory, under 10 in the reach; history `world:abandoned/ruined/ruingone/resettled`;
  effects on provisions and rebuild asks, refugees, the refugee camp and trader caravans' people; setting
  `regionChange`. Rates per home's reach: given up 7% every 5 days (one per 60), resettled 10% every 15 days (one per
  120), founded 15% every 15 days (one per 90), Rimfolk's ruins 12 kept, gone after 300 days. Not done: talk pieces
  for given-up or resettled towns; the beggars dilemma's odds. Unknown: whether a Faction Territories invasion ever
  razes a town without a `DestroyedSettlement`.
- 0.72.0, stage 1 release 5, raiders' camps: `World/Region/RaidersCampMath.cs`, `RaidersCamp.cs`, `RaidersCampTerms.cs`,
  `RaidersCampPage.cs`, `RaidersCampDebug.cs` (dev actions); each home looked at every 10 days from day 60 (1,000
  threat points, no camp, 60 days since the last; 10% x rate); a hostile people by grudge then distance; 6 to 10 tiles
  out; hidden until a colonist out of doors sees its smoke (no raid before the letter); the game's `BanditCamp` site;
  raids from its side (`Raids.Send` gained an edge cell and whether to tell the storyteller) at the raid-source curve,
  0.6 of threat points; ends: broken (put back if a quarter stand), paid (half a raid's points in silver carried to
  the edge; a word for a year on the ledger, one in ten broken), peace (the game's own letter plus a news line), an
  ally (10% every 5 days), gave up (two raids beaten off or 60 days); history `world:camp:*`, six deed kinds; region page
  "raiders' camps"; setting 0 to 200%. Hook for release 7: `RaidersCamps.SetUp(home, people, leader, why)`. Left: other
  ways to find it (a visitor's warning, a neighbour's letter), the leader on the camp's map and those who fought there
  remembered (release 7), a put-back camp's loot halved only once.
- 0.71.0, stage 1 release 2, travelers to the remote: `World/Region/TravelMath.cs`, `TravelLand.cs` (reasons read from
  the land once a day per home, in memory), `FarNews.cs` (news of far towns: nearest 12 beyond the reach within three
  reaches, 2 a stay, into Heard lately, history `world:heard`, colony news), `Camps/CampSeekers.cs` (`seeks` on eleven
  kinds), scholars (`Rimfolk_CampKinds_Scholars.xml`, `seekOnly`, weight 0.6), camp lines may use `[their:AGE]`;
  rates through `CampTimer` and `CampTracker.Blocker` and the event timer's visitor weight (+60% visitors only;
  through the storyteller by `ChanceFactorNow`); setting "Travelers to the remote" (0 to 200%); the prospectors'
  quarrel (12% a stay). Invitations: `Missions/InviteForms.cs`, `InviteMath.cs` ({HOST} {RITE} {GUEST}; Empire
  title via the ledger, the people's ritual with Ideology, else `Ages.Form`). `TravelTests`. To check in game:
  ThingMaker books' titles, discarded titled pawns, `Precept_Ritual.Label` in a sentence.
- 0.70.0, the interface (the player's word): four bar buttons, each its own window with the four in its corner
  (`UI/WindowTabs.cs`): Council (`MainTabWindow_Council`: Council, People, Colony, Almanac), Region
  (`MainTabWindow_World`, button `Rimfolk_WorldButton` labelled "region" since the game has "world": the map drawn
  in it by `RegionMapView` (the separate map window is gone), Neighbours, Peoples, Letters and calls, Journeys, World
  events, History; `MainTabWindow_World.Register` adds a page in one line, `WorldPages.cs`), Defense, Planning
  (`MainTabWindow_Planning`, kept `Council_RoomsButton`'s defName; `RoomsPanel`; settings planButton/roomsButton merged
  once). One page list and router (`PagesMath`, `Pages.Open`); old addresses forward. Settings `worldButton`,
  `defenseButton`, `planButton`. DESIGN.md "Rimfolk's windows", `PagesTests`.
- 0.69.0, stage 1 release 1, the region's towns (with decisions 1 and 3): `World/Ledger/LedgerRegion.cs`
  (`LedgerTown` kind, founding, founder, mother town, reason, prosperity, last move; `LedgerHouse`; `LedgerFounding`,
  6), `RegionBook`/`RegionHome` per home (`ColonyBook`), `Pass.Region` hourly at tick 107 (`Sched.HourlyOnCycle`, a
  new kind of slot LeanTests accepts) and `Pass.DailyRegion` at 1373, `RegionTowns` (nearest 40 humanlike towns per
  home within `regionMapRadius`), remoteness (not saved), `WorldSituationDef.prosperity`/`prosperityOther` (32 events),
  founders (`KeepFor.Founding`), two families, the envoy born to the second, a reason from the land, `NotableMath.MaxNew`
  (notables cap 42), `AgeFeature.Founding`, Faction Territories' message held then dropped or given back, facts
  provider `region` and 40 talk pieces, `FactScene.Event` and 32 memory lines in 22 events, Almanac
  `Council_Alm_C_Region`. Decision 1: `Rimfolk_WS_NewTownMet` (`World/Region/RegionWelcome.cs`, within 15 tiles, once a
  year per home, a free situation slot). Decision 3: `Patch_RegionStock` (postfix, Priority.Last, on
  `Settlement_TraderTracker.RegenerateStock`; struggling 0.75 food, medicine, silver; thriving silver 1.1,
  flourishing 1.2). Settings section "The region": regionFortunes, regionStock, regionFaces.
- 0.68.0, stage 0 step 7b (F7b), stage 0 done: `ColonyBook` (shared per-colony rules: old record to the seat, a
  lost home's kept 30 days, carried on), per colony: memo pick-ups and unanswered event letters, the calendar
  (`ColonyCalendar`; `CalendarTracker` is the book; founding day per settlement), all Interplay officers, the speaker
  in letters and talk, the annals' seats and raids, quarantine factor, the eight questions, camps (`CampHome`), encounters,
  herd and beast, `Battle` per home, social events and groups, the year letter (a part per colony), names with a colony
  key (founders and firstborn too), advisor keys and automation budgets, journeys (one plan per home), about 25
  News.Post calls given their map. Left: one faith per people; vendettas, fight names in marks and pets pick the last
  fight when two homes fight; the seat's calendar alone keeps nemeses and world events; remembered camps, the great
  beast, chronicle, annals, wishes, trust and the heart's cooldown shared; small ODD items in the audit. Edge
  differences: a message now with its map counts toward that home's Tell cap; an old save with no calendar whose only
  colony was settled after day 2 takes its settlement date as founding day; "Not this" from before a second colony
  stays at every home. `ManyHomesTests` (188).
- 0.67.1, stage 0 step 5 (F5): `Text/Facts/` (`FactScene` Talk/Letter, `Facts.For` -> `FactSet`, providers in
  FactsWho/Place/World/Colony/Marks/Ledger/News; read-only, each in its own try/catch), `OutsiderFacts.For` a one-line
  call, `Text/Fill.cs` (`Texts.Fill`, `FillLine`, `Holes`, `Capitalize`, `WithThe`). New tags `tie:KIND[:ours|:theirs|
  :you|:due]`, placeholders {FAVOURFOR} {PROMISEDTO} {CLAIMON} {DEBTTO} {FEALTYTO} {GRUDGEAGAINST}, `town:lately`,
  `them:lately` (with kind and family), `their:sameage/behind/ahead`. `FactsTests` (1500 generated talks identical;
  fill against every Defs/Keyed placeholder text). Left: CalendarKeeping, PlayWords, Pressures (game grammar), the
  council fills (OfficesElection, ImpactHints, ImpactMath, DilemmaImpact), the callbacks' `Since` (reasons in DESIGN).
- 0.67.0, stage 0 step 7a (F7a): `ColonyState.cs` (per colony: offices and election, decrees, ambition and meeting,
  dilemmas and traditions; keyed by `Homes.KeyOf`, saved in the council component as `colonies`; `For(map)`, `Of(pawn)`,
  `Ticking()`), rules in `Lean/ColonyStateMath.cs` (old save to the seat, a lost home's council kept 30 days, carried to
  the next home when none is left, a new lone seat takes the one just left). `CouncilGameComponent.offices/decrees/
  ambitions/dilemmas` are properties reading the seat's. `OfficeTracker.HeldBy`. `UI/ColonyView.cs` (the colony in
  view, "Another colony" button), `Rimfolk_Colonies.xml`, `ColonyStateTests`. One colony: the same people in the same
  order (filtered PawnsFinder lists, not `Homes.People`). Left for F7b: memo pick-ups by colony, an unanswered event
  letter holding every home, the calendar and keeper of days, herdmaster, `Interplay.Officer()`, the speaker in
  NeighbourLetters and TalkScene, the annals' seat list, quarantine's `VisitorFactor`, the one faith of a decree,
  dilemmas still asked of the seat (Workers_Living traditions, camps, GuestWill, shared names, guardians,
  MovingOnHeld, ResidentBeast, Encounters), camps, encounters, herd, social events and battle state, groups, the year
  letter, epithets' colony key, advisor and automation keys, journeys, map-less News.Post. Not built: moving the
  seat, merging two councils.
- Harm review: `docs/harm-review-proposals.md` (37 systems, 94 proposals, a pick each, top ten, shared rules); waiting
  for the player's picks.
- 0.66.3, stage 0 step 3b (F3b): the ledger owns persons and towns (`LedgerPersons.cs`: every notable, anyone a tie
  names, anyone kept; a gone town two years), grudges (one tie per people with a count, `Happenings/Grudges.cs`),
  favours and words (`Outsiders/OutsiderTies.cs`), guest kin as the new kind `Claim` (can be called in), the silver
  debt (`Scenarios/ScenarioDebts.cs`; the memo stays as the scenario's handle), granted as Fealty (the deed kept), a
  notable's own regard and a regular's bond as Regard ties; per-colonist regard, outsider pairs and camp warmth stay
  views (moving them could pass the 300-regard cap). One keep-in-world: `LedgerKeep.cs` (`Keep`, `Release`, `Held`)
  for notables, situations, memos, the departed, regulars, camps and story chains (missions keep the game's own quest
  reservation); the old held-elsewhere checks call `Held`. Old saves move in at FinalizeInit (`WorldLedger.Adopt`, no
  history entries); the notables' handover at PostLoadInit. Regard ties don't write to the history (merge fix).
- 0.66.2, stage 0 steps 8 and 9 (F8, F9): `Integrations/World/` (`WorldAdapter`, `WorldMechanics` finding adapters by
  type: `TerritoryOwner`, `SettlementsBeingBuilt`, `OurVassals`, `OurHoldings`, `Title`, `Ties` into LedgerToday).
  Faction Territories (ownership cache delegate, `SettlementConstruction`, `FactionTerritories_VassalOutpost`; hooks on
  TryCreateForFaction, TryComplete, IsDefeated, Invasions ResolveWithWinner, ExecuteVassalisation(AtTile); vassal
  fealty a view), Factional War (namespace `SR.ModRimWorld.FactionalWar`; `IncidentWorkerFactionWar.TryExecuteWorker`,
  `TriggerFactionAssaultVictory.ActivateOn`; `HistoryBook.SetRole`), Vanilla Outposts (`Outposts.Outpost`, view),
  the Empire (`PersonView.royal`), Diplomatic Marriage+ (read-only, `PermanentAlliance`; `IsValid()` deliberately
  not called). History kinds `world:founded/built/razed/taken/vassal/battle`, milestones when they touch the colony,
  everyday within the region map's reach, nothing far away. `Lean/Ages.cs` and `AgesMath.cs`: `Ages.Ours`,
  `Of(Map)`, `Of(Faction)`, `IsEmpire`, `Tag`; `Flavor.AddEraTags` uses it (identical tags, tested); `Ages.Form`
  (`AgeForm` word/rite) from `Rimfolk_Ages.xml` (10 features x 4 ages); word by our age, rite by theirs; no Empire
  rites yet (they use the Empire's titles). The self-check lists 8 new patches (skipped without the mods).
- 0.66.1, stage 0 step 4 (F4): `World/Ledger/History.cs` and `HistoryMath.cs` (`HistoryEntry`, not HistoryEvent:
  Ideology's `Verse.HistoryEvent` is in use), kinds as string keys with families (`world:`, `deed:`, `tie:`), actors with
  roles, places (colony, town, tile: `Ref` gained a tile kind `w801`), indexes, queries `Of`/`At`/`Latest`/`Count`/`Text`;
  the views carry `recent` (12). Dual-write: `AddChronicle` as before plus `History.FromChronicle`; an old save's
  chronicle copied in once. Writers: the chronicle, world situations, `FactionMemory.Note` (a day's repeats merge),
  ties. Folding daily (`Pass.DailyHistory`, tick 1489): everyday entries past 60 days into a summary per kind, actor
  and season; bound 800, newest 200 kept; about +234 KB at five years (2,978 KB). `WorldSituation.At(town, orName)`
  sets a place. Not yet writing through: Heard lately, talk news, situation records, calendar days; deeds have no
  place or doer. If later features need old years' counts, add a yearly fold or raise the bound.
- 0.66.0, stage 0 step 6 (F6): `UI/Tell.cs` and `UI/TellMath.cs` (`TellBook`, `TellTests`): `Tell.Message`, `Minor`
  (News.Post), `Letter`, `Ask`/`CanAsk` (LetterGap), caps 20 messages and 6 letters a day per kind and colony (two
  settings), 4-hour merge, overflow to the evening letter (held news saved with a `held` flag), the settlement named
  with more than one colony. 133 direct messages moved, 103 kept (click answers; others carry `// direct:`; TellTests
  checks), 61 letters through Tell.Letter, 9 through Ask. New code tells the player through Tell (DESIGN.md "Telling
  the player", the agents' brief). "Important only" now quiets the moved Notable messages.
- 0.65.3, stage 0 step 3a (F3a): `World/Ledger/` (`LedgerMath.cs` and `TieMath.cs` pure; `Tie.cs`, `WorldLedger.cs`,
  `LedgerToday.cs`, `LedgerViews.cs`), `LedgerTests`. `Ref` (p/t/f/c/h text form; ledger persons negative), `Refs`
  (game objects both ways), `WorldAge`, `Tie` (11 kinds, 5 states, `TieTerms` with `about`, `TieRules` state machine and
  bounds: 3 per pair and kind, 60 per kind, 300 regard, 900 in all, closed dropped after 60 days), `WorldLedger`
  (saved ties; `AddTie`, `Set`, `TiesOf`, `TiesBetween`, `Town`, `People`, `Person`; upkeep when asked, no pass).
  Views of today's ties: favours (per people), words (promises), the silver debt memo, grudges, guest kin (as Ward),
  granted (Fealty, from the deed), vendettas. Decisions for 3b: a town's mood is its people's stance, the head's regard
  when the head is known; grudges merge into one tie with a count; guest kin gets a kind of its own (Claim: a family's
  claim on someone) rather than Ward; granted keeps its deed (Tone and Baseline read it) and gains a Fealty tie;
  vendettas stay a view. For F4: world situations need a place ref (today only faction, enemy and a place name).
  Nothing is per colony yet (step 7).
- 0.65.2 (A1 and H1, under way before the priority was set): `Integrations/BeforeWork.cs` (was DrinkBeforeWork.cs)
  with `BeforeWork.Rows` (drink and smoke; setting `smokeBeforeWork`, pool `Rimfolk_Pool_SmokeBeforeWork`,
  `SmokeBeforeWorkTests`). The drink never happened before: 1.6 gives drug harm as a curve (its first point, 0.45 for
  alcohol, is now read), and the warm stage's 1.5x fights is now allowed (tipsy's 2.5x still refused); no row on top of
  another's high. Ailments `Council_Ail_MalariaAnemia` and `Council_Ail_DrowsySpells` (after malaria and sleeping
  sickness), hint `Council_Mood_RF_AilAfterFever`. `OutsiderFacts.Hospital` (`colony:hospital`,
  `colony:hospital:renowned`, `{HOSPITAL}`, `{DOCTOR}`, `colony:doctor`, `us:doctor`), 8 pieces in
  `Rimfolk_OutsiderTalks_Hospital.xml`. The game's 1.6 data was read from a public mirror
  (Adelphea/GuildForge_Mobile) and its code from 32bitx64bit/RW-Decompiled-1.6.
- 0.65.1, stage 0 step 2 (F2): `Lean/PassClockMath.cs` (the `Pass` table of 52 passes with their Sched slots,
  windows rolled by game ticks) and `Lean/PassClock.cs` (`Time(Pass.X)` scope, no allocation, nothing saved), every
  Sched pass wrapped after its own gates, the dev window `UI/ClockWindow.cs` (debug action next to "check myself"),
  `PassClockTests`, LeanTests reading Sched's constants by reflection (a new slot needs a pass). The shared dispatcher
  was left (ROADMAP, Lean): no real saving, and order within a tick would change. A new periodic pass needs a `Pass`
  entry and a wrap.
- 0.65.0, stage 0 step 1 (F1): `Lean/Homes.cs` and `Lean/HomesMath.cs` (All, Seat, SetSeat, Of, People, Name,
  KeyOf/MapOf/IsHome, Changed, Busiest; per-pawn ties set after a day on a home; `Homes.Hourly` in the 1544 pass),
  `MapFacts.BusiestHome => Homes.Seat`, `HappeningTracker.timers` (one event timer per home; the old `nextTick` is the
  seat's), `Memo.home` (follow-ups go back to their colony via `HappeningTracker.MapFor`), outsider talks on every
  home, raid journals per raided home, `MeetingAttendance.HomeIs` by `Homes.Of`; `HomesTests`. Left for F7: memo
  pick-ups by colony, an unanswered letter holding back every home, `Ambitions.MeetingMap`. Note: the build stamps the
  git commit into the DLL, so rebuilding after a commit shows the DLL changed with no code change.
- Wave plan: ROADMAP.md, "The step-by-step plan". Wave 1 beside F1: A1 (smoke before work) with H1 (health) in one
  worktree, D1 (defense) in another.
- Stage 0 started (4 November 2026): `docs/design/foundation.md` (Homes, the world ledger, one history, one facts
  service, one voice, budgets, many homes, adapters, ages, the coupling rule; nine steps, each a release that plays as
  before), `docs/design/world-stores-inventory.md`, `docs/design/multicolony-audit.md`. Next: step 1, `Lean/Homes.cs`
  and the busiest-home root cause. The player plans a bigger plan once limits keep being hit; the content-heavy stages
  come then.
- The plan from here (player-approved ideas, 4 November 2026): ROADMAP.md, "The world and its people": stage 0 the
  foundation (world ledger, one history, shared facts, one voice, budgets, many homes, adapters, coupling rule; the
  harm review alongside), stage 1 the region comes alive, stage 2 the Crusader Kings diplomacy, stage 3 many colonies
  on one world. Rimfolk owns people and stories; Faction Territories and Vassalage (territories, settlement building,
  vassals, invasions) and [SR] Factional War (battles between peoples on our map) keep their mechanics. Their files:
  Factional War ships its source; Faction Territories was read decompiled (`FactionTerritories.Expansion.Utility`
  `TryCreateForFaction`/`TryComplete`, settings `enableSettlementConstruction`, `factionExpansionIntervalTicks`
  1,800,000 per people).
- 0.64.4, the player's request (Faction Territories' settlement messages too many): `Integrations/TerritoryNews.cs`
  counts the depth inside the two methods and sends their `Messages.Message` beyond `regionMapRadius` of a home to
  `News.Post` (World, Flavor); setting `territoryNewsQuiet`.
- 0.64.3, the player's request (harvest before a frost by default): setting `frostHarvestAuto` (on), `FieldWeather.Tell`
  applies an offered frost step through `SuggestTracker.Apply(s, quiet: true)` unless `UndoneLast` (the player undid it).
- 0.64.2, the player's report (the guest tab still off screen, with RimHUD's taller inspect pane):
  `GuestCheck.MaxTabHeight` reads the tab's own `InspectTabBase.PaneTopY` and leaves the 30 of the tab buttons; under
  `MinScroll` (100) of room the checklist opens from a "What it takes" button in `GuestCheck.Window_GuestCheck`.
- 0.64.1, the player's report (a journey's caravan "out of food" with full packs: their Lavish policy allowed none of
  the survival meals, insect jelly or agave) and requests (a reminder; one of their food policies set automatically
  on travel; addicts bring their drugs): `Journeys/RoadFood.cs` (`JourneyBook.roadPolicy` and `roadFood`, an hourly
  look inside the journeys' pass: caravans' colonists onto it, home again back to their own, a policy changed on the
  road is kept; the caravan button `Patch_RoadFoodButton`; `Alert_RoadFood`; the planner's row and notes).
  `JourneyPack.Drugs` packs for addictions (`Need_Chemical` fall a day over the drug's `needLevelOffset`) and
  dependency genes (`DependencyDays` read from the hediff's stages, five days when unreadable); a "drugs" group.
  Not tried in the game: the caravan button, the alert, the doses for go-juice dependency.
- 0.64.0: the second library pass, 406 pieces (591 in all) in eleven new files in `Defs/OutsiderTalkDefs/` (events,
  peoples, Empire, homelands, trade, town life, the colony seen, acquaintance, children, memories, tones). New facts
  in `OutsiderFacts.cs`: `their:rough`, `their:ally`, `them:outranks`/`us:outranks`, `colony:noble`, `colony:quiet`,
  `quest:ongoing` (callbacks `then:success`/`then:failed`), `world:peace`, `ended:bad`, `ended:ws:`. Old pieces made
  true (gold fever, comet, ship, toll, a death, mourning). Still thin: mods' peoples and xenotypes (no `people:` tag),
  Empire permits and ceremonies, quest kinds, letters, the colony's buildings and decrees, traits by kind, Flood and
  Unnatural Heat conditions.
- 0.63.0, the player's requests (no spamming, talks on their own, bubbles not pop-ups, a history; 5 to 6 lines;
  reacting to the live world): `Outsiders/OutsiderTalk.cs` (patience, auto-talks at `Sched.RareOutsiderTalk` 83,
  scenes), `OutsiderFacts.cs` (live facts and placeholders), `OutsiderTalkMath.cs`, `OutsiderTalkDef.cs`,
  `OutsiderHeard.cs` (each town's Heard lately), 185 pieces in `Defs/OutsiderTalkDefs/` (format in the header of
  `Rimfolk_OutsiderTalks_Greetings.xml`; `OutsiderTalkTests` checks tags, placeholders, sayability, marks). Settings
  `outsiderChatter` and its frequency. Wars from the game's faction relations, daily; [SR] Factional War only as two
  hostile peoples fighting on our land. Needs a look in the game: six-line bubbles at speed, how often talks start,
  the menu's patience words, Heard lately's height, `town:ill`, a new head caught, quest names spoken raw. Next: the
  second library pass (thin: spacer/hightech voices, quests, Empire titles, world:war, more condition kinds, seasons
  by homeland, children, letters, nobles, decrees).
- 0.62.0, the player's request (how long the food lasts, choose any item, review the list): the journey planner's
  packing list. Food and feed days pinned at the foot (`JourneyMath`, `JourneyPack.PeoplePerDay`/`FeedNeeds`), the
  picker `UI/JourneyPick.cs` over `JourneyPack.Holdings`, grouped lines with subtotals, typed counts, lines taken off
  (`JourneyLine.removed`) or added (`added`), kept in order; feed and "other" lines load with the caravan's goods
  (`JourneyPack.OwnPack`). Not tried in the game yet: the picker's place, the text boxes, the reload icon, mod ammo and
  tents in its tabs, a departure with an "other" line.
- 0.61.1, the player's report (every colonist "Trapped"): RimWorld of Magic's ShouldHaveNeed prefix (priority 100)
  switches off its unfinished travel need (TM_Travel) for everyone, takes mana and stamina from the undead and keeps
  golems' needs. Harmony skips it when an earlier prefix on that check has answered, so colonists had the need at 0
  (their save) and the golem lost its energy. `Integrations/MagicNeeds.cs`: a Priority.Last postfix applies the
  mod's rules again; `GameComponent_MagicNeeds` takes the need away on the first tick after a load and daily
  (`Sched.DailyMagicNeeds` 1117); setting `magicNeedRules`. Which mod's prefix answers first isn't known (not in
  the uploads).
- 0.61.0, the player's request (prepare for travel, set off at a time, everyone ready together): journeys, an order of
  their own, not a decree (`Journeys/`: `JourneyMath` pure with `JourneyTests`, `Journeys`, `JourneyRun`,
  `JourneyPack`, `JourneyPatches`; planner `UI/JourneyDialog.cs`; from the Decrees page, World page and region map via
  `NeighbourPanel.Sections` and the new `NeighbourPanel.PlaceSections`). Sleep shifted the night before, last hours
  (setting `journeyFreeHours`) free of work: clothes for the forecast, pack, eat, Dubs Bad Hygiene toilet and wash by
  name, gather at the gate; the game's caravan forming at the hour; a not-ready letter (wait, leave without, call off).
  `Sched.RareJourneys` 173. One journey at a time. Not built, to propose: moods for those who love or dislike travel,
  Dubs Bad Hygiene water bottles, several journeys at once, road clothes back to the outfit when home.
- 0.60.1: Talk with's "trading" needs an open trade window (`Outsiders.TradingNow`).
- 0.60.0, right-click interactions with outsiders (`Source/ColonyCouncil/Outsiders/`: provider
  `FloatMenuOptionProvider_RimfolkOutsiders` "Talk with X...", `JobDriver_OutsiderTalk`/`OutsiderMeal`, pure
  `OutsiderMath` with `OutsiderTests`, component `OutsiderBook`; chat, ask about home, meal/drink, gift, good word,
  carry a letter home (`NeighbourLetter.bearer`), teach, bring next time, invite to stay, befriend, court, faith,
  spar, insult, duel (tribes, medieval, empire nobility); patches keep a bout's blows from counting as an attack;
  settings `outsiderTalk`, `outsiderSpar`, `outsiderProvoke`, `outsiderDuels`; `Sched.RareOutsiders` 241). To check
  in game: the option on every kind of outsider, outsiders staying put, the shared meal, a duel and a spar end to end.
- 0.59.1: Hospitality's guest tab capped at the screen; Rimfolk's checklist scrolls (`GuestCheck.MaxTabHeight`).
- 0.59.0, the diplomacy layer's foundation (the player's Crusader Kings request): notables (`World/Notables.cs`,
  `NotableMath.cs`, component `NotableBook`; nearest 4 towns within 40 tiles, head/heir/envoy/trader, faces met who
  went home; world pawns, at most 2 made an hour; opinions of the colony and single colonists, 8 memory lines; age and
  succession) and letters (`World/NeighbourLetters.cs`, `NeighbourJobs.cs`: purposes greetings, condolences, warning,
  news, invitation, help; writer at a table, courier trips with `CourierTrip.mailId`; answers by messenger; comms
  console calls for industrial+ peoples with aides and callbacks; harm: a waylaid messenger 4-15%, a threat at war
  that comes true). Settings `notables`, `neighbourLetters`; Almanac page `Council_Alm_C_Neighbours`; `Sched.Neighbours`
  64, `RareNeighbours` 211. Registers in `NeighbourPanel.Sections` for the region map. "A guide" as help was left out
  (no way for it to play out yet): propose. Next: right-click interactions (task list), then favors, pacts...
- 0.58.0, the player's request (a world map in a window, no screen switch): the region map (`UI/Window_RegionMap.cs`,
  `World/RegionMap.cs`, pure `World/RegionMapMath.cs` with `RegionMapTests`; setting `regionMapRadius`, key
  `Council_Key_RegionMap`, button on the World page). Tiles projected flat at the home tile, drawn once into a
  texture; settlement panel calls `World/NeighbourPanel.Sections` (notables and letters register there). To check in
  game: orientation vs the world view, first-draw time at reach 60, wheel over header/panel, window layering.
- 0.57.1, the player's frozen silk golem (RimWorld of Magic 1.6 DLL decompiled from the player's upload): the pawn had
  no TM_GolemEnergy need, CompGolem.CompTick reads `Energy.CurLevel` unguarded, throws every tick (its comp age stuck at
  1), so the pawn never moved. `Integrations/MagicGolems.cs`: ShouldHaveNeed postfix (Priority.Last) keeps the needs
  TM_Golem.needs lists; a CompTick prefix re-adds a lost energy need (AddOrRemoveNeedsAsAppropriate, else AddNeed) and
  clears `initialized` so ApplyNeeds refills it from the workstation; skips the tick if it can't. Setting
  `magicGolemRepair`. Why the need went missing is unknown (another mod's needs rule is the likeliest).
- 0.57.0, bonds rework, the player's request ("everyone just becomes best friends"; no forced balance, battles keep paying
  off): `Social/BondsMath.cs` (pure, `BondsTests`; Tools/Balance `--social` plays two years of a colony of ten with the
  old and new rules), `Social/Bonds.cs` (`PairBook` saved on the social web as `bonds`, one line per pair: familiarity,
  recent hours, fights side by side, seats lost each way, an awkward spell; `Patch_BondsMemory` TryGainMemory postfix
  reads the game's Insulted/Slighted, HadAngeringFight, HarmedMe, BotchedMySurgery, CheatedOnMe, rebuffs, rejected
  proposals, breakups; `Patch_BondsCouple` on AddDirectRelation), `Social/FightSides.cs` (the 250-tick fight looks, now
  shared by the talk's memories and comradeship, with rescue/tend/guard deeds). Familiarity from hours side by side
  (`SocialLog.Near`, meals and recreation count more), talks and shared memories; friendship needs it
  (`Bonds.FriendsForm`, `FriendCap`). How two get on (`Bonds.Affinity`: game compatibility, faith, views, shared
  passions, kind/abrasive) caps friendships and warm words (`WordsCap` in `Chemistry.Notify_Given`). Comrades only from
  fighting near each other, fading after ten quiet days to `ComradeFloor(battles)`. Grudges keep `SocialTie.cause` and
  `renewedTick` (hold six days, then fade). Seats: everyone who stood (self-vote in the instant vote, entries on the
  map) gets `Council_LostElection` by want (stages 0 felt, 1 mild, 2 bitter), the incumbent `Council_LostOffice`
  (stage 1 bitter), a grudge that grows per loss to the same winner, an abrasive winner's gloat face to face. New
  thoughts `Council_SavedMe`, `Council_GratefulTo`, `Council_JealousOf`. Settings `bondsTakeTime`, `chemistryLimits`,
  `comradesByDanger`, `deedsRemembered`, `seatFeelings`, `loveFeelings`. Old saves: familiarity seeded from days
  together, comrade battles from strength (`SeedBattles`), so ties settle over weeks rather than jump. Bug fixed:
  `QuirkMoments.Fought` always got -1 as the fight's start (read after `ResetCount`).
- 0.56.4: the try hour is any hour alike (no lean); `CampKindDef.arrives` (from~to, across midnight) makes a kind
  likelier at its hours (`CampStoryMath.ArrivalFit`: x3 inside, x0.25 outside) in `Prepare`; returning camps out of
  their hours mostly wait. 0.56.3, the player's wish: camps at any hour. `CampTimer.NextTry` picks each day's try hour by `HourWeight`
  (evening 16-21 most, night often, early morning least; `CampTracker.nextTryTick`, `tryHours` saved; 0.56.4 dropped `HourWeight`); passing reasons
  retry hourly up to `Retries` (23). `rolledDay` replaced.
- 0.56.2, the player's report (never a camp by day 107): CampTimer rolled only at 10, and LetterGap (a question in
  the last 12 hours) shut most mornings (openMornings 1 -> 4 between day 93 and 107). Now the day's one choice
  (`CampTracker.rolledDay`) is made in the first open hour 8..17; passing reasons (threat, weather, hazard, waiting,
  lettergap) retry later that day. `CampTracker.diary`/`diaryCounts`/`diarySince` (saved) log each day's outcome code
  for reviews; World page line (`DiaryLine`) and SelfCheck show it. Read the diary in the player's next save.
- 0.56.1, the player's report: no "lost my seat" for someone voted onto another seat in the same election.
  `Offices.Settle` only records losses (`SeatingRound.lost`/`beaten`); `Finish` tells them once every seat is filled:
  another seat means `Council_MovedSeat` (-2, only if they stood to keep it), `Council_MovedDown` (-3/-4 by
  `Offices.SeatRank`: speaker 2, founding seats 1, the rest 0; `ElectionMath.Move`), or `Council_OtherSeat` (-1/-2)
  for proud losers. `GiveThought` takes an optional stage.
- 0.56.0, the player's request (Smart Farming sowed, cut early and sowed again): fields by the weather
  (`Fields/FieldWeather*.cs`, tested in `FieldWeatherTests`; settings `fieldsByWeather`, `frostHarvestSuggest`; hourly
  slot `Sched.Fields`). Per-zone switch pauses sowing (Prefix on `WorkGiver_GrowerSow.JobOnCell`, restricts only) when a
  crop sown now brings under 60% of a full harvest by an hourly forecast (`OutdoorTemperatureAt` + active conditions +
  Rimfolk's weather push); frost harvest is a suggestion (source `fields`) naming the plants each plant's own cold
  limit would lose (`Plant.LeaflessTemperatureThresh` by reflection; 1.6: min growth temperature −18 to −10). The
  player keeps ReGrowth 2's built-in Smart Farming copy and drops the standalone. To check in game: forecast vs map
  temperature (dev log line), the desert save's winter outlook, the frost card, undo and the check after the frost.
- 0.55.3/0.55.4, the player's request: the health panel shows chronic conditions that can kill or cost a part
  (carcinoma: no lethalSeverity, it has a growth comp and destroys its part) as stable, shrinking or getting worse
  (`HudHealthMath.Shows`, `Grave`, `Lasting`); other chronic conditions stay off.
- 0.55.0, the player's request (colonists stuck at full mood): used to the good life (`Moods/GoodLife.cs`,
  `GoodLifeMath.cs`; since 0.55.1 each good mood group is felt at a share in `Patch_ComfortStandards`, with a note in
  its tooltip and the Mood tab readout, no bad mood of its own: the player found a big flat "-56" line unconvincing;
  since 0.55.2 spread by how ordinary each kind is (`GoodLifeMath.Weight`/`Spread`: lasting moods and memories had
  often lately lose most, new ones least, life goals none; recent counts kept per colonist in memory, not saved;
  since 0.55.3 also by the memory's age, `AgeWeight`, so a wedding lands nearly in full and settles over six days,
  and standards' comfort cut eases the same way, `ComfortFor`); line 45/40/35/30 by standards level, half of the
  first 20 points over it and three quarters of the rest taken back, fading to nothing at the minor-break line; reads the game's
  `SituationalThoughtHandler.cachedThoughts`, present in 1.6 and 1.7) and quiet troubles (`Setbacks/Troubles.cs`,
  Life's ups and downs, from the third content day). Settings `usedToGoodLife` and `quietTroubles`; tested in
  `GoodLifeTests`; `Tools/Balance` models both. To check in game: the "Why" sum against the Needs tab, the drift
  down from 100% on the player's save, the nightmare message for a bad night.
- 0.54.2, the player's request: with Alpha Skills, a drink before work for the drunken passion
  (`Integrations/DrinkBeforeWork.cs`, `DrinkBeforeWorkMath.cs`, `DrinkBeforeWorkTests`; setting `drinkBeforeWork`;
  pool `Rimfolk_Pool_DrinkBeforeWork`). The other conditional passions are in `docs/alphaskills-proposals.md` (P1 to
  P17), nothing built: the player picks.
- 0.54.1, the player's request: answers show hints in words, never numbers, under the answer and on hover
  (`Dilemmas/ImpactHints.cs`, tested in `ImpactHintTests`; setting `impactShows` = Hints (default) / Exact / Off,
  replacing `showImpact`; `DilemmaRecord.impactHint`).
- 0.54.0, the player's request: elections play out (`Offices/Election.cs` saved as `OfficeTracker.election`,
  `ElectionMath.cs` tested in `ElectionTests`, `ElectionPlanks.cs` with `<planks>` on the offices, `OfficesElection.cs`,
  `ElectionGathering.cs` with `LordJob_Election`, `ElectionLetter.cs`, `UI/ElectionPanel.cs` on the Decrees page;
  `Score` = `Leaning` + luck, `CampaignLean`; ranked ballots; setting `electionEvent`). 0.53.5: outdoor gatherings use
  `PlayOut/PlayYard.cs` + `YardMath.cs` (tested) through `PlaySpots.OpenCell`. 0.53.6: `Immersion/MoodBubbleText.cs`
  names the mood beside the game's mood bubble (setting `moodBubbleText`).

- 0.53.1: every source of words said over a head is in `ImmersionTests` (pools collected from all Defs folders, def
  `<lines>`, keyed fallbacks); 336 lines made one sentence. Open, the player's call: the wishes', ambitions' and
  decrees' own lines (about 3,600 of 6,300 are two sentences; they are also letter quotes; a bubble shows whole
  sentences up to 90 characters). Social-log narration is third person and left out of the rule.
- 0.53.2: ailment balance (`Tools/Balance`, 800 runs a setup): well run 9.3%, average 13.9%, neglectful 30.5% of the
  time with an everyday ailment (was 11.7 / 17.2 / 36.6); catching clock `AilmentTracker.EverydayCatchingDays` 35 to
  50 days, mild ailments shorter, bed rest and tending count for more. `docs/balance-report.md` regenerated.
- The player's answers (2 November): letter texts and quotes may have two or more sentences, and they don't want
  everything short (the one-sentence rule is for words said over a head only; the wishes', ambitions' and decrees'
  own lines stay as they are); of the Alpha Genes proposals only P3 (mood hints) and P5 (ailment risks from genes)
  are wanted (both shipped in 0.53.4; `Tools/Balance` doesn't model risk lines); an evening with a single piece of news stays a message (not a letter).
- (Answered, kept for the record) Open questions to the player: the six Alpha Genes proposals P1 to P6 (`docs/alphagenes-proposals.md`; the fixes F1
  to F7 shipped in 0.53.3: `PlayOut/PlaySafety.cs` in PlaySpots.Ok and PlayOuts.CanJoin, a "fasting" tag from the real
  hunger rate, natural armour, the `LikeThought` and `Heard` extensions, comfort offsets per body, no forecast with a
  weather maker; tested in `BodyTests`); whether an evening with only one piece of news should also come as a letter
  (`News.Send` shows a single item as a message, as before 0.44.1).

- 0.53.0, plan ahead phase 3 (details in `docs/plan-ahead.md` "Phase 3 as built"): `Integrations/ColonyManagerBridge.cs`
  (reflection on FluffyManager, not yet run against the real mod), `Building/AheadMore.cs` + `AheadMoreMath.cs` (tested,
  `AheadMoreTests`), `UI/AheadMorePanel.cs`; spare clothes bills; `AheadField.feedOnly`; quest joiners in beds and seats;
  `BuildAhead.KeptGround`; busy benches by recorded hours; mining in a room's hours; Building ahead shown with next
  steps off. Not done: livestock and foraging targets only shown; components for breakdowns not counted.

- 0.52.1, the player's idea: `Integrations/CoveredLooks.cs` patches VEF's `HediffComp_ThoughtEffecter.CompPostTickInterval`
  (by name) so Alpha Genes' AG_Disgust (repulsive gene) and AG_Awe (angelic beauty) auras stay still while the carrier
  wears a FullHead piece plus torso and legs coverage; setting `coveredLooks`; mood hint `Rimfolk_MoodHints_AlphaGenes.xml`.
  The aura reaches everyone within 6 cells through walls (GenRadial, no sight check). Alpha Genes' files were unpacked
  in the session scratchpad; a review of its other genes for Rimfolk interactions goes to `docs/alphagenes-proposals.md`.

- New content from `docs/content-proposals-A.md` and `-B.md` (parallel builders), 0.49.0 to 0.50.1: situations and
  events (`World/Workers_NewSituations.cs`, `Happenings/Workers_NewEvents.cs`, `Strangers/StrangerEffectsNew.cs`);
  ailments (`Rimfolk_Hediffs_Ailments_New.xml`, triggers in `AilmentChecks.Sample`/`AilmentTracker.Notify_Ate`, the
  commonest trimmed to keep the time ill down; the hourly cause mask is full at 32 bits; `Immersion/FirstPot.cs`);
  DLC visitors and regulars (`Visitors/VisitorsMore2.cs`, `VisitorWork2Math.cs`, `GravshipLanding.cs`,
  `Happenings/Workers_Visitors2.cs`); camps (`Camps/CampKindsLand.cs`, `CampLandMath.cs`, `World/Workers_Run.cs`);
  wishes and life moments (`Wishes/WishWorkers_LifeNew.cs`, `PlayOut/PlayPetGrave.cs`, `Lives/LifeNew.cs`,
  `ArcDef.again`). Each builder's "needs a look in the game" list is in its round's commit and the reports; dropped:
  kin taking a colonist away, the spacers' liftoff. 0.50.2: callers (`Visitors/Callers.cs` saved GameComponent `Callers`, `CallerJobs.cs`,
  `CallerMath.cs`, `Happenings/Workers_Callers.cs`). 0.52.0: list B dilemmas and customs (`Rimfolk_Dilemmas_New.xml`, `Rimfolk_Dilemmas_Customs_New.xml`,
  `Dilemmas/DilemmaWorkers_Variety.cs`, `Dilemmas/AnswerShifts.cs` saved component, route walks in
  `PlayOut/PlayRoutes.cs`) and the second balance pass (0 left in it; 58 first-pass dilemmas still flagged). Still
  building: plan ahead phase 3. 0.51.0: the same rules for the existing advisor/base/plan/making/automations/defense (`Building/Suggest/SameRules.cs`;
  keys `adv:`, `base:`, `def:`, `plan:research`, `line:<id>:`, `auto:`; `RoomGrowth.Wanting` reads building ahead's
  rows; `BaseReviewMath.WalkGrid`; `MakingMath.StapleCount`); details in docs/plan-ahead.md "The same rules as built".

- 0.48.0, plan ahead phase 2 (details in `docs/plan-ahead.md` "Phase 2 as built"): `Building/GrowthMath.cs` (pure,
  `GrowthTests`), `GrowthNeeds.cs`, `BuildAhead.cs` (room steps placed through `TemplatePlanner.Execute` at a
  `FindSpots` spot with `extraNear`, `UndoRoomPlaced`), `BuildProjects.cs` (`BuildAheadTracker` as `comp.buildAhead`,
  stages, `Why`), `UI/BuildAheadPanel.cs` at the top of the Plan page, `BuildAheadOverlay` (kept strips while placing).
  Open: the advisor's own "outgrown" signals can repeat these steps (route them through the core in the "same
  rules" pass); busy benches read queued bills, not bench hours; quest joiners aren't read; the ground plan doesn't
  avoid kept strips. Next: the same rules for the existing advisor/base/plan/defense/automations, then phase 3.

- 0.47.0, dilemmas (a parallel agent): `DilemmaEffect.Describe(DilemmaContext, ImpactSheet)` on all effect classes
  (a new effect must override it), `Dilemmas/ImpactMath.cs` (tested, `ImpactTests`), `DiaOption_Impact` +
  `Patch_DiaOptionImpact` (the short line under each answer, the full list on hover), `DilemmaRecord.impact`, setting
  `showImpact`; `Tools/dilemma_balance.py` and `docs/dilemma-balance.md` (points per scale; dominated/easy verdicts;
  `--defs DIR`); rebalanced numbers in the general dilemma files; new `Defs/DilemmaDefs/Rimfolk_Dilemmas_Choices.xml`.
  Second pass still to do: 37 flagged dilemmas in Strangers 13, Seasons 7, Customs 5, Customs2 4, Dilemmas3 5,
  Camps 2, Health 1 (listed in docs/dilemma-balance.md).

- The player approved building the content-variety proposals (new events, illnesses, dilemmas, visitors, camps,
  customs from `docs/content-proposals-A.md` and `-B.md`) without showing them a list first.

- 0.46.0, plan ahead phase 1 (a parallel agent; details in `docs/plan-ahead.md` "Phase 1 as built" and "The shared
  suggestion core"): `Building/AheadMath.cs` (pure, `AheadTests`), `Building/ColonyNeeds.cs` (`ColonyNeeds.For(map)`,
  hourly; fridges by a comp named like "Refrigerator" count as cold storage, for RimFridge), `Building/Suggest/`
  (`Suggestion`, `SuggestTracker` as `comp.suggest`: Not this, log with undo, checks), `Building/Ahead.cs`,
  `AheadStuck.cs`, `Integrations/BillMods.cs` (Everybody Gets One bills read only; Better Workbench Management linked
  bills change together), `UI/AheadPanel.cs` at the top of the Making page, settings `aheadMealDays`/`aheadReserveDays`.
  Not yet: spare clothes bills; hay counts in the food crop pool. Next: phase 2 (construction and expansion), then
  the same rules for the existing advisor, base, plan, defense and automations, then phase 3.

- Content rounds 0.45.0 to 0.45.4 (five parallel agents, text variety: wishes and nuisances, customs with
  `Dilemmas/CustomYearsMath.cs` tags [again]/[years]/[tradition]/[missed] and `Flavor.PickFresh` for openings and
  outcomes, ailments with `AilmentTexts.Pick` and two new ailments (sore elbow, chapped hands), visitors and camps,
  world situations and storyteller events). Known mismatches left for the next batch (words vs map):
  `Council_Evt_StrayBeast` says the branded beast is in the barn, but no animal exists unless "keep it" spawns one
  (fixed in 0.47.1: `HappeningSetting.stray`, spawned in `HappeningWorker_Setting.Begin`, held by `LordJob_StrayBeast`
  and the `Rimfolk_StrayHold` duty, fetched home through `Couriers.Fetch` by `DilemmaEffect_StrayHome`, kept by
  `DilemmaEffect_KeepStray`); `SituationOption` now has `outcomes`/`riskOutcomes` (picked by `Dilemmas/OutcomeMath.cs`). The player then clarified that variety means
  NEW events, illnesses, dilemmas, visitors, camps and customs, not only text: proposals are being gathered in
  `docs/content-proposals-A.md` and `-B.md`, to be shown to the player as a list before building.
- Other spoken pools may be said over heads without being in `ImmersionTests.SpokenPools` (Rimfolk_VisitorLines was;
  21 two-sentence lines were rewritten and the pool added). Worth an audit of every pool passed to Speech.Say.

- The player approved all eighteen items of the to-do list ("like all your suggestions"), then asked for (a) deep
  automation so a complex late game can be "outsourced" (rooms, production chains, base layout, expansion), and (b) a
  content variety, immersion and flavour update (more content, deeper existing mechanics, not new ones), in that order.
  Research for (a) was under way: what Rimfolk already automates, and late-game pain points against the player's own
  management mods (Colony Manager Fork, Better Workbench Management, Allow Tool, Pick Up And Haul, Haul to Stack,
  Smart Medicine, Nice Bill Tab, Deep Storage; the full list is in the save's header). For (b), the player's save
  shows the most repeated things: the shoddy clothes complaint (11), the armor wish (11), the comfortable temperature
  wish (10), the tradition dance thought (9), the own room wish (8), the harvest dance (7); midwinter, midsummer and
  the spoils have two opening texts each, and wishes have one line per temperament.
- 0.44.1: `PlayCook.LetGo` (a visiting cook off the feast's bill ends the bill or stove-refuel job and drops a carried
  ingredient or fuel; a carried thing stays in hand when a job ends). `Weather/RainWash.cs` (hourly from WeatherBook:
  filth with `rainWashes` thins outdoors with chance min(0.25, 0.15 x RainRate) from rain 0.2; setting `rainWashesMore`).
  Colony tools (a parallel agent): `Building/UseLog.cs` (pure, tested) and `Building/ColonyUse.cs` (`use` on the game
  component, five read-only hooks: Thing.Ingested, TendUtility.DoTend, Bill_Production.Notify_IterationCompleted,
  CompRefuelable.Refuel(List), Pawn_ApparelTracker.TakeWearoutDamageForDay by name) feed `Chains.cs` per bench
  (`BenchFactor`, `FindHand`, `ChainMath.BenchSpeed/HandHours/DoingBonus`); `AutomationTracker.SettingsLine` under
  each automation switch; `RoomArrangeMath.Tuning` (aisle, keep) from two rows in the rearrange window (not saved);
  the quiet-day news fold (`NewsQuiet`, setting `quietNewsDays`) was reverted in 0.49.3 at the player's request:
  the evening news is always a letter. Worth a look in game: the Making page after a day
  or two, the settings page with a game loaded, and that TakeWearoutDamageForDay doesn't throw on 1.7.
- 0.44.0, defense (two parallel agents): `UI/DefenseLinesPanel.PlanMenu/Plan` ("Plan this step..." with every plan);
  `DefenseSight` counts turrets as targets and watchers (`TurretSees`, `CanHitTurret`, `Reading.turretCover`), and
  `DefenseShapes.BuildBox/DrawBox` move turrets through `DecoyOffsets`/`BigOffsets` out of the way in's sight;
  `DefenseLineFacts.Turrets/TurretSight/OldLines` and `DefenseLineReview.Sight/OldLines` (a null saved shape, or the
  `DefenseLineMath.OldGateLines` geometry, marks a pre-0.14 gate line); `DefenseShapeSpec.SecondRank` (raids of 3000
  points or more, `DefenseTests.SecondRankChecks`); `Defense/DefenseStepWorkers_Threats.cs` (StepWorker_Emp,
  StepWorker_BaitYard, StepWorker_Mortars, filtered by `DefenseStepWorker.OnLadder`; defs in
  `Defs/DefenseStepDefs/Rimfolk_DefenseSteps_Threats.xml` and `Defs/DefenseTemplateDefs/Rimfolk_DefenseTemplates_Threats.xml`;
  beacon role 'B', template kind "bait", placement "core"). Not tried in game: the menu, killbox previews, the bait
  yard beside a trap corridor, the EMP lane's distance from turrets.
- The player's direction for (a), binding: they are early/mid game (the save is day ~96, 9 colonists), overwhelmed by
  balancing many things; they want help with what needs planning ahead (construction, production chains, base
  expansion), not reacting to events. The helper ONLY SUGGESTS; the player does each step with one click ("act by
  itself" to be reconsidered once they've tested that suggestions are consistent and helpful, so build each suggestion
  as a discrete, undoable step with its reason). Every suggestion must come from the live game state (eaters incl.
  guests, stocks, measured use, bench and worker throughput, season and growing days, freezer space, materials,
  builder hours), show its reason in numbers, order by urgency, vanish when its reason is gone, and check whether the
  last step worked; never fixed advice. "Not this" on everything. Research notes and the save's facts are in the
  session scratchpad (automation/inventory-gaps.md); the save: 2 cooked meals for ~17 eaters (stove bills until 2 and
  5), 2390 chickpeas from 35 cells, steel 31 / components 25 with machining bills waiting, nothing under construction.
- 0.44.2, visitors (two parallel agents): `Visitors/VisitorMusic.cs` (StartPlaying/StopPlaying on a built instrument),
  `Visitors/VisitorHunt.cs` + `VisitorActsMath.Fits`/`GamePrey` (wild, nobody's, unmarked, no predator, deer-sized at
  most, the land's own kinds; `Patch_HuntedBeastHolds` on Pawn_MindState.StartManhunterBecauseOfPawnAction;
  JobDriver_VisitDress), `Visitors/Borne.cs` + `Camps/CampDead.cs` (a real body from `Borne.MakeDead`, generated without
  relations and killed off-map; road mourners carry it off, camp mourners lay it by the lamp; colonists near it get the
  game's observed-corpse thought), `Visitors/VisitorBeds.cs` (Hospitality.Building_GuestBed by name,
  ClaimBedIfNonMedical, given back on any ending), hooks in `Visitors/VisitorActs.cs`. Not confirmed without the game:
  that a singer's StartPlaying keeps sounding outside the game's play job; that Hospitality's bed patch lets
  LayDown through for Rimfolk's visitors.
- Visitors trade (a parallel agent, in 0.44.2): `Integrations/RimMarketSale.cs` (RimMarketStall is
  partial; it copies RimMarket's `ColonyStalls.Sell` step by step by name, `SaleReady` false if anything is missing),
  `Visitors/VisitorTradeMath.cs` (tested), `Visitors/VisitorTrade.cs` (`JobGiver_VisitShop`, `JobDriver_MarketBuy`),
  `Camps/CampMarket.cs` (`CampKindDef.buys`), `Camps/CampMilk.cs` (the drovers' "milk" ask, paid on delivery through
  `CampAsk.price`), setting `marketCampersBuy`. 

## State (1 November 2026)

- 0.43.5, a break the player saw: RimMarket 1.0.13 (BenFD) gave `Building_MarketStall.SetOrder` two more parameters
  (`bool refill, int reserve`) and `StallOrder` the fields `refill` and `reserve`, so Rimfolk's reflection lookup for the
  4-parameter SetOrder failed and its sales table fell back to RimMarket's window. `Integrations/RimMarketStall.cs` now
  takes either form (the 6-parameter one first), reads both fields into `Line`, and `SetLine` keeps a line's refill and
  reserve when only its count or quality changes. `RimMarketStall.Keeps` is true with the new form; then
  `UI/Window_StallCounter.cs` draws a taller card with Refill and Keep home. Every other type and member Rimfolk uses
  in RimMarket is unchanged in 1.0.13 (checked in both its 1.6 and 1.7 builds).
- Shipped and pushed: 0.43.5 (0.43.4 before it), on `claude/gifted-noether-32w12b` (this session's branch) and `claude/charming-pasteur-9qbjle`.
  Tree clean. Every check green at that commit: build (only the CS0108 warning in Decrees.cs), DefValidator 0 errors,
  Tools/Tests "All tests passed", labelcheck, almanaccheck, save_estimate --strict, interaction_audit, playout_audit,
  ApiDiff "Nothing the DLL uses has changed".
- 0.41.1 was the player's request: a finished step of a defense line keeps "Plan it again" (the same plan, or another
  of the step's; `DefenseStepWorker.Choices` and `DistanceFor`, `DefenseLineReview.PlanAgain`, `DefenseLinesPanel.AgainMenu`).
  The outgrown killbox's own button folded into it.
- 0.41.2, also asked: "My animal fought for me" (`Council_Pet_Defended`) lasts 0.17 days, not 12. A loaded
  `Thought_CouncilMemory` keeps no longer a time than its def's (`Causes.cs` ExposeData), so old copies end too.
- 0.41.3, a bug the player saw: the guest checklist (`Integrations/GuestCheck.cs` Draw) overflowed Hospitality's
  fixed-height listing into a second column out of sight, so `CurHeight` read short and the tab never grew. It now
  draws with `listing.maxOneColumn` set.
- 0.41.4, the player's report (silver rush prospectors lay about in the dining room) and their rule that every visit
  plays out its words: `Visitors/VisitorWork.cs` (JobGiver_VisitRest only when tired or at night, on open ground;
  JobGiver_VisitWander off the colony's rooms and fields; JobGiver_VisitWork + JobDriver_VisitWork for a trip's `work`:
  prospect, glean, sing, hunt; JobDefs and the Rimfolk_VisitWork duty in `Defs/Misc/Rimfolk_Visitors.xml`). The vanilla
  JobGiver_GetRest in a plain priority node lies down whenever not fully rested: that was the "lay around" bug for
  every visitor. VisitEffect_Stay has `work`, `inside`, `room` (a roof whatever the weather, the barn first);
  VisitEffect_Come has `work`; water spots go to water clear of the colony (`Visitors.WaterSide`); gatherings take
  `guests` "visitors" (the world event's visit people, late arrivals join). `Tools/Tests/VisitorWorkTests.cs` checks
  the defs (known work, roof words match `inside`).
- 0.42.0, the player's request: conduits and pipes laid again under the walls. `Building/LineRouteMath.cs` (pure,
  `LineRouteTests`: Steiner growth by nearest-piece Dijkstra over cell costs wall 10 < furniture 30 < ground 50 < floor
  80, a line in sight kept at 80%; then reverse-delete; reachers need an own line within 6 nearer than any other net;
  `Removable` re-checks at take-up time), `Building/LineRoutes.cs` (reads power nets from the game's PowerNet, pipe
  nets by adjacency of BaseReview.PipeKey things; shut cells beside other nets and within reach of their connectors;
  the Base page finding "relay:KIND" and status "relaying:KIND"; `LineRelayTracker` as `relays` on the component,
  hourly from HourlyPeople). Plans are kept six hours, nets with fewer than three lines in sight skipped. Not tried in
  the game here: worth a look at the finding on the player's base, the yellow/white overlay, and one relay to the end.
- 0.43.0, the player's ask that visitors use, make and take items and touch the colony more: `Visitors/VisitorCamp.cs`
  (a camp out on the land lays a vanilla Campfire of the visitors' faction, fed while they stay, put out to ash when
  the trip leaves Here; `JoyGiver_SitWithVisitors` + `JobDriver_SitWithVisitors` for colonists), `VisitorWork.Done`
  (prospectors' finds: silver 35%, gold 4%, into inventory; gleaners: the zone's crop), JobGiver_VisitFire (17-23h),
  JobGiver_VisitSit (a seat near the gate), social modes on the work toils, pool moments find/glean/fire. Ideas not
  built (propose first): mourners carrying their dead, hunters taking real game (a hunted beast can turn on them),
  the singer playing the colony's instrument, guests in Hospitality's guest beds, drovers milking, campers trading.
- 0.43.1, the player's ask to apply items and touchpoints to every NPC kind: PlayGroups.HandOver feeds an animal
  by setting the food down (Feed); CampAnswers.Pay(Camp, n, by) and CampAnswers.Carry carry silver, provisions and cart
  makings over (DilemmaEffect_CarryOut to Camp); Health/OutsiderTend.cs (JobDriver_TendOutsider, DilemmaEffect_TendStranger)
  for camp sick, war wounded, the outside patient; Strangers/EncounterGifts.cs (DilemmaEffect_Carving,
  Council_EncP_RefugeeCarving); ResidentBeast.Steal walks to the stores. Three parallel pieces for 0.44.0 (visitors'
  trades, mission guests' meals and beds, camp help from real sources) were in flight at this point.
- 0.43.2 (built by a parallel agent, merged): `Camps/CampCarry.cs` (a camp's good turns gathered, milked or taken
  from stock and carried to the colony's edge; JobDriver_CampBring; DilemmaEffect_CampBrings for the caravan's stores),
  the blessing walk, mending at the colonist's side, dig and stall errands, mourners' ashes, the dead carried home
  (Council_CampCarryHome). Camp beasts start with milk or wool part grown (`CompHasGatherableBodyResource.fullness`
  by name, present in the 1.6 and 1.7 references).
- 0.43.3 (a parallel agent, merged): `Missions/Hosting.cs` (hosted guests eat stored meals, sleep in counted beds lent
  by ClaimBedIfNonMedical and given back in GiveBack; Council_GuestSleep = JobDriver_GuestSleep, a lie-down that doesn't
  look for other jobs, also used now by PlayStrangers' nights).
- 0.43.4 (a parallel agent, merged): `Happenings/VisitorServices.cs` (a saved GameComponent on Sched.RareTimed: the
  tinker's mending, the doctor's doses, standing still for a worn piece; jobs in `Defs/Misc/Rimfolk_VisitorServices.xml`),
  PlayCook.CookFromOutside (the visiting cook's bill drops meals by the stove), PlayCarry "coat" and one-of-each errands
  ("meals+coat"). Risks to look at in game: a visiting cook whose visit ends mid-bill may carry an ingredient off.
- Ideas not built, for the player (propose first): mourners carrying their dead on a real cart, hunters taking real game,
  the singer playing the colony's instrument, drovers' herds giving milk to buy, campers trading at the colony's market.
- Open with the player: they asked whether rain washes blood and dirt away. The game does it already outdoors (the
  filth def's `rainWashes`, thinned in `SteadyEnvironmentEffects`); nothing was built. Wait for their answer.
- Rounds 0.30.0 to 0.41.0 came before: the player's event rounds (camps, neighbours, visitors, world, animals, Pokémon),
  the five follow-up ideas, two explicit requests (rearranging rooms; production chains and opt-in automations) and
  the year's letter.
- Candidates if the player wants more (propose as a list first): chain rates per bench rather than per recipe, and
  consumption measured from the last days' real use; the automations' preview on the settings page itself; per-room
  "rearrange" tuning knobs (aisle width, keep weight); a quiet-season digest that folds the evening news for a day;
  the template choice on a step that isn't done yet (today "Plan this step" places the step's own pick).

## Where this session's systems live

- Camps and neighbours (0.30.0, 0.31.0, 0.37.0): `Camps/` (`Camp` partial classes: CampStory, CampLife, CampRequests,
  Holding, RoadPost), `CampTracker` (camp, holding, past; `PlayCamp` finds the camp a play-out means), `WarCamp.cs`,
  defs in `Defs/CampDefs/`. The wardens' post as a trading stop: `Camps/RoadPost.cs` (`CampAsk.settledOnly` asks,
  `DilemmaEffect_RoadCarry`, `Camp.owed*`, `PayRoad`, `roadLoads`), word from the road via `CampKindDef.roadWord` and
  the pool Council_Pool_RoadWord. Allies: `World/Allies.cs` (`Allies.Of()` from the history's "alliance" record;
  Council_Evt_AllyFood; the war camp's helper prefers the ally).
- Visitors and the world (0.32.0, 0.33.0): `Happenings/Regulars.cs`, `Visitors/`, `World/Workers_World.cs`
  (other settlers, the road over a year, the crossroads), `World/RivalMath.cs` (tested), `WorldSituationDef.afterFlags`.
- Animals and Pokémon (0.34.0, 0.35.0): `Animals/HerdStory.cs`, `Animals/ResidentBeast.cs`, `Animals/HerdStoryMath.cs`
  (tested), `Integrations/PokeWorldCamp.cs`; the Overview card and the check-myself part (0.36.0) in `UI/OverviewPanel.cs`
  `Land` and `UI/SelfCheck.cs` `Stories`.
- Rearrange a room (0.38.0): `Building/RoomArrangeMath.cs` (greedy placement, light, watch areas, move cost, `Refine`
  for the whole room) and `Building/RoomWalkMath.cs` (pure walking model: Dijkstra from the doors, ways, squeezes, dead
  ends, open stretch), both tested in `Tools/Tests/RoomArrangeTests.cs` (set RIMFOLK_ARRANGE_DEBUG=1 to dump rooms);
  game side `Building/RoomRearranger.cs`, window `Building/Designator_RearrangeRoom.cs` ("How the room walks").
- Production chains (0.39.0, 0.40.0): `Building/Making.cs` (`Compute`: building needs, starved bills, `Result.chains`
  trees flattened leaf first into `needs`), `Building/Chains.cs` (`ChainNode`, rates, `BestMaker`, `Sowing`),
  `Building/ChainMath.cs` and `MakingMath.cs` (tested), page `UI/MakingPanel.cs` (`DrawLink`).
- Automations (0.39.0, 0.40.0): `Building/Automations.cs` (`AutomationTracker` on the game component as `automations`,
  run from `CouncilGameComponent.HourlyPeople`; `Candidates` feeds `Hourly` and `Preview`; `AutoAction` + `Undo`),
  `Building/AutomationMath.cs` (tested), switches in `CouncilSettings` (`auto*`, all off by default) listed in the
  "Automations" section of `UI/SettingsCatalog.cs`, texts in `Languages/English/Keyed/Rimfolk_Automations.xml`.
- The year's letter (0.41.0): `World/YearLetter.cs` from `CouncilDynamics.DailyFirst`, `comp.yearLetterLast`, keyed in
  `Rimfolk_YearLetter.xml`. The annals (`Annals.cs`) and the founding-day event (`HappeningWorker_Founding`) are separate.

## How to work here (learned the hard way)

- A fresh container has no .NET. Ubuntu's packages work (the dotnet-install script was refused):
  `apt-get update && apt-get install -y dotnet-sdk-8.0 dotnet-targeting-pack-10.0` (the second is for ApiDiff). The 1.7
  references come from restoring any throwaway net8.0 project with `<PackageReference Include="Krafs.Rimworld.Ref"
  Version="1.7.4977-alpha.2" ExcludeAssets="all" />`. With an SDK in `$HOME/.dotnet` instead, every command needs
  `export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet`. The working directory resets between commands, so use
  absolute paths or `cd /home/user/Rimworld-Helper && ...` in each command.
- The test suite takes about four minutes: run it in the background writing to a log, append `echo "exit $?"`, and
  poll the log. Read failures before starting another run (a new run overwrites the log).
- Test rules that bite: settings fields one per line (`public bool x = false;`); a thought with `RewardInstead` has
  `baseMoodEffect` 0; feelings must be existing HeartDefs; spoken pool lines are one sentence, 90 characters at most,
  one '!' at most, no surrounding quotes, listed in `ImmersionTests.SpokenPools`; `Sched` rare offsets unique and not
  colliding with hourly offsets mod 250; a new saved collection needs a MODEL line in `Tools/save_estimate.py`
  (`"Path/File.cs:field": ("fixed", N, bytes, "why")`); two-pawn methods need `// apart:` or `// together:` comments
  (interaction_audit); an answer whose words describe a done thing needs a play-out or a line in
  `Tools/playout_skips.txt` (`DefName [option N] why`).
- Releases: a round in Almanac_WhatsNew.xml (2 to 8 points, no em dashes, no exclamation marks), `About.xml` modVersion
  equal to it, `python3 Tools/changelog.py` then `--check`, this file's head updated, then all checks, commit with the
  session's attribution lines, push both branches. No model names anywhere in the repo.
- The game is played on 1.7 but compiled against 1.6: new game API names can be checked with
  `grep -c Name ~/.nuget/packages/krafs.rimworld.ref/1.6.4871/ref/net472/Assembly-CSharp.dll` before use, and ApiDiff
  confirms 1.7 afterwards.

## Older history (reference only)

## Decrees when they're needed, as shipped (0.16.1)

- `Decrees/DecreeMath.cs` (pure, `DecreeMathTests`): `Need` (moment strengths, 1 or 2 each, three's worth is full),
  `Want` (love 1, like 0.75, dislike -0.5, hate -0.75 over everyone, +0.3 for a petition), `Understanding` (need x
  (0.4 + 0.6 want), halved once declared twice in the last month), `CooldownTicks` (up to 65% off, never under a day),
  `BacklashFactor` (up to 60% off).
- `Decrees/Decrees.cs`: `DecreeDef.neededWhen` (Clean sweep and Sick bay: RecentThreat); `DecreeTracker.NeedOf` (cached
  250 ticks; moments from requestWhen, orWhen and neededWhen; strong when bad), `ThreatsWithin` from `threatTicks` (the
  game's LastThreatBigTick noted hourly, 8 kept, 15 days). `ReadyAt` is the effective time (stored readyAt stays end +
  full rest, so it shortens and lengthens with the need); `UnrestChance`, `Backlash` and the decree mood's
  `ActiveDecree.Resentment` take `BacklashFactor(understood)` (saved on the decree as enacted).
- `UI/DecreePanel.cs`: a green "Needed now: ..." line on a card on cooldown or recently used.

## Weather that changes like weather, as shipped (0.16.0)

The player chose all six options of the proposal (spells, what follows what, temperature, events, a forecast, the
year), and asked that every part consider the biome and every combination of mods. Harmony, not a hidden condition, so
nothing is added to the map or the save beyond the forecast and the events' pulls, and taking Rimfolk out leaves the
game's own weather.
- `Weather/WeatherDynamicsMath.cs` (pure, `WeatherDynamicsTests`): `WeatherKind` from a weather's traits; the land
  (`BiomeWeather`: rain a year, coldest and warmest month, the biome's wet share, Seasonal Weather's seasons, open
  water; arid, humid, frozen, tropical, continental); `Factor` = spell x follow-on x events x year x season, clamped
  0.3 to 2.5, special weathers 1, a downpour and any rain in a drought never above 1; `Shared` renormalises over the
  weathers that could come so the ordinary ones keep their chance together (the specials keep theirs exactly);
  `TempTarget`/`Ease` (at most 5 °C down, 3 up), `FogTicks`, `ForecastAccuracy`/`ReadsWeather`, `YearOf` (hashed by
  tile and year), `YearWords`/`YearWordsFor`, the events' pulls (`TryParsePull`, `AddPull`). The tests run 100,000
  random combinations over eight lands, focused checks, and a hundred simulated years per land with and without:
  rain shares stay within 0.75 to 1.33 of the game's (measured within about 10% over 300 years and eight seeds), spells
  last longer, dry spells end, specials come as often, a desert stays one, a tropical land gets its wet season.
- `Weather/WeatherDynamics.cs`: `Patch_WeatherChoice` (CurrentWeatherCommonality postfix, factors cached per map and
  tick), `Patch_WeatherForecastHeld` (ChooseNextWeather prefix: the foretold weather if it still can come),
  `Patch_WeatherStarted` (StartNextWeather postfix: the forecast's outcome, a fog's length), `Patch_WeatherTemperature`
  (GameConditionManager.AggregateTemperatureOffset postfix, map managers only). The weather's kind is read from its
  def (lightning, a fog overlay, sand; very good, toxic, monolith or mood weathers are special). Seasonal Weather's four
  lists are read by field name for the land's wet share. Conditions folded in: heat wave and heat dome, cold snap and
  polar front, the warm spell, any condition named Drought, the region's drought, hard winter and floods. The state
  lives in `WeatherBook.dyn` (eased offset at `Sched.RareWeather` 97; the day's hours for a cold front; the forecast
  and pulls saved). Forecasts once per weather, 8 to 2 hours before it ends, by the best Plants reader (6, or 3 at 50);
  stations `Stormproof_WeatherForecaster`, `NCWC_WeatherStation`, `NCWC_WindSock` (powered if they use power).
- Events: `HappeningExtension.weatherAfter` on the long rains, growing spell, thirsty herd, ice fair and high water;
  the hail's end clears the sky for a day. Annals: "It was a wet and warm year." Overview: "Hector expects rain."
- Settings `dynamicWeather`, `weatherTemperature`, `weatherForecast` (Events page, "The weather"; deep hooks).
- Not tried in the game here: worth a look at the log for the four patches applied, a forecast bubble and the
  Overview line, a cold clear night's temperature against the game's, and a few weeks of weather on the player's map.

## Taking cover with a drag, as shipped (0.14.2)

TakeCover (Workshop 3749200746, 1.6 only) transpiles `Selector.HandleMapClicks`, runs its own controller and patches
and unpatches `Selector.SelectorOnGUI` at run time to catch the drag and the release; its release never arrives in the
player's 1.7 (the cause wasn't seen in the 1.7 code: only its reference assemblies are here, where every member it
uses is unchanged). Rimfolk's version leans on the game's own `MultiPawnGotoController` instead, which polls the
mouse button for the release:
- `Defense/CoverDragMath.cs` (pure, `CoverDragTests`): the drag's frame, cell scores (the game's block chance from the
  enemy side x100, -60 for a shooter's cell it can't shoot from, the preferred depth by reach, the place on the line),
  and a greedy pick of colonist and cell with a neighbor penalty and a reach check on the pairs about to be chosen.
- `Defense/CoverDrag.cs`: `Patch_CoverDragStart` (SelectorOnGUI prefix: Ctrl + right press with drafted colonists on
  open ground starts the game's drag and uses up the press, so TakeCover's code never sees it), `Patch_CoverDragCells`
  (RecomputeDestinations prefix), `Patch_CoverDragOrders` (FinalizeInteraction prefix: orders with the cell itself as
  the click, so a drag ending on the map edge never sets exitMapOnArrival), `Patch_CoverDragEnd`, `Patch_CoverDragLabels`.
  The controller's private `start`, `end`, `pawns`, `dests` are read by name (checked unchanged in the 1.7 reference).
- Setting `coverDrag` (Interface page, needs the deep hooks). Not tried in the game here: worth a look at the cover
  labels, a drag toward raiders behind a wall, and a Ctrl + right-click with enemies in sight.

## Friendships, as shipped (0.14.1)

From the player's save (9 colonists, day 63): every pair were comrades at strength 90+, so `Council_Tie_Comrades` gave
+14 to 64 of 72 opinions; the game's deep talks (+15 each, five per pair) gave the rest. 36 of 72 opinions were 20 or
more (the Social tab's "Friend"), including pairs whose game compatibility is -1.1 to -1.5.
- `Social/ChemistryMath.cs` (pure, tested in `ChemistryTests`): factors on a memory's opinion as it's given (good x0.35
  to x1.15, bad x1.35 to x0.85), on Rimfolk's warm moments (x0.4 to x1.2) and frictions (x2 to x0.6), and the lasting
  feeling's stage by compatibility and days together (-3/-6/-10 at -0.6/-1.2/-1.8 after 5/15/30 days; +3/+6 at 1.5/2.5).
- `Social/Chemistry.cs`: compatibility is the game's `CompatibilityWith` (the same number behind deep talk and insult
  rates); `Patch_Chemistry` (TryGainMemory prefix) scales memories that come of conversation (every InteractionDef's
  thoughts, `Rimfolk_Talk_*`) between two of the colony's people; `ThoughtWorker_Chemistry` (`Council_Chemistry`).
  Partners, babies and anyone outside the colony are left alone. Gated by the social web setting and the deep hooks.
- Comrades: +3/+5/+8 (was +4/+8/+14). The Social tab's pair note says when two don't click or get on easily.
- Estimated on the save: 29 of 72 opinions at 20 or more (was 36), two who clash hardest at -12 and -16. Existing
  memories keep their old values until they fade (deep talks last 20 days).

## The weather round, as shipped (0.15.0)

The player asked for weather ideas 1, 3, 5, 6, 7 and 8, a proposal on more dynamic weather, and soft integrations for
weather mods safe to add to a running save.
- `Weather/WeatherMath.cs` (pure, `WeatherTests`): days, quadrums, records (temperature after 20 days and by 1 °C;
  spells told once when they pass an earlier one, `wetHeld`/`dryHeld`), comparisons with the year before, season names
  (5 °C from the tile's twelfth means, 48 storm hours, 70% wet days, 12 dry days on land with 600 mm or more), the
  first snow across the turn of the year, event requirements ("rain:4/7", "[fall]rain:3/7", "dry:6", "frost:5/7",
  "storm:1"), packing.
- `Weather/WeatherBook.cs`: a MapComponent, hourly at `Sched.Weather` (2185); saves `days` (30) and `quarters` (12) as
  short lines. Remarks (`WeatherTalk.SayOne`), news, the chronicle, a season-compare journal, the annals line (with the
  year's quakes), the Overview Weather card. `HappeningExtension.weather`, checked in `IncidentWorker_Happening.Gates`
  for nine events.
- Hail (`Happenings/Workers_Nature.cs`): two hours coming (`Coming`, `Falling(map)`), the reader's warning
  (`WeatherTalk.SayBy`), a harvest work party from the event's effects; rain forced only inside Rain's temperature
  range; its own rain handed back with `StartNextWeather` when it ends. Warm spell remark in `WeatherTalk`.
- Quirks (`Council_Quirks_Habits.xml`): SnowLover (Snowing), SunSeeker (Sunny, from outdoors practice), RainSleeper
  (RainAtRest); habits SnowFall and Sun (`Immersion/QuirkHabits.cs`).
- `Immersion/WeatherTouches.cs`: breath, fire steam, sauna door steam, the lightning flinch (`Patch_LightningFlinch` on
  `WeatherEventHandler.AddEvent`; `jitterer`, `WeatherEvent.map` and `strikeLoc` by name, checked in the 1.7 reference).
- `Weather/WeatherVisitors.cs`: a trade caravan at its spot with under 8 hours left gets 12 more in a storm or heavy
  snow (its `Trigger_TicksPassed` set back, once per lord, not saved); snowbound missions half again as long.
- `Weather/WeatherDisasters.cs` (Seasonal Weather): `AdvisorWorker_Firebreak` with a mark button; the earthquake's end
  starts a repair/clean/haul party.
- `Integrations/WeatherMods.cs`: Stormproof, Weather is kinda cozy, SS Lightning Rod, Disasters: Tornado, Quiet
  Wonders. The survey and the mods not recommended are in `Modlists/mods/D_NatureAnimals.md`.
- Not tried in the game here: worth a look at the Overview's Weather card after a few weeks, a hailstorm's two hours,
  breath on a cold morning, and a trader caught by a storm.

### More dynamic weather: the proposal (built in 0.16.0, above)
How the game chooses weather (1.6 code): the next weather is rolled only when the current one ends (0.27 to 2.7 days;
storms shorter), by the biome's weights times rainfall and condition factors; nothing carries over from one weather to
the next, weather never changes the temperature, and nothing is known ahead. Seasonal Weather swaps each vanilla
biome's weight list by season. Options, each a postfix bias that leaves Seasonal Weather's lists and its gate alone:
1. Spells that hold: after a few wet days rain is likelier for a while, and a long dry spell breaks with a storm.
2. Weather that follows on: a storm seldom comes out of a clear sky, fog after a cold clear night or rain, snow after a
   cold front.
3. Weather in the temperature: a few degrees cooler under rain on a hot day, colder on clear nights, milder under
   cloud (a hidden map condition's temperature offset).
4. Rimfolk's events moving the real weather: the long rains bring days of rain, a dry spell's events less of it, a
   warm spell clear skies (a hidden condition's weather factor).
5. A forecast: a weather-wise colonist (or a forecaster building from Stormproof or WeatherControl) foretells the next
   weather, which is then held to unless it can no longer come.
6. Years with a character of their own: each year a little wetter or drier, warmer or colder, for the annals.

## The defense overhaul, as shipped

Research: `docs/defense-research/mechanics.md` (game mechanics with numbers, mostly from the decompiled 1.6 code) and
`designs.md` (designs that hold in 1.6, K1–K4, G1, P1, and a template-by-template review); `Tools/DefenseSim/kbsim.py`.

Code:
- `DefenseSight.cs` (pure): the game's LineOfSight, lean sources, CanHit and Cover; `Read(grid)` (worst raider cover
  while able to shoot at a firing position, leaving out cells at or behind the line) and `Problems(grid, grenades)`.
- `DefenseShapes.cs` (pure): `DefenseShapeSpec` (shooters, depth, turret size/count, grenades, low tech, range, raid
  points; `Save`/`Parse`), `DefenseShapeOptions` (the XML `<shape>`), and the generators: box (killbox, small, tunnel,
  baffled gate, palisade/castle yard), side lines, firing line and post of pockets, turret nest, trap corridor. The box
  lengthens its straight (corner seen) or deepens (doorway angle) until `DefenseSight.Read` passes; side lines try
  placements smallest first until hidden from outside, ≤30% in the gate and out of a throw.
- `DefenseLayout.Problems` accepts pockets (`Pocket`); size cap 41.
- `DefenseTemplateDef`: `shape`, `ShapeFor/GridFor(spec)`, `GridFor(map, cfg)`, `SpecFor`, `ColonySpec` (survey: ranged
  ≥ 4, best range, bows or pre-industrial, industrial or empire hostiles = grenades, raid points, the turret the plan
  picks; a manned turret is no box decoy), `BehindFor`, `SampleGrid`, `ResolveCached(map, out reason, cfg)` keyed by
  spec. `DefensePlanConfig.spec / fixedLayout / moreShooters / moreDepth`, `Resize`, `ColonySize`, `Of(saved)`.
  `DefensePlan.grid / spec`; `DefenseLinePlan.shape` saved (null = fixed layout, or placed before 0.14).
- Line review: `FirePos.lean` (pockets count and see by leaning); the killbox step notes an outgrown or pre-0.14
  killbox and offers "Plan it again"; close the gap offers the baffled gate against grenades when its trial fits.
- Planner window: "Drawn for your colony" with Shooters and Depth −/+, what it was drawn for, a grenade warning.
- Tests: `DefenseTests.DrawnChecks` draws every drawn template for ~650 colonies (8,400 plans) and checks both
  Problems, the world checks for a middle colony, and the spec's save round trip.

Not done, ideas for later (propose to the player first): a second rank of shooters for very big raids; mortars and
EMP steps on the ladder; a bait yard with an orbital trade beacon (40% of center drops); turrets as targets in the
sight check (the corner camper can see axis decoys); the line review re-planning the gate lines placed before 0.14
(the old gate's lines stand within a grenade's throw). Nothing was tried in the game itself here: worth a look at a
killbox's preview, the planner's new section, and a built pocket line in a line's review.
