using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// A colonist as a wearer, without game types: how they fight (a shooter, a melee fighter, both, or not at all), the
    /// body part groups they no longer have, where they are (the temperature outside now and the season's coldest and
    /// hottest), how warm and cool they are kept by what they wear without this piece and what it would replace, and the
    /// moods their other clothes already give.
    /// </summary>
    public sealed class ApparelWearer
    {
        public string Name = "";
        /// <summary>shooter, melee, both, or none (incapable of violence).</summary>
        public string Kind = "both";
        /// <summary>Why, in words: "holds an assault rifle", "a brawler", "Melee 12 against Shooting 4".</summary>
        public string KindWhy = "";
        /// <summary>Body part groups no part of theirs carries any more (both legs gone: Legs; the left hand: LeftHand).</summary>
        public List<string> MissingGroups = new List<string>();
        public string MissingWhy = "";
        /// <summary>The temperatures where they are: outside now, and the season's coldest night and hottest day.</summary>
        public bool HasClimate;
        public float OutdoorNow = 21f, SeasonLow = 21f, SeasonHigh = 21f;
        /// <summary>Their comfortable range by what they wear, without this piece and without what it would replace.</summary>
        public float ComfyMinWithout = 16f, ComfyMaxWithout = 26f;
        /// <summary>The apparel-damaged stage their other clothes already give (-1 none, 0 below half, 1 below a fifth), and the game's moods for it.</summary>
        public int DamageStageKept = -1;
        public (string label, float mood) Frayed, Tattered;
        /// <summary>The piece is made for the other gender, the game's mood for it, and whether something else they keep already gives it.</summary>
        public bool WrongGender, WrongGenderAlready;
        public (string label, float mood) WrongGenderMood;
    }

    /// <summary>A piece's rating for one colonist, the steps that move it from the usual rating, and how warm and cool it keeps them.</summary>
    public sealed class ApparelForColonist
    {
        public float Rating, Reference;
        public string Kind = "both";
        /// <summary>The steps from the usual rating, in order: how they fight, their body, where they live, moods.</summary>
        public List<(string why, float delta)> Why = new List<(string, float)>();
        /// <summary>Degrees this piece lowers their least comfortable temperature (warmth) and raises their most (cooling).</summary>
        public float Warmth, Cooling;
        /// <summary>Their comfortable range with this piece on, with what else they keep wearing.</summary>
        public float ComfyMinWith, ComfyMaxWith;
        public float Coldest, Hottest;
        /// <summary>How many degrees colder and hotter it gets than they can bear without this piece.</summary>
        public float NeedWarmth, NeedCooling;
        public bool HasClimate;
        public bool WarmEnough => !HasClimate || ComfyMinWith <= Coldest;
        public bool CoolEnough => !HasClimate || ComfyMaxWith >= Hottest;
    }

    /// <summary>A region of the body and how much of it a piece covers: its share of the shots that land on a body, and of those the share it covers.</summary>
    public sealed class ApparelRegion
    {
        public string Key;
        public float Shots;
        public float Covered;
    }

    /// <summary>
    /// The apparel rating for one colonist (docs/design/apparel.md, step 2), without game types: the usual rating moved, step
    /// by step, by how they fight (a shield belt is a melee fighter's, nothing for a shooter), the body parts they have, the
    /// warmth or cooling their climate asks of this piece on top of what they keep wearing, and the moods it would add.
    /// Also the piece's protection by region of the body, and the order of its sub-ratings.
    /// </summary>
    public static class ApparelForMath
    {
        /// <summary>Degrees of insulation beyond what their season asks count at a fifth: spare for a colder snap, a trip.</summary>
        public const float SpareShare = 0.2f;

        /// <summary>A shooter's and a melee fighter's rating are both shown when they differ by half a point (the review's rule).</summary>
        public const float ShowBothAt = 0.5f;

        public static bool BothDiffer(ApparelRating r) => r != null && Math.Abs(r.OverallShooter - r.OverallMelee) >= ShowBothAt;

        /// <summary>The body without the zones whose groups they no longer have (null: the whole body).</summary>
        public static ApparelZone[] BodyWithout(IList<string> missing)
        {
            if (missing == null || missing.Count == 0) return null;
            var zones = ApparelRateMath.Human.Where(z => !z.Groups.Any(missing.Contains)).ToArray();
            return zones.Length == ApparelRateMath.Human.Length || zones.Length == 0 ? null : zones;
        }

        /// <summary>
        /// The fight with the piece on, for one way of fighting, on a body (null: the whole of it): toughness times output
        /// times feet as the rating works them out; someone who can't fight only lasts and walks; both ways, the rating's own
        /// mix, three shots to one blow.
        /// </summary>
        public static float FightAs(ApparelFacts a, string kind, IList<ApparelZone> body)
        {
            var tS = ApparelRateMath.Toughness(a, false, body);
            var tM = ApparelRateMath.Toughness(a, true, body);
            var (oS, oM) = ApparelRateMath.Output(a);
            var (mS, mM) = ApparelRateMath.Feet(a);
            float pS = ApparelRateMath.PooledToughness(tS, a), pM = ApparelRateMath.PooledToughness(tM, a);
            float s = pS * oS * mS, m = pM * oM * mM;
            switch (kind)
            {
                case "shooter": return s;
                case "melee": return m;
                case "none": return pS * mS;
                default:
                    return (float)Math.Exp(ApparelRateMath.ShooterShare * Math.Log(Math.Max(s, 0.01f)) + (1f - ApparelRateMath.ShooterShare) * Math.Log(Math.Max(m, 0.01f)));
            }
        }

        /// <summary>The degrees a piece adds to its wearer's cold comfort (its cold insulation, less any rise of their least comfortable temperature).</summary>
        public static float Warmth(ApparelFacts a, ApparelRating r) => r.Cold - ApparelRateMath.Offset(a, "ComfyTemperatureMin");

        /// <summary>The degrees a piece adds to its wearer's heat comfort.</summary>
        public static float Cooling(ApparelFacts a, ApparelRating r) => r.HeatInsulation + ApparelRateMath.Offset(a, "ComfyTemperatureMax");

        /// <summary>
        /// Insulation's worth where they live: the degrees the season asks of this piece (how much colder it gets than they
        /// can bear without it) at the weight the review gives a cold climate (the role's own weight and one more, as its "in
        /// the cold"), and the degrees beyond at a fifth of the role's weight. A piece that takes warmth away counts it in full.
        /// </summary>
        public static float ClimateWorth(float degrees, float need, float roleWeight)
        {
            if (degrees <= 0f) return (roleWeight + (need > 0f ? 1f : 0f)) * degrees * ApparelRateMath.WorthPerDegree;
            float asked = Math.Min(degrees, need), spare = Math.Max(degrees - need, 0f);
            return ((roleWeight + 1f) * asked + roleWeight * SpareShare * spare) * ApparelRateMath.WorthPerDegree;
        }

        /// <summary>The worth of the areas by the role's weights, with warmth and cooling counted where they live instead.</summary>
        static float WorthIn(Dictionary<string, float> areas, string role, float warmthWorth, float coolingWorth)
        {
            var copy = new Dictionary<string, float>(areas) { ["cold"] = 0f, ["heat"] = 0f };
            return ApparelRateMath.WorthFor(copy, role) + warmthWorth + coolingWorth;
        }

        /// <summary>
        /// A piece's rating for a colonist and why it differs from the usual one, step by step so the steps add up: how they
        /// fight, their body, where they live (only with a climate), and the moods it would add for them (their other clothes
        /// already tattered, so this one's wear adds nothing; made for the other gender). <paramref name="asRole"/> judges it
        /// as another role than its own (the suggestions judge a piece in place of a parka as a coat): the same steps on that
        /// role's weights and scale.
        /// </summary>
        public static ApparelForColonist For(ApparelReport rep, ApparelWearer w, string asRole = null)
        {
            var a = rep.Facts;
            var r = rep.Rating;
            var c = new ApparelForColonist { Reference = r.Overall, Kind = w.Kind ?? "both", HasClimate = w.HasClimate };
            string role = asRole ?? r.Role;
            var (mid, width) = ApparelRateMath.RoleScale(role);
            float Scale(float worth) => ApparelRateMath.Scale(worth, mid, width);

            float prev = r.Overall;
            void Step(string why, float now)
            {
                c.Why.Add((why, now - prev));
                prev = now;
            }

            // How they fight.
            float fight = FightAs(a, c.Kind, null);
            var areas = ApparelRateMath.AreaWorth(a, fight);
            Step("kind:" + c.Kind, Scale(ApparelRateMath.WorthFor(areas, role)));

            // Their body.
            var body = BodyWithout(w.MissingGroups);
            if (body != null)
            {
                fight = FightAs(a, c.Kind, body);
                areas = ApparelRateMath.AreaWorth(a, fight);
            }
            Step("body", Scale(ApparelRateMath.WorthFor(areas, role)));

            // Where they live.
            c.Warmth = Warmth(a, r);
            c.Cooling = Cooling(a, r);
            c.Coldest = Math.Min(w.SeasonLow, w.OutdoorNow);
            c.Hottest = Math.Max(w.SeasonHigh, w.OutdoorNow);
            c.NeedWarmth = Math.Max(0f, w.ComfyMinWithout - c.Coldest);
            c.NeedCooling = Math.Max(0f, c.Hottest - w.ComfyMaxWithout);
            c.ComfyMinWith = w.ComfyMinWithout - c.Warmth;
            c.ComfyMaxWith = w.ComfyMaxWithout + c.Cooling;
            var weights = ApparelRateMath.RoleWeights(role);
            float warmthWorth = ClimateWorth(c.Warmth, c.NeedWarmth, weights["cold"]);
            float coolingWorth = ClimateWorth(c.Cooling, c.NeedCooling, weights["heat"]);
            Func<Dictionary<string, float>, float> worth = x => w.HasClimate ? WorthIn(x, role, warmthWorth, coolingWorth) : ApparelRateMath.WorthFor(x, role);
            Step("climate", Scale(worth(areas)));

            // Moods: the apparel-damaged mood is one, at the worst stage of what they wear; the other gender's.
            int stage = ApparelRead.DamageStage(rep.Def);
            float Mood(int s) => s == 1 ? w.Tattered.mood : s == 0 ? w.Frayed.mood : 0f;
            float counted = 0f;
            foreach (var (label, mood) in a.Moods)
                if ((stage == 1 && label == rep.Def.Tattered.label) || (stage == 0 && label == rep.Def.Frayed.label)) counted += mood;
            float marginal = Mood(Math.Max(stage, w.DamageStageKept)) - Mood(w.DamageStageKept);
            if (stage >= 0 && w.DamageStageKept >= 0)
            {
                areas = new Dictionary<string, float>(areas);
                areas["mind"] += (marginal - counted) * ApparelRateMath.WorthPerMood;
                Step("worn-already", Scale(worth(areas)));
            }
            if (w.WrongGender && !w.WrongGenderAlready && w.WrongGenderMood.mood != 0f)
            {
                areas = new Dictionary<string, float>(areas);
                areas["mind"] += w.WrongGenderMood.mood * ApparelRateMath.WorthPerMood;
                Step("gender", Scale(worth(areas)));
            }
            c.Rating = prev;
            return c;
        }

        /// <summary>The steps worth saying: a twentieth of a point or more.</summary>
        public static List<(string why, float delta)> Telling(ApparelForColonist c) => c.Why.Where(s => Math.Abs(s.delta) >= 0.05f).ToList();

        // ---------------------------------------------------------------- protection by region

        /// <summary>The body's regions, head to feet, as the zones of <see cref="ApparelRateMath.Human"/> by their index.</summary>
        public static readonly (string key, int[] zones)[] Regions =
        {
            ("head", new[] { 5, 8, 9, 10, 11, 12 }), ("neck", new[] { 7 }), ("shoulders", new[] { 3 }), ("torso", new[] { 0 }),
            ("arms", new[] { 2 }), ("hands", new[] { 6, 13, 14, 15, 16 }), ("legs", new[] { 1 }), ("feet", new[] { 4 }),
        };

        /// <summary>
        /// Each region's share of the shots that land on a body (a head hit counting double, as the rating counts it) and the
        /// share of them the groups cover.
        /// </summary>
        public static List<ApparelRegion> Coverage(IList<string> groups)
        {
            var body = ApparelRateMath.Human;
            float all = 0f;
            foreach (var z in body) all += z.Ranged * (z.Head ? ApparelRateMath.HeadHarm : 1f);
            var list = new List<ApparelRegion>();
            foreach (var (key, zones) in Regions)
            {
                float shots = 0f, covered = 0f;
                foreach (int i in zones)
                {
                    var z = body[i];
                    float s = z.Ranged * (z.Head ? ApparelRateMath.HeadHarm : 1f);
                    shots += s;
                    if (groups != null && z.CoveredBy(groups)) covered += s;
                }
                list.Add(new ApparelRegion { Key = key, Shots = all > 0f ? shots / all : 0f, Covered = shots > 0f ? covered / shots : 0f });
            }
            return list;
        }

        // ---------------------------------------------------------------- words and order

        /// <summary>The sub-ratings in the order they are shown: the fight's four, warmth and cooling, then what else it does.</summary>
        public static readonly string[] SubOrder =
        {
            "guns", "melee", "mechanoids", "fire", "cold", "heat", "toxins", "vacuum", "psychic", "psychic shelter", "work", "social",
            "mechanitor", "magic", "hunting", "carrying",
        };

        public static IEnumerable<string> SubKeys(ApparelRating r) => SubOrder.Where(k => r.Sub.ContainsKey(k));

        /// <summary>A sub-rating's or a situation's key as a word for a translation key: "psychic shelter" is "Psychicshelter".</summary>
        public static string KeyWord(string key)
        {
            string s = (key ?? "").Replace(" ", "").Replace("-", "");
            return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        /// <summary>The situations a piece is best for, at the rating's own marks (ApparelRateMath's best for): the fight's and warmth's at 7, the rest at 6, a wearer's at 7.5.</summary>
        static readonly Dictionary<string, float> BestAt = new Dictionary<string, float>
        {
            ["guns"] = 7f, ["melee"] = 7f, ["mechanoids"] = 7f, ["fire"] = 7f, ["cold"] = 7f, ["heat"] = 7f, ["toxins"] = 6f, ["vacuum"] = 6f, ["psychic"] = 6f,
            ["psychic shelter"] = 6f, ["work"] = 6f, ["social"] = 6f, ["mechanitor"] = 6f, ["magic"] = 6f, ["hunting"] = 6f,
            ["for a melee fighter"] = 7.5f, ["for a shooter"] = 7.5f,
        };

        public static bool Best(ApparelRating r, string key) => BestAt.TryGetValue(key, out var at) && r.Situations.TryGetValue(key, out var v) && v >= at;

        /// <summary>The situations it is weak in, as the rating marks them: armour's fight under 3, weather gear in the other weather, the wearer it isn't for.</summary>
        public static bool Weak(ApparelRating r, string key)
        {
            if (!r.Situations.TryGetValue(key, out var v)) return false;
            bool combat = r.Role == "body armour" || r.Role == "helmet" || r.Role == "off-hand shield";
            if (key == "guns" || key == "mechanoids" || key == "melee") return combat && v <= 3f;
            if (key == "in the cold" || key == "in the heat" || key == "for a shooter" || key == "for a melee fighter") return r.WeakWhen.Contains(key);
            return false;
        }

        /// <summary>A temperature with one decimal at most and its sign, as "-12" or "8.5".</summary>
        public static string Degrees(float c) => Math.Round(c, 1).ToString("0.#", CultureInfo.InvariantCulture);

        /// <summary>How a colonist fights, from what they hold and their skills: a gun in hand a shooter, a blade a melee fighter; else their skills.</summary>
        public static string KindOf(bool canFight, bool canShoot, string heldKind, bool brawler, int shooting, int melee)
        {
            if (!canFight) return "none";
            if (!canShoot) return "melee";
            if (heldKind == "ranged") return "shooter";
            if (heldKind == "melee") return "melee";
            if (brawler) return "melee";
            if (melee >= shooting + 4) return "melee";
            if (shooting >= melee + 4) return "shooter";
            return "both";
        }
    }
}
