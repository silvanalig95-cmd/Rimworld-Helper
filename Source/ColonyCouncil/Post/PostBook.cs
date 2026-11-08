using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>A letter for the colony on its way in: it waits for whoever comes its way, or a messenger after a while (Post/MailWatch.cs).</summary>
    public class PostItem : IExposable
    {
        public int id;
        public Map map;
        /// <summary>The writer's people (the messenger's, if one is sent).</summary>
        public Faction from;
        /// <summary>The town it was written in (Settlement ID), for the way it comes; -1 if none.</summary>
        public int town = -1;
        public string label, text, chronicle;
        public LetterDef def;
        public Pawn chroniclePawn;
        /// <summary>The answer to one of the colony's letters (World/NeighbourLetters.cs), or -1.</summary>
        public int letterId = -1;
        /// <summary>It is in the region from this tick (the road there behind it).</summary>
        public int arrive;
        public PostUrgency urgency;
        /// <summary>Who brings it in now (walking to the mailbag with it), or null while it waits.</summary>
        public Pawn carrier;
        /// <summary>What the messenger who brings it at last calls it ("a letter from Mayor Bell").</summary>
        public string bringLabel;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (carrier != null && (carrier.Discarded || carrier.Dead)) carrier = null;
                if (chroniclePawn != null && chroniclePawn.Discarded) chroniclePawn = null;
            }
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref from, "from");
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref chronicle, "chronicle");
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref chroniclePawn, "chroniclePawn");
            Scribe_Values.Look(ref letterId, "letterId", -1);
            Scribe_Values.Look(ref arrive, "arrive");
            Scribe_Values.Look(ref urgency, "urgency");
            Scribe_References.Look(ref carrier, "carrier");
            Scribe_Values.Look(ref bringLabel, "bringLabel");
        }
    }

    /// <summary>A home map's regular post: when it next comes, and the rider's group while they're on the map.</summary>
    public class PostRound : IExposable
    {
        public Map map;
        /// <summary>The next post comes no earlier than this (-1: not set yet).</summary>
        public int next = -1;
        public int last = -1;
        /// <summary>The post rider's group while on the map.</summary>
        public Lord lord;
        public int arrived = -1;
        /// <summary>The people whose post it is, for the words.</summary>
        public Faction people;
        /// <summary>At the gate: the mail brought set down, the letters going out taken, and when their business was done.</summary>
        public bool brought, took;
        public int doneAt = -1;

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref next, "next", -1);
            Scribe_Values.Look(ref last, "last", -1);
            Scribe_References.Look(ref lord, "lord");
            Scribe_Values.Look(ref arrived, "arrived", -1);
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref brought, "brought");
            Scribe_Values.Look(ref took, "took");
            Scribe_Values.Look(ref doneAt, "doneAt", -1);
        }
    }

    public enum RideStage
    {
        /// <summary>Fetching the letter from the mailbag and walking to the map's edge.</summary>
        SettingOut,
        /// <summary>On the road there, a caravan of their own.</summary>
        Out,
        /// <summary>At the town, waiting for the answer.</summary>
        AtTown,
        /// <summary>On the road home with the answer.</summary>
        Back
    }

    /// <summary>The colony's own rider with a letter (Post/OwnRiders.cs).</summary>
    public class OwnRide : IExposable
    {
        public int letterId;
        public Pawn rider, mount;
        public Map home;
        public int town;
        public string townName, riderName;
        public RideStage stage;
        public int since, until = -1;
        /// <summary>The answer they carry home: read when they're back.</summary>
        public string answerLabel, answerText, chronicle;
        public LetterDef answerDef;
        public bool answered;

        public void ExposeData()
        {
            Scribe_Values.Look(ref letterId, "letterId");
            Scribe_References.Look(ref rider, "rider", true);
            Scribe_References.Look(ref mount, "mount", true);
            Scribe_References.Look(ref home, "home");
            Scribe_Values.Look(ref town, "town");
            Scribe_Values.Look(ref townName, "townName");
            Scribe_Values.Look(ref riderName, "riderName");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref since, "since");
            Scribe_Values.Look(ref until, "until", -1);
            Scribe_Values.Look(ref answerLabel, "answerLabel");
            Scribe_Values.Look(ref answerText, "answerText");
            Scribe_Values.Look(ref chronicle, "chronicle");
            Scribe_Defs.Look(ref answerDef, "answerDef");
            Scribe_Values.Look(ref answered, "answered");
        }
    }

    /// <summary>
    /// The post's book (docs/design/post-and-letters.md, step 1): mail for the colony waiting for a carrier, each home's
    /// regular post, and the colony's own riders out with a letter. The letters going out wait in the outbox as letters of
    /// the neighbour book (World/NeighbourLetters.cs, stage Outbox). Looked at on the neighbours' passes (World/Notables.cs):
    /// hourly, and every 250 ticks while there is something to watch.
    /// </summary>
    public class PostBook : GameComponent
    {
        public List<PostItem> incoming = new List<PostItem>();
        public List<PostRound> rounds = new List<PostRound>();
        public List<OwnRide> rides = new List<OwnRide>();
        public int nextId = 1;

        private static PostBook cached;
        private static Game cachedGame;

        public PostBook(Game game)
        {
        }

        public static PostBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<PostBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            MailWatch.Reset();
        }

        /// <summary>The post (the setting): letters wait in the outbox and go with whoever is going; off, a messenger comes for each, as before.</summary>
        public static bool On => (ColonyCouncilMod.Settings?.post ?? true) && NeighbourLetters.On;

        public static PostPace Pace => ColonyCouncilMod.Settings?.postPace ?? PostPace.Usual;

        public PostRound RoundOf(Map map, bool make = true)
        {
            var r = rounds.FirstOrDefault(x => x.map == map);
            if (r == null && make && map != null)
            {
                r = new PostRound { map = map };
                rounds.Add(r);
            }
            return r;
        }

        public PostItem Item(int id) => incoming.FirstOrDefault(i => i.id == id);

        public OwnRide RideOf(Pawn p) => p == null ? null : rides.FirstOrDefault(r => r.rider == p);

        public OwnRide RideFor(int letterId) => rides.FirstOrDefault(r => r.letterId == letterId);

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                rounds.RemoveAll(r => r == null || r.map == null || !Find.Maps.Contains(r.map));
                // The rider's group gone from the map: not kept (a lord no longer on its map can't be found again on loading).
                foreach (var r in rounds)
                    if (r.lord != null && !r.map.lordManager.lords.Contains(r.lord))
                        r.lord = null;
                rides.RemoveAll(r => r == null || r.rider == null || r.rider.Dead);
            }
            Scribe_Collections.Look(ref incoming, "incoming", LookMode.Deep);
            Scribe_Collections.Look(ref rounds, "rounds", LookMode.Deep);
            Scribe_Collections.Look(ref rides, "rides", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                incoming ??= new List<PostItem>();
                incoming.RemoveAll(i => i == null);
                rounds ??= new List<PostRound>();
                rounds.RemoveAll(r => r == null || r.map == null);
                rides ??= new List<OwnRide>();
                rides.RemoveAll(r => r == null || r.rider == null);
            }
        }
    }
}
