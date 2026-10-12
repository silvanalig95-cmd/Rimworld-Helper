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
    /// The heart of each colonist (strain, feelings, the heart's needs), in a game component of its own so it keeps its
    /// own save data. Each colonist is looked at once an hour, a tenth of the colony at a time. Nothing runs per tick
    /// beyond a modulo check, and nothing here changes vanilla mood: the layer reaches the game only through its own
    /// situational thought, a stat part, the episode mental state and Rimfolk's own systems.
    /// </summary>
    public class HeartTracker : GameComponent
    {
        private Dictionary<Pawn, PawnHeart> hearts = new Dictionary<Pawn, PawnHeart>();
        private List<Pawn> tmpPawns;
        private List<PawnHeart> tmpHearts;
        /// <summary>The last episode anywhere in the colony (one per two days at most).</summary>
        public int lastColonyEpisodeTick = -999999;

        private static HeartTracker cached;
        private static Game cachedGame;

        /// <summary>Ten slices an hour: each colonist is in one of them (by ID), so the colony is spread over the hour.</summary>
        private const int Slices = 10;
        private const int SliceTicks = GenDate.TicksPerHour / Slices;

        public static HeartTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<HeartTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public HeartTracker(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            HeartMapContext.Clear();
            HeartView.Reset();
            HeartLogic.ResetSession();
            Heart.ResetCaches();
        }

        /// <summary>The colonist's heart, or null when they have none (not one of the colony's people, a baby).</summary>
        public PawnHeart For(Pawn p, bool create = false)
        {
            if (p == null) return null;
            if (hearts.TryGetValue(p, out var h)) return h;
            if (!create || !Heart.Eligible(p)) return null;
            h = new PawnHeart();
            h.EnsureLists();
            hearts[p] = h;
            return h;
        }

        public IEnumerable<KeyValuePair<Pawn, PawnHeart>> All => hearts;

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving) Prune();
            Scribe_Collections.Look(ref hearts, "hearts", LookMode.Reference, LookMode.Deep, ref tmpPawns, ref tmpHearts);
            Scribe_Values.Look(ref lastColonyEpisodeTick, "lastColonyEpisodeTick", -999999);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (hearts == null) hearts = new Dictionary<Pawn, PawnHeart>();
                // Someone the save no longer has (a pawn from a removed mod) loads as a null key.
                hearts.RemoveAll(kv => kv.Key == null || kv.Value == null);
                foreach (var h in hearts.Values) h.EnsureLists();
            }
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % SliceTicks != 71) return;
            if (!Heart.On) return;
            int slice = now / SliceTicks % Slices;
            try
            {
                Slice(slice, now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("heart", e);
            }
            // Once an hour: forget the dead and those who left.
            if (slice == 0)
            {
                try
                {
                    Prune();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("heart clean-up", e);
                }
            }
        }

        private readonly List<Pawn> sliceTmp = new List<Pawn>();

        private void Slice(int slice, int now)
        {
            sliceTmp.Clear();
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists)
                if (p != null && p.thingIDNumber % Slices == slice) sliceTmp.Add(p);
            foreach (var p in sliceTmp)
            {
                if (!Heart.Eligible(p)) continue;
                var h = For(p, true);
                if (h == null) continue;
                try
                {
                    HeartLogic.Evaluate(p, h, HeartMapContext.For(p.MapHeld, now), now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("heart of " + p.LabelShort, e);
                }
            }
            sliceTmp.Clear();
        }

        /// <summary>
        /// Drops the hearts of the dead, the destroyed and those no longer the colony's (kidnapped, gone, banished). A
        /// colonist away on a caravan or at an outpost keeps theirs.
        /// </summary>
        public void Prune()
        {
            if (hearts.Count == 0) return;
            List<Pawn> gone = null;
            foreach (var kv in hearts)
            {
                var p = kv.Key;
                bool keep = p != null && !p.Dead && !p.Destroyed && !p.Discarded && p.Faction == Faction.OfPlayer && !p.IsPrisoner && Heart.Eligible(p, false);
                if (keep) continue;
                (gone ??= new List<Pawn>()).Add(p);
            }
            if (gone == null) return;
            foreach (var p in gone) hearts.Remove(p);
        }

        /// <summary>Forgets one colonist at once (they died or left).</summary>
        public void Forget(Pawn p)
        {
            if (p != null) hearts.Remove(p);
        }
    }

    /// <summary>
    /// What the heart needs to know about a map, worked out at most once an hour (the threat flag every few minutes of
    /// game time): danger, food and medicine, what's coming, visitors, how long the colony has been there.
    /// </summary>
    public class HeartMapContext
    {
        public Map map;
        public int tick = -999999;
        public int threatTick = -999999;
        public bool threat;
        public int ticksSinceBigThreat = 999999;
        public float foodDays = 999f;
        public int medicine;
        public int colonists;
        public float defense = 1f;
        public Season season;
        public int filth;
        public bool visitors;
        public bool comet;
        public int newcomers;
        public int colonyDays;
        public bool home;
        /// <summary>What the colony knows is coming, each with how much dread it carries.</summary>
        public readonly List<(string cause, float amount)> dread = new List<(string, float)>();

        private static readonly Dictionary<Map, HeartMapContext> cache = new Dictionary<Map, HeartMapContext>();
        private static readonly HeartMapContext none = new HeartMapContext();

        public static void Clear() => cache.Clear();

        /// <summary>The context for a map (a colonist on no map, in a caravan, gets an empty one).</summary>
        public static HeartMapContext For(Map map, int now)
        {
            if (map == null) return none;
            if (!cache.TryGetValue(map, out var c))
            {
                if (cache.Count > 8) cache.Clear();
                c = new HeartMapContext { map = map };
                cache[map] = c;
            }
            if (now - c.threatTick >= 250 || now < c.threatTick)
            {
                c.threatTick = now;
                try
                {
                    c.threat = GenHostility.AnyHostileActiveThreatToPlayer(map, false, false);
                }
                catch
                {
                    c.threat = false;
                }
            }
            if (now - c.tick >= GenDate.TicksPerHour || now < c.tick) c.Refresh(now);
            return c;
        }

        private void Refresh(int now)
        {
            tick = now;
            home = map.IsPlayerHome;
            int last = map.storyState?.LastThreatBigTick ?? -1;
            ticksSinceBigThreat = last > 0 ? now - last : 999999;
            colonists = map.mapPawns.FreeColonistsSpawnedCount;
            season = GenLocalDate.Season(map);
            foodDays = SafeFloat(() => AdvisorWorker_Food.DaysOfFood(map), 999f);
            medicine = SafeInt(() => Avail.MedicineCount(map));
            filth = home ? SafeInt(() => WishWorker_Tidy.Filth(map)) : 0;
            defense = home ? SafeFloat(() => DefenseUtil.Assess(map).ratio, 1f) : 1f;
            comet = CouncilDefOf.Council_Comet != null && (map.gameConditionManager?.ConditionIsActive(CouncilDefOf.Council_Comet) ?? false);
            visitors = false;
            foreach (var p in map.mapPawns.AllHumanlikeSpawned)
            {
                if (p.Faction == null || p.Faction == Faction.OfPlayer || p.HostileTo(Faction.OfPlayer) || p.IsPrisoner) continue;
                visitors = true;
                break;
            }
            newcomers = 0;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                int asColonist = p.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 999999;
                if (asColonist < GenDate.TicksPerDay * 3) newcomers++;
            }
            colonyDays = Find.TickManager.TicksGame / GenDate.TicksPerDay;
            try
            {
                Dread(now);
            }
            catch (Exception e)
            {
                dread.Clear();
                CouncilLog.ErrorOnce("heart dread", e);
            }
        }

        /// <summary>What's coming, as the colony knows it: promised fights, debts, bad times in the region, a lean winter.</summary>
        private void Dread(int now)
        {
            dread.Clear();
            if (!home) return;
            var comp = CouncilGameComponent.Instance;
            var t = comp?.happenings;
            if (t != null)
            {
                int fights = t.raids.Count(r => r != null && r.faction != null && (r.map == null || r.map == map) && r.tick - now < GenDate.TicksPerDay * 15);
                if (fights > 0) dread.Add(("CC_Heart_Cause_FightComing".Translate(), Mathf.Min(8f, 4f * fights)));
                if (t.memos.Any(m => m != null && !m.used && m.kind == "debt")) dread.Add(("CC_Heart_Cause_Debt".Translate(), 3f));
            }
            var world = WorldSituations.Instance;
            if (world != null)
            {
                int bad = 0;
                bool war = false;
                foreach (var s in world.Running)
                {
                    if (s?.def == null) continue;
                    if (s.def.battle) war = true;
                    else if (s.def.bad) bad++;
                }
                if (war) dread.Add(("CC_Heart_Cause_Warband".Translate(), 3f));
                if (bad > 0) dread.Add(("CC_Heart_Cause_BadTimes".Translate(), Mathf.Min(4f, 2f * bad)));
            }
            if (foodDays < 5f) dread.Add(("CC_Heart_Cause_FoodLow".Translate(), 4f));
            else if ((season == Season.Fall || season == Season.Winter || season == Season.PermanentWinter) && foodDays < 20f)
                dread.Add(("CC_Heart_Cause_WinterFood".Translate(), 3f));
            if (colonists >= 2 && medicine < colonists / 2) dread.Add(("CC_Heart_Cause_FewMedicine".Translate(), 2f));
            if (colonyDays > 10 && defense < 0.5f) dread.Add(("CC_Heart_Cause_WeakDefenses".Translate(), 2f));
        }

        private static float SafeFloat(Func<float> f, float fallback)
        {
            try
            {
                return f();
            }
            catch
            {
                return fallback;
            }
        }

        private static int SafeInt(Func<int> f)
        {
            try
            {
                return f();
            }
            catch
            {
                return 0;
            }
        }
    }
}
