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

Entry points found: 241. Estimated total: 16,742,972 units an hour (before: 245,781,320).

"Before" is the code before this pass (the bug-fix merge, d86f843), read by this same script. An entry point that is gone or new shows "–".

| Kind | Units an hour | Share | Before |
|---|---|---|---|
| frame | 13,107,812 | 78% | 235,276,145 |
| tick | 3,541,960 | 21% | 10,408,814 |
| mood | 33,850 | 0% | 33,858 |
| patch | 28,473 | 0% | 31,548 |
| job | 22,408 | 0% | 22,408 |
| talk | 3,440 | 0% | 3,452 |
| job tick | 2,250 | 0% | 2,250 |
| stat | 1,800 | 0% | 1,800 |
| alert | 740 | 0% | 740 |
| event | 241 | 0% | 306 |

## Entry points, by estimated cost (top 40)

Runs an hour × work a run = units an hour. "Work" lists the biggest kinds of work inside (inclusive of what it calls).

| # | Entry point | Where | Runs an hour | Work a run | Units an hour | Work | Before |
|---|---|---|---|---|---|---|---|
| 1 | MainTabWindow_Council.DoWindowContents | `Source/ColonyCouncil/UI/MainTabWindow_Council.cs:185` | 1,666 | 2,566 | 4,275,461 | string 73%, Translate 24%, LINQ 3%, new collection 0% | 74,096,261 |
| 2 | Window_FloorPicker.DoWindowContents | `Source/ColonyCouncil/Building/Window_FloorPicker.cs:190` | 1,666 | 2,341 | 3,900,612 | lister scan 70%, Translate 19%, LINQ 8%, new collection 2% | 154,134,439 |
| 3 | Window_RoomPlanner.DoWindowContents | `Source/ColonyCouncil/Building/Window_RoomPlanner.cs:108` | 1,666 | 946 | 1,576,352 | Translate 49%, lister scan 31%, string 11%, LINQ 7% | 2,433,208 |
| 4 | MainTabWindow_Rooms.DoWindowContents | `Source/ColonyCouncil/UI/MainTabWindow_Rooms.cs:60` | 1,666 | 786 | 1,308,965 | LINQ 39%, Translate 26%, lister scan 21%, string 6% | 2,533,563 |
| 5 | LordJob_PlayOut.LordJobTick → LordJob_PlayOut.Serve (when due) | `Source/ColonyCouncil/PlayOut/PlayOutLord.cs:174` | 125 | 6,805 | 850,609 | reach or path 85%, LINQ 6%, Translate 4%, new collection 2% | 1,572,338 |
| 6 | ITab_Pawn_Mood.FillTab | `Source/ColonyCouncil/Moods/ITab_Pawn_Mood.cs:57` | 1,666 | 488 | 812,652 | Translate 83%, string 17%, LINQ 0%, new collection 0% | 835,906 |
| 7 | AilmentTracker.GameComponentTick → AilmentTracker.Slice (a gate kept in a variable) | `Source/ColonyCouncil/Ailments/AilmentTracker.cs:191` | 10 | 60,419 | 604,195 | LINQ 33%, Translate 23%, string 22%, new collection 8% | – |
| 8 | Dialog_Camp.DoWindowContents | `Source/ColonyCouncil/Camps/CampUI.cs:120` | 1,666 | 290 | 482,375 | Translate 87%, string 9%, LINQ 4%, lister scan 0% | 482,376 |
| 9 | CouncilGameComponent.GameComponentTick → CouncilGameComponent.HourlyWishes (hourly) | `Source/ColonyCouncil/CouncilGameComponent.cs:315` | 1 | 394,720 | 394,720 | LINQ 53%, pawn scan 22%, Translate 11%, lister scan 6% | 416,916 |
| 10 | StoriesComponent.GameComponentTick → Missions.Tick (every 250 ticks) | `Source/ColonyCouncil/Missions/Missions.cs:439` | 10 | 35,621 | 356,210 | LINQ 64%, Translate 30%, def by name 2%, string 1% | 895,073 |
| 11 | Dialog_News.DoWindowContents | `Source/ColonyCouncil/UI/News.cs:404` | 1,666 | 190 | 316,748 | Translate 100%, string 0% | 316,748 |
| 12 | VisualTouches.GameComponentTick → VisualTouches.Hourly (every 30 ticks) | `Source/ColonyCouncil/Touches/VisualTouches.cs:494` | 83.3 | 3,082 | 256,868 | room query 21%, new collection 19%, string 14%, LINQ 14% | 687,543 |
| 13 | VisualTouches.GameComponentTick → WeatherTalk.Hourly (every 30 ticks) | `Source/ColonyCouncil/Immersion/WeatherTalk.cs:58` | 83.3 | 3,015 | 251,213 | room query 22%, new collection 19%, LINQ 14%, string 13% | 680,190 |
| 14 | ITab_Pawn_Story.FillTab | `Source/ColonyCouncil/UI/ITab_Pawn_Story.cs:41` | 1,666 | 137 | 228,078 | Translate 100%, LINQ 0% | 228,078 |
| 15 | SetbackTracker.GameComponentTick → SetbackTracker.Refresh (every 60 ticks) | `Source/ColonyCouncil/Setbacks/SetbackTracker.cs:248` | 41.7 | 5,159 | 214,977 | Translate 61%, LINQ 23%, string 14%, new collection 3% | 215,145 |
| 16 | Dialog_CalendarDay.DoWindowContents | `Source/ColonyCouncil/Calendar/CalendarPanel.cs:295` | 1,666 | 101 | 167,936 | Translate 63%, string 37% | 176,933 |
| 17 | AilmentTracker.GameComponentTick → AilmentTracker.ProcessEnded (only when something is queued) | `Source/ColonyCouncil/Ailments/AilmentTracker.cs:874` | 25 | 5,786 | 144,659 | LINQ 23%, Translate 22%, new collection 15%, string 12% | 287,529 |
| 18 | ArcTracker.GameComponentTick → ArcTracker.Hourly (hourly, passed along) | `Source/ColonyCouncil/Arcs/ArcTracker.cs:161` | 1 | 136,797 | 136,797 | string 31%, Translate 30%, LINQ 25%, pawn scan 6% | 533,235 |
| 19 | HeartTracker.GameComponentTick → HeartTracker.Slice (a gate kept in a variable) | `Source/ColonyCouncil/Heart/HeartTracker.cs:154` | 10 | 10,255 | 102,548 | Translate 65%, LINQ 26%, string 4%, pawn scan 2% | 102,777 |
| 20 | LordJob_PlayOut.LordJobTick → ToastDrinks.Pour (when due) | `Source/ColonyCouncil/Integrations/ToastDrinks.cs:28` | 125 | 544 | 67,998 | reach or path 87%, stat 7%, LINQ 6%, new collection 1% | 324,200 |
| 21 | WorldSituations.GameComponentTick → WorldSituations.Hourly (hourly) | `Source/ColonyCouncil/World/WorldSituations.cs:127` | 1 | 26,444 | 26,444 | LINQ 34%, Translate 27%, string 23%, reach or path 6% | 131,801 |
| 22 | CouncilGameComponent.GameComponentTick → AmbitionTracker.MeetingTick (every 60 ticks) | `Source/ColonyCouncil/Ambitions/Ambitions.cs:466` | 41.7 | 606 | 25,250 | reach or path 58%, Translate 34%, string 3%, room query 2% | 260,893 |
| 23 | HeartTracker.GameComponentTick → HeartLogic.Peek (once after loading or building, filtered) | `Source/ColonyCouncil/Heart/HeartLogic.cs:67` | 1/2 | 49,725 | 24,862 | Translate 64%, LINQ 29%, string 3%, pawn scan 2% | 24,919 |
| 24 | ThoughtWorker_Pressure.CurrentStateInternal | `Source/ColonyCouncil/Pressures/PressureThoughts.cs:82` | 500 | 35.2 | 17,588 | LINQ 61%, new collection 29%, def by name 10% | 17,588 |
| 25 | GameCondition_Hail.GameConditionTick → GameCondition_Hail.Crops (every 250 ticks) | `Source/ColonyCouncil/Happenings/Workers_Nature.cs:142` | 10 | 1,453 | 14,532 | new collection 100% | 14,532 |
| 26 | Dialog_SelfCheck.DoWindowContents | `Source/ColonyCouncil/UI/SelfCheck.cs:315` | 1,666 | 8.71 | 14,517 | LINQ 55%, string 45% | 14,517 |
| 27 | CouncilGameComponent.GameComponentTick → CouncilGameComponent.HourlyLife (hourly) | `Source/ColonyCouncil/CouncilGameComponent.cs:327` | 1 | 13,802 | 13,802 | Translate 27%, LINQ 22%, room query 19%, string 13% | 3,174,650 |
| 28 | JoyGiver_Habit.TryGiveJob | `Source/ColonyCouncil/Immersion/SmallHabits.cs:309` | 120 | 99.9 | 11,991 | reach or path 86%, room query 12%, LINQ 1%, lister scan 0% | 11,991 |
| 29 | CouncilGameComponent.GameComponentTick → CouncilGameComponent.HourlyPeople (hourly) | `Source/ColonyCouncil/CouncilGameComponent.cs:337` | 1 | 11,968 | 11,968 | room query 30%, LINQ 28%, Translate 22%, string 11% | 15,920 |
| 30 | Dialog_Journal.DoWindowContents | `Source/ColonyCouncil/Journal.cs:209` | 1,666 | 6.60 | 10,996 | Translate 100% | 10,996 |
| 31 | CouncilGameComponent.GameComponentTick → CouncilGameComponent.DailySecond (hourly, first hour of the day, passed along) | `Source/ColonyCouncil/CouncilDynamics.cs:656` | 1/24 | 260,928 | 10,872 | LINQ 65%, Translate 16%, pawn scan 6%, string 6% | 13,402 |
| 32 | Dialog_NameRoom.DoWindowContents | `Source/ColonyCouncil/Building/CustomRooms.cs:434` | 1,666 | 6.00 | 9,997 | Translate 100% | 9,997 |
| 33 | CouncilGameComponent.GameComponentTick → CouncilGameComponent.HourlyEvents (hourly) | `Source/ColonyCouncil/CouncilGameComponent.cs:351` | 1 | 9,152 | 9,152 | LINQ 58%, Translate 12%, reach or path 11%, string 8% | 81,604 |
| 34 | ThoughtWorker_ColonyWays.CurrentStateInternal | `Source/ColonyCouncil/Character/Character.cs:351` | 500 | 17.9 | 8,941 | LINQ 48%, Translate 33%, new collection 11%, string 6% | 8,941 |
| 35 | JoyGiver_VisitCamp.TryGiveJob | `Source/ColonyCouncil/Camps/CampWork.cs:484` | 120 | 60.0 | 7,200 | reach or path 100% | 7,200 |
| 36 | SetbackTracker.GameComponentTick → SetbackTracker.Hourly (hourly, passed along) | `Source/ColonyCouncil/Setbacks/SetbackTracker.cs:227` | 1 | 5,755 | 5,755 | Translate 59%, LINQ 21%, string 15%, new collection 3% | 5,815 |
| 37 | Patch_HeartInspect.Postfix | `Source/ColonyCouncil/Patches/Patch_HeartInspect.cs:16` | 833 | 5.14 | 4,285 | string 56%, Translate 29%, LINQ 13%, pawn scan 1% | 4,293 |
| 38 | Patch_NeedsTabMoodHints.TipRegionGetter | `Source/ColonyCouncil/Patches/Patch_NeedsTabMoodHints.cs:75` | 41,650 | 0.10 | 4,282 | string 98%, Translate 2% | 4,282 |
| 39 | Patch_NeedsTabMoodHints.TipRegion | `Source/ColonyCouncil/Patches/Patch_NeedsTabMoodHints.cs:80` | 41,650 | 0.10 | 4,282 | string 98%, Translate 2% | 4,282 |
| 40 | Patch_WindGuard.Postfix | `Source/ColonyCouncil/Patches/Patch_WindGuard.cs:65` | 2,500 | 1.70 | 4,254 | string 100% | 4,254 |

Left the list since the earlier run: AilmentTracker.GameComponentTick → AilmentTracker.Hourly (942,655).

## Where the cost lands: methods by their own work (top 40)

Each method's own work (not what it calls), times how often the entry points above reach it.

| # | Method | Where | Units an hour | Work | Before |
|---|---|---|---|---|---|
| 1 | AlmanacPanel.Inline | `Source/ColonyCouncil/Almanac/AlmanacPanel.cs:684` | 2,020,244 | string 100% | 2,020,244 |
| 2 | Avail.HaveOrCanGet | `Source/ColonyCouncil/Avail.cs:195` | 1,613,340 | lister scan 100% | 62,841,646 |
| 3 | Beliefs.Bad | `Source/ColonyCouncil/Wishes/Beliefs.cs:175` | 893,392 |  | 1,475,498 |
| 4 | Avail.CanBuild | `Source/ColonyCouncil/Avail.cs:83` | 777,126 | lister scan 100% | 33,124,594 |
| 5 | Avail.BestStuff | `Source/ColonyCouncil/Avail.cs:145` | 546,096 | lister scan 100% | 21,269,523 |
| 6 | Window_FloorPicker.Tip | `Source/ColonyCouncil/Building/Window_FloorPicker.cs:433` | 464,614 | Translate 88%, LINQ 7%, string 4%, new collection 1% | 13,938,422 |
| 7 | MoodView.DrawButtons | `Source/ColonyCouncil/Moods/MoodView.cs:210` | 441,928 | Translate 86%, string 14% | 441,928 |
| 8 | WallLights.IsAttached | `Source/ColonyCouncil/Building/WallLights.cs:21` | 439,829 |  | 439,829 |
| 9 | RoomTemplateDef.ThingInGroup | `Source/ColonyCouncil/Building/RoomTemplateDef.cs:726` | 411,840 | LINQ 100% | 411,840 |
| 10 | RoomTemplateDef.FloorSubstitute | `Source/ColonyCouncil/Building/RoomTemplateDef.cs:828` | 398,299 | LINQ 100%, def scan 0% | 15,515,773 |
| 11 | Window_RoomPlanner.DoWindowContents | `Source/ColonyCouncil/Building/Window_RoomPlanner.cs:108` | 325,975 | Translate 78%, LINQ 11%, string 10%, room query 1% | 325,975 |
| 12 | Window_FloorPicker.DrawFamily | `Source/ColonyCouncil/Building/Window_FloorPicker.cs:314` | 305,878 | Translate 69%, LINQ 24%, string 8% | 305,878 |
| 13 | Flavor.TagsFor | `Source/ColonyCouncil/Flavor.cs:28` | 288,636 | room query 43%, string 22%, new collection 22%, pawn scan 13% | 1,088,081 |
| 14 | Dialog_News.Line | `Source/ColonyCouncil/UI/News.cs:465` | 287,885 | Translate 100% | 287,885 |
| 15 | AlmanacPanel.Layout | `Source/ColonyCouncil/Almanac/AlmanacPanel.cs:630` | 274,206 |  | 274,206 |
| 16 | StandingTracker.For | `Source/ColonyCouncil/Standing.cs:128` | 266,255 |  | 864,494 |
| 17 | UiKit.DrawInCircle | `Source/ColonyCouncil/UI/UiKit.cs:672` | 227,810 |  | 829,590 |
| 18 | Dialog_Camp.DoWindowContents | `Source/ColonyCouncil/Camps/CampUI.cs:120` | 219,413 | Translate 90%, string 8%, LINQ 2% | 219,413 |
| 19 | LineTags.EraFits | `Source/ColonyCouncil/Lean/LineTags.cs:150` | 203,463 |  | – |
| 20 | MainTabWindow_Rooms.BuildLayout | `Source/ColonyCouncil/UI/MainTabWindow_Rooms.cs:226` | 198,670 | LINQ 98%, new collection 2% | 198,670 |
| 21 | HappeningWorker_MissionShrine.Shrine | `Source/ColonyCouncil/Missions/Workers_Missions.cs:585` | 168,365 | LINQ 100%, lister scan 0% | 168,365 |
| 22 | Dialog_CalendarDay.DoWindowContents | `Source/ColonyCouncil/Calendar/CalendarPanel.cs:295` | 164,601 | Translate 64%, string 36% | 164,601 |
| 23 | UiKit.FoldTitle | `Source/ColonyCouncil/UI/UiKit.cs:402` | 156,253 | Translate 100% | 156,253 |
| 24 | Window_RoomPlanner.StyleRow | `Source/ColonyCouncil/Building/Window_RoomPlanner.cs:346` | 150,940 | Translate 80%, string 20% | 150,940 |
| 25 | JournalTracker.WriteTagged | `Source/ColonyCouncil/Journal.cs:81` | 144,937 | LINQ 37%, Translate 37%, string 13%, new collection 12% | 81,177 |
| 26 | AlmanacPanel.Table | `Source/ColonyCouncil/Almanac/AlmanacPanel.cs:688` | 137,419 | LINQ 100% | 137,419 |
| 27 | AlmanacPanel.Cards | `Source/ColonyCouncil/Almanac/AlmanacPanel.cs:715` | 136,787 |  | 136,787 |
| 28 | MainTabWindow_Council.TabName | `Source/ColonyCouncil/UI/MainTabWindow_Council.cs:468` | 136,611 | Translate 95%, string 5% | 136,600 |
| 29 | WishWorker_Prosthetic.BuildInstalls | `Source/ColonyCouncil/Wishes/WishWorkers_Life.cs:171` | 123,885 | LINQ 73%, new collection 25%, def scan 2% | – |
| 30 | TemplateLayout.RootFor | `Source/ColonyCouncil/Building/TemplateLayout.cs:125` | 116,792 |  | 116,792 |
| 31 | RoomTemplateDef.Icon | `Source/ColonyCouncil/Building/RoomTemplateDef.cs:740` | 114,334 | def by name 79%, LINQ 21% | 114,334 |
| 32 | Flavor.PickText | `Source/ColonyCouncil/Flavor.cs:378` | 111,664 | LINQ 100% | 36,668 |
| 33 | StoryView.Draw | `Source/ColonyCouncil/UI/ITab_Pawn_Story.cs:83` | 106,451 | Translate 100%, LINQ 0% | 106,451 |
| 34 | ModPeople.Build | `Source/ColonyCouncil/Integrations/ModPeople.cs:123` | 106,432 | LINQ 85%, new collection 15% | 62,430 |
| 35 | Flavor.FromPool | `Source/ColonyCouncil/Flavor.cs:454` | 102,051 | def by name 100% | 84,517 |
| 36 | LightMath.Relocate | `Source/ColonyCouncil/Building/LightMath.cs:453` | 99,215 | new collection 100% | 99,215 |
| 37 | FeelingNow.Sort | `Source/ColonyCouncil/Heart/HeartData.cs:48` | 96,421 |  | 96,421 |
| 38 | JournalTracker.For | `Source/ColonyCouncil/Journal.cs:61` | 94,847 | LINQ 100% | 53,404 |
| 39 | SetbackTracker.CaravanTrouble | `Source/ColonyCouncil/Setbacks/SetbackTracker.cs:392` | 93,867 | Translate 86%, string 14% | 93,867 |
| 40 | StoryChains.HasTag | `Source/ColonyCouncil/Happenings/Workers_Chains2.cs:463` | 93,213 | LINQ 100% | 93,213 |

## What runs inside loops, by kind (the top entry points)

For each of the top 15 entry points, the costly calls inside loops in the methods it reaches most, with the line.

- **MainTabWindow_Council.DoWindowContents**
  - AlmanacPanel.Inline (`Source/ColonyCouncil/Almanac/AlmanacPanel.cs:684`): 1,213 a run; nothing costly in loops
  - AlmanacPanel.Layout (`Source/ColonyCouncil/Almanac/AlmanacPanel.cs:630`): 165 a run; nothing costly in loops
  - UiKit.DrawInCircle (`Source/ColonyCouncil/UI/UiKit.cs:672`): 126 a run; nothing costly in loops
  - StandingTracker.For (`Source/ColonyCouncil/Standing.cs:128`): 123 a run; nothing costly in loops
- **Window_FloorPicker.DoWindowContents**
  - Avail.HaveOrCanGet (`Source/ColonyCouncil/Avail.cs:195`): 868 a run; nothing costly in loops
  - Avail.CanBuild (`Source/ColonyCouncil/Avail.cs:83`): 333 a run; in loops: lister scan ×1 (lines 91)
  - Avail.BestStuff (`Source/ColonyCouncil/Avail.cs:145`): 294 a run; in loops: lister scan ×1 (lines 154)
  - Window_FloorPicker.Tip (`Source/ColonyCouncil/Building/Window_FloorPicker.cs:433`): 279 a run; in loops: Translate ×1 (lines 448)
- **Window_RoomPlanner.DoWindowContents**
  - RoomTemplateDef.ThingInGroup (`Source/ColonyCouncil/Building/RoomTemplateDef.cs:726`): 247 a run; nothing costly in loops
  - Window_RoomPlanner.DoWindowContents (`Source/ColonyCouncil/Building/Window_RoomPlanner.cs:108`): 196 a run; in loops: LINQ ×4 (lines 223, 225, 231); string ×3 (lines 226, 284, 285); Translate ×3 (lines 233, 284, 285)
  - Avail.HaveOrCanGet (`Source/ColonyCouncil/Avail.cs:195`): 99.0 a run; nothing costly in loops
  - Window_RoomPlanner.StyleRow (`Source/ColonyCouncil/Building/Window_RoomPlanner.cs:346`): 90.6 a run; in loops: string ×3 (lines 357, 363); Translate ×2 (lines 357, 363)
- **MainTabWindow_Rooms.DoWindowContents**
  - WallLights.IsAttached (`Source/ColonyCouncil/Building/WallLights.cs:21`): 138 a run; nothing costly in loops
  - MainTabWindow_Rooms.BuildLayout (`Source/ColonyCouncil/UI/MainTabWindow_Rooms.cs:226`): 119 a run; in loops: LINQ ×3 (lines 234); new collection ×1 (lines 228)
  - TemplateLayout.RootFor (`Source/ColonyCouncil/Building/TemplateLayout.cs:125`): 69.9 a run; nothing costly in loops
  - RoomTemplateDef.Icon (`Source/ColonyCouncil/Building/RoomTemplateDef.cs:740`): 68.4 a run; in loops: def by name ×1 (lines 746)
- **LordJob_PlayOut.LordJobTick → LordJob_PlayOut.Serve**
  - Beliefs.Bad (`Source/ColonyCouncil/Wishes/Beliefs.cs:175`): 5,769 a run; nothing costly in loops
  - Beliefs.Disapproves (`Source/ColonyCouncil/Wishes/Beliefs.cs:150`): 417 a run; nothing costly in loops
  - PlaySpots.BestMeal (`Source/ColonyCouncil/PlayOut/PlaySpots.cs:456`): 386 a run; in loops: reach or path ×2 (lines 465)
  - Beliefs.BadMemoryFrom (`Source/ColonyCouncil/Wishes/Beliefs.cs:283`): 96.0 a run; nothing costly in loops
- **ITab_Pawn_Mood.FillTab**
  - MoodView.DrawButtons (`Source/ColonyCouncil/Moods/MoodView.cs:210`): 252 a run; nothing costly in loops
  - MainTabWindow_Council.TabName (`Source/ColonyCouncil/UI/MainTabWindow_Council.cs:468`): 76.0 a run; nothing costly in loops
  - TextUtil.ShortSpeech (`Source/ColonyCouncil/Tone.cs:176`): 40.8 a run; nothing costly in loops
  - UiKit.FoldTitle (`Source/ColonyCouncil/UI/UiKit.cs:402`): 18.0 a run; nothing costly in loops
- **AilmentTracker.GameComponentTick → AilmentTracker.Slice**
  - LineTags.EraFits (`Source/ColonyCouncil/Lean/LineTags.cs:150`): 12,582 a run; nothing costly in loops
  - Flavor.PickText (`Source/ColonyCouncil/Flavor.cs:378`): 8,999 a run; nothing costly in loops
  - JournalTracker.WriteTagged (`Source/ColonyCouncil/Journal.cs:81`): 7,689 a run; nothing costly in loops
  - Flavor.TagsFor (`Source/ColonyCouncil/Flavor.cs:28`): 5,959 a run; nothing costly in loops
- **Dialog_Camp.DoWindowContents**
  - Dialog_Camp.DoWindowContents (`Source/ColonyCouncil/Camps/CampUI.cs:120`): 132 a run; in loops: string ×1 (lines 184); Translate ×1 (lines 184)
  - CampAnswers.Silver (`Source/ColonyCouncil/Camps/CampAnswers.cs:38`): 42.2 a run; nothing costly in loops
  - Dialog_Camp.NotEnough (`Source/ColonyCouncil/Camps/CampUI.cs:227`): 42.0 a run; nothing costly in loops
  - Dialog_Camp.Check (`Source/ColonyCouncil/Camps/CampUI.cs:230`): 14.0 a run; nothing costly in loops
- **CouncilGameComponent.GameComponentTick → CouncilGameComponent.HourlyWishes**
  - WallLights.IsAttached (`Source/ColonyCouncil/Building/WallLights.cs:21`): 194,024 a run; nothing costly in loops
  - Beliefs.Bad (`Source/ColonyCouncil/Wishes/Beliefs.cs:175`): 95,737 a run; nothing costly in loops
  - SocialWeb.BuildCliques (`Source/ColonyCouncil/Social/SocialWeb.cs:643`): 24,381 a run; in loops: LINQ ×8 (lines 647, 650, 651, 653); string ×1 (lines 656); Translate ×1 (lines 656); new collection ×1 (lines 652)
  - PlaySpots.Take (`Source/ColonyCouncil/PlayOut/PlaySpots.cs:310`): 19,125 a run; nothing costly in loops
- **StoriesComponent.GameComponentTick → Missions.Tick**
  - HappeningWorker_MissionShrine.Shrine (`Source/ColonyCouncil/Missions/Workers_Missions.cs:585`): 16,616 a run; in loops: LINQ ×1 (lines 592)
  - StoryChains.HasTag (`Source/ColonyCouncil/Happenings/Workers_Chains2.cs:463`): 9,295 a run; in loops: LINQ ×1 (lines 465)
  - Missions.Need (`Source/ColonyCouncil/Missions/Missions.cs:640`): 3,101 a run; nothing costly in loops
  - LineTags.Match (`Source/ColonyCouncil/Lean/LineTags.cs:126`): 2,165 a run; nothing costly in loops
- **Dialog_News.DoWindowContents**
  - Dialog_News.Line (`Source/ColonyCouncil/UI/News.cs:465`): 173 a run; nothing costly in loops
  - Dialog_News.DoWindowContents (`Source/ColonyCouncil/UI/News.cs:404`): 17.3 a run; in loops: string ×1 (lines 426)
  - News.Unread (`Source/ColonyCouncil/UI/News.cs:180`): 0.01 a run; nothing costly in loops
  - News.PutAllAway (`Source/ColonyCouncil/UI/News.cs:192`): 0.00 a run; nothing costly in loops
- **VisualTouches.GameComponentTick → VisualTouches.Hourly**
  - Flavor.TagsFor (`Source/ColonyCouncil/Flavor.cs:28`): 1,093 a run; nothing costly in loops
  - Flavor.FromPool (`Source/ColonyCouncil/Flavor.cs:454`): 387 a run; nothing costly in loops
  - WeatherTalk.Say (`Source/ColonyCouncil/Immersion/WeatherTalk.cs:179`): 386 a run; in loops: string ×1 (lines 188); Translate ×1 (lines 188)
  - Flavor.IsGrieving (`Source/ColonyCouncil/Flavor.cs:226`): 212 a run; nothing costly in loops
- **VisualTouches.GameComponentTick → WeatherTalk.Hourly**
  - Flavor.TagsFor (`Source/ColonyCouncil/Flavor.cs:28`): 1,089 a run; nothing costly in loops
  - Flavor.FromPool (`Source/ColonyCouncil/Flavor.cs:454`): 386 a run; nothing costly in loops
  - WeatherTalk.Say (`Source/ColonyCouncil/Immersion/WeatherTalk.cs:179`): 386 a run; in loops: string ×1 (lines 188); Translate ×1 (lines 188)
  - Flavor.IsGrieving (`Source/ColonyCouncil/Flavor.cs:226`): 211 a run; nothing costly in loops
- **ITab_Pawn_Story.FillTab**
  - UiKit.FoldTitle (`Source/ColonyCouncil/UI/UiKit.cs:402`): 72.0 a run; nothing costly in loops
  - StoryView.Draw (`Source/ColonyCouncil/UI/ITab_Pawn_Story.cs:83`): 60.7 a run; in loops: Translate ×1 (lines 118)
  - ITab_Pawn_Story.FillTab (`Source/ColonyCouncil/UI/ITab_Pawn_Story.cs:41`): 3.00 a run; nothing costly in loops
  - UiKit.DrawInCircle (`Source/ColonyCouncil/UI/UiKit.cs:672`): 0.60 a run; nothing costly in loops
- **SetbackTracker.GameComponentTick → SetbackTracker.Refresh**
  - SetbackTracker.CaravanTrouble (`Source/ColonyCouncil/Setbacks/SetbackTracker.cs:392`): 2,200 a run; in loops: string ×1 (lines 399); Translate ×1 (lines 399)
  - ModPeople.Build (`Source/ColonyCouncil/Integrations/ModPeople.cs:123`): 987 a run; in loops: LINQ ×7 (lines 134, 141, 146, 148, 149, 155); new collection ×4 (lines 137, 143, 151, 158)
  - FactionMemory.Peoples (`Source/ColonyCouncil/World/FactionMemory.cs:351`): 526 a run; in loops: LINQ ×1 (lines 354)
  - FactionMemory.Text (`Source/ColonyCouncil/World/FactionMemory.cs:367`): 456 a run; nothing costly in loops

