using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Hospitality's guests in Rimfolk's visitor stories. When one of the events that bring a single visitor fires
    /// (a kindred spirit, an old flame, a would-be apprentice, the tinker, the scholar...) and a guest who fits is
    /// staying with the colony, the story is theirs: no new face walks in, the guest at the table turns out to be the
    /// one. A guest who joins the colony by such a story joins the way Hospitality itself takes a guest in. Half the
    /// time, so guests don't carry every story. The setting "Guests in the colony's stories" turns it off.
    /// </summary>
    public static class GuestStories
    {
        public static bool On => (ColonyCouncilMod.Settings?.guestStories ?? true) && People.ModActive(Guests.Mod);

        /// <summary>A guest who could be the stranger of a story: grown, up and about, of a people at peace with the colony.</summary>
        private static bool Fits(Pawn g, FloatRange ages)
        {
            if (g == null || g.Dead || !g.Spawned || g.Downed || g.InMentalState || g.RaceProps == null || !g.RaceProps.Humanlike) return false;
            if (!Strangers.IsHospitalityGuest(g) || g.Faction == null || g.Faction.IsPlayer || g.Faction.HostileTo(Faction.OfPlayer)) return false;
            if (g.DevelopmentalStage.Juvenile() || !People.HasInnerLife(g)) return false;
            float age = Flavor.HumanAge(g);
            return age >= ages.min - 2f && age <= ages.max + 5f;
        }

        /// <summary>Guests on the map who could carry a story.</summary>
        public static IEnumerable<Pawn> Candidates(Map map, FloatRange ages)
        {
            if (!On || map == null) yield break;
            foreach (var g in Strangers.On(map, StrangerKind.Visitor))
                if (Fits(g, ages)) yield return g;
        }

        /// <summary>
        /// The story's stranger, when a guest fits and the dice say so: the context gets them and their people, and no
        /// new visitor is made. False when the event should make its own.
        /// </summary>
        public static bool TryCast(HappeningContext ctx, HappeningExtension ext, Func<Pawn, bool> fits = null)
        {
            if (!On || ctx?.map == null || !Rand.Chance(0.5f)) return false;
            var g = Candidates(ctx.map, ext?.ages ?? new FloatRange(18f, 60f)).Where(p => fits == null || fits(p)).RandomElementWithFallback();
            if (g == null) return false;
            ctx.stranger = g;
            ctx.faction = g.Faction;
            ctx.group.Clear();
            ctx.xWaiting = false;
            return true;
        }
    }
}
