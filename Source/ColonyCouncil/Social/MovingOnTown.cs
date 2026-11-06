using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Moving there (docs/design/stage3-many-colonies.md, section 2, "Homecomings and moving there"; release 2b): A has a
    /// spouse, a child or a close friend among those who stayed at a town the colony was, still its people's and at peace
    /// with the colony, and asks the council to go and live there. A moving-on question of its own (Social/MovingOn.cs,
    /// Defs/DilemmaDefs/Rimfolk_Dilemmas_TownKin.xml), asked about once a person and one of theirs, never one of the
    /// colony's last four grown-ups. Going, they join the town's people, one of its former colonists. These ties are
    /// personal only: nothing is handed down. Tags: "spouse", "child" or "friend" for what they are to them.
    /// </summary>
    public class DilemmaWorker_TownKin : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!MovingOn.On || !TownsAfter.LifeOn || WorldLedger.Formers.Count == 0 || !CanHappenNow(map) || !MovingOn.Spare(map)) return false;
            var options = new List<(Pawn a, Pawn x, LedgerFormer rec, bool spouse, bool child)>();
            foreach (var rec in WorldLedger.Formers)
            {
                if (rec == null || !rec.Standing || rec.end != TownEnd.None || !MovingOn.Friendly(rec.people)) continue;
                foreach (var a in map.mapPawns.FreeColonistsSpawned.Where(MovingOn.Could))
                    foreach (var x in rec.formers)
                    {
                        if (x?.relations == null || x.Dead || x.Faction != rec.people || x.IsPrisoner || MovingOn.Asked(MovingOn.Key("town", a, x))) continue;
                        bool spouse = a.relations.DirectRelationExists(PawnRelationDefOf.Spouse, x);
                        bool child = x.relations.DirectRelationExists(PawnRelationDefOf.Parent, a);
                        bool friend = FormerMath.CloseFriends(a.relations.OpinionOf(x), x.relations.OpinionOf(a));
                        if (FormerMath.KinThere(spouse, child, friend)) options.Add((a, x, rec, spouse, child));
                    }
            }
            if (options.Count == 0) return false;
            var (pa, px, prec, isSpouse, isChild) = options.RandomElementByWeight(o => FormerMath.MoveStrength(o.spouse, o.child, o.a.relations.OpinionOf(o.x)));
            ctx = new TownKinContext
            {
                def = def, map = map, a = pa, faction = prec.people, relativeName = px.LabelShort,
                relation = isSpouse || isChild ? Family.Relation(pa, px) : "CC_TownAfter_CloseFriend".Translate().Resolve(), kind = "family", relative = px,
                strength = FormerMath.MoveStrength(isSpouse, isChild, pa.relations.OpinionOf(px)), town = prec.key, townName = TownsAfter.TownName(prec),
                tie = isSpouse ? "spouse" : isChild ? "child" : "friend"
            };
            MovingOn.MarkAsked(MovingOn.Key("town", pa, px));
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx is TownKinContext t && !t.tie.NullOrEmpty()) tags.Add(t.tie);
        }
    }

    /// <summary>A question about moving to a town the colony was: the town ({TOWN}) and what the one there is to them.</summary>
    public class TownKinContext : MovingOnContext
    {
        public int town = -1;
        public string townName;
        /// <summary>"spouse", "child" or "friend".</summary>
        public string tie;

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            args.Add((townName ?? "").Named("TOWN"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_Values.Look(ref townName, "townName");
            Scribe_Values.Look(ref tie, "tie");
        }
    }

    /// <summary>Going to live at a town the colony was: they are one of its former colonists from then on (its notables and its homecomings know them).</summary>
    public class DilemmaEffect_JoinTown : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's word; their going is the walk to the gate the answer set going.
            if (!(ctx is TownKinContext t) || ctx.a == null || ctx.a.Dead) return;
            var rec = WorldLedger.FormerOf(t.town);
            if (rec == null || !rec.Standing || rec.end != TownEnd.None) return;
            if (!rec.formers.Contains(ctx.a)) rec.formers.Add(ctx.a);
            WorldLedger.Keep(ctx.a, KeepFor.Former, false);
            WorldLedger.FormerChanged();
        }
    }
}
