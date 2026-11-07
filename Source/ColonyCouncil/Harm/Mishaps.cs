using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Harm, part 4: hurts from the weather and the work (Harm/HarmFour.cs is the hub). None of these has a message of its
    /// own, as the game gives none for the same wound: each is seen on the map (the flash of the hit, a puff of dust or
    /// smoke, a few words over them, the line they say) and on their health tab, and tended by the game's doctors.
    /// <list type="bullet">
    /// <item>Caught in the hail (2.2.3): anyone of the colony out under the falling hail (not under a roof, no helmet) has a
    /// chance each 250 ticks of a stone's bruise of 1 to 3, the mildest (children may take it); one or two a storm at most.
    /// Calling them in at the first stones, a roofed walk, a helmet or a hard hat prevents it. With Rimfolk's events.</item>
    /// <item>A felling mishap (4.3.1): a tree felled by a cutter under Plants 5 comes down the wrong way one time in 300 (a
    /// stone axe half again as often, a steel one less): a bruise on a leg, a crack one time in four. With the everyday
    /// ailments (it is a work ailment's kin) and the deeper hooks (Patch_FellingMishap).</item>
    /// <item>A failed build that hurts (3.4.1): a construction that fails on something heavy (a wall, furniture of two cells
    /// or a heavy piece, a bridge) bruises the builder's arm one time in ten, a wall or a bridge crushing a foot one time in
    /// five of those. With "Life's ups and downs" (its setbacks) and the deeper hooks (Patch_FailedBuildHurts).</item>
    /// <item>Burned at the bench (4.7.2): with Alpha Skills, a drink before hot work (cooking, smithing) ends one time in 100
    /// in a burn at the stove or forge, or a knife's cut at the stove. The setting "A drink before hot work" (on by default)
    /// keeps the drink from hot work altogether.</item>
    /// </list>
    /// </summary>
    public static class Mishaps
    {
        private static int Now => Find.TickManager.TicksGame;

        /// <summary>Dev mode: the next stone, felling or failed build hurts, past its rate (never past the cap).</summary>
        public static bool DebugHail, DebugFell, DebugBuild;

        public static void Reset()
        {
            drank.Clear();
        }

        // ================= Caught in the hail (2.2.3) =================

        /// <summary>Headgear that turns hail: worn over the top of the head, with blunt armour.</summary>
        private static bool Helmet(Pawn p)
        {
            var worn = p.apparel?.WornApparel;
            if (worn == null) return false;
            for (int i = 0; i < worn.Count; i++)
            {
                var a = worn[i];
                if (a?.def?.apparel == null || !a.def.apparel.CoversBodyPartGroup(BodyPartGroupDefOf.UpperHead)) continue;
                if (a.GetStatValue(StatDefOf.ArmorRating_Blunt) >= HarmFourMath.HelmetBlunt) return true;
            }
            return false;
        }

        /// <summary>
        /// One of the colony out under the falling hail (GameCondition_Hail, each 250 ticks): a stone that bruises, under the
        /// cap. True when it hit them.
        /// </summary>
        public static bool HailStone(Map map, Pawn p, int bruisedThisStorm)
        {
            if (!Harm.On || bruisedThisStorm >= HarmFourMath.HailMaxPerStorm || p == null || !p.Spawned || p.Map != map || p.Downed) return false;
            float chance = HarmFourMath.HailChance(p.Position.Roofed(map), Helmet(p));
            bool debug = DebugHail && chance > 0f;
            if (debug ? !Harm.May(p, HurtClass.Mild) : chance <= 0f || !Harm.Roll(p, HarmKinds.Hail, chance, HurtClass.Mild)) return false;
            DebugHail = false;
            var h = Harm.Injure(p, HurtKind.Bruise, Rand.Bool ? HurtWhere.Arm : HurtWhere.Torso, HarmFourMath.HailLow, HarmFourMath.HailHigh, HarmKinds.Hail, HurtClass.Mild);
            if (h == null) return false;
            VisualTouches.HailHit(map, p.Position);
            HarmFour.Say(p, "Rimfolk_Pool_HailHit");
            HarmFour.Mote(p, "CC_HarmFour_Mote_Hail");
            return true;
        }

        // ================= A felling mishap (4.3.1) =================

        /// <summary>A tree felled (Patch_FellingMishap): one roll for an unskilled cutter.</summary>
        public static void Felled(Plant tree, Pawn by, Map map, IntVec3 at)
        {
            if (!Harm.On || tree?.def?.plant == null || !tree.def.plant.IsTree || !HarmFour.Ours(by) || !Harm.SystemOn(HarmKinds.Felling.system)) return;
            int plants = by.skills?.GetSkill(SkillDefOf.Plants)?.Level ?? 0;
            float chance = HarmFourMath.Felling(plants, Ages.Ours);
            bool debug = DebugFell;
            DebugFell = false;
            if (debug ? !Harm.May(by, HurtClass.Injury) : chance <= 0f || !Harm.Roll(by, HarmKinds.Felling, chance, HurtClass.Injury)) return;
            bool crack = Rand.Chance(HarmFourMath.FellCrackShare);
            var h = crack ? Harm.Injure(by, HurtKind.Crack, HurtWhere.Leg, HarmFourMath.FellCrackLow, HarmFourMath.FellCrackHigh, HarmKinds.Felling) : null;
            if (h == null)
            {
                crack = false;
                h = Harm.Injure(by, HurtKind.Bruise, HurtWhere.Leg, HarmFourMath.FellBruiseLow, HarmFourMath.FellBruiseHigh, HarmKinds.Felling);
            }
            if (h == null) return;
            // The tree came down the wrong way: dust where it fell, the cutter scrambling clear and down on the ground.
            if (map != null && at.IsValid)
            {
                FleckMaker.ThrowDustPuffThick(at.ToVector3Shifted(), map, 2.2f, new UnityEngine.Color(0.65f, 0.55f, 0.45f));
                if (Rand.Bool) FilthMaker.TryMakeFilth(by.Position, map, ThingDefOf.Filth_Dirt, 2);
            }
            HarmFour.SitDown(by, 300);
            HarmFour.Say(by, "Rimfolk_Pool_FellingMishap", null, crack ? "crack" : "bruise", Ages.Tag(Ages.Ours));
            HarmFour.Mote(by, "CC_HarmFour_Mote_Felling");
        }

        // ================= A failed build that hurts (3.4.1) =================

        /// <summary>What a frame was for, as heavy work: a wall, big or heavy furniture, a bridge.</summary>
        public static HeavyBuild HeavyOf(BuildableDef def)
        {
            if (def is TerrainDef t) return t.bridge ? HeavyBuild.Bridge : HeavyBuild.None;
            if (!(def is ThingDef d) || d.building == null) return HeavyBuild.None;
            bool wall = d.passability == Traversability.Impassable && d.fillPercent >= 0.99f && d.size.x == 1 && d.size.z == 1;
            float mass = 0f;
            try
            {
                mass = d.BaseMass;
            }
            catch
            {
                // A modded def with no mass: by its size alone.
            }
            return HarmFourMath.HeavyOf(wall, false, d.size.x * d.size.z, mass);
        }

        /// <summary>A construction failed on the builder (Patch_FailedBuildHurts): heavy work's one roll.</summary>
        public static void BuildFailed(Pawn worker, BuildableDef built, Map map, IntVec3 at)
        {
            if (!Harm.On || !HarmFour.Ours(worker) || built == null || !Harm.SystemOn(HarmKinds.FailedBuild.system)) return;
            var heavy = HeavyOf(built);
            float chance = HarmFourMath.FailedBuild(heavy);
            bool debug = DebugBuild;
            DebugBuild = false;
            if (debug ? !Harm.May(worker, HurtClass.Injury) : chance <= 0f || !Harm.Roll(worker, HarmKinds.FailedBuild, chance, HurtClass.Injury)) return;
            bool crush = (heavy == HeavyBuild.Wall || heavy == HeavyBuild.Bridge) && Rand.Chance(HarmFourMath.BuildCrushShare);
            var h = crush ? Harm.Injure(worker, HurtKind.Crush, HurtWhere.Foot, HarmFourMath.BuildLow, HarmFourMath.BuildHigh, HarmKinds.FailedBuild) : null;
            if (h == null)
            {
                crush = false;
                h = Harm.Injure(worker, HurtKind.Bruise, HurtWhere.Arm, HarmFourMath.BuildLow, HarmFourMath.BuildHigh, HarmKinds.FailedBuild);
            }
            if (h == null) return;
            // The frame came down: dust where it stood, the builder stepping back holding the arm (or the foot).
            if (map != null && at.IsValid) FleckMaker.ThrowDustPuffThick(at.ToVector3Shifted(), map, 1.8f, new UnityEngine.Color(0.7f, 0.65f, 0.6f));
            HarmFour.SitDown(worker, 200);
            HarmFour.Say(worker, "Rimfolk_Pool_BuildFell", null, crush ? "foot" : "arm", heavy.ToString().ToLowerInvariant());
        }

        // ================= Burned at the bench (4.7.2) =================

        /// <summary>A colonist's thingIDNumber: when they had a drink before hot work, and whether its one roll was made.</summary>
        private static readonly Dictionary<int, (int at, bool rolled)> drank = new Dictionary<int, (int, bool)>();

        /// <summary>A drink before hot work was had (Integrations/BeforeWork.cs): the next hour at the stove or the forge has one roll.</summary>
        public static void DrankBeforeHotWork(Pawn p)
        {
            if (p == null || !HotBench.On) return;
            if (drank.Count > 100) drank.Clear();
            drank[p.thingIDNumber] = (Now, false);
        }

        /// <summary>Each colonist's hour: right at the stove or the forge within four hours of such a drink, its one roll.</summary>
        public static void HotHour(Pawn p, int now)
        {
            if (drank.Count == 0 || !drank.TryGetValue(p.thingIDNumber, out var d)) return;
            if (!HarmFourMath.HotBenchKept(d.at, now))
            {
                drank.Remove(p.thingIDNumber);
                return;
            }
            if (d.rolled || WorkHurts.WorkNow(p) != WorkHurtKind.Hot || (p.pather?.Moving ?? false)) return;
            drank[p.thingIDNumber] = (d.at, true);
            bool debug = HotBench.DebugNext;
            HotBench.DebugNext = false;
            if (debug ? !Harm.May(p, HurtClass.Injury) : !HotBench.On || !Harm.Roll(p, HarmKinds.HotBench, HarmFourMath.HotBenchChance, HurtClass.Injury)) return;
            bool cooking = p.CurJob?.workGiverDef?.workType?.defName == "Cooking";
            bool knife = cooking && Rand.Chance(HarmFourMath.KnifeShare);
            var h = Harm.Injure(p, knife ? HurtKind.Cut : HurtKind.Burn, HurtWhere.Hand, HarmFourMath.HotLow, HarmFourMath.HotHigh, HarmKinds.HotBench);
            if (h == null) return;
            p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            HarmFour.Say(p, "Rimfolk_Pool_HotBench", null, knife ? "cut" : "burn", cooking ? "stove" : "forge");
            HarmFour.Mote(p, knife ? "CC_HarmFour_Mote_Knife" : "CC_HarmFour_Mote_Burn");
        }

        /// <summary>Dev mode: as if they had a drink before hot work just now (its roll then comes, past its rate, at the stove or the forge).</summary>
        public static string DebugHot(Pawn p)
        {
            drank[p.thingIDNumber] = (Now, false);
            HotBench.DebugNext = true;
            return p.LabelShort + " had a drink before hot work: the next hour they're at the stove or the forge brings a burn" + (HotBench.On ? "." : " (needs Alpha Skills' drink before work and the setting).");
        }
    }

    /// <summary>
    /// A drink before hot work (Integrations/BeforeWork.cs, Alpha Skills): allowed by the setting "A drink before hot work",
    /// on by default; off, the drink before work leaves the stove and the forge alone.
    /// </summary>
    public static class HotBench
    {
        public static bool DebugNext;

        /// <summary>Alpha Skills' drink before work can be had, and hot work with it.</summary>
        public static bool On => (ColonyCouncilMod.Settings?.drinkBeforeHotWork ?? true) && BeforeWork.Drink.On && BeforeWork.Ready(BeforeWork.Drink);

        /// <summary>The work a job is, as a drink before it would see it: hot (the stove, the forge) or not.</summary>
        public static bool Hot(Job work) => HarmFourMath.HotWork(work?.workGiverDef?.workType?.defName) || HarmFourMath.HotWork(work?.RecipeDef?.requiredGiverWorkType?.defName);

        /// <summary>The setting keeps the drink from hot work.</summary>
        public static bool Barred(Job work) => !(ColonyCouncilMod.Settings?.drinkBeforeHotWork ?? true) && Hot(work);
    }

    /// <summary>A tree felled by one of the colony (the game's Plant.PlantCollected with the cut's chop): a felling mishap's roll.</summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.PlantCollected))]
    public static class Patch_FellingMishap
    {
        public static void Prefix(Plant __instance, out (Map map, IntVec3 at) __state)
        {
            __state = (__instance?.Map, __instance?.Position ?? IntVec3.Invalid);
        }

        public static void Postfix(Plant __instance, Pawn by, PlantDestructionMode plantDestructionMode, (Map map, IntVec3 at) __state)
        {
            if (!RimfolkPatches.On || by == null || plantDestructionMode != PlantDestructionMode.Chop || __instance?.def?.plant == null || !__instance.def.plant.IsTree) return;
            try
            {
                Mishaps.Felled(__instance, by, __state.map, __state.at);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm four: a felling mishap", e);
            }
        }
    }

    /// <summary>A construction failed on the builder (the game's Frame.FailConstruction): heavy work's roll for a hurt.</summary>
    [HarmonyPatch(typeof(Frame), nameof(Frame.FailConstruction))]
    public static class Patch_FailedBuildHurts
    {
        public static void Prefix(Frame __instance, out (BuildableDef built, Map map, IntVec3 at) __state)
        {
            __state = (__instance?.def?.entityDefToBuild, __instance?.Map, __instance?.Position ?? IntVec3.Invalid);
        }

        public static void Postfix(Pawn worker, (BuildableDef built, Map map, IntVec3 at) __state)
        {
            if (!RimfolkPatches.On || worker == null || __state.built == null) return;
            try
            {
                Mishaps.BuildFailed(worker, __state.built, __state.map, __state.at);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm four: a failed build", e);
            }
        }
    }
}
