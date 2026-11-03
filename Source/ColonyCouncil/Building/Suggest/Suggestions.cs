using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One suggestion: a single step the player can take with one click. Its source works it out again from the live
    /// game every time it is asked (so it goes by itself once its reason is gone) and gives it a stable key, so a "Not
    /// this" holds from hour to hour. It says what to do (<see cref="title"/>), why in numbers (<see cref="reason"/>),
    /// how soon (<see cref="days"/> until something runs short, else a <see cref="level"/>), and carries the doing:
    /// <see cref="apply"/> returns what was done, with its undo and the check that later says whether it worked.
    /// Nothing is done without the player's click. See "The shared suggestion core" in docs/plan-ahead.md.
    /// </summary>
    public class Suggestion
    {
        /// <summary>Stable from hour to hour: "source:what:where" (ahead:raise:12345:CookMealSimple).</summary>
        public string key;
        /// <summary>Who suggests it ("ahead"; later the advisor, the base review, the plan, the defenses, the automations).</summary>
        public string source;
        /// <summary>What part of the colony it is about ("meal", "fields", "medicine"), for grouping.</summary>
        public string area;
        public string title, reason;
        /// <summary>Days until what it is about runs short; SuggestMath.NoRush when it isn't about time.</summary>
        public float days = SuggestMath.NoRush;
        /// <summary>For sources that rank by level instead (4 critical to 0 an idea); used after the days.</summary>
        public int level;
        /// <summary>What the urgency pill says when it isn't a matter of days ("when the prisoners join"); null: by the days.</summary>
        public string when;
        /// <summary>What the urgency pill says in place of the days, when its hours say more ("Frost in 9 hours"); null: by the days or <see cref="when"/>.</summary>
        public string pill;
        public Map map;
        /// <summary>The button's words and what it does on the map (null: "Do it" and the reason).</summary>
        public string doLabel, doTip;
        /// <summary>Does it: returns what was done (with its undo and check), or null when it couldn't. Null: there is nothing to do with one click; the suggestion only says why and shows where.</summary>
        public Func<SuggestDone> apply;
        /// <summary>The cells Show outlines on the map; worked out the first time they are asked for.</summary>
        public Func<List<IntVec3>> cells;
        /// <summary>Where Show takes the camera.</summary>
        public LookTargets look;
        private List<IntVec3> cellsKept;

        public bool CanApply => apply != null;
        public bool CanShow => cells != null || look != null && look.IsValid;
        public int Level => Math.Max(level, days < SuggestMath.NoRush ? SuggestMath.LevelOf(days) : 0);

        public List<IntVec3> Cells()
        {
            if (cellsKept != null || cells == null) return cellsKept ?? new List<IntVec3>();
            try
            {
                cellsKept = cells() ?? new List<IntVec3>();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("suggest cells " + key, e);
                cellsKept = new List<IntVec3>();
            }
            return cellsKept;
        }
    }

    /// <summary>How a step that was done is taken back (saved with the step, so it holds over a save).</summary>
    public abstract class SuggestUndo : IExposable
    {
        /// <summary>Whether it can still be taken back on this map.</summary>
        public virtual bool Possible(Map map) => map != null;
        public abstract void Undo(Map map);
        public virtual void ExposeData() { }
    }

    /// <summary>How a step that was done is checked a while later: whether it worked, and if not what is in the way.</summary>
    public abstract class SuggestCheck : IExposable
    {
        /// <summary>The wait after the doing before the first look (and between looks).</summary>
        public int waitTicks = SuggestMath.TicksPerDay;

        public abstract bool Worked(SuggestDone done, out string whyNot);

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref waitTicks, "waitTicks", SuggestMath.TicksPerDay);
        }
    }

    /// <summary>A step the player did, kept in the log: what it was, when, its undo, its check and what the check found.</summary>
    public class SuggestDone : IExposable
    {
        public string key, source, area, text;
        public int tick;
        public Map map;
        public SuggestUndo undo;
        public SuggestCheck check;
        /// <summary>SuggestMath.Waiting, Worked, Failed, Undone or NoCheck.</summary>
        public int state = SuggestMath.Waiting;
        public int tries;
        /// <summary>What is in the way, when the check found it didn't work.</summary>
        public string why;
        /// <summary>What the check found when it worked, in words, if it says more than that it worked (a frost harvest: whether the frost came and how many were saved).</summary>
        public string found;

        public bool CanUndo => undo != null && state != SuggestMath.Undone && map != null && SafePossible();

        private bool SafePossible()
        {
            try
            {
                return undo.Possible(map);
            }
            catch
            {
                return false;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref source, "source");
            Scribe_Values.Look(ref area, "area");
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_References.Look(ref map, "map");
            Scribe_Deep.Look(ref undo, "undo");
            Scribe_Deep.Look(ref check, "check");
            Scribe_Values.Look(ref state, "state");
            Scribe_Values.Look(ref tries, "tries");
            Scribe_Values.Look(ref why, "why");
            Scribe_Values.Look(ref found, "found");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                key ??= "";
                text ??= "";
                if (check == null && state == SuggestMath.Waiting) state = SuggestMath.NoCheck;
            }
        }
    }

    /// <summary>
    /// The council's suggestions, kept for every source alike (CouncilGameComponent.suggest): what the player set aside
    /// with "Not this" (by key, for good or for some days, brought back at a click), and the log of steps done, each
    /// with its undo and, a while after, the check of whether it worked (the hourly look, from HourlyPeople). A step
    /// still waiting for its check isn't suggested again, so a step isn't offered twice before it could work.
    /// </summary>
    public class SuggestTracker : IExposable
    {
        public List<string> declinedKeys = new List<string>();
        public List<string> declinedTitles = new List<string>();
        public List<int> declinedUntil = new List<int>();
        public List<SuggestDone> done = new List<SuggestDone>();

        public static SuggestTracker Instance => CouncilGameComponent.Instance?.suggest;

        /// <summary>Goes up whenever something is set aside, brought back, done, undone or checked, so a page knows to work its suggestions out again (not saved).</summary>
        public int Version { get; private set; }
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------- Not this ----------------

        public bool Declined(string key)
        {
            int i = declinedKeys.IndexOf(key);
            return i >= 0 && SuggestMath.StillDeclined(declinedUntil[i], Now);
        }

        /// <summary>Sets a suggestion aside: for good (until brought back), or for so many days.</summary>
        public void Decline(Suggestion s, float days = 0f)
        {
            if (s?.key == null) return;
            Decline(s.key, s.title, days);
        }

        /// <summary>
        /// Sets aside by key, for a source whose advice isn't built as a Suggestion (an advisor card, a base finding, a
        /// piece of defense advice, a note on a line, an automation's action undone): the same store, the same Bring back.
        /// </summary>
        public void Decline(string key, string title, float days = 0f)
        {
            if (key.NullOrEmpty()) return;
            BringBack(key);
            declinedKeys.Add(key);
            declinedTitles.Add(title.NullOrEmpty() ? key : title);
            declinedUntil.Add(SuggestMath.DeclineUntil(Now, days));
            while (declinedKeys.Count > SuggestMath.DeclinedKept) RemoveDeclined(0);
            Version++;
        }

        public void BringBack(string key)
        {
            int i = declinedKeys.IndexOf(key);
            if (i >= 0) RemoveDeclined(i);
            Version++;
        }

        private void RemoveDeclined(int i)
        {
            declinedKeys.RemoveAt(i);
            declinedTitles.RemoveAt(i);
            declinedUntil.RemoveAt(i);
        }

        /// <summary>What a source has set aside, newest first: key, the words it had, and until when (-1 for good).</summary>
        public List<(string key, string title, int until)> DeclinedFor(string source) => DeclinedFor(source == null ? null : new[] { source });

        /// <summary>What any of these sources has set aside (by the start of the key: "adv", "base", "line:12"), newest first.</summary>
        public List<(string key, string title, int until)> DeclinedFor(IList<string> sources)
        {
            var list = new List<(string, string, int)>();
            int now = Now;
            for (int i = declinedKeys.Count - 1; i >= 0; i--)
                if (SuggestMath.OfSource(declinedKeys[i], sources) && SuggestMath.StillDeclined(declinedUntil[i], now))
                    list.Add((declinedKeys[i], declinedTitles[i], declinedUntil[i]));
            return list;
        }

        /// <summary>The keys set aside now, for a planner that gives their share to the next way.</summary>
        public HashSet<string> DeclinedKeys()
        {
            var set = new HashSet<string>();
            int now = Now;
            for (int i = 0; i < declinedKeys.Count; i++)
                if (SuggestMath.StillDeclined(declinedUntil[i], now)) set.Add(declinedKeys[i]);
            return set;
        }

        // ---------------- Doing and undoing ----------------

        /// <summary>A step done whose check hasn't come yet: not suggested again meanwhile.</summary>
        public bool Waiting(string key) => done.Any(d => d.key == key && d.state == SuggestMath.Waiting);

        /// <summary>The last time this step was done it was undone again: an automation leaves it alone (the player took it back).</summary>
        public bool UndoneLast(string key)
        {
            for (int i = done.Count - 1; i >= 0; i--)
                if (done[i].key == key) return done[i].state == SuggestMath.Undone;
            return false;
        }

        /// <summary>Neither set aside nor waiting for its check: a key that may be offered (or done by an automation) now.</summary>
        public bool Offered(string key) => !key.NullOrEmpty() && !Declined(key) && !Waiting(key);

        /// <summary>What a source's suggestions are left once those set aside and those waiting for their check are taken out, the soonest first.</summary>
        public List<Suggestion> Open(IEnumerable<Suggestion> all) =>
            all.Where(s => s?.key != null && !Declined(s.key) && !Waiting(s.key))
                .OrderBy(s => s, Comparer<Suggestion>.Create((a, b) => SuggestMath.Compare(a.days, a.Level, a.key, b.days, b.Level, b.key))).ToList();

        /// <summary>Does the step (the player's click), tells the player, and keeps it in the log; null when it couldn't be done.</summary>
        public SuggestDone Apply(Suggestion s)
        {
            if (s?.apply == null) return null;
            SuggestDone d;
            try
            {
                d = s.apply();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("suggest apply " + s.key, e);
                d = null;
            }
            if (d == null)
            {
                Messages.Message("CC_Suggest_Couldnt".Translate(s.title).Resolve(), MessageTypeDefOf.RejectInput, false);
                return null;
            }
            d.key ??= s.key;
            d.source ??= s.source;
            d.area ??= s.area;
            d.text = d.text.NullOrEmpty() ? s.title : d.text;
            d.map ??= s.map;
            d.tick = Now;
            d.state = d.check != null ? SuggestMath.Waiting : SuggestMath.NoCheck;
            done.Add(d);
            SuggestMath.TrimOldest(done, SuggestMath.DoneKept);
            Version++;
            Messages.Message("CC_Suggest_Done".Translate(d.text).Resolve(), s.look ?? LookTargets.Invalid, MessageTypeDefOf.TaskCompletion, false);
            return d;
        }

        public void Undo(SuggestDone d)
        {
            if (d == null || !d.CanUndo) return;
            try
            {
                d.undo.Undo(d.map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("suggest undo " + d.key, e);
            }
            d.state = SuggestMath.Undone;
            Version++;
            Messages.Message("CC_Suggest_Undone".Translate(d.text).Resolve(), MessageTypeDefOf.SilentInput, false);
        }

        /// <summary>The steps a source did on a map, newest first.</summary>
        public List<SuggestDone> DoneFor(string source, Map map, int count) => DoneFor(source == null ? null : new[] { source }, map, count);

        /// <summary>The steps any of these sources did on a map (by the source's name, or the start of the key), newest first.</summary>
        public List<SuggestDone> DoneFor(IList<string> sources, Map map, int count) =>
            done.AsEnumerable().Reverse().Where(d => (sources == null || sources.Contains(d.source) || SuggestMath.OfSource(d.key, sources)) && (map == null || d.map == null || d.map == map))
                .Take(count).ToList();

        // ---------------- The hour ----------------

        /// <summary>Each hour: the steps due their check looked at; "Not this" for some days let go once they are over.</summary>
        public void Hourly()
        {
            int now = Now;
            for (int i = declinedKeys.Count - 1; i >= 0; i--)
                if (!SuggestMath.StillDeclined(declinedUntil[i], now)) RemoveDeclined(i);
            foreach (var d in done)
            {
                if (d.state != SuggestMath.Waiting || d.check == null) continue;
                if (d.map == null || !Find.Maps.Contains(d.map))
                {
                    d.state = SuggestMath.NoCheck;
                    continue;
                }
                if (!SuggestMath.CheckDue(d.tick, d.check.waitTicks, d.tries, now)) continue;
                bool worked;
                string why;
                try
                {
                    worked = d.check.Worked(d, out why);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("suggest check " + d.key, e);
                    d.state = SuggestMath.NoCheck;
                    continue;
                }
                d.state = SuggestMath.AfterLook(worked, d.tries);
                d.tries++;
                d.why = worked ? null : why;
                Version++;
            }
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                int now = Now;
                for (int i = declinedKeys.Count - 1; i >= 0; i--)
                    if (!SuggestMath.StillDeclined(declinedUntil[i], now)) RemoveDeclined(i);
            }
            Scribe_Collections.Look(ref declinedKeys, "declinedKeys", LookMode.Value);
            Scribe_Collections.Look(ref declinedTitles, "declinedTitles", LookMode.Value);
            Scribe_Collections.Look(ref declinedUntil, "declinedUntil", LookMode.Value);
            Scribe_Collections.Look(ref done, "done", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                declinedKeys ??= new List<string>();
                declinedTitles ??= new List<string>();
                declinedUntil ??= new List<int>();
                if (declinedTitles.Count != declinedKeys.Count || declinedUntil.Count != declinedKeys.Count)
                {
                    declinedKeys.Clear();
                    declinedTitles.Clear();
                    declinedUntil.Clear();
                }
                done ??= new List<SuggestDone>();
                done.RemoveAll(d => d == null);
                SuggestMath.TrimOldest(done, SuggestMath.DoneKept);
            }
        }
    }
}
