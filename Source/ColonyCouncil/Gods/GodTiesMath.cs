using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// A tie between two gods (the player, 11 November 2026: "there's drama between the gods"). The first god of a tie is
    /// the one it starts from where that matters: the parent of a child, the one who cast the other down or stole the
    /// other's people.
    /// </summary>
    public enum GodTieKind
    {
        Rivals,
        Lovers,
        /// <summary>Lovers once, apart now.</summary>
        Estranged,
        /// <summary>The first is the second's parent.</summary>
        Parent,
        Siblings,
        Twins,
        Allies,
        /// <summary>The first cast the second down.</summary>
        CastDown,
        /// <summary>The first stole the second's people.</summary>
        StolePeople
    }

    /// <summary>What the ties' rules need of a god, with no game types.</summary>
    public struct GodSketch
    {
        public int id;
        public GodDomain domain, second;
        /// <summary>The age of its people (0 tribal to 3 spacer).</summary>
        public int age;
        /// <summary>The faiths that hold it (ideoligions, or the people itself without one), as numbers.</summary>
        public int[] faiths;
        /// <summary>The peoples that hold it, as numbers.</summary>
        public int[] peoples;
        public GodKind kind;
        /// <summary>0 none, 1 male, 2 female (the game's Gender).</summary>
        public int gender;
    }

    /// <summary>A tie as the rules make it.</summary>
    public struct TieSketch
    {
        public int a, b;
        public GodTieKind kind;
        /// <summary>How strong it is, 0.1 to 1.</summary>
        public float strength;
    }

    /// <summary>
    /// The pure rules of the ties between gods (docs/design/gods.md, "Drama between the gods"): a table of natural rivals
    /// and allies by domain (the storm and the harvest over the rain, the hunter and the herd over the beasts), the faiths
    /// that share or fight over them, and chance; the family a faith's own gods make; how favour with one god spills onto
    /// those it is tied to; and how ties shift slowly over the years. No game types: Tools/Tests checks them.
    /// </summary>
    public static class GodTiesMath
    {
        /// <summary>A god keeps at most this many ties (the strongest).</summary>
        public const int MaxTies = 4;

        /// <summary>
        /// How two domains stand by nature: below 0 rivals, above 0 allies, and whether they are the kind that love and quarrel
        /// (the storm and the harvest, the sea and the land). The reason, in words, is the key CC_God_Over_&lt;a&gt;_&lt;b&gt;.
        /// </summary>
        private static readonly Dictionary<(GodDomain, GodDomain), (float aff, bool romance)> Natural = BuildNatural();

        private static Dictionary<(GodDomain, GodDomain), (float, bool)> BuildNatural()
        {
            var t = new Dictionary<(GodDomain, GodDomain), (float, bool)>();
            void N(GodDomain a, GodDomain b, float aff, bool romance = false)
            {
                t[(a, b)] = (aff, romance);
                t[(b, a)] = (aff, romance);
            }
            // Rivals.
            N(GodDomain.Storm, GodDomain.Harvest, -0.8f, true); // over the rain
            N(GodDomain.Hunt, GodDomain.Wild, -0.6f, true); // over the beasts
            N(GodDomain.War, GodDomain.Healing, -0.7f); // the one breaks what the other mends
            N(GodDomain.War, GodDomain.Hearth, -0.5f); // over the young who go off to fight
            N(GodDomain.Healing, GodDomain.Dead, -0.6f); // over who goes and who stays
            N(GodDomain.Machine, GodDomain.Wild, -0.7f); // over the land
            N(GodDomain.Craft, GodDomain.Wild, -0.4f); // the forest felled for the forge
            N(GodDomain.Waters, GodDomain.Hearth, -0.4f, true); // water and fire
            N(GodDomain.Way, GodDomain.Hearth, -0.3f, true); // the road calls people from home
            N(GodDomain.Void, GodDomain.Dead, -0.8f); // the Hunger takes what is the dead's
            N(GodDomain.Void, GodDomain.Hearth, -0.6f);
            N(GodDomain.Void, GodDomain.Healing, -0.5f);
            N(GodDomain.Night, GodDomain.Hearth, -0.3f, true); // the dark and the lamp
            N(GodDomain.Storm, GodDomain.Waters, -0.3f, true); // over the sea's moods
            N(GodDomain.Machine, GodDomain.Hunt, -0.3f);
            // Allies.
            N(GodDomain.Harvest, GodDomain.Waters, 0.7f, true); // rain and rivers on the fields
            N(GodDomain.Harvest, GodDomain.Hearth, 0.6f);
            N(GodDomain.Hunt, GodDomain.Night, 0.5f, true); // the moonlit hunt
            N(GodDomain.Craft, GodDomain.Machine, 0.6f);
            N(GodDomain.Craft, GodDomain.Hearth, 0.4f);
            N(GodDomain.Way, GodDomain.Waters, 0.5f); // rivers are roads
            N(GodDomain.Healing, GodDomain.Hearth, 0.5f);
            N(GodDomain.Dead, GodDomain.Night, 0.6f, true);
            N(GodDomain.War, GodDomain.Craft, 0.5f); // the smith arms the captain
            N(GodDomain.Storm, GodDomain.War, 0.4f);
            N(GodDomain.Wild, GodDomain.Waters, 0.5f);
            N(GodDomain.Harvest, GodDomain.Wild, 0.3f, true);
            N(GodDomain.Way, GodDomain.Night, 0.3f); // the stars that guide the road
            N(GodDomain.Machine, GodDomain.Void, 0.2f);
            return t;
        }

        /// <summary>How two domains stand by nature (-1 rivals to 1 allies), and whether they are a pair that loves and quarrels.</summary>
        public static float Affinity(GodDomain a, GodDomain b, out bool romance)
        {
            romance = false;
            if (a == GodDomain.None || b == GodDomain.None || a == b) return 0f;
            if (Natural.TryGetValue((a, b), out var v))
            {
                romance = v.romance;
                return v.aff;
            }
            return 0f;
        }

        /// <summary>How two gods stand by their domains: their first domains fully, their second ones by half.</summary>
        public static float Affinity(GodSketch x, GodSketch y, out bool romance)
        {
            float a = Affinity(x.domain, y.domain, out romance);
            float b = 0.5f * Affinity(x.second, y.domain, out bool r2) + 0.5f * Affinity(x.domain, y.second, out bool r3);
            romance |= r2 || r3;
            return Math.Max(-1f, Math.Min(1f, a + b));
        }

        /// <summary>Whether the two share a faith (their family).</summary>
        public static bool ShareFaith(GodSketch x, GodSketch y) => x.faiths != null && y.faiths != null && x.faiths.Any(f => y.faiths.Contains(f));

        /// <summary>
        /// The ties of a pantheon. <paramref name="peoples"/>: how two peoples stand (-1 at war, 0, 1 allies);
        /// <paramref name="arose"/>: the first people arose from the second (its people taken from it). A faith's own gods are
        /// a family: parent and child, siblings, twins, lovers, now and then rivals. Across faiths: the same domain makes
        /// rivals or twins; the natural table makes rivals and allies (the romantic pairs lovers or lovers estranged); peoples
        /// at war make rivals of their gods; a newer age's god may have cast an older one down; a people that arose from
        /// another took its people from that one's god. Each god keeps its <see cref="MaxTies"/> strongest, and one lover.
        /// </summary>
        public static List<TieSketch> Build(IList<GodSketch> gods, Func<int, int, int> peoples, Func<int, int, bool> arose, int seed)
        {
            var all = new List<TieSketch>();
            if (gods == null) return all;
            for (int i = 0; i < gods.Count; i++)
                for (int j = i + 1; j < gods.Count; j++)
                {
                    var t = Pair(gods[i], gods[j], peoples, arose, seed);
                    if (t.HasValue) all.Add(t.Value);
                }
            return Keep(all, gods.Select(g => g.id));
        }

        /// <summary>The one tie (or none) between two gods, by the rules of <see cref="Build"/>.</summary>
        public static TieSketch? Pair(GodSketch x, GodSketch y, Func<int, int, int> peoples, Func<int, int, bool> arose, int seed)
        {
            if (x.id == y.id) return null;
            // The dead and the newborn have no part in the old quarrels (a newborn's come with time).
            if (x.kind == GodKind.Newborn || y.kind == GodKind.Newborn) return null;
            int s = GodsMath.Seed(seed, Math.Min(x.id, y.id), Math.Max(x.id, y.id));
            float r = GodsMath.Roll(s, 1), r2 = GodsMath.Roll(s, 2);
            float aff = Affinity(x, y, out bool romance);
            // The older (or first-made) god leads where the tie has a direction.
            bool xFirst = x.age < y.age || x.age == y.age && x.id < y.id;
            var older = xFirst ? x : y;
            var younger = xFirst ? y : x;
            int rel = PeoplesRelation(x, y, peoples);
            // A people that arose from another: its god took the other's people.
            if (arose != null && Arose(x, y, arose)) return T(x.id, y.id, GodTieKind.StolePeople, 0.7f);
            if (arose != null && Arose(y, x, arose)) return T(y.id, x.id, GodTieKind.StolePeople, 0.7f);
            if (ShareFaith(x, y))
            {
                // A faith's gods are a family.
                if (aff <= -0.5f && r < 0.45f) return T(x.id, y.id, GodTieKind.Rivals, Math.Abs(aff));
                if (romance && x.gender != y.gender && r < 0.4f) return T(x.id, y.id, GodTieKind.Lovers, 0.6f + 0.3f * r2);
                if (x.domain == y.domain && r < 0.5f) return T(x.id, y.id, GodTieKind.Twins, 0.8f);
                if (r < 0.35f) return T(older.id, younger.id, GodTieKind.Parent, 0.6f + 0.3f * r2);
                if (r < 0.8f) return T(x.id, y.id, GodTieKind.Siblings, 0.5f + 0.3f * r2);
                return aff >= 0.3f ? T(x.id, y.id, GodTieKind.Allies, 0.4f + aff * 0.4f) : (TieSketch?)null;
            }
            if (x.domain == y.domain && x.domain != GodDomain.None)
            {
                // Two gods of one thing, held by different peoples.
                if (rel > 0 && r < 0.5f) return T(x.id, y.id, GodTieKind.Allies, 0.5f);
                if (r < 0.15f) return T(x.id, y.id, GodTieKind.Twins, 0.6f);
                if (older.age < younger.age && r < 0.3f) return T(younger.id, older.id, GodTieKind.CastDown, 0.6f);
                if (r < 0.75f || rel < 0) return T(x.id, y.id, GodTieKind.Rivals, 0.6f + 0.3f * r2);
                return null;
            }
            if (aff <= -0.5f)
            {
                if (romance && x.gender != y.gender && r < 0.3f) return T(x.id, y.id, GodTieKind.Estranged, 0.5f + 0.4f * r2);
                if (r < 0.7f || rel < 0) return T(x.id, y.id, GodTieKind.Rivals, Math.Abs(aff) * (0.7f + 0.3f * r2));
                return null;
            }
            if (aff >= 0.5f)
            {
                if (romance && x.gender != y.gender && r < 0.25f) return T(x.id, y.id, GodTieKind.Lovers, 0.5f + 0.4f * r2);
                if (rel >= 0 && r < 0.6f) return T(x.id, y.id, GodTieKind.Allies, aff * (0.7f + 0.3f * r2));
                return null;
            }
            // Peoples at war set their gods against each other; allies' gods stand together.
            if (rel < 0 && r < 0.35f)
            {
                if (older.age < younger.age && r2 < 0.25f) return T(younger.id, older.id, GodTieKind.CastDown, 0.5f);
                return T(x.id, y.id, GodTieKind.Rivals, 0.4f + 0.3f * r2);
            }
            if (rel > 0 && r < 0.25f) return T(x.id, y.id, GodTieKind.Allies, 0.4f + 0.2f * r2);
            // And now and then a love nobody would have guessed.
            if (x.gender != y.gender && r < 0.02f) return T(x.id, y.id, r2 < 0.5f ? GodTieKind.Lovers : GodTieKind.Estranged, 0.4f + 0.3f * r2);
            return null;
        }

        private static TieSketch? T(int a, int b, GodTieKind k, float strength) =>
            new TieSketch { a = a, b = b, kind = k, strength = Math.Max(0.1f, Math.Min(1f, strength)) };

        /// <summary>How the peoples of two gods stand: at war if any are, allies if any are and none at war, else 0.</summary>
        private static int PeoplesRelation(GodSketch x, GodSketch y, Func<int, int, int> peoples)
        {
            if (peoples == null || x.peoples == null || y.peoples == null) return 0;
            int best = 0;
            foreach (int p in x.peoples)
                foreach (int q in y.peoples)
                {
                    if (p == q) continue;
                    int r = peoples(p, q);
                    if (r < 0) return -1;
                    if (r > 0) best = 1;
                }
            return best;
        }

        private static bool Arose(GodSketch child, GodSketch parent, Func<int, int, bool> arose)
        {
            if (child.peoples == null || parent.peoples == null) return false;
            foreach (int c in child.peoples)
                foreach (int p in parent.peoples)
                    if (c != p && arose(c, p))
                        return true;
            return false;
        }

        /// <summary>The ties each god keeps: its <see cref="MaxTies"/> strongest, one lover and one estranged at most.</summary>
        public static List<TieSketch> Keep(IEnumerable<TieSketch> ties, IEnumerable<int> ids)
        {
            var count = new Dictionary<int, int>();
            var lover = new HashSet<int>();
            var estranged = new HashSet<int>();
            foreach (int id in ids) count[id] = 0;
            var kept = new List<TieSketch>();
            // Kin and old wrongs first (they are the story), then the strongest.
            foreach (var t in ties.OrderByDescending(t => Weight(t.kind)).ThenByDescending(t => t.strength).ThenBy(t => t.a).ThenBy(t => t.b))
            {
                if (count.TryGetValue(t.a, out int ca) && ca >= MaxTies || count.TryGetValue(t.b, out int cb) && cb >= MaxTies) continue;
                if (t.kind == GodTieKind.Lovers && (lover.Contains(t.a) || lover.Contains(t.b))) continue;
                if (t.kind == GodTieKind.Estranged && (estranged.Contains(t.a) || estranged.Contains(t.b))) continue;
                if (t.kind == GodTieKind.Lovers)
                {
                    lover.Add(t.a);
                    lover.Add(t.b);
                }
                if (t.kind == GodTieKind.Estranged)
                {
                    estranged.Add(t.a);
                    estranged.Add(t.b);
                }
                count[t.a] = (count.TryGetValue(t.a, out int na) ? na : 0) + 1;
                count[t.b] = (count.TryGetValue(t.b, out int nb) ? nb : 0) + 1;
                kept.Add(t);
            }
            return kept;
        }

        /// <summary>Which ties a god keeps first when it has too many.</summary>
        public static int Weight(GodTieKind k) => k switch
        {
            GodTieKind.Parent => 6,
            GodTieKind.Twins => 6,
            GodTieKind.Lovers => 5,
            GodTieKind.StolePeople => 5,
            GodTieKind.CastDown => 5,
            GodTieKind.Siblings => 4,
            GodTieKind.Estranged => 4,
            GodTieKind.Rivals => 3,
            _ => 2
        };

        /// <summary>A tie that sets the two against each other (favour with one displeases the other).</summary>
        public static bool Hostile(GodTieKind k) => k == GodTieKind.Rivals || k == GodTieKind.Estranged || k == GodTieKind.CastDown || k == GodTieKind.StolePeople;

        /// <summary>A tie of blood (it never breaks, only cools or warms).</summary>
        public static bool Kin(GodTieKind k) => k == GodTieKind.Parent || k == GodTieKind.Siblings || k == GodTieKind.Twins;

        // ================= Favour spills over =================

        /// <summary>How much of a gain a rival feels against it, and an ally with it (times the tie's strength).</summary>
        public const float RivalShare = 0.25f, AllyShare = 0.15f, KinShare = 0.1f, LoverShare = 0.2f, EstrangedShare = 0.2f, WrongShare = 0.3f;

        /// <summary>
        /// What a change of favour with one god does to another it is tied to (the player: "raising favour with a god a
        /// little displeases its rivals and a little pleases its allies"). <paramref name="fromFirst"/>: the god whose
        /// favour changed is the tie's first (it matters for a wrong: honouring the one cast down galls the one who cast it,
        /// a little; honouring the one who did it galls the wronged one more). A loss spills half as far. A jealous god feels
        /// a rival's gain half again. Never more than the change itself.
        /// </summary>
        public static float Spill(float amount, GodTieKind kind, float strength, bool fromFirst, GodNature other)
        {
            if (float.IsNaN(amount) || amount == 0f || float.IsNaN(strength) || strength <= 0f) return 0f;
            float share;
            switch (kind)
            {
                case GodTieKind.Rivals: share = -RivalShare; break;
                case GodTieKind.Estranged: share = -EstrangedShare; break;
                case GodTieKind.CastDown:
                case GodTieKind.StolePeople: share = fromFirst ? -WrongShare : -WrongShare * 0.4f; break;
                case GodTieKind.Allies: share = AllyShare; break;
                case GodTieKind.Lovers: share = LoverShare; break;
                default: share = KinShare; break;
            }
            float s = amount * share * Math.Min(1f, strength);
            if (amount < 0f) s *= 0.5f;
            if (s < 0f && other == GodNature.Jealous) s *= 1.5f;
            if (Math.Abs(s) > Math.Abs(amount)) s = Math.Sign(s) * Math.Abs(amount);
            return s;
        }

        // ================= Ties shift slowly =================

        /// <summary>A tie's chance to shift on a given day (about once in two years each).</summary>
        public const float ShiftChancePerDay = 1f / 120f;

        /// <summary>A new tie between two untied gods: this chance on a day for the whole pantheon.</summary>
        public const float NewTieChancePerDay = 1f / 30f;

        /// <summary>
        /// A tie shifted (two rolls in [0, 1)): rivals cool, and may come to stand together; allies fall out; lovers part;
        /// the estranged come back to each other, or turn rivals; kin warm or cool but stay kin; an old wrong fades a little.
        /// <paramref name="gone"/>: a rivalry or alliance cooled to nothing.
        /// </summary>
        public static void Shift(GodTieKind kind, float strength, float r1, float r2, out GodTieKind next, out float nextStrength, out bool gone)
        {
            next = kind;
            nextStrength = strength;
            gone = false;
            switch (kind)
            {
                case GodTieKind.Rivals:
                    if (r1 < 0.15f) next = GodTieKind.Allies;
                    else nextStrength = strength + (r2 < 0.6f ? -0.2f : 0.15f);
                    break;
                case GodTieKind.Allies:
                    if (r1 < 0.2f) next = GodTieKind.Rivals;
                    else nextStrength = strength + (r2 < 0.5f ? -0.15f : 0.15f);
                    break;
                case GodTieKind.Lovers:
                    if (r1 < 0.35f) next = GodTieKind.Estranged;
                    else nextStrength = strength + 0.1f;
                    break;
                case GodTieKind.Estranged:
                    if (r1 < 0.25f) next = GodTieKind.Lovers;
                    else if (r1 < 0.45f) next = GodTieKind.Rivals;
                    else nextStrength = strength - 0.1f;
                    break;
                case GodTieKind.CastDown:
                case GodTieKind.StolePeople:
                    nextStrength = strength - 0.1f;
                    break;
                default:
                    nextStrength = strength + (r2 < 0.5f ? -0.1f : 0.1f);
                    break;
            }
            nextStrength = Math.Max(0f, Math.Min(1f, nextStrength));
            if (next != kind) nextStrength = Math.Max(0.4f, nextStrength);
            // Kin are kin for good; a wrong is remembered faintly; the rest can come to nothing.
            if (Kin(next)) nextStrength = Math.Max(0.2f, nextStrength);
            else if (next == GodTieKind.CastDown || next == GodTieKind.StolePeople) nextStrength = Math.Max(0.15f, nextStrength);
            else if (nextStrength < 0.1f) gone = true;
        }
    }
}
