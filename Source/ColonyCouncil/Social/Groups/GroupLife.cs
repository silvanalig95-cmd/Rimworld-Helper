using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The life of the colony's groups, once a day with the social web (SocialWeb.Daily): the dead and the departed
    /// leave them, people who no longer hold to the rest drift off, newcomers find their way in, each group grows closer
    /// or looser (cohesion), its people settle into roles, its ways and shared views take shape and pull its members a
    /// little towards them, groups fall out with each other, a loose group with a rift splits, two that overlap merge,
    /// an empty one ends, people join the group they hold to, and new groups form out of what binds people (friendship,
    /// work, a fight, the night hours, faith, where they came from, age, being alike). The first day on a save finds the
    /// groups already there quietly; after that a founding, a split, a merge and an end are each told once.
    /// See DESIGN.md ("Layer 8: Social life", Groups) and the Almanac pages "Groups" and "Roles in a group".
    /// </summary>
    public static partial class GroupLife
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Groups are on: "Personalities and social life" and the social web, in a game.</summary>
        public static bool Active => Persona.On && (S?.socialWeb ?? false) && Current.Game != null && GroupTracker.Instance != null;

        /// <summary>Someone who can be in a group: one of the colony's own with an inner life, not a baby or a lodger.</summary>
        public static bool Eligible(Pawn p) =>
            People.HasInnerLife(p) && p.IsColonist && !p.IsQuestLodger() && p.RaceProps.Humanlike && !p.DevelopmentalStage.Baby();

        private static GroupTracker Tracker => GroupTracker.Instance;
        private static int Now => Find.TickManager.TicksGame;
        private const int Day = GenDate.TicksPerDay;
        private static int Today => Now / Day;

        // ---------------- The social web's circles, built from the groups ----------------

        private static readonly Dictionary<int, Clique> cliqueById = new Dictionary<int, Clique>();
        private static int cliqueVersion = -1;
        private static GroupTracker cliqueTracker;

        /// <summary>The groups as the social web's circles (for the older code that asks about circles), one per group.</summary>
        public static List<Clique> BuildCliques()
        {
            cliqueById.Clear();
            var list = new List<Clique>();
            var t = Tracker;
            cliqueTracker = t;
            cliqueVersion = t?.version ?? -1;
            if (t == null) return list;
            foreach (var g in t.groups.OrderBy(x => x.formed))
            {
                var c = new Clique { name = g.Name, members = g.Pawns.Where(p => !p.Dead).ToList() };
                if (c.members.Count < 2) continue;
                cliqueById[g.id] = c;
                list.Add(c);
            }
            return list;
        }

        /// <summary>Someone's circle: their first group's.</summary>
        public static Clique CliqueOf(Pawn p)
        {
            var g = Groups.Primary(p);
            if (g == null) return null;
            // Built again only when membership has changed since (a group of one has no circle, and isn't asked about again).
            var t = Tracker;
            if (!cliqueById.TryGetValue(g.id, out var c) && (cliqueTracker != t || cliqueVersion != (t?.version ?? -1)))
                CouncilGameComponent.Instance?.social?.RebuildCliques();
            return cliqueById.TryGetValue(g.id, out c) ? c : null;
        }

        // ---------------- The day ----------------

        public static void Daily(CouncilGameComponent comp, SocialWeb web, List<Pawn> pawns)
        {
            var t = Tracker;
            if (t == null || comp == null) return;
            bool quiet = !t.seeded;
            GroupFacts facts;
            try
            {
                facts = new GroupFacts(pawns, web, t);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("groups: facts", e);
                return;
            }
            Step("groups: members", () => Members(comp, t, facts, quiet));
            Step("groups: newcomers", () => GroupNewcomers.Daily(comp, t, facts, quiet));
            foreach (var g in t.groups.ToList())
            {
                Step("groups: cohesion", () => Cohesion(g, facts));
                Step("groups: roles", () => Roles(g, facts, quiet));
                Step("groups: ways", () => Norms(g, facts, quiet));
                Step("groups: views", () => Views(g, facts));
                Step("groups: conformity", () => Conformity(g, facts));
            }
            Step("groups: rivalries", () => Rivalries(comp, t, facts, quiet));
            Step("groups: splits", () => Splits(comp, t, facts, quiet));
            Step("groups: merges", () => Merges(comp, t, facts, quiet));
            Step("groups: ends", () => Ends(comp, t, web, quiet));
            Step("groups: joining", () => Joins(comp, t, facts, quiet));
            Step("groups: forming", () => Formation(comp, t, facts, quiet));
            Step("groups: habits", () => GroupHabits.Daily(t, facts));
            Step("groups: left out", () => LeftOut(t, facts, web));
            foreach (var g in t.groups)
                g.todayHabits = g.todayQuarrels = g.todayMends = g.todayMemories = g.todayDied = g.todayLeft = 0;
            foreach (var log in t.work) log.Fade();
            t.seeded = true;
            t.Changed();
            web.RebuildCliques();
        }

        private static void Step(string context, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce(context, e);
            }
        }

        /// <summary>The members of a group who are here today (on a home map), as indices into the day's facts.</summary>
        public static List<int> Present(SocialGroup g, GroupFacts f)
        {
            var list = new List<int>();
            foreach (var m in g.members)
            {
                int i = f.IndexOf(m.pawn);
                if (i >= 0) list.Add(i);
            }
            return list;
        }

        /// <summary>Away, not gone: in a caravan, a guest elsewhere, carried off by raiders.</summary>
        private static bool Away(Pawn p)
        {
            if (p == null || p.Dead || p.Destroyed) return false;
            if (p.IsColonist) return true;
            return Find.FactionManager.AllFactionsListForReading.Any(f => f.kidnapped != null && f.kidnapped.KidnappedPawnsListForReading.Contains(p));
        }

        // ---------------- Who is in it ----------------

        private static void Members(CouncilGameComponent comp, GroupTracker t, GroupFacts f, bool quiet)
        {
            foreach (var g in t.groups)
            {
                foreach (var m in g.members.ToList())
                {
                    var p = m.pawn;
                    m.name = p?.LabelShort ?? m.name;
                    if (p == null || p.Dead)
                    {
                        g.members.Remove(m);
                        g.todayDied++;
                        g.Former(m.name);
                        if (!quiet) g.Note("CC_GroupNote_Died".Translate(m.name ?? "?").Resolve(), true);
                        Remember(comp, g, m.name, "Rimfolk_Journal_Group_LostOne");
                        continue;
                    }
                    if (f.IndexOf(p) < 0)
                    {
                        if (Away(p)) continue;
                        g.members.Remove(m);
                        g.todayLeft++;
                        g.Former(m.name);
                        if (!quiet) g.Note("CC_GroupNote_Gone".Translate(p.Named("PAWN")).Resolve(), true);
                        continue;
                    }
                }
                var present = Present(g, f);
                // Fewer than two of them at home together: the group goes on, but it feels it after a few days.
                g.apartDays = present.Count < 2 ? g.apartDays + 1 : 0;
                foreach (var m in g.members.ToList())
                {
                    int i = f.IndexOf(m.pawn);
                    if (i < 0) continue;
                    m.attachment = GroupMath.Attachment(f.aff, i, present);
                    m.lowDays = m.attachment < GroupMath.LeaveBelow && present.Count >= 2 ? m.lowDays + 1 : 0;
                    if (m.lowDays < GroupMath.LeaveDays || g.members.Count <= 2) continue;
                    // Drifted off: they don't hold to the rest any more.
                    g.members.Remove(m);
                    g.todayLeft++;
                    g.Former(m.name);
                    t.Changed();
                    g.Note("CC_GroupNote_DriftedOff".Translate(m.pawn.Named("PAWN")).Resolve());
                    if (!quiet)
                    {
                        News.Post(GroupText.Sentences("CC_Group_DriftedOff".Translate(m.pawn.Named("PAWN"), g.Name.Named("GROUP")).Resolve()), new LookTargets(m.pawn), NewsKind.People,
                            MessageTypeDefOf.SilentInput, Importance.Flavor);
                        GroupText.Journal(m.pawn, "Rimfolk_Journal_Group_DriftedOff", g.Name.Named("GROUP"));
                    }
                }
            }
            t.Changed();
        }

        /// <summary>A member lost: one of those left writes about it (the anchor first).</summary>
        private static void Remember(CouncilGameComponent comp, SocialGroup g, string name, string pool)
        {
            if (!Rand.Chance(0.6f)) return;
            var writer = g.Holder(GroupRole.Anchor) ?? g.Pawns.Where(p => !p.Dead).RandomElementWithFallback();
            if (writer == null || writer.Dead) return;
            GroupText.Journal(writer, pool, g.Name.Named("GROUP"), (name ?? "?").Named("NAME"));
        }

        // ---------------- How close it is ----------------

        private static void Cohesion(SocialGroup g, GroupFacts f)
        {
            var present = Present(g, f);
            var d = new CohesionDay
            {
                attachment = present.Count >= 2 ? GroupMath.Mean(f.aff, present) : 0.2f,
                habits = g.todayHabits,
                died = g.todayDied,
                left = g.todayLeft,
                apart = present.Count < 2,
                apartDays = g.apartDays
            };
            // What the members went through together today, and how things stood between them (frictions, making up).
            var tones = CouncilGameComponent.Instance?.social?.tones;
            int now = Now;
            for (int x = 0; x < present.Count; x++)
            for (int y = x + 1; y < present.Count; y++)
            {
                var a = f.people[present[x]];
                var b = f.people[present[y]];
                foreach (var mem in SharedMemories.Of(a, b))
                    if (now - mem.tick < Day)
                        d.memories++;
                var tone = tones?.Get(a, b, false);
                if (tone == null) continue;
                if (tone.friction >= 0 && now - tone.friction < Day) d.quarrels++;
                if (tone.mend >= 0 && now - tone.mend < Day) d.mends++;
            }
            d.quarrels += g.todayQuarrels;
            d.mends += g.todayMends;
            g.cohesion = GroupMath.NextCohesion(g.cohesion, d);
            // Someone who goes against the group's ways wears on it a little.
            foreach (int i in present)
                if (BreaksAny(g, f.values[i]) != null)
                    g.cohesion = Mathf.Max(0f, g.cohesion - 0.5f);
            g.lowDays = g.cohesion < GroupMath.SplitBelow ? g.lowDays + 1 : 0;
        }

        // ---------------- Roles ----------------

        private static void Roles(SocialGroup g, GroupFacts f, bool quiet)
        {
            var here = g.members.Where(m => f.IndexOf(m.pawn) >= 0).ToList();
            if (here.Count == 0) return;
            int today = Today;
            var facts = here.Select(m => f.Role(g, m, today)).ToArray();
            var current = here.Select(m => new RoleState { role = m.role, pending = m.pending, pendingDays = m.pendingDays }).ToArray();
            var next = GroupMath.AssignRoles(facts, current);
            for (int i = 0; i < here.Count; i++)
            {
                var m = here[i];
                var was = m.role;
                m.role = next[i].role;
                m.pending = next[i].pending;
                m.pendingDays = next[i].pendingDays;
                if (m.role == was) continue;
                m.roleSince = Now;
                if (quiet || m.role == GroupRole.None || m.role == GroupRole.Newcomer || was == GroupRole.Newcomer && m.role == GroupRole.None) continue;
                g.Note("CC_GroupNote_Role".Translate(m.pawn.Named("PAWN"), GroupText.Role(m.role).Named("ROLE")).Resolve());
            }
        }

        // ---------------- Ways ----------------

        /// <summary>The members' average on one side of their character.</summary>
        private static float Mean(GroupFacts f, List<int> present, PersonaAxis axis)
        {
            if (present.Count == 0) return 0f;
            float s = 0f;
            foreach (int i in present) s += f.values[i][(int)axis];
            return s / present.Count;
        }

        private static void Norms(SocialGroup g, GroupFacts f, bool quiet)
        {
            var present = Present(g, f);
            if (present.Count < 2) return;
            foreach (var n in GroupNormDef.All)
            {
                bool held = g.norms.Contains(n.defName);
                bool holds = Holds(g, n, f, present, held);
                if (holds == held) continue;
                if (holds)
                {
                    if (g.norms.Count >= 3) continue;
                    g.norms.Add(n.defName);
                    if (!quiet) g.Note("CC_GroupNote_Norm".Translate(n.label.Named("NORM")).Resolve());
                }
                else
                {
                    g.norms.Remove(n.defName);
                    if (!quiet) g.Note("CC_GroupNote_NormLost".Translate(n.label.Named("NORM")).Resolve());
                }
            }
        }

        private static bool Holds(SocialGroup g, GroupNormDef n, GroupFacts f, List<int> present, bool held)
        {
            if (!n.kinds.NullOrEmpty() && (g.kind == null || !n.kinds.Contains(g.kind.defName))) return false;
            if (g.kind != null && g.kind.Norms.Contains(n)) return true;
            var axis = n.Axis;
            if (axis != null && !GroupMath.NormHolds(held, Mean(f, present, axis.Value), n.Line, n.Sign > 0)) return false;
            if (!n.tags.NullOrEmpty())
            {
                float share = present.Count(i => PeopleTags.Matches(f.people[i], n.tags)) / (float)present.Count;
                if (share < n.tagShare - (held ? 0.1f : 0f)) return false;
            }
            return axis != null || !n.tags.NullOrEmpty() || !n.kinds.NullOrEmpty();
        }

        /// <summary>The group's way this person goes against (their character is far the other way), or null.</summary>
        public static GroupNormDef BreaksAny(SocialGroup g, float[] v)
        {
            foreach (var n in g.NormDefs)
            {
                var axis = n.Axis;
                if (axis == null || n.breakAt >= 900f) continue;
                float x = v[(int)axis.Value];
                if (n.Sign > 0 ? x <= -n.breakAt : x >= n.breakAt) return n;
            }
            return null;
        }

        /// <summary>The group's way this colonist goes against, or null (for the moments: someone out of step).</summary>
        public static GroupNormDef Breaks(SocialGroup g, Pawn p) => g == null || p == null ? null : BreaksAny(g, Persona.Values(p));

        // ---------------- Shared views ----------------

        private static void Views(SocialGroup g, GroupFacts f)
        {
            var present = Present(g, f);
            if (present.Count < 2) return;
            var byTopic = new Dictionary<string, List<float>>();
            foreach (int i in present)
            {
                var rec = Persona.Of(f.people[i]);
                if (rec?.stances == null) continue;
                foreach (var kv in rec.stances)
                {
                    if (!byTopic.TryGetValue(kv.Key, out var list)) byTopic[kv.Key] = list = new List<float>();
                    list.Add(kv.Value);
                }
            }
            var views = new List<(string view, float strength)>();
            foreach (var kv in byTopic)
            {
                int sign = GroupMath.SharedView(kv.Value, present.Count);
                if (sign != 0) views.Add((kv.Key + "|" + sign, Mathf.Abs(kv.Value.Average())));
            }
            g.views = views.OrderByDescending(v => v.strength).ThenBy(v => v.view).Take(3).Select(v => v.view).ToList();
        }

        // ---------------- Conformity ----------------

        /// <summary>
        /// Members are drawn a little towards their group's ways and views each day (a small fading nudge, so over the
        /// weeks the merry grow merrier and a doubter comes round), and only while they're with the others.
        /// </summary>
        private static void Conformity(SocialGroup g, GroupFacts f)
        {
            var present = Present(g, f);
            if (present.Count < 2 || g.cohesion < 30f) return;
            float pull = Mathf.Lerp(0.6f, 1.2f, (g.cohesion - 30f) / 70f);
            foreach (var n in g.NormDefs)
            {
                var axis = n.Axis;
                if (axis == null || n.conform <= 0f) continue;
                foreach (int i in present)
                {
                    float v = f.values[i][(int)axis.Value];
                    if (n.Sign > 0 ? v >= n.Line : v <= n.Line) continue;
                    Persona.Nudge(f.people[i], axis.Value, n.Sign * n.conform * pull);
                }
            }
            foreach (var view in g.views)
            {
                int bar = view.LastIndexOf('|');
                if (bar <= 0) continue;
                string topic = view.Substring(0, bar);
                float toward = view.Substring(bar + 1) == "-1" ? -0.6f : 0.6f;
                foreach (int i in present)
                {
                    var p = f.people[i];
                    if (!Persona.HasStance(p, topic))
                    {
                        // Someone with no view yet may take the group's, a little.
                        if (Rand.Chance(0.1f)) Persona.SetStance(p, topic, toward * 0.25f);
                        continue;
                    }
                    float s = Persona.Stance(p, topic);
                    Persona.SetStance(p, topic, GroupMath.Conform(s, toward, 0.02f * pull));
                }
            }
        }

        // ---------------- Between groups ----------------

        private static void Rivalries(CouncilGameComponent comp, GroupTracker t, GroupFacts f, bool quiet)
        {
            var tones = comp.social?.tones;
            int now = Now;
            var list = t.groups;
            for (int x = 0; x < list.Count; x++)
            for (int y = x + 1; y < list.Count; y++)
            {
                var g = list[x];
                var h = list[y];
                var a = Present(g, f).Where(i => !h.Has(f.people[i])).ToList();
                var b = Present(h, f).Where(i => !g.Has(f.people[i])).ToList();
                int rivalPairs = 0, frictions = 0;
                float rivalSum = 0f;
                string worstA = null, worstB = null;
                float worst = 0f;
                foreach (int i in a)
                foreach (int j in b)
                {
                    var pa = f.people[i];
                    var pb = f.people[j];
                    var tie = comp.social?.Get(pa, pb, TieKind.Rivals);
                    if (tie != null && tie.announced)
                    {
                        rivalPairs++;
                        rivalSum += tie.strength;
                        if (tie.strength > worst)
                        {
                            worst = tie.strength;
                            worstA = pa.LabelShort;
                            worstB = pb.LabelShort;
                        }
                    }
                    var tone = tones?.Get(pa, pb, false);
                    if (tone != null && tone.friction >= 0 && now - tone.friction < Day) frictions++;
                }
                float viewClash = 0f;
                string clashTopic = null;
                foreach (var v in g.views)
                {
                    int bar = v.LastIndexOf('|');
                    if (bar <= 0) continue;
                    string topic = v.Substring(0, bar);
                    string opposite = topic + "|" + (v.Substring(bar + 1) == "-1" ? "1" : "-1");
                    if (h.views.Contains(opposite))
                    {
                        viewClash = 1f;
                        clashTopic = topic;
                    }
                }
                float normClash = g.NormDefs.Any(n => !n.clashes.NullOrEmpty() && h.norms.Contains(n.clashes))
                                  || h.NormDefs.Any(n => !n.clashes.NullOrEmpty() && g.norms.Contains(n.clashes)) ? 1f : 0f;
                var rg = g.RivalryWith(h.id);
                float before = rg?.strength ?? 0f;
                float next = GroupMath.NextRivalry(before, rivalPairs, rivalPairs > 0 ? rivalSum / rivalPairs : 0f, viewClash, normClash, frictions);
                // Groups that share people are slow to fall out.
                if (g.members.Any(m => h.Has(m.pawn))) next = Mathf.Min(next, before + (next - before) * 0.5f);
                if (next < 1f && rg == null) continue;
                string why = worstA != null ? "CC_Group_RivalWhy_Feud".Translate(worstA, worstB).Resolve()
                    : clashTopic != null ? "CC_Group_RivalWhy_View".Translate(GroupText.TopicLabel(clashTopic)).Resolve()
                    : normClash > 0f ? "CC_Group_RivalWhy_Ways".Translate().Resolve()
                    : "CC_Group_RivalWhy_Frictions".Translate().Resolve();
                SetRivalry(g, h.id, next, why);
                SetRivalry(h, g.id, next, why);
                var r1 = g.RivalryWith(h.id);
                var r2 = h.RivalryWith(g.id);
                if (r1 == null || r2 == null) continue;
                if (!r1.announced && next >= GroupMath.RivalryShows)
                {
                    r1.announced = r2.announced = true;
                    r1.since = r2.since = now;
                    g.Note("CC_GroupNote_Rivals".Translate(h.Name.Named("GROUP"), why.Named("WHY")).Resolve(), true);
                    h.Note("CC_GroupNote_Rivals".Translate(g.Name.Named("GROUP"), why.Named("WHY")).Resolve(), true);
                    if (!quiet) AnnounceRivalry(comp, g, h, why, true);
                }
                else if (r1.announced && next < GroupMath.RivalryEnds)
                {
                    r1.announced = r2.announced = false;
                    g.Note("CC_GroupNote_RivalsOver".Translate(h.Name.Named("GROUP")).Resolve());
                    h.Note("CC_GroupNote_RivalsOver".Translate(g.Name.Named("GROUP")).Resolve());
                    if (!quiet) AnnounceRivalry(comp, g, h, why, false);
                }
                if (next < 1f && !r1.announced)
                {
                    g.rivals.Remove(r1);
                    h.rivals.Remove(r2);
                }
            }
            // Rivalries with groups that are no more.
            foreach (var g in list) g.rivals.RemoveAll(r => t.Get(r.other) == null);
        }

        private static void SetRivalry(SocialGroup g, int other, float strength, string why)
        {
            var r = g.RivalryWith(other);
            if (r == null)
            {
                r = new GroupRivalry { other = other, since = Now };
                g.rivals.Add(r);
            }
            r.strength = strength;
            r.why = why;
        }

        private static void AnnounceRivalry(CouncilGameComponent comp, SocialGroup g, SocialGroup h, string why, bool began)
        {
            // apart: news of two groups falling out (or making peace), read from the day's account of them.
            string text = (began ? "CC_Group_Rivalry" : "CC_Group_RivalryOver").Translate(g.Name.Named("GROUP"), h.Name.Named("OTHER"), why.Named("WHY")).Resolve();
            var look = new LookTargets(g.Pawns.Concat(h.Pawns).Where(p => p.Spawned).Take(6));
            News.Post(GroupText.Sentences(text), look, NewsKind.People, began ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.NeutralEvent, Importance.Notable);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Bond, GroupText.Sentences(text), g.Pawns.FirstOrDefault()));
            if (!began) return;
            var fire = g.Holder(GroupRole.Firebrand) ?? h.Holder(GroupRole.Firebrand);
            var writer = fire ?? g.Holder(GroupRole.Anchor) ?? g.Pawns.FirstOrDefault();
            var them = g.Has(writer) ? h : g;
            if (writer != null) GroupText.Journal(writer, "Rimfolk_Journal_Group_Rivalry", them.Name.Named("GROUP"));
        }

        // ---------------- Splits ----------------

        private static void Splits(CouncilGameComponent comp, GroupTracker t, GroupFacts f, bool quiet)
        {
            foreach (var g in t.groups.ToList())
            {
                var present = Present(g, f);
                if (present.Count < 4) continue;
                float worst = GroupMath.Worst(f.aff, present, out int wa, out int wb);
                if (!GroupMath.ShouldSplit(g.cohesion, g.lowDays, present.Count, worst)) continue;
                GroupMath.Cut(f.aff, present, out var sideA, out var sideB);
                if (sideA.Count == 0 || sideB.Count == 0) continue;
                // The side with the anchor (or the bigger one) keeps the group and its name.
                var anchor = g.Holder(GroupRole.Anchor);
                bool keepA = anchor != null ? sideA.Contains(f.IndexOf(anchor)) || !sideB.Contains(f.IndexOf(anchor)) && sideA.Count >= sideB.Count : sideA.Count >= sideB.Count;
                var stay = keepA ? sideA : sideB;
                var go = keepA ? sideB : sideA;
                var x = f.people[wa];
                var y = f.people[wb];
                bool feud = comp.social?.Get(x, y, TieKind.Rivals)?.announced ?? false;
                string why = (feud ? "CC_Group_SplitWhy_Feud" : "CC_Group_SplitWhy_Apart").Translate(x.Named("A"), y.Named("B")).Resolve();
                var leavers = go.Select(i => f.people[i]).ToList();
                foreach (var p in leavers)
                {
                    var m = g.Member(p);
                    if (m != null) g.members.Remove(m);
                }
                SocialGroup broke = null;
                foreach (var p in leavers) g.Former(p.LabelShort);
                if (go.Count >= GroupMath.MinSize) broke = Form(comp, t, f, go, quiet, g);
                g.cohesion = Mathf.Max(g.cohesion, 40f);
                g.lowDays = 0;
                t.Changed();
                string names = TextUtil.JoinAnd(leavers.Select(p => p.LabelShort).ToList());
                g.Note("CC_GroupNote_Split".Translate(names.Named("NAMES"), why.Named("WHY")).Resolve(), true);
                if (broke != null) broke.Note("CC_GroupNote_BrokeAway".Translate(g.Name.Named("GROUP"), why.Named("WHY")).Resolve(), true);
                // Each of the two sides remembers the day it came apart.
                if (GroupDefs.SplitMemory != null && Rand.Chance(0.7f)) SharedMemories.Add(x, y, GroupDefs.SplitMemory, g.Name);
                if (quiet) continue;
                // apart: news of a group coming apart, read from the day's account of it.
                string text = (broke != null ? "CC_Group_SplitInTwo" : "CC_Group_Split").Translate(g.Name.Named("GROUP"), names.Named("NAMES"), why.Named("WHY"),
                    (broke?.Name ?? "").Named("OTHER")).Resolve();
                News.Post(GroupText.Sentences(text), new LookTargets(leavers.Concat(g.Pawns).Where(p => p.Spawned).Take(6)), NewsKind.People, MessageTypeDefOf.NegativeEvent, Importance.Notable);
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Bond, GroupText.Sentences(text), x));
                // Each writes about the one of the two on the other side from them.
                var wentWith = leavers.Contains(y) ? y : x;
                var stayedWith = wentWith == y ? x : y;
                foreach (var p in leavers.Concat(g.Pawns).Distinct().ToList())
                {
                    if (p.Dead) continue;
                    CouncilGameComponent.GiveThought(p, GroupDefs.CircleBroke, null, -1f, "CC_Cause_CircleSplit".Translate(g.Name).Resolve());
                    if (Rand.Chance(0.6f)) GroupText.Journal(p, "Rimfolk_Journal_Group_Split", g.Name.Named("GROUP"), (leavers.Contains(p) ? stayedWith : wentWith).Named("OTHER"));
                }
            }
        }

        // ---------------- Merges ----------------

        private static void Merges(CouncilGameComponent comp, GroupTracker t, GroupFacts f, bool quiet)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                bool merged = false;
                var list = t.groups.OrderBy(g => g.formed).ToList();
                for (int x = 0; x < list.Count && !merged; x++)
                for (int y = x + 1; y < list.Count && !merged; y++)
                {
                    var g = list[x];
                    var h = list[y];
                    var a = g.members.Select(m => m.pawn).ToList();
                    var b = h.members.Select(m => m.pawn).ToList();
                    var ia = Present(g, f);
                    var ib = Present(h, f);
                    float cross = 0f;
                    int n = 0;
                    foreach (int i in ia)
                    foreach (int j in ib)
                    {
                        if (i == j) continue;
                        cross += f.aff[i, j];
                        n++;
                    }
                    cross = n == 0 ? 0f : cross / n;
                    bool rivals = Mathf.Max(g.RivalryTo(h.id), h.RivalryTo(g.id)) >= GroupMath.RivalryEnds;
                    var ids = a.Select(p => p.thingIDNumber).ToList();
                    var idsB = b.Select(p => p.thingIDNumber).ToList();
                    if (!GroupMath.ShouldMerge(ids, idsB, cross, g.cohesion, h.cohesion, rivals)) continue;
                    bool overlap = GroupMath.Overlap(ids, idsB) * 2 >= Mathf.Min(ids.Count, idsB.Count);
                    if (!overlap && !quiet && !Rand.Chance(0.15f)) continue;
                    Merge(comp, t, g, h, quiet);
                    merged = true;
                }
                if (!merged) break;
            }
        }

        /// <summary><paramref name="h"/> joins <paramref name="g"/> (the older keeps its name).</summary>
        private static void Merge(CouncilGameComponent comp, GroupTracker t, SocialGroup g, SocialGroup h, bool quiet)
        {
            foreach (var m in h.members)
            {
                if (g.Has(m.pawn)) continue;
                g.members.Add(new GroupMember { pawn = m.pawn, name = m.name, joined = m.joined, role = GroupRole.None, roleSince = Now });
            }
            g.cohesion = Mathf.Min(100f, (g.cohesion + h.cohesion) / 2f + 5f);
            foreach (var n in h.norms)
                if (!g.norms.Contains(n) && g.norms.Count < 3)
                    g.norms.Add(n);
            g.Note("CC_GroupNote_Merged".Translate(h.Name.Named("OTHER")).Resolve(), true);
            End(t, h, "CC_GroupHow_Merged".Translate(g.Name).Resolve());
            t.Changed();
            if (quiet) return;
            // apart: news of two groups becoming one, read from the day's account of them.
            string text = "CC_Group_Merged".Translate(h.Name.Named("OTHER"), g.Name.Named("GROUP")).Resolve();
            News.Post(GroupText.Sentences(text), new LookTargets(g.Pawns.Where(p => p.Spawned).Take(6)), NewsKind.People, MessageTypeDefOf.PositiveEvent, Importance.Notable);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Bond, GroupText.Sentences(text), g.Pawns.FirstOrDefault()));
            foreach (var p in g.Pawns.Where(p => !p.Dead && Rand.Chance(0.5f)).ToList())
                GroupText.Journal(p, "Rimfolk_Journal_Group_Merged", g.Name.Named("GROUP"), h.Name.Named("OLD"));
        }

        // ---------------- Ends ----------------

        private static void Ends(CouncilGameComponent comp, GroupTracker t, SocialWeb web, bool quiet)
        {
            foreach (var g in t.groups.ToList())
            {
                int alive = g.members.Count(m => m.pawn != null && !m.pawn.Dead);
                bool fell = g.cohesion <= GroupMath.DissolveBelow && g.lowDays >= GroupMath.DissolveDays;
                if (alive >= 2 && !fell) continue;
                string how = (alive == 0 ? "CC_GroupHow_AllGone" : alive == 1 ? "CC_GroupHow_LastOne" : "CC_GroupHow_DriftedApart").Translate().Resolve();
                var formerly = g.members.Select(m => m.pawn).Where(p => p != null && !p.Dead).ToList();
                var names = g.members.Select(m => m.name).Concat(g.formerNames).Where(n => n != null).ToList();
                End(t, g, how);
                if (quiet) continue;
                // apart: news that a group is no more, read from the day's account of it.
                string text = "CC_Group_Ended".Translate(g.Name.Named("GROUP"), how.Named("HOW")).Resolve();
                News.Post(GroupText.Sentences(text), new LookTargets(formerly.Where(p => p.Spawned)), NewsKind.People, MessageTypeDefOf.NeutralEvent, Importance.Notable);
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Bond, GroupText.Sentences(text), formerly.FirstOrDefault()));
                // Someone remembers it: those who were in it, or else someone who lost a friend in it.
                var rememberers = formerly.ToList();
                if (rememberers.Count == 0 && web != null)
                {
                    var lost = web.lost.Where(l => l.survivor != null && !l.survivor.Dead && l.survivor.IsColonist && names.Contains(l.name)).Select(l => l.survivor).FirstOrDefault();
                    if (lost != null) rememberers.Add(lost);
                }
                foreach (var p in rememberers)
                {
                    if (fell) CouncilGameComponent.GiveThought(p, GroupDefs.CircleBroke, null, -1f, "CC_Cause_CircleEnded".Translate(g.Name).Resolve());
                    GroupText.Journal(p, "Rimfolk_Journal_Group_Remembered", g.Name.Named("GROUP"));
                }
            }
        }

        private static void End(GroupTracker t, SocialGroup g, string how)
        {
            t.groups.Remove(g);
            t.past.Add(new PastGroup
            {
                name = g.Name,
                formed = g.formed,
                ended = Now,
                how = how,
                members = g.members.Where(m => m.pawn != null).Select(m => m.pawn.thingIDNumber).ToList()
            });
            while (t.past.Count > GroupTracker.MaxPast) t.past.RemoveAt(0);
            foreach (var n in t.newcomers)
                if (n.groupId == g.id)
                    n.groupId = -1;
            t.Changed();
        }

        // ---------------- Joining ----------------

        private static bool Free(GroupTracker t, Pawn p) =>
            t.Of(p).Count < GroupMath.MaxGroupsEach && (t.NewcomerOf(p) == null || t.NewcomerOf(p).progress >= GroupMath.Accepted);

        private static void Joins(CouncilGameComponent comp, GroupTracker t, GroupFacts f, bool quiet)
        {
            var lists = t.groups.Select(g => Present(g, f)).ToList();
            for (int i = 0; i < f.Count; i++)
            {
                var p = f.people[i];
                if (!Free(t, p)) continue;
                // Nobody goes back to a group they broke away from or drifted off from (only a merge brings them back).
                var closed = t.groups.Select(g => g.formerNames.Contains(p.LabelShort)).ToArray();
                int target = GroupMath.JoinTarget(f.aff, i, lists, closed);
                if (target < 0 || (!quiet && !Rand.Chance(0.3f))) continue;
                var g = t.groups[target];
                if (g.Has(p)) continue;
                Join(comp, t, g, p, quiet);
                lists[target].Add(i);
            }
        }

        /// <summary>Someone joins a group: as its newcomer, if it's been going a while.</summary>
        public static void Join(CouncilGameComponent comp, GroupTracker t, SocialGroup g, Pawn p, bool quiet, Pawn sponsor = null)
        {
            bool settled = Now - g.formed >= GroupMath.NewcomerDays * Day;
            g.members.Add(new GroupMember { pawn = p, name = p.LabelShort, joined = Now, role = settled ? GroupRole.Newcomer : GroupRole.None, roleSince = Now });
            t.Changed();
            g.Note((sponsor != null ? "CC_GroupNote_JoinedWith" : "CC_GroupNote_Joined").Translate(p.Named("PAWN"), (sponsor?.LabelShort ?? "").Named("OTHER")).Resolve());
            if (quiet) return;
            News.Post(GroupText.Sentences("CC_Group_Joined".Translate(p.Named("PAWN"), g.Name.Named("GROUP")).Resolve()), new LookTargets(p), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
            GroupText.Journal(p, "Rimfolk_Journal_Group_Joined", g.Name.Named("GROUP"));
        }

        // ---------------- Forming ----------------

        private static void Formation(CouncilGameComponent comp, GroupTracker t, GroupFacts f, bool quiet)
        {
            for (int k = 0; k < (quiet ? 6 : 1); k++)
            {
                // Newcomers may form a group of their own (those who came together, say): then they're outsiders no more.
                var free = new bool[f.Count];
                for (int i = 0; i < f.Count; i++) free[i] = t.Of(f.people[i]).Count < GroupMath.MaxGroupsEach;
                var existing = t.groups.Select(g => Present(g, f)).ToList();
                var cluster = GroupMath.Cluster(f.aff, f.Count, free, existing);
                if (cluster == null) return;
                if (!quiet && !Rand.Chance(0.4f)) return;
                Form(comp, t, f, cluster, quiet, null);
            }
        }

        /// <summary>A new group of these people (by index): its kind, a name nobody else has, its first roles, and the news of it.</summary>
        public static SocialGroup Form(CouncilGameComponent comp, GroupTracker t, GroupFacts f, List<int> cluster, bool quiet, SocialGroup brokeFrom)
        {
            var kind = f.KindFor(cluster, out _);
            if (kind == null) return null;
            int now = Now;
            var g = new SocialGroup
            {
                id = t.nextId++,
                kind = kind,
                kindName = kind.defName,
                why = kind.why,
                formed = now,
                cohesion = GroupMath.StartCohesion(GroupMath.Mean(f.aff, cluster)),
                announced = !quiet
            };
            foreach (int i in cluster)
                g.members.Add(new GroupMember { pawn = f.people[i], name = f.people[i].LabelShort, joined = now, roleSince = now });
            // Its first roles, so the one it gathers round can give it its name.
            var facts = g.members.Select(m => f.Role(g, m, now / Day)).ToArray();
            var roles = GroupMath.AssignRoles(facts, g.members.Select(_ => new RoleState()).ToArray());
            for (int i = 0; i < g.members.Count; i++) g.members[i].role = roles[i].role;
            var anchor = g.Holder(GroupRole.Anchor)
                         ?? g.members.OrderByDescending(m => GroupMath.RoleScore(GroupRole.Anchor, f.Role(g, m, now / Day))).First().pawn;
            g.name = UniqueName(t, kind, anchor, g.id);
            t.groups.Add(g);
            t.Changed();
            if (brokeFrom == null) g.Note((quiet ? "CC_GroupNote_Found" : "CC_GroupNote_Formed").Translate(kind.why.Named("WHY")).Resolve(), true);
            if (quiet || brokeFrom != null) return g;
            // apart: news that a group has formed, read from the day's account of who drifted together.
            string text = "CC_Group_Formed".Translate(g.Name.Named("GROUP"), GroupText.Members(g).Named("NAMES"), kind.why.Named("WHY")).Resolve();
            News.Post(GroupText.Sentences(text), new LookTargets(g.Pawns.Where(p => p.Spawned)), NewsKind.People, MessageTypeDefOf.PositiveEvent, Importance.Notable);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Bond, GroupText.Sentences(text), anchor));
            foreach (var m in g.members)
            {
                if (Rand.Chance(0.7f)) GroupText.Journal(m.pawn, "Rimfolk_Journal_Group_Formed", g.Name.Named("GROUP"));
                if (m.pawn != anchor && GroupDefs.FoundedMemory != null) SharedMemories.Add(anchor, m.pawn, GroupDefs.FoundedMemory, g.Name, 0.6f);
            }
            return g;
        }

        /// <summary>A name from the kind that no group now (or lately) has; else "Hector's hunters"; else the kind's first with a number.</summary>
        private static string UniqueName(GroupTracker t, GroupKindDef kind, Pawn anchor, int id)
        {
            var taken = new HashSet<string>(t.groups.Select(g => g.Name).Concat(t.past.Skip(Mathf.Max(0, t.past.Count - 4)).Select(p => p.name)), StringComparer.OrdinalIgnoreCase);
            var options = GroupText.NameOptions(kind, anchor).Where(n => !taken.Contains(n)).ToList();
            if (options.Count > 0)
            {
                Rand.PushState(id * 7919 + (anchor?.thingIDNumber ?? 0));
                try
                {
                    return options.RandomElement();
                }
                finally
                {
                    Rand.PopState();
                }
            }
            if (anchor != null)
            {
                string named = "CC_Group_NameOf".Translate(anchor.Named("ANCHOR"), kind.label.Named("KIND")).Resolve();
                if (!taken.Contains(named)) return named;
            }
            string first = GroupText.NameOptions(kind, anchor).FirstOrDefault() ?? kind.label;
            for (int n = 2; n < 50; n++)
            {
                string s = first + " (" + n + ")";
                if (!taken.Contains(s)) return s;
            }
            return first;
        }

        // ---------------- Left out ----------------

        /// <summary>
        /// Groups have a way of making the people outside them feel it (the social web's left-out feeling, with groups on):
        /// now and then someone in no group and with no close friend, or a newcomer a group has shut out, feels left out.
        /// </summary>
        private static void LeftOut(GroupTracker t, GroupFacts f, SocialWeb web)
        {
            if (t.groups.Count == 0 || !Rand.Chance(0.15f)) return;
            var options = new List<(Pawn p, float w, string cause)>();
            foreach (var p in f.people)
            {
                if (p.needs?.mood == null || p.story?.traits?.HasTrait(TraitDefOf.Psychopath) == true) continue;
                if (t.Of(p).Count > 0) continue;
                var n = t.NewcomerOf(p);
                var shut = n != null && n.closed && n.progress < GroupMath.Accepted ? t.Get(n.groupId) : null;
                if (shut != null)
                {
                    options.Add((p, 3f, "CC_Cause_ShutOut".Translate(shut.Name).Resolve()));
                    continue;
                }
                if (web.ties.Any(x => x.Involves(p) && x.kind == TieKind.Friends && x.strength >= 20f)) continue;
                options.Add((p, 1f, "CC_Cause_LeftOut".Translate().Resolve()));
            }
            if (options.Count == 0) return;
            var pick = options.RandomElementByWeight(o => o.w);
            CouncilGameComponent.GiveThought(pick.p, CouncilDefOf.Council_Soc_LeftOut, null, -1f, pick.cause);
        }
    }
}
