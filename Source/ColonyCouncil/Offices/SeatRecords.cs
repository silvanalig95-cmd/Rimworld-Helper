using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Elections on a record (decision 9 of the review, 6 November 2026), the game's side of Offices/RecordMath.cs: a
    /// candidate runs on their own record, what they did in the seat (its work as the game counts it, since their promise)
    /// or for the colony (that work in all, a name the colony gave them, a life goal reached); the winner's promise goes
    /// into the history with what the map showed of it then; the next yearly election reads it back, judges it from what
    /// the map shows now, and a kept promise helps the holder at the vote while a broken one costs, said in the
    /// announcement, at the hustings and in the results.
    /// </summary>
    public partial class OfficeTracker
    {
        // ---------------- What the map shows ----------------

        /// <summary>The seat's work as the game counts it for someone (its records, added up), 0 for a seat with none.</summary>
        public static int Measure(OfficeDef office, Pawn p)
        {
            var w = OfficeNames.Of(office);
            if (w?.measure == null || p?.records == null) return 0;
            float sum = 0f;
            foreach (var name in w.measure)
            {
                var def = DefDatabase<RecordDef>.GetNamedSilentFail(name);
                if (def == null) continue;
                try
                {
                    sum += p.records.GetValue(def);
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("seat record " + name, e);
                }
            }
            return Mathf.RoundToInt(sum);
        }

        /// <summary>The seat's work in words: "tended to the sick and hurt 23 times", or null for a seat with none.</summary>
        public static string Measured(OfficeDef office, int count)
        {
            var w = OfficeNames.Of(office);
            if (w == null || w.measured.NullOrEmpty() || count <= 0) return null;
            return count == 1 ? w.measuredOne : w.measured.Formatted(count.Named("COUNT")).Resolve();
        }

        /// <summary>What the map shows now of the thing a promise was about, for this colony (-1 where it can't be read).</summary>
        private float Reading(PromiseTest test, OfficeDef office, Pawn holder)
        {
            var comp = CouncilGameComponent.Instance;
            var map = HomeMap;
            var people = Eligible();
            try
            {
                switch (test)
                {
                    case PromiseTest.Trust:
                        return comp == null || people.Count == 0 ? -1f : people.Average(p => comp.standings.TrustOf(p));
                    case PromiseTest.Quarrels:
                        return comp?.social == null ? -1f : comp.social.ties.Count(t => t.kind == TieKind.Rivals && t.announced && t.a != null && t.b != null && Ours(t.a) && Ours(t.b));
                    case PromiseTest.Lost:
                        return Find.StoryWatcher?.statsRecord?.colonistsKilled ?? -1f;
                    case PromiseTest.Food:
                        return map == null ? -1f : MapFacts.Of(map).FoodDays;
                    case PromiseTest.Medicine:
                        return map == null ? -1f : MapFacts.Of(map).Medicine / (float)Mathf.Max(1, map.mapPawns.FreeColonistsSpawnedCount);
                    case PromiseTest.Sickbeds:
                        return map == null ? -1f : map.listerBuildings.allBuildingsColonist.Count(b => b is Building_Bed bed && bed.Medical);
                    case PromiseTest.Mood:
                        var moods = people.Where(p => p.needs?.mood != null).Select(p => p.needs.mood.CurLevelPercentage).ToList();
                        return moods.Count == 0 ? -1f : moods.Average();
                    case PromiseTest.Herd:
                        return map == null ? -1f : map.mapPawns.SpawnedColonyAnimals.Count;
                    case PromiseTest.Days:
                        var kept = DefDatabase<RecordDef>.GetNamedSilentFail(CouncilRecords.DaysKept);
                        return kept == null ? -1f : PawnsFinder.AllMaps_FreeColonists.Where(Ours).Sum(p => p.records?.GetValue(kept) ?? 0f);
                    case PromiseTest.Faith:
                        return DecreeWork.ColonyFaith == null || people.Count == 0 ? -1f : people.Count(DecreeWork.Believer) / (float)people.Count;
                    case PromiseTest.Work:
                        return holder == null ? -1f : Measure(office, holder);
                }
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("promise reading " + test, e);
            }
            return -1f;
        }

        // ---------------- A promise made ----------------

        /// <summary>Its words for the history and the letters, the holder's pronoun in it ("to pass on what she knows").</summary>
        private static string PromiseWords(OfficePlank plank, Pawn p) =>
            plank?.promise.NullOrEmpty() != false ? null : plank.promise.Formatted(p.Named("PAWN")).Resolve();

        /// <summary>
        /// The winner of a seat on a plank: the promise goes into the history (never the chronicle: the election's one line
        /// tells the seats), with what the map shows of it now and the seat's work count, to be judged at the next election.
        /// </summary>
        private void Promised(OfficeDef office, int seat, Pawn winner, string plankKey)
        {
            var plank = office?.planks?.FirstOrDefault(x => x != null && x.key == plankKey);
            string words = PromiseWords(plank, winner);
            if (words == null || winner == null) return;
            float reading = Reading(plank.test, office, winner);
            var values = RecordMath.Values(office.defName, seat, plank.key, reading < 0f ? PromiseTest.None : plank.test, reading, Measure(office, winner), words);
            bool that = words.StartsWith("that ") || words.StartsWith("to ");
            string line = (that ? "CC_Promise_MadeThat" : "CC_Promise_Made").Translate(winner.Named("PAWN"), office.Name.Named("OFFICE"), words.Named("PROMISE")).Resolve();
            History.Add(RecordMath.KindPromise, new List<HistoryActor> { new HistoryActor(Refs.Of(winner), HistoryRole.About) }, Refs.Colony(HomeMap ?? Homes.Seat),
                HistoryMath.Milestone, line, null, values);
        }

        /// <summary>The holder's promise for this seat still to be judged: their newest for it from within a year and a season, and not judged yet.</summary>
        private static HistoryEntry PromiseOf(Pawn holder, OfficeDef office, int seat)
        {
            if (holder == null || office == null) return null;
            int now = Find.TickManager.TicksGame;
            var who = Refs.Of(holder);
            var promise = History.Of(who, new[] { RecordMath.KindPromise }, now - GenDate.TicksPerDay * 75)
                .FirstOrDefault(e => RecordMath.Get(e.values, RecordMath.OfficeAt) == office.defName && RecordMath.SeatOf(e.values) == seat);
            if (promise == null) return null;
            // Judged after it was made (the results judge the last promise before the winner's new one goes in, the same tick).
            bool judged = History.Of(who, new[] { RecordMath.KindKept, RecordMath.KindBroken }, promise.tick)
                .Any(e => e.id > promise.id && RecordMath.Get(e.values, RecordMath.OfficeAt) == office.defName && RecordMath.SeatOf(e.values) == seat);
            return judged ? null : promise;
        }

        // ---------------- Judged at the next election ----------------

        /// <summary>
        /// At a yearly election's announcement: the seat's holder and their promise, judged from what the map shows now
        /// (RecordMath.Judge), and what it means at the vote (a kept promise and work done help; a broken promise costs).
        /// </summary>
        private void JudgeRace(ElectionRace race)
        {
            var holder = HolderOf(race.office, race.seat);
            var promise = PromiseOf(holder, race.office, race.seat);
            if (promise == null) return;
            var test = RecordMath.TestOf(promise.values);
            float before = RecordMath.Number(promise.values, RecordMath.ReadingAt);
            float after = test == PromiseTest.None ? -1f : Reading(test, race.office, holder);
            float days = (Find.TickManager.TicksGame - promise.tick) / (float)GenDate.TicksPerDay;
            var verdict = after < 0f ? PromiseVerdict.None : RecordMath.Judge(test, before, after, days);
            int work = Measure(race.office, holder) - (int)RecordMath.Number(promise.values, RecordMath.WorkAt);
            race.holder = holder;
            race.promiseId = promise.id;
            race.promise = RecordMath.Get(promise.values, RecordMath.PromiseAt);
            race.test = test;
            race.verdict = verdict;
            race.evidence = Evidence(test, verdict, holder, race.office, before, after, false);
            race.evidenceMine = Evidence(test, verdict, holder, race.office, before, after, true);
            race.lean = RecordMath.Lean(verdict) + RecordMath.WorkLeanOf(true, work);
        }

        /// <summary>What the map showed of a promise since, a clause after "and" (<paramref name="mine"/>: in the holder's own words).</summary>
        private static string Evidence(PromiseTest test, PromiseVerdict verdict, Pawn holder, OfficeDef office, float before, float after, bool mine)
        {
            string key = RecordMath.EvidenceKey(test, verdict);
            if (key == null) return null;
            int count = RecordMath.EvidenceCount(test, before, after);
            if (test == PromiseTest.Work)
            {
                string measured = Measured(office, count);
                if (measured == null) return null;
                return (mine ? key + "_Mine" : key).Translate(holder.Named("PAWN"), measured.Named("MEASURED")).Resolve();
            }
            if (test == PromiseTest.Lost && verdict == PromiseVerdict.Broken && count == 1) key += "_One";
            return key.Translate(holder.Named("PAWN"), count.Named("COUNT")).Resolve();
        }

        /// <summary>The race's judged promise as a sentence for the letters, or null.</summary>
        internal static string VerdictLine(ElectionRace race)
        {
            if (race?.holder == null || race.verdict == PromiseVerdict.None || race.promise.NullOrEmpty() || race.evidence.NullOrEmpty()) return null;
            return (race.verdict == PromiseVerdict.Kept ? "CC_Promise_KeptLine" : "CC_Promise_BrokenLine")
                .Translate(race.holder.Named("PAWN"), race.promise.Named("PROMISE"), race.evidence.Named("EVIDENCE")).Resolve();
        }

        /// <summary>At the results: the judged promise goes into the holder's history, kept or broken (never the chronicle).</summary>
        private void Judged(ElectionRace race)
        {
            string line = VerdictLine(race);
            if (line == null) return;
            History.Add(race.verdict == PromiseVerdict.Kept ? RecordMath.KindKept : RecordMath.KindBroken,
                new List<HistoryActor> { new HistoryActor(Refs.Of(race.holder), HistoryRole.About) }, Refs.Colony(HomeMap ?? Homes.Seat), HistoryMath.Milestone, line, null,
                new List<string> { race.office.defName, race.seat.ToString() });
        }

        /// <summary>
        /// What a voter's lean toward a candidate gains from the candidate's record in an election under way: the holder's
        /// promise judged (kept or broken) and their work in the seat, for the seat they hold. Nothing for oneself.
        /// </summary>
        private static float RecordLean(OfficeDef office, Pawn voter, Pawn candidate)
        {
            var e = voter == null ? null : ColonyState.Of(voter)?.offices.election;
            if (e == null || voter == candidate || candidate == null) return 0f;
            for (int i = 0; i < e.races.Count; i++)
            {
                var r = e.races[i];
                if (r.office == office && r.holder == candidate) return r.lean;
            }
            return 0f;
        }

        // ---------------- A candidate's record ----------------

        /// <summary>
        /// The record a candidate runs on, as a sentence for the letter, or null: their work in the seat since their
        /// promise when they hold it; the seat's kind of work for the colony in all; a name the colony gave them; a life
        /// goal reached.
        /// </summary>
        private string RecordOf(OfficeDef office, int seat, Pawn p)
        {
            if (p == null || office == null) return null;
            var comp = CouncilGameComponent.Instance;
            bool holds = HolderOf(office, seat) == p;
            var promise = holds ? History.Of(Refs.Of(p), new[] { RecordMath.KindPromise }, int.MinValue)
                .FirstOrDefault(e => RecordMath.Get(e.values, RecordMath.OfficeAt) == office.defName) : null;
            int all = Measure(office, p);
            int inSeat = promise == null ? -1 : all - (int)RecordMath.Number(promise.values, RecordMath.WorkAt);
            // The one name the colony knows them by (KnownAs's short form), as the card and the tables say it.
            string name = KnownAs.Short(p);
            var goal = comp?.aspirations?.Fulfilled(p).OrderByDescending(a => a.doneTick).FirstOrDefault();
            switch (RecordMath.Record(holds, inSeat, all, !name.NullOrEmpty(), goal != null))
            {
                case RecordMath.RecordKind.InSeat:
                    return "CC_Record_InSeat".Translate(p.Named("PAWN"), Measured(office, inSeat).Named("MEASURED")).Resolve();
                case RecordMath.RecordKind.ForColony:
                    return "CC_Record_ForColony".Translate(p.Named("PAWN"), Measured(office, all).Named("MEASURED")).Resolve();
                case RecordMath.RecordKind.Name:
                    return "CC_Record_Name".Translate(p.Named("PAWN"), name.Named("NAME")).Resolve();
                case RecordMath.RecordKind.Goal:
                    return "CC_Record_Goal".Translate(p.Named("PAWN"), goal.Deed.Named("DEED")).Resolve();
                default:
                    return null;
            }
        }
    }
}
