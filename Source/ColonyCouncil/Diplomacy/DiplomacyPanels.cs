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
    /// anywhere: what is owed each way (the owed strip at the top of Region → Peoples), a people's diplomacy (stance, head,
    /// ties in their age's words, what is under way, the acts that fit, each greyed with why not; in each people's card on
    /// Peoples), who is here from outside, who of the colony's is away and the peoples it is bound to by marriage (sections of
    /// Region → Here and away), a town's ties and calling one in (the Ties slot of the town's card, UI/Cards/), and This week's line for a
    /// call of theirs waiting on the colony (never with a timer). Their old page names (Diplomacy, Household) forward to
    /// those views (UI/PagesMath.cs). A name in a row opens its card through DiplomacyLinks.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DiplomacyPanels
    {
        /// <summary>The old page names, now addresses of the Peoples view and of Here and away.</summary>
        public const string DiplomacyPage = "Diplomacy", HouseholdPage = "Household";

        /// <summary>A people's name on its card leads to it: link ink (UI/UiKit.cs's tokens; the page's own violet gave way).</summary>
        private static readonly Color Accent = UiKit.Link;

        static DiplomacyPanels()
        {
            // A town's debts of honor are its card's Ties (UI/Cards/: the card draws the heading).
            CardSections.Town(CardSlot.Ties, 10f, DrawTown, "CC_Dip_TownHeader", "debts");
        }

        // ================= The pieces =================

        private const float ButtonW = 170f;

        /// <summary>Why an act can't be done now, for the kit's buttons (null: it can).</summary>
        private static string WhyNot(DipAct a) => a.why ?? (a.run == null ? a.tip ?? "" : null);

        /// <summary>An act's button (the kit's card act): greyed with its why-not in the tooltip when it can't be now. Returns whether it was used.</summary>
        public static bool ActButton(Rect r, DipAct a)
        {
            if (a == null) return false;
            return UiKit.ActButton(r, a.label, WhyNot(a), a.tip) && Run(a);
        }

        /// <summary>A row's act (the kit's small one on the right of a row). Returns whether it was used.</summary>
        public static bool RowActButton(Rect r, DipAct a) => a != null && UiKit.RowAct(r, a.label, WhyNot(a), a.tip) && Run(a);

        private static bool Run(DipAct a)
        {
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

        /// <summary>
        /// A row: its words (a click opens what it names), a quieter note below, its act at the right (the kit's row act),
        /// and when the act can't be done now, a line of why under the words. Returns the height used.
        /// </summary>
        public static float Row(float x, float y, float w, DipRow r)
        {
            if (r == null) return 0f;
            float actW = r.act != null ? UiKit.RowActWidth(r.act.label) : 0f;
            float textW = r.act != null ? w - actW - 10f : w;
            Text.Font = GameFont.Small;
            float h = Text.CalcHeight(r.text, textW);
            var textRect = new Rect(x, y, textW, h);
            GUI.color = r.color;
            Widgets.Label(textRect, r.text);
            GUI.color = Color.white;
            if (DiplomacyLinks.Opens(r.link))
            {
                UiKit.RowHover(textRect);
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
            if (r.act != null)
            {
                RowActButton(new Rect(x + w - actW, y, actW, 24f), r.act);
                if (r.act.why != null) used += UiKit.WhyNotLine(x, y + used, textW, r.act.label, r.act.why);
            }
            return Mathf.Max(used, r.act != null ? 26f : 0f) + 4f;
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
            UiKit.RowHover(title);
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
            if (p.head != null) h += RowHeight(p.head, inner);
            if (p.ties.Count == 0) h += Text.CalcHeight(DipWords.P("CC_Dip_NoTies"), inner) + 4f;
            foreach (var r in p.ties) h += RowHeight(r, inner);
            if (p.underway.Count > 0)
            {
                h += Text.CalcHeight(DipWords.P("CC_Dip_UnderWay"), inner) + 2f;
                foreach (var r in p.underway) h += RowHeight(r, inner - 8f);
            }
            return h + 10f;
        }

        /// <summary>How tall a row is in a width, as <see cref="Row"/> draws it.</summary>
        public static float RowHeight(DipRow r, float w)
        {
            if (r == null) return 0f;
            float tw = r.act != null ? w - UiKit.RowActWidth(r.act.label) - 10f : w;
            Text.Font = GameFont.Small;
            float t = Text.CalcHeight(r.text, tw);
            if (!r.note.NullOrEmpty())
            {
                Text.Font = GameFont.Tiny;
                t += Text.CalcHeight(r.note, tw);
                Text.Font = GameFont.Small;
            }
            if (r.act?.why != null) t += 18f;
            return Mathf.Max(t, r.act != null ? 26f : 0f) + 4f;
        }

        /// <summary>
        /// A section of rows under its heading with its count, folding with <paramref name="folds"/>, or a sentence of what
        /// will show there. Returns the y below it.
        /// </summary>
        public static float Section(float y, float w, string title, List<DipRow> rows, string empty, Folds folds = null, string key = null)
        {
            y += UiKit.SectionHeading(0f, y, w, title, rows.Count, folds, key ?? title, out bool open);
            if (!open) return y + 4f;
            if (rows.Count == 0) return y + UiKit.Paragraph(4f, y, w - 4f, empty, UiKit.Faint) + 10f;
            foreach (var r in rows) y += Row(8f, y, w - 8f, r);
            return y + 8f;
        }

        // ================= What is owed each way (Region → Peoples) =================

        /// <summary>
        /// The owed strip: what peoples owe the colony and what it owes them, each row with its act (call it in, answer it).
        /// Nothing while there are no debts and favours are off. Returns the y below it.
        /// </summary>
        public static float Owed(float y, float w, DiplomacyModel m, Folds folds = null)
        {
            if (!Diplomacy.FavoursOn && m.owedUs.Count == 0 && m.owedByUs.Count == 0) return y;
            y = Section(y, w, DipWords.P("CC_Dip_OwedUs"), m.owedUs, DipWords.P("CC_Dip_OwedUs_None"), folds, "owedUs");
            return Section(y, w, DipWords.P("CC_Dip_OwedByUs"), m.owedByUs, DipWords.P("CC_Dip_OwedByUs_None"), folds, "owedByUs");
        }

        /// <summary>What is owed each way, counted (the Peoples view's stat).</summary>
        public static string OwedStat(DiplomacyModel m)
        {
            int owed = m.owedUs.Count + m.owedByUs.Count;
            return owed > 0 ? DipWords.P("CC_Dip_Stat", owed.Named("COUNT")) : null;
        }

        /// <summary>
        /// A people's diplomacy inside its card on the Peoples view: its head, its ties in their age's words (or that it has
        /// none), what is under way. Returns the height used; <paramref name="draw"/> false only measures.
        /// </summary>
        public static float PeopleBody(float x, float y, float w, DipPeople p, bool draw)
        {
            float y0 = y;
            if (p == null) return 0f;
            Text.Font = GameFont.Small;
            if (p.head != null) y += draw ? Row(x, y, w, p.head) : RowHeight(p.head, w);
            if (p.ties.Count == 0)
            {
                string none = DipWords.P("CC_Dip_NoTies");
                y += (draw ? UiKit.Paragraph(x, y, w, none, UiKit.Faint) : Text.CalcHeight(none, w)) + 4f;
            }
            foreach (var r in p.ties) y += draw ? Row(x, y, w, r) : RowHeight(r, w);
            if (p.underway.Count > 0)
            {
                string uw = DipWords.P("CC_Dip_UnderWay");
                y += (draw ? UiKit.Paragraph(x, y, w, uw, UiKit.HeaderColor) : Text.CalcHeight(uw, w)) + 2f;
                foreach (var r in p.underway) y += draw ? Row(x + 8f, y, w - 8f, r) : RowHeight(r, w - 8f);
            }
            return y - y0;
        }

        private static void Reset()
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        // ================= The region map's town panel =================

        /// <summary>A town's ties with the colony and calling one in (the town's card, its Ties slot, under the card's heading); nothing when there are none.</summary>
        public static float DrawTown(Rect rect, Settlement s)
        {
            if (s == null || s.Faction == null) return 0f;
            try
            {
                var rows = DiplomacyModel.TownRows(s);
                if (rows.Count == 0) return 0f;
                float y = rect.y;
                foreach (var r in rows)
                {
                    // The panel is narrow: the act goes under the words.
                    var act = r.act;
                    r.act = null;
                    y += Row(rect.x, y, rect.width, r);
                    r.act = act;
                    if (act != null)
                    {
                        ActButton(new Rect(rect.x, y, Mathf.Min(rect.width, UiKit.ActButtonWidth(act.label)), 26f), act);
                        y += 30f;
                        if (act.why != null) y += UiKit.WhyNotLine(rect.x, y, rect.width, act.label, act.why) + 2f;
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

        /// <summary>
        /// A call of theirs waiting for the colony's answer, in This week's Needs you (UI/Inbox.cs): never with a timer,
        /// its people a link, its act opening the letter.
        /// </summary>
        public static void WeekRows(List<WeekRow> rows)
        {
            if (rows == null || WorldLedger.Instance == null) return;
            try
            {
                foreach (var t in FavourAsks.Waiting())
                {
                    var f = Refs.Faction(t.to);
                    if (f == null || !FavourMath.ParseNeed(t.terms.goods, out var need)) continue;
                    var home = FavoursOwed.HomeOf(t);
                    var people = ThingRefs.Of(f);
                    string text = DipWords.P("CC_Dip_Week", ThingRefs.Link(DipWords.People(f), people).Named("PEOPLE"), DipWords.Need(need).Named("NEED"));
                    if (Homes.All.Count > 1 && home != null) text = Homes.Name(home) + ": " + text;
                    var letter = FavourAsks.AnyLetterFor(t);
                    Action open = letter != null ? (Action)(() => DiplomacyLinks.Open(letter)) : WeekModel.Page(DiplomacyPage);
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Promise ?? Icons.Trust,
                        color = UiKit.Warn,
                        faction = f,
                        text = text,
                        sub = DipWords.P(FavourMath.ByMission(need) ? "CC_Dip_WeekNote_Mission" : "CC_Dip_WeekNote"),
                        subColor = UiKit.Warn,
                        tip = DipWords.DebtLine(t),
                        click = open,
                        act = InboxWords.Answer,
                        actRun = open,
                        follow = ThingRefs.Of(t),
                        order = -1
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("diplomacy: this week", e);
            }
        }

        /// <summary>How many of their calls wait for the colony's answer (the one count of what waits on the player).</summary>
        public static int WeekCount()
        {
            if (WorldLedger.Instance == null) return 0;
            int n = 0;
            foreach (var t in FavourAsks.Waiting())
                if (Refs.Faction(t.to) != null && FavourMath.ParseNeed(t.terms.goods, out _))
                    n++;
            return n;
        }
    }
}
