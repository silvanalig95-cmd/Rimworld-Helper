using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Setbacks from the council's decisions and the colony's gatherings, called from the council's own code where the
    /// decision is made: an idea turned down, a vote lost, a promise to a friend broken, a rival honoured, an answer that
    /// went against someone's nature, a gathering missed or a feast that ran out.
    /// </summary>
    public static partial class Setbacks
    {
        // ---------------- Council meetings ----------------

        /// <summary>
        /// The council chose one of the meeting's ideas: those who proposed another (and were there to argue for it) mind,
        /// more if their idea had the clear majority; those who voted for another mind a little.
        /// </summary>
        public static void Notify_AmbitionChosen(ChoiceLetter_CouncilMeeting letter, AmbitionDef chosen, Pawn chosenProposer)
        {
            try
            {
                if (letter?.options == null || chosen == null || !On(SetbackGroup.Council)) return;
                int pick = letter.options.IndexOf(chosen);
                if (pick < 0) return;
                var counts = letter.options.Select((o, k) => letter.voterChoice.Count(c => c == k)).ToList();
                int max = counts.Count > 0 ? counts.Max() : 0;
                bool clear = max >= 2 && counts.Count(c => c == max) == 1;
                var proposers = new HashSet<Pawn>();
                for (int i = 0; i < letter.options.Count && i < letter.proposers.Count; i++)
                {
                    var p = letter.proposers[i];
                    if (p == null) continue;
                    proposers.Add(p);
                    if (i == pick || p == chosenProposer || (letter.absentProposers?.Contains(p) ?? false)) continue;
                    int stage = clear && counts[i] == max ? 1 : 0;
                    string cause = "CC_Setback_Cause_IdeaPassed".Translate(letter.options[i].label, chosen.label).Resolve();
                    if (!Give(p, SetbackDefOf.Council_Setback_IdeaPassedOver, null, cause, stage)) continue;
                    Say(p, "Council_Pool_Setback_IdeaPassed", letter.options[i].label.Named("MINE"), chosen.label.Named("AMBITION"));
                    Write(p, "Council_Journal_Setback_IdeaPassed", 0.6f, letter.options[i].label.Named("MINE"), chosen.label.Named("AMBITION"));
                }
                for (int k = 0; k < letter.voterPawns.Count && k < letter.voterChoice.Count; k++)
                {
                    var v = letter.voterPawns[k];
                    int c = letter.voterChoice[k];
                    if (v == null || c == pick || c < 0 || c >= letter.options.Count || proposers.Contains(v)) continue;
                    Give(v, SetbackDefOf.Council_Setback_OutVoted, null, "CC_Setback_Cause_OutVoted".Translate(letter.options[c].label, chosen.label).Resolve());
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setbacks: meeting decided", e);
            }
        }

        // ---------------- Wishes ----------------

        /// <summary>A promised wish lapsed: those close to whoever it was for take it hard too (they get no word of their own).</summary>
        public static void Notify_PromiseBroken(Wish w)
        {
            try
            {
                if (w?.pawn == null || !On(SetbackGroup.Council)) return;
                var supporters = new HashSet<Pawn>(w.Supporters);
                var who = w.Beneficiary ?? w.pawn;
                string cause = "CC_Setback_Cause_FriendLetDown".Translate(who.LabelShort, w.Label).Resolve();
                foreach (var p in Everyone())
                {
                    if (supporters.Contains(p) || p == who) continue;
                    int close = Closeness(p, who);
                    if (close <= 0) continue;
                    if (!Give(p, SetbackDefOf.Council_Setback_FriendLetDown, who, cause, close >= 2 ? 1 : 0)) continue;
                    Write(p, "Council_Journal_Setback_FriendLetDown", 0.5f, who.Named("OTHER"), w.Label.Named("WISH"));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setbacks: promise broken", e);
            }
        }

        // ---------------- Honours ----------------

        /// <summary>Someone was given a name (not a dubious one): their rivals mind.</summary>
        public static void Notify_Named(Pawn p, EpithetDef def)
        {
            if (p == null || def == null || def.dubious) return;
            Honoured(p, null, "CC_Setback_Cause_RivalNamed".Translate(p.LabelShort, def.label).Resolve());
        }

        /// <summary>Someone took a seat on the council: their rivals mind (those who ran against them have their own mood).</summary>
        public static void Notify_OfficeWon(Pawn p, OfficeDef office, IEnumerable<Pawn> ran)
        {
            if (p == null || office == null) return;
            Honoured(p, ran != null ? new HashSet<Pawn>(ran) : null, "CC_Setback_Cause_RivalSeat".Translate(p.LabelShort, office.label).Resolve());
        }

        private static void Honoured(Pawn p, HashSet<Pawn> skip, string cause)
        {
            try
            {
                if (!On(SetbackDefOf.Council_Setback_RivalHonoured)) return;
                foreach (var r in Everyone())
                {
                    if (r == p || (skip != null && skip.Contains(r)) || !Rival(r, p) || r.story?.traits?.HasTrait(TraitDefOf.Kind) == true) continue;
                    if (!(Tracker?.Ready(r, "honoured|" + p.ThingID, 4f) ?? false)) continue;
                    Give(r, SetbackDefOf.Council_Setback_RivalHonoured, p, cause);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setbacks: rival honoured", e);
            }
        }

        // ---------------- Answers ----------------

        /// <summary>
        /// The council answered a dilemma or an event: those whose nature the answer goes against mind it ("not everyone
        /// agreed"), more for a strong clash; when its gamble went wrong, those it went against even a little say they
        /// said so. Not those the answer was about (it has moods of its own for them), and not anyone it already upset.
        /// </summary>
        public static void Notify_Answer(DilemmaContext ctx, WayShift ways, bool byDefault, bool risk, string question, string answer)
        {
            try
            {
                if (ctx == null || ways == null || !ways.Any || !On(SetbackGroup.Council)) return;
                var shift = byDefault ? ways.Scaled(0.5f) : ways;
                var all = CharacterTracker.All;
                var s = new float[all.Length];
                for (int i = 0; i < all.Length; i++) s[i] = shift.Get(all[i]);
                var map = ctx.map ?? ctx.a?.MapHeld ?? HappeningTracker.HomeMap();
                if (map == null) return;
                var a = new float[all.Length];
                foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
                {
                    if (p == ctx.a || p == ctx.b || p == ctx.stranger || !Eligible(p) || p.DevelopmentalStage.Juvenile() || UpsetJustNow(p)) continue;
                    // Their nature, and for an old hand a little of the colony's own name (Interplay/).
                    for (int i = 0; i < all.Length; i++) a[i] = Interplay.WayAffinity(p, all[i], CharacterTracker.Affinity(p, all[i]));
                    float clash = SetbackMath.Clash(s, a);
                    if (SetbackMath.ToldSo(clash, risk))
                    {
                        Give(p, SetbackDefOf.Council_Setback_ToldThemSo, null, "CC_Setback_Cause_ToldSo".Translate(question ?? "?", answer ?? "?").Resolve());
                        continue;
                    }
                    int stage = SetbackMath.ClashStage(clash);
                    if (stage < 0) continue;
                    Give(p, SetbackDefOf.Council_Setback_NotAgreed, null, "CC_Setback_Cause_NotAgreed".Translate(question ?? "?", answer ?? "?", Against(s, a)).Resolve(), stage);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setbacks: answer", e);
            }
        }

        /// <summary>The way the answer went that they mind most ("harsh", "thrifty").</summary>
        private static string Against(float[] shift, float[] affinity)
        {
            var all = CharacterTracker.All;
            int worst = -1;
            float most = 0f;
            for (int i = 0; i < all.Length; i++)
            {
                float c = -shift[i] * affinity[i];
                if (c > most)
                {
                    most = c;
                    worst = i;
                }
            }
            return worst < 0 ? "?" : CharacterTracker.PoleLabel(all[worst], shift[worst] > 0f);
        }

        /// <summary>A bad memory given this very tick (the answer's own effects already upset them).</summary>
        private static bool UpsetJustNow(Pawn p)
        {
            var memories = p.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null) return false;
            for (int i = 0; i < memories.Count; i++)
            {
                var m = memories[i];
                if (m == null || m.age > 0) continue;
                var stages = m.def?.stages;
                int idx = m.CurStageIndex;
                if (stages != null && idx >= 0 && idx < stages.Count && stages[idx] != null && stages[idx].baseMoodEffect < 0f) return true;
            }
            return false;
        }

        // ---------------- Gatherings ----------------

        /// <summary>
        /// A gathering an answer called is over: those it was meant for who missed it (away, ill, or busy) mind a little;
        /// at a feast that ran out of meals, those who came and got no plate mind more. Nothing for one called off early.
        /// </summary>
        public static void Notify_GatheringOver(PlayOut play, List<Pawn> attended, List<Pawn> fed, bool feast, bool early, Map map, IntVec3 spot)
        {
            try
            {
                if (play == null || early || attended.NullOrEmpty() || map == null || !On(SetbackGroup.Life)) return;
                string label = (play.label ?? "").NullOrEmpty() ? "CC_Setback_TheGathering".Translate().ToString() : play.label;
                var meant = new List<Pawn>(play.invited);
                if (play.organizer != null && !meant.Contains(play.organizer)) meant.Add(play.organizer);
                foreach (var p in meant)
                {
                    if (p == null || attended.Contains(p) || !Eligible(p) || Interplay.Excused(p) || !(Tracker?.Ready(p, "missed", 1f) ?? false)) continue;
                    string why = p.MapHeld != map ? "CC_Setback_Cause_MissedAway" : p.Downed || PlayOuts.MedicalRest(p) ? "CC_Setback_Cause_MissedSick" : "CC_Setback_Cause_MissedBusy";
                    Give(p, SetbackDefOf.Council_Setback_MissedGathering, null, why.Translate(label).Resolve());
                }
                // Who ate is only known through the meal hook.
                if (!feast || fed == null || !RimfolkPatches.On) return;
                foreach (var p in attended)
                {
                    if (p == null || fed.Contains(p) || !p.Spawned || p.Map != map || !Eligible(p)) continue;
                    // A meal left for them, or none fit for them (their beliefs, their food policy): not the council's lapse.
                    if (PlaySpots.BestMeal(p, map, spot, out bool noneFit) != null || noneFit) continue;
                    if (!Give(p, SetbackDefOf.Council_Setback_NoPlate, null, "CC_Setback_Cause_NoPlate".Translate(label).Resolve())) continue;
                    if (Rand.Chance(0.5f)) Say(p, "Council_Pool_Setback_NoPlate");
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setbacks: gathering over", e);
            }
        }
    }
}
