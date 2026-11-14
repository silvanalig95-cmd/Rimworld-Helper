using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods' public face (docs/design/gods.md, "the contract"): every other part asks here. The nine
    /// (<see cref="All"/>, <see cref="ByKey"/>, <see cref="Of(Faction)"/>, <see cref="OfIdeo"/>, <see cref="ColonyGods"/>,
    /// <see cref="Patron"/>), the colony's favour with each god at each home (<see cref="Favour"/>, <see cref="AddFavour"/>,
    /// <see cref="Deed"/>), the acts of the gods (<see cref="Remember"/>, each
    /// with the colonists who saw it), the ties (<see cref="TiesOf"/>, <see cref="Rivals"/>, <see cref="Allies"/>), a god's
    /// nature and kind, its words (<see cref="Name"/>, <see cref="NameBy"/>, <see cref="Epithet"/>, <see cref="GodOf"/>,
    /// <see cref="SwearBy"/>, the placeholders {GOD}, {GODEPITHET} and {GODOF}), and what the colony knows. The gods are felt,
    /// one way for every game; how near each one is to the colony now, and so how plainly it acts, is its own (GodNear).
    /// <para>
    /// All of it is hidden state that drives what the gods do. The gods are an unseen presence, like a storyteller (the
    /// player, 11 November 2026): nothing here is shown or told as what a god thinks, wants or feels. The player learns of
    /// the gods only what the colonists see, hear and believe (Gods/GodLore.cs). Off (the setting "Gods"), there are no
    /// gods: every list is empty and nothing moves.
    /// </para>
    /// </summary>
    public static class Gods
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The gods are on (the setting "Gods", on by default).</summary>
        public static bool On => S?.gods ?? true;

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

        /// <summary>One of the nine by its short name ("Hollin", any case; its def's key or its name), or null.</summary>
        public static God ByKey(string key)
        {
            var b = Book;
            if (b == null || key.NullOrEmpty()) return null;
            foreach (var g in b.gods)
                if (g != null && (string.Equals(g.Key, key, StringComparison.OrdinalIgnoreCase) || string.Equals(g.name, key, StringComparison.OrdinalIgnoreCase)))
                    return g;
            return null;
        }

        /// <summary>The gods a people reveres (by its def's name or its kind: Gods/Pantheon.cs), in the nine's order.</summary>
        public static List<God> Of(Faction f)
        {
            var b = Book;
            if (b == null || f == null) return new List<God>();
            return b.gods.Where(g => g.factions.Contains(f)).OrderBy(g => g.id).ToList();
        }

        /// <summary>The gods of a faith (Ideology's ideoligion): none while Ideology's deities are ignored (step 1 of the design's Ideology plan).</summary>
        public static List<God> OfIdeo(Ideo ideo)
        {
            var b = Book;
            if (b == null || ideo == null) return new List<God>();
            return b.gods.Where(g => g.ideos.Contains(ideo)).OrderBy(g => g.id).ToList();
        }

        private static readonly Dictionary<int, (int at, int version, List<God> gods)> colony = new Dictionary<int, (int, int, List<God>)>();

        /// <summary>
        /// The gods of a home's colony: those its own people revere by its age (Gods/Pantheon.cs), and those of its
        /// colonists' faiths where a faith holds one; the most widely held first. Worked out at most once in a 250-tick stretch.
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

        /// <summary>The god the colony knows and stands best with at this home (from noticed up), or null: "the colony's patron".</summary>
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
                // Only a god the colony knows can be its patron.
                if (g == null || !g.known) continue;
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
        /// in its hidden history with <paramref name="why"/> (already in words); and a little onto the gods it is tied to (its
        /// rivals displeased, its allies pleased: GodTiesMath.Spill). Never told: favour is hidden state.
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
                // One favor doesn't move (Helios, who gives without looking): what is meant for it goes along its ties alone.
                bool unmoved = g.def?.unmoved == true;
                if (!unmoved) b.Move(g, key, a, out _, out _);
                if (!unmoved && !why.NullOrEmpty() && Math.Abs(a) >= 0.5f) b.Remember(new GodAct { god = g.id, home = key, tick = Now, kind = GodActKind.Favour, text = why, favour = a });
                if (g.kind == GodKind.Newborn && a > 0f) Grow(b, g, a, home);
                if (source == FavourSource.Spill) return;
                foreach (var t in b.TiesOf(g.id).ToList())
                {
                    var other = b.ById(t.Other(g.id));
                    if (other == null || other.def?.unmoved == true) continue;
                    float s = GodTiesMath.Spill(a, t.kind, t.strength, t.a == g.id, other.nature);
                    if (Math.Abs(s) < 0.05f) continue;
                    b.Move(other, key, s, out _, out _);
                    if (Math.Abs(s) >= 1.5f)
                        b.Remember(new GodAct
                        {
                            god = other.id, other = g.id, home = key, tick = Now, kind = GodActKind.Favour, favour = s,
                            text = (s < 0f ? "CC_God_SpillAct_Against" : "CC_God_SpillAct_With").Translate(g.name.Named("OTHER"), GodWords.TieWord(t.kind).Named("TIE")).Resolve()
                        });
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

        /// <summary>A newborn grows with the colony's devotion, into an ordinary god once it has enough (hidden; the nine have none).</summary>
        private static void Grow(GodBook b, God g, float gain, Map home)
        {
            g.devotion += GodsMath.Devotion(gain);
            if (g.devotion < GodsMath.NewbornGrown) return;
            g.kind = g.baseKind = GodKind.Ordinary;
            b.Remember(new GodAct { god = g.id, home = HomeKey(home), tick = Now, kind = GodActKind.Born, text = "CC_God_GrownAct".Translate(g.name.Named("GOD")).Resolve() });
            Changed();
        }

        // ================= Acts =================

        /// <summary>A colonist this many cells from an act of the gods, in sight of it, saw it.</summary>
        public const float WitnessRange = 20f;

        /// <summary>The acts of the gods that happen on the land, where someone may see them (not favour, a tie, a vow or what the colony knows).</summary>
        public static bool SeenOnLand(GodActKind k) => k == GodActKind.Appear || k == GodActKind.Act || k == GodActKind.Reveal || k == GodActKind.Depart
                                                        || k == GodActKind.Blessing || k == GodActKind.Wrath || k == GodActKind.Omen || k == GodActKind.Mark;

        /// <summary>
        /// Remembers an act of a god (an appearance, a blessing, a mark) in the gods' hidden history. An act of the gods on the
        /// land (<see cref="SeenOnLand"/>) with no witnesses given gets them now: the colonists awake and near enough to have seen
        /// it (in sight within <see cref="WitnessRange"/> cells of its cell, or everyone awake on the map for one with no
        /// cell: the sky, a silence, a shadow). An act someone saw reaches the colony's book as they saw it (the god they took
        /// it for, which may not be the god it was); one nobody saw stays hidden.
        /// </summary>
        public static void Remember(GodAct act)
        {
            var b = Book;
            if (act == null || b == null) return;
            b.Remember(act);
            if (act.booked || !SeenOnLand(act.kind)) return;
            // A moment just written into the book (Moment) isn't written twice when its caller remembers it too.
            if (act.god == momentGod && Now - momentAt < GenDate.TicksPerHour * 2) return;
            try
            {
                var map = Homes.MapOf(act.home) ?? Homes.Seat;
                var pawns = act.witnesses.NullOrEmpty() ? Witnesses(map, act.cell) : WitnessPawns(map, act.witnesses);
                if (act.witnesses.NullOrEmpty()) act.witnesses = pawns.Select(p => p.LabelShort).ToList();
                if (pawns.Count == 0 || act.text.NullOrEmpty()) return;
                var g = ById(act.god);
                var lore = GodLore.Instance;
                string taken = g == null || lore == null ? null : GodLoreMath.Attribute(g.Key, lore.KnownOf(), Rand.Value);
                GodLore.Saw(pawns, ByKey(taken), act.text);
                act.booked = true;
                // Each witness's own belief in the god they took it for (Gods/Belief/PawnGods.cs).
                PawnGods.Saw(pawns, ByKey(taken), act.kind, false, act.text, act.cell, map);
                // A kindness or an anger seen: the believer among them comes to hold what the god they took it for loves or hates.
                if (taken != null && (act.kind == GodActKind.Blessing || act.kind == GodActKind.Wrath))
                    GodLore.Inferred(GodLore.Believer(pawns), ByKey(taken), g, act.kind == GodActKind.Wrath);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: an act's witnesses", e);
            }
        }

        private static int momentGod = -1, momentAt = -1;

        /// <summary>
        /// The colony's name for a god when the colony knows something was its doing, for an event, a card or a letter to name
        /// it plainly; else null (the thing stays unnamed, or "some say" where it is only suspected). The colony knows when it
        /// has seen this god act plainly (a manifestation it witnessed, a god drawn near enough to show itself, a disguised god
        /// revealed), or when it has learned <paramref name="sign"/> as the god's own (GodLore.LearnSign: a teller explained
        /// that the croaking before rain is Murmur). The name is the colony's: its name in the colony's book. Never anything of
        /// the god's mind, favor or ways.
        /// </summary>
        public static string CardName(God god, Map home, string sign = null)
        {
            if (god == null || !On) return null;
            var lore = GodLore.Instance;
            if (lore == null || !lore.KnowsActing(god) && !lore.Knows(god, sign)) return null;
            return lore.PageOf(god)?.name ?? GodLore.LabelFor(god.Key);
        }

        /// <summary>
        /// A god's moment was played (GodPresenceSeam.Call): the colony's book has it as those who saw it saw it, with the god
        /// they took it for (plainly named when it showed itself plainly; else whoever they believe it was, or nothing they can
        /// name), and in the card's words or, with none, what was seen (CC_GodLore_Moment_*).
        /// </summary>
        public static void Moment(PresenceSpec spec)
        {
            if (spec?.god == null || spec.map == null) return;
            try
            {
                var lore = GodLore.Instance;
                var witnesses = Witnesses(spec.map, spec.kind == PresenceKind.Omen ? IntVec3.Invalid : spec.cell);
                if (lore == null || witnesses.Count == 0) return;
                var taken = ByKey(GodLoreMath.Attribute(spec.god.Key, lore.KnownOf(), Rand.Value));
                string words = !spec.text.NullOrEmpty() ? spec.text : ("CC_GodLore_Moment_" + spec.kind).Translate().Resolve();
                // Seen plainly (not veiled), or a disguised god revealing itself: the colony knows it was this god, and names it.
                bool plain = !spec.veiled || spec.kind == PresenceKind.Reveal;
                if (plain) taken = spec.god;
                GodLore.Saw(witnesses, taken, words);
                if (plain) lore.MarkActing(spec.god);
                // The holy book: what the colony saw of it plainly, and what a believer now holds it loves or hates.
                if (plain) GodLore.Sighted(witnesses, spec.god, words);
                if (taken != null && (spec.kind == PresenceKind.Blessing || spec.kind == PresenceKind.Wrath))
                    GodLore.Inferred(GodLore.Believer(witnesses), taken, spec.god, spec.kind == PresenceKind.Wrath);
                // Each witness's own belief in the god they took it for, plainly seen the more (Gods/Belief/PawnGods.cs).
                PawnGods.Saw(witnesses, taken, PawnGods.KindOf(spec.kind), plain, words, spec.cell, spec.map);
                momentGod = spec.god.id;
                momentAt = Now;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a moment's witnesses", e);
            }
        }

        /// <summary>
        /// The colonists who saw something at <paramref name="cell"/> (awake, not downed, in sight within
        /// <see cref="WitnessRange"/> cells), or with no cell everyone awake on the map.
        /// </summary>
        public static List<Pawn> Witnesses(Map map, IntVec3 cell)
        {
            var list = new List<Pawn>();
            if (map?.mapPawns == null) return list;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (p == null || p.Downed || p.Dead || !(p.Awake())) continue;
                if (cell.IsValid)
                {
                    if (!cell.InBounds(map) || p.Position.DistanceTo(cell) > WitnessRange) continue;
                    if (!GenSight.LineOfSight(p.Position, cell, map, true)) continue;
                }
                list.Add(p);
            }
            return list;
        }

        private static List<Pawn> WitnessPawns(Map map, List<string> names)
        {
            var list = new List<Pawn>();
            if (names == null) return list;
            foreach (var p in PawnsFinder.AllMaps_FreeColonists.ToList())
                if (p != null && names.Contains(p.LabelShort) && !list.Contains(p))
                    list.Add(p);
            return list;
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

        /// <summary>What it is, as its def says ("the mother of every root under the world"); else its domain's words ({GODOF}).</summary>
        public static string GodOf(God g)
        {
            if (g == null) return "";
            if (!(g.def?.godOf).NullOrEmpty()) return g.def.godOf;
            var def = g.DomainDef;
            string noun = ("CC_God_Noun_" + GodsMath.Noun(g.form, (int)g.gender)).Translate();
            return "CC_God_Of".Translate(noun.Named("NOUN"), (def?.ofWords ?? "").Named("WHAT")).Resolve();
        }

        /// <summary>What it is with its article: one of the nine's already has its own ("the guest at every door"); else "a goddess of the harvest".</summary>
        public static string AGodOf(God g) => g == null ? "" : !(g.def?.godOf).NullOrEmpty() ? g.def.godOf : Find.ActiveLanguageWorker.WithIndefiniteArticle(GodOf(g));

        /// <summary>
        /// The name a speaker uses for a god: about half say its short name, the rest their own people's (their age's: the
        /// tribes' Grandmother Root, the medieval Green Lady), the same speaker always the same (GodLoreMath.NameFor). A
        /// colonist of the colony speaks of its age; anyone else of their people's.
        /// </summary>
        public static string NameBy(God g, Pawn speaker)
        {
            if (g == null) return "";
            if (g.def == null || speaker == null) return g.name ?? "";
            int age = speaker.Faction == null || speaker.Faction.IsPlayer ? (int)Ages.Ours : (int)Ages.Of(speaker.Faction);
            return GodLoreMath.NameFor(g.name, g.def.NamesOf(age), speaker.thingIDNumber, g.id);
        }

        /// <summary>The placeholders of a god as a speaker names it: {GOD} their name for it, {GODEPITHET}, {GODOF}.</summary>
        public static NamedArgument[] ArgsBy(God g, Pawn speaker) => g == null
            ? new NamedArgument[0]
            : new[] { NameBy(g, speaker).Named(GodsMath.God), (g.epithet ?? "").Named(GodsMath.Epithet), GodOf(g).Named(GodsMath.Of) };

        /// <summary>Its name and epithet: "Hollin, the Rootmother".</summary>
        public static string FullName(God g) => g == null ? "" : g.epithet.NullOrEmpty() ? g.name : "CC_God_FullName".Translate(g.name.Named("GOD"), g.epithet.Named("EPITHET")).Resolve();

        /// <summary>
        /// The god a pawn would swear by: a colonist's own belief first (the god they hold most, or one of the moment's domain
        /// they hold: Gods/Belief/PawnGods.cs); else one of their own faith's gods (with Ideology), else their people's (a
        /// colonist: the colony's own, else the god it stands best with); with <paramref name="about"/> (for a colonist drawn to
        /// no god, else the domain of the work they're best at), one of that domain where they have one. The same god for the
        /// same pawn each time otherwise (their people's patron first). Null with none.
        /// </summary>
        public static God SwearBy(Pawn p, GodDomain about = GodDomain.None)
        {
            if (Book == null || p == null) return null;
            try
            {
                var held = PawnGods.Held(p, about);
                if (held != null) return held;
                if (about == GodDomain.None) about = PawnGods.WorkDomain(p);
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
                return pool[(p.thingIDNumber & 0x7FFFFFFF) % pool.Count];
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

        /// <summary>The colony knows of this god (hidden state: what the player knows of it is only what the colony's book holds).</summary>
        public static bool Known(God g) => g != null && g.known;

        /// <summary>
        /// The colony comes to know of a god: from its own people ("faith"), its neighbours ("neighbours"), a letter
        /// ("letter"), visitors on its land ("visitors"). Hidden state, remembered in the gods' history; never told
        /// (<paramref name="tell"/> is kept for the parts that pass it).
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
        }

        /// <summary>The colony learns of the gods a people reveres (their people known by name, they came to the gate). Hidden state.</summary>
        public static void Learn(Faction f, string how, bool tell = true)
        {
            if (f == null || f.IsPlayer || Book == null) return;
            foreach (var g in Of(f).Where(g => !g.known).ToList()) Learn(g, how, false);
        }

        /// <summary>The peoples that revere a god, in words: "the Ashen Banner and the road league"; the colony's own "the colony" (the hidden history's words).</summary>
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
