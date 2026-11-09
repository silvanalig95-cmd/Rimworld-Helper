using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "Open Rimfolk's weapon card first" (the player, 9 November 2026: "can the rimworld weapon card be the default screen
    /// when opening stats/info of a weapon?"): a new game info card about a weapon the card can show, a weapon itself or a
    /// weapon's kind (its default stuff, normal quality), opens as Rimfolk's weapon card instead, wherever it is opened from
    /// (every caller that adds a <see cref="Dialog_InfoCard"/> to the window stack: the "i" buttons, the inspect pane, trade
    /// screens, bills and lists, the research and architect tabs). What stays the game's: a card opened from inside the
    /// game's info card (its links and its back button move within it), the game's card asked for from Rimfolk's card ("All
    /// the game's stats"), anything that isn't a weapon the rating reads, and everything with the setting off.
    /// </summary>
    public static class WeaponCardFirst
    {
        public static bool On => WeaponCardWindow.On && (ColonyCouncilMod.Settings?.weaponCardFirst ?? true);

        /// <summary>The game's card is being drawn: what it opens (a link, its back button) is navigation inside it, and stays its own.</summary>
        internal static bool insideGameCard;

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
        /// A new game info card about to open: true when it opened as Rimfolk's weapon card instead (the game's isn't added).
        /// Only a card about a weapon the rating reads, while playing, and not navigation inside the game's own card.
        /// </summary>
        public static bool Swapped(Dialog_InfoCard card)
        {
            if (letThrough || insideGameCard || !On || card == null || Current.ProgramState != ProgramState.Playing) return false;
            if (CardThing == null || CardDef == null) return false;
            var thing = CardThing(card);
            if (thing != null)
            {
                if (thing is Pawn || !WeaponLive.Rateable(thing.def) || WeaponLive.Of(thing) == null) return false;
                WeaponCardWindow.Open(thing, null);
                return true;
            }
            if (!(CardDef(card) is ThingDef def) || !WeaponLive.Rateable(def)) return false;
            var stuff = CardStuff?.Invoke(card);
            if (WeaponLive.Of(def, stuff) == null) return false;
            WeaponCardWindow.Open(def, stuff, null);
            return true;
        }

        /// <summary>The game's own info card for the same weapon or kind, from Rimfolk's card ("All the game's stats"), let through the hook.</summary>
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
    }

    /// <summary>A new info card about a weapon opens as Rimfolk's weapon card (WeaponCardFirst.Swapped); every other window is added as before.</summary>
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add))]
    public static class Patch_WeaponCardFirst
    {
        private static bool broken;

        public static bool Prefix(Window window)
        {
            if (broken || !(window is Dialog_InfoCard card)) return true;
            try
            {
                return !WeaponCardFirst.Swapped(card);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("opening Rimfolk's weapon card first", e);
                return true;
            }
        }
    }

    /// <summary>While the game's info card is drawn, what it opens (its links, its back button) is navigation inside it and stays the game's card.</summary>
    [HarmonyPatch(typeof(Dialog_InfoCard), nameof(Dialog_InfoCard.DoWindowContents))]
    public static class Patch_WeaponCardFirstInside
    {
        public static void Prefix() => WeaponCardFirst.insideGameCard = true;

        public static void Finalizer() => WeaponCardFirst.insideGameCard = false;
    }
}
