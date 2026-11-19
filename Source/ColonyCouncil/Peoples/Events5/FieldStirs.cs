using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 35. The field that stirs (the Quiet Bell) =====================

    /// <summary>
    /// The field that stirs (item 35, with Anomaly): when the colony's unburied dead rise as shamblers (bodies that lay on its
    /// land an hour before), three gravewardens of the Quiet Bell come at a run from the edge nearest them and put them down
    /// beside the colony (the game's assault on the risen, the hunt's lord). Their letter comes once they are already
    /// fighting, and is about what follows: once the risen are down they dig a grave each beyond the wall and lay them in it
    /// (the undertakers' own graves, Visitors/Callers.cs), the bell rung over each, and ask the colony to bury its dead from
    /// now on; the colony may promise it (their thanks the warmer, and the Bell remembers if the dead rise again within the
    /// year), or thank them and see to it itself. Their arms are their age's: maces and spades, firebombs and flamers from the
    /// industrial age (the gravewardens' own kit). The fight is the game's own.
    /// </summary>
    public static class FieldStirs
    {
        public const string Kind = "pev:stirs";
        public const string EventDef = "Rimfolk_Evt_FieldStirs";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>Per map: the people whose bodies lay on the land an hour ago, and those of them seen risen since.</summary>
        private static readonly Dictionary<int, HashSet<int>> lying = new Dictionary<int, HashSet<int>>();

        private static readonly Dictionary<int, HashSet<int>> risen = new Dictionary<int, HashSet<int>>();

        private static readonly Dictionary<int, (int at, int tries)> pending = new Dictionary<int, (int, int)>();

        public static void Forget()
        {
            lying.Clear();
            risen.Clear();
            pending.Clear();
        }

        private static bool Up(Pawn p, Map map) =>
            p != null && p.Spawned && !p.Dead && !p.Downed && p.Map == map && p.IsShambler && p.HostileTo(Faction.OfPlayer) && !p.Position.Fogged(map);

        /// <summary>Every shambler on its feet hunting the colony here, risen from its land or come from elsewhere: whom the gravewardens fight.</summary>
        public static List<Pawn> Shamblers(Map map) =>
            !ModsConfig.AnomalyActive || map == null ? new List<Pawn>() : map.mapPawns.AllPawnsSpawned.Where(p => Up(p, map)).ToList();

        /// <summary>The colony's unburied dead risen: shamblers whose bodies lay on its land (PeopleEvents5Math.Risen).</summary>
        public static List<Pawn> Risen(Map map)
        {
            if (map == null || !ModsConfig.AnomalyActive) return new List<Pawn>();
            lying.TryGetValue(map.uniqueID, out var was);
            risen.TryGetValue(map.uniqueID, out var seen);
            return Shamblers(map).Where(p => PeopleEvents5Math.Risen(was != null && was.Contains(p.thingIDNumber) || seen != null && seen.Contains(p.thingIDNumber), p.IsShambler)).ToList();
        }

        /// <summary>The humanlike dead lying unburied on the land now, by who they were.</summary>
        private static HashSet<int> Lying(Map map)
        {
            var set = new HashSet<int>();
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
                if (t is Corpse c && c.Spawned && c.InnerPawn?.RaceProps?.Humanlike == true && !c.InnerPawn.IsMutant)
                    set.Add(c.InnerPawn.thingIDNumber);
            return set;
        }

        /// <summary>
        /// The homes' hour: who lies dead on the land, and whether any of them have risen; risen, the colony's promise to the
        /// Bell (if any) is broken, and the gravewardens come at a chance, once a rising, within the hour.
        /// </summary>
        public static void Watch(Map map, int now)
        {
            if (!ModsConfig.AnomalyActive) return;
            int key = map.uniqueID;
            var up = Risen(map);
            risen[key] = new HashSet<int>(up.Select(p => p.thingIDNumber));
            var now0 = Lying(map);
            now0.UnionWith(risen[key]);
            lying[key] = now0;
            if (up.Count == 0)
            {
                pending.Remove(key);
                return;
            }
            var b = PeopleEvents5Book.Instance;
            if (b == null) return;
            Broken(b, map, now);
            var f = PeoplesD.Friendly(PeoplesMath.Sextons);
            if (f == null || b.lastStir >= 0 && now - b.lastStir < PeopleEvents5Math.StirGapDays * GenDate.TicksPerDay) return;
            if (!pending.TryGetValue(key, out var p))
            {
                bool comes = Rand.Chance(PeopleEvents5Math.StirChance * StoryDirection.Factor(StoryWaysMath.Visitors));
                p = (comes ? now + Rand.RangeInclusive(0, 1) * GenDate.TicksPerHour : -1, 0);
                pending[key] = p;
            }
            if (p.at < 0 || now < p.at) return;
            if (PeopleEvents2.Fire(EventDef, map, f))
            {
                b.lastStir = now;
                pending[key] = (-1, 0);
            }
            else pending[key] = p.tries >= 4 ? (-1, 0) : (p.at, p.tries + 1);
        }

        /// <summary>The colony's dead rose again within the year of its promise to the Bell: the Bell hears of it, once.</summary>
        private static void Broken(PeopleEvents5Book b, Map map, int now)
        {
            if (b.promiseBroken || !PeopleEvents5Math.PromiseBroken(b.promise, now)) return;
            // The rising that drew the promise is not a new one: only after the gravewardens have gone.
            if (b.stirs.Any(r => r != null && !r.done && r.map == map)) return;
            b.promiseBroken = true;
            var f = b.promiseTo;
            if (f == null || f.defeated || !f.HasGoodwill || f.IsPlayer) return;
            WorldSituationWorker.Goodwill(f, PeopleEvents5Math.PromiseBrokenGoodwill, GoodwillWhy.PromiseBroken);
            Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv5_PromiseBroken".Translate(f.Name.Named("FACTION")).Resolve(), LookTargets.Invalid, MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>Dev mode: the gravewardens come within the hour to the risen here (any shamblers on the land count).</summary>
        public static void Soon(Map map)
        {
            if (map == null) return;
            var all = Shamblers(map);
            if (!risen.TryGetValue(map.uniqueID, out var seen)) risen[map.uniqueID] = seen = new HashSet<int>();
            foreach (var p in all) seen.Add(p.thingIDNumber);
            pending[map.uniqueID] = (Now, 0);
            var b = PeopleEvents5Book.Instance;
            if (b != null) b.lastStir = -1;
        }

        private static Visit VisitAt(Map map) =>
            CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v != null && v.kind == Kind && !v.done && v.map == map);

        private static StirRecord RecordAt(Map map) => PeopleEvents5Book.Instance?.stirs.LastOrDefault(r => r != null && !r.done && r.map == map);

        private static List<Pawn> Wardens(Visit v) => v.group.Prepend(v.x).Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Map == v.map).ToList();

        /// <summary>Their run in: at the edge nearest the risen, straight into the game's assault on every shambler up; the visit and the record kept for what follows.</summary>
        public static bool Run(HappeningContext ctx)
        {
            var map = ctx?.map;
            var targets = Shamblers(map);
            var party = PeoplesG.Party(ctx);
            if (map == null || targets.Count == 0 || party.Count == 0 || ctx.faction == null) return false;
            var edge = LegendBeasts.EdgeToward(map, targets[0].Position);
            if (!edge.IsValid) return false;
            foreach (var p in party)
                if (!Arrivals.Spawn(p, map, edge)) return false;
            try
            {
                LordMaker.MakeNewLord(ctx.faction, new LordJob_AssaultThings(ctx.faction, targets.Cast<Thing>().ToList(), 1f, false), map, party);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 5: the gravewardens' run", e);
                return false;
            }
            var t = CouncilGameComponent.Instance?.happenings;
            var v = t?.AddVisit(Kind, ctx.incident, map, party[0], null, party.Skip(1).ToList(), Now + GenDate.TicksPerDay);
            if (v == null) return false;
            // The shamblers they fight, kept so their bodies are known once they are down.
            v.touched.AddRange(targets.Take(PeopleEvents5Math.StirGraves * 2));
            PeopleEvents5Book.Instance?.AddStir(new StirRecord { map = map, faction = ctx.faction, since = Now });
            ctx.spot = edge;
            return true;
        }

        /// <summary>The colony's answer: "bury" (the fight and the burial), "promise" (and the colony's promise), "away" (they go now).</summary>
        public static void Terms(HappeningContext ctx, string how)
        {
            var r = RecordAt(ctx?.map);
            var v = VisitAt(ctx?.map);
            if (r == null) return;
            r.how = how;
            var b = PeopleEvents5Book.Instance;
            if (how == "promise" && b != null)
            {
                b.promise = Now;
                b.promiseTo = ctx.faction;
                b.promiseBroken = false;
            }
            if (how != "away" || v == null) return;
            v.done = true;
            r.done = true;
            PeoplesG.Home(Wardens(v));
        }

        /// <summary>
        /// The bodies of the shamblers they fought, put down again (and any shambler's lying here): what they bury, humanlike
        /// and unburied, the nearest the colony first.
        /// </summary>
        private static List<Thing> Dead(Visit v)
        {
            var map = v.map;
            var mid = CampSite.ColonyCenter(map);
            return v.touched.Select(p => p?.Corpse).Cast<Thing>()
                .Concat(map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).Where(t => t is Corpse c && c.InnerPawn?.IsShambler == true))
                .Where(t => t is Corpse c && c.Spawned && c.Map == map && c.InnerPawn?.RaceProps?.Humanlike == true && !c.Position.Fogged(map)).Distinct()
                .OrderBy(c => c.Position.DistanceToSquared(mid)).Take(PeopleEvents5Math.StirGraves).ToList();
        }

        /// <summary>
        /// The visit's hour: while shamblers are up, the game's fight; once they are down, the graves dug and the dead laid in
        /// them (the undertakers' own play-out), the bell rung over each as it is filled; then their word and they go.
        /// </summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var r = RecordAt(v.map);
            var wardens = Wardens(v);
            if (r == null || wardens.Count == 0 || now > v.until)
            {
                Finish(v, r, wardens);
                return false;
            }
            var up = Shamblers(v.map);
            if (up.Count > 0)
            {
                foreach (var p in up)
                    if (!v.touched.Contains(p) && v.touched.Count < PeopleEvents5Math.StirGraves * 3) v.touched.Add(p);
                if (!(wardens[0].GetLord()?.LordJob is LordJob_AssaultThings))
                    try
                    {
                        Lords.Leave(wardens);
                        LordMaker.MakeNewLord(wardens[0].Faction, new LordJob_AssaultThings(wardens[0].Faction, up.Cast<Thing>().ToList(), 1f, false), v.map, wardens);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("peoples' events 5: the gravewardens' fight", e);
                    }
                return true;
            }
            if (r.how == "away")
            {
                Finish(v, r, wardens);
                return false;
            }
            if (!r.burying)
            {
                r.burying = true;
                r.dead = Dead(v);
                if (r.dead.Count == 0)
                {
                    Finish(v, r, wardens);
                    return false;
                }
                r.plots = PlayBurial.Plot(v.map, wardens[0], r.dead.Count, true, out var rot);
                r.rot = rot.AsInt;
                if (r.plots.Count == 0)
                {
                    PeoplesG.SayLine(wardens[0], v.incident, "noroom");
                    Finish(v, r, wardens);
                    return false;
                }
                PeoplesD.Gather(wardens, wardens[0].Faction, r.plots[0], 18f);
                PeoplesG.SayLine(wardens[0], v.incident, "dig");
                if (Callers.Graves(v.map, wardens, r.dead, r.plots, rot, null, EventDef) == null)
                {
                    // "Answers play out on the map" off: the graves and the burying are had at once, as the undertakers' are.
                    for (int i = 0; i < r.plots.Count && i < r.dead.Count; i++)
                    {
                        var grave = Callers.DigNow(v.map, r.plots[i], rot);
                        var body = r.dead[i];
                        if (grave == null || body == null || !body.Spawned) continue;
                        body.DeSpawn();
                        if (!grave.TryAcceptThing(body, false) && !body.Destroyed) GenPlace.TryPlaceThing(body, grave.Position, v.map, ThingPlaceMode.Near);
                    }
                }
                return true;
            }
            // Each grave as it is filled: the bell rung over it.
            int filled = r.plots.Count(c => PlayBurial.GraveAt(v.map, c)?.HasCorpse == true);
            if (filled > r.rung)
            {
                var at = r.plots.Where(c => PlayBurial.GraveAt(v.map, c)?.HasCorpse == true).Skip(r.rung).FirstOrDefault();
                r.rung = filled;
                PeopleEvents5.Bell(v.map, at);
                var near = wardens.OrderBy(p => p.Position.DistanceToSquared(at)).First();
                PeoplesG.SayLine(near, v.incident, "ring");
            }
            r.dead.RemoveAll(c => c == null || c.Destroyed || c.ParentHolder is Building_Grave);
            if (r.dead.Count == 0 || r.dead.All(c => !c.Spawned && !(c.ParentHolder is Pawn_CarryTracker)))
            {
                Finish(v, r, wardens);
                return false;
            }
            return true;
        }

        private static void Finish(Visit v, StirRecord r, List<Pawn> wardens)
        {
            v.done = true;
            var f = PeoplesF.PeopleOf(v);
            if (r != null)
            {
                r.done = true;
                r.dead.Clear();
                if (f != null && f.HasGoodwill && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer) && r.how != "away")
                {
                    WorldSituationWorker.Goodwill(f, PeopleEvents5Math.StirGoodwill + (r.how == "promise" ? PeopleEvents5Math.PromiseGoodwill : 0), GoodwillWhy.Helped);
                    // together: the gravewardens' word at the graves they have just filled, to the colony that watched.
                    if (wardens.Count > 0) PeoplesG.SayLine(wardens[0], v.incident, r.how == "promise" ? "kept" : "ask");
                    Tell.Message(NewsKind.Land, Importance.Notable, v.map,
                        (r.rung == 0 ? "CC_PEv5_StirDoneFought" : r.how == "promise" ? "CC_PEv5_StirDonePromise" : "CC_PEv5_StirDone").Translate(f.Name.Named("FACTION"), r.rung.Named("COUNT")).Resolve(),
                        wardens.Count > 0 ? new LookTargets(wardens) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
                }
            }
            PeoplesG.Home(wardens);
        }
    }

    /// <summary>
    /// Gravewardens of {FACTION} ({X} and {COUNT} in all, the risen {DEAD}) came at a run when the colony's unburied dead rose
    /// as shamblers, and are already fighting them; their letter asks what follows.
    /// </summary>
    public class HappeningWorker_FieldStirs : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            ModsConfig.AnomalyActive && PeoplesD.Friendly(PeoplesMath.Sextons) != null && FieldStirs.Risen(map).Count > 0 && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Sextons);
            var up = FieldStirs.Risen(ctx.map);
            if (ctx.faction == null || up.Count == 0) return false;
            for (int i = 0; i < PeopleEvents5Math.StirWardens; i++)
            {
                var p = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_QuietBell_Gravewarden"), ctx.faction, ext.ages);
                if (p == null) continue;
                if (ctx.stranger == null) ctx.stranger = p;
                else ctx.group.Add(p);
            }
            if (ctx.stranger == null) return false;
            ctx.count = PeoplesG.Party(ctx).Count;
            ctx.variant = up.Count;
            ctx.dead = up.Count.ToString();
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Melee) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        /// <summary>Their run in from the edge nearest the risen, already fighting as the letter comes.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) => FieldStirs.Run(ctx);

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.variant >= 2 ? "many" : "one");
            var b = PeopleEvents5Book.Instance;
            if (b != null && b.promise >= 0 && b.promiseBroken) tags.Add("again");
        }

        protected override LookTargets Look(HappeningContext ctx) => PeoplesG.Party(ctx).Any(p => p.Spawned) ? new LookTargets(PeoplesG.Party(ctx).Where(p => p.Spawned)) : base.Look(ctx);

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "bury" : option == 1 ? "promise" : "away");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>What follows the fight (FieldStirs.Terms): <see cref="how"/> "bury", "promise", or "away".</summary>
    public class DilemmaEffect_FieldStirs : DilemmaEffect
    {
        public string how = "bury";

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var f = ctx?.faction;
            switch (how)
            {
                case "bury":
                case "promise":
                    sheet.Play("CC_PEv5_Impact_StirBury".Translate().Resolve(), "CC_PEv5_Impact_StirBuryBrief".Translate().Resolve());
                    if (how == "promise")
                        sheet.Add(ImpactKind.Other, 0, "CC_PEv5_Impact_Promise".Translate(f?.Name ?? "?").Resolve(), "CC_PEv5_Impact_PromiseBrief".Translate().Resolve(), "pev5promise");
                    if (f != null) sheet.Goodwill(f, PeopleEvents5Math.StirGoodwill + (how == "promise" ? PeopleEvents5Math.PromiseGoodwill : 0));
                    break;
                default:
                    sheet.Add(ImpactKind.Other, 0, "CC_PEv5_Impact_StirAway".Translate(f?.Name ?? "?").Resolve(), "CC_PEv5_Impact_StirAwayBrief".Translate().Resolve(), "pev5stiraway");
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx) => FieldStirs.Terms(ctx as HappeningContext, how);
    }
}
