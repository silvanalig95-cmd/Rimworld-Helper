using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// What the item card reads of a def to know its kind (Items/ItemCardLive.cs from the game, by hand in the tests). Step 1
    /// reads food, drink, drugs and medicine; step 2 materials and art; step 3 adds its own flags and kinds here, after these.
    /// </summary>
    public sealed class ItemSort
    {
        public bool Medicine, Drug, Fluid, Feeds;
        /// <summary>The game's art comp, and not a grave whose art shows only once it is full (or one that is full).</summary>
        public bool Art;
        /// <summary>Things are made of it (the game's stuff).</summary>
        public bool Stuff;
        /// <summary>Filed by the game as a raw or manufactured resource, or a chunk (stone, slag).</summary>
        public bool Resource;
        /// <summary>Something to use, read or install rather than build with (a book, a techprint, a trainer, a genepack, a body part, a shell): the rest, step 3.</summary>
        public bool ToUse;
    }

    /// <summary>
    /// What a material does to what is made of it, from the game's own stats on it (its stuff powers, its stat factors and
    /// offsets, its damage multipliers), with its worth and weight a unit. One for the material, one for each it is
    /// compared with.
    /// </summary>
    public sealed class StuffNumbers
    {
        public string Def = "", Label = "";
        /// <summary>Metal, wood, stone or a mod's like them; soft is fabric and leather.</summary>
        public bool Hard = true;
        /// <summary>Its stuff categories' names (Metallic, Woody, Stony, Fabric, Leathery, a mod's).</summary>
        public List<string> Categories = new List<string>();
        /// <summary>Its stuff powers: armour against sharp, blunt and heat (a share), insulation against cold and heat (degrees).</summary>
        public float ArmorSharp, ArmorBlunt, ArmorHeat, InsulationCold, InsulationHeat;
        /// <summary>It has stuff powers at all: apparel is made of it.</summary>
        public bool Apparel;
        /// <summary>Factors on what is made of it: hit points, beauty, flammability, work to make and to build; and its beauty offset.</summary>
        public float HitPoints = 1f, Beauty = 1f, BeautyOffset, Flammability = 1f, WorkToMake = 1f, WorkToBuild = 1f;
        /// <summary>Melee weapons made of it: the damage factors for sharp and blunt blows; Weapons when it has them.</summary>
        public float SharpDamage = 1f, BluntDamage = 1f;
        public bool Weapons;
        /// <summary>A unit's worth in silver and weight in kilograms.</summary>
        public float Value, Mass;
    }

    /// <summary>
    /// One row of the comparison: what is compared (its id; the card's words are the key CC_ItemCard_Stuff_ and the id),
    /// each column's value in words, and how the material stands (1 better than all, -1 worse than all, 0 between).
    /// </summary>
    public sealed class StuffRow
    {
        public string Id = "";
        public List<string> Values = new List<string>();
        public int Tone;
    }

    /// <summary>What the item card knows of a material besides its stuff numbers: its uses here, the colony's stock and demand, where it comes from.</summary>
    public sealed class MaterialFacts
    {
        /// <summary>Its stuff numbers (null when things aren't made of it), and those it is compared with.</summary>
        public StuffNumbers Stuff;
        public List<StuffNumbers> Refs = new List<StuffNumbers>();
        /// <summary>A word for its kind: metal, wood, stone, fabric, leather, wool, chunk or material.</summary>
        public string Noun = "material";
        /// <summary>It has the most hit points of every stuff of its first category in the game and the player's mods.</summary>
        public bool SturdiestOfKind;
        /// <summary>Rimfolk's roles (Things/ThingRoles.cs): a precious material, a gift between peoples; silver is the money.</summary>
        public bool Precious, Gift, Money;
        /// <summary>It explodes when fire reaches it (the game's explosive comp, chemfuel); of a person (human leather).</summary>
        public bool Explosive, OfPerson;
        /// <summary>A chunk: stone or slag.</summary>
        public bool Chunk;
        /// <summary>What the colony can make of it now (its research done) by kind, and what more research would open (and the first such research).</summary>
        public int Buildings, Apparel, Weapons, Others, Later;
        public string LaterResearch = "";
        /// <summary>A recipe makes it whose research is done, where (a bench's label) and whether one is built; the research it waits on otherwise.</summary>
        public bool Makeable, BenchBuilt;
        public string Bench = "", MakeResearch = "";
        /// <summary>A raw material a recipe turns into something else (chunks into blocks, slag into steel): what, where, whether such a bench is built.</summary>
        public string RawInto = "", RawBench = "";
        public bool RawBenchBuilt;
        /// <summary>The colony: how many it holds (on the map, not forbidden, and in packs; -1 unknown), and what waits on it.</summary>
        public int Held = -1;
        /// <summary>What the blueprints and frames still need of it; finite bills' needs; bills that can't start for want of it; bills that take it at all.</summary>
        public int BuildWants, BillWants, BillsStarved, Bills;
        /// <summary>The trade advice's reserve for it (TradeAdviceMath): counted at all, the need and what is kept.</summary>
        public bool Counted;
        public int Need, Keep;
        /// <summary>Components: the colony's machines that break down, and the game's mean days between one machine's breakdowns.</summary>
        public int Machines;
        public float BreakdownDays;
        /// <summary>Fuel: some building can burn it; what at home burns it all the time (a few words), and how much a day; what burns it only while used; the colony's record of use a day.</summary>
        public bool FuelAnywhere;
        public int Burners, BurnersWhileUsed;
        public string BurnerWords = "";
        public float BurnADay, UseADay;
    }

    /// <summary>What the item card knows of a work of art: the piece, its story, where it stands and what it does there.</summary>
    public sealed class ArtFacts
    {
        /// <summary>Its quality's word ("excellent"; empty without one) and index (0 awful to 6 legendary; -1 none).</summary>
        public string Quality = "";
        public int QualityIndex = -1;
        /// <summary>Its beauty, worth and flammability by the game's stats.</summary>
        public float Beauty, Worth, Flammability;
        /// <summary>A tale is depicted: the art's title, the artist, the tale's own name.</summary>
        public bool Story;
        public string Title = "", Artist = "", Tale = "";
        /// <summary>Small art a people carries and gives (ThingRole.SmallArt).</summary>
        public bool SmallArt;
        /// <summary>Placed on the map, and indoors in a proper room (its role's word and whether the role's impressiveness gives a mood).</summary>
        public bool Placed, Indoors;
        public string Room = "", Role = "";
        public bool RoomMatters;
        /// <summary>The room's beauty and impressiveness with it (the game's own) and without it (the game's sums with its beauty and worth taken out); the impressiveness stages' words and indices.</summary>
        public float BeautyWith, BeautyWithout, ImpressWith, ImpressWithout;
        public string StageWith = "", StageWithout = "";
        public int StageIndexWith, StageIndexWithout;
        /// <summary>Not placed, or outdoors: the colony's room it would lift most, its impressiveness now and with it, and their stages.</summary>
        public string BestRoom = "";
        public float BestNow, BestWith;
        public string BestStageNow = "", BestStageWith = "";
        public int BestIndexNow, BestIndexWith;
        /// <summary>A noble's throne room or bedroom it stands in: whose, which room, the impressiveness their title needs.</summary>
        public string Noble = "", NobleRoom = "";
        public int NobleNeeds;
        /// <summary>Faiths at home whose precepts or memes care for art (named), and a meditation focus for psycasters who meditate on art (how many at home).</summary>
        public string Faiths = "";
        public bool MeditationFocus;
        public int Meditators;
    }

    /// <summary>A room's numbers as the game keeps them (RoomStatDefOf's wealth, beauty, space, cleanliness, impressiveness) and its size.</summary>
    public struct RoomNumbers
    {
        public float Wealth, Beauty, Space, Cleanliness, Impressiveness;
        public int Cells;
    }

    /// <summary>
    /// The item card's rules for materials and art (step 2), pure: the kind from <see cref="ItemSort"/>, the comparison of
    /// stuff against steel and wood or the colony's most held of its kind, a room's impressiveness with and without a
    /// piece of art by the game's own sums, the tags in the shared six categories and the line in plain words.
    /// </summary>
    public static partial class ItemCardMath
    {
        // ---------------- The kind ----------------

        /// <summary>
        /// The card's kind: step 1's first (medicine, drugs, drinks, food), then art, then materials (stuff, or a raw or
        /// manufactured resource or a chunk) that are not things to use (step 3's). None: the game's card opens.
        /// </summary>
        public static ItemKind KindOf(ItemSort s)
        {
            if (s == null) return ItemKind.None;
            var first = KindOf(s.Medicine, s.Drug, s.Fluid, s.Feeds);
            if (first != ItemKind.None) return first;
            if (s.Art) return ItemKind.Art;
            if ((s.Stuff || s.Resource) && !s.ToUse) return ItemKind.Material;
            return ItemKind.None;
        }

        // ---------------- Materials: thresholds ----------------

        /// <summary>Strong armour: a sharp armour power above steel's 0.9 (devilstrand, plasteel, hyperweave).</summary>
        public const float StrongArmour = 1f;
        /// <summary>Sturdy: things made of it have half again steel's hit points or more; soft, 70% or less.</summary>
        public const float SturdyAt = 1.5f, SoftAt = 0.7f;
        /// <summary>Fireproof at or under this flammability factor; burns at or over this one.</summary>
        public const float FireproofAt = 0.05f, BurnsAt = 1f;
        /// <summary>Beautiful: twice the beauty of what is made of steel, or more.</summary>
        public const float BeautifulAt = 2f;
        /// <summary>Warm and keeps cool: 20 degrees of insulation or more (cloth gives 18).</summary>
        public const float WarmAt = 20f;
        /// <summary>Light: a unit weighs 60% of steel's or less; heavy: 160% or more (in a hard material, not a precious one).</summary>
        public const float LightShare = 0.6f, HeavyShare = 1.6f;
        /// <summary>Melee weapons: sharp blows a tenth harder than steel's or more; blunt a fifth.</summary>
        public const float SharpBladesAt = 1.1f, HardBlowsAt = 1.2f;
        /// <summary>Slow to work: half again the work to build or make, or more.</summary>
        public const float SlowWorkAt = 1.5f;
        /// <summary>Steel's own unit weight, when the steel def can't be read.</summary>
        public const float SteelMass = 0.5f;

        // ---------------- Materials: small sums ----------------

        /// <summary>
        /// The materials a stuff is compared with: the colony's most held of its kind (not itself), then the base game's
        /// usual (steel and wood for a hard material, then granite blocks and plasteel; cloth and plainleather for a soft
        /// one, then devilstrand), two at most, never itself.
        /// </summary>
        public static List<string> RefsFor(string me, bool hard, string mostHeldOfKind)
        {
            var order = new List<string> { mostHeldOfKind };
            order.AddRange(hard ? new[] { "Steel", "WoodLog", "BlocksGranite", "Plasteel" } : new[] { "Cloth", "Leather_Plain", "DevilstrandCloth" });
            var refs = new List<string>();
            foreach (var d in order)
            {
                if (string.IsNullOrEmpty(d) || d == me || refs.Contains(d)) continue;
                refs.Add(d);
                if (refs.Count == 2) break;
            }
            return refs;
        }

        /// <summary>A hard material: not fabric or leather.</summary>
        public static bool IsHard(IList<string> categories) => categories == null || !categories.Any(c => c == "Fabric" || c == "Leathery");

        /// <summary>A word for a material's kind, by its stuff categories and the game's filing: metal, wood, stone, fabric, leather, wool; a chunk; else "material".</summary>
        public static string NounFor(IList<string> stuffCategories, IList<string> thingCategories, bool chunk)
        {
            if (chunk) return "chunk";
            if (thingCategories != null && thingCategories.Contains("Wools")) return "wool";
            var c = stuffCategories ?? new List<string>();
            if (c.Contains("Metallic")) return "metal";
            if (c.Contains("Woody")) return "wood";
            if (c.Contains("Stony")) return "stone";
            if (c.Contains("Leathery") && !c.Contains("Fabric")) return "leather";
            if (c.Contains("Fabric")) return "fabric";
            return "material";
        }

        /// <summary>A unit's weight against steel's.</summary>
        public static float MassShare(StuffNumbers s, float steelMass) => s == null || steelMass <= 0f ? 1f : s.Mass / steelMass;

        /// <summary>
        /// What a thing made of it is worth, for the order of "what the colony can make" (best first): its own worth where
        /// its def sets one, else the game's sum for an unpriced def (StatWorker_MarketValue: the stuff and the other
        /// ingredients at their worth, and the work at its rate).
        /// </summary>
        public static float MadeWorth(float ownValue, int stuffCount, float stuffValue, float costValue, float work, float valuePerWork) =>
            ownValue > 0f ? ownValue : Math.Max(0, stuffCount) * Math.Max(0f, stuffValue) + Math.Max(0f, costValue) + Math.Max(0f, work) * Math.Max(0f, valuePerWork);

        /// <summary>Components a season (15 days) the colony's machines use in repairs: one a breakdown, each machine breaking down once in so many days on average.</summary>
        public static float RepairsASeason(int machines, float breakdownDays) => machines <= 0 || breakdownDays <= 0f ? 0f : machines * TradeAdviceMath.SeasonDays / breakdownDays;

        /// <summary>How many days a stock lasts at so much a day (-1 when nothing burns it).</summary>
        public static float DaysOf(int held, float aDay) => aDay <= 0.0001f ? -1f : Math.Max(0, held) / aDay;

        /// <summary>The material's use a day: the colony's record where it has one, else what burns it all the time.</summary>
        public static float UseADay(MaterialFacts m) => m == null ? 0f : m.UseADay > 0f ? m.UseADay : m.BurnADay;

        /// <summary>
        /// What the colony is short of it: the blueprints' and finite bills' needs beyond what it holds, or the trade advice's
        /// need beyond it where the colony keeps a stock of it already (holds some, uses some by its record, or mends machines
        /// with it), as the advice itself never starts a new stock.
        /// </summary>
        public static int ShortBy(MaterialFacts m)
        {
            if (m == null || m.Held < 0) return 0;
            int plans = m.BuildWants + m.BillWants;
            bool keeps = m.Counted && (m.Held > 0 || m.UseADay > 0f || m.Machines > 0);
            int need = Math.Max(plans, keeps ? m.Need : 0);
            return Math.Max(0, need - m.Held);
        }

        /// <summary>
        /// Needed now: the blueprints and bills want more than the colony holds, a bill can't start for want of it, or the
        /// colony uses it (by its record, or to mend its machines) and holds less than the trade advice's need. A count under
        /// the advice's floor alone is said on the card, not tagged.
        /// </summary>
        public static bool NeededNow(MaterialFacts m) =>
            m != null && m.Held >= 0 && (m.BuildWants + m.BillWants > m.Held || m.BillsStarved > 0 || m.Counted && (m.UseADay > 0f || m.Machines > 0) && m.Held < m.Need);

        /// <summary>More than the trade advice keeps (the need twice over and the plans' share on top).</summary>
        public static bool Spare(MaterialFacts m) => m != null && m.Held >= 0 && m.Counted && m.Held > m.Keep + m.BuildWants + m.BillWants && ShortBy(m) == 0;

        /// <summary>The comparison's rows: armour and insulation for apparel stuff, hit points, beauty, flammability, melee damage for weapon stuff, work, worth and weight; the material first, then each it is compared with.</summary>
        public static List<StuffRow> StuffTable(StuffNumbers me, IList<StuffNumbers> refs)
        {
            var rows = new List<StuffRow>();
            if (me == null) return rows;
            var cols = new List<StuffNumbers> { me };
            cols.AddRange((refs ?? new List<StuffNumbers>()).Where(r => r != null));
            void Row(string id, Func<StuffNumbers, float> get, Func<float, string> words, int better)
            {
                var row = new StuffRow { Id = id };
                foreach (var c in cols) row.Values.Add(words(get(c)));
                float v = get(me);
                var others = cols.Skip(1).Select(get).ToList();
                if (better != 0 && others.Count > 0)
                {
                    if (others.All(o => better > 0 ? v > o + 0.0001f : v < o - 0.0001f)) row.Tone = 1;
                    else if (others.All(o => better > 0 ? v < o - 0.0001f : v > o + 0.0001f)) row.Tone = -1;
                }
                rows.Add(row);
            }
            if (me.Apparel)
            {
                Row("ArmorSharp", s => s.ArmorSharp, Share, 1);
                Row("ArmorBlunt", s => s.ArmorBlunt, Share, 1);
                Row("ArmorHeat", s => s.ArmorHeat, Share, 1);
                Row("InsulationCold", s => s.InsulationCold, Degrees, 1);
                Row("InsulationHeat", s => s.InsulationHeat, Degrees, 1);
            }
            Row("HitPoints", s => s.HitPoints, Times, 1);
            Row("Beauty", s => s.Beauty, Times, 1);
            if (cols.Any(c => c.BeautyOffset != 0f)) Row("BeautyAdded", s => s.BeautyOffset, Signed, 1);
            Row("Flammability", s => s.Flammability, Share, -1);
            if (me.Weapons)
            {
                Row("MeleeSharp", s => s.SharpDamage, Times, 1);
                Row("MeleeBlunt", s => s.BluntDamage, Times, 1);
            }
            Row("WorkToBuild", s => s.WorkToBuild, Times, -1);
            Row("WorkToMake", s => s.WorkToMake, Times, -1);
            Row("Worth", s => s.Value, Money, 0);
            Row("Weight", s => s.Mass, Kg, -1);
            return rows;
        }

        /// <summary>Every row id the comparison may give (each has its words in Rimfolk_ItemCard.xml).</summary>
        public static readonly string[] StuffRowIds =
            { "ArmorSharp", "ArmorBlunt", "ArmorHeat", "InsulationCold", "InsulationHeat", "HitPoints", "Beauty", "BeautyAdded", "Flammability", "MeleeSharp", "MeleeBlunt", "WorkToBuild", "WorkToMake", "Worth", "Weight" };

        // ---------------- Art: rooms ----------------

        /// <summary>The game's room beauty divisor by size (RoomStatWorker_Beauty's cell count curve in 1.6: 20 up to 5 cells, 40 at 40, the cells themselves beyond), when the game's own can't be read.</summary>
        public static float BeautyDivisor(int cells)
        {
            if (cells <= 5) return 20f;
            if (cells <= 40) return 20f + (cells - 5) * (20f / 35f);
            return cells;
        }

        /// <summary>The game's impressiveness factor of a share (RoomStatWorker_Impressiveness.GetFactor): itself under one, else one and its logarithm.</summary>
        public static float ImpressFactor(float x)
        {
            if (Math.Abs(x) < 1f) return x;
            return x > 0f ? 1f + (float)Math.Log(x) : -1f - (float)Math.Log(-x);
        }

        /// <summary>
        /// The game's impressiveness of a room (RoomStatWorker_Impressiveness): wealth over 1,500, beauty over 3, space over
        /// 125 and cleanliness each as a factor, their mean drawn a third of the way to the least, held near five times the
        /// space's factor, times 100.
        /// </summary>
        public static float Impressiveness(float wealth, float beauty, float space, float cleanliness)
        {
            float w = ImpressFactor(wealth / 1500f), b = ImpressFactor(beauty / 3f), s = ImpressFactor(space / 125f), c = ImpressFactor(1f + Math.Min(cleanliness, 0f) / 2.5f);
            float a = (w + b + s + c) / 4f;
            a = Lerp(a, Math.Min(w, Math.Min(b, Math.Min(s, c))), 0.35f);
            float cap = s * 5f;
            if (a > cap) a = Lerp(a, cap, 0.75f);
            return a * 100f;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>
        /// A room without a piece of art in it: its beauty less the piece's beauty over the room's divisor, its wealth less
        /// the piece's worth, space and cleanliness kept; the impressiveness the game's own less the difference the game's
        /// sums make (so the room's own number is never second-guessed).
        /// </summary>
        public static RoomNumbers Without(RoomNumbers room, float pieceBeauty, float pieceWorth, float divisor)
        {
            var r = room;
            float div = divisor > 0f ? divisor : BeautyDivisor(room.Cells);
            r.Beauty = room.Beauty - pieceBeauty / div;
            r.Wealth = Math.Max(0f, room.Wealth - pieceWorth);
            r.Impressiveness = room.Impressiveness - (Impressiveness(room.Wealth, room.Beauty, room.Space, room.Cleanliness) - Impressiveness(r.Wealth, r.Beauty, r.Space, r.Cleanliness));
            return r;
        }

        /// <summary>A room with a piece of art added (the same sums the other way).</summary>
        public static RoomNumbers With(RoomNumbers room, float pieceBeauty, float pieceWorth, float divisor)
        {
            var r = room;
            float div = divisor > 0f ? divisor : BeautyDivisor(room.Cells);
            r.Beauty = room.Beauty + pieceBeauty / div;
            r.Wealth = room.Wealth + Math.Max(0f, pieceWorth);
            r.Impressiveness = room.Impressiveness + (Impressiveness(r.Wealth, r.Beauty, r.Space, r.Cleanliness) - Impressiveness(room.Wealth, room.Beauty, room.Space, room.Cleanliness));
            return r;
        }

        /// <summary>A placed piece its room would be more impressive without (by half a point or more): poor work.</summary>
        public static bool BetterWithout(ArtFacts a) => a != null && a.Placed && a.Indoors && a.ImpressWithout > a.ImpressWith + 0.5f;

        /// <summary>The stage a score reaches among a stat's stages (each its least score, in order): the last it reaches, 0 below all.</summary>
        public static int StageIndex(float score, IList<float> minScores)
        {
            int at = 0;
            if (minScores == null) return 0;
            for (int i = 0; i < minScores.Count; i++)
                if (score >= minScores[i]) at = i;
            return at;
        }

        /// <summary>The base game's impressiveness stages (RoomStatDef Impressiveness), for the tests and when the def can't be read.</summary>
        public static readonly float[] ImpressStages = { -9999f, 20f, 30f, 40f, 50f, 65f, 85f, 120f, 170f, 240f };

        public static readonly string[] ImpressWords =
            { "awful", "dull", "mediocre", "decent", "slightly impressive", "somewhat impressive", "very impressive", "extremely impressive", "unbelievably impressive", "wondrously impressive" };

        /// <summary>Room roles whose impressiveness gives a mood (the game's thoughts on bedrooms, barracks, dining and recreation rooms, throne rooms, hospitals and prison cells).</summary>
        public static readonly string[] RolesThatMatter = { "Bedroom", "Barracks", "DiningRoom", "RecRoom", "ThroneRoom", "Hospital", "PrisonCell", "PrisonBarracks" };

        public static bool RoleMatters(string roleDefName) => roleDefName != null && RolesThatMatter.Contains(roleDefName);

        /// <summary>A precept, meme or issue about art or beauty, by its name's words (AM_Art, AM_ArtQuality, AM_Artist, a mod's Beauty; never Heart or Party).</summary>
        public static bool ArtRelated(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return false;
            foreach (var w in Words(defName))
            {
                string l = w.ToLowerInvariant();
                if (l == "art" || l == "arts" || l == "artist" || l == "artists" || l == "artistic" || l == "beauty" || l == "beautiful" || l == "sculpture" || l == "sculptures") return true;
            }
            return false;
        }

        /// <summary>A def name's words: split at underscores and where a capital follows a small letter.</summary>
        public static List<string> Words(string name)
        {
            var words = new List<string>();
            var cur = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char ch = name[i];
                bool split = ch == '_' || ch == ' ' || ch == '-' || i > 0 && char.IsUpper(ch) && char.IsLower(name[i - 1]);
                if (split && cur.Length > 0)
                {
                    words.Add(cur.ToString());
                    cur.Clear();
                }
                if (ch != '_' && ch != ' ' && ch != '-') cur.Append(ch);
            }
            if (cur.Length > 0) words.Add(cur.ToString());
            return words;
        }

        // ---------------- Words ----------------

        /// <summary>Silver in the game's sign, whole with thousands marked from 10 up ("$1,400", "$9", "$1.9").</summary>
        public static string Money(float v) => "$" + (Math.Abs(v) >= 10f || Math.Abs(v - (float)Math.Round(v)) < 0.005f ? Math.Round(v).ToString("#,0", Inv) : v.ToString("0.##", Inv));

        /// <summary>A factor with a times sign ("x2.8", "x1").</summary>
        public static string Times(float v) => "x" + v.ToString("0.##", Inv);

        /// <summary>Degrees of insulation ("18 °C").</summary>
        public static string Degrees(float v) => v.ToString("0.#", Inv) + " °C";

        /// <summary>A number with its sign ("+6", "-4", "0").</summary>
        public static string Signed(float v) => v > 0f ? "+" + v.ToString("0.#", Inv) : v.ToString("0.#", Inv);

        /// <summary>A weight ("0.5 kg", "1.25 kg").</summary>
        public static string Kg(float v) => v.ToString("0.###", Inv) + " kg";

        /// <summary>A share as a whole percentage ("90%").</summary>
        private static string Share(float x) => Math.Round(x * 100f).ToString("0", Inv) + "%";

        /// <summary>A material's noun with its article: "a metal", "a fabric"; wood is timber and wool is wool, with none.</summary>
        public static string NounPhrase(string noun)
        {
            if (noun == "wood") return "timber";
            if (noun == "wool") return "wool";
            return A(noun);
        }

        /// <summary>Words joined "a, b and c".</summary>
        public static string AndList(IList<string> parts)
        {
            var p = parts.Where(s => !string.IsNullOrEmpty(s)).ToList();
            if (p.Count == 0) return "";
            if (p.Count == 1) return p[0];
            return string.Join(", ", p.Take(p.Count - 1)) + " and " + p[p.Count - 1];
        }

        // ---------------- Materials: tags ----------------

        /// <summary>The tags step 2 adds to the one vocabulary: materials' and art's. Step 1's tags they share (needed now, spare to sell, trade good, a gift, of a person) keep their place there.</summary>
        private static IEnumerable<TagDef> StepTwoTags() => new[]
        {
            D("explosive", "Explodes in a fire", R, 96, "a stack explodes when fire reaches it (the game's explosive comp): kept away from fire and raids"),
            D("lifts-room", "Lifts the room", S, 89, "a work of art that raises its room's impressiveness a stage, or would raise a room of the colony's a stage once placed"),
            D("strong-armour", "Strong armour", S, 88, "armour made of it stops sharp blows better than steel's (a sharp armour power of 100% or more)"),
            D("sturdy", "Sturdy", S, 86, "things made of it have half again steel's hit points or more"),
            D("ugly", "Ugly", W, 85, "a work of art whose beauty is below nothing: poor work spoils a room"),
            D("fireproof", "Fireproof", S, 84, "what is made of it hardly burns (a flammability of 5% or less)"),
            D("soft", "Soft", W, 84, "things made of it have 70% of steel's hit points or less"),
            D("beautiful", "Beautiful", S, 82, "what is made of it is twice as beautiful as of steel or more; a work of art that lifts a room"),
            D("burns", "Burns", W, 82, "what is made of it burns as wood does or worse"),
            D("warm", "Warm", S, 80, "clothes of it keep out the cold: 20 degrees of insulation or more"),
            D("keeps-cool", "Keeps cool", S, 79, "clothes of it keep out the heat: 20 degrees of insulation or more"),
            D("heavy", "Heavy", W, 78, "a unit weighs 160% of steel's or more: slow to haul, heavy in a caravan"),
            D("light", "Light", S, 76, "a unit weighs 60% of steel's or less: quick to haul, light in a caravan"),
            D("slow-work", "Slow to work", W, 76, "half again the work to build or make with it, or more"),
            D("repairs", "For repairs", O, 75, "the colony's machines that break down each take one to mend"),
            D("sharp-blades", "Sharp blades", S, 74, "blades made of it strike a tenth harder than steel's or more"),
            D("hard-blows", "Hard blows", S, 73, "clubs and maces made of it strike a fifth harder than steel's or more"),
            D("masterwork", "Masterwork", X, 72, "a work of art of masterwork or legendary quality"),
            D("money", "Money", O, 72, "silver: what traders pay and ask in"),
            D("precious", "Precious", X, 70, "a precious material: metal or stone that makes things beautiful (Rimfolk's precious things)"),
            D("building-material", "Building material", O, 70, "buildings the colony can build now take it"),
            D("noble-room", "For a noble", O, 69, "it stands in a noble's throne room or bedroom, whose impressiveness their title asks for"),
            D("for-clothes", "For clothes", O, 68, "apparel the colony can make now is made of it"),
            D("tells-story", "Tells a story", X, 66, "a work of art that shows a tale of the colony or its people"),
            D("fuel", "Fuel", O, 66, "something at home burns it"),
            D("faith-art", "Valued by a faith", X, 63, "a faith at home has precepts or memes about art"),
            D("meditation", "Meditation focus", X, 62, "psycasters with the artistic focus meditate on it"),
            D("raw-material", "Raw material", O, 60, "a recipe turns it into something the colony uses"),
            D("research", "Research", Nd, 59, "nothing made of it is open to the colony yet; research opens some"),
            D("bench", "A bench", Nd, 57, "the bench that makes it or works it isn't built"),
            D("a-room", "A room", Nd, 55, "a work of art does nothing in storage: placed in a room, it lifts it"),
        };

        /// <summary>A material's tags, each from its own numbers or Rimfolk's rules, with why.</summary>
        private static void MaterialTags(ItemFacts f, List<ThingTag> tags)
        {
            var m = f.Material ?? new MaterialFacts();
            var s = m.Stuff;
            var r0 = m.Refs.FirstOrDefault();
            string vs(Func<StuffNumbers, float> get, Func<float, string> words) => r0 != null ? " (" + r0.Label + " " + words(get(r0)) + ")" : "";

            // Risks.
            if (m.Explosive) tags.Add(T("explosive", "a stack explodes when fire reaches it (the game's explosive comp): store it away from fire, and away from where raiders fight"));
            if (m.OfPerson) tags.Add(T("of-person", "human leather: people who aren't cannibals are unhappy to wear it"));

            // What it does to what is made of it.
            if (s != null)
            {
                if (s.Apparel && s.ArmorSharp >= StrongArmour)
                    tags.Add(T("strong-armour", "armour made of it has a sharp armour power of " + Share(s.ArmorSharp) + vs(x => x.ArmorSharp, Share) + ", blunt " + Share(s.ArmorBlunt)));
                if (s.HitPoints >= SturdyAt) tags.Add(T("sturdy", "things made of it have " + Times(s.HitPoints) + " hit points" + vs(x => x.HitPoints, Times)));
                else if (s.Hard && s.HitPoints <= SoftAt) tags.Add(T("soft", "things made of it have " + Times(s.HitPoints) + " hit points" + vs(x => x.HitPoints, Times) + ": they break sooner"));
                if (s.Flammability <= FireproofAt) tags.Add(T("fireproof", "what is made of it has a flammability of " + Share(s.Flammability) + vs(x => x.Flammability, Share) + ": walls and furniture of it don't catch"));
                else if (s.Flammability >= BurnsAt) tags.Add(T("burns", "what is made of it has a flammability of " + Share(s.Flammability) + vs(x => x.Flammability, Share) + ": it catches and spreads fire"));
                if (s.Beauty >= BeautifulAt || s.BeautyOffset >= 2f)
                    tags.Add(T("beautiful", "what is made of it is " + Times(s.Beauty) + " as beautiful" + (s.BeautyOffset > 0f ? ", " + Signed(s.BeautyOffset) + " beauty besides" : "") + vs(x => x.Beauty, Times)));
                if (s.Apparel && s.InsulationCold >= WarmAt) tags.Add(T("warm", "clothes of it insulate " + Degrees(s.InsulationCold) + " against the cold" + vs(x => x.InsulationCold, Degrees)));
                if (s.Apparel && s.InsulationHeat >= WarmAt) tags.Add(T("keeps-cool", "clothes of it insulate " + Degrees(s.InsulationHeat) + " against the heat" + vs(x => x.InsulationHeat, Degrees)));
                if (s.Weapons && s.SharpDamage >= SharpBladesAt) tags.Add(T("sharp-blades", "blades made of it strike " + Times(s.SharpDamage) + vs(x => x.SharpDamage, Times)));
                if (s.Weapons && s.BluntDamage >= HardBlowsAt) tags.Add(T("hard-blows", "clubs and maces made of it strike " + Times(s.BluntDamage) + vs(x => x.BluntDamage, Times)));
                float work = Math.Max(s.WorkToBuild, s.WorkToMake);
                if (work >= SlowWorkAt)
                    tags.Add(T("slow-work", (s.WorkToBuild >= s.WorkToMake ? "building with it takes " + Times(s.WorkToBuild) : "making things of it takes " + Times(s.WorkToMake)) + " the work"
                                            + (r0 != null ? " (" + r0.Label + " " + Times(s.WorkToBuild >= s.WorkToMake ? r0.WorkToBuild : r0.WorkToMake) + ")" : "")));
            }
            float steelMass = m.Refs.FirstOrDefault(x => x.Def == "Steel")?.Mass ?? (s != null && s.Def == "Steel" ? s.Mass : SteelMass);
            float massShare = f.Mass / Math.Max(0.0001f, steelMass);
            // Weight matters for what is hauled and carried in bulk: hard stuff and chunks.
            bool bulk = m.Chunk || s != null && s.Hard && !m.Precious && !m.Money;
            if (bulk && (m.Chunk || massShare >= HeavyShare))
                tags.Add(T("heavy", "a unit weighs " + Kg(f.Mass) + ", " + Times((float)Math.Round(massShare, 1)) + " steel's: slow to haul, heavy in a caravan"));
            else if (bulk && massShare <= LightShare && f.Mass > 0f)
                tags.Add(T("light", "a unit weighs " + Kg(f.Mass) + ", " + Share(massShare) + " of steel's: quick to haul, light in a caravan"));

            // Rimfolk's roles.
            if (m.Money) tags.Add(T("money", "silver is what traders pay and ask in, and what the colony's dealings are counted in"));
            if (m.Precious) tags.Add(T("precious", "a precious " + m.Noun + ": things made of it are beautiful, and peoples treasure it"));
            if (m.Gift) tags.Add(T("gift", "a precious " + m.Noun + ": a gift between peoples (gold for the middle ages and the Empire, jade for tribes, silver for the rest)"));

            // Its uses here.
            if (f.HasColony)
            {
                if (m.Buildings > 0 && s != null && s.Hard || m.Buildings > 0 && s == null)
                    tags.Add(T("building-material", m.Buildings + " kinds of building and floor the colony can build now take it"));
                if (m.Apparel > 0 && s != null && !s.Hard) tags.Add(T("for-clothes", m.Apparel + " kinds of apparel the colony can make now are made of it"));
                if (m.Machines > 0) tags.Add(T("repairs", "the colony's " + m.Machines + " machines that break down take one each to mend: about " + N((float)Math.Round(RepairsASeason(m.Machines, m.BreakdownDays), 1)) + " a season"));
                if (m.Burners + m.BurnersWhileUsed > 0)
                    tags.Add(T("fuel", m.BurnerWords.Length > 0 ? "the colony's " + m.BurnerWords + " burn it" : "something at home burns it"));
                if (m.RawInto.Length > 0) tags.Add(T("raw-material", "made into " + m.RawInto + (m.RawBench.Length > 0 ? " at " + A(m.RawBench) : "")));
                int shortBy = ShortBy(m);
                if (NeededNow(m))
                    tags.Add(T("needed", m.BuildWants + m.BillWants > m.Held
                        ? "the colony's blueprints and bills need " + (m.BuildWants + m.BillWants) + " and it holds " + m.Held
                        : m.BillsStarved > 0 ? m.BillsStarved + " of the colony's bills can't start for want of it"
                        : "the colony holds " + m.Held + ", under the " + m.Need + " the trade advice keeps as its need for what it uses"));
                else if (Spare(m)) tags.Add(T("spare", "the colony holds " + m.Held + ", more than the " + (m.Keep + m.BuildWants + m.BillWants) + " the trade advice keeps"));
                bool usesNone = m.Buildings + m.Apparel + m.Weapons + m.Others == 0;
                if (usesNone && m.Later > 0 && m.LaterResearch.Length > 0)
                    tags.Add(T("research", "nothing made of it is open yet; " + m.LaterResearch + " opens some (" + m.Later + " in all behind research)"));
                if (!m.Makeable && m.RawInto.Length == 0 && m.MakeResearch.Length > 0 && m.Held <= 0)
                    tags.Add(T("research", "made only once " + m.MakeResearch + " is done"));
                if (m.Makeable && !m.BenchBuilt && m.Bench.Length > 0 && (m.Held <= 0 || shortBy > 0))
                    tags.Add(T("bench", "made at " + A(m.Bench) + ", and none is built"));
                else if (m.RawInto.Length > 0 && !m.RawBenchBuilt && m.RawBench.Length > 0)
                    tags.Add(T("bench", "made into " + m.RawInto + " at " + A(m.RawBench) + ", and none is built"));
            }
            else if (m.FuelAnywhere) tags.Add(T("fuel", "some buildings burn it"));
            float perKg = ValuePerKg(f);
            if (f.Tradeable && !m.Money && perKg >= TradeValuePerKg)
                tags.Add(T("trade-good", "it keeps, and is worth " + Math.Round(perKg).ToString("0", Inv) + " silver a kilogram: much worth in a pack"));
        }

        // ---------------- Art: tags ----------------

        private static void ArtTags(ItemFacts f, List<ThingTag> tags)
        {
            var a = f.ArtPiece ?? new ArtFacts();
            if (a.Beauty < 0f) tags.Add(T("ugly", "beauty " + N((float)Math.Round(a.Beauty)) + (a.Quality.Length > 0 ? ": " + a.Quality + " work" : "") + " spoils the room it stands in"));
            else if (a.Beauty > 0f && !(a.Placed && a.Indoors && a.StageIndexWith > a.StageIndexWithout))
                tags.Add(T("beautiful", "beauty " + N((float)Math.Round(a.Beauty)) + (a.Quality.Length > 0 ? " at " + a.Quality + " quality" : "")));
            if (a.Placed && a.Indoors && a.StageIndexWith > a.StageIndexWithout)
                tags.Add(T("lifts-room", "the " + a.Room + " is " + a.StageWith + " with it (" + N((float)Math.Round(a.ImpressWith)) + "), " + a.StageWithout + " without (" + N((float)Math.Round(a.ImpressWithout)) + ")"));
            else if ((!a.Placed || !a.Indoors) && a.BestRoom.Length > 0 && a.BestIndexWith > a.BestIndexNow)
                tags.Add(T("lifts-room", "placed in the " + a.BestRoom + " it would make it " + a.BestStageWith + " (" + N((float)Math.Round(a.BestWith)) + "), from " + a.BestStageNow));
            if (a.Flammability >= 0.5f) tags.Add(T("burns", "flammability " + Share(a.Flammability) + ": a fire takes it"));
            if (a.QualityIndex >= 5) tags.Add(T("masterwork", Cap(a.Quality) + " work: the best an artist makes"));
            if (a.Story) tags.Add(T("tells-story", (a.Title.Length > 0 ? "\"" + a.Title + "\"" : "it") + " shows " + (a.Tale.Length > 0 ? a.Tale : "a tale") + (a.Artist.Length > 0 ? ", by " + a.Artist : "")));
            if (a.SmallArt) tags.Add(T("gift", "small art of the kind Rimfolk's peoples carry and give; a gift to a people counts by its worth"));
            if (a.Noble.Length > 0 && a.NobleNeeds > 0)
                tags.Add(T("noble-room", a.Noble + "'s " + a.NobleRoom + " needs impressiveness " + a.NobleNeeds + ": " + N((float)Math.Round(a.ImpressWith)) + " with it, "
                                         + N((float)Math.Round(a.ImpressWithout)) + " without"));
            if (a.Faiths.Length > 0) tags.Add(T("faith-art", a.Faiths));
            if (a.MeditationFocus && a.Meditators > 0) tags.Add(T("meditation", a.Meditators + " at home meditate on art (the artistic focus)"));
            if (f.HasColony && !a.Placed)
            {
                if (a.BestRoom.Length > 0 && a.BestIndexWith > a.BestIndexNow)
                    tags.Add(T("a-room", "in storage it lifts nothing; the " + a.BestRoom + " is the room it would lift most"));
                else tags.Add(T("spare", "not placed, and no room of the colony's would rise a stage with it: worth " + Money(a.Worth) + " to a trader"));
            }
            float perKg = ValuePerKg(f);
            if (f.Tradeable && perKg >= TradeValuePerKg) tags.Add(T("trade-good", "worth " + Math.Round(perKg).ToString("0", Inv) + " silver a kilogram: much worth in a pack"));
        }

        // ---------------- The lines in plain words ----------------

        /// <summary>
        /// A material in one line: what it is and does best, then the colony's stock and what waits on it. "Plasteel: the
        /// sturdiest metal there is, light and fireproof; 340 in store, the blueprints and bills need 60 more."
        /// </summary>
        private static string MaterialPlain(ItemFacts f)
        {
            var m = f.Material ?? new MaterialFacts();
            var s = m.Stuff;
            var tagIds = new HashSet<string>(AllTags(f).Select(t => t.Id));
            string lead;
            if (m.Money) lead = "the money traders pay and ask in";
            else if (m.Machines > 0) lead = "mends the colony's " + m.Machines + " machines when they break down";
            else if (s == null && m.Burners > 0 && m.BurnerWords.Length > 0) lead = "fuel for the colony's " + m.BurnerWords;
            else if (m.RawInto.Length > 0) lead = "made into " + m.RawInto + (m.RawBench.Length > 0 ? " at " + A(m.RawBench) : "");
            else if (s != null)
            {
                if (m.SturdiestOfKind && s.Hard) lead = "the sturdiest " + m.Noun + " there is";
                else if (m.Precious) lead = "a precious " + m.Noun;
                else
                {
                    var uses = new List<string>();
                    if (m.Buildings > 0 && s.Hard) uses.Add("building");
                    if (m.Apparel > 0) uses.Add(s.Hard ? "armour" : "clothes");
                    if (m.Weapons > 0) uses.Add("weapons");
                    lead = NounPhrase(m.Noun) + (uses.Count > 0 ? " for " + AndList(uses) : "");
                }
            }
            else if (m.Buildings + m.Apparel + m.Weapons + m.Others > 0)
            {
                int n = m.Buildings + m.Apparel + m.Weapons + m.Others;
                lead = "an ingredient of " + n + (n == 1 ? " thing" : " things") + " the colony can make";
            }
            else if (m.FuelAnywhere) lead = "a fuel";
            else lead = NounPhrase(m.Noun);

            // Its strengths, then its weaknesses, the most telling first, two of each at most.
            var good = new List<string>();
            var bad = new List<string>();
            void Add(List<string> into, string id, string word)
            {
                if (into.Count < 2 && tagIds.Contains(id)) into.Add(word);
            }
            if (!m.Money)
            {
                Add(good, "light", "light");
                Add(good, "fireproof", "fireproof");
                if (!m.Precious) Add(good, "beautiful", "beautiful");
                Add(good, "warm", "warm");
                Add(good, "keeps-cool", "cool");
                if (!(m.SturdiestOfKind && s != null && s.Hard)) Add(good, "sturdy", "sturdy");
                Add(good, "strong-armour", "good for armour");
                Add(good, "sharp-blades", "good for blades");
                Add(good, "hard-blows", "good for maces");
                if (s != null && m.Burners + m.BurnersWhileUsed > 0) Add(good, "fuel", "burned as fuel");
                Add(bad, "explosive", "explosive in a fire");
                Add(bad, "of-person", "of a person");
                Add(bad, "burns", "flammable");
                Add(bad, "soft", "soft");
                Add(bad, "heavy", "heavy");
                Add(bad, "slow-work", "slow to work");
            }
            string head = Cap(f.Label) + ": " + lead + (good.Count > 0 ? ", " + AndList(good) : "") + (bad.Count > 0 ? ", but " + AndList(bad) : "");

            // The colony's stock and what waits on it.
            if (!f.HasColony || m.Held < 0) return head + ".";
            var tail = new List<string> { (m.Held == 0 ? "none" : m.Held.ToString("#,0", Inv)) + " in store" };
            int plans = m.BuildWants + m.BillWants;
            float days = DaysOf(m.Held, UseADay(m));
            if (plans > m.Held) tail.Add((m.BuildWants > 0 && m.BillWants > 0 ? "the blueprints and bills need " : m.BuildWants > 0 ? "the blueprints need " : "the bills need ") + (plans - m.Held) + " more");
            else if (m.BillsStarved > 0) tail.Add(m.BillsStarved + (m.BillsStarved == 1 ? " bill waits" : " bills wait") + " on it");
            else if (NeededNow(m)) tail.Add("under the " + m.Need + " the colony keeps");
            else if (days >= 0f && m.Burners > 0) tail.Add("lasts about " + Days(days));
            else if (Spare(m)) tail.Add("more than the colony keeps");
            else if (plans > 0) tail.Add("enough for the blueprints and bills");
            else if (m.Counted && ShortBy(m) == 0) tail.Add("enough");
            return head + "; " + string.Join(", ", tail) + ".";
        }

        /// <summary>
        /// A work of art in one line: its beauty where it stands and its worth, then what selling it would do. "Grand
        /// sculpture, excellent: beauty 220 in the dining room, worth $1,400; selling it would make the room merely decent."
        /// </summary>
        private static string ArtPlain(ItemFacts f)
        {
            var a = f.ArtPiece ?? new ArtFacts();
            string head = Cap(f.Label) + (a.Quality.Length > 0 ? ", " + a.Quality : "") + ": beauty " + N((float)Math.Round(a.Beauty));
            if (a.Placed && a.Indoors && a.Room.Length > 0) head += " in the " + a.Room;
            else if (a.Placed) head += " outdoors";
            head += ", worth " + Money(a.Worth);
            if (!f.HasColony) return head + ".";
            if (a.Placed && a.Indoors)
            {
                if (a.StageIndexWithout < a.StageIndexWith)
                    return head + "; selling it would make the room " + (a.StageIndexWithout > 0 ? "merely " : "") + a.StageWithout + ".";
                if (BetterWithout(a)) return head + "; the room would be " + (a.StageIndexWithout > a.StageIndexWith ? a.StageWithout : "better") + " without it.";
                return head + "; the room stays " + a.StageWith + " without it.";
            }
            if (a.BestRoom.Length > 0 && a.BestIndexWith > a.BestIndexNow)
                return head + "; placed in the " + a.BestRoom + " it would make it " + a.BestStageWith + ".";
            return head + (a.Placed ? "; it lifts no room where it stands." : "; no room of the colony's would rise a stage with it.");
        }
    }
}
