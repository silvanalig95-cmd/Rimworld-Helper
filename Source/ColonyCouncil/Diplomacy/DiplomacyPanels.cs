using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Diplomacy's panels, each a static Draw over the one model (DiplomacyModel), its rows small pieces that can be drawn
    /// anywhere: the Diplomacy page (what is owed each way, then a card for each people: stance, head, ties in their age's
    /// words, what is under way, the acts that fit, each greyed with why not), the Household page (who is here from outside,
    /// who of the colony's is away, the colony's houses), the region map's town section (its ties and calling one in), and
    /// This week's line for a call of theirs waiting on the colony (never with a timer). Registered as region pages for now;
    /// the interface's redesign may move them. A name in a row opens through DiplomacyLinks.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DiplomacyPanels
    {
        public const string DiplomacyPage = "Diplomacy", HouseholdPage = "Household";

        private static readonly Color Accent = new Color(0.78f, 0.74f, 0.92f);
        private static Vector2 scrollD, scrollH;
        private static float heightD = 600f, heightH = 600f;

        static DiplomacyPanels()
        {
            MainTabWindow_World.Register(DiplomacyPage, "CC_Tab_Diplomacy", Icons.Promise ?? Icons.Trust, DrawDiplomacy, null, DiplomacyMath.DiplomacyOrder);
            MainTabWindow_World.Register(HouseholdPage, "CC_Tab_Household", Icons.People ?? Icons.Colony, DrawHousehold, null, DiplomacyMath.HouseholdOrder);
            NeighbourPanel.Sections.Add(DrawTown);
        }

        // ================= The pieces =================

        private const float ButtonW = 170f;

        /// <summary>An act's button: greyed with its why-not in the tooltip when it can't be now. Returns whether it was used.</summary>
        public static bool ActButton(Rect r, DipAct a)
        {
            if (a == null) return false;
            bool ok = a.why == null && a.run != null;
            bool clicked = Widgets.ButtonText(r, a.label.Truncate(r.width - 8f), true, true, ok);
            string tip = ok ? a.tip : (a.why ?? a.tip);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            if (!clicked || !ok) return false;
            try
            {
                a.run();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("diplomacy: an act", e);
            }
            DiplomacyModel.Dirty();
            return true;
        }

        /// <summary>A row: its words (a click opens what it names), a quieter note below, its act at the right. Returns the height used.</summary>
        public static float Row(float x, float y, float w, DipRow r)
        {
            if (r == null) return 0f;
            float textW = r.act != null ? w - ButtonW - 10f : w;
            Text.Font = GameFont.Small;
            float h = Text.CalcHeight(r.text, textW);
            var textRect = new Rect(x, y, textW, h);
            GUI.color = r.color;
            Widgets.Label(textRect, r.text);
            GUI.color = Color.white;
            if (DiplomacyLinks.Opens(r.link))
            {
                Widgets.DrawHighlightIfMouseover(textRect);
                if (Widgets.ButtonInvisible(textRect)) DiplomacyLinks.Open(r.link);
            }
            if (!r.tip.NullOrEmpty()) TooltipHandler.TipRegion(textRect, r.tip);
            float used = h;
            if (!r.note.NullOrEmpty())
            {
                Text.Font = GameFont.Tiny;
                float nh = Text.CalcHeight(r.note, textW);
                GUI.color = UiKit.Faint;
                Widgets.Label(new Rect(x, y + used, textW, nh), r.note);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                used += nh;
            }
            if (r.act != null) ActButton(new Rect(x + w - ButtonW, y, ButtonW, 26f), r.act);
            return Mathf.Max(used, r.act != null ? 28f : 0f) + 4f;
        }

        /// <summary>A people's card. Returns the height used.</summary>
        public static float PeopleCard(float x, float y, float w, DipPeople p)
        {
            float inner = w - 24f;
            float h = Measure(p, inner);
            UiKit.Card(new Rect(x, y, w, h), p.stanceColor);
            float cy = y + 8f;
            Text.Font = GameFont.Medium;
            var title = new Rect(x + 12f, cy, inner - ButtonW - 10f, 30f);
            GUI.color = Accent;
            Widgets.Label(title, p.name);
            GUI.color = Color.white;
            Widgets.DrawHighlightIfMouseover(title);
            if (Widgets.ButtonInvisible(title)) DiplomacyLinks.Open(p.people);
            Text.Font = GameFont.Small;
            UiKit.LabelFit(new Rect(x + 12f + Mathf.Min(Text.CalcSize(p.name).x + 120f, inner * 0.6f), cy + 4f, ButtonW + 60f, 24f), p.stance, p.stanceColor);
            float ax = x + w - 12f - ButtonW;
            foreach (var a in p.acts)
            {
                ActButton(new Rect(ax, cy, ButtonW, 26f), a);
                ax -= ButtonW + 6f;
            }
            cy += 32f;
            if (p.head != null) cy += Row(x + 12f, cy, inner, p.head);
            if (p.ties.Count == 0) cy += UiKit.Paragraph(x + 12f, cy, inner, DipWords.P("CC_Dip_NoTies"), UiKit.Faint) + 4f;
            foreach (var r in p.ties) cy += Row(x + 12f, cy, inner, r);
            if (p.underway.Count > 0)
            {
                cy += UiKit.Paragraph(x + 12f, cy, inner, DipWords.P("CC_Dip_UnderWay"), UiKit.HeaderColor) + 2f;
                foreach (var r in p.underway) cy += Row(x + 20f, cy, inner - 8f, r);
            }
            return h;
        }

        private static float Measure(DipPeople p, float inner)
        {
            float h = 8f + 32f;
            float RowH(DipRow r, float w)
            {
                float tw = r.act != null ? w - ButtonW - 10f : w;
                Text.Font = GameFont.Small;
                float t = Text.CalcHeight(r.text, tw);
                if (!r.note.NullOrEmpty())
                {
                    Text.Font = GameFont.Tiny;
                    t += Text.CalcHeight(r.note, tw);
                    Text.Font = GameFont.Small;
                }
                return Mathf.Max(t, r.act != null ? 28f : 0f) + 4f;
            }
            if (p.head != null) h += RowH(p.head, inner);
            if (p.ties.Count == 0) h += Text.CalcHeight(DipWords.P("CC_Dip_NoTies"), inner) + 4f;
            foreach (var r in p.ties) h += RowH(r, inner);
            if (p.underway.Count > 0)
            {
                h += Text.CalcHeight(DipWords.P("CC_Dip_UnderWay"), inner) + 2f;
                foreach (var r in p.underway) h += RowH(r, inner - 8f);
            }
            return h + 10f;
        }

        private static float Section(float y, float w, string title, Texture2D icon, List<DipRow> rows, string empty)
        {
            y += UiKit.SectionTitle(0f, y, w, title, icon, Accent);
            if (rows.Count == 0) return y + UiKit.Paragraph(0f, y, w, empty, UiKit.Faint) + 8f;
            foreach (var r in rows) y += Row(8f, y, w - 8f, r);
            return y + 6f;
        }

        // ================= The Diplomacy page =================

        public static void DrawDiplomacy(Rect rect)
        {
            var m = DiplomacyModel.Current;
            int owed = m.owedUs.Count + m.owedByUs.Count;
            string stat = owed > 0 ? DipWords.P("CC_Dip_Stat", owed.Named("COUNT")) : null;
            float top = UiKit.PageHeader(rect, Icons.Promise ?? Icons.Trust, "CC_Dip_Header".Translate(), "CC_Dip_Sub".Translate(), stat, UiKit.Good);
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, heightD);
            Widgets.BeginScrollView(outRect, ref scrollD, view);
            try
            {
                float y = 0f, w = view.width;
                if (!Diplomacy.FavoursOn) y += UiKit.Paragraph(0f, y, w, DipWords.P("CC_Dip_Off"), UiKit.Faint) + 8f;
                y = Section(y, w, DipWords.P("CC_Dip_OwedUs"), Icons.Promise, m.owedUs, DipWords.P("CC_Dip_OwedUs_None"));
                y = Section(y, w, DipWords.P("CC_Dip_OwedByUs"), Icons.Trader, m.owedByUs, DipWords.P("CC_Dip_OwedByUs_None"));
                y += UiKit.SectionTitle(0f, y, w, DipWords.P("CC_Dip_Peoples"), Icons.People, Accent);
                if (m.peoples.Count == 0) y += UiKit.EmptyState(0f, y, w, Icons.World, DipWords.P("CC_Dip_NoPeoples"), 60f);
                foreach (var p in m.peoples) y += PeopleCard(0f, y, w, p) + 8f;
                heightD = Mathf.Max(y + 10f, 100f);
            }
            finally
            {
                Widgets.EndScrollView();
                Reset();
            }
        }

        // ================= The Household page =================

        public static void DrawHousehold(Rect rect)
        {
            var m = DiplomacyModel.Current;
            float top = UiKit.PageHeader(rect, Icons.People ?? Icons.Colony, "CC_Dip_HouseHeader".Translate(), "CC_Dip_HouseSub".Translate());
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, heightH);
            Widgets.BeginScrollView(outRect, ref scrollH, view);
            try
            {
                float y = 0f, w = view.width;
                y = Section(y, w, DipWords.P("CC_Dip_Here_Title"), Icons.Visitor, m.here, DipWords.P("CC_Dip_Here_None"));
                y = Section(y, w, DipWords.P("CC_Dip_Away_Title"), Icons.Trader, m.away, DipWords.P("CC_Dip_Away_None"));
                y = Section(y, w, DipWords.P("CC_Dip_Houses_Title"), Icons.Bonds ?? Icons.People, m.houses, DipWords.P("CC_Dip_Houses_None"));
                heightH = Mathf.Max(y + 10f, 100f);
            }
            finally
            {
                Widgets.EndScrollView();
                Reset();
            }
        }

        private static void Reset()
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        // ================= The region map's town panel =================

        /// <summary>A town's ties with the colony and calling one in (NeighbourPanel.Sections); nothing when there are none.</summary>
        public static float DrawTown(Rect rect, Settlement s)
        {
            if (s == null || s.Faction == null) return 0f;
            try
            {
                var rows = DiplomacyModel.TownRows(s);
                if (rows.Count == 0) return 0f;
                float y = rect.y;
                UiKit.LabelFit(new Rect(rect.x, y, rect.width, 22f), DipWords.P("CC_Dip_TownHeader"), Accent);
                y += 24f;
                foreach (var r in rows)
                {
                    // The panel is narrow: the act goes under the words.
                    var act = r.act;
                    r.act = null;
                    y += Row(rect.x, y, rect.width, r);
                    r.act = act;
                    if (act != null)
                    {
                        ActButton(new Rect(rect.x, y, Mathf.Min(rect.width, ButtonW), 26f), act);
                        y += 30f;
                    }
                }
                Reset();
                return y + 4f - rect.y;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("diplomacy: the town panel", e);
                Reset();
                return 0f;
            }
        }

        // ================= This week =================

        /// <summary>A call of theirs waiting for the colony's answer (never with a timer).</summary>
        public static void WeekRows(WeekCard card)
        {
            if (card == null || WorldLedger.Instance == null) return;
            try
            {
                foreach (var t in FavourAsks.Waiting())
                {
                    var f = Refs.Faction(t.to);
                    if (f == null || !FavourMath.ParseNeed(t.terms.goods, out var need)) continue;
                    var home = FavoursOwed.HomeOf(t);
                    string text = DipWords.P("CC_Dip_Week", DipWords.People(f).Named("PEOPLE"), DipWords.Need(need).Named("NEED"));
                    if (Homes.All.Count > 1 && home != null) text = Homes.Name(home) + ": " + text;
                    var letter = FavourAsks.LetterFor(t);
                    card.rows.Add(new WeekRow
                    {
                        icon = Icons.Promise ?? Icons.Trust,
                        color = UiKit.Warn,
                        text = text,
                        note = DipWords.P(FavourMath.ByMission(need) ? "CC_Dip_WeekNote_Mission" : "CC_Dip_WeekNote"),
                        noteColor = UiKit.Warn,
                        tip = DipWords.DebtLine(t),
                        click = letter != null ? (Action)(() => DiplomacyLinks.Open(letter)) : WeekModel.Page(DiplomacyPage),
                        order = -1
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("diplomacy: this week", e);
            }
        }
    }
}
