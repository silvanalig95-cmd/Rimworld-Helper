using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Vanilla Outposts Expanded, read by type name: its outposts are world objects of the class Outposts.Outpost that
    /// keep their occupants in a list (AllPawns), off every map and out of the world pawns. Colonists posted there are
    /// still the colony's (<see cref="People.StillOurs"/>, <see cref="People.OffMapColonists"/>); here they're away: their
    /// wishes wait for them, and now and then they write home.
    /// </summary>
    public static class OutpostLife
    {
        /// <summary>A wish waits this long for someone posted away; after that it lapses, with nobody blamed.</summary>
        public const int PauseDays = 5;

        private static readonly Dictionary<Pawn, WorldObject> posted = new Dictionary<Pawn, WorldObject>();
        private static readonly Dictionary<System.Type, PropertyInfo> pawnLists = new Dictionary<System.Type, PropertyInfo>();
        private static int builtTick = -99999;
        private static Game builtGame;

        private static bool IsOutpost(WorldObject o) => o != null && HomeLife.ClassIs(o.GetType(), "Outposts", "Outpost");

        private static IEnumerable<Pawn> Occupants(WorldObject o)
        {
            var type = o.GetType();
            if (!pawnLists.TryGetValue(type, out var prop))
            {
                prop = type.GetProperty("AllPawns", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && !typeof(IEnumerable<Pawn>).IsAssignableFrom(prop.PropertyType)) prop = null;
                pawnLists[type] = prop;
            }
            if (prop == null) return Enumerable.Empty<Pawn>();
            try
            {
                return prop.GetValue(o) as IEnumerable<Pawn> ?? Enumerable.Empty<Pawn>();
            }
            catch
            {
                pawnLists[type] = null;
                return Enumerable.Empty<Pawn>();
            }
        }

        /// <summary>Who is posted where, worked out again at most every 250 ticks (this walks every world object).</summary>
        private static void Refresh()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (builtGame == Current.Game && now >= builtTick && now - builtTick < 250) return;
            builtTick = now;
            builtGame = Current.Game;
            posted.Clear();
            var objects = Find.WorldObjects?.AllWorldObjects;
            if (objects == null) return;
            foreach (var o in objects)
            {
                if (!IsOutpost(o)) continue;
                foreach (var p in Occupants(o).ToList())
                    if (p != null && p.Faction == Faction.OfPlayer) posted[p] = o;
            }
        }

        /// <summary>The outpost this colonist is posted at, or null.</summary>
        public static WorldObject PostOf(Pawn p)
        {
            if (p == null || p.Spawned || Current.ProgramState != ProgramState.Playing) return null;
            Refresh();
            return posted.TryGetValue(p, out var o) ? o : null;
        }

        public static bool AtOutpost(Pawn p) => PostOf(p) != null;

        /// <summary>"Mining" for Outpost_Mining, for tags like [outpost:Mining].</summary>
        public static string KindOf(WorldObject o)
        {
            string name = o?.def?.defName ?? "";
            return name.StartsWith("Outpost_") ? name.Substring("Outpost_".Length) : name;
        }

        /// <summary>
        /// Called each hour for a wish whose colonist is away. Posted at an outpost, the wish waits for them (its deadline
        /// moves on with the clock); after a long posting it lapses quietly, so it doesn't hold a place in the council's
        /// list for a whole season. True when it was dealt with here.
        /// </summary>
        public static bool WaitWhilePosted(CouncilGameComponent comp, Wish w, int ticks)
        {
            if (comp == null || w == null || !AtOutpost(w.Beneficiary)) return false;
            if (comp.AwayTicks(w.Beneficiary) > GenDate.TicksPerDay * PauseDays) comp.Resolve(w, WishState.Cancelled);
            else w.expiresTick += ticks;
            return true;
        }

        /// <summary>
        /// Once a day: now and then, someone posted at an outpost writes a line home in their journal (pool
        /// Council_Journal_Outpost, tagged with the kind of outpost, e.g. [outpost:Logging]).
        /// </summary>
        public static void Letters(JournalTracker journals, Dictionary<string, int> seen)
        {
            if (journals == null || seen == null || !ColonyCouncilMod.Settings.journals) return;
            if (DefDatabase<FlavorPoolDef>.GetNamedSilentFail("Council_Journal_Outpost") == null) return;
            try
            {
                Refresh();
                if (posted.Count == 0) return;
                int now = Find.TickManager.TicksGame;
                foreach (var kv in posted.ToList())
                {
                    var p = kv.Key;
                    if (!People.StillOurs(p) || !People.HasInnerLife(p) || p.DevelopmentalStage.Baby()) continue;
                    string key = p.ThingID + "|Outpost";
                    if (seen.TryGetValue(key, out int when) && now - when < GenDate.TicksPerDay * 4) continue;
                    if (!Rand.Chance(0.3f)) continue;
                    seen[key] = now;
                    journals.WriteTagged(p, "Council_Journal_Outpost", new[] { "outpost:" + KindOf(kv.Value) });
                }
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("outpost letters", e);
            }
        }
    }
}
