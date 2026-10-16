using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The defense survey drawn on the map: the ways raiders walk in from the edge (red), the narrow places on them
    /// (yellow, with their width and the share of raids that pass), the side each one faces (an orange arrow), and where
    /// the gate line would go (gold). Drawn only while the Defenses page is open, while a defense is being planned, or
    /// while the player has the overlay switched on; it draws the survey already made and never works one out itself.
    /// </summary>
    public class DefenseOverlay : MapComponent
    {
        /// <summary>The player's switch on the Defenses page, remembered between sessions.</summary>
        public static bool Show
        {
            get => show ??= PageMemory.GetBool("defenses.overlay", false);
            set
            {
                show = value;
                PageMemory.SetBool("defenses.overlay", value);
            }
        }

        private static bool? show;
        /// <summary>A defense plan is selected.</summary>
        public static bool planning;
        private static int pageFrame = -10;

        /// <summary>The Defenses page was drawn this frame.</summary>
        public static void PageDrawn() => pageFrame = Time.frameCount;

        public static bool Showing => Show || planning || Time.frameCount - pageFrame < 3;

        private DefenseSurvey drawn;
        private readonly List<List<Vector3>> ways = new List<List<Vector3>>();
        private readonly List<List<IntVec3>> gaps = new List<List<IntVec3>>();
        private readonly List<(Vector3 from, Vector3 to)> arrows = new List<(Vector3, Vector3)>();
        private readonly List<List<IntVec3>> lines = new List<List<IntVec3>>();
        private readonly List<(IntVec3 cell, string text)> labels = new List<(IntVec3, string)>();

        private static readonly Color GapColor = new Color(1f, 0.9f, 0.3f);
        private static readonly Color HeldColor = new Color(0.45f, 0.75f, 1f);
        private static readonly Color LineColor = new Color(1f, 0.78f, 0.35f, 0.9f);

        public DefenseOverlay(Map map) : base(map)
        {
        }

        public override void MapComponentUpdate()
        {
            if (!Showing || map != Find.CurrentMap) return;
            var s = DefenseSurvey.Kept(map);
            if (s?.result == null) return;
            if (s != drawn) Prepare(s);
            foreach (var way in ways)
                for (int i = 1; i < way.Count; i++) GenDraw.DrawLineBetween(way[i - 1], way[i], SimpleColor.Red, 0.12f);
            for (int i = 0; i < gaps.Count; i++)
            {
                bool held = i < s.chokes.Count && s.chokes[i].gated;
                GenDraw.DrawFieldEdges(gaps[i], held ? HeldColor : GapColor);
            }
            foreach (var line in lines) GenDraw.DrawFieldEdges(line, LineColor);
            foreach (var (from, to) in arrows) GenDraw.DrawLineBetween(from, to, SimpleColor.Orange, 0.3f);
        }

        public override void MapComponentOnGUI()
        {
            if (!Showing || map != Find.CurrentMap || drawn == null || drawn != DefenseSurvey.Kept(map)) return;
            if (Find.CameraDriver.CurrentZoom > CameraZoomRange.Far) return;
            foreach (var (cell, text) in labels)
                GenMapUI.DrawThingLabel(cell.ToVector3Shifted().MapToUIPosition(), text, GapColor);
        }

        /// <summary>Works out what to draw for a survey, once: the ways in thinned to every third cell, the gaps, the arrows, the gate lines.</summary>
        private void Prepare(DefenseSurvey s)
        {
            drawn = s;
            ways.Clear();
            gaps.Clear();
            arrows.Clear();
            lines.Clear();
            labels.Clear();
            var r = s.result;
            int w = r.w;
            foreach (var way in r.shown)
            {
                var pts = new List<Vector3>();
                for (int i = 0; i < way.cells.Count; i += 3)
                {
                    int c = way.cells[i];
                    pts.Add(new IntVec3(c % w, 0, c / w).ToVector3Shifted());
                }
                int last = way.cells[way.cells.Count - 1];
                pts.Add(new IntVec3(last % w, 0, last / w).ToVector3Shifted());
                ways.Add(pts);
            }
            var gate = DefDatabase<DefenseTemplateDef>.GetNamedSilentFail("Rimfolk_Defense_Gate");
            foreach (var info in s.chokes)
            {
                var c = info.c;
                gaps.Add(c.span.Select(i => new IntVec3(i % w, 0, i / w)).ToList());
                var outward = info.Outward;
                arrows.Add((info.cell.ToVector3Shifted(), (info.cell + outward * 5).ToVector3Shifted()));
                labels.Add((info.cell + outward * 2, "CC_Def_MapLabel".Translate(c.width, Mathf.RoundToInt(c.share * 10f)).Resolve()));
                // Where a gate's wall line would cross the gap.
                if (!info.gated && gate != null)
                {
                    var side = outward.RotatedBy(Rot4.East);
                    var line = new List<IntVec3>();
                    int half = Mathf.Max(gate.Width, c.width + 2) / 2;
                    for (int k = -half; k <= half; k++)
                    {
                        var cell = info.cell + side * k;
                        if (cell.InBounds(map)) line.Add(cell);
                    }
                    lines.Add(line);
                }
            }
            if (s.chokes.Count == 0 && s.approach.IsValid)
            {
                var outward = IntVec3.North.RotatedBy(new Rot4(s.approachFacing));
                arrows.Add((s.approach.ToVector3Shifted(), (s.approach + outward * 5).ToVector3Shifted()));
                labels.Add((s.approach + outward * 2, "CC_Def_MapOpen".Translate()));
            }
        }
    }
}
