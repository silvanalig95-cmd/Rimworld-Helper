using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One home's part in the wild's alphas and bosses: when its last alpha came, the last big threat looked at, and its great herd's season.</summary>
    public class BeastHome : IExposable
    {
        public int home = HomesMath.None;
        /// <summary>When the last pack on its land got an alpha, and the last pack of the game's manhunters.</summary>
        public int lastAlpha = -1, lastManhunterAlpha = -1;
        /// <summary>The storyteller's last big threat looked at for a manhunters' alpha (its tick).</summary>
        public int threatLooked = -1;
        /// <summary>The season (year * 4 + season) its great herd was decided for, and when it comes (-1: none this season).</summary>
        public int herdSeason = -1, herdAt = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref lastAlpha, "lastAlpha", -1);
            Scribe_Values.Look(ref lastManhunterAlpha, "lastManhunterAlpha", -1);
            Scribe_Values.Look(ref threatLooked, "threatLooked", -1);
            Scribe_Values.Look(ref herdSeason, "herdSeason", -1);
            Scribe_Values.Look(ref herdAt, "herdAt", -1);
        }
    }

    /// <summary>
    /// The wild's alphas and bosses, kept with the game (Beasts/; DESIGN.md "Alphas, nemeses, legends and great herds"):
    /// the packs led by a named alpha (<see cref="PackAlphas"/>), the beast nemeses that fled hurt and the oaths sworn
    /// against them (<see cref="BeastNemeses"/>), each home's alpha and herd season, and when the last legend of the region
    /// ended (<see cref="LegendWorker"/>; the legend itself is a world event). The herds crossing a map are read from it each
    /// time and not saved. Its clock: every 250 ticks at <see cref="Sched.RareBeasts"/> (only what is on a home map), its
    /// hour on the same tick, its day in the first hour of the day. A save from before has an empty one.
    /// </summary>
    public class BeastBook : GameComponent
    {
        public List<PackAlpha> alphas = new List<PackAlpha>();
        public List<BeastFlight> flights = new List<BeastFlight>();
        public List<BeastOath> oaths = new List<BeastOath>();
        public List<BeastHome> homes = new List<BeastHome>();
        /// <summary>Beast nemeses already given their scars and their oath (thingIDNumber), the newest kept.</summary>
        public List<int> noted = new List<int>();
        /// <summary>When the last legend of the region began and ended (ticks, -1: never).</summary>
        public int legendStarted = -1, legendEnded = -1;

        private static BeastBook cached;
        private static Game cachedGame;

        public BeastBook(Game game)
        {
        }

        public static BeastBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<BeastBook>();
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
            GreatHerds.Reset();
        }

        internal static CouncilSettings S => ColonyCouncilMod.Settings;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>A home's part, made when it has none.</summary>
        public BeastHome HomeOf(Map map)
        {
            int key = Homes.KeyOf(map);
            if (key == HomesMath.None) return null;
            homes ??= new List<BeastHome>();
            var h = homes.FirstOrDefault(x => x != null && x.home == key);
            if (h == null) homes.Add(h = new BeastHome { home = key });
            return h;
        }

        // ---------------- The clock ----------------

        public override void GameComponentTick()
        {
            var tm = Find.TickManager;
            if (tm == null) return;
            int now = tm.TicksGame;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareBeasts)) return;
            using (PassClock.Time(Pass.RareBeasts))
                Safe("the short pass", () => Rare(now));
            if (!Sched.Hourly(now, Sched.RareBeasts)) return;
            using (PassClock.Time(Pass.Beasts))
            {
                Safe("the hour", () => Hourly(now));
                if (Sched.FirstHourOfDay(now)) Safe("the day", () => Daily(now));
            }
        }

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("beasts: " + what, e);
            }
        }

        /// <summary>Every 250 ticks: what must be seen at once (an alpha down, a hurt beast turning, a herd spooked, someone in a stampede's way).</summary>
        private void Rare(int now)
        {
            if (alphas.Count > 0) Safe("alphas", () => PackAlphas.Rare(this, now));
            Safe("manhunters' alpha", () => PackAlphas.WatchThreats(this, now));
            Safe("beast nemeses", () => BeastNemeses.Rare(this, now));
            Safe("herds", () => GreatHerds.Rare(this, now));
            // The colony's animals hunting the wild (Animals/PetHunts.cs): the hunts under way kept to the rules, and the
            // hungry ones with nothing in reach looked at for their alert.
            Safe("pets hunting", () => PetHunts.Rare(now));
            // Bleeding animals marked for their body when they die (Animals/BodyWhenItDies.cs): an order dropped once one
            // stops bleeding, is tamed, joins the colony or leaves the map.
            Safe("the body when it dies", () => BodyWhenItDies.Rare(now));
        }

        private void Hourly(int now)
        {
            if (alphas.Count > 0) Safe("alphas' hour", () => PackAlphas.Hourly(this, now));
            Safe("beast nemeses' hour", () => BeastNemeses.Hourly(this, now));
        }

        private void Daily(int now)
        {
            foreach (var m in Homes.All.ToList())
            {
                if (m == null || MapFacts.InVacuum(m)) continue;
                Safe("a pack's alpha", () => PackAlphas.Daily(this, m, now));
                Safe("the great herd", () => GreatHerds.Daily(this, m, now));
            }
            Safe("beast nemeses' day", () => BeastNemeses.Daily(this, now));
            Trim();
        }

        /// <summary>Ended stories go first, the oldest first, past each list's cap.</summary>
        private void Trim()
        {
            while (alphas.Count > BeastsMath.MaxAlphaRecords)
            {
                var old = alphas.Where(a => !a.Open).OrderBy(a => a.endTick).FirstOrDefault() ?? alphas.OrderBy(a => a.madeTick).First();
                alphas.Remove(old);
            }
            while (flights.Count > BeastsMath.MaxFlights) flights.RemoveAt(0);
            while (oaths.Count > BeastsMath.MaxOaths)
            {
                var old = oaths.FirstOrDefault(o => o.ended) ?? oaths[0];
                oaths.Remove(old);
            }
            while (noted.Count > BeastsMath.MaxNoted) noted.RemoveAt(0);
            homes.RemoveAll(h => h == null || (h.home != HomesMath.None && Homes.MapOf(h.home) == null && Now - Math.Max(h.lastAlpha, h.herdAt) > GenDate.TicksPerYear));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref alphas, "alphas", LookMode.Deep);
            Scribe_Collections.Look(ref flights, "flights", LookMode.Deep);
            Scribe_Collections.Look(ref oaths, "oaths", LookMode.Deep);
            Scribe_Collections.Look(ref homes, "homes", LookMode.Deep);
            Scribe_Collections.Look(ref noted, "noted", LookMode.Value);
            Scribe_Values.Look(ref legendStarted, "legendStarted", -1);
            Scribe_Values.Look(ref legendEnded, "legendEnded", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                alphas ??= new List<PackAlpha>();
                alphas.RemoveAll(a => a == null || (a.alpha == null && a.Open));
                flights ??= new List<BeastFlight>();
                flights.RemoveAll(f => f == null || f.beast == null);
                oaths ??= new List<BeastOath>();
                oaths.RemoveAll(o => o == null || o.sworn == null);
                homes ??= new List<BeastHome>();
                homes.RemoveAll(h => h == null);
                noted ??= new List<int>();
            }
        }

        // ---------------- Shared ----------------

        /// <summary>The colonist who dealt the killing blow to a dead animal, if its wounds' log says (as a nemesis's slayer is read).</summary>
        public static Pawn Slayer(Pawn dead)
        {
            var hediffs = dead?.health?.hediffSet?.hediffs;
            if (hediffs == null) return null;
            foreach (var h in hediffs.Where(h => h.combatLogEntry != null).OrderBy(h => h.ageTicks).Take(5))
            {
                var k = h.combatLogEntry.Target?.GetConcerns().FirstOrDefault() as Pawn;
                if (k != null && k != dead && k.Faction == Faction.OfPlayer && k.RaceProps != null && k.RaceProps.Humanlike) return k;
            }
            return null;
        }

        /// <summary>Up and about: alive, on a map, not downed. A downed or dying beast never gets a name, a letter or a "got away" line.</summary>
        public static bool Up(Pawn x) => x != null && !x.Dead && !x.Destroyed && x.Spawned && !x.Downed;

        /// <summary>Someone of the colony who could name a beast or speak of it: grown, with an inner life, on the map.</summary>
        public static Pawn Voice(Map map, Pawn near = null)
        {
            var people = IncidentWorker_Happening.Colonists(map).Where(p => People.HasInnerLife(p) && !p.Downed && !p.DevelopmentalStage.Baby()).ToList();
            if (people.Count == 0) return null;
            if (near != null && near.Spawned && near.Map == map)
            {
                var seen = people.Where(p => p.Position.InHorDistOf(near.Position, 24f) && GenSight.LineOfSight(p.Position, near.Position, map)).ToList();
                if (seen.Count > 0) return seen.RandomElement();
            }
            return people.Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback() ?? people.RandomElement();
        }

        /// <summary>A name for a beast from one of the pools, picked in the voice of someone of the colony; null when none fits.</summary>
        public static string NameFrom(string pool, Pawn voice, params string[] tags)
        {
            if (voice == null) return null;
            string n = Flavor.FromPool(pool, voice, null, null, tags);
            return n.NullOrEmpty() ? null : n.Trim();
        }
    }
}
