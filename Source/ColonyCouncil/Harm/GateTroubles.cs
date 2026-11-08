using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Harm, part 7: visitors and outsiders (Harm/HarmSeven.cs is the hub). A fistfight with one of another people is the
    /// outsiders' own (a brawl their people remember, goodwill lost, never a war: Outsiders' bouts), through the harm
    /// service, with the deeper hooks; a duel or a bout asked for is the outsiders' own duel or bout.
    /// <list type="bullet">
    /// <item>Open gates, and who walks in (1.2.4): while the gates stand open, once a decree, about half the time (half
    /// with the warden of guests in office, half again under the on-guard decree), one of the visitors takes advantage: a
    /// thief at the stores (seen, chased off and caught it comes to blows half the time; unseen, off with a little of the
    /// stores), or one who drinks and picks a fight in the yard. Named on the decree's card. With the visitors.</item>
    /// <item>The gambler's game turns (2.2.2): letting anyone play the travelling gambler, one game in six a cheat is
    /// caught and a hot-tempered loser starts a fistfight; the gambler goes on. Named under the answer. The colony's best
    /// playing the one game, or no gambling, keeps it calm.</item>
    /// <item>The barber's nick (2.5.3): one cut in thirty the razor nicks a shoulder, twice for a poor barber; a spacer's
    /// clippers never nick. Not preventable but by declining the barber.</item>
    /// <item>The guest of rank's enemy (2.6.2): one dignitary's visit in six draws a hothead of their own people to the
    /// gate; the guest's own guard takes the challenge where there is one, else they ask for the colony's marshal (or its
    /// best fighter), who takes it by their nature (a duel to first blood where the people's ways know duels, else a
    /// bout) or turns it down (their people think a little less of the colony).</item>
    /// <item>A fistfight that draws a crowd (2.8.2): one fistfight after hard words in five, others of both sides near
    /// with a temper to them join, up to four in all (half with the warden near); the marshal near breaks it up.</item>
    /// <item>First blood, not stopped (2.8.3): one duel to first blood in twenty with a bloodthirsty fighter goes on past
    /// the wound until someone yields, and the onlookers pull them apart. The yield terms from the start, or no duel with
    /// the bloodthirsty, keep it to first blood.</item>
    /// <item>A challenge by letter (2.9.1): one in four notables who had hard words with one of ours, or whose people
    /// lost a duel to one of ours, writes a challenge (by their people's means: a runner, a sealed letter, the radio,
    /// comms), and a challenger comes unannounced some days after; refused, the notable thinks less of the colonist. A
    /// letter of greetings or condolences to their people first, or a good word or a gift, settles it. Only where their
    /// people's ways know duels.</item>
    /// <item>The heir's grudge (2.9.2): a head of a town the colony wronged dies; one heir in three carries it on, and
    /// while it holds their people's guards are short with the colony, one visit in five a fistfight. Condolences or a
    /// gift to their people end it.</item>
    /// <item>A trader of the hated people (3.8.1): a vendetta open against a people now at peace, one visit of theirs in
    /// four the avenger picks a fight with one of them. The avenger away from home, or the vendetta laid down, keeps
    /// it from coming.</item>
    /// <item>Zeal at the gate (3.13.2): a devout of the colony's faith and a visitor of another, hard words one visit in
    /// ten (half under the best-behavior decree), then the outsiders' own fistfight chance. With faith in the
    /// colony.</item>
    /// <item>Hungry hands in the fields (2.3.2): in a famine near the colony, one night in fifteen two or three hungry
    /// strangers come for the fields or the stores (half with a night watch, half with the fields lit; never while famine
    /// bread is given out); one of ours who meets them is struck one time in two before they flee. With world
    /// events.</item>
    /// <item>A brawl at the creek (2.3.3): two parties of prospectors on the colony's land at once, once a rush, half the
    /// time their quarrel becomes a brawl, and one of ours too near is caught in it; the marshal at home goes over and
    /// breaks it up first. With world events and the visitors.</item>
    /// </list>
    /// </summary>
    public static class GateTroubles
    {
        private static int Now => Find.TickManager.TicksGame;

        public static bool DebugGates, DebugGamble, DebugBarber, DebugRank, DebugCrowd, DebugBlood, DebugChallenge, DebugHeir, DebugHated, DebugZeal, DebugHungry, DebugCreek;

        /// <summary>Visits and occasions already looked at (a group's lord, a night, a duel): session memory.</summary>
        private static readonly HashSet<string> looked = new HashSet<string>();

        private static readonly HashSet<OutsiderBout> pastBlood = new HashSet<OutsiderBout>();

        /// <summary>A thief at the stores: the thing they mean to take, and whether one of ours saw them.</summary>
        private static readonly Dictionary<int, (Thing thing, bool seen)> thieves = new Dictionary<int, (Thing, bool)>();

        /// <summary>Hungry strangers out in the fields tonight: who, where, until when, and whether they've met one of ours.</summary>
        private sealed class Hungry
        {
            public List<Pawn> pawns;
            public Map map;
            public int until;
            public bool met;
        }

        private static readonly List<Hungry> hungry = new List<Hungry>();

        public static void Reset()
        {
            looked.Clear();
            pastBlood.Clear();
            thieves.Clear();
            hungry.Clear();
        }

        private static bool Look(string key)
        {
            if (looked.Count > 600) looked.Clear();
            return looked.Add(key);
        }

        private static bool CanFight(Pawn p) => p != null && Together.Able(p) && !p.DevelopmentalStage.Juvenile() && p.interactions != null
                                               && !p.WorkTagIsDisabled(WorkTags.Violent) && Harm.May(p, HurtClass.Fight);

        /// <summary>One of another people here at peace: a grown-up, up and about, in a group of their own (a visit, a caravan, a stay).</summary>
        private static bool Guest(Pawn p) => p != null && p.Spawned && !p.Dead && !p.Downed && p.RaceProps?.Humanlike == true && p.Faction != null && !p.Faction.IsPlayer
                                             && !p.HostileTo(Faction.OfPlayer) && !p.IsPrisoner && !p.IsSlave && !p.DevelopmentalStage.Juvenile() && p.GetLord() != null
                                             && !p.IsQuestLodger();

        /// <summary>The groups of another people at a home now, by their lord.</summary>
        private static List<List<Pawn>> Groups(Map map) =>
            map.mapPawns.AllPawnsSpawned.Where(Guest).GroupBy(p => p.GetLord().loadID).Select(g => g.ToList()).ToList();

        private static Pawn Marshal(Map map) => map == null ? null : OfficeTracker.For(map)?.HolderOf("Council_Office_Marshal");

        private static bool Warden(Map map) => map != null && ColonyState.For(map)?.offices?.HolderOf(Interplay.Warden) != null;

        private static Pawn NearestOurs(Pawn x, float range) =>
            x.Map.mapPawns.FreeColonistsSpawned.Where(o => HarmFour.Ours(o) && CanFight(o) && o.Position.InHorDistOf(x.Position, range))
                .OrderBy(o => o.Position.DistanceToSquared(x.Position)).FirstOrDefault();

        /// <summary>The fistfight: the outsiders' own brawl, then counted as the colonist's hurt of its kind.</summary>
        private static bool Brawl(Pawn c, Pawn x, HarmKind kind) => Outsiders.HarmBrawl(c, x, kind);

        /// <summary>The harm book's hour: the open gate, the gambler, the guests' groups, the famine's nights, the rush.</summary>
        public static void Hourly(HarmBook book, int now)
        {
            if (!Harm.On) return;
            Gates(now);
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome || MapFacts.ThreatOn(map)) continue;
                var groups = Groups(map);
                if (groups.Count > 0)
                {
                    Heirs(map, groups);
                    Hated(map, groups);
                    Zeal(map, groups);
                }
                HungryNight(map, now);
                Creek(map, now);
            }
            Ranks(now);
        }

        // ================= Open gates, and who walks in (1.2.4) =================

        private static void Gates(int now)
        {
            if (!Harm.SystemOn(HarmKinds.OpenGates.system)) return;
            var all = CouncilGameComponent.Instance?.colonies;
            if (all == null) return;
            foreach (var c in all)
            {
                var d = c?.decrees;
                var map = d?.Ground;
                if (map == null || !map.IsPlayerHome || MapFacts.ThreatOn(map)) continue;
                var decree = d.active.FirstOrDefault(a => a?.def != null && a.def.hurts == DecreeHurt.Gates);
                if (decree == null || HarmSeven.WaitingOf(HarmSeven.Gates, w => w.id == decree.startTick) != null) continue;
                var strangers = map.mapPawns.AllPawnsSpawned.Where(p => Guest(p) && p.interactions != null && !p.WorkTagIsDisabled(WorkTags.Violent)).ToList();
                if (strangers.Count == 0) continue;
                // One roll a decree: the first visitors while the gates stand open.
                HarmFour.Wait(new HarmWaiting { what = HarmSeven.Gates, kind = HarmKinds.OpenGates.key, id = decree.startTick, made = now });
                var someone = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
                bool onGuard = someone != null && HarmFour.Decree(someone, "Council_Decree_OnGuard");
                bool debug = DebugGates;
                DebugGates = false;
                if (!debug && !Harm.Roll(HarmKinds.OpenGates, HarmSevenMath.Gates(Warden(map), onGuard))) continue;
                var x = strangers.RandomElement();
                if (HarmSevenMath.StrangerOf(Rand.Value) == GateStranger.Thief) Sneak(x, map);
                else Drunk(x, map);
            }
        }

        /// <summary>A small thing worth taking from the stores: food or a raw resource, not too dear, in a stockpile.</summary>
        private static Thing Takeable(Map map, Pawn x)
        {
            foreach (var z in map.zoneManager.AllZones.OfType<Zone_Stockpile>().OrderByDescending(s => s.CellCount))
                foreach (var t in z.AllContainedThings)
                    if (t.def.category == ThingCategory.Item && !t.def.IsWeapon && !t.def.IsApparel && t.def.BaseMarketValue > 0f && t.def.BaseMarketValue <= 20f
                        && t.def.EverHaulable && !t.IsForbidden(Faction.OfPlayer) && x.CanReach(t, PathEndMode.Touch, Danger.Some))
                        return t;
            return null;
        }

        private static void Sneak(Pawn x, Map map)
        {
            var t = Takeable(map, x);
            if (t == null)
            {
                Drunk(x, map);
                return;
            }
            var cell = t.Position;
            var job = JobMaker.MakeJob(HarmSevenDefOf.Rimfolk_StoresSneak, cell);
            job.count = HarmSevenMath.ThiefTicks;
            x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (x.CurJobDef == HarmSevenDefOf.Rimfolk_StoresSneak) thieves[x.thingIDNumber] = (t, false);
        }

        /// <summary>The thief at the stores (JobDriver_HarmWalk's arrival): one of ours near sees them and gives chase.</summary>
        public static void AtStores(Pawn x)
        {
            if (!thieves.TryGetValue(x.thingIDNumber, out var th)) return;
            var c = x.Map.mapPawns.FreeColonistsSpawned.Where(o => HarmFour.Ours(o) && Together.Able(o) && !o.DevelopmentalStage.Baby()
                                                                    && o.Position.InHorDistOf(x.Position, HarmSevenMath.ThiefSeen) && GenSight.LineOfSight(o.Position, x.Position, x.Map))
                .OrderBy(o => o.Position.DistanceToSquared(x.Position)).FirstOrDefault();
            if (c == null) return;
            thieves[x.thingIDNumber] = (th.thing, true);
            // apart: shouted across the yard at the stranger with the sack.
            HarmFour.Say(c, "Rimfolk_Pool_ThiefSeen", null, Ages.Ours.ToString().ToLowerInvariant());
            HarmFour.Mote(x, "CC_HarmSeven_Mote_Thief");
            x.jobs.EndCurrentJob(JobCondition.InterruptForced);
            var go = JobMaker.MakeJob(JobDefOf.Goto, x.Position);
            go.locomotionUrgency = LocomotionUrgency.Sprint;
            go.expiryInterval = 900;
            c.jobs.StartJob(go, JobCondition.InterruptForced, null, false, true);
            HarmSeven.Later(400, () => Caught(c, x));
        }

        private static void Caught(Pawn c, Pawn x)
        {
            thieves.Remove(x.thingIDNumber);
            if (x == null || !x.Spawned || c == null || !c.Spawned || c.Map != x.Map) return;
            if (x.Position.InHorDistOf(c.Position, 5f) && Rand.Chance(HarmSevenMath.ThiefFights) && CanFight(c) && Brawl(c, x, HarmKinds.OpenGates))
            {
                HarmFour.Mote(x, "CC_HarmSeven_Mote_Caught");
                return;
            }
            // Chased off: the sack dropped, the stranger on their way.
            HarmFour.Mote(x, "CC_HarmSeven_Mote_SackDropped");
            Arrivals.SendAway(new[] { x });
        }

        /// <summary>The thief's look about the stores is over (JobDriver_HarmWalk's end): unseen, off with a little of it.</summary>
        public static void SneakDone(Pawn x)
        {
            if (!thieves.TryGetValue(x.thingIDNumber, out var th) || th.seen) return;
            thieves.Remove(x.thingIDNumber);
            var t = th.thing;
            if (t != null && t.Spawned && !t.Destroyed && t.Map == x.Map && x.Position.InHorDistOf(t.Position, 3f) && x.inventory != null)
            {
                var part = t.SplitOff(Math.Max(1, Math.Min(t.stackCount, 10)));
                if (!x.inventory.innerContainer.TryAddOrTransfer(part)) GenPlace.TryPlaceThing(part, x.Position, x.Map, ThingPlaceMode.Near);
            }
            Arrivals.SendAway(new[] { x });
        }

        private static void Drunk(Pawn x, Map map)
        {
            var c = map.mapPawns.FreeColonistsSpawned.Where(o => HarmFour.Ours(o) && CanFight(o) && x.CanReach(o, PathEndMode.Touch, Danger.Some))
                .OrderBy(o => o.Position.DistanceToSquared(x.Position)).FirstOrDefault();
            if (c == null) return;
            // A drink of their own first: the game's own drunkenness, from a bottle out of their pack.
            try
            {
                var beer = ThingMaker.MakeThing(ThingDefOf.Beer);
                if (x.inventory != null && x.inventory.innerContainer.TryAdd(beer))
                {
                    var job = JobMaker.MakeJob(JobDefOf.Ingest, beer);
                    job.count = 1;
                    x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                }
            }
            catch
            {
                // No bottle to be had: sour without it.
            }
            HarmSeven.Later(900, () => Together.When("gatesdrunk|" + x.thingIDNumber, x, c, () =>
            {
                // together: the stranger came to the colonist in the yard (Together.When), face to face.
                HarmFour.Say(x, "Rimfolk_Pool_GateDrunk", c, Ages.Ours.ToString().ToLowerInvariant());
                if (CanFight(c)) Brawl(c, x, HarmKinds.OpenGates);
            }, 6f, 0f));
        }

        // ================= The gambler's game turns (2.2.2) =================

        /// <summary>The colony plays the gambler, anyone who wants (DilemmaEffect_GamblerTurns): their evening of games is watched.</summary>
        public static void Gambling(Pawn gambler)
        {
            if (!Harm.On || gambler == null || !Harm.SystemOn(HarmKinds.GamblerTurns.system)) return;
            HarmFour.Wait(new HarmWaiting { what = HarmSeven.Gamble, kind = HarmKinds.GamblerTurns.key, pawn = gambler, made = Now });
        }

        /// <summary>The evening's games: one look while some of ours play near the gambler. True when done with.</summary>
        public static bool GambleHour(HarmWaiting x, int now)
        {
            var g = x.pawn;
            if (g == null || g.Dead || x.flag) return true;
            if (!g.Spawned || g.Map == null || !HarmSevenMath.Evening(GenLocalDate.HourOfDay(g.Map))) return false;
            var players = g.Map.mapPawns.FreeColonistsSpawned.Where(o => HarmFour.Ours(o) && Together.Can(o, g)).ToList();
            if (players.Count == 0) return false;
            x.flag = true;
            bool debug = DebugGamble;
            DebugGamble = false;
            if (!debug && !Harm.Roll(HarmKinds.GamblerTurns, HarmSevenMath.GamblerChance)) return true;
            var loser = players.Where(CanFight).OrderByDescending(Outsiders.Temper).FirstOrDefault();
            // A cheat caught; a loser with a temper to them goes for the gambler.
            if (loser == null || !HarmSevenMath.Hot(Outsiders.Temper(loser)) && !debug)
            {
                if (loser != null) HarmFour.Say(loser, "Rimfolk_Pool_GamblerCheat", g);
                return true;
            }
            // together: Together.Can, the loser and the gambler across the table.
            HarmFour.Say(loser, "Rimfolk_Pool_GamblerCheat", g, "fight");
            if (Brawl(loser, g, HarmKinds.GamblerTurns)) HarmSeven.Later(HarmMath.HourTicks, () => Arrivals.SendAway(new[] { g }));
            return true;
        }

        /// <summary>The answer that lets anyone play says what can come of it.</summary>
        public static void DescribeGamble(ImpactSheet sheet)
        {
            if (sheet == null || !Harm.On || !Harm.SystemOn(HarmKinds.GamblerTurns.system)) return;
            sheet.Add(ImpactKind.Health, -1, "CC_Impact_GamblerTurns".Translate().Resolve(), "CC_Impact_GamblerTurnsBrief".Translate().Resolve(), "gamblerturns");
        }

        // ================= The barber's nick (2.5.3) =================

        /// <summary>A cut done (Callers.Cut): now and then the razor nicks.</summary>
        public static void Cut(Pawn barber, Pawn p)
        {
            if (!Harm.On || barber == null || p == null || !Harm.SystemOn(HarmKinds.BarberNick.system)) return;
            bool spacer = (barber.Faction?.def?.techLevel ?? TechLevel.Industrial) >= TechLevel.Spacer;
            bool poor = (barber.skills?.GetSkill(SkillDefOf.Artistic)?.Level ?? 10) < HarmSevenMath.PoorBarberSkill;
            bool debug = DebugBarber;
            DebugBarber = false;
            var cls = HurtClass.Mild;
            if (!(debug ? Harm.May(p, cls) : Harm.Roll(p, HarmKinds.BarberNick, HarmSevenMath.Barber(poor, spacer), cls))) return;
            if (Harm.Injure(p, HurtKind.Cut, HurtWhere.Arm, HarmSevenMath.NickLow, HarmSevenMath.NickHigh, HarmKinds.BarberNick, cls, barber) == null) return;
            string tool = (barber.Faction?.def?.techLevel ?? TechLevel.Industrial) <= TechLevel.Neolithic ? "flint" : "razor";
            // together: the colonist sits at the barber's stool.
            HarmFour.Say(barber, "Rimfolk_Pool_BarberNick", p, tool);
            HarmFour.Say(p, "Rimfolk_Pool_NickLaugh", barber);
            HarmFour.Mote(p, "CC_HarmSeven_Mote_Nick");
        }

        // ================= The guest of rank's enemy (2.6.2) =================

        private static void Ranks(int now)
        {
            if (!Harm.SystemOn(HarmKinds.RankEnemy.system)) return;
            foreach (var m in Missions.All)
            {
                if (m == null || m.kind != "dignitary" || !m.Open || m.x == null || !m.x.Spawned || m.x.Map == null || !m.x.Map.IsPlayerHome) continue;
                if (HarmSeven.WaitingOf(HarmSeven.Rank, w => w.id == m.id) != null || !Look("rank|" + m.id)) continue;
                var map = m.x.Map;
                var f = m.faction ?? m.x.Faction;
                bool debug = DebugRank;
                DebugRank = false;
                if (f == null || f.HostileTo(Faction.OfPlayer) || MapFacts.ThreatOn(map)) continue;
                var x = new HarmWaiting { what = HarmSeven.Rank, kind = HarmKinds.RankEnemy.key, id = m.id, made = now, name = m.x.LabelShort };
                HarmFour.Wait(x);
                if (!debug && !Harm.Roll(HarmKinds.RankEnemy, HarmSevenMath.RankEnemyChance))
                {
                    x.flag = true;
                    continue;
                }
                var kind = Arrivals.PersonKind(f);
                Pawn challenger = null;
                try
                {
                    challenger = kind == null ? null : Arrivals.Make(kind, f, new FloatRange(20f, 40f));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("harm seven: the guest of rank's enemy", e);
                }
                if (challenger == null)
                {
                    x.flag = true;
                    continue;
                }
                var gate = PlaySpots.GateCell(map, challenger);
                if (Arrivals.Visit(new List<Pawn> { challenger }, map, f, gate.IsValid ? gate : (IntVec3?)null, 24f) == null)
                {
                    Arrivals.Discard(challenger);
                    x.flag = true;
                    continue;
                }
                x.other = challenger;
                // The guest's own guard, if they brought one; else the colony's marshal, or its best fighter.
                x.pawn = m.group?.FirstOrDefault(g => g != null && g != m.x && g.Spawned && g.Map == map && CanFight(g));
                x.flag2 = x.pawn != null;
                if (x.pawn == null) x.pawn = Marshal(map) ?? map.mapPawns.FreeColonistsSpawned.Where(o => HarmFour.Ours(o) && CanFight(o)).OrderByDescending(Outsiders.Melee).FirstOrDefault();
            }
        }

        /// <summary>The challenger at the gate: the guard takes it, or the colony's marshal is asked. True when done with.</summary>
        public static bool RankHour(HarmWaiting x, int now)
        {
            var ch = x.other;
            var c = x.pawn;
            if (x.flag || ch == null || ch.Dead || !ch.Spawned) return x.flag || ch == null || ch.Dead || now - x.made > HarmSevenMath.RankEnemyDays * HarmMath.DayTicks;
            if (c == null || c.Dead || !c.Spawned || c.Map != ch.Map) return false;
            Together.When("rank|" + ch.thingIDNumber, ch, c, () =>
            {
                if (x.flag) return;
                x.flag = true;
                // together: the challenger came to them (Together.When) and challenges face to face.
                HarmFour.Say(ch, "Rimfolk_Pool_RankChallenge", c, x.flag2 ? "guard" : "colony");
                if (x.flag2)
                {
                    // The guest's own guard takes it: a fight between the two of them, nobody of the colony's.
                    if (CanFight(ch) && CanFight(c)) Harm.Fight(c, ch, null);
                    HarmSeven.Later(HarmMath.HourTicks * 2, () => Arrivals.SendAway(new[] { ch }));
                    return;
                }
                if (!Outsiders.TakesChallenge(c, ch) || !Harm.May(c, HurtClass.Bout) || !Outsiders.Challenge(c, ch, HarmKinds.RankEnemy))
                {
                    HarmFour.Say(c, "Rimfolk_Pool_ChallengeRefused", ch);
                    var f = ch.Faction;
                    if (f != null) WorldSituationWorker.Goodwill(f, -2, true);
                    HarmSeven.Later(HarmMath.HourTicks, () => Arrivals.SendAway(new[] { ch }));
                    return;
                }
                HarmFour.Say(c, "Rimfolk_Pool_ChallengeTaken", ch);
            }, 12f, 0f);
            return x.flag;
        }

        // ================= A fistfight that draws a crowd (2.8.2) =================

        /// <summary>Hard words have come to blows (Outsiders.Provoke): others of both sides near may join.</summary>
        public static void Crowd(Pawn c, Pawn x)
        {
            if (!Harm.On || c?.Map == null || x == null || !Harm.SystemOn(HarmKinds.CrowdFight.system)) return;
            var map = c.Map;
            var warden = ColonyState.For(map)?.offices?.HolderOf(Interplay.Warden);
            bool wardenNear = warden != null && warden.Spawned && warden.Map == map && warden.Position.InHorDistOf(c.Position, 20f);
            bool debug = DebugCrowd;
            DebugCrowd = false;
            if (!debug && !Harm.Roll(HarmKinds.CrowdFight, HarmSevenMath.Crowd(wardenNear))) return;
            var ours = map.mapPawns.FreeColonistsSpawned.Where(p => p != c && HarmFour.Ours(p) && CanFight(p) && Outsiders.Temper(p) >= HarmSevenMath.CrowdTemper
                                                                   && p.Position.InHorDistOf(c.Position, HarmSevenMath.CrowdRange)).ToList();
            var theirs = map.mapPawns.AllPawnsSpawned.Where(p => p != x && p.Faction == x.Faction && Guest(p) && CanFight(p) && Outsiders.Temper(p) >= HarmSevenMath.CrowdTemper
                                                                && p.Position.InHorDistOf(c.Position, HarmSevenMath.CrowdRange)).ToList();
            int join = HarmSevenMath.CrowdJoins(ours.Count, theirs.Count);
            var fighting = new List<Pawn> { c, x };
            for (int i = 0; i < join; i++)
            {
                // together: both are within a few steps of the brawl.
                HarmFour.Say(ours[i], "Rimfolk_Pool_CrowdJoins", theirs[i]);
                if (Brawl(ours[i], theirs[i], HarmKinds.CrowdFight))
                {
                    fighting.Add(ours[i]);
                    fighting.Add(theirs[i]);
                }
            }
            if (fighting.Count <= 2) return;
            HarmFour.Mote(c, "CC_HarmSeven_Mote_Crowd");
            // The marshal near breaks it up.
            var marshal = Marshal(map);
            if (marshal == null || !Together.Able(marshal) || !marshal.Position.InHorDistOf(c.Position, 25f)) return;
            HarmSeven.Later(500, () =>
            {
                if (!marshal.Spawned || !Together.Able(marshal)) return;
                // apart: the marshal's shout over the brawl.
                HarmFour.Say(marshal, "Rimfolk_Pool_MarshalBreaksUp");
                foreach (var p in fighting)
                    if (p?.MentalStateDef == MentalStateDefOf.SocialFighting) p.MentalState.RecoverFromState();
            });
        }

        // ================= First blood, not stopped (2.8.3) =================

        /// <summary>
        /// The referee's look, as a duel to first blood is won (Outsiders.Referee): a bloodthirsty fighter may not stop at
        /// it, and the duel goes on until someone yields. True when it goes on.
        /// </summary>
        public static bool PastFirstBlood(OutsiderBout bout)
        {
            if (!Harm.On || bout?.a == null || bout.b == null || bout.terms != DuelTerms.FirstBlood || !Harm.SystemOn(HarmKinds.PastFirstBlood.system)) return false;
            if (pastBlood.Contains(bout)) return false;
            if (pastBlood.Count > 50) pastBlood.Clear();
            pastBlood.Add(bout);
            bool a = bout.a.story?.traits?.HasTrait(TraitDefOf.Bloodlust) ?? false, b = bout.b.story?.traits?.HasTrait(TraitDefOf.Bloodlust) ?? false;
            bool debug = DebugBlood;
            DebugBlood = false;
            var colonist = bout.a;
            if (!(debug ? Harm.May(colonist, HurtClass.Bout) : Harm.Roll(colonist, HarmKinds.PastFirstBlood, HarmSevenMath.PastBlood(a || b), HurtClass.Bout))) return false;
            var wild = a ? bout.a : bout.b;
            bout.terms = DuelTerms.Yield;
            Harm.Note(colonist, HarmKinds.PastFirstBlood);
            // apart: shouted over the duel as it goes on past the wound.
            HarmFour.Say(wild, "Rimfolk_Pool_PastFirstBlood");
            var then = bout.after;
            bout.after = done =>
            {
                then?.Invoke(done);
                PulledApart(done);
            };
            return true;
        }

        private static void PulledApart(OutsiderBout bout)
        {
            var map = bout.a?.Map ?? bout.b?.Map;
            if (map == null) return;
            var by = bout.watchers.FirstOrDefault(w => w != null && w.Spawned && !w.Dead && Together.Able(w))
                     ?? map.mapPawns.FreeColonistsSpawned.Where(o => o != bout.a && Together.Able(o) && o.Position.InHorDistOf(bout.ring, 15f)).FirstOrDefault();
            // apart: the onlooker's shout as they pull the two apart.
            if (by != null) HarmFour.Say(by, "Rimfolk_Pool_PullApart");
        }

        // ================= A challenge by letter (2.9.1) =================

        /// <summary>
        /// Hard words with a notable themself, or one of their people beaten in a duel by one of ours (Outsiders' outcomes):
        /// one in four writes a challenge, where their people's ways know duels.
        /// </summary>
        public static void Slighted(Pawn c, Pawn x, bool duel)
        {
            if (!Harm.On || c == null || x?.Faction == null || x.Faction.IsPlayer || !Harm.SystemOn(HarmKinds.LetterChallenge.system) || !HarmFour.Ours(c)) return;
            var notable = Outsiders.NotableOf(x);
            if (!duel && notable == null) return;
            if (!OutsiderMath.DuelFits((int)(x.Faction.def?.techLevel ?? TechLevel.Industrial), ModsConfig.RoyaltyActive, x.royalty?.MostSeniorTitle != null)) return;
            if (HarmSeven.WaitingOf(HarmSeven.Challenge, w => w.pawn == c && w.id == x.Faction.loadID) != null) return;
            bool debug = DebugChallenge;
            DebugChallenge = false;
            if (!debug && !Harm.Roll(HarmKinds.LetterChallenge, HarmSevenMath.ChallengeHold)) return;
            HarmFour.Wait(new HarmWaiting
            {
                what = HarmSeven.Challenge, kind = HarmKinds.LetterChallenge.key, pawn = c, id = x.Faction.loadID, id2 = notable?.id ?? -1, name = notable?.Name ?? x.LabelShort,
                made = Now, due = Now + (debug ? 0 : HarmSevenMath.LetterTicks(Rand.Value))
            });
        }

        /// <summary>A letter of greetings or condolences to a notable of this people since then, or a good word or a gift (Quarrels.Eased): settled.</summary>
        private static bool Apologised(Faction f, int since)
        {
            var book = NotableBook.Instance;
            if (book?.letters == null || f == null) return false;
            foreach (var l in book.letters)
            {
                if (l == null || l.created < since || l.purpose != LetterPurpose.Greetings && l.purpose != LetterPurpose.Condolences) continue;
                var n = book.Get(l.notable);
                if (n?.faction == f && l.stage != LetterStage.Cancelled && l.stage != LetterStage.ToWrite && l.stage != LetterStage.Writing) return true;
            }
            return false;
        }

        private static Faction FactionOf(int loadID) => Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.loadID == loadID);

        /// <summary>A challenge owed: the letter, then the challenger at the gate, then the asking. True when done with.</summary>
        public static bool ChallengeHour(HarmWaiting x, int now)
        {
            var c = x.pawn;
            var f = FactionOf(x.id);
            if (c == null || c.Dead || f == null || f.defeated || f.HostileTo(Faction.OfPlayer)) return true;
            if (x.count < 2 && Apologised(f, x.made)) return true;
            if (now < x.due) return false;
            if (x.count == 0)
            {
                if (!c.Spawned || c.Map == null || !c.Map.IsPlayerHome) return false;
                x.count = 1;
                x.due = now + (x.flag ? 0 : HarmSevenMath.ComesTicks(Rand.Value));
                // A challenge by letter: Rimfolk's own letters, by their people's means (a runner, a sealed letter, the radio, comms).
                var age = LedgerMath.AgeOf((int)(f.def?.techLevel ?? TechLevel.Industrial));
                string text = ("CC_HarmSeven_Challenge_" + age).Translate(c.Named("PAWN"), (x.name ?? "?").Named("NOTABLE"), f.Name.Named("FACTION")).Resolve();
                Tell.Letter(NewsKind.World, Importance.Notable, c.Map, "CC_HarmSeven_ChallengeTitle".Translate(c.Named("PAWN"), (x.name ?? "?").Named("NOTABLE")).Resolve(), text,
                    LetterDefOf.NeutralEvent, new LookTargets(c));
                return false;
            }
            if (x.count == 1)
            {
                if (!c.Spawned || c.Map == null || !c.Map.IsPlayerHome || MapFacts.ThreatOn(c.Map)) return false;
                var kind = Arrivals.PersonKind(f);
                Pawn ch = null;
                try
                {
                    ch = kind == null ? null : Arrivals.Make(kind, f, new FloatRange(20f, 45f));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("harm seven: a challenger", e);
                }
                if (ch == null) return true;
                var gate = PlaySpots.GateCell(c.Map, ch);
                if (Arrivals.Visit(new List<Pawn> { ch }, c.Map, f, gate.IsValid ? gate : (IntVec3?)null, 24f) == null)
                {
                    Arrivals.Discard(ch);
                    return true;
                }
                x.other = ch;
                x.count = 2;
                return false;
            }
            var chal = x.other;
            if (chal == null || chal.Dead || !chal.Spawned) return true;
            if (x.flag2) return true;
            if (!c.Spawned || c.Map != chal.Map) return false;
            Together.When("challenge|" + chal.thingIDNumber, chal, c, () =>
            {
                if (x.flag2) return;
                x.flag2 = true;
                // together: the challenger came to the colonist by name (Together.When), face to face.
                HarmFour.Say(chal, "Rimfolk_Pool_LetterChallenger", c);
                if (Outsiders.TakesChallenge(c, chal) && Harm.May(c, HurtClass.Bout) && Outsiders.Challenge(c, chal, HarmKinds.LetterChallenge))
                {
                    HarmFour.Say(c, "Rimfolk_Pool_ChallengeTaken", chal);
                    return;
                }
                HarmFour.Say(c, "Rimfolk_Pool_ChallengeRefused", chal);
                var n = x.id2 >= 0 ? NotableBook.Instance?.Get(x.id2) : null;
                if (n != null)
                {
                    n.RegardOf(c, true).personal += HarmSevenMath.RefusedRegard;
                    NotableBook.Changed();
                }
                HarmSeven.Later(HarmMath.HourTicks, () => Arrivals.SendAway(new[] { chal }));
            }, 18f, 0f);
            return x.flag2;
        }

        // ================= The heir's grudge (2.9.2) =================

        /// <summary>A town's head is followed by the heir (NotableBook's look): if the colony had wronged the one who led, the heir may carry it on.</summary>
        public static void HeirSucceeded(Notable old, Notable heir)
        {
            if (!Harm.On || old?.faction == null || heir == null || !Harm.SystemOn(HarmKinds.HeirGrudge.system)) return;
            bool debug = DebugHeir;
            DebugHeir = false;
            if (!debug && old.personal > HarmSevenMath.HeirWronged) return;
            if (!debug && !Harm.Roll(HarmKinds.HeirGrudge, HarmSevenMath.HeirChance)) return;
            if (HarmSeven.WaitingOf(HarmSeven.Heir, w => w.id == old.faction.loadID) != null) return;
            HarmFour.Wait(new HarmWaiting { what = HarmSeven.Heir, kind = HarmKinds.HeirGrudge.key, id = old.faction.loadID, id2 = heir.id, name = heir.Name, made = Now });
        }

        private static void Heirs(Map map, List<List<Pawn>> groups)
        {
            if (!Harm.SystemOn(HarmKinds.HeirGrudge.system)) return;
            var list = HarmBook.Instance?.waiting;
            if (list == null) return;
            foreach (var w in list.Where(w => w != null && w.what == HarmSeven.Heir).ToList())
            {
                var f = FactionOf(w.id);
                if (f == null || Apologised(f, w.made))
                {
                    list.Remove(w);
                    continue;
                }
                foreach (var g in groups)
                {
                    if (g[0].Faction != f || !Look("heir|" + g[0].GetLord().loadID)) continue;
                    var guard = g.Where(p => p.equipment?.Primary != null && CanFight(p)).OrderByDescending(Outsiders.Melee).FirstOrDefault() ?? g.FirstOrDefault(CanFight);
                    var c = guard == null ? null : map.mapPawns.FreeColonistsSpawned.Where(o => HarmFour.Ours(o) && Together.Can(o, guard)).FirstOrDefault();
                    if (c == null)
                    {
                        // Nobody of ours near them yet: looked at again next hour.
                        looked.Remove("heir|" + g[0].GetLord().loadID);
                        continue;
                    }
                    // together: Together.Can, the guard and one of ours face to face.
                    HarmFour.Say(guard, "Rimfolk_Pool_HeirGuards", c);
                    bool debug = DebugHeir;
                    DebugHeir = false;
                    if ((debug || Harm.Roll(HarmKinds.HeirGrudge, HarmSevenMath.HeirVisitFight)) && CanFight(c)) Brawl(c, guard, HarmKinds.HeirGrudge);
                }
            }
        }

        // ================= A trader of the hated people (3.8.1) =================

        private static void Hated(Map map, List<List<Pawn>> groups)
        {
            var vs = CouncilGameComponent.Instance?.vendettas?.vendettas;
            if (vs == null || !Harm.SystemOn(HarmKinds.HatedTrader.system)) return;
            foreach (var v in vs)
            {
                if (v == null || !v.Active || v.faction.HostileTo(Faction.OfPlayer) || v.avenger.Map != map || !v.avenger.Spawned) continue;
                foreach (var g in groups)
                {
                    if (g[0].Faction != v.faction || !Look("hated|" + g[0].GetLord().loadID + "|" + v.avenger.thingIDNumber)) continue;
                    var a = v.avenger;
                    bool debug = DebugHated;
                    DebugHated = false;
                    if (!CanFight(a) || !debug && !Harm.Roll(a, HarmKinds.HatedTrader, HarmSevenMath.HatedChance, HurtClass.Fight)) continue;
                    var x = g.Where(CanFight).OrderBy(p => p.Position.DistanceToSquared(a.Position)).FirstOrDefault();
                    if (x == null) continue;
                    Together.When("hated|" + a.thingIDNumber, a, x, () =>
                    {
                        // together: the avenger walked straight up to them (Together.When), face to face.
                        HarmFour.Say(a, "Rimfolk_Pool_HatedTrader", x);
                        if (CanFight(a) && CanFight(x)) Brawl(a, x, HarmKinds.HatedTrader);
                    }, 8f, 0f);
                }
            }
        }

        // ================= Zeal at the gate (3.13.2) =================

        private static void Zeal(Map map, List<List<Pawn>> groups)
        {
            if (!Harm.SystemOn(HarmKinds.GateZeal.system)) return;
            foreach (var g in groups)
            {
                var x = g.FirstOrDefault(p => Faiths.Of(p) != null && CanFight(p));
                if (x == null || !Look("zeal|" + g[0].GetLord().loadID)) continue;
                var c = map.mapPawns.FreeColonistsSpawned.Where(o => HarmFour.Ours(o) && Faiths.Devout(o) && Faiths.Of(o) != Faiths.Of(x) && CanFight(o)
                                                                    && o.CanReach(x, PathEndMode.Touch, Danger.Some))
                    .OrderByDescending(Outsiders.Temper).FirstOrDefault();
                if (c == null) continue;
                bool debug = DebugZeal;
                DebugZeal = false;
                if (!debug && !Harm.Roll(HarmKinds.GateZeal, HarmSevenMath.Zeal(HarmFour.Decree(c, "Council_Decree_Manners")))) continue;
                Together.When("zeal|" + c.thingIDNumber, c, x, () =>
                {
                    // together: the devout came up to the visitor (Together.When), face to face.
                    HarmFour.Say(c, "Rimfolk_Pool_GateZeal", x);
                    if (Rand.Chance(Outsiders.FistfightChance(c, x)) && CanFight(c) && CanFight(x) && Brawl(c, x, HarmKinds.GateZeal)) return;
                    HarmFour.Say(x, "Rimfolk_Pool_GateZealReply", c);
                }, 8f, 0f);
            }
        }

        // ================= Hungry hands in the fields (2.3.2) =================

        /// <summary>A famine (a world event of hunger) near the colony now, and its people; null when there's none.</summary>
        private static WorldSituation Famine(Map map)
        {
            var ws = WorldSituations.Instance;
            if (ws == null) return null;
            foreach (var w in ws.Running)
            {
                if (w?.def == null || !w.def.bad || w.def.kind != "hunger") continue;
                float tiles = TilesTo(w, map.Tile);
                if (HarmTwoMath.Reaches(false, tiles)) return w;
            }
            return null;
        }

        private static float TilesTo(WorldSituation w, PlanetTile home)
        {
            try
            {
                var at = PlanetTile.Invalid;
                var town = Refs.Town(w.place);
                if (town != null) at = town.Tile;
                else at = RiseFall.TileOf(w.place);
                if (!at.Valid || at.Layer != home.Layer) return -1f;
                return Find.WorldGrid.ApproxDistanceInTiles(at, home);
            }
            catch
            {
                return -1f;
            }
        }

        /// <summary>A night watch kept on the walls at this home now (a vigil kept on the move).</summary>
        private static bool Watched(Map map) =>
            PlayOutTracker.Instance?.plays.Any(p => p != null && p.map == map && p.kind == PlayKind.Vigil && p.patrol && p.state == PlayState.Running) ?? false;

        private static void HungryNight(Map map, int now)
        {
            if (!Harm.SystemOn(HarmKinds.HungryHands.system)) return;
            int hour = GenLocalDate.HourOfDay(map);
            bool debug = DebugHungry;
            if (!debug && !HarmSevenMath.HungryHour(hour)) return;
            var famine = Famine(map);
            if (famine == null && !debug) return;
            int night = (now + 6 * HarmMath.HourTicks) / HarmMath.DayTicks;
            if (!Look("hungry|" + map.uniqueID + "|" + night)) return;
            DebugHungry = false;
            var someone = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            bool fed = someone != null && HarmFour.Decree(someone, "Council_Decree_FamineBread");
            var field = Field(map);
            bool lit = field.IsValid && map.glowGrid.GroundGlowAt(field) >= 0.3f;
            if (!debug && !Harm.Roll(HarmKinds.HungryHands, HarmSevenMath.Hungry(fed, Watched(map), lit))) return;
            var f = famine?.ctx?.faction;
            if (f == null || !Arrivals.Usable(f)) f = Find.FactionManager.AllFactionsListForReading.Where(Arrivals.Usable).Where(x => x.def.humanlikeFaction).RandomElementWithFallback();
            var kind = Arrivals.PersonKind(f);
            if (f == null || kind == null) return;
            var people = new List<Pawn>();
            int n = Rand.RangeInclusive(HarmSevenMath.HungryMin, HarmSevenMath.HungryMax);
            try
            {
                for (int i = 0; i < n; i++)
                {
                    var p = Arrivals.Make(kind, f, new FloatRange(18f, 50f));
                    if (p == null) continue;
                    p.equipment?.DestroyAllEquipment();
                    p.inventory?.DestroyAll();
                    people.Add(p);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm seven: hungry strangers", e);
            }
            var spot = field.IsValid ? field : Stores(map);
            if (people.Count == 0 || !spot.IsValid || Arrivals.Visit(people, map, f, spot, 1.5f) == null)
            {
                foreach (var p in people.Where(p => !p.Spawned)) Arrivals.Discard(p);
                return;
            }
            hungry.Add(new Hungry { pawns = people, map = map, until = now + HarmSevenMath.HungryTicks + 2 * HarmMath.HourTicks });
        }

        /// <summary>A cell of the colony's fields with a crop in it, or invalid.</summary>
        private static IntVec3 Field(Map map)
        {
            var z = map.zoneManager.AllZones.OfType<Zone_Growing>().Where(g => g.CellCount > 0).OrderByDescending(g => g.CellCount).FirstOrDefault();
            return z?.Cells.InRandomOrder().FirstOrDefault(c => c.GetPlant(map) != null) ?? z?.Position ?? IntVec3.Invalid;
        }

        private static IntVec3 Stores(Map map)
        {
            var z = map.zoneManager.AllZones.OfType<Zone_Stockpile>().OrderByDescending(s => s.CellCount).FirstOrDefault();
            return z?.Position ?? IntVec3.Invalid;
        }

        /// <summary>Every 250 ticks (the slices): hungry strangers out tonight, met by one of ours or gone by morning.</summary>
        public static void HungrySlice(int now)
        {
            for (int i = hungry.Count - 1; i >= 0; i--)
            {
                var h = hungry[i];
                h.pawns.RemoveAll(p => p == null || p.Dead || !p.Spawned || p.Map != h.map);
                if (h.pawns.Count == 0)
                {
                    hungry.RemoveAt(i);
                    continue;
                }
                if (!h.met)
                    foreach (var x in h.pawns)
                    {
                        var c = h.map.mapPawns.FreeColonistsSpawned.Where(o => HarmFour.Ours(o) && !o.Downed && o.Awake() && o.Position.InHorDistOf(x.Position, HarmSevenMath.HungryMet)
                                                                             && GenSight.LineOfSight(o.Position, x.Position, h.map))
                            .OrderBy(o => o.Position.DistanceToSquared(x.Position)).FirstOrDefault();
                        if (c == null) continue;
                        h.met = true;
                        // A struggle as they're found; then they flee, the sack dropped.
                        if (Rand.Chance(HarmSevenMath.HungryStrike) && Harm.Injure(c, HurtKind.Bruise, Rand.Bool ? HurtWhere.Arm : HurtWhere.Torso, HarmSevenMath.HungryLow,
                                HarmSevenMath.HungryHigh, HarmKinds.HungryHands, HarmFour.Child(c) ? HurtClass.Mild : HurtClass.Injury, x) != null)
                            HarmFour.Say(c, "Rimfolk_Pool_HungryStruck");
                        else HarmFour.Say(c, "Rimfolk_Pool_HungrySeen");
                        HarmFour.Mote(x, "CC_HarmSeven_Mote_SackDropped");
                        Arrivals.SendAway(h.pawns);
                        break;
                    }
                if (!h.met && now >= h.until)
                {
                    Arrivals.SendAway(h.pawns);
                    hungry.RemoveAt(i);
                }
            }
        }

        // ================= A brawl at the creek (2.3.3) =================

        private static void Creek(Map map, int now)
        {
            if (!Harm.SystemOn(HarmKinds.CreekBrawl.system)) return;
            var trips = VisitorTracker.Instance?.trips;
            if (trips == null) return;
            var parties = trips.Where(t => t != null && t.map == map && t.state == VisitState.Here && t.work == "prospect").Select(t => t.Here.Where(CanFight).ToList())
                .Where(l => l.Count > 0).ToList();
            bool debug = DebugCreek;
            if (parties.Count < 2 && !debug) return;
            // Once a rush (its world event), at the first hour two parties are on the land together.
            int rush = trips.FirstOrDefault(t => t != null && t.map == map && t.work == "prospect")?.situationId ?? -1;
            if (!debug && (HarmSeven.WaitingOf(HarmSeven.Creek, w => w.id == rush) != null || !Look("creek|" + rush))) return;
            DebugCreek = false;
            if (!debug) HarmFour.Wait(new HarmWaiting { what = HarmSeven.Creek, kind = HarmKinds.CreekBrawl.key, id = rush, made = now });
            if (parties.Count < 2) return;
            if (!debug && !Harm.Roll(HarmKinds.CreekBrawl, HarmSevenMath.CreekChance)) return;
            var a = parties[0][0];
            var b = parties[1].OrderBy(p => p.Position.DistanceToSquared(a.Position)).First();
            // The two parties' quarrel: shouted between them, then the brawl (theirs: nobody of the colony in it).
            HarmFour.Say(a, "Rimfolk_Pool_CreekQuarrel");
            if (!Harm.Fight(a, b, null)) return;
            HarmFour.Mote(a, "CC_HarmSeven_Mote_Creek");
            var marshal = Marshal(map);
            if (marshal != null && Together.Able(marshal) && marshal.CanReach(a, PathEndMode.Touch, Danger.Some))
            {
                var go = JobMaker.MakeJob(JobDefOf.Goto, a.Position);
                go.locomotionUrgency = LocomotionUrgency.Jog;
                go.expiryInterval = 1500;
                marshal.jobs.StartJob(go, JobCondition.InterruptForced, null, false, true);
            }
            HarmSeven.Later(400, () => CreekCaught(a, b, marshal));
        }

        private static void CreekCaught(Pawn a, Pawn b, Pawn marshal)
        {
            if (a?.Map == null) return;
            var map = a.Map;
            bool marshalThere = marshal != null && marshal.Spawned && marshal.Map == map && marshal.Position.InHorDistOf(a.Position, 6f);
            if (marshalThere)
            {
                // apart: the marshal's shout as they break it up.
                HarmFour.Say(marshal, "Rimfolk_Pool_MarshalBreaksUp");
                foreach (var p in new[] { a, b })
                    if (p?.MentalStateDef == MentalStateDefOf.SocialFighting) p.MentalState.RecoverFromState();
                return;
            }
            var c = map.mapPawns.FreeColonistsSpawned.Where(o => HarmFour.Ours(o) && !o.Downed && o.Position.InHorDistOf(a.Position, HarmSevenMath.CreekCaught)).FirstOrDefault();
            if (!HarmSevenMath.CreekCatches(c != null, false)) return;
            var cls = HarmFour.Child(c) ? HurtClass.Mild : HurtClass.Injury;
            if (Harm.Injure(c, HurtKind.Bruise, Rand.Bool ? HurtWhere.Arm : HurtWhere.Torso, HarmSevenMath.CreekLow, HarmSevenMath.CreekHigh, HarmKinds.CreekBrawl, cls, a) == null) return;
            HarmFour.Say(c, "Rimfolk_Pool_CreekCaught");
            HarmFour.Mote(c, "CC_HarmSeven_Mote_CreekCaught");
        }
    }

    /// <summary>
    /// The gambler's answer that lets anyone play (Council_Evt_Gambler): the evening's games are watched for a cheat
    /// caught and a hot-tempered loser (harm, part 7, 2.2.2); named on the sheet in words.
    /// </summary>
    public class DilemmaEffect_GamblerTurns : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) => GateTroubles.DescribeGamble(sheet);

        public override void Apply(DilemmaContext ctx) => GateTroubles.Gambling(ctx?.stranger);
    }

    // Harm, part 7's fistfights and challenges with outsiders (Harm/GateTroubles.cs), by the outsiders' own rules
    // (Outsiders/OutsiderOutcomes.cs): the same brawls, bouts, duels, witnesses and endings.
    public static partial class Outsiders
    {
        /// <summary>
        /// A fistfight between one of the colony and one of another people: the outsiders' own brawl (their people remember
        /// it, a little goodwill lost, never a war), begun through the harm service with its kind. False when it can't.
        /// </summary>
        public static bool HarmBrawl(Pawn c, Pawn x, HarmKind kind)
        {
            if (c == null || x == null || x.interactions == null || c.interactions == null || Peaceful(x) || Peaceful(c)) return false;
            var bout = new OutsiderBout { a = c, b = x, kind = BoutKind.Brawl, start = Now };
            bouts.Add(bout);
            // together: the callers bring the two face to face first.
            if (Harm.Fight(c, x, kind)) return true;
            bouts.Remove(bout);
            return false;
        }

        /// <summary>Whether the colonist takes a challenge by their nature (a hot or proud temper, or the better fighter), as a rematch is taken.</summary>
        public static bool TakesChallenge(Pawn c, Pawn x) => c != null && x != null && HarmFourMath.TakesRematch(Temper(c), Melee(c), Melee(x));

        /// <summary>The chance hard words come to blows, by the outsiders' own reckoning (the game's insult chance, their tempers, their regard).</summary>
        public static float FistfightChance(Pawn c, Pawn x)
        {
            float game = 0f;
            try
            {
                if (InteractionDefOf.Insult != null && x.interactions != null) game = x.interactions.SocialFightChance(InteractionDefOf.Insult, c);
            }
            catch
            {
                game = 0f;
            }
            return OutsiderMath.FightChance(Temper(x), Temper(c), x.relations?.OpinionOf(c) ?? 0, game);
        }

        /// <summary>
        /// A challenge taken: a duel to first blood where the people's ways know duels and duels are allowed, else a bout;
        /// the colonist's hurt in it is its kind's. The colonist's job starts at once. False when it can't begin.
        /// </summary>
        public static bool Challenge(Pawn c, Pawn x, HarmKind kind)
        {
            if (c?.Map == null || x == null || c.Map != x.Map || Peaceful(c) || Peaceful(x)) return false;
            bool duel = DuelOn && OutsiderMath.DuelFits((int)(x.Faction?.def?.techLevel ?? TechLevel.Industrial), ModsConfig.RoyaltyActive, x.royalty?.MostSeniorTitle != null);
            if (!duel && !SparOn) return false;
            var job = BeginBout(c, x, duel ? BoutKind.Duel : BoutKind.Spar, duel ? DuelTerms.FirstBlood : DuelTerms.Yield);
            var bout = BoutOf(c);
            if (job == null || bout == null) return false;
            bout.after = b =>
            {
                if (b == null || c.Dead || b.off) return;
                foreach (var h in c.health.hediffSet.hediffs)
                    if (h is Hediff_Injury && !b.before.Contains(h))
                    {
                        Harm.Note(c, kind);
                        return;
                    }
            };
            if (duel) CallWitnesses(bout);
            c.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return true;
        }
    }
}
