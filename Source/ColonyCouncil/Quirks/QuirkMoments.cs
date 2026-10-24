using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The small effects of the quirks that were only flavour (Defs/QuirkDefs/Council_Quirks.xml, the habits' file):
    /// one who knocks on wood is glad after a fight they came through unhurt; one who counts everything tells you when
    /// the food won't last the week; one who salts the doorway has fewer nightmares (Dreams.cs); one who saves the best
    /// bite for last is lifted a while longer by a good meal; a roommate of one who talks in their sleep sleeps a
    /// little worse for it, unless they're lovers. (One who talks to the dead is comforted near the graves: a mood of
    /// its own, NearGrave.) And the habits of the quieter work: one who mends their own clothes (or has become a mender)
    /// mends the worst-worn thing they wear a little each day; one who doodles in the margins, while they keep at their
    /// art, is now and then inspired at it; one who sleeps light wakes when fire breaks out near them.
    /// </summary>
    public static class QuirkMoments
    {
        private static bool looked;
        private static QuirkDef knocksWood, countsThings, saltsDoor, bestBite, sleepTalker, mends, doodles, sleepsLight;
        private static ThoughtDef knocked, bestBiteThought;
        private static TraitDef mender;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            knocksWood = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_KnocksWood");
            countsThings = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_CountsThings");
            saltsDoor = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_SaltsDoor");
            bestBite = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_BestBiteLast");
            sleepTalker = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_SleepTalker");
            mends = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_MendsClothes");
            doodles = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_Doodles");
            sleepsLight = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_SleepsLight");
            mender = DefDatabase<TraitDef>.GetNamedSilentFail("Rimfolk_Mender");
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

        // ---------------- Mending ----------------

        /// <summary>A day's mending, as a share of the worst-worn piece's hit points: the habit, rooted, the trait.</summary>
        public const float MendHabit = 0.02f, MendDeep = 0.03f, MendTrait = 0.05f;

        /// <summary>Once a day (QuirkTracker.Daily): each who mends their own clothes mends the worst-worn thing they wear a little.</summary>
        public static void Mend(QuirkTracker t)
        {
            try
            {
                Look();
                var s = ColonyCouncilMod.Settings;
                if (s == null || Current.Game == null || t == null) return;
                bool habits = s.quirks && mends != null;
                bool traits = mender != null && Persona.On && s.traitChanges;
                if (!habits && !traits) return;
                foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList())
                {
                    if (p.Dead || p.Downed || p.apparel == null) continue;
                    float rate = 0f;
                    if (traits && p.story?.traits != null && p.story.traits.HasTrait(mender)) rate = MendTrait;
                    else if (habits && t.Get(p, mends) is Quirk q) rate = q.Deep ? MendDeep : MendHabit;
                    if (rate <= 0f) continue;
                    Apparel worst = null;
                    float worstShare = 1f;
                    foreach (var a in p.apparel.WornApparel)
                    {
                        if (a?.def == null || !a.def.useHitPoints || a.MaxHitPoints <= 0 || a.HitPoints >= a.MaxHitPoints) continue;
                        float share = (float)a.HitPoints / a.MaxHitPoints;
                        if (share < worstShare)
                        {
                            worstShare = share;
                            worst = a;
                        }
                    }
                    if (worst != null) worst.HitPoints = Math.Min(worst.MaxHitPoints, worst.HitPoints + Math.Max(1, (int)Math.Ceiling(worst.MaxHitPoints * rate)));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mending", e);
            }
        }

        // ---------------- Inspired at their art ----------------

        /// <summary>Each day, while they keep at their art: the chance one who doodles is inspired at it (rooted: a little more).</summary>
        public const float InspireHabit = 0.03f, InspireDeep = 0.05f;

        /// <summary>Once a day (QuirkTracker.Daily): one who doodles in the margins, still at their art, is now and then inspired.</summary>
        public static void Inspire(QuirkTracker t)
        {
            try
            {
                Look();
                if (!On || doodles == null || t == null || InspirationDefOf.Inspired_Creativity == null) return;
                foreach (var q in t.quirks.Where(x => x?.def == doodles).ToList())
                {
                    var p = q.pawn;
                    if (p == null || p.Dead || !p.Spawned || p.Downed || p.InMentalState) continue;
                    if (!PracticeMath.Lived(Practice.Hours(p, doodles.practice), doodles.practiceHours)) continue;
                    var h = p.mindState?.inspirationHandler;
                    if (h == null || h.Inspired || !Rand.Chance(q.Deep ? InspireDeep : InspireHabit)) continue;
                    h.TryStartInspiration(InspirationDefOf.Inspired_Creativity, "CC_Quirk_DoodleInspired".Translate(p.Named("PAWN")).Resolve());
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("doodles", e);
            }
        }

        // ---------------- Smoke in the night ----------------

        /// <summary>Woken once for a fire, they aren't woken again for this long.</summary>
        private const int WakeRest = GenDate.TicksPerHour * 4;

        /// <summary>A fire this close to where they sleep, or anywhere in the home area, wakes them.</summary>
        private const float SmokeRange = 30f;

        private static readonly Dictionary<int, int> woke = new Dictionary<int, int>();
        private static Game smokeGame;

        /// <summary>Every 250 ticks (CouncilGameComponent): one who sleeps light wakes when fire breaks out near them at home.</summary>
        public static void Smoke()
        {
            try
            {
                Look();
                if (!On || sleepsLight == null) return;
                var t = CouncilGameComponent.Instance?.quirks;
                if (t == null) return;
                if (smokeGame != Current.Game)
                {
                    smokeGame = Current.Game;
                    woke.Clear();
                }
                int now = Find.TickManager.TicksGame;
                foreach (var q in t.quirks)
                {
                    var p = q?.def == sleepsLight ? q.pawn : null;
                    if (p == null || !p.Spawned || p.Dead || p.Downed || p.Awake() || p.Map == null || !p.Map.IsPlayerHome) continue;
                    // Not out of a sickbed, nor someone who ought to be in one.
                    if (RestUtility.CurrentBed(p)?.Medical == true || HealthAIUtility.ShouldSeekMedicalRest(p)) continue;
                    if (woke.TryGetValue(p.thingIDNumber, out int at) && now - at < WakeRest) continue;
                    if (!FireNear(p)) continue;
                    woke[p.thingIDNumber] = now;
                    RestUtility.WakeUp(p);
                    Notify.Message("CC_Quirk_SmokeWoke".Translate(p.Named("PAWN")).Resolve(), p, MessageTypeDefOf.NeutralEvent, Importance.Notable);
                }
                if (woke.Count > 100) woke.RemoveAll(kv => now - kv.Value >= WakeRest);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("light sleepers", e);
            }
        }

        private static bool FireNear(Pawn p)
        {
            var map = p.Map;
            var fires = map.listerThings.ThingsOfDef(ThingDefOf.Fire);
            if (fires == null || fires.Count == 0) return false;
            var home = map.areaManager.Home;
            foreach (var f in fires)
                if (f != null && f.Spawned && (home[f.Position] || f.Position.InHorDistOf(p.Position, SmokeRange))) return true;
            return false;
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
