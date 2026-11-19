using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Choose the material when ordering a piece from Nice Inventory Tab's gear tab (the player, 19 November 2026: "when I
    /// select the open item slot, select the item that I want to create, and then I have to select where it's created
    /// from, and then maybe I could also immediately select which material it uses ... only select this one material as a
    /// allowed material and by default deselect everything else"). Read from the mod's DLL (decompiled outside the game,
    /// never loaded or referenced): an empty slot's menu, "Make", a piece, then a bench (or the piece itself for its first
    /// bench), calls <c>NiceInventoryTab.CommandUtility.CommandCreate(Building_WorkTable, ThingDef, RecipeDef,
    /// ApparelLayerDef)</c>, which makes the bill, registers it as made for the colonist whose tab it is
    /// (<c>BillFinished_Patch.Register</c>, with <c>ITab_Pawn_Gear_Patch.lastPawn</c>), adds it to the bench and says so.
    ///
    /// A prefix on that call, for a piece made from a material (the recipe takes its stuff from an ingredient), opens one
    /// more menu instead: "Any material" first, then each material the recipe takes that the colony has enough of on the
    /// map for one piece (<see cref="MaterialChoiceMath.Offered"/>), with how much there is and the material's own icon.
    /// Either choice calls the mod's own <c>CommandCreate</c> as it would have been called, so its bill, wear request,
    /// name and message are its own; for a material, the new bill's ingredient filter then allows only that one. A recipe
    /// without a material goes straight on, and without the mod, with the setting off or with anything not found, the mod's
    /// own way is left as it is.
    /// </summary>
    public static class MaterialChoice
    {
        public static readonly Type CommandType = AccessTools.TypeByName("NiceInventoryTab.CommandUtility");

        private static readonly Type TabType = AccessTools.TypeByName("NiceInventoryTab.ITab_Pawn_Gear_Patch");

        /// <summary>The colonist whose gear tab the mod drew last (the one it makes the piece for).</summary>
        private static readonly FieldInfo LastPawn = TabType == null ? null : AccessTools.Field(TabType, "lastPawn");

        /// <summary>The mod's order from the gear tab, or null.</summary>
        public static readonly MethodInfo Create = CommandType == null
            ? null
            : AccessTools.Method(CommandType, "CommandCreate", new[] { typeof(Building_WorkTable), typeof(ThingDef), typeof(RecipeDef), typeof(ApparelLayerDef) });

        public static string StandsDownWhy => CommandType == null ? "Nice Inventory Tab isn't loaded" : "";

        /// <summary>The material step is on (the setting, with the deeper hooks).</summary>
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.materialChoice ?? true);

        /// <summary>While Rimfolk calls the mod's own order after a choice: the prefix lets it through.</summary>
        internal static bool making;

        /// <summary>
        /// The ingredient a piece's material comes from, and the materials it takes (each allowed by that ingredient, by the
        /// recipe, and as a material for the piece); null when the piece isn't made from a material.
        /// </summary>
        internal static IngredientCount StuffIngredient(RecipeDef recipe, ThingDef piece, List<ThingDef> stuffs)
        {
            stuffs.Clear();
            var product = recipe?.ProducedThingDef ?? piece;
            if (product == null || !product.MadeFromStuff || !recipe.productHasIngredientStuff || recipe.ingredients == null) return null;
            var allowed = GenStuff.AllowedStuffsFor(product).ToList();
            foreach (var ing in recipe.ingredients)
            {
                if (ing?.filter == null || ing.IsFixedIngredient) continue;
                foreach (var s in allowed)
                    if (s != null && ing.filter.Allows(s) && (recipe.fixedIngredientFilter == null || recipe.fixedIngredientFilter.Allows(s)) && !stuffs.Contains(s))
                        stuffs.Add(s);
                if (stuffs.Count > 0) return ing;
            }
            return null;
        }

        /// <summary>How much of a material is on the map for the colony: lying where it can be seen, not forbidden.</summary>
        internal static int Have(Map map, ThingDef def, Faction player)
        {
            int n = 0;
            foreach (var t in map.listerThings.ThingsOfDef(def))
                if (t != null && t.Spawned && !t.Fogged() && (player == null || !t.IsForbidden(player)))
                    n += t.stackCount;
            return n;
        }

        /// <summary>The material step for a piece made from a material: its menu opened, and true; false to go on as before.</summary>
        internal static bool Offer(Building_WorkTable bench, ThingDef piece, RecipeDef recipe, ApparelLayerDef layer)
        {
            var map = bench?.Map;
            if (map == null || piece == null || recipe == null) return false;
            var stuffs = new List<ThingDef>();
            var ing = StuffIngredient(recipe, piece, stuffs);
            if (ing == null) return false;
            var player = Faction.OfPlayerSilentFail;
            var rows = stuffs.Select(s => new MaterialChoiceMath.Stuff(s.label, Have(map, s, player), ing.CountRequiredOfFor(s, recipe, null))).ToList();
            var pawn = LastPawn?.GetValue(null) as Pawn;
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CC_MaterialChoice_Any".Translate(), () => Make(bench, piece, recipe, layer, pawn, null, null))
                {
                    tooltip = new TipSignal("CC_MaterialChoice_Any_Tip".Translate())
                }
            };
            var offered = MaterialChoiceMath.Offered(rows);
            foreach (int i in offered)
            {
                var stuff = stuffs[i];
                var row = rows[i];
                options.Add(new FloatMenuOption("CC_MaterialChoice_Stuff".Translate(stuff.LabelCap.Named("STUFF"), row.have.Named("COUNT")),
                    () => Make(bench, piece, recipe, layer, pawn, ing, stuff), shownItemForIcon: stuff, thingStyle: null)
                {
                    tooltip = new TipSignal("CC_MaterialChoice_Stuff_Tip".Translate(piece.label.Named("THING"), row.need.Named("NEED"), stuff.label.Named("STUFF")))
                });
            }
            if (offered.Count == 0) options.Add(new FloatMenuOption("CC_MaterialChoice_None".Translate(piece.label.Named("THING")), null));
            Find.WindowStack.Add(new FloatMenu(options, "CC_MaterialChoice_Title".Translate(piece.label.Named("THING"))));
            return true;
        }

        /// <summary>
        /// The bill made by the mod's own order, for the colonist the menu was opened for; for a material, its ingredient
        /// filter then allows only that one.
        /// </summary>
        private static void Make(Building_WorkTable bench, ThingDef piece, RecipeDef recipe, ApparelLayerDef layer, Pawn pawn, IngredientCount ing, ThingDef stuff)
        {
            if (bench == null || bench.Destroyed || bench.billStack == null) return;
            try
            {
                var before = new HashSet<Bill>(bench.billStack.Bills);
                object was = LastPawn?.GetValue(null);
                bool swap = LastPawn != null && pawn != null && !ReferenceEquals(was, pawn);
                making = true;
                try
                {
                    if (swap) LastPawn.SetValue(null, pawn);
                    Create.Invoke(null, new object[] { bench, piece, recipe, layer });
                }
                finally
                {
                    making = false;
                    if (swap) LastPawn.SetValue(null, was);
                }
                if (stuff == null) return;
                var bill = bench.billStack.Bills.LastOrDefault(b => b != null && b.recipe == recipe && !before.Contains(b));
                if (bill?.ingredientFilter == null)
                {
                    CouncilLog.WarnOnce("Nice Inventory Tab made no bill Rimfolk could find after the material was chosen; its materials are left as the mod set them.");
                    return;
                }
                OnlyThis(bill.ingredientFilter, ing, stuff);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("choosing the material: making the bill", e is TargetInvocationException t && t.InnerException != null ? t.InnerException : e);
            }
        }

        /// <summary>Only this material allowed: every other one the ingredient takes is unticked; the rest of the filter is left as it is.</summary>
        internal static void OnlyThis(ThingFilter filter, IngredientCount ing, ThingDef stuff)
        {
            foreach (var d in ing.filter.AllowedThingDefs.ToList())
                if (d != stuff && filter.Allows(d)) filter.SetAllow(d, false);
            filter.SetAllow(stuff, true);
        }
    }

    /// <summary>The mod's order from the gear tab: for a piece made from a material, the material step first (Integrations/MaterialChoice.cs).</summary>
    [HarmonyPatch]
    public static class Patch_MaterialChoiceOrder
    {
        private static bool broken;

        public static bool Prepare()
        {
            if (MaterialChoice.CommandType != null && MaterialChoice.Create == null)
                CouncilLog.WarnOnce("Nice Inventory Tab is loaded, but its order from the gear tab (CommandUtility.CommandCreate) wasn't found; the material step is off.");
            return MaterialChoice.Create != null;
        }

        public static string StandsDownWhy() => MaterialChoice.StandsDownWhy;

        public static MethodBase TargetMethod() => MaterialChoice.Create;

        public static bool Prefix(Building_WorkTable __0, ThingDef __1, RecipeDef __2, ApparelLayerDef __3)
        {
            if (MaterialChoice.making || broken || !MaterialChoice.On) return true;
            try
            {
                return !MaterialChoice.Offer(__0, __1, __2, __3);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("choosing the material: the menu", e);
                return true;
            }
        }
    }
}
