using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Carriers on the map (the player, 10 November 2026: "the couriers that bring letters and material take an awkward
/// routine around my base before delivering the message a bit away from the base. if anything they should always walk to
/// the mailbag directly, drop/pick up the message or items. for the message they unmount and then they walk off map without
/// remounting ... the courier and mount looks always exactly the same"; Couriers/CarrierMath.cs): where they do their
/// business, where each stands and where goods are set down, the most direct way in, who a people sends and on what beast
/// (every one of Rimfolk's peoples, the old rule beside the new), getting back in the saddle, and the seams.
/// </summary>
public static class CarrierTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void C(bool ok, string what)
        {
            cases++;
            check(ok, "carriers: " + what);
        }

        Spots(C);
        Entries(C);
        Kinds(C);
        Mounts(C);
        Remounts(C);
        Peoples(root, C);
        Seams(root, C);
        return cases;
    }

    // ---------------- Where ----------------

    private static void Spots(Action<bool, string> C)
    {
        C(CarrierMath.SpotOf(true, true, true) == CarrierSpot.Mailbag && CarrierMath.SpotOf(true, false, false) == CarrierSpot.Mailbag,
            "a mailbag placed: they go to the mailbag, whatever else there is");
        C(CarrierMath.SpotOf(false, true, true) == CarrierSpot.Gate, "no mailbag: the gate");
        C(CarrierMath.SpotOf(false, false, true) == CarrierSpot.TradeSpot, "no mailbag, no gate: where traders leave goods");
        C(CarrierMath.SpotOf(false, false, false) == CarrierSpot.None, "nowhere at all: nobody comes");

        var seen = new HashSet<(int, int)>();
        int lastRing = 0;
        bool ordered = true, neverCentre = true;
        for (int i = 0; i < CarrierMath.RingCount; i++)
        {
            CarrierMath.RingOffset(i, out int dx, out int dz);
            int ring = Math.Max(Math.Abs(dx), Math.Abs(dz));
            if (ring < lastRing) ordered = false;
            lastRing = ring;
            if (dx == 0 && dz == 0) neverCentre = false;
            seen.Add((dx, dz));
        }
        C(CarrierMath.RingCount == 48 && seen.Count == 48, "three rings of places round the spot, each once");
        C(neverCentre, "nobody's place is the spot itself, and goods are never set down on the mailbag");
        C(ordered, "the nearest places first: the cells beside the spot, then the next ring");
        var first8 = Enumerable.Range(0, 8).Select(i =>
        {
            CarrierMath.RingOffset(i, out int dx, out int dz);
            return Math.Max(Math.Abs(dx), Math.Abs(dz));
        }).ToList();
        C(first8.All(r => r == 1), "the first eight are the cells beside it: a pile of stacks goes round the bag");
        var sides = Enumerable.Range(0, 4).Select(i =>
        {
            CarrierMath.RingOffset(i, out int dx, out int dz);
            return Math.Abs(dx) + Math.Abs(dz);
        }).ToList();
        C(sides.All(d => d == 1), "the four cells straight beside it before the corners");
        CarrierMath.RingOffset(CarrierMath.RingCount + 3, out int wx, out int wz);
        CarrierMath.RingOffset(3, out int ax, out int az);
        CarrierMath.RingOffset(-1, out int nx, out int nz);
        C(wx == ax && wz == az && (nx != 0 || nz != 0), "past the third ring it goes round again; a bad index is still a place");
        // Three porters, a pack animal and three mounts: seven places, all different, all within two cells.
        var party = Enumerable.Range(0, 7).Select(i =>
        {
            CarrierMath.RingOffset(i, out int dx, out int dz);
            return (dx, dz);
        }).ToList();
        C(party.Distinct().Count() == 7 && party.All(p => Math.Abs(p.dx) <= 1 && Math.Abs(p.dz) <= 1), "a whole party stands beside the bag, each in their own place");
    }

    private static void Entries(Action<bool, string> C)
    {
        C(CarrierMath.BestEntry(new List<(float, bool, bool)> { (90f, false, true), (40f, false, true), (60f, false, true) }) == 1, "the way in nearest the spot");
        C(CarrierMath.BestEntry(new List<(float, bool, bool)> { (90f, false, true), (40f, false, false), (60f, false, true) }) == 2, "one they can't come in by is passed over");
        C(CarrierMath.BestEntry(new List<(float, bool, bool)> { (45f, true, true), (40f, false, true) }) == 0, "a road a little further counts as nearer");
        C(CarrierMath.BestEntry(new List<(float, bool, bool)> { (70f, true, true), (40f, false, true) }) == 1, "but not a road far round: the direct way wins");
        C(CarrierMath.BestEntry(new List<(float, bool, bool)> { (40f, false, true), (40f, false, true) }) == 0, "a tie goes to the first");
        C(CarrierMath.BestEntry(new List<(float, bool, bool)> { (40f, false, false) }) == -1 && CarrierMath.BestEntry(null) == -1, "no way in: none (the game's own then)");
        C(CarrierMath.EntryKey(-5f, false) == 0f && Math.Abs(CarrierMath.EntryKey(60f, true) - 50f) < 0.01f, "the key is the distance, a road's a fifth nearer");
        // A 250-cell map, a mailbag near the west edge of a colony in the middle: every edge cell of the map against the old
        // random way in (any edge): the new one is the nearest there is.
        var bag = (x: 110, z: 125);
        var edge = new List<(int x, int z)>();
        for (int i = 0; i < 250; i++)
        {
            edge.Add((i, 0));
            edge.Add((i, 249));
            edge.Add((0, i));
            edge.Add((249, i));
        }
        float D((int x, int z) c) => (float)Math.Sqrt((c.x - bag.x) * (c.x - bag.x) + (c.z - bag.z) * (c.z - bag.z));
        var ways = edge.Select(c => (D(c), false, true)).ToList();
        int best = CarrierMath.BestEntry(ways);
        float avg = edge.Average(D);
        C(best >= 0 && Math.Abs(D(edge[best]) - edge.Min(D)) < 0.01f && D(edge[best]) < avg,
            $"the way in is the nearest edge ({D(edge[best]):0} cells), not a random one ({avg:0} on average, round the colony)");
    }

    // ---------------- Who comes ----------------

    private static CarrierKindOption K(string name, int source, int group, float w, float power, bool person = true, bool trader = false, bool adult = true,
        bool titled = false, bool leader = false, float commonality = 100f) =>
        new CarrierKindOption
        {
            name = name, source = source, group = group, groupCommonality = commonality, selectionWeight = w, power = power, person = person, trader = trader, adult = adult,
            titled = titled, leader = leader
        };

    private static float W(List<KeyValuePair<string, float>> pool, string name) => pool.Where(kv => kv.Key == name).Sum(kv => kv.Value);

    private static void Kinds(Action<bool, string> C)
    {
        // An outlander union: a caravan's guards (the game's own weights) and its visitors.
        var outlanders = new List<CarrierKindOption>
        {
            K("Villager", 0, 0, 3f, 35f), K("Town_Guard", 0, 0, 10f, 60f), K("Grenadier_Destructive", 0, 0, 1.5f, 110f), K("Mercenary_Slasher", 0, 0, 7f, 140f),
            K("Mercenary_Gunner", 0, 0, 10f, 85f), K("Town_Trader", 0, 0, 1f, 50f, trader: true),
            K("Villager", 1, 1, 10f, 35f), K("Town_Guard", 1, 1, 3f, 60f), K("Villager_Child", 1, 1, 2f, 10f, adult: false), K("Town_Councilman", 1, 1, 1f, 40f, leader: true),
            K("Villager", 2, -1, 1f, 35f)
        };
        var pool = CarrierMath.KindPool(outlanders);
        C(pool.Count == 2 && W(pool, "Villager") > 0f && W(pool, "Town_Guard") > 0f, "outlanders send villagers and town guards: " + string.Join(", ", pool.Select(kv => kv.Key)));
        C(Math.Abs(pool.Sum(kv => kv.Value) - 1f) < 0.001f, "the chances add up to one");
        C(W(pool, "Mercenary_Gunner") == 0f && W(pool, "Grenadier_Destructive") == 0f, "never a heavy trooper");
        C(W(pool, "Town_Trader") == 0f && W(pool, "Villager_Child") == 0f && W(pool, "Town_Councilman") == 0f, "never a trader, a child or a leader");
        // Caravan: villager 3/13, guard 10/13, half; visitors: villager 10/13, guard 3/13, half.
        C(Math.Abs(W(pool, "Villager") - (0.5f * 3f / 13f + 0.5f * 10f / 13f)) < 0.001f, "a caravan's hands and their visitors half and half, each by the game's weights");
        C(pool.Count(kv => kv.Key == "Villager") == 1, "a kind in both counts once, its chances added");
        var onlyBasic = CarrierMath.KindPool(new List<CarrierKindOption> { K("Raider", 0, 0, 5f, 130f, trader: true), K("Plain", 2, -1, 1f, 35f) });
        C(onlyBasic.Count == 1 && onlyBasic[0].Key == "Plain", "nobody fit to send from their groups: their ordinary member");
        var heavy = CarrierMath.KindPool(new List<CarrierKindOption> { K("Knight", 0, 0, 5f, 120f), K("Squire", 0, 0, 5f, 100f), K("Champion", 0, 0, 1f, 250f) });
        C(heavy.Count == 2 && W(heavy, "Champion") == 0f, "a people of warriors only: its lightest, and those near them");
        C(CarrierMath.KindPool(new List<CarrierKindOption> { K("Baron", 1, 0, 5f, 50f, titled: true), K("Mech", 1, 0, 5f, 50f, person: false) }).Count == 0,
            "a noble or a machine is never sent to carry");
        C(CarrierMath.KindPool(null).Count == 0 && CarrierMath.KindPool(new List<CarrierKindOption>()).Count == 0, "no people: nobody");
        var groups = CarrierMath.KindPool(new List<CarrierKindOption> { K("A", 1, 0, 1f, 30f, commonality: 300f), K("B", 1, 1, 1f, 30f, commonality: 100f) });
        C(Math.Abs(W(groups, "A") - 0.75f) < 0.001f, "group against group by the groups' commonality");

        // Drawn: by the chances, the one sent last time half as likely, a single kind always.
        C(CarrierMath.Pick(pool, 0.0) != null && CarrierMath.Pick(pool, 0.999) != null && CarrierMath.Pick(new List<KeyValuePair<string, float>>(), 0.5) == null,
            "a pick for any value, none from an empty pool");
        C(CarrierMath.Pick(new List<KeyValuePair<string, float>> { new KeyValuePair<string, float>("Only", 1f) }, 0.7, "Only") == "Only", "a people of one kind still sends it");
        int n = 20000;
        var counts = new Dictionary<string, int>();
        var rng = new Random(7);
        string last = null;
        int repeats = 0;
        for (int i = 0; i < n; i++)
        {
            var k = CarrierMath.Pick(pool, rng.NextDouble());
            counts[k] = counts.TryGetValue(k, out var c) ? c + 1 : 1;
        }
        C(Math.Abs(counts["Villager"] / (float)n - W(pool, "Villager")) < 0.02f, $"over {n} trips each kind comes as often as its chance");
        rng = new Random(11);
        for (int i = 0; i < n; i++)
        {
            var k = CarrierMath.Pick(pool, rng.NextDouble(), last);
            if (k == last) repeats++;
            last = k;
        }
        float plain = (float)Math.Pow(W(pool, "Villager"), 2) + (float)Math.Pow(W(pool, "Town_Guard"), 2);
        C(repeats / (float)n < plain, $"the same kind twice running less often than by chance alone ({repeats / (float)n:0.00} against {plain:0.00})");
    }

    private static CarrierMountOption M(string name, float w, float size, bool predator = false, bool ridable = true, bool land = true, bool season = true, bool animal = true) =>
        new CarrierMountOption { name = name, weight = w, bodySize = size, predator = predator, ridable = ridable, landOk = land, seasonOk = season, animal = animal };

    private static void Mounts(Action<bool, string> C)
    {
        var pool = CarrierMath.MountPool(new List<CarrierMountOption>
        {
            M("Muffalo", 6f, 2.4f), M("Dromedary", 5f, 2.1f, land: false), M("Alpaca", 2f, 1.0f), M("Elephant", 1f, 4f, season: false), M("Horse", 3f, 2.4f),
            M("Warg", 2f, 1.5f, predator: true), M("Thrumbo", 1f, 4f, ridable: false), M("Horse", 1f, 2.4f)
        });
        C(pool.Count == 2 && W(pool, "Muffalo") == 6f && W(pool, "Horse") == 4f, "muffalo and horses: " + string.Join(", ", pool.Select(kv => kv.Key)));
        C(W(pool, "Alpaca") == 0f, "never a beast too small to ride");
        C(W(pool, "Warg") == 0f, "never a predator");
        C(W(pool, "Dromedary") == 0f && W(pool, "Elephant") == 0f, "never one the land or the season doesn't allow");
        C(W(pool, "Thrumbo") == 0f, "never one Giddy-Up doesn't let be ridden");
        C(CarrierMath.MountPool(new List<CarrierMountOption> { M("Wolf", 1f, 1.3f, animal: false) }).Count == 0 && CarrierMath.MountPool(null).Count == 0, "nothing else");
        C(CarrierMath.Fallback(true, true) == "Horse" && CarrierMath.Fallback(false, true) == null && CarrierMath.Fallback(true, false) == null,
            "none of theirs fits: a horse where the season allows, else on foot");
        var rng = new Random(3);
        var got = new HashSet<string>();
        for (int i = 0; i < 200; i++) got.Add(CarrierMath.Pick(pool, rng.NextDouble()));
        C(got.SetEquals(new[] { "Muffalo", "Horse" }), "where they have more than one kind, both come");
    }

    private static void Remounts(Action<bool, string> C)
    {
        C(CarrierMath.Remount(false, true, false, true, true, 5f, 0) == RemountStep.None, "without Giddy-Up nobody rides: nothing to do");
        C(CarrierMath.Remount(true, false, false, true, true, 5f, 0) == RemountStep.None, "no mount: nothing to do");
        C(CarrierMath.Remount(true, true, true, true, true, 0f, 0) == RemountStep.None, "already in the saddle: nothing to do");
        C(CarrierMath.Remount(true, true, false, true, true, 1f, 0) == RemountStep.MountNow, "the beast beside them: on at once");
        C(CarrierMath.Remount(true, true, false, true, true, 12f, 0) == RemountStep.WalkToMount, "the beast a little way off (parked, waiting by the bag): they walk over and get on");
        C(CarrierMath.Remount(true, true, false, false, true, 3f, 0) == RemountStep.OnFoot, "the beast gone (dead, downed, fled): on foot");
        C(CarrierMath.Remount(true, true, false, true, false, 3f, 0) == RemountStep.OnFoot, "out of reach: on foot");
        C(CarrierMath.Remount(true, true, false, true, true, CarrierMath.RemountReach + 1f, 0) == RemountStep.OnFoot, "far off: on foot, the beast makes its own way out");
        C(CarrierMath.Remount(true, true, false, true, true, 12f, CarrierMath.RemountTicks + 1) == RemountStep.OnFoot, "an hour on and still not up: on foot");
        C(CarrierMath.MountWaits(RemountStep.MountNow) && CarrierMath.MountWaits(RemountStep.WalkToMount) && !CarrierMath.MountWaits(RemountStep.OnFoot)
          && !CarrierMath.MountWaits(RemountStep.None), "the beast stands for its rider only while they're coming to it, else it leaves too");
        C(CarrierMath.RemountTicks <= 2500 && CarrierMath.RemountReach >= 30f, "a short wait at the bag, never a long one");
        C(CarrierMath.OnFoot(true, false) && CarrierMath.OnFoot(false, true) && !CarrierMath.OnFoot(false, false),
            "on foot into the stores or to fetch someone; mail and goods at the bag from the saddle");
        // Every state, never stuck: they always end in the saddle or on foot, and the beast waits only while that's under way.
        bool stuck = false;
        foreach (bool gu in new[] { true, false })
            foreach (bool has in new[] { true, false })
                foreach (bool riding in new[] { true, false })
                    foreach (bool ok in new[] { true, false })
                        foreach (bool reach in new[] { true, false })
                            foreach (float d in new[] { 0f, 1f, 5f, 59f, 61f, 500f })
                                foreach (int t in new[] { 0, 2500, 2501, 60000 })
                                {
                                    var step = CarrierMath.Remount(gu, has, riding, ok, reach, d, t);
                                    if (CarrierMath.MountWaits(step) && (t > CarrierMath.RemountTicks || !ok || !reach || riding)) stuck = true;
                                }
        C(!stuck, "no state keeps a beast waiting for a rider who can't come, or past the hour");
        C(CourierMath.LingerTicks == 0, "no lingering once their business is done");
    }

    // ---------------- Rimfolk's own peoples: the old rule and the new ----------------

    private static void Peoples(string root, Action<bool, string> C)
    {
        string cat = Path.Combine(root, "docs", "player-mods");
        var power = new Dictionary<string, float>();
        var races = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(Path.Combine(cat, "pawnkinds.tsv")).Skip(1))
        {
            var c = line.Split('\t');
            if (c.Length < 7) continue;
            races[c[0]] = c[4];
            if (float.TryParse(c[6], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var p)) power[c[0]] = p;
        }
        var size = new Dictionary<string, float>();
        foreach (var line in File.ReadAllLines(Path.Combine(cat, "races.tsv")).Skip(1))
        {
            var c = line.Split('\t');
            if (c.Length > 5 && float.TryParse(c[5], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s)) size[c[0]] = s;
        }
        // The game's own kinds the catalogue doesn't give a power for.
        power.TryAdd("Mercenary_Gunner", 85f);
        power.TryAdd("Villager", 35f);
        power.TryAdd("Town_Guard", 60f);
        // A temperate forest, as the game makes it: muffalo and alpacas carry packs there; horses, donkeys and yaks live wild.
        var packHere = new HashSet<string> { "Muffalo", "Alpaca" };
        var wildHere = new HashSet<string> { "Muffalo", "Horse", "Donkey", "Yak", "Alpaca" };

        bool Person(string k) => races.TryGetValue(k, out var r) ? !r.StartsWith("Mech") : !k.StartsWith("Mech");
        float Pw(string k) => power.TryGetValue(k, out var p) ? p : 50f;
        int peoples = 0, varied = 0, oldOne = 0, mountsVaried = 0, oldMountOne = 0;
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs", "PeopleDefs"), "Rimfolk_People_*.xml"))
            foreach (var fd in XDocument.Load(f).Root!.Elements("FactionDef"))
            {
                string name = fd.Element("defName")?.Value ?? "?";
                var makers = fd.Element("pawnGroupMakers")?.Elements("li").ToList();
                if (makers == null || makers.Count == 0) continue;
                peoples++;
                var opts = new List<CarrierKindOption>();
                var oldPorters = new HashSet<string>();
                var carriers = new List<CarrierMountOption>();
                int g = 0;
                foreach (var m in makers)
                {
                    string kind = m.Element("kindDef")?.Value ?? "";
                    float commonality = float.TryParse(m.Element("commonality")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cm) ? cm : 100f;
                    IEnumerable<XElement> List(string tag) => m.Element(tag)?.Elements() ?? Enumerable.Empty<XElement>();
                    float Wt(XElement e) => float.Parse(e.Value, System.Globalization.CultureInfo.InvariantCulture);
                    if (kind == "Trader")
                        foreach (var e in List("guards"))
                        {
                            string k = e.Name.LocalName;
                            opts.Add(K(k, 0, g, Wt(e), Pw(k), person: Person(k), adult: !k.EndsWith("_Child"), commonality: commonality));
                            // The old rule: a caravan's guards of power 75 or less, people, not traders.
                            if (Person(k) && Pw(k) <= 75f) oldPorters.Add(k);
                        }
                    if (kind == "Peaceful")
                        foreach (var e in List("options"))
                        {
                            string k = e.Name.LocalName;
                            opts.Add(K(k, 1, g, Wt(e), Pw(k), person: Person(k), adult: !k.EndsWith("_Child"), commonality: commonality));
                        }
                    foreach (var e in List("carriers"))
                    {
                        string k = e.Name.LocalName;
                        bool land = packHere.Contains(k) || wildHere.Contains(k);
                        carriers.Add(M(k, Wt(e), size.TryGetValue(k, out var s) ? s : 1f, land: land));
                    }
                    g++;
                }
                string basic = fd.Element("basicMemberKind")?.Value;
                if (basic != null) opts.Add(K(basic, 2, -1, 1f, Pw(basic)));
                var pool = CarrierMath.KindPool(opts);
                C(pool.Count > 0, $"{name} has someone to send");
                C(pool.All(kv => !kv.Key.EndsWith("_Child") && !kv.Key.StartsWith("Mech_")), $"{name} sends no child and no machine");
                if (pool.Count >= 2) varied++;
                if (oldPorters.Count <= 1) oldOne++;
                C(pool.Count >= Math.Max(1, oldPorters.Count), $"{name}: at least as many kinds as before ({pool.Count}, was {Math.Max(1, oldPorters.Count)})");
                var mounts = CarrierMath.MountPool(carriers);
                // The old rule: the land's pack animals only.
                int oldMounts = carriers.Where(c => c.bodySize >= 1.2f && packHere.Contains(c.name)).Select(c => c.name).Distinct().Count();
                if (mounts.Count >= 2) mountsVaried++;
                if (oldMounts <= 1) oldMountOne++;
                C(mounts.Count >= oldMounts, $"{name}: at least as many beasts to ride in a temperate forest as before ({mounts.Count}, was {oldMounts})");
            }
        C(peoples >= 20, $"every one of Rimfolk's peoples looked at ({peoples})");
        C(varied >= peoples - 2, $"nearly every people sends more than one kind of person now ({varied} of {peoples}; one kind or none before for {oldOne})");
        C(mountsVaried > peoples / 2 && mountsVaried > peoples - oldMountOne, $"most peoples ride more than one kind of beast in a temperate forest now ({mountsVaried} of {peoples}; one or none for {oldMountOne} before)");
    }

    // ---------------- The seams ----------------

    private static void Seams(string root, Action<bool, string> C)
    {
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string tracker = Code("Couriers/CourierTracker.cs"), lordJob = Code("Couriers/CourierLord.cs"), lords = Code("Couriers/CarrierLords.cs"), jobs = Code("Couriers/CourierJobs.cs");
        string rider = Code("Post/PostRider.cs"), gu = Code("Integrations/GiddyUpMounts.cs"), kinds = Code("Couriers/CarrierKinds.cs"), spots = Code("Couriers/CarrierSpots.cs");
        string couriers = Code("Couriers/Couriers.cs"), trip = Code("Couriers/CourierTrip.cs"), helpers = Code("Happenings/HappeningHelpers.cs"), words = Code("Couriers/CourierWords.cs");

        C(tracker.Contains("var spot = CarrierSpots.Spot(map, out var where);") && tracker.Contains("CarrierSpots.EntryCell(map, spot, out var entry)")
          && rider.Contains("CarrierSpots.EntryCell(map, spot, out var entry)") && rider.Contains("var spot = Outbox.Spot(map);"),
            "couriers and the post rider make for the mailbag (else the gate), by the nearest way in");
        C(!tracker.Contains("PlaySpots.GateCell(map, null);\n            bool gate") && tracker.Contains("t.atBag = where == CarrierSpot.Mailbag;"), "the gate only when there's no mailbag");
        C(spots.Contains("Outbox.Bag(map)") && spots.Contains("PlaySpots.GateCell(map, null)") && spots.Contains("DropCellFinder.TradeDropSpot(map)") && spots.Contains("CarrierMath.SpotOf("),
            "the spot: the mailbag, the gate, the trade spot, by the pure rule");
        C(spots.Contains("map.floodFiller.FloodFill(spot, c => c.Walkable(map), (c, steps) =>") && spots.Contains("int best = CarrierMath.BestEntry(ways);"),
            "the way in measured on foot from the spot (a wall counts the way round), by the pure rule");
        C(lordJob.Contains("new LordToil_CarrierTravel(spot)") && lordJob.Contains("new LordToil_CarrierLeave()") && rider.Contains("new LordToil_CarrierTravel(spot)")
          && rider.Contains("new LordToil_CarrierLeave()"), "both groups walk in and out the carriers' way");
        C(!lordJob.Contains("new LordToil_ExitMap(Pace.Errand, false, true)") && !rider.Contains("new LordToil_ExitMap(Pace.Errand, false, true)")
          && !lordJob.Contains("LordToil_TravelOnErrand") && !rider.Contains("LordToil_TravelOnErrand"),
            "no more the game's travel (a random cell near the spot, stand and wander) nor its exit that cut a mount's job and set the rider down");
        C(!lordJob.Contains("DutyDefOf.WanderClose") && !rider.Contains("DutyDefOf.WanderClose") && lordJob.Contains("CourierDefOf.Rimfolk_CarrierWait")
          && rider.Contains("CourierDefOf.Rimfolk_CarrierWait"), "no wandering round the spot: they wait in their places");
        C(lords.Contains(": base(Pace.Errand, false, false)") && lords.Contains("GiddyUpMounts.CarryingRider(p) || GiddyUpMounts.Mounting(p)"),
            "setting off cuts the business short, never a beast carrying its rider or a rider getting on");
        C(lords.Contains("case RemountStep.MountNow:") && lords.Contains("GiddyUpMounts.MountNow(pawn, mount)") && lords.Contains("GiddyUpMounts.WalkAndMount(pawn, mount)")
          && lords.Contains("CarrierMath.MountWaits(CarrierMounts.Step(rider, pawn))"), "back in the saddle before leaving, the beast standing for them");
        C(rider.Contains("now - r.doneAt >= CourierMath.LingerTicks") && !rider.Contains("GenDate.TicksPerHour / 2"), "the post rider rides on once done, no half hour round the bag");
        C(jobs.Contains("OnFoot(pawn, t, true, false);") && jobs.Contains("OnFoot(pawn, t, false, true);") && jobs.Contains("GiddyUpMounts.GetOff(pawn, mount)"),
            "into the stores or to fetch someone on foot, the beast left at the bag");
        C(jobs.Contains("Drop(new LocalTargetInfo(CarrierJobs.PlaceOf(pawn, t.spot)), t.spot)") && tracker.Contains("CarrierSpots.SetDownBeside(inv, thing, count, map, at, out lying)"),
            "goods set down from their place onto the cells beside the bag");
        C(gu.Contains("TrainableDefOf.Tameness") && gu.Contains("\"SetAllowedJob\"") && gu.Contains("\"Council_CourierDrop\", \"Rimfolk_PostDrop\", \"Rimfolk_PostTake\"")
          && gu.Contains("animal.jobs.EndCurrentJob(JobCondition.InterruptForced)"),
            "Giddy-Up: the beast tame (so Giddy-Up lets them back on), the mailbag's jobs done in the saddle (no ride to the pen), getting off its own way");
        C(tracker.Contains("var kind = CarrierKinds.Person(f);") && tracker.Contains("var mountKind = ride ? CarrierKinds.Mount(f, map) : null;")
          && rider.Contains("var kind = CarrierKinds.Person(f);") && rider.Contains("CarrierKinds.Mount(f, map)"),
            "each courier, porter and post rider of a kind drawn fresh, on a beast drawn fresh");
        C(kinds.Contains("PawnGroupKindDefOf.Peaceful") && kinds.Contains("m.guards") && kinds.Contains("CarrierMath.KindPool(KindOptions(f))")
          && kinds.Contains("BiomeAnimals.LivesIn(biome, k)") && kinds.Contains("GiddyUpMounts.Ridable(k.race)"),
            "drawn from the people's caravans and visitors, beasts the land and Giddy-Up allow");
        C(helpers.Contains("forceGenerateNewPawn: true") && Regex.IsMatch(helpers, @"public static Pawn Make\(PawnKindDef kind, Faction faction, FloatRange\? age, Gender\? gender"),
            "every carrier made new (never a world pawn come again)");
        C(couriers.Contains("CarrierKinds.AnyPerson(f)"), "a people can send someone when its range has anyone");
        C(trip.Contains("Scribe_Values.Look(ref atBag, \"atBag\");") && trip.Contains("public Pawn MountOf(Pawn p)") && !trip.Contains("mounts.RemoveAll(p => p == null);"),
            "the trip keeps where it is and which beast is whose (a gap stays a gap)");
        C(words.Contains("CC_Courier_ByBag") && words.Contains("CC_Courier_ToBag"), "the words say the mailbag where there is one");

        var defs = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_Couriers.xml")).Root!;
        XElement Duty(string n) => defs.Elements("DutyDef").FirstOrDefault(d => d.Element("defName")?.Value == n);
        IEnumerable<string> Nodes(XElement d) => d?.Descendants("li").Select(l => (string)l.Attribute("Class")).Where(c => c != null) ?? Enumerable.Empty<string>();
        C(Nodes(Duty("Council_CourierWork")).SequenceEqual(new[] { "ColonyCouncil.JobGiver_CourierWork", "ColonyCouncil.JobGiver_CarrierWait" }), "the courier's duty: their work, else a wait in their place");
        C(Nodes(Duty("Rimfolk_CarrierTravel")).SequenceEqual(new[] { "ColonyCouncil.JobGiver_CarrierGoTo" }), "the walk in: straight to their place");
        C(Nodes(Duty("Rimfolk_CarrierWait")).SequenceEqual(new[] { "ColonyCouncil.JobGiver_CarrierWait" }), "the wait: in their place");
        C(Nodes(Duty("Rimfolk_CarrierLeave")).SequenceEqual(new[] { "ColonyCouncil.JobGiver_CarrierRemount", "ColonyCouncil.JobGiver_CarrierMountWait", "JobGiver_ExitMapBest" }),
            "the way out: back in the saddle, the beast waiting, then the nearest way off the map");

        var keys = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var e in XDocument.Load(f).Root!.Elements())
                keys[e.Name.LocalName] = e.Value;
        C(keys.TryGetValue("CC_Courier_ByBag", out var by) && by == "by the mailbag" && keys["CC_Courier_ToBag"] == "the mailbag" && keys["CC_Courier_ToGate"] == "the gate",
            "where, in words");
        foreach (var k in new[] { "CC_Courier_Arrive_Await_One", "CC_Courier_Arrive_Await_Many", "CC_Courier_Arrive_AwaitPay_One", "CC_Courier_Arrive_AwaitPay_Many" })
            C(keys.TryGetValue(k, out var t) && t.Contains("walking up to {4}") && !t.Contains("the gate"), $"{k} names where they walk up to");
        C(!keys["CC_Settings_GoodsComeAndGo_Tip"].Contains("They stay a little") && keys["CC_Settings_GoodsComeAndGo_Tip"].Contains("mailbag"), "the setting's tip says the mailbag, and no lingering");
    }
}
