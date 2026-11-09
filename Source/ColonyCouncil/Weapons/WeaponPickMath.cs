using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>A colonist as the choice of weapons sees them: what they can do, love and hold, and what their work is.</summary>
    public sealed class PickColonist
    {
        public string Key = "", Name = "";
        /// <summary>Able to fight at all (not incapable of violence), and to shoot (not incapable of shooting).</summary>
        public bool CanFight = true, CanShoot = true;
        public int Shooting = 8, Melee = 8;
        /// <summary>Their passion for Shooting and for Melee: 0 none, 1 minor, 2 burning.</summary>
        public int ShootingPassion, MeleePassion;
        /// <summary>A brawler: a gun in their hands costs them mood, so they are never handed one.</summary>
        public bool Brawler;
        /// <summary>Hunting is among their work: they can only hunt with a gun or bow that harms and doesn't explode.</summary>
        public bool Hunter;
        /// <summary>Their work is healing or making things ("doctor", "crafter") rather than fighting: a light weapon suits them. Null for neither.</summary>
        public string Worker;
        /// <summary>The weapon in their hands (a weapon's key), or null.</summary>
        public string Holds;
        /// <summary>The persona weapon they are bonded to (held or not), or null; whether its persona is jealous of other weapons.</summary>
        public string Bonded;
        public bool BondJealous;
        /// <summary>The weapons in their inventory (Simple Sidearms' sidearms).</summary>
        public List<string> Carries = new List<string>();
        /// <summary>The weapon in their off hand (Dual Wield), or null.</summary>
        public string OffHand;
        /// <summary>The player set their suggestions aside for now: they take no weapon in the colony's choice.</summary>
        public bool NotNow;

        public bool HoldsBonded => Holds != null && Holds == Bonded;
    }

    /// <summary>A weapon the colony has, as the choice sees it: its reference rating, where it is and what the mods allow.</summary>
    public sealed class PickWeapon
    {
        public string Key = "", Label = "";
        /// <summary>Weapons of one kind, stuff and quality without traits of their own share a group, rated once.</summary>
        public string Group = "";
        /// <summary>Its rating for the reference colonist: its situations, tags, effects and how much skill it rewards.</summary>
        public WeaponRating Rating;
        public float Mass;
        public bool OneUse;
        /// <summary>A hunter may hunt with it (the game's rule: a gun or bow that harms and doesn't explode).</summary>
        public bool HuntingOk;
        /// <summary>Dual Wield: it may go in the off hand; it takes both hands.</summary>
        public bool CanOffHand, TwoHanded;
        /// <summary>Held by a colonist, carried in a colonist's inventory (their keys), or lying in storage, free to take.</summary>
        public string Holder, CarriedBy;
        public bool Stored;
        /// <summary>Only to be taken in hand (a weapon on an outfit stand: the sidearm's and the off hand's jobs take it from the ground).</summary>
        public bool HandOnly;

        public bool Ranged => Rating != null && Rating.Kind == "ranged";
        public float Overall => Rating?.Overall ?? 1f;

        public float Situation(string key) => Rating != null && Rating.Situations.TryGetValue(key, out var v) ? v : Overall;

        public bool HasSpecial(string key) => Rating != null && Rating.Specials.Any(s => s.Key == key);

        public bool HasTag(string tag) => Rating != null && Rating.Tags.Contains(tag);
    }

    /// <summary>One colonist with one weapon: their own rating of it, why they can't use it, how their faith sees it, Dual Wield's pair.</summary>
    public sealed class PickOption
    {
        /// <summary>Their own rating (WeaponForMath: their skill, traits and body).</summary>
        public float Rating;
        /// <summary>Why they can't or mustn't carry it (incapable, coded to another, too heavy, despised by their faith), or null.</summary>
        public string Cant;
        /// <summary>Their faith holds it noble or preferred: carrying it lifts their mood.</summary>
        public bool Noble;
        /// <summary>Dual Wield: both hands together, what they hold in the main hand and this in the off hand, when it can go there.</summary>
        public float? Pair;
    }

    /// <summary>Which weapon mods are there, and Simple Sidearms' weights.</summary>
    public sealed class PickRules
    {
        public bool Sidearms, DualWield;
        public float SidearmRangedMass = WeaponTagMath.SidearmRangedMass, SidearmMeleeMass = WeaponTagMath.SidearmMeleeMass;
    }

    /// <summary>Everything the choice reads: the colonists, the weapons, each pair's option, the colony's fights, the mods.</summary>
    public sealed class PickInput
    {
        public List<PickColonist> Colonists = new List<PickColonist>();
        public List<PickWeapon> Weapons = new List<PickWeapon>();
        public Dictionary<(string colonist, string weapon), PickOption> Options = new Dictionary<(string, string), PickOption>();
        /// <summary>The situations the colony meets, each with its weight in the mix of fights (WeaponPickMath.ColonySituations).</summary>
        public Dictionary<string, float> Colony = new Dictionary<string, float>();
        public PickRules Rules = new PickRules();

        public PickOption Option(PickColonist c, PickWeapon w) => c != null && w != null && Options.TryGetValue((c.Key, w.Key), out var o) ? o : null;

        public PickWeapon Weapon(string key) => key == null ? null : Weapons.FirstOrDefault(w => w.Key == key);

        public PickColonist Colonist(string key) => key == null ? null : Colonists.FirstOrDefault(c => c.Key == key);
    }

    /// <summary>What a suggestion asks: a weapon from storage in hand, two colonists trading, a sidearm to carry, a weapon in the off hand.</summary>
    public enum PickKind
    {
        Primary,
        Swap,
        Sidearm,
        OffHand
    }

    /// <summary>Why a weapon suits a colonist better, or what taking it costs, as the line's parts.</summary>
    public enum PickWhy
    {
        /// <summary>Their high skill makes the most of a weapon that rewards it.</summary>
        SkillHigh,
        /// <summary>A forgiving weapon for a low skill, where what they hold asks for a good one.</summary>
        SkillLow,
        /// <summary>A trait of theirs that works on this kind of fight adds to it (Text: the trait, Value: how much).</summary>
        Trait,
        /// <summary>The fights the colony meets (Text: the situation's key).</summary>
        Situation,
        /// <summary>Its EMP against the mechanoids the colony meets.</summary>
        MechsEmp,
        /// <summary>It pierces the armour the colony's raiders wore.</summary>
        ArmourPierce,
        Hunting,
        Passion,
        Noble,
        /// <summary>A doctor or crafter: light enough not to slow their work.</summary>
        Worker,
        /// <summary>An effect the weapon they hold lacks (Text: its words).</summary>
        Special,
        /// <summary>Simply better for them (Value: its rating for them, Level: what they hold, in tenths).</summary>
        Better,
        /// <summary>They carry nothing now.</summary>
        Unarmed,
        /// <summary>Simple Sidearms: light enough to carry beside what they hold.</summary>
        Sidearm,
        /// <summary>Dual Wield: both hands together (Value: the pair's rating).</summary>
        OffHand,
        /// <summary>A trade with another colonist, better for both (Text: the other's name, Value: their gain).</summary>
        Swap,
        /// <summary>A cost: their bonded persona weapon would go unused.</summary>
        Bonded,
        /// <summary>A cost: its persona is jealous of other weapons.</summary>
        Jealous,
        /// <summary>A cost: a hunter who couldn't hunt with it.</summary>
        NoHunting
    }

    /// <summary>One part of a suggestion's line: why, or what it costs.</summary>
    public sealed class PickReason
    {
        public PickWhy Kind;
        public string Text = "";
        public float Value;
        public int Level;
        /// <summary>A cost of taking it rather than a reason for it.</summary>
        public bool Cost;
    }

    /// <summary>One suggestion: who, which weapon, what for, how much better for them (and for the other, in a trade), and why.</summary>
    public sealed class PickSuggestion
    {
        public PickKind Kind;
        public string Colonist, Weapon;
        /// <summary>What they give up: the weapon they hold (a trade's other weapon), or null.</summary>
        public string Instead;
        /// <summary>A trade's other colonist.</summary>
        public string Other;
        /// <summary>How much better for them on the rating's scale, the colony's fights and their own in it; a trade's other's.</summary>
        public float Gain, OtherGain;
        /// <summary>Its rating for them and what they have now, as the badges show them.</summary>
        public float Rating, Before;
        public List<PickReason> Why = new List<PickReason>();

        public IEnumerable<PickReason> Reasons => Why.Where(r => !r.Cost);
        public IEnumerable<PickReason> Costs => Why.Where(r => r.Cost);
    }

    /// <summary>A raid the colony met (Rimfolk's own record, or one read from the game's letters): how many, what kind, how armoured.</summary>
    public struct RaidSeen
    {
        public int Tick;
        /// <summary>How many came (0: not known, read from a letter).</summary>
        public int Count;
        public int Mechs, Armoured;
        public bool Siege, Tribal;
        /// <summary>Read from the game's letters: only its people are known, so its share of armour is the people's age's.</summary>
        public float ArmouredShare;
        public bool FromLetter;
    }

    /// <summary>
    /// The best weapon for each colonist (docs/design/weapons.md, step 3), without game types (Tools/Tests try it): each
    /// colonist with each weapon the colony has is scored by their own rating (their skill, traits and body), their passion,
    /// the fights the colony meets (mechanoids, crowds, armoured raiders, sieges, a gate it holds) and their work (a hunter's
    /// hunts, a doctor's or crafter's light weapon), their faith (a noble weapon in favour; a despised one never), and what
    /// it costs (a bonded persona weapon left unused, a hunter who couldn't hunt). One assignment for the whole colony, so
    /// two colonists are never told to take the same weapon: weapons from storage first, greedily by the biggest gain, then
    /// exchanged in pairs until no exchange raises the colony's total; then trades between colonists that are better for
    /// both; then Simple Sidearms' sidearms and Dual Wield's off hands from what is left. Only gains worth the walk are
    /// suggested (<see cref="Threshold"/>), and each comes with its reasons, strongest first.
    /// </summary>
    public static class WeaponPickMath
    {
        // ---------------------------------------------------------------- the thresholds

        /// <summary>
        /// The least gain suggested, on the rating's scale: 0.6 of a point. Near the middle a point is about a third more
        /// damage for a gun and a fifth more for a melee weapon, so 0.6 is about a fifth more for a gun and a ninth more for a
        /// blade: a difference felt in a fight, and three times the rating's rounding and the colony's mix of fights moving it.
        /// A revolver to a bolt-action rifle (0.4) or a longsword to a uranium one (0.3) isn't suggested; a bolt-action to an
        /// assault rifle (0.9), an assault rifle to a charge rifle (0.9), a knife to a longsword (1.2) are.
        /// </summary>
        public const float Threshold = 0.6f;

        /// <summary>The least gain a colonist asks for a weapon lying in storage: twice the gear tab's, a clearly better one.</summary>
        public const float AskThreshold = 1.2f;

        /// <summary>A trade between two colonists: each gains at least this, and the two together <see cref="SwapTotal"/>.</summary>
        public const float SwapEach = 0.3f, SwapTotal = 0.8f;

        /// <summary>More gain asked of a weapon that would leave a colonist's bonded persona weapon unused.</summary>
        public const float BondedExtra = 1f;

        /// <summary>More gain asked of a weapon of the other kind than the one they hold (a shooter turned to the blade), without Simple Sidearms.</summary>
        public const float KindChange = 0.5f;

        /// <summary>What empty hands are worth on the rating's scale; and a sidearm slot with nothing in it.</summary>
        public const float Unarmed = 1f, SidearmFloor = 3f;

        public const float PassionMinor = 0.15f, PassionMajor = 0.3f;

        /// <summary>A weapon their faith holds noble: its mood is worth this much.</summary>
        public const float NobleBonus = 0.4f;

        /// <summary>A hunter given a weapon they can't hunt with.</summary>
        public const float HunterLoss = 0.8f;

        /// <summary>The colony's fights weigh at most half the mix; a hunter's hunts and a worker's light weapon a quarter each; never more than 0.6 in all.</summary>
        public const float ColonyShare = 0.5f, HunterShare = 0.25f, WorkerShare = 0.25f, MixMax = 0.6f;

        /// <summary>How far the situations can move a weapon's score, either way.</summary>
        public const float SituationCap = 2f;

        // ---------------------------------------------------------------- the colony's fights

        /// <summary>A raid's weight by how long ago it was: in full for 30 days, half to 60, a quarter to 120, then none.</summary>
        public static float Recency(int now, int tick)
        {
            float days = (now - tick) / 60000f;
            return days < 0f ? 1f : days <= 30f ? 1f : days <= 60f ? 0.5f : days <= 120f ? 0.25f : 0f;
        }

        /// <summary>A raid of this many or more is a crowd (a tribe's raid is one whatever its size).</summary>
        public const int CrowdSize = 10;

        /// <summary>
        /// The situations the colony meets, each with its weight: the shares of the raids met lately (by recency) that were
        /// mechanoids, armoured raiders (flak or better on the body), crowds (ten or more, or a tribe's) and sieges, each
        /// times its part (mechanoids 0.5, armour 0.4, crowds 0.3, sieges 0.25); and a gate the colony holds (a defense line
        /// drawn) at 0.15. The mix (<see cref="Mix"/>) scales them to at most half of a colonist's fights.
        /// </summary>
        public static Dictionary<string, float> ColonySituations(IEnumerable<RaidSeen> raids, int now, bool holdsGate)
        {
            var d = new Dictionary<string, float>();
            float total = 0f, mechs = 0f, armour = 0f, crowds = 0f, sieges = 0f;
            foreach (var r in raids ?? Enumerable.Empty<RaidSeen>())
            {
                float w = Recency(now, r.Tick);
                if (w <= 0f) continue;
                total += w;
                if (r.Count > 0)
                {
                    mechs += w * Math.Min(1f, r.Mechs / (float)r.Count);
                    armour += w * Math.Min(1f, r.Armoured / (float)r.Count);
                    if (r.Count >= CrowdSize || r.Tribal) crowds += w;
                }
                else
                {
                    if (r.Mechs > 0) mechs += w;
                    armour += w * r.ArmouredShare;
                    if (r.Tribal) crowds += w;
                }
                if (r.Siege) sieges += w;
            }
            if (total > 0f)
            {
                Put(d, "mechanoids", mechs / total * 0.5f);
                Put(d, "heavily armoured", armour / total * 0.4f);
                Put(d, "crowds", crowds / total * 0.3f);
                Put(d, "siege or long duel", sieges / total * 0.25f);
            }
            if (holdsGate) Put(d, "holding a choke", 0.15f);
            return d;
        }

        private static void Put(Dictionary<string, float> d, string key, float v)
        {
            if (v < 0.01f) return;
            d[key] = d.TryGetValue(key, out var old) ? old + v : v;
        }

        /// <summary>
        /// A colonist's mix of fights for a weapon: the colony's situations (that the weapon has a rating for), at most
        /// <see cref="ColonyShare"/> together; a hunter's hunts with a weapon they can hunt with; a doctor's or crafter's
        /// light weapon (its "sidearm" rating); the whole at most <see cref="MixMax"/>, the rest their ordinary fights.
        /// </summary>
        public static Dictionary<string, float> Mix(PickColonist c, PickWeapon w, Dictionary<string, float> colony)
        {
            var mix = new Dictionary<string, float>();
            if (w?.Rating == null) return mix;
            float total = 0f;
            if (colony != null)
                foreach (var kv in colony)
                {
                    if (kv.Value <= 0f || !w.Rating.Situations.ContainsKey(kv.Key)) continue;
                    mix[kv.Key] = kv.Value;
                    total += kv.Value;
                }
            if (total > ColonyShare)
                foreach (var k in mix.Keys.ToList())
                    mix[k] *= ColonyShare / total;
            if (c != null && c.Hunter && w.Ranged && w.HuntingOk && w.Rating.Situations.ContainsKey("hunting")) Put(mix, "hunting", HunterShare);
            if (c?.Worker != null && w.Rating.Situations.ContainsKey("sidearm")) Put(mix, "sidearm", WorkerShare);
            total = mix.Values.Sum();
            if (total > MixMax)
                foreach (var k in mix.Keys.ToList())
                    mix[k] *= MixMax / total;
            return mix;
        }

        /// <summary>How much the mix moves a weapon from its own overall: each situation's rating over its overall, by its weight.</summary>
        public static float Situational(PickWeapon w, Dictionary<string, float> mix)
        {
            if (w?.Rating == null || mix == null) return 0f;
            float adj = 0f;
            foreach (var kv in mix) adj += kv.Value * (w.Situation(kv.Key) - w.Overall);
            return WeaponRateMath.Clamp(adj, -SituationCap, SituationCap);
        }

        // ---------------------------------------------------------------- one colonist, one weapon

        /// <summary>
        /// A weapon's score for a colonist: their own rating, their passion for its kind, the situations they meet in its
        /// favour or against it, a hunter's loss if they couldn't hunt with it, their faith's favour. Null when they can't or
        /// mustn't carry it (a gun for a brawler or someone who can't shoot, coded to another, despised, too heavy).
        /// </summary>
        public static float? Score(PickInput input, PickColonist c, PickWeapon w)
        {
            if (c == null || w?.Rating == null || !c.CanFight) return null;
            var o = input.Option(c, w);
            if (o == null || o.Cant != null) return null;
            if (w.Ranged && (c.Brawler || !c.CanShoot)) return null;
            float s = o.Rating;
            int passion = w.Ranged ? c.ShootingPassion : c.MeleePassion;
            if (passion >= 2) s += PassionMajor;
            else if (passion == 1) s += PassionMinor;
            s += Situational(w, Mix(c, w, input.Colony));
            if (c.Hunter && !(w.Ranged && w.HuntingOk)) s -= HunterLoss;
            if (o.Noble) s += NobleBonus;
            return s;
        }

        /// <summary>What a colonist has now in their hand: the score of what they hold, or empty hands.</summary>
        public static float Current(PickInput input, PickColonist c)
        {
            var held = input.Weapon(c.Holds);
            return held != null ? Score(input, c, held) ?? Unarmed : Unarmed;
        }

        /// <summary>
        /// The best of a kind a colonist has now: what they hold of that kind and, with Simple Sidearms, what they carry of it.
        /// Null when they have nothing of that kind.
        /// </summary>
        public static float? BestOfKind(PickInput input, PickColonist c, bool ranged)
        {
            float? best = null;
            var held = input.Weapon(c.Holds);
            if (held != null && held.Ranged == ranged) best = Score(input, c, held);
            if (input.Rules.Sidearms)
                foreach (var k in c.Carries)
                {
                    var w = input.Weapon(k);
                    if (w == null || w.Ranged != ranged) continue;
                    var s = Score(input, c, w);
                    if (s.HasValue && (!best.HasValue || s.Value > best.Value)) best = s;
                }
            return best;
        }

        /// <summary>
        /// A weapon in hand for a colonist: its gain over what they have now and the gain it must reach. The same kind as what
        /// they hold is set against the best of that kind they have (with Simple Sidearms their sidearms too); the other kind
        /// asks <see cref="KindChange"/> more (with Simple Sidearms it is a sidearm instead, not a weapon in hand); leaving a
        /// bonded persona weapon unused asks <see cref="BondedExtra"/> more. Null when it isn't a choice for them.
        /// </summary>
        public static (float gain, float need, float score, float before)? PrimaryGain(PickInput input, PickColonist c, PickWeapon w)
        {
            if (c.Holds == w.Key) return null;
            var s = Score(input, c, w);
            if (!s.HasValue) return null;
            var held = input.Weapon(c.Holds);
            // A weapon they mustn't carry (a gun in a brawler's hands, one their faith despises) counts as empty hands.
            if (held != null && !Score(input, c, held).HasValue) held = null;
            float need = Threshold, before;
            if (held == null)
            {
                before = Unarmed;
                if (input.Rules.Sidearms)
                    foreach (var k in c.Carries)
                    {
                        var cw = input.Weapon(k);
                        var cs = cw != null ? Score(input, c, cw) : null;
                        if (cs.HasValue && cs.Value > before) before = cs.Value;
                    }
            }
            else if (held.Ranged == w.Ranged) before = BestOfKind(input, c, w.Ranged) ?? Current(input, c);
            else
            {
                if (input.Rules.Sidearms) return null;
                before = Current(input, c);
                need += KindChange;
            }
            if (c.HoldsBonded && held != null) need += BondedExtra;
            return (s.Value - before, need, s.Value, before);
        }

        // ---------------------------------------------------------------- the colony's assignment

        /// <summary>
        /// One assignment for the whole colony (see the class): every suggestion, at most one weapon in hand (from storage, or
        /// a trade), one sidearm and one off-hand weapon per colonist, no weapon given to two. Colonists who can't fight or
        /// whose suggestions were set aside take none.
        /// </summary>
        public static List<PickSuggestion> Assign(PickInput input)
        {
            var result = new List<PickSuggestion>();
            if (input == null) return result;
            var fighters = input.Colonists.Where(c => c.CanFight && !c.NotNow).OrderBy(c => c.Key, StringComparer.Ordinal).ToList();
            var free = input.Weapons.Where(w => w.Stored && w.Rating != null).OrderBy(w => w.Key, StringComparer.Ordinal).ToList();
            var taken = new HashSet<string>();
            var primary = Primaries(input, fighters, free, taken);
            result.AddRange(primary.Values);
            var busy = new HashSet<string>(primary.Keys);
            result.AddRange(Swaps(input, fighters.Where(c => !busy.Contains(c.Key)).ToList(), busy));
            // What each colonist will hold, for the sidearm's kind and the off hand's pair.
            var holds = fighters.ToDictionary(c => c.Key, c => primary.TryGetValue(c.Key, out var p) ? input.Weapon(p.Weapon) : input.Weapon(c.Holds));
            foreach (var s in result.Where(s => s.Kind == PickKind.Swap)) holds[s.Colonist] = input.Weapon(s.Weapon);
            if (input.Rules.Sidearms) result.AddRange(Sidearms(input, fighters, free, taken, holds));
            if (input.Rules.DualWield) result.AddRange(OffHands(input, fighters.Where(c => !busy.Contains(c.Key)).ToList(), free, taken));
            foreach (var s in result) s.Why = Reasons(input, s);
            return result;
        }

        /// <summary>A weapon's group: alike weapons (one kind, stuff, quality, coding) are one choice; one without a group is its own.</summary>
        private static string GroupOf(PickWeapon w) => string.IsNullOrEmpty(w.Group) ? "#" + w.Key : w.Group;

        /// <summary>The first weapon not yet taken of each group: alike weapons are weighed once.</summary>
        private static IEnumerable<PickWeapon> Heads(List<PickWeapon> free, HashSet<string> taken)
        {
            var seen = new HashSet<string>();
            foreach (var w in free)
                if (!taken.Contains(w.Key) && seen.Add(GroupOf(w)))
                    yield return w;
        }

        /// <summary>The weapon itself if not taken, else another of its group not yet taken, or null.</summary>
        private static PickWeapon Untaken(PickWeapon w, List<PickWeapon> free, HashSet<string> taken)
        {
            if (!taken.Contains(w.Key)) return w;
            string g = GroupOf(w);
            return free.FirstOrDefault(x => !taken.Contains(x.Key) && GroupOf(x) == g);
        }

        /// <summary>
        /// Weapons in hand from storage (and, without Simple Sidearms, one a colonist carries in their own inventory): greedily
        /// the biggest gain left that is worth it, one weapon per colonist; then pairs of
        /// colonists exchange what they were given, and a colonist takes a better weapon left over, while the colony's total
        /// rises and both still gain enough; then again for anyone a freed weapon now suits.
        /// </summary>
        private static Dictionary<string, PickSuggestion> Primaries(PickInput input, List<PickColonist> fighters, List<PickWeapon> free, HashSet<string> taken)
        {
            var given = new Dictionary<string, PickWeapon>();
            var gainOf = new Dictionary<(string, string), (float gain, float need, float score, float before)?>();
            (float gain, float need, float score, float before)? G(PickColonist c, PickWeapon w)
            {
                var key = (c.Key, w.Key);
                if (!gainOf.TryGetValue(key, out var g)) gainOf[key] = g = PrimaryGain(input, c, w);
                return g;
            }
            bool Ok(PickColonist c, PickWeapon w) => G(c, w) is var g && g.HasValue && g.Value.gain >= g.Value.need - 1e-4f;
            // What a colonist may take in hand: from storage, and (without Simple Sidearms, which switches its own) a weapon
            // they carry in their inventory.
            IEnumerable<PickWeapon> Choices(PickColonist c)
            {
                foreach (var w in Heads(free, taken)) yield return w;
                if (input.Rules.Sidearms) yield break;
                foreach (var k in c.Carries)
                    if (input.Weapon(k) is PickWeapon own && own.Rating != null && !taken.Contains(own.Key))
                        yield return own;
            }
            for (int round = 0; round < 3; round++)
            {
                bool changed = false;
                // Greedy: the biggest worthwhile gain left.
                while (true)
                {
                    PickColonist bc = null;
                    PickWeapon bw = null;
                    float best = float.MinValue;
                    foreach (var c in fighters)
                    {
                        if (given.ContainsKey(c.Key)) continue;
                        foreach (var w in Choices(c))
                        {
                            if (taken.Contains(w.Key) || !Ok(c, w)) continue;
                            float gain = G(c, w).Value.gain;
                            if (gain > best + 1e-4f) (best, bc, bw) = (gain, c, w);
                        }
                    }
                    if (bc == null) break;
                    given[bc.Key] = bw;
                    taken.Add(bw.Key);
                    changed = true;
                }
                // Exchanges: two colonists swap what they were given, or one takes a better weapon left over.
                for (int pass = 0; pass < 40; pass++)
                {
                    bool improved = false;
                    var keys = given.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
                    for (int i = 0; i < keys.Count && !improved; i++)
                    for (int j = i + 1; j < keys.Count && !improved; j++)
                    {
                        var a = input.Colonist(keys[i]);
                        var b = input.Colonist(keys[j]);
                        PickWeapon wa = given[a.Key], wb = given[b.Key];
                        if (wa.CarriedBy != null || wb.CarriedBy != null || !Ok(a, wb) || !Ok(b, wa)) continue;
                        float now = G(a, wa).Value.score + G(b, wb).Value.score, then = G(a, wb).Value.score + G(b, wa).Value.score;
                        if (then <= now + 0.05f) continue;
                        given[a.Key] = wb;
                        given[b.Key] = wa;
                        improved = true;
                    }
                    foreach (var k in keys)
                    {
                        if (improved) break;
                        var c = input.Colonist(k);
                        var mine = given[k];
                        foreach (var w in Choices(c))
                        {
                            if (taken.Contains(w.Key) || !Ok(c, w)) continue;
                            if (G(c, w).Value.score <= G(c, mine).Value.score + 0.05f) continue;
                            taken.Remove(mine.Key);
                            taken.Add(w.Key);
                            given[k] = w;
                            improved = true;
                            break;
                        }
                    }
                    // A colonist left without one takes what another was given, who takes a weapon left over instead,
                    // when the two gain more together (the best such move first).
                    if (!improved)
                    {
                        PickColonist bu = null, ba = null;
                        PickWeapon bw = null;
                        float bestMove = 0.05f;
                        foreach (var u in fighters)
                        {
                            if (given.ContainsKey(u.Key)) continue;
                            foreach (var k in keys)
                            {
                                var a = input.Colonist(k);
                                var wa = given[k];
                                if (wa.CarriedBy != null || !Ok(u, wa)) continue;
                                foreach (var w in Heads(free, taken))
                                {
                                    if (taken.Contains(w.Key) || !Ok(a, w)) continue;
                                    float move = G(u, wa).Value.gain + G(a, w).Value.score - G(a, wa).Value.score;
                                    if (move > bestMove + 1e-4f) (bestMove, bu, ba, bw) = (move, u, a, w);
                                }
                            }
                        }
                        if (bu != null)
                        {
                            given[bu.Key] = given[ba.Key];
                            given[ba.Key] = bw;
                            taken.Add(bw.Key);
                            improved = true;
                        }
                    }
                    if (!improved) break;
                    changed = true;
                }
                if (!changed) break;
            }
            var result = new Dictionary<string, PickSuggestion>();
            foreach (var kv in given)
            {
                var c = input.Colonist(kv.Key);
                var g = G(c, kv.Value).Value;
                result[kv.Key] = new PickSuggestion
                {
                    Kind = PickKind.Primary, Colonist = c.Key, Weapon = kv.Value.Key, Instead = c.Holds, Gain = g.gain,
                    Rating = input.Option(c, kv.Value)?.Rating ?? g.score, Before = BeforeRating(input, c, kv.Value.Ranged)
                };
            }
            return result;
        }

        /// <summary>Their own rating of what they have now of a kind (the weapon in hand, or with Simple Sidearms the best of that kind), as the badge shows it.</summary>
        private static float BeforeRating(PickInput input, PickColonist c, bool ranged)
        {
            float best = 0f;
            var held = input.Weapon(c.Holds);
            if (held != null && (held.Ranged == ranged || !input.Rules.Sidearms)) best = input.Option(c, held)?.Rating ?? held.Overall;
            if (input.Rules.Sidearms)
                foreach (var k in c.Carries)
                {
                    var w = input.Weapon(k);
                    if (w == null || w.Ranged != ranged) continue;
                    best = Math.Max(best, input.Option(c, w)?.Rating ?? w.Overall);
                }
            return best;
        }

        /// <summary>
        /// Trades between two colonists who weren't given a weapon from storage: each gives the other what they hold, when it
        /// is better for both (each gains <see cref="SwapEach"/>, the other kind <see cref="KindChange"/> more, the two together
        /// <see cref="SwapTotal"/>). A bonded persona weapon is never traded. The biggest combined gain first, one trade each.
        /// </summary>
        private static List<PickSuggestion> Swaps(PickInput input, List<PickColonist> open, HashSet<string> busy)
        {
            var pairs = new List<(PickColonist a, PickColonist b, float ga, float gb)>();
            var holders = open.Where(c => c.Holds != null && !c.HoldsBonded && input.Weapon(c.Holds)?.Rating != null).ToList();
            for (int i = 0; i < holders.Count; i++)
            for (int j = i + 1; j < holders.Count; j++)
            {
                PickColonist a = holders[i], b = holders[j];
                PickWeapon wa = input.Weapon(a.Holds), wb = input.Weapon(b.Holds);
                if (wb.Key == a.Bonded || wa.Key == b.Bonded) continue;
                float? sa = Score(input, a, wb), sb = Score(input, b, wa);
                if (!sa.HasValue || !sb.HasValue) continue;
                float ga = sa.Value - Current(input, a), gb = sb.Value - Current(input, b);
                float needA = SwapEach + (wa.Ranged != wb.Ranged ? KindChange : 0f), needB = needA;
                if (ga < needA || gb < needB || ga + gb < SwapTotal) continue;
                pairs.Add((a, b, ga, gb));
            }
            var result = new List<PickSuggestion>();
            foreach (var (a, b, ga, gb) in pairs.OrderByDescending(p => p.ga + p.gb).ThenBy(p => p.a.Key, StringComparer.Ordinal).ThenBy(p => p.b.Key, StringComparer.Ordinal))
            {
                if (busy.Contains(a.Key) || busy.Contains(b.Key)) continue;
                busy.Add(a.Key);
                busy.Add(b.Key);
                PickWeapon wa = input.Weapon(a.Holds), wb = input.Weapon(b.Holds);
                result.Add(new PickSuggestion
                {
                    Kind = PickKind.Swap, Colonist = a.Key, Weapon = wb.Key, Instead = wa.Key, Other = b.Key, Gain = ga, OtherGain = gb,
                    Rating = input.Option(a, wb)?.Rating ?? wb.Overall, Before = input.Option(a, wa)?.Rating ?? wa.Overall
                });
                result.Add(new PickSuggestion
                {
                    Kind = PickKind.Swap, Colonist = b.Key, Weapon = wa.Key, Instead = wb.Key, Other = a.Key, Gain = gb, OtherGain = ga,
                    Rating = input.Option(b, wa)?.Rating ?? wa.Overall, Before = input.Option(b, wb)?.Rating ?? wb.Overall
                });
            }
            return result;
        }

        /// <summary>
        /// Simple Sidearms: a weapon of the other kind than the one they will hold, light enough for its default weights (2.7
        /// kg a gun, 1.9 kg a blade; never a one-use launcher), carried beside it, when it beats what they carry of that kind
        /// (or an empty slot, <see cref="SidearmFloor"/>) by <see cref="Threshold"/>. Never a gun for a brawler. The biggest gain first.
        /// </summary>
        private static List<PickSuggestion> Sidearms(PickInput input, List<PickColonist> fighters, List<PickWeapon> free, HashSet<string> taken,
            Dictionary<string, PickWeapon> holds)
        {
            var options = new List<(PickColonist c, PickWeapon w, float gain, float before)>();
            foreach (var c in fighters)
            {
                var main = holds.TryGetValue(c.Key, out var m) ? m : null;
                if (main == null) continue;
                bool ranged = !main.Ranged;
                if (ranged && (c.Brawler || !c.CanShoot)) continue;
                float before = SidearmFloor;
                foreach (var k in c.Carries)
                {
                    var cw = input.Weapon(k);
                    if (cw == null || cw.Ranged != ranged) continue;
                    var cs = Score(input, c, cw);
                    if (cs.HasValue) before = Math.Max(before, cs.Value);
                }
                foreach (var w in Heads(free, taken))
                {
                    if (taken.Contains(w.Key) || w.HandOnly || w.Ranged != ranged || (ranged && w.OneUse)) continue;
                    if (w.Mass > (ranged ? input.Rules.SidearmRangedMass : input.Rules.SidearmMeleeMass) + 0.001f) continue;
                    var s = Score(input, c, w);
                    if (!s.HasValue || s.Value - before < Threshold) continue;
                    options.Add((c, w, s.Value - before, before));
                }
            }
            var done = new HashSet<string>();
            var result = new List<PickSuggestion>();
            foreach (var (c, first, gain, before) in options.OrderByDescending(o => o.gain).ThenBy(o => o.c.Key, StringComparer.Ordinal).ThenBy(o => o.w.Key, StringComparer.Ordinal))
            {
                var w = done.Contains(c.Key) ? null : Untaken(first, free, taken);
                if (w == null) continue;
                done.Add(c.Key);
                taken.Add(w.Key);
                float was = 0f;
                foreach (var k in c.Carries)
                {
                    var cw = input.Weapon(k);
                    if (cw != null && cw.Ranged == w.Ranged) was = Math.Max(was, input.Option(c, cw)?.Rating ?? cw.Overall);
                }
                result.Add(new PickSuggestion
                {
                    Kind = PickKind.Sidearm, Colonist = c.Key, Weapon = w.Key, Instead = holds[c.Key]?.Key, Gain = gain,
                    Rating = input.Option(c, w)?.Rating ?? w.Overall, Before = was
                });
            }
            return result;
        }

        /// <summary>
        /// Dual Wield: a weapon for the off hand of a colonist who keeps what they hold (not two-handed) and has nothing there,
        /// of the same kind (guns with guns, blades with blades: both hands fire or swing together), when both hands
        /// together (with Dual Wield's penalties) beat what they hold alone by <see cref="Threshold"/>. The biggest gain first.
        /// </summary>
        private static List<PickSuggestion> OffHands(PickInput input, List<PickColonist> keeping, List<PickWeapon> free, HashSet<string> taken)
        {
            var options = new List<(PickColonist c, PickWeapon w, float gain, float pair, float alone)>();
            foreach (var c in keeping)
            {
                var main = input.Weapon(c.Holds);
                if (main == null || main.TwoHanded || c.OffHand != null) continue;
                var mo = input.Option(c, main);
                if (mo == null) continue;
                foreach (var w in Heads(free, taken))
                {
                    if (taken.Contains(w.Key) || w.HandOnly || !w.CanOffHand || w.TwoHanded || w.Ranged != main.Ranged) continue;
                    var o = input.Option(c, w);
                    if (o == null || o.Cant != null || !o.Pair.HasValue || Score(input, c, w) == null) continue;
                    float gain = o.Pair.Value - mo.Rating;
                    if (gain < Threshold) continue;
                    options.Add((c, w, gain, o.Pair.Value, mo.Rating));
                }
            }
            var done = new HashSet<string>();
            var result = new List<PickSuggestion>();
            foreach (var (c, first, gain, pair, alone) in options.OrderByDescending(o => o.gain).ThenBy(o => o.c.Key, StringComparer.Ordinal).ThenBy(o => o.w.Key, StringComparer.Ordinal))
            {
                var w = done.Contains(c.Key) ? null : Untaken(first, free, taken);
                if (w == null) continue;
                done.Add(c.Key);
                taken.Add(w.Key);
                result.Add(new PickSuggestion { Kind = PickKind.OffHand, Colonist = c.Key, Weapon = w.Key, Instead = c.Holds, Gain = gain, Rating = pair, Before = alone });
            }
            return result;
        }

        // ---------------------------------------------------------------- why

        /// <summary>
        /// A suggestion's reasons, strongest first (two at most; a trade also names the other), and what it costs: their skill
        /// and a weapon that rewards it (or forgives a low one), the trait that adds most (from <paramref name="steps"/>, the
        /// steps of their own rating, WeaponForMath.For, when worked out), the fights the colony meets where it is a point
        /// better (its EMP for mechanoids, its piercing for armour), a hunter's hunts, a doctor's or crafter's light weapon,
        /// their passion for a kind new to their hands, their faith, an effect the weapon in hand lacks; simply better when
        /// nothing else says why. Costs: a bonded persona weapon left unused (its jealousy), a hunter who couldn't hunt.
        /// </summary>
        public static List<PickReason> Reasons(PickInput input, PickSuggestion s, List<(string why, float delta)> steps = null)
        {
            var c = input.Colonist(s.Colonist);
            var w = input.Weapon(s.Weapon);
            var held = s.Kind == PickKind.Sidearm ? null : input.Weapon(s.Instead ?? c?.Holds);
            var list = new List<(PickReason r, float weight)>();
            if (c == null || w?.Rating == null) return new List<PickReason>();
            void Add(PickWhy kind, float weight, string text = "", float value = 0f, int level = 0) =>
                list.Add((new PickReason { Kind = kind, Text = text ?? "", Value = value, Level = level }, weight));
            int skill = w.Ranged ? c.Shooting : c.Melee;
            float gainNow = w.Rating.SkillGain, gainHeld = held?.Rating?.SkillGain ?? 1f;
            // Their skill: a high one makes the most of a weapon that rewards it; a low one is forgiven by one that doesn't ask much.
            if (skill >= 12 && gainNow >= 1.35f && (held == null || gainNow > gainHeld + 0.1f)) Add(PickWhy.SkillHigh, 0.5f + (gainNow - 1f), "", 0f, skill);
            else if (skill <= 5 && gainNow <= 1.25f && held != null && gainHeld >= 1.35f) Add(PickWhy.SkillLow, 0.4f, "", 0f, skill);
            // Their traits: the one that adds most to this weapon (from its own rating's steps).
            if (steps != null)
            {
                var trait = steps.Where(t => t.why != "body and gear" && !t.why.StartsWith("Shooting ", StringComparison.Ordinal) && !t.why.StartsWith("Melee ", StringComparison.Ordinal))
                    .OrderByDescending(t => t.delta).FirstOrDefault();
                if (trait.why != null && trait.delta >= 0.2f) Add(PickWhy.Trait, trait.delta, trait.why, trait.delta);
            }
            // The fights the colony meets: a situation where it is a point or more better than what they hold.
            var mix = Mix(c, w, input.Colony);
            foreach (var kv in mix)
            {
                if (kv.Key == "hunting" || kv.Key == "sidearm" || kv.Value < 0.05f) continue;
                float diff = w.Situation(kv.Key) - (held != null ? held.Situation(kv.Key) : Unarmed);
                if (diff < 1f) continue;
                float weight = kv.Value * diff * 2f;
                if (kv.Key == "mechanoids" && w.HasSpecial("emp")) Add(PickWhy.MechsEmp, weight + 0.1f, kv.Key);
                else if (kv.Key == "heavily armoured" && (w.HasTag("armour-piercing") || w.HasSpecial("piercing"))) Add(PickWhy.ArmourPierce, weight + 0.1f, kv.Key);
                else Add(PickWhy.Situation, weight, kv.Key);
            }
            // Their work: a hunter's clean kills, a doctor's or crafter's light weapon.
            if (c.Hunter && w.Ranged && w.HuntingOk && (held == null || !held.HuntingOk || w.Situation("hunting") >= held.Situation("hunting") + 1f)) Add(PickWhy.Hunting, 0.35f);
            if (c.Worker != null && w.Situation("sidearm") >= (held != null ? held.Situation("sidearm") : 0f) + 1f && s.Kind == PickKind.Primary)
                Add(PickWhy.Worker, 0.3f, c.Worker);
            // Their passion, when the kind is new to their hands.
            int passion = w.Ranged ? c.ShootingPassion : c.MeleePassion;
            if (passion > 0 && (held == null || held.Ranged != w.Ranged)) Add(PickWhy.Passion, 0.15f * passion + 0.1f, w.Ranged ? "Shooting" : "Melee", 0f, passion);
            if (input.Option(c, w)?.Noble == true) Add(PickWhy.Noble, 0.45f);
            // An effect the weapon in their hands lacks: an ability, EMP, a stun, fire, piercing, stopping power.
            string[] telling = { "ability:", "emp", "stun", "ignite", "piercing", "stopping", "toxic", "beam" };
            var special = w.Rating.Specials.Where(x => x.Counted && x.Sign > 0 && telling.Any(t => x.Key.StartsWith(t, StringComparison.Ordinal)))
                .Where(x => held == null || !held.HasSpecial(x.Key)).OrderBy(x => Array.FindIndex(telling, t => x.Key.StartsWith(t, StringComparison.Ordinal))).FirstOrDefault();
            if (special != null) Add(PickWhy.Special, 0.2f, Short(special.Label));
            // The kind of suggestion.
            switch (s.Kind)
            {
                case PickKind.Sidearm:
                    Add(PickWhy.Sidearm, 0.6f, input.Weapon(s.Instead)?.Label ?? "");
                    break;
                case PickKind.OffHand:
                    Add(PickWhy.OffHand, 0.6f, "", s.Rating);
                    break;
                case PickKind.Swap:
                    Add(PickWhy.Swap, 0.05f, input.Colonist(s.Other)?.Name ?? "", s.OtherGain);
                    break;
            }
            if (s.Kind == PickKind.Primary && c.Holds == null && !c.Carries.Any()) Add(PickWhy.Unarmed, 0.55f);
            var reasons = list.OrderByDescending(x => x.weight).ThenBy(x => x.r.Kind).Select(x => x.r).ToList();
            // A trade always names the other; otherwise two reasons, the strongest. Simply better when nothing else says why.
            var shown = new List<PickReason>();
            foreach (var r in reasons)
            {
                if (shown.Count >= 2) break;
                if (r.Kind == PickWhy.Swap) continue;
                shown.Add(r);
            }
            if (shown.Count == 0) shown.Add(new PickReason { Kind = PickWhy.Better, Value = s.Rating, Level = (int)Math.Round(s.Before * 10f) });
            if (s.Kind == PickKind.Swap) shown.Add(reasons.First(r => r.Kind == PickWhy.Swap));
            // What it costs, always said.
            if (s.Kind != PickKind.Sidearm && s.Kind != PickKind.OffHand && c.HoldsBonded)
            {
                shown.Add(new PickReason { Kind = PickWhy.Bonded, Text = input.Weapon(c.Bonded)?.Label ?? "", Cost = true });
                if (c.BondJealous) shown.Add(new PickReason { Kind = PickWhy.Jealous, Text = input.Weapon(c.Bonded)?.Label ?? "", Cost = true });
            }
            if (s.Kind != PickKind.Sidearm && s.Kind != PickKind.OffHand && c.Hunter && !(w.Ranged && w.HuntingOk))
                shown.Add(new PickReason { Kind = PickWhy.NoHunting, Cost = true });
            return shown;
        }

        /// <summary>An effect's words up to its first colon or bracket ("EMP: stuns mechanoids (3 s)" is "EMP").</summary>
        public static string Short(string label)
        {
            if (string.IsNullOrEmpty(label)) return "";
            int cut = label.Length;
            foreach (char ch in new[] { ':', '(' })
            {
                int i = label.IndexOf(ch);
                if (i > 0 && i < cut) cut = i;
            }
            return label.Substring(0, cut).Trim();
        }

        /// <summary>The line's tag for a suggestion's strongest reason, for the colonist's own words (Rimfolk_Pool_WeaponAsk).</summary>
        public static string AskTag(PickSuggestion s)
        {
            var r = s?.Reasons.FirstOrDefault();
            if (s?.Kind == PickKind.Sidearm) return "sidearm";
            if (s?.Kind == PickKind.OffHand) return "offhand";
            if (r == null) return "better";
            switch (r.Kind)
            {
                case PickWhy.MechsEmp: return "mechanoids";
                case PickWhy.ArmourPierce: return "armoured";
                case PickWhy.Situation:
                    switch (r.Text)
                    {
                        case "mechanoids": return "mechanoids";
                        case "heavily armoured": return "armoured";
                        case "crowds": return "crowds";
                        case "siege or long duel": return "siege";
                        case "holding a choke": return "choke";
                        default: return "better";
                    }
                case PickWhy.SkillHigh:
                case PickWhy.SkillLow: return "skill";
                case PickWhy.Trait: return "trait";
                case PickWhy.Hunting: return "hunting";
                case PickWhy.Passion: return "passion";
                case PickWhy.Noble: return "noble";
                case PickWhy.Worker: return "worker";
                case PickWhy.Special: return "special";
                default: return "better";
            }
        }

        /// <summary>A gain as the lines show it: "+1.4".</summary>
        public static string GainText(float g) => "+" + Math.Max(0f, (float)Math.Round(g, 1)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        // ---------------------------------------------------------------- who should carry this

        /// <summary>
        /// Who a weapon suits best: each colonist who could carry it, with its score for them and its gain over what they
        /// have now in that kind, the biggest gains first (then the highest scores), at most <paramref name="max"/>.
        /// </summary>
        public static List<(PickColonist c, float score, float gain)> WhoShouldCarry(PickInput input, PickWeapon w, int max = 3)
        {
            var list = new List<(PickColonist c, float score, float gain)>();
            if (input == null || w?.Rating == null) return list;
            foreach (var c in input.Colonists)
            {
                if (!c.CanFight) continue;
                var s = Score(input, c, w);
                if (!s.HasValue) continue;
                float before = c.Holds == w.Key ? s.Value : BestOfKind(input, c, w.Ranged) ?? (c.Holds == null ? Unarmed : Current(input, c));
                list.Add((c, s.Value, s.Value - before));
            }
            return list.OrderByDescending(x => x.gain).ThenByDescending(x => x.score).ThenBy(x => x.c.Key, StringComparer.Ordinal).Take(max).ToList();
        }
    }

    /// <summary>
    /// When a colonist asks for a better weapon lying in storage (docs/design/weapons.md, point 9), without game types: only
    /// a clearly better one (<see cref="WeaponPickMath.AskThreshold"/>), at most once in five days a colonist and once a day
    /// in the colony, the same weapon not again for fifteen days, never while the player has set their suggestions aside,
    /// and then only now and then (each hourly look in their free time).
    /// </summary>
    public static class WeaponAskMath
    {
        public const int DayTicks = 60000;
        public const int EachDays = 5, ColonyHours = 24, SameWeaponDays = 15, NotNowDays = 10;
        /// <summary>The inbox keeps the ask this long while the suggestion stands.</summary>
        public const int ShowDays = 3;
        /// <summary>The chance at each hourly look in their free time; about a third of the days they have free time, once the waits are over.</summary>
        public const float Chance = 0.12f;

        /// <summary>Whether a suggestion is one a colonist asks for: a weapon from storage in hand, as a sidearm or in the off hand, clearly better.</summary>
        public static bool Askable(PickSuggestion s) => s != null && s.Kind != PickKind.Swap && s.Gain >= WeaponPickMath.AskThreshold;

        /// <summary>Whether they may ask now: their own wait, the colony's, the same weapon's, and the player's "not now" all over.</summary>
        public static bool MayAsk(int now, int lastByThem, int lastInColony, int lastWeapon, int weapon, int notNowUntil)
        {
            if (notNowUntil > now) return false;
            if (lastByThem >= 0 && now - lastByThem < EachDays * DayTicks) return false;
            if (lastInColony >= 0 && now - lastInColony < ColonyHours * 2500) return false;
            if (lastWeapon == weapon && lastByThem >= 0 && now - lastByThem < SameWeaponDays * DayTicks) return false;
            return true;
        }

        /// <summary>Whether an ask still shows in the inbox: asked lately and not set aside.</summary>
        public static bool Shows(int now, int asked, int notNowUntil) => asked >= 0 && now - asked < ShowDays * DayTicks && notNowUntil <= now;
    }
}
