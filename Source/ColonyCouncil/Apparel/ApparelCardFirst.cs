using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "Open Rimfolk's apparel card first" (the player, 9 November 2026, after the plain card of a "Bluefur parka (awful)":
    /// "i don't see the gear cards in game by default"): a new game info card about a piece of apparel the rating reads, the
    /// piece itself or its kind (its usual stuff, normal quality), opens as Rimfolk's apparel card instead, wherever it is
    /// opened from. The weapons' one hook on the window stack asks here after the weapon card (Patch_WeaponCardFirst). What
    /// stays the game's: a card opened from inside the game's info card (its links and its back button move within it,
    /// WeaponCardFirst.insideGameCard), the game's card asked for from Rimfolk's card ("All the game's stats"), anything that
    /// isn't apparel the rating reads, and everything with the setting off.
    /// </summary>
    public static class ApparelCardFirst
    {
        public static bool On => ApparelCardWindow.On && (ColonyCouncilMod.Settings?.apparelCardFirst ?? true);

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
        /// A new game info card about to open: true when it opened as Rimfolk's apparel card instead (the game's isn't added).
        /// Only a card about apparel the rating reads, while playing, and not navigation inside the game's own card.
        /// </summary>
        public static bool Swapped(Dialog_InfoCard card)
        {
            if (letThrough || WeaponCardFirst.insideGameCard || !On || card == null || Current.ProgramState != ProgramState.Playing) return false;
            if (CardThing == null || CardDef == null) return false;
            var thing = CardThing(card);
            if (thing != null)
            {
                if (thing is Pawn || !ApparelLive.Rateable(thing.def) || ApparelLive.Of(thing) == null) return false;
                ApparelCardWindow.Open(thing, null);
                return true;
            }
            if (!(CardDef(card) is ThingDef def) || !ApparelLive.Rateable(def)) return false;
            var stuff = CardStuff?.Invoke(card);
            if (ApparelLive.Of(def, stuff) == null) return false;
            ApparelCardWindow.Open(def, stuff, null);
            return true;
        }

        /// <summary>The game's own info card for the same piece or kind, from Rimfolk's card ("All the game's stats"), let through the hook.</summary>
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
}
