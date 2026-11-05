using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The letter purpose of release 2c.1 in the existing machinery (World/NeighbourLetters.cs, through DiplomacyLetters):
    /// Task, the council's business, written only by an officer on a task (CouncilTasks) or by the keeper pressing a secret
    /// (SecretTies), never from the letter dialog (hidden there). The officer sits at a table and writes, or calls on the
    /// console when the colony has one powered and their people are industrial or later; their messenger fetches it; their
    /// answer comes back in its own words (the pool's "task" lines). The letter keeps what it is for in its topic: "task:12"
    /// the task, "press:7" the secret tie pressed.
    /// </summary>
    public static class TaskLetters
    {
        public const string TaskPrefix = "task:", PressPrefix = "press:";

        /// <summary>Their words in the answer: "task" (the purpose) and the kind's code; "pressed" for a secret pressed.</summary>
        public const string PressedTag = "pressed";

        /// <summary>
        /// An answer overtaken by events (DiplomacyLetters.Moot): a task over before they read its letter answered under
        /// "taskmoot" in place of its kind's code, a secret no longer pressed (answered already, or let go) under "pressmoot"
        /// in place of "pressed", so no line promising silver or answering the task's business is quoted with it
        /// (Rimfolk_Lines_Overtaken.xml).
        /// </summary>
        public const string TaskMootTag = "taskmoot", PressMootTag = "pressmoot";

        public static bool Owns(LetterPurpose p) => p == LetterPurpose.Task;

        /// <summary>Never offered in the letter dialog: officers write these on their own.</summary>
        public static bool Hidden(LetterPurpose p) => p == LetterPurpose.Task;

        public static string TaskKey(CouncilTask t) => TaskPrefix + t.id;

        public static string PressKey(Tie t) => PressPrefix + t.id;

        private static bool sending;

        /// <summary>Only an officer's own letter goes (the general rules, one at a time and the wait, come first in NeighbourLetters).</summary>
        public static string WhyNot(Notable n) => sending ? null : TaskWords.P("CC_Task_Why_LetterDialog");

        /// <summary>Nothing for the dialog to pick.</summary>
        public static List<LetterTopic> Topics(Notable n) => new List<LetterTopic>();

        /// <summary>
        /// The officer's letter or call: by call when the colony's console works and their people can answer one, else a
        /// letter by hand. Null when it can't go now (one at a time to them, the colony's letters all under way, the wait).
        /// </summary>
        public static NeighbourLetter Send(Notable n, Pawn writer, string topicKey, string topicText)
        {
            var map = writer?.MapHeld;
            if (n == null || writer == null || map == null || !KinMemory.Ours(writer)) return null;
            var channel = NeighbourLetters.ChannelWhyNot(n, LetterChannel.Console, map) == null ? LetterChannel.Console : LetterChannel.Courier;
            if (channel == LetterChannel.Courier && NeighbourLetters.ChannelWhyNot(n, LetterChannel.Courier, map) != null) return null;
            sending = true;
            try
            {
                return NeighbourLetters.Send(n, LetterPurpose.Task, channel, writer, new LetterTopic { key = topicKey, text = topicText }, null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("council tasks: a letter", e);
                return null;
            }
            finally
            {
                sending = false;
            }
        }

        private static int Id(string key, string prefix) =>
            key != null && key.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(key.Substring(prefix.Length), out int id) ? id : -1;

        /// <summary>
        /// Tags for their words in the answer (DiplomacyLetters.QuoteTags): the task's kind, or a secret pressed and how they
        /// took it; a letter overtaken by events by its moot tag instead (TaskMootTag, PressMootTag).
        /// </summary>
        public static void QuoteTags(NeighbourLetter l, Notable n, HashSet<string> tags)
        {
            if (l == null || tags == null || !Owns(l.purpose)) return;
            bool moot = DiplomacyLetters.IsMoot(l);
            int task = Id(l.topicKey, TaskPrefix);
            if (task >= 0)
            {
                if (moot)
                {
                    tags.Add(TaskMootTag);
                    return;
                }
                var t = CouncilTasks.Get(task, out _);
                if (t != null) tags.Add(TaskMath.Code(t.kind));
                return;
            }
            if (Id(l.topicKey, PressPrefix) >= 0) tags.Add(moot ? PressMootTag : PressedTag);
        }

        /// <summary>They have read it (or heard the call): the task's step, or the secret pressed and their answer. The answer's words, its tone set.</summary>
        public static string Read(NeighbourLetter l, Notable n, Pawn speaker, ref AnswerTone tone, List<Thing> things)
        {
            try
            {
                int task = Id(l.topicKey, TaskPrefix);
                if (task >= 0)
                {
                    var t = CouncilTasks.Get(task, out _);
                    if (t == null || !t.Live)
                    {
                        DiplomacyLetters.Moot(l);
                        return TaskWords.T("CC_Task_Ans_Moot", n.Name.Named("NAME"));
                    }
                    return TaskResults.LetterRead(t, l, n, ref tone);
                }
                int press = Id(l.topicKey, PressPrefix);
                if (press >= 0)
                {
                    string line = SecretTies.PressRead(press, n, l.map, ref tone, things, out bool moot);
                    if (moot) DiplomacyLetters.Moot(l);
                    return line;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("council tasks: their answer", e);
            }
            return null;
        }
    }
}
