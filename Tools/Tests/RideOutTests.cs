using ColonyCouncil;

/// <summary>
/// Riding out to far work and back with Giddy-Up 2 (the player, 15 November 2026: "pawns should really use mounts whenever
/// they walk away out of the base range ... avila did not mount up despite going really far to mine. it should mount to go
/// there, and mount back up aftr finishing work"; 16 November 2026: "it's not really working for all movement orders via
/// giddy up default settings"; Integrations/RideOutMath.cs, GiddyUpRideOut.cs): which trips count, the sums against walking,
/// getting on, the wait kept, the player's save, the setting and the seams.
/// </summary>
public static class RideOutTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void C(bool ok, string what)
        {
            cases++;
            check(ok, "riding out: " + what);
        }

        Trips(C);
        Sums(C);
        Wait(C);
        TheirSave(C);
        Seams(root, C);
        return cases;
    }

    private static void Trips(Action<bool, string> C)
    {
        C(RideOutMath.FarOut(RideOutMath.OutMin) && !RideOutMath.FarOut(RideOutMath.OutMin - 1) && !RideOutMath.FarOut(0), "out past the base from 10 cells past the home area");
        C(RideOutMath.Kind(RideOutMath.TripMin - 0.1f, 0, 50, 50, false) == RideTrip.None, "a trip under 40 cells is never ridden by Rimfolk");
        C(RideOutMath.Kind(float.NaN, 0, 50, 50, false) == RideTrip.None, "no trip read: nothing");
        C(RideOutMath.Kind(100f, 0, 0, 0, false) == RideTrip.None && RideOutMath.Kind(100f, 9, 9, 9, false) == RideTrip.None,
            "inside the base (all of it under 10 cells past the home area): Giddy-Up's own rules only");
        C(RideOutMath.Kind(60f, 0, 13, 13, false) == RideTrip.Out, "from the base to work out past it: out (the way back counted)");
        C(RideOutMath.Kind(60f, 13, 0, 0, false) == RideTrip.Back, "from out past the base to the base: back, one way");
        C(RideOutMath.Kind(60f, 13, 20, 20, false) == RideTrip.Out, "from one far place to work at another: out");
        C(RideOutMath.Kind(90f, 0, 30, 0, false) == RideTrip.Back, "a haul from out past the base into the stores: one way, it ends at home");
        C(RideOutMath.Kind(90f, 0, 0, 30, false) == RideTrip.Out, "a haul out past the base: the way back counted");
        C(RideOutMath.Kind(60f, 0, 13, 13, true) == RideTrip.Order && RideOutMath.Kind(60f, 0, 0, 0, true) == RideTrip.None,
            "a movement order far past the base rides; one inside it doesn't");
        C(RideOutMath.Slack(RideTrip.Order) == 1f && RideOutMath.Slack(RideTrip.Out) == RideOutMath.WorkSlack && RideOutMath.Slack(RideTrip.Back) == RideOutMath.WorkSlack
          && RideOutMath.WorkSlack > 1f && RideOutMath.WorkSlack <= 1.25f, "a movement order rides only when sooner; work may take up to a quarter longer");
        C(!RideOutMath.GiddyUpLooks(71f, RideOutMath.GiddyUpTripDefault, false) && RideOutMath.GiddyUpLooks(121f, RideOutMath.GiddyUpTripDefault, false)
          && !RideOutMath.GiddyUpLooks(400f, RideOutMath.GiddyUpTripDefault, true), "Giddy-Up's own look: over 120 cells, never for a drafted colonist");
        C(RideOutMath.TripMin < RideOutMath.GiddyUpTripDefault, "Rimfolk rides shorter trips than Giddy-Up's default, never longer ones only");
    }

    private static void Sums(Action<bool, string> C)
    {
        // Seconds on foot: one way, or there and back for work out past the base.
        C(Near(RideOutMath.WalkSeconds(46f, 0f, 46f, 4.6f, RideTrip.Back), 10f) && Near(RideOutMath.WalkSeconds(46f, 0f, 46f, 4.6f, RideTrip.Out), 20f),
            "on foot: the walk back only for work out there");
        C(Near(RideOutMath.WalkSeconds(46f, 23f, 0f, 4.6f, RideTrip.Back), 15f), "a haul's carry is walked too");
        C(RideOutMath.WalkSeconds(10f, 0f, 0f, 0f, RideTrip.Back) == float.MaxValue, "someone who can't walk: no sum");
        // Riding: the walk to the beast (Giddy-Up's power of 1.05), getting on, the ride with its 5% for the path.
        float ride = RideOutMath.RideSeconds(0f, 65f, 0f, 0f, 4.6f, 6.5f, 3f, RideTrip.Back);
        C(Near(ride, 3f + 65f * 1.05f / 6.5f), "riding: the beast beside them, getting on and the ride");
        C(RideOutMath.RideSeconds(20f, 50f, 0f, 0f, 4.6f, 6.5f, 3f, RideTrip.Back) > RideOutMath.RideSeconds(10f, 50f, 0f, 0f, 4.6f, 6.5f, 3f, RideTrip.Back),
            "a beast further off costs the walk to it");
        C(RideOutMath.RideSeconds(10f, 50f, 0f, 60f, 4.6f, 6.5f, 3f, RideTrip.Out) > RideOutMath.RideSeconds(10f, 50f, 0f, 60f, 4.6f, 6.5f, 3f, RideTrip.Back),
            "out to work: the ride back counted");
        C(RideOutMath.RideSeconds(10f, 50f, 0f, 0f, 4.6f, 0f, 3f, RideTrip.Back) == float.MaxValue, "a beast that can't move: no sum");
        C(!RideOutMath.Worth(10f, 100f, RideTrip.None) && !RideOutMath.Worth(float.MaxValue, 100f, RideTrip.Out), "no trip or no sum: never worth it");
        C(RideOutMath.Worth(12f, 10f, RideTrip.Out) && !RideOutMath.Worth(13f, 10f, RideTrip.Out) && !RideOutMath.Worth(10.5f, 10f, RideTrip.Order)
          && RideOutMath.Worth(10f, 10f, RideTrip.Order), "worth it by the slack: work up to 12.5 s against 10 s on foot, an order only when no slower");
        // Getting on, by Giddy-Up's own curves (JobDriver_Mount): a horse with a fair handler about 3 seconds, a thrumbo for a novice far longer.
        float horse = RideOutMath.MountSeconds(2.4f, 0.08f, 8);
        float thrumbo = RideOutMath.MountSeconds(4f, 0.985f, 0);
        C(Near(horse, 220f * 0.8f / 60f) && horse > 2.5f && horse < 3.5f, "getting on a horse with a handler of 8: about 3 seconds");
        C(Near(thrumbo, 300f * (0.8f + 1.2f * (0.985f - 0.35f) / 0.65f) * 1.4f / 60f) && thrumbo > 12f, "a thrumbo for someone with no handling: far longer");
        C(RideOutMath.MountSeconds(2.4f, 0.08f, 20) < horse && Near(RideOutMath.MountSeconds(2.4f, 0.08f, 20), 220f * 0.8f * 0.33f / 60f), "a master handler gets on quickest");
        C(Near(RideOutMath.MountSeconds(1.2f, 0f, 8), 200f * 0.8f / 60f) && Near(RideOutMath.MountSeconds(9f, 3f, 30), 300f * 2f * 0.33f / 60f),
            "the curves held flat past their ends, as the game's curves are");
    }

    private static void Wait(Action<bool, string> C)
    {
        C(RideOutMath.WaitExpiry(1000, 9000, 10000, 10000) == 18000, "a new job out there: Giddy-Up's own timer again from now");
        C(RideOutMath.WaitExpiry(1000, 2000, 10000, 5000) == 10000, "never shorter than it was");
        C(RideOutMath.WaitExpiry(0, 70000, 10000, 10000) == RideOutMath.WaitCap && RideOutMath.WaitCap == 60000, "a day at most");
        C(RideOutMath.WaitExpiry(0, 1000, 70000, 10000) == 70000, "a wait already longer is left as it is");
        C(RideOutMath.WaitExpiry(5000, 4000, 10000, 10000) == 10000, "a clock that went back: as it was");
        C(RideOutMath.RefusedTicks == 2500, "a beast a rider failed to get on is passed over by them for an hour");
    }

    /// <summary>
    /// The player's save League_of_Toba (15 November 2026, read with a script outside the save's folder): the home area's
    /// middle about (64, 194), Avila mining steel at (93, 259), 13 cells past the home area; Bautista at (137, 260), 47 past it;
    /// the pen's beasts about 25 cells south of the base's middle, the other way from the steel.
    /// </summary>
    private static void TheirSave(Action<bool, string> C)
    {
        float avila = Dist(64, 194, 93, 259), bautista = Dist(64, 194, 137, 260);
        C(avila > 70f && avila < 72f && bautista > 97f && bautista < 99f, "the save's distances: about 71 and 98 cells from the base's middle");
        C(!RideOutMath.GiddyUpLooks(avila, RideOutMath.GiddyUpTripDefault, false) && !RideOutMath.GiddyUpLooks(bautista, RideOutMath.GiddyUpTripDefault, false),
            "why they walked: both under Giddy-Up's 120 cells, so it never looked for a mount");
        C(RideOutMath.Kind(avila, 0, 13, 13, false) == RideTrip.Out && RideOutMath.Kind(bautista, 0, 47, 47, false) == RideTrip.Out, "Rimfolk: both are work out past the base");
        // Avila: walks 4.6 cells a second; a horse under her rides 6.5; the pen 25 cells off the other way; the horse 93 cells from the steel.
        float mount = RideOutMath.MountSeconds(2.4f, 0.08f, 6);
        float walk = RideOutMath.WalkSeconds(avila, 0f, avila, 4.6f, RideTrip.Out);
        float ride = RideOutMath.RideSeconds(25f, Dist(65, 170, 93, 259), 0f, avila, 4.6f, 6.5f, mount, RideTrip.Out);
        C(ride > walk && RideOutMath.Worth(ride, walk, RideTrip.Out), "Avila rides out and back, though the pen lies the other way (within the quarter)");
        C(!RideOutMath.Worth(ride, walk, RideTrip.Order), "the same sums as a movement order: on foot, as it is quicker");
        // The way home from the steel, her beast waiting a few cells off: about as quick as walking on a horse, quicker on a faster beast.
        float home = RideOutMath.RideSeconds(5f, avila, 0f, 0f, 4.6f, 6.5f, mount, RideTrip.Back), onFoot = RideOutMath.WalkSeconds(avila, 0f, 0f, 4.6f, RideTrip.Back);
        C(RideOutMath.Kind(avila, 13, 0, 0, false) == RideTrip.Back && RideOutMath.Worth(home, onFoot, RideTrip.Back), "and rides home on the beast that waited");
        C(RideOutMath.RideSeconds(5f, avila, 0f, 0f, 4.6f, 9f, mount, RideTrip.Back) < onFoot, "on a fast beast the ride home is quicker than walking");
    }

    private static void Seams(string root, Action<bool, string> C)
    {
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string ride = Code("Integrations/GiddyUpRideOut.cs"), mounts = Code("Integrations/GiddyUpMounts.cs"), settings = Code("CouncilSettings.cs"), catalog = Code("UI/SettingsCatalog.cs");
        string keys = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_RideOut.xml"));

        C(ride.Contains("[HarmonyPatch(typeof(Pawn_JobTracker), \"DetermineNextJob\")]") && ride.Contains("[HarmonyAfter(\"GiddyUp\")]") && ride.Contains("Priority.Last"),
            "as a job is chosen, after Giddy-Up's own look (where it mounts someone, nothing changes)");
        C(ride.Contains("__instance?.curJob != null) return;"), "only when nothing is under way: never the game's regular look for a better job");
        C(ride.Contains("public static bool Prepare() => GiddyUpMounts.On;") && ride.Contains("\"Giddy-Up isn't loaded\""), "soft: only with Giddy-Up");
        C(ride.Contains("GiddyUpMounts.MountFirst(pawn, plan.animal, result, then)") && mounts.Contains("args[2] = inject;") && mounts.Contains("Enum.Parse(kind, \"Inject\")"),
            "getting on Giddy-Up's own way (its Inject: the Mount job first, the chosen job next in the queue)");
        int first = mounts.IndexOf("public static ThinkResult? MountFirst", StringComparison.Ordinal);
        int end = first < 0 ? -1 : mounts.IndexOf("public static bool Riding", first, StringComparison.Ordinal);
        C(first > 0 && end > first && !mounts.Substring(first, end - first).Contains("Train("), "the colony's own beasts are never taught anything to be ridden");
        C(ride.Contains("\"IsMountable\"") && ride.Contains("m.GetParameters().Length == 7") && ride.Contains("new object[] { animal, null, rider, true, true, true, reservations }"),
            "Giddy-Up's own IsMountable decides: its list, grown, tame, weight, busy, roped, ridden, reserved, condition, research, the colony's own");
        C(ride.Contains("\"IsAllowed\"") && ride.Contains("\"canRide\"") && ride.Contains("\"IsCapableOfRiding\""),
            "the Animals tab's automount column, the Assign tab's allowed to ride, and the rider's age and wheelchair");
        C(ride.Contains("\"rideAndRollEnabled\"") && ride.Contains("\"battleMountsEnabled\"") && ride.Contains("\"waitForRiderTimer\"") && ride.Contains("\"minAutoMountDistance\""),
            "Giddy-Up's settings read: Ride and Roll for work, battle mounts for movement orders, its wait timer and distance");
        C(ride.Contains("if (Working(a))") && ride.Contains("if (WithYoung(a))") && ride.Contains("GiddyUpMounts.CarryingRider(a)") && ride.Contains("a.GetLord() != null")
          && ride.Contains("keeper != rider") && ride.Contains("a.Downed"), "never a beast at work, with young, carrying a rider, with a group, kept for someone else or downed");
        C(ride.Contains("job.workGiverDef != null") && ride.Contains("d == JobDefOf.HaulToCell") && ride.Contains("d == JobDefOf.PredatorHunt"),
            "at work: hauling, hunting and anything a work giver gave it");
        C(ride.Contains("p.IsFreeNonSlaveColonist") && ride.Contains("p.GetLord() != null") && ride.Contains("job.def != JobDefOf.Goto || !job.playerForced")
          && ride.Contains("p.IsQuestLodger()"), "free colonists only (no slave, guest, prisoner or lodger, no caravan or ritual); drafted ones on a movement order only");
        C(ride.Contains("RideOutMath.Kind(plan.trip + plan.carry, plan.riderOut, plan.targetOut, plan.finalOut, order)") && ride.Contains("RideOutMath.Worth(bestSeconds, plan.walkSeconds, plan.kind)"),
            "the trip and the sums by the pure rules");
        C(ride.Contains("map.areaManager?.Home") && ride.Contains("BaseEdge.CellsOut(map, pawn.Position)"), "out past the base: measured from the home area's edge");
        C(ride.Contains("job.def == JobDefOf.TendPatient || d == JobDefOf.Refuel") || ride.Contains("d == JobDefOf.TendPatient || d == JobDefOf.Refuel || d == JobDefOf.FixBrokenDownBuilding"),
            "where a job goes, Giddy-Up's own way (tending, refuelling and repairs; a bill's first ingredient)");
        C(ride.Contains("RideOutMath.WaitExpiry(wait.startTick, now, wait.expiryInterval, GiddyUpRules.WaitTimer)") && ride.Contains("GiddyUpMounts.WaitingFor(beast, rider)"),
            "the beast waiting for its rider out there keeps waiting");
        C(ride.Contains("job.targetC.Thing == null") && ride.Contains("JobMaker.MakeJob(GiddyUpMounts.DismountJob, plan.penCell)"),
            "a handler's rope job home from out past the base: ridden to the pen instead (Giddy-Up's own rope job left alone)");
        C(ride.Contains("refused[") && ride.Contains("RideOutMath.RefusedTicks"), "a failed try isn't repeated with the same beast for an hour");
        C(ride.Contains("\"Riding out: why a colonist rides or not (log)...\""), "the dev action says why");
        C(settings.Contains("public bool rideOut = true;") && settings.Contains("Scribe_Values.Look(ref rideOut, \"rideOut\", true);"), "the setting, on by default and kept");
        C(catalog.Contains("Mod(Toggle(\"rideOut\", \"CC_Settings_RideOut\", \"CC_SetNote_RideOut\", \"deepHooks\"), GiddyUpId);") && catalog.Contains("GiddyUpId = \"memegoddess.giddyup\""),
            "on Your other mods, under Giddy-Up 2, shown only with it");
        C(keys.Contains("<CC_Settings_RideOut>") && keys.Contains("<CC_Settings_RideOut_Tip>") && keys.Contains("<CC_SetNote_RideOut>"), "the setting's words");
        C(keys.Contains((int)RideOutMath.TripMin + " cells") && keys.Contains(RideOutMath.OutMin + " cells") && keys.Contains((int)RideOutMath.GiddyUpTripDefault + " cells"),
            "the setting's words say the same numbers as the rules");
        string almanac = File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Council_Animals.xml"));
        C(almanac.Contains((int)RideOutMath.TripMin + " cells") && almanac.Contains(RideOutMath.OutMin + " cells") && almanac.Contains("Find a mount if trip is longer than"),
            "the Almanac's animals page says the same numbers and names Giddy-Up's setting");
    }

    private static float Dist(int x0, int z0, int x1, int z1) => (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0));

    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.01f;
}
