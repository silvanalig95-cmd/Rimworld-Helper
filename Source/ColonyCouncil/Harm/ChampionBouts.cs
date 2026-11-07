using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The champion's bout that can hurt (the harm review's 2.2.1): a travelling champion's challenge at fighting is a bout
    /// on the map now, by the outsiders' sparring rule (Outsiders/OutsiderOutcomes.cs: eight swings each, or until one
    /// yields to the pain), at a ring beside the colony's champion with the colony's people called round to watch. One bout
    /// in 25 lands a real blow (a blunt 6 to 9, the tough taking less by the game's own damage factor), decided at the
    /// start under the harm service's scale and cap for both; the spar counts it for the cap. Whoever the bout favours wins
    /// (a draw is settled by skill, as before), and the challenger has a word for a real blow: an apology, or a sneer from
    /// a hot temper. The form is the challenger's people's (their ways): wrestling and sticks, staves or blunted swords,
    /// boxing, a stun-baton exhibition. Shooting at targets, crafts, cooking, art and debate stay as they were. Without
    /// the outsiders' sparring or with hurts at nothing, the bout is decided as before, on the spot and unhurt.
    /// </summary>
    public static class ChampionBouts
    {
        /// <summary>With hurts from everyday life on and the outsiders' practice bouts allowed (their setting).</summary>
        public static bool On => Harm.On && Outsiders.SparOn;

        /// <summary>A challenge that is a bout: fighting (Melee).</summary>
        public static bool IsBout(SkillDef s) => s != null && s == SkillDefOf.Melee;

        /// <summary>The bout's name in the challenger's people's ways.</summary>
        public static string FormWord(Faction people) => ("CC_HarmTwo_BoutForm_" + HarmTwoMath.BoutForm(Ages.Of(people))).Translate().Resolve();

        /// <summary>Dev mode (Harm/HarmTwoDebug.cs): the next champion's bout lands its real blow, when the cap lets both take one.</summary>
        internal static bool DebugHard;

        /// <summary>A bout under way for this visit (the visit waits for it).</summary>
        public static bool Live(Visit v) => v?.bout != null && Outsiders.Known(v.bout) && !v.bout.over;

        private static readonly Color SayColor = new Color(0.95f, 0.85f, 0.7f);

        /// <summary>
        /// The two are together for the contest: a fighting challenge begins as a bout at a ring beside our champion, its one
        /// real blow decided now. False when it isn't one, or can't begin (the contest is decided on the spot, as before).
        /// </summary>
        public static bool Begin(Visit v, CouncilGameComponent comp)
        {
            if (!On || v == null || !IsBout(v.skill)) return false;
            var a = v.a;
            var x = v.x;
            if (a == null || x == null || !a.Spawned || !x.Spawned || a.Map != x.Map) return false;
            try
            {
                bool hard = Harm.May(a, HurtClass.Bout) && Harm.May(x, HurtClass.Bout) && (DebugHard || Harm.Roll(HarmTwoMath.ChampionBlowChance));
                DebugHard = false;
                var bout = Outsiders.ChampionBout(a, x, hard);
                if (bout == null) return false;
                v.bout = bout;
                bout.after = b => Over(v, comp, b);
                // together: the champion and the challenger face each other at the ring.
                Say(x, "Rimfolk_Pool_ChampionBegin", a, "form:" + HarmTwoMath.BoutForm(Ages.Of(x.Faction)).ToLowerInvariant());
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: a champion's bout", e);
                return false;
            }
        }

        /// <summary>The bout is over: its winner the contest's (a draw by skill, as before), the real blow's word, and the contest's letter.</summary>
        private static void Over(Visit v, CouncilGameComponent comp, OutsiderBout b)
        {
            if (v == null || v.done) return;
            v.bout = null;
            var a = v.a;
            var x = v.x;
            if (b == null || b.off || a == null || x == null || a.Dead || x.Dead)
            {
                v.done = true;
                // A bout that never came to anything has said so already (Outsiders.SparOver).
                if (b == null || !b.off) Tell.Minor(NewsKind.World, null, v.map, "CC_Evt_ContestOff".Translate().Resolve(), null, MessageTypeDefOf.NeutralEvent);
                if (x != null && !x.Dead) Arrivals.SendAway(new[] { x });
                return;
            }
            bool won = b.winner == a || b.winner != x && HappeningWorker_Champion.SkillWins(a, x, v.skill);
            if (b.hurt != null) BlowWord(b, a, x);
            HappeningWorker_Champion.Decide(v, comp, won, b);
        }

        /// <summary>The challenger's word for the real blow: sorry, or a sneer from a hot temper, when it fell on ours; a fair blow granted when on them.</summary>
        private static void BlowWord(OutsiderBout b, Pawn a, Pawn x)
        {
            string pool = b.hurt == x ? "Rimfolk_Pool_ChampionTook" : Outsiders.Temper(x) > 0 ? "Rimfolk_Pool_ChampionSneer" : "Rimfolk_Pool_ChampionSorry";
            // together: the two are still at the ring when the bout ends.
            Say(x, pool, a, null);
        }

        private static void Say(Pawn p, string pool, Pawn to, string tag)
        {
            try
            {
                string line = Flavor.FromPool(pool, p, null, null, tag);
                if (line.NullOrEmpty()) return;
                // together: a word to the other at the ring, shown only while the two are together.
                Speech.Say(p, line.Formatted(p.Named("PAWN"), to.Named("OTHER")).Resolve(), to, SayColor);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: a champion's word", e);
            }
        }
    }
}
