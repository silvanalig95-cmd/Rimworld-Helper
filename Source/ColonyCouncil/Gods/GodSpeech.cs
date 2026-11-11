using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Colonists swear by their gods now and then, in speech moments that already are (one sentence over a head, through
    /// Speech.Say; the pool Rimfolk_Pool_SayGod): a line about how they really are (Immersion/StateSpeech.cs: hurt, grieving,
    /// worn down, glad, or nothing in particular), and a word when the weather turns (Immersion/WeatherTalk.cs: a storm, a
    /// cold snap, a heat wave, the first snow). The god is the one they would swear by (Gods.SwearBy), the one of the moment's
    /// domain where they have one: the healer when hurt, the dead's when grieving, the storm's in a storm. Light: a chance
    /// on top of each moment's own rate.
    /// </summary>
    public static class GodSpeech
    {
        public const string Pool = "Rimfolk_Pool_SayGod";

        /// <summary>How often a look at someone's state brings a god in at all, and how much it weighs among the other things on their mind.</summary>
        public const float StateChance = 0.3f, StateWeight = 0.4f;

        /// <summary>How often a word about the weather is a word to a god instead.</summary>
        public const float WeatherChance = 0.35f;

        /// <summary>The weather's pools a god's word may stand in for, and the moment's tag and domain.</summary>
        private static readonly Dictionary<string, (string tag, GodDomain d)> Weather = new Dictionary<string, (string, GodDomain)>
        {
            ["Council_Pool_SayStorm"] = ("storm", GodDomain.Storm),
            ["Council_Pool_SayColdSnap"] = ("cold", GodDomain.Storm),
            ["Council_Pool_SayHeatWave"] = ("heat", GodDomain.Harvest),
            ["Council_Pool_SayFirstSnow"] = ("snow", GodDomain.Night)
        };

        /// <summary>The tags a god gives its lines: its domain and kind ("god:harvest", "kind:archotech").</summary>
        public static List<string> Tags(God g)
        {
            var tags = new List<string>();
            if (g == null) return tags;
            tags.Add("god:" + g.domain.ToString().ToLowerInvariant());
            if (g.second != GodDomain.None) tags.Add("god:" + g.second.ToString().ToLowerInvariant());
            tags.Add("kind:" + g.kind.ToString().ToLowerInvariant());
            return tags;
        }

        /// <summary>
        /// For StateSpeech: a word to their god, now and then, by how they are (hurt: the healer; grieving: the dead's).
        /// Null when they have no god, the gods are off, or it isn't the moment. <paramref name="args"/>: {GOD}, {GODEPITHET}, {GODOF}.
        /// </summary>
        public static List<string> StateTags(Pawn p, out NamedArgument[] args)
        {
            args = null;
            if (!Gods.On || p == null || !Rand.Chance(StateChance)) return null;
            var tags = Flavor.TagsFor(p);
            var about = tags.Contains("hurt") ? GodDomain.Healing : tags.Contains("grieving") ? GodDomain.Dead : GodDomain.None;
            var g = Gods.SwearBy(p, about);
            if (g == null) return null;
            args = Gods.Args(g);
            return Tags(g);
        }

        /// <summary>
        /// For WeatherTalk: now and then, in place of a line from one of the weather's pools, a word to their god about it,
        /// filled in and ready to say; null to say the pool's own.
        /// </summary>
        public static string InPlaceOf(string pool, Pawn p)
        {
            if (!Gods.On || p == null || pool == null || !Weather.TryGetValue(pool, out var w) || !Rand.Chance(WeatherChance)) return null;
            try
            {
                var g = Gods.SwearBy(p, w.d);
                if (g == null) return null;
                var extra = Tags(g);
                extra.Add(w.tag);
                string line = Flavor.FromPool(Pool, p, null, null, extra.ToArray());
                if (line.NullOrEmpty()) return null;
                var args = new List<NamedArgument> { p.Named("PAWN") };
                args.AddRange(Gods.Args(g));
                return line.Formatted(args.ToArray()).Resolve().CapitalizeFirst();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a word to a god", e);
                return null;
            }
        }
    }
}
