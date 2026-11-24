using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// Pointed ears in the painted pictures (the player, 24 November 2026: the image model often paints pointed ears; "for all
/// the fantasy races, it's totally fine ... But for humans, of course, or anything that's really close to humans, they
/// should not have pointy ears ... unless the pawn for some reason has its xenotype on pointy ears"): the game's list of
/// human-looking kinds is the catalogue's (kinds.csv, humanLooking), a picture with pointed ears never stands for someone
/// who looks human and hasn't got them, and costs nothing for a fantasy race.
/// </summary>
public static class PortraitEarsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "pointed ears: " + what);
        }

        // ---------------- The catalogue and the game agree ----------------
        var lines = File.ReadAllLines(Path.Combine(root, "docs", "portrait-art", "catalogue", "kinds.csv"));
        var head = lines[0].Split(',').ToList();
        int kindAt = head.IndexOf("kind"), raceAt = head.IndexOf("race"), humanAt = head.IndexOf("humanLooking");
        C(humanAt > 0, "kinds.csv has the column humanLooking");
        var yes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            // The first columns are plain words (no quotes before humanLooking).
            var cells = line.Split(',');
            if (cells.Length <= humanAt) continue;
            all.Add(cells[kindAt]);
            if (cells[humanAt] == "yes") yes.Add(cells[kindAt]);
        }
        C(yes.SetEquals(PortraitLibraryMath.HumanLookingKinds), "the game's human-looking kinds are the catalogue's (" + yes.Count + ")");
        C(yes.Contains("Baseliner") && !yes.Contains("DevDesigner_XenotypeHighElves") && !yes.Contains("Impid") && !yes.Contains("VRE_Lowmate"),
            "a baseliner looks human; elves, impids and lowmates are fantasy races");

        // ---------------- The rule ----------------
        PortraitLook Person(string race, string xeno, bool baseliner, params string[] features)
        {
            var l = new PortraitLook { race = race, xenotype = xeno, baseliner = baseliner, sex = PortraitSex.Male, age = PortraitAge.Adult };
            foreach (var f in features) l.features.Add(f);
            return l;
        }
        PortraitRecord Picture(string xeno, params string[] features)
        {
            var r = new PortraitRecord { name = "P_" + xeno, sex = PortraitSex.Male, age = PortraitAge.Adult };
            if (xeno != "") r.xenotypes.Add(xeno);
            foreach (var f in features) r.features.Add(f);
            return r;
        }
        var human = Person("human", "baseliner", true);
        var elfish = Person("human", "baseliner", true, "pointedEars");
        var impid = Person("human", "impid", false, "horns");
        var custom = Person("human", "my folk", false);
        custom.ownXenotype = true;
        var customHorned = Person("human", "my horned folk", false, "horns");
        customHorned.ownXenotype = true;
        var hussar = Person("human", "hussar", false);
        C(PortraitLibraryMath.HumanLooking(human) && PortraitLibraryMath.HumanLooking(hussar) && !PortraitLibraryMath.HumanLooking(impid),
            "a baseliner and a hussar look human, an impid doesn't");
        C(PortraitLibraryMath.HumanLooking(custom) && !PortraitLibraryMath.HumanLooking(customHorned), "a xenotype of the player's own: human-looking unless its genes show something");
        C(PortraitLibraryMath.HumanLooking(Person("creepjoiner", "baseliner", true)) && !PortraitLibraryMath.HumanLooking(Person("dv_bogleg", "baseliner", true)),
            "a race: the creepjoiners look human, other races are fantasy races");

        var pointyHuman = Picture("", "pointedEars");
        var plainHuman = Picture("");
        C(!PortraitLibraryMath.EarsAgree(pointyHuman, human) && !PortraitLibraryMath.Shows(pointyHuman, human) && !PortraitLibraryMath.Rules(pointyHuman, human).All,
            "a human-looking person without pointed ears never gets a picture with them");
        C(float.IsPositiveInfinity(PortraitLibraryMath.Distances(human, new List<PortraitRecord> { pointyHuman })[0])
          && !float.IsPositiveInfinity(PortraitLibraryMath.Distances(human, new List<PortraitRecord> { plainHuman })[0]),
            "so it isn't among their candidates; a plain one is");
        C(PortraitLibraryMath.EarsAgree(pointyHuman, elfish) && PortraitLibraryMath.Rules(pointyHuman, elfish).All,
            "someone whose genes give them pointed ears may have such a picture");

        var pointyImpid = Picture("impid", "horns", "pointedEars");
        var plainImpid = Picture("impid", "horns");
        C(PortraitLibraryMath.EarsAgree(pointyImpid, impid) && PortraitLibraryMath.Rules(pointyImpid, impid).All, "a fantasy race takes a picture with pointed ears");
        C(Math.Abs(PortraitLibraryMath.Distance(impid, pointyImpid) - PortraitLibraryMath.Distance(impid, plainImpid)) < 1e-4f,
            "and pointed ears cost a fantasy race nothing: never a reason to pass a picture over");
        var hornless = Picture("impid", "pointedEars");
        C(PortraitLibraryMath.Distance(impid, hornless) > PortraitLibraryMath.Distance(impid, plainImpid), "other features still count as before");
        C(PortraitLibraryMath.Distance(elfish, plainHuman) > PortraitLibraryMath.Distance(elfish, pointyHuman), "someone with pointed ears is still better shown with them");
        return n;
    }
}
