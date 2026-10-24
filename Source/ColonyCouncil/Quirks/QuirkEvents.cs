using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a colonist saw with their own eyes, for the quirks that come from a sight rather than a memory
    /// (QuirkDef.afterSeen): "pyre", the enemy dead burning beyond the gate (PlayOut/OnMapTimed.cs), for those awake and
    /// near enough to see it and smell it. Looked at once an hour; held only for this session and a few days.
    /// </summary>
    public static class QuirkSights
    {
        public const string Pyre = "pyre";

        /// <summary>The sights a quirk can name.</summary>
        public static readonly string[] Known = { Pyre };

        /// <summary>Close enough to the pyre to see it burn and breathe its smoke.</summary>
        public const float PyreRange = 40f;

        /// <summary>A sight older than this is forgotten.</summary>
        private const int KeepTicks = GenDate.TicksPerDay * 5;

        private static readonly Dictionary<string, Dictionary<int, int>> seen = new Dictionary<string, Dictionary<int, int>>();
        private static Game game;

        public static bool IsKnown(string sight) => Array.IndexOf(Known, sight) >= 0;

        private static void Check()
        {
            if (game == Current.Game) return;
            game = Current.Game;
            seen.Clear();
        }

        public static void Saw(Pawn p, string sight)
        {
            if (p == null || sight == null) return;
            Check();
            if (!seen.TryGetValue(sight, out var who)) seen[sight] = who = new Dictionary<int, int>();
            who[p.thingIDNumber] = Find.TickManager.TicksGame;
        }

        /// <summary>Whether they saw one of these within <paramref name="window"/> ticks.</summary>
        public static bool SawAny(Pawn p, List<string> sights, int window)
        {
            if (p == null || sights.NullOrEmpty()) return false;
            Check();
            int now = Find.TickManager.TicksGame;
            foreach (var s in sights)
                if (s != null && seen.TryGetValue(s, out var who) && who.TryGetValue(p.thingIDNumber, out int t) && now - t < window)
                    return true;
            return false;
        }

        /// <summary>Once an hour (QuirkTracker.Hourly): who is watching a pyre burn.</summary>
        public static void Hourly()
        {
            try
            {
                Check();
                var timed = OnMapTimed.Instance?.kept;
                if (timed != null)
                {
                    foreach (var k in timed)
                    {
                        if (k == null || k.kind != TimedKind.Pyre || !k.on || k.map == null || !k.map.IsPlayerHome || !k.cell.IsValid) continue;
                        foreach (var p in k.map.mapPawns.FreeColonistsSpawned.ToList())
                            if (!p.Dead && !p.Downed && p.Awake() && p.Position.InHorDistOf(k.cell, PyreRange))
                                Saw(p, Pyre);
                    }
                }
                int now = Find.TickManager.TicksGame;
                foreach (var who in seen.Values)
                    if (who.Count > 0) who.RemoveAll(kv => now - kv.Value >= KeepTicks);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quirk sights", e);
            }
        }
    }

    /// <summary>
    /// The small effects of the quirks the colony's events leave behind (Defs/QuirkDefs/Rimfolk_Quirks_Events.xml): one
    /// who keeps a lamp lit for travelers, after taking in refugees or being taken in, leaves visitors who stay the night
    /// on better terms with the colony (+1 goodwill, once a visit); one who learned to juggle from the players or a camp's
    /// show cheers up those eating or resting near them (a little recreation, not mood); one who keeps a dream book since
    /// a dream came true dreams more often and writes every dream down (Dreams.cs); one who can't stand the smell of smoke
    /// since the pyre minds an open fire nearby (a mood, QuirkCondition.NearFire).
    /// </summary>
    public static class QuirkEvents
    {
        private static bool looked;
        private static QuirkDef lamp, juggles, dreamBook;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            lamp = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_LampForTravelers");
            juggles = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_Juggles");
            dreamBook = DefDatabase<QuirkDef>.GetNamedSilentFail("Council_Quirk_DreamBook");
        }

        private static bool On => ColonyCouncilMod.Settings?.quirks == true && Current.Game != null;

        private static bool Has(Pawn p, QuirkDef q) => q != null && p != null && CouncilGameComponent.Instance?.quirks?.Get(p, q) != null;

        // ---------------- The dream book ----------------

        /// <summary>How much likelier a dream is on waking for one who keeps a dream book.</summary>
        public const float DreamBookFactor = 1.5f;

        /// <summary>For the chance of a dream on waking (Dreams.cs).</summary>
        public static float DreamFactor(Pawn p)
        {
            Look();
            return On && Has(p, dreamBook) ? DreamBookFactor : 1f;
        }

        /// <summary>They woke from a dream in the last few hours (and haven't told it yet): the dream book comes out.</summary>
        public static bool JustDreamt(Pawn p)
        {
            var untold = CouncilGameComponent.Instance?.dreams?.untold;
            if (p == null || untold == null || !untold.TryGetValue(p, out var m) || m == null) return false;
            return Find.TickManager.TicksGame - m.tick < GenDate.TicksPerHour * 6;
        }

        /// <summary>Every dream goes in their journal, not only the ones that matter (Dreams.cs).</summary>
        public static bool WritesDreamsDown(Pawn p)
        {
            Look();
            return On && Has(p, dreamBook);
        }

        // ---------------- An open fire nearby ----------------

        /// <summary>How near an open fire has to be to bother one who can't stand the smoke.</summary>
        public const float SmokeRadius = 4.9f;

        /// <summary>Awake within a few steps of a burning fire, or a campfire, hearth, brazier or torch that is lit.</summary>
        public static bool NearOpenFire(Pawn p)
        {
            if (p == null || !p.Spawned || !p.Awake() || p.Map == null) return false;
            try
            {
                foreach (var t in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, SmokeRadius, true))
                {
                    if (t is Fire) return true;
                    if (!(t is ThingWithComps tc) || tc.GetComp<CompFireOverlay>() == null) continue;
                    var fuel = tc.GetComp<CompRefuelable>();
                    if (fuel == null || fuel.HasFuel) return true;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("near an open fire", e);
            }
            return false;
        }

        // ---------------- Once an hour ----------------

        public static void Hourly()
        {
            Look();
            if (!On) return;
            Lamp();
            Juggle();
        }

        // ---------------- A lamp for travelers ----------------

        /// <summary>The goodwill a visiting people's night under the lamp is worth.</summary>
        public const int LampGoodwill = 1;

        /// <summary>A people's visit counts once in this long (a stay of a night or two).</summary>
        private const int LampRest = GenDate.TicksPerDay * 2;

        private static readonly Dictionary<int, int> lampFor = new Dictionary<int, int>();
        private static Game lampGame;

        /// <summary>
        /// At night, on each home map with someone at home who keeps the lamp lit: each friendly people with visitors or
        /// a caravan on the map warms to the colony a little, once a visit.
        /// </summary>
        private static void Lamp()
        {
            if (lamp == null) return;
            try
            {
                if (lampGame != Current.Game)
                {
                    lampGame = Current.Game;
                    lampFor.Clear();
                }
                var t = CouncilGameComponent.Instance?.quirks;
                if (t == null) return;
                int now = Find.TickManager.TicksGame;
                foreach (var map in Find.Maps)
                {
                    if (!map.IsPlayerHome) continue;
                    int hour = GenLocalDate.HourOfDay(map);
                    if (hour < 20 && hour >= 4) continue;
                    Pawn keeper = null;
                    foreach (var q in t.quirks)
                        if (q?.def == lamp && q.pawn != null && q.pawn.Spawned && q.pawn.Map == map && !q.pawn.Dead && !q.pawn.Downed)
                        {
                            keeper = q.pawn;
                            break;
                        }
                    if (keeper == null) continue;
                    var guests = Strangers.On(map, StrangerKind.Visitor).Concat(Strangers.On(map, StrangerKind.Trader)).ToList();
                    foreach (var f in guests.Select(g => g.Faction).Distinct().ToList())
                    {
                        if (f == null || f.IsPlayer || f.defeated || !f.HasGoodwill || f.HostileTo(Faction.OfPlayer)) continue;
                        if (lampFor.TryGetValue(f.loadID, out int at) && now - at < LampRest) continue;
                        lampFor[f.loadID] = now;
                        f.TryAffectGoodwillWith(Faction.OfPlayer, LampGoodwill, false, false);
                        News.Post("CC_Quirk_LampSeen".Translate(keeper.Named("PAWN"), f.Name.Named("FACTION")).Resolve(), keeper, NewsKind.People,
                            MessageTypeDefOf.SilentInput, Importance.Flavor);
                    }
                }
                if (lampFor.Count > 50) lampFor.RemoveAll(kv => now - kv.Value >= LampRest);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("lamp for travelers", e);
            }
        }

        // ---------------- Juggling at the table ----------------

        /// <summary>The recreation each of those near them has from a juggler's turn at the table.</summary>
        public const float JuggleJoy = 0.05f;

        /// <summary>A juggler gives a turn at most once in this long.</summary>
        private const int JuggleRest = GenDate.TicksPerHour * 8;

        private static readonly Dictionary<int, int> juggled = new Dictionary<int, int>();
        private static Game juggleGame;

        /// <summary>Eating or at their leisure, or at a meal or their leisure themselves: the ones near them have a show.</summary>
        private static bool AtEase(Pawn p) => p.CurJobDef == JobDefOf.Ingest || p.CurJob?.def?.joyKind != null;

        /// <summary>
        /// Once an hour: a juggler at a meal or at their leisure, with others of the colony eating or resting beside them
        /// (together, in the same room), keeps a few things in the air for them: each of those has a little recreation.
        /// </summary>
        private static void Juggle()
        {
            if (juggles == null) return;
            try
            {
                if (juggleGame != Current.Game)
                {
                    juggleGame = Current.Game;
                    juggled.Clear();
                }
                var t = CouncilGameComponent.Instance?.quirks;
                if (t == null) return;
                int now = Find.TickManager.TicksGame;
                foreach (var q in t.quirks.Where(x => x?.def == juggles).ToList())
                {
                    var p = q.pawn;
                    if (p == null || !Together.Able(p) || p.Map == null || !p.Map.IsPlayerHome || !AtEase(p)) continue;
                    if (juggled.TryGetValue(p.thingIDNumber, out int at) && now - at < JuggleRest) continue;
                    var room = p.GetRoom();
                    int cheered = 0;
                    foreach (var o in p.Map.mapPawns.FreeColonistsSpawned.ToList())
                    {
                        // together: the juggler and those watching are side by side at the table or at their leisure.
                        if (o == p || !Together.Can(p, o) || o.GetRoom() != room || !AtEase(o) || o.needs?.joy == null) continue;
                        o.needs.joy.GainJoy(JuggleJoy, JoyKindDefOf.Social);
                        cheered++;
                    }
                    if (cheered == 0) continue;
                    juggled[p.thingIDNumber] = now;
                    News.Post("CC_Quirk_JuggledAtTable".Translate(p.Named("PAWN"), cheered.Named("COUNT")).Resolve(), p, NewsKind.People,
                        MessageTypeDefOf.SilentInput, Importance.Flavor);
                }
                if (juggled.Count > 50) juggled.RemoveAll(kv => now - kv.Value >= JuggleRest);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("juggling at the table", e);
            }
        }
    }
}
