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
    /// The second seven's part of the gods' own happenings book (round 4, batch 2: Hush, Undertow, Helios, Selene, Murmur,
    /// Ducat, Tamiu): each god's happening under way, in a small record of its own (HushState and the rest, in each god's own
    /// file), and the rooms of old ruins someone of the colony has already stood in. When each happening last came is the
    /// shared list (<see cref="last"/>, one a happening).
    /// </summary>
    public partial class OwnHappeningsBook
    {
        /// <summary>The rooms of old ruins someone of the colony has already stood in ("map|x|z"), OwnHappeningsMath.RuinsHeldAtMost at most.</summary>
        public List<string> ruins = new List<string>();

        public HushState hush = new HushState();
        public UndertowState undertow = new UndertowState();
        public HeliosState helios = new HeliosState();
        public SeleneState selene = new SeleneState();
        public MurmurState murmur = new MurmurState();
        public DucatState ducat = new DucatState();
        public TamiuState tamiu = new TamiuState();

        /// <summary>Saved with the rest of the book (its ExposeData).</summary>
        internal void ExposeSeven()
        {
            Scribe_Collections.Look(ref ruins, "ruins", LookMode.Value);
            Scribe_Deep.Look(ref hush, "hush");
            Scribe_Deep.Look(ref undertow, "undertow");
            Scribe_Deep.Look(ref helios, "helios");
            Scribe_Deep.Look(ref selene, "selene");
            Scribe_Deep.Look(ref murmur, "murmur");
            Scribe_Deep.Look(ref ducat, "ducat");
            Scribe_Deep.Look(ref tamiu, "tamiu");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ruins ??= new List<string>();
                hush ??= new HushState();
                undertow ??= new UndertowState();
                helios ??= new HeliosState();
                selene ??= new SeleneState();
                murmur ??= new MurmurState();
                ducat ??= new DucatState();
                tamiu ??= new TamiuState();
            }
        }
    }

    /// <summary>
    /// The second seven in the gods' own happenings' frame (OwnHappenings.cs): their hour at each home and their short steps,
    /// called from the frame's own; their feasts (Helios's first fruit, Murmur's singing, Ducat's open purse); and the ways
    /// they share beyond the frame's: a card named only where the colony is sure (Gods.CardName, with a sign the colony has
    /// learned where one fits), each god's own lines (named only where the colony is sure), a memory of someone, who may be
    /// asked, and the small things on the map (rime, a soft light, a beast led, a stray cat, a sound).
    /// </summary>
    public static partial class OwnHappenings
    {
        /// <summary>The gods' hour (the frame's Hourly): each of the seven's happenings looked at, at each home.</summary>
        internal static void Seven(OwnHappeningsBook b, int now)
        {
            foreach (var map in HomeMaps)
            {
                Safe("Hush's hour", () => OwnHush.Hourly(b, map, now));
                Safe("the dark's hour", () => OwnUndertow.Hourly(b, map, now));
                Safe("Helios's hour", () => OwnHelios.Hourly(b, map, now));
                Safe("Selene's hour", () => OwnSelene.Hourly(b, map, now));
                Safe("Murmur's hour", () => OwnMurmur.Hourly(b, map, now));
                Safe("Ducat's hour", () => OwnDucat.Hourly(b, map, now));
                Safe("Tamiu's hour", () => OwnTamiu.Hourly(b, map, now));
            }
        }

        /// <summary>The steps between the hours (the frame's Step).</summary>
        internal static void SevenSteps(OwnHappeningsBook b, int now)
        {
            Safe("Hush's steps", () => OwnHush.Step(b, now));
            Safe("the dark's steps", () => OwnUndertow.Step(b, now));
            Safe("Helios's steps", () => OwnHelios.Step(b, now));
            Safe("Selene's steps", () => OwnSelene.Step(b, now));
            Safe("Murmur's steps", () => OwnMurmur.Step(b, now));
            Safe("Ducat's steps", () => OwnDucat.Step(b, now));
            Safe("Tamiu's steps", () => OwnTamiu.Step(b, now));
        }

        /// <summary>A feast kept (the frame's FestivalKept): Helios's first fruit, Murmur's singing at the wells, Ducat's open purse.</summary>
        internal static void SevenFestival(OwnHappeningsBook b, God g, Map map)
        {
            if (b == null || g == null || map == null) return;
            if (g.Key == OwnHelios.Key) OwnHelios.Festival(b, map);
            else if (g.Key == OwnMurmur.Key) OwnMurmur.Festival(b, map);
            else if (g.Key == OwnDucat.Key) OwnDucat.Festival(b, map);
        }

        /// <summary>
        /// The hour's look for one of the seven's drawn happenings (the frame's Due, 24 looks a day), only where its god acts
        /// at the home (the frame's Acts: held, heard of, or drawn near); <paramref name="factor"/> above one gives it a second look.
        /// </summary>
        public static bool Calls(OwnHappeningsBook b, OwnHappening h, GodPaceMath.Trigger t, God g, Map map, int now, float factor = 1f)
        {
            if (b == null || g == null || t == GodPaceMath.Trigger.None || !Acts(g, map)) return false;
            if (Due(b, h, g, map, t, now, 24f)) return true;
            return factor > 1f && Rand.Chance(factor - 1f) && Due(b, h, g, map, t, now, 24f);
        }

        // ================= Words =================

        /// <summary>The colony's age as 0 tribal, 1 medieval, 2 industrial, 3 spacer (the words' forms).</summary>
        public static int Age => GodStranger.Age;

        /// <summary>
        /// A card whose text is given (a word and its plain fact, who sat with whom): titled by the god's name only where the
        /// colony knows it was this god, or has learned <paramref name="sign"/> as its own (an eclipse as the sun's and the
        /// moon's, the long rains as the toad's): Gods.CardName with the sign; else by what was seen. The text never names it.
        /// </summary>
        public static void CardOf(God g, Map map, string sign, string unnamedTitle, string namedTitle, string text, LookTargets look, LetterDef def, params NamedArgument[] args)
        {
            if (map == null || text.NullOrEmpty()) return;
            string name = g == null || namedTitle.NullOrEmpty() ? null : Gods.CardName(g, map, sign);
            var all = new List<NamedArgument>(args ?? new NamedArgument[0]);
            string title;
            if (name == null) title = unnamedTitle.Translate(all.ToArray()).Resolve().CapitalizeFirst();
            else
            {
                all.Add(name.Named("GOD"));
                title = namedTitle.Translate(all.ToArray()).Resolve().CapitalizeFirst();
            }
            Tell.Letter(NewsKind.Land, Importance.Notable, map, title, text, def ?? LetterDefOf.NeutralEvent, look);
        }

        /// <summary>A line in the news of what was seen, with nothing to decide.</summary>
        public static void Line(Map map, string text, LookTargets look)
        {
            if (map == null || text.NullOrEmpty()) return;
            Tell.Minor(NewsKind.Land, Importance.Notable, map, text, look);
        }

        /// <summary>One sentence from a god's pool over someone's head (tagged with the moment, "named" where the colony is sure, "scoffer"); false when none fits.</summary>
        public static bool SayFrom(Pawn p, string pool, God g, params string[] tags) => SayFromWith(p, pool, g, null, tags);

        public static bool SayFromWith(Pawn p, string pool, God g, NamedArgument[] more, params string[] tags)
        {
            string line = LineFrom(p, pool, g, more, tags);
            return !line.NullOrEmpty() && Speech.Say(p, line);
        }

        /// <summary>A line from a pool for someone, with {PAWN}, {GOD} (the colony's name, only on lines tagged named) and <paramref name="more"/> filled; null when none fits.</summary>
        public static string LineFrom(Pawn p, string pool, God g, NamedArgument[] more, params string[] tags)
        {
            if (p == null || !p.Spawned) return null;
            var all = new List<string>();
            if (tags != null) all.AddRange(tags.Where(t => !t.NullOrEmpty()));
            string name = g == null ? null : Gods.CardName(g, p.Map);
            if (name != null) all.Add("named");
            if (Worship.Scoffer(p)) all.Add("scoffer");
            string line = Flavor.FromPool(pool, p, null, null, all.ToArray());
            if (line.NullOrEmpty()) return null;
            var args = new List<NamedArgument> { p.Named("PAWN"), (name ?? "").Named("GOD") };
            if (more != null) args.AddRange(more);
            return line.Formatted(args.ToArray()).Resolve().CapitalizeFirst();
        }

        /// <summary>A memory of someone by its defName (none where the def or their mood is missing).</summary>
        public static void MemoryOf(Pawn p, string def, Pawn other)
        {
            var d = DefDatabase<ThoughtDef>.GetNamedSilentFail(def);
            if (d == null || p?.needs?.mood == null) return;
            // apart: the helper every memory of the seven goes through; its callers say how the two were together.
            CouncilGameComponent.GiveThought(p, d, other);
        }

        // ================= Who =================

        /// <summary>One of the colony's own people (not a guest or a prisoner), alive and on the map.</summary>
        public static bool Ours(Pawn p) => p != null && p.Spawned && !p.Dead && p.IsFreeColonist && !p.IsQuestLodger() && People.HasInnerLife(p);

        /// <summary>Someone who can be sent to do it now: free of anything that matters (WorshipJobs.Free, not their free time only).</summary>
        public static bool Free(Pawn p) => Ours(p) && WorshipJobs.Free(p, false);

        /// <summary>
        /// Someone (or a beast) whose doing now may be set aside for the happening: nothing at all, or something casual that
        /// wasn't ordered (never eating, sleeping, a doctor's work or the player's own order).
        /// </summary>
        public static bool Yields(Pawn p) => p != null && (p.CurJob == null || p.CurJob.def.casualInterruptible && !p.CurJob.playerForced);

        /// <summary>The nearest of the colony awake who can see a place (GodsOnMapWords.Seer).</summary>
        public static Pawn Seer(Map map, IntVec3 at, float range = 30f) => GodsOnMapWords.Seer(map, at, range);

        /// <summary>The colony's middle, else the map's.</summary>
        public static IntVec3 Middle(Map map)
        {
            var c = CampSite.ColonyCenter(map);
            return c.IsValid ? c : map.Center;
        }

        // ================= On the map =================

        /// <summary>A beast walks to a cell, unhurried, and waits there (a cat to a bedside, to a body).</summary>
        public static void Lead(Pawn a, IntVec3 to, int ticks)
        {
            if (a?.jobs == null || !a.Spawned || !to.IsValid) return;
            try
            {
                if (a.Position != to && a.CanReach(to, PathEndMode.OnCell, Danger.Some))
                {
                    var go = JobMaker.MakeJob(JobDefOf.Goto, to);
                    go.locomotionUrgency = LocomotionUrgency.Walk;
                    go.expiryInterval = ticks;
                    a.jobs.StartJob(go, JobCondition.InterruptForced);
                    return;
                }
                if (a.CurJobDef == JobDefOf.Wait) return;
                var w = JobMaker.MakeJob(JobDefOf.Wait);
                w.expiryInterval = ticks;
                a.jobs.StartJob(w, JobCondition.InterruptForced);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' own happenings: a beast led", e);
            }
        }

        /// <summary>A soft light at a place, in a colour (Rimfolk's own mote, tinted), <paramref name="n"/> of them.</summary>
        public static void Glow(Map map, IntVec3 at, Color c, int n = 8, float spread = 1.2f, float scale = 0.45f)
        {
            if (map == null || !at.IsValid || !at.InBounds(map)) return;
            var mote = GodFx.MoteDef;
            for (int i = 0; i < n; i++)
                GodFx.Throw(mote, at.ToVector3Shifted() + new Vector3(Rand.Range(-spread, spread), 0f, Rand.Range(-spread, spread)), map, Rand.Range(scale * 0.7f, scale * 1.3f), c,
                    Rand.Range(-30f, 30f), Rand.Range(0.1f, 0.35f), Rand.Range(-20f, 20f), Rand.Range(0.8f, 1.6f));
        }

        /// <summary>A mote of one of the player's mods by name (A RimWorld of Magic's, softly), else Rimfolk's own, at a place.</summary>
        public static void ModMote(Map map, IntVec3 at, Color c, float scale, params string[] names)
        {
            if (map == null || !at.IsValid) return;
            Def def = null;
            foreach (var n in names)
            {
                def = (Def)DefDatabase<FleckDef>.GetNamedSilentFail(n) ?? DefDatabase<ThingDef>.GetNamedSilentFail(n);
                if (def != null) break;
            }
            GodFx.Throw(def ?? GodFx.MoteDef, at.ToVector3Shifted(), map, scale, c, 0f, 0.1f, 0f, 1.4f);
        }

        /// <summary>A rime of frost on an open cell (snow laid thin, a puff of cold air).</summary>
        public static void Rime(Map map, IntVec3 c, float depth = 0.55f)
        {
            if (map == null || !c.InBounds(map) || c.Roofed(map) || c.GetEdifice(map) != null) return;
            map.snowGrid.AddDepth(c, depth);
            FleckMaker.ThrowAirPuffUp(c.ToVector3Shifted(), map);
        }

        /// <summary>A cell beside a place someone can stand on and reach (the nearest of its neighbours to them).</summary>
        public static IntVec3 Beside(Map map, IntVec3 at, Pawn p)
        {
            if (map == null || !at.IsValid) return IntVec3.Invalid;
            var from = p?.Position ?? at;
            foreach (var c in GenAdj.CellsAdjacent8Way(new TargetInfo(at, map)).OrderBy(c => c.DistanceToSquared(from)))
                if (c.InBounds(map) && c.Standable(map) && (p == null || p.CanReach(c, PathEndMode.OnCell, Danger.Some)))
                    return c;
            return at.Standable(map) ? at : IntVec3.Invalid;
        }

        /// <summary>A cat of the colony's (any animal the gods' belief counts as a cat), free to go, that can reach a place; else null.</summary>
        public static Pawn ColonyCat(Map map, IntVec3 to) =>
            map?.mapPawns.SpawnedColonyAnimals.Where(a => PawnGods.IsCat(a) && !a.Dead && !a.Downed && !a.InMentalState && a.Awake() && !(a.roping?.IsRoped ?? false)
                                                         && a.CanReach(to, PathEndMode.Touch, Danger.Some))
                .OrderBy(a => a.Position.DistanceToSquared(to)).FirstOrDefault();

        /// <summary>A stray cat walking in from the edge of the land toward a place (the game's cat), or null.</summary>
        public static Pawn StrayCat(Map map, IntVec3 toward)
        {
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Cat");
            if (kind == null || map == null) return null;
            try
            {
                var edge = toward.IsValid ? LegendBeasts.EdgeToward(map, toward) : IntVec3.Invalid;
                if (!edge.IsValid) edge = CellFinder.RandomEdgeCell(map);
                var c = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
                return (Pawn)GenSpawn.Spawn(c, CellFinder.RandomClosewalkCellNear(edge, map, 3), map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' own happenings: a stray cat", e);
                return null;
            }
        }

        /// <summary>A stray sent off the land in a while.</summary>
        public static void SendOff(Pawn a, int afterTicks)
        {
            if (a == null || a.Dead || a.Faction != null || a.mindState == null) return;
            a.mindState.exitMapAfterTick = Now + Math.Max(60, afterTicks);
        }

        /// <summary>A sound of the game's or the player's mods' by the first name loaded, at a place.</summary>
        public static void Sound(Map map, IntVec3 at, float volume, params string[] names)
        {
            foreach (var n in names)
            {
                var s = GodSounds.Sound(n);
                if (s == null) continue;
                GodSounds.Play(s, map, at, volume)?.End();
                return;
            }
        }

        /// <summary>A game condition on the map whose defName holds <paramref name="part"/> ("ColdSnap", "Eclipse", "Drought"), or null.</summary>
        public static GameCondition Condition(Map map, string part)
        {
            if (map?.gameConditionManager == null) return null;
            foreach (var c in map.gameConditionManager.ActiveConditions)
                if (c?.def != null && c.def.defName.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                    return c;
            return null;
        }

        /// <summary>How many days in a row before today were dry (no rain, no snow), and wet, by the weather book.</summary>
        public static int DryRun(Map map)
        {
            var book = WeatherBook.Of(map);
            int n = 0;
            if (book?.DaysBefore != null)
                for (int i = book.DaysBefore.Count - 1; i >= 0 && book.DaysBefore[i].Dry; i--)
                    n++;
            return n;
        }

        public static int WetRun(Map map)
        {
            var book = WeatherBook.Of(map);
            int n = 0;
            if (book?.DaysBefore != null)
                for (int i = book.DaysBefore.Count - 1; i >= 0 && book.DaysBefore[i].Wet; i--)
                    n++;
            return n;
        }

        /// <summary>It is raining now (any weather with rain), and the sky is clear (no rain, no fog).</summary>
        public static bool Raining(Map map) => (map?.weatherManager?.curWeather?.rainRate ?? 0f) > 0.01f;

        public static bool Clear(Map map)
        {
            var w = map?.weatherManager?.curWeather;
            if (w == null) return false;
            return w.rainRate <= 0.01f && w.snowRate <= 0.01f && w.defName.IndexOf("Fog", StringComparison.OrdinalIgnoreCase) < 0;
        }

        /// <summary>The colony's days of food (OwnHappeningsMath.FoodDays).</summary>
        public static float FoodDays(Map map) => OwnHappeningsMath.FoodDays(map?.resourceCounter?.TotalHumanEdibleNutrition ?? 0f, map?.mapPawns.FreeColonistsSpawnedCount ?? 0);

        /// <summary>
        /// Food going to waste on the colony's home ground: a beast's carcass left to rot there, or a stack of people's food
        /// nearly spoiled and not kept cold (the game destroys food the moment it rots, so the waste is seen just before).
        /// </summary>
        public static Thing Wasting(Map map)
        {
            if (map == null) return null;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
                if (t is Corpse c && c.Spawned && c.InnerPawn?.RaceProps?.Animal == true && map.areaManager.Home[c.Position] && c.GetRotStage() == RotStage.Rotting)
                    return c;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree))
            {
                if (!t.Spawned || t is Corpse || !map.areaManager.Home[t.Position] || t.def.ingestible == null || !t.def.ingestible.HumanEdible) continue;
                var rot = t.TryGetComp<CompRottable>();
                if (rot == null || rot.PropsRot == null || rot.PropsRot.TicksToRotStart <= 0) continue;
                if (rot.RotProgress >= OwnHappeningsMath.WastingShare * rot.PropsRot.TicksToRotStart && t.AmbientTemperature > 0f) return t;
            }
            return null;
        }
    }
}
