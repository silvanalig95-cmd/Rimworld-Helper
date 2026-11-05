using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The letter purpose of release 2b.3 in the existing machinery (World/NeighbourLetters.cs, through DiplomacyLetters):
    /// Ward, asking them for a youth of theirs to foster at the colony for a year ("ward:ask", what is asked the skill the
    /// colony teaches, "skill:Crafting"), or offering them a youth of the colony's ("ward:send", the youth, "youth:123"; to
    /// the Empire, as a page in a titled household). Their answer is by their head's regard (Wards, WardsAbroad), and the
    /// answer's tone follows it.
    /// </summary>
    public static class WardLetters
    {
        public static bool Owns(LetterPurpose p) => p == LetterPurpose.Ward;

        /// <summary>Their words in the answer: "send" for a youth of the colony's offered (and "page" to the Empire), "askward" for theirs asked for.</summary>
        public const string SendTag = "send", PageTag = "page", AskTag = "askward";

        /// <summary>Why it can't be written to them now, or null: neither a ward of theirs can be asked for nor a youth of the colony's offered.</summary>
        public static string WhyNot(Notable n, Map home)
        {
            var f = n?.faction;
            if (!Wards.On) return WardWords.P("CC_Ward_Why_Off");
            string ask = Wards.AskWhyNot(f, home);
            if (ask == null) return null;
            string send = WardsAbroad.SendWhyNot(f, home);
            return send == null ? null : ask;
        }

        /// <summary>What a letter to them can be about: asking for a ward of theirs, offering a youth of the colony's.</summary>
        public static List<LetterTopic> Topics(Notable n, Map home)
        {
            var list = new List<LetterTopic>();
            var f = n?.faction;
            if (f == null || home == null || !Wards.On) return list;
            try
            {
                if (Wards.AskWhyNot(f, home) == null)
                    list.Add(new LetterTopic { key = WardMath.AskTopic, text = WardWords.P("CC_Ward_Topic_Ask", WardWords.Form(home, f).word.Named("WORD")) });
                if (WardsAbroad.SendWhyNot(f, home) == null)
                    list.Add(new LetterTopic
                    {
                        key = WardMath.SendTopic,
                        text = Ages.IsEmpire(f) ? WardWords.P("CC_Ward_Topic_SendPage") : WardWords.P("CC_Ward_Topic_Send", WardWords.Form(home, f).word.Named("WORD"))
                    });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ward letters: topics", e);
            }
            return list;
        }

        /// <summary>What is asked: the skill to learn (each with its guardian), or the youth who goes (each greyed with why not).</summary>
        public static List<FavourCalls.Option> Asks(string topicKey, Map home)
        {
            var list = new List<FavourCalls.Option>();
            if (home == null) return list;
            if (topicKey == WardMath.AskTopic)
            {
                foreach (var s in Wards.Skills(home).Take(8))
                {
                    var g = Wards.Guardians(home, s).FirstOrDefault();
                    list.Add(new FavourCalls.Option
                    {
                        code = WardMath.SkillCode(s.defName), label = WardWords.P("CC_Ward_AskSkill", s.label.Named("SKILL"), (g?.LabelShort ?? "?").Named("GUARDIAN"))
                    });
                }
            }
            else if (topicKey == WardMath.SendTopic)
            {
                foreach (var p in WardsAbroad.Youths(home))
                    list.Add(new FavourCalls.Option
                    {
                        code = WardMath.YouthCode(p.thingIDNumber), label = WardWords.P("CC_Ward_AskYouth", p.Named("PAWN"), p.ageTracker.AgeBiologicalYears.Named("AGE")),
                        why = WardsAbroad.YouthWhyNot(p, home)
                    });
            }
            return list;
        }

        /// <summary>Tags for their own words in the answer (DiplomacyLetters.QuoteTags).</summary>
        public static void QuoteTags(NeighbourLetter l, Notable n, HashSet<string> tags)
        {
            if (l?.purpose != LetterPurpose.Ward) return;
            if (l.topicKey == WardMath.SendTopic)
            {
                tags.Add(SendTag);
                if (Ages.IsEmpire(n?.faction)) tags.Add(PageTag);
            }
            else tags.Add(AskTag);
        }

        /// <summary>
        /// They have read it (or heard the call): their answer by their head's regard, and the answer's tone follows it (no is a
        /// refusal; yes, never worse than cool). Their ward comes, or the colony's youth sets out, after the answer's way back.
        /// </summary>
        public static string Read(NeighbourLetter l, Notable n, Map home, ref AnswerTone tone)
        {
            var f = n?.faction;
            bool yes = false;
            string line;
            float days = l.channel == LetterChannel.Console ? Rand.Range(0.25f, 0.5f) : l.backDays + 0.5f;
            if (l.topicKey == WardMath.AskTopic)
            {
                WardMath.ParseSkill(l.help, out string skill);
                line = Wards.HearAsk(f, home, NotableBook.Opinion(n), skill, days + WardMath.ComeDays(Rand.Value) - 1f, out yes);
            }
            else if (l.topicKey == WardMath.SendTopic && WardMath.ParseYouth(l.help, out int id))
                line = WardsAbroad.HearSend(f, home, id, NotableBook.Opinion(n), days, out yes);
            else line = WardWords.T("CC_Ward_Ans_Moot", WardWords.People(f).Named("PEOPLE"));
            if (!yes) tone = AnswerTone.Refusal;
            else if (tone > AnswerTone.Cool) tone = AnswerTone.Cool;
            return line;
        }
    }
}
