using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Stage 2's letter purposes, in the existing machinery (World/NeighbourLetters.cs: the writer at a table or the caller
    /// at the console, their messenger at the gate, the answer by tone): this class says whether a purpose can be written,
    /// what it can be about and what is asked, and what their answer does; NeighbourLetters asks it for the purposes it
    /// owns. Favour now (a debt of honour called in, or the colony's word on their side when they called one in); Pact,
    /// Truce, Match, Ward, Ransom, Envoy and Fealty come with their releases (DiplomacyMath.Purposes).
    /// <para>The letter keeps what it is about in its topic ("tie:12" a debt of theirs, "side:15" the colony's word on a
    /// call of theirs) and what is asked in its help field (FavourMath.AskCode).</para>
    /// </summary>
    public static class DiplomacyLetters
    {
        public const string TiePrefix = "tie:", SidePrefix = "side:";

        public static string TieKey(Tie t) => TiePrefix + t.id;

        public static string SideKey(Tie t) => SidePrefix + t.id;

        /// <summary>A purpose this frame answers for.</summary>
        public static bool Owns(LetterPurpose p) => p == LetterPurpose.Favour;

        private static Tie TieOf(string key, string prefix) =>
            key != null && key.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(key.Substring(prefix.Length), out int id) ? FavoursOwed.Get(id) : null;

        // ================= Can it be written =================

        /// <summary>Why it can't be written to them now, or null (the general rules, one at a time and the wait, come first in NeighbourLetters).</summary>
        public static string WhyNot(Notable n, LetterPurpose p)
        {
            if (!Owns(p) || n == null) return null;
            if (!Diplomacy.FavoursOn) return DipWords.P("CC_Dip_Why_Off");
            if (NotableBook.Hostile(n)) return DipWords.P("CC_Dip_Why_War", DipWords.People(n.faction).Named("PEOPLE"));
            return Topics(n).Count == 0 ? DipWords.P("CC_Dip_Why_NothingOwed", DipWords.People(n.faction).Named("PEOPLE")) : null;
        }

        /// <summary>
        /// What a letter to them can be about: a debt their people owe the colony (to the head or anyone of theirs), one they
        /// owe in person (to them only), and a call of theirs on the colony's word that waits for the colony's answer.
        /// </summary>
        public static List<LetterTopic> Topics(Notable n)
        {
            var list = new List<LetterTopic>();
            if (n?.faction == null) return list;
            try
            {
                foreach (var t in FavoursOwed.OwedBy(n.faction))
                {
                    if (FavourCalls.WhyNot(t) != null) continue;
                    if (FavoursOwed.IsPersonal(t) && t.from != n.Person) continue;
                    list.Add(new LetterTopic { key = TieKey(t), text = DipWords.DebtLine(t) });
                }
                foreach (var t in FavourAsks.Waiting())
                    if (Refs.Faction(t.to) == n.faction && FavourMath.ParseNeed(t.terms.goods, out var need) && need == FavourNeed.Dispute)
                        list.Add(new LetterTopic { key = SideKey(t), text = DipWords.P("CC_Dip_Topic_Side", DipWords.People(n.faction).Named("PEOPLE")) });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("diplomacy letters: topics", e);
            }
            return list;
        }

        /// <summary>What can be asked in a letter about this topic, each with why not (none for the colony's word: the letter is the answer).</summary>
        public static List<FavourCalls.Option> Asks(string topicKey, Map map)
        {
            var t = TieOf(topicKey, TiePrefix);
            return t == null ? new List<FavourCalls.Option>() : FavourCalls.Options(t, map);
        }

        // ================= The dialog's own rows =================

        /// <summary>
        /// The letter dialog's rows for a purpose of this frame, under "What for": what about, and what is asked. Returns the
        /// height used; <paramref name="why"/> says why it can't be sent as it stands (nothing that can be asked).
        /// </summary>
        public static float DrawRows(Rect inRect, float y, List<LetterTopic> topics, ref int topicIndex, ref string ask, Map map, out string why)
        {
            why = null;
            float start = y;
            if (topics.Count == 0) return 0f;
            topicIndex = Mathf.Clamp(topicIndex, 0, topics.Count - 1);
            var topic = topics[topicIndex];
            Widgets.Label(new Rect(inRect.x, y + 2f, 120f, 24f), "CC_LetterDlg_About".Translate());
            if (Widgets.ButtonText(new Rect(inRect.x + 124f, y, inRect.width - 124f, 26f), topic.text.Truncate(inRect.width - 140f), true, true, topics.Count > 1))
            {
                var opts = new List<FloatMenuOption>();
                for (int i = 0; i < topics.Count; i++)
                {
                    int k = i;
                    var me = topics;
                    opts.Add(new FloatMenuOption(me[i].text, () => Picked = k));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            TooltipHandler.TipRegion(new Rect(inRect.x + 124f, y, inRect.width - 124f, 26f), topic.text);
            if (Picked >= 0)
            {
                topicIndex = Picked;
                Picked = -1;
                ask = null;
            }
            y += 30f;
            var asks = Asks(topic.key, map);
            if (asks.Count == 0) return y - start;
            // What is asked: the first that can be, unless one is chosen.
            string now = ask;
            if (now == null || !asks.Any(o => o.Code == now && o.why == null)) now = asks.FirstOrDefault(o => o.why == null)?.Code;
            ask = now;
            var chosen = asks.FirstOrDefault(o => o.Code == now);
            Widgets.Label(new Rect(inRect.x, y + 2f, 120f, 24f), "CC_LetterDlg_Ask".Translate());
            string shown = chosen?.label ?? DipWords.P("CC_Dip_Dlg_NothingToAsk");
            if (Widgets.ButtonText(new Rect(inRect.x + 124f, y, inRect.width - 124f, 26f), shown.Truncate(inRect.width - 140f)))
                Find.WindowStack.Add(new FloatMenu(asks.Select(o => o.why == null
                    ? new FloatMenuOption(o.label, () => Asked = o.Code)
                    : new FloatMenuOption("CC_Out_Disabled".Translate(o.label.Named("LABEL"), o.why.Named("WHY")).Resolve(), null)).ToList()));
            if (Asked != null)
            {
                ask = Asked;
                Asked = null;
            }
            if (chosen == null) why = DipWords.P("CC_Dip_Dlg_NothingToAsk");
            y += 30f;
            return y - start;
        }

        // A pick from a float menu, taken up on the next frame (the menu closes after the dialog has drawn).
        private static int Picked = -1;
        private static string Asked;

        // ================= Their answer =================

        /// <summary>What their answer does, and its words for their letter.</summary>
        public sealed class Answer
        {
            public string line;
            public List<Thing> things = new List<Thing>();
        }

        /// <summary>
        /// They have read it (NeighbourLetters.Read): a debt called in is honoured or refused (by their regard, not the
        /// letter's luck), and the answer's tone follows (a refusal is a refusal; honoured, never worse than cool); the
        /// colony's word on their side is taken with thanks. Null for a purpose this frame doesn't own.
        /// </summary>
        public static Answer Read(NeighbourLetter l, Notable n, Pawn speaker, ref AnswerTone tone)
        {
            if (l == null || !Owns(l.purpose)) return null;
            var a = new Answer();
            try
            {
                var side = TieOf(l.topicKey, SidePrefix);
                if (side != null)
                {
                    if (side.state == Tie.State.CalledIn) FavourAsks.SideTaken(side, KinMemory.Ours(speaker) ? speaker : null);
                    if (tone > AnswerTone.Cordial) tone = AnswerTone.Cordial;
                    a.line = DipWords.T("CC_Dip_Ans_Side", n.Name.Named("NAME"), DipWords.People(n.faction).Named("PEOPLE"));
                    return a;
                }
                var t = TieOf(l.topicKey, TiePrefix);
                if (t == null)
                {
                    a.line = DipWords.T("CC_Dip_Ans_Moot");
                    return a;
                }
                var heard = FavourCalls.Hear(t, l.help, l.map, KinMemory.Ours(speaker) ? speaker : null, true, Rand.Value);
                a.line = heard.line;
                a.things.AddRange(heard.things);
                if (!heard.honoured) tone = AnswerTone.Refusal;
                else if (tone > AnswerTone.Cool) tone = AnswerTone.Cool;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("diplomacy letters: the answer", e);
            }
            return a;
        }
    }
}
