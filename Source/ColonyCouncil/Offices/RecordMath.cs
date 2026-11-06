using System;
using System.Collections.Generic;
using System.Globalization;

namespace ColonyCouncil
{
    /// <summary>
    /// How the next election tells whether a promise was kept: what the map shows of the thing promised, read when the
    /// promise was made and again at the next election (Offices/SeatRecords.cs).
    /// </summary>
    public enum PromiseTest
    {
        /// <summary>Not judged: words only.</summary>
        None,
        /// <summary>Trust in the council on average (higher is better).</summary>
        Trust,
        /// <summary>Known rivalries between the colony's people (fewer is better).</summary>
        Quarrels,
        /// <summary>The colony's people lost since (dead): none is a promise kept.</summary>
        Lost,
        /// <summary>Days of food in store.</summary>
        Food,
        /// <summary>Medicine for each of the colony's people.</summary>
        Medicine,
        /// <summary>Beds kept for the sick and hurt.</summary>
        Sickbeds,
        /// <summary>The colony's mood on average (0 to 1).</summary>
        Mood,
        /// <summary>The colony's animals.</summary>
        Herd,
        /// <summary>Days and customs kept by the colony (the keeper's count, all of them added up).</summary>
        Days,
        /// <summary>The share of the colony's people of its own faith.</summary>
        Faith,
        /// <summary>The holder's own work in the seat (the seat's measure): some of it is a promise kept.</summary>
        Work
    }

    /// <summary>What the next election found of a promise.</summary>
    public enum PromiseVerdict
    {
        None,
        Kept,
        Broken
    }

    /// <summary>
    /// Elections on a record (decision 9 of the review, 6 November 2026): a candidate runs on their own record, what they
    /// did in the seat or for the colony; the winner's promise is kept in the history with what the map showed of it; the
    /// next election judges it, and a kept promise helps the holder while a broken one costs, said at the hustings and in
    /// the results. The rules, free of game types: Tools/Tests/ElectionTests.cs checks them.
    /// </summary>
    public static class RecordMath
    {
        /// <summary>The history's kinds for a seat's promise and how it went (never in the chronicle: the election's one line tells the seats).</summary>
        public const string Family = "office";
        public const string KindPromise = "office:promise";
        public const string KindKept = "office:kept";
        public const string KindBroken = "office:broken";

        /// <summary>What a vote for a holder gains from a promise kept, or loses for one broken (the luck of the day is 2.5).</summary>
        public const float KeptLean = 1.5f, BrokenLean = 1.75f;

        /// <summary>A holder who did the seat's work leans voters a little their way; the seat's work counted from this much.</summary>
        public const float WorkLean = 0.75f;

        public static float Lean(PromiseVerdict v) => v == PromiseVerdict.Kept ? KeptLean : v == PromiseVerdict.Broken ? -BrokenLean : 0f;

        /// <summary>A promise judged only after this many days in the seat: a by-election's winner a week before isn't held to it.</summary>
        public const float MinDays = 10f;

        // ---------------- The promise's values in the history ----------------

        /// <summary>The positions of a promise's values: the office, the seat, the plank, how it is judged, the reading then, the seat's work count then.</summary>
        public const int OfficeAt = 0, SeatAt = 1, PlankAt = 2, TestAt = 3, ReadingAt = 4, WorkAt = 5, PromiseAt = 6;

        public static List<string> Values(string office, int seat, string plank, PromiseTest test, float reading, float work, string promise) =>
            new List<string>
            {
                office ?? "", seat.ToString(CultureInfo.InvariantCulture), plank ?? "", test.ToString(), reading.ToString("R", CultureInfo.InvariantCulture),
                work.ToString("R", CultureInfo.InvariantCulture), promise ?? ""
            };

        public static string Get(IReadOnlyList<string> values, int at) => values != null && at >= 0 && at < values.Count ? values[at] : null;

        public static float Number(IReadOnlyList<string> values, int at) =>
            float.TryParse(Get(values, at), NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f;

        public static PromiseTest TestOf(IReadOnlyList<string> values) =>
            Enum.TryParse(Get(values, TestAt) ?? "", out PromiseTest t) ? t : PromiseTest.None;

        public static int SeatOf(IReadOnlyList<string> values) => (int)Number(values, SeatAt);

        // ---------------- Judging ----------------

        /// <summary>
        /// Kept or broken, from what the map showed then (<paramref name="before"/>) and now (<paramref name="after"/>).
        /// <paramref name="days"/> in the seat: under <see cref="MinDays"/>, nothing yet. Each test asks what its promise meant: things put right or held where they were good, and never a promise
        /// broken by the luck of a number that barely moved.
        /// </summary>
        public static PromiseVerdict Judge(PromiseTest test, float before, float after, float days)
        {
            if (test == PromiseTest.None || days < MinDays) return PromiseVerdict.None;
            switch (test)
            {
                case PromiseTest.Trust:
                    // Trust held (or rose), or stands well: kept; fell by three or more: broken.
                    if (after >= before - 1f || after >= 15f) return PromiseVerdict.Kept;
                    return after <= before - 3f ? PromiseVerdict.Broken : PromiseVerdict.None;
                case PromiseTest.Quarrels:
                    return after <= before ? PromiseVerdict.Kept : PromiseVerdict.Broken;
                case PromiseTest.Lost:
                    // The reading is how many were lost since: none is kept.
                    return after - before <= 0f ? PromiseVerdict.Kept : PromiseVerdict.Broken;
                case PromiseTest.Food:
                    if (after >= 5f || after >= before + 2f) return PromiseVerdict.Kept;
                    return after < 3f ? PromiseVerdict.Broken : PromiseVerdict.None;
                case PromiseTest.Medicine:
                    // Medicine for each of them: one each, or half as much again as then, is kept; less than then, broken.
                    if (after >= 1f || after >= before + 0.5f) return PromiseVerdict.Kept;
                    return after < before ? PromiseVerdict.Broken : PromiseVerdict.None;
                case PromiseTest.Sickbeds:
                    if (after >= 1f && after >= before) return PromiseVerdict.Kept;
                    return after < 1f ? PromiseVerdict.Broken : PromiseVerdict.None;
                case PromiseTest.Mood:
                    if (after >= 0.45f || after >= before + 0.05f) return PromiseVerdict.Kept;
                    return after < before - 0.05f || after < 0.3f ? PromiseVerdict.Broken : PromiseVerdict.None;
                case PromiseTest.Herd:
                    if (after >= before) return PromiseVerdict.Kept;
                    return after < before - 1f ? PromiseVerdict.Broken : PromiseVerdict.None;
                case PromiseTest.Days:
                    return after > before ? PromiseVerdict.Kept : PromiseVerdict.Broken;
                case PromiseTest.Faith:
                    if (after >= before - 0.01f) return PromiseVerdict.Kept;
                    return after < before - 0.1f ? PromiseVerdict.Broken : PromiseVerdict.None;
                case PromiseTest.Work:
                    // The reading is the seat's work count: some of it in the term is kept. None at all says nothing: no
                    // trader may have come, nobody may have been hurt.
                    return after > before ? PromiseVerdict.Kept : PromiseVerdict.None;
                default:
                    return PromiseVerdict.None;
            }
        }

        /// <summary>The words' key for what the map showed: "CC_Promise_Kept_Lost", "CC_Promise_Broken_Food".</summary>
        public static string EvidenceKey(PromiseTest test, PromiseVerdict v) =>
            v == PromiseVerdict.None || test == PromiseTest.None ? null : "CC_Promise_" + v + "_" + test;

        /// <summary>How many the evidence counts (the lost since, the work done), for "one" or "many" words.</summary>
        public static int EvidenceCount(PromiseTest test, float before, float after) =>
            test == PromiseTest.Lost || test == PromiseTest.Work ? Math.Max(0, (int)Math.Round(after - before)) : 0;

        // ---------------- The record ----------------

        /// <summary>What a candidate runs on, the first that holds: their work in this seat, their work of its kind for the colony, a name the colony gave them, a life goal reached.</summary>
        public enum RecordKind
        {
            None,
            InSeat,
            ForColony,
            Name,
            Goal
        }

        /// <summary>The seat's work done for the colony that is worth running on: this much at least (a single wound dressed isn't a record).</summary>
        public const int ColonyWorkFrom = 10;

        /// <summary>
        /// The record a candidate runs on: in the seat when they hold it and did some of its work since their promise
        /// (<paramref name="inSeat"/>, -1 for no promise to count from); else the seat's kind of work for the colony
        /// (<paramref name="lifetime"/>) when it is worth saying; else a name the colony gave them; else a life goal reached.
        /// </summary>
        public static RecordKind Record(bool holds, int inSeat, int lifetime, bool named, bool goal)
        {
            if (holds && inSeat > 0) return RecordKind.InSeat;
            if (lifetime >= ColonyWorkFrom) return RecordKind.ForColony;
            if (named) return RecordKind.Name;
            if (goal) return RecordKind.Goal;
            return RecordKind.None;
        }

        /// <summary>What a vote gains from a holder's work in the seat: something for some work, nothing for none (a broken promise costs on its own).</summary>
        public static float WorkLeanOf(bool holds, int inSeat) => holds && inSeat > 0 ? WorkLean : 0f;
    }
}
