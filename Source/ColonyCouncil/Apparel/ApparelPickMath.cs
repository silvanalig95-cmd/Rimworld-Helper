using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>A colonist as the choice of clothes sees them: how much they fight, their work, how cold or hot the season ahead is for them, what they wear and carry.</summary>
    public sealed class WearColonist
    {
        public string Key = "", Name = "";
        /// <summary>Able to fight at all (not incapable of violence).</summary>
        public bool CanFight = true;
        /// <summary>How much they fight, from <see cref="WeaponPickMath.FightFloor"/> to 1 (WeaponPickMath.FightWeight); 0 for someone who can't.</summary>
        public float Fights = 1f;
        /// <summary>Their main work, for clothes that speed it: doctor, crafter, researcher, grower, miner, cook, builder, social; null for none of these.</summary>
        public string Work;
        /// <summary>The player set their suggestions aside for now.</summary>
        public bool NotNow;
        /// <summary>
        /// How many degrees colder (warmer) than they can bear in what they wear it gets over the season ahead (the coldest
        /// night and hottest day of now and the next two twelfths, the game's "Need warm clothes" look); 0 when dressed for it.
        /// </summary>
        public float ColdShort, HeatShort;
        /// <summary>
        /// The degrees to spare before the season ahead is colder (hotter) than they can bear in what they wear: what a piece
        /// may take away without leaving them short (<see cref="ApparelPickMath.NoLimit"/> where it never gets cold or hot enough to count).
        /// </summary>
        public float ColdSpare = ApparelPickMath.NoLimit, HeatSpare = ApparelPickMath.NoLimit;
        /// <summary>How many degrees colder than they can bear it is outside right now (their words: "it's freezing").</summary>
        public float ColdNow;
        /// <summary>The pieces they wear and the apparel they carry in their pack (pieces' keys).</summary>
        public List<string> Wears = new List<string>();
        public List<string> Carries = new List<string>();
        /// <summary>Worn pieces that mean something to them (Rimfolk's lucky item, an heirloom): giving one up costs mood.</summary>
        public List<WearKeepsake> Keepsakes = new List<WearKeepsake>();
    }

    /// <summary>A worn piece that means something to its wearer: which, why, and their mood with it and without it.</summary>
    public sealed class WearKeepsake
    {
        public KeepsakeKind Kind;
        /// <summary>The piece's key (worn by them).</summary>
        public string Piece = "";
        /// <summary>Its kind (a def's name): a lucky item's mood comes with any piece of the kind, so another of it keeps it.</summary>
        public string Def = "";
        public string Label = "", Of = "";
        public float MoodWith, MoodWithout;
    }

    /// <summary>A piece the colony has, as the choice sees it: its role and reference rating, where it is, what it covers, how warm and worn it is.</summary>
    public sealed class WearPiece
    {
        public string Key = "", Label = "", Def = "";
        /// <summary>Alike pieces (one kind, stuff, quality and wear stage, none of their own traits) share a group, rated once.</summary>
        public string Group = "";
        /// <summary>The rating's role: body armour, helmet, off-hand shield, utility, everyday clothing, headgear, cold- or hot-weather gear, hazard gear, ceremonial or psychic.</summary>
        public string Role = "everyday clothing";
        public float Overall = 5.5f;
        /// <summary>Its layers and the body part groups it is in the way of (the game's interfering groups): two pieces sharing both can't be worn together.</summary>
        public List<string> Layers = new List<string>(), Groups = new List<string>();
        /// <summary>The degrees it lowers its wearer's least comfortable temperature and raises their most.</summary>
        public float Warmth, Cooling;
        /// <summary>The apparel-damaged mood it gives (-1 none, 0 below half its hit points, 1 below a fifth), and taken from the dead.</summary>
        public int Stage = -1;
        public bool FromDead;
        /// <summary>Worn by a colonist, carried in a colonist's pack (their keys), or lying in storage free to take (or on an outfit stand).</summary>
        public string WornBy, CarriedBy;
        public bool Stored;
        /// <summary>Worn and locked by the player: never taken off for a suggestion.</summary>
        public bool Locked;
        /// <summary>For a worn piece, what keeps it on its wearer (title, role, faith), or null: never replaced by one that doesn't meet it too.</summary>
        public string Rule;
        /// <summary>The rating's situations of the fight (guns, melee, mechanoids, fire), for the raids the colony meets.</summary>
        public Dictionary<string, float> Situations = new Dictionary<string, float>();

        public bool Armour => ApparelPickMath.IsArmour(Role);
        public bool Worn => Stage >= 0 || FromDead;
        public float Situation(string key) => Situations.TryGetValue(key, out var v) ? v : Overall;
    }

    /// <summary>One colonist with one piece: their own rating of it, what stops them, their outfit, what it would take the place of, the rules it meets, their work.</summary>
    public sealed class WearOption
    {
        /// <summary>Their own rating (ApparelForMath.For: how they fight, their body, their weather, the moods it adds).</summary>
        public float Rating;
        /// <summary>Why they can't wear it (coded to another, made for another age, nothing to wear it on, below their title's quality, would replace a locked piece), or null.</summary>
        public string Cant;
        /// <summary>Their outfit's name when it doesn't allow the piece (null: allowed); and whether allowing its kind there would let it.</summary>
        public string Outfit;
        public bool OutfitFixable = true;
        /// <summary>The pieces they wear it would take the place of (the game's CanWearTogether), keys.</summary>
        public List<string> Replaces = new List<string>();
        /// <summary>Its rating for them judged as another role (a replaced piece's), where that differs from its own.</summary>
        public Dictionary<string, float> As = new Dictionary<string, float>();
        /// <summary>The rules of theirs it meets (title, role, faith).</summary>
        public List<string> Meets = new List<string>();
        /// <summary>A rule of theirs not met now that it would meet (their title's or role's apparel they lack, their faith's desired apparel), or null.</summary>
        public string Unmet;
        /// <summary>How it moves their own work (ApparelPickMath.WorkLean): its share faster (+) or slower (-).</summary>
        public float Work;
        /// <summary>The mood of wearing a piece made for the other gender (0 or less), for the cost line.</summary>
        public float GenderMood;
    }

    /// <summary>Everything the choice reads: the colonists, the pieces, each pair's option, the raids the colony meets.</summary>
    public sealed class WearInput
    {
        public List<WearColonist> Colonists = new List<WearColonist>();
        public List<WearPiece> Pieces = new List<WearPiece>();
        public Dictionary<(string colonist, string piece), WearOption> Options = new Dictionary<(string, string), WearOption>();
        /// <summary>The raids met lately, each weighed by how long ago (WeaponPickMath.Recency); raid-heavy at <see cref="ApparelPickMath.RaidHeavyAt"/>.</summary>
        public float Raids;
        /// <summary>The shares of those raids by the armour they test: mechanoids, and crowds and tribes at close quarters (melee).</summary>
        public float MechShare, MeleeShare;

        public bool RaidHeavy => Raids >= ApparelPickMath.RaidHeavyAt;

        public WearOption Option(WearColonist c, WearPiece p) => c != null && p != null && Options.TryGetValue((c.Key, p.Key), out var o) ? o : null;

        public WearPiece Piece(string key) => key == null ? null : Pieces.FirstOrDefault(p => p.Key == key);

        public WearColonist Colonist(string key) => key == null ? null : Colonists.FirstOrDefault(c => c.Key == key);
    }

    /// <summary>What a suggestion asks: a piece from storage, one from their own pack, another's spare, a trade of armour between two, or a piece their outfit doesn't allow yet.</summary>
    public enum WearKind
    {
        Wear,
        Pack,
        Spare,
        Trade,
        Outfit
    }

    /// <summary>A gap in someone's clothes, filled before anyone's upgrade: underdressed for the cold or the heat ahead, no body armour or helmet in a raid-heavy colony, a worn-out piece or one from the dead.</summary>
    public enum WearGap
    {
        Warmth,
        Cooling,
        Armour,
        Helmet,
        Worn
    }

    /// <summary>Why a piece suits a colonist better, or what taking it costs, as the line's parts.</summary>
    public enum WearWhy
    {
        /// <summary>Underdressed for the cold ahead (Level: degrees short, Value: degrees it adds).</summary>
        Warmth,
        Cooling,
        /// <summary>No body armour (helmet) and the colony is raided often.</summary>
        Armour,
        Helmet,
        /// <summary>In place of a worn-out piece (Text: its label, Level: its stage).</summary>
        Worn,
        /// <summary>In place of a piece taken from the dead (Text: its label).</summary>
        Dead,
        /// <summary>It stands up to the mechanoids, or the crowds at close quarters, the colony keeps meeting.</summary>
        Mechs,
        Melee,
        /// <summary>It speeds their own work (Text: the work, Value: the share).</summary>
        Work,
        /// <summary>Their faith's desired apparel, their title's, their role's.</summary>
        Faith,
        Title,
        Role,
        /// <summary>Simply better for them (Value: its rating for them, Level: what they have, in tenths).</summary>
        Better,
        /// <summary>Nothing is worn there now, and it is a good piece for them.</summary>
        AddOn,
        /// <summary>It is in their own pack.</summary>
        Pack,
        /// <summary>Another carries it unworn (Text: their key).</summary>
        Spare,
        /// <summary>A trade of armour better for both (Text: the other's key, Value: their gain).</summary>
        Trade,
        /// <summary>A trade where one gives up some: the colony gains (Value: its gain).</summary>
        TradeColony,
        /// <summary>The one who gives up some rarely fights (Text: their key).</summary>
        RarelyFights,
        /// <summary>A cost: someone's lucky piece or heirloom would come off (Text: whose key, Extra: its label, Value, Level: mood with and without).</summary>
        Lucky,
        Heirloom,
        /// <summary>A cost: it slows their work (Value: the share).</summary>
        Slower,
        /// <summary>A cost: it is made for the other gender (Value: its mood).</summary>
        Gender
    }

    public sealed class WearReason
    {
        public WearWhy Kind;
        public string Text = "", Extra = "", Of = "";
        public float Value;
        public int Level;
        public bool Cost;
    }

    /// <summary>One suggestion: who, which piece, in place of what, how much better, which gap it fills, and why.</summary>
    public sealed class WearSuggestion
    {
        public WearKind Kind;
        public string Colonist, Piece;
        /// <summary>A trade's other, or another's spare's carrier.</summary>
        public string Other;
        /// <summary>The worn pieces it takes the place of.</summary>
        public List<string> Instead = new List<string>();
        /// <summary>How much better for them on the rating's scale (and for the other, in a trade); a trade's worth to the colony.</summary>
        public float Gain, OtherGain, Worth;
        /// <summary>Its rating for them, and the best of what it takes the place of (0: nothing there).</summary>
        public float Rating, Before;
        public WearGap? Gap;
        /// <summary>The degrees it adds to their warmth (cooling for a cooling gap), less what it replaces.</summary>
        public float Degrees;
        /// <summary>Their outfit that doesn't allow it (an Outfit suggestion).</summary>
        public string Outfit;
        public bool OutfitFixable = true;
        public List<WearReason> Why = new List<WearReason>();

        public IEnumerable<WearReason> Reasons => Why.Where(r => !r.Cost);
        public IEnumerable<WearReason> Costs => Why.Where(r => r.Cost);
    }

    /// <summary>
    /// Better armour and clothes for each colonist from what the colony has (docs/design/apparel.md, step 3, point 6),
    /// without game types (Tools/Tests try it). Each colonist with each piece is scored by their own rating
    /// (ApparelForMath: how they fight, their body, their weather, the moods it adds) and by what the colony's life asks of
    /// it: the raids met (armour against the mechanoids or crowds that come, weighed by how much the colonist fights), their
    /// work (a doctor's or crafter's clothes that speed it), and the rules of their title, role and faith (a piece they lack
    /// counts for much; one they wear for a rule is never taken off for one that doesn't meet it). A piece replacing others
    /// is judged in each one's own role, so marine armour in place of a vest and a parka gains as armour and loses as a coat.
    /// <para>
    /// One assignment for the whole colony, gaps first: the coldest (and the hottest) first, the warmth they lack over the
    /// season ahead filled from empty layers and slots; then, when the colony is raided often, the fighters without body
    /// armour or a helmet, the most fighting first; then worn-out pieces and those taken from the dead, always worth
    /// replacing; then upgrades past <see cref="Threshold"/>, armour's gains weighed by how much each fights. Pieces that
    /// fit together, each piece to one colonist. Then trades of armour between two colonists (better for both, or for the
    /// colony), and what their outfit doesn't allow yet, said as such.
    /// </para>
    /// </summary>
    public static class ApparelPickMath
    {
        // ---------------------------------------------------------------- the thresholds

        /// <summary>
        /// The least gain an upgrade is suggested for: 0.8 of a point on the rating's scale. A little more than the weapons'
        /// 0.6, as changing clothes leaves the old piece behind and the season moves a piece's rating by a few tenths: a flak
        /// helmet over a simple one (+0.8) or recon armour over a flak vest (+2.9) is suggested; marine armour over recon (+0.3)
        /// or a good parka over a normal one is not.
        /// </summary>
        public const float Threshold = 0.8f;

        /// <summary>The least gain a colonist asks for on their own: twice the gear tab's.</summary>
        public const float AskThreshold = 1.6f;

        /// <summary>A gap is filled with anything rated this or more for them: anything sensible is better than nothing.</summary>
        public const float GapFloor = 2f;

        /// <summary>A piece for an empty slot that is no gap is suggested only when it rates this much for them on its own (or meets a rule of theirs not met now).</summary>
        public const float AddOnAt = 6f;

        /// <summary>What nothing worn there is worth on the rating's scale.</summary>
        public const float Nothing = 1f;

        /// <summary>Underdressed when it gets at least this many degrees colder (hotter) over the season ahead than they can bear.</summary>
        public const float ShortAt = 1f;

        /// <summary>A piece fills a warmth (cooling) gap when it adds at least this many degrees, or all that is lacking when less.</summary>
        public const float DegreesAt = 3f;

        /// <summary>
        /// The cold and the heat that count, as the game's own "Need warm clothes" alert counts the cold: a season ahead whose
        /// coldest night is under 5 °C (whose hottest day is over 30 °C). Milder weather asks nothing of the clothes.
        /// </summary>
        public const float ColdAt = 5f, HotAt = 30f;

        /// <summary>Degrees to spare where the weather never gets cold or hot enough to count.</summary>
        public const float NoLimit = 99f;

        /// <summary>
        /// How many degrees short and to spare a colonist is over the season ahead: their least comfortable temperature in
        /// what they wear against its coldest night (or most against its hottest day), counted only past <see cref="ColdAt"/>
        /// (<see cref="HotAt"/>).
        /// </summary>
        public static (float shortBy, float spare) Margin(float comfy, float extreme, bool cold)
        {
            bool counts = cold ? extreme < ColdAt : extreme > HotAt;
            if (!counts) return (0f, NoLimit);
            float spare = cold ? extreme - comfy : comfy - extreme;
            return (Math.Max(0f, -spare), Math.Max(-NoLimit, Math.Min(NoLimit, spare)));
        }

        /// <summary>At most this many pieces for one colonist's warmth (cooling) in a round: a coat, then a hat.</summary>
        public const int WarmPieces = 2;

        /// <summary>The raids met lately, weighed by recency, from which the colony is raid-heavy: two in the last thirty days, or four in sixty.</summary>
        public const float RaidHeavyAt = 2f;

        /// <summary>A rule of theirs not met now that the piece meets: their title's or role's apparel (the game's mood for lacking it), their faith's desired apparel.</summary>
        public const float RuleBonus = 1.5f, FaithBonus = 0.6f;

        /// <summary>Clothes that speed their own work by <see cref="WorkAt"/> or more; those that slow it.</summary>
        public const float WorkBonus = 0.4f, WorkCost = 0.3f, WorkAt = 0.03f;

        /// <summary>How far the raids the colony meets move armour, either way, for someone who fights all the time.</summary>
        public const float LeanCap = 1f;

        /// <summary>A situation's share of the raids counts at this part: mechanoids half, crowds at close quarters two fifths.</summary>
        public const float MechPart = 0.5f, MeleePart = 0.4f;

        public static bool IsArmour(string role) => role == "body armour" || role == "helmet" || role == "off-hand shield";

        // ---------------------------------------------------------------- the raids

        /// <summary>
        /// The raids the colony met as the clothes see them: their recency-weighed count (raid-heavy at <see cref="RaidHeavyAt"/>),
        /// and the shares that were mechanoids and crowds of people or tribes (close quarters, the melee the armour meets).
        /// </summary>
        public static (float weighed, float mechs, float melee) Raids(IEnumerable<RaidSeen> raids, int now)
        {
            float total = 0f, mechs = 0f, melee = 0f;
            foreach (var r in raids ?? Enumerable.Empty<RaidSeen>())
            {
                float w = WeaponPickMath.Recency(now, r.Tick);
                if (w <= 0f) continue;
                total += w;
                if (r.Count > 0)
                {
                    mechs += w * Math.Min(1f, r.Mechs / (float)r.Count);
                    // A crowd of people at close quarters (a mechanoid raid's armour test is its own, above).
                    if (r.Tribal || (r.Count >= WeaponPickMath.CrowdSize && r.Mechs * 2 < r.Count)) melee += w;
                }
                else
                {
                    if (r.Mechs > 0) mechs += w;
                    if (r.Tribal) melee += w;
                }
            }
            return total > 0f ? (total, mechs / total, melee / total) : (0f, 0f, 0f);
        }

        // ---------------------------------------------------------------- work

        /// <summary>The stats each kind of work is sped by (its own, and the colonist's general work speed with all).</summary>
        public static readonly Dictionary<string, string[]> WorkStats = new Dictionary<string, string[]>
        {
            ["doctor"] = new[] { "MedicalTendQuality", "MedicalTendSpeed", "MedicalSurgerySuccessChance", "MedicalOperationSpeed" },
            ["crafter"] = new[] { "GeneralLaborSpeed" },
            ["researcher"] = new[] { "ResearchSpeed" },
            ["grower"] = new[] { "PlantWorkSpeed", "PlantHarvestYield" },
            ["miner"] = new[] { "MiningSpeed", "MiningYield" },
            ["cook"] = new[] { "CookSpeed", "ButcheryFleshSpeed", "ButcheryFleshEfficiency", "FoodPoisonChance" },
            ["builder"] = new[] { "ConstructionSpeed", "ConstructSuccessChance", "SmoothingSpeed" },
            ["social"] = new[] { "SocialImpact", "NegotiationAbility", "TradePriceImprovement", "SuppressionPower" },
        };

        /// <summary>
        /// How much a piece speeds (or slows) a colonist's own work: their general work speed and their work's own stats, as
        /// shares (a food poisoning chance counts against); null work, none.
        /// </summary>
        public static float WorkLean(ApparelFacts a, string work)
        {
            if (a == null || work == null || !WorkStats.TryGetValue(work, out var stats)) return 0f;
            float lean = ApparelRateMath.Offset(a, "WorkSpeedGlobal");
            foreach (var s in stats) lean += (s == "FoodPoisonChance" ? -1f : 1f) * ApparelRateMath.Offset(a, s);
            return lean;
        }

        // ---------------------------------------------------------------- pieces together

        /// <summary>Two pieces can be worn together unless they share a layer and a body part group (the game's CanWearTogether).</summary>
        public static bool Fits(WearPiece a, WearPiece b)
        {
            if (a == null || b == null || a == b) return a != b;
            return !(a.Layers.Any(b.Layers.Contains) && a.Groups.Any(b.Groups.Contains));
        }

        // ---------------------------------------------------------------- one colonist, one piece

        /// <summary>
        /// What the colony's life adds to a piece for a colonist: a rule of theirs it meets that isn't met now, their work, and
        /// for armour the raids the colony meets (its mechanoids' and close quarters' situations over its overall, by their
        /// shares, at most <see cref="LeanCap"/>), weighed by how much they fight.
        /// </summary>
        public static float Bonus(WearInput input, WearColonist c, WearPiece p, WearOption o)
        {
            float b = 0f;
            if (o.Unmet != null) b += o.Unmet == "faith" ? FaithBonus : RuleBonus;
            if (o.Work >= WorkAt) b += WorkBonus;
            else if (o.Work <= -WorkAt) b -= WorkCost;
            if (p.Armour && c.CanFight) b += Lean(input, p) * Math.Max(0f, Math.Min(1f, c.Fights));
            return b;
        }

        /// <summary>Armour's lean by the raids met: its mechanoids' and melee situations over its overall, by their shares, at most <see cref="LeanCap"/> either way.</summary>
        public static float Lean(WearInput input, WearPiece p)
        {
            if (input == null || p == null) return 0f;
            float l = input.MechShare * MechPart * (p.Situation("mechanoids") - p.Overall) + input.MeleeShare * MeleePart * (p.Situation("melee") - p.Overall);
            return Math.Max(-LeanCap, Math.Min(LeanCap, l));
        }

        /// <summary>A piece's score for a colonist: their own rating and what the colony's life adds. Null when they can't wear it.</summary>
        public static float? Score(WearInput input, WearColonist c, WearPiece p)
        {
            var o = input.Option(c, p);
            if (o == null || o.Cant != null) return null;
            return o.Rating + Bonus(input, c, p, o);
        }

        /// <summary>
        /// A piece's gain for a colonist, in place of what it would replace: judged in each replaced piece's own role (marine
        /// armour in place of a vest and a parka gains as armour and loses as a coat), the colony's additions over theirs, and
        /// over nothing (<see cref="Nothing"/>) when it replaces nothing. Null when it isn't a choice for them: they can't wear
        /// it, it would take off a locked piece, or one they wear for a rule it doesn't meet.
        /// <paramref name="before"/> is the best of what it replaces (0: nothing), <paramref name="degrees"/> the warmth it adds
        /// net of what it replaces, <paramref name="cooling"/> the same for the heat.
        /// </summary>
        public static float? Gain(WearInput input, WearColonist c, WearPiece x, out float before, out float degrees, out float cooling, bool ignoreOutfit = false)
        {
            before = 0f;
            degrees = 0f;
            cooling = 0f;
            var o = input.Option(c, x);
            if (o == null || o.Cant != null || (!ignoreOutfit && o.Outfit != null) || c.Wears.Contains(x.Key)) return null;
            float sx = o.Rating + Bonus(input, c, x, o);
            degrees = x.Warmth;
            cooling = x.Cooling;
            if (o.Replaces.Count == 0) return sx - Nothing;
            float g = 0f, bonusY = 0f;
            float lost = 0f, lostCool = 0f;
            foreach (var key in o.Replaces)
            {
                var y = input.Piece(key);
                if (y == null) continue;
                if (y.Locked) return null;
                if (y.Rule != null && !o.Meets.Contains(y.Rule)) return null;
                var oy = input.Option(c, y);
                float ry = oy?.Rating ?? y.Overall;
                float asY = y.Role == x.Role || !o.As.TryGetValue(y.Role, out var v) ? o.Rating : v;
                g += asY - ry;
                if (oy != null) bonusY += Bonus(input, c, y, oy);
                before = Math.Max(before, ry);
                lost += y.Warmth;
                lostCool += y.Cooling;
            }
            degrees -= lost;
            cooling -= lostCool;
            // Never one that leaves them colder (hotter) than they can bear over the season ahead.
            if (!KeepsComfort(c, degrees, cooling)) return null;
            return g + (sx - o.Rating) - bonusY;
        }

        /// <summary>Whether a change of this many degrees of warmth and cooling still leaves them dressed for the season ahead (a loss only within what they have to spare).</summary>
        public static bool KeepsComfort(WearColonist c, float warmth, float cooling) =>
            (warmth >= -1e-4f || c.ColdSpare + warmth >= -1e-4f) && (cooling >= -1e-4f || c.HeatSpare + cooling >= -1e-4f);

        // ---------------------------------------------------------------- gaps

        /// <summary>
        /// A colonist's gaps: underdressed for the cold (heat) ahead by <see cref="ShortAt"/> or more; in a raid-heavy colony,
        /// someone who fights more than rarely without body armour or a helmet; each worn piece below half its hit points or
        /// taken from the dead.
        /// </summary>
        public static List<(WearGap gap, string piece)> Gaps(WearInput input, WearColonist c)
        {
            var gaps = new List<(WearGap, string)>();
            if (c == null) return gaps;
            if (c.ColdShort >= ShortAt) gaps.Add((WearGap.Warmth, null));
            if (c.HeatShort >= ShortAt) gaps.Add((WearGap.Cooling, null));
            if (Fighter(input, c))
            {
                var worn = c.Wears.Select(input.Piece).Where(p => p != null).ToList();
                if (!worn.Any(p => p.Role == "body armour")) gaps.Add((WearGap.Armour, null));
                if (!worn.Any(p => p.Role == "helmet")) gaps.Add((WearGap.Helmet, null));
            }
            foreach (var k in c.Wears)
            {
                var p = input.Piece(k);
                if (p != null && p.Worn && !p.Locked) gaps.Add((WearGap.Worn, k));
            }
            return gaps;
        }

        /// <summary>One who should wear armour: the colony is raid-heavy and they fight more than rarely.</summary>
        public static bool Fighter(WearInput input, WearColonist c) => input.RaidHeavy && c.CanFight && c.Fights > WeaponPickMath.RarelyFightsAt + 1e-4f;

        /// <summary>
        /// Whether a piece fills a gap of theirs (with its gain and degrees from <see cref="Gain"/>): warmth or cooling that adds
        /// <see cref="DegreesAt"/> (or all that is lacking) net of what it replaces; body armour or a helmet for a fighter; in
        /// place of a worn-out piece, one not itself as worn and no worse once its wear is counted. Each at least
        /// <see cref="GapFloor"/> for them.
        /// </summary>
        public static bool Fills(WearInput input, WearColonist c, WearPiece x, WearGap gap, string worn, float gain, float degrees, float cooling, float shortNow)
        {
            var o = input.Option(c, x);
            if (o == null || o.Rating < GapFloor - 1e-4f) return false;
            switch (gap)
            {
                case WearGap.Warmth: return degrees >= Math.Min(DegreesAt, shortNow) - 1e-4f && degrees > 0f;
                case WearGap.Cooling: return cooling >= Math.Min(DegreesAt, shortNow) - 1e-4f && cooling > 0f;
                case WearGap.Armour: return x.Role == "body armour" && gain > 1e-4f && o.Replaces.All(k => input.Piece(k)?.Role != "body armour");
                case WearGap.Helmet: return x.Role == "helmet" && gain > 1e-4f && o.Replaces.All(k => input.Piece(k)?.Role != "helmet");
                case WearGap.Worn:
                    var y = input.Piece(worn);
                    if (y == null || !o.Replaces.Contains(worn) || x.Worn) return false;
                    return gain >= -1e-4f;
                default: return false;
            }
        }

        // ---------------------------------------------------------------- the colony's assignment

        /// <summary>
        /// One assignment for the whole colony (see the class): gaps first (warmth and cooling, the most lacking first; armour
        /// and helmets for the fighters, the most fighting first; worn-out pieces), then upgrades greedily by gain (armour's
        /// weighed by how much each fights); each piece to one colonist, a colonist's pieces fitting together; then trades of
        /// armour; then what their outfit doesn't allow, one each where nothing allowed fills the need. Colonists whose
        /// suggestions were set aside take none.
        /// </summary>
        public static List<WearSuggestion> Assign(WearInput input)
        {
            var result = new List<WearSuggestion>();
            if (input == null) return result;
            var people = input.Colonists.Where(c => !c.NotNow).OrderBy(c => c.Key, StringComparer.Ordinal).ToList();
            var free = input.Pieces.Where(p => p.WornBy == null && (p.Stored || p.CarriedBy != null)).OrderBy(p => p.Key, StringComparer.Ordinal).ToList();
            var taken = new HashSet<string>();
            var plans = people.ToDictionary(c => c.Key, c => new List<WearSuggestion>());

            bool Fresh(WearColonist c, WearPiece x)
            {
                if (taken.Contains(x.Key)) return false;
                var o = input.Option(c, x);
                if (o == null) return false;
                foreach (var s in plans[c.Key])
                {
                    var other = input.Piece(s.Piece);
                    if (!Fits(other, x)) return false;
                    if (s.Instead.Any(o.Replaces.Contains)) return false;
                }
                return true;
            }

            WearSuggestion Make(WearColonist c, WearPiece x, float gain, float before, float degrees, WearGap? gap)
            {
                var o = input.Option(c, x);
                var s = new WearSuggestion
                {
                    Kind = x.Stored ? WearKind.Wear : x.CarriedBy == c.Key ? WearKind.Pack : WearKind.Spare,
                    Colonist = c.Key, Piece = x.Key, Other = x.CarriedBy != null && x.CarriedBy != c.Key ? x.CarriedBy : null,
                    Instead = new List<string>(o.Replaces), Gain = gain, Rating = o.Rating, Before = before, Gap = gap, Degrees = degrees
                };
                taken.Add(x.Key);
                plans[c.Key].Add(s);
                result.Add(s);
                return s;
            }

            // 1. Warmth and cooling: the most lacking first, a coat and then a hat, while it still lacks.
            foreach (var hot in new[] { false, true })
            {
                var needy = people.Where(c => (hot ? c.HeatShort : c.ColdShort) >= ShortAt).OrderByDescending(c => hot ? c.HeatShort : c.ColdShort).ThenBy(c => c.Key, StringComparer.Ordinal);
                foreach (var c in needy)
                {
                    float left = hot ? c.HeatShort : c.ColdShort;
                    for (int n = 0; n < WarmPieces && left >= ShortAt; n++)
                    {
                        WearPiece best = null;
                        float bestCover = 0f, bestGain = 0f, bestBefore = 0f, bestDeg = 0f;
                        foreach (var x in Heads(free, taken))
                        {
                            if (!Fresh(c, x)) continue;
                            var g = Gain(input, c, x, out float before, out float warm, out float cool);
                            if (!g.HasValue) continue;
                            float deg = hot ? cool : warm;
                            if (!Fills(input, c, x, hot ? WearGap.Cooling : WearGap.Warmth, null, g.Value, warm, cool, left)) continue;
                            float cover = Math.Min(left, deg);
                            if (best == null || cover > bestCover + 0.5f || (Math.Abs(cover - bestCover) <= 0.5f && g.Value > bestGain))
                            {
                                best = x;
                                bestCover = cover;
                                bestGain = g.Value;
                                bestBefore = before;
                                bestDeg = deg;
                            }
                        }
                        if (best == null) break;
                        Make(c, best, bestGain, bestBefore, bestDeg, hot ? WearGap.Cooling : WearGap.Warmth);
                        left -= bestDeg;
                    }
                }
            }

            // 2. Armour and helmets for the fighters of a raid-heavy colony, the most fighting first.
            foreach (var gap in new[] { WearGap.Armour, WearGap.Helmet })
            {
                foreach (var c in people.Where(c => Gaps(input, c).Any(g => g.gap == gap)).OrderByDescending(c => c.Fights).ThenBy(c => c.Key, StringComparer.Ordinal))
                {
                    if (plans[c.Key].Any(s => input.Piece(s.Piece)?.Role == (gap == WearGap.Armour ? "body armour" : "helmet"))) continue;
                    var pick = Best(input, c, free, taken, Fresh, (x, g, deg, cool) => Fills(input, c, x, gap, null, g, deg, cool, 0f));
                    if (pick.x != null) Make(c, pick.x, pick.gain, pick.before, pick.deg, gap);
                }
            }

            // 3. Worn-out pieces and those from the dead: always worth replacing.
            foreach (var c in people)
                foreach (var (gap, worn) in Gaps(input, c).Where(g => g.gap == WearGap.Worn))
                {
                    if (plans[c.Key].Any(s => s.Instead.Contains(worn))) continue;
                    var pick = Best(input, c, free, taken, Fresh, (x, g, deg, cool) => Fills(input, c, x, WearGap.Worn, worn, g, deg, cool, 0f));
                    if (pick.x != null) Make(c, pick.x, pick.gain, pick.before, pick.deg, WearGap.Worn);
                }

            // 4. Upgrades, the biggest weighed gain first: armour's by how much each fights.
            var offers = new List<(WearColonist c, WearPiece x, float gain, float before, float deg, float weighed)>();
            foreach (var c in people)
                foreach (var x in Heads(free, taken))
                {
                    var g = Gain(input, c, x, out float before, out float warm, out _);
                    if (!g.HasValue) continue;
                    var o = input.Option(c, x);
                    bool upgrade = o.Replaces.Count > 0 && g.Value >= Threshold - 1e-4f;
                    bool addOn = o.Replaces.Count == 0 && (o.Rating >= AddOnAt - 1e-4f || o.Unmet != null);
                    if (!upgrade && !addOn) continue;
                    offers.Add((c, x, g.Value, before, warm, g.Value * Weight(c, x)));
                }
            foreach (var (c, x0, gain0, _, _, _) in offers.OrderByDescending(o => o.weighed).ThenBy(o => o.c.Key, StringComparer.Ordinal).ThenBy(o => o.x.Key, StringComparer.Ordinal))
            {
                // Another of its group when this one went to someone else.
                var x = taken.Contains(x0.Key) ? free.FirstOrDefault(p => p.Group == x0.Group && !string.IsNullOrEmpty(p.Group) && !taken.Contains(p.Key)) : x0;
                if (x == null || !Fresh(c, x)) continue;
                var g = Gain(input, c, x, out float before, out float warm, out _);
                if (!g.HasValue) continue;
                Make(c, x, g.Value, before, warm, null);
            }

            // 5. Trades of armour between two colonists.
            result.AddRange(Trades(input, people, plans));

            // 6. What their outfit doesn't allow yet, one each, said as such: a piece that fills a need of theirs nothing allowed
            // fills as well (warmth still lacking, or more of it than the allowed pieces it would take the place of), or a
            // big upgrade (twice the threshold) where nothing allowed is suggested in its place.
            foreach (var c in people)
            {
                var gaps = Gaps(input, c);
                WearSuggestion best = null;
                float bestRank = float.MinValue;
                foreach (var x in Heads(free, new HashSet<string>()))
                {
                    var o = input.Option(c, x);
                    if (o == null || o.Outfit == null || o.Cant != null) continue;
                    var conflicts = plans[c.Key].Where(s => !Fits(input.Piece(s.Piece), x) || s.Instead.Any(o.Replaces.Contains)).ToList();
                    var g = Gain(input, c, x, out float before, out float warm, out float cool, true);
                    if (!g.HasValue) continue;
                    WearGap? fills = null;
                    foreach (var (gap, worn) in gaps)
                    {
                        if (gap == WearGap.Warmth || gap == WearGap.Cooling)
                        {
                            float shortBy = gap == WearGap.Warmth ? c.ColdShort : c.HeatShort;
                            float given = plans[c.Key].Where(s => s.Gap == gap && !conflicts.Contains(s)).Sum(s => s.Degrees);
                            float lacking = shortBy - given;
                            float deg = gap == WearGap.Warmth ? warm : cool;
                            if (lacking < ShortAt) continue;
                            if (deg <= conflicts.Where(s => s.Gap == gap).Sum(s => s.Degrees) + 1f) continue;
                            if (!Fills(input, c, x, gap, worn, g.Value, warm, cool, lacking)) continue;
                        }
                        else
                        {
                            if (conflicts.Count > 0) continue;
                            if (gap == WearGap.Worn ? plans[c.Key].Any(s => s.Instead.Contains(worn)) : plans[c.Key].Any(s => s.Gap == gap)) continue;
                            if (!Fills(input, c, x, gap, worn, g.Value, warm, cool, 0f)) continue;
                        }
                        fills = gap;
                        break;
                    }
                    if (fills == null && (conflicts.Count > 0 || o.Replaces.Count == 0 || g.Value < 2f * Threshold - 1e-4f)) continue;
                    float rank = (fills != null ? 100f : 0f) + g.Value;
                    if (best != null && rank <= bestRank) continue;
                    bestRank = rank;
                    best = new WearSuggestion
                    {
                        Kind = WearKind.Outfit, Colonist = c.Key, Piece = x.Key, Instead = new List<string>(o.Replaces), Gain = g.Value, Rating = o.Rating, Before = before,
                        Gap = fills, Degrees = fills == WearGap.Cooling ? cool : warm, Outfit = o.Outfit, OutfitFixable = o.OutfitFixable
                    };
                }
                if (best != null) result.Add(best);
            }

            foreach (var s in result) s.Why = Reasons(input, s);
            return result;
        }

        /// <summary>The first piece not yet taken of each group: alike pieces are weighed once.</summary>
        internal static IEnumerable<WearPiece> Heads(List<WearPiece> free, HashSet<string> taken)
        {
            var seen = new HashSet<string>();
            foreach (var p in free)
                if (!taken.Contains(p.Key) && seen.Add(string.IsNullOrEmpty(p.Group) ? "#" + p.Key : p.Group))
                    yield return p;
        }

        /// <summary>What a colonist's gain is weighed by in the colony's choice: armour by how much they fight (at least <see cref="WeaponPickMath.FightFloor"/>), the rest in full.</summary>
        public static float Weight(WearColonist c, WearPiece x) => x.Armour ? Math.Max(WeaponPickMath.FightFloor, Math.Min(1f, c.Fights)) : 1f;

        private static (WearPiece x, float gain, float before, float deg) Best(WearInput input, WearColonist c, List<WearPiece> free, HashSet<string> taken,
            Func<WearColonist, WearPiece, bool> fresh, Func<WearPiece, float, float, float, bool> fills)
        {
            (WearPiece x, float gain, float before, float deg) best = (null, 0f, 0f, 0f);
            foreach (var x in Heads(free, taken))
            {
                if (!fresh(c, x)) continue;
                var g = Gain(input, c, x, out float before, out float warm, out float cool);
                if (!g.HasValue || !fills(x, g.Value, warm, cool)) continue;
                if (best.x == null || g.Value > best.gain + 1e-4f) best = (x, g.Value, before, warm);
            }
            return best;
        }

        /// <summary>The trades' two weighed gains, as the weapons weigh them (WeaponPickMath.SwapEach and the rest).</summary>
        public static bool TradeWorth(float ga, float gb, float fa, float fb, out float worth)
        {
            worth = ga * Math.Max(WeaponPickMath.FightFloor, fa) + gb * Math.Max(WeaponPickMath.FightFloor, fb);
            if (ga >= WeaponPickMath.SwapEach && gb >= WeaponPickMath.SwapEach && ga + gb >= WeaponPickMath.SwapTotal) return true;
            return Math.Max(ga, gb) >= WeaponPickMath.SwapGainer && worth >= WeaponPickMath.SwapColony;
        }

        /// <summary>
        /// Trades of armour between two colonists (body armour for body armour, a helmet for a helmet), as the weapons trade:
        /// better for both (each 0.3, together 0.8), or one gains a point and the colony 0.8 by how much each fights. Each
        /// takes the other's in place of their own only, both allowed by their outfits, neither locked nor kept for a rule;
        /// none for someone already given a piece of that role; each colonist in one trade; the most worth first.
        /// </summary>
        private static List<WearSuggestion> Trades(WearInput input, List<WearColonist> people, Dictionary<string, List<WearSuggestion>> plans)
        {
            var list = new List<(WearColonist a, WearColonist b, WearPiece xa, WearPiece xb, float ga, float gb, float worth)>();
            for (int i = 0; i < people.Count; i++)
                for (int j = i + 1; j < people.Count; j++)
                {
                    var a = people[i];
                    var b = people[j];
                    foreach (var role in new[] { "body armour", "helmet" })
                    {
                        var xa = a.Wears.Select(input.Piece).FirstOrDefault(p => p != null && p.Role == role);
                        var xb = b.Wears.Select(input.Piece).FirstOrDefault(p => p != null && p.Role == role);
                        if (xa == null || xb == null || xa.Locked || xb.Locked || xa.Rule != null || xb.Rule != null) continue;
                        if (plans[a.Key].Any(s => input.Piece(s.Piece)?.Role == role) || plans[b.Key].Any(s => input.Piece(s.Piece)?.Role == role)) continue;
                        var oa = input.Option(a, xb);
                        var ob = input.Option(b, xa);
                        if (oa == null || ob == null || oa.Cant != null || ob.Cant != null || oa.Outfit != null || ob.Outfit != null) continue;
                        if (oa.Replaces.Any(k => k != xa.Key) || ob.Replaces.Any(k => k != xb.Key)) continue;
                        float? na = Score(input, a, xb), ca = Score(input, a, xa), nb = Score(input, b, xa), cb = Score(input, b, xb);
                        if (!na.HasValue || !ca.HasValue || !nb.HasValue || !cb.HasValue) continue;
                        float ga = na.Value - ca.Value, gb = nb.Value - cb.Value;
                        if (!TradeWorth(ga, gb, a.Fights, b.Fights, out float worth)) continue;
                        list.Add((a, b, xa, xb, ga, gb, worth));
                    }
                }
            var done = new HashSet<string>();
            var result = new List<WearSuggestion>();
            foreach (var t in list.OrderByDescending(t => t.worth).ThenBy(t => t.a.Key, StringComparer.Ordinal))
            {
                if (done.Contains(t.a.Key) || done.Contains(t.b.Key)) continue;
                done.Add(t.a.Key);
                done.Add(t.b.Key);
                // The one who gains more is the one the suggestion is for.
                bool aFirst = t.ga >= t.gb;
                var me = aFirst ? t.a : t.b;
                var other = aFirst ? t.b : t.a;
                var theirs = aFirst ? t.xb : t.xa;
                var mine = aFirst ? t.xa : t.xb;
                result.Add(new WearSuggestion
                {
                    Kind = WearKind.Trade, Colonist = me.Key, Other = other.Key, Piece = theirs.Key, Instead = new List<string> { mine.Key },
                    Gain = aFirst ? t.ga : t.gb, OtherGain = aFirst ? t.gb : t.ga, Worth = t.worth,
                    Rating = input.Option(me, theirs).Rating, Before = input.Option(me, mine)?.Rating ?? mine.Overall
                });
            }
            return result;
        }

        // ---------------------------------------------------------------- keepsakes

        /// <summary>The keepsake a colonist loses by taking off <paramref name="losing"/> for <paramref name="getting"/>: an heirloom is that very piece; a lucky item's mood comes with any of its kind.</summary>
        public static WearKeepsake LostKeepsake(WearColonist c, WearPiece losing, WearPiece getting)
        {
            if (c == null || losing == null) return null;
            foreach (var k in c.Keepsakes)
            {
                if (k == null || k.Piece != losing.Key) continue;
                if (k.Kind == KeepsakeKind.Lucky && getting != null && getting.Def == k.Def) continue;
                return k;
            }
            return null;
        }

        private static WearReason KeepsakeReason(WearColonist c, WearKeepsake k) => new WearReason
        {
            Kind = k.Kind == KeepsakeKind.Lucky ? WearWhy.Lucky : WearWhy.Heirloom, Text = c.Key, Extra = k.Label, Of = k.Of ?? "", Value = k.MoodWith,
            Level = (int)Math.Round(k.MoodWithout), Cost = true
        };

        /// <summary>Whether a suggestion gives up a keepsake of this colonist's (its line says so as a cost; they never ask for it themselves).</summary>
        public static bool GivesUpKeepsake(WearSuggestion s, string colonist) =>
            s?.Why != null && s.Why.Any(r => r.Cost && (r.Kind == WearWhy.Lucky || r.Kind == WearWhy.Heirloom) && r.Text == colonist);

        // ---------------------------------------------------------------- why

        /// <summary>
        /// A suggestion's reasons, two at most and the strongest first, then its costs: the gap it fills (the degrees they
        /// lack and it adds, the armour or helmet a fighter lacks, the worn-out or dead man's piece it replaces), a rule of
        /// theirs it meets, the raids it stands up to, their work it speeds, simply better, a good piece where nothing is worn;
        /// where it comes from (their pack, another's spare); a trade's other. Costs: a keepsake taken off (theirs, or in a
        /// trade the other's), work it slows, the other gender's mood.
        /// </summary>
        public static List<WearReason> Reasons(WearInput input, WearSuggestion s)
        {
            var c = input.Colonist(s.Colonist);
            var x = input.Piece(s.Piece);
            var o = input.Option(c, x);
            if (c == null || x == null || o == null) return new List<WearReason>();
            var list = new List<(WearReason r, float w)>();
            void Add(WearWhy kind, float w, string text = "", float value = 0f, int level = 0) => list.Add((new WearReason { Kind = kind, Text = text ?? "", Value = value, Level = level }, w));
            var instead = s.Instead.Select(input.Piece).Where(p => p != null).ToList();
            switch (s.Gap)
            {
                case WearGap.Warmth: Add(WearWhy.Warmth, 2f, "", s.Degrees, (int)Math.Round(c.ColdShort)); break;
                case WearGap.Cooling: Add(WearWhy.Cooling, 2f, "", s.Degrees, (int)Math.Round(c.HeatShort)); break;
                case WearGap.Armour: Add(WearWhy.Armour, 2f); break;
                case WearGap.Helmet: Add(WearWhy.Helmet, 2f); break;
            }
            foreach (var y in instead.Where(y => y.Worn))
                Add(y.FromDead && y.Stage < 0 ? WearWhy.Dead : WearWhy.Worn, s.Gap == WearGap.Worn ? 1.9f : 0.9f, y.Label, 0f, y.Stage);
            if (o.Unmet != null) Add(o.Unmet == "faith" ? WearWhy.Faith : o.Unmet == "title" ? WearWhy.Title : WearWhy.Role, 1.2f);
            if (x.Armour && c.CanFight && c.Fights > WeaponPickMath.RarelyFightsAt + 1e-4f)
            {
                float mechs = x.Situation("mechanoids") - (instead.FirstOrDefault(y => y.Armour)?.Situation("mechanoids") ?? Nothing);
                float melee = x.Situation("melee") - (instead.FirstOrDefault(y => y.Armour)?.Situation("melee") ?? Nothing);
                if (input.MechShare >= 0.1f && mechs >= 1f) Add(WearWhy.Mechs, input.MechShare * mechs);
                if (input.MeleeShare >= 0.1f && melee >= 1f) Add(WearWhy.Melee, input.MeleeShare * melee * 0.8f);
            }
            if (o.Work >= WorkAt && c.Work != null) Add(WearWhy.Work, 0.5f, c.Work, o.Work);
            switch (s.Kind)
            {
                case WearKind.Pack: Add(WearWhy.Pack, 0.3f); break;
                case WearKind.Spare: Add(WearWhy.Spare, 0.3f, s.Other ?? ""); break;
                case WearKind.Trade: Add(WearWhy.Trade, 0.05f, s.Other ?? "", s.OtherGain); break;
            }
            var reasons = list.OrderByDescending(r => r.w).ThenBy(r => r.r.Kind).Select(r => r.r).ToList();
            var shown = new List<WearReason>();
            bool even = s.Kind != WearKind.Trade || (s.Gain >= 0f && s.OtherGain >= 0f);
            foreach (var r in reasons)
            {
                if (shown.Count >= 2) break;
                if (r.Kind == WearWhy.Trade) continue;
                shown.Add(r);
            }
            if (shown.Count == 0 || (shown.Count == 1 && (shown[0].Kind == WearWhy.Pack || shown[0].Kind == WearWhy.Spare)))
            {
                if (instead.Count == 0) shown.Insert(0, new WearReason { Kind = WearWhy.AddOn, Value = s.Rating });
                else shown.Insert(0, new WearReason { Kind = WearWhy.Better, Value = s.Rating, Level = (int)Math.Round(s.Before * 10f) });
            }
            if (s.Kind == WearKind.Trade)
            {
                var other = input.Colonist(s.Other);
                if (even) shown.Add(reasons.First(r => r.Kind == WearWhy.Trade));
                else
                {
                    shown.Add(new WearReason { Kind = WearWhy.TradeColony, Value = s.Worth });
                    var loser = s.Gain < 0f ? c : other;
                    if (loser != null && loser.Fights <= WeaponPickMath.RarelyFightsAt + 1e-4f) shown.Add(new WearReason { Kind = WearWhy.RarelyFights, Text = loser.Key });
                }
            }
            // Costs, always said: a keepsake taken off (theirs; in a trade the other's too), work it slows, the other gender's mood.
            foreach (var y in instead)
                if (LostKeepsake(c, y, x) is WearKeepsake k)
                    shown.Add(KeepsakeReason(c, k));
            if (s.Kind == WearKind.Trade && input.Colonist(s.Other) is WearColonist them && LostKeepsake(them, x, instead.FirstOrDefault()) is WearKeepsake theirs)
                shown.Add(KeepsakeReason(them, theirs));
            if (o.Work <= -WorkAt && c.Work != null) shown.Add(new WearReason { Kind = WearWhy.Slower, Text = c.Work, Value = o.Work, Cost = true });
            if (o.GenderMood < -0.01f) shown.Add(new WearReason { Kind = WearWhy.Gender, Value = o.GenderMood, Cost = true });
            return shown;
        }

        /// <summary>The line's tag for a suggestion's strongest reason, for the colonist's own words (Rimfolk_Pool_ApparelAsk).</summary>
        public static string AskTag(WearSuggestion s)
        {
            if (s == null) return "better";
            switch (s.Gap)
            {
                case WearGap.Warmth: return "warmth";
                case WearGap.Cooling: return "cooling";
                case WearGap.Armour: return "armour";
                case WearGap.Helmet: return "helmet";
            }
            var r = s.Reasons.FirstOrDefault();
            switch (r?.Kind)
            {
                case WearWhy.Worn: return "worn";
                case WearWhy.Dead: return "dead";
                case WearWhy.Faith: return "faith";
                case WearWhy.Title: return "title";
                case WearWhy.Role: return "role";
                case WearWhy.Work: return "work";
                case WearWhy.Mechs: return "mechanoids";
                case WearWhy.Melee: return "crowds";
                default: return "better";
            }
        }

        /// <summary>A gain as the lines show it: "+1.4", a loss "-0.5".</summary>
        public static string GainText(float g) => WeaponPickMath.GainText(g);

        /// <summary>Degrees as the lines say them: "18".</summary>
        public static string DegreesText(float d) => Math.Round(Math.Abs(d)).ToString("0", CultureInfo.InvariantCulture);

        // ---------------------------------------------------------------- who should wear this

        /// <summary>
        /// Who a piece suits best: each colonist who could wear it, its score for them, its gain over what it would replace
        /// (or over nothing), and whether it fills a gap of theirs; those it fills a gap for first, then the biggest gains,
        /// at most <paramref name="max"/>. Their outfit is set aside here: the card says who it suits.
        /// </summary>
        public static List<(WearColonist c, float rating, float gain, WearGap? gap)> WhoShouldWear(WearInput input, WearPiece x, int max = 3)
        {
            var list = new List<(WearColonist, float, float, WearGap?)>();
            if (input == null || x == null) return list;
            foreach (var c in input.Colonists)
            {
                var o = input.Option(c, x);
                if (o == null || o.Cant != null) continue;
                if (c.Wears.Contains(x.Key))
                {
                    list.Add((c, o.Rating, 0f, null));
                    continue;
                }
                var g = Gain(input, c, x, out _, out float warm, out float cool, true);
                if (!g.HasValue) continue;
                WearGap? fills = null;
                foreach (var (gap, worn) in Gaps(input, c))
                {
                    float shortNow = gap == WearGap.Warmth ? c.ColdShort : gap == WearGap.Cooling ? c.HeatShort : 0f;
                    if (Fills(input, c, x, gap, worn, g.Value, warm, cool, shortNow))
                    {
                        fills = gap;
                        break;
                    }
                }
                list.Add((c, o.Rating, g.Value, fills));
            }
            return list.OrderByDescending(t => t.Item4.HasValue).ThenByDescending(t => t.Item3).ThenByDescending(t => t.Item2).ThenBy(t => t.Item1.Key, StringComparer.Ordinal).Take(max).ToList();
        }
    }

    /// <summary>
    /// When a colonist asks for a better piece of clothing (docs/design/apparel.md, point 6), without game types: only a gap
    /// of theirs or a clearly better piece (<see cref="ApparelPickMath.AskThreshold"/>), never a trade; at most once in six
    /// days a colonist and once a day in the colony, the same piece not again for fifteen days, never while their
    /// suggestions are set aside; and then only now and then (each hourly look in their free time), twice as often for
    /// warmth while it is colder outside than they can bear.
    /// </summary>
    public static class ApparelAskMath
    {
        public const int DayTicks = 60000;
        public const int EachDays = 6, ColonyHours = 24, SamePieceDays = 15, NotNowDays = 10;
        /// <summary>The inbox keeps the ask this long while the suggestion stands.</summary>
        public const int ShowDays = 3;
        /// <summary>The chance at each hourly look in their free time once the waits are over; for warmth in the cold, twice it.</summary>
        public const float Chance = 0.1f;

        public static bool Askable(WearSuggestion s) =>
            s != null && s.Kind != WearKind.Trade && (s.Gap.HasValue || s.Gain >= ApparelPickMath.AskThreshold - 1e-4f);

        public static float ChanceFor(WearSuggestion s, float coldNow) => s?.Gap == WearGap.Warmth && coldNow >= ApparelPickMath.ShortAt ? 2f * Chance : Chance;

        /// <summary>Whether they may ask now: their own wait, the colony's, the same piece's, and the player's "not now" all over.</summary>
        public static bool MayAsk(int now, int lastByThem, int lastInColony, int lastPiece, int piece, int notNowUntil)
        {
            if (notNowUntil > now) return false;
            if (lastByThem >= 0 && now - lastByThem < EachDays * DayTicks) return false;
            if (lastInColony >= 0 && now - lastInColony < ColonyHours * 2500) return false;
            if (lastPiece == piece && lastByThem >= 0 && now - lastByThem < SamePieceDays * DayTicks) return false;
            return true;
        }

        public static bool Shows(int now, int asked, int notNowUntil) => asked >= 0 && now - asked < ShowDays * DayTicks && notNowUntil <= now;
    }
}
