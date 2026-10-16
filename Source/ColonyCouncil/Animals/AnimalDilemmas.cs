using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The animals' dilemmas and customs (Defs/DilemmaDefs/Rimfolk_Dilemmas_Animals.xml): the herd in a famine, a wild
    // animal in the crops, a sick animal and the medicine it would take, a predator stalking the pens, a bond the faith
    // frowns on, and the herd's year (the lambing watch, the shearing, bringing the herd home). Their answers play out
    // on the map: the handlers slaughter what the council culls, the hunters go out, a watch is kept at the pens.

    // ===================== Who and what =====================

    /// <summary>
    /// The colony is going hungry (its food lasts fewer than <see cref="AnimalMath.FamineDays"/> days) with a herd of four
    /// or more eating from the same stores. {PET} is one of the herd, {COUNT} how many a cull would take.
    /// </summary>
    public class DilemmaWorker_Famine : DilemmaWorker
    {
        public override bool CanHappenNow(Map map) =>
            base.CanHappenNow(map) && AnimalMath.Famine(MapFacts.Of(map).FoodDays, AnimalFacts.HerdCount(map));

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            var herd = AnimalFacts.Herd(ctx.map);
            ctx.count = AnimalMath.CullCount(herd.Count);
            ctx.pet = herd.Where(a => PetTracker.PersonOf(a) == null).OrderByDescending(AnimalFacts.LifeShare).FirstOrDefault() ?? herd.FirstOrDefault();
            return ctx.count > 0 && ctx.pet != null;
        }
    }

    /// <summary>A wild animal grazing in the colony's fields (X): a plant-eater within a few steps of a crop that's growing.</summary>
    public class DilemmaWorker_CropRaider : DilemmaWorker_Stranger
    {
        public static bool NearCrops(Pawn x, Map map, float radius = 6f)
        {
            if (x == null || map == null || !x.Spawned) return false;
            foreach (var c in GenRadial.RadialCellsAround(x.Position, radius, true))
            {
                if (!c.InBounds(map) || !(c.GetZone(map) is Zone_Growing)) continue;
                var plant = c.GetPlant(map);
                if (plant != null && plant.def.plant != null && plant.def.plant.Harvestable && plant.Growth > 0.2f) return true;
            }
            return false;
        }

        protected override bool Extra(DilemmaContext ctx) =>
            base.Extra(ctx) && ctx.stranger != null && !ctx.stranger.RaceProps.predator && NearCrops(ctx.stranger, ctx.map);
    }

    /// <summary>
    /// A predator on the colony's land (X), within a long stone's throw of the herd: {PET} is the herd animal nearest it.
    /// Needs two head of livestock or more.
    /// </summary>
    public class DilemmaWorker_PenStalker : DilemmaWorker_Stranger
    {
        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx) || ctx.stranger == null) return false;
            var herd = AnimalFacts.Herd(ctx.map);
            if (herd.Count < 2) return false;
            var near = herd.OrderBy(a => a.Position.DistanceTo(ctx.stranger.Position)).First();
            if (!near.Position.InHorDistOf(ctx.stranger.Position, 45f)) return false;
            ctx.pet = near;
            return true;
        }
    }

    /// <summary>
    /// An animal of the colony's is ill ({PET}, {THEIRILLNESS} its illness): A is the one it belongs to, or the herdmaster
    /// when it belongs to nobody. Medicine is the dilemma's own condition (maxMedicine, minMedicine).
    /// </summary>
    public class DilemmaWorker_SickAnimal : DilemmaWorker
    {
        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            var sick = ctx.map.mapPawns.SpawnedColonyAnimals.Where(a => AnimalFacts.Illness(a) != null).ToList();
            if (sick.Count == 0) return false;
            ctx.pet = sick.FirstOrDefault(a => AnimalFacts.Theirs(a, ctx.a))
                      ?? (AnimalFacts.Herdmaster == ctx.a ? sick.FirstOrDefault(a => PetTracker.PersonOf(a) == null) : null);
            if (ctx.pet == null) return false;
            // {THEIRILLNESS} names X's illness: the animal is X too, for the effects that act on it.
            ctx.stranger = ctx.pet;
            return true;
        }
    }

    /// <summary>
    /// A colonist (A) bonded to an animal ({PET}), and another (B) whose faith holds that bonding with animals is wrong
    /// (Wishes/Beliefs.cs: the precepts that abhor it), A's own faith not minding.
    /// </summary>
    public class DilemmaWorker_BondFaith : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!ModsConfig.IdeologyActive || !CanHappenNow(map)) return false;
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger()).ToList();
            if (pawns.Count < def.minColonists) return false;
            var objectors = pawns.Where(p => Beliefs.Forbids(p, Belief.NoPets) && (def.roleB == null || def.roleB.Allows(p, null))).ToList();
            if (objectors.Count == 0) return false;
            foreach (var a in pawns.InRandomOrder())
            {
                if (Beliefs.Forbids(a, Belief.NoPets) || !def.roleA.Allows(a, null)) continue;
                var pet = AnimalFacts.BondedAnimal(a);
                if (pet == null || !pet.Spawned) continue;
                var b = objectors.Where(o => o != a).RandomElementWithFallback();
                if (b == null) continue;
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b, pet = pet, count = def.thingCount };
                if (Extra(ctx)) return true;
            }
            ctx = null;
            return false;
        }
    }

    /// <summary>
    /// What a herd custom needs beyond its season (on a dilemma with <see cref="DilemmaWorker_HerdCustom"/>, beside the
    /// usual <see cref="CustomConditions"/>): a herd of at least <see cref="minHerd"/> head, and animals to shear, to
    /// milk, in lamb or young ones about.
    /// </summary>
    public class HerdConditions : DefModExtension
    {
        public int minHerd = 3;
        public bool shearable;
        public bool milkable;
        /// <summary>Shearable or milkable, either will do.</summary>
        public bool shearOrMilk;
        public bool pregnant;
        public bool young;
    }

    /// <summary>A custom of the herd's year: the seasonal custom's conditions and the herd's own. {PET} is one of the animals it's for.</summary>
    public class DilemmaWorker_HerdCustom : DilemmaWorker_Custom
    {
        private bool Fits(Pawn a, HerdConditions h)
        {
            if (h.shearable && !AnimalFacts.Shearable(a)) return false;
            if (h.milkable && !AnimalFacts.Milkable(a)) return false;
            if (h.shearOrMilk && !AnimalFacts.Shearable(a) && !AnimalFacts.Milkable(a)) return false;
            if (h.pregnant && !AnimalFacts.Pregnant(a)) return false;
            if (h.young && !AnimalFacts.Young(a)) return false;
            return true;
        }

        public override bool CanHappenNow(Map map)
        {
            if (!base.CanHappenNow(map)) return false;
            var h = def.GetModExtension<HerdConditions>();
            if (h == null) return AnimalFacts.HerdCount(map) >= 3;
            if (AnimalFacts.HerdCount(map) < h.minHerd) return false;
            return AnimalFacts.Herd(map).Any(a => Fits(a, h));
        }

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            var h = def.GetModExtension<HerdConditions>() ?? new HerdConditions();
            ctx.pet = AnimalFacts.Herd(ctx.map).Where(a => Fits(a, h)).RandomElementWithFallback();
            return ctx.pet != null;
        }
    }

    // ===================== What the answers do =====================

    /// <summary>
    /// The council culls the herd: <see cref="count"/> head (or the letter's {COUNT}) are marked for slaughter, and the
    /// colony's handlers see to it with the game's own work. Nobody's own animal, nothing bonded or named, and never a
    /// mother in lamb or a youngster while an old one is left.
    /// </summary>
    public class DilemmaEffect_Cull : DilemmaEffect
    {
        public int count;

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.map == null) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            try
            {
                int n = count > 0 ? count : Math.Max(1, ctx.count);
                var dm = ctx.map.designationManager;
                var pick = AnimalFacts.Herd(ctx.map)
                    .Where(a => PetTracker.PersonOf(a) == null && (a.Name == null || a.Name.Numerical) && dm.DesignationOn(a) == null && !AnimalFacts.Pregnant(a))
                    .OrderByDescending(a => AnimalFacts.Young(a) ? 0f : 1f + AnimalFacts.LifeShare(a))
                    .Take(n).ToList();
                if (pick.Count == 0) return;
                foreach (var a in pick) dm.AddDesignation(new Designation(a, DesignationDefOf.Slaughter));
                Messages.Message("CC_Dlm_Culled".Translate(pick.Count, TextUtil.JoinAnd(pick.Select(a => a.LabelShort).Distinct().ToList())).Resolve(),
                    new LookTargets(pick), MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("cull the herd", e);
            }
        }
    }

    /// <summary>
    /// The herd's least animals are turned out to fend for themselves (<see cref="count"/>, or the letter's {COUNT}): no
    /// longer the colony's. Nobody's own, nothing bonded or named.
    /// </summary>
    public class DilemmaEffect_TurnOut : DilemmaEffect
    {
        public int count;

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.map == null) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            try
            {
                int n = count > 0 ? count : Math.Max(1, ctx.count);
                var pick = AnimalFacts.Herd(ctx.map)
                    .Where(a => PetTracker.PersonOf(a) == null && (a.Name == null || a.Name.Numerical) && !AnimalFacts.Pregnant(a))
                    .OrderByDescending(AnimalFacts.LifeShare).Take(n).ToList();
                foreach (var a in pick)
                {
                    a.jobs?.StopAll();
                    a.SetFaction(null);
                }
                if (pick.Count > 0)
                    Messages.Message("CC_Dlm_TurnedOut".Translate(pick.Count).Resolve(), new LookTargets(pick), MessageTypeDefOf.NegativeEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("turn the herd out", e);
            }
        }
    }

    /// <summary>
    /// A setback for the herd: one animal of it is gone in the night (the dilemma's {PET} when it's nobody's own, else
    /// another of the herd that's nobody's own), carried off over the fence. Never a bonded or named animal.
    /// </summary>
    public class DilemmaEffect_LoseAnimal : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.map == null) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            try
            {
                bool Spare(Pawn a) => a != null && AnimalFacts.Livestock(a) && a.Spawned && PetTracker.PersonOf(a) == null && (a.Name == null || a.Name.Numerical);
                var lost = Spare(ctx.pet) ? ctx.pet : AnimalFacts.Herd(ctx.map).Where(Spare).RandomElementWithFallback();
                if (lost == null) return;
                string label = lost.LabelIndefinite();
                var at = lost.Position;
                lost.jobs?.StopAll();
                lost.Destroy();
                Messages.Message("CC_Dlm_AnimalLost".Translate(label.Named("ANIMAL")).Resolve().CapitalizeFirst(), new LookTargets(at, ctx.map), MessageTypeDefOf.NegativeEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("an animal lost", e);
            }
        }
    }

    /// <summary>The care the dilemma's animal ({PET}) gets from the doctors: the medical care setting, the game's own.</summary>
    public class DilemmaEffect_AnimalCare : DilemmaEffect
    {
        public MedicalCareCategory care = MedicalCareCategory.Best;

        public override void Apply(DilemmaContext ctx)
        {
            var a = ctx?.pet;
            if (!AnimalFacts.Ours(a) || a.playerSettings == null) return;
            a.playerSettings.medCare = care;
        }
    }

    /// <summary>
    /// The wild animal (X) in the fields eats its fill: a few of the crops nearest it are gone (<see cref="count"/>), and the
    /// growers feel it as a crop lost (the setback, through the usual watch on crops).
    /// </summary>
    public class DilemmaEffect_CropsEaten : DilemmaEffect
    {
        public int count = 6;

        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            var map = ctx?.map;
            if (x == null || map == null || !x.Spawned) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            try
            {
                var crops = GenRadial.RadialCellsAround(x.Position, 12f, true)
                    .Where(c => c.InBounds(map) && c.GetZone(map) is Zone_Growing)
                    .Select(c => c.GetPlant(map)).Where(p => p != null && p.def.plant != null && p.def.plant.Harvestable && p.Growth > 0.2f)
                    .Distinct().Take(Math.Max(1, count)).ToList();
                if (crops.Count == 0) return;
                var first = crops[0];
                Setbacks.Notify_CropLost(first, false);
                foreach (var p in crops) p.Destroy();
                Messages.Message("CC_Dlm_CropsEaten".Translate(crops.Count, x.Named("X")).Resolve(), new LookTargets(x), MessageTypeDefOf.NegativeEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("crops eaten", e);
            }
        }
    }
}
