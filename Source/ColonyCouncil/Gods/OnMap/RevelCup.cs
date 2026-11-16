using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The god in a cup (the player, 15 November 2026: "what would be interesting is that the god could take shape inside an
    /// actual drug, and once a pawn consumes it, they will get a godly effect from it"). Now and then Revel takes one drink or
    /// one dose of a stack in the colony's stores (any of his: beer, a joint, a draught, a coffee, whatever the colony has).
    /// It looks like any other: no label, no glow. At most someone passing notices something small about it, once ("this
    /// keg smells sweeter than it should"). The one he is in is followed through the game's own splitting and merging of
    /// stacks (RevelCupMath.Split, Absorb, Eat); whoever takes it gets one of his effects for a while, good or bad and never
    /// told which in advance: a strong clean high and no hangover, a burst of skill, a vision (true or false) they believe,
    /// no fear in a fight; or his deceit: a wonderful high then a hard crash, a sudden craving for more, or the drug's own
    /// addiction taking hold at once (through the shared harm budget, HarmKinds.GodCup). Each is shown as it happens; a few
    /// hours after, the colony's book keeps what they believe it was, perhaps wrongly (RevelCupMath.Read). The horned
    /// reveller's own cup is his too (OwnRevel.Share). One at a time; he leaves one nobody takes within a month.
    /// </summary>
    public static class RevelCup
    {
        public const string CleanHigh = "Rimfolk_RevelCleanHigh", Sharp = "Rimfolk_RevelSharp", Bold = "Rimfolk_RevelBold", Crash = "Rimfolk_RevelCrash",
            Craving = "Rimfolk_RevelCraving";

        private static int Now => Find.TickManager.TicksGame;

        // ---- The one he is in, kept in the book (RevelState.cup, cupK) and mirrored here for the hooks ----

        private static Thing marked;
        private static Game markedGame;
        /// <summary>Dev mode: what his next cup does, whatever the roll (None: as it falls).</summary>
        internal static CupOutcome Forced;

        /// <summary>The stack being eaten from just now (its own splitting inside the game's eating moves nothing).</summary>
        internal static Thing eating;

        private static RevelState State => OwnHappenings.Book?.revel;

        /// <summary>This stack holds his one (a single compare while none does).</summary>
        internal static bool Is(Thing t)
        {
            if (marked == null || t != marked || eating == t) return false;
            if (Current.Game != markedGame)
            {
                marked = null;
                return false;
            }
            return true;
        }

        internal static void Loaded(RevelState s)
        {
            marked = s.cupSince >= 0 ? s.cup : null;
            markedGame = Current.Game;
        }

        private static void Set(RevelState s, Thing t, int k)
        {
            s.cup = t;
            s.cupK = k;
            marked = t;
            markedGame = Current.Game;
        }

        private static void Clear(RevelState s)
        {
            if (s == null) return;
            s.cup = null;
            s.cupK = 0;
            s.cupSince = -1;
            s.cupNoticed = false;
            marked = null;
        }

        /// <summary>He is in the <paramref name="k"/>-th one of this stack now (<paramref name="came"/>: one he took in the stores, kept as come).</summary>
        public static bool Mark(OwnHappeningsBook b, Thing t, int k, bool came)
        {
            var s = b?.revel;
            if (s == null || t == null || t.Destroyed || !RevelGoods.His(t.def)) return false;
            Set(s, t, Mathf.Clamp(k, 1, Math.Max(1, t.stackCount)));
            s.cupSince = Now;
            s.cupNoticed = false;
            if (came) OwnHappenings.Came(b, OwnHappening.RevelCup, OwnHappenings.Now, null, t.def.defName);
            return true;
        }

        // ================= The game's own stacks =================

        /// <summary>A stack split (hauled, carried, sold): he goes with the part taken off if he was among it.</summary>
        internal static void Split(Thing from, Thing part, int count, int stackBefore)
        {
            var s = State;
            if (s == null || part == null || part == from) return;
            var r = RevelCupMath.Split(s.cupK, count, stackBefore);
            if (r.moves) Set(s, part, r.k);
            else s.cupK = r.k;
        }

        /// <summary>A stack took in part of another (hauled onto it): he comes across if he was among what moved.</summary>
        internal static void Absorbed(Thing into, Thing other, int moved, int intoBefore, bool otherHeld, bool intoHeld)
        {
            var s = State;
            if (s == null || moved <= 0) return;
            if (otherHeld)
            {
                var r = RevelCupMath.Absorb(s.cupK, moved, intoBefore);
                if (r.moves) Set(s, into, r.k);
                else s.cupK = r.k;
            }
            // Held by the stack that took the others in: his place in it doesn't change (the new ones go after).
        }

        /// <summary>Someone ate or drank from the stack: whether his one was among what they took.</summary>
        internal static void Ate(Thing t, Pawn ingester, int stackBefore)
        {
            var s = State;
            if (s == null) return;
            int taken = stackBefore - (t.Destroyed ? 0 : t.stackCount);
            var r = RevelCupMath.Eat(s.cupK, taken);
            if (!r.taken)
            {
                s.cupK = r.k;
                return;
            }
            var def = t.def;
            Clear(s);
            // Only one of the colony's own takes him; a guest, a beast or a prisoner takes only the drink.
            if (ingester == null || !OwnHappenings.Ours(ingester) || ingester.Dead) return;
            Taken(OwnHappenings.Book, s, ingester, def);
        }

        // ================= His hour =================

        internal static void Hourly(OwnHappeningsBook b, RevelState s, God g, Map map, int now, int hour)
        {
            if (s.cupSince >= 0) Waiting(b, s, map, now);
            else if (hour >= 6 && hour <= 22 && !MapFacts.ThreatOn(map)) Take(b, s, g, map, now);
            if (s.cupBy != null) Follow(s, map, now);
        }

        /// <summary>Now and then he takes one of the colony's stores' drink or drugs (more often at his feast or under his presence).</summary>
        private static void Take(OwnHappeningsBook b, RevelState s, God g, Map map, int now)
        {
            var held = RevelGoods.Held(map).ToList();
            bool strong = s.feastMap == map && s.feastDay == GenDate.DaysPassed || OwnRevel.Presence(map);
            var t = OwnHappeningsMath.CupTrigger(held.Count > 0, s.cupSince >= 0, strong);
            if (t == GodPaceMath.Trigger.None || !OwnHappenings.Calls(b, OwnHappening.RevelCup, t, g, map, now)) return;
            TakeNow(b, map);
        }

        /// <summary>He is in one of a stack in the stores now (a drink first, else any of his), any one of it alike.</summary>
        public static Thing TakeNow(OwnHappeningsBook b, Map map)
        {
            var held = RevelGoods.Held(map).ToList();
            if (held.Count == 0) return null;
            var stack = held.RandomElementByWeight(x => (RevelGoods.Drink(x.def) ? 2f : 1f) * (RevelGoods.Hard(x.def) ? 0.5f : 1f));
            return Mark(b, stack, RevelCupMath.Which(stack.stackCount, Rand.Value), true) ? stack : null;
        }

        /// <summary>Where his one is now: on a home map in the stores, or carried by someone there.</summary>
        private static Map MapOf(Thing t) => t?.MapHeld;

        /// <summary>While his one waits: someone passing may notice something small about it; he leaves it after a month, or once it is gone from the colony.</summary>
        private static void Waiting(OwnHappeningsBook b, RevelState s, Map map, int now)
        {
            var t = s.cup;
            var at = MapOf(t);
            if (t == null || t.Destroyed || at == null || !ColonyCouncil.Homes.IsHome(at) || now - s.cupSince > RevelCupMath.CupWaitDays * GenDate.TicksPerDay)
            {
                if (at == map || t == null || t.Destroyed || at == null) Clear(s);
                return;
            }
            if (at != map || s.cupNoticed || !t.Spawned) return;
            var p = map.mapPawns.FreeColonistsSpawned.Where(x => GodOmens.Awake(x) && x.Position.InHorDistOf(t.Position, RevelCupMath.NoticeRange) && GenSight.LineOfSight(x.Position, t.Position, map, true))
                .FirstOrDefault();
            if (p == null || !Rand.Chance(RevelCupMath.NoticeChance)) return;
            s.cupNoticed = true;
            OwnHappenings.SayFromWith(p, OwnRevel.Pool, null, new[] { t.def.label.Named("WHAT") }, "cupnotice");
        }

        // ================= Whoever takes it =================

        /// <summary>His one was taken by one of the colony: one of his effects, good or bad, shown as it happens.</summary>
        internal static void Taken(OwnHappeningsBook b, RevelState s, Pawn p, ThingDef def)
        {
            if (b == null || s == null || p?.health == null) return;
            var addiction = RevelGoods.Addiction(def);
            bool hard = RevelGoods.Hard(def);
            var o = Forced != CupOutcome.None ? Forced : RevelCupMath.Pick(Rand.Value, addiction != null, hard);
            Forced = CupOutcome.None;
            if (o == CupOutcome.Hooked)
            {
                bool may = addiction != null && !p.health.hediffSet.HasHediff(addiction) && Harm.Roll(p, HarmKinds.GodCup, 1f);
                o = RevelCupMath.HookedOr(may);
            }
            s.cupBy = p;
            s.cupDrug = def.label;
            s.cupOutcome = o;
            s.cupCrashAt = -1;
            s.cupBookAt = Now + Mathf.RoundToInt(RevelCupMath.BookHours * GenDate.TicksPerHour);
            var g = OwnRevel.God;
            switch (o)
            {
                case CupOutcome.CleanHigh:
                    Give(p, CleanHigh);
                    s.cleanUntil = Now + Mathf.RoundToInt(RevelCupMath.CleanHours * GenDate.TicksPerHour);
                    NoHangover(p);
                    Show(p, g, "Clean", "cupclean", MessageTypeDefOf.PositiveEvent);
                    break;
                case CupOutcome.Sharp:
                    if (!Inspire(p)) Give(p, Sharp);
                    Show(p, g, "Sharp", "cupsharp", MessageTypeDefOf.PositiveEvent);
                    break;
                case CupOutcome.Vision:
                    // Once the drinking is done (After): they stare and walk out to it; its words come when they reach the place.
                    Later(b, s, def);
                    break;
                case CupOutcome.Fearless:
                    Give(p, Bold);
                    Show(p, g, "Bold", "cupbold", MessageTypeDefOf.PositiveEvent);
                    break;
                case CupOutcome.Crash:
                    Give(p, Crash);
                    s.cupCrashAt = Now + Mathf.RoundToInt(RevelCupMath.CrashHours * GenDate.TicksPerHour);
                    s.cupBookAt = s.cupCrashAt + GenDate.TicksPerHour * 2;
                    Show(p, g, "High", "cuphigh", MessageTypeDefOf.PositiveEvent);
                    break;
                case CupOutcome.Craving:
                    Give(p, Craving);
                    Show(p, g, "Craving", "cupcraving", MessageTypeDefOf.NegativeEvent);
                    Later(b, s, def);
                    break;
                case CupOutcome.Hooked:
                    Harm.Ail(p, addiction, Mathf.Max(0.01f, addiction.initialSeverity), HarmKinds.GodCup);
                    Show(p, g, "Hooked", "cuphooked", MessageTypeDefOf.NegativeEvent, (RevelGoods.Chemical(def)?.label ?? def.label).Named("CHEMICAL"));
                    break;
            }
        }

        /// <summary>What the cup sets going waits until the drinking is done (a job is never started inside the game's own eating).</summary>
        private static void Later(OwnHappeningsBook b, RevelState s, ThingDef def)
        {
            s.cupAfter = Now + 90;
            s.cupAfterDef = def?.defName;
            b.Due(s.cupAfter);
        }

        /// <summary>The drinking done: a vision walked to (a clean high instead where there is nowhere to walk), or a craving's second one.</summary>
        internal static void After(OwnHappeningsBook b, RevelState s, int now)
        {
            if (now < s.cupAfter)
            {
                b.Due(s.cupAfter);
                return;
            }
            s.cupAfter = -1;
            var p = s.cupBy;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(s.cupAfterDef ?? "");
            if (p == null || p.Dead || !p.Spawned || def == null) return;
            if (s.cupOutcome == CupOutcome.Vision)
            {
                if (OwnRevel.See(b, p, true)) return;
                s.cupOutcome = CupOutcome.CleanHigh;
                Give(p, CleanHigh);
                s.cleanUntil = Now + Mathf.RoundToInt(RevelCupMath.CleanHours * GenDate.TicksPerHour);
                NoHangover(p);
                Show(p, OwnRevel.God, "Clean", "cupclean", MessageTypeDefOf.PositiveEvent);
            }
            else if (s.cupOutcome == CupOutcome.Craving) Again(p, def);
        }

        private static void Give(Pawn p, string hediff)
        {
            var d = DefDatabase<HediffDef>.GetNamedSilentFail(hediff);
            if (d == null || p?.health == null) return;
            var had = p.health.hediffSet.GetFirstHediffOfDef(d);
            if (had != null) p.health.RemoveHediff(had);
            p.health.AddHediff(d);
        }

        /// <summary>A burst of skill: an inspiration of the game's own (and the player's mods'), the first they can have.</summary>
        private static bool Inspire(Pawn p)
        {
            if (p.mindState?.inspirationHandler == null || p.Inspired) return false;
            foreach (var n in new[] { "Inspired_Creativity", "Frenzy_Work", "VSIE_Party_Frenzy", "Inspired_Recruitment", "Inspired_Trade", "Frenzy_Go" }.InRandomOrder())
            {
                var d = DefDatabase<InspirationDef>.GetNamedSilentFail(n);
                if (d?.Worker == null || !d.Worker.InspirationCanOccur(p)) continue;
                if (p.mindState.inspirationHandler.TryStartInspiration(d, "CC_GodRevel_Cup_InspireReason".Translate(p.Named("PAWN")).Resolve())) return true;
            }
            return false;
        }

        /// <summary>A craving's second one: another of the same from the stores, if it isn't hard and their policy allows it for fun (the game's own dose).</summary>
        private static void Again(Pawn p, ThingDef def)
        {
            if (!RevelCupMath.SecondDose(RevelGoods.Hard(def), RevelGoods.AllowedForJoy(p, def))) return;
            var t = RevelGoods.Held(p.Map).Where(x => x.def == def && p.CanReserveAndReach(x, PathEndMode.ClosestTouch, Danger.Some)).OrderBy(x => x.Position.DistanceToSquared(p.Position)).FirstOrDefault();
            if (t == null) return;
            try
            {
                var job = JobMaker.MakeJob(JobDefOf.Ingest, t);
                job.count = 1;
                job.ingestTotalCount = true;
                OwnHappenings.Job(p, job);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a craving's second one", e);
            }
        }

        private static void NoHangover(Pawn p)
        {
            var hangover = DefDatabase<HediffDef>.GetNamedSilentFail("Hangover");
            var h = hangover == null ? null : p?.health?.hediffSet?.GetFirstHediffOfDef(hangover);
            if (h != null) p.health.RemoveHediff(h);
        }

        /// <summary>What anyone can see of it: a message of what it did to them, their own word, a little of his look, the camera on it. Never whose it was.</summary>
        private static void Show(Pawn p, God g, string what, string tag, MessageTypeDef type, params NamedArgument[] more)
        {
            var map = p.MapHeld;
            if (map == null) return;
            var said = new System.Collections.Generic.List<NamedArgument> { (State?.cupDrug ?? "").Named("DRUG") };
            if (more != null) said.AddRange(more);
            var args = new System.Collections.Generic.List<NamedArgument> { p.Named("PAWN") };
            args.AddRange(said);
            if (p.Spawned)
            {
                OwnHappenings.Glow(map, p.Position, OwnRevel.Wine, 4, 0.4f, 0.3f);
                OwnHappenings.SayFromWith(p, OwnRevel.Pool, g, said.ToArray(), tag);
            }
            GodPresence.Look(new LookTargets(p));
            Tell.Message(NewsKind.Land, Importance.Notable, map, ("CC_GodRevel_Cup_" + what).Translate(args.ToArray()).Resolve(), new LookTargets(p), type);
        }

        /// <summary>After it was taken: the clean high keeps the hangover off, the crash comes, and the book is written.</summary>
        private static void Follow(RevelState s, Map map, int now)
        {
            var p = s.cupBy;
            if (p == null || p.Dead || p.Destroyed)
            {
                s.cupBy = null;
                s.cupBookAt = s.cupCrashAt = s.cleanUntil = -1;
                return;
            }
            if (p.MapHeld != map) return;
            if (s.cleanUntil >= 0)
            {
                if (now > s.cleanUntil) s.cleanUntil = -1;
                else NoHangover(p);
            }
            if (s.cupCrashAt >= 0 && now >= s.cupCrashAt)
            {
                s.cupCrashAt = -1;
                Show(p, OwnRevel.God, "CrashCame", "cupcrash", MessageTypeDefOf.NegativeEvent);
            }
            if (s.cupBookAt >= 0 && now >= s.cupBookAt && s.cupCrashAt < 0)
            {
                Book(s, p, map);
                s.cupBookAt = -1;
                if (s.cleanUntil < 0) s.cupBy = null;
            }
            if (s.cupBookAt < 0 && s.cupCrashAt < 0 && s.cleanUntil < 0) s.cupBy = null;
        }

        /// <summary>The colony's book keeps what they believe it was, by the god they take it for: never the truth marked.</summary>
        private static void Book(RevelState s, Pawn p, Map map)
        {
            var g = OwnRevel.God;
            if (g == null || s.cupOutcome == CupOutcome.None || s.cupOutcome == CupOutcome.Vision) return;
            bool scoffer = Worship.Scoffer(p);
            var taken = OwnRevel.TakenFor(g, map);
            bool dreads = taken != null && PawnGods.Dreads(p, taken);
            var r = RevelCupMath.Read(s.cupOutcome, scoffer, dreads, Rand.Value);
            if (r == CupReading.None) return;
            string words = ("CC_GodRevel_Believes_Cup_" + r).Translate((s.cupDrug ?? "").Named("DRUG"), GodLorePage.Date(Now).Named("DATE")).Resolve();
            if (RevelCupMath.Doubts(r)) GodLore.Believes(p, taken, words, true);
            else GodLore.Believes(p, taken, words);
        }
    }

    // ================= The hooks: the game's own stacks, followed =================

    /// <summary>A stack split (hauled, carried off, eaten from, sold): the god's one goes with the part it was among (RevelCup.Split). Off with the deeper hooks.</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.SplitOff))]
    public static class Patch_RevelCupSplit
    {
        public static void Prefix(Thing __instance, out int __state) => __state = RimfolkPatches.On && RevelCup.Is(__instance) ? __instance.stackCount : -1;

        public static void Postfix(Thing __instance, int count, Thing __result, int __state)
        {
            if (__state < 0) return;
            try
            {
                RevelCup.Split(__instance, __result, count, __state);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the god's cup, a stack split", e);
            }
        }
    }

    /// <summary>What a stack took in from another, and whose, to follow the god's one across (RevelCup.Absorbed).</summary>
    public struct RevelAbsorb
    {
        public int into, other;
        public bool otherHeld, intoHeld;
    }

    /// <summary>A stack takes in another (hauled onto it in the stores): the god's one comes across if it was among what moved.</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TryAbsorbStack))]
    public static class Patch_RevelCupAbsorb
    {
        public static void Prefix(Thing __instance, Thing other, out RevelAbsorb __state)
        {
            __state = new RevelAbsorb { into = -1 };
            if (!RimfolkPatches.On || other == null) return;
            bool otherHeld = RevelCup.Is(other), intoHeld = RevelCup.Is(__instance);
            if (!otherHeld && !intoHeld) return;
            __state = new RevelAbsorb { into = __instance.stackCount, other = other.stackCount, otherHeld = otherHeld, intoHeld = intoHeld };
        }

        public static void Postfix(Thing __instance, Thing other, RevelAbsorb __state)
        {
            if (__state.into < 0 || other == null) return;
            try
            {
                int moved = __state.other - (other.Destroyed ? 0 : other.stackCount);
                RevelCup.Absorbed(__instance, other, moved, __state.into, __state.otherHeld, __state.intoHeld);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the god's cup, a stack merged", e);
            }
        }
    }

    /// <summary>Someone eats or drinks from a stack: the god's one taken if it was among what they took (RevelCup.Ate).</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    public static class Patch_RevelCupIngested
    {
        public static void Prefix(Thing __instance, out int __state)
        {
            __state = -1;
            if (!RimfolkPatches.On || !RevelCup.Is(__instance)) return;
            __state = __instance.stackCount;
            RevelCup.eating = __instance;
        }

        public static void Postfix(Thing __instance, Pawn ingester, int __state)
        {
            if (__state < 0) return;
            RevelCup.eating = null;
            try
            {
                RevelCup.Ate(__instance, ingester, __state);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the god's cup taken", e);
            }
        }

        public static void Finalizer() => RevelCup.eating = null;
    }
}
