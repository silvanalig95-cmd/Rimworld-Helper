using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One fight at one home (the social web keeps one per home, SocialWeb.BattleAt): who has fought in it so far, who
    /// it's against, and its tally for the colony's account of its victory (enemies fallen, who brought them down, our
    /// dead). Saved under the names the social web always used, so an old save's one fight is read straight into the seat's.
    /// </summary>
    public class Battle : IExposable
    {
        /// <summary>The home it's at (its key, Homes.KeyOf), or none for an old save's (the seat's, on first look).</summary>
        public int home = HomesMath.None;
        public List<Pawn> fighters = new List<Pawn>();
        public Faction faction;
        public bool active;
        public int kills;
        public int countStart = -1;
        public List<string> fallen = new List<string>();
        public int quietHours;
        /// <summary>Colonists held by other peoples when the fight began: if that went up, the fight wasn't won.</summary>
        public int kidnapped;
        /// <summary>Hours the end of the fight was held back while one of ours lay bleeding (or the enemy lingered).</summary>
        [Unsaved] public int heldHours;
        [Unsaved] public Dictionary<Pawn, int> killsBy = new Dictionary<Pawn, int>();

        /// <summary>Its home while it's ours.</summary>
        public Map Map => Homes.MapOf(home);

        /// <summary>Counts from the first blood of a fight; anything older than that fight is forgotten.</summary>
        public void StartCount()
        {
            int now = Find.TickManager.TicksGame;
            if (active || (countStart >= 0 && now - countStart < GenDate.TicksPerHour * 3)) return;
            ResetCount();
            countStart = now;
        }

        public void ResetCount()
        {
            kills = 0;
            countStart = -1;
            kidnapped = Kidnapped();
            fallen.Clear();
            killsBy.Clear();
        }

        /// <summary>Colonists held by other peoples, anywhere.</summary>
        public static int Kidnapped() =>
            Find.FactionManager?.AllFactionsListForReading.Sum(f => f.kidnapped?.KidnappedPawnsListForReading.Count ?? 0) ?? 0;

        /// <summary>One of ours lies bleeding at this home: whether they make it is part of the fight's story.</summary>
        public static bool Bleeding(Map map) =>
            map != null && map.mapPawns.FreeColonistsSpawned.Any(p => p.Downed && !p.Dead && p.health.hediffSet.BleedRateTotal > 0.01f);

        /// <summary>The enemy is still about at this home, though not attacking: fleeing slowly, or insects guarding their hive.</summary>
        public static bool EnemyLingers(Faction foe, Map map) =>
            foe != null && map != null && map.mapPawns.AllPawnsSpawned.Any(x => x.Faction == foe && !x.Dead && !x.Downed && !x.IsPrisoner && x.HostileTo(Faction.OfPlayer));

        /// <summary>A real threat came to this home this time (a raid, a pack, an infestation...), not a scuffle with guests or a hunt gone wrong.</summary>
        public bool WasThreat(Map map) => countStart >= 0 && map != null && map.storyState.LastThreatBigTick >= countStart - GenDate.TicksPerDay;

        public void ExposeData()
        {
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Collections.Look(ref fighters, "fighters", LookMode.Reference);
            Scribe_References.Look(ref faction, "battleFaction");
            Scribe_Values.Look(ref active, "battleActive");
            Scribe_Values.Look(ref kills, "battleKills");
            Scribe_Values.Look(ref countStart, "battleCountStart", -1);
            Scribe_Collections.Look(ref fallen, "battleFallen", LookMode.Value);
            Scribe_Values.Look(ref quietHours, "quietHours");
            Scribe_Values.Look(ref kidnapped, "battleKidnapped");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                fighters ??= new List<Pawn>();
                fighters.RemoveAll(p => p == null);
                fallen ??= new List<string>();
                killsBy ??= new Dictionary<Pawn, int>();
            }
        }
    }
}
