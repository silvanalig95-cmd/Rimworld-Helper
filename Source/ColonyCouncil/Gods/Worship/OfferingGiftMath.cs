using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What the colony knows of a kind of thing it might give a god, for whether it is precious to it (<see cref="OfferingGiftMath.Precious"/>).</summary>
    public struct PreciousFacts
    {
        /// <summary>One of the things the player named: components, advanced components, plasteel, uranium, gold, jade, neutroamine.</summary>
        public bool named;
        /// <summary>A part for machines: a manufactured piece worth <see cref="OfferingGiftMath.PartValue"/> or more (a mod's components too).</summary>
        public bool part;
        public bool medicine;
        /// <summary>Its medical potency (herbal medicine 0.6, the game's industrial medicine 1).</summary>
        public float potency;
        /// <summary>Glitterworld goods: traded as exotic, or of the spacer age or later.</summary>
        public bool exotic;
        /// <summary>A material other things are made of.</summary>
        public bool stuff;
        /// <summary>What one piece of it is worth.</summary>
        public float unitValue;
        /// <summary>The colony can make it now (a recipe it has the research and the bench for) or gather it again (food, wood, hides, stone, a plant or a rock on its land).</summary>
        public bool renewable;
    }

    /// <summary>
    /// The pure rules of gifts worth what they cost (the player, 16 November 2026: "i just wonder whether these offering
    /// ressources are "wasted", or if there is always some benefit from them"; "sometimes a return in kind sounds great";
    /// "it should please never take very valuable items like components etc. as offering, or only if there is a proper
    /// payoff for it. but then it shouldn't take it automatically, but perhaps an event where a pawn asks for approval
    /// first"). What is precious to the colony and never carried off unasked; what a pawn may ask the colony to give, how
    /// much and how often; how strongly a gift leans what follows its taking; and the return in kind, its chance, its wait
    /// and its worth. Free of game types, so Tools/Tests checks them.
    /// </summary>
    public static class OfferingGiftMath
    {
        // ================= Precious =================

        /// <summary>Herbal medicine's potency: medicine past it is precious.</summary>
        public const float HerbalPotency = 0.6f;

        /// <summary>A manufactured piece worth this much or more is a part for machines (a component, a mod's like of one).</summary>
        public const float PartValue = 25f;

        /// <summary>A material worth more than this a piece is precious (gold, plasteel, uranium, devilstrand, hyperweave, a mod's rare metal).</summary>
        public const float PreciousStuffValue = 5f;

        /// <summary>
        /// Precious to the colony: one of the things the player named, a part for machines, medicine past herbal, glitterworld
        /// goods, a costly material, or anything the colony can't make now or gather again. Never carried to a god unasked.
        /// </summary>
        public static bool Precious(PreciousFacts f) =>
            f.named || f.part || f.medicine && f.potency > HerbalPotency + 0.05f || f.exotic || f.stuff && f.unitValue > PreciousStuffValue || !f.renewable;

        /// <summary>Of what is not precious, a pawn carries it unasked only from plenty: three times the gift, and the colony's own store of it besides.</summary>
        public const int PlentyTimes = 3;

        public static bool Plenty(int held, int count, int keep) => count > 0 && held >= Math.Max(PlentyTimes * count, keep);

        // ================= Asked first =================

        /// <summary>
        /// A precious gift is asked of the colony at most once in <see cref="AskGapDays"/> at a home, one god's once in
        /// <see cref="AskGodGapDays"/>, the same thing to the same god not again for <see cref="NoAgainDays"/> after the colony
        /// said no; then about one day in <see cref="AskChance"/>'s worth, from someone devout. The card waits
        /// <see cref="AskWaitDays"/>.
        /// </summary>
        public const float AskGapDays = 12f, AskGodGapDays = 20f, NoAgainDays = 30f, AskChance = 0.25f, AskWaitDays = 2f;

        public static bool AskNow(float sinceAsk, float sinceGodAsk, float sinceNo, float roll) =>
            sinceAsk >= AskGapDays && sinceGodAsk >= AskGodGapDays && sinceNo >= NoAgainDays && roll < AskChance;

        /// <summary>What a precious gift aims at: four everyday gifts' worth, between these two.</summary>
        public const float AskWorthMin = 120f, AskWorthMax = 1000f;

        /// <summary>One piece worth more than this is never asked for: a fortune, not a gift.</summary>
        public const float AskUnitMax = 1500f;

        public static float AskWorth(int colonists) => Math.Max(AskWorthMin, Math.Min(AskWorthMax, 4f * WorshipMath.OfferingValueTarget(colonists)));

        /// <summary>How many to ask to give: the aim's worth in pieces, at least one, never more than a third of what the colony holds (none from fewer than three).</summary>
        public static int AskCount(float unitValue, int held, int colonists)
        {
            if (float.IsNaN(unitValue) || unitValue <= 0f || unitValue > AskUnitMax || held < 3) return 0;
            int n = (int)Math.Round(AskWorth(colonists) / unitValue);
            return Math.Max(1, Math.Min(n, held / 3));
        }

        /// <summary>The colony can spare it when asked: twice the gift left after it, and its own store of it besides.</summary>
        public static bool Askable(int held, int count, int keep) => count > 0 && held - count >= Math.Max(keep, 2 * count);

        /// <summary>A precious gift's favour, against an everyday one of the same worth: half again.</summary>
        public const float PreciousFavour = 1.5f;

        /// <summary>A gift asked for waits this many hours for its giver to carry it (an everyday one waits Offerings.PlanHours).</summary>
        public const int PreciousPlanHours = 48;

        // ================= What follows a taking =================

        /// <summary>
        /// How strongly a gift's worth and fit lean what follows its taking (GodTouchMath.TierFor): 1 for an everyday gift of
        /// about forty silver's worth that the god likes; less for a token or one it only takes; more the richer and the
        /// better liked; a precious gift asked of the colony first half again and at least 2. Between a half and three.
        /// </summary>
        public static float Lean(float worth, float fit, bool precious)
        {
            if (float.IsNaN(worth) || float.IsNaN(fit)) return 1f;
            float w = (float)Math.Sqrt(Math.Max(1f, worth) / 40f);
            float f = Math.Max(0.4f, Math.Min(1.8f, fit));
            float lean = w * (0.5f + 0.5f * f);
            if (precious) lean = Math.Max(2f, lean * 1.5f);
            return Math.Max(0.5f, Math.Min(3f, lean));
        }

        // ================= A return in kind =================

        /// <summary>
        /// The chance a taking is answered in kind (something of the god's own left where the gift lay): a little for an
        /// everyday gift, more the stronger its lean; about half for a precious one.
        /// </summary>
        public static float ReturnChance(float lean, bool precious)
        {
            float l = float.IsNaN(lean) ? 1f : Math.Max(0f, lean);
            return precious ? Math.Min(0.65f, 0.4f + 0.08f * l) : Math.Min(0.3f, 0.09f * l);
        }

        /// <summary>A god answers in kind once in this many days at a home at most, and any god once in this many; a precious gift whenever.</summary>
        public const float ReturnGodDays = 10f, ReturnAnyDays = 3f;

        public static bool ReturnAllowed(bool precious, float sinceGod, float sinceAny) => precious || sinceGod >= ReturnGodDays && sinceAny >= ReturnAnyDays;

        /// <summary>What comes back is worth at least this, so it is something to find.</summary>
        public const float ReturnWorthMin = 15f;

        /// <summary>
        /// What comes back is worth about what was given: three quarters to a fifth more for an everyday gift, as much to half
        /// again for a precious one (<paramref name="roll"/> in [0, 1)).
        /// </summary>
        public static float ReturnWorth(float given, bool precious, float roll)
        {
            float r = WorshipMath.Clamp01(roll);
            float f = precious ? 1f + 0.5f * r : 0.75f + 0.45f * r;
            return Math.Max(ReturnWorthMin, Math.Max(0f, float.IsNaN(given) ? 0f : given) * f);
        }

        /// <summary>How many pieces make that worth: at least one, at most a stack.</summary>
        public static int ReturnCount(float worth, float unitValue, int stackLimit)
        {
            if (float.IsNaN(unitValue) || unitValue <= 0f) return 0;
            int n = (int)Math.Round(Math.Max(0f, worth) / unitValue);
            return Math.Max(1, Math.Min(Math.Max(1, stackLimit), n));
        }

        /// <summary>
        /// Which of the god's goods comes back (their pieces' worth and weights, in order): by weight, among those a single
        /// piece of which is worth no more than the return; -1 when none is.
        /// </summary>
        public static int PickReturn(IList<float> unitValues, IList<float> weights, float worth, float roll)
        {
            if (unitValues == null || weights == null) return -1;
            int n = Math.Min(unitValues.Count, weights.Count);
            float total = 0f;
            for (int i = 0; i < n; i++)
                if (Fits(unitValues[i], weights[i], worth))
                    total += weights[i];
            if (total <= 0f) return -1;
            float r = WorshipMath.Clamp01(roll) * total;
            int last = -1;
            for (int i = 0; i < n; i++)
            {
                if (!Fits(unitValues[i], weights[i], worth)) continue;
                last = i;
                if (r < weights[i]) return i;
                r -= weights[i];
            }
            return last;
        }

        private static bool Fits(float unit, float weight, float worth) => weight > 0f && unit > 0f && !float.IsNaN(unit) && unit <= worth;

        // ================= The book =================

        /// <summary>The colony's book keeps this many gifts at most, and this many a god (the oldest let go).</summary>
        public const int BookGifts = 300, BookGiftsAGod = 40;
    }
}
