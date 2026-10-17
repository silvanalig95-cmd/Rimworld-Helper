using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>How a conversation between two colonists goes (Social/Talk/).</summary>
    public enum TalkOutcome
    {
        /// <summary>They see it the same way: warm, a little closer.</summary>
        Agree,
        /// <summary>They don't, and enjoy arguing it out: friends, the warm, the humorous.</summary>
        Debate,
        /// <summary>They don't, and it gets heated: tempers, opposed views, little liking.</summary>
        Argue,
        /// <summary>A joker pulls the other's leg about it.</summary>
        Tease,
        /// <summary>The other is low or grieving, and the first sees it.</summary>
        Comfort,
        /// <summary>They bring up something they went through together.</summary>
        Reminisce,
        /// <summary>An in-joke of theirs.</summary>
        Joke
    }

    /// <summary>Which line of an exchange: the opening, the reply, the first speaker's closing word, the other's last word.</summary>
    public enum TalkTurn
    {
        Open,
        Reply,
        Close,
        Last
    }

    /// <summary>What decides how a talk goes, read from the two people (see <see cref="TalkMath.Outcome"/>).</summary>
    public struct OutcomeInput
    {
        /// <summary>Where each stands on the topic, -1..1.</summary>
        public float stanceA, stanceB;
        /// <summary>Their sides (-100..100): temper, warmth and humour of each.</summary>
        public float temperA, temperB, warmthA, warmthB, humourA, humourB;
        /// <summary>What each thinks of the other (-100..100).</summary>
        public int opinionAB, opinionBA;
        public bool friends, rivals, lovers;
        /// <summary>A spat between them not yet made up for, still sore.</summary>
        public bool sore;
        /// <summary>A warm moment between them in the last few hours.</summary>
        public bool warmLately;
        /// <summary>The "frictions" setting: without it, nobody argues.</summary>
        public bool frictionsOn;
        /// <summary>The second person is low or grieving (a tease would be unkind).</summary>
        public bool otherLow;
        /// <summary>A topic nobody jokes about (the dead).</summary>
        public bool noTease;
    }

    /// <summary>
    /// The arithmetic of conversations, with no game types so the tests can check it: how a topic is picked, how a
    /// talk goes from both stances and both characters, how far one person's words move another's view, how many
    /// lines an exchange has, and the voice each speaker uses. Social/Talk/Talk.cs reads the people and the game and
    /// hands the numbers here.
    /// </summary>
    public static class TalkMath
    {
        /// <summary>The most one talk can move a view (a share of the way to the other's).</summary>
        public const float MaxPull = 0.25f;
        /// <summary>A view leans one way from here ("for" or "against"); below it, someone is of two minds.</summary>
        public const float SideAt = 0.25f;
        /// <summary>A view held strongly from here.</summary>
        public const float StrongAt = 0.6f;
        /// <summary>Views seeded from someone's character stay inside this, so experience and talk can still move them.</summary>
        public const float SeedMax = 0.85f;
        /// <summary>A view drifts this share of the way toward what someone's life now suggests, each day.</summary>
        public const float DailyDrift = 0.04f;

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        public static float Clamp1(float v) => Clamp(v, -1f, 1f);

        // ---------------- Views ----------------

        /// <summary>"for", "against" or "mixed": the side of a view, for lines written for one side.</summary>
        public static string Side(float stance) => stance >= SideAt ? "for" : stance <= -SideAt ? "against" : "mixed";

        /// <summary>-1 against, 0 of two minds, 1 for.</summary>
        public static int SideOf(float stance) => stance >= SideAt ? 1 : stance <= -SideAt ? -1 : 0;

        /// <summary>How much someone cares about a topic, 0..1: the strength of their view.</summary>
        public static float Care(float stance) => Math.Min(1f, Math.Abs(stance));

        /// <summary>
        /// A first view on a topic from someone's character: each side of them (-100..100) times its weight (per 100), plus
        /// what their traits, passions and life add, kept inside <see cref="SeedMax"/>.
        /// </summary>
        public static float Seed(float[] sides, float[] weights, float extra)
        {
            float s = extra;
            if (sides != null && weights != null)
                for (int i = 0; i < sides.Length && i < weights.Length; i++)
                    s += sides[i] / 100f * weights[i] / 100f;
            return Clamp(s, -SeedMax, SeedMax);
        }

        /// <summary>A view moved toward what someone's life now suggests, a little each day.</summary>
        public static float Drift(float stance, float seed, float rate = DailyDrift) => Clamp1(stance + (seed - stance) * Clamp(rate, 0f, 1f));

        /// <summary>A view moved by something that happened (a won fight, a death), kept within -1..1.</summary>
        public static float Shift(float stance, float amount) => Clamp1(stance + amount);

        /// <summary>
        /// How far a speaker's words pull a listener's view toward their own (a share of the distance, 0..<see cref="MaxPull"/>):
        /// more when the listener likes the speaker and is trusting and open by nature, and the speaker is sure of
        /// themselves (proud); a friendly debate moves people most, an argument least.
        /// </summary>
        public static float Pull(int opinionOfSpeaker, float listenerTrust, float speakerPride, float listenerOpenness, TalkOutcome how)
        {
            float like = Clamp(0.5f + opinionOfSpeaker / 100f, 0.05f, 1.5f);
            float trust = 1f + Clamp(listenerTrust, -100f, 100f) / 200f;
            float pride = 1f + Clamp(speakerPride, -100f, 100f) / 250f;
            float open = 1f + Clamp(listenerOpenness, -100f, 100f) / 150f;
            float basePull;
            switch (how)
            {
                case TalkOutcome.Debate: basePull = 0.10f; break;
                case TalkOutcome.Agree: basePull = 0.06f; break;
                case TalkOutcome.Argue: basePull = 0.03f; break;
                default: basePull = 0.02f; break;
            }
            return Clamp(basePull * like * trust * pride * open, 0f, MaxPull);
        }

        /// <summary>Two who agree: each moves a little toward the other and both grow a little surer.</summary>
        public static float Confirmed(float stance, float other, float pull)
        {
            float s = PersonaMath.Persuaded(stance, other, pull);
            float sign = s >= 0f ? 1f : -1f;
            return Clamp1(s + sign * 0.04f * (1f - Math.Abs(s)));
        }

        /// <summary>An argument that only hardens a view: it moves a little away from the other's.</summary>
        public static float Hardened(float stance, float other, float amount = 0.04f)
        {
            float away = stance >= other ? 1f : -1f;
            return Clamp1(stance + away * Math.Abs(amount));
        }

        // ---------------- Picking a topic ----------------

        /// <summary>
        /// How likely a topic comes up between two people. <paramref name="relevance"/>: whether it means anything here
        /// now (0: it can't come up). <paramref name="hot"/>: something about it just happened (0..1). Both caring
        /// (strong views, the speaker's counting more), the speaker's character, their tie and having talked about it
        /// lately weigh in.
        /// </summary>
        public static float Salience(float relevance, float hot, float careA, float careB, float character, float tie, float recent)
        {
            if (relevance <= 0f) return 0f;
            float s = relevance * (1f + 2f * Clamp(hot, 0f, 1f)) * (0.5f + Clamp(careA, 0f, 1f)) * (0.75f + 0.5f * Clamp(careB, 0f, 1f));
            s *= Clamp(character, 0.2f, 3f) * Clamp(tie, 0.2f, 3f) * Clamp(recent, 0f, 1f);
            return s < 0f ? 0f : s;
        }

        /// <summary>The tie's say in a topic: friends talk about anything, rivals pick the things they disagree on.</summary>
        public static float TieFactor(bool friends, bool rivals, float stanceA, float stanceB)
        {
            float f = friends ? 1.15f : 1f;
            if (rivals) f *= 1f + Math.Abs(stanceA - stanceB) * 0.75f;
            return f;
        }

        /// <summary>A speaker's interest in a topic from their character: 1 plus each side times its weight (per 100 per 100).</summary>
        public static float Interest(float[] sides, float[] weights)
        {
            float f = 1f;
            if (sides != null && weights != null)
                for (int i = 0; i < sides.Length && i < weights.Length; i++)
                    f += sides[i] / 100f * weights[i] / 100f;
            return Clamp(f, 0.4f, 2f);
        }

        /// <summary>The index picked from <paramref name="weights"/> by a roll in 0..1, or -1 if none weighs anything.</summary>
        public static int PickIndex(IList<float> weights, float roll)
        {
            if (weights == null) return -1;
            float total = 0f;
            foreach (var w in weights)
                if (w > 0f) total += w;
            if (total <= 0f) return -1;
            float r = Clamp(roll, 0f, 0.99999f) * total;
            int last = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f) continue;
                last = i;
                r -= weights[i];
                if (r < 0f) return i;
            }
            return last;
        }

        /// <summary>
        /// Each topic's share of a pair's talk: the budget split by salience, so more to talk about doesn't mean more talk.
        /// </summary>
        public static float[] Shares(IList<float> salience, float budget)
        {
            var shares = new float[salience?.Count ?? 0];
            if (salience == null || budget <= 0f) return shares;
            float total = 0f;
            foreach (var s in salience)
                if (s > 0f) total += s;
            if (total <= 0f) return shares;
            for (int i = 0; i < shares.Length; i++)
                shares[i] = salience[i] > 0f ? budget * salience[i] / total : 0f;
            return shares;
        }

        /// <summary>How often someone starts a talk at all, from their character: the warm and the humorous more, the cold and dour less.</summary>
        public static float Talkative(float warmth, float humour) => Clamp(1f + (warmth + humour) / 400f, 0.6f, 1.5f);

        // ---------------- How it goes ----------------

        /// <summary>A joker's chance to pull someone's leg, from their humour (0 below 30).</summary>
        public static float TeaseChance(float humour, bool friends)
        {
            if (humour < 30f) return 0f;
            return Clamp(0.15f + (humour - 30f) / 200f + (friends ? 0.1f : 0f), 0f, 0.6f);
        }

        /// <summary>They see it the same way: close views on the same side, or both near the middle.</summary>
        public static bool Agreeing(float a, float b)
        {
            float gap = Math.Abs(a - b);
            return gap < 0.3f || (a * b >= 0f && gap < 0.5f);
        }

        /// <summary>
        /// How heated a disagreement gets: the gap between the views, both tempers, against warmth, liking, friendship,
        /// humour, and how things stand between them lately. Above <see cref="ArgueAt"/> it's an argument.
        /// </summary>
        public static float Heat(in OutcomeInput x)
        {
            float h = 0.35f * Math.Abs(x.stanceA - x.stanceB);
            h += (x.temperA + x.temperB) / 400f;
            h -= (x.warmthA + x.warmthB) / 600f;
            h -= (x.opinionAB + x.opinionBA) / 400f;
            h -= (x.humourA + x.humourB) / 800f;
            if (x.rivals) h += 0.25f;
            if (x.friends) h -= 0.2f;
            if (x.lovers) h -= 0.15f;
            if (x.sore) h += 0.25f;
            if (x.warmLately) h -= 0.15f;
            return h;
        }

        public const float ArgueAt = 0.35f;

        /// <summary>
        /// How a talk on a topic goes. <paramref name="roll"/> (0..1) nudges a disagreement's heat a little either way;
        /// <paramref name="teaseRoll"/> (0..1) decides whether a joker teases.
        /// </summary>
        public static TalkOutcome Outcome(in OutcomeInput x, float roll, float teaseRoll)
        {
            if (!x.sore && !x.otherLow && !x.noTease && x.opinionAB >= 5 && teaseRoll < TeaseChance(x.humourA, x.friends)) return TalkOutcome.Tease;
            if (Agreeing(x.stanceA, x.stanceB)) return TalkOutcome.Agree;
            float heat = Heat(x) + (Clamp(roll, 0f, 1f) - 0.5f) * 0.3f;
            return x.frictionsOn && heat > ArgueAt ? TalkOutcome.Argue : TalkOutcome.Debate;
        }

        /// <summary>How many lines an exchange has (2 to 4).</summary>
        public static int Turns(TalkOutcome how, float roll)
        {
            roll = Clamp(roll, 0f, 1f);
            switch (how)
            {
                case TalkOutcome.Agree: return roll < 0.4f ? 2 : 3;
                case TalkOutcome.Debate: return roll < 0.5f ? 3 : 4;
                case TalkOutcome.Argue: return roll < 0.4f ? 3 : 4;
                case TalkOutcome.Tease: return roll < 0.35f ? 2 : 3;
                case TalkOutcome.Joke: return roll < 0.5f ? 2 : 3;
                case TalkOutcome.Reminisce: return roll < 0.7f ? 3 : 4;
                default: return 3;
            }
        }

        /// <summary>Which turn the n-th line (0-based) is, and who says it: the first speaker opens and closes.</summary>
        public static TalkTurn TurnAt(int index) => index <= 0 ? TalkTurn.Open : index == 1 ? TalkTurn.Reply : index == 2 ? TalkTurn.Close : TalkTurn.Last;

        public static bool ByFirst(int index) => index % 2 == 0;

        // Tones as numbers, in the order of the Tone enum (Tone.cs): Plain, Warm, Blunt, Cheerful, Gloomy, Cold.
        public const int Plain = 0, Warm = 1, Blunt = 2, Cheerful = 3, Gloomy = 4, Cold = 5;

        /// <summary>
        /// The voice a speaker uses in this talk: their usual tone (from traits and character), sharpened by the moment.
        /// A hothead snaps in an argument, the cold go curt, the bleak see the worst; a joker teasing jokes; the warm comfort warmly.
        /// </summary>
        public static int Voice(int usual, float temper, float warmth, float humour, float outlook, TalkOutcome how, bool first)
        {
            switch (how)
            {
                case TalkOutcome.Argue:
                    if (temper >= 40f) return Blunt;
                    if (warmth <= -45f) return Cold;
                    if (outlook <= -40f) return Gloomy;
                    if (usual == Cheerful && humour < 50f) return Plain;
                    return usual;
                case TalkOutcome.Tease:
                    if (first && humour >= 30f) return Cheerful;
                    if (!first && temper >= 40f) return Blunt;
                    return usual;
                case TalkOutcome.Comfort:
                    if (first && warmth >= 30f) return Warm;
                    if (!first && outlook <= -30f) return Gloomy;
                    return usual;
                case TalkOutcome.Joke:
                    return humour >= 30f ? Cheerful : usual;
                default:
                    if (humour >= 55f && usual == Plain) return Cheerful;
                    if (outlook <= -50f && usual == Plain) return Gloomy;
                    if (warmth <= -60f && usual == Plain) return Cold;
                    return usual;
            }
        }

        /// <summary>Whether a joke has been told too often: it wears out after a few tellings.</summary>
        public static bool Worn(int recalls) => recalls > 4;
    }
}
