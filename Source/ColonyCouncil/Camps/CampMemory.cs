using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A camp the colony remembers: its kind and people, how the stay went, and whether they were asked back. Kept so
    /// the same faces can come back a season later (see <see cref="CampReturns"/>). Their people are kept in the
    /// world between visits (never let go by the game's clean-up) until the memory fades.
    /// </summary>
    public class PastCamp : IExposable
    {
        public int id;
        public CampKindDef kind;
        public Faction faction;
        /// <summary>Those kept to come back: whoever spoke for them first, then their trader, then the ones the colony liked.</summary>
        public List<Pawn> people = new List<Pawn>();
        /// <summary>The colonist warmest with them, who'll be glad to see them.</summary>
        public Pawn friendOf;
        public string place;
        public int leftTick;
        public string ending;
        public int helps, gifts, tradeDays, returns;
        public bool invited, provisioned;

        private static int Now => Find.TickManager.TicksGame;

        public float DaysSince => (Now - leftTick) / (float)GenDate.TicksPerDay;

        /// <summary>Someone of theirs is still out there to come back, and their people still a people at peace with the colony.</summary>
        public bool Alive =>
            kind != null && faction != null && !faction.defeated && !faction.Hidden && !faction.HostileTo(Faction.OfPlayer)
            && people.Any(p => p != null && !p.Dead && !p.Discarded && p.Faction == faction);

        public List<Pawn> Living => people.Where(p => p != null && !p.Dead && !p.Discarded && !p.Spawned && p.Faction == faction).ToList();

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                people.RemoveAll(p => p == null || p.Discarded);
                if (friendOf != null && (friendOf.Discarded || friendOf.Dead)) friendOf = null;
            }
            Scribe_Values.Look(ref id, "id");
            CouncilScribe.LookDef(ref kind, "kind");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Collections.Look(ref people, "people", LookMode.Reference);
            Scribe_References.Look(ref friendOf, "friendOf");
            Scribe_Values.Look(ref place, "place");
            Scribe_Values.Look(ref leftTick, "leftTick");
            Scribe_Values.Look(ref ending, "ending");
            Scribe_Values.Look(ref helps, "helps");
            Scribe_Values.Look(ref gifts, "gifts");
            Scribe_Values.Look(ref tradeDays, "tradeDays");
            Scribe_Values.Look(ref returns, "returns");
            Scribe_Values.Look(ref invited, "invited");
            Scribe_Values.Look(ref provisioned, "provisioned");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                people ??= new List<Pawn>();
                people.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// Camps that come back. A camp that stayed and left in peace is remembered (CampStoryMath.Remembered) with up to
    /// four of its people kept in the world; when a camp could come again a month or more later, it may be that one
    /// coming back (CampStoryMath.ReturnChance: more after a warm stay or an invitation, less each time), with the same
    /// faces, news of what became of them since, an easier stay, and a glad colonist who made a friend of them. The
    /// colony keeps six such memories at most; they fade after some seasons, or when the people fall out with the colony.
    /// </summary>
    public static class CampReturns
    {
        private static int Now => Find.TickManager.TicksGame;

        public static List<PastCamp> Past => CampTracker.Instance?.past ?? new List<PastCamp>();

        /// <summary>A camp that ended: remembered if it went well enough (the one it was a return of is brought up to date instead).</summary>
        public static void Remember(Camp c)
        {
            var tracker = CampTracker.Instance;
            if (tracker == null || c?.kind == null || c.faction == null) return;
            var past = c.returnOf > 0 ? tracker.past.FirstOrDefault(p => p.id == c.returnOf) : null;
            // Those held while they walked off (Keep) are let go unless kept below.
            var held = c.members.Where(p => p != null && !p.Discarded).ToList();
            void Release(IEnumerable<Pawn> ps)
            {
                foreach (var p in ps.Where(p => p != null && !p.Spawned && (past == null || !past.people.Contains(p)))) Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            }
            if (c.kind.fights || !FactionMemory.Remembers(c.faction) || !CampStoryMath.Remembered(c.ending, c.arrivedTick >= 0, c.tension))
            {
                // A war party goes home to its own wars; a stay that went badly ends the acquaintance.
                Release(held);
                if (past != null) Forget(past);
                return;
            }
            var candidates = c.members.Where(p => p != null && !p.Dead && !p.Discarded && p.Faction == c.faction && !c.joined.Contains(p) && !c.arrested.Contains(p)
                                                && !p.DevelopmentalStage.Juvenile()).ToList();
            if (candidates.Count == 0)
            {
                Release(held);
                if (past != null) Forget(past);
                return;
            }
            var colonists = c.map != null ? IncidentWorker_Happening.Colonists(c.map) : new List<Pawn>();
            var liking = candidates.Select(m => colonists.Sum(col => col.relations != null ? Mathf.Max(0, col.relations.OpinionOf(m)) : 0) / 10f).ToList();
            var kept = CampStoryMath.Kept(candidates.Count, candidates.IndexOf(c.leader), candidates.IndexOf(c.trader), liking).Select(i => candidates[i]).ToList();
            if (past == null)
            {
                past = new PastCamp { id = tracker.nextId++, kind = c.kind, faction = c.faction };
                tracker.past.Add(past);
            }
            else
            {
                foreach (var p in past.people.Where(p => p != null && !kept.Contains(p) && !p.Spawned)) Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            }
            past.people = kept;
            foreach (var p in kept) Find.WorldPawns.ForcefullyKeptPawns.Add(p);
            Release(held.Where(p => !kept.Contains(p)));
            past.place = c.place;
            past.leftTick = Now;
            past.ending = c.ending;
            past.helps = c.helps;
            past.gifts = c.gifts;
            past.tradeDays = c.tradeDays;
            past.returns = c.returns;
            past.invited = c.invited;
            past.provisioned = c.provisioned;
            past.friendOf = c.warmth.Where(kv => kv.Key != null && !kv.Key.Dead && kv.Key.IsColonist).OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault();
            // Six at most: the oldest makes way.
            while (tracker.past.Count > CampStoryMath.RememberMax) Forget(tracker.past.OrderBy(p => p.leftTick).First());
        }

        /// <summary>
        /// They're setting off: those who might be remembered are held in the world from now, so the game's clean-up
        /// can't let them go in the hour between their leaving the map and the books closing (Remember).
        /// </summary>
        public static void Keep(Camp c)
        {
            if (c?.kind == null || c.kind.fights || c.faction == null || !CampStoryMath.Remembered(c.ending, c.arrivedTick >= 0, c.tension)) return;
            foreach (var p in c.members.Where(p => p != null && !p.Dead && !p.Discarded && p.Faction == c.faction && !c.joined.Contains(p)))
                Find.WorldPawns.ForcefullyKeptPawns.Add(p);
        }

        /// <summary>The memory fades: their people go back to the world's own keeping.</summary>
        public static void Forget(PastCamp past)
        {
            var tracker = CampTracker.Instance;
            if (past == null || tracker == null) return;
            foreach (var p in past.people.Where(p => p != null && !p.Spawned)) Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            tracker.past.Remove(past);
        }

        /// <summary>Once a day: the faded, the dead and those fallen out with the colony are forgotten.</summary>
        public static void Daily()
        {
            var tracker = CampTracker.Instance;
            if (tracker == null || tracker.past.Count == 0) return;
            foreach (var p in tracker.past.ToList())
                if (!p.Alive || p.DaysSince > CampStoryMath.ForgetAfterDays) Forget(p);
        }

        /// <summary>A remembered camp that could come back now and rolls to: the likeliest first.</summary>
        public static PastCamp Pick(Map map)
        {
            var tracker = CampTracker.Instance;
            if (tracker == null || tracker.past.Count == 0) return null;
            foreach (var p in tracker.past.OrderByDescending(p => p.helps + p.gifts + (p.invited ? 3 : 0)).ThenBy(_ => Rand.Value))
            {
                if (!p.Alive || p.Living.Count == 0 || !p.kind.FitsEra() || !p.kind.Possible || !p.kind.FitsFaction(p.faction)) continue;
                if (Rand.Chance(CampStoryMath.ReturnChance(p.DaysSince, p.helps + p.gifts, p.tradeDays, p.invited, p.returns))) return p;
            }
            return null;
        }

        /// <summary>Their camp is here again: an easier start, a glad friend, and the memory marked as back in use.</summary>
        public static void Started(Camp c, PastCamp past)
        {
            if (c == null || past == null) return;
            c.returnOf = past.id;
            c.returns = past.returns + 1;
            past.returns = c.returns;
            c.tension = 0f;
            var friend = past.friendOf;
            if (friend != null && !friend.Dead && friend.Spawned && friend.Map == c.map && friend.IsColonist)
            {
                // apart: the news of their coming reaches the colonist wherever they are; the visit itself is the camp's visitors
                // once the camp stands (CampLife.Visitors), as with any camp.
                c.Warm(friend, 3);
                var them = c.members.FirstOrDefault(m => past.people.Contains(m)) ?? c.leader;
                CouncilGameComponent.GiveThought(friend, CampDefOf.Council_Camp_FriendBack, them);
                CouncilGameComponent.Instance?.journals.WriteTagged(friend, "Council_Journal_Camp", new[] { "back" }, null,
                    (them != null ? them.Named("X") : "?".Named("X")), (c.faction?.Name ?? "").Named("FACTION"), c.Label.Named("NAME"), (c.place ?? "").Named("PLACE"));
            }
        }

        /// <summary>What became of them since they left, for the letter that brings them back: a line that fits their kind.</summary>
        public static string News(Camp c, PastCamp past)
        {
            if (c?.kind == null) return "";
            var tags = new HashSet<string>();
            Flavor.AddEraTags(tags);
            CampWords.AddTags(tags, c.kind, c.faction, c.map);
            var k = c.kind;
            if (k.trader) tags.Add("trader");
            if (k.help != null && k.help.Contains("heal")) tags.Add("healers");
            if (k.family) tags.Add("family");
            if (k.huntsPerDay > 0f) tags.Add("hunters");
            if (!k.memes.NullOrEmpty() || (k.task != null && k.task.defName.IndexOf("Pray", StringComparison.OrdinalIgnoreCase) >= 0)) tags.Add("faithful");
            if (k.defName.IndexOf("Players", StringComparison.OrdinalIgnoreCase) >= 0 || k.defName.IndexOf("Minstrels", StringComparison.OrdinalIgnoreCase) >= 0) tags.Add("players");
            if (k.fights) tags.Add("fighters");
            if (past != null && past.provisioned) tags.Add("provisioned");
            if (past != null && past.invited) tags.Add("invited");
            if (past != null && past.returns >= 2) tags.Add("again");
            var lines = new List<string>();
            for (int i = 1; i <= 16; i++)
            {
                string key = "CC_Camp_News_" + i;
                if (key.CanTranslate()) lines.Add(key.Translate().RawText);
            }
            string line = Flavor.Pick(lines, tags) ?? Flavor.PickText(lines);
            return c.Format(line);
        }

        /// <summary>For the World page: the camps the colony remembers, in a line ("the drovers of the Hill Tribes, gone 23 days").</summary>
        public static string KnownLine()
        {
            var past = Past.Where(p => p.Alive).OrderBy(p => p.leftTick).ToList();
            if (past.Count == 0) return null;
            var bits = past.Select(p => "CC_Camp_KnownOne".Translate(p.kind.label.Named("NAME"), (p.faction?.Name ?? "").Named("FACTION"), TextUtil.DaysString(Now - p.leftTick).Named("AGO")).Resolve()).ToList();
            return "CC_Camp_Known".Translate(TextUtil.JoinAnd(bits)).Resolve();
        }
    }
}
