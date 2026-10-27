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
            [PlayKind.Hunt] = new[] { "Council_Pool_PlayHuntInvite", "Council_Pool_PlayHunt", "Council_Journal_PlayHunt", "Council_Journal_PlayHuntMissed" },
            [PlayKind.Give] = new[] { "Council_Pool_PlayGiveInvite", "Council_Pool_PlayGive", "Council_Journal_PlayGive", "Council_Journal_PlayGiveMissed" },
            [PlayKind.Rounds] = new[] { "Council_Pool_PlayRoundsInvite", "Council_Pool_PlayRounds", "Council_Journal_PlayRounds", "Council_Journal_PlayRoundsMissed" },
            [PlayKind.Haul] = new[] { "Council_Pool_PlayHaulInvite", "Council_Pool_PlayHaul", "Council_Journal_PlayHaul", "Council_Journal_PlayHaulMissed" }
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
            // The new plays' own: a watch on the walls, a gift or a stash burned, rounds outdoors, the pyre or the tree line.
            if (p.patrol) tags.Add("walls");
            if (!p.mode.NullOrEmpty() && (p.kind == PlayKind.Give || p.kind == PlayKind.Rounds || p.kind == PlayKind.Haul)) tags.Add(p.mode);
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

        /// <summary>
        /// What those who were there learned from it, a sentence per skill ("Hector and Stace learned from it: intellectual
        /// +800 xp each."), or null when it taught nothing.
        /// </summary>
        public static string Learned(PlayOut p, List<Pawn> took)
        {
            if (p?.moods == null || took == null) return null;
            var parts = new List<string>();
            var taught = p.moods.Where(m => m?.skill != null && m.pawn != null && !m.pawn.Dead && took.Contains(m.pawn)
                                            && m.pawn.skills?.GetSkill(m.skill) is SkillRecord rec && !rec.TotallyDisabled);
            foreach (var g in taught.GroupBy(m => (m.skill, xp: Mathf.RoundToInt(m.xp))))
            {
                var who = g.Select(m => m.pawn).Distinct().ToList();
                string key = who.Count == 1 ? "CC_Play_End_LearnedOne" : "CC_Play_End_Learned";
                parts.Add(key.Translate(Names(who), g.Key.skill.label, g.Key.xp).Resolve());
            }
            return parts.Count == 0 ? null : string.Join(" ", parts);
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
                case PlayKind.Give:
                {
                    string what = PlayGive.Label(PlayGive.ThingOf(p));
                    if (p.mode == PlayGive.Burn) return "CC_Play_Begin_Burn".Translate(label, names, what).Resolve();
                    if (p.mode == PlayGive.Fetch) return "CC_Play_Begin_Fetch".Translate(label, names, what, (p.other?.LabelShort ?? "").Named("OTHER")).Resolve();
                    return "CC_Play_Begin_Give".Translate(label, names, what, where ?? p.other?.LabelShort ?? "").Resolve();
                }
                case PlayKind.Rounds: return "CC_Play_Begin_Rounds".Translate(label, names, where ?? "").Resolve();
                case PlayKind.Haul: return "CC_Play_Begin_Haul".Translate(label, names, p.things.Count, where ?? "").Resolve();
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
                // together: said by whoever leads it as it sets off, to no one in particular; the others are on their way to them.
                if (!line.NullOrEmpty()) Speech.Say(lead, Fill(line, p, lead), null, SpeechColor);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out invite", e);
            }
        }

        /// <summary>A line now and then while it goes on: the answer's own, or the kind's (said to <paramref name="toWhom"/> when given).</summary>
        public static void During(PlayOut p, Pawn speaker, Pawn toWhom = null)
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
                // together: said at the work party, the walk, the vigil, the search or the hunt itself, by one of those on it;
                // an errand's words go to whoever it's handed to, and only show when they're really together (Speech.Say checks).
                var to = toWhom != null && toWhom != speaker ? toWhom : p.kind == PlayKind.Carry && p.other != null && p.other != speaker ? p.other : null;
                if (!line.NullOrEmpty()) Speech.Say(speaker, Fill(line, p, speaker, to), to, SpeechColor);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out line", e);
            }
        }

        /// <summary>Fills in a spoken line's names ({OTHER}, the one it's about; {NAME}, what the answer called it).</summary>
        private static string Fill(string line, PlayOut p, Pawn speaker, Pawn to = null)
        {
            if (line.IndexOf('{') < 0) return line;
            var other = to ?? p.other ?? speaker;
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

        /// <summary>
        /// What an answer set going, and when ("The harvest dance is planned for this evening."): said when the answer is
        /// given, while its outcome waits for the end (<see cref="PlayOuts.TellWhenDone"/>).
        /// </summary>
        public static string Planned(PlayOut p)
        {
            string label = (p.label ?? "").CapitalizeFirst();
            if (label.NullOrEmpty()) return null;
            var map = p.map ?? Find.CurrentMap;
            int hour = map != null ? GenLocalDate.HourOfDay(map) : 12;
            int now = Find.TickManager.TicksGame;
            // Still within today's hours for it: today; else the next day it can.
            bool today = hour < p.hourTo - 1 && p.deadline - now > 0;
            string part = p.hourFrom >= 17 ? "Evening" : p.hourFrom >= 12 ? "Afternoon" : "Day";
            string when = ("CC_Play_When_" + (today ? "Today" : "Tomorrow") + part).Translate();
            // A thing carried over by hand: who takes what to whom. together: the giver walks it over to them (JobDriver_PlayOutGive).
            if (p.kind == PlayKind.Give && p.mode == PlayGive.Give && p.visitor != null && p.other != null)
                return (PlayMake.IsMaking(p) ? "CC_Play_Planned_Make" : "CC_Play_Planned_Give")
                    .Translate(p.visitor.Named("PAWN"), p.other.Named("OTHER"), PlayMake.Label(p).Named("THING"), when.Named("WHEN")).Resolve();
            return "CC_Play_Planned".Translate(label, when).Resolve();
        }

        /// <summary>
        /// The words at the end: the answer's own outcome when it waited for this (with how it went in brackets), else the
        /// kind's own end text.
        /// </summary>
        public static string EndWords(PlayOut p, string plain, string how = null)
        {
            if (p.outcome.NullOrEmpty()) return plain;
            return how.NullOrEmpty() ? p.outcome : p.outcome + " (" + how + ")";
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
                case PlayKind.Give:
                {
                    var giver = p.hands.FirstOrDefault()?.pawn;
                    string gn = giver?.LabelShort ?? names;
                    if (p.mode == PlayGive.Burn) return "CC_Play_End_Burn".Translate(gn, carried ?? "").Resolve();
                    if (p.mode == PlayGive.Fetch) return "CC_Play_End_Fetch".Translate(gn, carried ?? "", (p.other?.LabelShort ?? "").Named("OTHER")).Resolve();
                    // together: the giver walked it over and set it down at their feet (JobDriver_PlayOutGive).
                    return to != null ? "CC_Play_End_Give".Translate(gn, carried ?? "", to.LabelShort).Resolve() : "CC_Play_End".Translate(label).Resolve();
                }
                case PlayKind.Rounds:
                    return "CC_Play_End_Rounds".Translate(label, p.hands.FirstOrDefault()?.pawn?.LabelShort ?? names, p.tally).Resolve();
                case PlayKind.Haul:
                    return ("CC_Play_End_Haul_" + PlayHaul.Key(p.mode)).Translate(label, names, p.tally).Resolve();
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
                case PlayKind.Rounds: return "CC_Play_Note_Rounds".Translate(p.tally).Resolve();
                case PlayKind.Haul: return p.tally > 0 ? "CC_Play_Note_Haul".Translate(p.tally).Resolve() : null;
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
                    if (p.patrol) return "CC_Play_Report_Watch".Translate();
                    return p.to == PlaySpot.Person && p.other != null
                        ? "CC_Play_Report_VigilBeside".Translate(p.other.Named("OTHER")).Resolve()
                        : "CC_Play_Report_Vigil".Translate().ToString();
                case PlayKind.Search: return "CC_Play_Report_Search".Translate();
                case PlayKind.Rounds:
                {
                    var x2 = PlayRounds.Current(p, p.Hand(x));
                    return x2 != null ? "CC_Play_Report_Rounds".Translate(x2.Named("OTHER")).Resolve() : null;
                }
                default: return null;
            }
        }
    }
}
