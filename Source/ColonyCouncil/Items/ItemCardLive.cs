using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One colonist's line on the item card: who, what it means for them, and in which tone (1 good, 0 plain, -1 bad).</summary>
    public sealed class ItemPawnLine
    {
        public Pawn pawn;
        public string text = "";
        public int tone;
    }

    /// <summary>A line on the item card that may lead somewhere (a bench and its bills, a field), and its tone.</summary>
    public sealed class ItemGoLine
    {
        public string text = "";
        public string tip;
        public Action go;
        public int tone;
    }

    /// <summary>One effect of a drink or drug: its name with how long a dose lasts, and what it does (the game's own words for its stage).</summary>
    public sealed class ItemEffectLine
    {
        public string head = "";
        public string body = "";
    }

    /// <summary>
    /// What the item card shows for one thing or kind, worked out when the card opens and kept: the facts and tags, and every
    /// line of every section as finished words. <see cref="ItemCardLive.Refresh"/> brings it up to date, the live parts
    /// (the stack, how long it keeps where it lies) at most once a second and the colony's at most every ten seconds; the
    /// card only ever draws from here, and nothing is worked out while it is closed.
    /// </summary>
    public sealed class ItemCardModel
    {
        public Thing thing;
        public ThingDef def, stuff;
        public Map map;
        public ItemKind kind;
        public ItemFacts facts = new ItemFacts();
        public List<ThingTag> tags = new List<ThingTag>();
        public string label = "", kindLine = "", plain = "";

        public readonly List<(string label, string value)> food = new List<(string, string)>();
        public readonly List<ItemPawnLine> eaters = new List<ItemPawnLine>();
        public string eatersNote;
        public readonly List<ItemEffectLine> effects = new List<ItemEffectLine>();
        public readonly List<(string label, string value)> drug = new List<(string, string)>();
        public readonly List<ItemPawnLine> takers = new List<ItemPawnLine>();
        public readonly List<string> faiths = new List<string>();
        public readonly List<(string label, string value)> medicine = new List<(string, string)>();
        public readonly List<ItemPawnLine> doctors = new List<ItemPawnLine>();
        public string bestUse = "";
        public readonly List<(string label, string value)> stock = new List<(string, string)>();
        public readonly List<ItemGoLine> making = new List<ItemGoLine>();
        public readonly List<(string label, string value)> trade = new List<(string, string)>();
        public readonly List<string> market = new List<string>();

        // ---------------- Materials (step 2) ----------------

        /// <summary>What it does to what is made of it against what it is compared with: the columns' heads (it, then the others) and the rows.</summary>
        public readonly List<string> stuffHeads = new List<string>();
        public readonly List<StuffRow> stuffTable = new List<StuffRow>();
        public readonly List<string> stuffLabels = new List<string>();
        public string stuffKinds = "";
        /// <summary>What the colony can make of it now, by kind (the best first), and what research would open more.</summary>
        public readonly List<(string label, string value)> madeOf = new List<(string, string)>();
        public string madeLater;
        /// <summary>The colony's stock and what waits on it (blueprints, repairs, fuel, the trade advice's verdict), and the bills that take it.</summary>
        public readonly List<(string label, string value)> demand = new List<(string, string)>();
        public readonly List<ItemGoLine> bills = new List<ItemGoLine>();
        /// <summary>Where it comes from here: deposits, a deep drill, trees and crops, animals, deconstruction, the recipes that make it.</summary>
        public readonly List<ItemGoLine> sources = new List<ItemGoLine>();

        // ---------------- Art (step 2) ----------------

        /// <summary>The piece: beauty, by quality, worth, material, the artist.</summary>
        public readonly List<(string label, string value)> art = new List<(string, string)>();
        /// <summary>Its story: the title, the tale and its date, and the game's own description of the image.</summary>
        public readonly List<(string label, string value)> story = new List<(string, string)>();
        public string storyText;
        /// <summary>Where it stands and what it does there, with it and without it; the keep-or-sell verdict.</summary>
        public readonly List<(string label, string value)> room = new List<(string, string)>();
        public string keepSell = "";
        /// <summary>Not placed (or outdoors): the colony's rooms it would lift most, a click going to each.</summary>
        public readonly List<ItemGoLine> rooms = new List<ItemGoLine>();
        /// <summary>The rules that care: a noble's room, a faith's precepts, meditation.</summary>
        public readonly List<string> rules = new List<string>();

        // ---------------- Step 3: books, body parts, techprints, trainers, genes, usable items, shells, furniture, the rest ----------------

        /// <summary>What it does, in rows, and a paragraph under them (the game's own words, a description).</summary>
        public readonly List<(string label, string value)> use = new List<(string, string)>();
        public string useNote;
        /// <summary>A genepack's, xenogerm's or embryo's genes, each a line with what it does.</summary>
        public readonly List<ItemEffectLine> genes = new List<ItemEffectLine>();
        /// <summary>Who at home it is for, the best first, and a line over them.</summary>
        public readonly List<ItemPawnLine> forWhom = new List<ItemPawnLine>();
        public string forWhomNote;
        /// <summary>The rules that care (a faith's precepts), and lines that lead somewhere (a gun, a room, a bench).</summary>
        public readonly List<string> useRules = new List<string>();
        public readonly List<ItemGoLine> useGo = new List<ItemGoLine>();

        /// <summary>The colony's best cook and the chance in a meal of theirs (a kind of meal), and how many at home are addicted to it.</summary>
        internal string cookPoison;
        internal int addicted;
        internal float liveAt = -999f, colonyAt = -999f;
        /// <summary>The thing the card reads: the piece inside a minified one, else the thing itself.</summary>
        internal Thing inner;
        /// <summary>A work of art's tale and its beauty by quality, read once a card.</summary>
        internal bool storyRead;
        internal string byQuality;
        /// <summary>Step 3: the thing's own reading (a book's skills and research, a gene holder's genes, an unfinished thing's work), read once a card.</summary>
        internal bool useRead;
    }

    /// <summary>
    /// The item card's reading of the game (Rimfolk's look, stage 3, point 5: step 1 food, drink, drugs and medicine, step 2
    /// materials and art, in Items/ItemCardMaterials.cs and Items/ItemCardArt.cs, step 3 everything else in
    /// Items/ItemCardUses.cs, ItemCardBodies.cs and ItemCardThings.cs). A kind's own numbers are read once (its
    /// nutrition and rot, its taste, its drug's chemistry and effects, its potency, a material's stuff numbers and what is
    /// made of it, Rimfolk's roles for it, the recipes and crops that make it, the traders who deal in it) and kept for the
    /// session; the thing in hand and the colony are read when a card opens and while it stays open, never otherwise. Rules
    /// and words in <see cref="ItemCardMath"/>; who may eat or take what in Wishes/Beliefs.cs; medicine by what is treated in
    /// Health/MedicineChoiceMath.cs; the colony's reserves in Trade/TradeAdviceMath.cs.
    /// </summary>
    public static partial class ItemCardLive
    {
        /// <summary>The live parts are read at most this often (real seconds), the colony's at most this often.</summary>
        public const float LiveEvery = 1f, ColonyEvery = 10f;

        /// <summary>The card's kind of a def (None: the game's card opens). Weapons, apparel, corpses and pawns are never its; a building only as art or in a crate.</summary>
        public static ItemKind KindOf(ThingDef d) => KindOf(d, null);

        /// <summary>
        /// The card's kind of a def, or of a thing in hand (a full grave's art counts; a minified piece is read as the piece
        /// inside). What still goes to the game's card, and why: weapons and apparel (their own cards), corpses (the game's
        /// card is the dead one's own, their health and story), pawns, plants, filth and the like (not items), a building
        /// standing on the map that isn't art (the game's card for a building, with its power and links), a blueprint or a
        /// frame, and the crate's own kind with nothing in it.
        /// </summary>
        public static ItemKind KindOf(ThingDef d, Thing t)
        {
            if (d == null || d.IsCorpse || d.IsWeapon || d.IsApparel || d.race != null || d.IsBlueprint || d.IsFrame) return ItemKind.None;
            if (d.category != ThingCategory.Item && d.category != ThingCategory.Building) return ItemKind.None;
            if (typeof(MinifiedThing).IsAssignableFrom(d.thingClass)) return ItemKind.None;
            return ItemCardMath.KindOf(SortOf(d, t));
        }

        /// <summary>The card's kind of a thing in hand: a minified piece is the piece inside.</summary>
        public static ItemKind KindOf(Thing t)
        {
            var inner = t?.GetInnerIfMinified();
            return inner == null ? ItemKind.None : KindOf(inner.def, inner);
        }

        public static bool Covers(ThingDef d) => KindOf(d) != ItemKind.None;

        public static bool Covers(Thing t) => KindOf(t) != ItemKind.None;

        /// <summary>What a def is, for its kind (ItemCardMath.KindOf): step 1's food, drink, drugs and medicine, step 2's art and materials, step 3's the rest.</summary>
        private static ItemSort SortOf(ThingDef d, Thing t)
        {
            var s = new ItemSort();
            if (d.category == ThingCategory.Item && (d.IsMedicine || d.ingestible != null))
            {
                s.Medicine = d.IsMedicine;
                s.Drug = d.IsDrug;
                s.Fluid = d.ingestible != null && (d.ingestible.foodType & (FoodTypeFlags.Fluid | FoodTypeFlags.Liquor)) != 0;
                s.Feeds = d.IsNutritionGivingIngestible;
            }
            var art = d.GetCompProperties<CompProperties_Art>();
            if (art != null)
            {
                bool full = false;
                if (art.mustBeFullGrave)
                    try
                    {
                        full = t is Building_CorpseCasket g && g.HasCorpse && t.TryGetComp<CompArt>()?.Active == true;
                    }
                    catch
                    {
                        full = false;
                    }
                s.Art = !art.mustBeFullGrave || full;
            }
            if (d.category == ThingCategory.Item)
            {
                s.Stuff = d.IsStuff;
                s.Resource = d.thingCategories != null && (d.IsWithinCategory(ThingCategoryDefOf.ResourcesRaw) || d.IsWithinCategory(ThingCategoryDefOf.Manufactured)
                                                          || d.IsWithinCategory(ThingCategoryDefOf.Chunks) || d.IsWithinCategory(ThingCategoryDefOf.StoneChunks));
                s.ToUse = ToUse(d);
                StepThreeSort(d, s);
            }
            // A building in a crate (the piece inside a minified thing), read as furniture to place.
            else if (d.category == ThingCategory.Building && t != null && t.ParentHolder is MinifiedThing) s.Minified = true;
            return s;
        }

        /// <summary>Something to use, read, install or fire rather than build with, never a material: step 3's own kinds (a shell, a book, a techprint, a trainer, a genepack, a body part, an unfinished thing).</summary>
        private static bool ToUse(ThingDef d)
        {
            if (d.IsShell || d.isTechHediff || d.isUnfinishedThing) return true;
            if (d.IsWithinCategory(ThingCategoryDefOf.BodyParts) || d.IsWithinCategory(ThingCategoryDefOf.Techprints) || d.IsWithinCategory(ThingCategoryDefOf.Neurotrainers)) return true;
            if (d.comps != null)
                foreach (var c in d.comps)
                    if (c is CompProperties_Usable || c is CompProperties_Techprint) return true;
            string cls = d.thingClass?.Name ?? "";
            return cls == "Book" || cls == "Genepack" || cls == "Xenogerm" || cls == "HumanEmbryo" || cls == "HumanOvum" || typeof(Book).IsAssignableFrom(d.thingClass);
        }

        private static string W(string key, params NamedArgument[] args) => key.Translate(args).Resolve();

        // ---------------------------------------------------------------- a kind's own numbers, kept

        private sealed class DefPart
        {
            public ThingDef def;
            public ItemKind kind;
            public float nutrition, rotDays = -1f, value, mass;
            public int pref;
            public bool meal, humanEdible, fluid, tradeable;
            public string tasteLabel = "";
            public float tasteMood, tasteDays;
            public float poisonFixed;
            public bool poisonable, ofPerson, insect, travelFood;
            public readonly List<RecipeDef> cooks = new List<RecipeDef>();
            public CompProperties_Drug drug;
            public int drugKind;
            public bool alcohol, soft;
            public float joy, highMood, highDays, tolerancePerDose, toleranceFall;
            public readonly List<ItemEffectLine> effects = new List<ItemEffectLine>();
            public float potency, qualityMax;
            public bool feast, toast, gift, provisions;
            public int travelRank;
            public readonly List<RecipeDef> makes = new List<RecipeDef>();
            public readonly List<ThingDef> crops = new List<ThingDef>();
            /// <summary>The kinds of trader who deal in it, with the share of its worth they ask when the colony buys and pay when it sells (the game's price types).</summary>
            public readonly List<(string label, float asks, float pays)> traders = new List<(string, float, float)>();
            /// <summary>A material's own numbers and what is made of it, or a work of art's (step 2: Items/ItemCardMaterials.cs, Items/ItemCardArt.cs).</summary>
            public MatPart mat;
            public ArtPart art;
            /// <summary>Step 3's own numbers (Items/ItemCardUses.cs).</summary>
            public UsePart use;
        }

        private static readonly Dictionary<ThingDef, DefPart> parts = new Dictionary<ThingDef, DefPart>();
        private static Dictionary<HediffDef, List<ThoughtDef>> hediffThoughts;

        private static DefPart PartOf(ThingDef d) => PartOf(d, KindOf(d));

        /// <summary>A kind's own numbers, read once for the kind the card shows it as (a full grave is art though its kind on its own isn't).</summary>
        private static DefPart PartOf(ThingDef d, ItemKind kind)
        {
            if (parts.TryGetValue(d, out var p) && p.kind == kind) return p;
            if (parts.Count > 300) parts.Clear();
            p = new DefPart { def = d, kind = kind };
            try
            {
                Read(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the item card reading " + d.defName, e);
            }
            parts[d] = p;
            return p;
        }

        private static float Stat(ThingDef d, StatDef s)
        {
            if (s == null) return 0f;
            try
            {
                return d.GetStatValueAbstract(s);
            }
            catch
            {
                return 0f;
            }
        }

        private static void Read(DefPart p)
        {
            var d = p.def;
            var ing = d.ingestible;
            p.value = d.BaseMarketValue;
            p.mass = Stat(d, StatDefOf.Mass);
            p.tradeable = d.tradeability != Tradeability.None;
            var rot = d.GetCompProperties<CompProperties_Rottable>();
            p.rotDays = rot != null && rot.daysToRotStart > 0f ? rot.daysToRotStart : -1f;
            if (ing != null)
            {
                p.nutrition = Stat(d, StatDefOf.Nutrition);
                p.pref = ThingRolesMath.IndexOf(ThingRolesMath.Preferabilities, ing.preferability.ToString());
                p.meal = (ing.foodType & FoodTypeFlags.Meal) != 0 || ing.IsMeal;
                p.humanEdible = ing.HumanEdible;
                p.fluid = (ing.foodType & (FoodTypeFlags.Fluid | FoodTypeFlags.Liquor)) != 0;
                var taste = ing.tasteThought ?? (ing.preferability == FoodPreferability.RawBad ? ThoughtDefOf.AteRawFood : null);
                var stage = taste?.stages?.FirstOrDefault(s => s != null);
                if (stage != null)
                {
                    p.tasteLabel = (stage.label ?? taste.label ?? "").CapitalizeFirst();
                    p.tasteMood = stage.baseMoodEffect;
                    p.tasteDays = taste.durationDays;
                }
                p.poisonFixed = Stat(d, StatDefOf.FoodPoisonChanceFixedHuman);
                p.poisonable = d.HasComp(typeof(CompFoodPoisonable));
                var src = ing.sourceDef?.race;
                p.ofPerson = src?.Humanlike == true || ThingRoles.Facts(d)?.humanlike == true;
                p.insect = src != null && src.FleshType == FleshTypeDefOf.Insectoid && (ing.foodType & FoodTypeFlags.Meat) != 0;
                p.travelFood = d.IsNutritionGivingIngestible && !d.IsDrug && ing.HumanEdible && ing.preferability >= FoodPreferability.RawTasty
                               && ing.preferability <= FoodPreferability.MealLavish;
                p.joy = ing.joy;
                if (d.IsDrug)
                {
                    p.drug = d.GetCompProperties<CompProperties_Drug>();
                    string cat = ing.drugCategory.ToString();
                    p.drugKind = cat == "Medical" ? 1 : cat == "Social" ? 2 : cat == "Hard" ? 3 : 0;
                    p.soft = p.drug != null && p.drug.teetotalerCanConsume;
                    p.alcohol = Beliefs.IsAlcohol(d) && !p.soft;
                    Effects(p);
                }
                bool food = p.kind == ItemKind.Food || p.kind == ItemKind.Drink && !d.IsDrug;
                if (food && ing.HumanEdible && (ing.preferability == FoodPreferability.RawBad || ing.preferability == FoodPreferability.RawTasty)) Cooks(p);
            }
            if (d.IsMedicine)
            {
                p.potency = Stat(d, StatDefOf.MedicalPotency);
                p.qualityMax = Stat(d, StatDefOf.MedicalQualityMax);
            }
            p.feast = ThingRoles.Fits(ThingRole.Feast, d);
            p.provisions = ThingRoles.Fits(ThingRole.Provisions, d);
            p.travelRank = ThingRolesMath.TravelRank(ThingRoles.Facts(d));
            p.gift = d.IsDrug && ThingRoles.Fits(ThingRole.Gift, d);
            p.toast = ToastDrinks.IsToastDrink(d);
            Makers(p);
            Traders(p);
            if (p.kind == ItemKind.Material) ReadMaterial(p);
            else if (p.kind == ItemKind.Art) ReadArt(p);
            else if (ItemCardMath.StepThree(p.kind)) ReadUse(p);
        }

        /// <summary>A drug's effects: each hediff its outcomes give (its stage at one dose, the game's words for it, how long it lasts, the mood of its thought), its tolerance and the needs it moves.</summary>
        private static void Effects(DefPart p)
        {
            var doers = p.def.ingestible?.outcomeDoers;
            if (doers == null) return;
            var chem = p.drug?.chemical;
            foreach (var doer in doers)
            {
                try
                {
                    if (doer is IngestionOutcomeDoer_GiveHediff give && give.hediffDef != null)
                    {
                        var h = give.hediffDef;
                        float days = Lasts(h, give.severity);
                        if (chem != null ? h == chem.toleranceHediff : h.defName.EndsWith("Tolerance", StringComparison.Ordinal))
                        {
                            p.tolerancePerDose = give.severity;
                            p.toleranceFall = Falls(h);
                            continue;
                        }
                        int stageIndex = StageAt(h, give.severity);
                        var stage = h.stages != null && stageIndex >= 0 && stageIndex < h.stages.Count ? h.stages[stageIndex] : null;
                        var words = new List<string>();
                        if (stage != null)
                        {
                            try
                            {
                                foreach (var e in HediffStatsUtility.SpecialDisplayStats(stage, null))
                                    if (e != null) words.Add(e.LabelCap + " " + e.ValueString);
                            }
                            catch
                            {
                                // A mod's stage the game's own words can't read without a hediff: its name alone.
                            }
                        }
                        float mood = MoodOf(h, stageIndex);
                        if (mood > p.highMood) p.highMood = mood;
                        if (days > p.highDays) p.highDays = days;
                        if (Math.Abs(mood) > 0.01f) words.Add("CC_ItemCard_EffectMood".Translate(ItemCardMath.Mood(mood).Named("MOOD")).Resolve());
                        string head = h.LabelCap + (days > 0f ? " · " + "CC_ItemCard_EffectLasts".Translate(ItemCardMath.Days(days).Named("DAYS")).Resolve() : "");
                        if (doer.chance > 0f && doer.chance < 0.999f) head += " · " + "CC_ItemCard_EffectChance".Translate(ItemCardMath.Pct(doer.chance).Named("CHANCE")).Resolve();
                        p.effects.Add(new ItemEffectLine { head = head, body = string.Join(", ", words) });
                    }
                    else if (doer is IngestionOutcomeDoer_OffsetNeed need && need.need != null && Math.Abs(need.offset) > 0.001f)
                        p.effects.Add(new ItemEffectLine
                        {
                            head = need.need.LabelCap,
                            body = "CC_ItemCard_EffectNeed".Translate(((need.offset > 0f ? "+" : "") + need.offset.ToStringPercent()).Named("OFFSET")).Resolve()
                        });
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("the item card reading a drug's effects: " + p.def.defName, e);
                }
            }
        }

        /// <summary>The stage a hediff is at with this severity (the last whose least severity it reaches).</summary>
        private static int StageAt(HediffDef h, float severity)
        {
            if (h.stages == null || h.stages.Count == 0) return -1;
            int at = 0;
            for (int i = 0; i < h.stages.Count; i++)
                if (h.stages[i] != null && severity >= h.stages[i].minSeverity) at = i;
            return at;
        }

        /// <summary>How long a hediff given at this severity lasts, in days: by its fall a day, or its own time to go (0 when unknown).</summary>
        private static float Lasts(HediffDef h, float severity)
        {
            if (h.comps == null) return 0f;
            foreach (var c in h.comps)
            {
                if (c is HediffCompProperties_SeverityPerDay sp)
                {
                    float perDay = Math.Abs(sp.severityPerDay) > 0.0001f ? sp.severityPerDay : (sp.severityPerDayRange.min + sp.severityPerDayRange.max) / 2f;
                    if (perDay < -0.0001f) return severity / -perDay;
                }
                if (c is HediffCompProperties_Disappears dis && dis.disappearsAfterTicks.max > 0)
                    return (dis.disappearsAfterTicks.min + dis.disappearsAfterTicks.max) / 2f / GenDate.TicksPerDay;
            }
            return 0f;
        }

        /// <summary>How fast a hediff falls a day (0 when it doesn't on its own).</summary>
        private static float Falls(HediffDef h)
        {
            if (h.comps == null) return 0f;
            foreach (var c in h.comps)
                if (c is HediffCompProperties_SeverityPerDay sp && sp.severityPerDay < 0f) return -sp.severityPerDay;
            return 0f;
        }

        /// <summary>The mood of the game's thought on a hediff at a stage (a drug's high), 0 with none.</summary>
        private static float MoodOf(HediffDef h, int stageIndex)
        {
            if (hediffThoughts == null)
            {
                hediffThoughts = new Dictionary<HediffDef, List<ThoughtDef>>();
                foreach (var t in DefDatabase<ThoughtDef>.AllDefsListForReading)
                {
                    if (t?.hediff == null) continue;
                    if (!hediffThoughts.TryGetValue(t.hediff, out var l)) hediffThoughts[t.hediff] = l = new List<ThoughtDef>();
                    l.Add(t);
                }
            }
            if (!hediffThoughts.TryGetValue(h, out var list)) return 0f;
            float best = 0f;
            foreach (var t in list)
            {
                if (t.stages == null || t.stages.Count == 0) continue;
                var s = t.stages[Mathf.Clamp(stageIndex, 0, t.stages.Count - 1)];
                if (s != null && Math.Abs(s.baseMoodEffect) > Math.Abs(best)) best = s.baseMoodEffect;
            }
            return best;
        }

        /// <summary>The recipes that cook this raw food into a meal people eat (a few, the game's and mods' benches alike).</summary>
        private static void Cooks(DefPart p)
        {
            foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (p.cooks.Count >= 6) break;
                try
                {
                    var product = r?.ProducedThingDef ?? (r?.products != null && r.products.Count > 0 ? r.products[0].thingDef : null);
                    if (product?.ingestible == null || !product.ingestible.HumanEdible || !product.ingestible.IsMeal || product.IsDrug) continue;
                    if (r.ingredients == null || r.ingredients.Count == 0 || !r.IsIngredient(p.def)) continue;
                    p.cooks.Add(r);
                }
                catch
                {
                    // A mod's recipe that can't be read: left out.
                }
            }
        }

        /// <summary>The recipes that make it and the crops that grow it.</summary>
        private static void Makers(DefPart p)
        {
            foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (p.makes.Count >= 8) break;
                if (r?.products != null && r.products.Any(x => x?.thingDef == p.def)) p.makes.Add(r);
            }
            foreach (var t in DefDatabase<ThingDef>.AllDefsListForReading)
                if (t?.plant != null && t.plant.Sowable && t.plant.harvestedThingDef == p.def && !t.plant.IsTree && p.crops.Count < 4) p.crops.Add(t);
        }

        /// <summary>The kinds of trader who deal in it, and the price type each gives it when the colony buys and when it sells.</summary>
        private static void Traders(DefPart p)
        {
            if (!p.tradeable) return;
            var seen = new HashSet<string>();
            foreach (var k in DefDatabase<TraderKindDef>.AllDefsListForReading)
            {
                try
                {
                    if (k == null || k.label.NullOrEmpty() || !k.WillTrade(p.def)) continue;
                    string label = k.LabelCap.ToString();
                    if (!seen.Add(label)) continue;
                    p.traders.Add((label, k.PriceTypeFor(p.def, TradeAction.PlayerBuys).PriceMultiplier(), k.PriceTypeFor(p.def, TradeAction.PlayerSells).PriceMultiplier()));
                }
                catch
                {
                    // A mod's trader whose stock can't be asked about: left out.
                }
            }
        }

        // ---------------------------------------------------------------- the model

        /// <summary>A card's model for a thing in hand or a kind, on the map it is opened on (a home map, else no colony to speak of).</summary>
        public static ItemCardModel Build(Thing thing, ThingDef def, ThingDef stuff, Map map)
        {
            // A minified piece of art is read as the piece inside it; the card's picture stays the thing as it lies.
            var inner = thing?.GetInnerIfMinified();
            var m = new ItemCardModel { thing = thing, inner = inner, def = inner?.def ?? def, stuff = inner != null ? inner.Stuff : stuff };
            if (m.def == null) return m;
            m.kind = inner != null ? KindOf(inner.def, inner) : KindOf(m.def);
            var home = thing != null && thing.MapHeld != null ? thing.MapHeld : map;
            m.map = home != null && home.IsPlayerHome ? home : null;
            Refresh(m, true);
            return m;
        }

        /// <summary>Brings the model up to date: the live parts once a second, the colony's every ten seconds (or both now, forced).</summary>
        public static void Refresh(ItemCardModel m, bool force = false)
        {
            if (m?.def == null) return;
            float now = Time.realtimeSinceStartup;
            bool colony = force || now - m.colonyAt >= ColonyEvery || now < m.colonyAt;
            bool live = colony || now - m.liveAt >= LiveEvery || now < m.liveAt;
            if (!live) return;
            try
            {
                // The thing's own facts first (the colony's reading asks what kind it is), then the colony's, then the lines.
                var p = PartOf(m.def, m.kind);
                m.liveAt = now;
                // A material's or a piece's own facts first: the kind's line in the head reads them.
                if (m.kind == ItemKind.Material) LiveMaterial(m, p);
                else if (m.kind == ItemKind.Art) LiveArt(m, p);
                else if (ItemCardMath.StepThree(m.kind)) LiveUse(m, p);
                Live(m, p);
                if (colony)
                {
                    m.colonyAt = now;
                    Colony(m, p);
                    if (m.kind == ItemKind.Material) ColonyMaterial(m, p);
                    else if (m.kind == ItemKind.Art) ColonyArt(m, p);
                    else if (ItemCardMath.StepThree(m.kind)) ColonyUse(m, p);
                }
                Rows(m, p);
                if (m.kind == ItemKind.Material) RowsMaterial(m, p);
                else if (m.kind == ItemKind.Art) RowsArt(m, p);
                else if (ItemCardMath.StepThree(m.kind)) RowsUse(m, p);
                m.tags = ItemCardMath.Tags(m.facts);
                m.plain = ItemCardMath.Plain(m.facts);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the item card's model", e);
            }
        }

        // ---------------------------------------------------------------- the live parts

        private static void Live(ItemCardModel m, DefPart p)
        {
            var f = m.facts;
            var t = m.thing;
            f.Label = m.def.label ?? m.def.defName;
            f.Kind = p.kind;
            f.InHand = t != null;
            f.Count = t?.stackCount ?? 1;
            f.Value = m.inner != null ? m.inner.MarketValue : t != null ? t.MarketValue : p.value;
            f.Mass = p.mass;
            f.Tradeable = p.tradeable;
            f.Nutrition = p.nutrition;
            f.Preferability = p.pref;
            f.Meal = p.meal;
            f.HumanEdible = p.humanEdible;
            f.Fluid = p.fluid;
            f.RotDays = p.rotDays;
            f.TasteThought = p.tasteLabel;
            f.TasteMood = p.tasteMood;
            f.TasteDays = p.tasteDays;
            f.PoisonFixed = p.poisonFixed;
            f.PoisonFactor = Find.Storyteller?.difficulty?.foodPoisonChanceFactor ?? 1f;
            f.OfPerson = p.ofPerson;
            f.Insect = p.insect;
            f.IsDrug = m.def.IsDrug;
            f.DrugKind = p.drugKind;
            f.Alcohol = p.alcohol;
            f.Soft = p.soft;
            f.Addictive = p.drug != null && p.drug.Addictive;
            f.Addictiveness = p.drug?.addictiveness ?? 0f;
            f.MinTolerance = p.drug?.minToleranceToAddict ?? 0f;
            f.OverdoseMin = p.drug != null && p.drug.CanCauseOverdose ? p.drug.overdoseSeverityOffset.min : 0f;
            f.OverdoseMax = p.drug != null && p.drug.CanCauseOverdose ? p.drug.overdoseSeverityOffset.max : 0f;
            f.LargeOverdose = p.drug != null && p.drug.CanCauseOverdose ? p.drug.largeOverdoseChance : 0f;
            f.Joy = p.joy;
            f.HighMood = p.highMood;
            f.HighDays = p.highDays;
            f.Potency = p.potency;
            f.QualityMax = p.qualityMax;
            f.HerbalPotency = ThingDefOf.MedicineHerbal != null ? Stat(ThingDefOf.MedicineHerbal, StatDefOf.MedicalPotency) : 0.6f;
            f.IndustrialPotency = ThingDefOf.MedicineIndustrial != null ? Stat(ThingDefOf.MedicineIndustrial, StatDefOf.MedicalPotency) : 1f;
            f.MedicineByNeed = MedicineChoice.SettingOn;
            f.FeastDish = p.feast;
            f.ToastDrink = p.toast;
            f.Gift = p.gift;
            f.Provisions = p.provisions;
            f.TravelRank = p.travelRank;

            // The thing in hand: how long it keeps where it lies, its own chance of food poisoning.
            f.DaysLeftHere = -1f;
            f.FrozenHere = false;
            f.Rotting = false;
            f.PoisonChance = 0f;
            if (t != null && !t.Destroyed)
            {
                var rot = t.TryGetComp<CompRottable>();
                if (rot != null && rot.Active && p.rotDays > 0f)
                {
                    f.Rotting = rot.Stage != RotStage.Fresh;
                    int ticks = rot.TicksUntilRotAtCurrentTemp;
                    if (ticks >= int.MaxValue / 2) f.FrozenHere = true;
                    else f.DaysLeftHere = Mathf.Max(0f, ticks / (float)GenDate.TicksPerDay);
                }
                else if (rot != null && !rot.Active && p.rotDays > 0f) f.FrozenHere = true;
                var poison = t.TryGetComp<CompFoodPoisonable>();
                if (poison != null) f.PoisonChance = poison.PoisonPercent;
            }

            m.label = t != null ? t.LabelCap : GenLabel.ThingLabel(m.def, m.stuff).CapitalizeFirst();
            m.kindLine = KindLine(m, p);
        }

        /// <summary>What kind of thing it is in a few words, with its worth: "Meal, fine · 28 silver each".</summary>
        private static string KindLine(ItemCardModel m, DefPart p)
        {
            string kind;
            switch (p.kind)
            {
                case ItemKind.Medicine:
                    kind = W("CC_ItemCard_Kind_Medicine", p.potency.ToStringPercent().Named("POTENCY"));
                    break;
                case ItemKind.Drink:
                    kind = W(!m.def.IsDrug ? "CC_ItemCard_Kind_DrinkPlain" : p.alcohol ? "CC_ItemCard_Kind_DrinkAlcohol" : p.soft ? "CC_ItemCard_Kind_DrinkSoft" : "CC_ItemCard_Kind_DrinkDrug");
                    break;
                case ItemKind.Drug:
                    kind = W(p.drugKind == 1 ? "CC_ItemCard_Kind_DrugMedical" : p.drugKind == 3 ? "CC_ItemCard_Kind_DrugHard" : "CC_ItemCard_Kind_DrugSocial");
                    break;
                case ItemKind.Material:
                    kind = MaterialKindWords(m, p);
                    break;
                case ItemKind.Art:
                    kind = ArtKindWords(m, p);
                    break;
                case ItemKind k when ItemCardMath.StepThree(k):
                    kind = UseKindWords(m, p);
                    break;
                default:
                    kind = !p.humanEdible ? W("CC_ItemCard_Kind_Feed")
                        : p.meal ? W("CC_ItemCard_Kind_Meal", W(p.pref >= ThingRolesMath.MealLavish ? "CC_ItemCard_Pref_Lavish" : p.pref == ThingRolesMath.MealFine ? "CC_ItemCard_Pref_Fine"
                            : p.pref == ThingRolesMath.MealSimple ? "CC_ItemCard_Pref_Simple" : "CC_ItemCard_Pref_Awful").Named("PREF"))
                        : ItemCardMath.Raw(m.facts) ? W("CC_ItemCard_Kind_Raw") : W("CC_ItemCard_Kind_Food");
                    break;
            }
            string worth = m.thing != null && m.thing.stackCount > 1
                ? W("CC_ItemCard_WorthStack", (m.thing.MarketValue).ToStringMoney().Named("EACH"), (m.thing.MarketValue * m.thing.stackCount).ToStringMoney().Named("ALL"))
                : W("CC_ItemCard_Worth", m.facts.Value.ToStringMoney().Named("EACH"));
            return kind + " · " + worth;
        }

        // ---------------------------------------------------------------- each section's lines

        private static void Rows(ItemCardModel m, DefPart p)
        {
            var f = m.facts;
            m.food.Clear();
            if (ItemCardMath.ShowsFood(f))
            {
                // Nutrition, and how many colonist-days it is.
                string nut = W("CC_ItemCard_NutritionEach", ItemCardMath.N(f.Nutrition).Named("NUT"));
                if (f.InHand && f.Count > 1)
                    nut += " · " + W("CC_ItemCard_NutritionStack", ItemCardMath.N(f.Nutrition * f.Count).Named("NUT"), ItemCardMath.N((float)Math.Round(ItemCardMath.ColonistDays(f.Nutrition, f.Count), 1)).Named("DAYS"));
                else if (f.Nutrition > 0f && f.Nutrition < ItemCardMath.MealNutrition - 0.0001f) nut += " · " + W("CC_ItemCard_ToAMeal", ItemCardMath.ToAMeal(f.Nutrition).Named("N"));
                if (!f.HumanEdible) nut += " · " + W("CC_ItemCard_AnimalsOnly");
                m.food.Add((W("CC_ItemCard_Row_Nutrition"), nut));
                m.food.Add((W("CC_ItemCard_Row_Keeps"), Keeps(m)));
                // Mood: the game's thought.
                m.food.Add((W("CC_ItemCard_Row_Mood"), f.TasteMood != 0f
                    ? W("CC_ItemCard_MoodThought", f.TasteThought.Named("THOUGHT"), ItemCardMath.Mood(f.TasteMood).Named("MOOD"), ItemCardMath.Days(f.TasteDays).Named("DAYS"))
                    : W("CC_ItemCard_MoodNone")));
                // Food poisoning.
                float odds = ItemCardMath.PoisonOdds(f);
                string poison = f.Rotting && odds >= 1f ? W("CC_ItemCard_PoisonRotting")
                    : f.InHand && f.PoisonChance > 0f && f.PoisonChance >= f.PoisonFixed ? W("CC_ItemCard_PoisonStack", ItemCardMath.Pct(odds).Named("CHANCE"))
                    : f.PoisonFixed > 0f ? W("CC_ItemCard_PoisonKind", ItemCardMath.Pct(odds).Named("CHANCE"))
                    : p.poisonable && f.InHand ? W("CC_ItemCard_PoisonStackNone")
                    : p.poisonable && m.cookPoison != null ? m.cookPoison
                    : p.poisonable ? W("CC_ItemCard_PoisonByCook") : W("CC_ItemCard_PoisonNone");
                m.food.Add((W("CC_ItemCard_Row_Poison"), poison));
                // The road.
                m.food.Add((W("CC_ItemCard_Row_Road"), !p.travelFood ? W("CC_ItemCard_RoadNot")
                    : f.TravelRank >= 2 ? W("CC_ItemCard_RoadFirst")
                    : f.TravelRank == 1 ? W("CC_ItemCard_RoadEarly", ItemCardMath.Days(f.RotDays).Named("DAYS"))
                    : f.RotDays < 0f ? W("CC_ItemCard_RoadKeeps")
                    : W("CC_ItemCard_RoadShort", ItemCardMath.Days(f.RotDays).Named("DAYS"))));
                if (f.FeastDish) m.food.Add((W("CC_ItemCard_Row_Feast"), W("CC_ItemCard_FeastDish")));
            }

            m.effects.Clear();
            m.drug.Clear();
            if (ItemCardMath.ShowsDrug(f))
            {
                m.effects.AddRange(p.effects);
                if (f.Joy > 0f) m.drug.Add((W("CC_ItemCard_Row_Joy"), W("CC_ItemCard_Joy", f.Joy.ToStringPercent().Named("JOY"))));
                m.drug.Add((W("CC_ItemCard_Row_Addiction"), !f.Addictive || f.Addictiveness <= 0f ? W("CC_ItemCard_NotAddictive")
                    : f.MinTolerance > 0.0001f ? W("CC_ItemCard_AddictiveAfter", ItemCardMath.Pct(f.Addictiveness).Named("CHANCE"), ItemCardMath.Pct(f.MinTolerance).Named("TOLERANCE"))
                    : W("CC_ItemCard_AddictiveEach", ItemCardMath.Pct(f.Addictiveness).Named("CHANCE"))));
                if (p.tolerancePerDose > 0f)
                    m.drug.Add((W("CC_ItemCard_Row_Tolerance"), p.toleranceFall > 0f
                        ? W("CC_ItemCard_Tolerance", ItemCardMath.Pct(p.tolerancePerDose).Named("DOSE"), ItemCardMath.Pct(p.toleranceFall).Named("FALL"))
                        : W("CC_ItemCard_ToleranceStays", ItemCardMath.Pct(p.tolerancePerDose).Named("DOSE"))));
                m.drug.Add((W("CC_ItemCard_Row_Overdose"), f.OverdoseMax <= 0f && f.LargeOverdose <= 0f ? W("CC_ItemCard_NoOverdose")
                    : W("CC_ItemCard_Overdose", (Math.Abs(f.OverdoseMax - f.OverdoseMin) < 0.005f ? ItemCardMath.Pct(f.OverdoseMax) : ItemCardMath.Pct(f.OverdoseMin) + " to " + ItemCardMath.Pct(f.OverdoseMax)).Named("DOSE"))
                      + (f.LargeOverdose > 0f ? " " + W("CC_ItemCard_OverdoseLarge", ItemCardMath.Pct(f.LargeOverdose).Named("CHANCE")) : "")));
                m.drug.Add((W("CC_ItemCard_Row_Toast"), f.ToastDrink ? W(f.Alcohol ? "CC_ItemCard_ToastAlcohol" : "CC_ItemCard_ToastSoft") : W("CC_ItemCard_ToastNot")));
                if (f.Gift) m.drug.Add((W("CC_ItemCard_Row_Gift"), W("CC_ItemCard_GiftDrink")));
                if (f.RotDays > 0f) m.drug.Add((W("CC_ItemCard_Row_Keeps"), Keeps(m)));
            }

            m.medicine.Clear();
            if (f.Kind == ItemKind.Medicine)
            {
                m.medicine.Add((W("CC_ItemCard_Row_Potency"), W("CC_ItemCard_Potency", f.Potency.ToStringPercent().Named("POTENCY"), f.QualityMax.ToStringPercent().Named("MAX"))));
                int tier = ItemCardMath.CareTier(f);
                string care = tier <= MedicineChoiceMath.Herbal ? MedicalCareCategory.HerbalOrWorse.GetLabel() : tier == MedicineChoiceMath.Normal ? MedicalCareCategory.NormalOrWorse.GetLabel() : MedicalCareCategory.Best.GetLabel();
                m.medicine.Add((W("CC_ItemCard_Row_Care"), W("CC_ItemCard_Care", care.Named("CARE"))));
                // Herbal medicine rots in time (150 days in the base game).
                if (f.RotDays > 0f) m.medicine.Add((W("CC_ItemCard_Row_Keeps"), Keeps(m)));
                m.bestUse = W(!f.MedicineByNeed ? "CC_ItemCard_UseGame" : tier <= MedicineChoiceMath.Herbal ? "CC_ItemCard_UseMinor" : tier == MedicineChoiceMath.Normal ? "CC_ItemCard_UseSerious" : "CC_ItemCard_UseBest");
                if (f.Potency < f.IndustrialPotency - 0.0001f) m.bestUse += " " + W("CC_ItemCard_UseSurgeryRisk");
            }
        }

        /// <summary>How it keeps: its kind (never, or after so many days above 10 °C), and this stack where it lies.</summary>
        private static string Keeps(ItemCardModel m)
        {
            var f = m.facts;
            string keeps = f.RotDays < 0f ? W("CC_ItemCard_NeverRots") : W("CC_ItemCard_RotsAfter", ItemCardMath.Days(f.RotDays).Named("DAYS"));
            if (f.InHand && f.RotDays > 0f)
                keeps += " " + (f.Rotting ? W("CC_ItemCard_RottingNow") : f.FrozenHere ? W("CC_ItemCard_FrozenHere")
                    : f.DaysLeftHere >= 0f ? W("CC_ItemCard_LeftHere", ItemCardMath.Days(f.DaysLeftHere).Named("DAYS"), TempHere(m.thing).Named("TEMP")) : "");
            return keeps.Trim();
        }

        /// <summary>The temperature where a thing lies, in the player's units.</summary>
        private static string TempHere(Thing t)
        {
            try
            {
                return t.AmbientTemperature.ToStringTemperature("F0");
            }
            catch
            {
                return "?";
            }
        }

        // ---------------------------------------------------------------- the colony

        private static void Colony(ItemCardModel m, DefPart p)
        {
            var f = m.facts;
            var map = m.map;
            f.HasColony = map != null;
            m.eaters.Clear();
            m.takers.Clear();
            m.faiths.Clear();
            m.doctors.Clear();
            m.stock.Clear();
            m.making.Clear();
            m.trade.Clear();
            m.market.Clear();
            m.eatersNote = null;
            m.cookPoison = null;
            m.addicted = 0;
            f.Grown = f.ForbiddenFor = f.PolicyAllows = f.PolicyOf = 0;
            f.ForbiddenWho = "";
            f.FoodDays = -1f;
            f.MedicineHeld = -1;
            f.People = 0;
            f.ColdStore = true;
            f.Cookable = false;
            f.CookAt = "";
            // The cooking: a recipe the colony has unlocked, and its bench (one built here first).
            foreach (var r in p.cooks)
            {
                bool open;
                try
                {
                    open = r.AvailableNow;
                }
                catch
                {
                    open = false;
                }
                if (!open) continue;
                f.Cookable = true;
                var users = Avail.RecipeUsers(r).ToList();
                var bench = users.FirstOrDefault(u => map != null && map.listerBuildings.AllBuildingsColonistOfDef(u).Any()) ?? users.FirstOrDefault(u => u.IsResearchFinished) ?? users.FirstOrDefault();
                f.CookAt = bench?.label ?? "";
                if (map == null || bench != null && map.listerBuildings.AllBuildingsColonistOfDef(bench).Any()) break;
            }
            if (map != null)
            {
                var people = map.mapPawns.FreeColonistsSpawned.Where(x => x.RaceProps.Humanlike && !x.DevelopmentalStage.Baby()).OrderBy(x => x.LabelShort).ToList();
                f.People = people.Count;
                if (ItemCardMath.ShowsFood(f) && f.HumanEdible) Eaters(m, p, people);
                if (ItemCardMath.ShowsDrug(f)) Takers(m, p, people);
                if (f.Kind == ItemKind.Medicine) Doctors(m, people);
                if (p.poisonable) m.cookPoison = BestCook(people);
                try
                {
                    f.ColdStore = RoomUtil.HasFreezer(map) || map.mapTemperature.OutdoorTemp <= 0f;
                }
                catch
                {
                    f.ColdStore = true;
                }
                // Materials, art and furniture in a crate have their own stock (ColonyMaterial, ColonyArt, ColonyUse); a material's
                // makers are among its sources; furniture is built, not made at a bench.
                if (f.Kind != ItemKind.Material && f.Kind != ItemKind.Art && f.Kind != ItemKind.Furniture) Stock(m, p, people.Count);
                if (f.Kind != ItemKind.Art && f.Kind != ItemKind.Furniture) Making(m, p);
            }
            Trade(m, p);
        }

        /// <summary>Why one won't eat a kind (no thing in hand): their nature, their food policy, their beliefs, or a bad thought of their own (a precept's, a trait's).</summary>
        private static string WhyNotKind(Pawn x, ThingDef d, out string thought)
        {
            thought = null;
            try
            {
                if (!FoodUtility.WillEat(x, d)) return "wont";
            }
            catch
            {
                // Read as willing.
            }
            try
            {
                var policy = x.foodRestriction?.GetCurrentRespectedRestriction(x);
                if (policy != null && !policy.Allows(d)) return "policy";
            }
            catch
            {
                // No policy to read.
            }
            var facts = Beliefs.Facts(d);
            facts.badMemory = false;
            string why = BeliefMath.WhyNotEat(Beliefs.Mask(x), facts);
            if (why != null) return why;
            thought = OwnBadThought(x, null, d);
            return thought != null ? "memory" : null;
        }

        /// <summary>
        /// A bad thought eating it gives this one in particular (a precept's, a trait's, what it is made of), its label; the
        /// kind's own taste and the raw or rotten mood everyone shares are not refusals.
        /// </summary>
        private static string OwnBadThought(Pawn x, Thing t, ThingDef d)
        {
            if (x?.needs?.mood == null) return null;
            try
            {
                var thoughts = FoodUtility.ThoughtsFromIngesting(x, t, d);
                if (thoughts == null) return null;
                foreach (var th in thoughts)
                {
                    var td = th.thought;
                    if (td == null || td == d.ingestible?.tasteThought || td == ThoughtDefOf.AteRawFood || td == ThoughtDefOf.AteRottenFood) continue;
                    if (!Beliefs.Bad(td)) continue;
                    return (td.stages?.FirstOrDefault(s => s != null)?.label ?? td.label ?? td.defName).CapitalizeFirst();
                }
            }
            catch
            {
                // A kind without a thing to eat: no thoughts of its own read.
            }
            return null;
        }

        /// <summary>The mood eating it gives this one, by the game's own sum (0 when it can't be read for a kind).</summary>
        private static float MoodFor(Pawn x, Thing t, ThingDef d, float fallback)
        {
            if (x?.needs?.mood == null) return 0f;
            try
            {
                return FoodUtility.MoodFromIngesting(x, t, d);
            }
            catch
            {
                return fallback;
            }
        }

        private static readonly HashSet<string> NotRefusals = new HashSet<string> { "policy", "inedible" };

        /// <summary>Who at home eats it, with the mood it gives them, and who won't, with why.</summary>
        private static void Eaters(ItemCardModel m, DefPart p, List<Pawn> people)
        {
            var f = m.facts;
            var who = new List<string>();
            var wont = new List<ItemPawnLine>();
            int eat = 0;
            foreach (var x in people)
            {
                string thought = null;
                string why;
                try
                {
                    why = m.thing != null ? Beliefs.WhyNotEat(x, m.thing) : WhyNotKind(x, m.def, out thought);
                    // A shared mood (raw or rotten) is no refusal: they eat it all the same.
                    if (why == "memory" && m.thing != null)
                    {
                        thought = OwnBadThought(x, m.thing, m.def);
                        if (thought == null) why = null;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("the item card: who eats it", e);
                    why = null;
                }
                bool grown = !x.DevelopmentalStage.Juvenile();
                if (grown) f.Grown++;
                if (why == null)
                {
                    eat++;
                    float mood = MoodFor(x, m.thing, m.def, f.TasteMood);
                    m.eaters.Add(new ItemPawnLine
                    {
                        pawn = x, tone = mood > 0.05f ? 1 : mood < -0.05f ? -1 : 0,
                        text = Math.Abs(mood) > 0.05f ? W("CC_ItemCard_EatsMood", ItemCardMath.Mood(mood).Named("MOOD")) : W("CC_ItemCard_Eats")
                    });
                    continue;
                }
                string words = why == "memory" && thought != null ? W("CC_ItemCard_Why_memory", thought.Named("THOUGHT")) : W("CC_ItemCard_Why_" + why);
                wont.Add(new ItemPawnLine { pawn = x, tone = NotRefusals.Contains(why) ? 0 : -1, text = W("CC_ItemCard_Wont", words.Named("WHY")) });
                if (grown && !NotRefusals.Contains(why))
                {
                    f.ForbiddenFor++;
                    if (who.Count < 4) who.Add(x.LabelShort + " (" + (why == "memory" && thought != null ? thought.UncapitalizeFirst() : W("CC_ItemCard_WhyShort_" + why)) + ")");
                }
            }
            if (f.ForbiddenFor > who.Count) who.Add(W("CC_ItemCard_AndMore", (f.ForbiddenFor - who.Count).Named("N")));
            f.ForbiddenWho = string.Join(", ", who);
            // Those who eat it first, then those who won't.
            m.eaters.AddRange(wont);
            m.eatersNote = eat == people.Count ? W("CC_ItemCard_AllEat") : eat == 0 ? W("CC_ItemCard_NoneEat") : W("CC_ItemCard_SomeEat", eat.Named("EAT"), people.Count.Named("ALL"));
        }

        /// <summary>Each colonist's drug policy for it, who is addicted, and what each faith at home holds on it.</summary>
        private static void Takers(ItemCardModel m, DefPart p, List<Pawn> people)
        {
            var f = m.facts;
            var chem = p.drug?.chemical;
            var who = new List<string>();
            foreach (var x in people)
            {
                bool grown = !x.DevelopmentalStage.Juvenile();
                if (grown) f.Grown++;
                string text;
                int tone = 0;
                bool addicted = false;
                try
                {
                    addicted = chem != null && AddictionUtility.IsAddicted(x, chem);
                }
                catch
                {
                    // Read as not.
                }
                DrugPolicyEntry entry = null;
                string policy = null;
                try
                {
                    var pol = x.drugs?.CurrentPolicy;
                    policy = pol?.label;
                    if (pol != null)
                        for (int i = 0; i < pol.Count; i++)
                            if (pol[i]?.drug == m.def)
                            {
                                entry = pol[i];
                                break;
                            }
                }
                catch
                {
                    // No policy to read.
                }
                bool faith = Beliefs.FaithForbidsDrug(x, m.def);
                if (x.drugs != null)
                {
                    f.PolicyOf++;
                    if (entry != null && (entry.allowedForJoy || entry.allowScheduled)) f.PolicyAllows++;
                }
                if (faith && grown)
                {
                    f.ForbiddenFor++;
                    if (who.Count < 4) who.Add(x.LabelShort);
                }
                if (entry == null) text = W("CC_ItemCard_PolicyNone", (policy ?? "-").Named("POLICY"));
                else if (entry.allowScheduled)
                {
                    text = W("CC_ItemCard_PolicySchedule", policy.Named("POLICY"), ItemCardMath.N(entry.daysFrequency).Named("DAYS"));
                    tone = 1;
                }
                else if (entry.allowedForJoy)
                {
                    text = W("CC_ItemCard_PolicyJoy", policy.Named("POLICY"));
                    tone = 1;
                }
                else if (entry.allowedForAddiction) text = W("CC_ItemCard_PolicyAddiction", policy.Named("POLICY"));
                else text = W("CC_ItemCard_PolicyNo", policy.Named("POLICY"));
                if (faith)
                {
                    text += " · " + W("CC_ItemCard_FaithForbids");
                    tone = -1;
                }
                if (addicted)
                {
                    text += " · " + W("CC_ItemCard_Addicted");
                    tone = -1;
                    m.addicted++;
                }
                m.takers.Add(new ItemPawnLine { pawn = x, text = text, tone = tone });
            }
            if (f.ForbiddenFor > who.Count) who.Add(W("CC_ItemCard_AndMore", (f.ForbiddenFor - who.Count).Named("N")));
            f.ForbiddenWho = string.Join(", ", who);
            // Each faith at home and its precepts on drugs and drink.
            if (!ModsConfig.IdeologyActive) return;
            var seen = new HashSet<Ideo>();
            foreach (var x in people)
            {
                Ideo ideo;
                try
                {
                    ideo = x.Ideo;
                }
                catch
                {
                    continue;
                }
                if (ideo == null || !seen.Add(ideo)) continue;
                var views = new List<string>();
                foreach (var pr in ideo.PreceptsListForReading)
                {
                    string issue = pr?.def?.issue?.defName ?? "";
                    if (issue.IndexOf("Drug", StringComparison.OrdinalIgnoreCase) < 0 && issue.IndexOf("Alcohol", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    views.Add(pr.LabelCap);
                }
                int held = people.Count(o => SameIdeo(o, ideo));
                m.faiths.Add(W("CC_ItemCard_FaithLine", ideo.name.Named("FAITH"), held.Named("N"),
                    (views.Count > 0 ? string.Join(", ", views) : W("CC_ItemCard_FaithSilent")).Named("VIEWS")));
            }
        }

        private static bool SameIdeo(Pawn x, Ideo ideo)
        {
            try
            {
                return x.Ideo == ideo;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>The doctors at home, the best first: their tending with this medicine and without any.</summary>
        private static void Doctors(ItemCardModel m, List<Pawn> people)
        {
            var f = m.facts;
            var doctors = new List<(Pawn p, float q)>();
            foreach (var x in people)
            {
                if (x.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) || x.workSettings == null || !x.workSettings.WorkIsActive(WorkTypeDefOf.Doctor)) continue;
                float q;
                try
                {
                    q = x.GetStatValue(StatDefOf.MedicalTendQuality);
                }
                catch
                {
                    continue;
                }
                doctors.Add((x, q));
            }
            foreach (var (x, q) in doctors.OrderByDescending(d => d.q).Take(5))
            {
                float with = ItemCardMath.TendQuality(q, f.Potency, f.QualityMax);
                float without = ItemCardMath.TendQuality(q, ItemCardMath.NoMedicinePotency, ItemCardMath.NoMedicineQualityMax);
                float industrial = ItemCardMath.TendQuality(q, f.IndustrialPotency, ThingDefOf.MedicineIndustrial != null ? Stat(ThingDefOf.MedicineIndustrial, StatDefOf.MedicalQualityMax) : 1f);
                int level = x.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;
                string text = W("CC_ItemCard_DoctorLine", level.Named("LEVEL"), with.ToStringPercent().Named("WITH"), without.ToStringPercent().Named("WITHOUT"));
                if (industrial > with + 0.05f && f.Potency < f.IndustrialPotency) text += " · " + W("CC_ItemCard_DoctorCapped", industrial.ToStringPercent().Named("BETTER"));
                m.doctors.Add(new ItemPawnLine { pawn = x, text = text, tone = with >= 0.8f ? 1 : with < 0.5f ? -1 : 0 });
            }
        }

        /// <summary>The colony's best cook and the chance of food poisoning in a meal of theirs, or null with no cook.</summary>
        private static string BestCook(List<Pawn> people)
        {
            Pawn best = null;
            float chance = 1f;
            var cooking = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking");
            foreach (var x in people)
            {
                if (cooking != null && (x.WorkTypeIsDisabled(cooking) || x.workSettings == null || !x.workSettings.WorkIsActive(cooking))) continue;
                float c;
                try
                {
                    c = x.GetStatValue(StatDefOf.FoodPoisonChance);
                }
                catch
                {
                    continue;
                }
                if (best == null || c < chance)
                {
                    best = x;
                    chance = c;
                }
            }
            if (best == null) return null;
            float factor = Find.Storyteller?.difficulty?.foodPoisonChanceFactor ?? 1f;
            return W("CC_ItemCard_PoisonCook", best.LabelShort.Named("COOK"), ItemCardMath.Pct(chance * factor).Named("CHANCE"));
        }

        /// <summary>How many the colony holds, its days of food or its medicine, and whether it is short.</summary>
        private static void Stock(ItemCardModel m, DefPart p, int people)
        {
            var f = m.facts;
            var map = m.map;
            int have = 0;
            var player = Faction.OfPlayer;
            foreach (var t in map.listerThings.ThingsOfDef(m.def))
                if (t != null && t.Spawned && !t.IsForbidden(player)) have += t.stackCount;
            foreach (var x in map.mapPawns.FreeColonistsSpawned)
            {
                var inv = x.inventory?.innerContainer;
                if (inv == null) continue;
                foreach (var t in inv)
                    if (t.def == m.def) have += t.stackCount;
            }
            m.stock.Add((W("CC_ItemCard_Row_Held"), W("CC_ItemCard_Held", have.Named("N"))));
            if (ItemCardMath.ShowsFood(f) && f.HumanEdible)
            {
                f.FoodDays = MapFacts.Of(map)?.FoodDays ?? -1f;
                float mine = people > 0 ? ItemCardMath.ColonistDays(f.Nutrition, have) / people : 0f;
                string days = f.FoodDays >= 999f ? W("CC_ItemCard_FoodNoEaters")
                    : W("CC_ItemCard_FoodDays", ItemCardMath.Days(Mathf.Max(0f, f.FoodDays)).Named("DAYS"), ItemCardMath.Days(Mathf.Max(0f, mine)).Named("MINE"));
                string verdict = ItemCardMath.FoodShort(f.FoodDays) ? W("CC_ItemCard_FoodShort", ItemCardMath.N(TradeAdviceMath.FoodDaysAtLeast).Named("LEAST"))
                    : ItemCardMath.FoodSpare(f.FoodDays) ? W("CC_ItemCard_FoodSpare") : W("CC_ItemCard_FoodEnough");
                m.stock.Add((W("CC_ItemCard_Row_Food"), days + " " + verdict));
            }
            if (f.Kind == ItemKind.Medicine)
            {
                f.MedicineHeld = Avail.MedicineCount(map);
                int need = ItemCardMath.MedicineNeed(people), keep = ItemCardMath.MedicineKeep(people);
                string verdict = ItemCardMath.MedicineShort(f.MedicineHeld, people) ? W("CC_ItemCard_MedShort", need.Named("NEED"))
                    : ItemCardMath.MedicineSpare(f.MedicineHeld, people) ? W("CC_ItemCard_MedSpare", keep.Named("KEEP")) : W("CC_ItemCard_MedEnough", need.Named("NEED"));
                m.stock.Add((W("CC_ItemCard_Row_Medicine"), W("CC_ItemCard_MedHeld", f.MedicineHeld.Named("N"), people.Named("PEOPLE")) + " " + verdict));
            }
            if (ItemCardMath.ShowsDrug(f))
            {
                if (m.addicted > 0) m.stock.Add((W("CC_ItemCard_Row_Addicted"), W("CC_ItemCard_AddictedCount", m.addicted.Named("N"))));
            }
        }

        /// <summary>Where it can be made or grown: bills on the colony's benches (a click goes to the bench and its bills), a bench without one, the research it waits on.</summary>
        private static void Making(ItemCardModel m, DefPart p)
        {
            var map = m.map;
            foreach (var r in p.makes)
            {
                if (m.making.Count >= 6) break;
                bool open;
                try
                {
                    open = r.AvailableNow;
                }
                catch
                {
                    open = false;
                }
                if (!open)
                {
                    var need = r.researchPrerequisite ?? r.researchPrerequisites?.FirstOrDefault(x => x != null && !x.IsFinished);
                    m.making.Add(new ItemGoLine { text = W("CC_ItemCard_MakeResearch", r.LabelCap.Named("RECIPE"), (need?.LabelCap.ToString() ?? "?").Named("RESEARCH")), tone = 0 });
                    continue;
                }
                bool any = false;
                foreach (var benchDef in Avail.RecipeUsers(r))
                {
                    foreach (var b in map.listerBuildings.AllBuildingsColonistOfDef(benchDef).OfType<Building_WorkTable>())
                    {
                        var bill = b.BillStack?.Bills?.OfType<Bill_Production>().FirstOrDefault(x => x.recipe == r && !x.DeletedOrDereferenced);
                        if (bill == null) continue;
                        any = true;
                        var bench = b;
                        m.making.Add(new ItemGoLine
                        {
                            text = W(bill.suspended ? "CC_ItemCard_MakeBillPaused" : "CC_ItemCard_MakeBill", r.LabelCap.Named("RECIPE"), b.LabelCap.Named("BENCH"), bill.RepeatInfoText.Named("REPEAT")),
                            tip = W("CC_ItemCard_GoBench_Tip"), go = () => GoToBench(bench), tone = bill.suspended ? 0 : 1
                        });
                        if (m.making.Count >= 6) break;
                    }
                    if (m.making.Count >= 6) break;
                }
                if (any) continue;
                var free = Avail.RecipeUsers(r).SelectMany(u => map.listerBuildings.AllBuildingsColonistOfDef(u)).OfType<Building_WorkTable>().FirstOrDefault();
                if (free != null)
                    m.making.Add(new ItemGoLine
                    {
                        text = W("CC_ItemCard_MakeNoBill", r.LabelCap.Named("RECIPE"), free.LabelCap.Named("BENCH")), tip = W("CC_ItemCard_GoBench_Tip"), go = () => GoToBench(free), tone = 0
                    });
                else
                    m.making.Add(new ItemGoLine
                    {
                        text = W("CC_ItemCard_MakeNoBench", r.LabelCap.Named("RECIPE"), Find.ActiveLanguageWorker.WithIndefiniteArticle(Avail.RecipeUsers(r).FirstOrDefault()?.label ?? "?").Named("BENCH")), tone = 0
                    });
            }
            foreach (var crop in p.crops)
            {
                var zones = map.zoneManager.AllZones.OfType<Zone_Growing>().Where(z => z.GetPlantDefToGrow() == crop).ToList();
                bool sowable = crop.plant.sowResearchPrerequisites == null || crop.plant.sowResearchPrerequisites.All(x => x == null || x.IsFinished);
                var first = zones.FirstOrDefault();
                m.making.Add(new ItemGoLine
                {
                    text = zones.Count > 0 ? W("CC_ItemCard_GrowsIn", crop.LabelCap.Named("CROP"), zones.Count.Named("N"))
                        : W(sowable ? "CC_ItemCard_GrowsFrom" : "CC_ItemCard_GrowsLater", crop.LabelCap.Named("CROP")),
                    tip = first != null ? W("CC_ItemCard_GoField_Tip") : null,
                    go = first != null ? () => GoToField(first) : (Action)null, tone = zones.Count > 0 ? 1 : 0
                });
            }
            if (m.making.Count == 0) m.making.Add(new ItemGoLine { text = W("CC_ItemCard_MakeNone") });
        }

        private static void GoToBench(Building_WorkTable b)
        {
            if (b == null || !b.Spawned) return;
            try
            {
                Pages.LeaveForMap();
                CameraJumper.TryJumpAndSelect(b);
                if (Find.Selector.SingleSelectedThing == b) InspectPaneUtility.OpenTab(typeof(ITab_Bills));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the item card going to a bench", e);
            }
        }

        private static void GoToField(Zone_Growing z)
        {
            if (z?.Map == null || z.Cells.Count == 0) return;
            try
            {
                Pages.LeaveForMap();
                CameraJumper.TryJump(z.Cells[z.Cells.Count / 2], z.Map);
                Find.Selector.ClearSelection();
                Find.Selector.Select(z);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the item card going to a field", e);
            }
        }

        /// <summary>Its worth, who deals in it and what they pay, Vanilla Trading Expanded's market and the colony's own last prices.</summary>
        private static void Trade(ItemCardModel m, DefPart p)
        {
            float each = m.thing?.MarketValue ?? p.value;
            m.trade.Add((W("CC_ItemCard_Row_Value"), m.thing != null && m.thing.stackCount > 1
                ? W("CC_ItemCard_ValueStack", each.ToStringMoney().Named("EACH"), (each * m.thing.stackCount).ToStringMoney().Named("ALL"))
                : each.ToStringMoney()));
            float perKg = ItemCardMath.ValuePerKg(new ItemFacts { Value = each, Mass = p.mass });
            if (p.mass > 0f) m.trade.Add((W("CC_ItemCard_Row_PerKg"), W("CC_ItemCard_PerKg", p.mass.ToStringMass().Named("MASS"), perKg.ToStringMoney().Named("PERKG"))));
            if (!p.tradeable) m.trade.Add((W("CC_ItemCard_Row_Traders"), W("CC_ItemCard_NotTraded")));
            else if (p.traders.Count == 0) m.trade.Add((W("CC_ItemCard_Row_Traders"), W("CC_ItemCard_NoTraders")));
            else
            {
                var names = p.traders.Select(t => t.label).Take(6).ToList();
                if (p.traders.Count > 6) names.Add(W("CC_ItemCard_AndMore", (p.traders.Count - 6).Named("N")));
                m.trade.Add((W("CC_ItemCard_Row_Traders"), string.Join(", ", names)));
                // Where the game's price types differ between traders: who asks least for it, who pays most for it.
                var cheapest = p.traders.OrderBy(t => t.asks).First();
                if (p.traders.Any(t => t.asks > cheapest.asks + 0.01f))
                    m.trade.Add((W("CC_ItemCard_Row_Cheapest"), W("CC_ItemCard_Cheapest", cheapest.label.Named("TRADER"), cheapest.asks.ToStringPercent().Named("ASKS"))));
                var best = p.traders.OrderByDescending(t => t.pays).First();
                if (p.traders.Any(t => t.pays < best.pays - 0.01f))
                    m.trade.Add((W("CC_ItemCard_Row_BestPay"), W("CC_ItemCard_BestPay", best.label.Named("TRADER"), best.pays.ToStringPercent().Named("PAYS"))));
            }
            // Vanilla Trading Expanded's market for it, read as the trade window reads it.
            try
            {
                if (VanillaTrading.Market(m.def, out var history, out float now, out float usual))
                {
                    var view = new MarketView { def = m.def, stuffed = m.def.MadeFromStuff, stats = TradeMarketMath.Stats(history, now, usual) };
                    view.market = view.stats.known;
                    m.market.AddRange(TradeMarket.MarketLines(view));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the item card reading the market", e);
            }
            try
            {
                int quality = m.thing != null && m.thing.TryGetQuality(out var q) ? (int)q : -1;
                m.market.AddRange(TradeMarket.RecordLines(m.def, m.stuff, quality));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the item card reading the colony's trades", e);
            }
        }
    }
}
