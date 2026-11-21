using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Traits other mods give the colony's people during play, by the setting "Traits other mods give" (21 November 2026,
    /// the player, of Guido, a shotgunner given brawler by Vanilla Social Interactions Expanded after an illness: "be a bit
    /// more delicate, I guess, with giving brawler"): as they come, only where they fit the person's life as Rimfolk reads
    /// it (TraitChanges.Unfit: no brawler for one who fights at range), or none. Caught where every mod gives a trait, the
    /// game's TraitSet.GainTrait, and only for another mod's gain: never a newcomer's traits as they are made, a gene's, a
    /// scenario's, the game's own (a growth moment, a quest, the dev tools), Rimfolk's own changes, or a click of the
    /// player's (an editor's window, a letter's choice). Who gave it is read from the calls that led to it (the nearest one
    /// that isn't the game's own or the runtime's). A gain refused is told in a message, with who gave it and why; the
    /// other mod may still send its own letter of it, which the message answers.
    /// </summary>
    public static class TraitGains
    {
        private static Assembly game, ours;

        /// <summary>Whether this gain may go ahead; false refuses it, and the colony is told why.</summary>
        public static bool Allow(Pawn p, Trait trait)
        {
            try
            {
                var setting = ColonyCouncilMod.Settings?.otherTraitGains ?? OtherTraitGains.WhereTheyFit;
                if (trait?.def == null || p == null) return true;
                bool playing = Current.ProgramState == ProgramState.Playing && Find.TickManager != null;
                bool colonys = p.Faction != null && p.Faction.IsPlayer && p.RaceProps != null && p.RaceProps.Humanlike && !p.Dead;
                if (!TraitGainMath.Looks(setting, RimfolkPatches.On, playing, colonys, PawnGenerator.IsBeingGenerated(p), trait.sourceGene != null, trait.ScenForced, false))
                    return true;
                var callers = Callers(out var mod, out bool clicked);
                var giver = TraitGainMath.Giver(callers, clicked);
                if (giver != TraitGiver.OtherMod) return true;
                string why = setting == OtherTraitGains.None ? "none" : TraitChanges.Unfit(p, trait.def, trait.Degree);
                string refused = TraitGainMath.Refused(setting, giver, why);
                if (refused == null) return true;
                Tell(p, trait, mod, refused == "none" ? "CC_TraitGain_Why_None".Translate().Resolve() : refused);
                return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("traits other mods give", e);
                return true;
            }
        }

        /// <summary>
        /// The calls that led to the gain, nearest first, as TraitGainMath.Giver reads them: "rimfolk", or "mod" (another
        /// mod's, named in <paramref name="mod"/>), the game's own, the runtime's, Harmony's and the patched method's own
        /// frame passed over. <paramref name="clicked"/>: it came of a click of the player's, inside the game's drawing of
        /// its interface (the ticks never run there; a letter's choice, an editor's window, the dev tools do).
        /// </summary>
        private static List<string> Callers(out string mod, out bool clicked)
        {
            mod = null;
            clicked = false;
            game ??= typeof(TraitSet).Assembly;
            ours ??= typeof(TraitGains).Assembly;
            var list = new List<string>();
            var frames = new StackTrace(1, false).GetFrames();
            if (frames == null) return list;
            foreach (var f in frames)
            {
                var m = f?.GetMethod();
                if (m == null) continue;
                if (m.Name == "OnGUI" || m.Name == "UIRootOnGUI" || m.DeclaringType != null && typeof(UIRoot).IsAssignableFrom(m.DeclaringType))
                {
                    clicked = true;
                    break;
                }
            }
            int seen = 0;
            foreach (var f in frames)
            {
                var m = f?.GetMethod();
                var asm = m?.DeclaringType?.Assembly;
                if (asm == null || asm.IsDynamic) continue;
                if (asm == ours)
                {
                    // Rimfolk's own hook isn't a giver: only a call of Rimfolk's below it.
                    if (m.DeclaringType == typeof(TraitGains) || m.DeclaringType == typeof(Patch_OtherModsTraitGains)) continue;
                    list.Add("rimfolk");
                    break;
                }
                // Only the calls close to the gain: far up the stack is the game's tick or a mod that runs the ticks its own way.
                if (++seen > CallsRead) break;
                if (asm == game || Runtime(asm)) continue;
                list.Add("mod");
                mod = ModOf(asm);
                break;
            }
            return list;
        }

        /// <summary>How many of the game's own calls below the gain are read for the one who gave it (a mod's call to it is seldom more than a few away).</summary>
        private const int CallsRead = 10;

        /// <summary>The runtime's, Unity's and Harmony's own assemblies: never a giver.</summary>
        private static bool Runtime(Assembly asm)
        {
            string n = asm.GetName().Name ?? "";
            return n == "mscorlib" || n == "netstandard" || n.StartsWith("System", StringComparison.Ordinal) || n.StartsWith("Unity", StringComparison.Ordinal)
                   || n.StartsWith("Mono", StringComparison.Ordinal) || n == "0Harmony" || n.StartsWith("HarmonyLib", StringComparison.Ordinal) || n.StartsWith("DMD", StringComparison.Ordinal)
                   || n == "Assembly-CSharp-firstpass";
        }

        /// <summary>The name of the mod whose assembly it is, as the mod list shows it.</summary>
        private static string ModOf(Assembly asm)
        {
            try
            {
                foreach (var m in LoadedModManager.RunningModsListForReading)
                    if (m?.assemblies?.loadedAssemblies != null && m.assemblies.loadedAssemblies.Contains(asm)) return m.Name;
            }
            catch (Exception)
            {
                // The mod list unreadable: named as another mod.
            }
            return null;
        }

        /// <summary>The colony hears that the trait wasn't given, who gave it and why.</summary>
        private static void Tell(Pawn p, Trait trait, string mod, string why)
        {
            string who = mod.NullOrEmpty() ? "CC_TraitGain_AnotherMod".Translate().Resolve() : mod;
            string text = "CC_TraitGain_Refused".Translate(p.Named("PAWN"), trait.LabelCap.Named("TRAIT"), who.Named("MOD"), why.Named("WHY")).Resolve();
            Notify.Message(text, p.Spawned ? new LookTargets(p) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, Importance.Notable);
            if (Prefs.DevMode) Log.Message("[Rimfolk] " + text);
        }
    }

    /// <summary>Where every mod gives a trait: another mod's gain during play is weighed by the setting before it lands (TraitGains).</summary>
    [HarmonyPatch(typeof(TraitSet), nameof(TraitSet.GainTrait))]
    public static class Patch_OtherModsTraitGains
    {
        public static bool Prefix(Pawn ___pawn, Trait __0) => TraitGains.Allow(___pawn, __0);
    }
}
