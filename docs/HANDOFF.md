# Handoff: start here

Read this first; it is the whole state. CLAUDE.md has the binding rules and the check commands. CHANGELOG.md (written
from `Defs/AlmanacArticleDefs/Almanac_WhatsNew.xml`) has every round in the player's words. The sections under
"Older history" at the bottom are kept for reference only.

## State (4 November 2026)

- Stage 2 round a review: fixes (the three releases 0.81.0, 0.88.0 and 0.89.0, diplomacy's base and favours, pacts and
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
