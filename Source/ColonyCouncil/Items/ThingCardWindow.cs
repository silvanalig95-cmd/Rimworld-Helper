using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's item card ("Rimfolk's item card", key "ItemCard" in Rimfolk's frame, on the item card's base; the player, 9
    /// November 2026: "also like item card for everything"), step 1: food, drink, drugs and medicine. The thing's own picture
    /// large in its frame, its label with the stack and quality, what kind it is and its worth, a line in plain words on what
    /// it is good for in this colony, and its tags; then by kind: for food its nutrition and colonist-days, how long it keeps
    /// (this stack where it lies, and in the cold), the game's mood for eating it, food poisoning, the road, and who at home
    /// eats it or won't and why; for drinks and drugs their effects and how long they last, addiction and tolerance, overdose,
    /// the toast, and each colonist's drug policy, faith and addiction; for medicine its potency, the care that allows it,
    /// what Rimfolk's medicine by what is treated gives it to, and each doctor's tending with it and without. Then the
    /// colony's stock (how many, days of food or medicine, short or spare) with where it is made, a click going to the bench
    /// and its bills or the field; and trade (its worth, the traders who deal in it, Vanilla Trading Expanded's market, the
    /// colony's own last prices). Its model is built when it opens (Items/ItemCardLive.cs) and refreshed rarely while it
    /// stays open; nothing is worked out with it closed. Opens first in place of the game's card ("Open Rimfolk's item card
    /// first", Items/ItemCardFirst.cs), and from a button at the foot of the game's card.
    /// </summary>
    public class ThingCardWindow : ItemCardWindow
    {
        /// <summary>The card works with the deeper game hooks on (the window hook and the button on the game's card are hooks).</summary>
        public static bool On => RimfolkPatches.On;

        private ItemCardModel model;

        public ThingCardWindow() : base("ItemCard")
        {
        }

        /// <summary>The card for a thing in hand (a stack, a drug in a pack). One card at a time.</summary>
        public static void Open(Thing t)
        {
            if (t == null || !ItemCardLive.Covers(t.def)) return;
            var w = Find.WindowStack.WindowOfType<ThingCardWindow>() ?? Added();
            w.Show(t, null, null, null);
        }

        /// <summary>The card for a kind (and its stuff, where it has one).</summary>
        public static void Open(ThingDef d, ThingDef inStuff)
        {
            if (d == null || !ItemCardLive.Covers(d)) return;
            var w = Find.WindowStack.WindowOfType<ThingCardWindow>() ?? Added();
            w.Show(null, d, inStuff, null);
        }

        private static ThingCardWindow Added()
        {
            var w = new ThingCardWindow();
            Find.WindowStack.Add(w);
            return w;
        }

        /// <summary>A new thing or kind: its model is built afresh when next drawn.</summary>
        protected override void OnChosen() => model = null;

        /// <summary>The model, built once for this thing or kind and brought up to date rarely (ItemCardLive.Refresh).</summary>
        private ItemCardModel Model
        {
            get
            {
                if (model == null || model.thing != thing || model.def != def) model = ItemCardLive.Build(thing, def, stuff, Find.CurrentMap);
                else ItemCardLive.Refresh(model);
                return model;
            }
        }

        protected override string Untitled => "CC_ItemCard_Title".Translate().Resolve();

        protected override Texture2D FrameIcon => Icons.CatFood;

        protected override bool Rated => def != null && ItemCardLive.Covers(def);

        protected override string NotRated => "CC_ItemCard_NotCovered".Translate();

        protected override string What => "item card";

        protected override float Body(float w)
        {
            var m = Model;
            var f = m.facts;
            float y = 4f;
            var t = thing;
            var d = def;
            var s = stuff;
            y = Head(w, y, new HeadFacts
            {
                NoBadge = true, Band = m.label, RatedFor = m.kindLine, Plain = m.plain, GameLabel = "CC_ItemCard_GameCard".Translate(), GameTip = "CC_ItemCard_GameCard_Tip".Translate(),
                OpenGameCard = () => ItemCardFirst.OpenGameCard(t, d, s)
            });
            if (m.tags.Count > 0)
            {
                y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_Tags".Translate());
                y += WeaponUi.Chips(0f, y, w, m.tags) + 10f;
            }

            // Food: nutrition, keeping, mood, poisoning, the road; who eats it.
            if (m.food.Count > 0)
            {
                y += UiKit.SectionTitle(0f, y, w, (f.Kind == ItemKind.Drink ? "CC_ItemCard_Sec_Drink" : "CC_ItemCard_Sec_Food").Translate());
                y = Rows(w, y, m.food, 130f) + 8f;
            }
            if (m.eatersNote != null)
            {
                y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_Eaters".Translate());
                y += UiKit.Paragraph(0f, y, w, m.eatersNote, UiKit.Faint, GameFont.Tiny) + 2f;
                y = PawnLines(w, y, m.eaters) + 8f;
            }

            // Drinks and drugs: what they do, then who may take them.
            if (ItemCardMath.ShowsDrug(f))
            {
                y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_Effects".Translate());
                if (m.effects.Count == 0) y += UiKit.Paragraph(0f, y, w, "CC_ItemCard_NoEffects".Translate(), UiKit.Faint) + 4f;
                foreach (var e in m.effects)
                {
                    UiKit.LabelFit(new Rect(0f, y, w, 20f), e.head, UiKit.Ink);
                    y += 20f;
                    if (!e.body.NullOrEmpty()) y += UiKit.Paragraph(12f, y, w - 12f, e.body, UiKit.Faint, GameFont.Tiny) + 3f;
                }
                y = Rows(w, y + 2f, m.drug, 130f) + 8f;
                if (f.HasColony)
                {
                    y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_Takers".Translate());
                    foreach (var line in m.faiths) y += UiKit.Paragraph(0f, y, w, line, UiKit.Ink, GameFont.Tiny) + 2f;
                    y = PawnLines(w, y, m.takers) + 8f;
                }
            }

            // Medicine: potency and the care that allows it, what it is given to, the doctors.
            if (f.Kind == ItemKind.Medicine)
            {
                y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_Medicine".Translate());
                y = Rows(w, y, m.medicine, 130f) + 2f;
                y += UiKit.Paragraph(0f, y, w, m.bestUse, UiKit.Ink) + 8f;
                if (f.HasColony)
                {
                    y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_Doctors".Translate());
                    y = m.doctors.Count == 0 ? y + UiKit.Paragraph(0f, y, w, "CC_ItemCard_NoDoctors".Translate(), UiKit.Faint) + 4f : PawnLines(w, y, m.doctors);
                    y += 8f;
                }
            }

            // The colony's stock and where it is made.
            y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_Stock".Translate());
            if (!f.HasColony) y += UiKit.Paragraph(0f, y, w, "CC_ItemCard_NoColony".Translate(), UiKit.Faint) + 8f;
            else
            {
                y = Rows(w, y, m.stock, 130f) + 4f;
                y = GoLines(w, y, m.making) + 8f;
            }

            // Trade.
            y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_Trade".Translate());
            y = Rows(w, y, m.trade, 130f) + 2f;
            foreach (var line in m.market) y += UiKit.Paragraph(0f, y, w, line, UiKit.Faint, GameFont.Tiny) + 2f;
            return y;
        }

        private static Color Tone(int tone) => tone > 0 ? UiKit.Good : tone < 0 ? UiKit.Bad : UiKit.Ink;

        /// <summary>A colonist a line: their portrait and name (a click opens their card), then what it means for them in its tone.</summary>
        private static float PawnLines(float w, float y, List<ItemPawnLine> lines)
        {
            const float nameW = 150f;
            foreach (var l in lines)
            {
                if (l.pawn == null || l.pawn.Destroyed) continue;
                UiKit.PawnChip(0f, y, l.pawn, 22f, true, nameW);
                float h = UiKit.Paragraph(nameW + 6f, y + 2f, w - nameW - 6f, l.text, Tone(l.tone), GameFont.Small);
                y += Mathf.Max(24f, h + 4f);
            }
            return y;
        }

        /// <summary>Lines that may lead somewhere: drawn as a link where they do (a bench and its bills, a field).</summary>
        private static float GoLines(float w, float y, List<ItemGoLine> lines)
        {
            foreach (var l in lines)
            {
                float h = Mathf.Max(20f, UiKit.Paragraph(14f, y + 1f, w - 14f, l.text, l.go != null ? UiKit.Link : Tone(l.tone), GameFont.Small));
                UiKit.LabelFit(new Rect(0f, y, 12f, 20f), "·", UiKit.Faint);
                var r = new Rect(0f, y, w, h);
                if (l.go != null)
                {
                    if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);
                    if (!l.tip.NullOrEmpty()) TooltipHandler.TipRegion(r, l.tip);
                    if (Widgets.ButtonInvisible(r)) l.go();
                }
                y += h + 2f;
            }
            return y;
        }
    }
}
