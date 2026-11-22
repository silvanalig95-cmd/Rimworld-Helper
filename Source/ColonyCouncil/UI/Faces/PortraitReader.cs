using System;
using System.Collections.Generic;
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
    /// <item>their race and xenotype (a xenotype of the player's own making has no name to match); whether the race is
    /// built as a human is;</item>
    /// <item>sex; the age band from the game's own stage of life (a baby under one, else a toddler; a child), else their biological age and their kind's life expectancy;</item>
    /// <item>the skin's and hair's colours as the game has them (genes' colours in them);</item>
    /// <item>the hair from its def's words (name, label, texture path, style tags), else the shape of its front texture
    /// measured once, else long for a woman and short for anyone else; bald where the game draws no hair;</item>
    /// <item>the beard from its def's words; the build from the body type's words;</item>
    /// <item>the clothes' age (the outermost piece on the torso whose tech level the game knows, else their people's) and
    /// kind (rags, armour, robes, fine, work), and headgear where the setting shows it;</item>
    /// <item>features from their active genes' words (ears, horns, tusks, fur, scales, gills, glowing eyes, markings, a
    /// tail, a snout), each gene read once;</item>
    /// <item>marks from their health: a lost eye or ear, a bionic eye, a scar or a burn on the head.</item>
    /// </list>
    /// </summary>
    public static class PortraitReader
    {
        private static readonly Dictionary<ThingDef, bool> shaped = new Dictionary<ThingDef, bool>();
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
            return s.Baby() ? GameStage.Baby : s.Child() ? GameStage.Child : s.Adult() ? GameStage.Adult : GameStage.Unknown;
        }

        /// <summary>A pawn's age band (a baby, a toddler, a child, then young, adult, old), cheaply; false for anyone not humanlike.</summary>
        public static bool AgeOf(Pawn p, out PortraitAge age)
        {
            age = PortraitAge.Adult;
            if (p?.RaceProps == null || !p.RaceProps.Humanlike) return false;
            return PortraitLibraryMath.AgeOf(p.ageTracker?.AgeBiologicalYearsFloat ?? 30f, p.RaceProps.lifeExpectancy, GameStageOf(p), out age);
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
                humanShaped = HumanShaped(p.def),
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
                l.baseliner = false;
                l.xenotype = "";
            }
            else
            {
                var x = genes.Xenotype;
                l.baseliner = x == null || x == XenotypeDefOf.Baseliner;
                l.xenotype = x?.defName.ToLowerInvariant() ?? "baseliner";
            }
            var skin = p.story.SkinColor;
            l.skin = new PortraitColour(skin.r, skin.g, skin.b);
            l.unusualSkin = PortraitLibraryMath.UnusualSkin(l.skin);
            var hc = p.story.HairColor;
            l.hairColour = new PortraitColour(hc.r, hc.g, hc.b);
            var hair = p.story.hairDef;
            l.hair = hair == null || hair.noGraphic ? PortraitHairStyle.Bald : HairOf(hair) ?? PortraitLibraryMath.HairFallback(l.sex);
            l.beard = BeardOf(p.style?.beardDef);
            l.build = BuildOf(p.story.bodyType);
            Clothes(p, l);
            Features(p, l);
            Marks(p, l);
            return true;
        }

        /// <summary>A race built as a human is: the game's own kind of race def (another mod's race framework uses its own), the human body, the game's humanlike drawing.</summary>
        private static bool HumanShaped(ThingDef def)
        {
            if (def == null) return false;
            if (shaped.TryGetValue(def, out bool ok)) return ok;
            ok = def.GetType() == typeof(ThingDef) && def.race != null && def.race.body == BodyDefOf.Human
                 && (def.race.renderTree == null || def.race.renderTree.defName == "Humanlike");
            shaped[def] = ok;
            return ok;
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

        /// <summary>The marks their body carries on the head: a lost eye or ear, a bionic eye, a scar or a burn.</summary>
        private static void Marks(Pawn p, PortraitLook l)
        {
            var hediffs = p.health?.hediffSet?.hediffs;
            if (hediffs == null) return;
            foreach (var h in hediffs)
            {
                var part = h?.Part;
                if (part?.def == null) continue;
                var tags = part.def.tags;
                bool eye = tags != null && tags.Contains(BodyPartTagDefOf.SightSource);
                bool ear = tags != null && tags.Contains(BodyPartTagDefOf.HearingSource);
                if (h is Hediff_MissingPart)
                {
                    if (eye) l.marks.Add("lostEye");
                    else if (ear) l.marks.Add("missingEar");
                }
                else if (h is Hediff_AddedPart)
                {
                    if (eye) l.marks.Add("bionicEye");
                }
                else if (h is Hediff_Injury && h.IsPermanent() && (eye || ear || part.IsInGroup(BodyPartGroupDefOf.FullHead) || part.IsInGroup(BodyPartGroupDefOf.UpperHead)))
                {
                    bool burn = h.def.defName.IndexOf("burn", StringComparison.OrdinalIgnoreCase) >= 0;
                    l.marks.Add(burn ? "burn" : "scar");
                }
            }
        }
    }
}
