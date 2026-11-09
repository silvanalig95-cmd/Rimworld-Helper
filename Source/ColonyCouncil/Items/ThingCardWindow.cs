using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's item card ("Rimfolk's item card", key "ItemCard" in Rimfolk's frame, on the item card's base; the player, 9
    /// November 2026: "also like item card for everything"), step 1: food, drink, drugs and medicine; step 2 below. The thing's own picture
    /// large in its frame, its label with the stack and quality, what kind it is and its worth, a line in plain words on what
    /// it is good for in this colony, and its tags; then by kind: for food its nutrition and colonist-days, how long it keeps
    /// (this stack where it lies, and in the cold), the game's mood for eating it, food poisoning, the road, and who at home
    /// eats it or won't and why; for drinks and drugs their effects and how long they last, addiction and tolerance, overdose,
    /// the toast, and each colonist's drug policy, faith and addiction; for medicine its potency, the care that allows it,
    /// what Rimfolk's medicine by what is treated gives it to, and each doctor's tending with it and without. Then the
    /// colony's stock (how many, days of food or medicine, short or spare) with where it is made, a click going to the bench
    /// and its bills or the field; and trade (its worth, the traders who deal in it, Vanilla Trading Expanded's market, the
    /// colony's own last prices). Step 2 adds materials (what it does to what is made of it against steel and wood or the
    /// colony's most held of its kind, what the colony can make of it now, its stock and what waits on it, where it comes
    /// from here, fuel) and art (its beauty and worth, the tale it shows, the room it stands in with it and without it, keep
    /// or sell, the rules that care). Step 3 adds every other item (UseParts): books (what reading gives and who at home gains
    /// most), body parts (what it replaces, its efficiency, the surgery, who could take it, the faiths), techprints (the
    /// project, applied of needed, what it opens), trainers (the skill or psycast, who gains most), genepacks, xenogerms and
    /// embryos (the genes and what each does, the assembler, who a xenogerm suits), usable items (what a use does, to what,
    /// its risks, who may use it), shells (the burst, the guns at home that fire it), furniture in a crate (what the piece
    /// does, the room it would lift, who has no bed) and anything else (the game's own words and stats, the work left on an
    /// unfinished thing). Its model is built when it opens (Items/ItemCardLive.cs, ItemCardMaterials.cs, ItemCardArt.cs,
    /// ItemCardUses.cs, ItemCardBodies.cs, ItemCardThings.cs) and refreshed rarely while it stays open; nothing is worked out
    /// with it closed. Opens first in place of the game's card ("Open Rimfolk's item card first", Items/ItemCardFirst.cs),
    /// and from a button at the foot of the game's card.
    /// </summary>
    public class ThingCardWindow : ItemCardWindow
    {
        /// <summary>The card works with the deeper game hooks on (the window hook and the button on the game's card are hooks).</summary>
        public static bool On => RimfolkPatches.On;

        private ItemCardModel model;

        public ThingCardWindow() : base("ItemCard")
        {
        }

        /// <summary>The card for a thing in hand (a stack, a drug in a pack, a piece of art placed or minified). One card at a time.</summary>
        public static void Open(Thing t)
        {
            if (t == null || !ItemCardLive.Covers(t)) return;
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
                // A minified piece's model reads the piece inside (its def differs from the crate's): a thing is matched by itself.
                if (model == null || model.thing != thing || thing == null && model.def != def) model = ItemCardLive.Build(thing, def, stuff, Find.CurrentMap);
                else ItemCardLive.Refresh(model);
                return model;
            }
        }

        protected override string Untitled => "CC_ItemCard_Title".Translate().Resolve();

        protected override Texture2D FrameIcon => Icons.CatFood;

        protected override bool Rated => def != null && Model.kind != ItemKind.None;

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

            if (f.Kind == ItemKind.Material) y = MaterialParts(w, y, m);
            else if (f.Kind == ItemKind.Art) y = ArtParts(w, y, m);
            else
            {
                // Step 3's kinds: what it does and who at home it is for.
                if (ItemCardMath.StepThree(f.Kind)) y = UseParts(w, y, m);
                // The colony's stock and where it is made (furniture in a crate has its own count among its rows: it is built, not made).
                if (f.Kind != ItemKind.Furniture)
                {
                    y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_Stock".Translate());
                    if (!f.HasColony) y += UiKit.Paragraph(0f, y, w, "CC_ItemCard_NoColony".Translate(), UiKit.Faint) + 8f;
                    else
                    {
                        y = Rows(w, y, m.stock, 130f) + 4f;
                        y = GoLines(w, y, m.making) + 8f;
                    }
                }
            }

            // Trade.
            y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_Trade".Translate());
            y = Rows(w, y, m.trade, 130f) + 2f;
            foreach (var line in m.market) y += UiKit.Paragraph(0f, y, w, line, UiKit.Faint, GameFont.Tiny) + 2f;
            return y;
        }

        /// <summary>A material: what it does to what is made of it, what the colony can make of it, the stock and what waits on it, where it comes from here.</summary>
        private float MaterialParts(float w, float y, ItemCardModel m)
        {
            var f = m.facts;
            if (m.stuffTable.Count > 0)
            {
                y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_AsStuff".Translate());
                y = StuffTable(w, y, m) + 2f;
                if (m.stuffKinds.Length > 0) y += UiKit.Paragraph(0f, y, w, m.stuffKinds, UiKit.Faint, GameFont.Tiny) + 2f;
                y += 8f;
            }
            y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_MadeOf".Translate());
            if (m.madeOf.Count == 0) y += UiKit.Paragraph(0f, y, w, "CC_ItemCard_MadeNothing".Translate(), UiKit.Faint) + 4f;
            else y = Rows(w, y, m.madeOf, 130f) + 2f;
            if (m.madeLater != null) y += UiKit.Paragraph(0f, y, w, m.madeLater, UiKit.Faint, GameFont.Tiny) + 2f;
            y += 8f;
            y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_Stock".Translate());
            if (!f.HasColony) return y + UiKit.Paragraph(0f, y, w, "CC_ItemCard_NoColony".Translate(), UiKit.Faint) + 8f;
            y = Rows(w, y, m.demand, 130f) + 4f;
            if (m.bills.Count > 0) y = GoLines(w, y, m.bills) + 4f;
            y += 4f;
            y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_Sources".Translate());
            y = GoLines(w, y, m.sources);
            y = GoLines(w, y, m.making) + 8f;
            return y;
        }

        /// <summary>The comparison: the material's column first, then each it is compared with; its own value in good or bad ink where it beats or trails them all.</summary>
        private static float StuffTable(float w, float y, ItemCardModel m)
        {
            const float labelW = 130f;
            int cols = Mathf.Max(1, m.stuffHeads.Count);
            float colW = (w - labelW) / cols;
            for (int i = 0; i < m.stuffHeads.Count; i++)
                UiKit.LabelFit(new Rect(labelW + i * colW, y, colW - 4f, 20f), m.stuffHeads[i], i == 0 ? UiKit.Heading : UiKit.Faint, GameFont.Tiny);
            y += 20f;
            for (int r = 0; r < m.stuffTable.Count; r++)
            {
                var row = m.stuffTable[r];
                UiKit.LabelFit(new Rect(0f, y, labelW, 20f), r < m.stuffLabels.Count ? m.stuffLabels[r] : row.Id, UiKit.Faint, GameFont.Small);
                for (int i = 0; i < row.Values.Count && i < cols; i++)
                    UiKit.LabelFit(new Rect(labelW + i * colW, y, colW - 4f, 20f), row.Values[i], i == 0 ? Tone(row.Tone) : UiKit.Dim, GameFont.Small);
                y += 20f;
            }
            return y;
        }

        /// <summary>A work of art: the piece, its story, where it stands and what it does there, keep or sell, the rules that care.</summary>
        private float ArtParts(float w, float y, ItemCardModel m)
        {
            var f = m.facts;
            y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_Art".Translate());
            y = Rows(w, y, m.art, 130f) + 8f;
            if (m.story.Count > 0 || !m.storyText.NullOrEmpty())
            {
                y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_Story".Translate());
                y = Rows(w, y, m.story, 130f) + 2f;
                if (!m.storyText.NullOrEmpty()) y += UiKit.Paragraph(0f, y, w, m.storyText, UiKit.Faint, GameFont.Tiny) + 2f;
                y += 8f;
            }
            y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_Room".Translate());
            if (!f.HasColony) return y + UiKit.Paragraph(0f, y, w, "CC_ItemCard_NoColony".Translate(), UiKit.Faint) + 8f;
            y = Rows(w, y, m.room, 130f) + 2f;
            if (m.rooms.Count > 0) y = GoLines(w, y, m.rooms) + 2f;
            if (m.keepSell.Length > 0) y += UiKit.Paragraph(0f, y, w, m.keepSell, UiKit.Ink) + 2f;
            y += 8f;
            if (m.rules.Count > 0)
            {
                y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_Rules".Translate());
                foreach (var line in m.rules) y += UiKit.Paragraph(0f, y, w, line, UiKit.Ink, GameFont.Small) + 2f;
                y += 8f;
            }
            return y;
        }

        /// <summary>
        /// Step 3's kinds: what it does (rows under a title by kind, the game's own words, a gene holder's genes), who at home it
        /// is for (the best first, or where a piece would do most), the faiths that care and the lines that lead somewhere.
        /// </summary>
        private float UseParts(float w, float y, ItemCardModel m)
        {
            var f = m.facts;
            string kind = f.Kind.ToString();
            y += UiKit.SectionTitle(0f, y, w, ("CC_ItemCard_Sec_Use_" + kind).Translate());
            if (m.use.Count > 0) y = Rows(w, y, m.use, 130f) + 2f;
            if (!m.useNote.NullOrEmpty()) y += UiKit.Paragraph(0f, y, w, m.useNote, UiKit.Faint, GameFont.Tiny) + 2f;
            foreach (var e in m.genes)
            {
                UiKit.LabelFit(new Rect(0f, y, w, 20f), e.head, UiKit.Ink);
                y += 20f;
                if (!e.body.NullOrEmpty()) y += UiKit.Paragraph(12f, y, w - 12f, e.body, UiKit.Faint, GameFont.Tiny) + 3f;
            }
            y += 8f;
            if (f.HasColony && (m.forWhom.Count > 0 || !m.forWhomNote.NullOrEmpty() || m.rooms.Count > 0 && f.Kind == ItemKind.Furniture))
            {
                y += UiKit.SectionTitle(0f, y, w, ("CC_ItemCard_Sec_Who_" + kind).Translate());
                if (!m.forWhomNote.NullOrEmpty()) y += UiKit.Paragraph(0f, y, w, m.forWhomNote, UiKit.Faint, GameFont.Tiny) + 2f;
                y = PawnLines(w, y, m.forWhom);
                if (f.Kind == ItemKind.Furniture && m.rooms.Count > 0) y = GoLines(w, y, m.rooms);
                y += 8f;
            }
            if (m.useRules.Count > 0)
            {
                y += UiKit.SectionTitle(0f, y, w, "CC_ItemCard_Sec_Rules".Translate());
                foreach (var line in m.useRules) y += UiKit.Paragraph(0f, y, w, line, UiKit.Ink, GameFont.Small) + 2f;
                y += 8f;
            }
            if (m.useGo.Count > 0) y = GoLines(w, y, m.useGo) + 8f;
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
