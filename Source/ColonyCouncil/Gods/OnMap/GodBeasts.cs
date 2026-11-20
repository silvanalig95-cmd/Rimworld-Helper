using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>A beast on the land taken for a god's (a legendary of PokéWorld): which, the god the colony's believer took it for, the god it is like, who said so, and when.</summary>
    public class LegendTaken : IExposable
    {
        public int id, god = -1, like = -1, tick;
        public string by, kind, defName;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref like, "like", -1);
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref by, "by");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref defName, "defName");
        }
    }

    /// <summary>
    /// The gods' beasts as they stand (Gods/OnMap/GodBeasts.cs), saved with the game: who has greeted which of the colony's
    /// own beasts of a god's kind (GodBeastsMath.GreetedMax at most), the beasts on the land taken for a god's
    /// (GodBeastsMath.LegendsMax), and a bite by hand or a festival's walk to the god's place under way.
    /// </summary>
    public class GodBeastsBook : GameComponent
    {
        public List<string> greeted = new List<string>();
        public List<LegendTaken> legends = new List<LegendTaken>();
        /// <summary>A bite under way: who brings it, to which beast, at which home; 1 the bite, 2 led on to the god's place; the place, the god, until when.</summary>
        public Pawn by, beast;
        public Map map;
        public int stage, god = -1, until = -1, lastBite = -999999;
        public IntVec3 to = IntVec3.Invalid;
        public bool festival;

        public GodBeastsBook(Game game)
        {
        }

        private static GodBeastsBook cached;
        private static Game cachedGame;

        public static GodBeastsBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<GodBeastsBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        [Unsaved] private int nextDue = int.MaxValue;

        public void Due(int tick)
        {
            if (tick < nextDue) nextDue = tick;
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now < nextDue) return;
            nextDue = int.MaxValue;
            GodBeasts.Step(this, now);
        }

        public void EndWalk()
        {
            by = beast = null;
            map = null;
            stage = 0;
            god = until = -1;
            to = IntVec3.Invalid;
            festival = false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref greeted, "greeted", LookMode.Value);
            Scribe_Collections.Look(ref legends, "legends", LookMode.Deep);
            Scribe_References.Look(ref by, "by");
            Scribe_References.Look(ref beast, "beast");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref until, "until", -1);
            Scribe_Values.Look(ref lastBite, "lastBite", -999999);
            Scribe_Values.Look(ref to, "to", IntVec3.Invalid);
            Scribe_Values.Look(ref festival, "festival");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                greeted ??= new List<string>();
                legends ??= new List<LegendTaken>();
                legends.RemoveAll(l => l == null);
                if (stage != 0 && (by == null || beast == null || map == null)) EndWalk();
                nextDue = 0;
            }
        }
    }

    /// <summary>
    /// The gods' beasts (docs/content-update-proposals.md, C3, C4 and C16; the player's go of 20 November 2026):
    /// <list type="bullet">
    /// <item>A god's beast at home: one of the colony's own animals of a god's sacred kind (Gods/SacredKinds.cs). Those who
    /// hold the god they take it for (their own reading, which may be wrong and stays theirs: GodBeastsMath.Roll) greet it
    /// the first time they see it, in one sentence; now and then, in the animals' rounds hours, one of them brings it a bite
    /// by hand from the stores; on that god's festival it is given a bite first and walked to the god's place; one born on
    /// its god's festival day is an omen animal (Animals/OmenAnimals.cs) named in the god's own words; when it dies those
    /// who hold the god grieve a little, and the colony's book keeps a believer's verse under the god they took it for.</item>
    /// <item>The beast that stays (Animals/ResidentBeast.cs), of a god's sacred kind or the shape a god walks in: a believer
    /// says whose they take it for when it is named; the council's question about it then has a believer's answer (leave it,
    /// it is not ours to hunt: Council_DlmA_ResidentBeastSacred); its death at the colony's hands is the harm of a sacred
    /// beast to those who hold that god; if it stays a season, the book keeps that it stayed among them.</item>
    /// <item>A legendary of PokéWorld (and the few the table names) wild on the land: a believer says whose they take it
    /// for, perhaps wrongly, and the book keeps it as a god's hand (Gods/GodHands.cs); hunting it is the harm of a sacred
    /// beast for those who believe it.</item>
    /// </list>
    /// What the colony knows is only what its people say: a god is named in their words by their own name for it, as a
    /// belief; nothing tells the player whose a beast really is. Nobody is hurt.
    /// </summary>
    public static class GodBeasts
    {
        public const string Pool = "Rimfolk_Pool_GodBeast_Say";
        public const string NamesPool = "Rimfolk_Pool_GodBeast_Names";
        public const string GoneThought = "Rimfolk_Thought_GodBeastGone";
        public const string SlainThought = "Rimfolk_Thought_SacredBeastSlain";

        private static int Now => Find.TickManager.TicksGame;

        private static GodBeastsBook Book => Gods.On ? GodBeastsBook.Instance : null;

        // ================= Whose beast =================

        /// <summary>The gods the colony can name: those its book has a name for, and its own.</summary>
        private static HashSet<string> KnownOf(Map map)
        {
            var set = GodLore.Instance?.KnownOf() ?? new HashSet<string>();
            if (map != null)
                foreach (var g in Gods.ColonyGods(map))
                    set.Add(g.Key);
            return set;
        }

        /// <summary>
        /// The god <paramref name="p"/> takes the beast for: the god whose kind it is if they know of it and don't mistake it,
        /// else one they confuse it with that they know of (GodLoreMath.Attribute, by their own fixed roll); null for none
        /// (not a god's kind, or none they can name).
        /// </summary>
        public static God TakenFor(Pawn p, Pawn a)
        {
            var truth = SacredKinds.BeastOf(a?.def);
            if (truth == null || p == null) return null;
            return Gods.ByKey(GodLoreMath.Attribute(truth.Key, KnownOf(a.MapHeld), GodBeastsMath.Roll(a.thingIDNumber, p.thingIDNumber)));
        }

        /// <summary>The colony's own animals of a god's kind on a map.</summary>
        public static List<Pawn> AtHome(Map map) =>
            map?.mapPawns.SpawnedColonyAnimals.Where(a => !a.Dead && a.RaceProps?.Animal == true && SacredKinds.BeastOf(a.def) != null).ToList() ?? new List<Pawn>();

        /// <summary>A line from the beasts' pool for <paramref name="p"/>, with {GOD} their own name for the god they speak of and {BEAST} the beast; null when none fits.</summary>
        private static string Line(Pawn p, God g, Pawn beast, string beastName, params string[] tags)
        {
            if (p == null) return null;
            var all = tags.Where(t => !t.NullOrEmpty()).ToList();
            if (g != null) all.Add(GodShapes.LineTag(g));
            string line = Flavor.FromPool(Pool, p, null, null, all.ToArray());
            if (line.NullOrEmpty()) return null;
            string name = g == null ? "" : PawnGods.NameOf(g, p);
            return line.Formatted(p.Named("PAWN"), name.Named("GOD"), (beastName ?? beast?.LabelShort ?? "").Named("BEAST"),
                (beast != null ? AnimalFacts.KindLabel(beast) : "").Named("KIND")).Resolve().CapitalizeFirst();
        }

        // ================= The hour =================

        /// <summary>The gods' hour (OwnHappenings.Hourly): greetings and bites at home, and a legendary on the land.</summary>
        public static void Hourly(int now)
        {
            var b = Book;
            if (b == null) return;
            foreach (var map in OwnHappenings.HomeMaps)
            {
                if (!OwnHappenings.Quiet(map)) continue;
                var beasts = AtHome(map);
                if (beasts.Count == 0) continue;
                OwnHappenings.Safe("a god's beast greeted", () => Greet(b, map, beasts));
                if (AnimalDaysMath.RoundsHour(GenLocalDate.HourOfDay(map)) && b.stage == 0 && GodBeastsMath.BiteDue(b.lastBite, now, Rand.Value))
                    OwnHappenings.Safe("a bite by hand", () => Bite(b, map, beasts, null, now));
            }
            OwnHappenings.Safe("a legendary on the land", () => Legends(b, now));
        }

        /// <summary>Someone who holds the god they take a beast at home for sees it for the first time: one sentence to it.</summary>
        internal static void Greet(GodBeastsBook b, Map map, List<Pawn> beasts)
        {
            foreach (var a in beasts)
            {
                if (!a.Spawned || map.fogGrid.IsFogged(a.Position)) continue;
                foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(p => Together.Able(p) && People.HasInnerLife(p) && p.Position.InHorDistOf(a.Position, GodBeastsMath.GreetRange)
                                                                               && GenSight.LineOfSight(p.Position, a.Position, map)).ToList())
                {
                    string key = GodBeastsMath.Greeting(a.thingIDNumber, p.thingIDNumber);
                    if (b.greeted.Contains(key)) continue;
                    var g = TakenFor(p, a);
                    if (g == null || !OwnFeasts.Holds(p, g)) continue;
                    b.greeted.Add(key);
                    while (b.greeted.Count > GodBeastsMath.GreetedMax) b.greeted.RemoveAt(0);
                    string line = Line(p, g, a, null, "greet");
                    // apart: said to the animal, not to another of the colony.
                    if (!line.NullOrEmpty()) Speech.Say(p, line);
                    OwnHappenings.Glow(map, a.Position, g.colour, 3, 0.5f, 0.3f);
                    return;
                }
            }
        }

        // ================= A bite by hand =================

        /// <summary>Something from the stores the beast eats (hay, kibble, raw food, meat for a meat-eater), the plainest first; null for none.</summary>
        private static Thing BiteFor(Pawn a, Map map) =>
            map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.Spawned && !(t is Corpse) && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer) && t.def.ingestible != null && !t.def.IsDrug && a.RaceProps.CanEverEat(t)
                            && t.def.ingestible.preferability > FoodPreferability.NeverForNutrition && t.def.ingestible.preferability <= FoodPreferability.RawTasty)
                .OrderBy(t => (int)t.def.ingestible.preferability).ThenByDescending(t => t.stackCount).FirstOrDefault();

        /// <summary>
        /// A believer free to (their free time, or for a festival anyone of the god's) takes a bite from the stores and walks to
        /// the beast; there they give it by hand (Step), and for the festival lead it on to the god's place. False when nobody can.
        /// </summary>
        internal static bool Bite(GodBeastsBook b, Map map, List<Pawn> beasts, God festivalGod, int now, IntVec3 place = default)
        {
            foreach (var a in beasts.Where(x => x.Spawned && !x.Downed && !x.InMentalState && !(x.roping?.IsRoped ?? false)).InRandomOrder())
            {
                var by = map.mapPawns.FreeColonistsSpawned.Where(p => !p.DevelopmentalStage.Baby() && OwnHappenings.Free(p) && (festivalGod != null || Together.FreeTime(p))
                                                                      && p.CanReach(a, PathEndMode.Touch, Danger.Some))
                    .Select(p => (p, g: TakenFor(p, a))).Where(x => x.g != null && OwnFeasts.Holds(x.p, x.g) && (festivalGod == null || x.g == festivalGod))
                    .OrderBy(x => x.p.Position.DistanceToSquared(a.Position)).FirstOrDefault();
                if (by.p == null) continue;
                var food = BiteFor(a, map);
                var near = CellFinder.RandomClosewalkCellNear(a.Position, map, 2, c => c.Standable(map));
                var go = JobMaker.MakeJob(JobDefOf.Goto, near.IsValid ? near : a.Position);
                go.locomotionUrgency = LocomotionUrgency.Walk;
                go.expiryInterval = GenDate.TicksPerHour;
                try
                {
                    if (food != null && by.p.CanReserveAndReach(food, PathEndMode.ClosestTouch, Danger.Some))
                    {
                        var take = JobMaker.MakeJob(JobDefOf.TakeInventory, food);
                        take.count = 1;
                        by.p.jobs.StartJob(take, JobCondition.InterruptForced, null, true);
                        by.p.jobs.jobQueue.EnqueueLast(go);
                    }
                    else if (!OwnHappenings.Job(by.p, go)) continue;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods' beasts: a bite carried", e);
                    continue;
                }
                b.by = by.p;
                b.beast = a;
                b.map = map;
                b.god = by.g.id;
                b.stage = 1;
                b.festival = festivalGod != null;
                b.to = place;
                b.until = now + GenDate.TicksPerHour * 3;
                b.lastBite = now;
                b.Due(now + 120);
                return true;
            }
            return false;
        }

        /// <summary>The steps of a bite or a festival's walk (GodBeastsBook's tick).</summary>
        internal static void Step(GodBeastsBook b, int now)
        {
            if (b.stage == 0) return;
            OwnHappenings.Safe("a god's beast's walk", () => StepOf(b, now));
            if (b.stage != 0) b.Due(now + 120);
        }

        private static void StepOf(GodBeastsBook b, int now)
        {
            var p = b.by;
            var a = b.beast;
            var map = b.map;
            var g = Gods.ById(b.god);
            if (p == null || a == null || map == null || g == null || p.Dead || a.Dead || !p.Spawned || !a.Spawned || p.Map != map || a.Map != map || now > b.until
                || MapFacts.ThreatOn(map))
            {
                b.EndWalk();
                return;
            }
            if (b.stage == 1)
            {
                if (!p.Position.InHorDistOf(a.Position, 2.9f))
                {
                    if (p.CurJobDef != JobDefOf.Goto && p.CurJobDef != JobDefOf.TakeInventory)
                    {
                        var near = CellFinder.RandomClosewalkCellNear(a.Position, map, 1, c => c.Standable(map));
                        OwnHappenings.GoTo(p, near.IsValid ? near : a.Position, GenDate.TicksPerHour);
                    }
                    return;
                }
                if (p.CurJobDef == JobDefOf.TakeInventory) return;
                // The beast holds still for it, and eats from the hand.
                OwnHappenings.Lead(a, a.Position, 300);
                OwnHappenings.Stand(p, a.Position, 300);
                bool ate = Feed(p, a);
                string line = Line(p, g, a, null, b.festival ? "festivalbite" : "bite", ate ? null : "nofood");
                // apart: said to the animal as it eats.
                if (!line.NullOrEmpty()) Speech.Say(p, line);
                if (ate) AnimalTempers.Noted(a, "CC_GodBeast_Noted_Bite", p.Named("PAWN"));
                if (b.festival && b.to.IsValid && a.CanReach(b.to, PathEndMode.Touch, Danger.Some))
                {
                    b.stage = 2;
                    b.until = now + GenDate.TicksPerHour * 2;
                    var spot = CellFinder.RandomClosewalkCellNear(b.to, map, 2, c => c.Standable(map));
                    if (!spot.IsValid) spot = b.to;
                    OwnHappenings.Lead(a, spot, GenDate.TicksPerHour * 2);
                    var beside = CellFinder.RandomClosewalkCellNear(spot, map, 2, c => c.Standable(map) && c != spot);
                    OwnHappenings.GoTo(p, beside.IsValid ? beside : spot, GenDate.TicksPerHour * 2);
                    b.to = spot;
                    return;
                }
                b.EndWalk();
                return;
            }
            if (b.stage != 2) return;
            if (!a.Position.InHorDistOf(b.to, 3f))
            {
                if (a.CurJobDef != JobDefOf.Goto) OwnHappenings.Lead(a, b.to, GenDate.TicksPerHour);
                if (p.CurJobDef != JobDefOf.Goto && !p.Position.InHorDistOf(b.to, 4f)) OwnHappenings.GoTo(p, b.to, GenDate.TicksPerHour);
                return;
            }
            OwnHappenings.Lead(a, b.to, 900);
            OwnHappenings.Stand(p, b.to, 600);
            string said = Line(p, g, a, null, "shrine");
            // apart: said at the god's place, to nobody in particular.
            if (!said.NullOrEmpty()) Speech.Say(p, said);
            OwnHappenings.Glow(map, b.to, g.colour, 6, 1f, 0.4f);
            string text = "CC_GodBeast_Walked_Text".Translate(p.LabelShort.Named("PAWN"), a.LabelShort.Named("BEAST"), AnimalFacts.KindLabel(a).Named("KIND"),
                Worship.ShrineOf(g).Named("SHRINE"), Homes.Name(map).Named("HOME")).Resolve();
            OwnHappenings.Line(map, text, new LookTargets(a));
            Worship.Booked(g, map, b.to, "CC_GodBeast_Walked_Book".Translate(a.LabelShort.Named("BEAST"), AnimalFacts.KindLabel(a).Named("KIND")).Resolve(), p);
            ArtTales.Record(ArtTales.FestivalBeast, p, a, Festivals.Name(g), null);
            b.EndWalk();
        }

        /// <summary>The bite in their hand eaten by the beast (the game's own eating sound); false with nothing in their hand it eats.</summary>
        private static bool Feed(Pawn p, Pawn a)
        {
            var held = p.inventory?.innerContainer?.FirstOrDefault(t => t.def.ingestible != null && a.RaceProps.CanEverEat(t));
            if (held == null) return false;
            try
            {
                var one = held.stackCount > 1 ? held.SplitOff(1) : held;
                if (one.holdingOwner != null) one.holdingOwner.Remove(one);
                var sound = one.def.ingestible.ingestSound;
                float food = one.Ingested(a, a.needs?.food?.NutritionWanted ?? 0f);
                if (a.needs?.food != null) a.needs.food.CurLevel += food;
                if (sound != null) Verse.Sound.SoundStarter.PlayOneShot(sound, new TargetInfo(a.Position, a.Map));
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' beasts: a bite eaten", e);
                return false;
            }
        }

        // ================= The festival =================

        /// <summary>A god's festival kept at a home (Festivals.Kept): a beast at home taken for that god's is given a bite first and walked to its place.</summary>
        public static void Festival(God g, Map map)
        {
            var b = Book;
            if (b == null || g == null || map == null) return;
            var beasts = AtHome(map);
            if (beasts.Count == 0) return;
            var place = Shrines.Of(map, g).Select(s => s.thing).Where(t => t != null && t.Spawned).OrderByDescending(Shrines.IsTemple).FirstOrDefault();
            var at = place == null ? IntVec3.Invalid : place.InteractionCell.IsValid && place.def.hasInteractionCell ? place.InteractionCell : place.Position;
            if (b.stage != 0) b.EndWalk();
            OwnHappenings.Safe("a god's beast at its festival", () => Bite(b, map, beasts, g, Now, at));
        }

        /// <summary>
        /// A newborn of the colony's (OmenAnimals.Consider): one of a god's kind born on that god's festival day is an omen,
        /// named in the god's own words while there is room among the living omens. True when it was.
        /// </summary>
        public static bool FestivalBirth(AnimalPowersBook book, Pawn a, Map map, int now)
        {
            if (!Gods.On || book == null || a == null || map == null) return false;
            var g = SacredKinds.BeastOf(a.def);
            var cal = ColonyCalendar.For(map);
            if (g == null || cal?.days == null) return false;
            int today = CalendarMath.Wrap(GenLocalDate.DayOfYear(map));
            var day = cal.days.FirstOrDefault(d => d != null && d.kind == CalendarKind.GodFestival && d.day == today && ColonyCalendar.GodOfDay(d) == g);
            if (day == null || !GodBeastsMath.FestivalOmen(book.LivingOmens(Homes.KeyOf(map)), AnimalPowersMath.MaxOmens)) return false;
            try
            {
                var keeper = HerdStory.Keeper(map, a);
                string name = keeper == null ? null : Flavor.FromPool(NamesPool, keeper, null, null, "god:" + g.Key.ToLowerInvariant());
                name = name?.Trim().TrimEnd('.');
                if (!name.NullOrEmpty() && name.Length <= 16 && !map.mapPawns.SpawnedColonyAnimals.Any(x => x != a && x.Name != null && x.Name.ToStringShort == name))
                    a.Name = new NameSingle(name);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' beasts: a festival's newborn named", e);
            }
            OmenAnimals.Born(book, a, map, OmenSign.KeptDay, day.label, now);
            return true;
        }

        // ================= A death =================

        /// <summary>
        /// An animal died (Happenings.Notify_Killed): one of the colony's taken for a god's is grieved a little by those who hold
        /// that god, with a believer's verse in the book; a beast on the land taken for a god's (a legendary, the beast that
        /// stays) slain by the colony is the harm of a sacred beast to those who believe it.
        /// </summary>
        public static void Died(Pawn victim, DamageInfo? dinfo, Map map)
        {
            if (!Gods.On || victim?.RaceProps?.Animal != true || map == null) return;
            try
            {
                bool byUs = dinfo?.Instigator?.Faction == Faction.OfPlayer;
                if (victim.Faction == Faction.OfPlayer && SacredKinds.BeastOf(victim.def) != null) Ours(victim, map);
                var b = Book;
                var legend = b?.legends.FirstOrDefault(l => l.id == victim.thingIDNumber);
                if (legend != null)
                {
                    b.legends.Remove(legend);
                    if (byUs) Slain(victim, map, Gods.ById(legend.god), Gods.ById(legend.like), "legend", legend.by);
                }
                var rb = CouncilGameComponent.Instance?.BeastOf(map);
                if (byUs && rb != null && rb.animal == victim && rb.takenGod >= 0)
                    Slain(victim, map, Gods.ById(rb.takenGod), SacredKinds.BeastOf(victim.def), "resident", rb.taker?.LabelShort);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' beasts: a death", e);
            }
        }

        /// <summary>One of the colony's own, of a god's kind, died: those who hold the god they take it for grieve a little; a believer's verse in the book.</summary>
        internal static void Ours(Pawn a, Map map)
        {
            var mourners = new List<Pawn>();
            God written = null;
            Pawn writer = null;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(p => Worship.Ours(p)))
            {
                var g = TakenFor(p, a);
                if (g == null || !OwnFeasts.Holds(p, g)) continue;
                mourners.Add(p);
                OwnHappenings.Memory(p, GoneThought);
                if (writer == null)
                {
                    writer = p;
                    written = g;
                }
            }
            if (writer == null) return;
            GodLore.Believes(writer, written, "CC_GodBeast_Book_Gone".Translate(a.LabelShort.Named("BEAST"), AnimalFacts.KindLabel(a).Named("KIND"),
                PawnGods.NameOf(written, writer).Named("GOD")).Resolve());
            var at = a.Corpse != null && a.Corpse.Spawned ? a.Corpse.Position : IntVec3.Invalid;
            var near = at.IsValid ? mourners.Where(p => p.Position.InHorDistOf(at, 15f)).OrderBy(p => p.Position.DistanceToSquared(at)).FirstOrDefault() : null;
            string line = near == null ? null : Line(near, TakenFor(near, a), a, a.LabelShort, "gone");
            // apart: said over the body, to nobody in particular.
            if (!line.NullOrEmpty()) Speech.Say(near, line);
        }

        /// <summary>
        /// A beast on the land taken for a god's slain by the colony: those who hold the god it was taken for are troubled as at
        /// a sacred beast killed, the book keeps it under that god, and the god it is like takes it ill as its sacred beast's
        /// death (unless it is that god's own sacred kind, which GodDeeds already counted).
        /// </summary>
        private static void Slain(Pawn a, Map map, God taken, God like, string what, string by)
        {
            if (taken != null)
            {
                Pawn writer = null;
                foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(p => OwnFeasts.Holds(p, taken)))
                {
                    OwnHappenings.Memory(p, SlainThought);
                    writer ??= p;
                }
                writer ??= map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.LabelShort == by);
                if (writer != null)
                    GodLore.Believes(writer, taken, ("CC_GodBeast_Book_Slain_" + what).Translate(a.LabelShort.Named("BEAST"), AnimalFacts.KindLabel(a).Named("KIND"),
                        PawnGods.NameOf(taken, writer).Named("GOD")).Resolve());
            }
            if (like != null && !SacredKinds.GodsOf(a.def).Contains(like))
            {
                // Hidden, as all favour is: no words go with it.
                float by2 = GodsMath.SacredDeath(like.domain, false, a.Faction == null);
                if (by2 != 0f) Gods.AddFavour(like, map, by2, null, FavourSource.Sacrilege);
            }
        }

        // ================= The beast that stays =================

        /// <summary>
        /// The beast that stays is named (ResidentBeast.Become): one of a god's kind or shape is taken for that god's by a
        /// believer there, perhaps wrongly, who says so; kept on the beast (takenGod, taker) for the council's question, its
        /// death and its season. The sentence for the naming letter, or null.
        /// </summary>
        public static string Resident(ResidentBeast rb, Pawn a, Map map)
        {
            if (!Gods.On || rb == null || a == null || map == null) return null;
            var truth = SacredKinds.BeastOf(a.def);
            if (truth == null) return null;
            var believer = map.mapPawns.FreeColonistsSpawned.Where(p => Worship.Ours(p) && Together.Able(p) && !p.DevelopmentalStage.Baby() && GodLore.Credence(p) >= GodLoreMath.BelieverFrom)
                .OrderByDescending(p => OwnFeasts.Holds(p, truth) ? 1 : 0).ThenByDescending(GodLore.Credence).FirstOrDefault();
            if (believer == null) return null;
            var taken = Gods.ByKey(GodLoreMath.Attribute(truth.Key, KnownOf(map), Rand.Value));
            if (taken == null) return null;
            rb.takenGod = taken.id;
            rb.taker = believer;
            string kind = AnimalFacts.KindLabel(a);
            string name = PawnGods.NameOf(taken, believer);
            string line = Line(believer, taken, a, a.LabelShort, "resident");
            // apart: said of the beast out on the land, to nobody in particular.
            if (!line.NullOrEmpty()) Speech.Say(believer, line);
            GodLore.Believes(believer, taken, "CC_GodBeast_Book_Resident".Translate(a.LabelShort.Named("BEAST"), kind.Named("KIND"), name.Named("GOD")).Resolve());
            return "CC_GodBeast_ResidentLetter".Translate(believer.Named("PAWN"), name.Named("GOD"), a.Named("PET")).Resolve();
        }

        /// <summary>The beast that stays has stayed a season (ResidentBeast's end): under the god it was taken for, the book keeps that it stayed among them.</summary>
        public static void ResidentStayed(ResidentBeast rb, Pawn a, Map map)
        {
            var g = rb == null || rb.takenGod < 0 ? null : Gods.ById(rb.takenGod);
            if (g == null || a == null || map == null) return;
            var writer = rb.taker != null && !rb.taker.Dead && rb.taker.IsColonist ? rb.taker : map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => OwnFeasts.Holds(p, g));
            if (writer == null) return;
            GodLore.Believes(writer, g, "CC_GodBeast_Book_Stayed".Translate(a.LabelShort.Named("BEAST"), AnimalFacts.KindLabel(a).Named("KIND"),
                PawnGods.NameOf(g, writer).Named("GOD")).Resolve());
        }

        /// <summary>The colony spared the beast as its believer asked (the council's answer): the god it is like glad of it, and the book keeps it under the god it was taken for.</summary>
        public static void Spared(ResidentBeast rb, Map map)
        {
            var a = rb?.animal;
            var taken = rb == null || rb.takenGod < 0 ? null : Gods.ById(rb.takenGod);
            if (a == null || taken == null || map == null) return;
            var like = SacredKinds.BeastOf(a.def);
            if (like != null) Gods.AddFavour(like, map, GodBeastsMath.SparedFavour, null, FavourSource.Rite);
            var writer = rb.taker != null && !rb.taker.Dead && rb.taker.IsColonist ? rb.taker : null;
            if (writer != null)
            {
                GodLore.Believes(writer, taken, "CC_GodBeast_Book_Spared".Translate(a.LabelShort.Named("BEAST"), AnimalFacts.KindLabel(a).Named("KIND"),
                    PawnGods.NameOf(taken, writer).Named("GOD")).Resolve());
                PawnGods.Move(writer, taken, BeliefEvent.Credited);
            }
        }

        // ================= A legendary on the land =================

        /// <summary>
        /// A legendary of PokéWorld (or another the table names) wild on a map where the colony's people are: a believer there
        /// says whose they take it for, as a god's hand (GodHands.ShowWith), once for each, the same kind once in a while.
        /// </summary>
        private static void Legends(GodBeastsBook b, int now)
        {
            if (!PokeWorldLink.Active) return;
            foreach (var map in Find.Maps)
            {
                if (map == null || map.mapPawns.FreeColonistsSpawnedCount == 0) continue;
                foreach (var a in map.mapPawns.AllPawnsSpawned.Where(x => x.Faction == null && !x.Dead && x.RaceProps?.Animal == true && GodBeastsMath.LegendGod(x.def.defName) != null).ToList())
                {
                    if (map.fogGrid.IsFogged(a.Position) || b.legends.Any(l => l.id == a.thingIDNumber)) continue;
                    int last = b.legends.Where(l => l.defName == a.def.defName).Select(l => l.tick).DefaultIfEmpty(-1).Max();
                    if (!GodBeastsMath.LegendDue(last, now)) continue;
                    var like = Gods.ByKey(GodBeastsMath.LegendGod(a.def.defName));
                    if (like == null || like.kind == GodKind.Dead) continue;
                    if (Taken(b, map, a, like, now)) return;
                }
            }
        }

        /// <summary>A believer on the map says whose they take the legendary for; the book keeps it as a hand. True when someone did.</summary>
        public static bool Taken(GodBeastsBook b, Map map, Pawn a, God like, int now)
        {
            if (b == null || a == null || like == null) return false;
            string kind = AnimalFacts.KindLabel(a);
            Pawn believer = null;
            string label = "CC_GodBeast_LegendLabel".Translate(kind.Named("KIND")).Resolve();
            bool shown = GodHands.ShowWith(map, like, HandKind.Sign, label, now,
                (g, p) =>
                {
                    believer = p;
                    return Line(p, g, a, kind, g == null ? "legendunnamed" : "legend");
                },
                (g, p) => Line(p, g, a, kind, "legendscoff"), out var taken);
            if (!shown) return false;
            b.legends.Add(new LegendTaken { id = a.thingIDNumber, god = taken?.id ?? -1, like = like.id, tick = now, by = believer?.LabelShort, kind = kind, defName = a.def.defName });
            while (b.legends.Count > GodBeastsMath.LegendsMax) b.legends.RemoveAt(0);
            return true;
        }
    }
}
