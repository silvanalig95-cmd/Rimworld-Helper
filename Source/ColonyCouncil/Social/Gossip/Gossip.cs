using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Passing on what someone did: an ordinary conversation in the game's own picker (like the moments), weighed by
    /// <see cref="Gossip.Weight"/>. It only comes up when the speaker knows of a deed the listener doesn't, by someone
    /// else (or their own good deed, for the proud), and the one it's about isn't there to hear it (unless the speaker
    /// is a hothead or their rival, and doesn't care who hears). The game writes the conversation into the social log;
    /// the words themselves are said over their heads (<see cref="Speech"/>).
    /// <para>Separate from the talk about the council (InteractionWorker_CouncilGossip, about trust in the council).</para>
    /// </summary>
    public class InteractionWorker_Gossip : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            try
            {
                return Gossip.Weight(initiator, recipient);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gossip weight", e);
                return 0f;
            }
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
            try
            {
                Gossip.Told(initiator, recipient, extraSentencePacks);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gossip", e);
            }
        }
    }

    /// <summary>
    /// How a story goes round (see <see cref="InteractionWorker_Gossip"/>): who tells what to whom, coloured by the
    /// teller's opinion of the one it's about and by their character (<see cref="RepMath.SlantFor"/>); how much it grows
    /// in the telling (<see cref="RepMath.Retold"/>); how far the listener believes it (<see cref="RepMath.Belief"/>);
    /// and the one or two lines said over their heads, from pools by the deed, the slant and the speaker's character.
    /// Cooldowns and choices are not saved: a load only gives a story a second chance.
    /// </summary>
    public static class Gossip
    {
        /// <summary>One colonist passes on a story at most this often, and the same two not more than once in this long.</summary>
        public const int TellerGapTicks = GenDate.TicksPerHour * 10, PairGapTicks = GenDate.TicksPerDay * 2;

        /// <summary>A colonist with the colony fewer days than this is still learning who's who.</summary>
        public const float NewcomerDays = 20f;

        private sealed class Pick
        {
            public Deed deed;
            public float weight;
            public bool present;
            public bool own;
            public int tick;
        }

        private static readonly Dictionary<int, int> lastTold = new Dictionary<int, int>();
        private static readonly Dictionary<long, int> pairTold = new Dictionary<long, int>();
        private static readonly Dictionary<long, Pick> picks = new Dictionary<long, Pick>();

        private static int Now => Find.TickManager.TicksGame;

        private static long Key(Pawn a, Pawn b) => ((long)a.thingIDNumber << 32) | (uint)b.thingIDNumber;

        public static void Reset()
        {
            lastTold.Clear();
            pairTold.Clear();
            picks.Clear();
        }

        public static bool Newcomer(Pawn p) =>
            p?.records != null && p.records.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) < NewcomerDays * GenDate.TicksPerDay;

        // ---------------- Whether there's anything to tell ----------------

        /// <summary>How likely this conversation is between the two right now (0: nothing to tell, or told lately).</summary>
        public static float Weight(Pawn teller, Pawn listener)
        {
            if (!Reputations.On || teller == null || listener == null || teller == listener) return 0f;
            if (!Reputations.Public(teller) || !Reputations.Public(listener) || teller.DevelopmentalStage.Baby()) return 0f;
            int now = Now;
            if (lastTold.TryGetValue(teller.thingIDNumber, out int t) && t <= now && now - t < TellerGapTicks) return 0f;
            long key = Key(teller, listener);
            if (pairTold.TryGetValue(key, out int pt) && pt <= now && now - pt < PairGapTicks) return 0f;
            if (!picks.TryGetValue(key, out var pick) || now - pick.tick >= Sched.RareTicks || now < pick.tick)
            {
                if (picks.Count > 600) picks.Clear();
                pick = Choose(teller, listener) ?? new Pick();
                pick.tick = now;
                picks[key] = pick;
            }
            return pick.deed == null ? 0f : pick.weight * (ColonyCouncilMod.Settings?.momentFrequency ?? 1f);
        }

        private static bool Rival(Pawn a, Pawn b) => (CouncilGameComponent.Instance?.social?.Strength(a, b, TieKind.Rivals) ?? 0f) >= 20f;

        private static bool Friend(Pawn a, Pawn b) => (CouncilGameComponent.Instance?.social?.Strength(a, b, TieKind.Friends) ?? 0f) >= 20f;

        /// <summary>
        /// The two belong to the same group (Social/Groups/, read only through its tags: group:&lt;id&gt;). Group members
        /// speak up for each other a little.
        /// </summary>
        private static bool SameGroup(Pawn a, Pawn b)
        {
            var ta = PeopleTags.Identity(a);
            var tb = PeopleTags.Identity(b);
            foreach (var t in ta)
                if (t.StartsWith("group:", StringComparison.Ordinal) && tb.Contains(t))
                    return true;
            return false;
        }

        /// <summary>The story the teller would tell this listener now, and how likely: the likeliest of all they know.</summary>
        private static Pick Choose(Pawn teller, Pawn listener)
        {
            var tr = Reputations.Tracker;
            if (tr == null) return null;
            int now = Now;
            var pt = Persona.Values(teller);
            bool newcomer = Newcomer(listener);
            Pick best = null;
            // What the teller thinks of each subject, and whether they're standing right there: asked once per subject.
            var about = new Dictionary<int, (float opinion, int present)>();
            void Consider(Deed d, bool own)
            {
                if (d?.def == null || !d.def.told || d.doer == listener.thingIDNumber || d.Knows(listener.thingIDNumber)) return;
                if (!own && d.doer == teller.thingIDNumber) return;
                if (own && !RepMath.MayBoast(pt[(int)PersonaAxis.Pride], d.def.Valence)) return;
                float w = d.Weight(now);
                if (w < 0.15f) return;
                var subject = own ? teller : tr.PawnOf(d.doer);
                if (subject == null) return;
                if (!about.TryGetValue(d.doer, out var a))
                    about[d.doer] = a = (own ? 0f : teller.relations?.OpinionOf(subject) ?? 0f, -1);
                float weight = RepMath.TellWeight(d.def.interest * Math.Min(1.5f, w), d.AgeDays(now), newcomer, pt[(int)PersonaAxis.Humour],
                    pt[(int)PersonaAxis.Warmth], a.opinion);
                if (own) weight *= 0.4f;
                if (best != null && best.weight >= weight) return;
                // Only now, for a story that could be the one: is its subject standing right there?
                if (a.present < 0)
                {
                    bool here = !own && (SocialLog.Near(subject, teller) || SocialLog.Near(subject, listener));
                    a.present = here && RepMath.MayTellInFront(pt[(int)PersonaAxis.Temper], Rival(teller, subject)) ? 1 : here ? 2 : 0;
                    about[d.doer] = a;
                }
                // 2: there, and the teller isn't one to say it to their face.
                if (a.present == 2) return;
                bool present = a.present == 1;
                if (present) weight *= 0.5f;
                if (best != null && best.weight >= weight) return;
                best = new Pick { deed = d, weight = weight, present = present, own = own };
            }
            foreach (var d in tr.KnownBy(teller.thingIDNumber)) Consider(d, false);
            if (RepMath.MayBoast(pt[(int)PersonaAxis.Pride], 1))
                foreach (var d in tr.By(teller.thingIDNumber))
                    Consider(d, true);
            return best;
        }

        // ---------------- Telling it ----------------

        /// <summary>
        /// The conversation happened (the game's own, so the two are face to face): the listener now knows the story, in
        /// the teller's version, and believes it as far as their nature lets them; the teller says it, and the listener
        /// (or, told to their face, the one it's about) answers.
        /// </summary>
        public static void Told(Pawn teller, Pawn listener, List<RulePackDef> packs)
        {
            // together: the game's own conversation brought the two face to face (InteractionWorker_Gossip).
            var tr = Reputations.Tracker;
            if (!Reputations.On || tr == null || teller == null || listener == null) return;
            int now = Now;
            var pick = Choose(teller, listener);
            picks.Remove(Key(teller, listener));
            lastTold[teller.thingIDNumber] = now;
            pairTold[Key(teller, listener)] = now;
            if (pairTold.Count > 2000) pairTold.Clear();
            var d = pick?.deed;
            if (d == null) return;
            var subject = pick.own ? teller : tr.PawnOf(d.doer);
            if (subject == null) return;
            int valence = d.def.Valence;
            var pt = Persona.Values(teller);
            var pl = Persona.Values(listener);
            float opinion = pick.own ? 0f : teller.relations?.OpinionOf(subject) ?? 0f;
            bool rival = !pick.own && Rival(teller, subject);
            bool friend = !pick.own && Friend(teller, subject);
            // One of their own group: spoken up for a little, as a friend would be.
            if (!pick.own && !rival && SameGroup(teller, subject)) opinion += 15f;
            var slant = RepMath.SlantFor(valence, opinion, friend, rival, pt[(int)PersonaAxis.Humour], pick.own, Rand.Value, d.def.Grave);
            float heard = pick.own ? d.size : d.KnowerOf(teller.thingIDNumber)?.version ?? d.size;
            float version = RepMath.Retold(heard, d.size, slant, valence, Rand.Value);
            float listenerOfSubject = listener.relations?.OpinionOf(subject) ?? 0f;
            float belief = RepMath.Belief(valence, pl[(int)PersonaAxis.Trust], listenerOfSubject, listener.relations?.OpinionOf(teller) ?? 0f, slant);
            tr.Learn(d, listener, teller, version, belief, slant);
            // Running others down (or passing on their troubles) is talked of in turn: the teller's own name for it.
            if (!pick.own && (slant == Slant.Scorn || (valence < 0 && slant != Slant.Praise)))
                Deeds.Record("Rimfolk_Deed_Gossiped", teller, null, slant == Slant.Scorn ? 0.25f : 0.15f, null, null, new[] { listener }, false);
            // The social log's line for it: what it was about and how it was told.
            if (packs != null)
            {
                var topic = d.def.logTopic ?? DefDatabase<RulePackDef>.GetNamedSilentFail("Rimfolk_GossipTopic_Generic");
                var how = DefDatabase<RulePackDef>.GetNamedSilentFail(LogPack(valence, slant, pick.present));
                if (topic != null && how != null)
                {
                    packs.Add(topic);
                    packs.Add(how);
                }
            }
            var tags = Tags(d, subject, teller, listener, slant, pick, tr);
            string pool = pick.present ? "Rimfolk_Gossip_Present" : pick.own ? "Rimfolk_Gossip_Boast" : "Rimfolk_Gossip_" + (valence < 0 ? "Bad_" : "Good_") + slant;
            string line = Line(pool, teller, d, subject, listener, tags);
            if (!line.NullOrEmpty()) Speech.Say(teller, line, listener);
            if (pick.present)
            {
                InFront(teller, listener, subject, d, slant, valence, tags);
                return;
            }
            Answer(teller, listener, subject, d, slant, valence, belief, listenerOfSubject, tags);
        }

        private static string LogPack(int valence, Slant slant, bool present)
        {
            if (present) return "Rimfolk_GossipLog_Present";
            if (slant == Slant.Boast) return "Rimfolk_GossipLog_Boast";
            return "Rimfolk_GossipLog_" + (valence < 0 ? "Bad_" : "Good_") + slant;
        }

        /// <summary>The tags a line about this story can ask for, beside the speaker's own.</summary>
        private static string[] Tags(Deed d, Pawn subject, Pawn teller, Pawn listener, Slant slant, Pick pick, ReputationTracker tr)
        {
            var tags = new List<string>
            {
                d.def.Tag, "slant:" + slant.ToString().ToLowerInvariant(), d.victimName.NullOrEmpty() ? "novictim" : "hasvictim",
                d.def.Valence < 0 ? "bad" : "good"
            };
            if (Newcomer(listener)) tags.Add("tonewcomer");
            if (!pick.own)
            {
                if (Friend(teller, subject)) tags.Add("aboutfriend");
                if (Rival(teller, subject)) tags.Add("aboutrival");
                if (subject.relations?.DirectRelations?.Any(r => r.otherPawn == teller && r.def != null && r.def.familyByBloodRelation) ?? false) tags.Add("aboutkin");
                // The groups (Social/Groups/, by their tags): one of the teller's own, or someone who belongs nowhere.
                if (SameGroup(teller, subject)) tags.Add("aboutgroupmate");
                else if (PeopleTags.Identity(subject).Contains("outsider")) tags.Add("aboutoutsider");
            }
            // Not the first time: the same thing again, or a big one.
            if (d.size >= 2f || tr.By(d.doer).Count(x => x.def == d.def) >= 2) tags.Add("again");
            return tags.ToArray();
        }

        /// <summary>A line from a pool in the speaker's voice, with the story's people and words filled in; null if none fits.</summary>
        private static string Line(string pool, Pawn speaker, Deed d, Pawn subject, Pawn listener, string[] tags)
        {
            try
            {
                string raw = Flavor.FromPool(pool, speaker, null, null, tags);
                if (raw.NullOrEmpty()) return null;
                return raw.Formatted(speaker.Named("PAWN"), listener.Named("OTHER"), subject.Named("SUBJECT"), (d.victimName ?? "").Named("VICTIM"),
                    Deeds.TextOf(d, subject).Named("DEED"), (d.label ?? "").Named("LABEL"), (d.detail ?? "").Named("DETAIL")).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gossip line " + pool, e);
                return null;
            }
        }

        /// <summary>
        /// What the listener says to it, by what they make of it: admiring, condemning, laughing, doubting it, or sticking
        /// up for someone they think well of. Taking the teller's side brings the two a little closer; sticking up for
        /// the one it's about is a small friction between them.
        /// </summary>
        private static void Answer(Pawn teller, Pawn listener, Pawn subject, Deed d, Slant slant, int valence, float belief, float opinionOfSubject, string[] tags)
        {
            // together: said back in the same conversation (Told).
            string kind;
            if (valence < 0 && opinionOfSubject >= 40f && slant != Slant.Praise) kind = "Defend";
            else if (belief < 0.45f) kind = "Doubt";
            else if (slant == Slant.Joke) kind = "Laugh";
            else if (valence > 0) kind = "Admire";
            else if (valence < 0) kind = slant == Slant.Praise ? "Shrug" : "Condemn";
            else kind = "Shrug";
            if (Rand.Chance(0.7f))
            {
                string line = Line("Rimfolk_Gossip_React_" + kind, listener, d, subject, teller, tags);
                if (!line.NullOrEmpty()) Speech.Say(listener, line, teller);
            }
            if (kind == "Defend")
                MomentTones.RecordEvent("friction", teller, listener, 6f);
            else if (kind != "Doubt" && kind != "Shrug" && belief >= 0.7f)
                CouncilGameComponent.Instance?.social?.Bump(teller, listener, TieKind.Friends, 1f, null, true);
        }

        /// <summary>
        /// Told with its subject right there: they answer for themselves. Run down to their face, they take it badly (a
        /// friction, and the teller is caught at it); talked up, it brings them a little closer.
        /// </summary>
        private static void InFront(Pawn teller, Pawn listener, Pawn subject, Deed d, Slant slant, int valence, string[] tags)
        {
            // together: the one it's about is standing with them (SocialLog.Near in Choose); Speech.Say checks it again.
            if (!Together.Can(subject, teller)) return;
            bool against = slant == Slant.Scorn || (valence < 0 && slant != Slant.Praise);
            var all = new List<string>(tags) { against ? "against" : "for" };
            string line = Line("Rimfolk_Gossip_React_Subject", subject, d, subject, teller, all.ToArray());
            if (!line.NullOrEmpty()) Speech.Say(subject, line, teller);
            if (against)
            {
                CouncilGameComponent.GiveThought(subject, DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Friction_TalkedAboutMe"), teller);
                CouncilGameComponent.GiveThought(teller, DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Friction_CaughtGossiping"), subject);
                MomentTones.RecordEvent("friction", subject, teller, 12f);
                CouncilGameComponent.Instance?.social?.Bump(subject, teller, TieKind.Rivals, 2f, null, true);
            }
            else if (valence > 0)
                CouncilGameComponent.Instance?.social?.Bump(subject, teller, TieKind.Friends, 1f, null, true);
        }
    }
}
