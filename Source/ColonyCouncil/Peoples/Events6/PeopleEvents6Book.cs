using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A psychic tide a Conclave watch held back (item 49): its home, the game's incident, the sex it will touch (as the game
    /// chose it when held), when it is due, and whether a reader has come to sit with the worst touched.
    /// </summary>
    public class TideRecord : IExposable
    {
        public Map map;
        public Faction faction;
        public string def;
        public int gender, due = -1;
        public bool came, done;

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref def, "def");
            Scribe_Values.Look(ref gender, "gender");
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref came, "came");
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>The Mossborn's camp after the fire, asked for at the fire in the canopy (item 52's second answer): due at this home.</summary>
    public class AfterRecord : IExposable
    {
        public Map map;
        public Faction faction;
        public int due = -1;
        public bool done;

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>
    /// A contract on the colony's enemy (item 55): the Banner's company bound for a world object (its id and name), by when the
    /// colony must get there, the price, whether the loot is all the colony's, the company once it is there, and the names
    /// the company lost. Stage: "waiting" (for the colony's people to reach it), "fighting" (the company there), "done".
    /// </summary>
    public class HiredContract : IExposable
    {
        public int id;
        public Faction faction, enemy;
        public Map home;
        public int target = -1, due = -1, price, size = 3;
        public string targetName, stage = "waiting";
        public bool town, upFront;
        public List<Pawn> company = new List<Pawn>();
        public List<string> lost = new List<string>();
        public int took;

        public bool Done => stage == "done";

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref enemy, "enemy");
            Scribe_References.Look(ref home, "home");
            Scribe_Values.Look(ref target, "target", -1);
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref price, "price");
            Scribe_Values.Look(ref size, "size", 3);
            Scribe_Values.Look(ref targetName, "targetName");
            Scribe_Values.Look(ref stage, "stage", "waiting");
            Scribe_Values.Look(ref town, "town");
            Scribe_Values.Look(ref upFront, "upFront");
            Scribe_Collections.Look(ref company, "company", LookMode.Reference);
            Scribe_Collections.Look(ref lost, "lost", LookMode.Value);
            Scribe_Values.Look(ref took, "took");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                company ??= new List<Pawn>();
                company.RemoveAll(p => p == null);
                lost ??= new List<string>();
            }
        }
    }

    /// <summary>An old soldier of the Banner settled with the colony (item 56): their next muster, or bought out for good.</summary>
    public class VeteranRecord : IExposable
    {
        public Pawn pawn;
        public Faction faction;
        public int next = -1, called = -1;
        public bool bought, done;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref next, "next", -1);
            Scribe_Values.Look(ref called, "called", -1);
            Scribe_Values.Look(ref bought, "bought");
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>
    /// The other side's contract (item 57): a raid held back while the Banner's sergeant says who hired them; its home, the
    /// raid's incident and points, the people who hired them, when it comes, and the colony's answer: "march" (the Banner
    /// marches with the raid), "buyout" (the company stands with the colony against it), "aside" (they stand aside).
    /// </summary>
    public class HireRecord : IExposable
    {
        public Map map;
        public Faction faction, enemy;
        public string def, choice = "march";
        public float points;
        public int due = -1;
        public bool came, done;

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref enemy, "enemy");
            Scribe_Values.Look(ref def, "def");
            Scribe_Values.Look(ref choice, "choice", "march");
            Scribe_Values.Look(ref points, "points");
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref came, "came");
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>
    /// What the peoples' own events of round 6 keep with the game (Peoples/Events6/): the dry spells and fires already rolled
    /// for, the tides held back, the camps after the fire asked for, the Banner's contracts, its old soldiers' musters and the
    /// raids it was hired for, and when the code-fired visits last came. The rest lives in the events' visits and in a camp's
    /// own state. A save from before has none of it.
    /// </summary>
    public class PeopleEvents6Book : GameComponent
    {
        /// <summary>"mapId:conditionId" for each dry spell the storm-walkers were rolled for, so none is asked twice.</summary>
        public List<string> rainRolled = new List<string>();
        public List<TideRecord> tides = new List<TideRecord>();
        /// <summary>"mapId:day" for each wildfire the thorn-guards were rolled for.</summary>
        public List<string> canopyRolled = new List<string>();
        /// <summary>"mapId:thingId" for the burned trees and blighted plants already reckoned for a camp after the fire.</summary>
        public List<string> burnSeen = new List<string>();
        /// <summary>"mapId:day" for each blight a camp after the fire was rolled for.</summary>
        public List<string> blightRolled = new List<string>();
        public List<AfterRecord> afters = new List<AfterRecord>();
        public List<HiredContract> contracts = new List<HiredContract>();
        public int nextContract = 1;
        public List<VeteranRecord> veterans = new List<VeteranRecord>();
        public List<HireRecord> hires = new List<HireRecord>();
        /// <summary>When the storm-walkers, the thorn-guards, the camp after the fire and the hired sergeant last came.</summary>
        public int lastRain = -1, lastCanopy = -1, lastAfter = -1, lastHire = -1;

        private static PeopleEvents6Book cached;
        private static Game cachedGame;

        public PeopleEvents6Book(Game game)
        {
        }

        public static PeopleEvents6Book Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<PeopleEvents6Book>();
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
            PeopleEvents6.Forget();
        }

        private static void Note(List<string> list, string key, int max)
        {
            if (list.Contains(key)) return;
            list.Add(key);
            while (list.Count > max) list.RemoveAt(0);
        }

        public bool RainRolled(Map map, int condition) => map != null && rainRolled.Contains(map.uniqueID + ":" + condition);

        public void NoteRain(Map map, int condition)
        {
            if (map != null) Note(rainRolled, map.uniqueID + ":" + condition, 8);
        }

        public bool CanopyRolled(Map map, int day) => map != null && canopyRolled.Contains(map.uniqueID + ":" + day);

        public void NoteCanopy(Map map, int day)
        {
            if (map != null) Note(canopyRolled, map.uniqueID + ":" + day, 8);
        }

        /// <summary>The highest id of the burned trees or blighted plants already reckoned on this map (-1: none).</summary>
        public int BurnSeen(Map map)
        {
            if (map == null) return -1;
            string pre = map.uniqueID + ":";
            var mine = burnSeen.LastOrDefault(s => s.StartsWith(pre));
            return mine != null && int.TryParse(mine.Substring(pre.Length), out int id) ? id : -1;
        }

        public void NoteBurn(Map map, int id)
        {
            if (map == null) return;
            string pre = map.uniqueID + ":";
            burnSeen.RemoveAll(s => s.StartsWith(pre));
            Note(burnSeen, pre + id, 8);
        }

        public bool BlightRolled(Map map, int day) => map != null && (blightRolled.Contains(map.uniqueID + ":" + day) || blightRolled.Contains(map.uniqueID + ":" + (day - 1)));

        public void NoteBlight(Map map, int day)
        {
            if (map != null) Note(blightRolled, map.uniqueID + ":" + day, 8);
        }

        public void AddTide(TideRecord r)
        {
            if (r?.map == null) return;
            tides.Add(r);
            while (tides.Count > 4 && tides.Any(x => x.done)) tides.Remove(tides.First(x => x.done));
            while (tides.Count > 4) tides.RemoveAt(0);
        }

        public void AddAfter(AfterRecord r)
        {
            if (r?.map == null || afters.Any(a => !a.done && a.map == r.map)) return;
            afters.Add(r);
            while (afters.Count > 4 && afters.Any(x => x.done)) afters.Remove(afters.First(x => x.done));
            while (afters.Count > 4) afters.RemoveAt(0);
        }

        public HiredContract AddContract(HiredContract r)
        {
            if (r == null) return null;
            r.id = nextContract++;
            contracts.Add(r);
            while (contracts.Count > 4 && contracts.Any(x => x.Done)) contracts.Remove(contracts.First(x => x.Done));
            return r;
        }

        public void AddVeteran(VeteranRecord r)
        {
            if (r?.pawn == null || veterans.Any(v => v.pawn == r.pawn)) return;
            veterans.Add(r);
            while (veterans.Count > PeopleEvents6.MaxVeterans && veterans.Any(x => x.done)) veterans.Remove(veterans.First(x => x.done));
        }

        public void AddHire(HireRecord r)
        {
            if (r?.map == null) return;
            hires.Add(r);
            while (hires.Count > 4 && hires.Any(x => x.done)) hires.Remove(hires.First(x => x.done));
            while (hires.Count > 4) hires.RemoveAt(0);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref rainRolled, "rainRolled", LookMode.Value);
            Scribe_Collections.Look(ref tides, "tides", LookMode.Deep);
            Scribe_Collections.Look(ref canopyRolled, "canopyRolled", LookMode.Value);
            Scribe_Collections.Look(ref burnSeen, "burnSeen", LookMode.Value);
            Scribe_Collections.Look(ref blightRolled, "blightRolled", LookMode.Value);
            Scribe_Collections.Look(ref afters, "afters", LookMode.Deep);
            Scribe_Collections.Look(ref contracts, "contracts", LookMode.Deep);
            Scribe_Values.Look(ref nextContract, "nextContract", 1);
            Scribe_Collections.Look(ref veterans, "veterans", LookMode.Deep);
            Scribe_Collections.Look(ref hires, "hires", LookMode.Deep);
            Scribe_Values.Look(ref lastRain, "lastRain", -1);
            Scribe_Values.Look(ref lastCanopy, "lastCanopy", -1);
            Scribe_Values.Look(ref lastAfter, "lastAfter", -1);
            Scribe_Values.Look(ref lastHire, "lastHire", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                rainRolled ??= new List<string>();
                tides ??= new List<TideRecord>();
                tides.RemoveAll(r => r?.map == null);
                canopyRolled ??= new List<string>();
                burnSeen ??= new List<string>();
                blightRolled ??= new List<string>();
                afters ??= new List<AfterRecord>();
                afters.RemoveAll(r => r?.map == null);
                contracts ??= new List<HiredContract>();
                contracts.RemoveAll(r => r == null);
                veterans ??= new List<VeteranRecord>();
                veterans.RemoveAll(r => r?.pawn == null);
                hires ??= new List<HireRecord>();
                hires.RemoveAll(r => r?.map == null);
            }
        }
    }
}
