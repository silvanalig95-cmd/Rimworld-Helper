using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>One key of a town plan as plain words (Peoples/PeopleDefs.cs reads a TownPlanKey into one).</summary>
    public sealed class PlanKey
    {
        /// <summary>The plan's character for it.</summary>
        public char key;
        /// <summary>A building's defName, or null for a floor (or nothing) alone.</summary>
        public string thing;
        /// <summary>The building's material's defName, or null for the game's own choice for it.</summary>
        public string stuff;
        /// <summary>0 north, 1 east, 2 south, 3 west: a bed's head, a table's long side.</summary>
        public int rot;
        /// <summary>The building's size as the game has it (a bed 1 by 2): the cells it covers from its origin, by the game's own rule.</summary>
        public int sizeX = 1, sizeZ = 1;
        /// <summary>A floor's defName laid under it, or null for the ground as it is.</summary>
        public string floor;
        /// <summary>A roof over it.</summary>
        public bool roof;
        /// <summary>What the cell is for: "store" (the town's goods), "pen" (its herd), "plant:DEFNAME" (a garden bed), or null.</summary>
        public string mark;
    }

    /// <summary>
    /// The pure rules of Rimfolk's own peoples (docs/design/peoples.md; DESIGN.md "Rimfolk's peoples"), with no game types
    /// so Tools/Tests can check them: what each lives by and the tags it gives Rimfolk's words, how much likelier their
    /// caravans, camps and asks are, the titles of their notables, and their towns' plans read into the kept-layout grid the
    /// game's map making places (Colonies/TownLayout.cs).
    /// </summary>
    public static class PeoplesMath
    {
        // ================= What they live by =================

        /// <summary>The road league's, the hospitaller order's and the herding folk's callings (PeopleExtension.calling).</summary>
        public const string Traders = "traders", Healers = "healers", Herders = "herders";

        /// <summary>
        /// Release C's: the pilgrim fellowship's (wandering faithful who keep hostels at the shrines), the learned society's
        /// (scholars who keep an archive), the spacer enclave's (brokers between the towns and the ships above), the river
        /// folk's (fishers and ferry people).
        /// </summary>
        public const string Pilgrims = "pilgrims", Scholars = "scholars", Brokers = "brokers", Fishers = "fishers";

        /// <summary>
        /// Release D's (docs/design/peoples-next.md, the first four): the Lantern Wardens' (beast-hunters of the
        /// crossroads), the Quiet Bell's (keepers of the dead, whose sextons ring for everyone), the Circuit of Tamers'
        /// (beast-tamers who keep bout-rings), the Wheel's (the rolling salvage town).
        /// </summary>
        public const string Hunters = "hunters", Sextons = "sextons", Tamers = "tamers", Salvagers = "salvagers";

        /// <summary>
        /// Release E's (docs/design/peoples-next.md, the second four): the Stormglass Conclave's (weather-seers who read the
        /// sky from a tower of glass), the Mossborn's (grove-keepers who collect a seedling for every tree felled), the
        /// Ashen Banner's (a free company for hire that keeps the roll of its dead), the Kiln Guild's (artisans who mark
        /// everything they make).
        /// </summary>
        public const string Seers = "seers", Grovekeepers = "grovekeepers", Mercenaries = "mercenaries", Artisans = "artisans";

        /// <summary>
        /// Release F's (docs/design/peoples-third.md, the first four): the Signal Line's (keepers of the radio masts), the
        /// Deepwell Union's (drillers of the boomtowns), the Last Republic's (citizens of what is left of the old government),
        /// the Hollow Fleet's (the colony ship's crew, still at their posts).
        /// </summary>
        public const string Linesmen = "linesmen", Drillers = "drillers", Citizens = "citizens", Crew = "crew";

        /// <summary>
        /// Release G's (docs/design/peoples-third.md, the second four): the Helix Works' (gene-crafters, the editors of
        /// every body), the Lattice's (mechanitors who link their mechs into one whole), the Choir of the Spire's (listeners
        /// at the archotech), the Sunken Vault's (the shelter's folk come up into a world that didn't wait for them).
        /// </summary>
        public const string Editors = "editors", Mechanitors = "mechanitors", Listeners = "listeners", Vaultfolk = "vaultfolk";

        /// <summary>Every calling Rimfolk's words know. Release B's peoples take one of these.</summary>
        public static readonly string[] Callings =
        {
            Traders, Healers, Herders, Pilgrims, Scholars, Brokers, Fishers, Hunters, Sextons, Tamers, Salvagers, Seers, Grovekeepers, Mercenaries, Artisans,
            Linesmen, Drillers, Citizens, Crew, Editors, Mechanitors, Listeners, Vaultfolk
        };

        public static bool IsCalling(string calling) => calling != null && Array.IndexOf(Callings, calling) >= 0;

        /// <summary>The tag their people give Rimfolk's lines ("their:healers"), or null for a people of no calling.</summary>
        public static string Tag(string calling) => IsCalling(calling) ? "their:" + calling : null;

        /// <summary>Every such tag, for the talks' and the lines' tests.</summary>
        public static readonly string[] Tags = Callings.Select(c => "their:" + c).ToArray();

        // ================= How often =================

        /// <summary>A kind of camp a people favours (their drovers, their healers on the road) comes from them this much more often.</summary>
        public const float FavouredCamp = 4f;

        /// <summary>An ask, a caller or a world event that prefers a calling weighs a people of it this much more.</summary>
        public const float Prefer = 5f;

        /// <summary>A people's caravans weigh at most this much (the league's 3: many caravans), at least a tenth.</summary>
        public const float MaxCaravanWeight = 5f, MinCaravanWeight = 0.1f;

        public static float CampFactor(bool favoured) => favoured ? FavouredCamp : 1f;

        /// <summary>The weight on a people for something that prefers a calling: <see cref="Prefer"/> for theirs, 1 for any other or when none is preferred.</summary>
        public static float Preference(string wanted, string theirs) => string.IsNullOrEmpty(wanted) ? 1f : wanted == theirs ? Prefer : 1f;

        /// <summary>Something only for a calling (the order's ask for medicine): a people of it, or anyone when it names none.</summary>
        public static bool Fits(string only, string theirs) => string.IsNullOrEmpty(only) || only == theirs;

        /// <summary>A people's caravan weight as the game's choice of whose caravan comes reads it, bounded.</summary>
        public static float CaravanFactor(float weight) => float.IsNaN(weight) || float.IsInfinity(weight) ? 1f : Math.Max(MinCaravanWeight, Math.Min(MaxCaravanWeight, weight));

        // ================= Their ways =================

        /// <summary>
        /// A notable's title by their people's ways ("head", "heir", "envoy", "trader"): the people's own word, the female
        /// one where it has one, else null (the age's word, as for every other people).
        /// </summary>
        public static string Title(string role, bool female, string head, string headFemale, string heir, string heirFemale, string envoy, string trader)
        {
            switch (role)
            {
                case "head": return female && !string.IsNullOrEmpty(headFemale) ? headFemale : Empty(head);
                case "heir": return female && !string.IsNullOrEmpty(heirFemale) ? heirFemale : Empty(heir);
                case "envoy": return Empty(envoy);
                case "trader": return Empty(trader);
                default: return null;
            }
        }

        private static string Empty(string s) => string.IsNullOrEmpty(s) ? null : s;

        /// <summary>A sickhouse of the hospitallers sees to the colony's travellers who lodge there, as medicine in their packs would (Harm/SickHome.cs).</summary>
        public static bool TendsLodgers(string calling) => calling == Healers;

        /// <summary>A plague year: a sickness among the region's running world events (the kind the order asks for medicine in).</summary>
        public static bool PlagueYear(IEnumerable<string> runningKinds) => runningKinds != null && runningKinds.Any(k => k == "sickness");

        /// <summary>A holy year: a world event of the faith among the region's (a great pilgrimage, a holy year, a prophet on the roads).</summary>
        public static bool HolyYear(IEnumerable<string> runningKinds) => runningKinds != null && runningKinds.Any(k => k == "faith");

        // ================= The ages a template arises in =================

        /// <summary>
        /// The ages (0 tribal to 3 spacer) a people of this template may arise in: its own age, or the span its template
        /// gives (the river folk from the tribes to the industrial towns; the pilgrims and the scholars from the medieval
        /// towns on). A span that is unset or upside down is the template's own age.
        /// </summary>
        public static void AgeSpan(int own, int min, int max, out int lo, out int hi)
        {
            if (min < 0 || max < 0 || max < min)
            {
                lo = hi = own;
                return;
            }
            lo = Math.Max(0, Math.Min(3, min));
            hi = Math.Max(lo, Math.Min(3, max));
        }

        /// <summary>How many ages a cause's age lies outside a template's span: 0 within it.</summary>
        public static int AgeDistance(int age, int lo, int hi) => age < lo ? lo - age : age > hi ? age - hi : 0;

        // ================= The pilgrims' night =================

        /// <summary>A band of pilgrims asks for a night's shelter at dusk: from this hour to this one.</summary>
        public const int ShelterFrom = 16, ShelterTo = 22;

        public static bool ShelterHour(int hour) => hour >= ShelterFrom && hour <= ShelterTo;

        /// <summary>A band of three to five, the eldest speaking for them.</summary>
        public const int BandMin = 3, BandMax = 5;

        /// <summary>
        /// One night in ten a pilgrim on the road has brought a cough in with them, and the colonist who sat up with the
        /// band catches it (before the harm setting's scale): about the chance of a night's lodging among strangers.
        /// </summary>
        public const float CoughChance = 0.1f;

        /// <summary>Hours from <paramref name="hour"/> to first light (VisitorServices.Dawn), the night the band stays.</summary>
        public static int HoursToLight(int hour, int dawn) => hour < dawn ? dawn - hour : 24 - hour + dawn;

        // ================= The enclave's lander =================

        /// <summary>The enclave's factor comes down with two to four of its marshals as guards.</summary>
        public const int LanderGuardsMin = 2, LanderGuardsMax = 4;

        /// <summary>Guards for a lander by the colony's wealth factor (0.6 to 2: a richer colony sees more of them).</summary>
        public static int LanderGuards(float wealthFactor)
        {
            if (float.IsNaN(wealthFactor)) wealthFactor = 1f;
            float f = Math.Max(0f, Math.Min(1f, (wealthFactor - 0.6f) / 1.4f));
            return LanderGuardsMin + (int)Math.Round(f * (LanderGuardsMax - LanderGuardsMin));
        }

        /// <summary>The lander is for a colony of the industrial age or later (2 or 3), which knows what comes down out of the sky.</summary>
        public static bool LanderAge(int colonyAge) => colonyAge >= 2;

        // ================= The river folk's landing =================

        /// <summary>A river landing counts as by the water when at least this share of its docks stands in water.</summary>
        public const float DockWater = 0.25f;

        public static bool ByWater(int dockWater, int dockCells) => dockCells > 0 && dockWater > 0 && dockWater >= Math.Ceiling(dockCells * DockWater);

        /// <summary>
        /// What a spot is worth for a plan with docks: -1 when the rest of the plan's ground won't take it (more than a
        /// little bad ground off the docks); else how many of its docks stand in water (the more, the better).
        /// </summary>
        public static int DockSpot(int dockWater, int otherBad, int otherCells) => LandFits(otherBad, otherCells) ? Math.Max(0, dockWater) : -1;

        // ================= Their towns =================

        /// <summary>The share of a plan's cells that may be water, marsh or bare rock floor too rough to build on before the game's own base is made instead.</summary>
        public const float MaxBadLand = 0.06f;

        public static bool LandFits(int bad, int cells) => cells > 0 && bad >= 0 && bad <= cells * MaxBadLand;

        /// <summary>
        /// The cells a building of this size covers from its origin at (x, z) turned <paramref name="rot"/>, by the game's own
        /// rule (GenAdj.OccupiedRect): its corner and size.
        /// </summary>
        public static void Footprint(int x, int z, int rot, int sizeX, int sizeZ, out int minX, out int minZ, out int w, out int h)
        {
            rot &= 3;
            if (sizeX < 1) sizeX = 1;
            if (sizeZ < 1) sizeZ = 1;
            if (rot == 1 || rot == 3)
            {
                int t = sizeX;
                sizeX = sizeZ;
                sizeZ = t;
            }
            int cx = x, cz = z;
            if (sizeX != 1 || sizeZ != 1)
            {
                int ox = rot == 2 || rot == 3 ? -1 : 0;
                int oz = rot == 1 || rot == 2 ? -1 : 0;
                if (sizeX % 2 == 0) cx += ox;
                if (sizeZ % 2 == 0) cz += oz;
            }
            minX = cx - (sizeX - 1) / 2;
            minZ = cz - (sizeZ - 1) / 2;
            w = sizeX;
            h = sizeZ;
        }

        /// <summary>A plan's character for the ground left as it is (a space reads the same, where the text keeps it).</summary>
        public const char Ground = '_';

        public static bool IsGround(char c) => c == Ground || c == ' ';

        /// <summary>A plan's row (from the top, north) as the map's z within the plan.</summary>
        public static int RowZ(int row, int height) => height - 1 - row;

        /// <summary>
        /// What is wrong with a plan: rows of different lengths, characters with no key, a key given twice, a building
        /// that would reach past the plan or over another, or over a cell whose own key is a building. Empty when it is
        /// sound. <see cref="Ground"/> is the ground left as it is.
        /// </summary>
        public static List<string> Check(IList<string> rows, IList<PlanKey> keys)
        {
            var errors = new List<string>();
            if (rows == null || rows.Count == 0)
            {
                errors.Add("no rows");
                return errors;
            }
            var byKey = new Dictionary<char, PlanKey>();
            foreach (var k in keys ?? new List<PlanKey>())
            {
                if (k == null) continue;
                if (IsGround(k.key)) errors.Add("a key for '" + k.key + "', which is the ground as it is");
                else if (byKey.ContainsKey(k.key)) errors.Add("the key '" + k.key + "' is given twice");
                else byKey[k.key] = k;
            }
            int width = rows[0]?.Length ?? 0, height = rows.Count;
            for (int r = 0; r < height; r++)
                if ((rows[r]?.Length ?? 0) != width) errors.Add("row " + r + " is " + (rows[r]?.Length ?? 0) + " long, not " + width);
            if (errors.Count > 0) return errors;
            var taken = new Dictionary<(int, int), (int, int)>();
            for (int r = 0; r < height; r++)
                for (int c = 0; c < width; c++)
                {
                    char ch = rows[r][c];
                    if (IsGround(ch)) continue;
                    if (!byKey.TryGetValue(ch, out var k))
                    {
                        errors.Add("no key for '" + ch + "' at row " + r + ", column " + c);
                        continue;
                    }
                    if (string.IsNullOrEmpty(k.thing)) continue;
                    int z = RowZ(r, height);
                    Footprint(c, z, k.rot, k.sizeX, k.sizeZ, out int mx, out int mz, out int w, out int h);
                    for (int x = mx; x < mx + w; x++)
                        for (int zz = mz; zz < mz + h; zz++)
                        {
                            if (x < 0 || zz < 0 || x >= width || zz >= height)
                            {
                                errors.Add("'" + ch + "' at row " + r + ", column " + c + " reaches past the plan");
                                continue;
                            }
                            if (taken.TryGetValue((x, zz), out var by)) errors.Add("'" + ch + "' at row " + r + ", column " + c + " overlaps the one at row " + by.Item1 + ", column " + by.Item2);
                            else taken[(x, zz)] = (r, c);
                            if (x == c && zz == z) continue;
                            char other = rows[RowZ(zz, height)][x];
                            if (IsGround(other) || byKey.TryGetValue(other, out var ok) && !string.IsNullOrEmpty(ok.thing))
                                errors.Add("'" + ch + "' at row " + r + ", column " + c + " covers '" + other + "', which should be a floor of its own");
                        }
                }
            return errors;
        }

        /// <summary>
        /// The plan read into the kept-layout grid (Colonies/TownAfterMath.cs), its corner at (originX, originZ) on a map of
        /// this size: each building at its origin with its rotation, the floors, the roofs, and every cell the plan uses
        /// marked clear of the land's rock (the ground is left as it is). <paramref name="marks"/> gets each mark's
        /// cells (their index in the grid). A plan that isn't sound gives null.
        /// </summary>
        public static LayoutGrid Grid(IList<string> rows, IList<PlanKey> keys, int originX, int originZ, int mapX, int mapZ, Dictionary<string, List<int>> marks)
        {
            if (Check(rows, keys).Count > 0) return null;
            var byKey = keys.Where(k => k != null).ToDictionary(k => k.key);
            int width = rows[0].Length, height = rows.Count;
            var g = LayoutGrid.Make(originX, originZ, width, height, mapX, mapZ);
            var index = new Dictionary<string, int>();
            for (int r = 0; r < height; r++)
                for (int c = 0; c < width; c++)
                {
                    char ch = rows[r][c];
                    if (IsGround(ch)) continue;
                    var k = byKey[ch];
                    int i = g.Index(c, RowZ(r, height));
                    byte flag = LayoutMath.Clear;
                    if (k.roof) flag |= LayoutMath.Roof;
                    g.flags[i] = flag;
                    if (!string.IsNullOrEmpty(k.floor)) g.floors[i] = (ushort)LayoutMath.Add(g, LayoutMath.TerrainEntry(k.floor), index);
                    if (!string.IsNullOrEmpty(k.thing))
                    {
                        int no = LayoutMath.Add(g, LayoutMath.BuildingEntry(k.thing, k.stuff), index);
                        if (no > 0) g.things[i] = LayoutMath.Pack(no, k.rot);
                    }
                    if (marks != null && !string.IsNullOrEmpty(k.mark))
                    {
                        if (!marks.TryGetValue(k.mark, out var list)) marks[k.mark] = list = new List<int>();
                        list.Add(i);
                    }
                }
            return g;
        }

        /// <summary>The plan's corner for a town whose middle is at (centreX, centreZ), moved in to fit a map of this size.</summary>
        public static void Corner(int centreX, int centreZ, int width, int height, int mapX, int mapZ, out int x, out int z)
        {
            x = LayoutMath.Shift(Math.Max(0, centreX - width / 2), width, mapX);
            z = LayoutMath.Shift(Math.Max(0, centreZ - height / 2), height, mapZ);
        }

        /// <summary>How many of a town's herd stand in its pens: as many as the pens hold with room to graze (one beast to four cells), within the people's own count.</summary>
        public static int HerdCount(int penCells, int min, int max, float roll)
        {
            if (penCells <= 0 || max <= 0) return 0;
            int want = min + (int)Math.Floor(Math.Max(0f, Math.Min(0.999f, roll)) * (Math.Max(min, max) - min + 1));
            return Math.Max(0, Math.Min(want, penCells / 4));
        }
    }
}
