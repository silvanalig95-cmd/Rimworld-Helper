using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The council's card after a remarkable death (Defs/GodDefs/Rimfolk_Saints.xml, Rimfolk_Dlm_Saint): the fallen, their
    /// deed and the god they held. {DEAD} their name, {DEED} why ("who kept the sick for years"), {SAINT} their title as a saint
    /// ("Saint Mira"), {ASAINT} what the council would name them ("a saint of Hollin"; "a saint" where the colony doesn't know
    /// the god), {GOD} the colony's name for their god.
    /// </summary>
    public class SaintDilemmaContext : DilemmaContext
    {
        public SaintCandidate dead;

        public God God => dead == null ? null : Gods.ById(dead.god);

        /// <summary>The title they would have, by the colony's age now and their faith's culture.</summary>
        public string Set => SaintsMath.WordSet((int)Ages.Ours, dead?.ideo?.culture?.defName);

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            string name = dead?.name ?? "?";
            args.Add(name.Named("DEAD"));
            args.Add(("CC_Saint_Deed_" + (dead?.reason ?? SaintReason.None)).Translate().Resolve().Named("DEED"));
            args.Add(Saints.Title(name, Set).Named("SAINT"));
            args.Add(Saints.ASaint(Set, God).Named("ASAINT"));
            args.Add(Worship.Called(God).Named(GodsMath.God));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref dead, "dead");
        }
    }

    /// <summary>
    /// The council is asked about a fallen colonist whose death may merit a saint (Gods/Saints/Saints.cs, Died; asked in its
    /// moment, DilemmaDef.timely, within SaintsMath.CandidateDays of the death). The one who asks is the priest, else the most
    /// devout of those who hold the fallen one's god, else the most devout grown colonist with a god.
    /// </summary>
    public class DilemmaWorker_Saint : DilemmaWorker
    {
        public override bool CanHappenNow(Map map) => Saints.On && base.CanHappenNow(map) && Saints.Waiting(map, false) != null;

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            var c = Saints.Waiting(map, false);
            var g = c == null ? null : Gods.ById(c.god);
            if (c == null) return false;
            var a = GodSpeaker.Of(map, def.roleA, g) ?? GodSpeaker.Of(map, def.roleA);
            if (a == null) return false;
            Saints.Waiting(map, true);
            ctx = new SaintDilemmaContext { def = def, map = map, a = a, dead = c };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            var c = (ctx as SaintDilemmaContext)?.dead;
            if (c != null) tags.Add("reason:" + c.reason.ToString().ToLowerInvariant());
            if (ctx?.a != null && Worship.IsPriest(ctx.a)) tags.Add("priest");
            if (Saints.OfGod((ctx as SaintDilemmaContext)?.God) != null) tags.Add("known");
        }
    }

    /// <summary>
    /// The council names the fallen a saint (Saints.Name): the colony's own, in its book under their god, their grave a place
    /// to pray, their day kept each year by the devoted, a relic set aside, a small good mood for the colony's people (not
    /// its scoffers). Never anything for their kin.
    /// </summary>
    public class DilemmaEffect_NameSaint : DilemmaEffect
    {
        private static List<Pawn> Heard(DilemmaContext ctx) =>
            ctx?.map == null ? new List<Pawn>() : Homes.People(ctx.map).Where(p => Worship.Ours(p) && !Worship.Scoffer(p)).ToList();

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var sc = ctx as SaintDilemmaContext;
            if (sc?.dead == null || !Saints.On) return;
            string title = Saints.Title(sc.dead.name, sc.Set);
            sheet.Add(ImpactKind.Other, 1, "CC_Saint_Impact_Named".Translate(title.Named("SAINT")).Resolve(), "CC_Saint_Impact_NamedBrief".Translate(title.Named("SAINT")).Resolve());
            var who = Heard(ctx);
            if (who.Count > 0 && SaintsDefOf.Rimfolk_Saint_Named != null) sheet.Thought(who, WhoOf(ctx, who), SaintsDefOf.Rimfolk_Saint_Named, p => null);
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's word, for the colony as a whole.
            var sc = ctx as SaintDilemmaContext;
            if (sc?.dead == null || ctx.map == null) return;
            Saints.Name(sc.dead, ctx.map, ctx.a);
        }
    }

    /// <summary>
    /// Words said at the fallen one's grave (else at their god's place, else where the colony gathers): the one who asked
    /// speaks, those who come listen; the answer's own words and pool.
    /// </summary>
    public class DilemmaEffect_SaintWords : DilemmaEffect_Words
    {
        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            if (!base.Fill(p, ctx)) return false;
            var sc = ctx as SaintDilemmaContext;
            if (sc?.dead == null || ctx.map == null) return true;
            Thing place = PlaySpots.GraveOf(ctx.map, sc.dead.pawnId);
            var g = sc.God;
            if (place == null && g != null) place = Shrines.Of(ctx.map, g).Select(s => s.thing).FirstOrDefault();
            if (place != null && place.Spawned && place.Map == ctx.map)
            {
                p.place = place;
                p.spots.Remove(PlaySpot.Place);
                p.spots.Insert(0, PlaySpot.Place);
            }
            return true;
        }
    }
}
