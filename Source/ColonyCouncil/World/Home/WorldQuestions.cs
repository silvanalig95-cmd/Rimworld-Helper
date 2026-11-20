using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A council question the world brings home (B26 of docs/content-update-proposals.md): it comes only while a world event
    /// of its <see cref="DilemmaDef.situations"/> runs, and its words name it: {NAME} the world event, {PLACE} where it is,
    /// {FACTION} its people, {ENEMY} the other side, {GOD} a vow's god as the one who asks calls it.
    /// </summary>
    public class WorldQuestionContext : DilemmaContext
    {
        public int situationId = -1;
        public string wsName, wsPlace, god;
        public int godId = -1;
        public Faction enemy;
        /// <summary>A side of a war: A's side is the world event's first people (true) or its second (false).</summary>
        public bool aFirst = true;

        public WorldSituation Situation => WorldSeason.Running.FirstOrDefault(s => s.id == situationId);

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            args.Add((wsName ?? "").Named("NAME"));
            args.Add((wsPlace ?? "").Named("PLACE"));
            args.Add((enemy?.Name ?? "").Named("ENEMY"));
            args.Add((god ?? "").Named("GOD"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref situationId, "situationId", -1);
            Scribe_Values.Look(ref wsName, "wsName");
            Scribe_Values.Look(ref wsPlace, "wsPlace");
            Scribe_Values.Look(ref god, "god");
            Scribe_Values.Look(ref godId, "godId", -1);
            Scribe_References.Look(ref enemy, "enemy");
            Scribe_Values.Look(ref aFirst, "aFirst", true);
        }
    }

    /// <summary>
    /// The council's questions of a world event's season (Defs/DilemmaDefs/Rimfolk_Dilemmas_WorldHome.xml): A and B by the
    /// roles, in a context that names the world event.
    /// </summary>
    public class DilemmaWorker_WorldSeason : DilemmaWorker
    {
        protected static List<Pawn> Pawns(Map map) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && People.HasInnerLife(p)).ToList();

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var s = WorldSeason.Of(def.situations);
            if (s == null || !CanHappenNow(map)) return false;
            var pawns = Pawns(map);
            if (pawns.Count < def.minColonists) return false;
            var c = new WorldQuestionContext
            {
                def = def, map = map, count = def.thingCount, situationId = s.id, wsName = WorldSeason.NameOf(s), wsPlace = WorldSeason.PlaceOf(s),
                faction = s.ctx?.faction, enemy = s.ctx?.enemy
            };
            if (!Cast(c, s, pawns)) return false;
            ctx = c;
            return true;
        }

        /// <summary>Who it is put to: A (and B) by the roles.</summary>
        protected virtual bool Cast(WorldQuestionContext c, WorldSituation s, List<Pawn> pawns)
        {
            var aOptions = pawns.Where(p => def.roleA.Allows(p, null)).ToList();
            if (def.roleA.bestAt != null) aOptions = aOptions.OrderByDescending(p => p.skills?.GetSkill(def.roleA.bestAt)?.Level ?? 0).Take(1).ToList();
            foreach (var a in aOptions.InRandomOrder())
            {
                Pawn b = null;
                if (def.roleB != null)
                {
                    b = pawns.Where(p => def.roleB.Allows(p, a)).RandomElementWithFallback();
                    if (b == null) continue;
                }
                c.a = a;
                c.b = b;
                return true;
            }
            return false;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            var s = (ctx as WorldQuestionContext)?.Situation;
            WorldSeason.AddTags(tags, s);
            Peoples.AddTags(s?.ctx?.faction, tags);
        }
    }

    /// <summary>The holy year's vow (Rimfolk_Dlm_WS_Vigil): A is the most devout of the colony, by the god they hold (the gods on), else their faith (Ideology).</summary>
    public class DilemmaWorker_WorldVigil : DilemmaWorker_WorldSeason
    {
        protected override bool Cast(WorldQuestionContext c, WorldSituation s, List<Pawn> pawns)
        {
            var grown = pawns.Where(p => !p.DevelopmentalStage.Juvenile() && def.roleA.Allows(p, null)).ToList();
            if (Worship.On)
            {
                var a = grown.Where(p => Worship.Ours(p) && Worship.GodOf(p) != null && GodLore.Credence(p) >= GodLoreMath.BelieverFrom)
                    .OrderByDescending(p => PawnGods.HoldOf(p, Worship.GodOf(p))).FirstOrDefault();
                if (a != null)
                {
                    var g = Worship.GodOf(a);
                    c.a = a;
                    c.godId = g.id;
                    c.god = Worship.Called(g, a);
                    return true;
                }
            }
            var devout = grown.Where(Faiths.Devout).OrderByDescending(Faiths.Certainty).FirstOrDefault();
            if (devout == null) return false;
            c.a = devout;
            c.god = Faiths.Of(devout)?.name ?? "";
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            tags.Add((ctx as WorldQuestionContext)?.godId >= 0 ? "god" : "faith");
        }
    }

    /// <summary>
    /// The war between neighbours (Rimfolk_Dlm_WS_Sides): A belongs with one side (family among them, their people's good
    /// memory of them, a faith shared: WorldHomeMath.SideTie), B, a friend of A's, with the other. {FACTION} is A's side,
    /// {ENEMY} B's.
    /// </summary>
    public class DilemmaWorker_WorldSides : DilemmaWorker_WorldSeason
    {
        public static int Tie(Pawn p, Faction f)
        {
            if (p == null || f == null) return 0;
            bool family = p.relations?.RelatedPawns?.Any(o => o != null && !o.Dead && o.Faction == f) ?? false;
            bool remembered = ColonistMemory.Standing(f, p) > 0f;
            bool faith = ModsConfig.IdeologyActive && p.Ideo != null && p.Ideo == f.ideos?.PrimaryIdeo;
            return WorldHomeMath.SideTie(family, remembered, faith);
        }

        protected override bool Cast(WorldQuestionContext c, WorldSituation s, List<Pawn> pawns)
        {
            var first = s.ctx?.faction;
            var second = s.ctx?.enemy;
            if (first == null || second == null) return false;
            var grown = pawns.Where(p => !p.DevelopmentalStage.Juvenile()).ToList();
            foreach (var a in grown.OrderByDescending(p => Mathf.Max(Tie(p, first), Tie(p, second))))
            {
                int tf = Tie(a, first), ts = Tie(a, second);
                if (tf == ts) continue;
                bool aFirst = tf > ts;
                var mine = aFirst ? first : second;
                var theirs = aFirst ? second : first;
                var b = grown.Where(p => p != a && Tie(p, theirs) > Tie(p, mine) && (p.relations?.OpinionOf(a) ?? 0) >= 0)
                    .OrderByDescending(p => p.relations?.OpinionOf(a) ?? 0).FirstOrDefault();
                if (b == null) continue;
                c.a = a;
                c.b = b;
                c.aFirst = aFirst;
                c.faction = mine;
                c.enemy = theirs;
                return true;
            }
            return false;
        }
    }

    /// <summary>Kept apart from travellers while the sickness lasts (WorldApart): the world event's flag.</summary>
    public class DilemmaEffect_WorldApart : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            sheet.Add(ImpactKind.Health, 1, "CC_WH_Impact_Apart".Translate().Resolve(), "CC_WH_Impact_ApartBrief".Translate().Resolve(), "whapart");
            sheet.Add(ImpactKind.Goodwill, -1, "CC_WH_Impact_ApartCool".Translate().Resolve(), "CC_WH_Impact_ApartCoolBrief".Translate().Resolve(), "whapartcool");
        }

        public override void Apply(DilemmaContext ctx) => (ctx as WorldQuestionContext)?.Situation?.Set(WorldApart.Flag);
    }

    /// <summary>A's vow of a vigil day each week while the holy year lasts (WorldHomeBook.Vow; each kept by WorldQuestions.VigilDay).</summary>
    public class DilemmaEffect_WorldVow : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a == null) return;
            sheet.Play("CC_WH_Impact_Vow".Translate(ctx.a.LabelShort.Named("PAWN")).Resolve(), "CC_WH_Impact_VowBrief".Translate(ctx.a.LabelShort.Named("PAWN")).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = ctx as WorldQuestionContext;
            if (c?.a == null || c.Situation == null) return;
            WorldHomeBook.Instance?.Vow(c.a, c.map, c.situationId, c.godId);
        }
    }

    /// <summary>The council backs a side of the war (<see cref="forA"/>: A's, else B's): only that side's asks come now, and goodwill with each side moves.</summary>
    public class DilemmaEffect_WorldSide : DilemmaEffect
    {
        public bool forA = true;

        private static Faction Backed(WorldQuestionContext c, bool forA) => forA ? c.faction : c.enemy;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!(ctx is WorldQuestionContext c)) return;
            var backed = Backed(c, forA);
            var other = Backed(c, !forA);
            if (backed == null || other == null) return;
            sheet.Goodwill(backed, WorldHomeMath.BackedGoodwill);
            sheet.Goodwill(other, WorldHomeMath.OtherGoodwill);
            sheet.Add(ImpactKind.Other, 0, "CC_WH_Impact_Side".Translate(backed.Name.Named("BACKED"), other.Name.Named("OTHER")).Resolve(),
                "CC_WH_Impact_SideBrief".Translate(backed.Name.Named("BACKED")).Resolve(), "whside");
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (!(ctx is WorldQuestionContext c)) return;
            var s = c.Situation;
            var backed = Backed(c, forA);
            var other = Backed(c, !forA);
            if (s == null || backed == null || other == null) return;
            // The world event's first people is s.ctx.faction: the flag says which of its two sides the colony backs.
            s.Set(WorldHomeMath.SideFlag(backed == s.ctx.faction));
            WorldSituationWorker.Goodwill(backed, WorldHomeMath.BackedGoodwill, GoodwillWhy.Council);
            WorldSituationWorker.Goodwill(other, WorldHomeMath.OtherGoodwill, GoodwillWhy.Council);
        }
    }

    /// <summary>
    /// Mourning colours for the neighbours' dead: everyone at home has their outermost clothes dyed dark grey (those that
    /// take a colour) until the mourning ends, when each is put back as it was (WorldHomeBook); and the mourning people
    /// hear of it.
    /// </summary>
    public class DilemmaEffect_WorldMourning : DilemmaEffect
    {
        public DilemmaEffect_WorldMourning()
        {
            target = DilemmaTarget.Colony;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var who = Reached(ctx);
            if (who.Count == 0) return;
            sheet.Play("CC_WH_Impact_Mourning".Translate(WhoOf(ctx, who).Named("WHO")).Resolve(), "CC_WH_Impact_MourningBrief".Translate().Resolve());
            if (ctx.faction != null) sheet.Goodwill(ctx.faction, WorldHomeMath.MourningGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = ctx as WorldQuestionContext;
            var book = WorldHomeBook.Instance;
            if (c == null || book == null || c.Situation == null) return;
            var colour = new Color(WorldHomeMath.MourningColour[0], WorldHomeMath.MourningColour[1], WorldHomeMath.MourningColour[2]);
            var dyed = new List<Pawn>();
            foreach (var p in Targets(ctx).Where(p => p.Spawned && p.apparel != null))
            {
                var outer = p.apparel.WornApparel.Where(ap => ap.TryGetComp<CompColorable>() != null && ap.def.apparel?.bodyPartGroups != null && ap.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))
                    .OrderByDescending(ap => ap.def.apparel.LastLayer.drawOrder).FirstOrDefault();
                if (outer == null || book.dyed.Count >= WorldHomeMath.MaxDyed || book.dyed.Any(d => d.apparel == outer)) continue;
                var comp = outer.TryGetComp<CompColorable>();
                var was = outer.DrawColor;
                book.dyed.Add(new MourningDye { apparel = outer, situation = c.situationId, had = comp.Active, r = was.r, g = was.g, b = was.b, a = was.a });
                outer.SetColor(colour, false);
                dyed.Add(p);
            }
            if (ctx.faction != null) WorldSituationWorker.Goodwill(ctx.faction, WorldHomeMath.MourningGoodwill, GoodwillWhy.Council);
            if (dyed.Count > 0)
                Tell.Message(NewsKind.People, Importance.Notable, ctx.map, "CC_WH_Mourning_Dyed".Translate(c.wsName.Named("NAME"), dyed.Count.Named("COUNT")).Resolve(),
                    new LookTargets(dyed), MessageTypeDefOf.NeutralEvent);
        }
    }

    /// <summary>What the vows and the colours do over the days (from WorldHomeBook's hour).</summary>
    public static class WorldQuestions
    {
        private static Thing PlaceFor(Pawn p, God g, Map map)
        {
            if (g != null)
            {
                var shrine = Shrines.Of(map, g).Select(s => s.thing).FirstOrDefault(t => t != null && t.Spawned);
                if (shrine != null) return shrine;
            }
            return WorldScenes.HolyPlace(map, p.Position);
        }

        /// <summary>A vigil day due now: the one who vowed it keeps vigil at their god's place (else an altar, else the gathering spot) most of the working day; their belief and mood rise once it is kept.</summary>
        public static bool VigilDay(VigilVow v)
        {
            var p = v.pawn;
            var map = v.map;
            var g = v.god >= 0 ? Gods.ById(v.god) : null;
            var place = PlaceFor(p, g, map);
            var ctx = new HappeningContext { map = map, a = p, place = place };
            var vigil = new DilemmaEffect_Vigil
            {
                target = DilemmaTarget.A, count = 1, to = place != null ? PlaySpot.Place : PlaySpot.Gather,
                hours = new IntRange(WorldHomeMath.VigilFrom, WorldHomeMath.VigilTo + 1), duration = new FloatRange(WorldHomeMath.VigilHoursMin, WorldHomeMath.VigilHoursMax),
                label = "CC_WH_VigilLabel".Translate().Resolve()
            };
            var thought = new DilemmaEffect_Thought { target = DilemmaTarget.A, thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_WH_VigilKept") };
            List<PlayOut> plays;
            using (PlayOuts.Collect())
            {
                HappeningTracker.Safe(vigil, ctx);
                plays = PlayOuts.Collected();
                // The mood only for a vigil set going (held until it is kept), or at once with the setting off.
                if (thought.thought != null && (plays.Count > 0 || !PlayOuts.On)) HappeningTracker.Safe(thought, ctx);
            }
            // With "Answers play out on the map" off nothing is set going: the day is kept as answers were before, its mood
            // given at once above.
            if (plays.Count == 0 && PlayOuts.On) return false;
            if (g != null) PawnGods.Move(p, g, BeliefEvent.VowKept);
            else Faiths.Steady(p, 0.04f);
            string told = "CC_WH_Vigil_Kept".Translate(p.LabelShort.Named("PAWN")).Resolve();
            if (!PlayOuts.TellWhenDone(plays, told)) Tell.Message(NewsKind.People, Importance.Flavor, map, told, new LookTargets(p), MessageTypeDefOf.NeutralEvent);
            return true;
        }

        /// <summary>The holy year is over: a vow kept to its end is told.</summary>
        public static void VowDone(VigilVow v)
        {
            if (v?.pawn == null || v.kept == 0 || v.pawn.Dead) return;
            Tell.Message(NewsKind.People, Importance.Notable, v.map, "CC_WH_Vow_Done".Translate(v.pawn.LabelShort.Named("PAWN"), v.kept.Named("COUNT")).Resolve(),
                new LookTargets(v.pawn), MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>The mourning is over: a piece still in its mourning colour is put back as it was.</summary>
        public static void Undye(MourningDye d)
        {
            var t = d?.apparel as ThingWithComps;
            var comp = t?.TryGetComp<CompColorable>();
            if (comp == null) return;
            var mourning = new Color(WorldHomeMath.MourningColour[0], WorldHomeMath.MourningColour[1], WorldHomeMath.MourningColour[2]);
            // Dyed again by the colony since: theirs to keep.
            if (!comp.Active || !comp.Color.IndistinguishableFrom(mourning)) return;
            if (d.had) t.SetColor(new Color(d.r, d.g, d.b, d.a), false);
            else comp.Disable();
        }
    }
}
