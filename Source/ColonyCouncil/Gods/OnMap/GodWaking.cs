using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The sleeper under the mountain (Smoulder: "a colony that mines too deep under the mountain makes him turn in his
    /// sleep. Cave-ins, a vein that closes, a heat rising from the floor. Waking him fully: it would be the end of a colony,
    /// or the start of a legend."). A god whose signs are the rock rumbling: where the colony mines under thick rock, now
    /// and then (days apart) the rock rumbles over the miner, dust and loose stone come down, and the floor warms; from the
    /// third time, stone may fall on the miner (the shared harm budget, "dangers on the map", shown as the game shows a
    /// hit). Past every warning (GodsOnMapMath.Wakes), and not within years of the last time, it wakes: the deep working
    /// closes behind a fall of rock (never on anyone), and when the deep has drawn it near enough to show itself (GodNear: the
    /// rumbles, its relations and what the colony did) its true form comes up out of the mountain (its shape's trueForm:
    /// Dragon's Descent's true dragon, by name, when the mod is loaded), with all its presence, looks at the colony a few
    /// hours and goes; else the mountain quakes and its shadow crosses the land.
    /// It is never hostile unless harmed (then the game's own revenge). The card says what was seen; nothing explains it.
    /// </summary>
    public static class GodWaking
    {
        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The god asleep in the rock: the one whose signs include the rock rumbling, or null.</summary>
        public static God Sleeper => Gods.All.FirstOrDefault(g => g != null && g.kind != GodKind.Dead && GodShapes.Omens(g)?.Contains(OmenKind.Rumble) == true);

        // ================= Turning in its sleep =================

        /// <summary>Someone of the colony mining under thick rock now, on a home map, or null.</summary>
        public static Pawn Miner(out Map map)
        {
            map = null;
            var thick = RoofDefOf.RoofRockThick;
            foreach (var m in Homes.All.ToList())
            {
                if (m == null) continue;
                var p = m.mapPawns.FreeColonistsSpawned.FirstOrDefault(x => x.CurJobDef == JobDefOf.Mine && x.CurJob.targetA.Cell.InBounds(m) && x.CurJob.targetA.Cell.GetRoof(m) == thick);
                if (p == null) continue;
                map = m;
                return p;
            }
            return null;
        }

        /// <summary>
        /// It turns in its sleep over <paramref name="miner"/>: the rock rumbles, the floor warms, someone says so, a little
        /// of its favour goes (unseen), stone may fall on the miner, and past every warning it wakes.
        /// </summary>
        public static void Turned(GodsOnMapBook b, God god, Map map, Pawn miner, int now)
        {
            if (b == null || god == null || map == null || miner == null) return;
            b.deepWarnings = GodsOnMapMath.Warned(b.deepWarnings, b.lastDeep < -900000 ? 99999 : (now - b.lastDeep) / GenDate.TicksPerDay);
            b.lastDeep = now;
            var at = miner.Position;
            GodOmens.Rumble(map, at);
            GenTemperature.PushHeat(at, map, 30f);
            Gods.AddFavour(god, map, -1f, null, FavourSource.Neglect);
            GodOmens.Seen(god, map, at, OmenKind.Rumble, miner);
            // Before stone ever falls, the floor of the deep working grows hot for hours (Gods/OnMap/OwnSmoulder.cs).
            OwnSmoulder.Turned(god, map, at, miner, b.deepWarnings);
            if (GodsOnMapMath.StoneFalls(b.deepWarnings, Harm.ScaleFor(HarmKinds.Dangers), Rand.Value)
                && Harm.Injure(miner, HurtKind.Bruise, HurtWhere.Any, 3f, 7f, HarmKinds.Dangers) != null)
                Tell.Message(NewsKind.People, Importance.Important, map, "CC_GodM_StoneFell".Translate(miner.Named("PAWN")).Resolve(), new LookTargets(miner),
                    MessageTypeDefOf.NegativeHealthEvent);
            int sinceWake = b.lastWake < -900000 ? 99999 : (now - b.lastWake) / GenDate.TicksPerDay;
            if (GodsOnMapMath.Wakes(b.deepWarnings, sinceWake)) Wake(b, god, map, miner, now);
        }

        // ================= Waking =================

        /// <summary>It wakes over <paramref name="miner"/>'s working: the working closes, and its true form comes up out of the mountain, or the mountain quakes and its shadow passes. True when it did.</summary>
        public static bool Wake(GodsOnMapBook b, God god, Map map, Pawn miner, int now)
        {
            if (b == null || god == null || map == null || b.woken != null) return false;
            var at = miner?.Position ?? map.Center;
            // How near the waking finds it, before the warnings are let go.
            float near = GodNear.Of(god, map);
            b.deepWarnings = 0;
            b.lastWake = now;
            Close(map, at, miner);
            try
            {
                if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(2.5f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the mountain shaking", e);
            }
            Gods.AddFavour(god, map, -4f, null, FavourSource.Neglect);
            var kind = GodShapes.TrueForm(god);
            Pawn form = GodsOnMapMath.TrueForm(near, kind != null, Rand.Value) ? Rise(b, god, map, at, kind) : null;
            var cell = form?.Position ?? at;
            if (form == null) GodSky.Pass(map, SkySign.Wings);
            var spec = PresenceSpec.For(god, map, form != null ? PresenceKind.Appear : PresenceKind.Wrath, cell, form);
            // Its true form up on the land is the god seen plainly.
            if (form != null) spec.veiled = false;
            spec.title = form != null ? GodsOnMapWords.Title(god, map, false, "CC_GodM_Woken_Title", "CC_GodM_Woken_TitleNamed")
                : "CC_GodM_Quake_Title".Translate().Resolve().CapitalizeFirst();
            spec.text = (form != null ? "CC_GodM_Woken_Text" : "CC_GodM_Quake_Text").Translate(Homes.Name(map).Named("HOME")).Resolve();
            if (!GodPresenceSeam.Call(spec)) Tell.Letter(NewsKind.Land, Importance.Important, map, spec.title, spec.text, LetterDefOf.ThreatSmall, new LookTargets(cell, map));
            var seer = GodsOnMapWords.Seer(map, cell, 50f) ?? miner;
            string saw = GodsOnMapWords.Saw(god, map, cell, form != null ? GodActKind.Appear : GodActKind.Wrath, seer, form != null ? "CC_GodM_Saw_Woken" : "CC_GodM_Saw_Quake");
            if (form != null && !spec.veiled) Gods.Learn(god, "visitors");
            GodMarks.Note(god, map, cell, (form != null ? "CC_GodM_Woken_Act" : "CC_GodM_Quake_Act").Translate().Resolve(), PresenceKind.Wrath, form != null && !spec.veiled);
            GodMarks.Witnessed(map, cell, (form != null ? "CC_GodM_Memory_Woken" : "CC_GodM_Memory_Quake").Translate().Resolve(), miner == null ? null : new[] { miner });
            if (saw != null) CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, saw, seer));
            return true;
        }

        /// <summary>
        /// The deep working closes: rock comes down in the worked-out cells about the mining (never where anyone stands or
        /// next to the miner, so no one is buried), a vein that closes.
        /// </summary>
        private static void Close(Map map, IntVec3 at, Pawn miner)
        {
            var rocks = DefDatabase<ThingDef>.GetNamedSilentFail("CollapsedRocks");
            var thick = RoofDefOf.RoofRockThick;
            int closed = 0;
            foreach (var c in GenRadial.RadialCellsAround(at, 6f, false).InRandomOrder())
            {
                if (closed >= 6) break;
                if (!c.InBounds(map) || c.GetRoof(map) != thick || !c.Standable(map) || c.GetEdifice(map) != null || c.GetFirstPawn(map) != null || c.GetFirstItem(map) != null) continue;
                if (miner != null && c.InHorDistOf(miner.Position, 2.5f)) continue;
                try
                {
                    FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(), map, 2f, new Color(0.6f, 0.58f, 0.52f, 0.8f));
                    if (rocks != null) GenSpawn.Spawn(rocks, c, map);
                    closed++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: the working closing", e);
                }
            }
            GodOmens.Rumble(map, at);
        }

        /// <summary>Its true form comes up out of the mountain at the foot of the rock nearest the working, shown as the god's with all its presence. The creature, or null.</summary>
        private static Pawn Rise(GodsOnMapBook b, God god, Map map, IntVec3 at, PawnKindDef kind)
        {
            if (!CellFinder.TryFindRandomCellNear(at, map, 40, c => c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && c.GetFirstPawn(map) == null
                                                                && GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)).Any(n => n.InBounds(map) && n.GetEdifice(map)?.def.building?.isNaturalRock == true),
                    out var foot)
                && !RCellFinder.TryFindRandomPawnEntryCell(out foot, map, 0f))
                return null;
            Pawn x;
            try
            {
                float grown = kind.RaceProps.lifeStageAges.NullOrEmpty() ? 5f : kind.RaceProps.lifeStageAges.Last().minAge;
                x = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, fixedBiologicalAge: grown + 300f));
                if (x == null) return null;
                GenSpawn.Spawn(x, foot, map, Rot4.Random);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the sleeper's true form", e);
                return null;
            }
            var look = GodShapes.TrueLook(god) ?? GodsWiring.LookOf(god);
            if (!Gods.On || look == null || !GodAvatar.Make(x, look, Label(god), false))
            {
                x.Destroy();
                return null;
            }
            try
            {
                var food = x.needs?.food;
                if (food != null) food.CurLevel = food.MaxLevel;
                x.mindState.exitMapAfterTick = Now + GodsOnMapMath.WokenHours * GenDate.TicksPerHour;
                for (int i = 0; i < 12; i++) FleckMaker.ThrowSmoke(foot.ToVector3Shifted() + new Vector3(Rand.Range(-2f, 2f), 0f, Rand.Range(-2f, 2f)), map, 2.5f);
                FleckMaker.ThrowHeatGlow(foot, map, 3f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the sleeper's true form", e);
            }
            b.woken = x;
            b.wokenGod = god.id;
            b.wokenUntil = Now + GenDate.TicksPerDay;
            return x;
        }

        /// <summary>The name over its true form: the colony's own for the god once one of them has heard of it, else what it is seen as.</summary>
        private static string Label(God god) =>
            god.known && !Gods.Name(god).NullOrEmpty() ? Gods.Name(god) : GodShapes.TrueLook(god)?.label ?? "CC_GodM_Woken_Called".Translate().Resolve();

        // ================= Going back =================

        /// <summary>Its true form gone from the land (off the map on its own, or a day on, in a breath): out of the world.</summary>
        internal static void Hourly(GodsOnMapBook b, int now)
        {
            var x = b.woken;
            if (x == null) return;
            if (x.Dead || x.Discarded)
            {
                b.woken = null;
                return;
            }
            bool gone = !x.Spawned;
            if (!gone && now < b.wokenUntil) return;
            b.woken = null;
            var god = Gods.ById(b.wokenGod);
            var map = gone ? null : x.Map;
            try
            {
                if (x.Spawned)
                {
                    FleckMaker.ThrowSmoke(x.DrawPos, x.Map, 4f);
                    x.DeSpawn();
                }
                GodAvatar.Unmake(x);
                if (Find.WorldPawns.Contains(x)) Find.WorldPawns.RemoveAndDiscardPawnViaGC(x);
                else Find.WorldPawns.PassToWorld(x, PawnDiscardDecideMode.Discard);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the sleeper gone back", e);
            }
            map ??= Homes.Seat ?? Homes.All.FirstOrDefault();
            if (god != null && map != null) Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_GodM_Woken_Gone".Translate().Resolve(), null, MessageTypeDefOf.NeutralEvent);
        }
    }
}
