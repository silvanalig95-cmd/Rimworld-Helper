using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Better clothes for each colonist where the player looks (docs/design/apparel.md, step 3): on the gear tab, the game's
    /// and Nice Inventory Tab's (Apparel/ApparelNiceTab.cs) alike, a row for each suggestion that stands, drawn as the
    /// weapons' are (a dashed, softly tinted frame and a tag, so it never reads as a piece worn: "Suggested", "Needed" for a
    /// gap, "Asked for", "Outfit" for one their outfit doesn't allow): the piece's own picture, a short head ("Warmth: parka
    /// (+40 °C)", "No helmet: flak helmet", "In place of tattered T-shirt: button-down shirt"), its rating for them and the
    /// gain, why in a line (the whole line on the tooltip), a warning when it costs something that matters (a lucky piece,
    /// an heirloom, the other gender, slower work), and its buttons (Wear, Take from whom, Trade with whom, Allow, Not now),
    /// each a real job walked out on the map. With nothing to suggest, one quiet line says so, honest about their gaps:
    /// "No warmer clothes in storage for Bautista", its tooltip what was compared. Worked out once a real second.
    /// </summary>
    public static class ApparelPickGearTab
    {
        public static bool On => RimfolkPatches.On && ApparelPick.On;

        public const int MaxRows = 3;

        private static Pawn viewFor;
        private static float viewAt = -10f;
        private static ApparelPick.Picks viewPicks;
        private static List<WearSuggestion> view = new List<WearSuggestion>();
        private static string quietText, quietKey;
        private static bool quietUnder;

        private sealed class LineView
        {
            public string why, whyNot, actTip, notNowTip, name, gain, act, warning, tag;
            public bool onTheWay, asked;
            public float actW;
        }

        private static readonly Dictionary<WearSuggestion, LineView> lines = new Dictionary<WearSuggestion, LineView>();

        private static float ButtonW(string label) => Mathf.Max(48f, UiKit.KeptWidth(label, GameFont.Tiny) + 16f);

        /// <summary>A suggestion's button: "Wear", "Take from Bautista", "Trade with Bautista", "Allow" (its outfit), "Outfit" (open it).</summary>
        public static string ActLabel(ApparelPick.Picks picks, WearSuggestion s)
        {
            switch (s.Kind)
            {
                case WearKind.Spare: return "CC_ApparelPick_Act_SpareFrom".Translate(picks.Pawn(s.Other).Named("OTHER")).Resolve();
                case WearKind.Trade: return "CC_WeaponPick_Act_SwapWith".Translate(picks.Pawn(s.Other).Named("OTHER")).Resolve();
                case WearKind.Outfit: return (s.OutfitFixable ? "CC_ApparelPick_Act_Allow" : "CC_ApparelPick_Act_OpenOutfit").Translate().Resolve();
                default: return "CC_ApparelPick_Act_Wear".Translate().Resolve();
            }
        }

        public static string ActTip(ApparelPick.Picks picks, WearSuggestion s)
        {
            var p = picks.Pawn(s.Colonist);
            if (s.Kind == WearKind.Outfit)
            {
                var policy = p?.outfits?.CurrentApparelPolicy;
                var t = picks.Thing(s.Piece);
                return s.OutfitFixable
                    ? "CC_ApparelPick_Act_Allow_Tip".Translate((t?.def.label ?? "?").Named("KIND"), (s.Outfit ?? "?").Named("OUTFIT"), ApparelPick.SharingOutfit(picks.map, policy).Named("COUNT"), p.Named("PAWN")).Resolve()
                    : "CC_ApparelPick_Act_OpenOutfit_Tip".Translate((s.Outfit ?? "?").Named("OUTFIT")).Resolve();
            }
            return ("CC_ApparelPick_Act_" + s.Kind + "_Tip").Translate(p.Named("PAWN"), (picks.Pawn(s.Other)?.LabelShort ?? "").Named("OTHER")).Resolve();
        }

        /// <summary>The row's own head, without the colonist's name (it is their tab): "Warmth: parka (+40 °C)", "No helmet: flak helmet", "In place of tattered T-shirt: button-down shirt".</summary>
        public static string RowName(ApparelPick.Picks picks, WearSuggestion s, Thing t)
        {
            string piece = t?.LabelCapNoCount ?? ApparelPick.Label(picks, s.Piece).CapitalizeFirst();
            if (s.Kind == WearKind.Outfit) return "CC_ApparelPick_Row_Outfit".Translate((s.Outfit ?? "?").Named("OUTFIT"), piece.Named("APPAREL")).Resolve();
            switch (s.Gap)
            {
                case WearGap.Warmth:
                case WearGap.Cooling:
                    return ("CC_ApparelPick_Row_" + s.Gap).Translate(piece.Named("APPAREL"), ApparelPickMath.DegreesText(s.Degrees).Named("DEGREES")).Resolve();
                case WearGap.Armour:
                case WearGap.Helmet:
                    return ("CC_ApparelPick_Row_" + s.Gap).Translate(piece.Named("APPAREL")).Resolve();
                case WearGap.Worn:
                    return "CC_ApparelPick_Row_Worn".Translate(piece.Named("APPAREL"), string.Join(", ", s.Instead.Select(k => ApparelPick.Label(picks, k))).Named("WORN")).Resolve();
            }
            return piece;
        }

        private static LineView Make(ApparelPick.Picks picks, WearSuggestion s)
        {
            var p = picks.Pawn(s.Colonist);
            var t = picks.Thing(s.Piece);
            var v = new LineView
            {
                why = ApparelPick.Why(picks, s).CapitalizeFirst(), onTheWay = ApparelPick.OnTheWay(picks, s), name = RowName(picks, s, t), gain = ApparelPickMath.GainText(s.Gain)
            };
            if (!v.onTheWay) v.whyNot = ApparelPick.WhyNot(picks, s);
            v.act = ActLabel(picks, s);
            v.actW = ButtonW(v.act);
            v.actTip = ActTip(picks, s);
            v.notNowTip = "CC_ApparelPick_NotNow_Tip".Translate(p.Named("PAWN"), ApparelAskMath.NotNowDays.Named("DAYS")).Resolve();
            var costs = s.Costs.Select(r => ApparelPick.Words(picks, s, r)).Where(x => !x.NullOrEmpty()).ToList();
            if (costs.Count > 0) v.warning = string.Join("; ", costs).CapitalizeFirst();
            var book = ApparelPickBook.Instance;
            int now = Find.TickManager?.TicksGame ?? 0;
            v.asked = t != null && book?.AskOf(p) is ApparelAskNote a && a.piece == t.thingIDNumber && ApparelAskMath.Shows(now, a.tick, 0);
            v.tag = (v.asked ? "CC_WeaponPick_TagAsked" : s.Kind == WearKind.Outfit ? "CC_ApparelPick_TagOutfit" : s.Gap.HasValue ? "CC_ApparelPick_TagNeeded" : "CC_WeaponPick_TagSuggested").Translate();
            return v;
        }

        /// <summary>A colonist's suggestions that stand, worked out at most once a real second; the quiet line's state with them.</summary>
        public static List<WearSuggestion> For(Pawn p, out ApparelPick.Picks picks)
        {
            picks = null;
            if (p == null || (!p.IsColonistPlayerControlled && !p.IsSlaveOfColony) || !On)
            {
                quietText = null;
                return new List<WearSuggestion>();
            }
            float now = Time.realtimeSinceStartup;
            if (viewFor != p || now - viewAt > 1f || viewAt > now)
            {
                viewFor = p;
                viewAt = now;
                view = ApparelPick.Standing(p);
                viewPicks = ApparelPick.Current(p.Map);
                lines.Clear();
                if (viewPicks != null)
                    foreach (var s in view.Take(MaxRows))
                        lines[s] = Make(viewPicks, s);
                MakeQuiet(p);
            }
            picks = viewPicks;
            return view;
        }

        /// <summary>
        /// The quiet line: with nothing to suggest, set aside, or a gap of theirs nothing fills ("No warmer clothes in storage for
        /// Bautista"), else no better clothes; with rows, only a gap nothing fills (under them).
        /// </summary>
        private static void MakeQuiet(Pawn p)
        {
            quietText = null;
            quietKey = null;
            quietUnder = false;
            if (viewPicks == null || !p.Spawned || !ApparelPick.Colonists(p.Map).Contains(p)) return;
            if (ApparelPickBook.Instance?.IsNotNow(p) == true)
            {
                if (view.Count > 0) return;
                quietKey = "SetAside";
                quietText = "CC_ApparelPick_QuietSetAside".Translate(p.Named("PAWN")).Resolve();
                return;
            }
            var input = viewPicks.input;
            var c = input.Colonist(ApparelPick.Key(p));
            if (c != null)
                foreach (var (gap, worn) in ApparelPickMath.Gaps(input, c))
                {
                    bool filled = view.Any(s => s.Gap == gap && (gap != WearGap.Worn || s.Instead.Contains(worn)));
                    if (filled) continue;
                    quietKey = gap.ToString();
                    quietText = ("CC_ApparelPick_Quiet_" + gap).Translate(p.Named("PAWN"), ApparelPick.Label(viewPicks, worn).Named("WORN")).Resolve();
                    quietUnder = view.Count > 0;
                    return;
                }
            if (view.Count == 0)
            {
                quietKey = "None";
                quietText = "CC_ApparelPick_QuietNone".Translate(p.Named("PAWN")).Resolve();
            }
        }

        public static void Refresh() => viewAt = -10f;

        private static bool HasWarning(WearSuggestion s) => lines.TryGetValue(s, out var v) && v.warning != null;

        /// <summary>The room the suggestions take (the rows, or the quiet line), for Nice Inventory Tab's box; 0 for none.</summary>
        public static float Height(Pawn p)
        {
            var list = For(p, out var picks);
            if (list.Count > 0 && picks != null) return WeaponNiceMath.Height(list.Take(MaxRows).Select(HasWarning).ToList(), false, quietUnder);
            return WeaponNiceMath.Height(null, quietText != null);
        }

        private static SuggestLook gameLook;

        private static SuggestLook GameLook => gameLook ??= new SuggestLook
        {
            ground = new Color(1f, 1f, 1f, 0.03f), tile = new Color(0.07f, 0.08f, 0.09f, 1f), ink = Color.white, faint = new Color(0.7f, 0.7f, 0.7f),
            good = new Color(0.52f, 0.85f, 0.5f), bad = new Color(0.93f, 0.45f, 0.38f), warn = new Color(0.95f, 0.66f, 0.28f),
            frame = new Color(0.52f, 0.85f, 0.5f, 0.7f), gameButtons = true
        };

        /// <summary>The rows (or the quiet line) in a rect from its top, for a colonist; returns the height used.</summary>
        public static float Rows(Rect r, Pawn p, SuggestLook look)
        {
            var list = For(p, out var picks);
            if (list.Count == 0 || picks == null)
            {
                if (quietText == null) return 0f;
                QuietLine(new Rect(r.x, r.y, r.width, WeaponNiceMath.QuietH), p, look);
                return WeaponNiceMath.QuietH;
            }
            float y = r.y;
            int i = 0;
            foreach (var s in list.Take(MaxRows))
            {
                if (i++ > 0) y += WeaponNiceMath.Gap;
                float h = WeaponNiceMath.RowHeight(HasWarning(s));
                Row(new Rect(r.x, y, r.width, h), picks, s, look);
                y += h;
            }
            if (quietUnder && quietText != null)
            {
                y += WeaponNiceMath.Gap;
                QuietLine(new Rect(r.x, y, r.width, WeaponNiceMath.QuietH), p, look);
                y += WeaponNiceMath.QuietH;
            }
            return y - r.y;
        }

        /// <summary>The game's gear tab: "Suggested clothes" and the rows, or the quiet line, from <paramref name="y"/>; moves it down past them.</summary>
        public static void Draw(ref float y, float width, Pawn p)
        {
            var list = For(p, out var picks);
            if ((list.Count == 0 || picks == null) && quietText == null) return;
            if (list.Count > 0 && picks != null) Widgets.ListSeparator(ref y, width, "CC_ApparelPick_GearHeader".Translate());
            else y += 4f;
            y += Rows(new Rect(0f, y, width, 9999f), p, GameLook) + 6f;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        /// <summary>One suggestion's row, as the weapons' rows are drawn: frame and tag, picture, head, badge and gain, why, its buttons or "on the way", the warning.</summary>
        public static void Row(Rect r, ApparelPick.Picks picks, WearSuggestion s, SuggestLook look)
        {
            var p = picks.Pawn(s.Colonist);
            var t = picks.Thing(s.Piece);
            if (p == null || t == null) return;
            if (!lines.TryGetValue(s, out var v)) lines[s] = v = Make(picks, s);
            var accent = s.Kind == WearKind.Outfit ? look.warn : look.good;
            if (Event.current.type == EventType.Repaint)
            {
                Widgets.DrawBoxSolid(r, look.ground);
                Widgets.DrawBoxSolid(r, UiKit.WithAlpha(accent, Mouse.IsOver(r) ? 0.09f : 0.05f));
            }
            UiKit.DashedBox(r, s.Kind == WearKind.Outfit ? UiKit.WithAlpha(look.warn, 0.7f) : look.frame);
            float m = WeaponNiceMath.Margin;
            var tile = new Rect(r.x + m, r.y + m, WeaponNiceMath.TileW, WeaponNiceMath.TileH);
            UiKit.ItemPortrait(tile, t, ground: look.tile, tip: () => Tip(picks, s));
            float x = tile.xMax + WeaponNiceMath.TileGap, right = r.xMax - m, ly = r.y + m;
            Text.Font = GameFont.Tiny;
            float tagW = UiKit.KeptWidth(v.tag, GameFont.Tiny) + 12f;
            var tagR = new Rect(x, ly + 2f, tagW, 16f);
            if (Event.current.type == EventType.Repaint) UiKit.RoundBox(tagR, UiKit.WithAlpha(accent, 0.14f), UiKit.WithAlpha(accent, 0.6f), SkinMath.RadiusSmall);
            Label(tagR, v.tag, accent, GameFont.Tiny, TextAnchor.MiddleCenter);
            float gainW = 32f;
            var gainR = new Rect(right - gainW, ly, gainW, 20f);
            Label(gainR, v.gain, s.Gain >= 0f ? look.good : look.bad, GameFont.Small, TextAnchor.MiddleRight);
            var badge = new Rect(gainR.x - 4f - WeaponGearTab.BadgeW, ly + 1.5f, WeaponGearTab.BadgeW, WeaponGearTab.BadgeH);
            WeaponUi.Badge(badge, s.Rating);
            var nameR = new Rect(tagR.xMax + 6f, ly, badge.x - 6f - tagR.xMax - 6f, 20f);
            Label(nameR, v.name, look.ink, GameFont.Small);
            Label(new Rect(x, ly + 20f, right - x, 17f), v.why, look.faint, GameFont.Tiny);
            var words = new Rect(x, r.y, right - x, ly + 37f - r.y);
            TooltipHandler.TipRegion(words, () => Tip(picks, s), s.Piece.GetHashCode() ^ 0x5a1e);
            if (ApparelCardWindow.On && Widgets.ButtonInvisible(tile, false)) ApparelCardWindow.Open(t, p);
            if (ApparelCardWindow.On && Widgets.ButtonInvisible(new Rect(x, ly, right - x, 37f), false)) ApparelCardWindow.Open(t, p);
            float by = ly + 38f, bh = 20f;
            if (v.onTheWay) Label(new Rect(x, by, right - x, bh), "CC_WeaponPick_OnTheWay".Translate(), look.faint, GameFont.Tiny);
            else
            {
                float bx = x;
                if (Button(new Rect(bx, by, v.actW, bh), v.act, v.whyNot, v.actTip, look))
                {
                    ApparelPick.Do(picks, s);
                    Refresh();
                }
                bx += v.actW + 4f;
                string notNow = "CC_WeaponPick_NotNowShort".Translate();
                float nw = ButtonW(notNow);
                bool word = WeaponNiceMath.NotNowAsWord(right - x, bx - 4f - x, nw);
                var nr = word ? new Rect(right - nw, by, nw, bh) : new Rect(right - WeaponNiceMath.CrossW, by, WeaponNiceMath.CrossW, bh);
                bool clicked = word ? Button(nr, notNow, null, v.notNowTip, look) : Widgets.ButtonImage(nr.ContractedBy(2f), TexButton.CloseXSmall, look.faint, GenUI.MouseoverColor);
                if (!word) TooltipHandler.TipRegion(nr, v.notNowTip);
                if (clicked)
                {
                    ApparelPickBook.Instance?.SetNotNow(p, true);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    Refresh();
                }
            }
            if (v.warning != null)
            {
                var wr = new Rect(x, by + bh + 2f, right - x, WeaponNiceMath.WarnH - 2f);
                UiKit.Icon(new Rect(wr.x, wr.center.y - 6f, 12f, 12f), Icons.Warning, look.warn);
                Label(new Rect(wr.x + 15f, wr.y, wr.width - 15f, wr.height), v.warning, look.warn, GameFont.Tiny);
                TooltipHandler.TipRegion(wr, v.warning);
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private static void Label(Rect r, string text, Color c, GameFont font, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            if (text.NullOrEmpty() || r.width <= 4f) return;
            var oldFont = Text.Font;
            Text.Font = font;
            Text.Anchor = anchor;
            GUI.color = c;
            bool wrap = Text.WordWrap;
            Text.WordWrap = false;
            Widgets.Label(r, UiKit.Fit(text, r.width));
            Text.WordWrap = wrap;
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;
        }

        private static bool Button(Rect r, string label, string whyNot, string tip, SuggestLook look)
        {
            bool can = whyNot == null;
            string t = can ? tip : whyNot;
            if (!t.NullOrEmpty()) TooltipHandler.TipRegion(r, t);
            if (look.gameButtons) return UiKit.Button(r, label, ButtonKind.Normal, can, null, GameFont.Tiny) && can;
            bool over = can && Mouse.IsOver(r);
            if (Event.current.type == EventType.Repaint) Widgets.DrawBoxSolid(r, over ? look.buttonHover : look.buttonFill);
            Label(r, label, can ? (over ? Color.white : look.buttonInk) : UiKit.WithAlpha(look.buttonInk, 0.4f), GameFont.Tiny, TextAnchor.MiddleCenter);
            if (!can) return false;
            MouseoverSounds.DoRegion(r);
            if (!Widgets.ButtonInvisible(r, false)) return false;
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            return true;
        }

        /// <summary>The quiet line: a click on "set aside" brings them back; any other opens every colonist's clothes on the Defenses page.</summary>
        private static void QuietLine(Rect r, Pawn p, SuggestLook look)
        {
            if (quietText == null) return;
            bool over = Mouse.IsOver(r);
            if (over) Widgets.DrawHighlight(r);
            UiKit.Icon(new Rect(r.x + 4f, r.center.y - 7f, 14f, 14f), Icons.CatDefense, UiKit.WithAlpha(look.faint, 0.8f));
            Label(new Rect(r.x + 24f, r.y, r.width - 28f, r.height), quietText, look.faint, GameFont.Tiny);
            var who = p;
            string kind = quietKey;
            TooltipHandler.TipRegion(r, () => QuietTip(who, kind), who.thingIDNumber ^ 0x0c10);
            if (!Widgets.ButtonInvisible(r)) return;
            if (kind == "SetAside")
            {
                ApparelPickBook.Instance?.SetNotNow(p, false);
                Refresh();
            }
            else ApparelPickPanel.Show(p.Map);
        }

        /// <summary>What was compared, made only while the mouse is over the quiet line: the cold or heat ahead for them, the pieces in storage, the threshold.</summary>
        private static string QuietTip(Pawn p, string kind)
        {
            if (kind == "SetAside") return "CC_ApparelPick_QuietSetAside_Tip".Translate(p.Named("PAWN")).Resolve();
            var picks = ApparelPick.Current(p.Map);
            var c = picks?.input.Colonist(ApparelPick.Key(p));
            string tip = "CC_ApparelPick_QuietNone_Tip".Translate(p.Named("PAWN"), (picks?.stored ?? 0).Named("COUNT"), WeaponForMath.RatingText(ApparelPickMath.Threshold).Named("GAIN")).Resolve();
            if (picks != null && c != null)
            {
                if (c.ColdShort >= ApparelPickMath.ShortAt)
                    tip += "\n\n" + "CC_ApparelPick_QuietCold".Translate(p.Named("PAWN"), ApparelPickMath.DegreesText(c.ColdShort).Named("SHORT"), ApparelForMath.Degrees(picks.coldest).Named("COLDEST")).Resolve();
                if (c.HeatShort >= ApparelPickMath.ShortAt)
                    tip += "\n\n" + "CC_ApparelPick_QuietHot".Translate(p.Named("PAWN"), ApparelPickMath.DegreesText(c.HeatShort).Named("SHORT"), ApparelForMath.Degrees(picks.hottest).Named("HOTTEST")).Resolve();
                if (picks.forbidden > 0) tip += "\n\n" + "CC_ApparelPick_QuietForbidden".Translate(picks.forbidden.Named("COUNT")).Resolve();
                if (kind == "Armour" || kind == "Helmet") tip += "\n\n" + "CC_ApparelPick_QuietRaids".Translate().Resolve();
            }
            return tip + "\n\n" + "CC_ApparelPick_QuietClick".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor);
        }

        /// <summary>A suggestion's tooltip: the whole line, what it costs, where it lies, what a click does.</summary>
        public static string Tip(ApparelPick.Picks picks, WearSuggestion s)
        {
            string tip = ApparelPick.Full(picks, s);
            string place = picks.Place(s.Piece);
            if (!place.NullOrEmpty()) tip += "\n\n" + "CC_ApparelPick_Lies".Translate(ApparelPick.Label(picks, s.Piece).CapitalizeFirst().Named("APPAREL"), place.Named("PLACE")).Resolve();
            if (ApparelCardWindow.On) tip += "\n\n" + "CC_ApparelPick_ClickCard".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor);
            return tip;
        }
    }

    /// <summary>The suggested clothes' rows on the game's own gear tab, under the comfortable temperatures and the suggested weapons (the tab's scroll height follows).</summary>
    [HarmonyPatch(typeof(ITab_Pawn_Gear), "TryDrawComfyTemperatureRange")]
    public static class Patch_ApparelPickGearTab
    {
        private static bool broken;
        private static readonly MethodInfo SelPawn = AccessTools.PropertyGetter(typeof(ITab_Pawn_Gear), "SelPawnForGear");

        [HarmonyPriority(Priority.Low)]
        public static void Postfix(ITab_Pawn_Gear __instance, ref float curY, float width)
        {
            if (broken || !ApparelPickGearTab.On) return;
            try
            {
                if (SelPawn?.Invoke(__instance, null) is Pawn p) ApparelPickGearTab.Draw(ref curY, width, p);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the suggested clothes on the gear tab", e);
            }
        }
    }

    /// <summary>
    /// The colony's view on the Defenses page ("Clothes and armour for each colonist"), next to the weapons: every colonist
    /// with their gaps at a glance (how many degrees short for the cold or heat ahead, no body armour, no helmet, a worn-out
    /// piece), their suggestions with the gain and why, a button each, "Not now" or "Show again"; "Wear all suggested" at its
    /// heading, which only gives the orders. What the choice weighs is said under the heading. Its words worked out once a second.
    /// </summary>
    public static class ApparelPickPanel
    {
        public const string FoldKey = "apparel";

        private static bool focus;
        private static Map modelFor;
        private static float modelAt = -10f;
        private static ApparelPick.Picks modelPicks;
        private static readonly List<Row> rows = new List<Row>();
        private static string weighs;
        private static int doable;

        private sealed class Row
        {
            public Pawn p;
            public bool notNow;
            public readonly List<(string text, bool bad, string tip)> chips = new List<(string, bool, string)>();
            public readonly List<(WearSuggestion s, string head, string why, string whyNot, bool onTheWay)> picks = new List<(WearSuggestion, string, string, string, bool)>();
        }

        private static float RowHeight(Row row) => 30f + ChipH + Mathf.Max(1, row.picks.Count) * 40f;

        private const float ChipH = 24f;

        public static void Show(Map map)
        {
            focus = true;
            if (map != null && Find.CurrentMap != map) Current.Game.CurrentMap = map;
            Pages.Open("Defenses");
        }

        public static bool TakeFocus()
        {
            bool f = focus;
            focus = false;
            return f;
        }

        private static void Build(Map map)
        {
            rows.Clear();
            modelFor = map;
            modelAt = Time.realtimeSinceStartup;
            modelPicks = ApparelPick.For(map);
            doable = 0;
            weighs = null;
            var picks = modelPicks;
            if (picks == null) return;
            var book = ApparelPickBook.Instance;
            var input = picks.input;
            foreach (var p in ApparelPick.Colonists(map).OrderBy(x => x.LabelShort))
            {
                var row = new Row { p = p, notNow = book?.IsNotNow(p) ?? false };
                var c = input.Colonist(ApparelPick.Key(p));
                if (c != null)
                {
                    var gaps = ApparelPickMath.Gaps(input, c);
                    if (c.ColdShort >= ApparelPickMath.ShortAt)
                        row.chips.Add(("CC_ApparelPick_Chip_Cold".Translate(ApparelPickMath.DegreesText(c.ColdShort).Named("SHORT")).Resolve(), true,
                            "CC_ApparelPick_QuietCold".Translate(p.Named("PAWN"), ApparelPickMath.DegreesText(c.ColdShort).Named("SHORT"), ApparelForMath.Degrees(picks.coldest).Named("COLDEST")).Resolve()));
                    if (c.HeatShort >= ApparelPickMath.ShortAt)
                        row.chips.Add(("CC_ApparelPick_Chip_Hot".Translate(ApparelPickMath.DegreesText(c.HeatShort).Named("SHORT")).Resolve(), true,
                            "CC_ApparelPick_QuietHot".Translate(p.Named("PAWN"), ApparelPickMath.DegreesText(c.HeatShort).Named("SHORT"), ApparelForMath.Degrees(picks.hottest).Named("HOTTEST")).Resolve()));
                    if (c.ColdShort < ApparelPickMath.ShortAt && c.HeatShort < ApparelPickMath.ShortAt)
                        row.chips.Add(("CC_ApparelPick_Chip_Dressed".Translate().Resolve(), false, "CC_ApparelPick_Chip_Dressed_Tip".Translate(p.Named("PAWN")).Resolve()));
                    if (gaps.Any(g => g.gap == WearGap.Armour)) row.chips.Add(("CC_ApparelPick_Chip_NoArmour".Translate().Resolve(), true, "CC_ApparelPick_QuietRaids".Translate().Resolve()));
                    if (gaps.Any(g => g.gap == WearGap.Helmet)) row.chips.Add(("CC_ApparelPick_Chip_NoHelmet".Translate().Resolve(), true, "CC_ApparelPick_QuietRaids".Translate().Resolve()));
                    var worn = gaps.Where(g => g.gap == WearGap.Worn).Select(g => ApparelPick.Label(picks, g.piece)).ToList();
                    if (worn.Count > 0) row.chips.Add(("CC_ApparelPick_Chip_Worn".Translate(worn.Count.Named("COUNT")).Resolve(), true, worn.ToCommaList(true).CapitalizeFirst()));
                }
                foreach (var s in picks.Of(p).Where(x => ApparelPick.Stands(picks, x)).OrderBy(x => x.Kind == WearKind.Outfit ? 1 : 0).ThenBy(x => x.Gap.HasValue ? 0 : 1).ThenByDescending(x => x.Gain))
                {
                    bool onTheWay = ApparelPick.OnTheWay(picks, s);
                    string whyNot = onTheWay ? null : ApparelPick.WhyNot(picks, s);
                    string why = ApparelPick.Why(picks, s).CapitalizeFirst();
                    string costs = ApparelPick.Costs(picks, s);
                    if (!costs.NullOrEmpty()) why += (why.NullOrEmpty() ? "" : ". ") + costs;
                    row.picks.Add((s, ApparelPick.Head(picks, s), why, whyNot, onTheWay));
                    if (s.Kind != WearKind.Outfit && !onTheWay && whyNot == null) doable++;
                }
                rows.Add(row);
            }
            var what = new List<string>();
            if (input.Colonists.Any(c => c.ColdShort >= ApparelPickMath.ShortAt)) what.Add("CC_ApparelPick_Weighs_Cold".Translate(ApparelForMath.Degrees(picks.coldest).Named("COLDEST")).Resolve());
            if (input.Colonists.Any(c => c.HeatShort >= ApparelPickMath.ShortAt)) what.Add("CC_ApparelPick_Weighs_Heat".Translate(ApparelForMath.Degrees(picks.hottest).Named("HOTTEST")).Resolve());
            if (input.RaidHeavy) what.Add("CC_ApparelPick_Weighs_Raids".Translate().Resolve());
            weighs = (what.Count > 0 ? "CC_ApparelPick_Weighs".Translate(what.ToCommaList(true).Named("WHAT")).Resolve() : "CC_ApparelPick_WeighsNone".Translate().Resolve()) +
                     " " + "CC_ApparelPick_Stored".Translate(picks.stored.Named("COUNT")).Resolve();
        }

        /// <summary>The view in the Defenses page's right column, from <paramref name="y"/>; returns the y under it.</summary>
        public static float Draw(Map map, float x, float y, float w, Folds folds)
        {
            if (!ApparelPick.On || map == null) return y;
            if (modelFor != map || Time.realtimeSinceStartup - modelAt > 1f) Build(map);
            int count = rows.Sum(r => r.picks.Count);
            string why = doable > 0 ? null : "CC_ApparelPick_AllNone".Translate().Resolve();
            y += UiKit.SectionHeading(x, y, w, "CC_ApparelPick_Section".Translate(), count, folds, FoldKey, out bool open, "CC_ApparelPick_AllAct".Translate(doable.Named("COUNT")).Resolve(), why,
                out bool acted, "CC_ApparelPick_AllAct_Tip".Translate().Resolve());
            if (acted && modelPicks != null)
            {
                ApparelPick.DoAll(modelPicks);
                SoundDefOf.Click.PlayOneShotOnCamera();
                modelAt = -10f;
            }
            if (!open) return y;
            if (!weighs.NullOrEmpty()) y += UiKit.Paragraph(x, y, w, weighs, UiKit.Faint, GameFont.Tiny) + 6f;
            if (rows.Count == 0) return y + UiKit.EmptyState(x, y, w, Icons.CatDefense, "CC_ApparelPick_NoColonists".Translate(), 70f) + 4f;
            foreach (var row in rows) y = DrawRow(row, x, y, w) + 4f;
            return y;
        }

        private static float DrawRow(Row row, float x, float y, float w)
        {
            var picks = modelPicks;
            float h = RowHeight(row);
            var box = new Rect(x, y, w, h);
            UiKit.Card(box, row.picks.Count > 0 && !row.notNow ? UiKit.WithAlpha(UiKit.Good, 0.5f) : (Color?)null, false, false);
            var face = new Rect(x + 6f, y + 4f, 24f, 24f);
            UiKit.Portrait(face, row.p, UiKit.Faint);
            if (Widgets.ButtonInvisible(face, false))
            {
                Cards.Click();
                Cards.Open(row.p);
            }
            UiKit.LabelFit(new Rect(face.xMax + 6f, y + 4f, w - 160f, 24f), row.p.LabelShortCap, UiKit.Ink, GameFont.Small);
            var aside = new Rect(x + w - 116f, y + 4f, 110f, 22f);
            if (row.notNow)
            {
                if (UiKit.ActButton(aside, "CC_WeaponPick_AskAgain".Translate(), null, "CC_ApparelPick_AskAgain_Tip".Translate(row.p.Named("PAWN")).Resolve()))
                {
                    ApparelPickBook.Instance?.SetNotNow(row.p, false);
                    modelAt = -10f;
                }
            }
            else if (row.picks.Count > 0 && UiKit.ActButton(aside, "CC_WeaponPick_NotNowShort".Translate(), null,
                         "CC_ApparelPick_NotNow_Tip".Translate(row.p.Named("PAWN"), ApparelAskMath.NotNowDays.Named("DAYS")).Resolve()))
            {
                ApparelPickBook.Instance?.SetNotNow(row.p, true);
                modelAt = -10f;
            }
            float ly = y + 30f;
            // Their gaps at a glance.
            float cx = face.xMax + 6f;
            foreach (var (text, bad, tip) in row.chips)
            {
                float cw = Mathf.Min(UiKit.KeptWidth(text, GameFont.Tiny) + 12f, x + w - cx - 6f);
                if (cw < 30f) break;
                var cr = new Rect(cx, ly - 2f, cw, ChipH - 6f);
                if (Event.current.type == EventType.Repaint) Widgets.DrawBoxSolid(cr, UiKit.WithAlpha(bad ? UiKit.Warn : UiKit.Faint, 0.1f));
                UiKit.LabelFit(cr, text, bad ? UiKit.Warn : UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleCenter);
                TooltipHandler.TipRegion(cr, tip);
                cx += cw + 6f;
            }
            ly += ChipH;
            if (row.picks.Count == 0)
            {
                string none = row.notNow ? "CC_WeaponPick_SetAside".Translate(row.p.Named("PAWN")).Resolve() : "CC_ApparelPick_KeepsThem".Translate(row.p.Named("PAWN")).Resolve();
                UiKit.LabelFit(new Rect(face.xMax + 6f, ly + 4f, w - 40f, 22f), none, UiKit.Faint, GameFont.Tiny);
                return y + h;
            }
            foreach (var (s, head, why, whyNot, onTheWay) in row.picks)
            {
                var line = new Rect(x + 30f, ly, w - 36f, 38f);
                var t = picks.Thing(s.Piece);
                if (t != null) UiKit.ItemPortrait(new Rect(line.x, ly + 4f, 26f, 26f), t);
                WeaponUi.Badge(new Rect(line.x + 30f, ly + 8f, 32f, 18f), s.Rating);
                float bw = 112f;
                var text = new Rect(line.x + 66f, ly, line.width - 66f - bw - 6f, 38f);
                UiKit.LabelFit(new Rect(text.x, ly, text.width, 20f), head, row.notNow ? UiKit.Faint : s.Kind == WearKind.Outfit ? UiKit.Warn : s.Gain >= 0f ? UiKit.Good : UiKit.Warn, GameFont.Small);
                UiKit.LabelFit(new Rect(text.x, ly + 19f, text.width, 18f), why, UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(new Rect(line.x, ly, text.xMax - line.x, 38f), () => ApparelPickGearTab.Tip(picks, s), s.Piece.GetHashCode() ^ 0x2e7);
                if (t != null && ApparelCardWindow.On && Widgets.ButtonInvisible(new Rect(line.x, ly, text.xMax - line.x, 38f), false)) ApparelCardWindow.Open(t, row.p);
                var b = new Rect(line.xMax - bw, ly + 7f, bw, 24f);
                if (onTheWay) UiKit.LabelFit(b, "CC_WeaponPick_OnTheWay".Translate(), UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                else if (UiKit.ActButton(b, ApparelPickGearTab.ActLabel(picks, s), whyNot, ApparelPickGearTab.ActTip(picks, s)))
                {
                    ApparelPick.Do(picks, s);
                    modelAt = -10f;
                }
                ly += 40f;
            }
            return y + h;
        }
    }
}
