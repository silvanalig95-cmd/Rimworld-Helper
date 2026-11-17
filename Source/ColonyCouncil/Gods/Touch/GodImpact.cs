using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What an occurrence was about, said just before its words are written (GodTouch.About), for the effect of its own that
    /// follows it: the one named in its words, the one it is about, another, a thing, several people, cells.
    /// </summary>
    public sealed class TouchAbout
    {
        public Map map;
        public string key;
        public int at;
        public Pawn who, pawn, other;
        public Thing thing;
        public List<Pawn> pawns;
        public List<IntVec3> cells;
    }

    /// <summary>
    /// An occurrence's own effect that keeps going a while (saved with the gods' touch): a body rotting away, the food in a room
    /// keeping less long, a room held cold, the long noon, the still air, the anima tree humming, a machine's free hour, a light
    /// held lit or dark, the warm ground, rain on its way, the game coming back, a kind dream waiting for its night.
    /// </summary>
    public class GodTouchRunning : IExposable
    {
        public string kind, god, what;
        public Map map;
        public IntVec3 cell = IntVec3.Invalid;
        public Thing thing;
        public Pawn pawn;
        public int since, until, last;
        public float value;
        public bool flag;

        public TouchEffect Effect => Enum.TryParse(kind, out TouchEffect e) ? e : TouchEffect.None;

        /// <summary>Nothing left to do it on: its home gone, or a kind not known any more.</summary>
        public bool Gone => map == null || map.Disposed || Effect == TouchEffect.None;

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref god, "god");
            Scribe_Values.Look(ref what, "what");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_References.Look(ref thing, "thing");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref since, "since");
            Scribe_Values.Look(ref until, "until");
            Scribe_Values.Look(ref last, "last");
            Scribe_Values.Look(ref value, "value");
            Scribe_Values.Look(ref flag, "flag");
        }
    }

    /// <summary>
    /// Every god's doing leaves something real (docs/gods-impact-proposals.md; the player, 17 November 2026: "not every god
    /// interaction with the world necessarily needs to have a strong impact, but it should also not be completely trivial",
    /// then "yes, go ahead with all!"): each occurrence in GodImpactMath.Table brings its own effect, always, played out here
    /// after the moods (GodTouch.Resolve). An effect on the place happens whether or not anyone of the colony saw it, there
    /// to be found; one on people goes to those it names or to those who saw it. Each is shown: on the map, or as a mood or a
    /// condition on someone with a plain label saying why, and in a line of its own where nothing on the map says it; the
    /// god's own look plays where it happens (GodTouchFx), and the camera moves only for a notable one, as the touch does. The
    /// words never name the god. Harm only where the proposals say, and never to anyone's body. The game's own things and
    /// mechanics throughout: rot, rich soil, the cold that kills tender crops, the stone of the place, the game's pregnancy,
    /// inspirations, the turbines and the wind, fire.
    /// </summary>
    public static class GodImpact
    {
        private static int Now => Find.TickManager.TicksGame;

        private static string K(string what) => "CC_GodImpact_" + what;

        private static ThingDef Thing(string name) => DefDatabase<ThingDef>.GetNamedSilentFail(name);

        /// <summary>A running effect is looked at this often.</summary>
        public const int StepTicks = 250;

        // ================= Following an occurrence =================

        /// <summary>
        /// The occurrence's own effect (its words key in GodImpactMath.Table), after the moods. False when it had nothing to
        /// do. Not kept among the touch's effects lately: the random effects lean away from their own, as before.
        /// </summary>
        internal static bool Follow(GodTouchBook b, GodTouch.Pending p, List<Pawn> ours, string key)
        {
            var im = GodImpactMath.Of(key);
            if (im.effect == TouchEffect.None || p.map == null || p.god == null) return false;
            if (GodImpactMath.NeedsWitness(im.effect) && ours.Count == 0) return false;
            p.about.TryGetValue(key, out var about);
            var at = p.cellOf.TryGetValue(key, out var own) ? own : GodTouch.Place(p, ours);
            var c = new TouchContext(p.god, p.map, at, ours, p.taken, p.plain, im.tier, im.dark);
            bool done = false;
            try
            {
                done = GodTouch.Quietly(() => Do(b, im, c, about));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: what an occurrence leaves (" + key + ")", e);
            }
            return done;
        }

        /// <summary>Plays the effect out and shows it. False when there was nothing for it to do here.</summary>
        internal static bool Do(GodTouchBook b, Impact im, TouchContext c, TouchAbout about)
        {
            switch (im.effect)
            {
                // ---- The touch's own effects, as they are ----
                case TouchEffect.Bloom:
                case TouchEffect.CatComes:
                case TouchEffect.RatsGone:
                case TouchEffect.Game:
                case TouchEffect.Fish:
                case TouchEffect.Find:
                    return GodTouchEffects.Do(im.Entry, c);
                // ---- The touch's own, fitted to what was seen ----
                case TouchEffect.RichSoil: return RichRing(c, im.size);
                case TouchEffect.Trader: return Trader(c, im.size > 0f);
                case TouchEffect.Inspire: return Inspire(c, about?.who, im.size > 0f);
                case TouchEffect.Charge: return Charge(c, im.size);
                case TouchEffect.FiresOut: return FiresOut(c);
                case TouchEffect.Rain: return RainSoon(b, c, im.size);
                case TouchEffect.GameLeaves: return GameLeaves(b, c, im.size);
                case TouchEffect.Hardy: return Hardy(c, im.size);
                case TouchEffect.Sleepless: return Sleepless(c, about?.who);
                // ---- Each occurrence's own ----
                case TouchEffect.RotAway: return RotAway(b, c, about);
                case TouchEffect.RotGone: return RotGone(c);
                case TouchEffect.GriefEased: return GriefEased(c, about);
                case TouchEffect.KeepsLess: return KeepsLess(b, c);
                case TouchEffect.RestKin: return RestKin(c, about);
                case TouchEffect.FrostLine: return FrostLine(c, about);
                case TouchEffect.ColdRoom: return ColdRoom(b, c);
                case TouchEffect.Rested: return Rested(c, about);
                case TouchEffect.LongNoon: return LongNoon(b, c);
                case TouchEffect.Warmth: return Warmth(c);
                case TouchEffect.NightWard: return NightWard(b, c, about);
                case TouchEffect.MoonSleep: return MoonSleep(c);
                case TouchEffect.PainEased: return PainEased(c, about);
                case TouchEffect.HealFaster: return HealFaster(c, about);
                case TouchEffect.BingeEnds: return BingeEnds(c, about);
                case TouchEffect.Drink: return Drink(c, about);
                // The sign itself spilled the drink and left the coin where it showed, whether or not anyone saw (GodOmensOwn).
                case TouchEffect.Spill:
                case TouchEffect.Coin:
                    return true;
                case TouchEffect.Cheered: return Cheered(c);
                case TouchEffect.Silver: return Silver(c);
                case TouchEffect.StaysLit: return StaysLit(b, c);
                case TouchEffect.FreeHour: return FreeHour(b, c, about);
                case TouchEffect.StoneChunks: return StoneChunks(c);
                case TouchEffect.DigFaster: return DigFaster(c, about);
                case TouchEffect.WarmGround: return WarmGround(b, c);
                case TouchEffect.StillAir: return StillAir(b, c);
                case TouchEffect.AnimaHum: return AnimaHum(b, c);
                case TouchEffect.WithYoung: return WithYoung(c);
                case TouchEffect.AnimalsCalm: return Everyone(c, GodImpactDefOf.Rimfolk_GodTouch_AnimalsCalm, 0, K("AnimalsCalm"));
                case TouchEffect.StaysDark: return StaysDark(b, c);
                case TouchEffect.KindDream: return KindDream(b, c, about);
                case TouchEffect.SameDream: return SameDream(c, about);
                case TouchEffect.StillHour: return Everyone(c, GodImpactDefOf.Rimfolk_GodTouch_StillHour, 0, K("StillHour"));
                case TouchEffect.Admired: return Admire(b, c, about);
                default: return false;
            }
        }

        /// <summary>Shown where it happened: the god's look, a line of what happened (the camera for a notable one), as the touch shows its own.</summary>
        private static void Show(TouchContext c, IntVec3 at, string key, params NamedArgument[] args) => GodTouchEffects.Show(c, at, key, args);

        private static string Names(List<Pawn> pawns) => GodTouchEffects.Names(pawns);

        // ================= Running on =================

        /// <summary>An effect that keeps going: the same one at the same place takes the place of the last; the oldest give way.</summary>
        private static void Run(GodTouchBook b, GodTouchRunning r)
        {
            b.running.RemoveAll(x => x == null || x.kind == r.kind && x.map == r.map && x.thing == r.thing && x.pawn == r.pawn && x.cell == r.cell);
            b.running.Add(r);
            while (b.running.Count > GodImpactMath.RunningKept) b.running.RemoveAt(0);
            Refresh(b);
            b.Due(Now + StepTicks);
        }

        private static GodTouchRunning Running(TouchEffect e, TouchContext c, int until) =>
            new GodTouchRunning { kind = e.ToString(), god = c.god?.Key, map = c.map, cell = c.cell, since = Now, last = Now, until = until };

        /// <summary>The book's due tick: each running effect looked at, those whose time is up let go.</summary>
        internal static void Step(GodTouchBook b, int now)
        {
            if (b.running.Count == 0) return;
            bool changed = false;
            for (int i = b.running.Count - 1; i >= 0; i--)
            {
                var r = b.running[i];
                bool keep;
                try
                {
                    keep = r != null && !r.Gone && Tick(r, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: what an occurrence keeps doing (" + r?.kind + ")", e);
                    keep = false;
                }
                if (keep) continue;
                b.running.RemoveAt(i);
                changed = true;
            }
            if (changed) Refresh(b);
            if (b.running.Count > 0) b.Due(now + StepTicks);
        }

        private static bool Tick(GodTouchRunning r, int now)
        {
            switch (r.Effect)
            {
                case TouchEffect.RotAway: return RotStep(r, now);
                case TouchEffect.KeepsLess: return KeepsLessStep(r, now);
                case TouchEffect.ColdRoom: return ColdStep(r, now);
                case TouchEffect.WarmGround: return WarmStep(r, now);
                case TouchEffect.StaysLit:
                case TouchEffect.StaysDark:
                    return HoldStep(r, now);
                case TouchEffect.FreeHour: return FreeStep(r, now);
                case TouchEffect.Rain: return RainStep(r, now);
                case TouchEffect.GameLeaves: return GameBackStep(r, now);
                case TouchEffect.KindDream: return DreamStep(r, now);
                case TouchEffect.AnimaHum: return r.thing != null && r.thing.Spawned && now < r.until;
                case TouchEffect.LongNoon:
                case TouchEffect.StillAir:
                    return now < r.until;
                default: return false;
            }
        }

        /// <summary>A line about a running effect, where it is, with the god's look (it has no occurrence of its own to carry it).</summary>
        private static void Told(GodTouchRunning r, IntVec3 at, string key, bool dark, params NamedArgument[] more)
        {
            var map = r.map;
            var args = new List<NamedArgument> { Homes.Name(map).Named("HOME") };
            if (more != null) args.AddRange(more);
            string text = key.Translate(args.ToArray()).Resolve().CapitalizeFirst();
            var g = Gods.ByKey(r.god);
            if (g != null && at.IsValid && at.InBounds(map)) GodTouchFx.Show(g, map, at, TouchTier.Small, dark);
            Tell.Message(NewsKind.Land, Importance.Notable, map, text, at.IsValid ? new LookTargets(at, map) : LookTargets.Invalid,
                dark ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.PositiveEvent);
        }

        // ---------------- What the game's hooks read (kept from the running list) ----------------

        private static readonly HashSet<Thing> admiredSet = new HashSet<Thing>();
        private static readonly Dictionary<Thing, int> humming = new Dictionary<Thing, int>();
        private static readonly Dictionary<Map, int> noon = new Dictionary<Map, int>(), still = new Dictionary<Map, int>();

        /// <summary>The machine running its free hour, and until when (CompPowerTrader's hooks look at nothing while it is null).</summary>
        internal static Thing Free;

        internal static int FreeUntil = -1, NoonUntil = -1, StillUntil = -1;

        /// <summary>Works over what the hooks read from the book (on load, and whenever the list changes).</summary>
        internal static void Refresh(GodTouchBook b)
        {
            admiredSet.Clear();
            humming.Clear();
            noon.Clear();
            still.Clear();
            Free = null;
            FreeUntil = NoonUntil = StillUntil = -1;
            if (b == null) return;
            foreach (var t in b.admired)
                if (t != null && !t.Destroyed)
                    admiredSet.Add(t);
            foreach (var r in b.running)
            {
                if (r == null || r.Gone) continue;
                switch (r.Effect)
                {
                    case TouchEffect.AnimaHum:
                        if (r.thing != null) humming[r.thing] = r.until;
                        break;
                    case TouchEffect.LongNoon:
                        noon[r.map] = Math.Max(noon.TryGetValue(r.map, out int n) ? n : -1, r.until);
                        NoonUntil = Math.Max(NoonUntil, r.until);
                        break;
                    case TouchEffect.StillAir:
                        still[r.map] = Math.Max(still.TryGetValue(r.map, out int s) ? s : -1, r.until);
                        StillUntil = Math.Max(StillUntil, r.until);
                        break;
                    case TouchEffect.FreeHour:
                        Free = r.thing;
                        FreeUntil = r.until;
                        break;
                }
            }
        }

        /// <summary>A work named for a god and admired since.</summary>
        internal static bool Admired(Thing t) => admiredSet.Count > 0 && t != null && admiredSet.Contains(t);

        /// <summary>An anima tree humming today.</summary>
        internal static bool Humming(Thing t) => humming.Count > 0 && t != null && Current.Game != null && humming.TryGetValue(t, out int until) && Now < until;

        /// <summary>The long noon on this map: its solar panels give their most.</summary>
        internal static bool NoonOn(Map map) => map != null && Now < NoonUntil && noon.TryGetValue(map, out int until) && Now < until;

        /// <summary>The still air on this map: fire doesn't spread.</summary>
        internal static bool StillOn(Map map) => map != null && Now < StillUntil && still.TryGetValue(map, out int until) && Now < until;

        /// <summary>The machine running its free hour.</summary>
        internal static bool FreeNow(Thing t) => Free != null && t == Free && Now < FreeUntil;

        // ================= The touch's own, fitted =================

        /// <summary>The ground inside a ring of mushrooms turned rich (the touch's rich soil, within the ring).</summary>
        private static bool RichRing(TouchContext c, float radius)
        {
            if (!GodTouchEffects.Soil(c, GenRadial.RadialCellsAround(c.cell, Mathf.Max(1f, radius), true), 13)) return false;
            Show(c, c.cell, K("RichSoil_Ring"));
            return true;
        }

        /// <summary>A trader: a ship in orbit for a voice in the static (or Ducat's glint) where the colony has comms, else a caravan.</summary>
        private static bool Trader(TouchContext c, bool orbit)
        {
            if ((orbit || c.god.Key == "Ducat") && GodTouchEffects.Comms(c.map) && GodTouchEffects.Incident(c, "OrbitalTraderArrival"))
            {
                Show(c, c.cell, "CC_GodTouch_TraderOrbit");
                return true;
            }
            if (!GodTouchEffects.Incident(c, "TraderCaravanArrival")) return false;
            Show(c, c.cell, "CC_GodTouch_Trader");
            return true;
        }

        /// <summary>Inspirations to research first (a star moving the wrong way), by name: Vanilla Social Interactions Expanded's.</summary>
        private static readonly string[] Research = { "VSIE_Inspired_Research", "VSIE_Learning_Frenzy" };

        /// <summary>The game's own, when none of the god's can come to them.</summary>
        private static readonly string[] AnyInspiration = { "Inspired_Creativity", "Frenzy_Work" };

        private static IEnumerable<InspirationDef> Named(IEnumerable<string> names)
        {
            foreach (var n in names)
            {
                var d = DefDatabase<InspirationDef>.GetNamedSilentFail(n);
                if (d != null) yield return d;
            }
        }

        /// <summary>The one who saw it (named in its words) inspired: to research first for the wrong-way star, else the god's own kind, else the game's.</summary>
        private static bool Inspire(TouchContext c, Pawn who, bool research)
        {
            var people = who != null && !who.Dead && who.Spawned && who.Map == c.map ? new List<Pawn> { who } : c.saw.InRandomOrder().ToList();
            var defs = (research ? Named(Research) : Enumerable.Empty<InspirationDef>()).Concat(GodTouchEffects.Inspirations(c.god)).Concat(Named(AnyInspiration)).Distinct().ToList();
            foreach (var def in defs)
            foreach (var p in people)
            {
                if (p.mindState?.inspirationHandler == null || p.Inspired || def.Worker == null || !def.Worker.InspirationCanOccur(p)) continue;
                string reason = GodTouchEffects.Words(c, "CC_GodTouch_Inspire_Reason", p.LabelShort.Named("PAWN"));
                if (!p.mindState.inspirationHandler.TryStartInspiration(def, reason)) continue;
                GodTouchFx.Show(c.god, c.map, p.Position, TouchTier.Notable, false);
                GodTouchFx.Sound(c.god, c.map, p.Position);
                GodTouch.Write(c, GodActKind.Blessing, reason);
                return true;
            }
            return false;
        }

        /// <summary>The light at or nearest the place (the one the sign was at).</summary>
        private static ThingWithComps LightAt(Map map, IntVec3 at, bool powered) =>
            map.listerBuildings.allBuildingsColonist.OfType<ThingWithComps>()
                .Where(b => b.Spawned && b.GetComp<CompGlower>() != null && (!powered || b.GetComp<CompPowerTrader>() != null) && b.Position.InHorDistOf(at, 3f))
                .OrderBy(b => b.Position.DistanceToSquared(at)).FirstOrDefault();

        /// <summary>The batteries on the same line as the flickering lights take a share of what they hold.</summary>
        private static bool Charge(TouchContext c, float share)
        {
            var light = LightAt(c.map, c.cell, true);
            var net = light?.GetComp<CompPowerTrader>()?.PowerNet;
            if (net?.batteryComps == null) return false;
            int n = 0;
            foreach (var bat in net.batteryComps.ToList())
            {
                float add = GodImpactMath.Charge(bat.Props.storedEnergyMax, bat.AmountCanAccept, share > 0f ? share : 0.25f);
                if (add < 1f) continue;
                bat.AddEnergy(add);
                GodTouchFx.Show(c.god, c.map, bat.parent.Position, TouchTier.Small, false);
                n++;
            }
            if (n == 0) return false;
            Show(c, light.Position, K("Charge"));
            return true;
        }

        /// <summary>The room's label for the words: the game's own name for what the room is, else "room".</summary>
        private static string RoomLabel(Room room) => room?.Role != null && room.Role != RoomRoleDefOf.None ? room.Role.label : "CC_GodImpact_Room".Translate().Resolve();

        /// <summary>The fires and torches in the room of the light that kept going out, emptied: they have to be fed and lit again.</summary>
        private static bool FiresOut(TouchContext c)
        {
            var light = LightAt(c.map, c.cell, false);
            var at = light?.Position ?? c.cell;
            // A light on a wall is in the room it lights.
            var room = at.GetRoom(c.map) ?? GenAdj.CellsAdjacentCardinal(at, Rot4.North, IntVec2.One).Where(x => x.InBounds(c.map)).Select(x => x.GetRoom(c.map))
                .Where(r => r != null).OrderBy(r => r.PsychologicallyOutdoors ? 1 : 0).FirstOrDefault();
            if (room == null) return false;
            bool outdoors = room.PsychologicallyOutdoors;
            int n = 0;
            foreach (var r in GodTouchEffects.Fires(c.map, IntVec3.Invalid, false)
                         .Where(f => outdoors ? f.parent.Position.InHorDistOf(at, 6f) : f.parent.GetRoom() == room).Take(8).ToList())
            {
                r.ConsumeFuel(r.Fuel);
                GodTouchFx.Show(c.god, c.map, r.parent.Position, TouchTier.Small, true);
                n++;
            }
            if (n == 0) return false;
            Show(c, at, K(outdoors ? "FiresOut_Out" : "FiresOut"), RoomLabel(room).Named("ROOM"));
            return true;
        }

        /// <summary>Rain within the day: it comes a few hours later (snow where it is freezing), with its own line.</summary>
        private static bool RainSoon(GodTouchBook b, TouchContext c, float most)
        {
            float hours = GodImpactMath.RainHours(most, Rand.Value);
            Run(b, Running(TouchEffect.Rain, c, Now + Mathf.RoundToInt(hours * GenDate.TicksPerHour)));
            return true;
        }

        private static bool RainStep(GodTouchRunning r, int now)
        {
            if (now < r.until) return true;
            var map = r.map;
            var w = map.weatherManager.curWeather;
            // Already wet: the rain came on its own.
            if (w != null && (w.rainRate > 0.1f || w.snowRate > 0.1f)) return false;
            bool cold = map.mapTemperature.OutdoorTemp <= 0f;
            var def = DefDatabase<WeatherDef>.GetNamedSilentFail(cold ? "SnowGentle" : "Rain");
            if (def == null) return false;
            map.weatherManager.TransitionTo(def);
            Told(r, IntVec3.Invalid, cold ? "CC_GodTouch_Snow" : "CC_GodTouch_Rain", false);
            return false;
        }

        /// <summary>The wild game walks off the land (the touch's own), and comes back after the days of its size.</summary>
        private static bool GameLeaves(GodTouchBook b, TouchContext c, float days)
        {
            var going = GodTouchEffects.WildGame(c.map).InRandomOrder().Take(6).ToList();
            if (going.Count == 0) return false;
            foreach (var p in going) p.mindState.exitMapAfterTick = Now + Rand.Range(60, 2500);
            var back = Running(TouchEffect.GameLeaves, c, Now + Mathf.RoundToInt(Mathf.Max(0.5f, days) * GenDate.TicksPerDay));
            back.value = going.Count;
            back.what = string.Join(",", going.Select(p => p.kindDef.defName).Distinct());
            Run(b, back);
            Show(c, going[0].Position, "CC_GodTouch_GameLeaves");
            return true;
        }

        private static bool GameBackStep(GodTouchRunning r, int now)
        {
            if (now < r.until) return true;
            var map = r.map;
            var kinds = (r.what ?? "").Split(',').Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n))
                .Where(k => k?.RaceProps != null && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race)).ToList();
            if (kinds.Count == 0 || !RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Animal)) return false;
            int n = Mathf.Clamp(Mathf.RoundToInt(r.value), 1, 6);
            Pawn first = null;
            for (int i = 0; i < n; i++)
            {
                var a = PawnGenerator.GeneratePawn(kinds.RandomElement());
                GenSpawn.Spawn(a, CellFinder.RandomClosewalkCellNear(entry, map, 4), map);
                first ??= a;
            }
            Told(r, first?.Position ?? entry, K("GameBack"), false);
            return false;
        }

        /// <summary>The colony's animals bear the cold for a few days (the touch's own frost-hardiness, on the animals).</summary>
        private static bool Hardy(TouchContext c, float days)
        {
            var def = GodTouchDefOf.Rimfolk_GodTouch_Hardy;
            var animals = c.map.mapPawns.SpawnedColonyAnimals.Where(a => !a.Dead && a.health != null && !GodAvatar.Is(a)).ToList();
            if (def == null || animals.Count == 0) return false;
            int ticks = Mathf.RoundToInt(Mathf.Max(1f, days) * GenDate.TicksPerDay);
            foreach (var a in animals) Give(a, def, ticks);
            Show(c, c.cell, K("Hardy"));
            return true;
        }

        /// <summary>The one who saw it (named in its words) sleeps badly that night.</summary>
        private static bool Sleepless(TouchContext c, Pawn who)
        {
            var one = who != null && c.saw.Contains(who) ? who : c.saw.FirstOrDefault();
            var def = GodTouchDefOf.Rimfolk_GodTouch_Sleepless;
            if (one == null || def == null || one.health == null) return false;
            Give(one, def, 0);
            Show(c, one.Position, "CC_GodTouch_Sleepless", one.LabelShort.Named("WHO"));
            return true;
        }

        /// <summary>A condition on someone, renewed (its time set where <paramref name="ticks"/> is given, else the def's own).</summary>
        private static Hediff Give(Pawn p, HediffDef def, int ticks)
        {
            if (p?.health == null || def == null || p.Dead) return null;
            var had = p.health.hediffSet.GetFirstHediffOfDef(def);
            if (had != null) p.health.RemoveHediff(had);
            var h = p.health.AddHediff(def);
            if (ticks > 0 && h?.TryGetComp<HediffComp_Disappears>() is HediffComp_Disappears d) d.ticksToDisappear = ticks;
            return h;
        }

        /// <summary>Everyone of the colony at the home (as many as the rules allow): the condition, and one line.</summary>
        private static bool Everyone(TouchContext c, HediffDef def, int ticks, string key)
        {
            var people = c.map.mapPawns.FreeColonistsSpawned.Where(Worship.Ours).Take(GodImpactMath.EveryoneMost).ToList();
            if (def == null || people.Count == 0) return false;
            foreach (var p in people) Give(p, def, ticks);
            Show(c, c.cell, key);
            return true;
        }

        private static int Hour(Map map) => GenLocalDate.HourOfDay(map);

        // ================= Morel =================

        /// <summary>The body the ring came up round: the one it is about, else a person's body at the place.</summary>
        private static Corpse BodyNear(Map map, IntVec3 at)
        {
            foreach (var x in GenRadial.RadialCellsAround(at, 1.5f, true))
            {
                if (!x.InBounds(map)) continue;
                foreach (var t in x.GetThingList(map))
                    if (t is Corpse body && body.InnerPawn?.RaceProps?.Humanlike == true)
                        return body;
            }
            return null;
        }

        /// <summary>1. The body inside the ring rots away to bones in a day or two (the game's own rot, moved on), and the ground inside the ring turns rich.</summary>
        private static bool RotAway(GodTouchBook b, TouchContext c, TouchAbout about)
        {
            var map = c.map;
            var body = about?.thing as Corpse ?? BodyNear(map, c.cell);
            var mid = body != null && body.Spawned && body.Map == map ? body.Position : c.cell;
            bool soil = GodTouchEffects.Soil(c, GenRadial.RadialCellsAround(mid, GodImpactMath.RingInside, true), 9);
            var rot = body?.TryGetComp<CompRottable>();
            bool rotting = body != null && body.Spawned && body.Map == map && rot != null && rot.Stage != RotStage.Dessicated;
            if (rotting)
            {
                var r = Running(TouchEffect.RotAway, c, Now + Mathf.RoundToInt(GodImpactMath.RotAwayDays * GenDate.TicksPerDay));
                r.cell = mid;
                r.thing = body;
                r.value = rot.RotProgress;
                Run(b, r);
            }
            if (!soil && !rotting) return false;
            string dead = body?.InnerPawn?.LabelShort ?? "";
            Show(c, mid, K(rotting ? soil ? "RotAway" : "RotAway_NoSoil" : "RotAway_Soil"), dead.Named("DEAD"));
            return true;
        }

        private static bool RotStep(GodTouchRunning r, int now)
        {
            var body = r.thing as Corpse;
            var rot = body?.TryGetComp<CompRottable>();
            if (body == null || body.Destroyed || !body.Spawned || body.Map != r.map || rot == null) return false;
            float want = GodImpactMath.RotAt(r.value, rot.PropsRot.TicksToDessicated, r.since, r.until, now);
            if (want > rot.RotProgress) rot.RotProgress = want;
            return now < r.until && rot.Stage != RotStage.Dessicated;
        }

        /// <summary>2. The rotten stack the white threads spread round is gone, and the floor under it clean (never a person's body).</summary>
        private static bool RotGone(TouchContext c)
        {
            var map = c.map;
            Thing rotten = null;
            foreach (var x in GenRadial.RadialCellsAround(c.cell, 1.5f, true))
            {
                if (!x.InBounds(map)) continue;
                rotten = x.GetThingList(map).FirstOrDefault(t => t.def.category == ThingCategory.Item && !(t is Corpse body && body.InnerPawn?.RaceProps?.Humanlike == true)
                                                                 && t.TryGetComp<CompRottable>() is CompRottable r && r.Stage != RotStage.Fresh);
                if (rotten != null) break;
            }
            if (rotten == null) return false;
            string what = rotten.LabelNoCount;
            var at = rotten.Position;
            rotten.Destroy(DestroyMode.Vanish);
            foreach (var x in GenRadial.RadialCellsAround(at, 1.5f, true).Where(x => x.InBounds(map)))
            foreach (var f in x.GetThingList(map).OfType<Filth>().ToList())
                f.Destroy(DestroyMode.Vanish);
            Show(c, at, K("RotGone"), what.Named("WHAT"));
            return true;
        }

        /// <summary>A memory of grief for a death: the one it is about, else any death of late.</summary>
        private static bool Mourning(Thought_Memory m, Pawn dead)
        {
            if (m?.def == null || m.MoodOffset() >= 0f) return false;
            string n = m.def.defName;
            bool death = n.IndexOf("Died", StringComparison.Ordinal) >= 0 || n.IndexOf("Death", StringComparison.Ordinal) >= 0
                         || n.IndexOf("Grief", StringComparison.Ordinal) >= 0 || n.IndexOf("Mourn", StringComparison.Ordinal) >= 0;
            if (dead != null) return m.otherPawn == dead || m.otherPawn == null && death && m.age < GenDate.TicksPerDay;
            return m.otherPawn != null && m.otherPawn.Dead || death;
        }

        /// <summary>
        /// 3 and 19. Those who mourn (the newly dead at the graves, or the one the cat stayed with to the end): each memory of
        /// grief ends sooner (GodImpactMath.GriefAge), and they know why (a small memory of it).
        /// </summary>
        private static bool GriefEased(TouchContext c, TouchAbout about)
        {
            var dead = about?.pawn;
            bool cat = dead != null;
            var took = new List<Pawn>();
            foreach (var p in c.map.mapPawns.FreeColonistsSpawned.Where(Worship.Ours).ToList())
            {
                var memories = p.needs?.mood?.thoughts?.memories;
                if (memories == null || p == dead) continue;
                bool eased = false;
                foreach (var m in memories.Memories.ToList())
                {
                    if (!Mourning(m, dead)) continue;
                    m.age = GodImpactMath.GriefAge(m.age, m.DurationTicks);
                    eased = true;
                }
                if (!eased) continue;
                CouncilGameComponent.GiveThought(p, GodImpactDefOf.Rimfolk_GodTouch_GriefEased, stage: cat ? 1 : 0);
                took.Add(p);
            }
            if (took.Count == 0) return false;
            Show(c, c.cell, K(cat ? "GriefEased_Cat" : "GriefEased_Ring"), Names(took).Named("WHO"), (dead?.LabelShort ?? "").Named("DEAD"));
            return true;
        }

        /// <summary>The food things kept in a room (fresh, with a rot to them).</summary>
        private static IEnumerable<Thing> FoodIn(Room room, Map map) =>
            room.ContainedAndAdjacentThings.Where(t => t.Spawned && t.def.category == ThingCategory.Item && t.GetRoom() == room
                                                       && t.TryGetComp<CompRottable>() is CompRottable r && r.Stage == RotStage.Fresh);

        /// <summary>5. The food stored in the room of the white threads keeps a little less long for a few days, never rotting from it.</summary>
        private static bool KeepsLess(GodTouchBook b, TouchContext c)
        {
            var room = c.cell.GetRoom(c.map);
            if (room == null || room.PsychologicallyOutdoors || room.IsHuge || !FoodIn(room, c.map).Any()) return false;
            Run(b, Running(TouchEffect.KeepsLess, c, Now + Mathf.RoundToInt(GodImpactMath.KeepsLessDays * GenDate.TicksPerDay)));
            Show(c, c.cell, K("KeepsLess"), RoomLabel(room).Named("ROOM"));
            return true;
        }

        private static bool KeepsLessStep(GodTouchRunning r, int now)
        {
            var room = r.cell.GetRoom(r.map);
            if (room == null || room.PsychologicallyOutdoors) return false;
            int ticks = now - r.last;
            r.last = now;
            foreach (var t in FoodIn(room, r.map).ToList())
            {
                var rot = t.TryGetComp<CompRottable>();
                rot.RotProgress = GodImpactMath.KeepsLess(rot.RotProgress, rot.PropsRot.TicksToRotStart, ticks, GenDate.TicksPerHour);
            }
            return now < r.until;
        }

        // ================= Hush =================

        /// <summary>The grave at the place (the one the sign was on).</summary>
        private static Building_Grave GraveAt(Map map, IntVec3 at) =>
            map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>().Where(g => g.Spawned && g.HasCorpse && g.OccupiedRect().ExpandedBy(2).Contains(at))
                .OrderBy(g => g.Position.DistanceToSquared(at)).FirstOrDefault();

        /// <summary>The living of the colony closest to a dead person: kin and partners first, then those who thought most of them.</summary>
        private static List<Pawn> Closest(Map map, Pawn dead, int most)
        {
            if (dead == null) return new List<Pawn>();
            return map.mapPawns.FreeColonistsSpawned.Where(p => p != dead && Worship.Ours(p) && p.relations != null)
                .Select(p => (p, kin: p.GetRelations(dead).Any(), opinion: p.relations.OpinionOf(dead)))
                .Where(x => x.kin || x.opinion >= GodImpactMath.CloseOpinion)
                .OrderByDescending(x => (x.kin ? 100 : 0) + x.opinion).Take(most).Select(x => x.p).ToList();
        }

        /// <summary>6. Those closest to the one in the grave the frost lay on sleep well that night and wake rested.</summary>
        private static bool RestKin(TouchContext c, TouchAbout about)
        {
            var grave = about?.thing as Building_Grave ?? GraveAt(c.map, c.cell);
            var dead = grave?.Corpse?.InnerPawn;
            var kin = Closest(c.map, dead, GodImpactMath.KinMost);
            if (kin.Count == 0) return false;
            int ticks = GodImpactMath.HoursUntil(Hour(c.map), 8) * GenDate.TicksPerHour;
            foreach (var p in kin) Give(p, GodImpactDefOf.Rimfolk_GodTouch_GraveRest, ticks);
            Show(c, grave.Position, K("RestKin"), Names(kin).Named("WHO"), dead.LabelShort.Named("DEAD"));
            return true;
        }

        /// <summary>8. A few tender crops along the line of frost take the cold: the game's own cold, which kills a tender crop.</summary>
        private static bool FrostLine(TouchContext c, TouchAbout about)
        {
            var line = about?.cells;
            if (line == null || line.Count == 0) return false;
            var map = c.map;
            var near = new HashSet<IntVec3>();
            foreach (var x in line)
            foreach (var y in GenRadial.RadialCellsAround(x, GodImpactMath.FrostLineReach, true))
                if (y.InBounds(map))
                    near.Add(y);
            var crops = near.Select(x => x.GetPlant(map)).Where(p => p != null && p.IsCrop && p.def.plant.dieIfLeafless && !p.LeaflessNow && p.LifeStage != PlantLifeStage.Sowing)
                .Distinct().InRandomOrder().Take(GodImpactMath.FrostCropsMost).ToList();
            if (crops.Count == 0) return false;
            var at = crops[0].Position;
            foreach (var p in crops)
            {
                GodTouchFx.Show(c.god, map, p.Position, TouchTier.Small, true);
                p.MakeLeafless(Plant.LeaflessCause.Cold, false);
            }
            Show(c, at, K("FrostLine"), crops.Count.Named("COUNT"));
            return true;
        }

        /// <summary>The room behind the wall the frost writing was by: past the wall from where it was written.</summary>
        private static Room RoomBehind(Map map, IntVec3 at, out IntVec3 inside)
        {
            inside = IntVec3.Invalid;
            var wall = at + IntVec3.North;
            foreach (var x in new[] { wall + IntVec3.North, wall + IntVec3.East, wall + IntVec3.West })
            {
                if (!x.InBounds(map)) continue;
                var room = x.GetRoom(map);
                if (room == null || room.PsychologicallyOutdoors || room.UsesOutdoorTemperature || room.IsHuge) continue;
                inside = x;
                return room;
            }
            return null;
        }

        /// <summary>10. The room behind the wall with the frost writing goes cold, and stays cold for a day.</summary>
        private static bool ColdRoom(GodTouchBook b, TouchContext c)
        {
            var room = RoomBehind(c.map, c.cell, out var inside);
            if (room == null) return false;
            var r = Running(TouchEffect.ColdRoom, c, Now + Mathf.RoundToInt(GodImpactMath.ColdRoomDays * GenDate.TicksPerDay));
            r.cell = inside;
            Run(b, r);
            room.Temperature = GodImpactMath.ColdStep(room.Temperature);
            Show(c, c.cell, K("ColdRoom"), RoomLabel(room).Named("ROOM"));
            return true;
        }

        private static bool ColdStep(GodTouchRunning r, int now)
        {
            var room = r.cell.GetRoom(r.map);
            if (room == null || room.PsychologicallyOutdoors || room.UsesOutdoorTemperature) return false;
            room.Temperature = GodImpactMath.ColdStep(room.Temperature);
            return now < r.until;
        }

        // ================= Helios =================

        /// <summary>11. Those who stood facing the sunrise are fully rested.</summary>
        private static bool Rested(TouchContext c, TouchAbout about)
        {
            var who = (about?.pawns ?? c.saw).Where(p => p != null && !p.Dead && p.needs?.rest != null).ToList();
            if (who.Count == 0) return false;
            foreach (var p in who) p.needs.rest.CurLevel = p.needs.rest.MaxLevel;
            Show(c, c.cell, K("Rested"), Names(who).Named("WHO"));
            return true;
        }

        /// <summary>12. The crops in the open grow a day ahead, and the solar panels give their most until sundown.</summary>
        private static bool LongNoon(GodTouchBook b, TouchContext c)
        {
            var map = c.map;
            int n = 0;
            foreach (var p in GodTouchEffects.Crops(map, IntVec3.Invalid, 0f).Where(p => p.Growth < 1f && p.def.plant.growDays > 0f && !p.Position.Roofed(map)).ToList())
            {
                p.Growth = Mathf.Min(1f, p.Growth + 1f / p.def.plant.growDays);
                p.DirtyMapMesh(map);
                if (n % 8 == 0) GodTouchFx.Show(c.god, map, p.Position, TouchTier.Small, false);
                n++;
            }
            int hours = GodImpactMath.HoursOfLight(Hour(map));
            bool panels = hours > 0 && map.listerBuildings.allBuildingsColonist.Any(x => x.GetComp<CompPowerPlantSolar>() != null);
            if (panels) Run(b, Running(TouchEffect.LongNoon, c, Now + hours * GenDate.TicksPerHour));
            if (n == 0 && !panels) return false;
            Show(c, c.cell, K(n > 0 && panels ? "LongNoon" : n > 0 ? "LongNoon_Crops" : "LongNoon_Panels"));
            return true;
        }

        /// <summary>13. The open ground a few degrees warmer for the afternoon (its own condition, shown with the others).</summary>
        private static bool Warmth(TouchContext c)
        {
            int hours = GodImpactMath.HoursOfLight(Hour(c.map));
            var def = DefDatabase<GameConditionDef>.GetNamedSilentFail(GameCondition_GodWarmth.DefName);
            if (hours <= 0 || def == null || c.map.gameConditionManager.ConditionIsActive(def)) return false;
            c.map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(def, hours * GenDate.TicksPerHour));
            Show(c, c.cell, K("Warmth"));
            return true;
        }

        // ================= Selene =================

        /// <summary>
        /// 15. The door kept on a night with no moon: while its keeper stood there, the threats that came at the home waited for
        /// daylight (<see cref="HoldTillDawn"/>); its words come at dawn, and say so only when something was held back.
        /// </summary>
        private static bool NightWard(GodTouchBook b, TouchContext c, TouchAbout about)
        {
            if (b.DaysSince("ward|" + Gods.HomeKey(c.map), Now) > 0.5f) return true;
            var keeper = about?.who ?? c.saw.FirstOrDefault();
            Show(c, c.cell, K("NightWard"), (keeper?.LabelShort ?? "").Named("PAWN"));
            return true;
        }

        /// <summary>
        /// A threat about to come at a home where the dark face is being kept at the door (OwnSelene.KeptNow): it waits on the
        /// game's own queue until a little after the keeper's night ends. True when held.
        /// </summary>
        public static bool HoldTillDawn(FiringIncident fi)
        {
            var map = fi?.parms?.target as Map;
            if (fi?.def?.Worker == null || map == null || !Homes.IsHome(map) || fi.sourceQuestPart != null || fi.parms.quest != null) return false;
            bool threat = fi.def.category == IncidentCategoryDefOf.ThreatBig || fi.def.category == IncidentCategoryDefOf.ThreatSmall || fi.def.Worker is IncidentWorker_Raid;
            if (!threat || !OwnSelene.KeptNow(map, out int until)) return false;
            int at = Math.Max(Now + GenDate.TicksPerHour, until + Rand.Range(GenDate.TicksPerHour / 2, GenDate.TicksPerHour * 2));
            if (!Find.Storyteller.incidentQueue.Add(new QueuedIncident(fi, at, 6 * GenDate.TicksPerHour))) return false;
            GodTouchBook.Instance?.Set("ward|" + Gods.HomeKey(map), Now);
            return true;
        }

        /// <summary>16. Everyone at the home sleeps deeper that night.</summary>
        private static bool MoonSleep(TouchContext c) =>
            Everyone(c, GodImpactDefOf.Rimfolk_GodTouch_MoonSleep, GodImpactMath.HoursUntil(Hour(c.map), 8) * GenDate.TicksPerHour, K("MoonSleep"));

        // ================= Tamiu =================

        /// <summary>18. The one near death feels less pain while the cat stays (it goes with the cat: <see cref="CatGone"/>).</summary>
        private static bool PainEased(TouchContext c, TouchAbout about)
        {
            var p = about?.pawn;
            if (p == null || p.Dead || Give(p, GodImpactDefOf.Rimfolk_GodTouch_PainEased, GenDate.TicksPerDay) == null) return false;
            Show(c, p.PositionHeld, K("PainEased"), p.LabelShort.Named("WHO"));
            return true;
        }

        /// <summary>The cat at the threshold got up and left (OwnTamiu): the pain it eased comes back.</summary>
        internal static void CatGone(Pawn p)
        {
            var def = GodImpactDefOf.Rimfolk_GodTouch_PainEased;
            var h = def == null ? null : p?.health?.hediffSet?.GetFirstHediffOfDef(def);
            if (h != null) p.health.RemoveHediff(h);
        }

        /// <summary>20. The one who came through heals faster for a few days.</summary>
        private static bool HealFaster(TouchContext c, TouchAbout about)
        {
            var p = about?.pawn;
            if (p == null || p.Dead || Give(p, GodImpactDefOf.Rimfolk_GodTouch_HealFaster, 0) == null) return false;
            Show(c, p.PositionHeld, K("HealFaster"), p.LabelShort.Named("WHO"));
            return true;
        }

        // ================= Revel =================

        /// <summary>23. The binge ends sooner (within a couple of hours), and the binger's recreation is full.</summary>
        private static bool BingeEnds(TouchContext c, TouchAbout about)
        {
            var p = about?.pawn;
            if (p == null || p.Dead || !p.Spawned) return false;
            if (p.needs?.joy != null) p.needs.joy.CurLevel = p.needs.joy.MaxLevel;
            var ms = p.MentalState;
            bool binge = ms != null && RevelGoods.Binging(p);
            if (binge)
            {
                int soon = ms.Age + 2 * GenDate.TicksPerHour;
                if (ms.forceRecoverAfterTicks < 0 || ms.forceRecoverAfterTicks > soon) ms.forceRecoverAfterTicks = soon;
            }
            Show(c, p.Position, K(binge ? "BingeEnds" : "BingeEnds_Over"), p.LabelShort.Named("PAWN"));
            return true;
        }

        /// <summary>24. A drink of the horned man's own kind (the cup he came with, of the colony's kind: RevelGoods) left where he stood.</summary>
        private static bool Drink(TouchContext c, TouchAbout about)
        {
            var def = about?.thing?.def ?? RevelGoods.Pick(c.map, true);
            if (def == null) return false;
            var t = ThingMaker.MakeThing(def);
            t.stackCount = 1;
            if (!GenPlace.TryPlaceThing(t, c.cell, c.map, ThingPlaceMode.Near, out var placed) || placed == null) return false;
            GodTouchFx.Show(c.god, c.map, placed.Position, TouchTier.Small, false);
            Show(c, placed.Position, K("Drink"), placed.LabelCap.Named("WHAT"));
            return true;
        }

        /// <summary>26. Those who smelled the sweet smoke are cheered for the day.</summary>
        private static bool Cheered(TouchContext c)
        {
            var took = c.saw.Where(p => p.needs?.mood != null).Take(GodTouchMath.BlessAtMost).ToList();
            if (took.Count == 0) return false;
            foreach (var p in took) CouncilGameComponent.GiveThought(p, GodImpactDefOf.Rimfolk_GodTouch_SweetSmoke);
            Show(c, c.cell, K("Cheered"), Names(took).Named("WHO"));
            return true;
        }

        // ================= Ducat =================

        /// <summary>29. A little silver under the table the golden beetle walked across.</summary>
        private static bool Silver(TouchContext c)
        {
            var table = c.map.listerBuildings.allBuildingsColonist.Where(x => x.Spawned && x.def.IsTable && x.OccupiedRect().ExpandedBy(1).Contains(c.cell)).FirstOrDefault();
            var t = ThingMaker.MakeThing(ThingDefOf.Silver);
            t.stackCount = GodImpactMath.Silver(Rand.Value);
            if (!GenPlace.TryPlaceThing(t, table?.Position ?? c.cell, c.map, ThingPlaceMode.Near, out var placed) || placed == null) return false;
            Show(c, placed.Position, K("Silver"), placed.stackCount.Named("COUNT"));
            return true;
        }

        // ================= Pip =================

        /// <summary>The hour until the next dawn (six o'clock).</summary>
        private static int ToDawn(Map map) => GodImpactMath.HoursUntil(Hour(map), 6) * GenDate.TicksPerHour;

        /// <summary>31. The light glowing with no power keeps glowing all night.</summary>
        private static bool StaysLit(GodTouchBook b, TouchContext c)
        {
            var light = LightAt(c.map, c.cell, true);
            int until = Now + ToDawn(c.map);
            if (light == null || !GodOmens.Hold(c.map, light, until, true)) return false;
            var r = Running(TouchEffect.StaysLit, c, until);
            r.thing = light;
            Run(b, r);
            Show(c, light.Position, K("StaysLit"), light.LabelNoCount.Named("LIGHT"));
            return true;
        }

        /// <summary>A light held lit or dark until its time, held again after a load (the sign's hold isn't saved).</summary>
        private static bool HoldStep(GodTouchRunning r, int now)
        {
            if (now >= r.until) return false;
            bool lit = r.Effect == TouchEffect.StaysLit;
            if (r.thing is ThingWithComps light && light.Spawned && !GodOmens.Held(light)) GodOmens.Hold(r.map, light, r.until, lit);
            if (!lit && r.cell.IsValid && !GodSky.On(r.map, SkySign.Dark)) GodSky.Stay(r.map, SkySign.Dark, r.cell, r.until - now, 7f);
            return lit ? r.thing != null && r.thing.Spawned : true;
        }

        /// <summary>33. The machine that ran on its own hour draws no power for it (its fuel given back at the end).</summary>
        private static bool FreeHour(GodTouchBook b, TouchContext c, TouchAbout about)
        {
            var own = OwnHappenings.Book;
            var t = own?.wontStop ?? about?.thing;
            if (t == null || !t.Spawned || t.Map != c.map) return false;
            int until = Math.Max(Now + GenDate.TicksPerHour / 2, own?.wontStop == t ? own.wontStopUntil : Now + GenDate.TicksPerHour);
            var r = Running(TouchEffect.FreeHour, c, until);
            r.thing = t;
            r.value = t.TryGetComp<CompRefuelable>()?.Fuel ?? -1f;
            Run(b, r);
            Show(c, t.Position, K("FreeHour"), t.LabelNoCount.Named("THING"));
            return true;
        }

        private static bool FreeStep(GodTouchRunning r, int now)
        {
            if (r.thing == null || r.thing.Destroyed || !r.thing.Spawned) return false;
            if (now < r.until) return true;
            var fuel = r.thing.TryGetComp<CompRefuelable>();
            if (fuel != null && r.value >= 0f && fuel.Fuel < r.value) fuel.Refuel(r.value - fuel.Fuel);
            return false;
        }

        // ================= Smoulder =================

        /// <summary>The chunk of the rock of the place: the nearest natural rock's, else the land's own rock.</summary>
        private static ThingDef ChunkHere(Map map, IntVec3 at)
        {
            foreach (var x in GenRadial.RadialCellsAround(at, 15f, true))
            {
                if (!x.InBounds(map)) continue;
                var chunk = x.GetEdifice(map)?.def?.building is BuildingProperties b && b.isNaturalRock ? b.mineableThing : null;
                if (chunk != null && chunk.IsWithinCategory(ThingCategoryDefOf.StoneChunks)) return chunk;
            }
            return Find.World.NaturalRockTypesIn(map.Tile).Select(r => r.building?.mineableThing).FirstOrDefault(d => d != null);
        }

        /// <summary>35. A few chunks of the place's own stone come down where the rock rumbled, on cells nobody stands on.</summary>
        private static bool StoneChunks(TouchContext c)
        {
            var map = c.map;
            var chunk = ChunkHere(map, c.cell);
            if (chunk == null) return false;
            int n = GodImpactMath.Chunks(Rand.Value), placed = 0;
            var first = IntVec3.Invalid;
            foreach (var x in GenRadial.RadialCellsAround(c.cell, 4f, true).Where(x => x.InBounds(map) && x.Standable(map) && x.GetFirstPawn(map) == null
                                                                                         && x.GetFirstItem(map) == null && x.GetEdifice(map) == null).InRandomOrder())
            {
                if (placed >= n) break;
                GenSpawn.Spawn(ThingMaker.MakeThing(chunk), x, map);
                FleckMaker.ThrowDustPuffThick(x.ToVector3Shifted(), map, Rand.Range(1f, 1.6f), new Color(0.6f, 0.58f, 0.52f, 0.7f));
                if (!first.IsValid) first = x;
                placed++;
            }
            if (placed == 0) return false;
            Show(c, first, K("StoneChunks"), placed.Named("COUNT"));
            return true;
        }

        /// <summary>36. The miners who dreamt of something stretching under the mountain dig faster for a day.</summary>
        private static bool DigFaster(TouchContext c, TouchAbout about)
        {
            var miners = (about?.pawns ?? c.saw).Where(p => p != null && !p.Dead && p.health != null).ToList();
            var def = GodImpactDefOf.Rimfolk_GodTouch_DeepDream;
            if (miners.Count == 0 || def == null) return false;
            foreach (var p in miners) Give(p, def, 0);
            Show(c, c.cell, K("DigFaster"), Names(miners).Named("WHO"));
            return true;
        }

        /// <summary>The snow gone from the warm ground round the steam.</summary>
        private static void Melt(Map map, IntVec3 at)
        {
            foreach (var x in GenRadial.RadialCellsAround(at, GodImpactMath.WarmRadius, true))
                if (x.InBounds(map) && map.snowGrid.GetDepth(x) > 0.01f)
                    map.snowGrid.SetDepth(x, 0f);
        }

        /// <summary>37. The ground round the steam stays warm a few days: no snow lies on it, and the plants near it grow on.</summary>
        private static bool WarmGround(GodTouchBook b, TouchContext c)
        {
            var map = c.map;
            int grown = 0;
            foreach (var x in GenRadial.RadialCellsAround(c.cell, GodImpactMath.WarmRadius + 1f, true).Where(x => x.InBounds(map)))
            {
                var p = x.GetPlant(map);
                if (p?.def?.plant == null || p.Growth >= 1f || p.def.plant.IsTree) continue;
                p.Growth = Mathf.Min(1f, p.Growth + GodImpactMath.WarmGrowth);
                p.DirtyMapMesh(map);
                grown++;
            }
            Melt(map, c.cell);
            foreach (var x in GenRadial.RadialCellsAround(c.cell, 3f, true).Where(x => x.InBounds(map)).InRandomOrder().Take(6)) FleckMaker.ThrowHeatGlow(x, map, 1f);
            Run(b, Running(TouchEffect.WarmGround, c, Now + Mathf.RoundToInt(GodImpactMath.WarmDays * GenDate.TicksPerDay)));
            Show(c, c.cell, K(grown > 0 ? "WarmGround" : "WarmGround_Bare"));
            return true;
        }

        private static bool WarmStep(GodTouchRunning r, int now)
        {
            Melt(r.map, r.cell);
            if (Rand.Chance(0.1f) && r.cell.InBounds(r.map)) FleckMaker.ThrowHeatGlow(r.cell, r.map, 0.8f);
            return now < r.until;
        }

        // ================= Cirrus =================

        /// <summary>38. While the wind is dead (the calm's own stillness, which stops the turbines), fire doesn't spread.</summary>
        private static bool StillAir(GodTouchBook b, TouchContext c)
        {
            var calm = c.map.gameConditionManager.GetActiveCondition<GameCondition_GodPresence>();
            int until = calm != null && !calm.Permanent ? Now + calm.TicksLeft : Now + 4 * GenDate.TicksPerHour;
            Run(b, Running(TouchEffect.StillAir, c, until));
            Show(c, c.cell, K("StillAir"));
            return true;
        }

        // ================= Hollin, Bellwether and the beasts =================

        /// <summary>44. The anima tree's grass spreads a little, and meditating at it is better for a day (StatPart_GodTouch).</summary>
        private static bool AnimaHum(GodTouchBook b, TouchContext c)
        {
            var map = c.map;
            var tree = GodOmens.AnimaTrees(map).OrderBy(t => t.Position.DistanceToSquared(c.cell)).FirstOrDefault();
            if (tree == null) return false;
            var grass = Thing("Plant_GrassAnima");
            int n = 0;
            if (grass?.plant != null)
                foreach (var x in GenRadial.RadialCellsAround(tree.Position, 6f, true).Where(x => x.InBounds(map) && !x.InHorDistOf(tree.Position, 1.9f)).InRandomOrder())
                {
                    if (n >= GodImpactMath.AnimaGrassMost) break;
                    if (x.GetPlant(map) != null || x.GetEdifice(map) != null || x.GetFirstItem(map) != null || x.Fogged(map) || !grass.CanEverPlantAt(x, map)) continue;
                    var p = (Plant)ThingMaker.MakeThing(grass);
                    p.Growth = 1f;
                    GenSpawn.Spawn(p, x, map);
                    n++;
                }
            var r = Running(TouchEffect.AnimaHum, c, Now + Mathf.RoundToInt(GodImpactMath.AnimaDays * GenDate.TicksPerDay));
            r.thing = tree;
            Run(b, r);
            Show(c, tree.Position, K(n > 0 ? "AnimaHum" : "AnimaHum_NoGrass"));
            return true;
        }

        /// <summary>45. One of the colony's herd animals is with young: the game's own pregnancy, by a male of its kind where there is one.</summary>
        private static bool WithYoung(TouchContext c)
        {
            var herd = c.map.mapPawns.SpawnedColonyAnimals.Where(a => !a.Dead && a.health != null && !GodAvatar.Is(a)).ToList();
            var mothers = herd.Where(a => a.gender == Gender.Female && a.RaceProps.Animal && a.RaceProps.gestationPeriodDays > 0f && a.ageTracker?.CurLifeStage?.reproductive == true
                                          && a.TryGetComp<CompEggLayer>() == null && !AnimalFacts.Pregnant(a))
                .OrderByDescending(a => herd.Any(m => m.def == a.def && m.gender == Gender.Male) ? 1 : 0).ThenBy(_ => Rand.Value).ToList();
            var mother = mothers.FirstOrDefault();
            if (mother == null || HediffDefOf.Pregnant == null) return false;
            var father = herd.Where(m => m.def == mother.def && m.gender == Gender.Male && m.ageTracker?.CurLifeStage?.reproductive == true).RandomElementWithFallback();
            if (!(HediffMaker.MakeHediff(HediffDefOf.Pregnant, mother) is Hediff_Pregnant pregnant)) return false;
            pregnant.SetParents(null, father, null);
            mother.health.AddHediff(pregnant);
            Show(c, mother.Position, K("WithYoung"), mother.LabelIndefinite().Named("WHO"));
            return true;
        }

        // ================= The eerie signs =================

        /// <summary>50. The spot the light didn't reach stays dark for the night, its light held out until dawn.</summary>
        private static bool StaysDark(GodTouchBook b, TouchContext c)
        {
            var light = GodOmens.Lights(c.map).Where(l => l.Position.InHorDistOf(c.cell, 2.5f)).OrderBy(l => l.Position.DistanceToSquared(c.cell)).FirstOrDefault();
            int until = Now + ToDawn(c.map);
            GodSky.Stay(c.map, SkySign.Dark, c.cell, until - Now, 7f);
            if (light != null) GodOmens.Hold(c.map, light, until, false);
            var r = Running(TouchEffect.StaysDark, c, until);
            r.thing = light;
            Run(b, r);
            Show(c, c.cell, K(light != null ? "StaysDark" : "StaysDark_NoLight"), (light?.LabelNoCount ?? "").Named("LIGHT"));
            return true;
        }

        /// <summary>52. The one closest to the one in the grave the pale sand lay on dreams of them kindly, the next time they sleep.</summary>
        private static bool KindDream(GodTouchBook b, TouchContext c, TouchAbout about)
        {
            var grave = GraveAt(c.map, c.cell);
            var dead = grave?.Corpse?.InnerPawn;
            var one = Closest(c.map, dead, 1).FirstOrDefault();
            if (one == null) return false;
            var r = Running(TouchEffect.KindDream, c, Now + Mathf.RoundToInt(1.5f * GenDate.TicksPerDay));
            r.pawn = one;
            r.what = dead.LabelShort;
            Run(b, r);
            return true;
        }

        private static bool DreamStep(GodTouchRunning r, int now)
        {
            var p = r.pawn;
            if (p == null || p.Dead || now > r.until) return false;
            if (!p.Spawned) return true;
            if (!p.Awake())
            {
                r.flag = true;
                return true;
            }
            if (!r.flag) return true;
            Dreamt(p, r.what ?? "");
            Told(r, p.Position, K("KindDream"), false, p.LabelShort.Named("PAWN"), (r.what ?? "").Named("DEAD"));
            return false;
        }

        private static List<ThoughtDef> dreamThoughts;

        /// <summary>The kind dream: the dream book, the journal, and the game's dream of someone lost, with its words.</summary>
        private static void Dreamt(Pawn p, string dead)
        {
            string text = "CC_GodImpact_KindDream_Dream".Translate(dead.Named("DEAD")).Resolve();
            var comp = CouncilGameComponent.Instance;
            var dreams = comp?.dreams;
            if (dreams != null)
            {
                dreams.recent.Add(new DreamRecord { pawn = p, name = p.LabelShort, text = text, kind = DreamKind.Lost, tick = Now });
                while (dreams.recent.Count > 80) dreams.recent.RemoveAt(0);
            }
            comp?.journals?.WriteText(p, text);
            var td = CouncilDefOf.Council_Dream_Lost;
            var memories = p.needs?.mood?.thoughts?.memories;
            if (td == null || memories == null || !(ColonyCouncilMod.Settings?.MoodEffects ?? true)) return;
            dreamThoughts ??= DefDatabase<ThoughtDef>.AllDefsListForReading.Where(d => d.ThoughtClass == typeof(Thought_Dream)).ToList();
            foreach (var d in dreamThoughts) memories.RemoveMemoriesOfDef(d);
            if (!(ThoughtMaker.MakeThought(td) is Thought_Memory m)) return;
            if (m is Thought_Dream dream) dream.dream = text;
            memories.TryGainMemory(m, null);
        }

        /// <summary>53. The two who had the same dream think better of each other.</summary>
        private static bool SameDream(TouchContext c, TouchAbout about)
        {
            var a = about?.pawn ?? about?.who;
            var o = about?.other;
            var def = GodImpactDefOf.Rimfolk_GodTouch_SameDream;
            if (a == null || o == null || a == o || a.Dead || o.Dead || def == null) return false;
            // apart: each had the dream in their own bed the same night; they speak of it when next together (GodOmens.Dream).
            CouncilGameComponent.GiveThought(a, def, o);
            CouncilGameComponent.GiveThought(o, def, a);
            Show(c, a.PositionHeld, K("SameDream"), a.LabelShort.Named("PAWN"), o.LabelShort.Named("OTHER"));
            return true;
        }

        /// <summary>55. The work named for the god is admired more: its beauty a little higher (StatPart_GodTouch).</summary>
        private static bool Admire(GodTouchBook b, TouchContext c, TouchAbout about)
        {
            var art = about?.thing;
            if (art == null || art.Destroyed) return false;
            if (!b.admired.Contains(art))
            {
                b.admired.Add(art);
                while (b.admired.Count > GodImpactMath.AdmiredKept) b.admired.RemoveAt(0);
                Refresh(b);
            }
            string title = art.TryGetComp<CompArt>() is CompArt ca && ca.Active && !ca.Title.NullOrEmpty() ? ca.Title : art.LabelNoCount;
            Show(c, art.PositionHeld, K("Admired"), title.Named("TITLE"), (about.pawn?.LabelShort ?? "").Named("PAWN"));
            return true;
        }

        // ================= Dev mode =================

        /// <summary>
        /// Dev mode: what an occurrence at <paramref name="cell"/> would be about, as near as the place gives it: the one selected
        /// (else the nearest of the colony) as the one it is about and the one who saw it, the next nearest as another, the thing
        /// selected or lying there, everyone near, and a line of frost across the place.
        /// </summary>
        internal static TouchAbout DevAbout(Map map, IntVec3 cell, string key, List<Pawn> near)
        {
            var selected = Find.Selector.SingleSelectedThing;
            var byDistance = near.OrderBy(x => x.Position.DistanceToSquared(cell)).ToList();
            var pawn = selected as Pawn ?? byDistance.FirstOrDefault();
            var line = new List<IntVec3>();
            for (int dx = -12; dx <= 12; dx++)
            {
                var x = cell + new IntVec3(dx, 0, 0);
                if (x.InBounds(map)) line.Add(x);
            }
            return new TouchAbout
            {
                map = map, key = key, at = Now, who = pawn, pawn = pawn, other = byDistance.FirstOrDefault(x => x != pawn),
                thing = selected != null && !(selected is Pawn) ? selected : cell.GetThingList(map).FirstOrDefault(t => t.def.category == ThingCategory.Item || t is Building),
                pawns = near, cells = line
            };
        }

        /// <summary>The god each covered occurrence is mostly of, for the dev action (a shared sign may be another's in a game).</summary>
        internal static string DevGod(string key)
        {
            if (key.StartsWith("CC_GodHush_", StringComparison.Ordinal)) return "Hush";
            if (key.StartsWith("CC_GodHelios_", StringComparison.Ordinal)) return "Helios";
            if (key.StartsWith("CC_GodSelene_", StringComparison.Ordinal)) return "Selene";
            if (key.StartsWith("CC_GodTamiu_", StringComparison.Ordinal)) return "Mog";
            if (key.StartsWith("CC_GodRevel_", StringComparison.Ordinal)) return "Revel";
            switch (key.Substring(key.LastIndexOf('_') + 1))
            {
                case "RingBody": case "RingStores": case "MournerGone": case "FairyRing": case "Threads": case "Earth": return "Morel";
                case "FrostWriting": case "Silence": case "WhiteFigure": case "Knock": case "Footprints": return "Hush";
                case "StillSun": case "HeatHaze": return "Helios";
                case "BigMoon": return "Selene";
                case "CatsFacing": case "GraveCat": case "GoldEyes": case "Sand": return "Mog";
                case "Spilled": case "SweetSmoke": return "Revel";
                case "Coin": case "Glint": case "Beetle": return "Ducat";
                case "Flicker": case "Unpowered": case "Static": case "WontStop": case "Candle": return "Pip";
                case "Rumble": case "Stretch": case "Steam": case "Name": case "Shadow": return "Smoulder";
                case "Calm": case "Thunder": case "VastShadow": case "Star": return "Cirrus";
                case "Drift": case "AnimaHum": case "DryadsStill": return "Hollin";
                case "Dark": case "Whisper": return "Undertow";
                case "Croak": return "Murmur";
                default: return "Bellwether";
            }
        }
    }

    /// <summary>
    /// A work named for a god, admired since (its beauty a little higher), and an anima tree humming today (meditating at it
    /// a little better): shown in the stat's own breakdown with why. Added to the game's Beauty and MeditationFocusStrength.
    /// </summary>
    public class StatPart_GodTouch : StatPart
    {
        private bool Beauty => parentStat == StatDefOf.Beauty;

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!req.HasThing) return;
            if (Beauty)
            {
                if (val > 0f && GodImpact.Admired(req.Thing)) val *= GodImpactMath.AdmiredFactor;
            }
            else if (GodImpact.Humming(req.Thing)) val += GodImpactMath.AnimaFocus;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!req.HasThing) return null;
            if (Beauty) return GodImpact.Admired(req.Thing) ? "CC_GodImpact_Stat_Admired".Translate(GodImpactMath.AdmiredFactor.ToStringPercent()).Resolve() : null;
            return GodImpact.Humming(req.Thing) ? "CC_GodImpact_Stat_Humming".Translate(GodImpactMath.AnimaFocus.ToStringPercent()).Resolve() : null;
        }
    }

    /// <summary>Adds the gods' touch's part to the game's beauty and meditation focus, once the defs are loaded.</summary>
    [StaticConstructorOnStartup]
    public static class GodTouchStats
    {
        static GodTouchStats()
        {
            try
            {
                Add(StatDefOf.Beauty);
                Add(DefDatabase<StatDef>.GetNamedSilentFail("MeditationFocusStrength"));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the touch's part in beauty and meditation", e);
            }
        }

        private static void Add(StatDef stat)
        {
            if (stat == null) return;
            stat.parts ??= new List<StatPart>();
            if (!stat.parts.Any(p => p is StatPart_GodTouch)) stat.parts.Add(new StatPart_GodTouch { parentStat = stat });
        }
    }

    /// <summary>
    /// The open ground a few degrees warmer for an afternoon after the air shimmered over it at noon (GodImpact, proposal 13):
    /// shown with the game's other conditions, its warmth eased in and out.
    /// </summary>
    public class GameCondition_GodWarmth : GameCondition
    {
        public const string DefName = "Rimfolk_GodTouch_WarmAfternoon";

        public override float TemperatureOffset() => GameConditionUtility.LerpInOutValue(this, GenDate.TicksPerHour / 2f, GodImpactMath.WarmOffset);
    }

    [DefOf]
    public static class GodImpactDefOf
    {
        public static HediffDef Rimfolk_GodTouch_GraveRest;
        public static HediffDef Rimfolk_GodTouch_MoonSleep;
        public static HediffDef Rimfolk_GodTouch_PainEased;
        public static HediffDef Rimfolk_GodTouch_HealFaster;
        public static HediffDef Rimfolk_GodTouch_DeepDream;
        public static HediffDef Rimfolk_GodTouch_StillHour;
        public static HediffDef Rimfolk_GodTouch_AnimalsCalm;
        public static ThoughtDef Rimfolk_GodTouch_GriefEased;
        public static ThoughtDef Rimfolk_GodTouch_SweetSmoke;
        public static ThoughtDef Rimfolk_GodTouch_SameDream;

        static GodImpactDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(GodImpactDefOf));
    }
}
