using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A thing carried over by hand from code (colony life, part 2): the toy a wish asked for taken to the child (A11), the
    /// children's pieces taken to the elders (A28). One of the play-out engine's own handings over (PlayKind.Give, as an
    /// answer's gift is): it waits for its hours and for the giver and the one it's for to be free, the thing is forbidden
    /// while it waits, and the giver walks it over and sets it down at their feet. Its moods are held until it has happened.
    /// With "Answers play out on the map" off nothing is carried, and the caller gives its moods at once.
    /// </summary>
    public static class LifeHands
    {
        /// <summary>
        /// Sets <paramref name="carrier"/> off to hand <paramref name="thing"/> (lying on the map) to <paramref name="to"/>,
        /// with <paramref name="moods"/> held until it is done. The plan, or null when it can't be.
        /// </summary>
        public static PlayOut Give(Pawn carrier, Pawn to, Thing thing, string label, List<HeldMood> moods = null, int hourFrom = 8, int hourTo = 21, float waitHours = 18f)
        {
            var tracker = PlayOutTracker.Instance;
            if (!PlayOuts.On || tracker == null || carrier == null || to == null || thing == null || !thing.Spawned) return null;
            if (!PlayOuts.OurPerson(carrier) || carrier == to || carrier.MapHeld != thing.Map || to.MapHeld != thing.Map) return null;
            try
            {
                var map = thing.Map;
                int now = Find.TickManager.TicksGame;
                thing.SetForbidden(true, false);
                var p = new PlayOut
                {
                    kind = PlayKind.Give,
                    map = map,
                    created = now,
                    hourFrom = hourFrom,
                    hourTo = hourTo,
                    durationTicks = PlayOutMath.DurationTicks(1f),
                    label = label,
                    things = new List<Thing> { thing },
                    visitor = carrier,
                    organizer = carrier,
                    invited = new List<Pawn> { carrier },
                    mode = PlayGive.Give,
                    other = to,
                    named = new List<Pawn> { carrier },
                    count = 1
                };
                if (PlayOuts.OurPerson(to)) p.named.Add(to);
                if (moods != null) p.moods.AddRange(moods.Where(m => m?.pawn != null));
                p.deadline = PlayOutMath.Deadline(now, GenLocalDate.HourOfDay(map), p.hourFrom, p.hourTo, waitHours);
                tracker.Add(p);
                return p;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colony life, part 2: a thing handed over", e);
                thing.SetForbidden(false, false);
                return null;
            }
        }

        /// <summary>A memory held for someone until the handing over is done.</summary>
        public static HeldMood Mood(Pawn p, ThoughtDef thought, Pawn about, string cause = null) =>
            p == null || thought == null ? null : new HeldMood { pawn = p, thought = thought, about = about, cause = cause };

        /// <summary>The newest of these things on the map, lying loose or in storage (not carried, not in a container), or null.</summary>
        public static Thing Newest(Map map, ICollection<ThingDef> defs, int afterId = -1)
        {
            if (map == null || defs == null || defs.Count == 0) return null;
            Thing best = null;
            foreach (var d in defs)
            {
                foreach (var t in map.listerThings.ThingsOfDef(d))
                {
                    if (t == null || !t.Spawned || t.thingIDNumber <= afterId || t.IsForbidden(Faction.OfPlayer)) continue;
                    if (best == null || t.thingIDNumber > best.thingIDNumber) best = t;
                }
                // A sculpture made and packed up: the minified thing that holds one.
                foreach (var t in map.listerThings.ThingsOfDef(ThingDefOf.MinifiedThing))
                {
                    if (!(t is MinifiedThing m) || m.InnerThing?.def != d || !t.Spawned || t.thingIDNumber <= afterId || t.IsForbidden(Faction.OfPlayer)) continue;
                    if (best == null || t.thingIDNumber > best.thingIDNumber) best = t;
                }
            }
            return best;
        }
    }
}
