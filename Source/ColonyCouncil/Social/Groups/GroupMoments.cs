using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A moment between two colonists that belongs to their groups (Defs/GroupDefs/Rimfolk_GroupMoments.xml): a welcome
    /// for a newcomer or the cold shoulder, a dig at the other lot, a word to someone out of step, the joker lifting
    /// the mood, the worrier fretting, the firebrand stirring things up, the anchor checking on someone. They are the
    /// game's own conversations (picked among the rest when two colonists talk, so the two are face to face), with a
    /// <see cref="MomentExtension"/> for the usual conditions and a <see cref="GroupMomentExtension"/> for the group's.
    /// </summary>
    public class InteractionWorker_GroupMoment : InteractionWorker_Moment
    {
        private GroupMomentExtension GroupExt => interaction.GetModExtension<GroupMomentExtension>();

        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            try
            {
                if (!GroupLife.Active) return 0f;
                var x = GroupExt;
                if (x == null || initiator == null || recipient == null) return 0f;
                float f = GroupMoments.Fits(x, initiator, recipient);
                if (f <= 0f) return 0f;
                return base.RandomSelectionWeight(initiator, recipient) * f * x.factor;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("group moment weight " + interaction?.defName, e);
                return 0f;
            }
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef,
            out LookTargets lookTargets)
        {
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
            try
            {
                GroupMoments.After(GroupExt, initiator, recipient);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("group moment " + interaction?.defName, e);
            }
        }
    }

    /// <summary>The conditions and after-effects of group moments, and the group's peacemaker stepping into a spat.</summary>
    public static class GroupMoments
    {
        private static GroupRole? Role(string name) => !name.NullOrEmpty() && Enum.TryParse(name, true, out GroupRole r) ? r : (GroupRole?)null;

        /// <summary>A rivalry under this doesn't show in moments.</summary>
        public const float RivalMoments = 20f;

        /// <summary>How well a group moment fits the two (0: not at all; a rivalry's moments grow likelier as it worsens).</summary>
        public static float Fits(GroupMomentExtension x, Pawn a, Pawn b)
        {
            var t = GroupTracker.Instance;
            if (t == null) return 0f;
            switch (x.between)
            {
                case "rivalGroups":
                {
                    float r = Groups.Rivalry(a, b, out var ga, out _);
                    if (r < RivalMoments || !RoleFits(x.initiatorRole, ga, a)) return 0f;
                    return Mathf.Clamp(r / 50f, 0.4f, 2f);
                }
                case "welcome":
                case "closeRanks":
                case "sponsor":
                {
                    var n = t.NewcomerOf(b);
                    if (n == null || n.progress >= GroupMath.Accepted) return 0f;
                    var g = t.Get(n.groupId);
                    if (g == null || !g.Has(a)) return 0f;
                    if (x.between == "welcome" && (n.closed || n.sponsor == a)) return 0f;
                    if (x.between == "closeRanks" && !n.closed) return 0f;
                    if (x.between == "sponsor" && n.sponsor != a) return 0f;
                    return RoleFits(x.initiatorRole, g, a) ? 1f : 0f;
                }
                default:
                {
                    var g = Groups.Shared(a, b);
                    if (g == null || g.cohesion < x.minCohesion) return 0f;
                    if (!RoleFits(x.initiatorRole, g, a) || !RoleFits(x.recipientRole, g, b)) return 0f;
                    if (x.hasRival && !g.rivals.Any(r => r.strength >= RivalMoments)) return 0f;
                    if (x.recipientMoodBelow >= 0f && (b.needs?.mood == null || b.needs.mood.CurLevel >= x.recipientMoodBelow)) return 0f;
                    if (x.between == "outOfStep" && (GroupLife.Breaks(g, b) == null || GroupLife.Breaks(g, a) != null)) return 0f;
                    return 1f;
                }
            }
        }

        private static bool RoleFits(string role, SocialGroup g, Pawn p)
        {
            var r = Role(role);
            return r == null || g != null && g.RoleOf(p) == r.Value;
        }

        /// <summary>What a group moment does afterwards: to the group's closeness, the rivalry, the newcomer's way in.</summary>
        public static void After(GroupMomentExtension x, Pawn a, Pawn b)
        {
            var t = GroupTracker.Instance;
            if (x == null || t == null || !GroupLife.Active) return;
            switch (x.between)
            {
                case "rivalGroups":
                {
                    Groups.Rivalry(a, b, out var ga, out var gb);
                    if (ga == null || gb == null || x.rivalry == 0f) return;
                    AddRivalry(ga, gb, x.rivalry);
                    return;
                }
                case "welcome":
                case "sponsor":
                case "closeRanks":
                {
                    var n = t.NewcomerOf(b);
                    if (n == null) return;
                    if (x.between != "closeRanks") n.welcomes++;
                    n.progress = Mathf.Clamp(n.progress + x.integration, 0f, GroupMath.Accepted - 1f);
                    return;
                }
                default:
                {
                    var g = Groups.Shared(a, b);
                    if (g == null) return;
                    g.cohesion = Mathf.Clamp(g.cohesion + x.cohesion, 0f, 100f);
                    if (x.hasRival && x.rivalry != 0f)
                    {
                        var top = g.rivals.OrderByDescending(r => r.strength).FirstOrDefault();
                        var other = top != null ? t.Get(top.other) : null;
                        if (other != null) AddRivalry(g, other, x.rivalry);
                    }
                    if (x.between == "outOfStep")
                    {
                        // Under the group's eye, the odd one out comes round a little, or digs in.
                        var n = GroupLife.Breaks(g, b);
                        var axis = n?.Axis;
                        if (axis != null && Rand.Chance(0.5f)) Persona.Nudge(b, axis.Value, n.Sign * 1.5f);
                    }
                    return;
                }
            }
        }

        private static void AddRivalry(SocialGroup g, SocialGroup h, float amount)
        {
            foreach (var (from, to) in new[] { (g, h), (h, g) })
            {
                var r = from.RivalryWith(to.id);
                if (r == null)
                {
                    r = new GroupRivalry { other = to.id, since = Find.TickManager.TicksGame, why = "CC_Group_RivalWhy_Frictions".Translate().Resolve() };
                    from.rivals.Add(r);
                }
                r.strength = Mathf.Clamp(r.strength + amount, 0f, 100f);
            }
        }

        // ---------------- The peacemaker ----------------

        private static readonly Dictionary<int, int> lastStepped = new Dictionary<int, int>();

        public static void Reset() => lastStepped.Clear();

        /// <summary>
        /// A spat between two colonists (MomentTones, through Interplay.Notify_Spat). Between two of the same group, its
        /// peacemaker, if they're right there (Together.Can with one of them) and now and then, steps in: the spat is
        /// over sooner, it counts as a quarrel mended, and they say something. At most every few hours each.
        /// </summary>
        public static void Notify_Spat(Pawn a, Pawn b, PairTone tone)
        {
            if (!GroupLife.Active || a == null || b == null || tone == null) return;
            try
            {
                var g = Groups.Shared(a, b);
                var pm = g?.Holder(GroupRole.Peacemaker);
                if (pm == null || pm == a || pm == b || !Together.Able(pm)) return;
                if (!Together.Can(pm, a) && !Together.Can(pm, b)) return;
                int now = Find.TickManager.TicksGame;
                int left = tone.soreUntil - now;
                if (left < GenDate.TicksPerHour * 2 || !Rand.Chance(0.4f)) return;
                if (lastStepped.TryGetValue(pm.thingIDNumber, out int last) && last <= now && now - last < GenDate.TicksPerHour * 5) return;
                lastStepped[pm.thingIDNumber] = now;
                if (lastStepped.Count > 200) lastStepped.Clear();
                tone.soreUntil = now + left / 2;
                g.todayMends++;
                CouncilRecords.Add(pm, CouncilRecords.QuarrelsMended);
                var to = Together.Can(pm, a) ? a : b;
                string said = Flavor.FromPool("Rimfolk_Pool_Group_PeacemakerSays", pm);
                bool spoke = !said.NullOrEmpty() && Speech.Say(pm, said, to);
                string text = Flavor.FromPool("Rimfolk_Pool_Group_PeacemakerSteps", pm);
                if (!text.NullOrEmpty())
                {
                    text = text.Formatted(pm.Named("PAWN"), a.Named("A"), b.Named("B"), g.Name.Named("GROUP")).Resolve();
                    SocialLog.Add(pm, to, text, !spoke);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("groups: peacemaker", e);
            }
        }
    }
}
