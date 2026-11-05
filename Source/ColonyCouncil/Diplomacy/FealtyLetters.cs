using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The letter purpose of release 2c.2 in the existing machinery (World/NeighbourLetters.cs, through DiplomacyLetters):
    /// Fealty, the colony asking to swear fealty to their people ("fealty:swear:7", people 7), its terms told by their ways in
    /// the topic. Their answer is by their goodwill and their head's regard (Fealty.Hear); agreed, their lord and retinue
    /// come to the gate after the answer's way back, and the answer's tone follows it.
    /// </summary>
    public static class FealtyLetters
    {
        public const string SwearPrefix = "fealty:swear:";

        public static bool Owns(LetterPurpose p) => p == LetterPurpose.Fealty;

        /// <summary>Why it can't be written to them now, or null.</summary>
        public static string WhyNot(Notable n, Map home) => Fealty.SwearWhyNot(n?.faction, home);

        /// <summary>The one thing a letter of this purpose is about: the oath offered, by both sides' ways.</summary>
        public static List<LetterTopic> Topics(Notable n, Map home)
        {
            var list = new List<LetterTopic>();
            var f = n?.faction;
            if (f == null || home == null || Fealty.SwearWhyNot(f, home) != null) return list;
            try
            {
                var form = FealtyWords.Form(home, f);
                list.Add(new LetterTopic
                {
                    key = SwearPrefix + f.loadID, text = FealtyWords.P("CC_Fealty_Topic", form.word.Named("WORD"), form.rite.Named("RITE"), Fealty.TributeValue(home).Named("TRIBUTE"))
                });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fealty letters: topics", e);
            }
            return list;
        }

        /// <summary>They have read it (or heard the call): their answer, and the answer's tone follows it (no is a refusal; yes, never worse than cool).</summary>
        public static string Read(NeighbourLetter l, Notable n, Map home, ref AnswerTone tone)
        {
            var f = n?.faction;
            bool yes = false;
            string line;
            int id = l.topicKey != null && l.topicKey.StartsWith(SwearPrefix, StringComparison.Ordinal) && int.TryParse(l.topicKey.Substring(SwearPrefix.Length), out int x) ? x : -1;
            if (f == null || id != f.loadID) line = FealtyWords.T("CC_Fealty_Ans_Moot", FealtyWords.People(f).Named("PEOPLE"));
            else
            {
                // Their lord comes after the answer's way back (a call: some hours).
                float days = l.channel == LetterChannel.Console ? Rand.Range(FealtyMath.OathHoursMin, FealtyMath.OathHoursMax) / 24f : l.backDays + 0.5f;
                line = Fealty.Hear(f, home, days, Rand.Value, out yes);
            }
            if (!yes) tone = AnswerTone.Refusal;
            else if (tone > AnswerTone.Cool) tone = AnswerTone.Cool;
            return line;
        }
    }
}
