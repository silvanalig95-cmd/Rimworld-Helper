using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Rimfolk events told from data alone: everything an event needs beyond its HappeningExtension (the land, the era,
    // what must be there, who comes) is in a HappeningSetting on the same def, and HappeningWorker_Setting does the
    // rest. The answers do their work through the usual effects (DilemmaEffect_*, and the WorldEffect_* ones that need
    // only a map), plus the few below that reach the newer systems: ailments, strain and camps.

    /// <summary>
    /// Where, when and with whom a data-only Rimfolk event happens (see <see cref="HappeningWorker_Setting"/>): the
    /// colony's era, the land it lives on (the tile's climate, a river, the coast, hills), what has to be there (a camp,
    /// a grave, crops in the fields, a colonist with an everyday ailment or worn down by strain, council meetings, tame
    /// animals), and who comes: a visitor of a friendly people, alone or with company, who walks in and stays a while
    /// or waits at the edge of the map for the answer.
    ///
    /// <para>Lines can ask for the land ([cold], [hot], [temperate], [dry], [wet], [river], [coast], [hills], [swamp],
    /// [desert], [forest], [jungle], [tundra]), for [camp] and [grave], for A's ailment ([ail:cough]...) and strain
    /// ([strained], [worn], [breaking]), as well as the usual era, season and people tags. {TALE} is a line picked
    /// from <see cref="tales"/> (or, with an ailing A, the ailment's name); {PLACE} from <see cref="places"/> (or the
    /// visitor's nearest town, or the camp's spot).</para>
    /// </summary>
    public class HappeningSetting : DefModExtension
    {
        /// <summary>The colony's era, as a tag expression: "lowtech", "medieval", "industrial|spacer", "!lowtech".</summary>
        public string era;
        /// <summary>The tile's average temperature (°C).</summary>
        public float minTileTemp = -999f;
        public float maxTileTemp = 999f;
        /// <summary>The tile's yearly rainfall (mm).</summary>
        public float minRain = -1f;
        public float maxRain = 99999f;
        public bool river;
        public bool coast;
        public bool hills;
        /// <summary>
        /// What has to be there: camp (a camp on the map that will talk), grave (a colonist's grave), crops (a field
        /// under the sky), ailing (A has an everyday ailment), strained (A is worn down), meetings (the council meets),
        /// animals (the colony keeps tame animals: {PET}), herd (at least three head of livestock: {PET} is one of them),
        /// power, research (a project in hand), children, prisoner.
        /// </summary>
        public List<string> needs;

        // ---- A visitor (X), and anyone with them (ext.count in all) ----
        public bool visitor;
        /// <summary>X waits at the edge of the map until the colony answers (someone who asks to join).</summary>
        public bool waits;
        /// <summary>Where visitors stay: outside (just outside the colony), gathering (the party spot or the dining table), grave.</summary>
        public string visitAt = "outside";
        public SkillDef visitorSkill;
        public IntRange visitorLevel = new IntRange(8, 14);
        /// <summary>The visitor's people: lowtech (tribal or medieval), hightech (industrial or later), own (within one era of the colony), or any.</summary>
        public string people;

        // ---- More of the cast ----
        /// <summary>{FACTION} (and {PLACE}, their nearest town): a people on good terms with the colony, when nobody comes from them.</summary>
        public bool neighbor;
        /// <summary>{ENEMY}: a hostile people the texts name (the event needs one).</summary>
        public bool enemy;
        /// <summary>{COUNT} (the extension's amount) grows with the colony's wealth.</summary>
        public bool wealthScaled = true;
        [MustTranslate] public List<string> places;
        [MustTranslate] public List<string> tales;
        /// <summary>Answers that need something more than the event does (singing at the graves needs a grave): greyed out without it.</summary>
        public List<OptionNeed> optionNeeds;

        public bool Needs(string what) => needs != null && needs.Contains(what);
    }

    /// <summary>One answer of a <see cref="HappeningSetting"/> event that needs something there (a need as in its needs list).</summary>
    public class OptionNeed
    {
        public int option;
        public string need;
        /// <summary>What the greyed-out answer says.</summary>
        [MustTranslate] public string reason;
    }

    /// <summary>A Rimfolk event told from data: see <see cref="HappeningSetting"/>.</summary>
    public class HappeningWorker_Setting : HappeningWorker_Visitors
    {
        public HappeningSetting Setting => def.GetModExtension<HappeningSetting>() ?? Empty;

        private static readonly HappeningSetting Empty = new HappeningSetting();

        // ---------------- Can it happen? ----------------

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var s = Setting;
            if (!EraFits(s.era) || !LandFits(map, s) || !NeedsMet(map, s)) return false;
            if (s.visitor && People(s) == null) return false;
            if (s.neighbor && Neighbor() == null) return false;
            if (s.enemy && Raids.Enemy() == null) return false;
            if (ext.condition != null && map.gameConditionManager.ConditionIsActive(ext.condition)) return false;
            var pool = Candidates(map, ext, s);
            if (pool.Count == 0) return false;
            if (ext.second == null) return true;
            var everyone = Colonists(map);
            return pool.Any(a => everyone.Any(b => b != a && ext.second.Allows(b, a)));
        }

        public static bool EraFits(string era)
        {
            if (era.NullOrEmpty()) return true;
            var tags = new HashSet<string>();
            Flavor.AddEraTags(tags);
            return Flavor.Match("[" + era + "]", tags) >= 0;
        }

        private static Tile TileOf(Map map) => map?.TileInfo;

        public static bool LandFits(Map map, HappeningSetting s)
        {
            var t = TileOf(map);
            if (t == null) return s.minTileTemp <= -999f && s.maxTileTemp >= 999f && s.minRain < 0f && s.maxRain >= 99999f && !s.river && !s.coast && !s.hills;
            if (t.temperature < s.minTileTemp || t.temperature > s.maxTileTemp) return false;
            if (t.rainfall < s.minRain || t.rainfall > s.maxRain) return false;
            if (s.river && !HasRiver(map)) return false;
            if (s.coast && !IsCoastal(map)) return false;
            if (s.hills && t.hilliness < Hilliness.SmallHills) return false;
            return true;
        }

        public static bool HasRiver(Map map) => !((map?.TileInfo as SurfaceTile)?.Rivers).NullOrEmpty();

        public static bool IsCoastal(Map map)
        {
            try
            {
                return map?.TileInfo?.IsCoastal ?? false;
            }
            catch
            {
                return false;
            }
        }

        private static bool NeedsMet(Map map, HappeningSetting s)
        {
            if (s.needs.NullOrEmpty()) return true;
            foreach (var n in s.needs)
                if (!Met(map, n))
                    return false;
            return true;
        }

        /// <summary>One thing an event (or one of its answers) needs to be there.</summary>
        public static bool Met(Map map, string need)
        {
            if (map == null || need.NullOrEmpty()) return true;
            switch (need)
            {
                case "camp": return CampHere(map) != null;
                case "grave": return Graves(map).Any();
                case "crops": return GameCondition_Hail.Crops(map).Count >= 8;
                case "ailing": return AilmentTracker.On;
                case "strained": return Heart.On;
                case "meetings": return ColonyCouncilMod.Settings.enableAmbitions;
                case "animals": return Animals(map).Any();
                case "herd": return Herd(map).Count >= 3;
                case "power": return Avail.HasElectricity;
                case "research": return Find.ResearchManager?.GetProject(null) != null;
                case "children": return Colonists(map).Any(p => p.DevelopmentalStage.Child());
                case "prisoner": return map.mapPawns.PrisonersOfColonySpawned.Any();
            }
            return true;
        }

        /// <summary>A people on good terms with the colony, of its own era by preference.</summary>
        private static Faction Neighbor() => Arrivals.Friendly(VarietyUtil.OnGoodTerms);

        /// <summary>The camp on this map, if it will talk to the colony.</summary>
        public static Camp CampHere(Map map)
        {
            var c = CampTracker.Instance?.camp;
            return c != null && c.map == map && c.Settled && c.CanTalk && c.Here.Count > 0 ? c : null;
        }

        public static IEnumerable<Building_Grave> Graves(Map map) =>
            map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>()
                .Where(g => g.HasCorpse && g.Corpse?.InnerPawn is Pawn d && d.RaceProps.Humanlike && d.Faction == Faction.OfPlayer);

        public static IEnumerable<Pawn> Animals(Map map) =>
            map.mapPawns.SpawnedColonyAnimals.Where(p => p.RaceProps.Animal && !p.Dead && !p.Downed);

        /// <summary>The colony's livestock on this map, up and about (no pets, nothing downed).</summary>
        public static List<Pawn> Herd(Map map) => AnimalFacts.Herd(map).Where(p => !p.Dead && !p.Downed).ToList();

        /// <summary>A's everyday (never serious) Rimfolk ailment, or null.</summary>
        public static Hediff Ailment(Pawn p)
        {
            var hediffs = p?.health?.hediffSet?.hediffs;
            if (hediffs == null || !AilmentTracker.On) return null;
            foreach (var h in hediffs)
            {
                var info = AilmentTracker.InfoOf(h?.def);
                if (info != null && !info.serious && !info.lingering) return h;
            }
            return null;
        }

        private static StrainBand Band(Pawn p) => Heart.On ? Heart.BandOf(p) : StrainBand.Steady;

        /// <summary>The people a visitor comes from.</summary>
        private static Faction People(HappeningSetting s)
        {
            var tech = Avail.EffectiveTech;
            switch (s.people)
            {
                case "lowtech": return Arrivals.Friendly(f => f.def.techLevel <= TechLevel.Medieval);
                case "hightech": return Arrivals.Friendly(f => f.def.techLevel >= TechLevel.Industrial);
                case "own": return Arrivals.Friendly(f => Mathf.Abs((int)f.def.techLevel - (int)tech) <= 1);
                default: return Arrivals.Friendly();
            }
        }

        /// <summary>Who A can be: the role, and what the event needs of them (an ailment, strain).</summary>
        private static List<Pawn> Candidates(Map map, HappeningExtension ext, HappeningSetting s)
        {
            IEnumerable<Pawn> pool = Colonists(map);
            pool = ext.colonist != null ? pool.Where(p => ext.colonist.Allows(p, null)) : pool.Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.InMentalState);
            if (s.Needs("ailing")) pool = pool.Where(p => Ailment(p) != null);
            if (s.Needs("strained")) pool = pool.Where(p => Band(p) >= StrainBand.Strained);
            return pool.ToList();
        }

        // ---------------- Who ----------------

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var s = Setting;
            var map = ctx.map;
            var pool = Candidates(map, ext, s);
            if (pool.Count == 0) return false;
            var everyone = Colonists(map);
            // A: the most worn, the best at the role's skill, or anyone; and B, when the event has one.
            IEnumerable<Pawn> order;
            if (s.Needs("strained")) order = pool.OrderByDescending(p => Heart.StrainOf(p));
            else if (ext.colonist?.bestAt != null) order = pool.OrderByDescending(p => p.skills?.GetSkill(ext.colonist.bestAt)?.Level ?? 0);
            else order = pool.InRandomOrder();
            foreach (var a in order)
            {
                if (ext.second == null)
                {
                    ctx.a = a;
                    break;
                }
                var b = everyone.Where(p => p != a && ext.second.Allows(p, a)).RandomElementWithFallback();
                if (b == null) continue;
                ctx.a = a;
                ctx.b = b;
                break;
            }
            if (ctx.a == null) return false;
            if (s.Needs("animals")) ctx.pet = Animals(map).RandomElementWithFallback();
            else if (s.Needs("herd")) ctx.pet = Herd(map).RandomElementWithFallback();
            if (s.enemy)
            {
                ctx.enemy = Raids.Enemy();
                if (ctx.enemy == null) return false;
            }
            if (s.Needs("camp"))
            {
                var camp = CampHere(map);
                if (camp == null) return false;
                ctx.faction = camp.faction;
                ctx.stranger = camp.Here.Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback() ?? camp.leader;
                ctx.placeName = camp.place;
                ctx.spot = camp.center;
            }
            else if (s.visitor)
            {
                ctx.faction = People(s);
                if (ctx.faction == null) return false;
                // A visitor who waits at the edge for the answer is never a guest who's here already.
                MakeGroup(ctx, ext, Mathf.Max(1, ext.count.RandomInRange), ext.ages, null, !s.waits);
                if (ctx.stranger == null) return false;
                if (s.visitorSkill != null && !Strangers.IsHospitalityGuest(ctx.stranger)) StorytellerEvents.Skilled(ctx.stranger, s.visitorSkill, s.visitorLevel);
            }
            else if (s.neighbor)
            {
                ctx.faction = Neighbor();
                if (ctx.faction == null) return false;
            }
            if (ext.amount.max > 0)
            {
                float v = ext.amount.RandomInRange * (s.wealthScaled ? LivingEvents.WealthFactor(map) : 1f);
                ctx.count = Mathf.Max(10, Mathf.RoundToInt(v / 10f) * 10);
            }
            // {TALE}: the event's own line, or the ailment's name; {PLACE}: the event's own, or the visitor's town.
            if (!s.tales.NullOrEmpty()) ctx.tale = Pick(s.tales, ctx);
            else if (s.Needs("ailing")) ctx.tale = Ailment(ctx.a)?.LabelBase ?? "";
            if (!s.places.NullOrEmpty()) ctx.placeName = Pick(s.places, ctx);
            else if (ctx.placeName.NullOrEmpty() && ctx.faction != null) ctx.placeName = LivingEvents.TownOf(ctx.faction, map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var s = Setting;
            var map = ctx.map;
            if (ext.condition != null)
            {
                var cond = GameConditionMaker.MakeCondition(ext.condition, Mathf.RoundToInt(ext.days.RandomInRange * GenDate.TicksPerDay));
                map.gameConditionManager.RegisterCondition(cond);
            }
            if (!s.visitor || s.Needs("camp") || ctx.stranger == null) return true;
            var people = ctx.group.Prepend(ctx.stranger).Where(p => p != null).ToList();
            if (s.waits)
            {
                foreach (var p in people) Arrivals.Hold(p);
                ctx.xWaiting = true;
                return true;
            }
            IntVec3? spot = null;
            switch (s.visitAt)
            {
                case "gathering":
                    spot = GatheringSpot(map);
                    break;
                case "grave":
                    var g = Graves(map).RandomElementWithFallback();
                    if (g != null)
                    {
                        spot = g.Position;
                        ctx.place = g;
                    }
                    break;
            }
            return Arrivals.Visit(people, map, ctx.faction, spot, ext.stayHours.RandomInRange) != null;
        }

        /// <summary>Visitors who came along leave with the one who spoke for them, if the colony sends X away.</summary>
        public override void Cleanup(HappeningContext ctx)
        {
            if (ctx.xWaiting)
                foreach (var p in ctx.group.Where(p => p != null && !p.Spawned && p.Faction != Faction.OfPlayer))
                    Arrivals.Release(p);
            base.Cleanup(ctx);
        }

        /// <summary>An answer that needs something the colony doesn't have now (a grave to sing at).</summary>
        public override string Unavailable(HappeningContext ctx, int option)
        {
            var needs = Setting.optionNeeds;
            if (needs.NullOrEmpty() || ctx?.map == null) return null;
            foreach (var n in needs)
                if (n != null && n.option == option && !Met(ctx.map, n.need))
                    return n.reason.NullOrEmpty() ? "CC_Needs_Someone".Translate().Resolve() : ctx.Format(n.reason);
            return null;
        }

        // ---------------- Text ----------------

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var map = ctx.map;
            AddLandTags(map, tags);
            var s = Setting;
            if (s.visitor) tags.Add("visitor");
            if (map != null)
            {
                var camp = CampHere(map);
                if (camp != null)
                {
                    tags.Add("camp");
                    tags.Add("camp:" + camp.kind.defName.Replace("Council_Camp_", ""));
                }
                if (Graves(map).Any()) tags.Add("grave");
                if (Colonists(map).Any(p => p.DevelopmentalStage.Child())) tags.Add("children");
            }
            var ail = Ailment(ctx.a);
            if (ail != null)
            {
                string key = AilmentTracker.InfoOf(ail.def)?.key;
                if (!key.NullOrEmpty()) tags.Add("ail:" + key);
            }
            if (ctx.a != null && Heart.On)
            {
                switch (Band(ctx.a))
                {
                    case StrainBand.Strained: tags.Add("strained"); break;
                    case StrainBand.Worn: tags.Add("worn"); break;
                    case StrainBand.Breaking: tags.Add("breaking"); break;
                }
            }
            if (ctx.pet != null) tags.Add("pet:" + ctx.pet.def.defName);
        }

        /// <summary>The land's tags, read from the colony's tile (works for modded biomes too).</summary>
        public static void AddLandTags(Map map, HashSet<string> tags)
        {
            var t = TileOf(map);
            if (t == null || tags == null) return;
            try
            {
                if (t.temperature < 0f) tags.Add("cold");
                else if (t.temperature > 24f) tags.Add("hot");
                else tags.Add("temperate");
                if (t.rainfall < 600f) tags.Add("dry");
                else if (t.rainfall > 1600f) tags.Add("wet");
                if (t.swampiness > 0.5f) tags.Add("swamp");
                if (t.hilliness >= Hilliness.LargeHills) tags.Add("hills");
                if (HasRiver(map)) tags.Add("river");
                if (IsCoastal(map)) tags.Add("coast");
                string biome = t.PrimaryBiome?.defName ?? "";
                if (biome.IndexOf("Desert", StringComparison.OrdinalIgnoreCase) >= 0 || biome.IndexOf("Arid", StringComparison.OrdinalIgnoreCase) >= 0) tags.Add("desert");
                if (biome.IndexOf("Forest", StringComparison.OrdinalIgnoreCase) >= 0 && biome.IndexOf("Rain", StringComparison.OrdinalIgnoreCase) < 0) tags.Add("forest");
                if (biome.IndexOf("Tropical", StringComparison.OrdinalIgnoreCase) >= 0 || biome.IndexOf("Jungle", StringComparison.OrdinalIgnoreCase) >= 0) tags.Add("jungle");
                if (biome.IndexOf("Tundra", StringComparison.OrdinalIgnoreCase) >= 0 || biome.IndexOf("Ice", StringComparison.OrdinalIgnoreCase) >= 0) tags.Add("tundra");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("land tags", e);
            }
        }
    }

    // ===================== Effects for the newer systems =====================

    /// <summary>
    /// Eases the targets' everyday Rimfolk ailments (never a serious illness): each one's severity is cut to
    /// <see cref="factor"/> of what it was, or with <see cref="cure"/> it goes altogether.
    /// </summary>
    public class DilemmaEffect_EaseAilment : DilemmaEffect
    {
        public float factor = 0.4f;
        public bool cure;
        /// <summary>Only the first ailment each target has (the one the event is about).</summary>
        public bool one = true;

        public override void Apply(DilemmaContext ctx)
        {
            if (!AilmentTracker.On) return;
            foreach (var p in Targets(ctx).ToList())
            {
                var hediffs = p.health?.hediffSet?.hediffs;
                if (hediffs == null) continue;
                foreach (var h in hediffs.ToList())
                {
                    var info = AilmentTracker.InfoOf(h?.def);
                    if (info == null || info.serious || info.lingering) continue;
                    if (cure) p.health.RemoveHediff(h);
                    else h.Severity = Mathf.Max(0.01f, h.Severity * Mathf.Clamp01(factor));
                    if (one) break;
                }
            }
        }
    }

    /// <summary>
    /// Strain eased (or built) for the targets: <see cref="amount"/> off their strain (negative: more strain). With
    /// <see cref="onlyStrained"/>, only those already strained or worse feel it.
    /// </summary>
    public class DilemmaEffect_StrainRelief : DilemmaEffect
    {
        public float amount = 10f;
        public bool onlyStrained;

        public override void Apply(DilemmaContext ctx)
        {
            if (!Heart.On || amount == 0f) return;
            foreach (var p in Targets(ctx).ToList())
            {
                if (!Heart.Eligible(p)) continue;
                if (onlyStrained && Heart.BandOf(p) < StrainBand.Strained) continue;
                HeartLogic.Change(p, -amount);
            }
        }
    }

    /// <summary>
    /// The camp on the colony's map feels the answer: <see cref="tension"/> added (negative eases it), and A, who dealt
    /// with them, counts as a friend of the camp by <see cref="warm"/>.
    /// </summary>
    public class DilemmaEffect_CampMood : DilemmaEffect
    {
        public float tension;
        public int warm;

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.Instance?.camp;
            if (c == null || ctx.map == null || c.map != ctx.map || !c.CanTalk) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            if (tension != 0f) c.AddTension(tension);
            if (warm > 0 && ctx.a != null) c.Warm(ctx.a, warm);
        }
    }
}
