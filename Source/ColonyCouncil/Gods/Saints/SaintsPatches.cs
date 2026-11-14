using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a colonist's death was, read at the moment they die (Gods/Saints/SaintsMath.cs, SaintDeath), before the game
    /// forgets what they were doing: an enemy's hand while the colony was under attack, drafted or carrying, rescuing or
    /// tending someone, the healer's years, their devotion, the priest's seat, a vow still kept, a god's presence on the land.
    /// </summary>
    public static class SaintDeaths
    {
        public static SaintDeath Read(Pawn p, DamageInfo? dinfo)
        {
            var map = p.MapHeld;
            bool attack = map != null && GenHostility.AnyHostileActiveThreatToPlayer(map, false, false);
            var by = dinfo?.Instigator;
            bool enemy = by != null && by.HostileTo(Faction.OfPlayer);
            // Bleeding out from an enemy's wounds mid-fight has no hand on it at the end.
            bool wounds = dinfo == null && (p.health?.hediffSet?.hediffs?.Any(h => h is Hediff_Injury) ?? false);
            var job = p.CurJobDef;
            bool carrying = p.carryTracker?.CarriedThing is Pawn carried && carried.Downed && carried.Faction == Faction.OfPlayer;
            var med = p.skills?.GetSkill(SkillDefOf.Medicine);
            var first = PawnGods.First(p);
            var word = first == null ? BeliefWord.None : PawnGods.WordOf(p, first);
            return new SaintDeath
            {
                ours = p.IsFreeColonist && !p.IsSlave && !p.IsQuestLodger() && p.RaceProps?.Humanlike == true && p.DevelopmentalStage.Adult() && People.HasInnerLife(p),
                scoffer = Worship.Scoffer(p),
                hunger = first?.kind == GodKind.Hunger,
                days = p.records == null ? 0 : (int)(p.records.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) / GenDate.TicksPerDay),
                byEnemy = attack && (enemy || wounds),
                drafted = p.Drafted,
                rescuing = job == JobDefOf.Rescue || job == JobDefOf.TendPatient || carrying,
                medicine = med?.Level ?? 0,
                medicinePassion = med != null && med.passion != Passion.None,
                tended = p.records == null ? 0 : (int)p.records.GetValue(RecordDefOf.TimesTendedOther),
                devoted = word == BeliefWord.Devoted || word == BeliefWord.Fervent,
                priest = Worship.IsPriest(p),
                vow = Worship.Book?.vows.Any(v => v != null && !v.done && v.pawn == p) ?? false,
                godNear = map?.gameConditionManager?.ActiveConditions?.Any(c => c is GameCondition_GodPresence) ?? false
            };
        }
    }

    /// <summary>A colonist of the colony's own dies: their death may wait for the council's card about a saint (Gods/Saints/Saints.cs, Died).</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_SaintDeath
    {
        public static void Prefix(Pawn __instance, DamageInfo? dinfo, out SaintDeath? __state)
        {
            __state = null;
            if (!RimfolkPatches.On || !Saints.On || __instance == null || __instance.Dead || !__instance.IsFreeColonist) return;
            try
            {
                __state = SaintDeaths.Read(__instance, dinfo);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("saints: a death read", e);
            }
        }

        /// <summary>Only once they're really dead (another mod may stop a death).</summary>
        public static void Postfix(Pawn __instance, SaintDeath? __state)
        {
            if (__state == null || __instance == null || !__instance.Dead) return;
            try
            {
                Saints.Died(__instance, __state.Value);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("saints: a death", e);
            }
        }
    }

    /// <summary>
    /// The ideoligion screen's deities shown as the faith's saints (the player's pick, 14 November 2026): while the game draws a
    /// faith's deity box (IdeoFoundation_Deity.DoInfo), its word "Deities" reads as the faith's own (Saints.HeaderWord: its
    /// saints by its age and culture, its damned where every one is damned), and under the box, in play, a line names the god
    /// each is held to be of, only where the colony knows that god by that name. The ideoligion itself is never changed:
    /// the word is the game's own text, swapped for the length of the drawing and put back after.
    /// <para>
    /// Stands down safely: <see cref="Prepare"/> checks the method is there as expected; the word is found by the game's own
    /// English text, so a renamed key leaves the game's word as it is.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(IdeoFoundation_Deity), nameof(IdeoFoundation_Deity.DoInfo))]
    public static class Patch_SaintsWord
    {
        public static bool Found;

        public static bool Prepare()
        {
            try
            {
                var m = AccessTools.Method(typeof(IdeoFoundation_Deity), nameof(IdeoFoundation_Deity.DoInfo), new[] { typeof(float).MakeByRefType(), typeof(float), typeof(IdeoEditMode) });
                var ps = m?.GetParameters();
                Found = m != null && !m.IsStatic && ps.Length == 3 && ps[0].Name == "curY" && ps[1].Name == "width";
            }
            catch
            {
                Found = false;
            }
            if (!Found) Log.Message("[Rimfolk] The game draws a faith's deities other than Rimfolk expects in this version, so the ideoligion screen keeps the word \"Deities\".");
            return Found;
        }

        private static List<string> keys;

        /// <summary>The game's keys whose English text is "Deities", found once.</summary>
        private static List<string> Keys()
        {
            if (keys != null) return keys;
            keys = new List<string>();
            try
            {
                var en = LanguageDatabase.defaultLanguage?.keyedReplacements;
                if (en != null)
                    foreach (var kv in en)
                        if (kv.Value?.value != null && string.Equals(kv.Value.value.Trim(), "Deities", StringComparison.OrdinalIgnoreCase))
                            keys.Add(kv.Key);
            }
            catch
            {
                // Nothing found: the game's word stands.
            }
            return keys;
        }

        public static void Prefix(IdeoFoundation_Deity __instance, out List<(LoadedLanguage.KeyedReplacement r, string was)> __state)
        {
            __state = null;
            if (!RimfolkPatches.On || !Saints.On || __instance?.ideo == null) return;
            try
            {
                if (Saints.Of(__instance.ideo).Count == 0) return;
                string word = Saints.HeaderWord(__instance.ideo);
                if (word.NullOrEmpty()) return;
                foreach (var lang in new[] { LanguageDatabase.activeLanguage, LanguageDatabase.defaultLanguage }.Where(l => l?.keyedReplacements != null).Distinct())
                    foreach (var key in Keys())
                        if (lang.keyedReplacements.TryGetValue(key, out var r) && r != null)
                        {
                            (__state ??= new List<(LoadedLanguage.KeyedReplacement, string)>()).Add((r, r.value));
                            r.value = word;
                        }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("saints: the ideoligion screen's word", e);
            }
        }

        /// <summary>Under the deity box, in play: the god each saint is held to be of, where the colony knows it.</summary>
        public static void Postfix(IdeoFoundation_Deity __instance, ref float curY, float width)
        {
            if (!RimfolkPatches.On || !Saints.On || __instance?.ideo == null || Current.ProgramState != ProgramState.Playing) return;
            try
            {
                var known = Saints.Of(__instance.ideo).Where(s => Saints.OfGod(s.god) != null).ToList();
                if (known.Count == 0) return;
                string text = "CC_Saint_IdeoLine".Translate(string.Join("; ", known.Select(s => (s.damned ? "CC_Saint_IdeoDamned" : "CC_Saint_IdeoHoly")
                    .Translate(Saints.Title(s).Named("SAINT"), Saints.OfGod(s.god).Named("GOD")).Resolve())).Named("SAINTS")).Resolve();
                var font = Text.Font;
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.75f, 0.72f, 0.62f);
                float h = Text.CalcHeight(text, width);
                Widgets.Label(new Rect(0f, curY, width, h), text);
                GUI.color = Color.white;
                Text.Font = font;
                curY += h + 6f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("saints: the ideoligion screen's line", e);
            }
        }

        public static void Finalizer(List<(LoadedLanguage.KeyedReplacement r, string was)> __state)
        {
            if (__state == null) return;
            foreach (var (r, was) in __state) r.value = was;
        }
    }

    /// <summary>The button on a statue: raise it to a saint, or see whose it is (Gods/Saints/SaintsPlaces.cs).</summary>
    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetGizmos))]
    public static class Patch_SaintGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, ThingWithComps __instance)
        {
            if (__result != null)
                foreach (var g in __result)
                    yield return g;
            Gizmo more = null;
            if (RimfolkPatches.On && __instance is Building && __instance.Faction == Faction.OfPlayer)
                try
                {
                    more = SaintsPlaces.GizmoFor(__instance);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("saints: a statue's button", e);
                }
            if (more != null) yield return more;
        }
    }

    /// <summary>A saint's statue or grave says whose it is (Gods/Saints/SaintsPlaces.cs).</summary>
    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetInspectString))]
    public static class Patch_SaintInspect
    {
        public static void Postfix(ThingWithComps __instance, ref string __result)
        {
            if (!RimfolkPatches.On || !(__instance is Building)) return;
            try
            {
                string line = SaintsPlaces.InspectLine(__instance);
                if (line.NullOrEmpty()) return;
                __result = __result.NullOrEmpty() ? line : __result + "\n" + line;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("saints: a saint's place's line", e);
            }
        }
    }
}
