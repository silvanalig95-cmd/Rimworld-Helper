using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Now and then a colonist on screen says a short line about how they really are, from what Rimfolk knows of them: a
    /// feeling that's running strong, an ailment, getting back on their feet, the stage their personal story has reached,
    /// a calendar day coming tomorrow, a setback they've just had, a promise the council took back, a nemesis still out
    /// there, or a wish still waiting. Lines come from the <c>Council_Pool_Say...</c> pools in the speaker's own voice
    /// (<see cref="Tone"/>), tagged for the particular thing (<c>[feeling:Grief]</c>, <c>[ail:StrainedBack]</c>,
    /// <c>[story:LongGrief,stage:1]</c>, <c>[day:birthday,mine]</c>, <c>[setback:CropLost]</c>...).
    /// </summary>
    /// <remarks>
    /// Run from <see cref="VisualTouches"/>' hourly look, when nothing else was said that hour: one line at most an hour,
    /// for a colonist on the map being looked at and in view, and the same colonist at most once a day (the rate every
    /// ambient line shares). Looking at a colonist reads what's already worked out (their heart's feelings, their
    /// memories, the council's lists); nothing is computed for it, and nothing is saved.
    /// </remarks>
    internal static class StateSpeech
    {
        private static readonly Color SpeechColor = new Color(0.9f, 0.92f, 0.88f);

        /// <summary>How likely an hour is to bring a line at all, when someone in view has something to say.</summary>
        private const float HourChance = 0.6f;
        /// <summary>At most this many colonists are looked at in one hour.</summary>
        private const int MaxLooked = 5;
        /// <summary>A setback counts as "just suffered" for this long.</summary>
        private const int FreshSetbackTicks = GenDate.TicksPerDay / 2;
        /// <summary>A promise taken back stays on their mind (out loud) for this long.</summary>
        private const int TakenBackTicks = GenDate.TicksPerDay * 3;
        /// <summary>A feeling has to be at least this strong (its share of full intensity) to be said out loud.</summary>
        private const float StrongFeeling = 0.6f;

        private sealed class Said
        {
            public string pool;
            public float weight;
            public string[] tags;
            public List<NamedArgument> args = new List<NamedArgument>();
            public Wish wish;
            public Arc arc;
        }

        private static readonly List<Said> options = new List<Said>(8);

        /// <summary>Tomorrow's day on the calendar, worked out once per look (and let go of after it).</summary>
        private static CalendarDay tomorrow;
        private static bool tomorrowKnown;

        public static bool TrySay(VisualTouches vt, Map map, CellRect view, int now)
        {
            if (vt == null || map == null || !Rand.Chance(HourChance)) return false;
            var pawns = map.mapPawns.FreeColonistsSpawned;
            int count = pawns.Count;
            if (count == 0) return false;
            int start = Rand.Range(0, count);
            int looked = 0;
            tomorrowKnown = false;
            try
            {
                for (int i = 0; i < count && looked < MaxLooked; i++)
                {
                    var p = pawns[(start + i) % count];
                    if (!vt.Free(p, map, view, now)) continue;
                    looked++;
                    options.Clear();
                    Gather(p, map);
                    if (options.Count == 0) continue;
                    var s = options.RandomElementByWeight(o => o.weight);
                    string line = LineFor(p, s);
                    options.Clear();
                    if (line.NullOrEmpty()) continue;
                    CouncilGameComponent.Speak(p, line, SpeechColor);
                    vt.Spoke(p, now + GenDate.TicksPerDay);
                    return true;
                }
                return false;
            }
            finally
            {
                options.Clear();
                tomorrow = null;
                tomorrowKnown = false;
            }
        }

        // ---------------- What's on their mind ----------------

        private static void Gather(Pawn p, Map map)
        {
            int now = Find.TickManager.TicksGame;
            var comp = CouncilGameComponent.Instance;
            var s = ColonyCouncilMod.Settings;
            Try(() => Feeling(p), "feeling");
            Try(() => Ailment(p), "ailment");
            Try(() => Convalescing(p), "convalescence");
            Try(() => Story(p), "story");
            Try(() => Tomorrow(p, map), "calendar");
            Try(() => Setback(p), "setback");
            if (comp != null)
            {
                Try(() => TakenBack(p, comp, now), "promise");
                if (s != null && s.nemeses) Try(() => Nemesis(p, comp), "nemesis");
                if (s != null && s.enableWishes) Try(() => WishWaiting(p, comp), "wish");
            }
        }

        private static void Try(Action a, string what)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("state speech: " + what, e);
            }
        }

        private static Said Add(string pool, float weight, params string[] tags)
        {
            var s = new Said { pool = pool, weight = weight, tags = tags };
            options.Add(s);
            return s;
        }

        /// <summary>The strongest feeling, if it's strong: [feeling:Grief], bright or hard, and [about] when it has someone in it.</summary>
        private static void Feeling(Pawn p)
        {
            var h = Heart.Of(p);
            if (h?.feelings == null || h.feelings.Count == 0) return;
            var f = h.feelings[0];
            if (f?.def == null || f.def.key.NullOrEmpty()) return;
            float share = HeartMath.Share(f.intensity);
            if (share < StrongFeeling || !HasLines("Council_Pool_SayFeeling", "feeling:" + f.def.key)) return;
            bool about = f.target != null && f.target != p && !f.target.Destroyed;
            var said = Add("Council_Pool_SayFeeling", 0.5f + share, "feeling:" + f.def.key, f.def.bright ? "bright" : "hard", about ? "about" : null);
            said.args.Add((about ? f.target : p).Named("OTHER"));
        }

        /// <summary>An everyday ailment or an illness they have now: [ail:Key], serious or everyday. Not while they're convalescing (that has its own).</summary>
        private static void Ailment(Pawn p)
        {
            if (!AilmentTracker.On) return;
            var hediffs = p.health?.hediffSet?.hediffs;
            if (hediffs == null) return;
            foreach (var h in hediffs)
            {
                if (h == null || !h.Visible) continue;
                var info = AilmentTracker.InfoOf(h.def);
                if (info == null || info.key.NullOrEmpty() || info.key == "Convalescent") continue;
                var said = Add("Council_Pool_SayAilment", info.serious ? 1.5f : 1.2f, "ail:" + info.key, info.serious ? "serious" : "everyday");
                said.args.Add((h.def.label ?? "").Named("ILLNESS"));
                return;
            }
        }

        /// <summary>Getting back on their feet after an illness, an operation, a long stay in bed or a lost limb: [walking] on a slow walk.</summary>
        private static void Convalescing(Pawn p)
        {
            if (!HealthTracker.ConvalescenceOn || HealthTracker.ConvalescenceOf(p) == null) return;
            var job = p.CurJobDef;
            bool walking = job != null && (job == SmallHabits.SlowWalkDef || job.defName == "GoForWalk");
            Add("Council_Pool_SayConvalescing", 1f, walking ? "walking" : null);
        }

        /// <summary>Their personal story: [story:LongGrief] (the def's name without Council_Arc_) and [stage:0] to [stage:3].</summary>
        private static void Story(Pawn p)
        {
            if (!ArcTracker.On) return;
            var arc = ArcTracker.Instance?.Of(p);
            if (arc?.def == null || arc.Stage == null || arc.lost) return;
            var said = Add("Council_Pool_SayStory", 0.9f, "story:" + Short(arc.def.defName), "stage:" + arc.stage);
            said.arc = arc;
        }

        /// <summary>
        /// A day on the calendar tomorrow that's theirs to mind: [day:birthday] (its kind), [way:feast] (how it's kept), and
        /// [mine] when it's their own birthday, arrival or wedding, [knew] when it remembers someone they knew.
        /// </summary>
        private static void Tomorrow(Pawn p, Map map)
        {
            if (!CalendarTracker.On || !map.IsPlayerHome) return;
            var d = TomorrowDay(map);
            if (d == null) return;
            bool mine = d.pawnId == p.ThingID || d.otherId == p.ThingID;
            bool knew = d.knew != null && d.knew.Contains(p.ThingID);
            Pawn whose = null;
            switch (d.kind)
            {
                case CalendarKind.Birthday:
                case CalendarKind.Arrival:
                    // Their own day, or a friend's.
                    if (!mine && (whose = Fond(p, map, d.pawnId)) == null) return;
                    break;
                case CalendarKind.Death:
                    if (!knew) return;
                    break;
                case CalendarKind.Wedding:
                    if (!mine && (whose = Fond(p, map, d.pawnId) ?? Fond(p, map, d.otherId)) == null) return;
                    break;
            }
            var said = Add("Council_Pool_SayTomorrow", 1f, "day:" + d.kind.ToString().ToLowerInvariant(), "way:" + d.way.ToString().ToLowerInvariant(),
                mine ? "mine" : null, knew ? "knew" : null);
            said.args.Add((d.label ?? "").Named("NAME"));
            said.args.Add((d.kind == CalendarKind.Death ? d.pawnName ?? "" : "").Named("DEAD"));
            said.args.Add((whose ?? p).Named("OTHER"));
        }

        private static CalendarDay TomorrowDay(Map map)
        {
            if (tomorrowKnown) return tomorrow;
            tomorrowKnown = true;
            tomorrow = null;
            var cal = CalendarTracker.Instance;
            if (cal?.days == null) return null;
            int today = GenLocalDate.DayOfYear(map);
            int year = GenLocalDate.Year(map);
            int tomorrowYear = today + 1 >= CalendarMath.DaysPerYear ? year + 1 : year;
            CalendarDay best = null;
            foreach (var d in cal.days)
            {
                if (d == null || d.kind == CalendarKind.FirstSnow || CalendarMath.DaysUntil(today, d.day) != 1 || d.lastYear >= tomorrowYear) continue;
                if (!d.byPlayer && tomorrowYear <= d.sinceYear) continue;
                if (best == null || d.Standing > best.Standing) best = d;
            }
            tomorrow = best;
            return best;
        }

        /// <summary>The one whose day it is (found on this map by id), if they think well of them; else null.</summary>
        private static Pawn Fond(Pawn p, Map map, string id)
        {
            if (id.NullOrEmpty() || p.relations == null) return null;
            foreach (var o in map.mapPawns.FreeColonistsSpawned)
                if (o.ThingID == id) return o != p && p.relations.OpinionOf(o) >= 20 ? o : null;
            return null;
        }

        /// <summary>A setback in the last half day: [setback:CropLost] (the thought's name without its prefix), [about] with someone in it.</summary>
        private static void Setback(Pawn p)
        {
            var mem = p.needs?.mood?.thoughts?.memories?.Memories;
            if (mem == null) return;
            Thought_Memory freshest = null;
            foreach (var m in mem)
            {
                if (m?.def == null || m.age > FreshSetbackTicks || (freshest != null && m.age >= freshest.age) || !IsSetback(m.def)) continue;
                freshest = m;
            }
            if (freshest == null) return;
            bool about = freshest.otherPawn != null && freshest.otherPawn != p;
            var said = Add("Council_Pool_SaySetback", 1.3f, "setback:" + Short(freshest.def.defName), about ? "about" : null);
            said.args.Add((about ? freshest.otherPawn : p).Named("OTHER"));
        }

        /// <summary>
        /// One of Rimfolk's setbacks; or a friction or a small nuisance (a cooking run down, a soaking without a coat) that
        /// has lines of its own: the many small frictions between people without any are left to the social log.
        /// </summary>
        private static bool IsSetback(ThoughtDef def)
        {
            if (Setbacks.Ext(def) != null) return true;
            string n = def.defName;
            return (n.StartsWith("Council_Friction_") || n.StartsWith("Council_Nuisance_")) && HasLines("Council_Pool_SaySetback", "setback:" + Short(n));
        }

        /// <summary>Something the council granted them and then took back, in the last three days: [kept:ownroom], bed, pet... ({WISH} is what it was).</summary>
        private static void TakenBack(Pawn p, CouncilGameComponent comp, int now)
        {
            if (!KeptTracker.On || comp.kept?.watches == null) return;
            foreach (var w in comp.kept.watches)
            {
                if (w == null || w.pawn != p || w.phase != KeptPhase.TakenBack || w.takenBackTick < 0 || now - w.takenBackTick > TakenBackTicks) continue;
                var said = Add("Council_Pool_SayTakenBack", 1.4f, "kept:" + w.Kind);
                said.args.Add((w.label ?? "").Named("WISH"));
                return;
            }
        }

        /// <summary>A nemesis who hurt them and is still out there: [beast] for an animal, [deed:maimed], [sworn].</summary>
        private static void Nemesis(Pawn p, CouncilGameComponent comp)
        {
            if (comp.nemeses == null) return;
            foreach (var n in comp.nemeses.Of(p))
            {
                if (n == null || n.ended || n.captured) continue;
                var said = Add("Council_Pool_SayNemesis", 0.7f, n.IsBeast ? "beast" : null, "deed:" + (n.deed ?? "downed"), n.Sworn ? "sworn" : null);
                said.args.Add(n.Name.Named("ENEMY"));
                return;
            }
        }

        /// <summary>A wish of theirs the council hasn't answered yet (its own tags: promised, petition, forother).</summary>
        private static void WishWaiting(Pawn p, CouncilGameComponent comp)
        {
            foreach (var w in comp.wishes)
            {
                if (w == null || w.pawn != p || w.state != WishState.Open || w.def == null) continue;
                Add("Council_Pool_SayWish", 0.8f).wish = w;
                return;
            }
        }

        // ---------------- The line ----------------

        /// <summary>Whether a pool has lines tagged for this (a feeling another mod added may have none): looked up once each.</summary>
        private static readonly Dictionary<string, bool> hasLines = new Dictionary<string, bool>();

        private static bool HasLines(string pool, string tag)
        {
            string key = pool + "|" + tag;
            if (hasLines.TryGetValue(key, out bool has)) return has;
            var def = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(pool);
            has = def?.lines != null && def.lines.AllLines().Any(l => Flavor.Strip(l, out var tags) != null && tags != null && tags.Any(t => t.Split('|').Contains(tag)));
            hasLines[key] = has;
            return has;
        }

        private static string LineFor(Pawn p, Said s)
        {
            var pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(s.pool);
            if (pool?.lines == null) return null;
            try
            {
                var extra = s.tags.Where(t => !t.NullOrEmpty()).ToArray();
                var tags = s.arc != null ? ArcText.Tags(p, s.arc, s.arc.other, extra) : Flavor.TagsFor(p, s.wish);
                foreach (var t in extra) tags.Add(t);
                string raw = pool.lines.Pick(ToneUtility.ToneFor(p), tags);
                if (raw.NullOrEmpty()) return null;
                if (s.wish != null) return s.wish.Format(raw);
                if (s.arc != null) return raw.Formatted(ArcText.Args(s.arc, p, s.arc.other)).Resolve();
                var args = new List<NamedArgument> { p.Named("PAWN") };
                args.AddRange(s.args);
                return raw.Formatted(args.ToArray()).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("state speech line " + s.pool, e);
                return null;
            }
        }

        /// <summary>A def's name without Rimfolk's prefix: Council_Setback_CropLost -> CropLost.</summary>
        internal static string Short(string defName)
        {
            if (defName.NullOrEmpty()) return defName;
            foreach (var prefix in Prefixes)
                if (defName.StartsWith(prefix)) return defName.Substring(prefix.Length);
            return defName;
        }

        private static readonly string[] Prefixes = { "Council_Setback_", "Council_Friction_", "Council_Nuisance_", "Council_Arc_" };
    }
}
