using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A fight that comes at once, with its own story in the raid letter. Subclasses say who comes and why; the raid
    /// is sent when the event fires, so there's no second letter.
    /// </summary>
    public abstract class HappeningWorker_Attack : IncidentWorker_Happening
    {
        protected virtual Pawn Leader(HappeningContext ctx) => null;

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            string label = ctx.Format(Pick(ext.titles, ctx) ?? def.letterLabel ?? def.label).CapitalizeFirst();
            ctx.text = ctx.Format(Pick(ext.texts, ctx));
            ctx.why = ctx.cause = label;
            return Raids.Send(ctx.map, ctx.enemy, PointsNow(ctx.map, ext.points), label, ctx.text, Leader(ctx));
        }

        protected override void Send(HappeningContext ctx, HappeningExtension ext)
        {
            // The raid letter already told the story.
            using (Causes.Push("CC_Cause_EventHappened".Translate(ctx.why).Resolve()))
            {
                foreach (var e in ext.effects) HappeningTracker.Safe(e, ctx);
            }
            Happened(ctx, ext, ctx.why);
            Attacked(ctx);
        }

        protected virtual void Attacked(HappeningContext ctx)
        {
        }
    }

    /// <summary>A nemesis comes back at the head of their people, for unfinished business.</summary>
    public class HappeningWorker_NemesisReturns : HappeningWorker_Attack
    {
        private static Nemesis Usable()
        {
            var nemeses = CouncilGameComponent.Instance?.nemeses;
            if (nemeses == null || !S.nemeses) return null;
            return nemeses.Open.Where(n =>
            {
                var x = n.pawn;
                return !n.IsBeast && !n.captured && x != null && !x.Dead && !x.Discarded && !x.Spawned && Find.WorldPawns.Contains(x)
                       && Raids.CanRaid(x.Faction) && !x.IsPrisoner && !People.Kidnapped(x) && x.GetCaravan() == null
                       && !QuestUtility.IsReservedByQuestOrQuestBeingGenerated(x) && x.health.summaryHealth.SummaryHealthPercent >= 0.7f;
            }).RandomElementWithFallback();
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => Usable() != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var n = Usable();
            if (n == null) return false;
            ctx.stranger = n.pawn;
            ctx.enemy = n.pawn.Faction;
            ctx.dead = n.victimName;
            // Told from the one they hurt, if that person is still with us.
            ctx.a = n.victim != null && !n.victim.Dead && n.victim.Spawned && n.victim.Map == ctx.map ? n.victim
                : Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            return ctx.a != null;
        }

        protected override void Abort(HappeningContext ctx) => ctx.stranger = null;

        protected override Pawn Leader(HappeningContext ctx) => ctx.stranger;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var n = CouncilGameComponent.Instance?.nemeses.For(ctx.stranger);
            if (n?.deed != null) tags.Add(n.deed);
            if (ctx.a != null && n?.victim == ctx.a) tags.Add("victim");
        }

        protected override void Attacked(HappeningContext ctx)
        {
            var comp = CouncilGameComponent.Instance;
            var n = comp?.nemeses.For(ctx.stranger);
            if (n != null) comp.nemeses.Notify_LedRaid(comp, n);
        }
    }

    /// <summary>The people of a prisoner who died in the colony's hands come to settle it.</summary>
    public class HappeningWorker_Revenge : HappeningWorker_Attack
    {
        private static Grudge Usable()
        {
            var t = CouncilGameComponent.Instance?.happenings;
            int now = Find.TickManager.TicksGame;
            return t?.grudges.Where(g => !g.settled && Raids.CanRaid(g.faction) && now - g.tick > GenDate.TicksPerDay * 8).RandomElementWithFallback();
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => Usable() != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var g = Usable();
            if (g == null) return false;
            ctx.enemy = g.faction;
            ctx.dead = g.name;
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            return ctx.a != null;
        }

        protected override void Attacked(HappeningContext ctx)
        {
            var grudges = CouncilGameComponent.Instance?.happenings.grudges;
            foreach (var g in grudges?.Where(g => g.faction == ctx.enemy && g.name == ctx.dead) ?? Enumerable.Empty<Grudge>()) g.settled = true;
        }
    }

    /// <summary>A hostile people demands tribute. Pay, haggle, or refuse and fight.</summary>
    public class HappeningWorker_Tribute : IncidentWorker_Happening
    {
        private static bool HasHome(Faction f) => Find.WorldObjects.Settlements.Any(s => s.Faction == f);

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            GenDate.DaysPassed >= 20 && map.wealthWatcher.WealthTotal >= 8000f && Raids.Enemy(HasHome) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.enemy = Raids.Enemy(HasHome);
            if (ctx.enemy == null) return false;
            var pawns = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).ToList();
            ctx.a = pawns.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.count = Mathf.Clamp(Mathf.RoundToInt(ctx.map.wealthWatcher.WealthTotal * Rand.Range(0.008f, 0.014f) / 10f) * 10, 150, 2500);
            return true;
        }

        protected override string Extra(HappeningContext ctx) =>
            "CC_Evt_TributeHave".Translate(ctx.map.resourceCounter.Silver, ctx.count).Resolve();
    }
}
