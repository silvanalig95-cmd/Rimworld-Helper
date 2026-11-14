using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Who a writer is, as their letter's lines are chosen by (the player, 9 November 2026: "the writer's race and culture
    /// shape the tone and style", "you can also be experimentive with grammar, writing style, formatting, phrasing etc if
    /// it makes sense, like strange races or cultures", "how a stupid bandit could not spell words correctly"). Their
    /// xenotype and race (Biotech and the player's race mods), their ideoligion's memes (Ideology and the meme mods), read
    /// softly by defName against these tables, each name one of the player's active defs (docs/player-mods, checked by
    /// Tools/Tests); anyone else writes plainly. Three kinds of answer:
    /// - flavour tags (x: a xenotype's kind, c: a culture), which lines of every speech may carry;
    /// - a strange voice (sv:machine, sv:hive, sv:beast, sv:green, sv:fey, sv:ancient, sv:deep) for writers who are truly
    ///   different: their letters bend the form and are written in lines of their own;
    /// - how well they were schooled (LetterFormMath.SchoolingOf): an outlaw people, little Intellectual skill, a slow
    ///   learner. They write plainly and shortly, spelled right (the player, 13 November 2026: "you can remove these
    ///   purposely placed typos in letter, they don't really work"). Pure: no game types.
    /// </summary>
    public static class LetterCultureMath
    {
        /// <summary>The strange voices, each with lines of its own.</summary>
        public const string Machine = "sv:machine", Hive = "sv:hive", Beast = "sv:beast", Green = "sv:green", Fey = "sv:fey", Ancient = "sv:ancient", Deep = "sv:deep";

        public static readonly string[] Voices = { Machine, Hive, Beast, Green, Fey, Ancient, Deep };

        /// <summary>Xenotypes and races whose writers have a voice of their own (every name an active def of the player's).</summary>
        public static readonly Dictionary<string, string> StrangeXeno = new Dictionary<string, string>
        {
            // Androids: a record, numbered points.
            ["VREA_AndroidAwakened"] = Machine, ["VREA_AndroidBasic"] = Machine,
            // Insectoid and hive-minded: "we", clipped.
            ["VRE_Insector"] = Hive, ["AG_Hiveling"] = Hive, ["Korg"] = Hive,
            // Animal-like: smells, tracks and seasons.
            ["VRE_Lycan"] = Beast, ["VRE_Wolfman"] = Beast, ["VRESaurids_Saurid"] = Beast, ["AG_Animusen"] = Beast,
            ["Bonobo"] = Beast, ["Chimp"] = Beast, ["Gorilla"] = Beast, ["Orangutan"] = Beast, ["RimworldMonkey"] = Beast,
            ["PoI_Absol"] = Beast, ["PoI_Eevee"] = Beast, ["PoI_Espeon"] = Beast, ["PoI_Flareon"] = Beast, ["PoI_Glaceon"] = Beast, ["PoI_Goodra"] = Beast,
            ["PoI_Leafeon"] = Beast, ["PoI_Lopunny"] = Beast, ["PoI_Marshmallow"] = Beast, ["PoI_Netskie"] = Beast, ["PoI_Skitty"] = Beast,
            ["PoI_Umbreon"] = Beast, ["PoI_Vaporeon"] = Beast,
            // Plant folk: light, roots and seasons.
            ["VRE_Animakin"] = Green, ["VRE_Gauranlenkin"] = Green, ["VRE_Poluxkin"] = Green, ["VRE_Ocularkin"] = Green, ["VRE_Fungoid"] = Green, ["AG_Mycormorph"] = Green,
            // Fairies and wood elves: a ritual formula, said at the start and again at the end.
            ["FG_CourtFairy"] = Fey, ["FG_Fairy"] = Fey, ["FG_LesserFairy"] = Fey, ["FG_PixieFairy"] = Fey, ["DevDesigner_XenotypeWoodElves"] = Fey,
            // The long-lived and the ancient: a dated, overly literal register, and a postscript longer than the letter.
            ["Sanguophage"] = Ancient, ["VRE_Bruxa"] = Ancient, ["VRE_Strigoi"] = Ancient, ["VRE_Ekkimian"] = Ancient, ["AG_Malachai"] = Ancient,
            ["AG_Forsaken"] = Ancient, ["DevDesigner_XenotypeHighElves"] = Ancient, ["VRE_Archon"] = Ancient, ["TM_SpiritTD"] = Ancient,
            // The eldritch: words in an order of their own.
            ["AG_MindDevourer"] = Deep
        };

        /// <summary>Memes whose people write in a voice of their own (a gestalt, an eldritch cult).</summary>
        public static readonly Dictionary<string, string> StrangeMemes = new Dictionary<string, string>
        {
            ["VME_Gestalt"] = Hive, ["VME_InsectoidSupremacy"] = Hive,
            ["VME_Structure_ChthonianCult"] = Deep, ["AM_Structure_Horaxian"] = Deep, ["Inhuman"] = Deep, ["AM_Madness"] = Deep
        };

        /// <summary>Xenotypes' kinds as flavour tags (x:): lines of every speech may carry them.</summary>
        public static readonly Dictionary<string, string[]> XenoTags = new Dictionary<string, string[]>
        {
            ["Neanderthal"] = new[] { "x:hulking" }, ["AG_Wretch"] = new[] { "x:hulking" }, ["AG_Efreet"] = new[] { "x:hulking", "x:fiery" },
            ["Genie"] = new[] { "x:refined" }, ["Highmate"] = new[] { "x:refined" }, ["DV_Avaloi"] = new[] { "x:refined", "x:space" }, ["DV_Bogleg"] = new[] { "x:refined" },
            ["AG_Drakonori"] = new[] { "x:refined" },
            ["Hussar"] = new[] { "x:martial" }, ["VREH_Uhlan"] = new[] { "x:martial" }, ["DV_Venator"] = new[] { "x:martial" }, ["Uredd"] = new[] { "x:martial" },
            ["Askbarn"] = new[] { "x:martial" },
            ["Dirtmole"] = new[] { "x:underground" }, ["Stoneborn"] = new[] { "x:underground" }, ["AG_Lapis"] = new[] { "x:underground" },
            ["Waster"] = new[] { "x:toxic" }, ["AG_Taukai"] = new[] { "x:toxic" }, ["AG_Helixien"] = new[] { "x:toxic" },
            ["Impid"] = new[] { "x:fiery" },
            ["Yttakin"] = new[] { "x:cold" },
            ["Pigskin"] = new[] { "x:pig" }, ["VRE_Boarskin"] = new[] { "x:pig" },
            ["Stagz_Merfolk_Coastal"] = new[] { "x:sea" }, ["Stagz_Merfolk_Abyssal"] = new[] { "x:sea" }, ["AG_Nereid"] = new[] { "x:sea" },
            ["Starjack"] = new[] { "x:space" }, ["AG_Fleetkind"] = new[] { "x:space" },
            ["DevDesigner_XenotypeDarkElves"] = new[] { "x:elf" }
        };

        /// <summary>Memes as culture tags (c:): the devout, raiders, the proud, the loyal, nature's own, herders, the many, the free...</summary>
        public static readonly Dictionary<string, string[]> MemeTags = new Dictionary<string, string[]>
        {
            ["Structure_TheistEmbodied"] = new[] { "c:pious" }, ["Structure_TheistAbstract"] = new[] { "c:pious" }, ["Structure_OriginChristian"] = new[] { "c:pious" },
            ["Structure_OriginIslamic"] = new[] { "c:pious" }, ["Structure_OriginHindu"] = new[] { "c:pious" }, ["Structure_OriginBuddhist"] = new[] { "c:pious" },
            ["AM_DeepDevotion"] = new[] { "c:pious" }, ["AM_Monastic"] = new[] { "c:pious" }, ["VME_ExaltedPriesthood"] = new[] { "c:pious" },
            ["AM_Structure_Jewish"] = new[] { "c:pious" }, ["AM_Structure_Sikhism"] = new[] { "c:pious" }, ["AM_Structure_Jainism"] = new[] { "c:pious", "c:peace" },
            ["AM_Structure_Kemetism"] = new[] { "c:pious" }, ["VME_Structure_Shintaoism"] = new[] { "c:pious" }, ["VFEM2_Structure_Norse"] = new[] { "c:pious", "c:martial" },
            ["Raider"] = new[] { "c:raider" }, ["VFEP_PirateMeme"] = new[] { "c:raider" }, ["AM_Structure_Corsair"] = new[] { "c:raider" }, ["VME_ViolentConversion"] = new[] { "c:raider" },
            ["AM_Gladiator"] = new[] { "c:martial" }, ["VME_Bushido"] = new[] { "c:martial" }, ["AM_Sharpshooter"] = new[] { "c:martial" }, ["AM_Bulwark"] = new[] { "c:martial" },
            ["Supremacist"] = new[] { "c:proud" }, ["HumanPrimacy"] = new[] { "c:proud" }, ["VME_Nationalist"] = new[] { "c:proud" }, ["VME_Aristocratic"] = new[] { "c:proud" },
            ["VME_Royal"] = new[] { "c:proud", "c:loyal" }, ["VME_GodEmperor"] = new[] { "c:loyal" }, ["VME_BloodCourt"] = new[] { "c:proud" },
            ["Loyalist"] = new[] { "c:loyal" }, ["VME_Structure_Authoritarianism"] = new[] { "c:loyal" }, ["VME_Structure_CultOfPersonality"] = new[] { "c:loyal" },
            ["NaturePrimacy"] = new[] { "c:nature" }, ["TreeConnection"] = new[] { "c:nature" }, ["AnimalPersonhood"] = new[] { "c:nature" }, ["Structure_Animist"] = new[] { "c:nature" },
            ["AM_GauranlenSupremacy"] = new[] { "c:nature" }, ["VME_Vegan"] = new[] { "c:nature" },
            ["Rancher"] = new[] { "c:herd" }, ["AM_Cowboys"] = new[] { "c:herd" }, ["VME_Nomad"] = new[] { "c:herd" },
            ["Collectivist"] = new[] { "c:together" }, ["VME_Egalitarian"] = new[] { "c:together" }, ["VME_Republic"] = new[] { "c:together" },
            ["Individualist"] = new[] { "c:free" }, ["VME_Emancipation"] = new[] { "c:free" }, ["VME_Progressive"] = new[] { "c:free" },
            ["Transhumanist"] = new[] { "c:transhuman" }, ["VME_MechanoidSupremacy"] = new[] { "c:transhuman" }, ["GR_MadScientists"] = new[] { "c:transhuman" },
            ["Tunneler"] = new[] { "c:tunnel" }, ["Darkness"] = new[] { "c:dark" }, ["Blindsight"] = new[] { "c:dark" },
            ["PainIsVirtue"] = new[] { "c:pain" }, ["AM_PainIsDivine"] = new[] { "c:pain" }, ["Guilty"] = new[] { "c:guilty" },
            ["HighLife"] = new[] { "c:highlife" }, ["VME_PartyLife"] = new[] { "c:highlife" }, ["VME_Structure_Bacchanalianism"] = new[] { "c:highlife" },
            ["AM_Epicurean"] = new[] { "c:highlife" }, ["Proselytizer"] = new[] { "c:preach" },
            ["VME_Cultured"] = new[] { "c:craft" }, ["AM_Artist"] = new[] { "c:craft" }, ["VME_CraftCulture"] = new[] { "c:craft" },
            ["VME_Trader"] = new[] { "c:trade" }, ["VME_Pacifist"] = new[] { "c:peace" }, ["AM_NonViolence"] = new[] { "c:peace" },
            ["VME_Angler"] = new[] { "c:sea" }, ["VVE_Nauticals"] = new[] { "c:sea" }, ["AM_WaterPrimacy"] = new[] { "c:sea" },
            ["AM_TeaPrimacy"] = new[] { "c:tea" }, ["AM_CoffeePrimacy"] = new[] { "c:coffee" }, ["Cannibal"] = new[] { "c:cannibal" }
        };

        /// <summary>
        /// Precepts as tags (p:), for a zealous writer's sermon (10 November 2026: "ideological ramblings", by their
        /// ideoligion's real precepts): charity, no meat, no drink, pain, the trees, no mining, no learning, preaching, going
        /// bare, the road, the sacred fish, the animals, the body remade or kept pure, one faith only, hardship, chastity,
        /// scars, the blind, fungus, insects, cannibals, raiding, drugs, the dark. Every name an active PreceptDef of the
        /// player's (docs/player-mods/defs.tsv.gz, checked by Tools/Tests).
        /// </summary>
        public static readonly Dictionary<string, string> Precepts = new Dictionary<string, string>
        {
            ["Charity_Essential"] = "p:charity", ["Charity_Important"] = "p:charity",
            ["MeatEating_Abhorrent"] = "p:nomeat", ["MeatEating_Horrible"] = "p:nomeat", ["MeatEating_Disapproved"] = "p:nomeat",
            ["DrugUse_Prohibited"] = "p:sober", ["DrugUse_Essential"] = "p:drugs",
            ["Pain_Idealized"] = "p:pain",
            ["TreeCutting_Prohibited"] = "p:trees", ["TreeCutting_Horrible"] = "p:trees", ["Trees_Desired"] = "p:trees",
            ["Mining_Prohibited"] = "p:nomining", ["Mining_Horrible"] = "p:nomining",
            ["Research_None"] = "p:unlearned", ["Research_ExtremelySlow"] = "p:unlearned", ["Research_VerySlow"] = "p:unlearned",
            ["Proselytizing_Frequently"] = "p:preach",
            ["Nudity_Male_Mandatory"] = "p:nude", ["Nudity_Female_Mandatory"] = "p:nude",
            ["Nomadic_Preferred"] = "p:nomad",
            ["Fishing_Sacred"] = "p:fishing",
            ["AnimalVenerated"] = "p:animals", ["AnimalConnection_Strong"] = "p:animals",
            ["BodyMod_Approved"] = "p:bodymod", ["BodyMod_Abhorrent"] = "p:purebody",
            ["IdeoDiversity_Abhorrent"] = "p:onefaith", ["IdeoDiversity_Horrible"] = "p:onefaith", ["Apostasy_Abhorrent"] = "p:onefaith",
            ["Temperature_Tough"] = "p:hardy", ["RoughLiving_Welcomed"] = "p:hardy", ["Comfort_Ignored"] = "p:hardy",
            ["Lovin_Prohibited"] = "p:chaste", ["Lovin_SpouseOnly_Strict"] = "p:chaste",
            ["Scarification_Extreme"] = "p:scars", ["Scarification_Heavy"] = "p:scars",
            ["Blindness_Sublime"] = "p:blind", ["Blindness_Elevated"] = "p:blind",
            ["FungusEating_Preferred"] = "p:fungus", ["InsectMeatEating_Loved"] = "p:insects",
            ["Cannibalism_Preferred"] = "p:cannibal", ["Cannibalism_RequiredStrong"] = "p:cannibal", ["Cannibalism_RequiredRavenous"] = "p:cannibal",
            ["Raiding_Required"] = "p:raiding", ["Raiding_Respected"] = "p:raiding",
            ["Darklight_Preferred"] = "p:dark", ["DarknessCombat_Preferred"] = "p:dark"
        };

        /// <summary>A writer's precepts (by defName) as their tags (p:), added to <paramref name="tags"/>.</summary>
        public static void ReadPrecepts(IEnumerable<string> precepts, ICollection<string> tags)
        {
            if (precepts == null || tags == null) return;
            foreach (var p in precepts)
                if (!string.IsNullOrEmpty(p) && Precepts.TryGetValue(p, out var t) && !tags.Contains(t))
                    tags.Add(t);
        }

        /// <summary>Peoples (faction defs, the player's active ones) of outlaws and bandits: their letters are seldom well schooled.</summary>
        public static readonly HashSet<string> Outlaws = new HashSet<string>
        {
            "Pirate", "PirateWaster", "PirateYttakin", "CannibalPirate", "SettlerSavage", "VFEP_Junkers", "AG_XenohumanPirates", "VFEM2_ClanSavage", "VFEM2_KingdomSavage"
        };

        /// <summary>Traits that lower or raise how well someone writes (the player's active traits, by defName).</summary>
        public const string SlowLearner = "SlowLearner", TooSmart = "TooSmart", Scholar = "Rimfolk_Scholar";

        /// <summary>
        /// The writer's strange voice (their xenotype or race first, then their memes), or null, and their flavour tags
        /// (x: and c:) added to <paramref name="tags"/>.
        /// </summary>
        public static string Read(string xenotype, string race, IEnumerable<string> memes, ICollection<string> tags)
        {
            string voice = null;
            if (!string.IsNullOrEmpty(xenotype))
            {
                if (StrangeXeno.TryGetValue(xenotype, out var v)) voice = v;
                if (XenoTags.TryGetValue(xenotype, out var xs))
                    foreach (var t in xs)
                        tags?.Add(t);
            }
            if (voice == null && !string.IsNullOrEmpty(race) && StrangeXeno.TryGetValue(race, out var rv)) voice = rv;
            if (memes != null)
                foreach (var m in memes)
                {
                    if (string.IsNullOrEmpty(m)) continue;
                    if (voice == null && StrangeMemes.TryGetValue(m, out var mv)) voice = mv;
                    if (MemeTags.TryGetValue(m, out var cs))
                        foreach (var t in cs)
                            tags?.Add(t);
                }
            return voice;
        }

        /// <summary>Every name the tables use (for the test that each is one of the player's active defs).</summary>
        public static IEnumerable<(string name, string kind)> Names()
        {
            foreach (var k in StrangeXeno.Keys) yield return (k, k == "TM_SpiritTD" ? "race" : "xenotype");
            foreach (var k in XenoTags.Keys) yield return (k, "xenotype");
            foreach (var k in StrangeMemes.Keys) yield return (k, "meme");
            foreach (var k in MemeTags.Keys) yield return (k, "meme");
            foreach (var k in Outlaws) yield return (k, "faction");
            yield return (SlowLearner, "trait");
            yield return (TooSmart, "trait");
            yield return (Scholar, "trait");
        }
    }
}
