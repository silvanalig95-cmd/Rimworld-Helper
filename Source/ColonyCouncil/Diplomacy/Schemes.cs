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
    /// <summary>
    /// Schemes against the colony (docs/design/stage2-diplomacy.md, item 11, theirs only: the player's decision 16; release
    /// 2c.3). A people with a grudge within a home's reach, or a notable who thinks -40 or less of the colony, may scheme
    /// against it: once a season has passed since that home's last, a day's chance of 8% (the setting "Schemes against the
    /// colony" scales it; about two a year). The scheme is a Scheme tie in the world ledger, from the schemer to the colony,
    /// unknown until it is found out (nothing of it is written into the history before then), given up after a season.
    /// It is acted out only while one of theirs is on the home's map: a visitor of the game's or one of a trader's caravan
    /// (never the trader), grown up, up and about. By the colony's means:
    /// <list type="bullet">
    /// <item>a machine broken down in the night (industrial and after; the job of a rival's sabotage, Harm/Sabotage.cs, and
    /// the game's own breakdown): a jammed machine, a spacer colony's comms console first;</item>
    /// <item>a small valuable taken from a stockpile in the night: a tribe's totem (a small carving), a purse (silver, gold,
    /// jade), a tool (components), into their pack;</item>
    /// <item>a word in a hot-tempered colonist's ear by day, about the one they like least: the game's social fight through
    /// the harm service and its cap, or hard words when the cap holds;</item>
    /// <item>the colony's rooms gone through in the night: a smear, ill words told round the region by their people's ways a
    /// day or so later, 3 goodwill with each other people at peace in reach.</item>
    /// </list>
    /// Caught by a colonist awake, a guard on duty (drafted, or under the on-guard decree) or a dog: a shout (a dog's bark
    /// and the colony woken), the thing dropped, the agent leaving the map at once; the tie broken, the history's
    /// tie:scheme:theirs:broken and dip:foundout, their people remember it (schemedUs) and think 5 less of the colony (never
    /// to hostility), a letter. Not caught, it is noticed the game's way: a machine broken down, something gone from the
    /// stores, a fight. Nothing saved but the tie (an act under way is forgotten at a load, and comes again).
    /// <para>No pass of its own: the day's roll and the hour ride on diplomacy's (Diplomacy.Hourly).</para>
    /// </summary>
    public static class Schemes
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The setting "Schemes against the colony", 0 to 2.</summary>
        public static float Setting => SchemeMath.Rate(S?.theirSchemes ?? 1f);

        /// <summary>Schemes begin and are acted out: the setting above nothing, with answers on the map (they are acted out there). Off, the live ones run out by their terms.</summary>
        public static bool On => Setting > 0.001f && PlayOuts.On;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static readonly Color SayColor = new Color(1f, 0.75f, 0.6f);

        // ---------------- The jobs ----------------

        private static JobDef takeJob, lookJob, wordJob;

        public static JobDef TakeJob => takeJob ??= DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_SchemeTake");

        public static JobDef LookJob => lookJob ??= DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_SchemeLook");

        public static JobDef WordJob => wordJob ??= DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_SchemeWord");

        private static JobDef JobOf(SchemeAct act)
        {
            switch (act)
            {
                case SchemeAct.Machine: return Sabotage.Job;
                case SchemeAct.Theft: return TakeJob;
                case SchemeAct.Fight: return WordJob;
                default: return LookJob;
            }
        }

        // ---------------- An act under way (not saved) ----------------

        private sealed class Intent
        {
            public int tie;
            public SchemeAct act;
            public JobDef job;
            public Pawn agent;
            /// <summary>The machine, or the thing taken.</summary>
            public Thing target;
            public int count;
            /// <summary>The fight's hot temper, and the one they are set against.</summary>
            public Pawn hot, other;
            public int started;
        }

        private static readonly Dictionary<int, Intent> intents = new Dictionary<int, Intent>();
        private static Game intentsGame;

        /// <summary>Dev mode (SchemesDebug.cs): acts at any hour; the next act caught or not, whatever the watch.</summary>
        internal static bool anyHour;
        internal static bool? forceCaught;

        // ---------------- The ties ----------------

        /// <summary>A Scheme tie of theirs, against the colony.</summary>
        public static bool Theirs(Tie t) => t != null && !t.IsView && t.kind == Tie.Kind.Scheme && t.origin == SchemeMath.Origin;

        /// <summary>The schemes of theirs under way (a new list).</summary>
        public static List<Tie> Live() => WorldLedger.LiveTies(Tie.Kind.Scheme).Where(Theirs).ToList();

        /// <summary>The schemes of theirs found out, still kept by the ledger (sixty days after), newest first.</summary>
        public static List<Tie> FoundOut() =>
            WorldLedger.OwnTies(Tie.Kind.Scheme).Where(t => Theirs(t) && t.state == Tie.State.Broken).OrderByDescending(t => t.closed).ToList();

        public static Map HomeOf(Tie t) => t == null ? null : Refs.Home(t.to);

        /// <summary>The notable behind a scheme, if a notable schemes.</summary>
        public static Notable NotableOf(Tie t)
        {
            if (t == null || t.from.kind != RefKind.Person) return null;
            var book = NotableBook.Instance;
            return book?.notables.FirstOrDefault(n => n != null && n.Person == t.from);
        }

        /// <summary>The schemer's people: the people itself, or the notable's.</summary>
        public static Faction PeopleOf(Tie t)
        {
            if (t == null) return null;
            if (t.from.kind == RefKind.People) return Refs.Faction(t.from);
            return NotableOf(t)?.faction ?? WorldLedger.PersonRecord(t.from)?.people;
        }

        /// <summary>When this home's newest scheme began, or -1.</summary>
        private static int LastBegan(Ref colony)
        {
            int last = -1;
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Scheme))
                if (Theirs(t) && t.to == colony && t.since > last)
                    last = t.since;
            return last;
        }

        // ---------------- Who schemes ----------------

        /// <summary>The schemers within a home's reach: each people at peace with a grudge (a town of theirs in reach), each notable that cold.</summary>
        public static List<Ref> Schemers(Map home)
        {
            var list = new List<Ref>();
            if (home == null) return list;
            var reach = RegionTowns.Of(home);
            if (FactionMemory.On && reach != null)
            {
                var mem = FactionMemory.Instance;
                var dead = new Dictionary<Faction, int>();
                foreach (var d in Grudges.All())
                    if (d.faction != null && !d.Settled)
                        dead[d.faction] = dead.TryGetValue(d.faction, out int n) ? n + 1 : 1;
                var comp = CouncilGameComponent.Instance;
                var seen = new HashSet<Faction>();
                foreach (var s in reach.towns)
                {
                    var f = s?.Faction;
                    if (f == null || f.IsPlayer || !seen.Add(f) || !Diplomacy.Deals(f)) continue;
                    bool vendetta = comp?.vendettas?.vendettas != null && comp.vendettas.vendettas.Any(v => v != null && v.Active && v.faction == f);
                    float grudge = SchemeMath.Grudge(mem.Sum(f, DeedSide.Wrong, true), dead.TryGetValue(f, out int k) ? k : 0, vendetta);
                    if (SchemeMath.PeopleSchemes(grudge, Diplomacy.Hostile(f), true)) list.Add(Refs.Of(f));
                }
            }
            var book = NotableBook.On ? NotableBook.Instance : null;
            if (book != null)
                foreach (var n in book.notables)
                {
                    if (n?.faction == null || !Diplomacy.Deals(n.faction)) continue;
                    if (!SchemeMath.NotableSchemes(NotableBook.Opinion(n), n.Alive, Diplomacy.Hostile(n.faction), book.NearColony(n, home))) continue;
                    var p = n.Person;
                    if (!p.IsNone && !list.Contains(p)) list.Add(p);
                }
            return list;
        }

        // ---------------- The day: a roll at each home ----------------

        /// <summary>Once a day (diplomacy's day): each home with a schemer in reach and a season since its last scheme rolls once.</summary>
        public static void Daily(int now)
        {
            if (!On || WorldLedger.Instance == null) return;
            float chance = SchemeMath.Chance(Setting);
            foreach (var home in Homes.All.ToList())
            {
                try
                {
                    Roll(home, now, chance, false, null);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("schemes: a home's day", e);
                }
            }
        }

        /// <summary>A home's roll; with <paramref name="force"/> (dev mode) past the season and the chance. Returns why not, or null.</summary>
        internal static string Roll(Map home, int now, float chance, bool force, SchemeAct? act, Ref? schemer = null)
        {
            var colony = Refs.Colony(home);
            if (colony.IsNone || !Homes.IsHome(home)) return "not one of the colony's homes.";
            if (Live().Any(t => t.to == colony)) return "a scheme against this home is under way already.";
            if (!force && !SchemeMath.SeasonPassed(LastBegan(colony), now)) return "a scheme began here less than a season ago.";
            var schemers = schemer.HasValue ? new List<Ref> { schemer.Value } : Schemers(home);
            if (schemers.Count == 0) return "no people with a grudge and no notable that cold within reach.";
            if (!force && !Rand.Chance(chance)) return null;
            var who = schemers.RandomElement();
            var a = act ?? PickAct(home);
            var t = WorldLedger.AddTie(Tie.Kind.Scheme, who, colony, new TieTerms { key = a.ToString() }, SchemeMath.Until(now), SchemeMath.Origin);
            return t == null ? "the ledger has no room for another scheme." : null;
        }

        /// <summary>An act the home offers, by the colony's means: what it has, whatever the hour (its people asleep, a machine off).</summary>
        private static SchemeAct PickAct(Map home)
        {
            bool machine = SchemeMath.MachineAge(Ages.Ours) && Machines(home).Any();
            bool theft = SchemeMath.TheftOrder(Ages.Ours).Any(k => Loot(home, k).Any());
            bool fight = HotAtHome(home);
            return SchemeMath.Pick(machine, theft, fight, Harm.On ? Harm.Scale : 0f, Rand.Value);
        }

        /// <summary>The act can be done at this home at all (a machine, a small valuable, a hot temper with someone to set against; a smear always).</summary>
        private static bool Possible(SchemeAct act, Map home)
        {
            switch (act)
            {
                case SchemeAct.Machine: return SchemeMath.MachineAge(Ages.Ours) && Machines(home).Any();
                case SchemeAct.Theft: return SchemeMath.TheftOrder(Ages.Ours).Any(k => Loot(home, k).Any());
                case SchemeAct.Fight: return Harm.On && HotAtHome(home);
                default: return true;
            }
        }

        // ---------------- The hour: one of theirs on the map ----------------

        /// <summary>The hour (diplomacy's): each scheme under way acted out when one of theirs is here at its hour; a smear carried home told round.</summary>
        public static void Hourly(int now)
        {
            if (intentsGame != Current.Game)
            {
                intents.Clear();
                intentsGame = Current.Game;
            }
            if (WorldLedger.Instance == null) return;
            var live = Live();
            if (live.Count == 0)
            {
                intents.Clear();
                return;
            }
            foreach (var t in live)
            {
                try
                {
                    Step(t, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("schemes: a scheme's hour", e);
                }
            }
        }

        private static void Step(Tie t, int now)
        {
            var home = HomeOf(t);
            if (home == null)
            {
                // The home is gone: nothing is left to scheme against there.
                intents.Remove(t.id);
                WorldLedger.Set(t, Tie.State.Ended);
                return;
            }
            if (t.terms.count == SchemeMath.Carried)
            {
                if (now >= (long)(t.terms.days * SchemeMath.DayTicks)) Land(t, home);
                return;
            }
            if (intents.TryGetValue(t.id, out var i))
            {
                if (Running(i, now)) return;
                intents.Remove(t.id);
            }
            if (!On) return;
            var f = PeopleOf(t);
            if (f == null || Diplomacy.Hostile(f)) return;
            var agents = Agents(home, f);
            if (agents.Count == 0) return;
            var act = SchemeMath.ActOf(t.terms.key);
            if (!Possible(act, home))
            {
                // What it meant to do is gone from the colony (its one machine sold, the stores emptied): another act.
                act = PickAct(home);
                t.terms.key = act.ToString();
                WorldLedger.Touch(t);
            }
            if (!anyHour && !SchemeMath.Hour(act, GenLocalDate.HourOfDay(home))) return;
            if (PlayOuts.WhyNotNow(home, false) != null) return;
            Begin(t, act, home, agents, now);
        }

        private static bool Running(Intent i, int now) =>
            i.agent != null && !i.agent.Dead && i.agent.Spawned && i.agent.CurJob != null && i.agent.CurJob.def == i.job && now - i.started < 3 * SchemeMath.HourTicks;

        /// <summary>One of theirs who can act for them: a grown-up guest of the game's visitors or a trader's caravan (never the trader), up and about.</summary>
        public static bool Agent(Pawn p)
        {
            if (p == null || p.Dead || !p.Spawned || p.Downed || p.InMentalState || p.Drafted || p.RaceProps == null || !p.RaceProps.Humanlike) return false;
            if (p.IsPrisoner || p.IsSlave || p.IsQuestLodger() || p.DevelopmentalStage.Juvenile() || p.HostileTo(Faction.OfPlayer)) return false;
            var job = p.GetLord()?.LordJob;
            if (!(job is LordJob_VisitColony) && !(job is LordJob_TradeWithColony)) return false;
            if (job is LordJob_TradeWithColony && p.GetTraderCaravanRole() != TraderCaravanRole.Guard) return false;
            if (Strangers.IsHospitalityGuest(p) || Outsiders.Leaving(p)) return false;
            var caps = p.health?.capacities;
            return caps != null && caps.CapableOf(PawnCapacityDefOf.Moving) && caps.CapableOf(PawnCapacityDefOf.Manipulation);
        }

        private static List<Pawn> Agents(Map home, Faction f)
        {
            var list = new List<Pawn>();
            foreach (var p in home.mapPawns.SpawnedPawnsInFaction(f))
                if (Agent(p) && !intents.Values.Any(i => i.agent == p))
                    list.Add(p);
            return list;
        }

        /// <summary>Someone of the colony awake this near a place (nobody sets to it under their nose).</summary>
        private static bool AwakeNear(Map map, IntVec3 at, float range)
        {
            foreach (var c in map.mapPawns.FreeColonistsSpawned)
                if (!c.Dead && !c.Downed && c.Awake() && c.Position.InHorDistOf(at, range))
                    return true;
            return false;
        }

        private static Pawn Nearest(List<Pawn> agents, LocalTargetInfo to, PathEndMode mode, bool reserve)
        {
            Pawn best = null;
            float bestD = float.MaxValue;
            foreach (var a in agents)
            {
                float d = a.Position.DistanceToSquared(to.Cell);
                if (d >= bestD || !a.CanReach(to, mode, Danger.Some) || reserve && !a.CanReserve(to)) continue;
                best = a;
                bestD = d;
            }
            return best;
        }

        private static void Begin(Tie t, SchemeAct act, Map home, List<Pawn> agents, int now)
        {
            var def = JobOf(act);
            if (def == null) return;
            Pawn agent = null, hot = null, other = null;
            Thing target = null;
            int count = 1;
            LocalTargetInfo to = LocalTargetInfo.Invalid;
            switch (act)
            {
                case SchemeAct.Machine:
                    foreach (var b in Machines(home).Take(12))
                    {
                        if (AwakeNear(home, b.Position, SchemeMath.QuietRange)) continue;
                        agent = Nearest(agents, b, PathEndMode.Touch, true);
                        if (agent == null) continue;
                        target = b;
                        break;
                    }
                    to = target;
                    break;
                case SchemeAct.Theft:
                    foreach (var kind in SchemeMath.TheftOrder(Ages.Ours))
                    {
                        foreach (var thing in Loot(home, kind).InRandomOrder().Take(10))
                        {
                            if (AwakeNear(home, thing.Position, SchemeMath.QuietRange)) continue;
                            agent = Nearest(agents, thing, PathEndMode.ClosestTouch, true);
                            if (agent == null) continue;
                            target = thing;
                            count = kind == SchemeMath.Totem ? 1 : SchemeMath.LootCount(thing.MarketValue, thing.stackCount, Rand.Value);
                            break;
                        }
                        if (target != null) break;
                    }
                    to = target;
                    break;
                case SchemeAct.Fight:
                    foreach (var h in Hot(home))
                    {
                        var o = LeastLiked(h, home);
                        if (o == null) continue;
                        agent = Nearest(agents, h, PathEndMode.Touch, false);
                        if (agent == null) continue;
                        hot = h;
                        other = o;
                        break;
                    }
                    to = hot;
                    break;
                default:
                    var cell = QuietRoomCell(home);
                    if (cell.IsValid) agent = Nearest(agents, cell, PathEndMode.OnCell, false);
                    to = cell;
                    break;
            }
            if (agent == null || !to.IsValid) return;
            var job = JobMaker.MakeJob(def, to);
            job.count = count;
            // The night's errand is theirs alone: they slip away from their group to it, and back to it once done.
            agent.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (agent.CurJobDef != def) return;
            intents[t.id] = new Intent { tie = t.id, act = act, job = def, agent = agent, target = target, count = count, hot = hot, other = other, started = now };
        }

        // ---------------- What the colony has ----------------

        /// <summary>The colony's machines that can break down and aren't broken: a spacer colony's comms console first, then the dearest.</summary>
        private static List<Building> Machines(Map home)
        {
            bool spacer = Ages.Ours >= WorldAge.Spacer;
            return home.listerBuildings.allBuildingsColonist.Where(Breakable)
                .OrderByDescending(b => spacer && b.def == ThingDefOf.CommsConsole ? 1 : 0).ThenByDescending(b => b.MarketValue).ToList();
        }

        /// <summary>A machine the game can break down (CompBreakdownable), not broken now; powered or not (the game's breakdown needs no power).</summary>
        private static bool Breakable(Building b)
        {
            if (b == null || !b.Spawned || b.IsBurning()) return false;
            var comp = b.TryGetComp<CompBreakdownable>();
            return comp != null && !comp.BrokenDown;
        }

        /// <summary>Small valuables of a kind in the colony's stockpiles and shelves.</summary>
        private static IEnumerable<Thing> Loot(Map home, string kind)
        {
            var groups = home.haulDestinationManager?.AllGroupsListForReading;
            if (groups == null) yield break;
            foreach (var g in groups)
            {
                foreach (var t in g.HeldThings)
                    if (t != null && t.Spawned && !t.IsBurning() && LootKind(t) == kind)
                        yield return t;
            }
        }

        /// <summary>What a thing would be to a thief: a totem (a small carving), a purse (silver, gold, jade), a tool (components); or null.</summary>
        private static string LootKind(Thing t)
        {
            if (t is MinifiedThing m)
            {
                var inner = m.InnerThing;
                return inner is Building_Art && inner.def.size.x * inner.def.size.z == 1 && m.MarketValue <= SchemeMath.LootMaxUnit ? SchemeMath.Totem : null;
            }
            var d = t.def;
            string kind = d == ThingDefOf.Silver || d == ThingDefOf.Gold || d == ThingDefOf.Jade ? SchemeMath.Purse
                : d == ThingDefOf.ComponentIndustrial || d == ThingDefOf.ComponentSpacer ? SchemeMath.Tool : null;
            if (kind == null) return null;
            return SchemeMath.SmallValuable(t.MarketValue, t.GetStatValue(StatDefOf.Mass)) ? kind : null;
        }

        /// <summary>The colony's hot tempers who could be set against someone now, the hottest first.</summary>
        private static IEnumerable<Pawn> Hot(Map home) =>
            Grown(home).Where(p => Outsiders.Temper(p) >= SchemeMath.HotTemper).OrderByDescending(Outsiders.Temper);

        /// <summary>The home has a hot temper among its grown-ups and someone else grown up, awake or asleep now.</summary>
        private static bool HotAtHome(Map home)
        {
            var grown = home.mapPawns.FreeColonistsSpawned.Where(p => !p.DevelopmentalStage.Juvenile() && !p.WorkTagIsDisabled(WorkTags.Violent)).ToList();
            return grown.Count >= 2 && grown.Any(p => Outsiders.Temper(p) >= SchemeMath.HotTemper);
        }

        private static IEnumerable<Pawn> Grown(Map home) =>
            home.mapPawns.FreeColonistsSpawned.Where(p => !p.DevelopmentalStage.Juvenile() && Together.Able(p) && !p.InBed() && PlayOuts.CanJoin(p)
                                                          && !p.WorkTagIsDisabled(WorkTags.Violent));

        /// <summary>The one a hot temper likes least, kin and lovers left out.</summary>
        private static Pawn LeastLiked(Pawn hot, Map home) =>
            Grown(home).Where(o => o != hot && !hot.GetRelations(o).Any()).OrderBy(o => hot.relations?.OpinionOf(o) ?? 0).FirstOrDefault();

        /// <summary>A cell in one of the colony's rooms with nobody in it (a hall, a store, a workshop), for going through it in the night.</summary>
        private static IntVec3 QuietRoomCell(Map home)
        {
            var people = home.mapPawns.FreeColonistsSpawned.Select(p => p.GetRoom()).Where(r => r != null).ToHashSet();
            var rooms = home.regionGrid.AllRooms.Where(r => r != null && r.ProperRoom && !r.PsychologicallyOutdoors && !r.IsPrisonCell && !r.IsHuge && !r.Fogged
                                                           && r.Role != null && r.Role != RoomRoleDefOf.None && r.CellCount >= 6 && !people.Contains(r)).ToList();
            foreach (var r in rooms.InRandomOrder().Take(6))
            {
                var cells = r.Cells.Where(c => c.Standable(home) && home.areaManager.Home[c] && c.GetFirstPawn(home) == null).ToList();
                if (cells.Count > 0) return cells.RandomElement();
            }
            return IntVec3.Invalid;
        }

        // ---------------- Done: caught, or not ----------------

        /// <summary>
        /// One of theirs finished at it (the last toil of the sabotage's job or of the schemes' own): who saw, and what
        /// follows. False for anyone with no scheme under way (a rival at a bench, a job left over from before a load).
        /// </summary>
        public static bool Done(Pawn agent, Thing target)
        {
            if (agent == null) return false;
            Intent i = null;
            foreach (var kv in intents)
                if (kv.Value.agent == agent)
                {
                    i = kv.Value;
                    break;
                }
            if (i == null) return false;
            intents.Remove(i.tie);
            try
            {
                Resolve(i);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("schemes: an act done", e);
            }
            return true;
        }

        private static void Resolve(Intent i)
        {
            var t = WorldLedger.Get(i.tie);
            var agent = i.agent;
            var map = agent.Map;
            if (t == null || !t.Live || map == null) return;
            var f = PeopleOf(t);
            if (f == null || Diplomacy.Hostile(f)) return;
            Thing took = null;
            if (i.act == SchemeAct.Theft)
            {
                took = Take(i);
                // Gone from the stores since they set out: nothing to take, and they'll look again another night.
                if (took == null) return;
            }
            // together: the word in the hot temper's ear, the agent at their side (the job brought them there).
            if (i.act == SchemeAct.Fight) Say(agent, "Rimfolk_Pool_SchemeWhisper", i.hot, i.other);
            var at = i.act == SchemeAct.Machine && i.target != null && i.target.Spawned ? i.target.Position : agent.Position;
            var watch = Watch(i, map, at, out var by);
            string form = Form(i, took);
            if (watch != SchemeWatch.None) Caught(t, i, f, map, watch, by, took, form);
            else Unseen(t, i, map, took, form);
            Diplomacy.Dirty();
        }

        /// <summary>The thing taken into their pack, as much of the stack as they came for.</summary>
        private static Thing Take(Intent i)
        {
            var thing = i.target;
            var agent = i.agent;
            if (thing == null || !thing.Spawned || thing.Map != agent.Map || agent.inventory == null) return null;
            int n = Math.Min(Math.Max(1, i.count), thing.stackCount);
            var part = thing.SplitOff(n);
            if (agent.inventory.innerContainer.TryAdd(part, false)) return part;
            GenPlace.TryPlaceThing(part, agent.Position, agent.Map, ThingPlaceMode.Near);
            return null;
        }

        /// <summary>The form the act took, kept in the tie's key once done: a jammed machine or a console, a totem, a purse, a tool, the fight, the smear by their ways.</summary>
        private static string Form(Intent i, Thing took)
        {
            switch (i.act)
            {
                case SchemeAct.Machine: return SchemeMath.MachineForm(Ages.Ours, i.target?.def == ThingDefOf.CommsConsole);
                case SchemeAct.Theft: return (took != null ? LootKind(took) : null) ?? SchemeMath.Purse;
                case SchemeAct.Fight: return "Fight";
                default:
                    var f = i.agent?.Faction;
                    return SchemeMath.SmearForm(Ages.Of(f), Ages.IsEmpire(f));
            }
        }

        private static HediffDef onGuard;
        private static bool onGuardLooked;

        /// <summary>On guard: drafted, or awake under the council's on-guard decree (sleep in shifts, eyes on the horizon).</summary>
        private static bool Guard(Pawn c)
        {
            if (c.Drafted) return true;
            if (!onGuardLooked)
            {
                onGuard = DefDatabase<HediffDef>.GetNamedSilentFail("Council_Decree_OnGuard");
                onGuardLooked = true;
            }
            return onGuard != null && c.health?.hediffSet != null && c.health.hediffSet.HasHediff(onGuard);
        }

        private static readonly string[] Dogs = { "LabradorRetriever", "Husky", "YorkshireTerrier" };

        /// <summary>A dog of the colony's: the game's three, or another mod's by its name.</summary>
        private static bool Dog(Pawn a) =>
            a?.def != null && a.RaceProps.Animal && (Dogs.Contains(a.def.defName) || a.def.defName.IndexOf("Dog", StringComparison.OrdinalIgnoreCase) >= 0);

        private static bool Sees(Pawn w, IntVec3 at)
        {
            try
            {
                var room = w.GetRoom();
                if (room != null && room == at.GetRoom(w.Map)) return true;
                return GenSight.LineOfSight(w.Position, at, w.Map, true);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Who catches them at it: in the night a colonist awake within sight, a guard on duty further, a dog awake near
        /// (through a wall close by); a word in someone's ear, whoever is within earshot (the hot temper aside). The nearest of
        /// the one who catches it.
        /// </summary>
        private static SchemeWatch Watch(Intent i, Map map, IntVec3 at, out Pawn by)
        {
            by = null;
            Pawn colonist = null, guard = null, dog = null;
            int colonists = 0, guards = 0, dogs = 0;
            float cd = float.MaxValue, gd = float.MaxValue, dd = float.MaxValue;
            bool word = i.act == SchemeAct.Fight;
            foreach (var c in map.mapPawns.FreeColonistsSpawned)
            {
                if (c == i.hot || c.Dead || c.Downed || c.InMentalState || !c.Awake() || c.DevelopmentalStage.Baby()) continue;
                bool g = Guard(c);
                if (word)
                {
                    if (!Together.Hears(c, i.agent, SchemeMath.EarRange)) continue;
                }
                else if (!c.Position.InHorDistOf(at, g ? SchemeMath.GuardRange : SchemeMath.SeeRange) || !Sees(c, at)) continue;
                float d = c.Position.DistanceToSquared(at);
                if (g)
                {
                    guards++;
                    if (d < gd)
                    {
                        guard = c;
                        gd = d;
                    }
                }
                else
                {
                    colonists++;
                    if (d < cd)
                    {
                        colonist = c;
                        cd = d;
                    }
                }
            }
            if (!word)
                foreach (var a in map.mapPawns.SpawnedColonyAnimals)
                {
                    if (!Dog(a) || a.Dead || a.Downed || !a.Awake() || a.InMentalState) continue;
                    if (!a.Position.InHorDistOf(at, SchemeMath.DogNear) && !(a.Position.InHorDistOf(at, SchemeMath.DogRange) && Sees(a, at))) continue;
                    dogs++;
                    float d = a.Position.DistanceToSquared(at);
                    if (d < dd)
                    {
                        dog = a;
                        dd = d;
                    }
                }
            float roll = Rand.Value;
            if (forceCaught.HasValue)
            {
                roll = forceCaught.Value ? 0f : 1f;
                bool forced = forceCaught.Value;
                forceCaught = null;
                if (forced && colonists + guards + dogs == 0)
                {
                    // Dev mode: caught whatever the watch, by the nearest of the colony (woken if asleep).
                    colonist = map.mapPawns.FreeColonistsSpawned.Where(c => c != i.hot && !c.Dead && !c.Downed && !c.DevelopmentalStage.Baby())
                        .OrderBy(c => c.Position.DistanceToSquared(at)).FirstOrDefault();
                    if (colonist == null) return SchemeWatch.None;
                    if (!colonist.Awake()) RestUtility.WakeUp(colonist);
                    colonists = 1;
                }
            }
            var w = SchemeMath.Catcher(colonists, guards, dogs, roll, word ? SchemeMath.EarNotice : SchemeMath.ColonistNotice);
            by = w == SchemeWatch.Guard ? guard : w == SchemeWatch.Colonist ? colonist : w == SchemeWatch.Dog ? dog : null;
            return by == null ? SchemeWatch.None : w;
        }

        /// <summary>Caught at it: a shout (or a dog's bark and the colony woken), the thing dropped, the agent gone at once; the tie found out, their people remembering it.</summary>
        private static void Caught(Tie t, Intent i, Faction f, Map map, SchemeWatch watch, Pawn by, Thing took, string form)
        {
            var agent = i.agent;
            string thing = ThingWords(i.target, took);
            if (took != null && agent.inventory.innerContainer.Contains(took)) agent.inventory.innerContainer.TryDrop(took, agent.Position, map, ThingPlaceMode.Near, out _);
            if (watch == SchemeWatch.Dog)
            {
                by.caller?.DoCall(true);
                Wake(map, agent.Position);
            }
            else Say(by, "Rimfolk_Pool_SchemeCaught", null, null, "act:" + i.act.ToString().ToLowerInvariant());
            Say(agent, "Rimfolk_Pool_SchemeCaughtGoes", null);
            SendOff(agent, f);
            // The ledger: what it was and who was caught at it, found out.
            var thingDef = took?.def ?? i.target?.def;
            if (took is MinifiedThing m && m.InnerThing != null) thingDef = m.InnerThing.def;
            t.terms.key = form;
            t.terms.goods = thingDef?.defName;
            t.terms.about = WorldLedger.PersonOf(agent);
            WorldLedger.Touch(t);
            WorldLedger.Set(t, Tie.State.Broken);
            string what = What(form, thing, i.hot, i.other);
            var colony = Diplomacy.Colony(map);
            var actors = new List<HistoryActor>
            {
                new HistoryActor(t.from, HistoryRole.Did), new HistoryActor(colony, HistoryRole.Against), new HistoryActor(t.terms.about, HistoryRole.About)
            };
            string colonyName = DipWords.NameOf(colony);
            History.Add(SchemeMath.HistoryKind("broken"), actors, colony, HistoryMath.Everyday,
                P("CC_Scheme_Hist_Broken", DipWords.People(f).Named("PEOPLE"), colonyName.Named("COLONY")));
            Diplomacy.Write(DiplomacyMath.FoundOut, actors, colony,
                P("CC_Scheme_Hist_FoundOut", agent.LabelShort.Named("AGENT"), DipWords.People(f).Named("PEOPLE"), what.Named("WHAT"), colonyName.Named("COLONY")), true);
            // Their people remember that the colony caught them at it, and think less of it for it.
            FactionMemory.Note(f, "schemedUs", agent.LabelShort);
            LoseGoodwill(f, SchemeMath.CaughtGoodwill);
            NotableOf(t)?.Remember(P("CC_Scheme_NotableNote", agent.LabelShort.Named("AGENT")), -SchemeMath.CaughtGoodwill);
            string how = How(watch, by, i.act);
            string after = After(i.act, agent, thing, i.hot);
            string text = T("CC_Scheme_Caught", agent.Named("AGENT"), DipWords.People(f).Named("PEOPLE"), what.Named("WHAT"), how.Named("HOW"), after.Named("AFTER"));
            Tell.Letter(NewsKind.World, Importance.Important, map, P("CC_Scheme_Caught_Label", agent.Named("AGENT")), text, LetterDefOf.NegativeEvent,
                new LookTargets(agent), f);
        }

        /// <summary>Not caught: the machine breaks down (the game's way), the thing goes with them, the word does its work, the tales are carried home.</summary>
        private static void Unseen(Tie t, Intent i, Map map, Thing took, string form)
        {
            t.terms.key = form;
            switch (i.act)
            {
                case SchemeAct.Machine:
                    var comp = i.target?.TryGetComp<CompBreakdownable>();
                    if (comp == null || comp.BrokenDown || !i.target.Spawned) return;
                    comp.DoBreakdown();
                    FleckMaker.ThrowMicroSparks(i.target.TrueCenter(), map);
                    t.terms.goods = i.target.def.defName;
                    WorldLedger.Set(t, Tie.State.Kept);
                    return;
                case SchemeAct.Theft:
                    t.terms.goods = (took is MinifiedThing m && m.InnerThing != null ? m.InnerThing.def : took?.def)?.defName;
                    WorldLedger.Set(t, Tie.State.Kept);
                    return;
                case SchemeAct.Fight:
                    var hot = i.hot;
                    var other = i.other;
                    if (hot != null && other != null && !hot.Dead && !other.Dead)
                        Together.When("scheme fight|" + hot.thingIDNumber + "|" + other.thingIDNumber, hot, other, () => Provoked(hot, other), 12f, 0f);
                    WorldLedger.Set(t, Tie.State.Kept);
                    return;
                default:
                    int lands = SchemeMath.SmearLands(Now, Rand.Value);
                    t.terms.count = SchemeMath.Carried;
                    t.terms.days = lands / (float)SchemeMath.DayTicks;
                    // Carried home, it is told round whatever the season's end: the tie lasts till then.
                    if (t.until != Tie.Never && t.until < lands + 2 * SchemeMath.HourTicks) t.until = lands + 2 * SchemeMath.HourTicks;
                    WorldLedger.Touch(t);
                    return;
            }
        }

        /// <summary>The hot temper goes to the one they were told about, and has it out: the game's social fight under the harm cap for both, or hard words.</summary>
        private static void Provoked(Pawn hot, Pawn other)
        {
            if (hot == null || other == null || hot.Dead || other.Dead || hot.Map == null) return;
            // together: the two face to face (Together.When); the line names what was said of the other.
            Say(hot, "Rimfolk_Pool_SchemeProvoked", other);
            bool fight = Harm.Fight(hot, other, "a quarrel a guest stirred up");
            string text = TextVariants.Vary(fight ? "CC_Scheme_Fight" : "CC_Scheme_Words").Translate(hot.Named("PAWN"), other.Named("OTHER")).Resolve();
            Tell.Message(NewsKind.Colony, fight ? Importance.Important : Importance.Notable, hot.Map, text, new LookTargets(new[] { hot, other }), MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>A smear carried home is told round the region: every other people at peace in reach thinks a little less of the colony.</summary>
        private static void Land(Tie t, Map home)
        {
            var f = PeopleOf(t);
            var heard = new List<Faction>();
            var reach = RegionTowns.Of(home);
            if (reach != null)
                foreach (var s in reach.towns)
                {
                    var o = s?.Faction;
                    if (o == null || o == f || o.IsPlayer || heard.Contains(o) || !Diplomacy.Deals(o) || Diplomacy.Hostile(o)) continue;
                    heard.Add(o);
                }
            foreach (var o in heard) LoseGoodwill(o, SchemeMath.SmearGoodwill);
            WorldLedger.Set(t, Tie.State.Kept);
            if (heard.Count == 0) return;
            string where = P("CC_Scheme_SmearWhere_" + t.terms.key);
            string peoples = heard.Select(DipWords.People).ToCommaList(true);
            string text = T("CC_Scheme_Smear", DipWords.NameOf(t.to).Named("COLONY"), where.Named("WHERE"), peoples.Named("PEOPLES"));
            Tell.Message(NewsKind.World, Importance.Notable, home, text, LookTargets.Invalid, MessageTypeDefOf.NegativeEvent);
            Diplomacy.Dirty();
        }

        /// <summary>The agent leaves at once, on their own, at a jog; the rest of theirs stay as they were.</summary>
        private static void SendOff(Pawn agent, Faction f)
        {
            var map = agent.Map;
            if (map == null) return;
            agent.GetLord()?.RemovePawn(agent);
            LordMaker.MakeNewLord(f ?? agent.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Jog, false, true), map, new List<Pawn> { agent });
        }

        /// <summary>A dog's barking wakes the nearest of the colony asleep.</summary>
        private static void Wake(Map map, IntVec3 at)
        {
            var p = map.mapPawns.FreeColonistsSpawned.Where(c => !c.Dead && !c.Downed && !c.Awake() && c.Position.InHorDistOf(at, 30f))
                .OrderBy(c => c.Position.DistanceToSquared(at)).FirstOrDefault();
            if (p != null) RestUtility.WakeUp(p);
        }

        /// <summary>Their people think less of the colony, never so much that this alone turns them hostile; no message (the letter or the news says it).</summary>
        private static void LoseGoodwill(Faction f, int amount)
        {
            if (f == null || f.IsPlayer || !f.HasGoodwill || Diplomacy.Hostile(f) || amount <= 0) return;
            try
            {
                while (amount > 0 && f.PlayerGoodwill + f.CalculateAdjustedGoodwillChange(Faction.OfPlayer, -amount) <= SchemeMath.HostileAt) amount--;
                if (amount > 0) f.TryAffectGoodwillWith(Faction.OfPlayer, -amount, false, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("schemes: goodwill", e);
            }
        }

        // ---------------- The words ----------------

        private static string T(string key, params NamedArgument[] args) => TextVariants.Vary(key).Translate(args).Resolve();

        private static string P(string key, params NamedArgument[] args) => key.Translate(args).Resolve();

        /// <summary>The thing in words: "the comms console", "120 silver", "a small sculpture".</summary>
        private static string ThingWords(Thing target, Thing took)
        {
            var t = took ?? target;
            if (t == null) return "";
            if (t is MinifiedThing m && m.InnerThing != null) return Find.ActiveLanguageWorker.WithIndefiniteArticle(m.InnerThing.def.label);
            if (t.def.stackLimit > 1) return t.stackCount > 1 ? t.stackCount + " " + Find.ActiveLanguageWorker.Pluralize(t.def.label, t.stackCount) : Find.ActiveLanguageWorker.WithIndefiniteArticle(t.def.label);
            return Find.ActiveLanguageWorker.WithDefiniteArticle(t.def.label);
        }

        /// <summary>What they were caught at, by the form it took, for the letter and the history: "at the generator in the night, working to jam it".</summary>
        private static string What(string form, string thing, Pawn hot, Pawn other)
        {
            if (form == "Fight")
                return hot != null && other != null ? P("CC_Scheme_What_Fight", hot.Named("HOT"), other.Named("OTHER")) : P("CC_Scheme_Short_Fight");
            string key = SchemeMath.ActOf(form) == SchemeAct.Smear ? "CC_Scheme_What_Smear_" + form : "CC_Scheme_What_" + form;
            return P(key, thing.Named("THING"));
        }

        /// <summary>The same, short, for a row of the pages (what was taken and how much isn't kept): "cutting a purse from the colony's stores".</summary>
        private static string Short(string form, string thing)
        {
            string key = SchemeMath.ActOf(form) == SchemeAct.Smear ? "CC_Scheme_What_Smear_" + form : "CC_Scheme_Short_" + form;
            return P(key, thing.Named("THING"));
        }

        /// <summary>Who caught them, as a sentence.</summary>
        private static string How(SchemeWatch watch, Pawn by, SchemeAct act)
        {
            switch (watch)
            {
                case SchemeWatch.Guard: return P("CC_Scheme_How_Guard", by.Named("WATCHER"));
                case SchemeWatch.Dog: return P("CC_Scheme_How_Dog", by.LabelShort.Named("DOG"));
                default: return P(act == SchemeAct.Fight ? "CC_Scheme_How_Heard" : "CC_Scheme_How_Awake", by.Named("WATCHER"));
            }
        }

        /// <summary>What came of it, as a sentence: nothing broken, the thing dropped, the lies not listened to, nothing to tell.</summary>
        private static string After(SchemeAct act, Pawn agent, string thing, Pawn hot)
        {
            switch (act)
            {
                case SchemeAct.Machine: return P("CC_Scheme_After_Machine", agent.Named("AGENT"));
                case SchemeAct.Theft: return P("CC_Scheme_After_Theft", agent.Named("AGENT"), thing.Named("THING"));
                case SchemeAct.Fight: return hot != null ? P("CC_Scheme_After_Fight", agent.Named("AGENT"), hot.Named("HOT")) : P("CC_Scheme_After_Smear", agent.Named("AGENT"));
                default: return P("CC_Scheme_After_Smear", agent.Named("AGENT"));
            }
        }

        private static void Say(Pawn p, string pool, Pawn to, Pawn about = null, string tag = null)
        {
            try
            {
                if (p == null || !p.Spawned) return;
                string line = Flavor.FromPool(pool, p, null, null, tag);
                if (line.NullOrEmpty()) return;
                var args = new List<NamedArgument> { p.Named("PAWN") };
                if (to != null) args.Add(to.Named("OTHER"));
                if (about != null) args.Add(about.Named("ABOUT"));
                // together: a line to the other, shown only while the two are together (Speech.Say checks it); a shout with nobody named, to whoever hears.
                Speech.Say(p, line.Formatted(args.ToArray()).Resolve(), to, SayColor);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("schemes: a line from " + pool, e);
            }
        }

        // ---------------- The pages ----------------

        /// <summary>
        /// The schemes found out, for the pages (Region, Here and away; a people's card on Peoples): who was caught at what,
        /// and when; a click opens the tie's card. Of one people with <paramref name="f"/>, else every one.
        /// </summary>
        public static List<DipRow> FoundRows(Faction f = null)
        {
            var rows = new List<DipRow>();
            if (WorldLedger.Instance == null) return rows;
            foreach (var t in FoundOut())
            {
                var people = PeopleOf(t);
                if (f != null && people != f) continue;
                string agent = WorldLedger.NameOf(t.terms.about) ?? P("CC_Scheme_Someone");
                var def = t.terms.goods.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(t.terms.goods);
                string thing = def == null ? P("CC_Scheme_AMachine") : Find.ActiveLanguageWorker.WithDefiniteArticle(def.label);
                string what = Short(t.terms.key, thing);
                rows.Add(new DipRow
                {
                    text = P("CC_Scheme_Row", agent.Named("AGENT"), DipWords.People(people).Named("PEOPLE"), what.Named("WHAT")),
                    note = P("CC_Scheme_RowNote", FactionMemory.AgoText(t.closed >= 0 ? t.closed : t.since).Named("AGO")), color = UiKit.Warn, link = t,
                    home = Homes.KeyOf(HomeOf(t))
                });
            }
            return rows;
        }

        // ---------------- Dev mode ----------------

        /// <summary>Dev mode: a schemer for a home whatever the grudges, of a people with someone here who could act for them.</summary>
        internal static Ref? DebugSchemer(Map home)
        {
            var real = Schemers(home);
            foreach (var r in real)
            {
                var f = r.kind == RefKind.People ? Refs.Faction(r) : NotableBook.Instance?.notables.FirstOrDefault(n => n.Person == r)?.faction;
                if (f != null && home.mapPawns.SpawnedPawnsInFaction(f).Any(Agent)) return r;
            }
            foreach (var p in home.mapPawns.AllPawnsSpawned)
                if (Agent(p) && p.Faction != null && Diplomacy.Deals(p.Faction))
                    return Refs.Of(p.Faction);
            return real.Count > 0 ? real[0] : (Ref?)null;
        }

        /// <summary>Dev mode: the smear carried home is told now.</summary>
        internal static string DebugLand(Map home)
        {
            var colony = Refs.Colony(home);
            var t = Live().FirstOrDefault(x => x.to == colony && x.terms.count == SchemeMath.Carried);
            if (t == null) return "no smear carried home from this home.";
            t.terms.days = Now / (float)SchemeMath.DayTicks;
            Land(t, home);
            return "told round the region.";
        }

        /// <summary>Dev mode: what is under way, for the log.</summary>
        internal static string DebugList()
        {
            var lines = new List<string>();
            foreach (var t in Live())
                lines.Add($"{DipWords.NameOf(t.from)} against {DipWords.NameOf(t.to)}: {t.terms.key}{(t.terms.count == SchemeMath.Carried ? " (carried home)" : "")}, since day {t.since / SchemeMath.DayTicks + 1}"
                          + (intents.ContainsKey(t.id) ? ", acting now" : ""));
            return lines.Count == 0 ? "no scheme under way." : string.Join("; ", lines);
        }
    }

    /// <summary>
    /// One of theirs at the colony on a scheme's errand (Schemes): at a stockpile taking a small valuable into their pack, in
    /// one of the colony's rooms looking about, or at a hot temper's side with a word in their ear; a few seconds at it,
    /// then what it does (Schemes.Done). A machine broken down uses the rival's sabotage job (Harm/Sabotage.cs).
    /// </summary>
    public class JobDriver_Scheme : JobDriver
    {
        private bool IsWord => job.def == Schemes.WordJob;

        private bool IsLook => job.def == Schemes.LookJob;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => IsWord || IsLook || pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        public override string GetReport()
        {
            if (IsLook) return "CC_Scheme_Report_Look".Translate().Resolve();
            var t = job.targetA.Thing;
            if (t == null) return base.GetReport();
            return IsWord ? "CC_Scheme_Report_Word".Translate(t.Named("OTHER")).Resolve() : "CC_Scheme_Report_Take".Translate(t.LabelShort.Named("THING")).Resolve();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            if (IsLook)
            {
                yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
                yield return Toils_General.Wait(300);
            }
            else if (IsWord)
            {
                this.FailOnDespawnedOrNull(TargetIndex.A);
                this.FailOn(() => !Together.Able(job.targetA.Pawn));
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
                var word = Toils_General.Wait(150, TargetIndex.A);
                word.AddFailCondition(() => !pawn.Position.InHorDistOf(job.targetA.Cell, 4f));
                yield return word;
            }
            else
            {
                this.FailOnDespawnedOrNull(TargetIndex.A);
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
                var at = Toils_General.Wait(180, TargetIndex.A);
                at.WithProgressBarToilDelay(TargetIndex.A);
                yield return at;
            }
            var done = ToilMaker.MakeToil("SchemeDone");
            done.initAction = () => Schemes.Done(pawn, job.targetA.Thing);
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }
}
