using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The content round's third part, its world events (docs/content-round-3-camps-world.md, R3-W1 to R3-W6; Defs/
    // WorldSituationDefs/Rimfolk_WS_Round3.xml): a mine falls in, the fouled wells, an expedition, a ransom, the year of the
    // rats and a new way spreads. Each in its people's age's form (SituationWorker_Variety's tags), their state in the world
    // event's own flags; the pure rules are World/Variety/Round3WorldMath.cs. Their answers use the frame's own effects:
    // the visitors at the gate and on the road, the far errand, food, wood, medicine and silver carried out or handed over,
    // a lesson at the gate, lanterns.

    // ===================== R3-W1. A mine falls in =====================

    /// <summary>
    /// A mine falls in (R3-W1): the miners of a people at peace are trapped behind a fall at one of their towns. A runner comes
    /// to the gate asking for timber for the props (carried out to them), medicine for those brought out (handed over), or the
    /// colony's best miner for two days' digging (the far errand: a caravan of one there and home, with what they learned).
    /// The ending says whether the miners were brought out, likelier with the colony's help. All in the def; the class gives it
    /// its people's age.
    /// </summary>
    public class SituationWorker_R3MineFall : SituationWorker_Variety
    {
    }

    // ===================== R3-W2. The fouled wells =====================

    /// <summary>
    /// The fouled wells (R3-W2): a people's wells at a town go bad. Only when the colony's land has fresh water near the colony
    /// (a river, a lake or a marsh, not the sea), since their water-carriers come to it: let them (for nothing, or for a fee
    /// they hand over at the gate), and every few days a party of them stands at the colony's water an hour or two before
    /// going home (the visitors' Water spot).
    /// </summary>
    public class SituationWorker_R3Wells : SituationWorker_Variety
    {
        /// <summary>Fresh water within reach of the colony: a river, or water that isn't the sea.</summary>
        public static bool FreshWater(Map map)
        {
            if (map == null) return false;
            try
            {
                var center = CampSite.ColonyCenter(map);
                if (!center.IsValid) center = map.Center;
                foreach (var c in GenRadial.RadialCellsAround(center, 50f, true))
                {
                    if (!c.InBounds(map)) continue;
                    var t = c.GetTerrain(map);
                    if (t != null && t.IsWater && (t.IsRiver || !t.defName.Contains("Ocean"))) return true;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3 world events: fresh water", e);
            }
            return false;
        }

        protected override bool CanHappen(Map map) => FreshWater(map);
    }

    // ===================== R3-W3. An expedition =====================

    /// <summary>
    /// An expedition (R3-W3): a people sends an expedition from its town into the far country. Whether it comes back is settled
    /// at the start, out of sight (Round3WorldMath.Returns), and shown by what follows: it comes back (and if the colony helped
    /// it on its way out, their courier brings a share of what it carried back), or one of them comes back alone past the
    /// colony and asks at the gate.
    /// </summary>
    public class SituationWorker_R3Expedition : SituationWorker_Variety
    {
        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            s.Set(Round3WorldMath.Returns(Rand.Value) ? "willReturn" : "willBeLost");
            return true;
        }
    }

    // ===================== R3-W4. A ransom =====================

    /// <summary>
    /// A ransom (R3-W4): raiders (a people hostile to the colony where there is one: {ENEMY}, [enemy]) took people of a friendly
    /// people's town on the road, and that people collects the ransom across the region. Their collector asks the colony's share
    /// at the gate ({COUNT}, Round3WorldMath.RansomShare). About a week in it is settled whether they are ransomed, likelier
    /// if the colony gave; ransomed, the freed walk home past the colony.
    /// </summary>
    public class SituationWorker_R3Ransom : SituationWorker_Variety
    {
        protected override void AddTags(WorldSituation s, HashSet<string> tags)
        {
            base.AddTags(s, tags);
            if (s?.ctx?.enemy != null) tags.Add("enemy");
        }

        public override bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            if (beat.key == "collector") ctx.count = Round3WorldMath.RansomShare(CampAnswers.Silver(SituationEffectUtil.MapOf(ctx)));
            return true;
        }

        public override string Daily(WorldSituation s)
        {
            if (s.Has("settled") || s.DaysIn < Round3WorldMath.SettledDay) return null;
            s.Set("settled");
            if (Rand.Chance(Round3WorldMath.FreedChance(s.Has("gave")))) s.Set("freed");
            return null;
        }
    }

    // ===================== R3-W5. The year of the rats =====================

    /// <summary>
    /// The year of the rats (R3-W5): rats in a people's granaries. A handful of the game's own wild rats come onto the colony's
    /// land from its edge (never manhunting), only where rats can live in this season. A rat-catcher comes to the gate with a
    /// dog: hired (their fee handed over, and they stay the night), the dog goes after the rats on the colony's land, one after
    /// another, the game's own attack; when none are left, the land is clear.
    /// </summary>
    public class SituationWorker_R3Rats : SituationWorker_Variety
    {
        public static PawnKindDef RatKind(Map map)
        {
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Rat");
            return kind?.race != null && map != null && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(kind.race) ? kind : null;
        }

        /// <summary>The rats on the colony's land: the game's own wild rats, up and about.</summary>
        public static List<Pawn> Rats(Map map)
        {
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Rat");
            return map == null || kind == null
                ? new List<Pawn>()
                : map.mapPawns.AllPawnsSpawned.Where(p => p.kindDef == kind && p.Faction == null && !p.Dead && !p.Downed).ToList();
        }

        protected override bool CanHappen(Map map) => RatKind(map) != null;

        public override bool BeatCanHappen(WorldSituation s, SituationBeat beat, Map map, out bool retry)
        {
            if (!base.BeatCanHappen(s, beat, map, out retry)) return false;
            return beat.key != "rats" || RatKind(map) != null;
        }

        public override void BeatHappened(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            if (beat.key != "rats") return;
            var map = SituationEffectUtil.MapOf(ctx);
            var kind = RatKind(map);
            if (kind == null || !RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Animal)) return;
            int n = Round3WorldMath.Rats(Rand.Value);
            try
            {
                for (int i = 0; i < n; i++)
                {
                    var rat = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, map.Tile));
                    GenSpawn.Spawn(rat, CellFinder.RandomClosewalkCellNear(entry, map, 6), map);
                }
                s.Set("rats");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3 world events: the rats", e);
            }
        }

        public override string Hourly(WorldSituation s)
        {
            string end = base.Hourly(s);
            if (end != null) return end;
            if (!s.Has("hired") || s.Has("clear")) return null;
            try
            {
                Catch(s);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3 world events: the rat-catcher's dog", e);
            }
            return null;
        }

        /// <summary>The catcher's dog after the nearest rat on the colony's land; none left, and the land is clear.</summary>
        private static void Catch(WorldSituation s)
        {
            var trip = VisitorTracker.Instance?.trips.FirstOrDefault(t => t.situationId == s.id && t.state == VisitState.Here);
            var map = trip?.map;
            var dog = trip?.animals?.FirstOrDefault(a => a != null && !a.Dead && a.Spawned && !a.Downed && a.Map == map);
            if (dog == null) return;
            var rats = Rats(map);
            if (rats.Count == 0)
            {
                if (!s.Has("rats")) return;
                s.Set("clear");
                Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_R3Rats_Clear".Translate().Resolve(), new LookTargets(dog), MessageTypeDefOf.PositiveEvent);
                return;
            }
            if (dog.CurJobDef == JobDefOf.AttackMelee) return;
            var rat = rats.Where(r => r.Position.InHorDistOf(dog.Position, Round3WorldMath.RatRange) && dog.CanReach(r, PathEndMode.Touch, Danger.Some))
                .OrderBy(r => r.Position.DistanceToSquared(dog.Position)).FirstOrDefault();
            if (rat == null) return;
            var job = JobMaker.MakeJob(JobDefOf.AttackMelee, rat);
            job.killIncappedTarget = true;
            job.expiryInterval = GenDate.TicksPerHour;
            dog.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (s.Has("chasing")) return;
            s.Set("chasing");
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_R3Rats_Chasing".Translate().Resolve(), new LookTargets(dog), MessageTypeDefOf.NeutralEvent);
        }
    }

    // ===================== R3-W6. A new way spreads =====================

    /// <summary>
    /// A new way spreads (R3-W6): a people takes up a new way of working, in the form of its age: a binding for blades
    /// (tribal), the heavy plough (medieval), a threshing machine (industrial), a fabrication pattern (spacer), so a craft
    /// (Round3WorldMath.SkillFor) and the beat of that craft (its flag). The one who brought it comes to the gate: the colony's
    /// best at that craft learns it there (a lesson at the gate), or the colony buys the drawings (the game's schematic,
    /// handed over by them).
    /// </summary>
    public class SituationWorker_R3NewWay : SituationWorker_Variety
    {
        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            string age = s.ctx.faction != null ? Ages.Tag(Ages.Of(s.ctx.faction)) : "medieval";
            s.Set(Round3WorldMath.SkillFlag(Round3WorldMath.SkillFor(age)));
            return true;
        }
    }

    /// <summary>The drawings of a new way bought (R3-W6): the game's schematic, handed over by the one at the gate.</summary>
    public class WorldEffect_R3Drawings : WorldEffect
    {
        public static ThingDef Drawings => DefDatabase<ThingDef>.GetNamedSilentFail("Schematic");

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (Drawings != null && Allowed(ctx)) sheet.Goods(1, Drawings, 1);
        }

        public override string Unavailable(DilemmaContext ctx) => Drawings == null ? "CC_R3Way_NoDrawings".Translate().Resolve() : null;

        public override void Apply(DilemmaContext ctx)
        {
            var map = SituationEffectUtil.MapOf(ctx);
            var def = Drawings;
            if (map == null || def == null || !Gate(ctx)) return;
            try
            {
                Couriers.Hand(def, 1, map, DropCellFinder.TradeDropSpot(map), Couriers.PeopleOf(ctx), ctx.stranger);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 3 world events: the drawings", e);
            }
        }
    }

    // ===================== Dev mode =====================

    /// <summary>
    /// Dev mode for the round's camps and world events (Debug actions, "Rimfolk"): each new world event started now, each new
    /// camp brought here now (beside any already here), and the camps' turns hurried: the rain come for the rain-callers, the
    /// questioning, the dyeing and the dice settled for whoever is at their fire.
    /// </summary>
    public static class Round3Debug
    {
        private static Map Map => Find.CurrentMap ?? HappeningTracker.HomeMap();

        private static void Say(string text) => Messages.Message("Round 3: " + text, MessageTypeDefOf.SilentInput, false);

        /// <summary>The world events of this round.</summary>
        public static readonly string[] WorldEvents =
        {
            "Rimfolk_WS_MineFall", "Rimfolk_WS_FouledWells", "Rimfolk_WS_Expedition", "Rimfolk_WS_Ransom", "Rimfolk_WS_Rats", "Rimfolk_WS_NewWay"
        };

        [DebugAction("Rimfolk", "Round 3: start one of the new world events now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Start()
        {
            var inst = WorldSituations.Instance;
            var map = Map;
            if (inst == null || map == null) return;
            var options = new List<FloatMenuOption>();
            foreach (string n in WorldEvents)
            {
                var def = DefDatabase<WorldSituationDef>.GetNamedSilentFail(n);
                if (def == null) continue;
                bool can;
                try
                {
                    can = def.Worker.CanStart(map);
                }
                catch
                {
                    can = false;
                }
                options.Add(new FloatMenuOption(def.LabelCap + (can ? "" : " (can't now: its people, the land or its season)"), () =>
                {
                    var s = inst.TryStart(map, x => x == def, true);
                    Say(s != null ? def.label + " started." : "couldn't start " + def.label + " now (nobody fits, or one of its kind is running).");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Round 3: one of the new camps here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Camp()
        {
            var map = Map;
            var options = new List<FloatMenuOption>();
            foreach (string n in CampRound3.Kinds)
            {
                var kind = DefDatabase<CampKindDef>.GetNamedSilentFail(n);
                if (kind == null) continue;
                options.Add(new FloatMenuOption(kind.label + (kind.FitsMap(map) ? "" : " (the land or the world lacks what they come for)"), () =>
                {
                    var tracker = CampTracker.Instance;
                    if (tracker == null || !CampTracker.Enabled)
                    {
                        Say("camps are off.");
                        return;
                    }
                    var f = Arrivals.Friendly(x => kind.FitsFaction(x));
                    var why = new List<string>();
                    var c = f == null ? null : tracker.StartKind(map, kind, f, why);
                    Say(c != null ? kind.label + " of " + f.Name + " on the way." : "no camp: " + (f == null ? "no people at peace that fits" : string.Join("; ", why)));
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Round 3: the camps' turns now (the rain, the questioning, the dyeing, the dice)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Turns()
        {
            var map = Map;
            int n = 0;
            foreach (var c in CampTracker.VisitorsAt(map))
            {
                if (c.kind?.Worker is CampWorker_R3RainCallers && CampWorker_R3RainCallers.StateOf(c) is RainState r && !r.rained)
                {
                    r.rite = true;
                    CampWorker_R3RainCallers.Rain(c, r);
                    n++;
                }
                if (c.kind?.Worker is CampWorker_R3WitchFinders && CampWorker_R3WitchFinders.StateOf(c) is WitchState w && w.questioning && w.withFrom >= 0)
                {
                    w.withFrom = CampRound3.Now - 10 * GenDate.TicksPerHour;
                    n++;
                }
                if (c.kind?.Worker is CampWorker_R3Dyers && CampWorker_R3Dyers.StateOf(c) is DyeState d && d.sent != null && d.withFrom >= 0)
                {
                    d.withFrom = CampRound3.Now - 10 * GenDate.TicksPerHour;
                    n++;
                }
                if (c.kind?.Worker is CampWorker_R3Gamblers && CampWorker_R3Gamblers.StateOf(c) is DiceState g && g.player != null && g.withFrom >= 0)
                {
                    g.withFrom = CampRound3.Now - 10 * GenDate.TicksPerHour;
                    n++;
                }
            }
            Say(n > 0 ? n + " camp turn(s) hurried (the sittings settle at the next hour)." : "no rain-callers, or nobody sitting at a round-3 camp's fire.");
        }
    }
}
