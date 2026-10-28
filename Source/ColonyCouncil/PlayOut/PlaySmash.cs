using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// An unflattering likeness (Council_Dilemma_Sculpture): only about a sculpture of A's that really stands in the
    /// colony, the newest they made, named by its own title ({PLACE}). Smashed, the one it mocks (B) goes to it with a
    /// hammer, breaks it to rubble when the visit is over and sweeps up the pieces (DilemmaEffect_Smash, PlayAfter).
    /// </summary>
    public class DilemmaWorker_Likeness : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!base.TryCast(map, out var c) || c?.a == null) return false;
            var piece = PlaySmash.PieceBy(c.a, map);
            if (piece == null) return false;
            ctx = new HappeningContext
            {
                def = c.def, map = map, a = c.a, b = c.b, count = c.count, place = piece,
                placeName = piece.TryGetComp<CompArt>()?.Title ?? piece.LabelNoCount
            };
            return true;
        }

        protected override bool Extra(DilemmaContext ctx) => PlaySmash.PieceBy(ctx.a, ctx.map) != null && base.Extra(ctx);
    }

    /// <summary>
    /// The event's piece (the likeness) is broken up: when the visit to it the answer set going is over (or falls
    /// through), else now. Before nothing else in the answer; after the visit to it.
    /// </summary>
    public class DilemmaEffect_Smash : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var piece = (ctx as HappeningContext)?.place;
            if (piece == null || piece.Destroyed || !piece.Spawned) return;
            var visit = PlayOuts.On ? PlayOuts.Collected().FirstOrDefault(p => p.kind == PlayKind.Visit && p.state == PlayState.Pending && p.afterward.NullOrEmpty()) : null;
            if (visit != null)
            {
                visit.place = piece;
                visit.afterward = PlayAfter.Smash;
                return;
            }
            PlaySmash.Smash(piece, ctx.b);
        }
    }

    public static class PlaySmash
    {
        /// <summary>The newest sculpture standing in the colony that this person made, or null.</summary>
        public static Thing PieceBy(Pawn a, Map map)
        {
            if (a?.Name == null || map == null) return null;
            string name = ColoredText.StripTags(a.NameFullColored.RawText ?? "");
            if (name.NullOrEmpty()) return null;
            return map.listerThings.ThingsInGroup(ThingRequestGroup.Art)
                .Where(t => t != null && t.Spawned && t.def.IsArt && t.Faction == Faction.OfPlayer && !t.Position.Fogged(map)
                            && t.TryGetComp<CompArt>() is CompArt art && art.Active && ColoredText.StripTags(art.AuthorName.RawText ?? "") == name)
                .OrderByDescending(t => t.thingIDNumber).FirstOrDefault();
        }

        /// <summary>The piece goes to rubble where it stood, and the one who broke it sweeps the pieces up.</summary>
        public static void Smash(Thing piece, Pawn by)
        {
            if (piece == null || piece.Destroyed || !piece.Spawned) return;
            var map = piece.Map;
            var cells = piece.OccupiedRect().ExpandedBy(1).Cells.Where(c => c.InBounds(map) && c.Standable(map)).ToList();
            try
            {
                piece.Destroy(DestroyMode.KillFinalize);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a sculpture smashed", e);
                if (!piece.Destroyed) piece.Destroy();
            }
            foreach (var c in cells.Where(c => c.Standable(map)).InRandomOrder().Take(4))
                FilthMaker.TryMakeFilth(c, map, ThingDefOf.Filth_RubbleBuilding, 1);
            if (by == null || by.Dead || !by.Spawned || by.Map != map || by.Downed || by.Drafted) return;
            // Swept up by the one who broke it: the game's own cleaning, of this rubble.
            var rubble = cells.SelectMany(c => c.GetThingList(map).OfType<Filth>()).Where(f => f.def == ThingDefOf.Filth_RubbleBuilding && by.CanReserve(f)).Distinct().ToList();
            if (rubble.Count == 0) return;
            var job = JobMaker.MakeJob(JobDefOf.Clean);
            foreach (var f in rubble) job.AddQueuedTarget(TargetIndex.A, f);
            by.jobs?.jobQueue?.EnqueueFirst(job);
        }
    }
}
