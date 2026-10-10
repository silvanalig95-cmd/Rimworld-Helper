using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Tattered clothes, a dead man's shirt, or nothing at all.</summary>
    public class WishWorker_NewClothes : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || HasTrait(pawn, TraitDefOf.Nudist)) yield break;
            string key = null;
            if (RoomUtil.HasActiveThought(pawn, ThoughtDefOf.DeadMansApparel)) key = "dead";
            else if (RoomUtil.HasActiveThought(pawn, ThoughtDefOf.ApparelDamaged)) key = "tattered";
            else if (RoomUtil.HasActiveThought(pawn, ThoughtDefOf.Naked)) key = "naked";
            if (key == null) yield break;
            bool canTailor = Avail.ColonyHas(map, d => Avail.IsWorkstationFor(d, SkillDefOf.Crafting))
                             || Avail.AnyBuildable(map, d => Avail.IsWorkstationFor(d, SkillDefOf.Crafting));
            if (!canTailor) yield break;
            var c = Candidate(key == "naked" ? 2f : 1.2f, ("CC_Reason_Clothes_" + key).Translate(pawn.Named("PAWN")));
            c.subjectKey = key;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish)
        {
            var p = wish.Beneficiary;
            switch (wish.subjectKey)
            {
                case "dead": return !RoomUtil.HasActiveThought(p, ThoughtDefOf.DeadMansApparel);
                case "naked": return !RoomUtil.HasActiveThought(p, ThoughtDefOf.Naked);
                default: return !RoomUtil.HasActiveThought(p, ThoughtDefOf.ApparelDamaged);
            }
        }
    }

    /// <summary>A friend or relative lies unburied. Bury them (or cremate them) properly.</summary>
    public class WishWorker_Memorial : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            var memories = pawn.needs?.mood?.thoughts?.memories?.Memories;
            if (map == null || memories == null || pawn.relations == null) yield break;
            if (!Avail.AnyBuildable(map, Avail.IsGrave) && !Avail.ColonyHas(map, Avail.IsGrave)) yield break;
            var seen = new HashSet<Pawn>();
            foreach (var m in memories)
            {
                var dead = m.otherPawn;
                if (dead == null || !dead.Dead || !seen.Add(dead)) continue;
                if (!dead.RaceProps.Humanlike) continue;
                var corpse = dead.Corpse;
                if (corpse == null || !corpse.Spawned || corpse.Map != map) continue;
                bool family = pawn.relations.FamilyByBlood.Contains(dead) || LovePartnerRelationUtility.LovePartnerRelationExists(pawn, dead)
                              || pawn.GetRelations(dead).Any(r => r == PawnRelationDefOf.ExLover || r == PawnRelationDefOf.ExSpouse);
                int opinion = pawn.relations.OpinionOf(dead);
                if (!family && opinion < 15) continue;
                var c = Candidate(family ? 4f : 2.5f, family
                    ? "CC_Reason_DeadFamily".Translate(pawn.Named("PAWN"), dead.Named("OTHER"), PawnRelationUtility.GetMostImportantRelation(pawn, dead)?.GetGenderSpecificLabel(dead) ?? "")
                    : "CC_Reason_DeadFriend".Translate(pawn.Named("PAWN"), dead.Named("OTHER")));
                c.other = dead;
                c.subjectKey = dead.ThingID;
                yield return c;
            }
        }

        public override bool StillValid(Wish wish) => wish.other != null;

        public override bool IsFulfilled(Wish wish)
        {
            var dead = wish.other;
            if (dead == null) return false;
            var corpse = dead.Corpse;
            if (corpse == null || corpse.Destroyed) return true;
            return corpse.ParentHolder is Building_Grave;
        }

        public override LookTargets LookAt(Wish wish)
        {
            var corpse = wish.other?.Corpse;
            if (corpse != null && corpse.Spawned) return new LookTargets(corpse);
            return base.LookAt(wish);
        }

        /// <summary>They mark out a grave themselves: next to the others if there's a graveyard, else outside the colony.</summary>
        public override string TrySelfHelp(Wish wish)
        {
            var map = wish.Beneficiary.MapHeld;
            if (map == null) return null;
            var grave = Avail.BestBuildable(map, Avail.IsGrave, "Grave");
            if (grave == null) return null;
            var existing = Avail.ColonyBuildings(map, Avail.IsGrave).FirstOrDefault();
            IntVec3 center = existing?.Position ?? TemplatePlanner.ColonyCenter(map);
            float minDist = existing != null ? 0f : 12f;
            var stuff = grave.MadeFromStuff ? Avail.BestStuff(grave, map) : null;
            foreach (var c in GenRadial.RadialCellsAround(center, 30f, true))
            {
                if (!c.InBounds(map) || c.Fogged(map) || c.DistanceTo(center) < minDist) continue;
                if (c.Roofed(map) || c.GetZone(map) != null || !c.Standable(map)) continue;
                if (!GenConstruct.CanPlaceBlueprintAt(grave, c, Rot4.North, map, false, null, null, stuff).Accepted) continue;
                bool clear = GenAdj.OccupiedRect(c, Rot4.North, grave.size).ExpandedBy(1).Cells.All(x => x.InBounds(map) && x.GetEdifice(map) == null);
                if (!clear) continue;
                GenConstruct.PlaceBlueprintForBuild(grave, c, map, Rot4.North, Faction.OfPlayer, stuff);
                return "CC_SelfHelp_Grave";
            }
            return null;
        }
    }

    /// <summary>A missing limb or eye, and a prosthetic within reach.</summary>
    public class WishWorker_Prosthetic : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || pawn.health?.hediffSet == null) yield break;
            if (HasTrait(pawn, TraitDefOf.BodyPurist)) yield break;
            foreach (var missing in pawn.health.hediffSet.GetMissingPartsCommonAncestors())
            {
                var part = missing.Part;
                if (part == null || part.def == null) continue;
                var install = ObtainableInstall(map, part.def);
                if (install == null) continue;
                var c = Candidate(HasTrait(pawn, TraitDefOf.Transhumanist) ? 2.5f : 1.5f,
                    "CC_Reason_MissingPart".Translate(pawn.Named("PAWN"), part.Label.Named("PART")));
                c.subjectKey = part.Label;
                c.subjectLabel = part.Label;
                c.thingDef = install;
                yield return c;
            }
        }

        /// <summary>A body part item that can be installed on this part and is in stock or craftable.</summary>
        private static ThingDef ObtainableInstall(Map map, BodyPartDef partDef)
        {
            foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (r.addsHediff == null || r.appliedOnFixedBodyParts == null || !r.appliedOnFixedBodyParts.Contains(partDef)) continue;
                if (!r.AvailableNow || r.ingredients == null) continue;
                foreach (var ing in r.ingredients)
                {
                    if (ing.filter == null) continue;
                    foreach (var item in ing.filter.AllowedThingDefs)
                    {
                        if (item.IsMedicine || !item.isTechHediff) continue;
                        if (map.listerThings.ThingsOfDef(item).Any()) return item;
                        if (DefDatabase<RecipeDef>.AllDefsListForReading.Any(cr => cr.ProducedThingDef == item && cr.AvailableNow
                                && cr.AllRecipeUsers.Any(u => map.listerBuildings.ColonistsHaveBuilding(u) || Avail.CanBuild(u, map))))
                            return item;
                    }
                }
            }
            return null;
        }

        public override bool IsFulfilled(Wish wish)
        {
            var set = wish.Beneficiary.health?.hediffSet;
            if (set == null) return false;
            foreach (var missing in set.GetMissingPartsCommonAncestors())
                if (missing.Part != null && missing.Part.Label == wish.subjectKey) return false;
            return true;
        }
    }

    /// <summary>An animal lover wants to tame something that's roaming the map.</summary>
    public class WishWorker_Companion : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            var animals = pawn.skills?.GetSkill(SkillDefOf.Animals);
            if (map == null || animals == null || animals.TotallyDisabled || animals.passion == Passion.None) yield break;
            if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Handling)) yield break;
            var wild = map.mapPawns.AllPawnsSpawned
                .Where(a => a.RaceProps.Animal && a.Faction == null && !a.Downed && a.GetStatValue(StatDefOf.Wildness) < 0.85f
                            && a.def.race.trainability != null && !map.fogGrid.IsFogged(a.Position))
                .ToList();
            if (wild.Count == 0) yield break;
            var example = wild.RandomElement();
            var c = Candidate(animals.passion == Passion.Major ? 1.2f : 0.5f, "CC_Reason_AnimalLover".Translate(pawn.Named("PAWN"), example.kindDef.GetLabelPlural().Named("ANIMALS")));
            c.baseline = RoomUtil.ColonyAnimalCount(map);
            c.subjectLabel = example.kindDef.label;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => RoomUtil.ColonyAnimalCount(wish.Beneficiary.MapHeld) > wish.baseline;

        public override InspirationDef InspirationFor(Wish wish) => InspirationDefOf.Inspired_Taming;

        /// <summary>They pick an animal and mark it for taming.</summary>
        public override string TrySelfHelp(Wish wish)
        {
            var pawn = wish.Beneficiary;
            var map = pawn.MapHeld;
            if (map == null) return null;
            var animal = map.mapPawns.AllPawnsSpawned
                .Where(a => a.RaceProps.Animal && a.Faction == null && !a.Downed && a.GetStatValue(StatDefOf.Wildness) < 0.85f
                            && a.def.race.trainability != null && !map.fogGrid.IsFogged(a.Position)
                            && map.designationManager.DesignationOn(a) == null)
                .OrderBy(a => a.Position.DistanceTo(pawn.Position))
                .FirstOrDefault();
            if (animal == null) return null;
            map.designationManager.AddDesignation(new Designation(animal, DesignationDefOf.Tame));
            wish.subjectLabel = animal.LabelShort;
            return "CC_SelfHelp_Tame";
        }
    }

    /// <summary>Feeling unsafe: more defences, in whatever form the colony can build.</summary>
    public class WishWorker_Defenses : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null) yield break;
            var assessment = DefenseUtil.Assess(map);
            if (assessment.ratio >= 0.8f) yield break;
            var shoot = pawn.skills?.GetSkill(SkillDefOf.Shooting);
            var melee = pawn.skills?.GetSkill(SkillDefOf.Melee);
            bool fighter = (shoot != null && shoot.passion == Passion.Major) || (melee != null && melee.passion == Passion.Major);
            bool shaken = HasMemory(pawn, ThoughtDefOf.WitnessedDeathAlly) || HasMemory(pawn, ThoughtDefOf.ColonistLost)
                          || HasTrait(pawn, TraitDefOf.Wimp);
            if (!fighter && !shaken) yield break;

            var (def, key, extra) = DefenseUtil.BestDefenseToAdd(map);
            if (def == null) yield break;
            var c = Candidate(shaken ? 1.6f : 1f, shaken ? "CC_Reason_Unsafe".Translate(pawn.Named("PAWN")) : "CC_Reason_Tactician".Translate(pawn.Named("PAWN")));
            c.thingDef = def;
            c.subjectKey = key;
            c.subjectLabel = ("CC_DefKind_" + key).Translate();
            c.baseline = DefenseUtil.CountOfKind(map, key);
            c.targetValue = c.baseline + extra;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => DefenseUtil.CountOfKind(wish.Beneficiary.MapHeld, wish.subjectKey) >= wish.targetValue;

        public override string ProgressText(Wish wish) =>
            "CC_Progress_Count".Translate(DefenseUtil.CountOfKind(wish.Beneficiary.MapHeld, wish.subjectKey), wish.targetValue.ToString("0"));

        public override string FulfilledWhen(Wish wish) =>
            "CC_When_Defenses".Translate((wish.targetValue - wish.baseline).ToString("0"), wish.thingDef?.label ?? "");
    }

    /// <summary>Nobles want their title respected: vanilla's bedroom and throne room requirements.</summary>
    public class WishWorker_RoyalStandards : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            if (!ModsConfig.RoyaltyActive || pawn.royalty == null) yield break;
            List<string> bedroom = null, throne = null;
            try
            {
                if (pawn.royalty.CanRequireBedroom()) bedroom = pawn.royalty.GetUnmetBedroomRequirements(false).ToList();
                if (pawn.royalty.CanRequireThroneroom()) throne = pawn.royalty.GetUnmetThroneroomRequirements(false).ToList();
            }
            catch
            {
                yield break;
            }
            var title = pawn.royalty.MostSeniorTitle?.Label ?? "";
            if (bedroom != null && bedroom.Count > 0)
            {
                var c = Candidate(2f, "CC_Reason_RoyalBedroom".Translate(pawn.Named("PAWN"), title.Named("TITLE"), string.Join("; ", bedroom).Named("LIST")));
                c.subjectKey = "bedroom";
                c.subjectLabel = "CC_RoyalBedroom".Translate();
                yield return c;
            }
            if (throne != null && throne.Count > 0)
            {
                var c = Candidate(2f, "CC_Reason_RoyalThrone".Translate(pawn.Named("PAWN"), title.Named("TITLE"), string.Join("; ", throne).Named("LIST")));
                c.subjectKey = "throne";
                c.subjectLabel = "CC_RoyalThroneRoom".Translate();
                yield return c;
            }
        }

        public override bool IsFulfilled(Wish wish)
        {
            var r = wish.Beneficiary.royalty;
            if (r == null) return true;
            return wish.subjectKey == "throne"
                ? !r.GetUnmetThroneroomRequirements(false).Any()
                : !r.GetUnmetBedroomRequirements(false).Any();
        }

        public override bool StillValid(Wish wish) => wish.Beneficiary.royalty?.MostSeniorTitle != null;

        public override string ProgressText(Wish wish)
        {
            var r = wish.Beneficiary.royalty;
            if (r == null) return null;
            var list = wish.subjectKey == "throne" ? r.GetUnmetThroneroomRequirements(false) : r.GetUnmetBedroomRequirements(false);
            return string.Join("; ", list);
        }
    }

    /// <summary>Children want somewhere to learn and play (Biotech).</summary>
    public class WishWorker_ChildPlay : WishWorker
    {
        private static readonly string[] Candidates = { "SchoolDesk", "Blackboard", "BabyToyBox", "ToyBox" };

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !ModsConfig.BiotechActive || !pawn.DevelopmentalStage.Child()) yield break;
            foreach (var name in Candidates)
            {
                var d = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (d == null || map.listerBuildings.ColonistsHaveBuilding(d) || !Avail.CanBuild(d, map)) continue;
                var c = Candidate(1.5f, "CC_Reason_Child".Translate(pawn.Named("PAWN")));
                c.thingDef = d;
                c.subjectKey = d.defName;
                yield return c;
                yield break;
            }
        }

        public override bool IsFulfilled(Wish wish) => wish.thingDef != null && wish.Beneficiary.MapHeld.listerBuildings.ColonistsHaveBuilding(wish.thingDef);
    }
}
