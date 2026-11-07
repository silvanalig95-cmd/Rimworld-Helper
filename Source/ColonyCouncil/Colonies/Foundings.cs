using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One of a home's people in the founding window: whether they may go, their wish and why.</summary>
    public sealed class FounderRow
    {
        public Pawn pawn;
        public bool grown;
        public FoundingWish wish;
        public List<string> reasons = new List<string>();
        /// <summary>Why they can't go (in words), or null.</summary>
        public string cant;
    }

    /// <summary>
    /// A fresh start in the same world (docs/design/stage3-many-colonies.md, release 1; the player's decisions of 6
    /// November 2026). The player picks a place on the world map from a colony's card, then who goes from that home
    /// (each grown-up's wish shown, decision 2A), newcomers or not (the game's own window for new people, decision 3A; a
    /// scenario's start is release 3), and whether the old colony is kept (a second home, the seat moved on their word)
    /// or abandoned. The party goes by the journey planner's caravan, or by the colony's own pods, shuttle or gravship
    /// the game's way; when a caravan of theirs reaches the place the colony is asked "Settle here?", which runs the
    /// game's own Settle; any home founded at the place by any way is the new colony. Newcomers only: the settlement and
    /// map are made the game's way and they walk in. Then: the colony's record and the history's milestone, the moods of
    /// those sent or kept against their wish and of close bonds split, the first night at a fire, the parting day on the
    /// old colony's calendar, newcomers with their passage paid (decision 4A) and, with the old colony abandoned, the
    /// game's own abandon once those who go have left. The game's colony limit is raised by one when needed (decision
    /// 5B) and set back once the old colony is gone. Nothing per tick: it rides the council's hour with the homes.
    /// <para>
    /// The hand-over (HandOver, Colonies/HandOvers.cs, release 2a): the old colony handed over instead of kept or
    /// abandoned, in the same act or later, uses the same who-goes list and this plan; in the same act it is the founding
    /// window's third choice, the plan waits with the hand-over until its ceremony, and the newcomers come free. Release 3: a scenario's start
    /// shapes the newcomers, their kit and arrival and Rimfolk's own parts at the new colony (Colonies/ScenarioStarts.cs). Release 4: the
    /// notables and world events near every home.
    /// </para>
    /// </summary>
    public static class Foundings
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Fresh starts are offered (Many colonies: "Fresh starts").</summary>
        public static bool On => S?.freshStarts ?? true;

        /// <summary>Newcomers may be called to a new colony (Many colonies: "Newcomers").</summary>
        public static bool NewcomersOn => On && (S?.newcomers ?? true);

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The founding under way (one at a time), or null.</summary>
        public static FoundingPlan Current => Homes.Instance?.foundings?.FirstOrDefault(f => f != null && f.stage == FoundingStage.Going);

        /// <summary>Every founding still being seen to (on its way, or standing with something still to come).</summary>
        public static IEnumerable<FoundingPlan> All => Homes.Instance?.foundings?.Where(f => f != null) ?? Enumerable.Empty<FoundingPlan>();

        public static FoundingPlan Of(int id) => All.FirstOrDefault(f => f.id == id);

        /// <summary>The founding a journey plan is the party of, or null.</summary>
        public static FoundingPlan OfJourney(JourneyPlan plan) => plan == null || plan.founding < 0 ? null : All.FirstOrDefault(f => f.id == plan.founding);

        // ================= Before: the place and who goes =================

        /// <summary>The homes the game counts against its colony limit (SettleUtility.PlayerSettlementsCountLimitReached).</summary>
        public static int Counted()
        {
            int n = 0;
            foreach (var m in Find.Maps)
                if (m.IsPlayerHome && m.Parent is Settlement || m.wasSpawnedViaGravShipLanding)
                    n++;
            return n;
        }

        /// <summary>Why no new colony can be founded from this home now (in words), or null.</summary>
        public static string WhyNot(Map from)
        {
            if (!On) return "CC_Found_No_Off".Translate();
            if (from == null || !Homes.IsHome(from)) return "CC_Found_No_NotHome".Translate();
            if (MapFacts.InVacuum(from)) return "CC_Found_No_Orbit".Translate();
            if (Current != null) return "CC_Found_No_UnderWay".Translate(Current.place.Named("PLACE")).Resolve();
            // A fresh start in the same act as a hand-over, waiting for its ceremony (Colonies/HandOvers.cs).
            var pending = HandOvers.PendingFounding;
            if (pending != null) return "CC_Found_No_UnderWay".Translate(pending.place.Named("PLACE")).Resolve();
            // This home being handed over on its own: its people are spoken for.
            if (HandOvers.At(from) != null) return "CC_HandOver_FoundNoHandingOver".Translate(Homes.Name(from).Named("HOME")).Resolve();
            if (FoundingMath.LimitFor(Prefs.MaxNumberOfPlayerSettlements, Counted()) < 0)
                return "CC_Found_No_Limit".Translate(Prefs.MaxNumberOfPlayerSettlements.Named("COUNT")).Resolve();
            return null;
        }

        /// <summary>Why a tile won't do for a new colony (the game's own check), or null.</summary>
        public static string TileWhyNot(PlanetTile tile)
        {
            if (!tile.Valid) return "CC_Found_No_Tile".Translate();
            var sb = new System.Text.StringBuilder();
            if (!TileFinder.IsValidTileForNewSettlement(tile, sb)) return sb.ToString();
            return null;
        }

        /// <summary>
        /// A home's people as the founding window lists them: grown-ups first, each with whether they may go, their wish
        /// from their own ties as the party stands (<paramref name="going"/>) and why.
        /// </summary>
        public static List<FounderRow> Rows(Map home, ICollection<Pawn> going)
        {
            var rows = new List<FounderRow>();
            if (home == null) return rows;
            var people = Homes.People(home);
            int from = BanishMath.WindowFrom(Now);
            var banish = BanishTracker.Instance;
            var colony = ColonyState.For(home);
            foreach (var p in people)
            {
                var row = new FounderRow { pawn = p, grown = p.DevelopmentalStage.Adult() };
                var r = banish?.Of(p);
                bool leaving = r != null && (r.stage == BanishStage.ToTell || r.stage == BanishStage.Gathering);
                var other = JourneyBook.PlanOf(p);
                bool otherJourney = other != null && other.founding < 0;
                string cant = FoundingMath.CantSend(true, p.IsQuestLodger(), leaving, otherJourney, p.MapHeld == home);
                if (cant != null) row.cant = ("CC_Found_Cant_" + cant).Translate();
                if (row.grown)
                {
                    var t = TiesOf(p, people, going, colony, banish, from);
                    row.wish = FoundingMath.WishOf(t);
                    row.reasons = FoundingMath.Reasons(t).Select(k => ("CC_Found_Why_" + k).Translate().Resolve()).ToList();
                }
                rows.Add(row);
            }
            rows.Sort((a, b) => a.grown != b.grown ? (a.grown ? -1 : 1) : string.Compare(a.pawn.LabelShort, b.pawn.LabelShort, StringComparison.CurrentCulture));
            return rows;
        }

        /// <summary>What one grown colonist's ties say, with the party as it stands.</summary>
        private static FoundingTies TiesOf(Pawn p, List<Pawn> people, ICollection<Pawn> going, ColonyState colony, BanishTracker banish, int from)
        {
            var t = new FoundingTies();
            foreach (var o in people)
            {
                if (o == p) continue;
                bool goes = going != null && going.Contains(o);
                if (Kin(p, o))
                {
                    if (goes) t.kinGoing++;
                    else t.kinStaying++;
                    continue;
                }
                int opinion = p.relations?.OpinionOf(o) ?? 0;
                if (opinion >= 40)
                {
                    if (goes) t.friendsGoing++;
                    else t.friendsStaying++;
                }
                else if (opinion <= -20)
                {
                    if (goes) t.rivalsGoing++;
                    else t.rivalsStaying++;
                }
            }
            try
            {
                if (banish != null) t.atOdds = banish.TroubleOf(p, people, from).fights >= BanishMath.WordFights;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("founding: at odds", e);
            }
            t.ownRoom = p.ownership?.OwnedRoom != null;
            t.office = colony?.offices?.OfficeOf(p) != null;
            float life = p.RaceProps?.lifeExpectancy ?? 80f;
            float age = p.ageTracker?.AgeBiologicalYearsFloat ?? 30f;
            t.elder = age >= life * 0.8f;
            t.young = age < life * 0.32f;
            return t;
        }

        /// <summary>Their own: a spouse, lover or betrothed, a child or a parent.</summary>
        public static bool Kin(Pawn a, Pawn b)
        {
            if (a?.relations == null || b == null) return false;
            if (LovePartnerRelationUtility.LovePartnerRelationExists(a, b)) return true;
            return a.relations.DirectRelationExists(PawnRelationDefOf.Parent, b) || b.relations != null && b.relations.DirectRelationExists(PawnRelationDefOf.Parent, a);
        }

        /// <summary>A close bond (decision 2A's "parted"): a partner, a parent or child, a sibling, or a close friend (the hand-over's farewells and moods too).</summary>
        public static bool IsClose(Pawn a, Pawn b) => Close(a, b);

        private static bool Close(Pawn a, Pawn b)
        {
            if (a?.relations == null || b == null) return false;
            bool partner = LovePartnerRelationUtility.LovePartnerRelationExists(a, b);
            bool parent = a.relations.DirectRelationExists(PawnRelationDefOf.Parent, b) || b.relations != null && b.relations.DirectRelationExists(PawnRelationDefOf.Parent, a);
            bool sibling = a.GetRelations(b).Contains(PawnRelationDefOf.Sibling);
            return FoundingMath.Close(partner, parent, sibling, a.relations.OpinionOf(b));
        }

        /// <summary>The means the colony has at this home: always the caravan; pods or a shuttle with a launcher there; the gravship with Odyssey's engine there.</summary>
        public static List<FoundingMeans> MeansAt(Map home)
        {
            var list = new List<FoundingMeans> { FoundingMeans.Caravan };
            if (home == null) return list;
            try
            {
                if (home.listerThings.ThingsInGroup(ThingRequestGroup.Transporter).Any(t => t.Faction == Faction.OfPlayer && t.TryGetComp<CompLaunchable>() != null))
                    list.Add(FoundingMeans.Pods);
                if (ModsConfig.OdysseyActive && home.listerBuildings.allBuildingsColonist.Any(b => b is Building_GravEngine)) list.Add(FoundingMeans.Gravship);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("founding: the means at home", e);
            }
            return list;
        }

        // ================= Setting it going =================

        /// <summary>
        /// The founding window's "Found the colony": the plan stored, the game's limit raised by one if the new colony needs
        /// it (and said in the window), the party's journey planned by caravan, or a word for pods and the gravship, or the
        /// newcomers' window at once when nobody goes from the colony.
        /// </summary>
        public static void Begin(FoundingPlan plan)
        {
            var h = Homes.Instance;
            var home = Homes.MapOf(plan.from);
            if (h == null || home == null) return;
            plan.id = h.nextFounding++;
            plan.started = Now;
            plan.stage = FoundingStage.Going;
            plan.fromName = Homes.Name(home);
            h.foundings.Add(plan);
            RaiseLimit(plan);
            switch (plan.means)
            {
                case FoundingMeans.Caravan:
                    // The planner opens on the party's plan, to add pack animals or change what they bring.
                    if (PlanJourney(plan) != null && Find.CurrentMap == home) Dialog_JourneyPlan.Open(true);
                    break;
                case FoundingMeans.Pods:
                case FoundingMeans.Gravship:
                    Tell.Letter(NewsKind.Colony, Importance.Important, home, "CC_Found_SendLabel".Translate(plan.place.Named("PLACE")).Resolve(),
                        ("CC_Found_Send_" + plan.means).Translate(Names(plan.party).Named("NAMES"), plan.place.Named("PLACE")).Resolve() + Limit(plan),
                        LetterDefOf.NeutralEvent, new LookTargets(plan.party.Where(p => p != null && p.Spawned)));
                    break;
                case FoundingMeans.Newcomers:
                    Find.WindowStack.Add(new Dialog_Newcomers(plan));
                    break;
            }
            LensModel.Dirty();
            Cards.Dirty();
        }

        /// <summary>The game's colony limit, raised by one when the new colony would pass it (decision 5B): saved in the game's own preferences.</summary>
        private static void RaiseLimit(FoundingPlan plan)
        {
            int pref = Prefs.MaxNumberOfPlayerSettlements;
            int to = FoundingMath.LimitFor(pref, Counted());
            if (to <= pref) return;
            Prefs.MaxNumberOfPlayerSettlements = to;
            Prefs.Save();
            plan.limitRaisedTo = to;
        }

        /// <summary>A line naming the limit raised, for a founding's first letter or message.</summary>
        private static string Limit(FoundingPlan plan) =>
            plan.limitRaisedTo > 0 ? "\n\n" + "CC_Found_LimitRaised".Translate(plan.limitRaisedTo.Named("COUNT")).Resolve() : "";

        /// <summary>
        /// The party's journey by caravan: the journey planner's own plan from the old home to the place, one way, with food
        /// for the road and fifteen days more, and the founding kit by the colony's age (what raises the first shelter, and
        /// medicine for the first season) from what the stores hold. The player can open it in the planner and change it.
        /// </summary>
        public static JourneyPlan PlanJourney(FoundingPlan plan)
        {
            var home = Homes.MapOf(plan.from);
            if (home == null || !JourneyBook.On) return null;
            var old = JourneyBook.PlanAt(home);
            if (old != null && old.founding != plan.id) return null;
            try
            {
                int now = Find.TickManager.TicksAbs;
                int hourNow = JourneyRun.HourOfDay(now, home);
                var going = plan.party.Where(p => p != null && p.Spawned && p.Map == home).ToList();
                var j = new JourneyPlan
                {
                    map = home, travellers = going, departAbs = JourneyRun.HourStart(now) + JourneyMath.DefaultHoursAhead(hourNow) * GenDate.TicksPerHour,
                    andBack = false, destTile = plan.tile, destName = plan.place, founding = plan.id, daysChosen = true
                };
                var route = JourneyPack.Route(home, j.DestTile, going, 0f, j.departAbs, false, -1f);
                float road = route.oneWayTicks > 0 ? JourneyMath.TripDays(route.oneWayTicks, false) : 2f;
                j.low = route.low;
                j.high = route.high;
                // What they can carry (a caravan over its room doesn't move): the fifteen days' food first, fewer days if
                // it won't fit, then the shelter's materials as far as the room left goes. Pack animals added in the
                // planner make room for more.
                float room = going.Sum(p => MassUtility.Capacity(p)) * FoundingMath.LoadShare;
                var kit = Kit(home, going.Count);
                foreach (int extra in FoundingMath.FoodDaysTried)
                {
                    j.days = road + extra;
                    j.packing = JourneyPack.Packing(home, j.travellers, j.animals, j.days, route, kit);
                    if (JourneyPack.Mass(j.travellers, j.packing) <= room) break;
                }
                foreach (var line in j.packing.Where(l => l.added && l.kind == "other").Reverse())
                {
                    float over = JourneyPack.Mass(j.travellers, j.packing) - room;
                    if (over <= 0f) break;
                    float unit = Mathf.Max(0.01f, JourneyPack.UnitMass(line.def));
                    line.count = Math.Max(0, line.count - Mathf.CeilToInt(over / unit));
                }
                j.packing.RemoveAll(l => l.added && l.count <= 0);
                JourneyRun.Confirm(j, old == null);
                plan.journey = j.id;
                return j;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("founding: the journey", e);
                return null;
            }
        }

        /// <summary>
        /// The founding kit's own lines (the planner keeps them as the player's): what raises the first shelter by the
        /// colony's means, wood for a tribe or a medieval colony, steel and components for industry and beyond, and
        /// medicine for the first season, each as much as the stores hold.
        /// </summary>
        private static List<JourneyLine> Kit(Map home, int people)
        {
            var lines = new List<JourneyLine>();
            var (wood, steel, components) = FoundingMath.Shelter((int)Ages.Of(home), people);
            void Add(ThingDef def, int want, string kind)
            {
                if (def == null || want <= 0) return;
                int have = JourneyPack.InStores(home, def);
                int n = Math.Min(want, have);
                if (n > 0) lines.Add(new JourneyLine(def, n, kind) { added = true });
            }
            Add(ThingDefOf.WoodLog, wood, "other");
            Add(ThingDefOf.Steel, steel, "other");
            Add(ThingDefOf.ComponentIndustrial, components, "other");
            var meds = new[] { ThingDefOf.MedicineIndustrial, ThingDefOf.MedicineHerbal, ThingDefOf.MedicineUltratech }.Where(d => d != null)
                .OrderByDescending(d => JourneyPack.InStores(home, d)).FirstOrDefault();
            Add(meds, FoundingMath.KitMedicine(people), "medicine");
            return lines;
        }

        /// <summary>The founding called off (from the colony's card): the journey too while it hasn't left, and the game's limit set back if nothing was founded.</summary>
        public static void CallOff(FoundingPlan plan)
        {
            var h = Homes.Instance;
            if (h == null || plan == null) return;
            var j = JourneyBook.Plans.FirstOrDefault(x => x.founding == plan.id);
            if (j != null) JourneyRun.CallOff(j, "CC_Journey_CalledOff");
            if (plan.stage == FoundingStage.Going) LowerLimit(plan, true);
            h.foundings.Remove(plan);
            // direct: the answer to the player's own click (the card's "Call off").
            Messages.Message("CC_Found_CalledOff".Translate(plan.place.Named("PLACE")).Resolve(), MessageTypeDefOf.NeutralEvent, false);
            LensModel.Dirty();
            Cards.Dirty();
        }

        /// <summary>The game's limit back down by one, while it is what Rimfolk set and the homes left fit under it.</summary>
        private static void LowerLimit(FoundingPlan plan, bool calledOff)
        {
            if (plan.limitRaisedTo <= 0) return;
            int pref = Prefs.MaxNumberOfPlayerSettlements;
            int back = calledOff ? (pref == plan.limitRaisedTo && Counted() <= pref - 1 ? pref - 1 : pref) : FoundingMath.LimitBack(pref, plan.limitRaisedTo, Counted());
            plan.limitRaisedTo = -1;
            if (back == pref) return;
            Prefs.MaxNumberOfPlayerSettlements = back;
            Prefs.Save();
        }

        // ================= The hour =================

        /// <summary>
        /// In the council's hour, after the homes: a caravan of the party at the place is asked whether to settle; a home
        /// founded at the place is the new colony; and for a colony standing, what is still to come (the newcomers asked
        /// for, the first night, the old colony abandoned once those who go have left). Then the questions about moving.
        /// </summary>
        public static void Hourly()
        {
            var h = Homes.Instance;
            if (h == null || h.foundings.Count == 0 && h.asks.Count == 0) return;
            foreach (var plan in h.foundings.ToList())
            {
                if (plan == null) continue;
                try
                {
                    if (plan.stage == FoundingStage.Going) Going(plan);
                    else Standing(plan);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("founding: the hour", e);
                }
            }
            try
            {
                Asks(h);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("founding: asking to move", e);
            }
        }

        private static void Going(FoundingPlan plan)
        {
            // A home founded at the place, by any way (the Settle question, the game's own Settle, a gravship's landing).
            foreach (var map in Homes.All)
            {
                if (map.Parent == null || Homes.KeyOf(map) == plan.from || Settled(map) < plan.started) continue;
                // The gravship's landing is the new colony wherever it came down; any other way, at the place chosen.
                if (plan.means != FoundingMeans.Gravship && !FoundingMath.AtPlace(Distance(map.Tile, plan.Tile))) continue;
                Founded(plan, map);
                return;
            }
            if (plan.means == FoundingMeans.Newcomers) return;
            // A caravan of theirs at the place, stopped: asked once whether to settle here.
            foreach (var c in Find.WorldObjects.Caravans)
            {
                if (c == null || !c.IsPlayerControlled || c.ID == plan.askedCaravan || c.pather != null && c.pather.Moving) continue;
                if (!FoundingMath.AtPlace(Distance(c.Tile, plan.Tile))) continue;
                if (!c.PawnsListForReading.Any(p => plan.party.Contains(p) || plan.party.Count == 0 && p.IsFreeColonist)) continue;
                if (Find.WorldObjects.AnySettlementBaseAt(c.Tile) || TileWhyNot(c.Tile) != null) continue;
                plan.askedCaravan = c.ID;
                FoundingLetters.AskSettle(plan, c);
                return;
            }
        }

        /// <summary>When a home was made: its settlement's creation tick, else its map's.</summary>
        internal static int Settled(Map map) => map.Parent != null && map.Parent.creationGameTicks >= 0 ? map.Parent.creationGameTicks : map.generationTick;

        private static float Distance(PlanetTile a, PlanetTile b)
        {
            if (!a.Valid || !b.Valid) return -1f;
            if (a == b) return 0f;
            try
            {
                return Find.WorldGrid.ApproxDistanceInTiles(a, b);
            }
            catch (Exception)
            {
                return -1f;
            }
        }

        /// <summary>"Settle here": the game's own Settle, with its own warning near another people's town.</summary>
        public static void Settle(FoundingPlan plan, Caravan c)
        {
            if (plan == null || c == null || c.Destroyed) return;
            string why = TileWhyNot(c.Tile);
            if (why != null)
            {
                Messages.Message(why, c, MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (SettleUtility.PlayerSettlementsCountLimitReached)
            {
                Messages.Message("CC_Found_No_Limit".Translate(Prefs.MaxNumberOfPlayerSettlements.Named("COUNT")).Resolve(), c, MessageTypeDefOf.RejectInput, false);
                return;
            }
            SettlementProximityGoodwillUtility.CheckConfirmSettle(c.Tile, () => SettleInEmptyTileUtility.Settle(c));
        }

        // ================= The colony stands =================

        /// <summary>
        /// The new colony stands: its record and its mother's, the milestone, its letter, the seat moved if the player
        /// asked, the moods of those sent or kept against their wish and of close bonds split, the questions about moving,
        /// and the parting day on the old colony's calendar.
        /// </summary>
        public static void Founded(FoundingPlan plan, Map map, bool tell = true)
        {
            plan.stage = FoundingStage.Founded;
            plan.founded = Now;
            plan.home = Homes.KeyOf(map);
            var mother = Homes.MapOf(plan.from);
            string name = Homes.Name(map);
            try
            {
                // The record of every colony: the mother's, from its own founding day, and the new one's.
                if (mother != null) WorldLedger.NoteColony(mother, CalendarTracker.Instance?.Colony(mother)?.FoundedTick ?? 0);
                else
                {
                    // Gone already (a gravship's takeoff without an anchor): its record, ended as the game ended it, and
                    // nothing kept whatever was planned.
                    WorldLedger.NoteColony(plan.from, plan.fromName, -1, 0);
                    WorldLedger.EndColony(plan.from, ColonyEnd.Abandoned, -1);
                    plan.keepOld = false;
                }
                WorldLedger.NoteColony(map, Now, plan.from, plan.fromName);
                // Each colony its own character (decision 13A): one founded by people of another starts with half its
                // mother's; newcomers alone start fresh (Character/ColonyCharacters.cs).
                CouncilGameComponent.Instance?.CharacterFounded(map, plan.from, plan.party.Count(p => p != null && !p.Dead));
                var founders = map.mapPawns.FreeColonists.Where(p => plan.party.Contains(p)).ToList();
                if (founders.Count == 0) founders = plan.party.Where(p => p != null && !p.Dead).ToList();
                var actors = founders.Select(p => new HistoryActor(Refs.Of(p), HistoryRole.Did)).ToList();
                actors.Add(new HistoryActor(Ref.Colony(plan.from), HistoryRole.About));
                string who = founders.Count > 0 ? Names(founders) : "CC_Found_Newcomers".Translate().Resolve();
                // The founding party remember it together (Social/Talk/SharedTimesTogether.cs).
                SharedTimesTogether.Founded(founders, name);
                History.Add(FoundingMath.KindFounded, actors, Refs.Colony(map), HistoryMath.Milestone,
                    (founders.Count > 0 ? "CC_Found_History" : "CC_Found_HistoryNewcomers").Translate(name.Named("PLACE"), who.Named("NAMES"), (plan.fromName ?? "?").Named("MOTHER")).Resolve());
                bool seat = plan.makeSeat && plan.keepOld && ColonySeat.Move(map, true);
                string text = TextVariants.Vary("CC_Found_LetterText").Translate(name.Named("PLACE"), who.Named("NAMES"), (plan.fromName ?? "?").Named("MOTHER"),
                    Ages.Form(AgeFeature.Party, Ages.Of(map), Ages.Of(map)).word.Named("PARTY")).Resolve();
                text += " " + OldColonyLine(plan, map, seat);
                if (plan.makeSeat && plan.keepOld && !seat) text += " " + "CC_Found_SeatNotYet".Translate(ColonySeat.WhyNot(map).Named("WHY")).Resolve();
                if (plan.newcomers && !plan.newcomersChosen) text += " " + "CC_Found_NewcomersComing".Translate().Resolve();
                plan.seatMoved = seat;
                // Founded by newcomers alone, their own letter tells it as they walk in.
                if (tell)
                    Tell.Letter(NewsKind.Colony, Importance.Important, map, "CC_Found_LetterLabel".Translate(name.Named("PLACE")).Resolve(), text, LetterDefOf.PositiveEvent,
                        new LookTargets(map.Parent));
                Moods(plan, map, mother);
                if (plan.keepOld && mother != null) PartingDay(plan, mother, name);
                // The old colony handed over before the new one stood: its founding day kept here as a remembrance.
                if (plan.handOver && plan.abandoned) ColonyCalendar.Notify_OldFounding(map, plan.fromName, WorldLedger.ColonyRecord(plan.from)?.founded ?? -1);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("founding: the colony stands", e);
            }
            LensModel.Dirty();
            Cards.Dirty();
        }

        /// <summary>What becomes of the old colony, in a sentence: kept (with the seat here or not), or abandoned once those who go have left.</summary>
        internal static string OldColonyLine(FoundingPlan plan, Map map, bool seat) =>
            Homes.MapOf(plan.from) == null ? (plan.handOver ? "CC_HandOver_FoundOldGone" : "CC_Found_OldGone").Translate((plan.fromName ?? "?").Named("MOTHER")).Resolve()
            : plan.handOver ? "CC_HandOver_FoundHandingOver".Translate((plan.fromName ?? "?").Named("MOTHER")).Resolve()
            : plan.keepOld
                ? (seat ? "CC_Found_Kept_Seat" : "CC_Found_Kept").Translate((plan.fromName ?? "?").Named("MOTHER"), ColonySeat.Word(map).Named("SEAT")).Resolve()
                : "CC_Found_Abandoning".Translate((plan.fromName ?? "?").Named("MOTHER")).Resolve();

        /// <summary>
        /// Uprooted (a season) for those sent or kept against their wish, with a question later about moving when the old
        /// colony is kept; parted (a season) from someone close on the other side.
        /// </summary>
        private static void Moods(FoundingPlan plan, Map map, Map mother)
        {
            if (plan.moodsGiven) return;
            plan.moodsGiven = true;
            var h = Homes.Instance;
            foreach (var p in plan.sentAgainst.Where(p => p != null && !p.Dead))
            {
                CouncilGameComponent.GiveThought(p, FoundingDefOf.Rimfolk_Uprooted, null, -1f, "CC_Found_UprootedSent".Translate((plan.fromName ?? "?").Named("MOTHER")).Resolve());
                if (plan.keepOld && h != null) h.asks.Add(new FoundingAsk { pawn = p, wants = plan.from, founded = Now });
            }
            if (plan.keepOld)
                foreach (var p in plan.keptAgainst.Where(p => p != null && !p.Dead))
                {
                    CouncilGameComponent.GiveThought(p, FoundingDefOf.Rimfolk_Uprooted, null, -1f, "CC_Found_UprootedKept".Translate(Homes.Name(map).Named("PLACE")).Resolve());
                    if (h != null) h.asks.Add(new FoundingAsk { pawn = p, wants = plan.home, founded = Now });
                }
            // apart: close bonds split between the two colonies, while both stand: they are at two homes by now, which is the mood.
            if (!plan.keepOld || mother == null) return;
            var stayed = Homes.People(mother).Where(p => !plan.party.Contains(p)).ToList();
            var went = plan.party.Where(p => p != null && !p.Dead).ToList();
            void Parted(Pawn a, IEnumerable<Pawn> others)
            {
                if (a == null || !People.HasInnerLife(a)) return;
                int n = 0;
                foreach (var b in others.Where(b => b != null && b != a && Close(a, b)).OrderByDescending(b => a.relations.OpinionOf(b)))
                {
                    if (n++ >= FoundingMath.MaxParted) break;
                    CouncilGameComponent.GiveThought(a, FoundingDefOf.Rimfolk_Parted, b);
                }
            }
            foreach (var a in went) Parted(a, stayed);
            foreach (var a in stayed) Parted(a, went);
        }

        /// <summary>The parting day on the old colony's calendar: the day its people set out to found the new one, which it may keep as a tradition.</summary>
        private static void PartingDay(FoundingPlan plan, Map mother, string name)
        {
            try
            {
                ColonyCalendar.Notify_Parting(mother, name, plan.party);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("founding: the parting day", e);
            }
        }

        /// <summary>A standing colony's founding still being seen to: the newcomers, the first night, the old colony abandoned. Done, it is let go.</summary>
        private static void Standing(FoundingPlan plan)
        {
            var map = Homes.MapOf(plan.home);
            var h = Homes.Instance;
            if (map == null)
            {
                // The new colony is gone already: nothing more comes of it.
                h?.foundings.Remove(plan);
                return;
            }
            if (plan.newcomers && !plan.newcomersChosen && !plan.newcomersAsked && NewcomersOn)
            {
                plan.newcomersAsked = true;
                FoundingLetters.AskNewcomers(plan, map);
            }
            if (!plan.firstNight) FirstNight(plan, map);
            // A scenario's start (release 3): Rimfolk's own parts once its people are there (Colonies/ScenarioStarts.cs).
            if (plan.scenarioPeople.Count > 0) ScenarioStarts.SetUpWhenHere(plan, map);
            // Handed over in the same act (HandOver): the hand-over makes the town once those who go have left (Colonies/HandOvers.cs).
            if (!plan.keepOld && !plan.abandoned && !plan.handOver) AbandonWhenLeft(plan);
            bool newcomersDone = !plan.newcomers || plan.newcomersChosen || !NewcomersOn || Now - plan.founded > 10 * GenDate.TicksPerDay;
            if (plan.newcomers && !plan.newcomersChosen && newcomersDone)
            {
                // The call was never answered: let go, and said.
                plan.newcomers = false;
                FoundingLetters.RemoveNewcomerLetters(plan);
                Tell.Minor(NewsKind.Colony, Importance.Notable, map, "CC_Found_NewcomersLapsed".Translate(Homes.Name(map).Named("PLACE")).Resolve(), new LookTargets(map.Parent));
            }
            if (plan.firstNight && (plan.keepOld || plan.abandoned || plan.handOver && HandOvers.OfFounding(plan.id) == null) && newcomersDone && plan.scenarioPeople.Count == 0)
                h?.foundings.Remove(plan);
        }

        /// <summary>
        /// The first night: the founders gather at a fire (laid from the wood they brought where there is none), and the
        /// speaker-to-be (a seat holder among them, else the eldest) speaks; each feels the new hearth. Played out on the
        /// map with "Answers play out" on; else the words and the mood.
        /// </summary>
        private static void FirstNight(FoundingPlan plan, Map map)
        {
            int hour = GenLocalDate.HourOfDay(map);
            if (Now - plan.founded < 2 * GenDate.TicksPerHour || hour < 17 || hour > 21) return;
            var founders = map.mapPawns.FreeColonistsSpawned.Where(p => PlayOuts.OurPerson(p) && !p.Downed && !p.InMentalState).ToList();
            plan.firstNight = true;
            if (founders.Count == 0) return;
            var speaker = founders.Where(p => p.DevelopmentalStage.Adult()).OrderByDescending(p => OfficeTracker.HeldBy(p) != null ? 1 : 0)
                .ThenByDescending(p => p.ageTracker.AgeBiologicalYearsFloat).FirstOrDefault() ?? founders[0];
            string cause = "CC_Found_FirstNightCause".Translate(Homes.Name(map).Named("PLACE")).Resolve();
            var tracker = PlayOutTracker.Instance;
            if (PlayOuts.On && tracker != null)
            {
                try
                {
                    var p = new PlayOut
                    {
                        kind = PlayKind.Bonfire, map = map, created = Now, label = "CC_Found_FirstNightLabel".Translate().Resolve(), invited = founders, organizer = speaker,
                        spots = new List<PlaySpot> { PlaySpot.Fire, PlaySpot.Outdoors, PlaySpot.Gather }, durationTicks = PlayOutMath.DurationTicks(2f),
                        pool = "Rimfolk_Pool_FirstNight", hourFrom = 17, hourTo = 23, buildFire = true, music = false
                    };
                    p.deadline = PlayOutMath.Deadline(Now, hour, p.hourFrom, p.hourTo, PlayOutMath.DefaultWaitHours);
                    foreach (var x in founders) p.moods.Add(new HeldMood { pawn = x, thought = FoundingDefOf.Rimfolk_FirstNight, cause = cause });
                    tracker.Add(p);
                    return;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("founding: the first night", e);
                }
            }
            // apart: with answers not played out on the map, the first night is the speaker's word and the founders' mood, as any gathering is then.
            if (Speech.On) Speech.Say(speaker, Flavor.FromPool("Rimfolk_Pool_FirstNight", speaker, null, null, "speaker"));
            foreach (var x in founders) CouncilGameComponent.GiveThought(x, FoundingDefOf.Rimfolk_FirstNight, null, -1f, cause);
        }

        // ================= The old colony abandoned =================

        /// <summary>
        /// With the old colony to be abandoned: once the new one stands and none of those who go is still on the old map,
        /// the game's own abandon, at the next frame (outside the hour's loops).
        /// </summary>
        private static void AbandonWhenLeft(FoundingPlan plan)
        {
            var old = Homes.MapOf(plan.from);
            if (old == null)
            {
                // Gone already (a gravship's takeoff without an anchor, or the player's own Abandon).
                Ended(plan, -1);
                return;
            }
            var still = plan.party.Where(p => p != null && !p.Dead && p.Spawned && p.Map == old).ToList();
            if (still.Count > 0)
            {
                // Someone of the party never left: the old colony waits for them, and the colony is told once.
                if (!plan.waitTold && !still.Any(p => Verse.AI.Group.LordUtility.GetLord(p)?.LordJob is LordJob_FormAndSendCaravan))
                {
                    plan.waitTold = true;
                    Tell.Message(NewsKind.Colony, Importance.Important, old, "CC_Found_AbandonWaits".Translate(Names(still).Named("NAMES"), (plan.fromName ?? "?").Named("MOTHER")).Resolve(),
                        new LookTargets(still), MessageTypeDefOf.CautionInput);
                }
                return;
            }
            plan.abandoned = true;
            LongEventHandler.QueueLongEvent(() => Abandon(plan, old), "CC_Found_Abandoning_Long", false, null);
        }

        private static void Abandon(FoundingPlan plan, Map old)
        {
            try
            {
                var parent = old?.Parent;
                if (parent == null || parent.Destroyed || !Homes.IsHome(old)) return;
                var tile = parent.Tile;
                // The council goes with its people to the new home (7a's rule, as a gravship landing carries it).
                var comp = CouncilGameComponent.Instance;
                if (comp != null) FoundingMath.CarryCouncil(comp.colonies, plan.from, plan.home);
                // And its character with it: the colony moved, its ways and what its work made it go too.
                comp?.CharacterCarried(plan.from, plan.home);
                parent.Abandon(false);
                Find.GameEnder.CheckOrUpdateGameOver();
                var ruin = Find.WorldObjects.AllWorldObjects.FirstOrDefault(o => o != null && o.Tile == tile && o.def == WorldObjectDefOf.AbandonedSettlement);
                Ended(plan, ruin?.ID ?? -1);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("founding: abandoning the old colony", e);
            }
        }

        /// <summary>The old colony is gone: its record ends "abandoned", the milestone, its founding day kept at the new colony, and the game's limit set back.</summary>
        private static void Ended(FoundingPlan plan, int ruin)
        {
            plan.abandoned = true;
            WorldLedger.EndColony(plan.from, ColonyEnd.Abandoned, ruin);
            var map = Homes.MapOf(plan.home);
            History.Add(FoundingMath.KindAbandoned, new List<HistoryActor> { new HistoryActor(Ref.Colony(plan.from), HistoryRole.About) }, map != null ? Refs.Colony(map) : Ref.Colony(plan.home),
                HistoryMath.Milestone, "CC_Found_AbandonedHistory".Translate((plan.fromName ?? "?").Named("MOTHER"), (map != null ? Homes.Name(map) : "?").Named("PLACE")).Resolve());
            if (map != null)
            {
                try
                {
                    var record = WorldLedger.ColonyRecord(plan.from);
                    ColonyCalendar.Notify_OldFounding(map, plan.fromName, record?.founded ?? -1);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("founding: the old founding day", e);
                }
            }
            LowerLimit(plan, false);
            LensModel.Dirty();
            Cards.Dirty();
        }

        /// <summary>
        /// The old colony handed over in the same act (HandOver): the founding is done with it, its founding day kept at the
        /// new colony as a remembrance, and the game's limit set back as after an abandon.
        /// </summary>
        public static void HandedOver(FoundingPlan plan)
        {
            if (plan == null) return;
            plan.abandoned = true;
            var map = Homes.MapOf(plan.home);
            if (map != null)
            {
                try
                {
                    var record = WorldLedger.ColonyRecord(plan.from);
                    ColonyCalendar.Notify_OldFounding(map, plan.fromName, record?.founded ?? -1);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("founding: the old founding day", e);
                }
            }
            LowerLimit(plan, false);
            LensModel.Dirty();
            Cards.Dirty();
        }

        // ================= The newcomers =================

        /// <summary>
        /// The newcomers chosen in the game's window for new people: their passage paid from the old colony's stores
        /// (named before the choice; free when the old colony is abandoned in the same act), then they walk in from the
        /// edge of the new colony's map; with nobody going from the colony, the settlement and map are made first. With a
        /// scenario's start (<paramref name="kit"/>, release 3) they arrive the scenario's way with its kit
        /// (Colonies/ScenarioStarts.cs).
        /// </summary>
        public static void NewcomersCome(FoundingPlan plan, List<Pawn> pawns, Dictionary<Pawn, List<ThingDefCount>> possessions, int price, ScenarioKit kit = null)
        {
            if (plan == null || pawns == null || pawns.Count == 0) return;
            var h = Homes.Instance;
            if (price > 0)
            {
                var payFrom = Homes.MapOf(plan.from) ?? Homes.Seat;
                if (!PayPassage(payFrom, price)) return;
                if (h != null) h.newcomersPaidAt = Now;
            }
            plan.newcomersChosen = true;
            FoundingLetters.RemoveNewcomerLetters(plan);
            var map = Homes.MapOf(plan.home);
            if (map != null)
            {
                Arrive(map, pawns, possessions, plan, price, kit);
                return;
            }
            // Nobody went from the colony: the settlement and its map made the game's way, then they walk in.
            var tile = plan.Tile;
            if (TileWhyNot(tile) != null || Find.WorldObjects.AnySettlementBaseAt(tile))
            {
                Messages.Message(TileWhyNot(tile) ?? "CC_Found_No_Tile".Translate().ToString(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            var settlement = SettleUtility.AddNewHome(tile, Faction.OfPlayer);
            LongEventHandler.QueueLongEvent(() => GetOrGenerateMapUtility.GetOrGenerateMap(tile, Find.World.info.initialMapSize, null), "GeneratingMap", true,
                GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
            LongEventHandler.QueueLongEvent(() =>
            {
                var made = settlement.Map;
                if (made == null) return;
                settlement.Notify_MyMapSettled(made);
                Homes.Clear();
                Founded(plan, made, false);
                Arrive(made, pawns, possessions, plan, price, kit, true);
            }, "CC_Found_Newcomers_Long", true, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
        }

        /// <summary>The newcomers arrive: as a new game's group they walk in; with a scenario's start, its way (Colonies/ScenarioStarts.cs).</summary>
        private static void Arrive(Map map, List<Pawn> pawns, Dictionary<Pawn, List<ThingDefCount>> possessions, FoundingPlan plan, int price, ScenarioKit kit, bool founding = false)
        {
            if (kit == null) WalkIn(map, pawns, possessions, plan, price, founding);
            else ScenarioStarts.Arrive(map, pawns, possessions, plan, price, kit, founding);
        }

        /// <summary>An edge they can walk in from: toward the colony's people where they are, else toward the middle of the land.</summary>
        internal static IntVec3 EdgeCell(Map map)
        {
            bool people = map.mapPawns.FreeColonistsSpawnedCount > 0;
            if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && (people ? map.reachability.CanReachColony(c) : map.reachability.CanReach(c, map.Center, Verse.AI.PathEndMode.Touch,
                    TraverseMode.PassDoors, Danger.Deadly)), map, CellFinder.EdgeRoadChance_Neutral, out var cell)
                && !CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map), map, CellFinder.EdgeRoadChance_Neutral, out cell))
                cell = CellFinder.RandomEdgeCell(map);
            return cell;
        }

        /// <summary>What a newcomer brought, on them as the game's own new wanderers carry it: a weapon in hand, clothes worn, the rest in their pack.</summary>
        internal static void GivePossessions(Pawn p, List<ThingDefCount> things)
        {
            if (p == null || things == null) return;
            foreach (var t in things)
            {
                if (!(StartingPawnUtility.GenerateStartingPossession(t) is ThingWithComps thing)) continue;
                // A weapon in hand, unless they hold one already (then in their pack).
                if (thing.HasComp<CompEquippable>() && p.equipment != null && p.equipment.Primary == null) p.equipment.AddEquipment(thing);
                else if (thing is Apparel apparel) p.apparel.Wear(apparel);
                else p.inventory.innerContainer.TryAdd(thing);
            }
        }

        /// <summary>The newcomers come in from the edge of the map together, with what they brought, as the game's own new wanderers do.</summary>
        private static void WalkIn(Map map, List<Pawn> pawns, Dictionary<Pawn, List<ThingDefCount>> possessions, FoundingPlan plan, int price, bool founding = false)
        {
            var cell = EdgeCell(map);
            var came = new List<Pawn>();
            foreach (var p in pawns)
            {
                if (p == null || p.Dead || p.Destroyed) continue;
                if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
                var at = CellFinder.RandomClosewalkCellNear(cell, map, 3);
                GenSpawn.Spawn(p, at, map);
                if (possessions != null && possessions.TryGetValue(p, out var things)) GivePossessions(p, things);
                came.Add(p);
            }
            if (came.Count == 0) return;
            FloodFillerFog.FloodUnfog(cell, map);
            string call = Ages.Form(AgeFeature.Call, Ages.Of(map), Ages.Of(map)).word;
            string text = TextVariants.Vary(founding ? "CC_Found_NewcomersFoundText" : "CC_Found_NewcomersText").Translate(Names(came).Named("NAMES"), Homes.Name(map).Named("PLACE"), call.Named("CALL"),
                (plan.fromName ?? "?").Named("MOTHER")).Resolve();
            if (founding) text += " " + OldColonyLine(plan, map, plan.seatMoved);
            if (price > 0) text += " " + "CC_Found_NewcomersPaid".Translate(price.Named("SILVER"), (plan.fromName ?? "?").Named("MOTHER")).Resolve();
            else text += " " + "CC_Found_NewcomersFree".Translate().Resolve();
            Tell.Letter(NewsKind.Colony, Importance.Important, map, "CC_Found_NewcomersLabel".Translate(Homes.Name(map).Named("PLACE")).Resolve(), text, LetterDefOf.PositiveEvent,
                new LookTargets(came));
            History.Add(FoundingMath.KindFounded + ":newcomers", came.Select(p => new HistoryActor(Refs.Of(p), HistoryRole.Did)).ToList(), Refs.Colony(map), HistoryMath.Everyday,
                "CC_Found_NewcomersHistory".Translate(Names(came).Named("NAMES"), Homes.Name(map).Named("PLACE")).Resolve());
            FirstWords(came, founding, "walked", false);
            CameraJumper.TryJump(came[0]);
        }

        /// <summary>
        /// Each newcomer's first journal entry as they come (Rimfolk_Journal_NewcomerFirst), in place of the one the day's
        /// journal pass writes, which is in the voice of someone joining people already there: founding (nobody of the
        /// colony on the land before them) or joining, how they came (walked, pods, gravship), a scenario's start, alone (the
        /// only one who came), and whether they are a mechanitor or grew up in a tribe.
        /// </summary>
        internal static void FirstWords(List<Pawn> came, bool founding, string how, bool scenario)
        {
            var journals = CouncilGameComponent.Instance?.journals;
            if (journals == null || came == null) return;
            bool alone = came.Count(x => x != null && !x.Dead && x.RaceProps.Humanlike && !x.DevelopmentalStage.Baby()) == 1;
            foreach (var p in came)
            {
                try
                {
                    if (p == null || p.Dead || !p.RaceProps.Humanlike || p.DevelopmentalStage.Baby() || journals.For(p) != null) continue;
                    var tags = new List<string> { founding ? "founding" : "joining", how };
                    if (scenario) tags.Add("scenario");
                    if (alone) tags.Add("alone");
                    if (ModsConfig.BiotechActive && MechanitorUtility.IsMechanitor(p)) tags.Add("mechanitor");
                    if (p.story?.Childhood?.spawnCategories?.Contains("Tribal") == true) tags.Add("tribalborn");
                    journals.WriteTagged(p, NewcomerFirstPool, tags.ToArray());
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("founding: a newcomer's first words", e);
                }
            }
        }

        public const string NewcomerFirstPool = "Rimfolk_Journal_NewcomerFirst";

        /// <summary>The silver and food a passage costs, from the stores: the food first, by its market value, then the silver (FoundingMath.Pay).</summary>
        public static bool CanPay(Map map, int price, out int silver, out float food)
        {
            silver = 0;
            food = 0f;
            if (price <= 0) return true;
            if (map == null) return false;
            return FoundingMath.Pay(price, StoredSilver(map), StoredFood(map).Sum(t => t.MarketValue * t.stackCount), out silver, out food);
        }

        private static int StoredSilver(Map map) => map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer)).Sum(t => t.stackCount);

        private static List<Thing> StoredFood(Map map) =>
            map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer) && t.def.IsNutritionGivingIngestible && t.def.ingestible.HumanEdible && !t.def.IsDrug)
                .OrderBy(t => t.def.ingestible.preferability).ToList();

        private static bool PayPassage(Map map, int price)
        {
            if (!CanPay(map, price, out int silver, out float food))
            {
                Messages.Message("CC_Found_CantPay".Translate(price.Named("SILVER")).Resolve(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            // The plainest food first, by its worth, as much as the passage takes.
            float left = food;
            foreach (var t in StoredFood(map))
            {
                if (left <= 0.5f) break;
                float each = Mathf.Max(0.01f, t.MarketValue);
                int n = Mathf.Min(t.stackCount, Mathf.CeilToInt(left / each));
                left -= n * each;
                t.SplitOff(n).Destroy();
            }
            int s = silver;
            foreach (var t in map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer)).ToList())
            {
                if (s <= 0) break;
                int n = Mathf.Min(s, t.stackCount);
                s -= n;
                t.SplitOff(n).Destroy();
            }
            return true;
        }

        // ================= Asking to move =================

        /// <summary>
        /// Someone sent or kept against their wish asks, once in the season, to move to the colony they wanted: the
        /// journey planner's plan to take them there, or not.
        /// </summary>
        private static void Asks(Homes h)
        {
            if (h.asks.Count == 0) return;
            int now = Now;
            h.asks.RemoveAll(a => a?.pawn == null || a.pawn.Dead || !People.StillOurs(a.pawn) || a.asked || FoundingMath.AskMoveLapsed(now, a.founded) || Homes.MapOf(a.wants) == null);
            foreach (var a in h.asks)
            {
                if (!FoundingMath.AskMoveDue(now, a.founded, a.asked) || !Tell.CanAsk) continue;
                var at = a.pawn.MapHeld;
                if (at == null || !Homes.IsHome(at) || Homes.KeyOf(at) == a.wants || JourneyBook.PlanOf(a.pawn) != null || JourneyBook.PlanAt(at) != null) continue;
                if (!a.pawn.Spawned || a.pawn.Downed || a.pawn.InMentalState) continue;
                a.asked = true;
                FoundingLetters.AskMove(a.pawn, at, Homes.MapOf(a.wants));
                return;
            }
        }

        /// <summary>"Let them go": a journey plan of one, from their home to the colony they wanted.</summary>
        public static void PlanMove(Pawn p, Map from, Map to)
        {
            if (p == null || from == null || to?.Parent == null || !JourneyBook.On || JourneyBook.PlanAt(from) != null) return;
            int now = Find.TickManager.TicksAbs;
            var j = new JourneyPlan
            {
                map = from, travellers = new List<Pawn> { p }, departAbs = JourneyRun.HourStart(now) + JourneyMath.DefaultHoursAhead(JourneyRun.HourOfDay(now, from)) * GenDate.TicksPerHour,
                andBack = false, destTile = to.Tile.tileId, destObject = to.Parent.ID, destName = Homes.Name(to), daysChosen = false
            };
            var route = JourneyPack.Route(from, j.DestTile, j.travellers, 0f, j.departAbs, false, -1f);
            j.days = route.oneWayTicks > 0 ? JourneyMath.TripDays(route.oneWayTicks, false) : 2f;
            j.packing = JourneyPack.Packing(from, j.travellers, j.animals, j.days, route, null);
            JourneyRun.Confirm(j, true);
        }

        /// <summary>Homes lost: a colony on the record whose home is gone (the player's own Abandon, a home taken) ends there.</summary>
        public static void HomesLost(IReadOnlyList<int> lost)
        {
            if (lost == null) return;
            foreach (int key in lost)
            {
                var r = WorldLedger.ColonyRecord(key);
                if (r == null || !r.Standing) continue;
                var ruin = r.tile < 0 ? null : Find.WorldObjects.AllWorldObjects.FirstOrDefault(o => o != null && o.Tile.tileId == r.tile && o.def == WorldObjectDefOf.AbandonedSettlement);
                WorldLedger.EndColony(key, ruin != null ? ColonyEnd.Abandoned : ColonyEnd.Lost, ruin?.ID ?? -1);
            }
        }

        // ================= Words =================

        public static string Names(IEnumerable<Pawn> pawns) => TextUtil.JoinAnd(pawns.Where(p => p != null).Select(p => p.LabelShort).ToList());

        /// <summary>The place in words: its land and how far it is from the home it sets out from.</summary>
        public static string PlaceName(PlanetTile tile, Map from)
        {
            int tiles = from != null ? Mathf.RoundToInt(Find.WorldGrid.ApproxDistanceInTiles(from.Tile, tile)) : 0;
            return "CC_Journey_Place".Translate(tile.Tile?.PrimaryBiome?.label ?? "?", tiles).Resolve();
        }
    }

    /// <summary>Listens for homes lost (Homes.Changed): a colony on the record whose home is gone ends there, abandoned (the game's ruin at its place) or lost.</summary>
    [StaticConstructorOnStartup]
    public static class FoundingListener
    {
        static FoundingListener()
        {
            Homes.Changed += (founded, lost) => Foundings.HomesLost(lost);
        }
    }

    [DefOf]
    public static class FoundingDefOf
    {
        public static ThoughtDef Rimfolk_Uprooted;
        public static ThoughtDef Rimfolk_Parted;
        public static ThoughtDef Rimfolk_FirstNight;
        public static LetterDef Rimfolk_FoundingLetter;

        static FoundingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FoundingDefOf));
        }
    }
}
