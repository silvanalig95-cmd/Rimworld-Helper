using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What harmless wounds get under medicine by what is treated (the setting): no medicine, or herbal at most.</summary>
    public enum HarmlessMedicine
    {
        None,
        Herbal
    }

    /// <summary>
    /// The rules of medicine by what is treated (Health/MedicineChoice.cs): how much a condition threatens the one who has
    /// it, what medicine that earns under the player's medical care setting, and what a tending does with the medicine the
    /// doctor carries. Pure (no game types): Tools/Tests checks them on clear cases.
    /// <para>
    /// Three kinds. Harmless: a wound that neither bleeds nor can get infected (a bruise, a crack, a scratch so small it
    /// barely bleeds and is unlikely to go bad); it gets no medicine, only bandaging (a setting makes it herbal at most).
    /// Minor: a wound that bleeds a little or can get infected (a cut, a bite, a small burn), and Rimfolk's everyday
    /// ailments (a head cold, blisters); herbal medicine at most. Serious: heavy bleeding, a wound on or in a part the body
    /// can't live without, a deep wound, a fresh stump, anything threatening a life now, and every other illness, infection
    /// and lasting condition; the best the medical care setting allows. The setting is always the ceiling: the rules only
    /// ever choose less.
    /// </para>
    /// </summary>
    public static class MedicineChoiceMath
    {
        /// <summary>The game's MedicalCareCategory, in its own numbers.</summary>
        public const int NoCare = 0, NoMeds = 1, Herbal = 2, Normal = 3, Best = 4;

        /// <summary>A wound bleeding this much a day or more (40% of the blood, as the health tab shows it) bleeds heavily.</summary>
        public const float HeavyBleedPerDay = 0.4f;

        /// <summary>A wound bleeding less than this a day (10%) barely bleeds.</summary>
        public const float BleedsPerDay = 0.1f;

        /// <summary>A wound whose own chance of turning infected is under this (3%) is unlikely to go bad.</summary>
        public const float MayInfectChance = 0.03f;

        /// <summary>A wound this deep (the game's infection chance is at its full from here) is deep, if it can get infected.</summary>
        public const float DeepSeverity = 12f;

        /// <summary>A wound taking this much of its part's health or more is deep, whatever kind it is.</summary>
        public const float DeepPartFraction = 0.5f;

        /// <summary>
        /// Someone who would bleed to death within this many hours (the game's own mark for an urgent tending, 45,000 ticks)
        /// is bleeding out: then every wound that bleeds is serious.
        /// </summary>
        public const float BleedOutHours = 18f;

        /// <summary>
        /// A condition coming due a tending within this long (an hour) counts for the medicine a doctor fetches: a job made
        /// for a bruise brings the plague's medicine when the plague's last tending runs out on the way.
        /// </summary>
        public const int SoonTicks = 2500;

        public enum Threat
        {
            Harmless,
            Minor,
            Serious
        }

        /// <summary>Why a condition is judged as it is, for the line on the health tab.</summary>
        public enum Why
        {
            // Serious
            CouncilAnswer,
            LifeThreatening,
            BleedingOut,
            BleedsHeavily,
            VitalPart,
            Organ,
            Deep,
            Infection,
            SeriousIllness,
            Chronic,
            Lethal,
            Illness,
            // Minor
            EverydayAilment,
            Bleeds,
            MayInfect,
            // Harmless
            Clean,
            Small
        }

        /// <summary>What the game knows of one condition, as the rule reads it.</summary>
        public struct Facts
        {
            /// <summary>A wound (the game's injury: a cut, a bruise, a burn, a bite, a gunshot, a crack).</summary>
            public bool injury;
            /// <summary>A fresh stump: a part lost and still bleeding.</summary>
            public bool missingPart;
            /// <summary>The wound's size, in the game's points of damage.</summary>
            public float severity;
            /// <summary>The full health of the part it is on (0: on the whole body).</summary>
            public float partHealth;
            /// <summary>How much of the blood it would lose a day left untended (the game's bleed rate, before any tending).</summary>
            public float bleedPerDay;
            /// <summary>The wound kind's own chance of turning infected, before its size (0: it can't).</summary>
            public float infectionChance;
            /// <summary>An animal: the game gives an animal's wounds a tenth of the chance.</summary>
            public bool animal;
            /// <summary>On a part the body can't live without (the game's vital parts: brain, heart, lungs, liver, kidneys, stomach, neck).</summary>
            public bool vitalPart;
            /// <summary>On an organ inside the body.</summary>
            public bool organ;
            /// <summary>At a stage that threatens their life now.</summary>
            public bool lifeThreatening;
            /// <summary>It can kill once it goes far enough.</summary>
            public bool lethal;
            /// <summary>A wound gone infected (the game's infection of a wound).</summary>
            public bool infection;
            /// <summary>A lasting condition (the game's chronic ones: asthma, a bad back, a carcinoma).</summary>
            public bool chronic;
            /// <summary>One of Rimfolk's ailments, and whether it is of its serious illnesses.</summary>
            public bool rimfolkAilment, rimfolkSerious;
            /// <summary>The patient would bleed to death within <see cref="BleedOutHours"/>.</summary>
            public bool bleedingOut;
            /// <summary>The council's answer gave this patient the best medicine there is.</summary>
            public bool bestByAnswer;
        }

        /// <summary>The game's factor on a wound's infection chance by its size: a tenth at 1 point, the whole at 12, evenly between.</summary>
        public static float SeverityFactor(float severity)
        {
            if (severity <= 1f) return 0.1f;
            if (severity >= 12f) return 1f;
            return 0.1f + (severity - 1f) * 0.9f / 11f;
        }

        /// <summary>The chance this wound turns infected, left untended: its kind's chance, by its size, a tenth for an animal.</summary>
        public static float WoundInfectionChance(float infectionChance, float severity, bool animal) =>
            infectionChance <= 0f ? 0f : infectionChance * (animal ? 0.1f : 1f) * SeverityFactor(severity);

        /// <summary>How much a condition threatens the one who has it, and why.</summary>
        public static Threat Judge(Facts f, out Why why)
        {
            if (f.bestByAnswer)
            {
                why = Why.CouncilAnswer;
                return Threat.Serious;
            }
            if (f.lifeThreatening)
            {
                why = Why.LifeThreatening;
                return Threat.Serious;
            }
            if (!f.injury && !f.missingPart)
            {
                // Illnesses, infections and lasting conditions: the right medicine decides how soon and whether it passes,
                // except Rimfolk's everyday ailments, which herbs see to.
                if (f.rimfolkAilment && !f.rimfolkSerious && !f.lethal)
                {
                    why = Why.EverydayAilment;
                    return Threat.Minor;
                }
                why = f.infection ? Why.Infection : f.rimfolkSerious ? Why.SeriousIllness : f.chronic ? Why.Chronic : f.lethal ? Why.Lethal : Why.Illness;
                return Threat.Serious;
            }
            bool bleeds = f.bleedPerDay >= BleedsPerDay;
            if (f.bleedingOut && f.bleedPerDay > 0f)
            {
                why = Why.BleedingOut;
                return Threat.Serious;
            }
            if (f.bleedPerDay >= HeavyBleedPerDay)
            {
                why = Why.BleedsHeavily;
                return Threat.Serious;
            }
            if (f.missingPart)
            {
                // A stump that bleeds less than heavily (a small creature's toe): it bleeds all the same.
                why = Why.Bleeds;
                return Threat.Minor;
            }
            if (f.vitalPart)
            {
                why = Why.VitalPart;
                return Threat.Serious;
            }
            if (f.organ)
            {
                why = Why.Organ;
                return Threat.Serious;
            }
            float chance = WoundInfectionChance(f.infectionChance, f.severity, f.animal);
            if ((f.partHealth > 0f && f.severity >= f.partHealth * DeepPartFraction) || (chance > 0f && f.severity >= DeepSeverity))
            {
                why = Why.Deep;
                return Threat.Serious;
            }
            if (bleeds)
            {
                why = Why.Bleeds;
                return Threat.Minor;
            }
            if (chance >= MayInfectChance)
            {
                why = Why.MayInfect;
                return Threat.Minor;
            }
            why = f.bleedPerDay > 0f || chance > 0f ? Why.Small : Why.Clean;
            return Threat.Harmless;
        }

        /// <summary>
        /// The medical care a condition gets: serious, the player's setting itself; minor, herbal at most; harmless, no
        /// medicine (or herbal at most with <paramref name="harmlessHerbal"/>). Never above the setting, and never no care
        /// at all where the setting allows any: the harmless are still tended.
        /// </summary>
        public static int CareFor(Threat t, int ceiling, bool harmlessHerbal)
        {
            if (ceiling <= NoCare) return NoCare;
            int want = t == Threat.Serious ? Best : t == Threat.Minor ? Herbal : harmlessHerbal ? Herbal : NoMeds;
            return want < ceiling ? want : ceiling;
        }

        /// <summary>The lowest medical care that allows a medicine of this potency, against the game's herbal and industrial medicine.</summary>
        public static int NeedsCare(float potency, float herbalPotency, float industrialPotency) =>
            potency <= herbalPotency ? Herbal : potency <= industrialPotency ? Normal : Best;

        /// <summary>A medical care allows a medicine that needs <paramref name="needs"/> (as the game's AllowsMedicine).</summary>
        public static bool Allows(int care, int needs) => care >= Herbal && care >= needs;

        /// <summary>
        /// What a tending of a batch (the care it gets) does with the medicine the doctor carries, which needs care
        /// <paramref name="needs"/>: uses it when the batch's care allows it; without any when the batch gets no medicine;
        /// otherwise the doctor goes for lesser medicine first (a fresh job finds the medicine the batch may have).
        /// </summary>
        public enum AtTend
        {
            UseIt,
            WithoutIt,
            FetchOther
        }

        public static AtTend WithCarried(int batchCare, int needs)
        {
            if (Allows(batchCare, needs)) return AtTend.UseIt;
            return batchCare >= Herbal ? AtTend.FetchOther : AtTend.WithoutIt;
        }

        /// <summary>
        /// Whether a doctor's tend job ends after this tending, so a fresh one fetches the medicine the next tending gets:
        /// the medicine in hand is better than it gets (and, where a tending can't be done without it in hand, any it gets
        /// none), or the next tending gets better than the medicine was fetched for (<paramref name="chosenCare"/>; the
        /// plague came due while the scratches were seen to). A job with no medicine in hand goes on unless the next
        /// tending gets some the job wasn't made for. <paramref name="chosenCare"/> unknown: int.MaxValue.
        /// </summary>
        public static bool EndAfter(int nextCare, bool carrying, int carriedNeeds, int chosenCare, bool canTendWithout)
        {
            if (nextCare < 0) return false;
            if (carrying)
            {
                if (Allows(nextCare, carriedNeeds)) return nextCare > chosenCare;
                return nextCare >= Herbal || !canTendWithout;
            }
            return nextCare >= Herbal && nextCare > chosenCare;
        }

        /// <summary>
        /// How many tendings in a row one kind of medicine serves: the tendings that get the same care as the first (the
        /// game tends the most serious first; the medicine is fetched for that one). None when the first gets no medicine.
        /// </summary>
        public static int SameMedicineCount(IList<int> batchCares)
        {
            if (batchCares == null || batchCares.Count == 0 || batchCares[0] < Herbal) return 0;
            int n = 0;
            for (int i = 0; i < batchCares.Count; i++)
                if (batchCares[i] == batchCares[0]) n++;
            return n;
        }

        /// <summary>
        /// What the game's tend priority is shifted by for each kind, so the serious are tended first, then the minor, then
        /// the harmless; within a kind the game's own order (bleeding first) stands. The game's own priorities run from 0
        /// to a few points (1 for a threat to life, the bleed rate and a half): ten points apart keeps the kinds apart.
        /// </summary>
        public static float PriorityShift(Threat t) => t == Threat.Serious ? 0f : t == Threat.Minor ? -10f : -20f;
    }
}
