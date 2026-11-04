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
2. Offices and elections 5/5/1. One holder per seat (`Offices/Offices.cs:115-116`); `Eligible()` takes everyone
   (`:166`); the Peacemaker cools rivalries everywhere (`:253-255`); a seat's hediff reaches every map (`:694-715`); the
   election is held at the busiest home (`OfficesElection.cs:106`); years from `AnyPlayerHomeMap` (`Offices.cs:214,
   668`, `UI/DecreePanel.cs:631`); texts "The colony will choose its council again". Way: per colony.
3. Council meetings and ambitions 2/2/2. One ambition and one meeting (`Ambitions/Ambitions.cs:176-178`);
   `MeetingMap()` the map with most voters (`:325-341`); clean-up checks the map exists, not that it is ours
   (`:246-247, 270`). Way: per colony.
4. Decrees 8/4/0. One `DecreeTracker` (`CouncilGameComponent.cs:86`); eligibility, effects, tithes and petitions from
   the busiest map (`Decrees/DecreeWork.cs:44, 159-163, 191-197, 249-255`; `Decrees.cs:379, 700`); effects on every
   colonist everywhere (`DecreeWork.cs:225, 266, 443, 472`; `Decrees.cs:580, 640, 661, 793, 816`;
   `DecreeFaith.cs:200`); daily trust (`CouncilDynamics.cs:873-897`); the tally letter mixes both
   (`CouncilDynamics.cs:829-861`). Way: per colony.
5. Wishes, petitions, trust, standards 3/4/4. Petitions joined across colonies (`CouncilGameComponent.cs:726-734`);
   the Speaker raises everyone's trust (`CouncilDynamics.cs:709-714`); the last-few-people guard counts both (`:776`);
   one wish pool and cap (`CouncilGameComponent.cs:628-630`); `Standing.cs:156` and `Standards.cs:64-72` global. Way:
   trust per pawn as now, a colony key on the pool and on petition matching.
6. Dilemmas and traditions 3/1/2. `TryFire` at the busiest (`Dilemmas/Dilemmas.cs:1175`); a raid anywhere triggers
   its aftermath at the busiest (`:1141`); one tradition list (`:1107-1109, 1148-1153`). Way: a colony key on the pool,
   traditions per colony.
7. Calendar 4/5/3. Days only at the busiest (`Calendar/CalendarTracker.cs:162, 192-216`), `seenDay` follows its local
   date; the keeping letter keeps no map (`CalendarKeeping.cs:304, 1077-1088`); a victory is filed at the busiest
   (`CalendarTracker.cs:603-607`); deaths, harvest, snow and land at the busiest (`:518, 560-580, 696`,
   `CalendarKeeping.cs:136`). Way: one calendar per home (each settlement its own founding day).
8. Heart and feelings 0/2/3. One episode cooldown (`Heart/HeartTracker.cs:23`, `Episodes.cs:119, 165`). Way: fine;
   the cooldown per map if wanted.
9. Reputation and names 1/4/2. A name taken over brings a rivalry across colonies (`Epithets/Epithets.cs:198-203`);
   one holder per name; founders and firstborn once a game; `ReputationTracker.cs:66-87` everyone. Way: deeds per
   pawn, a colony key on names.
10. Groups and the social web 2/2/3. One battle state (`Social/SocialWeb.cs:115-116, 553-605`); social events at the
    busiest (`Social/SocialEvents.cs:94-117`); groups across colonies (`SocialWeb.cs:422`). Way: battle per map, groups
    within one home.
11. Journeys 0/1/3. One plan a game (`Journeys/Journeys.cs:233`). Way: fine; one per home if wanted.
12. Defense lines 0/1/2. Messages don't name the settlement (`DefenseLinesWatch.cs:246, 274`).
13. Advisor, suggestions, automations 2/2/4. Suggestion keys have no map (`Advisor/AdvisorSteps.cs:16`,
    `Building/Suggest/Suggestions.cs:147-235`); automations' cooldowns and budget shared (`Building/Automations.cs:120-121,
    346-363`). Way: a map id in keys and budgets.
14. Fields by the weather 0/2/3. The frost alert checks the map in view only (`FieldWeather.cs:886`).
15. Camps 2/2/2. Camps rolled for the busiest home only (`Camps/CampTimer.cs:63-110`); allies help only there
    (`WarCamp.cs:73-91`); one camp, one neighbour, one war band (`CampTracker.cs:39-41, 91`). Way: a colony key.
16. Visitors and callers 0/0/4.
17. Couriers 0/1/1. `TakeFor` falls back to `HomeMap()` (`Couriers.cs:594`).
18. Outsiders 1/3/3. Talks start only on `Find.CurrentMap` (`Outsiders/OutsiderTalk.cs:457`); kin from all maps
    (`OutsiderFacts.cs:183`); distance from the busiest (`:280`); the favour bearer anywhere (`Outsiders.cs:720`). Way:
    loop the homes, prefer `x.MapHeld`.
19. Other 2/6/2. Raid journal entries at `AnyPlayerHomeMap` only (`Journal.cs:197-205`); encounters at the busiest
    (`Strangers/Encounters.cs:139`); one herd leader and resident beast (`Animals/HerdStory.cs:39`,
    `ResidentBeast.cs:54`); the region anchored at the busiest (`World/WorldSituations.cs:154`, `Notables.cs:306`); the
    year letter about the busiest (`World/YearLetter.cs:30-50`); `Annals.cs:74, 81`; wealth summed (`History.cs:49`,
    `UI/OverviewPanel.cs:122`); `Building/GrowthNeeds.cs:153`. Worth copying: `World/Workers_World.cs:177-178` and
    `Social/Family.cs:243` name the settlement through `map.Parent`.

## Texts
"The colony" is everywhere and mostly fine (one event on one map). Mixing two: the election announcement and
ceremony, the decree tally, tradition born or lapsed, calendar status and "missed", the ambition achieved, the year
letter, an office vacant; 54 `News.Post(..., NewsKind.Colony, ...)` calls never say which settlement.

## Handing a colony over
Clean-up checks that a map exists, not that it is ours: `Ambitions.cs:246, 270`, `OfficesElection.cs:270`,
`JourneyRun.cs:70`, `Camp.cs:357`, `Kept.cs:374-393` (`Missions/Missions.cs:107` does check `IsPlayerHome`).
Pawn-keyed state is pruned when a pawn stops being ours (offices, standing, heart, names), which would post a
"vacant seat" for each officer with no wording for a hand-over. Nothing hooks a faction change.
