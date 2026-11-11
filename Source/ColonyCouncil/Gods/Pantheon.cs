using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The nine, made from their defs and kept right (docs/design/gods.md, "the contract"):
    /// <list type="bullet">
    /// <item>The pantheon is the nine GodDefs (Defs/GodDefs/Rimfolk_Gods.xml), one God each, in the order of
    /// <see cref="GodLoreMath.Keys"/>. Ideology's deities are ignored for now (step 1 of the design's Ideology plan).</item>
    /// <item>An older save's generated gods (0.215.0 and 0.216.0) are dropped on load, with their standings, acts, ties and
    /// vows: the nine take their place, the colony's standing with each unknown.</item>
    /// <item>The ties are the defs' own, made again on every load, and never shift.</item>
    /// <item>Who holds a god: the peoples its def names, else those of the kinds it names (GodLoreMath.Reveres); the colony
    /// by its own age.</item>
    /// <item>What the colony knows (hidden state, never shown): the gods of its own people, its neighbours', its visitors'.</item>
    /// </list>
    /// </summary>
    public static class Pantheon
    {
        /// <summary>The book's version: 1 is the nine (an older save's generated pantheon is 0).</summary>
        public const int Nine = 1;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>A people with gods: of humans, not gone, not one of the game's hidden ones (the colony always).</summary>
        public static bool Fits(Faction f) => f != null && !f.defeated && f.def != null && f.def.humanlikeFaction && (f.IsPlayer || !f.Hidden);

        private static IEnumerable<Faction> Factions() =>
            Find.FactionManager?.AllFactionsListForReading.Where(Fits).ToList() ?? new List<Faction>();

        /// <summary>The age a people's names for the gods come from (0 tribal to 3 spacer): the colony's own, or the people's.</summary>
        public static int AgeOf(Faction f) => f == null || f.IsPlayer ? (int)Ages.Ours : Peoples.AgeOf(f);

        /// <summary>The nine's defs, in the order of their keys.</summary>
        public static List<GodDef> Defs() =>
            GodLoreMath.Keys.Select(GodDef.Of).Where(d => d != null).Concat(DefDatabase<GodDef>.AllDefsListForReading.Where(d => !GodLoreMath.Keys.Contains(d.key))).ToList();

        // ================= Building =================

        /// <summary>
        /// The nine for this world: an older save's generated gods let go first (and told once, in the world's voice), then a
        /// god for each def, the ties, who holds them and what the colony knows.
        /// </summary>
        public static void Build(GodBook b)
        {
            if (b == null) return;
            int now = Now;
            bool older = b.gods.Count > 0 && b.version < Nine;
            if (b.version < Nine)
            {
                b.gods.Clear();
                b.ties.Clear();
                b.standings.Clear();
                b.acts.Clear();
                b.vows.Clear();
                b.nextId = 1;
                b.newborns = 0;
                b.lastNewborn = -1;
            }
            foreach (var def in Defs())
            {
                if (b.gods.Any(g => g.def == def)) continue;
                var g = new God { def = def, born = now, lastHeld = now, form = FormFor(def) };
                g.FromDef();
                b.Add(g);
            }
            b.built = true;
            b.version = Nine;
            if (older) b.retired = true;
            Keep(b, now, false);
        }

        /// <summary>The form the colony pictures a god in, by its own age (the presence part's noun: a spirit, an old god).</summary>
        private static GodForm FormFor(GodDef def) => GodsMath.FormOf((int)Ages.Ours, def.domain, def.second, GodsMath.Seed(def.key ?? def.defName));

        // ================= Ties =================

        /// <summary>The ties the defs author, each once (made again on every load, so a def's change reaches an old save).</summary>
        public static void Ties(GodBook b)
        {
            int now = Now;
            var list = new List<GodTie>();
            foreach (var g in b.gods)
            {
                if (g.def?.ties == null) continue;
                foreach (var spec in g.def.ties)
                {
                    var other = b.gods.FirstOrDefault(x => x.Key == spec?.other);
                    if (other == null || other == g || list.Any(t => t.Has(g.id) && t.Has(other.id))) continue;
                    var had = b.Tie(g.id, other.id);
                    list.Add(new GodTie { a = g.id, b = other.id, kind = spec.kind, strength = Math.Max(0.1f, Math.Min(1f, spec.strength)), since = had?.since ?? now });
                }
            }
            b.ties = list;
        }

        /// <summary>The words of a tie, as people say it (its def's), or null.</summary>
        public static string TieWords(GodTie t)
        {
            var a = Gods.ById(t?.a ?? -1);
            var c = Gods.ById(t?.b ?? -1);
            var spec = a?.def?.ties?.FirstOrDefault(s => s?.other == c?.Key) ?? c?.def?.ties?.FirstOrDefault(s => s?.other == a?.Key);
            return spec?.words;
        }

        // ================= Keeping it right =================

        /// <summary>
        /// The region's day (and every load): a def added since gets its god, the ties are the defs', each god is held by the
        /// peoples that revere it, and the colony knows the gods of its own people, its neighbours and its visitors (hidden
        /// state: nothing is told).
        /// </summary>
        public static void Keep(GodBook b, int now, bool tell = true)
        {
            if (b == null) return;
            if (!b.built || b.version < Nine)
            {
                Build(b);
                return;
            }
            foreach (var def in Defs())
                if (!b.gods.Any(g => g.def == def))
                {
                    var g = new God { def = def, born = now, lastHeld = now, form = FormFor(def) };
                    g.FromDef();
                    b.Add(g);
                }
            b.gods.RemoveAll(g => g.def == null);
            Ties(b);
            var named = new HashSet<string>(Defs().SelectMany(d => d.peoples ?? new List<string>()));
            var factions = Factions().ToList();
            foreach (var g in b.gods)
            {
                g.factions.RemoveAll(f => f == null || f.defeated || !factions.Contains(f));
                g.ideos.Clear();
                g.form = FormFor(g.def);
                foreach (var f in factions)
                {
                    bool holds = GodLoreMath.Reveres(g.def.peoples, g.def.kinds, f.def.defName, KindsOf(f), named.Contains(f.def.defName));
                    if (holds && !g.factions.Contains(f)) g.factions.Add(f);
                    else if (!holds) g.factions.Remove(f);
                }
                if (g.Worshipped) g.lastHeld = now;
            }
            LearnColony(b, now, false);
            LearnFromNotables(b, false);
            Gods.Changed();
        }

        /// <summary>A people's kinds: its age, and raiders, the Empire and cannibals where it is one.</summary>
        public static List<string> KindsOf(Faction f)
        {
            if (f?.def == null) return new List<string>();
            bool cannibal = f.def.defName.IndexOf("Cannibal", StringComparison.OrdinalIgnoreCase) >= 0;
            try
            {
                if (!cannibal && ModsConfig.IdeologyActive && f.ideos?.PrimaryIdeo?.memes != null)
                    cannibal = f.ideos.PrimaryIdeo.memes.Any(m => m?.defName == "Cannibal");
            }
            catch
            {
                // Their faith unread: their name alone.
            }
            return GodLoreMath.KindsOf(AgeOf(f), !f.IsPlayer && f.def.permanentEnemy, !f.IsPlayer && Ages.IsEmpire(f), cannibal);
        }

        // ================= What the colony knows (hidden) =================

        /// <summary>The colony knows the gods its own people hold.</summary>
        public static void LearnColony(GodBook b, int now, bool tell)
        {
            var player = Faction.OfPlayerSilentFail;
            if (player == null) return;
            foreach (var g in b.gods)
                if (!g.known && g.factions.Contains(player))
                    Gods.Learn(g, "faith", false);
        }

        /// <summary>The colony knows the gods of the peoples whose people it knows by name (its neighbours).</summary>
        public static void LearnFromNotables(GodBook b, bool tell)
        {
            var book = NotableBook.Instance;
            if (book?.notables == null) return;
            foreach (var f in book.notables.Where(n => n?.faction != null).Select(n => n.faction).Distinct().ToList())
                Gods.Learn(f, "neighbours", false);
        }

        /// <summary>The region's hour: someone of another people on a home map (a trader, a guest, a raider swearing): the colony learns their gods.</summary>
        public static void LearnFromVisitors(GodBook b)
        {
            if (b.gods.All(g => g.known)) return;
            var seen = new HashSet<Faction>();
            foreach (var map in Homes.All)
            {
                var pawns = map?.mapPawns?.AllPawnsSpawned;
                if (pawns == null) continue;
                for (int i = 0; i < pawns.Count; i++)
                {
                    var p = pawns[i];
                    var f = p?.Faction;
                    if (f == null || f.IsPlayer || f.Hidden || p.RaceProps?.Humanlike != true || !seen.Add(f)) continue;
                }
            }
            foreach (var f in seen) Gods.Learn(f, "visitors", false);
        }
    }
}
