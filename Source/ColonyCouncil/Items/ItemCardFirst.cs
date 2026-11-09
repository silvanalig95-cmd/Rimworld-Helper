using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "Open Rimfolk's item card first" (the player, 9 November 2026: "also like item card for everything"): a new game info
    /// card about any item (food, a drink, a drug, medicine: step 1; a material or a work of art held, minified or placed: step
    /// 2; a book, a body part, a techprint, a trainer, genes, something to use, a shell, furniture in a crate, anything else:
    /// step 3), the thing itself or its kind, opens as Rimfolk's item card instead, wherever it is opened from. The weapons'
    /// one hook on the window stack (Patch_WeaponCardFirst) asks here; the item card's kinds are never weapons or apparel, so
    /// the three cards never meet. What stays the game's: a card opened from inside the game's info card (its links and its
    /// back button move within it, WeaponCardFirst.insideGameCard), the game's card asked for from Rimfolk's card ("All the
    /// game's stats"), what isn't an item (ItemCardLive.KindOf names it: corpses, pawns, a building standing on the map that
    /// isn't art), and everything with the setting off. The game's card for these kinds keeps a button back to Rimfolk's.
    /// </summary>
    public static class ItemCardFirst
    {
        public static bool On => ThingCardWindow.On && (ColonyCouncilMod.Settings?.itemCardFirst ?? true);

        /// <summary>The game's card asked for from Rimfolk's card: let through once.</summary>
        private static bool letThrough;

        private static readonly AccessTools.FieldRef<Dialog_InfoCard, Thing> CardThing = Ref<Thing>("thing");
        private static readonly AccessTools.FieldRef<Dialog_InfoCard, Def> CardDef = Ref<Def>("def");
        private static readonly AccessTools.FieldRef<Dialog_InfoCard, ThingDef> CardStuff = Ref<ThingDef>("stuff");

        private static AccessTools.FieldRef<Dialog_InfoCard, T> Ref<T>(string name)
        {
            try
            {
                return AccessTools.FieldRefAccess<Dialog_InfoCard, T>(name);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// A new game info card about to open: true when it opened as Rimfolk's item card instead (the game's isn't added).
        /// Only a card about an item the card covers (ItemCardLive.Covers) or a work of art, while playing, and not navigation
        /// inside the game's own card.
        /// </summary>
        public static bool Swapped(Dialog_InfoCard card)
        {
            if (letThrough || WeaponCardFirst.insideGameCard || !On || card == null || Current.ProgramState != ProgramState.Playing) return false;
            if (CardThing == null || CardDef == null) return false;
            var thing = CardThing(card);
            if (thing != null)
            {
                // A thing in hand by its own kind: a minified piece of art is the piece inside, a full grave's art counts.
                if (thing is Pawn || !ItemCardLive.Covers(thing)) return false;
                ThingCardWindow.Open(thing);
                return true;
            }
            if (!(CardDef(card) is ThingDef def) || !ItemCardLive.Covers(def)) return false;
            ThingCardWindow.Open(def, CardStuff?.Invoke(card));
            return true;
        }

        /// <summary>The game's own info card for the same thing or kind, from Rimfolk's card ("All the game's stats"), let through the hook.</summary>
        public static void OpenGameCard(Thing thing, ThingDef def, ThingDef stuff)
        {
            Dialog_InfoCard card = thing != null ? new Dialog_InfoCard(thing) : def == null ? null : stuff != null ? new Dialog_InfoCard(def, stuff) : new Dialog_InfoCard(def);
            if (card == null) return;
            letThrough = true;
            try
            {
                Find.WindowStack.Add(card);
            }
            finally
            {
                letThrough = false;
            }
        }

        /// <summary>"Rimfolk's item card" at the foot of the game's card, left of its close button, for the kinds the item card shows.</summary>
        public static void Button(Dialog_InfoCard card, Rect inRect)
        {
            if (!ThingCardWindow.On || CardThing == null || CardDef == null) return;
            var thing = CardThing(card);
            if (thing is Pawn) return;
            var def = thing?.def ?? CardDef(card) as ThingDef;
            if (thing != null ? !ItemCardLive.Covers(thing) : !ItemCardLive.Covers(def)) return;
            string label = "CC_ItemCard_OpenCard".Translate();
            Text.Font = GameFont.Small;
            float w = Mathf.Min(260f, Text.CalcSize(label).x + 54f);
            var b = new Rect(inRect.x, inRect.yMax - 36f, w, 34f);
            if (UiKit.Button(b, ""))
            {
                if (thing != null) ThingCardWindow.Open(thing);
                else ThingCardWindow.Open(def, CardStuff?.Invoke(card));
                // With Rimfolk's card opening first, the two cards are two ways of looking: going over closes the game's.
                if (On) card.Close(false);
            }
            UiKit.Icon(new Rect(b.x + 10f, b.center.y - 9f, 18f, 18f), Icons.CatFood, Color.white);
            UiKit.LabelFit(new Rect(b.x + 36f, b.y, b.width - 40f, b.height), label, Color.white);
            TooltipHandler.TipRegion(b, "CC_ItemCard_OpenCard_Tip".Translate());
        }
    }

    /// <summary>The button to Rimfolk's item card at the foot of the game's info card (ItemCardFirst.Button).</summary>
    [HarmonyPatch(typeof(Dialog_InfoCard), nameof(Dialog_InfoCard.DoWindowContents))]
    public static class Patch_ItemCardButton
    {
        private static bool broken;

        public static void Postfix(Dialog_InfoCard __instance, Rect inRect)
        {
            if (broken || !ThingCardWindow.On) return;
            try
            {
                ItemCardFirst.Button(__instance, inRect);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the item card's button on the info card", e);
            }
        }
    }
}
