using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What a merge does to one tradition of the colony whose people came (ManyColoniesMath.Tradition).</summary>
    public enum TraditionMerge
    {
        /// <summary>Not a tradition of theirs: nothing joins.</summary>
        None,
        /// <summary>Only they kept it: it joins the colony's at half its strength.</summary>
        Join,
        /// <summary>Both kept it, theirs the stronger: the colony's keeps theirs.</summary>
        Theirs,
        /// <summary>Both kept it, the colony's own the stronger (or as strong): it stays as it was.</summary>
        Ours
    }

    /// <summary>
    /// The rules of many colonies in play (docs/design/stage3-many-colonies.md, section 3, release 4; the player's decisions
    /// 12A, 13A and 14A), with no game types so Tools/Tests can check them: two councils become one when a colony's people
    /// come to live at another home (when, where, its traditions, the early election, the names, the remembrance day, the
    /// welcome); each colony its own character (a colony founded from another starts with half its mother's, "walled" only
    /// with walls); the region's faces and events near every home; and the pages that follow the colony in view. With one
    /// colony none of it changes anything: every rule answers as before.
    /// </summary>
    public static class ManyColoniesMath
    {
        public const int DayTicks = 60000;
        public const int HourTicks = 2500;
        public const int SeasonTicks = 15 * DayTicks;
        public const int None = HomesMath.None;

        // ================= Two councils become one (3b, decision 12A) =================

        /// <summary>
        /// The home the people of a lost colony came to live at, or none yet: the home where most of them have lived a day
        /// (their last home, <paramref name="lastHomes"/>, one per person of theirs still the colony's), among the colony's
        /// homes now, never the lost one; equal, the home founded first (the lower key). None while nobody has lived a day
        /// at another home.
        /// </summary>
        public static int MergedInto(int from, IList<int> lastHomes, ICollection<int> homes)
        {
            if (lastHomes == null || homes == null) return None;
            var counts = new Dictionary<int, int>();
            foreach (int h in lastHomes)
            {
                if (h == None || h == from || !homes.Contains(h)) continue;
                counts[h] = (counts.TryGetValue(h, out int n) ? n : 0) + 1;
            }
            int best = None, most = 0;
            foreach (var kv in counts)
                if (kv.Value > most || kv.Value == most && kv.Key < best)
                {
                    best = kv.Key;
                    most = kv.Value;
                }
            return best;
        }

        /// <summary>
        /// Two councils become one only where the home had people of its own before them (grown-ups who are not of the
        /// colony that came), and the other colony's council is still kept (its home lost under thirty days ago); a home of
        /// theirs alone is their colony moved, and its council carried, by 7a's rule.
        /// </summary>
        public static bool IsMerge(int hostGrownUps, bool fromCouncilKept) => hostGrownUps > 0 && fromCouncilKept;

        /// <summary>When the newcomers are a third or more of the grown-ups of the colony as it is now, the next election comes within the season.</summary>
        public static bool EarlyElection(int newcomerGrownUps, int allGrownUps) => allGrownUps > 0 && newcomerGrownUps > 0 && newcomerGrownUps * 3 >= allGrownUps;

        /// <summary>The early election is called this many days after the merge (the welcome first), well within the season.</summary>
        public const int ElectionAfterDays = 3;

        /// <summary>When the early election is called; none when there is none.</summary>
        public static int ElectionAt(int mergedAt, bool early) => early && mergedAt >= 0 ? mergedAt + ElectionAfterDays * DayTicks : None;

        /// <summary>The early election is due now (it waits for an election already under way, and is let go once one is held after the merge).</summary>
        public static bool ElectionDue(int now, int at, int lastElectionTick) => at >= 0 && now >= at && lastElectionTick < at - ElectionAfterDays * DayTicks;

        /// <summary>
        /// What a merge does to one tradition of the colony that came (<paramref name="theirs"/> is one of theirs), by what
        /// the colony's own calendar has of it: only they kept it, it joins; both kept it, the stronger stays.
        /// </summary>
        public static TraditionMerge Tradition(bool theirs, bool oursTradition, float theirStanding, float ourStanding)
        {
            if (!theirs) return TraditionMerge.None;
            if (!oursTradition) return TraditionMerge.Join;
            return theirStanding > ourStanding ? TraditionMerge.Theirs : TraditionMerge.Ours;
        }

        /// <summary>
        /// A tradition only the newcomers kept joins at half its strength: half its years kept (rounded down) and half its
        /// weight. It stays a tradition, and lapses by the usual rule (two years let pass) unless the colony keeps it.
        /// </summary>
        public static void Halve(ref int keptYears, ref float weight)
        {
            keptYears = Math.Max(0, keptYears) / 2;
            weight = Math.Max(0f, weight) * 0.5f;
        }

        /// <summary>Where both colonies have a holder of a name, the better keeps it: the newcomer only when clearly ahead on its count (the host's holder keeps it on a tie).</summary>
        public static bool NewcomerKeeps(float newcomerCount, float hostCount) => newcomerCount > hostCount;

        /// <summary>A merge makes no rival in its first season: a name taken over in the colony then makes no rival.</summary>
        public static bool QuietSeason(int now, int mergedAt) => mergedAt >= 0 && now >= mergedAt && now - mergedAt < SeasonTicks;

        /// <summary>The old colony's founding day stays a remembrance of the new home while some of its people live there.</summary>
        public static bool KeepRemembrance(int ofItsPeopleThere) => ofItsPeopleThere > 0;

        /// <summary>The welcome: a meal together on the first evening after the merge, between these hours.</summary>
        public const int WelcomeFrom = 17, WelcomeTo = 21;

        /// <summary>The welcome is set going at the first look in the evening's hours.</summary>
        public static bool WelcomeNow(int hour) => hour >= WelcomeFrom && hour <= WelcomeTo;

        /// <summary>A merge waits this long for its welcome (a fight, a night with nobody about), then is done without one.</summary>
        public const int WelcomeWaitDays = 3;

        /// <summary>Merges noted and not yet done, at most (a home lost each: the colony's twenty-four colonies on the ledger at most).</summary>
        public const int MaxMerges = 24;

        /// <summary>A merge noted lets go once its colony's council is no longer kept (thirty days, ColonyStateMath.KeepLostDays) with nobody of theirs settled.</summary>
        public static bool Lapsed(int now, int lostAt) => lostAt >= 0 && now - lostAt >= ColonyStateMath.KeepLostDays * DayTicks;

        // ================= Each colony its own character (3d, decision 13A) =================

        /// <summary>A colony founded from another starts with this share of its mother's ways and of what its work made it.</summary>
        public const float FoundedShare = 0.5f;

        /// <summary>A founded colony's start of one way (or one of the rates of what the work made it): half its mother's.</summary>
        public static float FoundedValue(float mother) => mother * FoundedShare;

        /// <summary>
        /// A colony founded from another starts with half its mother's; newcomers alone (nobody of the colony went) start
        /// fresh, and so does a colony whose mother isn't known.
        /// </summary>
        public static bool StartsFromMother(int partyFromMother, bool motherKnown) => partyFromMother > 0 && motherKnown;

        /// <summary>"Walled" can't be said of a colony with no walls: it needs the calendar's walls-up line of wall sections.</summary>
        public static bool MayBeWalled(int wallSections) => wallSections >= CalendarMath.WallsUp;

        /// <summary>The trait that needs walls (Defs/Misc/Rimfolk_ColonyTraits_Grown.xml).</summary>
        public const string WalledTrait = "Council_Trait_Walled";

        /// <summary>
        /// Word gets around about a colony's ways (the goodwill a generous or open colony earns, CharacterTracker): the
        /// colony as a whole has one name among the peoples, so only the seat's character carries word, never one per home.
        /// </summary>
        public static bool CarriesWord(bool seat) => seat;

        /// <summary>What the colony is known for beyond its walls (renown) is the colony's as a whole: only the seat's name tells a change of it.</summary>
        public static bool TellsRenown(bool seat) => seat;

        // ================= The region near every home (3d, decision 14A) =================

        /// <summary>Each further home gets its nearest towns staffed with notables, beside the seat's.</summary>
        public const int FurtherTowns = 2;

        /// <summary>
        /// A further home's towns (indices into the lists, nearest first): its nearest <see cref="FurtherTowns"/> within twice
        /// the region's reach, none the seat's already has (<paramref name="taken"/>).
        /// </summary>
        public static List<int> NearestTowns(IList<float> tiles, ICollection<int> taken, float reach)
        {
            var result = new List<int>();
            int n = tiles?.Count ?? 0;
            var order = new List<int>();
            for (int i = 0; i < n; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int c = tiles[a].CompareTo(tiles[b]);
                return c != 0 ? c : a.CompareTo(b);
            });
            foreach (int i in order)
            {
                if (result.Count >= FurtherTowns) break;
                if (tiles[i] > reach * 2f || taken != null && taken.Contains(i)) continue;
                result.Add(i);
            }
            return result;
        }

        /// <summary>The notables' cap: the seat's as before, and eight more people a further home on the ground (two towns of four).</summary>
        public static int NotableCap(int seatCap, int furtherHomes, int rolesPerTown) => seatCap + Math.Max(0, furtherHomes) * FurtherTowns * rolesPerTown;

        /// <summary>
        /// The home a world event is placed near: picked by its people (each home's weight its people; a home with none
        /// isn't picked), from a roll in [0, 1). One home: that home, without a roll. None when no home has anyone.
        /// </summary>
        public static int PickHome(IList<int> keys, IList<int> people, float roll)
        {
            if (keys == null || keys.Count == 0) return None;
            if (keys.Count == 1) return keys[0];
            int total = 0;
            for (int i = 0; i < keys.Count && i < people.Count; i++) total += Math.Max(0, people[i]);
            if (total <= 0) return keys[0];
            float at = Math.Max(0f, Math.Min(0.999999f, roll)) * total;
            int run = 0;
            for (int i = 0; i < keys.Count && i < people.Count; i++)
            {
                int w = Math.Max(0, people[i]);
                if (w == 0) continue;
                run += w;
                if (at < run) return keys[i];
            }
            return keys[keys.Count - 1];
        }

        /// <summary>The one roll a world event's home costs: only with more than one home, so one colony's rolls stay as they were.</summary>
        public static bool RollsForHome(int homes) => homes > 1;

        /// <summary>An acquaintance lives in their people's town nearest their own colony: the colony of whoever met them, else the seat.</summary>
        public static int AcquaintanceColony(int metByColony, int seat) => metByColony != None ? metByColony : seat;

        // ================= What the player sees (3c) =================

        /// <summary>The council window's pages that follow the colony in view with more than one home, everyone a click away.</summary>
        public static readonly string[] FollowingPages = { "Table", "Moods", "Bonds", "Faiths", "Renown", "Chronicle" };

        /// <summary>The region window's views that show the colony in view's neighbours.</summary>
        public static readonly string[] FollowingViews = { "Peoples", "HereAndAway" };

        /// <summary>A page follows the colony in view.</summary>
        public static bool Follows(string page)
        {
            if (page == null) return false;
            foreach (var p in FollowingPages)
                if (p == page)
                    return true;
            foreach (var p in FollowingViews)
                if (p == page)
                    return true;
            return false;
        }

        /// <summary>
        /// Someone shows on a page that follows the colony in view: always with one colony, or with everyone chosen;
        /// else only the colony in view's own (<paramref name="theirs"/>, the colony they belong to).
        /// </summary>
        public static bool Shows(int theirs, int inView, bool everyone, int homes) => homes <= 1 || everyone || theirs == inView;

        /// <summary>
        /// A line of the chronicle shows for the colony in view: with one colony, or everyone, every line; else a line placed
        /// at that colony, or about one of its people, or of no colony at all (the world's, a people's, a town's).
        /// </summary>
        public static bool ChronicleShows(int placeColony, int aboutColony, int inView, bool everyone, int homes)
        {
            if (homes <= 1 || everyone) return true;
            if (aboutColony != None) return aboutColony == inView;
            return placeColony == None || placeColony == inView;
        }

        /// <summary>The frost alert: the map in view first, then each home in the order founded (one alert, the first open).</summary>
        public static int FrostAt(int inView, IList<int> homes, IList<bool> open)
        {
            if (homes == null || open == null) return None;
            for (int i = 0; i < homes.Count && i < open.Count; i++)
                if (homes[i] == inView && open[i]) return inView;
            for (int i = 0; i < homes.Count && i < open.Count; i++)
                if (open[i]) return homes[i];
            return None;
        }

        /// <summary>
        /// Where something about an outsider is reckoned from (their town's distance, their kin among the colony): the home
        /// they are at, else the colony of the colonist they talk to, else the seat.
        /// </summary>
        public static int OutsiderHome(int atHome, int colonistColony, int seat) => atHome != None ? atHome : colonistColony != None ? colonistColony : seat;

        /// <summary>The colony an answer is about when it names no map: its colonist's, else the seat.</summary>
        public static int AnswerHome(int mapHome, int colonistColony, int seat) => mapHome != None ? mapHome : colonistColony != None ? colonistColony : seat;
    }
}
