using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The game's hooks for each god's own presence (GodPresenceOwn.cs). Each looks first whether any own presence lasts at
    // all (GodPresenceOwn.On: one tick compare when none does), so a game without one pays nothing; then whether the one on
    // the pawn's or the thing's map is the kind that changes it. The sizes are GodPresenceOwnMath's.

    /// <summary>
    /// Plants grow faster under a presence that grows them (Hollin's, every plant; Morel's, fungus; Helios's, crops; Murmur's,
    /// wet ground), shown in the plant's own growth-rate breakdown under the presence's label.
    /// </summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.GrowthRate), MethodType.Getter)]
    public static class Patch_GodPresenceGrowth
    {
        public static void Postfix(Plant __instance, ref float __result)
        {
            if (__result <= 0f || !RimfolkPatches.On || Find.TickManager.TicksGame > GameCondition_GodPresence.AnyOwnUntil) return;
            float f = Factor(__instance, out _);
            if (f != 1f) __result *= f;
        }

        internal static float Factor(Plant p, out GameCondition_GodPresence c)
        {
            c = null;
            if (p?.def?.plant == null || !p.Spawned) return 1f;
            c = GodPresenceOwn.On(p.Map);
            if (c == null) return 1f;
            var e = c.Effect;
            if (e != PresenceEffect.Growth && e != PresenceEffect.Rot && e != PresenceEffect.LongNoon && e != PresenceEffect.Rain) return 1f;
            return GodPresenceOwnMath.Growth(e, c.Strength, c.Harsh, Kind(p), e == PresenceEffect.Rain && GodPresenceShows.WetGround(p.Map, p.Position));
        }

        /// <summary>A plant as the presences see it: a fungus by its kind (cave plants, the mods' mushrooms), a crop sown by the colony, a tree, else wild.</summary>
        internal static PresencePlant Kind(Plant p)
        {
            var d = p.def;
            if (d.plant.cavePlant || d.defName.IndexOf("fung", StringComparison.OrdinalIgnoreCase) >= 0 || d.defName.IndexOf("mushroom", StringComparison.OrdinalIgnoreCase) >= 0)
                return PresencePlant.Fungus;
            if (d.plant.IsTree) return PresencePlant.Tree;
            if (d.plant.Sowable && (p.Map.zoneManager.ZoneAt(p.Position) is Zone_Growing || p.Position.GetEdifice(p.Map) is Building_PlantGrower)) return PresencePlant.Crop;
            return PresencePlant.Wild;
        }
    }

    /// <summary>The presence's factor in the plant's growth-rate breakdown, under its own label.</summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.GrowthRateCalcDesc), MethodType.Getter)]
    public static class Patch_GodPresenceGrowthDesc
    {
        public static void Postfix(Plant __instance, ref string __result)
        {
            if (!RimfolkPatches.On || Find.TickManager.TicksGame > GameCondition_GodPresence.AnyOwnUntil) return;
            float f = Patch_GodPresenceGrowth.Factor(__instance, out var c);
            if (f == 1f || c == null) return;
            __result = (__result ?? "") + "\n" + c.LabelCap + ": x" + f.ToStringPercent();
        }
    }

    /// <summary>
    /// Things rot faster under Morel's spore-fall (food left out of the cold, and the dead going to earth), and not at all out
    /// of doors under Hush's white silence: the rot each turn scaled by the presence (nothing frozen rots either way).
    /// </summary>
    [HarmonyPatch(typeof(CompRottable), "TickInterval")]
    public static class Patch_GodPresenceRot
    {
        public static void Prefix(CompRottable __instance, out float __state) => __state = __instance.RotProgress;

        public static void Postfix(CompRottable __instance, float __state)
        {
            if (!RimfolkPatches.On || Find.TickManager.TicksGame > GameCondition_GodPresence.AnyOwnUntil) return;
            float after = __instance.RotProgress;
            if (after <= __state) return;
            try
            {
                var t = __instance.parent;
                var map = t?.MapHeld;
                var c = GodPresenceOwn.On(map);
                if (c == null || c.Effect != PresenceEffect.Rot && c.Effect != PresenceEffect.WhiteSilence) return;
                float f = GodPresenceOwnMath.Rot(c.Effect, c.Strength, c.Harsh, t.PositionHeld.Roofed(map));
                if (f != 1f) __instance.RotProgress = __state + (after - __state) * f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: rot under a presence", e);
            }
        }
    }

    /// <summary>What a presence changes in a stat (shown in the stat's breakdown under the presence's label).</summary>
    public enum PresenceStat
    {
        MiningYield,
        BenchSpeed,
        Trade,
        SleepHealing,
        Immunity,
        ColdSooner,
        Psychic
    }

    /// <summary>
    /// A presence's part in one of the game's stats: Smoulder's mining yield, Pip's powered benches, Ducat's trade, Selene's
    /// healing in sleep, Morel's immunity, Hush's cold felt sooner by the weak, the dark's psychic sensitivity. Added to the
    /// stats when the game starts (<see cref="GodPresenceStatsStartup"/>).
    /// </summary>
    public class StatPart_GodPresence : StatPart
    {
        public PresenceStat which;

        private bool Value(StatRequest req, out float factor, out float offset, out GameCondition_GodPresence c)
        {
            factor = 1f;
            offset = 0f;
            c = null;
            if (Find.TickManager == null || Find.TickManager.TicksGame > GameCondition_GodPresence.AnyOwnUntil || !req.HasThing) return false;
            var t = req.Thing;
            c = GodPresenceOwn.On(t.MapHeld);
            if (c == null) return false;
            var e = c.Effect;
            float s = c.Strength;
            bool h = c.Harsh;
            var p = t as Pawn;
            switch (which)
            {
                case PresenceStat.MiningYield:
                    factor = GodPresenceOwnMath.MiningYield(e, s, h);
                    break;
                case PresenceStat.BenchSpeed:
                    if (t.TryGetComp<CompPowerTrader>()?.PowerOn == true) factor = GodPresenceOwnMath.BenchSpeed(e, s, h);
                    break;
                case PresenceStat.Trade:
                    offset = GodPresenceOwnMath.Trade(e, s, h);
                    break;
                case PresenceStat.SleepHealing:
                    if (p != null && p.Spawned) factor = GodPresenceOwnMath.SleepHealing(e, s, h, !p.Awake());
                    break;
                case PresenceStat.Immunity:
                    factor = GodPresenceOwnMath.Immunity(e, s, h);
                    break;
                case PresenceStat.ColdSooner:
                    if (p != null) offset = GodPresenceOwnMath.ColdSooner(e, s, h, Weak(p));
                    break;
                case PresenceStat.Psychic:
                    if (p != null) offset = GodPresenceOwnMath.Psychic(e, s, h);
                    break;
            }
            return factor != 1f || offset != 0f;
        }

        /// <summary>The old (three quarters of their kind's years), the sick and babies.</summary>
        private static bool Weak(Pawn p)
        {
            if (p.DevelopmentalStage.Baby()) return true;
            float life = p.RaceProps?.lifeExpectancy ?? 80f;
            if (p.ageTracker != null && p.ageTracker.AgeBiologicalYearsFloat >= life * 0.75f) return true;
            var hediffs = p.health?.hediffSet?.hediffs;
            if (hediffs == null) return false;
            for (int i = 0; i < hediffs.Count; i++)
                if (hediffs[i].def.makesSickThought)
                    return true;
            return false;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!Value(req, out float f, out float o, out _)) return;
            val = val * f + o;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!Value(req, out float f, out float o, out var c)) return null;
            return c.LabelCap + ": " + (f != 1f ? "x" + f.ToStringPercent() : (o >= 0f ? "+" : "") + (which == PresenceStat.ColdSooner ? o.ToStringTemperatureOffset("F0") : o.ToStringPercent()));
        }
    }

    /// <summary>Puts <see cref="StatPart_GodPresence"/> on the game's stats it changes, once, when the game starts.</summary>
    [StaticConstructorOnStartup]
    public static class GodPresenceStatsStartup
    {
        static GodPresenceStatsStartup()
        {
            Add(StatDefOf.MiningYield, PresenceStat.MiningYield);
            Add(StatDefOf.WorkTableWorkSpeedFactor, PresenceStat.BenchSpeed);
            Add(StatDefOf.TradePriceImprovement, PresenceStat.Trade);
            Add(StatDefOf.InjuryHealingFactor, PresenceStat.SleepHealing);
            Add(StatDefOf.ImmunityGainSpeed, PresenceStat.Immunity);
            Add(StatDefOf.ComfyTemperatureMin, PresenceStat.ColdSooner);
            Add(StatDefOf.PsychicSensitivity, PresenceStat.Psychic);
        }

        private static void Add(StatDef stat, PresenceStat which)
        {
            try
            {
                if (stat == null) return;
                stat.parts ??= new List<StatPart>();
                if (!stat.parts.Any(x => x is StatPart_GodPresence g && g.which == which)) stat.parts.Add(new StatPart_GodPresence { parentStat = stat, which = which });
            }
            catch (Exception e)
            {
                Log.Warning("[Rimfolk] A god's presence can't change " + stat?.defName + ": " + e.Message);
            }
        }
    }

    /// <summary>
    /// What a presence makes people feel while it lasts (its ThoughtDefs name the effect in their worker's def): the Guest's
    /// warm evening (its visitors more than the colony's own), the long noon that won't let those who shun the light rest.
    /// </summary>
    public class ThoughtWorker_GodPresence : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p?.Map == null || Find.TickManager.TicksGame > GameCondition_GodPresence.AnyOwnUntil) return ThoughtState.Inactive;
            var c = GodPresenceOwn.On(p.Map);
            if (c == null) return ThoughtState.Inactive;
            switch (def.defName)
            {
                case "Rimfolk_GodPO_WarmEvening":
                {
                    bool visitor = !p.IsColonist && !p.IsPrisoner && !p.IsSlave && p.Faction != null && !p.Faction.HostileTo(Faction.OfPlayer);
                    if (!p.IsColonist && !visitor) return ThoughtState.Inactive;
                    int mood = GodPresenceOwnMath.WarmEveningMood(c.Effect, c.Harsh, visitor);
                    return mood == 0 ? ThoughtState.Inactive : ThoughtState.ActiveAtStage(visitor ? 1 : 0);
                }
                case "Rimfolk_GodPO_SunWontSet":
                    return c.Effect == PresenceEffect.LongNoon && p.Awake() && ShunsLight(p) && p.Map.skyManager.CurSkyGlow > 0.5f ? ThoughtState.ActiveDefault : ThoughtState.Inactive;
            }
            return ThoughtState.Inactive;
        }

        /// <summary>A night owl, or someone the sun hurts (the game's and the player's mods' genes, by name).</summary>
        private static bool ShunsLight(Pawn p)
        {
            if (p.story?.traits != null && p.story.traits.allTraits.Any(t => t.def.defName == "NightOwl")) return true;
            var genes = p.genes?.GenesListForReading;
            return genes != null && genes.Any(g => g.Active && (g.def.defName.IndexOf("UVSensitiv", StringComparison.Ordinal) >= 0 || g.def.defName == "VRE_Photophobia"));
        }
    }

    /// <summary>Insults and slights are rarer under the Old Horn's peace and the Guest's warm evening.</summary>
    [HarmonyPatch(typeof(InteractionWorker_Insult), nameof(InteractionWorker_Insult.RandomSelectionWeight))]
    public static class Patch_GodPresenceInsult
    {
        public static void Postfix(Pawn initiator, ref float __result) => __result *= Patch_GodPresenceSlight.Factor(initiator);
    }

    [HarmonyPatch(typeof(InteractionWorker_Slight), nameof(InteractionWorker_Slight.RandomSelectionWeight))]
    public static class Patch_GodPresenceSlight
    {
        public static void Postfix(Pawn initiator, ref float __result) => __result *= Factor(initiator);

        internal static float Factor(Pawn initiator)
        {
            if (!RimfolkPatches.On || initiator?.Map == null || Find.TickManager.TicksGame > GameCondition_GodPresence.AnyOwnUntil) return 1f;
            var c = GodPresenceOwn.On(initiator.Map);
            return c == null ? 1f : GodPresenceOwnMath.Insults(c.Effect, c.Strength, c.Harsh);
        }
    }

    /// <summary>An insult is far less likely to come to blows under the Old Horn's peace, less under the Guest's.</summary>
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.SocialFightChance))]
    public static class Patch_GodPresenceFights
    {
        public static void Postfix(Pawn initiator, ref float __result)
        {
            if (__result <= 0f || !RimfolkPatches.On || initiator?.Map == null || Find.TickManager.TicksGame > GameCondition_GodPresence.AnyOwnUntil) return;
            var c = GodPresenceOwn.On(initiator.Map);
            if (c != null) __result *= GodPresenceOwnMath.Fights(c.Effect, c.Strength, c.Harsh);
        }
    }

    /// <summary>Under Hush's white silence nobody starts a conversation.</summary>
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), "TryInteractRandomly")]
    public static class Patch_GodPresenceSilence
    {
        public static bool Prefix(Pawn ___pawn, ref bool __result)
        {
            if (!RimfolkPatches.On || ___pawn?.Map == null || Find.TickManager.TicksGame > GameCondition_GodPresence.AnyOwnUntil) return true;
            var c = GodPresenceOwn.On(___pawn.Map);
            if (c == null || GodPresenceOwnMath.Talks(c.Effect)) return true;
            __result = false;
            return false;
        }
    }

    /// <summary>No animal turns on anyone while the Old Horn grazes in peace on the land.</summary>
    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    public static class Patch_GodPresenceNoManhunter
    {
        public static bool Prefix(MentalStateDef stateDef, Pawn ___pawn, ref bool __result)
        {
            if (stateDef == null || !RimfolkPatches.On || ___pawn?.Map == null || Find.TickManager.TicksGame > GameCondition_GodPresence.AnyOwnUntil) return true;
            if (!___pawn.RaceProps.Animal || stateDef != MentalStateDefOf.Manhunter && stateDef != MentalStateDefOf.ManhunterPermanent) return true;
            var c = GodPresenceOwn.On(___pawn.Map);
            if (c == null || !GodPresenceOwnMath.CalmsBeasts(c.Effect, c.Harsh)) return true;
            __result = false;
            return false;
        }
    }

    /// <summary>Batteries hold under Pip's hum: a share of what they give up (to the net or on their own) stays in them.</summary>
    [HarmonyPatch(typeof(CompPowerBattery), nameof(CompPowerBattery.DrawPower))]
    public static class Patch_GodPresenceBatteries
    {
        public static void Prefix(CompPowerBattery __instance, ref float amount)
        {
            if (amount <= 0f || !RimfolkPatches.On || Find.TickManager.TicksGame > GameCondition_GodPresence.AnyOwnUntil) return;
            var c = GodPresenceOwn.On(__instance.parent?.Map);
            if (c == null) return;
            float kept = GodPresenceOwnMath.BatteryKept(c.Effect, c.Strength, c.Harsh);
            if (kept > 0f) amount *= 1f - kept;
        }
    }

    /// <summary>Solar panels give more in the long noon (on top of the held light).</summary>
    [HarmonyPatch(typeof(CompPowerPlantSolar), "DesiredPowerOutput", MethodType.Getter)]
    public static class Patch_GodPresenceSolar
    {
        public static void Postfix(CompPowerPlantSolar __instance, ref float __result)
        {
            if (__result <= 0f || !RimfolkPatches.On || Find.TickManager.TicksGame > GameCondition_GodPresence.AnyOwnUntil) return;
            var c = GodPresenceOwn.On(__instance.parent?.Map);
            if (c != null) __result *= GodPresenceOwnMath.Solar(c.Effect, c.Strength);
        }
    }

    /// <summary>Lamps reach less far under the dark (the glow grid re-lit when it comes and goes: GodPresenceShows.Lamps).</summary>
    [HarmonyPatch(typeof(CompGlower), nameof(CompGlower.GlowRadius), MethodType.Getter)]
    public static class Patch_GodPresenceLamps
    {
        public static void Postfix(CompGlower __instance, ref float __result)
        {
            if (__result <= 0f || !RimfolkPatches.On || Find.TickManager == null || Find.TickManager.TicksGame > GameCondition_GodPresence.AnyOwnUntil) return;
            var t = __instance.parent;
            if (t == null || !t.Spawned || t.def.category != ThingCategory.Building) return;
            var c = GodPresenceOwn.On(t.Map);
            if (c != null) __result *= GodPresenceOwnMath.Lamps(c.Effect, c.Strength, c.Harsh);
        }
    }

    /// <summary>Pip's prank: one door won't open for one person while it lasts.</summary>
    [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.PawnCanOpen))]
    public static class Patch_GodPresencePrankDoor
    {
        public static void Postfix(Building_Door __instance, Pawn p, ref bool __result)
        {
            if (!__result || GodPresenceShows.PrankDoor == null || __instance != GodPresenceShows.PrankDoor || p != GodPresenceShows.PrankPawn) return;
            __result = false;
        }
    }

    /// <summary>Someone else opening the prank's door ends the prank.</summary>
    [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.StartManualOpenBy))]
    public static class Patch_GodPresencePrankOpened
    {
        public static void Postfix(Building_Door __instance, Pawn opener)
        {
            if (GodPresenceShows.PrankDoor == null || __instance != GodPresenceShows.PrankDoor) return;
            try
            {
                GodPresenceShows.OpenedBy(__instance, opener);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the prank's door opened", e);
            }
        }
    }

    /// <summary>A tree felled under the green leaning leaves its feller uneasy.</summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.PlantCollected))]
    public static class Patch_GodPresenceTreeFelled
    {
        public static void Prefix(Plant __instance, Pawn by)
        {
            if (!RimfolkPatches.On || by == null || Find.TickManager.TicksGame > GameCondition_GodPresence.AnyOwnUntil) return;
            try
            {
                GodPresenceShows.TreeFelled(__instance, by);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a tree felled under the green leaning", e);
            }
        }
    }

    /// <summary>
    /// The wells run clean while Murmur sings (with Dubs Bad Hygiene): nobody comes down with its sicknesses of bad water
    /// (cholera, dysentery, diarrhoea) on the map while the presence lasts.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff), new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class Patch_GodPresenceCleanWells
    {
        private static readonly HashSet<string> Water = new HashSet<string> { "Cholera", "Dysentery", "Diarrhea" };

        public static bool Prepare() => ModsConfig.IsActive(GodPresenceOwn.DubsWells);

        public static string StandsDownWhy() => ModsConfig.IsActive(GodPresenceOwn.DubsWells) ? "" : "Dubs Bad Hygiene isn't loaded";

        public static bool Prefix(Hediff hediff, Pawn ___pawn)
        {
            if (hediff?.def == null || !RimfolkPatches.On || Find.TickManager.TicksGame > GameCondition_GodPresence.AnyOwnUntil || !Water.Contains(hediff.def.defName)) return true;
            var c = GodPresenceOwn.On(___pawn?.MapHeld);
            return c == null || c.Effect != PresenceEffect.Rain || c.Harsh;
        }
    }
}
