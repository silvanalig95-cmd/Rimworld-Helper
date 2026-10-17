using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Groups on the page: a card per group for People → Bonds (its name, its people with their roles, how close it is,
    /// its ways and habits, its quarrels and its history), and the colonist's own section of their character page
    /// ("Groups and role", order 50).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class GroupView
    {
        static GroupView()
        {
            try
            {
                CharacterPage.Add("groups", 50, Section);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("groups: character page", e);
            }
        }

        // ---------------- The character page ----------------

        public static CharacterSection Section(Pawn p)
        {
            if (!GroupLife.Active || p == null || !Persona.Keeps(p)) return null;
            var t = GroupTracker.Instance;
            var s = new CharacterSection { title = "CC_Group_PageTitle".Translate() };
            foreach (var g in Groups.Of(p))
            {
                var m = g.Member(p);
                if (m == null) continue;
                string role = m.role == GroupRole.None ? "CC_Group_Page_NoRole".Translate().ToString() : GroupText.Role(m.role);
                string line = "CC_Group_Page_In".Translate(g.Name.Named("GROUP"), GroupText.Date(m.joined).Named("DATE"), role.Named("ROLE")).Resolve();
                var tip = new List<string> { g.Name.CapitalizeFirst() + ": " + (g.why ?? ""), GroupText.RoleTip(m.role) };
                if (m.pending != GroupRole.None && m.pendingDays > 0) tip.Add("CC_Group_Page_Pending".Translate(GroupText.Role(m.pending)).Resolve());
                tip.Add("CC_Group_Card_Cohesion".Translate(GroupText.Cohesion(g.cohesion), g.cohesion.ToString("0")).Resolve());
                if (g.norms.Count > 0) tip.Add("CC_Group_Card_Ways".Translate(GroupText.NormWords(g)).Resolve());
                tip.Add("CC_Group_Card_Members".Translate(GroupText.Members(g)).Resolve());
                s.Line(line, string.Join("\n", tip));
                var odd = GroupLife.Breaks(g, p);
                if (odd != null) s.Line("CC_Group_Page_OutOfStep".Translate(g.Name.Named("GROUP"), odd.label.Named("NORM")).Resolve(), odd.description);
                foreach (var r in g.rivals.Where(r => r.announced).OrderByDescending(r => r.strength).Take(2))
                {
                    var other = t?.Get(r.other);
                    if (other != null) s.Line("CC_Group_Page_Rival".Translate(g.Name.Named("GROUP"), other.Name.Named("OTHER")).Resolve(), r.why);
                }
            }
            var n = t?.NewcomerOf(p);
            if (n != null && n.progress < GroupMath.Accepted)
            {
                var into = t.Get(n.groupId);
                if (into == null) s.Line("CC_Group_Page_NewNoGroup".Translate(p.Named("PAWN")).Resolve(), "CC_Group_OutsiderTip".Translate());
                else if (n.closed) s.Line("CC_Group_Page_ShutOut".Translate(into.Name.Named("GROUP"), p.Named("PAWN")).Resolve(), "CC_Group_OutsiderTip".Translate());
                else
                    s.Line("CC_Group_Page_FindingWay".Translate(into.Name.Named("GROUP"), Mathf.RoundToInt(n.progress).Named("PCT"),
                        (n.sponsor?.LabelShort ?? "").Named("SPONSOR")).Resolve() + (n.sponsor != null ? " " + "CC_Group_Page_Sponsor".Translate(n.sponsor.Named("PAWN")).Resolve() : ""),
                        "CC_Group_OutsiderTip".Translate());
            }
            if (t != null)
                foreach (var past in t.past.Where(x => x.members.Contains(p.thingIDNumber)).Reverse().Take(2))
                    s.Line("CC_Group_Page_Past".Translate(past.name.Named("GROUP"), GroupText.Date(past.ended).Named("DATE"), (past.how ?? "").Named("HOW")).Resolve());
            if (s.Empty && p.IsColonist) s.Line("CC_Group_Page_None".Translate(p.Named("PAWN")).Resolve(), "CC_Group_NoneTip".Translate());
            return s;
        }

        // ---------------- A group's card ----------------

        private const float Pad = 8f, Face = 34f, FaceW = 78f;

        /// <summary>A group's card at (x, y), <paramref name="w"/> wide; returns its height. With draw off, only measures.</summary>
        public static float Card(float x, float y, float w, SocialGroup g, GroupTracker t, bool draw)
        {
            var color = GroupText.ColorOf(g);
            float cy = y + Pad;
            float inner = w - Pad * 2f - 4f;
            float ix = x + Pad + 4f;
            // Its name, what binds it, since when.
            if (draw)
            {
                UiKit.Icon(new Rect(ix, cy + 3f, 18f, 18f), Icons.Bonds, color);
                Text.Font = GameFont.Medium;
                float nameW = Mathf.Min(inner * 0.5f, Text.CalcSize(g.Name.CapitalizeFirst()).x + 8f);
                UiKit.LabelFit(new Rect(ix + 24f, cy - 2f, nameW, 30f), g.Name.CapitalizeFirst(), color, GameFont.Medium);
                string since = "CC_Group_Card_Since".Translate(GroupText.Date(g.formed)).Resolve();
                UiKit.LabelFit(new Rect(ix + 30f + nameW, cy + 4f, inner - nameW - 30f, 20f), (g.why ?? "") + "  ·  " + since, UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(new Rect(ix, cy, inner, 26f), () => Tip(g, t), g.id ^ 0x6A0F);
            }
            cy += 30f;
            // How close it is, its ways, habits and views.
            if (draw)
            {
                UiKit.Bar(new Rect(ix, cy + 6f, 120f, 8f), g.cohesion / 100f, color);
                UiKit.LabelFit(new Rect(ix + 126f, cy, 150f, 20f), "CC_Group_Card_Cohesion".Translate(GroupText.Cohesion(g.cohesion), g.cohesion.ToString("0")).Resolve(),
                    UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(new Rect(ix, cy, 270f, 20f), "CC_Group_CohesionTip".Translate());
            }
            var pills = new List<UiKit.PillSpec>();
            foreach (var n in g.NormDefs) pills.Add(new UiKit.PillSpec(n.label, UiKit.Info, null, n.description));
            foreach (var h in g.Habits) pills.Add(new UiKit.PillSpec(h.label, UiKit.Good, null, h.description + "\n\n" + "CC_Group_HabitTip".Translate(g.habitsKept)));
            foreach (var v in g.views) pills.Add(new UiKit.PillSpec(GroupText.View(v), UiKit.Quote, null, "CC_Group_ViewTip".Translate()));
            float px = ix + 280f;
            float pw = Mathf.Max(120f, inner - 280f);
            float ph = UiKit.Pills(px, cy, pw, pills, draw);
            cy += Mathf.Max(22f, ph + 4f);
            // Its people, with their roles; then those at its edge.
            float fx = ix;
            int perRow = Mathf.Max(1, Mathf.FloorToInt(inner / FaceW));
            var members = g.members.Where(m => m.pawn != null && !m.pawn.Dead).OrderBy(m => m.role == GroupRole.Anchor ? 0 : m.role == GroupRole.Newcomer ? 2 : 1).ThenBy(m => m.joined).ToList();
            var edge = GroupNewcomers.Into(t, g);
            int count = members.Count + edge.Count;
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)perRow));
            float rowH = Face + 30f;
            if (draw)
            {
                int k = 0;
                foreach (var m in members)
                {
                    DrawFace(ix + (k % perRow) * FaceW, cy + (k / perRow) * rowH, m.pawn, GroupText.Role(m.role), GroupText.RoleColor(m.role), RoleTip(g, m), 1f);
                    k++;
                }
                foreach (var n in edge)
                {
                    string label = n.closed ? "CC_Group_Card_ShutOut".Translate().ToString() : "CC_Group_Card_Edge".Translate(Mathf.RoundToInt(n.progress)).ToString();
                    DrawFace(ix + (k % perRow) * FaceW, cy + (k / perRow) * rowH, n.pawn, label, n.closed ? UiKit.Bad : UiKit.Info, EdgeTip(n), 0.6f);
                    k++;
                }
            }
            cy += rows * rowH;
            // Its quarrels with other groups.
            foreach (var r in g.rivals.Where(r => r.strength >= GroupMath.RivalryEnds).OrderByDescending(r => r.strength))
            {
                var other = t?.Get(r.other);
                if (other == null) continue;
                if (draw)
                {
                    string text = (r.announced ? "CC_Group_Card_Rival" : "CC_Group_Card_RivalBudding").Translate(other.Name.Named("OTHER"), GroupText.Rivalry(r.strength).Named("HOW"),
                        (r.why ?? "").Named("WHY")).Resolve();
                    UiKit.LabelFit(new Rect(ix, cy, inner, 20f), text, r.announced ? new Color(0.92f, 0.55f, 0.45f) : UiKit.Faint, GameFont.Tiny);
                    TooltipHandler.TipRegion(new Rect(ix, cy, inner, 20f), "CC_Group_RivalTip".Translate(r.strength.ToString("0")));
                }
                cy += 20f;
            }
            // Its history, newest first.
            var history = g.history.AsEnumerable().Reverse().Take(5).ToList();
            if (history.Count > 0)
            {
                if (draw) UiKit.Divider(ix, cy + 2f, inner);
                cy += 6f;
                foreach (var e in history)
                {
                    if (draw)
                    {
                        UiKit.DiscFill(new Rect(ix + 2f, cy + 6f, 6f, 6f), UiKit.WithAlpha(color, e.major ? 0.9f : 0.45f));
                        UiKit.LabelFit(new Rect(ix + 14f, cy, 90f, 18f), GroupText.Day(e.tick), UiKit.Dim, GameFont.Tiny);
                        UiKit.LabelFit(new Rect(ix + 106f, cy, inner - 106f, 18f), e.text, e.major ? Color.white : UiKit.Faint, GameFont.Tiny);
                        TooltipHandler.TipRegion(new Rect(ix, cy, inner, 18f), GroupText.Date(e.tick) + "\n" + e.text);
                    }
                    cy += 18f;
                }
            }
            cy += Pad;
            if (draw)
            {
                var r = new Rect(x, y, w, cy - y);
                UiKit.Card(r, color, false, false);
            }
            return cy - y;
        }

        private static void DrawFace(float x, float y, Pawn p, string label, Color roleColor, string tip, float alpha)
        {
            var face = new Rect(x + (FaceW - Face) / 2f, y, Face, Face);
            UiKit.Portrait(face, p, roleColor, alpha);
            UiKit.LabelFit(new Rect(x, y + Face + 1f, FaceW - 4f, 15f), p.LabelShort, new Color(1f, 1f, 1f, 0.6f + 0.4f * alpha), GameFont.Tiny, TextAnchor.UpperCenter);
            UiKit.LabelFit(new Rect(x, y + Face + 14f, FaceW - 4f, 15f), label, roleColor, GameFont.Tiny, TextAnchor.UpperCenter);
            var area = new Rect(x, y, FaceW - 4f, Face + 29f);
            Widgets.DrawHighlightIfMouseover(area);
            TooltipHandler.TipRegion(area, tip);
            if (Widgets.ButtonInvisible(area)) MainTabWindow_Council.OpenPerson(p);
        }

        private static string RoleTip(SocialGroup g, GroupMember m)
        {
            var lines = new List<string> { m.pawn.LabelShortCap + ": " + GroupText.Role(m.role), GroupText.RoleTip(m.role) };
            lines.Add("CC_Group_Card_Joined".Translate(GroupText.Date(m.joined)).Resolve());
            if (m.pending != GroupRole.None && m.pendingDays > 0) lines.Add("CC_Group_Page_Pending".Translate(GroupText.Role(m.pending)).Resolve());
            var odd = GroupLife.Breaks(g, m.pawn);
            if (odd != null) lines.Add("CC_Group_Card_OutOfStep".Translate(odd.label).Resolve());
            if (Persona.On) lines.Add("CC_Group_Card_Character".Translate(Persona.Words(m.pawn)).Resolve());
            return string.Join("\n", lines);
        }

        private static string EdgeTip(GroupNewcomer n)
        {
            var lines = new List<string> { "CC_Group_OutsiderTip".Translate() };
            lines.Add("CC_Group_Card_EdgeTip".Translate(n.pawn.LabelShort, Mathf.RoundToInt(n.progress)).Resolve());
            if (n.sponsor != null) lines.Add("CC_Group_Page_Sponsor".Translate(n.sponsor.Named("PAWN")).Resolve());
            if (n.closed) lines.Add("CC_Group_Card_ClosedTip".Translate());
            return string.Join("\n", lines);
        }

        private static string Tip(SocialGroup g, GroupTracker t)
        {
            var lines = new List<string> { g.Name.CapitalizeFirst(), g.why ?? "", "CC_Group_Card_Since".Translate(GroupText.Date(g.formed)).Resolve(), "" };
            lines.Add("CC_Group_Card_Cohesion".Translate(GroupText.Cohesion(g.cohesion), g.cohesion.ToString("0")).Resolve());
            lines.Add("CC_Group_CohesionTip".Translate());
            if (g.history.Count > 0)
            {
                lines.Add("");
                foreach (var e in g.history) lines.Add(GroupText.Day(e.tick) + ": " + e.text);
            }
            return string.Join("\n", lines);
        }

        /// <summary>The groups a colonist is in, for a tooltip: "the hunters (the joker)".</summary>
        public static string Of(Pawn p) =>
            string.Join("\n", Groups.Of(p).Select(g => g.Name.CapitalizeFirst() + " (" + GroupText.Role(g.RoleOf(p)) + ")"));
    }

    /// <summary>
    /// Opinion from groups (Council_Grp_OneOfUs, Council_Grp_OtherLot): one of us, a little warmer; one of the other lot,
    /// once the two groups are at odds, a little colder. No mood.
    /// </summary>
    public class ThoughtWorker_GroupBond : ThoughtWorker
    {
        private bool? rival;

        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn other)
        {
            if (other == null || p == other || !GroupLife.Active) return ThoughtState.Inactive;
            rival ??= def.defName.EndsWith("OtherLot", StringComparison.Ordinal);
            if (rival.Value)
            {
                float r = Groups.Rivalry(p, other);
                return r >= GroupMath.RivalryShows ? ThoughtState.ActiveAtStage(r >= 65f ? 1 : 0) : ThoughtState.Inactive;
            }
            return Groups.Same(p, other) ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
        }
    }
}
