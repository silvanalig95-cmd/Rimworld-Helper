using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The provider of earlier mentions (marks), and what became of them.
    public static partial class Facts
    {
        // ---------------- Earlier talks, and what became of them ----------------

        private static void Marks(FactSet f, Pawn c, Pawn x, Faction p, Settlement town)
        {
            var pair = OutsiderBook.Instance?.Pair(c, x, false);
            if (pair == null || pair.marks.Count == 0) return;
            int now = Now;
            foreach (var s in pair.marks)
            {
                var m = OutsiderTalkMath.DecodeMark(s);
                if (m == null || now - m.tick < GenDate.TicksPerHour * 12) continue;
                var mf = new MarkFacts { key = m.key, subject = m.subject, fact = m.fact };
                mf.tags.Add("talked:" + m.key);
                mf.heat["talked:" + m.key] = 1f;
                f.tags.Add("talked:" + m.key);
                string ago = OutsiderTalkMath.AgoTag(m.tick, now);
                if (ago != null) mf.tags.Add(ago);
                if (!m.subject.NullOrEmpty()) mf.values["THEN"] = m.subject;
                Since(mf, m, p, town);
                f.marks.Add(mf);
            }
        }

        /// <summary>
        /// What became of the fact a mark remembers: still so (then:still), over (then:over), the one asked about dead
        /// (then:dead). Read from the game itself, not the history or the ledger: they don't keep these by the ids a mark
        /// holds (a world event's, a site's, a quest's), the game's relations and a notable's health are the moment's,
        /// and the ledger's views are kept for a 250-tick stretch (a head who died within it would still lead there).
        /// </summary>
        private static void Since(MarkFacts mf, TalkMark m, Faction p, Settlement town)
        {
            string fact = m.fact;
            if (fact.NullOrEmpty()) return;
            var parts = fact.Split(':');
            void Then(string tag, float heat)
            {
                mf.tags.Add(tag);
                mf.heat[tag] = heat;
            }
            switch (parts[0])
            {
                case "ws":
                {
                    int id = parts.Length > 1 && int.TryParse(parts[1], out int i) ? i : -1;
                    bool on = WorldSituations.Instance?.Running.Any(s => s.id == id) ?? false;
                    if (on) Then("then:still", 1.5f);
                    else Then("then:over", 3f);
                    break;
                }
                case "war":
                {
                    var g = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => parts.Length > 1 && x.loadID.ToString() == parts[1]);
                    if (g == null || p == null) return;
                    if (g.defeated) Then("then:over", 3f);
                    else if (p.HostileTo(g)) Then("then:still", 1.5f);
                    else Then("then:over", 3f);
                    break;
                }
                case "ill":
                {
                    int id = parts.Length > 1 && int.TryParse(parts[1], out int i) ? i : -1;
                    var n = NotableBook.Instance?.Get(id);
                    if (n == null || !n.Alive)
                    {
                        if (n != null || NotableBook.Instance?.losses.Any(l => l.name == m.subject) == true) Then("then:dead", 3f);
                        return;
                    }
                    var hs = n.pawn.health?.hediffSet;
                    bool sick = hs != null && hs.hediffs.Any(h => h?.def != null && h.Visible && (h.def.makesSickThought || h.def.lethalSeverity > 0f && h.def.isBad && !(h is Hediff_Injury)));
                    if (sick || (n.pawn.health?.summaryHealth?.SummaryHealthPercent ?? 1f) < 0.6f) Then("then:still", 1.5f);
                    else Then("then:over", 3f);
                    break;
                }
                case "head":
                case "leader":
                {
                    // head:townId:pawnId, leader:factionId:pawnId: the same one leads still, or someone new does.
                    int pid = parts.Length > 2 && int.TryParse(parts[2], out int i) ? i : -1;
                    Pawn now = null;
                    if (parts[0] == "leader") now = p?.leader;
                    else if (town != null) now = NotableBook.Instance?.notables.FirstOrDefault(n => n.town == town.ID && n.role == NotableRole.Head && n.Alive)?.pawn;
                    if (now == null) return;
                    if (now.thingIDNumber == pid) Then("then:still", 1f);
                    else
                    {
                        Then("then:changed", 3f);
                        mf.values["NOW"] = now.LabelShort;
                    }
                    break;
                }
                case "site":
                {
                    int id = parts.Length > 1 && int.TryParse(parts[1], out int i) ? i : -1;
                    bool there = Find.WorldObjects.Sites.Any(s => s.ID == id);
                    Then(there ? "then:still" : "then:over", there ? 1f : 2.5f);
                    break;
                }
                case "quest":
                {
                    // quest:id: still under way, or over (seen through, failed, or otherwise ended).
                    int id = parts.Length > 1 && int.TryParse(parts[1], out int i) ? i : -1;
                    var q = Find.QuestManager.QuestsListForReading.FirstOrDefault(x => x != null && x.id == id);
                    if (q == null || q.State == QuestState.NotYetAccepted) return;
                    if (q.State == QuestState.Ongoing)
                    {
                        Then("then:still", 1.5f);
                        break;
                    }
                    Then("then:over", 3f);
                    if (q.State == QuestState.EndedSuccess) Then("then:success", 3f);
                    else if (q.State == QuestState.EndedFailed) Then("then:failed", 3f);
                    break;
                }
                case "peace":
                {
                    // peace:factionId: the peace holds (still), or they are at war again (over).
                    var g = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => parts.Length > 1 && x.loadID.ToString() == parts[1]);
                    if (g == null || p == null || g.defeated) return;
                    if (p.HostileTo(g)) Then("then:over", 3f);
                    else Then("then:still", 1f);
                    break;
                }
                case "notable":
                {
                    // notable:id: alive still, or dead since.
                    int id = parts.Length > 1 && int.TryParse(parts[1], out int i) ? i : -1;
                    var n = NotableBook.Instance?.Get(id);
                    if (n == null) return;
                    Then(n.Alive ? "then:still" : "then:dead", n.Alive ? 0.5f : 3f);
                    break;
                }
            }
        }
    }
}
