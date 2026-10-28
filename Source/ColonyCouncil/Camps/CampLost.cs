using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A child of the camp lost out on the land with one of the camp's beasts (Council_DlmL_CampLostChild): when the
    /// council is asked, they really are out there, the child lying down somewhere a long walk from the camp and the
    /// beast beside them, and they come home the way the answer has it: found by the colony's searcher at the end of the
    /// search and walked back (the search's last place is where they are), found by the camp's own people before
    /// midnight or at dawn, or walking in toward the colony's bell before first light and on to the camp. Only when the
    /// weather is mild enough that a night out does them no harm; after a day and a bit they come home whatever happens.
    /// </summary>
    public partial class Camp
    {
        public Pawn lostChild, lostBeast;
        public IntVec3 lostCell = IntVec3.Invalid;
        /// <summary>When they come home at the latest, and when they set off home by themselves (-1: not set).</summary>
        public int lostUntil = -1, lostBackAt = -1;
        /// <summary>Walking home now, and by way of the colony's gate first (the bell).</summary>
        public bool lostHome;
        public IntVec3 lostVia = IntVec3.Invalid;

        public bool LostActive => lostChild != null && lostUntil > 0;

        public bool IsLost(Pawn p) => LostActive && p != null && (p == lostChild || p == lostBeast);

        /// <summary>The child (and the beast) are out there now: a place a long walk from the camp, away from the colony. False when there's none.</summary>
        public bool Lose(Pawn child, Pawn beast)
        {
            if (child == null || map == null || LostActive) return false;
            var away = center - colonyCenter;
            float len = Mathf.Max(1f, Mathf.Sqrt(away.x * away.x + away.z * away.z));
            for (int tries = 0; tries < 6; tries++)
            {
                float dist = Rand.Range(28f, 42f);
                float turn = Rand.Range(-60f, 60f) * Mathf.Deg2Rad;
                float dx = away.x / len, dz = away.z / len;
                var aim = new IntVec3(center.x + Mathf.RoundToInt((dx * Mathf.Cos(turn) - dz * Mathf.Sin(turn)) * dist), 0,
                    center.z + Mathf.RoundToInt((dx * Mathf.Sin(turn) + dz * Mathf.Cos(turn)) * dist)).ClampInsideMap(map);
                if (!CellFinder.TryFindRandomCellNear(aim, map, 8, c => c.Standable(map) && !c.Fogged(map) && !c.Roofed(map) && c.GetEdifice(map) == null
                                                                       && !(c.GetTerrain(map)?.IsWater ?? false) && !map.areaManager.Home[c]
                                                                       && child.CanReach(c, PathEndMode.OnCell, Danger.Some), out var spot))
                    continue;
                lostChild = child;
                lostBeast = beast;
                lostCell = spot;
                lostUntil = Now + GenDate.TicksPerDay * 6 / 5;
                lostBackAt = -1;
                lostHome = false;
                lostVia = IntVec3.Invalid;
                child.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                beast?.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                return true;
            }
            return false;
        }

        /// <summary>Found by someone of the colony (<paramref name="by"/>): they walk home together, the beast behind.</summary>
        public void FoundLost(Pawn by)
        {
            if (!LostActive || lostHome) return;
            lostHome = true;
            lostVia = IntVec3.Invalid;
            if (by != null && by.Spawned && !by.Downed && by.Map == map)
            {
                // together: the one who found them walks them back to the camp's fire.
                if (CellFinder.TryFindRandomCellNear(FireCell, map, 3, c => c.Standable(map) && by.CanReach(c, PathEndMode.OnCell, Danger.Some), out var back))
                    by.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, back), JobTag.Misc);
                Note("CC_CampLost_Found".Translate(by.Named("PAWN"), (lostBeast?.KindLabel ?? "").Named("BEAST")).Resolve(), MessageTypeDefOf.PositiveEvent, Importance.Notable, lostChild);
            }
            lostChild?.jobs?.EndCurrentJob(JobCondition.InterruptForced);
        }

        /// <summary>They set off home by themselves at this tick (the camp's own search, first light), by the colony's gate first with <paramref name="viaGate"/>.</summary>
        public void LostComeHomeAt(int tick, bool viaGate)
        {
            if (!LostActive || lostHome) return;
            lostBackAt = tick;
            if (viaGate)
            {
                var gate = PlaySpots.GateCell(map, lostChild);
                if (gate.IsValid) lostVia = gate;
            }
        }

        private void EndLost(bool told)
        {
            if (told && lostChild != null && lostChild.Spawned)
                Note("CC_CampLost_Home".Translate().Resolve(), MessageTypeDefOf.NeutralEvent, Importance.Notable, lostChild);
            lostChild = lostBeast = null;
            lostCell = lostVia = IntVec3.Invalid;
            lostUntil = lostBackAt = -1;
            lostHome = false;
        }

        /// <summary>The lost child's (or the beast's) next job: out there, lying down; once found, home to the camp's fire.</summary>
        public Job LostJob(Pawn p)
        {
            if (!IsLost(p) || map == null) return null;
            int now = Now;
            if (!Settled || lostChild.Dead || !lostChild.Spawned || lostChild.Map != map)
            {
                EndLost(false);
                return null;
            }
            if (!lostHome && (now >= lostUntil || lostBackAt > 0 && now >= lostBackAt)) lostHome = true;
            var child = lostChild;
            if (p == lostBeast)
            {
                var near = lostHome ? child.Position : lostCell;
                if (p.Position.InHorDistOf(near, 3f)) return lostHome ? null : JobMaker.MakeJob(JobDefOf.LayDown, p.Position);
                return p.CanReach(near, PathEndMode.Touch, Danger.Some) ? JobMaker.MakeJob(JobDefOf.Goto, near) : null;
            }
            if (lostHome)
            {
                // Toward the bell first, then on to the camp.
                if (lostVia.IsValid)
                {
                    if (p.Position.InHorDistOf(lostVia, 3f)) lostVia = IntVec3.Invalid;
                    else if (p.CanReach(lostVia, PathEndMode.OnCell, Danger.Some)) return JobMaker.MakeJob(JobDefOf.Goto, lostVia);
                    else lostVia = IntVec3.Invalid;
                }
                if (p.Position.InHorDistOf(FireCell, 6f) || !p.CanReach(FireCell, PathEndMode.Touch, Danger.Some))
                {
                    EndLost(true);
                    return null;
                }
                return JobMaker.MakeJob(JobDefOf.Goto, FireCell);
            }
            if (!p.Position.InHorDistOf(lostCell, 2f))
                return p.CanReach(lostCell, PathEndMode.OnCell, Danger.Some) ? JobMaker.MakeJob(JobDefOf.Goto, lostCell) : null;
            var lie = JobMaker.MakeJob(JobDefOf.LayDown, lostCell);
            lie.expiryInterval = 2000;
            return lie;
        }

        private void ExposeLost()
        {
            Scribe_References.Look(ref lostChild, "lostChild");
            Scribe_References.Look(ref lostBeast, "lostBeast");
            Scribe_Values.Look(ref lostCell, "lostCell", IntVec3.Invalid);
            Scribe_Values.Look(ref lostUntil, "lostUntil", -1);
            Scribe_Values.Look(ref lostBackAt, "lostBackAt", -1);
            Scribe_Values.Look(ref lostHome, "lostHome");
            Scribe_Values.Look(ref lostVia, "lostVia", IntVec3.Invalid);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && lostChild == null) lostUntil = -1;
        }
    }

    /// <summary>
    /// A child of the camp missing (Council_DlmL_CampLostChild): only with a child of theirs up and about, one of their
    /// beasts, nothing else of theirs under way, and weather a child takes no harm from a night out in. When it's asked,
    /// the child and the beast are out there (Camp.Lose).
    /// </summary>
    public class DilemmaWorker_CampLostChild : DilemmaWorker_Camp
    {
        public override bool CanHappenNow(Map map)
        {
            var c = CampOn(map);
            return c != null && !c.ErrandSet && !c.LostActive && c.herd.Count > 0 && base.CanHappenNow(map);
        }

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            var c = CampOn(ctx.map);
            if (c == null) return false;
            var child = c.Here.Where(p => p.DevelopmentalStage.Child() && !p.Downed && !p.InMentalState).RandomElementWithFallback();
            var beast = c.herd.Where(a => a != null && a.Spawned && !a.Dead && !a.Downed).RandomElementWithFallback();
            if (child == null || beast == null) return false;
            float t = ctx.map.mapTemperature.OutdoorTemp;
            if (!child.SafeTemperatureRange().Includes(t) || ctx.map.gameConditionManager.ConditionIsActive(GameConditionDefOf.ToxicFallout)) return false;
            if (!c.Lose(child, beast)) return false;
            ctx.pet = beast;
            return true;
        }
    }

    /// <summary>
    /// How the lost child comes home (<see cref="how"/>): "search", found at the last place of the search the answer set
    /// going (by whoever searches; never set going: the camp finds them soon); "soon", found by the camp's own people in a
    /// few hours; "midnight", before midnight; "dawn", at first light; "bell", walking in toward the colony's gate just
    /// before first light, then on to the camp.
    /// </summary>
    public class DilemmaEffect_FindLost : DilemmaEffect
    {
        public string how = "dawn";

        public const string Search = "findlost";

        public override void Apply(DilemmaContext ctx)
        {
            var c = DilemmaWorker_Camp.CampOn(ctx?.map) ?? CampTracker.Instance?.camp;
            if (c == null || !c.LostActive) return;
            var map = c.map;
            int now = Find.TickManager.TicksGame;
            int hour = GenLocalDate.HourOfDay(map);
            int ToHour(int h) => now + ((h - hour + 24) % 24) * GenDate.TicksPerHour + Rand.Range(0, GenDate.TicksPerHour);
            switch (how)
            {
                case "search":
                {
                    var search = PlayOuts.On ? PlayOuts.Collected().FirstOrDefault(p => p.kind == PlayKind.Search && p.state == PlayState.Pending && p.afterward.NullOrEmpty()) : null;
                    if (search != null)
                    {
                        search.afterward = Search;
                        search.cell = c.lostCell;
                        return;
                    }
                    c.LostComeHomeAt(now + GenDate.TicksPerHour * 2, false);
                    return;
                }
                case "soon":
                    c.LostComeHomeAt(now + Rand.Range(2, 4) * GenDate.TicksPerHour, false);
                    return;
                case "midnight":
                    c.LostComeHomeAt(hour >= 21 || hour < 2 ? now + GenDate.TicksPerHour : ToHour(22), false);
                    return;
                case "bell":
                    c.LostComeHomeAt(ToHour(4), true);
                    return;
                default:
                    c.LostComeHomeAt(ToHour(5), false);
                    return;
            }
        }

        /// <summary>The searcher has got to where the child lies: found.</summary>
        public static void Reached(PlayOut p, Pawn by)
        {
            if (p == null || p.afterward != Search) return;
            var c = CampTracker.Instance?.camp;
            if (c == null || !c.LostActive || c.map != p.map || !by.Position.InHorDistOf(c.lostCell, 5f)) return;
            p.afterward = null;
            c.FoundLost(by);
        }

        /// <summary>The search ended without them: the camp's own people find them now.</summary>
        public static void Fallback(PlayOut p)
        {
            var c = CampTracker.Instance?.camp;
            if (c == null || !c.LostActive) return;
            c.LostComeHomeAt(Find.TickManager.TicksGame, false);
        }
    }
}
