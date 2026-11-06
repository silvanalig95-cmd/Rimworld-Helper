using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>One tie between the two sides of a scene, as the facts read it (Facts.Ties).</summary>
    public struct TieFact
    {
        public Tie.Kind kind;
        /// <summary>The party bound (who owes, promised, swore, holds the grudge) is on our side.</summary>
        public bool oursBound;
        /// <summary>The speaker is one of its two parties.</summary>
        public bool you;
        /// <summary>Asked for now (called in).</summary>
        public bool calledIn;
        /// <summary>Days since it was made (a large number when not known).</summary>
        public float days;
        /// <summary>The party it is toward, by name.</summary>
        public string toName;
    }

    /// <summary>
    /// The facts service's small rules (Facts.cs is the game's side): which providers each scene asks for, how current a
    /// fact is, and the tags and placeholders of the ties, the history and the ages. Pure: Tools/Tests checks them.
    /// </summary>
    public static class FactsMath
    {
        // ---------------- Providers ----------------

        public const string Moment = "moment", People = "people", Persons = "persons", Pair = "pair", Town = "town", Land = "land", Notables = "notables",
            Places = "places", Wars = "wars", Situations = "situations", Conditions = "conditions", Colony = "colony", Hospital = "hospital", Deeds = "deeds",
            Marks = "marks", Ages = "ages", Ties = "ties", History = "history", News = "news", Region = "region";

        /// <summary>
        /// A conversation with someone from outside the colony: the outsider talks' sections in the order they were always
        /// read, then the ages, the ties, the history and the region (their town's fortune and founding, new towns near).
        /// </summary>
        public static readonly string[] Talk =
        {
            Moment, People, Persons, Pair, Town, Land, Notables, Places, Wars, Situations, Conditions, Colony, Hospital, Deeds, Marks, Ages, Ties, History, Region
        };

        private static readonly string[] LetterFacts = { Ties, History };
        private static readonly string[] LetterNews = { News, Ties, History };

        /// <summary>A letter to a neighbour: the ties and the history, and the world's news near them when it tells some.</summary>
        public static string[] Letter(bool news) => news ? LetterNews : LetterFacts;

        /// <summary>
        /// One of Rimfolk's events about a people (its letter's text): what they remember of the colony, the ties between
        /// the two sides, what the history holds of their town lately, and the region's word on their town.
        /// </summary>
        public static readonly string[] Event = { Deeds, Ties, History, Region };

        // ---------------- The region ----------------

        /// <summary>The region's tags (Facts.Region): their town's fortune and founding, a new town near the colony, and what else stands near it.</summary>
        public static readonly string[] RegionTags =
        {
            "town:struggling", "town:poor", "town:steady", "town:thriving", "town:flourishing", "town:new", "town:founder", "town:mother", "them:founder",
            "region:newtown", "region:newtown:theirs", "region:newtown:other",
            // Trading posts (World/Region/TradingPostFacts.cs): their town is one, its keeper has warned, theirs near the colony, another people's.
            "town:post", "town:post:warned", "their:post", "region:post",
            // Hamlets (World/Region/HamletFacts.cs): their town is the colony's hamlet (and has sworn fealty), theirs near the colony, another people's.
            "town:hamlet", "town:hamlet:sworn", "their:hamlet", "region:hamlet",
            // What stands near the colony and what their town has been through (World/Region/RegionNearFacts.cs): a raiders' camp the colony
            // knows of (at war with theirs), a ruin within reach (theirs, another people's, a fight's), what brought a traveller
            // (TravelMath.Reasons), their town on its own people's old walls or another's, a town of theirs given up lately, a world event at their town.
            "region:camp", "region:camp:theirfoe", "region:ruin", "region:ruin:theirs", "region:ruin:other", "region:ruin:fight",
            "them:seeking", "them:seeking:relic", "them:seeking:wonder", "them:seeking:trees", "them:seeking:ruins", "them:seeking:ruinRegion", "them:seeking:library",
            "them:seeking:history", "them:seeking:ore", "them:seeking:rush", "them:seeking:grazing", "them:seeking:road", "them:seeking:far",
            "town:resettled:own", "town:resettled:newcomers", "their:givenup", "town:situation", "town:situation:bad", "town:situation:good",
            // One of a camp of travellers at the colony (World/Region/RegionNearFacts.cs), and the camp's kind (CampTag).
            "them:camp", "them:camp:Hunters", "them:camp:Refugees", "them:camp:Prospectors", "them:camp:Pilgrims", "them:camp:Market", "them:camp:Smith",
            "them:camp:Hermit", "them:camp:Healers", "them:camp:Procession", "them:camp:Players", "them:camp:Surveyors", "them:camp:Fishers", "them:camp:Salvagers",
            "them:camp:HarvestHands", "them:camp:Spacers", "them:camp:Watchers", "them:camp:BeastShow", "them:camp:Trainers", "them:camp:SaltCaravan",
            "them:camp:FarMerchants", "them:camp:Drovers", "them:camp:Penitents", "them:camp:TreeSingers", "them:camp:LoneHealer", "them:camp:Minstrels",
            "them:camp:Mourners", "them:camp:Scholars", "them:camp:Settlers", "them:camp:WarParty", "them:camp:Wardens", "them:camp:Deserters",
            "them:camp:Woodcutters", "them:camp:HerbGatherers", "them:camp:Stonecutters", "them:camp:Shearers", "them:camp:Catchers", "them:camp:Teacher",
            "them:camp:Wounded"
        };

        /// <summary>The tag for one of a camp of this kind: them:camp: and the kind's defName without its Council_Camp_ or Rimfolk_Camp_ ("them:camp:SaltCaravan").</summary>
        public static string CampTag(string kindDefName)
        {
            if (string.IsNullOrEmpty(kindDefName)) return null;
            string k = kindDefName;
            foreach (var prefix in new[] { "Council_Camp_", "Rimfolk_Camp_" })
                if (k.StartsWith(prefix, StringComparison.Ordinal))
                {
                    k = k.Substring(prefix.Length);
                    break;
                }
            return "them:camp:" + k;
        }

        /// <summary>The region's placeholders: their town's fortune in a word and the world event behind it, its founder, mother town and why it was founded; a new town near and its people; a trading post near, its kind, its people and its keeper; a hamlet; a raiders' camp, a ruin, a traveller's reason, a town given up, the event at their town.</summary>
        public static readonly string[] RegionValues = { "FORTUNE", "FORTUNEWHY", "FOUNDER", "MOTHERTOWN", "WHYFOUNDED", "NEWTOWN", "NEWTOWNPEOPLE", "POST", "POSTKIND", "POSTPEOPLE", "KEEPER", "HAMLET", "HAMLETWORD",
            "HAMLETPEOPLE",
            // A raiders' camp near (its kind, its people), a ruin near (its old name, its people), what a traveller came for, a town of theirs given up
            // lately, the world event at their town (World/Region/RegionNearFacts.cs).
            "RAIDCAMP", "RAIDCAMPPEOPLE", "RUINNAME", "RUINPEOPLE", "SEEK", "GIVENUP", "TOWNSITUATION" };

        /// <summary>A new town's newness: current over its first year, the more so in its first weeks.</summary>
        public static float NewTownHeat(float days) => Fresh(days, 60f, 2f);

        // ---------------- How current ----------------

        /// <summary>How current something <paramref name="days"/> old is: <paramref name="top"/> when new, fading over <paramref name="span"/> days to a floor of 0.4.</summary>
        public static float Fresh(float days, float span, float top) => Max(0.4f, top * (1f - Clamp01(days / Max(0.1f, span))));

        // The game's own Mathf.Max and Clamp01, so a fact is exactly as current as it always was.
        private static float Max(float a, float b) => a > b ? a : b;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        // ---------------- Ties ----------------

        /// <summary>The kinds of tie the facts speak of: a favour owed, a promise, a claim, a debt, fealty, a grudge.</summary>
        public static readonly Tie.Kind[] TieKinds = { Tie.Kind.Favour, Tie.Kind.Promise, Tie.Kind.Claim, Tie.Kind.Debt, Tie.Kind.Fealty, Tie.Kind.Grudge };

        public static bool Speaks(Tie.Kind k) => Array.IndexOf(TieKinds, k) >= 0;

        /// <summary>A tie made in the last this many days is news.</summary>
        public const float TieNewDays = 10f;

        /// <summary>A tie's tag: tie:favour, tie:promise, tie:claim, tie:debt, tie:fealty, tie:grudge.</summary>
        public static string TieTag(Tie.Kind k) => "tie:" + HistoryMath.LowerFirst(k.ToString());

        /// <summary>
        /// The placeholder naming the party a tie is toward: the colonist a favour is owed to (FAVOURFOR), the one
        /// promised (PROMISEDTO), the one a family has a claim on (CLAIMON), whom a debt is owed (DEBTTO), fealty sworn
        /// (FEALTYTO), a grudge held against (GRUDGEAGAINST).
        /// </summary>
        public static string TieKey(Tie.Kind k)
        {
            switch (k)
            {
                case Tie.Kind.Favour: return "FAVOURFOR";
                case Tie.Kind.Promise: return "PROMISEDTO";
                case Tie.Kind.Claim: return "CLAIMON";
                case Tie.Kind.Debt: return "DEBTTO";
                case Tie.Kind.Fealty: return "FEALTYTO";
                case Tie.Kind.Grudge: return "GRUDGEAGAINST";
                default: return null;
            }
        }

        /// <summary>Of two ties of a kind, the one a scene speaks of: one the speaker is party to, then the newer.</summary>
        public static bool Before(TieFact a, TieFact b) => a.you != b.you ? a.you : a.days < b.days;

        /// <summary>
        /// A tie's facts: tie:KIND (current, more so when new), tie:KIND:ours or tie:KIND:theirs (which side is bound),
        /// tie:KIND:you (the speaker is a party to it), tie:KIND:due (asked for now), and the name of the party it is toward.
        /// </summary>
        public static void AddTie(FactSet f, TieFact t)
        {
            if (f == null || !Speaks(t.kind)) return;
            string tag = TieTag(t.kind);
            f.Tag(tag, t.days <= TieNewDays ? Fresh(t.days, TieNewDays, 2f) : 1f);
            f.Tag(tag + (t.oursBound ? ":ours" : ":theirs"));
            if (t.you) f.Tag(tag + ":you", 2f);
            if (t.calledIn) f.Tag(tag + ":due", 2f);
            f.Value(TieKey(t.kind), t.toName);
        }

        /// <summary>
        /// A debt of honour (a Favour tie of origin "debts", Diplomacy/FavoursOwed.cs) speaks as its own family, so the talk
        /// of a favour to bring on the next visit (tie:favour) never takes one for the other: tie:honour, tie:honour:ours or
        /// tie:honour:theirs (which side owes), tie:honour:you (the speaker is owed, or owes, in person), tie:honour:due (called
        /// in), the party owed by name ({HONOURTO}) and what it was for ({FAVOURWHY}: "a prisoner freed", "relief sent").
        /// </summary>
        public static readonly string HonourTag = "tie:honour";

        public static void AddHonour(FactSet f, TieFact t, string why)
        {
            if (f == null) return;
            f.Tag(HonourTag, t.days <= TieNewDays ? Fresh(t.days, TieNewDays, 2f) : 1f);
            f.Tag(HonourTag + (t.oursBound ? ":ours" : ":theirs"));
            if (t.you) f.Tag(HonourTag + ":you", 2f);
            if (t.calledIn) f.Tag(HonourTag + ":due", 2f);
            f.Value("HONOURTO", t.toName);
            if (!string.IsNullOrEmpty(why)) f.Value("FAVOURWHY", why);
        }

        // ---------------- The history ----------------

        /// <summary>What happened in the last this many days is "lately".</summary>
        public const float LatelyDays = 30f;

        /// <summary>The newest this many events of each are read.</summary>
        public const int LatelyMax = 12;

        /// <summary>
        /// What the history holds of a town or a person lately (<paramref name="who"/>: "town" or "them"): WHO:lately, and
        /// WHO:lately:KIND for each event's kind and each family it belongs to (town:lately:world, town:lately:world:taken),
        /// the newer the more current. Events older than <see cref="LatelyDays"/> are left out.
        /// </summary>
        public static void AddLately(FactSet f, string who, IEnumerable<(string kind, float days)> events)
        {
            if (f == null || string.IsNullOrEmpty(who) || events == null) return;
            float newest = -1f;
            foreach (var (kind, days) in events)
            {
                if (string.IsNullOrEmpty(kind) || days < 0f || days > LatelyDays) continue;
                float h = Fresh(days, LatelyDays, 1.5f);
                foreach (var level in HistoryKinds.Levels(kind)) f.Tag(who + ":lately:" + level, h);
                if (newest < 0f || days < newest) newest = days;
            }
            if (newest >= 0f) f.Tag(who + ":lately", Fresh(newest, LatelyDays, 1.5f));
        }

        // ---------------- The ages ----------------

        /// <summary>
        /// Their people's age as the lines are tagged (their:tribal, their:medieval, their:industrial, their:spacer;
        /// their:lowtech before industry, else their:hightech; their:empire), and how it stands to the colony's own:
        /// their:sameage, their:behind (an earlier age), their:ahead (a later one).
        /// </summary>
        public static void AddAges(FactSet f, WorldAge ours, WorldAge theirs, bool empire)
        {
            if (f == null) return;
            f.Tag("their:" + AgesMath.Tag(theirs));
            f.Tag(theirs <= WorldAge.Medieval ? "their:lowtech" : "their:hightech");
            if (empire) f.Tag("their:empire");
            f.Tag(theirs == ours ? "their:sameage" : theirs < ours ? "their:behind" : "their:ahead");
        }
    }
}
