using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>A dealing with a whole people or one of their towns (docs/design/diplomacy-screen.md, step B, points 6 to 13), or an answer to one of the world's letters.</summary>
    public enum DealKind : byte
    {
        /// <summary>A supply contract with a town: goods each season for silver, one way or the other (point 6).</summary>
        Supply,
        /// <summary>Food or medicine for a town in hard times (point 7).</summary>
        Relief,
        /// <summary>A fair day at the colony, friendship declared, the colony's trade terms posted (point 8).</summary>
        Proclaim,
        /// <summary>A crew or a guard detail hired from a town for a season (point 9).</summary>
        Hire,
        /// <summary>A petition to their council (point 10).</summary>
        Petition,
        /// <summary>A joint work: a road, a well, a bridge, a watchtower (point 11).</summary>
        Work,
        /// <summary>Market rights: a stall in their town (point 12).</summary>
        Market,
        /// <summary>A whole town invited to the colony's feast or fair, or the colony's entertainers to their festival (point 13).</summary>
        Invite,
        /// <summary>The colony's answer to a letter the world wrote first (post-and-letters.md, point 13).</summary>
        Reply
    }

    /// <summary>Where a dealing stands. Saved by number: new ones go at the end.</summary>
    public enum DealStage : byte
    {
        /// <summary>The letter is on its way, or read and answered with the answer on its way back.</summary>
        Asked,
        Refused,
        /// <summary>Agreed, its answer not yet at the colony.</summary>
        Agreed,
        /// <summary>The answer is in: it plays out (deliveries due, the share to collect, the work being built, the term running).</summary>
        Running,
        Kept,
        Broken,
        /// <summary>Over with nobody at fault: its term ran out, called off, one side gone, the letter lost.</summary>
        Ended
    }

    public enum SupplyWay : byte
    {
        /// <summary>The colony delivers its goods to them each season, for their silver.</summary>
        Ours,
        /// <summary>They deliver their goods to the colony each season, for its silver.</summary>
        Theirs
    }

    public enum ProclaimKind : byte
    {
        Fair,
        Friendship,
        Terms
    }

    public enum HireKind : byte
    {
        /// <summary>Harvest hands for the colony's fields (the camps' harvest hands).</summary>
        Crew,
        /// <summary>A guard detail camped by the colony that takes up arms against what comes at it (the Ashen Banner's contract).</summary>
        Guard
    }

    public enum PetitionKind : byte
    {
        /// <summary>Their raiders: kept away for a term, as a truce does.</summary>
        Raiders,
        /// <summary>Amends for a wrong of theirs: silver at the gate.</summary>
        Reparations,
        /// <summary>A refugee family camped at the colony settled with them.</summary>
        Settle,
        /// <summary>A family of theirs in hard times taken in: they come and make camp at the gate.</summary>
        TakeIn
    }

    public enum WorkKind : byte
    {
        Road,
        Well,
        Bridge,
        Tower
    }

    public enum InviteKind : byte
    {
        Feast,
        Entertainers
    }

    public enum ReplyKind : byte
    {
        News,
        Request,
        Invitation
    }

    /// <summary>What a people's ways bring to their answer: their calling (Peoples/), their age, the Empire's honour.</summary>
    public struct DealWays
    {
        public string calling;
        public WorldAge age;
        public bool empire;
    }

    /// <summary>What is true now that decides whether a dealing can be asked (read by Dealings.Facts from what Rimfolk tracks).</summary>
    public struct DealFacts
    {
        public bool settingOn, hostile, noOne, unreachable, already, lately;
        public int goodwill;
        public int regard;
        /// <summary>The colony has goods to spare for a contract (Pacts.CartGoods), silver for what it pays.</summary>
        public bool surplus, silver;
        /// <summary>The town in hard times the colony has heard of (struggling, a bad world event on their people).</summary>
        public bool hardTimes;
        /// <summary>The colony has food or medicine to send.</summary>
        public bool relief;
        public bool raidedLately, wrongedLately;
        /// <summary>A refugee family camped at the colony (the camps' refugee family).</summary>
        public bool refugeesHere;
        /// <summary>The colony's fields have something to reap.</summary>
        public bool crops;
        /// <summary>A camp could be pitched at the colony now (a spot, no threat).</summary>
        public bool campRoom;
        /// <summary>A feast with guests, a gathering day or a proclaimed fair is coming.</summary>
        public bool gathering;
        /// <summary>A people at war with the colony (a watchtower has something to watch for).</summary>
        public bool enemies;
        /// <summary>The work of this kind already stands with this town.</summary>
        public bool built;
        public bool feastsOn, missionsOn, campsOn, guestsOk;
    }

    /// <summary>
    /// The rules of dealings with a whole people or one of their towns (docs/design/diplomacy-screen.md, step B, points 6
    /// to 13) and of the letters' step 2, points 12 to 14 (pen friends, the world writing first, the chain of neighbours):
    /// what is open and why not, the terms, the answer by their regard and their ways, and the numbers of what follows. No
    /// game types: Tools/Tests checks it, and the player's text names these numbers.
    /// </summary>
    public static class DealingMath
    {
        public const int DayTicks = 60000;

        // ================= Kinds =================

        public static readonly DealKind[] Kinds = { DealKind.Supply, DealKind.Relief, DealKind.Proclaim, DealKind.Hire, DealKind.Petition, DealKind.Work, DealKind.Market, DealKind.Invite };

        /// <summary>How many sub-kinds a dealing has (its sub is one of them).</summary>
        public static int Subs(DealKind k)
        {
            switch (k)
            {
                case DealKind.Supply: return 2;
                case DealKind.Proclaim: return 3;
                case DealKind.Hire: return 2;
                case DealKind.Petition: return 4;
                case DealKind.Work: return 4;
                case DealKind.Invite: return 2;
                case DealKind.Reply: return 3;
                default: return 1;
            }
        }

        /// <summary>The sub-kind's name, for its keys ("Supply_Ours", "Work_Road"; a dealing with no sub-kinds, its kind's name).</summary>
        public static string SubName(DealKind k, byte sub)
        {
            switch (k)
            {
                case DealKind.Supply: return k + "_" + (SupplyWay)sub;
                case DealKind.Proclaim: return k + "_" + (ProclaimKind)sub;
                case DealKind.Hire: return k + "_" + (HireKind)sub;
                case DealKind.Petition: return k + "_" + (PetitionKind)sub;
                case DealKind.Work: return k + "_" + (WorkKind)sub;
                case DealKind.Invite: return k + "_" + (InviteKind)sub;
                case DealKind.Reply: return k + "_" + (ReplyKind)sub;
                default: return k.ToString();
            }
        }

        /// <summary>Dealings with a town (a supply contract, relief, a crew or guards, a joint work, a stall, a town invited) or with the people as a whole too (proclamations, petitions).</summary>
        public static bool TownOnly(DealKind k) => k != DealKind.Proclaim && k != DealKind.Petition && k != DealKind.Reply;

        /// <summary>The letter's topic for a dealing: "deal:Kind:sub:town" (town -1: the people as a whole).</summary>
        public const string Prefix = "deal:";

        public static string TopicKey(DealKind k, byte sub, int town) => Prefix + k + ":" + sub + ":" + town;

        public static bool ParseTopic(string key, out DealKind k, out byte sub, out int town)
        {
            k = DealKind.Supply;
            sub = 0;
            town = -1;
            if (key == null || !key.StartsWith(Prefix, StringComparison.Ordinal)) return false;
            var parts = key.Substring(Prefix.Length).Split(':');
            if (parts.Length != 3 || !Enum.TryParse(parts[0], out k) || !byte.TryParse(parts[1], out sub) || !int.TryParse(parts[2], out town)) return false;
            return sub < Subs(k);
        }

        // ================= Open, and why not =================

        /// <summary>Days before the same dealing can be asked of the same people again once it was refused.</summary>
        public const float RefusedDays = 15f;

        /// <summary>Goodwill a people must have of the colony to take part in most dealings (fair terms); a friendship declared needs more, a petition none.</summary>
        public const int MinGoodwill = 0, FriendshipGoodwill = 20, MarketGoodwill = 10;

        /// <summary>
        /// Why this dealing can't be asked now (the key's last part, CC_Deal_Why_ and it), or null. The general reasons
        /// first (the setting, war, nobody to receive it, out of reach, one of its kind already under way, refused lately),
        /// then each kind's own.
        /// </summary>
        public static string WhyNot(DealKind k, byte sub, DealFacts f)
        {
            if (!f.settingOn) return "Off";
            bool petition = k == DealKind.Petition;
            // A people at war hears only petitions (about their raiders, amends); everything else waits for peace.
            if (f.hostile && !(petition && (sub == (byte)PetitionKind.Raiders || sub == (byte)PetitionKind.Reparations))) return "War";
            if (f.noOne) return "NoOne";
            if (f.unreachable) return "Unreachable";
            if (f.already) return "Already";
            if (f.lately) return "Lately";
            if (!petition && !f.hostile && f.goodwill < MinGoodwill && k != DealKind.Relief) return "Goodwill";
            switch (k)
            {
                case DealKind.Supply:
                    if (sub == (byte)SupplyWay.Ours) return f.surplus ? null : "NoSurplus";
                    return f.silver ? null : "NoSilver";
                case DealKind.Relief:
                    if (!f.hardTimes) return "NoHardTimes";
                    return f.relief ? null : "NoRelief";
                case DealKind.Proclaim:
                    if (sub == (byte)ProclaimKind.Friendship && f.goodwill < FriendshipGoodwill) return "FriendshipGoodwill";
                    return null;
                case DealKind.Hire:
                    if (!f.campsOn) return "NoCamps";
                    if (!f.campRoom) return "NoCampRoom";
                    if (sub == (byte)HireKind.Crew && !f.crops) return "NoCrops";
                    return f.silver ? null : "NoSilver";
                case DealKind.Petition:
                    switch ((PetitionKind)sub)
                    {
                        case PetitionKind.Raiders: return f.raidedLately ? null : "NotRaided";
                        case PetitionKind.Reparations: return f.wrongedLately ? null : "NotWronged";
                        case PetitionKind.Settle: return f.refugeesHere ? null : "NoRefugees";
                        default:
                            if (!f.campsOn) return "NoCamps";
                            if (!f.hardTimes) return "NoHardTimes";
                            return f.campRoom ? null : "NoCampRoom";
                    }
                case DealKind.Work:
                    if (f.built) return "Built";
                    if (sub == (byte)WorkKind.Tower && !f.enemies) return "NoEnemies";
                    return f.surplus ? null : "NoShare";
                case DealKind.Market:
                    if (f.goodwill < MarketGoodwill) return "MarketGoodwill";
                    return f.silver ? null : "NoSilver";
                case DealKind.Invite:
                    if (sub == (byte)InviteKind.Feast) return !f.gathering ? "NoGathering" : f.guestsOk ? null : "NoGuests";
                    return f.missionsOn ? null : "NoMissions";
                default:
                    return null;
            }
        }

        // ================= Their answer =================

        /// <summary>
        /// The chance their council agrees, by their regard of the colony (their head's, -100 to 100), their people's goodwill
        /// and their ways: each dealing's own base, a little more for each point of regard and goodwill, their calling's
        /// liking for it, the Empire's pride; between one in twenty and nineteen in twenty.
        /// </summary>
        public static float Chance(DealKind k, byte sub, int regard, int goodwill, DealWays w)
        {
            float c = Base(k, sub) + regard * 0.004f + goodwill * 0.003f + Ways(k, sub, w);
            return Math.Max(0.05f, Math.Min(0.95f, c));
        }

        public static bool Agrees(DealKind k, byte sub, int regard, int goodwill, DealWays w, float roll) => roll < Chance(k, sub, regard, goodwill, w);

        private static float Base(DealKind k, byte sub)
        {
            switch (k)
            {
                case DealKind.Supply: return 0.5f;
                case DealKind.Relief: return 0.85f;
                case DealKind.Proclaim:
                    return sub == (byte)ProclaimKind.Friendship ? 0.35f : sub == (byte)ProclaimKind.Fair ? 0.55f : 0.5f;
                case DealKind.Hire: return sub == (byte)HireKind.Guard ? 0.4f : 0.55f;
                case DealKind.Petition:
                    switch ((PetitionKind)sub)
                    {
                        case PetitionKind.Raiders: return 0.3f;
                        case PetitionKind.Reparations: return 0.25f;
                        case PetitionKind.Settle: return 0.4f;
                        default: return 0.85f;
                    }
                case DealKind.Work:
                    return sub == (byte)WorkKind.Well ? 0.6f : sub == (byte)WorkKind.Tower ? 0.45f : 0.5f;
                case DealKind.Market: return 0.4f;
                case DealKind.Invite: return sub == (byte)InviteKind.Feast ? 0.7f : 0.65f;
                default: return 0.6f;
            }
        }

        /// <summary>Their ways: what their calling likes or minds, and the Empire's honour.</summary>
        public static float Ways(DealKind k, byte sub, DealWays w)
        {
            float v = 0f;
            string c = w.calling ?? "";
            switch (k)
            {
                case DealKind.Supply:
                case DealKind.Market:
                    if (c == PeoplesMath.Traders || c == PeoplesMath.Brokers) v += 0.2f;
                    if (w.empire) v -= 0.1f;
                    break;
                case DealKind.Relief:
                    // The Empire takes no charity from those beneath it.
                    if (w.empire) v -= 0.35f;
                    if (c == PeoplesMath.Healers || c == PeoplesMath.Pilgrims) v += 0.1f;
                    break;
                case DealKind.Proclaim:
                    if (sub == (byte)ProclaimKind.Fair && (c == PeoplesMath.Traders || c == PeoplesMath.Artisans)) v += 0.2f;
                    if (sub == (byte)ProclaimKind.Terms && (c == PeoplesMath.Traders || c == PeoplesMath.Brokers)) v += 0.2f;
                    if (sub == (byte)ProclaimKind.Friendship && w.empire) v -= 0.15f;
                    break;
                case DealKind.Hire:
                    if (sub == (byte)HireKind.Guard && c == PeoplesMath.Mercenaries) v += 0.35f;
                    if (sub == (byte)HireKind.Crew && (c == PeoplesMath.Herders || c == PeoplesMath.Grovekeepers)) v += 0.1f;
                    if (w.empire) v -= 0.15f;
                    break;
                case DealKind.Petition:
                    if (sub == (byte)PetitionKind.Settle && (c == PeoplesMath.Healers || c == PeoplesMath.Pilgrims)) v += 0.25f;
                    if (sub == (byte)PetitionKind.Reparations && w.empire) v -= 0.15f;
                    break;
                case DealKind.Work:
                    if (c == PeoplesMath.Artisans || c == PeoplesMath.Traders) v += 0.1f;
                    if (sub == (byte)WorkKind.Tower && c == PeoplesMath.Mercenaries) v += 0.15f;
                    break;
                case DealKind.Invite:
                    if (c == PeoplesMath.Pilgrims || c == PeoplesMath.Artisans) v += 0.1f;
                    break;
            }
            return v;
        }

        // ================= The terms =================

        /// <summary>A season, in days: a contract's deliveries come this far apart, a crew or guards stay this long, the raids kept away this long.</summary>
        public const float SeasonDays = 15f;

        /// <summary>Deliveries in a supply contract: four seasons, a year.</summary>
        public const int Deliveries = 4;

        /// <summary>Days after the answer is in before the first delivery.</summary>
        public const float FirstDeliveryDays = 2f;

        /// <summary>The worth their deliveries carry, by their age: tribal, medieval, industrial, spacer.</summary>
        public static float TheirLoadValue(WorldAge theirs)
        {
            switch (theirs)
            {
                case WorldAge.Tribal: return 220f;
                case WorldAge.Medieval: return 300f;
                case WorldAge.Industrial: return 400f;
                default: return 500f;
            }
        }

        public static int TheirLoadCount(float unitValue, WorldAge theirs) => unitValue <= 0f ? 0 : Math.Max(1, (int)Math.Round(TheirLoadValue(theirs) / unitValue));

        /// <summary>The colony pays a little over the market for their goods brought to its gate: eleven tenths of their worth.</summary>
        public const float TheirPayShare = 1.1f;

        public static int TheirLoadPay(float unitValue, int count) => Math.Max(1, (int)Math.Round(Math.Max(0f, unitValue) * Math.Max(0, count) * TheirPayShare));

        /// <summary>Deliveries missed in a row, or in all, that break a contract.</summary>
        public const int MissedInARow = 2, MissedInAll = 3;

        public static bool Broken(int missedInARow, int missedInAll) => missedInARow >= MissedInARow || missedInAll >= MissedInAll;

        /// <summary>Goodwill for each delivery kept, at the end of a contract kept to its end, and when one is broken.</summary>
        public const int DeliveryGoodwill = 1, KeptGoodwill = 6, BrokenGoodwill = -10;

        /// <summary>What relief carries: medicine for the sick, meals for the hungry, raw food in bulk.</summary>
        public static readonly string[] ReliefGoods = { "medicine", "meals", "rawfood" };

        public static int ReliefCount(string what)
        {
            switch (what)
            {
                case "medicine": return 6;
                case "meals": return 15;
                default: return 75;
            }
        }

        /// <summary>How relief moves the town's fortune (RegionFortunes.Move, half at their other towns) and their goodwill.</summary>
        public const float ReliefFortune = 8f;

        public const int ReliefGoodwill = 6;

        /// <summary>A friendship declared and taken up: goodwill, and their head's regard.</summary>
        public const int FriendshipGoodwill2 = 8, FriendshipRegard = 6;

        /// <summary>The trade terms posted: their traders' better terms for a season, and the days within which their caravan comes.</summary>
        public const float TermsOffset = 0.03f;

        public const float TermsCaravanMin = 2f, TermsCaravanMax = 6f;

        /// <summary>A fair day: at least this many days after the answer (the gathering day on the calendar if one comes within the window), at most this many.</summary>
        public const int FairMinDays = 3, FairMaxDays = 15, FairDefaultDays = 6;

        /// <summary>A town is invited to a gathering at least this many days off (the answer back, their folk on the road).</summary>
        public const int InviteMinDays = 2;

        /// <summary>The fair's day: a gathering day on the calendar within the window, else the default.</summary>
        public static int FairIn(int gatheringIn) => gatheringIn >= FairMinDays && gatheringIn <= FairMaxDays ? gatheringIn : FairDefaultDays;

        /// <summary>The hours of the fair's morning their traders and visitors come in.</summary>
        public const int FairHourFrom = 7, FairHourTo = 12;

        /// <summary>Raids kept away by a petition granted: two seasons.</summary>
        public const float RaidersDays = 30f;

        /// <summary>Amends in silver for a wrong, by its weight in their memory and their age: 12 a point, from 100 to 600.</summary>
        public static int Reparations(int wrongWeight, WorldAge theirs)
        {
            float f = theirs == WorldAge.Tribal ? 0.7f : theirs == WorldAge.Medieval ? 1f : theirs == WorldAge.Industrial ? 1.3f : 1.6f;
            return (int)Math.Round(Math.Max(100f, Math.Min(600f, wrongWeight * 12f * f)));
        }

        /// <summary>A joint work's share from the colony: its goods (wood for a road or a bridge in the early ages, else steel; stone blocks for a well or a tower), by the kind and the distance.</summary>
        public static int WorkShare(WorkKind w, float tiles)
        {
            switch (w)
            {
                case WorkKind.Road: return (int)Math.Round(Math.Max(80f, Math.Min(300f, 60f + tiles * 12f)));
                case WorkKind.Well: return 60;
                case WorkKind.Bridge: return 120;
                default: return 100;
            }
        }

        /// <summary>The share's goods by the age and the work: "wood", "steel", or "blocks" (stone blocks, whatever stone the colony has).</summary>
        public static string ShareGoods(WorkKind w, WorldAge ours)
        {
            bool early = ours == WorldAge.Tribal || ours == WorldAge.Medieval;
            switch (w)
            {
                case WorkKind.Road:
                case WorkKind.Bridge:
                    return early ? "wood" : "steel";
                default:
                    return ours == WorldAge.Tribal ? "wood" : "blocks";
            }
        }

        /// <summary>Days the work takes once the colony's share is with them: a road a day for every two tiles (five at least, fifteen at most), the rest their own.</summary>
        public static float WorkDays(WorkKind w, float tiles)
        {
            switch (w)
            {
                case WorkKind.Road: return Math.Max(5f, Math.Min(15f, tiles / 2f));
                case WorkKind.Well: return 6f;
                case WorkKind.Bridge: return 8f;
                default: return 6f;
            }
        }

        /// <summary>A road's kind by the shared age: a dirt path for tribes, a dirt road in the medieval age, a stone road after (the game's own RoadDefs).</summary>
        public static string RoadDef(WorldAge shared) => shared == WorldAge.Tribal ? "DirtPath" : shared == WorldAge.Medieval ? "DirtRoad" : "StoneRoad";

        /// <summary>The road's kind by the colony's age and theirs: the earlier of the two (both sides build it).</summary>
        public static WorldAge SharedAge(WorldAge ours, WorldAge theirs) => ours < theirs ? ours : theirs;

        /// <summary>
        /// A world tile's days on a road, as the game counts a road's movement (its multiplier: a dirt path 0.75, a dirt road
        /// 0.5, a stone road 0.3 in the base game); the post's days follow the game's path over it (PostReach.WalkDays).
        /// </summary>
        public static float OnRoad(float daysOffRoad, float roadMultiplier) => Math.Max(0f, daysOffRoad) * Math.Max(0.05f, Math.Min(1f, roadMultiplier));

        /// <summary>A well or a bridge: their town's fortune, goodwill, and the memory's weight (enough to be a debt of honor: FavourMath.MinSize).</summary>
        public const float WorkFortune = 5f;

        public const int WorkGoodwill = 5, WorkWeight = 12;

        /// <summary>A watchtower toward their town warns of a raid of a people whose nearest town lies within this many degrees of the way to it, the raid held this many hours.</summary>
        public const float TowerDegrees = 60f;

        public const int WarnHoursMin = 3, WarnHoursMax = 5;

        public static int WarnHours(float roll) => WarnHoursMin + (int)Math.Floor(Math.Max(0f, Math.Min(0.999f, roll)) * (WarnHoursMax - WarnHoursMin + 1));

        /// <summary>The way to one place lies within the tower's arc of the way to another (world headings, degrees).</summary>
        public static bool ThatSide(float towerHeading, float raidersHeading) => PostMath.Angle(towerHeading, raidersHeading) <= TowerDegrees;

        /// <summary>Market rights: the fee by the age, the better terms with their traders, how often their caravans come, and the rights' term.</summary>
        public static int MarketFee(WorldAge theirs)
        {
            switch (theirs)
            {
                case WorldAge.Tribal: return 200;
                case WorldAge.Medieval: return 300;
                case WorldAge.Industrial: return 450;
                default: return 600;
            }
        }

        public const float MarketOffset = 0.05f;

        /// <summary>Their caravans with market rights: a chance a day, never closer than so many days apart.</summary>
        public const float MarketCaravanChance = 0.1f, MarketCaravanGapDays = 6f;

        public const float MarketDays = 60f;

        /// <summary>The better terms a people gives the colony in all from dealings (market rights, terms posted, a pen friend): never more than this.</summary>
        public const float MaxOffset = 0.08f;

        public static float Offset(bool market, bool terms, bool penFriend) =>
            Math.Min(MaxOffset, (market ? MarketOffset : 0f) + (terms ? TermsOffset : 0f) + (penFriend ? PenFriendOffset : 0f));

        // ================= What their people remember =================

        /// <summary>The deed their people remember for a dealing as it ends or is done (World/FactionMemory.cs), or null for none.</summary>
        public static string Deed(DealKind k, byte sub, DealStage how)
        {
            if (how == DealStage.Broken) return k == DealKind.Petition && sub == (byte)PetitionKind.Raiders ? "brokeWord" : "contractBroken";
            if (how != DealStage.Kept) return null;
            switch (k)
            {
                case DealKind.Supply: return "contractKept";
                case DealKind.Relief: return "hardship";
                case DealKind.Proclaim: return sub == (byte)ProclaimKind.Friendship ? "friendship" : null;
                case DealKind.Hire: return "contractKept";
                case DealKind.Petition:
                    switch ((PetitionKind)sub)
                    {
                        case PetitionKind.Reparations: return "amends";
                        case PetitionKind.Settle: return "aid";
                        default: return null;
                    }
                case DealKind.Work: return "builtTogether";
                // The festival's own journey remembers its guests (Missions/Workers_MissionRemembered.cs).
                case DealKind.Invite: return sub == (byte)InviteKind.Feast ? "feast" : null;
                default: return null;
            }
        }

        // ================= Pen friends (post-and-letters.md, point 12) =================

        /// <summary>Letters answered with warmth or courtesy within this many days count as a steady exchange.</summary>
        public const float ExchangeDays = 60f;

        /// <summary>An exchange becomes a friendship at this many letters within the window and this regard; it lapses below one letter in the longer window or below the lower regard.</summary>
        public const int FriendLetters = 3, FriendRegard = 30, KeepRegard = 15;

        public const float KeepDays = 90f;

        /// <summary>Letters remembered for each notable (the oldest go).</summary>
        public const int MaxExchanges = 8;

        /// <summary>Pen friends at most (the coolest lapse first).</summary>
        public const int MaxFriends = 12;

        public static int Recent(IList<int> ticks, int now, float days)
        {
            int n = 0;
            if (ticks == null) return 0;
            foreach (int t in ticks)
                if (now - t >= 0 && now - t <= days * DayTicks)
                    n++;
            return n;
        }

        /// <summary>A steady exchange becomes a friendship.</summary>
        public static bool Becomes(int lettersInWindow, int regard) => lettersInWindow >= FriendLetters && regard >= FriendRegard;

        /// <summary>A friendship holds while letters still pass and their regard stays fair.</summary>
        public static bool Holds(int lettersInLongWindow, int regard) => lettersInLongWindow >= 1 && regard >= KeepRegard;

        /// <summary>A pen friend's better terms with their people's traders.</summary>
        public const float PenFriendOffset = 0.02f;

        /// <summary>A pen friend's warning comes for a raid of a people whose nearest town lies within this many tiles of the friend's town, once in so many days.</summary>
        public const float WarnTiles = 20f, WarnGapDays = 10f;

        /// <summary>A pen friend's favor (a debt of honor of theirs): once a year at most, after a season of friendship.</summary>
        public const float FavourAfterDays = 15f, FavourGapDays = 60f;

        public static bool FavourDue(float friendDays, float sinceLastFavour) => friendDays >= FavourAfterDays && (sinceLastFavour < 0f || sinceLastFavour >= FavourGapDays);

        // ================= The world writes first (point 13) =================

        /// <summary>
        /// The chance a day that one of the notables the colony knows writes to it first: none while two such letters wait
        /// for an answer or within six days of the last; a modest base with someone who thinks well enough of the colony,
        /// a little more for each pen friend, never more than one in seven days.
        /// </summary>
        public static float WriteChance(int warmNotables, int penFriends, int waiting, float daysSinceLast)
        {
            if (warmNotables <= 0 || waiting >= MaxWaiting || daysSinceLast >= 0f && daysSinceLast < MinGapDays) return 0f;
            return Math.Min(MaxChance, BaseChance + PerFriend * Math.Max(0, penFriends));
        }

        public const float BaseChance = 0.06f, PerFriend = 0.02f, MaxChance = 0.14f, MinGapDays = 6f;

        public const int MaxWaiting = 2;

        /// <summary>A letter the world wrote that wants an answer waits this long for one before they take the silence as it is.</summary>
        public const float AnswerDays = 15f;

        /// <summary>Who writes first: someone who thinks this well of the colony or more.</summary>
        public const int WriterRegard = 10;

        /// <summary>What they write: news most often, a small request, an invitation (by a roll).</summary>
        public static ReplyKind WriteKind(float roll, bool canInvite) => roll < 0.5f ? ReplyKind.News : roll < 0.8f || !canInvite ? ReplyKind.Request : ReplyKind.Invitation;

        /// <summary>What a small request asks for, by their age: medicine, wood, cloth or steel, so many.</summary>
        public static string RequestGoods(WorldAge theirs, float roll)
        {
            string[] early = { "medicine", "wood", "Cloth" };
            string[] late = { "medicine", "Steel", "Cloth" };
            var list = theirs == WorldAge.Tribal || theirs == WorldAge.Medieval ? early : late;
            return list[Math.Min(list.Length - 1, (int)Math.Floor(Math.Max(0f, roll) * list.Length))];
        }

        public static int RequestCount(string what) => what == "medicine" ? 3 : what == "wood" ? 60 : what == "Steel" ? 40 : 30;

        /// <summary>Their regard of the colony for an answer given (a request granted, any reply) and for one never written.</summary>
        public const float ReplyRegard = 4f, GrantedRegard = 6f, UnansweredRegard = -3f;

        // ================= A chain of neighbours (point 14) =================

        /// <summary>
        /// Days a letter takes by introduction through a people who knows the far one: there to their town within the
        /// colony's range, a day there to be introduced and a day further on, and on by their means within twice their own
        /// range; -1 when no way. Slower than a relay by the extra day.
        /// </summary>
        public static float ChainDays(float toThem, float onward, float range, float theirRange)
        {
            if (toThem < 0f || onward < 0f || range < 0f || toThem > range) return -1f;
            if (theirRange >= 0f && onward > theirRange * 2f) return -1f;
            return toThem + PostMath.RelayDays * 2f + onward;
        }

        /// <summary>A people introduces the colony's letters to one it knows: at peace with both, on good terms with the colony (goodwill 40), or a pen friend among them (goodwill 0 is enough then).</summary>
        public const int IntroGoodwill = 40;

        public static bool Introduces(bool atPeaceWithFar, int goodwill, bool penFriend) => atPeaceWithFar && (goodwill >= IntroGoodwill || penFriend && goodwill >= 0);

        // ================= The words' seams =================

        /// <summary>The keys of a dealing's words (CC_Deal_ and these, with the sub-kind's name).</summary>
        public static string LabelKey(DealKind k, byte sub) => "CC_Deal_" + SubName(k, sub);

        /// <summary>The key of their council's answer, agreed or refused.</summary>
        public static string AnswerKey(DealKind k, byte sub, bool yes) => "CC_Deal_Ans_" + SubName(k, sub) + (yes ? "_Yes" : "_No");

        public static string WhyKey(string why) => "CC_Deal_Why_" + why;
    }
}
