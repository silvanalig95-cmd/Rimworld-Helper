using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Council → Decrees: the offices, then each decree as a card: what it does, how long it has left, who will love
    /// or hate it (their portraits), and the buttons to enact or repeal.
    /// </summary>
    public static class DecreePanel
    {
        private static Vector2 scroll;
        private static float height = 600f;

        private static readonly Color Good = new Color(0.55f, 0.9f, 0.55f);
        private static readonly Color Bad = new Color(0.95f, 0.55f, 0.45f);
        private static readonly Color Asked = new Color(1f, 0.85f, 0.45f);

        private const float IconX = 12f;
        private const float TextX = 54f;
        private const float ButtonsW = 180f;
        private const float FaceSize = 22f;

        private static readonly DecreeReaction[] Reactions = { DecreeReaction.Love, DecreeReaction.Like, DecreeReaction.Dislike, DecreeReaction.Hate };

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            var tracker = comp.decrees;
            var pawns = PawnsFinder.AllMaps_FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && p.needs?.mood != null).ToList();
            string state = null;
            if (pawns.Count > 0)
                state = UiCache.Get(comp, 70, () => "CC_Decrees_State".Translate(pawns.Average(p => p.needs.mood.CurLevelPercentage).ToStringPercent(),
                    ("CC_Standards_" + Standards.LevelFor(pawns.Average(p => Standards.Of(p)))).Translate()).Resolve());
            string stat = "CC_UI_Stat_InForce".Translate(tracker.active.Count, DecreeTracker.MaxActive).Resolve();
            float top = UiKit.PageHeader(rect, Icons.Decrees, "CC_Decrees_Header".Translate(), state ?? "CC_UI_Sub_Decrees".Translate().ToString(), stat,
                tracker.active.Count > 0 ? Good : UiKit.Faint, Prefs.DevMode ? 190f : 0f, "CC_Decrees_Sub".Translate(DecreeTracker.MaxActive));
            if (Prefs.DevMode && Widgets.ButtonText(new Rect(rect.xMax - 180f, rect.y + 6f, 180f, 28f), "CC_Dev_Dilemma".Translate()))
            {
                if (!comp.dilemmas.TryFire(true)) Messages.Message("No dilemma could happen right now.", MessageTypeDefOf.RejectInput, false);
            }

            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float w = view.width;
            float cy = 0f;
            cy += UiKit.Paragraph(0f, cy, w, "CC_Decrees_Sub".Translate(DecreeTracker.MaxActive), UiKit.Faint) + 10f;
            if (ColonyCouncilMod.Settings.offices) cy += DrawOffices(comp, w, cy) + 14f;
            cy += UiKit.SectionTitle(0f, cy, w, "CC_Decrees_Header".Translate(), Icons.Decree);
            foreach (var def in DefDatabase<DecreeDef>.AllDefsListForReading.OrderBy(d => d.order))
            {
                // Another era's, another faith's, or a missing mod's decree isn't listed (unless it's somehow in force).
                if (def.Hidden && !tracker.IsActive(def)) continue;
                var l = LayoutOf(def, comp, pawns, w);
                if (cy + l.height >= scroll.y && cy <= scroll.y + outRect.height) DrawDecree(def, l, comp, pawns, w, cy);
                cy += l.height + 8f;
            }
            height = Mathf.Max(cy + 10f, 100f);
            Widgets.EndScrollView();
        }

        // ---------------- Decree cards ----------------

        /// <summary>A card's text, the people for each reaction, and its measurements; worked out about once a second.</summary>
        private class Layout
        {
            public float width = -1f, at, titleH, descH, effectsH, noteH, warnH, facesH, height;
            public string effects, note, warning;
            public List<Pawn>[] faces = new List<Pawn>[4];
        }

        private static readonly Dictionary<DecreeDef, Layout> layouts = new Dictionary<DecreeDef, Layout>();

        private static float TextWidth(float width) => Mathf.Max(100f, width - TextX - ButtonsW - 10f);

        private static Layout LayoutOf(DecreeDef def, CouncilGameComponent comp, List<Pawn> pawns, float width)
        {
            float now = Time.realtimeSinceStartup;
            if (layouts.TryGetValue(def, out var l) && Mathf.Abs(l.width - width) < 0.5f && now - l.at < 1f) return l;
            var tracker = comp.decrees;
            var active = tracker.Get(def);
            l = new Layout { width = width, at = now };
            l.effects = string.Join("\n", def.effects.Select(e => "- " + e));
            // Feelings and strain (Heart/): a hard decree wears on those who resent it.
            if (def.strainPerDay > 0f && Heart.On) l.effects += "\n- " + "CC_Heart_DecreeStrain".Translate(def.strainPerDay.ToString("0.#")).Resolve();
            var req = tracker.RequestFor(def);
            l.note = req?.pawn == null ? null
                : (req.repeal ? "CC_Decree_RepealLine" : "CC_Decree_PetitionLine").Translate(req.pawn.Named("PAWN")).Resolve() + (req.line != null ? " \"" + req.line + "\"" : "");
            int fatigue = active == null ? tracker.Fatigue(def) : 0;
            l.warning = fatigue > 0
                ? "CC_Decree_FatigueWarn".Translate(fatigue, Mathf.RoundToInt(DecreeTracker.FatigueDays), tracker.UnrestChance(def, tracker.OnCooldown(def)).ToStringPercent()).Resolve()
                : null;
            for (int i = 0; i < Reactions.Length; i++)
            {
                var r = Reactions[i];
                l.faces[i] = pawns.Where(p => def.ReactionOf(p) == r).ToList();
            }
            float textW = TextWidth(width);
            Text.Font = GameFont.Small;
            l.titleH = 26f;
            // The longer description lives in the name's tooltip; the card shows what the decree does and who minds.
            l.descH = 0f;
            l.effectsH = Text.CalcHeight(l.effects, textW);
            l.noteH = l.note == null ? 0f : Text.CalcHeight(l.note, textW) + 2f;
            l.warnH = l.warning == null ? 0f : Text.CalcHeight(l.warning, textW) + 2f;
            l.facesH = Faces(l, 0f, 0f, textW, false);
            float h = 8f + l.titleH + l.noteH + l.warnH + l.descH + 2f + l.effectsH + 4f + (active != null ? UiKit.MeterHeight + 4f : 0f) + l.facesH + 8f;
            l.height = Mathf.Max(h, 74f);
            layouts[def] = l;
            return l;
        }

        private static void DrawDecree(DecreeDef def, Layout l, CouncilGameComponent comp, List<Pawn> pawns, float width, float y)
        {
            var tracker = comp.decrees;
            var active = tracker.Get(def);
            var req = tracker.RequestFor(def);
            var accent = active != null ? Good : req != null ? Asked : l.warning != null ? Bad : (Color?)null;
            var box = new Rect(0f, y, width, l.height);
            UiKit.Card(box, accent, false, false);
            if (active != null) Widgets.DrawBoxSolid(box.ContractedBy(1f), new Color(0.3f, 0.45f, 0.3f, 0.12f));
            UiKit.Badge(new Rect(IconX, y + 10f, 32f, 32f), Icons.Decree, active != null ? Good : req != null ? Asked : UiKit.Faint);

            float textX = TextX;
            float textW = TextWidth(width);
            float cy = y + 8f;
            // The name, with what's going on as pills beside it.
            Text.Font = GameFont.Medium;
            string title = def.LabelCap;
            float titleW = Mathf.Min(Text.CalcSize(title).x + 4f, textW);
            // Back to the body font before anything else is drawn: the card's heights were measured with it.
            Text.Font = GameFont.Small;
            UiKit.LabelFit(new Rect(textX, cy - 2f, titleW, 28f), title, active != null ? Good : Color.white, GameFont.Medium);
            if (!def.description.NullOrEmpty())
            {
                TooltipHandler.TipRegion(new Rect(textX, cy - 2f, titleW, 28f), def.description);
                TooltipHandler.TipRegion(new Rect(IconX, y + 10f, 32f, 32f), def.description);
            }
            float px = textX + titleW + 8f;
            if (active != null && px + 60f < textX + textW) px += UiKit.Pill(px, cy + 4f, "CC_UI_Pill_InForce".Translate(), Good, null, null, textX + textW - px) + 4f;
            if (active != null && active.forced && px + 60f < textX + textW) px += UiKit.Pill(px, cy + 4f, "CC_UI_Pill_Forced".Translate(), Bad, null, null, textX + textW - px) + 4f;
            if (req?.pawn != null && px + 60f < textX + textW)
                UiKit.Pill(px, cy + 4f, (req.repeal ? "CC_UI_Pill_RepealAsked" : "CC_UI_Pill_Asked").Translate(req.pawn.LabelShort), Asked, Icons.Promise, null, textX + textW - px);
            cy += l.titleH;
            if (l.note != null)
            {
                cy += UiKit.Paragraph(textX, cy, textW, l.note, Asked) + 2f;
            }
            if (l.warning != null)
            {
                cy += UiKit.Paragraph(textX, cy, textW, l.warning, Bad) + 2f;
            }
            if (l.descH > 0f)
            {
                GUI.color = UiKit.Faint;
                Widgets.Label(new Rect(textX, cy, textW, l.descH), def.description);
                GUI.color = Color.white;
                cy += l.descH + 2f;
            }
            Widgets.Label(new Rect(textX, cy, textW, l.effectsH), l.effects);
            cy += l.effectsH + 4f;
            if (active != null)
            {
                int total = Mathf.Max(1, active.endTick - active.startTick);
                UiKit.Countdown(new Rect(textX, cy, Mathf.Min(textW, 360f), UiKit.MeterHeight), active.endTick - Find.TickManager.TicksGame, total, "CC_UI_TimeLeft".Translate());
                cy += UiKit.MeterHeight + 4f;
            }
            Faces(l, textX, cy, textW, true);

            DrawButtons(def, comp, pawns, width, y);
        }

        /// <summary>Who loves, likes, dislikes and hates it: a tag, then their portraits; wrapping. Returns the height used.</summary>
        private static float Faces(Layout l, float x, float y, float width, bool draw)
        {
            float cx = x, cy = y;
            bool any = false;
            float rowH = FaceSize + 4f;
            for (int i = 0; i < Reactions.Length; i++)
            {
                var people = l.faces[i];
                if (people == null || people.Count == 0) continue;
                any = true;
                var color = UiKit.ReactionColor(Reactions[i]);
                string label = ("CC_UI_Reaction_" + Reactions[i]).Translate();
                float pw = UiKit.PillWidth(label);
                if (cx > x && cx + pw + FaceSize > x + width)
                {
                    cx = x;
                    cy += rowH;
                }
                if (draw)
                {
                    string tip = ReactionLine(Reactions[i], people);
                    UiKit.Pill(cx, cy + (FaceSize - UiKit.PillHeight) / 2f, label, color, null, tip);
                }
                cx += pw + 4f;
                foreach (var p in people)
                {
                    if (cx + FaceSize > x + width)
                    {
                        cx = x + 12f;
                        cy += rowH;
                    }
                    if (draw) UiKit.PawnChip(cx, cy, p, FaceSize, false, FaceSize, color);
                    cx += FaceSize + 2f;
                }
                cx += 10f;
            }
            return any ? cy - y + rowH : 0f;
        }

        private static string ReactionLine(DecreeReaction r, List<Pawn> people)
        {
            string key = r == DecreeReaction.Love ? "CC_Decree_Loves" : r == DecreeReaction.Like ? "CC_Decree_Likes" : r == DecreeReaction.Dislike ? "CC_Decree_Dislikes" : "CC_Decree_Hates";
            return key.Translate(string.Join(", ", people.Select(p => p.LabelShort)));
        }

        private static void DrawButtons(DecreeDef def, CouncilGameComponent comp, List<Pawn> pawns, float width, float y)
        {
            var tracker = comp.decrees;
            var active = tracker.Get(def);
            var btn = new Rect(width - ButtonsW + 4f, y + 10f, ButtonsW - 14f, 32f);
            if (active != null)
            {
                if (Widgets.ButtonText(btn, "CC_Decree_Repeal".Translate())) tracker.Repeal(def, comp, true);
                return;
            }
            string why = tracker.CannotEnact(def);
            if (why == null)
            {
                if (Widgets.ButtonText(btn, UiKit.Fit("CC_Decree_Enact".Translate(TextUtil.DaysString(Mathf.RoundToInt(def.durationDays * GenDate.TicksPerDay))), btn.width - 10f)))
                    tracker.Enact(def, comp);
                return;
            }
            // Only the cooldown stands in the way: it can be forced through, at a price.
            bool forcible = tracker.CannotEnact(def, true) == null;
            var whyRect = forcible ? new Rect(btn.x, btn.y - 4f, btn.width, 24f) : btn;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = UiKit.Faint;
            Text.Font = GameFont.Tiny;
            Widgets.Label(whyRect, UiKit.Fit(why, whyRect.width));
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(whyRect, why);
            if (!forcible) return;
            var forceBtn = new Rect(btn.x, btn.y + 22f, btn.width, 28f);
            string chance = tracker.UnrestChance(def, true).ToStringPercent();
            TooltipHandler.TipRegion(forceBtn, "CC_Decree_ForceTip".Translate(chance));
            if (Widgets.ButtonText(forceBtn, "CC_Decree_Force".Translate()))
            {
                var against = pawns.Where(p => def.ReactionOf(p) <= DecreeReaction.Dislike).Select(p => p.LabelShort).ToList();
                string who = against.Count > 0 ? string.Join(", ", against) : "CC_Decree_ForceNobody".Translate().ToString();
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "CC_Decree_ForceConfirm".Translate(def.label.Named("DECREE"), who.Named("WHO"), chance.Named("CHANCE")).Resolve(),
                    () => tracker.Enact(def, comp, true), true));
            }
        }

        // ---------------- Offices ----------------

        private static float DrawOffices(CouncilGameComponent comp, float width, float y)
        {
            float start = y;
            UiKit.SectionTitle(0f, y, width - 210f, "CC_Offices_Header".Translate(), Icons.Office);
            UiKit.Divider(width - 210f, y + 25f, 210f);
            if (Widgets.ButtonText(new Rect(width - 200f, y, 200f, 24f), "CC_Office_CallElection".Translate()))
            {
                var map = Find.AnyPlayerHomeMap;
                if (map != null) comp.offices.Hold(comp, GenLocalDate.Year(map), false);
            }
            y += UiKit.SectionHeight;
            y += UiKit.Paragraph(0f, y, width, "CC_Offices_Sub".Translate(), UiKit.Faint) + 4f;
            int i = 0;
            foreach (var office in OfficeTracker.Offices)
            for (int seat = 0; seat < office.Seats; seat++)
            {
                var holder = comp.offices.HolderOf(office, seat);
                // A seat this game can't have (the chaplain without a faith) isn't listed, unless someone still holds it.
                if (!office.Available && holder == null) continue;
                int seatNow = seat;
                var row = new Rect(0f, y, width, 32f);
                UiKit.Zebra(row, i++);
                UiKit.Icon(new Rect(6f, y + 7f, 18f, 18f), Icons.Office, holder != null ? UiKit.Gold : UiKit.Dim);
                string name = office.Seats > 1 ? "CC_Office_Seat".Translate(office.LabelCap, seat + 1).Resolve() : office.LabelCap.ToString();
                UiKit.LabelFit(new Rect(30f, y, 150f, 32f), name, Color.white);
                if (holder != null)
                {
                    UiKit.PawnChip(186f, y + 3f, holder, 26f, true, 180f);
                    UiKit.LabelFit(new Rect(372f, y, Mathf.Max(0f, Mathf.Min(120f, width - 240f - 372f)), 32f),
                        "CC_Office_Competence".Translate(office.Competence(holder).ToString("0")), UiKit.Faint, GameFont.Tiny);
                }
                else UiKit.LabelFit(new Rect(186f, y, 180f, 32f), "CC_Office_Empty".Translate(), UiKit.Dim);
                TooltipHandler.TipRegion(new Rect(0f, y, 180f, 32f), office.description + "\n\n" + string.Join("\n", office.duties.Select(d => "- " + d)));
                if (Widgets.ButtonText(new Rect(width - 230f, y + 3f, 120f, 26f), "CC_Office_Appoint".Translate()))
                {
                    var options = PawnsFinder.AllMaps_FreeColonistsSpawned
                        .Where(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Juvenile() && !p.IsSlave)
                        .OrderByDescending(p => office.Competence(p))
                        .Select(p => new FloatMenuOption(p.LabelShort + " (" + "CC_Office_Competence".Translate(office.Competence(p).ToString("0")) + ")", () => comp.offices.Appoint(office, p, comp, seatNow)))
                        .ToList();
                    if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
                }
                if (holder != null && Widgets.ButtonText(new Rect(width - 105f, y + 3f, 100f, 26f), "CC_Office_Dismiss".Translate()))
                    comp.offices.Dismiss(office, comp, seatNow);
                y += 32f;
            }
            return y - start;
        }
    }
}
