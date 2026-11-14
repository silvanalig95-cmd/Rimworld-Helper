using System.Text.RegularExpressions;
using ColonyCouncil;
using P = ColonyCouncil.WeatherDressMath.Piece;

/// <summary>
/// People who come to the colony's map dressed for the weather (Visitors/WeatherDressMath.cs, Visitors/WeatherDress.cs; the
/// player, 14 November 2026: a herd of the road league's on the far meadow at -20 °C "left it again soon after due to
/// weather conditions"). Who is dressed on arrival and who isn't, the stay's line between hard weather and the
/// extremes, what may come off and what may go on, what it may cost (a raid's loot as it was), the pick, why a group
/// still goes, the player's own case and pool, and the seams.
/// </summary>
public static class WeatherDressTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "dressed for the weather: " + what);
        }

        Who(C);
        Line(C);
        Pieces(C);
        Worth(C);
        Pick(C);
        Hindering(C);
        TheSave(C);
        Leaving(C);
        Pool(root, C);
        Seams(root, C);
        return n;
    }

    // ---------------- Who ----------------

    private static WeatherDressMath.Arrival Visitor() => new WeatherDressMath.Arrival
    {
        playing = true, home = true, humanlike = true, toolUser = true, flesh = true, faction = true, tech = WeatherDressMath.Industrial, fresh = true,
        came = WeatherDressMath.Came.Walked
    };

    private static void Who(Action<bool, string> C)
    {
        var W = WeatherDressMath.Who.Dress;
        WeatherDressMath.Who Q(WeatherDressMath.Arrival a) => WeatherDressMath.Qualifies(a);
        var v = Visitor();
        C(Q(v) == W, "a visitor, a trader or a traveller walking in from the edge is dressed for the weather");
        var world = v;
        world.fresh = false;
        C(Q(world) == W, "and someone who has been before, kept in the world between visits (Hospitality's guests, a world event's own person)");
        var pod = v;
        pod.came = WeatherDressMath.Came.Pod;
        var shuttle = v;
        shuttle.came = WeatherDressMath.Came.Shuttle;
        C(Q(pod) == W && Q(shuttle) == W, "allies, guests and raiders off a drop pod or a shuttle planned the trip and dressed for it");
        var tribe = v;
        tribe.tech = WeatherDressMath.Neolithic;
        C(Q(tribe) == W, "every age: a tribe's people too, in their own clothes");
        // Raiders: hostility is no part of who; they dress the same way, their gear's worth kept close (Worth).
        C(!typeof(WeatherDressMath.Arrival).GetFields().Any(f => f.Name.Contains("hostile")), "friend or foe alike: raiders, sieges and war bands come dressed for the weather too");

        var joiner = v;
        joiner.player = true;
        C(Q(joiner) == W, "a wanderer who joins, new to the colony and walking in, comes dressed for it");
        var lodger = joiner;
        lodger.came = WeatherDressMath.Came.Shuttle;
        C(Q(lodger) == W, "lodgers off a shuttle too");
        var home = joiner;
        home.fresh = false;
        C(Q(home) == WeatherDressMath.Who.OwnPeople, "the colony's own coming home from a caravan come as they are");
        var bought = joiner;
        bought.came = WeatherDressMath.Came.Pod;
        C(Q(bought) == WeatherDressMath.Who.OwnPeople, "someone bought and sent down in a pod comes as they are (nothing given to the colony)");

        var crash = pod;
        crash.downed = true;
        C(Q(crash) == WeatherDressMath.Who.Downed, "a crashed pod's survivor, down as they land, had no time to dress");
        var carried = v;
        carried.came = WeatherDressMath.Came.FromHere;
        C(Q(carried) == WeatherDressMath.Who.AlreadyHere, "someone set down, out of a casket or landing from a jump is already here");
        var moved = v;
        moved.recentlyHere = true;
        C(Q(moved) == WeatherDressMath.Who.AlreadyHere, "and someone off a map within the hour is moving about, not arriving");

        var animal = v;
        animal.humanlike = false;
        var mech = v;
        mech.flesh = false;
        var noTools = v;
        noTools.toolUser = false;
        C(Q(animal) == WeatherDressMath.Who.NotAPerson && Q(mech) == WeatherDressMath.Who.NotAPerson && Q(noTools) == WeatherDressMath.Who.NotAPerson,
            "animals, insects and mechanoids are left alone");
        var entity = v;
        entity.entity = true;
        C(Q(entity) == WeatherDressMath.Who.Entity, "Anomaly's entities and the walking dead too");
        var captive = v;
        captive.captive = true;
        C(Q(captive) == WeatherDressMath.Who.Captive, "a prisoner or a slave doesn't choose what they wear");
        var baby = v;
        baby.baby = true;
        C(Q(baby) == WeatherDressMath.Who.Baby, "a baby is carried in someone's arms");
        var none = v;
        none.faction = false;
        var wild = v;
        wild.wild = true;
        var beasts = v;
        beasts.tech = WeatherDressMath.Animal;
        C(Q(none) == WeatherDressMath.Who.NoWays && Q(wild) == WeatherDressMath.Who.NoWays && Q(beasts) == WeatherDressMath.Who.NoWays,
            "no people of their own, wild, or no age with clothing: nothing they could own");
        var loading = v;
        loading.playing = false;
        var making = v;
        making.generating = true;
        C(Q(loading) == WeatherDressMath.Who.NotNow && Q(making) == WeatherDressMath.Who.NotNow, "not while the game starts or a map is being made");
        var away = v;
        away.home = false;
        var space = v;
        space.vacuum = true;
        C(Q(away) == WeatherDressMath.Who.NotHome && Q(space) == WeatherDressMath.Who.Vacuum, "only on the colony's homes, and not in space (the game's vacuum gear counts there)");
    }

    // ---------------- The weather of the stay ----------------

    private static void Line(Action<bool, string> C)
    {
        C(WeatherDressMath.ArrivalHours == 48, "they dress for two days ahead as they arrive: the coming night and the next");
        C(WeatherDressMath.VisitHours(0f, 0) == 48 && WeatherDressMath.VisitHours(6f, 1) == 48, "a short visit or a night on the far meadow: two days");
        C(WeatherDressMath.VisitHours(8f, 3) == 8 + 72 + 12, "three nights: their hours, their nights and the half day they may wait on");
        C(WeatherDressMath.VisitHours(9999f, 99) == WeatherDressMath.MaxHours && WeatherDressMath.VisitHours(float.NaN, -2) == 48, "never past a season, and a bad number reads as none");

        // Someone in ordinary clothes: comfortable from 16 to 28 °C.
        C(WeatherDressMath.Outcome(16f, 28f, 16f, 28f, 14f, 30f) == WeatherDressMath.Dressing.AsTheyAre, "a mild stay: they come as they are (nothing needless shown)");
        C(WeatherDressMath.Outcome(16f, 28f, 16f, 28f, 16f - WeatherDressMath.Enough, 28f) == WeatherDressMath.Dressing.AsTheyAre
          && WeatherDressMath.Outcome(16f, 28f, -20f, 28f, 16f - WeatherDressMath.Enough - 1f, 28f) == WeatherDressMath.Dressing.Dressed,
            "four degrees past comfortable is the game's own line for dressing warmer; past it they put something on");
        C(WeatherDressMath.Outcome(2f, 24f, -22f, 24f, -24f, -10f) == WeatherDressMath.Dressing.Dressed, "hard weather, -20 by day and colder at night, that a parka keeps out: dressed");
        C(WeatherDressMath.Outcome(2f, 24f, -30f, 24f, -60f, -45f) == WeatherDressMath.Dressing.PastTheirClothes,
            "past what any clothing of their age keeps out: the extreme cold, where the game's own leaving stays");
        C(WeatherDressMath.Outcome(16f, 28f, 16f, 40f, 20f, 58f) == WeatherDressMath.Dressing.PastTheirClothes, "and the extreme heat");
        C(WeatherDressMath.SafeThrough(-22f, 24f, -31f, 10f) && !WeatherDressMath.SafeThrough(-22f, 24f, -33f, 10f),
            "safe is the game's own range, ten degrees past comfortable either way");
    }

    // ---------------- What may come off, what may go on ----------------

    private static void Pieces(Action<bool, string> C)
    {
        // The player's pool, from docs/player-mods/apparel_numbers.json.gz: duster and jacket 0.3, parka 0.2, plate 0.9 (steel 0.81).
        C(!WeatherDressMath.Armour(false, 0.3f, 0f) && !WeatherDressMath.Armour(false, 0.2f, 0f), "a duster, a jacket or a parka is a plain layer");
        C(WeatherDressMath.Armour(false, 0.9f, 0f) && WeatherDressMath.Armour(false, 0f, 0.55f) && WeatherDressMath.Armour(true, 0f, 0f),
            "plate, a flak jacket (armour of its own) and anything of the armour kinds are armour");
        C(WeatherDressMath.Kept(true, false, false, false, false, false) && WeatherDressMath.Kept(false, true, false, false, false, false)
          && WeatherDressMath.Kept(false, false, true, false, false, false) && WeatherDressMath.Kept(false, false, false, false, true, false)
          && WeatherDressMath.Kept(false, false, false, false, false, true) && WeatherDressMath.Kept(false, false, false, true, false, false),
            "never taken off: armour, a belt, gear, a special piece, what's locked on, what a title or a role asks for");
        C(!WeatherDressMath.Kept(false, false, false, false, false, false), "a plain coat or hat may give way to a warmer one");
        C(WeatherDressMath.Offered(true, true, true, false, false, false, false, false), "a plain layer that keeps out cold or heat may go on");
        C(!WeatherDressMath.Offered(true, true, true, false, false, false, false, true), "nothing that slows or hinders: no straitjacket however warm");
        C(!WeatherDressMath.Offered(true, true, true, true, false, false, false, false) && !WeatherDressMath.Offered(true, true, true, false, true, false, false, false)
          && !WeatherDressMath.Offered(true, true, true, false, false, true, false, false), "no armour, belt or gear handed out: a raid gets no armour for the cold");
        C(!WeatherDressMath.Offered(true, false, true, false, false, false, false, false) && !WeatherDressMath.Offered(true, true, false, false, false, false, false, false)
          && !WeatherDressMath.Offered(false, true, true, false, false, false, false, false),
            "only what the game itself would hand out for warmth, that people are made wearing, and that insulates");

        int I = WeatherDressMath.Industrial, N = WeatherDressMath.Neolithic, M = WeatherDressMath.Medieval, S = WeatherDressMath.Spacer;
        C(!WeatherDressMath.AgeFits(S, I, false, false, false) && WeatherDressMath.AgeFits(M, I, false, false, false), "nothing past their own age");
        C(!WeatherDressMath.AgeFits(I, I, false, true, true) && WeatherDressMath.AgeFits(M, I, false, true, true), "nothing past the colony's era in a colony of the old ways");
        C(!WeatherDressMath.AgeFits(N, I, false, false, false) && !WeatherDressMath.AgeFits(I, N, false, false, false),
            "the game's own rule: the tribes' hides for the tribes, the industrial for those past them");
        C(WeatherDressMath.AgeFits(N, I, false, true, false), "a parka (tribal work) for an outlander whose kind wears it (its IndustrialBasic tag): the game's own free layer missed it");
        C(WeatherDressMath.AgeFits(N, I, true, false, false), "and what is meant for any age");
        C(WeatherDressMath.AgeFits(N, N, false, false, false), "a tribe's own parka for a tribe");
        C(WeatherDressMath.Fit(true, true) > WeatherDressMath.Fit(true, false) && WeatherDressMath.Fit(true, false) > WeatherDressMath.Fit(false, true)
          && WeatherDressMath.Fit(false, true) > WeatherDressMath.Fit(false, false), "their own kind's pieces first, then their faith's style");
    }

    // ---------------- What it may cost ----------------

    private static void Worth(Action<bool, string> C)
    {
        const float parka = 120f, tuque = 30f;
        C(Math.Abs(WeatherDressMath.Room(false, 300f, 0f, parka, tuque) - (parka + tuque) * WeatherDressMath.FreeShare) < 0.01f,
            "a visitor may come with the game's own free parka and tuque");
        C(WeatherDressMath.Room(false, 300f, 650f, parka, tuque) == 350f, "or what their kind would have spent on clothes and didn't");
        C(Math.Abs(WeatherDressMath.Room(true, 100f, 650f, parka, tuque) - tuque * WeatherDressMath.FreeShare) < 0.01f
          && Math.Abs(WeatherDressMath.Room(true, 1000f, 650f, parka, tuque) - 150f) < 0.01f, "a raider's gear keeps close to its worth: a tuque's or a small share of it");
        C(WeatherDressMath.Room(true, 1000f, 9999f, parka, tuque) < WeatherDressMath.Room(false, 1000f, 9999f, parka, tuque), "never as much as a visitor's");
        C(WeatherDressMath.Room(false, -5f, 0f, parka, tuque) > 0f, "a bad worth reads as none");
        C(WeatherDressMath.PieceShare >= WeatherDressMath.FreeShare && WeatherDressMath.HostileShare <= 0.2f, "nothing dear: a cloth parka and a half for one piece at most");
    }

    // ---------------- The pick ----------------

    private static void Pick(Action<bool, string> C)
    {
        bool None(int a, int b) => false;
        // Bare 16 to 26 (a person), a night of -28 and a day of -10.
        var nothing = new List<P>();
        var coat = new List<P> { new P(36f, 0f, 120f), new P(9f, 0f, 30f) };
        // The parka and the tuque don't clash with each other or with anything.
        var c = WeatherDressMath.Choose(nothing, coat, None, None, 16f, 26f, -28f, -10f, 999f);
        C(c.on.Contains(0) && c.on.Contains(1) && WeatherDressMath.Short(c.comfyMin, c.comfyMax, -28f, -10f) <= WeatherDressMath.Enough,
            "a parka and a tuque against a night of -28");
        var one = WeatherDressMath.Choose(nothing, coat, None, None, 16f, 26f, -24f, -10f, 999f);
        C(one.on.Count == 1 && one.on[0] == 0, "the parka alone against -24: within four degrees of comfortable, nothing more");
        C(c.comfyMin == 16f - 45f && c.spent == 150f, "comfortable and what it cost, as the pieces add up");
        var mild = WeatherDressMath.Choose(nothing, coat, None, None, 16f, 26f, 15f, 24f, 999f);
        C(mild.on.Count == 0, "nothing put on in mild weather");
        var poor = WeatherDressMath.Choose(nothing, coat, None, None, 16f, 26f, -28f, -10f, 40f);
        C(!poor.on.Contains(0) && poor.on.Contains(1), "within what it may cost: the tuque, not the parka");

        // A kept shell on the torso (armour) and a plain one: the parka takes the place of the plain one only.
        var armour = new List<P> { new P(10f, 2f, 400f, 0, true) };
        var a = WeatherDressMath.Choose(armour, coat, (o, w) => o == 0, None, 16f, 26f, -28f, -10f, 999f);
        C(!a.on.Contains(0) && a.on.Contains(1) && a.off.Count == 0, "armour is never taken off: the parka that would replace it isn't put on");
        var plain = new List<P> { new P(12f, 9f, 60f) };
        var b = WeatherDressMath.Choose(plain, coat, (o, w) => o == 0, None, 16f, 26f, -28f, -10f, 999f);
        C(b.on.Contains(0) && b.off.Contains(0) && Math.Abs(b.spent - (120f + 30f - 60f)) < 0.01f, "a plain duster gives way to the parka, its worth counted off");

        // Two coats that would each do: their own kind's, dearer, before a cheaper stranger's; of the same fit, the cheaper.
        var two = new List<P> { new P(40f, 0f, 150f, 0), new P(40f, 0f, 160f, 2) };
        var k = WeatherDressMath.Choose(nothing, two, None, (o, q) => true, 16f, 26f, -24f, -10f, 999f);
        C(k.on.Count == 1 && k.on[0] == 1, "of two that would do, the one of their own kind");
        var same = new List<P> { new P(40f, 0f, 160f, 2), new P(40f, 0f, 150f, 2) };
        var s = WeatherDressMath.Choose(nothing, same, None, (o, q) => true, 16f, 26f, -24f, -10f, 999f);
        C(s.on.Count == 1 && s.on[0] == 1, "of the same fit, the cheaper");
        // Neither does alone: the one that leaves them least short.
        var weak = new List<P> { new P(10f, 0f, 20f, 2), new P(30f, 0f, 80f, 0) };
        var w2 = WeatherDressMath.Choose(nothing, weak, None, (o, q) => true, 16f, 26f, -40f, -10f, 999f);
        C(w2.on.Count == 1 && w2.on[0] == 1, "none would do alone: the warmest, unless another is within a couple of degrees");

        // The heat: a cowboy hat and a duster against a hot afternoon.
        var hot = new List<P> { new P(1.8f, 9f, 25f), new P(10f, 15f, 70f) };
        var h = WeatherDressMath.Choose(nothing, hot, None, None, 16f, 26f, 18f, 44f, 999f);
        C(h.on.Count > 0 && h.comfyMax > 26f, "cool layers against the heat");

        // Never more than five layers; each brings them nearer comfortable.
        var many = new List<P>();
        for (int i = 0; i < 12; i++) many.Add(new P(3f, 0f, 5f));
        var m = WeatherDressMath.Choose(nothing, many, None, None, 16f, 26f, -60f, -40f, 999f);
        C(m.on.Count == WeatherDressMath.MaxLayers, "five layers at most");
        var useless = new List<P> { new P(0.2f, 0f, 5f) };
        C(WeatherDressMath.Choose(nothing, useless, None, None, 16f, 26f, -24f, -10f, 999f).on.Count == 0, "a layer that hardly helps isn't put on");
        // Their own kind's coat, chosen within a couple of degrees of a stranger's, isn't swapped for it a layer later.
        var close = new List<P> { new P(30f, 0f, 50f, 2), new P(31.5f, 0f, 50f, 0) };
        var r = WeatherDressMath.Choose(nothing, close, None, (o, q) => true, 16f, 26f, -40f, -10f, 999f);
        C(r.on.Count == 1 && r.on[0] == 0, "their own kind's coat stays on rather than a stranger's barely warmer");
        var covers = new List<P> { new P(20f, 0f, 30f, 2), new P(45f, 0f, 120f, 0) };
        var cv = WeatherDressMath.Choose(nothing, covers, None, (o, q) => true, 16f, 26f, -30f, -10f, 999f);
        C(cv.on.Count == 1 && cv.on[0] == 1 && WeatherDressMath.SafeThrough(cv.comfyMin, cv.comfyMax, -30f, -10f), "but a coat that alone keeps out the cold before one that doesn't");
    }

    // ---------------- A piece that hinders them ----------------

    private static void Hindering(Action<bool, string> C)
    {
        C(WeatherDressMath.OutOfPlace(true, false, false), "a piece that slows or hinders them, that their kind didn't ask for, comes off (the free warm layer's straitjacket)");
        C(!WeatherDressMath.OutOfPlace(true, true, false), "never armour, a title's or a role's apparel, however it hinders");
        C(!WeatherDressMath.OutOfPlace(true, false, true), "never what their kind requires or asks for by its apparel tags");
        C(!WeatherDressMath.OutOfPlace(false, false, false), "a plain piece that doesn't hinder stays");

        // Worn: a sash, the straitjacket (1.6 x cloth 18 = 28.8 cold, 3.6 heat), a visage mask. Bare 16 to 26.
        var worn = new List<P> { new P(1.8f, 1.8f, 20f), new P(28.8f, 3.6f, 60f), new P(0.5f, 0.5f, 30f) };
        // On offer: a cloth parka and a cloth jacket (Shell, the straitjacket's place), a tuque (the mask's place), a coat
        // that would take the sash's place too.
        var offered = new List<P> { new P(36f, 0f, 120f, 2), new P(14.4f, 5.4f, 90f, 2), new P(9f, 0f, 30f, 2), new P(40f, 2f, 100f, 2) };
        bool Clash(int o, int w) => (o == 0 || o == 1) && w == 1 || o == 2 && w == 2 || o == 3 && (w == 0 || w == 1);
        int mild = WeatherDressMath.Swap(worn, 1, offered, Clash, 16f, 26f, 10f, 20f, 999f);
        C(mild == 1, "whatever the weather: on a mild day, the jacket of their kind, the cheapest that keeps them as comfortable");
        int cold = WeatherDressMath.Swap(worn, 1, offered, Clash, 16f, 26f, -24f, -12f, 999f);
        C(cold == 0, "on a night of -24, the parka: a jacket would leave them colder than they came");
        C(WeatherDressMath.Swap(worn, 1, offered, Clash, 16f, 26f, -24f, -12f, 20f) == -1, "within the same worth budget, or it stays on");
        C(WeatherDressMath.Swap(worn, 1, new List<P> { offered[1] }, (o, w) => w == 1, 16f, 26f, -24f, -12f, 999f) == -1,
            "none that keeps them as warm (only a jacket on offer at -24): it stays on rather than leave them colder");
        C(WeatherDressMath.Swap(worn, 1, offered, Clash, 16f, 26f, -50f, -40f, 999f) == 0, "past the extremes still swapped for a warmer one: never colder than they came");
        var onlyWide = new List<P> { offered[3] };
        C(WeatherDressMath.Swap(worn, 1, onlyWide, (o, w) => w == 0 || w == 1, 16f, 26f, -24f, -12f, 999f) == -1,
            "a piece that would take more than its place (the sash too) isn't the swap");
        var stranger = new List<P> { new P(36f, 0f, 100f, 0), new P(36f, 0f, 120f, 2) };
        C(WeatherDressMath.Swap(worn, 1, stranger, (o, w) => w == 1, 16f, 26f, -24f, -12f, 999f) == 1, "their own kind's piece first");
        C(WeatherDressMath.Swap(worn, 7, offered, Clash, 16f, 26f, 10f, 20f, 999f) == -1 && WeatherDressMath.Swap(null, 0, offered, Clash, 16f, 26f, 10f, 20f, 999f) == -1,
            "nothing to swap: nothing done");
    }

    // ---------------- The player's case ----------------

    private static void TheSave(Action<bool, string> C)
    {
        // Their save (League of Toba, 14 November 2026): trip 3 of the road league's "Ararcoler League" (the hauler convoy's
        // lead train, Rimfolk_WS_Variety3_Roads, VisitEffect_Come caravan to the far meadow for a night), three
        // Rimfolk_League_Wayfarer, left 1.3 hours after setting out, end Weather, never reached the meadow; one with
        // hypothermia 0.0525 in a sash of cloth, a fox-leather duster and a visage mask, about -20 °C on the map.
        float bare = 16f, top = 26f;
        // Sash 0.1 x cloth 18, duster 0.6 x fox leather about 20, a steel mask next to nothing.
        var worn = new List<P> { new P(1.8f, 1.8f, 20f), new P(12f, 17f, 90f), new P(0.5f, 0.5f, 30f) };
        float nowMin = bare - 1.8f - 12f - 0.5f, nowMax = top + 1.8f + 17f + 0.5f;
        C(!WeatherDressMath.SafeThrough(nowMin, nowMax, -24f, -12f), "as they came, a night of -24 would hurt them: the cause");
        C(WeatherDressMath.Suffers(0.0525f, 0f, false) == WeatherDressMath.Why.Cold, "and the hypothermia the save shows was past Rimfolk's line: they left");
        // On offer: a cloth parka (2 x 18, their kind's IndustrialBasic, the duster's Shell layer), a tuque, a cowboy hat.
        var offered = new List<P> { new P(36f, 0f, 120f, 2), new P(9f, 0f, 30f, 2), new P(1.8f, 9f, 25f, 2) };
        bool Clash(int o, int w) => o == 0 && w == 1 || o == 1 && w == 2 || o == 2 && w == 2;
        var c = WeatherDressMath.Choose(worn, offered, Clash, (o, q) => o != 0 && q != 0, bare, top, -24f, -12f,
            WeatherDressMath.Room(false, 140f, 650f, 120f, 30f));
        C(c.on.Contains(0) && c.off.Contains(1), "now a teal parka (their coat's colour by their kind) in place of the duster");
        C(WeatherDressMath.Outcome(nowMin, nowMax, c.comfyMin, c.comfyMax, -24f, -12f) == WeatherDressMath.Dressing.Dressed && WeatherDressMath.SafeThrough(c.comfyMin, c.comfyMax, -24f, -12f),
            "and the night on the far meadow no longer hurts them: they stay");
        // Human531631 came in the game's free warm layer, Vanilla Anomaly Expanded's straitjacket (MoveSpeed -1, tagged only
        // VAE_Straitjacket, not of the wayfarer's kind): it comes off for the parka.
        var jacketed = new List<P> { new P(1.8f, 1.8f, 20f), new P(28.8f, 3.6f, 60f), new P(0.5f, 0.5f, 30f) };
        C(WeatherDressMath.OutOfPlace(true, false, false) && WeatherDressMath.Swap(jacketed, 1, offered, (o, w) => o == 0 && w == 1, bare, top, -24f, -12f, 510f) == 0,
            "and the wayfarer the game put in a straitjacket comes in a parka instead");
    }

    // ---------------- Why they still go ----------------

    private static void Leaving(Action<bool, string> C)
    {
        C(WeatherDressMath.Suffering == 0.04f && WeatherDressMath.GameLeaves == 0.15f && WeatherDressMath.Suffering < WeatherDressMath.GameLeaves,
            "Rimfolk's visitors go a little before the game's own (0.04 against 0.15 of hypothermia or heatstroke)");
        C(WeatherDressMath.Suffers(0.03f, 0f, false) == WeatherDressMath.Why.None, "a chill short of that keeps them");
        C(WeatherDressMath.Suffers(0f, 0.05f, false) == WeatherDressMath.Why.Heat && WeatherDressMath.Suffers(0.06f, 0.05f, true) == WeatherDressMath.Why.Cold,
            "the cold or the heat, whichever has taken hold more");
        C(WeatherDressMath.Suffers(0f, 0f, true) == WeatherDressMath.Why.Hunger, "else hunger");
        C(WeatherDressMath.NightWhy(-20f, 24f, -31f, 0f) == WeatherDressMath.Why.Cold && WeatherDressMath.NightWhy(16f, 28f, 20f, 39f) == WeatherDressMath.Why.Heat
          && WeatherDressMath.NightWhy(-20f, 24f, -29f, 0f) == WeatherDressMath.Why.None, "a night out that would hurt them, by its cold or heat");
        C(WeatherDressMath.VisitKey(WeatherDressMath.Why.Cold) == "CC_Visit_Weather_cold" && WeatherDressMath.VisitKey(WeatherDressMath.Why.Heat) == "CC_Visit_Weather_heat"
          && WeatherDressMath.VisitKey(WeatherDressMath.Why.Hunger) == "CC_Visit_Weather_hunger", "the message says it was the extreme cold, the extreme heat, or hunger");
        C(WeatherDressMath.GameKey(0.2f, 0f) == "CC_WeatherLeave_Cold" && WeatherDressMath.GameKey(0f, 0.2f) == "CC_WeatherLeave_Heat",
            "the game's own groups' message too");
    }

    // ---------------- The player's pool ----------------

    private static void Pool(string root, Action<bool, string> C)
    {
        var rows = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "apparel.tsv")).Skip(1).Select(l => l.Split('\t')).Where(r => r.Length > 6)
            .GroupBy(r => r[0]).ToDictionary(g => g.Key, g => g.Last());
        bool Active(string d) => rows.TryGetValue(d, out var r) && r[2] == "yes";
        bool Tagged(string d, string tag) => rows.TryGetValue(d, out var r) && r[5].Split(',').Contains(tag);
        C(Active("Apparel_Parka") && Active("Apparel_Tuque") && Active("Apparel_Duster") && Active("Apparel_CowboyHat") && Active("Apparel_KidParka"),
            "the game's parka, tuque, duster, cowboy hat and children's parka are in the player's pool");
        C(Active("VCE_Apparel_WarmJacket") && Active("VCE_Apparel_WarmHat") && Active("VAE_Headgear_Scarf") && Active("VAE_Handwear_Gloves"),
            "and their mods' warm jacket, warm hat, scarf and gloves, drawn on as the game's own are");
        C(Tagged("Apparel_Parka", "IndustrialBasic") && Tagged("Apparel_Parka", "Neolithic") && rows["Apparel_Parka"][4] == "Neolithic",
            "the parka is tribal work tagged for outlanders too: why their kind's tag lets an industrial people wear it");
        string league = File.ReadAllText(Path.Combine(root, "Defs", "PeopleDefs", "Rimfolk_People_League.xml"));
        C(Regex.IsMatch(league, @"<defName>Rimfolk_League_Wayfarer</defName>[\s\S]*?<apparelTags>[\s\S]*?<li>IndustrialBasic</li>"), "the wayfarer's kind wears IndustrialBasic: the parka is theirs");
    }

    // ---------------- The seams ----------------

    private static void Seams(string root, Action<bool, string> C)
    {
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => Regex.Replace(File.ReadAllText(Path.Combine(src, rel)), @"//[^\n]*", "");
        string dress = Code("Visitors/WeatherDress.cs");
        C(dress.Contains("[HarmonyPatch(typeof(GenSpawn), nameof(GenSpawn.Spawn), new[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool), typeof(bool) })]")
          && Regex.IsMatch(dress, @"public static void Prefix\(Thing newThing, Map map, bool respawningAfterLoad\)\s*\{\s*if \(respawningAfterLoad \|\| map == null \|\| !\(newThing is Pawn p\) \|\| p\.Spawned \|\| !RimfolkPatches\.On\) return;\s*WeatherDress\.OnSpawn\(p, map\);"),
            "one hook where everyone appears on a map, before they show, never on loading, under the deeper hooks setting");
        C(dress.Contains("return WeatherDressMath.Qualifies(a);") && dress.Contains("if (Arriving(p, map) != WeatherDressMath.Who.Dress) return;")
          && dress.Contains("ForStay(p, map, WeatherDressMath.ArrivalHours);"), "who by the pure rule, for the next two days");
        C(dress.Contains("var outcome = WeatherDressMath.Outcome(nowMin, nowMax, plan.choice.comfyMin, plan.choice.comfyMax, low, high);")
          && dress.Contains("if (outcome == WeatherDressMath.Dressing.Dressed) Apply(p, plan);"), "dressed only where it keeps them safe; past that, as they came");
        C(dress.Contains("if (off.Any(w => Kept(w, p))) continue;") && dress.Contains("PawnApparelGenerator.PostProcessApparel(ap, p);")
          && dress.Contains("PawnGenerator.PostProcessGeneratedGear(ap, p);") && dress.Contains("if (!EquipmentUtility.CanEquip(ap, p))"),
            "nothing kept comes off; in their kind's colours and faith's style; only what they can wear (other mods' races too)");
        C(dress.Contains("p.apparel.Remove(w);") && dress.Contains("w.Destroy();") && !dress.Contains("TryPlaceThing") && !dress.Contains("innerContainer.TryAdd"),
            "what comes off is gone, never left on the map or in a pack (nothing given to the colony, no loot)");
        C(dress.Contains("WeatherDressMath.Room(hostile, wornWorth, p.kindDef?.apparelMoney.max ?? 0f, parka, tuque)") && dress.Contains("p.Faction.HostileTo(Faction.OfPlayer)"),
            "worth counted, a raider's by its own rule");
        C(dress.Contains("CampWeather.StayRange(map, hours * GenDate.TicksPerHour)"), "the stay's weather is the land's own reckoning, as the camps read it");
        C(dress.Contains("[HarmonyPatch(typeof(TransitionAction_Message), nameof(TransitionAction_Message.DoAction))]") && dress.Contains("\"MessageVisitorsDangerousTemperature\".Translate(plural, f.Name)")
          && dress.Contains("a.message = WeatherDressMath.GameKey(cold, heat).Translate(plural, f.Name);"),
            "the game's own leaving for the temperature stays, said as the extreme cold or heat");
        C(Regex.IsMatch(dress, @"float room = RoomFor\(p, hostile\);\s*room -= SwapHindering\(p, tech, low, high, room\);\s*float nowMin = Min\(p\), nowMax = Max\(p\);\s*if \(WeatherDressMath\.Short\(nowMin, nowMax, low, high\) <= WeatherDressMath\.Enough\)")
          && dress.Contains("var plan = Make(p, low, high, nowMin, nowMax, tech, hostile, Math.Max(0f, room));"),
            "a piece that hinders them is swapped first, whatever the weather, within the same worth budget");
        C(dress.Contains("WeatherDressMath.OutOfPlace(Hinders(a.def), Kept(a, p), AskedFor(a.def, p.kindDef))") && dress.Contains("WeatherDressMath.Swap(wornPieces, old, pieces,")
          && dress.Contains("PutOn(p, offered[i], new List<Apparel> { a })"), "by the pure rule, the swapped piece gone as every replaced piece is");
        C(dress.Contains("kind.apparelRequired.Contains(d)") && dress.Contains("kind.apparelTags.Any(t => tags.Contains(t))") && dress.Contains("r.ApparelDef == d")
          && dress.Contains("tags.Contains(r.RequiredTag)") && dress.Contains("r.AlternateTagChoices.Any("), "what their kind requires or asks for by its tags is theirs to keep");
        C(Code("Camps/CampWeather.cs").Contains("added.AddRange(WeatherDress.Layers(p, low, high, tech, f.HostileTo(Faction.OfPlayer)));")
          && Code("Camps/CampWeather.cs").Contains("WeatherDress.Kept(a, p)"), "camps dress the same way, their winter clothes never over armour");
        string visitors = Code("Visitors/Visitors.cs");
        C(visitors.Contains("WeatherDress.ForStay(p, map, WeatherDressMath.VisitHours(t.hours, t.stayNights));") && visitors.Contains("WeatherDress.AsTheyCame(p);"),
            "Rimfolk's own visitors dress for the whole visit, the robbed come as they were left");
        C(visitors.Contains("public static bool Suffering(Pawn p) => SufferingFrom(p) != WeatherDressMath.Why.None;") && visitors.Contains("WeatherDressMath.Suffers(")
          && visitors.Contains("WeatherDressMath.SafeThrough(p.GetStatValue(StatDefOf.ComfyTemperatureMin), p.GetStatValue(StatDefOf.ComfyTemperatureMax), night.Item1, night.Item2)"),
            "Rimfolk's own leaving follows the same line: hurt by it, or a night out that would hurt them as dressed, by the forecast to first light");
        C(visitors.Contains("bool open = !at.IsValid || !at.InBounds(map) || at.UsesOutdoorTemperature(map);") && visitors.Contains("var night = open ? Night(map) : (0f, 0f);"),
            "the flat eight degrees for the night is gone out in the open: the forecast to first light instead");
        C(Code("Visitors/VisitorWords.cs").Contains("WeatherDressMath.VisitKey(Visitors.WeatherWhy(t))"), "the message says which");
        var keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Visitors.xml"));
        foreach (var key in new[] { "CC_Visit_Weather_cold", "CC_Visit_Weather_heat", "CC_Visit_Weather_hunger", "CC_WeatherLeave_Cold", "CC_WeatherLeave_Heat" })
        {
            var m = Regex.Match(keyed, "<" + key + ">([^<]*)</" + key + ">");
            C(m.Success && !m.Groups[1].Value.Contains("—") && !m.Groups[1].Value.Contains("!") && Regex.Matches(m.Groups[1].Value, @"[.?]").Count == 1, key + ": one plain sentence");
        }
        C(!keyed.Contains("<CC_Visit_Weather>"), "the old weather message is gone");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj")).Contains("Visitors/WeatherDressMath.cs"), "the pure rules are linked into the tests");
    }
}
