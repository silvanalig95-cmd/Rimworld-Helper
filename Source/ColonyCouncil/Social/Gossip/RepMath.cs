using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The sides of a person's name in the colony: what people say about them, from what they have heard they did. Each
    /// runs from -<see cref="RepMath.Cap"/> to +<see cref="RepMath.Cap"/>; the sign says which way.
    /// </summary>
    public enum RepFacet
    {
        /// <summary>A coward (-) to brave (+).</summary>
        Courage,
        /// <summary>Cruel (-) to kind (+).</summary>
        Kindness,
        /// <summary>Idle (-) to someone you can count on (+).</summary>
        Diligence,
        /// <summary>Clumsy (-) to skilled (+).</summary>
        Skill,
        /// <summary>A gossip who can't keep a thing to themselves (-) to trustworthy (+).</summary>
        Discretion,
        /// <summary>Level-headed (-) to a hothead (+).</summary>
        Temper
    }

    /// <summary>How a teller colours a story.</summary>
    public enum Slant : byte
    {
        /// <summary>As they heard it, give or take.</summary>
        Plain,
        /// <summary>In the subject's favour: the good talked up, the bad played down or excused.</summary>
        Praise,
        /// <summary>Against the subject: the good played down, the bad made worse.</summary>
        Scorn,
        /// <summary>Turned into a joke: the edge taken off, and believed a little less.</summary>
        Joke,
        /// <summary>Their own good deed, told by themselves.</summary>
        Boast
    }

    /// <summary>
    /// The arithmetic of gossip and reputation, with no game types, so the tests can check it: how a teller's opinion and
    /// character colour a story, how far a rumour may grow, how much a listener believes and how much it weighs with them,
    /// how deeds fade, and what a name adds up to. See Social/Gossip/ for the parts that use it.
    /// </summary>
    public static class RepMath
    {
        public const int Facets = 6;

        public static readonly RepFacet[] All =
        {
            RepFacet.Courage, RepFacet.Kindness, RepFacet.Diligence, RepFacet.Skill, RepFacet.Discretion, RepFacet.Temper
        };

        /// <summary>A side of someone's name shows from here (a word, a tag) and is strong from <see cref="Strong"/>.</summary>
        public const float Shows = 6f, Strong = 18f;

        /// <summary>No side of what one person makes of another, or of a name, goes past this.</summary>
        public const float Cap = 40f;

        /// <summary>A retold story stays within these shares of what really happened: rumours grow, but never into fiction.</summary>
        public const float MaxGrowth = 1.6f, MinShrink = 0.4f;

        /// <summary>Deeds kept per person, in all, and people who know of one deed.</summary>
        public const int MaxDeedsPerSubject = 10, MaxDeeds = 220, MaxKnowers = 30;

        /// <summary>A deed fainter than this (its size, faded) is let go.</summary>
        public const float Forgotten = 0.08f;

        /// <summary>A merged deed (the same thing again, soon after) never grows past this size.</summary>
        public const float MaxSize = 3f;

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        public static float CapFacet(float v) => Clamp(v, -Cap, Cap);

        /// <summary>-2 strongly against, -1 leaning against, 0 nothing said, 1 leaning for, 2 strongly for.</summary>
        public static int Degree(float v) => v <= -Strong ? -2 : v <= -Shows ? -1 : v >= Strong ? 2 : v >= Shows ? 1 : 0;

        // ---------------- Deeds ----------------

        /// <summary>What's left of a deed's weight after <paramref name="days"/> days: it halves every <paramref name="halfLife"/> days.</summary>
        public static float Faded(float weight, float days, float halfLife)
        {
            if (days <= 0f || halfLife <= 0f) return weight;
            return weight * (float)Math.Pow(0.5, days / halfLife);
        }

        /// <summary>The same deed again soon after (another hour at the wounded, another blow in the same fit): it grows, to a point.</summary>
        public static float Merged(float size, float added, float share) => Math.Min(MaxSize, size + Math.Max(0f, added) * Clamp(share, 0f, 1f));

        /// <summary>
        /// The worth of each side of a name to those who hear of it, in <see cref="RepFacet"/> order: kindness most, then
        /// courage, a loose tongue, a steady hand at work and skill; a temper counts against.
        /// </summary>
        public static readonly float[] Worth = { 1f, 1.3f, 0.6f, 0.5f, 0.8f, -0.7f };

        /// <summary>Good (+1), bad (-1) or neither (0), from a deed's shifts in <see cref="RepFacet"/> order.</summary>
        public static int Valence(float[] shifts)
        {
            if (shifts == null) return 0;
            float sum = 0f;
            for (int i = 0; i < Facets && i < shifts.Length; i++) sum += shifts[i] * Worth[i];
            return sum >= 1f ? 1 : sum <= -1f ? -1 : 0;
        }

        // ---------------- Telling ----------------

        /// <summary>
        /// How a teller colours a deed. Their own good deed, told by someone proud, is a boast. A born joker turns a story
        /// into a joke now and then (more often the stronger the humour, <paramref name="roll"/> 0..1 decides), but never
        /// a <paramref name="grave"/> one (someone hurt, a cruelty). Otherwise it follows what they think of the one it's
        /// about: a friend speaks up for them, a rival against them, and anyone else tells it plain.
        /// </summary>
        public static Slant SlantFor(int valence, float opinion, bool friend, bool rival, float humour, bool own, float roll, bool grave = false)
        {
            if (own) return Slant.Boast;
            if (!grave && roll < JokeChance(humour)) return Slant.Joke;
            float lean = opinion / 100f + (friend ? 0.4f : 0f) - (rival ? 0.5f : 0f);
            if (lean >= 0.25f) return Slant.Praise;
            if (lean <= -0.2f) return Slant.Scorn;
            return Slant.Plain;
        }

        /// <summary>The chance a story comes out as a joke: none below humour 25, half at the very top.</summary>
        public static float JokeChance(float humour) => humour <= 25f ? 0f : Clamp((humour - 25f) / 150f, 0f, 0.5f);

        /// <summary>Grave: someone was hurt, or it was cruel (kindness -8 or worse at size 1). Nobody jokes about those.</summary>
        public static bool Grave(float[] shifts) => shifts != null && shifts.Length > (int)RepFacet.Kindness && shifts[(int)RepFacet.Kindness] <= -8f;

        /// <summary>A proud colonist may tell of their own good deed; nobody boasts of a bad one.</summary>
        public static bool MayBoast(float pride, int valence) => valence > 0 && pride >= 30f;

        /// <summary>Someone only tells a story with its subject standing right there if they're a hothead or the subject's rival.</summary>
        public static bool MayTellInFront(float temper, bool rival) => rival || temper >= 40f;

        /// <summary>
        /// The story as this teller passes it on (<paramref name="roll"/> 0..1): talked up or down by their slant, by a
        /// little each telling, and never past what could have been (<see cref="MaxGrowth"/> and <see cref="MinShrink"/>
        /// of the <paramref name="truth"/>). A story is never turned around: the bad stays bad, the good good.
        /// </summary>
        public static float Retold(float version, float truth, Slant slant, int valence, float roll)
        {
            roll = Clamp(roll, 0f, 1f);
            bool good = valence >= 0;
            float f;
            switch (slant)
            {
                case Slant.Praise: f = good ? 1.1f + 0.15f * roll : 0.7f + 0.15f * roll; break;
                case Slant.Boast: f = 1.15f + 0.2f * roll; break;
                case Slant.Scorn: f = good ? 0.7f + 0.15f * roll : 1.1f + 0.18f * roll; break;
                case Slant.Joke: f = 0.85f + 0.1f * roll; break;
                default: f = 0.95f + 0.12f * roll; break;
            }
            if (truth <= 0f) return 0f;
            return Clamp(version * f, truth * MinShrink, truth * MaxGrowth);
        }

        /// <summary>
        /// How far a listener believes what they're told, 0.2..1. The wary believe the bad more readily and the trusting
        /// the good; a story against what they already think of its subject is believed less, and so is one from someone
        /// they don't like; a boast and a joke less again. (A witness believes their own eyes: they're never asked.)
        /// </summary>
        public static float Belief(int valence, float listenerTrust, float opinionOfSubject, float opinionOfTeller, Slant slant)
        {
            float b = 0.75f;
            if (valence < 0) b -= listenerTrust / 250f;
            else if (valence > 0) b += listenerTrust / 400f;
            b += (valence >= 0 ? opinionOfSubject : -opinionOfSubject) / 300f;
            b += opinionOfTeller / 400f;
            if (slant == Slant.Boast) b -= 0.25f;
            else if (slant == Slant.Joke) b -= 0.1f;
            return Clamp(b, 0.2f, 1f);
        }

        /// <summary>
        /// How much one side of a deed weighs with a listener, by their own character (<paramref name="persona"/> in
        /// <see cref="PersonaAxis"/> order, -100..100): the bold admire courage and scorn cowardice more, the warm prize
        /// kindness and condemn cruelty more (cruelty most), the wary mind a loose tongue, the even-tempered a temper (a
        /// hothead shrugs it off), the proud and curious notice skill, and those set in their ways value a steady worker.
        /// Never under half, never over double.
        /// </summary>
        public static float Weigh(RepFacet facet, float shift, float[] persona)
        {
            float P(PersonaAxis a) => persona != null && (int)a < persona.Length ? persona[(int)a] : 0f;
            float f = 1f;
            switch (facet)
            {
                case RepFacet.Courage: f += P(PersonaAxis.Nerve) / 160f; break;
                case RepFacet.Kindness: f += P(PersonaAxis.Warmth) / (shift < 0f ? 110f : 200f); break;
                case RepFacet.Diligence: f -= P(PersonaAxis.Openness) / 250f; break;
                case RepFacet.Skill: f += (P(PersonaAxis.Pride) + P(PersonaAxis.Openness)) / 300f; break;
                case RepFacet.Discretion: f -= P(PersonaAxis.Trust) / 180f; break;
                case RepFacet.Temper: f -= P(PersonaAxis.Temper) / 160f; break;
            }
            return Clamp(f, 0.5f, 2f);
        }

        /// <summary>One deed's part in what a listener makes of its doer, on one side.</summary>
        public static float Part(float shift, float version, float belief, float fade, float weigh) => shift * version * belief * fade * weigh;

        // ---------------- A name ----------------

        /// <summary>
        /// The colony's view of someone on one side: what each of the others makes of them, added up and shared out over
        /// all of them (those who've heard nothing count as nothing), so a name needs the story to have got round.
        /// </summary>
        public static float Standing(float sum, int others) => others <= 0 ? 0f : CapFacet(sum / others);

        /// <summary>What a person's sides add up to in one listener's eyes (see <see cref="Worth"/>).</summary>
        public static float Esteem(float[] impression)
        {
            if (impression == null) return 0f;
            float e = 0f;
            for (int i = 0; i < Facets && i < impression.Length; i++) e += impression[i] * Worth[i];
            return e;
        }

        /// <summary>
        /// The stage of the opinion a listener holds from what they've heard (0 the worst .. 5 the best), or -1 for none:
        /// small either way, and the bad sooner felt than the good.
        /// </summary>
        public static int OpinionStage(float esteem) =>
            esteem <= -26f ? 0 : esteem <= -13f ? 1 : esteem <= -5f ? 2 : esteem >= 30f ? 5 : esteem >= 15f ? 4 : esteem >= 7f ? 3 : -1;

        // ---------------- What a name does ----------------

        /// <summary>Strain eased in a fight beside someone known as brave, by their standing on courage (0 below <see cref="Shows"/>).</summary>
        public static float BraveRelief(float courage) => courage < Shows ? 0f : courage >= Strong ? 2f : 1.2f;

        /// <summary>A factor on a secret being confided to someone with this name for keeping things to themselves.</summary>
        public static float ConfideFactor(float discretion) =>
            discretion <= -Strong ? 0.25f : discretion <= -Shows ? 0.5f : discretion >= Strong ? 1.25f : 1f;

        /// <summary>A small lean at the ballot from what the voter has heard of the candidate (their esteem), within ±2.</summary>
        public static float VoteLean(float esteem) => Clamp(esteem / 12f, -2f, 2f);

        /// <summary>A strong name on the side a colony name (an epithet) is about lowers the bar for it a little.</summary>
        public static float EpithetEase(float standing) => standing >= Strong ? 0.85f : standing >= Shows ? 0.93f : 1f;

        /// <summary>
        /// How likely a colonist is to pass a story on in a conversation (next to chitchat at 1): fresh news more than old
        /// (old news is still new to a newcomer), the juicier the more, more from the talkative and the warm and less from
        /// the cold, more about someone they feel strongly about, and much more to a newcomer still learning who's who.
        /// </summary>
        public static float TellWeight(float interest, float ageDays, bool newcomer, float humour, float warmth, float opinionOfSubject)
        {
            float fresh = newcomer ? 1f : Math.Max(0.25f, (float)Math.Pow(0.5, Math.Max(0f, ageDays) / 20.0));
            float talk = Clamp(1f + Math.Max(0f, humour) / 150f + warmth / 300f, 0.5f, 2f);
            float feeling = 1f + Math.Abs(opinionOfSubject) / 120f;
            return 0.05f * Math.Max(0f, interest) * fresh * talk * feeling * (newcomer ? 2.5f : 1f);
        }
    }
}
