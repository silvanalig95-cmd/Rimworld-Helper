using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What one pawn carries in its inventory for Rimfolk: what its own work dropped and is owed to storage (counts by
    /// def, not things, so a harvest that merges with a stack the pawn already had is still told apart), the skills of
    /// that work, and the building material it carries for a build area on a long material run.
    /// </summary>
    public class HaulLoad : IExposable
    {
        /// <summary>Owed to storage: what the pawn's work dropped, the leftovers of a run, things picked up on the way.</summary>
        public Dictionary<ThingDef, int> owed = new Dictionary<ThingDef, int>();
        /// <summary>The skills of the work that produced the load: while the pawn keeps at work that uses one, it carries the load along.</summary>
        public List<SkillDef> skills = new List<SkillDef>();
        public int startedTick = -1;
        /// <summary>Something didn't fit any more: stop working and deliver.</summary>
        public bool full;
        /// <summary>Deliver at the next chance, whatever the work: a run's leftovers, things picked up on the way, a load from the standalone mod.</summary>
        public bool now;
        public int lastDeliveryFailTick = -99999;

        /// <summary>Building material carried for a build area on a long material run, by def.</summary>
        public Dictionary<ThingDef, int> run = new Dictionary<ThingDef, int>();
        /// <summary>The frame or blueprint the run was taken for: the build area is round it.</summary>
        public IntVec3 runCenter = IntVec3.Invalid;
        /// <summary>The run's last pickup or delivery.</summary>
        public int runTick = -1;

        // Not saved.
        /// <summary>Guards against two delivery jobs in one tick.</summary>
        public int lastDeliveryJobTick = -1;
        /// <summary>The stack that didn't fit: carried in the hands on the delivery while it still lies close by.</summary>
        public Thing leftover;
        /// <summary>Things a deconstruction left (chunks, fuel, contents), hauled by hand right after, and when they were noted.</summary>
        public readonly List<Thing> haulNow = new List<Thing>();
        public int haulNowTick = -1;
        /// <summary>What the run's build area still needed of each material at the last look (HaulBook's pass): the other builders' cover.</summary>
        public readonly Dictionary<ThingDef, int> areaNeed = new Dictionary<ThingDef, int>();
        /// <summary>A walk that found nothing on the way: when, so it isn't looked at again at once.</summary>
        public int onTheWayFailTick = -99999;

        private static readonly List<ThingDef> tmpDefs = new List<ThingDef>();

        public bool Empty => owed.Count == 0;
        public bool RunEmpty => run.Count == 0;
        public bool Idle => owed.Count == 0 && run.Count == 0 && haulNow.Count == 0;

        public bool Owes(ThingDef def) => def != null && owed.ContainsKey(def);
        public int OwedCount(ThingDef def) => def != null && owed.TryGetValue(def, out int n) ? n : 0;
        public int RunCount(ThingDef def) => def != null && run.TryGetValue(def, out int n) ? n : 0;

        /// <summary>The owed load's mass by the defs' base mass (what a trip is worth).</summary>
        public float Mass
        {
            get
            {
                float m = 0f;
                foreach (var kv in owed) m += kv.Key.BaseMass * kv.Value;
                return m;
            }
        }

        public void Add(ThingDef def, int count)
        {
            if (def == null || count <= 0) return;
            if (owed.Count == 0) startedTick = Find.TickManager.TicksGame;
            owed[def] = OwedCount(def) + count;
        }

        public void AddSkill(SkillDef skill)
        {
            if (skill != null && !skills.Contains(skill)) skills.Add(skill);
        }

        public void Remove(ThingDef def, int count)
        {
            if (def == null || count <= 0 || !owed.TryGetValue(def, out int current)) return;
            if (current <= count) owed.Remove(def);
            else owed[def] = current - count;
            if (owed.Count == 0) Reset();
        }

        public void AddRun(ThingDef def, int count, IntVec3 center, int now)
        {
            if (def == null || count <= 0) return;
            run[def] = RunCount(def) + count;
            if (center.IsValid) runCenter = center;
            runTick = now;
        }

        public void RemoveRun(ThingDef def, int count)
        {
            if (def == null || count <= 0 || !run.TryGetValue(def, out int current)) return;
            if (current <= count) run.Remove(def);
            else run[def] = current - count;
            if (run.Count == 0) EndRun();
        }

        /// <summary>What is left of the run's material goes to storage at the next chance.</summary>
        public void RunToStorage(ThingDef def)
        {
            int n = RunCount(def);
            run.Remove(def);
            areaNeed.Remove(def);
            if (n > 0)
            {
                Add(def, n);
                now = true;
            }
            if (run.Count == 0) EndRun();
        }

        /// <summary>The run ends with nothing sent to storage (the pawn left the map: its material is plain inventory now).</summary>
        public void DropRun()
        {
            run.Clear();
            EndRun();
        }

        private void EndRun()
        {
            runCenter = IntVec3.Invalid;
            areaNeed.Clear();
        }

        /// <summary>
        /// Never more owed or carried for a run than the pawn still has (it may have eaten, used, dropped or traded some,
        /// and a frame takes the run's material straight from the inventory): what's missing comes off the run first.
        /// </summary>
        public void Reconcile(Pawn pawn)
        {
            tmpDefs.Clear();
            tmpDefs.AddRange(owed.Keys);
            foreach (var d in run.Keys)
                if (!tmpDefs.Contains(d))
                    tmpDefs.Add(d);
            for (int i = 0; i < tmpDefs.Count; i++)
            {
                ThingDef def = tmpDefs[i];
                int have = def == null || pawn?.inventory == null ? 0 : pawn.inventory.innerContainer.TotalStackCountOfDef(def);
                int r = RunCount(def), o = OwedCount(def);
                int excess = r + o - have;
                if (excess <= 0) continue;
                int fromRun = Math.Min(r, excess);
                if (fromRun >= r) run.Remove(def);
                else run[def] = r - fromRun;
                excess -= fromRun;
                if (excess <= 0) continue;
                if (excess >= o) owed.Remove(def);
                else owed[def] = o - excess;
            }
            tmpDefs.Clear();
            if (owed.Count == 0) Reset();
            if (run.Count == 0) EndRun();
        }

        private void Reset()
        {
            startedTick = -1;
            full = false;
            now = false;
            skills.Clear();
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref owed, "owed", LookMode.Def, LookMode.Value);
            Scribe_Collections.Look(ref skills, "skills", LookMode.Def);
            Scribe_Values.Look(ref startedTick, "startedTick", -1);
            Scribe_Values.Look(ref full, "full", false);
            Scribe_Values.Look(ref now, "now", false);
            Scribe_Values.Look(ref lastDeliveryFailTick, "lastDeliveryFailTick", -99999);
            Scribe_Collections.Look(ref run, "run", LookMode.Def, LookMode.Value);
            Scribe_Values.Look(ref runCenter, "runCenter", IntVec3.Invalid);
            Scribe_Values.Look(ref runTick, "runTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                owed ??= new Dictionary<ThingDef, int>();
                skills ??= new List<SkillDef>();
                run ??= new Dictionary<ThingDef, int>();
                // Defs of a removed mod load as null.
                owed.RemoveAll(kv => kv.Key == null || kv.Value <= 0);
                run.RemoveAll(kv => kv.Key == null || kv.Value <= 0);
                skills.RemoveAll(s => s == null);
                if (owed.Count == 0) Reset();
                if (run.Count == 0) EndRun();
            }
        }
    }

    /// <summary>
    /// Carrying and hauling's book, saved with the game: who carries what for Rimfolk. Each tick it looks again around
    /// the spots where work just dropped something (only while some wait); every 250 ticks (Sched.RareHauling, only while
    /// someone carries something) it ends runs whose build area needs no more and sends loads to storage when the
    /// choice of the next job can't. When the player's standalone Harvest and Haul is gone from a save it still knew,
    /// the loads its pawns carried are taken over (read from the save itself), so nothing stays in a pocket.
    /// </summary>
    public class HaulBook : GameComponent
    {
        private static HaulBook instance;
        private static Game instanceGame;

        private Dictionary<Pawn, HaulLoad> loads = new Dictionary<Pawn, HaulLoad>();
        private readonly List<HarvestHaul.Request> sweeps = new List<HarvestHaul.Request>();
        private readonly List<HarvestHaul.Request> sweeping = new List<HarvestHaul.Request>();
        private List<HaulMath.StandaloneLoad> fromStandalone;

        private List<Pawn> tmpPawns;
        private List<HaulLoad> tmpLoads;
        private static readonly List<Pawn> tmpGone = new List<Pawn>();

        /// <summary>This game's book (null between games).</summary>
        public static HaulBook Get => instance != null && instanceGame != null && instanceGame == Current.Game ? instance : null;

        public HaulBook(Game game)
        {
            instance = this;
            instanceGame = game;
            // Made while a save loads: the standalone mod's tracker may be in it, with what its pawns still carry.
            if (Scribe.mode == LoadSaveMode.LoadingVars && !HaulLinks.Standalone)
            {
                try
                {
                    var node = HaulMath.FindStandaloneTracker(Scribe.loader?.curXmlParent);
                    if (node != null) fromStandalone = HaulMath.ReadStandaloneLoads(node);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("reading harvest and haul's loads", e);
                }
            }
        }

        public HaulLoad LoadOf(Pawn pawn) => pawn != null && loads.TryGetValue(pawn, out var l) ? l : null;

        public HaulLoad LoadFor(Pawn pawn)
        {
            if (!loads.TryGetValue(pawn, out var l))
            {
                l = new HaulLoad();
                loads[pawn] = l;
            }
            return l;
        }

        public bool AnyLoads => loads.Count > 0;

        /// <summary>The pawns on a run, for the other builders' cover.</summary>
        public IEnumerable<KeyValuePair<Pawn, HaulLoad>> Runners
        {
            get
            {
                foreach (var kv in loads)
                    if (kv.Value.run.Count > 0)
                        yield return kv;
            }
        }

        /// <summary>Look again round the spot on the next tick: things a mod or the game places after the hooked method.</summary>
        public void QueueSweep(HarvestHaul.Request r) => sweeps.Add(r);

        public override void GameComponentTick()
        {
            if (sweeps.Count > 0)
            {
                sweeping.Clear();
                sweeping.AddRange(sweeps);
                sweeps.Clear();
                for (int i = 0; i < sweeping.Count; i++)
                {
                    try
                    {
                        HarvestHaul.Sweep(sweeping[i]);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("collecting what work dropped", e);
                    }
                }
                sweeping.Clear();
            }
            if (loads.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareHauling)) return;
            using var clock = PassClock.Time(Pass.RareHauling);
            tmpGone.Clear();
            foreach (var kv in loads.ToList())
            {
                Pawn p = kv.Key;
                if (p == null || p.Destroyed || p.Discarded || p.Dead)
                {
                    tmpGone.Add(p);
                    continue;
                }
                try
                {
                    Carrying.Pass(p, kv.Value, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("carrying and hauling's pass", e);
                }
                if (kv.Value.Idle) tmpGone.Add(p);
            }
            foreach (var p in tmpGone) loads.Remove(p);
            tmpGone.Clear();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (fromStandalone == null) return;
            try
            {
                int taken = Carrying.TakeOver(fromStandalone, this);
                if (taken > 0) Log.Message("[Rimfolk] Took over what " + taken + " pawns carried for the standalone Harvest and Haul; they take it to storage as before.");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("taking over harvest and haul's loads", e);
            }
            fromStandalone = null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
                loads.RemoveAll(kv => kv.Key == null || kv.Value == null || kv.Value.Idle || kv.Key.Destroyed || kv.Key.Discarded || kv.Key.Dead);
            Scribe_Collections.Look(ref loads, "loads", LookMode.Reference, LookMode.Deep, ref tmpPawns, ref tmpLoads);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                loads ??= new Dictionary<Pawn, HaulLoad>();
                loads.RemoveAll(kv => kv.Key == null || kv.Value == null);
            }
        }
    }
}
