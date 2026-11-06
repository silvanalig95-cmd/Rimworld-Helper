using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>An answer whose play-outs are still being watched: its event in the history and what it set going.</summary>
    public class AnswerWatch : IExposable
    {
        /// <summary>The answer's event in the history (HistoryEntry.id).</summary>
        public int entry;
        public List<int> plays = new List<int>();
        /// <summary>Of those, the ones seen under way, so their end can be told from one that never came about.</summary>
        public List<int> started = new List<int>();
        public int tookPlace;
        public int fellThrough;
        /// <summary>The first thing it set going ("the victory feast"), and a word on how it went once over ("14 jobs done").</summary>
        public string label;
        public string note;

        public void ExposeData()
        {
            Scribe_Values.Look(ref entry, "entry");
            Scribe_Collections.Look(ref plays, "plays", LookMode.Value);
            Scribe_Collections.Look(ref started, "started", LookMode.Value);
            Scribe_Values.Look(ref tookPlace, "tookPlace");
            Scribe_Values.Look(ref fellThrough, "fellThrough");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref note, "note");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                plays ??= new List<int>();
                started ??= new List<int>();
            }
        }
    }

    /// <summary>
    /// The answers' log: every question letter, answered or left to run out, writes how it went into the one history
    /// (<see cref="AnswerMath"/>): the question, the answer, the follow-up told, and the chronicle's line for it. What
    /// the answer set going on the map (a feast, a work party, an errand) is watched, a light look every 250 ticks and only
    /// while something is, until it took place or fell through; the record then says so. The chronicle shows each answer
    /// with what came of it. Never throws: the question itself matters more than its record.
    /// </summary>
    public static class Answers
    {
        /// <summary>The most answers watched at once (each only for the days its play-outs take).</summary>
        public const int MaxWatched = 40;

        private static List<AnswerWatch> Watched => WeekMemory.Instance?.answers;

        /// <summary>
        /// A question answered (or its time run out): its source (<see cref="AnswerMath.Sources"/>), the question and the
        /// answer as the letter put them, the follow-up told (if any), who it was mostly about, the people it was with, the
        /// colony it was asked at; the chronicle's line for it (else "{QUESTION}: the council chose "{ANSWER}"."). A small
        /// one (<paramref name="minor"/>) is kept but not chronicled; a milestone (<paramref name="weight"/>) never folds.
        /// What it set going on the map this tick is watched. Returns the event, or null.
        /// </summary>
        public static HistoryEntry Record(string source, string question, string answer, string outcome = null, Pawn about = null, Faction people = null, Map map = null,
            bool byDefault = false, bool risk = false, string line = null, List<PlayOut> plays = null, bool minor = false, int weight = HistoryMath.Everyday)
        {
            try
            {
                if (question.NullOrEmpty() && answer.NullOrEmpty()) return null;
                var actors = new List<HistoryActor>();
                if (about != null) actors.Add(new HistoryActor(Refs.Of(about), HistoryRole.About));
                if (people != null && !people.IsPlayer) actors.Add(new HistoryActor(Refs.Of(people), HistoryRole.With));
                var home = map != null && Homes.IsHome(map) ? map : about?.MapHeld != null && Homes.IsHome(about.MapHeld) ? about.MapHeld : Homes.Seat;
                var set = SetGoing(plays);
                var values = AnswerMath.Values(question, answer, outcome, byDefault, risk, set.Count > 0);
                string words = (line.NullOrEmpty() ? Chronicle.AnswerLine(question, answer, byDefault) : line).CapitalizeFirst();
                var e = History.Add(AnswerMath.KindFor(source, minor), actors, Refs.Colony(home), weight, words, null, values);
                if (e != null && set.Count > 0) Watch(e, set);
                return e;
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("answers: a record of " + source, ex);
                return null;
            }
        }

        /// <summary>
        /// An option of a question letter that answers it: its own action first, then the answer written down (the letter's
        /// label as the question, the option's words as the answer). Returns the same option.
        /// </summary>
        public static DiaOption Recorded(DiaOption option, Letter letter, string source, Faction people = null, Pawn about = null, Func<Map> map = null, bool minor = false)
        {
            if (option == null) return null;
            var act = option.action;
            string answer = TextOf(option);
            string question = letter?.Label.Resolve();
            option.action = () =>
            {
                act?.Invoke();
                Map at = null;
                try
                {
                    at = map?.Invoke();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("answers: where " + source + " was asked", e);
                }
                Record(source, question, answer, null, about, people, at, minor: minor);
            };
            return option;
        }

        /// <summary>
        /// Every option of a question letter that answers it, recorded (<see cref="Recorded"/>); the letter's stock options
        /// (close, put off, jump to the place, reject, the quest's page) and disabled ones pass as they are.
        /// </summary>
        public static IEnumerable<DiaOption> RecordedAll(IEnumerable<DiaOption> options, Letter letter, string source, Faction people = null, Pawn about = null,
            Func<Map> map = null)
        {
            foreach (var o in options)
            {
                if (o == null) continue;
                yield return o.disabled || Stock(o) ? o : Recorded(o, letter, source, people, about, map);
            }
        }

        private static string[] stock;

        /// <summary>An option's words (the game keeps them to itself).</summary>
        private static readonly AccessTools.FieldRef<DiaOption, string> OptionText = AccessTools.FieldRefAccess<DiaOption, string>("text");

        private static string TextOf(DiaOption o)
        {
            try
            {
                return o == null ? null : OptionText(o);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("answers: an option's words", e);
                return null;
            }
        }

        /// <summary>One of a letter's stock options, by its words.</summary>
        private static bool Stock(DiaOption o)
        {
            stock ??= new[] { "Close", "PostponeLetter", "JumpToLocation", "RejectLetter", "ViewRelatedQuest" }.Where(k => k.CanTranslate()).Select(k => k.Translate().ToString()).ToArray();
            string text = TextOf(o);
            return text == null || Array.IndexOf(stock, text) >= 0;
        }

        /// <summary>The play-outs given, or else those made this very tick that no other answer has already.</summary>
        private static List<PlayOut> SetGoing(List<PlayOut> given)
        {
            var list = new List<PlayOut>();
            if (given != null)
            {
                foreach (var p in given)
                    if (p != null && p.state != PlayState.Done)
                        list.Add(p);
                return list;
            }
            var all = PlayOutTracker.Instance?.plays;
            if (all == null) return list;
            int now = Find.TickManager.TicksGame;
            foreach (var p in all)
                if (p != null && p.created == now && p.state != PlayState.Done && !Claimed(p.id))
                    list.Add(p);
            return list;
        }

        private static bool Claimed(int id)
        {
            var w = Watched;
            if (w != null)
                for (int i = w.Count - 1; i >= 0 && i >= w.Count - 6; i--)
                    if (w[i].plays.Contains(id))
                        return true;
            return false;
        }

        private static void Watch(HistoryEntry e, List<PlayOut> set)
        {
            var w = Watched;
            if (w == null) return;
            var watch = new AnswerWatch { entry = e.id };
            foreach (var p in set)
            {
                watch.plays.Add(p.id);
                if (watch.label == null && !p.label.NullOrEmpty()) watch.label = p.label;
            }
            w.Add(watch);
            if (w.Count > MaxWatched) w.RemoveRange(0, w.Count - MaxWatched);
        }

        public static bool AnyWatched(List<AnswerWatch> list) => list != null && list.Count > 0;

        /// <summary>Follows what the answers set going: under way, taken place or fallen through; once all of it is over, the record says what came of it.</summary>
        public static void Watch(List<AnswerWatch> list)
        {
            if (list == null || list.Count == 0) return;
            var tracker = PlayOutTracker.Instance;
            var book = WorldLedger.Instance?.Book;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var w = list[i];
                var e = book?.Get(w.entry);
                // The record is gone (folded, or another game's): nothing left to tell.
                if (e == null || !HistoryKinds.Matches(e.kind, AnswerMath.Family) || e.values == null)
                {
                    list.RemoveAt(i);
                    continue;
                }
                for (int j = w.plays.Count - 1; j >= 0; j--)
                {
                    int id = w.plays[j];
                    var p = tracker?.Get(id);
                    bool running = p != null && (p.state == PlayState.Running || p.started >= 0);
                    if (running && !w.started.Contains(id))
                    {
                        w.started.Add(id);
                        if (AnswerMath.Started(e.values)) book.Touch(e);
                    }
                    if (p != null && p.state != PlayState.Done) continue;
                    // Gone or done: as the tracker remembers it ended, else it took place if it was ever seen under way.
                    var res = tracker?.ResultOf(id);
                    bool seen = w.started.Remove(id) || running;
                    if (res != null ? res.took : seen) w.tookPlace++;
                    else w.fellThrough++;
                    if (w.note == null && !(res?.note).NullOrEmpty()) w.note = res.note;
                    w.plays.RemoveAt(j);
                }
                if (w.plays.Count > 0) continue;
                string note = w.note.NullOrEmpty() ? null : w.label.NullOrEmpty() ? w.note : w.label + ": " + w.note;
                if (AnswerMath.Settle(e.values, w.tookPlace, w.fellThrough, note)) book.Touch(e);
                list.RemoveAt(i);
            }
        }
    }
}
