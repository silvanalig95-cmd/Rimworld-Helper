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
    /// What a data-driven wish wants and who wants it: lets new wishes be written in XML with the generic workers
    /// below (a building, a stock of something, something to wear). Things match if any of roles, defNames,
    /// nameContains or categories match (and none of notContains); <see cref="fire"/> also requires a fire.
    /// </summary>
    public class WishParams
    {
        // ---- What ----
        public List<string> roles;
        /// <summary>Exact defNames, in order of preference for what to suggest.</summary>
        public List<string> defNames;
        /// <summary>Lower-case words looked for in defName and label (modded things count without naming them).</summary>
        public List<string> nameContains;
        public List<string> notContains;
        /// <summary>Building classes (by name; subclasses count), e.g. Building_Bookcase.</summary>
        public List<string> thingClasses;
        public List<ThingCategoryDef> categories;
        public bool fire;
        public int count = 1;
        /// <summary>This many more than there are now, rather than this many in all.</summary>
        public bool more;
        /// <summary>In the colonist's own bedroom.</summary>
        public bool inBedroom;
        /// <summary>Stock wishes: don't require that the colony can make or grow it (it just has to be running low).</summary>
        public bool anySource;
        // Wear
        public List<ApparelLayerDef> layers;
        public float minColdInsulation;
        /// <summary>Excellent quality or better.</summary>
        public bool fine;
        // ---- Who ----
        public List<TraitDef> anyTrait;
        public List<TraitDef> notTrait;
        public SkillDef passion;
        /// <summary>A passion for any one of these skills.</summary>
        public List<SkillDef> passions;
        /// <summary>The colony keeps at least this many animals.</summary>
        public int minAnimals;
        public bool adult = true;
        public bool child;
        public List<Season> seasons;
        public float minDaysInColony;
        /// <summary>Has a baby or toddler of their own (Biotech).</summary>
        public bool parentOfLittleOne;
        public float weight = 1f;
        /// <summary>Why they want it ({PAWN}).</summary>
        [MustTranslate] public string reason;

        [Unsaved] private Func<ThingDef, bool> matcher;

        public bool Matches(ThingDef d)
        {
            if (d == null) return false;
            if (matcher == null) matcher = BuildMatcher();
            return matcher(d);
        }

        private Func<ThingDef, bool> BuildMatcher()
        {
            var preds = new List<Func<ThingDef, bool>>();
            if (!roles.NullOrEmpty())
                foreach (var r in roles)
                {
                    var p = RoomTemplateDef.RolePredicate(r);
                    if (p != null) preds.Add(p);
                }
            if (!defNames.NullOrEmpty())
            {
                var set = new HashSet<string>(defNames);
                preds.Add(d => set.Contains(d.defName));
            }
            if (!nameContains.NullOrEmpty()) preds.Add(d => nameContains.Any(w => NameOf(d).Contains(w)));
            if (!categories.NullOrEmpty()) preds.Add(d => d.thingCategories != null && categories.Any(c => d.IsWithinCategory(c)));
            if (!thingClasses.NullOrEmpty())
                preds.Add(d =>
                {
                    for (var t = d.thingClass; t != null && t != typeof(object); t = t.BaseType)
                        if (thingClasses.Contains(t.Name)) return true;
                    return false;
                });
            // A real fire: the game's fire overlay, or something that burns fuel for heat and light (Medieval
            // Overhaul's hearth has no overlay).
            bool hasFire(ThingDef d) => d.comps != null && (d.comps.Any(c => c is CompProperties_FireOverlay)
                                                            || (d.comps.Any(c => c is CompProperties_Refuelable) && d.comps.Any(c => c is CompProperties_HeatPusher h && h.heatPerSecond > 0f) && d.comps.Any(c => c is CompProperties_Glower)));
            return d =>
            {
                if (fire && !hasFire(d)) return false;
                if (preds.Count > 0 && !preds.Any(p => p(d))) return false;
                if (preds.Count == 0 && !fire) return false;
                if (!notContains.NullOrEmpty() && notContains.Any(w => NameOf(d).Contains(w))) return false;
                return true;
            };
        }

        private static readonly Dictionary<ThingDef, string> names = new Dictionary<ThingDef, string>();

        private static string NameOf(ThingDef d)
        {
            if (!names.TryGetValue(d, out var n)) names[d] = n = (d.defName + " " + d.label).ToLowerInvariant();
            return n;
        }

        public bool Who(Pawn p)
        {
            if (p == null) return false;
            if (child) { if (!p.DevelopmentalStage.Child()) return false; }
            else if (adult && p.DevelopmentalStage.Juvenile()) return false;
            var traits = p.story?.traits;
            if (!anyTrait.NullOrEmpty() && (traits == null || !anyTrait.Any(traits.HasTrait))) return false;
            if (!notTrait.NullOrEmpty() && traits != null && notTrait.Any(traits.HasTrait)) return false;
            if (passion != null && !People.Passionate(p.skills?.GetSkill(passion))) return false;
            if (!passions.NullOrEmpty() && !passions.Any(s => People.Passionate(p.skills?.GetSkill(s)))) return false;
            if (minAnimals > 0 && (p.MapHeld?.mapPawns.SpawnedColonyAnimals.Count ?? 0) < minAnimals) return false;
            if (!seasons.NullOrEmpty() && (p.MapHeld == null || !seasons.Contains(GenLocalDate.Season(p.MapHeld)))) return false;
            if (minDaysInColony > 0f && (p.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) < minDaysInColony * GenDate.TicksPerDay) return false;
            if (parentOfLittleOne && !(p.relations?.Children.Any(c => !c.Dead && c.DevelopmentalStage.Baby() && c.MapHeld == p.MapHeld) ?? false)) return false;
            return true;
        }

        public string Reason(Pawn p) => reason.NullOrEmpty() ? null : reason.Formatted(p.Named("PAWN")).Resolve();
    }

    /// <summary>Base for the XML-driven workers.</summary>
    public abstract class WishWorker_Params : WishWorker
    {
        protected WishParams P => def.wants ?? (def.wants = new WishParams());

        protected WishCandidate Make(Pawn pawn, float weight) => Candidate(weight * P.weight, P.Reason(pawn));
    }

    /// <summary>A building in the colony (or in their own room): a hearth, a bookcase, a training dummy, a crib.</summary>
    public class WishWorker_Have : WishWorker_Params
    {
        private int Count(Pawn p, Map map)
        {
            if (P.inBedroom) return RoomUtil.RoomCount(RoomUtil.PrivateRoom(p), P.Matches);
            return Avail.ColonyCount(map, P.Matches);
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !P.Who(pawn)) yield break;
            if (P.inBedroom && RoomUtil.PrivateRoom(pawn) == null) yield break;
            int have = Count(pawn, map);
            int target = P.more ? have + P.count : P.count;
            if (have >= target) yield break;
            var best = Avail.BestBuildable(map, P.Matches, P.defNames?.ToArray());
            if (best == null) yield break;
            var c = Make(pawn, 1f);
            c.thingDef = best;
            c.targetValue = target;
            c.baseline = have;
            c.subjectKey = P.inBedroom ? "room" : "colony";
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => Count(wish.Beneficiary, wish.Beneficiary.MapHeld) >= wish.targetValue;

        public override string ProgressText(Wish wish) =>
            wish.targetValue > 1 ? "CC_Progress_Count".Translate(Count(wish.Beneficiary, wish.Beneficiary.MapHeld), wish.targetValue.ToString("0")).ToString() : null;

        public override string OnPromised(Wish wish) => P.inBedroom && PlanInOwnRoom(wish.Beneficiary, wish.thingDef) ? "CC_Promise_Planned" : null;

        public override string TrySelfHelp(Wish wish) => P.inBedroom && PlanInOwnRoom(wish.Beneficiary, wish.thingDef) ? "CC_SelfHelp_Plan" : null;
    }

    /// <summary>A stock of something: firewood before winter, books, honey, hay for the animals, a nest egg of silver.</summary>
    public class WishWorker_Stock : WishWorker_Params
    {
        [Unsaved] private List<ThingDef> defs;

        private List<ThingDef> Defs => defs ?? (defs = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.category == ThingCategory.Item && P.Matches(d)).ToList());

        public int Count(Map map)
        {
            if (map == null) return 0;
            int n = 0;
            var home = map.areaManager.Home;
            foreach (var d in Defs)
            {
                var list = map.listerThings.ThingsOfDef(d);
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    if (t.Spawned && !t.IsForbidden(Faction.OfPlayer) && (t.IsInAnyStorage() || home[t.Position])) n += t.stackCount;
                }
            }
            // Things kept inside furniture (books on a bookcase) aren't spawned.
            foreach (var t in HeldInBuildings(map))
                if (Defs.Contains(t.def)) n += t.stackCount;
            return n;
        }

        /// <summary>Items held inside the colony's buildings (a bookcase's books), but not graves, caskets or pawns.</summary>
        public static IEnumerable<Thing> HeldInBuildings(Map map)
        {
            if (map == null) yield break;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (!(b is IThingHolder holder) || b is Building_Grave || b is Building_Casket) continue;
                var held = holder.GetDirectlyHeldThings();
                if (held == null) continue;
                for (int i = 0; i < held.Count; i++)
                    if (held[i] != null && held[i].def.category == ThingCategory.Item) yield return held[i];
            }
        }

        private int producibleMap = -1;
        private int producibleHour = -1;
        private bool producible;

        /// <summary>Can the colony make or grow any of it? Asked for every colonist on each wish roll, so remembered for the hour.</summary>
        private bool Producible(Map map)
        {
            int hour = Find.TickManager.TicksGame / GenDate.TicksPerHour;
            if (map.uniqueID != producibleMap || hour != producibleHour)
            {
                producible = FindProducible(map);
                producibleMap = map.uniqueID;
                producibleHour = hour;
            }
            return producible;
        }

        private bool FindProducible(Map map) =>
            DefDatabase<RecipeDef>.AllDefsListForReading.Any(r => r.ProducedThingDef != null && Defs.Contains(r.ProducedThingDef) && r.AvailableNow
                                                                  && Avail.RecipeUsers(r).Any(u => map.listerBuildings.ColonistsHaveBuilding(u) || Avail.CanBuild(u, map)))
            || DefDatabase<ThingDef>.AllDefsListForReading.Any(p => p.plant?.harvestedThingDef != null && Defs.Contains(p.plant.harvestedThingDef)
                                                                    && p.plant.Sowable && (p.plant.sowResearchPrerequisites == null || p.plant.sowResearchPrerequisites.All(rp => rp.IsFinished)));

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || Defs.Count == 0 || !P.Who(pawn)) yield break;
            int have = Count(map);
            if (have >= P.count) yield break;
            if (!P.anySource && !Producible(map)) yield break;
            if (P.anySource && have == 0 && !Producible(map)) yield break;
            var c = Make(pawn, 1f);
            c.targetValue = P.count;
            c.subjectKey = "colony";
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => Count(wish.Beneficiary.MapHeld) >= wish.targetValue;

        public override string ProgressText(Wish wish) => "CC_Progress_Stock".Translate(Count(wish.Beneficiary.MapHeld), wish.targetValue.ToString("0")).ToString();

        public override float Progress(Wish wish) => Mathf.Clamp01(Count(wish.Beneficiary.MapHeld) / Mathf.Max(1f, wish.targetValue));
    }

    /// <summary>Something to wear: a warm cloak for winter, a hat, something fine.</summary>
    public class WishWorker_Wear : WishWorker_Params
    {
        private bool DefFits(ThingDef d)
        {
            if (d?.apparel == null) return false;
            if (!P.layers.NullOrEmpty() && !d.apparel.layers.Any(l => P.layers.Contains(l))) return false;
            bool anyWhat = !P.roles.NullOrEmpty() || !P.defNames.NullOrEmpty() || !P.nameContains.NullOrEmpty() || !P.categories.NullOrEmpty();
            if (anyWhat && !P.Matches(d)) return false;
            if (P.minColdInsulation > 0f)
            {
                var stuff = d.MadeFromStuff ? GenStuff.DefaultStuffFor(d) : null;
                if (d.GetStatValueAbstract(StatDefOf.Insulation_Cold, stuff) < P.minColdInsulation) return false;
            }
            return true;
        }

        private bool Fits(Apparel a, Pawn p)
        {
            if (!DefFits(a.def) || (p.apparel?.WornApparel.Contains(a) == false && !ApparelUtility.HasPartsToWear(p, a.def))) return false;
            if (P.fine && (!a.TryGetQuality(out var q) || q < QualityCategory.Excellent)) return false;
            return true;
        }

        private bool Wearing(Pawn p) => p.apparel != null && p.apparel.WornApparel.Any(a => Fits(a, p));

        private bool Obtainable(Pawn p, Map map)
        {
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel))
                if (t is Apparel a && a.Spawned && !a.IsForbidden(Faction.OfPlayer) && Fits(a, p)) return true;
            if (P.fine) return DefDatabase<RecipeDef>.AllDefsListForReading.Any(r => r.ProducedThingDef != null && DefFits(r.ProducedThingDef) && r.AvailableNow
                                                                                    && Avail.RecipeUsers(r).Any(u => map.listerBuildings.ColonistsHaveBuilding(u)));
            return DefDatabase<RecipeDef>.AllDefsListForReading.Any(r => r.ProducedThingDef != null && DefFits(r.ProducedThingDef) && r.AvailableNow
                                                                         && ApparelUtility.HasPartsToWear(p, r.ProducedThingDef)
                                                                         && Avail.RecipeUsers(r).Any(u => map.listerBuildings.ColonistsHaveBuilding(u) || Avail.CanBuild(u, map)));
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || pawn.apparel == null || !P.Who(pawn) || Wearing(pawn) || !Obtainable(pawn, map)) yield break;
            var c = Make(pawn, 1f);
            c.subjectKey = pawn.ThingID;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => Wearing(wish.Beneficiary);
    }

    /// <summary>To see something of the world: fulfilled by going out with a caravan.</summary>
    public class WishWorker_Travel : WishWorker_Params
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !P.Who(pawn) || pawn.IsCaravanMember()) yield break;
            if (!Find.WorldObjects.Settlements.Any(s => s.Faction != null && !s.Faction.IsPlayer && !s.Faction.HostileTo(Faction.OfPlayer))) yield break;
            var c = Make(pawn, 1f);
            c.subjectKey = pawn.ThingID;
            yield return c;
        }

        public override void WhileAway(Wish wish)
        {
            if (!wish.Beneficiary.IsCaravanMember()) return;
            wish.baseline = 1f;
            // Off seeing the world: the wish waits for them to come home.
            wish.expiresTick = Mathf.Max(wish.expiresTick, Find.TickManager.TicksGame + GenDate.TicksPerDay);
        }

        public override bool IsFulfilled(Wish wish) => wish.baseline >= 1f;
    }

    /// <summary>
    /// Something a visiting trader has that a colonist has fallen for: a fine cloak, a blade for a fighter, a book,
    /// a sculpture. Fulfilled when that very thing ends up the colony's; gone when the trader leaves with it.
    /// </summary>
    public class WishWorker_TraderGoods : WishWorker_Params
    {
        private static IEnumerable<Pawn> Traders(Map map) => Strangers.On(map, StrangerKind.Trader);

        private static float Appeal(Pawn p, Thing t)
        {
            var d = t.def;
            if (d.IsApparel)
            {
                if (!ApparelUtility.HasPartsToWear(p, d) || (d.apparel.gender != Gender.None && d.apparel.gender != p.gender)) return 0f;
                if (!t.TryGetQuality(out var q) || q < QualityCategory.Good) return 0f;
                return 0.6f + (q >= QualityCategory.Excellent ? 0.6f : 0f) + (Standards.Of(p) > 0.5f ? 0.5f : 0f);
            }
            if (d.IsWeapon)
            {
                if (p.WorkTagIsDisabled(WorkTags.Violent)) return 0f;
                var skill = p.skills?.GetSkill(d.IsRangedWeapon ? SkillDefOf.Shooting : SkillDefOf.Melee);
                if (!People.Passionate(skill)) return 0f;
                float mine = p.equipment?.Primary?.MarketValue ?? 0f;
                return t.MarketValue > mine * 1.3f ? 1.2f : 0f;
            }
            if (d.HasComp(typeof(CompArt)) && d.category == ThingCategory.Item && People.Passionate(p.skills?.GetSkill(SkillDefOf.Artistic))) return 0.8f;
            if (d.HasComp(typeof(CompBook)) && People.Passionate(p.skills?.GetSkill(SkillDefOf.Intellectual))) return 0.9f;
            return 0f;
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !P.Who(pawn) || map.resourceCounter.Silver < 30) yield break;
            foreach (var trader in Traders(map))
            {
                var goods = trader.trader?.Goods;
                if (goods == null) continue;
                Thing best = null;
                float bestScore = 0f;
                foreach (var t in goods)
                {
                    if (t == null || t is Pawn || t.def.category != ThingCategory.Item || t.MarketValue > map.resourceCounter.Silver * 1.5f + 300f) continue;
                    float s = Appeal(pawn, t);
                    if (s > bestScore)
                    {
                        best = t;
                        bestScore = s;
                    }
                }
                if (best == null) continue;
                var c = Make(pawn, bestScore);
                c.other = trader;
                c.thingDef = best.def;
                c.subjectLabel = best.LabelNoCount;
                c.subjectKey = best.ThingID;
                yield return c;
            }
        }

        private static Thing Find(Wish wish)
        {
            var map = wish.Beneficiary.MapHeld;
            if (map == null || wish.thingDef == null) return null;
            foreach (var t in map.listerThings.ThingsOfDef(wish.thingDef))
                if (t.ThingID == wish.subjectKey) return t;
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                if (p.Faction != Faction.OfPlayer) continue;
                if (p.apparel?.WornApparel.FirstOrDefault(a => a.ThingID == wish.subjectKey) is Thing worn) return worn;
                if (p.equipment?.Primary != null && p.equipment.Primary.ThingID == wish.subjectKey) return p.equipment.Primary;
                if (p.inventory?.innerContainer.FirstOrDefault(i => i.ThingID == wish.subjectKey) is Thing inv) return inv;
            }
            foreach (var t in WishWorker_Stock.HeldInBuildings(map))
                if (t.ThingID == wish.subjectKey) return t;
            return null;
        }

        public override bool IsFulfilled(Wish wish) => Find(wish) != null;

        /// <summary>The trader left with it.</summary>
        public override bool StillValid(Wish wish) =>
            Find(wish) != null || (wish.other != null && !wish.other.Dead && wish.other.Spawned && wish.other.Map == wish.Beneficiary.MapHeld);

        public override LookTargets LookAt(Wish wish) => wish.other != null && wish.other.Spawned ? new LookTargets(wish.Beneficiary, wish.other) : base.LookAt(wish);
    }

    /// <summary>The named beast that mauled someone, hunted down while it still prowls the map.</summary>
    public class WishWorker_Beast : WishWorker_Params
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            var nem = CouncilGameComponent.Instance?.nemeses;
            if (map == null || nem == null || !ColonyCouncilMod.Settings.nemeses || !P.Who(pawn)) yield break;
            foreach (var n in nem.Open.Where(n => n.IsBeast && n.pawn != null && n.pawn.Faction == null && n.pawn.Spawned && n.pawn.Map == map))
            {
                bool mine = n.victim == pawn;
                bool friend = !mine && n.victim != null && pawn.relations != null && pawn.relations.OpinionOf(n.victim) >= 30;
                bool hunter = People.Passionate(pawn.skills?.GetSkill(SkillDefOf.Shooting)) || People.Passionate(pawn.skills?.GetSkill(SkillDefOf.Melee));
                if (!mine && !friend && !hunter) continue;
                var c = Make(pawn, mine ? 2.5f : friend ? 1.5f : 0.8f);
                c.other = n.pawn;
                c.subjectKey = n.pawn.ThingID;
                yield return c;
            }
        }

        public override bool IsFulfilled(Wish wish) => wish.other != null && wish.other.Dead;

        public override bool StillValid(Wish wish) => wish.other != null && (wish.other.Dead || (wish.other.Faction == null && wish.other.Spawned && wish.other.Map == wish.Beneficiary.MapHeld));

        public override LookTargets LookAt(Wish wish) => wish.other != null && wish.other.Spawned ? new LookTargets(wish.other) : base.LookAt(wish);

        private static bool Mark(Wish wish)
        {
            var x = wish.other;
            if (x == null || x.Dead || !x.Spawned || x.Faction != null) return false;
            var dm = x.Map.designationManager;
            if (dm.DesignationOn(x, DesignationDefOf.Hunt) != null) return false;
            dm.RemoveAllDesignationsOn(x);
            dm.AddDesignation(new Designation(x, DesignationDefOf.Hunt));
            return true;
        }

        public override string OnPromised(Wish wish) => Mark(wish) ? "CC_Promise_Beast" : null;

        public override string TrySelfHelp(Wish wish) => Mark(wish) ? "CC_SelfHelp_Beast" : null;
    }

    /// <summary>Lessons from the colony's best in a skill they love: the council pairs them up when promised.</summary>
    public class WishWorker_Lessons : WishWorker_Params
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            var social = CouncilGameComponent.Instance?.social;
            if (map == null || social == null || !ColonyCouncilMod.Settings.socialWeb || pawn.skills == null || !P.Who(pawn)) yield break;
            foreach (var s in pawn.skills.skills.Where(s => People.Passionate(s) && s.Level <= 8))
            {
                if (social.ties.Any(t => t.kind == TieKind.Mentor && t.b == pawn && t.skill == s.def)) continue;
                var teacher = map.mapPawns.FreeColonistsSpawned.Where(t => t != pawn && !t.IsQuestLodger() && People.HasInnerLife(t) && !t.DevelopmentalStage.Juvenile()
                                                                           && (t.skills?.GetSkill(s.def)?.Level ?? 0) >= Mathf.Max(10, s.Level + 5))
                    .OrderByDescending(t => t.skills.GetSkill(s.def).Level).FirstOrDefault();
                if (teacher == null) continue;
                var c = Make(pawn, People.Burning(s) ? 1.3f : 0.7f);
                c.other = teacher;
                c.subjectLabel = s.def.label;
                c.subjectKey = s.def.defName;
                c.baseline = s.Level;
                c.targetValue = s.Level + 2;
                yield return c;
            }
        }

        private static SkillDef Skill(Wish wish) => DefDatabase<SkillDef>.GetNamedSilentFail(wish.subjectKey);

        public override bool IsFulfilled(Wish wish) => (wish.Beneficiary.skills?.GetSkill(Skill(wish))?.Level ?? 0) >= wish.targetValue;

        public override bool StillValid(Wish wish) => Skill(wish) != null;

        public override string ProgressText(Wish wish) =>
            "CC_Progress_Lessons".Translate(wish.Beneficiary.skills?.GetSkill(Skill(wish))?.Level ?? 0, wish.targetValue.ToString("0")).ToString();

        public override string OnPromised(Wish wish)
        {
            var social = CouncilGameComponent.Instance?.social;
            var skill = Skill(wish);
            if (social == null || skill == null || wish.other == null || wish.other.Dead || !wish.other.IsColonist) return null;
            social.StartMentorship(wish.other, wish.Beneficiary, skill);
            return "CC_Promise_Lessons";
        }
    }

    /// <summary>Flowers around the colony, spring and summer: decorative plants growing in the home area.</summary>
    public class WishWorker_Flowers : WishWorker_Params
    {
        private static int Count(Map map)
        {
            if (map == null) return 0;
            int n = 0;
            var home = map.areaManager.Home;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant))
                if (t is Plant p && p.def.plant != null && p.def.plant.purpose == PlantPurpose.Beauty && home[p.Position] && p.Growth > 0.3f) n++;
            return n;
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !P.Who(pawn) || Count(map) >= P.count) yield break;
            if (!DefDatabase<ThingDef>.AllDefsListForReading.Any(d => d.plant != null && d.plant.purpose == PlantPurpose.Beauty && d.plant.Sowable
                                                                      && (d.plant.sowResearchPrerequisites == null || d.plant.sowResearchPrerequisites.All(r => r.IsFinished)))) yield break;
            var c = Make(pawn, 1f);
            c.targetValue = P.count;
            c.subjectKey = "colony";
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => Count(wish.Beneficiary.MapHeld) >= wish.targetValue;

        public override string ProgressText(Wish wish) => "CC_Progress_Flowers".Translate(Count(wish.Beneficiary.MapHeld), wish.targetValue.ToString("0")).ToString();
    }
}
