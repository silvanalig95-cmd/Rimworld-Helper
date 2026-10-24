using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A joke passed on (<see cref="JokesRetold"/>): an ordinary conversation in the game's own picker, which only comes up
    /// when the speaker has a good in-joke with someone else that the listener wasn't part of.
    /// </summary>
    public class InteractionWorker_JokeRetold : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            try
            {
                return JokesRetold.Weight(initiator, recipient);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("joke retold weight", e);
                return 0f;
            }
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
            try
            {
                JokesRetold.Told(initiator, recipient);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("joke retold", e);
            }
        }
    }

    /// <summary>
    /// In-jokes that pass to a third person. A joke two colonists have laughed over at least once, and not yet to death,
    /// can be told by one of them to a friend who wasn't there; from then on the teller and the friend share it too (a
    /// memory of its own, "told them about the stew", with its own lines). A joke passed on is never passed on again, so a
    /// story spreads one step from those who lived it, as such stories do. Cooldowns are not saved: a load only gives a
    /// joke a second chance.
    /// </summary>
    public static class JokesRetold
    {
        /// <summary>One colonist passes on a joke at most once a day, and to the same friend not more than once a week.</summary>
        public const int TellerGapTicks = GenDate.TicksPerDay, PairGapTicks = GenDate.TicksPerDay * 7;

        /// <summary>Only jokes this fresh are passed on.</summary>
        public const int FreshDays = 60;

        private static readonly Dictionary<int, int> lastTold = new Dictionary<int, int>();
        private static readonly Dictionary<long, int> pairTold = new Dictionary<long, int>();
        private static readonly Dictionary<long, (int tick, SharedMemory joke)> picks = new Dictionary<long, (int, SharedMemory)>();
        private static Game game;

        private static int Now => Find.TickManager.TicksGame;

        private static long Key(Pawn a, Pawn b) => ((long)a.thingIDNumber << 32) | (uint)b.thingIDNumber;

        private static void Fresh()
        {
            if (game == Current.Game) return;
            game = Current.Game;
            lastTold.Clear();
            pairTold.Clear();
            picks.Clear();
        }

        /// <summary>How likely this conversation is between the two right now (0: no joke to pass on, or one passed on lately).</summary>
        public static float Weight(Pawn teller, Pawn listener)
        {
            if (!Talk.On || teller == null || listener == null || teller == listener || !Talk.Eligible(teller) || !Talk.Eligible(listener)) return 0f;
            Fresh();
            int now = Now;
            if (lastTold.TryGetValue(teller.thingIDNumber, out int t) && t <= now && now - t < TellerGapTicks) return 0f;
            long key = Key(teller, listener);
            if (pairTold.TryGetValue(key, out int pt) && pt <= now && now - pt < PairGapTicks) return 0f;
            if (!SocialPace.Ready(teller, listener)) return 0f;
            if (!picks.TryGetValue(key, out var pick) || now - pick.tick >= Sched.RareTicks || now < pick.tick)
            {
                if (picks.Count > 600) picks.Clear();
                pick = (now, Choose(teller, listener));
                picks[key] = pick;
            }
            if (pick.joke == null) return 0f;
            float humour = Persona.Values(teller)[(int)PersonaAxis.Humour];
            float factor = humour >= 30f ? 1.6f : humour <= -30f ? 0.3f : 1f;
            return 0.04f * factor * (ColonyCouncilMod.Settings?.momentFrequency ?? 1f);
        }

        /// <summary>
        /// The joke the teller would pass on to this listener: one of the teller's own in-jokes with someone else (not one
        /// heard secondhand), told at least once and not worn out, from the last two months; the listener on good terms with
        /// the teller, not in it, and not already in on it.
        /// </summary>
        private static SharedMemory Choose(Pawn teller, Pawn listener)
        {
            var tr = PersonaTracker.Instance;
            var heard = TimesDefs.JokeHeard;
            if (tr == null || heard == null || (listener.relations?.OpinionOf(teller) ?? 0) < 10 || (teller.relations?.OpinionOf(listener) ?? 0) < 10) return null;
            int now = Now;
            int me = teller.thingIDNumber, them = listener.thingIDNumber;
            // What the listener is already in on, by its name.
            var known = new HashSet<string>();
            foreach (var m in tr.memories)
                if (m?.def != null && m.def.joke && m.Has(them) && !m.label.NullOrEmpty())
                    known.Add(m.label);
            SharedMemory best = null;
            float bestW = 0f;
            foreach (var m in tr.memories)
            {
                if (m?.def == null || !m.def.joke || m.def == heard || !m.Has(me) || m.Has(them) || m.label.NullOrEmpty()) continue;
                if (m.recalls < 1 || TalkMath.Worn(m.recalls) || now - m.tick > FreshDays * GenDate.TicksPerDay || known.Contains(m.label)) continue;
                float w = m.WeightNow(now);
                if (w > bestW)
                {
                    bestW = w;
                    best = m;
                }
            }
            return best;
        }

        /// <summary>
        /// The conversation happened (the game's own, so the two are face to face): the listener is in on the joke now,
        /// with the teller; the teller tells it, and the listener answers.
        /// </summary>
        public static void Told(Pawn teller, Pawn listener)
        {
            // together: the game's own conversation brought the two face to face (InteractionWorker_JokeRetold).
            if (teller == null || listener == null || !Persona.On) return;
            Fresh();
            int now = Now;
            long key = Key(teller, listener);
            var joke = Choose(teller, listener);
            picks.Remove(key);
            SocialPace.Started(teller);
            lastTold[teller.thingIDNumber] = now;
            pairTold[key] = now;
            if (pairTold.Count > 2000) pairTold.Clear();
            if (joke == null) return;
            var mem = SharedMemories.Add(teller, listener, TimesDefs.JokeHeard, joke.label, 0.8f);
            if (mem == null) return;
            string about = joke.NameOf(joke.Other(teller.thingIDNumber)) ?? "?";
            var tags = new[] { joke.a == teller.thingIDNumber ? "byme" : "byother" };
            var turns = new List<(Pawn speaker, Pawn listener, string text)>();
            string tell = Line("Rimfolk_JokeRetold_Tell", teller, listener, about, joke.label, tags);
            if (!tell.NullOrEmpty()) turns.Add((teller, listener, tell));
            string reply = Line("Rimfolk_JokeRetold_Reply", listener, teller, about, joke.label, tags);
            if (!reply.NullOrEmpty()) turns.Add((listener, teller, reply));
            if (turns.Count == 0) return;
            var queue = TalkTracker.Instance?.exchanges;
            if (queue != null && queue.AddTurns(turns, now, Talk.FirstGap, Talk.LineGap) != 0) return;
            foreach (var (speaker, to, text) in turns) Speech.Say(speaker, text, to);
        }

        private static string Line(string pool, Pawn speaker, Pawn listener, string about, string label, string[] tags)
        {
            try
            {
                string raw = Flavor.FromPool(pool, speaker, null, null, tags);
                if (raw.NullOrEmpty()) return null;
                return raw.Formatted(speaker.Named("PAWN"), listener.Named("OTHER"), about.Named("NAME"), (label ?? "").Named("LABEL")).Resolve().CapitalizeFirst();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("joke retold line " + pool, e);
                return null;
            }
        }
    }
}
