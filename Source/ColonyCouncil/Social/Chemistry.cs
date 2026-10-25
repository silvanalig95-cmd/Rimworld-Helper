using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// How two colonists get on (the arithmetic is in ChemistryMath). Time together doesn't make everyone friends: it
    /// follows the game's own compatibility between the two, a fixed number for each pair that the game already uses
    /// for how often they have deep talks or trade insults. What passes between them in conversation (the game's
    /// chitchat, deep talks, slights and insults, other mods' talks, Rimfolk's moments and talks) counts for more or
    /// less as it's given; Rimfolk's warm moments come up less often between two who don't click, and its frictions
    /// more; and after a few days side by side the way they get on is a lasting feeling of its own in the Social tab.
    /// Partners and anyone outside the colony are left alone. With the social web on.
    /// </summary>
    public static class Chemistry
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Whether the way two people get on counts between these two: two of the colony's people, both past babyhood.</summary>
        public static bool Applies(Pawn a, Pawn b)
        {
            if (S == null || !S.socialWeb || a == null || b == null || a == b) return false;
            if (!Ours(a) || !Ours(b) || !People.HasInnerLife(a) || !People.HasInnerLife(b)) return false;
            return a.relations != null && b.relations != null && !a.DevelopmentalStage.Baby() && !b.DevelopmentalStage.Baby();
        }

        private static bool Ours(Pawn p) => p.IsColonist || p.IsSlaveOfColony;

        /// <summary>The game's compatibility between the two (0 between two of different races, as the game has it).</summary>
        public static float Of(Pawn a, Pawn b) => a?.relations == null || b == null ? 0f : a.relations.CompatibilityWith(b);

        /// <summary>Whole days the two have both been with the colony: the shorter of their times as colonists.</summary>
        public static int DaysTogether(Pawn a, Pawn b)
        {
            int ta = a.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0;
            int tb = b.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0;
            return Math.Min(ta, tb) / GenDate.TicksPerDay;
        }

        /// <summary>The lasting feeling between them now (ChemistryMath stages), or None: never between partners.</summary>
        public static int Stage(Pawn a, Pawn b)
        {
            if (!Applies(a, b) || LovePartnerRelationUtility.LovePartnerRelationExists(a, b)) return ChemistryMath.None;
            return ChemistryMath.Stage(Of(a, b), DaysTogether(a, b));
        }

        /// <summary>For the Social tab's note: -1 when two don't click, 1 when they get on easily, 0 for an ordinary pair or partners.</summary>
        public static int Leaning(Pawn a, Pawn b)
        {
            if (!Applies(a, b) || LovePartnerRelationUtility.LovePartnerRelationExists(a, b)) return 0;
            float c = Of(a, b);
            return c <= ChemistryMath.PoorAt ? -1 : c >= ChemistryMath.GoodAt ? 1 : 0;
        }

        /// <summary>How likely one of Rimfolk's moments comes up between them: warm ones and frictions follow how they get on.</summary>
        public static float MomentFactor(Pawn initiator, Pawn recipient, MomentExtension ext)
        {
            if (ext == null || (ext.tone != "warm" && !ext.Friction) || !Applies(initiator, recipient)) return 1f;
            return ChemistryMath.MomentFactor(Of(initiator, recipient), ext.Friction);
        }

        // ---------------- What passes between them ----------------

        private static HashSet<ThoughtDef> spoken;
        private static int spokenGeneration = -1;

        /// <summary>
        /// The memories that come of a conversation: every interaction's thoughts (the game's, other mods', Rimfolk's
        /// moments) and Rimfolk's talks. Worked out once (again after the defs are reloaded).
        /// </summary>
        private static bool Spoken(ThoughtDef def)
        {
            if (spoken == null || spokenGeneration != DefRefs.Generation)
            {
                spoken = new HashSet<ThoughtDef>();
                foreach (var i in DefDatabase<InteractionDef>.AllDefsListForReading)
                {
                    if (i.initiatorThought != null) spoken.Add(i.initiatorThought);
                    if (i.recipientThought != null) spoken.Add(i.recipientThought);
                }
                foreach (var t in DefDatabase<ThoughtDef>.AllDefsListForReading)
                    if (t.defName.StartsWith("Rimfolk_Talk_", StringComparison.Ordinal)) spoken.Add(t);
                spokenGeneration = DefRefs.Generation;
            }
            return def != null && spoken.Contains(def);
        }

        /// <summary>A memory of what passed between <paramref name="holder"/> and <paramref name="other"/> counts as much as the two get on.</summary>
        public static void Notify_Given(Pawn holder, Thought_Memory m, Pawn other)
        {
            if (!(m is Thought_MemorySocial social) || social.opinionOffset == 0f || !Spoken(m.def)) return;
            other ??= m.otherPawn;
            if (!Applies(holder, other)) return;
            social.opinionOffset = ChemistryMath.Scaled(social.opinionOffset, Of(holder, other));
        }
    }

    /// <summary>
    /// What passes between two colonists counts as much as they get on (Social/Chemistry.cs): a memory of a
    /// conversation has its opinion set as it's given, before the game adds it to a chitchat already there.
    /// </summary>
    [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory), new[] { typeof(Thought_Memory), typeof(Pawn) })]
    public static class Patch_Chemistry
    {
        public static void Prefix(MemoryThoughtHandler __instance, Thought_Memory newThought, Pawn otherPawn)
        {
            if (!RimfolkPatches.On || newThought == null || __instance?.pawn == null) return;
            try
            {
                Chemistry.Notify_Given(__instance.pawn, newThought, otherPawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("how two get on", e);
            }
        }
    }

    /// <summary>
    /// The lasting feeling between two who have lived side by side a while (Council_Chemistry): they don't click, rub
    /// each other the wrong way or get on each other's nerves; or they're easy company, or on the same wavelength.
    /// </summary>
    public class ThoughtWorker_Chemistry : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn other)
        {
            int stage = Chemistry.Stage(p, other);
            return stage < 0 || stage >= (def.stages?.Count ?? 0) ? ThoughtState.Inactive : ThoughtState.ActiveAtStage(stage);
        }
    }
}
