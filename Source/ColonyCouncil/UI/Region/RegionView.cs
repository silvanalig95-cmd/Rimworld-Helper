using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Where the region window's views stand, kept for the session: the map's lens and whether it shows the map or the
    /// List, and whether Peoples shows the peoples or their faces. An address (PagesMath.TryAddress: "Map/Trade/List",
    /// "Peoples/Faces") sets them; the way back keeps them as a view's "within" (UI/Cards/Nav.cs). The words and icons of
    /// the lenses and modes are here too, made once.
    /// </summary>
    public static class RegionView
    {
        public const string Map = "Map", Peoples = "Peoples", HereAndAway = "HereAndAway", History = "WorldHistory";

        /// <summary>The map's lens.</summary>
        public static Lens lens = Lens.Land;

        /// <summary>The map's List rather than the map itself.</summary>
        public static bool list;

        /// <summary>Peoples shows their faces (the people the colony knows by name and those who left) rather than the peoples.</summary>
        public static bool faces;

        /// <summary>An address puts the view as it names it: a mode it doesn't name is the view's first, a lens it doesn't name stays.</summary>
        public static void Apply(PagesMath.Address a)
        {
            if (a.page == Map)
            {
                list = a.mode == "List";
                if (a.lens != null && RegionViewsMath.TryLens(a.lens, out var l)) lens = l;
            }
            else if (a.page == Peoples) faces = a.mode == "Faces";
        }

        /// <summary>A view's lens and mode as the way back keeps them ("Trade/List", "Faces"), or null.</summary>
        public static string Within(string page)
        {
            if (page == Map) return lens + (list ? "/List" : "/Map");
            if (page == Peoples) return faces ? "Faces" : null;
            return null;
        }

        /// <summary>A new game: the views as a first visit finds them.</summary>
        public static void Reset()
        {
            lens = Lens.Land;
            list = false;
            faces = false;
        }

        // ---------------- Words and icons ----------------

        private static string[] lensLabels, lensTips, mapModes, mapModeTips, peopleModes, peopleModeTips;
        private static Texture2D[] lensIcons, mapModeIcons, peopleModeIcons;

        public static string[] LensLabels => lensLabels ??= Array.ConvertAll(RegionViewsMath.Lenses, l => ("CC_Lens_" + l).Translate().ToString());

        public static string[] LensTips => lensTips ??= Array.ConvertAll(RegionViewsMath.Lenses, l => ("CC_Lens_" + l + "_Tip").Translate().ToString());

        public static Texture2D[] LensIcons => lensIcons ??= new[] { Icons.LensLand, Icons.LensStance, Icons.LensFortunes, Icons.LensThreats, Icons.LensTrade, Icons.LensOurs };

        public static string LensLabel(Lens l) => LensLabels[(int)l];

        public static string[] MapModes => mapModes ??= new[] { "CC_Mode_Map".Translate().ToString(), "CC_Mode_List".Translate().ToString() };

        public static string[] MapModeTips => mapModeTips ??= new[] { "CC_Mode_Map_Tip".Translate().ToString(), "CC_Mode_List_Tip".Translate().ToString() };

        public static Texture2D[] MapModeIcons => mapModeIcons ??= new[] { Icons.ModeMap, Icons.ModeList };

        public static string[] PeopleModes => peopleModes ??= new[] { "CC_Mode_Peoples".Translate().ToString(), "CC_Mode_Faces".Translate().ToString() };

        public static string[] PeopleModeTips => peopleModeTips ??= new[] { "CC_Mode_Peoples_Tip".Translate().ToString(), "CC_Mode_Faces_Tip".Translate().ToString() };

        public static Texture2D[] PeopleModeIcons => peopleModeIcons ??= new[] { Icons.Trust ?? Icons.People, Icons.Visitor };

        /// <summary>A view's name with the mode or lens an address names ("Map: Trade, as a list"), for the buttons and tooltips that lead there.</summary>
        public static string Name(PagesMath.Address a, string viewName)
        {
            if (a.page == Map && a.lens != null && RegionViewsMath.TryLens(a.lens, out var l))
                return (a.mode == "List" ? "CC_Region_AddrLensList" : "CC_Region_AddrLens").Translate(viewName.Named("VIEW"), LensLabel(l).Named("LENS")).Resolve();
            if (a.page == Peoples && a.mode == "Faces") return "CC_Region_AddrLens".Translate(viewName.Named("VIEW"), PeopleModes[1].Named("LENS")).Resolve();
            return viewName;
        }
    }
}
