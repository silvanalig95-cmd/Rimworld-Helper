using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The gifts of Rimfolk's peoples' people (docs/design/peoples.md "Their gear, their kinds of people and their gifts"):
    /// what their pawn kinds can't give softly in their defs, given once as a new pawn of one of these kinds is made
    /// (<see cref="PeopleKindExtension"/>; nobody else's pawns, and no pawn already in a game). A share of the kind, never
    /// all, has at most one: a class of A RimWorld of Magic, by its trait's name (the hospitallers' priests, the wardens'
    /// paladins, the river folk's druids and rangers), only when the mod has that class switched on and doesn't keep
    /// the people from it in its own settings; a psylink of a few levels (Royalty: the game's own random psycasts of
    /// each level; with Vanilla Psycasts Expanded, one of the kind's paths and a few of its powers, read by name); or a
    /// gene or two put in (Biotech: a baseliner of the scholars or the enclave given them as xenogenes, by name, the game's
    /// or a mod's). No letter, nothing saved, nothing on a clock. Without a mod, its gift is simply never given.
    /// </summary>
    public static class PeopleGifts
    {
        public static void Give(Pawn p)
        {
            var ext = p?.kindDef?.GetModExtension<PeopleKindExtension>();
            if (ext == null || ext.gifts == null && ext.ageArms == null || p.story?.traits == null || p.health?.hediffSet == null || p.Dead || p.RaceProps?.Humanlike != true) return;
            if (!p.DevelopmentalStage.Adult() || p.IsMutant) return;
            int age = Peoples.AgeOf(p.Faction);
            if (!ext.ageArms.NullOrEmpty()) AgeArms.Arm(p, ext.ageArms, age);
            if (ext.gifts.NullOrEmpty()) return;
            // A gift of another age (the tamers' Wildspeakers in the spacer age) is no share of this one.
            int i = PeoplesGearMath.Pick(ext.gifts.Select(g => g != null && PeoplesDMath.AgeFits(age, g.ages.min, g.ages.max) ? g.chance : 0f).ToList(), Rand.Value);
            if (i < 0 || ext.gifts[i] == null) return;
            var gift = ext.gifts[i];
            if (!gift.trait.NullOrEmpty()) GiveClass(p, gift.trait);
            else if (gift.psylink.max > 0) GivePsylink(p, gift);
            else if (!gift.genes.NullOrEmpty()) GiveGenes(p, gift.genes);
        }

        // ================= Genes put in =================

        /// <summary>
        /// A baseliner's xenogenes, as a gene clinic of their people would put them in: only a baseliner of no custom
        /// xenotype (the other kinds of people keep their own genome whole), each gene only when it is in the game, the
        /// pawn hasn't it and nothing it has conflicts with it.
        /// </summary>
        private static void GiveGenes(Pawn p, List<string> names)
        {
            if (!ModsConfig.BiotechActive || p.genes == null || p.genes.Xenotype != XenotypeDefOf.Baseliner || p.genes.UniqueXenotype) return;
            foreach (string name in names)
            {
                var def = DefDatabase<GeneDef>.GetNamedSilentFail(name);
                if (def == null || p.genes.GenesListForReading.Any(g => g?.def != null && g.def.ConflictsWith(def))) continue;
                try
                {
                    p.genes.AddGene(def, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' gifts: a gene", e);
                }
            }
        }

        // ================= A RimWorld of Magic =================

        private static void GiveClass(Pawn p, string name)
        {
            var def = DefDatabase<TraitDef>.GetNamedSilentFail(name);
            if (def == null || !ClassOn(name, p.Faction)) return;
            var traits = p.story.traits;
            // One class a pawn, as the mod itself keeps it (its classes conflict with each other): none if it rolled its own.
            if (traits.HasTrait(def) || traits.allTraits.Any(t => t?.def != null && (t.def.ConflictsWith(def) || def.ConflictsWith(t.def)))) return;
            int degree = def.degreeDatas.NullOrEmpty() ? 0 : def.degreeDatas[0].degree;
            if (p.story.AllBackstories.Any(b => b != null && b.DisallowsTrait(def, degree))) return;
            traits.GainTrait(new Trait(def, degree, false));
            Renew(p);
        }

        private static Type settingsType;
        private static bool settingsLooked;

        /// <summary>The class is switched on in the mod's settings, and the mod's own share for this people isn't none.</summary>
        private static bool ClassOn(string trait, Faction f)
        {
            string setting = PeoplesGearMath.MagicSetting(trait);
            if (setting == null) return false;
            try
            {
                if (!settingsLooked)
                {
                    settingsLooked = true;
                    settingsType = AccessTools.TypeByName("TorannMagic.ModOptions.Settings");
                }
                var settings = settingsType == null ? null : AccessTools.Field(settingsType, "Instance")?.GetValue(null);
                // Settings not read yet: the class as the mod ships it, on.
                if (settings == null) return true;
                if (AccessTools.Field(settingsType, setting)?.GetValue(settings) is bool on && !on) return false;
                var table = AccessTools.Field(settingsType, PeoplesGearMath.Might(trait) ? "FactionFighterSettings" : "FactionMageSettings")?.GetValue(settings) as IDictionary<string, float>;
                float? factor = f?.def != null && table != null && table.TryGetValue(f.def.defName, out float v) ? v : (float?)null;
                return PeoplesGearMath.FactionAllows(factor);
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// The mod's magic and might components keep whether a pawn is a caster once they first look (their
        /// initializedIsMagicUser and initializedIsMightUser): looked before the class was given, they would never wake. Set
        /// back so they look again; nothing happens when the fields aren't there.
        /// </summary>
        private static void Renew(Pawn p)
        {
            if (p.AllComps == null) return;
            foreach (var c in p.AllComps)
            {
                if (c == null) continue;
                var type = c.GetType();
                string flag = type.Name == "CompAbilityUserMagic" ? "initializedIsMagicUser" : type.Name == "CompAbilityUserMight" ? "initializedIsMightUser" : null;
                if (flag == null) continue;
                try
                {
                    AccessTools.Field(type, flag)?.SetValue(c, false);
                    var index = AccessTools.Field(type, "customIndex");
                    if (index != null && index.FieldType == typeof(int)) index.SetValue(c, -2);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' gifts: waking a class of A RimWorld of Magic", e);
                }
            }
        }

        // ================= A psylink =================

        private static void GivePsylink(Pawn p, PeopleGift gift)
        {
            if (!ModsConfig.RoyaltyActive || p.abilities == null || p.GetMainPsylinkSource() != null || p.health.hediffSet.GetBrain() == null) return;
            int levels = PeoplesGearMath.Levels(gift.psylink.min, gift.psylink.max, Rand.Value);
            try
            {
                // The game's own way, with no letter: the psylink, then its levels, each with a psycast of its level.
                p.ChangePsylinkLevel(1, false);
                if (levels > 1) p.ChangePsylinkLevel(levels - 1, false);
                if (!gift.paths.NullOrEmpty()) PsycastPaths.Follow(p, gift.paths, PeoplesGearMath.Powers(levels));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' gifts: a psylink", e);
            }
        }
    }

    /// <summary>
    /// Vanilla Psycasts Expanded, read by name (its 1.6 and 1.7 code): with it, a psylink gives no psycasts of its own (its
    /// implant, VPE_PsycastAbilityImplant, holds points instead), so a psycaster of Rimfolk's peoples takes one of its
    /// kind's paths it can follow (PsycasterPathDef.CanPawnUnlock) and a few of its powers in order, as the mod's own pawn
    /// generation does for a pawn kind's paths (Hediff_PsycastAbilities.UnlockPath, CompAbilities.GiveAbility, a point a
    /// power). Without the mod, or with anything not where it is expected, nothing happens: the psylink stays as given.
    /// </summary>
    internal static class PsycastPaths
    {
        public static void Follow(Pawn p, List<string> paths, int powers)
        {
            var implantDef = DefDatabase<HediffDef>.GetNamedSilentFail("VPE_PsycastAbilityImplant");
            if (implantDef == null) return;
            var implant = p.health.hediffSet.GetFirstHediffOfDef(implantDef);
            var pathType = AccessTools.TypeByName("VanillaPsycastsExpanded.PsycasterPathDef");
            var abilityDefType = AccessTools.TypeByName("VEF.Abilities.AbilityDef");
            var comp = p.AllComps?.FirstOrDefault(c => c?.GetType().FullName == "VEF.Abilities.CompAbilities");
            if (implant == null || pathType == null || abilityDefType == null || comp == null) return;
            var it = implant.GetType();
            var unlock = AccessTools.Method(it, "UnlockPath", new[] { pathType });
            var levelUp = AccessTools.Method(it, "ChangeLevel", new[] { typeof(int), typeof(bool) });
            var points = AccessTools.Field(it, "points");
            var canUnlock = AccessTools.Method(pathType, "CanPawnUnlock", new[] { typeof(Pawn) });
            var abilitiesField = AccessTools.Field(pathType, "abilities");
            var give = AccessTools.Method(comp.GetType(), "GiveAbility", new[] { abilityDefType });
            var psycastType = AccessTools.TypeByName("VanillaPsycastsExpanded.AbilityExtension_Psycast");
            var prereqs = psycastType == null ? null : AccessTools.Method(psycastType, "PrereqsCompleted", new[] { comp.GetType() });
            var levelField = psycastType == null ? null : AccessTools.Field(psycastType, "level");
            if (unlock == null || levelUp == null || points == null || points.FieldType != typeof(int) || canUnlock == null || abilitiesField == null || give == null || prereqs == null)
                return;
            var open = paths.Select(n => GenDefDatabase.GetDefSilentFail(pathType, n, false))
                .Where(d => d != null && canUnlock.Invoke(d, new object[] { p }) is bool ok && ok).ToList();
            if (open.Count == 0) return;
            var path = open.RandomElement();
            unlock.Invoke(implant, new object[] { path });
            var left = (abilitiesField.GetValue(path) as IEnumerable)?.Cast<Def>().Where(d => d != null).ToList() ?? new List<Def>();
            for (int n = 0; n < powers && left.Count > 0; n++)
            {
                // The next power whose prerequisites are learned, the lowest level first.
                var next = left.Select(a => (a, ext: a.modExtensions?.FirstOrDefault(m => m != null && psycastType.IsInstanceOfType(m))))
                    .Where(x => x.ext != null && prereqs.Invoke(x.ext, new object[] { comp }) is bool ready && ready)
                    .OrderBy(x => levelField?.GetValue(x.ext) is int l ? l : 99).Select(x => x.a).FirstOrDefault();
                if (next == null) break;
                give.Invoke(comp, new object[] { next });
                left.Remove(next);
                if ((int)points.GetValue(implant) <= 0) levelUp.Invoke(implant, new object[] { 1, false });
                points.SetValue(implant, (int)points.GetValue(implant) - 1);
            }
        }
    }

    /// <summary>
    /// The arms of another age (<see cref="PeopleKindExtension.ageArms"/>): a people of the Wardens' ways that arose among
    /// the survivors of an industrial town hunts with rifles, not crossbows; the Quiet Bell of that age carries a flamer
    /// for what will not stay down. The first set whose ages hold the people's own takes the place of the weapon the
    /// game gave, one of its tags' weapons (any mod's under the same tags) within the kind's means, of a quality the
    /// kind's own would be. No set fits: the game's choice stays. Nothing when no weapon of those tags is loaded.
    /// </summary>
    public static class AgeArms
    {
        public static void Arm(Pawn p, List<PeopleAgeArms> sets, int age)
        {
            if (p?.equipment == null || sets.NullOrEmpty() || p.WorkTagIsDisabled(WorkTags.Violent)) return;
            var set = sets.FirstOrDefault(a => a != null && !a.weaponTags.NullOrEmpty() && PeoplesDMath.AgeFits(age, a.ages.min, a.ages.max));
            if (set == null) return;
            if (!set.apparel.NullOrEmpty()) Kit(p, set.apparel);
            float means = set.money > 0f ? set.money : Math.Max(p.kindDef.weaponMoney.max, 150f) * 1.25f;
            var choices = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d != null && d.IsWeapon && d.equipmentType == EquipmentType.Primary && !d.weaponTags.NullOrEmpty()
                && d.weaponTags.Any(set.weaponTags.Contains) && d.BaseMarketValue <= means && d.tradeability != Tradeability.None
                && d.GetCompProperties<CompProperties_Biocodable>() == null).ToList();
            if (choices.Count == 0) return;
            var def = choices.RandomElementByWeight(d => Math.Max(0.1f, d.generateCommonality));
            ThingDef stuff = null;
            if (def.MadeFromStuff && !GenStuff.TryRandomStuffByCommonalityFor(def, out stuff, p.Faction?.def?.techLevel ?? TechLevel.Undefined)) return;
            if (!(ThingMaker.MakeThing(def, stuff) is ThingWithComps weapon)) return;
            weapon.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityGeneratingPawn(p.kindDef, def), ArtGenerationContext.Outsider);
            p.equipment.DestroyAllEquipment();
            p.equipment.AddEquipment(weapon);
        }

        /// <summary>
        /// The issued kit of the age (release E's Ashen Banner): each piece made of its stuff as the game picks it for the
        /// people's age, of the quality the kind's own would be, in the kind's colour for that piece (its
        /// specificApparelRequirements), worn in place of whatever it can't be worn with.
        /// </summary>
        public static void Kit(Pawn p, List<ThingDef> pieces)
        {
            if (p?.apparel == null || pieces.NullOrEmpty()) return;
            foreach (var def in pieces)
            {
                if (def?.apparel == null || !def.apparel.PawnCanWear(p)) continue;
                ThingDef stuff = null;
                if (def.MadeFromStuff && !GenStuff.TryRandomStuffByCommonalityFor(def, out stuff, p.Faction?.def?.techLevel ?? TechLevel.Undefined)) continue;
                if (!(ThingMaker.MakeThing(def, stuff) is Apparel a)) continue;
                a.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityGeneratingPawn(p.kindDef, def), ArtGenerationContext.Outsider);
                var colour = p.kindDef.specificApparelRequirements?.FirstOrDefault(r => r != null && r.ApparelDef == def && r.Color != default);
                if (colour != null) a.SetColor(colour.Color, false);
                foreach (var worn in p.apparel.WornApparel.ToList())
                    if (!ApparelUtility.CanWearTogether(def, worn.def, p.RaceProps.body))
                    {
                        p.apparel.Remove(worn);
                        worn.Destroy();
                    }
                p.apparel.Wear(a, false);
            }
        }
    }

    /// <summary>
    /// The gifts given as a new pawn is made (<see cref="PeopleGifts"/>): a postfix on the game's making of a fresh pawn,
    /// after its traits, gear and hediffs (and A RimWorld of Magic's own roll of a class), for the pawn kinds of Rimfolk's
    /// peoples only; a pawn the game takes from the world and dresses again keeps what it has. Off with the deeper game hooks.
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), "GenerateNewPawnInternal")]
    public static class Patch_PeopleGifts
    {
        public static void Postfix(Pawn __result)
        {
            if (__result == null || !RimfolkPatches.On || __result.kindDef?.GetModExtension<PeopleKindExtension>() == null) return;
            try
            {
                PeopleGifts.Give(__result);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' gifts", e);
            }
        }
    }
}
