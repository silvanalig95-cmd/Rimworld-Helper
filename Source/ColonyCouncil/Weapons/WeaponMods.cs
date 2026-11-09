using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The weapon mods the card fits with, all read softly by name (their code read in the player's uploaded DLLs, never
    /// run here): Simple Sidearms' sidearms (what a colonist carries, which it remembers), Dual Wield's off hand (the weapon,
    /// which weapons it allows, its penalties), RunAndGun's shooting on the move (its penalty, the weapons it forbids) and
    /// Melee Animation's moves (its execution animations and duels for a weapon). Without the mod, each answer is "not
    /// loaded" and nothing else changes; a mod whose names moved is read as not loaded, once warned.
    /// </summary>
    public static class WeaponMods
    {
        private static T Safe<T>(string what, Func<T> f, T fallback)
        {
            try
            {
                return f();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("reading " + what + " for the weapon card", e);
                return fallback;
            }
        }

        private static object Member(object o, string name)
        {
            if (o == null) return null;
            var t = o as Type ?? o.GetType();
            var target = o is Type ? null : o;
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var f = t.GetField(name, all);
            if (f != null) return f.GetValue(target);
            var p = t.GetProperty(name, all);
            return p?.GetValue(target, null);
        }

        // ---------------------------------------------------------------- Simple Sidearms

        private static readonly Type SidearmMemory = GenTypes.GetTypeInAnyAssembly("SimpleSidearms.rimworld.CompSidearmMemory");

        public static bool SidearmsLoaded => SidearmMemory != null;

        /// <summary>The weapons a colonist carries besides those in hand: Simple Sidearms keeps a colonist's sidearms in their inventory.</summary>
        public static List<Thing> Carried(Pawn p)
        {
            var list = new List<Thing>();
            if (p?.inventory?.innerContainer == null) return list;
            foreach (var t in p.inventory.innerContainer)
                if (t != null && WeaponLive.Rateable(t.def)) list.Add(t);
            return list;
        }

        /// <summary>Whether Simple Sidearms remembers this weapon (its kind and stuff) as one of the colonist's sidearms.</summary>
        public static bool Remembered(Pawn p, Thing t)
        {
            if (!SidearmsLoaded || p == null || t == null) return false;
            return Safe("Simple Sidearms' memory", () =>
            {
                var comp = p.AllComps?.FirstOrDefault(c => c != null && SidearmMemory.IsInstanceOfType(c));
                if (!(Member(comp, "rememberedWeapons") is IEnumerable list)) return false;
                foreach (var pair in list)
                    if (Member(pair, "thing") as ThingDef == t.def && Member(pair, "stuff") as ThingDef == t.Stuff)
                        return true;
                return false;
            }, false);
        }

        /// <summary>
        /// The weapons in a colonist's pack Simple Sidearms doesn't remember, as its own unloading reads its memory
        /// (Pawn_InventoryTracker.FirstUnloadableThing): each remembered kind and stuff counts once, the one in hand first; a
        /// weapon left over is one it has unloaded to storage. Empty without Simple Sidearms.
        /// </summary>
        public static List<Thing> Unremembered(Pawn p)
        {
            var list = new List<Thing>();
            if (!SidearmsLoaded || p?.inventory?.innerContainer == null) return list;
            return Safe("Simple Sidearms' memory", () =>
            {
                var comp = p.AllComps?.FirstOrDefault(c => c != null && SidearmMemory.IsInstanceOfType(c));
                if (comp == null) return list;
                var left = new List<(ThingDef thing, ThingDef stuff)>();
                if (Member(comp, "rememberedWeapons") is IEnumerable remembered)
                    foreach (var pair in remembered)
                        left.Add((Member(pair, "thing") as ThingDef, Member(pair, "stuff") as ThingDef));
                bool Take(Thing t)
                {
                    int i = left.FindIndex(x => x.thing == t.def && x.stuff == t.Stuff);
                    if (i < 0) return false;
                    left.RemoveAt(i);
                    return true;
                }
                if (p.equipment?.Primary != null) Take(p.equipment.Primary);
                foreach (var t in p.inventory.innerContainer)
                    if (t != null && t.def.IsWeapon && WeaponLive.Rateable(t.def) && !Take(t))
                        list.Add(t);
                return list;
            }, new List<Thing>());
        }

        private static readonly MethodInfo MemoryOf = SidearmMemory != null ? AccessTools.Method(SidearmMemory, "GetMemoryCompForPawn") : null;
        private static readonly MethodInfo InformAdded = SidearmMemory != null ? AccessTools.Method(SidearmMemory, "InformOfAddedSidearm") : null;

        /// <summary>Whether a weapon in a colonist's pack can be made a sidearm Simple Sidearms remembers (its own memory, by name).</summary>
        public static bool CanRemember => MemoryOf != null && InformAdded != null;

        /// <summary>Simple Sidearms told, as its own sidearm job tells it, that a weapon in their pack is one of their sidearms; false if it can't be.</summary>
        public static bool Remember(Pawn p, Thing t)
        {
            if (!CanRemember || p == null || t == null) return false;
            return Safe("Simple Sidearms' memory, a sidearm remembered", () =>
            {
                var args = MemoryOf.GetParameters().Length == 2 ? new object[] { p, true } : new object[] { p };
                var memory = MemoryOf.Invoke(null, args);
                if (memory == null) return false;
                InformAdded.Invoke(memory, new object[] { t });
                return true;
            }, false);
        }

        /// <summary>A hand or an arm missing: Dual Wield (Ext_Pawn.HasMissingArmOrHand) then allows nothing in the off hand.</summary>
        public static bool MissingHand(Pawn p)
        {
            var hediffs = p?.health?.hediffSet;
            if (hediffs == null) return false;
            foreach (var h in hediffs.GetMissingPartsCommonAncestors())
                if (h?.Part?.def == BodyPartDefOf.Hand || h?.Part?.def == BodyPartDefOf.Arm)
                    return true;
            return false;
        }

        /// <summary>Their Ideology role bars a kind of weapon (the game's role effects: no ranged weapons, no melee weapons).</summary>
        public static bool RoleBars(Pawn p, bool ranged)
        {
            var effects = p?.Ideo?.GetRole(p)?.def?.roleEffects;
            if (effects == null) return false;
            foreach (var e in effects)
                if (ranged ? e is RoleEffect_NoRangedWeapons : e is RoleEffect_NoMeleeWeapons)
                    return true;
            return false;
        }

        // ---------------------------------------------------------------- Dual Wield

        private static readonly Type DualWieldMod = GenTypes.GetTypeInAnyAssembly("DualWield.DualWield");
        private static readonly MethodInfo TryGetOffHand = GenTypes.GetTypeInAnyAssembly("DualWield.Ext_Pawn_EquipmentTracker") is Type t1 ? AccessTools.Method(t1, "TryGetOffHandEquipment") : null;
        private static readonly MethodInfo CanBeOffHandM = GenTypes.GetTypeInAnyAssembly("DualWield.Ext_ThingDef") is Type t2 ? AccessTools.Method(t2, "CanBeOffHand") : null;
        private static readonly MethodInfo IsTwoHandM = GenTypes.GetTypeInAnyAssembly("DualWield.Ext_ThingDef") is Type t3 ? AccessTools.Method(t3, "IsTwoHand") : null;

        public static bool DualWieldLoaded => DualWieldMod != null && TryGetOffHand != null;

        /// <summary>The weapon in a colonist's off hand (Dual Wield), or null.</summary>
        public static ThingWithComps OffHand(Pawn p)
        {
            if (!DualWieldLoaded || p?.equipment == null) return null;
            return Safe("Dual Wield's off hand", () =>
            {
                var args = new object[] { p.equipment, null };
                return TryGetOffHand.Invoke(null, args) is bool ok && ok ? args[1] as ThingWithComps : null;
            }, null);
        }

        /// <summary>Whether Dual Wield lets this weapon go in the off hand (null: not loaded).</summary>
        public static bool? CanBeOffHand(ThingDef def) =>
            CanBeOffHandM == null || def == null ? (bool?)null : Safe("Dual Wield's off-hand weapons", () => CanBeOffHandM.Invoke(null, new object[] { def }) is bool b && b, false);

        /// <summary>Whether Dual Wield counts this weapon two-handed (null: not loaded).</summary>
        public static bool? IsTwoHand(ThingDef def) =>
            IsTwoHandM == null || def == null ? (bool?)null : Safe("Dual Wield's two-handed weapons", () => IsTwoHandM.Invoke(null, new object[] { def }) is bool b && b, false);

        /// <summary>Dual Wield's penalties as its settings hold them (its defaults where they can't be read).</summary>
        public static WeaponForMath.DualWieldRules DualRules()
        {
            var d = new WeaponForMath.DualWieldRules();
            if (DualWieldMod == null) return d;
            return Safe("Dual Wield's penalties", () =>
            {
                var s = Member(DualWieldMod, "Settings");
                if (s == null) return d;
                float F(string name, float def) => Member(s, name) is float f ? f : def;
                d.CooldownOffHand = F("StaticCooldownPOffHand", d.CooldownOffHand);
                d.CooldownMainHand = F("StaticCooldownPMainHand", d.CooldownMainHand);
                d.AccuracyOffHand = F("StaticAccPOffHand", d.AccuracyOffHand);
                d.AccuracyMainHand = F("StaticAccPMainHand", d.AccuracyMainHand);
                d.CooldownPerLevel = F("DynamicCooldownP", d.CooldownPerLevel);
                d.AccuracyPerLevel = F("DynamicAccP", d.AccuracyPerLevel);
                return d;
            }, d);
        }

        // ---------------------------------------------------------------- RunAndGun

        private static readonly Type RunAndGunMod = GenTypes.GetTypeInAnyAssembly("RunAndGun.RunAndGun");

        public static bool RunAndGunLoaded => RunAndGunMod != null;

        /// <summary>RunAndGun for a gun: allowed on the move, its accuracy penalty and how much slower the shooter walks (percent).</summary>
        public struct OnTheMove
        {
            public bool Allowed;
            public int AccuracyPenalty, Slower;
        }

        /// <summary>
        /// What RunAndGun does with a gun, as its settings hold it: forbidden on the move if its forbidden list selects it, else
        /// shot on the move a penalty less accurate, the shooter slowed by its light or heavy share (a weapon its weight list
        /// doesn't name as light counts heavy). Null without RunAndGun or for a melee weapon.
        /// </summary>
        public static OnTheMove? RunAndGunFor(ThingDef def)
        {
            if (RunAndGunMod == null || def == null || !def.IsRangedWeapon) return null;
            return Safe("RunAndGun's settings", () =>
            {
                var s = Member(RunAndGunMod, "settings");
                var o = new OnTheMove { Allowed = true, AccuracyPenalty = 10, Slower = 40 };
                if (s == null) return o;
                if (Member(s, "accuracyPenalty") is int acc) o.AccuracyPenalty = acc;
                bool Selected(string dict, out bool known)
                {
                    known = false;
                    if (!(Member(s, dict) is IDictionary d) || !d.Contains(def.defName)) return false;
                    known = true;
                    return Member(d[def.defName], "isSelected") is bool b && b;
                }
                if (Selected("forbiddenWeapons", out _)) o.Allowed = false;
                bool heavy = Selected("selectedWeapons", out bool listed);
                bool light = listed && !heavy;
                if (Member(s, light ? "movementPenaltyLight" : "movementPenaltyHeavy") is int slow) o.Slower = slow;
                return o;
            }, (OnTheMove?)null);
        }

        // ---------------------------------------------------------------- Melee Animation

        private static readonly Type AnimDefType = GenTypes.GetTypeInAnyAssembly("AM.AnimDef");
        private static readonly MethodInfo Executions = AnimDefType != null ? AccessTools.Method(AnimDefType, "GetExecutionAnimationsForPawnAndWeapon") : null;
        private static readonly MethodInfo TryGetTweak = GenTypes.GetTypeInAnyAssembly("AM.Tweaks.TweakDataManager") is Type t4
            ? AccessTools.Method(t4, "TryGetTweak", new[] { typeof(ThingDef) })
            : GenTypes.GetTypeInAnyAssembly("AM.TweakDataManager") is Type t5 ? AccessTools.Method(t5, "TryGetTweak", new[] { typeof(ThingDef) }) : null;
        private static readonly Type MeleeAnimationCore = GenTypes.GetTypeInAnyAssembly("AM.Core");

        public static bool MeleeAnimationLoaded => AnimDefType != null && TryGetTweak != null;

        public struct Moves
        {
            /// <summary>Melee Animation draws and animates this weapon's blows.</summary>
            public bool Animated;
            /// <summary>Execution animations open to it at this colonist's Melee (0 with executions switched off).</summary>
            public int ExecutionMoves;
            public bool ExecutionsOn;
            /// <summary>A long blade or staff: it can be used in Melee Animation's friendly duels.</summary>
            public bool Duels;
        }

        /// <summary>What Melee Animation does with a melee weapon for a colonist; null without it or for a gun.</summary>
        public static Moves? MeleeAnimationFor(Pawn p, ThingDef def)
        {
            if (!MeleeAnimationLoaded || def == null || !def.IsMeleeWeapon) return null;
            return Safe("Melee Animation's moves", () =>
            {
                var m = new Moves();
                var tweak = TryGetTweak.Invoke(null, new object[] { def });
                m.Animated = tweak != null;
                if (!m.Animated) return m;
                m.Duels = (Member(tweak, "MeleeWeaponType")?.ToString() ?? "").Contains("Long");
                var settings = MeleeAnimationCore != null ? Member(MeleeAnimationCore, "Settings") : null;
                m.ExecutionsOn = !(Member(settings, "EnableExecutions") is bool on) || on;
                if (m.ExecutionsOn && Executions != null && p != null)
                {
                    int level = p.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 8;
                    if (Executions.Invoke(null, new object[] { p, def, (int?)level }) is IEnumerable anims)
                        foreach (var _ in anims) m.ExecutionMoves++;
                }
                return m;
            }, (Moves?)null);
        }
    }
}
