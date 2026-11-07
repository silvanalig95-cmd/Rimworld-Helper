using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's own envoy at their town for a season (docs/design/stage2-diplomacy.md, round a, item 4; decision 6):
    /// a colonist good with people, chosen by the player, whose work is lost to the colony for the season (said before the
    /// choice). They take food that keeps for the road from the stores, walk to the map's edge and set out as a caravan of
    /// their own, on the game's roads; at the town they stay as the colony's envoy (kept in the world, KeepFor.Lent), and
    /// every five days their letter brings real news of the town (its world events, its places, its quests) while their
    /// stay raises the regard of its notables by their Social. At the season's end their hosts give them food for the road
    /// home (and their head a parting gift when they think well of the colony), and they come back by caravan to the home
    /// they left, with the game's peace talks' practice in Social. Nothing happens to them at the town; the roads are the
    /// game's own.
    /// </summary>
    public static class EnvoysAbroad
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ================= Reading =================

        public static List<(DiplomacyHome home, EnvoyAbroad abroad)> All()
        {
            var list = new List<(DiplomacyHome, EnvoyAbroad)>();
            var book = DiplomacyBook.Instance;
            if (book == null) return list;
            foreach (var h in book.Live())
                if (h.abroad != null)
                    list.Add((h, h.abroad));
            return list;
        }

        public static EnvoyAbroad Of(Pawn p, out DiplomacyHome home)
        {
            home = null;
            if (p == null) return null;
            foreach (var (h, a) in All())
                if (a.colonist == p)
                {
                    home = h;
                    return a;
                }
            return null;
        }

        /// <summary>KeepFor.Lent still holds: they are the colony's envoy away.</summary>
        public static bool Holds(Pawn p) => Of(p, out _) != null;

        /// <summary>The colony's envoy at (or on the way to) a town of this people, or null.</summary>
        public static EnvoyAbroad With(Faction f)
        {
            foreach (var (_, a) in All())
                if (a.people == f)
                    return a;
            return null;
        }

        public static Settlement TownOf(EnvoyAbroad a) => a == null ? null : NotableBook.TownOf(a.town);

        /// <summary>The town a home would send its envoy to: their nearest to it.</summary>
        public static Settlement TownFor(Faction f, Map home) => f != null && home != null ? WorldSituationWorker.NearestTown(f, home) : null;

        /// <summary>Who could go: free colonists at the home, grown, able to talk, good with people (Social 6 or more), best first.</summary>
        public static List<Pawn> Candidates(Map home)
        {
            var list = new List<Pawn>();
            if (home == null) return list;
            foreach (var p in home.mapPawns.FreeColonistsSpawned)
            {
                if (p == null || p.Dead || p.Downed || p.InMentalState || p.Drafted || p.IsQuestLodger() || Of(p, out _) != null) continue;
                int social = p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
                bool talk = p.health?.capacities?.CapableOf(PawnCapacityDefOf.Talking) ?? false;
                if (EnvoyMath.CanGo(social, !p.DevelopmentalStage.Juvenile(), talk) && !(p.skills?.GetSkill(SkillDefOf.Social)?.TotallyDisabled ?? true)) list.Add(p);
            }
            return list.OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Social).Level).ToList();
        }

        /// <summary>Days on the road to a town, roughly (a walking caravan of one).</summary>
        private static float RoadDays(Map home, Settlement town) =>
            home == null || town == null ? 2f : 0.5f + Find.WorldGrid.ApproxDistanceInTiles(home.Tile, town.Tile) * 0.4f;

        /// <summary>Food in the stores that keeps on the road (no rot, fit for people, no drug): the most nourishing kind first.</summary>
        private static List<Thing> RoadFood(Map map, Pawn p, bool reach = true)
        {
            return map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.Spawned && t.def.IsNutritionGivingIngestible && t.def.ingestible.HumanEdible && !t.def.IsDrug && t.TryGetComp<CompRottable>() == null
                            && !t.IsForbidden(p) && t.IsInAnyStorage() && !FoodUtility.IsHumanlikeCorpseOrHumanlikeMeatOrIngredient(t) && FoodUtility.WillEat(p, t, p)
                            && (!reach || p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some)))
                .OrderByDescending(t => (int)t.def.ingestible.preferability).ThenBy(t => t.Position.DistanceToSquared(p.Position)).ToList();
        }

        private static readonly Dictionary<int, (float at, bool ok)> foodOk = new Dictionary<int, (float, bool)>();

        /// <summary>Enough food that keeps in the stores for the road to the town (looked at most every five seconds a home and town, for the cards' why not).</summary>
        private static bool FoodFor(Map home, Pawn p, Settlement town)
        {
            int key = unchecked(home.uniqueID * 7919 + town.ID);
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (foodOk.TryGetValue(key, out var was) && now - was.at < 5f && now >= was.at) return was.ok;
            bool ok = Have(RoadFood(home, p, false), p) >= Want(p, RoadDays(home, town));
            foodOk[key] = (now, ok);
            return ok;
        }

        /// <summary>The nutrition the road there takes at a full appetite.</summary>
        private static float Want(Pawn p, float days)
        {
            var need = p?.needs?.food;
            return need == null ? 0f : need.FoodFallPerTickAssumingCategory(HungerCategory.Fed, false) * GenDate.TicksPerDay * days;
        }

        private static float Have(List<Thing> food, Pawn p) => food.Sum(t => FoodUtility.NutritionForEater(p, t) * t.stackCount);

        // ================= Sending =================

        /// <summary>Why the colony can't send an envoy to this people from this home now (any colonist), or null.</summary>
        public static string WhyNot(Faction f, Map home, Settlement at = null)
        {
            if (!Envoys.On) return RansomWords.P("CC_Envoy_Why_Off");
            if (f == null || !Diplomacy.Deals(f)) return DipWords.P("CC_Dip_Why_Gone");
            if (Diplomacy.Hostile(f) || !f.HasGoodwill || f.PlayerGoodwill < 0) return RansomWords.P("CC_Envoy_Why_NotAtPeace", RansomWords.People(f).Named("PEOPLE"));
            var h = DiplomacyBook.Instance?.Existing(home);
            if (h?.abroad != null) return RansomWords.P("CC_Envoy_Why_OneAway", RansomWords.Name(h.abroad.colonist, h.abroad.name).Named("PAWN"));
            var town = at ?? TownFor(f, home);
            if (town == null) return RansomWords.P("CC_Envoy_Why_NoTown", RansomWords.People(f).Named("PEOPLE"));
            if (!Find.WorldReachability.CanReach(home.Tile, town.Tile)) return RansomWords.P("CC_Envoy_Why_NoRoad", town.Name.Named("TOWN"));
            if (home.mapPawns.FreeColonistsSpawnedCount < 2) return RansomWords.P("CC_Envoy_Why_Alone");
            var who = Candidates(home);
            if (who.Count == 0) return RansomWords.P("CC_Envoy_Why_NoOne", EnvoyMath.MinSocial.Named("SOCIAL"));
            if (!FoodFor(home, who[0], town)) return RansomWords.P("CC_Envoy_Why_NoFood", town.Name.Named("TOWN"));
            return null;
        }

        /// <summary>The player picks who goes (a menu of those good with people), then the council's question names what it costs.</summary>
        public static void Choose(Faction f, Map home, Settlement at = null)
        {
            if (WhyNot(f, home, at) != null) return;
            var town = at ?? TownFor(f, home);
            var opts = new List<FloatMenuOption>();
            foreach (var p in Candidates(home))
            {
                var c = p;
                string label = RansomWords.P("CC_Envoy_Pick", c.Named("PAWN"), c.skills.GetSkill(SkillDefOf.Social).Level.Named("SOCIAL"));
                opts.Add(new FloatMenuOption(label, () => Ask(c, f, town, home)));
            }
            Find.WindowStack.Add(new FloatMenu(opts, RansomWords.P("CC_Envoy_Pick_Title", town.Name.Named("TOWN"))));
        }

        /// <summary>The council's question: their work is lost for the season, and the road is the game's own.</summary>
        private static void Ask(Pawn p, Faction f, Settlement town, Map home)
        {
            var form = RansomWords.EnvoyForm(home, f);
            string text = RansomWords.P("CC_Envoy_Ask", p.Named("PAWN"), town.Name.Named("TOWN"), RansomWords.People(f).Named("PEOPLE"), form.word.Named("WORD"),
                CardWords.DistanceShort(town.Tile).Named("DISTANCE"));
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, () => Send(p, f, town, home)));
        }

        /// <summary>Agreed: they take food for the road from the stores, walk to the edge and set out as a caravan of their own.</summary>
        public static bool Send(Pawn p, Faction f, Settlement town, Map home)
        {
            var book = DiplomacyBook.Instance;
            var setOut = RansomDefOf.Rimfolk_EnvoySetOut;
            if (book == null || setOut == null || p == null || town == null || WhyNot(f, home, town) != null) return false;
            if (!RCellFinder.TryFindBestExitSpot(p, out var edge, TraverseMode.ByPawn)) return false;
            var h = book.HomeOf(home);
            h.abroad = new EnvoyAbroad { colonist = p, people = f, town = town.ID, townName = town.Name, stage = EnvoyStage.Coming, agreed = Now, name = p.LabelShort };
            WorldLedger.Keep(p, KeepFor.Lent, false);
            try
            {
                // together: the envoy fetches food for the road and walks out; nobody else is needed for it.
                var food = RoadFood(home, p);
                float want = Want(p, RoadDays(home, town)), got = 0f;
                var jobs = new List<Job>();
                foreach (var t in food)
                {
                    if (got >= want || jobs.Count >= 3) break;
                    float each = FoodUtility.NutritionForEater(p, t);
                    if (each <= 0f) continue;
                    int n = Mathf.Clamp(Mathf.CeilToInt((want - got) / each), 1, t.stackCount);
                    var take = JobMaker.MakeJob(JobDefOf.TakeInventory, t);
                    take.count = n;
                    jobs.Add(take);
                    got += n * each;
                }
                jobs.Add(JobMaker.MakeJob(setOut, edge));
                p.jobs.StartJob(jobs[0], JobCondition.InterruptForced, null, false, true);
                for (int i = 1; i < jobs.Count; i++) p.jobs.jobQueue.EnqueueLast(jobs[i]);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("envoys abroad: setting out", e);
            }
            Diplomacy.Dirty();
            // apart: the council's answer, told as the envoy gets ready on the map.
            Tell.Message(NewsKind.World, Importance.Important, home, RansomWords.P("CC_Envoy_Msg_SettingOut", p.Named("PAWN"), town.Name.Named("TOWN")), new LookTargets(p),
                MessageTypeDefOf.NeutralEvent);
            return true;
        }

        /// <summary>At the map's edge (JobDriver_EnvoySetOut): a caravan of their own, on the road to the town.</summary>
        public static void AtEdge(Pawn p)
        {
            var a = Of(p, out var h);
            var map = h?.Map;
            var town = TownOf(a);
            if (a == null || map == null || town == null || !p.Spawned) return;
            // A season away as they walk off the map: against the settled ways of one set in them, and the curious glad of it (Nature.cs).
            Nature.Felt(p, NatureAct.SeasonAway, a.people, null, "Envoy");
            try
            {
                var c = CaravanExitMapUtility.ExitMapAndCreateCaravan(new List<Pawn> { p }, Faction.OfPlayer, map.Tile, map.Tile, PlanetTile.Invalid, false);
                c?.pather?.StartPath(town.Tile, null, true);
                a.stage = EnvoyStage.Staying;
                Diplomacy.Dirty();
                // apart: the envoy's caravan is out on the world map; the message looks at it.
                Tell.Message(NewsKind.World, Importance.Notable, map, RansomWords.P("CC_Envoy_Msg_SetOut", p.Named("PAWN"), town.Name.Named("TOWN")),
                    c != null ? new LookTargets(c) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("envoys abroad: the caravan", e);
            }
        }

        // ================= The hour =================

        public static void Hourly(int now)
        {
            foreach (var (h, a) in All())
            {
                try
                {
                    Step(h, a, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("envoys abroad: the hour", e);
                }
            }
        }

        private static void Step(DiplomacyHome h, EnvoyAbroad a, int now)
        {
            var p = a.colonist;
            var town = TownOf(a);
            var map = h.Map;
            if (p == null || p.Dead || p.Discarded)
            {
                Clear(h, a);
                return;
            }
            switch (a.stage)
            {
                case EnvoyStage.Coming:
                    // Still at home getting ready: called off when they're no longer on their way out (the player gave them other work).
                    if (p.Spawned && p.CurJobDef != JobDefOf.TakeInventory && p.CurJobDef != RansomDefOf.Rimfolk_EnvoySetOut
                        && !(p.jobs?.jobQueue?.Any(q => q.job.def == RansomDefOf.Rimfolk_EnvoySetOut) ?? false))
                    {
                        Clear(h, a);
                        // apart: the envoy didn't leave after all; told in a message.
                        Tell.Message(NewsKind.World, Importance.Notable, map, RansomWords.P("CC_Envoy_Msg_Stayed", p.Named("PAWN")), new LookTargets(p), MessageTypeDefOf.NeutralEvent);
                    }
                    return;
                case EnvoyStage.Staying:
                {
                    // On the road there: at the town (or beside it, stopped), they stay; turned for home by the player (back on a
                    // home's map), or carried off, the plan is off. On the map of a fight on the road (the game's ambush), they
                    // wait: once their caravan sets off again, it goes on to the town.
                    var c = p.GetCaravan();
                    if (c == null)
                    {
                        if (p.Spawned && Homes.IsHome(p.Map) || People.Kidnapped(p)) Clear(h, a);
                        return;
                    }
                    if (town == null || town.Destroyed || town.Faction != a.people || Diplomacy.Hostile(a.people))
                    {
                        Clear(h, a);
                        return;
                    }
                    bool there = c.Tile == town.Tile || !c.pather.Moving && Find.WorldGrid.ApproxDistanceInTiles(c.Tile, town.Tile) <= 1.01f;
                    if (there) Arrived(h, a, c, town);
                    else if (!c.pather.Moving && !c.pather.Paused) c.pather.StartPath(town.Tile, null, true);
                    return;
                }
                case EnvoyStage.There:
                    if (town == null || town.Destroyed || Diplomacy.Hostile(a.people) || now >= a.until)
                    {
                        Home(h, a, town, map);
                        return;
                    }
                    if (EnvoyMath.LetterDue((now - a.since) / (float)EnvoyMath.DayTicks, a.letters)) Letter(h, a, town, map);
                    return;
                case EnvoyStage.Leaving:
                    // On the road home: once back on the home's map, the season is done.
                    if (p.Spawned && p.Map == map) Back(h, a, map);
                    else if (p.GetCaravan() == null && !p.Spawned) Clear(h, a);
                    return;
            }
        }

        /// <summary>At their town: the envoy stays the season, kept in the world; the caravan, theirs alone, is gone.</summary>
        private static void Arrived(DiplomacyHome h, EnvoyAbroad a, Caravan c, Settlement town)
        {
            var p = a.colonist;
            c.RemovePawn(p);
            if (!c.PawnsListForReading.Any(x => x != null && x.IsColonist)) c.Destroy();
            WorldLedger.Keep(p, KeepFor.Lent);
            a.stage = EnvoyStage.There;
            a.since = Now;
            a.until = Now + Mathf.RoundToInt(EnvoyMath.StayDays * EnvoyMath.DayTicks);
            var map = h.Map;
            var f = a.people;
            var form = RansomWords.EnvoyForm(map, f);
            Diplomacy.Write(DiplomacyMath.Envoy, new List<HistoryActor>
            {
                new HistoryActor(Diplomacy.Colony(map), HistoryRole.Did), new HistoryActor(WorldLedger.PersonOf(p), HistoryRole.About), new HistoryActor(Refs.Of(f), HistoryRole.For)
            }, Refs.Of(town), RansomWords.P("CC_Envoy_Hist_Went", p.Named("PAWN"), town.Name.Named("TOWN"), form.word.Named("WORD")), true);
            Tell.Letter(NewsKind.World, Importance.Notable, map, RansomWords.P("CC_Envoy_There_Label", p.Named("PAWN"), town.Name.Named("TOWN")),
                RansomWords.T("CC_Envoy_There", p.Named("PAWN"), town.Name.Named("TOWN"), RansomWords.People(f).Named("PEOPLE"), form.rite.Named("RITE")), LetterDefOf.PositiveEvent,
                new LookTargets(town), f);
            Diplomacy.Dirty();
        }

        /// <summary>Every five days at the town: their letter with the town's real news, and its notables think the better of the colony by their Social.</summary>
        private static void Letter(DiplomacyHome h, EnvoyAbroad a, Settlement town, Map map)
        {
            var p = a.colonist;
            a.letters++;
            int social = p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            int up = EnvoyMath.AbroadRegard(social);
            var book = NotableBook.Instance;
            var here = book?.notables.Where(n => n != null && n.Alive && n.town == town.ID).ToList() ?? new List<Notable>();
            foreach (var n in here)
            {
                n.personal += up;
                n.RegardOf(p, true).personal += up;
                n.Remember(RansomWords.P("CC_Envoy_Note_Ours", p.Named("PAWN")), up);
            }
            string news = "";
            try
            {
                var head = here.FirstOrDefault(n => n.role == NotableRole.Head) ?? here.FirstOrDefault();
                if (head != null)
                {
                    var facts = Facts.For(FactScene.Letter(head, p, map, true));
                    if (facts.news.Count > 0) news = "\n\n" + string.Join("\n", facts.news.Take(3).Select(i => "  - " + i));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("envoys abroad: the town's news", e);
            }
            if (news.Length == 0) news = "\n\n" + RansomWords.P("CC_Envoy_Letter_Quiet", town.Name.Named("TOWN"));
            Tell.Letter(NewsKind.World, Importance.Flavor, map, RansomWords.P("CC_Envoy_Letter_Label", p.Named("PAWN"), town.Name.Named("TOWN")),
                RansomWords.T("CC_Envoy_Letter", p.Named("PAWN"), town.Name.Named("TOWN"), RansomWords.People(a.people).Named("PEOPLE"), here.Count.Named("COUNT")) + news,
                LetterDefOf.NeutralEvent, new LookTargets(town), a.people);
            Diplomacy.Dirty();
        }

        /// <summary>
        /// The season over (or their town lost to the colony): their hosts give them food for the road, their head a parting
        /// gift when they think well of the colony, and they set out for home as a caravan of their own.
        /// </summary>
        private static void Home(DiplomacyHome h, EnvoyAbroad a, Settlement town, Map map)
        {
            var p = a.colonist;
            var f = a.people;
            PlanetTile from = town != null && !town.Destroyed ? town.Tile : p.Tile;
            string gift = null;
            try
            {
                Hosting.Provision(p, EnvoyMath.RoadDays(RoadDays(map, town)));
                var head = f != null ? FavourCalls.HeadOf(f, map) : null;
                if (town != null && !town.Destroyed && head != null && EnvoyMath.PartingGift(NotableBook.Opinion(head)))
                {
                    var (def, count) = GiftOf(f);
                    if (def != null && count > 0)
                    {
                        foreach (var t in Couriers.Make(def, count)) p.inventory?.innerContainer.TryAdd(t);
                        gift = Couriers.Amount(count, null, def);
                    }
                }
                if (Find.WorldPawns.Contains(p) == false) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                var c = CaravanMaker.MakeCaravan(new List<Pawn> { p }, Faction.OfPlayer, from, false);
                if (map?.Parent != null) c.pather.StartPath(map.Tile, new CaravanArrivalAction_Enter(map.Parent), true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("envoys abroad: the road home", e);
            }
            WorldLedger.Release(p, KeepFor.Lent);
            Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            a.stage = EnvoyStage.Leaving;
            Diplomacy.Dirty();
            string text = RansomWords.T("CC_Envoy_Homeward", p.Named("PAWN"), (town?.Name ?? a.townName ?? "?").Named("TOWN"), RansomWords.People(f).Named("PEOPLE"));
            if (gift != null) text += "\n\n" + RansomWords.P("CC_Envoy_Homeward_Gift", p.Named("PAWN"), gift.Named("GIFT"));
            Tell.Letter(NewsKind.World, Importance.Notable, map, RansomWords.P("CC_Envoy_Homeward_Label", p.Named("PAWN")), text, LetterDefOf.NeutralEvent,
                p.GetCaravan() != null ? new LookTargets(p.GetCaravan()) : LookTargets.Invalid, f);
        }

        /// <summary>
        /// A parting gift worth about 200 silver in what their people make (Things/ThingRoles.cs): their medicine, or a
        /// gift by their ways (a precious metal or stone, a fine drink of theirs), else silver.
        /// </summary>
        private static (ThingDef def, int count) GiftOf(Faction f)
        {
            var def = Rand.Chance(0.5f) ? ThingRoles.From(ThingRole.Medicine, f) : ThingRoles.From(ThingRole.Gift, f);
            if (def == null || def.BaseMarketValue <= 0f) def = ThingDefOf.Silver;
            return (def, RansomMath.GoodsCount(EnvoyMath.GiftWorth, def.BaseMarketValue));
        }

        /// <summary>Home again: the game's peace talks' practice in Social, the history, and the season done.</summary>
        private static void Back(DiplomacyHome h, EnvoyAbroad a, Map map)
        {
            var p = a.colonist;
            try
            {
                p.skills?.Learn(SkillDefOf.Social, EnvoyMath.SocialXp, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("envoys abroad: practice", e);
            }
            var form = RansomWords.EnvoyForm(map, a.people);
            Diplomacy.Write(DiplomacyMath.Envoy, new List<HistoryActor>
            {
                new HistoryActor(WorldLedger.PersonOf(p), HistoryRole.Did), new HistoryActor(Refs.Of(a.people), HistoryRole.With), new HistoryActor(Diplomacy.Colony(map), HistoryRole.For)
            }, Diplomacy.Colony(map), RansomWords.P("CC_Envoy_Hist_Back", p.Named("PAWN"), (a.townName ?? "?").Named("TOWN"), form.word.Named("WORD")));
            // apart: the envoy is home on the map; the message looks at them.
            Tell.Message(NewsKind.World, Importance.Notable, map, RansomWords.P("CC_Envoy_Msg_Back", p.Named("PAWN"), (a.townName ?? "?").Named("TOWN")), new LookTargets(p),
                MessageTypeDefOf.PositiveEvent);
            Clear(h, a);
        }

        /// <summary>The colony calls its envoy home before the season is out (the card's act).</summary>
        public static void Recall(EnvoyAbroad a)
        {
            Of(a?.colonist, out var h);
            if (a == null || h == null || a.stage != EnvoyStage.There) return;
            a.until = Now;
            Home(h, a, TownOf(a), h.Map);
        }

        /// <summary>The home they went from is no longer the colony's: at the town, their hosts see them off as a caravan of the colony's (to be sent where the player wishes).</summary>
        internal static void HomeLost(DiplomacyHome h)
        {
            var a = h?.abroad;
            if (a == null) return;
            if (a.stage == EnvoyStage.There && a.colonist != null && !a.colonist.Dead) Home(h, a, TownOf(a), null);
            Clear(h, a);
        }

        private static void Clear(DiplomacyHome h, EnvoyAbroad a)
        {
            if (h == null) return;
            if (h.abroad == a) h.abroad = null;
            if (a?.colonist != null) WorldLedger.Release(a.colonist, KeepFor.Lent);
            Diplomacy.Dirty();
        }
    }

    /// <summary>The colony's envoy walks to the map's edge (target A) and sets out as a caravan of their own for the town.</summary>
    public class JobDriver_EnvoySetOut : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => RansomWords.P("CC_Envoy_Report_SetOut");

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var go = ToilMaker.MakeToil("EnvoySetOut");
            go.initAction = () => EnvoysAbroad.AtEdge(pawn);
            go.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return go;
        }
    }
}
