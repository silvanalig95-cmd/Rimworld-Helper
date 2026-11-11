using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Builds the pantheon once per world and keeps it right (docs/design/gods.md, "One pantheon per world, many faiths"):
    /// <list type="bullet">
    /// <item>With Ideology, each faith's deities are real gods, their domains read from their own words and the faith's
    /// memes and precepts (GodsMath.DeityDomains); an animist faith with no deities has two or three spirits by Rimfolk's
    /// makers, an archist faith an archotech; an ideological faith none.</item>
    /// <item>Without Ideology (or for a people with no faith to read), Rimfolk's makers give each people two or three gods by
    /// its age, its ways and the land round its towns (GodsMath.MakerDomains), named from the domains' names for its age.</item>
    /// <item>Rimfolk's peoples each have their patron (Defs/GodDefs/Rimfolk_Pantheon_Patrons.xml): one of their faith's gods
    /// where it fits, else added to it.</item>
    /// <item>The ones who don't fit: an archotech for each spacer people's god, the Hunger with the Choir of the Spire (or the
    /// world's darkest void god), one trickster, a dead god in old places, a fading god of a faith that died out before the
    /// colony came; and later a newborn of the colony's own faith.</item>
    /// <item>The ties between them (GodTiesMath.Build), shifting slowly in the region's day.</item>
    /// </list>
    /// Everything the same for the same world (the world's seed), so a pantheon rebuilt reads the same.
    /// </summary>
    public static class Pantheon
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static int WorldSeed => Find.World?.info?.Seed ?? 0;

        /// <summary>A people with gods: of humans, not gone, not one of the game's hidden ones (the colony always).</summary>
        public static bool Fits(Faction f) => f != null && !f.defeated && f.def != null && f.def.humanlikeFaction && (f.IsPlayer || !f.Hidden);

        private static IEnumerable<Faction> Factions() =>
            Find.FactionManager?.AllFactionsListForReading.Where(Fits).ToList() ?? new List<Faction>();

        /// <summary>The age a people's gods take their form from (0 tribal to 3 spacer): the colony's own, or the people's.</summary>
        public static int AgeOf(Faction f) => f == null || f.IsPlayer ? (int)Ages.Ours : Peoples.AgeOf(f);

        /// <summary>The faiths a people's gods come from: the colony's every faith, another people's own.</summary>
        private static List<Ideo> FaithsOf(Faction f)
        {
            var list = new List<Ideo>();
            if (!ModsConfig.IdeologyActive || f?.ideos == null) return list;
            if (f.IsPlayer)
            {
                foreach (var i in f.ideos.AllIdeos)
                    if (i != null && !list.Contains(i))
                        list.Add(i);
            }
            else if (f.ideos.PrimaryIdeo != null) list.Add(f.ideos.PrimaryIdeo);
            return list;
        }

        // ================= Building =================

        /// <summary>The pantheon for this world, once: every people's gods, the patrons, the old ones, the kinds that don't fit, their ties.</summary>
        public static void Build(GodBook b)
        {
            if (b == null || b.built) return;
            int now = Now;
            foreach (var f in Factions()) AddFaction(b, f, now);
            AddPatrons(b, now);
            AddOld(b, now);
            MarkKinds(b);
            BuildTies(b, now);
            b.built = true;
            LearnColony(b, now, false);
            LearnFromNotables(b, false);
            Gods.Changed();
        }

        private static void AddFaction(GodBook b, Faction f, int now)
        {
            int age = AgeOf(f);
            var faiths = FaithsOf(f);
            foreach (var ideo in faiths) AddIdeo(b, ideo, f, age, now);
            // No faith to read: Rimfolk's makers (the colony too, with no Ideology: the gods of where it came from).
            if (faiths.Count == 0) AddMakers(b, f, null, age, now, 0, null, null);
        }

        /// <summary>A faith's gods: its deities, or spirits for an animist faith, an archotech for an archist one. Idempotent.</summary>
        private static void AddIdeo(GodBook b, Ideo ideo, Faction f, int age, int now)
        {
            if (ideo == null) return;
            var deities = (ideo.foundation as IdeoFoundation_Deity)?.DeitiesListForReading;
            if (!deities.NullOrEmpty())
            {
                var taken = new HashSet<GodDomain>(b.gods.Where(g => g.ideos.Contains(ideo)).Select(g => g.domain));
                var names = new HashSet<string>(deities.Select(d => d?.name).Where(n => !n.NullOrEmpty()));
                for (int i = 0; i < deities.Count; i++)
                {
                    var deity = deities[i];
                    if (deity == null || deity.name.NullOrEmpty()) continue;
                    string key = ideo.id + "|" + deity.name;
                    var existing = b.gods.FirstOrDefault(g => g.deityKey == key);
                    // A deity renamed in a reform: the god it was, under its new name.
                    existing ??= b.gods.FirstOrDefault(g => g.deityKey != null && g.deityKey.StartsWith(ideo.id + "|") && !names.Contains(g.deityKey.Substring(g.deityKey.IndexOf('|') + 1)));
                    if (existing != null)
                    {
                        existing.name = deity.name;
                        existing.deityKey = key;
                        existing.deityType = deity.type;
                        Join(existing, f, ideo, now);
                        taken.Add(existing.domain);
                        continue;
                    }
                    var memes = ideo.memes?.Where(m => m != null).Select(m => (m.defName, m.label)).ToList();
                    var precepts = ideo.PreceptsListForReading?.Where(p => p?.def != null).Select(p => p.def.defName).ToList();
                    int seed = GodsMath.Seed(WorldSeed, ideo.id, i);
                    GodsMath.DeityDomains(deity.name, deity.type, deity.relatedMeme?.defName, deity.relatedMeme?.label, memes, precepts, taken, age, seed,
                        out var d, out var s);
                    var g = Make(b, deity.name, d, s, age, seed, "deity", deity.gender, null, now);
                    if (g == null) continue;
                    g.deityKey = key;
                    g.deityType = deity.type;
                    Join(g, f, ideo, now);
                    taken.Add(d);
                }
                return;
            }
            string structure = ideo.StructureMeme?.defName ?? "";
            if (Spirits(structure)) AddMakers(b, f, ideo, age, now, 0, GodForm.Spirit, null);
            else if (structure.IndexOf("Archist", StringComparison.OrdinalIgnoreCase) >= 0)
                AddMakers(b, f, ideo, 3, now, 1, GodForm.Archotech, new List<GodDomain> { GodDomain.Machine });
        }

        /// <summary>A faith that names no god but holds the world alive: its people have spirits.</summary>
        private static bool Spirits(string structure) =>
            structure.IndexOf("Animist", StringComparison.OrdinalIgnoreCase) >= 0 || structure.IndexOf("Pantheism", StringComparison.OrdinalIgnoreCase) >= 0
            || structure.IndexOf("Shinta", StringComparison.OrdinalIgnoreCase) >= 0 || structure.IndexOf("Neolithic", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Rimfolk's makers: two or three gods for a people (or a faith) by its age, its ways and its land. Idempotent.</summary>
        private static void AddMakers(GodBook b, Faction f, Ideo ideo, int age, int now, int count, GodForm? form, List<GodDomain> domains)
        {
            if (f == null) return;
            string keyBase = ideo != null ? "maker|i" + ideo.id : "maker|f" + f.loadID;
            var had = b.gods.Where(g => g.deityKey != null && g.deityKey.StartsWith(keyBase + "|")).ToList();
            if (had.Count > 0)
            {
                foreach (var g in had) Join(g, f, ideo, now);
                return;
            }
            int seed = GodsMath.Seed(WorldSeed, GodsMath.Seed(keyBase));
            string biome = null;
            bool water = false;
            try
            {
                var town = Find.WorldObjects?.Settlements?.FirstOrDefault(s => s.Faction == f);
                if (town == null && f.IsPlayer) town = Find.WorldObjects?.Settlements?.FirstOrDefault(s => s.Faction == Faction.OfPlayer);
                if (town != null)
                {
                    biome = Find.WorldGrid[town.Tile]?.PrimaryBiome?.defName;
                    water = Peoples.HasRiver(town.Tile) || Find.World.CoastDirectionAt(town.Tile).IsValid;
                }
            }
            catch
            {
                // The land unread: the age and the people's ways alone.
            }
            bool raiders = f.def != null && f.def.permanentEnemy;
            var list = domains ?? GodsMath.MakerDomains(age, raiders, biome, water, seed, count);
            for (int i = 0; i < list.Count; i++)
            {
                var g = Make(b, null, list[i], GodDomain.None, age, GodsMath.Seed(seed, i), "maker", null, form, now);
                if (g == null) continue;
                g.deityKey = keyBase + "|" + i;
                Join(g, f, ideo, now);
            }
        }

        /// <summary>A god made: its form by its age, its kind by its form, its nature by its domain, a name and an epithet not taken yet.</summary>
        private static God Make(GodBook b, string name, GodDomain d, GodDomain s, int age, int seed, string source, Gender? gender, GodForm? form, int now)
        {
            var def = GodDomainDef.Of(d);
            if (def == null) return null;
            var f = form ?? GodsMath.FormOf(age, d, s, seed);
            var kind = GodsMath.KindOfForm(f);
            var names = new HashSet<string>(b.gods.Select(g => g.name));
            var epithets = new HashSet<string>(b.gods.Select(g => g.epithet));
            name ??= GodsMath.PickName(def.NamesFor(f), names, seed) ?? def.LabelCap.Resolve();
            string epithet = GodsMath.PickName(def.epithets, epithets, GodsMath.Seed(seed, 7)) ?? def.epithets?.FirstOrDefault();
            var g = new God
            {
                name = name, epithet = epithet, domain = d, second = s, form = f, kind = kind, baseKind = kind, nature = GodsMath.NatureFor(d, kind, seed),
                gender = gender ?? GenderFor(f, seed), age = age, source = source, born = now, lastHeld = now
            };
            Colour(g);
            return b.Add(g);
        }

        /// <summary>A god's colours: its domain's first, and its second domain's (or its domain's second).</summary>
        private static void Colour(God g)
        {
            var def = GodDomainDef.Of(g.domain);
            if (def == null) return;
            g.colour = def.colour;
            var sec = g.second != GodDomain.None ? GodDomainDef.Of(g.second) : null;
            g.colour2 = sec != null ? sec.colour : def.colour2;
        }

        private static Gender GenderFor(GodForm f, int seed)
        {
            float r = GodsMath.Roll(seed, 23);
            if ((f == GodForm.Spirit || f == GodForm.Archotech || f == GodForm.MachineGod) && r < 0.3f) return Gender.None;
            return r < 0.65f ? (GodsMath.Roll(seed, 29) < 0.5f ? Gender.Male : Gender.Female) : GodsMath.Roll(seed, 31) < 0.5f ? Gender.Female : Gender.Male;
        }

        private static void Join(God g, Faction f, Ideo ideo, int now)
        {
            if (g == null) return;
            if (f != null && !g.factions.Contains(f)) g.factions.Add(f);
            if (ideo != null && !g.ideos.Contains(ideo)) g.ideos.Add(ideo);
            g.lastHeld = now;
        }

        // ================= The patrons =================

        private static void AddPatrons(GodBook b, int now)
        {
            var all = Find.FactionManager?.AllFactionsListForReading;
            if (all == null) return;
            foreach (var pd in DefDatabase<GodPatronDef>.AllDefsListForReading)
            {
                if (pd?.peoples == null) continue;
                var peoples = all.Where(f => Fits(f) && !f.IsPlayer && pd.peoples.Contains(f.def)).ToList();
                if (peoples.Count == 0) continue;
                var g = b.gods.FirstOrDefault(x => x.patron == pd.defName);
                if (g == null)
                {
                    // One of their faith's own gods where it fits: it keeps its faith's name and takes the patron's ways.
                    g = peoples.SelectMany(f => b.gods.Where(x => x.factions.Contains(f))).FirstOrDefault(x => x.patron == null && x.source == "deity" && x.domain == pd.domain);
                    if (g != null)
                    {
                        g.patron = pd.defName;
                        g.epithet = pd.epithet;
                        if (g.second == GodDomain.None && pd.second != GodDomain.None) g.second = pd.second;
                        if (pd.kind != GodKind.Ordinary) g.kind = g.baseKind = pd.kind;
                        g.nature = pd.nature;
                        Colour(g);
                    }
                    else
                    {
                        int age = peoples.Min(f => AgeOf(f));
                        int seed = GodsMath.Seed(WorldSeed, GodsMath.Seed(pd.defName));
                        var form = pd.Form ?? GodsMath.FormOf(age, pd.domain, pd.second, seed);
                        var kind = pd.kind != GodKind.Ordinary ? pd.kind : GodsMath.KindOfForm(form);
                        g = new God
                        {
                            name = pd.godName, epithet = pd.epithet, domain = pd.domain, second = pd.second, form = form, kind = kind, baseKind = kind,
                            nature = pd.nature, gender = pd.gender, age = age, source = "patron", patron = pd.defName, born = now, lastHeld = now
                        };
                        Colour(g);
                        b.Add(g);
                        if (b.built) AddTiesFor(b, g, now);
                    }
                }
                foreach (var f in peoples) Join(g, f, ModsConfig.IdeologyActive ? f.ideos?.PrimaryIdeo : null, now);
            }
        }

        // ================= The ones who don't fit =================

        /// <summary>A dead god in old places, and a fading god of a faith that died out before the colony came (once per world).</summary>
        private static void AddOld(GodBook b, int now)
        {
            if (b.gods.Any(g => g.source == "old")) return;
            int seed = GodsMath.Seed(WorldSeed, 991);
            var deadPool = new[] { GodDomain.Storm, GodDomain.Harvest, GodDomain.Dead, GodDomain.Night, GodDomain.Waters, GodDomain.Wild };
            var fadingPool = new[] { GodDomain.Harvest, GodDomain.Hearth, GodDomain.Waters, GodDomain.Way, GodDomain.Healing, GodDomain.Hunt };
            var taken = new HashSet<GodDomain>(b.gods.Select(g => g.domain));
            // Rather a domain nobody else holds, so the old ones aren't one of many.
            GodDomain Pick(GodDomain[] pool, int salt)
            {
                var free = pool.Where(d => !taken.Contains(d)).ToList();
                var from = free.Count > 0 ? free : pool.ToList();
                return from[(int)(GodsMath.Roll(seed, salt) * from.Count) % from.Count];
            }
            var dead = Make(b, null, Pick(deadPool, 1), GodDomain.None, 0, GodsMath.Seed(seed, 1), "old", null, GodForm.Spirit, now);
            if (dead != null)
            {
                dead.kind = dead.baseKind = GodKind.Dead;
                dead.nature = GodsMath.NatureFor(dead.domain, GodKind.Dead, seed);
                dead.epithet = OldEpithet("Dead", seed);
                taken.Add(dead.domain);
            }
            var fading = Make(b, null, Pick(fadingPool, 2), GodDomain.None, 1, GodsMath.Seed(seed, 2), "old", null, GodForm.OldGod, now);
            if (fading != null)
            {
                fading.kind = fading.baseKind = GodKind.Fading;
                fading.nature = GodsMath.NatureFor(fading.domain, GodKind.Fading, GodsMath.Seed(seed, 3));
                fading.epithet = OldEpithet("Fading", GodsMath.Seed(seed, 3));
            }
        }

        /// <summary>The number of old epithets each kind has (CC_God_Epithet_Dead_0..; CC_God_Epithet_Fading_0..).</summary>
        public const int OldEpithets = 5;

        private static string OldEpithet(string kind, int seed) => ("CC_God_Epithet_" + kind + "_" + (int)(GodsMath.Roll(seed, 53) * OldEpithets) % OldEpithets).Translate().Resolve();

        /// <summary>The Hunger (the Choir's, or the world's darkest void god) and one trickster, where the world has none yet.</summary>
        private static void MarkKinds(GodBook b)
        {
            int seed = GodsMath.Seed(WorldSeed, 773);
            if (!b.gods.Any(g => g.baseKind == GodKind.Hunger))
            {
                var hunger = b.gods.Where(g => g.domain == GodDomain.Void && g.patron == null && g.source != "old")
                    .OrderByDescending(g => g.ideos.Any(i => i.memes != null && i.memes.Any(m => m?.defName == "Inhuman")) ? 1 : 0).ThenBy(g => g.id).FirstOrDefault();
                if (hunger != null)
                {
                    hunger.kind = hunger.baseKind = GodKind.Hunger;
                    hunger.nature = GodNature.Jealous;
                }
            }
            if (!b.gods.Any(g => g.baseKind == GodKind.Trickster))
            {
                float Score(God g) => (g.domain == GodDomain.Way || g.domain == GodDomain.Night ? 2f : g.domain == GodDomain.Wild ? 1f : 0f)
                                      + (g.nature == GodNature.Playful || g.nature == GodNature.Capricious ? 1f : 0f) + GodsMath.Roll(seed, g.id);
                var trickster = b.gods.Where(g => g.patron == null && g.kind == GodKind.Ordinary && (g.source == "deity" || g.source == "maker"))
                    .OrderByDescending(Score).FirstOrDefault();
                if (trickster != null)
                {
                    trickster.kind = trickster.baseKind = GodKind.Trickster;
                    trickster.nature = GodNature.Playful;
                }
            }
        }

        // ================= Ties =================

        private static GodSketch Sketch(God g)
        {
            var faiths = g.ideos.Select(i => i.id).ToList();
            if (faiths.Count == 0) faiths.AddRange(g.factions.Select(f => 1000000 + f.loadID));
            return new GodSketch
            {
                id = g.id, domain = g.domain, second = g.second, age = g.age, faiths = faiths.ToArray(), peoples = g.factions.Select(f => f.loadID).ToArray(),
                kind = g.kind, gender = (int)g.gender
            };
        }

        private static Faction FactionById(int loadID) => Find.FactionManager?.AllFactionsListForReading.FirstOrDefault(f => f.loadID == loadID);

        private static int Relation(int p, int q)
        {
            var a = FactionById(p);
            var c = FactionById(q);
            if (a == null || c == null || a == c) return 0;
            try
            {
                var k = a.RelationKindWith(c);
                return k == FactionRelationKind.Hostile ? -1 : k == FactionRelationKind.Ally ? 1 : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static bool Arose(int child, int parent)
        {
            var c = FactionById(child);
            var p = FactionById(parent);
            return c != null && p != null && ArisingBook.Instance?.Of(c)?.parent == p;
        }

        private static void BuildTies(GodBook b, int now)
        {
            var made = GodTiesMath.Build(b.gods.Select(Sketch).ToList(), Relation, Arose, GodsMath.Seed(WorldSeed, 313));
            b.ties = made.Select(t => new GodTie { a = t.a, b = t.b, kind = t.kind, strength = t.strength, since = now }).ToList();
        }

        /// <summary>A god new to the pantheon takes its ties with those who have room for one.</summary>
        private static void AddTiesFor(GodBook b, God g, int now)
        {
            var me = Sketch(g);
            int seed = GodsMath.Seed(WorldSeed, 313);
            foreach (var other in b.gods.ToList())
            {
                if (other == g || b.Tie(g.id, other.id) != null) continue;
                if (b.TiesOf(g.id).Count() >= GodTiesMath.MaxTies) break;
                if (b.TiesOf(other.id).Count() >= GodTiesMath.MaxTies) continue;
                var t = GodTiesMath.Pair(me, Sketch(other), Relation, Arose, seed);
                if (!t.HasValue) continue;
                var k = t.Value.kind;
                if ((k == GodTieKind.Lovers || k == GodTieKind.Estranged) && (b.TiesOf(g.id).Any(x => x.kind == k) || b.TiesOf(other.id).Any(x => x.kind == k))) continue;
                b.ties.Add(new GodTie { a = t.Value.a, b = t.Value.b, kind = k, strength = t.Value.strength, since = now });
            }
        }

        /// <summary>The region's day: now and then a tie shifts (rivals cool, lovers part, the estranged come back), and a new one forms.</summary>
        public static void ShiftTies(GodBook b, int now)
        {
            foreach (var t in b.ties.ToList())
            {
                if (!Rand.Chance(GodTiesMath.ShiftChancePerDay)) continue;
                GodTiesMath.Shift(t.kind, t.strength, Rand.Value, Rand.Value, out var next, out float strength, out bool gone);
                var ga = b.ById(t.a);
                var gb = b.ById(t.b);
                if (ga == null || gb == null) continue;
                if (gone)
                {
                    b.ties.Remove(t);
                    TieAct(b, ga, gb, "Gone", t.kind, now);
                    continue;
                }
                bool changed = next != t.kind;
                t.strength = strength;
                if (!changed) continue;
                var was = t.kind;
                t.kind = next;
                t.since = now;
                TieAct(b, ga, gb, next.ToString(), was, now);
            }
            if (!Rand.Chance(GodTiesMath.NewTieChancePerDay) || b.gods.Count < 2) return;
            var open = b.gods.Where(g => g.kind != GodKind.Newborn && b.TiesOf(g.id).Count() < GodTiesMath.MaxTies).ToList();
            if (open.Count < 2) return;
            var x = open.RandomElement();
            var y = open.Where(o => o != x && b.Tie(x.id, o.id) == null).RandomElementWithFallback();
            if (y == null) return;
            var made = GodTiesMath.Pair(Sketch(x), Sketch(y), Relation, Arose, Rand.Int);
            if (!made.HasValue) return;
            var k = made.Value.kind;
            if ((k == GodTieKind.Lovers || k == GodTieKind.Estranged) && (b.TiesOf(x.id).Any(z => z.kind == k) || b.TiesOf(y.id).Any(z => z.kind == k))) return;
            b.ties.Add(new GodTie { a = made.Value.a, b = made.Value.b, kind = k, strength = made.Value.strength, since = now });
            TieAct(b, b.ById(made.Value.a), b.ById(made.Value.b), "New" + k, k, now);
        }

        /// <summary>A tie changed, remembered on both gods' pages (CC_God_TieAct_&lt;what&gt;).</summary>
        private static void TieAct(GodBook b, God a, God c, string what, GodTieKind was, int now)
        {
            if (a == null || c == null) return;
            string text = ("CC_God_TieAct_" + what).Translate(a.name.Named("A"), c.name.Named("B"), GodWords.TieWord(was).Named("WAS")).Resolve();
            b.Remember(new GodAct { god = a.id, other = c.id, tick = now, kind = GodActKind.Tie, text = text });
            b.Remember(new GodAct { god = c.id, other = a.id, tick = now, kind = GodActKind.Tie, text = text });
        }

        // ================= Keeping it right =================

        /// <summary>
        /// The region's day (and every load): new peoples and faiths get their gods (and their ties), deities renamed or added
        /// in a reform are followed, peoples and faiths gone are let go, a god with nobody left fades (and in time falls
        /// silent), one taken up again comes back, and the colony learns the gods of its faith and its neighbours.
        /// <paramref name="tell"/> false (a load): nothing told, only kept.
        /// </summary>
        public static void Keep(GodBook b, int now, bool tell = true)
        {
            if (b == null) return;
            if (!b.built)
            {
                Build(b);
                return;
            }
            int before = b.gods.Count;
            foreach (var f in Factions()) AddFaction(b, f, now);
            AddPatrons(b, now);
            var factions = new HashSet<Faction>(Find.FactionManager?.AllFactionsListForReading ?? new List<Faction>());
            var ideos = new HashSet<Ideo>(Find.IdeoManager?.IdeosListForReading ?? new List<Ideo>());
            foreach (var g in b.gods)
            {
                g.factions.RemoveAll(f => f == null || f.defeated || !factions.Contains(f));
                g.ideos.RemoveAll(i => i == null || !ideos.Contains(i) || g.deityKey != null && g.deityKey.StartsWith(i.id + "|") && !HasDeity(i, g.deityKey));
                // A faith's gods held by a people that no longer keeps that faith (or the faith gone): let go of the people.
                if (ModsConfig.IdeologyActive && (g.source == "deity" || g.source == "maker" && g.deityKey != null && g.deityKey.StartsWith("maker|i")))
                    g.factions.RemoveAll(f => !FaithsOf(f).Any(i => g.ideos.Contains(i)));
                bool held = g.Worshipped;
                if (held) g.lastHeld = now;
                var k = GodsMath.KindNow(g.kind, g.baseKind, held, (now - g.lastHeld) / GenDate.TicksPerDay);
                if (k == g.kind) continue;
                var was = g.kind;
                g.kind = k;
                string what = k == GodKind.Fading ? "Fading" : k == GodKind.Dead ? "Dead" : "Returned";
                b.Remember(new GodAct
                {
                    god = g.id, tick = now, kind = k == GodKind.Fading || k == GodKind.Dead ? GodActKind.Faded : GodActKind.Returned,
                    text = ("CC_God_KindAct_" + what).Translate(g.name.Named("GOD")).Resolve()
                });
                if (g.known && tell) Tell.Minor(NewsKind.World, Importance.Flavor, null, ("CC_God_KindNews_" + what).Translate(g.name.Named("GOD"), Gods.GodOf(g).Named("GODOF")).Resolve(), null);
            }
            for (int i = before; i < b.gods.Count; i++)
            {
                var g = b.gods[i];
                AddTiesFor(b, g, now);
                b.Remember(new GodAct { god = g.id, tick = now, kind = GodActKind.Born, text = "CC_God_BornAct".Translate(g.name.Named("GOD")).Resolve() });
            }
            if (b.gods.Count != before) MarkKinds(b);
            LearnColony(b, now, tell);
            LearnFromNotables(b, tell);
            Gods.Changed();
        }

        private static bool HasDeity(Ideo ideo, string key)
        {
            var deities = (ideo?.foundation as IdeoFoundation_Deity)?.DeitiesListForReading;
            if (deities == null) return false;
            return deities.Any(d => d != null && ideo.id + "|" + d.name == key);
        }

        // ================= A newborn =================

        /// <summary>
        /// The region's day: at a home whose deeds keep serving a domain the colony's faith has no god of
        /// (GodsMath.NewbornDue), a god is born of the colony's own faith: small, weak, and growing with its devotion.
        /// </summary>
        public static void Newborn(GodBook b, int now)
        {
            if (b.newborns >= GodsMath.MaxNewborns) return;
            int since = b.lastNewborn < 0 ? int.MaxValue : (now - b.lastNewborn) / GenDate.TicksPerDay;
            foreach (var map in Homes.All)
            {
                int home = Homes.KeyOf(map);
                var colony = Gods.ColonyGods(map);
                Ideo faith = ModsConfig.IdeologyActive ? Faction.OfPlayer?.ideos?.PrimaryIdeo : null;
                bool hasFaith = !ModsConfig.IdeologyActive || faith != null;
                foreach (var d in GodsMath.Domains)
                {
                    bool has = colony.Any(g => g.domain == d || g.second == d);
                    if (!GodsMath.NewbornDue(b.DeedsThisYear(d, home), has, hasFaith, b.newborns, since)) continue;
                    Born(b, map, d, faith, now);
                    return;
                }
            }
        }

        /// <summary>A newborn god of the colony's faith at this home (also the dev tools').</summary>
        public static God Born(GodBook b, Map map, GodDomain d, Ideo faith, int now)
        {
            var def = GodDomainDef.Of(d);
            if (def == null) return null;
            int seed = GodsMath.Seed(WorldSeed, now, (int)d);
            var form = GodsMath.FormOf((int)Ages.Ours, d, GodDomain.None, seed);
            string home = Homes.Name(map) ?? Faction.OfPlayer?.Name ?? "";
            var g = new God
            {
                name = def.newbornName.NullOrEmpty() ? def.LabelCap.Resolve() : def.newbornName, epithet = "CC_God_NewbornEpithet".Translate(home.Named("HOME")).Resolve(),
                domain = d, form = form, kind = GodKind.Newborn, baseKind = GodKind.Newborn, nature = GodsMath.NatureFor(d, GodKind.Newborn, seed),
                gender = GenderFor(form, seed), age = (int)Ages.Ours, source = "newborn", born = now, lastHeld = now, deityKey = "newborn|" + now
            };
            Colour(g);
            b.Add(g);
            Join(g, Faction.OfPlayer, faith, now);
            b.newborns++;
            b.lastNewborn = now;
            Gods.Learn(g, "faith", false);
            int key = Homes.KeyOf(map);
            b.Move(g, key, 15f, out _, out _);
            string text = "CC_God_NewbornText".Translate(g.name.Named("GOD"), Gods.AGodOf(g).Named("GODOF"), home.Named("HOME"), def.sacredDeed.Named("DEED")).Resolve();
            b.Remember(new GodAct { god = g.id, home = key, tick = now, kind = GodActKind.Born, text = "CC_God_NewbornAct".Translate(home.Named("HOME")).Resolve() });
            Tell.Letter(NewsKind.Colony, Importance.Notable, map, "CC_God_NewbornLabel".Translate(g.name.Named("GOD")).Resolve(), text, LetterDefOf.PositiveEvent);
            Gods.Changed();
            return g;
        }

        // ================= What the colony knows =================

        /// <summary>The colony knows the gods of its own faith (and its own makers' and newborns').</summary>
        public static void LearnColony(GodBook b, int now, bool tell)
        {
            var player = Faction.OfPlayerSilentFail;
            if (player == null) return;
            var faiths = new HashSet<Ideo>();
            if (ModsConfig.IdeologyActive)
                foreach (var p in PawnsFinder.AllMaps_FreeColonists)
                    if (p?.Ideo != null)
                        faiths.Add(p.Ideo);
            foreach (var g in b.gods)
                if (!g.known && (g.factions.Contains(player) || g.ideos.Any(faiths.Contains)))
                    Gods.Learn(g, "faith", tell);
        }

        /// <summary>The colony knows the gods of the peoples whose people it knows by name (its neighbours).</summary>
        public static void LearnFromNotables(GodBook b, bool tell)
        {
            var book = NotableBook.Instance;
            if (book?.notables == null) return;
            foreach (var f in book.notables.Where(n => n?.faction != null).Select(n => n.faction).Distinct().ToList())
                Gods.Learn(f, "neighbours", tell);
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
            foreach (var f in seen) Gods.Learn(f, "visitors", true);
        }
    }
}
