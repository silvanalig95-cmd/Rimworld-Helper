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
    /// The best weapon for each colonist where the player looks (docs/design/weapons.md, step 3): on the gear tab a line for
    /// each suggestion that stands ("Better for Ana: charge rifle (+1.4)", its reasons under it), with a button to equip it (a
    /// real job, walked out on the map), with Simple Sidearms one to take it as a sidearm, and "not now"; on Nice Inventory
    /// Tab's equipment box the same behind a small mark on its title, a click giving the choices. What is drawn is read from
    /// the colony's kept choice, checked that it still stands once a second.
    /// </summary>
    public static class WeaponPickGearTab
    {
        public static bool On => RimfolkPatches.On && WeaponPick.On;

        public const float LineH = 46f;

        private static Pawn viewFor;
        private static float viewAt = -10f;
        private static WeaponPick.Picks viewPicks;
        private static List<PickSuggestion> view = new List<PickSuggestion>();

        /// <summary>What a line shows, worked out with the view (once a real second), so nothing is asked of the map per frame.</summary>
        private sealed class LineView
        {
            public string head, why, whyNot, whyNotSidearm, actTip, sidearmTip, notNowTip;
            public bool onTheWay, sidearms;
        }

        private static readonly Dictionary<PickSuggestion, LineView> lines = new Dictionary<PickSuggestion, LineView>();

        private static LineView Make(WeaponPick.Picks picks, PickSuggestion s)
        {
            var p = picks.Pawn(s.Colonist);
            var v = new LineView { head = WeaponPick.Head(picks, s), why = WeaponPick.Why(picks, s).CapitalizeFirst(), onTheWay = WeaponPick.OnTheWay(picks, s) };
            v.sidearms = s.Kind == PickKind.Primary && WeaponPick.CanTakeSidearms && picks.input.Weapon(s.Weapon) is PickWeapon w && w.Stored && !w.HandOnly &&
                         w.Mass <= (w.Ranged ? picks.input.Rules.SidearmRangedMass : picks.input.Rules.SidearmMeleeMass) + 0.001f;
            if (!v.onTheWay)
            {
                v.whyNot = WeaponPick.WhyNot(picks, s);
                if (v.sidearms) v.whyNotSidearm = WeaponPick.WhyNot(picks, s, true);
            }
            v.actTip = ("CC_WeaponPick_Act_" + s.Kind + "_Tip").Translate(p.Named("PAWN")).Resolve();
            v.sidearmTip = "CC_WeaponPick_Act_AsSidearm_Tip".Translate(p.Named("PAWN")).Resolve();
            v.notNowTip = "CC_WeaponPick_NotNow_Tip".Translate(p.Named("PAWN"), WeaponAskMath.NotNowDays.Named("DAYS")).Resolve();
            return v;
        }

        /// <summary>A colonist's suggestions that stand, worked out at most once a real second.</summary>
        public static List<PickSuggestion> For(Pawn p, out WeaponPick.Picks picks)
        {
            picks = null;
            if (p == null || !p.IsColonistPlayerControlled || !On) return new List<PickSuggestion>();
            float now = Time.realtimeSinceStartup;
            if (viewFor != p || now - viewAt > 1f || viewAt > now)
            {
                viewFor = p;
                viewAt = now;
                view = WeaponPick.Standing(p);
                viewPicks = WeaponPick.Current(p.Map);
                lines.Clear();
                if (viewPicks != null)
                    foreach (var s in view.Take(3))
                        lines[s] = Make(viewPicks, s);
            }
            picks = viewPicks;
            return view;
        }

        public static void Refresh() => viewAt = -10f;

        /// <summary>The height the game's gear tab gives the lines for a colonist (a heading and a line each, three at most).</summary>
        public static float Height(List<PickSuggestion> list) => list.Count == 0 ? 0f : 30f + Math.Min(3, list.Count) * LineH;

        /// <summary>The lines on the game's gear tab, from <paramref name="y"/>; moves it down past them.</summary>
        public static void Draw(ref float y, float width, Pawn p)
        {
            var list = For(p, out var picks);
            if (list.Count == 0 || picks == null) return;
            Widgets.ListSeparator(ref y, width, "CC_WeaponPick_GearHeader".Translate());
            foreach (var s in list.Take(3))
            {
                Line(new Rect(0f, y, width, LineH - 4f), picks, s);
                y += LineH;
            }
            y += 4f;
        }

        /// <summary>One suggestion: the weapon's picture, the head and the reasons, its buttons (equip, as a sidearm, not now) or "on the way".</summary>
        public static void Line(Rect r, WeaponPick.Picks picks, PickSuggestion s)
        {
            var p = picks.Pawn(s.Colonist);
            var t = picks.Thing(s.Weapon);
            if (p == null || t == null) return;
            if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);
            var icon = new Rect(r.x, r.y + 4f, 28f, 28f);
            Widgets.ThingIcon(icon, t);
            if (!lines.TryGetValue(s, out var v)) lines[s] = v = Make(picks, s);
            bool sidearms = v.sidearms;
            float bw = 70f, sw = sidearms ? 84f : 0f, xw = 22f;
            float buttons = bw + (sidearms ? sw + 4f : 0f) + xw + 6f;
            var text = new Rect(icon.xMax + 6f, r.y, r.width - icon.width - 6f - buttons, r.height);
            UiKit.LabelFit(new Rect(text.x, text.y + 1f, text.width, 22f), v.head, UiKit.Good, GameFont.Small);
            UiKit.LabelFit(new Rect(text.x, text.y + 22f, text.width, 18f), v.why, UiKit.Faint, GameFont.Tiny);
            TooltipHandler.TipRegion(new Rect(r.x, r.y, text.xMax - r.x, r.height), () => Tip(picks, s), s.Weapon.GetHashCode() ^ 0x77aa);
            if (Widgets.ButtonInvisible(new Rect(r.x, r.y, text.xMax - r.x, r.height), false) && WeaponCardWindow.On) WeaponCardWindow.Open(t, p);
            float bx = r.xMax - buttons + 6f, by = r.y + (r.height - 24f) / 2f;
            if (v.onTheWay)
            {
                UiKit.LabelFit(new Rect(bx, by, buttons - 6f, 24f), "CC_WeaponPick_OnTheWay".Translate(), UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                return;
            }
            string label = ("CC_WeaponPick_Act_" + s.Kind).Translate();
            if (UiKit.ActButton(new Rect(bx, by, bw, 24f), label, v.whyNot, v.actTip))
            {
                WeaponPick.DoConfirmed(picks, s);
                Refresh();
            }
            bx += bw + 4f;
            if (sidearms)
            {
                if (UiKit.ActButton(new Rect(bx, by, sw, 24f), "CC_WeaponPick_Act_AsSidearm".Translate(), v.whyNotSidearm, v.sidearmTip))
                {
                    WeaponPick.DoConfirmed(picks, s, true);
                    Refresh();
                }
                bx += sw + 4f;
            }
            var x = new Rect(bx, by + 2f, 20f, 20f);
            if (Widgets.ButtonImage(x, TexButton.CloseXSmall, UiKit.Faint, GenUI.MouseoverColor))
            {
                WeaponPickBook.Instance?.SetNotNow(p, true);
                SoundDefOf.Click.PlayOneShotOnCamera();
                Refresh();
            }
            TooltipHandler.TipRegion(x, v.notNowTip);
        }

        /// <summary>A suggestion's tooltip: the whole line, what it costs, what a click does.</summary>
        public static string Tip(WeaponPick.Picks picks, PickSuggestion s)
        {
            string tip = WeaponPick.Full(picks, s);
            string place = picks.Place(s.Weapon);
            if (!place.NullOrEmpty()) tip += "\n\n" + "CC_WeaponPick_Lies".Translate(WeaponPick.Label(picks, s.Weapon).CapitalizeFirst().Named("WEAPON"), place.Named("PLACE")).Resolve();
            if (WeaponCardWindow.On) tip += "\n\n" + "CC_WeaponPick_ClickCard".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor);
            return tip;
        }

        /// <summary>The choices for a suggestion as a menu (Nice Inventory Tab's mark): equip, as a sidearm, not now, its card.</summary>
        public static void Menu(WeaponPick.Picks picks, List<PickSuggestion> list, Pawn p)
        {
            var opts = new List<FloatMenuOption>();
            foreach (var s in list)
            {
                var t = picks.Thing(s.Weapon);
                string head = WeaponPick.Head(picks, s);
                if (WeaponPick.OnTheWay(picks, s))
                {
                    opts.Add(new FloatMenuOption(head + ": " + "CC_WeaponPick_OnTheWay".Translate(), null));
                    continue;
                }
                string why = WeaponPick.WhyNot(picks, s);
                var pick = s;
                opts.Add(new FloatMenuOption(head + " · " + ("CC_WeaponPick_Act_" + s.Kind).Translate() + (why != null ? " (" + why + ")" : ""),
                    why != null ? (Action)null : () =>
                    {
                        WeaponPick.DoConfirmed(picks, pick);
                        Refresh();
                    }, t, Color.white));
                if (s.Kind == PickKind.Primary && WeaponPick.CanTakeSidearms && picks.input.Weapon(s.Weapon) is PickWeapon w && w.Stored && !w.HandOnly)
                {
                    string sw = WeaponPick.WhyNot(picks, s, true);
                    opts.Add(new FloatMenuOption("CC_WeaponPick_Act_AsSidearmLong".Translate(WeaponPick.Label(picks, s.Weapon).Named("WEAPON")).Resolve() + (sw != null ? " (" + sw + ")" : ""),
                        sw != null ? (Action)null : () =>
                        {
                            WeaponPick.DoConfirmed(picks, pick, true);
                            Refresh();
                        }, t, Color.white));
                }
                if (WeaponCardWindow.On) opts.Add(new FloatMenuOption("CC_WeaponPick_CardOf".Translate(WeaponPick.Label(picks, s.Weapon).Named("WEAPON")).Resolve(), () => WeaponCardWindow.Open(t, p)));
            }
            opts.Add(new FloatMenuOption("CC_WeaponPick_NotNow".Translate(p.Named("PAWN")).Resolve(), () =>
            {
                WeaponPickBook.Instance?.SetNotNow(p, true);
                Refresh();
            }));
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        // ---------------- Nice Inventory Tab, by name ----------------

        internal static readonly Type NiceBox = GenTypes.GetTypeInAnyAssembly("NiceInventoryTab.GroupBox");
        private static readonly Type NicePatch = GenTypes.GetTypeInAnyAssembly("NiceInventoryTab.ITab_Pawn_Gear_Patch");
        private static readonly FieldInfo LegacyBox = NicePatch != null ? AccessTools.Field(NicePatch, "equipment_legacy_groupbox") : null;
        private static readonly FieldInfo AltBox = NicePatch != null ? AccessTools.Field(NicePatch, "equipment_alt_groupbox") : null;
        private static readonly FieldInfo LastPawn = NicePatch != null ? AccessTools.Field(NicePatch, "lastPawn") : null;
        private static readonly FieldInfo Geometry = GenTypes.GetTypeInAnyAssembly("NiceInventoryTab.Widget") is Type wt ? AccessTools.Field(wt, "Geometry") : null;
        private static readonly FieldInfo Visible = GenTypes.GetTypeInAnyAssembly("NiceInventoryTab.Widget") is Type wv ? AccessTools.Field(wv, "Visible") : null;

        /// <summary>On Nice Inventory Tab's equipment box: a small mark at its title's right end ("▲ +1.4 charge rifle"), its tooltip the lines, a click the choices.</summary>
        internal static void NiceMark(object box)
        {
            if (box == null || LegacyBox == null || Geometry == null || LastPawn == null) return;
            if (box != LegacyBox.GetValue(null) && box != AltBox?.GetValue(null)) return;
            if (Visible?.GetValue(box) is bool v && !v) return;
            var p = LastPawn.GetValue(null) as Pawn;
            var list = For(p, out var picks);
            if (list.Count == 0 || picks == null) return;
            var g = (Rect)Geometry.GetValue(box);
            var best = list[0];
            string text = WeaponPickMath.GainText(best.Gain) + " " + WeaponPick.Label(picks, best.Weapon);
            float w = Mathf.Min(UiKit.KeptWidth(text, GameFont.Tiny) + 26f, Mathf.Max(60f, g.width * 0.4f));
            var r = new Rect(g.xMax - w - 3f, g.y + 1f, w, 18f);
            UiKit.RoundBox(r, new Color(0f, 0f, 0f, 0.55f), UiKit.WithAlpha(UiKit.Good, 0.8f), SkinMath.RadiusSmall);
            UiKit.Icon(new Rect(r.x + 4f, r.center.y - 6f, 12f, 12f), Icons.TrendUp, UiKit.Good);
            UiKit.LabelFit(new Rect(r.x + 19f, r.y, r.width - 21f, r.height), text, UiKit.Good, GameFont.Tiny);
            if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);
            TooltipHandler.TipRegion(r, () => string.Join("\n\n", list.Take(3).Select(s => WeaponPick.Head(picks, s) + "\n" + WeaponPick.Full(picks, s))) + "\n\n" +
                                              "CC_WeaponPick_NiceClick".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor), 0x5e1f);
            if (Widgets.ButtonInvisible(r)) Menu(picks, list, p);
        }
    }

    /// <summary>The suggested weapons' lines on the game's own gear tab, under the comfortable temperatures (the tab's scroll height follows).</summary>
    [HarmonyPatch(typeof(ITab_Pawn_Gear), "TryDrawComfyTemperatureRange")]
    public static class Patch_WeaponPickGearTab
    {
        private static bool broken;
        private static readonly MethodInfo SelPawn = AccessTools.PropertyGetter(typeof(ITab_Pawn_Gear), "SelPawnForGear");

        public static void Postfix(ITab_Pawn_Gear __instance, ref float curY, float width)
        {
            if (broken || !WeaponPickGearTab.On) return;
            try
            {
                if (SelPawn?.Invoke(__instance, null) is Pawn p) WeaponPickGearTab.Draw(ref curY, width, p);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the suggested weapons on the gear tab", e);
            }
        }
    }

    /// <summary>The suggested weapons' mark on Nice Inventory Tab's equipment box, read by name; skipped without the mod.</summary>
    [HarmonyPatch]
    public static class Patch_WeaponPickNiceBox
    {
        private static bool broken;

        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        public static string StandsDownWhy() => WeaponPickGearTab.NiceBox == null ? "Nice Inventory Tab isn't loaded" : "";

        private static MethodBase Target() => WeaponPickGearTab.NiceBox is Type t ? AccessTools.Method(t, "Draw") : null;

        public static void Postfix(object __instance)
        {
            if (broken || !WeaponPickGearTab.On) return;
            try
            {
                WeaponPickGearTab.NiceMark(__instance);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the suggested weapons on Nice Inventory Tab", e);
            }
        }
    }

    /// <summary>
    /// The colony's view on the Defenses page ("Weapons for each colonist"): every colonist with what they hold and its
    /// rating for them, their suggestions with the gain and why, a button each, "not now" or "ask again", and "Equip all
    /// suggested" at its heading, which only gives the orders. What the choice weighs is said under the heading. In
    /// Rimfolk's look with the kit; its words worked out once a second.
    /// </summary>
    public static class WeaponPickPanel
    {
        public const string FoldKey = "weapons";

        private static bool focus;
        private static Map modelFor;
        private static float modelAt = -10f;
        private static WeaponPick.Picks modelPicks;
        private static readonly List<Row> rows = new List<Row>();
        private static string weighs;
        private static int doable;

        private sealed class Row
        {
            public Pawn p;
            public Thing held;
            public float? heldRating;
            public string heldLabel, cant;
            public bool notNow;
            public readonly List<(PickSuggestion s, string head, string why, string whyNot, bool onTheWay)> picks = new List<(PickSuggestion, string, string, string, bool)>();
        }

        /// <summary>Opens the Defenses page at this view (the inbox's act).</summary>
        public static void Show(Map map)
        {
            focus = true;
            if (map != null && Find.CurrentMap != map) Current.Game.CurrentMap = map;
            Pages.Open("Defenses");
        }

        /// <summary>The page asked to show this view: where to scroll and that it should be open.</summary>
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
            modelPicks = WeaponPick.For(map);
            doable = 0;
            weighs = null;
            var picks = modelPicks;
            if (picks == null) return;
            var book = WeaponPickBook.Instance;
            foreach (var p in WeaponPick.Colonists(map).OrderBy(x => x.LabelShort))
            {
                var row = new Row { p = p, held = p.equipment.Primary, notNow = book?.IsNotNow(p) ?? false };
                if (p.WorkTagIsDisabled(WorkTags.Violent)) row.cant = "CC_Weapon_CantFight".Translate(p.Named("PAWN")).Resolve();
                if (row.held != null)
                {
                    row.heldLabel = row.held.LabelCapNoCount;
                    var r = WeaponLive.Of(row.held);
                    if (r != null && row.cant == null) row.heldRating = WeaponPick.Rate(p, r);
                }
                foreach (var s in picks.Of(p).Where(x => WeaponPick.Stands(picks, x)).OrderBy(x => (int)x.Kind))
                {
                    bool onTheWay = WeaponPick.OnTheWay(picks, s);
                    string whyNot = onTheWay ? null : WeaponPick.WhyNot(picks, s);
                    row.picks.Add((s, WeaponPick.Head(picks, s), WeaponPick.Why(picks, s).CapitalizeFirst(), whyNot, onTheWay));
                    if (!onTheWay && whyNot == null && WeaponPick.Confirm(picks, s).NullOrEmpty()) doable++;
                }
                rows.Add(row);
            }
            var sits = picks.input.Colony.Where(kv => kv.Value >= 0.05f).OrderByDescending(kv => kv.Value).Select(kv => ("CC_WeaponPick_Sit_" + WeaponPick.SituationWord(kv.Key)).Translate().Resolve()).ToList();
            weighs = (sits.Count > 0 ? "CC_WeaponPick_Weighs".Translate(sits.ToCommaList(true).Named("FIGHTS")).Resolve() : "CC_WeaponPick_WeighsNone".Translate().Resolve()) +
                     " " + "CC_WeaponPick_Stored".Translate(picks.stored.Named("COUNT")).Resolve();
        }

        /// <summary>The view in the Defenses page's right column, from <paramref name="y"/>; returns the y under it.</summary>
        public static float Draw(Map map, float x, float y, float w, Folds folds)
        {
            if (!WeaponPick.On || map == null) return y;
            if (modelFor != map || Time.realtimeSinceStartup - modelAt > 1f) Build(map);
            int count = rows.Sum(r => r.picks.Count);
            string why = doable > 0 ? null : "CC_WeaponPick_AllNone".Translate().Resolve();
            y += UiKit.SectionHeading(x, y, w, "CC_WeaponPick_Section".Translate(), count, folds, FoldKey, out bool open, "CC_WeaponPick_AllAct".Translate(doable.Named("COUNT")).Resolve(), why,
                out bool acted, "CC_WeaponPick_AllAct_Tip".Translate().Resolve());
            if (acted && modelPicks != null)
            {
                // Only the orders: each row turns to "on the way", and the map shows them walking.
                WeaponPick.DoAll(modelPicks);
                SoundDefOf.Click.PlayOneShotOnCamera();
                modelAt = -10f;
            }
            if (!open) return y;
            if (!weighs.NullOrEmpty()) y += UiKit.Paragraph(x, y, w, weighs, UiKit.Faint, GameFont.Tiny) + 6f;
            if (rows.Count == 0) return y + UiKit.EmptyState(x, y, w, Icons.CatDefense, "CC_WeaponPick_NoColonists".Translate(), 70f) + 4f;
            foreach (var row in rows) y = DrawRow(row, x, y, w) + 4f;
            return y;
        }

        private static float DrawRow(Row row, float x, float y, float w)
        {
            var picks = modelPicks;
            float h = 30f + Mathf.Max(1, row.picks.Count) * 40f;
            var box = new Rect(x, y, w, h);
            UiKit.Card(box, row.picks.Count > 0 && !row.notNow ? UiKit.WithAlpha(UiKit.Good, 0.5f) : (Color?)null, false, false);
            var face = new Rect(x + 6f, y + 4f, 24f, 24f);
            UiKit.Portrait(face, row.p, UiKit.Faint);
            if (Widgets.ButtonInvisible(face, false))
            {
                Cards.Click();
                Cards.Open(row.p);
            }
            UiKit.LabelFit(new Rect(face.xMax + 6f, y + 4f, 120f, 24f), row.p.LabelShortCap, UiKit.Ink, GameFont.Small);
            // What they hold, rated for them.
            float hx = face.xMax + 130f;
            if (row.held != null)
            {
                Widgets.ThingIcon(new Rect(hx, y + 6f, 20f, 20f), row.held);
                if (row.heldRating.HasValue) WeaponUi.Badge(new Rect(hx + 24f, y + 7f, 32f, 18f), row.heldRating.Value);
                UiKit.LabelFit(new Rect(hx + 60f, y + 4f, w - (hx - x) - 64f - 120f, 24f), row.heldLabel, UiKit.Faint, GameFont.Small);
            }
            else UiKit.LabelFit(new Rect(hx, y + 4f, w - (hx - x) - 124f, 24f), "CC_WeaponPick_HoldsNothing".Translate(), UiKit.Faint, GameFont.Small);
            // Not now, or asked again.
            var aside = new Rect(x + w - 116f, y + 4f, 110f, 22f);
            if (row.notNow)
            {
                if (UiKit.ActButton(aside, "CC_WeaponPick_AskAgain".Translate(), null, "CC_WeaponPick_AskAgain_Tip".Translate(row.p.Named("PAWN")).Resolve()))
                {
                    WeaponPickBook.Instance?.SetNotNow(row.p, false);
                    modelAt = -10f;
                }
            }
            else if (row.picks.Count > 0 && UiKit.ActButton(aside, "CC_WeaponPick_NotNowShort".Translate(), null,
                         "CC_WeaponPick_NotNow_Tip".Translate(row.p.Named("PAWN"), WeaponAskMath.NotNowDays.Named("DAYS")).Resolve()))
            {
                WeaponPickBook.Instance?.SetNotNow(row.p, true);
                modelAt = -10f;
            }
            float ly = y + 30f;
            if (row.picks.Count == 0)
            {
                string none = row.cant ?? (row.notNow ? "CC_WeaponPick_SetAside".Translate(row.p.Named("PAWN")).Resolve() : "CC_WeaponPick_KeepsIt".Translate(row.p.Named("PAWN")).Resolve());
                UiKit.LabelFit(new Rect(face.xMax + 6f, ly + 4f, w - 40f, 22f), none, UiKit.Faint, GameFont.Tiny);
                return y + h;
            }
            foreach (var (s, head, why, whyNot, onTheWay) in row.picks)
            {
                var line = new Rect(x + 30f, ly, w - 36f, 38f);
                var t = picks.Thing(s.Weapon);
                if (t != null) Widgets.ThingIcon(new Rect(line.x, ly + 4f, 24f, 24f), t);
                WeaponUi.Badge(new Rect(line.x + 28f, ly + 7f, 32f, 18f), s.Rating);
                float bw = 96f;
                var text = new Rect(line.x + 64f, ly, line.width - 64f - bw - 6f, 38f);
                UiKit.LabelFit(new Rect(text.x, ly, text.width, 20f), head, row.notNow ? UiKit.Faint : UiKit.Good, GameFont.Small);
                UiKit.LabelFit(new Rect(text.x, ly + 19f, text.width, 18f), why, UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(new Rect(line.x, ly, text.xMax - line.x, 38f), () => WeaponPickGearTab.Tip(picks, s), s.Weapon.GetHashCode() ^ 0x1d3);
                if (t != null && WeaponCardWindow.On && Widgets.ButtonInvisible(new Rect(line.x, ly, text.xMax - line.x, 38f), false)) WeaponCardWindow.Open(t, row.p);
                var b = new Rect(line.xMax - bw, ly + 7f, bw, 24f);
                if (onTheWay) UiKit.LabelFit(b, "CC_WeaponPick_OnTheWay".Translate(), UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                else if (UiKit.ActButton(b, ("CC_WeaponPick_Act_" + s.Kind).Translate(), whyNot,
                             ("CC_WeaponPick_Act_" + s.Kind + "_Tip").Translate(row.p.Named("PAWN")).Resolve()))
                {
                    WeaponPick.DoConfirmed(picks, s);
                    modelAt = -10f;
                }
                ly += 40f;
            }
            return y + h;
        }
    }
}
