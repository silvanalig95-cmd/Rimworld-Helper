using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods' public face (docs/design/gods.md, "Core: the pantheon"): every other part asks here. The pantheon
    /// (<see cref="All"/>, <see cref="Of(Faction)"/>, <see cref="OfIdeo"/>, <see cref="ColonyGods"/>, <see cref="Patron"/>),
    /// the colony's favour with each god at each home in words (<see cref="Favour"/>, <see cref="AddFavour"/>, <see cref="Deed"/>),
    /// how real the gods are (<see cref="Realness"/>), the acts the colony remembers (<see cref="Remember"/>), the ties
    /// (<see cref="TiesOf"/>, <see cref="Rivals"/>, <see cref="Allies"/>), a god's nature and kind, its words
    /// (<see cref="Name"/>, <see cref="Epithet"/>, <see cref="GodOf"/>, <see cref="SwearBy"/>, the placeholders
    /// {GOD}, {GODEPITHET} and {GODOF}), and what the colony knows. Off (the setting "Gods"), there is no pantheon: every list
    /// is empty and nothing moves. Favour is never shown as a number.
    /// </summary>
    public static class Gods
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The gods are on (the setting "Gods", on by default).</summary>
        public static bool On => S?.gods ?? true;

        /// <summary>How real the gods are (the setting): Believed, Felt (the default) or Walking.</summary>
        public static GodRealness Realness => S?.godRealness ?? GodRealness.Felt;

        private static GodBook Book => On ? GodBook.Instance : null;

        private static readonly List<God> None = new List<God>();

        private static int version;

        /// <summary>The pantheon changed (a god added, a faith gone): what was worked out from it is worked out again.</summary>
        public static void Changed()
        {
            version++;
            colony.Clear();
        }

        /// <summary>Which version of the pantheon (for a page that keeps what it worked out).</summary>
        public static int Version => version;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ================= The pantheon =================

        /// <summary>Every god of the world.</summary>
        public static IReadOnlyList<God> All => Book?.gods ?? None;

        public static God ById(int id) => Book?.ById(id);

        /// <summary>The gods a people holds: its patron first, then its faith's.</summary>
        public static List<God> Of(Faction f)
        {
            var b = Book;
            if (b == null || f == null) return new List<God>();
            return b.gods.Where(g => g.factions.Contains(f)).OrderBy(g => g.patron == null ? 1 : 0).ThenBy(g => g.id).ToList();
        }

        /// <summary>The gods of a faith (Ideology's ideoligion).</summary>
        public static List<God> OfIdeo(Ideo ideo)
        {
            var b = Book;
            if (b == null || ideo == null) return new List<God>();
            return b.gods.Where(g => g.ideos.Contains(ideo)).OrderBy(g => g.id).ToList();
        }

        private static readonly Dictionary<int, (int at, int version, List<God> gods)> colony = new Dictionary<int, (int, int, List<God>)>();

        /// <summary>
        /// The gods of a home's colony: those of its colonists' faiths (with Ideology), and those the colony holds itself
        /// (its makers' gods without Ideology, a newborn of its faith); the most widely held first. Worked out at most once
        /// in a 250-tick stretch.
        /// </summary>
        public static List<God> ColonyGods(Map home)
        {
            var b = Book;
            if (b == null) return new List<God>();
            int key = HomeKey(home);
            int now = Now;
            if (colony.TryGetValue(key, out var c) && Fresh.Holds(c.at, c.version, now, version, Sched.RareTicks)) return c.gods;
            var player = Faction.OfPlayerSilentFail;
            var held = new Dictionary<God, int>();
            var map = home != null && Homes.IsHome(home) ? home : Homes.Seat;
            if (ModsConfig.IdeologyActive && map != null)
                foreach (var p in Homes.People(map))
                {
                    if (p?.Ideo == null) continue;
                    foreach (var g in b.gods)
                        if (g.ideos.Contains(p.Ideo))
                            held[g] = (held.TryGetValue(g, out int n) ? n : 0) + 1;
                }
            foreach (var g in b.gods)
                if (player != null && g.factions.Contains(player) && !held.ContainsKey(g))
                    held[g] = 0;
            var list = held.Keys.Where(g => g.kind != GodKind.Dead).OrderByDescending(g => held[g]).ThenBy(g => g.id).ToList();
            colony[key] = (now, version, list);
            return list;
        }

        /// <summary>The god the colony stands best with at this home (from noticed up), or null: "the colony's patron".</summary>
        public static God Patron(Map home)
        {
            var b = Book;
            if (b == null) return null;
            int key = HomeKey(home);
            God best = null;
            float most = float.MinValue;
            var mine = ColonyGods(home);
            foreach (var s in b.standings)
            {
                if (s == null || s.home != key) continue;
                var g = b.ById(s.god);
                if (g == null) continue;
                float v = b.ValueNow(g, key) + (mine.Contains(g) ? 0.01f : 0f);
                if (!GodsMath.PatronWorthy(v) || v <= most) continue;
                most = v;
                best = g;
            }
            return best;
        }

        // ================= Favour =================

        /// <summary>The home a map's favour is kept under: the home itself, or the seat for a map that isn't one.</summary>
        public static int HomeKey(Map map)
        {
            if (map != null && Homes.IsHome(map)) return Homes.KeyOf(map);
            var seat = Homes.Seat;
            return seat != null ? Homes.KeyOf(seat) : HomesMath.None;
        }

        /// <summary>The colony's standing with a god at a home, in words.</summary>
        public static FavourWord Favour(God g, Map home) => GodsMath.Word(FavourValue(g, home));

        /// <summary>The standing as the rules read it (-100 to 100, drifted to now); never shown to the player as a number.</summary>
        public static float FavourValue(God g, Map home)
        {
            var b = Book;
            return b == null || g == null ? 0f : b.ValueNow(g, HomeKey(home));
        }

        /// <summary>
        /// The colony's standing with a god moves: by its nature and kind and where it came from (GodsMath.Scale), remembered
        /// in its acts with <paramref name="why"/> (already in words), told as news when it changes the word for a god the
        /// colony knows; and a little onto the gods it is tied to (its rivals displeased, its allies pleased:
        /// GodTiesMath.Spill). A newborn grows with it.
        /// </summary>
        public static void AddFavour(God g, Map home, float amount, string why, FavourSource source = FavourSource.Deed, GodOffence offence = GodOffence.None)
        {
            var b = Book;
            if (b == null || g == null || float.IsNaN(amount) || amount == 0f) return;
            try
            {
                int key = HomeKey(home);
                float a = GodsMath.Scale(amount, g.nature, g.kind, source, offence);
                if (a == 0f) return;
                b.Move(g, key, a, out var before, out var after);
                if (!why.NullOrEmpty() && Math.Abs(a) >= 0.5f) b.Remember(new GodAct { god = g.id, home = key, tick = Now, kind = GodActKind.Favour, text = why, favour = a });
                if (g.kind == GodKind.Newborn && a > 0f) Grow(b, g, a, home);
                if (before != after) Tell(g, home, after, why);
                if (source == FavourSource.Spill) return;
                foreach (var t in b.TiesOf(g.id).ToList())
                {
                    var other = b.ById(t.Other(g.id));
                    if (other == null) continue;
                    float s = GodTiesMath.Spill(a, t.kind, t.strength, t.a == g.id, other.nature);
                    if (Math.Abs(s) < 0.05f) continue;
                    b.Move(other, key, s, out var ob, out var oa);
                    if (Math.Abs(s) >= 1.5f)
                        b.Remember(new GodAct
                        {
                            god = other.id, other = g.id, home = key, tick = Now, kind = GodActKind.Favour, favour = s,
                            text = (s < 0f ? "CC_God_SpillAct_Against" : "CC_God_SpillAct_With").Translate(g.name.Named("OTHER"), GodWords.TieWord(t.kind).Named("TIE")).Resolve()
                        });
                    if (ob != oa) Tell(other, home, oa, null);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: favour", e);
            }
        }

        /// <summary>
        /// A deed in a domain at a home (a big harvest, a legend beast brought down): every god of the domain hears of it, a
        /// god whose second domain it is by half, and it counts toward a newborn where the colony's faith has no god of it.
        /// A loss (a sacred animal slaughtered) displeases them instead.
        /// </summary>
        public static void Deed(GodDomain d, Map home, float amount, string why, FavourSource source = FavourSource.Deed, GodOffence offence = GodOffence.None)
        {
            var b = Book;
            if (b == null || d == GodDomain.None || amount == 0f) return;
            if (amount > 0f && source == FavourSource.Deed) b.NoteDeed(d, HomeKey(home));
            foreach (var g in b.gods.ToList())
            {
                if (g.domain == d) AddFavour(g, home, amount, why, source, offence);
                else if (g.second == d) AddFavour(g, home, amount * 0.5f, why, source, offence);
            }
        }

        /// <summary>A newborn grows with the colony's devotion, into an ordinary god once it has enough.</summary>
        private static void Grow(GodBook b, God g, float gain, Map home)
        {
            g.devotion += GodsMath.Devotion(gain);
            if (g.devotion < GodsMath.NewbornGrown) return;
            g.kind = g.baseKind = GodKind.Ordinary;
            b.Remember(new GodAct { god = g.id, home = HomeKey(home), tick = Now, kind = GodActKind.Born, text = "CC_God_GrownAct".Translate(g.name.Named("GOD")).Resolve() });
            ColonyCouncil.Tell.Minor(NewsKind.Colony, Importance.Notable, home, "CC_God_GrownNews".Translate(g.name.Named("GOD"), GodOf(g).Named("GODOF")).Resolve(), null);
            Changed();
        }

        /// <summary>A god the colony knows changed its word for a home: a line in the news, in the voice of how real the gods are.</summary>
        private static void Tell(God g, Map home, FavourWord now, string why)
        {
            if (g == null || !g.known) return;
            var map = home != null && Homes.IsHome(home) ? home : Homes.Seat;
            string place = Homes.Name(map);
            string stands = ("CC_God_Stands_" + now).Translate(g.name.Named("GOD"), place.Named("HOME")).Resolve();
            string text = ("CC_God_Voice_" + GodsMath.Voice(Realness)).Translate(stands.Named("STANDS")).Resolve().CapitalizeFirst();
            text = why.NullOrEmpty() ? text + "." : "CC_God_Why".Translate(text.Named("TEXT"), why.Named("WHY")).Resolve();
            ColonyCouncil.Tell.Minor(NewsKind.Colony, now <= FavourWord.Displeased || now >= FavourWord.Favoured ? Importance.Notable : Importance.Flavor, map, text, null);
        }

        // ================= Acts =================

        /// <summary>Remembers an act of a god (an appearance, a blessing, a mark): shown on its page.</summary>
        public static void Remember(GodAct act)
        {
            if (act == null) return;
            Book?.Remember(act);
        }

        /// <summary>The acts remembered of a god, newest first.</summary>
        public static List<GodAct> ActsOf(God g)
        {
            var b = Book;
            if (b == null || g == null) return new List<GodAct>();
            var list = b.acts.Where(a => a.god == g.id).ToList();
            list.Reverse();
            return list;
        }

        // ================= Ties, nature, kind =================

        public static List<GodTie> TiesOf(God g) => Book == null || g == null ? new List<GodTie>() : Book.TiesOf(g.id).ToList();

        /// <summary>The gods set against it: rivals, lovers estranged, and the old wrongs either way.</summary>
        public static List<God> Rivals(God g) =>
            TiesOf(g).Where(t => GodTiesMath.Hostile(t.kind)).Select(t => ById(t.Other(g.id))).Where(x => x != null).ToList();

        /// <summary>The gods that stand with it: allies, lovers and kin.</summary>
        public static List<God> Allies(God g) =>
            TiesOf(g).Where(t => !GodTiesMath.Hostile(t.kind)).Select(t => ById(t.Other(g.id))).Where(x => x != null).ToList();

        public static GodNature NatureOf(God g) => g?.nature ?? GodNature.Patient;

        public static GodKind KindOf(God g) => g?.kind ?? GodKind.Ordinary;

        /// <summary>How loudly the god answers (0 to 1), by its nature and kind.</summary>
        public static float Loudness(God g) => g == null ? 0f : GodsMath.Loudness(g.nature, g.kind);

        // ================= Words =================

        public static string Name(God g) => g?.name ?? "";

        public static string Epithet(God g) => g?.epithet ?? "";

        /// <summary>What it is: "goddess of the harvest", "spirit of the hunt", "archotech of machines" ({GODOF}).</summary>
        public static string GodOf(God g)
        {
            if (g == null) return "";
            var def = g.DomainDef;
            string noun = ("CC_God_Noun_" + GodsMath.Noun(g.form, (int)g.gender)).Translate();
            return "CC_God_Of".Translate(noun.Named("NOUN"), (def?.ofWords ?? "").Named("WHAT")).Resolve();
        }

        /// <summary>What it is with its article: "a goddess of the harvest", "an archotech of machines".</summary>
        public static string AGodOf(God g) => g == null ? "" : Find.ActiveLanguageWorker.WithIndefiniteArticle(GodOf(g));

        /// <summary>Its name and epithet: "Ceren, the Bringer of Rain".</summary>
        public static string FullName(God g) => g == null ? "" : g.epithet.NullOrEmpty() ? g.name : "CC_God_FullName".Translate(g.name.Named("GOD"), g.epithet.Named("EPITHET")).Resolve();

        /// <summary>
        /// The god a pawn would swear by: one of their own faith's gods (with Ideology), else their people's (a colonist: the
        /// colony's own, else the god it stands best with); with <paramref name="about"/>, one of that domain where they have
        /// one. The same god for the same pawn each time otherwise (their people's patron first). Null with none.
        /// </summary>
        public static God SwearBy(Pawn p, GodDomain about = GodDomain.None)
        {
            if (Book == null || p == null) return null;
            try
            {
                List<God> pool = null;
                if (ModsConfig.IdeologyActive && p.Ideo != null) pool = OfIdeo(p.Ideo).Where(g => g.kind != GodKind.Dead).ToList();
                if (pool.NullOrEmpty() && p.Faction != null)
                {
                    if (p.Faction.IsPlayer)
                    {
                        pool = ColonyGods(p.MapHeld);
                        if (pool.Count == 0)
                        {
                            var patron = Patron(p.MapHeld);
                            if (patron != null) pool = new List<God> { patron };
                        }
                    }
                    else pool = Of(p.Faction).Where(g => g.kind != GodKind.Dead).ToList();
                }
                if (pool.NullOrEmpty()) return null;
                if (about != GodDomain.None)
                {
                    var fit = pool.FirstOrDefault(g => g.domain == about) ?? pool.FirstOrDefault(g => g.second == about);
                    if (fit != null) return fit;
                }
                var own = p.Faction != null ? pool.FirstOrDefault(g => g.patron != null && g.factions.Contains(p.Faction)) : null;
                return own ?? pool[(p.thingIDNumber & 0x7FFFFFFF) % pool.Count];
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: who swears by whom", e);
                return null;
            }
        }

        /// <summary>The placeholders of a god into a letter's or a line's values: {GOD}, {GODEPITHET}, {GODOF}.</summary>
        public static void Fill(IDictionary<string, string> values, God g)
        {
            if (values == null || g == null) return;
            values[GodsMath.God] = g.name;
            values[GodsMath.Epithet] = g.epithet ?? "";
            values[GodsMath.Of] = GodOf(g);
        }

        /// <summary>The placeholders of a god as named arguments, for Translate and Formatted.</summary>
        public static NamedArgument[] Args(God g) => g == null
            ? new NamedArgument[0]
            : new[] { g.name.Named(GodsMath.God), (g.epithet ?? "").Named(GodsMath.Epithet), GodOf(g).Named(GodsMath.Of) };

        // ================= What the colony knows =================

        /// <summary>The colony knows this god (else it is a rumour on the page).</summary>
        public static bool Known(God g) => g != null && g.known;

        /// <summary>
        /// The colony comes to know a god: from its own faith ("faith"), its neighbours ("neighbours"), a letter ("letter"),
        /// visitors on its land ("visitors"). Remembered, and with <paramref name="tell"/> a line in the news.
        /// </summary>
        public static void Learn(God g, string how, bool tell = true)
        {
            var b = Book;
            if (b == null || g == null || g.known) return;
            g.known = true;
            g.knownHow = how;
            g.knownAt = Now;
            string from = ("CC_God_KnownFrom_" + (how ?? "faith")).Translate(PeopleWords(g).Named("PEOPLE")).Resolve();
            b.Remember(new GodAct { god = g.id, tick = Now, kind = GodActKind.Known, text = "CC_God_KnownAct".Translate(from.Named("FROM")).Resolve() });
            if (tell && how != "faith")
                ColonyCouncil.Tell.Minor(NewsKind.World, Importance.Flavor, null, "CC_God_KnownNews".Translate(FullName(g).Named("GOD"), AGodOf(g).Named("GODOF"), from.Named("FROM")).Resolve(), null);
        }

        /// <summary>The colony learns the gods of a people (their letter named one, they came to the gate).</summary>
        public static void Learn(Faction f, string how)
        {
            if (f == null || f.IsPlayer) return;
            foreach (var g in Of(f)) Learn(g, how);
        }

        /// <summary>The peoples (and faiths) that hold a god, in words: "the Ashen Banner and the road league"; the colony's own "the colony".</summary>
        public static string PeopleWords(God g)
        {
            if (g == null) return "";
            var names = g.factions.Where(f => f != null).Select(f => f.IsPlayer ? "CC_God_TheColony".Translate().ToString() : f.Name).Distinct().ToList();
            if (names.Count == 0) names = g.ideos.Where(i => i != null).Select(i => i.name).Distinct().ToList();
            if (names.Count == 0) return "CC_God_Nobody".Translate();
            return names.ToCommaList(true);
        }

        // ================= Vows =================

        /// <summary>The colony's vows, open and done (stage 2's wishes and life goals make them).</summary>
        public static IReadOnlyList<GodVow> Vows => Book?.vows ?? new List<GodVow>();

        /// <summary>A colonist vows something to a god (in words), for so many days (0: no end), at their home.</summary>
        public static GodVow Vow(God g, Pawn p, string what, int days)
        {
            var b = Book;
            if (b == null || g == null || p == null) return null;
            var v = new GodVow { god = g.id, pawn = p, home = HomeKey(p.MapHeld), what = what, since = Now, until = days > 0 ? Now + days * GenDate.TicksPerDay : -1 };
            b.vows.Add(v);
            b.Remember(new GodAct { god = g.id, home = v.home, tick = Now, kind = GodActKind.Vow, text = "CC_God_VowAct".Translate(p.LabelShort.Named("PAWN"), (what ?? "").Named("WHAT")).Resolve() });
            return v;
        }

        /// <summary>A vow kept (the god pleased) or broken (offended, a patient or cold god most).</summary>
        public static void EndVow(GodVow v, bool kept)
        {
            if (v == null || !v.Open) return;
            var g = ById(v.god);
            v.kept = kept;
            v.broken = !kept;
            if (g == null) return;
            var map = Homes.MapOf(v.home) ?? v.pawn?.MapHeld;
            string why = (kept ? "CC_God_VowKept" : "CC_God_VowBroken").Translate((v.pawn?.LabelShort ?? "?").Named("PAWN"), (v.what ?? "").Named("WHAT")).Resolve();
            AddFavour(g, map, kept ? 8f : -10f, why, FavourSource.Vow);
        }
    }
}
