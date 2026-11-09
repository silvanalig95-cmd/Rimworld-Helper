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
    /// <summary>How a suggestion's row is drawn: Nice Inventory Tab's dark boxes, its colours and flat buttons, or the game's own look.</summary>
    public sealed class SuggestLook
    {
        public Color ground, tile, ink, faint, good, bad, warn, frame, buttonFill, buttonHover, buttonInk;
        /// <summary>The game's own buttons (the game's gear tab); else flat ones in the look's colours.</summary>
        public bool gameButtons;
    }

    /// <summary>
    /// The best weapon for each colonist where the player looks (docs/design/weapons.md, step 3): on the gear tab, the game's
    /// and Nice Inventory Tab's (Weapons/WeaponNiceTab.cs) alike, a row for each suggestion that stands, marked as one (a
    /// dashed, softly tinted frame and a "Suggested" tag, so it never reads as a weapon held): the weapon's own picture, its
    /// name with its quality, its rating for the colonist and the gain, why in a line (the whole line on the tooltip), a
    /// warning when it gives up something that matters (a lucky weapon, an heirloom, a bonded persona weapon), and its
    /// buttons (Equip, As sidearm with Simple Sidearms, Off hand with Dual Wield, Trade with whom, Not now), each a real job
    /// walked out on the map. With nothing to suggest, one quiet line says so ("Say when there's no better weapon"). What
    /// is drawn is read from the colony's kept choice, worked out with its words once a real second.
    /// </summary>
    public static class WeaponPickGearTab
    {
        public static bool On => RimfolkPatches.On && WeaponPick.On;

        /// <summary>"Say when there's no better weapon": the quiet line when there is nothing to suggest.</summary>
        public static bool QuietOn => On && (ColonyCouncilMod.Settings?.weaponNoBetterLine ?? true);

        /// <summary>The rows shown at most for a colonist (in hand, a sidearm, the off hand).</summary>
        public const int MaxRows = 3;

        private static Pawn viewFor;
        private static float viewAt = -10f;
        private static WeaponPick.Picks viewPicks;
        private static List<PickSuggestion> view = new List<PickSuggestion>();
        private static Quiet quiet;
        private static string quietText;

        /// <summary>What the quiet line says: nothing (none shown), no better weapon, set aside, can't fight.</summary>
        private enum Quiet
        {
            None,
            NoBetter,
            SetAside,
            CantFight
        }

        /// <summary>What a row shows, worked out with the view (once a real second), so nothing is asked of the map per frame.</summary>
        private sealed class LineView
        {
            public string head, why, whyNot, whyNotSidearm, actTip, sidearmTip, notNowTip, name, gain, act, warning, tag;
            public bool onTheWay, sidearms, asked;
            public float actW, sidearmW;
        }

        private static readonly Dictionary<PickSuggestion, LineView> lines = new Dictionary<PickSuggestion, LineView>();

        private static float ButtonW(string label) => Mathf.Max(48f, UiKit.KeptWidth(label, GameFont.Tiny) + 16f);

        private static LineView Make(WeaponPick.Picks picks, PickSuggestion s)
        {
            var p = picks.Pawn(s.Colonist);
            var t = picks.Thing(s.Weapon);
            var v = new LineView
            {
                head = WeaponPick.Head(picks, s), why = WeaponPick.Why(picks, s).CapitalizeFirst(), onTheWay = WeaponPick.OnTheWay(picks, s),
                name = t?.LabelCapNoCount ?? WeaponPick.Label(picks, s.Weapon).CapitalizeFirst(), gain = WeaponPickMath.GainText(s.Gain)
            };
            v.sidearms = s.Kind == PickKind.Primary && WeaponPick.CanTakeSidearms && picks.input.Weapon(s.Weapon) is PickWeapon w && w.Stored && !w.HandOnly &&
                         w.Mass <= (w.Ranged ? picks.input.Rules.SidearmRangedMass : picks.input.Rules.SidearmMeleeMass) + 0.001f;
            if (!v.onTheWay)
            {
                v.whyNot = WeaponPick.WhyNot(picks, s);
                if (v.sidearms) v.whyNotSidearm = WeaponPick.WhyNot(picks, s, true);
            }
            v.act = s.Kind == PickKind.Swap ? "CC_WeaponPick_Act_SwapWith".Translate(picks.Pawn(s.Other).Named("OTHER")).Resolve() : ("CC_WeaponPick_Act_" + s.Kind).Translate().Resolve();
            v.actW = ButtonW(v.act);
            v.sidearmW = ButtonW("CC_WeaponPick_Act_AsSidearm".Translate());
            v.actTip = ("CC_WeaponPick_Act_" + s.Kind + "_Tip").Translate(p.Named("PAWN")).Resolve();
            v.sidearmTip = "CC_WeaponPick_Act_AsSidearm_Tip".Translate(p.Named("PAWN")).Resolve();
            v.notNowTip = "CC_WeaponPick_NotNow_Tip".Translate(p.Named("PAWN"), WeaponAskMath.NotNowDays.Named("DAYS")).Resolve();
            // What it costs, said on its own line: a keepsake, a bonded persona weapon, a hunter's hunts.
            var costs = s.Costs.Select(r => WeaponPick.Words(picks, s, r)).Where(x => !x.NullOrEmpty()).ToList();
            if (costs.Count > 0) v.warning = string.Join("; ", costs).CapitalizeFirst();
            // Asked for lately (by them, or in a trade by the other): the tag says so.
            var book = WeaponPickBook.Instance;
            int now = Find.TickManager?.TicksGame ?? 0;
            bool AskedBy(Pawn who, Thing weapon) => who != null && weapon != null && book?.AskOf(who) is WeaponAskNote a && a.weapon == weapon.thingIDNumber && WeaponAskMath.Shows(now, a.tick, 0);
            v.asked = AskedBy(p, t) || (s.Kind == PickKind.Swap && AskedBy(picks.Pawn(s.Other), picks.Thing(s.Instead)));
            v.tag = (v.asked ? "CC_WeaponPick_TagAsked" : "CC_WeaponPick_TagSuggested").Translate();
            return v;
        }

        /// <summary>A colonist's suggestions that stand, worked out at most once a real second; the quiet line's state with them.</summary>
        public static List<PickSuggestion> For(Pawn p, out WeaponPick.Picks picks)
        {
            picks = null;
            if (p == null || !p.IsColonistPlayerControlled || !On)
            {
                quiet = Quiet.None;
                return new List<PickSuggestion>();
            }
            float now = Time.realtimeSinceStartup;
            if (viewFor != p || now - viewAt > 1f || viewAt > now)
            {
                viewFor = p;
                viewAt = now;
                view = WeaponPick.Standing(p);
                viewPicks = WeaponPick.Current(p.Map);
                lines.Clear();
                if (viewPicks != null)
                    foreach (var s in view.Take(MaxRows))
                        lines[s] = Make(viewPicks, s);
                MakeQuiet(p);
            }
            picks = viewPicks;
            return view;
        }

        /// <summary>The quiet line, when there is nothing to suggest for one of the colonists the choice is for.</summary>
        private static void MakeQuiet(Pawn p)
        {
            quiet = Quiet.None;
            quietText = null;
            if (view.Count > 0 || viewPicks == null || !QuietOn || !p.Spawned || !WeaponPick.Colonists(p.Map).Contains(p)) return;
            if (WeaponPickBook.Instance?.IsNotNow(p) == true)
            {
                quiet = Quiet.SetAside;
                quietText = "CC_WeaponPick_QuietSetAside".Translate(p.Named("PAWN")).Resolve();
            }
            else if (p.WorkTagIsDisabled(WorkTags.Violent))
            {
                quiet = Quiet.CantFight;
                quietText = "CC_WeaponPick_QuietCantFight".Translate(p.Named("PAWN")).Resolve();
            }
            else
            {
                quiet = Quiet.NoBetter;
                quietText = "CC_WeaponPick_QuietNone".Translate(p.Named("PAWN")).Resolve();
            }
        }

        public static void Refresh() => viewAt = -10f;

        private static bool HasWarning(PickSuggestion s) => lines.TryGetValue(s, out var v) && v.warning != null;

        /// <summary>The room the suggestions take (the rows, or the quiet line), for Nice Inventory Tab's box; 0 for none.</summary>
        public static float Height(Pawn p)
        {
            var list = For(p, out var picks);
            if (list.Count > 0 && picks != null) return WeaponNiceMath.Height(list.Take(MaxRows).Select(HasWarning).ToList(), false);
            return WeaponNiceMath.Height(null, quiet != Quiet.None);
        }

        // ---------------- The looks ----------------

        /// <summary>The game's own gear tab: its plain text, the kit's tokens in the game's look, the game's buttons.</summary>
        private static SuggestLook gameLook;

        private static SuggestLook GameLook => gameLook ??= new SuggestLook
        {
            ground = new Color(1f, 1f, 1f, 0.03f), tile = new Color(0.07f, 0.08f, 0.09f, 1f), ink = Color.white, faint = new Color(0.7f, 0.7f, 0.7f),
            good = new Color(0.52f, 0.85f, 0.5f), bad = new Color(0.93f, 0.45f, 0.38f), warn = new Color(0.95f, 0.66f, 0.28f),
            frame = new Color(0.52f, 0.85f, 0.5f, 0.7f), gameButtons = true
        };

        // ---------------- Drawing ----------------

        /// <summary>The rows (or the quiet line) in a rect from its top, for a colonist; returns the height used.</summary>
        public static float Rows(Rect r, Pawn p, SuggestLook look)
        {
            var list = For(p, out var picks);
            if (list.Count == 0 || picks == null)
            {
                if (quiet == Quiet.None) return 0f;
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
            return y - r.y;
        }

        /// <summary>The game's gear tab: "Suggested weapons" and the rows, or the quiet line, from <paramref name="y"/>; moves it down past them.</summary>
        public static void Draw(ref float y, float width, Pawn p)
        {
            var list = For(p, out var picks);
            if ((list.Count == 0 || picks == null) && quiet == Quiet.None) return;
            if (list.Count > 0 && picks != null) Widgets.ListSeparator(ref y, width, "CC_WeaponPick_GearHeader".Translate());
            else y += 4f;
            y += Rows(new Rect(0f, y, width, 9999f), p, GameLook) + 6f;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        /// <summary>
        /// One suggestion's row: the dashed, softly tinted frame and its tag; the weapon's own picture (UiKit.ItemPortrait, its
        /// quality's frame); its name and quality, its rating for them and the gain; why in a line; a warning when it gives
        /// up something that matters; its buttons, or "on the way". A click on the picture or the words opens its card.
        /// </summary>
        public static void Row(Rect r, WeaponPick.Picks picks, PickSuggestion s, SuggestLook look)
        {
            var p = picks.Pawn(s.Colonist);
            var t = picks.Thing(s.Weapon);
            if (p == null || t == null) return;
            if (!lines.TryGetValue(s, out var v)) lines[s] = v = Make(picks, s);
            // Proposed, never held: a soft tint and dashes.
            if (Event.current.type == EventType.Repaint)
            {
                Widgets.DrawBoxSolid(r, look.ground);
                Widgets.DrawBoxSolid(r, UiKit.WithAlpha(look.good, Mouse.IsOver(r) ? 0.09f : 0.05f));
            }
            UiKit.DashedBox(r, look.frame);
            float m = WeaponNiceMath.Margin;
            var tile = new Rect(r.x + m, r.y + m, WeaponNiceMath.TileW, WeaponNiceMath.TileH);
            UiKit.ItemPortrait(tile, t, ground: look.tile, tip: () => Tip(picks, s));
            float x = tile.xMax + WeaponNiceMath.TileGap, right = r.xMax - m, ly = r.y + m;
            // The first line: the tag, the name with its quality, the badge and the gain.
            Text.Font = GameFont.Tiny;
            float tagW = UiKit.KeptWidth(v.tag, GameFont.Tiny) + 12f;
            var tagR = new Rect(x, ly + 2f, tagW, 16f);
            if (Event.current.type == EventType.Repaint) UiKit.RoundBox(tagR, UiKit.WithAlpha(look.good, 0.14f), UiKit.WithAlpha(look.good, 0.6f), SkinMath.RadiusSmall);
            Label(tagR, v.tag, look.good, GameFont.Tiny, TextAnchor.MiddleCenter);
            float gainW = 32f;
            var gainR = new Rect(right - gainW, ly, gainW, 20f);
            Label(gainR, v.gain, s.Gain >= 0f ? look.good : look.bad, GameFont.Small, TextAnchor.MiddleRight);
            var badge = new Rect(gainR.x - 4f - WeaponGearTab.BadgeW, ly + 1.5f, WeaponGearTab.BadgeW, WeaponGearTab.BadgeH);
            WeaponUi.Badge(badge, s.Rating);
            var nameR = new Rect(tagR.xMax + 6f, ly, badge.x - 6f - tagR.xMax - 6f, 20f);
            Label(nameR, v.name, look.ink, GameFont.Small);
            // Why, in a line (the whole of it on the tooltip).
            Label(new Rect(x, ly + 20f, right - x, 17f), v.why, look.faint, GameFont.Tiny);
            var words = new Rect(x, r.y, right - x, ly + 37f - r.y);
            TooltipHandler.TipRegion(words, () => Tip(picks, s), s.Weapon.GetHashCode() ^ 0x77aa);
            if (WeaponCardWindow.On && Widgets.ButtonInvisible(new Rect(tile.x, tile.y, tile.width, tile.height), false)) WeaponCardWindow.Open(t, p);
            if (WeaponCardWindow.On && Widgets.ButtonInvisible(new Rect(x, ly, right - x, 37f), false)) WeaponCardWindow.Open(t, p);
            // The buttons, or "on the way".
            float by = ly + 38f, bh = 20f;
            if (v.onTheWay) Label(new Rect(x, by, right - x, bh), "CC_WeaponPick_OnTheWay".Translate(), look.faint, GameFont.Tiny);
            else
            {
                float bx = x;
                if (Button(new Rect(bx, by, v.actW, bh), v.act, v.whyNot, v.actTip, look))
                {
                    WeaponPick.DoConfirmed(picks, s);
                    Refresh();
                }
                bx += v.actW + 4f;
                if (v.sidearms)
                {
                    if (Button(new Rect(bx, by, v.sidearmW, bh), "CC_WeaponPick_Act_AsSidearm".Translate(), v.whyNotSidearm, v.sidearmTip, look))
                    {
                        WeaponPick.DoConfirmed(picks, s, true);
                        Refresh();
                    }
                    bx += v.sidearmW + 4f;
                }
                string notNow = "CC_WeaponPick_NotNowShort".Translate();
                float nw = ButtonW(notNow);
                bool word = WeaponNiceMath.NotNowAsWord(right - x, bx - 4f - x, nw);
                var nr = word ? new Rect(right - nw, by, nw, bh) : new Rect(right - WeaponNiceMath.CrossW, by, WeaponNiceMath.CrossW, bh);
                bool clicked = word ? Button(nr, notNow, null, v.notNowTip, look) : Widgets.ButtonImage(nr.ContractedBy(2f), TexButton.CloseXSmall, look.faint, GenUI.MouseoverColor);
                if (!word) TooltipHandler.TipRegion(nr, v.notNowTip);
                if (clicked)
                {
                    WeaponPickBook.Instance?.SetNotNow(p, true);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    Refresh();
                }
            }
            // What it gives up that matters, under the buttons.
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

        /// <summary>One line of text cut to fit, without a tooltip of its own (the row's covers it).</summary>
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

        /// <summary>A row's button: the game's own on its tab; a flat one in Nice Inventory Tab's colours on its box. Greyed with why when it can't be done now.</summary>
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

        /// <summary>
        /// The quiet line: "No better weapon in storage" (its tooltip what was compared, a click the colony's view on the
        /// Defenses page), "Suggestions set aside for now" (a click brings them back), or that they don't fight.
        /// </summary>
        private static void QuietLine(Rect r, Pawn p, SuggestLook look)
        {
            if (quietText == null) return;
            bool over = Mouse.IsOver(r);
            if (over && quiet != Quiet.CantFight) Widgets.DrawHighlight(r);
            UiKit.Icon(new Rect(r.x + 4f, r.center.y - 7f, 14f, 14f), Icons.CatDefense, UiKit.WithAlpha(look.faint, 0.8f));
            Label(new Rect(r.x + 24f, r.y, r.width - 28f, r.height), quietText, look.faint, GameFont.Tiny);
            var who = p;
            var kind = quiet;
            TooltipHandler.TipRegion(r, () => QuietTip(who, kind), who.thingIDNumber ^ 0x0b7e);
            if (quiet == Quiet.CantFight || !Widgets.ButtonInvisible(r)) return;
            if (quiet == Quiet.SetAside)
            {
                WeaponPickBook.Instance?.SetNotNow(p, false);
                Refresh();
            }
            else WeaponPickPanel.Show(p.Map);
        }

        /// <summary>What was compared, made only while the mouse is over the quiet line: what they hold and its rating for them, the weapons in storage, the fights weighed.</summary>
        private static string QuietTip(Pawn p, Quiet kind)
        {
            if (kind == Quiet.SetAside) return "CC_WeaponPick_QuietSetAside_Tip".Translate(p.Named("PAWN")).Resolve();
            if (kind == Quiet.CantFight) return "CC_Weapon_CantFight".Translate(p.Named("PAWN")).Resolve();
            var picks = WeaponPick.Current(p.Map);
            var held = p.equipment?.Primary;
            var report = held != null ? WeaponLive.Of(held) : null;
            string heldText = held == null ? "CC_WeaponPick_HoldsNothing".Translate().Resolve()
                : report != null ? "CC_WeaponPick_QuietHeld".Translate(held.LabelNoCount.Named("WEAPON"), WeaponForMath.RatingText(WeaponPick.Rate(p, report)).Named("RATING"), p.Named("PAWN")).Resolve()
                : held.LabelNoCount;
            var fights = picks?.input.Colony.Where(kv => kv.Value >= 0.05f).OrderByDescending(kv => kv.Value)
                .Select(kv => ("CC_WeaponPick_Sit_" + WeaponPick.SituationWord(kv.Key)).Translate().Resolve()).ToList() ?? new List<string>();
            string weighs = fights.Count > 0 ? "CC_WeaponPick_Weighs".Translate(fights.ToCommaList(true).Named("FIGHTS")).Resolve() : "CC_WeaponPick_WeighsNone".Translate().Resolve();
            return "CC_WeaponPick_QuietNone_Tip".Translate(p.Named("PAWN"), heldText.Named("HELD"), (picks?.stored ?? 0).Named("COUNT"),
                       WeaponForMath.RatingText(WeaponPickMath.Threshold).Named("GAIN")).Resolve() + " " + weighs + "\n\n" +
                   "CC_WeaponPick_QuietClick".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor);
        }

        /// <summary>A suggestion's tooltip: the whole line, what it costs, where it lies, what a click does.</summary>
        public static string Tip(WeaponPick.Picks picks, PickSuggestion s)
        {
            string tip = WeaponPick.Full(picks, s);
            string place = picks.Place(s.Weapon);
            if (!place.NullOrEmpty()) tip += "\n\n" + "CC_WeaponPick_Lies".Translate(WeaponPick.Label(picks, s.Weapon).CapitalizeFirst().Named("WEAPON"), place.Named("PLACE")).Resolve();
            if (WeaponCardWindow.On) tip += "\n\n" + "CC_WeaponPick_ClickCard".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor);
            return tip;
        }
    }

    /// <summary>The suggested weapons' rows on the game's own gear tab, under the comfortable temperatures (the tab's scroll height follows).</summary>
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
                    string why = WeaponPick.Why(picks, s).CapitalizeFirst();
                    string costs = WeaponPick.Costs(picks, s);
                    if (!costs.NullOrEmpty()) why += (why.NullOrEmpty() ? "" : ". ") + costs;
                    row.picks.Add((s, WeaponPick.Head(picks, s), why, whyNot, onTheWay));
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
                UiKit.ItemPortrait(new Rect(hx, y + 5f, 22f, 22f), row.held);
                if (row.heldRating.HasValue) WeaponUi.Badge(new Rect(hx + 26f, y + 7f, 32f, 18f), row.heldRating.Value);
                UiKit.LabelFit(new Rect(hx + 62f, y + 4f, w - (hx - x) - 66f - 120f, 24f), row.heldLabel, UiKit.Faint, GameFont.Small);
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
                if (t != null) UiKit.ItemPortrait(new Rect(line.x, ly + 4f, 26f, 26f), t);
                WeaponUi.Badge(new Rect(line.x + 30f, ly + 8f, 32f, 18f), s.Rating);
                float bw = 112f;
                var text = new Rect(line.x + 66f, ly, line.width - 66f - bw - 6f, 38f);
                UiKit.LabelFit(new Rect(text.x, ly, text.width, 20f), head, row.notNow ? UiKit.Faint : s.Gain >= 0f ? UiKit.Good : UiKit.Warn, GameFont.Small);
                UiKit.LabelFit(new Rect(text.x, ly + 19f, text.width, 18f), why, UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(new Rect(line.x, ly, text.xMax - line.x, 38f), () => WeaponPickGearTab.Tip(picks, s), s.Weapon.GetHashCode() ^ 0x1d3);
                if (t != null && WeaponCardWindow.On && Widgets.ButtonInvisible(new Rect(line.x, ly, text.xMax - line.x, 38f), false)) WeaponCardWindow.Open(t, row.p);
                var b = new Rect(line.xMax - bw, ly + 7f, bw, 24f);
                string act = s.Kind == PickKind.Swap ? "CC_WeaponPick_Act_SwapWith".Translate(picks.Pawn(s.Other).Named("OTHER")).Resolve() : ("CC_WeaponPick_Act_" + s.Kind).Translate().Resolve();
                if (onTheWay) UiKit.LabelFit(b, "CC_WeaponPick_OnTheWay".Translate(), UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                else if (UiKit.ActButton(b, act, whyNot, ("CC_WeaponPick_Act_" + s.Kind + "_Tip").Translate(row.p.Named("PAWN")).Resolve()))
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
