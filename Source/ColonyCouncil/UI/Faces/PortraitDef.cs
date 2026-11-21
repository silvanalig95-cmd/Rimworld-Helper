using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One picture of the portrait library (docs: DESIGN.md, "Portraits, phase 3: a library of painted portraits";
    /// <c>docs/portrait-art/PLAN.md</c>), described from the picture itself, in <c>Defs/PortraitDefs/</c> of Rimfolk or of
    /// any mod. Every field is a word or a colour so a def is quick to write; unknown words are reported as def errors at
    /// start-up. For example:
    /// <code>
    /// &lt;ColonyCouncil.PortraitDef&gt;
    ///   &lt;defName&gt;Portrait_Baseliner_M_0042&lt;/defName&gt;
    ///   &lt;texPath&gt;Rimfolk/Portraits/Library/m_0042&lt;/texPath&gt;
    ///   &lt;sex&gt;male&lt;/sex&gt; &lt;age&gt;adult&lt;/age&gt;
    ///   &lt;skinColor&gt;#c8a07a&lt;/skinColor&gt; &lt;hairColor&gt;#4a3020&lt;/hairColor&gt;
    ///   &lt;hair&gt;short&lt;/hair&gt; &lt;beard&gt;full&lt;/beard&gt; &lt;build&gt;broad&lt;/build&gt;
    ///   &lt;era&gt;medieval&lt;/era&gt; &lt;dress&gt;work&lt;/dress&gt; &lt;facing&gt;threeQuarterLeft&lt;/facing&gt;
    ///   &lt;character&gt;m_0042&lt;/character&gt;
    /// &lt;/ColonyCouncil.PortraitDef&gt;
    /// </code>
    /// A variant of the same face (a lost eye, a scar, older) has the same <see cref="character"/>, its own
    /// <see cref="variant"/> and its <see cref="marks"/> or age.
    /// </summary>
    public class PortraitDef : Def
    {
        /// <summary>The picture's texture under Textures, without ".png": a head-and-shoulders picture with a clear background.</summary>
        public string texPath;
        /// <summary>The xenotypes it shows (def names); none: a baseliner, or any xenotype of a race listed.</summary>
        public List<string> xenotypes = new List<string>();
        /// <summary>The races it shows (def names); none: a human.</summary>
        public List<string> races = new List<string>();
        /// <summary>male, female or any.</summary>
        public string sex;
        /// <summary>child, young, adult or old.</summary>
        public string age;
        /// <summary>The skin's colour (or the fur's or scales'), as #rrggbb or (r, g, b).</summary>
        public string skinColor;
        /// <summary>The hair's colour.</summary>
        public string hairColor;
        /// <summary>bald, short, medium, long, braids, bun or tied.</summary>
        public string hair;
        /// <summary>none, stubble, short, full or moustache.</summary>
        public string beard;
        /// <summary>slim, average, heavy or broad.</summary>
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
        /// <summary>front, left, right, threeQuarterLeft or threeQuarterRight: where the person looks in the picture.</summary>
        public string facing;
        /// <summary>The face this picture is a variant of (the same word on each variant); none: a face of its own.</summary>
        public string character;
        /// <summary>base, or the variant's own word (lostEye, scarred, burned, older...).</summary>
        public string variant;
        /// <summary>Where the head is (its middle across and down, its height with the hair, as shares of the picture), where it isn't where the lead lines them up.</summary>
        public float headX = PortraitViewMath.LibraryHeadX, headY = PortraitViewMath.LibraryHeadY, headSize = PortraitViewMath.LibraryHeadSize;

        [Unsaved] private PortraitRecord record;
        [Unsaved] private List<string> problems;
        [Unsaved] private Texture2D texture;
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

        public PictureLayout Layout => new PictureLayout(headX, headY, headSize, PortraitViewMath.FacesOf(Record.facing));

        /// <summary>The picture, as the game loaded it (null if the texture is missing).</summary>
        public Texture2D Texture
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    texture = texPath.NullOrEmpty() ? null : ContentFinder<Texture2D>.Get(texPath, false);
                }
                return texture;
            }
        }

        private void Read()
        {
            problems = new List<string>();
            record = PortraitLibraryMath.Record(defName, Field, List, problems);
        }

        private string Field(string name)
        {
            switch (name)
            {
                case "texPath": return texPath;
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
                default: return null;
            }
        }

        private IList<string> List(string name) => name == "xenotypes" ? xenotypes : name == "races" ? races : name == "features" ? features : name == "marks" ? marks : null;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            Read();
            foreach (var p in problems) yield return p;
        }
    }
}
