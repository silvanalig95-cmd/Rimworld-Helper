using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>History → Chronicle: everything the colony has been through, newest first, with filters and the yearly annals.</summary>
    public static class ChroniclePanel
    {
        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color GoodColor = new Color(0.55f, 0.9f, 0.55f);
        private static readonly Color QuoteColor = new Color(1f, 0.93f, 0.75f);
        private static readonly Color IdeaColor = new Color(0.45f, 0.7f, 0.95f);
        private static Vector2 scroll;
        private static float lastHeight = 1000f;

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;

        private static float BeginScroll(Rect outRect)
        {
            var view = new Rect(0f, 0f, outRect.width - 18f, lastHeight);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            return view.width;
        }

        private static void EndScroll(float height)
        {
            lastHeight = Mathf.Max(height + 10f, 100f);
            Widgets.EndScrollView();
        }

        private static float WrappedLabel(float x, float y, float width, string text, Color? color = null, GameFont font = GameFont.Small) =>
            MainTabWindow_Council.WrappedLabel(x, y, width, text, color, font);

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), "CC_Chronicle_Header".Translate());
            Text.Font = GameFont.Small;
            // The yearly annals, newest first, as buttons along the header; the journals of the departed before them.
            float bx = rect.xMax;
            var departed = comp.journals.Departed().ToList();
            if (departed.Count > 0)
            {
                string dl = "CC_Journal_Departed".Translate();
                float dw = Text.CalcSize(dl).x + 20f;
                bx -= dw + 6f;
                if (Widgets.ButtonText(new Rect(bx, rect.y + 2f, dw, 28f), dl))
                    Find.WindowStack.Add(new FloatMenu(departed.Select(j => new FloatMenuOption(j.name, () => Find.WindowStack.Add(new Dialog_Journal(j)))).ToList()));
            }
            foreach (var a in comp.annals.written.AsEnumerable().Reverse().Take(6))
            {
                string label = "CC_Annals_Button".Translate(a.year);
                float w = Text.CalcSize(label).x + 20f;
                bx -= w + 6f;
                if (bx < rect.x + 200f) break;
                var entry = a;
                if (Widgets.ButtonText(new Rect(bx, rect.y + 2f, w, 28f), label))
                    Find.WindowStack.Add(new Dialog_MessageBox(entry.text, title: "CC_Annals_Title".Translate(entry.year)));
            }
            // Filters: everything, or one part of colony life.
            float fx = rect.x;
            foreach (ChronicleFilter f in System.Enum.GetValues(typeof(ChronicleFilter)))
            {
                string label = ("CC_ChronicleFilter_" + f).Translate();
                float fw = Text.CalcSize(label).x + 20f;
                var fr = new Rect(fx, rect.y + 38f, fw, 26f);
                if (f == chronicleFilter) Widgets.DrawBoxSolid(fr, new Color(1f, 1f, 1f, 0.12f));
                Widgets.DrawHighlightIfMouseover(fr);
                GUI.color = f == chronicleFilter ? Color.white : Faint;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(fr, label);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                if (Widgets.ButtonInvisible(fr))
                {
                    chronicleFilter = f;
                    scroll = Vector2.zero;
                }
                fx += fw + 4f;
            }
            var entries = new List<ChronicleEntry>();
            for (int i = comp.chronicle.Count - 1; i >= 0; i--)
                if (Matches(comp.chronicle[i].kind, chronicleFilter)) entries.Add(comp.chronicle[i]);

            var outRect = new Rect(rect.x, rect.y + 70f, rect.width, rect.height - 70f);
            float width = BeginScroll(outRect);
            float y = 0f;
            if (entries.Count == 0) y += WrappedLabel(0f, y, width, "CC_Chronicle_Empty".Translate(), Faint);
            if (chronicleWidth != width)
            {
                chronicleHeights.Clear();
                chronicleWidth = width;
            }
            foreach (var e in entries)
            {
                if (!chronicleHeights.TryGetValue(e, out float h))
                {
                    h = Text.CalcHeight(e.text, width - 115f);
                    chronicleHeights[e] = h;
                }
                // Only what's on screen is drawn; the rest just takes up its space.
                if (y + h < scroll.y || y > scroll.y + outRect.height)
                {
                    y += h + 4f;
                    continue;
                }
                Color col;
                switch (e.kind)
                {
                    case ChronicleKind.WishGranted:
                    case ChronicleKind.AmbitionAchieved:
                    case ChronicleKind.PersonalTouch:
                        col = GoodColor;
                        break;
                    case ChronicleKind.WishExpired:
                    case ChronicleKind.AmbitionFailed:
                    case ChronicleKind.AmbitionAbandoned:
                    case ChronicleKind.WishDismissed:
                        col = Faint;
                        break;
                    case ChronicleKind.Meeting:
                    case ChronicleKind.AmbitionChosen:
                    case ChronicleKind.Council:
                        col = IdeaColor;
                        break;
                    case ChronicleKind.Dream:
                        col = QuoteColor;
                        break;
                    case ChronicleKind.Loss:
                        col = new Color(0.85f, 0.7f, 0.65f);
                        break;
                    case ChronicleKind.Stranger:
                        col = new Color(0.75f, 0.85f, 0.7f);
                        break;
                    case ChronicleKind.World:
                        col = new Color(0.72f, 0.8f, 0.95f);
                        break;
                    default:
                        col = Color.white;
                        break;
                }
                GUI.color = Faint;
                Widgets.Label(new Rect(0f, y, 110f, 24f), MainTabWindow_Council.DayString(e.tick));
                GUI.color = Color.white;
                GUI.color = col;
                Widgets.Label(new Rect(115f, y, width - 115f, h), e.text);
                GUI.color = Color.white;
                y += h + 4f;
            }
            EndScroll(y);
        }

        private enum ChronicleFilter
        {
            All,
            Wishes,
            Council,
            People,
            Strangers,
            World,
            Dreams,
            Losses
        }

        private static ChronicleFilter chronicleFilter = ChronicleFilter.All;
        private static readonly Dictionary<ChronicleEntry, float> chronicleHeights = new Dictionary<ChronicleEntry, float>();
        private static float chronicleWidth;

        private static bool Matches(ChronicleKind k, ChronicleFilter f)
        {
            switch (f)
            {
                case ChronicleFilter.Wishes:
                    return k == ChronicleKind.WishMade || k == ChronicleKind.WishGranted || k == ChronicleKind.WishExpired || k == ChronicleKind.WishDismissed
                           || k == ChronicleKind.Petition || k == ChronicleKind.PersonalTouch;
                case ChronicleFilter.Council:
                    return k == ChronicleKind.Meeting || k == ChronicleKind.AmbitionChosen || k == ChronicleKind.AmbitionAchieved || k == ChronicleKind.AmbitionFailed
                           || k == ChronicleKind.AmbitionAbandoned || k == ChronicleKind.Council;
                case ChronicleFilter.People:
                    return k == ChronicleKind.Bond || k == ChronicleKind.Life || k == ChronicleKind.Secret;
                case ChronicleFilter.Strangers:
                    return k == ChronicleKind.Stranger;
                case ChronicleFilter.World:
                    return k == ChronicleKind.World;
                case ChronicleFilter.Dreams:
                    return k == ChronicleKind.Dream;
                case ChronicleFilter.Losses:
                    return k == ChronicleKind.Loss;
                default:
                    return true;
            }
        }
    }
}
