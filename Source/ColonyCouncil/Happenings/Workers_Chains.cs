using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // World events that pick up where an earlier one left off (see Memo in Happenings.cs): a deserter's old unit finds
    // them, a lost child taken home comes back grown, a beaten champion wants a rematch, a felled beast's young appear,
    // fed pilgrims send their thanks.

    /// <summary>The deserter the colony took in has been found by their old unit.</summary>
    public class HappeningWorker_DeserterFound : IncidentWorker_Happening
    {
        private static Memo Pick(Map map) =>
            CouncilGameComponent.Instance?.happenings.Memos("deserter", 20f).Where(m =>
                m.pawn != null && !m.pawn.Dead && m.pawn.IsColonist && m.pawn.Spawned && m.pawn.Map == map && Raids.CanRaid(m.faction)).RandomElementWithFallback();

        protected override bool CanHappen(Map map, HappeningExtension ext) => Pick(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var m = Pick(ctx.map);
            if (m == null) return false;
            ctx.a = m.pawn;
            ctx.enemy = m.faction;
            float points = StorytellerUtility.DefaultThreatPointsNow(ctx.map);
            ctx.count = Mathf.Clamp(Mathf.RoundToInt(points * Rand.Range(1.2f, 1.8f) / 10f) * 10, 200, 3000);
            CouncilGameComponent.Instance.happenings.Use(m);
            return true;
        }
    }

    /// <summary>The lost child the colony took home, back years later to say thank you.</summary>
    public class HappeningWorker_ChildGrown : HappeningWorker_Visitors
    {
        private static Memo Pick() =>
            CouncilGameComponent.Instance?.happenings.Memos("child", 90f).Where(m =>
            {
                var x = m.pawn;
                return x != null && !x.Dead && !x.Discarded && !x.Spawned && Find.WorldPawns.Contains(x) && x.Faction != null && !x.Faction.IsPlayer
                       && !x.Faction.defeated && !x.Faction.HostileTo(Faction.OfPlayer) && !x.IsPrisoner && !People.Kidnapped(x) && x.GetCaravan() == null
                       && !QuestUtility.IsReservedByQuestOrQuestBeingGenerated(x);
            }).RandomElementWithFallback();

        protected override bool CanHappen(Map map, HappeningExtension ext) => Colonists(map).Count > 0 && Pick() != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var m = Pick();
            if (m == null) return false;
            ctx.stranger = m.pawn;
            ctx.faction = m.pawn.Faction;
            ctx.variant = m.pawn.DevelopmentalStage.Juvenile() ? 0 : 1;
            var colonists = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Baby()).ToList();
            ctx.a = m.other != null && colonists.Contains(m.other) ? m.other : colonists.RandomElementWithFallback();
            if (ctx.a == null) return false;
            CouncilGameComponent.Instance.happenings.Use(m);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        protected override void Abort(HappeningContext ctx) => ctx.stranger = null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => tags.Add(ctx.variant == 1 ? "grown" : "young");

        public override string Unavailable(HappeningContext ctx, int option) =>
            option == 0 && (ctx.stranger == null || ctx.stranger.DevelopmentalStage.Juvenile()) ? "CC_Evt_TooYoung".Translate().ToString() : null;
    }

    /// <summary>A travelling champion who met the colony's best before comes back for another contest.</summary>
    public class HappeningWorker_Rematch : HappeningWorker_Champion
    {
        private static bool CanCompete(Pawn p, SkillDef sk) =>
            !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.InMentalState && p.skills?.GetSkill(sk) is SkillRecord r && !r.TotallyDisabled && r.Level >= 4
            && ((sk != SkillDefOf.Shooting && sk != SkillDefOf.Melee) || !p.WorkTagIsDisabled(WorkTags.Violent));

        private static Memo Pick(Map map) =>
            CouncilGameComponent.Instance?.happenings.Memos("challenger", 45f).Where(m =>
            {
                var x = m.pawn;
                var sk = DefDatabase<SkillDef>.GetNamedSilentFail(m.def ?? "");
                return sk != null && x != null && !x.Dead && !x.Discarded && !x.Spawned && Find.WorldPawns.Contains(x) && x.Faction != null
                       && !x.Faction.defeated && !x.Faction.HostileTo(Faction.OfPlayer) && !x.IsPrisoner && !People.Kidnapped(x) && x.GetCaravan() == null
                       && !QuestUtility.IsReservedByQuestOrQuestBeingGenerated(x) && Colonists(map).Any(p => CanCompete(p, sk));
            }).RandomElementWithFallback();

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            Pick(map) != null && !(CouncilGameComponent.Instance?.happenings.visits.Any(v => v.kind == "contest") ?? false);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var m = Pick(ctx.map);
            if (m == null) return false;
            ctx.skill = DefDatabase<SkillDef>.GetNamedSilentFail(m.def);
            ctx.contest = true;
            ctx.stranger = m.pawn;
            ctx.faction = m.pawn.Faction;
            // Last time's opponent if they're still the best; otherwise whoever is now.
            var able = Colonists(ctx.map).Where(p => CanCompete(p, ctx.skill)).ToList();
            ctx.a = able.OrderByDescending(p => p.skills.GetSkill(ctx.skill).Level).FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.variant = m.won ? 1 : 0;
            int ours = ctx.a.skills.GetSkill(ctx.skill).Level;
            // They've been practising.
            var r = m.pawn.skills?.GetSkill(ctx.skill);
            if (r == null || r.TotallyDisabled) return false;
            r.Level = Mathf.Clamp(Mathf.Max(r.Level + 2, ours - 1), 1, 20);
            ctx.count = Mathf.Clamp(Mathf.RoundToInt((60f + ours * 15f) / 10f) * 10, 60, 500);
            CouncilGameComponent.Instance.happenings.Use(m);
            return true;
        }

        // They were someone before this event: never thrown away.
        protected override void Abort(HappeningContext ctx) => ctx.stranger = null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            tags.Add(ctx.variant == 1 ? "won" : "lost");
        }
    }

    /// <summary>The young of a great beast the colony brought down, grown and roaming the same land.</summary>
    public class HappeningWorker_BeastHeir : HappeningWorker_GreatBeast
    {
        private static Memo Pick(Map map) =>
            CouncilGameComponent.Instance?.happenings.Memos("beast", 40f).Where(m =>
                !m.text.NullOrEmpty() && Fits(DefDatabase<PawnKindDef>.GetNamedSilentFail(m.def ?? ""), map)).RandomElementWithFallback();

        private Memo picked;

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            CouncilGameComponent.Instance?.happenings.beast == null && Pick(map) != null;

        protected override PawnKindDef PickKind(HappeningContext ctx)
        {
            picked = Pick(ctx.map);
            return picked == null ? null : DefDatabase<PawnKindDef>.GetNamedSilentFail(picked.def);
        }

        protected override string PickName(HappeningContext ctx, HappeningExtension ext, PawnKindDef kind)
        {
            ctx.dead = picked?.text;
            CouncilGameComponent.Instance?.happenings.Use(picked);
            return "CC_Evt_HeirName".Translate(picked?.text ?? kind.label).Resolve();
        }

        /// <summary>Younger than its parent was, and not quite as tough yet.</summary>
        protected override FloatRange AgeShare => new FloatRange(0.3f, 0.4f);
    }

    /// <summary>Pilgrims the colony fed send their thanks down the road.</summary>
    public class HappeningWorker_PilgrimsReturn : IncidentWorker_Happening
    {
        private static Memo Pick() =>
            CouncilGameComponent.Instance?.happenings.Memos("pilgrims", 15f).Where(m =>
                m.faction != null && !m.faction.defeated && !m.faction.HostileTo(Faction.OfPlayer)).RandomElementWithFallback();

        protected override bool CanHappen(Map map, HappeningExtension ext) => Colonists(map).Count > 0 && Pick() != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var m = Pick();
            if (m == null || !base.Cast(ctx, ext)) return false;
            ctx.faction = m.faction;
            CouncilGameComponent.Instance.happenings.Use(m);
            return true;
        }
    }
}
