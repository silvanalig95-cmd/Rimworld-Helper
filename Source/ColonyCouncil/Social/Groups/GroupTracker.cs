using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Keeps the colony's groups (<see cref="SocialGroup"/>), the newcomers finding their way into them, the groups that
    /// are no more, and what everyone's days go to (for who works together), in a game component of its own so it has
    /// its own save data. An old save simply starts without it, and the first day finds the groups already there quietly.
    /// Its own tick only samples work and looks for habits being kept, once an hour (Sched.Groups); the day's life of the
    /// groups runs with the social web's day (SocialWeb.Daily → <see cref="GroupLife.Daily"/>).
    /// </summary>
    public class GroupTracker : GameComponent
    {
        public List<SocialGroup> groups = new List<SocialGroup>();
        public List<GroupNewcomer> newcomers = new List<GroupNewcomer>();
        public List<PastGroup> past = new List<PastGroup>();
        public List<WorkLog> work = new List<WorkLog>();
        public int nextId = 1;
        /// <summary>The groups already there in a save have been found (quietly, once).</summary>
        public bool seeded;

        public const int MaxPast = 12;

        // Who is in which group, rebuilt only when membership changes (asked for every pair of colonists by the opinion thoughts).
        [Unsaved] private Dictionary<int, List<SocialGroup>> byPawn;
        [Unsaved] private int indexVersion = -1;
        [Unsaved] public int version;
        [Unsaved] private Dictionary<int, WorkLog> workByPawn;

        private static GroupTracker cached;
        private static Game cachedGame;

        public static GroupTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<GroupTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public GroupTracker(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            Changed();
            workByPawn = null;
        }

        /// <summary>Membership changed: the index is rebuilt on its next use.</summary>
        public void Changed() => version++;

        public List<SocialGroup> Of(Pawn p)
        {
            if (p == null) return Empty;
            if (byPawn == null || indexVersion != version)
            {
                byPawn ??= new Dictionary<int, List<SocialGroup>>();
                byPawn.Clear();
                foreach (var g in groups)
                foreach (var m in g.members)
                {
                    if (m.pawn == null) continue;
                    if (!byPawn.TryGetValue(m.pawn.thingIDNumber, out var list)) byPawn[m.pawn.thingIDNumber] = list = new List<SocialGroup>();
                    list.Add(g);
                }
                indexVersion = version;
            }
            return byPawn.TryGetValue(p.thingIDNumber, out var found) ? found : Empty;
        }

        private static readonly List<SocialGroup> Empty = new List<SocialGroup>();

        public SocialGroup Get(int id)
        {
            foreach (var g in groups)
                if (g.id == id)
                    return g;
            return null;
        }

        public GroupNewcomer NewcomerOf(Pawn p)
        {
            if (p == null) return null;
            foreach (var n in newcomers)
                if (n.pawn == p)
                    return n;
            return null;
        }

        public WorkLog WorkOf(Pawn p, bool create)
        {
            if (p == null) return null;
            if (workByPawn == null || workByPawn.Count != work.Count)
            {
                workByPawn = new Dictionary<int, WorkLog>();
                foreach (var w in work)
                    if (w.pawn != null)
                        workByPawn[w.pawn.thingIDNumber] = w;
            }
            if (workByPawn.TryGetValue(p.thingIDNumber, out var log) || !create) return log;
            log = new WorkLog { pawn = p };
            work.Add(log);
            workByPawn[p.thingIDNumber] = log;
            return log;
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (!Sched.Hourly(now, Sched.Groups) || !GroupLife.Active) return;
            try
            {
                SampleWork();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("groups: work", e);
            }
            try
            {
                GroupHabits.Hourly(this);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("groups: habits", e);
            }
        }

        /// <summary>The hour's look at what everyone is doing: their work counts towards who works together; the small hours towards the night shift.</summary>
        private void SampleWork()
        {
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                int hour = GenLocalDate.HourOfDay(map);
                bool night = hour < 5 || hour >= 23;
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                {
                    if (!GroupLife.Eligible(p) || !p.Awake()) continue;
                    var wt = p.CurJob?.workGiverDef?.workType;
                    if (wt == null && !night) continue;
                    var log = WorkOf(p, true);
                    if (wt != null && wt.defName != "Firefighter" && wt.defName != "Patient" && wt.defName != "PatientBedRest") log.Add(wt.defName);
                    if (night && (wt != null || p.CurJob?.def?.joyKind != null)) log.night += 1f;
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                try
                {
                    Prune();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("groups clean-up before saving", e);
                }
            }
            Scribe_Collections.Look(ref groups, "groups", LookMode.Deep);
            Scribe_Collections.Look(ref newcomers, "newcomers", LookMode.Deep);
            Scribe_Collections.Look(ref past, "past", LookMode.Deep);
            Scribe_Collections.Look(ref work, "work", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            Scribe_Values.Look(ref seeded, "seeded");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                groups ??= new List<SocialGroup>();
                groups.RemoveAll(g => g == null);
                newcomers ??= new List<GroupNewcomer>();
                newcomers.RemoveAll(n => n == null || n.pawn == null);
                past ??= new List<PastGroup>();
                past.RemoveAll(p => p == null);
                work ??= new List<WorkLog>();
                work.RemoveAll(w => w == null || w.pawn == null);
                if (nextId <= groups.Select(g => g.id).DefaultIfEmpty(0).Max()) nextId = groups.Max(g => g.id) + 1;
                workByPawn = null;
                Changed();
            }
        }

        /// <summary>Drops the dead and the discarded from work logs and newcomers, and those the game no longer knows from the past groups.</summary>
        public void Prune()
        {
            work.RemoveAll(w => w.pawn == null || w.pawn.Dead || w.pawn.Destroyed || !w.pawn.IsColonist);
            newcomers.RemoveAll(n => n.pawn == null || n.pawn.Dead || n.pawn.Destroyed);
            var known = CouncilGameComponent.KnownPawnNumbers();
            foreach (var p in past) p.members.RemoveAll(id => !known.Contains(id));
            past.RemoveAll(p => p.members.Count == 0);
            while (past.Count > MaxPast) past.RemoveAt(0);
            workByPawn = null;
        }
    }

    /// <summary>
    /// The groups as the rest of the mod asks about them: who is in which, whether two people share one, whether their
    /// groups are at odds, someone's role, and the tags for lines (group:ID, grouprole:joker, ingroup, outsider,
    /// groupkind:hunters, groupnorm:merry). Everything answers nothing while groups are off.
    /// </summary>
    public static class Groups
    {
        public static GroupTracker Tracker => GroupTracker.Instance;

        public static List<SocialGroup> All => GroupLife.Active ? Tracker?.groups ?? new List<SocialGroup>() : new List<SocialGroup>();

        /// <summary>The groups someone is in (none while groups are off).</summary>
        public static List<SocialGroup> Of(Pawn p) => GroupLife.Active ? Tracker?.Of(p) ?? new List<SocialGroup>() : new List<SocialGroup>();

        /// <summary>Their first group: the one they joined first.</summary>
        public static SocialGroup Primary(Pawn p)
        {
            var list = Of(p);
            SocialGroup best = null;
            int bestJoined = int.MaxValue;
            foreach (var g in list)
            {
                var m = g.Member(p);
                if (m != null && (m.joined < bestJoined || (m.joined == bestJoined && best != null && g.id < best.id)))
                {
                    best = g;
                    bestJoined = m.joined;
                }
            }
            return best;
        }

        /// <summary>A group the two share, or null.</summary>
        public static SocialGroup Shared(Pawn a, Pawn b)
        {
            if (a == null || b == null || a == b) return null;
            foreach (var g in Of(a))
                if (g.Has(b))
                    return g;
            return null;
        }

        public static bool Same(Pawn a, Pawn b) => Shared(a, b) != null;

        /// <summary>How much the groups of the two are at odds (0-100): the worst rivalry between a group of one and a group of the other they don't share.</summary>
        public static float Rivalry(Pawn a, Pawn b) => Rivalry(a, b, out _, out _);

        public static float Rivalry(Pawn a, Pawn b, out SocialGroup ga, out SocialGroup gb)
        {
            ga = gb = null;
            if (a == null || b == null || a == b) return 0f;
            var la = Of(a);
            var lb = Of(b);
            if (la.Count == 0 || lb.Count == 0) return 0f;
            float best = 0f;
            foreach (var x in la)
            {
                if (x.Has(b)) continue;
                foreach (var y in lb)
                {
                    if (y.Has(a)) continue;
                    float r = x.RivalryTo(y.id);
                    if (r > best)
                    {
                        best = r;
                        ga = x;
                        gb = y;
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// How warmly x regards y because of their groups (SocialWeb.Warmth): one of us counts for 20, the other lot
        /// against by a quarter of how much the groups are at odds. Votes, petitions and objections read it.
        /// </summary>
        public static float Warmth(Pawn x, Pawn y)
        {
            if (!GroupLife.Active || x == null || y == null || x == y) return 0f;
            if (Same(x, y)) return 20f;
            float r = Rivalry(x, y);
            return r >= GroupMath.RivalryEnds ? -r * 0.25f : 0f;
        }

        public static GroupRole RoleIn(Pawn p, SocialGroup g) => g?.RoleOf(p) ?? GroupRole.None;

        /// <summary>New to the colony and not yet taken in by a group.</summary>
        public static bool Outsider(Pawn p)
        {
            if (!GroupLife.Active || p == null) return false;
            var n = Tracker?.NewcomerOf(p);
            return n != null && n.progress < GroupMath.Accepted;
        }

        /// <summary>The tags a group gives someone, for lines (PeopleTags).</summary>
        public static void AddTags(Pawn p, HashSet<string> tags)
        {
            if (p == null || tags == null || !GroupLife.Active) return;
            try
            {
                var list = Of(p);
                if (list.Count > 0) tags.Add("ingroup");
                foreach (var g in list)
                {
                    tags.Add("group:" + g.id);
                    var role = g.RoleOf(p);
                    if (role != GroupRole.None) tags.Add("grouprole:" + GroupText.RoleKey(role));
                    if (g.kind != null && !g.kind.tag.NullOrEmpty()) tags.Add("groupkind:" + g.kind.tag);
                    foreach (var n in g.NormDefs)
                        if (!n.tag.NullOrEmpty())
                            tags.Add("groupnorm:" + n.tag);
                    if (g.rivals.Any(r => r.strength >= GroupMath.RivalryShows)) tags.Add("grouprival");
                }
                if (Outsider(p)) tags.Add("outsider");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("group tags", e);
            }
        }
    }
}
