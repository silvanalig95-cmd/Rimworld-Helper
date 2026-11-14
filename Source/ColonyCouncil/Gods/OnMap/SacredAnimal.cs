using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>The sacred animal while it walks the land: which beast and which god, its stay, who it follows or leads, and what was done to it.</summary>
    public class SacredStay : IExposable
    {
        public Pawn a;
        public int god = -1;
        public Map map;
        public int since, leaveAt;
        /// <summary>The colonist a small one follows about.</summary>
        public Pawn chosen;
        /// <summary>The colonist following a large one where it leads, the place, and the boon there.</summary>
        public Pawn follower;
        public IntVec3 to = IntVec3.Invalid;
        public AnimalBoon boon;
        public int leadUntil = -1;
        public bool led, beckoned, fed, hurt, slain, ended;
        /// <summary>Someone tried to tame it or keep it: it would not be kept, and went.</summary>
        public bool spurned;
        public Pawn feeder, harmer;
        public int lastSeenSaid = -1;
        /// <summary>Others of its kind following it at a distance (its shape's kin), and those who sat near it this visit.</summary>
        public List<Pawn> kin = new List<Pawn>(), sat = new List<Pawn>();

        public God God => Gods.ById(god);

        public void ExposeData()
        {
            Scribe_References.Look(ref a, "a");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref since, "since");
            Scribe_Values.Look(ref leaveAt, "leaveAt");
            Scribe_References.Look(ref chosen, "chosen");
            Scribe_References.Look(ref follower, "follower");
            Scribe_Values.Look(ref to, "to", IntVec3.Invalid);
            Scribe_Values.Look(ref boon, "boon", AnimalBoon.Find);
            Scribe_Values.Look(ref leadUntil, "leadUntil", -1);
            Scribe_Values.Look(ref led, "led");
            Scribe_Values.Look(ref beckoned, "beckoned");
            Scribe_Values.Look(ref fed, "fed");
            Scribe_Values.Look(ref hurt, "hurt");
            Scribe_Values.Look(ref slain, "slain");
            Scribe_Values.Look(ref ended, "ended");
            Scribe_Values.Look(ref spurned, "spurned");
            Scribe_References.Look(ref feeder, "feeder");
            Scribe_References.Look(ref harmer, "harmer");
            Scribe_Values.Look(ref lastSeenSaid, "lastSeenSaid", -1);
            Scribe_Collections.Look(ref kin, "kin", LookMode.Reference);
            Scribe_Collections.Look(ref sat, "sat", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                kin ??= new List<Pawn>();
                sat ??= new List<Pawn>();
                kin.RemoveAll(x => x == null);
                sat.RemoveAll(x => x == null);
            }
        }
    }

    /// <summary>
    /// The sacred animal (brainstorm 8; docs/design/gods.md, "On the map"): a god whose shape walks as a beast comes as one
    /// of its shape's beasts, else its look's kinds (the player's mods' first), shown as a god's (larger, in its colours,
    /// its aura and trail: Gods/Presence/; in the look its shape names, as Bellwether's white). It comes in from the edge
    /// after days of omens, stays a few days and goes (the game's own leaving), never tamed and never hostile unless
    /// harmed: the legend beast's shape. A small one follows one colonist about; a large one may lead whoever follows it to
    /// a find, a cure or a vein of ore, and take food from a hand (its shape says whether). Bellwether's way (his shape):
    /// other thrumbos following at a distance like pilgrims; someone may sit near him while he grazes and come away changed
    /// (a grief eased, a fear gone, a passion found; a child let touch his horn); let pass, a strange peace for a season;
    /// harming him or his kin is the worst thing: every god colder, every people on the map hears of it. Leaving it be,
    /// feeding it and following it move the god's favour, hidden; hunting or hurting it is sacrilege: the game's own
    /// revenge, a Wrath moment, a fever at the game's rate, and a slain one's body gone to ash. The cards say what was seen,
    /// never why (the player: the gods are an ominous presence, never explained).
    /// </summary>
    public static class SacredAnimal
    {
        public const string SeenPool = "Rimfolk_Pool_SacredAnimalSeen";
        public const string BeckonPool = "Rimfolk_Pool_SacredAnimalBeckon";

        private static int Now => Find.TickManager.TicksGame;

        // ================= Which beast, which god =================

        /// <summary>The kinds a god's look walks as that can live on this map now: animals only, not insects or dryads, at ease in the season.</summary>
        public static List<PawnKindDef> KindsFor(God god, Map map)
        {
            var list = new List<PawnKindDef>();
            // Only a god whose shape walks as a beast, as the beasts its shape names, else its look's.
            if (god == null || map == null || !GodShapes.Beast(god)) return list;
            var races = GodShapes.Beasts(god) ?? GodsWiring.LookOf(god)?.AnimalKinds;
            if (races == null) return list;
            foreach (var race in races)
            {
                var rp = race?.race;
                if (rp == null || !rp.Animal || rp.Insect || rp.Dryad || rp.IsMechanoid || rp.IsAnomalyEntity) continue;
                if (map.mapTemperature != null && !map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(race)) continue;
                var kind = DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(k => k.race == race);
                if (kind != null) list.Add(kind);
            }
            return list;
        }

        /// <summary>Some god can walk as a beast here.</summary>
        public static bool AnyFor(Map map) => map != null && !MapFacts.InVacuum(map) && Gods.All.Any(g => g != null && g.kind != GodKind.Dead && KindsFor(g, map).Count > 0);

        /// <summary>The god who walks as a beast: the wild's and the hunt's most, the colony's own and its patron more (GodsOnMapMath.AnimalWeight).</summary>
        public static God PickGod(Map home)
        {
            var mine = Gods.ColonyGods(home);
            var patron = Gods.Patron(home);
            return Gods.All.Where(g => g != null).RandomElementByWeightWithFallback(g =>
                GodsOnMapMath.AnimalWeight(g.domain, g.kind, KindsFor(g, home).Count, mine.Contains(g), g == patron, g.known));
        }

        /// <summary>The sacred animal on the map, if this is it.</summary>
        public static bool Is(Pawn p) => p != null && GodsOnMapBook.Instance?.beast?.a == p;

        /// <summary>One of the beasts following the sacred animal at a distance (its kin).</summary>
        public static bool IsKin(Pawn p) => p != null && GodsOnMapBook.Instance?.beast?.kin is List<Pawn> k && k.Count > 0 && k.Contains(p);

        // ================= Its coming =================

        /// <summary>It comes in from the edge now, shown as the god's, and the moment of its coming plays. True once it is on the land.</summary>
        internal static bool Come(GodsOnMapBook b, GodComing c, God god) => Bring(b, c.map, god, null) != null;

        /// <summary>Brings the god's beast to <paramref name="map"/> (a kind of its look's, <paramref name="kind"/> to choose one). The beast, or null.</summary>
        public static Pawn Bring(GodsOnMapBook b, Map map, God god, PawnKindDef kind)
        {
            if (b == null || map == null || god == null || b.beast != null) return null;
            var kinds = KindsFor(god, map);
            // The player's mods' beasts come first in a look's list: the first few most likely.
            kind ??= kinds.Count == 0 ? null : kinds[Mathf.Min(kinds.Count - 1, Rand.Chance(0.6f) ? 0 : Rand.Range(0, kinds.Count))];
            if (kind == null || !RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Animal + 0.2f)) return null;
            Pawn a;
            try
            {
                float grown = kind.RaceProps.lifeStageAges.NullOrEmpty() ? 2f : kind.RaceProps.lifeStageAges.Last().minAge;
                a = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                    fixedBiologicalAge: grown + Rand.Range(1f, 3f)));
                if (a == null) return null;
                GenSpawn.Spawn(a, entry, map, Rot4.Random);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a sacred animal", e);
                return null;
            }
            if (!Show(a, god))
            {
                a.Destroy();
                return null;
            }
            int now = Now;
            var s = new SacredStay
            {
                a = a, god = god.id, map = map, since = now,
                leaveAt = now + Mathf.RoundToInt(GodShapes.StayDays(god, Rand.Range(GodsOnMapMath.AnimalStayMin, GodsOnMapMath.AnimalStayMax)) * GenDate.TicksPerDay)
            };
            b.beast = s;
            try
            {
                // The game's own wandering beast: out to the open land, and off the map when its days are done.
                a.mindState.exitMapAfterTick = s.leaveAt;
                if (RCellFinder.TryFindRandomCellOutsideColonyNearTheCenterOfTheMap(entry, map, 10f, out var spot)) a.mindState.forcedGotoPosition = spot;
                // The great cat who keeps the dead makes for the graves.
                if (IsMog(s)) MogsWays.Arrived(s);
                Fed(a);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a sacred animal's way", e);
            }
            Kin(s, god, entry);
            var spec = PresenceSpec.For(god, map, PresenceKind.Appear, entry, a);
            string called = Called(a, god);
            bool kin = s.kin.Count > 0;
            spec.title = GodsOnMapWords.Title(god, map, false, "CC_GodM_Animal_Title", "CC_GodM_Animal_TitleNamed", called.Named("BEAST"));
            spec.text = ((IsMog(s) ? "CC_Mog_Came_Text" : "CC_GodM_Animal_Text") + (kin ? "Kin" : "")).Translate(called.Named("BEAST"), (s.kin.FirstOrDefault()?.def.label ?? "").Named("KIN"))
                .Resolve();
            if (!GodPresenceSeam.Call(spec)) Tell.Letter(NewsKind.Land, Importance.Important, map, spec.title, spec.text, LetterDefOf.NeutralEvent, new LookTargets(a));
            if (!spec.veiled) Gods.Learn(god, "visitors");
            GodsOnMapWords.Saw(god, map, entry, GodActKind.Appear, GodsOnMapWords.Seer(map, entry, 45f), "CC_GodM_Saw_Animal" + (kin ? "Kin" : ""), called.Named("BEAST"),
                (s.kin.FirstOrDefault()?.def.label ?? "").Named("KIN"));
            return a;
        }

        /// <summary>
        /// The beast shown as the god's, with all its presence (larger, in its colours, its light): in the look its shape
        /// names (Bellwether's white), else the god's own; over it, what the colony knows it as (<see cref="Label"/>).
        /// </summary>
        private static bool Show(Pawn a, God god)
        {
            if (!Gods.On) return false;
            string name = Label(a, god);
            var own = GodShapes.Look(god);
            return own != null ? GodAvatar.Make(a, own, name, false) : GodAvatar.Make(a, GodsWiring.LookOf(god), name, false, god.colour, god.colour2);
        }

        /// <summary>What the colony sees it as, in the words of it: its shape's look ("great white thrumbo"), else a strange one of its kind.</summary>
        public static string Called(Pawn a, God god) =>
            GodShapes.Look(god)?.label ?? "CC_GodM_Animal_Seen".Translate((a?.def.label ?? "?").Named("KIND")).Resolve();

        /// <summary>
        /// The name over it: the colony's own name for the god once one of them has heard of it ("the Old Horn"), else what
        /// it is seen as ("a great white thrumbo").
        /// </summary>
        public static string Label(Pawn a, God god) => god != null && god.known && !Gods.Name(god).NullOrEmpty() ? Gods.Name(god) : Called(a, god);

        /// <summary>The name over it kept to what the colony knows now (it may have heard of the god while it stayed).</summary>
        private static void Relabel(Pawn a, God god)
        {
            var c = GodAvatar.Of(a);
            if (c != null && !c.disguised) c.godName = Label(a, god);
        }

        /// <summary>Others of its kind (its shape's kin) come in behind it and follow at a distance, wild, leaving when it leaves.</summary>
        private static void Kin(SacredStay s, God god, IntVec3 entry)
        {
            var kind = GodShapes.Kin(god);
            int n = GodShapes.KinCount(god).RandomInRange;
            var map = s.map;
            if (kind?.RaceProps == null || n <= 0 || map == null) return;
            for (int i = 0; i < n; i++)
            {
                try
                {
                    var cell = CellFinder.RandomClosewalkCellNear(entry, map, 8);
                    float grown = kind.RaceProps.lifeStageAges.NullOrEmpty() ? 2f : kind.RaceProps.lifeStageAges.Last().minAge;
                    var k = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                        fixedBiologicalAge: grown + Rand.Range(0.5f, 6f)));
                    if (k == null) continue;
                    GenSpawn.Spawn(k, cell, map, Rot4.Random);
                    k.mindState.exitMapAfterTick = s.leaveAt + Rand.Range(1, 4) * GenDate.TicksPerHour;
                    Fed(k);
                    s.kin.Add(k);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: the sacred animal's kin", e);
                }
            }
        }

        /// <summary>A god's beast is never hungry, so it never hunts anything of the colony's.</summary>
        private static void Fed(Pawn a)
        {
            var food = a?.needs?.food;
            if (food != null) food.CurLevel = food.MaxLevel;
        }

        // ================= Its stay =================

        internal static void Hourly(GodsOnMapBook b, int now)
        {
            var s = b.beast;
            if (s == null) return;
            var a = s.a;
            var map = s.map;
            if (a == null || a.Discarded || map == null || !Homes.IsHome(map))
            {
                b.beast = null;
                return;
            }
            // Gone its way (off the map, unharmed): the god's favour for a stay let be.
            if (!a.Spawned && !a.Dead)
            {
                Departed(b, s, now);
                return;
            }
            if (a.Dead) return;
            // Hurt: once its revenge is done (or it took none), it leaves the land in a breath.
            if (s.hurt)
            {
                Settled(b);
                return;
            }
            Fed(a);
            Relabel(a, s.God);
            // Never tamed, owned or kept: marked to be tamed, it goes, and does not come back this time.
            if (a.Map.designationManager.DesignationOn(a, DesignationDefOf.Tame) != null || a.Faction != null)
            {
                a.Map.designationManager.RemoveAllDesignationsOn(a);
                s.spurned = true;
                Vanish(a);
                Departed(b, s, now);
                return;
            }
            if (now >= s.leaveAt + GenDate.TicksPerDay)
            {
                // Lingered past its days (shut in, lost): it goes as it came.
                Vanish(a);
                Departed(b, s, now);
                return;
            }
            var god = s.God;
            // Mog (Tamiu, the great cat who keeps the dead) leaves her chosen one while a stranger stays, and keeps to the
            // stranger instead; else her own hour (MogsWays.Keep): her riddle at the door, the heel of the one she favours,
            // the graves.
            if (IsMog(s) && Stare(b.stranger, s, a, now)) { }
            else if (IsMog(s)) MogsWays.Keep(s, a, now);
            else if (GodsOnMapMath.Follows(a.BodySize)) Follow(s, a, now);
            else if (GodShapes.Leads(god) && !s.led && !s.beckoned && s.follower == null && now - s.since > GenDate.TicksPerDay / 2
                     && Rand.Chance(GodsOnMapMath.BeckonChanceADay / 24f))
                Beckon(s, a);
            Pilgrims(s, a);
            if (GodShapes.SitNear(god)) Child(s, a);
            Seen(s, a, now);
        }

        /// <summary>
        /// Its kin keep their distance behind it, like pilgrims; one gone off the land (or dead) is let go. The colony's own
        /// animals of its kin's kind take to following it now and then too (Mog's cats), when they are free to.
        /// </summary>
        private static void Pilgrims(SacredStay s, Pawn a)
        {
            var kind = GodShapes.Kin(s.God);
            if (kind != null && Rand.Chance(0.3f))
            {
                var own = a.Map.mapPawns.SpawnedColonyAnimals.Where(x => x.def == kind.race && !x.Downed && x.Awake() && !x.InMentalState && !(x.roping?.IsRoped ?? false)
                                                                    && (x.CurJob == null || x.CurJob.def.casualInterruptible) && !x.Position.InHorDistOf(a.Position, 8f))
                    .RandomElementWithFallback();
                if (own != null)
                {
                    try
                    {
                        var j = JobMaker.MakeJob(JobDefOf.FollowClose, a);
                        j.followRadius = 5f;
                        j.expiryInterval = GenDate.TicksPerHour;
                        j.checkOverrideOnExpire = true;
                        own.jobs.StartJob(j, JobCondition.InterruptForced);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("gods: a colony animal following", e);
                    }
                }
            }
            if (s.kin.Count == 0) return;
            s.kin.RemoveAll(k => k == null || k.Dead || !k.Spawned || k.Map != a.Map);
            foreach (var k in s.kin)
            {
                if (k.Downed || k.InMentalState || !k.Awake() || k.Position.InHorDistOf(a.Position, 14f)) continue;
                if (k.CurJobDef == JobDefOf.FollowClose && k.CurJob?.targetA.Thing == a) continue;
                try
                {
                    Fed(k);
                    var j = JobMaker.MakeJob(JobDefOf.FollowClose, a);
                    j.followRadius = 9f;
                    j.expiryInterval = GenDate.TicksPerHour;
                    j.checkOverrideOnExpire = true;
                    j.locomotionUrgency = LocomotionUrgency.Walk;
                    k.jobs.StartJob(j, JobCondition.InterruptForced);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: the kin following", e);
                }
            }
        }

        /// <summary>Now and then a child of the colony with time on their hands goes out to it on their own, to touch its horn.</summary>
        private static void Child(SacredStay s, Pawn a)
        {
            if (s.hurt || s.sat.Count >= GodsOnMapMath.SittersAVisit || a.InMentalState || a.Downed || !Rand.Chance(0.1f)) return;
            var def = DefDatabase<JobDef>.GetNamedSilentFail(JobDriver_SitNearSacredAnimal.JobName);
            var c = a.Map.mapPawns.FreeColonistsSpawned.Where(x => x.DevelopmentalStage.Child() && !s.sat.Contains(x) && Together.Able(x) && Together.FreeTime(x)
                                                               && x.Position.InHorDistOf(a.Position, 40f) && x.CanReach(a, PathEndMode.Touch, Danger.None))
                .RandomElementWithFallback();
            if (def == null || c == null) return;
            try
            {
                c.jobs.StartJob(JobMaker.MakeJob(def, a), JobCondition.InterruptForced, null, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a child going out to the beast", e);
            }
        }

        // ================= Sitting near it =================

        public const string SatPool = "Rimfolk_Pool_SacredAnimalSat";
        public const string SatThought = "Rimfolk_Thought_SatNearSacred";
        public const string PeaceThought = "Rimfolk_Thought_StrangePeace";

        /// <summary>
        /// <paramref name="p"/> sat near it a while as it grazed (JobDriver_SitNearSacredAnimal) and comes away changed, once
        /// each and a few a visit: a child touches its horn (and somewhere the sky notices), else a grief eased, a fear gone,
        /// a passion found. Shown in what they say, their journal and their mood; nothing says why.
        /// </summary>
        internal static void SatBy(Pawn p)
        {
            var s = GodsOnMapBook.Instance?.beast;
            var a = s?.a;
            if (s == null || a == null || p == null || s.hurt || s.sat.Contains(p) || s.sat.Count >= GodsOnMapMath.SittersAVisit) return;
            s.sat.Add(p);
            var grief = Grief(p);
            var fear = Fear(p);
            var open = p.skills?.skills?.Where(k => k != null && k.passion == Passion.None && !k.TotallyDisabled).ToList() ?? new List<SkillRecord>();
            var change = GodsOnMapMath.Sat(p.DevelopmentalStage.Child(), grief != null, fear != null, open.Count > 0);
            string what = "";
            try
            {
                switch (change)
                {
                    case GodsOnMapMath.SitChange.Horn:
                        // The sky looks down on him with fondness (the god whose shape's lines are the sky's).
                        var sky = Gods.All.FirstOrDefault(g => g != null && GodShapes.LineTag(g) == "cirrus");
                        if (sky != null) Gods.AddFavour(sky, s.map, 1f, null);
                        if (a.Spawned) FleckMaker.ThrowLightningGlow(a.DrawPos, a.Map, 1.2f);
                        break;
                    case GodsOnMapMath.SitChange.Grief:
                        what = grief.LabelCap;
                        p.needs.mood.thoughts.memories.RemoveMemory(grief);
                        break;
                    case GodsOnMapMath.SitChange.Fear:
                        what = fear.LabelCap;
                        p.story.traits.RemoveTrait(fear);
                        break;
                    case GodsOnMapMath.SitChange.Passion:
                        var k = open.RandomElementByWeight(x => 1f + x.Level);
                        k.passion = Passion.Minor;
                        what = k.def.label;
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: sitting near the beast", e);
                change = GodsOnMapMath.SitChange.Nothing;
            }
            var mem = DefDatabase<ThoughtDef>.GetNamedSilentFail(SatThought);
            if (mem != null && p.needs?.mood != null)
            {
                var th = (Thought_Memory)ThoughtMaker.MakeThought(mem);
                th.SetForcedStage(change == GodsOnMapMath.SitChange.Horn ? 1 : 0);
                p.needs.mood.thoughts.memories.TryGainMemory(th);
            }
            // apart: said aloud as they get up from beside it, to nobody in particular.
            string line = Flavor.FromPool(SatPool, p, null, null, change.ToString().ToLowerInvariant(), GodShapes.LineTag(s.God));
            if (!line.NullOrEmpty()) Speech.Say(p, line.Formatted(Called(a, s.God).Named("BEAST"), a.def.label.Named("KIND")).Resolve());
            string called = Called(a, s.God);
            CouncilGameComponent.Instance?.journals?.WriteText(p, ("CC_GodM_Journal_Sat_" + change).Translate(called.Named("BEAST"), what.Named("WHAT")).Resolve());
            GodMarks.Witnessed(a.Map, a.Position, "CC_GodM_Memory_Animal".Translate(called.Named("BEAST")).Resolve(), new[] { p });
            GodsOnMapWords.Saw(s.God, a.Map, a.Position, GodActKind.Act, p, change == GodsOnMapMath.SitChange.Horn ? "CC_GodM_Saw_Horn" : "CC_GodM_Saw_Sat", called.Named("BEAST"));
        }

        /// <summary>The heaviest memory of a death they carry, or null.</summary>
        private static Thought_Memory Grief(Pawn p) =>
            p?.needs?.mood?.thoughts?.memories?.Memories?.Where(m => m?.def != null && m.MoodOffset() < 0f
                                                                    && (m.def.defName.Contains("Died") || m.def.defName.Contains("Death") || m.def.defName.Contains("Lost")))
                .OrderBy(m => m.MoodOffset()).FirstOrDefault();

        /// <summary>A fearful nature (nervous, volatile, a coward), or null.</summary>
        private static Trait Fear(Pawn p) =>
            p?.story?.traits?.allTraits?.FirstOrDefault(t => t?.def != null && (t.def.defName == "Nerves" && t.Degree < 0 || t.def.defName == "Wimp"));

        public const string StarePool = "Rimfolk_Pool_MogStares";

        /// <summary>Mog's stay (her shape's line tag), while she is unharmed and here.</summary>
        public static bool IsMog(SacredStay s) => s?.a != null && !s.hurt && !s.slain && !s.ended && GodShapes.LineTag(s.God) == "mog";

        /// <summary>
        /// The one Mog has chosen has a lucky season while she stays (docs/gods-pantheon-brainstorm.md, Mog): a sickness
        /// passes them by (the game's disease events, the mods' and Rimfolk's own pick someone else: Patch_MogsLuck), and
        /// Rimfolk's everyday hurts don't land (Harm.Bar: a fall doesn't break a bone). Nothing says so; people notice.
        /// </summary>
        public static bool Lucky(Pawn p)
        {
            if (p == null) return false;
            var s = GodsOnMapBook.Instance?.beast;
            return s != null && s.chosen == p && s.a.Spawned && IsMog(s);
        }

        /// <summary>
        /// Mog sees a god in disguise for what it is: while a stranger stays, she keeps a few steps from it wherever it goes,
        /// staring, and someone who sees it says so now and then (and the colony's book has it). False when there is no
        /// stranger here to stare at.
        /// </summary>
        private static bool Stare(StrangerStay st, SacredStay s, Pawn a, int now)
        {
            var x = st?.x;
            if (x == null || x.Dead || !x.Spawned || x.Map != a.Map) return false;
            if (a.InMentalState || a.Downed || !a.Awake()) return true;
            if (!(a.CurJobDef == JobDefOf.FollowClose && a.CurJob?.targetA.Thing == x))
            {
                try
                {
                    var j = JobMaker.MakeJob(JobDefOf.FollowClose, x);
                    j.followRadius = 3f;
                    j.expiryInterval = GenDate.TicksPerHour;
                    j.checkOverrideOnExpire = true;
                    a.jobs.StartJob(j, JobCondition.InterruptForced);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: Mog staring at the stranger", e);
                }
            }
            if (now - s.lastSeenSaid < GenDate.TicksPerHour * 6 || !Rand.Chance(0.4f)) return true;
            var p = a.Map.mapPawns.FreeColonistsSpawned.Where(o => Together.Able(o) && o.Position.InHorDistOf(a.Position, 14f) && GenSight.LineOfSight(o.Position, a.Position, a.Map, true))
                .RandomElementWithFallback();
            if (p == null) return true;
            string called = Called(a, s.God);
            string line = Flavor.FromPool(StarePool, p, null, null);
            if (line.NullOrEmpty() || !Speech.Say(p, line.Formatted(called.Named("BEAST"), x.LabelShort.Named("NAME")).Resolve())) return true;
            s.lastSeenSaid = now;
            GodsOnMapWords.Saw(s.God, a.Map, a.Position, GodActKind.Act, p, "CC_GodM_Saw_Stare", called.Named("BEAST"), x.LabelShort.Named("NAME"));
            return true;
        }

        /// <summary>A small one follows its chosen colonist about while they are out and about.</summary>
        private static void Follow(SacredStay s, Pawn a, int now)
        {
            if (s.chosen == null || s.chosen.Dead || !s.chosen.Spawned || s.chosen.Map != a.Map)
                s.chosen = Homes.People(s.map).Where(p => p.Spawned && Together.Able(p) && !p.DevelopmentalStage.Baby()).RandomElementWithFallback();
            var c = s.chosen;
            if (c == null || !c.Spawned || c.Map != a.Map || a.CurJobDef == JobDefOf.FollowClose && a.CurJob?.targetA.Thing == c) return;
            if (a.InMentalState || a.Downed || !a.Awake()) return;
            try
            {
                var j = JobMaker.MakeJob(JobDefOf.FollowClose, c);
                j.followRadius = 2.5f;
                j.expiryInterval = GenDate.TicksPerHour;
                j.checkOverrideOnExpire = true;
                a.jobs.StartJob(j, JobCondition.InterruptForced);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the sacred animal following", e);
            }
        }

        /// <summary>A colonist who sees it says so now and then (a few times a stay).</summary>
        private static void Seen(SacredStay s, Pawn a, int now)
        {
            if (now - s.lastSeenSaid < GenDate.TicksPerHour * 8 || !Rand.Chance(0.35f)) return;
            var p = a.Map.mapPawns.FreeColonistsSpawned.Where(x => Together.Able(x) && x.Position.InHorDistOf(a.Position, 14f) && GenSight.LineOfSight(x.Position, a.Position, a.Map, true))
                .RandomElementWithFallback();
            if (p == null) return;
            var god = s.God;
            string line = Flavor.FromPool(SeenPool, p, null, null, GodsOnMapMath.Follows(a.BodySize) ? "small" : "large", s.chosen == p ? "chosen" : null,
                god?.domain.ToString().ToLowerInvariant(), GodShapes.LineTag(god));
            if (line.NullOrEmpty()) return;
            string said = line.Formatted(Called(a, god).Named("BEAST"), a.def.label.Named("KIND")).Resolve();
            if (Speech.Say(p, said)) s.lastSeenSaid = now;
        }

        /// <summary>A large one comes to a colonist out of doors and waits for them: they follow it, of their own accord, where it leads.</summary>
        private static void Beckon(SacredStay s, Pawn a)
        {
            if (!PlayOuts.On || a.InMentalState || a.Downed || !a.Awake()) return;
            var p = a.Map.mapPawns.FreeColonistsSpawned.Where(x => Together.Able(x) && Together.FreeTime(x) && !x.Position.Roofed(a.Map) && x.Position.InHorDistOf(a.Position, 30f))
                .OrderBy(x => x.Position.DistanceToSquared(a.Position)).FirstOrDefault();
            if (p == null) return;
            s.beckoned = true;
            string line = Flavor.FromPool(BeckonPool, p, null, null, s.God?.domain.ToString().ToLowerInvariant());
            if (!line.NullOrEmpty()) Speech.Say(p, line.Formatted(a.def.label.Named("KIND")).Resolve());
            Lead(s, p);
        }

        // ================= Following it =================

        /// <summary>
        /// <paramref name="p"/> follows the god's beast where it leads (the player's word on the float menu, or a beckoning):
        /// it walks out to a place well off (a vein of ore when there is one to find and its god is of the deep, else the open
        /// land) with them behind it, and what it leads to is there. False when it can't lead now.
        /// </summary>
        public static bool Lead(SacredStay s, Pawn p)
        {
            var a = s?.a;
            if (a == null || p == null || !a.Spawned || a.Downed || a.InMentalState || s.led || s.follower != null || p.Map != a.Map) return false;
            var map = a.Map;
            var god = s.God;
            if (god == null) return false;
            var ore = Ore(map, a.Position);
            bool ailing = Ailing(p) != null;
            s.boon = GodsOnMapMath.Boon(god.domain, ailing, ore.IsValid, Rand.Value);
            IntVec3 to;
            if (s.boon == AnimalBoon.Vein && ore.IsValid) to = ore;
            else if (!CellFinder.TryFindRandomCellNear(a.Position, map, GodsOnMapMath.LeadMax,
                         c => c.Standable(map) && !c.Fogged(map) && !c.Roofed(map) && c.DistanceTo(a.Position) >= GodsOnMapMath.LeadMin && !map.areaManager.Home[c]
                              && a.CanReach(c, PathEndMode.OnCell, Danger.Some) && p.CanReach(c, PathEndMode.OnCell, Danger.Some), out to))
                return false;
            if (s.boon == AnimalBoon.Vein && !ore.IsValid) s.boon = AnimalBoon.Find;
            s.follower = p;
            s.to = to;
            s.leadUntil = Now + GenDate.TicksPerHour * 2;
            try
            {
                var go = JobMaker.MakeJob(JobDefOf.Goto, to);
                go.locomotionUrgency = LocomotionUrgency.Walk;
                go.expiryInterval = GenDate.TicksPerHour * 2;
                a.jobs.StartJob(go, JobCondition.InterruptForced);
                var follow = JobMaker.MakeJob(JobDefOf.FollowClose, a);
                follow.followRadius = 3f;
                follow.expiryInterval = GenDate.TicksPerHour * 2;
                follow.locomotionUrgency = LocomotionUrgency.Walk;
                p.jobs.StartJob(follow, JobCondition.InterruptForced, null, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: following the sacred animal", e);
                s.follower = null;
                return false;
            }
            GodsOnMapBook.Instance?.Due(Now + 250);
            return true;
        }

        /// <summary>A standable cell beside a vein of ore no one has mined (silver, gold, jade, plasteel, components, uranium, steel), near enough to lead to.</summary>
        private static IntVec3 Ore(Map map, IntVec3 from)
        {
            Building best = null;
            float bestD = float.MaxValue;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial).Concat(map.listerBuildings.allBuildingsNonColonist).Take(4000))
            {
                if (!(t is Building b) || b.def.building?.mineableThing == null || b.def.building.mineableThing == ThingDefOf.WoodLog) continue;
                if (b.def.building.isResourceRock == false) continue;
                float d = b.Position.DistanceTo(from);
                if (d < GodsOnMapMath.LeadMin || d > GodsOnMapMath.LeadMax * 1.5f || d >= bestD) continue;
                best = b;
                bestD = d;
            }
            if (best == null) return IntVec3.Invalid;
            foreach (var c in GenAdj.CellsAdjacent8Way(best))
                if (c.InBounds(map) && c.Standable(map))
                    return c;
            return IntVec3.Invalid;
        }

        /// <summary>The worst of what ails them: an illness, else the worst injury not yet healing.</summary>
        private static Hediff Ailing(Pawn p) =>
            p?.health?.hediffSet?.hediffs.Where(h => h != null && h.Visible && (h.def.makesSickThought || h is Hediff_Injury) && !h.IsPermanent())
                .OrderByDescending(h => h.Severity).FirstOrDefault();

        /// <summary>Steps between the hours: whether the beast and its follower are there yet.</summary>
        internal static void Step(GodsOnMapBook b, int now)
        {
            var s = b.beast;
            if (s?.follower == null || s.led) return;
            var a = s.a;
            var p = s.follower;
            if (a == null || !a.Spawned || a.Dead || a.Downed || p.Dead || !p.Spawned || p.Downed || now > s.leadUntil)
            {
                s.follower = null;
                return;
            }
            if (a.Position.DistanceTo(s.to) <= 3f && p.Position.DistanceTo(a.Position) <= 6f)
            {
                Arrived(s, a, p);
                return;
            }
            b.Due(now + 250);
        }

        /// <summary>There: the boon, the god's blessing moment, the favour, and the follower's thanks.</summary>
        private static void Arrived(SacredStay s, Pawn a, Pawn p)
        {
            s.led = true;
            var god = s.God;
            var map = a.Map;
            if (god == null) return;
            string what = null;
            try
            {
                switch (s.boon)
                {
                    case AnimalBoon.Cure:
                        var h = Ailing(p);
                        if (h is Hediff_Injury inj)
                        {
                            inj.Heal(inj.Severity);
                            what = "CC_GodM_Boon_CureWound".Translate(p.Named("PAWN")).Resolve();
                        }
                        else if (h != null)
                        {
                            h.Severity = Mathf.Max(0.01f, h.Severity * 0.3f);
                            what = "CC_GodM_Boon_CureIll".Translate(p.Named("PAWN"), h.LabelBase.Named("ILLNESS")).Resolve();
                        }
                        break;
                    case AnimalBoon.Vein:
                        foreach (var c in GenRadial.RadialCellsAround(s.to, 7f, true))
                            if (c.InBounds(map) && map.fogGrid.IsFogged(c))
                                map.fogGrid.Unfog(c);
                        var ore = GenAdj.CellsAdjacent8Way(new TargetInfo(s.to, map)).Select(c => c.InBounds(map) ? c.GetFirstMineable(map) : null).FirstOrDefault(m => m != null);
                        if (ore != null) what = "CC_GodM_Boon_Vein".Translate(p.Named("PAWN"), (ore.def.building?.mineableThing?.label ?? ore.def.label).Named("ORE")).Resolve();
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: what the sacred animal led to", e);
            }
            if (what == null)
            {
                string found = GodGifts.LeaveFind(god, map, s.to);
                if (found != null) what = "CC_GodM_Boon_Find".Translate(p.Named("PAWN"), found.Named("FOUND")).Resolve();
            }
            string called = Called(a, god);
            var spec = PresenceSpec.For(god, map, PresenceKind.Blessing, s.to, a);
            spec.title = GodsOnMapWords.Title(god, map, false, "CC_GodM_Boon_Title", "CC_GodM_Boon_TitleNamed", called.Named("BEAST"), p.Named("PAWN"));
            spec.text = "CC_GodM_Boon_Text".Translate(called.Named("BEAST"), p.Named("PAWN")).Resolve() + (what.NullOrEmpty() ? "" : "\n\n" + what);
            if (!GodPresenceSeam.Call(spec)) Tell.Letter(NewsKind.Land, Importance.Important, map, spec.title, spec.text, LetterDefOf.PositiveEvent, new LookTargets(s.to, map));
            Gods.AddFavour(god, map, GodsOnMapMath.Followed, null);
            GodsOnMapWords.Saw(god, map, s.to, GodActKind.Blessing, p, "CC_GodM_Saw_Led", called.Named("BEAST"));
            GodMarks.Note(god, map, s.to, "CC_GodM_Animal_LedAct".Translate(called.Named("BEAST"), p.LabelShort.Named("PAWN")).Resolve(), PresenceKind.Blessing);
            GodMarks.Witnessed(map, s.to, "CC_GodM_Memory_Animal".Translate(called.Named("BEAST")).Resolve(), new[] { p });
            Deeds.Record("Rimfolk_Deed_FollowedSacred", p, null, -1f, null, called);
            CouncilGameComponent.Instance?.journals?.WriteText(p, "CC_GodM_Journal_Led".Translate(called.Named("BEAST")).Resolve() + (what.NullOrEmpty() ? "" : " " + what));
            s.follower = null;
            try
            {
                if (p.CurJobDef == JobDefOf.FollowClose) p.jobs.EndCurrentJob(JobCondition.Succeeded);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the follower back to work", e);
            }
        }

        // ================= Fed =================

        /// <summary>One of the colony left food for it and it ate (JobDriver_FeedSacredAnimal).</summary>
        internal static void FedBy(Pawn by)
        {
            var s = GodsOnMapBook.Instance?.beast;
            if (s == null || s.fed) return;
            s.fed = true;
            s.feeder = by;
            var god = s.God;
            if (god == null || s.a == null) return;
            // The great cat walks at the heel of the one who brought her a bowl she didn't ask for, until someone answers her riddle.
            if (IsMog(s) && s.chosen == null) MogsWays.Favour(s, by);
            Gods.AddFavour(god, s.map, GodsOnMapMath.Fed, null);
            string called = Called(s.a, god);
            Deeds.Record("Rimfolk_Deed_FedSacred", by, null, -1f, null, called);
            GodsOnMapWords.Saw(god, s.map, s.a.Position, GodActKind.Act, by, "CC_GodM_Saw_Fed", called.Named("BEAST"));
        }

        // ================= Hurt or slain =================

        /// <summary>The colony's own hurt the god's beast or one of its kin (GodsOnMapPatches): the sacrilege, the god's anger at the next step.</summary>
        internal static void Struck(Pawn victim, Pawn by)
        {
            var b = GodsOnMapBook.Instance;
            var s = b?.beast;
            if (s == null || victim == null) return;
            if (s.a != victim)
            {
                if (s.kin.Contains(victim)) KinStruck(s, victim, by);
                return;
            }
            bool slain = victim.Dead;
            if (s.hurt && (!slain || s.slain)) return;
            bool first = !s.hurt;
            s.hurt = true;
            s.slain |= slain;
            s.harmer ??= by;
            var god = s.God;
            var map = s.map;
            if (god == null || map == null) return;
            var cell = victim.Spawned ? victim.Position : victim.Corpse?.Position ?? IntVec3.Invalid;
            string called = Called(victim, god);
            string harmer = by?.LabelShort ?? "CC_GodM_Someone".Translate().Resolve();
            if (first)
            {
                var spec = PresenceSpec.For(god, map, PresenceKind.Wrath, cell, victim.Spawned ? victim : null);
                spec.title = GodsOnMapWords.Title(god, map, false, "CC_GodM_Sacrilege_Title", "CC_GodM_Sacrilege_TitleNamed", called.Named("BEAST"));
                spec.text = (slain ? "CC_GodM_Sacrilege_TextSlain" : "CC_GodM_Sacrilege_Text").Translate(called.Named("BEAST"), harmer.Named("HARMER")).Resolve();
                if (!GodPresenceSeam.Call(spec)) Tell.Letter(NewsKind.Land, Importance.Important, map, spec.title, spec.text, LetterDefOf.NegativeEvent, new LookTargets(cell, map));
                GodsOnMapWords.Saw(god, map, cell, GodActKind.Wrath, GodsOnMapWords.Seer(map, cell, 40f) ?? (by != null && by.IsColonist ? by : null), "CC_GodM_Saw_Struck",
                    called.Named("BEAST"), harmer.Named("HARMER"));
                GodMarks.Note(god, map, cell, "CC_GodM_Animal_HurtAct".Translate(called.Named("BEAST"), (by?.LabelShort ?? "?").Named("PAWN")).Resolve(), PresenceKind.Wrath);
                if (by != null && by.IsColonist) Deeds.Record("Rimfolk_Deed_HuntedSacred", by, null, -1f, null, called);
                if (by != null && by.IsColonist && map != null) GodGifts.Fever(by, map, god);
                if (GodShapes.HarmIsWorst(god)) Worst(god, map, cell);
            }
            float favour = slain ? (first ? GodsOnMapMath.Slain : GodsOnMapMath.Slain - GodsOnMapMath.Hurt) : GodsOnMapMath.Hurt;
            Gods.AddFavour(god, map, favour, null, FavourSource.Sacrilege);
            if (slain)
            {
                b.beast = null;
                GodsOnMapWords.Saw(god, map, cell, GodActKind.Wrath, GodsOnMapWords.Seer(map, cell, 40f), "CC_GodM_Saw_Slain", called.Named("BEAST"));
                // Its kin go their way at once.
                foreach (var k in s.kin.Where(k => k != null && k.Spawned && !k.Dead)) k.mindState.exitMapAfterTick = Now + GenDate.TicksPerHour / 2;
                b.Due(Now + 1);
                pendingAsh = victim;
            }
        }

        /// <summary>
        /// One of its kin hurt by the colony's own: the god's beast turns and leaves as if it were struck itself (its kin's
        /// own revenge is the game's), the sacrilege and, where its shape says so, the worst thing done.
        /// </summary>
        private static void KinStruck(SacredStay s, Pawn victim, Pawn by)
        {
            var god = s.God;
            var map = s.map;
            if (god == null || map == null || s.hurt) return;
            s.hurt = true;
            s.harmer ??= by;
            var cell = victim.Spawned ? victim.Position : victim.Corpse?.Position ?? s.a?.Position ?? IntVec3.Invalid;
            bool there = s.a != null && s.a.Spawned;
            var spec = PresenceSpec.For(god, map, PresenceKind.Wrath, there ? s.a.Position : cell, there ? s.a : null);
            var args = new[]
            {
                Called(s.a ?? victim, god).Named("BEAST"), victim.def.label.Named("KIN"), (by?.LabelShort ?? "CC_GodM_Someone".Translate().Resolve()).Named("HARMER")
            };
            spec.title = "CC_GodM_KinStruck_Title".Translate(args).Resolve().CapitalizeFirst();
            spec.text = "CC_GodM_KinStruck_Text".Translate(args).Resolve();
            if (!GodPresenceSeam.Call(spec)) Tell.Letter(NewsKind.Land, Importance.Important, map, spec.title, spec.text, LetterDefOf.NegativeEvent, new LookTargets(cell, map));
            GodsOnMapWords.Saw(god, map, cell, GodActKind.Wrath, GodsOnMapWords.Seer(map, cell, 40f), "CC_GodM_Saw_KinStruck", args);
            if (cell.IsValid)
                GodMarks.Note(god, map, cell, "CC_GodM_Animal_KinHurtAct".Translate(victim.def.label.Named("KIN"), (by?.LabelShort ?? "?").Named("PAWN")).Resolve(), PresenceKind.Wrath);
            if (by != null && by.IsColonist)
            {
                Deeds.Record("Rimfolk_Deed_HuntedSacred", by, null, -1f, null, victim.def.label);
                GodGifts.Fever(by, map, god);
            }
            if (GodShapes.HarmIsWorst(god)) Worst(god, map, cell);
            Gods.AddFavour(god, map, GodsOnMapMath.Hurt, null, FavourSource.Sacrilege);
        }

        public const string WorstEvent = "Rimfolk_HarmedSacredBeast";

        /// <summary>
        /// The worst thing a colony can do (a shape's harmIsWorst): every god it has turns colder, unseen, and every people
        /// with someone on the map hears of it (their goodwill, with the game's own line for it).
        /// </summary>
        private static void Worst(God god, Map map, IntVec3 cell)
        {
            foreach (var g in Gods.All.Where(g => g != null && g != god).ToList())
                Gods.AddFavour(g, map, GodsOnMapMath.EveryGodColder, null, FavourSource.Sacrilege);
            var heard = DefDatabase<HistoryEventDef>.GetNamedSilentFail(WorstEvent);
            var peoples = map.mapPawns.AllPawnsSpawned.Select(p => p.Faction).Where(f => f != null && !f.IsPlayer && !f.Hidden && f.def.humanlikeFaction && !f.defeated).Distinct()
                .ToList();
            foreach (var f in peoples)
            {
                try
                {
                    f.TryAffectGoodwillWith(Faction.OfPlayer, GodsOnMapMath.PeoplesHear, true, true, heard, cell.IsValid ? new GlobalTargetInfo(cell, map) : (GlobalTargetInfo?)null);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: the peoples hearing of it", e);
                }
            }
        }

        /// <summary>A slain god's beast whose body goes to ash at the next step (nothing of a god is butchered or eaten).</summary>
        private static Pawn pendingAsh;

        internal static void Ash()
        {
            var p = pendingAsh;
            pendingAsh = null;
            var corpse = p?.Corpse;
            if (corpse == null || corpse.Destroyed) return;
            try
            {
                if (corpse.Spawned)
                {
                    var map = corpse.Map;
                    var at = corpse.Position;
                    FleckMaker.ThrowLightningGlow(corpse.DrawPos, map, 2.5f);
                    FilthMaker.TryMakeFilth(at, map, ThingDefOf.Filth_Ash, 3);
                }
                corpse.Destroy();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the slain beast's body", e);
            }
        }

        // ================= Its going =================

        /// <summary>It has gone its way: the god's favour for a stay let be (hidden), the peace its shape leaves, a line of what was seen, and the book.</summary>
        private static void Departed(GodsOnMapBook b, SacredStay s, int now)
        {
            b.beast = null;
            var god = s.God;
            var map = s.map;
            var a = s.a;
            if (god == null || map == null || a == null) return;
            if (s.spurned) Gods.AddFavour(god, map, GodsOnMapMath.Hurt / 2f, null, FavourSource.Sacrilege);
            else if (!s.hurt)
            {
                float favour = GodsOnMapMath.AnimalFavour(false, false, false, false);
                Gods.AddFavour(god, map, favour, null);
                if (GodShapes.Peace(god)) Peace(map);
            }
            // Its kin go their way with it.
            foreach (var k in s.kin.Where(k => k != null && k.Spawned && !k.Dead))
                k.mindState.exitMapAfterTick = k.mindState.exitMapAfterTick < 0 ? now + GenDate.TicksPerHour : Math.Min(k.mindState.exitMapAfterTick, now + GenDate.TicksPerHour);
            string called = Called(a, god);
            string named = GodsOnMapWords.CardName(god, map, false);
            string text = named != null && !s.hurt && !s.spurned ? "CC_GodM_Animal_GoneNamed".Translate(named.Named("GOD"), called.Named("BEAST")).Resolve().CapitalizeFirst()
                : (s.hurt ? "CC_GodM_Animal_GoneHurt" : s.spurned ? "CC_GodM_Animal_GoneSpurned" : "CC_GodM_Animal_Gone").Translate(called.Named("BEAST")).Resolve().CapitalizeFirst();
            Tell.Message(NewsKind.Land, Importance.Notable, map, text, null, MessageTypeDefOf.NeutralEvent);
            var seer = a.Spawned ? GodsOnMapWords.Seer(map, a.Position, 45f) : null;
            seer ??= s.follower ?? s.chosen ?? s.feeder ?? s.sat.FirstOrDefault(x => x != null && !x.Dead);
            string saw = GodsOnMapWords.Saw(god, map, a.Spawned ? a.Position : IntVec3.Invalid, GodActKind.Depart, seer, "CC_GodM_Saw_AnimalGone" + (s.hurt ? "Hurt" : ""),
                called.Named("BEAST"));
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, saw ?? text, seer));
            Vanish(a);
        }

        /// <summary>Let pass: a strange peace on everyone of the colony at home, for a season.</summary>
        private static void Peace(Map map)
        {
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail(PeaceThought);
            if (def == null) return;
            foreach (var p in Homes.People(map).Where(p => p.Spawned && p.needs?.mood != null && People.HasInnerLife(p)))
                p.needs.mood.thoughts.memories.TryGainMemory(def);
        }

        /// <summary>The beast is gone for good: off the map and out of the world.</summary>
        private static void Vanish(Pawn a)
        {
            if (a == null || a.Discarded) return;
            try
            {
                if (a.Spawned)
                {
                    FleckMaker.ThrowLightningGlow(a.DrawPos, a.Map, 2f);
                    a.DeSpawn();
                }
                if (!a.Dead) GodAvatar.Unmake(a);
                if (Find.WorldPawns.Contains(a)) Find.WorldPawns.RemoveAndDiscardPawnViaGC(a);
                else if (!a.Dead) Find.WorldPawns.PassToWorld(a, PawnDiscardDecideMode.Discard);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the sacred animal gone", e);
            }
        }

        /// <summary>A hurt beast that has done with its revenge (or never took it) leaves the land at the next hour: in a breath, as a god's beast does.</summary>
        internal static void Settled(GodsOnMapBook b)
        {
            var s = b.beast;
            var a = s?.a;
            if (s == null || !s.hurt || a == null || a.Dead || !a.Spawned || a.InMentalState) return;
            Vanish(a);
            Departed(b, s, Now);
        }
    }
}
