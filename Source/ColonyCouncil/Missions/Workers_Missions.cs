using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // The missions a neighbor may ask of the colony (texts: Defs/MissionDefs/Council_Missions.xml): goods for a funeral
    // or a wedding by a date, a prize animal that bolted to be tamed and handed back, and a shrine for their pilgrims.

    /// <summary>
    /// A mission's offer, drawn by the storyteller like any event. It sets out the terms (<see cref="Setup"/>), shows up
    /// in the Quests tab (or as a letter), and once taken on is started, checked and ended by its worker.
    /// </summary>
    public abstract class HappeningWorker_Mission : IncidentWorker_Happening
    {
        private Mission making;

        public MissionText More => def.GetModExtension<MissionText>();

        protected abstract string Kind { get; }

        /// <summary>Days before the same kind of mission is offered again.</summary>
        protected virtual float RestDays => 45f;

        /// <summary>Stars in the Quests tab.</summary>
        protected virtual int Rating => 2;

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            Missions.CanOffer(Kind, RestDays) && StorytellerEvents.Adults(map).Any() && Possible(map);

        /// <summary>The colony could do it at all (has what it takes, a neighbor to ask).</summary>
        protected abstract bool Possible(Map map);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            making = new Mission { kind = Kind, def = def };
            return Setup(making, ctx, ext);
        }

        /// <summary>The terms: who asks, what, by when, for what. Fills the mission and the offer's texts.</summary>
        protected abstract bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext);

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var m = making;
            if (m == null) return false;
            Missions.Register(m);
            ctx.note = Missions.Note(m);
            making = null;
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            if (making != null) Release(making);
            making = null;
            base.Abort(ctx);
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var m = making ?? Missions.Of(ctx);
            if (m != null)
                foreach (var t in Missions.Tags(m))
                    tags.Add(t);
        }

        /// <summary>The terms, under the offer's text.</summary>
        protected override string Extra(HappeningContext ctx)
        {
            var m = making ?? Missions.Of(ctx);
            if (m == null) return null;
            return VarietyUtil.Line(More?.terms, ctx, Missions.Tags(m), Missions.GoodsText(m), TextUtil.DaysString(Mathf.RoundToInt(m.days * GenDate.TicksPerDay)));
        }

        /// <summary>Into the Quests tab when it can go there; otherwise a letter with "accept" and "decline".</summary>
        protected override void Send(HappeningContext ctx, HappeningExtension ext)
        {
            var m = Missions.Of(ctx);
            string label = ctx.Format(Pick(ext.titles, ctx) ?? def.letterLabel ?? def.label).CapitalizeFirst();
            if (m != null) m.title = label;
            if (m != null && Missions.AsQuests)
            {
                string text = ctx.Format(Pick(ext.texts, ctx));
                string extra = Extra(ctx);
                if (Missions.TryOfferAsQuest(m, label, text + (extra.NullOrEmpty() ? "" : "\n\n" + extra), ext.answerDays, Rating)) return;
            }
            base.Send(ctx, ext);
        }

        /// <summary>Answered by letter: taken on, or not.</summary>
        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var m = Missions.Of(ctx);
            if (m != null && m.state == MissionState.Offered)
            {
                if (option == 0 && !byDefault) Missions.Accept(m);
                else Missions.Decline(m);
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        // ---------------- While it runs ----------------

        /// <summary>The colony took it on: set it going. False: it can't go ahead after all.</summary>
        public abstract bool Start(Mission m, Map map);

        /// <summary>Every few seconds while it runs: progress, success, failure.</summary>
        public abstract void Check(Mission m, Map map, int now);

        /// <summary>A line on where things stand, beyond the goods (the World page, the quest).</summary>
        public virtual string Status(Mission m) => null;

        /// <summary>Lets go of whatever the mission made and no longer needs.</summary>
        public virtual void Release(Mission m)
        {
        }

        public virtual LookTargets Targets(Mission m) => m.x != null && m.x.Spawned ? new LookTargets(m.x) : LookTargets.Invalid;

        protected static string Line(List<string> lines, Mission m, Map map, string item = null, string days = null) =>
            VarietyUtil.Line(lines, Missions.Context(m, map), Missions.Tags(m), item ?? "", days);

        protected static Faction Neighbor(Map map) =>
            Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && LivingEvents.TownOf(f, map) != null) ?? Arrivals.Friendly(VarietyUtil.OnGoodTerms);

        /// <summary>The colony could make sculptures: it has an art bench, or could build one, and someone to work it.</summary>
        public static bool CanSculpt(Map map)
        {
            var bench = DefDatabase<ThingDef>.GetNamedSilentFail("TableSculpting");
            if (bench == null || !(map.listerBuildings.ColonistsHaveBuilding(bench) || Avail.CanBuild(bench, map))) return false;
            return VarietyUtil.Best(map, SkillDefOf.Artistic) != null;
        }

        protected static void Fill(Mission m, HappeningContext ctx)
        {
            ctx.faction = m.faction;
            ctx.a = m.a;
            ctx.placeName = m.place;
            ctx.count = m.reward;
            ctx.tale = m.tale ?? Missions.GoodsText(m);
            ctx.dead = m.name;
            ctx.beast = m.name;
        }
    }

    // ===================== Goods for a funeral, or a wedding =====================

    /// <summary>
    /// A neighbor town has a funeral (or a wedding) to provide for and not enough hands: it asks the colony for meals,
    /// sculptures, stone blocks or beer, all of it by a date. In full, it pays well over the worth; half of everything
    /// at least, it pays the plain worth; less, and it goes elsewhere and remembers.
    /// </summary>
    public class HappeningWorker_MissionFeast : HappeningWorker_Mission
    {
        protected override string Kind => "feast";

        protected override bool Possible(Map map) => Neighbor(map) != null;

        private static bool Brews(Map map)
        {
            var beer = DefDatabase<ThingDef>.GetNamedSilentFail("Beer");
            return beer != null && (Avail.ResearchDone("Brewing") || VarietyUtil.InStores(map, beer) >= 10);
        }

        private static bool CutsStone(Map map) => Avail.ResearchDone("Stonecutting") || Missions.Count(map, "blocks") >= 20;

        private static int Scaled(Map map, float min, float max) => VarietyUtil.Round(Mathf.Max(5, Mathf.RoundToInt(Rand.Range(min, max) * LivingEvents.WealthFactor(map))));

        private static void Add(Mission m, string what, int n)
        {
            var g = m.goals.FirstOrDefault(x => x.what == what);
            if (g != null) g.count = VarietyUtil.Round(g.count + n);
            else m.goals.Add(new MissionGoal { what = what, count = n });
        }

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            if (m.faction == null) return false;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            bool funeral = Rand.Chance(0.5f);
            m.variant = funeral ? "funeral" : "wedding";
            // Who it's for: their old head, or the bride; someone of theirs, named and never met.
            var kind = Arrivals.PersonKind(m.faction);
            var p = kind == null ? null : Arrivals.Make(kind, m.faction, funeral ? new FloatRange(62f, 85f) : new FloatRange(18f, 26f), funeral ? (Gender?)null : Gender.Female, null);
            m.name = p?.LabelShort ?? "?";
            if (p != null) Arrivals.Discard(p);
            bool art = CanSculpt(map);
            string wood = ThingDefOf.WoodLog.defName;
            Add(m, "meals", Scaled(map, 20f, 35f));
            if (funeral)
            {
                if (art) Add(m, "art", 2);
                else Add(m, wood, Scaled(map, 150f, 250f));
                if (CutsStone(map)) Add(m, "blocks", Scaled(map, 60f, 100f));
                else Add(m, wood, Scaled(map, 120f, 200f));
            }
            else
            {
                if (Brews(map)) Add(m, "Beer", Scaled(map, 25f, 40f));
                else Add(m, "textiles", Scaled(map, 50f, 90f));
                if (art) Add(m, "art", 1);
                else Add(m, "textiles", Scaled(map, 30f, 50f));
            }
            float worth = m.goals.Sum(g => g.count * Missions.UnitValue(g.what));
            m.reward = VarietyUtil.Round(Mathf.RoundToInt(worth * 1.6f));
            m.goodwill = 12;
            m.penalty = 8;
            m.days = Rand.Range(8f, 11f);
            Fill(m, ctx);
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            bool all = m.goals.All(g => g.have >= g.count);
            if (m.stage == 0 && all)
            {
                // Everything's ready early: their carts come for it within half a day. Told once: if the stores dip below
                // and fill up again (meals eaten and cooked), the carts still come when they said they would.
                m.stage = 1;
                if (m.due < 0)
                {
                    m.due = Mathf.Min(now + GenDate.TicksPerDay / 2, m.deadline);
                    string line = Line(More?.ready, m, map, null, TextUtil.DaysString(m.due - now));
                    if (!line.NullOrEmpty()) Messages.Message(line, MessageTypeDefOf.PositiveEvent, false);
                }
            }
            else if (m.stage == 1 && !all) m.stage = 0;
            if (m.stage == 1 && now >= m.due) Deliver(m, map);
            else if (now >= m.deadline) Deliver(m, map);
        }

        /// <summary>Their carts come: everything (well paid), at least half of everything (plain price), or not enough.</summary>
        private void Deliver(Mission m, Map map)
        {
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            var spot = DropCellFinder.TradeDropSpot(map);
            var look = new LookTargets(new TargetInfo(spot, map));
            if (m.goals.All(g => g.have >= g.count))
            {
                foreach (var g in m.goals) Missions.Take(map, g.what, g.count);
                VarietyUtil.PaySilver(map, spot, m.reward);
                Missions.Goodwill(m, m.goodwill);
                Missions.Finish(m, MissionState.Done, "delivered", m.reward.ToString(), look);
                return;
            }
            if (m.goals.All(g => g.have * 2 >= g.count))
            {
                float worth = 0f;
                foreach (var g in m.goals) worth += Missions.Take(map, g.what, g.count) * Missions.UnitValue(g.what);
                int pay = VarietyUtil.Round(Mathf.Max(10, Mathf.RoundToInt(worth)));
                VarietyUtil.PaySilver(map, spot, pay);
                Missions.Finish(m, MissionState.Done, "partial", pay.ToString(), look, LetterDefOf.NeutralEvent);
                return;
            }
            Missions.Goodwill(m, -m.penalty);
            Missions.Finish(m, MissionState.Failed, "missed", null);
        }

        public override string Status(Mission m) =>
            m.stage == 1 ? "CC_Mission_Feast_Ready".Translate(m.faction?.Name ?? "?", TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame))).ToString() : null;
    }

    // ===================== The runaway =====================

    /// <summary>
    /// A neighbor's prize animal bolted and is loose on the colony's land, wild again. Tame it before it wanders off, and
    /// hand it back for a reward (or keep it, paid for or not). Killed, it's a bitter failure; lost, a small one.
    /// </summary>
    public class HappeningWorker_MissionRunaway : HappeningWorker_Mission
    {
        public const string TamedDef = "Council_Mission_RunawayTamed";

        protected override string Kind => "runaway";

        protected override int Rating => 1;

        private static readonly string[] Kinds = { "Horse", "Muffalo", "Dromedary", "Yak", "Donkey", "Cow", "Alpaca", "Bison", "Goat", "Elk", "Caribou" };

        /// <summary>Farm animals that would be fine outdoors here right now, and that could be tamed at all.</summary>
        private static IEnumerable<PawnKindDef> Candidates(Map map)
        {
            foreach (var name in Kinds)
            {
                var k = DefDatabase<PawnKindDef>.GetNamedSilentFail(name);
                if (k?.race?.race == null || !k.RaceProps.Animal || k.RaceProps.Insect || k.RaceProps.predator) continue;
                if (k.race.GetStatValueAbstract(StatDefOf.Wildness) > 0.75f) continue;
                if (!map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f)) continue;
                yield return k;
            }
        }

        private static Pawn Tamer(Map map) => VarietyUtil.Best(map, SkillDefOf.Animals);

        protected override bool Possible(Map map) => Neighbor(map) != null && Tamer(map) != null && Candidates(map).Any();

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            var tamer = Tamer(map);
            if (m.faction == null || tamer == null) return false;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = tamer;
            int skill = tamer.skills.GetSkill(SkillDefOf.Animals).Level;
            // One the colony's best handler could win over.
            foreach (var kind in Candidates(map).InRandomOrder().Take(4))
            {
                float age = kind.RaceProps.lifeExpectancy * Rand.Range(0.25f, 0.45f);
                var animal = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true, fixedBiologicalAge: age, fixedChronologicalAge: age));
                if (animal == null) continue;
                if (TrainableUtility.MinimumHandlingSkill(animal) > skill)
                {
                    Find.WorldPawns.PassToWorld(animal, PawnDiscardDecideMode.Discard);
                    continue;
                }
                string name = More?.names.NullOrEmpty() ?? true ? kind.label : Flavor.PickText(More.names);
                animal.Name = new NameSingle(name);
                Arrivals.Hold(animal);
                m.x = animal;
                m.name = name;
                break;
            }
            if (m.x == null) return false;
            m.reward = StoryChains.Silver(map, 200f, 380f);
            m.goodwill = 10;
            m.penalty = 6;
            m.days = Rand.Range(5f, 7f);
            Fill(m, ctx);
            ctx.thing = m.x.def;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var x = m.x;
            if (x == null || x.Dead || x.Discarded) return false;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, CellFinder.EdgeRoadChance_Animal, false, null)) return false;
            Find.WorldPawns.ForcefullyKeptPawns.Remove(x);
            if (Find.WorldPawns.Contains(x)) Find.WorldPawns.RemovePawn(x);
            GenSpawn.Spawn(x, cell, map);
            return x.Spawned;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var x = m.x;
            if (x == null || x.Discarded)
            {
                Missions.Finish(m, MissionState.Failed, "lost", null);
                return;
            }
            if (x.Dead)
            {
                Missions.Goodwill(m, -m.penalty * 2);
                Missions.Finish(m, MissionState.Failed, "killed", null, x.Corpse != null && x.Corpse.Spawned ? new LookTargets(x.Corpse) : LookTargets.Invalid);
                return;
            }
            if (x.Faction == Faction.OfPlayer)
            {
                if (m.stage == 0)
                {
                    m.stage = 2;
                    m.due = now + GenDate.TicksPerDay;
                }
                // Only while it's on a map (not off with a caravan). Handed back unasked only as agreed, and never when one
                // of the colony's people has bonded with it: then the colony is asked, however long that takes.
                if (!x.Spawned) return;
                if (m.stage == 2 && !Ask(m, map) && now >= m.due && !Bonded(x)) HandBack(m, map);
                // The question went unanswered somehow: after a day or two, it's handed back (or asked again).
                else if (m.stage == 3 && now >= m.due + GenDate.TicksPerDay && !Missions.LetterOut(m))
                {
                    if (Bonded(x)) m.stage = 2;
                    else HandBack(m, map);
                }
                return;
            }
            if (!x.Spawned)
            {
                Missions.Goodwill(m, -m.penalty);
                Missions.Finish(m, MissionState.Failed, "lost", null);
                return;
            }
            if (now >= m.deadline)
            {
                // Off into the hills before anyone could win it over.
                x.mindState.exitMapAfterTick = now + GenDate.TicksPerHour;
                Missions.Goodwill(m, -m.penalty);
                Missions.Finish(m, MissionState.Failed, "escaped", null, new LookTargets(x));
            }
        }

        /// <summary>Tamed: hand it back, or keep it? Asked in a letter as soon as no other question is waiting.</summary>
        private static bool Ask(Mission m, Map map)
        {
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(TamedDef);
            if (def == null || HappeningTracker.Waiting) return false;
            HappeningWorker_MissionTamed.current = m;
            try
            {
                var parms = StorytellerUtility.DefaultParmsNow(def.category ?? IncidentCategoryDefOf.Misc, map);
                parms.forced = true;
                if (!def.Worker.TryExecute(parms)) return false;
            }
            finally
            {
                HappeningWorker_MissionTamed.current = null;
            }
            m.stage = 3;
            return true;
        }

        /// <summary>One of the colony's people has bonded with it.</summary>
        public static bool Bonded(Pawn x) =>
            x?.relations != null && x.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Bond && r.otherPawn != null && !r.otherPawn.Dead && r.otherPawn.Faction == Faction.OfPlayer);

        /// <summary>Their herder leads it home: it leaves the colony, and the reward is paid.</summary>
        public static void HandBack(Mission m, Map map)
        {
            var x = m.x;
            // Their people turned on the colony in the meantime: there's nobody to hand it to.
            if (!LivingEvents.Friendly(m.faction))
            {
                Missions.Finish(m, MissionState.Failed, "cancelled", null);
                return;
            }
            if (x != null && !x.Dead && x.Spawned && x.Faction == Faction.OfPlayer)
            {
                try
                {
                    // Free of the colony first: no master to follow, no rope, no caravan or other group of ours.
                    x.GetLord()?.RemovePawn(x);
                    x.roping?.BreakAllRopes();
                    if (x.playerSettings != null) x.playerSettings.Master = null;
                    x.SetFaction(m.faction);
                    // It walks off with their herder if it can (not down, not shut in a pen); else the herder carries it off.
                    if (!x.Downed && ReachabilityUtility.CanReachMapEdge(x))
                        LordMaker.MakeNewLord(m.faction, new LordJob_ExitMapBest(LocomotionUrgency.Walk, false, false), x.Map, new List<Pawn> { x });
                    else
                    {
                        x.DeSpawn();
                        if (!Find.WorldPawns.Contains(x)) Find.WorldPawns.PassToWorld(x, PawnDiscardDecideMode.Decide);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("mission runaway hand back", e);
                    // Only ever one that is theirs already: the colony's own animal never leaves this way.
                    if (x.Faction != Faction.OfPlayer)
                    {
                        if (x.Spawned) x.DeSpawn();
                        if (!x.Spawned && !Find.WorldPawns.Contains(x)) Find.WorldPawns.PassToWorld(x, PawnDiscardDecideMode.Decide);
                    }
                }
            }
            VarietyUtil.PaySilver(map, DropCellFinder.TradeDropSpot(map), m.reward);
            Missions.Goodwill(m, m.goodwill);
            Missions.Finish(m, MissionState.Done, "returned", m.reward.ToString(), x != null && x.Spawned ? new LookTargets(x) : LookTargets.Invalid);
        }

        public override void Release(Mission m)
        {
            var x = m.x;
            if (x == null || x.Dead) return;
            // Never brought onto the map (the offer was turned down): it was only ever a name.
            if (!x.Spawned && x.Faction == null && m.started < 0) Arrivals.Discard(x);
            else Find.WorldPawns.ForcefullyKeptPawns.Remove(x);
        }

        public override string Status(Mission m)
        {
            var x = m.x;
            if (x == null || x.Dead) return null;
            return (x.Faction == Faction.OfPlayer ? "CC_Mission_Runaway_Tamed" : "CC_Mission_Runaway_Wild").Translate(m.name ?? x.LabelShort).ToString();
        }
    }

    /// <summary>The runaway is tamed: hand it back for the reward, keep it and pay for it, or keep it and pay nothing.</summary>
    public class HappeningWorker_MissionTamed : IncidentWorker_Happening
    {
        internal static Mission current;

        protected override bool CanHappen(Map map, HappeningExtension ext) => current?.x != null && current.x.Faction == Faction.OfPlayer;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var m = current;
            if (m?.x == null) return false;
            var c = Missions.Context(m, ctx.map);
            ctx.a = c.a;
            ctx.faction = m.faction;
            ctx.placeName = m.place;
            ctx.beast = m.name;
            ctx.stranger = m.x;
            ctx.thing = m.x.def;
            // {COUNT}: what they'd take for it; {TALE}: the reward for handing it back.
            ctx.count = VarietyUtil.Round(Mathf.RoundToInt(m.reward * 0.8f));
            ctx.tale = m.reward.ToString();
            ctx.note = Missions.Note(m);
            return ctx.a != null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var m = Missions.Of(ctx);
            var map = ctx.map != null && Find.Maps.Contains(ctx.map) ? ctx.map : HappeningTracker.HomeMap();
            var x = m?.x;
            if (m != null && m.state == MissionState.Active && map != null)
            {
                // Killed or lost since the letter came: the mission's own check tells that.
                if (x != null && !x.Dead && !x.Discarded)
                {
                    bool ours = x.Faction == Faction.OfPlayer;
                    // Nobody answered, and one of the colony's people has bonded with it: it isn't taken from them unasked.
                    int answer = option == 0 && byDefault && ours && HappeningWorker_MissionRunaway.Bonded(x) ? 2 : option;
                    if (answer == 0 || !ours) HappeningWorker_MissionRunaway.HandBack(m, map);
                    else if (answer == 1)
                    {
                        // Bought (the silver went with the option).
                        Missions.Goodwill(m, 3);
                        Missions.Finish(m, MissionState.Done, "bought", null, new LookTargets(x));
                    }
                    else
                    {
                        Missions.Goodwill(m, -m.penalty * 2);
                        Missions.Finish(m, MissionState.Failed, "kept", null, new LookTargets(x));
                    }
                }
                outcome = "";
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>The animal is the colony's now: nothing to let go of.</summary>
        public override void Cleanup(HappeningContext ctx)
        {
        }
    }

    // ===================== A shrine for the pilgrims =====================

    /// <summary>
    /// A neighbor's pilgrims pass the colony on their way to a holy place, and ask for somewhere to stop and pray: a
    /// sculpture of good enough quality standing under the open sky. Raised in time, the pilgrims come, pray, leave an
    /// offering, and think well of the colony; not there, they walk past.
    /// </summary>
    public class HappeningWorker_MissionShrine : HappeningWorker_Mission
    {
        protected override string Kind => "shrine";

        protected override float RestDays => 60f;

        protected override bool Possible(Map map) => Neighbor(map) != null && CanSculpt(map);

        private static QualityCategory Min(Mission m) => m.variant == "good" ? QualityCategory.Good : QualityCategory.Normal;

        private static Building Shrine(Mission m, Map map)
        {
            var min = Min(m);
            Building best = null;
            var bestQ = QualityCategory.Awful;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                // A work of art (not a grave or a sarcophagus), with open sky over every part of it.
                if (!b.Spawned || b is Building_Grave || b.TryGetComp<CompArt>() == null || b.OccupiedRect().Cells.Any(c => c.Roofed(map))) continue;
                var q = b.TryGetComp<CompQuality>();
                if (q == null || q.Quality < min) continue;
                if (best == null || q.Quality > bestQ)
                {
                    best = b;
                    bestQ = q.Quality;
                }
            }
            return best;
        }

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Neighbor(map);
            if (m.faction == null) return false;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            var artist = VarietyUtil.Best(map, SkillDefOf.Artistic);
            m.a = artist ?? VarietyUtil.Best(map, SkillDefOf.Social);
            if (m.a == null) return false;
            // Good work if the colony has a real artist, else honest work.
            m.variant = (artist?.skills.GetSkill(SkillDefOf.Artistic).Level ?? 0) >= 8 ? "good" : "normal";
            m.reward = StoryChains.Silver(map, 200f, 400f);
            m.goodwill = 12;
            m.penalty = 6;
            m.days = Rand.Range(10f, 14f);
            m.tale = Min(m).GetLabel();
            Fill(m, ctx);
            return true;
        }

        public override bool Start(Mission m, Map map) => true;

        public override void Check(Mission m, Map map, int now)
        {
            var shrine = Shrine(m, map);
            if (m.stage == 0)
            {
                if (shrine != null)
                {
                    // Word goes out: the pilgrims set out at once.
                    m.stage = 1;
                    m.due = Mathf.Min(now + Mathf.RoundToInt(Rand.Range(0.8f, 1.5f) * GenDate.TicksPerDay), m.deadline);
                    string line = Line(More?.ready, m, map, shrine.LabelShortCap, TextUtil.DaysString(m.due - now));
                    if (!line.NullOrEmpty()) Messages.Message(line, new LookTargets(shrine), MessageTypeDefOf.PositiveEvent, false);
                }
                else if (now >= m.deadline)
                {
                    Missions.Goodwill(m, -m.penalty);
                    Missions.Finish(m, MissionState.Failed, "noshrine", null);
                }
                return;
            }
            if (now < m.due) return;
            if (shrine == null)
            {
                // It stood once, and is gone by the time they come.
                Missions.Goodwill(m, -m.penalty / 2);
                Missions.Finish(m, MissionState.Failed, "gone", null);
                return;
            }
            // The pilgrims come and pray, and leave an offering at its foot.
            var pilgrims = new List<Pawn>();
            var kind = Arrivals.PersonKind(m.faction);
            int n = Rand.RangeInclusive(3, 5);
            for (int i = 0; kind != null && i < n; i++)
            {
                var p = Arrivals.Make(kind, m.faction, new FloatRange(18f, 70f));
                if (p != null) pilgrims.Add(p);
            }
            // They gather beside it (a large sculpture can't be stood on), as mourners do by a grave; else just outside the colony.
            IntVec3? spot = CellFinder.TryFindRandomCellNear(shrine.Position, map, 3, c => c.Standable(map) && c.GetFirstBuilding(map) == null, out var near) ? near : (IntVec3?)null;
            if (pilgrims.Count == 0 || Arrivals.Visit(pilgrims, map, m.faction, spot, Rand.Range(16f, 24f)) == null)
                foreach (var p in pilgrims.Where(p => !p.Spawned)) Arrivals.Discard(p);
            VarietyUtil.PaySilver(map, shrine.Position, m.reward);
            Missions.Goodwill(m, m.goodwill);
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Mission_Shrine");
            if (def != null)
                using (Causes.Push(LivingEvents.Cause(m.def)))
                    foreach (var p in StorytellerEvents.Adults(map).ToList())
                        CouncilGameComponent.GiveThought(p, def);
            Missions.Finish(m, MissionState.Done, "blessed", m.reward.ToString(), new LookTargets(shrine));
        }

        public override string Status(Mission m)
        {
            var map = HappeningTracker.HomeMap();
            if (map == null) return null;
            var shrine = Shrine(m, map);
            if (m.stage == 1 && shrine != null)
                return "CC_Mission_Shrine_Ready".Translate(shrine.LabelShortCap, TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame))).ToString();
            return "CC_Mission_Shrine_None".Translate(Min(m).GetLabel()).ToString();
        }

        public override LookTargets Targets(Mission m)
        {
            var map = HappeningTracker.HomeMap();
            var shrine = map == null ? null : Shrine(m, map);
            return shrine != null ? new LookTargets(shrine) : LookTargets.Invalid;
        }
    }
}
