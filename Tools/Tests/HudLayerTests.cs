using ColonyCouncil;

/// <summary>
/// The on-screen panels' fourth round (Source/ColonyCouncil/Hud/): where the game's own interface sits over the panels
/// (the inspect pane's tab row, the letters' column), where the panels start beside the inventory and clear of the
/// game's readouts, the health panel's "how far along" (healed, the game's healing shared among wounds, the days to
/// heal, to pass and to recover, rough times), and the inventory panel's rules (its orders, "yesterday", the change,
/// the packed snapshots, short counts, the icon grid).
/// </summary>
public static class HudLayerTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "on-screen panels: " + what);
        }
        bool Near(float a, float b, float eps = 0.01f) => Math.Abs(a - b) < eps;

        // ---------- Whoever draws the inspect tabs, the whole row is the game's ----------
        var all = HudMath.TabRowAll(432f, 880f, 7, 30f, 1800f);
        Check(Near(all.x, 0f) && all.XMax >= 7 * 92f && Near(all.YMax, 880f), "seven tabs a mod draws wider (RimHUD's) from the left edge are all covered");
        var few = HudMath.TabRowAll(432f, 880f, 2, 30f, 1800f);
        Check(Near(few.x, 0f) && Near(few.w, 432f), "a couple of tabs: at least the pane's width");
        var capped = HudMath.TabRowAll(432f, 880f, 30, 30f, 1000f);
        Check(capped.XMax <= 1000f + 0.01f, "never past the letters' column");

        // ---------- Never past the bottom of the screen: the rest scrolls ----------
        var tall = HudMath.FitOnScreen(new HudBox(0f, 0f, 230f, 1400f), 1400f, 1080f);
        Check(Near(tall.h, 1080f - HudMath.BottomReserve) && Near(tall.y, 0f), "a list taller than the screen stops at the game's button bar");
        var fits = HudMath.FitOnScreen(new HudBox(0f, 100f, 230f, 400f), 400f, 1080f);
        Check(Near(fits.h, 400f) && Near(fits.y, 100f), "one that fits keeps its size and place");
        var low = HudMath.FitOnScreen(new HudBox(0f, 1000f, 230f, 400f), 400f, 1080f);
        Check(low.h >= HudMath.MinScrollHeight - 0.01f && low.YMax <= 1080f - HudMath.BottomReserve + 0.01f, "one placed too low moves up to keep room for a few rows");
        Check(Near(HudMath.ClampScroll(-5f, 1400f, 1041f), 0f) && Near(HudMath.ClampScroll(9999f, 1400f, 1041f), 359f) && Near(HudMath.ClampScroll(50f, 300f, 400f), 0f),
            "scrolling stays between the top and the content's overflow, and doesn't when it fits");

        // ---------- The game's interface over the panels ----------
        var row = HudMath.TabRow(432f, 880f, 3, false, 72f, 30f);
        Check(Near(row.x, 432f - 216f) && Near(row.XMax, 432f) && Near(row.y, 850f) && Near(row.h, 30f),
            "three closed tabs: the row runs right to left from the pane's right edge, above the pane");
        var open = HudMath.TabRow(432f, 880f, 3, true, 72f, 30f);
        Check(Near(open.x, 0f) && Near(open.w, 432f), "with a tab open the game fills the row to the left edge");
        var many = HudMath.TabRow(432f, 880f, 9, false, 72f, 30f);
        Check(Near(many.x, 0f), "more tabs than fit: the row never starts left of the screen");
        Check(HudMath.TabRow(432f, 880f, 0, false, 72f, 30f).w <= 0f, "no tabs, no row");
        var col = HudMath.RightColumn(1920f, 1080f, 700f, 200f);
        Check(Near(col.x, 1720f) && Near(col.y, 696f) && Near(col.YMax, 1080f), "the letters and the controls under them: the right column from the top letter down");
        Check(Near(HudMath.RightColumn(1920f, 1080f, -1f, 200f).YMax, 1080f), "an unknown letter line keeps the column at the bottom");

        // ---------- Where the panels start ----------
        foreach (var (sw, sh) in new[] { (1920f, 1080f), (2560f, 1440f) })
        {
            HudMath.DefaultPlace(HudIds.Inventory, sw, sh, out float ix, out float iy, out bool ir, out bool ib, out bool ia);
            var inv = HudMath.FromFraction(ix, iy, ir, ib, 126f, 8f + 25 * 20f + 16f, sw, sh);
            Check(Near(inv.x, 0f) && Near(inv.y, 0f) && !ia, $"the inventory starts in the game's resource list place, top left ({sw}x{sh})");
            foreach (var id in new[] { HudIds.Moods, HudIds.Trend })
            {
                HudMath.DefaultPlace(id, sw, sh, out float fx, out float fy, out bool right, out bool bottom, out _);
                var b = HudMath.FromFraction(fx, fy, right, bottom, 226f, id == HudIds.Trend ? 100f : 8f + 10 * 34f + 16f, sw, sh);
                Check(b.x >= HudMath.ResourceListWidth, $"{id} starts clear of the resource list and the inventory ({sw}x{sh})");
                Check(!HudMath.Overlap(b, inv), $"{id} and the inventory don't overlap ({sw}x{sh})");
                // The inspect pane (165 tall) and its tab row (30) above the main buttons (35).
                Check(b.YMax <= sh - 35f - 165f - 30f, $"{id} starts above the inspect pane's tab row ({sw}x{sh})");
            }
        }

        // ---------- Health: how far along ----------
        Check(Near(HudHealthMath.Healed(10f, 4f), 0.6f), "a wound of 10 now at 4 is 60% healed");
        Check(HudHealthMath.Healed(0f, 4f) == 0f && HudHealthMath.Healed(5f, 7f) == 0f && HudHealthMath.Healed(5f, 0f) == 1f,
            "healed stays between nothing and all");
        float standing = HudHealthMath.NaturalHealPerDay(false, 0f, 1f, 1f, 1f, false);
        float inBed = HudHealthMath.NaturalHealPerDay(true, 6f, 1f, 1f, 1f, false);
        Check(Near(standing, 8f) && Near(inBed, 18f), "the game's healing: 8 a day up and about, 12 lying down plus the bed's own");
        Check(HudHealthMath.NaturalHealPerDay(true, 6f, 1f, 1f, 1f, true) == 0f, "no healing while starving");
        Check(Near(HudHealthMath.NaturalHealPerDay(false, 0f, 0.5f, 1.2f, 1f, false), 4.8f), "conditions and healing speed scale it");
        Check(Near(HudHealthMath.TendedHealPerDay(0f, 1f, 1f, false), 4f) && Near(HudHealthMath.TendedHealPerDay(1f, 1f, 1f, false), 12f),
            "tended wounds: half to one and a half times 8 by the tending's quality");
        float share = HudHealthMath.WoundHealPerDay(8f, 2, true, 12f, 1, true);
        Check(Near(share, 16f), "a tended wound among two healing: half the natural healing and all the tended");
        Check(Near(HudHealthMath.WoundHealPerDay(8f, 2, true, 12f, 1, false), 4f), "an untended wound gets only its share of the natural healing");
        Check(Near(HudHealthMath.DaysToHeal(8f, 16f), 0.5f) && HudHealthMath.DaysToHeal(5f, 0f) < 0f && HudHealthMath.DaysToHeal(0f, 0f) == 0f,
            "days to heal: what is left over the healing a day; never when nothing heals it");
        Check(Near(HudHealthMath.DaysToPass(0.3f, -0.1f), 3f) && HudHealthMath.DaysToPass(0.3f, 0.05f) < 0f,
            "an ailment easing 0.1 a day from 0.3 has three days left; one getting worse has no end in sight");
        float flu = HudHealthMath.DaysToRecover(0.6f, 0.2f, 0.4f, 0.1f, -0.3f, 1f);
        Check(Near(flu, 2f + 0.6f / 0.3f), "recovery: until immune at today's pace, then until the severity has gone at its pace once immune");
        Check(HudHealthMath.DaysToRecover(0.2f, 0.1f, 0.8f, 0.2f, -0.3f, 1f) < 0f, "it kills before immunity comes: no recovery to promise");
        Check(HudHealthMath.DaysToRecover(0.5f, 0f, 0.3f, 0.1f, -0.3f, 1f) < 0f, "no immunity gained: no recovery to promise");
        Check(Near(HudHealthMath.DaysToRecover(1f, 0f, 0.3f, -0.3f, -0.3f, 1f), 1f), "already immune: only the easing is left");
        Check(HudHealthMath.RoughTime(0.3f, "~{0} h", "~{0} d") == "~7 h" && HudHealthMath.RoughTime(0.01f, "~{0} h", "~{0} d") == "~1 h",
            "under a day in whole hours, at least one");
        Check(HudHealthMath.RoughTime(1.54f, "~{0} h", "~{0} d") == "~1.5 d" && HudHealthMath.RoughTime(12.4f, "~{0} h", "~{0} d") == "~12 d"
                                                                             && HudHealthMath.RoughTime(-1f, "~{0} h", "~{0} d") == "",
            "under ten days to a tenth, then whole days; nothing for no end");

        // ---------- The inventory: order ----------
        List<StockEntry> Stock() => new List<StockEntry>
        {
            new StockEntry { key = "Steel", label = "steel", count = 400, group = 2, order = 5, hasChange = true, change = -30 },
            new StockEntry { key = "MealSimple", label = "simple meal", count = 24, group = 0, order = 1, hasChange = true, change = 6 },
            new StockEntry { key = "Silver", label = "silver", count = 900, group = 3, order = 8, hasChange = true, change = 30 },
            new StockEntry { key = "Herbal", label = "herbal medicine", count = 12, group = 1, order = 3, hasChange = false, pinned = true },
            new StockEntry { key = "WoodLog", label = "wood", count = 400, group = 2, order = 4, hasChange = true, change = 0 }
        };
        string Keys(List<StockEntry> l) => string.Join(",", l.Select(e => e.key));
        var s1 = Stock();
        HudInventoryMath.Sort(s1, HudSort.Category);
        Check(Keys(s1) == "Herbal,MealSimple,WoodLog,Steel,Silver", "by category: pinned first, then the game's categories and its own order in them");
        var s2 = Stock();
        HudInventoryMath.Sort(s2, HudSort.Name);
        Check(Keys(s2) == "Herbal,Silver,MealSimple,Steel,WoodLog", "by name: pinned first, then a to z");
        var s3 = Stock();
        HudInventoryMath.Sort(s3, HudSort.Count);
        Check(Keys(s3) == "Herbal,Silver,WoodLog,Steel,MealSimple", "by count: the most first, the same count in the game's category order");
        var s4 = Stock();
        HudInventoryMath.Sort(s4, HudSort.Change);
        Check(Keys(s4) == "Herbal,Silver,Steel,MealSimple,WoodLog", "by change: the biggest first, a gain before a loss of the same size, unknown last");

        // ---------- The inventory: the weapons and apparel in storage, at the bottom (the player: "at the bottom perhaps") ----------
        const int W = HudInventoryMath.TailWeapons, A = HudInventoryMath.TailApparel;
        List<StockEntry> Armed()
        {
            var l = Stock();
            l.Add(new StockEntry { key = "Apparel_Parka", label = "parka", count = 4, group = 8, order = 22, tail = A, kind = HudInventoryMath.FilterClothes });
            l.Add(new StockEntry { key = "Gun_Revolver", label = "revolver", count = 3, group = 7, order = 20, tail = W, kind = HudInventoryMath.FilterRanged, hasChange = true, change = 1 });
            l.Add(new StockEntry { key = "MeleeWeapon_Club", label = "club", count = 2000, group = 7, order = 21, tail = W, kind = HudInventoryMath.FilterMelee, hasChange = true, change = 900 });
            l.Add(new StockEntry { key = "Apparel_FlakVest", label = "flak vest", count = 1, group = 8, order = 23, tail = A, kind = HudInventoryMath.FilterArmour });
            l.Add(new StockEntry { key = "Gun_Autopistol", label = "autopistol", count = 1, group = 7, order = 19, tail = W, kind = HudInventoryMath.FilterRanged, pinned = true });
            return l;
        }
        foreach (var sort in new[] { HudSort.Category, HudSort.Name, HudSort.Count, HudSort.Change })
        {
            var a1 = Armed();
            HudInventoryMath.Sort(a1, sort);
            Check(a1.Count == 10 && a1.Take(2).All(e => e.pinned) && a1.Take(2).Any(e => e.key == "Gun_Autopistol") && a1.Skip(2).Take(4).All(e => e.tail == 0)
                  && a1.Skip(6).Take(2).All(e => e.tail == W) && a1.Skip(8).All(e => e.tail == A),
                "by " + sort + ": the pinned at the top, then the stock, the weapons and the apparel, each below the last");
        }
        var a2 = Armed();
        HudInventoryMath.Sort(a2, HudSort.Name);
        Check(Keys(a2).EndsWith("WoodLog,MeleeWeapon_Club,Gun_Revolver,Apparel_FlakVest,Apparel_Parka"), "the weapons and apparel keep the panel's order among themselves (by name)");
        var a3 = Armed();
        HudInventoryMath.Sort(a3, HudSort.Category);
        Check(Keys(a3).EndsWith("Silver,Gun_Revolver,MeleeWeapon_Club,Parka".Replace("Parka", "Apparel_Parka,Apparel_FlakVest")),
            "by category they keep the order given when gathered");
        Check(HudInventoryMath.KindRank(HudInventoryMath.FilterRanged) < HudInventoryMath.KindRank(HudInventoryMath.FilterMelee)
              && HudInventoryMath.KindRank(HudInventoryMath.FilterArmour) < HudInventoryMath.KindRank(HudInventoryMath.FilterUtility)
              && HudInventoryMath.KindRank(HudInventoryMath.FilterUtility) < HudInventoryMath.KindRank(HudInventoryMath.FilterClothes),
            "gathered ranged before melee, armour before utility before clothes");
        foreach (var mark in new[] { HudInventoryMath.WeaponsMark, HudInventoryMath.ApparelMark })
            Check(mark.IndexOfAny(new[] { ',', ':', '|' }) < 0 && mark.StartsWith("~"), "the snapshot's mark " + mark + " packs as a key and is never a def's name");
        Check(HudInventoryMath.Mark(W) == HudInventoryMath.WeaponsMark && HudInventoryMath.Mark(A) == HudInventoryMath.ApparelMark, "each tail has its own mark");
        var marked = HudInventoryMath.Pack(1, 10, new[] { new KeyValuePair<string, int>("Gun_Revolver", 3), new KeyValuePair<string, int>(HudInventoryMath.WeaponsMark, 1) });
        var markedBack = new Dictionary<string, int>();
        Check(HudInventoryMath.Unpack(marked, out _, out _, markedBack) && markedBack.ContainsKey(HudInventoryMath.WeaponsMark) && markedBack["Gun_Revolver"] == 3,
            "a snapshot with the weapons keeps its mark through the save");

        // ---------- The inventory: the filters (the player: "perhaps i can then have filters for those as well") ----------
        string F(string f, int tail, string kind, bool part) => HudInventoryMath.FilterShows(f, tail, kind, part) ? "y" : "n";
        Check(F("", 0, null, true) + F("", W, "ranged", true) + F("", W, "ranged", false) + F("", A, "clothes", true) == "yyny",
            "everything: the stock always, the weapons and apparel where their part is on");
        Check(F("stock", 0, null, true) + F("stock", W, "melee", true) + F("stock", A, "armour", true) == "ynn", "the stock alone");
        Check(F("weapons", W, "ranged", false) + F("weapons", W, "melee", true) + F("weapons", 0, null, true) + F("weapons", A, "armour", true) == "yynn",
            "weapons: every weapon, even with its part off, nothing else");
        Check(F("ranged", W, "ranged", true) + F("ranged", W, "melee", true) + F("melee", W, "melee", true) == "yny", "ranged or melee alone");
        Check(F("apparel", A, "utility", false) + F("armour", A, "armour", true) + F("armour", A, "clothes", true) + F("utility", A, "utility", true)
              + F("clothes", A, "clothes", true) + F("clothes", W, "clothes", true) == "yynyyn", "apparel, and armour, utility or clothes alone");
        Check(HudInventoryMath.Family("ranged") == "weapons" && HudInventoryMath.Family("clothes") == "apparel" && HudInventoryMath.Family("weapons") == "weapons"
              && HudInventoryMath.Family("nonsense") == "" && HudInventoryMath.Filters[0] == "", "each kind belongs to its family; an unknown filter is everything");
        Check(HudInventoryMath.Filters.Length == 9 && HudInventoryMath.Filters.Distinct().Count() == 9, "nine filters, each once");
        Check(HudInventoryMath.ApparelKind("body armour", false, false) == "armour" && HudInventoryMath.ApparelKind("helmet", false, false) == "armour"
              && HudInventoryMath.ApparelKind("off-hand shield", false, false) == "armour" && HudInventoryMath.ApparelKind("utility", true, false) == "utility"
              && HudInventoryMath.ApparelKind("everyday clothing", true, true) == "clothes" && HudInventoryMath.ApparelKind("cold-weather gear", false, false) == "clothes",
            "an apparel's kind by the rating's role where it has one");
        Check(HudInventoryMath.ApparelKind(null, true, false) == "armour" && HudInventoryMath.ApparelKind(null, false, true) == "utility"
              && HudInventoryMath.ApparelKind("", false, false) == "clothes", "else by the game's categories, clothes otherwise");

        // ---------- The inventory: yesterday and the change ----------
        var hours = new List<int> { 100, 106, 112, 118, 124 };
        Check(HudInventoryMath.Yesterday(hours, 130) == 1, "yesterday is the newest snapshot at least a day old (106 for hour 130)");
        Check(HudInventoryMath.Yesterday(hours, 123) == -1, "less than a day of snapshots: no yesterday yet");
        Check(HudInventoryMath.Yesterday(hours, 200) == -1, "every snapshot far too old (the game skipped ahead): no yesterday");
        Check(HudInventoryMath.SnapshotDue(-1, 5) && HudInventoryMath.SnapshotDue(100, 106) && !HudInventoryMath.SnapshotDue(100, 105) && HudInventoryMath.SnapshotDue(100, 90),
            "a snapshot every six hours, and at once when there's none or the clock went back");
        var then = new Dictionary<string, int> { ["Steel"] = 430 };
        Check(HudInventoryMath.Change(400, then, "Steel") == -30 && HudInventoryMath.Change(12, then, "Herbal") == 12, "the change is now less then; nothing then counts as none");
        Check(HudInventoryMath.Signed(12) == "+12" && HudInventoryMath.Signed(-5) == "−5" && HudInventoryMath.Signed(0) == "", "the change reads +12 or −5, and nothing for none");
        Check(HudInventoryMath.ShortCount(950) == "950" && HudInventoryMath.ShortCount(4250) == "4.2k" && HudInventoryMath.ShortCount(12900) == "12k"
              && HudInventoryMath.ShortCount(1340000) == "1.3M", "short counts for small tiles");

        // ---------- The inventory: snapshots in the save ----------
        var counts = new List<KeyValuePair<string, int>>
        {
            new KeyValuePair<string, int>("Steel", 400), new KeyValuePair<string, int>("Silver", 0), new KeyValuePair<string, int>("Bad,Name", 3),
            new KeyValuePair<string, int>("MealSimple", 24)
        };
        string line = HudInventoryMath.Pack(7, 130, counts);
        var back = new Dictionary<string, int>();
        Check(HudInventoryMath.Unpack(line, out int map, out int hour, back) && map == 7 && hour == 130, "a snapshot reads back its map and hour");
        Check(back.Count == 2 && back["Steel"] == 400 && back["MealSimple"] == 24, "it keeps only what there's some of, and no name it can't read back");
        Check(!HudInventoryMath.Unpack("rubbish", out _, out _, back) && !HudInventoryMath.Unpack(null, out _, out _, back), "a line that isn't a snapshot is refused");
        Check(HudInventoryMath.Unpack(HudInventoryMath.Pack(1, 2, null), out _, out _, back) && back.Count == 0, "an empty snapshot reads back empty");

        // ---------- The inventory: the icon grid ----------
        Check(HudInventoryMath.GridRows(17, 8) == 3 && HudInventoryMath.GridRows(0, 8) == 0 && HudInventoryMath.GridRows(5, 0) == 5, "rows: as many as the tiles need");
        HudInventoryMath.GridCell(9, 8, 38f, 40f, 2f, out float cx, out float cy);
        Check(Near(cx, 40f) && Near(cy, 42f), "the tenth tile of eight to a row: second in the second row");
        HudInventoryMath.GridSize(17, 8, 38f, 40f, 2f, out float gw, out float gh);
        Check(Near(gw, 8 * 38f + 7 * 2f) && Near(gh, 3 * 40f + 2 * 2f), "the grid's size: eight columns, three rows, gaps between");
        HudInventoryMath.GridSize(3, 8, 38f, 40f, 2f, out float nw, out _);
        Check(Near(nw, 3 * 38f + 2 * 2f), "fewer tiles than a row: no empty columns");
        return cases;
    }
}
