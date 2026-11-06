using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The council's questions about what the colony meets that nobody asked about before
    // (Defs/DilemmaDefs/Rimfolk_Dilemmas_Variety2.xml): the cold snap's hours, blight in the fields, a good weapon lying in
    // the stores with two who want it, and an animal whose person has died. Each comes only while the colony is as its
    // letter says, and each answer's words happen in the game: timetables shifted until the cold snap ends, a fire laid in
    // a cold room, the blighted plants (and the field) marked for the growers, the weapon taken up by the one chosen, the
    // animal given a new master who it follows. The rules without game types are in VarietyTwoMath.

    // ===================================== The cold snap's hours =====================================

    /// <summary>
    /// The game's cold snap is on, it freezes outside, and two or more of the colony are out working in it by day. A is
    /// the one whose bedroom is the coldest (a proper room, cold enough for a fire to be kept in it: the answer that lays
    /// one is true), B one of those out in the cold (never A).
    /// </summary>
    public class DilemmaWorker_ColdSnap : DilemmaWorker
    {
        public override bool CanHappenNow(Map map)
        {
            if (!base.CanHappenNow(map) || GameConditionDefOf.ColdSnap == null || !map.gameConditionManager.ConditionIsActive(GameConditionDefOf.ColdSnap)) return false;
            if (!OnMapMath.Between(GenLocalDate.HourOfDay(map), 8, 17) || !VarietyTwoMath.Freezing(map.mapTemperature.OutdoorTemp)) return false;
            return Outside(map).Count >= 2;
        }

        private static List<Pawn> Outside(Map map) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.Downed && p.Awake() && !p.Position.Roofed(map) && p.CurJob != null && p.CurJob.workGiverDef != null).ToList();

        /// <summary>A's own bedroom, if it is one a fire can be kept in and cold enough to want one.</summary>
        public static Room ColdRoom(Pawn p)
        {
            var room = RoomUtil.BedRoom(p);
            if (room == null || room.Map != p.Map || !room.ProperRoom || room.PsychologicallyOutdoors || room.IsHuge) return null;
            return VarietyTwoMath.ColdRoom(room.Temperature) ? room : null;
        }

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger()).ToList();
            if (pawns.Count < def.minColonists) return false;
            var outside = Outside(map);
            var cold = pawns.Where(p => def.roleA.Allows(p, null) && ColdRoom(p) != null).OrderBy(p => ColdRoom(p).Temperature).FirstOrDefault();
            if (cold == null) return false;
            var b = outside.Where(p => p != cold).InRandomOrder().FirstOrDefault();
            if (b == null) return false;
            ctx = new DilemmaContext { def = def, map = map, a = cold, b = b };
            return true;
        }

        /// <summary>"freezing" when it is well below freezing outside, for the letter's lines.</summary>
        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx?.map != null && ctx.map.mapTemperature.OutdoorTemp <= -10f) tags.Add("freezing");
        }
    }

    // ===================================== Blight in the fields =====================================

    /// <summary>The colony's blight: plants of its growing zones the game's crop blight has reached.</summary>
    public static class FieldBlight
    {
        public static IEnumerable<Zone_Growing> Fields(Map map) =>
            map?.zoneManager?.AllZones.OfType<Zone_Growing>() ?? Enumerable.Empty<Zone_Growing>();

        /// <summary>Every plant of the colony's fields that has the blight, and isn't marked to cut already.</summary>
        public static List<Plant> Blighted(Map map, bool marked = true)
        {
            var list = new List<Plant>();
            foreach (var z in Fields(map))
                foreach (var c in z.cells)
                {
                    var plant = c.GetPlant(map);
                    if (plant == null || !plant.Blighted) continue;
                    if (!marked && map.designationManager.DesignationOn(plant, DesignationDefOf.CutPlant) != null) continue;
                    list.Add(plant);
                }
            return list;
        }

        /// <summary>The rest of the fields the blight has got into: plants without it that could be harvested now, though not ripe.</summary>
        public static List<Plant> Unripe(Map map)
        {
            var list = new List<Plant>();
            foreach (var z in Fields(map))
            {
                var here = z.cells.Select(c => c.GetPlant(map)).Where(p => p != null).ToList();
                if (!here.Any(p => p.Blighted)) continue;
                list.AddRange(here.Where(p => !p.Blighted && p.def.plant != null && p.def.plant.Harvestable && p.HarvestableNow && p.Growth < 0.999f
                                              && map.designationManager.DesignationOn(p) == null));
            }
            return list;
        }

        /// <summary>A mark of <paramref name="des"/> on the plant, in place of any it had.</summary>
        public static void Mark(Map map, Plant p, DesignationDef des)
        {
            if (p == null || !p.Spawned || des == null) return;
            var dm = map.designationManager;
            if (dm.DesignationOn(p, des) != null) return;
            dm.RemoveAllDesignationsOn(p, false);
            dm.AddDesignation(new Designation(p, des));
        }
    }

    /// <summary>
    /// The game's crop blight has reached two or more plants of the colony's fields. A is the colony's best grower, B the
    /// next (never A). {COUNT}: how many plants have it.
    /// </summary>
    public class DilemmaWorker_Blight : DilemmaWorker
    {
        public override bool CanHappenNow(Map map) => base.CanHappenNow(map) && VarietyTwoMath.BlightWorthAsking(FieldBlight.Blighted(map, false).Count);

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger()).ToList();
            if (pawns.Count < def.minColonists) return false;
            var growers = pawns.Where(p => def.roleA.Allows(p, null) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Growing))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Plants)?.Level ?? 0).ToList();
            if (growers.Count < 2) return false;
            ctx = new DilemmaContext { def = def, map = map, a = growers[0], b = growers[1], count = FieldBlight.Blighted(map, false).Count };
            return true;
        }

        /// <summary>"spread" when the blight is in more than one field or a dozen plants and more, "ripe" when part of the field could come in now.</summary>
        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx?.map == null) return;
            var sick = FieldBlight.Blighted(ctx.map);
            if (sick.Count >= 12 || sick.Select(p => p.Position.GetZone(ctx.map)).Distinct().Count() > 1) tags.Add("spread");
            if (FieldBlight.Unripe(ctx.map).Count > 0) tags.Add("ripe");
        }
    }

    /// <summary>
    /// The blighted plants of the colony's fields marked for cutting (the game's own cut designation, done by the
    /// growers in their own time, so the blight goes no further); with <see cref="field"/>, also every plant of those
    /// fields that could come in now though not ripe marked for harvest, so it's in before the blight reaches it. Can't
    /// be chosen once the blight is gone.
    /// </summary>
    public class DilemmaEffect_CutBlight : DilemmaEffect
    {
        public bool field;

        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) =>
            ctx?.map != null && FieldBlight.Blighted(ctx.map, false).Count == 0 ? "CC_DlmE_NoBlight".Translate().ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var map = ctx?.map;
            if (map == null || FieldBlight.Blighted(map, false).Count == 0) return;
            bool more = field && FieldBlight.Unripe(map).Count > 0;
            sheet.Play((more ? "CC_Impact_BlightField" : "CC_Impact_BlightCut").Translate().Resolve(), (more ? "CC_Impact_BlightFieldBrief" : "CC_Impact_BlightCutBrief").Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null) return;
            try
            {
                var sick = FieldBlight.Blighted(map, false);
                var unripe = field ? FieldBlight.Unripe(map) : new List<Plant>();
                foreach (var p in sick) FieldBlight.Mark(map, p, DesignationDefOf.CutPlant);
                foreach (var p in unripe) FieldBlight.Mark(map, p, DesignationDefOf.HarvestPlant);
                if (sick.Count == 0 && unripe.Count == 0) return;
                Tell.Message(NewsKind.Colony, Importance.Notable, map, (unripe.Count > 0 ? "CC_DlmE_BlightFieldMarked" : "CC_DlmE_BlightMarked").Translate().Resolve(),
                    new LookTargets(sick.Concat(unripe).Take(20).Cast<Thing>()), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("blight marked", e);
            }
        }
    }

    // ===================================== The good weapon =====================================

    /// <summary>The colony's stored weapons, and who of the colony would be better armed with one.</summary>
    public static class StoredWeapons
    {
        /// <summary>A fine weapon (good or better) in the colony's storage, unforbidden, nobody's but the colony's.</summary>
        public static IEnumerable<Thing> Fine(Map map) =>
            map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon).Where(t => t.Spawned && t.def.IsWeapon && t.def.equipmentType == EquipmentType.Primary
                                                                                && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage() && !CompBiocodable.IsBiocoded(t)
                                                                                && t.TryGetQuality(out var q) && q >= QualityCategory.Good);

        private static SkillDef SkillFor(ThingDef weapon) => weapon.IsRangedWeapon ? SkillDefOf.Shooting : SkillDefOf.Melee;

        /// <summary>Whether <paramref name="p"/> would take <paramref name="weapon"/> up: able to, skilled enough, and better armed for it.</summary>
        public static bool Wants(Pawn p, Thing weapon)
        {
            if (p?.equipment == null || weapon == null || p.Downed || p.WorkTagIsDisabled(WorkTags.Violent) || p.DevelopmentalStage.Juvenile()) return false;
            if (!EquipmentUtility.CanEquip(weapon, p)) return false;
            if (weapon.def.IsRangedWeapon && p.story?.traits != null && p.story.traits.HasTrait(TraitDefOf.Brawler)) return false;
            var skill = p.skills?.GetSkill(SkillFor(weapon.def));
            if (skill == null || skill.TotallyDisabled || skill.Level < VarietyTwoMath.MinSkill) return false;
            var now = p.equipment.Primary;
            bool same = now != null && now.def.IsRangedWeapon == weapon.def.IsRangedWeapon;
            return VarietyTwoMath.Upgrade(weapon.MarketValue, now?.MarketValue ?? 0f, now != null, same);
        }

        /// <summary>The finest stored weapon of this kind that <paramref name="p"/> would take up (null when none is left).</summary>
        public static Thing For(Map map, ThingDef def, Pawn p) =>
            map == null || def == null ? null : Fine(map).Where(t => t.def == def && Wants(p, t)).OrderByDescending(t => t.MarketValue).FirstOrDefault();

        public static int Level(Pawn p, ThingDef weapon) => p?.skills?.GetSkill(SkillFor(weapon))?.Level ?? 0;
    }

    /// <summary>
    /// A fine weapon ({THING}, good or better) lies in the stores, and two of the colony would be better armed with it,
    /// each skilled at its kind ({SKILL}: shooting or melee). A is the more skilled, B the other. Tags "ranged" or "melee".
    /// </summary>
    public class DilemmaWorker_GoodWeapon : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.IsSlave).ToList();
            if (pawns.Count < def.minColonists) return false;
            foreach (var weapon in StoredWeapons.Fine(map).OrderByDescending(t => t.MarketValue).ToList())
            {
                var fit = pawns.Where(p => def.roleA.Allows(p, null) && StoredWeapons.Wants(p, weapon)).OrderByDescending(p => StoredWeapons.Level(p, weapon.def)).ToList();
                if (fit.Count < 2) continue;
                ctx = new DilemmaContext
                {
                    def = def, map = map, a = fit[0], b = fit[1], thing = weapon.def, count = 1,
                    skill = weapon.def.IsRangedWeapon ? SkillDefOf.Shooting : SkillDefOf.Melee
                };
                return true;
            }
            return false;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx?.thing != null) tags.Add(ctx.thing.IsRangedWeapon ? "ranged" : "melee");
        }
    }

    /// <summary>
    /// The dilemma's weapon ({THING}) taken up by the target (A unless given): they walk to where it lies in the stores
    /// and arm themselves with it (the game's own equip job, which sets down what they carried), at once when they're
    /// free, else after what they're doing. Can't be chosen once it's gone from the stores or isn't a step up for them.
    /// </summary>
    public class DilemmaEffect_TakeUp : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private Pawn Who(DilemmaContext ctx) => target == DilemmaTarget.B ? ctx?.b : ctx?.a;

        public override string Unavailable(DilemmaContext ctx)
        {
            var p = Who(ctx);
            if (p == null || ctx.thing == null) return null;
            return StoredWeapons.For(ctx.map, ctx.thing, p) == null ? "CC_DlmE_WeaponGone".Translate(ctx.thing.label).Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var p = Who(ctx);
            if (p == null || ctx.thing == null || StoredWeapons.For(ctx.map, ctx.thing, p) == null) return;
            sheet.Play("CC_Impact_TakeUp".Translate(p.LabelShort, ctx.thing.label).Resolve(), "CC_Impact_TakeUpBrief".Translate(p.LabelShort, ctx.thing.label).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var p = Who(ctx);
            if (p == null || p.Dead || !p.Spawned || p.Map != ctx.map || ctx.thing == null) return;
            try
            {
                var weapon = StoredWeapons.For(ctx.map, ctx.thing, p);
                if (weapon == null || !p.CanReach(weapon, PathEndMode.ClosestTouch, Danger.Some)) return;
                var job = JobMaker.MakeJob(JobDefOf.Equip, weapon);
                bool free = !p.Downed && !p.Drafted && p.Awake() && !p.InMentalState && !p.InBed() && ctx.map.dangerWatcher.DangerRating == StoryDanger.None;
                if (free) p.jobs.TryTakeOrderedJob(job, JobTag.Misc, false);
                else p.jobs.jobQueue.EnqueueLast(job, JobTag.Misc);
                Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map, "CC_DlmE_TakingUp".Translate(p.Named("PAWN"), weapon.LabelNoCount).Resolve(), new LookTargets(weapon), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("weapon taken up", e);
            }
        }
    }

    // ===================================== The animal whose person died =====================================

    /// <summary>
    /// An animal of the colony's, trained to obey and bonded to someone of the colony who died lately (in the last
    /// <see cref="VarietyTwoMath.LossDays"/> days), with no living master and no living bond. {PET} is the animal; A
    /// is the one who was closest to the dead and could handle it, B the colony's best handler (never A).
    /// </summary>
    public class DilemmaWorker_MasterGone : DilemmaWorker
    {
        /// <summary>The bonded person of <paramref name="pet"/> who died, if every one it was bonded to is dead.</summary>
        public static Pawn DeadPerson(Pawn pet)
        {
            var bonds = pet?.relations?.DirectRelations?.Where(r => r.def == PawnRelationDefOf.Bond && r.otherPawn != null && r.otherPawn.RaceProps.Humanlike).ToList();
            if (bonds.NullOrEmpty() || bonds.Any(r => !r.otherPawn.Dead)) return null;
            return bonds.Select(r => r.otherPawn).OrderBy(DaysSince).FirstOrDefault();
        }

        /// <summary>
        /// Days since <paramref name="dead"/> died: from the body when there is one, else from the freshest memory of them
        /// among the colony (their death remembered); -1 when nothing tells.
        /// </summary>
        public static float DaysSince(Pawn dead)
        {
            int now = Find.TickManager.TicksGame;
            var body = dead?.Corpse;
            if (body != null && body.timeOfDeath > 0) return (now - body.timeOfDeath) / (float)GenDate.TicksPerDay;
            int age = int.MaxValue;
            foreach (var c in PawnsFinder.AllMaps_FreeColonistsSpawned)
            {
                var mems = c.needs?.mood?.thoughts?.memories?.Memories;
                if (mems == null) continue;
                foreach (var m in mems)
                    if (m.otherPawn == dead && m.age < age) age = m.age;
            }
            return age == int.MaxValue ? -1f : age / (float)GenDate.TicksPerDay;
        }

        private static bool Fits(Pawn pet)
        {
            if (pet == null || pet.Dead || pet.Downed || !(pet.RaceProps?.Animal ?? false) || pet.playerSettings == null || pet.training == null) return false;
            if (!pet.training.HasLearned(TrainableDefOf.Obedience) || AnimalPenUtility.NeedsToBeManagedByRope(pet)) return false;
            var master = pet.playerSettings.Master;
            if (master != null && !master.Dead) return false;
            var dead = DeadPerson(pet);
            return dead != null && VarietyTwoMath.RecentLoss(DaysSince(dead));
        }

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.IsSlave && !p.DevelopmentalStage.Juvenile()).ToList();
            if (pawns.Count < def.minColonists) return false;
            foreach (var pet in map.mapPawns.SpawnedColonyAnimals.Where(Fits).InRandomOrder().ToList())
            {
                var dead = DeadPerson(pet);
                var can = pawns.Where(p => def.roleA.Allows(p, null) && TrainableUtility.CanBeMaster(p, pet, true)).ToList();
                if (can.Count < 2) continue;
                var a = can.OrderByDescending(p => p.relations?.OpinionOf(dead) ?? 0).First();
                var b = can.Where(p => p != a).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0).First();
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b, pet = pet };
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// The target (A unless given) becomes the dilemma's animal's master, the game's own (it follows them to the fields
    /// and when they're drafted, as the master's setting has it), and it keeps at their heels for the next day as it
    /// gets used to them (with "Answers play out on the map" on). Can't be chosen when they couldn't handle it.
    /// </summary>
    public class DilemmaEffect_NewMaster : DilemmaEffect
    {
        public float hours = 24f;

        public override bool ChanceEach => false;

        private Pawn Who(DilemmaContext ctx) => target == DilemmaTarget.B ? ctx?.b : ctx?.a;

        private static bool Ours(Pawn pet) => pet != null && !pet.Dead && pet.Faction == Faction.OfPlayer && pet.playerSettings != null;

        public override string Unavailable(DilemmaContext ctx)
        {
            var p = Who(ctx);
            var pet = ctx?.pet;
            if (p == null || !Ours(pet)) return null;
            return TrainableUtility.CanBeMaster(p, pet, false) ? null : "CC_DlmE_CantHandle".Translate(p.Named("PAWN"), pet.Named("PET")).Resolve();
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var p = Who(ctx);
            var pet = ctx?.pet;
            if (p == null || !Ours(pet) || !TrainableUtility.CanBeMaster(p, pet, false)) return;
            sheet.Play("CC_Impact_NewMaster".Translate(pet.LabelShort, p.LabelShort).Resolve(), "CC_Impact_NewMasterBrief".Translate(pet.LabelShort, p.LabelShort).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var p = Who(ctx);
            var pet = ctx?.pet;
            if (p == null || p.Dead || !Ours(pet) || !TrainableUtility.CanBeMaster(p, pet, false)) return;
            try
            {
                pet.playerSettings.Master = p;
                Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map, "CC_DlmE_NewMaster".Translate(p.Named("PAWN"), pet.Named("PET")).Resolve(), new LookTargets(pet), MessageTypeDefOf.NeutralEvent);
                // At their heels a day, as an animal stays close to someone new (PlayOut/OnMapTimed.cs keeps it there).
                var timed = OnMapTimed.Instance;
                if (!PlayOuts.On || timed == null || !p.Spawned || !pet.Spawned || p.Map != pet.Map) return;
                timed.kept.RemoveAll(k => k.kind == TimedKind.Follow && k.animal == pet);
                timed.Add(new TimedThing
                {
                    kind = TimedKind.Follow, map = pet.Map, pawn = p, animal = pet,
                    until = Find.TickManager.TicksGame + (int)(Mathf.Clamp(hours, 4f, 72f) * Sched.HourTicks)
                });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("new master", e);
            }
        }
    }
}
