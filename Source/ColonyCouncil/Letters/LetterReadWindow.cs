using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A small window to read an exchange of letters again (the player, 9 November 2026), in Rimfolk's frame (key
    /// "LetterRead"): each letter on a sheet of paper in the order written (the colony's, then theirs; or theirs first, the
    /// colony's reply, their thanks), who wrote it and when above it, the narrator's lines in a quieter hand under theirs. A
    /// call reads as its transcript, words passed on as words. Opened from the answer's event card, the Diplomacy screen's
    /// people page and the mailbag. Several may be open at once; each built once and redrawn from what it kept.
    /// </summary>
    public class LetterReadWindow : FramedWindow
    {
        private readonly LetterExchange exchange;
        private readonly List<(string head, string text, string note, bool ours)> sheets = new List<(string, string, string, bool)>();
        private readonly string title;
        private Vector2 scroll;
        private float height = 400f, laidW = -1f;
        private readonly List<float> textH = new List<float>(), noteH = new List<float>();

        /// <summary>The paper: a warm cream with dark ink, in either look.</summary>
        private static readonly Color Paper = new Color(0.93f, 0.89f, 0.80f), PaperEdge = new Color(0.62f, 0.55f, 0.43f), Ink = new Color(0.17f, 0.14f, 0.11f);

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
            if (x == null) return;
            foreach (var p in x.pages)
            {
                if (p == null || p.text.NullOrEmpty()) continue;
                string date = Date(p.tick);
                string key = p.ours ? p.shape == (byte)LetterShape.Spoken ? "CC_LetterRead_OursSpoken" : "CC_LetterRead_Ours"
                    : p.shape == (byte)LetterShape.Call ? "CC_LetterRead_Call" : p.shape == (byte)LetterShape.Spoken ? "CC_LetterRead_TheirsSpoken" : "CC_LetterRead_Theirs";
                sheets.Add((key.Translate((p.who ?? "?").Named("PAWN"), date.Named("DATE")).Resolve(), p.text, p.ours ? null : p.note, p.ours));
            }
            if (x.Ours == null || x.pages.Count < 2) sheets.Add((null, null, x.Ours != null ? "CC_LetterRead_NoAnswerYet".Translate().Resolve() : null, false));
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
            var outRect = new Rect(inRect.x, top, inRect.width, inRect.yMax - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            float pad = 16f, textW = view.width - pad * 2f;
            if (Math.Abs(laidW - textW) > 0.5f) Lay(textW);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float y = 0f;
                for (int i = 0; i < sheets.Count; i++)
                {
                    var (head, text, note, ours) = sheets[i];
                    if (!head.NullOrEmpty())
                    {
                        UiKit.LabelFit(new Rect(0f, y, view.width, 20f), head, UiKit.Faint, GameFont.Tiny);
                        y += 20f;
                    }
                    if (!text.NullOrEmpty())
                    {
                        // The sheet: the colony's set a little in from the right, theirs from the left, as letters lie on a table.
                        float inset = ours ? 0f : 10f;
                        var sheet = new Rect(inset, y, view.width - 10f, textH[i] + pad * 2f);
                        Widgets.DrawBoxSolid(sheet, Paper);
                        Widgets.DrawBox(sheet, 1);
                        GUI.color = PaperEdge;
                        Widgets.DrawBox(sheet.ContractedBy(3f), 1);
                        GUI.color = Ink;
                        Text.Font = GameFont.Small;
                        Widgets.Label(new Rect(sheet.x + pad, sheet.y + pad, textW - 10f, textH[i]), text);
                        GUI.color = Color.white;
                        y = sheet.yMax + 6f;
                    }
                    if (!note.NullOrEmpty())
                    {
                        Text.Font = GameFont.Tiny;
                        GUI.color = UiKit.Faint;
                        Widgets.Label(new Rect(10f, y, view.width - 10f, noteH[i]), note);
                        GUI.color = Color.white;
                        Text.Font = GameFont.Small;
                        y += noteH[i] + 6f;
                    }
                    y += 12f;
                }
                height = Mathf.Max(y, 50f);
            }
            finally
            {
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                Widgets.EndScrollView();
            }
        }

        /// <summary>Each sheet's and note's height at this width, worked out once a width.</summary>
        private void Lay(float textW)
        {
            laidW = textW;
            textH.Clear();
            noteH.Clear();
            foreach (var (_, text, note, _) in sheets)
            {
                Text.Font = GameFont.Small;
                textH.Add(text.NullOrEmpty() ? 0f : Text.CalcHeight(text, textW - 10f));
                Text.Font = GameFont.Tiny;
                noteH.Add(note.NullOrEmpty() ? 0f : Text.CalcHeight(note, textW));
            }
            Text.Font = GameFont.Small;
        }
    }
}
