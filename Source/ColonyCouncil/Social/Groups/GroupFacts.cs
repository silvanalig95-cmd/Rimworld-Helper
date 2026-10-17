using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What the game says about the colony's people for their groups, read once a day: who pulls towards whom
    /// (<see cref="PairFacts"/> into an affinity table for <see cref="GroupMath"/>), what binds a set of them (the kind of
    /// group, and so its name), and what each brings to a role (<see cref="RoleFacts"/>).
    /// </summary>
    public class GroupFacts
    {
        public readonly List<Pawn> people;
        public readonly Dictionary<Pawn, int> index = new Dictionary<Pawn, int>();
        public readonly float[,] aff;
        public readonly float[][] values;
        public readonly int[] arrived;
        public readonly bool[] young, old, night, devout;
        public readonly Ideo[] ideo;
        public readonly HashSet<string>[] origins;
        public readonly HashSet<string>[] trades;
        public readonly WorkLog[] logs;
        /// <summary>The colony holds more than one faith.</summary>
        public readonly bool faithDivides;
        public readonly SocialWeb web;

        private static int Day => GenDate.TicksPerDay;

        public int Count => people.Count;

        public int IndexOf(Pawn p) => p != null && index.TryGetValue(p, out int i) ? i : -1;

        public GroupFacts(List<Pawn> pawns, SocialWeb web, GroupTracker tracker)
        {
            this.web = web;
            people = pawns;
            int n = pawns.Count;
            aff = new float[n, n];
            values = new float[n][];
            arrived = new int[n];
            young = new bool[n];
            old = new bool[n];
            night = new bool[n];
            devout = new bool[n];
            ideo = new Ideo[n];
            origins = new HashSet<string>[n];
            trades = new HashSet<string>[n];
            logs = new WorkLog[n];
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < n; i++)
            {
                var p = pawns[i];
                index[p] = i;
                values[i] = Persona.Values(p);
                int asColonist = p.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? now;
                arrived[i] = Mathf.Max(0, now - asColonist);
                float years = p.ageTracker?.AgeBiologicalYearsFloat ?? 30f;
                young[i] = years < 18f;
                old[i] = years >= 58f;
                logs[i] = tracker?.WorkOf(p, false);
                night[i] = NightShift(p, logs[i]);
                if (ModsConfig.IdeologyActive && p.Ideo != null)
                {
                    ideo[i] = p.Ideo;
                    devout[i] = (p.ideo?.Certainty ?? 0f) >= 0.6f;
                }
                origins[i] = Origins(p);
                trades[i] = Trades(p, logs[i]);
            }
            faithDivides = ideo.Where(x => x != null).Distinct().Count() > 1;
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                float a = 0f;
                try
                {
                    a = GroupMath.Affinity(Pair(i, j));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("groups: affinity", e);
                }
                aff[i, j] = aff[j, i] = a;
            }
        }

        // ---------------- Pairs ----------------

        public PairFacts Pair(int i, int j)
        {
            var a = people[i];
            var b = people[j];
            var f = new PairFacts();
            if (web != null)
            {
                f.friends = Announced(a, b, TieKind.Friends);
                f.rivals = Announced(a, b, TieKind.Rivals);
                f.comrades = Announced(a, b, TieKind.Comrades);
                f.mentor = web.Get(a, b, TieKind.Mentor) != null;
            }
            if (a.relations != null && b.relations != null)
            {
                f.minOpinion = Mathf.Min(a.relations.OpinionOf(b), b.relations.OpinionOf(a));
                f.lovers = LovePartnerRelationUtility.LovePartnerRelationExists(a, b);
                f.family = a.relations.FamilyByBlood.Contains(b);
            }
            f.sharedWork = SharedWork(i, j);
            f.nightShift = night[i] && night[j];
            f.likeness = PersonaMath.Likeness(values[i], values[j]);
            f.sameFaith = ideo[i] != null && ideo[i] == ideo[j] && devout[i] && devout[j]
                          && (faithDivides || ((a.ideo?.Certainty ?? 0f) >= 0.85f && (b.ideo?.Certainty ?? 0f) >= 0.85f));
            f.arrivedTogether = Math.Abs(arrived[i] - arrived[j]) < Day / 2;
            f.sameOrigin = origins[i].Overlaps(origins[j]);
            f.bothYoung = young[i] && young[j];
            f.bothOld = old[i] && old[j];
            return f;
        }

        private float Announced(Pawn a, Pawn b, TieKind kind)
        {
            var t = web.Get(a, b, kind);
            return t != null && t.announced ? t.strength : 0f;
        }

        /// <summary>How much of their days go to the same work: from what they've been seen doing, or their trades while there's little to go on.</summary>
        public float SharedWork(int i, int j)
        {
            var la = logs[i];
            var lb = logs[j];
            if (la != null && lb != null && la.Total >= 6f && lb.Total >= 6f)
            {
                float s = 0f;
                foreach (var k in la.kinds) s += Mathf.Min(la.Share(k), lb.Share(k));
                return Mathf.Clamp01(s);
            }
            var ta = trades[i];
            var tb = trades[j];
            if (ta.Count == 0 || tb.Count == 0) return 0f;
            int both = ta.Count(tb.Contains);
            return 0.7f * both / Mathf.Max(ta.Count, tb.Count);
        }

        /// <summary>Their trades: work set first in their priorities, or the work of the skills they love.</summary>
        private static HashSet<string> Trades(Pawn p, WorkLog log)
        {
            var set = new HashSet<string>();
            if (log != null && log.Total >= 6f)
            {
                foreach (var k in log.kinds)
                    if (log.Share(k) >= 0.2f)
                        set.Add(k);
                return set;
            }
            if (p.workSettings == null || !p.workSettings.EverWork) return set;
            bool manual = Find.PlaySettings?.useWorkPriorities ?? false;
            foreach (var wt in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (wt.relevantSkills.NullOrEmpty() || p.WorkTypeIsDisabled(wt)) continue;
                if (manual)
                {
                    int pr = p.workSettings.GetPriority(wt);
                    if (pr == 1) set.Add(wt.defName);
                }
                else if (wt.relevantSkills.Any(s => p.skills?.GetSkill(s) is SkillRecord r && !r.TotallyDisabled && People.Passionate(r) && r.Level >= 6))
                    set.Add(wt.defName);
            }
            return set;
        }

        /// <summary>Keeps the night hours: seen up and about at night lately, or their schedule sleeps them through the day.</summary>
        private static bool NightShift(Pawn p, WorkLog log)
        {
            if (log != null && log.night >= 15f) return true;
            var tt = p.timetable;
            if (tt == null) return false;
            int daySleep = 0;
            for (int h = 9; h <= 17; h++)
                if (tt.GetAssignment(h) == TimeAssignmentDefOf.Sleep)
                    daySleep++;
            return daySleep >= 4;
        }

        /// <summary>The kinds of life they had before (backstory spawn categories: Tribal, Offworld...).</summary>
        private static HashSet<string> Origins(Pawn p)
        {
            var set = new HashSet<string>();
            var st = p.story;
            if (st == null) return set;
            void Add(BackstoryDef b)
            {
                if (b?.spawnCategories == null) return;
                foreach (var c in b.spawnCategories)
                    if (c == "Tribal" || c == "Offworld")
                        set.Add(c);
            }
            Add(st.Childhood);
            Add(st.Adulthood);
            // Tribe-born only counts in a colony that isn't a tribe; off-world only in one that hasn't seen machines.
            if (Avail.EffectiveTech <= TechLevel.Neolithic) set.Remove("Tribal");
            if (!Avail.IsPreIndustrial) set.Remove("Offworld");
            return set;
        }

        // ---------------- What binds a set of them ----------------

        /// <summary>The colony's first day: those who came then are its founders.</summary>
        private bool Founder(int i) => arrived[i] < Day;

        /// <summary>Whether one member fits what a kind of group is about.</summary>
        public bool Fits(GroupKindDef kind, int i, IList<int> members)
        {
            switch (kind.binds)
            {
                case GroupBinds.Work:
                {
                    float share = 0f;
                    var log = logs[i];
                    foreach (var w in kind.Works)
                    {
                        if (log != null && log.Total >= 6f) share += log.Share(w.defName);
                        else if (trades[i].Contains(w.defName)) share += 0.5f;
                    }
                    return share >= 0.25f;
                }
                case GroupBinds.NightShift: return night[i];
                case GroupBinds.Battle:
                {
                    int with = 0, others = 0;
                    foreach (int m in members)
                    {
                        if (m == i) continue;
                        others++;
                        if (Announced(people[i], people[m], TieKind.Comrades) >= 20f) with++;
                    }
                    return others > 0 && with * 2 >= others;
                }
                case GroupBinds.Elders: return old[i];
                case GroupBinds.Young: return young[i];
                case GroupBinds.Faith:
                {
                    var main = MainIdeo(members);
                    return main != null && ideo[i] == main && devout[i];
                }
                case GroupBinds.Founders: return Founder(i);
                case GroupBinds.Arrived:
                {
                    if (Founder(i)) return false;
                    int with = members.Count(m => m != i && Math.Abs(arrived[m] - arrived[i]) < Day / 2);
                    return with * 2 >= members.Count - 1;
                }
                case GroupBinds.Origin: return !kind.origin.NullOrEmpty() && origins[i].Contains(kind.origin);
                case GroupBinds.Likeness:
                {
                    var axis = kind.Axis;
                    if (axis == null) return false;
                    float v = values[i][(int)axis.Value];
                    return kind.low ? v <= -kind.atLeast : v >= kind.atLeast;
                }
                default:
                {
                    foreach (int m in members)
                        if (m != i && Announced(people[i], people[m], TieKind.Friends) >= 20f)
                            return true;
                    return false;
                }
            }
        }

        private Ideo MainIdeo(IList<int> members) =>
            members.Select(m => ideo[m]).Where(x => x != null).GroupBy(x => x).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key;

        /// <summary>
        /// The kind of group a set of people make: among the kinds that fit enough of them, the most particular (lowest
        /// order), then the one that fits most of them. Friends (named after the anchor) when nothing else does.
        /// </summary>
        public GroupKindDef KindFor(IList<int> members, out float share)
        {
            GroupKindDef best = null;
            share = 0f;
            foreach (var k in GroupKindDef.All)
            {
                if (k.binds == GroupBinds.Faith && !ModsConfig.IdeologyActive) continue;
                if (k.binds == GroupBinds.Work && k.Works.Count == 0) continue;
                float s = members.Count(m => Fits(k, m, members)) / (float)Mathf.Max(1, members.Count);
                if (s < k.minShare) continue;
                if (best == null || k.order < best.order || (k.order == best.order && s > share))
                {
                    best = k;
                    share = s;
                }
            }
            return best ?? GroupKindDef.All.Where(k => k.binds == GroupBinds.Friends).OrderBy(k => k.order).FirstOrDefault();
        }

        // ---------------- Roles ----------------

        public RoleFacts Role(SocialGroup g, GroupMember m, int day)
        {
            var p = m.pawn;
            int i = IndexOf(p);
            var v = i >= 0 ? values[i] : Persona.Values(p);
            var f = new RoleFacts
            {
                warmth = v[(int)PersonaAxis.Warmth],
                nerve = v[(int)PersonaAxis.Nerve],
                temper = v[(int)PersonaAxis.Temper],
                humour = v[(int)PersonaAxis.Humour],
                trust = v[(int)PersonaAxis.Trust],
                pride = v[(int)PersonaAxis.Pride],
                openness = v[(int)PersonaAxis.Openness],
                outlook = v[(int)PersonaAxis.Outlook]
            };
            var social = p.skills?.GetSkill(SkillDefOf.Social);
            f.social = social == null || social.TotallyDisabled ? 2 : social.Level;
            int others = 0, friends = 0;
            foreach (var o in g.members)
            {
                if (o == m || o.pawn == null) continue;
                others++;
                if (web != null && Announced(p, o.pawn, TieKind.Friends) >= 20f) friends++;
            }
            f.tieShare = others == 0 ? 0f : friends / (float)others;
            var rec = DefRefs.Named<RecordDef>(CouncilRecords.QuarrelsMended);
            f.mended = rec != null && p.records != null ? p.records.GetAsInt(rec) : 0;
            f.newcomer = day - m.joined / Day < GroupMath.NewcomerDays && (m.joined - g.formed) / Day >= GroupMath.NewcomerDays;
            return f;
        }
    }
}
