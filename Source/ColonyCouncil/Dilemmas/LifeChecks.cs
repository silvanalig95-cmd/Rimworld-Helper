using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What is going on in someone's life, for the people a dilemma, an encounter or a stranger role asks for: an illness,
    /// a memory still on their mind, a mood that holds right now. And what is going on in the colony: a busy time, the
    /// medicine in the stores. All of it cheap enough to ask whenever a dilemma looks for its people.
    /// </summary>
    public static class LifeChecks
    {
        /// <summary>
        /// The illness that troubles them most, or null: a Rimfolk ailment that shows (not a quiet one or the cough an
        /// illness leaves behind), or any other disease their body is still fighting or that makes them feel sick.
        /// </summary>
        public static Hediff Illness(Pawn p)
        {
            var hediffs = p?.health?.hediffSet?.hediffs;
            if (hediffs == null) return null;
            Hediff best = null;
            for (int i = 0; i < hediffs.Count; i++)
            {
                var h = hediffs[i];
                if (h?.def == null || !h.Visible || !IsIllness(h)) continue;
                if (best == null || h.Severity > best.Severity) best = h;
            }
            return best;
        }

        /// <summary>Wounds still healing worth about a gunshot or a few cuts: enough to notice, more than a scratch.</summary>
        public const float HurtFrom = 6f;

        /// <summary>The severity of their wounds still healing: not scars, not lost limbs.</summary>
        public static float WoundSeverity(Pawn p)
        {
            var hediffs = p?.health?.hediffSet?.hediffs;
            if (hediffs == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < hediffs.Count; i++)
                if (hediffs[i] is Hediff_Injury injury && !injury.IsPermanent()) sum += injury.Severity;
            return sum;
        }

        /// <summary>Hurt (wounds of <see cref="HurtFrom"/> or more) or ill, and still healing.</summary>
        public static bool Healing(Pawn p) => WoundSeverity(p) >= HurtFrom || Illness(p) != null;

        private static bool IsIllness(Hediff h)
        {
            var info = AilmentTracker.InfoOf(h.def);
            if (info != null) return !info.lingering && !info.quiet;
            if (h.def.makesSickThought) return true;
            return h is HediffWithComps hc && hc.TryGetComp<HediffComp_Immunizable>() != null && !h.FullyImmune();
        }

        /// <summary>
        /// Whether they have one of these illnesses: "HeadCold|Typhus|Flu", by a Rimfolk ailment's key or any hediff's
        /// defName. Empty: any illness.
        /// </summary>
        public static bool HasIllness(Pawn p, string which)
        {
            if (which.NullOrEmpty()) return Illness(p) != null;
            var hediffs = p?.health?.hediffSet?.hediffs;
            if (hediffs == null) return false;
            var names = which.Split('|');
            for (int i = 0; i < hediffs.Count; i++)
            {
                var h = hediffs[i];
                if (h?.def == null || !h.Visible || !IsIllness(h)) continue;
                string key = AilmentTracker.InfoOf(h.def)?.key;
                foreach (var n in names)
                {
                    string name = n.Trim();
                    if (name.Length > 0 && (name == h.def.defName || name == key)) return true;
                }
            }
            return false;
        }

        /// <summary>The illness to name in a letter ("head cold"), or "".</summary>
        public static string IllnessLabel(Pawn p) => Illness(p)?.def?.label ?? "";

        /// <summary>An old injury that still troubles them: a scar or an old wound that never healed clean, or a bad back.</summary>
        public static bool OldInjury(Pawn p)
        {
            var hediffs = p?.health?.hediffSet?.hediffs;
            if (hediffs == null) return false;
            for (int i = 0; i < hediffs.Count; i++)
            {
                var h = hediffs[i];
                if (h?.def == null || !h.Visible) continue;
                if (h is Hediff_Injury inj && inj.IsPermanent()) return true;
                if (h.def.defName == "BadBack") return true;
            }
            return false;
        }

        /// <summary>
        /// Whether one of these is on their mind: a memory no older than <paramref name="days"/> (about
        /// <paramref name="about"/>, when given), or a mood that holds right now (the pressures, a decree). A name ending
        /// in * stands for every thought that starts so ("Council_Friction_*").
        /// </summary>
        public static bool HasThought(Pawn p, List<string> names, float days, Pawn about = null)
        {
            if (p?.needs?.mood?.thoughts == null || names.NullOrEmpty()) return false;
            int window = (int)(days * GenDate.TicksPerDay);
            var memories = p.needs.mood.thoughts.memories?.Memories;
            if (memories != null)
            {
                for (int i = 0; i < memories.Count; i++)
                {
                    var m = memories[i];
                    if (m?.def == null || m.age > window || !Named(m.def.defName, names)) continue;
                    if (about != null && m.otherPawn != about) continue;
                    return true;
                }
            }
            if (about != null) return false;
            foreach (var n in names)
            {
                if (n.NullOrEmpty() || n.EndsWith("*")) continue;
                var def = DefDatabase<ThoughtDef>.GetNamedSilentFail(n);
                if (def?.workerClass == null) continue;
                try
                {
                    if (def.Worker.CurrentState(p).Active) return true;
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("thought check " + n, e);
                }
            }
            return false;
        }

        private static bool Named(string defName, List<string> names)
        {
            foreach (var n in names)
            {
                if (n.NullOrEmpty()) continue;
                if (n.EndsWith("*") ? defName.StartsWith(n.Substring(0, n.Length - 1)) : defName == n) return true;
            }
            return false;
        }

        /// <summary>Medicine of any kind in storage.</summary>
        public static int Medicine(Map map)
        {
            if (map == null) return 0;
            int n = 0;
            foreach (var t in DilemmaEffect_Consume.Medicine(map)) n += t.stackCount;
            return n;
        }

        /// <summary>
        /// A bad time to be one pair of hands short: the harvest, the days after a raid, food running low, or someone laid
        /// up in bed.
        /// </summary>
        public static bool BusyTime(Map map)
        {
            if (map == null) return false;
            if (GenLocalDate.Season(map) == Season.Fall) return true;
            int threat = map.storyState.LastThreatBigTick;
            if (threat > 0 && Find.TickManager.TicksGame - threat < GenDate.TicksPerDay * 4) return true;
            try
            {
                if (MapFacts.Of(map).FoodDays < 6f) return true;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("busy time food", e);
            }
            return map.mapPawns.FreeColonistsSpawned.Any(p => p.InBed() && HealthAIUtility.ShouldSeekMedicalRest(p));
        }
    }
}
