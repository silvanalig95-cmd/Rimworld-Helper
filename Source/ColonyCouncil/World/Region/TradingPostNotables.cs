using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A trading post's people by name at once (docs/design/stage1-region.md, item 3): its keeper as the town's head
    /// notable (titled by their age: a trader, a factor, a manager, a quartermaster; for the Empire one of its titled
    /// kinds, so the game shows a title of its own) and its trader, made when the post opens rather than in the notables'
    /// next look. The keeper remembers opening it for the colony, so the post counts as a town the colony has dealt with
    /// and stays among the neighbours by name.
    /// </summary>
    public partial class NotableBook
    {
        /// <summary>
        /// The post's keeper and trader, made now when it has none (and the notables are on); returns the keeper, or null.
        /// <paramref name="note"/>: what the keeper remembers of the colony from the start.
        /// </summary>
        public static Pawn StaffPost(Settlement post, string note)
        {
            var book = Instance;
            if (!On || book == null || post == null || post.Destroyed || post.Faction == null) return null;
            var f = post.Faction;
            var head = book.notables.FirstOrDefault(n => n.town == post.ID && n.role == NotableRole.Head && n.Alive);
            if (head == null)
            {
                var keeper = MakeKeeper(f);
                if (keeper != null)
                {
                    head = book.Add(keeper, post, NotableRole.Head, true, null, null);
                    if (!note.NullOrEmpty()) head.Remember(note, 0f);
                }
            }
            if (!book.notables.Any(n => n.town == post.ID && n.role == NotableRole.Trader))
            {
                var trader = Make(f, NotableRole.Trader, head?.pawn);
                if (trader != null) book.Add(trader, post, NotableRole.Trader, true, null, null);
            }
            Changed();
            return head?.pawn;
        }

        /// <summary>
        /// A post's keeper: one of their caravans' traders (for the Empire, one of its titled kinds from the lower ranks,
        /// never a second ruler), else the person they send to visit; aged 35 to 60, kept in the world while a notable.
        /// </summary>
        private static Pawn MakeKeeper(Faction f)
        {
            try
            {
                PawnKindDef kind = null;
                if (f.def.HasRoyalTitles) kind = RegionFaces.TitledKind(f);
                kind ??= TraderKind(f) ?? Arrivals.PersonKind(f);
                if (kind == null) return null;
                var req = new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                    colonistRelationChanceFactor: 0f, biologicalAgeRange: new FloatRange(35f, 60f), developmentalStages: DevelopmentalStage.Adult, dontGiveWeapon: true);
                var p = PawnGenerator.GeneratePawn(req);
                if (p == null) return null;
                Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                return p;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trading posts: a keeper made for " + f?.def?.defName, e);
                return null;
            }
        }
    }
}
