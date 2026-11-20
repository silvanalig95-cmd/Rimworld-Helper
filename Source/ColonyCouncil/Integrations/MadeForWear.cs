using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A piece made for someone with Nice Inventory Tab, put on when they are back (the player, 18 November 2026: since the
    /// crafter pockets a bill's products, "the pawn who's supposed to wear it doesn't get the job to wear it"; and "even
    /// better would be that the pawn who's supposed to wear it doesn't immediately go ... they just wear it once they are
    /// kind of close by again or back into the base"). The mod (read from its DLL, which the player sent) keeps which bill
    /// is for whom (BillFinished_Patch.wearRequests) and finishes such a bill itself (ExecuteFinishRecipeLogic): it makes the
    /// product, calls the game's RecordsUtility.Notify_BillDone, places the piece by the crafter and orders the one it is
    /// for to wear it at once (TryTakeOrderedJob, queued and forced), with its message. Rimfolk's carrying took the product
    /// into the crafter's pocket inside Notify_BillDone, so the mod had nothing to place and gave no order.
    ///
    /// While the mod finishes such a bill (<see cref="finishingFor"/>), the crafter keeps nothing (Hauling's
    /// Patch_HaulBillProducts stands aside), so the mod places the piece and orders as before. With the setting on, when the
    /// one it is for isn't near (<see cref="MadeForWearMath.GoNow"/>), its order is held: the piece goes to storage like any
    /// other, Rimfolk says it is ready (the mod's own message left out), and they put it on when they next choose a job back
    /// near the base (<see cref="MadeForWearBook"/>). Without the mod, nothing of this is applied.
    /// </summary>
    public static class MadeForWear
    {
        public const string PackageId = "andromeda.niceinventorytab";

        public static readonly Type PatchType = AccessTools.TypeByName("NiceInventoryTab.BillFinished_Patch");

        public static string StandsDownWhy => PatchType == null ? "Nice Inventory Tab isn't loaded" : "";

        /// <summary>Hold the order until they are back (the setting, with the deeper hooks).</summary>
        public static bool Later => RimfolkPatches.On && (ColonyCouncilMod.Settings?.madeForWearLater ?? true);

        /// <summary>While Nice Inventory Tab finishes a bill made for someone: the one it is for; null otherwise.</summary>
        internal static Pawn finishingFor;

        /// <summary>Its order was just held and Rimfolk's message said so: the mod's own message that follows is left out.</summary>
        internal static bool held;

        /// <summary>Free to go and put something on: on the map, not drafted, downed, in a mental state or starving, no threat there.</summary>
        public static bool Free(Pawn p) =>
            p != null && p.Spawned && !p.Dead && !p.Downed && !p.Drafted && !p.InMentalState && p.IsColonistPlayerControlled
            && !(p.needs?.food?.Starving ?? false) && !MapFacts.ThreatOn(p.Map);

        /// <summary>They would go now for this piece (<see cref="MadeForWearMath.GoNow"/>).</summary>
        public static bool Near(Pawn p, Thing piece)
        {
            if (p?.Map == null || piece == null || !piece.Spawned || piece.Map != p.Map) return false;
            var home = p.Map.areaManager?.Home;
            return MadeForWearMath.GoNow(Free(p), home != null && home[p.Position], home != null && home[piece.Position], p.Position.DistanceTo(piece.Position));
        }

        /// <summary>The mod's order held: the piece waits for them, and Rimfolk says it is ready.</summary>
        internal static void Hold(Pawn p, Apparel piece)
        {
            var book = MadeForWearBook.Get;
            if (book == null) return;
            book.Add(p, piece);
            Tell.Message(NewsKind.Colony, Importance.Notable, p.MapHeld, "CC_MadeForWear_Held".Translate(piece.LabelShort.Named("PIECE"), p.Named("PAWN")).Resolve(),
                new LookTargets(piece), MessageTypeDefOf.TaskCompletion);
            held = true;
        }

        /// <summary>
        /// They have just chosen their next job with nothing under way: a piece waiting for them, near now, is put on first
        /// (forced, as the mod's own order was, so the outfit doesn't take it off again; what they wear in its place comes
        /// off, as with the mod's own order). One that is gone, worn, past its days, that they can't wear, or that would take
        /// off something locked on them is let go of.
        /// </summary>
        internal static void NextJob(Pawn p, MadeForWearBook book, ref ThinkResult result)
        {
            var e = book.For(p);
            if (e == null) return;
            var piece = e.piece as Apparel;
            if (piece == null || piece.Destroyed || piece.ParentHolder is Pawn_ApparelTracker || p.Dead || p.apparel == null
                || MadeForWearMath.Stale(e.since, Find.TickManager.TicksGame) || !EquipmentUtility.CanEquip(piece, p) || p.apparel.WouldReplaceLockedApparel(piece))
            {
                book.waiting.Remove(e);
                return;
            }
            // Turned off since: they go when free, wherever they are, as the mod would have had them.
            bool go = Later ? Near(p, piece) : Free(p) && piece.Spawned && piece.Map == p.Map;
            if (!go || piece.IsForbidden(p) || !p.CanReserveAndReach(piece, PathEndMode.ClosestTouch, Danger.Some)) return;
            var job = JobMaker.MakeJob(JobDefOf.Wear, piece);
            job.count = 1;
            job.playerForced = true;
            result = new ThinkResult(job, result.IsValid ? result.SourceNode : null, JobTag.Misc, false);
            book.waiting.Remove(e);
        }
    }

    /// <summary>The pieces made for someone that wait for them to come back (Integrations/MadeForWear.cs).</summary>
    public class MadeForWearBook : GameComponent
    {
        public class Entry : IExposable
        {
            public Pawn pawn;
            public Thing piece;
            public int since;

            public void ExposeData()
            {
                Scribe_References.Look(ref pawn, "pawn");
                Scribe_References.Look(ref piece, "piece");
                Scribe_Values.Look(ref since, "since");
            }
        }

        public List<Entry> waiting = new List<Entry>();

        public MadeForWearBook(Game game)
        {
        }

        public static MadeForWearBook Get => Current.Game?.GetComponent<MadeForWearBook>();

        public Entry For(Pawn p) => p == null ? null : waiting.FirstOrDefault(e => e?.pawn == p);

        /// <summary>The piece is waiting for someone other than <paramref name="p"/> (and its wait hasn't run out).</summary>
        public bool KeptFromOthers(Thing piece, Pawn p)
        {
            for (int i = 0; i < waiting.Count; i++)
            {
                var e = waiting[i];
                if (e != null && e.piece == piece)
                    return MadeForWearMath.KeptFrom(e.pawn != null && !e.pawn.Dead, e.pawn == p, MadeForWearMath.Stale(e.since, Find.TickManager.TicksGame));
            }
            return false;
        }

        public void Add(Pawn p, Thing piece)
        {
            waiting.RemoveAll(e => e == null || e.piece == piece);
            waiting.Add(new Entry { pawn = p, piece = piece, since = Find.TickManager.TicksGame });
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref waiting, "waiting", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                waiting ??= new List<Entry>();
                waiting.RemoveAll(e => e?.pawn == null || e.piece == null);
            }
        }
    }

    /// <summary>Nice Inventory Tab finishing a bill made for someone: whom it is for, meanwhile (Integrations/MadeForWear.cs).</summary>
    [HarmonyPatch]
    public static class Patch_MadeForWearFinish
    {
        public static bool Prepare() => TargetMethod() != null;

        public static string StandsDownWhy() => MadeForWear.StandsDownWhy;

        public static MethodBase TargetMethod() => MadeForWear.PatchType == null ? null : AccessTools.Method(MadeForWear.PatchType, "ExecuteFinishRecipeLogic");

        public static void Prefix(Pawn __2, out Pawn __state)
        {
            __state = MadeForWear.finishingFor;
            MadeForWear.finishingFor = __2;
            MadeForWear.held = false;
        }

        public static Exception Finalizer(Exception __exception, Pawn __state)
        {
            MadeForWear.finishingFor = __state;
            MadeForWear.held = false;
            return __exception;
        }
    }

    /// <summary>The mod's order to wear the piece, held while the one it is for is away (Integrations/MadeForWear.cs).</summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob), new[] { typeof(Job), typeof(JobTag?), typeof(bool) })]
    public static class Patch_MadeForWearOrder
    {
        public static bool Prepare() => MadeForWear.PatchType != null;

        public static string StandsDownWhy() => MadeForWear.StandsDownWhy;

        public static bool Prefix(Pawn ___pawn, Job __0, ref bool __result)
        {
            var target = MadeForWear.finishingFor;
            if (target == null || ___pawn != target || __0?.def != JobDefOf.Wear || !(__0.targetA.Thing is Apparel piece) || !MadeForWear.Later) return true;
            try
            {
                if (MadeForWear.Near(target, piece)) return true;
                MadeForWear.Hold(target, piece);
                __result = true;
                return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a piece made for someone: holding the order", e);
                return true;
            }
        }
    }

    /// <summary>
    /// A piece waiting for the one it was made for isn't taken by anyone else looking for better clothes, the crafter among
    /// them (the player, 20 November 2026: "when the pawn that creates the item would also wear it, they immediately wear
    /// it ... instead of reserving it for the other pawn"). The game's own look for better clothes scores it as worth nothing
    /// to them; an order the player gives still goes (Integrations/MadeForWear.cs).
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), "ApparelScoreGain")]
    public static class Patch_MadeForWearKept
    {
        private static bool broken;

        public static bool Prepare() => MadeForWear.PatchType != null;

        public static string StandsDownWhy() => MadeForWear.StandsDownWhy;

        public static void Postfix(Pawn pawn, Apparel ap, ref float __result)
        {
            if (broken || __result <= 0f || ap == null) return;
            var book = MadeForWearBook.Get;
            if (book == null || book.waiting.Count == 0) return;
            try
            {
                if (book.KeptFromOthers(ap, pawn)) __result = MadeForWearMath.NotForThem;
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("a piece made for someone: keeping it for them", e);
            }
        }
    }

    /// <summary>The mod's own message after an order Rimfolk held, left out: Rimfolk's says what happens (Integrations/MadeForWear.cs).</summary>
    [HarmonyPatch(typeof(Messages), nameof(Messages.Message), new[] { typeof(string), typeof(MessageTypeDef), typeof(bool) })]
    public static class Patch_MadeForWearMessage
    {
        public static bool Prepare() => MadeForWear.PatchType != null;

        public static string StandsDownWhy() => MadeForWear.StandsDownWhy;

        public static bool Prefix() => !(MadeForWear.held && MadeForWear.finishingFor != null);
    }

    /// <summary>
    /// A colonist has chosen their next job with nothing under way: a piece made for them and waiting, near now, is put on
    /// first (Integrations/MadeForWear.cs). Never the game's regular look for a better job while one is under way.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
    public static class Patch_MadeForWearNextJob
    {
        private static bool broken;

        public static bool Prepare() => MadeForWear.PatchType != null;

        public static string StandsDownWhy() => MadeForWear.StandsDownWhy;

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Pawn_JobTracker __instance, Pawn ___pawn, ref ThinkResult __result)
        {
            if (broken || __result.Job == null || __instance?.curJob != null || ___pawn == null) return;
            var book = MadeForWearBook.Get;
            if (book == null || book.waiting.Count == 0 || __result.Job.playerForced || __result.Job.def == JobDefOf.Wear) return;
            try
            {
                MadeForWear.NextJob(___pawn, book, ref __result);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("a piece made for someone: putting it on", e);
            }
        }
    }
}
