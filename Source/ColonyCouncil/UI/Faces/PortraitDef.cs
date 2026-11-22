using System;
using System.Collections.Generic;
using System.IO;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One picture of the portrait library (docs: DESIGN.md, "Portraits, phase 3" and "Portraits, phase 4";
    /// <c>docs/portrait-art/FRAMEWORK.md</c>), described from the picture itself, in the Defs of any mod (the player's own
    /// pack "Rimfolk Portraits" writes one per picture). Every field is a word or a colour; unknown words are reported as
    /// def errors at start-up. For example:
    /// <code>
    /// &lt;ColonyCouncil.PortraitDef&gt;
    ///   &lt;defName&gt;Portrait_colonists_Avila_01&lt;/defName&gt;
    ///   &lt;texPath&gt;colonists/Avila_style_01_cut_00001_&lt;/texPath&gt;
    ///   &lt;pawn&gt;Avila&lt;/pawn&gt;
    ///   &lt;xenotypes&gt;&lt;li&gt;Baseliner&lt;/li&gt;&lt;/xenotypes&gt;
    ///   &lt;sex&gt;male&lt;/sex&gt; &lt;age&gt;young&lt;/age&gt; &lt;skinColor&gt;#c9a27e&lt;/skinColor&gt; &lt;hairColor&gt;#1a1a1a&lt;/hairColor&gt;
    ///   &lt;hair&gt;tied&lt;/hair&gt; &lt;beard&gt;none&lt;/beard&gt; &lt;build&gt;slim&lt;/build&gt; &lt;era&gt;medieval&lt;/era&gt; &lt;dress&gt;work&lt;/dress&gt;
    ///   &lt;facing&gt;threeQuarterLeft&lt;/facing&gt; &lt;character&gt;Avila&lt;/character&gt;
    /// &lt;/ColonyCouncil.PortraitDef&gt;
    /// </code>
    /// A variant of the same face (a lost eye, a scar, older) has the same <see cref="character"/>, its own
    /// <see cref="variant"/> and its <see cref="marks"/> or age.
    /// </summary>
    public class PortraitDef : Def
    {
        /// <summary>
        /// The picture, without ".png": under its mod's <c>Portraits</c> folder (a person cut out on a clear ground, from about
        /// the belt up, read only when shown), or for an older def a texture under Textures.
        /// </summary>
        public string texPath;
        /// <summary>A colonist of the player's this picture is for alone (their nickname, the name on the colonist bar, first, last or full name): used for them ahead of the matcher and never for anyone else.</summary>
        public string pawn;
        /// <summary>The xenotypes it shows (def names); none: a baseliner, or any xenotype of a race listed.</summary>
        public List<string> xenotypes = new List<string>();
        /// <summary>The races it shows (def names); none: a human.</summary>
        public List<string> races = new List<string>();
        /// <summary>male, female or any.</summary>
        public string sex;
        /// <summary>baby, toddler, child, young, adult or old.</summary>
        public string age;
        /// <summary>The skin's colour (or the fur's or scales'), as #rrggbb or (r, g, b).</summary>
        public string skinColor;
        /// <summary>The hair's colour.</summary>
        public string hairColor;
        /// <summary>bald, short, medium, long, braids, bun or tied (the framework's buzzed, mohawk, topknot, dreadlocks and curly read as these).</summary>
        public string hair;
        /// <summary>none, stubble, short, full or moustache.</summary>
        public string beard;
        /// <summary>slim, average, heavy or broad (muscular reads as broad).</summary>
        public string build;
        /// <summary>The clothes' age: tribal, medieval, industrial or spacer.</summary>
        public string era;
        /// <summary>The clothes' kind: rags, work, fine, robes or armour.</summary>
        public string dress;
        /// <summary>A hat, hood or helmet is on (shown only to those wearing one, with headgear shown in portraits).</summary>
        public string headwear;
        /// <summary>pointedEars, longEars, horns, tusks, fur, scales, gills, glowingEyes, markings, tail, snout.</summary>
        public List<string> features = new List<string>();
        /// <summary>lostEye, eyePatch, scar, burn, bionicEye, missingEar.</summary>
        public List<string> marks = new List<string>();
        /// <summary>The looks it shows, by their words (<see cref="PortraitLookDef.word"/>: drinker, pyromaniac, conjoinedTwins...); a special look's picture stands only for those who have it.</summary>
        public List<string> looks = new List<string>();
        /// <summary>The work it shows (scholar, doctor, soldier, warrior, hunter, scout, builder, miner, crafter, smith, farmer, herder, cook, artist, trader, leader, priest, labourer); empty for any.</summary>
        public string role;
        /// <summary>front, left, right, threeQuarterLeft or threeQuarterRight: where the person looks in the picture.</summary>
        public string facing;
        /// <summary>The face this picture is a variant of (the same word on each variant); none: a face of its own.</summary>
        public string character;
        /// <summary>base, or the variant's own word (lostEye, scarred, burned, older...).</summary>
        public string variant;
        /// <summary>Where the head is (its middle across and down, its height with the hair, as shares of the picture), where it isn't measured from the cut-out's shape as it should be.</summary>
        public float headX = float.NaN, headY = float.NaN, headSize = float.NaN;

        [Unsaved] private PortraitRecord record;
        [Unsaved] private List<string> problems;
        [Unsaved] private PictureFile file;
        [Unsaved] private bool looked;

        /// <summary>What the picture shows, in the matcher's words.</summary>
        public PortraitRecord Record
        {
            get
            {
                if (record == null) Read();
                return record;
            }
        }

        /// <summary>
        /// Where the head is and the way the person faces: the def's own numbers where it gives them, else as measured from
        /// the cut-out when it loaded, else where the library's older square pictures had it.
        /// </summary>
        public PictureLayout Layout => PortraitFiles.LayoutOf(Record, File, headX, headY, headSize);

        /// <summary>The picture's file (looked up once; null where neither the PNG nor a texture is there).</summary>
        public PictureFile File
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    file = PortraitFiles.For(this, texPath, true);
                }
                return file;
            }
        }

        private void Read()
        {
            // The looks' words are known before any record is read.
            PortraitLooks.Ensure();
            problems = new List<string>();
            record = PortraitLibraryMath.Record(defName, Field, List, problems);
        }

        private string Field(string name)
        {
            switch (name)
            {
                case "texPath": return texPath;
                case "pawn": return pawn;
                case "sex": return sex;
                case "age": return age;
                case "skinColor": return skinColor;
                case "hairColor": return hairColor;
                case "hair": return hair;
                case "beard": return beard;
                case "build": return build;
                case "era": return era;
                case "dress": return dress;
                case "headwear": return headwear;
                case "facing": return facing;
                case "character": return character;
                case "variant": return variant;
                case "role": return role;
                default: return null;
            }
        }

        private IList<string> List(string name) =>
            name == "xenotypes" ? xenotypes : name == "races" ? races : name == "features" ? features : name == "marks" ? marks : name == "looks" ? looks : null;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            Read();
            foreach (var p in problems) yield return p;
        }
    }

    /// <summary>
    /// Where a library picture's file is: <c>Portraits/&lt;texPath&gt;.png</c> in the folder of the mod its def came from
    /// (each mod's folder listed once), else a texture the game loaded under Textures (the older defs). Nothing is read here.
    /// </summary>
    public static class PortraitFiles
    {
        private static readonly Dictionary<string, HashSet<string>> listed = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The PNGs under a mod's Portraits folder, by their path from it (forward slashes, small letters).</summary>
        private static HashSet<string> Listed(string root)
        {
            if (listed.TryGetValue(root, out var set)) return set;
            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string dir = Path.Combine(root, "Portraits");
                if (Directory.Exists(dir))
                    foreach (var f in Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories))
                        set.Add(f.Substring(dir.Length + 1).Replace('\\', '/'));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: listing the pictures of " + root, e);
            }
            listed[root] = set;
            return set;
        }

        /// <summary>
        /// Where the head is and the way the person faces in a library picture: the record's own numbers where it gives all
        /// three, else as measured from the cut-out when it loaded, else where the library's older square pictures had it.
        /// </summary>
        public static PictureLayout LayoutOf(PortraitRecord r, PictureFile file, float headX, float headY, float headSize)
        {
            var turn = PortraitViewMath.FacesOf(r.facing);
            float shape = file != null && file.height > 0 ? file.Shape : 1f;
            if (!float.IsNaN(headX) && !float.IsNaN(headY) && !float.IsNaN(headSize)) return new PictureLayout(headX, headY, headSize, turn, shape);
            if (file != null && file.head.found) return file.head.Layout(turn, shape);
            return new PictureLayout(PortraitViewMath.LibraryHeadX, PortraitViewMath.LibraryHeadY, PortraitViewMath.LibraryHeadSize, turn, shape);
        }

        /// <summary>The file for a def's picture, or null (told once in the log) where there is none.</summary>
        public static PictureFile For(Def def, string texPath, bool cutOut)
        {
            if (def == null || texPath.NullOrEmpty()) return null;
            string rel = texPath.Replace('\\', '/').TrimStart('/') + ".png";
            string root = def.modContentPack?.RootDir;
            if (!root.NullOrEmpty() && Listed(root).Contains(rel))
                return new PictureFile { key = def.defName, file = Path.Combine(Path.Combine(root, "Portraits"), rel), texPath = texPath, cutOut = cutOut };
            if (ContentFinder<UnityEngine.Texture2D>.Get(texPath, false) != null) return new PictureFile { key = def.defName, file = null, texPath = texPath, cutOut = cutOut };
            CouncilLog.WarnOnce("Portraits: the picture " + def.defName + " has no file at Portraits/" + rel + (root.NullOrEmpty() ? "" : " in " + root) + ", so it is left out.");
            return null;
        }
    }
}
