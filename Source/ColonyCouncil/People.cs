using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Who counts as a person, and how keen they are on a skill, kept in one place so the odd pawns and passions
    /// other mods add are handled the same way everywhere.
    /// </summary>
    public static class People
    {
        /// <summary>
        /// Someone with an inner life: humanlike, with moods, and not a ghoul or other mutant. Keeps out raised undead,
        /// feelingless constructs and anything else that only looks like a person.
        /// </summary>
        public static bool HasInnerLife(Pawn p) =>
            p != null && !p.Dead && p.RaceProps != null && p.RaceProps.Humanlike && !p.IsMutant && !IsRaisedDead(p)
            && (p.needs?.mood != null || !MoodTracked(p));

        /// <summary>
        /// The game only keeps a mood for the colony's own people and those it holds (prisoners, guests it hosts).
        /// Visitors, traders and raiders have none, so for them a missing mood says nothing about who they are.
        /// </summary>
        private static bool MoodTracked(Pawn p)
        {
            var player = Faction.OfPlayerSilentFail;
            return player != null && (p.Faction == player || p.HostFaction == player);
        }

        private static bool deadLooked;
        private static TraitDef undeadTrait;
        private static HediffDef undeadHediff;

        /// <summary>The walking dead of magic mods (A RimWorld of Magic's raised undead keep a mood need, pinned at full).</summary>
        public static bool IsRaisedDead(Pawn p)
        {
            if (!deadLooked)
            {
                deadLooked = true;
                undeadTrait = DefDatabase<TraitDef>.GetNamedSilentFail("Undead");
                undeadHediff = DefDatabase<HediffDef>.GetNamedSilentFail("TM_UndeadHD");
            }
            if (undeadTrait != null && (p.story?.traits?.HasTrait(undeadTrait) ?? false)) return true;
            return undeadHediff != null && p.health?.hediffSet?.GetFirstHediffOfDef(undeadHediff) != null;
        }

        /// <summary>
        /// Mechanoids are part of this game: they exist and are hostile. Rimedieval keeps the faction but turns it
        /// friendly and destroys any that appear; other mods remove it outright.
        /// </summary>
        public static bool MechanoidsAround =>
            Faction.OfMechanoids != null && !Faction.OfMechanoids.defeated && Faction.OfMechanoids.HostileTo(Faction.OfPlayer);

        private static readonly Dictionary<System.Type, System.Reflection.PropertyInfo> pawnLists = new Dictionary<System.Type, System.Reflection.PropertyInfo>();

        /// <summary>
        /// Colonists held somewhere the game's pawn lists don't look: Vanilla Outposts keeps its occupants inside the
        /// outpost (an "AllPawns" list), and Vehicle Framework its passengers in flight ("AllPawnsAboard"), not as
        /// world pawns.
        /// </summary>
        public static IEnumerable<Pawn> OffMapColonists()
        {
            var objects = Find.WorldObjects?.AllWorldObjects;
            if (objects == null) yield break;
            foreach (var o in objects)
            {
                if (o == null) continue;
                var type = o.GetType();
                if (!pawnLists.TryGetValue(type, out var prop))
                {
                    // Vanilla Outposts: AllPawns. Vehicle Framework's vehicles in flight: AllPawnsAboard.
                    prop = null;
                    foreach (var name in new[] { "AllPawns", "AllPawnsAboard" })
                    {
                        var candidate = type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (candidate != null && typeof(IEnumerable<Pawn>).IsAssignableFrom(candidate.PropertyType))
                        {
                            prop = candidate;
                            break;
                        }
                    }
                    pawnLists[type] = prop;
                }
                if (prop == null) continue;
                IEnumerable<Pawn> list = null;
                try
                {
                    list = prop.GetValue(o) as IEnumerable<Pawn>;
                }
                catch
                {
                    pawnLists[type] = null;
                }
                if (list == null) continue;
                foreach (var p in list)
                    if (p != null && p.Faction == Faction.OfPlayer) yield return p;
            }
        }

        /// <summary>
        /// Still one of the colony even when on no map: at an outpost, aboard a ship in flight, in a vehicle. False once
        /// they're dead, kidnapped, arrested, or have left.
        /// </summary>
        public static bool StillOurs(Pawn p) =>
            p != null && !p.Dead && !p.Discarded && p.Faction == Faction.OfPlayer && !p.IsPrisoner && p.RaceProps.Humanlike && !Kidnapped(p);

        private static HashSet<string> oursIds;
        private static float oursTime = -999f;
        private static int oursTick = -99999;

        /// <summary>
        /// ThingIDs of everyone still ours (see <see cref="StillOurs"/>), for the UI. Refreshed as game time passes
        /// (every few seconds of play), and only now and then while paused, since this walks every pawn in the world.
        /// </summary>
        public static HashSet<string> OursIds()
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            int tick = Find.TickManager?.TicksGame ?? 0;
            bool stale = oursIds == null || tick < oursTick || (tick - oursTick >= 250 && now - oursTime > 2f) || now - oursTime > 15f;
            if (stale)
            {
                oursIds = new HashSet<string>(PawnsFinder.AllMapsWorldAndTemporary_Alive.Concat(OffMapColonists()).Where(StillOurs).Select(p => p.ThingID));
                oursTime = now;
                oursTick = tick;
            }
            return oursIds;
        }

        private static readonly Dictionary<string, ModMetaData> activeMods = new Dictionary<string, ModMetaData>();

        /// <summary>
        /// The active mod with this package id, or null. Ignores letter case and the "_steam" / "_copy" endings the
        /// game adds to local copies, so ids can be written in lowercase anywhere in Rimfolk's XML.
        /// </summary>
        public static ModMetaData ActiveMod(string packageId)
        {
            if (packageId.NullOrEmpty()) return null;
            string id = packageId.Trim().ToLowerInvariant();
            if (activeMods.TryGetValue(id, out var found)) return found;
            found = ModLister.GetActiveModWithIdentifier(id, true);
            if (found == null)
            {
                foreach (var m in ModsConfig.ActiveModsInLoadOrder)
                {
                    string mine = (m.PackageIdPlayerFacing ?? m.PackageId ?? "").ToLowerInvariant();
                    if (mine == id || mine == id + "_steam" || mine == id + "_copy")
                    {
                        found = m;
                        break;
                    }
                }
            }
            activeMods[id] = found;
            return found;
        }

        public static bool ModActive(string packageId) => ActiveMod(packageId) != null;

        public static bool Kidnapped(Pawn p) =>
            p != null && Find.FactionManager.AllFactionsListForReading.Any(f => f.kidnapped != null && f.kidnapped.KidnappedPawnsListForReading.Contains(p));

        private static readonly Dictionary<int, int> modPassions = new Dictionary<int, int>();

        /// <summary>
        /// 0 = no passion (or a bad one), 1 = interested, 2 = burning. Passions added by mods (Vanilla Skills Expanded
        /// and the like) are judged by how fast they make the pawn learn.
        /// </summary>
        public static int PassionLevel(SkillRecord s)
        {
            if (s == null || s.TotallyDisabled) return 0;
            switch (s.passion)
            {
                case Passion.None: return 0;
                case Passion.Minor: return 1;
                case Passion.Major: return 2;
            }
            int key = (int)s.passion;
            if (modPassions.TryGetValue(key, out int level)) return level;
            // Vanilla Skills Expanded (and Alpha Skills, which builds on it) defines each passion: read how fast it
            // makes one learn, and whether it's a bad one. A colonist's own learning speed won't do, because VSE also
            // slows it for passions in their other skills.
            int? fromDef = VsePassionLevel(key);
            if (fromDef.HasValue)
            {
                modPassions[key] = fromDef.Value;
                return fromDef.Value;
            }
            level = 1;
            try
            {
                float f = s.LearnRateFactor(true);
                level = f < 0.9f ? 0 : f >= 1.25f ? 2 : 1;
            }
            catch
            {
                // A passion the game can't price: call it an interest.
            }
            // Judged from one colonist's learning speed, so not remembered: another may learn differently.
            return level;
        }

        private static bool vseLooked;
        private static System.Reflection.FieldInfo vsePassions;

        private static int? VsePassionLevel(int passion)
        {
            try
            {
                if (!vseLooked)
                {
                    vseLooked = true;
                    var type = GenTypes.GetTypeInAnyAssembly("VSE.Passions.PassionManager");
                    vsePassions = type?.GetField("Passions", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                }
                if (!(vsePassions?.GetValue(null) is System.Array defs) || passion < 0 || passion >= defs.Length) return null;
                var def = defs.GetValue(passion);
                if (def == null) return null;
                var t = def.GetType();
                bool bad = t.GetField("isBad")?.GetValue(def) is bool b && b;
                float rate = t.GetField("learnRateFactor")?.GetValue(def) is float f ? f : 1f;
                if (bad || rate < 0.9f) return 0;
                return rate >= 1.25f ? 2 : 1;
            }
            catch
            {
                return null;
            }
        }

        public static bool Passionate(SkillRecord s) => PassionLevel(s) > 0;

        public static bool Burning(SkillRecord s) => PassionLevel(s) >= 2;
    }
}
