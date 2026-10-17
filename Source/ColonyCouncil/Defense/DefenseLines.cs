using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The defense lines of one map (<see cref="DefenseLine"/>): made from a gap the survey found, from a spot the player
    /// picks, or by placing a gate on a gap; plans placed near a line's gate join it. While raiders are on the map it
    /// watches, every 250 ticks, how they meet each line (<see cref="Watch"/>). A save from before lines loads with none.
    /// </summary>
    public partial class DefenseLines : MapComponent
    {
        public List<DefenseLine> lines = new List<DefenseLine>();
        private int nextId = 1;

        public DefenseLines(Map map) : base(map)
        {
        }

        public static DefenseLines Of(Map map) => map?.GetComponent<DefenseLines>();

        public DefenseLine Get(int id) => id < 0 ? null : lines.FirstOrDefault(l => l.id == id);

        // ---------------- Which line is shown on the map ----------------

        private static readonly Dictionary<Map, int> selected = new Dictionary<Map, int>();

        public static void Select(Map map, DefenseLine line)
        {
            if (map == null) return;
            if (line == null) selected.Remove(map);
            else selected[map] = line.id;
        }

        public static DefenseLine Selected(Map map)
        {
            if (map == null || !selected.TryGetValue(map, out int id)) return null;
            return Of(map)?.Get(id);
        }

        // ---------------- Making and removing ----------------

        /// <summary>A line on one of the survey's gaps: its gate (the door already there, else the middle), the side raiders come from, its cells.</summary>
        public DefenseLine FromChoke(DefenseSurvey s, DefenseSurvey.ChokeInfo c)
        {
            if (s?.grid == null || c == null) return null;
            var g = s.grid;
            var cells = c.c.span.Select(i => new IntVec3(i % g.w, 0, i / g.w)).ToList();
            var gate = c.cell;
            var door = cells.Where(x => x.InBounds(map) && x.GetEdifice(map) is Building_Door).OrderBy(x => x.DistanceTo(c.cell)).FirstOrDefault();
            if (door.IsValid && door != default && door.InBounds(map) && door.GetEdifice(map) is Building_Door) gate = door;
            string place = c.c.bridge ? "bridge" : c.gated ? "gate" : c.c.endA == DefenseMath.Rock && c.c.endB == DefenseMath.Rock ? "pass" : "gap";
            return Add(gate, c.c.facing, cells, c.c.width, place);
        }

        /// <summary>
        /// A line at a cell the player picked: the facing of the gap it's in (or next to), else away from the colony's
        /// middle; the cells across the way there.
        /// </summary>
        public DefenseLine AtCell(IntVec3 cell)
        {
            var s = DefenseSurvey.For(map);
            if (s?.grid == null || !cell.InBounds(map)) return null;
            var choke = s.chokes.Where(c => c.cell.DistanceTo(cell) <= 10f).OrderBy(c => c.cell.DistanceTo(cell)).FirstOrDefault();
            int facing;
            if (choke != null) facing = choke.c.facing;
            else
            {
                var from = s.bedCenter.IsValid ? s.bedCenter : s.center;
                facing = DefenseLineMath.FacingOf(cell.x - from.x, cell.z - from.z);
            }
            var span = DefenseLineMath.SpanAcross(s.grid, cell.x, cell.z, facing, DefenseMath.MaxSpanSide);
            var cells = span.Select(i => new IntVec3(i % s.grid.w, 0, i / s.grid.w)).ToList();
            string place = choke != null && choke.cell.DistanceTo(cell) <= 3f ? (choke.c.bridge ? "bridge" : choke.gated ? "gate" : "pass") : cells.Count > DefenseMath.MaxChokeWidth ? "open" : "gap";
            if (choke != null && place == "pass" && !(choke.c.endA == DefenseMath.Rock && choke.c.endB == DefenseMath.Rock)) place = "gap";
            return Add(cell, facing, cells, cells.Count, place);
        }

        private DefenseLine Add(IntVec3 gate, int facing, List<IntVec3> span, int width, string place)
        {
            var line = new DefenseLine
            {
                id = nextId++,
                gate = gate,
                facing = DefenseLineMath.Norm(facing),
                span = span ?? new List<IntVec3>(),
                width = Mathf.Max(1, width),
                place = place ?? "gap",
                created = Find.TickManager.TicksGame,
                aim = Avail.IsPreIndustrial ? "archers" : "gate"
            };
            lines.Add(line);
            Select(map, line);
            return line;
        }

        public void Remove(DefenseLine line)
        {
            if (line == null) return;
            lines.Remove(line);
            DefenseLineReview.Forget(map, line);
            if (Selected(map) == line) Select(map, null);
        }

        /// <summary>The line whose gate is within <paramref name="radius"/> cells of a place.</summary>
        public DefenseLine Near(IntVec3 c, float radius = 4f) =>
            lines.Where(l => l.gate.DistanceTo(c) <= radius).OrderBy(l => l.gate.DistanceTo(c)).FirstOrDefault();

        /// <summary>The line made on one of the survey's gaps, if there is one.</summary>
        public DefenseLine ForChoke(DefenseSurvey s, DefenseSurvey.ChokeInfo c)
        {
            if (s?.grid == null || c == null) return null;
            foreach (var l in lines)
            {
                if (l.gate.DistanceTo(c.cell) <= 4f) return l;
                foreach (int i in c.c.span)
                    if (l.gate.DistanceTo(new IntVec3(i % s.grid.w, 0, i / s.grid.w)) <= 2f) return l;
            }
            return null;
        }

        // ---------------- Plans placed ----------------

        private static bool LineKind(DefenseTemplateDef t) => t.line >= 0 && (t.kind == "gate" || t.kind == "wall" || t.kind == "palisade" || t.kind == "killbox");

        /// <summary>
        /// A defense plan was placed: it joins the line it was planned for, or one whose gate is close; a gate or wall
        /// put down on a gap with no line yet offers to start one.
        /// </summary>
        public void OnPlaced(DefensePlan plan, int lineId)
        {
            if (plan?.template == null) return;
            var t = plan.template;
            var gatePiece = plan.pieces.FirstOrDefault(p => p.group == "gate" && p.state != PieceState.Blocked);
            var at = gatePiece?.cell ?? Center(plan);
            var line = Get(lineId) ?? NearPlan(plan, at);
            var saved = new DefenseLinePlan
            {
                template = t.defName, anchor = plan.anchor, rotation = plan.rotation, mirror = plan.mirror,
                closeGap = plan.extension.Count > 0 || t.line >= 0, tick = Find.TickManager.TicksGame
            };
            if (line != null)
            {
                line.AddPlan(saved);
                // A gate placed on the line is where raiders now cross.
                if (gatePiece != null && LineKind(t) && gatePiece.cell.DistanceTo(line.gate) <= 4f && t.kind != "killbox") line.gate = gatePiece.cell;
                Messages.Message("CC_DefLine_PlanJoined".Translate(t.LabelNow, DefenseLineReview.NameOf(line, map)).Resolve(), MessageTypeDefOf.SilentInput, false);
                return;
            }
            if (!LineKind(t)) return;
            var s = DefenseSurvey.For(map);
            var choke = s?.chokes.FirstOrDefault(c => ForChoke(s, c) == null && (c.cell.DistanceTo(at) <= 4f || c.c.span.Any(i => new IntVec3(i % s.grid.w, 0, i / s.grid.w).DistanceTo(at) <= 2f)));
            if (choke == null) return;
            string where = DefenseAdvice.Where(s, choke.cell);
            Find.WindowStack.Add(new Dialog_MessageBox("CC_DefLine_Offer".Translate(t.LabelNow, where).Resolve(),
                "CC_DefLine_Offer_Yes".Translate(), () =>
                {
                    var made = FromChoke(s, choke);
                    if (made == null) return;
                    if (gatePiece != null) made.gate = gatePiece.cell;
                    made.facing = DefenseLineMath.Norm(plan.Outward);
                    made.AddPlan(saved);
                    Messages.Message("CC_DefLine_Made".Translate(DefenseLineReview.NameOf(made, map)).Resolve(), new LookTargets(made.gate, map), MessageTypeDefOf.TaskCompletion, false);
                },
                "CC_DefLine_Offer_No".Translate(), null, "CC_DefLine_Offer_Title".Translate()));
        }

        private static IntVec3 Center(DefensePlan plan)
        {
            TemplateMath.RotatedSize(plan.template.Width, plan.template.Height, plan.rotation, out int w, out int h);
            return new IntVec3(plan.anchor.x + w / 2, 0, plan.anchor.z + h / 2);
        }

        /// <summary>The line a plan placed by hand belongs to: one within a few cells of its gate, or (for plans that go behind, in front or further in) one it faces the same way as, close by.</summary>
        private DefenseLine NearPlan(DefensePlan plan, IntVec3 at)
        {
            var near = Near(at, 4f);
            if (near != null) return near;
            foreach (var p in plan.pieces)
            {
                near = Near(p.cell, 3f);
                if (near != null) return near;
            }
            if (plan.template.placeAt == "choke" || plan.template.placeAt == "core") return null;
            var center = Center(plan);
            return lines.Where(l => DefenseLineMath.Norm(l.facing) == DefenseLineMath.Norm(plan.Outward) && l.gate.DistanceTo(center) <= 16f)
                .OrderBy(l => l.gate.DistanceTo(center)).FirstOrDefault();
        }

        // ---------------- Saving ----------------

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref lines, "lines", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            ExposeWatch();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lines ??= new List<DefenseLine>();
                lines.RemoveAll(l => l == null);
                foreach (var l in lines)
                    if (l.id >= nextId) nextId = l.id + 1;
            }
        }
    }
}
