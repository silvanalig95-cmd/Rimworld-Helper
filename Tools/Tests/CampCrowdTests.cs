using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Camps side by side, bands against a camp, old camps' sites (Camps/CampCrowdMath, CampRaidMath, CampGroundMath; the
/// player's requests of 8 November 2026): the setting's levels (chance, rest, how many at once), the map's size and open
/// ground capping the count (a small map one or two, the player's 275 by 275 the level's full count), how far apart camps
/// keep, peoples at war never side by side, how many camps a band comes against and never on their first day, who may
/// come against whom, how strong, whether the campers stand, how it ends and the thanks; moving into an old site, its
/// history, contested spots and two parties for one site. Then the mod's side: the saved shape and the old save's.
/// </summary>
public static class CampCrowdTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "camp crowds: " + what);
        }

        var levels = new[] { CampCrowd.Rare, CampCrowd.Usual, CampCrowd.Many, CampCrowd.Crowded };

        // ---------------- The levels ----------------

        C(Math.Abs(CampCrowdMath.DailyChance(CampCrowd.Usual) - 0.05f) < 1e-6 && CampCrowdMath.GapDays(CampCrowd.Usual) == 15f && CampCrowdMath.MostAtOnce(CampCrowd.Usual) == 1,
            "Usual is how camps always came: one chance in twenty a day, a fortnight's rest, one at a time");
        for (int i = 1; i < levels.Length; i++)
        {
            C(CampCrowdMath.DailyChance(levels[i]) > CampCrowdMath.DailyChance(levels[i - 1]), $"{levels[i]} comes more often than {levels[i - 1]}");
            C(CampCrowdMath.GapDays(levels[i]) < CampCrowdMath.GapDays(levels[i - 1]), $"{levels[i]} rests less after a camp than {levels[i - 1]}");
            C(CampCrowdMath.MostAtOnce(levels[i]) >= CampCrowdMath.MostAtOnce(levels[i - 1]), $"{levels[i]} takes at least as many at once as {levels[i - 1]}");
        }
        C(CampCrowdMath.MostAtOnce(CampCrowd.Rare) == 1 && CampCrowdMath.MostAtOnce(CampCrowd.Many) == 2 && CampCrowdMath.MostAtOnce(CampCrowd.Crowded) == 4,
            "one, one, two, four at most");
        foreach (var l in levels)
            C(CampCrowdMath.DailyChance(l) > 0f && CampCrowdMath.DailyChance(l) <= 0.35f && CampCrowdMath.GapDays(l) >= 1f, $"{l}: a chance short of every day, a day's rest at least");

        // ---------------- The map's room ----------------

        int Cells(int side) => side * side;
        C(CampCrowdMath.SizeCap(Cells(200)) == 1 && CampCrowdMath.SizeCap(Cells(225)) == 2 && CampCrowdMath.SizeCap(Cells(250)) == 3 && CampCrowdMath.SizeCap(Cells(275)) == 4
          && CampCrowdMath.SizeCap(Cells(325)) == 4, "the size caps the count: 200 one, 225 two, 250 three, 275 and up four");
        foreach (var l in levels)
        {
            C(CampCrowdMath.Cap(l, Cells(200), 0.9f) == 1, $"{l}: a small map takes one camp, whatever the level");
            C(CampCrowdMath.Cap(l, Cells(225), 0.9f) <= 2, $"{l}: a medium map never more than two");
            C(CampCrowdMath.Cap(l, Cells(275), 0.8f) == CampCrowdMath.MostAtOnce(l), $"{l}: the player's 275 by 275 takes the level's full count ({CampCrowdMath.MostAtOnce(l)})");
            C(CampCrowdMath.Cap(l, Cells(275), 0.05f) == 1, $"{l}: a map with little open ground takes one");
            for (int side = 150; side <= 400; side += 25)
                for (float open = 0f; open <= 1f; open += 0.1f)
                    C(CampCrowdMath.Cap(l, Cells(side), open) >= 1 && CampCrowdMath.Cap(l, Cells(side), open) <= CampCrowdMath.MostAtOnce(l), $"{l} {side} {open:0.0}: one to the level's count");
        }
        C(CampCrowdMath.Cap(CampCrowd.Crowded, Cells(275), 0.2f) == 3, "a mostly mountain, marsh or water map takes one fewer");
        C(CampCrowdMath.Cap(CampCrowd.Crowded, Cells(250), 0.8f) == 3 && CampCrowdMath.Cap(CampCrowd.Many, Cells(250), 0.8f) == 2, "a large map: three crowded, two many");
        C(CampCrowdMath.Full(0, 1) == null && CampCrowdMath.Full(1, 1) == "here" && CampCrowdMath.Full(1, 2) == null && CampCrowdMath.Full(2, 2) == "full"
          && CampCrowdMath.Full(4, 4) == "full" && CampCrowdMath.Full(3, 4) == null && CampCrowdMath.Full(2, 1) == "here", "room for another until the count is reached; one alone reads as before");

        // ---------------- Keeping apart, and peoples at war ----------------

        C(CampCrowdMath.Apart == 2 * CampBuildMath.Reach + 12 && CampCrowdMath.Apart > 2 * CampBuildMath.Reach, "camps keep their own sites: more than both reaches between fires");
        C(CampCrowdMath.FarEnough(0, 0, CampCrowdMath.Apart, 0) && !CampCrowdMath.FarEnough(0, 0, CampCrowdMath.Apart - 1, 0) && !CampCrowdMath.FarEnough(10, 10, 10, 10),
            "far enough at the distance, not a cell nearer, never stacked");
        C(CampCrowdMath.FarEnough(0, 0, 23, 23) && !CampCrowdMath.FarEnough(0, 0, 22, 22), "apart measured as the crow flies");
        C(!CampCrowdMath.MayShare(true, false) && CampCrowdMath.MayShare(true, true) && CampCrowdMath.MayShare(false, false), "peoples at war never camp side by side, unless one came to attack");

        // ---------------- How often a band comes ----------------

        float usual = CampRaidMath.Share(CampCrowd.Usual);
        C(usual >= 1f / 10f && usual <= 1f / 8f, $"about one camp in eight to ten at Usual ({1f / usual:0.0})");
        C(CampRaidMath.Share(CampCrowd.Rare) < usual && CampRaidMath.Share(CampCrowd.Many) > usual && CampRaidMath.Share(CampCrowd.Crowded) > CampRaidMath.Share(CampCrowd.Many)
          && CampRaidMath.Share(CampCrowd.Crowded) <= 0.2f, "fewer at Rare, a bit more at Many and Crowded, kept modest");
        foreach (var l in levels)
        {
            C(CampRaidMath.FateChance(l, 1f, false) == 0f && CampRaidMath.FateChance(l, 1.49f, true) == 0f, $"{l}: a stay of a day or so sees no band");
            C(CampRaidMath.FateChance(l, 5f, true) > CampRaidMath.FateChance(l, 5f, false) && CampRaidMath.FateChance(l, 5f, true) <= CampRaidMath.MostShare, $"{l}: a contested site likelier, never past the most");
        }
        for (double r = 0; r <= 1.0; r += 0.125)
        {
            int at = CampRaidMath.AttackTick(100000, 100000 + 4 * CampRaidMath.DayTicks, r);
            C(at >= 100000 + CampRaidMath.DayTicks && at <= 100000 + 4 * CampRaidMath.DayTicks - 6 * CampRaidMath.HourTicks, $"never on the first day, never in the last hours ({r:0.000})");
        }
        C(CampRaidMath.AttackTick(1000, 1000 + CampRaidMath.DayTicks, 0.5) == -1 && CampRaidMath.AttackTick(-1, 500000, 0.5) == -1, "no time for it: none");
        C(CampRaidMath.AttackTick(0, 3 * CampRaidMath.DayTicks, 0.2) < CampRaidMath.AttackTick(0, 3 * CampRaidMath.DayTicks, 0.8), "the roll picks the moment");

        // A season's worth of camps at each level: the share attacked stays near the level's (deterministic dice).
        var rng = new Random(8112026);
        foreach (var l in levels)
        {
            int many = 20000, hit = 0;
            for (int i = 0; i < many; i++)
            {
                float stay = 1f + (float)rng.NextDouble() * 6f;
                if (rng.NextDouble() < CampRaidMath.FateChance(l, stay, rng.NextDouble() < 0.15)) hit++;
            }
            float share = hit / (float)many;
            C(share > 0.03f && share < 0.2f, $"{l}: {share:0.000} of camps see a band, stays of one to seven days");
        }

        // ---------------- Who may come against whom ----------------

        var foe = new AttackerFacts { hostileToCamp = true, humanlike = true, hasFighters = true, fightsAllowed = true, techWeight = 1f };
        C(CampRaidMath.MayAttack(foe), "a people of fighters at war with the camp's may come");
        var notFoe = foe;
        notFoe.hostileToCamp = false;
        var player = foe;
        player.isPlayer = true;
        var ally = foe;
        ally.allyOfColony = true;
        var beasts = foe;
        beasts.humanlike = false;
        var noFighters = foe;
        noFighters.hasFighters = false;
        var gone = foe;
        gone.gone = true;
        foreach (var (f, what) in new[] { (notFoe, "a people at peace with the camp's"), (player, "the colony itself"), (ally, "the colony's ally"), (beasts, "beasts or machines"), (noFighters, "a people without fighters"), (gone, "a people gone from the world") })
            C(!CampRaidMath.MayAttack(f), "never: " + what);
        var ourFoe = foe;
        ourFoe.hostileToColony = true;
        C(CampRaidMath.MayAttack(ourFoe), "a people at war with the colony too, while the storyteller sends fights");
        var quiet = ourFoe;
        quiet.fightsAllowed = false;
        var truce = ourFoe;
        truce.truce = true;
        C(!CampRaidMath.MayAttack(quiet) && !CampRaidMath.MayAttack(truce), "the colony's enemies not while the storyteller sends no fights, nor under a truce");
        var quietNeutral = foe;
        quietNeutral.fightsAllowed = false;
        C(CampRaidMath.MayAttack(quietNeutral) == CampRaidMath.MayAttack(foe), "one at peace with the colony is no fight of the colony's");
        var claim = foe;
        claim.heldSiteBefore = true;
        C(CampRaidMath.Weight(claim) == 3f * CampRaidMath.Weight(foe) && CampRaidMath.Weight(ourFoe) < CampRaidMath.Weight(foe), "one that held the site likeliest; the colony's enemies a little less");

        // ---------------- How strong, standing or going ----------------

        for (double r = 0; r <= 1.0; r += 0.25)
        {
            float p = CampRaidMath.Points(200f, 2000f, false, r);
            C(p >= 150f && p <= 270f, $"about the camp's own strength ({p:0})");
            C(CampRaidMath.Points(200f, 100f, true, r) <= 80f + 0.01f, "at war with the colony too: never past six tenths of its threat points (or the least)");
            C(CampRaidMath.Points(5000f, 100000f, false, r) <= CampRaidMath.MostPoints && CampRaidMath.Points(0f, 0f, false, r) >= CampRaidMath.LeastPoints, "within the least and the most");
        }
        C(CampRaidMath.Points(200f, 2000f, false, 0.0) < CampRaidMath.Points(200f, 2000f, false, 1.0), "the roll makes them a little weaker or stronger");
        C(CampRaidMath.Stand(0f, 500f, false, true), "a camp of fighters always stands");
        C(CampRaidMath.Stand(300f, 300f, false, false) && !CampRaidMath.Stand(100f, 300f, false, false), "strong enough, they stand; too weak, they go");
        C(CampRaidMath.Stand(150f, 300f, true, false) && !CampRaidMath.Stand(150f, 300f, false, false), "help coming, they stand where alone they would go");

        // ---------------- How it ends ----------------

        C(CampRaidMath.Outcome(4, 4, false, 3, 3, false, 2f) == CampRaidEnd.Ongoing, "still going");
        C(CampRaidMath.Outcome(0, 4, false, 2, 3, false, 2f) == CampRaidEnd.CampersWon && CampRaidMath.Outcome(2, 4, true, 2, 3, false, 2f) == CampRaidEnd.CampersWon, "the band down or fled: the campers won");
        C(CampRaidMath.Outcome(3, 4, false, 0, 3, false, 2f) == CampRaidEnd.CampersLost && CampRaidMath.Outcome(3, 4, false, 2, 3, true, 2f) == CampRaidEnd.CampersLost, "the campers down or fled: lost");
        C(CampRaidMath.Outcome(3, 4, false, 2, 3, true, 0f) == CampRaidEnd.CampersLost, "gone in time is the band's empty camp");
        C(CampRaidMath.Outcome(3, 4, false, 2, 3, false, CampRaidMath.GiveUpHours) == CampRaidEnd.Held, "hours of it: the band gives up");
        C(CampRaidMath.Outcome(0, 4, false, 0, 3, true, 1f) == CampRaidEnd.CampersLost, "the campers fled first counts before the band's losses");
        C(CampRaidMath.CampersBreak(1, 4, 3) && !CampRaidMath.CampersBreak(3, 4, 3) && !CampRaidMath.CampersBreak(2, 4, 1), "half down and outnumbered, the campers run");
        C(CampRaidMath.BandBreaks(2, 4) && !CampRaidMath.BandBreaks(3, 4), "half the band down, it runs");
        C(CampRaidMath.Goodwill(true, true, CampRaidEnd.CampersWon) > CampRaidMath.Goodwill(true, true, CampRaidEnd.CampersLost) && CampRaidMath.Goodwill(true, true, CampRaidEnd.CampersLost) > CampRaidMath.Goodwill(false, true, CampRaidEnd.CampersWon)
          && CampRaidMath.Goodwill(false, true, CampRaidEnd.CampersWon) > 0 && CampRaidMath.Goodwill(false, false, CampRaidEnd.CampersWon) == 0, "fought beside them most, warned them some, stayed out nothing");
        C(CampRaidMath.GiftWorth(true, 3, CampRaidEnd.CampersWon) > CampRaidMath.GiftWorth(true, 0, CampRaidEnd.CampersWon) && CampRaidMath.GiftWorth(true, 3, CampRaidEnd.CampersLost) == 0
          && CampRaidMath.GiftWorth(false, 3, CampRaidEnd.CampersWon) == 0 && CampRaidMath.GiftWorth(true, 50, CampRaidEnd.Held) <= 80 + 20 * 6, "a gift only for fighting beside them and holding the camp, more for each of the band down, kept modest");
        C(CampRaidMath.SendsWord(CampRaidAnswer.Help) && CampRaidMath.SendsWord(CampRaidAnswer.Warn) && !CampRaidMath.SendsWord(CampRaidAnswer.Out) && CampRaidMath.Default == CampRaidAnswer.Out,
            "help and warn send word; staying out is the default");

        // ---------------- Old camps' sites ----------------

        var site = new GroundFacts { pieces = 10, standing = 8, fireStands = true, shelterStands = true, beds = 4 };
        C(CampGroundMath.Worth(site, 2), "most of it standing, beds for them: worth moving into");
        var g1 = site;
        g1.claimed = true;
        var g2 = site;
        g2.colonyNear = true;
        var g3 = site;
        g3.campNear = true;
        var g4 = site;
        g4.standing = 3;
        var g5 = site;
        g5.fireStands = false;
        g5.shelterStands = false;
        var g6 = site;
        g6.beds = 1;
        var g7 = site;
        g7.standing = 0;
        foreach (var (g, what) in new[] { (g1, "the colony's (claimed)"), (g2, "the colony's land up to it"), (g3, "another camp near or holding it"), (g4, "too little standing"), (g5, "no fire nor shelter"), (g6, "too few beds' places"), (g7, "nothing standing") })
            C(!CampGroundMath.Worth(g, 2), "not worth it: " + what);
        var fireOnly = site;
        fireOnly.shelterStands = false;
        C(CampGroundMath.Worth(fireOnly, 2), "the fire pit alone with enough standing will do");
        C(CampGroundMath.StillASite(1, false, false) && !CampGroundMath.StillASite(0, false, false) && !CampGroundMath.StillASite(5, true, false) && !CampGroundMath.StillASite(5, false, true),
            "a site while something stands, unclaimed, clear of the colony");
        C(CampGroundMath.People(8, 4, 2) == 4 && CampGroundMath.People(2, 6, 1) == 2 && CampGroundMath.People(3, 1, 2) == 2, "as many as its beds take, never fewer than the kind's fewest");
        for (int i = 1; i < levels.Length; i++) C(CampGroundMath.ReuseChance(levels[i]) > CampGroundMath.ReuseChance(levels[i - 1]), $"{levels[i]}: the old sites taken more readily");
        var one = new List<HoldFacts> { new HoldFacts { people = 1, how = "left" }, new HoldFacts { people = 1, how = "left" } };
        var two = new List<HoldFacts> { new HoldFacts { people = 1, how = "left" }, new HoldFacts { people = 2, how = "left" } };
        var driven = new List<HoldFacts> { new HoldFacts { people = 1, how = "driven" } };
        C(!CampGroundMath.Contested(one) && CampGroundMath.Contested(two) && CampGroundMath.Contested(driven) && !CampGroundMath.Contested(new List<HoldFacts>()) && !CampGroundMath.Contested(null),
            "contested: two peoples held it, or someone was driven off it");
        C(CampGroundMath.HasClaim(two, 1, 2) && !CampGroundMath.HasClaim(two, 3, 2) && !CampGroundMath.HasClaim(two, 2, 2), "a claim: held it before, and isn't holding it now");
        C(CampGroundMath.TogetherChance(CampCrowd.Rare, true) == 0f && CampGroundMath.TogetherChance(CampCrowd.Usual, false) > 0f && CampGroundMath.TogetherChance(CampCrowd.Usual, false) < 0.1f
          && CampGroundMath.TogetherChance(CampCrowd.Crowded, false) > CampGroundMath.TogetherChance(CampCrowd.Many, false)
          && Math.Abs(CampGroundMath.TogetherChance(CampCrowd.Many, true) - 1.5f * CampGroundMath.TogetherChance(CampCrowd.Many, false)) < 1e-5, "two for one site: never at Rare, seldom, more on a crowded land and a contested site");
        C(CampGroundMath.Together(true, false) == TogetherEnd.Blows && CampGroundMath.Together(true, true) == TogetherEnd.Blows && CampGroundMath.Together(false, true) == TogetherEnd.GivesWay
          && CampGroundMath.Together(false, false) == TogetherEnd.None, "at war: blows; else the second gives way where there is room, or nothing comes of it");
        C(CampGroundMath.HowLeft("raided") == "driven" && CampGroundMath.HowLeft("forced") == "sent" && CampGroundMath.HowLeft("time") == "left" && CampGroundMath.HowLeft(null) == "left", "how each going reads in a site's history");
        C(CampGroundMath.Drop(3) == 0 && CampGroundMath.Drop(CampGroundMath.MostHolders + 2) == 2, "a site's history keeps the newest holders");

        // ---------------- Packing in time ----------------

        C(CampPackMath.Orderly("warned") && CampPackMath.PackHours("warned") == 1 && !CampPackMath.WaitsForMorning("warned") && CampPackMath.WaitsForMorning("time"), "warned of a band, they pack an hour and go, by night too");
        C(!CampPackMath.Orderly("raided") && CampPackMath.PackHours("raided") == 0, "driven off by a band, they go with what they carry");

        // ---------------- The mod's side ----------------

        string camps = Path.Combine(root, "Source", "ColonyCouncil", "Camps");
        string Code(string f) => Regex.Replace(File.ReadAllText(Path.Combine(camps, f)), @"//[^\n]*", "");
        string tracker = Code("CampTracker.cs");
        C(tracker.Contains("Scribe_Collections.Look(ref camps, \"camps\", LookMode.Deep)") && tracker.Contains("Scribe_Deep.Look(ref old, \"camp\")") && tracker.Contains("LoadSaveMode.LoadingVars"),
            "the camps are saved as a list, and an older save's one camp is read into it");
        C(tracker.Contains("Scribe_Collections.Look(ref raids, \"campRaids\", LookMode.Deep)") && tracker.Contains("Scribe_Collections.Look(ref grounds, \"grounds\", LookMode.Deep)"), "bands and old sites saved with their home");
        C(tracker.Contains("CampCrowdMath.Full(here, CampCrowds.CapAt(map))") && tracker.Contains("CampCrowdMath.GapDays(CampCrowds.Level)"), "the blocker reads the level's count and rest");
        C(tracker.Contains("CampCrowds.AtPeaceHere(map, x)") && tracker.Contains("c.plans.Where(p => !p.reuse)"), "no people at war with a camp here; nothing carried for what stands");
        C(Code("CampSite.cs").Contains("CampCrowds.Apart(map, c, fires)"), "the site finder keeps every camp apart");
        string lord = Code("CampLord.cs");
        int hunt = lord.IndexOf("graph.AddToil(hunt);", StringComparison.Ordinal), guard = lord.IndexOf("graph.AddToil(guard);", StringComparison.Ordinal);
        C(hunt > 0 && guard > hunt && Regex.Matches(lord.Substring(guard + 1), @"graph\.AddToil\(").Count == 0, "the guard toil is added last, so a saved group keeps its toils' places");
        string raid = Code("CampRaid.cs");
        C(raid.Contains("Edge(map, c.FireCell, out var entry)") && raid.Contains("PawnGroupMakerUtility.GeneratePawns"), "the band comes in at the camp's edge, its fighters the game's own of their people");
        C(raid.Contains("Notify_IncidentFired") && raid.Contains("if (hostile)"), "a band at war with the colony too is told to the storyteller");
        C(raid.Contains("p.Drafted") && !raid.Contains("drafter.Drafted = true"), "colonists fight only when the player drafts them");
        string life = Code("CampLife.cs");
        C(life.Contains("CampGrounds.TakeOver(this);") && life.Contains("CampGrounds.Left(this);"), "an old site taken over on arriving, recorded on going");
        return n;
    }
}
