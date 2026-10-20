using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A wavering colonist's wish (Ideology, "Faith in the colony" on): someone whose faith is shaken (Faiths.Wavering)
    /// asks to spend an evening with a devout believer of another faith they think well of (opinion 20 or more), learning
    /// about it. Promised, the evening is arranged and plays out on the map: the one who asked walks over to the believer
    /// in the evening and keeps them company for a couple of hours (a play-out visit; with answers not playing out on the
    /// map, the next time the two are together). It also comes true if they go to an evening of that faith
    /// (Faith/FaithEvenings.cs). Granted after the evening with the believer, it sways them toward that faith
    /// (FaithCouncilMath.WishSway, through Faiths.Sway) and the two share the memory; ignored, it lapses at a wish's usual
    /// cost. Never for someone whose own faith frowns on being preached to.
    /// <para>The wish keeps the faith's id in subjectKey and its name in subjectLabel, the visit's play-out in targetValue,
    /// and how it came true in baseline (1 the evening with the believer, 2 an evening of faith).</para>
    /// </summary>
    public class WishWorker_LearnFaith : WishWorker
    {
        private static readonly Dictionary<int, int> lastArranged = new Dictionary<int, int>();

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            if (!Faiths.On || !Faiths.Wavering(pawn) || pawn.DevelopmentalStage.Juvenile() || pawn.relations == null || Faiths.DislikesPreaching(pawn)) yield break;
            var map = HomeMap(pawn);
            if (map == null) yield break;
            Pawn best = null;
            float bestWeight = 0f;
            float certainty = Faiths.Certainty(pawn);
            foreach (var o in map.mapPawns.FreeColonistsSpawned)
            {
                if (o == pawn || o.IsQuestLodger() || o.DevelopmentalStage.Juvenile() || !Faiths.Devout(o) || Faiths.Same(pawn, o)) continue;
                float w = FaithCouncilMath.WishWeight(certainty, pawn.relations.OpinionOf(o));
                if (w > bestWeight)
                {
                    bestWeight = w;
                    best = o;
                }
            }
            var faith = Faiths.Of(best);
            if (best == null || faith == null) yield break;
            var c = Candidate(bestWeight, "CC_Reason_LearnFaith".Translate(pawn.Named("PAWN"), best.Named("OTHER"), faith.name.Named("FAITH")).Resolve());
            c.other = best;
            c.subjectKey = faith.id.ToString();
            c.subjectLabel = faith.name;
            yield return c;
        }

        /// <summary>The faith the wish is about (by the game's id), else the believer's.</summary>
        public static Ideo FaithOf(Wish w)
        {
            if (w == null) return null;
            if (Find.IdeoManager != null && int.TryParse(w.subjectKey, out int id))
            {
                var f = Find.IdeoManager.IdeosListForReading.FirstOrDefault(i => i != null && i.id == id);
                if (f != null) return f;
            }
            return Faiths.Of(w.other);
        }

        public override bool StillValid(Wish w)
        {
            var p = w.Beneficiary;
            var o = w.other;
            var faith = FaithOf(w);
            return Faiths.On && faith != null && o != null && !o.Dead && o.IsColonist && Faiths.Of(o) == faith && Faiths.Holds(p) && Faiths.Of(p) != faith;
        }

        public override bool IsFulfilled(Wish w)
        {
            if (w.baseline > 0f) return true;
            var p = w.Beneficiary;
            var faith = FaithOf(w);
            if (p == null || faith == null) return false;
            // The evening with the believer happened (the play-out visit took).
            int id = (int)w.targetValue;
            var plays = PlayOutTracker.Instance;
            if (id > 0 && plays != null)
            {
                var res = plays.ResultOf(id);
                if (res != null && res.took)
                {
                    w.baseline = 1f;
                    return true;
                }
                // It fell through (danger, the believer busy all evening): promised, it's arranged again for another evening.
                if (w.promised && plays.Get(id) == null && (res == null || !res.took)) Arrange(w, false);
            }
            // Or they went to hear the faith at an evening of faith.
            if (FaithCouncilTracker.Instance?.HeardSince(p, faith, w.createdTick) ?? false)
            {
                w.baseline = 2f;
                return true;
            }
            return false;
        }

        public override LookTargets LookAt(Wish w) => w.other != null && w.other.Spawned ? new LookTargets(new[] { w.Beneficiary, w.other }) : base.LookAt(w);

        /// <summary>Promised: the evening is arranged (a visit this evening), or, with answers not playing out on the map, the next time the two are together.</summary>
        public override string OnPromised(Wish w) => Arrange(w, true);

        private static string Arrange(Wish w, bool promising)
        {
            var p = w?.Beneficiary;
            var o = w?.other;
            var faith = FaithOf(w);
            var map = p?.MapHeld;
            if (!Faiths.On || p == null || o == null || faith == null || map == null || o.MapHeld != map) return null;
            int now = Find.TickManager.TicksGame;
            if (!promising && lastArranged.TryGetValue(w.id, out int last) && now - last < GenDate.TicksPerDay / 2) return null;
            lastArranged[w.id] = now;
            if (lastArranged.Count > 100) lastArranged.Clear();
            try
            {
                if (!PlayOuts.On)
                {
                    // together: it happens when the two are together, or once one walks over to find the other.
                    Together.When("learnfaith|" + w.id, p, o, () =>
                    {
                        if (w.state == WishState.Open) w.baseline = 1f;
                    }, 48f, 4f);
                    return "CC_Promise_LearnFaithTogether";
                }
                var plays = PlayOutTracker.Instance;
                if (plays == null) return null;
                // A visit (PlayOut/): they walk over to the believer in the evening and keep them company for a couple of hours.
                var play = new PlayOut
                {
                    kind = PlayKind.Visit,
                    map = map,
                    created = now,
                    label = "CC_LearnFaith_Label".Translate(faith.name.Named("FAITH")).Resolve(),
                    visitor = p,
                    other = o,
                    to = PlaySpot.Person,
                    hourFrom = FaithEvenings.HourFrom,
                    hourTo = FaithEvenings.HourTo,
                    durationTicks = PlayOutMath.DurationTicks(2f)
                };
                play.deadline = PlayOutMath.Deadline(now, GenLocalDate.HourOfDay(map), play.hourFrom, play.hourTo, PlayOutMath.DefaultWaitHours);
                plays.Add(play);
                w.targetValue = play.id;
                return promising ? "CC_Promise_LearnFaith" : null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("learn faith wish", e);
                return null;
            }
        }
    }

    /// <summary>
    /// The wish to learn another faith came true: after an evening with the believer, the one who asked is swayed toward
    /// that faith (a little more than an evening of faith does, FaithCouncilMath.WishSway); having heard it at an evening
    /// of faith, the evening already did that. Either way the two remember it together.
    /// </summary>
    public class WishEffect_LearnFaith : WishEffect
    {
        protected override void Apply(Wish w, Pawn p, float scale)
        {
            var o = w.other;
            var faith = WishWorker_LearnFaith.FaithOf(w);
            if (!Faiths.On || o == null || o.Dead || faith == null || p != w.Beneficiary) return;
            // together: the wish is granted by the evening the two spent together (its visit), or the evening of faith they both came to.
            if (w.baseline < 1.5f)
            {
                var r = Faiths.Sway(p, o, FaithCouncilMath.WishSway, "CC_LearnFaith_Cause".Translate().Resolve());
                if (r == Faiths.SwayResult.Moved)
                    News.Post("CC_LearnFaith_Moved".Translate(p.Named("PAWN"), o.Named("OTHER"), faith.name.Named("FAITH")).Resolve(), new LookTargets(p, o),
                        NewsKind.People, MessageTypeDefOf.NeutralEvent, Importance.Notable);
            }
            SharedMemories.Add(o, p, FaithCouncilDefs.LearnedMemory, "CC_LearnFaith_MemLabel".Translate(faith.name.Named("FAITH")).Resolve(), 1f);
        }
    }
}
