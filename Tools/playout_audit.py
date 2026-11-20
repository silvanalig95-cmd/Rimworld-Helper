#!/usr/bin/env python3
"""
Play-out audit: which answers say people do something on the map, but leave it to a mood?

Walks every answer in Defs/ (dilemma options, Rimfolk events and their story stages and missions, world events' beats
and their options, decrees, ambitions) and:

  * counts the answers that play out on the map (an effect of PlayOut/: a feast, a gathering, a bonfire, a toast, words,
    a visit, a lesson, a day off, a hunt, a work party, a walk together, a vigil, a search, a gift carried out, a thing
    made and handed over, rounds, the dead carried out, lanterns, a fire kept, a bolted door, an animal at heel), by
    source and by kind, for the README and the ROADMAP; and, on a line of their own, the stages of personal stories and
    the encounters that play out (not answers, so never candidates);
  * lists the answers whose words describe a physical activity (a meal, a vigil, a party, a visit, a lesson, a work
    party, a search, a funeral, a hunt, a procession, a gift carried out, a cleaning, a rebuilding...) and have no
    play-out, as candidates. Not every candidate fits: some happen far from the map ("half a day's walk out"), some are
    told about rather than done, some are refusals, some are done by the event's own code. Those read by hand and left
    are in the skip list (Tools/playout_skips.txt, one "DefName [where] why" a line): they're kept out of the
    candidates, --skip lists them with why, and one that has gone stale (the answer is gone, or now plays out) is
    named after the candidates, so the list stays true.

Usage: python3 Tools/playout_audit.py [--counts] [--candidates] [--skip] [--filter REGEX]
  (no flags: the counts and the candidates)
"""
import glob
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SKIPS = os.path.join(ROOT, "Tools", "playout_skips.txt")
SKIP_LINE = re.compile(r"^(\S+) \[([^\]]+)\]\s*(.*)$")

PLAY = {
    "DilemmaEffect_Gathering": "gathering",
    "DilemmaEffect_Feast": "feast",
    # The year's first supper (ColonyLife/FirstSupper.cs): a feast at which each says their aim for the year.
    "DilemmaEffect_FirstSupper": "feast",
    "DilemmaEffect_Bonfire": "bonfire",
    "DilemmaEffect_Toast": "toast",
    "DilemmaEffect_Words": "words",
    "DilemmaEffect_Visit": "visit",
    "DilemmaEffect_Lesson": "lesson",
    "DilemmaEffect_Rest": "rest",
    "DilemmaEffect_Hunt": "hunt",
    "DilemmaEffect_WorkParty": "work party",
    "DilemmaEffect_Procession": "walk together",
    "DilemmaEffect_Vigil": "vigil",
    "DilemmaEffect_Search": "search",
    "DilemmaEffect_CarryOut": "gift carried out",
    "DilemmaEffect_HuntParty": "hunt party",
    # Faith and the council (Dilemmas/DilemmaWorkers_Faith.cs): words said for a faith, and an evening of faith called.
    "DilemmaEffect_FaithWords": "words",
    "DilemmaEffect_FaithEvening": "evening of faith",
    # One thing or one round (PlayOut/OnMapPlays.cs): a thing made and carried over by hand (or set in someone's room),
    # a stash burned in the yard, a doctor's or a cook's rounds, the enemy dead carried out to a pyre or the tree line.
    "DilemmaEffect_Made": "handed over",
    "DilemmaEffect_BurnIt": "burned in yard",
    "DilemmaEffect_Rounds": "rounds",
    "DilemmaEffect_HaulDead": "dead carried out",
    # Kept on the map for a while (PlayOut/OnMapTimed.cs, OnMapRooms.cs): lanterns for a night, a fire in a cold room, a
    # bolted door, an animal at someone's heels, two rooms traded.
    "DilemmaEffect_Lanterns": "lanterns",
    "DilemmaEffect_KeepFire": "fire kept",
    "DilemmaEffect_BoltDoor": "door bolted",
    "DilemmaEffect_Follow": "at heel",
    # An animal's moment played out before it is told (Animals/AnimalScenes.cs): running to someone, the foot of their
    # bed, a meal, chase with a child, following them, comfort, a trick, the stores, sitting together, a feud's ambush.
    "DilemmaEffect_AnimalScene": "an animal's moment",
    "DilemmaEffect_SwapRooms": "rooms traded",
    # Also kept on the map for a while: smudge fires along the fields, a stone cut for each grave, a fenced patch sown,
    # the children's race run.
    "DilemmaEffect_SmudgeFires": "smudge fires",
    "DilemmaEffect_MarkGraves": "grave stones",
    "DilemmaEffect_SowPatch": "patch sown",
    "DilemmaEffect_Race": "race",
    # The game's own wedding ceremony, on the evening the council set (Social/WeddingPlans.cs), and what follows it.
    "DilemmaEffect_PlanWedding": "wedding",
    # Worship of the gods (Gods/Worship/WorshipCouncil.cs): words said at a god's place, a beast walked out and let go,
    # a lesser offering carried to the god's place.
    "DilemmaEffect_ShrineWords": "words",
    # The saints (Gods/Saints/SaintsCouncil.cs): words said at the fallen one's grave when the council names them a saint or
    # honours them.
    "DilemmaEffect_SaintWords": "words",
    "DilemmaEffect_GiveBeast": "beast given",
    "DilemmaEffect_LesserOffering": "offering carried",
    # More council questions of faith (Gods/Worship/WorshipCouncilMore.cs): the offering carried by the scoffer, the place
    # given to another god (someone walks out and says the words), the place in the way taken up, built round or taken down.
    "DilemmaEffect_ScofferCarries": "offering carried",
    "DilemmaEffect_ShrineGiven": "place given",
    "DilemmaEffect_ShrineInTheWay": "place moved",
    # The colony's own settings changed for a while and put back (Dilemmas/AnswerShifts.cs), and the game's own orders
    # (Dilemmas/DilemmaWorkers_Variety.cs): timetables shifted, work handed over, an operation on the medical tab, drug
    # bills suspended, a slave freed by the game's emancipation, self-tend or the best medicine for a patient.
    "DilemmaEffect_Hours": "hours shifted",
    "DilemmaEffect_HandOver": "work handed over",
    "DilemmaEffect_Operation": "operation",
    "DilemmaEffect_SuspendBills": "bills suspended",
    "DilemmaEffect_Emancipate": "slave freed",
    "DilemmaEffect_SelfTend": "self-tend",
    "DilemmaEffect_MedCare": "best medicine",
    # What the colony meets (Dilemmas/DilemmaWorkers_Variety2.cs): the blighted plants (and the field) marked for the
    # growers, a weapon from the stores taken up by the one chosen, an animal given a new master it follows.
    "DilemmaEffect_CutBlight": "blight marked",
    "DilemmaEffect_TakeUp": "weapon taken up",
    "DilemmaEffect_NewMaster": "new master",
    # A herd crossing the land (Happenings/Workers_Variety2.cs): grown ones of it marked for the colony's hunters.
    "DilemmaEffect_HuntHerd": "herd hunted",
    # The third round (Dilemmas/DilemmaWorkers_Variety3.cs, Happenings/Workers_Variety3.cs): a bill at the art bench for
    # the one inspired, a dose from the stores or a drug policy that keeps it from them, a cooking bill for the food that
    # won't keep, a roof marked over the stores or the open part given up, a ruin marked to come down or claimed, a bed
    # claimed, the colony's study chosen, an animal or a wild one marked for the hunters or for taming, a fence's
    # blueprint, ambrosia marked for harvest, a clutch of eggs carried in.
    "DilemmaEffect_ArtBill": "art bill",
    "DilemmaEffect_Dose": "dose taken",
    "DilemmaEffect_RideItOut": "drug policy",
    "DilemmaEffect_CookIt": "cooking bill",
    "DilemmaEffect_RoofStores": "roof marked",
    "DilemmaEffect_GiveUpOpen": "stockpile drawn in",
    "DilemmaEffect_OldWalls": "ruin marked",
    "DilemmaEffect_ClaimBed": "bed claimed",
    "DilemmaEffect_Study": "study chosen",
    "DilemmaEffect_MarkBeast": "animal marked",
    "DilemmaEffect_MendFence": "fence mended",
    "DilemmaEffect_PickAmbrosia": "ambrosia picked",
    "DilemmaEffect_Clutch": "eggs carried in",
    # Dangers on the map (Happenings/Dangers.cs): a creaking face propped, dug on or left; a leaning tree felled or
    # watched; a smoulder stamped out or left to catch; an old shell carried out, set off or kept; a live scrap broken up
    # or left; soft ground floored or left.
    "DilemmaEffect_DigFace": "creaking face",
    "DilemmaEffect_LeaningTree": "leaning tree",
    "DilemmaEffect_Smoulder": "smoulder",
    "DilemmaEffect_OldShell": "old shell",
    "DilemmaEffect_Scrap": "live scrap",
    "DilemmaEffect_SoftGround": "soft ground",
    # The fourth round of the council and the colony's year (Dilemmas/DilemmaWorkers_YearFour.cs): a field given up or
    # set to another crop, the snow-clearing area marked, the plans the stores can't pay for taken off, trees marked for
    # the cutters, wild plants marked for picking, animals marked for slaughter, an animal's training set, a day off for
    # those back from the road, an hour alone at the faith's altar, the faith's moral guide named.
    "DilemmaEffect_GiveUpField": "field given up",
    "DilemmaEffect_SetCrop": "crop set",
    "DilemmaEffect_SnowArea": "snow marked",
    "DilemmaEffect_CancelPlans": "plans taken off",
    "DilemmaEffect_MarkTrees": "trees marked",
    "DilemmaEffect_Forage": "plants marked",
    "DilemmaEffect_MarkSlaughter": "herd marked",
    "DilemmaEffect_SetTraining": "training set",
    "DilemmaEffect_RestTravellers": "rest",
    "DilemmaEffect_VisitAltar": "visit",
    "DilemmaEffect_NameGuide": "guide named",
    # Banishment by the council (Social/Banishment.cs): the council's word said face to face, and someone sent away told
    # face to face, gathering their things in their room and walking out by the edge.
    "DilemmaEffect_TroubleWord": "a word face to face",
    "DilemmaEffect_Banish": "sent away by the edge",
    # Visitors set to what they came for at the colony's relic, anima tree, gravship or bookcase, or kept by the gate
    # for a word or a lesson (Visitors/VisitorsMore2.cs).
    "VisitEffect_WorkHere": "visitors at it",
    # Visitors who came to do something, doing it on the map (Visitors/Callers.cs): a piece of art carried out to its
    # buyer, a sculpture made for a collector, a wedding party's night, graves dug and the dead laid in them or loaded on
    # the undertakers' beast, a captive walked out to their kin or talked with at their door, haircuts, animals tended.
    "DilemmaEffect_SellArt": "carried out to a buyer",
    "DilemmaEffect_ArtCommission": "made for a visitor",
    "DilemmaEffect_WeddingNight": "a night here",
    "DilemmaEffect_UndertakersBury": "graves dug",
    "DilemmaEffect_UndertakersTake": "the dead taken away",
    "DilemmaEffect_KinFree": "walked out free",
    "DilemmaEffect_KinTalk": "talk at the door",
    "DilemmaEffect_BarberCuts": "haircuts",
    "DilemmaEffect_VetRounds": "animals tended",
    # Release D's peoples at the gate (Peoples/PeoplesD.cs, PeoplesD2.cs): the Wardens' hunt and the skinning after it,
    # a tamer's friendly bout, an egg-trader's eggs set down at the gate, a stray handed over there, a mended piece
    # handed over, a wreck stripped where it lies.
    "DilemmaEffect_WardensHunt": "a hunt beside them",
    "DilemmaEffect_TamerBout": "a friendly bout",
    "DilemmaEffect_TamerEgg": "eggs handed over",
    "DilemmaEffect_StrayBack": "a stray handed over",
    "DilemmaEffect_WheelRefurbished": "handed over",
    "DilemmaEffect_WheelStrip": "a wreck stripped",
    # Release E's peoples at the gate (Peoples/PeoplesE.cs, PeoplesE2.cs): the reader out at the fields, a rod set up
    # indoors, saplings planted round the colony and beside a grave, a Gauranlen tree pruned, the squad's camp made, a
    # guild commission made at the bench, a night's lodging in a warm corner.
    "DilemmaEffect_GlassWatch": "the fields read",
    "DilemmaEffect_GlassRod": "a rod set up",
    "DilemmaEffect_MossPlant": "saplings planted",
    "DilemmaEffect_MossGraveTree": "a tree by a grave",
    "DilemmaEffect_MossTend": "a tree pruned",
    "DilemmaEffect_BannerHire": "a camp made",
    "DilemmaEffect_GuildCommission": "made for a visitor",
    "DilemmaEffect_NightLodging": "a night here",
    # The peoples' own events, round 1 (Peoples/Events/): a beast led home and a trade at the gate, a colonist worked on by
    # a visitor (a limb fitted at the bed, an hour in the storm, a mechlink put in), a seed planted together, crates
    # carried in, a camp pitched by an old place, the hunt or the taming or a bout at the gate, the Wardens' trail.
    "DilemmaEffect_HerdersBeast": "a beast led home",
    "DilemmaEffect_HerdersTrade": "a beast traded at the gate",
    "DilemmaEffect_OrderLimb": "a limb fitted",
    "DilemmaEffect_StormSense": "an hour in the storm",
    "DilemmaEffect_Mechlink": "a mechlink put in",
    "DilemmaEffect_MossSeed": "a seed planted together",
    "DilemmaEffect_TrustTake": "crates carried in",
    "DilemmaEffect_ScholarsMeasure": "a camp by an old place",
    "DilemmaEffect_HuntOrTame": "a hunt, a taming or a bout",
    "DilemmaEffect_LanternTrail": "out on the trail",
    "DilemmaEffect_HostelBread": "bread carried out",
    # The peoples' own events, round 2 (Peoples/Events2/): a colonist away for a season (a pilgrimage, a guard with the
    # league's caravan), a part fitted at the bed, an instalment carried to the gate, a crossing laid span by span and a
    # night of rest at it, a weapon reworked at the bench, a camp by the water or a pad by the colony, an archive carried
    # in, a beast's leg fitted or the beast led to the stables, the silver for the news carried out.
    "DilemmaEffect_Pilgrimage": "walks out with the pilgrims",
    "DilemmaEffect_LeagueGuard": "walks out with a beast",
    "DilemmaEffect_EnclaveBody": "a part fitted",
    "DilemmaEffect_EnclaveInstalment": "silver to the gate",
    "DilemmaEffect_RiverCrossing": "a crossing laid",
    "DilemmaEffect_KnotRest": "a night at the crossing",
    "DilemmaEffect_GuildMark": "reworked at the bench",
    "DilemmaEffect_DryWaterCamp": "a camp by the water",
    "DilemmaEffect_EnclavePad": "a pad and stalls",
    "DilemmaEffect_ArchiveKeep": "books carried in",
    "DilemmaEffect_BeastLeg": "a leg fitted",
    "DilemmaEffect_BeastStables": "led to the stables",
    "DilemmaEffect_NewsBought": "silver carried out",
    # The peoples' own events, round 3 (Peoples/Events3/): the raiders carried off on litters, a sick room kept with its
    # rounds, a field sickhouse pitched, a drove across the land, a road gang's camp and road, a rider out on the thieves'
    # trail or the herders riding alone, a calving camp and a hard birth worked at the mother's side, an old shrine raised
    # stone by stone, the palmer's walk from believer to believer.
    "DilemmaEffect_Litters": "carried off on litters",
    "DilemmaEffect_SickRoom": "a sick room and its rounds",
    "DilemmaEffect_SickhouseCamp": "a sickhouse pitched",
    "DilemmaEffect_Drove": "a drove across the land",
    "DilemmaEffect_RoadGang": "a road laid a stretch a day",
    "DilemmaEffect_ThiefRide": "out on the thieves' trail",
    "DilemmaEffect_ThiefAlone": "the riders out and back",
    "DilemmaEffect_CalvingCamp": "a calving camp",
    "DilemmaEffect_HardBirth": "a birth worked at the mother's side",
    "DilemmaEffect_OldShrine": "stones set and a lamp lit",
    "DilemmaEffect_TwoFaithsApart": "blessed one by one",
    # The peoples' own events, round 4 (Peoples/Events4/): the examination at the colonist's side, the summer school's camp,
    # the contract's goods carried out and the fee, the broker carried in, the fouled water cleaned spot by spot, the
    # lanterns set on the water, the flooded families' camp, the boat-builders' camp, the cord tied at the gate, the
    # Wardens' fight and the carcasses skinned, the Wardens at the hive.
    "DilemmaEffect_FellowExamined": "examined before the colony or at their side",
    "DilemmaEffect_SummerSchool": "a school's camp",
    "DilemmaEffect_ContractCarry": "the goods carried out",
    "DilemmaEffect_ContractFee": "the fee carried out",
    "DilemmaEffect_BrokerPod": "the broker carried in",
    "DilemmaEffect_FouledClean": "the water cleaned",
    "DilemmaEffect_KnotLanterns": "lanterns set on the water",
    "DilemmaEffect_FloodCamp": "a camp on the bank",
    "DilemmaEffect_BoatBuilders": "a hull built on the bank",
    "DilemmaEffect_WardensPage": "a cord tied at the gate",
    "DilemmaEffect_WardensManhunt": "the fight and the skinning",
    "DilemmaEffect_HiveBurned": "the Wardens at the hive",
    # The peoples' own events, round 5 (Peoples/Events5/): the gravewardens' fight and their graves, the mourners walked to
    # the graves or turning back at the gate, the grave-garden laid stone by stone, the field of the fallen's camp, the dead
    # carried home or the band against the camp, the beast trained at its side, the egg handed over and the grown beast
    # looked over, the bout-ring's camp and a challenger at the ring, the night's mending, the piece taken to the cart, the
    # goods carried out and the vehicle built at the gate, the yard at the edge, its mending and its scrap market.
    "DilemmaEffect_FieldStirs": "the gravewardens' fight and graves",
    "DilemmaEffect_MournersThrough": "walked to the graves",
    "DilemmaEffect_MournersTurned": "turned back at the gate",
    "DilemmaEffect_GraveGarden": "stones, roses and a bell-post",
    "DilemmaEffect_FallenCamp": "a camp and a row of graves",
    "DilemmaEffect_FallenClaim": "the dead carried home",
    "DilemmaEffect_BondGiven": "a beast trained at its side",
    "DilemmaEffect_CraterEgg": "an egg handed over",
    "DilemmaEffect_CraterEggGrown": "the grown beast looked over",
    "DilemmaEffect_CircuitRing": "a bout-ring pitched",
    "DilemmaEffect_RingChallenge": "a bout at the ring",
    "DilemmaEffect_MendTonight": "mended by lamplight",
    "DilemmaEffect_MadeBetter": "taken to the cart",
    "DilemmaEffect_WagonGoods": "the goods carried out",
    "DilemmaEffect_WagonBuild": "a vehicle built at the gate",
    "DilemmaEffect_WheelYard": "a yard at the edge",
    "DilemmaEffect_YardMend": "mended where they are",
    "DilemmaEffect_YardScrap": "the scrap carried out",
    # The peoples' own events, round 6 (Peoples/Events6/): the storm-walkers' night by their rods and the rain at first light,
    # the reader's hour beside the one the drone touches worst, the storm tower's camp and the night's watch at its mast,
    # the thorn-guards beating out the fire (or walking off), the plot sown beside the grower, the camp in the burn and the
    # planter beside them, the contract's march and the company at the site, the old soldier and his horse taken in and the
    # muster's road, the Banner beside the colony or in the raid, the company's camp at the edge and a fighter enlisted.
    "DilemmaEffect_BreakDrought": "a night by the rods, rain at first light",
    "DilemmaEffect_TideReader": "an hour sat beside them",
    "DilemmaEffect_StormTower": "a camp on the high ground",
    "DilemmaEffect_TowerWatch": "the night's watch at the mast",
    "DilemmaEffect_CanopyFire": "the fire beaten out",
    "DilemmaEffect_OldWoodSeeds": "a plot sown",
    "DilemmaEffect_AfterFire": "a camp in the burn",
    "DilemmaEffect_AfterPlanter": "planting beside them",
    "DilemmaEffect_BannerContract": "the company at the site",
    "DilemmaEffect_OldSoldier": "a veteran and a horse taken in",
    "DilemmaEffect_BannerMuster": "the road to the muster",
    "DilemmaEffect_OtherSideContract": "the Banner when the raid comes",
    "DilemmaEffect_CompanyMarch": "a company at the edge",
    "DilemmaEffect_CompanyEnlist": "the road to the company",
    # The peoples' own events, round 7 (Peoples/Events7/): the master's hour at the piece with its maker beside, the master's gift set
    # down at the gate, the journeymen's camp and the stone carried out to their work, the colony's best beside them, the call made at
    # the radio or carried off by a runner, the lineman on the road to the friend's town, the line crew's camp and its mast kept, the
    # miner and the doctor on the road to the pit, the well drilled or the seam marked where it lies, the picket by the road and the
    # food carried out to it.
    "DilemmaEffect_MastersMark": "the piece judged where it is",
    "DilemmaEffect_ComeBackBetter": "the master's gift set down at the gate",
    "DilemmaEffect_WorkThatStays": "a camp and the stone carried out",
    "DilemmaEffect_WorkBeside": "working beside them",
    "DilemmaEffect_VoiceCall": "the call made at the radio, or carried off",
    "DilemmaEffect_TownMast": "the lineman on the road",
    "DilemmaEffect_LineCrew": "a camp on the high ground",
    "DilemmaEffect_KeepMast": "the mast left standing",
    "DilemmaEffect_PitCaveIn": "the road to the pit",
    "DilemmaEffect_OilInGround": "the well drilled or the seam marked",
    "DilemmaEffect_Picket": "a picket by the road",
    "DilemmaEffect_PicketFed": "the strike settled at the picket",
    # The peoples' own events, round 8 (Peoples/Events8/): the clerk walking the roll and standing by the count, the citation read at
    # the gate, the deserter's fine carried out, sheltered or led away, the ration post by the road and what it buys and hands out, the
    # officers walking the gravship's decks, the casket opened, the colonist on the road to the hull, the landing party's camp and the
    # meals it buys, the gene taken out and the xenogerm written in at the colonist's side, the hybrid handed over, the field lab and
    # the work done at its people's side.
    "DilemmaEffect_VoteObserver": "the roll walked, the count stood by",
    "DilemmaEffect_RepublicMedal": "the citation read at the gate",
    "DilemmaEffect_FugitiveFine": "the fine paid at the gate",
    "DilemmaEffect_FugitiveShelter": "the deserter taken in",
    "DilemmaEffect_FugitiveHandOver": "the deserter led away",
    "DilemmaEffect_RationPost": "a ration post by the road",
    "DilemmaEffect_RationSold": "meals sold to the post",
    "DilemmaEffect_RationCards": "rations set down at the post",
    "DilemmaEffect_GravshipLog": "the decks walked",
    "DilemmaEffect_FleetSleeper": "the casket opened",
    "DilemmaEffect_OrderToDisembark": "the road to the hull",
    "DilemmaEffect_LandingParty": "a camp by the colony",
    "DilemmaEffect_PartyBuys": "meals sold to the camp",
    "DilemmaEffect_GeneOut": "the gene taken out where they stand",
    "DilemmaEffect_WholeXenogerm": "the xenogerm written in where they stand",
    "DilemmaEffect_HybridBeast": "the beast handed over",
    "DilemmaEffect_FieldLab": "a field lab by the colony",
    "DilemmaEffect_LabGene": "gene work at the colonist's side",
    "DilemmaEffect_LabSample": "a sample drawn at the colonist's side",
    "DilemmaEffect_LabBuys": "a genepack sold to the camp",
    # The peoples' own events, round 9 (Peoples/Events9/): the link put in where the mechanitor stands, the feral mech relinked at the
    # gate or taken down, the mech yard and its hire, the night at the colonist's bed, the warden at a colonist's side or at the colony's
    # edge, the listener beside a mind in a break, the listening ring and a psycaster sitting with it, the caravan's window for the
    # vault's fight, the bionic fitted at the bed or handed over, the evenings at the radio or the table, the scouts' camp and its lessons,
    # the talk at the tent, the search, and their odds and ends set down at the fire.
    "DilemmaEffect_LatticeLink": "linked in where they stand",
    "DilemmaEffect_FeralMech": "the mech relinked at the gate, or taken down",
    "DilemmaEffect_MechYard": "a mech yard by the colony",
    "DilemmaEffect_YardHire": "the yard's mechs at the colony's work",
    "DilemmaEffect_HumDeepened": "a night at the colonist's bed",
    "DilemmaEffect_VoidListens": "a day at their side, or an hour at the edge",
    "DilemmaEffect_MindShouts": "sat with through the break",
    "DilemmaEffect_ListeningRing": "a listening ring by the colony",
    "DilemmaEffect_RingSit": "a psycaster sits with the ring",
    "DilemmaEffect_SealBroken": "a caravan to the vault",
    "DilemmaEffect_VaultLastPart": "the part fitted at the bed, or handed over",
    "DilemmaEffect_ConsoleTalk": "the evenings at the radio or the table",
    "DilemmaEffect_SurfaceScouts": "the scouts' camp by the colony",
    "DilemmaEffect_ScoutLesson": "a lesson at the scouts' fire",
    "DilemmaEffect_ScoutTalkOut": "talked out of the tent",
    "DilemmaEffect_ScoutSearch": "the lost scout looked for",
    "DilemmaEffect_ScoutOdds": "odds and ends set down at the fire",
    # The peoples' own events, round 10 (Peoples/Events10/): the hearing at the colony's table and the ruling, the Wheel's carts across
    # the colony's land, turned back, or the fair toll set at the gate, the colony's people setting out for a plague town, the three
    # days at the colony's table, and the afternoon at the gate with one of a people who hands on their art.
    "DilemmaEffect_WoodMoot": "the hearing at the table, then the ruling",
    "DilemmaEffect_RoadToll": "the carts across the land or turned back, or the fair toll set at the gate",
    "DilemmaEffect_PlagueYear": "a caravan of one each to the plague town",
    "DilemmaEffect_LearningTraded": "three days at the colony's table",
    "DilemmaEffect_ArtHandedOn": "an afternoon at the gate, then the art",
    # A world event's answer that sends a colonist to a far town (World/FarErrands.cs): a caravan of one there, their days
    # there and the road home, the season away's own (Peoples/Events2/SeasonAway.cs).
    "WorldEffect_Away": "a caravan of one to a far town and home",
    # A promise halfway (Offices/SeatHalfway.cs): the seat's own work with a hand (a watch on the walls, the food brought in,
    # the sickroom seen to, the herd brought home, an evening), and the holder's word at supper.
    "DilemmaEffect_SeatHands": "the seat's work with a hand",
    "DilemmaEffect_SeatSupper": "words at supper",
    # Help sent home (World/KinNews.cs): the food or medicine carried out of the stores to the gate for their courier.
    "DilemmaEffect_KinSendHome": "the help carried to the gate for their courier",
}

# Words that say people do something together, or go somewhere on the map. Deliberately wide: the list is read by hand.
ACTIVITY = re.compile(
    r"\b(feast\w*|meal|supper|dinner|breakfast|lunch|picnic|ate|eat(s|ing)?|party|parties|celebrat\w+|danc\w+|sang|sing\w*|songs?"
    r"|music|bonfire|fire ?side|campfire|vigil|keep watch|kept watch|sat up|sit(s|ting)? (up )?with|sat with|keep\w* .{0,12}company"
    r"|visit\w*|walk\w*|stroll\w*|lesson\w*|teach\w*|taught|tutor\w*|show\w* .{0,20}how|apprentic\w+"
    r"|clean\w*|scrub\w*|sweep\w*|swept|tidy|tidied|haul\w*|rebuil\w+|repair\w*|mend\w*|patch\w* up|clear\w* (out|up|away)"
    r"|search\w*|look(s|ed|ing)? for|comb(ed|ing)?|scour\w*|hunt\w*|track(s|ed|ing)?|funeral|bur(y|ied|ial)|graves?|mourn\w*"
    r"|procession|parade|march\w*|escort\w*|see .{0,20} off|saw .{0,20} off|to the (gate|edge|road)|gifts?|carr(y|ied|ies)"
    r"|toast\w*|drink\w*|swim\w*|games?|contest|race|stor(y|ies) told|read\w* aloud|pray\w*|bless\w*|gather\w*|together|everyone)\b",
    re.I)

PLAY_RE = re.compile(r"DilemmaEffect_(\w+)")


def text_of(el):
    return "" if el is None or el.text is None else el.text.strip()


def lis(el, name):
    node = el.find(name)
    return [] if node is None else list(node.findall("li"))


def effect_kinds(option):
    kinds = []
    for name in ("effects", "riskEffects"):
        for li in lis(option, name):
            cls = (li.get("Class") or "").replace("ColonyCouncil.", "")
            if cls in PLAY:
                kinds.append(PLAY[cls])
    return kinds


class Answer:
    def __init__(self, source, defname, where, label, words, kinds, path):
        self.source = source
        self.defname = defname
        self.where = where
        self.label = label
        self.words = words
        self.kinds = kinds
        self.path = path


# A wording's tags ("[visitorGone]", "[!colony:Harsh]") pick which wording shows; they are not words the answer says.
TAGS = re.compile(r"\[[^\]]*\]")


def option_words(option):
    parts = [text_of(option.find("label")), text_of(option.find("outcome"))]
    for name in ("outcomes", "riskOutcomes"):
        parts += [text_of(li) for li in lis(option, name)]
    parts.append(text_of(option.find("riskOutcome")))
    return TAGS.sub(" ", " ".join(p for p in parts if p))


def walk():
    answers = []
    for path in sorted(glob.glob(os.path.join(ROOT, "Defs", "**", "*.xml"), recursive=True)):
        try:
            tree = ET.parse(path)
        except ET.ParseError as e:
            print("cannot parse", path, e, file=sys.stderr)
            continue
        rel = os.path.relpath(path, ROOT)
        for d in tree.getroot():
            tag = d.tag
            defname = text_of(d.find("defName"))
            if tag == "ColonyCouncil.DilemmaDef":
                for i, o in enumerate(lis(d, "options")):
                    answers.append(Answer("dilemma", defname, "option %d" % i, text_of(o.find("label")), option_words(o), effect_kinds(o), rel))
            elif tag == "IncidentDef":
                ext = None
                story = mission = False
                for li in lis(d, "modExtensions"):
                    cls = li.get("Class") or ""
                    if cls.endswith("HappeningExtension"):
                        ext = li
                    elif cls.endswith("StoryText"):
                        story = True
                    elif cls.endswith("MissionText"):
                        mission = True
                if ext is None:
                    continue
                source = "mission" if mission else "story" if story else "event"
                options = lis(ext, "options")
                for i, o in enumerate(options):
                    answers.append(Answer(source, defname, "option %d" % i, text_of(o.find("label")), option_words(o), effect_kinds(o), rel))
                if not options:
                    words = " ".join(text_of(t) for t in lis(ext, "texts"))
                    kinds = [PLAY[(li.get("Class") or "").replace("ColonyCouncil.", "")] for li in lis(ext, "effects")
                             if (li.get("Class") or "").replace("ColonyCouncil.", "") in PLAY]
                    answers.append(Answer(source, defname, "letter", text_of(d.find("label")), words, kinds, rel))
            elif tag == "ColonyCouncil.WorldSituationDef":
                for b in lis(d, "beats"):
                    key = text_of(b.find("key"))
                    options = lis(b, "options")
                    for i, o in enumerate(options):
                        answers.append(Answer("world", defname, "%s/%d" % (key, i), text_of(o.find("label")), option_words(o), effect_kinds(o), rel))
                    if not options:
                        words = " ".join(text_of(t) for t in lis(b, "texts"))
                        answers.append(Answer("world", defname, key, key, words, effect_kinds(b), rel))
                for e in lis(d, "endings"):
                    key = text_of(e.find("key"))
                    words = " ".join(text_of(t) for t in lis(e, "texts"))
                    answers.append(Answer("world", defname, "end:" + key, key, words, effect_kinds(e), rel))
            elif tag == "ColonyCouncil.DecreeDef":
                kinds = [PLAY.get((li.get("Class") or "").replace("ColonyCouncil.", "")) for li in lis(d, "onEnact") + lis(d, "daily")]
                answers.append(Answer("decree", defname, "enact", text_of(d.find("label")), text_of(d.find("description")), [k for k in kinds if k], rel))
            elif tag == "ColonyCouncil.AmbitionDef":
                kinds = [PLAY.get((li.get("Class") or "").replace("ColonyCouncil.", "")) for li in lis(d, "achievedEffects")]
                answers.append(Answer("ambition", defname, "achieved", text_of(d.find("label")), text_of(d.find("description")), [k for k in kinds if k], rel))
            elif tag == "ColonyCouncil.ArcDef":
                # Not answers: a personal story's stages, counted apart (never candidates).
                for i, st in enumerate(lis(d, "stages")):
                    kinds = []
                    for li in lis(st, "effects"):
                        cls = (li.get("Class") or "").replace("ColonyCouncil.", "")
                        if cls in PLAY:
                            kinds.append(PLAY[cls])
                        elif cls == "ArcEffect_Confess" and text_of(li.find("gathering")) == "true":
                            # A secret said out loud at a gathering called to hear it.
                            kinds.append("words")
                        elif cls == "ArcEffect_Cast":
                            kinds += [PLAY[c] for c in ((x.get("Class") or "").replace("ColonyCouncil.", "") for x in lis(li, "effects")) if c in PLAY]
                    answers.append(Answer("arc", defname, "stage %d" % i, text_of(st.find("label")), "", kinds, rel))
            elif tag == "ColonyCouncil.EncounterDef":
                answers.append(Answer("encounter", defname, "moment", text_of(d.find("label")), "", effect_kinds(d), rel))
    return answers


# Not answers to a question: counted on their own line, never listed as candidates.
APART = ("arc", "encounter")


def counts(answers):
    apart = [a for a in answers if a.kinds and a.source in APART]
    answers = [a for a in answers if a.source not in APART]
    played = [a for a in answers if a.kinds]
    by_source = defaultdict(lambda: [0, set()])
    for a in played:
        by_source[a.source][0] += 1
        by_source[a.source][1].add(a.defname)
    kinds = Counter(k for a in played for k in set(a.kinds))
    print("Answers that play out on the map: %d" % len(played))
    for src in ("dilemma", "event", "story", "mission", "world", "decree", "ambition"):
        n, defs = by_source.get(src, [0, set()])
        print("  %-9s %4d answers in %3d defs" % (src, n, len(defs)))
    dil_ev = sum(by_source[s][0] for s in ("dilemma", "event", "story", "mission", "world"))
    print("  answers of dilemmas, events, stories, missions and world events: %d" % dil_ev)
    print("  dilemmas with one: %d; events, stories, missions and world events with one: %d" % (
        len(by_source["dilemma"][1]), len(by_source["event"][1] | by_source["story"][1] | by_source["mission"][1] | by_source["world"][1])))
    print("By kind (answers using it):")
    for k, n in kinds.most_common():
        print("  %-16s %4d" % (k, n))
    arcs = [a for a in apart if a.source == "arc"]
    enc = [a for a in apart if a.source == "encounter"]
    print("Besides answers: %d stages of %d personal stories, and %d encounters" % (len(arcs), len({a.defname for a in arcs}), len(enc)))


def load_skips():
    """The skip list: (defName, where) -> why, in the file's order."""
    skips = {}
    if not os.path.exists(SKIPS):
        return skips
    with open(SKIPS, encoding="utf-8") as f:
        for n, line in enumerate(f, 1):
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            m = SKIP_LINE.match(line)
            if not m:
                print("playout_skips.txt:%d: not 'DefName [where] why': %s" % (n, line), file=sys.stderr)
                continue
            skips[(m.group(1), m.group(2))] = m.group(3).strip()
    return skips


def candidates(answers, flt, skips):
    rx = re.compile(flt, re.I) if flt else None
    rows = []
    skipped = 0
    for a in answers:
        if a.kinds or a.source in ("decree", "ambition") + APART:
            continue
        hits = sorted(set(m.group(0).lower() for m in ACTIVITY.finditer(a.words)))
        if not hits:
            continue
        if rx and not (rx.search(a.defname) or rx.search(a.words)):
            continue
        if (a.defname, a.where) in skips:
            skipped += 1
            continue
        rows.append((a, hits))
    print("\nCandidates (answers whose words describe something done, with no play-out): %d (and %d looked at and left: --skip)" % (len(rows), skipped))
    last = None
    for a, hits in rows:
        if a.path != last:
            print("\n== %s" % a.path)
            last = a.path
        print("  %-8s %s [%s] %s\n           words: %s" % (a.source, a.defname, a.where, a.label[:70], ", ".join(hits[:10])))
    stale(answers, skips)


def stale(answers, skips):
    """Skip entries that no longer say anything true: the answer is gone, or it plays out now."""
    by_key = {(a.defname, a.where): a for a in answers}
    gone = [k for k in skips if k not in by_key]
    played = [k for k in skips if k in by_key and by_key[k].kinds]
    if not gone and not played:
        return
    print("\nStale entries in playout_skips.txt: %d" % (len(gone) + len(played)))
    for d, w in gone:
        print("  gone:       %s [%s]" % (d, w))
    for d, w in played:
        print("  plays out:  %s [%s]" % (d, w))


def list_skips(answers, flt, skips):
    rx = re.compile(flt, re.I) if flt else None
    by_key = {(a.defname, a.where): a for a in answers}
    rows = [(k, why) for k, why in skips.items() if not rx or rx.search(k[0]) or rx.search(why)]
    print("\nLooked at and left (Tools/playout_skips.txt): %d" % len(rows))
    last = None
    for (d, w), why in rows:
        a = by_key.get((d, w))
        path = a.path if a else "(no such answer)"
        if path != last:
            print("\n== %s" % path)
            last = path
        print("  %s [%s]: %s" % (d, w, why))
    heads = Counter(reason_kind(why) for _, why in rows)
    print("\nBy reason:")
    for h, n in heads.most_common():
        print("  %4d  %s" % (n, h))


# The kinds of reason, for the summary: the first that fits.
REASONS = [
    ("a refusal, a ruling, or nothing done", r"refusal|nothing done|ruling|not answered|an order|a ban|a promise made|a wager|an office|a rule for"),
    ("a habit, or it takes days", r"habit|takes days|for days|over a week|two days|every night|a rota|a roster"),
    ("far away or off the map", r"far away|off the map|news|comms|not on the map|aren't on the map|nobody waiting|away from the colony|where they were found"),
    ("told after it happened", r"told after|already happened|already over|last night|already been out|a find already|came and went|arrives|already played out"),
    ("someone else's part (a stranger, a guest, an animal)", r"part\b|the camp's own|the strangers|the visitors|pass by|passes|watching"),
    ("already real, or the event's own code", r"already real|own code|already its|sends them away|the game's own|a trade"),
    ("would hurt someone or take the player's place", r"hurt|player's"),
    ("no play-out says it honestly", r"no play-out kind|no kind|not a meal|a moment|not a thing on the map|not an activity|tomorrow|a letter written|by the game's doctor"),
]


def reason_kind(why):
    for name, rx in REASONS:
        if re.search(rx, why, re.I):
            return name
    return "other: " + why


def visits_visitor(li):
    """An answer's visit that walks a colonist out to the event's visitor (X) or its group, not to one of the colony."""
    if (li.get("Class") or "").replace("ColonyCouncil.", "") != "DilemmaEffect_Visit":
        return None
    to = text_of(li.find("to")) or "Person"
    whom = text_of(li.find("whom")) or "B"
    if to == "Stranger" or (to == "Person" and whom == "X"):
        return "X"
    if to == "Person" and whom == "Group":
        return "Group"
    return None


def strangers():
    """
    Answers that walk a colonist out to the event's visitor never run without them (the great caravan's rider, 15 November
    2026: the answer went out to a rider who never came). For world events, where the defs say who comes: a visit to X
    needs a beat that brings X to the gate (VisitEffect_AtGate, X its first unless stranger is false, or the event's own
    X at their head with withStranger), since the beat's letter waits until they are there (WorldSituations.Run); a visit
    to the group needs the beat's VisitEffect_Come with group. Or the visit says where it goes instead (orTo). Rimfolk's
    events bring their visitor in code, and every such answer greys out while the visitor isn't on the map
    (DilemmaEffect_Visit.Unavailable). Returns the problems.
    """
    problems = []
    world = events = 0
    for path in sorted(glob.glob(os.path.join(ROOT, "Defs", "**", "*.xml"), recursive=True)):
        try:
            tree = ET.parse(path)
        except ET.ParseError:
            continue
        rel = os.path.relpath(path, ROOT)
        for d in tree.getroot():
            defname = text_of(d.find("defName"))
            if d.tag == "ColonyCouncil.WorldSituationDef":
                for b in lis(d, "beats"):
                    key = text_of(b.find("key"))
                    effects = lis(b, "effects")
                    cls = lambda li: (li.get("Class") or "").replace("ColonyCouncil.", "")
                    brings_x = any(cls(li) == "VisitEffect_AtGate" and (text_of(li.find("stranger")) != "false" or text_of(li.find("withStranger")) == "true") for li in effects)
                    brings_group = any(cls(li) == "VisitEffect_Come" and text_of(li.find("group")) == "true" for li in effects)
                    for i, o in enumerate(lis(b, "options")):
                        for li in lis(o, "effects") + lis(o, "riskEffects"):
                            whom = visits_visitor(li)
                            if whom is None:
                                continue
                            world += 1
                            if li.find("orTo") is not None:
                                continue
                            if whom == "X" and not brings_x:
                                problems.append("%s %s [%s/%d]: walks out to X, but the beat brings nobody to the gate as X" % (rel, defname, key, i))
                            if whom == "Group" and not brings_group:
                                problems.append("%s %s [%s/%d]: walks out to the group, but no VisitEffect_Come of the beat has group" % (rel, defname, key, i))
            elif d.tag == "IncidentDef":
                for ext in lis(d, "modExtensions"):
                    if not (ext.get("Class") or "").endswith("HappeningExtension"):
                        continue
                    for o in lis(ext, "options"):
                        for li in lis(o, "effects") + lis(o, "riskEffects"):
                            if visits_visitor(li) is not None:
                                events += 1
    print("\nAnswers that walk out to the event's visitor: %d of world events (each where its beat brings them, or with a place to go instead), %d of Rimfolk's events (greyed out while the visitor isn't on the map)" % (world, events))
    for p in problems:
        print("  " + p)
    return problems


def main():
    args = sys.argv[1:]
    flt = None
    if "--filter" in args:
        flt = args[args.index("--filter") + 1]
    answers = walk()
    skips = load_skips()
    modes = ("--counts", "--candidates", "--skip")
    want_counts = "--counts" in args or not any(a in args for a in modes)
    want_cands = "--candidates" in args or not any(a in args for a in modes)
    if want_counts:
        counts(answers)
    if want_cands:
        candidates(answers, flt, skips)
    if "--skip" in args:
        list_skips(answers, flt, skips)
        stale(answers, skips)
    # Always: no answer walks out to a visitor who isn't there.
    if strangers():
        sys.exit(1)


if __name__ == "__main__":
    main()
