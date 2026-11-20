using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the gods among the colony's people and the world (the content update's integrations, part 2: C6,
    /// C7, C8, C12, C14, C15, C18, C26, C29 of docs/content-update-proposals.md). No game types here: Tools/Tests checks
    /// them. The parts are in Gods/Among/.
    /// </summary>
    public static class GodsAmongMath
    {
        // ================= C6. A vowed pilgrimage that really walks out =================

        /// <summary>The road each way to a holy place within reach of a vowed pilgrimage (the world event's errands walk eight).</summary>
        public const float PilgrimMaxRoad = 6f;

        /// <summary>A holy place this close on the road is no journey: the walk on the colony's own land does instead.</summary>
        public const float PilgrimMinRoad = 1f;

        /// <summary>Days at the holy place: one to three, by the roll.</summary>
        public static float DaysThere(float roll) => 1f + 2f * Clamp01(roll);

        public static bool InReach(float road) => !float.IsNaN(road) && road >= PilgrimMinRoad && road <= PilgrimMaxRoad;

        /// <summary>What kind of holy place: a town of a people that holds the god, the pilgrim fellowship's shrine, an old shrine by a landmark.</summary>
        public enum HolyKind
        {
            Town,
            Fellowship,
            Old
        }

        /// <summary>How much farther a place of this kind may lie and still be chosen first: a people that holds the god is worth a longer road.</summary>
        public static float RoadWeight(HolyKind k) => k == HolyKind.Town ? 1f : k == HolyKind.Fellowship ? 1.3f : 1.6f;

        /// <summary>The holy place a vower goes to (its index), or -1: the nearest in reach, weighed by its kind (RoadWeight).</summary>
        public static int PickHoly(IList<(HolyKind kind, float road)> places)
        {
            int best = -1;
            float bestScore = float.MaxValue;
            if (places == null) return -1;
            for (int i = 0; i < places.Count; i++)
            {
                var p = places[i];
                if (!InReach(p.road)) continue;
                float score = p.road * RoadWeight(p.kind);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Whole days away, both roads and the days there, for the words ("away about nine days").</summary>
        public static int AwayDays(float road, float there) => (int)Math.Ceiling(2f * Math.Max(0f, float.IsNaN(road) ? 0f : road) + Math.Max(0f, float.IsNaN(there) ? 0f : there));

        /// <summary>The tale told at supper once home: from this hour of the evening on, for this many days at most.</summary>
        public const int TaleFrom = 17, TaleTo = 22, TaleDays = 6;

        // ================= C7. Murmur and the fishers =================

        /// <summary>The morning, when a fisher gives the first fish back: from first light until late morning.</summary>
        public static bool Morning(int hour) => hour >= 4 && hour < 12;

        /// <summary>Of a catch of this many, how many stay with the fisher once one is given back (never under none).</summary>
        public static int AfterGiving(int caught) => Math.Max(0, caught - 1);

        /// <summary>The fisher's day of good fishing afterwards (the mods' own stats, Defs/GodDefs/Rimfolk_GodsAmong.xml): a tenth more, and a little luck.</summary>
        public const float YieldOffset = 0.1f, LuckOffset = 0.02f;

        /// <summary>Someone this close, awake, sees the splash.</summary>
        public const float SplashSeen = 18f;

        /// <summary>The colony's book keeps a fisher's give-back at most once in this many days (it is done every morning).</summary>
        public const float BookGapDays = 10f;

        /// <summary>The fisher says a word as they give it back, at most once in this many days.</summary>
        public const float WordGapDays = 3f;

        /// <summary>What the water gives back may also leave this many fish on the mud (the mods' fish, else the game's).</summary>
        public const int MudFishMin = 2, MudFishMax = 4;

        /// <summary>The chance it does.</summary>
        public const float MudFishChance = 0.5f;

        // ================= C8. Ducat and what the colony owes =================

        /// <summary>What a debt's move means for the coin, the trader and the bargain.</summary>
        public enum DebtTurn
        {
            None,
            Paid,
            Lapsed
        }

        /// <summary>
        /// A debt the colony owed (a silver debt to a people, a good turn owed to them) was kept, paid when due: Paid; broken, not
        /// met in time or refused: Lapsed. A debt owed to the colony, or one that ended with nobody at fault, is nothing here.
        /// </summary>
        public static DebtTurn Turn(bool colonyOwes, bool kept, bool broken)
        {
            if (!colonyOwes) return DebtTurn.None;
            if (kept) return DebtTurn.Paid;
            return broken ? DebtTurn.Lapsed : DebtTurn.None;
        }

        /// <summary>The coin lies on the doorstep from this hour of the next morning to that one.</summary>
        public const int CoinFrom = 5, CoinTo = 10;

        /// <summary>The next trader at the gate stays this much longer (half a day).</summary>
        public const int LongerTicks = 30000;

        /// <summary>The next trader drives a hard bargain: this much off the colony's prices, for the rest of the day.</summary>
        public const float HardBargain = -0.05f;

        public const int HardTicks = 60000;

        /// <summary>A paid debt's turn, or a lapsed one's, waits this long for the next trader before it is let go.</summary>
        public const int WaitDays = 30;

        /// <summary>How often a debt paid or let lapse is followed so (the gold has moods of its own).</summary>
        public const float PaidChance = 0.75f, LapsedChance = 0.75f;

        /// <summary>The trader's stay can be lengthened only while their lord has this many ticks left (it is not lengthened on the way out).</summary>
        public static int Lengthened(int ticksPassed, int extra) => ticksPassed - Math.Max(0, extra);

        // ================= C12. The keeper at work =================

        /// <summary>The keeper walks out to a sign they didn't see only this far (cells) from where they are, and within this many hours.</summary>
        public const float LookReach = 90f;

        public const int LookHours = 8;

        /// <summary>They go to look: they didn't see it, it was at a place on the land, and it is within reach.</summary>
        public static bool GoesToLook(bool keeperSaw, bool place, float distance) => !keeperSaw && place && !float.IsNaN(distance) && distance <= LookReach;

        /// <summary>The evening's writing: from this hour, to that one.</summary>
        public const int WriteFrom = 18, WriteTo = 22;

        /// <summary>They write when this many entries are new since they last did, and not again for this many days.</summary>
        public const int WriteNew = 1;

        public const float WriteGapDays = 2f;

        /// <summary>How long they sit at it (ticks), by how many entries are new: a little longer for each, an hour and a half at most.</summary>
        public static int WriteTicks(int newEntries) => Math.Min(3750, 1250 + 400 * Math.Max(0, newEntries));

        /// <summary>The evening's writing goes when there are new entries and the hour is right.</summary>
        public static bool Writes(int hour, int newEntries) => hour >= WriteFrom && hour < WriteTo && newEntries >= WriteNew;

        // ================= C14. The raiders' gods, heard at the wall =================

        /// <summary>A raider cries their god's name when one of the colony is this close and can see them.</summary>
        public const float CryRange = 38f;

        /// <summary>The raid's cry waits this long for them to come on, then is let go.</summary>
        public const int CryHours = 18;

        /// <summary>How often a raid of a people that holds a god has its cry (not every raider band is pious).</summary>
        public const float CryChance = 0.7f;

        /// <summary>A prisoner talking with their warden tells a piece of their gods' tales this often, and once in so many days at most.</summary>
        public const float PrisonerTaleChance = 0.2f;

        public const float PrisonerGapDays = 2f;

        // ================= C15. The dead's own god at the grave =================

        /// <summary>A mourner who holds the god the dead held finds the grief lighter: from leaning toward it.</summary>
        public const float MournerHoldsFrom = PawnGodsMath.LeansFrom;

        /// <summary>The dead held a god this much at least for the speaker to give them to it.</summary>
        public const float DeadHeldFrom = PawnGodsMath.LeansFrom;

        // ================= C18. Named seasons taken for a god's doing =================

        /// <summary>
        /// The gods a believer may take a named season for (WeatherMath.NameOf: cold, heat, storms, wet, dry), by the season's
        /// word: Hush for a long cold, Helios for a hot summer (Smoulder too), Helios for a warm winter, Cirrus for a stormy
        /// one, Murmur for a wet one, and for a dry one Helios's sun or Murmur fallen silent.
        /// </summary>
        public static string[] GodsFor(string name, string seasonWord)
        {
            switch (name)
            {
                case "cold": return new[] { "Hush" };
                case "heat": return seasonWord == "winter" ? new[] { "Helios" } : new[] { "Helios", "Smoulder" };
                case "storms": return new[] { "Cirrus" };
                case "wet": return new[] { "Murmur" };
                case "dry": return new[] { "Helios", "Murmur" };
                default: return new string[0];
            }
        }

        /// <summary>The named seasons the colony keeps a god's doing for, at most.</summary>
        public const int MaxSeasons = 12;

        /// <summary>
        /// The same season come round again: the year after, in the same season, its first days (a council question may ask
        /// for a day against it then).
        /// </summary>
        public static bool ComesRound(int year, int season, int nowYear, int nowSeason, int dayInSeason) =>
            nowYear == year + 1 && nowSeason == season && dayInSeason >= 0 && dayInSeason < AskWithinDays;

        public const int AskWithinDays = 6;

        /// <summary>Two believers who take the season for different gods say so to each other (when they are together).</summary>
        public static bool Argue(string a, string b) => !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && a != b;

        // ================= C26. A shrine room for each god =================

        /// <summary>Each god's shrine room (Defs/RoomTemplateDefs/Rimfolk_RoomTemplates_GodRooms.xml), by the god's key.</summary>
        public static readonly (string god, string template)[] Rooms =
        {
            ("Hollin", "Rimfolk_GodRoom_Hollin"), ("Smoulder", "Rimfolk_GodRoom_Smoulder"), ("Bellwether", "Rimfolk_GodRoom_Bellwether"),
            ("Morel", "Rimfolk_GodRoom_Morel"), ("Cirrus", "Rimfolk_GodRoom_Cirrus"), ("Hush", "Rimfolk_GodRoom_Hush"), ("Helios", "Rimfolk_GodRoom_Helios"),
            ("Selene", "Rimfolk_GodRoom_Selene"), ("Mog", "Rimfolk_GodRoom_Tamiu"), ("Murmur", "Rimfolk_GodRoom_Murmur"), ("Pip", "Rimfolk_GodRoom_Pip")
        };

        /// <summary>A shrine room shows in the planner once the colony has a page (a chapter) for its god in the book.</summary>
        public static bool RoomShown(bool hasGod, bool hasPage) => hasGod && hasPage;

        // ================= C29. Midwinter with Vanilla Christmas Expanded =================

        /// <summary>The gods whose festival is the colony's midwinter day (Hush's Stillness, Selene's Longest Night).</summary>
        public static readonly string[] MidwinterGods = { "Hush", "Selene" };

        /// <summary>The calendar's midwinter custom, kept as a day of the colony's.</summary>
        public const string MidwinterCustom = "custom|Council_Dilemma_Midwinter";

        /// <summary>A day kept is the colony's midwinter: the winter god's festival, or the midwinter custom.</summary>
        public static bool IsMidwinter(string dayId, string festivalGod) =>
            dayId == MidwinterCustom || festivalGod != null && Array.IndexOf(MidwinterGods, festivalGod) >= 0;

        /// <summary>The evening's order: the tree lit at dusk, the table laid, the presents handed round after supper.</summary>
        public const int TreeFrom = 15, TableFrom = 16, PresentsFrom = 19, EveningTo = 23;

        /// <summary>The stage the evening is at by the hour (0 waiting, 1 the tree, 2 the table, 3 the presents, 4 done).</summary>
        public static int StageAt(int hour)
        {
            if (hour >= EveningTo || hour < 6) return 4;
            if (hour >= PresentsFrom) return 3;
            if (hour >= TableFrom) return 2;
            if (hour >= TreeFrom) return 1;
            return 0;
        }

        /// <summary>Presents to hand round: one each for those at home, as many as the stores hold.</summary>
        public static int Presents(int people, int inStore) => Math.Max(0, Math.Min(people, inStore));

        /// <summary>Of the midwinter food in store, this much at most is carried to the festive table.</summary>
        public const int TableFoodMost = 12;

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
