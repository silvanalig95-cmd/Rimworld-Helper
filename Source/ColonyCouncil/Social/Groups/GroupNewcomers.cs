using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Someone new to the colony is an outsider until a group takes them in. Each day they find their way a little
    /// further into the group they hold to best: what its people think of them, what they went through together, work
    /// they share, a member who speaks for them (a sponsor) and the welcomes they get all count, more in a group open to
    /// newcomers and much less in one that closes ranks (its ways, its people's trust and warmth decide which). Once far
    /// enough in, they're one of them. A newcomer no group takes in within forty days simply settles in on their own.
    /// </summary>
    public static class GroupNewcomers
    {
        private const int Day = GenDate.TicksPerDay;
        /// <summary>Someone who came this long after the colony's first days, and this lately, is new.</summary>
        public const int NewAfterDays = 3, NewWithinDays = 15, GiveUpDays = 40;

        public static void Daily(CouncilGameComponent comp, GroupTracker t, GroupFacts f, bool quiet)
        {
            int now = Find.TickManager.TicksGame;
            // Those who just came.
            if (!quiet && t.groups.Count > 0)
            {
                for (int i = 0; i < f.Count; i++)
                {
                    var p = f.people[i];
                    if (t.Of(p).Count > 0 || t.NewcomerOf(p) != null) continue;
                    int arrivedTick = f.arrived[i];
                    if (arrivedTick < NewAfterDays * Day || now - arrivedTick >= NewWithinDays * Day) continue;
                    t.newcomers.Add(new GroupNewcomer { pawn = p, since = now });
                }
            }
            foreach (var n in t.newcomers.ToList())
            {
                var p = n.pawn;
                if (p == null || p.Dead || !p.IsColonist && !p.IsSlaveOfColony)
                {
                    t.newcomers.Remove(n);
                    continue;
                }
                int i = f.IndexOf(p);
                if (i < 0) continue;
                if (t.Of(p).Count > 0)
                {
                    t.newcomers.Remove(n);
                    continue;
                }
                OneDay(comp, t, f, n, i, now);
                n.welcomes = 0;
            }
        }

        /// <summary>How open this group is to someone new today (see <see cref="GroupMath.Welcome"/>).</summary>
        public static float WelcomeOf(SocialGroup g, GroupFacts f)
        {
            var present = GroupLife.Present(g, f);
            float trust = 0f, warmth = 0f;
            foreach (int m in present)
            {
                trust += f.values[m][(int)PersonaAxis.Trust];
                warmth += f.values[m][(int)PersonaAxis.Warmth];
            }
            if (present.Count > 0)
            {
                trust /= present.Count;
                warmth /= present.Count;
            }
            float norms = g.NormDefs.Sum(x => x.welcome);
            return GroupMath.Welcome(trust, warmth, norms, g.cohesion);
        }

        private static void OneDay(CouncilGameComponent comp, GroupTracker t, GroupFacts f, GroupNewcomer n, int i, int now)
        {
            var p = n.pawn;
            // The group they hold to best (staying with the one they're finding their way into unless another is clearly closer).
            SocialGroup best = null;
            float bestV = -0.05f;
            var current = t.Get(n.groupId);
            foreach (var g in t.groups)
            {
                if (g.members.Count >= GroupMath.MaxSize) continue;
                var present = GroupLife.Present(g, f);
                if (present.Count == 0) continue;
                float v = GroupMath.Attachment(f.aff, i, present) + (g == current ? 0.15f : 0f);
                if (v > bestV)
                {
                    bestV = v;
                    best = g;
                }
            }
            if (best != current)
            {
                n.groupId = best?.id ?? -1;
                n.closed = false;
                n.sponsor = null;
                n.progress *= 0.5f;
            }
            if (best == null)
            {
                // No group to find their way into: they settle in on their own in time.
                n.progress = Mathf.Min(GroupMath.Accepted, n.progress + 3f);
                if (n.progress >= GroupMath.Accepted || now - n.since > GiveUpDays * Day) t.newcomers.Remove(n);
                return;
            }
            var members = GroupLife.Present(best, f);
            // A member who thinks well of them, and is warm enough to say so, speaks for them.
            Pawn sponsor = null;
            int bestOpinion = 24;
            foreach (int m in members)
            {
                var o = f.people[m];
                int op = o.relations?.OpinionOf(p) ?? 0;
                if (op > bestOpinion && f.values[m][(int)PersonaAxis.Warmth] >= 0f)
                {
                    bestOpinion = op;
                    sponsor = o;
                }
            }
            if (sponsor != null && n.sponsor != sponsor)
            {
                n.sponsor = sponsor;
                best.Note("CC_GroupNote_Sponsor".Translate(sponsor.Named("PAWN"), p.Named("OTHER")).Resolve());
            }
            float welcome = WelcomeOf(best, f);
            bool closes = GroupMath.ClosesRanks(welcome);
            if (closes && !n.closed && now - n.since >= Day * 2)
            {
                n.closed = true;
                best.Note("CC_GroupNote_ClosedRanks".Translate(p.Named("PAWN")).Resolve());
                // apart: news of how a group has taken to someone new, read from the day's account of them.
                News.Post(GroupText.Sentences("CC_Group_ClosedRanks".Translate(best.Name.Named("GROUP"), p.Named("PAWN")).Resolve()), new LookTargets(p), NewsKind.People,
                    MessageTypeDefOf.SilentInput, Importance.Flavor);
                GroupText.Journal(p, "Rimfolk_Journal_Group_ShutOut", best.Name.Named("GROUP"));
            }
            else if (!closes && n.closed && welcome >= GroupMath.CloseRanksBelow + 0.1f) n.closed = false;
            var d = new IntegrationDay { sponsor = n.sponsor != null && best.Has(n.sponsor), welcomes = n.welcomes };
            float opinion = 0f, work = 0f;
            foreach (int m in members)
            {
                var o = f.people[m];
                opinion += o.relations?.OpinionOf(p) ?? 0;
                work += f.SharedWork(i, m);
                foreach (var mem in SharedMemories.Of(p, o))
                    if (now - mem.tick < Day)
                        d.memories++;
            }
            if (members.Count > 0)
            {
                d.opinion = opinion / members.Count;
                d.sharedWork = work / members.Count;
            }
            n.progress = GroupMath.NextIntegration(n.progress, d, welcome);
            if (n.progress < GroupMath.Accepted)
            {
                if (now - n.since > GiveUpDays * Day) t.newcomers.Remove(n);
                return;
            }
            // One of them now.
            t.newcomers.Remove(n);
            GroupLife.Join(comp, t, best, p, true, d.sponsor ? n.sponsor : null);
            best.cohesion = Mathf.Min(100f, best.cohesion + 2f);
            // apart: news that a group has taken someone in, read from the day's account of them.
            News.Post(GroupText.Sentences("CC_Group_Accepted".Translate(p.Named("PAWN"), best.Name.Named("GROUP")).Resolve()), new LookTargets(p), NewsKind.People,
                MessageTypeDefOf.PositiveEvent, Importance.Flavor);
            GroupText.Journal(p, "Rimfolk_Journal_Group_Accepted", best.Name.Named("GROUP"));
            if (d.sponsor && n.sponsor != null && Rand.Chance(0.5f))
                GroupText.Journal(n.sponsor, "Rimfolk_Journal_Group_Vouched", best.Name.Named("GROUP"), p.Named("OTHER"));
        }

        /// <summary>The newcomers finding their way into a group, for its card.</summary>
        public static List<GroupNewcomer> Into(GroupTracker t, SocialGroup g) => t?.newcomers.Where(n => n.groupId == g.id && n.pawn != null && !n.pawn.Dead).ToList() ?? new List<GroupNewcomer>();
    }
}
