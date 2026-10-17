using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One person in a group: since when, their role (and the one they're drifting into), and how they hold to it.</summary>
    public class GroupMember : IExposable
    {
        public Pawn pawn;
        /// <summary>Their name, kept for the history if they're gone.</summary>
        public string name;
        public int joined;
        public GroupRole role = GroupRole.None;
        public int roleSince;
        public GroupRole pending;
        public int pendingDays;
        /// <summary>Days in a row they've hardly held to the others (they drift off after a few).</summary>
        public int lowDays;
        /// <summary>How much they hold to the others today (not saved: worked out each day).</summary>
        [Unsaved] public float attachment;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref joined, "joined");
            Scribe_Values.Look(ref role, "role");
            Scribe_Values.Look(ref roleSince, "roleSince");
            Scribe_Values.Look(ref pending, "pending");
            Scribe_Values.Look(ref pendingDays, "pendingDays");
            Scribe_Values.Look(ref lowDays, "lowDays");
        }
    }

    /// <summary>A line in a group's history (text, so it outlives the people in it).</summary>
    public class GroupEvent : IExposable
    {
        public int tick;
        public string text;
        /// <summary>A turning point (founded, split, merged, a member lost): kept before everyday lines.</summary>
        public bool major;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref major, "major");
        }
    }

    /// <summary>Bad blood between this group and another (by id): how much, whether it has been noticed, since when, and why.</summary>
    public class GroupRivalry : IExposable
    {
        public int other;
        public float strength;
        public bool announced;
        public int since;
        public string why;

        public void ExposeData()
        {
            Scribe_Values.Look(ref other, "other");
            Scribe_Values.Look(ref strength, "strength");
            Scribe_Values.Look(ref announced, "announced");
            Scribe_Values.Look(ref since, "since");
            Scribe_Values.Look(ref why, "why");
        }
    }

    /// <summary>
    /// One of the colony's social circles: a name chosen once when it formed, the people in it with their roles, how close
    /// it is (cohesion), its ways (norms) and shared views, what it does together, its quarrels with other groups, and its
    /// history. Kept by the <see cref="GroupTracker"/>.
    /// </summary>
    public class SocialGroup : IExposable
    {
        public int id;
        public string name;
        public GroupKindDef kind;
        /// <summary>The kind's name as a defName, kept in case the def is gone.</summary>
        public string kindName;
        /// <summary>Why it formed, in words, at the time ("They go out after game together").</summary>
        public string why;
        public List<GroupMember> members = new List<GroupMember>();
        public int formed;
        public float cohesion = 50f;
        public List<GroupEvent> history = new List<GroupEvent>();
        /// <summary>Its ways (GroupNormDef names).</summary>
        public List<string> norms = new List<string>();
        /// <summary>Views its people share ("topic|1" for, "topic|-1" against).</summary>
        public List<string> views = new List<string>();
        public List<GroupRivalry> rivals = new List<GroupRivalry>();
        /// <summary>The names of those who were in it and are no longer (the last few), so it can be remembered.</summary>
        public List<string> formerNames = new List<string>();
        /// <summary>Days in a row under the split line, and with fewer than two of them at home together.</summary>
        public int lowDays, apartDays;
        /// <summary>The day its habit was last kept, how many times in all, and when the anchor last called them to it.</summary>
        public int habitDay = -1;
        public int habitsKept;
        public int lastArranged = -999999;
        /// <summary>The gathering the anchor called (a PlayOut's id) while it's on, else -1.</summary>
        public int playId = -1;
        /// <summary>Its founding was told (a quiet one, found in an old save, isn't).</summary>
        public bool announced;

        // What happened today, for the day's cohesion (not saved: a reload in the middle of a day only loses a little).
        [Unsaved] public int todayHabits, todayQuarrels, todayMends, todayMemories, todayDied, todayLeft;

        public const int MaxHistory = 14;

        public string Name => name ?? "?";

        public bool Has(Pawn p)
        {
            if (p == null) return false;
            foreach (var m in members)
                if (m.pawn == p)
                    return true;
            return false;
        }

        public GroupMember Member(Pawn p)
        {
            if (p == null) return null;
            foreach (var m in members)
                if (m.pawn == p)
                    return m;
            return null;
        }

        public GroupRole RoleOf(Pawn p) => Member(p)?.role ?? GroupRole.None;

        public Pawn Holder(GroupRole r) => members.FirstOrDefault(m => m.role == r)?.pawn;

        public IEnumerable<Pawn> Pawns => members.Select(m => m.pawn).Where(p => p != null);

        public GroupRivalry RivalryWith(int other) => rivals.FirstOrDefault(r => r.other == other);

        public float RivalryTo(int other) => RivalryWith(other)?.strength ?? 0f;

        public IEnumerable<GroupNormDef> NormDefs => norms.Select(n => DefDatabase<GroupNormDef>.GetNamedSilentFail(n)).Where(n => n != null);

        /// <summary>What it does together: its kind's habits, and those its ways bring.</summary>
        public List<GroupHabitDef> Habits
        {
            get
            {
                var list = new List<GroupHabitDef>();
                if (kind != null) list.AddRange(kind.Habits);
                foreach (var n in NormDefs)
                {
                    var h = n.Habit;
                    if (h != null && !list.Contains(h)) list.Add(h);
                }
                return list.Where(h => h.FitsEra).ToList();
            }
        }

        /// <summary>Someone was in it and is no longer: their name is kept a while.</summary>
        public void Former(string who)
        {
            if (who.NullOrEmpty() || formerNames.Contains(who)) return;
            formerNames.Add(who);
            while (formerNames.Count > 10) formerNames.RemoveAt(0);
        }

        /// <summary>A line in its history; the oldest everyday lines go first once it's full.</summary>
        public void Note(string text, bool major = false)
        {
            if (text.NullOrEmpty()) return;
            history.Add(new GroupEvent { tick = Find.TickManager.TicksGame, text = text, major = major });
            while (history.Count > MaxHistory)
            {
                var drop = history.Take(history.Count - 4).FirstOrDefault(e => !e.major) ?? history[0];
                history.Remove(drop);
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            bool lost = false;
            CouncilScribe.LookDef(ref kind, "kind", ref lost);
            Scribe_Values.Look(ref kindName, "kindName");
            Scribe_Values.Look(ref why, "why");
            Scribe_Collections.Look(ref members, "members", LookMode.Deep);
            Scribe_Values.Look(ref formed, "formed");
            Scribe_Values.Look(ref cohesion, "cohesion", 50f);
            Scribe_Collections.Look(ref history, "history", LookMode.Deep);
            Scribe_Collections.Look(ref norms, "norms", LookMode.Value);
            Scribe_Collections.Look(ref views, "views", LookMode.Value);
            Scribe_Collections.Look(ref rivals, "rivals", LookMode.Deep);
            Scribe_Collections.Look(ref formerNames, "formerNames", LookMode.Value);
            Scribe_Values.Look(ref lowDays, "lowDays");
            Scribe_Values.Look(ref apartDays, "apartDays");
            Scribe_Values.Look(ref habitDay, "habitDay", -1);
            Scribe_Values.Look(ref habitsKept, "habitsKept");
            Scribe_Values.Look(ref lastArranged, "lastArranged", -999999);
            Scribe_Values.Look(ref playId, "playId", -1);
            Scribe_Values.Look(ref announced, "announced");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                members ??= new List<GroupMember>();
                members.RemoveAll(m => m == null || m.pawn == null);
                history ??= new List<GroupEvent>();
                history.RemoveAll(e => e == null || e.text.NullOrEmpty());
                norms ??= new List<string>();
                views ??= new List<string>();
                rivals ??= new List<GroupRivalry>();
                rivals.RemoveAll(r => r == null);
                formerNames ??= new List<string>();
            }
        }
    }

    /// <summary>Someone new to the colony finding their way into a group: how far in, which group, who speaks for them, and whether it shut them out.</summary>
    public class GroupNewcomer : IExposable
    {
        public Pawn pawn;
        public int since;
        public float progress;
        public int groupId = -1;
        public Pawn sponsor;
        /// <summary>The group closed ranks against them (told once).</summary>
        public bool closed;
        /// <summary>Welcomes from members today (not saved).</summary>
        [Unsaved] public int welcomes;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref since, "since");
            Scribe_Values.Look(ref progress, "progress");
            Scribe_Values.Look(ref groupId, "groupId", -1);
            Scribe_References.Look(ref sponsor, "sponsor");
            Scribe_Values.Look(ref closed, "closed");
        }
    }

    /// <summary>A group that is no more: its name, its span and how it ended, and who was in it (by ID), so they can remember it.</summary>
    public class PastGroup : IExposable
    {
        public string name;
        public int formed, ended;
        public string how;
        public List<int> members = new List<int>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref formed, "formed");
            Scribe_Values.Look(ref ended, "ended");
            Scribe_Values.Look(ref how, "how");
            Scribe_Collections.Look(ref members, "members", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) members ??= new List<int>();
        }
    }

    /// <summary>What someone's days go to lately (the hour's samples of their work), and how much of the night they're up.</summary>
    public class WorkLog : IExposable
    {
        public Pawn pawn;
        public List<string> kinds = new List<string>();
        public List<float> counts = new List<float>();
        public float night;

        public const int MaxKinds = 6;

        public float Total
        {
            get
            {
                float t = 0f;
                foreach (var c in counts) t += c;
                return t;
            }
        }

        public void Add(string kind)
        {
            int i = kinds.IndexOf(kind);
            if (i >= 0)
            {
                counts[i] += 1f;
                return;
            }
            if (kinds.Count >= MaxKinds)
            {
                int low = 0;
                for (int k = 1; k < counts.Count; k++)
                    if (counts[k] < counts[low])
                        low = k;
                kinds.RemoveAt(low);
                counts.RemoveAt(low);
            }
            kinds.Add(kind);
            counts.Add(1f);
        }

        /// <summary>The share of their work that goes to this kind (0-1).</summary>
        public float Share(string kind)
        {
            float t = Total;
            if (t <= 0f) return 0f;
            int i = kinds.IndexOf(kind);
            return i < 0 ? 0f : counts[i] / t;
        }

        /// <summary>Once a day: older work counts for less.</summary>
        public void Fade()
        {
            for (int i = counts.Count - 1; i >= 0; i--)
            {
                counts[i] *= 0.85f;
                if (counts[i] < 0.2f)
                {
                    counts.RemoveAt(i);
                    kinds.RemoveAt(i);
                }
            }
            night *= 0.85f;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Collections.Look(ref kinds, "kinds", LookMode.Value);
            Scribe_Collections.Look(ref counts, "counts", LookMode.Value);
            Scribe_Values.Look(ref night, "night");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && (kinds == null || counts == null || kinds.Count != counts.Count))
            {
                kinds = new List<string>();
                counts = new List<float>();
            }
        }
    }
}
