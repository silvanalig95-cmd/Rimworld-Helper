using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The supplies packed for one letter's rider (Post/RiderPacks.cs): kept in the post's book from the send until they're
    /// home again, or the letter goes back in the mailbag. Only the lines taken are kept.
    /// </summary>
    public class RiderPack : IExposable
    {
        public int letterId = -1;

        /// <summary>What goes, as the journey planner's lines: food, medicine, drugs, feed, anything else the player added.</summary>
        public List<JourneyLine> lines = new List<JourneyLine>();

        /// <summary>The player let them go short of the road, knowing it.</summary>
        public bool knowingly;

        /// <summary>The trip it was packed for, in days: there, the wait at the town, and back.</summary>
        public float tripDays;

        public IEnumerable<JourneyLine> Taken => lines.Where(l => l != null && l.Taken);

        public RiderPack Copy() =>
            new RiderPack { letterId = letterId, lines = lines.Where(l => l?.def != null).Select(l => l.Copy()).ToList(), knowingly = knowingly, tripDays = tripDays };

        public void ExposeData()
        {
            Scribe_Values.Look(ref letterId, "letterId", -1);
            Scribe_Collections.Look(ref lines, "lines", LookMode.Deep);
            Scribe_Values.Look(ref knowingly, "knowingly");
            Scribe_Values.Look(ref tripDays, "tripDays");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lines ??= new List<JourneyLine>();
                lines.RemoveAll(l => l?.def == null);
            }
        }
    }

    /// <summary>
    /// Supplies for the colony's own rider (the player, 10 November 2026: "sending a colonist off to send a letter makes them
    /// just go off map without any supplies or preparation. supply selection should be part of the initial decision"). Where
    /// a letter is sent with the colony's own rider (the letter's window, the mailbag's "Send with our rider") the supplies
    /// are chosen too: road food for there and back by the journey planner's rules (the game's own reckoning of the road for
    /// this rider and beast, the food policy they eat by on the road, a quarter more and a spare day), a little medicine,
    /// drugs for an addiction, the beast's feed for the days it can't graze, and anything else from the stores. Short of the
    /// road, the send is greyed with why unless the player lets them go short knowingly; more than they can carry, always.
    /// The rider gathers it as the colony's caravans do (Post/OwnRiders.cs, Gather); the pure rules are RiderPackMath.cs.
    /// </summary>
    public static class RiderPacks
    {
        private static PostBook Book => PostBook.Instance;

        private static RiderPack next;

        /// <summary>The letter's window sets the supplies for the letter it sends next (taken by OwnRiders.Send); null after.</summary>
        public static void ForNext(RiderPack p) => next = p;

        public static RiderPack TakeNext()
        {
            var p = next;
            next = null;
            return p;
        }

        public static RiderPack For(int letterId) => Book?.packs.FirstOrDefault(p => p != null && p.letterId == letterId);

        /// <summary>The supplies go with this letter's rider: what is taken, kept in the post's book.</summary>
        public static void Keep(int letterId, RiderPack p)
        {
            var book = Book;
            if (book == null || p == null) return;
            Drop(letterId);
            var kept = p.Copy();
            kept.letterId = letterId;
            kept.lines.RemoveAll(l => !l.Taken);
            book.packs.Add(kept);
        }

        public static void Drop(int letterId) => Book?.packs.RemoveAll(p => p == null || p.letterId == letterId);

        /// <summary>Supplies whose letter is gone, done, or no longer going with the colony's rider (and nobody out with it) are let go.</summary>
        public static void Prune()
        {
            var book = Book;
            if (book == null || book.packs.Count == 0) return;
            book.packs.RemoveAll(p =>
            {
                if (p == null) return true;
                if (book.RideFor(p.letterId) != null) return false;
                var l = NeighbourLetters.Get(p.letterId);
                return l == null || !l.Open || !l.ownRider;
            });
        }

        /// <summary>The supplies by the journey planner's rules, for a rider sent without choosing (an older save, a letter already on its way).</summary>
        public static RiderPack DefaultFor(Map map, Pawn rider, Pawn mount, Settlement town)
        {
            var m = new RiderPackModel(map, rider, town).WithMount(mount);
            m.Refresh(true);
            return m.pack;
        }

        // ---------------- What they may eat ----------------

        /// <summary>Food for the road by the journey planner's rule: a person's food, no drug, nothing raw and nasty, no corpse.</summary>
        public static bool TravelFood(ThingDef d)
        {
            var ing = d?.ingestible;
            if (ing == null || !d.IsNutritionGivingIngestible || d.IsDrug || d.IsCorpse || !ing.HumanEdible) return false;
            return ing.preferability >= FoodPreferability.RawTasty && ing.preferability <= FoodPreferability.MealLavish;
        }

        /// <summary>
        /// Food the rider would eat on the road: the caravan's own rules (Journeys/RoadFood.cs), the food policy they will eat by
        /// on the road, no venerated animal, and nothing their beliefs rule out.
        /// </summary>
        public static bool Eats(Pawn p, ThingDef d)
        {
            if (p == null || !TravelFood(d) || !RoadFood.RoadEdible(d, p)) return false;
            try
            {
                var policy = RoadFood.OnTheRoad(p);
                if (policy != null && !policy.Allows(d)) return false;
                if (FoodUtility.IsVeneratedAnimalMeatOrCorpse(d, p)) return false;
                return Beliefs.FitForStock(p, d);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post: the rider's food", e);
                return false;
            }
        }

        private static bool Eats(Pawn p, Thing t) => Eats(p, t?.def) && Beliefs.FitForStock(p, t);

        /// <summary>Feed the beast eats: kibble or hay, as the journey planner packs.</summary>
        public static bool Feed(Pawn beast, ThingDef d) => beast?.RaceProps != null && d != null && (d == ThingDefOf.Kibble || d == ThingDefOf.Hay) && beast.RaceProps.CanEverEat(d);

        // ---------------- The stores ----------------

        /// <summary>Things of the colony's stores the rider may take: spawned, in storage, not forbidden.</summary>
        private static IEnumerable<Thing> Stored(Map map, ThingRequestGroup group)
        {
            foreach (var t in map.listerThings.ThingsInGroup(group))
                if (t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage())
                    yield return t;
        }

        /// <summary>
        /// The supplies by the journey planner's rules for one rider: food for the trip with its margin from the best travel food
        /// they would eat, a little medicine (never more than half the stores while anyone stays home), drugs for their
        /// addictions, and the beast's feed for the days it can't graze. The player's lines stay as they set them (a number, a
        /// line added, a suggestion taken off), in the order they had. <paramref name="foodToHand"/> and
        /// <paramref name="feedToHand"/>: the most the stores (and what they carry) could give.
        /// </summary>
        public static List<JourneyLine> Packing(Map map, Pawn rider, Pawn mount, float tripDays, float noGrazing, List<JourneyLine> old, out float foodToHand, out float feedToHand)
        {
            foodToHand = feedToHand = 0f;
            var lines = new List<JourneyLine>();
            if (map == null || rider == null) return lines;
            var kept = old?.Where(o => o?.def != null && o.Player).Select(o => o.Copy()).ToList() ?? new List<JourneyLine>();
            var off = new HashSet<ThingDef>(kept.Where(o => o.removed).Select(o => o.def));
            var carried = new List<Thing>();
            if (rider.inventory != null) carried.AddRange(rider.inventory.innerContainer);
            if (mount?.inventory != null) carried.AddRange(mount.inventory.innerContainer);
            int stayHome = map.mapPawns.FreeColonistsSpawned.Count(p => p != rider);
            try
            {
                // Food: the rider's nourishment a day for the trip with the margin, from the best travel food they'd eat.
                float need = RiderPackMath.FoodWanted(JourneyPack.NutritionPerDay(rider), tripDays);
                var foods = new List<JourneyMath.FoodOption>();
                var defs = new List<ThingDef>();
                foreach (var g in Stored(map, ThingRequestGroup.FoodSourceNotPlantOrTree).Concat(carried).Where(t => !(t is Pawn) && Eats(rider, t)).GroupBy(t => t.def))
                {
                    var d = g.Key;
                    int stock = g.Sum(t => t.stackCount);
                    foodToHand += stock * JourneyPack.Nutrition(d);
                    if (off.Contains(d)) continue;
                    defs.Add(d);
                    foods.Add(new JourneyMath.FoodOption
                    {
                        nutrition = JourneyPack.Nutrition(d), mass = d.GetStatValueAbstract(StatDefOf.Mass), rotDays = JourneyPack.RotDays(d), preferability = (int)d.ingestible.preferability,
                        travel = ThingRolesMath.TravelRank(ThingRoles.Facts(d)), stock = stock
                    });
                }
                foreach (var (i, units) in JourneyMath.PackFood(foods, tripDays, need)) lines.Add(new JourneyLine(defs[i], units, "food"));

                // A little medicine: the plainer kinds first, never more than half the stores while anyone stays home.
                var meds = Stored(map, ThingRequestGroup.Medicine).Concat(carried.Where(t => t.def.IsMedicine)).GroupBy(t => t.def)
                    .Select(g => (def: g.Key, stock: g.Sum(t => t.stackCount), potency: g.Key.GetStatValueAbstract(StatDefOf.MedicalPotency)))
                    .OrderBy(m => m.potency >= 1.5f ? 1 : 0).ThenByDescending(m => m.potency).ToList();
                int medWant = JourneyMath.MedicineTaken(RiderPackMath.MedicineWanted(tripDays), meds.Sum(m => m.stock), stayHome);
                foreach (var m in meds.Where(m => !off.Contains(m.def)))
                {
                    if (medWant <= 0) break;
                    int take = Mathf.Min(medWant, m.stock);
                    lines.Add(new JourneyLine(m.def, take, "medicine"));
                    medWant -= take;
                }

                // Drugs for their addictions and chemical dependencies, for the days away with the margin.
                lines.AddRange(JourneyPack.Drugs(map, new List<Pawn> { rider }, tripDays, carried, off));

                // The beast's feed for the days it can't graze (every day for one that doesn't): kibble, else hay.
                if (mount?.needs?.food != null)
                {
                    bool grazes = mount.RaceProps.Eats(FoodTypeFlags.Plant);
                    float n = RiderPackMath.FeedWanted(JourneyPack.NutritionPerDay(mount), tripDays, grazes, noGrazing);
                    foreach (var d in new[] { ThingDefOf.Kibble, ThingDefOf.Hay })
                        if (Feed(mount, d))
                            feedToHand += (JourneyPack.InStores(map, d) + JourneyPack.Carried(rider, d) + JourneyPack.Carried(mount, d)) * JourneyPack.Nutrition(d);
                    var feed = new[] { ThingDefOf.Kibble, ThingDefOf.Hay }.FirstOrDefault(d => Feed(mount, d) && !off.Contains(d));
                    if (n > 0f && feed != null)
                    {
                        int have = JourneyPack.InStores(map, feed) + JourneyPack.Carried(rider, feed) + JourneyPack.Carried(mount, feed);
                        int take = Mathf.Min(have, JourneyMath.Units(n, JourneyPack.Nutrition(feed)));
                        if (take > 0) lines.Add(new JourneyLine(feed, take, "feed"));
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post: the rider's supplies", e);
            }
            // What the player set stays set: their number on the suggestion, or their own line.
            foreach (var o in kept)
            {
                var same = lines.FirstOrDefault(l => l.def == o.def && l.kind == o.kind && !l.Player);
                if (same == null) lines.Add(o);
                else
                {
                    same.count = o.count;
                    same.edited = o.edited;
                    same.added = o.added;
                    same.removed = o.removed;
                }
            }
            var order = new Dictionary<(ThingDef, string), int>();
            for (int i = 0; i < (old?.Count ?? 0); i++)
                if (old[i]?.def != null && !order.ContainsKey((old[i].def, old[i].kind))) order[(old[i].def, old[i].kind)] = i;
            return lines.OrderBy(l => order.TryGetValue((l.def, l.kind), out int i) ? i : int.MaxValue).ToList();
        }

        /// <summary>The journey planner's own plan for the rider's road, not kept anywhere: for its way out, its gathering and its picker.</summary>
        public static JourneyPlan Shadow(Map map, Pawn rider, Pawn mount, Settlement town, List<JourneyLine> lines)
        {
            var plan = new JourneyPlan
            {
                map = map, destTile = town?.Tile.tileId ?? -1, destObject = town?.ID ?? -1, destName = town?.Name, andBack = true,
                packing = lines ?? new List<JourneyLine>()
            };
            if (rider != null) plan.travellers.Add(rider);
            if (mount != null) plan.animals.Add(mount);
            return plan;
        }

        // ---------------- Home again ----------------

        /// <summary>What is left of the supplies in the rider's and the beast's packs ("3 packaged survival meals"), up to what was packed.</summary>
        public static List<string> Left(RiderPack pack, Pawn rider, Pawn mount)
        {
            var left = new List<string>();
            if (pack == null) return left;
            foreach (var line in pack.Taken)
            {
                int n = Mathf.Min(line.count, JourneyPack.Carried(rider, line.def) + JourneyPack.Carried(mount, line.def));
                if (n > 0) left.Add(Count(line.def, n));
            }
            return left;
        }

        public static string Count(ThingDef d, int n) => n + " " + (n == 1 ? d.label : Find.ActiveLanguageWorker.Pluralize(d.label, n));

        /// <summary>The supplies in a few words: "6 packaged survival meals, 1 herbal medicine and 40 hay".</summary>
        public static string Words(IEnumerable<JourneyLine> lines, int most = 4)
        {
            var parts = lines.Where(l => l != null && l.Taken).Select(l => Count(l.def, l.count)).ToList();
            if (parts.Count > most) parts = parts.Take(most).Concat(new[] { "CC_RoadFood_More".Translate().Resolve() }).ToList();
            return TextUtil.JoinAnd(parts);
        }
    }

    /// <summary>
    /// The supplies being chosen for a rider (the letter's window, the supplies window): the road by the game's own caravan
    /// reckoning for this rider and beast, the suggestions with the player's own lines, what there is to hand, and how it
    /// stands. Looked at once a second at most; the road every half a minute, or when the rider or the beast changes. Not saved.
    /// </summary>
    public sealed class RiderPackModel
    {
        public Map map;
        public Pawn rider, mount;
        public Settlement town;
        public readonly RiderPack pack = new RiderPack();

        /// <summary>The road there in days (-1 until worked out), the share of the trip without grazing, the most food and feed to hand.</summary>
        public float oneWayDays = -1f, noGrazing, foodToHand, feedToHand;

        public RiderPackFacts facts;
        public RiderPackVerdict verdict;

        private float lookedAt = -99f, routedAt = -99f, packedAt = -99f;
        private Pawn routedRider, routedMount;
        private bool fixedMount;

        public RiderPackModel(Map map, Pawn rider, Settlement town)
        {
            this.map = map;
            this.rider = rider;
            this.town = town;
        }

        /// <summary>The same model for the same home and town (the rider may change: the supplies are worked out again for them).</summary>
        public static RiderPackModel For(RiderPackModel old, Map map, Pawn rider, Settlement town)
        {
            if (old != null && old.map == map && old.town == town)
            {
                // Another rider chosen: their supplies at once, not on the next look.
                if (old.rider != rider) old.lookedAt = -99f;
                old.rider = rider;
                return old;
            }
            return new RiderPackModel(map, rider, town);
        }

        public List<Pawn> Going
        {
            get
            {
                var list = new List<Pawn>();
                if (rider != null) list.Add(rider);
                if (mount != null) list.Add(mount);
                return list;
            }
        }

        /// <summary>The road, the suggestions and the verdict, worked out again (at once when <paramref name="now"/>).</summary>
        public void Refresh(bool now = false)
        {
            float t = Time.realtimeSinceStartup;
            if (!now && t - lookedAt < 1f && t >= lookedAt) return;
            lookedAt = t;
            if (map == null || rider == null || town == null) return;
            try
            {
                // The colony's best beast to ride now, unless the one they set out with was given (WithMount).
                if (!fixedMount) mount = OwnRiders.MountFor(map, rider);
                bool moved = rider != routedRider || mount != routedMount;
                if (now || moved || oneWayDays < 0f || t - routedAt > 30f || t < routedAt) Route(t);
                if (now || moved || t - packedAt > 3f || t < packedAt)
                {
                    var fresh = RiderPacks.Packing(map, rider, mount, pack.tripDays, noGrazing, pack.lines, out foodToHand, out feedToHand);
                    // In place: the picker works on this very list.
                    pack.lines.Clear();
                    pack.lines.AddRange(fresh);
                    packedAt = t;
                }
                Judge();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post: the rider's supplies", e);
            }
        }

        /// <summary>The beast set out with, kept as it is (an older save's rider, sent without choosing).</summary>
        public RiderPackModel WithMount(Pawn m)
        {
            mount = m;
            fixedMount = true;
            return this;
        }

        /// <summary>The road there by the game's own caravan reckoning for this rider and beast, with what they'd carry.</summary>
        private void Route(float t)
        {
            routedAt = t;
            routedRider = rider;
            routedMount = mount;
            float mass = Going.Sum(p => MassUtility.GearAndInventoryMass(p)) + LoadToTake();
            var r = JourneyPack.Route(map, town.Tile, Going, mass, Find.TickManager.TicksAbs, true, 0f);
            oneWayDays = r.oneWayTicks > 0 ? r.oneWayTicks / (float)GenDate.TicksPerDay : Mathf.Max(0.5f, PostReach.RiderDays(map, town, mount != null));
            noGrazing = r.noGrazing;
            pack.tripDays = RiderPackMath.TripDays(oneWayDays);
        }

        /// <summary>The weight the supplies add: what is taken less what the rider and the beast carry already.</summary>
        public float LoadToTake()
        {
            float m = 0f;
            foreach (var l in pack.Taken)
                m += Mathf.Max(0, l.count - Carried(l.def)) * JourneyPack.UnitMass(l.def);
            return m;
        }

        public int Carried(ThingDef d) => JourneyPack.Carried(rider, d) + JourneyPack.Carried(mount, d);

        /// <summary>The player changed a line: the verdict at once.</summary>
        public void Changed() => Judge();

        /// <summary>The suggestions again, the player's own numbers and lines let go.</summary>
        public void Recommended()
        {
            pack.lines.Clear();
            pack.knowingly = false;
            Refresh(true);
        }

        private void Judge()
        {
            var f = new RiderPackFacts { oneWayDays = oneWayDays, perDay = JourneyPack.NutritionPerDay(rider), foodToHand = foodToHand, feedToHand = feedToHand };
            foreach (var l in pack.Taken)
            {
                if (l.kind == "food" && RiderPacks.Eats(rider, l.def)) f.food += l.count * JourneyPack.Nutrition(l.def);
                else if (l.kind == "medicine") f.medicine += l.count;
                if (mount != null && RiderPacks.Feed(mount, l.def)) f.feed += l.count * JourneyPack.Nutrition(l.def);
            }
            if (mount?.needs?.food != null)
            {
                f.beast = true;
                f.grazes = mount.RaceProps.Eats(FoodTypeFlags.Plant);
                f.beastPerDay = JourneyPack.NutritionPerDay(mount);
                f.noGrazing = noGrazing;
            }
            f.load = Going.Sum(p => MassUtility.GearAndInventoryMass(p)) + LoadToTake();
            f.room = Going.Sum(p => MassUtility.Capacity(p));
            facts = f;
            verdict = RiderPackMath.Judge(f);
        }

        public bool MaySend => RiderPackMath.MaySend(verdict, pack.knowingly);

        private NamedArgument[] Args() => new[]
        {
            (rider?.LabelShort ?? "?").Named("PAWN"), (mount?.LabelShort ?? "").Named("MOUNT"), (town?.Name ?? "?").Named("TOWN"),
            RiderPackMath.Days(verdict.foodDays).Named("FOOD"), RiderPackMath.Days(verdict.tripDays).Named("ROAD"),
            RiderPackMath.Days(verdict.feedDays).Named("FEED"), RiderPackMath.Days(verdict.feedNeedDays).Named("NEED"),
            JourneyMath.ShortMeals(JourneyMath.ShortDays(verdict.foodDays, verdict.tripDays), facts.perDay).Named("MEALS"),
            facts.load.ToString("0.#").Named("LOAD"), facts.room.ToString("0.#").Named("ROOM"), facts.medicine.Named("COUNT")
        };

        private string P(string key) => key.Translate(Args()).Resolve();

        /// <summary>Why the send is greyed by the supplies, or null.</summary>
        public string Why
        {
            get
            {
                string key = RiderPackMath.WhyKey(verdict, pack.knowingly);
                return key == null ? null : P(key);
            }
        }

        /// <summary>The supplies' lines below the list: the food, the beast's feed, the medicine and the load, each in its colour.</summary>
        public List<(string text, Color color)> Lines()
        {
            var list = new List<(string, Color)>();
            if (facts.perDay > 0f) list.Add((P(RiderPackMath.FoodKey(verdict.food)), CoverColor(verdict.food)));
            if (verdict.storesShortFood && verdict.food >= RiderPackCover.Short) list.Add((P("CC_RiderPack_StoresShort"), UiKit.Bad));
            if (facts.beast) list.Add((P(RiderPackMath.FeedKey(verdict.feed, verdict.feedNeedDays)), verdict.feedNeedDays <= 0f ? UiKit.Faint : CoverColor(verdict.feed)));
            list.Add((P(verdict.noMedicine ? "CC_RiderPack_NoMedicine" : "CC_RiderPack_Medicine"), verdict.noMedicine ? UiKit.Warn : UiKit.Faint));
            list.Add((P(mount != null && MassUtility.Capacity(mount) > 0f ? "CC_RiderPack_LoadBeast" : "CC_RiderPack_Load"), verdict.over ? UiKit.Bad : UiKit.Faint));
            if (pack.knowingly && verdict.Short && !verdict.over) list.Add((P("CC_RiderPack_GoesShort"), UiKit.Warn));
            return list;
        }

        public static Color CoverColor(RiderPackCover c) => c == RiderPackCover.Enough ? UiKit.Good : c == RiderPackCover.NoMargin ? UiKit.Warn : UiKit.Bad;

        /// <summary>One line for the letter's window: the food against the road (and the beast's feed when it needs any).</summary>
        public string Summary()
        {
            string s = P(facts.perDay > 0f ? "CC_RiderPack_Summary" : "CC_RiderPack_SummaryNoFood");
            if (facts.beast && verdict.feedNeedDays > 0f) s += " " + P("CC_RiderPack_SummaryFeed");
            return s;
        }

        public Color SummaryColor => verdict.over || verdict.Short && !pack.knowingly ? UiKit.Bad : verdict.Short ? UiKit.Warn
            : verdict.food == RiderPackCover.NoMargin || verdict.feed == RiderPackCover.NoMargin ? UiKit.Warn : UiKit.Good;

        /// <summary>Who goes where, and how long: under the supplies window's title.</summary>
        public string Subtitle() => P(mount != null ? "CC_RiderPack_Sub" : "CC_RiderPack_SubFoot") + " " + "CC_RiderPack_SubDays".Translate(
            RiderPackMath.Days(oneWayDays).Named("THERE"), RiderPackMath.Days(verdict.tripDays).Named("ROAD")).Resolve();

        /// <summary>What is packed, on hover.</summary>
        public string Tip()
        {
            string what = RiderPacks.Words(pack.lines, 12);
            return (what.NullOrEmpty() ? "CC_RiderPack_Nothing".Translate().Resolve() : "CC_RiderPack_Packed".Translate(what.Named("WHAT")).Resolve()) + "\n\n"
                   + string.Join("\n", Lines().Select(x => x.text));
        }
    }

    /// <summary>
    /// The supplies in the letter's window (World/NeighboursPanel.cs), under "By our own rider": a button to the supplies
    /// window, a line on the food against the road, and when it falls short, "Let them go short, knowing it".
    /// </summary>
    public sealed class RiderPackPart
    {
        private RiderPackModel model;

        /// <summary>The supplies as chosen now, or null.</summary>
        public RiderPack Pack => model?.pack;

        /// <summary>Why the send is greyed by the supplies (null: they may go).</summary>
        public string Why => model?.Why;

        /// <summary>Draws the part at the top of <paramref name="r"/>; returns its height.</summary>
        public float Draw(Rect r, Map map, Notable to, Pawn rider)
        {
            var town = to != null ? NotableBook.TownOf(to.town) : null;
            if (map == null || rider == null || town == null)
            {
                model = null;
                return 0f;
            }
            model = RiderPackModel.For(model, map, rider, town);
            model.Refresh();
            float y = r.y;
            var button = new Rect(r.x, y, 140f, 24f);
            if (UiKit.Button(button, "CC_RiderPack_Button".Translate(), ButtonKind.Normal, true, "CC_RiderPack_Button_Tip".Translate().Resolve()))
                Dialog_RiderPack.Open(model, null);
            var line = new Rect(button.xMax + 8f, y, Mathf.Max(0f, r.xMax - button.xMax - 8f), 24f);
            UiKit.LabelFit(line, model.Summary(), model.SummaryColor, GameFont.Tiny);
            if (Mouse.IsOver(line)) TooltipHandler.TipRegion(line, model.Tip());
            y += 26f;
            if (model.verdict.Short && !model.verdict.over)
            {
                bool k = model.pack.knowingly;
                var box = new Rect(r.x, y, Mathf.Min(r.width, 340f), 24f);
                UiKit.Toggle(box, "CC_RiderPack_Knowingly".Translate(), ref k, "CC_RiderPack_Knowingly_Tip".Translate().Resolve());
                model.pack.knowingly = k;
                y += 26f;
            }
            return y - r.y;
        }
    }
}
