using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A grave for a pet (the wish WishWorker_PetGrave): its person digs a grave near the colony and carries the animal
    /// into it, with the enemy-dead burial's own digging and hauling (PlayKind.Haul with the mode "petgrave",
    /// PlayDead.cs) for one animal. The grave goes beside the colony's other graves when it has some, else out past the
    /// gate as for the dead of a raid. Only the animal's own kind may be laid in it, so the colony's haulers don't fill
    /// it with someone else. The game's own grave and the game's own hauling: nothing conjured.
    /// </summary>
    public static class PetGrave
    {
        /// <summary>This burial is a pet's.</summary>
        public static bool Is(PlayOut p) => p != null && p.mode == PlayHaul.Pet;

        /// <summary>An animal's body lying whole on the open map: not dried to bones, not in the fog, not in a grave.</summary>
        public static bool Lies(Thing t)
        {
            if (!(t is Corpse c) || c.Destroyed || !c.Spawned || c.Map == null) return false;
            var x = c.InnerPawn;
            if (x?.RaceProps == null || x.RaceProps.Humanlike || !x.RaceProps.Animal) return false;
            return c.GetRotStage() != RotStage.Dessicated && !c.Position.Fogged(c.Map);
        }

        /// <summary>The animal's body, while there is one.</summary>
        public static Corpse CorpseOf(Pawn pet)
        {
            var c = pet?.Corpse;
            return c == null || c.Destroyed ? null : c;
        }

        /// <summary>The colony can dig a grave (the game's own), and a grave takes this kind of body.</summary>
        public static bool CanBury(Map map, Corpse c)
        {
            if (!PlayBurial.CanDig(map) || c == null) return false;
            var fixedFilter = PlayBurial.GraveDef?.building?.fixedStorageSettings?.filter;
            return fixedFilter == null || fixedFilter.Allows(c.def);
        }

        /// <summary>The bodies of named or bonded animals lying at home, each with its person still here.</summary>
        public static IEnumerable<Corpse> Bodies(Map map)
        {
            if (map == null) yield break;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
            {
                if (!Lies(t)) continue;
                var c = (Corpse)t;
                var pet = c.InnerPawn;
                var person = PetTracker.PersonOf(pet);
                if (person == null) continue;
                bool bonded = pet.relations != null && pet.relations.DirectRelationExists(PawnRelationDefOf.Bond, person);
                if (pet.Name == null && !bonded) continue;
                yield return c;
            }
        }

        /// <summary>
        /// Where the grave goes: a step or two from the colony's other graves, else out past the gate where the dead of
        /// a raid would go. Empty when there's no room for one.
        /// </summary>
        public static List<IntVec3> Plot(Map map, Pawn pawn, out Rot4 rot)
        {
            rot = Rot4.North;
            if (map == null) return new List<IntVec3>();
            foreach (var g in map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>().Where(g => g.Spawned).Take(20))
                foreach (var c in GenRadial.RadialCellsAround(g.Position, 5f, false))
                    foreach (var r in new[] { Rot4.North, Rot4.East })
                        if (c.InBounds(map) && PlayBurial.Fits(c, r, map, pawn, true))
                        {
                            rot = r;
                            return new List<IntVec3> { c };
                        }
            return PlayBurial.Plot(map, pawn, 1, true, out rot);
        }

        /// <summary>The grave takes this animal's kind of body and nothing else, whatever else its settings would let in.</summary>
        public static bool TakeOnly(Building_Grave grave, Thing body)
        {
            var settings = grave?.GetStoreSettings();
            var filter = settings?.filter;
            if (filter == null || body == null) return false;
            if (!filter.Allows(body.def) || filter.AllowedDefCount > 1)
            {
                // Every kind of thing out, the body's kind in; the special filters (rotten, fresh) stay as they were.
                filter.SetDisallowAll(null, DefDatabase<SpecialThingFilterDef>.AllDefsListForReading);
                filter.SetAllow(body.def, true);
            }
            if (grave.Accepts(body)) return true;
            // A special filter keeps it out (a rotting body, say): let in what this body is.
            foreach (var sf in DefDatabase<SpecialThingFilterDef>.AllDefsListForReading)
                if (sf?.Worker != null && !filter.Allows(sf) && sf.Worker.Matches(body)) filter.SetAllow(sf, true);
            return grave.Accepts(body);
        }

        // ---------------- Set going by the wish ----------------

        private static readonly Dictionary<int, int> lastArranged = new Dictionary<int, int>();

        /// <summary>
        /// The burial is set going (promised, or the person seeing to it themselves): the person digs and carries; one
        /// who can't dig has one or two who can at their side. Only with answers playing out on the map. Again, now and
        /// then, if it fell through while the body still lies there. True when it's under way or waiting for its hour.
        /// </summary>
        public static bool Arrange(Wish w, bool now)
        {
            var person = w?.Beneficiary;
            var pet = w?.other;
            var corpse = CorpseOf(pet);
            var map = person?.MapHeld;
            if (!PlayOuts.On || person == null || corpse == null || map == null || corpse.MapHeld != map || !Lies(corpse) || !CanBury(map, corpse)) return false;
            var tracker = PlayOutTracker.Instance;
            if (tracker == null) return false;
            int id = (int)w.targetValue;
            if (id > 0 && tracker.Get(id) != null) return true;
            int tick = Find.TickManager.TicksGame;
            if (!now && lastArranged.TryGetValue(w.id, out int last) && tick - last < GenDate.TicksPerDay / 2) return false;
            lastArranged[w.id] = tick;
            if (lastArranged.Count > 100) lastArranged.Clear();
            try
            {
                bool digs = !person.WorkTypeIsDisabled(WorkTypeDefOf.Construction) && !person.DevelopmentalStage.Juvenile();
                var invited = new List<Pawn> { person };
                if (!digs)
                    invited.AddRange(map.mapPawns.FreeColonistsSpawned
                        .Where(x => x != person && PlayOuts.OurPerson(x) && !x.WorkTypeIsDisabled(WorkTypeDefOf.Construction) && !x.DevelopmentalStage.Juvenile())
                        .OrderByDescending(x => x.relations?.OpinionOf(person) ?? 0).Take(2));
                var p = new PlayOut
                {
                    kind = PlayKind.Haul,
                    map = map,
                    created = tick,
                    hourFrom = 7,
                    hourTo = 19,
                    durationTicks = PlayOutMath.DurationTicks(3f),
                    label = "CC_Play_Label_PetGrave".Translate(pet.Named("PET")).Resolve(),
                    mode = PlayHaul.Pet,
                    things = new List<Thing> { corpse },
                    other = pet,
                    invited = invited,
                    named = new List<Pawn> { person },
                    organizer = person,
                    count = digs ? 1 : 2,
                    pool = "Rimfolk_Pool_PetGrave",
                    seed = Rand.Range(0, 360)
                };
                p.deadline = PlayOutMath.Deadline(tick, GenLocalDate.HourOfDay(map), p.hourFrom, p.hourTo, PlayOutMath.DefaultWaitHours);
                tracker.Add(p);
                w.targetValue = p.id;
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("pet grave", e);
                return false;
            }
        }
    }
}
