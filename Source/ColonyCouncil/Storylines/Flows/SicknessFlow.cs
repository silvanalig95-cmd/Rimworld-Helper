using System;

namespace ColonyCouncil
{
    /// <summary>
    /// S2, the sickness in the next town (StorylineSickness.cs; docs/quests-proposals.md). The town (the hospitaller order
    /// first) asks for medicine; then three to six of its sick are cared for in the colony's beds; all recovered, the order
    /// sends a healer who stays and teaches the colony's doctor; some died, their kin come for the bodies (handed over, or
    /// buried here).
    /// </summary>
    public sealed class SicknessFlow : StorylineFlow
    {
        public const string Key = "sickness";

        public override string Kind => Key;

        public override int Number => 2;

        public override string[] Parts => new[] { "medicine", "sick", "healer", "kin" };

        public override string[] Endings => new[] { "declined", "nohelp", "turnedaway", "taught", "thanked", "handed", "buried", "refused", "nobodies", "harmed", "gone", "faded" };

        public override string[] Outcomes(string part)
        {
            switch (part)
            {
                case "medicine": return new[] { "sent", "partly", "none" };
                case "sick": return new[] { "recovered", "somedied", "harmed" };
                case "healer": return new[] { "taught", "fewlessons" };
                case "kin": return new[] { "handed", "buried", "nobodies" };
            }
            return new string[0];
        }

        public override bool Failed(string outcome) => outcome == "none" || outcome == "harmed" || outcome == "nobodies";

        public override StorylineMath.Step Next(string part, string outcome, int times, float roll)
        {
            switch (part)
            {
                case "medicine":
                    if (outcome == "sent" || outcome == "partly") return To("sick", 2f, 5f, outcome);
                    if (outcome == "none") return End("nohelp");
                    if (outcome == StorylineMath.Declined) return End("declined");
                    return End(StorylineMath.Faded);
                case "sick":
                    if (outcome == "recovered") return To("healer", 3f, 8f);
                    if (outcome == "somedied") return To("kin", 1f, 2f, "died");
                    if (outcome == "harmed") return End("harmed");
                    if (outcome == StorylineMath.Declined) return End("turnedaway");
                    return End(StorylineMath.Faded);
                case "healer":
                    if (outcome == "taught" || outcome == "fewlessons") return End("taught");
                    // Not wanted: the order thanks the colony another way, a few days later.
                    if (outcome == StorylineMath.Declined) return End("thanked", 1f, 3f);
                    return End(StorylineMath.Faded);
                case "kin":
                    if (outcome == "handed") return End("handed");
                    if (outcome == "buried") return End("buried");
                    if (outcome == "nobodies") return End("nobodies");
                    if (outcome == StorylineMath.Declined) return End("refused");
                    return End(StorylineMath.Faded);
            }
            return End(StorylineMath.Faded);
        }

        public override bool EndingHasLetter(string ending) => ending == "thanked" || ending == StorylineMath.Gone;

        // ================= The numbers =================

        /// <summary>Medicine asked for: six to fourteen by the colony's size.</summary>
        public static int MedicineCount(int colonists) => Clamp(5 + colonists / 2 + (colonists >= 10 ? 2 : 0), 6, 14);

        /// <summary>What the order pays for all of it: half again over its worth.</summary>
        public static int MedicinePay(int count, float unit) => Math.Max(60, StorylineMath.Round10(count * Safe(unit) * 1.5f));

        /// <summary>Part of it, at least half: its plain worth.</summary>
        public static int PartPay(int have, float unit) => Math.Max(0, StorylineMath.Round10(have * Safe(unit)));

        /// <summary>"sent" (all of it), "partly" (half or more), "none".</summary>
        public static string MedicineOutcome(int have, int count) => count <= 0 || have >= count ? "sent" : have * 2 >= count ? "partly" : "none";

        public const float MedicineMinDays = 6f, MedicineMaxDays = 8f;

        /// <summary>Three to six sick, by the colony's size.</summary>
        public static int SickCount(int colonists, float roll) => Clamp(3 + (int)Math.Floor(Clamp(Safe(roll), 0f, 0.999f) * 3f) + (colonists >= 8 ? 1 : 0), 3, 6);

        /// <summary>The longest the sick stay in the colony's care before the part is counted (most are well in four to six days).</summary>
        public const float SickDays = 9f;

        /// <summary>
        /// The illness, the game's own catching kind (never a made-up one): the game's flu in every age; its plague likelier
        /// among the tribal and medieval peoples, rare among the industrial, never the spacer age; a plague among the
        /// region's world events makes it likelier. <paramref name="age"/>: 0 tribal, 1 medieval, 2 industrial, 3 spacer.
        /// </summary>
        public static string Illness(int age, bool plagueYear, float roll)
        {
            float plague = age <= 1 ? 0.4f : age == 2 ? 0.15f : 0f;
            if (plagueYear) plague = Math.Max(plague, age <= 1 ? 0.7f : age == 2 ? 0.4f : 0.1f);
            return Safe(roll) < plague ? "Plague" : "Flu";
        }

        public const string Flu = "Flu", Plague = "Plague";

        /// <summary>How far along it is when they come: well along, the body already fighting it, so with care they mend.</summary>
        public const float SeverityMin = 0.22f, SeverityMax = 0.32f, ImmunityMin = 0.35f, ImmunityMax = 0.5f;

        /// <summary>Days the order's healer stays.</summary>
        public const float HealerMinDays = 5f, HealerMaxDays = 6f;

        /// <summary>The healer's skill at medicine.</summary>
        public const int HealerSkillMin = 12, HealerSkillMax = 15;

        /// <summary>Hours of a day's lesson at the healer's side.</summary>
        public const float LessonMinHours = 2.5f, LessonMaxHours = 3.5f;

        /// <summary>Experience an hour of the lesson, by how far the healer leads: 150 to 320 (a day's lesson about 400 to 1100).</summary>
        public static float LessonXp(int teacher, int learner) => Clamp(150f + 18f * (teacher - learner), 150f, 320f);

        /// <summary>Taught: a lesson on at least half the days the healer stayed.</summary>
        public static bool Taught(int lessons, int days) => lessons > 0 && lessons * 2 >= Math.Max(1, days);

        /// <summary>The kin who come for the dead: one to carry each, two at least, five at most.</summary>
        public static int KinCount(int dead) => Clamp(dead, 2, 5);

        /// <summary>Hours the kin wait at the graves for the burial.</summary>
        public const float BurialHours = 30f;

        /// <summary>The order's thanks when the healer was not wanted: medicine worth this much.</summary>
        public const float ThanksWorth = 200f;
    }
}
