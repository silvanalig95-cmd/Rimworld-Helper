using ColonyCouncil;

/// <summary>
/// Riding out to far work and back with Giddy-Up 2 (the player, 15 November 2026: "pawns should really use mounts whenever
/// they walk away out of the base range ... avila did not mount up despite going really far to mine. it should mount to go
/// there, and mount back up aftr finishing work"; 16 November 2026: "it's not really working for all movement orders via
/// giddy up default settings"; 18 November 2026: "WHY DOES bautista still walk halfway across the map to mine without
/// mounting up first? then the mount should wait for him until he's done"; Integrations/RideOutMath.cs, GiddyUpRideOut.cs):
/// which trips count, the sums against walking, out to work on any beast near enough, Giddy-Up's old marks, getting on,
/// the wait kept, the last decision kept, the player's saves, the setting and the seams.
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
        OutToWork(C);
        Wait(C);
        Notes(C);
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

    /// <summary>Out to work: any free beast no further from them than the work and no slower than they walk, the quickest taken.</summary>
    private static void OutToWork(Action<bool, string> C)
    {
        C(RideOutMath.NearEnough(34f, 95f) && RideOutMath.NearEnough(95f, 95f) && !RideOutMath.NearEnough(95.5f, 95f),
            "a beast counts when the walk to it is no longer than the trip itself");
        C(!RideOutMath.NearEnough(float.NaN, 95f) && !RideOutMath.NearEnough(10f, float.NaN) && !RideOutMath.NearEnough(-1f, 95f), "no distance read: no");
        C(RideOutMath.FastEnough(6f, 4.6f) && RideOutMath.FastEnough(4.6f, 4.6f) && !RideOutMath.FastEnough(4f, 4.6f) && !RideOutMath.FastEnough(0f, 0f),
            "never a beast slower under them than they walk");
        // Out to work, any sum rides; the way back and movement orders keep the sums against walking.
        C(RideOutMath.Rides(RideTrip.Out, 60f, 40f) && RideOutMath.Rides(RideTrip.Out, 500f, 40f) && !RideOutMath.Rides(RideTrip.Out, float.MaxValue, 40f),
            "out to work: they ride whenever a beast was found, however the sums come out");
        C(RideOutMath.Rides(RideTrip.Back, 12f, 10f) && !RideOutMath.Rides(RideTrip.Back, 13f, 10f), "back from out there: up to a quarter longer than walking, as before");
        C(RideOutMath.Rides(RideTrip.Order, 10f, 10f) && !RideOutMath.Rides(RideTrip.Order, 10.5f, 10f), "a movement order: only when no slower, as before");
        C(!RideOutMath.Rides(RideTrip.None, 1f, 100f), "no trip of Rimfolk's: no ride");
        // Giddy-Up's mark keeping a beast for someone holds only while it waits for them, carries them or they are getting on.
        C(RideOutMath.KeptFor(true, false, false) && RideOutMath.KeptFor(false, true, false) && RideOutMath.KeptFor(false, false, true),
            "a mark holds while the beast waits for that rider, carries them, or they are getting on it");
        C(!RideOutMath.KeptFor(false, false, false), "otherwise the mark is old and left aside");
    }

    private static void Wait(Action<bool, string> C)
    {
        C(RideOutMath.WaitExpiry(1000, 9000, 10000, 10000, 9000) == 18000, "a new job out there: Giddy-Up's own timer again from now");
        C(RideOutMath.WaitExpiry(1000, 2000, 10000, 5000, 2000) == 10000, "never shorter than it was");
        C(RideOutMath.WaitExpiry(0, 1000, 70000, 10000, 1000) == 70000, "a wait already longer is left as it is");
        C(RideOutMath.WaitExpiry(5000, 4000, 10000, 10000, 4000) == 10000, "a clock that went back: as it was");
        C(RideOutMath.WaitExpiry(0, 1000, -1, 10000, 1000) == -1 && RideOutMath.WaitExpiry(0, 1000, 0, 10000, 1000) == 0, "a wait with no end of its own is left as it is");
        C(RideOutMath.RefusedTicks == 2500 && RideOutMath.WaitCap == 60000, "a beast a rider failed to get on is passed over by them for an hour; a day after the last job at most");
        // The cap counts from the rider's last job, not from the start of the wait.
        C(RideOutMath.WaitExpiry(0, 70000, 75000, 10000, 65000) == 80000, "still at work more than a day after the beast was left: it keeps waiting");
        C(RideOutMath.WaitExpiry(0, 50000, 52000, 10000, 0) == 60000, "a job begun long ago: a day after it began at most");
        C(RideOutMath.WaitExpiry(0, 58000, 60000, 10000, 0) == 60000, "and no further");
        C(RideOutMath.WaitExpiry(0, 70000, 72000, 10000, 5000) == 72000, "never set at or below the time already waited (the game would carry it on another round)");
        C(RideOutMath.WaitExpiry(0, 9000, 10000, 10000, 20000) == 19000, "a last job read as later than now counts as now");

        // Two days of mining out there, a new cell every 2000 ticks, the wait looked at every 250 ticks: it never runs out
        // while he works (Giddy-Up's 10000 ticks alone would end it after 4 hours); once he has gone home it ends within the
        // timer, and when he starts no new job it ends a day after his last one.
        int expiry = 10000, lastJob = 0, endedAt = -1;
        for (int now = 0; now <= 140000; now += Sched.RareTicks)
        {
            if (now < 120000 && now % 2000 == 0) lastJob = now;
            if (now >= expiry)
            {
                endedAt = now;
                break;
            }
            if (now < 120000) expiry = RideOutMath.WaitExpiry(0, now, expiry, 10000, lastJob);
        }
        C(endedAt >= 120000 && endedAt <= 120000 + 10000, $"two days of work out there: the beast waits the whole time and leaves within the timer once he has gone ({endedAt})");
        int stuck = 10000, stuckEnd = -1;
        for (int now = 0; now <= 100000; now += Sched.RareTicks)
        {
            if (now >= stuck)
            {
                stuckEnd = now;
                break;
            }
            stuck = RideOutMath.WaitExpiry(0, now, stuck, 10000, 1000);
        }
        C(stuckEnd == 1000 + RideOutMath.WaitCap, $"one job and no other after it: the beast waits a day after it began, then goes ({stuckEnd})");
    }

    /// <summary>A colonist's last decision kept for the dev action, in plain words.</summary>
    private static void Notes(Action<bool, string> C)
    {
        var walked = new RideOutNote { tick = 7668315, job = "Mine", kind = RideTrip.Out, cells = 94.6f, why = "walking is quicker (41 s on foot against 52 s on Calvin)", beast = "Calvin", rideSeconds = 52f, walkSeconds = 41f };
        string w = walked.Describe(7668315 + 5000);
        C(w.Contains("2.0 hours ago") && w.Contains("Mine") && w.Contains("out to work") && w.Contains("94 cells") && w.Contains("no ride, walking is quicker")
          && !w.Contains("(Calvin:"), "a walk: when, the job, the trip, and why (the sums named once): " + w);
        var rode = new RideOutNote { tick = 100, job = "Mine", kind = RideTrip.Out, cells = 95f, beast = "Calvin", rideSeconds = 53f, walkSeconds = 41f, mount = RideMount.InSaddle };
        string r = rode.Describe(100);
        C(r.Contains("they ride (Calvin: 53 s riding against 41 s on foot)") && r.Contains("they got in the saddle"), "a ride: the beast, its sums and what came of it: " + r);
        var none = new RideOutNote { tick = 100, job = "Mine", kind = RideTrip.Out, cells = 95f, why = "no beast Giddy-Up lets them ride is free" };
        C(none.Describe(50).Contains("the clock went back") && none.Describe(100).EndsWith("no beast Giddy-Up lets them ride is free."), "no beast: no sums; a clock that went back is said");
        C(RideOutNote.MountWords(RideMount.NotTaken).Contains("didn't take") && RideOutNote.MountWords(RideMount.DidNotGetOn).Contains("passed over")
          && RideOutNote.MountWords(RideMount.Sent).Contains("not yet known"), "what came of the order to get on, each in words");
    }

    /// <summary>
    /// The player's save League_of_Toba_7 (18 November 2026, tick 7668315, read with a script outside the save's folder):
    /// Bautista mining steel at (136, 264), 44 cells past the home area's edge, having chosen the job in the dining hall
    /// about (62, 205) after a meal; walking 4.6 cells a second, Animals 4. The colony's beasts in and by the pen in the base,
    /// about 36 to 53 cells from him the other way: Stallion 1 (65, 166), Calvin the titanis (64, 169), Zoe (65, 169), Takuya
    /// (78, 161), Gonzalo (93, 185), Emma (53, 153). Giddy-Up refused the horses and the rest (tameness not learned, with
    /// young); its data still kept Tatyana the horse for Soot and Zoe for Wyninn, all of them at home; Calvin, about 6 cells
    /// a second under him, was the best left.
    /// </summary>
    private static void TheirSave(Action<bool, string> C)
    {
        float trip = Dist(62, 205, 136, 264), toCalvin = Dist(62, 205, 64, 169), calvinToVein = Dist(64, 169, 136, 264);
        C(trip > 94f && trip < 96f && toCalvin > 35f && toCalvin < 37f && calvinToVein > 118f && calvinToVein < 121f,
            "the save's distances: the steel about 95 cells from the dining hall, the pen about 36 the other way, 119 from the pen to the steel");
        C(!RideOutMath.GiddyUpLooks(trip, RideOutMath.GiddyUpTripDefault, false), "under Giddy-Up's 120 cells, so it never looked for a mount itself");
        C(RideOutMath.Kind(trip, 0, 44, 44, false) == RideTrip.Out, "Rimfolk: work out past the base, out and back");
        // What Rimfolk decided before: out and back on Calvin against walking there and back, with a titanis's time to get
        // on (body size 3, Animals 4; any wildness from a half up): over the quarter's slack, so "walking is quicker".
        float walk = RideOutMath.WalkSeconds(trip, 0f, trip, 4.6f, RideTrip.Out);
        bool walkedBefore = true;
        foreach (float wild in new[] { 0.5f, 0.75f, 0.985f })
        {
            float ride = RideOutMath.RideSeconds(toCalvin, calvinToVein, 0f, trip, 4.6f, 6f, RideOutMath.MountSeconds(3f, wild, 4), RideTrip.Out);
            walkedBefore &= !RideOutMath.Worth(ride, walk, RideTrip.Out) && RideOutMath.Rides(RideTrip.Out, ride, walk);
        }
        C(walkedBefore, "the old sums said walking is quicker; now he rides Calvin out");
        C(RideOutMath.NearEnough(toCalvin, trip) && RideOutMath.FastEnough(6f, 4.6f), "Calvin is nearer to him than the steel is, and quicker than his feet");
        int[,] pen = { { 65, 166 }, { 64, 169 }, { 65, 169 }, { 78, 161 }, { 93, 185 }, { 53, 153 } };
        bool allNear = true;
        for (int i = 0; i < pen.GetLength(0); i++) allNear &= RideOutMath.NearEnough(Dist(62, 205, pen[i, 0], pen[i, 1]), trip);
        C(allNear, "every beast in the pen counts by distance; Giddy-Up's own rules and Rimfolk's say which may be ridden");
        C(!RideOutMath.KeptFor(false, false, false), "a beast still marked as kept for someone at home, neither waiting for, carrying nor being got on by them: the mark is left aside");
        // The way home from the steel, Calvin waiting a few cells off: the sums against walking, as before.
        float home = RideOutMath.RideSeconds(5f, trip, 0f, 0f, 4.6f, 6f, RideOutMath.MountSeconds(3f, 0.5f, 4), RideTrip.Back), onFoot = RideOutMath.WalkSeconds(trip, 0f, 0f, 4.6f, RideTrip.Back);
        C(RideOutMath.Kind(trip, 44, 0, 0, false) == RideTrip.Back && RideOutMath.Rides(RideTrip.Back, home, onFoot), "and rides home on the beast that waited");
        // A beast further off than the work is never fetched first: from the steel to another vein 50 cells on, the pen is 120 away.
        C(!RideOutMath.NearEnough(calvinToVein, 50f), "out there, the pen's beasts are too far to fetch for the next far job");
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
        C(ride.Contains("keeper.Spawned && KeptFor(a, keeper))")
          && ride.Contains("RideOutMath.KeptFor(GiddyUpMounts.WaitingFor(a, keeper), GiddyUpMounts.Riding(keeper, a), GiddyUpMounts.MountingOnto(keeper, a))")
          && mounts.Contains("public static bool MountingOnto(Pawn rider, Pawn animal)") && mounts.Contains("rider.jobs.jobQueue"),
            "Giddy-Up's mark counts only while the beast waits for, carries or is being got on by that rider (their queue too)");
        C(ride.Contains("bool outTrip = plan.kind == RideTrip.Out;") && ride.Contains("outTrip && !RideOutMath.NearEnough(toAnimal, reach)")
          && ride.Contains("outTrip && !RideOutMath.FastEnough(speed, walk)") && ride.Contains("float reach = plan.trip + plan.carry;"),
            "out to work: beasts further off than the trip, or slower than walking, are passed over; the quickest of the rest is taken");
        C(ride.Contains("job.workGiverDef != null") && ride.Contains("d == JobDefOf.HaulToCell") && ride.Contains("d == JobDefOf.PredatorHunt"),
            "at work: hauling, hunting and anything a work giver gave it");
        C(ride.Contains("p.IsFreeNonSlaveColonist") && ride.Contains("p.GetLord() != null") && ride.Contains("job.def != JobDefOf.Goto || !job.playerForced")
          && ride.Contains("p.IsQuestLodger()"), "free colonists only (no slave, guest, prisoner or lodger, no caravan or ritual); drafted ones on a movement order only");
        C(ride.Contains("RideOutMath.Kind(plan.trip + plan.carry, plan.riderOut, plan.targetOut, plan.finalOut, order)") && ride.Contains("RideOutMath.Rides(plan.kind, bestSeconds, plan.walkSeconds)")
          && !ride.Contains("RideOutMath.Worth(bestSeconds"), "the trip and the sums by the pure rules (out to work whenever a beast was found)");
        C(ride.Contains("map.areaManager?.Home") && ride.Contains("BaseEdge.CellsOut(map, pawn.Position)"), "out past the base: measured from the home area's edge");
        C(ride.Contains("job.def == JobDefOf.TendPatient || d == JobDefOf.Refuel") || ride.Contains("d == JobDefOf.TendPatient || d == JobDefOf.Refuel || d == JobDefOf.FixBrokenDownBuilding"),
            "where a job goes, Giddy-Up's own way (tending, refuelling and repairs; a bill's first ingredient)");
        C(ride.Contains("RideOutMath.WaitExpiry(wait.startTick, now, wait.expiryInterval, GiddyUpRules.WaitTimer, lastJob)") && ride.Contains("GiddyUpMounts.WaitingFor(beast, rider)"),
            "the beast waiting for its rider out there keeps waiting, until a day after their last job");
        C(ride.Contains("KeepWaiting(pawn, GiddyUpRules.ReservedMount(pawn), now, now)") && ride.Contains("KeepWaiting(rider, beast, rider.CurJob.startTick, now)")
          && ride.Contains("job.def != GiddyUpMounts.WaitJob") && ride.Contains("if (!OutThere(rider, map)) continue;"),
            "the wait looked at as a job is chosen and every 250 ticks while the rider works out there");
        C(ride.Contains("public class GameComponent_RideOut : GameComponent") && ride.Contains("Sched.Every(now, Sched.RareTicks, Sched.RareRideOut) || !GiddyUpRideOut.On")
          && ride.Contains("PassClock.Time(Pass.RareRideOut)") && ride.Contains("GiddyUpRideOut.Reset();") && keys.Contains("<CC_Clock_Pass_RareRideOut>"),
            "its pass on a 250-tick slot of its own, timed and named on the clock, only with riding out on; nothing kept across games");
        var otherRare = PassTable.All.Where(p => p.period == Sched.RareTicks && p.pass != Pass.RareRideOut).Select(p => p.offset).Concat(Sched.HourlyOnCycle).ToList();
        C(Sched.RareOffsets.Count(o => o == Sched.RareRideOut) == 1 && !otherRare.Contains(Sched.RareRideOut) && Sched.HourlyOffsets.All(h => h % Sched.RareTicks != Sched.RareRideOut)
          && Sched.RareRideOut < Sched.RareTicks, "riding out's pass has a tick of the cycle of its own, off every hourly pass");
        // The last decision kept and shown; the self-check's line.
        C(ride.Contains("var note = Note(pawn, job, plan, now);") && ride.Contains("note.mount = mounted == null ? RideMount.NotTaken : RideMount.Sent;")
          && ride.Contains("note.mount = on ? RideMount.InSaddle : RideMount.DidNotGetOn;") && ride.Contains("last.Describe(Now)"),
            "each colonist's last decision kept in memory, what came of it, and shown by the dev action");
        C(!ride.Contains("Scribe_"), "nothing of riding out is saved");
        string check = Code("UI/SelfCheck.cs");
        C(check.Contains("GiddyUpRideOut.CheckLine()") && ride.Contains("Patch_RideOutNextJob.Broken") && ride.Contains("GiddyUpMounts.InjectFailed")
          && ride.Contains("GiddyUpRules.CanRideFound") && ride.Contains("GiddyUpRules.CapableFound") && mounts.Contains("InjectFailed = true;"),
            "check myself says when riding out switched itself off or Giddy-Up's canRide or IsCapableOfRiding wasn't found");
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
