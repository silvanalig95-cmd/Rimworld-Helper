using System;
using System.Collections.Generic;
using System.Globalization;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What the council window keeps with each game: the things the player follows (at most twelve, by their text form,
    /// "p123", "t55": UI/FollowMath.cs) and the wishes pinned to This week, a week of each colonist's trust (one sample a
    /// day, for the trends), and the answered dilemmas (<see cref="DilemmaLog"/>). Things are kept by their text form,
    /// never by reference, so something gone can't break a save. Its tick does almost nothing: one check every 250 ticks,
    /// a sample once a day, and the dilemma watch only while an answer is still playing out. It also hears the optional
    /// keyboard shortcuts for the council's pages (<see cref="CouncilKeys"/>) and the search's key.
    /// </summary>
    public class WeekMemory : GameComponent
    {
        /// <summary>What the player follows (docs/design/interface.md, "Following"): things' text forms, at most twelve.</summary>
        public List<string> following = new List<string>();
        /// <summary>The colonists pinned before following, by ThingID: read from an older save only, then followed.</summary>
        private List<string> pinnedPawns;
        public List<int> pinnedWishes = new List<int>();
        public List<DilemmaRecord> dilemmas = new List<DilemmaRecord>();
        /// <summary>Trust by day, per colonist: "ThingID" → "lastDay|v;v;v" (oldest first), parallel lists for saving.</summary>
        private List<string> trustKeys = new List<string>();
        private List<string> trustValues = new List<string>();
        private int lastSampleDay = -1;

        /// <summary>The trails, read from the saved strings once and kept as numbers.</summary>
        [Unsaved] private Dictionary<string, Trail> trails;

        public const int TrailDays = 8;
        /// <summary>A colonist not seen for this many days is forgotten.</summary>
        public const int ForgetDays = 10;

        private class Trail
        {
            public int lastDay;
            public readonly List<float> values = new List<float>(TrailDays);
        }

        private static WeekMemory cached;
        private static Game cachedGame;

        public static WeekMemory Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<WeekMemory>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public WeekMemory(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            WeekPanel.Dirty();
            PeopleTable.Dirty();
        }

        // ---------------- Following, and the wishes pinned ----------------

        public bool IsFollowed(ThingRef r) => FollowMath.Has(following, r);

        /// <summary>
        /// Follows a thing, or stops following it (the pin on its card's head, a row, the People table). A thirteenth
        /// is refused with a word why.
        /// </summary>
        public FollowChange ToggleFollow(ThingRef r)
        {
            var change = FollowMath.Toggle(following, r);
            if (change == FollowChange.Full)
                // direct: the answer to the player's own click.
                Messages.Message("CC_Follow_Full".Translate(FollowMath.Max.Named("COUNT")).Resolve(), MessageTypeDefOf.RejectInput, false);
            if (change == FollowChange.Followed || change == FollowChange.Unfollowed)
            {
                WeekPanel.Dirty();
                PeopleTable.Dirty();
                FollowWords.Dirty();
            }
            return change;
        }

        /// <summary>The things followed, in the order they were followed.</summary>
        public IEnumerable<ThingRef> Followed()
        {
            foreach (var s in following)
                if (ThingRef.TryParse(s, out var r))
                    yield return r;
        }

        /// <summary>A colonist is followed (what was "pinned").</summary>
        public bool IsPinned(Pawn p) => p != null && IsFollowed(ThingRefs.Of(p));

        public bool IsPinned(Wish w) => w != null && pinnedWishes.Contains(w.id);

        public void TogglePin(Pawn p)
        {
            if (p != null) ToggleFollow(ThingRefs.Of(p));
        }

        public void TogglePin(Wish w)
        {
            if (w == null) return;
            if (!pinnedWishes.Remove(w.id)) pinnedWishes.Add(w.id);
            WeekPanel.Dirty();
        }

        // ---------------- Trust over the week ----------------

        /// <summary>How much their trust moved over about a week (at least three days of samples), or null when it's too soon to say.</summary>
        public float? TrustTrend(Pawn p, float now)
        {
            if (p == null) return null;
            Load();
            if (!trails.TryGetValue(p.ThingID, out var t) || t.values.Count < 3) return null;
            int back = Mathf.Min(7, t.values.Count);
            return now - t.values[t.values.Count - back];
        }

        private void Load()
        {
            if (trails != null) return;
            trails = new Dictionary<string, Trail>();
            for (int i = 0; i < trustKeys.Count && i < trustValues.Count; i++)
            {
                string key = trustKeys[i], raw = trustValues[i];
                if (key.NullOrEmpty() || raw.NullOrEmpty()) continue;
                int bar = raw.IndexOf('|');
                if (bar <= 0 || !int.TryParse(raw.Substring(0, bar), NumberStyles.Integer, CultureInfo.InvariantCulture, out int day)) continue;
                var t = new Trail { lastDay = day };
                foreach (var part in raw.Substring(bar + 1).Split(';'))
                    if (float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                        t.values.Add(v);
                if (t.values.Count > 0) trails[key] = t;
            }
        }

        private void Store()
        {
            if (trails == null) return;
            trustKeys = new List<string>(trails.Count);
            trustValues = new List<string>(trails.Count);
            foreach (var kv in trails)
            {
                var parts = new string[kv.Value.values.Count];
                for (int i = 0; i < parts.Length; i++) parts[i] = kv.Value.values[i].ToString("0.#", CultureInfo.InvariantCulture);
                trustKeys.Add(kv.Key);
                trustValues.Add(kv.Value.lastDay.ToString(CultureInfo.InvariantCulture) + "|" + string.Join(";", parts));
            }
        }

        /// <summary>Once a day: each colonist's trust, for the week's trend; those not seen for a while are forgotten.</summary>
        private void Sample(int day)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            Load();
            // A copy: the game reuses the list it hands out.
            foreach (var p in new List<Pawn>(PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoCryptosleep))
            {
                if (p == null || p.IsQuestLodger() || !People.HasInnerLife(p)) continue;
                if (!trails.TryGetValue(p.ThingID, out var t)) trails[p.ThingID] = t = new Trail();
                if (t.lastDay == day && t.values.Count > 0) t.values[t.values.Count - 1] = comp.standings.TrustOf(p);
                else t.values.Add(comp.standings.TrustOf(p));
                t.lastDay = day;
                if (t.values.Count > TrailDays) t.values.RemoveRange(0, t.values.Count - TrailDays);
            }
            List<string> gone = null;
            foreach (var kv in trails)
                if (day - kv.Value.lastDay > ForgetDays)
                    (gone ??= new List<string>()).Add(kv.Key);
            if (gone != null)
                foreach (var k in gone)
                    trails.Remove(k);
            // Pins of wishes no longer open go; what is followed stays, gone or not, until the player lets it go.
            if (pinnedWishes.Count > 0) pinnedWishes.RemoveAll(id => !comp.wishes.Exists(w => w.id == id && w.state == WishState.Open));
        }

        // ---------------- The clock ----------------

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % Sched.RareTicks != DilemmaLog.WatchTick) return;
            using var clock = PassClock.Time(Pass.RareWeekMemory);
            try
            {
                int day = GenDate.DaysPassed;
                if (day != lastSampleDay)
                {
                    lastSampleDay = day;
                    Sample(day);
                }
                if (DilemmaLog.AnyWatched(dilemmas)) DilemmaLog.Watch(dilemmas);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("council window memory", e);
            }
        }

        public override void GameComponentOnGUI()
        {
            try
            {
                CouncilKeys.OnGUI();
                // Back and forward through Rimfolk's windows (keys, side buttons), the search's key, and a name's preview (UI/Cards/).
                Nav.OnGUI();
                Search.OnGUI();
                Previews.OnGUI();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("council shortcuts", e);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving) Store();
            Scribe_Collections.Look(ref following, "following", LookMode.Value);
            // An older save's pinned colonists, followed from now on.
            if (Scribe.mode == LoadSaveMode.LoadingVars) Scribe_Collections.Look(ref pinnedPawns, "pinnedPawns", LookMode.Value);
            Scribe_Collections.Look(ref pinnedWishes, "pinnedWishes", LookMode.Value);
            Scribe_Collections.Look(ref dilemmas, "dilemmas", LookMode.Deep);
            Scribe_Collections.Look(ref trustKeys, "trustKeys", LookMode.Value);
            Scribe_Collections.Look(ref trustValues, "trustValues", LookMode.Value);
            Scribe_Values.Look(ref lastSampleDay, "lastSampleDay", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                following ??= new List<string>();
                if (pinnedPawns != null) FollowMath.TakeOldPins(following, pinnedPawns);
                pinnedPawns = null;
                FollowMath.Clean(following);
                pinnedWishes ??= new List<int>();
                dilemmas ??= new List<DilemmaRecord>();
                dilemmas.RemoveAll(d => d == null);
                trustKeys ??= new List<string>();
                trustValues ??= new List<string>();
                trails = null;
            }
        }
    }
}
