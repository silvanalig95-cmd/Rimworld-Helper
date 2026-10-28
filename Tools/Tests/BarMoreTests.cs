using System;
using System.Collections.Generic;
using ColonyCouncil;

/// <summary>
/// The bottom bar's buttons put away under More (UI/BarMoreMath): which can be, putting away and back, a loaded list
/// set right, and More's place before the game's menu.
/// </summary>
public static class BarMoreTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "bottom bar: " + what);
        }

        C(!BarMoreMath.CanPutAway("Menu") && !BarMoreMath.CanPutAway("Inspect") && !BarMoreMath.CanPutAway(BarMoreMath.More),
            "the menu, the inspect pane and More itself always stay on the bar");
        C(!BarMoreMath.CanPutAway(null) && !BarMoreMath.CanPutAway(""), "a button with no name can't be put away");
        C(BarMoreMath.CanPutAway("Work") && BarMoreMath.CanPutAway("MintBillMenu") && BarMoreMath.CanPutAway("Council_MainButton"),
            "the game's, another mod's and Rimfolk's own buttons can be");

        var away = new List<string>();
        C(BarMoreMath.PutAway(away, "Factions") && away.Count == 1, "a button put away is listed");
        C(!BarMoreMath.PutAway(away, "Factions") && away.Count == 1, "put away twice, it's listed once");
        C(!BarMoreMath.PutAway(away, "Menu") && !away.Contains("Menu"), "the menu is never put away");
        C(BarMoreMath.PutAway(away, "History") && away[0] == "Factions" && away[1] == "History", "the list keeps the order they were put away in");
        C(BarMoreMath.PinBack(away, "Factions") && !away.Contains("Factions") && away.Count == 1, "pinned back, it's off the list");
        C(!BarMoreMath.PinBack(away, "Factions") && !BarMoreMath.PinBack(away, null), "pinning back one not put away changes nothing");
        C(!BarMoreMath.PutAway(null, "Work") && !BarMoreMath.PinBack(null, "Work"), "no list, nothing happens");

        var loaded = BarMoreMath.Clean(new List<string> { "Work", null, "", "Work", "Menu", BarMoreMath.More, "SomeRemovedModTab", "Inspect" });
        C(loaded.Count == 2 && loaded[0] == "Work" && loaded[1] == "SomeRemovedModTab",
            "a loaded list loses empty names, repeats and the buttons that always stay, and keeps a removed mod's button for when it's back");
        C(BarMoreMath.Clean(null) != null && BarMoreMath.Clean(null).Count == 0, "no list loaded is an empty list");

        C(BarMoreMath.OrderFor(100, 120) == 99, "More goes just before the game's menu");
        C(BarMoreMath.OrderFor(null, 120) == 121, "with no menu, at the end of the bar");
        return n;
    }
}
