using System.Collections.Generic;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One painted backdrop for the large portrait (docs: DESIGN.md, "Portraits, phase 4"; <c>docs/portrait-art/FRAMEWORK.md</c>,
    /// "Backgrounds"): an empty painted scene (a room of one of the four ages, a look outdoors, a plain ground) in the Defs of
    /// any mod (the player's pack "Rimfolk Portraits" writes one per picture). Unknown words are def errors at start-up.
    /// <code>
    /// &lt;ColonyCouncil.BackdropDef&gt;
    ///   &lt;defName&gt;Backdrop_room_bedroom_medieval_01&lt;/defName&gt;
    ///   &lt;texPath&gt;backdrops/room_bedroom_medieval_style-backdrop_01_00001_&lt;/texPath&gt;
    ///   &lt;place&gt;room&lt;/place&gt;
    ///   &lt;rooms&gt;&lt;li&gt;Bedroom&lt;/li&gt;&lt;li&gt;GuestRoom&lt;/li&gt;&lt;/rooms&gt;
    ///   &lt;era&gt;medieval&lt;/era&gt;
    ///   &lt;contents&gt;&lt;li&gt;bed&lt;/li&gt;&lt;li&gt;hearth&lt;/li&gt;&lt;li&gt;window&lt;/li&gt;&lt;/contents&gt;
    /// &lt;/ColonyCouncil.BackdropDef&gt;
    /// </code>
    /// </summary>
    public class BackdropDef : Def
    {
        /// <summary>The picture under its mod's <c>Portraits</c> folder, without ".png" (or a texture under Textures).</summary>
        public string texPath;
        /// <summary>
        /// Its depth map (phase 6), found the same way: a grey picture of the same size beside it, white near and black far;
        /// none given, the picture's own name with "_depth" where that file is there.
        /// </summary>
        public string depthPath;
        /// <summary>room, outdoors or plain.</summary>
        public string place;
        /// <summary>The room roles it fits (RoomRoleDef names: Bedroom, Barracks, Hospital, PrisonCell, ...).</summary>
        public List<string> rooms = new List<string>();
        /// <summary>For a look outdoors, its group of land: temperateForest, borealForest, tundra, ice, grassland, shrubland, desert, jungle, swamp, coast, wasteland, volcanic, glowforest, cave, space or mountain.</summary>
        public string outdoors;
        /// <summary>tribal, medieval, industrial, spacer or any.</summary>
        public string era;
        /// <summary>What is in it: bed, beds, table, chairs, hearth, fire, window, shelves, crates, altar, throne, banners, pillars, workbench, tools, stove, pots, books, medicine, bars, games.</summary>
        public List<string> contents = new List<string>();
        /// <summary>
        /// A god's place (docs/gods-altars-proposals.md, C1): the domain of the god it is for (harvest, storm, hunt, healing,
        /// war, craft, way, dead, night, hearth, wild, waters, machine, void), shown behind someone praying at a god's place of
        /// that domain or standing in its temple, and never anywhere else; none for an ordinary backdrop.
        /// </summary>
        public string god;

        [Unsaved] private BackdropRecord record;
        [Unsaved] private List<string> problems;
        [Unsaved] private PictureFile file;
        [Unsaved] private bool looked;
        [Unsaved] private PictureFile depth;
        [Unsaved] private bool lookedDepth;

        public BackdropRecord Record
        {
            get
            {
                if (record == null) Read();
                return record;
            }
        }

        /// <summary>The picture's file (looked up once; null where it isn't there).</summary>
        public PictureFile File
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    file = PortraitFiles.For(this, texPath, false);
                }
                return file;
            }
        }

        /// <summary>The depth map's file (looked up once; null where there is none): <see cref="depthPath"/>, else the picture's own name with "_depth" where that file is there.</summary>
        public PictureFile DepthFile
        {
            get
            {
                if (!lookedDepth)
                {
                    lookedDepth = true;
                    if (!depthPath.NullOrEmpty()) depth = PortraitFiles.For(this, depthPath, false);
                    else if (!texPath.NullOrEmpty()) depth = PortraitFiles.For(this, texPath + "_depth", false, true);
                }
                return depth;
            }
        }

        private void Read()
        {
            problems = new List<string>();
            record = BackdropMath.Record(defName, Field, List, problems);
        }

        private string Field(string name)
        {
            switch (name)
            {
                case "texPath": return texPath;
                case "place": return place;
                case "outdoors": return outdoors;
                case "god": return god;
                case "era": return era;
                default: return null;
            }
        }

        private IList<string> List(string name) => name == "rooms" ? rooms : name == "contents" ? contents : null;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            Read();
            foreach (var p in problems) yield return p;
        }
    }
}
