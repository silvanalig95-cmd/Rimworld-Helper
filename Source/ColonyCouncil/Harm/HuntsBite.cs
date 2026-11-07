using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Hunts that can bite back (the harm review's 2.1.1): an answer's hunt, a hunting party and the calendar's hunting day
    /// may mark one head of game that can turn on its hunters (a lone boar, an elk, a muffalo; never a predator, an insect,
    /// a dryad, anything bigger than a muffalo or an elk, nobody's animal, one bonded or venerated), at the game's own
    /// revenge chance for its kind. The chance is the game's (PawnUtility.GetManhunterOnDamageChance, with its distance,
    /// stealth and the hunter's role) times the harm scale, and nothing for a hunter the harm cap keeps from a hurt; a
    /// head that turns counts as that hunter's hurt for the cap. Alone means no packmate within the game's own reach, so
    /// the turn is that one animal and never a herd. The answer's sheet, the calendar's letter and the marking's message
    /// name it ("the boar may turn on its hunters"); the hunter says so when it turns, and the game's own revenge letter
    /// and fight follow. Needs answers on the map and the deeper hooks (without them only safe game, as before). The
    /// hunting season decree keeps to safe game.
    /// </summary>
    public static class HuntsBite
    {
        /// <summary>
        /// Hurts from everyday life on, answers on the map, and the deeper hooks that scale and cap the game's own chance;
        /// never inside a decree's morning work (the hunting season keeps to safe game).
        /// </summary>
        public static bool On => Harm.On && PlayOuts.On && RimfolkPatches.On && safeOnly == 0;

        private static int safeOnly;

        private sealed class SafeScope : IDisposable
        {
            public void Dispose() => safeOnly = Math.Max(0, safeOnly - 1);
        }

        /// <summary>While it lasts, hunts take safe game only (a decree's morning work).</summary>
        public static IDisposable SafeOnly()
        {
            safeOnly++;
            return new SafeScope();
        }

        /// <summary>
        /// Game that can turn and that a hunt may take now: wild, up and about, seen, a revenge chance above the safe game's
        /// (by its kind and the storyteller), never a predator, an insect, a dryad or anything bigger than a muffalo or an
        /// elk, nobody's bond or faith's beast, and alone.
        /// </summary>
        public static bool MayTurn(Pawn a, Map map)
        {
            if (a == null || a.Dead || !a.Spawned || a.Map != map || a.Downed || a.RaceProps == null || !a.RaceProps.Animal || a.Faction != null) return false;
            if (a.InMentalState || a.Position.Fogged(map) || PlayHunt.Safe(a, map) || PlayHunt.Kept(a)) return false;
            float revenge;
            try
            {
                revenge = Math.Max(a.RaceProps.manhunterOnDamageChance, PawnUtility.GetManhunterOnDamageChance(a.def));
            }
            catch
            {
                return false;
            }
            return HarmTwoMath.MayTurn(a.RaceProps.predator, a.RaceProps.Insect, a.RaceProps.Dryad, a.RaceProps.baseBodySize, revenge, Lone(a, map));
        }

        /// <summary>No packmate of its kind (or of the kinds it runs with) within the game's own reach: a turn would be this one alone.</summary>
        public static bool Lone(Pawn a, Map map)
        {
            var with = a.RaceProps.crossAggroWith;
            var district = a.GetDistrict();
            foreach (var o in map.mapPawns.AllPawnsSpawned)
            {
                if (o == a || o.Faction != a.Faction || o.Dead) continue;
                if (o.def != a.def && (with == null || !with.Contains(o.def))) continue;
                if (o.Position.InHorDistOf(a.Position, HarmTwoMath.PackRadius) && o.GetDistrict() == district) return false;
            }
            return true;
        }

        /// <summary>
        /// The game for a hunt from candidates already in order: safe ones, and with this on, at most one that can turn.
        /// <paramref name="turning"/>: the one that can turn, if one was taken.
        /// </summary>
        public static List<Pawn> Pick(IEnumerable<Pawn> ordered, Map map, int n, bool mayTurn, out Pawn turning)
        {
            turning = null;
            var result = new List<Pawn>();
            foreach (var a in ordered)
            {
                if (result.Count >= n) break;
                if (PlayHunt.Safe(a, map))
                {
                    result.Add(a);
                    continue;
                }
                if (!mayTurn || turning != null || !MayTurn(a, map)) continue;
                turning = a;
                result.Add(a);
            }
            return result;
        }

        // ---------------- Named before the choice ----------------

        /// <summary>The answer's sheet: a health line naming the one that may turn, when the hunt would take one now.</summary>
        public static void Describe(ImpactSheet sheet, Pawn turning)
        {
            if (sheet == null || turning == null) return;
            sheet.Add(ImpactKind.Health, -1, "CC_Impact_HuntTurn".Translate(turning.KindLabel.Named("KIND")).Resolve(), "CC_Impact_HuntTurnBrief".Translate(turning.KindLabel.Named("KIND")).Resolve(),
                "huntturn");
        }

        /// <summary>The words that follow a hunt's marking: the one that may turn, named; empty for none.</summary>
        public static string MarkedNote(Pawn turning) => turning == null ? "" : " " + "CC_HarmTwo_HuntTurnNote".Translate(turning.KindLabel.Named("KIND")).Resolve();

        /// <summary>The calendar's hunting day: its letter's line when the day's three head would take one that can turn (null for none).</summary>
        public static string CalendarLine(Map map)
        {
            if (!On || map == null) return null;
            var turning = DilemmaEffect_Hunt.WouldTurn(map, null, null, 3);
            return turning == null ? null : "CC_HarmTwo_HuntTurnCalendar".Translate(turning.KindLabel.Named("KIND")).Resolve();
        }

        // ---------------- Marked, and the game's own chance ----------------

        /// <summary>A head that can turn is marked for a hunt: the game's own chance for it runs through the harm scale and cap while it's remembered.</summary>
        public static void Marked(Pawn a)
        {
            if (a == null) return;
            var book = HarmBook.Instance;
            book?.Turning(a, Find.TickManager.TicksGame);
            // Its turn within the shared budget is one roll a hunt (HarmBudgetMath): when it doesn't come up, this hunt's head
            // stays calm under its hunters, as it does for a hunter the cap holds back.
            if (!Rand.Chance(Harm.PassOf(HarmKinds.HuntTurn))) book?.Calm(a);
        }

        /// <summary>One of the hunts' game that can turn (marked by Rimfolk, still remembered).</summary>
        public static bool IsTurning(Pawn a) => a != null && (HarmBook.Instance?.IsTurning(a) ?? false);

        private static readonly Color SayColor = new Color(1f, 0.7f, 0.6f);

        /// <summary>It turned on its hunter: counted as the hunter's hurt for the cap, said by them, and told.</summary>
        public static void Turned(Pawn animal, Pawn hunter)
        {
            try
            {
                HarmBook.Instance?.NotTurning(animal);
                if (hunter == null || !KinMemory.Ours(hunter)) return;
                Harm.Note(hunter, HarmKinds.HuntTurn);
                // apart: a word over the hunter's own head, to nobody, as the game turns on them.
                string line = Flavor.FromPool("Rimfolk_Pool_GameTurns", hunter);
                if (!line.NullOrEmpty()) Speech.Say(hunter, line.Formatted(hunter.Named("PAWN")).Resolve(), null, SayColor);
                string text = TextVariants.Vary("CC_HarmTwo_HuntTurned").Translate(hunter.Named("PAWN"), animal.KindLabel.Named("KIND")).Resolve();
                Tell.Message(NewsKind.Land, Importance.Important, hunter.MapHeld, text, new LookTargets(new[] { hunter, animal }), MessageTypeDefOf.ThreatSmall);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: a hunt's game turned", e);
            }
        }
    }

    /// <summary>
    /// The game's own revenge chance for one of the hunts' game that can turn (Harm/HuntsBite.cs): times the harm scale,
    /// and nothing for a hunter the harm cap keeps from a hurt (their own shot at it not counted as already fighting). A
    /// colonist the player drafted to shoot it has the game's own chance. Every other animal is the game's alone.
    /// </summary>
    [HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.GetManhunterOnDamageChance), typeof(Pawn), typeof(Thing), typeof(float))]
    public static class Patch_HuntTurnChance
    {
        public static void Postfix(Pawn pawn, Thing instigator, ref float __result)
        {
            if (!RimfolkPatches.On || __result <= 0f || pawn == null) return;
            try
            {
                var book = HarmBook.Instance;
                if (book == null || !book.AnyTurning || !book.IsTurning(pawn)) return;
                var hunter = instigator as Pawn;
                if (hunter != null && hunter.Drafted) return;
                bool may = (hunter == null || !KinMemory.Ours(hunter) || Harm.MayAtOwnAttack(hunter, HurtClass.Injury)) && !book.IsCalm(pawn);
                __result = HarmTwoMath.TurnChance(__result, Harm.Scale, may);
            }
            catch
            {
                // The game's own chance, as it was.
            }
        }
    }

    /// <summary>One of the hunts' game that can turn was hit and turned (the game's own revenge): its hunter says so and it's told (Harm/HuntsBite.cs).</summary>
    [HarmonyPatch(typeof(Pawn_MindState), nameof(Pawn_MindState.Notify_DamageTaken))]
    public static class Patch_HuntTurned
    {
        public static void Prefix(Pawn_MindState __instance, out bool __state)
        {
            __state = false;
            if (!RimfolkPatches.On) return;
            try
            {
                var book = HarmBook.Instance;
                var p = __instance?.pawn;
                __state = book != null && book.AnyTurning && p != null && !p.InMentalState && book.IsTurning(p);
            }
            catch
            {
                __state = false;
            }
        }

        public static void Postfix(Pawn_MindState __instance, DamageInfo dinfo, bool __state)
        {
            if (!__state) return;
            try
            {
                var p = __instance.pawn;
                var ms = p?.MentalStateDef;
                if (ms != MentalStateDefOf.Manhunter && ms != MentalStateDefOf.ManhunterPermanent) return;
                HuntsBite.Turned(p, dinfo.Instigator as Pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: a turned hunt", e);
            }
        }
    }
}
