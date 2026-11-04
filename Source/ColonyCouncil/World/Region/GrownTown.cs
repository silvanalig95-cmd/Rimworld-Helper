using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The town that grew from the settlers' camp (docs/design/stage1-region.md, item 7): 60 to 180 days after the other
    /// settlers' year ended in friendship or as neighbours, their steading is a town. The steading already stands as a
    /// town of their people on the world map (the game's own Settlement, made by the other settlers' event under the new
    /// towns' rules: six tiles or more from every home); now the ledger keeps it as a town, its prosperity rises, its
    /// leader is its head, the colony may give it a name of its own, its founding families remember what the colony did
    /// in their first year (that year's flags, kept when it ended, as first:FLAG), and the colony is asked to its first
    /// market. A hamlet's second year (a later release) calls <see cref="Note"/> the same way.
    /// </summary>
    public static class GrownTowns
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        public static WorldSituationDef Def => DefDatabase<WorldSituationDef>.GetNamedSilentFail("Rimfolk_WS_GrownTown");

        /// <summary>The other settlers' year ended (World/Workers_World.cs): in friendship or as neighbours, their steading will grow.</summary>
        public static void Remember(WorldSituation s, SituationEnding ending)
        {
            try
            {
                if (s?.ctx == null || ending == null || ending.key == "hamlet" || !FollowUpsMath.TownGrows(ending.key)) return;
                var town = SituationWorker_NewTown.Town(s);
                if (town == null) return;
                Note(s.ctx.map, town, s.ctx.stranger, s.flags, s.ctx.name, ending.key);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a town that grew: remembered", e);
            }
        }

        /// <summary>
        /// A settlers' town will grow, 60 to 180 days from now, at this home: its leader, the flags of their first year (what
        /// the colony did for them, as the world event's answers set them), what the texts call that year ({TALE}), how it
        /// ended ("alliance", "neighbours", or "hamlet" for a hamlet's second year). The hamlets' release calls this when a
        /// hamlet's second year ends.
        /// </summary>
        public static void Note(Map home, Settlement town, Pawn leader, IEnumerable<string> firstYear, string tale, string how = "hamlet")
        {
            if (town == null || town.Destroyed || town.Faction == null || town.Faction.IsPlayer) return;
            int now = Now;
            var fu = new FollowUp
            {
                kind = "town", people = town.Faction, ending = how, endTick = now, due = FollowUpsMath.DueTick(now, Rand.Value), thing = town.ID, tale = tale,
                flags = firstYear?.Where(x => !x.NullOrEmpty()).Distinct().ToList() ?? new List<string>()
            };
            if (leader != null && !leader.Dead && !leader.Discarded)
            {
                fu.who = leader;
                fu.whoName = leader.LabelShort;
                fu.whoGender = leader.gender;
            }
            FollowUps.Add(FollowUps.HomeOf(home), fu);
        }

        /// <summary>Starts the town's growing at this home now; null when it can't (no slot free, the town or its people not fit).</summary>
        public static WorldSituation Start(Map home, FollowUp f)
        {
            var def = Def;
            var ws = WorldSituations.Instance;
            if (def == null || ws == null || home == null || f == null) return null;
            SituationWorker_GrownTown.pending = f;
            try
            {
                return ws.TryStart(home, d => d == def, false);
            }
            finally
            {
                SituationWorker_GrownTown.pending = null;
            }
        }

        /// <summary>The town while it stands.</summary>
        public static Settlement TownOf(FollowUp f) => f == null || f.thing < 0 ? null : NotableBook.TownOf(f.thing);
    }

    /// <summary>
    /// The town that grew, as a world event on that town (Defs/WorldSituationDefs/Rimfolk_WS_GrownTown.xml). It never
    /// starts by itself: the follow-ups start it (GrownTowns.Start). {PLACE} the town, {X} its leader (the steading's,
    /// else its head), {TALE} the steading as its first year called it, {A} the colonist their people remember by name
    /// (flag friend) or anyone, {ITEM} a founding family's name (flag family). Flags: first:FLAG (their first year:
    /// first:fed, first:gift, first:shared...), afterEnd:alliance / neighbours / hamlet, named (the colony named it).
    /// </summary>
    public class SituationWorker_GrownTown : WorldSituationWorker
    {
        internal static FollowUp pending;

        public override bool CanStart(Map map) => pending != null && base.CanStart(map);

        protected override bool CanHappen(Map map)
        {
            var town = GrownTowns.TownOf(pending);
            var f = town?.Faction;
            return town != null && town.HasName && f != null && !f.IsPlayer && !f.defeated && !f.HostileTo(Faction.OfPlayer);
        }

        public override bool Cast(WorldSituation s, Map map)
        {
            var fu = pending;
            var town = GrownTowns.TownOf(fu);
            if (town == null || map == null || !CanHappen(map)) return false;
            var ctx = s.ctx;
            ctx.map = map;
            ctx.faction = town.Faction;
            s.At(town);
            s.SetNum("town", town.ID);
            ctx.tale = !fu.tale.NullOrEmpty() ? fu.tale : town.Name;
            FollowUps.TakeFlags(s, fu, true);
            // Its leader: the steading's while still of its people and free in the world, else whoever heads it now.
            var leader = fu.who != null && !fu.who.Discarded && FreeInWorld(fu.who) && fu.who.Faction == town.Faction ? fu.who : Head(town);
            if (leader != null)
            {
                ctx.stranger = leader;
                if (FreeInWorld(leader)) s.Hold(leader);
                s.flags.Add("leader");
            }
            // The colonist their people remember best by name (World/KinMemory.cs, World/ColonistMemory.cs), else anyone.
            var friend = KinMemory.Friend(town.Faction, out _);
            if (KinMemory.Ours(friend) && friend.MapHeld == map)
            {
                ctx.a = friend;
                s.flags.Add("friend");
            }
            else ctx.a = IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            var family = WorldLedger.HousesOf(town.ID).FirstOrDefault(h => h != null && h.founding && !h.name.NullOrEmpty())?.name;
            if (!family.NullOrEmpty())
            {
                ctx.item = family;
                s.flags.Add("family");
            }
            if (!def.names.NullOrEmpty()) ctx.name = Pick(def.names, s);
            return true;
        }

        /// <summary>The town's head notable's pawn, while free in the world, or null.</summary>
        private static Pawn Head(Settlement town)
        {
            var book = NotableBook.Instance;
            var n = book?.notables.FirstOrDefault(x => x != null && x.town == town.ID && x.role == NotableRole.Head && x.Alive);
            return n?.pawn != null && FreeInWorld(n.pawn) ? n.pawn : null;
        }

        private static Settlement TownOf(WorldSituation s)
        {
            int id = s.Num("town");
            return id == 0 ? null : NotableBook.TownOf(id);
        }

        /// <summary>
        /// It is a town now: the ledger keeps it as one, its prosperity rises (with the fortunes on), its leader heads it
        /// (a notable), and the history has it as a milestone at its place.
        /// </summary>
        public override void Begin(WorldSituation s)
        {
            var town = TownOf(s);
            if (town == null) return;
            try
            {
                var rec = WorldLedger.TownFor(town.ID, town.Name, town.Faction, true);
                if (rec != null)
                {
                    rec.kind = TownKind.Town;
                    if (RegionBook.FortunesOn && rec.Seeded)
                    {
                        rec.prosperity = FollowUpsMath.Grown(rec.prosperity);
                        rec.cause = "CC_Grown_Cause".Translate().Resolve();
                        rec.causeDir = 1;
                        rec.causeAt = Find.TickManager.TicksGame;
                        rec.lowSince = -1;
                    }
                    WorldLedger.TownChanged();
                }
                var leader = s.ctx.stranger;
                if (leader != null && !leader.Dead && leader.Faction == town.Faction) NotableBook.MakeHead(town, leader);
                var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(town.Faction), HistoryRole.Did) };
                if (leader != null) actors.Add(new HistoryActor(WorldLedger.PersonOf(leader), HistoryRole.Did));
                string line = "CC_Grown_History".Translate(town.Name.Named("PLACE"), town.Faction.Name.Named("FACTION")).Resolve();
                History.Add(FollowUpsMath.KindGrown, actors, Refs.Of(town), HistoryMath.Milestone, line);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a town that grew: begun", e);
            }
        }

        protected override bool StillPossible(WorldSituation s) => base.StillPossible(s) && TownOf(s) != null;

        public override LookTargets StartLook(WorldSituation s)
        {
            var t = TownOf(s);
            return t != null ? new LookTargets(t) : base.StartLook(s);
        }

        /// <summary>The first market: a load of what the colony has plenty of, for the town's carter to take ({COUNT} {THING}, {ITEM} kept for the family).</summary>
        public override bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            // Whoever comes to the gate is the beat's {X}: the leader when free to come, else the town's reeve; the carter for the market.
            if (beat.key == "name" && ctx.stranger != null && !FreeInWorld(ctx.stranger)) ctx.stranger = null;
            // The colonist their people remember is away: no name of theirs to offer.
            if (beat.key == "name" && s.Has("friend") && ctx.a != s.ctx.a) s.Clear("friend");
            if (beat.key != "market") return true;
            ctx.stranger = null;
            var map = ctx.map;
            if (map == null) return false;
            var pick = map.resourceCounter.AllCountedAmounts.Where(kv => kv.Value >= 20 && Sellable(kv.Key) && kv.Value * kv.Key.BaseMarketValue >= 250f)
                .RandomElementByWeightWithFallback(kv => kv.Value * kv.Key.BaseMarketValue);
            if (pick.Key == null) return false;
            float target = Rand.Range(250f, 550f) * LivingEvents.WealthFactor(map);
            ctx.thing = pick.Key;
            ctx.count = VarietyUtil.Round(Mathf.Clamp(Mathf.RoundToInt(target / pick.Key.BaseMarketValue), 10, Mathf.RoundToInt(pick.Value * 0.6f)));
            return ctx.count >= 10;
        }

        private static bool Sellable(ThingDef d) =>
            d != null && d != ThingDefOf.Silver && d.category == ThingCategory.Item && TradeabilityUtility.PlayerCanSell(d.tradeability) && !d.IsDrug && !d.IsCorpse
            && !d.IsMedicine && d.stackLimit >= 10 && d.BaseMarketValue >= 0.5f && d.BaseMarketValue <= 40f && d.GetCompProperties<CompProperties_Rottable>() == null;

        /// <summary>The name: the colony's (by their age's word for a new place), the colonist's they remember, or its own.</summary>
        public override string Chosen(WorldSituation s, SituationContext ctx, SituationBeat beat, int option, bool risk, bool byDefault, string outcome)
        {
            if (beat.key != "name" || byDefault) return outcome;
            var o = option < beat.options.Count ? beat.options[option] : null;
            var town = TownOf(s);
            if (o?.flags == null || town == null) return outcome;
            string age = Ages.Of(town.Faction).ToString();
            string name = null;
            if (o.flags.Contains("namedColony"))
            {
                var home = ctx.map?.Parent as Settlement;
                string ours = home != null && home.HasName ? home.Name : Faction.OfPlayer.Name;
                name = ("CC_Grown_Name_Colony_" + age).Translate(ours.Named("COLONY")).Resolve();
            }
            else if (o.flags.Contains("namedFriend") && ctx.a != null)
            {
                string first = ctx.a.Name is NameTriple nt ? nt.First : ctx.a.LabelShort;
                name = ("CC_Grown_Name_Friend_" + age).Translate(first.Named("NAME")).Resolve();
            }
            if (name.NullOrEmpty()) return outcome;
            try
            {
                town.Name = name;
                s.At(town);
                ctx.placeName = s.ctx.placeName;
                var rec = WorldLedger.TownRecord(town.ID);
                if (rec != null) rec.name = name;
                WorldLedger.NoteTown(town.ID, name, town.Faction);
                NotableBook.TownRenamed(town.ID, name);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a town that grew: its name", e);
            }
            return outcome;
        }
    }
}
