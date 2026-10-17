using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A person's character showing, softly, in what other systems already do: the warm and the playful start more
    /// warm moments and the hot-tempered more frictions (Heart.MomentWeightFactor), the trusting confide a secret
    /// sooner (Secrets), the warm take on a student more readily (SocialWeb). Each is a small factor around 1, and 1
    /// with "Personalities and social life" off. Asked often (the moment picker), so each person's sides are kept for
    /// an in-game hour.
    /// </summary>
    public static class PersonaLean
    {
        private struct Sides
        {
            public int tick;
            public float warmth, humour, temper, trust;
        }

        private static readonly Dictionary<int, Sides> cache = new Dictionary<int, Sides>();

        /// <summary>Forget the last game's people (a game was loaded or started).</summary>
        public static void Reset() => cache.Clear();

        private static bool Get(Pawn p, out Sides s)
        {
            s = default;
            if (p == null || !Persona.On || !Persona.Eligible(p) || Find.TickManager == null) return false;
            int now = Find.TickManager.TicksGame;
            if (cache.TryGetValue(p.thingIDNumber, out s) && now - s.tick >= 0 && now - s.tick < GenDate.TicksPerHour) return true;
            var v = Persona.Values(p);
            s = new Sides
            {
                tick = now, warmth = v[(int)PersonaAxis.Warmth], humour = v[(int)PersonaAxis.Humour], temper = v[(int)PersonaAxis.Temper],
                trust = v[(int)PersonaAxis.Trust]
            };
            if (cache.Count > 600) cache.Clear();
            cache[p.thingIDNumber] = s;
            return true;
        }

        /// <summary>How much likelier this person is to start a moment of this kind (0.7 to 1.35).</summary>
        public static float Moment(Pawn initiator, bool friction, string kind)
        {
            if (!Get(initiator, out var s)) return 1f;
            return MarksMath.MomentLean(s.warmth, s.humour, s.temper, friction, kind == "grief");
        }

        /// <summary>How readily they confide a secret (0.4 to 1.6), with the systems talking to each other.</summary>
        public static float Confiding(Pawn p) => Interplay.On && Get(p, out var s) ? MarksMath.ConfideFactor(s.trust) : 1f;

        /// <summary>How readily they take on a student (0.5 to 1.6), with the systems talking to each other.</summary>
        public static float Mentoring(Pawn p) => Interplay.On && Get(p, out var s) ? MarksMath.MentorWeight(s.warmth) : 1f;
    }
}
