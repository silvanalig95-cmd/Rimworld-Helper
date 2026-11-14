using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// What each god's own presence shows on the map, as its words say (round 2 of docs/gods-next-proposals.md, section C):
    /// at its start (the first look: steam from the rock, the birds leaving, the dark corner, the frogs), over the land
    /// where the player is looking (motes: drifting leaves, ash, spores, frost, shadows, glints, heat haze, silver over the
    /// sleepers), at each of its turns (Sched.RareGodPresence: the beasts lying down, the hearths kept, the frost by the
    /// walls, the heat under the rock, Pip's prank) and on its hour (the croak in the wells, the Old Horn's call, the
    /// dream told at dawn). Someone of the colony who sees it says so once, in one sentence; named only where the colony
    /// is sure whose it is. Motes and sounds of the player's mods are used by name (A RimWorld of Magic's), the game's and
    /// Rimfolk's own otherwise.
    /// </summary>
    public static class GodPresenceShows
    {
        private static readonly Color Green = new Color(0.55f, 0.88f, 0.42f), Ember = new Color(1f, 0.55f, 0.25f), Ash = new Color(0.42f, 0.4f, 0.38f, 0.55f),
            Pale = new Color(0.93f, 0.92f, 0.82f), Amber = new Color(1f, 0.72f, 0.35f), Spark = new Color(0.55f, 0.85f, 1f), Sky = new Color(0.82f, 0.9f, 1f),
            Frost = new Color(0.85f, 0.93f, 1f), Shade = new Color(0.12f, 0.08f, 0.16f), Sun = new Color(1f, 0.93f, 0.6f), Silver = new Color(0.78f, 0.84f, 1f),
            Water = new Color(0.55f, 0.72f, 0.82f, 0.45f), Gold = new Color(1f, 0.84f, 0.35f);

        private static int Now => Find.TickManager.TicksGame;

        // ================= Each tick =================

        internal static void Tick(GameCondition_GodPresence c, Map map, int now)
        {
            if (!c.shown)
            {
                c.shown = true;
                Start(c, map);
                GodPresenceOwn.Forget(map);
            }
            if (map == Find.CurrentMap && (now + map.uniqueID) % MoteEvery(map) == 0) Motes(c, map);
            int lightning = GodPresenceOwnMath.FarLightningEvery(c.Effect, c.Harsh);
            if (lightning > 0 && (now + map.uniqueID * 7) % lightning == 0) FarLightning(map);
            if (Sched.Every(now, Sched.RareTicks, Sched.RareGodPresence))
            {
                using (PassClock.Time(Pass.RareGodPresence)) Rare(c, map, now);
            }
        }

        private static int MoteEvery(Map map) => map.skyManager.CurSkyGlow < 0.4f ? 40 : 60;

        // ================= Its start =================

        private static void Start(GameCondition_GodPresence c, Map map)
        {
            c.startWeather = map.weatherManager?.curWeather;
            c.heldGlow = GodPresenceOwnMath.HeldGlow(c.Effect, GenCelestial.CurCelestialSunGlow(map));
            c.Extended();
            Play(c, map, Center(map));
            switch (c.Effect)
            {
                case PresenceEffect.Growth:
                    Dryads(map, Focus(map), 1500);
                    break;
                case PresenceEffect.WarmStone:
                    Steam(map, 4);
                    break;
                case PresenceEffect.Rot:
                    GodOmens.FairyRing(map, out _, out _);
                    break;
                case PresenceEffect.Hum:
                    foreach (var l in GodOmens.Powered(map, true).InRandomOrder().Take(3).ToList()) GodOmens.Play(map, l, 400, true, false);
                    if (GodPresenceOwnMath.Pranks(c.Effect, c.Harsh)) PrankBegin(c, map);
                    break;
                case PresenceEffect.HighSky:
                    GodOmens.Leave(map, a => GodOmens.Birds(map).Contains(a), 12);
                    if (!c.Harsh && map.skyManager.CurSkyGlow > 0.5f && !GodSky.On(map, SkySign.Vast)) GodSky.Pass(map, SkySign.Vast);
                    break;
                case PresenceEffect.WhiteSilence:
                    try
                    {
                        if (Find.CurrentMap == map) Find.MusicManagerPlay.ForceFadeoutAndSilenceFor(40f, 2f, true);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("gods: the white silence", e);
                    }
                    FrostByWalls(map, 6);
                    break;
                case PresenceEffect.Dark:
                    Lamps(map);
                    DarkCorner(c, map);
                    break;
                case PresenceEffect.LongNoon:
                    if (GenCelestial.CurCelestialSunGlow(map) > 0.5f) WeatherEvent_StillSun.Begin(map, Math.Min(c.TicksLeft, GenDate.TicksPerHour * 3));
                    break;
                case PresenceEffect.Rain:
                    WaterAtTheWell(map);
                    Frogs(map, c.Harsh ? 4 : 3);
                    Croak(c, map, false);
                    break;
                case PresenceEffect.Glint:
                    Beetle(map);
                    CoinTrail(map);
                    break;
            }
        }

        // ================= Over the land, where the player looks =================

        private static IntVec3 InView(Map map, Predicate<IntVec3> ok, int tries = 12)
        {
            var rect = Find.CameraDriver.CurrentViewRect.ExpandedBy(1).ClipInsideMap(map);
            if (rect.Area <= 0) return IntVec3.Invalid;
            for (int i = 0; i < tries; i++)
            {
                var c = rect.RandomCell;
                if (c.InBounds(map) && !c.Fogged(map) && (ok == null || ok(c))) return c;
            }
            return IntVec3.Invalid;
        }

        private static Def Mote(GameCondition_GodPresence c)
        {
            var list = c.Own?.Motes;
            return list != null && list.Count > 0 && Rand.Chance(0.6f) ? list.RandomElement() : GodFx.MoteDef;
        }

        private static void Throw(GameCondition_GodPresence c, Map map, Vector3 at, Color color, float angle, float speed, float scale = 0.45f, float solid = 1.2f) =>
            GodFx.Throw(Mote(c), at, map, scale, color, angle, speed, Rand.Range(-30f, 30f), solid);

        private static void Motes(GameCondition_GodPresence c, Map map)
        {
            bool night = map.skyManager.CurSkyGlow < 0.35f;
            switch (c.Effect)
            {
                case PresenceEffect.Growth:
                {
                    // Leaves and seeds drift toward one place on no wind.
                    var to = Focus(map).ToVector3Shifted();
                    for (int i = 0; i < 3; i++)
                    {
                        var cell = InView(map, x => x.GetPlant(map) != null);
                        if (!cell.IsValid) continue;
                        var from = cell.ToVector3Shifted();
                        Throw(c, map, from, Green, Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg, Rand.Range(0.6f, 1.2f), Rand.Range(0.25f, 0.4f), 1.8f);
                    }
                    break;
                }
                case PresenceEffect.WarmStone:
                {
                    var foot = InView(map, x => x.Standable(map) && !x.Roofed(map) && NextToRock(map, x), 20);
                    if (foot.IsValid)
                    {
                        FleckMaker.ThrowAirPuffUp(foot.ToVector3Shifted(), map);
                        if (Rand.Chance(0.4f)) FleckMaker.ThrowSmoke(foot.ToVector3Shifted(), map, 1.2f);
                    }
                    var ash = InView(map, x => !x.Roofed(map));
                    if (ash.IsValid) FleckMaker.ThrowDustPuffThick(ash.ToVector3Shifted(), map, Rand.Range(0.6f, 1f), Ash);
                    var ore = InView(map, x => x.GetEdifice(map) is Mineable m && m.def.building != null && m.def.building.isResourceRock, 20);
                    if (ore.IsValid) GodFx.Throw(GodFx.MoteDef, ore.ToVector3Shifted(), map, 0.3f, Gold, 0f, 0.05f, 0f, 0.5f);
                    break;
                }
                case PresenceEffect.Grazing:
                {
                    var calm = map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Animal && p.CurJobDef?.defName == GodReactions.LieJob).InRandomOrder().FirstOrDefault();
                    if (calm != null) Throw(c, map, calm.DrawPos + new Vector3(0f, 0f, 0.4f), Pale, 0f, 0.3f, 0.3f);
                    break;
                }
                case PresenceEffect.Rot:
                    for (int i = 0; i < 3; i++)
                    {
                        var cell = InView(map, x => !x.Roofed(map));
                        if (cell.IsValid)
                            Throw(c, map, cell.ToVector3Shifted() + new Vector3(Rand.Value, 0f, Rand.Value), night ? new Color(0.75f, 0.95f, 0.85f) : Pale, Rand.Range(150f, 210f), Rand.Range(0.1f, 0.25f),
                                Rand.Range(0.2f, 0.35f), 2.2f);
                    }
                    break;
                case PresenceEffect.Warmth:
                {
                    var fire = Hearths(map).Where(h => Find.CameraDriver.CurrentViewRect.Contains(h.Position)).InRandomOrder().FirstOrDefault();
                    if (fire != null) Throw(c, map, fire.TrueCenter() + new Vector3(Rand.Range(-0.3f, 0.3f), 0f, 0.3f), Amber, Rand.Range(-15f, 15f), Rand.Range(0.3f, 0.6f), 0.3f);
                    break;
                }
                case PresenceEffect.Hum:
                {
                    var machine = GodOmens.Powered(map, true).Concat(map.listerBuildings.allBuildingsColonist.OfType<ThingWithComps>().Where(b => b.GetComp<CompPowerTrader>()?.PowerOn == true))
                        .Where(b => Find.CameraDriver.CurrentViewRect.Contains(b.Position)).InRandomOrder().FirstOrDefault();
                    if (machine != null)
                    {
                        FleckMaker.ThrowMicroSparks(machine.TrueCenter(), map);
                        Throw(c, map, machine.TrueCenter(), Spark, 0f, 0.4f, 0.3f, 0.6f);
                    }
                    break;
                }
                case PresenceEffect.HighSky:
                {
                    // Feathered cloud drifting very high: pale streaks crossing slowly.
                    var cell = InView(map, x => !x.Roofed(map));
                    if (cell.IsValid) GodFx.Throw(GodFx.MoteDef, cell.ToVector3Shifted(), map, Rand.Range(0.8f, 1.3f), new Color(Sky.r, Sky.g, Sky.b, 0.5f), 90f, Rand.Range(0.4f, 0.7f), 0f, 2.5f);
                    break;
                }
                case PresenceEffect.WhiteSilence:
                    for (int i = 0; i < 3; i++)
                    {
                        var cell = InView(map, x => !x.Roofed(map));
                        if (cell.IsValid) Throw(c, map, cell.ToVector3Shifted(), Frost, Rand.Range(160f, 200f), Rand.Range(0.1f, 0.2f), Rand.Range(0.2f, 0.3f), 2f);
                    }
                    break;
                case PresenceEffect.Dark:
                {
                    var corner = DarkCell(map);
                    if (corner.IsValid && Find.CameraDriver.CurrentViewRect.ExpandedBy(6).Contains(corner))
                        for (int i = 0; i < 2; i++)
                            GodFx.Throw(Mote(c), (corner + GenRadial.RadialPattern[Rand.Range(0, 30)]).ToVector3Shifted(), map, Rand.Range(0.6f, 1f), Shade, Rand.Range(-20f, 20f), 0.2f, 0f, 1.6f);
                    break;
                }
                case PresenceEffect.LongNoon:
                {
                    if (map.skyManager.CurSkyGlow < 0.5f) break;
                    var cell = InView(map, x => !x.Roofed(map) && x.Standable(map));
                    if (!cell.IsValid) break;
                    FleckMaker.ThrowHeatGlow(cell, map, Rand.Range(0.8f, 1.2f));
                    if (Rand.Chance(0.5f)) FleckMaker.ThrowAirPuffUp(cell.ToVector3Shifted(), map);
                    break;
                }
                case PresenceEffect.MoonSleep:
                {
                    if (!night) break;
                    // Silver over the sleepers (healing in her light), and a shimmer on still water at the full moon.
                    var sleeper = map.mapPawns.FreeColonistsSpawned.Where(p => !p.Awake() && Find.CameraDriver.CurrentViewRect.Contains(p.Position)).InRandomOrder().FirstOrDefault();
                    if (sleeper != null) Throw(c, map, sleeper.DrawPos + new Vector3(Rand.Range(-0.3f, 0.3f), 0f, 0.4f), Silver, 0f, 0.15f, 0.3f, 1.4f);
                    if (GodOmens.MoonTonight == GodsOnMapMath.MoonFace.Full)
                    {
                        var water = InView(map, x => x.GetTerrain(map)?.IsWater == true, 16);
                        if (water.IsValid) GodFx.Throw(GodFx.MoteDef, water.ToVector3Shifted(), map, 0.5f, Silver, 90f, 0.05f, 0f, 1f);
                    }
                    break;
                }
                case PresenceEffect.Rain:
                {
                    var wet = InView(map, x => Wet(map, x), 16);
                    if (wet.IsValid) FleckMaker.WaterRipple(wet.ToVector3Shifted(), map, Rand.Range(0.8f, 1.4f));
                    break;
                }
                case PresenceEffect.Glint:
                {
                    var coins = map.listerThings.ThingsOfDef(ThingDefOf.Silver).Concat(map.listerThings.ThingsOfDef(ThingDefOf.Gold))
                        .Where(t => t.Spawned && Find.CameraDriver.CurrentViewRect.Contains(t.Position)).InRandomOrder().FirstOrDefault();
                    if (coins != null) GodFx.Throw(GodFx.MoteDef, coins.DrawPos + new Vector3(Rand.Range(-0.3f, 0.3f), 0f, Rand.Range(-0.2f, 0.3f)), map, 0.3f, Gold, 0f, 0.05f, 0f, 0.4f);
                    break;
                }
            }
        }

        // ================= Its turns =================

        private static void Rare(GameCondition_GodPresence c, Map map, int now)
        {
            var god = GodPresenceOwn.GodOf(c);
            // Held while its god walks the land.
            if (GodPresenceOwnMath.HeldWhileHere(c.Effect))
            {
                bool here = GodPresenceOwn.Here(god, map) != null;
                int left = GodPresenceOwnMath.Held(c.TicksLeft, here);
                if (left != c.TicksLeft)
                {
                    c.TicksLeft = left;
                    c.Extended();
                }
            }
            if (!c.said && c.TicksPassed >= 300) c.said = Notice(c, map);
            float s = c.Strength;
            bool h = c.Harsh;
            switch (c.Effect)
            {
                case PresenceEffect.WarmStone:
                {
                    // Heat from below in the rooms under the rock, and the snow off the rock.
                    float heat = GodPresenceOwnMath.UnderRockHeat(c.Effect, s, h);
                    var rooms = new HashSet<Room>();
                    foreach (var p in map.mapPawns.FreeColonistsSpawned)
                    {
                        if (p.Position.GetRoof(map) != RoofDefOf.RoofRockThick) continue;
                        var room = p.GetRoom();
                        if (room == null || room.PsychologicallyOutdoors || !rooms.Add(room)) continue;
                        GenTemperature.PushHeat(p.Position, map, heat);
                    }
                    float melt = GodPresenceOwnMath.SnowMelt(c.Effect, s);
                    for (int i = 0; i < 40; i++)
                    {
                        var cell = CellFinder.RandomCell(map);
                        float d = map.snowGrid.GetDepth(cell);
                        if (d > 0f && NextToRock(map, cell)) map.snowGrid.SetDepth(cell, Mathf.Max(0f, d - melt * 4f));
                    }
                    break;
                }
                case PresenceEffect.Grazing:
                    Calm(c, map, god);
                    break;
                case PresenceEffect.Warmth:
                    KeepHearths(c, map, s, h);
                    FollowTheGuest(map, god);
                    break;
                case PresenceEffect.Hum:
                    PrankStep(c, map, now);
                    if (Rand.Chance(0.3f))
                    {
                        var l = GodOmens.Powered(map, true).RandomElementWithFallback();
                        if (l != null) GodOmens.Play(map, l, h ? 900 : 300, !h, false);
                    }
                    break;
                case PresenceEffect.HighSky:
                    if (!h) Watched(map);
                    break;
                case PresenceEffect.WhiteSilence:
                    FrostByWalls(map, 3);
                    break;
                case PresenceEffect.Dark:
                    if (!GodSky.On(map, SkySign.Dark)) DarkCorner(c, map);
                {
                    int hour = GenLocalDate.HourOfDay(map);
                    if (!c.whispered && (hour >= 20 || hour <= 4)) c.whispered = Whisper(c, map);
                }
                    break;
            }
        }

        /// <summary>Someone of the colony who sees what the presence does says so, once (its notice line); true when someone did.</summary>
        private static bool Notice(GameCondition_GodPresence c, Map map)
        {
            var at = Center(map);
            Pawn who = c.Effect switch
            {
                PresenceEffect.Dark => GodOmens.Witness(map, DarkCell(map).IsValid ? DarkCell(map) : at, 25f),
                PresenceEffect.Grazing => GodPresenceOwn.Here(GodPresenceOwn.GodOf(c), map) is Pawn g ? GodOmens.Witness(map, g.Position, 30f) : null,
                PresenceEffect.Warmth => map.mapPawns.FreeColonistsSpawned.Where(GodOmens.Awake).FirstOrDefault(p => Hearths(map).Any(f => f.Position.InHorDistOf(p.Position, 8f))),
                PresenceEffect.MoonSleep => map.mapPawns.FreeColonistsSpawned.Where(p => GodOmens.Awake(p) && !p.Position.Roofed(map)).RandomElementWithFallback(),
                PresenceEffect.WarmStone => map.mapPawns.FreeColonistsSpawned.Where(GodOmens.Awake).OrderBy(p => p.Position.GetRoof(map) == RoofDefOf.RoofRockThick ? 0 : 1).FirstOrDefault(),
                // Hush's silence: nobody says a word while it lasts; it is told once it has passed, if at all.
                PresenceEffect.WhiteSilence => null,
                _ => map.mapPawns.FreeColonistsSpawned.Where(p => GodOmens.Awake(p) && !p.Position.Roofed(map)).RandomElementWithFallback()
                     ?? map.mapPawns.FreeColonistsSpawned.Where(GodOmens.Awake).RandomElementWithFallback()
            };
            if (c.Effect == PresenceEffect.WhiteSilence) return true;
            if (who == null) return false;
            GodPresenceOwn.Say(who, c, "notice:" + c.Effect.ToString().ToLowerInvariant());
            return true;
        }

        // ================= Its hour =================

        internal static void Hourly(GameCondition_GodPresence c, Map map, int now)
        {
            int hour = GenLocalDate.HourOfDay(map);
            switch (c.Effect)
            {
                case PresenceEffect.Grazing:
                {
                    var horn = GodPresenceOwn.Here(GodPresenceOwn.GodOf(c), map);
                    if (horn != null && Rand.Chance(GodPresenceOwnMath.CallChance)) GodSounds.Play(GodSounds.Sound("Pawn_Thrumbo_Call"), map, horn.Position);
                    break;
                }
                case PresenceEffect.Rain:
                    if (Rand.Chance(GodPresenceOwnMath.CroakChance)) Croak(c, map, true);
                    break;
                case PresenceEffect.MoonSleep:
                    if (c.dreamDay != GenLocalDate.DayOfYear(map) && GodPresenceOwnMath.TrueDreamNow(c.Effect, c.Harsh, GodOmens.MoonTonight == GodsOnMapMath.MoonFace.Full, hour)
                        && TrueDream(c, map, now))
                        c.dreamDay = GenLocalDate.DayOfYear(map);
                    break;
                case PresenceEffect.WarmStone:
                    if (c.dreamDay != GenLocalDate.DayOfYear(map) && GodPresenceOwnMath.WarmVoiceNow(c.Effect, hour) && WarmVoice(c, map)) c.dreamDay = GenLocalDate.DayOfYear(map);
                    break;
            }
        }

        // ================= Its end =================

        internal static void Ended(GameCondition_GodPresence c, Map map)
        {
            GodPresenceOwn.Forget(map);
            if (c.Effect == PresenceEffect.Dark) Lamps(map);
            if (c.prankPawn != null || c.prankDoor != null) PrankEnd(c, false);
            // Nobody says a word under Hush's silence: it is told once it has lifted.
            if (c.Effect == PresenceEffect.WhiteSilence && c.TicksPassed >= GenDate.TicksPerHour)
            {
                var who = map.mapPawns.FreeColonistsSpawned.Where(GodOmens.Awake).RandomElementWithFallback();
                if (who != null) GodPresenceOwn.Say(who, c, "notice:whitesilence");
            }
        }

        // ================= Hollin's =================

        /// <summary>Where the green leans: the anima tree, else the oldest tree near the colony, else the colony's middle.</summary>
        internal static IntVec3 Focus(Map map)
        {
            var anima = GodOmens.AnimaTrees(map).FirstOrDefault();
            if (anima != null) return anima.Position;
            var center = Center(map);
            Plant oldest = null;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant))
                if (t is Plant p && p.def.plant != null && p.def.plant.IsTree && p.Position.InHorDistOf(center, 40f) && (oldest == null || p.Age > oldest.Age))
                    oldest = p;
            return oldest?.Position ?? center;
        }

        private static void Dryads(Map map, IntVec3 toward, int ticks)
        {
            var look = DefDatabase<JobDef>.GetNamedSilentFail(GodReactions.BeholdJob);
            if (look == null) return;
            foreach (var d in GodOmens.Dryads(map))
            {
                try
                {
                    var j = JobMaker.MakeJob(look, toward);
                    j.count = ticks;
                    j.expiryInterval = ticks + 200;
                    d.jobs.StartJob(j, JobCondition.InterruptForced);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: a dryad standing still", e);
                }
            }
        }

        /// <summary>A tree felled under the green leaning: its feller felt the axe go wrong (a memory, more in her wrath).</summary>
        internal static void TreeFelled(Plant tree, Pawn by)
        {
            if (tree?.def.plant == null || !tree.def.plant.IsTree || by == null || !by.RaceProps.Humanlike) return;
            var c = GodPresenceOwn.On(by.Map ?? tree.Map);
            if (c == null || c.Effect != PresenceEffect.Growth) return;
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail(c.Harsh ? "Rimfolk_GodPO_AxeWrongWrath" : "Rimfolk_GodPO_AxeWrong");
            if (def != null) CouncilGameComponent.GiveThought(by, def);
            if (Rand.Chance(0.5f)) GodPresenceOwn.Say(by, c, "axe");
        }

        // ================= Smoulder's =================

        private static bool NextToRock(Map map, IntVec3 c)
        {
            foreach (var n in GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)))
                if (n.InBounds(map) && n.GetEdifice(map)?.def.building?.isNaturalRock == true)
                    return true;
            return false;
        }

        /// <summary>His breath: steam from the geysers, else from the ground at the foot of the rock, and warm air with it.</summary>
        private static void Steam(Map map, int most)
        {
            var vents = map.listerThings.ThingsOfDef(ThingDefOf.SteamGeyser).Select(g => g.Position).Take(most).ToList();
            for (int i = 0; i < 80 && vents.Count < most; i++)
            {
                var c = CellFinder.RandomCell(map);
                if (c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && NextToRock(map, c)) vents.Add(c);
            }
            foreach (var v in vents)
            {
                for (int i = 0; i < 5; i++) FleckMaker.ThrowAirPuffUp(v.ToVector3Shifted() + new Vector3(Rand.Range(-0.5f, 0.5f), 0f, Rand.Range(-0.5f, 0.5f)), map);
                FleckMaker.ThrowSmoke(v.ToVector3Shifted(), map, 2f);
                GenTemperature.PushHeat(v, map, 20f);
            }
            if (vents.Count > 0) GodSounds.Play(GodSounds.Sound("GeyserSpray"), map, vents[0], 0.6f);
        }

        /// <summary>Someone who slept under the mountain wakes and says a warm voice spoke in their dream (the dream book has it).</summary>
        private static bool WarmVoice(GameCondition_GodPresence c, Map map)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp?.dreams == null) return false;
            var p = map.mapPawns.FreeColonistsSpawned.Where(x => x.Spawned && GodOmens.Awake(x) && comp.dreams.WokeFromNight(x) && x.Position.GetRoof(map) == RoofDefOf.RoofRockThick)
                .RandomElementWithFallback();
            string line = GodPresenceOwn.Line(GodPresenceOwn.DreamPool, p, c, "warmvoice");
            if (line.NullOrEmpty()) return false;
            Speech.Say(p, line);
            GodOmens.Dreamt(comp, p, line);
            var god = GodPresenceOwn.GodOf(c);
            if (god != null) GodsOnMapWords.Saw(god, map, p.Position, GodActKind.Omen, p, "CC_GodPO_Saw_WarmVoice");
            return true;
        }

        // ================= Bellwether's =================

        /// <summary>The wild beasts near him stop and lie down facing him; one that had turned on anyone near him calms.</summary>
        private static void Calm(GameCondition_GodPresence c, Map map, God god)
        {
            if (!GodPresenceOwnMath.CalmsBeasts(c.Effect, c.Harsh)) return;
            var horn = GodPresenceOwn.Here(god, map);
            if (horn == null) return;
            var lie = DefDatabase<JobDef>.GetNamedSilentFail(GodReactions.LieJob);
            int n = 0;
            foreach (var a in map.mapPawns.AllPawnsSpawned.Where(p => p != horn && p.RaceProps.Animal && !p.Dead && !p.Downed && p.Position.InHorDistOf(horn.Position, 30f))
                         .OrderBy(p => p.Position.DistanceToSquared(horn.Position)).ToList())
            {
                try
                {
                    if (a.InMentalState && a.MentalStateDef != null && a.MentalStateDef.IsAggro)
                    {
                        a.mindState.mentalStateHandler.CurState.RecoverFromState();
                        continue;
                    }
                    if (n >= GodPresenceOwnMath.CalmPerPass || lie == null || a.Faction != null || GodAvatar.Is(a) || a.CurJobDef?.defName == GodReactions.LieJob) continue;
                    if (a.CurJob != null && !a.CurJob.def.casualInterruptible) continue;
                    var j = JobMaker.MakeJob(lie, horn);
                    j.count = Rand.Range(600, 1100);
                    j.expiryInterval = j.count + 120;
                    a.jobs.StartJob(j, JobCondition.InterruptForced);
                    n++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: a beast calmed", e);
                }
            }
        }

        // ================= Ennu's =================

        /// <summary>The colony's lit hearths, fires and braziers (what burns fuel and gives heat).</summary>
        private static List<ThingWithComps> Hearths(Map map) =>
            map.listerBuildings.allBuildingsColonist.OfType<ThingWithComps>()
                .Where(b => b.Spawned && b.GetComp<CompRefuelable>() is CompRefuelable r && r.HasFuel && (b.GetComp<CompHeatPusher>() != null || b.GetComp<CompFireOverlay>() != null)).ToList();

        /// <summary>The hearths burn longer (some of what they burn kept) and warmer (a little more heat).</summary>
        private static void KeepHearths(GameCondition_GodPresence c, Map map, float s, bool h)
        {
            float kept = GodPresenceOwnMath.HearthKept(c.Effect, s, h);
            if (kept <= 0f) return;
            foreach (var b in Hearths(map))
            {
                var r = b.GetComp<CompRefuelable>();
                float burnt = r.Props.fuelConsumptionRate / GenDate.TicksPerDay * Sched.RareTicks;
                if (burnt > 0f && !r.IsFull) r.Refuel(burnt * kept);
                GenTemperature.PushHeat(b.Position, map, 4f * s);
            }
        }

        /// <summary>One of the colony's animals near the Guest follows it a while.</summary>
        private static void FollowTheGuest(Map map, God god)
        {
            var guest = GodPresenceOwn.Here(god, map);
            if (guest == null || !Rand.Chance(0.25f)) return;
            var a = map.mapPawns.SpawnedColonyAnimals.Where(x => !x.Downed && x.Position.InHorDistOf(guest.Position, 20f) && (x.CurJob == null || x.CurJob.def.casualInterruptible)
                                                               && x.CurJobDef != JobDefOf.Follow).RandomElementWithFallback();
            if (a == null) return;
            try
            {
                var j = JobMaker.MakeJob(JobDefOf.Follow, guest);
                j.expiryInterval = 1200;
                j.checkOverrideOnExpire = true;
                a.jobs.StartJob(j, JobCondition.InterruptForced);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: an animal following the guest", e);
            }
        }

        // ================= Pip's prank =================

        internal static Pawn PrankPawn;
        internal static Building_Door PrankDoor;

        /// <summary>Pip picks one of the colony's people and a door near them: it won't open for them until someone else opens it.</summary>
        private static void PrankBegin(GameCondition_GodPresence c, Map map)
        {
            var doors = map.listerBuildings.allBuildingsColonist.OfType<Building_Door>().Where(d => d.Spawned && !d.FreePassage && !d.Open).ToList();
            if (doors.Count == 0) return;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(x => GodOmens.Awake(x) && Worship.Ours(x)).InRandomOrder())
            {
                if (MustPass(p, map)) continue;
                var door = doors.Where(d => d.Position.InHorDistOf(p.Position, 18f) && d.PawnCanOpen(p)).OrderBy(d => d.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                if (door == null) continue;
                var side = GenAdj.CellsAdjacentCardinal(door).Where(x => x.InBounds(map) && x.Standable(map) && p.CanReach(x, PathEndMode.OnCell, Danger.None))
                    .OrderBy(x => x.DistanceToSquared(p.Position)).FirstOrDefault();
                if (!side.IsValid || side == default) continue;
                c.prankPawn = p;
                c.prankDoor = door;
                c.prankUntil = Now + GodPresenceOwnMath.PrankTicks;
                c.prankSaid = false;
                PrankPawn = p;
                PrankDoor = door;
                // They go to the door, and it won't open for them.
                if (p.CurJob == null || p.CurJob.def.casualInterruptible)
                {
                    var go = JobMaker.MakeJob(JobDefOf.Goto, side);
                    go.expiryInterval = 900;
                    p.jobs.StartJob(go, JobCondition.InterruptForced, null, true);
                }
                return;
            }
        }

        /// <summary>The prank's victim must be let through: drafted, in a state, hungry, hurt, or danger on the map.</summary>
        private static bool MustPass(Pawn p, Map map) =>
            p == null || !p.Spawned || p.Downed || p.Drafted || p.InMentalState || (p.needs?.food?.CurLevelPercentage ?? 1f) < 0.25f || p.health.HasHediffsNeedingTend()
            || MapFacts.ThreatOn(map);

        private static void PrankStep(GameCondition_GodPresence c, Map map, int now)
        {
            var p = c.prankPawn;
            var d = c.prankDoor;
            if (p == null || d == null) return;
            bool openedByOther = d.Spawned && d.Open && !p.Position.InHorDistOf(d.Position, 1.5f);
            if (!d.Spawned || p.Map != map || GodPresenceOwnMath.PrankEnds(now, c.prankUntil, openedByOther, MustPass(p, map)))
            {
                PrankEnd(c, openedByOther && p.Spawned && p.Map == map);
                return;
            }
            if (!c.prankSaid && p.Position.InHorDistOf(d.Position, 1.9f) && !d.Open)
            {
                p.rotationTracker?.FaceTarget(d);
                c.prankSaid = GodPresenceOwn.Say(p, c, "prank");
            }
        }

        /// <summary>Someone else opened the prank's door: it is over (called from the door's hook).</summary>
        internal static void OpenedBy(Building_Door door, Pawn opener)
        {
            if (door == null || door != PrankDoor || opener == null || opener == PrankPawn) return;
            var c = GodPresenceOwn.On(door.Map);
            if (c != null && c.prankDoor == door) PrankEnd(c, true);
            else
            {
                PrankPawn = null;
                PrankDoor = null;
            }
        }

        private static void PrankEnd(GameCondition_GodPresence c, bool laugh)
        {
            var p = c.prankPawn;
            var d = c.prankDoor;
            c.prankPawn = null;
            c.prankDoor = null;
            c.prankUntil = -1;
            PrankPawn = null;
            PrankDoor = null;
            if (laugh && c.prankSaid && p != null && d != null && p.Spawned && p.Position.InHorDistOf(d.Position, 10f)) GodPresenceOwn.Say(p, c, "prankend");
        }

        /// <summary>The prank as it was saved (a game loaded mid-prank).</summary>
        internal static void PrankLoaded(GameCondition_GodPresence c)
        {
            if (c?.prankPawn == null || c.prankDoor == null) return;
            PrankPawn = c.prankPawn;
            PrankDoor = c.prankDoor;
        }

        // ================= Cirrus's =================

        /// <summary>Lightning flickering far off over the hills, and thunder, with no rain.</summary>
        private static void FarLightning(Map map)
        {
            if (map != Find.CurrentMap) return;
            var edge = CellFinder.RandomEdgeCell(map);
            FleckMaker.ThrowLightningGlow(edge.ToVector3Shifted(), map, Rand.Range(6f, 10f));
            GodSounds.Play(GodSounds.Sound("Thunder_OffMap"), map, edge, 0.6f);
        }

        /// <summary>Those looking up at the sky feel watched from very high (a memory, once).</summary>
        private static void Watched(Map map)
        {
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_GodPO_FeltWatched");
            if (def == null) return;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                string job = p.CurJobDef?.defName;
                if (job != "Skygaze" && job != "VSIE_Skygaze") continue;
                if (p.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(def) != null) continue;
                CouncilGameComponent.GiveThought(p, def);
            }
        }

        // ================= Hush's =================

        /// <summary>Frost by the colony's walls out of doors (where it is cold enough), else a cold mist.</summary>
        private static void FrostByWalls(Map map, int most)
        {
            bool cold = map.mapTemperature.OutdoorTemp < 4f;
            int n = 0;
            foreach (var w in map.listerBuildings.allBuildingsColonist.Where(b => b.def.building != null && !b.def.building.isNaturalRock && b.def.fillPercent >= 0.99f && b.def.size == IntVec2.One)
                         .InRandomOrder().Take(40))
            {
                if (n >= most) break;
                var cell = w.Position + GenAdj.CardinalDirections[Rand.Range(0, 4)];
                if (!cell.InBounds(map) || cell.Roofed(map) || !cell.Standable(map)) continue;
                if (cold) GodOmens.Rime(map, cell);
                else FleckMaker.ThrowAirPuffUp(cell.ToVector3Shifted(), map);
                n++;
            }
        }

        // ================= Undertow's =================

        /// <summary>The lamps re-lit at their reach under the dark (shorter while it lasts, as before after).</summary>
        private static void Lamps(Map map)
        {
            try
            {
                foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
                {
                    var g = t.TryGetComp<CompGlower>();
                    if (g == null || !g.Glows) continue;
                    map.glowGrid.DeRegisterGlower(g);
                    map.glowGrid.RegisterGlower(g);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the lamps under the dark", e);
            }
        }

        /// <summary>The corner of the colony the dark keeps: the home ground's cell farthest from its middle, within reach of its people.</summary>
        private static IntVec3 DarkCell(Map map)
        {
            var home = map.areaManager.Home;
            var center = Center(map);
            IntVec3 best = IntVec3.Invalid;
            float bestD = -1f;
            foreach (var c in home.ActiveCells)
            {
                float d = c.DistanceToSquared(center);
                if (d > bestD && d < 45f * 45f)
                {
                    best = c;
                    bestD = d;
                }
            }
            return best;
        }

        private static void DarkCorner(GameCondition_GodPresence c, Map map)
        {
            var at = DarkCell(map);
            if (at.IsValid) GodSky.Stay(map, SkySign.Dark, at, Math.Max(GenDate.TicksPerHour, c.TicksLeft), c.Harsh ? 9f : 7f);
        }

        /// <summary>The most desperate of the colony hears a sweet voice in the dark, and says so (once).</summary>
        private static bool Whisper(GameCondition_GodPresence c, Map map)
        {
            var p = GodOmens.Desperate(map);
            return p != null && GodPresenceOwn.Say(p, c, "whisper");
        }

        // ================= Murmur's =================

        private static bool Wet(Map map, IntVec3 c)
        {
            var t = c.GetTerrain(map);
            if (t == null) return false;
            if (t.IsWater && t.passability != Traversability.Impassable) return true;
            string n = t.defName;
            return n.IndexOf("Marsh", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Mud", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>A plant stands on wet ground (a marsh, mud, the edge of water): Murmur's presence grows it faster.</summary>
        internal static bool WetGround(Map map, IntVec3 c) => c.InBounds(map) && Wet(map, c);

        /// <summary>Water stands at the colony's well (else on its wet ground): a ring of puddles.</summary>
        private static void WaterAtTheWell(Map map)
        {
            var at = GodOmens.Wet(map);
            if (!at.IsValid) return;
            var water = ThingDefOf.Filth_Water;
            foreach (var (dx, dz) in GodPresenceMath.Ring(1))
            {
                var c = at + new IntVec3(dx, 0, dz);
                if (c.InBounds(map) && c.Standable(map) && water != null && Rand.Chance(0.6f)) FilthMaker.TryMakeFilth(c, map, water);
            }
        }

        /// <summary>Frogs out on the wet ground (Odyssey's bullfrogs, where the game has them), wild, going their own way after.</summary>
        private static void Frogs(Map map, int count)
        {
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Bullfrog");
            if (kind == null) return;
            var center = Center(map);
            var cells = GenRadial.RadialCellsAround(center, 45f, true).Where(c => c.InBounds(map) && !c.Fogged(map) && c.Standable(map) && Wet(map, c)).Take(60).ToList();
            if (cells.Count == 0) return;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    var frog = PawnGenerator.GeneratePawn(kind);
                    GenSpawn.Spawn(frog, cells.RandomElement(), map);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: frogs on the wet ground", e);
                    return;
                }
            }
        }

        /// <summary>The deep croak from the wells and the wet ground, felt in the chest; someone near says so.</summary>
        private static void Croak(GameCondition_GodPresence c, Map map, bool say)
        {
            var at = GodOmens.Wet(map);
            if (!at.IsValid) at = Center(map);
            try
            {
                if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(0.12f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the croak", e);
            }
            GodSounds.Play(GodSounds.Sound("Pawn_Frog_Call") ?? GodSounds.Sound("AG_Pawn_Frog_Call"), map, at, 1f);
            for (int i = 0; i < 6; i++)
            {
                var cell = at + GenRadial.RadialPattern[Rand.Range(0, 20)];
                if (cell.InBounds(map)) FleckMaker.WaterRipple(cell.ToVector3Shifted(), map, Rand.Range(0.8f, 1.6f));
            }
            if (!say) return;
            var p = map.mapPawns.FreeColonistsSpawned.Where(x => GodOmens.Awake(x) && x.Position.InHorDistOf(at, 35f)).OrderBy(x => x.Position.DistanceToSquared(at)).FirstOrDefault();
            if (p != null) GodPresenceOwn.Say(p, c, "croak");
        }

        // ================= Selene's =================

        /// <summary>
        /// A true dream at the full moon: someone who slept the night wakes and says what they dreamt, and it is true (a raid
        /// coming, visitors on their way, teeth on the land, the stores going soft, someone sadder than they let on), or only
        /// the moon. In the dream book and their journal.
        /// </summary>
        private static bool TrueDream(GameCondition_GodPresence c, Map map, int now)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp?.dreams == null) return false;
            var p = map.mapPawns.FreeColonistsSpawned.Where(x => x.Spawned && GodOmens.Awake(x) && comp.dreams.WokeFromNight(x)).RandomElementWithFallback();
            if (p == null) return false;
            bool raid = false, visitors = false;
            try
            {
                var queue = Find.Storyteller?.incidentQueue;
                if (queue != null)
                    foreach (QueuedIncident q in queue)
                    {
                        var fi = q?.FiringIncident;
                        if (fi?.def == null || fi.parms?.target != map || q.FireTick - now > GodPresenceOwnMath.TrueDreamDays * GenDate.TicksPerDay) continue;
                        if (fi.def.category == IncidentCategoryDefOf.ThreatBig || fi.def.category == IncidentCategoryDefOf.ThreatSmall) raid = true;
                        else if (fi.def.defName.IndexOf("Visitor", StringComparison.Ordinal) >= 0 || fi.def.defName.IndexOf("Trader", StringComparison.Ordinal) >= 0
                                 || fi.def.defName.IndexOf("Traveler", StringComparison.Ordinal) >= 0)
                            visitors = true;
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: what a true dream foresees", e);
            }
            var beast = map.mapPawns.AllPawnsSpawned.Where(a => a.RaceProps.Animal && a.Faction == null && !a.Dead && (a.RaceProps.predator && a.BodySize >= 0.8f || a.InAggroMentalState))
                .OrderByDescending(a => a.BodySize).FirstOrDefault();
            bool rot = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).Any(t => t.Spawned && t.IsInAnyStorage() && t.TryGetComp<CompRottable>() is CompRottable r
                                                                                               && r.Active && r.Stage == RotStage.Fresh && r.RotProgressPct > 0.5f
                                                                                               && t.AmbientTemperature > 0f);
            var sad = map.mapPawns.FreeColonistsSpawned.Where(x => x != p && (x.needs?.mood?.CurLevelPercentage ?? 1f) < 0.3f).OrderBy(x => x.needs.mood.CurLevelPercentage).FirstOrDefault();
            string tag = GodPresenceOwnMath.TrueDream(raid, visitors, beast != null, rot, sad != null);
            string dir = beast != null ? Direction(map, beast.Position) : "";
            // apart: a dream of someone told aloud at breakfast; the one dreamt of takes no part.
            string line = GodPresenceOwn.Line(GodPresenceOwn.DreamPool, p, c, tag, (sad?.LabelShort ?? "").Named("OTHER"), dir.Named("DIR"),
                (beast?.KindLabel ?? "").Named("BEAST"));
            if (line.NullOrEmpty()) return false;
            Speech.Say(p, line);
            GodOmens.Dreamt(comp, p, line);
            var god = GodPresenceOwn.GodOf(c);
            if (god != null) GodsOnMapWords.Saw(god, map, p.Position, GodActKind.Omen, p, "CC_GodPO_Saw_TrueDream", line.Named("DREAM"));
            return true;
        }

        private static string Direction(Map map, IntVec3 c)
        {
            var d = c - Center(map);
            string k = Math.Abs(d.x) > Math.Abs(d.z) ? d.x > 0 ? "East" : "West" : d.z > 0 ? "North" : "South";
            return ("CC_GodPO_Dir_" + k).Translate().Resolve();
        }

        // ================= Ducat's =================

        private static void Beetle(Map map)
        {
            var table = GodOmens.Table(map);
            if (table == null) return;
            var mid = table.TrueCenter();
            var along = table.Rotation.IsHorizontal ? new Vector3(0f, 0f, table.def.size.x * 0.45f) : new Vector3(table.def.size.x * 0.45f, 0f, 0f);
            GodSky.Crawl(map, mid - along, mid + along, 22f);
        }

        /// <summary>A few single coins from one of the stores lie in a line toward the nearest edge of the land, as if walking off.</summary>
        private static void CoinTrail(Map map)
        {
            var stack = map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => t.Spawned && t.IsInAnyStorage() && GodPresenceOwnMath.CoinTrail(t.stackCount) > 0)
                .OrderByDescending(t => t.stackCount).FirstOrDefault();
            if (stack == null) return;
            int n = GodPresenceOwnMath.CoinTrail(stack.stackCount);
            var from = stack.Position;
            var edge = CellFinder.RandomEdgeCell(map);
            float best = float.MaxValue;
            foreach (var e in new[] { new IntVec3(from.x, 0, 0), new IntVec3(from.x, 0, map.Size.z - 1), new IntVec3(0, 0, from.z), new IntVec3(map.Size.x - 1, 0, from.z) })
            {
                float d = e.DistanceToSquared(from);
                if (d < best)
                {
                    best = d;
                    edge = e;
                }
            }
            var dir = (edge - from).ToVector3().normalized;
            int placed = 0;
            for (int i = 2; i < 30 && placed < n; i += 2)
            {
                var c = (from.ToVector3Shifted() + dir * i).ToIntVec3();
                if (!c.InBounds(map) || !c.Standable(map) || c.GetFirstItem(map) != null) continue;
                try
                {
                    var coin = stack.SplitOff(1);
                    if (!GenPlace.TryPlaceThing(coin, c, map, ThingPlaceMode.Direct))
                    {
                        GenPlace.TryPlaceThing(coin, from, map, ThingPlaceMode.Near);
                        continue;
                    }
                    FleckMaker.ThrowLightningGlow(coin.DrawPos, map, 0.3f);
                    placed++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: the coins trailing off", e);
                    return;
                }
                if (stack.Destroyed) break;
            }
        }

        // ================= Shared =================

        /// <summary>The middle of the colony's people (the map's middle when nobody is out).</summary>
        internal static IntVec3 Center(Map map)
        {
            var people = map.mapPawns.FreeColonistsSpawned;
            if (people.Count == 0) return map.Center;
            return new IntVec3((int)people.Average(p => p.Position.x), 0, (int)people.Average(p => p.Position.z)).ClampInsideMap(map);
        }

        /// <summary>One of its own sounds, where it begins.</summary>
        private static void Play(GameCondition_GodPresence c, Map map, IntVec3 at)
        {
            var sounds = c.Own?.Sounds;
            if (sounds == null || sounds.Count == 0 || !at.IsValid) return;
            GodSounds.Play(sounds.RandomElement(), map, at, 0.7f)?.End();
        }
    }
}
