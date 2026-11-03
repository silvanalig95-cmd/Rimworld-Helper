using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The journey planner's "Add from the stores" (UI/JourneyDialog.cs): everything in the colony's stores on the map
    /// and in the travellers' packs, by kind (food, medicine, drugs, apparel, weapons, ammunition when there is any,
    /// tools and gear, silver and trade goods, materials, everything) and by name, with how many there are and what one
    /// weighs. A click puts one on the list (Shift 10, Ctrl 100), "All" all there is; the line's kind follows the thing
    /// (food the travellers eat, medicine, a bedroll, a tent, feed, or other, which goes with the caravan's goods). The
    /// window stays open to pick several, and the planner beside it shows the list and its totals as they change.
    /// </summary>
    public class Dialog_JourneyPick : Window
    {
        /// <summary>The picker's tabs.</summary>
        public enum Shelf
        {
            Food,
            Medicine,
            Drugs,
            Apparel,
            Weapons,
            Ammo,
            Tools,
            Trade,
            Materials,
            All
        }

        private sealed class Entry
        {
            public ThingDef def;
            public int stores, carried;
            public float mass;
            public Shelf shelf;
            public string kind, label, tip;
        }

        private readonly JourneyPlan plan;
        private readonly Action changed;
        private readonly Rect place;
        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<Entry> shown = new List<Entry>();
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        /// <summary>The tab last looked at, this session.</summary>
        private static Shelf shelf = Shelf.All;
        private float countedAt = -99f;
        private Vector2 scroll;

        private const float RowH = 30f, RefreshSeconds = 2f;

        public Dialog_JourneyPick(JourneyPlan plan, Action changed, Rect place)
        {
            this.plan = plan;
            this.changed = changed;
            this.place = place;
            doCloseX = true;
            doCloseButton = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
            forcePause = true;
            preventCameraMotion = false;
        }

        public override Vector2 InitialSize => new Vector2(Mathf.Min(580f, UI.screenWidth - 20f), Mathf.Min(700f, UI.screenHeight - 20f));

        protected override void SetInitialSizeAndPosition()
        {
            if (place.width <= 0f || place.height <= 0f) base.SetInitialSizeAndPosition();
            else windowRect = place.Rounded();
        }

        // ---------------- What there is ----------------

        /// <summary>The tab a thing is shown under (besides Everything).</summary>
        public static Shelf ShelfOf(ThingDef d)
        {
            if (d.IsNutritionGivingIngestible && !d.IsDrug && !d.IsCorpse) return Shelf.Food;
            if (d.IsMedicine) return Shelf.Medicine;
            if (d.IsDrug) return Shelf.Drugs;
            if (d.IsShell || InCategory(d, "Ammo")) return Shelf.Ammo;
            if (d.IsWithinCategory(ThingCategoryDefOf.Techprints)) return Shelf.Trade;
            if (InCategory(d, "Tool") || (d.building != null && d.building.bed_caravansCanUse) || JourneyPack.IsTent(d) || (d.comps != null && d.comps.Any(c => c is CompProperties_Usable)))
                return Shelf.Tools;
            if (d.IsWeapon) return Shelf.Weapons;
            if (d.IsApparel) return Shelf.Apparel;
            if (d == ThingDefOf.Silver || d == ThingDefOf.Gold || d.defName == "Jade" || d.IsArt || d.IsWithinCategory(ThingCategoryDefOf.BodyParts) ||
                d.IsWithinCategory(ThingCategoryDefOf.Items))
                return Shelf.Trade;
            if (d.IsStuff || d.IsWithinCategory(ThingCategoryDefOf.ResourcesRaw) || d.IsWithinCategory(ThingCategoryDefOf.Manufactured) || d.IsWithinCategory(ThingCategoryDefOf.Chunks))
                return Shelf.Materials;
            return Shelf.All;
        }

        /// <summary>A thing in a category (or under one) whose name holds the word: a mod's ammunition or tools.</summary>
        private static bool InCategory(ThingDef d, string word)
        {
            if (d.thingCategories == null) return false;
            foreach (var c0 in d.thingCategories)
                for (var c = c0; c != null; c = c.parent)
                    if (c.defName.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
            return false;
        }

        private void Count()
        {
            countedAt = Time.realtimeSinceStartup;
            entries.Clear();
            foreach (var kv in JourneyPack.Holdings(plan.map, plan.travellers))
            {
                var d = kv.Key;
                entries.Add(new Entry
                {
                    def = d, stores = kv.Value.stores, carried = kv.Value.carried, mass = JourneyPack.UnitMass(d), shelf = ShelfOf(d), kind = JourneyPack.KindFor(d),
                    label = d.LabelCap.Resolve()
                });
            }
            entries.Sort((a, b) => string.Compare(a.label, b.label, StringComparison.CurrentCultureIgnoreCase));
        }

        private JourneyLine LineOf(ThingDef def) => plan.packing.FirstOrDefault(l => l.def == def && !l.removed) ?? plan.packing.FirstOrDefault(l => l.def == def);

        // ---------------- Drawing ----------------

        public override void DoWindowContents(Rect inRect)
        {
            if (plan?.map == null || !Find.Maps.Contains(plan.map) || Find.WindowStack.WindowOfType<Dialog_JourneyPlan>() == null)
            {
                Close();
                return;
            }
            if (Time.realtimeSinceStartup - countedAt > RefreshSeconds) Count();
            Text.Font = GameFont.Small;
            float y = inRect.y;
            y += UiKit.PageHeader(new Rect(inRect.x, y, inRect.width - 24f, UiKit.HeaderHeight), Icons.Trader, "CC_JourneyPick_Title".Translate(), "CC_JourneyPick_Sub".Translate());
            search.OnGUI(new Rect(inRect.x, y, inRect.width, 24f));
            y += 30f;

            // The tabs, wrapping, each with how many kinds of thing it holds; Ammunition only when there is some.
            float x = inRect.x;
            const float tabH = 24f;
            foreach (Shelf s in Enum.GetValues(typeof(Shelf)))
            {
                int n = s == Shelf.All ? entries.Count : entries.Count(e => e.shelf == s);
                if (n == 0 && s == Shelf.Ammo) continue;
                string label = "CC_JourneyPick_Tab_" + s;
                label = label.Translate() + " (" + n + ")";
                float w = Mathf.Min(inRect.width, UiKit.TabButtonWidth(label, null) - 6f);
                if (x + w > inRect.xMax)
                {
                    x = inRect.x;
                    y += tabH + 3f;
                }
                if (UiKit.TabButton(new Rect(x, y, w, tabH), label, null, shelf == s) && shelf != s)
                {
                    shelf = s;
                    scroll = Vector2.zero;
                }
                x += w + 3f;
            }
            y += tabH + 8f;
            if (shelf == Shelf.Ammo && !entries.Any(e => e.shelf == Shelf.Ammo)) shelf = Shelf.All;

            shown.Clear();
            foreach (var e in entries)
            {
                if (shelf != Shelf.All && e.shelf != shelf) continue;
                if (search.filter.Active && !search.filter.Matches(e.def)) continue;
                shown.Add(e);
            }
            var outer = new Rect(inRect.x, y, inRect.width, inRect.yMax - y - 48f);
            if (shown.Count == 0)
            {
                UiKit.EmptyState(outer, Icons.Trader, "CC_JourneyPick_Nothing".Translate());
                return;
            }
            var view = new Rect(0f, 0f, outer.width - 16f, Mathf.Max(shown.Count * RowH, outer.height - 1f));
            Widgets.BeginScrollView(outer, ref scroll, view);
            for (int i = 0; i < shown.Count; i++)
            {
                float ry = i * RowH;
                if (ry + RowH < scroll.y || ry > scroll.y + outer.height) continue;
                Row(new Rect(0f, ry, view.width, RowH), shown[i], i);
            }
            Widgets.EndScrollView();
        }

        /// <summary>One thing: icon, name, on the list already, how many there are and carried, one's weight, and + and All.</summary>
        private void Row(Rect r, Entry e, int index)
        {
            UiKit.Zebra(r, index);
            Widgets.DrawHighlightIfMouseover(r);
            int most = e.stores + e.carried;
            var line = LineOf(e.def);
            bool listed = line != null && line.Taken;
            Widgets.ThingIcon(new Rect(r.x + 4f, r.y + 3f, 24f, 24f), e.def);

            // From the right: All, +, one's weight, how many.
            float right = r.xMax - 2f;
            var allRect = new Rect(right - 40f, r.y + 3f, 40f, 24f);
            right -= 44f;
            var plusRect = new Rect(right - 26f, r.y + 3f, 26f, 24f);
            right -= 32f;
            string each = "CC_JourneyPick_Each".Translate(e.mass.ToString(e.mass < 10f ? "0.##" : "0")).Resolve();
            Text.Font = GameFont.Tiny;
            float ew = Text.CalcSize(each).x + 6f;
            UiKit.LabelFit(new Rect(right - ew, r.y, ew, r.height), each, UiKit.Dim, GameFont.Tiny, TextAnchor.MiddleRight);
            right -= ew + 6f;
            string count = "×" + e.stores.ToStringCached() + (e.carried > 0 ? " " + "CC_JourneyPick_Carried".Translate(e.carried).Resolve() : "");
            Text.Font = GameFont.Small;
            float cw = Mathf.Min(140f, Text.CalcSize(count).x + 6f);
            UiKit.LabelFit(new Rect(right - cw, r.y, cw, r.height), count, Color.white, GameFont.Small, TextAnchor.MiddleRight);
            right -= cw + 6f;
            if (listed)
            {
                string pill = "CC_JourneyPick_OnList".Translate(line.count).Resolve();
                float pw = UiKit.PillWidth(pill);
                if (right - pw > r.x + 120f)
                {
                    UiKit.Pill(right - pw, r.y + (r.height - UiKit.PillHeight) / 2f, pill, UiKit.Gold);
                    right -= pw + 6f;
                }
            }
            UiKit.LabelFit(new Rect(r.x + 34f, r.y, Mathf.Max(0f, right - r.x - 34f), r.height), e.label, listed ? UiKit.Gold : Color.white);

            var about = new Rect(r.x, r.y, plusRect.x - r.x - 4f, r.height);
            if (Mouse.IsOver(about)) TooltipHandler.TipRegion(about, e.tip ??= Tip(e));
            if (Widgets.ButtonText(plusRect, "+") && most > 0) Add(e, GenUI.CurrentAdjustmentMultiplier());
            TooltipHandler.TipRegion(plusRect, "CC_Journey_StepTip".Translate());
            Text.Font = GameFont.Tiny;
            if (Widgets.ButtonText(allRect, "CC_Journey_SetAll".Translate()) && most > 0) Add(e, int.MaxValue);
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(allRect, "CC_Journey_SetAllTip".Translate(most));
            if (Widgets.ButtonInvisible(about) && most > 0) Add(e, GenUI.CurrentAdjustmentMultiplier());
        }

        /// <summary>The thing on hover: what it is, its weight, nourishment and keeping, how many there are, and where it goes on the list.</summary>
        private static string Tip(Entry e)
        {
            var sb = new StringBuilder();
            sb.AppendLine(e.label);
            string desc = e.def.description ?? "";
            if (desc.Length > 400) desc = desc.Substring(0, 400).TrimEnd() + "...";
            if (desc.Length > 0) sb.AppendLine().AppendLine(desc);
            sb.AppendLine();
            sb.AppendLine("CC_JourneyPick_TipMass".Translate(e.mass.ToString("0.##")).Resolve());
            float nutrition = JourneyPack.Nutrition(e.def);
            if (nutrition > 0f) sb.AppendLine("CC_JourneyPick_TipNutrition".Translate(nutrition.ToString("0.##")).Resolve());
            if (e.def.IsIngestible)
            {
                float rot = JourneyPack.RotDays(e.def);
                sb.AppendLine(float.IsPositiveInfinity(rot) ? "CC_Journey_TipKeeps".Translate().Resolve() : "CC_Journey_TipRots".Translate(rot.ToString("0.#")).Resolve());
            }
            sb.AppendLine("CC_Journey_TipToHand".Translate(e.stores, e.carried).Resolve());
            sb.AppendLine("CC_JourneyPick_TipGroup".Translate(("CC_Journey_Group_" + JourneyMath.GroupOf(e.kind)).Translate()).Resolve());
            sb.Append("CC_JourneyPick_TipClick".Translate().Resolve());
            return sb.ToString();
        }

        /// <summary>
        /// More of a thing on the list: its line if there is one (a suggestion taken off comes back with the player's
        /// number), else a new line of the player's. Never more than there is to hand.
        /// </summary>
        private void Add(Entry e, int step)
        {
            int most = e.stores + e.carried;
            var line = LineOf(e.def);
            if (line == null)
            {
                line = new JourneyLine(e.def, 0, e.kind) { added = true };
                plan.packing.Add(line);
            }
            if (line.removed)
            {
                line.removed = false;
                line.count = 0;
            }
            if (!line.added) line.edited = true;
            line.count = step == int.MaxValue ? most : JourneyMath.StepCount(line.count, step, most);
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            changed?.Invoke();
        }
    }
}
