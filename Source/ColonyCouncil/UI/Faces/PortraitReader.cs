using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A pawn's looks read into the portrait library's words (<see cref="PortraitLook"/>, <see cref="PortraitLibraryMath"/>;
    /// docs: DESIGN.md, "Portraits, phase 3"), from the game's own data, the same for every mod's race, xenotype, hair,
    /// beard, body type and clothes:
    /// <list type="bullet">
    /// <item>their race and xenotype (a xenotype of the player's own making by its name; a hybrid none);</item>
    /// <item>sex; how old they look (<see cref="AgeOf(Pawn, out PortraitAge)"/>: their biological age, the game's own stages
    /// for the young ones, their kind's life span and genes for the grown-ups, the ageless by their years on a longer scale);</item>
    /// <item>the skin's and hair's colours as the game has them (genes' colours in them);</item>
    /// <item>the hair from its def's words (name, label, texture path, style tags), else the shape of its front texture
    /// measured once, else long for a woman and short for anyone else; bald where the game draws no hair;</item>
    /// <item>the beard from its def's words; the build from the body type's words;</item>
    /// <item>the clothes' age (the outermost piece on the torso whose tech level the game knows, else their people's) and
    /// kind (rags, armour, robes, fine, work), and headgear where the setting shows it;</item>
    /// <item>features from their active genes' words (ears, horns, tusks, fur, scales, gills, glowing eyes, markings, a
    /// tail, a snout), each gene read once;</item>
    /// <item>marks from their health: a lost eye or ear, a bionic eye, a scar or a burn on the head, an artificial arm or hand;</item>
    /// <item>their looks (phase 5: <see cref="PortraitLooks"/>, from their traits, genes, hediffs and body) and their work
    /// (<see cref="RoleOf"/>: their grown-up backstory where it names one, else the colony's leader or a titled noble, a
    /// moral guide, else their best skill with a passion; none for a child).</item>
    /// </list>
    /// </summary>
    public static class PortraitReader
    {
        private static readonly Dictionary<GeneDef, List<string>> geneFeatures = new Dictionary<GeneDef, List<string>>();
        private static readonly Dictionary<HairDef, PortraitHairStyle?> hairs = new Dictionary<HairDef, PortraitHairStyle?>();
        private static readonly Dictionary<BeardDef, PortraitBeard> beards = new Dictionary<BeardDef, PortraitBeard>();
        private static readonly Dictionary<BodyTypeDef, PortraitBuild> builds = new Dictionary<BodyTypeDef, PortraitBuild>();
        private static readonly Dictionary<ThingDef, List<string>> apparelWords = new Dictionary<ThingDef, List<string>>();
        private static readonly List<PortraitLibraryMath.WornPiece> worn = new List<PortraitLibraryMath.WornPiece>();

        /// <summary>The game's own stage of a pawn's life: a baby (a newborn too), a child, a grown-up.</summary>
        public static GameStage GameStageOf(Pawn p)
        {
            var s = p.DevelopmentalStage;
            if (s.Baby()) return GameStage.Baby;
            if (s.Child()) return GameStage.Child;
            if (!s.Adult()) return GameStage.Unknown;
            // The game's teenage stage (HumanlikeTeenager; a race of another mod by its stage's name), and past it.
            string stage = p.ageTracker?.CurLifeStage?.defName ?? "";
            if (stage.IndexOf("teen", StringComparison.OrdinalIgnoreCase) >= 0 && stage.IndexOf("preteen", StringComparison.OrdinalIgnoreCase) < 0) return GameStage.Teen;
            return stage == "HumanlikeAdult" ? GameStage.Grown : GameStage.Adult;
        }

        private static StatDef lifespan;
        private static bool lifespanLooked;
        private static readonly Dictionary<GeneDef, bool> stopsAgeing = new Dictionary<GeneDef, bool>();

        /// <summary>The game's life span factor of a pawn (the genes for a long or short life), 1 where the game has none.</summary>
        private static float LifespanOf(Pawn p)
        {
            if (!lifespanLooked)
            {
                lifespanLooked = true;
                lifespan = DefDatabase<StatDef>.GetNamedSilentFail("LifespanFactor");
            }
            if (lifespan == null) return 1f;
            try
            {
                return p.GetStatValue(lifespan);
            }
            catch (Exception)
            {
                return 1f;
            }
        }

        /// <summary>
        /// Whether a pawn's ageing has stopped: an active gene that stops it (Biotech's Ageless, the dark
        /// elves' DevDesigner_Ageless), or any gene whose ageing curve stops at nothing; each gene read once.
        /// </summary>
        private static bool Ageless(Pawn p)
        {
            var genes = p.genes?.GenesListForReading;
            if (genes == null) return false;
            for (int i = 0; i < genes.Count; i++)
            {
                var g = genes[i];
                if (g?.def == null || !g.Active) continue;
                if (!stopsAgeing.TryGetValue(g.def, out bool stops))
                {
                    string n = g.def.defName;
                    stops = n == "Ageless" || n == "DevDesigner_Ageless";
                    var curve = g.def.biologicalAgeTickFactorFromAgeCurve;
                    if (!stops && curve != null)
                        foreach (var point in curve)
                            if (point.y <= 0.001f)
                                stops = true;
                    stopsAgeing[g.def] = stops;
                }
                if (stops) return true;
            }
            return false;
        }

        /// <summary>
        /// How old a pawn looks as an age band (<see cref="PortraitLibraryMath.AgeOf(float, float, float, float, GameStage, bool, out PortraitAge)"/>):
        /// their biological age, the game's stage of life, their race's life expectancy times their life span factor, and for
        /// someone whose ageing has stopped their years since birth on a much longer scale. False for anyone not humanlike.
        /// </summary>
        public static bool AgeOf(Pawn p, out PortraitAge age)
        {
            age = PortraitAge.Adult;
            if (p?.RaceProps == null || !p.RaceProps.Humanlike) return false;
            float bio = p.ageTracker?.AgeBiologicalYearsFloat ?? 30f, born = p.ageTracker?.AgeChronologicalYearsFloat ?? bio;
            return PortraitLibraryMath.AgeOf(bio, born, p.RaceProps.lifeExpectancy, LifespanOf(p), GameStageOf(p), Ageless(p), out age);
        }

        /// <summary>A pawn's looks; false for anyone the library has no pictures for at all (not humanlike).</summary>
        public static bool LookOf(Pawn p, out PortraitLook l)
        {
            l = null;
            if (p?.RaceProps == null || !p.RaceProps.Humanlike || p.story == null) return false;
            if (!AgeOf(p, out var age)) return false;
            l = new PortraitLook
            {
                race = p.def.defName.ToLowerInvariant(),
                sex = p.gender == Gender.Male ? PortraitSex.Male : p.gender == Gender.Female ? PortraitSex.Female : PortraitSex.Any,
                age = age
            };
            var genes = p.genes;
            if (genes == null)
            {
                l.baseliner = true;
                l.xenotype = "baseliner";
            }
            else if (genes.UniqueXenotype)
            {
                // A xenotype of the player's own making matches pictures listing its name; a hybrid or one with no name, none.
                l.baseliner = false;
                l.ownXenotype = true;
                l.xenotype = genes.hybrid || genes.xenotypeName.NullOrEmpty() ? "" : genes.xenotypeName.Trim().ToLowerInvariant();
            }
            else
            {
                var x = genes.Xenotype;
                l.baseliner = x == null || x == XenotypeDefOf.Baseliner;
                l.xenotype = x?.defName.ToLowerInvariant() ?? "baseliner";
            }
            var skin = p.story.SkinColor;
            l.skin = new PortraitColour(skin.r, skin.g, skin.b);
            var hc = p.story.HairColor;
            l.hairColour = new PortraitColour(hc.r, hc.g, hc.b);
            var hair = p.story.hairDef;
            l.hair = hair == null || hair.noGraphic ? PortraitHairStyle.Bald : HairOf(hair) ?? PortraitLibraryMath.HairFallback(l.sex);
            l.beard = BeardOf(p.style?.beardDef);
            l.build = BuildOf(p.story.bodyType);
            Clothes(p, l);
            Features(p, l);
            Marks(p, l);
            PortraitLooks.Fill(p, l);
            l.role = RoleOf(p, age);
            return true;
        }

        private static readonly Dictionary<BackstoryDef, string> backstoryRoles = new Dictionary<BackstoryDef, string>();
        private static RoyalTitleDef knight;
        private static bool knightLooked;
        private static readonly List<PortraitLookMath.SkillSeen> skillBuf = new List<PortraitLookMath.SkillSeen>();

        /// <summary>
        /// A person's work, for the matcher (the player, 22 November 2026: "the profession can and should also be reflected
        /// in how they look like"): none for a child; their grown-up backstory where its title, name or spawn categories
        /// clearly name one (<see cref="PortraitLookMath.RoleFromWords"/>; each backstory read once); else a leader for
        /// their people's leader, a holder of a royal title of knight or higher, or their faith's leader; else a priest for
        /// their faith's moral guide, a priest's role or the colony's own priest; else the work of their best skill with a passion
        /// (<see cref="PortraitLookMath.RoleFromSkills"/>); else none.
        /// </summary>
        public static string RoleOf(Pawn p, PortraitAge age)
        {
            if (p == null || age < PortraitAge.Teen) return "";
            try
            {
                var bs = p.story?.Adulthood;
                if (bs != null)
                {
                    if (!backstoryRoles.TryGetValue(bs, out var r))
                    {
                        r = PortraitLookMath.RoleFromWords(PortraitLibraryMath.Tokens(bs.title, bs.titleShort), PortraitLibraryMath.Tokens(bs.defName),
                            PortraitLibraryMath.Tokens(bs.spawnCategories?.ToArray())) ?? "";
                        backstoryRoles[bs] = r;
                    }
                    if (r.Length > 0) return r;
                }
                var role = ModsConfig.IdeologyActive ? p.Ideo?.GetRole(p) : null;
                string roleName = role?.def?.defName ?? "";
                if (p.Faction != null && p.Faction.leader == p || Titled(p) || roleName == "IdeoRole_Leader") return "leader";
                // Their faith's moral guide, a priest's role, or the colony's own priest (the council's seat: Gods/Worship).
                if (roleName == "IdeoRole_Moralist" || roleName.IndexOf("priest", StringComparison.OrdinalIgnoreCase) >= 0 || Worship.IsPriest(p)) return "priest";
                var skills = p.skills?.skills;
                if (skills == null) return "";
                skillBuf.Clear();
                foreach (var s in skills)
                    if (s?.def != null)
                        skillBuf.Add(new PortraitLookMath.SkillSeen(s.def.defName, s.Level, (int)s.passion, s.TotallyDisabled));
                var tech = p.Faction?.def?.techLevel ?? TechLevel.Undefined;
                return PortraitLookMath.RoleFromSkills(skillBuf, tech != TechLevel.Undefined && tech <= TechLevel.Neolithic) ?? "";
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: reading a person's work", e);
                return "";
            }
        }

        /// <summary>A royal title of knight or higher (any title where the game has no knight).</summary>
        public static bool Titled(Pawn p)
        {
            var t = p.royalty?.MostSeniorTitle;
            if (t?.def == null) return false;
            if (!knightLooked)
            {
                knightLooked = true;
                knight = DefDatabase<RoyalTitleDef>.GetNamedSilentFail("Knight");
            }
            return knight == null || t.def.seniority >= knight.seniority;
        }

        private static string[] Words(Def def, string texPath, IEnumerable<string> tags)
        {
            var list = new List<string> { def.defName, def.label, texPath };
            if (tags != null) list.AddRange(tags);
            return list.ToArray();
        }

        /// <summary>The hair from its words, else its front texture's shape (each read once a def), null where neither tells.</summary>
        private static PortraitHairStyle? HairOf(HairDef def)
        {
            if (hairs.TryGetValue(def, out var h)) return h;
            h = PortraitLibraryMath.HairFromWords(PortraitLibraryMath.Tokens(Words(def, def.texPath, def.styleTags))) ?? Measured(def);
            hairs[def] = h;
            return h;
        }

        private static PortraitBeard BeardOf(BeardDef def)
        {
            if (def == null || def.noGraphic) return PortraitBeard.None;
            if (!beards.TryGetValue(def, out var b))
            {
                b = PortraitLibraryMath.BeardFromWords(PortraitLibraryMath.Tokens(Words(def, def.texPath, def.styleTags)), false);
                beards[def] = b;
            }
            return b;
        }

        private static PortraitBuild BuildOf(BodyTypeDef def)
        {
            if (def == null) return PortraitBuild.Average;
            if (!builds.TryGetValue(def, out var b))
            {
                b = PortraitLibraryMath.BuildFromWords(PortraitLibraryMath.Tokens(def.defName, def.label));
                builds[def] = b;
            }
            return b;
        }

        /// <summary>
        /// The hair's front texture measured once: copied small onto a texture of Rimfolk's own (the game keeps loaded
        /// textures unreadable) and each row's cover summed (<see cref="PortraitLibraryMath.HairFromShape"/>).
        /// </summary>
        private static PortraitHairStyle? Measured(HairDef def)
        {
            if (def.texPath.NullOrEmpty()) return null;
            const int S = 64;
            RenderTexture rt = null;
            Texture2D read = null;
            var active = RenderTexture.active;
            try
            {
                var tex = ContentFinder<Texture2D>.Get(def.texPath + "_south", false) ?? ContentFinder<Texture2D>.Get(def.texPath, false);
                if (tex == null) return null;
                rt = RenderTexture.GetTemporary(S, S, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                read = new Texture2D(S, S, TextureFormat.RGBA32, false);
                read.ReadPixels(new Rect(0f, 0f, S, S), 0, 0, false);
                read.Apply(false);
                var px = read.GetPixels32();
                // The texture's rows come from its foot up: turned to run from the top.
                var rows = new float[S];
                for (int y = 0; y < S; y++)
                {
                    float sum = 0f;
                    for (int x = 0; x < S; x++) sum += px[y * S + x].a / 255f;
                    rows[S - 1 - y] = sum;
                }
                return PortraitLibraryMath.HairFromShape(rows, S);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: measuring a hair's length from its texture", e);
                return null;
            }
            finally
            {
                RenderTexture.active = active;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (read != null) UnityEngine.Object.Destroy(read);
            }
        }

        /// <summary>
        /// The clothes' age and kind, and headgear: the outermost piece on the torso decides the kind (armour where the
        /// game rates it so, a robe or cloak by its words, fine where made excellent or better or a noble's, rags where
        /// worn to tatters or nothing is on the torso).
        /// </summary>
        private static void Clothes(Pawn p, PortraitLook l)
        {
            worn.Clear();
            Apparel outer = null;
            int outerLayer = int.MinValue;
            bool head = false;
            var list = p.apparel?.WornApparel;
            if (list != null)
            {
                foreach (var a in list)
                {
                    var props = a?.def?.apparel;
                    if (props == null) continue;
                    bool torso = props.bodyPartGroups != null && props.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso);
                    int layer = props.LastLayer?.drawOrder ?? 0;
                    worn.Add(new PortraitLibraryMath.WornPiece((int)a.def.techLevel, layer, torso));
                    if (torso && layer > outerLayer)
                    {
                        outer = a;
                        outerLayer = layer;
                    }
                    if (props.bodyPartGroups != null && (props.bodyPartGroups.Contains(BodyPartGroupDefOf.FullHead) || props.bodyPartGroups.Contains(BodyPartGroupDefOf.UpperHead))) head = true;
                }
            }
            l.era = PortraitLibraryMath.EraOfWorn(worn, (int)(p.Faction?.def?.techLevel ?? TechLevel.Undefined));
            bool armour = false, fine = false, tattered = false;
            List<string> words = null;
            if (outer != null)
            {
                if (!apparelWords.TryGetValue(outer.def, out words))
                {
                    words = PortraitLibraryMath.Tokens(outer.def.defName, outer.def.label);
                    apparelWords[outer.def] = words;
                }
                armour = outer.def.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp, outer.Stuff) >= 0.4f;
                fine = outer.TryGetQuality(out var q) && q >= QualityCategory.Excellent;
                tattered = outer.MaxHitPoints > 0 && outer.HitPoints < 0.5f * outer.MaxHitPoints;
            }
            l.dress = PortraitLibraryMath.DressOf(words, outer == null, tattered, armour, fine);
            l.headwear = head && FaceEngine.Headgear;
        }

        /// <summary>The features their active genes show, each gene's words read once.</summary>
        private static void Features(Pawn p, PortraitLook l)
        {
            var genes = p.genes?.GenesListForReading;
            if (genes == null) return;
            for (int i = 0; i < genes.Count; i++)
            {
                var g = genes[i];
                if (g?.def == null || !g.Active) continue;
                if (!geneFeatures.TryGetValue(g.def, out var f))
                {
                    var texts = new List<string> { g.def.defName, g.def.label };
                    if (g.def.renderNodeProperties != null)
                        foreach (var n in g.def.renderNodeProperties)
                            if (n != null)
                                texts.Add(n.texPath);
                    f = PortraitLibraryMath.FeaturesOf(PortraitLibraryMath.Tokens(texts.ToArray()));
                    geneFeatures[g.def] = f;
                }
                foreach (var x in f) l.features.Add(x);
            }
        }

        /// <summary>
        /// The marks their body carries: on the head a lost eye or ear, a bionic eye, a scar or a burn; an artificial arm or
        /// hand (artificialArm: an added part, a bionic, archotech or prosthetic one or a mod's, on an arm, a shoulder or a hand
        /// or a part of one, <see cref="ArmPart"/>).
        /// </summary>
        private static void Marks(Pawn p, PortraitLook l)
        {
            var hediffs = p.health?.hediffSet?.hediffs;
            if (hediffs != null)
                foreach (var h in hediffs)
                {
                    var part = h?.Part;
                    if (part?.def == null) continue;
                    var tags = part.def.tags;
                    bool eye = tags != null && tags.Contains(BodyPartTagDefOf.SightSource);
                    bool ear = tags != null && tags.Contains(BodyPartTagDefOf.HearingSource);
                    if (h is Hediff_MissingPart)
                    {
                        if (eye) l.AddMark("lostEye", SideOf(part));
                        else if (ear) l.AddMark("missingEar", SideOf(part));
                    }
                    else if (h is Hediff_AddedPart || h.def?.addedPartProps != null)
                    {
                        // A patch put in an eye's place (a mod's) is a lost eye under an eye patch, not a bionic eye.
                        if (eye && PatchPart(h.def))
                        {
                            l.AddMark("lostEye", SideOf(part));
                            l.eyePatch = true;
                        }
                        else if (eye) l.AddMark("bionicEye", SideOf(part));
                        else if (ArmPart(part)) l.AddMark("artificialArm", SideOf(part));
                    }
                    else if (h is Hediff_Injury && h.IsPermanent() && (eye || ear || part.IsInGroup(BodyPartGroupDefOf.FullHead) || part.IsInGroup(BodyPartGroupDefOf.UpperHead)))
                    {
                        bool burn = h.def.defName.IndexOf("burn", StringComparison.OrdinalIgnoreCase) >= 0;
                        l.AddMark(burn ? "burn" : "scar");
                    }
                }
            // An eye patch worn (an apparel of that name, Alpha Memes' among them): a lost eye under it is shown with the patch.
            var worn = p.apparel?.WornApparel;
            if (worn != null)
                foreach (var a in worn)
                    if (a?.def != null && PatchPart(a.def))
                        l.eyePatch = true;
        }

        private static readonly Dictionary<Def, bool> patches = new Dictionary<Def, bool>();

        /// <summary>A def named for an eye patch (each read once).</summary>
        private static bool PatchPart(Def d)
        {
            if (d == null) return false;
            if (!patches.TryGetValue(d, out bool patch))
            {
                patch = PortraitLibraryMath.EyePatchWords(PortraitLibraryMath.Tokens(d.defName, d.label));
                patches[d] = patch;
            }
            return patch;
        }

        /// <summary>
        /// The side a body part is on, as seen from the person: by the words of its label as the body def writes it ("left
        /// eye") and the tag its wounds are drawn at ("LeftEye"), else its parent's (a finger on the left hand).
        /// </summary>
        private static PortraitSide SideOf(BodyPartRecord part)
        {
            for (var at = part; at != null; at = at.parent)
            {
                var side = PortraitLibraryMath.SideFromWords(PortraitLibraryMath.Tokens(at.untranslatedCustomLabel, at.woundAnchorTag));
                if (side != PortraitSide.None) return side;
            }
            return PortraitSide.None;
        }

        /// <summary>
        /// The marks of a pawn's body alone (for the version of a kept picture shown, read every few seconds while they are
        /// shown and only where their picture has mark variants); null for anyone not humanlike.
        /// </summary>
        public static PortraitLook MarksOf(Pawn p)
        {
            if (p?.RaceProps == null || !p.RaceProps.Humanlike) return null;
            var l = new PortraitLook();
            try
            {
                Marks(p, l);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: reading a person's marks", e);
            }
            return l;
        }

        private static readonly Dictionary<BodyPartDef, bool> armParts = new Dictionary<BodyPartDef, bool>();

        /// <summary>
        /// A body part that is an arm, a shoulder or a hand, or whose parent is one (a finger on a hand): by its def's name,
        /// label or the game's tags for the limbs that handle things (<see cref="PortraitLibraryMath.ArmPart"/>), so a mod's
        /// body counts as well; each part's def read once.
        /// </summary>
        private static bool ArmPart(BodyPartRecord part) => IsArm(part.def) || part.parent?.def != null && IsArm(part.parent.def);

        private static bool IsArm(BodyPartDef d)
        {
            if (!armParts.TryGetValue(d, out bool arm))
            {
                arm = PortraitLibraryMath.ArmPart(PortraitLibraryMath.Tokens(d.defName, d.label), d.tags?.Select(t => t?.defName));
                armParts[d] = arm;
            }
            return arm;
        }
    }
}
