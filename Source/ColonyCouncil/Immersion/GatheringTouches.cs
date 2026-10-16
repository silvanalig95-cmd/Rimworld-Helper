using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a gathering an answer called looks like on the map, beyond the game's own party (PlayOut/PlayOutLord.cs): the
    /// fire at a bonfire smokes and, after dark, sparks; at a remembrance by a grave, a low warm glow there once night
    /// falls; at a toast, once, a while in, cups go up together with a word from whoever leads it (Hospitality's guests
    /// standing near raise theirs too); and at a feast, those eating are the ones most often heard. Called with the gathering's own look every 250 ticks, only for the map
    /// being looked at; everything drawn goes through <see cref="VisualTouches"/> (its setting, its view and its caps).
    /// </summary>
    internal static class GatheringTouches
    {
        private static readonly Color SpeechColor = new Color(1f, 0.93f, 0.72f);

        /// <summary>The fire, the grave's glow and the toast. <paramref name="raised"/>: the cups have gone up already.</summary>
        public static void Look(LordJob_PlayOut job, PlayOut play, List<Pawn> here, Map map, IntVec3 spot, int duration, ref bool raised)
        {
            if (!VisualTouches.On || job?.lord == null || here == null || here.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            var grave = play?.place as Building_Grave;
            bool remembrance = grave != null && grave.Spawned && grave.Map == map && grave.Position.InHorDistOf(spot, 6f);
            if (job.kind == PlayKind.Bonfire)
            {
                var fire = job.laidFire != null && job.laidFire.Spawned ? job.laidFire : FireNear(spot, map);
                if (fire != null) VisualTouches.Bonfire(fire, now + 400);
            }
            if (remembrance) VisualTouches.Candle(map, grave.Position, now + 400);
            if (job.kind == PlayKind.Toast && !job.words && !remembrance && !raised && here.Count >= 2 && job.lord.ticksInToil >= duration * 0.3f)
            {
                raised = true;
                // Hospitality's guests standing near raise theirs too, from where they are (they stay in their own lord).
                var guests = EventTouches.GuestsNear(map, spot, 8f).ToList();
                VisualTouches.RaiseCups(guests.Count == 0 ? here : here.Concat(guests).ToList());
                var lead = here.Contains(play?.organizer) ? play.organizer : here.RandomElement();
                string line = Flavor.FromPool(job.pool.NullOrEmpty() ? "Council_Pool_PlayToast" : job.pool, lead, null, null, "raise");
                if (!line.NullOrEmpty()) CouncilGameComponent.Speak(lead, line, SpeechColor);
            }
        }

        /// <summary>Who says the next line: at a feast, someone eating more often than not.</summary>
        public static Pawn Speaker(PlayKind kind, List<Pawn> here)
        {
            if (kind == PlayKind.Feast && Rand.Chance(0.6f))
            {
                var eating = here.Where(p => p.CurJobDef == JobDefOf.Ingest).ToList();
                if (eating.Count > 0) return eating.RandomElement();
            }
            return here.RandomElement();
        }

        /// <summary>A lit campfire, bonfire or fire pit within a few cells of the spot.</summary>
        private static Thing FireNear(IntVec3 spot, Map map)
        {
            foreach (var t in GenRadial.RadialDistinctThingsAround(spot, map, 4.9f, true))
            {
                if (!(t is Building) || t is Frame || t.def?.defName == null) continue;
                string n = t.def.defName;
                if (n.IndexOf("Campfire", System.StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("Bonfire", System.StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("FirePit", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var fuel = t.TryGetComp<CompRefuelable>();
                if (fuel == null || fuel.HasFuel) return t;
            }
            return null;
        }
    }
}
