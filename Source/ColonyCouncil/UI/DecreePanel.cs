using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Council, then Decrees: the offices, then the decrees. With thirty and more of them the page is organized rather
    /// than one long list: a row of filters (ready now, in force, asked for, needed now, on cooldown), an order (by
    /// kind, most wanted first, soonest ready), every decree at a glance as a small chip with its support bar (a click
    /// scrolls to its card), and the cards in their kinds (work, stores, order, spirit, faith, the wider world), each
    /// kind foldable. A card keeps everything it showed: what the decree does, who loves, likes, dislikes and hates it
    /// (their portraits, now beside the effects rather than under them), a bar of how the colony stands on it, how long
    /// it has left, who asked, why it's needed, and the buttons to enact, force or repeal.
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
        private const float FacesW = 250f;
        private const float FaceSize = 22f;
        private const float ChipH = 34f;

        private static readonly DecreeReaction[] Reactions = { DecreeReaction.Love, DecreeReaction.Like, DecreeReaction.Dislike, DecreeReaction.Hate };

        /// <summary>The kinds in the order of the page (DecreeDef.kind).</summary>
        public static readonly string[] Kinds = { "work", "stores", "order", "spirit", "faith", "world" };

        private enum Filter { All, Ready, InForce, Asked, Needed, Cooldown }
        private enum Sort { Kind, Wanted, Soon }

        private static Filter filter = PageMemory.GetEnum("decrees.filter", Filter.All);
        private static Sort sort = PageMemory.GetEnum("decrees.sort", Sort.Kind);
        private static bool glance = PageMemory.GetBool("decrees.glance", true);
        private static readonly Dictionary<string, bool> folded = new Dictionary<string, bool>();

        /// <summary>A chip was clicked: the page scrolls to the card and marks it for a moment.</summary>
        private static DecreeDef scrollTo, marked;
        private static float markedAt = -10f;

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            var tracker = comp.decrees;
            // Who is at home, worked out once a second rather than every frame (the cards are, too).
            var pawns = UiCache.Get(comp, 72, () => PawnsFinder.AllMaps_FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && p.needs?.mood != null).ToList());
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
            try
            {
                float w = view.width;
                float cy = 0f;
                cy += UiKit.Paragraph(0f, cy, w, "CC_Decrees_Sub".Translate(DecreeTracker.MaxActive), UiKit.Faint) + 10f;
                if (ColonyCouncilMod.Settings.offices) cy += DrawOffices(comp, w, cy) + 14f;

                // The decrees that are listed at all, each laid out (once a second) so the filters and the chips can read them.
                var listed = new List<(DecreeDef def, Layout l)>();
                foreach (var def in Ordered)
                {
                    // Another era's, another faith's, or a missing mod's decree isn't listed (unless it's somehow in force).
                    if (def.Hidden && !tracker.IsActive(def)) continue;
                    listed.Add((def, LayoutOf(def, comp, pawns, w)));
                }
                int ready = listed.Count(x => x.l.ready);
                cy += UiKit.SectionTitle(0f, cy, w, "CC_Decrees_Header".Translate(), Icons.Decree, null,
                    "CC_Decrees_Listed".Translate(listed.Count, ready, tracker.active.Count).Resolve());
                cy += Toolbar(0f, cy, w) + 6f;
                if (glance) cy += Glance(listed, 0f, cy, w, pawns.Count, tracker) + 10f;

                var shown = listed.Where(x => Passes(x.l)).ToList();
                if (shown.Count == 0) cy += UiKit.Paragraph(8f, cy, w - 16f, "CC_Decrees_NoneFit".Translate(), UiKit.Dim) + 8f;
                else if (sort == Sort.Kind)
                {
                    foreach (var kind in Kinds)
                    {
                        var part = shown.Where(x => x.def.kind == kind).ToList();
                        if (part.Count == 0) continue;
                        bool fold = folded.TryGetValue(kind, out var f) && f;
                        cy += KindTitle(kind, part, 0f, cy, w, ref fold) + 4f;
                        folded[kind] = fold;
                        if (fold) continue;
                        foreach (var (def, l) in part) cy += Card(def, l, comp, pawns, w, cy, outRect.height) + 8f;
                        cy += 6f;
                    }
                    // A kind the defs name that the page doesn't: listed last rather than lost.
                    var odd = shown.Where(x => !Kinds.Contains(x.def.kind)).ToList();
                    foreach (var (def, l) in odd) cy += Card(def, l, comp, pawns, w, cy, outRect.height) + 8f;
                }
                else
                {
                    var sorted = sort == Sort.Wanted
                        ? shown.OrderByDescending(x => x.l.active).ThenByDescending(x => x.l.net).ThenBy(x => x.def.order)
                        : shown.OrderByDescending(x => x.l.active).ThenByDescending(x => x.l.ready).ThenBy(x => x.l.readyIn).ThenBy(x => x.def.order);
                    foreach (var (def, l) in sorted) cy += Card(def, l, comp, pawns, w, cy, outRect.height) + 8f;
                }
                height = Mathf.Max(cy + 10f, 100f);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private static List<DecreeDef> ordered;
        private static int orderedGeneration = -1;

        /// <summary>The decrees in their order on the page (the defs never change while playing, so sorted once a game).</summary>
        private static List<DecreeDef> Ordered
        {
            get
            {
                if (ordered == null || orderedGeneration != DefRefs.Generation)
                {
                    ordered = DefDatabase<DecreeDef>.AllDefsListForReading.OrderBy(d => d.order).ToList();
                    orderedGeneration = DefRefs.Generation;
                }
                return ordered;
            }
        }

        // ---------------- The toolbar: filters and order ----------------

        private static bool Passes(Layout l)
        {
            switch (filter)
            {
                case Filter.Ready: return l.ready;
                case Filter.InForce: return l.active;
                case Filter.Asked: return l.asked;
                case Filter.Needed: return l.needed;
                case Filter.Cooldown: return !l.active && !l.ready;
                default: return true;
            }
        }

        private static float Toolbar(float x, float y, float w)
        {
            float cx = x + 4f;
            float py = y + 4f;
            Text.Font = GameFont.Tiny;
            string show = "CC_Decrees_Show".Translate();
            float showW = Text.CalcSize(show).x + 4f;
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(cx, py - 1f, showW, 20f), show);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            cx += showW + 6f;
            foreach (Filter f in System.Enum.GetValues(typeof(Filter)))
            {
                string label = ("CC_Decrees_Filter_" + f).Translate();
                var color = f == Filter.InForce ? Good : f == Filter.Asked ? Asked : f == Filter.Needed ? UiKit.Info : f == Filter.Cooldown ? UiKit.Faint : Color.white;
                float pw = PillButton(cx, py, label, color, filter == f, ("CC_Decrees_Filter_" + f + "_Tip").Translate());
                if (pw < 0f)
                {
                    filter = f;
                    PageMemory.SetEnum("decrees.filter", f);
                    pw = -pw;
                }
                cx += pw + 4f;
            }
            // The order, from the right.
            float rx = x + w - 4f;
            var sorts = (Sort[])System.Enum.GetValues(typeof(Sort));
            for (int i = sorts.Length - 1; i >= 0; i--)
            {
                var s = sorts[i];
                string label = ("CC_Decrees_Sort_" + s).Translate();
                rx -= UiKit.PillWidth(label);
                float pw = PillButton(rx, py, label, UiKit.Gold, sort == s, ("CC_Decrees_Sort_" + s + "_Tip").Translate());
                if (pw < 0f)
                {
                    sort = s;
                    PageMemory.SetEnum("decrees.sort", s);
                }
                rx -= 4f;
            }
            Text.Font = GameFont.Tiny;
            string order = "CC_Decrees_Order".Translate();
            float ow = Text.CalcSize(order).x + 6f;
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(rx - ow, py - 1f, ow, 20f), order);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            return UiKit.PillHeight + 8f;
        }

        /// <summary>A pill that can be clicked: drawn fuller when on. Returns its width, negative when it was clicked.</summary>
        private static float PillButton(float x, float y, string label, Color color, bool on, string tip = null)
        {
            float pw = UiKit.PillWidth(label);
            var r = new Rect(x, y, pw, UiKit.PillHeight);
            if (on) Widgets.DrawBoxSolid(r, UiKit.WithAlpha(color, 0.25f));
            else if (Mouse.IsOver(r)) Widgets.DrawBoxSolid(r, UiKit.WithAlpha(color, 0.1f));
            UiKit.Pill(x, y, label, on ? color : UiKit.WithAlpha(color, 0.7f), null, tip);
            if (Widgets.ButtonInvisible(r))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                return -pw;
            }
            return pw;
        }

        // ---------------- At a glance: a chip per decree ----------------

        private static float Glance(List<(DecreeDef def, Layout l)> listed, float x, float y, float w, int pawnCount, DecreeTracker tracker)
        {
            float start = y;
            // The title with its fold.
            var fold = new Rect(x + 2f, y + 4f, 18f, 18f);
            if (Widgets.ButtonImage(fold, glance ? TexButton.Collapse : TexButton.Reveal))
            {
                glance = !glance;
                PageMemory.SetBool("decrees.glance", glance);
            }
            Text.Font = GameFont.Small;
            GUI.color = UiKit.HeaderColor;
            Widgets.Label(new Rect(x + 24f, y + 2f, w - 24f, 24f), "CC_Decrees_Glance".Translate());
            GUI.color = Color.white;
            TooltipHandler.TipRegion(new Rect(x, y, 240f, 24f), "CC_Decrees_GlanceTip".Translate());
            y += 26f;
            if (!glance) return y - start;

            int perRow = Mathf.Clamp(Mathf.FloorToInt((w + 6f) / 196f), 2, 6);
            float chipW = (w - 6f * (perRow - 1)) / perRow;
            int i = 0;
            foreach (var (def, l) in listed)
            {
                int col = i % perRow, row = i / perRow;
                var r = new Rect(x + col * (chipW + 6f), y + row * (ChipH + 6f), chipW, ChipH);
                Chip(def, l, r, pawnCount);
                i++;
            }
            int rows = (listed.Count + perRow - 1) / perRow;
            y += rows * (ChipH + 6f);
            return y - start;
        }

        private static void Chip(DecreeDef def, Layout l, Rect r, int pawnCount)
        {
            var status = l.active ? Good : l.asked ? Asked : l.ready ? Color.white : UiKit.Dim;
            bool hot = marked == def && Time.realtimeSinceStartup - markedAt < 2.5f;
            UiKit.Card(r, l.active || l.asked ? status : (Color?)null, hot, true);
            Widgets.DrawBoxSolid(new Rect(r.x + 1f, r.y + 1f, 3f, r.height - 2f), status);
            Text.Font = GameFont.Tiny;
            UiKit.LabelFit(new Rect(r.x + 8f, r.y + 2f, r.width - 12f, 16f), def.LabelCap, l.active ? Good : l.ready || l.asked ? Color.white : UiKit.Faint, GameFont.Tiny);
            Text.Font = GameFont.Small;
            SupportBar(new Rect(r.x + 8f, r.y + 21f, r.width - 16f, 7f), l, pawnCount);
            string tip = def.LabelCap + "\n" + l.supportLine + "\n" + (l.active ? "CC_UI_Pill_InForce".Translate().ToString() : l.ready ? "CC_Decrees_Filter_Ready".Translate().ToString() : l.why ?? "")
                + "\n\n" + "CC_Decrees_ChipTip".Translate();
            TooltipHandler.TipRegion(r, tip);
            if (Widgets.ButtonInvisible(r))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                scrollTo = def;
                marked = def;
                markedAt = Time.realtimeSinceStartup;
                // A folded kind, or a filter that hides it, would keep the card out of sight.
                if (sort == Sort.Kind) folded[def.kind] = false;
                if (!Passes(l))
                {
                    filter = Filter.All;
                    PageMemory.SetEnum("decrees.filter", filter);
                }
            }
        }

        /// <summary>How the colony stands on it: a bar of who loves, likes, doesn't mind, dislikes and hates it, by head.</summary>
        private static void SupportBar(Rect r, Layout l, int pawnCount)
        {
            Widgets.DrawBoxSolid(r, UiKit.MeterBack);
            if (pawnCount <= 0) return;
            int neutral = Mathf.Max(0, pawnCount - l.counts.Sum());
            var parts = new (int n, Color c)[]
            {
                (l.counts[0], UiKit.ReactionColor(DecreeReaction.Love)), (l.counts[1], UiKit.ReactionColor(DecreeReaction.Like)),
                (neutral, UiKit.Neutral), (l.counts[2], UiKit.ReactionColor(DecreeReaction.Dislike)), (l.counts[3], UiKit.ReactionColor(DecreeReaction.Hate))
            };
            float x = r.x;
            foreach (var (n, c) in parts)
            {
                if (n == 0) continue;
                float pw = r.width * n / pawnCount;
                Widgets.DrawBoxSolid(new Rect(x, r.y, Mathf.Max(1f, pw - 1f), r.height), c);
                x += pw;
            }
        }

        // ---------------- Kinds ----------------

        private static float KindTitle(string kind, List<(DecreeDef def, Layout l)> part, float x, float y, float w, ref bool fold)
        {
            var fr = new Rect(x + 2f, y + 6f, 18f, 18f);
            if (Widgets.ButtonImage(fr, fold ? TexButton.Reveal : TexButton.Collapse)) fold = !fold;
            int inForce = part.Count(p => p.l.active), ready = part.Count(p => p.l.ready), asked = part.Count(p => p.l.asked);
            var bits = new List<string>();
            if (inForce > 0) bits.Add("CC_Decrees_KindInForce".Translate(inForce).Resolve());
            if (ready > 0) bits.Add("CC_Decrees_KindReady".Translate(ready).Resolve());
            if (asked > 0) bits.Add("CC_Decrees_KindAsked".Translate(asked).Resolve());
            string right = bits.Count > 0 ? string.Join(", ", bits) : "CC_Decrees_KindNone".Translate().Resolve();
            float h = UiKit.SectionTitle(x + 24f, y, w - 24f, ("CC_Decrees_Kind_" + kind).Translate() + " (" + part.Count + ")", null, UiKit.HeaderColor, right);
            TooltipHandler.TipRegion(new Rect(x, y, 260f, h), ("CC_Decrees_Kind_" + kind + "_Tip").Translate());
            // The whole title folds and unfolds too.
            if (Widgets.ButtonInvisible(new Rect(x + 24f, y, 240f, h))) fold = !fold;
            return h;
        }

        // ---------------- Decree cards ----------------

        /// <summary>A card's text, the people for each reaction, and its measurements; worked out about once a second.</summary>
        private class Layout
        {
            public float width = -1f, at, titleH, effectsH, noteH, needH, warnH, facesH, leftH, height;
            public string effects, note, need, warning, supportLine;
            public List<Pawn>[] faces = new List<Pawn>[4];
            public int[] counts = new int[4];
            /// <summary>Each reaction's pill and its tooltip ("Loves it: ...").</summary>
            public string[] reactionLabels = new string[4], reactionTips = new string[4];
            /// <summary>Why it can't be enacted (null if it can), whether it can be forced, and the buttons' texts.</summary>
            public string why, enactLabel, forceTip, chance;
            public bool forcible, active, ready, asked, needed;
            /// <summary>For the orders: how the colony stands on it (loves twice, hates twice), and ticks until its cooldown is over.</summary>
            public int net, readyIn;
        }

        private static readonly Dictionary<DecreeDef, Layout> layouts = new Dictionary<DecreeDef, Layout>();

        private static float TextWidth(float width) => Mathf.Max(100f, width - TextX - ButtonsW - 10f);
        private static float LeftWidth(float width) => Mathf.Max(100f, TextWidth(width) - FacesW - 10f);

        private static Layout LayoutOf(DecreeDef def, CouncilGameComponent comp, List<Pawn> pawns, float width)
        {
            float now = Time.realtimeSinceStartup;
            var tracker = comp.decrees;
            var active = tracker.Get(def);
            // Enacted or repealed since: the card is laid out again at once.
            if (layouts.TryGetValue(def, out var l) && Mathf.Abs(l.width - width) < 0.5f && now - l.at < 1f && l.active == (active != null)) return l;
            l = new Layout { width = width, at = now, active = active != null };
            l.effects = string.Join("\n", def.effects.Select(e => "- " + e));
            // Feelings and strain (Heart/): a hard decree wears on those who resent it.
            if (def.strainPerDay > 0f && Heart.On) l.effects += "\n- " + "CC_Heart_DecreeStrain".Translate(def.strainPerDay.ToString("0.#")).Resolve();
            // One faith for the colony: which faith it names (Decrees/DecreeFaith.cs).
            string faithLine = DecreeFaith.CardLine(def);
            if (faithLine != null) l.effects += "\n- " + faithLine;
            var req = tracker.RequestFor(def);
            l.asked = req?.pawn != null;
            l.note = req?.pawn == null ? null
                : (req.repeal ? "CC_Decree_RepealLine" : "CC_Decree_PetitionLine").Translate(req.pawn.Named("PAWN")).Resolve() + (req.line != null ? " \"" + req.line + "\"" : "");
            int fatigue = active == null ? tracker.Fatigue(def) : 0;
            // Needed now and wanted: it can come again sooner, and a repeat is minded less.
            l.need = active == null && (tracker.OnCooldown(def) || fatigue > 0) ? NeedLine(tracker, def, fatigue) : null;
            if (active == null)
            {
                var n = tracker.NeedOf(def);
                l.needed = n.understanding >= 0.1f && n.holding.Count > 0;
            }
            l.warning = fatigue > 0
                ? "CC_Decree_FatigueWarn".Translate((fatigue == 1 ? "CC_Decree_UsedOnce".Translate() : "CC_Decree_UsedTimes".Translate(fatigue)).Named("TIMES"),
                    Mathf.RoundToInt(DecreeTracker.FatigueDays).Named("DAYS"), tracker.UnrestChance(def, tracker.OnCooldown(def)).ToStringPercent().Named("CHANCE"),
                    TextUtil.DaysString(tracker.TicksUntilFresh(def)).Named("SAFE")).Resolve()
                : null;
            for (int i = 0; i < Reactions.Length; i++)
            {
                var r = Reactions[i];
                l.faces[i] = pawns.Where(p => def.ReactionOf(p) == r).ToList();
                l.counts[i] = l.faces[i].Count;
                l.reactionLabels[i] = ("CC_UI_Reaction_" + r).Translate();
                l.reactionTips[i] = l.faces[i].Count > 0 ? ReactionLine(r, l.faces[i]) : null;
            }
            l.net = l.counts[0] * 2 + l.counts[1] - l.counts[2] - l.counts[3] * 2;
            int forIt = l.counts[0] + l.counts[1], against = l.counts[2] + l.counts[3];
            l.supportLine = "CC_Decrees_Support".Translate(forIt, against, Mathf.Max(0, pawns.Count - forIt - against)).Resolve();
            if (active == null)
            {
                l.why = tracker.CannotEnact(def);
                l.ready = l.why == null;
                l.readyIn = tracker.TicksUntilReady(def);
                if (l.why == null) l.enactLabel = "CC_Decree_Enact".Translate(TextUtil.DaysString(Mathf.RoundToInt(def.durationDays * GenDate.TicksPerDay)));
                else
                {
                    l.forcible = tracker.CannotEnact(def, true) == null;
                    if (l.forcible)
                    {
                        l.chance = tracker.UnrestChance(def, true).ToStringPercent();
                        l.forceTip = "CC_Decree_ForceTip".Translate(l.chance);
                    }
                }
            }
            float leftW = LeftWidth(width);
            Text.Font = GameFont.Small;
            l.titleH = 26f;
            l.effectsH = Text.CalcHeight(l.effects, leftW);
            l.noteH = l.note == null ? 0f : Text.CalcHeight(l.note, leftW) + 2f;
            l.needH = l.need == null ? 0f : Text.CalcHeight(l.need, leftW) + 2f;
            l.warnH = l.warning == null ? 0f : Text.CalcHeight(l.warning, leftW) + 2f;
            l.facesH = Faces(l, 0f, 0f, FacesW, false);
            l.leftH = l.noteH + l.needH + l.warnH + l.effectsH + (active != null ? 4f + UiKit.MeterHeight : 0f);
            float h = 8f + l.titleH + 14f + Mathf.Max(l.leftH, l.facesH) + 8f;
            l.height = Mathf.Max(h, 74f);
            layouts[def] = l;
            return l;
        }

        /// <summary>
        /// "Needed now: the home is filthy and many are hurt. Most want it, so it can come again sooner and a repeat is
        /// minded less." Null when it isn't needed enough to make a difference.
        /// </summary>
        private static string NeedLine(DecreeTracker tracker, DecreeDef def, int fatigue)
        {
            var n = tracker.NeedOf(def);
            if (n.understanding < 0.1f || n.holding.Count == 0) return null;
            var why = n.holding.Select(h => ("CC_Decree_Need_" + h.moment + (h.strength >= DecreeMath.HoldsStrongly ? "_Strong" : "")).Translate().Resolve()).ToList();
            string wanted = (n.want >= 0.5f ? "CC_Decree_NeedWantedMost" : n.want >= 0.2f ? "CC_Decree_NeedWantedSome" : "CC_Decree_NeedWantedFew").Translate().Resolve();
            string line = "CC_Decree_NeedLine".Translate(TextUtil.JoinAnd(why), wanted).Resolve();
            if (fatigue >= 2) line += " " + "CC_Decree_NeedPatience".Translate(fatigue).Resolve();
            return line;
        }

        /// <summary>One card, drawn only when it's in view; the page scrolls to it when its chip was clicked. Returns its height.</summary>
        private static float Card(DecreeDef def, Layout l, CouncilGameComponent comp, List<Pawn> pawns, float width, float y, float viewH)
        {
            if (scrollTo == def)
            {
                scroll.y = Mathf.Max(0f, y - 8f);
                scrollTo = null;
            }
            if (y + l.height >= scroll.y && y <= scroll.y + viewH) DrawDecree(def, l, comp, pawns, width, y);
            return l.height;
        }

        private static void DrawDecree(DecreeDef def, Layout l, CouncilGameComponent comp, List<Pawn> pawns, float width, float y)
        {
            var tracker = comp.decrees;
            var active = tracker.Get(def);
            var req = tracker.RequestFor(def);
            var accent = active != null ? Good : req != null ? Asked : l.warning != null ? Bad : (Color?)null;
            var box = new Rect(0f, y, width, l.height);
            bool hot = marked == def && Time.realtimeSinceStartup - markedAt < 2.5f;
            UiKit.Card(box, accent, hot, false);
            if (active != null) Widgets.DrawBoxSolid(box.ContractedBy(1f), new Color(0.3f, 0.45f, 0.3f, 0.12f));
            UiKit.Badge(new Rect(IconX, y + 10f, 32f, 32f), Icons.Decree, active != null ? Good : req != null ? Asked : UiKit.Faint);

            float textX = TextX;
            float textW = TextWidth(width);
            float leftW = LeftWidth(width);
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

            // How the colony stands on it, in one bar across the text.
            var bar = new Rect(textX, cy + 2f, Mathf.Min(leftW, 260f), 8f);
            SupportBar(bar, l, pawns.Count);
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(bar.xMax + 8f, cy - 3f, textW - bar.width - 8f, 18f), l.supportLine);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(new Rect(textX, cy - 2f, textW, 16f), string.Join("\n", l.reactionTips.Where(t => t != null)));
            cy += 14f;

            // Left: the lines and what it does; right: who minds.
            float ly = cy;
            if (l.note != null) ly += UiKit.Paragraph(textX, ly, leftW, l.note, Asked) + 2f;
            if (l.need != null) ly += UiKit.Paragraph(textX, ly, leftW, l.need, Good) + 2f;
            if (l.warning != null) ly += UiKit.Paragraph(textX, ly, leftW, l.warning, Bad) + 2f;
            Widgets.Label(new Rect(textX, ly, leftW, l.effectsH), l.effects);
            ly += l.effectsH;
            if (active != null)
            {
                ly += 4f;
                int total = Mathf.Max(1, active.endTick - active.startTick);
                UiKit.Countdown(new Rect(textX, ly, Mathf.Min(leftW, 360f), UiKit.MeterHeight), active.endTick - Find.TickManager.TicksGame, total, "CC_UI_TimeLeft".Translate());
            }
            Faces(l, textX + leftW + 10f, cy, FacesW, true);

            DrawButtons(def, l, comp, pawns, width, y);
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
                string label = l.reactionLabels[i] ?? ("CC_UI_Reaction_" + Reactions[i]).Translate();
                float pw = UiKit.PillWidth(label);
                if (cx > x && cx + pw + FaceSize > x + width)
                {
                    cx = x;
                    cy += rowH;
                }
                if (draw)
                {
                    string tip = l.reactionTips[i] ?? ReactionLine(Reactions[i], people);
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

        private static void DrawButtons(DecreeDef def, Layout l, CouncilGameComponent comp, List<Pawn> pawns, float width, float y)
        {
            var tracker = comp.decrees;
            var active = tracker.Get(def);
            var btn = new Rect(width - ButtonsW + 4f, y + 10f, ButtonsW - 14f, 32f);
            if (active != null)
            {
                if (Widgets.ButtonText(btn, "CC_Decree_Repeal".Translate())) tracker.Repeal(def, comp, true);
                return;
            }
            // What stands in the way is worked out with the card, once a second (the card's layout); a click asks again.
            string why = l.why;
            if (why == null)
            {
                if (Widgets.ButtonText(btn, UiKit.Fit(l.enactLabel ?? "CC_Decree_Enact".Translate(TextUtil.DaysString(Mathf.RoundToInt(def.durationDays * GenDate.TicksPerDay))), btn.width - 10f)))
                    DecreeFaith.Enact(def, () => tracker.Enact(def, comp));
                return;
            }
            // Only the cooldown stands in the way: it can be forced through, at a price.
            bool forcible = l.forcible;
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
            string chance = l.chance ?? tracker.UnrestChance(def, true).ToStringPercent();
            TooltipHandler.TipRegion(forceBtn, l.forceTip ?? "CC_Decree_ForceTip".Translate(chance));
            if (Widgets.ButtonText(forceBtn, "CC_Decree_Force".Translate()))
            {
                var against = pawns.Where(p => def.ReactionOf(p) <= DecreeReaction.Dislike).Select(p => p.LabelShort).ToList();
                string who = against.Count > 0 ? string.Join(", ", against) : "CC_Decree_ForceNobody".Translate().ToString();
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "CC_Decree_ForceConfirm".Translate(def.label.Named("DECREE"), who.Named("WHO"), chance.Named("CHANCE")).Resolve(),
                    () => DecreeFaith.Enact(def, () => tracker.Enact(def, comp, true)), true));
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
                        .OrderByDescending(p => office.CanHold(p))
                        .ThenByDescending(p => office.Competence(p))
                        .Select(p => office.CanHold(p)
                            ? new FloatMenuOption(p.LabelShort + " (" + "CC_Office_Competence".Translate(office.Competence(p).ToString("0")) + ")", () => comp.offices.Appoint(office, p, comp, seatNow))
                            : new FloatMenuOption(p.LabelShort + " (" + "CC_Office_CantHold".Translate() + ")", null))
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
