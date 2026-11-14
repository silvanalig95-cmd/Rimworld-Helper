using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Food Alert (Continued) (mlie.foodalert): the "days worth of food" readout under the date. It adds up what its list of
    /// counters finds (FoodAlert.NutritionCounter.NutritionFinders, a public dictionary of map to nutrition made for other
    /// mods to add to; its sum catches each counter's error and logs it red) and divides by what the colonists and
    /// prisoners eat a day. Its own counter, "default", counts what a person eats as it is, raw food at its raw worth, so
    /// flour never counts (Vanilla Cooking Expanded's is plant matter, Alpha Crafts' never for nutrition) and a store of
    /// chickpeas and flour reads as two days. Rimfolk's count (Things/MealWorth.cs: meals, raw food and flour as the meals
    /// the kitchen makes of them, feed left out) takes "default"'s place, by name, while its setting is on; Food Alert's
    /// own counter is kept and answers whenever the setting is off or Rimfolk's count fails (logged once, never thrown into
    /// Food Alert). The readout's tooltip line "Food preferability: ..." then says what is counted instead
    /// (<see cref="Patch_FoodAlertTip"/>). Without the mod nothing happens; a list of another shape stands it down.
    /// </summary>
    public static class FoodAlertCount
    {
        public const string PackageId = "mlie.foodalert";
        public const string CounterType = "FoodAlert.NutritionCounter", FindersField = "NutritionFinders", DefaultKey = "default";
        public const string ReadoutType = "FoodAlert.HarmonyPatches", ReadoutMethod = "FoodCounter_NearDatePostfix";
        /// <summary>The readout tooltip's id in the game's tooltips (Food Alert's own).</summary>
        public const int TipId = 76515;

        /// <summary>Rimfolk's count stands in Food Alert's list.</summary>
        public static bool Hooked { get; private set; }
        /// <summary>Why it doesn't, for the self-check ("" while it does or before it was tried).</summary>
        public static string NotWhy = "";

        private static Func<Map, float> original;
        private static bool failed;

        /// <summary>Rimfolk's count failed once, so Food Alert's own answers for the rest of the session.</summary>
        public static bool Failed => failed;

        /// <summary>The setting, and Rimfolk's count working.</summary>
        public static bool Counting => Hooked && !failed && (ColonyCouncilMod.Settings?.foodAlertMealCount ?? true);

        public static bool Loaded => GenTypes.GetTypeInAnyAssembly(CounterType) != null;

        /// <summary>Puts Rimfolk's count in Food Alert's list in place of its own (once, at startup).</summary>
        public static void Hook()
        {
            if (Hooked) return;
            try
            {
                var type = GenTypes.GetTypeInAnyAssembly(CounterType);
                if (type == null)
                {
                    NotWhy = "Food Alert isn't loaded";
                    return;
                }
                var field = type.GetField(FindersField, BindingFlags.Public | BindingFlags.Static);
                if (!(field?.GetValue(null) is IDictionary<string, Func<Map, float>> finders) || finders.IsReadOnly)
                {
                    NotWhy = "Food Alert's list of counters isn't the one Rimfolk knows";
                    Log.Message("[Rimfolk] " + NotWhy + "; its days of food are left as it counts them.");
                    return;
                }
                finders.TryGetValue(DefaultKey, out original);
                finders[DefaultKey] = Count;
                Hooked = true;
                NotWhy = "";
            }
            catch (Exception e)
            {
                NotWhy = "Food Alert's list of counters couldn't be reached";
                CouncilLog.ErrorOnce("Food Alert's count", e);
            }
        }

        /// <summary>Food Alert's count of a map's stores: Rimfolk's while its setting is on and it works, else Food Alert's own. Never throws.</summary>
        private static float Count(Map map)
        {
            if (Counting && map != null)
            {
                try
                {
                    return MealWorth.Total(map);
                }
                catch (Exception e)
                {
                    failed = true;
                    CouncilLog.ErrorOnce("Food Alert's count (Food Alert's own is used from now on)", e);
                }
            }
            if (original == null) return 0f;
            try
            {
                return original(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Food Alert's own count", e);
                return 0f;
            }
        }

        // ---------------- The tooltip's line ----------------

        private static Dictionary<int, ActiveTip> tips;
        private static bool tipsLooked;
        private static string prefLabel, counted, countedRaw;

        /// <summary>The game's tooltips on screen, by id (TooltipHandler.activeTips), or null if they can't be read.</summary>
        public static Dictionary<int, ActiveTip> Tips()
        {
            if (!tipsLooked)
            {
                tipsLooked = true;
                try
                {
                    tips = AccessTools.Field(typeof(TooltipHandler), "activeTips")?.GetValue(null) as Dictionary<int, ActiveTip>;
                }
                catch
                {
                    tips = null;
                }
            }
            return tips;
        }

        /// <summary>Food Alert's readout method, or null.</summary>
        public static MethodBase ReadoutTarget()
        {
            var type = GenTypes.GetTypeInAnyAssembly(ReadoutType);
            return type == null ? null : AccessTools.Method(type, ReadoutMethod);
        }

        private static bool TipOn => Counting && RimfolkPatches.On && Event.current != null && Event.current.type == EventType.Repaint;

        /// <summary>The readout's tooltip text as it stood before Food Alert drew this time.</summary>
        internal static Func<string> TipBefore()
        {
            if (!TipOn) return null;
            var all = Tips();
            return all != null && all.TryGetValue(TipId, out var t) ? t?.signal.textGetter : null;
        }

        /// <summary>Food Alert set its tooltip just now: its text from then on says what Rimfolk counts.</summary>
        internal static void TipAfter(Func<string> before)
        {
            if (!TipOn) return;
            var all = Tips();
            if (all == null || !all.TryGetValue(TipId, out var t) || t == null) return;
            var getter = t.signal.textGetter;
            if (getter == null || getter == before || getter.Target is Reword) return;
            t.signal.textGetter = new Reword(getter).Text;
        }

        /// <summary>Food Alert's tooltip, its preferability line put as what Rimfolk counts.</summary>
        private sealed class Reword
        {
            private readonly Func<string> inner;

            public Reword(Func<string> inner) => this.inner = inner;

            public string Text()
            {
                string text = inner();
                try
                {
                    return Counting ? Reworded(text) : text;
                }
                catch
                {
                    return text;
                }
            }
        }

        /// <summary>The line "Food preferability: RawBad" (Food Alert's own words, its setting's value) replaced; anything else left as it is.</summary>
        public static string Reworded(string text)
        {
            if (text.NullOrEmpty()) return text;
            prefLabel ??= (string)"SettingDescription".Translate() + ": ";
            int at = text.IndexOf(prefLabel, StringComparison.Ordinal);
            if (at < 0) return text;
            int end = text.IndexOf('\n', at);
            if (end < 0) end = text.Length;
            counted ??= "CC_FoodAlert_Counted".Translate().Resolve();
            countedRaw ??= "CC_FoodAlert_CountedRaw".Translate().Resolve();
            return text.Substring(0, at) + (MealWorth.Cooks(Find.CurrentMap) ? counted : countedRaw) + text.Substring(end);
        }
    }

    /// <summary>Puts Rimfolk's count in Food Alert's list once every def has loaded.</summary>
    [StaticConstructorOnStartup]
    public static class FoodAlertCountStartup
    {
        static FoodAlertCountStartup()
        {
            FoodAlertCount.Hook();
        }
    }

    /// <summary>
    /// Food Alert's readout (its postfix on the game's date, by name): once it has set its tooltip, the tooltip's
    /// preferability line says what Rimfolk counts while Rimfolk's count stands in for its own. Only the one line, found
    /// in the text as Food Alert writes it; nothing of Food Alert's own changes. Without the mod, or with its readout or
    /// the game's tooltips other than expected, it stands down.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_FoodAlertTip
    {
        public static bool Prepare() => FoodAlertCount.ReadoutTarget() != null && FoodAlertCount.Tips() != null;

        public static MethodBase TargetMethod() => FoodAlertCount.ReadoutTarget();

        public static string StandsDownWhy() =>
            !FoodAlertCount.Loaded ? "Food Alert isn't loaded" : "Food Alert's readout or the game's tooltips aren't the ones it expects";

        public static void Prefix(out Func<string> __state) => __state = FoodAlertCount.TipBefore();

        public static void Postfix(Func<string> __state) => FoodAlertCount.TipAfter(__state);
    }
}
