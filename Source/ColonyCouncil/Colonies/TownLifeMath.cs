using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>How a raid on a town the colony was ended: driven off with the colony's fighters, held alone, hurt, or sacked.</summary>
    public enum TownRaidEnd
    {
        None,
        Defended,
        Held,
        Hurt,
        Sacked
    }

    /// <summary>
    /// The rules of a town the colony was in its own life (docs/design/stage3-many-colonies.md, section 2, "The bad side",
    /// "Taking it back" and "Its growth"; release 5, the player's decisions 11B and 15B), with no game types so Tools/Tests
    /// can check them: a raid on it (the two sides' points by the game's own measure, what an unanswered raid costs, a
    /// former colonist's death only in a raid that really came), its hard times (the town asking to come back, refused
    /// twice its people at the colony's gate), its turning against the colony (the colony's wrongs and a cool head in its
    /// natural goodwill), and the region's hardships that can take one of the colony's former people.
    /// </summary>
    public static class TownLifeMath
    {
        public const int DayTicks = 60000, SeasonDays = 15, YearDays = 60;

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        // ---------------- A raid on the town ----------------

        /// <summary>The game's own points for a town's defenders (SymbolResolver_Settlement.DefaultPawnsPoints).</summary>
        public const float DefendMin = 1150f, DefendMax = 1600f;

        /// <summary>
        /// The town's defenders by its fortune: a struggling town has fewer to stand on its walls, a flourishing one more
        /// (0.7 of the game's at nothing, the game's own at 50, 1.3 at 100). No fortune known: the game's own.
        /// </summary>
        public static float DefendPoints(float basePoints, float prosperity)
        {
            if (prosperity < 0f) return basePoints;
            return basePoints * (0.7f + 0.6f * Clamp(prosperity, 0f, 100f) / 100f);
        }

        /// <summary>
        /// The raiders come 0.75 to 1.5 times as strong as the game's own defenders of such a town (before its fortune): they
        /// came because they thought they could win, so a struggling town meets them weaker and a flourishing one stronger.
        /// </summary>
        public const float RaidLow = 0.75f, RaidHigh = 1.5f;

        public static float RaidPoints(float townPoints, float roll) => Math.Max(0f, townPoints) * (RaidLow + (RaidHigh - RaidLow) * Clamp(roll, 0f, 1f));

        /// <summary>The raiders' strength to the town's, by the game's measure of points.</summary>
        public static float Ratio(float raid, float defend) => defend <= 0f ? 9f : Math.Max(0f, raid) / defend;

        /// <summary>Left to itself, the town holds by its share of the two sides' points, never surely either way (0.1 to 0.9).</summary>
        public static float HoldChance(float raid, float defend)
        {
            float sum = Math.Max(0f, raid) + Math.Max(0f, defend);
            return sum <= 0f ? 0.5f : Clamp(Math.Max(0f, defend) / sum, 0.1f, 0.9f);
        }

        /// <summary>Raiders this much the stronger sack a town they beat, rather than only hurting it.</summary>
        public const float SackedRatio = 1.25f;

        /// <summary>A raid nobody of the colony saw: held on the roll, else hurt, or sacked when the raiders were much the stronger.</summary>
        public static TownRaidEnd Alone(float raid, float defend, float roll)
        {
            if (roll < HoldChance(raid, defend)) return TownRaidEnd.Held;
            return Ratio(raid, defend) >= SackedRatio ? TownRaidEnd.Sacked : TownRaidEnd.Hurt;
        }

        /// <summary>
        /// The fight on the town's map, the one the player saw: every raider down, dead or fled, it was defended; nobody of
        /// the town or the colony on their feet with raiders still up, it was sacked; the map closed with both still up (the
        /// colony's people left), it goes by those left standing, the town's against the raiders. Still going: none.
        /// </summary>
        public static TownRaidEnd OnMap(int raidersUp, int townUp, int colonyUp, bool mapGone)
        {
            if (raidersUp <= 0) return TownRaidEnd.Defended;
            if (townUp + colonyUp <= 0) return TownRaidEnd.Sacked;
            if (!mapGone) return TownRaidEnd.None;
            if (townUp <= 0 || raidersUp >= townUp * SackedRatio) return TownRaidEnd.Sacked;
            return raidersUp > townUp ? TownRaidEnd.Hurt : TownRaidEnd.Held;
        }

        /// <summary>Points of fortune it costs the town: none defended, 3 held, 12 hurt, 25 sacked.</summary>
        public static float FortuneLoss(TownRaidEnd e) => e == TownRaidEnd.Held ? 3f : e == TownRaidEnd.Hurt ? 12f : e == TownRaidEnd.Sacked ? 25f : 0f;

        /// <summary>The share of its trade goods taken or burned: none defended, a tenth held, a third hurt, most of it sacked.</summary>
        public static float GoodsLost(TownRaidEnd e) => e == TownRaidEnd.Held ? 0.1f : e == TownRaidEnd.Hurt ? 0.35f : e == TownRaidEnd.Sacked ? 0.7f : 0f;

        /// <summary>How many of its other people (its notables who were never the colony's) die at most: one hurt, two sacked.</summary>
        public static int OthersLost(TownRaidEnd e) => e == TownRaidEnd.Hurt ? 1 : e == TownRaidEnd.Sacked ? 2 : 0;

        /// <summary>
        /// A former colonist's death in a raid nobody of the colony saw (decision 11B: a real hardship of the town, told by
        /// letter): only a town hurt or sacked, likelier the stronger the raiders were; one at most a raid, never a hidden roll
        /// apart from the raid.
        /// </summary>
        public static float FormerDeathChance(TownRaidEnd e, float ratio)
        {
            if (e == TownRaidEnd.Sacked) return Clamp((ratio - 0.8f) * 0.5f, 0.1f, 0.4f);
            if (e == TownRaidEnd.Hurt) return Clamp((ratio - 0.8f) * 0.25f, 0.03f, 0.15f);
            return 0f;
        }

        /// <summary>The town holds out for the colony's fighters this many days after the answer, and on while they are on the road to it.</summary>
        public const int HoldDays = 5;

        /// <summary>The raid comes this long after the colony says it cannot help (or leaves the question): half a day to a day and a half.</summary>
        public const float RaidAfterMin = 0.5f, RaidAfterMax = 1.5f;

        /// <summary>The colony's fighters: two grown colonists or more, on their feet, at the town or on the road to it.</summary>
        public const int MinFighters = 2;

        public static bool Answers(int grown, bool boundOrThere) => boundOrThere && grown >= MinFighters;

        /// <summary>The town keeps it in its goodwill: the colony stood with it, said no, or said it would come and never did.</summary>
        public const int DefendedGoodwill = 12, RefusedGoodwill = -4, NoShowGoodwill = -8;

        /// <summary>How the town's call for help comes, by the town's own means (its age at the hand-over): a runner, a rider, the radio, comms.</summary>
        public static string CallMeans(WorldAge age) =>
            age == WorldAge.Tribal ? "runner" : age == WorldAge.Medieval ? "rider" : age == WorldAge.Industrial ? "radio" : "comms";

        /// <summary>The old ways send someone to the colony's gate; the radio and comms bring the word at once.</summary>
        public static bool MessengerComes(WorldAge age) => age == WorldAge.Tribal || age == WorldAge.Medieval;

        // ---------------- Hard times: the town asks to come back ----------------

        /// <summary>Peacefully, the town asks only with this much goodwill or more.</summary>
        public const int AskGoodwill = 60;

        /// <summary>Never in the first season after the hand-over, and a season between its asks.</summary>
        public const int AskFirstDays = SeasonDays, AskGapDays = SeasonDays;

        /// <summary>Refused this many times, its people give it up and come to the colony's gate.</summary>
        public const int RefusalsToGive = 2;

        /// <summary>Its question left unanswered this many days is taken as a no.</summary>
        public const int AskLapseDays = 10;

        /// <summary>
        /// The town asks to come back under the colony (section 2, "Peacefully, the town asks"): struggling long by the
        /// region's own rule (RiseFallMath.Struggling), goodwill 60 or more and at peace, not agreed already, not refused twice,
        /// past its first season and a season since its last ask.
        /// </summary>
        public static bool AsksBack(bool struggling, int goodwill, bool hostile, bool agreed, int refusals, int handedAt, int lastAsk, int now)
        {
            if (!struggling || hostile || agreed || goodwill < AskGoodwill || refusals >= RefusalsToGive) return false;
            if (handedAt < 0 || now - handedAt < (long)AskFirstDays * DayTicks) return false;
            return lastAsk < 0 || now - lastAsk >= (long)AskGapDays * DayTicks;
        }

        /// <summary>Refused twice: its people give it up and come to the colony's gate.</summary>
        public static bool GivesUp(int refusals) => refusals >= RefusalsToGive;

        /// <summary>They come to the gate this many days after the second no.</summary>
        public const int GateMinDays = 2, GateMaxDays = 5;

        /// <summary>At the gate: its former colonists first, then up to three of its other people; eight at most.</summary>
        public const int GateMax = 8, GateOthers = 3;

        /// <summary>
        /// Who comes to the gate, in order: every former colonist first (as they come), then the others whose wish to join
        /// the colony reaches the line (FormerMath.JoinLine), the best first, three at most; eight in all.
        /// </summary>
        public static List<int> Gate(IReadOnlyList<bool> former, IReadOnlyList<float> scores)
        {
            var list = new List<int>();
            int n = former?.Count ?? 0;
            for (int i = 0; i < n && list.Count < GateMax; i++)
                if (former[i])
                    list.Add(i);
            var others = new List<int>();
            for (int i = 0; i < n; i++)
                if (!former[i] && scores != null && i < scores.Count && scores[i] >= FormerMath.JoinLine)
                    others.Add(i);
            others.Sort((a, b) => scores[b].CompareTo(scores[a]) != 0 ? scores[b].CompareTo(scores[a]) : a.CompareTo(b));
            for (int i = 0; i < others.Count && i < GateOthers && list.Count < GateMax; i++) list.Add(others[i]);
            return list;
        }

        /// <summary>
        /// The region never gives a people's seat up (RiseFallMath.MayGiveUp); only its own people give up a town the colony
        /// was, and with no other town left the people is no more once they have.
        /// </summary>
        public static bool LastTown(int peopleTowns) => peopleTowns <= 1;

        // ---------------- Turning against the colony ----------------

        /// <summary>The colony's wrongs they remember cool them by half a point a point of strength, 40 at most; each grudge on the ledger by 10, 30 at most.</summary>
        public const float WrongWeight = 0.5f, MaxWrongs = 40f;

        public const int GrudgeWeight = 10, MaxGrudges = 30;

        /// <summary>A head who thinks little of the colony (under nothing) cools the town by 0.4 a point of it, 30 at most.</summary>
        public const float HeadWeight = 0.4f, MaxHead = 30f;

        /// <summary>What the colony's wrongs take from the town's natural goodwill (0 or more): the faction memory's and the ledger's grudges.</summary>
        public static int WrongsPart(float wrongs, int grudges) =>
            (int)Math.Round(Math.Min(MaxWrongs, Math.Max(0f, wrongs) * WrongWeight)) + Math.Min(MaxGrudges, Math.Max(0, grudges) * GrudgeWeight);

        /// <summary>What a cool head takes from it (0 or more): only a head whose regard of the colony is under nothing.</summary>
        public static int HeadPart(int headRegard) => headRegard >= 0 ? 0 : (int)Math.Round(Math.Min(MaxHead, -headRegard * HeadWeight));

        /// <summary>The town's natural goodwill from the colony's wrongs and a cool head: nothing or less, which the game's own drift follows.</summary>
        public static int Cooling(float wrongs, int grudges, int headRegard) => -(WrongsPart(wrongs, grudges) + HeadPart(headRegard));

        /// <summary>A new head who thinks this little of the colony, or less, is told: the town cools from now.</summary>
        public const int CoolHead = -10;

        // ---------------- The region's hardships (decision 11B) ----------------

        /// <summary>The kinds of world event that can take a life in the town: a plague (sickness) or a famine (hunger, drought).</summary>
        public const int NoHardship = 0, Famine = 1, Plague = 2;

        public static int HardshipKind(string kind) =>
            kind == "sickness" ? Plague : kind == "hunger" || kind == "drought" ? Famine : NoHardship;

        /// <summary>A hardship reaches the town when it was its own people's, or within this many tiles of it.</summary>
        public const float HardshipTiles = 20f;

        public static bool HardshipReaches(bool theirPeople, float tiles) => theirPeople || tiles >= 0f && tiles <= HardshipTiles;

        /// <summary>
        /// A former colonist's chance to die of it, once when it ends: 6% a plague, 4% a famine; half again in a struggling
        /// town, twice from 65, half where the colony helped that people through it; 25% at most.
        /// </summary>
        public const float PlagueDeath = 0.06f, FamineDeath = 0.04f, HardFactor = 1.5f, OldFactor = 2f, HelpedFactor = 0.5f, MaxHardDeath = 0.25f;

        public const int OldAge = 65;

        public static float HardshipDeathChance(int hardship, bool struggling, int age, bool helped = false)
        {
            if (hardship == NoHardship) return 0f;
            float p = hardship == Plague ? PlagueDeath : FamineDeath;
            if (struggling) p *= HardFactor;
            if (age >= OldAge) p *= OldFactor;
            if (helped) p *= HelpedFactor;
            return Math.Min(MaxHardDeath, p);
        }

        /// <summary>One death a hardship at most: the first of them (in the order given, the eldest first) whose roll falls under their chance; -1 for none.</summary>
        public static int WhoDies(IReadOnlyList<float> chances, IReadOnlyList<float> rolls)
        {
            int n = Math.Min(chances?.Count ?? 0, rolls?.Count ?? 0);
            for (int i = 0; i < n; i++)
                if (rolls[i] < chances[i])
                    return i;
            return -1;
        }
    }
}
