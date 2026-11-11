using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The colony's own rider's supplies for the road (the player, 10 November 2026: "sending a colonist off to send a letter
/// makes them just go off map without any supplies or preparation. supply selection should be part of the initial
/// decision"; Post/RiderPackMath.cs): the trip the supplies are for (there, the wait at the town, back), the food by the
/// journey planner's rules, a little medicine, the beast's feed for the days it can't graze, how far it reaches, when the
/// send is greyed (short of the road unless sent short knowingly, too heavy always), what is said as they set out, and the
/// seams: the letter's window and the mailbag choose the supplies, the rider gathers them in the game's caravan forming,
/// eats from them on the road, and brings what is left home to the stores.
/// </summary>
public static class RiderPackTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void C(bool ok, string what)
        {
            cases++;
            check(ok, "the rider's supplies: " + what);
        }

        Trip(C);
        Wants(C);
        Verdicts(C);
        Packed(C);
        Words(C);
        Seams(root, C);
        return cases;
    }

    private static bool Near(float a, float b, float eps = 0.001f) => Math.Abs(a - b) <= eps;

    // ---------------- The trip ----------------

    private static void Trip(Action<bool, string> C)
    {
        C(Near(RiderPackMath.TownDays, NotableMath.ReplyDays(1f) * 0.5f) && Near(RiderPackMath.TownDays, 0.625f),
            "packed for the longest wait at the town for the answer (half a reply's days at most)");
        C(Near(RiderPackMath.TownWaitDays(0f), 0.125f) && Near(RiderPackMath.TownWaitDays(1f), RiderPackMath.TownDays) && RiderPackMath.TownWaitDays(0.5f) < RiderPackMath.TownDays,
            "the rider's own wait at the town is the same rule the supplies are packed for");
        C(Near(RiderPackMath.TripDays(3f), 6.625f), "three days there: there, the wait, and as long back");
        C(RiderPackMath.TripDays(0f) == 0f && RiderPackMath.TripDays(-1f) == 0f, "no road worked out: no trip");
        bool grows = true;
        for (float d = 0.25f; d < 40f; d += 0.25f)
            if (RiderPackMath.TripDays(d + 0.25f) <= RiderPackMath.TripDays(d)) grows = false;
        C(grows, "a longer road is always a longer trip");
    }

    // ---------------- What they want ----------------

    private static void Wants(Action<bool, string> C)
    {
        float trip = RiderPackMath.TripDays(3f);
        C(Near(RiderPackMath.FoodWanted(1.6f, trip), JourneyMath.FoodNutrition(1.6f, trip)) && Near(RiderPackMath.FoodWanted(1.6f, trip), 1.6f * (trip * 1.25f + 1f)),
            "food for the trip by the journey planner's rule: the days, a quarter more and a spare day");
        C(RiderPackMath.FoodWanted(0f, trip) == 0f && RiderPackMath.FoodWanted(1.6f, 0f) == 0f, "nothing for one who doesn't eat, or for no road");
        C(Near(RiderPackMath.FeedNeedDays(10f, true, 0.3f), 3f) && Near(RiderPackMath.FeedNeedDays(10f, false, 0.3f), 10f),
            "a grazer needs feed only for the share without grazing; a beast that doesn't graze, every day");
        C(Near(RiderPackMath.FeedNeedDays(10f, true, 1.5f), 10f) && RiderPackMath.FeedNeedDays(10f, true, -1f) == 0f && RiderPackMath.FeedNeedDays(0f, false, 1f) == 0f,
            "the share without grazing kept between none and all");
        C(Near(RiderPackMath.FeedWanted(2f, 10f, true, 0.5f), JourneyMath.FoodNutrition(2f, 5f)), "the beast's feed with the same margin as the food");
        C(RiderPackMath.FeedWanted(2f, 10f, true, 0f) == 0f, "a grazer on good land all the way needs none");
        C(RiderPackMath.MedicineWanted(2f) == 1 && RiderPackMath.MedicineWanted(4f) == 1 && RiderPackMath.MedicineWanted(6f) == 2 && RiderPackMath.MedicineWanted(14f) == 3,
            "a little medicine: one, and one more for every eight days on the road");
        C(RiderPackMath.MedicineWanted(0f) == 0, "none without a road");
    }

    // ---------------- How it stands ----------------

    private static RiderPackFacts Rider(float oneWay = 3f, float foodDays = -1f)
    {
        var f = new RiderPackFacts { oneWayDays = oneWay, perDay = 1.6f, foodToHand = 200f, load = 20f, room = 35f, medicine = 1 };
        f.food = foodDays < 0f ? RiderPackMath.FoodWanted(f.perDay, RiderPackMath.TripDays(oneWay)) : foodDays * f.perDay;
        return f;
    }

    private static void Verdicts(Action<bool, string> C)
    {
        var enough = RiderPackMath.Judge(Rider());
        C(enough.food == RiderPackCover.Enough && !enough.Short && !enough.over && RiderPackMath.MaySend(enough, false) && RiderPackMath.WhyKey(enough, false) == null,
            "food for the trip with the margin: enough, and the send is open");
        C(Near(enough.tripDays, 6.625f) && Near(enough.foodDays, RiderPackMath.FoodWanted(1.6f, 6.625f) / 1.6f), "the verdict says the trip and how many days the food lasts");

        var noMargin = RiderPackMath.Judge(Rider(3f, 7f));
        C(noMargin.food == RiderPackCover.NoMargin && !noMargin.Short && RiderPackMath.MaySend(noMargin, false), "the road covered but nothing to spare: still sent, with a word");

        var shortOf = RiderPackMath.Judge(Rider(3f, 4f));
        C(shortOf.food == RiderPackCover.Short && shortOf.Short && !shortOf.storesShortFood, "food for four of six and a half days: short, though the stores have more");
        C(!RiderPackMath.MaySend(shortOf, false) && RiderPackMath.WhyKey(shortOf, false) == "CC_RiderPack_Why_Short", "short of the road: greyed with why (pack more)");
        C(RiderPackMath.MaySend(shortOf, true) && RiderPackMath.WhyKey(shortOf, true) == null, "or sent short, knowingly");

        var bare = Rider(3f, 4f);
        bare.foodToHand = bare.food;
        var stores = RiderPackMath.Judge(bare);
        C(stores.storesShortFood && RiderPackMath.WhyKey(stores, false) == "CC_RiderPack_Why_Stores" && RiderPackMath.MaySend(stores, true),
            "the stores can't cover the road: greyed with that reason, or sent short knowingly");

        var none = Rider(3f, 0f);
        none.food = 0f;
        var nothing = RiderPackMath.Judge(none);
        C(nothing.food == RiderPackCover.None && nothing.Short && !RiderPackMath.MaySend(nothing, false) && RiderPackMath.MaySend(nothing, true), "no food at all: short too");

        var heavy = Rider();
        heavy.load = 40f;
        var over = RiderPackMath.Judge(heavy);
        C(over.over && !RiderPackMath.MaySend(over, false) && !RiderPackMath.MaySend(over, true) && RiderPackMath.WhyKey(over, true) == "CC_RiderPack_Why_Over",
            "more than they can carry: never sent (a caravan that heavy can't move), knowingly or not");
        var noRoom = Rider();
        noRoom.room = 0f;
        C(RiderPackMath.Judge(noRoom).over, "nothing can be carried by one who can't carry");

        var grazer = Rider();
        grazer.beast = true;
        grazer.grazes = true;
        grazer.beastPerDay = 3f;
        grazer.noGrazing = 0f;
        var summer = RiderPackMath.Judge(grazer);
        C(summer.feedNeedDays == 0f && summer.feed == RiderPackCover.Enough && !summer.Short && RiderPackMath.FeedKey(summer.feed, summer.feedNeedDays) == "CC_RiderPack_Feed_Graze",
            "a horse on good land grazes all the way: no feed wanted");
        var winter = grazer;
        winter.noGrazing = 1f;
        winter.feedToHand = 500f;
        var hungryBeast = RiderPackMath.Judge(winter);
        C(hungryBeast.feed == RiderPackCover.None && hungryBeast.Short && RiderPackMath.WhyKey(hungryBeast, false) == "CC_RiderPack_Why_Feed" && !hungryBeast.storesShortFeed,
            "in winter with no feed packed: the beast would go hungry, greyed with why");
        winter.feed = RiderPackMath.FeedWanted(3f, RiderPackMath.TripDays(3f), true, 1f);
        var fed = RiderPackMath.Judge(winter);
        C(fed.feed == RiderPackCover.Enough && RiderPackMath.MaySend(fed, false), "its feed packed: sent");
        winter.feedToHand = 1f;
        winter.feed = 1f;
        C(RiderPackMath.Judge(winter).storesShortFeed, "the stores short of feed: said");

        var noMeds = Rider();
        noMeds.medicine = 0;
        var nm = RiderPackMath.Judge(noMeds);
        C(nm.noMedicine && RiderPackMath.MaySend(nm, false), "no medicine to spare is a word, not a reason to hold them");

        var eatsNothing = Rider();
        eatsNothing.perDay = 0f;
        eatsNothing.food = 0f;
        C(RiderPackMath.Judge(eatsNothing).food == RiderPackCover.Enough, "one who never eats needs no food");

        // Every mix: too heavy never goes; short goes only knowingly; neither always goes.
        var rand = new Random(17);
        bool rule = true;
        for (int i = 0; i < 4000; i++)
        {
            var f = new RiderPackFacts
            {
                oneWayDays = (float)rand.NextDouble() * 12f, perDay = rand.Next(4) == 0 ? 0f : 0.5f + (float)rand.NextDouble() * 2f, food = (float)rand.NextDouble() * 40f,
                foodToHand = (float)rand.NextDouble() * 60f, beast = rand.Next(2) == 0, grazes = rand.Next(2) == 0, beastPerDay = (float)rand.NextDouble() * 4f,
                noGrazing = (float)rand.NextDouble(), feed = (float)rand.NextDouble() * 60f, feedToHand = (float)rand.NextDouble() * 80f, load = (float)rand.NextDouble() * 60f,
                room = rand.Next(6) == 0 ? 0f : 35f + (float)rand.NextDouble() * 60f, medicine = rand.Next(3)
            };
            var v = RiderPackMath.Judge(f);
            bool knowingly = rand.Next(2) == 0;
            bool may = RiderPackMath.MaySend(v, knowingly);
            string why = RiderPackMath.WhyKey(v, knowingly);
            if (v.over && may) rule = false;
            if (v.Short && !knowingly && may) rule = false;
            if (!v.over && !v.Short && !may) rule = false;
            if (may != (why == null)) rule = false;
        }
        C(rule, "of every mix: too heavy never goes, short only knowingly, otherwise always, and a reason whenever it is greyed");
    }

    // ---------------- The default supplies ----------------

    private static void Packed(Action<bool, string> C)
    {
        float trip = RiderPackMath.TripDays(4f);
        float want = RiderPackMath.FoodWanted(1.6f, trip);
        var store = new List<JourneyMath.FoodOption>
        {
            new JourneyMath.FoodOption { nutrition = 0.9f, mass = 0.5f, rotDays = 4f, preferability = 8, travel = 0, stock = 40 },
            new JourneyMath.FoodOption { nutrition = 0.9f, mass = 0.3f, rotDays = float.PositiveInfinity, preferability = 7, travel = 2, stock = 30 },
            new JourneyMath.FoodOption { nutrition = 0.05f, mass = 0.025f, rotDays = 70f, preferability = 7, travel = 1, stock = 200 }
        };
        var packed = JourneyMath.PackFood(store, trip, want);
        float got = packed.Sum(p => p.units * store[p.food].nutrition);
        var f = Rider(4f);
        f.food = got;
        C(got >= want - 0.01f && RiderPackMath.Judge(f).food == RiderPackCover.Enough, "the default food from well-stocked stores covers the trip with the margin");
        C(packed.Count > 0 && packed[0].food == 1, "travel food that never spoils first (a survival meal before a fine meal that rots on the way)");
        C(packed.All(p => p.food != 0), "food that would rot on the road is left while better keeping food is enough");

        var thin = new List<JourneyMath.FoodOption> { new JourneyMath.FoodOption { nutrition = 0.9f, mass = 0.3f, rotDays = float.PositiveInfinity, preferability = 7, travel = 2, stock = 4 } };
        var fewMeals = JourneyMath.PackFood(thin, trip, want);
        var g = Rider(4f);
        g.food = fewMeals.Sum(p => p.units * thin[p.food].nutrition);
        g.foodToHand = 4 * 0.9f;
        var v = RiderPackMath.Judge(g);
        C(fewMeals.Sum(p => p.units) == 4 && v.Short && v.storesShortFood && RiderPackMath.WhyKey(v, false) == "CC_RiderPack_Why_Stores",
            "stores with four meals: all four packed, and the send says the stores can't cover the road");
        C(JourneyMath.MedicineTaken(RiderPackMath.MedicineWanted(trip), 1, 3) == 1 && JourneyMath.MedicineTaken(RiderPackMath.MedicineWanted(trip), 2, 3) == 1,
            "never more than half the colony's medicine (one of two)");
    }

    // ---------------- Words ----------------

    private static void Words(Action<bool, string> C)
    {
        C(RiderPackMath.OutKey(10f, 6f) == "CC_Mail_Msg_RiderOutFed" && RiderPackMath.OutKey(6f, 6f) == "CC_Mail_Msg_RiderOutFed", "fed for the road: said with the days");
        C(RiderPackMath.OutKey(3f, 6f) == "CC_Mail_Msg_RiderOutShort" && RiderPackMath.OutKey(0f, 6f) == "CC_Mail_Msg_RiderOutNoFood", "short, or with nothing: they will go hungry, said");
        C(RiderPackMath.OutKey(3f, 0f) == "CC_Mail_Msg_RiderOut", "the road unknown: the plain message");
        C(RiderPackMath.Days(2.5f) == "2.5" && RiderPackMath.Days(12.3f) == "12" && RiderPackMath.Days(float.PositiveInfinity) == "99+" && RiderPackMath.Days(-1f) == "0",
            "days as the planner writes them");
        foreach (var c in new[] { RiderPackCover.Enough, RiderPackCover.NoMargin, RiderPackCover.Short, RiderPackCover.None })
            C(RiderPackMath.FoodKey(c).StartsWith("CC_RiderPack_Food_") && RiderPackMath.FeedKey(c, 1f).StartsWith("CC_RiderPack_Feed_"), "a line for the food and the feed: " + c);
        C(RiderPackMath.Step(3, 1, 10) == 4 && RiderPackMath.Step(10, 1, 10) == 10 && RiderPackMath.Step(12, 1, 10) == 12 && RiderPackMath.Step(1, -10, 10) == 0,
            "a line's count stepped: not past what is to hand, never below none");
    }

    // ---------------- The seams ----------------

    private static void Seams(string root, Action<bool, string> C)
    {
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => Regex.Replace(File.ReadAllText(Path.Combine(src, rel)), @"//[^\n]*", "");
        string riders = Code("Post/OwnRiders.cs"), packs = Code("Post/RiderPacks.cs"), window = Code("Post/RiderPackWindow.cs"), jobs = Code("Post/PostJobs.cs");
        string book = Code("Post/PostBook.cs"), watch = Code("Post/MailWatch.cs"), bag = Code("Post/Building_Mailbag.cs"), dlg = Code("World/NeighboursPanel.cs");
        string pick = Code("UI/JourneyPick.cs"), math = Code("Post/RiderPackMath.cs");

        // Chosen where the letter is sent.
        C(Regex.IsMatch(dlg, @"RiderPacks\.ForNext\(byOwnRider \? riderPack\.Pack : null\);\s*try\s*\{\s*if \(NeighbourLetters\.Send\([^;]*byOwnRider \? rider : null\)[\s\S]*?finally\s*\{\s*RiderPacks\.ForNext\(null\);"),
            "the letter's window hands its supplies to the letter it sends, and lets them go after");
        C(dlg.Contains("riderPack.Draw(") && dlg.Contains("packing ? riderPack.Why : null"), "the letter's window shows the supplies and greys the send with their why");
        C(Regex.IsMatch(riders, @"public static void Send\(NeighbourLetter l, Pawn rider, RiderPack pack = null\)[\s\S]*?pack \?\?= RiderPacks\.TakeNext\(\);[\s\S]*?RiderPacks\.Keep\(l\.id, pack\);"),
            "the rider's send keeps the supplies with the letter");
        C(bag.Contains("Dialog_RiderPack.ForLetter(map, l, p)") && !bag.Contains("OwnRiders.Send(l, p)"), "the mailbag's \"Send with our rider\" chooses the supplies before it sends");
        C(Regex.IsMatch(window, @"bool ok = model\.MaySend;[\s\S]*?ButtonKind\.Primary, ok, ok \? null : why"), "the supplies window's send is greyed with why");
        C(Regex.IsMatch(window, @"UiKit\.Toggle\(box, ""CC_RiderPack_Knowingly""") && Regex.IsMatch(packs, @"UiKit\.Toggle\(box, ""CC_RiderPack_Knowingly"""),
            "both let them go short, knowingly");
        C(window.Contains("new Dialog_JourneyPick(plan, model.Changed,") && window.Contains("() => Find.WindowStack.IsOpen(this), false)"),
            "anything else from the stores by the journey planner's own picker, without the stalls");
        C(pick.Contains("Func<bool> open = null, bool withStalls = true") && pick.Contains("!(open?.Invoke() ?? Find.WindowStack.WindowOfType<Dialog_JourneyPlan>() != null)"),
            "the picker stays open beside whichever window it serves");

        // By the journey planner's rules.
        C(packs.Contains("RiderPackMath.FoodWanted(JourneyPack.NutritionPerDay(rider), tripDays)") && packs.Contains("JourneyMath.PackFood(foods, tripDays, need)"),
            "the food by the journey planner's packing, for the rider's own appetite");
        C(Regex.IsMatch(packs, @"public static bool Eats\(Pawn p, ThingDef d\)[\s\S]*?RoadFood\.RoadEdible\(d, p\)[\s\S]*?RoadFood\.OnTheRoad\(p\)[\s\S]*?Beliefs\.FitForStock\(p, d\)"),
            "only food they'd eat on the road: the caravan's rules, the food policy for the road, their beliefs");
        C(packs.Contains("JourneyPack.Drugs(map, new List<Pawn> { rider }, tripDays, carried, off)"), "drugs for an addiction, by the planner's rule");
        C(packs.Contains("RiderPackMath.MedicineWanted(tripDays)") && packs.Contains("JourneyMath.MedicineTaken("), "a little medicine, never more than half the stores");
        C(packs.Contains("RiderPackMath.FeedWanted(JourneyPack.NutritionPerDay(mount), tripDays, grazes, noGrazing)"), "the beast's feed by the game's own hunger and the land's grazing");
        C(Regex.IsMatch(packs, @"JourneyPack\.Route\(map, town\.Tile, Going, mass, Find\.TickManager\.TicksAbs, true, 0f\)") && packs.Contains("pack.tripDays = RiderPackMath.TripDays(oneWayDays);"),
            "the road's days by the game's own caravan reckoning for this rider and beast");
        C(packs.Contains("f.room = Going.Sum(p => MassUtility.Capacity(p));") && packs.Contains("MassUtility.GearAndInventoryMass(p)"), "the load against what the rider and beast can carry, by the game's rules");

        // Gathered before setting out.
        C(Regex.IsMatch(jobs, @"OwnRiders\.TookLetter\(pawn\)[\s\S]*?OwnRiders\.Gather\(pawn\)[\s\S]*?Toils_Goto\.GotoCell\(TargetIndex\.B, PathEndMode\.OnCell\)[\s\S]*?OwnRiders\.AtEdge\(pawn\)"),
            "the letter taken, then the supplies gathered; the walk to the edge only where gathering can't be");
        C(Regex.IsMatch(jobs, @"class JobDriver_RiderSetOut[\s\S]*?GotoCell\(TargetIndex\.A[\s\S]*?Wait\(90[\s\S]*?RiderTook"), "the first toils keep their order for a rider saved on the way");
        C(Regex.IsMatch(riders, @"public static bool Gather\(Pawn p\)[\s\S]*?JourneyPack\.Exit\(plan\)[\s\S]*?JourneyPack\.Gate\(plan, plan\.exit\)[\s\S]*?JourneyPack\.Missing\(plan, going, shortOf\)[\s\S]*?CaravanFormingUtility\.StartFormingCaravan\(going, new List<Pawn>\(\), Faction\.OfPlayer, goods, plan\.gate, plan\.exit, start, town\.Tile\)"),
            "gathered as the journey planner's caravans gather: the way toward the town, the gate, what isn't in their packs yet, the game's caravan forming");
        C(Regex.IsMatch(riders, @"case RideStage\.SettingOut:[\s\S]*?p\.GetCaravan\(\)[\s\S]*?Departed\(ride, p, c, now\)[\s\S]*?!Forming\(p\)"),
            "out on the road once the game sends their caravan; called off only when neither setting out nor gathering");
        C(watch.Contains("OwnRiders.Watching") && watch.Contains("OwnRiders.Rare(now);"), "watched every few seconds while they get ready and as they come home");

        // On the road and home again.
        C(riders.Contains("RiderPackMath.TownWaitDays(Rand.Value)") && !riders.Contains("NotableMath.ReplyDays(Rand.Value) * 0.5f"), "their wait at the town is the rule the supplies are packed for");
        C(Regex.IsMatch(riders, @"private static void OutMessage[\s\S]*?c\.DaysWorthOfFood\.days[\s\S]*?RiderPackMath\.OutKey\(food, trip\)"),
            "as they set out, the game's own days of food in their caravan against the road, said");
        C(Regex.IsMatch(riders, @"private static void Home\([\s\S]*?Unpack\(ride, p\);") && Regex.IsMatch(riders, @"private static void Unpack[\s\S]*?RiderPacks\.Left\(pack, p, mount\)[\s\S]*?UnloadEverything = true[\s\S]*?CC_Mail_Msg_PackLeft"),
            "home again, what is left goes back into the stores, said");
        C(Regex.IsMatch(riders, @"private static void End\([\s\S]*?RiderPacks\.Drop\(ride\.letterId\)") && Regex.IsMatch(riders, @"private static bool Fail\([\s\S]*?RiderPacks\.Drop\(l\.id\)"),
            "the supplies let go when the rider is lost or the letter goes back in the bag");
        C(Regex.IsMatch(riders, @"if \(RiderPacks\.For\(l\.id\) == null\) RiderPacks\.Keep\(l\.id, RiderPacks\.DefaultFor\(map, p, mount, town\)\);"),
            "a rider sent without choosing (an older save) takes the planner's supplies");

        // Saved.
        C(book.Contains("Scribe_Collections.Look(ref packs, \"packs\", LookMode.Deep);") && packs.Contains("Scribe_Collections.Look(ref lines, \"lines\", LookMode.Deep);"), "saved with the post's book");
        C(packs.Contains("kept.lines.RemoveAll(l => !l.Taken);") && packs.Contains("public static void Prune()"), "only the lines taken are kept, and only while the letter goes with the rider");
        string est = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(est.Contains("\"Post/PostBook.cs:packs\"") && est.Contains("\"Post/RiderPacks.cs:lines\""), "modelled in the save estimate");

        // Words.
        var keys = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var e in XDocument.Load(f).Root!.Elements())
                keys[e.Name.LocalName] = e.Value;
        var used = Regex.Matches(math + packs + window + riders, @"""((?:CC_RiderPack|CC_Mail_Msg_Rider|CC_Mail_Msg_Pack)\w*)""").Select(m => m.Groups[1].Value).Distinct().ToList();
        C(used.Count >= 30, $"the words found ({used.Count})");
        foreach (var k in used)
            C(keys.TryGetValue(k, out var text) && !text.Contains("!") && !text.Contains("—"), $"{k} is there, plain (no exclamation marks, no em dashes)");
        C(FramedWindowsMath.TryKey("RiderPack", out _), "the supplies window has its frame key");
    }
}
