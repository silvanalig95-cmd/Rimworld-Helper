using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace ColonyCouncil
{
    [DefOf]
    public static class HomeQuests2DefOf
    {
        public static JobDef Rimfolk_HomeQ_Train;
        public static JobDef Rimfolk_HomeQ_Copy;
        public static JobDef Rimfolk_HomeQ_Play;

        static HomeQuests2DefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(HomeQuests2DefOf));
    }

    /// <summary>
    /// What round 2's seven quests at home share (the workers in Workers_MissionHome3.cs and Workers_MissionHome4.cs, the
    /// pure rules in HomeQuests2Math.cs; round 1's own helpers in HomeQuests.cs): the reward, who of the colony is free for
    /// a task, the wedding's piece by the asking people's age and the colony's means, the colony's cold store, the sick
    /// animal's illness, the colony's instruments and the sound of a people's own, and the jobs: training together, copying
    /// at the research bench, and playing for the colony.
    /// </summary>
    public static class HomeQuests2
    {
        public static bool IsOurs(Mission m) => m != null && Array.IndexOf(HomeQuests2Math.Kinds, m.kind) >= 0;

        /// <summary>The open quest of round 2 of this kind, if any.</summary>
        public static Mission Open(string kind) => Missions.All.FirstOrDefault(m => m.Open && m.kind == kind);

        public static int Reward(HomeQuest2 q, Faction f, Map map) =>
            HomeQuests2Math.Reward(q, HomeQuests.AgeOf(f), Ages.IsEmpire(f), LivingEvents.WealthFactor(map), Rand.Value);

        /// <summary>One of the colony's grown people free for a while: up and about, at nothing that matters more, not pressed by hunger or tiredness, at no gathering.</summary>
        public static bool Free(Pawn p) =>
            PlayOuts.OurPerson(p) && !p.DevelopmentalStage.Juvenile() && Together.Able(p) && PlayOuts.CanJoin(p) && PlayOuts.Interruptible(p) && !PlayOuts.NeedsPress(p)
            && p.GetLord() == null && DrillBouts.Of(p) == null;

        /// <summary>
        /// The pay handed over by <paramref name="bearer"/> where they stand, before they set off (silver, or a tribe's goods),
        /// set down at their feet; with nobody of theirs here, it comes as round 1's quests send it (HomeQuests.Pay).
        /// </summary>
        public static void PayInPerson(Mission m, Map map, Pawn bearer, int amount)
        {
            if (map == null || amount <= 0) return;
            if (bearer == null || bearer.Dead || !bearer.Spawned || bearer.Map != map || bearer.Downed)
            {
                HomeQuests.Pay(m, map, IntVec3.Invalid, null, amount);
                return;
            }
            foreach (var t in HomeQuests.PayThings(m, amount))
                if (GenPlace.TryPlaceThing(t, bearer.Position, map, ThingPlaceMode.Near, out var lying))
                    lying?.SetForbidden(false, false);
        }

        /// <summary>Their animals (a caravan's pack animal) lead off the map with them.</summary>
        public static void LeadOff(IEnumerable<Pawn> animals, Map map, Faction f)
        {
            var list = animals.Where(a => a != null && a.Spawned && !a.Dead && !a.Downed && a.Map == map && !a.RaceProps.Humanlike && a.Faction == f).ToList();
            if (list.Count == 0 || f == null) return;
            try
            {
                Lords.Leave(list);
                LordMaker.MakeNewLord(f, new LordJob_ExitMapBest(Pace.Errand, false, false), map, list);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: their animals leave", e);
            }
        }

        /// <summary>Hours from now to the next <paramref name="hour"/> o'clock on the map, at least <paramref name="min"/>.</summary>
        public static int TicksUntilHour(Map map, int hour, float min) =>
            Mathf.RoundToInt(HomeQuests2Math.HoursUntil(map != null ? GenLocalDate.HourFloat(map) : 12f, hour, min) * GenDate.TicksPerHour);

        // ---------------- The wedding's piece ----------------

        /// <summary>
        /// Clothes for a wedding by the asking people's age, from the game, its DLCs and the player's mods (by name, each
        /// only where it is loaded): a tribe's headdress, the medieval dresses, robes and capes, the modern
        /// formal shirts, vests and dresses, and for the Empire and the spacer peoples Royalty's formal wear.
        /// </summary>
        private static readonly string[] TribalClothes = { "Apparel_TribalHeaddress" };

        private static readonly string[] MedievalClothes =
        {
            "VFEM2_Apparel_DameDress", "Apparel_GreatDress", "FCA2_WhiteBlueDress", "FCA2_BlueLacedDress", "Apparel_Robe", "Apparel_Cape", "Apparel_ShirtRuffle", "Apparel_Corset"
        };

        private static readonly string[] ModernClothes = { "Apparel_ShirtRuffle", "Apparel_VestRoyal", "Apparel_Corset", "Apparel_GreatDress", "Apparel_CollarShirt", "Apparel_Vest" };

        private static readonly string[] HighClothes = { "Apparel_RobeRoyal", "Apparel_ShirtRuffle", "Apparel_VestRoyal", "Apparel_Corset" };

        /// <summary>Furniture for a new home: a double bedroll for a tribe, a double bed for the rest, the Empire's royal bed.</summary>
        private static string[] Beds(int age, bool empire) =>
            empire ? new[] { "RoyalBed", "DoubleBed" } : age <= HomeQuestsMath.Tribal ? new[] { "BedrollDouble" } : new[] { "DoubleBed" };

        private static string[] Clothes(int age, bool empire) =>
            empire || age >= HomeQuestsMath.Spacer ? HighClothes : age == HomeQuestsMath.Industrial ? ModernClothes : age == HomeQuestsMath.Medieval ? MedievalClothes : TribalClothes;

        private static bool HasQuality(ThingDef d) => d != null && d.HasComp(typeof(CompQuality));

        /// <summary>
        /// The pieces the colony could make for their wedding now, each with its kind ("clothes", "art", "bed"): clothes of
        /// their age it has the research for, a small work of art when it can sculpt, furniture for the couple's home it can
        /// build. Every one of good quality or better is possible to make.
        /// </summary>
        public static List<(ThingDef def, string tag)> PieceOptions(Faction f, Map map)
        {
            var list = new List<(ThingDef, string)>();
            if (f == null || map == null) return list;
            int age = HomeQuests.AgeOf(f);
            bool empire = Ages.IsEmpire(f);
            foreach (var n in Clothes(age, empire))
            {
                var d = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                if (d?.apparel != null && HasQuality(d) && ThingRoles.ColonyCanMake(d) && !list.Any(x => x.Item1 == d)) list.Add((d, "clothes"));
            }
            if (HappeningWorker_Mission.CanSculpt(map))
            {
                var art = ThingRoles.Ours(ThingRole.SmallArt, map, 0, d => HasQuality(d) && ThingRoles.ColonyCanMake(d)) ?? DefDatabase<ThingDef>.GetNamedSilentFail("SculptureSmall");
                if (art != null && HasQuality(art)) list.Add((art, "art"));
            }
            foreach (var n in Beds(age, empire))
            {
                var d = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                if (d != null && HasQuality(d) && d.Minifiable && Avail.CanBuild(d, map))
                {
                    list.Add((d, "bed"));
                    break;
                }
            }
            return list;
        }

        /// <summary>A thing of the piece's kind on the map, ready to hand over: of good quality or better, not forbidden, a building packed (uninstalled) and not about to be placed.</summary>
        public static bool IsPiece(Thing t, ThingDef def, out QualityCategory q)
        {
            q = QualityCategory.Awful;
            if (t == null || def == null || t.Destroyed || !t.Spawned || t.IsForbidden(Faction.OfPlayer)) return false;
            var inner = t is MinifiedThing mt ? mt.InnerThing : t;
            if (inner?.def != def || !inner.TryGetQuality(out q) || !HomeQuests2Math.GoodEnough((int)q)) return false;
            if (t is MinifiedThing && InstallBlueprintUtility.ExistingBlueprintFor(t) != null) return false;
            return true;
        }

        /// <summary>The colony's fitting pieces on the map, the best first.</summary>
        public static List<Thing> Pieces(Map map, ThingDef def)
        {
            var list = new List<Thing>();
            if (map == null || def == null) return list;
            IEnumerable<Thing> pool = def.Minifiable ? map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing) : map.listerThings.ThingsOfDef(def);
            foreach (var t in pool)
                if (IsPiece(t, def, out _))
                    list.Add(t);
            return list.OrderByDescending(t => (t is MinifiedThing m ? m.InnerThing : t).TryGetQuality(out var q) ? (int)q : 0).ToList();
        }

        // ---------------- The cold store ----------------

        /// <summary>
        /// A room of the colony's at freezing or colder now (under a roof, walled in, not a prison cell or a bedroom), and a
        /// free cell in it to point to (on a stockpile if it has one). False when there's none.
        /// </summary>
        public static bool ColdStore(Map map, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (map == null) return false;
            Room best = null;
            float bestTemp = float.MaxValue;
            foreach (var room in RoomUtil.ColonyRooms(map))
            {
                if (room == null || room.Dereferenced || room.PsychologicallyOutdoors || room.UsesOutdoorTemperature || room.IsPrisonCell || room.Role == RoomRoleDefOf.Bedroom) continue;
                float t = room.Temperature;
                if (!HomeQuests2Math.Cold(t) || t >= bestTemp) continue;
                best = room;
                bestTemp = t;
            }
            if (best == null) return false;
            var cells = best.Cells.Where(c => c.Standable(map) && c.GetEdifice(map) == null).ToList();
            if (cells.Count == 0) return false;
            cell = cells.FirstOrDefault(c => c.GetZone(map) is Zone_Stockpile && c.GetFirstItem(map) == null);
            if (!cell.IsValid) cell = cells.FirstOrDefault(c => c.GetFirstItem(map) == null);
            if (!cell.IsValid) cell = cells[0];
            return true;
        }

        /// <summary>How the colony keeps food cold, by its age, in words: a cold room, an ice house, a freezer.</summary>
        public static string ColdStoreWord(Map map)
        {
            var age = Ages.Of(map);
            string key = age == WorldAge.Tribal ? "CC_HomeQ2_Cold_Tribal" : age == WorldAge.Medieval ? "CC_HomeQ2_Cold_Medieval" : "CC_HomeQ2_Cold_Modern";
            return key.Translate().Resolve();
        }

        // ---------------- The sick animal ----------------

        /// <summary>The illness by its tag, the game's own: animal flu, animal plague, malnutrition.</summary>
        public static HediffDef IllnessDef(string tag) =>
            tag == HomeQuests2Math.Plague ? DefDatabase<HediffDef>.GetNamedSilentFail("Animal_Plague")
            : tag == HomeQuests2Math.Thin ? HediffDefOf.Malnutrition
            : DefDatabase<HediffDef>.GetNamedSilentFail("Animal_Flu");

        /// <summary>The animal comes in with its illness, as far along as the rules say.</summary>
        public static void MakeSick(Pawn x, string tag)
        {
            var def = IllnessDef(tag);
            if (x?.health?.hediffSet == null || def == null) return;
            try
            {
                var h = x.health.hediffSet.GetFirstHediffOfDef(def) ?? HediffMaker.MakeHediff(def, x);
                h.Severity = HomeQuests2Math.Severity(tag);
                if (!x.health.hediffSet.hediffs.Contains(h)) x.health.AddHediff(h);
                if (tag == HomeQuests2Math.Thin && x.needs?.food != null) x.needs.food.CurLevelPercentage = 0.1f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: a sick animal", e);
            }
        }

        /// <summary>Still sick: its illness (or any other the game's doctors treat) on it, underfed, or a wound waiting for a doctor.</summary>
        public static bool Sick(Pawn x, string tag)
        {
            var hs = x?.health?.hediffSet;
            if (hs == null) return false;
            var def = IllnessDef(tag);
            if (def != null && hs.GetFirstHediffOfDef(def) != null) return true;
            if (hs.hediffs.Any(h => h != null && h.Visible && h.TryGetComp<HediffComp_Immunizable>() != null)) return true;
            if ((hs.GetFirstHediffOfDef(HediffDefOf.Malnutrition)?.Severity ?? 0f) > 0.05f) return true;
            return x.health.HasHediffsNeedingTend();
        }

        // ---------------- Music ----------------

        /// <summary>
        /// The colony's own instrument near the spot that a musician may play: the game's own or a mod's, not burning,
        /// broken or forbidden, not in a bedroom, a cell or a sickroom, free to reserve, with a place to stand or sit at it.
        /// </summary>
        public static bool Instrument(Pawn p, IntVec3 near, float range, out Building_MusicalInstrument instrument, out IntVec3 seat)
        {
            instrument = null;
            seat = IntVec3.Invalid;
            var map = p?.Map;
            if (map == null || !near.IsValid) return false;
            float best = float.MaxValue;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (!(b is Building_MusicalInstrument m) || !m.Spawned || m.Faction != Faction.OfPlayer) continue;
                float d = m.Position.DistanceToSquared(near);
                if (d >= best || d > range * range || m.IsBeingPlayed || m.IsBurning() || m.IsBrokenDown() || m.IsForbidden(Faction.OfPlayer)) continue;
                var room = m.GetRoom();
                if (room != null && (room.IsPrisonCell || room.Role == RoomRoleDefOf.Bedroom || room.Role == RoomRoleDefOf.Barracks || room.Role == RoomRoleDefOf.Hospital)) continue;
                if (!p.CanReserve(m)) continue;
                var at = Seat(p, m);
                if (!at.IsValid) continue;
                instrument = m;
                seat = at;
                best = d;
            }
            return instrument != null;
        }

        private static IntVec3 Seat(Pawn p, Building m)
        {
            var map = m.Map;
            if (m.def.hasInteractionCell)
            {
                var c = m.InteractionCell;
                return c.InBounds(map) && c.Standable(map) && p.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Some) ? c : IntVec3.Invalid;
            }
            foreach (var c in GenAdj.CellsAdjacentCardinal(m))
                if (c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map) == null && p.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Some))
                    return c;
            return IntVec3.Invalid;
        }

        /// <summary>
        /// The instrument whose sound a people's own instruments make: a tribe's drums (Ideology's drum), anyone else's strings
        /// (Royalty's harp); null when the game has neither, and then only the notes show.
        /// </summary>
        public static ThingDef OwnInstrument(Faction f)
        {
            var drum = DefDatabase<ThingDef>.GetNamedSilentFail("Drum");
            var harp = DefDatabase<ThingDef>.GetNamedSilentFail("Harp");
            bool tribal = f != null && HomeQuests.AgeOf(f) <= HomeQuestsMath.Tribal && !Ages.IsEmpire(f);
            var first = tribal ? drum : harp;
            var other = tribal ? harp : drum;
            return first?.soundPlayInstrument != null ? first : other?.soundPlayInstrument != null ? other : null;
        }

        // ---------------- Words over a head ----------------

        public static void Say(Pawn p, string pool, Pawn to = null, string tag = null) => HomeQuests.Say(p, pool, to, tag);
    }

    // ===================== Training together =====================

    /// <summary>
    /// One of the pair at a young fighter's training (Workers_MissionHome3.cs): to their place beside the other, then, until
    /// the evening's hours are done, pulled blows at each other (sparring) or shots at the colony's target side by side. The
    /// teacher keeps the count. Either one leaving, danger, or the hours done ends it for both.
    /// </summary>
    public class JobDriver_HomeQuestTrain : JobDriver
    {
        private int waited, nextAct = 70;

        private Pawn Partner => job.targetA.Thing as Pawn;

        private Thing Target => job.targetC.Thing;

        private bool Shoot => Target != null;

        private TrainSession Session => HappeningWorker_MissionTrainee.SessionOf(pawn);

        /// <summary>At the training itself (not walking there or waiting for the other).</summary>
        public bool Training { get; private set; }

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() =>
            Partner != null
                ? (Shoot ? "CC_HomeQ2_Train_ReportShoot" : "CC_HomeQ2_Train_ReportSpar").Translate(Partner.Named("OTHER"), (Target?.LabelShort ?? "").Named("TARGET")).Resolve()
                : base.GetReport();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref waited, "waited");
            Scribe_Values.Look(ref nextAct, "nextAct", 70);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Session == null || Session.over);
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOn(() => Shoot && (Target == null || !Target.Spawned));
            AddFinishAction(_ =>
            {
                Training = false;
                HappeningWorker_MissionTrainee.Left(pawn);
            });
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var wait = ToilMaker.MakeToil("HomeQuestTrainWait");
            wait.defaultCompleteMode = ToilCompleteMode.Never;
            wait.handlingFacing = true;
            wait.tickIntervalAction = delta =>
            {
                var o = Partner;
                if (o == null) return;
                if (Shoot) pawn.rotationTracker.FaceTarget(Target);
                else pawn.rotationTracker.FaceTarget(o);
                waited += delta;
                bool there = o.CurJobDef == job.def && !o.pather.Moving && o.Position.InHorDistOf(pawn.Position, Shoot ? 3f : 1.5f);
                if (there) ReadyForNextToil();
                else if (waited > 2500) EndJobWith(JobCondition.Incompletable);
            };
            yield return wait;
            var train = ToilMaker.MakeToil("HomeQuestTrain");
            train.defaultCompleteMode = ToilCompleteMode.Never;
            train.handlingFacing = true;
            train.socialMode = RandomSocialMode.Off;
            train.initAction = () =>
            {
                Training = true;
                var s = Session;
                // The pair's blows by turns: the teacher first.
                nextAct = s != null && s.a == pawn ? 70 : 140;
                // together: the pair at their places side by side; the teacher's first word to the young one.
                if (s != null && s.a == pawn && !s.spoke)
                {
                    s.spoke = true;
                    HomeQuests2.Say(pawn, Shoot ? "Rimfolk_Pool_HomeQ2_TrainShoot" : "Rimfolk_Pool_HomeQ2_TrainSpar", Partner);
                }
            };
            train.tickIntervalAction = delta =>
            {
                var o = Partner;
                var s = Session;
                if (o == null || s == null || s.over)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                if (Shoot) pawn.rotationTracker.FaceTarget(Target);
                else pawn.rotationTracker.FaceTarget(o);
                // The other has stepped away (or not got there yet): wait for them.
                if (!(o.jobs?.curDriver is JobDriver_HomeQuestTrain d) || !d.Training || !o.Position.InHorDistOf(pawn.Position, Shoot ? 3f : 1.5f)) return;
                nextAct -= delta;
                if (nextAct <= 0)
                {
                    nextAct = Shoot ? Rand.RangeInclusive(160, 260) : 140;
                    if (Shoot) Shot();
                    else HappeningWorker_MissionTrainee.Swing(s, pawn, o);
                }
                // The teacher keeps the count of the time trained, for both.
                if (s.a == pawn) HappeningWorker_MissionTrainee.Trained(s, delta);
            };
            yield return train;
        }

        /// <summary>A shot at the target: the weapon's own sound, a flash from a gun, and the hit on the target.</summary>
        private void Shot()
        {
            try
            {
                var map = pawn.Map;
                var t = Target;
                if (map == null || t == null) return;
                var verb = pawn.equipment?.PrimaryEq?.PrimaryVerb;
                var weapon = pawn.equipment?.Primary?.def;
                verb?.verbProps?.soundCast?.PlayOneShot(new TargetInfo(pawn.Position, map));
                var dir = (t.TrueCenter() - pawn.DrawPos).normalized;
                if (weapon != null && weapon.techLevel >= TechLevel.Industrial) FleckMaker.Static(pawn.DrawPos + dir * 0.6f, map, FleckDefOf.ShotFlash, 5f);
                FleckMaker.ThrowDustPuff(t.TrueCenter() + new Vector3(Rand.Range(-0.25f, 0.25f), 0f, Rand.Range(-0.25f, 0.25f)), map, 0.5f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: a shot at the target", e);
            }
        }
    }

    /// <summary>A pair at training: who teaches (<see cref="a"/>), who learns (<see cref="b"/>), for which quest; the one real blow if it comes.</summary>
    public sealed class TrainSession
    {
        public int missionId;
        public Pawn a, b;
        public bool shoot, spoke, over;
        public int swings;
        /// <summary>The swing on which a sparring blow lands for real (-1: none this evening), and on whom.</summary>
        public int hardAt = -1;
        public Pawn hardOn;

        public bool Has(Pawn p) => p != null && (p == a || p == b);
    }

    // ===================== Copying at the bench =====================

    /// <summary>
    /// Copying a scholar's book at the colony's research bench (Workers_MissionHome4.cs): the walk to the bench, then hours
    /// of work there with the game's own look of research, and the game's own learning from it (Intellectual, as at research),
    /// until the copy is done or the stint ends.
    /// </summary>
    public class JobDriver_HomeQuestCopy : JobDriver
    {
        private Thing Bench => job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(Bench, job, 1, -1, null, errorOnFailed)) return false;
            return !Bench.def.hasInteractionCell || pawn.ReserveSittableOrSpot(Bench.InteractionCell, job, errorOnFailed);
        }

        public override string GetReport()
        {
            var m = HomeQuests2.Open(HomeQuests2Math.CopyKind);
            return m == null ? base.GetReport() : "CC_HomeQ2_Copy_Report".Translate((m.name ?? "?").Named("SCHOLAR"), (m.tale ?? "?").Named("BOOK")).Resolve();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => !HappeningWorker_MissionBookCopy.Copying(pawn.Map, out _));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            var copy = ToilMaker.MakeToil("HomeQuestCopy");
            copy.tickIntervalAction = delta =>
            {
                var actor = copy.actor;
                actor.skills?.Learn(SkillDefOf.Intellectual, HomeQuests2Math.CopyXpPerTick * delta);
                actor.GainComfortFromCellIfPossible(delta, true);
                if (HappeningWorker_MissionBookCopy.Copied(actor, delta)) ReadyForNextToil();
            };
            copy.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            copy.WithEffect(EffecterDefOf.Research, TargetIndex.A);
            copy.WithProgressBar(TargetIndex.A, () => HappeningWorker_MissionBookCopy.Progress(pawn.Map));
            copy.defaultCompleteMode = ToilCompleteMode.Delay;
            copy.defaultDuration = 4000;
            copy.activeSkill = () => SkillDefOf.Intellectual;
            yield return copy;
            yield return Toils_General.Wait(2);
        }
    }

    /// <summary>
    /// The colony's researchers copy a visiting scholar's book at a research bench while the quest asks for it: part of
    /// their research work, before the research itself (its priority in the work tab is the research's).
    /// </summary>
    public class WorkGiver_HomeQuestCopy : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.ResearchBench);

        public override bool Prioritized => true;

        public override bool ShouldSkip(Pawn pawn, bool forced = false) => !HappeningWorker_MissionBookCopy.Copying(pawn?.Map, out _);

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Building_ResearchBench) || t.IsForbidden(pawn) || t.IsBurning() || !HappeningWorker_MissionBookCopy.Copying(pawn.Map, out _)) return false;
            if (!pawn.CanReserve(t, 1, -1, null, forced)) return false;
            return !t.def.hasInteractionCell || pawn.CanReserveSittableOrSpot(t.InteractionCell, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) => JobMaker.MakeJob(HomeQuests2DefOf.Rimfolk_HomeQ_Copy, t);

        public override float GetPriority(Pawn pawn, TargetInfo t) => t.Thing?.GetStatValue(StatDefOf.ResearchSpeedFactor) ?? 1f;
    }

    // ===================== Playing for the colony =====================

    /// <summary>
    /// A visiting musician playing (Workers_MissionHome4.cs): at the colony's own instrument (the game's own playing, its
    /// music heard), or with their own where the colony has none for them: standing by the party and playing, the sound of
    /// their people's kind of instrument and the notes over them. Until the stint is over.
    /// </summary>
    public class JobDriver_HomeQuestPlay : JobDriver
    {
        private bool playing;
        [Unsaved] private Sustainer sound;
        private static FleckDef musicFleck;
        private static bool looked;

        private Building_MusicalInstrument Instrument => job.targetA.Thing as Building_MusicalInstrument;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => Instrument == null || pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        public override string GetReport() =>
            (Instrument != null ? "CC_HomeQ2_Play_ReportInstrument" : "CC_HomeQ2_Play_ReportOwn").Translate((Instrument?.LabelShort ?? "").Named("INSTRUMENT")).Resolve();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref playing, "playing");
        }

        private void Stop()
        {
            try
            {
                if (playing) Instrument?.StopPlaying();
                sound?.End();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests at home: the music stops", e);
            }
            sound = null;
            playing = false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => job.targetA.IsValid && (Instrument == null || !Instrument.Spawned || Instrument.IsBurning()));
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating == StoryDanger.High);
            AddFinishAction(_ => Stop());
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var play = ToilMaker.MakeToil("HomeQuestPlay");
            play.initAction = () =>
            {
                var m = Instrument;
                if (m != null)
                {
                    if (m.IsBeingPlayed)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    m.StartPlaying(pawn);
                    playing = true;
                }
            };
            play.defaultCompleteMode = ToilCompleteMode.Delay;
            play.defaultDuration = Mathf.Max(600, job.count);
            play.handlingFacing = true;
            play.socialMode = RandomSocialMode.Quiet;
            play.tickIntervalAction = delta =>
            {
                var map = pawn.Map;
                var m = Instrument;
                if (m != null)
                {
                    pawn.rotationTracker.FaceTarget(m);
                    // Someone else took the instrument over: they leave it be.
                    if (playing && !m.IsBeingPlayed)
                    {
                        playing = false;
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                }
                else
                {
                    if (job.targetC.IsValid) pawn.rotationTracker.FaceCell(job.targetC.Cell);
                    // Their own instrument: its sound where they stand, while they play.
                    if (sound == null && job.thingDefToCarry?.soundPlayInstrument != null && map != null)
                    {
                        try
                        {
                            sound = job.thingDefToCarry.soundPlayInstrument.TrySpawnSustainer(SoundInfo.InMap(new TargetInfo(pawn.Position, map), MaintenanceType.None));
                        }
                        catch (Exception e)
                        {
                            CouncilLog.ErrorOnce("quests at home: their own music", e);
                        }
                    }
                }
                if (map != null && pawn.IsHashIntervalTick(240, delta))
                {
                    if (!looked)
                    {
                        looked = true;
                        musicFleck = DefDatabase<FleckDef>.GetNamedSilentFail("Music");
                    }
                    if (musicFleck != null) FleckMaker.ThrowMetaIcon(m?.Position ?? pawn.Position, map, musicFleck);
                }
            };
            yield return play;
        }
    }
}
