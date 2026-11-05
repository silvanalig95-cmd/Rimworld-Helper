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
    /// The colony's own away (docs/design/stage2-diplomacy.md, round b, item 7; release 2b.3; the player's decisions 9 and
    /// 10): its youth living with another people for a year, or its hostage at their town for a truce's or a pact's term.
    /// Either goes as the colony's envoy does (EnvoysAbroad.cs): food that keeps for the road from the stores, a walk to the
    /// map's edge, a caravan of their own on the game's roads; at the town a lent colonist, kept in the world
    /// (KeepFor.Lent). The youth writes home every ten days and comes back with what their people are good at, by their ways
    /// (a tribe's beasts and plants, the middle ages' crafts and the sword, the machine age's building and medicine, the
    /// spacers' learning and medicine), and a friend among their notables; with the Empire the youth serves as a page in a
    /// titled household, for regard only. The hostage comes home at the term's end, or, the colony having broken its word,
    /// is held by their people in the game's own captivity (Hostages.Held). Nothing happens to either at the town; the roads
    /// are the game's own.
    /// </summary>
    public static class WardsAbroad
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>KeepFor.Lent still holds: the colony's ward or hostage, away at their town or on the road.</summary>
        public static bool Holds(Pawn p)
        {
            var w = Wards.Of(p);
            return w != null && !w.Theirs && (w.stage == WardStage.Away || w.stage == WardStage.Going);
        }

        public static Settlement TownOf(WardStay w) => w == null || w.town < 0 ? null : NotableBook.TownOf(w.town);

        /// <summary>The town the colony's own would go to: their nearest to the home.</summary>
        public static Settlement TownFor(Faction f, Map home) => f != null && home != null ? WorldSituationWorker.NearestTown(f, home) : null;

        /// <summary>At their town (not on the road there or home).</summary>
        public static bool There(WardStay w) => w != null && !w.Theirs && w.stage == WardStage.Away && w.since >= 0;

        /// <summary>Youths of the home who could go: 10 to 19 where the game has children, else 14 to 19; free, not a lodger, not away already.</summary>
        public static List<Pawn> Youths(Map home)
        {
            var list = new List<Pawn>();
            if (home == null) return list;
            foreach (var p in home.mapPawns.FreeColonistsSpawned)
            {
                if (p == null || p.Dead || p.IsQuestLodger() || Wards.Of(p) != null || EnvoysAbroad.Of(p, out _) != null) continue;
                if (!WardMath.YouthMayGo(p.ageTracker.AgeBiologicalYearsFloat, Wards.ChildrenHere, p.DevelopmentalStage.Baby())) continue;
                if (!(p.health?.capacities?.CapableOf(PawnCapacityDefOf.Moving) ?? false)) continue;
                list.Add(p);
            }
            return list.OrderBy(p => p.ageTracker.AgeBiologicalYearsFloat).ToList();
        }

        /// <summary>Why the colony can't offer a youth of its own to this people from this home now (any youth), or null.</summary>
        public static string SendWhyNot(Faction f, Map home)
        {
            if (!Wards.On) return WardWords.P("CC_Ward_Why_Off");
            if (f == null || !Diplomacy.Deals(f)) return DipWords.P("CC_Dip_Why_Gone");
            if (Diplomacy.Hostile(f) || !f.HasGoodwill || f.PlayerGoodwill < 0) return WardWords.P("CC_Ward_Why_NotAtPeace", WardWords.People(f).Named("PEOPLE"));
            if (home == null) return DipWords.P("CC_Dip_Why_Gone");
            var h = DiplomacyBook.Instance?.Existing(home);
            if (h != null && h.wards.Any(w => w != null && w.kind == WardKind.Ours)) return WardWords.P("CC_Ward_Why_OneAway");
            if (h != null && h.wards.Count >= WardMath.MaxPerHome) return WardWords.P("CC_Ward_Why_Full");
            var town = TownFor(f, home);
            if (town == null) return RansomWords.P("CC_Envoy_Why_NoTown", WardWords.People(f).Named("PEOPLE"));
            if (!Find.WorldReachability.CanReach(home.Tile, town.Tile)) return RansomWords.P("CC_Envoy_Why_NoRoad", town.Name.Named("TOWN"));
            if (Youths(home).Count == 0) return WardWords.P(Wards.ChildrenHere ? "CC_Ward_Why_NoYouthChild" : "CC_Ward_Why_NoYouth");
            return null;
        }

        /// <summary>Why this youth can't go now, or null.</summary>
        public static string YouthWhyNot(Pawn p, Map home)
        {
            if (p == null || p.Dead || !p.Spawned || p.Map != home) return WardWords.P("CC_Ward_Why_YouthAway");
            return Youths(home).Contains(p) ? null : WardWords.P("CC_Ward_Why_YouthBusy", p.Named("PAWN"));
        }

        /// <summary>
        /// The colony offered a youth of its own by letter: they take them in by their head's regard (the Empire into a titled
        /// household as a page). Agreed, the youth sets out once the answer is back. Returns their words.
        /// </summary>
        public static string HearSend(Faction f, Map home, int youth, int regard, float days, out bool yes)
        {
            yes = false;
            var p = youth > 0 ? Refs.Pawn(Ref.Person(youth)) : null;
            if (SendWhyNot(f, home) != null || YouthWhyNot(p, home) != null) return WardWords.T("CC_Ward_Ans_Moot", WardWords.People(f).Named("PEOPLE"));
            bool page = Ages.IsEmpire(f);
            var form = WardWords.Form(home, f);
            if (!WardMath.SendsWhenAsked(Diplomacy.Hostile(f), regard, Rand.Value))
                return WardWords.T(page ? "CC_Ward_Ans_SendNoEmpire" : "CC_Ward_Ans_SendNo", WardWords.People(f).Named("PEOPLE"), p.Named("PAWN"), form.word.Named("WORD"));
            var w = Agree(home, f, p, true, days);
            if (w == null) return WardWords.T("CC_Ward_Ans_Moot", WardWords.People(f).Named("PEOPLE"));
            yes = true;
            return WardWords.T(page ? "CC_Ward_Ans_SendYesEmpire" : "CC_Ward_Ans_SendYes", WardWords.People(f).Named("PEOPLE"), p.Named("PAWN"), form.rite.Named("RITE"),
                WardWords.Skill(w.skill).Named("SKILL"), (w.townName ?? "?").Named("TOWN"));
        }

        /// <summary>The colony's youth agreed: due to set out after the answer's road back; the Ward tie from the colony to their people.</summary>
        public static WardStay Agree(Map home, Faction f, Pawn p, bool asked, float days)
        {
            var book = DiplomacyBook.Instance;
            var town = TownFor(f, home);
            if (book == null || home == null || f == null || p == null || town == null) return null;
            var h = book.HomeOf(home);
            if (!WardMath.Fits(h.wards.Select(x => (x.kind, x.guarantees)).ToList(), WardKind.Ours, -1)) return null;
            bool page = Ages.IsEmpire(f);
            var w = new WardStay
            {
                id = book.NextId(), kind = WardKind.Ours, stage = WardStage.Coming, pawn = p, people = f, town = town.ID, townName = town.Name, page = page, asked = asked,
                skill = page ? null : WardMath.TheirSkill(Ages.Of(f), Rand.Value), agreed = Now, due = Now + Mathf.RoundToInt(Mathf.Max(0f, days) * WardMath.DayTicks), name = p.LabelShort
            };
            h.wards.Add(w);
            var t = WorldLedger.AddTie(Tie.Kind.Ward, Diplomacy.Colony(home), Refs.Of(f), new TieTerms { about = Refs.Of(p), key = w.skill, count = w.id }, Tie.Never,
                WardMath.WardOrigin);
            if (t != null) w.tie = t.id;
            Diplomacy.Dirty();
            return w;
        }

        // ================= The hour =================

        /// <summary>The colony's own a step on (Wards' hour): getting ready, on the road, at the town, home again, or held.</summary>
        public static void Step(DiplomacyHome h, WardStay w, int now)
        {
            var p = w.pawn;
            var map = h.Map;
            if (p == null || p.Discarded)
            {
                Wards.Clear(h, w, false);
                return;
            }
            if (p.Dead)
            {
                Lost(h, w, map);
                return;
            }
            switch (w.stage)
            {
                case WardStage.Coming:
                    Coming(h, w, map, now);
                    return;
                case WardStage.Away:
                    Away(h, w, map, now);
                    return;
                case WardStage.Going:
                    // On the road home: once back on the home's map, done; carried off on the road, the game's captivity has them.
                    if (p.Spawned && p.Map == map) Back(h, w, map);
                    else if (People.Kidnapped(p)) Wards.Clear(h, w, false);
                    else if (p.GetCaravan() == null && !p.Spawned && now - w.due > 30 * WardMath.DayTicks) Wards.Clear(h, w, false);
                    return;
                case WardStage.Held:
                    if (!People.Kidnapped(p)) Hostages.Free(h, w);
                    return;
                default:
                    Wards.Clear(h, w, false);
                    return;
            }
        }

        /// <summary>Agreed and due: the youth (or hostage) gets ready and sets out; not free within three days, it is called off.</summary>
        private static void Coming(DiplomacyHome h, WardStay w, Map map, int now)
        {
            var p = w.pawn;
            var f = w.people;
            if (w.ready)
            {
                // Getting ready: called off when they're no longer on their way out (the player gave them other work).
                if (p.Spawned && p.CurJobDef != JobDefOf.TakeInventory && p.CurJobDef != WardDefOf.Rimfolk_WardSetOut
                    && !(p.jobs?.jobQueue?.Any(q => q.job.def == WardDefOf.Rimfolk_WardSetOut) ?? false))
                {
                    w.ready = false;
                    w.due = now;
                    if (!w.Hostage) CallOff(h, w, map, "CC_Ward_Msg_Stayed");
                }
                return;
            }
            if (now < w.due) return;
            if (f == null || f.defeated || !w.Hostage && Diplomacy.Hostile(f) || TownOf(w) == null)
            {
                CallOff(h, w, map, "CC_Ward_Msg_NotGoing");
                return;
            }
            if (now - w.due > 3 * WardMath.DayTicks)
            {
                CallOff(h, w, map, "CC_Ward_Msg_NotGoing");
                return;
            }
            if (!p.Spawned || p.Map != map || p.Downed || p.Drafted || p.InMentalState || MapFacts.ThreatOn(map)) return;
            SetOut(h, w, map);
        }

        private static void CallOff(DiplomacyHome h, WardStay w, Map map, string key)
        {
            var t = WorldLedger.Get(w.tie);
            if (t != null && t.Live) WorldLedger.Set(t, Tie.State.Ended);
            var p = w.pawn;
            Wards.Clear(h, w, false);
            // apart: the plan is off; told in a message.
            Tell.Message(NewsKind.World, Importance.Notable, map, WardWords.P(key, WardWords.Name(w).Named("PAWN"), WardWords.People(w.people).Named("PEOPLE")),
                p != null && p.Spawned ? new LookTargets(p) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>Food in the stores that keeps on the road (no rot, fit for people, no drug): the most nourishing kind first.</summary>
        private static List<Thing> RoadFood(Map map, Pawn p) =>
            map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.Spawned && t.def.IsNutritionGivingIngestible && t.def.ingestible.HumanEdible && !t.def.IsDrug && t.TryGetComp<CompRottable>() == null
                            && !t.IsForbidden(p) && t.IsInAnyStorage() && !FoodUtility.IsHumanlikeCorpseOrHumanlikeMeatOrIngredient(t) && FoodUtility.WillEat(p, t, p)
                            && p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some))
                .OrderByDescending(t => (int)t.def.ingestible.preferability).ThenBy(t => t.Position.DistanceToSquared(p.Position)).ToList();

        private static float RoadDays(Map home, Settlement town) =>
            home == null || town == null ? 2f : 0.5f + Find.WorldGrid.ApproxDistanceInTiles(home.Tile, town.Tile) * 0.4f;

        /// <summary>They take food that keeps for the road from the stores (what there is), walk to the edge and set out as a caravan of their own.</summary>
        private static void SetOut(DiplomacyHome h, WardStay w, Map map)
        {
            var p = w.pawn;
            var town = TownOf(w);
            var setOut = WardDefOf.Rimfolk_WardSetOut;
            if (setOut == null || town == null || !RCellFinder.TryFindBestExitSpot(p, out var edge, TraverseMode.ByPawn)) return;
            try
            {
                var need = p.needs?.food;
                float want = need == null ? 0f : need.FoodFallPerTickAssumingCategory(HungerCategory.Fed, false) * GenDate.TicksPerDay * RoadDays(map, town), got = 0f;
                var jobs = new List<Job>();
                foreach (var t in RoadFood(map, p))
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
                // together: they fetch food for the road and walk out; nobody else is needed for it.
                p.jobs.StartJob(jobs[0], JobCondition.InterruptForced, null, false, true);
                for (int i = 1; i < jobs.Count; i++) p.jobs.jobQueue.EnqueueLast(jobs[i]);
                w.ready = true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wards abroad: setting out", e);
                return;
            }
            Diplomacy.Dirty();
            // apart: told as they get ready on the map.
            Tell.Message(NewsKind.World, Importance.Notable, map,
                WardWords.P(w.Hostage ? "CC_Hostage_Msg_SettingOut" : w.page ? "CC_Ward_Msg_SettingOutPage" : "CC_Ward_Msg_SettingOut", p.Named("PAWN"), town.Name.Named("TOWN"),
                    WardWords.People(w.people).Named("PEOPLE")), new LookTargets(p), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>At the map's edge (JobDriver_WardSetOut): a caravan of their own, on the road to the town.</summary>
        public static void AtEdge(Pawn p)
        {
            var w = Wards.Of(p, out var h);
            var map = h?.Map;
            var town = TownOf(w);
            if (w == null || w.Theirs || map == null || town == null || !p.Spawned) return;
            // As they walk off the map: a hostage given (the timid, the set in their ways; their close kin, if warm), a youth
            // sent away (their parents, if warm) take it to heart (Nature.cs).
            if (w.Hostage) Nature.HostageGoes(p, w.people);
            else Nature.WardGoes(p, w.people);
            try
            {
                var c = CaravanExitMapUtility.ExitMapAndCreateCaravan(new List<Pawn> { p }, Faction.OfPlayer, map.Tile, map.Tile, PlanetTile.Invalid, false);
                c?.pather?.StartPath(town.Tile, null, true);
                w.stage = WardStage.Away;
                w.ready = false;
                w.since = -1;
                WorldLedger.Keep(p, KeepFor.Lent, false);
                Diplomacy.Dirty();
                // apart: their caravan is out on the world map; the message looks at it.
                Tell.Message(NewsKind.World, Importance.Notable, map, WardWords.P("CC_Ward_Msg_SetOut", p.Named("PAWN"), town.Name.Named("TOWN")),
                    c != null ? new LookTargets(c) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wards abroad: the caravan", e);
            }
        }

        /// <summary>
        /// Away: on the road there (the caravan reaching the town; turned home by the player or carried off, the plan is off;
        /// on the map of a fight on the road, it waits), or at the town (letters, the year's end, a hostage's term).
        /// </summary>
        private static void Away(DiplomacyHome h, WardStay w, Map map, int now)
        {
            var p = w.pawn;
            var town = TownOf(w);
            var f = w.people;
            if (w.since < 0)
            {
                var c = p.GetCaravan();
                if (c == null)
                {
                    if (p.Spawned && Homes.IsHome(p.Map)) CallOff(h, w, map, "CC_Ward_Msg_Stayed");
                    else if (People.Kidnapped(p)) Wards.Clear(h, w, false);
                    return;
                }
                if (town == null || town.Destroyed || town.Faction != f || !w.Hostage && Diplomacy.Hostile(f))
                {
                    CallOff(h, w, map, "CC_Ward_Msg_NotGoing");
                    return;
                }
                bool there = c.Tile == town.Tile || !c.pather.Moving && Find.WorldGrid.ApproxDistanceInTiles(c.Tile, town.Tile) <= 1.01f;
                if (there) Arrived(h, w, c, town);
                else if (!c.pather.Moving && !c.pather.Paused) c.pather.StartPath(town.Tile, null, true);
                return;
            }
            if (w.Hostage)
            {
                switch (Hostages.EndOf(w))
                {
                    case WardMath.HostageEnd.Holds:
                        if (town == null || town.Destroyed) Home(h, w, town, map);
                        return;
                    case WardMath.HostageEnd.WeBroke:
                        Hostages.Held(h, w);
                        return;
                    default:
                        Home(h, w, town, map);
                        return;
                }
            }
            if (town == null || town.Destroyed || Diplomacy.Hostile(f) || now >= w.until)
            {
                Home(h, w, town, map);
                return;
            }
            if (WardMath.LetterDue((now - w.since) / (float)WardMath.DayTicks, w.letters)) Letter(h, w, town, map);
        }

        /// <summary>At their town: they stay (the youth a year, the hostage the term), kept in the world; the caravan, theirs alone, is gone.</summary>
        private static void Arrived(DiplomacyHome h, WardStay w, Caravan c, Settlement town)
        {
            var p = w.pawn;
            c.RemovePawn(p);
            if (!c.PawnsListForReading.Any(x => x != null && x.IsColonist)) c.Destroy();
            WorldLedger.Keep(p, KeepFor.Lent);
            w.since = Now;
            // A hostage keeps the term noted at the giving (a pact's year: Hostages.EndOf); a ward stays a year from now.
            w.until = w.Hostage ? w.until : Now + Mathf.RoundToInt(WardMath.YearDays * WardMath.DayTicks);
            var map = h.Map;
            var f = w.people;
            Diplomacy.Dirty();
            if (w.Hostage)
            {
                var hf = WardWords.HostageForm(map, f);
                Tell.Letter(NewsKind.World, Importance.Notable, map, WardWords.P("CC_Hostage_There_Label", p.Named("HOSTAGE"), town.Name.Named("TOWN")),
                    WardWords.T("CC_Hostage_There", p.Named("HOSTAGE"), town.Name.Named("TOWN"), WardWords.People(f).Named("PEOPLE"), hf.rite.Named("RITE")), LetterDefOf.NeutralEvent,
                    new LookTargets(town), f);
                return;
            }
            var form = WardWords.Form(map, f);
            Diplomacy.Write(DiplomacyMath.Ward, new List<HistoryActor>
            {
                new HistoryActor(Diplomacy.Colony(map), HistoryRole.Did), new HistoryActor(Refs.Of(p), HistoryRole.About), new HistoryActor(Refs.Of(f), HistoryRole.For)
            }, Refs.Of(town), WardWords.P(w.page ? "CC_Ward_Hist_WentPage" : "CC_Ward_Hist_Went", p.Named("PAWN"), town.Name.Named("TOWN"), form.word.Named("WORD")), true);
            var head = Head(town);
            Tell.Letter(NewsKind.World, Importance.Notable, map, WardWords.P("CC_Ward_There_Label", p.Named("PAWN"), town.Name.Named("TOWN")),
                w.page
                    ? WardWords.T("CC_Ward_ThereEmpire", p.Named("PAWN"), town.Name.Named("TOWN"), (head != null ? head.Name : WardWords.People(f)).Named("HEAD"))
                    : WardWords.T("CC_Ward_There", p.Named("PAWN"), town.Name.Named("TOWN"), WardWords.People(f).Named("PEOPLE"), form.rite.Named("RITE"), WardWords.Skill(w.skill).Named("SKILL")),
                LetterDefOf.PositiveEvent, new LookTargets(town), f);
        }

        private static Notable Head(Settlement town) =>
            town == null ? null : NotableBook.Instance?.notables.Where(n => n != null && n.Alive && n.town == town.ID).OrderBy(n => n.role == NotableRole.Head ? 0 : 1).FirstOrDefault();

        /// <summary>Every ten days at the town: the youth's letter home (what they are learning, or a page's duties), and its notables think the better of the colony.</summary>
        private static void Letter(DiplomacyHome h, WardStay w, Settlement town, Map map)
        {
            var p = w.pawn;
            w.letters++;
            int up = WardMath.AbroadRegard(w.page);
            var here = NotableBook.Instance?.notables.Where(n => n != null && n.Alive && n.town == town.ID).ToList() ?? new List<Notable>();
            foreach (var n in here)
            {
                n.personal += up;
                n.RegardOf(p, true).personal += up;
                n.Remember(WardWords.P("CC_Ward_Note_Ours", p.Named("PAWN")), up);
            }
            var head = Head(town);
            string key = w.page ? "CC_Ward_Letter_Page" : "CC_Ward_Letter_" + Ages.Of(w.people);
            Tell.Letter(NewsKind.World, Importance.Flavor, map, WardWords.P("CC_Ward_Letter_Label", p.Named("PAWN"), town.Name.Named("TOWN")),
                WardWords.T(key, p.Named("PAWN"), town.Name.Named("TOWN"), WardWords.People(w.people).Named("PEOPLE"), WardWords.Skill(w.skill).Named("SKILL"),
                    (head != null ? head.Name : WardWords.People(w.people)).Named("HEAD")),
                LetterDefOf.NeutralEvent, new LookTargets(town), w.people);
            Diplomacy.Dirty();
        }

        /// <summary>The year (or the term) over: their hosts give them food for the road and they set out for home as a caravan of their own.</summary>
        private static void Home(DiplomacyHome h, WardStay w, Settlement town, Map map)
        {
            var p = w.pawn;
            var f = w.people;
            PlanetTile from = town != null && !town.Destroyed ? town.Tile : p.Tile;
            try
            {
                Hosting.Provision(p, RoadDays(map, town));
                if (!Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                var c = CaravanMaker.MakeCaravan(new List<Pawn> { p }, Faction.OfPlayer, from, false);
                if (map?.Parent != null) c.pather.StartPath(map.Tile, new CaravanArrivalAction_Enter(map.Parent), true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wards abroad: the road home", e);
            }
            WorldLedger.Release(p, KeepFor.Lent);
            Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            w.stage = WardStage.Going;
            w.due = Now;
            Diplomacy.Dirty();
            Tell.Letter(NewsKind.World, Importance.Notable, map, WardWords.P("CC_Ward_Homeward_Label", p.Named("PAWN")),
                WardWords.T(w.Hostage ? "CC_Hostage_Homeward" : "CC_Ward_Homeward", p.Named("PAWN"), (town?.Name ?? w.townName ?? "?").Named("TOWN"),
                    WardWords.People(f).Named("PEOPLE")), LetterDefOf.NeutralEvent, p.GetCaravan() != null ? new LookTargets(p.GetCaravan()) : LookTargets.Invalid, f);
        }

        /// <summary>
        /// Home again. The youth brings what they learned (the practice of the year in their people's skill; a page, none) and a
        /// friend among their notables, and their people remember teaching them (the deed "taught"); the hostage's term is kept.
        /// </summary>
        private static void Back(DiplomacyHome h, WardStay w, Map map)
        {
            var p = w.pawn;
            var f = w.people;
            float days = w.since >= 0 ? Mathf.Min(WardMath.YearDays, (w.due - w.since) / (float)WardMath.DayTicks) : 0f;
            var t = WorldLedger.Get(w.tie);
            if (w.Hostage)
            {
                if (t != null && t.Live) WorldLedger.Set(t, Tie.State.Kept);
                // The hostage served the term for the colony (as its youth and its envoy home): their house's renown reads it (Diplomacy/Renown.cs).
                Diplomacy.Write(DiplomacyMath.Hostage, new List<HistoryActor>
                {
                    new HistoryActor(Refs.Of(p), HistoryRole.Did), new HistoryActor(Refs.Of(f), HistoryRole.With), new HistoryActor(Diplomacy.Colony(map), HistoryRole.For)
                }, Diplomacy.Colony(map), WardWords.P("CC_Hostage_Hist_Back", p.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE")));
                // apart: the hostage is home on the map; the message looks at them.
                Tell.Message(NewsKind.World, Importance.Notable, map, WardWords.P("CC_Hostage_Msg_Back", p.Named("HOSTAGE"), (w.townName ?? "?").Named("TOWN")), new LookTargets(p),
                    MessageTypeDefOf.PositiveEvent);
                Wards.Clear(h, w, false);
                return;
            }
            var skill = Wards.SkillOf(w);
            int before = skill != null ? p.skills?.GetSkill(skill)?.Level ?? 0 : 0;
            try
            {
                float xp = WardMath.AbroadXp(days, w.page);
                if (skill != null && xp > 0f && !(p.skills?.GetSkill(skill)?.TotallyDisabled ?? true)) p.skills.Learn(skill, xp, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wards abroad: what they learned", e);
            }
            int after = skill != null ? p.skills?.GetSkill(skill)?.Level ?? 0 : 0;
            Notable friend = null;
            var town = TownOf(w);
            if (town != null && days >= WardMath.LetterDays)
            {
                friend = NotableBook.Instance?.notables.Where(n => n != null && n.Alive && n.town == town.ID)
                    .OrderByDescending(n => n.role == NotableRole.Heir ? 1 : 0).ThenByDescending(n => n.RegardOf(p, false)?.personal ?? 0f).FirstOrDefault();
                if (friend != null)
                {
                    friend.RegardOf(p, true).personal += WardMath.FriendRegard;
                    friend.Remember(WardWords.P("CC_Ward_Note_Friend", p.Named("PAWN")), 4f);
                }
            }
            if (f != null && !f.defeated && !w.page && skill != null && days >= WardMath.LetterDays) FactionMemory.Note(f, "taught", skill.label, WardMath.TaughtWeight);
            if (t != null && t.Live) WorldLedger.Set(t, days >= WardMath.YearDays - 1f ? Tie.State.Kept : Tie.State.Ended);
            var form = WardWords.Form(map, f);
            Diplomacy.Write(DiplomacyMath.Ward, new List<HistoryActor>
            {
                new HistoryActor(Refs.Of(p), HistoryRole.Did), new HistoryActor(Refs.Of(f), HistoryRole.With), new HistoryActor(Diplomacy.Colony(map), HistoryRole.For)
            }, Diplomacy.Colony(map), WardWords.P(w.page ? "CC_Ward_Hist_BackPage" : "CC_Ward_Hist_Back", p.Named("PAWN"), (w.townName ?? "?").Named("TOWN"), form.word.Named("WORD")),
                true);
            var parts = new List<string> { WardWords.T(w.page ? "CC_Ward_Back_Page" : "CC_Ward_Back", p.Named("PAWN"), (w.townName ?? "?").Named("TOWN"), WardWords.People(f).Named("PEOPLE")) };
            if (skill != null && after > before) parts.Add(WardWords.P("CC_Ward_Back_Learned", p.Named("PAWN"), skill.label.Named("SKILL"), before.Named("BEFORE"), after.Named("AFTER")));
            if (friend != null) parts.Add(WardWords.P("CC_Ward_Back_Friend", p.Named("PAWN"), NotableBook.Called(friend).Named("FRIEND")));
            Tell.Letter(NewsKind.World, Importance.Notable, map, WardWords.P("CC_Ward_Back_Label", p.Named("PAWN")), string.Join("\n\n", parts), LetterDefOf.PositiveEvent,
                new LookTargets(p), f);
            Wards.Clear(h, w, false);
        }

        /// <summary>Died away from home (on the road, or held): the record let go, told.</summary>
        private static void Lost(DiplomacyHome h, WardStay w, Map map)
        {
            var t = WorldLedger.Get(w.tie);
            if (t != null && t.Live) WorldLedger.Set(t, Tie.State.Ended);
            Tell.Letter(NewsKind.World, Importance.Important, map, WardWords.P("CC_Ward_DiedAway_Label", WardWords.Name(w).Named("PAWN")),
                WardWords.T("CC_Ward_DiedAway", WardWords.Name(w).Named("PAWN"), WardWords.People(w.people).Named("PEOPLE")), LetterDefOf.NegativeEvent, LookTargets.Invalid, w.people);
            Wards.Clear(h, w, false);
        }

        // ================= Acts =================

        /// <summary>The colony calls its youth home before the year is out (the card's act).</summary>
        public static void Recall(WardStay w)
        {
            Wards.Of(w?.pawn, out var h);
            if (w == null || h == null || w.Theirs || w.Hostage || !There(w)) return;
            w.until = Now;
            Home(h, w, TownOf(w), h.Map);
        }

        public static string RecallWhyNot(WardStay w) => w != null && !w.Hostage && There(w) ? null : WardWords.P("CC_Ward_Why_NotThere");

        /// <summary>The home they went from is no longer the colony's: at the town, their hosts see them off as a caravan of the colony's.</summary>
        internal static void HomeLost(DiplomacyHome h, WardStay w)
        {
            if (w == null) return;
            if (There(w) && w.pawn != null && !w.pawn.Dead) Home(h, w, TownOf(w), null);
            Wards.Clear(h, w, false);
        }

        // ================= Words =================

        /// <summary>The colony's own away, in a sentence for Here and away and the cards.</summary>
        public static string Line(WardStay w, Map home)
        {
            var town = TownOf(w);
            string townName = town != null ? ThingRefs.Link(town.Name, ThingRefs.Of(town)) : w.townName ?? "?";
            string name = w.pawn != null ? ThingRefs.Link(w.pawn.LabelShort, ThingRefs.Of(w.pawn)) : w.name ?? "?";
            string people = ThingRefs.Link(WardWords.People(w.people), ThingRefs.Of(w.people));
            string what = WardWords.What(w, home);
            string key;
            switch (w.stage)
            {
                case WardStage.Coming: key = "CC_Ward_Row_Ours_Coming"; break;
                case WardStage.Away: key = w.since < 0 ? "CC_Ward_Row_Ours_Road" : "CC_Ward_Row_Ours_There"; break;
                case WardStage.Held: key = "CC_Hostage_Row_Held"; break;
                default: key = "CC_Ward_Row_Ours_Going"; break;
            }
            return WardWords.P(key, name.Named("PAWN"), townName.Named("TOWN"), people.Named("PEOPLE"), what.Named("WHAT"));
        }
    }
}
