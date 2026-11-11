using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The game's side of a correspondence that builds up (Letters/LetterThreadMath.cs; the player, 10 November 2026:
    /// "continued letter exchange should build up, the contents and familiarity between the colony and others should
    /// develop and be reflected in the messages"). Before a letter is shaped: how things stand with the notable (the
    /// letters kept in LetterBook's correspondence, their regard and its warmest, their people's goodwill, a war, a pen
    /// friend, their people's memory of good turns and wrongs either way), the stage it gives, the names (their full name
    /// for formal terms, a short one between old friends, the colonist who usually writes), a short signature between old
    /// friends, and the thread the letter takes up. After it is written: counted, the thread marked as taken up, and the
    /// threads it raised kept. Only letters written out in full (LetterForm.Full) are read; a call and words passed on are
    /// counted, with no stage of their own.
    /// </summary>
    public static class LetterThreads
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>Good turns and wrongs remembered this long count toward how things stand.</summary>
        private const int DeedDays = 120;

        // ================= Before the letter is shaped =================

        /// <summary>
        /// The stage, its tags and names, and the thread this letter takes up, into the spec (a written letter, not a
        /// council's). <paramref name="to"/> is the one written to (the colonist, or the notable's pawn), <paramref name="writer"/>
        /// the colony's writer for its own letter.
        /// </summary>
        public static void Correspond(LetterSpec s, Notable n, bool ours, Pawn to, string writer)
        {
            if (s == null || n == null || s.shape != LetterShape.Written || s.council) return;
            try
            {
                var c = LetterBook.CorrespondenceWith(n, false);
                var st = Standing(c, n, ours, out string slight);
                s.fam = LetterThreadMath.StageOf(st);
                if (c != null && c.Letters > 0 && st.silence >= LetterThreadMath.LongSilence) s.tags.Add(LetterThreadMath.LongTag);
                // A running joke of theirs is in the context, so its first telling isn't told again (the pools' !jk: lines).
                if (c != null)
                    foreach (var t in c.threads)
                        if (t?.key != null && t.key.StartsWith("jk:", StringComparison.Ordinal))
                            s.tags.Add(t.key);
                Names(s, c, ours, to, writer);
                // Old friends sign with the name alone.
                if (!string.IsNullOrEmpty(s.sign) && LetterThreadMath.SignsShort(s, Rand.Value))
                    s.sign = LetterFormMath.Signature(LetterFormMath.SignOf(ours, s.council, s.empire, s.values.ContainsKey("TITLE"), LetterFormMath.SpeechOf(s),
                        s.values.ContainsKey("HELPER"), true), LetterForm.Words, s.values);
                var scene = new RecallScene
                {
                    ours = ours, stage = s.fam, letterNo = (c?.Letters ?? 0) + 1, now = Now, otherLast = c == null ? -1 : ours ? c.lastTheirs : c.lastOurs, tags = s.tags,
                    values = s.values, here = Here, cool = s.tone >= AnswerTone.Cool, riddleOwed = ours && LetterBook.Owed(n.id) != null, slight = slight, war = st.war
                };
                s.recall = LetterThreadMath.Choose(c?.threads, scene, () => Rand.Value);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letters: a correspondence's stage", e);
                s.fam = Familiarity.None;
                s.recall = null;
            }
        }

        /// <summary>
        /// How things stand with them: the letters each way and when, their regard now and its warmest, their people's
        /// goodwill, a war, a pen friend; their people's memory of good turns either way (summed) and the newest wrong either
        /// way that a cooled letter can name (<paramref name="slight"/>), within DeedDays.
        /// </summary>
        private static LetterStanding Standing(Correspondence c, Notable n, bool ours, out string slight)
        {
            int now = Now;
            slight = null;
            var st = new LetterStanding { wrongDays = -1f, opinion = NotableBook.Opinion(n) };
            if (c != null)
            {
                st.letters = c.Letters;
                st.mine = ours ? c.ours : c.theirs;
                st.days = c.first >= 0 ? (now - c.first) / (float)GenDate.TicksPerDay : 0f;
                st.silence = c.last >= 0 ? (now - c.last) / (float)GenDate.TicksPerDay : 0f;
                st.peak = Math.Max(c.peak, st.opinion);
            }
            else st.peak = st.opinion;
            var f = n.faction;
            st.goodwill = f != null && f.HasGoodwill && !f.IsPlayer ? f.PlayerGoodwill : 0;
            st.war = NotableBook.Hostile(n);
            st.penFriend = PenFriends.Is(n);
            var mem = FactionMemory.Instance;
            if (mem != null && f != null)
            {
                int since = now - GenDate.TicksPerDay * DeedDays;
                FactionDeed wrong = null;
                foreach (var d in mem.Of(f))
                {
                    if (d == null || d.tick < since) continue;
                    if (d.side == DeedSide.Favor || d.side == DeedSide.TheirFavor) st.deeds += d.weight;
                    else if (d.weight >= 6 && (wrong == null || d.tick > wrong.tick) && LetterThreadMath.SlightOf(d.kind, d.side == DeedSide.TheirWrong) != null) wrong = d;
                }
                if (wrong != null)
                {
                    st.wrongDays = (now - wrong.tick) / (float)GenDate.TicksPerDay;
                    st.wrongWeight = wrong.weight;
                    slight = LetterThreadMath.SlightOf(wrong.kind, wrong.side == DeedSide.TheirWrong);
                }
            }
            return st;
        }

        /// <summary>
        /// The names a letter's own lines may use: {TOFULL} the one written to by their first and last name (formal terms),
        /// {TONICK} a short name between old friends (the colony's writer only when they usually write), {LASTPEN} the
        /// colonist who usually wrote when someone else writes now (pen:new), only while they are still of the colony.
        /// </summary>
        private static void Names(LetterSpec s, Correspondence c, bool ours, Pawn to, string writer)
        {
            string toName = s.values.TryGetValue("TO", out var v) ? v : null;
            var nt = to?.Name as NameTriple;
            string full = nt != null ? LetterThreadMath.FullName(nt.First, nt.Last, toName) : toName;
            if (!full.NullOrEmpty()) s.values["TOFULL"] = full;
            bool friends = s.fam == Familiarity.Close || s.fam == Familiarity.Familiar && LetterFormMath.Warmth(s);
            bool usual = ours || c != null && c.pen == toName && c.penRun >= 2;
            string nick = friends && usual && !s.empire ? LetterThreadMath.Nickname(toName) : null;
            if (nick != null) s.values["TONICK"] = nick;
            string last = null;
            if (c != null)
            {
                if (ours && c.pen != null && c.pen != writer && c.penRun >= 2) last = c.pen;
                else if (!ours && c.penRun == 1 && c.oldPen != null && c.pen == toName && c.oldPen != toName) last = c.oldPen;
            }
            if (last != null && Here(last))
            {
                s.tags.Add(LetterThreadMath.PenTag);
                s.values["LASTPEN"] = last;
            }
        }

        /// <summary>A colonist by this name is still of the colony (at home, in a caravan, on their way).</summary>
        private static bool Here(string name)
        {
            if (name.NullOrEmpty()) return false;
            try
            {
                return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Any(p => p != null && p.LabelShort == name);
            }
            catch
            {
                return false;
            }
        }

        // ================= After it is written =================

        /// <summary>
        /// A letter written either way (a call and words passed on too): counted, the thread it took up marked, the threads
        /// it raised kept (what it said; <paramref name="sent"/> what came with their answer: "aid", "gift", "aid:no", and
        /// <paramref name="sentWhat"/> in words). <paramref name="writer"/> is the colony's writer for its own letter.
        /// </summary>
        public static void Corresponded(LetterSpec s, LetterText t, Notable n, bool ours, string writer = null, string sent = null, string sentWhat = null)
        {
            if (s == null || t == null || n == null) return;
            try
            {
                int now = Now;
                var c = LetterBook.CorrespondenceWith(n, true);
                if (c == null) return;
                int letterNo = c.Letters + 1;
                var r = s.recall;
                if (t.recalled && r != null && r.index >= 0 && r.index < c.threads.Count && c.threads[r.index]?.key == r.key)
                    LetterThreadMath.Mark(c.threads[r.index], r.move, now);
                if (ours)
                {
                    c.ours++;
                    c.lastOurs = now;
                    Pen(c, writer);
                }
                else
                {
                    c.theirs++;
                    c.lastTheirs = now;
                }
                if (c.first < 0) c.first = now;
                c.last = now;
                c.peak = Math.Max(c.peak, NotableBook.Opinion(n));
                if (s.shape != LetterShape.Written) return;
                foreach (var x in LetterThreadMath.Raised(ours, s, t.full, sent, sentWhat, t.recalled ? r : null))
                {
                    var made = LetterThread.Of(x, letterNo, now);
                    int slot = LetterThreadMath.Slot(c.threads, made);
                    if (slot >= 0) c.threads[slot] = made;
                    else c.threads.Add(made);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letters: a correspondence kept", e);
            }
        }

        /// <summary>Who of the colony writes to them: the same hand again, or a new one (the one before remembered after two or more in a row).</summary>
        private static void Pen(Correspondence c, string writer)
        {
            if (writer.NullOrEmpty()) return;
            if (c.pen == writer) c.penRun++;
            else
            {
                c.oldPen = c.penRun >= 2 ? c.pen : null;
                c.pen = writer;
                c.penRun = 1;
            }
        }

        // ================= Said in the reading window =================

        /// <summary>"14 letters so far: old friends by letter.", or null before any letter.</summary>
        public static string Summary(Notable n)
        {
            var c = n != null ? LetterBook.CorrespondenceWith(n, false) : null;
            if (c == null || c.Letters == 0) return null;
            try
            {
                var st = Standing(c, n, true, out _);
                st.mine = Math.Max(1, c.Letters);
                var stage = LetterThreadMath.StageOf(st);
                return "CC_LetterThread_Summary".Translate(c.Letters.Named("N"), ("CC_LetterThread_Stage_" + stage).Translate().Named("STAGE")).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letters: a correspondence's summary", e);
                return null;
            }
        }
    }
}
