using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Visual and sound touches: the game's own flecks, sky colors and sounds, used sparingly so that what Rimfolk says is
    /// happening can be seen and heard. Fireflies blink around colonists on firefly nights, hail pings off the fields, a
    /// camp's fire smokes and sparks, a warlord's band raises dust, a wedding gets hearts, a kept tradition a song, a
    /// council meeting a bell, and now and then someone says something about the sky or hums at work.
    /// </summary>
    /// <remarks>
    /// <para>Nothing here changes the game: no stats, no thoughts, no needs. The one exception is small and vanilla's own:
    /// on a comet night, an idle colonist in good spirits may go and lie down to watch the sky (the game's skygazing,
    /// with its usual recreation), instead of wandering about with nothing to do.</para>
    /// <para>The same clock runs the parts in Immersion/: a colonist now and then saying something about how they really
    /// are (<see cref="StateSpeech"/>), the first snow, heat waves, cold snaps and storms remarked on and written down
    /// (<see cref="WeatherTalk"/>), a gathering's fire, a grave's low glow and a toast's raised cups (<see cref="Glows"/>),
    /// and a small bell when a calendar day comes round (<see cref="Chimes"/>).</para>
    /// <para>Cost: one modulo per tick. Twice a second of game time (and never more often than about twice a second of
    /// real time, whatever the speed), on the map being looked at only, a few cheap checks and at most a handful of
    /// flecks, each only on cells in view. Once an in-game hour, one ambient line or hum at most. No textures, materials,
    /// shaders or looping sounds; the only sound is a one-shot bell. Lists are small, capped and cleared when the setting
    /// is off. Nothing is saved.</para>
    /// </remarks>
    public class VisualTouches : GameComponent
    {
        public VisualTouches(Game game)
        {
        }

        /// <summary>The "visual and sound touches" setting.</summary>
        public static bool On => ColonyCouncilMod.Settings != null && ColonyCouncilMod.Settings.visualTouches;

        public static VisualTouches Instance => Current.Game?.GetComponent<VisualTouches>();

        private const int PulseTicks = 30;
        private const int PulseOffset = 11;
        /// <summary>At high game speeds, the pulse still comes no more often than this (seconds of real time).</summary>
        private const float MinPulseSeconds = 0.4f;
        private const int MaxPending = 16;
        private const int MaxBand = 10;
        private const int MaxFirefliesPerPulse = 4;

        private static readonly Color FireflyColor = new Color(0.78f, 1f, 0.4f, 0.9f);
        private static readonly Color IceColor = new Color(0.92f, 0.96f, 1f, 0.9f);
        private static readonly Color DustColor = new Color(0.92f, 0.86f, 0.74f, 1f);
        private static readonly Color AmbientSpeech = new Color(0.9f, 0.92f, 0.88f);

        private float lastPulse = -100f;
        private int pulses;
        /// <summary>The in-game hour the hourly look last ran in.</summary>
        private int lastHour = -1;

        /// <summary>Flecks still to come over someone's head (hearts at a wedding, a few notes of a song).</summary>
        private readonly List<Pending> pending = new List<Pending>();

        /// <summary>A warlord's band walking in, for a little while after it arrives.</summary>
        private readonly List<Pawn> band = new List<Pawn>();
        private int bandUntil = -1;

        /// <summary>When each colonist (by id) may next say an ambient line, hum, or be sent to watch the comet.</summary>
        private readonly Dictionary<int, int> lineUntil = new Dictionary<int, int>();
        private readonly Dictionary<int, int> gazeUntil = new Dictionary<int, int>();

        /// <summary>A gathering's fire, a grave's glow, a toast's raised cups (Immersion/Glows.cs).</summary>
        internal readonly Glows glows = new Glows();
        /// <summary>The first snow, heat waves, cold snaps and storms (Immersion/WeatherTalk.cs).</summary>
        internal readonly WeatherTalk weather = new WeatherTalk();
        /// <summary>A small bell when a calendar day comes round (Immersion/Chimes.cs).</summary>
        private readonly Chimes chimes = new Chimes();

        private sealed class Pending
        {
            public Pawn pawn;
            public FleckDef def;
            public int left;
            public int wait;
        }

        // ---------------- Vanilla defs, looked up once ----------------

        private static bool looked;
        private static FleckDef snowBits;
        private static FleckDef music;
        private static JoyGiverDef skygaze;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            // Both are the game's own (the bits thrown when snow is cleared, the notes over a musician), looked up by name
            // so a missing one only means that touch is left out.
            snowBits = DefDatabase<FleckDef>.GetNamedSilentFail("SnowBits");
            music = DefDatabase<FleckDef>.GetNamedSilentFail("Music");
            foreach (var d in DefDatabase<JoyGiverDef>.AllDefsListForReading)
                if (d.giverClass == typeof(JoyGiver_Skygaze))
                {
                    skygaze = d;
                    break;
                }
        }

        // ---------------- The clock ----------------

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % PulseTicks != PulseOffset) return;
            bool on = On;
            if (!on)
            {
                if (pending.Count > 0 || band.Count > 0 || glows.Any) Clear();
                // Small habits still need to know when the first snow fell (children out playing in it).
                if (!SmallHabits.On) return;
            }
            try
            {
                Look();
                // The first pulse of each in-game hour: an hour isn't a whole number of pulses, so a fixed tick of the
                // hour would only fall on a pulse every third hour.
                int hour = now / GenDate.TicksPerHour;
                if (hour != lastHour)
                {
                    lastHour = hour;
                    if (on) Hourly(now);
                    else weather.Hourly(this, now, false);
                }
                if (!on) return;
                float real = Time.realtimeSinceStartup;
                if (real - lastPulse < MinPulseSeconds && real >= lastPulse) return;
                lastPulse = real;
                Pulse(now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visual touches", e);
                Clear();
            }
        }

        private void Clear()
        {
            pending.Clear();
            band.Clear();
            bandUntil = -1;
            glows.Clear();
        }

        /// <summary>The map being looked at and the cells in view, or false when there's nothing to look at.</summary>
        private static bool Looking(out Map map, out CellRect view)
        {
            map = Find.CurrentMap;
            view = CellRect.Empty;
            if (map == null || WorldRendererUtility.WorldRendered || Find.CameraDriver == null) return false;
            view = Find.CameraDriver.CurrentViewRect.ClipInsideMap(map);
            return view.Area > 0;
        }

        private void Pulse(int now)
        {
            if (!Looking(out var map, out var view))
            {
                pending.Clear();
                return;
            }
            pulses++;
            if (pending.Count > 0) Pendings(map, view);
            if (band.Count > 0) Band(map, view, now);
            chimes.Pulse(map, now);
            // From that far out, a firefly or a bit of ice is a pixel at best.
            if (Find.CameraDriver.CurrentZoom >= CameraZoomRange.Furthest) return;
            if (glows.Any) glows.Pulse(map, view, now, pulses);
            var conditions = map.gameConditionManager;
            if (conditions == null) return;
            if (conditions.GetActiveCondition<GameCondition_Fireflies>() != null) Fireflies(map, view);
            if (conditions.GetActiveCondition<GameCondition_Hail>() != null) HailAround(map, view);
            if (pulses % 4 == 0) CampFire(map, view);
        }

        internal static bool Dark(Map map) => GenCelestial.CurCelestialSunGlow(map) < 0.3f;

        // ---------------- Fireflies ----------------

        /// <summary>A few short green-yellow blinks around colonists outside after dark, and one now and then over the grass.</summary>
        private static void Fireflies(Map map, CellRect view)
        {
            int hour = GenLocalDate.HourOfDay(map);
            if ((hour >= 4 && hour < 20) || !Dark(map)) return;
            int made = 0;
            var pawns = map.mapPawns.FreeColonistsSpawned;
            int count = pawns.Count;
            int start = count > 0 ? Rand.Range(0, count) : 0;
            for (int i = 0; i < count && made < MaxFirefliesPerPulse; i++)
            {
                var p = pawns[(start + i) % count];
                if (p == null || !view.Contains(p.Position) || p.Position.Roofed(map) || !Rand.Chance(0.5f)) continue;
                var at = p.DrawPos + new Vector3(Rand.Range(-2.5f, 2.5f), 0f, Rand.Range(-2f, 2.5f));
                var c = at.ToIntVec3();
                if (!c.InBounds(map) || c.Roofed(map)) continue;
                Firefly(at, map);
                made++;
            }
            if (made < MaxFirefliesPerPulse && Rand.Chance(0.5f))
            {
                var c = view.RandomCell;
                if (c.InBounds(map) && !c.Roofed(map) && !c.Fogged(map) && c.GetPlant(map) != null)
                    Firefly(c.ToVector3Shifted() + new Vector3(Rand.Range(-0.4f, 0.4f), 0f, Rand.Range(-0.4f, 0.4f)), map);
            }
        }

        private static void Firefly(Vector3 at, Map map)
        {
            var data = FleckMaker.GetDataStatic(at, map, FleckDefOf.LightningGlow, Rand.Range(0.4f, 0.65f));
            data.instanceColor = FireflyColor;
            data.velocityAngle = Rand.Range(0f, 360f);
            data.velocitySpeed = Rand.Range(0.08f, 0.25f);
            data.solidTimeOverride = Rand.Range(0.15f, 0.5f);
            map.flecks.CreateFleck(data);
        }

        // ---------------- Hail ----------------

        /// <summary>Hailstones bouncing off open ground in view: two small pings a pulse.</summary>
        private static void HailAround(Map map, CellRect view)
        {
            for (int i = 0; i < 2; i++)
            {
                var c = view.RandomCell;
                if (!c.InBounds(map) || c.Roofed(map) || c.Fogged(map) || c.GetTerrain(map)?.IsWater == true) continue;
                Ping(map, c, false);
            }
        }

        /// <summary>The hail just knocked a crop back (called by the hailstorm): a bigger ping where it was hit, if in view.</summary>
        public static void HailHit(Map map, IntVec3 c)
        {
            if (!On || map == null) return;
            try
            {
                if (!Visible(map, c)) return;
                Look();
                Ping(map, c, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hail touch", e);
            }
        }

        private static void Ping(Map map, IntVec3 c, bool hit)
        {
            var def = snowBits ?? FleckDefOf.AirPuff;
            float size = snowBits != null ? 1f : 0.5f;
            var at = c.ToVector3Shifted() + new Vector3(Rand.Range(-0.35f, 0.35f), 0f, Rand.Range(-0.35f, 0.35f));
            int n = hit ? 4 : 2;
            for (int i = 0; i < n; i++)
            {
                var data = FleckMaker.GetDataStatic(at, map, def, Rand.Range(0.4f, 0.7f) * size);
                data.instanceColor = IceColor;
                data.velocityAngle = Rand.Range(0f, 360f);
                data.velocitySpeed = Rand.Range(0.6f, 1.6f);
                data.rotationRate = Rand.Range(-200f, 200f);
                map.flecks.CreateFleck(data);
            }
        }

        // ---------------- A camp's fire ----------------

        /// <summary>A thin line of smoke from a camp's lit fire, and at night a few sparks going up. Every two seconds or so.</summary>
        private static void CampFire(Map map, CellRect view)
        {
            var camp = CampTracker.Instance?.camp;
            if (camp == null || camp.map != map || !camp.Active || camp.phase == CampPhase.Coming) return;
            var cell = camp.FireCell;
            if (!cell.IsValid || !view.ExpandedBy(3).Contains(cell)) return;
            FireSmoke(camp.Fire, map);
        }

        /// <summary>A thin line of smoke from a lit fire, and at night now and then a few sparks going up. False when it's out.</summary>
        internal static bool FireSmoke(Thing fire, Map map)
        {
            if (fire == null || !fire.Spawned || fire.Map != map) return false;
            var fuel = fire.TryGetComp<CompRefuelable>();
            if (fuel != null && !fuel.HasFuel) return false;
            var top = fire.DrawPos + new Vector3(Rand.Range(-0.15f, 0.15f), 0f, 0.3f);
            FleckMaker.ThrowSmoke(top, map, Rand.Range(0.45f, 0.7f));
            if (Dark(map) && Rand.Chance(0.5f)) FleckMaker.ThrowMicroSparks(fire.DrawPos, map);
            return true;
        }

        // ---------------- A warlord's band ----------------

        /// <summary>
        /// A warlord walks in with his people (called when he joins an arriving raid): dust where they stand, if in view,
        /// and behind them as they march for the next half hour or so. Not in rain or snow, not on water.
        /// </summary>
        public static void BandArrives(Map map, List<Pawn> pawns)
        {
            if (!On || map == null || pawns == null) return;
            try
            {
                var self = Instance;
                if (self == null) return;
                self.band.Clear();
                foreach (var p in pawns)
                {
                    if (self.band.Count >= MaxBand) break;
                    if (p != null && p.Spawned && p.Map == map) self.band.Add(p);
                }
                if (self.band.Count == 0) return;
                self.bandUntil = Find.TickManager.TicksGame + 1500;
                if (!Looking(out var current, out var view) || current != map || Wet(map)) return;
                int made = 0;
                foreach (var p in self.band)
                {
                    if (made >= 8) break;
                    if (!view.Contains(p.Position) || !Dusty(map, p.Position)) continue;
                    FleckMaker.ThrowDustPuffThick(p.DrawPos, map, Rand.Range(1.5f, 2.2f), DustColor);
                    made++;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("band touch", e);
            }
        }

        private void Band(Map map, CellRect view, int now)
        {
            if (now > bandUntil)
            {
                band.Clear();
                return;
            }
            if (Wet(map)) return;
            int made = 0;
            foreach (var p in band)
            {
                if (made >= 4) break;
                if (p == null || p.Dead || !p.Spawned || p.Map != map || p.pather == null || !p.pather.Moving) continue;
                if (!view.Contains(p.Position) || !Dusty(map, p.Position) || !Rand.Chance(0.6f)) continue;
                FleckMaker.ThrowDustPuffThick(p.DrawPos + new Vector3(0f, 0f, -0.3f), map, Rand.Range(1.1f, 1.7f), DustColor);
                made++;
            }
        }

        private static bool Wet(Map map) => map.weatherManager != null && (map.weatherManager.RainRate > 0.1f || map.weatherManager.SnowRate > 0.1f);

        private static bool Dusty(Map map, IntVec3 c) =>
            c.InBounds(map) && c.GetTerrain(map)?.IsWater != true && (map.snowGrid == null || map.snowGrid.GetDepth(c) < 0.2f);

        // ---------------- Celebrations ----------------

        /// <summary>Two people just married: a few hearts over each, and one over each friend standing nearby.</summary>
        public static void Wedding(Pawn a, Pawn b)
        {
            if (!On) return;
            try
            {
                var self = Instance;
                if (self == null) return;
                self.Queue(a, FleckDefOf.Heart, 3, 0);
                self.Queue(b, FleckDefOf.Heart, 3, 1);
                var at = a != null && a.Spawned ? a : b;
                if (at == null || !at.Spawned) return;
                int guests = 0;
                foreach (var o in at.Map.mapPawns.FreeColonistsSpawned)
                {
                    if (guests >= 4) break;
                    if (o == a || o == b || !o.Position.InHorDistOf(at.Position, 10f) || !People.HasInnerLife(o)) continue;
                    self.Queue(o, FleckDefOf.Heart, 1, Rand.RangeInclusive(2, 5));
                    guests++;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wedding touch", e);
            }
        }

        /// <summary>A real fight won without losses: one or two of those who fought say so, if they're in view.</summary>
        public static void Victory(List<Pawn> fighters)
        {
            if (!On || fighters == null || !Speech.On) return;
            try
            {
                if (!Looking(out var map, out var view)) return;
                int said = 0;
                foreach (var p in fighters.InRandomOrder())
                {
                    if (said >= 2) break;
                    if (!CanSpeak(p, map, view)) continue;
                    string line = Flavor.FromPool("Council_Pool_TouchVictory", p);
                    if (line.NullOrEmpty()) continue;
                    CouncilGameComponent.Speak(p, line, AmbientSpeech);
                    said++;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("victory touch", e);
            }
        }

        /// <summary>A tradition kept (or just become one): a few colonists in view sing a little, and one of them says something.</summary>
        public static void Tradition(IEnumerable<Pawn> colonists, string label)
        {
            if (!On || colonists == null) return;
            try
            {
                var self = Instance;
                if (self == null || !Looking(out var map, out var view)) return;
                Look();
                int singers = 0;
                bool spoke = !Speech.On;
                foreach (var p in colonists.InRandomOrder())
                {
                    if (singers >= 3) break;
                    if (!CanSpeak(p, map, view)) continue;
                    if (music != null) self.Queue(p, music, 2, Rand.RangeInclusive(0, 3));
                    if (!spoke)
                    {
                        string line = Flavor.FromPool("Council_Pool_TouchTradition", p);
                        if (!line.NullOrEmpty())
                        {
                            CouncilGameComponent.Speak(p, line.Formatted((label ?? "").Named("TRADITION")).Resolve(), AmbientSpeech);
                            spoke = true;
                        }
                    }
                    singers++;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("tradition touch", e);
            }
        }

        /// <summary>A council meeting is called to order: a small bell at the table, heard if the camera is nearby.</summary>
        public static void MeetingOpens(Map map, IntVec3 spot)
        {
            if (!On || map == null || !spot.IsValid) return;
            try
            {
                if (!Looking(out var current, out var view) || current != map || !view.ExpandedBy(12).Contains(spot)) return;
                SoundDefOf.TinyBell?.PlayOneShot(SoundInfo.InMap(new TargetInfo(spot, map)));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("meeting bell", e);
            }
        }

        private void Queue(Pawn p, FleckDef def, int count, int wait)
        {
            if (p == null || def == null || !p.Spawned || pending.Count >= MaxPending) return;
            pending.Add(new Pending { pawn = p, def = def, left = count, wait = wait });
        }

        /// <summary>One fleck per entry every other pulse, over the head of whoever it's for, if they're in view.</summary>
        private void Pendings(Map map, CellRect view)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var e = pending[i];
                if (e.pawn == null || e.pawn.Dead || !e.pawn.Spawned)
                {
                    pending.RemoveAt(i);
                    continue;
                }
                if (e.wait > 0)
                {
                    e.wait--;
                    continue;
                }
                if (e.pawn.Map == map && view.Contains(e.pawn.Position)) FleckMaker.ThrowMetaIcon(e.pawn.Position, map, e.def);
                e.left--;
                e.wait = 1;
                if (e.left <= 0) pending.RemoveAt(i);
            }
        }

        // ---------------- Once an hour: the sky, and people ----------------

        private void Hourly(int now)
        {
            Prune(lineUntil, now);
            Prune(gazeUntil, now);
            // The weather is watched on every home map (a journal is written wherever it snows); it speaks only in view.
            bool spoke = weather.Hourly(this, now, true);
            if (!Looking(out var map, out var view)) return;
            var conditions = map.gameConditionManager;
            if (conditions == null) return;
            bool comet = conditions.GetActiveCondition<GameCondition_Comet>() != null;
            if (comet) CometGazer(map, now);
            if (spoke) return;
            bool speech = Speech.On;
            if (speech && AmbientLine(map, view, now, comet, conditions)) return;
            if (speech && StateSpeech.TrySay(this, map, view, now)) return;
            Hum(map, view, now);
        }

        private static void Prune(Dictionary<int, int> until, int now)
        {
            if (until.Count == 0) return;
            List<int> gone = null;
            foreach (var kv in until)
                if (kv.Value <= now) (gone ??= new List<int>()).Add(kv.Key);
            if (gone != null) foreach (int k in gone) until.Remove(k);
        }

        /// <summary>
        /// On a comet night, an idle colonist in good spirits sometimes stops wandering about and lies down to watch the
        /// sky: the game's own skygazing, chosen through its own checks (weather, temperature, a spot outside). At most
        /// one an hour, each colonist once a day.
        /// </summary>
        private void CometGazer(Map map, int now)
        {
            if (skygaze?.Worker == null || GenCelestial.CurCelestialSunGlow(map) > 0.3f || !Rand.Chance(0.5f)) return;
            var pawns = map.mapPawns.FreeColonistsSpawned;
            int count = pawns.Count;
            if (count == 0) return;
            int start = Rand.Range(0, count);
            for (int i = 0; i < count; i++)
            {
                var p = pawns[(start + i) % count];
                if (!Idle(p) || gazeUntil.TryGetValue(p.thingIDNumber, out int until) && until > now) continue;
                if ((p.needs?.mood?.CurLevelPercentage ?? 0f) < 0.55f || p.needs.joy == null || p.needs.joy.CurLevel > 0.95f) continue;
                if (p.needs.rest != null && p.needs.rest.CurLevel < 0.3f) continue;
                if (!skygaze.Worker.CanBeGivenTo(p)) continue;
                var job = skygaze.Worker.TryGiveJob(p);
                if (job == null) continue;
                gazeUntil[p.thingIDNumber] = now + GenDate.TicksPerDay;
                p.jobs.StartJob(job, JobCondition.InterruptForced);
                return;
            }
        }

        /// <summary>Wandering about with nothing to do, and free to stop.</summary>
        private static bool Idle(Pawn p) =>
            p != null && p.Spawned && !p.Downed && !p.Drafted && !p.InMentalState && p.Awake() && p.jobs != null && p.mindState != null
            && p.mindState.IsIdle && (p.CurJobDef == JobDefOf.Wait_Wander || p.CurJobDef == JobDefOf.GotoWander)
            && p.GetLord() == null && !p.DevelopmentalStage.Baby() && !HealthAIUtility.ShouldSeekMedicalRest(p)
            && (p.timetable == null || p.timetable.CurrentAssignment != TimeAssignmentDefOf.Sleep);

        /// <summary>Someone outside says a line about the sky or the weather Rimfolk brought. Sometimes, if anyone is in view.</summary>
        private bool AmbientLine(Map map, CellRect view, int now, bool comet, GameConditionManager conditions)
        {
            bool dark = Dark(map);
            string pool = null;
            if (comet && dark) pool = "Council_Pool_TouchComet";
            else if (dark && conditions.GetActiveCondition<GameCondition_Fireflies>() != null) pool = "Council_Pool_TouchFireflies";
            else if (conditions.GetActiveCondition<GameCondition_Hail>() != null) pool = "Council_Pool_TouchHail";
            else if (!dark && conditions.GetActiveCondition<GameCondition_WarmSpell>() != null) pool = "Council_Pool_TouchWarmSpell";
            if (pool == null || !Rand.Chance(0.4f)) return false;
            var p = Someone(map, view, now, true, false);
            if (p == null) return false;
            string line = Flavor.FromPool(pool, p);
            if (line.NullOrEmpty()) return false;
            CouncilGameComponent.Speak(p, line, AmbientSpeech);
            lineUntil[p.thingIDNumber] = now + GenDate.TicksPerDay;
            return true;
        }

        /// <summary>A content colonist at work or at play hums: two notes over their head. Sometimes, if anyone is in view.</summary>
        private void Hum(Map map, CellRect view, int now)
        {
            if (music == null || !Rand.Chance(0.2f)) return;
            var p = Someone(map, view, now, false, true);
            if (p == null) return;
            Queue(p, music, 2, 0);
            lineUntil[p.thingIDNumber] = now + GenDate.TicksPerDay / 2;
        }

        /// <summary>A colonist in view, awake and free, who hasn't said or hummed anything lately; outside, or content and busy.</summary>
        private Pawn Someone(Map map, CellRect view, int now, bool outside, bool content)
        {
            var pawns = map.mapPawns.FreeColonistsSpawned;
            int count = pawns.Count;
            if (count == 0) return null;
            int start = Rand.Range(0, count);
            for (int i = 0; i < count; i++)
            {
                var p = pawns[(start + i) % count];
                if (!CanSpeak(p, map, view) || p.Drafted || p.GetLord() != null) continue;
                if (lineUntil.TryGetValue(p.thingIDNumber, out int until) && until > now) continue;
                if (outside && p.Position.Roofed(map)) continue;
                if (content)
                {
                    if ((p.needs?.mood?.CurLevelPercentage ?? 0f) < 0.65f) continue;
                    var job = p.CurJob;
                    if (job == null || (job.workGiverDef == null && job.def.joyKind == null)) continue;
                }
                return p;
            }
            return null;
        }

        internal static bool CanSpeak(Pawn p, Map map, CellRect view) =>
            p != null && p.Spawned && p.Map == map && view.Contains(p.Position) && !p.Dead && !p.Downed && p.Awake() && !p.InMentalState
            && People.HasInnerLife(p) && !p.DevelopmentalStage.Baby() && p.health?.capacities != null && p.health.capacities.CapableOf(PawnCapacityDefOf.Talking);

        private static bool Visible(Map map, IntVec3 c) => Looking(out var current, out var view) && current == map && view.Contains(c);

        /// <summary>The map being looked at and the cells in view (for Immersion/), or false when there's nothing to look at.</summary>
        internal static bool InView(out Map map, out CellRect view) => Looking(out map, out view);

        // ---------------- Who may speak (shared with Immersion/) ----------------

        /// <summary>
        /// A colonist on screen, awake, free and able to talk, who isn't under arms or at a gathering and hasn't said an
        /// ambient line (or hummed) lately: one rate for everything said this way, so nobody talks more than about once a day.
        /// </summary>
        internal bool Free(Pawn p, Map map, CellRect view, int now) =>
            CanSpeak(p, map, view) && !p.Drafted && p.GetLord() == null && !(lineUntil.TryGetValue(p.thingIDNumber, out int until) && until > now);

        /// <summary>They just said something: the next ambient line from them waits until then.</summary>
        internal void Spoke(Pawn p, int until)
        {
            if (p != null) lineUntil[p.thingIDNumber] = until;
        }

        // ---------------- Fires, glows and cups (Immersion/Glows.cs) ----------------

        /// <summary>A gathering round this fire: smoke now and then, and sparks at night, while it goes on (refreshed by the gathering).</summary>
        public static void Bonfire(Thing fire, int untilTick)
        {
            if (!On || fire == null) return;
            try
            {
                Instance?.glows.Fire(fire, untilTick);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("bonfire touch", e);
            }
        }

        /// <summary>Someone remembering at a grave: after dark, a low warm glow there, like a candle, while they stay.</summary>
        public static void Candle(Map map, IntVec3 cell, int untilTick)
        {
            if (!On || map == null || !cell.IsValid) return;
            try
            {
                Instance?.glows.Candle(map, cell, untilTick);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("grave glow touch", e);
            }
        }

        /// <summary>A toast: cups go up together (a small warm glint over each head in view).</summary>
        public static void RaiseCups(IList<Pawn> who)
        {
            if (!On || who == null) return;
            try
            {
                if (!Looking(out var map, out var view)) return;
                Glows.Raise(who, map, view);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("toast touch", e);
            }
        }
    }
}
