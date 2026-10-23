using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The small effects of the quirks that were only flavour (Defs/QuirkDefs/Council_Quirks.xml, the habits' file):
    /// one who knocks on wood is glad after a fight they came through unhurt; one who counts everything tells you when
    /// the food won't last the week; one who salts the doorway has fewer nightmares (Dreams.cs); one who saves the best
    /// bite for last is lifted a while longer by a good meal; a roommate of one who talks in their sleep sleeps a
    /// little worse for it, unless they're lovers. (One who talks to the dead is comforted near the graves: a mood of
    /// its own, NearGrave.)
    /// </summary>
    public static class QuirkMoments
    {
        private static bool looked;
        private static QuirkDef knocksWood, countsThings, saltsDoor, bestBite, sleepTalker;
        private static ThoughtDef knocked, bestBiteThought;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            knocksWood = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_KnocksWood");
            countsThings = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_CountsThings");
            saltsDoor = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_SaltsDoor");
            bestBite = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_BestBiteLast");
            sleepTalker = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_SleepTalker");
            knocked = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Quirk_KnockedWood");
            bestBiteThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Quirk_BestBite");
        }

        private static bool On => ColonyCouncilMod.Settings?.quirks == true && Current.Game != null;

        private static bool Has(Pawn p, QuirkDef q) => q != null && p != null && CouncilGameComponent.Instance?.quirks?.Get(p, q) != null;

        /// <summary>
        /// A fight they took part in is over (SocialWeb; it began at <paramref name="since"/>, -1 when not known): one who
        /// knocks on wood and came through it unhurt is glad of it.
        /// </summary>
        public static void Fought(Pawn p, int since)
        {
            try
            {
                Look();
                if (!On || !ColonyCouncilMod.Settings.MoodEffects || knocked == null || !Has(p, knocksWood) || p.Dead || p.Downed || p.needs?.mood == null) return;
                var hediffs = p.health?.hediffSet?.hediffs;
                if (hediffs == null) return;
                // Unhurt: no wound since the fight began (an hour before it was noticed), or from the last day when that isn't known.
                int now = Find.TickManager.TicksGame;
                int window = since >= 0 && since <= now ? now - since + GenDate.TicksPerHour : GenDate.TicksPerDay;
                foreach (var h in hediffs)
                    if ((h is Hediff_Injury || h is Hediff_MissingPart) && h.ageTicks <= window) return;
                p.needs.mood.thoughts.memories.TryGainMemory(knocked);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("knocks on wood", e);
            }
        }

        /// <summary>A memory was just given (Patch_GoodTimes): a good meal lifts one who saves the best bite for last a while longer.</summary>
        public static void Notify_Memory(Pawn p, Thought_Memory m)
        {
            if (m?.def == null || p == null || m.def == bestBiteThought) return;
            try
            {
                if (!m.def.defName.StartsWith("Ate", StringComparison.Ordinal) || m.def.stages.NullOrEmpty()) return;
                var stage = m.CurStage;
                if (stage == null || stage.baseMoodEffect <= 0f) return;
                Look();
                if (!On || !ColonyCouncilMod.Settings.MoodEffects || bestBiteThought == null || !Has(p, bestBite)) return;
                p.needs?.mood?.thoughts?.memories?.TryGainMemory(bestBiteThought);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("best bite last", e);
            }
        }

        /// <summary>Nightmares come half as often to one who salts the doorway (Dreams.cs).</summary>
        public static float NightmareFactor(Pawn p)
        {
            Look();
            return On && Has(p, saltsDoor) ? 0.5f : 1f;
        }

        /// <summary>Whether they talk in their sleep (for the roommate's mood).</summary>
        public static bool TalksInSleep(Pawn p)
        {
            Look();
            return On && Has(p, sleepTalker);
        }

        // ---------------- Counting the stores ----------------

        /// <summary>The food lasts this many days or less: one who counts everything says so.</summary>
        public const float WarnBelowDays = 7f;

        /// <summary>It has to climb back above this before they say it again.</summary>
        public const float WarnAgainAboveDays = 10f;

        private static readonly Dictionary<int, bool> warned = new Dictionary<int, bool>();
        private static Game game;

        /// <summary>Once a day: on each home map, one who counts everything tells you when the food won't last the week, once each time it runs low.</summary>
        public static void Daily()
        {
            try
            {
                Look();
                if (game != Current.Game)
                {
                    game = Current.Game;
                    warned.Clear();
                }
                if (!On || countsThings == null) return;
                foreach (var map in Find.Maps)
                {
                    if (!map.IsPlayerHome) continue;
                    float days = AdvisorWorker_Food.DaysOfFood(map);
                    warned.TryGetValue(map.uniqueID, out bool said);
                    if (days > WarnAgainAboveDays) warned[map.uniqueID] = false;
                    if (said || days >= WarnBelowDays || days <= 0f) continue;
                    Pawn counter = null;
                    foreach (var p in map.mapPawns.FreeColonistsSpawned)
                        if (Has(p, countsThings) && !p.Downed && p.Awake())
                        {
                            counter = p;
                            break;
                        }
                    if (counter == null) continue;
                    warned[map.uniqueID] = true;
                    Notify.Message("CC_Quirk_CountedStores".Translate(counter.Named("PAWN")).Resolve(), counter, MessageTypeDefOf.CautionInput, Importance.Notable);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("counts everything", e);
            }
        }
    }

    /// <summary>
    /// Sharing a bedroom with someone who talks in their sleep: a little worse for it, or, for their lover, endearing.
    /// Only while they own beds in the same room.
    /// </summary>
    public class ThoughtWorker_SleepTalkRoommate : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var s = ColonyCouncilMod.Settings;
            if (s == null || !s.quirks || !s.MoodEffects || !p.IsColonist) return ThoughtState.Inactive;
            var bed = p.ownership?.OwnedBed;
            var room = bed?.GetRoom();
            if (room == null || room.PsychologicallyOutdoors) return ThoughtState.Inactive;
            Pawn talker = null;
            foreach (var other in room.Owners)
                if (other != null && other != p && QuirkMoments.TalksInSleep(other))
                {
                    talker = other;
                    break;
                }
            if (talker == null) return ThoughtState.Inactive;
            if (LovePartnerRelationUtility.LovePartnerRelationExists(p, talker)) return ThoughtState.ActiveAtStage(0);
            return s.Negatives ? ThoughtState.ActiveAtStage(1) : ThoughtState.Inactive;
        }
    }
}
