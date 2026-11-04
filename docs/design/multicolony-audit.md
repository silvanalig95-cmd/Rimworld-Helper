# Rimfolk with more than one colony: where it assumes one (audit, 4 November 2026)

Read-only audit for stage 0 (ROADMAP.md, "The world and its people"). Paths under `Source/ColonyCouncil/`.
Totals: 38 BREAKS (wrong with two colonies), 48 ODD (works but reads or feels wrong), 45 FINE.

## The root cause
- `Lean/MapFacts.cs:200-229` `BusiestHome` (the home map with the most free colonists on the ground), wrapped by
  `Happenings/Happenings.cs:532` `HappeningTracker.HomeMap()`, is "the colony" for about 80 call sites. It is worked
  out every 250 ticks, so "the colony" switches maps as people move, and the smaller colony never gets anything
  scheduled through it.
- `PawnsFinder.AllMaps_FreeColonists` and its kin are used about 140 times where "our colonists" is meant.
- `MeetingAttendance.Members(map)` (`Ambitions/MeetingAttendance.cs:102-122`, `HomeIs`) is the one helper that knows
  which colony a pawn belongs to: the start of a "people of this colony" helper.

## One per game or one per map
- GameComponents: the council (`CouncilGameComponent`: wishes, offices, decrees, ambitions and meetings, dilemmas and
  traditions, social web, epithets, annals, automations, suggestions), calendar, heart, groups, reputation, journeys,
  camps, visitors, callers, couriers, outsiders, world situations, notables, play-outs, ailments, health, persona,
  arcs, faith.
- MapComponents: the advisor (`CouncilMapComponent`), defense lines and overlay, fields by the weather, the weather
  book, the build and plan queues.

## By system (BREAKS / ODD / FINE, and the way forward)
1. Home map and events (Happenings) 3/1/1. "The colony" is the busiest home; Rimfolk's event timer fires only there
   (`Happenings.cs:562-572`); `Memo` (`:375`) keeps no map, so follow-ups land wherever is busiest when due
   (`Workers_Living.cs:675, 1141, 1225, 1335, 1676`, `Workers_Storyteller.cs:493`, `Workers_NewEvents.cs:703`); an
   unanswered event letter holds back both colonies (`:576`). Way: a map on `Memo`, the timer per home.
   Status: Done. 7a: the event timer per home, memos keep their colony. 7b: pick-ups take only their own colony's memos;
   an unanswered event letter holds back only its own colony.
2. Offices and elections 5/5/1. One holder per seat (`Offices/Offices.cs:115-116`); `Eligible()` takes everyone
   (`:166`); the Peacemaker cools rivalries everywhere (`:253-255`); a seat's hediff reaches every map (`:694-715`); the
   election is held at the busiest home (`OfficesElection.cs:106`); years from `AnyPlayerHomeMap` (`Offices.cs:214,
   668`, `UI/DecreePanel.cs:631`); texts "The colony will choose its council again". Way: per colony.
   Status: Done (7a: offices and elections per colony). 7b: the officers at work (Interplay) and the herdmaster are each
   colony's.
3. Council meetings and ambitions 2/2/2. One ambition and one meeting (`Ambitions/Ambitions.cs:176-178`);
   `MeetingMap()` the map with most voters (`:325-341`); clean-up checks the map exists, not that it is ours
   (`:246-247, 270`). Way: per colony.
   Status: Done (7a).
4. Decrees 8/4/0. One `DecreeTracker` (`CouncilGameComponent.cs:86`); eligibility, effects, tithes and petitions from
   the busiest map (`Decrees/DecreeWork.cs:44, 159-163, 191-197, 249-255`; `Decrees.cs:379, 700`); effects on every
   colonist everywhere (`DecreeWork.cs:225, 266, 443, 472`; `Decrees.cs:580, 640, 661, 793, 816`;
   `DecreeFaith.cs:200`); daily trust (`CouncilDynamics.cs:873-897`); the tally letter mixes both
   (`CouncilDynamics.cs:829-861`). Way: per colony.
   Status: Done (7a). 7b: the quarantine's visitor factor per colony. Left by design: the one faith a decree names stays
   one for the game (the game keeps one faith per people, not per map).
5. Wishes, petitions, trust, standards 3/4/4. Petitions joined across colonies (`CouncilGameComponent.cs:726-734`);
   the Speaker raises everyone's trust (`CouncilDynamics.cs:709-714`); the last-few-people guard counts both (`:776`);
   one wish pool and cap (`CouncilGameComponent.cs:628-630`); `Standing.cs:156` and `Standards.cs:64-72` global. Way:
   trust per pawn as now, a colony key on the pool and on petition matching.
   Status: Done (7a).
6. Dilemmas and traditions 3/1/2. `TryFire` at the busiest (`Dilemmas/Dilemmas.cs:1175`); a raid anywhere triggers
   its aftermath at the busiest (`:1141`); one tradition list (`:1107-1109, 1148-1153`). Way: a colony key on the pool,
   traditions per colony.
   Status: Done (7a). 7b: the questions still asked of the seat (Workers_Living traditions, camps, GuestWill, shared
   names, guardians, MovingOnHeld, ResidentBeast, encounters) are asked of the colony concerned.
7. Calendar 4/5/3. Days only at the busiest (`Calendar/CalendarTracker.cs:162, 192-216`), `seenDay` follows its local
   date; the keeping letter keeps no map (`CalendarKeeping.cs:304, 1077-1088`); a victory is filed at the busiest
   (`CalendarTracker.cs:603-607`); deaths, harvest, snow and land at the busiest (`:518, 560-580, 696`,
   `CalendarKeeping.cs:136`). Way: one calendar per home (each settlement its own founding day).
   Status: Done (7b): one calendar per colony (ColonyCalendar), each its own founding day, date, days and keeping
   letter; victories, deaths, harvest, snow and the land read per colony; an old save's calendar to the seat.
8. Heart and feelings 0/2/3. One episode cooldown (`Heart/HeartTracker.cs:23`, `Episodes.cs:119, 165`). Way: fine;
   the cooldown per map if wanted.
   Status: Left as the audit says (fine): one episode cooldown.
9. Reputation and names 1/4/2. A name taken over brings a rivalry across colonies (`Epithets/Epithets.cs:198-203`);
   one holder per name; founders and firstborn once a game; `ReputationTracker.cs:66-87` everyone. Way: deeds per
   pawn, a colony key on names.
   Status: Done (7b): a colony key on names, rivals only within one colony, founders and firstborn per colony. Left
   (fine): deeds and reputation per pawn.
10. Groups and the social web 2/2/3. One battle state (`Social/SocialWeb.cs:115-116, 553-605`); social events at the
    busiest (`Social/SocialEvents.cs:94-117`); groups across colonies (`SocialWeb.cs:422`). Way: battle per map, groups
    within one home.
    Status: Done (7b): a fight per home (Battle), social events per home, groups within one home.
11. Journeys 0/1/3. One plan a game (`Journeys/Journeys.cs:233`). Way: fine; one per home if wanted.
    Status: Done (7b): one plan per home.
12. Defense lines 0/1/2. Messages don't name the settlement (`DefenseLinesWatch.cs:246, 274`).
    Status: Fine since step 6: the messages go through Tell, which names the settlement by what they point at.
13. Advisor, suggestions, automations 2/2/4. Suggestion keys have no map (`Advisor/AdvisorSteps.cs:16`,
    `Building/Suggest/Suggestions.cs:147-235`); automations' cooldowns and budget shared (`Building/Automations.cs:120-121,
    346-363`). Way: a map id in keys and budgets.
    Status: Done (7b): the colony in the suggestion keys (Not this, waiting) and in the automations' cooldowns, budget
    and caps; the game-wide advice at each home.
14. Fields by the weather 0/2/3. The frost alert checks the map in view only (`FieldWeather.cs:886`).
    Status: Left (ODD): the frost alert still looks at the map in view.
15. Camps 2/2/2. Camps rolled for the busiest home only (`Camps/CampTimer.cs:63-110`); allies help only there
    (`WarCamp.cs:73-91`); one camp, one neighbour, one war band (`CampTracker.cs:39-41, 91`). Way: a colony key.
    Status: Done (7b): camps, holding, war band and clock per home (CampHome); allies at any home.
16. Visitors and callers 0/0/4.
    Status: Fine.
17. Couriers 0/1/1. `TakeFor` falls back to `HomeMap()` (`Couriers.cs:594`).
    Status: Left (ODD): TakeFor falls back to the seat only when an answer has no map.
18. Outsiders 1/3/3. Talks start only on `Find.CurrentMap` (`Outsiders/OutsiderTalk.cs:457`); kin from all maps
    (`OutsiderFacts.cs:183`); distance from the busiest (`:280`); the favour bearer anywhere (`Outsiders.cs:720`). Way:
    loop the homes, prefer `x.MapHeld`.
    Status: Partly done (F1: talks on every home). Left (ODD): kin from all maps, distance from the seat, the favour
    bearer anywhere.
19. Other 2/6/2. Raid journal entries at `AnyPlayerHomeMap` only (`Journal.cs:197-205`); encounters at the busiest
    (`Strangers/Encounters.cs:139`); one herd leader and resident beast (`Animals/HerdStory.cs:39`,
    `ResidentBeast.cs:54`); the region anchored at the busiest (`World/WorldSituations.cs:154`, `Notables.cs:306`); the
    year letter about the busiest (`World/YearLetter.cs:30-50`); `Annals.cs:74, 81`; wealth summed (`History.cs:49`,
    `UI/OverviewPanel.cs:122`); `Building/GrowthNeeds.cs:153`. Worth copying: `World/Workers_World.cs:177-178` and
    `Social/Family.cs:243` name the settlement through `map.Parent`.
    Status: Done: raid journals per raided home (F1); encounters, the herd leader and the resident beast per colony, the
    year letter with a part per colony, the annals' raids per home and the whole colony's wealth (7b). Left: the region
    anchored at the seat (one region map for now); GrowthNeeds' arrivals with no map count at the first home.

## Texts
"The colony" is everywhere and mostly fine (one event on one map). Mixing two: the election announcement and
ceremony, the decree tally, tradition born or lapsed, calendar status and "missed", the ambition achieved, the year
letter, an office vacant; 54 `News.Post(..., NewsKind.Colony, ...)` calls never say which settlement.
Status: done for the council (7a) and the calendar (7b); 7b sends the news that was told with no map but is about a
home through Tell with its home, so it names the settlement. What stays without a home is the world's news.

## Handing a colony over
Clean-up checks that a map exists, not that it is ours: `Ambitions.cs:246, 270`, `OfficesElection.cs:270`,
`JourneyRun.cs:70`, `Camp.cs:357`, `Kept.cs:374-393` (`Missions/Missions.cs:107` does check `IsPlayerHome`).
Pawn-keyed state is pruned when a pawn stops being ours (offices, standing, heart, names), which would post a
"vacant seat" for each officer with no wording for a hand-over. Nothing hooks a faction change.
Status: done. The meeting and the election (7a), a journey plan, a promise kept and a camp (7b) end when their home
is no longer ours; each book kept per colony keeps a lost home's entry thirty days, then lets it go. Saving still drops
references to maps the game no longer has.
