using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The paper a letter is drawn on, in the reading window and on a received letter's event card (UI/EventCards/): a warm
    /// cream with dark ink in either look, a double edge, the words set in from it.
    /// </summary>
    internal static class LetterPaper
    {
        public static readonly Color Paper = new Color(0.93f, 0.89f, 0.80f), Edge = new Color(0.62f, 0.55f, 0.43f), Ink = new Color(0.17f, 0.14f, 0.11f),
            InkFaint = new Color(0.17f, 0.14f, 0.11f, 0.6f);

        /// <summary>The words set in from the paper's edge.</summary>
        public const float Pad = 16f;

        /// <summary>The words' height on a sheet this wide (Small).</summary>
        public static float TextHeight(string text, float sheetW)
        {
            Text.Font = GameFont.Small;
            return string.IsNullOrEmpty(text) ? 0f : Text.CalcHeight(text, sheetW - Pad * 2f);
        }

        /// <summary>A sheet this wide with these words: their height and the padding round them.</summary>
        public static float Height(string text, float sheetW) => TextHeight(text, sheetW) + Pad * 2f;

        /// <summary>The sheet, and the words on it in ink (<paramref name="textH"/> as measured; the sheet may be taller).</summary>
        public static void Draw(Rect sheet, string text, float textH)
        {
            Widgets.DrawBoxSolid(sheet, Paper);
            Widgets.DrawBox(sheet, 1);
            GUI.color = Edge;
            Widgets.DrawBox(sheet.ContractedBy(3f), 1);
            GUI.color = Ink;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(sheet.x + Pad, sheet.y + Pad, sheet.width - Pad * 2f, textH), text);
            GUI.color = Color.white;
        }
    }

    /// <summary>
    /// A small window to read an exchange of letters again (the player, 9 November 2026), in Rimfolk's frame (key
    /// "LetterRead"): each letter on a sheet of paper in the order written (the colony's, then theirs; or theirs first, the
    /// colony's reply, their thanks), who wrote it and when above it, the narrator's lines in a quieter hand under theirs. A
    /// call reads as its transcript, words passed on as words. A long letter (10 November 2026: "some could be several pages
    /// long") is read a page at a time on its sheet, "Page 1 of 3" and the turn of the page under it
    /// (LetterKindMath.Pages). Opened from the answer's event card, the Diplomacy screen's people page and the mailbag.
    /// Several may be open at once; each built once and redrawn from what it kept.
    /// </summary>
    public class LetterReadWindow : FramedWindow
    {
        private sealed class Sheet
        {
            public string head, note;
            public bool ours;
            public List<string> pages = new List<string>();
            public int page;
            public readonly List<float> textH = new List<float>();
            public float noteH;
        }

        private readonly LetterExchange exchange;
        private readonly List<Sheet> sheets = new List<Sheet>();
        private readonly string title;
        /// <summary>Who the letters are with, for "Write back" (Letters/LetterReply.cs); its reason not, looked at twice a second.</summary>
        private readonly Notable with;
        private string backWhy;
        private float backAt = -1f;

        /// <summary>The foot of the window: "Write back".</summary>
        private const float FootH = 34f;
        private Vector2 scroll;
        private float height = 400f, laidW = -1f;

        private static Color Ink => LetterPaper.Ink;
        private static Color InkFaint => LetterPaper.InkFaint;

        /// <summary>The row under a long letter's page: its number and the turn of the page.</summary>
        private const float PageRowH = 24f;

        protected override string FrameTitle => title;

        protected override Texture2D FrameIcon => Icons.Journal;

        public LetterReadWindow(LetterExchange x) : base("LetterRead", true)
        {
            exchange = x;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            forcePause = false;
            title = "CC_LetterRead_Title".Translate((x?.name ?? "?").Named("NAME"), (x?.town ?? "").Named("TOWN")).Resolve();
            with = LetterReply.Of(x);
            if (x == null) return;
            foreach (var p in x.pages)
            {
                if (p == null || p.text.NullOrEmpty()) continue;
                string date = Date(p.tick);
                string key = p.ours ? p.shape == (byte)LetterShape.Spoken ? "CC_LetterRead_OursSpoken" : "CC_LetterRead_Ours"
                    : p.shape == (byte)LetterShape.Call ? "CC_LetterRead_Call" : p.shape == (byte)LetterShape.Spoken ? "CC_LetterRead_TheirsSpoken" : "CC_LetterRead_Theirs";
                var sheet = new Sheet { head = key.Translate((p.who ?? "?").Named("PAWN"), date.Named("DATE")).Resolve(), note = p.ours ? null : p.note, ours = p.ours };
                // A long letter in pages; any other on one.
                if (p.Long) sheet.pages.AddRange(LetterKindMath.Pages(p.text));
                else sheet.pages.Add(p.text);
                sheets.Add(sheet);
            }
            if (x.Ours == null || x.pages.Count < 2) sheets.Add(new Sheet { note = x.Ours != null ? "CC_LetterRead_NoAnswerYet".Translate().Resolve() : null });
        }

        public override Vector2 InitialSize => new Vector2(560f, 680f);

        /// <summary>Opens the exchange to read (one window an exchange: a second click brings it to the front).</summary>
        public static void Open(LetterExchange x)
        {
            if (x == null) return;
            foreach (var w in Find.WindowStack.Windows)
                if (w is LetterReadWindow r && r.exchange == x)
                {
                    Find.WindowStack.Notify_ClickedInsideWindow(w);
                    return;
                }
            Find.WindowStack.Add(new LetterReadWindow(x));
        }

        private static string Date(int tick)
        {
            try
            {
                var map = HappeningTracker.HomeMap();
                float lon = map != null ? Find.WorldGrid.LongLatOf(map.Tile).x : 0f;
                return GenDate.DateFullStringAt(GenDate.TickGameToAbs(tick), new Vector2(lon, 0f));
            }
            catch
            {
                return "";
            }
        }

        protected override void DoFramedContents(Rect inRect)
        {
            float top = TitleTop(inRect, FrameTitle, 30f, 40f);
            float foot = with != null ? FootH : 0f;
            var outRect = new Rect(inRect.x, top, inRect.width, inRect.yMax - top - foot);
            if (with != null) WriteBack(new Rect(inRect.x, inRect.yMax - FootH + 6f, inRect.width, FootH - 6f));
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            float pad = LetterPaper.Pad, sheetW = view.width - 10f;
            if (Math.Abs(laidW - sheetW) > 0.5f) Lay(sheetW, view.width);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float y = 0f;
                foreach (var s in sheets)
                {
                    if (!s.head.NullOrEmpty())
                    {
                        UiKit.LabelFit(new Rect(0f, y, view.width, 20f), s.head, UiKit.Faint, GameFont.Tiny);
                        y += 20f;
                    }
                    if (s.pages.Count > 0 && !s.pages[s.page].NullOrEmpty())
                    {
                        bool paged = s.pages.Count > 1;
                        float textH = s.textH[s.page];
                        // The sheet: the colony's set a little in from the right, theirs from the left, as letters lie on a table.
                        float inset = s.ours ? 0f : 10f;
                        var sheet = new Rect(inset, y, sheetW, textH + pad * 2f + (paged ? PageRowH : 0f));
                        LetterPaper.Draw(sheet, s.pages[s.page], textH);
                        if (paged) PageRow(new Rect(sheet.x + pad, sheet.y + pad + textH + 4f, sheetW - pad * 2f, PageRowH - 4f), s);
                        y = sheet.yMax + 6f;
                    }
                    if (!s.note.NullOrEmpty())
                    {
                        Text.Font = GameFont.Tiny;
                        GUI.color = UiKit.Faint;
                        Widgets.Label(new Rect(10f, y, view.width - 10f, s.noteH), s.note);
                        GUI.color = Color.white;
                        Text.Font = GameFont.Small;
                        y += s.noteH + 6f;
                    }
                    y += 12f;
                }
                height = Mathf.Max(y, 50f);
            }
            finally
            {
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.EndScrollView();
            }
        }

        /// <summary>"Write back" at the foot, greyed with why when no letter can go to them now.</summary>
        private void WriteBack(Rect r)
        {
            if (Time.realtimeSinceStartup - backAt > 0.5f)
            {
                backAt = Time.realtimeSinceStartup;
                backWhy = LetterReply.WhyNot(with);
            }
            float bw = Mathf.Clamp(UiKit.KeptWidth(LetterReply.Label, GameFont.Small) + 28f, 110f, 220f);
            if (UiKit.Button(new Rect(r.xMax - bw, r.y, bw, r.height), LetterReply.Label, ButtonKind.Normal, backWhy == null,
                    backWhy ?? "CC_LetterRead_WriteBackTip".Translate(with.Name.Named("NAME")).Resolve()))
                LetterReply.Open(with);
        }

        /// <summary>A long letter's page row: the turn back, "Page 1 of 3", the turn on; the sheet scrolled to its top when turned.</summary>
        private void PageRow(Rect r, Sheet s)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = InkFaint;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(r, "CC_LetterRead_Page".Translate((s.page + 1).Named("N"), s.pages.Count.Named("COUNT")).Resolve());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            float w = 110f;
            var back = new Rect(r.x, r.y, w, r.height);
            var on = new Rect(r.xMax - w, r.y, w, r.height);
            if (s.page > 0 && Turn(back, "CC_LetterRead_PageBack".Translate().Resolve(), TextAnchor.MiddleLeft)) s.page--;
            if (s.page < s.pages.Count - 1 && Turn(on, "CC_LetterRead_PageOn".Translate().Resolve(), TextAnchor.MiddleRight)) s.page++;
            Text.Font = GameFont.Small;
        }

        /// <summary>A quiet turn of the page in the ink's colour, underlined on hover.</summary>
        private static bool Turn(Rect r, string label, TextAnchor anchor)
        {
            bool over = Mouse.IsOver(r);
            GUI.color = over ? Ink : InkFaint;
            Text.Anchor = anchor;
            Widgets.Label(r, label);
            Text.Anchor = TextAnchor.UpperLeft;
            if (over) Widgets.DrawLineHorizontal(anchor == TextAnchor.MiddleLeft ? r.x : r.xMax - Text.CalcSize(label).x, r.yMax - 3f, Text.CalcSize(label).x);
            GUI.color = Color.white;
            return Widgets.ButtonInvisible(r);
        }

        /// <summary>Each sheet's pages' and note's heights at this width, worked out once a width.</summary>
        private void Lay(float sheetW, float viewW)
        {
            laidW = sheetW;
            foreach (var s in sheets)
            {
                s.textH.Clear();
                foreach (var p in s.pages) s.textH.Add(LetterPaper.TextHeight(p, sheetW));
                Text.Font = GameFont.Tiny;
                s.noteH = s.note.NullOrEmpty() ? 0f : Text.CalcHeight(s.note, viewW - 10f);
            }
            Text.Font = GameFont.Small;
        }
    }
}
