using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Conditions on what's going on in a colonist's life, shared by dreams, moments between colonists and social
    /// events: what they feel (the heart's feelings), what ails them, whether a camp is on their map, and what is
    /// happening in the region (world events). Each answers "no" when its feature is off, so nothing written for a
    /// feature ever fires without it. Cheap enough to ask when a moment is weighed: no allocation, no map walks.
    /// </summary>
    public static class LifeConditions
    {
        /// <summary>
        /// Their strongest feeling among <paramref name="keys"/> (FeelingDef keys: Fear, Anger, Gratitude...) that is
        /// at least <paramref name="minShare"/> of full strength, or null. With <paramref name="needTarget"/>, only a
        /// feeling about someone else who is alive and humanlike.
        /// </summary>
        public static FeelingNow Feels(Pawn p, List<string> keys, float minShare, bool needTarget = false)
        {
            if (p == null || keys.NullOrEmpty()) return null;
            PawnHeart h;
            try
            {
                h = Heart.Of(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("life conditions: heart", e);
                return null;
            }
            if (h?.feelings == null) return null;
            FeelingNow best = null;
            for (int i = 0; i < h.feelings.Count; i++)
            {
                var f = h.feelings[i];
                if (f?.def == null || f.Share < minShare || !keys.Contains(f.def.key)) continue;
                if (needTarget && (f.target == null || f.target == p || f.target.Dead || !f.target.RaceProps.Humanlike)) continue;
                if (best == null || f.intensity > best.intensity) best = f;
            }
            return best;
        }

        /// <summary>
        /// They have one of these conditions, visible (HediffDef names; "Prefix*" for a family, like "Council_Ail_*").
        /// </summary>
        public static bool Has(Pawn p, List<string> hediffs)
        {
            var hs = p?.health?.hediffSet?.hediffs;
            if (hs == null || hediffs.NullOrEmpty()) return false;
            for (int i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                if (h?.def == null || !h.Visible) continue;
                if (Matches(h.def.defName, hediffs)) return true;
            }
            return false;
        }

        /// <summary>One of Rimfolk's everyday ailments or serious illnesses, visible (not what an illness leaves behind).</summary>
        public static bool Ailing(Pawn p, bool seriousOnly = false)
        {
            var hs = p?.health?.hediffSet?.hediffs;
            if (hs == null) return false;
            for (int i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                if (h == null || !h.Visible) continue;
                var info = AilmentTracker.InfoOf(h.def);
                if (info == null || info.lingering) continue;
                if (!seriousOnly || info.serious) return true;
            }
            return false;
        }

        /// <summary>A memory of one of these thoughts (names or "Prefix*"), at most <paramref name="days"/> old, or null.</summary>
        public static Thought_Memory Remembered(Pawn p, List<string> thoughts, float days)
        {
            var memories = p?.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null || thoughts.NullOrEmpty()) return null;
            int max = (int)(days * GenDate.TicksPerDay);
            for (int i = 0; i < memories.Count; i++)
            {
                var m = memories[i];
                if (m?.def != null && m.age <= max && Matches(m.def.defName, thoughts)) return m;
            }
            return null;
        }

        /// <summary>Both have the same one of these conditions, visible (a cold going round, lice in a shared room).</summary>
        public static bool ShareOne(Pawn a, Pawn b, List<string> hediffs)
        {
            var hs = a?.health?.hediffSet?.hediffs;
            if (hs == null || b?.health?.hediffSet == null || hediffs.NullOrEmpty()) return false;
            for (int i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                if (h?.def == null || !h.Visible || !Matches(h.def.defName, hediffs)) continue;
                var other = b.health.hediffSet.GetFirstHediffOfDef(h.def);
                if (other != null && other.Visible) return true;
            }
            return false;
        }

        /// <summary>A name against a list of names and "Prefix*" patterns, compared in place.</summary>
        public static bool Matches(string name, List<string> patterns)
        {
            if (name == null || patterns == null) return false;
            for (int i = 0; i < patterns.Count; i++)
            {
                string n = patterns[i];
                if (n.NullOrEmpty()) continue;
                bool match = n[n.Length - 1] == '*'
                    ? name.Length >= n.Length - 1 && string.CompareOrdinal(name, 0, n, 0, n.Length - 1) == 0
                    : name == n;
                if (match) return true;
            }
            return false;
        }

        /// <summary>A camp (Rimfolk's world events) is set up or settling in on this map, not leaving or gone.</summary>
        public static bool CampOn(Map map)
        {
            if (map == null || !CampTracker.Enabled) return false;
            var c = CampTracker.Instance?.camp;
            return c != null && c.map == map && (c.phase == CampPhase.Building || c.phase == CampPhase.Living);
        }

        /// <summary>A world event of one of these kinds ("sickness", "hunger", "fair"...) or defNames is running.</summary>
        public static bool Situation(List<string> kindsOrNames)
        {
            if (kindsOrNames.NullOrEmpty() || ColonyCouncilMod.Settings == null || !ColonyCouncilMod.Settings.worldEvents) return false;
            var world = WorldSituations.Instance;
            if (world == null) return false;
            try
            {
                foreach (var s in world.Running)
                {
                    if (s?.def == null) continue;
                    if ((s.def.kind != null && kindsOrNames.Contains(s.def.kind)) || kindsOrNames.Contains(s.def.defName)) return true;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("life conditions: world events", e);
            }
            return false;
        }
    }
}
