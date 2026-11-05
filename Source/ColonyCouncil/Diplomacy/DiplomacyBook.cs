using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// An offer for a captive, either way (Ransoms.cs; docs/design/stage2-diplomacy.md, item 3): their price for one of
    /// theirs in the colony's cells, the game's demand for one of the colony's by their messenger, or an exchange of the
    /// two. Kept on its home's part until it is answered and done, or the captive is gone.
    /// </summary>
    public class RansomOffer : IExposable
    {
        public int id;
        public RansomKind kind;
        public RansomStage stage;
        /// <summary>One of theirs in the colony's cells (an offer of theirs, an exchange).</summary>
        public Pawn captive;
        /// <summary>One of the colony's they hold (their demand, an exchange).</summary>
        public Pawn ours;
        public Faction people;
        /// <summary>The price in silver (their offer, the game's fee); a tribe pays it in <see cref="goods"/>.</summary>
        public int price;
        /// <summary>A thing's def name when they pay in goods, else none (silver).</summary>
        public string goods;
        /// <summary>When their messenger sets out (Waiting), when the colony's own walks in (Returning).</summary>
        public int due = -1;
        /// <summary>When their messenger leaves the gate without an answer; when a waiting offer is given up.</summary>
        public int answerBy = -1;
        public int refusals;
        /// <summary>The colony wrote to them about it.</summary>
        public bool asked;
        /// <summary>Their word came by a call at the comms console; their porters bring the price once it is agreed.</summary>
        public bool call;
        public int created;
        public string captiveName, oursName;

        public bool Has(Pawn p) => p != null && (captive == p || ours == p);

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (captive != null && captive.Discarded) captive = null;
                if (ours != null && ours.Discarded) ours = null;
            }
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_References.Look(ref captive, "captive");
            Scribe_References.Look(ref ours, "ours");
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref price, "price");
            Scribe_Values.Look(ref goods, "goods");
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref answerBy, "answerBy", -1);
            Scribe_Values.Look(ref refusals, "refusals");
            Scribe_Values.Look(ref asked, "asked");
            Scribe_Values.Look(ref call, "call");
            Scribe_Values.Look(ref created, "created");
            Scribe_Values.Look(ref captiveName, "captiveName");
            Scribe_Values.Look(ref oursName, "oursName");
        }
    }

    /// <summary>
    /// Their envoy's season at a home (Envoys.cs; item 4): who, with whom, since and until when, the bed set aside, how the
    /// stay goes (its score, tallied day by day and night by night), and the letters sent home.
    /// </summary>
    public class EnvoyStay : IExposable
    {
        public Pawn envoy, attendant;
        public Faction people;
        public EnvoyStage stage;
        /// <summary>When it was agreed; when they are due (Coming); since and until when they stay.</summary>
        public int agreed = -1, due = -1, since = -1, until = -1;
        /// <summary>The colony asked for them by letter (else they offered).</summary>
        public bool asked;
        public Building_Bed bed;
        public float score;
        public int letters;
        /// <summary>The day last tallied, and that day's meals: of the colony's, fine ones, and whether they ate from their packs.</summary>
        public int day = -1, meals, fine;
        public bool packs;
        /// <summary>The night last counted (its day), and where they slept it.</summary>
        public int nightDay = -1;
        public EnvoyNight night;
        /// <summary>What the outsiders' talks and gifts stood at when last tallied; the injuries seen; the meeting they last sat in at.</summary>
        public float warmth;
        public int gifts, injuries, satIn = -1;
        public string envoyName;

        public bool Here => stage == EnvoyStage.Staying && envoy != null && envoy.Spawned;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (envoy != null && envoy.Discarded) envoy = null;
                if (attendant != null && attendant.Discarded) attendant = null;
                if (bed != null && bed.Destroyed) bed = null;
            }
            Scribe_References.Look(ref envoy, "envoy");
            Scribe_References.Look(ref attendant, "attendant");
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref agreed, "agreed", -1);
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref until, "until", -1);
            Scribe_Values.Look(ref asked, "asked");
            Scribe_References.Look(ref bed, "bed");
            Scribe_Values.Look(ref score, "score");
            Scribe_Values.Look(ref letters, "letters");
            Scribe_Values.Look(ref day, "day", -1);
            Scribe_Values.Look(ref meals, "meals");
            Scribe_Values.Look(ref fine, "fine");
            Scribe_Values.Look(ref packs, "packs");
            Scribe_Values.Look(ref nightDay, "nightDay", -1);
            Scribe_Values.Look(ref night, "night");
            Scribe_Values.Look(ref warmth, "warmth");
            Scribe_Values.Look(ref gifts, "gifts");
            Scribe_Values.Look(ref injuries, "injuries");
            Scribe_Values.Look(ref satIn, "satIn", -1);
            Scribe_Values.Look(ref envoyName, "envoyName");
        }
    }

    /// <summary>
    /// The colony's own envoy at their town for a season (EnvoysAbroad.cs; item 4): a colonist good with people, the town,
    /// since and until when, the letters they sent.
    /// </summary>
    public class EnvoyAbroad : IExposable
    {
        public Pawn colonist;
        public Faction people;
        /// <summary>Their town's world object ID and its name.</summary>
        public int town = -1;
        public string townName;
        public EnvoyStage stage;
        public int agreed = -1, since = -1, until = -1;
        public int letters;
        public string name;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && colonist != null && colonist.Discarded) colonist = null;
            Scribe_References.Look(ref colonist, "colonist");
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_Values.Look(ref townName, "townName");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref agreed, "agreed", -1);
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref until, "until", -1);
            Scribe_Values.Look(ref letters, "letters");
            Scribe_Values.Look(ref name, "name");
        }
    }

    /// <summary>
    /// A ward or a hostage, either way (Wards.cs, WardsAbroad.cs, Hostages.cs; docs/design/stage2-diplomacy.md, item 7):
    /// their youth or hostage living at the colony as the game's lodger (its quest), or one of the colony's living at their
    /// town as a lent colonist. Its tie (Ward or Hostage) is the ledger's; this keeps what isn't a tie: who and where, the
    /// guardian and the skill, the year's tally, the letters. Kept on its home's part until they are home again.
    /// </summary>
    public class WardStay : IExposable
    {
        public int id;
        public WardKind kind;
        public WardStage stage;
        public Pawn pawn;
        public Faction people;
        /// <summary>Its Ward or Hostage tie in the ledger, and (a hostage) the truce or pact it guarantees.</summary>
        public int tie = -1, guarantees = -1;
        /// <summary>Their ward's teacher, and the skill (def name) taught there or learned with them.</summary>
        public Pawn guardian;
        public string skill;
        /// <summary>Ours away: their town's world object ID and name.</summary>
        public int town = -1;
        public string townName;
        public int agreed = -1, due = -1, since = -1, until = -1;
        /// <summary>Theirs: the game's quest that makes them a lodger.</summary>
        public int quest = -1;
        /// <summary>The year's tally: lessons given (and the day of the last), the skill's practice at the start, the days tallied, hungry, their mood summed, injuries seen and new ones.</summary>
        public int lessons, lessonDay = -1, dayTallied = -1, days, hungryDays, injuries, hurt;
        public float xpAtStart = -1f, moodSum;
        public int letters;
        /// <summary>The notable of theirs they are kin to (their parent), or -1.</summary>
        public int notable = -1;
        /// <summary>Ours with the Empire: a page in a titled household (regard only).</summary>
        public bool page;
        /// <summary>The colony asked for it by letter (else they offered, or it came with the terms).</summary>
        public bool asked;
        /// <summary>Theirs going home: their kin can't come or left without them, so they walk out alone.</summary>
        public bool alone;
        /// <summary>Ours: told to set out (fetching food for the road, walking to the edge).</summary>
        public bool ready;
        public string name;

        public bool Theirs => WardMath.IsTheirs(kind);

        public bool Hostage => WardMath.IsHostage(kind);

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (pawn != null && pawn.Discarded) pawn = null;
                if (guardian != null && guardian.Discarded) guardian = null;
            }
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref tie, "tie", -1);
            Scribe_Values.Look(ref guarantees, "guarantees", -1);
            Scribe_References.Look(ref guardian, "guardian");
            Scribe_Values.Look(ref skill, "skill");
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_Values.Look(ref townName, "townName");
            Scribe_Values.Look(ref agreed, "agreed", -1);
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref until, "until", -1);
            Scribe_Values.Look(ref quest, "quest", -1);
            Scribe_Values.Look(ref lessons, "lessons");
            Scribe_Values.Look(ref lessonDay, "lessonDay", -1);
            Scribe_Values.Look(ref dayTallied, "dayTallied", -1);
            Scribe_Values.Look(ref days, "days");
            Scribe_Values.Look(ref hungryDays, "hungryDays");
            Scribe_Values.Look(ref injuries, "injuries");
            Scribe_Values.Look(ref hurt, "hurt");
            Scribe_Values.Look(ref xpAtStart, "xpAtStart", -1f);
            Scribe_Values.Look(ref moodSum, "moodSum");
            Scribe_Values.Look(ref letters, "letters");
            Scribe_Values.Look(ref notable, "notable", -1);
            Scribe_Values.Look(ref page, "page");
            Scribe_Values.Look(ref asked, "asked");
            Scribe_Values.Look(ref alone, "alone");
            Scribe_Values.Look(ref ready, "ready");
            Scribe_Values.Look(ref name, "name");
        }
    }

    /// <summary>
    /// One home's part of diplomacy that isn't a tie (docs/design/stage2-diplomacy.md, "Shared rules"), kept by the
    /// council's rules for a colony's books (ColonyBook): its offers for captives (six at most), their envoy staying there
    /// and the colony's own envoy away (one each), and when an envoy was last offered to it.
    /// </summary>
    public class DiplomacyHome : IExposable, IColonyEntry
    {
        public int home = HomesMath.None;
        public int lostAt = ColonyStateMath.NotLost;

        public int Home
        {
            get => home;
            set => home = value;
        }

        public int LostAt
        {
            get => lostAt;
            set => lostAt = value;
        }

        /// <summary>Offers for captives, either way: RansomMath.MaxOffers at most, each dropped when done or its captive is gone.</summary>
        public List<RansomOffer> offers = new List<RansomOffer>();
        /// <summary>Their envoy's season here, agreed or under way; null when none.</summary>
        public EnvoyStay envoy;
        /// <summary>The colony's own envoy away at a town; null when none.</summary>
        public EnvoyAbroad abroad;
        /// <summary>When an envoy was last offered to this home, and by which people (its loadID), or -1.</summary>
        public int envoyOfferAt = -1, envoyOfferPeople = -1;
        /// <summary>Its feast with guests under way (Feasts.cs, release 2b.1), the last one (kept for its card), when the last was held, when the steward last suggested one.</summary>
        public GuestFeast feast, lastFeast;
        public int feastHeld = -1, feastSuggested = -1;
        /// <summary>Who of the colony's won something at a neighbour's feast, told when they are home (FeastMath.MaxTales at most).</summary>
        public List<FeastTale> tales = new List<FeastTale>();
        /// <summary>Matches under way (Matches.cs, release 2b.2): MatchMath.MaxPlans at most, each let go at the wedding or when called off.</summary>
        public List<MatchPlan> matches = new List<MatchPlan>();
        /// <summary>Wards and hostages either way (release 2b.3): WardMath.MaxPerHome at most, each dropped once they are home again.</summary>
        public List<WardStay> wards = new List<WardStay>();
        /// <summary>When a ward was last offered to this home, and by which people (its loadID), or -1.</summary>
        public int wardOfferAt = -1, wardOfferPeople = -1;
        /// <summary>
        /// Fealty (Fealty.cs, release 2c.2): the oath agreed with a people and their lord's retinue to come to the gate (their
        /// loadID, when they are due, when it was agreed, and whether they offered it), when a people last offered the colony
        /// its protection, and when a struggling town last asked for the colony's (once a year each).
        /// </summary>
        public int oathPeople = -1, oathDue = -1, oathAt = -1, fealtyOfferAt = -1, protectAskAt = -1;
        public bool oathOffered;
        /// <summary>The colony's own vassals (Vassals.cs): its hamlet once sworn and the towns under its protection, FealtyMath.MaxVassals at most.</summary>
        public List<VassalTerms> vassals = new List<VassalTerms>();

        public Map Map => home == HomesMath.None ? null : Homes.MapOf(home);

        public void ExposeData()
        {
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref lostAt, "lostAt", ColonyStateMath.NotLost);
            if (Scribe.mode == LoadSaveMode.Saving) offers?.RemoveAll(o => o == null);
            Scribe_Collections.Look(ref offers, "offers", LookMode.Deep);
            Scribe_Deep.Look(ref envoy, "envoy");
            Scribe_Deep.Look(ref abroad, "abroad");
            Scribe_Values.Look(ref envoyOfferAt, "envoyOfferAt", -1);
            Scribe_Values.Look(ref envoyOfferPeople, "envoyOfferPeople", -1);
            Scribe_Deep.Look(ref feast, "feast");
            Scribe_Deep.Look(ref lastFeast, "lastFeast");
            Scribe_Values.Look(ref feastHeld, "feastHeld", -1);
            Scribe_Values.Look(ref feastSuggested, "feastSuggested", -1);
            if (Scribe.mode == LoadSaveMode.Saving) tales?.RemoveAll(t => t == null || t.pawn == null);
            Scribe_Collections.Look(ref tales, "tales", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.Saving) matches?.RemoveAll(m => m == null || !m.Live);
            Scribe_Collections.Look(ref matches, "matches", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.Saving) wards?.RemoveAll(w => w == null);
            Scribe_Collections.Look(ref wards, "wards", LookMode.Deep);
            Scribe_Values.Look(ref wardOfferAt, "wardOfferAt", -1);
            Scribe_Values.Look(ref wardOfferPeople, "wardOfferPeople", -1);
            // An older save's council tasks ("tasks", "drillPeople", "drillAt": retired 5 November 2026) are left unread.
            Scribe_Values.Look(ref oathPeople, "oathPeople", -1);
            Scribe_Values.Look(ref oathDue, "oathDue", -1);
            Scribe_Values.Look(ref oathAt, "oathAt", -1);
            Scribe_Values.Look(ref oathOffered, "oathOffered");
            Scribe_Values.Look(ref fealtyOfferAt, "fealtyOfferAt", -1);
            Scribe_Values.Look(ref protectAskAt, "protectAskAt", -1);
            if (Scribe.mode == LoadSaveMode.Saving) vassals?.RemoveAll(v => v == null || v.town < 0);
            Scribe_Collections.Look(ref vassals, "vassals", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                offers ??= new List<RansomOffer>();
                offers.RemoveAll(o => o == null);
                tales ??= new List<FeastTale>();
                tales.RemoveAll(t => t == null || t.pawn == null);
                matches ??= new List<MatchPlan>();
                matches.RemoveAll(m => m == null || !m.Live);
                wards ??= new List<WardStay>();
                wards.RemoveAll(w => w == null);
                vassals ??= new List<VassalTerms>();
                vassals.RemoveAll(v => v == null || v.town < 0);
            }
        }
    }

    /// <summary>
    /// Diplomacy's records that aren't ties (release 2a.3 brings the first): one part per home (DiplomacyHome, kept by
    /// ColonyBook's rules: one colony plays as before, a second has its own). Nothing ticks here: the hour and the day ride
    /// on diplomacy's (Diplomacy.Hourly), which do nothing while nothing waits.
    /// </summary>
    public class DiplomacyBook : GameComponent
    {
        private List<DiplomacyHome> homes = new List<DiplomacyHome>();
        public int nextId = 1;
        /// <summary>The colony's band of renown last told (RenownBand; RenownMath.NotRead before the first day's look), so a change is told once; the renown itself is read, never kept (Diplomacy/Renown.cs).</summary>
        public int renownBand = RenownMath.NotRead;

        private static DiplomacyBook cached;
        private static Game cachedGame;

        public DiplomacyBook(Game game)
        {
        }

        public static DiplomacyBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<DiplomacyBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
        }

        /// <summary>A home's part (the seat's for a map that isn't a home), made when it first needs one.</summary>
        public DiplomacyHome HomeOf(Map map)
        {
            homes ??= new List<DiplomacyHome>();
            return ColonyBook.Entry(homes, map, () => new DiplomacyHome());
        }

        /// <summary>A home's part if it has one (none made).</summary>
        public DiplomacyHome Existing(Map map)
        {
            if (homes == null || map == null || !Homes.IsHome(map)) return null;
            return ColonyBook.ByKey(homes, Homes.KeyOf(map));
        }

        /// <summary>Every part of a home that is ours now (a new list).</summary>
        public List<DiplomacyHome> Live()
        {
            var list = new List<DiplomacyHome>();
            if (homes == null) return list;
            foreach (var h in homes)
                if (h != null && h.lostAt == ColonyStateMath.NotLost && h.Map != null)
                    list.Add(h);
            return list;
        }

        /// <summary>Something waits at some home: an offer, an envoy either way, a ward or a hostage, an oath of fealty awaited.</summary>
        public bool AnyWaiting
        {
            get
            {
                if (homes == null) return false;
                foreach (var h in homes)
                    if (h != null && (h.offers.Count > 0 || h.envoy != null || h.abroad != null || h.feast != null || h.tales.Count > 0 || h.matches.Count > 0 || h.wards.Count > 0
                                  || h.oathPeople >= 0))
                        return true;
                return false;
            }
        }

        public int NextId() => nextId++;

        /// <summary>Homes lost (Homes.Changed): a stay or an offer at a lost home ends; the part is kept a while, as the council's.</summary>
        internal void HomesLost(IReadOnlyList<int> lost)
        {
            if (lost == null || homes == null) return;
            foreach (var h in homes)
                if (h != null && lost.Contains(h.Home))
                {
                    try
                    {
                        Ransoms.HomeLost(h);
                        Envoys.HomeLost(h);
                        EnvoysAbroad.HomeLost(h);
                        Feasts.HomeLost(h);
                        Matches.HomeLost(h);
                        Wards.HomeLost(h);
                        Fealty.HomeLost(h);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("diplomacy: a home lost", e);
                    }
                }
            ColonyBook.Lost(homes, lost, null);
        }

        /// <summary>Once a day: a home that came back is ours again, and parts of homes lost long enough ago are let go.</summary>
        internal void Daily() => ColonyBook.Daily(homes, null);

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving) homes?.RemoveAll(h => h == null);
            Scribe_Collections.Look(ref homes, "homes", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            Scribe_Values.Look(ref renownBand, "renownBand", RenownMath.NotRead);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                homes ??= new List<DiplomacyHome>();
                homes.RemoveAll(h => h == null);
            }
        }
    }
}
