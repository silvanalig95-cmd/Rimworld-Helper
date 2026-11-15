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
    /// The old powers' own strange happenings (the player, 13 November 2026: "i like the old powers storyteller and want you
    /// to do some more more events in this style, including the gods and using ressources from rimworld of magic", and on 14
    /// November "do ... more old powers style events"): the world feels watched. A seer at the season's turn, a night when
    /// everything faces one way, a stone that hums, a sealed scroll in the old ruins, casters burning bright under a sky of
    /// power, a wandering enchanter, a thing under the ground, the dead on the road at dusk, pilgrims passing, another sky, a
    /// beast that speaks, a god going by in the night. Each plays out on the map, and its card says what was seen; the gods
    /// stay unseen (a god is named only where the colony knows it was that god, Gods.CardName). They keep the gods' pace
    /// (OldPowersMath, GodPaceMath), come three times as often under Old Powers and its story direction (the ways' lever
    /// StoryWaysMath.Uncanny), and take the colony's age's form in their words. What hurts in them is the shared budget's
    /// (HarmKinds.Uncanny), at its rate, and shown. A RimWorld of Magic's motes, sounds, scrolls, gems and classes by name,
    /// with the game's own where it isn't loaded. This file is the frame: the hour, the steps, the words, the cards.
    /// </summary>
    public static partial class OldPowers
    {
        public const string Pool = "Rimfolk_Pool_OldPowersSaid";

        internal static int Now => Find.TickManager.TicksGame;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>With Rimfolk's world events on.</summary>
        public static bool On => Current.Game != null && (S?.worldEvents ?? true);

        internal static OldPowersBook Book => On ? OldPowersBook.Instance : null;

        /// <summary>The ways' lever on them now: 1 as they are, three times under Old Powers or its direction.</summary>
        public static float Lever => OldPowersMath.Lever(StoryDirection.Factor(StoryWaysMath.Uncanny));

        /// <summary>The colony's age as the words take it (0 tribal to 3 spacer).</summary>
        public static int Age => OldPowersMath.Age((int)Avail.EffectiveTech);

        /// <summary>Days since a tick (fractions kept), a long time for never.</summary>
        public static float DaysSince(int tick, int now) => tick < -900000 ? 99999f : (now - tick) / (float)GenDate.TicksPerDay;

        // ================= The hour =================

        /// <summary>
        /// The world events' hour (HappeningTracker.Hourly): what is under way has its hour; with nothing under way and the last
        /// far enough back, each happening whose hour it is and whose call holds has its chance, one at most.
        /// </summary>
        public static void Hourly(int now)
        {
            var b = Book;
            if (b == null) return;
            b.Fit();
            if (b.play != null)
            {
                Safe("the hour of what is under way", () => PlayHour(b, now));
                return;
            }
            if (!OldPowersMath.Spaced(DaysSince(b.lastAny, now))) return;
            float lever = Lever;
            foreach (var map in Homes.All.ToList())
            {
                if (map == null || !map.IsPlayerHome || MapFacts.InVacuum(map) || MapFacts.ThreatOn(map)) continue;
                int hour = GenLocalDate.HourOfDay(map);
                foreach (var e in OldPowersMath.All.InRandomOrder().ToList())
                {
                    if (!OldPowersMath.InHours(e, hour)) continue;
                    float since = DaysSince(b.Last(e), now);
                    if (since < OldPowersMath.PaceOf(e).gap) continue;
                    var why = CallSafe(e, map);
                    if (why == GodPaceMath.Trigger.None || !Rand.Chance(OldPowersMath.CheckChance(e, since, why, lever))) continue;
                    if (BeginSafe(b, e, map, why)) return;
                }
            }
        }

        /// <summary>Its call at a home now: none, an ordinary reason, or a strong one.</summary>
        public static GodPaceMath.Trigger Call(OldPower e, Map map)
        {
            switch (e)
            {
                case OldPower.Seer: return OldPowersVisitors.SeerCall(map);
                case OldPower.OneWay: return OldPowersNight.OneWayCall(map);
                case OldPower.Stone: return OldPowersNight.StoneCall(map);
                case OldPower.Scroll: return OldPowersFinds.ScrollCall(map);
                case OldPower.Charged: return OldPowersNight.ChargedCall(map);
                case OldPower.Enchanter: return OldPowersVisitors.EnchanterCall(map);
                case OldPower.Buried: return OldPowersFinds.BuriedCall(map);
                case OldPower.DeadRoad: return OldPowersNight.DeadRoadCall(map);
                case OldPower.Pilgrims: return OldPowersVisitors.PilgrimsCall(map);
                case OldPower.OtherSky: return OldPowersNight.OtherSkyCall(map);
                case OldPower.Beast: return OldPowersFinds.BeastCall(map);
                case OldPower.GodPasses: return OldPowersFinds.GodPassesCall(map);
                default: return GodPaceMath.Trigger.None;
            }
        }

        private static GodPaceMath.Trigger CallSafe(OldPower e, Map map)
        {
            try
            {
                return Call(e, map);
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("the old powers: the call of " + e, ex);
                return GodPaceMath.Trigger.None;
            }
        }

        /// <summary>It begins at a home (its first moment on the map); false when it couldn't (nobody to see it, nowhere for it).</summary>
        public static bool Begin(OldPowersBook b, OldPower e, Map map, GodPaceMath.Trigger why)
        {
            if (b == null || map == null) return false;
            bool strong = why == GodPaceMath.Trigger.Strong;
            switch (e)
            {
                case OldPower.Seer: return OldPowersVisitors.Seer(b, map, strong);
                case OldPower.OneWay: return OldPowersNight.OneWay(b, map);
                case OldPower.Stone: return OldPowersNight.Stone(b, map, strong);
                case OldPower.Scroll: return OldPowersFinds.Scroll(b, map);
                case OldPower.Charged: return OldPowersNight.Charged(b, map, strong);
                case OldPower.Enchanter: return OldPowersVisitors.Enchanter(b, map);
                case OldPower.Buried: return OldPowersFinds.Buried(b, map);
                case OldPower.DeadRoad: return OldPowersNight.DeadRoad(b, map, strong);
                case OldPower.Pilgrims: return OldPowersVisitors.Pilgrims(b, map, strong);
                case OldPower.OtherSky: return OldPowersNight.OtherSky(b, map);
                case OldPower.Beast: return OldPowersFinds.Beast(b, map);
                case OldPower.GodPasses: return OldPowersFinds.GodPasses(b, map);
                default: return false;
            }
        }

        private static bool BeginSafe(OldPowersBook b, OldPower e, Map map, GodPaceMath.Trigger why)
        {
            try
            {
                return Begin(b, e, map, why);
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("the old powers: " + e + " beginning", ex);
                b.play = null;
                return false;
            }
        }

        /// <summary>The dev action's: it begins now at <paramref name="map"/>, past its pace and its call (a strong one when asked).</summary>
        public static bool Fire(OldPower e, Map map, bool strong = false)
        {
            var b = OldPowersBook.Instance;
            if (b == null || map == null) return false;
            if (b.play != null) End(b);
            return BeginSafe(b, e, map, strong ? GodPaceMath.Trigger.Strong : GodPaceMath.Trigger.Ordinary);
        }

        // ================= What is under way =================

        /// <summary>A happening of several steps begins: kept as the one under way, its first step due.</summary>
        internal static OldPowerPlay Play(OldPowersBook b, OldPower e, Map map, float hours, int firstStepIn = 60)
        {
            var p = new OldPowerPlay { kind = e, map = map, until = Now + Mathf.RoundToInt(hours * GenDate.TicksPerHour), next = Now + Math.Max(1, firstStepIn) };
            b.play = p;
            b.Due(p.next);
            return p;
        }

        /// <summary>Its next short step in <paramref name="ticks"/>.</summary>
        internal static void Next(OldPowersBook b, OldPowerPlay p, int ticks)
        {
            p.next = Now + Math.Max(1, ticks);
            b.Due(p.next);
        }

        /// <summary>It is over: let go, so nothing of it stays in the save.</summary>
        internal static void End(OldPowersBook b)
        {
            var p = b?.play;
            if (p == null) return;
            if (p.kind == OldPower.OtherSky) OldPowersNight.EndSky(p);
            b.play = null;
        }

        /// <summary>The short steps (the frame's tick): only with something under way and its step due.</summary>
        internal static void Step(OldPowersBook b, int now)
        {
            var p = b.play;
            if (p == null) return;
            if (p.map == null || !Homes.IsHome(p.map) || now > p.until)
            {
                Safe("the end of what was under way", () => Over(b, p));
                End(b);
                return;
            }
            if (now < p.next)
            {
                b.Due(p.next);
                return;
            }
            Safe(p.kind + "'s step", () =>
            {
                switch (p.kind)
                {
                    case OldPower.Seer: OldPowersVisitors.SeerStep(b, p, now); break;
                    case OldPower.OneWay: OldPowersNight.OneWayStep(b, p, now); break;
                    case OldPower.Stone: OldPowersNight.StoneStep(b, p, now); break;
                    case OldPower.Scroll: OldPowersFinds.ScrollStep(b, p, now); break;
                    case OldPower.Charged: OldPowersNight.ChargedStep(b, p, now); break;
                    case OldPower.Enchanter: OldPowersVisitors.EnchanterStep(b, p, now); break;
                    case OldPower.Buried: OldPowersFinds.BuriedStep(b, p, now); break;
                    case OldPower.DeadRoad: OldPowersNight.DeadRoadStep(b, p, now); break;
                    case OldPower.Pilgrims: OldPowersVisitors.PilgrimsStep(b, p, now); break;
                    case OldPower.OtherSky: OldPowersNight.OtherSkyStep(b, p, now); break;
                    case OldPower.Beast: OldPowersFinds.BeastStep(b, p, now); break;
                    case OldPower.GodPasses: OldPowersFinds.GodPassesStep(b, p, now); break;
                }
            });
            // A step that threw, or that set nothing further: it waits a while and looks again, never every tick.
            if (b.play == p && p.next <= now) Next(b, p, 250);
        }

        /// <summary>The hour of what is under way (its hours of the night and the dawn).</summary>
        private static void PlayHour(OldPowersBook b, int now)
        {
            var p = b.play;
            if (p == null) return;
            b.Due(now + 1);
        }

        /// <summary>Its time ran out: whatever was still waiting to be said is let go (a visitor still here walks on).</summary>
        private static void Over(OldPowersBook b, OldPowerPlay p)
        {
            if (p.kind == OldPower.Enchanter) OldPowersVisitors.EnchanterOver(p);
        }

        internal static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the old powers: " + what, e);
            }
        }

        // ================= Words =================

        /// <summary>A key of the old powers by its own part ("Seer_Title": CC_OldP_Seer_Title).</summary>
        public static string K(string rest) => "CC_OldP_" + rest;

        /// <summary>A key's words, filled.</summary>
        internal static string T(string rest, params NamedArgument[] args) => K(rest).Translate(args).Resolve();

        /// <summary>A key's words in the colony's age's form (CC_OldP_<c>rest</c>_0 tribal to _3 spacer).</summary>
        internal static string Aged(string rest, params NamedArgument[] args) => T(rest + "_" + Age, args);

        /// <summary>
        /// One sentence from the old powers' pool for <paramref name="p"/> at a moment (its tags, comma-separated: "seer,cast"):
        /// only lines tagged with every one of them that fit the colony's age, so nothing of another moment is ever said;
        /// null when none fits. {PAWN} is the speaker, with <paramref name="more"/>.
        /// </summary>
        internal static string Line(Pawn p, string moment, params NamedArgument[] more)
        {
            var pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(Pool);
            if (pool?.lines == null || p == null || moment.NullOrEmpty()) return null;
            var want = moment.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
            var ctx = new HashSet<string>(want);
            Flavor.AddEraTags(ctx);
            var fits = new List<string>();
            foreach (var l in pool.lines.AllLines())
            {
                Flavor.Strip(l, out var tags);
                if (tags == null || want.Any(w => Array.IndexOf(tags, w) < 0)) continue;
                if (!Flavor.EraTagsFit(l) || Flavor.Match(l, ctx) < 0) continue;
                fits.Add(l);
            }
            if (fits.Count == 0) return null;
            var said = Flavor.Memory.SaidBy(p.thingIDNumber);
            var fresh = said == null ? fits : fits.Where(l => !said.Contains(Flavor.Strip(l))).ToList();
            string line = Flavor.Strip((fresh.Count > 0 ? fresh : fits).RandomElement());
            Flavor.Memory.RememberSaid(p.thingIDNumber, line);
            var args = new List<NamedArgument> { p.Named("PAWN") };
            if (more != null) args.AddRange(more);
            return line.Formatted(args.ToArray()).Resolve().CapitalizeFirst();
        }

        /// <summary>One sentence over <paramref name="p"/>'s head at a moment (Speech.Say); false when nothing was said.</summary>
        internal static bool Say(Pawn p, string moment, params NamedArgument[] more)
        {
            if (p == null || !p.Spawned) return false;
            string line = Line(p, moment, more);
            return !line.NullOrEmpty() && Speech.Say(p, line);
        }

        /// <summary>A card of what was seen: titled CC_OldP_<c>what</c>_Title (or the title given), as a letter.</summary>
        internal static void Card(Map map, string what, string text, LookTargets look, LetterDef def = null, string title = null, params NamedArgument[] args)
        {
            if (map == null || text.NullOrEmpty()) return;
            var all = new List<NamedArgument>(args ?? new NamedArgument[0]) { Homes.Name(map).Named("HOME") };
            title ??= K(what + "_Title").Translate(all.ToArray()).Resolve();
            Tell.Letter(NewsKind.Land, Importance.Notable, map, title.CapitalizeFirst(), text, def ?? LetterDefOf.NeutralEvent, look);
        }

        /// <summary>A line in the news of what was seen, with nothing to decide.</summary>
        internal static void Minor(Map map, string text, LookTargets look)
        {
            if (map == null || text.NullOrEmpty()) return;
            Tell.Minor(NewsKind.Land, Importance.Notable, map, text, look);
        }

        // ================= Motes, sounds, memories =================

        /// <summary>
        /// A few motes at a place: A RimWorld of Magic's own by name (the first of <paramref name="names"/> that is loaded),
        /// else the game's or Rimfolk's own small light, in a colour.
        /// </summary>
        internal static void Motes(Map map, Vector3 at, Color c, int n, float spread, params string[] names)
        {
            if (map == null) return;
            Def def = null;
            foreach (var name in names ?? new string[0])
            {
                def = (Def)DefDatabase<FleckDef>.GetNamedSilentFail(name) ?? DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def != null) break;
            }
            def ??= GodFx.MoteDef;
            for (int i = 0; i < n; i++)
                GodFx.Throw(def, at + new Vector3(Rand.Range(-spread, spread), 0f, Rand.Range(-spread * 0.6f, spread)), map, Rand.Range(0.35f, 0.65f), c, Rand.Range(-25f, 25f),
                    Rand.Range(0.15f, 0.45f), Rand.Range(-20f, 20f), Rand.Range(0.9f, 1.8f));
        }

        /// <summary>One mote moving off in a direction (degrees; 0 up the screen), for a shadow running out or a ghost walking.</summary>
        internal static void Drift(Map map, Vector3 at, Color c, float angle, float speed, float scale, float solid, params string[] names)
        {
            if (map == null) return;
            Def def = null;
            foreach (var name in names ?? new string[0])
            {
                def = (Def)DefDatabase<FleckDef>.GetNamedSilentFail(name) ?? DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def != null) break;
            }
            GodFx.Throw(def ?? GodFx.MoteDef, at, map, scale, c, angle, speed, 0f, solid);
        }

        /// <summary>A sound by the first name loaded that plays once (A RimWorld of Magic's first, the game's after), at a place.</summary>
        internal static void Sound(Map map, IntVec3 at, params string[] names)
        {
            if (map == null || !at.IsValid || names == null) return;
            foreach (var n in names)
            {
                var s = GodSounds.Sound(n);
                if (s == null || s.sustain) continue;
                GodSounds.Play(s, map, at);
                return;
            }
        }

        /// <summary>A memory for one of the colony (a ThoughtDef of Rimfolk's, by name), with the mood effects on.</summary>
        internal static void Memory(Pawn p, string thought) => OwnHappenings.Memory(p, thought);

        // ================= Who, and the world =================

        internal static bool Awake(Pawn p) => GodOmens.Awake(p);

        /// <summary>One of the colony awake who can see a place, nearest first.</summary>
        internal static Pawn Witness(Map map, IntVec3 at, float range) => map == null || !at.IsValid ? null : GodOmens.Witness(map, at, range);

        /// <summary>One of the colony, awake and free to go now (nothing that matters set aside), nearest a place; or null.</summary>
        internal static Pawn FreeNear(Map map, IntVec3 at, Pawn not = null) =>
            map?.mapPawns.FreeColonistsSpawned.Where(p => p != not && OwnHappenings.Ours(p) && Awake(p) && OwnHappenings.Yields(p) && (!at.IsValid || p.CanReach(at, PathEndMode.Touch, Danger.Some)))
                .OrderBy(p => at.IsValid ? p.Position.DistanceToSquared(at) : 0).FirstOrDefault();

        /// <summary>The moon tonight: none at all, or full.</summary>
        internal static bool Moonless => GodOmens.MoonTonight == GodsOnMapMath.MoonFace.Gone;

        internal static bool FullMoon => GodOmens.MoonTonight == GodsOnMapMath.MoonFace.Full;

        /// <summary>The day of the season (0 to 14) and the quarter of the year (0 spring to 3 winter) at a home.</summary>
        internal static int DayOfSeason(Map map) => GenLocalDate.DayOfQuadrum(map);

        internal static int Quarter(Map map) => StoryDirection.Quarter(map);

        /// <summary>Hours since the newest death of one of the colony's own here (a body on the land or in a grave), and where they lie; NaN for none.</summary>
        internal static float HoursSinceDeath(Map map, out Thing where)
        {
            where = null;
            if (map == null) return float.NaN;
            int best = -1;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
                if (t is Corpse c && c.Spawned && Ours(c) && c.timeOfDeath > best)
                {
                    best = c.timeOfDeath;
                    where = c;
                }
            foreach (var g in map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>())
                if (g.Spawned && g.HasCorpse && Ours(g.Corpse) && g.Corpse.timeOfDeath > best)
                {
                    best = g.Corpse.timeOfDeath;
                    where = g;
                }
            return best <= 0 ? float.NaN : (Now - best) / (float)GenDate.TicksPerHour;
        }

        private static bool Ours(Corpse c) => c?.InnerPawn != null && c.InnerPawn.RaceProps.Humanlike && c.InnerPawn.Faction == Faction.OfPlayer;

        /// <summary>One of the colony's own dead lies unburied on the home ground, a day or more.</summary>
        internal static bool Unburied(Map map) =>
            map != null && map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).Any(t => t is Corpse c && c.Spawned && c.InnerPawn?.RaceProps?.Humanlike == true
                                                                                             && map.areaManager.Home[c.Position] && Now - c.timeOfDeath > GenDate.TicksPerDay);

        /// <summary>A fight the storyteller holds for this home within <paramref name="days"/> days (its queue).</summary>
        internal static bool FightAhead(Map map, float days)
        {
            var queue = Find.Storyteller?.incidentQueue;
            if (map == null || queue == null) return false;
            int until = Now + Mathf.RoundToInt(days * GenDate.TicksPerDay);
            foreach (var o in queue)
            {
                if (!(o is QueuedIncident q) || q.FiringIncident?.def == null || q.FiringIncident.parms?.target != map || q.FireTick > until) continue;
                var cat = q.FiringIncident.def.category;
                if (cat == IncidentCategoryDefOf.ThreatBig || cat == IncidentCategoryDefOf.ThreatSmall) return true;
            }
            return false;
        }

        /// <summary>A game condition on the map by one of these exact defNames.</summary>
        internal static bool AnyCondition(Map map, params string[] names)
        {
            if (map?.gameConditionManager == null) return false;
            foreach (var c in map.gameConditionManager.ActiveConditions)
                if (c?.def != null && Array.IndexOf(names, c.def.defName) >= 0)
                    return true;
            return false;
        }

        /// <summary>The compass point from one cell to another, as the words say it ("north-east").</summary>
        internal static string Toward(IntVec3 from, IntVec3 to) => T("Point_" + OldPowersMath.Points[OldPowersMath.Compass(to.x - from.x, to.z - from.z)]);

        /// <summary>A standable, unroofed cell near a place, or Invalid.</summary>
        internal static IntVec3 OpenNear(Map map, IntVec3 at, int radius)
        {
            if (map == null || !at.IsValid) return IntVec3.Invalid;
            return CellFinder.TryFindRandomCellNear(at, map, radius, c => c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && c.GetEdifice(map) == null, out var cell)
                ? cell : IntVec3.Invalid;
        }
    }
}
