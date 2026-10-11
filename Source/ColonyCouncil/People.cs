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
            p != null && !p.Dead && p.RaceProps != null && p.RaceProps.Humanlike && p.needs?.mood != null && !p.IsMutant;

        /// <summary>
        /// Still one of the colony even when on no map: at an outpost, aboard a ship in flight, in a vehicle. False once
        /// they're dead, kidnapped, arrested, or have left.
        /// </summary>
        public static bool StillOurs(Pawn p) =>
            p != null && !p.Dead && !p.Discarded && p.Faction == Faction.OfPlayer && !p.IsPrisoner && p.RaceProps.Humanlike && !Kidnapped(p);

        private static HashSet<string> oursIds;
        private static float oursTime = -999f;

        /// <summary>ThingIDs of everyone still ours (see <see cref="StillOurs"/>), refreshed every couple of seconds for the UI.</summary>
        public static HashSet<string> OursIds()
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (oursIds == null || now - oursTime > 2f)
            {
                oursIds = new HashSet<string>(PawnsFinder.AllMapsWorldAndTemporary_Alive.Where(StillOurs).Select(p => p.ThingID));
                oursTime = now;
            }
            return oursIds;
        }

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
            if (!DebugSettings.fastLearning) modPassions[key] = level;
            return level;
        }

        public static bool Passionate(SkillRecord s) => PassionLevel(s) > 0;

        public static bool Burning(SkillRecord s) => PassionLevel(s) >= 2;
    }
}
