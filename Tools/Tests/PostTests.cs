using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// The post (docs/design/post-and-letters.md, step 1; Post/PostMath.cs): letters that travel with whoever is going their
/// way (the on-their-way rule), the regular post by the age and its pace, the wait before a messenger brings the mail, the
/// diplomatic range until radio (the land's cost, the range by age and means, the relay), the reach line, and the seams.
/// </summary>
public static class PostTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void C(bool ok, string what)
        {
            cases++;
            check(ok, "the post: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        Way(C);
        Carriers(C);
        Land(C);
        Range(C);
        Relay(C);
        Post(C);
        Mail(C);
        Reach(C);
        Seams(root, Code, C);
        return cases;
    }

    // ---------------- On their way ----------------

    private static void Way(Action<bool, string> C)
    {
        C(PostMath.Heading(0f, 1f) == 0f && Math.Abs(PostMath.Heading(1f, 0f) - 90f) < 0.01f && Math.Abs(PostMath.Heading(0f, -1f) - 180f) < 0.01f
          && Math.Abs(PostMath.Heading(-1f, 0f) - 270f) < 0.01f, "headings: north 0, east 90, south 180, west 270");
        C(Math.Abs(PostMath.Angle(350f, 10f) - 20f) < 0.01f && Math.Abs(PostMath.Angle(10f, 350f) - 20f) < 0.01f && Math.Abs(PostMath.Angle(0f, 180f) - 180f) < 0.01f,
            "the angle between headings wraps round north");
        C(PostMath.OnTheirWay(true, 270f, 40f, false, 0f, 0f, true, 90f) == WayWhy.Theirs, "their own people's towns are on their way, whichever way they leave");
        C(PostMath.OnTheirWay(false, 140f, 20f, false, 0f, 0f, true, 90f) == WayWhy.Cone, "a town 50 degrees off the way they leave is on it (a generous cone)");
        C(PostMath.OnTheirWay(false, 155f, 20f, false, 0f, 0f, true, 90f) == WayWhy.No, "a town 65 degrees off is not");
        C(PostMath.OnTheirWay(false, 270f, 20f, false, 0f, 0f, true, 90f) == WayWhy.No, "a town behind them is not");
        C(PostMath.OnTheirWay(false, 270f, 1.5f, false, 0f, 0f, true, 90f) == WayWhy.Cone, "a town at the colony's door is on everyone's way");
        C(PostMath.OnTheirWay(false, 20f, 25f, true, 10f, 40f, true, 200f) == WayWhy.Road, "a town along the road to where they're going (nearer, a few degrees off)");
        C(PostMath.OnTheirWay(false, 50f, 39f, true, 40f, 40f, true, 200f) == WayWhy.Road, "a town near their destination is on their road");
        C(PostMath.OnTheirWay(false, 20f, 70f, true, 10f, 40f, true, 200f) == WayWhy.Cone, "a town well past their destination, that way: only the cone");
        C(PostMath.OnTheirWay(false, 100f, 30f, true, 10f, 40f, true, 200f) == WayWhy.No, "a town far off the road and the way out is not");
        // Eight towns round the colony, the carrier leaving east: the three toward the east.
        int east = 0;
        for (int h = 0; h < 360; h += 45)
            if (PostMath.OnTheirWay(false, h, 30f, false, 0f, 0f, true, 90f) != WayWhy.No)
                east++;
        C(east == 3, $"of eight towns round the colony, a carrier leaving east takes the letters for three ({east})");
        C(PostMath.OnTheirWay(false, 90f, 30f, false, 0f, 0f, false, 0f) == WayWhy.No, "no way known and not their people: not on their way");
    }

    private static void Carriers(Action<bool, string> C)
    {
        C(PostMath.CarrierOf("LordJob_TradeWithColony") == PostCarrier.Traders, "a trade caravan's group is traders");
        C(PostMath.CarrierOf("LordJob_VisitColony") == PostCarrier.Visitors, "the game's visitors (and Hospitality's guests, by the same name) are visitors");
        C(PostMath.CarrierOf("LordJob_Camp") == PostCarrier.Camp, "a camp of Rimfolk's is a camp");
        C(PostMath.CarrierOf("LordJob_Courier") == PostCarrier.Messengers && PostMath.CarrierOf("LordJob_Messenger") == PostCarrier.Messengers
          && PostMath.CarrierOf("LordJob_EnvoyStay") == PostCarrier.Messengers, "couriers, messengers and envoys are a people's messengers");
        C(PostMath.CarrierOf("LordJob_PostRider") == PostCarrier.PostRider, "the post rider is the post");
        C(PostMath.CarrierOf("LordJob_TravelAndExit") == PostCarrier.Travellers && PostMath.CarrierOf("LordJob_RimfolkVisit") == PostCarrier.Visitors
          && PostMath.CarrierOf("LordJob_ExitMapBest") == PostCarrier.Travellers, "travellers passing and anyone else on the road");
        C(PostMath.CarrierOf("LordJob_PilgrimsRoad") == PostCarrier.Pilgrims, "pilgrims by their group's name");
        foreach (var no in new[] { "LordJob_CampRaid", "LordJob_WarBand", "LordJob_AssaultColony", "LordJob_Siege", "LordJob_CouncilMeeting", "LordJob_Election", "LordJob_PlayOut",
                     "LordJob_StrayBeast", "LordJob_BestowingCeremony", "LordJob_DefendPoint" })
            C(!PostMath.CarriesMail(no), "carries no mail: " + no);
        foreach (var yes in new[] { "LordJob_TradeWithColony", "LordJob_VisitColony", "LordJob_Camp", "LordJob_Courier", "LordJob_TravelAndExit", "LordJob_FeastGuests", "LordJob_MatchEscort" })
            C(PostMath.CarriesMail(yes), "carries mail: " + yes);
        C(PostMath.Outsider(PostCarrier.Traders) && !PostMath.Outsider(PostCarrier.OwnCaravan) && !PostMath.Outsider(PostCarrier.OwnRider) && !PostMath.Outsider(PostCarrier.None),
            "outsiders walk to the mailbag; the colony's own and someone of theirs here are handed it");
        C(PostMath.MeansOf(WorldAge.Tribal, false) == LetterMeans.Runner && PostMath.MeansOf(WorldAge.Tribal, true) == LetterMeans.Rider
          && PostMath.MeansOf(WorldAge.Medieval, false) == LetterMeans.Rider && PostMath.MeansOf(WorldAge.Industrial, false) == LetterMeans.Courier
          && PostMath.MeansOf(WorldAge.Spacer, true) == LetterMeans.Shuttle, "a carrier's means: on foot, mounted, by vehicle, by shuttle");
    }

    // ---------------- The land ----------------

    private static void Land(Action<bool, string> C)
    {
        float flat = PostMath.TileDays(1f, 1f, 1f, 1f);
        C(flat > 0.1f && flat < 0.12f, $"a flat tile is about a ninth of a day's walk ({flat:0.000})");
        C(Math.Abs(PostMath.TileDays(2f, 1f, 1f, 1f) - 2f * flat) < 1e-4f && PostMath.TileDays(3f, 1f, 1f, 1f) > PostMath.TileDays(2f, 1f, 1f, 1f), "hills twice flat land, mountains more");
        C(Math.Abs(PostMath.TileDays(2f, 0.5f, 1f, 1f) - flat) < 1e-4f && PostMath.TileDays(1f, 0.3f, 1f, 1f) < PostMath.TileDays(1f, 0.5f, 1f, 1f), "a road halves it, a stone road more");
        C(PostMath.TileDays(1f, 1f, 0.8f, 1f) < flat, "a river's way eases it");
        C(Math.Abs(PostMath.TileDays(1f, 1f, 1f, 2f) - 2f * flat) < 1e-4f && PostMath.TileDays(1f, 1f, 1f, 0.5f) == flat, "winter and snow double it; nothing makes the season faster than summer");
        C(PostMath.TileDays(1000f, 1f, 1f, 1f) < 0f && PostMath.TileDays(0f, 1f, 1f, 1f) < 0f, "impassable land can't be crossed");
        C(PostMath.TileDays(1f, 3f, 3f, 1f) == flat, "a factor above one never slows it as a road or a river");
        C(PostMath.RouteDays(new List<float> { flat, flat, 2f * flat }) > 0f && Math.Abs(PostMath.RouteDays(new List<float> { flat, flat }) - 2f * flat) < 1e-5f,
            "a route is its tiles' days");
        C(PostMath.RouteDays(new List<float> { flat, -1f, flat }) < 0f && PostMath.RouteDays(null) < 0f, "a route with a tile that can't be crossed is cut");
        // By the means.
        C(PostMath.LegDays(4f, 40f, LetterMeans.Rider) < PostMath.LegDays(4f, 40f, LetterMeans.Runner) && PostMath.LegDays(4f, 40f, LetterMeans.Courier) < PostMath.LegDays(4f, 40f, LetterMeans.Rider),
            "mounted beats on foot, a vehicle beats a mount, over the same road");
        C(Math.Abs(PostMath.LegDays(4f, 40f, LetterMeans.Runner) - (LetterMath.LegStartDays + 3.2f)) < 1e-4f, "on foot: half a day to start, then 0.8 of a caravan's walk");
        C(PostMath.LegDays(-1f, 40f, LetterMeans.Rider) < 0f, "no way by land: no leg by land");
        C(PostMath.LegDays(-1f, 40f, LetterMeans.Shuttle) > 0f && PostMath.LegDays(30f, 200f, LetterMeans.Shuttle) <= 2f, "a shuttle goes over the land: two days at most, a way by land or not");
        C(PostMath.LegDays(9f, 40f, LetterMeans.Runner) > PostMath.LegDays(3f, 40f, LetterMeans.Runner), "the same distance over hard land takes longer");
    }

    // ---------------- The range ----------------

    private static void Range(Action<bool, string> C)
    {
        C(PostMath.RangeDays(WorldAge.Tribal, false) == 5f && PostMath.RangeDays(WorldAge.Medieval, false) == 7f && PostMath.RangeDays(WorldAge.Industrial, false) == 9f,
            "the range by the colony's age: five days by runners, seven by riders, nine by vehicles");
        C(PostMath.RangeDays(WorldAge.Spacer, false) < 0f, "a spacer colony's shuttles have no range");
        C(PostMath.RangeDays(WorldAge.Medieval, true) == 10.5f, "a Signal Line mast near widens it by half");
        C(PostMath.ColonyMeans(WorldAge.Tribal, true) == LetterMeans.Rider && PostMath.ColonyMeans(WorldAge.Tribal, false) == LetterMeans.Runner,
            "a tribe with an animal to ride sends riders");
        // Mounts widen it: the same town within range mounted, out of it on foot.
        float walk = 9f;
        float range = PostMath.RangeDays(WorldAge.Tribal, false);
        C(PostMath.LegDays(walk, 50f, LetterMeans.Runner) > range && PostMath.LegDays(walk, 50f, LetterMeans.Rider) <= range, "a mount brings a town nine walking days off within a tribe's range");
        C(PostMath.Band(true, 30f, 7f, false) == ReachBand.Radio && PostMath.Band(false, 30f, -1f, false) == ReachBand.Radio, "radio, or shuttles: no range");
        C(PostMath.Band(false, 6.9f, 7f, false) == ReachBand.Near && PostMath.Band(false, 7f, 7f, false) == ReachBand.Near, "within range");
        C(PostMath.Band(false, 9f, 7f, true) == ReachBand.Far && PostMath.Band(false, 9f, 7f, false) == ReachBand.Cut, "beyond it: by relay, else out of reach");
        C(PostMath.Band(false, -1f, 7f, false) == ReachBand.Cut && PostMath.Band(false, -1f, 7f, true) == ReachBand.Far, "no way by land straight there: a relay or nothing");
        C(PostMath.RadioReaches(4, -1f) && PostMath.RadioReaches(5, -1f) && !PostMath.RadioReaches(3, -1f), "radio: industrial and later");
        C(PostMath.RadioReaches(3, 12f) && !PostMath.RadioReaches(3, PostMath.MastReach + 1f), "or a town within a Signal Line mast's reach");
    }

    private static void Relay(Action<bool, string> C)
    {
        var opts = new List<RelayOption>
        {
            new RelayOption { id = 1, toRelay = 3f, onward = 6f, goodTerms = true },
            new RelayOption { id = 2, toRelay = 2f, onward = 4f, goodTerms = true },
            new RelayOption { id = 3, toRelay = 1f, onward = 2f, goodTerms = false },
            new RelayOption { id = 4, toRelay = 8f, onward = 1f, goodTerms = true }
        };
        int i = PostMath.Relay(opts, 7f, out float days);
        C(i == 1 && Math.Abs(days - (2f + PostMath.RelayDays + 4f)) < 1e-4f, $"the relay with the fewest days in all, a day at the relay ({i}, {days})");
        opts[1] = new RelayOption { id = 2, toRelay = 2f, onward = 4f, goodTerms = false };
        C(PostMath.Relay(opts, 7f, out _) == 0, "only a people on good terms relays");
        C(PostMath.Relay(new List<RelayOption> { new RelayOption { toRelay = 8f, onward = 1f, goodTerms = true } }, 7f, out _) < 0, "the relay's town must be within range itself");
        C(PostMath.Relay(new List<RelayOption> { new RelayOption { toRelay = 2f, onward = 9f, goodTerms = true } }, 7f, out _) < 0, "and its way on within the same range");
        C(PostMath.Relay(new List<RelayOption> { new RelayOption { toRelay = -1f, onward = 2f, goodTerms = true } }, 7f, out _) < 0, "a relay cut off by the land can't");
        C(PostMath.Relay(new List<RelayOption>(), 7f, out float none) < 0 && none < 0f && PostMath.Relay(null, 7f, out _) < 0, "no relay: none");
        C(PostMath.Relay(opts, -1f, out _) < 0, "no range (shuttles): no relay needed");
    }

    // ---------------- The regular post ----------------

    private static void Post(Action<bool, string> C)
    {
        C(PostMath.PostAge(WorldAge.Tribal, WorldAge.Industrial) == WorldAge.Tribal && PostMath.PostAge(WorldAge.Spacer, WorldAge.Medieval) == WorldAge.Medieval
          && PostMath.PostAge(WorldAge.Industrial, WorldAge.Industrial) == WorldAge.Industrial, "the post's age: what the colony and its best neighbour share");
        C(!PostMath.HasPost(WorldAge.Tribal) && PostMath.HasPost(WorldAge.Medieval) && PostMath.HasPost(WorldAge.Spacer), "no post for a tribe: only travellers, camps and its own rider");
        PostMath.PostDays(WorldAge.Medieval, out float m0, out float m1);
        PostMath.PostDays(WorldAge.Industrial, out float i0, out float i1);
        PostMath.PostDays(WorldAge.Spacer, out float s0, out float s1);
        C(m0 == 7f && m1 == 14f && i0 == 3f && i1 == 7f && s0 == 2f && s1 == 4f, "a rider every 7 to 14 days, a mail run every 3 to 7, a shuttle every 2 to 4");
        C(PostMath.NextPostDays(WorldAge.Tribal, PostPace.Usual, 0.5f) < 0f, "a tribe's next post never comes");
        foreach (WorldAge a in new[] { WorldAge.Medieval, WorldAge.Industrial, WorldAge.Spacer })
        {
            PostMath.PostDays(a, out float lo, out float hi);
            C(PostMath.NextPostDays(a, PostPace.Usual, 0f) == lo && PostMath.NextPostDays(a, PostPace.Usual, 1f) == hi && PostMath.NextPostDays(a, PostPace.Usual, 2f) == hi,
                $"{a}: the next post within its days");
            C(PostMath.NextPostDays(a, PostPace.Seldom, 0.5f) > PostMath.NextPostDays(a, PostPace.Usual, 0.5f)
              && PostMath.NextPostDays(a, PostPace.Often, 0.5f) < PostMath.NextPostDays(a, PostPace.Usual, 0.5f), $"{a}: the pace setting, seldom and often");
        }
        C(PostMath.PostServes(WorldAge.Medieval, false) && !PostMath.PostServes(WorldAge.Tribal, false) && !PostMath.PostServes(WorldAge.Industrial, true),
            "the post serves peoples on the road (not a tribe's camps), at peace");
        C(PostMath.ByDay(7) && PostMath.ByDay(17) && !PostMath.ByDay(18) && !PostMath.ByDay(3), "the post comes by day");
    }

    // ---------------- Mail for the colony ----------------

    private static void Mail(Action<bool, string> C)
    {
        C(PostMath.MessengerWaitDays(PostUrgency.Urgent, false) < PostMath.MessengerWaitDays(PostUrgency.Business, false)
          && PostMath.MessengerWaitDays(PostUrgency.Business, false) < PostMath.MessengerWaitDays(PostUrgency.Ordinary, false),
            "urgent news sooner than business, ordinary letters can wait");
        C(PostMath.MessengerWaitDays(PostUrgency.Urgent, false) <= 0.5f, "urgent: half a day at most before a messenger brings it");
        C(PostMath.MessengerWaitDays(PostUrgency.Ordinary, true) > PostMath.MessengerWaitDays(PostUrgency.Ordinary, false), "with a regular post, ordinary letters wait for it");
        C(PostMath.UrgencyOf(LetterPurpose.Help) == PostUrgency.Urgent && PostMath.UrgencyOf(LetterPurpose.Truce) == PostUrgency.Urgent
          && PostMath.UrgencyOf(LetterPurpose.Greetings) == PostUrgency.Ordinary && PostMath.UrgencyOf(LetterPurpose.Pact) == PostUrgency.Business,
            "an answer's urgency by what was asked");
        foreach (LetterPurpose p in Enum.GetValues(typeof(LetterPurpose)))
            C(PostMath.MessengerWaitDays(PostMath.UrgencyOf(p), true) <= 12f, $"an answer about {p} never waits more than twelve days");
        C(PostMath.Brings(PostCarrier.PostRider, WayWhy.No, true) && !PostMath.Brings(PostCarrier.PostRider, WayWhy.Cone, false), "the post brings what comes by the post");
        C(PostMath.Brings(PostCarrier.Traders, WayWhy.Cone, false) && !PostMath.Brings(PostCarrier.Traders, WayWhy.No, true), "anyone else brings what comes from their way");
        C(PostMath.MaxOpen >= 6 && PostMath.MaxOpen <= 20 && PostMath.MaxIncoming >= PostMath.MaxOpen, "a sane cap on letters open and mail waiting");
        C(PostMath.Spoken(WorldAge.Tribal) && !PostMath.Spoken(WorldAge.Medieval), "a tribe gives spoken messages");
        C(PostMath.CanWrite(true, true, true, false, false) && !PostMath.CanWrite(true, true, false, true, false) && PostMath.CanWrite(true, true, false, true, true)
          && !PostMath.CanWrite(false, true, true, true, false) && !PostMath.CanWrite(true, false, true, true, false), "who writes: grown, free, reads (or speaks, in a tribe)");
    }

    // ---------------- The reach line ----------------

    private static void Reach(Action<bool, string> C)
    {
        C(PostMath.RoughDays(0.3f) == 1 && PostMath.RoughDays(1.4f) == 1 && PostMath.RoughDays(1.5f) == 2 && PostMath.RoughDays(2.4f) == 2 && PostMath.RoughDays(6.6f) == 7,
            "rough days, never a countdown");
        C(PostMath.Lead(ReachBand.Radio, false, true, true, true) == PostMath.ReachBy.Radio, "radio first");
        C(PostMath.Lead(ReachBand.Near, false, true, true, true) == PostMath.ReachBy.Bearer, "then someone of theirs at the colony");
        C(PostMath.Lead(ReachBand.Near, true, false, false, true) == PostMath.ReachBy.GoBetween, "a people at war: a go-between");
        C(PostMath.Lead(ReachBand.Cut, false, false, true, true) == PostMath.ReachBy.OnlyGoing, "out of reach: only someone going there");
        C(PostMath.Lead(ReachBand.Near, false, false, true, true) == PostMath.ReachBy.Camped, "within reach: their people on the map now");
        C(PostMath.Lead(ReachBand.Far, false, false, false, true) == PostMath.ReachBy.Relay, "far: by relay");
        C(PostMath.Lead(ReachBand.Near, false, false, false, true) == PostMath.ReachBy.Post && PostMath.Lead(ReachBand.Near, false, false, false, false) == PostMath.ReachBy.Travellers,
            "within reach: the post, else whoever goes that way");
    }

    // ---------------- The seams ----------------

    private static void Seams(string root, Func<string, string> Code, Action<bool, string> C)
    {
        string math = Code("Post/PostMath.cs");
        C(!Regex.IsMatch(math, @"using (RimWorld|Verse|UnityEngine)"), "the rules use no game types");
        var post = Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil", "Post"), "*.cs").Select(File.ReadAllText).ToList();
        C(post.All(t => !t.Contains("Messages.Message(") && !t.Contains("Find.LetterStack.ReceiveLetter(")), "the post tells the player only through Notify and Tell");
        C(post.All(t => !Regex.IsMatch(t, @"\bnew HarmonyPatch|\[HarmonyPatch")), "no patches: the watch reads the game's own lords and jobs");
        var keys = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml")
            .SelectMany(f => System.Xml.Linq.XDocument.Load(f).Root!.Elements()).Select(e => e.Name.LocalName));
        foreach (var t in post)
            foreach (Match m in Regex.Matches(t, "\"(CC_[A-Za-z0-9_]+)\""))
            {
                string k = m.Groups[1].Value;
                if (k.EndsWith("_")) continue;
                C(keys.Contains(k), "a word of the post is written: " + k);
            }
        foreach (PostCarrier c in Enum.GetValues(typeof(PostCarrier))) C(keys.Contains("CC_Mail_Carrier_" + c), "who carries, in words: " + c);
        foreach (var a in new[] { "Medieval", "Industrial", "Spacer" })
            C(keys.Contains("CC_Mail_Name_" + a) && keys.Contains("CC_Mail_Msg_Coming_" + a), "the post by its age, in words: " + a);
        foreach (PostPace p in Enum.GetValues(typeof(PostPace)))
            C(keys.Contains("CC_PostPace_" + p) && keys.Contains("CC_PostPace_" + p + "_Tip") && keys.Contains("CC_SetNote_PostPace_" + p), "the post's pace in the settings: " + p);
        // The pool and its moments.
        string pool = File.ReadAllText(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Post.xml"));
        foreach (var moment in new[] { "take", "bring", "drop", "rider", "rideon", "speak", "heard", "setout", "home" })
        {
            C(post.Any(t => t.Contains("\"" + moment + "\"")) && pool.Contains("[" + moment), "a line for the moment: " + moment);
        }
        string defs = File.ReadAllText(Path.Combine(root, "Defs", "Misc", "Rimfolk_Post.xml"));
        foreach (var d in new[] { "Rimfolk_Mailbag", "Rimfolk_PostTake", "Rimfolk_PostDrop", "Rimfolk_PostSpeak", "Rimfolk_RiderSetOut" })
            C(defs.Contains("<defName>" + d + "</defName>"), "the def is there: " + d);
        C(defs.Contains("ColonyCouncil.Building_Mailbag") && defs.Contains("<designationCategory>Misc</designationCategory>"), "the mailbag is a spot the colony places");
        // Letters: into the outbox when written, words-only answers into the post, goods by courier, a go-between for a people at war, old saves.
        string letters = Code("World/NeighbourLetters.cs");
        C(letters.Contains("Outbox.Bring(l, p)") && letters.Contains("PostMath.Written(PostBook.On, l.hostile || NotableBook.Hostile(n))"),
            "a written letter goes in the outbox (its writer carries it there), unless to a people at war");
        C(letters.Contains("MailIn.Add(l.map") && letters.Contains("things.Count == 0"), "an answer of words only waits in the post for a carrier");
        C(letters.Contains("Couriers.MailAnswer(l.map"), "an answer that sends goods still comes with its messenger");
        C(letters.Contains("An older save's letter whose messenger never came") && letters.Contains("Outbox.Put(l, null)"), "an older save's letter waiting for a messenger goes in the outbox");
        C(letters.Contains("PostReach.Radio(n.faction"), "a call reaches peoples with radio, or a Signal Line mast near");
        C(letters.Contains("PostBook.On ? PostMath.MaxOpen : NotableMath.MaxOpen"), "the three under way go with the post; a sane cap stays");
        C(letters.Contains("BookUtility.CanReadEver"), "writing takes someone who can read and write");
        string notables = Code("World/Notables.cs");
        C(notables.Contains("MailWatch.Rare(now)") && notables.Contains("MailIn.Hourly(now)") && notables.Contains("PostRider.Hourly(now)") && notables.Contains("OwnRiders.Hourly(now)"),
            "the post rides on the neighbours' passes (no tick of its own)");
        C(Code("Diplomacy/EnvoysAbroad.cs").Contains("MailIn.FromAbroad") && Code("Diplomacy/WardsAbroad.cs").Contains("MailIn.FromAbroad")
          && Code("Colonies/TownsAfter.cs").Contains("MailIn.FromAbroad"), "the envoy's, the ward's and a town's letters come by the post");
        C(Code("UI/RegionMapView.cs").Contains("PostRing.Ring(") && Code("UI/RegionMapView.cs").Contains("PostRing.Tip("), "the region map shows the range as a soft ring and the reach line");
        string settings = Code("CouncilSettings.cs");
        C(settings.Contains("public bool post = true") && settings.Contains("public PostPace postPace = PostPace.Usual") && Code("UI/SettingsCatalog.cs").Contains("Toggle(\"post\""),
            "the settings: the post, and its pace");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "ImmersionTests.cs")).Contains("\"Rimfolk_Pool_PostSays\""), "the post's lines are held to one sentence");
        C(File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")).Contains("Post/PostBook.cs:incoming"), "the post's book is in the save's estimate");
    }
}
