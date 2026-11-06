using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The heart's good side overflowing, the mirror of strain's day off (Episodes): in high spirits and safe, now and then
    /// (about once in four days held there, once in ten days for each colonist and once a day in the colony) something
    /// good comes of it, shown on the map with the game's own tools. Either one of the game's inspirations, picked by the
    /// game by their passions and its own rules (InspirationHandler.GetRandomAvailableInspirationDef and
    /// TryStartInspiration, with its own letter), or a kind word for someone close: they walk over in their free time
    /// (Together), say it face to face (Speech.Say), and the other gets a small good memory (a small joy, which eases
    /// their strain or fills their own good side). Either spends a little of the good side. Nothing here is saved but
    /// the times on the heart and the tracker.
    /// </summary>
    public static class GoodRelease
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public const string KindPool = "Rimfolk_Pool_GoodHeart_KindWord", InspiredPool = "Rimfolk_Pool_GoodHeart_Inspired";

        private static ThoughtDef kindWord;
        private static bool looked;

        /// <summary>The kind word's memory (Council_Heart_KindWord), looked up quietly.</summary>
        private static ThoughtDef KindWordDef
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    kindWord = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Heart_KindWord");
                }
                return kindWord;
            }
        }

        /// <summary>The hour (HeartLogic.Evaluate): in high spirits, the chance of a release, and then whether it can be now.</summary>
        public static void MaybeStart(Pawn p, PawnHeart h, HeartMapContext m, int now)
        {
            if (!GoodHeart.On || h == null || h.good < HeartMath.HighSpiritsAt) return;
            if (!Rand.Chance(HeartMath.ReleaseChancePerHour(h.good))) return;
            if (Blocked(p, h, m, now) != null) return;
            Start(p, h, now);
        }

        /// <summary>Why a release can't come for this colonist now, or null if it could.</summary>
        public static string Blocked(Pawn p, PawnHeart h, HeartMapContext m, int now)
        {
            if (!GoodHeart.On) return "off";
            if (h.good < HeartMath.HighSpiritsAt) return "not in high spirits";
            if (!p.Spawned || p.Map == null || !p.Map.IsPlayerHome) return "away";
            if (!p.IsFreeNonSlaveColonist || p.IsQuestLodger()) return "not free";
            if (!p.DevelopmentalStage.Adult()) return "child";
            if (p.Drafted || p.Downed || p.InMentalState || !RestUtility.Awake(p)) return "busy";
            if (p.GetLord() != null) return "with others";
            if (m.threat || MapFacts.ThreatOn(p.Map)) return "danger";
            if (now - h.lastReleaseTick < GenDate.TicksPerDay * HeartMath.ReleasePawnDays) return "pawn cooldown";
            var tracker = HeartTracker.Instance;
            if (tracker == null || now - tracker.lastColonyReleaseTick < GenDate.TicksPerDay * HeartMath.ReleaseColonyDays) return "colony cooldown";
            return null;
        }

        /// <summary>
        /// A release now (also from dev mode): a kind word when someone close could use one (more likely when they're under
        /// strain or low), otherwise one of the game's inspirations if the game allows one, otherwise a kind word to whoever
        /// is close. Returns whether something began.
        /// </summary>
        public static bool Start(Pawn p, PawnHeart h, int now)
        {
            var other = Close(p);
            var insp = InspirationFor(p);
            if (other == null && insp == null) return false;
            bool needs = other != null && (Heart.BandOf(other) >= StrainBand.Strained || (other.needs?.mood?.CurLevelPercentage ?? 1f) < 0.35f);
            bool kind = other != null && (insp == null || Rand.Chance(needs ? 0.7f : 0.5f));
            bool began = kind ? KindWord(p, other, now) : Inspire(p, h, insp, now);
            if (!began) return false;
            h.lastReleaseTick = now;
            var tracker = HeartTracker.Instance;
            if (tracker != null) tracker.lastColonyReleaseTick = now;
            return true;
        }

        // ---------------- One of the game's inspirations ----------------

        /// <summary>An inspiration the game would give them now (by their passions and its own rules), or null.</summary>
        private static InspirationDef InspirationFor(Pawn p)
        {
            var handler = p.mindState?.inspirationHandler;
            if (handler == null || handler.Inspired || p.health?.capacities == null || !p.health.capacities.CanBeAwake) return null;
            try
            {
                return handler.GetRandomAvailableInspirationDef();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("the heart's good side, an inspiration", e);
                return null;
            }
        }

        private static bool Inspire(Pawn p, PawnHeart h, InspirationDef def, int now)
        {
            var handler = p.mindState?.inspirationHandler;
            if (handler == null || def == null) return false;
            string reason = "CC_Good_InspiredReason".Translate(p.Named("PAWN")).Resolve();
            // The game's own start, with its own letter (the reason said at its foot).
            if (!handler.TryStartInspiration(def, reason)) return false;
            h.good = Mathf.Max(0f, h.good - HeartMath.InspirationSpends);
            h.releaseWhy = "CC_Good_Release_Inspired".Translate((def.label ?? def.defName).Named("WHAT"), p.Named("PAWN")).Resolve();
            string line = Flavor.FromPool(InspiredPool, p, null, null, "insp:" + def.defName);
            if (!line.NullOrEmpty()) Speech.Say(p, line);
            HeartView.Refresh(p, h);
            return true;
        }

        // ---------------- A kind word for someone close ----------------

        /// <summary>
        /// Someone close to them on the map, up and about and reachable: a partner, kin or a friend (their opinion 20 or
        /// more). The one under the most strain first, then the lowest in spirits, then any.
        /// </summary>
        private static Pawn Close(Pawn p)
        {
            if (p.Map == null || p.relations == null) return null;
            Pawn best = null;
            float bestScore = float.MinValue;
            foreach (var o in p.Map.mapPawns.FreeColonistsSpawned)
            {
                if (o == p || !Together.Able(o) || !Heart.Eligible(o) || o.DevelopmentalStage.Baby()) continue;
                bool partner = LovePartnerRelationUtility.LovePartnerRelationExists(p, o);
                bool kin = p.GetRelations(o).Any(r => r != null && r.familyByBloodRelation);
                if (!partner && !kin && p.relations.OpinionOf(o) < 20) continue;
                if (!p.CanReach(o, Verse.AI.PathEndMode.Touch, Danger.Some)) continue;
                float score = Heart.StrainOf(o) + (1f - (o.needs?.mood?.CurLevelPercentage ?? 0.5f)) * 30f + Rand.Range(0f, 10f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = o;
                }
            }
            return best;
        }

        /// <summary>
        /// They set out to say a kind word: when the two are together (one walks over in their free time, Together.When),
        /// the line is said to the other's face and the other gets its memory; the good side spends a little then. True when
        /// it was set going (it may still not come, if the two never meet within the day).
        /// </summary>
        private static bool KindWord(Pawn p, Pawn other, int now)
        {
            if (KindWordDef == null) return false;
            var tags = OtherTags(p, other);
            string key = "goodheart-kind|" + p.thingIDNumber + "|" + other.thingIDNumber;
            Together.When(key, p, other, () =>
            {
                if (p.Dead || other.Dead || !Heart.Eligible(other)) return;
                string line = Flavor.FromPool(KindPool, p, null, null, tags.ToArray());
                if (!line.NullOrEmpty()) Speech.Say(p, line, other);
                var memories = other.needs?.mood?.thoughts?.memories;
                if (S.MoodEffects && memories != null && ThoughtMaker.MakeThought(KindWordDef) is Thought_Memory t)
                {
                    // Who came over to say it, in its words and the small joys' row.
                    if (t is Thought_CouncilSocial social) social.cause = "CC_Good_KindWordCause".Translate(p.Named("PAWN")).Resolve();
                    memories.TryGainMemory(t, p);
                }
                var h = Heart.Of(p);
                if (h == null) return;
                h.good = Mathf.Max(0f, h.good - HeartMath.KindWordSpends);
                h.releaseWhy = "CC_Good_Release_KindWord".Translate(other.Named("OTHER"), p.Named("PAWN")).Resolve();
                HeartView.Refresh(p, h);
            }, 10f, 0f);
            return true;
        }

        /// <summary>What the line may ask of the one who hears it: under strain, grieving, low, a partner, kin, a friend, a child, new here.</summary>
        private static List<string> OtherTags(Pawn p, Pawn o)
        {
            var tags = new List<string>();
            if (Heart.BandOf(o) >= StrainBand.Strained) tags.Add("them:strained");
            if (Flavor.IsGrieving(o)) tags.Add("them:grieving");
            if ((o.needs?.mood?.CurLevelPercentage ?? 1f) < 0.35f) tags.Add("them:low");
            if ((o.health?.summaryHealth?.SummaryHealthPercent ?? 1f) < 0.85f) tags.Add("them:hurt");
            if (LovePartnerRelationUtility.LovePartnerRelationExists(p, o)) tags.Add("them:partner");
            else if (p.GetRelations(o).Any(r => r != null && r.familyByBloodRelation)) tags.Add("them:kin");
            else tags.Add("them:friend");
            if (o.DevelopmentalStage.Child()) tags.Add("them:child");
            if ((o.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 999999) < GenDate.TicksPerDay * 10) tags.Add("them:newcomer");
            if (o.CurJob?.workGiverDef?.workType != null) tags.Add("them:working");
            return tags;
        }
    }
}
