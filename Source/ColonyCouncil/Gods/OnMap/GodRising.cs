using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A god rising from the land for an hour or two, very rarely (GodShapeDef.rises; GodsOnMapMath.RisingDue), after days of
    /// its signs; by day, so it is seen:
    /// <list type="bullet">
    /// <item>with no body of its own (Hollin: "roots break through the ground and draw themselves up into a shape taller
    /// than any tree, bark and moss and a face like a knot in old wood. She stands for an hour and sinks again."): the
    /// ground breaks and the shape stands drawn on the map (GodSky);</item>
    /// <item>in its true form (its shape's trueForm, a creature by name; Murmur: "it surfaces in a marsh or a flooded
    /// field: a toad the size of a house, mottled and old, with eyes like wet stones. It looks at the colony for a while and
    /// sinks again", Odyssey's colossus toad when it is loaded): it comes up out of the wet ground with all its presence,
    /// looks at the colony a couple of hours, and sinks again.</item>
    /// </list>
    /// The card says what was seen, those who saw it remember it together, the place is a mark on the land and the colony's
    /// book keeps it in a witness's words. Nothing is explained.
    /// </summary>
    public static class GodRising
    {
        public const float Height = 11f;

        /// <summary>How long a true form stays up before it sinks again, in hours.</summary>
        public const int FormHours = 2;

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The god rises at <paramref name="map"/> now. False when it can't this hour (dark, or nowhere to rise).</summary>
        public static bool Rise(Map map, God god)
        {
            if (map == null || god == null || !Gods.On || map.skyManager.CurSkyGlow < 0.4f) return false;
            var kind = GodShapes.TrueForm(god);
            if (GodShapes.Of(god).trueForm != null) return kind != null && RiseAs(map, god, kind);
            if (GodSky.On(map, SkySign.Rising)) return false;
            var at = Place(map);
            if (!at.IsValid) return false;
            Break(map, at);
            GodSky.Stay(map, SkySign.Rising, at, GenDate.TicksPerHour, Height);
            var spec = PresenceSpec.For(god, map, PresenceKind.Appear, at);
            // The card says only what is seen.
            spec.title = GodsOnMapWords.Title(god, map, false, "CC_GodM_Rising_Title", "CC_GodM_Rising_TitleNamed");
            spec.text = "CC_GodM_Rising_Text".Translate(Homes.Name(map).Named("HOME")).Resolve();
            if (!GodPresenceSeam.Call(spec)) Tell.Letter(NewsKind.Land, Importance.Important, map, spec.title, spec.text, LetterDefOf.NeutralEvent, new LookTargets(at, map));
            Remember(god, map, at, spec.veiled, "CC_GodM_Saw_Rising", "CC_GodM_Rising_Act", "CC_GodM_Memory_Rising");
            Said(god, map, at);
            return true;
        }

        /// <summary>Someone who saw it says so.</summary>
        private static void Said(God god, Map map, IntVec3 at)
        {
            var seer = GodsOnMapWords.Seer(map, at, 45f);
            if (seer == null) return;
            string line = Flavor.FromPool(GodOmens.RemarkPool, seer, null, null, "rising", god.domain.ToString().ToLowerInvariant(), GodShapes.LineTag(god));
            if (!line.NullOrEmpty()) Speech.Say(seer, line);
        }

        // ================= In its true form =================

        /// <summary>
        /// It surfaces in its true form in the wet ground nearest the colony (a marsh, mud, shallow water), with all its
        /// presence, and stays a couple of hours. False when there is no wet ground or it is already up.
        /// </summary>
        private static bool RiseAs(Map map, God god, PawnKindDef kind)
        {
            var b = GodsOnMapBook.Instance;
            if (b == null || b.risen != null) return false;
            var at = Wet(map);
            if (!at.IsValid) return false;
            Pawn x;
            try
            {
                float grown = kind.RaceProps.lifeStageAges.NullOrEmpty() ? 3f : kind.RaceProps.lifeStageAges.Last().minAge;
                x = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, fixedBiologicalAge: grown + 80f));
                if (x == null) return false;
                GenSpawn.Spawn(x, at, map, Rot4.South);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a true form surfacing", e);
                return false;
            }
            string label = god.known && !Gods.Name(god).NullOrEmpty() ? Gods.Name(god) : GodShapes.TrueLook(god)?.label ?? x.def.label;
            var look = GodShapes.TrueLook(god) ?? GodsWiring.LookOf(god);
            if (look == null || !GodAvatar.Make(x, look, label, false))
            {
                x.Destroy();
                return false;
            }
            try
            {
                var food = x.needs?.food;
                if (food != null) food.CurLevel = food.MaxLevel;
                x.mindState.exitMapAfterTick = Now + GenDate.TicksPerDay;
                for (int i = 0; i < 8; i++) FleckMaker.ThrowDustPuffThick(at.ToVector3Shifted() + new Vector3(Rand.Range(-1.5f, 1.5f), 0f, Rand.Range(-1.5f, 1.5f)), map, 1.6f, new Color(0.4f, 0.5f, 0.45f, 0.6f));
                // It sits still in the water and looks at the colony.
                var wait = JobMaker.MakeJob(JobDefOf.Wait, x);
                wait.expiryInterval = FormHours * GenDate.TicksPerHour;
                x.jobs.StartJob(wait, Verse.AI.JobCondition.InterruptForced);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a true form surfacing", e);
            }
            b.risen = x;
            b.risenUntil = Now + FormHours * GenDate.TicksPerHour;
            var spec = PresenceSpec.For(god, map, PresenceKind.Appear, at, x);
            string seen = GodShapes.TrueLook(god)?.label ?? x.def.label;
            spec.title = GodsOnMapWords.Title(god, map, false, "CC_GodM_Surfaced_Title", "CC_GodM_Surfaced_TitleNamed");
            spec.text = "CC_GodM_Surfaced_Text".Translate(Homes.Name(map).Named("HOME"), seen.Named("BEAST")).Resolve();
            if (!GodPresenceSeam.Call(spec)) Tell.Letter(NewsKind.Land, Importance.Important, map, spec.title, spec.text, LetterDefOf.NeutralEvent, new LookTargets(x));
            Remember(god, map, at, spec.veiled, "CC_GodM_Saw_Surfaced", "CC_GodM_Surfaced_Act", "CC_GodM_Memory_Surfaced", seen.Named("BEAST"));
            Said(god, map, at);
            return true;
        }

        /// <summary>The wet ground nearest the colony's people where something could surface, or none.</summary>
        private static IntVec3 Wet(Map map)
        {
            var near = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            var from = near?.Position ?? map.Center;
            foreach (var c in GenRadial.RadialCellsAround(from, 56f, true))
            {
                if (!c.InBounds(map) || c.Fogged(map) || c.Roofed(map) || !c.Standable(map) || c.GetFirstPawn(map) != null || c.GetEdifice(map) != null) continue;
                var t = c.GetTerrain(map);
                if (t == null) continue;
                string n = t.defName;
                if (n.IndexOf("Marsh", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Mud", StringComparison.OrdinalIgnoreCase) >= 0 || t.IsWater) return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>A true form's time is up (or it wandered off): it sinks back into the water, gone from the world.</summary>
        internal static void Hourly(GodsOnMapBook b, int now)
        {
            var x = b.risen;
            if (x == null) return;
            if (x.Dead || x.Discarded)
            {
                b.risen = null;
                return;
            }
            if (x.Spawned && now < b.risenUntil) return;
            b.risen = null;
            try
            {
                if (x.Spawned)
                {
                    for (int i = 0; i < 8; i++) FleckMaker.ThrowDustPuffThick(x.DrawPos + new Vector3(Rand.Range(-1f, 1f), 0f, Rand.Range(-1f, 1f)), x.Map, 1.4f, new Color(0.4f, 0.5f, 0.45f, 0.6f));
                    x.DeSpawn();
                }
                GodAvatar.Unmake(x);
                if (Find.WorldPawns.Contains(x)) Find.WorldPawns.RemoveAndDiscardPawnViaGC(x);
                else Find.WorldPawns.PassToWorld(x, PawnDiscardDecideMode.Discard);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a true form sinking", e);
            }
        }

        // ================= With no body =================

        /// <summary>Where it rises: by an anima tree of the colony's land if there is one, else open wild ground just outside the colony.</summary>
        private static IntVec3 Place(Map map)
        {
            var anima = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_TreeAnima");
            var tree = anima == null ? null : map.listerThings.ThingsOfDef(anima).FirstOrDefault(t => t.Spawned);
            if (tree != null && CellFinder.TryFindRandomCellNear(tree.Position, map, 6, c => Fits(map, c) && !c.InHorDistOf(tree.Position, 2.5f), out var byTree)) return byTree;
            var near = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            for (int i = 0; i < 20 && near != null; i++)
                if (RCellFinder.TryFindRandomSpotJustOutsideColony(near, out var c) && Fits(map, c))
                    return c;
            return IntVec3.Invalid;
        }

        private static bool Fits(Map map, IntVec3 c) =>
            c.InBounds(map) && c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && c.GetTerrain(map)?.IsWater != true && map.fertilityGrid.FertilityAt(c) > 0.3f
            && !c.GetThingList(map).Any(t => t is Pawn || t is Building);

        /// <summary>The ground breaking as the roots come up: the earth shakes, dust and loose soil round the place, and the plants about it thicker.</summary>
        private static void Break(Map map, IntVec3 at)
        {
            try
            {
                if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(0.8f);
                var dirt = DefDatabase<ThingDef>.GetNamedSilentFail("Filth_Dirt");
                foreach (var c in GenRadial.RadialCellsAround(at, 3.5f, true).Where(c => c.InBounds(map) && c.Standable(map)))
                {
                    FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(), map, Rand.Range(1f, 2f), new Color(0.38f, 0.3f, 0.2f, 0.75f));
                    if (dirt != null && Rand.Chance(0.5f)) FilthMaker.TryMakeFilth(c, map, dirt);
                }
                foreach (var c in GenRadial.RadialCellsAround(at, 9f, true))
                    if (c.InBounds(map) && c.GetPlant(map) is Plant p && p.def.plant != null && !p.def.plant.IsTree && p.Growth < 1f)
                        p.Growth = Mathf.Min(1f, p.Growth + 0.3f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the ground breaking", e);
            }
        }

        /// <summary>Kept: in the colony's book and chronicle in the words of one who saw it, as a mark on the land, and among those who saw it.</summary>
        private static void Remember(God god, Map map, IntVec3 at, bool veiled, string sawKey, string actKey, string memoryKey, params NamedArgument[] more)
        {
            var seer = GodsOnMapWords.Seer(map, at, 45f);
            string saw = GodsOnMapWords.Saw(god, map, at, GodActKind.Appear, seer, sawKey, more);
            if (!veiled) Gods.Learn(god, "visitors");
            GodMarks.Note(god, map, at, actKey.Translate(more).Resolve(), PresenceKind.Appear, !veiled);
            GodMarks.Witnessed(map, at, memoryKey.Translate(more).Resolve(), null);
            if (saw != null) CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, saw, seer));
        }
    }
}
