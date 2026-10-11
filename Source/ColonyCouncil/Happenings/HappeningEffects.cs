using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Effects for world events. They work like the dilemma effects (and can be used there too); the ones about a
    // stranger act on X, the ones about fights and finds read the event's knobs.

    /// <summary>X joins the colony (walking in from the edge if they were waiting out in the world). With withGroup, everyone who came with them too.</summary>
    public class DilemmaEffect_Join : DilemmaEffect
    {
        public bool withGroup;

        /// <summary>Someone who was here (a visitor, a lost child) and has gone can't be asked to stay.</summary>
        private static bool Gone(DilemmaContext ctx)
        {
            var x = ctx.stranger;
            if (x == null || x.Dead || x.Discarded) return true;
            bool waiting = (ctx as HappeningContext)?.xWaiting ?? false;
            return !waiting && (!x.Spawned || x.Map != ctx.map);
        }

        public override string Unavailable(DilemmaContext ctx) =>
            Gone(ctx) ? "CC_Stranger_Gone".Translate(ctx.stranger?.LabelShort ?? "?").ToString() : null;

        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx.stranger;
            if (x == null || ctx.map == null || Gone(ctx)) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            var h = ctx as HappeningContext;
            IntVec3? near = ctx.a != null && ctx.a.Spawned && ctx.a.Map == ctx.map && !x.Spawned && x.RaceProps.Animal ? ctx.a.Position : (IntVec3?)null;
            if (!Arrivals.Join(x, ctx.map, near)) return;
            if (h != null) h.xWaiting = false;
            if (withGroup && h != null)
                foreach (var p in h.group.Where(p => p != null && !p.Dead)) Arrivals.Join(p, ctx.map, x.Position);
        }
    }

    /// <summary>
    /// A fight comes later: the event's enemy (or faction) attacks after a delay, with its own letter. Strength is a
    /// share of what the storyteller would send at the time (the event's points range unless given). With xLeads,
    /// X walks in with them.
    /// </summary>
    public class DilemmaEffect_RaidLater : DilemmaEffect
    {
        public FloatRange points = FloatRange.Zero;
        public FloatRange delayHours = FloatRange.Zero;
        [MustTranslate] public string label;
        [MustTranslate] public string text;
        public bool xLeads;
        /// <summary>Part of the scenario the player chose (see PendingRaid.scenario).</summary>
        public bool scenario;
        /// <summary>They come for A: if A has died or left since, there's nobody to come for.</summary>
        public bool forA;

        public override void Apply(DilemmaContext ctx)
        {
            var h = ctx as HappeningContext;
            var f = h?.enemy ?? ctx.faction;
            var tracker = CouncilGameComponent.Instance?.happenings;
            if (f == null || ctx.map == null || tracker == null || !Raids.CanRaid(f)) return;
            if (forA && (ctx.a == null || ctx.a.Dead || !ctx.a.IsColonist)) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            var ext = h?.Ext;
            float factor = (points.max > 0f ? points : ext?.points ?? new FloatRange(0.5f, 0.7f)).RandomInRange;
            float delay = (delayHours.max > 0f ? delayHours : ext?.delayHours ?? new FloatRange(3f, 8f)).RandomInRange;
            tracker.AddRaid(ctx.map, f, factor, delay, ctx.Format(label ?? ""), ctx.Format(text ?? ""), xLeads ? ctx.stranger : null, scenario);
        }
    }

    /// <summary>Things of the colony's era worth about <see cref="value"/> silver, near A (or by drop pod in an industrial colony).</summary>
    public class DilemmaEffect_Loot : DilemmaEffect
    {
        public FloatRange value = FloatRange.Zero;
        public bool fancy;
        public bool pod;

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx.map == null) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            var h = ctx as HappeningContext;
            float v = (value.max > 0f ? value : h?.Ext?.value ?? new FloatRange(300f, 700f)).RandomInRange;
            // A richer colony is sent richer things, within reason.
            v *= Mathf.Clamp(Mathf.Sqrt(Mathf.Max(1f, ctx.map.wealthWatcher.WealthTotal) / 40000f), 0.6f, 2f);
            var things = Loot.Make(v, fancy);
            var near = target == DilemmaTarget.X && ctx.stranger != null && ctx.stranger.Spawned ? ctx.stranger.Position
                : ctx.a != null && ctx.a.Spawned && ctx.a.Map == ctx.map ? ctx.a.Position : IntVec3.Invalid;
            Loot.Deliver(things, ctx.map, near, pod);
        }
    }

    /// <summary>Progress on the research project in hand: a share of what's left of it.</summary>
    public class DilemmaEffect_Research : DilemmaEffect
    {
        public float share = 0.25f;
        public float max = 1500f;

        private static ResearchProjectDef Project => Find.ResearchManager?.GetProject(null);

        public override string Unavailable(DilemmaContext ctx) => Project == null ? "CC_Evt_NoResearch".Translate().ToString() : null;

        public override void Apply(DilemmaContext ctx)
        {
            var proj = Project;
            if (proj == null || proj.IsFinished) return;
            float left = proj.Cost - Find.ResearchManager.GetProgress(proj);
            float amount = Mathf.Min(left * share, max, left - 1f);
            if (amount > 1f) Find.ResearchManager.AddProgress(proj, amount, null);
        }
    }

    /// <summary>A bond between A and X (or A and B): friends, rivals, comrades.</summary>
    public class DilemmaEffect_Tie : DilemmaEffect
    {
        public TieKind kind = TieKind.Friends;
        public float amount = 20f;
        public bool withB;

        public override void Apply(DilemmaContext ctx)
        {
            var other = withB ? ctx.b : ctx.stranger;
            if (ctx.a == null || other == null || other.Faction != Faction.OfPlayer) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            CouncilGameComponent.Instance?.social.Bump(ctx.a, other, kind, amount, ctx.why);
        }
    }

    /// <summary>A takes X on as a student of the event's skill.</summary>
    public class DilemmaEffect_Mentor : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            if (ctx.a == null || ctx.stranger == null || ctx.skill == null || ctx.stranger.Faction != Faction.OfPlayer) return;
            CouncilGameComponent.Instance?.social.StartMentorship(ctx.a, ctx.stranger, ctx.skill);
        }
    }

    /// <summary>X arrives with something to hide (Rimfolk's secrets).</summary>
    public class DilemmaEffect_Secret : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            if (ctx.stranger == null || ctx.stranger.Faction != Faction.OfPlayer) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            CouncilGameComponent.Instance?.secrets.GiveNow(ctx.stranger);
        }
    }

    /// <summary>The target colonist leaves the colony for good (walks off the map).</summary>
    public class DilemmaEffect_LeaveColony : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            foreach (var p in Targets(ctx).ToList())
            {
                if (p == null || p.Dead || p.Faction != Faction.OfPlayer || !p.RaceProps.Humanlike) continue;
                foreach (var w in CouncilGameComponent.Instance?.wishes.Where(x => x.state == WishState.Open && x.Involves(p)).ToList() ?? new List<Wish>())
                    CouncilGameComponent.Instance.Resolve(w, WishState.Cancelled);
                PawnBanishUtility.Banish(p, false);
            }
        }
    }

    /// <summary>Recreation for the targets (an evening of tales, a feast with guests).</summary>
    public class DilemmaEffect_Joy : DilemmaEffect
    {
        public float amount = 0.2f;

        public override void Apply(DilemmaContext ctx)
        {
            foreach (var p in Targets(ctx)) p.needs?.joy?.GainJoy(amount, JoyKindDefOf.Social);
        }
    }

    /// <summary>The colony's standing with the event's own faction (for the enemy: <see cref="enemy"/>).</summary>
    public class DilemmaEffect_EventGoodwill : DilemmaEffect
    {
        public int amount;
        public bool enemy;

        public override void Apply(DilemmaContext ctx)
        {
            var f = enemy ? (ctx as HappeningContext)?.enemy : ctx.faction ?? ctx.stranger?.Faction;
            if (f == null || f.IsPlayer || f.defeated || !f.HasGoodwill) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            f.TryAffectGoodwillWith(Faction.OfPlayer, amount, true, false);
        }
    }
}
