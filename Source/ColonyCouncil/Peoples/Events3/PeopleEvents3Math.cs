using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the peoples' own events, round 3 (docs/peoples-events-proposals.md, items 2, 4, 6, 7, 8, 9, 11, 13, 15
    /// and 16; Peoples/Events3/): the league's red ink and what a wrong costs to put right, the road gang's road by its age
    /// and its stretch a day, the order's litters after a raid, its sick room and its field sickhouse, the herders' drove
    /// across the land, the thieves' trail, the spring calving, the pilgrims' old shrine and the two faiths at one table.
    /// Shared with the tests (Tools/Tests/PeopleEvents3Tests.cs); nothing here knows the game.
    /// </summary>
    public static class PeopleEvents3Math
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        public static int Round10(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0 : (int)Math.Round(v / 10f) * 10;

        private static float Clamp(float v, float lo, float hi) => float.IsNaN(v) ? lo : Math.Max(lo, Math.Min(hi, v));

        private static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));

        // ================= 2. Red ink (the road league) =================

        /// <summary>The wrongs the league writes in red: the colony attacked a people's traders, or their caravan on the road.</summary>
        public static readonly string[] RedInkKinds = { "robbed", "caravanRaided" };

        /// <summary>A killing counts when it came with the robbing: a "killed" deed this close (in days) to one of the wrongs above.</summary>
        public const float KilledWithinDays = 2f;

        /// <summary>A killing is part of the robbing when it came this close to it.</summary>
        public static bool KilledInRobbing(int killedTick, int wrongTick) => Math.Abs(killedTick - wrongTick) <= KilledWithinDays * DayTicks;

        /// <summary>The factor comes no sooner than this after the wrong (the book is written up first), and not for one older than this.</summary>
        public const int RedInkMinDays = 2, RedInkMaxDays = 120;

        /// <summary>The wrong is one the league comes about: its age in days within the book's reach.</summary>
        public static bool InBook(float ageDays) => !float.IsNaN(ageDays) && ageDays >= RedInkMinDays && ageDays <= RedInkMaxDays;

        /// <summary>
        /// What paying the wronged people back costs: by the deed's size (its weight) and how many it was about, at the
        /// colony's wealth: twelve silver a point of weight, more for a run of them, within 80 and 1500.
        /// </summary>
        public static int Restitution(int weight, int count, float wealthFactor)
        {
            float w = Math.Max(1, weight) * 12f * (float)Math.Sqrt(Clamp(count, 1, 6)) * Clamp(wealthFactor, 0.6f, 2f);
            return Math.Max(80, Math.Min(1500, Round10(w)));
        }

        /// <summary>Paid back, part of the wronged people's goodwill returns: half the deed's weight, within 3 and 15.</summary>
        public static int GoodwillBack(int weight) => Clamp(Math.Max(0, weight) / 2, 3, 15);

        /// <summary>Arguing the entry: the risk that it is written twice, lowered by the talker's Social (3.5% a level, as answers' risks are).</summary>
        public const float ArgueRisk = 0.7f;

        /// <summary>Written twice: the sum owed doubles.</summary>
        public static int Twice(int owed) => Math.Max(0, owed) * 2;

        /// <summary>Refused (or written twice and not paid): every league trades with the colony at worse terms until it pays.</summary>
        public const float WorseTerms = -0.05f;

        /// <summary>The league's thanks when the book is cleared by paying.</summary>
        public const int PaidGoodwill = 2;

        /// <summary>The colony's red ink the record keeps (open ones and the last few closed).</summary>
        public const int MaxRedInk = 6;

        // ================= 4. The road gang (the road league) =================

        /// <summary>The road's floor by the league's age: packed earth, flagstones of the land's stone, concrete, paved tile.</summary>
        public static string RoadFloor(int age, string stone)
        {
            if (age <= 0) return "PackedDirt";
            if (age == 1) return "Flagstone" + (string.IsNullOrEmpty(stone) ? "Granite" : stone);
            return age == 2 ? "Concrete" : "PavedTile";
        }

        /// <summary>The road is two cells wide; a spur to the gate one.</summary>
        public const int RoadWidth = 2, SpurWidth = 1;

        /// <summary>How many of the road's cells are to be laid by the end of day <paramref name="day"/> (0 the first) of <paramref name="days"/>: a stretch a day.</summary>
        public static int Stretch(int total, int days, int day)
        {
            if (total <= 0) return 0;
            int d = Math.Max(1, days);
            int k = Clamp(day + 1, 0, d);
            return Math.Min(total, (int)Math.Ceiling(total * k / (double)d));
        }

        /// <summary>Each turn the road makes costs this much more than a straight step, so it runs straight where it can.</summary>
        public const float TurnCost = 3f;

        /// <summary>
        /// The road's way across a map of <paramref name="w"/> by <paramref name="h"/> cells, from one cell to another, four
        /// ways only (nobody lays a road on the diagonal), by the cheapest way: <paramref name="cost"/> of a cell (below zero: no
        /// road there; at least one), a turn <see cref="TurnCost"/> dearer. Null when there's no way.
        /// </summary>
        public static List<(int x, int z)> Route(int w, int h, Func<int, int, float> cost, int sx, int sz, int tx, int tz)
        {
            if (w <= 0 || h <= 0 || cost == null || sx < 0 || sz < 0 || tx < 0 || tz < 0 || sx >= w || sz >= h || tx >= w || tz >= h) return null;
            if (cost(sx, sz) < 0f || cost(tx, tz) < 0f) return null;
            int[] dx = { 1, 0, -1, 0 }, dz = { 0, 1, 0, -1 };
            int states = w * h * 4;
            var g = new float[states];
            var from = new int[states];
            for (int i = 0; i < states; i++)
            {
                g[i] = float.MaxValue;
                from[i] = -1;
            }
            var open = new SortedSet<(float f, int s)>();
            float Heur(int x, int z) => Math.Abs(x - tx) + Math.Abs(z - tz);
            for (int d = 0; d < 4; d++)
            {
                int s0 = (sz * w + sx) * 4 + d;
                g[s0] = 0f;
                open.Add((Heur(sx, sz), s0));
            }
            int end = -1;
            while (open.Count > 0)
            {
                var top = open.Min;
                open.Remove(top);
                int s = top.s, cell = s / 4, dir = s % 4, x = cell % w, z = cell / w;
                if (top.f - Heur(x, z) > g[s] + 1e-3f) continue;
                if (x == tx && z == tz)
                {
                    end = s;
                    break;
                }
                for (int d = 0; d < 4; d++)
                {
                    if (d == (dir + 2) % 4) continue;
                    int nx = x + dx[d], nz = z + dz[d];
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                    float c = cost(nx, nz);
                    if (c < 0f || float.IsNaN(c)) continue;
                    float step = Math.Max(1f, c) + (d == dir ? 0f : TurnCost);
                    int ns = (nz * w + nx) * 4 + d;
                    float ng = g[s] + step;
                    if (ng >= g[ns]) continue;
                    if (g[ns] < float.MaxValue) open.Remove((g[ns] + Heur(nx, nz), ns));
                    g[ns] = ng;
                    from[ns] = s;
                    open.Add((ng + Heur(nx, nz), ns));
                }
            }
            if (end < 0) return null;
            var path = new List<(int x, int z)>();
            for (int s = end; s >= 0; s = from[s])
            {
                int cell = s / 4;
                var p = (cell % w, cell / w);
                if (path.Count == 0 || path[path.Count - 1] != p) path.Add(p);
                if (from[s] < 0) break;
            }
            path.Reverse();
            return path;
        }

        /// <summary>
        /// The road's cells in the order they are laid: each cell of its way and, for a road two wide, the one beside it on
        /// its left as it goes (the first beside it that the map has), none twice.
        /// </summary>
        public static List<(int x, int z)> Widen(IList<(int x, int z)> way, int width, int w, int h)
        {
            var list = new List<(int x, int z)>();
            if (way == null) return list;
            var seen = new HashSet<(int, int)>();
            for (int i = 0; i < way.Count; i++)
            {
                var p = way[i];
                if (seen.Add(p)) list.Add(p);
                if (width < 2) continue;
                var q = way[i < way.Count - 1 ? i + 1 : Math.Max(0, i - 1)];
                int ax = i < way.Count - 1 ? q.x - p.x : p.x - q.x, az = i < way.Count - 1 ? q.z - p.z : p.z - q.z;
                var side = (x: p.x - az, z: p.z + ax);
                if (side.x < 0 || side.z < 0 || side.x >= w || side.z >= h) side = (p.x + az, p.z - ax);
                if (side.x >= 0 && side.z >= 0 && side.x < w && side.z < h && seen.Add(side)) list.Add(side);
            }
            return list;
        }

        /// <summary>A field in the road's way: this much dearer (they go round it unless let through).</summary>
        public const float FieldCost = 40f;

        /// <summary>A road's cell is laid in this many ticks of work (a stretch of road a few hours a day).</summary>
        public const int RoadCellTicks = 420;

        /// <summary>The spur to the gate, in silver: twelve a cell, at the colony's wealth, within 60 and 600.</summary>
        public static int SpurPrice(int cells, float wealthFactor) => Math.Max(60, Math.Min(600, Round10(Math.Max(1, cells) * 12f * Clamp(wealthFactor, 0.6f, 2f))));

        /// <summary>The spur paid in the colony's stone: two blocks a cell, within 20 and 200.</summary>
        public static int SpurStone(int cells) => Clamp(Math.Max(1, cells) * 2, 20, 200);

        /// <summary>The colony's builder hired for a day: forty silver and six a level of Construction.</summary>
        public static int BuilderWage(int construction) => Round10(40f + 6f * Clamp(construction, 0, 20));

        /// <summary>Practice for the colony's builder, a cell of road laid beside them.</summary>
        public const float RoadCellXp = 90f;

        /// <summary>The line refused: they lay it round the colony's land, and think less of it.</summary>
        public const int LineRefusedGoodwill = -5;

        /// <summary>The road done across the colony's ground with its leave: their thanks.</summary>
        public const int RoadGoodwill = 3;

        /// <summary>Round the colony's land: the road keeps this far from its home ground and its buildings.</summary>
        public const int AroundMargin = 10;

        /// <summary>A robber band comes against the camp once a stay at this chance, on top of the camps' own.</summary>
        public const float GangBandChance = 0.12f;

        /// <summary>A day's food sold to the gang: meals at their worth and a tenth more.</summary>
        public const int GangMeals = 8;

        // ================= 6. Nobody on a sickbed is anyone's enemy (the hospitaller order) =================

        /// <summary>Two or three hospitallers come, by how many lie downed.</summary>
        public static int Litters(int downed) => downed >= 4 ? 3 : 2;

        /// <summary>The order comes after a raid at this chance (the storytellers' lean on visitors on top), not sooner than this after it.</summary>
        public const float LittersChance = 0.45f;

        public const int LittersMinHours = 1, LittersMaxHours = 3;

        /// <summary>The order's own thanks for those let go to the sickhouse.</summary>
        public const int LittersGoodwill = 5;

        /// <summary>No sooner again than this.</summary>
        public const int LittersGapDays = 15;

        /// <summary>Those carried for one answer at most.</summary>
        public const int LittersMax = 10;

        /// <summary>The worst hurt first, for "only the worst hurt": the half of them (at least one) bleeding or nearest death.</summary>
        public static int WorstHurt(int downed) => downed <= 0 ? 0 : Math.Max(1, (downed + 1) / 2);

        // ================= 7. The sick room (the hospitaller order) =================

        /// <summary>The sister keeps the sick room three days.</summary>
        public const int SickRoomDays = 3;

        /// <summary>While the sick room stands, the sickness spreads at a third of its rate.</summary>
        public const float SickRoomSpread = 1f / 3f;

        /// <summary>Her rounds: a dose for each of the sick at these hours.</summary>
        public static readonly int[] RoundHours = { 7, 12, 17, 22 };

        public static bool RoundHour(int hour) => Array.IndexOf(RoundHours, hour) >= 0;

        /// <summary>She may catch it herself, a day of rounds at a time (the physician's chance, through the harm budget and the illness budget).</summary>
        public const float SisterCatch = 0.06f;

        public const int SickRoomGoodwill = 3;

        /// <summary>From the industrial age she works with the colony's own medicine: this much carried to her at most.</summary>
        public const int SickRoomMedicine = 6;

        /// <summary>The sick she sees to at most.</summary>
        public const int SickRoomMax = 6;

        // ================= 8. A field sickhouse (the hospitaller order) =================

        /// <summary>Two to six sick or wounded strangers of the region come in over the days.</summary>
        public const int PatientsMin = 2, PatientsMax = 6;

        public const int PatientGapMinHours = 12, PatientGapMaxHours = 30;

        /// <summary>How many strangers come to this sickhouse, by a roll in [0, 1).</summary>
        public static int Patients(float roll) => PatientsMin + Clamp((int)Math.Floor(Clamp(roll, 0f, 0.999f) * (PatientsMax - PatientsMin + 1)), 0, PatientsMax - PatientsMin);

        /// <summary>A stranger carried in rather than walking, this often.</summary>
        public const float CarriedIn = 0.35f;

        /// <summary>The colony's doctor works the ward a day and learns a lot.</summary>
        public const float WardXp = 3000f;

        /// <summary>The doctor may bring the sickness home (the physician's chance, through the harm budget and the illness budget).</summary>
        public const float DoctorCatch = 0.1f;

        /// <summary>The illness crosses to the colony once a stay at most, a day the colony's people went to the ward.</summary>
        public const float CrossChance = 0.08f;

        /// <summary>The medicine draws a band against the sickhouse now and then, on top of the camps' own.</summary>
        public const float MedicineBandChance = 0.12f;

        public const int SickhouseGoodwill = 8, FedGoodwill = 3;

        /// <summary>Medicine, food and clean cloth asked.</summary>
        public const int MedicineAsked = 8, FoodAsked = 12, ClothAsked = 30;

        // ================= 9. Grazing across the land (the herding folk) =================

        /// <summary>A drove of twenty to forty head, two or three days crossing.</summary>
        public const int DroveMin = 20, DroveMax = 40;

        public const float DroveMinDays = 2f, DroveMaxDays = 3f;

        /// <summary>The toll: five silver a head, by the colony's wealth, within 60 and 300.</summary>
        public static int DroveToll(int head, float wealthFactor) => Math.Max(60, Math.Min(300, Round10(5f * Math.Max(1, head) * Clamp(wealthFactor, 0.5f, 2f))));

        public const int CrossedGoodwill = 4, DroveTollGoodwill = -3, DroveTurnedGoodwill = -6;

        /// <summary>What the drove eats of an unfenced field it grazes by in a day: a fifth of the crops within reach, at most this many.</summary>
        public static int Grazed(int cropsInReach) => Math.Min(DroveMaxGrazed, (int)Math.Ceiling(Math.Max(0, cropsInReach) * 0.2));

        public const int DroveMaxGrazed = 24;

        /// <summary>How far from the herd's ground (in cells) a field counts as within its reach.</summary>
        public const float GrazeReach = 14f;

        /// <summary>Rarely, a spooked beast kicks one of the colony who comes close (once a drove, through the harm budget).</summary>
        public const float KickChance = 0.15f;

        /// <summary>The colony's best hand at beasts rides with the drove for a day: practice with animals.</summary>
        public const float RideXp = 2500f;

        /// <summary>Their thanks for the crossing: a young beast of the drove six times in ten, else hides and milk.</summary>
        public const float YoungBeastChance = 0.6f;

        public const int HidesMin = 20, HidesMax = 40, MilkMin = 20, MilkMax = 40;

        /// <summary>Where the herd grazes at a share <paramref name="t"/> of its days: from one edge to the other, eased in and out.</summary>
        public static float DroveShare(float t)
        {
            float x = Clamp(t, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        /// <summary>The grazing beasts of the drove, by the land: the base game's and the player's mods', by name.</summary>
        public static readonly string[] DroveKinds =
        {
            "Muffalo", "Yak", "Cow", "Sheep", "Goat", "Alpaca", "Dromedary", "Muskox", "AEXP_Camel", "AA_Thunderox", "AA_GreyCoatedMouflon", "Aurochs"
        };

        // ================= 11. The thief on the drove road (the herding folk) =================

        /// <summary>
        /// Raiders going off the map drive some of the colony's beasts off with them, once a raid at this chance: likelier
        /// when they came with beasts of their own (Animal Husbandry Raids' drovers among them).
        /// </summary>
        public static float DriveOffChance(bool ownBeasts) => ownBeasts ? 0.3f : 0.12f;

        /// <summary>How many they drive off, by a roll in [0, 1): one to three, those nearest them.</summary>
        public static int DriveOffCount(float roll) => 1 + Clamp((int)Math.Floor(Clamp(roll, 0f, 0.999f) * 3f), 0, 2);

        /// <summary>A beast within this many cells of a raider on the way out can be driven off.</summary>
        public const float DriveReach = 22f;

        /// <summary>A beast whose drovers fell or ran off without it, this far from any of them, is the colony's again.</summary>
        public const float LeftBehind = 16f;

        /// <summary>The thieves' camp stays on the world map this many days before the trail goes cold.</summary>
        public const int CampDays = 6;

        /// <summary>The herders' riders come on the thieves' trail the next morning at this chance (the storytellers' lean on visitors on top).</summary>
        public const float TrailChance = 0.6f;

        /// <summary>The morning after: they come between these hours, at least this long after the theft.</summary>
        public const int TrailFromHour = 6, TrailToHour = 10, TrailMinHours = 6;

        /// <summary>Their riders, two or three.</summary>
        public const int RidersMin = 2, RidersMax = 3;

        /// <summary>The thieves' camp, a tile or two away.</summary>
        public const int TrailTilesMin = 1, TrailTilesMax = 2;

        /// <summary>The thieves at their camp: a share of the colony's threat points.</summary>
        public const float ThiefPoints = 0.35f;

        /// <summary>Going alone, the herders bring back each beast at this chance.</summary>
        public const float AloneBack = 0.6f;

        /// <summary>How many of the stolen the herders bring back alone, by a roll per beast (rolls in [0, 1)).</summary>
        public static int BroughtBack(IList<float> rolls)
        {
            if (rolls == null) return 0;
            int n = 0;
            foreach (float r in rolls)
                if (!float.IsNaN(r) && r < AloneBack)
                    n++;
            return n;
        }

        /// <summary>They keep one beast as their due, of those they bring back.</summary>
        public static int TheirDue(int broughtBack) => broughtBack > 0 ? 1 : 0;

        public const int AloneMinDays = 1, AloneMaxDays = 3;

        public const int TrailGoodwill = 6, AloneGoodwill = 2;

        /// <summary>A theft the record keeps (one open at a time a home), and the beasts in it at most.</summary>
        public const int MaxThefts = 4, MaxStolen = 12;

        // ================= 13. Spring calving (the herding folk) =================

        /// <summary>Four to seven in-calf beasts.</summary>
        public const int InCalfMin = 4, InCalfMax = 7;

        /// <summary>The in-calf beasts' gestation left, as a share of their kind's: births over the stay's first days.</summary>
        public static float GestationAt(float daysToBirth, float gestationDays) => gestationDays <= 0f ? 0.99f : Clamp(1f - Math.Max(0.1f, daysToBirth) / gestationDays, 0.05f, 0.99f);

        /// <summary>A birth goes hard this often, once a stay at most asked of the colony.</summary>
        public const float HardBirthChance = 0.35f;

        /// <summary>A hard birth is held at this point until someone helps it (or the herders do alone).</summary>
        public const float HardHeld = 0.97f;

        /// <summary>The herders wait this long for the colony's hand before they do it alone.</summary>
        public const int HardWaitHours = 8;

        /// <summary>
        /// The mother lost, as the card's roll has it (the answer's risk, lowered by the best hand's way with beasts, three and a
        /// half points a level, as every answer's risk is).
        /// </summary>
        public const float HardRisk = 0.55f;

        /// <summary>Both live: one less the risk the helper's way with beasts leaves, within 5% and 95% either way.</summary>
        public static float BirthOdds(int animals) => 1f - Clamp(HardRisk - 0.035f * Clamp(animals, 0, 20), 0.05f, 0.95f);

        /// <summary>The herders seeing it through by themselves (their own hands, about eight at beasts): the risk on the card.</summary>
        public const float HerdersRisk = 0.27f;

        public static float HerdersOdds => 1f - HerdersRisk;

        /// <summary>Working the hard birth: an hour and a half.</summary>
        public const int BirthTicks = 3 * HourTicks / 2;

        public const float BirthXp = 800f;

        /// <summary>Once a stay, a calf goes missing, and they search with lanterns at night.</summary>
        public const float CalfMissingChance = 0.18f;

        /// <summary>The afterbirth draws a predator in the night, once a stay, through the harm budget (a camp's draw).</summary>
        public const float AfterbirthPredator = 0.3f;

        /// <summary>A lantern-bearer finds the calf within this many cells.</summary>
        public const float FoundWithin = 5f;

        public const int CalvingGoodwill = 6, CalvingTollGoodwill = -3;

        /// <summary>The toll for the colony's ground: eight silver a head, within 40 and 160.</summary>
        public static int CalvingToll(int head, float wealthFactor) => Math.Max(40, Math.Min(160, Round10(8f * Math.Max(1, head) * Clamp(wealthFactor, 0.5f, 2f))));

        /// <summary>The beasts that calve, by name: the base game's and the player's mods'.</summary>
        public static readonly string[] CalvingKinds = { "Cow", "Muffalo", "Yak", "Sheep", "Goat", "Alpaca", "Dromedary", "Muskox", "AEXP_Camel", "AA_Thunderox", "Aurochs" };

        public const int HayAsked = 40;

        // ================= 15. An old shrine on the colony's ground (the pilgrim fellowship) =================

        /// <summary>What they find holy, the strongest first: an ancient structure, a grave older than the colony, a great old tree, the land's landmark.</summary>
        public static readonly string[] HolyKinds = { "ancient", "grave", "tree", "landmark" };

        /// <summary>Two pilgrims work three days setting stones and a lamp; two with one of the colony beside them.</summary>
        public static int ShrineDays(bool helped) => helped ? 2 : 3;

        /// <summary>A day's work at the shrine: these hours.</summary>
        public const int ShrineFrom = 8, ShrineTo = 18;

        /// <summary>A great old tree: a grown tree at least this many years old.</summary>
        public const float OldTreeYears = 3f;

        /// <summary>In spring and summer pilgrims stop at the shrine, a day at this chance, no sooner than this after the last.</summary>
        public const float StopChance = 0.1f;

        public const int StopGapDays = 10;

        /// <summary>What they leave at its foot: a little silver (15 to 40) or herbs (two to five), by a roll.</summary>
        public static int OfferingSilver(float roll) => 15 + (int)Math.Floor(Clamp(roll, 0f, 0.999f) * 26f);

        public static int OfferingHerbs(float roll) => 2 + (int)Math.Floor(Clamp(roll, 0f, 0.999f) * 4f);

        /// <summary>Left out in the weather, it loses this share of its strength a day (repaired, it stands).</summary>
        public const float WeatherPerDay = 0.02f;

        /// <summary>Below this share of its strength it has fallen to ruin, and the pilgrims mourn it; above this again, it is kept.</summary>
        public const float RuinBelow = 0.35f, KeptAbove = 0.6f;

        public static bool Ruined(int hp, int max) => max > 0 && hp < max * RuinBelow;

        public static bool Kept(int hp, int max) => max > 0 && hp >= max * KeptAbove;

        public const int ShrineGoodwill = 3, MournGoodwill = -3;

        /// <summary>The colony's shrines the record keeps.</summary>
        public const int MaxShrines = 6;

        // ================= 16. Two faiths at one table (the pilgrim fellowship) =================

        /// <summary>The split comes anyway, as the card's roll has it: the answer's risk, lowered by the better speaker's Social.</summary>
        public const float SplitRisk = 0.67f;

        /// <summary>The blessing settles it: one less the risk the better of the two speakers' Social leaves (3.5% a level), within 5% and 95%.</summary>
        public static float SettleChance(int socialA, int socialB) => 1f - Clamp(SplitRisk - 0.035f * Clamp(Math.Max(socialA, socialB), 0, 20), 0.05f, 0.95f);

        /// <summary>Settled for a season: the faiths' clash asked no sooner than this.</summary>
        public const int SettledDays = 15;

        /// <summary>Certainty steadied on both sides.</summary>
        public const float Steadied = 0.1f;

        /// <summary>The split comes anyway: the wavering lose this much more.</summary>
        public const float SplitShaken = 0.08f;

        /// <summary>A believer wavers below this certainty.</summary>
        public const float Waver = 0.3f;

        /// <summary>The colony's own faith near a split: three believers or more, a third of them (two at least) wavering.</summary>
        public static bool NearSplit(int believers, int wavering) => believers >= 3 && wavering >= 2 && wavering * 3 >= believers;

        public const int TableGoodwill = 3;
    }
}
