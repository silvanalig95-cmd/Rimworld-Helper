using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    public enum TrustLevel
    {
        Bitter,
        Doubtful,
        Neutral,
        Trusting,
        Devoted
    }

    /// <summary>
    /// How much one colonist trusts the council (you) to listen. Grows when wishes come true and promises
    /// are kept; shrinks when wishes are ignored, refused or promises broken. Slowly drifts back towards
    /// neutral, because people forget, eventually.
    /// </summary>
    public class PawnStanding : IExposable
    {
        public Pawn pawn;
        public float trust;
        public int granted;
        public int ignored;
        public int refused;
        public int promisesKept;
        public int promisesBroken;
        public int selfHelped;
        public List<int> recentGrants = new List<int>();
        public int lastTriggerTick = -999999;
        public int lastSulkTick = -999999;
        /// <summary>When they last turned bitter (0 = not bitter), and when they'll walk out (0 = staying).</summary>
        public int bitterSince;
        public int leavingTick;
        /// <summary>0..1: how hard they are to please (see <see cref="Standards"/>).</summary>
        public float standards;
        /// <summary>Years in the colony already marked (-1 = not counted yet).</summary>
        public int yearsMarked = -1;
        /// <summary>Wishes this colonist has made before (defNames), for "I've asked before" lines.</summary>
        public List<string> asked = new List<string>();
        /// <summary>Short history for the tooltip, newest last.</summary>
        public List<string> log = new List<string>();
        /// <summary>
        /// Older saves only: when the council gave them a room of their own, for the old "room taken" mood. Read once and
        /// turned into a watched wish (see <see cref="KeptTracker"/>), then cleared.
        /// </summary>
        public int ownRoomGranted;
        public int ownRoomLostHours;
        public bool ownRoomLossNoted;

        public TrustLevel Level => LevelFor(trust);

        public static TrustLevel LevelFor(float trust)
        {
            if (trust <= -45f) return TrustLevel.Bitter;
            if (trust <= -15f) return TrustLevel.Doubtful;
            if (trust < 25f) return TrustLevel.Neutral;
            if (trust < 60f) return TrustLevel.Trusting;
            return TrustLevel.Devoted;
        }

        public int RecentGrants(float days = 15f)
        {
            int now = Find.TickManager.TicksGame;
            return recentGrants.Count(t => now - t < days * GenDate.TicksPerDay);
        }

        /// <summary>
        /// Spoiled colonists expect more: every wish granted lately makes the next disappointment sting harder.
        /// 1 = normal, up to 2.
        /// </summary>
        public float Entitlement => Mathf.Min(2f, 1f + 0.2f * RecentGrants());

        /// <summary>The latest few things that moved their trust, newest first, for the trust mood's tooltip.</summary>
        public string RecentReasons(int n = 3) => log.Count == 0 ? null : string.Join("\n", Enumerable.Reverse(log).Take(n));

        public void Change(float delta, string why)
        {
            if (Mathf.Abs(delta) < 0.01f) return;
            trust = Mathf.Clamp(trust + delta, -100f, 100f);
            if (!why.NullOrEmpty())
            {
                log.Add((delta > 0 ? "+" : "") + delta.ToString("0") + "  " + why);
                if (log.Count > 8) log.RemoveAt(0);
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref trust, "trust");
            Scribe_Values.Look(ref granted, "granted");
            Scribe_Values.Look(ref ignored, "ignored");
            Scribe_Values.Look(ref refused, "refused");
            Scribe_Values.Look(ref promisesKept, "promisesKept");
            Scribe_Values.Look(ref promisesBroken, "promisesBroken");
            Scribe_Values.Look(ref selfHelped, "selfHelped");
            Scribe_Collections.Look(ref recentGrants, "recentGrants", LookMode.Value);
            Scribe_Values.Look(ref lastTriggerTick, "lastTriggerTick", -999999);
            Scribe_Values.Look(ref lastSulkTick, "lastSulkTick", -999999);
            Scribe_Values.Look(ref bitterSince, "bitterSince");
            Scribe_Values.Look(ref leavingTick, "leavingTick");
            Scribe_Values.Look(ref standards, "standards");
            Scribe_Values.Look(ref yearsMarked, "yearsMarked", -1);
            Scribe_Collections.Look(ref log, "log", LookMode.Value);
            Scribe_Collections.Look(ref asked, "asked", LookMode.Value);
            Scribe_Values.Look(ref ownRoomGranted, "ownRoomGranted");
            Scribe_Values.Look(ref ownRoomLostHours, "ownRoomLostHours");
            Scribe_Values.Look(ref ownRoomLossNoted, "ownRoomLossNoted");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (asked == null) asked = new List<string>();
                if (recentGrants == null) recentGrants = new List<int>();
                if (log == null) log = new List<string>();
            }
        }
    }

    public class StandingTracker : IExposable
    {
        public List<PawnStanding> standings = new List<PawnStanding>();

        public PawnStanding For(Pawn p, bool create = true)
        {
            if (p == null) return null;
            for (int i = 0; i < standings.Count; i++)
            {
                if (standings[i].pawn == p) return standings[i];
            }
            if (!create) return null;
            var s = new PawnStanding { pawn = p };
            // First impressions: a colony that already shows it cares starts a little warmer.
            standings.Add(s);
            return s;
        }

        public float TrustOf(Pawn p) => For(p, false)?.trust ?? 0f;

        public TrustLevel LevelOf(Pawn p) => PawnStanding.LevelFor(TrustOf(p));

        public void Change(Pawn p, float delta, string why)
        {
            if (p == null || !p.IsColonist) return;
            var st = For(p);
            st.Change(delta, why);
            TrustEffects.Sync(p, st);
        }

        /// <summary>Average trust of the free colonists, for the council tab and meetings.</summary>
        public float ColonyTrust()
        {
            var pawns = PawnsFinder.AllMaps_FreeColonists.Where(p => !p.IsQuestLodger()).ToList();
            if (pawns.Count == 0) return 0f;
            return pawns.Average(TrustOf);
        }

        /// <summary>Once a day: memories fade towards neutral; the gone are forgotten.</summary>
        public void Daily()
        {
            int now = Find.TickManager.TicksGame;
            foreach (var gone in standings.Where(s => s.pawn != null && !s.pawn.Dead && !s.pawn.Destroyed && !s.pawn.IsColonist && !s.pawn.IsPrisonerOfColony).ToList())
                TrustEffects.StripAll(gone.pawn);
            standings.RemoveAll(s => s.pawn == null || s.pawn.Dead || s.pawn.Destroyed || (!s.pawn.IsColonist && !s.pawn.IsPrisonerOfColony));
            foreach (var s in standings)
            {
                s.trust *= 0.985f;
                s.recentGrants.RemoveAll(t => now - t > GenDate.TicksPerDay * 30);
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref standings, "standings", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (standings == null) standings = new List<PawnStanding>();
                standings.RemoveAll(s => s == null || s.pawn == null);
            }
        }
    }

    /// <summary>
    /// The working side of trust, which matters even when everyone is already happy: loyal colonists work and
    /// learn a little faster and hold together under stress; those with a grudge drag their feet and snap sooner.
    /// Shown in the health tab.
    /// </summary>
    public static class TrustEffects
    {
        /// <summary>Takes every council condition off someone leaving the colony: trust and decree effects.</summary>
        public static void StripAll(Pawn p)
        {
            var set = p?.health?.hediffSet;
            if (set == null) return;
            var ours = new HashSet<HediffDef> { CouncilDefOf.Council_Loyalty, CouncilDefOf.Council_Grudge };
            foreach (var d in DefDatabase<DecreeDef>.AllDefsListForReading)
                if (d.hediff != null) ours.Add(d.hediff);
            foreach (var h in set.hediffs.Where(h => ours.Contains(h.def)).ToList()) p.health.RemoveHediff(h);
        }

        public static void Sync(Pawn p, PawnStanding st)
        {
            if (p?.health?.hediffSet == null || p.Dead) return;
            var s = ColonyCouncilMod.Settings;
            HediffDef want = null;
            float severity = 0f;
            if (s.enableWishes && s.MoodEffects && st != null && p.IsFreeNonSlaveColonist)
            {
                switch (st.Level)
                {
                    case TrustLevel.Devoted: want = CouncilDefOf.Council_Loyalty; severity = 1f; break;
                    case TrustLevel.Trusting: want = CouncilDefOf.Council_Loyalty; severity = 0.5f; break;
                    case TrustLevel.Doubtful: if (s.Negatives) { want = CouncilDefOf.Council_Grudge; severity = 0.5f; } break;
                    case TrustLevel.Bitter: if (s.Negatives) { want = CouncilDefOf.Council_Grudge; severity = 1f; } break;
                }
            }
            foreach (var def in new[] { CouncilDefOf.Council_Loyalty, CouncilDefOf.Council_Grudge })
            {
                if (def == null) continue;
                var h = p.health.hediffSet.GetFirstHediffOfDef(def);
                if (def != want)
                {
                    if (h != null) p.health.RemoveHediff(h);
                    continue;
                }
                if (h == null)
                {
                    h = HediffMaker.MakeHediff(def, p);
                    h.Severity = severity;
                    p.health.AddHediff(h);
                }
                else if (Mathf.Abs(h.Severity - severity) > 0.01f) h.Severity = severity;
            }
        }
    }

    /// <summary>The mood side of trust: devoted colonists feel heard, bitter ones feel ignored.</summary>
    public class ThoughtWorker_CouncilTrust : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var s = ColonyCouncilMod.Settings;
            if (!s.enableWishes || !s.MoodEffects) return ThoughtState.Inactive;
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !p.IsFreeNonSlaveColonist) return ThoughtState.Inactive;
            var standing = comp.standings.For(p, false);
            if (standing == null) return ThoughtState.Inactive;
            switch (standing.Level)
            {
                case TrustLevel.Bitter: return s.Negatives ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
                case TrustLevel.Doubtful: return s.Negatives ? ThoughtState.ActiveAtStage(1) : ThoughtState.Inactive;
                case TrustLevel.Trusting: return ThoughtState.ActiveAtStage(2);
                case TrustLevel.Devoted: return ThoughtState.ActiveAtStage(3);
                default: return ThoughtState.Inactive;
            }
        }
    }

    /// <summary>The trust mood, with what moved it lately.</summary>
    public class Thought_CouncilTrust : Thought_Situational, IExplainedThought
    {
        /// <summary>
        /// Feeling heard is one of the council's rewards, so its good side follows the reward strength and shrinks for
        /// the spoiled, who take being listened to for granted. Grudges count as they are.
        /// </summary>
        public override float MoodOffset()
        {
            float m = base.MoodOffset();
            return m > 0f ? m * SmallJoys.LiveScale(pawn) : m;
        }

        public string Why
        {
            get
            {
                var reasons = CouncilGameComponent.Instance?.standings.For(pawn, false)?.RecentReasons();
                return reasons.NullOrEmpty() ? null : "CC_Cause_Trust".Translate(reasons).Resolve();
            }
        }

        public override string Description => Causes.Append(base.Description, Why);
    }
}
