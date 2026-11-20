using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    // The peoples and their ties (the content update's B4, B12, B14, B17, B22, B29 and B31, the player's go of 20
    // November 2026): the pure rules, linked into the tests (Tools/Tests/PeoplesTiesTests.cs). The code that reads the game
    // is in World/Region/NewHeads.cs, World/Region/FortuneShows.cs, Peoples/PeopleTies.cs, World/KinNews.cs,
    // Camps/HoldingDays.cs, Diplomacy/FeastYear.cs and Camps/CampMemory.cs.

    /// <summary>
    /// A tie between the colony and a people (B14), from the weakest to the strongest as their letters name it: a debt of
    /// honour, an envoy (theirs here or the colony's there), a pact, fealty either way, a hostage, a ward, a marriage.
    /// </summary>
    public enum TieWay
    {
        None,
        Debt,
        Envoy,
        Pact,
        Fealty,
        Hostage,
        Ward,
        Marriage
    }

    /// <summary>B14: a people bound to the colony sends its own events, gifts and camps more often; one the colony broke its word with, less often.</summary>
    public static class PeopleTiesMath
    {
        /// <summary>A bound people's events and camps come about half again as often ("about 1.5 times").</summary>
        public const float Bound = 1.5f;
        /// <summary>A people the colony broke its word with sends them about a third less often.</summary>
        public const float Broken = 0.67f;

        /// <summary>How much likelier a people's own event or camp is: bound, broken, both (they cancel out) or neither.</summary>
        public static float Factor(bool bound, bool broken) => (bound ? Bound : 1f) * (broken ? Broken : 1f);

        /// <summary>The deeds of the colony that are its word broken (FactionMemory's kinds).</summary>
        public static readonly HashSet<string> BrokenDeeds = new HashSet<string> { "brokePact", "brokeTruce", "wordBroken", "renounced", "contractBroken" };

        /// <summary>A broken word still weighs on them while its memory is this strong (MemoryMath.Strength).</summary>
        public const float BrokenFelt = 3f;

        public static bool IsBrokenWord(string deed) => deed != null && BrokenDeeds.Contains(deed);

        /// <summary>The tie their letters name: the strongest there is (None for none).</summary>
        public static TieWay Strongest(IEnumerable<TieWay> ways)
        {
            var best = TieWay.None;
            if (ways == null) return best;
            foreach (var w in ways)
                if (w > best) best = w;
            return best;
        }

        /// <summary>
        /// Who of the colony their gifts go to first, by the tie they carry: the ward's guardian, then the one married in,
        /// then the colonist a debt of honour is about, then the speaker for a pact, fealty or an envoy; a hostage is the
        /// colony's keeping as a whole. Lower comes first; -1 for a tie no one person carries.
        /// </summary>
        public static int CarrierRank(TieWay w)
        {
            switch (w)
            {
                case TieWay.Ward: return 0;
                case TieWay.Marriage: return 1;
                case TieWay.Debt: return 2;
                case TieWay.Pact:
                case TieWay.Fealty:
                case TieWay.Envoy: return 3;
                default: return -1;
            }
        }
    }

    /// <summary>B12: a town's fortune shows on its people at the gate.</summary>
    public static class FortuneShowsMath
    {
        /// <summary>A struggling town's people come in clothes worn to this share of their hit points, by a roll.</summary>
        public const float WornMin = 0.4f, WornMax = 0.65f;

        public static float Worn(float roll) => WornMin + Clamp01(roll) * (WornMax - WornMin);

        /// <summary>A worn garment's hit points: never below one, never above what it had.</summary>
        public static int WornHitPoints(int hitPoints, int max, float roll)
        {
            if (max <= 0) return hitPoints;
            int worn = Math.Max(1, (int)Math.Round(max * Worn(roll)));
            return Math.Min(Math.Max(1, hitPoints), worn);
        }

        /// <summary>The game's quality steps: awful 0 to legendary 6. A flourishing town's people wear one step better, at most masterwork.</summary>
        public const int Masterwork = 5, Legendary = 6;

        public static int StepUp(int quality) => quality < 0 ? quality : quality >= Masterwork ? quality : quality + 1;

        /// <summary>A struggling town's caravan has one pack beast fewer, never its last.</summary>
        public static int PackBeastsKept(int n) => n >= 2 ? n - 1 : Math.Max(0, n);

        /// <summary>One of a flourishing town's people brings a small gift this often.</summary>
        public const float GiftChance = 0.35f;

        /// <summary>The small gift's worth in silver: 20 to 45, by the colony's wealth (LivingEvents.WealthFactor), in fives.</summary>
        public static int GiftSilver(float wealthFactor, float roll)
        {
            float w = float.IsNaN(wealthFactor) ? 1f : Math.Max(0.5f, Math.Min(2f, wealthFactor));
            int s = (int)Math.Round((20f + Clamp01(roll) * 25f) * w / 5f) * 5;
            return Math.Max(10, s);
        }

        /// <summary>One of a struggling town's people asks for a meal once someone of the colony is this near, within so many hours of coming.</summary>
        public const float AskRange = 12f;
        public const int AskHours = 18;

        /// <summary>The fortunes that show at the gate: only the two ends.</summary>
        public static bool Shows(Fortune f) => f == Fortune.Struggling || f == Fortune.Flourishing;

        private static float Clamp01(float x) => float.IsNaN(x) ? 0f : Math.Max(0f, Math.Min(1f, x));
    }

    /// <summary>The new head's temper, as the succession reads it from their traits.</summary>
    public enum HeadTemper
    {
        Steady,
        Kind,
        Greedy,
        Harsh
    }

    /// <summary>B4: the new head comes to the gate.</summary>
    public static class NewHeadMath
    {
        public const int DayTicks = 60000;

        /// <summary>They come this many days after the seat was settled, and are let go when no world event's slot came free within <see cref="LapseDays"/>.</summary>
        public const float MinDays = 3f, MaxDays = 8f, LapseDays = 30f;

        public static int DueTick(int endTick, float roll) => endTick + (int)Math.Round((MinDays + Clamp01(roll) * (MaxDays - MinDays)) * DayTicks);

        public static bool Lapsed(int endTick, int now) => endTick >= 0 && now - endTick > (long)(LapseDays * DayTicks);

        /// <summary>Two to four of their people ride with them.</summary>
        public const int RetinueMin = 2, RetinueMax = 4;

        /// <summary>A steady head opens talk of a trade pact where goodwill allows: 20 or more.</summary>
        public const int PactGoodwill = 20;

        public static bool OpensPact(HeadTemper t, int goodwill, bool pactPossible) => t == HeadTemper.Steady && goodwill >= PactGoodwill && pactPossible;

        public static HeadTemper Of(string flag)
        {
            switch (flag)
            {
                case "kind": return HeadTemper.Kind;
                case "greedy": return HeadTemper.Greedy;
                case "harsh": return HeadTemper.Harsh;
                default: return HeadTemper.Steady;
            }
        }

        public static string Flag(HeadTemper t) => t.ToString().ToLowerInvariant();

        /// <summary>
        /// How much better the head thinks of the colony for its answer: a seat at the table (more by the best talker's
        /// Social, 2 to 7), a gift (more for a greedy head), a word at the gate (less than nothing for a harsh head, who
        /// wanted a token of respect); a kind head a point more for anything.
        /// </summary>
        public static float Regard(HeadTemper t, string answer, int social)
        {
            float r;
            switch (answer)
            {
                case "table": r = 2f + Math.Max(0, Math.Min(20, social)) / 4f; break;
                case "gift": r = t == HeadTemper.Greedy ? 6f : 4f; break;
                case "word": r = t == HeadTemper.Harsh ? -5f : 1f; break;
                default: r = t == HeadTemper.Harsh ? -8f : -2f; break;
            }
            if (t == HeadTemper.Kind && r < 0f) r = 0f;
            else if (t == HeadTemper.Kind) r += 1f;
            return r;
        }

        /// <summary>The head of the best talker at the table thinks this much better of them in person.</summary>
        public static float Personal(int social) => 1f + Math.Max(0, Math.Min(20, social)) / 5f;

        private static float Clamp01(float x) => float.IsNaN(x) ? 0f : Math.Max(0f, Math.Min(1f, x));
    }

    /// <summary>B17: the colony's people with a tie to a struck town hear of it.</summary>
    public static class KinNewsMath
    {
        /// <summary>Word of a hard year reaches the colony with the next messenger; at the latest by the road after this many days.</summary>
        public const float RoadDays = 2.5f;

        public static bool ByRoad(float daysRunning) => daysRunning >= RoadDays;

        /// <summary>One of them asks the council to send help home this often, once an event, while it runs.</summary>
        public const float AskChance = 0.6f;

        /// <summary>What they would send home for a kind of hard year: medicine for a sickness, food for the rest; null for none.</summary>
        public static string SendFor(string kind)
        {
            switch (kind)
            {
                case "sickness": return "medicine";
                case "hunger":
                case "drought":
                case "weather":
                case "water":
                case "fire":
                case "war":
                case "townraid":
                case "roads": return "rawfood";
                default: return null;
            }
        }

        /// <summary>The colony's own at a plague town come home early this often (the "may").</summary>
        public const float ComeHomeChance = 0.5f;

        public static bool Plague(string kind) => kind == "sickness";

        /// <summary>The worry's weight on the heart's card: more for the closest ties.</summary>
        public static float Worry(TieWay w) => w == TieWay.Marriage || w == TieWay.Ward || w == TieWay.Hostage ? 3f : 2f;

        /// <summary>How much they send: by the stores (a fifth of what is to spare, 10 to 60 raw food or 2 to 6 medicine).</summary>
        public static int Amount(string what, int stored)
        {
            if (stored <= 0) return 0;
            if (what == "medicine") return Math.Max(2, Math.Min(6, stored / 5));
            return Math.Max(10, Math.Min(60, stored / 5 / 10 * 10));
        }
    }

    /// <summary>B22: the neighbours for good keep the colony's days.</summary>
    public static class HoldingDaysMath
    {
        /// <summary>Up to three of them come to a gathering, never the children alone.</summary>
        public const int GuestsMost = 3;

        public static int Guests(int grown) => Math.Max(0, Math.Min(GuestsMost, grown));

        /// <summary>A burial this fresh (hours since the death) brings them to the grave; a grave seen once is not come to again.</summary>
        public const float FuneralHours = 48f;

        public static bool Fresh(int deathTick, int now) => deathTick >= 0 && now >= deathTick && now - deathTick <= (long)(FuneralHours * 2500);

        /// <summary>They come only while they're at ease with the colony.</summary>
        public const float TensionMost = 50f;

        public static bool AtEase(float tension) => tension < TensionMost;

        /// <summary>A day of their own each year: the day they settled, from the first year on.</summary>
        public static bool Anniversary(int settledDay, int today, float daysSettled) => settledDay >= 0 && today == settledDay && daysSettled >= 59f;

        /// <summary>Two of theirs who are free marry this often on a day (rare: a small family has few).</summary>
        public const float WeddingChance = 0.03f;

        /// <summary>The colony's nearest friend among them is asked over only once their warmth is this much.</summary>
        public const int FriendWarmth = 2;
    }

    /// <summary>A town's year as a feast's guests bring it (B29).</summary>
    public enum TownYear
    {
        None,
        Hard,
        Good
    }

    /// <summary>B29: a feast's guests bring their town's year.</summary>
    public static class FeastYearMath
    {
        /// <summary>A good year: a fair or a bumper harvest at their town; a hard one: any bad world event there.</summary>
        public static TownYear Year(string kind, bool bad)
        {
            if (bad) return TownYear.Hard;
            return kind == "fair" || kind == "harvest" ? TownYear.Good : TownYear.None;
        }

        /// <summary>A hard year's guests think more of the meal and less of the contests: the meal counts double, each contest half.</summary>
        public static float Welcome(float welcome, bool ate, int contests, TownYear year)
        {
            if (year != TownYear.Hard) return welcome;
            float w = welcome;
            if (ate) w += 4f;
            w -= Math.Min(3, Math.Max(0, contests)) * 1f;
            return w;
        }

        /// <summary>The leftovers a hard year's guests carry home: two meals each party, never past half of what is to spare.</summary>
        public static int Leftovers(int parties, int spare)
        {
            if (parties <= 0 || spare <= 1) return 0;
            return Math.Min(parties * 2, spare / 2);
        }

        /// <summary>A good year's guests bring a gift: 60 to 120 silver by the colony's wealth, in tens.</summary>
        public static int Gift(float wealthFactor, float roll)
        {
            float w = float.IsNaN(wealthFactor) ? 1f : Math.Max(0.5f, Math.Min(2f, wealthFactor));
            float r = float.IsNaN(roll) ? 0f : Math.Max(0f, Math.Min(1f, roll));
            return Math.Max(30, (int)Math.Round((60f + r * 60f) * w / 10f) * 10);
        }
    }

    /// <summary>B31: what a camp that comes back lived through since, from the world events that ended at their people.</summary>
    public static class CampNewsMath
    {
        /// <summary>
        /// The news a world event gives, by its kind and how it ended: "fever", "hunger", "rains" (a drought the rains
        /// ended), "dryyear" (one they didn't), "fire", "flood", "winter", "herds", "harvest", "fair", "head", "war", "raid",
        /// "raidheld", "rebuilt", "faith", "mourning"; null for one a camp wouldn't speak of.
        /// </summary>
        public static string Topic(string defName, string kind, string ending)
        {
            switch (kind)
            {
                case "sickness": return defName == "Council_WS_Murrain" ? "herds" : "fever";
                case "hunger": return ending == "missed" ? null : "hunger";
                case "drought": return ending == "rains" || ending == "someHelp" ? "rains" : "dryyear";
                case "fire": return "fire";
                case "water": return "flood";
                case "weather": return "winter";
                case "harvest": return "harvest";
                case "fair": return "fair";
                case "court": return defName == "Council_WS_Succession" || defName == "Council_WS_Claimants" ? "head" : null;
                case "war": return defName == "Rimfolk_WS_NeighborsWar" || defName == "Council_WS_Warlord" ? "war" : null;
                case "townraid": return ending == "defended" || ending == "held" ? "raidheld" : "raid";
                case "rebuild": return "rebuilt";
                case "faith": return defName == "Council_WS_HolyYear" || defName == "Council_WS_Pilgrimage" ? "faith" : null;
                case "mourning": return "mourning";
                default: return null;
            }
        }

        /// <summary>Of the events that ended at their people since they left (end ticks, newest last), the one to tell: the newest with news.</summary>
        public static int Pick(IList<int> endTicks, IList<bool> hasNews, int leftTick)
        {
            int best = -1;
            if (endTicks == null || hasNews == null) return best;
            for (int i = 0; i < endTicks.Count && i < hasNews.Count; i++)
            {
                if (!hasNews[i] || endTicks[i] <= leftTick) continue;
                if (best < 0 || endTicks[i] >= endTicks[best]) best = i;
            }
            return best;
        }
    }
}
