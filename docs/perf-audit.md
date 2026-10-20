# Performance audit (static)

Written by `Tools/perf_audit.py` (run `python3 Tools/perf_audit.py` from the repository root; `--json` saves the
numbers, `--compare` adds an earlier run's). It reads the mod's C# without the game: it finds every entry point the
game calls on a clock or often, follows the calls into Rimfolk's own code, and weighs what runs inside loops. The
cost of an entry point is how often it runs in an in-game hour times the weighted work of one run. These are
estimates for ranking where to look, not measurements; a profiler in a real save has the last word.

**Assumptions.** A large colony: 20 colonists, 120 pawns on the map, a few thousand things; speed 3 at 60 frames a
second (an in-game hour is about 833 frames; GUI code runs at least twice a frame). Vanilla asks situational moods
every 100 ticks per colonist, checks alerts every 24 frames and works out moods every 150 ticks. A loop multiplies
what is in it by what it goes over (colonists 20, all pawns 120, things 400, cells 200, rooms 60, Rimfolk's defs 60,
the game's big def lists 500 to 3,000, other lists 12). A pass gated by the tick of the hour runs once an hour, by
the first hour of the day once a day, by `Rand.Chance(p)` p of the time. Weights (rough units, a LINQ iterator with its
delegate is 1): LINQ; def by name; def scan; string; Translate; pawn scan; room query; lister scan; reach or path; new collection; stat — see `PATTERNS` in the script.

Per-frame code (windows, tabs, overlays) only costs while it is on screen; it is ranked by its cost while open.

Entry points found: 309. Estimated total: 7,736,072,553 units an hour.

| Kind | Units an hour | Share |
|---|---|---|
| tick | 4,294,925,783 | 56% |
| frame | 3,438,760,378 | 44% |
| patch | 2,071,488 | 0% |
| mood | 177,059 | 0% |
| job | 122,293 | 0% |
| talk | 8,310 | 0% |
| job tick | 3,875 | 0% |
| stat | 1,800 | 0% |
| alert | 1,334 | 0% |
| event | 232 | 0% |

## Entry points, by estimated cost (top 40)

Runs an hour × work a run = units an hour. "Work" lists the biggest kinds of work inside (inclusive of what it calls).

| # | Entry point | Where | Runs an hour | Work a run | Units an hour | Work |
|---|---|---|---|---|---|---|
| 1 | AilmentTracker.GameComponentTick → AilmentTracker.Slice (a gate kept in a variable) | `Source/ColonyCouncil/Ailments/AilmentTracker.cs:199` | 10 | 423,460,877 | 4,234,608,766 | lister scan 98%, LINQ 1%, room query 1%, string 0% |
| 2 | MainTabWindow_Council.DoWindowContents | `Source/ColonyCouncil/UI/MainTabWindow_Council.cs:187` | 1,666 | 2,048,925 | 3,413,509,400 | Translate 98%, lister scan 2%, LINQ 0%, string 0% |
| 3 | PlayOutTracker.GameComponentTick → PlayOutTracker.Step (every 250 ticks) | `Source/ColonyCouncil/PlayOut/PlayOutTracker.cs:160` | 10 | 5,718,975 | 57,189,755 | reach or path 97%, room query 2%, LINQ 0%, Translate 0% |
| 4 | Dialog_Camp.DoWindowContents | `Source/ColonyCouncil/Camps/CampUI.cs:120` | 1,666 | 7,323 | 12,199,607 | string 39%, Translate 30%, LINQ 23%, new collection 5% |
| 5 | Window_FloorPicker.DoWindowContents | `Source/ColonyCouncil/Building/Window_FloorPicker.cs:190` | 1,666 | 2,341 | 3,900,612 | lister scan 70%, Translate 19%, LINQ 8%, new collection 2% |
| 6 | Window_DefensePlanner.DoWindowContents | `Source/ColonyCouncil/Defense/Window_DefensePlanner.cs:90` | 1,666 | 1,987 | 3,310,463 | Translate 85%, string 8%, LINQ 6%, lister scan 1% |
| 7 | Window_RoomPlanner.DoWindowContents | `Source/ColonyCouncil/Building/Window_RoomPlanner.cs:108` | 1,666 | 947 | 1,577,385 | Translate 49%, lister scan 31%, string 11%, LINQ 7% |
| 8 | Patch_AilmentDiet.Postfix | `Source/ColonyCouncil/Ailments/Patch_AilmentDiet.cs:18` | 80 | 19,088 | 1,527,066 | lister scan 98%, LINQ 1%, room query 1%, string 0% |
| 9 | MainTabWindow_Rooms.DoWindowContents | `Source/ColonyCouncil/UI/MainTabWindow_Rooms.cs:60` | 1,666 | 799 | 1,331,270 | LINQ 37%, Translate 28%, lister scan 20%, string 6% |
| 10 | DefenseOverlay.MapComponentOnGUI | `Source/ColonyCouncil/Defense/DefenseOverlay.cs:145` | 1,666 | 624 | 1,040,084 | Translate 80%, string 13%, LINQ 7% |
| 11 | LordJob_PlayOut.LordJobTick → LordJob_PlayOut.Serve (when due) | `Source/ColonyCouncil/PlayOut/PlayOutLord.cs:179` | 125 | 6,808 | 850,956 | reach or path 85%, LINQ 6%, Translate 4%, new collection 2% |
| 12 | ITab_Pawn_Mood.FillTab | `Source/ColonyCouncil/Moods/ITab_Pawn_Mood.cs:57` | 1,666 | 488 | 812,842 | Translate 83%, string 17%, LINQ 0%, new collection 0% |
| 13 | Patch_GoodTimesFade.Postfix | `Source/ColonyCouncil/Patches/Patch_GoodTimes.cs:31` | 400 | 1,228 | 491,247 | def by name 83%, string 10%, LINQ 4%, new collection 3% |
| 14 | CouncilGameComponent.GameComponentTick → CouncilGameComponent.HourlyWishes (hourly) | `Source/ColonyCouncil/CouncilGameComponent.cs:315` | 1 | 369,592 | 369,592 | LINQ 50%, pawn scan 20%, Translate 10%, def by name 7% |
| 15 | ITab_Pawn_Story.FillTab | `Source/ColonyCouncil/UI/ITab_Pawn_Story.cs:41` | 1,666 | 200 | 333,306 | Translate 96%, string 4%, LINQ 0%, new collection 0% |
| 16 | Dialog_News.DoWindowContents | `Source/ColonyCouncil/UI/News.cs:404` | 1,666 | 190 | 316,748 | Translate 100%, string 0% |
| 17 | StoriesComponent.GameComponentTick → Missions.Tick (every 250 ticks) | `Source/ColonyCouncil/Missions/Missions.cs:441` | 10 | 31,613 | 316,125 | LINQ 65%, Translate 29%, def by name 2%, string 1% |
| 18 | VisualTouches.GameComponentTick → VisualTouches.Hourly (every 30 ticks) | `Source/ColonyCouncil/Touches/VisualTouches.cs:504` | 83.3 | 3,319 | 276,596 | room query 21%, new collection 19%, string 14%, LINQ 14% |
| 19 | VisualTouches.GameComponentTick → WeatherTalk.Hourly (every 30 ticks) | `Source/ColonyCouncil/Immersion/WeatherTalk.cs:72` | 83.3 | 3,247 | 270,578 | room query 22%, new collection 19%, LINQ 14%, string 13% |
| 20 | SetbackTracker.GameComponentTick → SetbackTracker.Refresh (every 60 ticks) | `Source/ColonyCouncil/Setbacks/SetbackTracker.cs:248` | 41.7 | 5,309 | 221,209 | Translate 61%, LINQ 23%, string 14%, new collection 3% |
| 21 | DefenseOverlay.MapComponentUpdate | `Source/ColonyCouncil/Defense/DefenseOverlay.cs:71` | 833 | 249 | 207,097 | Translate 42%, LINQ 38%, new collection 13%, string 6% |
| 22 | AilmentTracker.GameComponentTick → AilmentTracker.ProcessEnded (only when something is queued) | `Source/ColonyCouncil/Ailments/AilmentTracker.cs:1126` | 25 | 7,389 | 184,727 | LINQ 19%, def by name 19%, Translate 18%, new collection 13% |
| 23 | Dialog_CalendarDay.DoWindowContents | `Source/ColonyCouncil/Calendar/CalendarPanel.cs:321` | 1,666 | 101 | 167,936 | Translate 63%, string 37% |
| 24 | ArcTracker.GameComponentTick → ArcTracker.Hourly (hourly, passed along) | `Source/ColonyCouncil/Arcs/ArcTracker.cs:161` | 1 | 150,462 | 150,462 | string 30%, Translate 28%, LINQ 28%, pawn scan 6% |
| 25 | ThoughtWorker_GroupBond.CurrentSocialStateInternal | `Source/ColonyCouncil/Social/Groups/GroupView.cs:238` | 2,375 | 59.5 | 141,421 | new collection 100% |
| 26 | JoyGiver_Habit.TryGiveJob | `Source/ColonyCouncil/Immersion/SmallHabits.cs:348` | 120 | 932 | 111,876 | reach or path 97%, room query 2%, LINQ 1%, pawn scan 0% |
| 27 | HeartTracker.GameComponentTick → HeartTracker.Slice (a gate kept in a variable) | `Source/ColonyCouncil/Heart/HeartTracker.cs:154` | 10 | 10,483 | 104,831 | Translate 65%, LINQ 26%, string 4%, pawn scan 2% |
| 28 | TalkTracker.GameComponentTick → Talk.Step | `Source/ColonyCouncil/Social/Talk/Talk.cs:775` | 2,500 | 38.3 | 95,808 | room query 98%, new collection 2%, string 0% |
| 29 | LordJob_PlayOut.LordJobTick → ToastDrinks.Pour (when due) | `Source/ColonyCouncil/Integrations/ToastDrinks.cs:28` | 125 | 544 | 67,998 | reach or path 87%, stat 7%, LINQ 6%, new collection 1% |
| 30 | WorldSituations.GameComponentTick → WorldSituations.Hourly (hourly) | `Source/ColonyCouncil/World/WorldSituations.cs:127` | 1 | 27,033 | 27,033 | LINQ 38%, string 24%, Translate 23%, reach or path 6% |
| 31 | HeartTracker.GameComponentTick → HeartLogic.Peek (once after loading or building, filtered) | `Source/ColonyCouncil/Heart/HeartLogic.cs:67` | 1/2 | 50,848 | 25,424 | Translate 64%, LINQ 29%, string 3%, pawn scan 2% |
| 32 | CouncilGameComponent.GameComponentTick → AmbitionTracker.MeetingTick (every 60 ticks) | `Source/ColonyCouncil/Ambitions/Ambitions.cs:466` | 41.7 | 606 | 25,268 | reach or path 58%, Translate 34%, string 3%, room query 2% |
| 33 | Patch_TalkBuilt.Postfix | `Source/ColonyCouncil/Social/Talk/TalkPatches.cs:30` | 30 | 626 | 18,793 | LINQ 100%, new collection 0%, pawn scan 0%, def by name 0% |
| 34 | ThoughtWorker_Pressure.CurrentStateInternal | `Source/ColonyCouncil/Pressures/PressureThoughts.cs:82` | 500 | 35.2 | 17,588 | LINQ 61%, new collection 29%, def by name 10% |
| 35 | PlantingWatch.GameComponentTick (its own work) | `Source/ColonyCouncil/Dilemmas/PlantingDay.cs:195` | 2,500 | 6.38 | 15,950 | Translate 86%, string 14% |
| 36 | PlantingWatch.GameComponentTick → PlantingDay.Sown (when due) | `Source/ColonyCouncil/Dilemmas/PlantingDay.cs:42` | 125 | 122 | 15,275 | LINQ 100% |
| 37 | CouncilGameComponent.GameComponentTick → CouncilGameComponent.DailySecond (hourly, first hour of the day, passed along) | `Source/ColonyCouncil/CouncilDynamics.cs:656` | 1/24 | 365,312 | 15,221 | LINQ 37%, def by name 35%, Translate 10%, string 8% |
| 38 | Dialog_NameLine.DoWindowContents | `Source/ColonyCouncil/UI/DefenseLinesPanel.cs:686` | 1,666 | 9.00 | 14,994 | Translate 100% |
| 39 | GameCondition_Hail.GameConditionTick → GameCondition_Hail.Crops (every 250 ticks) | `Source/ColonyCouncil/Happenings/Workers_Nature.cs:142` | 10 | 1,453 | 14,532 | new collection 100% |
| 40 | Dialog_SelfCheck.DoWindowContents | `Source/ColonyCouncil/UI/SelfCheck.cs:315` | 1,666 | 8.71 | 14,517 | LINQ 55%, string 45% |

## Where the cost lands: methods by their own work (top 40)

Each method's own work (not what it calls), times how often the entry points above reach it.

| # | Method | Where | Units an hour | Work |
|---|---|---|---|---|
| 1 | Avail.CanBuild | `Source/ColonyCouncil/Avail.cs:83` | 3,518,085,245 | lister scan 100% |
| 2 | DefensePlanner.PieceStateAt | `Source/ColonyCouncil/Defense/DefensePlanner.cs:240` | 2,758,054,373 | Translate 100%, lister scan 0% |
| 3 | AdvisorWorker_WinterStores.Pemmican | `Source/ColonyCouncil/Advisor/AdvisorSignals5.cs:433` | 366,200,002 | lister scan 85%, LINQ 15%, def by name 0%, def scan 0% |
| 4 | Avail.NeedsPower | `Source/ColonyCouncil/Avail.cs:132` | 319,825,931 |  |
| 5 | DefensePlanner.Warnings | `Source/ColonyCouncil/Defense/DefensePlanner.cs:388` | 228,096,890 | Translate 98%, LINQ 2% |
| 6 | TemplateLayout.RootFor | `Source/ColonyCouncil/Building/TemplateLayout.cs:125` | 137,053,918 |  |
| 7 | DefensePlanner.Plan | `Source/ColonyCouncil/Defense/DefensePlanner.cs:131` | 99,498,874 | Translate 83%, lister scan 14%, LINQ 2%, new collection 1% |
| 8 | DefensePlanner.ShelterIn | `Source/ColonyCouncil/Defense/DefensePlanner.cs:564` | 78,966,074 | lister scan 100% |
| 9 | HerdHome.Destination | `Source/ColonyCouncil/Animals/HerdHome.cs:85` | 53,231,997 | reach or path 100%, LINQ 0% |
| 10 | TemplateMath.RotatePoint | `Source/ColonyCouncil/Building/TemplateMath.cs:29` | 48,994,115 |  |
| 11 | RoomUtil.HasFreezer | `Source/ColonyCouncil/RoomUtil.cs:108` | 25,181,442 | room query 100%, lister scan 0% |
| 12 | DefensePlanner.Search | `Source/ColonyCouncil/Defense/DefensePlanner.cs:578` | 18,107,725 | LINQ 100% |
| 13 | TemplatePlanner.GroundHolds | `Source/ColonyCouncil/Building/TemplatePlanner.cs:1139` | 10,540,151 |  |
| 14 | Avail.IsFridge | `Source/ColonyCouncil/Avail.cs:417` | 6,391,266 |  |
| 15 | DefensePlanner.CloseGap | `Source/ColonyCouncil/Defense/DefensePlanner.cs:338` | 4,465,380 | new collection 62%, LINQ 37%, lister scan 0% |
| 16 | DefenseSurvey.Direction | `Source/ColonyCouncil/Defense/DefenseSurvey.cs:494` | 4,430,585 | Translate 86%, string 14% |
| 17 | DefensePlan.Count | `Source/ColonyCouncil/Defense/DefensePlanner.cs:45` | 4,387,508 | LINQ 100% |
| 18 | Persona.Word | `Source/ColonyCouncil/Persona/PersonaTracker.cs:324` | 4,343,676 | Translate 67%, string 33% |
| 19 | DefensePlanner.Fit | `Source/ColonyCouncil/Defense/DefensePlanner.cs:630` | 2,851,553 | LINQ 100% |
| 20 | LetterTags.Who | `Source/ColonyCouncil/Dilemmas/DilemmaChoices.cs:186` | 2,271,334 | string 100% |
| 21 | AlmanacPanel.Inline | `Source/ColonyCouncil/Almanac/AlmanacPanel.cs:684` | 1,919,232 | string 100% |
| 22 | DefenseLineReview.AutoName | `Source/ColonyCouncil/Defense/DefenseLineReview.cs:188` | 1,847,841 | Translate 67%, LINQ 22%, string 11% |
| 23 | DefenseLayout.CarryLine | `Source/ColonyCouncil/Defense/DefenseLayout.cs:395` | 1,755,604 |  |
| 24 | DefenseLinesPanel.Step | `Source/ColonyCouncil/UI/DefenseLinesPanel.cs:384` | 1,637,933 | Translate 93%, string 6%, new collection 1% |
| 25 | Avail.HaveOrCanGet | `Source/ColonyCouncil/Avail.cs:195` | 1,613,318 | lister scan 100% |
| 26 | PlaySpots.Ok | `Source/ColonyCouncil/PlayOut/PlaySpots.cs:327` | 1,466,650 | room query 93%, reach or path 7% |
| 27 | LineTags.Match | `Source/ColonyCouncil/Lean/LineTags.cs:126` | 1,340,828 |  |
| 28 | GroupView.Card | `Source/ColonyCouncil/Social/Groups/GroupView.cs:81` | 1,290,302 | Translate 72%, string 20%, LINQ 5%, room query 3% |
| 29 | GroupView.RoleTip | `Source/ColonyCouncil/Social/Groups/GroupView.cs:192` | 1,039,919 | Translate 69%, string 23%, room query 4%, new collection 4% |
| 30 | Beliefs.Bad | `Source/ColonyCouncil/Wishes/Beliefs.cs:175` | 1,003,789 |  |
| 31 | CampAnswers.Silver | `Source/ColonyCouncil/Camps/CampAnswers.cs:38` | 911,984 | LINQ 76%, lister scan 24% |
| 32 | DefenseLinesPanel.Notes | `Source/ColonyCouncil/UI/DefenseLinesPanel.cs:508` | 911,651 | Translate 60%, LINQ 25%, string 14% |
| 33 | Camp.AskColonist | `Source/ColonyCouncil/Camps/CampAsks.cs:78` | 798,223 | LINQ 100% |
| 34 | PlaySpots.Take | `Source/ColonyCouncil/PlayOut/PlaySpots.cs:479` | 762,460 | LINQ 92%, lister scan 8% |
| 35 | LifeChecks.Illness | `Source/ColonyCouncil/Dilemmas/LifeChecks.cs:20` | 699,895 |  |
| 36 | HappeningWorker_Setting.Ailment | `Source/ColonyCouncil/Happenings/Workers_Setting.cs:193` | 699,750 |  |
| 37 | Pasts.Of | `Source/ColonyCouncil/Happenings/HappeningHelpers.cs:426` | 679,801 | LINQ 100% |
| 38 | GroupView.EdgeTip | `Source/ColonyCouncil/Social/Groups/GroupView.cs:203` | 671,741 | Translate 86%, string 11%, new collection 4% |
| 39 | Persona.Words | `Source/ColonyCouncil/Persona/PersonaTracker.cs:332` | 615,763 | LINQ 54%, Translate 46% |
| 40 | TextUtil.JoinAnd | `Source/ColonyCouncil/Tone.cs:218` | 608,881 | Translate 46%, string 38%, LINQ 15% |

## What runs inside loops, by kind (the top entry points)

For each of the top 15 entry points, the costly calls inside loops in the methods it reaches most, with the line.

- **AilmentTracker.GameComponentTick → AilmentTracker.Slice**
  - Avail.CanBuild (`Source/ColonyCouncil/Avail.cs:83`): 351,600,325 a run; in loops: lister scan ×1 (lines 91)
  - AdvisorWorker_WinterStores.Pemmican (`Source/ColonyCouncil/Advisor/AdvisorSignals5.cs:433`): 36,606,799 a run; in loops: LINQ ×1 (lines 437); lister scan ×1 (lines 437)
  - Avail.NeedsPower (`Source/ColonyCouncil/Avail.cs:132`): 31,963,666 a run; nothing costly in loops
  - RoomUtil.HasFreezer (`Source/ColonyCouncil/RoomUtil.cs:108`): 2,517,227 a run; in loops: room query ×4 (lines 121, 122, 130, 131)
- **MainTabWindow_Council.DoWindowContents**
  - DefensePlanner.PieceStateAt (`Source/ColonyCouncil/Defense/DefensePlanner.cs:240`): 1,654,740 a run; in loops: Translate ×4 (lines 248, 285, 297, 312)
  - DefensePlanner.Warnings (`Source/ColonyCouncil/Defense/DefensePlanner.cs:388`): 136,850 a run; in loops: Translate ×4 (lines 394, 395, 396, 416); LINQ ×1 (lines 414)
  - TemplateLayout.RootFor (`Source/ColonyCouncil/Building/TemplateLayout.cs:125`): 82,158 a run; nothing costly in loops
  - DefensePlanner.Plan (`Source/ColonyCouncil/Defense/DefensePlanner.cs:131`): 59,696 a run; in loops: Translate ×1 (lines 161); lister scan ×1 (lines 227)
- **PlayOutTracker.GameComponentTick → PlayOutTracker.Step**
  - HerdHome.Destination (`Source/ColonyCouncil/Animals/HerdHome.cs:85`): 443,600 a run; in loops: LINQ ×2 (lines 104, 106); reach or path ×2 (lines 107)
  - PlaySpots.Ok (`Source/ColonyCouncil/PlayOut/PlaySpots.cs:327`): 12,205 a run; nothing costly in loops
  - PlaySpots.Take (`Source/ColonyCouncil/PlayOut/PlaySpots.cs:479`): 6,354 a run; nothing costly in loops
  - PlaySpots.FreeCellNear (`Source/ColonyCouncil/PlayOut/PlaySpots.cs:344`): 3,031 a run; nothing costly in loops
- **Dialog_Camp.DoWindowContents**
  - LetterTags.Who (`Source/ColonyCouncil/Dilemmas/DilemmaChoices.cs:186`): 1,363 a run; in loops: string ×2 (lines 189)
  - LineTags.Match (`Source/ColonyCouncil/Lean/LineTags.cs:126`): 749 a run; nothing costly in loops
  - CampAnswers.Silver (`Source/ColonyCouncil/Camps/CampAnswers.cs:38`): 547 a run; nothing costly in loops
  - Camp.AskColonist (`Source/ColonyCouncil/Camps/CampAsks.cs:78`): 479 a run; nothing costly in loops
- **Window_FloorPicker.DoWindowContents**
  - Avail.HaveOrCanGet (`Source/ColonyCouncil/Avail.cs:195`): 868 a run; nothing costly in loops
  - Avail.CanBuild (`Source/ColonyCouncil/Avail.cs:83`): 333 a run; in loops: lister scan ×1 (lines 91)
  - Avail.BestStuff (`Source/ColonyCouncil/Avail.cs:145`): 294 a run; in loops: lister scan ×1 (lines 154)
  - Window_FloorPicker.Tip (`Source/ColonyCouncil/Building/Window_FloorPicker.cs:433`): 279 a run; in loops: Translate ×1 (lines 448)
- **Window_DefensePlanner.DoWindowContents**
  - DefensePlanner.PieceStateAt (`Source/ColonyCouncil/Defense/DefensePlanner.cs:240`): 754 a run; in loops: Translate ×4 (lines 248, 285, 297, 312)
  - DefenseSurvey.Direction (`Source/ColonyCouncil/Defense/DefenseSurvey.cs:494`): 431 a run; nothing costly in loops
  - Window_DefensePlanner.DoWindowContents (`Source/ColonyCouncil/Defense/Window_DefensePlanner.cs:90`): 305 a run; in loops: string ×12 (lines 127, 179, 183, 188, 190, 223); LINQ ×5 (lines 177, 178, 192); Translate ×5 (lines 179, 183, 194, 223, 224)
  - DefenseLineReview.AutoName (`Source/ColonyCouncil/Defense/DefenseLineReview.cs:188`): 185 a run; nothing costly in loops
- **Window_RoomPlanner.DoWindowContents**
  - RoomTemplateDef.ThingInGroup (`Source/ColonyCouncil/Building/RoomTemplateDef.cs:726`): 247 a run; nothing costly in loops
  - Window_RoomPlanner.DoWindowContents (`Source/ColonyCouncil/Building/Window_RoomPlanner.cs:108`): 196 a run; in loops: LINQ ×4 (lines 223, 225, 231); string ×3 (lines 226, 284, 285); Translate ×3 (lines 233, 284, 285)
  - Avail.HaveOrCanGet (`Source/ColonyCouncil/Avail.cs:195`): 99.0 a run; nothing costly in loops
  - Window_RoomPlanner.StyleRow (`Source/ColonyCouncil/Building/Window_RoomPlanner.cs:346`): 90.6 a run; in loops: string ×3 (lines 357, 363); Translate ×2 (lines 357, 363)
- **Patch_AilmentDiet.Postfix**
  - Avail.CanBuild (`Source/ColonyCouncil/Avail.cs:83`): 15,850 a run; in loops: lister scan ×1 (lines 91)
  - AdvisorWorker_WinterStores.Pemmican (`Source/ColonyCouncil/Advisor/AdvisorSignals5.cs:433`): 1,650 a run; in loops: LINQ ×1 (lines 437); lister scan ×1 (lines 437)
  - Avail.NeedsPower (`Source/ColonyCouncil/Avail.cs:132`): 1,441 a run; nothing costly in loops
  - RoomUtil.HasFreezer (`Source/ColonyCouncil/RoomUtil.cs:108`): 113 a run; in loops: room query ×4 (lines 121, 122, 130, 131)
- **MainTabWindow_Rooms.DoWindowContents**
  - WallLights.IsAttached (`Source/ColonyCouncil/Building/WallLights.cs:21`): 138 a run; nothing costly in loops
  - MainTabWindow_Rooms.BuildLayout (`Source/ColonyCouncil/UI/MainTabWindow_Rooms.cs:226`): 119 a run; in loops: LINQ ×3 (lines 234); new collection ×1 (lines 228)
  - TemplateLayout.RootFor (`Source/ColonyCouncil/Building/TemplateLayout.cs:125`): 69.9 a run; nothing costly in loops
  - RoomTemplateDef.Icon (`Source/ColonyCouncil/Building/RoomTemplateDef.cs:740`): 68.4 a run; in loops: def by name ×1 (lines 746)
- **DefenseOverlay.MapComponentOnGUI**
  - DefenseSurvey.Direction (`Source/ColonyCouncil/Defense/DefenseSurvey.cs:494`): 430 a run; nothing costly in loops
  - DefenseLineReview.AutoName (`Source/ColonyCouncil/Defense/DefenseLineReview.cs:188`): 184 a run; nothing costly in loops
  - DefenseLineReview.NameOf (`Source/ColonyCouncil/Defense/DefenseLineReview.cs:178`): 6.10 a run; nothing costly in loops
  - DefenseLines.Get (`Source/ColonyCouncil/Defense/DefenseLines.cs:25`): 2.60 a run; nothing costly in loops
- **LordJob_PlayOut.LordJobTick → LordJob_PlayOut.Serve**
  - Beliefs.Bad (`Source/ColonyCouncil/Wishes/Beliefs.cs:175`): 5,769 a run; nothing costly in loops
  - Beliefs.Disapproves (`Source/ColonyCouncil/Wishes/Beliefs.cs:150`): 417 a run; nothing costly in loops
  - PlaySpots.BestMeal (`Source/ColonyCouncil/PlayOut/PlaySpots.cs:625`): 386 a run; in loops: reach or path ×2 (lines 634)
  - Beliefs.BadMemoryFrom (`Source/ColonyCouncil/Wishes/Beliefs.cs:283`): 96.0 a run; nothing costly in loops
- **ITab_Pawn_Mood.FillTab**
  - MoodView.DrawButtons (`Source/ColonyCouncil/Moods/MoodView.cs:210`): 252 a run; nothing costly in loops
  - MainTabWindow_Council.TabName (`Source/ColonyCouncil/UI/MainTabWindow_Council.cs:472`): 76.0 a run; nothing costly in loops
  - TextUtil.ShortSpeech (`Source/ColonyCouncil/Tone.cs:180`): 40.8 a run; nothing costly in loops
  - UiKit.FoldTitle (`Source/ColonyCouncil/UI/UiKit.cs:404`): 18.0 a run; nothing costly in loops
- **Patch_GoodTimesFade.Postfix**
  - MarkDefs.Build (`Source/ColonyCouncil/Persona/MarksDefs.cs:116`): 889 a run; in loops: new collection ×2 (lines 124, 132); def by name ×1 (lines 130)
  - TraitChanges.Build (`Source/ColonyCouncil/Persona/TraitChanges.cs:268`): 145 a run; in loops: def by name ×4 (lines 286, 287, 288, 297); new collection ×2 (lines 283, 290)
  - TraitTally.Split (`Source/ColonyCouncil/Persona/TraitChangeDefs.cs:64`): 69.1 a run; nothing costly in loops
  - MarksMath.TryParseWhen (`Source/ColonyCouncil/Persona/MarksMath.cs:71`): 59.9 a run; in loops: string ×6 (lines 82, 89, 95)
- **CouncilGameComponent.GameComponentTick → CouncilGameComponent.HourlyWishes**
  - WallLights.IsAttached (`Source/ColonyCouncil/Building/WallLights.cs:21`): 167,566 a run; nothing costly in loops
  - Beliefs.Bad (`Source/ColonyCouncil/Wishes/Beliefs.cs:175`): 94,597 a run; nothing costly in loops
  - SocialWeb.BuildCliques (`Source/ColonyCouncil/Social/SocialWeb.cs:674`): 25,271 a run; in loops: LINQ ×8 (lines 680, 683, 684, 686); string ×1 (lines 689); Translate ×1 (lines 689); new collection ×1 (lines 685)
  - WallLights.SideTaken (`Source/ColonyCouncil/Building/WallLights.cs:69`): 14,313 a run; nothing costly in loops
- **ITab_Pawn_Story.FillTab**
  - UiKit.FoldTitle (`Source/ColonyCouncil/UI/UiKit.cs:404`): 72.0 a run; nothing costly in loops
  - StoryView.Draw (`Source/ColonyCouncil/UI/ITab_Pawn_Story.cs:83`): 60.7 a run; in loops: Translate ×1 (lines 123)
  - CharacterView.DrawSection (`Source/ColonyCouncil/Persona/CharacterView.cs:136`): 24.0 a run; nothing costly in loops
  - Persona.Word (`Source/ColonyCouncil/Persona/PersonaTracker.cs:324`): 14.5 a run; nothing costly in loops

