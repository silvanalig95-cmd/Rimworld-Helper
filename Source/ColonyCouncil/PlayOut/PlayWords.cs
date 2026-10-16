using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What's said about a work party, a walk together, a vigil, a search, an errand or a hunt: the message when it
    /// begins and when it's over, a line over the head of whoever calls the others to it (the invitation), a line now
    /// and then while it goes on, a journal entry in their own voice afterwards, and one from whoever was waiting for it
    /// if it never came about. The lines are toned flavor pools (Defs/FlavorPoolDefs/Rimfolk_PlayOut.xml), picked by the
    /// speaker's tone and tagged by the kind of work, the place, the season and the hour.
    /// </summary>
    public static class PlayWords
    {
        private static readonly Color SpeechColor = new Color(1f, 0.93f, 0.72f);

        private const int Invitation = 0, While = 1, Afterwards = 2, FellThrough = 3;

        /// <summary>Each kind's pools: the call to it, lines while it goes on, the journal afterwards, the journal when it never came about.</summary>
        private static readonly Dictionary<PlayKind, string[]> Pools = new Dictionary<PlayKind, string[]>
        {
            [PlayKind.WorkParty] = new[] { "Council_Pool_PlayWorkInvite", "Council_Pool_PlayWork", "Council_Journal_PlayWork", "Council_Journal_PlayWorkMissed" },
            [PlayKind.Procession] = new[] { "Council_Pool_PlayWalkInvite", "Council_Pool_PlayWalk", "Council_Journal_PlayWalk", "Council_Journal_PlayWalkMissed" },
            [PlayKind.Vigil] = new[] { "Council_Pool_PlayVigilInvite", "Council_Pool_PlayVigil", "Council_Journal_PlayVigil", "Council_Journal_PlayVigilMissed" },
            [PlayKind.Search] = new[] { "Council_Pool_PlaySearchInvite", "Council_Pool_PlaySearch", "Council_Journal_PlaySearch", "Council_Journal_PlaySearchMissed" },
            [PlayKind.Carry] = new[] { "Council_Pool_PlayCarryInvite", "Council_Pool_PlayCarry", "Council_Journal_PlayCarry", "Council_Journal_PlayCarryMissed" },
            [PlayKind.Hunt] = new[] { "Council_Pool_PlayHuntInvite", "Council_Pool_PlayHunt", "Council_Journal_PlayHunt", "Council_Journal_PlayHuntMissed" }
        };

        private static string Pool(PlayKind k, int stage) => Pools.TryGetValue(k, out var names) ? names[stage] : null;

        /// <summary>The tags its lines are picked by: the kind of work, the place.</summary>
        public static string[] Tags(PlayOut p, Pawn speaker = null)
        {
            var tags = new List<string>();
            foreach (var t in p.tasks) tags.Add(t.ToString().ToLowerInvariant());
            switch (p.to)
            {
                case PlaySpot.Grave: tags.Add("grave"); break;
                case PlaySpot.Gate: tags.Add("gate"); break;
                case PlaySpot.Fields: tags.Add("fields"); break;
                case PlaySpot.Water: tags.Add("water"); break;
                case PlaySpot.Camp: tags.Add("camp"); break;
                case PlaySpot.Person: tags.Add("beside"); break;
                case PlaySpot.Walk: tags.Add("walkout"); break;
            }
            if (p.other != null && p.other != speaker) tags.Add("other");
            return tags.ToArray();
        }

        /// <summary>Names for a message: up to three, then how many more.</summary>
        public static string Names(List<Pawn> who)
        {
            var named = who.Where(x => x != null).Select(x => x.LabelShort).Distinct().ToList();
            if (named.Count == 0) return "CC_Play_Everyone".Translate();
            if (named.Count <= 3) return TextUtil.JoinAnd(named);
            return "CC_Play_NamesAndMore".Translate(string.Join(", ", named.Take(2)), named.Count - 2).Resolve();
        }

        /// <summary>What a work party does, in words ("cleaning and hauling").</summary>
        public static string TaskWords(PlayOut p) =>
            TextUtil.JoinAnd(p.tasks.Distinct().Select(t => ("CC_Play_Task_" + t).Translate().ToString()).ToList());

        private static Pawn Lead(PlayOut p)
        {
            if (p.organizer != null && p.Hand(p.organizer) != null) return p.organizer;
            return p.hands.FirstOrDefault()?.pawn;
        }

        public static string BeginText(PlayOut p, string where)
        {
            string label = (p.label ?? "").CapitalizeFirst();
            string names = Names(p.hands.Select(h => h.pawn).ToList());
            switch (p.kind)
            {
                case PlayKind.WorkParty: return "CC_Play_Begin_WorkParty".Translate(label, names, TaskWords(p)).Resolve();
                case PlayKind.Procession: return "CC_Play_Begin_Procession".Translate(label, names, where ?? "").Resolve();
                case PlayKind.Vigil: return "CC_Play_Begin_Vigil".Translate(label, names, where ?? "").Resolve();
                case PlayKind.Search: return "CC_Play_Begin_Search".Translate(label, names).Resolve();
                case PlayKind.Carry:
                    return "CC_Play_Begin_Carry".Translate(label, names, PlayCarry.Label(p.what, p.thing), where ?? "CC_Play_To_Gate".Translate().ToString()).Resolve();
                case PlayKind.Hunt:
                    return "CC_Play_Begin_HuntParty".Translate(label, names, TextUtil.JoinAnd(p.game.Where(a => a != null).Select(a => a.LabelShort).ToList())).Resolve();
                default: return label;
            }
        }

        /// <summary>The call to it: whoever leads says something as they set off (only seen with the camera there).</summary>
        public static void Invite(PlayOut p)
        {
            try
            {
                var lead = Lead(p);
                string pool = Pool(p.kind, Invitation);
                if (lead == null || pool == null) return;
                string line = Flavor.FromPool(pool, lead, null, null, Tags(p, lead));
                if (!line.NullOrEmpty()) CouncilGameComponent.Speak(lead, Fill(line, p, lead), SpeechColor);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out invite", e);
            }
        }

        /// <summary>A line now and then while it goes on: the answer's own, or the kind's.</summary>
        public static void During(PlayOut p, Pawn speaker)
        {
            if (speaker == null || !speaker.Spawned || Find.CurrentMap != speaker.Map) return;
            try
            {
                string line = null;
                if (!p.lines.NullOrEmpty() && Rand.Chance(0.5f)) line = p.lines.RandomElement();
                if (line.NullOrEmpty())
                {
                    string pool = !p.pool.NullOrEmpty() ? p.pool : Pool(p.kind, While);
                    if (pool != null) line = Flavor.FromPool(pool, speaker, null, null, Tags(p, speaker));
                }
                if (!line.NullOrEmpty()) CouncilGameComponent.Speak(speaker, Fill(line, p, speaker), SpeechColor);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out line", e);
            }
        }

        /// <summary>Fills in a spoken line's names ({OTHER}, the one it's about; {NAME}, what the answer called it).</summary>
        private static string Fill(string line, PlayOut p, Pawn speaker)
        {
            if (line.IndexOf('{') < 0) return line;
            var other = p.other ?? speaker;
            return line.Formatted(speaker.Named("PAWN"), other.Named("OTHER"), (p.label ?? "").Named("NAME")).Resolve();
        }

        /// <summary>Afterwards: whoever led it, and now and then one more of those who took part, write it in their journal.</summary>
        public static void After(PlayOut p, List<Pawn> took)
        {
            var journals = CouncilGameComponent.Instance?.journals;
            string pool = Pool(p.kind, Afterwards);
            if (journals == null || pool == null || took.NullOrEmpty()) return;
            try
            {
                var first = took.Contains(Lead(p)) ? Lead(p) : took[0];
                Write(journals, first, pool, p);
                var second = took.Where(x => x != first).RandomElementWithFallback();
                if (second != null && Rand.Chance(0.4f)) Write(journals, second, pool, p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out journal", e);
            }
        }

        /// <summary>It never came about: whoever it was for (or would have led it) writes that down.</summary>
        public static void Missed(PlayOut p)
        {
            var journals = CouncilGameComponent.Instance?.journals;
            string pool = Pool(p.kind, FellThrough);
            if (journals == null || pool == null) return;
            try
            {
                var who = p.organizer ?? p.named.FirstOrDefault() ?? p.visitor ?? p.invited.FirstOrDefault();
                if (who == null || who.Dead || !PlayOuts.OurPerson(who)) return;
                Write(journals, who, pool, p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out journal missed", e);
            }
        }

        private static void Write(JournalTracker journals, Pawn x, string pool, PlayOut p)
        {
            if (x == null || x.Dead) return;
            var other = p.other != null && p.other != x ? p.other : x;
            journals.WriteTagged(x, pool, Tags(p, x), null, other.Named("OTHER"), (p.label ?? "").Named("NAME"));
        }

        public static string EndText(PlayOut p, bool early, List<Pawn> took, List<Thing> found, Pawn to, string carried)
        {
            string label = (p.label ?? "").CapitalizeFirst();
            if (early) return "CC_Play_End_Early".Translate(label).Resolve();
            string names = Names(took);
            switch (p.kind)
            {
                case PlayKind.WorkParty:
                    return p.tally > 0 ? "CC_Play_End_WorkParty".Translate(label, p.tally).Resolve() : "CC_Play_End".Translate(label).Resolve();
                case PlayKind.Procession: return "CC_Play_End_Procession".Translate(label, names).Resolve();
                case PlayKind.Vigil: return "CC_Play_End_Vigil".Translate(label, names).Resolve();
                case PlayKind.Search:
                    return !found.NullOrEmpty()
                        ? "CC_Play_End_SearchFound".Translate(label, took[0].LabelShort, TextUtil.JoinAnd(found.Select(f => f.LabelCap.ToString()).ToList())).Resolve()
                        : "CC_Play_End_Search".Translate(label, names).Resolve();
                case PlayKind.Carry:
                    return to != null
                        ? "CC_Play_End_CarryTo".Translate(names, carried ?? PlayCarry.Label(p.what, p.thing), to.LabelShort).Resolve()
                        : "CC_Play_End_Carry".Translate(names, carried ?? PlayCarry.Label(p.what, p.thing)).Resolve();
                case PlayKind.Hunt:
                    return "CC_Play_End_HuntParty".Translate(label, TextUtil.JoinAnd(p.game.Where(a => a != null && a.Dead).Select(a => a.LabelShort).ToList())).Resolve();
                default: return "CC_Play_End".Translate(label).Resolve();
            }
        }

        /// <summary>A short word on how it went, for the dilemmas page ("12 jobs done", "3 kept watch").</summary>
        public static string Note(PlayOut p, List<Pawn> took)
        {
            if (took.NullOrEmpty()) return null;
            switch (p.kind)
            {
                case PlayKind.WorkParty: return p.tally > 0 ? "CC_Play_Note_Work".Translate(p.tally).Resolve() : null;
                case PlayKind.Procession: return "CC_Play_Note_Walk".Translate(took.Count).Resolve();
                case PlayKind.Vigil: return "CC_Play_Note_Vigil".Translate(took.Count).Resolve();
                case PlayKind.Search: return "CC_Play_Note_Searched".Translate(took.Count).Resolve();
                case PlayKind.Hunt: return p.tally > 0 ? "CC_Play_Note_Hunt".Translate(p.tally).Resolve() : null;
                default: return null;
            }
        }

        /// <summary>Why nobody took part, when it ended with nobody counted.</summary>
        public static string WhyNot(PlayOut p)
        {
            switch (p.kind)
            {
                case PlayKind.WorkParty: return (p.tally > 0 ? "CC_Play_Why_CutShort" : "CC_Play_Why_NoWork").Translate();
                case PlayKind.Hunt: return "CC_Play_Why_GotAway".Translate();
                default: return "CC_Play_Why_CutShort".Translate();
            }
        }

        /// <summary>What they're doing, for the inspect pane.</summary>
        public static string Report(PlayOut p, Pawn x)
        {
            if (p == null) return null;
            switch (p.kind)
            {
                case PlayKind.Procession:
                    switch (p.phase)
                    {
                        case 0: return "CC_Play_Report_WalkGather".Translate();
                        case 1: return "CC_Play_Report_WalkOut".Translate();
                        case 2: return "CC_Play_Report_WalkThere".Translate();
                        default: return "CC_Play_Report_WalkBack".Translate();
                    }
                case PlayKind.Vigil:
                    return p.to == PlaySpot.Person && p.other != null
                        ? "CC_Play_Report_VigilBeside".Translate(p.other.Named("OTHER")).Resolve()
                        : "CC_Play_Report_Vigil".Translate().ToString();
                case PlayKind.Search: return "CC_Play_Report_Search".Translate();
                default: return null;
            }
        }
    }
}
