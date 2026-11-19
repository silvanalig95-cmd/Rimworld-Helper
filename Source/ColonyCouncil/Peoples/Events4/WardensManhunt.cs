using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 32. Wardens at the manhunt (the Lantern Wardens) =====================

    /// <summary>
    /// Wardens at the manhunt (item 32): when a manhunting pack or a mad beast besets the colony, two to four Wardens come at
    /// a run from the edge nearest the beasts and fight beside the colony under the hunt's lord (the game's assault on the
    /// beasts). Their letter is about the terms: as on the hunt, the hides of what they felled go with them and the meat
    /// stays (the first answer, and what happens if none is given, since the help is already on the map); or they keep all
    /// they fell (their thanks the warmer); or the colony thanks them and they go. The fight is the game's own; the help is
    /// free; the Wardens give +3. Their arms are their age's (their kinds' own, guns from the industrial age).
    /// </summary>
    public static class WardensManhunt
    {
        public const string Kind = "pev:manhunt";
        public const string EventDef = "Rimfolk_Evt_WardensManhunt";

        public static JobDef DressJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv4_Dress");

        private static int Now => Find.TickManager.TicksGame;

        private static readonly Dictionary<int, (int at, int tries)> pending = new Dictionary<int, (int, int)>();

        public static void Forget() => pending.Clear();

        private static bool Mad(Pawn p) => p?.MentalStateDef != null && (p.MentalStateDef == MentalStateDefOf.Manhunter || p.MentalStateDef == MentalStateDefOf.ManhunterPermanent);

        /// <summary>The wild beasts hunting the colony's people on this map now (seen, up).</summary>
        public static List<Pawn> Manhunters(Map map) =>
            map?.mapPawns.AllPawnsSpawned.Where(p => p != null && !p.Dead && !p.Downed && p.RaceProps.Animal && Mad(p) && (p.Faction == null || p.Faction.HostileTo(Faction.OfPlayer))
                                                  && !p.Position.Fogged(map)).ToList() ?? new List<Pawn>();

        /// <summary>A pack, or a mad beast of a size.</summary>
        public static bool Beset(Map map)
        {
            var list = Manhunters(map);
            return list.Count > 0 && PeopleEvents4Math.Beset(list.Count, list.Max(p => p.BodySize));
        }

        /// <summary>The homes' hour: beset, the Wardens at peace and not lately here: once a manhunt, at a chance, an hour or two after it began.</summary>
        public static void Watch(Map map, int now)
        {
            int key = map.uniqueID;
            if (!Beset(map))
            {
                pending.Remove(key);
                return;
            }
            var b = PeopleEvents4Book.Instance;
            var f = PeoplesD.Friendly(PeoplesMath.Hunters);
            if (b == null || f == null) return;
            if (b.lastManhunt >= 0 && now - b.lastManhunt < PeopleEvents4Math.ManhuntGapDays * GenDate.TicksPerDay) return;
            if (!pending.TryGetValue(key, out var p))
            {
                bool comes = Rand.Chance(PeopleEvents4Math.ManhuntChance * StoryDirection.Factor(StoryWaysMath.Visitors));
                p = (comes ? now + Rand.RangeInclusive(PeopleEvents4Math.ManhuntMinHours, PeopleEvents4Math.ManhuntMaxHours) * GenDate.TicksPerHour : -1, 0);
                pending[key] = p;
            }
            if (p.at < 0 || now < p.at) return;
            if (PeopleEvents2.Fire(EventDef, map, f))
            {
                b.lastManhunt = now;
                pending[key] = (-1, 0);
            }
            else pending[key] = p.tries >= 6 ? (-1, 0) : (p.at, p.tries + 1);
        }

        /// <summary>Dev mode: the Wardens come within the hour to the beasts here.</summary>
        public static void Soon(Map map)
        {
            if (map == null) return;
            pending[map.uniqueID] = (Now, 0);
            var b = PeopleEvents4Book.Instance;
            if (b != null) b.lastManhunt = -1;
        }

        private static Visit VisitAt(Map map) =>
            CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v != null && v.kind == Kind && !v.done && v.map == map);

        private static List<Pawn> Wardens(Visit v) => v.group.Prepend(v.x).Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Map == v.map).ToList();

        /// <summary>A beast died (HappeningTracker.Notify_Killed): felled by a Warden of the manhunt, it is theirs to dress.</summary>
        public static void Killed(Pawn victim, DamageInfo? dinfo)
        {
            var v = VisitAt(victim?.MapHeld);
            if (v == null || !(dinfo?.Instigator is Pawn by) || (by != v.x && !v.group.Contains(by)) || v.touched.Contains(victim)) return;
            v.touched.Add(victim);
            // The last of them down: the Wardens gather at the carcasses before their lord walks them off.
            var wardens = Wardens(v);
            var body = victim.Corpse;
            if (wardens.Count > 0 && body != null && body.Spawned && Manhunters(v.map).Count == 0 && How(v) != "away") PeoplesD.Gather(wardens, wardens[0].Faction, body.Position, 6f);
        }

        /// <summary>
        /// Their run in: at the edge nearest the beasts, straight into the game's assault on them (the hunt's lord), the
        /// visit kept for what they fell.
        /// </summary>
        public static bool Run(HappeningContext ctx)
        {
            var map = ctx?.map;
            var beasts = Manhunters(map);
            var party = PeoplesG.Party(ctx);
            if (map == null || beasts.Count == 0 || party.Count == 0 || ctx.faction == null) return false;
            var mid = beasts[0].Position;
            var edge = LegendBeasts.EdgeToward(map, mid);
            if (!edge.IsValid) return false;
            foreach (var p in party)
                if (!Arrivals.Spawn(p, map, edge)) return false;
            try
            {
                LordMaker.MakeNewLord(ctx.faction, new LordJob_AssaultThings(ctx.faction, beasts.Cast<Thing>().ToList(), 1f, false), map, party);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: the Wardens' run", e);
                return false;
            }
            var t = CouncilGameComponent.Instance?.happenings;
            var v = t?.AddVisit(Kind, ctx.incident, map, party[0], null, party.Skip(1).ToList(), Now + GenDate.TicksPerDay);
            if (v != null) v.text = ctx.faction.GetUniqueLoadID() + "|split";
            ctx.spot = edge;
            return true;
        }

        /// <summary>The colony's terms: <paramref name="how"/> "split" (hides theirs, meat the colony's), "keep" (all they fell theirs), "away" (they go now).</summary>
        public static void Terms(HappeningContext ctx, string how)
        {
            var v = VisitAt(ctx?.map);
            if (v == null) return;
            v.text = (ctx.faction?.GetUniqueLoadID() ?? "") + "|" + how;
            if (how != "away") return;
            v.done = true;
            PeoplesG.Home(Wardens(v));
        }

        private static string How(Visit v) => (v.text ?? "").Split('|').ElementAtOrDefault(1) ?? "split";

        private static bool Dressing(Pawn p) => p?.CurJobDef != null && p.CurJobDef == DressJob;

        /// <summary>
        /// The manhunt's hour: while the beasts are up, the game's fight; once they are down, the Wardens dress what they felled
        /// one by one (gathered at the carcasses), then their thanks, and they go.
        /// </summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var wardens = Wardens(v);
            if (wardens.Count == 0 || now > v.until)
            {
                Finish(v, wardens);
                return false;
            }
            var beasts = Manhunters(v.map);
            if (beasts.Count > 0)
            {
                // Beasts still up and the Wardens not at them (their first targets down): the game's assault again, on these.
                if (!(wardens[0].GetLord()?.LordJob is LordJob_AssaultThings))
                    try
                    {
                        Lords.Leave(wardens);
                        LordMaker.MakeNewLord(wardens[0].Faction, new LordJob_AssaultThings(wardens[0].Faction, beasts.Cast<Thing>().ToList(), 1f, false), v.map, wardens);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("peoples' events 4: the Wardens' fight", e);
                    }
                return true;
            }
            var bodies = v.touched.Where(p => p?.Corpse != null && p.Corpse.Spawned && p.Corpse.Map == v.map).Select(p => p.Corpse).ToList();
            if (bodies.Count == 0 && !wardens.Any(Dressing))
            {
                Finish(v, wardens);
                return false;
            }
            // Their lord now a stay among the carcasses, so the work isn't called off.
            if (!(wardens[0].GetLord()?.LordJob is LordJob_VisitOnErrand) && bodies.Count > 0) PeoplesD.Gather(wardens, wardens[0].Faction, bodies[0].Position, 6f);
            var taken = new HashSet<Thing>(wardens.Where(Dressing).Select(p => p.CurJob.targetA.Thing));
            foreach (var w in wardens.Where(p => !Dressing(p)))
            {
                var body = bodies.Where(x => !taken.Contains(x)).OrderBy(x => x.Position.DistanceToSquared(w.Position)).FirstOrDefault();
                if (body == null) break;
                if (PeopleEvents4.Send(w, DressJob, body, PeoplesDMath.SkinTicks)) taken.Add(body);
            }
            return true;
        }

        /// <summary>A carcass dressed (the job done): as the hunt splits it, the hide with the Wardens and the meat for the colony, or all of it theirs.</summary>
        public static void Dressed(Pawn warden, Thing target)
        {
            if (!(target is Corpse body) || !body.Spawned || warden == null) return;
            var v = CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(x => x != null && x.kind == Kind && !x.done && (x.x == warden || x.group.Contains(warden)));
            bool keep = v != null && How(v) == "keep";
            string beast = body.InnerPawn?.LabelShort ?? body.LabelShort;
            var map = body.Map;
            var at = body.Position;
            if (v != null) v.touched.Remove(body.InnerPawn);
            string kept = PeoplesD.Divide(warden, body, t => keep || t.def.IsLeather ? 0 : t.stackCount);
            if (keep) return;
            Tell.Message(NewsKind.Land, Importance.Flavor, map,
                "CC_PeoplesD_Skinned".Translate(warden.Named("PAWN"), beast.Named("BEAST"), (kept.NullOrEmpty() ? "CC_PeoplesD_Nothing".Translate().Resolve() : kept).Named("KEPT")).Resolve(),
                new LookTargets(at, map), MessageTypeDefOf.PositiveEvent);
        }

        private static void Finish(Visit v, List<Pawn> wardens)
        {
            v.done = true;
            var f = PeoplesF.PeopleOf(v);
            if (f != null && f.HasGoodwill && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer) && How(v) != "away")
            {
                WorldSituationWorker.Goodwill(f, PeopleEvents4Math.ManhuntGoodwill + (How(v) == "keep" ? 2 : 0), GoodwillWhy.Helped);
                FactionMemory.Note(f, "campHelped", "CC_PEv4_ManhuntName".Translate().Resolve());
                if (wardens.Count > 0) PeoplesG.SayLine(wardens[0], v.incident, "done");
                Tell.Message(NewsKind.Land, Importance.Notable, v.map, "CC_PEv4_ManhuntDone".Translate(f.Name.Named("FACTION")).Resolve(), wardens.Count > 0 ? new LookTargets(wardens) : LookTargets.Invalid,
                    MessageTypeDefOf.PositiveEvent);
            }
            PeoplesG.Home(wardens);
        }
    }

    /// <summary>
    /// Wardens of {FACTION} ({X} and {COUNT} in all) came at a run to the manhunt ({TALE}: the beasts on the colony now), and
    /// are already fighting from the edge; their letter asks the terms.
    /// </summary>
    public class HappeningWorker_WardensManhunt : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Hunters) != null && WardensManhunt.Beset(map) && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Hunters);
            var beasts = WardensManhunt.Manhunters(ctx.map);
            if (ctx.faction == null || beasts.Count == 0) return false;
            int n = PeopleEvents4Math.ManhuntWardens(beasts.Count);
            for (int i = 0; i < n; i++)
            {
                var p = Arrivals.Make(PeoplesD.FighterKind(ctx.faction), ctx.faction, ext.ages);
                if (p == null) continue;
                if (ctx.stranger == null) ctx.stranger = p;
                else ctx.group.Add(p);
            }
            if (ctx.stranger == null) return false;
            ctx.count = n;
            ctx.variant = beasts.Count;
            ctx.tale = beasts.Count == 1 ? beasts[0].LabelShort : VarietyUtil.Bundle(beasts.GroupBy(b => b.def).OrderByDescending(g => g.Count()).First().Key, beasts.Count);
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Shooting) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        /// <summary>Their run in from the edge nearest the beasts, already fighting as the letter comes.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) => WardensManhunt.Run(ctx);

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.variant >= 3 ? "pack" : "mad");
        }

        protected override LookTargets Look(HappeningContext ctx) => PeoplesG.Party(ctx).Any(p => p.Spawned) ? new LookTargets(PeoplesG.Party(ctx).Where(p => p.Spawned)) : base.Look(ctx);

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "split" : option == 1 ? "keep" : "away");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The terms of their help (WardensManhunt.Terms): <see cref="how"/> "split", "keep", or "away".</summary>
    public class DilemmaEffect_WardensManhunt : DilemmaEffect
    {
        public string how = "split";

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var f = ctx?.faction;
            switch (how)
            {
                case "split":
                    sheet.Add(ImpactKind.Goods, 1, "CC_PEv4_Impact_Split".Translate().Resolve(), "CC_PEv4_Impact_SplitBrief".Translate().Resolve(), "pev4split");
                    if (f != null) sheet.Goodwill(f, PeopleEvents4Math.ManhuntGoodwill);
                    break;
                case "keep":
                    sheet.Add(ImpactKind.Goods, 0, "CC_PEv4_Impact_Keep".Translate().Resolve(), "CC_PEv4_Impact_KeepBrief".Translate().Resolve(), "pev4keep");
                    if (f != null) sheet.Goodwill(f, PeopleEvents4Math.ManhuntGoodwill + 2);
                    break;
                default:
                    sheet.Add(ImpactKind.Other, 0, "CC_PEv4_Impact_Away".Translate(f?.Name ?? "?").Resolve(), "CC_PEv4_Impact_AwayBrief".Translate().Resolve(), "pev4away");
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx) => WardensManhunt.Terms(ctx as HappeningContext, how);
    }
}
