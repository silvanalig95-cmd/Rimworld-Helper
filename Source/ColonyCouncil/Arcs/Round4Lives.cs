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
    // Content round 4, the colonists' own lives (docs/content-round-4-lives.md; the player's go of 27 November 2026: more
    // content inside the frames there are). Seven personal stories' workers (the eighth, at the healer's side, is the
    // apprentice's own), eight wishes' workers and two ambitions' goals. Each reads what the game and Rimfolk already keep;
    // what a story or a wish needs to remember it keeps in the fields they already save (a story's key and tag, a wish's
    // subjectKey, baseline and targetValue). The rules without game types are in Round4LivesMath.cs. Nobody is hurt.

    /// <summary>
    /// A cache kept for one game only: emptied when another game is begun or loaded, since map and wish ids start again
    /// there and its benches and maps are not this game's.
    /// </summary>
    public sealed class R4LGameCache<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> map = new Dictionary<TKey, TValue>();
        private Game game;

        public Dictionary<TKey, TValue> Now
        {
            get
            {
                var g = Current.Game;
                if (g != game)
                {
                    map.Clear();
                    game = g;
                }
                return map;
            }
        }
    }

    /// <summary>Helpers the round's stories and wishes share.</summary>
    public static class Round4Lives
    {
        public static bool Grown(Pawn p) => p != null && !p.Dead && p.DevelopmentalStage.Adult();

        /// <summary>
        /// The hands-on skills a story of work can be about (Round4LivesMath.HandsOn): building, mining, cooking, growing,
        /// crafting, art, healing, animals and the hunt's shooting; never talk, study or fighting, which read oddly as a trade.
        /// </summary>
        public static bool HandsOn(SkillDef s) => s != null && Round4LivesMath.HandsOn(s.defName);

        public static bool AtHome(Pawn p) => p != null && p.Spawned && p.Map != null && p.Map.IsPlayerHome;

        // ---------------- The seasons ----------------

        private static readonly R4LGameCache<int, float> coldestOf = new R4LGameCache<int, float>();

        /// <summary>The mean of the land's coldest month, from the game's own climate (kept per map).</summary>
        public static float ColdestMonth(Map map)
        {
            if (map == null) return 99f;
            var coldest = coldestOf.Now;
            if (coldest.TryGetValue(map.uniqueID, out float c)) return c;
            c = 99f;
            for (int i = 0; i < 12; i++) c = Mathf.Min(c, GenTemperature.AverageTemperatureAtTileForTwelfth(map.Tile, (Twelfth)i));
            if (coldest.Count > 50) coldest.Clear();
            coldest[map.uniqueID] = c;
            return c;
        }

        public static bool RealWinter(Map map) => map != null && Round4LivesMath.RealWinter(ColdestMonth(map));

        public static bool Autumn(Map map) => map != null && GenLocalDate.Season(map) == Season.Fall;

        public static bool Winter(Map map)
        {
            if (map == null) return false;
            var s = GenLocalDate.Season(map);
            return s == Season.Winter || s == Season.PermanentWinter;
        }

        public static bool Spring(Map map)
        {
            if (map == null) return false;
            var s = GenLocalDate.Season(map);
            return s == Season.Spring || s == Season.Summer || s == Season.PermanentSummer;
        }

        // ---------------- Between two people ----------------

        /// <summary>The last warm moment (or making up) between the two that Rimfolk's moments still remember, or -1.</summary>
        public static int LastWarm(Pawn a, Pawn b)
        {
            var t = CouncilGameComponent.Instance?.social?.tones?.Get(a, b, false);
            return t == null ? -1 : Math.Max(t.warm, t.mend);
        }

        public static bool Rivals(Pawn a, Pawn b) => CouncilGameComponent.Instance?.social?.Get(a, b, TieKind.Rivals)?.announced ?? false;

        public static bool Friends(Pawn a, Pawn b) => CouncilGameComponent.Instance?.social?.Get(a, b, TieKind.Friends)?.announced ?? false;

        /// <summary>The newest memory <paramref name="p"/> has about someone of another people (a talk with a visitor), at most this many days old.</summary>
        public static Thought_Memory TalkWithOutsider(Pawn p, float days, Func<Pawn, bool> ok)
        {
            var memories = p?.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null) return null;
            Thought_Memory best = null;
            foreach (var m in memories)
            {
                var o = m?.otherPawn;
                if (o == null || o.Dead || o.RaceProps == null || !o.RaceProps.Humanlike || o.Faction == null || o.Faction.IsPlayer) continue;
                if (m.age > days * GenDate.TicksPerDay || !ok(o)) continue;
                if (best == null || m.age < best.age) best = m;
            }
            return best;
        }

        /// <summary>Someone of this people on the map, at peace with the colony and not held by it.</summary>
        public static IEnumerable<Pawn> PeopleHere(Map map, Faction f)
        {
            if (map == null || f == null) yield break;
            foreach (var x in map.mapPawns.AllPawnsSpawned)
                if (x != null && x.Faction == f && x.RaceProps.Humanlike && !x.Dead && !x.IsPrisoner && !x.HostileTo(Faction.OfPlayer))
                    yield return x;
        }

        // ---------------- A wish's visit, played out on the map ----------------

        /// <summary>The plan a wish set going (its number in targetValue) took place.</summary>
        public static bool Took(Wish w)
        {
            int id = (int)w.targetValue;
            var res = id > 0 ? PlayOutTracker.Instance?.ResultOf(id) : null;
            return res != null && res.took;
        }

        /// <summary>
        /// The plan a wish set going is over without happening (danger, nobody free), or none could be set going yet: it may
        /// be set going again.
        /// </summary>
        public static bool FellThrough(Wish w)
        {
            int id = (int)w.targetValue;
            var plays = PlayOutTracker.Instance;
            if (plays == null) return false;
            if (id <= 0) return true;
            if (plays.Get(id) != null) return false;
            var res = plays.ResultOf(id);
            return res == null || !res.took;
        }

        /// <summary>
        /// Free to go off on a wish's errand of their own: not held (drafted, in a break, down or on the player's order) and
        /// doing nothing that matters more (tending, a rescue, a meal, sleep: PlayOuts.Interruptible). The lives review.
        /// </summary>
        public static bool Free(Pawn p) => p != null && p.Spawned && !OwnHappenings.Held(p) && PlayOuts.Interruptible(p);

        /// <summary>A short walk to a place, the game's own job (answers not played out on the map).</summary>
        public static bool WalkTo(Pawn p, Thing place)
        {
            if (place == null || !Free(p) || !place.Spawned || p.Map != place.Map) return false;
            if (!p.CanReach(place, PathEndMode.Touch, Danger.Some)) return false;
            var job = JobMaker.MakeJob(JobDefOf.Goto, place.Position);
            return p.jobs != null && p.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        // ---------------- Snow ----------------

        private static readonly R4LGameCache<int, (int hour, int cells)> snowOf = new R4LGameCache<int, (int hour, int cells)>();

        /// <summary>Open ground in the home area under snow deep enough for a snowman (counted once an hour per map).</summary>
        public static int DeepSnow(Map map)
        {
            if (map?.snowGrid == null || !map.IsPlayerHome) return 0;
            int hour = Find.TickManager.TicksGame / GenDate.TicksPerHour;
            var snow = snowOf.Now;
            if (snow.TryGetValue(map.uniqueID, out var k) && k.hour == hour) return k.cells;
            int n = 0;
            foreach (var c in map.areaManager.Home.ActiveCells)
            {
                if (map.snowGrid.GetDepth(c) < PlaySnow.Depth || c.Roofed(map) || !c.Standable(map)) continue;
                if (++n >= Round4LivesMath.SnowCells * 4) break;
            }
            if (snow.Count > 20) snow.Clear();
            snow[map.uniqueID] = (hour, n);
            return n;
        }

        /// <summary>Deep enough snow near them, out in the open, with nothing on it.</summary>
        public static IntVec3 SnowCell(Pawn x)
        {
            var map = x?.Map;
            if (map?.snowGrid == null) return IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(x.Position, 12f, true))
            {
                if (!c.InBounds(map) || c.Roofed(map) || !c.Standable(map) || c.GetEdifice(map) != null || c.GetFirstItem(map) != null || c.Fogged(map)) continue;
                if (map.snowGrid.GetDepth(c) < PlaySnow.Depth || c.IsForbidden(x) || !x.CanReserveAndReach(c, PathEndMode.Touch, Danger.None)) continue;
                return c;
            }
            return IntVec3.Invalid;
        }
    }

    // =====================================================================================================
    // Personal stories
    // =====================================================================================================

    /// <summary>
    /// A trade of their own (Rimfolk_R4L_Arc_TradeOfTheirOwn): a kind of work has fallen to someone grown: it is on for them,
    /// nobody at home who does it is better, and it is no passion of theirs and they are still poor at it. Days of it, a talk
    /// about it, two levels gained, and it is theirs. The key is the work; the other is someone better at the skill who
    /// thinks well of them, else their closest friend (the story only leans on them).
    /// </summary>
    public class R4LArcWorker_Trade : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (!Round4Lives.Grown(p) || p.workSettings == null || p.skills == null) return null;
            WorkTypeDef pick = null;
            SkillRecord pickRec = null;
            foreach (var w in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (w.relevantSkills.NullOrEmpty() || w.defName == "PlantCutting" || !Round4Lives.HandsOn(w.relevantSkills[0]) || p.WorkTypeIsDisabled(w) || !p.workSettings.WorkIsActive(w)) continue;
                var rec = p.skills.GetSkill(w.relevantSkills[0]);
                if (rec == null || rec.TotallyDisabled) continue;
                int better = home.Count(o => o != p && Round4Lives.Grown(o) && o.workSettings != null && !o.WorkTypeIsDisabled(w) && o.workSettings.WorkIsActive(w)
                                             && ArcUtil.Level(o, rec.def) > rec.Level);
                if (!Round4LivesMath.TradeFell(true, People.PassionLevel(rec), rec.Level, better)) continue;
                if (pickRec == null || rec.Level < pickRec.Level || (rec.Level == pickRec.Level && Rand.Bool))
                {
                    pick = w;
                    pickRec = rec;
                }
            }
            if (pick == null) return null;
            var skill = pickRec.def;
            var other = home.Where(o => o != p && Round4Lives.Grown(o) && ArcUtil.Level(o, skill) >= pickRec.Level + 3 && ArcUtil.Opinion(o, p) >= 0)
                            .OrderByDescending(o => ArcUtil.Level(o, skill)).FirstOrDefault()
                        ?? ArcUtil.Closest(p, home.Where(Round4Lives.Grown), 10f);
            return new ArcCandidate { other = other, skill = skill, key = pick.defName, weight = 1f };
        }
    }

    /// <summary>
    /// The wager (Rimfolk_R4L_Arc_Wager): two grown colonists who get on, both good at the same kind of work and both doing
    /// it, bet on who brings in more. The wager is made in a talk; then the work, counted by the game's own record for it
    /// from the moment it was made (kept in the key); at the count the story's tag says how it went: won (the colonist),
    /// lost, or even. The loser raises the cup. A fight between them ends it.
    /// </summary>
    public class R4LArcWorker_Wager : ArcWorker
    {
        private static WorkTypeDef Work(string name) => name == null ? null : DefDatabase<WorkTypeDef>.GetNamedSilentFail(name);

        private static bool Does(Pawn p, WorkTypeDef w) => p.workSettings != null && !p.WorkTypeIsDisabled(w) && p.workSettings.WorkIsActive(w);

        private static float Brought(Pawn p, string work)
        {
            var rec = DefDatabase<RecordDef>.GetNamedSilentFail(Round4LivesMath.WagerRecord(work) ?? "");
            return rec != null && p?.records != null ? p.records.GetValue(rec) : 0f;
        }

        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (!Round4Lives.Grown(p) || ArcUtil.Psychopath(p)) return null;
            var options = new List<(Pawn o, string work, SkillDef skill)>();
            foreach (var name in Round4LivesMath.WagerWorks)
            {
                var w = Work(name);
                var skill = w?.relevantSkills?.FirstOrDefault();
                if (skill == null || Round4LivesMath.WagerRecord(name) == null || !Does(p, w)) continue;
                foreach (var o in home)
                {
                    if (o == p || !Round4Lives.Grown(o) || !Does(o, w) || ArcUtil.Psychopath(o)) continue;
                    if (!Round4LivesMath.WagerPair(ArcUtil.Level(p, skill), ArcUtil.Level(o, skill), ArcUtil.Opinion(p, o), ArcUtil.Opinion(o, p), Round4Lives.Rivals(p, o))) continue;
                    options.Add((o, name, skill));
                }
            }
            if (options.Count == 0) return null;
            var pick = options.RandomElement();
            return new ArcCandidate { other = pick.o, skill = pick.skill, key = pick.work, weight = 1f };
        }

        /// <summary>The wager made: where both stood. The count: who won.</summary>
        public override void Settle(Arc arc)
        {
            if (arc.stage == 0)
            {
                string work = arc.key;
                if (Round4LivesMath.ReadWagerKey(arc.key, out string w, out _, out _)) work = w;
                arc.key = Round4LivesMath.WagerKey(work, Brought(arc.pawn, work), Brought(arc.other, work));
                return;
            }
            if (arc.stage == 1 && Round4LivesMath.ReadWagerKey(arc.key, out string done, out float a, out float b))
                arc.tag = Round4LivesMath.WagerOutcome(Brought(arc.pawn, done) - a, Brought(arc.other, done) - b);
        }

        public override string Broken(Arc arc) => arc.stage < arc.def.LastStage && ArcLives.FoughtSince(arc, arc.pawn, arc.other) ? "fight" : null;
    }

    /// <summary>
    /// The old one's tales (Rimfolk_R4L_Arc_OldTales): one of the colony's elders and a child at home who like each other.
    /// A tale told, time together, and the child asking for it to be told to everyone: an evening by the fire. The story is
    /// the elder's; the child is the other.
    /// </summary>
    public class R4LArcWorker_OldTales : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (!Round4Lives.Grown(p) || ArcUtil.Psychopath(p)) return null;
            float age = Flavor.HumanAge(p);
            if (age < Round4LivesMath.ElderAge) return null;
            var child = home.Where(o => o != p && Round4LivesMath.TalesPair(age, Flavor.HumanAge(o), o.DevelopmentalStage.Child(), ArcUtil.Opinion(p, o), ArcUtil.Opinion(o, p)))
                .OrderByDescending(o => ArcUtil.Opinion(p, o) + ArcUtil.Opinion(o, p) + Rand.Value * 10f).FirstOrDefault();
            return child == null ? null : new ArcCandidate { other = child, weight = 1.2f };
        }
    }

    /// <summary>
    /// The first winter (Rimfolk_R4L_Arc_FirstWinter): autumn on land with a real winter, and someone who came since the last
    /// one. Its first custom gate waits for winter by the map's own calendar, its last for spring. The other is someone who
    /// has wintered here and thinks well of them, else their closest friend (the story only leans on them).
    /// </summary>
    public class R4LArcWorker_FirstWinter : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var map = p?.Map;
            if (!Round4Lives.Grown(p) || !Round4Lives.AtHome(p)) return null;
            if (!Round4LivesMath.FirstWinter(Round4Lives.Autumn(map), Round4Lives.RealWinter(map), ArcUtil.DaysHere(p))) return null;
            var other = home.Where(o => o != p && Round4Lives.Grown(o) && ArcUtil.DaysHere(o) >= Round4LivesMath.WinteredDays && ArcUtil.Opinion(o, p) >= 0)
                            .OrderByDescending(o => ArcUtil.Opinion(o, p)).FirstOrDefault()
                        ?? ArcUtil.Closest(p, home, 10f);
            return new ArcCandidate { other = other, weight = 1.2f };
        }

        /// <summary>The first gate: winter has come. The last: spring.</summary>
        public override bool CustomGate(Arc arc, ArcGate gate)
        {
            var map = arc.pawn?.MapHeld;
            return arc.stage == 0 ? Round4Lives.Winter(map) : Round4Lives.Spring(map);
        }
    }

    /// <summary>
    /// Work friends (Rimfolk_R4L_Arc_WorkFriends): two grown colonists who love the same kind of work, both doing it, who
    /// hardly know each other yet. The work, a talk about it, time side by side, and an evening together. A fight ends it.
    /// </summary>
    public class R4LArcWorker_WorkFriends : ArcWorker
    {
        private static bool Doing(Pawn p, SkillDef s) =>
            p.workSettings != null && DefDatabase<WorkTypeDef>.AllDefsListForReading.Any(w => w.relevantSkills != null && w.relevantSkills.Count > 0 && w.relevantSkills[0] == s
                                                                                          && !p.WorkTypeIsDisabled(w) && p.workSettings.WorkIsActive(w));

        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (!Round4Lives.Grown(p) || p.skills == null || ArcUtil.Psychopath(p)) return null;
            var options = new List<(Pawn o, SkillDef s)>();
            foreach (var rec in p.skills.skills)
            {
                if (rec.TotallyDisabled || !Round4Lives.HandsOn(rec.def) || !People.Passionate(rec) || !Doing(p, rec.def)) continue;
                foreach (var o in home)
                {
                    if (o == p || !Round4Lives.Grown(o) || ArcUtil.Psychopath(o) || !People.Passionate(ArcUtil.Skill(o, rec.def)) || !Doing(o, rec.def)) continue;
                    if (!Round4LivesMath.WorkStrangers(ArcUtil.Opinion(p, o), ArcUtil.Opinion(o, p), Round4Lives.Friends(p, o), ArcUtil.Partners(p, o))) continue;
                    options.Add((o, rec.def));
                }
            }
            if (options.Count == 0) return null;
            var pick = options.RandomElement();
            return new ArcCandidate { other = pick.o, skill = pick.s, weight = 1f };
        }

        public override string Broken(Arc arc) => arc.stage < arc.def.LastStage && ArcLives.FoughtSince(arc, arc.pawn, arc.other) ? "fight" : null;
    }

    /// <summary>
    /// Friends over the hills (Rimfolk_R4L_Arc_OverTheHills): someone grown got on well with a visitor of another people at
    /// peace with the colony (the game's own memory of a talk with them, lately), and the visitors have gone. They wait for
    /// that people to come again ({FACTION}; {NAME} the one they talked with, the key their id), then an hour near them; if
    /// it was the same person, the story's tag is "friend". The people turning hostile ends it ([fight]).
    /// </summary>
    public class R4LArcWorker_OverTheHills : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var map = p?.Map;
            if (!Round4Lives.Grown(p) || !Round4Lives.AtHome(p) || p.relations == null) return null;
            // A visitor gone home: not one held here, and nobody of their people still on the map.
            var m = Round4Lives.TalkWithOutsider(p, Round4LivesMath.MetDays, o =>
                !o.Faction.Hidden && !o.IsPrisonerOfColony && !o.IsSlaveOfColony && o.MapHeld != map
                && Round4LivesMath.GotOnWith(p.relations.OpinionOf(o), 0f, !o.Faction.HostileTo(Faction.OfPlayer), Round4Lives.PeopleHere(map, o.Faction).Any()));
            var friend = m?.otherPawn;
            if (friend == null) return null;
            return new ArcCandidate
            {
                other = ArcUtil.Closest(p, home, 10f),
                name = friend.LabelShort,
                faction = friend.Faction,
                key = friend.ThingID,
                weight = 1f
            };
        }

        /// <summary>The first gate: that people on the map again. The second: an hour near one of them.</summary>
        public override bool CustomGate(Arc arc, ArcGate gate)
        {
            var p = arc.pawn;
            var map = p?.MapHeld;
            if (arc.stage == 0) return Round4Lives.PeopleHere(map, arc.faction).Any();
            if (p == null || !p.Spawned) return false;
            foreach (var x in Round4Lives.PeopleHere(map, arc.faction))
            {
                if (!x.Position.InHorDistOf(p.Position, 10f)) continue;
                if (x.ThingID == arc.key) arc.tag = "friend";
                return true;
            }
            return false;
        }

        public override string Broken(Arc arc)
        {
            if (arc.stage >= arc.def.LastStage) return null;
            if (arc.faction == null || arc.faction.defeated) return "gone";
            return arc.faction.HostileTo(Faction.OfPlayer) ? "fight" : null;
        }
    }

    /// <summary>
    /// A promise in the dark (Rimfolk_R4L_Arc_Promise): someone who holds to a god, with a shrine of it at home, was down and
    /// helpless lately and is up again. They tell someone of the promise they made, keep it with their hands (work at what
    /// they do best), and give thanks at the shrine. {NAME} is the god as they call it; no line says the god did anything.
    /// </summary>
    public class R4LArcWorker_Promise : ArcWorker
    {
        private static readonly string[] Hands = { "Crafting", "Artistic", "Construction", "Cooking", "Plants" };

        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (!Gods.On || !Round4Lives.Grown(p) || !Round4Lives.AtHome(p) || p.Downed || p.InBed()) return null;
            var god = PawnGods.Held(p);
            if (god == null || Shrines.Nearest(p, god) == null) return null;
            int downed = CouncilGameComponent.Instance?.aspirations.When(p, "downed") ?? -1;
            if (!Round4LivesMath.Spared(downed, Find.TickManager.TicksGame, GenDate.TicksPerDay, !p.Downed)) return null;
            var skill = ArcUtil.Best(p, Hands.Select(n => DefDatabase<SkillDef>.GetNamedSilentFail(n)).Where(s => s != null), 2) ?? ArcUtil.Best(p, ArcUtil.AllSkills, 0);
            if (skill == null) return null;
            return new ArcCandidate { other = ArcUtil.Closest(p, home, 10f), skill = skill, name = Worship.Called(god, p), weight = 1.3f };
        }

        /// <summary>Their shrine is gone before the thanks.</summary>
        public override string Broken(Arc arc)
        {
            if (arc.stage >= arc.def.LastStage || arc.pawn == null || !arc.pawn.Spawned) return null;
            var god = PawnGods.Held(arc.pawn);
            return god == null || Shrines.Nearest(arc.pawn, god) == null ? "gone" : null;
        }
    }

    // =====================================================================================================
    // Wishes
    // =====================================================================================================

    /// <summary>
    /// A wish whose promise sets a visit going on the map (a plan of the play-out tracker, its number kept in targetValue):
    /// met when it took place. If it fell through (danger, nobody free), a promised one is set going again, at most twice a
    /// day. With answers not played out on the map, <see cref="Instead"/> does what can be done without a plan.
    /// </summary>
    public abstract class R4LWishWorker_Visit : WishWorker
    {
        private static readonly R4LGameCache<int, int> lastArrangedOf = new R4LGameCache<int, int>();

        /// <summary>The visit (or gathering), set going now; null when it can't be.</summary>
        protected abstract PlayOut Plan(Wish w);

        /// <summary>Answers not played out: what is done instead (a walk there, the two when they meet); the message key, or null.</summary>
        protected abstract string Instead(Wish w);

        protected abstract string PromiseKey { get; }

        /// <summary>Met some other way (a talk had anyway).</summary>
        protected virtual bool MetOtherwise(Wish w) => false;

        public override string OnPromised(Wish w) => Arrange(w, true);

        public override string TrySelfHelp(Wish w) => null;

        private string Arrange(Wish w, bool promising)
        {
            if (w == null || w.baseline >= 1f) return null;
            int now = Find.TickManager.TicksGame;
            var lastArranged = lastArrangedOf.Now;
            if (!promising && lastArranged.TryGetValue(w.id, out int last) && now - last < GenDate.TicksPerDay / 2) return null;
            lastArranged[w.id] = now;
            if (lastArranged.Count > 100) lastArranged.Clear();
            try
            {
                if (!PlayOuts.On) return promising ? Instead(w) : null;
                var play = Plan(w);
                if (play == null) return null;
                w.targetValue = play.id;
                return promising ? PromiseKey : null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4 wish " + def?.defName, e);
                return null;
            }
        }

        public override bool IsFulfilled(Wish w)
        {
            if (w.baseline >= 1f) return true;
            if (Round4Lives.Took(w) || MetOtherwise(w))
            {
                w.baseline = 1f;
                return true;
            }
            if (w.promised && PlayOuts.On && Round4Lives.FellThrough(w)) Arrange(w, false);
            return false;
        }
    }

    /// <summary>
    /// Something new at the bench (Rimfolk_R4L_Wish_NewMake): a crafter or a cook (a passion for it) could make, at one of
    /// the colony's benches, something the colony has none of: the recipe known, their skill enough, the stuff for it in
    /// store. Promised (or done by themselves), a bill for one at that bench, for them. Met once one exists in the colony.
    /// Any recipe of any of the colony's benches counts, the game's or a mod's. subjectKey: the recipe.
    /// </summary>
    public class R4LWishWorker_NewMake : WishWorker
    {
        private static readonly R4LGameCache<int, (int hour, List<(Building_WorkTable bench, RecipeDef recipe)> list)> knownOf =
            new R4LGameCache<int, (int hour, List<(Building_WorkTable bench, RecipeDef recipe)> list)>();

        private static bool Kind(ThingDef d) =>
            d != null && !d.IsDrug && !d.IsMedicine && !d.IsStuff && d.category == ThingCategory.Item
            && (d.IsWeapon || d.IsApparel || (d.ingestible != null && d.ingestible.IsMeal) || d.stackLimit == 1);

        /// <summary>Has the colony one: lying about, worn or carried by its people.</summary>
        public static bool Have(Map map, ThingDef d)
        {
            if (map == null || d == null) return false;
            if (map.listerThings.ThingsOfDef(d).Count > 0) return true;
            foreach (var c in map.mapPawns.FreeColonistsSpawned)
            {
                if (c.equipment?.Primary?.def == d) return true;
                if (c.apparel != null && c.apparel.WornApparel.Any(a => a.def == d)) return true;
                if (c.inventory?.innerContainer != null && c.inventory.innerContainer.Any(t => t.def == d)) return true;
            }
            return false;
        }

        /// <summary>The stuff for one in store.</summary>
        private static bool Stocked(RecipeDef r, Dictionary<ThingDef, int> stock)
        {
            foreach (var ing in r.ingredients ?? new List<IngredientCount>())
            {
                if (ing?.filter == null) continue;
                bool any = false;
                foreach (var d in ing.filter.AllowedThingDefs)
                {
                    if (d == null || !r.IsIngredient(d) || !stock.TryGetValue(d, out int n)) continue;
                    if (n >= ing.CountRequiredOfFor(d, r, null))
                    {
                        any = true;
                        break;
                    }
                }
                if (!any) return false;
            }
            return true;
        }

        /// <summary>What could be made new at this home's benches now (worked out once an hour).</summary>
        private static List<(Building_WorkTable bench, RecipeDef recipe)> Options(Map map)
        {
            int hour = Find.TickManager.TicksGame / GenDate.TicksPerHour;
            var known = knownOf.Now;
            if (known.TryGetValue(map.uniqueID, out var k) && k.hour == hour) return k.list;
            var list = new List<(Building_WorkTable, RecipeDef)>();
            var stock = Making.Stock(map);
            var seen = new HashSet<ThingDef>();
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (!(b is Building_WorkTable bench) || !bench.Spawned || bench.def.AllRecipes == null || !bench.CurrentlyUsableForBills()) continue;
                foreach (var r in bench.def.AllRecipes)
                {
                    var d = r?.ProducedThingDef;
                    if (d == null || !r.AvailableNow || (r.workSkill != SkillDefOf.Crafting && r.workSkill != SkillDefOf.Cooking) || !Kind(d) || seen.Contains(d)) continue;
                    if (Have(map, d) || !Stocked(r, stock)) continue;
                    seen.Add(d);
                    list.Add((bench, r));
                }
            }
            if (known.Count > 20) known.Clear();
            known[map.uniqueID] = (hour, list);
            return list;
        }

        private static RecipeDef Recipe(Wish w) => DefDatabase<RecipeDef>.GetNamedSilentFail(w.subjectKey ?? "");

        /// <summary>A bench of the colony's that makes it; with <paramref name="usable"/>, one that can take bills now (powered, fuelled).</summary>
        private static Building_WorkTable Bench(Map map, RecipeDef r, bool usable = true) =>
            map == null || r == null ? null
                : map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().FirstOrDefault(b => b.Spawned && b.def.AllRecipes != null && b.def.AllRecipes.Contains(r) && (!usable || b.CurrentlyUsableForBills()));

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !map.IsPlayerHome || !Round4Lives.Grown(pawn) || pawn.skills == null) yield break;
            var options = Options(map).Where(o => o.recipe.workSkill != null && People.Passionate(pawn.skills.GetSkill(o.recipe.workSkill))
                                                  && !pawn.skills.GetSkill(o.recipe.workSkill).TotallyDisabled && o.recipe.PawnSatisfiesSkillRequirements(pawn)).ToList();
            if (options.Count == 0) yield break;
            var pick = options.RandomElement();
            var d = pick.recipe.ProducedThingDef;
            var c = Candidate(1f, "CC_R4L_Reason_NewMake".Translate(pawn.Named("PAWN"), d.label.Named("THING"), pick.bench.LabelNoCount.Named("BENCH")).Resolve());
            c.thingDef = d;
            c.subjectKey = pick.recipe.defName;
            c.subjectLabel = d.label;
            c.baseline = Made(pawn);
            yield return c;
        }

        /// <summary>What they have made with their hands, things and meals (the game's own records).</summary>
        private static float Made(Pawn p) => p?.records == null ? 0f : p.records.GetValue(RecordDefOf.ThingsCrafted) + p.records.GetValue(RecordDefOf.MealsCooked);

        /// <summary>
        /// The colony has one, and they have made something since they asked, as the round meant it (the lives review: a
        /// trader's or a raider's left behind was thanked for as made).
        /// </summary>
        public override bool IsFulfilled(Wish wish) => Have(wish.Beneficiary?.MapHeld, wish.thingDef) && Made(wish.Beneficiary) > wish.baseline;

        public override bool StillValid(Wish wish) => Recipe(wish) != null && Bench(wish.Beneficiary?.MapHeld, Recipe(wish), false) != null;

        public override string OnPromised(Wish wish) => Order(wish) ? "CC_R4L_Promise_NewMake" : null;

        public override string TrySelfHelp(Wish wish) => Order(wish) ? "CC_R4L_SelfHelp_NewMake" : null;

        public override LookTargets LookAt(Wish wish)
        {
            var bench = Bench(wish.Beneficiary?.MapHeld, Recipe(wish), false);
            return bench != null ? new LookTargets(bench) : base.LookAt(wish);
        }

        /// <summary>A bill for one at the bench, first in its list, for the one who wished it when they can do it.</summary>
        private static bool Order(Wish wish)
        {
            var r = Recipe(wish);
            var p = wish.Beneficiary;
            var bench = Bench(p?.MapHeld, r);
            if (r == null || bench == null) return false;
            if (bench.BillStack.Bills.Any(b => b.recipe == r)) return true;
            try
            {
                if (!(r.MakeNewBill() is Bill_Production bill)) return false;
                bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                bill.repeatCount = 1;
                if (p != null && !p.Dead && p.IsColonist && r.PawnSatisfiesSkillRequirements(p)) bill.SetPawnRestriction(p);
                VarietyThree.First(bench, bill);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 4: something new at the bench", e);
                return false;
            }
        }
    }

    /// <summary>
    /// An evening with an old friend (Rimfolk_R4L_Wish_OldFriend): two friends (a friendship tie) who haven't had a warm
    /// moment together in four days, both at home. Promised, the one who asked walks over in the evening and the two sit
    /// down together; with answers not played out, the next time the two are together.
    /// </summary>
    public class R4LWishWorker_OldFriend : R4LWishWorker_Visit
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var social = CouncilGameComponent.Instance?.social;
            if (social == null || !ColonyCouncilMod.Settings.socialWeb || !Round4Lives.Grown(pawn) || !Round4Lives.AtHome(pawn)) yield break;
            int now = Find.TickManager.TicksGame;
            Pawn best = null;
            float bestTie = 0f;
            foreach (var t in social.ties)
            {
                if (t.kind != TieKind.Friends || !t.announced || t.strength < Round4LivesMath.OldFriendTie || !t.Involves(pawn)) continue;
                var o = t.Other(pawn);
                if (!Round4Lives.Grown(o) || !o.Spawned || o.Map != pawn.Map || !o.IsColonist || ArcUtil.Partners(pawn, o)) continue;
                if (!Round4LivesMath.GoneQuiet(Round4Lives.LastWarm(pawn, o), now, GenDate.TicksPerDay) || t.strength <= bestTie) continue;
                best = o;
                bestTie = t.strength;
            }
            if (best == null) yield break;
            var c = Candidate(0.9f, "CC_R4L_Reason_OldFriend".Translate(pawn.Named("PAWN"), best.Named("OTHER")).Resolve());
            c.other = best;
            yield return c;
        }

        public override bool StillValid(Wish w)
        {
            var o = w.other;
            return o != null && !o.Dead && o.IsColonist && o.MapHeld != null && o.MapHeld == w.Beneficiary?.MapHeld;
        }

        public override LookTargets LookAt(Wish w) => w.other != null && w.other.Spawned ? new LookTargets(new[] { w.Beneficiary, w.other }) : base.LookAt(w);

        protected override string PromiseKey => "CC_R4L_Promise_OldFriend";

        protected override PlayOut Plan(Wish w)
        {
            var ctx = new DilemmaContext { map = w.Beneficiary?.MapHeld, a = w.Beneficiary, b = w.other };
            var e = new DilemmaEffect_Visit
            {
                label = "CC_R4L_Play_OldFriend".Translate().Resolve(),
                who = PlayWho.A,
                to = PlaySpot.Person,
                whom = PlayWho.B,
                sit = true,
                hours = new IntRange(17, 22),
                duration = new FloatRange(2f, 2.5f)
            };
            return e.Begin(ctx);
        }

        protected override string Instead(Wish w)
        {
            // together: it happens when the two are together, or once one walks over to find the other.
            Together.When("r4l|oldfriend|" + w.id, w.Beneficiary, w.other, () =>
            {
                if (w.state == WishState.Open) w.baseline = 1f;
            }, 48f, 4f);
            return "CC_R4L_Promise_OldFriendTogether";
        }
    }

    /// <summary>
    /// Someone to read to me (Rimfolk_R4L_Wish_ReadToMe): one of the old whose eyes have gone poor (the game's sight), with
    /// books in the colony and someone grown who thinks well of them and can read. Promised, the reader comes in the evening
    /// and sits with them; with answers not played out, the next time the two are together.
    /// </summary>
    public class R4LWishWorker_ReadToMe : R4LWishWorker_Visit
    {
        private static float Sight(Pawn p) => p?.health?.capacities != null ? p.health.capacities.GetLevel(PawnCapacityDefOf.Sight) : 1f;

        private static bool CanRead(Pawn o) =>
            Round4Lives.Grown(o) && Sight(o) > Round4LivesMath.PoorSight && o.skills?.GetSkill(SkillDefOf.Intellectual) is SkillRecord r && !r.TotallyDisabled;

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !Round4Lives.AtHome(pawn) || !Round4LivesMath.WantsReading(Flavor.HumanAge(pawn), Sight(pawn))) yield break;
            if (map.listerThings.ThingsInGroup(ThingRequestGroup.Book).Count == 0) yield break;
            var reader = map.mapPawns.FreeColonistsSpawned.Where(o => o != pawn && CanRead(o) && ArcUtil.Opinion(o, pawn) >= Round4LivesMath.ReaderOpinion)
                .OrderByDescending(o => ArcUtil.Opinion(o, pawn) + ArcUtil.Opinion(pawn, o)).FirstOrDefault();
            if (reader == null) yield break;
            var c = Candidate(1f, "CC_R4L_Reason_ReadToMe".Translate(pawn.Named("PAWN"), reader.Named("OTHER")).Resolve());
            c.other = reader;
            yield return c;
        }

        public override bool StillValid(Wish w)
        {
            var o = w.other;
            return o != null && !o.Dead && o.IsColonist && o.MapHeld != null && o.MapHeld == w.Beneficiary?.MapHeld;
        }

        public override LookTargets LookAt(Wish w) => w.other != null && w.other.Spawned ? new LookTargets(new[] { w.Beneficiary, w.other }) : base.LookAt(w);

        protected override string PromiseKey => "CC_R4L_Promise_ReadToMe";

        protected override PlayOut Plan(Wish w)
        {
            // The reader (B) goes to the one who asked (A).
            var ctx = new DilemmaContext { map = w.Beneficiary?.MapHeld, a = w.Beneficiary, b = w.other };
            var e = new DilemmaEffect_Visit
            {
                label = "CC_R4L_Play_ReadToMe".Translate(w.Beneficiary.Named("PAWN")).Resolve(),
                who = PlayWho.B,
                to = PlaySpot.Person,
                whom = PlayWho.A,
                sit = true,
                hours = new IntRange(17, 22),
                duration = new FloatRange(1.5f, 2f)
            };
            return e.Begin(ctx);
        }

        protected override string Instead(Wish w)
        {
            // together: the reading happens when the two are together, or once the reader walks over to find them.
            Together.When("r4l|readtome|" + w.id, w.other, w.Beneficiary, () =>
            {
                if (w.state == WishState.Open) w.baseline = 1f;
            }, 48f, 4f);
            return "CC_R4L_Promise_ReadToMeTogether";
        }
    }

    /// <summary>
    /// The harvest in before the frost (Rimfolk_R4L_Wish_FrostHarvest): autumn on land with a real winter, and twelve or more
    /// ripe crops standing in the colony's fields, not yet marked. Promised (or done by themselves), each is marked for the
    /// game's own harvest. Met when nine in ten are in. subjectKey: the cells; targetValue: how many.
    /// </summary>
    public class R4LWishWorker_FrostHarvest : WishWorker
    {
        private static bool Ripe(Plant p) =>
            p != null && !p.Destroyed && p.def.plant != null && !p.def.plant.IsTree && p.def.plant.Harvestable && p.HarvestableNow && p.LifeStage == PlantLifeStage.Mature;

        private static List<IntVec3> Cells(Wish w) => LifeNewMath.Decode(w.subjectKey).Select(c => new IntVec3(c.x, 0, c.z)).ToList();

        private static IEnumerable<Plant> StillRipe(Wish w)
        {
            var map = w.Beneficiary?.MapHeld;
            if (map == null) yield break;
            foreach (var c in Cells(w))
                if (c.InBounds(map) && c.GetPlant(map) is Plant p && Ripe(p))
                    yield return p;
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !map.IsPlayerHome || !Round4Lives.Grown(pawn) || !Round4Lives.Autumn(map) || !Round4Lives.RealWinter(map)) yield break;
            var cells = new List<(int, int)>();
            var dm = map.designationManager;
            foreach (var z in map.zoneManager.AllZones.OfType<Zone_Growing>())
                foreach (var c in z.Cells)
                    if (c.GetPlant(map) is Plant p && Ripe(p) && dm.DesignationOn(p, DesignationDefOf.HarvestPlant) == null)
                        cells.Add((c.x, c.z));
            if (!Round4LivesMath.FrostHarvestWorth(true, true, cells.Count)) yield break;
            float weight = People.Passionate(pawn.skills?.GetSkill(SkillDefOf.Plants)) ? 1.3f : 0.9f;
            var w = Candidate(weight, "CC_R4L_Reason_FrostHarvest".Translate(pawn.Named("PAWN"), cells.Count.Named("COUNT")).Resolve());
            w.subjectKey = LifeNewMath.Encode(cells);
            w.targetValue = cells.Count;
            w.baseline = Harvested(map);
            yield return w;
        }

        /// <summary>
        /// The harvests the colony's people have brought in (the game's own record), kept in baseline when it is asked: what
        /// tells a field brought in from one the frost, a blight or a fire took (the lives review).
        /// </summary>
        private static float Harvested(Map map) => map == null ? 0f : map.mapPawns.FreeColonists.Sum(p => p.records?.GetValue(RecordDefOf.PlantsHarvested) ?? 0f);

        /// <summary>Brought in by the colony's hands: half as many harvests since it was asked as the plants it named, at least.</summary>
        private static bool BroughtIn(Wish wish) => Harvested(wish.Beneficiary?.MapHeld) - wish.baseline >= Cells(wish).Count / 2f;

        public override bool IsFulfilled(Wish wish) => Round4LivesMath.HarvestIn(StillRipe(wish).Count(), Cells(wish).Count) && BroughtIn(wish);

        /// <summary>
        /// The frost came: what still stands is the cold's now, not the colony's to bring in. And the crops gone with nobody
        /// bringing them in (the frost, a blight, a fire took them): let go without blame, never thanked for.
        /// </summary>
        public override bool StillValid(Wish wish)
        {
            var map = wish.Beneficiary?.MapHeld;
            if (Cells(wish).Count == 0 || map == null || (Round4Lives.Winter(map) && map.mapTemperature.OutdoorTemp < -5f)) return false;
            return !Round4LivesMath.HarvestIn(StillRipe(wish).Count(), Cells(wish).Count) || BroughtIn(wish);
        }

        public override float Progress(Wish wish) => wish.targetValue <= 0f ? -1f : 1f - StillRipe(wish).Count() / wish.targetValue;

        public override string ProgressText(Wish wish) => "CC_R4L_Wish_FrostHarvest_Progress".Translate(StillRipe(wish).Count()).Resolve();

        public override LookTargets LookAt(Wish wish)
        {
            var p = StillRipe(wish).FirstOrDefault();
            return p != null ? new LookTargets(p) : base.LookAt(wish);
        }

        public override string OnPromised(Wish wish) => Mark(wish) ? "CC_R4L_Promise_FrostHarvest" : null;

        public override string TrySelfHelp(Wish wish) => Mark(wish) ? "CC_R4L_SelfHelp_FrostHarvest" : null;

        /// <summary>Each ripe plant still standing marked for the game's own harvest.</summary>
        private static bool Mark(Wish wish)
        {
            int marked = 0;
            foreach (var p in StillRipe(wish).ToList())
            {
                var dm = p.Map.designationManager;
                if (dm.DesignationOn(p, DesignationDefOf.HarvestPlant) == null)
                {
                    dm.RemoveAllDesignationsOn(p);
                    dm.AddDesignation(new Designation(p, DesignationDefOf.HarvestPlant));
                }
                marked++;
            }
            return marked > 0;
        }
    }

    /// <summary>
    /// A snow day (Rimfolk_R4L_Wish_SnowDay): a child at home, deep snow on the open ground, not bitter cold. Promised, a
    /// gathering out in the open by day with whoever is free, and the child builds a snowman there (the game's own job,
    /// PlaySnow); with answers not played out, the child goes out and builds one.
    /// </summary>
    public class R4LWishWorker_SnowDay : R4LWishWorker_Visit
    {
        private static JobDef build;
        private static JobDef Build => build ??= DefDatabase<JobDef>.GetNamedSilentFail("BuildSnowman");

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !Round4Lives.AtHome(pawn) || !pawn.DevelopmentalStage.Child() || Build == null) yield break;
            if (!Round4LivesMath.SnowDay(Round4Lives.DeepSnow(map), map.mapTemperature.OutdoorTemp)) yield break;
            yield return Candidate(1.2f, "CC_R4L_Reason_SnowDay".Translate(pawn.Named("PAWN")).Resolve());
        }

        public override bool StillValid(Wish w)
        {
            var map = w.Beneficiary?.MapHeld;
            return map != null && (w.promised || Round4Lives.DeepSnow(map) >= Round4LivesMath.SnowCells / 2);
        }

        protected override string PromiseKey => "CC_R4L_Promise_SnowDay";

        protected override PlayOut Plan(Wish w)
        {
            var child = w.Beneficiary;
            var ctx = new DilemmaContext { map = child?.MapHeld, a = child };
            var e = new DilemmaEffect_Gathering
            {
                label = "CC_R4L_Play_SnowDay".Translate().Resolve(),
                target = DilemmaTarget.Colony,
                organizer = PlayWho.A,
                spots = new List<PlaySpot> { PlaySpot.Outdoors },
                hours = new IntRange(9, 16),
                duration = new FloatRange(2f, 2.5f)
            };
            var play = e.Begin(ctx);
            if (play != null) play.snowman = child;
            return play;
        }

        protected override string Instead(Wish w)
        {
            var child = w.Beneficiary;
            if (!Round4Lives.Free(child) || Build == null) return null;
            var cell = Round4Lives.SnowCell(child);
            if (!cell.IsValid) return null;
            // apart: the child goes out alone to build it, with the game's own job.
            if (child.jobs == null || !child.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Build, cell), JobTag.Misc)) return null;
            w.baseline = 1f;
            return "CC_R4L_Promise_SnowDayAlone";
        }
    }

    /// <summary>
    /// A word at the grave (Rimfolk_R4L_Wish_GraveWord): someone of the colony died in the last fifteen days and lies in a
    /// grave here, and a grown colonist thought well of them. Promised, they walk to the grave and stand there a while, a
    /// word said over it; with answers not played out, they walk there. other: the dead.
    /// </summary>
    public class R4LWishWorker_GraveWord : R4LWishWorker_Visit
    {
        public static Building_Grave GraveOf(Map map, Pawn dead) =>
            map == null || dead == null ? null : map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>().FirstOrDefault(g => g.Spawned && g.Corpse?.InnerPawn == dead);

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !Round4Lives.AtHome(pawn) || !Round4Lives.Grown(pawn) || pawn.relations == null) yield break;
            int now = Find.TickManager.TicksGame;
            Pawn best = null;
            int bestOpinion = int.MinValue;
            foreach (var g in map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>())
            {
                var corpse = g.Spawned ? g.Corpse : null;
                var dead = corpse?.InnerPawn;
                if (dead == null || dead == pawn || !dead.RaceProps.Humanlike || dead.Faction != Faction.OfPlayer) continue;
                float days = (now - corpse.timeOfDeath) / (float)GenDate.TicksPerDay;
                int opinion = pawn.relations.OpinionOf(dead);
                if (!Round4LivesMath.GraveWord(days, opinion) || opinion <= bestOpinion) continue;
                best = dead;
                bestOpinion = opinion;
            }
            if (best == null) yield break;
            var c = Candidate(1f, "CC_R4L_Reason_GraveWord".Translate(pawn.Named("PAWN"), best.LabelShort.Named("DEAD")).Resolve());
            c.other = best;
            c.subjectLabel = best.LabelShort;
            yield return c;
        }

        public override bool StillValid(Wish w) => GraveOf(w.Beneficiary?.MapHeld, w.other) != null;

        public override LookTargets LookAt(Wish w)
        {
            var g = GraveOf(w.Beneficiary?.MapHeld, w.other);
            return g != null ? new LookTargets(g) : base.LookAt(w);
        }

        protected override string PromiseKey => "CC_R4L_Promise_GraveWord";

        protected override PlayOut Plan(Wish w)
        {
            var map = w.Beneficiary?.MapHeld;
            var grave = GraveOf(map, w.other);
            if (grave == null) return null;
            var ctx = new HappeningContext { map = map, a = w.Beneficiary, place = grave };
            var e = new DilemmaEffect_Visit
            {
                label = "CC_R4L_Play_GraveWord".Translate((w.subjectLabel ?? "").Named("DEAD")).Resolve(),
                who = PlayWho.A,
                to = PlaySpot.Grave,
                hours = new IntRange(8, 19),
                duration = new FloatRange(0.75f, 1.25f)
            };
            return e.Begin(ctx);
        }

        protected override string Instead(Wish w)
        {
            if (!Round4Lives.WalkTo(w.Beneficiary, GraveOf(w.Beneficiary?.MapHeld, w.other))) return null;
            w.baseline = 1f;
            return "CC_R4L_Promise_GraveWordWalk";
        }
    }

    /// <summary>
    /// A word with the visitors (Rimfolk_R4L_Wish_Visitors): visitors of another people at peace with the colony are on the
    /// map (a visit, traders, a caravan; not a camp, which has its own evenings), and a sociable or curious colonist hasn't
    /// spoken with them. Promised, they walk over to one of them and spend an hour with them. Met also when they talked with
    /// someone of that people anyway. other: the visitor.
    /// </summary>
    public class R4LWishWorker_Visitors : R4LWishWorker_Visit
    {
        private static bool Visitor(Pawn x) =>
            KeepOut.IsVisitor(x) && x.RaceProps.Humanlike && !x.IsQuestLodger() && x.GetLord() != null && CampTracker.CampOf(x) == null && !x.Faction.Hidden;

        private static bool Sociable(Pawn p) =>
            People.Passionate(p.skills?.GetSkill(SkillDefOf.Social)) || ArcUtil.Level(p, SkillDefOf.Social) >= 8 || ArcUtil.HasTrait(p, TraitDefOf.Kind)
            || ArcUtil.Feeling(p, "Curiosity") != null || ArcUtil.Feeling(p, "Restless") != null;

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !Round4Lives.AtHome(pawn) || !Round4Lives.Grown(pawn) || !Sociable(pawn)) yield break;
            var visitors = map.mapPawns.AllPawnsSpawned.Where(Visitor).ToList();
            if (visitors.Count == 0) yield break;
            var f = visitors[0].Faction;
            // Not if they've spoken with that people already this visit.
            if (Round4Lives.TalkWithOutsider(pawn, 2f, o => o.Faction == f) != null) yield break;
            var x = visitors.Where(v => v.Faction == f).RandomElement();
            var c = Candidate(0.9f, "CC_R4L_Reason_Visitors".Translate(pawn.Named("PAWN"), f.Name.Named("FACTION")).Resolve());
            c.other = x;
            c.subjectLabel = f.Name;
            yield return c;
        }

        public override bool StillValid(Wish w)
        {
            var x = w.other;
            return x != null && !x.Dead && x.Spawned && x.Map == w.Beneficiary?.MapHeld && !x.HostileTo(Faction.OfPlayer);
        }

        public override LookTargets LookAt(Wish w) => w.other != null && w.other.Spawned ? new LookTargets(new[] { w.Beneficiary, w.other }) : base.LookAt(w);

        protected override bool MetOtherwise(Wish w)
        {
            var f = w.other?.Faction;
            int since = Find.TickManager.TicksGame - w.createdTick;
            return f != null && Round4Lives.TalkWithOutsider(w.Beneficiary, since / (float)GenDate.TicksPerDay, o => o.Faction == f) != null;
        }

        protected override string PromiseKey => "CC_R4L_Promise_Visitors";

        protected override PlayOut Plan(Wish w)
        {
            var ctx = new DilemmaContext { map = w.Beneficiary?.MapHeld, a = w.Beneficiary, stranger = w.other };
            var e = new DilemmaEffect_Visit
            {
                label = "CC_R4L_Play_Visitors".Translate((w.subjectLabel ?? "").Named("FACTION")).Resolve(),
                who = PlayWho.A,
                to = PlaySpot.Stranger,
                hours = new IntRange(7, 21),
                duration = new FloatRange(1f, 1.5f)
            };
            return e.Begin(ctx);
        }

        protected override string Instead(Wish w)
        {
            // apart: they go over to where the visitor is; the talk itself is the game's own, when the two are together.
            return Round4Lives.WalkTo(w.Beneficiary, w.other) ? "CC_R4L_Promise_VisitorsWalk" : null;
        }
    }

    /// <summary>
    /// An hour at the shrine (Rimfolk_R4L_Wish_ShrineHour): someone who holds to a god, with a shrine of it at home, is
    /// having a hard time (low mood, strain, or a loss lately). Promised, they walk there and stay a while; with answers not
    /// played out, they walk there. {SUBJECT}: the god as they call it; nothing is said of what the god thinks.
    /// </summary>
    public class R4LWishWorker_ShrineHour : R4LWishWorker_Visit
    {
        private static Thing Shrine(Pawn p)
        {
            var god = p != null && Gods.On ? PawnGods.Held(p) : null;
            return god == null ? null : Shrines.Nearest(p, god);
        }

        private static float DaysSinceLoss(Pawn p)
        {
            var lost = CouncilGameComponent.Instance?.social?.lost;
            if (lost == null) return -1f;
            int now = Find.TickManager.TicksGame;
            var l = lost.Where(x => x != null && x.survivor == p && x.kind != TieKind.Rivals).OrderByDescending(x => x.tick).FirstOrDefault();
            return l == null ? -1f : (now - l.tick) / (float)GenDate.TicksPerDay;
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            if (!Gods.On || !Round4Lives.Grown(pawn) || !Round4Lives.AtHome(pawn)) yield break;
            var god = PawnGods.Held(pawn);
            if (god == null || Shrines.Nearest(pawn, god) == null) yield break;
            float mood = pawn.needs?.mood?.CurLevelPercentage ?? -1f;
            float strain = Heart.On ? Heart.StrainOf(pawn) : 0f;
            if (!Round4LivesMath.HardTime(mood, strain, DaysSinceLoss(pawn))) yield break;
            string called = Worship.Called(god, pawn);
            var c = Candidate(0.8f, "CC_R4L_Reason_ShrineHour".Translate(pawn.Named("PAWN"), called.Named("GOD")).Resolve());
            c.subjectLabel = called;
            yield return c;
        }

        public override bool StillValid(Wish w) => Shrine(w.Beneficiary) != null;

        public override LookTargets LookAt(Wish w)
        {
            var s = Shrine(w.Beneficiary);
            return s != null ? new LookTargets(s) : base.LookAt(w);
        }

        protected override string PromiseKey => "CC_R4L_Promise_ShrineHour";

        protected override PlayOut Plan(Wish w)
        {
            var ctx = new DilemmaContext { map = w.Beneficiary?.MapHeld, a = w.Beneficiary };
            var e = new DilemmaEffect_ShrineThanks
            {
                label = "CC_R4L_Play_ShrineHour".Translate(w.Beneficiary.Named("PAWN")).Resolve(),
                who = PlayWho.A,
                hours = new IntRange(6, 21),
                duration = new FloatRange(1f, 1.5f)
            };
            return e.Begin(ctx);
        }

        protected override string Instead(Wish w)
        {
            if (!Round4Lives.WalkTo(w.Beneficiary, Shrine(w.Beneficiary))) return null;
            w.baseline = 1f;
            return "CC_R4L_Promise_ShrineHourWalk";
        }
    }

    // =====================================================================================================
    // Ambitions
    // =====================================================================================================

    /// <summary>
    /// A good old age (Rimfolk_R4L_Amb_GoodOldAge): every one of the colony's elders at this home (EldersDay's elders)
    /// sleeps in a bedroom of their own, or one shared with their partner (the game's own room of theirs), at least decent
    /// (impressiveness 30). Pitched only while there is an elder and one of them lacks it.
    /// </summary>
    public class R4LGoal_EldersRooms : AmbitionGoal
    {
        public float impressiveness = Round4LivesMath.ElderRoom;

        /// <summary>A room's goal: any building is its own work (harm, part 6: the last push).</summary>
        public override bool OwnWork(ThingDef def) => def?.building != null;

        private bool Housed(Pawn p)
        {
            var room = RoomUtil.PrivateRoom(p);
            return room != null && !room.PsychologicallyOutdoors && RoomUtil.Impressiveness(room) >= impressiveness;
        }

        private int Count(Map map, out int elders)
        {
            var list = EldersDay.Elders(map);
            elders = list.Count;
            return list.Count(Housed);
        }

        public override bool Possible(Map map)
        {
            if (map == null || Avail.BestBuildable(map, Avail.IsSingleBed, "Bed") == null) return false;
            int housed = Count(map, out int elders);
            return Round4LivesMath.EldersWorth(housed, elders);
        }

        public override float Progress(Map map, float baseline)
        {
            int housed = Count(map, out int elders);
            return elders == 0 ? 0f : Round4LivesMath.EldersShare(housed, elders);
        }

        public override string Describe(Map map, float baseline)
        {
            int housed = Count(map, out int elders);
            return "CC_R4L_Goal_EldersRooms".Translate(impressiveness.ToString("0"), housed, elders);
        }

        public override Thing Showpiece(Map map) => EldersDay.Elders(map).Where(Housed).Select(p => p.ownership?.OwnedBed).FirstOrDefault(b => b != null && b.Spawned);
    }

    /// <summary>
    /// The neighbours' call (Rimfolk_R4L_Amb_NeighboursCall): a call answered by one of the colony's people (Rimfolk's own
    /// count, Council_WorldCallsAnswered, as Goal_Record counts it). Pitched only with world events on and a running one
    /// that asks for help.
    /// </summary>
    public class R4LGoal_NeighbourCall : Goal_Record
    {
        public static bool CallRunning()
        {
            var world = WorldSituations.Instance;
            if (world == null) return false;
            return world.Running.Any(s => s?.def?.beats != null && s.def.beats.Any(b => b?.options != null && b.options.Any(o => o != null && o.help > 0)));
        }

        public override bool Possible(Map map) => ColonyCouncilMod.Settings.worldSituations && ColonyCouncilMod.Settings.worldEvents && CallRunning() && base.Possible(map);
    }
}
