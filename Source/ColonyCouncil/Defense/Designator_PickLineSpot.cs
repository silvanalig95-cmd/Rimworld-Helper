using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "Pick a spot on the map" for a new defense line: click the cell where raiders cross. The line faces the way the
    /// gap it's in faces (a narrow place the survey found close by), else away from the colony's middle; the rotate keys
    /// or buttons turn it from there. The cells across the way it faces are its span. Then the Defenses page opens on it.
    /// </summary>
    public class Designator_PickLineSpot : Designator
    {
        private static readonly Color SpanColor = new Color(1f, 0.85f, 0.35f);

        /// <summary>Quarter turns the player has given the line from the facing worked out for the spot.</summary>
        private int turn;

        public Designator_PickLineSpot()
        {
            useMouseIcon = true;
            soundSucceeded = SoundDefOf.Designate_PlanAdd;
            icon = Icons.Defenses ?? Icons.CatDefense;
        }

        public override string Label => "CC_DefLine_PickSpot".Translate();
        public override string Desc => "CC_DefLine_PickSpot_Tip".Translate();

        public static void Begin()
        {
            if (Find.CurrentMap == null) return;
            Find.MainTabsRoot.EscapeCurrentTab(false);
            Find.DesignatorManager.Select(new Designator_PickLineSpot());
            DefenseOverlay.planning = true;
            Messages.Message("CC_DefLine_PickSpot_Msg".Translate(), MessageTypeDefOf.SilentInput, false);
        }

        public override void Deselected()
        {
            base.Deselected();
            DefenseOverlay.planning = false;
        }

        /// <summary>Which way the line at this cell faces: the gap's way (or away from the colony), turned as the player turned it.</summary>
        private int FacingAt(DefenseSurvey s, IntVec3 c)
        {
            var choke = s.chokes.Where(x => x.cell.DistanceTo(c) <= 10f).OrderBy(x => x.cell.DistanceTo(c)).FirstOrDefault();
            var from = s.bedCenter.IsValid ? s.bedCenter : s.center;
            int facing = choke != null ? choke.c.facing : DefenseLineMath.FacingOf(c.x - from.x, c.z - from.z);
            return DefenseLineMath.Norm(facing + turn);
        }

        public override void SelectedProcessInput(Event ev)
        {
            base.SelectedProcessInput(ev);
            int dir = RotateControls.FromKeys();
            if (dir != 0) turn = DefenseLineMath.Norm(turn + dir);
        }

        public override void DoExtraGuiControls(float leftX, float bottomY) =>
            RotateControls.Draw(leftX, bottomY, dir => turn = DefenseLineMath.Norm(turn + dir));

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map) || c.Fogged(Map)) return false;
            if (!c.Walkable(Map)) return "CC_DefLine_PickSpot_NotHere".Translate();
            var comp = DefenseLines.Of(Map);
            if (comp?.Near(c, 3f) != null) return "CC_DefLine_PickSpot_Taken".Translate();
            return true;
        }

        private DefenseLine made;

        public override void DesignateSingleCell(IntVec3 c)
        {
            var comp = DefenseLines.Of(Map);
            var survey = DefenseSurvey.Kept(Map);
            made = comp?.AtCell(c, survey?.grid != null ? FacingAt(survey, c) : -1);
            if (made == null)
            {
                Messages.Message("CC_Def_NoSurvey".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            Messages.Message("CC_DefLine_Made".Translate(DefenseLineReview.NameOf(made, Map)).Resolve(), new LookTargets(made.gate, Map), MessageTypeDefOf.TaskCompletion, false);
        }

        /// <summary>One line per pick: once it's made, the picking ends and the Defenses page opens on it.</summary>
        protected override void FinalizeDesignationSucceeded()
        {
            base.FinalizeDesignationSucceeded();
            if (made == null) return;
            made = null;
            Find.DesignatorManager.Deselect();
            MainTabWindow_Council.OpenTab("Defenses");
        }

        public override void SelectedUpdate()
        {
            base.SelectedUpdate();
            var map = Find.CurrentMap;
            var c = UI.MouseCell();
            var s = DefenseSurvey.Kept(map);
            if (map == null || s?.grid == null || !c.InBounds(map)) return;
            int facing = FacingAt(s, c);
            var span = DefenseLineMath.SpanAcross(s.grid, c.x, c.z, facing, DefenseMath.MaxSpanSide);
            GenDraw.DrawFieldEdges(span.Select(i => new IntVec3(i % s.grid.w, 0, i / s.grid.w)).ToList(), SpanColor);
            var tip = c + IntVec3.North.RotatedBy(new Rot4(facing)) * 5;
            GenDraw.DrawLineBetween(c.ToVector3Shifted(), tip.ToVector3Shifted(), SimpleColor.Orange);
        }

        public override void DrawMouseAttachments()
        {
            var map = Find.CurrentMap;
            var s = DefenseSurvey.Kept(map);
            var c = UI.MouseCell();
            if (map == null || s == null || !c.InBounds(map))
            {
                base.DrawMouseAttachments();
                return;
            }
            int facing = FacingAt(s, c);
            GenUI.DrawMouseAttachment(icon, "CC_Def_Faces".Translate(DefenseSurvey.FacingWord(facing)) + "\n" +
                "CC_DefLine_PickSpot_Turn".Translate(KeyBindingDefOf.Designator_RotateLeft.MainKeyLabel, KeyBindingDefOf.Designator_RotateRight.MainKeyLabel));
        }
    }
}
