using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The world events running now, easy to see (the player, 15 November 2026: "do i still see current world events
    /// somewhere?"; they were in the region window's History, on the region map and on the peoples' cards). A line each in
    /// the corner on the right of the screen, just above the letters where the evening's Colony news comes in, in the
    /// alerts' column (the alerts, the game's or Rimfolk's panel, move up to make room): its name and where, as the colony
    /// heard of it. Resting on a line says what it is and how it has gone (the History page's chips' own words,
    /// <see cref="WorldHistoryPage.RunningTip"/>); a click opens the region window where it is (the region map, the place
    /// picked), or on its History page when it has no place. Three lines at most, the last "and N more" past that
    /// (<see cref="WorldEventsCornerMath"/>). Nothing while none runs, with world events off, or with the setting "World
    /// events in the corner" off. No world event names a god, so none is named here. Built at most every two seconds, so
    /// drawing asks nothing of the game.
    /// </summary>
    public static class WorldEventsCorner
    {
        /// <summary>The alerts' column: as wide as theirs, against the right edge.</summary>
        private const float Width = 154f, TextWidth = 128f, RowH = 26f, RebuildSeconds = 2f;

        private sealed class Line
        {
            public string text, tip;
            /// <summary>Where a click goes: the place on the region map; none, the region window's History.</summary>
            public ThingRef place;
            public float h;
        }

        private static readonly List<Line> lines = new List<Line>();
        private static float builtAt = -99f;
        private static AccessTools.FieldRef<LetterStack, float> lastTop;
        private static bool failed;

        private static bool On
        {
            get
            {
                var s = ColonyCouncilMod.Settings;
                return s != null && s.worldSituations && s.worldEventsCorner && WorldSituations.Instance != null;
            }
        }

        /// <summary>After the letters are drawn: the lines just above them, and the letters' top raised past the lines so what stacks above them (the alerts) makes room.</summary>
        internal static void Draw(LetterStack stack)
        {
            if (failed || stack == null || Current.ProgramState != ProgramState.Playing || !On) return;
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            try
            {
                lastTop ??= AccessTools.FieldRefAccess<LetterStack, float>("lastTopYInt");
                Text.Font = GameFont.Small;
                if (Time.realtimeSinceStartup - builtAt > RebuildSeconds || builtAt > Time.realtimeSinceStartup) Build();
                if (lines.Count == 0) return;
                float total = 0f;
                foreach (var l in lines) total += l.h;
                float top = stack.LastTopY - total - 4f;
                var block = new Rect(UI.screenWidth - Width, top, Width, total);
                float dark = GenUI.BackgroundDarkAlphaForText();
                if (dark > 0.001f)
                {
                    GUI.color = UiKit.WithAlpha(Color.white, dark);
                    Widgets.DrawShadowAround(block);
                    GUI.color = Color.white;
                }
                float y = top;
                foreach (var l in lines)
                {
                    var r = new Rect(block.x, y, Width, l.h);
                    UiKit.Icon(new Rect(r.x + 4f, r.y + (r.height - 16f) / 2f, 16f, 16f), Icons.WorldEvent, UiKit.WorldColor);
                    Text.Anchor = TextAnchor.MiddleRight;
                    Widgets.Label(new Rect(r.xMax - 6f - TextWidth, r.y, TextWidth, r.height), l.text);
                    Text.Anchor = TextAnchor.UpperLeft;
                    if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);
                    TooltipHandler.TipRegion(r, l.tip);
                    if (Widgets.ButtonInvisible(r, false)) Go(l);
                    y += l.h;
                }
                lastTop(stack) = top;
            }
            catch (Exception e)
            {
                failed = true;
                CouncilLog.ErrorOnce("world events corner", e);
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = Color.white;
            }
        }

        /// <summary>The lines, oldest event first (as the History page's strip has them).</summary>
        private static void Build()
        {
            builtAt = Time.realtimeSinceStartup;
            lines.Clear();
            var running = WorldSituations.Instance?.Running.OrderBy(s => s.startTick).ToList();
            if (running == null || running.Count == 0) return;
            int own = WorldEventsCornerMath.OwnLines(running.Count), more = WorldEventsCornerMath.More(running.Count);
            for (int i = 0; i < own; i++)
            {
                var s = running[i];
                string where = s.ctx?.placeName;
                string text = where.NullOrEmpty() ? s.Label : "CC_WorldCorner_LineAt".Translate(s.Label.Named("LABEL"), where.Named("PLACE")).Resolve();
                var place = !s.place.IsNone ? ThingRef.From(s.place) : ThingRef.None;
                if (!place.IsPlace) place = ThingRef.None;
                string click = (place.IsNone ? "CC_WorldCorner_ClickHistory" : "CC_WorldCorner_ClickPlace").Translate();
                Add(text, WorldHistoryPage.RunningTip(s) + "\n\n" + click.Colorize(ColoredText.SubtleGrayColor), place);
            }
            if (more > 0)
            {
                var rest = running.Skip(own).Select(s => s.Label).ToList();
                Add("CC_WorldCorner_More".Translate(more).Resolve(),
                    "CC_WorldCorner_MoreTip".Translate(TextUtil.JoinAnd(rest)).Resolve() + "\n\n" + "CC_WorldCorner_ClickHistory".Translate().ToString().Colorize(ColoredText.SubtleGrayColor),
                    ThingRef.None);
            }
        }

        private static void Add(string text, string tip, ThingRef place) =>
            lines.Add(new Line { text = text, tip = tip, place = place, h = Mathf.Max(RowH, Text.CalcHeight(text, TextWidth) + 4f) });

        private static void Go(Line l)
        {
            Cards.Click();
            if (!l.place.IsNone) Cards.ShowOnMap(l.place);
            else Pages.Open("WorldHistory");
        }
    }

    /// <summary>The world events running now, a line each above the letters (UI/WorldEventsCorner.cs).</summary>
    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.LettersOnGUI))]
    public static class Patch_WorldEventsCorner
    {
        public static void Postfix(LetterStack __instance) => WorldEventsCorner.Draw(__instance);
    }
}
