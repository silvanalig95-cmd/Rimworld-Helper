using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The world events of the content update's variety (part B of docs/content-update-proposals.md: B6, B7, B8 and the six
    /// of this round, Defs/WorldSituationDefs/Rimfolk_WS_Variety4.xml and _Variety5.xml): their people's own age as a tag of
    /// their lines ([theirTribal], [theirMedieval], [theirIndustrial], [theirSpacer], VarietyMath.TheirAgeTag), so each age
    /// has its own form of the event, and [bound] when their people is bound to the colony by a tie (Peoples/PeopleTies.cs).
    /// </summary>
    public class SituationWorker_Variety : WorldSituationWorker
    {
        protected override void AddTags(WorldSituation s, HashSet<string> tags)
        {
            base.AddTags(s, tags);
            var f = s?.ctx?.faction;
            if (f == null) return;
            string age = VarietyMath.TheirAgeTag(Ages.Tag(Ages.Of(f)));
            if (age != null) tags.Add(age);
            // Bound to the colony by a tie (a pact, a marriage, a ward, an envoy, fealty, a debt of honour: Peoples/PeopleTies.cs).
            if (PeopleTies.Bound(f)) tags.Add("bound");
        }
    }

    /// <summary>
    /// Cloth gone scarce (B6): it comes on its own, or more readily (VarietyMath.ClothFactor) after a sickness in the herds or
    /// markets closed, in which case it is that world event's people's, its lines can ask for [after:defName] and {TALE}
    /// names what came before.
    /// </summary>
    public class SituationWorker_ClothScarce : SituationWorker_Variety
    {
        /// <summary>The newest sickness in the herds or markets closed that ended lately and wasn't followed by cloth yet.</summary>
        private SituationRecord Before()
        {
            var history = WorldSituations.Instance?.history;
            if (history == null) return null;
            int now = Now;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var r = history[i];
                if (r == null || r.Followed(def.defName)) continue;
                if (VarietyMath.CountsForCloth(r.defName, r.ending == null || r.ending == WorldSituations.Quietly, (now - r.endTick) / (float)GenDate.TicksPerDay)) return r;
            }
            return null;
        }

        public override float WeightFactor(Map map) => VarietyMath.ClothFactor(Before() != null);

        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            var r = Before();
            if (r == null) return true;
            r.Follow(def.defName);
            s.ctx.tale = r.Called;
            s.Set("after:" + r.defName);
            if (r.faction != null && Candidates(def.faction, null, map).Contains(r.faction))
            {
                s.ctx.faction = r.faction;
                s.At(NearestTown(r.faction, map), r.faction.Name);
            }
            return true;
        }
    }

    /// <summary>
    /// A season of games (B7): a town of a friendly people holds its games, in its age's form. The region's best is one of
    /// theirs ({X}), named in the ending. While it runs, travelling champions come more often (its modifiers), and the
    /// colony may hold a games day of its own (GamesDay.cs) for the young of two friendly peoples ({FACTION}, {ENEMY}): a
    /// colonist who wins there is named in the ending ({A}, [ourWin]), and counts it for their own name as any contest won
    /// at a feast does (Diplomacy/Renown.cs).
    /// </summary>
    public class SituationWorker_Games : SituationWorker_Variety
    {
        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            if (s.ctx.enemy != null && (s.ctx.enemy.HostileTo(Faction.OfPlayer) || s.ctx.enemy.HostileTo(s.ctx.faction))) s.ctx.enemy = null;
            var champ = MakePerson(s, s.ctx.faction, null, new FloatRange(19f, 32f), true);
            if (champ == null) return false;
            s.ctx.stranger = champ;
            return true;
        }

        /// <summary>The games day can't be called while the colony has guests coming already, or with feasts switched off.</summary>
        public override string Unavailable(WorldSituation s, SituationContext ctx, SituationBeat beat, int option)
        {
            if (beat?.key != "gamesDay" || option != 0) return null;
            var map = SituationEffectUtil.MapOf(ctx);
            if (!Feasts.On) return "CC_Variety_GamesDay_Off".Translate().Resolve();
            if (map == null || Feasts.At(map) != null) return "CC_Variety_GamesDay_Busy".Translate().Resolve();
            return null;
        }
    }

    /// <summary>
    /// A pardon proclaimed (B8): after a kind new head takes their seat (the succession's follow-up, its flag "kind"), or
    /// while a holy year runs, by that year's people ([holyYear]). Outlaws walk home along the roads and one asks a meal at
    /// the gate; the colony's prisoners of that people may be sent home under it (Pardon.cs), asked only when the colony
    /// holds some; while it runs, bounty hunters come no more and the outlaw quest is rarer (its modifiers).
    /// </summary>
    public class SituationWorker_Pardon : SituationWorker_Variety
    {
        /// <summary>The holy year running now, of a people at peace with the colony, or null.</summary>
        public static WorldSituation HolyYear() =>
            WorldSituations.Instance?.Running.FirstOrDefault(s => s.def?.defName == "Council_WS_HolyYear" && s.ctx?.faction != null && !s.ctx.faction.HostileTo(Faction.OfPlayer));

        public override bool CanStart(Map map)
        {
            bool after = AfterRecord(map) != null;
            if (!VarietyMath.PardonPossible(after, HolyYear() != null)) return false;
            if (after) return base.CanStart(map);
            // In a holy year: its own people proclaim it, so the follow-up and the usual choice of people don't apply.
            if (map == null || IncidentWorker_Happening.Colonists(map).Count < UnityEngine.Mathf.Max(1, def.minColonists)) return false;
            var tech = Avail.EffectiveTech;
            return tech >= def.minTech && tech <= def.maxTech;
        }

        public override bool Cast(WorldSituation s, Map map)
        {
            if (AfterRecord(map) != null) return base.Cast(s, map);
            var holy = HolyYear();
            if (holy?.ctx?.faction == null) return false;
            var ctx = s.ctx;
            ctx.map = map;
            ctx.faction = holy.ctx.faction;
            s.At(NearestTown(ctx.faction, map), ctx.faction.Name);
            s.Set("holyYear");
            ctx.a = IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            if (!def.names.NullOrEmpty()) ctx.name = Pick(def.names, s);
            return true;
        }

        public override bool BeatCanHappen(WorldSituation s, SituationBeat beat, Map map, out bool retry)
        {
            if (!base.BeatCanHappen(s, beat, map, out retry)) return false;
            if (beat.key == "prisoners" && Pardon.Covered(s.ctx.faction).Count == 0) return false;
            return true;
        }

        public override bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            if (beat.key != "prisoners") return true;
            ctx.count = Pardon.Covered(s.ctx.faction).Count;
            return ctx.count > 0;
        }
    }

    /// <summary>
    /// A town's founding day: its people tell how their ways began (theirs, from their own beginnings), and the colony may
    /// send a gift or, when the town is near (within three tiles: the flag "near", VarietyMath.NearTiles), one of its own to
    /// sit at the day.
    /// </summary>
    public class SituationWorker_FoundingDay : SituationWorker_Variety
    {
        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            if (VarietyMath.Near(Distance(map, NearestTown(s.ctx.faction, map)))) s.Set("near");
            return true;
        }
    }

    /// <summary>
    /// Falling stars: a finder brings a piece of one to the gate, star-iron (steel) to a colony of the old ways, plasteel to
    /// a later one (the flag "lowtechColony" picks the beat). Some at the colony take the stars for a god's sign (World/Home/,
    /// GreatTaken: it is one of the great world events).
    /// </summary>
    public class SituationWorker_FallingStars : SituationWorker_Variety
    {
        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            if (Avail.IsPreIndustrial) s.Set("lowtechColony");
            return true;
        }
    }

    /// <summary>
    /// The great burying: the sextons on their way ask after the colony's unburied dead only when there are some lying out on
    /// its land (the undertakers' rule: the enemy dead a day old or more, Workers_Callers.cs), and carry them off on their
    /// beast (DilemmaEffect_BuryingTake).
    /// </summary>
    public class SituationWorker_GreatBurying : SituationWorker_Variety
    {
        public override bool BeatCanHappen(WorldSituation s, SituationBeat beat, Map map, out bool retry)
        {
            if (!base.BeatCanHappen(s, beat, map, out retry)) return false;
            if (beat.key == "ourDead" && (map == null || HappeningWorker_Undertakers.Dead(map).Count == 0)) return false;
            return true;
        }
    }

    /// <summary>
    /// The sextons at the gate load the colony's unburied dead on their beast and carry them off to the old field, the
    /// undertakers' own play-out (Visitors/Callers.cs, Load): those who came to the gate with the letter, and their beast.
    /// </summary>
    public class DilemmaEffect_BuryingTake : WorldEffect
    {
        private static VisitorTrip Trip(DilemmaContext ctx) => VisitorTracker.Instance?.WaitingFor(ctx);

        private static Pawn Beast(VisitorTrip t) => t?.animals?.FirstOrDefault(a => a != null && !a.Dead && a.Spawned && a.RaceProps.packAnimal);

        public override string Unavailable(DilemmaContext ctx) =>
            Beast(Trip(ctx)) == null ? "CC_Caller_NoBeast".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var map = SituationEffectUtil.MapOf(ctx);
            if (map == null) return;
            int n = HappeningWorker_Undertakers.Dead(map).Count;
            if (n > 0) sheet.Play("CC_Variety_Burying_Impact".Translate(n).Resolve(), "CC_Variety_Burying_ImpactBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = SituationEffectUtil.MapOf(ctx);
            var t = Trip(ctx);
            var beast = Beast(t);
            if (map == null || t == null || beast == null || !Gate(ctx)) return;
            var dead = HappeningWorker_Undertakers.Dead(map);
            var task = Callers.Load(map, t.Standing, dead, beast, null, "Rimfolk_WS_GreatBurying");
            if (task != null)
            {
                task.ctxRef = ctx;
                return;
            }
            if (PlayOuts.On) return;
            // "Answers play out on the map" off: the dead and their gear are gone at once, as answers used to.
            foreach (var body in dead)
            {
                foreach (var w in Callers.GearBy(body)) w.Destroy();
                if (!body.Destroyed) body.Destroy();
            }
        }
    }
}
