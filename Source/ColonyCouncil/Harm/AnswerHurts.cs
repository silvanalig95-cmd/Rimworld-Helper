using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Harm, part 6: hurts from the work an answer, a bill or an order sets (Harm/HarmSix.cs is the hub). Each is seen on the
    /// map (the flash of the hit, a puff of smoke or dust, a few words over them, the line they say) and on their health tab;
    /// a message only for an ailment caught, as the game gives one for that.
    /// <list type="bullet">
    /// <item>Hot and heavy answers (2.1.2): an answer's work that plays out on the map, a fire laid and lit for a gathering,
    /// the pyre lit at dusk, the dead carried out, the graves dug for them, has one roll a play-out at the moment of the work:
    /// one in 40, half for skilled hands (Cooking 8 at a fire, Construction 8 at a load or a grave). A burn on a hand, or a
    /// bruise on a foot from what was dropped. Named under the answer. With answers on the map.</item>
    /// <item>Back too soon, for real (2.1.3): when one let back to light work overdoes it (the answer's own risk), a third of
    /// those times, the first hour within the day they're found carrying something, they fall with it: a bruise, two more
    /// days in bed, and whoever is near helps them back to it. Named under the risk. With convalescence.</item>
    /// <item>The makeshift spot's burn (3.4.2): a bill cooked at a campfire or butchered at a butcher's spot while a proper
    /// bench could be built, one in 200: a burn at the fire, a cut at the spot. Never in a colony that can't build better
    /// yet. With "Life's ups and downs" and the deeper hooks.</item>
    /// <item>Taking down hurts too (4.3.2): a wall or a door taken down by someone under Construction 5, one in 100: a bruise
    /// on an arm, a crushed foot one time in five. With the everyday ailments (a work ailment's kin) and the deeper hooks
    /// (Patch_TakingDownHurts).</item>
    /// <item>The hands come home sore (2.6.3): the hands back from a town's harvest bring what a day and a half in the fields
    /// would have given at home, by the everyday ailments' own rates (a strained back, blisters, a sunburn), half for the
    /// tough; it shows as they walk in at home. With the everyday ailments and "Systems talk to each other".</item>
    /// </list>
    /// </summary>
    public static class AnswerHurts
    {
        private static int Now => Find.TickManager.TicksGame;

        /// <summary>Dev mode: the next answer's hot or heavy work, the next makeshift bill, the next wall taken down hurt, past their rates (never past the cap).</summary>
        public static bool DebugWork, DebugMakeshift, DebugTakeDown, DebugHands;

        /// <summary>The play-outs whose hot or heavy work has had its one roll (play-out ids), session memory.</summary>
        private static readonly HashSet<int> rolled = new HashSet<int>();

        public static void Reset() => rolled.Clear();

        // ================= Hot and heavy answers (2.1.2) =================

        /// <summary>The hour: each play-out carrying out the dead or digging their graves, once, when one of ours is right at it.</summary>
        public static void Hourly(int now)
        {
            var tracker = PlayOutTracker.Instance;
            if (tracker == null || !Harm.On || !Harm.SystemOn(HarmKinds.HotHeavy.system)) return;
            foreach (var p in tracker.plays)
            {
                if (p == null || p.kind != PlayKind.Haul || p.state != PlayState.Running || p.hands == null || rolled.Contains(p.id)) continue;
                foreach (var h in p.hands)
                {
                    var x = h?.pawn;
                    if (x == null || !x.Spawned || x.Map != p.map || !HarmFour.Ours(x) || !PlayHaul.OnIt(p, x)) continue;
                    bool carrying = x.carryTracker?.CarriedThing is Corpse;
                    bool digging = !carrying && PlayHaul.Burial(p) && !(x.pather?.Moving ?? false);
                    if (!carrying && !digging) continue;
                    rolled.Add(p.id);
                    if (rolled.Count > 200) rolled.Clear();
                    Work(x, carrying ? AnswerWork.Carry : AnswerWork.Dig, x.Position);
                    break;
                }
            }
        }

        /// <summary>A fire laid and lit for a gathering (JobDriver_PlayOutLayFire), the pyre lit (JobDriver_PlayOutLightPyre): the work's one roll.</summary>
        public static void Lit(Pawn p, AnswerWork work, IntVec3 at)
        {
            try
            {
                if (!Harm.On || !HarmFour.Ours(p) || !Harm.SystemOn(HarmKinds.HotHeavy.system)) return;
                Work(p, work, at);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm six: a fire lit", e);
            }
        }

        private static void Work(Pawn p, AnswerWork work, IntVec3 at)
        {
            bool hot = HarmSixMath.Hot(work);
            int skill = p.skills?.GetSkill(hot ? SkillDefOf.Cooking : SkillDefOf.Construction)?.Level ?? 0;
            bool debug = DebugWork;
            DebugWork = false;
            if (debug ? !Harm.May(p, HurtClass.Injury) : !Harm.Roll(p, HarmKinds.HotHeavy, HarmSixMath.HotHeavy(skill >= HarmSixMath.SkilledHands), HurtClass.Injury)) return;
            if (hot)
            {
                var h = Harm.Injure(p, HurtKind.Burn, HurtWhere.Hand, HarmSixMath.HotLow, HarmSixMath.HotHigh, HarmKinds.HotHeavy);
                if (h == null) return;
                if (p.Map != null && at.IsValid) FleckMaker.ThrowMicroSparks(at.ToVector3Shifted(), p.Map);
                HarmFour.Say(p, "Rimfolk_Pool_HotHeavy", null, work == AnswerWork.Pyre ? "pyre" : "fire");
                HarmFour.Mote(p, "CC_HarmSix_Mote_Burn");
                return;
            }
            // The load, a body or a spade of earth, comes down on a foot.
            if (p.carryTracker?.CarriedThing != null) p.carryTracker.TryDropCarriedThing(p.Position, ThingPlaceMode.Near, out _);
            var b = Harm.Injure(p, HurtKind.Bruise, HurtWhere.Foot, HarmSixMath.HeavyLow, HarmSixMath.HeavyHigh, HarmKinds.HotHeavy);
            if (b == null) return;
            if (p.Map != null) FleckMaker.ThrowDustPuff(p.Position, p.Map, 1.2f);
            HarmFour.SitDown(p, 200);
            HarmFour.Say(p, "Rimfolk_Pool_HotHeavy", null, work == AnswerWork.Dig ? "dig" : "carry");
            HarmFour.Mote(p, "CC_HarmSix_Mote_Dropped");
        }

        /// <summary>An answer's sheet: its hot or heavy work can bring a small hurt (only while the kind's system is on).</summary>
        public static void Describe(ImpactSheet sheet, bool hot, bool heavy)
        {
            if (sheet == null || !Harm.On || !Harm.SystemOn(HarmKinds.HotHeavy.system) || !hot && !heavy) return;
            string key = hot && heavy ? "CC_Impact_HotHeavy_Both" : hot ? "CC_Impact_HotHeavy_Hot" : "CC_Impact_HotHeavy_Heavy";
            sheet.Add(ImpactKind.Health, -1, key.Translate().Resolve(), "CC_Impact_HotHeavyBrief".Translate().Resolve(), "hotheavy");
        }

        // ================= Back too soon, for real (2.1.3) =================

        /// <summary>One let back to light work overdid it (the answer's risk): the fall waits a day for them to be carrying something.</summary>
        public static void OwesFall(Pawn p)
        {
            if (p == null || !Harm.On || !Harm.SystemOn(HarmKinds.BackTooSoon.system)) return;
            var book = HarmBook.Instance;
            if (book == null || HarmSix.WaitingFor(book, p, HarmSix.Fall) != null) return;
            HarmFour.Wait(new HarmWaiting { what = HarmSix.Fall, kind = HarmKinds.BackTooSoon.key, pawn = p, due = Now });
        }

        /// <summary>Each colonist's hour: one owed a fall, found carrying something, has its one roll.</summary>
        public static void FallHour(HarmBook book, Pawn p, int now)
        {
            var x = HarmSix.WaitingFor(book, p, HarmSix.Fall);
            if (x == null || p.carryTracker?.CarriedThing == null || p.Downed || !Together.Able(p)) return;
            book.waiting.Remove(x);
            bool debug = DebugFall;
            DebugFall = false;
            if (debug ? !Harm.May(p, HurtClass.Injury) : !Harm.Roll(p, HarmKinds.BackTooSoon, HarmSixMath.BackTooSoonChance, HurtClass.Injury)) return;
            p.carryTracker.TryDropCarriedThing(p.Position, ThingPlaceMode.Near, out _);
            var h = Harm.Injure(p, HurtKind.Bruise, Rand.Bool ? HurtWhere.Leg : HurtWhere.Torso, HarmSixMath.FallLow, HarmSixMath.FallHigh, HarmKinds.BackTooSoon);
            if (h == null) return;
            HarmFour.SitDown(p, 300);
            HarmFour.Say(p, "Rimfolk_Pool_BackTooSoon");
            HarmFour.Mote(p, "CC_HarmSix_Mote_Fall");
            var conv = HealthTracker.ConvalescenceOf(p);
            if (conv != null) conv.Severity = Mathf.Clamp(conv.Severity + HarmSixMath.FallMoreDays * HealthMath.RecoveryPerDay, conv.Severity, HealthMath.MaxSeverity);
            // Whoever of the colony is near helps them back to bed.
            var by = p.Map.mapPawns.FreeColonistsSpawned.FirstOrDefault(o => o != p && !HarmFour.Child(o) && Together.Can(o, p));
            // together: Together.Can, the helper beside them where they fell.
            if (by != null) HarmFour.Say(by, "Rimfolk_Pool_HelpedToBed", p);
            var bed = p.ownership?.OwnedBed;
            if (bed != null && bed.Spawned && bed.Map == p.Map) p.jobs?.jobQueue?.EnqueueFirst(JobMaker.MakeJob(JobDefOf.LayDown, bed));
        }

        public static bool DebugFall;

        // ================= The makeshift spot's burn (3.4.2) =================

        private static bool MakeshiftBench(ThingDef d) => d != null && (d.defName.EndsWith("Spot", StringComparison.Ordinal) || d.defName == "Campfire");

        /// <summary>A bill done (Patch_SetbackBillDone): cooked at a campfire or butchered at a spot while a proper bench could be built, its one roll.</summary>
        public static void MakeshiftBill(Pawn worker, RecipeDef recipe, Thing bench)
        {
            try
            {
                if (!Harm.On || recipe == null || bench?.def == null || !bench.Spawned || !HarmFour.Ours(worker) || !Harm.SystemOn(HarmKinds.Makeshift.system)) return;
                if (!MakeshiftBench(bench.def)) return;
                bool better = false;
                foreach (var u in Avail.RecipeUsers(recipe))
                    if (u != null && u != bench.def && !MakeshiftBench(u) && u.category == ThingCategory.Building && Avail.CanBuild(u, bench.Map))
                    {
                        better = true;
                        break;
                    }
                float chance = HarmSixMath.Makeshift(true, better, recipe.workSkill == SkillDefOf.Cooking);
                bool debug = DebugMakeshift && chance > 0f;
                if (debug ? !Harm.May(worker, HurtClass.Injury) : chance <= 0f || !Harm.Roll(worker, HarmKinds.Makeshift, chance, HurtClass.Injury)) return;
                DebugMakeshift = false;
                bool fire = bench.def.defName == "Campfire";
                var h = Harm.Injure(worker, fire ? HurtKind.Burn : HurtKind.Cut, HurtWhere.Hand, HarmSixMath.MakeshiftLow, HarmSixMath.MakeshiftHigh, HarmKinds.Makeshift);
                if (h == null) return;
                if (fire) FleckMaker.ThrowSmoke(bench.DrawPos, bench.Map, 0.7f);
                HarmFour.Say(worker, "Rimfolk_Pool_Makeshift", null, fire ? "burn" : "cut");
                HarmFour.Mote(worker, fire ? "CC_HarmFour_Mote_Burn" : "CC_HarmFour_Mote_Knife");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm six: a makeshift bill", e);
            }
        }

        // ================= Taking down hurts too (4.3.2) =================

        /// <summary>A wall (one cell, filling it) or a door.</summary>
        public static bool WallOrDoor(ThingDef d) => d?.building != null && (d.IsDoor || Mishaps.HeavyOf(d) == HeavyBuild.Wall);

        /// <summary>Something taken down by one of the colony (Patch_TakingDownHurts): a wall's or a door's one roll.</summary>
        public static void TookDown(Pawn worker, ThingDef def, Map map, IntVec3 at)
        {
            if (!Harm.On || !HarmFour.Ours(worker) || def == null || !Harm.SystemOn(HarmKinds.TakingDown.system)) return;
            int construction = worker.skills?.GetSkill(SkillDefOf.Construction)?.Level ?? 0;
            float chance = HarmSixMath.TakingDown(WallOrDoor(def), construction);
            bool debug = DebugTakeDown && WallOrDoor(def);
            if (debug ? !Harm.May(worker, HurtClass.Injury) : chance <= 0f || !Harm.Roll(worker, HarmKinds.TakingDown, chance, HurtClass.Injury)) return;
            DebugTakeDown = false;
            bool crush = Rand.Chance(HarmSixMath.TakeDownCrushShare);
            var h = crush ? Harm.Injure(worker, HurtKind.Crush, HurtWhere.Foot, HarmSixMath.TakeDownLow, HarmSixMath.TakeDownHigh, HarmKinds.TakingDown) : null;
            if (h == null)
            {
                crush = false;
                h = Harm.Injure(worker, HurtKind.Bruise, HurtWhere.Arm, HarmSixMath.TakeDownLow, HarmSixMath.TakeDownHigh, HarmKinds.TakingDown);
            }
            if (h == null) return;
            // The wall came apart on them: dust where it stood, the worker jumping back.
            if (map != null && at.IsValid) FleckMaker.ThrowDustPuffThick(at.ToVector3Shifted(), map, 1.8f, new Color(0.7f, 0.65f, 0.6f));
            HarmFour.SitDown(worker, 200);
            HarmFour.Say(worker, "Rimfolk_Pool_TakeDown", null, crush ? "foot" : "arm", def.IsDoor ? "door" : "wall");
        }

        // ================= The hands come home sore (2.6.3) =================

        private static readonly string[] SoreDefs = { "Council_Ail_StrainedBack", "Council_Ail_Blisters", "Council_Ail_Sunburn" };

        private static bool Tough(Pawn p) => p.story?.traits?.allTraits?.Any(t => t.def.defName == "Tough") ?? false;

        /// <summary>A town's harvest done (the harvest mission's Harvested): each hand's day and a half in the fields, rolled once, showing at home.</summary>
        public static void HandsBack(List<Pawn> hands, float share, string place)
        {
            try
            {
                if (hands == null || !Harm.On || !Harm.SystemOn(HarmKinds.HandsSore.system)) return;
                foreach (var p in hands)
                {
                    if (p == null || p.Dead || p.health?.hediffSet == null) continue;
                    bool debug = DebugHands;
                    DebugHands = false;
                    if (!debug && !Harm.Roll(HarmKinds.HandsSore, HarmSixMath.HandsSore(share, Tough(p)))) continue;
                    var def = HarmFour.Def(SoreDefs[HarmSixMath.SoreWhich(Rand.Value)]);
                    if (def == null) continue;
                    HarmFour.Wait(new HarmWaiting { what = HarmSix.Hands, kind = HarmKinds.HandsSore.key, pawn = p, def = def, name = place, due = Now });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm six: the hands back", e);
            }
        }

        /// <summary>At the hour: a hand who brought something home from the fields, home now and up and about, shows it.</summary>
        public static bool HandsHome(HarmWaiting x, int now)
        {
            var p = x.pawn;
            if (p == null || p.Dead || x.def == null) return true;
            if (!p.Spawned || p.Map == null || !p.Map.IsPlayerHome || !HarmFour.Ours(p)) return false;
            if (p.health.hediffSet.HasHediff(x.def)) return true;
            bool back = x.def.defName == "Council_Ail_StrainedBack";
            var cls = back ? HurtClass.Injury : HurtClass.Mild;
            if (!Harm.May(p, cls)) return false;
            var info = AilmentTracker.InfoOf(x.def);
            var h = Harm.Ail(p, x.def, info?.startSeverity.RandomInRange ?? 0.3f, HarmKinds.HandsSore, cls);
            if (h == null) return true;
            if (info != null) AilmentTracker.Instance?.Notify_Caught(p, h);
            HarmFour.Say(p, "Rimfolk_Pool_HandsSore", null, back ? "back" : x.def.defName == "Council_Ail_Blisters" ? "blisters" : "sunburn");
            HarmFour.TellHurt(p, "CC_HarmSix_HandsSore", p.Named("PAWN"), AilmentTracker.NameOf(x.def).Named("ILLNESS"), (x.name ?? "").Named("PLACE"),
                (AilmentTracker.DoctorsNote(x.def) ?? "").Named("NOTE"));
            return true;
        }
    }

    /// <summary>
    /// One let back to light work too soon who overdoes it (Council_DlmH_BackTooSoon's risk): a third of those times they
    /// fall with a load within the day (Harm/AnswerHurts.cs). Its sheet says so under the risk.
    /// </summary>
    public class DilemmaEffect_BackTooSoonFall : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!Harm.On || sheet == null || !Harm.SystemOn(HarmKinds.BackTooSoon.system)) return;
            foreach (var p in Reached(ctx))
            {
                if (HealthTracker.ConvalescenceOf(p) == null) continue;
                sheet.Add(ImpactKind.Health, -1, "CC_Impact_BackTooSoonFall".Translate(p.LabelShort).Resolve(), "CC_Impact_BackTooSoonFallBrief".Translate().Resolve(), "fall|" + p.thingIDNumber);
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            foreach (var p in Targets(ctx).ToList())
                if (HealthTracker.ConvalescenceOf(p) != null)
                    AnswerHurts.OwesFall(p);
        }
    }

    /// <summary>A wall or a door taken down by one of the colony (the game's JobDriver_Deconstruct.FinishedRemoving): its roll for a hurt.</summary>
    [HarmonyPatch(typeof(JobDriver_Deconstruct), "FinishedRemoving")]
    public static class Patch_TakingDownHurts
    {
        public static void Prefix(JobDriver_Deconstruct __instance, out (ThingDef def, Map map, IntVec3 at) __state)
        {
            var t = __instance?.job?.targetA.Thing;
            __state = (t?.def, t?.Map, t?.Position ?? IntVec3.Invalid);
        }

        public static void Postfix(JobDriver_Deconstruct __instance, (ThingDef def, Map map, IntVec3 at) __state)
        {
            if (!RimfolkPatches.On || __instance?.pawn == null || __state.def == null) return;
            try
            {
                AnswerHurts.TookDown(__instance.pawn, __state.def, __state.map, __state.at);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm six: taking down", e);
            }
        }
    }
}
