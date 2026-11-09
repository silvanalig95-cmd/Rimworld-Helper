using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The item card's reading of body parts and genes (Rimfolk's look, stage 3, point 5, step 3). A body part (a natural
    /// organ, a prosthetic, a bionic, an archotech part, a mod's implant) by the recipes that install it (every recipe looked
    /// through once a kind: the game's install workers and the mods' built on them, or a hediff that gives this part back),
    /// the hediff it adds (its efficiency against a natural part, what it changes, the abilities it gives), the operation's
    /// doctor and research; at home, who could take it (the recipe's own parts for each, missing or worse than it), the best
    /// doctor's chance and what each faith holds on it (the game's thoughts on an installed prosthetic, the blindness
    /// precepts for an eye). A genepack, xenogerm or embryo by its gene set (each gene's effects in words, complexity,
    /// metabolism and archite), the colony's gene assembler, and who a xenogerm would suit (their faith, their metabolism
    /// after it, their own xenogenes). Rules and words in Items/ItemCardUseMath.cs.
    /// </summary>
    public static partial class ItemCardLive
    {
        // ---------------------------------------------------------------- body parts

        /// <summary>The recipes that install it and what they add (once a kind).</summary>
        private static void ReadBodyPart(ThingDef d, UsePart up)
        {
            foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (up.installs.Count >= 4) break;
                try
                {
                    if (r?.ingredients == null || r.ingredients.Count == 0 || r.workerClass == null) continue;
                    bool installs = typeof(Recipe_InstallArtificialBodyPart).IsAssignableFrom(r.workerClass) || typeof(Recipe_InstallImplant).IsAssignableFrom(r.workerClass)
                                    || typeof(Recipe_InstallNaturalBodyPart).IsAssignableFrom(r.workerClass) || r.addsHediff?.spawnThingOnRemoved == d;
                    if (!installs || !r.IsIngredient(d)) continue;
                    up.installs.Add(r);
                }
                catch
                {
                    // A mod's recipe that can't be read: left out.
                }
            }
            var first = up.installs.FirstOrDefault();
            up.hediff = first?.addsHediff;
            up.natural = first != null && typeof(Recipe_InstallNaturalBodyPart).IsAssignableFrom(first.workerClass);
            up.implant = first != null && typeof(Recipe_InstallImplant).IsAssignableFrom(first.workerClass);
            up.part = first?.appliedOnFixedBodyParts?.FirstOrDefault(b => b != null)?.label ?? "";
            var props = up.hediff?.addedPartProps;
            up.efficiency = up.natural ? 1f : props != null ? props.partEfficiency : -1f;
            up.better = props != null && props.betterThanNatural;
            var stage = up.hediff?.stages?.FirstOrDefault(s => s != null);
            if (stage != null)
            {
                foreach (var cm in stage.capMods ?? new List<PawnCapacityModifier>())
                    if (cm?.capacity != null && Math.Abs(cm.offset) > 0.001f) up.adds.Add(cm.capacity.LabelCap + " " + (cm.offset > 0f ? "+" : "") + cm.offset.ToStringPercent());
                foreach (var so in stage.statOffsets ?? new List<StatModifier>())
                    if (so?.stat != null) up.adds.Add(so.stat.LabelCap + " " + so.stat.ValueToString(so.value, ToStringNumberSense.Offset));
                foreach (var sf in stage.statFactors ?? new List<StatModifier>())
                    if (sf?.stat != null) up.adds.Add(sf.stat.LabelCap + " " + sf.stat.ValueToString(sf.value, ToStringNumberSense.Factor));
            }
            foreach (var ab in up.hediff?.abilities ?? new List<AbilityDef>())
                if (ab != null) up.abilities.Add(ab.label);
            up.doctorLevel = first?.skillRequirements?.FirstOrDefault(sr => sr?.skill == SkillDefOf.Medicine)?.minLevel ?? 0;
        }

        private static void LiveBodyPart(ItemCardModel m, UsePart up, UseFacts u)
        {
            u.Part = up.part;
            u.Natural = up.natural;
            u.Implant = up.implant;
            u.Efficiency = up.efficiency;
            u.BetterThanNatural = up.better;
            u.Adds = up.adds.ToList();
            u.Abilities = up.abilities.ToList();
            u.Installable = up.installs.Count > 0;
            u.DoctorLevel = up.doctorLevel;
        }

        /// <summary>
        /// At home: the operation's research, who could take it (the recipe's own parts for each, the part's efficiency now
        /// against this one's; missing first), the best doctor's chance, and what each faith holds on it.
        /// </summary>
        private static void ColonyBodyPart(ItemCardModel m, UsePart up, UseFacts u, List<Pawn> people)
        {
            u.SurgeryResearch = u.BestDoctor = u.MissingWho = u.FaithAgainst = u.FaithFor = "";
            u.BestChance = -1f;
            u.Missing = 0;
            u.Held = HeldOf(m.map, m.def);
            var recipe = up.installs.FirstOrDefault();
            if (recipe == null) return;
            var waits = recipe.researchPrerequisite != null && !recipe.researchPrerequisite.IsFinished ? recipe.researchPrerequisite
                : recipe.researchPrerequisites?.FirstOrDefault(r => r != null && !r.IsFinished);
            u.SurgeryResearch = waits?.label ?? "";

            // What each faith at home holds on an installed prosthetic (the game's own thoughts on that event), and on blindness for an eye.
            var faithMood = new Dictionary<Ideo, (float mood, bool unwilling, string precept)>();
            bool eye = (recipe.appliedOnFixedBodyParts ?? new List<BodyPartDef>()).Any(b => b != null && b.defName.IndexOf("Eye", StringComparison.OrdinalIgnoreCase) >= 0);
            var against = new List<string>();
            var approve = new List<string>();
            if (ModsConfig.IdeologyActive)
            {
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
                    if (ideo == null || faithMood.ContainsKey(ideo)) continue;
                    var view = FaithOnPart(ideo, up.natural, eye);
                    faithMood[ideo] = view;
                    if (view.precept.Length == 0) continue;
                    string line = W(view.unwilling ? "CC_ItemCard_RulePartUnwilling" : view.mood < 0f ? "CC_ItemCard_RulePartFrowns" : view.mood > 0f ? "CC_ItemCard_RulePartGlad" : "CC_ItemCard_RulePartView",
                        ideo.name.Named("FAITH"), view.precept.Named("PRECEPT"), ItemCardMath.Mood(view.mood).Named("MOOD"));
                    m.useRules.Add(line);
                    if (view.unwilling || view.mood < 0f) against.Add(ideo.name + " (" + view.precept + ")");
                    else if (view.mood > 0f) approve.Add(ideo.name + " (" + view.precept + ")");
                }
            }
            if (against.Count > 0) u.FaithAgainst = W("CC_ItemCard_FaithsFrown", string.Join(", ", against).Named("FAITHS"));
            if (approve.Count > 0) u.FaithFor = W("CC_ItemCard_FaithsGlad", string.Join(", ", approve).Named("FAITHS"));

            // Who could take it.
            var list = new List<(Pawn, UseCandidate)>();
            var missingNames = new List<string>();
            foreach (var x in people)
            {
                var c = new UseCandidate { Name = x.LabelShort };
                List<BodyPartRecord> parts;
                try
                {
                    parts = recipe.Worker.GetPartsToApplyOn(x, recipe)?.ToList() ?? new List<BodyPartRecord>();
                }
                catch
                {
                    parts = new List<BodyPartRecord>();
                }
                if (parts.Count == 0)
                {
                    c.Why = W(u.Implant ? "CC_ItemCard_WhoHasImplant" : "CC_ItemCard_WhoNoPart", (u.Part.Length > 0 ? u.Part : "?").Named("PART"));
                    list.Add((x, c));
                    continue;
                }
                var hs = x.health.hediffSet;
                var missingAll = hs.GetMissingPartsCommonAncestors();
                BodyPartRecord bestPart = null;
                string goneLabel = null;
                float bestGain = 0f, nowAt = 1f;
                bool missing = false;
                foreach (var part in parts)
                {
                    // A part gone (or one under it, a hand under an arm) comes first; then the most gained over what they have there now.
                    var lost = missingAll.FirstOrDefault(h => h?.Part != null && Under(h.Part, part));
                    bool gone = lost != null;
                    float now = gone ? 0f : PawnCapacityUtility.CalculatePartEfficiency(hs, part);
                    float gain = u.Implant ? 0.5f : Mathf.Max(0f, (u.Efficiency >= 0f ? u.Efficiency : 1f) - now);
                    if (gone) gain = Mathf.Max(gain, 1f);
                    bool better = bestPart == null || gone && !missing || gone == missing && gain > bestGain + 0.001f;
                    if (!better) continue;
                    bestPart = part;
                    bestGain = gain;
                    nowAt = now;
                    missing = gone;
                    goneLabel = lost?.Part?.Label;
                }
                if (missing) missingNames.Add(x.LabelShort);
                string mood = "";
                try
                {
                    if (x.Ideo != null && faithMood.TryGetValue(x.Ideo, out var view) && view.precept.Length > 0)
                        mood = " · " + W(view.unwilling ? "CC_ItemCard_WhoFaithWont" : "CC_ItemCard_WhoFaithMood", ItemCardMath.Mood(view.mood).Named("MOOD"));
                    if (x.Ideo != null && faithMood.TryGetValue(x.Ideo, out var v2) && v2.unwilling) bestGain = 0f;
                }
                catch
                {
                    // No faith to read.
                }
                c.Gain = bestGain;
                c.Can = bestGain > 0.001f;
                string where = bestPart?.Label ?? u.Part;
                c.Why = (missing ? W("CC_ItemCard_WhoMissing", (goneLabel ?? where).Named("PART"))
                        : u.Implant ? W("CC_ItemCard_WhoCanImplant", where.Named("PART"))
                        : c.Can ? W("CC_ItemCard_WhoPartGain", where.Named("PART"), nowAt.ToStringPercent().Named("NOW"), (u.Efficiency >= 0f ? u.Efficiency : 1f).ToStringPercent().Named("THEN"))
                        : W("CC_ItemCard_WhoPartNoBetter", where.Named("PART"), nowAt.ToStringPercent().Named("NOW"))) + mood;
                list.Add((x, c));
            }
            u.Missing = missingNames.Count;
            if (missingNames.Count > 0)
                u.MissingWho = W(missingNames.Count == 1 ? "CC_ItemCard_MissingOne" : "CC_ItemCard_MissingMany", ItemCardMath.AndList(missingNames.Take(3).ToList()).Named("WHO"), (u.Part.Length > 0 ? u.Part : "?").Named("PART"));
            SetWho(m, list, null);

            // The best doctor for it: their surgery success by the operation's own factor, among those with doctoring on who meet its skill.
            foreach (var x in people)
            {
                try
                {
                    if (x.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) || x.workSettings == null || !x.workSettings.WorkIsActive(WorkTypeDefOf.Doctor)) continue;
                    if (!recipe.PawnSatisfiesSkillRequirements(x)) continue;
                    float chance = ItemCardMath.SurgeryChance(x.GetStatValue(StatDefOf.MedicalSurgerySuccessChance), recipe.surgerySuccessChanceFactor);
                    if (chance <= u.BestChance) continue;
                    u.BestChance = chance;
                    u.BestDoctor = x.LabelShort;
                }
                catch
                {
                    // A doctor that can't be read: left out.
                }
            }
        }

        /// <summary>A part, or one under it (a hand under an arm under a shoulder).</summary>
        private static bool Under(BodyPartRecord part, BodyPartRecord top)
        {
            for (var p = part; p != null; p = p.parent)
                if (p == top) return true;
            return false;
        }

        /// <summary>What a faith holds on installing it: the mood of the game's thought for its believer, whether they won't, and the precept's name (empty: nothing said).</summary>
        private static (float mood, bool unwilling, string precept) FaithOnPart(Ideo ideo, bool natural, bool eye)
        {
            float mood = 0f;
            bool unwilling = false;
            string precept = "";
            try
            {
                foreach (var pr in ideo.PreceptsListForReading)
                {
                    if (pr?.def == null) continue;
                    string issue = pr.def.issue?.defName ?? "";
                    bool said = false;
                    if (!natural && pr.def.comps != null)
                        foreach (var c in pr.def.comps)
                        {
                            if (c is PreceptComp_SelfTookMemoryThought self && self.eventDef == HistoryEventDefOf.InstalledProsthetic && self.thought?.stages != null && self.thought.stages.Count > 0)
                            {
                                mood += self.thought.stages[0].baseMoodEffect;
                                said = true;
                            }
                            else if (c is PreceptComp_UnwillingToDo no && no.eventDef == HistoryEventDefOf.InstalledProsthetic)
                            {
                                unwilling = true;
                                said = true;
                            }
                        }
                    // A faith that reveres blindness: a new eye ends it.
                    if (eye && issue.IndexOf("Blind", StringComparison.OrdinalIgnoreCase) >= 0) said = true;
                    if (said && precept.Length == 0) precept = pr.LabelCap;
                }
            }
            catch
            {
                // A faith that can't be read: nothing said.
            }
            return (mood, unwilling, precept);
        }

        /// <summary>A body part's rows from the facts (after the colony's reading).</summary>
        private static void RowsBodyPart(ItemCardModel m, UseFacts u)
        {
            m.use.Clear();
            string part = u.Part.Length > 0 ? u.Part : "?";
            m.use.Add((W("CC_ItemCard_Row_Part"), W(u.Natural ? "CC_ItemCard_PartNatural" : u.Implant ? "CC_ItemCard_PartImplant" : "CC_ItemCard_PartReplaces", part.Named("PART"))));
            if (!u.Implant && u.Efficiency >= 0f)
                m.use.Add((W("CC_ItemCard_Row_Efficiency"), W(u.Efficiency > 1.0001f ? "CC_ItemCard_EffBetter" : u.Efficiency < 0.9999f ? "CC_ItemCard_EffWorse" : "CC_ItemCard_EffSame", u.Efficiency.ToStringPercent().Named("EFF"))));
            if (u.Adds.Count > 0) m.use.Add((W("CC_ItemCard_Row_Adds"), string.Join(", ", u.Adds.Take(5))));
            if (u.Abilities.Count > 0) m.use.Add((W("CC_ItemCard_Row_Abilities"), ItemCardMath.Cap(ItemCardMath.AndList(u.Abilities))));
            if (!u.Installable) m.use.Add((W("CC_ItemCard_Row_Surgery"), W("CC_ItemCard_SurgeryNone")));
            else
            {
                m.use.Add((W("CC_ItemCard_Row_Surgery"), (u.DoctorLevel > 0 ? W("CC_ItemCard_SurgeryLevel", u.DoctorLevel.Named("LEVEL")) : W("CC_ItemCard_SurgeryAny"))
                                                         + (u.SurgeryResearch.Length > 0 ? " " + W("CC_ItemCard_SurgeryResearch", u.SurgeryResearch.Named("RESEARCH")) : "")));
                if (m.facts.HasColony)
                    m.use.Add((W("CC_ItemCard_Row_Doctor"), u.BestDoctor.Length > 0 && u.BestChance >= 0f
                        ? W("CC_ItemCard_SurgeryChance", u.BestDoctor.Named("DOCTOR"), u.BestChance.ToStringPercent().Named("CHANCE"))
                        : W("CC_ItemCard_SurgeryNoDoctor")));
            }
        }

        // ---------------------------------------------------------------- genes

        /// <summary>A gene holder in hand: its genes, each with what it does, and their sums (once a card).</summary>
        private static void LiveGenes(ItemCardModel m, UsePart up, UseFacts u)
        {
            u.GeneKind = up.geneKind;
            u.GenesKnown = false;
            var set = (m.thing as GeneSetHolderBase)?.GeneSet;
            var genes = set?.GenesListForReading;
            if (genes == null || genes.Count == 0) return;
            u.GenesKnown = true;
            u.GeneCount = genes.Count;
            u.Complexity = set.ComplexityTotal;
            u.Metabolism = set.MetabolismTotal;
            u.Archites = set.ArchitesTotal;
            var names = genes.Where(g => g != null).Take(3).Select(g => g.label).ToList();
            u.GeneNames = string.Join(", ", names) + (genes.Count > names.Count ? ", " + W("CC_ItemCard_AndMore", (genes.Count - names.Count).Named("N")) : "");
            u.Xenotype = (m.thing as Xenogerm)?.xenotypeName ?? "";
            try
            {
                u.Hunger = GeneTuning.MetabolismToFoodConsumptionFactorCurve?.Evaluate(u.Metabolism) ?? ItemCardMath.HungerFor(u.Metabolism);
            }
            catch
            {
                u.Hunger = ItemCardMath.HungerFor(u.Metabolism);
            }
            foreach (var g in genes.Take(12))
            {
                if (g == null) continue;
                string stats = W("CC_ItemCard_GeneStats", g.biostatCpx.Named("CPX"), ItemCardMath.Signed(g.biostatMet).Named("MET")) + (g.biostatArc > 0 ? ", " + W("CC_ItemCard_GeneArc", g.biostatArc.Named("ARC")) : "");
                m.genes.Add(new ItemEffectLine { head = g.LabelCap + " · " + stats, body = GeneEffects(g) });
            }
            if (genes.Count > 12) m.genes.Add(new ItemEffectLine { head = W("CC_ItemCard_AndMore", (genes.Count - 12).Named("N")), body = "" });
            if (m.thing is HumanEmbryo embryo)
            {
                try
                {
                    var parents = new List<string>();
                    if (embryo.Mother != null) parents.Add(embryo.Mother.LabelShort);
                    if (embryo.Father != null) parents.Add(embryo.Father.LabelShort);
                    if (parents.Count > 0) m.useNote = W("CC_ItemCard_EmbryoParents", ItemCardMath.AndList(parents).Named("PARENTS"));
                }
                catch
                {
                    // No parents to name.
                }
            }
        }

        /// <summary>What a gene does, in words: its own effect lines, the stats and capacities it changes, its aptitudes and abilities, what it rules out; else its description's first sentence.</summary>
        private static string GeneEffects(GeneDef g)
        {
            var words = new List<string>();
            try
            {
                if (g.customEffectDescriptions != null) words.AddRange(g.customEffectDescriptions.Where(x => !x.NullOrEmpty()).Select(x => x.TrimEnd('.')));
                foreach (var so in g.statOffsets ?? new List<StatModifier>())
                    if (so?.stat != null) words.Add(so.stat.LabelCap + " " + so.stat.ValueToString(so.value, ToStringNumberSense.Offset));
                foreach (var sf in g.statFactors ?? new List<StatModifier>())
                    if (sf?.stat != null) words.Add(sf.stat.LabelCap + " " + sf.stat.ValueToString(sf.value, ToStringNumberSense.Factor));
                foreach (var cm in g.capMods ?? new List<PawnCapacityModifier>())
                    if (cm?.capacity != null && Math.Abs(cm.offset) > 0.001f) words.Add(cm.capacity.LabelCap + " " + (cm.offset > 0f ? "+" : "") + cm.offset.ToStringPercent());
                foreach (var a in g.aptitudes ?? new List<Aptitude>())
                    if (a?.skill != null) words.Add(a.skill.LabelCap + " " + ItemCardMath.Signed(a.level));
                foreach (var ab in g.abilities ?? new List<AbilityDef>())
                    if (ab != null) words.Add(W("CC_ItemCard_GeneGives", ab.label.Named("ABILITY")));
                foreach (var h in g.makeImmuneTo ?? new List<HediffDef>())
                    if (h != null) words.Add(W("CC_ItemCard_GeneImmune", h.label.Named("WHAT")));
                if (g.disabledWorkTags != WorkTags.None) words.Add(W("CC_ItemCard_GeneNoWork", string.Join(", ", ItemCardMath.Words(g.disabledWorkTags.ToString().Replace(", ", "_")).Select(w => w.ToLowerInvariant())).Named("WORK")));
                if (words.Count == 0 && !g.description.NullOrEmpty())
                {
                    string d = g.description;
                    int dot = d.IndexOf(". ", StringComparison.Ordinal);
                    words.Add(dot > 0 ? d.Substring(0, dot) : d.TrimEnd('.'));
                }
            }
            catch
            {
                // A mod's gene that can't be read: its name alone.
            }
            return string.Join(", ", words.Take(5));
        }

        /// <summary>At home: the gene assembler, and who a xenogerm would suit (their faith, their metabolism after it, their own xenogenes).</summary>
        private static void ColonyGenes(ItemCardModel m, UsePart up, UseFacts u, List<Pawn> people)
        {
            u.Assembler = false;
            u.AssemblerMax = 0;
            u.FaithAgainst = "";
            u.Held = HeldOf(m.map, m.def);
            if (!ModsConfig.BiotechActive) return;
            foreach (var b in m.map.listerBuildings.allBuildingsColonist.OfType<Building_GeneAssembler>())
            {
                u.Assembler = true;
                try
                {
                    u.AssemblerMax = Math.Max(u.AssemblerMax, b.MaxComplexity());
                }
                catch
                {
                    u.AssemblerMax = Math.Max(u.AssemblerMax, ItemCardMath.BaseAssemblerComplexity);
                }
            }
            if (!(m.thing is Xenogerm xg) || !u.GenesKnown) return;
            var list = new List<(Pawn, UseCandidate)>();
            var against = new List<string>();
            foreach (var x in people)
            {
                var c = new UseCandidate { Name = x.LabelShort };
                if (x.genes == null)
                {
                    c.Why = W("CC_ItemCard_WhoNoGenes");
                    list.Add((x, c));
                    continue;
                }
                bool faith = false;
                int after = u.Metabolism;
                try
                {
                    faith = xg.PawnIdeoDisallowsImplanting(x);
                    after = GeneUtility.MetabolismAfterImplanting(x, xg.GeneSet);
                }
                catch
                {
                    // Read as allowed, its own metabolism.
                }
                string now = x.genes.XenotypeLabel ?? "";
                bool own = x.genes.Xenogenes != null && x.genes.Xenogenes.Count > 0;
                if (faith)
                {
                    c.Why = W("CC_ItemCard_WhoFaithForbidsGenes");
                    if (!x.DevelopmentalStage.Juvenile()) against.Add(x.LabelShort);
                }
                else
                {
                    c.Can = true;
                    // The metabolism after it orders them: a bearer who stays fed easily first (held above nothing so all who may are named).
                    c.Gain = Mathf.Max(0.1f, 6f + after);
                    c.Why = W(own ? "CC_ItemCard_WhoXenogermOwn" : "CC_ItemCard_WhoXenogerm", now.Named("NOW"), ItemCardMath.Signed(after).Named("MET"));
                }
                list.Add((x, c));
            }
            if (against.Count > 0) u.FaithAgainst = W("CC_ItemCard_FaithForbidsGenes", ItemCardMath.AndList(against.Take(4).ToList()).Named("WHO"));
            SetWho(m, list, W("CC_ItemCard_WhoXenogermNote"));
        }

        /// <summary>A gene holder's rows from the facts.</summary>
        private static void RowsGenes(ItemCardModel m, UseFacts u)
        {
            m.use.Clear();
            if (!u.GenesKnown)
            {
                m.use.Add((W("CC_ItemCard_Row_Genes"), W(u.GeneKind == "embryo" ? "CC_ItemCard_GenesEmbryoKind" : "CC_ItemCard_GenesKind")));
                return;
            }
            if (u.Xenotype.Length > 0) m.use.Add((W("CC_ItemCard_Row_Xenotype"), u.Xenotype));
            m.use.Add((W("CC_ItemCard_Row_Genes"), W("CC_ItemCard_GenesSum", u.GeneCount.Named("N"), u.Complexity.Named("CPX"), ItemCardMath.Signed(u.Metabolism).Named("MET"), ItemCardMath.N(u.Hunger).Named("HUNGER"))
                                                   + (u.Archites > 0 ? " " + W("CC_ItemCard_GenesArchite", u.Archites.Named("ARC")) : "")));
            if (m.facts.HasColony && ModsConfig.BiotechActive && u.GeneKind == "genepack")
                m.use.Add((W("CC_ItemCard_Row_Assembler"), !u.Assembler ? W("CC_ItemCard_AssemblerNone")
                    : u.Complexity > u.AssemblerMax ? W("CC_ItemCard_AssemblerTooComplex", u.AssemblerMax.Named("MAX"))
                    : W("CC_ItemCard_AssemblerFits", u.Complexity.Named("CPX"), u.AssemblerMax.Named("MAX"))));
            if (u.GeneKind == "xenogerm")
            {
                float coma = HediffDefOf.XenogerminationComa != null ? Lasts(HediffDefOf.XenogerminationComa, 1f) : 0f;
                m.use.Add((W("CC_ItemCard_Row_Implanting"), coma > 0f ? W("CC_ItemCard_ImplantComa", ItemCardMath.Days(coma).Named("DAYS")) : W("CC_ItemCard_ImplantPlain")));
            }
        }
    }
}
