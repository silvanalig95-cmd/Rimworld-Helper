using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>What era the colony lives in, for what the mod suggests and how its colonists talk.</summary>
    public enum EraMode
    {
        Auto,
        PreIndustrial,
        Industrial
    }

    public enum StakesMode
    {
        /// <summary>Only upsides: granted wishes help, ignored ones cost nothing.</summary>
        Gentle,
        /// <summary>Ignored wishes, broken promises, favoritism and jealousy all bite, and can lead to sulking and grudges.</summary>
        Demanding,
        /// <summary>No mood effects at all. Pure suggestions.</summary>
        AdvisorOnly,
        /// <summary>Upsides and downsides in proportion (default).</summary>
        Balanced
    }

    /// <summary>Where the council button sits in the bottom bar.</summary>
    public enum ButtonPosition
    {
        BeforeMenu,
        AfterResearch,
        AfterHistory,
        AfterWork
    }

    public class CouncilSettings : ModSettings
    {
        // Layers
        public bool enableAdvisor = true;
        public bool enableWishes = true;
        public bool enableAmbitions = true;
        public bool enablePersonalTouches = true;

        // Wishes
        public StakesMode stakes = StakesMode.Balanced;
        public float daysBetweenWishes = 2f;
        public int maxOpenWishes = 5;
        public float wishDurationFactor = 1f;
        public float rewardFactor = 1f;
        public float penaltyFactor = 1f;
        public bool jealousy = true;
        public bool selfHelp = true;
        /// <summary>Promises kept (Wishes/Kept.cs): a lasting wish granted and then taken away again upsets the one who asked.</summary>
        public bool wishesTakenBack = true;
        /// <summary>How long what was given may be gone (moved, reassigned) before they mind.</summary>
        public KeptGrace takenBackGrace = KeptGrace.Normal;
        public bool allowQuitting = true;
        public bool risingStandards = true;
        /// <summary>
        /// Good things grow ordinary (Moods/GoodLife.cs): part of the good moods above a line set by standards counts for
        /// less, the everyday ones most and the new ones least. One setting for what four did until phase 3 of the review
        /// (the good moods' budget, standards over the game's comforts, good times fading sooner, used to the good life).
        /// </summary>
        public bool goodGrowsOrdinary = true;
        /// <summary>The four old switches were read into <see cref="goodGrowsOrdinary"/> (an older settings file reads false).</summary>
        public bool ordinaryMerged = true;
        /// <summary>Quiet troubles: now and then a low day with a real cause for someone content a while (Setbacks/Troubles.cs).</summary>
        public bool quietTroubles = true;
        /// <summary>Life's ups and downs: how hard the everyday pressures press (the same meal, the same work, crowding...).</summary>
        public LifePressure lifePressure = LifePressure.Normal;
        public bool enableDilemmas = true;
        /// <summary>Banishment by the council (Social/Banishment.cs): a word for someone who keeps causing trouble and whom most can't stand, and banishment if it goes on.</summary>
        public bool councilBanishment = true;
        /// <summary>The old "show what each answer does" switch, kept in step with <see cref="answerImpact"/> (off when it is Off) so older settings files load.</summary>
        public bool showImpact = true;
        /// <summary>
        /// The choice before the flavour line came (hints in words, exact numbers or off), kept in step with
        /// <see cref="answerImpact"/> so older settings files load: their hints, the default then, read as the flavour line.
        /// </summary>
        public ImpactShow impactShows = ImpactShow.Hints;
        /// <summary>What each answer of a dilemma or an event does, under it in the letter and on the Dilemmas page: a line of flavour (hints on hover), hints in words, exact numbers, or nothing (Dilemmas/DilemmaImpact.cs).</summary>
        public ImpactShow answerImpact = ImpactShow.Flavour;
        public bool annals = true;
        public bool decreeRequests = true;
        public bool anniversaries = true;
        /// <summary>The colony calendar's days come round with letters, gatherings and moods (Calendar/); off, they're only written down.</summary>
        public bool colonyCalendar = true;
        /// <summary>Answers that describe something people do (a feast, a bonfire, a visit, a day off) play out on the map (PlayOut/).</summary>
        public bool answersPlayOut = true;
        /// <summary>Goods and people that events move between the colony and the outside world come and go on the map, carried by real people (Couriers/).</summary>
        public bool goodsComeAndGo = true;
        public bool epithets = true;
        public bool journals = true;
        public bool offices = true;
        /// <summary>An election is an event on the map: announced for a date, a campaign, the hustings, polling day and the count aloud (off: the instant letter).</summary>
        public bool electionEvent = true;
        public bool socialWeb = true;
        /// <summary>Friendships take time: two need to know each other (time side by side, talks, what they went through) as well as like each other (Social/Bonds.cs).</summary>
        public bool bondsTakeTime = true;
        /// <summary>How two get on (the game's compatibility with faith, views, shared passions and temper) sets how far words and a friendship can go.</summary>
        public bool chemistryLimits = true;
        /// <summary>Comrades only from fighting near each other, more when one saved the other; fading in quiet spells to what their fights together leave.</summary>
        public bool comradesByDanger = true;
        /// <summary>What people did to each other is remembered: harm, cross words, a botched operation, an affair and envy start grudges with their cause; rescues and doctors earn gratitude.</summary>
        public bool deedsRemembered = true;
        /// <summary>A council seat lost is felt toward the winner, by how much it was wanted; losing to the same person again, or a gloating winner, can start a rivalry.</summary>
        public bool seatFeelings = true;
        /// <summary>Love's own troubles: jealousy of a partner's close friend, an awkward spell after a rejection or a breakup, losing out to someone else.</summary>
        public bool loveFeelings = true;
        /// <summary>Rimfolk's column in the game's own Social tab (bonds, groups, name, shared memories, talks, secrets), and a line about the pair on its rows.</summary>
        public bool socialTabColumn = true;
        /// <summary>Personalities shaped by what happens to people, what they went through together, the views they hold, their groups and talk.</summary>
        public bool socialLife = true;
        /// <summary>The game's traits can grow, appear or fade with what happens to a colonist (Persona/TraitChanges.cs); needs social life.</summary>
        public bool traitChanges = true;
        /// <summary>Faith in the colony (Faith/, with Ideology): faith talks that move certainty, evenings of faith, crises, the faiths page.</summary>
        public bool faithLife = true;
        public bool socialEvents = true;
        public bool dreams = true;
        public bool aspirations = true;
        /// <summary>Personal stories (Arcs/): how often a colonist's own story begins.</summary>
        public ArcFrequency arcFrequency = ArcFrequency.Normal;
        public bool secrets = true;
        public bool vendettas = true;
        public bool eulogies = true;
        public bool quirks = true;
        public bool heirlooms = true;
        public bool comingOfAge = true;
        public bool dreamOmens = true;
        /// <summary>Small moments with visitors, traders, prisoners, fallen enemies, pets and wild animals.</summary>
        public bool strangers = true;
        public bool nemeses = true;
        /// <summary>A nemesis who got away may ride with their people's next raid.</summary>
        public bool nemesisReturns = true;
        public bool petMoments = true;
        /// <summary>With Ideology: the animals the colony's faith holds sacred are mourned when one falls on the land, and their day can become a custom (Animals/SacredAnimals.cs).</summary>
        public bool sacredAnimals = true;
        /// <summary>An animal born to the colony under a sign (an eclipse, an aurora, a day the colony keeps, a shiny Pokémon) is an omen (Animals/OmenAnimals.cs).</summary>
        public bool omenAnimals = true;
        /// <summary>Every named or bonded animal has a temperament, on its card and in small moments on the map (Animals/AnimalTempers.cs); needs pets as companions.</summary>
        public bool animalTempers = true;
        /// <summary>How often the animals' temperaments show on the map, 0 to 2 (0: never, the card still says it).</summary>
        public float animalMoments = 1f;
        /// <summary>Friendships, rivalries and grief among the colony's animals, and an old animal that knows its handler again (Animals/AnimalBonds.cs); never a letter or a message.</summary>
        public bool animalBonds = true;
        /// <summary>Animals that speak (Animals/AnimalVoices.cs), a fantasy part: Auto only with PokéWorld, A RimWorld of Magic or a psylink in the colony.</summary>
        public AnimalSpeech speakingAnimals = AnimalSpeech.Auto;
        /// <summary>The colony's animals whose kind hunts go after prey below them in the food chain when nothing they may eat is in reach, and now and then for small game (Animals/PetHunts.cs).</summary>
        public bool petsHunt = true;
        /// <summary>A bleeding animal that isn't the colony's gets the button "Take the body when it dies": left alone while it lives, its body hauled in once it dies (Animals/BodyWhenItDies.cs).</summary>
        public bool bodyWhenItDies = true;
        public bool colonistMoments = true;
        /// <summary>Everyday frictions: small irritations between colonists, and with a messy or cramped home.</summary>
        public bool frictions = true;
        public bool autonomy = true;
        public bool worldReactions = true;
        public bool autonomyPlans = true;
        /// <summary>Rimfolk's world events: pasts catching up, challenges, wonders, visitors (on their own rhythm).</summary>
        public bool worldEvents = true;
        /// <summary>World events that can end in a fight (bounty hunters, revenge, tribute refused).</summary>
        public bool worldEventBattles = true;
        /// <summary>Groups who make camp on the colony's map for a while (Camps/).</summary>
        public bool camps = true;
        /// <summary>How many camps: how often they come, the rest after one leaves, how many at once by the map's room (Camps/CampCrowdMath.cs). Usual is one at a time.</summary>
        public CampCrowd campCrowd = CampCrowd.Usual;
        /// <summary>Missions a neighbor offers show up in the game's Quests tab, to accept or let expire there (else as a letter).</summary>
        public bool missionsAsQuests = true;
        public float daysBetweenWorldEvents = 4f;
        /// <summary>Rimfolk's colony events join the storyteller's own random events (else they come on Rimfolk's own timer).</summary>
        public bool eventsFromStoryteller = true;
        /// <summary>How likely each of them is, compared with one of the game's own events.</summary>
        public float storytellerEventWeight = 1f;
        /// <summary>World events: rare, region-wide situations that last a while (a plague, a warlord, a succession).</summary>
        public bool worldSituations = true;
        public float daysBetweenWorldSituations = 45f;
        /// <summary>The world's peoples remember what the colony did for them and against them, and act on it (World/FactionMemory.cs).</summary>
        public bool factionMemory = true;
        /// <summary>The nearby towns' people by name (head, heir, envoy, trader) and the faces the colony met, kept in the world while they matter (World/Notables.cs).</summary>
        public bool notables = true;
        /// <summary>Letters to them by hand, and calls on a comms console, answered in their own way (World/NeighbourLetters.cs).</summary>
        public bool neighbourLetters = true;
        /// <summary>The post (Post/): letters wait in a mailbag at the gate and go with whoever is going their way; a regular post by the age; the colony's own rider; mail coming in the same way; the range until radio.</summary>
        public bool post = true;
        /// <summary>How often the regular post comes (Post/PostRider.cs): seldom, as usual, often.</summary>
        public PostPace postPace = PostPace.Usual;
        /// <summary>Letters by occasion (World/LetterOccasions.cs): greetings, invitations and asking for help only when there is a reason.</summary>
        public bool letterOccasions = true;
        /// <summary>Dealings with a whole people or one of their towns, received by its council (Dealings/): contracts, relief, proclamations, hires, petitions, joint works, market rights, invitations.</summary>
        public bool townDealings = true;
        /// <summary>Pen friends (Dealings/PenFriends.cs): a steady exchange of letters with one notable brings warnings, better terms, introductions and a favor.</summary>
        public bool penFriends = true;
        /// <summary>The world writes first (Dealings/WorldWrites.cs): now and then a notable writes to the colony unasked, with news, a request or an invitation that wants an answer.</summary>
        public bool worldWrites = true;
        /// <summary>The region: each town within reach has a fortune that the world's events move, shown in its panel and its people's words (World/Region/).</summary>
        public bool regionFortunes = true;
        /// <summary>The region: a new town within reach has a founder, founding families and a reason, told in the news, its talk and a letter (World/Region/).</summary>
        public bool regionFaces = true;
        /// <summary>The region: a town's fortune in what it sells when the game restocks it (World/Region/RegionStock.cs; needs the fortunes and the deeper hooks).</summary>
        public bool regionStock = true;
        /// <summary>
        /// The region: towns rise and fall at a slow pace (World/Region/RiseFall.cs): struggling towns given up, ruins where
        /// towns fell, ruins settled again, Rimfolk's own new towns. Off: nothing changes on the map, and the records are kept.
        /// </summary>
        public bool regionChange = true;
        /// <summary>
        /// The region: how much a home's remoteness draws travellers (0 to 2, 1 by default): camps up to 60% more often and
        /// Rimfolk's visitors up to 60% more at the most remote, the kinds that seek what its land has likelier, and news of
        /// far towns told at their fire (World/Region/TravelLand.cs). 0: as before, and no far news.
        /// </summary>
        public float remoteTravellers = 1f;
        /// <summary>
        /// The region: raiders' camps (0 to 2, 1 by default): how likely a rich colony on bad terms with a hostile people is
        /// to find one set up near it, and how often it raids (the game's raid-source rate at 1). 0: none set up, none raids
        /// (World/Region/RaidersCamp.cs).
        /// </summary>
        public float raidersCamps = 1f;
        /// <summary>
        /// The region: new peoples arise during play (0 to 2, 1 by default): survivors of a fallen town, a league of
        /// prospering towns, a band of those the council sent away, faithful who broke away (Peoples/Arising.cs). At 1 one
        /// arises every 40 days at most, at 2 every 20; 0: none.
        /// </summary>
        public float newPeoples = 1f;
        /// <summary>
        /// The region: Rimfolk's peoples' ages change over the years (Peoples/PeopleAges.cs): each rises toward the next age
        /// at a pace of its ways, faster for the colony's trade, gifts of learning, teaching and alliance, and falls back an
        /// age when cut off from the world. Off: every people lives in the age it began in, and nothing moves.
        /// </summary>
        public bool peopleAges = true;
        /// <summary>
        /// The wild's alphas and bosses (Beasts/), each 0 to 2, 1 by default: a wild pack led by a named alpha (one a home
        /// at a time, a month apart at 1), a beast nemesis that flees hurt and comes back and the one who swears to bring it
        /// down, a legend of the region (150 days between two at 1), and the great herds of spring and fall with their
        /// stampedes. 0 switches a part off.
        /// </summary>
        public float packAlphas = 1f;
        public float beastNemeses = 1f;
        public float regionLegends = 1f;
        public float greatHerds = 1f;
        /// <summary>
        /// The region: friends who trade with a far colony may open a trading post a few days off, a real town of theirs
        /// (World/Region/TradingPost.cs). Off: none opens, and those standing are left as ordinary towns of theirs.
        /// </summary>
        public bool tradingPosts = true;
        /// <summary>
        /// The region: settlers may ask to found a hamlet a few days off under the colony's protection (World/Region/Hamlet.cs).
        /// Off: no settlers ask, and a hamlet standing goes on as their town while its record is kept.
        /// </summary>
        public bool hamlets = true;
        /// <summary>
        /// The region: a hamlet, a trading post or a town in its first year has a smaller base with fewer defenders on its
        /// own map (World/Region/SmallTowns.cs; needs the deeper hooks). Off: every town's map as the game makes it.
        /// </summary>
        public bool smallTownMaps = true;
        /// <summary>
        /// Diplomacy: a good turn big enough leaves a debt of honour either way, the colony calls in what is owed it and
        /// the peoples call in what the colony owes them (Diplomacy/). Off: no debt is made or called in, and those open run
        /// out by their terms.
        /// </summary>
        public bool diplomacyFavours = true;
        /// <summary>Pacts (defence and trade) and truces with the peoples around the colony (Diplomacy/Pacts.cs, Truces.cs). Off, nothing new is made, and those live run out by their terms.</summary>
        public bool diplomacyPacts = true;
        /// <summary>While a truce holds, the storyteller doesn't choose that people to raid that home (a hook on the game's choice of raiders; with the deeper hooks).</summary>
        public bool truceRaids = true;
        /// <summary>Ransom both ways (Diplomacy/Ransoms.cs): their offers for their own in the colony's cells, the game's demand by messenger without a comms console, exchanges at the gate. Off, nothing new comes, and what is under way plays out.</summary>
        public bool diplomacyRansom = true;
        /// <summary>Envoys both ways (Diplomacy/Envoys.cs, EnvoysAbroad.cs): their envoy as the colony's guest for a season, and the colony's own at their town. Off, nothing new is agreed, and a stay under way runs its season.</summary>
        public bool diplomacyEnvoys = true;
        /// <summary>Feasts, hunts, games and contests with guests from the peoples around (Diplomacy/Feasts.cs). Off, nothing new is planned or suggested, and a feast under way plays out.</summary>
        public bool diplomacyFeasts = true;
        /// <summary>Marriage alliances (Diplomacy/Matches.cs): matches between the colony's people and theirs, the wedding, the alliance. Off, nothing new is matched, a match agreed goes ahead, and a marriage holds.</summary>
        public bool diplomacyMarriage = true;
        /// <summary>Wards and hostages (stage 2, release 2b.3): a youth fostered either way for a year, hostages for a truce's or a pact's terms.</summary>
        public bool diplomacyWards = true;
        /// <summary>Fealty and vassals (stage 2, release 2c.2): the colony sworn to a stronger neighbour, and vassals of its own. Off, nothing new is sworn or asked, and the oaths standing run on.</summary>
        public bool diplomacyFealty = true;
        /// <summary>Colony renown (stage 2, release 2d.1, reshaped 5 November 2026): the colony's name among the peoples, and each colonist's own, known for what its people did, read from deeds and ties, nothing spent. Off, nothing is told, read or tipped.</summary>
        public bool diplomacyRenown = true;
        /// <summary>Against their nature (stage 2, release 2d.2): an act of diplomacy against a colonist's nature builds their strain through the heart, one with it eases them, and repeated acts may leave a mark. Off, nothing new is felt, and what was felt drains as any strain does.</summary>
        public bool diplomacyNature = true;
        /// <summary>
        /// Schemes against the colony (stage 2, release 2c.3, theirs only; 0 to 2, 1 by default): how often a people with a
        /// grudge within reach, or a notable that cold, schemes against a home while one of theirs is on its map (a machine
        /// broken down, a small valuable taken, a quarrel stirred up, a smear). 0: none begin, and those under way run out.
        /// </summary>
        public float theirSchemes = 1f;
        /// <summary>Right-clicking someone from outside the colony offers things to do together: talk, news, a meal, a gift, favours, bonds (Outsiders/).</summary>
        public bool outsiderTalk = true;
        /// <summary>A practice bout with an outsider: blunt, bruises likely, a real injury now and then.</summary>
        public bool outsiderSpar = true;
        /// <summary>Insulting an outsider: it sours them, and a hot temper may answer with a fistfight their people remember.</summary>
        public bool outsiderProvoke = true;
        /// <summary>A duel of honour with someone of a people whose ways know it: a real fight, to first blood or until one yields.</summary>
        public bool outsiderDuels = true;
        /// <summary>Colonists sometimes start a talk with an outsider near them by themselves, in bubbles (Outsiders/OutsiderTalk.cs).</summary>
        public bool outsiderChatter = true;
        /// <summary>How often they do, 1 normal.</summary>
        public float outsiderChatterFrequency = 1f;
        /// <summary>
        /// The right-click's things done by the colonists themselves at a good moment (Outsiders/OutsiderAuto.cs): off, only
        /// those that spend nothing of the colony's, or those and the ones that spend a little or take a small risk, within
        /// the day's limits.
        /// </summary>
        public OutsiderAutoMode outsiderAuto = OutsiderAutoMode.WithinLimits;
        /// <summary>Journeys planned ahead: the travellers get ready together and set off at the hour chosen (Journeys/).</summary>
        public bool journeys = true;
        /// <summary>Many colonies (Colonies/Foundings.cs): a fresh start in the same world offered from a colony's card, and newcomers called to it.</summary>
        public bool freshStarts = true;
        public bool newcomers = true;
        /// <summary>Many colonies (Colonies/HandOvers.cs): a colony handed over to a people of its own, from its card or its world button.</summary>
        public bool handOvers = true;
        /// <summary>Many colonies (Colonies/TownsAfter.cs): a town the colony was writes, sends its trader, invites the colony to its days and asks to send its people home.</summary>
        public bool townLife = true;
        /// <summary>Many colonies (Colonies/Merges.cs): a colony's people come to live at another home, and the two councils become one.</summary>
        public bool manyMerge = true;
        /// <summary>A journey shifts each traveller's night before, so they wake rested in time to set off.</summary>
        public bool journeyShiftSleep = true;
        /// <summary>Travellers change into clothes from the stores for the weather on the road.</summary>
        public bool journeyClothes = true;
        /// <summary>How many hours before setting off the travellers put work aside to get ready.</summary>
        public int journeyFreeHours = 4;
        /// <summary>The game's next weather weighed as the land and the last days make likely: spells, what follows what, events, the year (Weather/WeatherDynamics.cs).</summary>
        public bool dynamicWeather = true;
        /// <summary>The weather moves the outdoor temperature a few degrees: cooler under rain on a warm day, colder on a clear night, milder under cloud.</summary>
        public bool weatherTemperature = true;
        /// <summary>Someone who reads the weather (or a weather station) says what's coming, and is right as often as they're good at it.</summary>
        public bool weatherForecast = true;
        /// <summary>The harder it rains, the faster blood, dirt and other filth the game lets rain wash comes off the open ground (Weather/RainWash.cs).</summary>
        public bool rainWashesMore = true;
        /// <summary>How hard the fights a Rimfolk scenario promises hit (share of their usual strength).</summary>
        public float scenarioThreat = 1f;
        /// <summary>How often the smaller things happen: 1 = as designed, 0.25 = rarely, 3 = often.</summary>
        public float encounterFrequency = 1f;
        public float momentFrequency = 1f;
        public float socialEventFrequency = 1f;
        public float dreamFrequency = 1f;
        /// <summary>The colony's character: what its choices make it known for.</summary>
        public bool colonyCharacter = true;
        /// <summary>Rimfolk's systems talk to each other (Interplay/): offices at work, the heart in choices, stories hearing the world...</summary>
        public bool interplay = true;
        /// <summary>Feelings and strain (Heart/): a layer under mood that never changes it.</summary>
        public bool heart = true;
        /// <summary>A colonist at the end of their rope may take a day to themselves.</summary>
        public bool heartEpisodes = true;
        /// <summary>A line about strain and feelings under a colonist's name in the inspector (a deeper game hook).</summary>
        public bool heartInspector = true;
        /// <summary>The heart's good side (Heart/GoodHeart.cs): in good heart, in high spirits. Off, nothing builds and the bands give nothing.</summary>
        public bool heartGoodSide = true;
        /// <summary>How quickly strain builds: 0.5 to 1.5.</summary>
        public float strainSpeed = 1f;
        /// <summary>Everyday ailments (Ailments/): colds, strained backs, sunburn, and the like.</summary>
        public bool ailments = true;
        /// <summary>The rare serious illnesses, which can kill (tetanus, pneumonia, tuberculosis...).</summary>
        public bool seriousIllnesses = true;
        /// <summary>
        /// Rimfolk's serious illnesses share the game's own disease rate (Ailments/IllnessBudget.cs): each that starts holds
        /// back the game's next disease on that home, so in all serious illness comes about as often as in plain RimWorld.
        /// </summary>
        public bool illnessBudget = true;
        /// <summary>A crowded, dirty colony catches serious illness more often (Ailments/LifeRisks.cs).</summary>
        public bool crowdedSickness = true;
        /// <summary>The old, small children, the hungry and the badly hurt fight illness slower (Ailments/LifeRisks.cs).</summary>
        public bool frailty = true;
        /// <summary>A wound left untended in the dirt is likelier to turn bad (Ailments/LifeRisks.cs).</summary>
        public bool dirtyWounds = true;
        /// <summary>The end of a long life: a season's decline past old age, and death in their sleep (Health/LastSeason.cs).</summary>
        public bool endOfLife = true;
        /// <summary>Colonists may ask to move on, always as a question for the council (Social/MovingOn.cs).</summary>
        public bool movingOn = true;
        /// <summary>How often ailments come: 0.25 to 2.</summary>
        public float ailmentFrequency = 1f;
        /// <summary>
        /// Hurts from everyday life (Harm/): the factor on every Rimfolk-made hurt (drills that bruise, tired hands,
        /// a strain worked through), 0 to 2, leaning with the storyteller's own threat and disease numbers.
        /// </summary>
        public float everydayHurts = 1f;
        /// <summary>Convalescence after serious illness, operations, long bed rest and lost limbs; sickbed visits (Health/).</summary>
        public bool convalescence = true;
        /// <summary>Doctors' and midwives' reputations: names, the colony's trust, the weight of losses (Health/).</summary>
        public bool healerReputations = true;
        /// <summary>
        /// Medicine by what is treated (Health/MedicineChoice.cs): the serious get the best the medical care setting allows,
        /// minor wounds and everyday ailments herbal at most, harmless wounds none; with Smart Medicine, its per-injury
        /// choice filled in where the player set none.
        /// </summary>
        public bool medicineByNeed = true;
        /// <summary>What harmless wounds (a bruise, a crack, the smallest scratch) get under medicine by what is treated.</summary>
        public HarmlessMedicine harmlessMedicine = HarmlessMedicine.None;
        /// <summary>
        /// Bed rest only when it makes sense (Health/BedRest.cs): only a serious wound, a dangerous illness, a stage too much
        /// to work through, and what the game makes urgent send someone to bed; with a tended scratch, a cold or a sunburn they
        /// keep working.
        /// </summary>
        public bool restByNeed = true;
        /// <summary>On-screen buttons for half and quarter speed, and where they sit (fractions of the screen; -1 = default).</summary>
        public bool slowButtons = true;
        public bool moodInsight = true;
        public bool deepHooks = true;
        public bool needsTabHints = true;
        /// <summary>With Ideology: the style-surroundings mood names the things that count against a colonist, a button selects them, and a styled thing names whose style it goes against (Moods/StyleSurroundings.cs).</summary>
        public bool styleNotes = true;
        /// <summary>A line of Rimfolk's context in the game's raid, mood break and death letters.</summary>
        public bool letterNotes = true;
        /// <summary>Small badges on the colonist bar's portraits (ColonistBar/BarBadges.cs, a deeper game hook), and what they show.</summary>
        public bool barBadges = true;
        public BarBreakMark barBreakMark = BarBreakMark.Ring;
        /// <summary>How far above the minor break line (as a share of mood) the break ring starts, faintly; 0 = only under it.</summary>
        public float barBreakMargin = 0.05f;
        public bool barBadgeLeaving = true;
        public bool barBadgeWish = true;
        /// <summary>The share of a wish's patience gone before its badge shows.</summary>
        public float barWishPatience = 0.5f;
        public bool barBadgeStrain = true;
        public bool barBadgeFaith = true;
        public bool barBadgePromise = true;
        public BarBadgeSize barBadgeSize = BarBadgeSize.Normal;
        public BarBadgeCorner barBadgeCorner = BarBadgeCorner.TopRight;
        public int barBadgeMax = 2;
        public float slowButtonsX = -1f;
        public float slowButtonsY = -1f;
        public bool allDreamMessages;
        public MessageLevel messageLevel = MessageLevel.Notable;
        /// <summary>Where the smaller news goes: a message as it happens, one letter each evening (default), or only the History tab (UI/News.cs).</summary>
        public NewsMode colonyNews = NewsMode.Digest;
        /// <summary>
        /// The most messages a day the teller shows for one colony, all kinds of news together; past it, and for a person's,
        /// health and the land's news on a busy day, they wait for the evening's colony news (UI/Tell.cs). It was a cap per
        /// kind (tellMessagesADay, 20) before the teller weighed the day; the new key starts from the new default.
        /// </summary>
        public int messagesADay = 12;
        /// <summary>The most letters that only tell, a day for one colony; past it they become lines in the evening's colony news (UI/Tell.cs). Per kind and 6 before (tellLettersADay).</summary>
        public int lettersADay = 4;
        /// <summary>
        /// The colony's pace (PlayOut/Stage.cs): its own doings (a group's evening, a family supper, a camp's show, a
        /// birthday) come only while the last seven days hold fewer gatherings than this, the player's own choices counted.
        /// </summary>
        public int gatheringsAWeek = 2;
        public EraMode era = EraMode.Auto;
        public float daysBetweenDilemmas = 10f;
        public bool eventWishes = true;
        public bool reminders = true;
        public bool wishesForOthers = true;
        public bool petitions = true;
        public bool wishLetters = true;
        public bool grantedLetters = false;
        /// <summary>A letter after Rimfolk has been updated, with the newest round's highlights (UI/WhatsNew.cs).</summary>
        public bool whatsNewLetter = true;
        /// <summary>The Rimfolk version the player last saw, so the update letter comes once per update.</summary>
        public string lastSeenVersion = "";
        /// <summary>The old "colonists speak" switch, kept in step with <see cref="speechShows"/> (off when it is Off) so older settings files load.</summary>
        public bool speechText = true;
        /// <summary>How colonists' speech shows: bubbles when Interaction Bubbles is loaded, floating text, or not at all (Immersion/Speech.cs).</summary>
        public SpeechMode speechShows = SpeechMode.Bubbles;
        /// <summary>The game's own flecks, sky colors and sounds for what Rimfolk says is happening (Touches/).</summary>
        public bool visualTouches = true;
        /// <summary>The name of a mood floats up beside the game's mood bubble over a colonist (Immersion/MoodBubbleText.cs).</summary>
        public bool moodBubbleText = true;
        /// <summary>Recreation Rimfolk's knowledge makes likelier: graves visited, slow walks, the fire after a fight (Immersion/SmallHabits.cs).</summary>
        public bool smallHabits = true;

        // Meetings
        public float daysBetweenMeetings = 15f;
        public float ambitionDeadlineFactor = 1f;
        /// <summary>At a meeting's start the chair calls everyone from ordinary work and has sleepers woken (off: they come when free).</summary>
        public bool meetingsCallFromWork = true;

        // Personal touches
        public float daysBetweenPersonalTouches = 6f;

        // Advisor
        public bool advisorAlert = true;
        public bool wishAlert = true;
        /// <summary>When raiders come, say once if a defense line's gate is shut (raiders with no way in break the wall anywhere).</summary>
        public bool defenseGateReminder = true;
        /// <summary>Ctrl and a right-drag with drafted colonists sends them to cover from the side the drag points at (Defense/CoverDrag.cs).</summary>
        public bool coverDrag = true;
        /// <summary>With RimMarket: a colony stall's sales table opens as Rimfolk's window (Integrations/RimMarketStall.cs).</summary>
        public bool marketStallWindow = true;
        /// <summary>With RimMarket and a powered orbital trade beacon: a colony stall's goods are offered to trade ships too.</summary>
        public bool marketStallShips = true;
        /// <summary>With Alpha Genes: a carrier of the repulsive gene or angelic beauty whose face and body are covered stirs nothing in others (Integrations/CoveredLooks.cs).</summary>
        public bool coveredLooks = true;
        /// <summary>With RimWorld of Magic: a golem keeps its own needs and gets back an energy need it lost (Integrations/MagicGolems.cs).</summary>
        public bool magicGolemRepair = true;
        /// <summary>With RimWorld of Magic: the mod's own rules about needs hold (nobody has its unfinished travel need; the undead have no mana or stamina), even when another mod answers the needs check first (Integrations/MagicNeeds.cs).</summary>
        public bool magicNeedRules = true;
        /// <summary>With Faction Territories and Vassalage: its messages about new settlements far away go to the colony news; nearby ones stay messages (Integrations/TerritoryNews.cs).</summary>
        public bool territoryNewsQuiet = true;
        /// <summary>A faith's precept against automated turrets gives its mood only on a map where the colony has one (Integrations/TurretFaith.cs).</summary>
        public bool faithTurretsHere = true;
        /// <summary>With Alpha Skills: a colonist with a drunken passion sometimes takes a drink before work that trains it (Integrations/BeforeWork.cs).</summary>
        public bool drinkBeforeWork = true;
        /// <summary>The drink before work may come before hot work too (cooking, smithing), with its small chance of a burn (Harm/Mishaps.cs).</summary>
        public bool drinkBeforeHotWork = true;
        /// <summary>With Alpha Skills: a colonist with a stoned passion sometimes smokes a joint before work that trains it (Integrations/BeforeWork.cs).</summary>
        public bool smokeBeforeWork = true;
        /// <summary>With RimMarket and Hospitality, guests may always reach the market's stalls to shop, whatever their guest area (Integrations/MarketGuests.cs).</summary>
        public bool marketGuestsShop = true;
        /// <summary>With RimMarket: visitors camped on the colony's land and people from a camp near it buy off the colony's stalls (Visitors/VisitorTrade.cs).</summary>
        public bool marketCampersBuy = true;
        /// <summary>With Hospitality: a guest joins of their own will only when they would, and the colony has its say (Integrations/GuestWill.cs).</summary>
        public bool guestWill = true;
        /// <summary>Hospitality's guests may be the strangers of Rimfolk's visitor stories (Strangers/GuestStories.cs).</summary>
        public bool guestStories = true;
        /// <summary>Hospitality's guest tab says whether a guest can be recruited, how, and what it takes (Integrations/GuestCheck.cs).</summary>
        public bool guestCheck = true;
        /// <summary>With Hospitality: a guest group is no bigger than the free guest beds (Integrations/GuestGroupBeds.cs).</summary>
        public bool guestGroupsFitBeds = true;
        /// <summary>How many guests beyond the free guest beds a group may now and then bring (Integrations/GuestGroupBeds.cs).</summary>
        public int guestBedsExtra = 2;
        /// <summary>With Move the Patient: patients take the best free medical bed, and a lighter case is moved to another bed for someone worse off (Integrations/MedicalTriage.cs).</summary>
        public bool medicalTriage = true;
        /// <summary>What PokéWorld's Pokémon sound like (Integrations/PokeWorldSounds.cs): the nearest animal's calls, their own a third as often, or their own.</summary>
        public PokemonSounds pokemonSounds = PokemonSounds.Creatures;
        /// <summary>With PokéWorld: the colony's own Pokémon do a little at home by their type (Integrations/PokeWorldTypes.cs); its parts below.</summary>
        public bool pokemonTypes = true;
        /// <summary>A Fire type warms the room it rests in, an Ice type cools a hot one.</summary>
        public bool pokemonTypeWarmth = true;
        /// <summary>A Grass type quickens the crops close by.</summary>
        public bool pokemonTypeGrowth = true;
        /// <summary>An Electric type tops up a battery of the colony's it rests beside.</summary>
        public bool pokemonTypeBattery = true;
        /// <summary>A Psychic type calms a colonist close by who is near a mental break.</summary>
        public bool pokemonTypeCalm = true;
        /// <summary>A Fighting type spars with a colonist at their leisure.</summary>
        public bool pokemonTypeSpar = true;
        /// <summary>A Water type puts out a fire near it.</summary>
        public bool pokemonTypeDouse = true;
        /// <summary>With A RimWorld of Magic: a mage may bind one of the colony's animals as a familiar (Integrations/MagicFamiliars.cs).</summary>
        public bool familiars = true;
        /// <summary>An alert while an animal's fresh carcass lies out with nobody seeing to it (Animals/FreshCarcasses.cs).</summary>
        public bool freshCarcassAlert = true;
        /// <summary>An alert while one of the colony's animals is hungry with nothing it may eat in reach and no hunt to go on (Animals/PetHuntsPatches.cs).</summary>
        public bool noFoodAlert = true;
        /// <summary>The colony's next steps: a plan the council proposes to and keeps with the player (Building/NextSteps.cs).</summary>
        public bool nextSteps = true;

        // Room planner (the clearing options are remembered between games)
        public bool planCutPlants = true;
        public bool planMineRock = true;
        public bool planReplaceOwn = false;
        public bool planSmoothStone = false;
        public bool planFurniture = true;
        /// <summary>The game's build menu shows how much of each material the colony has (a deeper game hook).</summary>
        public bool buildMenuCounts = true;
        /// <summary>A priority button on the colony's blueprints and frames (Building/BuildPriority.cs).</summary>
        public bool buildPriorities = true;
        // The council's automations (Building/Automations.cs): each off unless the player switches it on.
        public bool autoBills = false;
        public bool autoPlan = false;
        public bool autoResearch = false;
        public bool autoStockpile = false;
        public bool autoTakeDown = false;
        public bool autoLamps = false;
        public bool autoFirewood = false;
        public bool autoGrow = false;
        /// <summary>Under the bills automation: which staples it sets bills for, and whether it follows the chains.</summary>
        public bool autoBillMeals = true;
        public bool autoBillMedicine = true;
        public bool autoBillComponents = true;
        public bool autoBillChains = true;
        /// <summary>The day's budget of materials (market worth) for the blueprints the automations place, and the daily caps on take-down orders and chopping marks.</summary>
        public float autoBudget = 200f;
        public int autoTakeDownsADay = 8;
        public int autoChopsADay = 10;
        /// <summary>Planning ahead (the Making page's Ahead section): days of meals to keep cooked, and days of food to keep in store beyond the days nothing grows.</summary>
        public float aheadMealDays = 2f;
        public float aheadReserveDays = 5f;
        /// <summary>A "By the weather" switch on each growing zone (off on each until switched on there): sowing there pauses while a crop sown now wouldn't pay by the forecast (Fields/FieldWeather.cs).</summary>
        public bool fieldsByWeather = true;
        /// <summary>A one-click suggestion to harvest the plants of an outdoor field that a frost in the next day or so would kill (Fields/FieldWeather.cs).</summary>
        public bool frostHarvestSuggest = true;
        /// <summary>The frost harvest is done by itself: the plants a coming frost would kill are marked for harvest as soon as the forecast shows it (Fields/FieldWeather.cs; the player's request).</summary>
        public bool frostHarvestAuto = true;
        // ---- The trade screen (Trade/): each part its own switch ----
        /// <summary>Rimfolk's own trade window in place of the game's trade screen, on the same trade and the game's own deal; a button there switches to the game's (Trade/TradeWindow.cs).</summary>
        public bool tradeWindow = true;
        /// <summary>Rimfolk's sorts in the trade screen's two sort menus: what the trader pays and asks, the price a kilo, the best deal, the suggestions first (Trade/TradeSorts.cs).</summary>
        public bool tradeSorts = true;
        /// <summary>A mark beside each good worth selling, worth buying or kept, with why (Trade/TradeAdvice.cs).</summary>
        public bool tradeMarks = true;
        /// <summary>The buttons that fill the suggested basket within the silver both sides have, and take it back out.</summary>
        public bool tradeBasket = true;
        /// <summary>The silver each side has after the trade, and the days of food after a trade at home.</summary>
        public bool tradeReadouts = true;
        /// <summary>The market card on a good's prices, the trend mark beside a good whose price moved, and the Market window on the silver row (Trade/TradeMarket.cs).</summary>
        public bool tradeMarket = true;
        /// <summary>The colony's own record of what it sold and bought, to whom and at what price a piece (Trade/TradeRecord.cs).</summary>
        public bool tradeRecord = true;
        /// <summary>With Vanilla Trading Expanded: its market, contracts and banks in the advice and the readouts (Integrations/VanillaTrading.cs).</summary>
        public bool tradeVte = true;
        /// <summary>With Vanilla Trading Expanded: its price history, trends and news, and what a trade does to its prices, on the trade screen.</summary>
        public bool tradeVteHistory = true;
        /// <summary>A "Reserved slots" button on the colony's shelves: a slot kept for each kind picked (Building/ShelfReserves.cs).</summary>
        public bool shelfReserves = true;
        /// <summary>A drink, smoke or snack had at a table or outside, not standing in a freezer or store room (Building/ChewSpots.cs).</summary>
        public bool chewOutOfStores = true;
        // ---- Carrying and hauling (Hauling/): the player's Harvest and Haul folded in, and the long material run ----
        /// <summary>Pawns put what their own work drops into their inventory and take it to storage themselves (Hauling/HarvestHaul.cs).</summary>
        public bool haulOwnWork = true;
        /// <summary>From harvesting, chopping and cutting plants, also a plant cut to clear a cell for sowing or building.</summary>
        public bool haulPlants = true;
        public bool haulMining = true;
        public bool haulDeepDrill = true;
        /// <summary>Wool, milk and the like from shearing and milking.</summary>
        public bool haulAnimals = true;
        /// <summary>The materials a building or floor gives back when it is taken apart.</summary>
        public bool haulDeconstruct = true;
        /// <summary>Whatever else a deconstruction leaves (chunks, fuel, its contents) is hauled by the same pawn right after.</summary>
        public bool haulLeftoversNow = true;
        /// <summary>Fish and other catches (Odyssey, and the mods' fishing jobs).</summary>
        public bool haulFishing = true;
        /// <summary>A bill's products at a bench (crafting, cooking, butchering, smelting and the rest) when the bill takes them to a stockpile (Hauling/WorkProducts.cs).</summary>
        public bool haulBills = true;
        /// <summary>What a production building gives up when a pawn empties or works it: barrels, egg boxes, processors, beehouses, harvesters.</summary>
        public bool haulBuildings = true;
        /// <summary>Anything else a work job makes beside the pawn: extracting, taking apart a mechanoid, a mod's work.</summary>
        public bool haulOtherWork = true;
        /// <summary>Stone and slag chunks too (off: one fills most of an inventory).</summary>
        public bool haulChunks = false;
        /// <summary>The share of a pawn's carrying capacity its inventory may be filled to.</summary>
        public float haulFill = 1f;
        /// <summary>A full pawn stops work for a trip to storage only with at least this many kg collected.</summary>
        public float haulMinTrip = 5f;
        /// <summary>A pawn carrying its load along while it keeps at the same kind of work delivers after this many hours anyway.</summary>
        public float haulMaxHours = 8f;
        /// <summary>Delivers before eating, sleeping, recreation or meditation (never when urgent).</summary>
        public bool haulBeforeNeeds = true;
        /// <summary>Colony mechanoids that can carry (agrihands, constructoids) take part too.</summary>
        public bool haulMechs = true;
        /// <summary>The long material run (Hauling/MaterialRun.cs): material fetched from far away comes for the whole build area, in the hands and the inventory.</summary>
        public bool materialRun = true;
        /// <summary>How far (cells, straight) the material must be from the build for a run.</summary>
        public float materialRunDistance = 30f;
        /// <summary>Loose things lying near a walk to storage that belong in storage are picked up on the way (Hauling/OnTheWay.cs).</summary>
        public bool haulOnTheWay = true;
        /// <summary>Writes each decision of carrying and hauling to the log (for bug reports).</summary>
        public bool haulLog = false;
        /// <summary>Whether the standalone Harvest and Haul's settings file was read into the rows above, so it is done once (Hauling/HaulLinks.cs).</summary>
        public bool haulImported = false;
        /// <summary>Rooms the player saved from a map ("Your rooms"), usable in every save.</summary>
        public System.Collections.Generic.List<CustomRoomData> savedRooms = new System.Collections.Generic.List<CustomRoomData>();
        /// <summary>The materials last picked in the room planner, "room::group=>material", used again while there's enough.</summary>
        public System.Collections.Generic.List<string> planMaterials = new System.Collections.Generic.List<string>();
        public ButtonPosition buttonPosition = ButtonPosition.BeforeMenu;
        /// <summary>The old switch for the rooms button, read once into <see cref="planButton"/> (the planning button took the rooms button's place).</summary>
        public bool roomsButton = true;
        /// <summary>The planning button in the bottom bar (UI/MainTabWindow_Planning.cs): the plan, the base, making, room plans and rooms.</summary>
        public bool planButton = true;
        /// <summary>
        /// The rooms and planning buttons became one (and the rooms and planning settings with them). False in a settings
        /// file from before, so the two are merged once on loading it; a new player's settings start merged.
        /// </summary>
        public bool planningMerged = true;
        /// <summary>A "defense" button in the bottom bar, next to the council's: the Defenses page in a window of its own.</summary>
        public bool defenseButton = true;
        /// <summary>A "region" button in the bottom bar: the Rimfolk map and everything beyond the colony (UI/MainTabWindow_World.cs).</summary>
        public bool worldButton = true;
        /// <summary>The council button on the bottom bar carries the count of what waits on the player, as This week's tab does (UI/Inbox.cs).</summary>
        public bool inboxCountOnButton = true;
        /// <summary>A More button on the bottom bar, for the buttons put away (UI/BarMore.cs; a deeper game hook).</summary>
        public bool barMore = true;
        /// <summary>The bottom bar's buttons put away under More, by def name (arranged in the game).</summary>
        public System.Collections.Generic.List<string> barPutAway = new System.Collections.Generic.List<string>();
        /// <summary>The council window's page when it was last closed, and the Almanac page, to open there again next time.</summary>
        public string lastCouncilPage = "";
        public string lastAlmanacPage = "";
        /// <summary>How large the Almanac's pages are written (its "Aa" buttons), and whether its lists are folded away.</summary>
        public AlmanacTextSize almanacTextSize = AlmanacTextSize.Normal;
        public bool almanacFolded = false;
        /// <summary>The Mood tab and the Moods page show every mood as a card ("Details") rather than one line each, switched on the pages themselves.</summary>
        public bool moodDetails = false;
        /// <summary>Where the council window opens: This week (UI/WeekPanel.cs), or the page last looked at.</summary>
        public CouncilOpensOn councilOpensOn = CouncilOpensOn.LastPage;
        /// <summary>Which page the planning window opens on with its button: Rooms, At a glance (the default) or the last page.</summary>
        public PlanningOpensOn planningOpensOn = PlanningOpensOn.AtAGlance;
        /// <summary>How many tiles each way from the colony the region map shows (UI/RegionMapView.cs), also set at the top of the map.</summary>
        public int regionMapRadius = 35;
        /// <summary>A name in Rimfolk's windows shows a small preview of its card when the mouse rests on it (UI/Cards/Previews.cs).</summary>
        public bool linkPreviews = true;
        /// <summary>How long the mouse rests on a name before its preview shows, in seconds.</summary>
        public float linkPreviewDelay = 0.35f;
        /// <summary>How large Rimfolk's four windows are: standard (1010 by 720) or large (up to 1280 by 860 where the screen has room).</summary>
        public WindowSizeChoice windowSize = WindowSizeChoice.Standard;
        /// <summary>The filters and sort orders the council's pages remember, as "page.key=value" (UI/PageMemory.cs).</summary>
        public System.Collections.Generic.List<string> councilPageState = new System.Collections.Generic.List<string>();
        // Rimfolk's own look and its window frame (docs/design/rimfolk-look.md, UI/WindowFrame.cs, UI/UiKitSkin.cs).
        /// <summary>Rimfolk's windows in a style of their own (rounded cards, flat buttons, tabs that read as tabs); off draws the game's look.</summary>
        public bool ownLook = true;
        /// <summary>Rimfolk's windows move by their title bar, size from their edges and go large; the region window floats.</summary>
        public bool windowsMove = true;
        /// <summary>Where each of Rimfolk's windows was left and how big, as "key=x;y;w;h" (UI/WindowFrameMath.cs), set by moving them.</summary>
        public System.Collections.Generic.List<string> windowPlaces = new System.Collections.Generic.List<string>();

        // On-screen panels and the hover card (Hud/). The food lines default to the advisor's own (Advisor: four days is low).
        public bool hudShown = true;
        public bool hudLock = false;
        // Off since 0.9.4: the panels paint beneath the game's interface, so a big window simply covers them (HudLayout.Migrate).
        public bool hudHideWithTabs = false;
        public bool hudHideOnWorld = true;
        public HudTheme hudTheme = HudTheme.Dark;
        public HudFont hudFont = HudFont.Normal;
        public float hudMoodMargin = 0.05f;
        public float hudFoodWarnDays = 10f;
        public float hudFoodBadDays = 4f;
        public bool hudHideGameAlerts = true;
        /// <summary>The inventory panel stands in for the game's resource list (Hud/Patch_HudGameResources.cs).</summary>
        public bool hudHideGameResources = true;
        /// <summary>The on-screen panels' settings version, for one-time changes to older settings (HudLayout.Migrate).</summary>
        public int hudVersion = HudLayout.SettingsVersion;
        public bool hoverCard = true;
        public float hoverDelay = 0.4f;
        public HudModifier hoverModifier = HudModifier.None;
        public HoverWho hoverWho = HoverWho.Colonists;
        public HoverPlace hoverPlace = HoverPlace.Cursor;
        public bool hoverMood = true;
        public bool hoverConcerns = true;
        public bool hoverDoing = true;
        public int hoverLines = 3;
        public float hoverScale = 1f;
        public float hoverOpacity = 0.9f;
        public bool hoverHideGameTip = true;
        /// <summary>Each panel's place and options (Hud/HudPanelState.cs), set on the panels themselves.</summary>
        public System.Collections.Generic.List<HudPanelState> hudPanels = new System.Collections.Generic.List<HudPanelState>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableAdvisor, "enableAdvisor", true);
            Scribe_Values.Look(ref enableWishes, "enableWishes", true);
            Scribe_Values.Look(ref enableAmbitions, "enableAmbitions", true);
            Scribe_Values.Look(ref enablePersonalTouches, "enablePersonalTouches", true);
            Scribe_Values.Look(ref stakes, "stakes", StakesMode.Balanced);
            Scribe_Values.Look(ref daysBetweenWishes, "daysBetweenWishes", 2f);
            Scribe_Values.Look(ref maxOpenWishes, "maxOpenWishes", 5);
            Scribe_Values.Look(ref wishDurationFactor, "wishDurationFactor", 1f);
            Scribe_Values.Look(ref rewardFactor, "rewardFactor", 1f);
            Scribe_Values.Look(ref penaltyFactor, "penaltyFactor", 1f);
            Scribe_Values.Look(ref jealousy, "jealousy", true);
            Scribe_Values.Look(ref selfHelp, "selfHelp", true);
            Scribe_Values.Look(ref wishesTakenBack, "wishesTakenBack", true);
            Scribe_Values.Look(ref takenBackGrace, "takenBackGrace", KeptGrace.Normal);
            Scribe_Values.Look(ref allowQuitting, "allowQuitting", true);
            Scribe_Values.Look(ref risingStandards, "risingStandards", true);
            Scribe_Values.Look(ref goodGrowsOrdinary, "goodGrowsOrdinary", true);
            Scribe_Values.Look(ref ordinaryMerged, "ordinaryMerged", false);
            if (Scribe.mode == LoadSaveMode.LoadingVars && !ordinaryMerged)
            {
                // The four rules that softened good moods are one now: on if any of them was, off only if all four were off.
                bool budget = true, comforts = true, fade = true, used = true;
                Scribe_Values.Look(ref budget, "smallJoysDiminish", true);
                Scribe_Values.Look(ref comforts, "standardsCoverComforts", true);
                Scribe_Values.Look(ref fade, "goodTimesFade", true);
                Scribe_Values.Look(ref used, "usedToGoodLife", true);
                goodGrowsOrdinary = GoodLifeMath.MergedSetting(budget, comforts, fade, used);
                ordinaryMerged = true;
            }
            Scribe_Values.Look(ref quietTroubles, "quietTroubles", true);
            Scribe_Values.Look(ref lifePressure, "lifePressure", LifePressure.Normal);
            Scribe_Values.Look(ref enableDilemmas, "enableDilemmas", true);
            Scribe_Values.Look(ref councilBanishment, "councilBanishment", true);
            // The old switches are written from the new choice (flavour as hints, the old default), and an old settings file
            // with the first off reads as "off" (on reads as hints).
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                showImpact = answerImpact != ImpactShow.Off;
                impactShows = answerImpact == ImpactShow.Flavour ? ImpactShow.Hints : answerImpact;
            }
            Scribe_Values.Look(ref showImpact, "showImpact", true);
            Scribe_Values.Look(ref impactShows, "impactShows", ImpactShow.Hints);
            if (Scribe.mode == LoadSaveMode.LoadingVars && !showImpact) impactShows = ImpactShow.Off;
            Scribe_Values.Look(ref answerImpact, "answerImpact", ImpactShow.Flavour);
            // A file from before the flavour line has no choice of its own: exact numbers and off carry over, and its hints
            // (the default then) become the flavour line, the default now. The flavour line is always saved as hints.
            if (Scribe.mode == LoadSaveMode.LoadingVars && answerImpact == ImpactShow.Flavour && impactShows != ImpactShow.Hints) answerImpact = impactShows;
            Scribe_Values.Look(ref annals, "annals", true);
            Scribe_Values.Look(ref decreeRequests, "decreeRequests", true);
            Scribe_Values.Look(ref anniversaries, "anniversaries", true);
            Scribe_Values.Look(ref colonyCalendar, "colonyCalendar", true);
            Scribe_Values.Look(ref answersPlayOut, "answersPlayOut", true);
            Scribe_Values.Look(ref goodsComeAndGo, "goodsComeAndGo", true);
            Scribe_Values.Look(ref epithets, "epithets", true);
            Scribe_Values.Look(ref journals, "journals", true);
            Scribe_Values.Look(ref offices, "offices", true);
            Scribe_Values.Look(ref electionEvent, "electionEvent", true);
            Scribe_Values.Look(ref socialWeb, "socialWeb", true);
            Scribe_Values.Look(ref bondsTakeTime, "bondsTakeTime", true);
            Scribe_Values.Look(ref chemistryLimits, "chemistryLimits", true);
            Scribe_Values.Look(ref comradesByDanger, "comradesByDanger", true);
            Scribe_Values.Look(ref deedsRemembered, "deedsRemembered", true);
            Scribe_Values.Look(ref seatFeelings, "seatFeelings", true);
            Scribe_Values.Look(ref loveFeelings, "loveFeelings", true);
            Scribe_Values.Look(ref socialTabColumn, "socialTabColumn", true);
            Scribe_Values.Look(ref socialLife, "socialLife", true);
            Scribe_Values.Look(ref traitChanges, "traitChanges", true);
            Scribe_Values.Look(ref faithLife, "faithLife", true);
            Scribe_Values.Look(ref socialEvents, "socialEvents", true);
            Scribe_Values.Look(ref dreams, "dreams", true);
            Scribe_Values.Look(ref aspirations, "aspirations", true);
            Scribe_Values.Look(ref arcFrequency, "arcFrequency", ArcFrequency.Normal);
            Scribe_Values.Look(ref secrets, "secrets", true);
            Scribe_Values.Look(ref vendettas, "vendettas", true);
            Scribe_Values.Look(ref eulogies, "eulogies", true);
            Scribe_Values.Look(ref quirks, "quirks", true);
            Scribe_Values.Look(ref heirlooms, "heirlooms", true);
            Scribe_Values.Look(ref comingOfAge, "comingOfAge", true);
            Scribe_Values.Look(ref strangers, "strangers", true);
            Scribe_Values.Look(ref nemeses, "nemeses", true);
            Scribe_Values.Look(ref nemesisReturns, "nemesisReturns", true);
            Scribe_Values.Look(ref petMoments, "petMoments", true);
            Scribe_Values.Look(ref sacredAnimals, "sacredAnimals", true);
            Scribe_Values.Look(ref omenAnimals, "omenAnimals", true);
            Scribe_Values.Look(ref animalTempers, "animalTempers", true);
            Scribe_Values.Look(ref animalMoments, "animalMoments", 1f);
            Scribe_Values.Look(ref animalBonds, "animalBonds", true);
            Scribe_Values.Look(ref speakingAnimals, "speakingAnimals", AnimalSpeech.Auto);
            Scribe_Values.Look(ref petsHunt, "petsHunt", true);
            Scribe_Values.Look(ref bodyWhenItDies, "bodyWhenItDies", true);
            Scribe_Values.Look(ref colonistMoments, "colonistMoments", true);
            Scribe_Values.Look(ref frictions, "frictions", true);
            Scribe_Values.Look(ref autonomy, "autonomy", true);
            Scribe_Values.Look(ref worldReactions, "worldReactions", true);
            Scribe_Values.Look(ref autonomyPlans, "autonomyPlans", true);
            Scribe_Values.Look(ref worldEvents, "worldEvents", true);
            Scribe_Values.Look(ref worldEventBattles, "worldEventBattles", true);
            Scribe_Values.Look(ref camps, "camps", true);
            Scribe_Values.Look(ref campCrowd, "campCrowd", CampCrowd.Usual);
            Scribe_Values.Look(ref missionsAsQuests, "missionsAsQuests", true);
            Scribe_Values.Look(ref daysBetweenWorldEvents, "daysBetweenWorldEvents", 4f);
            Scribe_Values.Look(ref eventsFromStoryteller, "eventsFromStoryteller", true);
            Scribe_Values.Look(ref storytellerEventWeight, "storytellerEventWeight", 1f);
            Scribe_Values.Look(ref worldSituations, "worldSituations", true);
            Scribe_Values.Look(ref daysBetweenWorldSituations, "daysBetweenWorldSituations", 45f);
            Scribe_Values.Look(ref factionMemory, "factionMemory", true);
            Scribe_Values.Look(ref notables, "notables", true);
            Scribe_Values.Look(ref neighbourLetters, "neighbourLetters", true);
            Scribe_Values.Look(ref post, "post", true);
            Scribe_Values.Look(ref postPace, "postPace", PostPace.Usual);
            Scribe_Values.Look(ref letterOccasions, "letterOccasions", true);
            Scribe_Values.Look(ref townDealings, "townDealings", true);
            Scribe_Values.Look(ref penFriends, "penFriends", true);
            Scribe_Values.Look(ref worldWrites, "worldWrites", true);
            Scribe_Values.Look(ref regionFortunes, "regionFortunes", true);
            Scribe_Values.Look(ref regionFaces, "regionFaces", true);
            Scribe_Values.Look(ref regionStock, "regionStock", true);
            Scribe_Values.Look(ref regionChange, "regionChange", true);
            Scribe_Values.Look(ref remoteTravellers, "remoteTravellers", 1f);
            Scribe_Values.Look(ref raidersCamps, "raidersCamps", 1f);
            Scribe_Values.Look(ref newPeoples, "newPeoples", 1f);
            Scribe_Values.Look(ref peopleAges, "peopleAges", true);
            Scribe_Values.Look(ref packAlphas, "packAlphas", 1f);
            Scribe_Values.Look(ref beastNemeses, "beastNemeses", 1f);
            Scribe_Values.Look(ref regionLegends, "regionLegends", 1f);
            Scribe_Values.Look(ref greatHerds, "greatHerds", 1f);
            Scribe_Values.Look(ref tradingPosts, "tradingPosts", true);
            Scribe_Values.Look(ref hamlets, "hamlets", true);
            Scribe_Values.Look(ref smallTownMaps, "smallTownMaps", true);
            Scribe_Values.Look(ref diplomacyFavours, "diplomacyFavours", true);
            Scribe_Values.Look(ref diplomacyPacts, "diplomacyPacts", true);
            Scribe_Values.Look(ref truceRaids, "truceRaids", true);
            Scribe_Values.Look(ref diplomacyRansom, "diplomacyRansom", true);
            Scribe_Values.Look(ref diplomacyEnvoys, "diplomacyEnvoys", true);
            Scribe_Values.Look(ref diplomacyFeasts, "diplomacyFeasts", true);
            Scribe_Values.Look(ref diplomacyMarriage, "diplomacyMarriage", true);
            Scribe_Values.Look(ref diplomacyWards, "diplomacyWards", true);
            Scribe_Values.Look(ref diplomacyFealty, "diplomacyFealty", true);
            Scribe_Values.Look(ref diplomacyRenown, "diplomacyRenown", true);
            Scribe_Values.Look(ref diplomacyNature, "diplomacyNature", true);
            Scribe_Values.Look(ref theirSchemes, "theirSchemes", 1f);
            Scribe_Values.Look(ref outsiderTalk, "outsiderTalk", true);
            Scribe_Values.Look(ref outsiderSpar, "outsiderSpar", true);
            Scribe_Values.Look(ref outsiderProvoke, "outsiderProvoke", true);
            Scribe_Values.Look(ref outsiderDuels, "outsiderDuels", true);
            Scribe_Values.Look(ref outsiderChatter, "outsiderChatter", true);
            Scribe_Values.Look(ref outsiderChatterFrequency, "outsiderChatterFrequency", 1f);
            Scribe_Values.Look(ref outsiderAuto, "outsiderAuto", OutsiderAutoMode.WithinLimits);
            Scribe_Values.Look(ref journeys, "journeys", true);
            Scribe_Values.Look(ref freshStarts, "freshStarts", true);
            Scribe_Values.Look(ref newcomers, "newcomers", true);
            Scribe_Values.Look(ref handOvers, "handOvers", true);
            Scribe_Values.Look(ref townLife, "townLife", true);
            Scribe_Values.Look(ref manyMerge, "manyMerge", true);
            Scribe_Values.Look(ref journeyShiftSleep, "journeyShiftSleep", true);
            Scribe_Values.Look(ref journeyClothes, "journeyClothes", true);
            Scribe_Values.Look(ref journeyFreeHours, "journeyFreeHours", 4);
            Scribe_Values.Look(ref dynamicWeather, "dynamicWeather", true);
            Scribe_Values.Look(ref weatherTemperature, "weatherTemperature", true);
            Scribe_Values.Look(ref weatherForecast, "weatherForecast", true);
            Scribe_Values.Look(ref rainWashesMore, "rainWashesMore", true);
            Scribe_Values.Look(ref scenarioThreat, "scenarioThreat", 1f);
            Scribe_Values.Look(ref encounterFrequency, "encounterFrequency", 1f);
            Scribe_Values.Look(ref momentFrequency, "momentFrequency", 1f);
            Scribe_Values.Look(ref socialEventFrequency, "socialEventFrequency", 1f);
            Scribe_Values.Look(ref dreamFrequency, "dreamFrequency", 1f);
            Scribe_Values.Look(ref colonyCharacter, "colonyCharacter", true);
            Scribe_Values.Look(ref interplay, "interplay", true);
            Scribe_Values.Look(ref heart, "heart", true);
            Scribe_Values.Look(ref heartEpisodes, "heartEpisodes", true);
            Scribe_Values.Look(ref heartInspector, "heartInspector", true);
            Scribe_Values.Look(ref heartGoodSide, "heartGoodSide", true);
            Scribe_Values.Look(ref strainSpeed, "strainSpeed", 1f);
            Scribe_Values.Look(ref ailments, "ailments", true);
            Scribe_Values.Look(ref seriousIllnesses, "seriousIllnesses", true);
            Scribe_Values.Look(ref illnessBudget, "illnessBudget", true);
            Scribe_Values.Look(ref crowdedSickness, "crowdedSickness", true);
            Scribe_Values.Look(ref frailty, "frailty", true);
            Scribe_Values.Look(ref dirtyWounds, "dirtyWounds", true);
            Scribe_Values.Look(ref endOfLife, "endOfLife", true);
            Scribe_Values.Look(ref movingOn, "movingOn", true);
            Scribe_Values.Look(ref ailmentFrequency, "ailmentFrequency", 1f);
            Scribe_Values.Look(ref everydayHurts, "everydayHurts", 1f);
            Scribe_Values.Look(ref convalescence, "convalescence", true);
            Scribe_Values.Look(ref healerReputations, "healerReputations", true);
            Scribe_Values.Look(ref medicineByNeed, "medicineByNeed", true);
            Scribe_Values.Look(ref harmlessMedicine, "harmlessMedicine", HarmlessMedicine.None);
            Scribe_Values.Look(ref restByNeed, "restByNeed", true);
            Scribe_Values.Look(ref slowButtons, "slowButtons", true);
            Scribe_Values.Look(ref moodInsight, "moodInsight", true);
            Scribe_Values.Look(ref deepHooks, "deepHooks", true);
            Scribe_Values.Look(ref needsTabHints, "needsTabHints", true);
            Scribe_Values.Look(ref styleNotes, "styleNotes", true);
            Scribe_Values.Look(ref letterNotes, "letterNotes", true);
            Scribe_Values.Look(ref barBadges, "barBadges", true);
            Scribe_Values.Look(ref barBreakMark, "barBreakMark", BarBreakMark.Ring);
            Scribe_Values.Look(ref barBreakMargin, "barBreakMargin", 0.05f);
            Scribe_Values.Look(ref barBadgeLeaving, "barBadgeLeaving", true);
            Scribe_Values.Look(ref barBadgeWish, "barBadgeWish", true);
            Scribe_Values.Look(ref barWishPatience, "barWishPatience", 0.5f);
            Scribe_Values.Look(ref barBadgeStrain, "barBadgeStrain", true);
            Scribe_Values.Look(ref barBadgeFaith, "barBadgeFaith", true);
            Scribe_Values.Look(ref barBadgePromise, "barBadgePromise", true);
            Scribe_Values.Look(ref barBadgeSize, "barBadgeSize", BarBadgeSize.Normal);
            Scribe_Values.Look(ref barBadgeCorner, "barBadgeCorner", BarBadgeCorner.TopRight);
            Scribe_Values.Look(ref barBadgeMax, "barBadgeMax", 2);
            Scribe_Values.Look(ref slowButtonsX, "slowButtonsX", -1f);
            Scribe_Values.Look(ref slowButtonsY, "slowButtonsY", -1f);
            Scribe_Values.Look(ref dreamOmens, "dreamOmens", true);
            Scribe_Values.Look(ref allDreamMessages, "allDreamMessages", false);
            Scribe_Values.Look(ref messageLevel, "messageLevel", MessageLevel.Notable);
            Scribe_Values.Look(ref colonyNews, "colonyNews", NewsMode.Digest);
            Scribe_Values.Look(ref messagesADay, "messagesADay", TellMath.DefaultMessagesADay);
            Scribe_Values.Look(ref lettersADay, "lettersADay", TellMath.DefaultLettersADay);
            Scribe_Values.Look(ref gatheringsAWeek, "gatheringsAWeek", StageMath.DefaultAWeek);
            Scribe_Values.Look(ref era, "era", EraMode.Auto);
            Scribe_Values.Look(ref daysBetweenDilemmas, "daysBetweenDilemmas", 10f);
            Scribe_Values.Look(ref eventWishes, "eventWishes", true);
            Scribe_Values.Look(ref reminders, "reminders", true);
            Scribe_Values.Look(ref wishesForOthers, "wishesForOthers", true);
            Scribe_Values.Look(ref petitions, "petitions", true);
            Scribe_Values.Look(ref wishLetters, "wishLetters", true);
            Scribe_Values.Look(ref grantedLetters, "grantedLetters", false);
            Scribe_Values.Look(ref whatsNewLetter, "whatsNewLetter", true);
            Scribe_Values.Look(ref lastSeenVersion, "lastSeenVersion", "");
            // The old switch is written from the new choice, and an old settings file with it off reads as "off".
            if (Scribe.mode == LoadSaveMode.Saving) speechText = speechShows != SpeechMode.Off;
            Scribe_Values.Look(ref speechText, "speechText", true);
            Scribe_Values.Look(ref speechShows, "speechShows", SpeechMode.Bubbles);
            if (Scribe.mode == LoadSaveMode.LoadingVars && !speechText) speechShows = SpeechMode.Off;
            Scribe_Values.Look(ref visualTouches, "visualTouches", true);
            Scribe_Values.Look(ref moodBubbleText, "moodBubbleText", true);
            Scribe_Values.Look(ref smallHabits, "smallHabits", true);
            Scribe_Values.Look(ref daysBetweenMeetings, "daysBetweenMeetings", 15f);
            Scribe_Values.Look(ref ambitionDeadlineFactor, "ambitionDeadlineFactor", 1f);
            Scribe_Values.Look(ref meetingsCallFromWork, "meetingsCallFromWork", true);
            Scribe_Values.Look(ref daysBetweenPersonalTouches, "daysBetweenPersonalTouches", 6f);
            Scribe_Values.Look(ref advisorAlert, "advisorAlert", true);
            Scribe_Values.Look(ref wishAlert, "wishAlert", true);
            Scribe_Values.Look(ref defenseGateReminder, "defenseGateReminder", true);
            Scribe_Values.Look(ref coverDrag, "coverDrag", true);
            Scribe_Values.Look(ref marketStallWindow, "marketStallWindow", true);
            Scribe_Values.Look(ref marketStallShips, "marketStallShips", true);
            Scribe_Values.Look(ref coveredLooks, "coveredLooks", true);
            Scribe_Values.Look(ref magicGolemRepair, "magicGolemRepair", true);
            Scribe_Values.Look(ref magicNeedRules, "magicNeedRules", true);
            Scribe_Values.Look(ref territoryNewsQuiet, "territoryNewsQuiet", true);
            Scribe_Values.Look(ref faithTurretsHere, "faithTurretsHere", true);
            Scribe_Values.Look(ref drinkBeforeWork, "drinkBeforeWork", true);
            Scribe_Values.Look(ref drinkBeforeHotWork, "drinkBeforeHotWork", true);
            Scribe_Values.Look(ref smokeBeforeWork, "smokeBeforeWork", true);
            Scribe_Values.Look(ref marketGuestsShop, "marketGuestsShop", true);
            Scribe_Values.Look(ref marketCampersBuy, "marketCampersBuy", true);
            Scribe_Values.Look(ref guestWill, "guestWill", true);
            Scribe_Values.Look(ref guestStories, "guestStories", true);
            Scribe_Values.Look(ref guestCheck, "guestCheck", true);
            Scribe_Values.Look(ref guestGroupsFitBeds, "guestGroupsFitBeds", true);
            Scribe_Values.Look(ref guestBedsExtra, "guestBedsExtra", 2);
            Scribe_Values.Look(ref medicalTriage, "medicalTriage", true);
            Scribe_Values.Look(ref pokemonSounds, "pokemonSounds", PokemonSounds.Creatures);
            Scribe_Values.Look(ref pokemonTypes, "pokemonTypes", true);
            Scribe_Values.Look(ref pokemonTypeWarmth, "pokemonTypeWarmth", true);
            Scribe_Values.Look(ref pokemonTypeGrowth, "pokemonTypeGrowth", true);
            Scribe_Values.Look(ref pokemonTypeBattery, "pokemonTypeBattery", true);
            Scribe_Values.Look(ref pokemonTypeCalm, "pokemonTypeCalm", true);
            Scribe_Values.Look(ref pokemonTypeSpar, "pokemonTypeSpar", true);
            Scribe_Values.Look(ref pokemonTypeDouse, "pokemonTypeDouse", true);
            Scribe_Values.Look(ref familiars, "familiars", true);
            Scribe_Values.Look(ref freshCarcassAlert, "freshCarcassAlert", true);
            Scribe_Values.Look(ref noFoodAlert, "noFoodAlert", true);
            Scribe_Values.Look(ref nextSteps, "nextSteps", true);
            Scribe_Values.Look(ref planCutPlants, "planCutPlants", true);
            Scribe_Values.Look(ref planMineRock, "planMineRock", true);
            Scribe_Values.Look(ref planReplaceOwn, "planReplaceOwn", false);
            Scribe_Values.Look(ref planSmoothStone, "planSmoothStone", false);
            Scribe_Values.Look(ref planFurniture, "planFurniture", true);
            Scribe_Values.Look(ref buildMenuCounts, "buildMenuCounts", true);
            Scribe_Values.Look(ref buildPriorities, "buildPriorities", true);
            Scribe_Values.Look(ref autoBills, "autoBills");
            Scribe_Values.Look(ref autoPlan, "autoPlan");
            Scribe_Values.Look(ref autoResearch, "autoResearch");
            Scribe_Values.Look(ref autoStockpile, "autoStockpile");
            Scribe_Values.Look(ref autoTakeDown, "autoTakeDown");
            Scribe_Values.Look(ref autoLamps, "autoLamps");
            Scribe_Values.Look(ref autoFirewood, "autoFirewood");
            Scribe_Values.Look(ref autoGrow, "autoGrow");
            Scribe_Values.Look(ref autoBillMeals, "autoBillMeals", true);
            Scribe_Values.Look(ref autoBillMedicine, "autoBillMedicine", true);
            Scribe_Values.Look(ref autoBillComponents, "autoBillComponents", true);
            Scribe_Values.Look(ref autoBillChains, "autoBillChains", true);
            Scribe_Values.Look(ref autoBudget, "autoBudget", 200f);
            Scribe_Values.Look(ref autoTakeDownsADay, "autoTakeDownsADay", 8);
            Scribe_Values.Look(ref autoChopsADay, "autoChopsADay", 10);
            Scribe_Values.Look(ref aheadMealDays, "aheadMealDays", 2f);
            Scribe_Values.Look(ref aheadReserveDays, "aheadReserveDays", 5f);
            Scribe_Values.Look(ref fieldsByWeather, "fieldsByWeather", true);
            Scribe_Values.Look(ref frostHarvestSuggest, "frostHarvestSuggest", true);
            Scribe_Values.Look(ref frostHarvestAuto, "frostHarvestAuto", true);
            Scribe_Values.Look(ref tradeWindow, "tradeWindow", true);
            Scribe_Values.Look(ref tradeSorts, "tradeSorts", true);
            Scribe_Values.Look(ref tradeMarks, "tradeMarks", true);
            Scribe_Values.Look(ref tradeBasket, "tradeBasket", true);
            Scribe_Values.Look(ref tradeReadouts, "tradeReadouts", true);
            Scribe_Values.Look(ref tradeVte, "tradeVte", true);
            Scribe_Values.Look(ref tradeMarket, "tradeMarket", true);
            Scribe_Values.Look(ref tradeRecord, "tradeRecord", true);
            Scribe_Values.Look(ref tradeVteHistory, "tradeVteHistory", true);
            Scribe_Values.Look(ref shelfReserves, "shelfReserves", true);
            Scribe_Values.Look(ref chewOutOfStores, "chewOutOfStores", true);
            Scribe_Values.Look(ref haulOwnWork, "haulOwnWork", true);
            Scribe_Values.Look(ref haulPlants, "haulPlants", true);
            Scribe_Values.Look(ref haulMining, "haulMining", true);
            Scribe_Values.Look(ref haulDeepDrill, "haulDeepDrill", true);
            Scribe_Values.Look(ref haulAnimals, "haulAnimals", true);
            Scribe_Values.Look(ref haulDeconstruct, "haulDeconstruct", true);
            Scribe_Values.Look(ref haulLeftoversNow, "haulLeftoversNow", true);
            Scribe_Values.Look(ref haulFishing, "haulFishing", true);
            Scribe_Values.Look(ref haulChunks, "haulChunks", false);
            Scribe_Values.Look(ref haulBills, "haulBills", true);
            Scribe_Values.Look(ref haulBuildings, "haulBuildings", true);
            Scribe_Values.Look(ref haulOtherWork, "haulOtherWork", true);
            Scribe_Values.Look(ref haulFill, "haulFill", 1f);
            Scribe_Values.Look(ref haulMinTrip, "haulMinTrip", 5f);
            Scribe_Values.Look(ref haulMaxHours, "haulMaxHours", 8f);
            Scribe_Values.Look(ref haulBeforeNeeds, "haulBeforeNeeds", true);
            Scribe_Values.Look(ref haulMechs, "haulMechs", true);
            Scribe_Values.Look(ref materialRun, "materialRun", true);
            Scribe_Values.Look(ref materialRunDistance, "materialRunDistance", 30f);
            Scribe_Values.Look(ref haulOnTheWay, "haulOnTheWay", true);
            Scribe_Values.Look(ref haulLog, "haulLog", false);
            Scribe_Values.Look(ref haulImported, "haulImported", false);
            Scribe_Collections.Look(ref savedRooms, "savedRooms", LookMode.Deep);
            Scribe_Collections.Look(ref planMaterials, "planMaterials", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit || Scribe.mode == LoadSaveMode.LoadingVars)
            {
                savedRooms ??= new System.Collections.Generic.List<CustomRoomData>();
                savedRooms.RemoveAll(r => r == null);
                planMaterials ??= new System.Collections.Generic.List<string>();
            }
            Scribe_Values.Look(ref buttonPosition, "buttonPosition", ButtonPosition.BeforeMenu);
            Scribe_Values.Look(ref roomsButton, "roomsButton", true);
            Scribe_Values.Look(ref planButton, "planButton", true);
            Scribe_Values.Look(ref defenseButton, "defenseButton", true);
            Scribe_Values.Look(ref worldButton, "worldButton", true);
            Scribe_Values.Look(ref inboxCountOnButton, "inboxCountOnButton", true);
            Scribe_Values.Look(ref barMore, "barMore", true);
            Scribe_Collections.Look(ref barPutAway, "barPutAway", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit || Scribe.mode == LoadSaveMode.LoadingVars) barPutAway = BarMoreMath.Clean(barPutAway);
            Scribe_Values.Look(ref planningMerged, "planningMerged", false);
            if (Scribe.mode == LoadSaveMode.LoadingVars && !planningMerged)
            {
                // The rooms and planning buttons are one now: on the bar if either was, put away only if both were.
                barPutAway = BarMoreMath.MergePlanning(barPutAway, roomsButton, planButton, out planButton);
                planningMerged = true;
            }
            Scribe_Values.Look(ref lastCouncilPage, "lastCouncilPage", "");
            Scribe_Values.Look(ref lastAlmanacPage, "lastAlmanacPage", "");
            Scribe_Values.Look(ref almanacTextSize, "almanacTextSize", AlmanacTextSize.Normal);
            Scribe_Values.Look(ref almanacFolded, "almanacFolded", false);
            Scribe_Values.Look(ref moodDetails, "moodDetails", false);
            Scribe_Values.Look(ref councilOpensOn, "councilOpensOn", CouncilOpensOn.LastPage);
            Scribe_Values.Look(ref planningOpensOn, "planningOpensOn", PlanningOpensOn.AtAGlance);
            Scribe_Values.Look(ref regionMapRadius, "regionMapRadius", 35);
            Scribe_Values.Look(ref linkPreviews, "linkPreviews", true);
            Scribe_Values.Look(ref linkPreviewDelay, "linkPreviewDelay", 0.35f);
            Scribe_Values.Look(ref windowSize, "windowSize", WindowSizeChoice.Standard);
            Scribe_Collections.Look(ref councilPageState, "councilPageState", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit || Scribe.mode == LoadSaveMode.LoadingVars)
                councilPageState ??= new System.Collections.Generic.List<string>();
            Scribe_Values.Look(ref ownLook, "ownLook", true);
            Scribe_Values.Look(ref windowsMove, "windowsMove", true);
            Scribe_Collections.Look(ref windowPlaces, "windowPlaces", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit || Scribe.mode == LoadSaveMode.LoadingVars)
                windowPlaces ??= new System.Collections.Generic.List<string>();
            Scribe_Values.Look(ref hudShown, "hudShown", true);
            Scribe_Values.Look(ref hudLock, "hudLock", false);
            Scribe_Values.Look(ref hudHideWithTabs, "hudHideWithTabs", false);
            Scribe_Values.Look(ref hudHideOnWorld, "hudHideOnWorld", true);
            Scribe_Values.Look(ref hudTheme, "hudTheme", HudTheme.Dark);
            Scribe_Values.Look(ref hudFont, "hudFont", HudFont.Normal);
            Scribe_Values.Look(ref hudMoodMargin, "hudMoodMargin", 0.05f);
            Scribe_Values.Look(ref hudFoodWarnDays, "hudFoodWarnDays", 10f);
            Scribe_Values.Look(ref hudFoodBadDays, "hudFoodBadDays", 4f);
            Scribe_Values.Look(ref hudHideGameAlerts, "hudHideGameAlerts", true);
            Scribe_Values.Look(ref hudHideGameResources, "hudHideGameResources", true);
            Scribe_Values.Look(ref hudVersion, "hudVersion", 0);
            Scribe_Values.Look(ref hoverCard, "hoverCard", true);
            Scribe_Values.Look(ref hoverDelay, "hoverDelay", 0.4f);
            Scribe_Values.Look(ref hoverModifier, "hoverModifier", HudModifier.None);
            Scribe_Values.Look(ref hoverWho, "hoverWho", HoverWho.Colonists);
            Scribe_Values.Look(ref hoverPlace, "hoverPlace", HoverPlace.Cursor);
            Scribe_Values.Look(ref hoverMood, "hoverMood", true);
            Scribe_Values.Look(ref hoverConcerns, "hoverConcerns", true);
            Scribe_Values.Look(ref hoverDoing, "hoverDoing", true);
            Scribe_Values.Look(ref hoverLines, "hoverLines", 3);
            Scribe_Values.Look(ref hoverScale, "hoverScale", 1f);
            Scribe_Values.Look(ref hoverOpacity, "hoverOpacity", 0.9f);
            Scribe_Values.Look(ref hoverHideGameTip, "hoverHideGameTip", true);
            Scribe_Collections.Look(ref hudPanels, "hudPanels", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit || Scribe.mode == LoadSaveMode.LoadingVars)
            {
                hudPanels ??= new System.Collections.Generic.List<HudPanelState>();
                hudPanels.RemoveAll(p => p == null || p.id.NullOrEmpty());
            }
        }

        /// <summary>Saves the settings file right away (used by the planner's checkboxes).</summary>
        public static void SaveNow()
        {
            try
            {
                LoadedModManager.GetMod<ColonyCouncilMod>()?.WriteSettings();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("saving settings", e);
            }
        }

        public bool MoodEffects => stakes != StakesMode.AdvisorOnly;

        /// <summary>Downsides are on (Balanced or Demanding).</summary>
        public bool Negatives => stakes == StakesMode.Balanced || stakes == StakesMode.Demanding;

        /// <summary>The harsh extras: sulking, tantrums, stronger penalties.</summary>
        public bool Harsh => stakes == StakesMode.Demanding;

        /// <summary>Multiplier for every penalty: 0 in Gentle, scaled by the slider.</summary>
        public float PenaltyScale => !Negatives ? 0f : (Harsh ? 1.5f : 1f) * penaltyFactor;

        /// <summary>For downsides the player picked knowingly (decrees): half strength in Gentle instead of none.</summary>
        public float ChosenPenaltyScale => !MoodEffects ? 0f : Negatives ? PenaltyScale : 0.5f;

        /// <summary>The settings window, drawn page by page from the catalog (UI/SettingsWindow.cs, UI/SettingsCatalog.cs).</summary>
        public void DoWindowContents(Rect inRect) => SettingsWindow.Draw(this, inRect);
    }
}
