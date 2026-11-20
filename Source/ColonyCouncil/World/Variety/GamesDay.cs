using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's games day in a season of games (B7 of docs/content-update-proposals.md; Rimfolk_WS_Games): the young of
    /// the world event's two friendly peoples ({FACTION} and {ENEMY}; two and one, or three of one people,
    /// VarietyMath.GamesGuests) come in two days on the first fine morning, each with one or two friends (the feast's own
    /// retinue), for the feast's bouts, race and shooting match (where the colony has targets), sit at the colony's table
    /// and stay the night, as guests of a feast do (Diplomacy/Feasts.cs, FeastPurpose.Games), but without its notables. The
    /// young people are made for it and kept by the world event. A colonist who wins a contest is named in its ending
    /// (GamesHooks) and counts it for their own name, as any contest won at a feast (Diplomacy/Renown.cs).
    /// </summary>
    public class DilemmaEffect_GamesDay : WorldEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var map = SituationEffectUtil.MapOf(ctx);
            if (map == null || !Feasts.On) return;
            sheet.Play("CC_Variety_GamesDay_Impact".Translate().Resolve(), "CC_Variety_GamesDay_ImpactBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = SituationEffectUtil.MapOf(ctx);
            var s = SituationEffectUtil.Of(ctx);
            if (map == null || s == null || !Feasts.On || !Gate(ctx)) return;
            try
            {
                var first = s.ctx.faction;
                var second = s.ctx.enemy != null && !s.ctx.enemy.HostileTo(Faction.OfPlayer) && first != null && !s.ctx.enemy.HostileTo(first) ? s.ctx.enemy : null;
                if (first == null || first.HostileTo(Faction.OfPlayer)) return;
                VarietyMath.GamesGuests(second != null, out int a, out int b);
                var young = new List<Pawn>();
                for (int i = 0; i < a; i++) Add(s, first, young);
                for (int i = 0; i < b; i++) Add(s, second, young);
                if (young.Count == 0) return;
                var contests = VarietyMath.GamesContests(FeastContests.WhyNot(ContestKind.Shooting, map) == null);
                int when = Find.TickManager.TicksGame + (int)(VarietyMath.GamesDayNoticeDays * GenDate.TicksPerDay);
                var plan = Feasts.Host(map, first, young, FeastPurpose.Games, when, VarietyMath.GamesTag(s.id), contests);
                if (plan == null) return;
                // They come for the games, not for a hope of their people's ways.
                foreach (var g in plan.guests) g.hope = FeastHope.None;
                s.SetNum("gamesFeast", plan.id);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a season of games: the games day", e);
            }
        }

        private static void Add(WorldSituation s, Faction f, List<Pawn> into)
        {
            if (f == null) return;
            Pawn p;
            try
            {
                var kind = Arrivals.PersonKind(f);
                if (kind == null) return;
                var req = new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                    colonistRelationChanceFactor: 0f, biologicalAgeRange: new FloatRange(VarietyMath.GamesYoungFrom, VarietyMath.GamesYoungTo));
                p = PawnGenerator.GeneratePawn(req);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a season of games: a young one made", e);
                return;
            }
            if (p == null) return;
            // Kept in the world by the world event until it ends (out there between now and the day, like every guest).
            s.Hold(p);
            into.Add(p);
        }
    }

    /// <summary>
    /// The games day's end heard (Feasts.StageChanged): the young people came (the world event's flag "gamesHeld"), and a
    /// colonist who won a contest is the one its ending names ({A}, the flag "ourWin").
    /// </summary>
    [StaticConstructorOnStartup]
    public static class GamesHooks
    {
        static GamesHooks()
        {
            Feasts.StageChanged += OnStage;
        }

        private static void OnStage(GuestFeast p, Map map, FeastStage to)
        {
            if (p == null || p.purpose != FeastPurpose.Games || !VarietyMath.ParseGamesTag(p.tag, out int id)) return;
            var s = WorldSituations.Instance?.Get(id);
            if (s == null || s.ended) return;
            if (to == FeastStage.Here) s.Set("gamesHeld");
            if (to != FeastStage.Done || p.arrived < 0) return;
            var win = p.results?.Where(r => r != null && !r.off && r.ours && r.winner != null && !r.winner.Dead && r.winner.IsColonist).Select(r => r.winner).FirstOrDefault();
            if (win == null) return;
            s.ctx.a = win;
            s.Set("ourWin");
        }
    }
}
