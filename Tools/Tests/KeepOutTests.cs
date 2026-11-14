using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Visitors keep out of the colony's bedrooms and its cold stores (Source/ColonyCouncil/Visitors/KeepOutMath.cs,
/// KeepOut.cs): what makes a room a cold store (its cold, with a margin, and the food kept in it, whatever its role), a
/// bedroom of the colony's people, who may go in (only those whose bed it is while one of the colony sleeps there; a
/// doctor's work too when nobody does; nobody into a cold store), the way out from inside; the player's save of 13
/// November 2026 as the rules see it; and the code's seams: the door hook, the look on the visitors' tick, Rimfolk's own
/// choices of where visitors stay and sleep, and food carried out to them.
/// </summary>
public static class KeepOutTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "visitors kept out: " + what);
        }

        // ---------- Cold ----------
        C(KeepOutMath.Cold(-6.25f, false) && KeepOutMath.Cold(4.9f, false), "a freezer, and a room just under the line, are cold");
        C(!KeepOutMath.Cold(5f, false) && !KeepOutMath.Cold(18f, false), "at the line or warmer, not cold");
        C(KeepOutMath.Cold(7f, true) && !KeepOutMath.Cold(7f, false), "a cold room warming a little (a cooler cycling, the door open) stays cold");
        C(!KeepOutMath.Cold(KeepOutMath.ColdLeaves, true) && KeepOutMath.ColdLeaves > KeepOutMath.ColdBelow, "warm enough, it leaves being cold");

        // ---------- Food kept there ----------
        C(KeepOutMath.HoldsFood(0, true), "an empty stockpile that takes food is a food store");
        C(KeepOutMath.HoldsFood(KeepOutMath.FoodStacks, false) && !KeepOutMath.HoldsFood(KeepOutMath.FoodStacks - 1, false), "enough food lying there, or not");
        C(!KeepOutMath.HoldsFood(1, false), "a meal on a cold table isn't a store");

        // ---------- What a room is ----------
        C(KeepOutMath.Kind(true, false, KeepOutMath.Cold(-6.25f, false), KeepOutMath.HoldsFood(12, true)) == KeptKind.ColdStore,
            "a freezer with food in it is a cold store, whatever role the game gives it (a butcher's table makes it a kitchen)");
        C(KeepOutMath.Kind(true, false, true, false) == KeptKind.None, "cold with no food kept in it: not a store");
        C(KeepOutMath.Kind(true, false, false, true) == KeptKind.None, "food in a warm room: not a cold store");
        C(KeepOutMath.Kind(true, true, false, false) == KeptKind.Bedroom && KeepOutMath.Kind(true, true, true, true) == KeptKind.Bedroom,
            "a bed of the colony's people makes it a bedroom, cold or not");
        C(KeepOutMath.Kind(false, true, true, true) == KeptKind.None, "out of doors, nothing is kept");

        // ---------- Who may go in ----------
        C(KeepOutMath.Barred(KeptKind.ColdStore, false, false, false, false) && KeepOutMath.Barred(KeptKind.ColdStore, false, false, true, true),
            "nobody goes into a cold store, a bed or a doctor's work there notwithstanding");
        C(KeepOutMath.Barred(KeptKind.Bedroom, false, false, false, false), "a colonist's room is private at any hour");
        C(KeepOutMath.Barred(KeptKind.Bedroom, true, false, false, false), "a sleeping owner's room is barred");
        C(KeepOutMath.Barred(KeptKind.Bedroom, true, false, false, true), "a doctor's work waits while one of the colony sleeps there");
        C(!KeepOutMath.Barred(KeptKind.Bedroom, false, false, false, true), "a doctor's work takes them in while nobody sleeps there");
        C(!KeepOutMath.Barred(KeptKind.Bedroom, true, false, true, false) && !KeepOutMath.Barred(KeptKind.Bedroom, false, false, true, false),
            "a bed given to them there (a guest bed, a lover's place in their partner's bed): the room is theirs too, asleep or not");
        C(!KeepOutMath.Barred(KeptKind.None, true, false, false, false), "anywhere else, the doors are open");
        foreach (KeptKind k in Enum.GetValues(typeof(KeptKind)))
            C(!KeepOutMath.Barred(k, true, true, false, false), $"inside a {k} already, the way out stays open");
        C(KeepOutMath.WalksOut(KeptKind.ColdStore, false, false, false) && KeepOutMath.WalksOut(KeptKind.Bedroom, true, false, true)
          && !KeepOutMath.WalksOut(KeptKind.Bedroom, true, true, false) && !KeepOutMath.WalksOut(KeptKind.None, true, false, false),
            "someone in a room they couldn't go into now walks out; whose bed it is, stays");

        // ---------- The player's save (League of Toba, 13 November 2026) ----------
        // The freezer: -6.25 degrees, raw food and hay in baskets and a stockpile, a butcher's table (the game: a kitchen).
        C(KeepOutMath.Kind(true, false, KeepOutMath.Cold(-6.25f, false), KeepOutMath.HoldsFood(30, true)) == KeptKind.ColdStore, "the save's freezer is a cold store");
        // Bautista and Darunia's room (a double bed, both up and about) with a Hospitality guest standing in it.
        C(KeepOutMath.WalksOut(KeptKind.Bedroom, false, false, false), "the guest standing in Bautista and Darunia's room walks out");
        // Aspen asleep in hers: a guest neither goes in, nor a doctor's work.
        C(KeepOutMath.Barred(KeptKind.Bedroom, true, false, false, true), "nobody goes into Aspen's room while she sleeps");
        // The guests' own room: guest beds only, at 17 degrees.
        C(KeepOutMath.Kind(true, false, KeepOutMath.Cold(17.5f, false), false) == KeptKind.None, "the guests' own room stays theirs");

        // ---------- The code ----------
        string dir = Path.Combine(root, "Source", "ColonyCouncil");
        string Src(string rel) => File.ReadAllText(Path.Combine(dir, rel));
        var keep = Src("Visitors/KeepOut.cs");
        C(keep.Contains("[HarmonyPatch(typeof(ForbidUtility), nameof(ForbidUtility.IsForbiddenToPass))]") && keep.Contains("!RimfolkPatches.On"),
            "the door hook: a door forbidden to a visitor as a forbidden door to a colonist, with the deeper hooks");
        var barred = Regex.Match(keep, @"public static bool Barred\(Building_Door door, Pawn p\)\s*\{(.*?)\n        \}", RegexOptions.Singleline).Groups[1].Value;
        C(barred.Length > 0 && !barred.Contains("GetRoom") && !barred.Contains("Find.TickManager") && barred.Contains("snaps"),
            "the door check reads only the last look (the game finds paths on worker threads)");
        C(keep.Contains("Sched.Every(now, Sched.RareTicks, Sched.RareVisitors)") && keep.Contains("Pass.RareKeepOut"), "the look runs on the visitors' tick of the cycle, timed");
        C(keep.Contains("reachability?.ClearCache()") && keep.Contains("ClearCacheFor(p)"), "reach is worked out again when what the doors allow changes");
        C(keep.Contains("LordJob_AssistColony") && keep.Contains("IsPrisonerOfColony") && keep.Contains("HostileTo(Faction.OfPlayer)"),
            "allies at the fight, the colony's prisoners and enemies are left out");
        C(keep.Contains("GuestBeds.IsGuestBed(b)") && keep.Contains("b.Medical") && keep.Contains("b.ForPrisoners"), "guest beds, sickbeds and cells don't make a bedroom private");
        C(keep.Contains("ServiceKind.Dose") && keep.Contains("CallerKind.Vet") && keep.Contains("PeopleWorkOn.Kind") && keep.Contains("JobDefOf.TendPatient"),
            "a doctor's work: the doses, the animal doctor, a people's worker on a colonist, the game's own tending");
        C(Src("Peoples/Events/PeopleWorkOn.cs").Contains("KeepOut.Refresh(map);"), "a people's worker's way in counts from the start");
        foreach (var (file, what) in new[] { ("Happenings/VisitorServices.cs", "a guest's corner for the night"), ("Visitors/Visitors.cs", "a corner indoors"),
                     ("Visitors/VisitorBeds.cs", "Hospitality's guest beds for a visit"), ("Missions/Hosting.cs", "a spare bed for a hosted guest"),
                     ("Integrations/GuestHelp.cs", "a helping guest's work") })
            C(Src(file).Contains("KeepOut.IsKept("), $"{what} is never in a kept room");
        C(Src("Integrations/MarketGuests.cs").Contains("KeepOut.KeptFrom(p, dest.Cell)"), "the market opens only a stall's place to Hospitality's guests, never a kept room");

        // ---------- Food brought out ----------
        C(keep.Contains("class JobDriver_BringOutFood") && keep.Contains("TryTransferToContainer") && keep.Contains("JobDefOf.Ingest"),
            "the colonist carries it out, hands it over, and they eat it");
        C(Src("Missions/Hosting.cs").Contains("public static bool Brought(") && Src("Diplomacy/Envoys.cs").Contains("Hosting.Brought(")
          && Src("Diplomacy/Feasts.cs").Contains("Hosting.Brought(") && Src("Peoples/Events/HerdersBeast.cs").Contains("Hosting.Brought("),
            "hosted guests, envoys, a feast's guests and the herders' rider are brought what they may not go in for");
        C(Src("PlayOut/PlayGuests.cs").Contains("KeepOut.Fetch(") && Src("PlayOut/PlayOutLord.cs").Contains("KeepOut.Fetch("), "the feasts' guests too");
        var outs = Src("Outsiders/OutsiderOutcomes.cs");
        C(outs.Contains("KeepOut.KeptFrom(x, t.Position)") && outs.Contains("KeepOut.BringJob(fx, x)") && outs.Contains("brought = true"),
            "a meal shared with an outsider: their portion brought out of the cold store first");
        var job = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_KeepOut.xml")).Root!.Elements("JobDef")
            .FirstOrDefault(d => d.Element("defName")?.Value == "Rimfolk_BringOutFood");
        C(job != null && job.Element("driverClass")?.Value == "ColonyCouncil.JobDriver_BringOutFood", "the bringing's job def");
        var keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_KeepOut.xml"));
        C(keyed.Contains("<CC_Clock_Pass_RareKeepOut>"), "the clock names the pass");
        return n;
    }
}
