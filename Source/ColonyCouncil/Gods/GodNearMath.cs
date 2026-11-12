using System;

namespace ColonyCouncil
{
    /// <summary>
    /// What draws a god nearer the colony on its land (a god's shape names its own: GodShapeDef.nearWhen), each read off the
    /// map as how strongly it holds now (Gods/GodNear.cs).
    /// </summary>
    public enum GodStir
    {
        /// <summary>No rain for many days (Murmur).</summary>
        Drought,
        /// <summary>The colony mining deep under the mountain, and the rock rumbling over it (Smoulder).</summary>
        DeepMine,
        /// <summary>A hard winter: bitter cold out of doors, or a cold snap (Hush).</summary>
        HardWinter,
        /// <summary>A storm overhead, or a flashstorm (Cirrus).</summary>
        Storm,
        /// <summary>The moon full, or near it (Selene).</summary>
        FullMoon,
        /// <summary>A night with no moon (Undertow).</summary>
        DarkMoon,
        /// <summary>A heat wave, a solar flare, a scorching day (Helios, Smoulder).</summary>
        Heat,
        /// <summary>An anima tree on the land, and the land in full growth (Hollin).</summary>
        Growth,
        /// <summary>The dead left lying (Morel).</summary>
        Death,
        /// <summary>Sickness in the colony (Morel).</summary>
        Sickness,
        /// <summary>Guests and travellers on the land (Ennu).</summary>
        Guests,
        /// <summary>Many machines, or one broken down (Pip).</summary>
        Machines,
        /// <summary>A great hoard of silver (Ducat).</summary>
        Hoard,
        /// <summary>A trader on the land or in reach of the comms (Ducat).</summary>
        Trade,
        /// <summary>A thrumbo or a great herd on the land (Bellwether).</summary>
        Herd,
        /// <summary>Cats in the colony (Mog).</summary>
        Cats,
        /// <summary>Someone of the colony dying or breaking, or the stores empty (Undertow).</summary>
        Desperate
    }

    /// <summary>
    /// How near each god is to the colony (the player, 12 November 2026: "let's stick to one level and use felt as primary
    /// reference. i just want the 'walking'-exclusive options also to be able to appear ... some gods might become more
    /// present depending on situations, relations, actions etc."). The gods are felt: they act, but never so plainly that
    /// nobody could doubt it. A god drawn near acts more plainly, on its own: one can walk plainly while another stays
    /// hidden. Its nearness (0 felt, 1 as near as a god comes) moves with three things, each hidden state never shown:
    /// <list type="bullet">
    /// <item>the situation on the land (<see cref="GodStir"/>: a drought for Murmur, a deep mine for Smoulder, a hard winter
    /// for Hush), the most of it;</item>
    /// <item>its relations: a quarrel it is in, and the gods tied to it stirred (an ally drawn near draws it, a rival stirred
    /// wakes it);</item>
    /// <item>what the colony does: offerings to it, harm to what it holds sacred, how much the colony's book has of it lately,
    /// and a standing strong either way.</item>
    /// </list>
    /// At nothing it is exactly as the gods have always been felt; nearer, what only gods walking openly did can happen: a
    /// god seen plainly (and so named), its true form, a stronger answer, an offering gone in a breath of light, its doings
    /// in the world a little more often. No game types here, so Tools/Tests check them.
    /// </summary>
    public static class GodNearMath
    {
        /// <summary>The share of nearness the situation can give, its relations, and what the colony does.</summary>
        public const float SituationShare = 0.6f, QuarrelNear = 0.2f, TiedShare = 0.15f, ColonyMost = 0.4f;

        /// <summary>
        /// A god's nearness (0 to 1) from how strongly its situation holds (0 to 1), its relations (<see cref="Relations"/>) and
        /// what the colony does (<see cref="Colony"/>).
        /// </summary>
        public static float Nearness(float situation, float relations, float colony) =>
            Clamp01(SituationShare * Clamp01(situation) + Math.Max(0f, Nan0(relations)) + Math.Min(ColonyMost, Math.Max(0f, Nan0(colony))));

        /// <summary>Its relations' share: a quarrel it is in now, and the most stirred of the gods tied to it (either way).</summary>
        public static float Relations(bool inQuarrel, float tiedMostStirred) => (inQuarrel ? QuarrelNear : 0f) + TiedShare * Clamp01(tiedMostStirred);

        /// <summary>
        /// What the colony does, as nearness: offerings to it lately, harm lately to what it holds sacred (or offences against
        /// it), entries of it in the colony's book lately, and a standing strong either way (from favoured or displeased).
        /// Capped at <see cref="ColonyMost"/>.
        /// </summary>
        public static float Colony(int offerings, int harms, int bookEntries, float favour)
        {
            float c = 0.04f * Math.Min(4, Math.Max(0, offerings)) + 0.08f * Math.Min(3, Math.Max(0, harms)) + 0.02f * Math.Min(6, Math.Max(0, bookEntries));
            float strong = Math.Abs(Nan0(favour));
            if (strong > 50f) c += 0.1f * Clamp01((strong - 50f) / 50f);
            return Math.Min(ColonyMost, c);
        }

        /// <summary>The days counted as lately: offerings, harm, and the colony's book.</summary>
        public const int OfferingDays = 10, HarmDays = 15, BookDays = 30;

        /// <summary>An offence against a god counts as harm from a change of favour this large.</summary>
        public const float HarmFrom = -3f;

        // ================= What nearness does =================

        /// <summary>How far from felt toward walking a god's acts are (0 to 1): nothing below a fifth, all of it from four fifths.</summary>
        public static float Strength(float nearness) => Clamp01((Nan0(nearness) - 0.2f) / 0.6f);

        /// <summary>
        /// The chance a moment shows the god plainly (its card named, as the colony is sure; a stranger revealed openly; its true
        /// form; an offering gone in light; rain with thunder within the hour): none below two fifths, sure from four fifths.
        /// </summary>
        public static float PlainChance(float nearness) => Clamp01((Nan0(nearness) - 0.4f) / 0.4f);

        public static bool Plain(float nearness, float roll) => !float.IsNaN(roll) && roll < PlainChance(nearness);

        /// <summary>A god drawn this near is spoken of as near (lines tagged near), from where it may show plainly.</summary>
        public static bool Near(float nearness) => PlainChance(nearness) > 0f;

        /// <summary>An avatar comes in disguise, or as a sacred animal: nearly always (felt), less the nearer the god is.</summary>
        public static float DisguiseShare(float nearness) => 0.85f - 0.55f * Strength(nearness);

        /// <summary>The base chance a prayer is answered: as felt, more the nearer the god is.</summary>
        public static float AnswerBase(float nearness) => 0.12f + 0.13f * Strength(nearness);

        /// <summary>How much of a sickness an answer eases (its severity): a little as felt, plainly near.</summary>
        public static float SicknessEase(float nearness) => 0.08f + 0.07f * Strength(nearness);

        /// <summary>How many days a refusal keeps the rain away: five as felt, up to eight near; the lesser gift half as long.</summary>
        public static int DrySpellDays(float nearness, bool lesser) => (5 + (int)Math.Round(3f * Strength(nearness))) / (lesser ? 2 : 1);

        /// <summary>How much of a standing's pull on a caster's mana is felt: most of it as felt, all of it near.</summary>
        public static float ManaShare(float nearness) => 0.6f + 0.4f * Strength(nearness);

        /// <summary>How much more often a god's own doings in the world come: as felt, up to two fifths more near.</summary>
        public static float WorldFactor(float nearness) => 1f + 0.4f * Strength(nearness);

        // ================= How strongly the situation holds =================

        /// <summary>A drought: nothing for a few dry days, all of it from twelve.</summary>
        public static float DroughtStir(int dryDays) => Clamp01((dryDays - 4) / 8f);

        /// <summary>A hard winter: from -5 degrees out of doors, all of it at -25; a cold snap at least most of it.</summary>
        public static float WinterStir(float outdoorTemp, bool coldSnap) => Math.Max(coldSnap ? 0.7f : 0f, float.IsNaN(outdoorTemp) ? 0f : Clamp01((-5f - outdoorTemp) / 20f));

        /// <summary>Heat: a heat wave or a solar flare, else from 32 degrees out of doors, all of it at 44.</summary>
        public static float HeatStir(float outdoorTemp, bool heatWaveOrFlare) => Math.Max(heatWaveOrFlare ? 1f : 0f, float.IsNaN(outdoorTemp) ? 0f : Clamp01((outdoorTemp - 32f) / 12f));

        /// <summary>The sleeper's rock rumbling: by the warnings counted toward its waking, all of it while it is up.</summary>
        public static float DeepStir(int warnings, int wakesAt, bool woken) => woken ? 1f : wakesAt <= 0 ? 0f : Clamp01(warnings / (float)wakesAt);

        /// <summary>So many of something on the land: none below <paramref name="from"/>, all of it at <paramref name="full"/>.</summary>
        public static float CountStir(float count, float from, float full) => full <= from ? (count >= full ? 1f : 0f) : Clamp01((count - from) / (full - from));

        private static float Nan0(float v) => float.IsNaN(v) ? 0f : v;

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : Math.Max(0f, Math.Min(1f, v));
    }
}
