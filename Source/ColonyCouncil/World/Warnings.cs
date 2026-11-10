using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A people's record of a threat they were warned of (any letter to any of them, read): kept so the same threat is
    /// warned of once per people, and so a debt from warnings comes once a season (World/Warnings.cs).
    /// </summary>
    public class WarnedThreat : IExposable
    {
        public Faction people;
        /// <summary>The threat's key (WarnMath.RaidKey, SiteKey, SitKey).</summary>
        public string key;
        /// <summary>When their notable read the warning.</summary>
        public int tick;
        /// <summary>What the warning was worth to them (WarnMath sizes); 0 for an older save's.</summary>
        public int size;
        /// <summary>It was heeded warmly and made (or renewed) a debt of honour.</summary>
        public bool debt;
        /// <summary>The threat struck them afterwards (WarnMath.Struck): counted once.</summary>
        public bool struck;
        /// <summary>Whose threat it was (the raiders, a site's people, a war's attacker), when it was a people's.</summary>
        public Faction by;

        public void ExposeData()
        {
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref size, "size");
            Scribe_Values.Look(ref debt, "debt");
            Scribe_Values.Look(ref struck, "struck");
            Scribe_References.Look(ref by, "by");
        }
    }

    /// <summary>A threat the colony heard of in talk with outsiders (point 10 of the player's list, 10 November 2026): it may be warned of.</summary>
    public class KnownThreat : IExposable
    {
        public string key;
        public int tick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref tick, "tick");
        }
    }

    /// <summary>
    /// Warning a people of a threat (the player, 10 November 2026: "warning about raiders should only be an option if the
    /// target colony is actually a potential target for these raiders and not too much time has expired"). What threats
    /// the colony knows of are a real threat to one of their towns now (WarnMath: raiders by their reach, a hostile site
    /// near, a war or road trouble near, a hardship spreading toward them), what each is worth to them heeded warmly, the
    /// people-wide record of what they were warned of (once per threat, one debt from warnings a season), the threat
    /// striking them afterwards, and the threats heard of in talk. Its records are saved in the neighbours' book
    /// (NotableBook.warned, NotableBook.known); the threats themselves are read from the map and worked out at most once
    /// an hour for each people.
    /// </summary>
    public static class Warnings
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static NotableBook Book => NotableBook.Instance;

        /// <summary>A threat to a people now.</summary>
        public sealed class Threat
        {
            public ThreatKind kind;
            public string key;
            public string text;
            public int size;
            public Faction by;
            public Settlement town;
        }

        /// <summary>Raiders the colony knows of that are no warning to them, and why (the letter dialog says so).</summary>
        public sealed class Unlisted
        {
            public Faction raiders;
            public WarnWhy why;
        }

        /// <summary>What a warning read now is worth to them.</summary>
        public sealed class Heeding
        {
            /// <summary>The threat still stands; else it is thanks and a memory only.</summary>
            public bool current;
            public ThreatKind kind;
            public int size;
            /// <summary>Heeded warmly it makes a debt: worth one, and none from warnings this season.</summary>
            public bool debt;
            public Faction by;
        }

        private sealed class Kept
        {
            public int hour = -1;
            public int known = -1;
            public List<Threat> threats = new List<Threat>();
            public List<Unlisted> unlisted = new List<Unlisted>();
        }

        private static readonly Dictionary<int, Kept> cache = new Dictionary<int, Kept>();
        private static Game cacheGame;
        private static int knownVersion;

        // ================= What they can be warned of =================

        /// <summary>Every threat to them now the colony knows of (warned of or not), weightiest first.</summary>
        public static List<Threat> Threats(Faction f) => Look(f)?.threats ?? new List<Threat>();

        /// <summary>Raiders the colony knows of that are no warning to them now, with why.</summary>
        public static List<Unlisted> NotListed(Faction f) => Look(f)?.unlisted ?? new List<Unlisted>();

        /// <summary>What a letter to this notable can warn of: the threats to their people not warned of yet, by anyone's letter.</summary>
        public static List<LetterTopic> Topics(Notable n)
        {
            var list = new List<LetterTopic>();
            if (n?.faction == null) return list;
            foreach (var t in Threats(n.faction))
            {
                if (Warned(n.faction, t.key) || n.done.Contains(t.key) || UnderWay(n.faction, t.key)) continue;
                list.Add(new LetterTopic { key = t.key, text = t.text });
            }
            return list;
        }

        /// <summary>Why there is nothing to warn them of now, in words: the first raiders left out and why, else nothing known.</summary>
        public static string WhyNone(Notable n)
        {
            var f = n?.faction;
            if (f == null) return null;
            var u = NotListed(f).FirstOrDefault();
            if (u == null) return null;
            return ("CC_Warn_Why_" + u.why).Translate(u.raiders.Name.Named("FACTION"), f.Name.Named("PEOPLE")).Resolve();
        }

        /// <summary>The lines under the dialog's topics: each raiders the colony knows of not listed, and why.</summary>
        public static List<string> NotListedLines(Notable n)
        {
            var list = new List<string>();
            if (n?.faction == null) return list;
            foreach (var u in NotListed(n.faction).Take(3))
                list.Add(("CC_Warn_NotListed_" + u.why).Translate(u.raiders.Name.Named("FACTION"), n.faction.Name.Named("PEOPLE")).Resolve());
            return list;
        }

        private static Kept Look(Faction f)
        {
            if (f == null || Current.Game == null) return null;
            if (cacheGame != Current.Game)
            {
                cacheGame = Current.Game;
                cache.Clear();
            }
            int hour = Now / GenDate.TicksPerHour;
            if (cache.TryGetValue(f.loadID, out var k) && k.hour == hour && k.known == knownVersion) return k;
            k = new Kept { hour = hour, known = knownVersion };
            cache[f.loadID] = k;
            try
            {
                Gather(f, k);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("warnings: threats to a people", e);
            }
            return k;
        }

        /// <summary>Their towns standing now (the places a threat is measured to).</summary>
        private static List<Settlement> TownsOf(Faction f) =>
            Find.WorldObjects.Settlements.Where(s => s != null && s.Faction == f && !s.Destroyed && s.Tile.Valid).ToList();

        /// <summary>The nearest of their towns to a tile, with how far (-1 and null for none).</summary>
        private static float Nearest(List<Settlement> towns, PlanetTile tile, out Settlement town)
        {
            town = null;
            float best = -1f;
            if (!tile.Valid) return best;
            foreach (var s in towns)
            {
                if (s.Tile.Layer != tile.Layer) continue;
                float d = Find.WorldGrid.ApproxDistanceInTiles(s.Tile, tile);
                if (best < 0f || d < best)
                {
                    best = d;
                    town = s;
                }
            }
            return best;
        }

        private static void Gather(Faction f, Kept k)
        {
            var towns = TownsOf(f);
            if (towns.Count == 0) return;
            int now = Now;
            var threats = k.threats;
            var raiders = new HashSet<Faction>();
            // ---- Raiders: who struck a home lately, a raid of theirs on its way, a camp of theirs standing ----
            var lastRaid = new Dictionary<Faction, int>();
            var mem = FactionMemory.Instance;
            if (mem != null)
                foreach (var d in mem.deeds)
                    if (d != null && d.kind == "raided" && d.faction != null && now - d.tick <= 30 * GenDate.TicksPerDay)
                        lastRaid[d.faction] = Math.Max(lastRaid.TryGetValue(d.faction, out int was) ? was : -1, d.tick);
            var coming = new Dictionary<Faction, Map>();
            foreach (var r in CouncilGameComponent.Instance?.happenings?.raids ?? new List<PendingRaid>())
                if (r?.faction != null && r.map != null && !coming.ContainsKey(r.faction))
                    coming[r.faction] = r.map;
            var camps = new Dictionary<Faction, PlanetTile>();
            foreach (var (home, c) in RaidersCamps.Known())
                if (c?.people != null)
                {
                    var tile = RaidersCamps.TileOf(c);
                    if (tile.Valid) camps[c.people] = tile;
                }
            foreach (var g in lastRaid.Keys.Concat(coming.Keys).Concat(camps.Keys).Distinct())
            {
                if (g == null || g.IsPlayer || g == f) continue;
                bool recent = lastRaid.TryGetValue(g, out int at) && WarnMath.Recent((now - at) / (float)GenDate.TicksPerDay);
                bool standing = coming.ContainsKey(g) || camps.ContainsKey(g);
                float townTiles = -1f, campTiles = -1f, landTiles = -1f, homeTiles = -1f;
                Settlement near = null;
                if (g != f && g.HostileTo(f) && !g.defeated)
                {
                    foreach (var s in Find.WorldObjects.Settlements)
                    {
                        if (s == null || s.Faction != g || s.Destroyed) continue;
                        float d = Nearest(towns, s.Tile, out var t);
                        if (d >= 0f && (townTiles < 0f || d < townTiles))
                        {
                            townTiles = d;
                            near = t;
                        }
                    }
                    if (camps.TryGetValue(g, out var ct))
                    {
                        float cd = Nearest(towns, ct, out var ctTown);
                        if (cd >= 0f)
                        {
                            campTiles = cd;
                            if (cd <= WarnMath.NearTiles) near = ctTown;
                        }
                    }
                    foreach (var site in Find.WorldObjects.Sites)
                        if (site != null && site.Faction == g && Seen(site))
                        {
                            float d = Nearest(towns, site.Tile, out var st);
                            if (d >= 0f && (campTiles < 0f || d < campTiles))
                            {
                                campTiles = d;
                                if (d <= WarnMath.NearTiles) near = st;
                            }
                        }
                    // The home they struck or are coming to.
                    var homes = coming.TryGetValue(g, out var cm) ? new List<Map> { cm } : Homes.All.ToList();
                    foreach (var m in homes)
                        if (m != null && m.Tile.Valid)
                        {
                            float d = Nearest(towns, m.Tile, out var ht);
                            if (d >= 0f && (homeTiles < 0f || d < homeTiles)) homeTiles = d;
                            near ??= ht;
                        }
                    if (WarnMath.RaidersSize(townTiles, campTiles, -1f, homeTiles) <= 0) landTiles = Land(g, towns);
                }
                int size = WarnMath.RaidersSize(townTiles, campTiles, landTiles, homeTiles);
                var why = WarnMath.RaidersWhy(g == f, g != f && g.HostileTo(f), g.defeated, recent || standing, size);
                if (why != WarnWhy.None)
                {
                    k.unlisted.Add(new Unlisted { raiders = g, why = why });
                    continue;
                }
                raiders.Add(g);
                near ??= towns[0];
                string key = camps.ContainsKey(g) && campTiles >= 0f && campTiles <= WarnMath.NearTiles ? "CC_Letter_Topic_RaidsCamp"
                    : coming.ContainsKey(g) ? "CC_Letter_Topic_RaidsComing" : recent ? "CC_Letter_Topic_RaidsStruck" : "CC_Letter_Topic_Raids";
                threats.Add(new Threat
                {
                    kind = ThreatKind.Raiders, key = WarnMath.RaidKey(g.loadID), size = size, by = g, town = near,
                    text = key.Translate(g.Name.Named("FACTION"), near.Name.Named("TOWN")).Resolve()
                });
            }
            // ---- A hostile site near one of their towns, seen or heard of ----
            foreach (var site in Find.WorldObjects.Sites)
            {
                if (site?.Faction == null || raiders.Contains(site.Faction)) continue;
                float d = Nearest(towns, site.Tile, out var st);
                if (d < 0f || WarnMath.SiteWhy(site.Faction == f, site.Faction.HostileTo(f), d) != WarnWhy.None || !Seen(site)) continue;
                threats.Add(new Threat
                {
                    kind = ThreatKind.Site, key = WarnMath.SiteKey(site.ID), size = WarnMath.Size(ThreatKind.Site, 0), by = site.Faction, town = st,
                    text = "CC_Letter_Topic_Site".Translate(site.LabelCap.Named("SITE"), NeighbourLetters.Direction(st.Tile, site.Tile).Named("DIR"), st.Name.Named("TOWN")).Resolve()
                });
            }
            // ---- A war or road trouble near them; a hardship spreading toward them ----
            var running = WorldSituations.Instance?.Running?.ToList() ?? new List<WorldSituation>();
            foreach (var s in running)
            {
                if (s?.def == null || s.ctx == null || s.ended) continue;
                if (WarnMath.WarOrRoad(s.def.kind, s.def.bad))
                {
                    // Their own war (a side of it) they know of; the attacker is the side at war with them.
                    if (s.ctx.faction == f || s.ctx.enemy == f) continue;
                    var by = Attacker(s, f);
                    float d = Distance(s, by, towns, out var wt);
                    if (WarnMath.WarWhy(by == null || by == f, by != null && by.HostileTo(f), d) != WarnWhy.None || wt == null) continue;
                    threats.Add(new Threat
                    {
                        kind = ThreatKind.War, key = WarnMath.SitKey(s.id), size = WarnMath.Size(ThreatKind.War, 0), by = by, town = wt,
                        text = "CC_Letter_Topic_War".Translate(Called(s).Named("EVENT"), wt.Name.Named("TOWN")).Resolve()
                    });
                }
                else if (WarnMath.Spreading(s.def.defName, s.def.kind, s.def.bad))
                {
                    bool suffering = running.Any(o => o?.def == s.def && o.ctx?.faction == f && !o.ended);
                    float d = Distance(s, s.ctx.faction, towns, out var ht);
                    if (WarnMath.HardshipWhy(s.ctx.faction == f, suffering, d) != WarnWhy.None || ht == null) continue;
                    threats.Add(new Threat
                    {
                        kind = ThreatKind.Hardship, key = WarnMath.SitKey(s.id), size = WarnMath.Size(ThreatKind.Hardship, 0), town = ht,
                        text = "CC_Letter_Topic_Spreading".Translate(Called(s).Named("EVENT"), ht.Name.Named("TOWN")).Resolve()
                    });
                }
            }
            // Raiders who are a war's attacker near them are told as the war (the weightier warning), not twice.
            var warBy = new HashSet<Faction>(threats.Where(t => t.kind == ThreatKind.War && t.by != null).Select(t => t.by));
            threats.RemoveAll(t => t.kind == ThreatKind.Raiders && warBy.Contains(t.by));
            var order = WarnMath.Order(threats.Select(t => t.size).ToList());
            var sorted = order.Select(i => threats[i]).ToList();
            threats.Clear();
            threats.AddRange(sorted);
        }

        /// <summary>The raiders' own land (Faction Territories) nearest any of their towns, within WarnMath.TerritoryTiles; -1 for none or no such mod.</summary>
        private static float Land(Faction g, List<Settlement> towns)
        {
            if (WorldMechanics.Ready.Count == 0) return -1f;
            float best = -1f;
            try
            {
                var grid = Find.WorldGrid;
                foreach (var town in towns.Take(4))
                {
                    var root = town.Tile;
                    float max = WarnMath.TerritoryTiles;
                    root.Layer.Filler.FloodFill(root, t => grid.ApproxDistanceInTiles(root, t) <= max, (t, _) =>
                    {
                        if (WorldMechanics.TerritoryOwner(t) != g) return;
                        float d = grid.ApproxDistanceInTiles(root, t);
                        if (best < 0f || d < best) best = d;
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("warnings: the raiders' land", e);
            }
            return best;
        }

        /// <summary>A world event's attacker for them: its people, else its enemy, whichever is at war with them and not them.</summary>
        private static Faction Attacker(WorldSituation s, Faction f)
        {
            var a = s.ctx.faction;
            if (a != null && a != f && a.HostileTo(f)) return a;
            var b = s.ctx.enemy;
            if (b != null && b != f && b.HostileTo(f)) return b;
            return a ?? b;
        }

        /// <summary>How near a world event lies to their nearest town: its place, else the nearest town of its people; -1 for neither.</summary>
        private static float Distance(WorldSituation s, Faction people, List<Settlement> towns, out Settlement town)
        {
            town = null;
            var at = RiseFall.TileOf(s.place);
            if (at.Valid) return Nearest(towns, at, out town);
            if (people == null) return -1f;
            float best = -1f;
            foreach (var o in Find.WorldObjects.Settlements)
            {
                if (o == null || o.Faction != people || o.Destroyed) continue;
                float d = Nearest(towns, o.Tile, out var t);
                if (d >= 0f && (best < 0f || d < best))
                {
                    best = d;
                    town = t;
                }
            }
            return best;
        }

        private static string Called(WorldSituation s) =>
            !(s.ctx?.name).NullOrEmpty() ? s.ctx.name : Find.ActiveLanguageWorker.WithIndefiniteArticle(s.def?.label ?? "?");

        /// <summary>
        /// The colony knows of a site: within its region's reach of a home (its people about the land see it), a raiders' camp
        /// it found, or one heard of in talk with outsiders (<see cref="Heard"/>).
        /// </summary>
        public static bool Seen(Site site)
        {
            if (site == null || !site.Tile.Valid) return false;
            if (Knows(WarnMath.SiteKey(site.ID))) return true;
            if (RaidersCamps.CampAt(site, out _) is RaidersCamp c && c.Shown) return true;
            float reach = RegionTowns.ReachTiles;
            foreach (var m in Homes.All)
                if (m != null && m.Tile.Valid && m.Tile.Layer == site.Tile.Layer && Find.WorldGrid.ApproxDistanceInTiles(m.Tile, site.Tile) <= reach)
                    return true;
            return false;
        }

        // ================= Their record =================

        /// <summary>They were warned of it already (any letter to any of them, read).</summary>
        public static bool Warned(Faction f, string key) => Book?.warned.Any(w => w != null && w.people == f && w.key == key) ?? false;

        /// <summary>A letter warning of it to any of them is on its way.</summary>
        private static bool UnderWay(Faction f, string key)
        {
            var book = Book;
            if (book == null) return false;
            foreach (var l in book.letters)
                if (l != null && l.Open && l.purpose == LetterPurpose.Warning && l.topicKey == key && book.Get(l.notable)?.faction == f)
                    return true;
            return false;
        }

        /// <summary>The last debt from a warning of theirs (-1: none).</summary>
        private static int LastDebt(Faction f)
        {
            int last = -1;
            foreach (var w in Book?.warned ?? new List<WarnedThreat>())
                if (w != null && w.people == f && w.debt)
                    last = Math.Max(last, w.tick);
            return last;
        }

        /// <summary>The threat a key names, if it still stands for them.</summary>
        public static Threat Standing(Faction f, string key) => key == null ? null : Threats(f).FirstOrDefault(t => t.key == key);

        /// <summary>
        /// Their notable reads a warning now: is the threat still standing, what is it worth to them, and may it make a debt
        /// (WarnMath.MakesDebt, and none from warnings this season: WarnMath.DebtAllowed).
        /// </summary>
        public static Heeding Heed(Faction f, string key)
        {
            var h = new Heeding();
            var t = Standing(f, key);
            if (t == null) return h;
            h.current = true;
            h.kind = t.kind;
            h.size = t.size;
            h.by = t.by;
            h.debt = WarnMath.MakesDebt(t.kind, t.size) && WarnMath.DebtAllowed(LastDebt(f), Now);
            return h;
        }

        /// <summary>The warning read: their people's record of it (once per threat), with what it was worth and whether it made a debt.</summary>
        public static void Record(Faction f, string key, Heeding h, bool debt)
        {
            var book = Book;
            if (book == null || f == null || key.NullOrEmpty()) return;
            book.warned.RemoveAll(w => w == null || w.people == f && w.key == key);
            book.warned.Add(new WarnedThreat { people = f, key = key, tick = Now, size = h?.size ?? 0, debt = debt, by = h?.by });
            while (book.warned.Count > WarnMath.MaxKept) book.warned.RemoveAt(0);
            NotableBook.Changed();
        }

        // ================= Heard of in talk =================

        /// <summary>The colony knows of this threat from news heard in talk (a site, a world event).</summary>
        public static bool Knows(string key) => key != null && (Book?.known.Any(k => k != null && k.key == key) ?? false);

        /// <summary>
        /// News heard in talk named a threat (OutsiderTalk.Heard: the fact behind its placeholder, "site:345", "ws:7"): the colony
        /// knows of it from now, and it can be warned of (point 10).
        /// </summary>
        public static void Heard(string fact)
        {
            string key = WarnMath.FromFact(fact);
            var book = Book;
            if (key == null || book == null) return;
            book.known.RemoveAll(k => k == null || k.key == key);
            book.known.Add(new KnownThreat { key = key, tick = Now });
            while (book.known.Count > WarnMath.MaxKnown) book.known.RemoveAt(0);
            knownVersion++;
        }

        // ================= The day's look =================

        /// <summary>
        /// Once a day (the neighbours' day): an older save's warnings moved from each notable to their people's record, the
        /// records past their days let go, the threat that struck a people it was warned of counted once more (WarnMath.Struck:
        /// the debt grows by 4, said in a line of news), and the threats heard of long ago forgotten.
        /// </summary>
        public static void Daily(int now)
        {
            var book = Book;
            if (book == null) return;
            // An older save: the threats each notable was warned of are their people's now (and can't come back to another of them).
            foreach (var n in book.notables)
            {
                if (n?.faction == null || n.done == null) continue;
                foreach (var key in n.done.Where(WarnMath.IsWarningKey).ToList())
                {
                    if (!book.warned.Any(w => w != null && w.people == n.faction && w.key == key))
                        book.warned.Add(new WarnedThreat { people = n.faction, key = key, tick = now });
                    n.done.Remove(key);
                }
            }
            book.warned.RemoveAll(w => w?.people == null || w.key == null);
            foreach (var w in book.warned.ToList())
            {
                if (!w.struck && w.by != null && w.size > 0 && WarnMath.Struck(w.tick, now) && StruckBy(w.people, w.by)) Struck(w);
                if (WarnMath.LetGo(w.tick, now, Standing(w.people, w.key) != null, WarnMath.KeepDays)) book.warned.Remove(w);
            }
            book.known.RemoveAll(k => k == null || WarnMath.LetGo(k.tick, now, false, WarnMath.KnownDays));
        }

        /// <summary>A town of theirs within a home's reach under attack now by that people (CallsToArms.Attacked).</summary>
        private static bool StruckBy(Faction f, Faction by)
        {
            foreach (var m in Homes.All)
                if (CallsToArms.Attacked(f, m, out _, out var attacker) && attacker == by)
                    return true;
            return false;
        }

        /// <summary>The warned threat struck them: the warning counts the more (its debt 4 more, else a little more remembered), and the news says so.</summary>
        private static void Struck(WarnedThreat w)
        {
            w.struck = true;
            int grown = w.size + WarnMath.StruckBonus;
            bool debtGrew = false;
            if (w.debt)
                foreach (var t in FavoursOwed.OwedBy(w.people))
                    if (t.state == Tie.State.Open && !FavoursOwed.IsPersonal(t) && t.terms.key == "warned" && t.terms.number < grown)
                    {
                        t.terms.number = grown;
                        WorldLedger.Touch(t);
                        debtGrew = true;
                    }
            if (!debtGrew) FavoursOwed.Quietly(() => FactionMemory.Note(w.people, "warned", w.by?.Name, WarnMath.StruckBonus));
            // apart: the threat struck their town off the map; the colony hears of it in the news.
            Tell.Minor(NewsKind.World, Importance.Notable, Homes.Seat,
                (debtGrew ? "CC_Warn_Struck_Debt" : "CC_Warn_Struck").Translate(w.people.Name.Named("PEOPLE"), (w.by?.Name ?? "?").Named("FACTION")).Resolve(), LookTargets.Invalid);
            NotableBook.Changed();
        }
    }
}
