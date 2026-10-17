using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A conversation between two colonists, in the game's own social picker: one interaction per topic (the last raid,
    /// the council, the food, a newcomer...) and one each for remembering, an in-joke and comfort, so the game picks what
    /// they talk about by how much it matters to them now, beside its own chitchat. The talk's share is fixed (the
    /// budget, from the speaker's character and the moments setting) and only split by what's worth talking about, so a
    /// busy week means livelier talk, not more of it. See <see cref="Talk"/>.
    /// </summary>
    public class InteractionWorker_Talk : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            try
            {
                return Talk.Weight(interaction, initiator, recipient);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("talk weight " + interaction?.defName, e);
                return 0f;
            }
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
            try
            {
                Talk.Run(interaction, initiator, recipient, extraSentencePacks);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("talk " + interaction?.defName, e);
            }
        }
    }

    /// <summary>One conversation as it's worked out: who, about what, how it goes, and its lines.</summary>
    public sealed class Conversation
    {
        public Pawn a, b;
        public TalkKind kind;
        public TopicDef topic;
        public TalkSubject subject;
        public SharedMemory memory;
        public string feeling;
        public TalkScene scene;
        public float stanceA, stanceB;
        public TalkOutcome outcome;
        public bool hot, friends, rivals, lovers, family, mend, sore;
        public readonly List<string> lines = new List<string>();
    }

    /// <summary>
    /// Conversations between colonists (Social/Talk/): what they talk about and how it goes, in their own voices, with
    /// views that persist and shift. Plugs into the game's own interaction picker (<see cref="InteractionWorker_Talk"/>),
    /// so it never ticks to find talkers; the lines of a talk under way are said one by one by <see cref="TalkTracker"/>.
    /// <list type="bullet">
    /// <item>A topic comes up by how much it matters to the two now: news (a raid yesterday, a death, a decree), how strongly each
    /// cares, the speaker's character, their tie (rivals pick what they disagree on), what they've been through together.</item>
    /// <item>It goes by both views and both characters (<see cref="TalkMath.Outcome"/>): agreeing, a friendly debate, a heated
    /// argument, teasing; or they remember something together, share an in-joke, or one comforts the other.</item>
    /// <item>It shows as two to four lines over their heads (<see cref="Speech.Say"/>), the speakers taking turns, each in their
    /// own voice; the social log gets one line saying what it was about and how it went.</item>
    /// <item>It leaves small marks: an opinion of each other, views a little closer or further apart, how things stand between
    /// them (<see cref="MomentTones"/>), now and then a nudge to who they are.</item>
    /// </list>
    /// Behind "Personalities and social life" and "Moments between colonists"; frictions follow their own setting, and
    /// the moments setting's frequency scales it.
    /// </summary>
    public static class Talk
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => Persona.On && S != null && S.colonistMoments && Current.Game != null;

        /// <summary>The share of a colonist's random talk Rimfolk's conversations take, next to chitchat at 1.</summary>
        public const float BaseBudget = 0.1f;
        /// <summary>Ticks before the first line, and between lines.</summary>
        public const int FirstGap = 45, LineGap = 165;

        private static int Now => Find.TickManager.TicksGame;

        // ---------------- The talk interactions ----------------

        private static List<InteractionDef> defs;
        private static Dictionary<InteractionDef, int> index;
        private static TopicDef[] topicOf;
        private static TalkKind[] kindOf;
        private static float[] extWeight;
        private static int builtGeneration = -1;

        private static void Build()
        {
            if (defs != null && builtGeneration == DefRefs.Generation) return;
            builtGeneration = DefRefs.Generation;
            defs = new List<InteractionDef>();
            index = new Dictionary<InteractionDef, int>();
            var byInteraction = new Dictionary<InteractionDef, TopicDef>();
            foreach (var t in TopicDef.All)
                if (t.interaction != null && !byInteraction.ContainsKey(t.interaction))
                    byInteraction[t.interaction] = t;
            foreach (var d in DefDatabase<InteractionDef>.AllDefsListForReading)
            {
                var ext = d.GetModExtension<TalkExtension>();
                if (ext == null || !(d.Worker is InteractionWorker_Talk)) continue;
                if (ext.kind == TalkKind.Topic && !byInteraction.ContainsKey(d)) continue;
                index[d] = defs.Count;
                defs.Add(d);
            }
            topicOf = new TopicDef[defs.Count];
            kindOf = new TalkKind[defs.Count];
            extWeight = new float[defs.Count];
            for (int i = 0; i < defs.Count; i++)
            {
                var ext = defs[i].GetModExtension<TalkExtension>();
                kindOf[i] = ext.kind;
                extWeight[i] = ext.weight;
                if (ext.kind == TalkKind.Topic) topicOf[i] = byInteraction[defs[i]];
            }
            // Another mod with conversations of its own: ours step back (ModOverlapDef lists our talk interactions).
            foreach (var o in DefDatabase<ModOverlapDef>.AllDefsListForReading)
            {
                if (o.moments.NullOrEmpty()) continue;
                bool active;
                try
                {
                    active = o.Active;
                }
                catch
                {
                    active = false;
                }
                if (!active) continue;
                foreach (var m in o.moments)
                    if (m != null && index.TryGetValue(m, out int k))
                        extWeight[k] *= o.factor;
            }
            BuildLines();
        }

        /// <summary>A new game or a load: nothing kept from the last one.</summary>
        public static void Reset()
        {
            cacheTick = -1;
            cacheA = cacheB = -1;
            cached = null;
        }

        // ---------------- Who talks ----------------

        /// <summary>One of the colony's people with an inner life, past childhood, up and about.</summary>
        public static bool Eligible(Pawn p) =>
            p != null && !p.Dead && p.RaceProps != null && p.RaceProps.Humanlike && (p.IsColonist || p.IsSlaveOfColony) && !p.IsQuestLodger()
            && People.HasInnerLife(p) && !p.DevelopmentalStage.Baby() && !p.DevelopmentalStage.Child() && Persona.Eligible(p);

        // One pair's shares are worked out once per tick (the game asks for every talk interaction in turn).
        private static int cacheTick = -1, cacheA = -1, cacheB = -1;
        private static float[] cached;

        public static float Weight(InteractionDef def, Pawn a, Pawn b)
        {
            if (!On || def == null || a == null || b == null) return 0f;
            Build();
            if (!index.TryGetValue(def, out int k)) return 0f;
            int now = Now;
            if (cacheTick != now || cacheA != a.thingIDNumber || cacheB != b.thingIDNumber)
            {
                cacheTick = now;
                cacheA = a.thingIDNumber;
                cacheB = b.thingIDNumber;
                cached = Shares(a, b, now);
            }
            return cached == null ? 0f : cached[k];
        }

        private static float[] Shares(Pawn a, Pawn b, int now)
        {
            if (!Eligible(a) || !Eligible(b) || !Together.Can(a, b)) return null;
            var tr = TalkTracker.Instance;
            if (tr == null || !tr.MayStart(a, b, now)) return null;
            var scene = TalkScene.Of(a.Map);
            if (scene == null) return null;
            var va = Persona.Values(a);
            var vb = Persona.Values(b);
            var tie = TieOf(a, b);
            var sal = new float[defs.Count];
            for (int k = 0; k < defs.Count; k++)
            {
                try
                {
                    sal[k] = Salience(k, a, b, va, vb, tie, scene, tr, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("talk salience " + defs[k].defName, e);
                    sal[k] = 0f;
                }
            }
            // How talkative the speaker is (warm and playful more, cold and dour less) and how they feel today come in
            // through the moments' own factor (Heart.MomentWeightFactor, with the persona's lean), counted once.
            float budget = BaseBudget * S.momentFrequency * PairFactor(a, b, tie) * InteractionWorker_Moment.MomentWeightFactor(a, b, false, "talk");
            var shares = TalkMath.Shares(sal, budget);
            for (int k = 0; k < shares.Length; k++) shares[k] *= extWeight[k];
            return shares;
        }

        private struct Tie
        {
            public bool friends, rivals, lovers, family;
            public float friendStrength;
        }

        private static Tie TieOf(Pawn a, Pawn b)
        {
            var t = new Tie();
            var social = CouncilGameComponent.Instance?.social;
            if (social != null)
            {
                t.friendStrength = social.Strength(a, b, TieKind.Friends);
                t.friends = t.friendStrength >= 20f;
                t.rivals = social.Strength(a, b, TieKind.Rivals) >= 20f;
            }
            if (!t.friends && (a.relations?.OpinionOf(b) ?? 0) >= 40 && (b.relations?.OpinionOf(a) ?? 0) >= 40) t.friends = true;
            var rel = a.relations;
            if (rel != null)
            {
                t.lovers = rel.DirectRelationExists(PawnRelationDefOf.Lover, b) || rel.DirectRelationExists(PawnRelationDefOf.Spouse, b)
                           || rel.DirectRelationExists(PawnRelationDefOf.Fiance, b);
                t.family = !t.lovers && a.GetRelations(b).Any(r => r.familyByBloodRelation);
            }
            return t;
        }

        private static float PairFactor(Pawn a, Pawn b, Tie tie)
        {
            int ab = a.relations?.OpinionOf(b) ?? 0;
            float f = ab <= -50 ? 0.5f : ab < -20 ? 0.8f : 1f;
            if (tie.friends || tie.lovers) f *= 1.2f;
            if (tie.rivals) f *= 1.1f;
            return f;
        }

        private static float Salience(int k, Pawn a, Pawn b, float[] va, float[] vb, Tie tie, TalkScene scene, TalkTracker tr, int now)
        {
            switch (kindOf[k])
            {
                case TalkKind.Topic:
                {
                    var topic = topicOf[k];
                    var w = topic.Worker;
                    float rel = w.Relevance(a, b, scene);
                    if (rel <= 0f) return 0f;
                    var subject = w.Subject(a, b, scene);
                    if (topic.perSubject && subject == null) return 0f;
                    float sa = Stances.Get(a, topic, subject, scene), sb = Stances.Get(b, topic, subject, scene);
                    float hot = w.Hot(scene);
                    float character = TalkMath.Interest(va, topic.InterestWeights);
                    float tieF = TalkMath.TieFactor(tie.friends || tie.lovers, tie.rivals, sa, sb);
                    return TalkMath.Salience(rel, hot, TalkMath.Care(sa), TalkMath.Care(sb), character, tieF, tr.Freshness(a, b, topic.shortHash, now));
                }
                case TalkKind.Memory:
                case TalkKind.Joke:
                {
                    bool jokes = kindOf[k] == TalkKind.Joke;
                    float sum = 0f;
                    foreach (var m in SharedMemories.Of(a, b))
                    {
                        if (m.def == null || m.def.joke != jokes) continue;
                        float w = m.WeightNow(now);
                        if (m.lastRecalled >= 0 && m.lastRecalled <= now && now - m.lastRecalled < GenDate.TicksPerDay * 2) w *= 0.25f;
                        sum += w;
                    }
                    if (sum <= 0f) return 0f;
                    float f = jokes ? 0.7f : 0.5f;
                    if (tie.friends || tie.lovers) f *= 1.5f;
                    if (jokes && va[(int)PersonaAxis.Humour] <= -30f) f *= 0.4f;
                    if (!jokes && va[(int)PersonaAxis.Warmth] >= 30f) f *= 1.2f;
                    if (Low(b) != null) f *= jokes ? 0.2f : 0.6f;
                    return Mathf.Min(2.5f, sum * f);
                }
                case TalkKind.Comfort:
                {
                    var low = Low(b);
                    if (low == null) return 0f;
                    int ab = a.relations?.OpinionOf(b) ?? 0;
                    if (ab < -10) return 0f;
                    float f = 2.5f * (va[(int)PersonaAxis.Warmth] >= 0f ? 1f : 0.5f) * (ab >= 20 ? 1.3f : 1f);
                    // The one they're angry with, or ashamed before, isn't the one to comfort them.
                    var feel = LowFeeling(b);
                    if (feel?.target == a && (feel.def.key == "Anger" || feel.def.key == "Shame")) return 0f;
                    return f;
                }
            }
            return 0f;
        }

        private static readonly List<string> lowKeys = new List<string> { "Grief", "Despair", "Loneliness", "Fear", "Dread", "Worry", "Shame", "Homesick" };

        private static FeelingNow LowFeeling(Pawn p) => LifeConditions.Feels(p, lowKeys, 0.35f);

        /// <summary>Why someone is low ("grief", "fear", "lonely"...), or null if they aren't.</summary>
        public static string Low(Pawn p)
        {
            var f = LowFeeling(p);
            if (f?.def != null)
            {
                switch (f.def.key)
                {
                    case "Grief": return "grief";
                    case "Despair": return "despair";
                    case "Loneliness": return "lonely";
                    case "Fear":
                    case "Dread": return "fear";
                    case "Worry": return "worry";
                    case "Shame": return "shame";
                    case "Homesick": return "homesick";
                }
            }
            if ((p.needs?.mood?.CurLevelPercentage ?? 0.5f) < 0.25f) return "lowmood";
            if (Flavor.IsGrieving(p)) return "grief";
            return null;
        }

        // ---------------- A talk ----------------

        /// <summary>The game picked one of the talks for these two: work it out, show it and let it leave its marks.</summary>
        public static void Run(InteractionDef def, Pawn a, Pawn b, List<RulePackDef> packs)
        {
            if (!On || def == null || a == null || b == null) return;
            Build();
            if (!index.TryGetValue(def, out int k)) return;
            var tr = TalkTracker.Instance;
            var scene = TalkScene.Of(a.MapHeld);
            if (tr == null || scene == null) return;
            int now = Now;
            var c = new Conversation { a = a, b = b, kind = kindOf[k], topic = topicOf[k], scene = scene };
            var tie = TieOf(a, b);
            c.friends = tie.friends;
            c.rivals = tie.rivals;
            c.lovers = tie.lovers;
            c.family = tie.family;
            if (!Plan(c, tie)) return;
            // A talk that goes well after a spat they haven't made up for makes it up (and its lines may say so).
            c.mend = Warm(c, tie) && MomentTones.Allows("mend", a, b);
            Lines(c);
            Effects(c, tie);
            var sent = SummaryPack(c);
            if (sent != null && packs != null && !packs.Contains(sent)) packs.Add(sent);
            tr.Started(a, b, c.topic?.shortHash ?? 0, now);
            cacheTick = -1;
            if (c.lines.Count > 0) tr.exchanges.Add(a, b, c.lines, now, FirstGap, LineGap);
        }

        private static bool Plan(Conversation c, Tie tie)
        {
            var a = c.a;
            var b = c.b;
            var va = Persona.Values(a);
            var vb = Persona.Values(b);
            switch (c.kind)
            {
                case TalkKind.Topic:
                {
                    var w = c.topic.Worker;
                    c.subject = w.Subject(a, b, c.scene);
                    if (c.topic.perSubject && c.subject == null) return false;
                    c.stanceA = Stances.Get(a, c.topic, c.subject, c.scene);
                    c.stanceB = Stances.Get(b, c.topic, c.subject, c.scene);
                    c.hot = w.Hot(c.scene) >= 0.3f;
                    var tones = TonesOf(a, b);
                    var x = new OutcomeInput
                    {
                        stanceA = c.stanceA,
                        stanceB = c.stanceB,
                        temperA = va[(int)PersonaAxis.Temper],
                        temperB = vb[(int)PersonaAxis.Temper],
                        warmthA = va[(int)PersonaAxis.Warmth],
                        warmthB = vb[(int)PersonaAxis.Warmth],
                        humourA = va[(int)PersonaAxis.Humour],
                        humourB = vb[(int)PersonaAxis.Humour],
                        opinionAB = a.relations?.OpinionOf(b) ?? 0,
                        opinionBA = b.relations?.OpinionOf(a) ?? 0,
                        friends = tie.friends,
                        rivals = tie.rivals,
                        lovers = tie.lovers,
                        sore = tones.sore,
                        warmLately = tones.warm,
                        frictionsOn = S.frictions,
                        otherLow = Low(b) != null,
                        noTease = c.topic.noTease
                    };
                    c.sore = tones.sore;
                    c.outcome = TalkMath.Outcome(x, Rand.Value, Rand.Value);
                    return true;
                }
                case TalkKind.Memory:
                case TalkKind.Joke:
                {
                    bool jokes = c.kind == TalkKind.Joke;
                    c.memory = SharedMemories.Pick(a, b, m => m.def != null && m.def.joke == jokes);
                    if (c.memory == null) return false;
                    c.outcome = jokes ? TalkOutcome.Joke : TalkOutcome.Reminisce;
                    return true;
                }
                case TalkKind.Comfort:
                {
                    c.feeling = Low(b);
                    if (c.feeling == null) return false;
                    var f = LowFeeling(b);
                    if (f?.target != null && f.target != a && f.target != b && f.target.RaceProps?.Humanlike == true) c.subject = TalkSubject.Of(f.target);
                    else if (c.feeling == "grief")
                    {
                        var dead = TalkTracker.Instance?.Latest("Rimfolk_Topic_Dead", 20f);
                        if (dead != null && !dead.subject.NullOrEmpty()) c.subject = TalkSubject.Named(dead.subject);
                    }
                    c.outcome = TalkOutcome.Comfort;
                    return true;
                }
            }
            return false;
        }

        private static (bool sore, bool warm) TonesOf(Pawn a, Pawn b)
        {
            bool sore = !MomentTones.Allows("warm", a, b);
            bool warmLately = !MomentTones.Allows("friction", a, b);
            return (sore, warmLately);
        }

        // ---------------- Lines ----------------

        private static Dictionary<string, List<TalkLinesDef>> pools;

        private static string PoolKey(Def about, TalkTurn turn) => (about?.defName ?? "*") + "|" + (int)turn;

        private static void BuildLines()
        {
            pools = new Dictionary<string, List<TalkLinesDef>>();
            foreach (var d in DefDatabase<TalkLinesDef>.AllDefsListForReading)
            {
                if (d.lines == null || !d.lines.Any) continue;
                Def about = (Def)d.topic ?? d.memory;
                string key = PoolKey(about, d.turn);
                if (!pools.TryGetValue(key, out var list)) pools[key] = list = new List<TalkLinesDef>();
                list.Add(d);
            }
        }

        private static List<TalkLinesDef> PoolsFor(Def about, TalkTurn turn, TalkOutcome how)
        {
            if (pools == null) BuildLines();
            var found = new List<TalkLinesDef>();
            if (pools.TryGetValue(PoolKey(about, turn), out var list))
                foreach (var d in list)
                    if (d.For(how)) found.Add(d);
            return found;
        }

        /// <summary>The lines of the talk, in turn: the first speaker opens, the other replies, and so on.</summary>
        private static void Lines(Conversation c)
        {
            int turns = TalkMath.Turns(c.outcome, Rand.Value);
            for (int i = 0; i < turns; i++)
            {
                bool first = TalkMath.ByFirst(i);
                var speaker = first ? c.a : c.b;
                var listener = first ? c.b : c.a;
                string line = Line(c, TalkMath.TurnAt(i), speaker, listener, first);
                if (line.NullOrEmpty())
                {
                    // Nothing written for this turn: the talk ends here (an opening it can't do without).
                    if (i == 0) return;
                    break;
                }
                c.lines.Add(line);
            }
        }

        private static string Line(Conversation c, TalkTurn turn, Pawn speaker, Pawn listener, bool first)
        {
            Def about = (Def)c.topic ?? c.memory?.def;
            var specific = about != null ? PoolsFor(about, turn, c.outcome) : new List<TalkLinesDef>();
            var generic = PoolsFor(null, turn, c.outcome);
            if (specific.Count == 0 && generic.Count == 0) return null;
            var tags = Tags(c, speaker, listener, first, turn);
            var v = Persona.Values(speaker);
            var tone = (Tone)TalkMath.Voice((int)ToneUtility.ToneFor(speaker), v[(int)PersonaAxis.Temper], v[(int)PersonaAxis.Warmth],
                v[(int)PersonaAxis.Humour], v[(int)PersonaAxis.Outlook], c.outcome, first);
            bool hasSubject = c.subject != null && !c.subject.name.NullOrEmpty();
            bool hasLabel = c.memory != null && !c.memory.label.NullOrEmpty();
            var (raw, pool) = PickLine(specific, generic, tone, tags, hasSubject, hasLabel);
            if (raw == null) return null;
            Flavor.Memory.Remember(pool, raw, pool.lines.AllLines().Count());
            string label = c.memory?.label ?? "";
            string topic = About(c.topic, c.subject) ?? "";
            return raw.Formatted(speaker.Named("PAWN"), listener.Named("OTHER"), (c.subject?.name ?? "").Named("SUBJECT"), label.Named("LABEL"), topic.Named("TOPIC")).Resolve();
        }

        private static (string line, TalkLinesDef pool) PickLine(List<TalkLinesDef> specific, List<TalkLinesDef> generic, Tone tone, HashSet<string> tags, bool hasSubject, bool hasLabel)
        {
            var cands = new List<(string line, float w, TalkLinesDef pool)>();
            void From(List<TalkLinesDef> pools, bool own, bool plainToo, bool avoidRecent, float factor)
            {
                foreach (var d in pools)
                {
                    var recent = avoidRecent ? Flavor.Memory.Recent(d) : null;
                    void Add(List<string> list, float f)
                    {
                        if (list == null) return;
                        foreach (var l in list)
                        {
                            if (l.NullOrEmpty() || !Flavor.TechFits(l)) continue;
                            int m = Flavor.Match(l, tags);
                            if (m < 0) continue;
                            string text = Flavor.Strip(l);
                            if (!hasSubject && text.Contains("{SUBJECT")) continue;
                            if (!hasLabel && text.Contains("{LABEL")) continue;
                            if (recent != null && recent.Contains(text)) continue;
                            cands.Add((text, (m == 0 ? 1f : 2.5f + 1.5f * m) * f * factor, d));
                        }
                    }
                    if (own) Add(d.lines.For(tone), tone == Tone.Plain ? 1f : 1.5f);
                    if (plainToo && tone != Tone.Plain) Add(d.lines.plain, 1f);
                    if (!own && !plainToo)
                        foreach (var t in new[] { Tone.Warm, Tone.Blunt, Tone.Cheerful, Tone.Gloomy, Tone.Cold })
                            Add(d.lines.For(t), 0.5f);
                }
            }
            foreach (bool avoid in new[] { true, false })
            {
                cands.Clear();
                From(specific, true, true, avoid, 2f);
                From(generic, true, true, avoid, 1f);
                if (cands.Count == 0)
                {
                    // Nothing in their voice or plain: any voice, rather than silence.
                    From(specific, false, false, avoid, 2f);
                    From(generic, false, false, avoid, 1f);
                }
                if (cands.Count > 0) break;
            }
            if (cands.Count == 0) return (null, null);
            var pick = cands.RandomElementByWeight(x => x.w);
            return (pick.line, pick.pool);
        }

        /// <summary>The moment's tags for a line: the speaker's own (Flavor), their side and the other's, how it goes, their tie, the news.</summary>
        private static HashSet<string> Tags(Conversation c, Pawn speaker, Pawn listener, bool first, TalkTurn turn)
        {
            var tags = Flavor.TagsFor(speaker);
            tags.Add(c.outcome.ToString().ToLowerInvariant());
            float mine = first ? c.stanceA : c.stanceB, theirs = first ? c.stanceB : c.stanceA;
            if (c.kind == TalkKind.Topic)
            {
                tags.Add(TalkMath.Side(mine));
                tags.Add("other" + TalkMath.Side(theirs));
                if (Mathf.Abs(mine) >= TalkMath.StrongAt) tags.Add("strong");
                if (c.hot) tags.Add("hot");
                c.topic.Worker.AddTags(speaker, listener, c.scene, c.subject, tags);
            }
            if (c.subject != null) tags.Add("subject");
            if (c.friends) tags.Add("friends");
            if (c.rivals) tags.Add("rivals");
            if (c.lovers) tags.Add("lovers");
            if (c.family) tags.Add("family");
            if (c.mend) tags.Add("mend");
            if (c.sore) tags.Add("sore");
            if (c.memory?.def != null)
            {
                tags.Add(c.memory.def.valence > 0 ? "good" : c.memory.def.valence < 0 ? "bad" : "mixed");
                if (c.memory.def.joke) tags.Add(TalkMath.Worn(c.memory.recalls) ? "worn" : c.memory.recalls == 0 ? "fresh" : "told");
                if (c.memory.recalls == 0) tags.Add("first");
                // Who it happened to most (the one rescued, the one taught): "mine" when it's the speaker.
                tags.Add(c.memory.a == speaker.thingIDNumber ? "byme" : "byother");
            }
            if (c.feeling != null) tags.Add(c.feeling);
            // How the one teased took it: the reply knows.
            if (c.outcome == TalkOutcome.Tease && !first && TookItBadly(speaker, listener)) tags.Add("stung");
            return tags;
        }

        private static bool TookItBadly(Pawn teased, Pawn teaser)
        {
            var v = Persona.Values(teased);
            int op = teased.relations?.OpinionOf(teaser) ?? 0;
            return v[(int)PersonaAxis.Humour] < -20f && op < 30 || v[(int)PersonaAxis.Pride] >= 50f && op < 20 || Low(teased) != null;
        }

        private static RulePackDef SummaryPack(Conversation c)
        {
            string name;
            switch (c.outcome)
            {
                case TalkOutcome.Agree: name = "Rimfolk_TalkSent_Agree"; break;
                case TalkOutcome.Debate: name = "Rimfolk_TalkSent_Debate"; break;
                case TalkOutcome.Argue: name = "Rimfolk_TalkSent_Argue"; break;
                case TalkOutcome.Tease: name = TookItBadly(c.b, c.a) ? "Rimfolk_TalkSent_TeaseStung" : "Rimfolk_TalkSent_Tease"; break;
                case TalkOutcome.Joke: name = c.memory != null && TalkMath.Worn(c.memory.recalls) ? "Rimfolk_TalkSent_JokeWorn" : "Rimfolk_TalkSent_Joke"; break;
                case TalkOutcome.Reminisce: name = c.memory?.def?.valence < 0 ? "Rimfolk_TalkSent_Grief" : "Rimfolk_TalkSent_Reminisce"; break;
                default: return null;
            }
            if (c.mend) name = "Rimfolk_TalkSent_MadeUp";
            return DefRefs.Named<RulePackDef>(name);
        }

        // ---------------- What it leaves ----------------

        private static ThoughtDef T(string name) => DefRefs.Named<ThoughtDef>(name);

        private static void Effects(Conversation c, Tie tie)
        {
            // together: the game's own picker brought the two face to face (InteractionWorker_Talk.Interacted).
            var a = c.a;
            var b = c.b;
            var social = CouncilGameComponent.Instance?.social;
            bool warm = Warm(c, tie);
            string topicLabel = About(c.topic, c.subject);
            using var _cause = Causes.Push("CC_Talk_Cause".Translate(a.LabelShort, b.LabelShort).Resolve());
            switch (c.outcome)
            {
                case TalkOutcome.Agree:
                    Both(a, b, "Rimfolk_Talk_Agreed");
                    Views(c, (sa, sb, pab, pba) => (TalkMath.Confirmed(sa, sb, pba * 0.5f), TalkMath.Confirmed(sb, sa, pab * 0.5f)));
                    social?.Bump(a, b, TieKind.Friends, 1.5f);
                    break;
                case TalkOutcome.Debate:
                    Both(a, b, "Rimfolk_Talk_Debated");
                    Views(c, (sa, sb, pab, pba) => (PersonaMath.Persuaded(sa, sb, pba), PersonaMath.Persuaded(sb, sa, pab)));
                    if (warm) social?.Bump(a, b, TieKind.Friends, 1f);
                    break;
                case TalkOutcome.Argue:
                    Both(a, b, "Rimfolk_Talk_Argued");
                    bool harden = Rand.Chance(0.6f);
                    Views(c, (sa, sb, pab, pba) => harden ? (TalkMath.Hardened(sa, sb), TalkMath.Hardened(sb, sa)) : (PersonaMath.Persuaded(sa, sb, pba), PersonaMath.Persuaded(sb, sa, pab)));
                    social?.Bump(a, b, TieKind.Rivals, 2f, topicLabel != null ? "CC_TieNote_TalkArgued".Translate(topicLabel).Resolve() : null);
                    MomentTones.RecordEvent("friction", a, b, 10f);
                    if (topicLabel != null) TalkTracker.Instance?.lastArgued.SetOrAdd(PersonaMath.PairKey(a.thingIDNumber, b.thingIDNumber), topicLabel);
                    // Arguing all the time hardens the temper, a little.
                    if (Rand.Chance(0.2f)) Persona.Nudge(a, PersonaAxis.Temper, 1.5f);
                    if (Rand.Chance(0.2f)) Persona.Nudge(b, PersonaAxis.Temper, 1.5f);
                    break;
                case TalkOutcome.Tease:
                    One(a, b, "Rimfolk_Talk_Teaser");
                    if (TookItBadly(b, a))
                    {
                        One(b, a, "Rimfolk_Talk_TeasedStung");
                        MomentTones.RecordEvent("friction", a, b, 4f);
                    }
                    else
                    {
                        One(b, a, "Rimfolk_Talk_TeasedFine");
                        MomentTones.RecordEvent("warm", a, b, 0f);
                        if (Rand.Chance(0.1f)) Persona.Nudge(b, PersonaAxis.Humour, 0.8f);
                    }
                    break;
                case TalkOutcome.Comfort:
                    One(b, a, "Rimfolk_Talk_Comforted");
                    One(a, b, "Rimfolk_Talk_Comforter");
                    social?.Bump(a, b, TieKind.Friends, 3f, "CC_TieNote_TalkComfort".Translate().Resolve());
                    if (Interplay.On) Interplay.Ease(b, 3f, StrainRelief.Friends);
                    if (Rand.Chance(0.1f)) Persona.Nudge(a, PersonaAxis.Warmth, 0.8f);
                    if (Rand.Chance(0.1f)) Persona.Nudge(b, PersonaAxis.Trust, 0.8f);
                    break;
                case TalkOutcome.Reminisce:
                    Both(a, b, c.memory?.def?.valence < 0 ? "Rimfolk_Talk_RememberedLoss" : "Rimfolk_Talk_Remembered");
                    SharedMemories.Recalled(c.memory);
                    social?.Bump(a, b, TieKind.Friends, 1.5f);
                    break;
                case TalkOutcome.Joke:
                    Both(a, b, c.memory != null && TalkMath.Worn(c.memory.recalls) ? "Rimfolk_Talk_OldJoke" : "Rimfolk_Talk_InJoke");
                    SharedMemories.Recalled(c.memory);
                    if (Rand.Chance(0.1f)) Persona.Nudge(a, PersonaAxis.Humour, 0.8f);
                    if (Rand.Chance(0.1f)) Persona.Nudge(b, PersonaAxis.Humour, 0.8f);
                    break;
            }
            if (warm && c.outcome != TalkOutcome.Tease) MomentTones.RecordEvent(c.mend ? "mend" : "warm", a, b, 0f);
            if (c.mend)
            {
                long key = PersonaMath.PairKey(a.thingIDNumber, b.thingIDNumber);
                string what = TalkTracker.Instance != null && TalkTracker.Instance.lastArgued.TryGetValue(key, out var t) ? "CC_TalkLabel_RowAbout".Translate(t).Resolve() : "CC_TalkLabel_Row".Translate().Resolve();
                SharedMemories.Add(a, b, TalkDefs.Mended, what, 1f);
                social?.Bump(a, b, TieKind.Friends, 2f, "CC_TieNote_MadeUp".Translate().Resolve());
            }
            // A long friendship softens people, a little.
            if (warm && tie.friendStrength >= 60f)
            {
                if (Rand.Chance(0.1f)) Persona.Nudge(a, PersonaAxis.Warmth, 1f);
                if (Rand.Chance(0.1f)) Persona.Nudge(b, PersonaAxis.Warmth, 1f);
            }
        }

        /// <summary>The topic in a few words, with who it's about ("Mira, the newcomer"), or null.</summary>
        public static string About(TopicDef topic, TalkSubject subject) =>
            topic?.about == null ? null : topic.about.Formatted((subject?.name ?? "").Named("SUBJECT")).Resolve();

        /// <summary>A talk that leaves them warmer: agreeing, comfort, remembering, a joke, or a friendly debate between people who like each other.</summary>
        private static bool Warm(Conversation c, Tie tie) =>
            c.outcome == TalkOutcome.Agree || c.outcome == TalkOutcome.Comfort || c.outcome == TalkOutcome.Reminisce || c.outcome == TalkOutcome.Joke
            || (c.outcome == TalkOutcome.Debate && ((c.a.relations?.OpinionOf(c.b) ?? 0) >= 15 || tie.friends || tie.lovers));

        private static void Both(Pawn a, Pawn b, string thought)
        {
            One(a, b, thought);
            One(b, a, thought);
        }

        private static void One(Pawn p, Pawn about, string thought)
        {
            // together: the talk worker's effects; the two are face to face.
            var def = T(thought);
            if (def != null) CouncilGameComponent.GiveThought(p, def, about);
        }

        /// <summary>Both views moved by how the talk went (pull of a on b, pull of b on a), and noted for the character page.</summary>
        private static void Views(Conversation c, Func<float, float, float, float, (float, float)> move)
        {
            if (c.topic == null) return;
            var a = c.a;
            var b = c.b;
            var va = Persona.Values(a);
            var vb = Persona.Values(b);
            float pab = TalkMath.Pull(b.relations?.OpinionOf(a) ?? 0, vb[(int)PersonaAxis.Trust], va[(int)PersonaAxis.Pride], vb[(int)PersonaAxis.Openness], c.outcome);
            float pba = TalkMath.Pull(a.relations?.OpinionOf(b) ?? 0, va[(int)PersonaAxis.Trust], vb[(int)PersonaAxis.Pride], va[(int)PersonaAxis.Openness], c.outcome);
            var (na, nb) = move(c.stanceA, c.stanceB, pab, pba);
            string key = c.topic.Key(c.subject);
            Stances.Set(a, key, na, b, c.stanceA);
            Stances.Set(b, key, nb, a, c.stanceB);
        }

        // ---------------- The lines said ----------------

        /// <summary>Every 15 ticks while a talk is under way: the lines due are said, if the two are still together.</summary>
        public static void Step(TalkTracker tr, int now)
        {
            List<(int id, ExchangeQueue<Pawn>.Line line)> due;
            try
            {
                due = tr.exchanges.Due(now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("talk lines", e);
                tr.exchanges.Clear();
                return;
            }
            foreach (var (id, line) in due)
            {
                var speaker = line.speaker;
                var listener = line.listener;
                // They parted (or one was called away): the rest of it isn't said.
                if (!Together.Can(speaker, listener))
                {
                    tr.exchanges.Drop(id);
                    continue;
                }
                try
                {
                    Speech.Say(speaker, line.text, listener);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("talk line", e);
                }
            }
        }

        // ---------------- Views drifting ----------------

        /// <summary>Once a day: each view moves a little toward what the person's life now suggests (a good harvest, a bad decree).</summary>
        public static void DailyDrift()
        {
            if (!Persona.On) return;
            Build();
            foreach (var p in PawnsFinder.AllMaps_FreeColonists.ToList())
            {
                var r = Persona.Of(p);
                if (r == null || r.stances.Count == 0 || !Eligible(p)) continue;
                var scene = TalkScene.Of(p.MapHeld);
                foreach (var key in r.stances.Keys.ToList())
                {
                    try
                    {
                        Drift(p, r, key, scene);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("talk drift " + key, e);
                    }
                }
            }
        }

        private static void Drift(Pawn p, PawnPersona r, string key, TalkScene scene)
        {
            int colon = key.IndexOf(':');
            string name = colon >= 0 ? key.Substring(0, colon) : key;
            var topic = DefRefs.Named<TopicDef>(name);
            if (topic == null) return;
            TalkSubject subject = null;
            if (topic.perSubject)
            {
                int id = colon >= 0 && int.TryParse(key.Substring(colon + 1), out int x) ? x : -1;
                var who = scene?.colonists.FirstOrDefault(o => o.thingIDNumber == id);
                // Someone no longer new (or gone): the view about them is let go; their reputation is another matter.
                if (who == null || scene.NewcomerDays(who) >= 60f)
                {
                    r.stances.Remove(key);
                    return;
                }
                subject = TalkSubject.Of(who);
            }
            float seed = Stances.Seed(p, topic, subject, scene);
            r.stances[key] = TalkMath.Drift(r.stances[key], seed);
        }

        // ---------------- Read from outside ----------------

        /// <summary>The kinds of talk loaded (for the self-check and the Almanac).</summary>
        public static int KindCount
        {
            get
            {
                Build();
                return defs.Count;
            }
        }
    }

    /// <summary>Where people stand on topics: seeded from who they are the first time, kept with <see cref="Persona"/>.</summary>
    public static class Stances
    {
        /// <summary>Where someone stands on a topic (about a subject, for a view per person), seeding it the first time.</summary>
        public static float Get(Pawn p, TopicDef topic, TalkSubject subject, TalkScene scene)
        {
            string key = topic.Key(subject);
            if (Persona.HasStance(p, key)) return Persona.Stance(p, key);
            float seed = Seed(p, topic, subject, scene);
            if (Persona.Keeps(p)) Persona.SetStance(p, key, seed);
            return seed;
        }

        /// <summary>A view changed by a talk with <paramref name="with"/>, noted for the character page when it moved.</summary>
        public static void Set(Pawn p, string key, float value, Pawn with, float was)
        {
            if (!Persona.Keeps(p)) return;
            Persona.SetStance(p, key, value);
            if (with != null && Mathf.Abs(value - was) >= 0.02f)
                TalkTracker.Instance?.movedBy.SetOrAdd(p.thingIDNumber + "|" + key, (with.LabelShort, Find.TickManager.TicksGame));
        }

        /// <summary>A view moved by something that happened (a fight won, a death), for someone who already has one or gets one now.</summary>
        public static void Shift(Pawn p, TopicDef topic, float amount, TalkScene scene = null)
        {
            if (p == null || topic == null || topic.perSubject || !Persona.Keeps(p) || !Talk.Eligible(p)) return;
            float s = Get(p, topic, null, scene ?? TalkScene.Of(p.MapHeld));
            Persona.SetStance(p, topic.defName, TalkMath.Shift(s, amount));
        }

        /// <summary>
        /// The view someone's character, traits, faith and life now suggest: what a first view starts from, and where
        /// a view drifts toward day by day.
        /// </summary>
        public static float Seed(Pawn p, TopicDef topic, TalkSubject subject, TalkScene scene)
        {
            var v = Persona.Values(p);
            float extra = 0f;
            if (topic.leans != null)
                foreach (var l in topic.leans)
                    if (l != null && l.Fits(p))
                        extra += l.lean;
            extra += PersonaMath.Seed(p.thingIDNumber * 31 + topic.shortHash, PersonaAxis.Outlook, topic.spread * 100f) / 100f;
            try
            {
                extra += Mathf.Clamp(topic.Worker.Experience(p, subject, scene), -0.6f, 0.6f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("talk experience " + topic.defName, e);
            }
            return TalkMath.Seed(v, topic.SeedWeights, extra);
        }
    }
}
