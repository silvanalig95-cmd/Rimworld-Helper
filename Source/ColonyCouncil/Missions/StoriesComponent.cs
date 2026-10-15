using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Keeps the colony's longer stories: branching story chains that play out over days and weeks (StoryChains, in
    /// Happenings/Workers_Chains2.cs) and the missions it has taken on (Missions.cs). A game component of its own, so
    /// it saves with the game without touching the council's own save data.
    /// </summary>
    public class StoriesComponent : GameComponent
    {
        public List<StoryChain> chains = new List<StoryChain>();
        public List<Mission> missions = new List<Mission>();
        public int nextId = 1;

        private static StoriesComponent cachedInstance;
        private static Game cachedGame;

        public StoriesComponent(Game game)
        {
        }

        public static StoriesComponent Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cachedInstance == null)
                {
                    cachedInstance = game.GetComponent<StoriesComponent>();
                    cachedGame = game;
                }
                return cachedInstance;
            }
        }

        public int NextId() => nextId++;

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cachedInstance = this;
            cachedGame = Current.Game;
            StoryChains.ClearCache();
        }

        public override void GameComponentTick()
        {
            int t = Find.TickManager.TicksGame;
            if (!Sched.Every(t, Sched.RareTicks, Sched.RareMissions) || Find.Maps.Count == 0) return;
            // Every few seconds: a mission accepted in the Quests tab starts at once, a goal met is noticed soon.
            Safe("missions", () => Missions.Tick(this, t));
            if (!Sched.Hourly(t, Sched.Stories)) return;
            Safe("story chains", () => StoryChains.Hourly(t));
            Safe("missions hourly", () => Missions.Hourly(this, t));
        }

        private static void Safe(string context, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce(context, e);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref chains, "chains", LookMode.Deep);
            Scribe_Collections.Look(ref missions, "missions", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (chains == null) chains = new List<StoryChain>();
                if (missions == null) missions = new List<Mission>();
                chains.RemoveAll(c => c == null || c.kind == null);
                missions.RemoveAll(m => m == null || m.kind == null);
            }
        }
    }
}
