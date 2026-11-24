using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    [DefOf]
    public static class GodRitesDefOf
    {
        public static ThoughtDef Rimfolk_Rite_Kept;
        public static ThoughtDef Rimfolk_Worship_OathKept;
        public static ThoughtDef Rimfolk_Worship_OathBroken;
        public static ThoughtDef Rimfolk_Worship_OathBrokenSeen;
        public static ThoughtDef Rimfolk_Worship_PlaceGiven;
        public static ThoughtDef Rimfolk_Worship_PlaceRefused;
        public static ThoughtDef Rimfolk_Worship_PrayedAtFinePlace;
        public static ThoughtDef Rimfolk_Worship_FeastQuiet;
        public static HediffDef Rimfolk_Rite_Bless;

        static GodRitesDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(GodRitesDefOf));
    }

    /// <summary>
    /// The rites at a god's place and what they bring (the player, 24 November 2026: "the different interactions and rituals
    /// and prayers and stuff should not just be for flavor only. They should then always have some game impact ... depending
    /// on the god and how it turns out, and the pawns"; docs/gods-altars-proposals.md A4, B1 to B4). Every rite here is
    /// held with the believers standing at the place, and how it goes (GodPlacesMath.RiteScore) depends on who came, who led
    /// it, the place, its room, an offering and a keepsake lying there, and the day. What it brings, by how it went:
    /// <list type="bullet">
    /// <item>a mood for those who came (a poor rite a bad one);</item>
    /// <item>their belief in the god grows, the leader's most;</item>
    /// <item>those who hold the god come away with a short blessing of the god's own kind (Rimfolk_Rite_Bless_&lt;God&gt;:
    /// plants for Hollin, the forge and the rock for Smoulder, trade for Ducat ...), stronger the better it went;</item>
    /// <item>the god's regard (never shown), and now and then the god's own answer through the gods' touch (GodTouch.Rite),
    /// with that god's own effects and waits.</item>
    /// </list>
    /// The rites: the dedication (Shrines), the week's service at a temple (<see cref="Services"/>), the festival's rite
    /// (Festivals.RiteDone), and the game's own rites held at a god's place (<see cref="GameRite"/>). Beside them: oaths sworn
    /// at the place (<see cref="SwearAtPlace"/>) and pilgrims at a temple (<see cref="PilgrimsDaily"/>).
    /// </summary>
    public static class GodRites
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The colony's people standing at a place for its rite: within nine cells and in sight of it, up and awake.</summary>
        public static List<Pawn> Attending(Thing place, Pawn leader)
        {
            var map = place?.Map;
            var list = new List<Pawn>();
            if (map == null) return list;
            if (leader != null && leader.Spawned && leader.Map == map) list.Add(leader);
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (list.Contains(p) || !Worship.Ours(p) || p.Downed || !p.Awake() || p.Drafted) continue;
                if (!p.Position.InHorDistOf(place.Position, 9f) || !GenSight.LineOfSight(p.Position, place.Position, map, true)) continue;
                list.Add(p);
            }
            return list;
        }

        /// <summary>The short blessing of the god's own kind a rite gives (its own where it has one, else the plain one).</summary>
        public static HediffDef BlessDef(God g) =>
            (g == null ? null : DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_Rite_Bless_" + g.Key)) ?? GodRitesDefOf.Rimfolk_Rite_Bless;

        private static void Bless(Pawn p, HediffDef def, int level)
        {
            if (p?.health?.hediffSet == null || def == null || level <= 0) return;
            var h = p.health.hediffSet.GetFirstHediffOfDef(def);
            if (h == null)
            {
                h = HediffMaker.MakeHediff(def, p);
                h.Severity = level;
                p.health.AddHediff(h);
            }
            else
            {
                h.Severity = Mathf.Max(h.Severity, level);
                var d = h.TryGetComp<HediffComp_Disappears>();
                if (d != null) d.ticksToDisappear = Mathf.Max(d.ticksToDisappear, d.Props.disappearsAfterTicks.RandomInRange);
            }
        }

        /// <summary>
        /// A rite held at a god's place has ended: how it went, and what it brings to those who came (the class's summary).
        /// <paramref name="told"/> is what was done, in a sentence; the news adds how it went. Returns how it went.
        /// </summary>
        public static RiteOutcome Held(RiteKind kind, God g, Thing place, Pawn leader, string told, IEnumerable<Pawn> came = null)
        {
            var map = place?.Map;
            if (g == null || map == null) return RiteOutcome.Plain;
            var att = (came ?? Attending(place, leader)).Where(p => p != null && !p.Dead && p.Spawned && p.Map == map && Worship.Ours(p)).Distinct().ToList();
            if (leader != null && !att.Contains(leader) && leader.Spawned && leader.Map == map) att.Insert(0, leader);
            var b = Worship.Book;
            int holders = GodPlaces.Holders(map, g).Count;
            int came2 = att.Count(p => GodPlaces.HoldsFor(p, g));
            bool priest = leader != null && Worship.IsPriest(leader);
            int social = leader?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            float fit = GodPlaces.FitOf(place, g);
            bool offering = b != null && b.offerings.Any(o => o != null && o.shrine == place && o.set && o.fate == OfferingFate.Waiting);
            bool keepsake = GodPlaces.Keepsake(place, g) != null;
            float score = GodPlacesMath.RiteScore(came2, holders, priest, social, Shrines.IsTemple(place), GodPlaces.RoomFine(place, g), fit, offering, keepsake,
                GodPlaces.Neglected(place), Rand.Value);
            var o = GodPlacesMath.Outcome(score);
            string god = Worship.Called(g);
            string why = "CC_Belief_Why_Rite".Translate(god.Named("GOD"), GodLorePage.Date(Now).Named("DATE")).Resolve();
            var bless = BlessDef(g);
            int level = GodPlacesMath.BlessLevel(o);
            var blessed = new List<Pawn>();
            foreach (var p in att)
            {
                if (Worship.Scoffer(p) && !PawnGods.WonOver(p)) continue;
                CouncilGameComponent.GiveThought(p, GodRitesDefOf.Rimfolk_Rite_Kept, null, -1f, null, GodPlacesMath.MoodStage(o));
                if (!GodPlaces.HoldsFor(p, g) && !GodPlaces.Seeks(p, g)) continue;
                PawnGods.Move(p, g, p == leader ? BeliefEvent.RiteLed : BeliefEvent.Festival, why, GodPlacesMath.BeliefScale(o, fit));
                if (level > 0 && GodPlaces.HoldsFor(p, g))
                {
                    Bless(p, bless, level);
                    blessed.Add(p);
                }
            }
            GodPlaces.Tend(place);
            Worship.Favour(g, map, GodPlacesMath.RiteFavour(o, kind) * (priest ? WorshipMath.PriestRiteFactor : 1f), told, FavourSource.Rite);
            // How it went, plainly: who came and what they came away with (the god's own answer, if any, is its own moment).
            string how = ("CC_Wsh_Rite_" + o + (blessed.Count > 0 || o == RiteOutcome.Poor ? "" : "_None")).Translate(att.Count.Named("COUNT"), god.Named("GOD"),
                (bless?.label ?? "").Named("BLESSING")).Resolve();
            Tell.Minor(NewsKind.Colony, o == RiteOutcome.Fine ? Importance.Notable : Importance.Flavor, map, (told.NullOrEmpty() ? "" : told + " ") + how, place);
            if (o >= RiteOutcome.Good) FaithHeart.Witnessed(map, g, place.Position);
            if (Rand.Chance(GodPlacesMath.AnswerChance(o, kind, fit)))
                GodTouch.Rite(g, map, place.Position, att, GodPlacesMath.AnswerLean(o, fit), told);
            return o;
        }

        // ================= The week's service (B1) =================

        /// <summary>
        /// The region's hour: in the early evening, a temple whose last service is a week past holds one: its keeper (the
        /// priest where they hold the god, else the most devout who does) leads it, those who hold the god stand behind.
        /// </summary>
        public static void Services(WorshipBook b, Map map, int now)
        {
            if (!GodPlacesMath.ServiceHour(GenLocalDate.HourOfDay(map))) return;
            if (map.mapPawns.FreeColonistsSpawned.Any(p => p.CurJobDef?.defName == WorshipJobs.ServiceJob || p.CurJobDef?.defName == WorshipJobs.RiteJob)) return;
            foreach (var s in b.shrines.Where(x => x != null && !x.pending && x.Live && x.thing.Map == map).ToList())
            {
                if (s.served >= 0 && now - s.served < GodPlacesMath.ServiceDays * GenDate.TicksPerDay) continue;
                var g = Gods.ById(s.god);
                if (g == null || Shrines.Temple(map, g) != s.thing) continue;
                var holders = GodPlaces.Holders(map, g);
                if (holders.Count < 2) continue;
                var who = new List<Pawn>();
                var priest = Worship.Priest(map);
                if (priest != null && holders.Contains(priest)) who.Add(priest);
                who.AddRange(holders.OrderByDescending(p => PawnGods.HoldOf(p, g)));
                foreach (var p in who.Distinct())
                {
                    if (p.DevelopmentalStage.Juvenile() || !WorshipJobs.Free(p) || !p.CanReach(s.thing, PathEndMode.Touch, Danger.Some)) continue;
                    if (!WorshipJobs.Start(p, WorshipJobs.At(WorshipJobs.ServiceJob, s.thing, GodPlacesMath.ServiceTicks))) continue;
                    s.served = now;
                    OwnFeasts.Gather(p, s.thing, g, GodPlacesMath.ServiceTicks + 600);
                    return;
                }
            }
        }

        /// <summary>The service's words in the colony's age: a fire circle, a mass, a sermon, a broadcast.</summary>
        public static string ServiceWord => ("CC_Wsh_Service_" + WorshipMath.ShrineWord((int)Ages.Ours)).Translate();

        public static void ServiceArrive(Pawn p, Thing place)
        {
            var g = Shrines.GodAt(place);
            if (g == null) return;
            Worship.Say(p, "Rimfolk_Pool_Worship_Service", g, null, "about:" + g.domain.ToString().ToLowerInvariant());
            Worship.PriestLight(p, place.Position);
            Worship.Glow(place.Map, place.Position, g);
        }

        public static void ServiceDone(Pawn p, Thing place)
        {
            var g = Shrines.GodAt(place);
            if (g == null) return;
            string told = "CC_Wsh_ServiceDone".Translate(p.LabelShort.Named("PAWN"), ServiceWord.Named("SERVICE"), place.LabelShort.Named("PLACE"), Worship.Called(g).Named("GOD")).Resolve();
            Held(RiteKind.Service, g, place, p, told);
            if (Worship.IsPriest(p)) CouncilRecords.Add(p, "Council_RitualsLed");
        }

        // ================= Oaths at the place (B3) =================

        /// <summary>A vow just made: where the god has a place the one who vowed can reach, they go there to swear it before it (it weighs more).</summary>
        public static void SwearAtPlace(Pawn p, God g)
        {
            if (p == null || g == null || !p.Spawned || !Worship.On) return;
            var place = Shrines.Nearest(p, g);
            if (place == null || !WorshipJobs.Free(p, false)) return;
            WorshipJobs.Start(p, WorshipJobs.At(WorshipJobs.SwearJob, place, Rand.Range(500, 700)));
        }

        public static void SwearArrive(Pawn p, Thing place)
        {
            var g = Shrines.GodAt(place);
            if (g == null) return;
            Worship.Say(p, "Rimfolk_Pool_Worship_Swear", g);
            Worship.PriestLight(p, place.Position);
        }

        public static void Sworn(Pawn p, Thing place)
        {
            var g = Shrines.GodAt(place);
            var b = Worship.Book;
            if (g == null || b == null) return;
            var v = b.vows.LastOrDefault(x => x != null && !x.done && x.pawn == p && x.god == g.id && !x.atPlace);
            if (v == null) return;
            v.atPlace = true;
            GodPlaces.Tend(place);
            string text = "CC_Wsh_Sworn".Translate(p.LabelShort.Named("PAWN"), place.LabelShort.Named("PLACE"), Worship.Called(g).Named("GOD")).Resolve();
            Tell.Minor(NewsKind.People, Importance.Flavor, place.Map, text, place);
            Worship.Witness(g, place.Map, GodActKind.Vow, text, place.Position);
        }

        /// <summary>A vow sworn at the god's place ends (Vows.End): kept, it binds them closer still and pleases the god; broken, every believer at home feels it.</summary>
        public static void OathEnded(VowRecord r, God g, bool kept)
        {
            // apart: the god's believers at home hear of an oath broken at its place; they need not be beside the one who broke it.
            var p = r?.pawn;
            if (p == null || g == null || !r.atPlace) return;
            var map = p.MapHeld;
            if (kept)
            {
                if (!p.Dead)
                {
                    CouncilGameComponent.GiveThought(p, GodRitesDefOf.Rimfolk_Worship_OathKept);
                    PawnGods.Move(p, g, BeliefEvent.VowKept, null, GodPlacesMath.OathWeight - 1f);
                }
                Worship.Favour(g, map, GodPlacesMath.OathKeptFavour, "CC_Wsh_Why_OathKept".Translate(p.LabelShort.Named("PAWN")).Resolve(), FavourSource.Vow);
                return;
            }
            if (!p.Dead)
            {
                CouncilGameComponent.GiveThought(p, GodRitesDefOf.Rimfolk_Worship_OathBroken);
                PawnGods.Move(p, g, BeliefEvent.VowBroken, null, GodPlacesMath.OathWeight - 1f);
            }
            if (map == null) return;
            foreach (var x in map.mapPawns.FreeColonistsSpawned.Where(x => x != p && GodPlaces.HoldsFor(x, g)).ToList())
                CouncilGameComponent.GiveThought(x, GodRitesDefOf.Rimfolk_Worship_OathBrokenSeen, p);
        }

        // ================= Pilgrims (B2) =================

        /// <summary>
        /// The region's day: a temple of a god a friendly people holds draws pilgrims of that people now and then (the game's
        /// own visitors), one group at a time; they come to pray there.
        /// </summary>
        public static void PilgrimsDaily(WorshipBook b, Map map, int now)
        {
            int home = Gods.HomeKey(map);
            var m = b.Mark(home, "pilgrims");
            if (m != null && now <= m.until) return;
            foreach (var s in b.shrines.Where(x => x != null && !x.pending && x.Live && x.thing.Map == map).InRandomOrder().ToList())
            {
                if (s.pilgrims >= 0 && now - s.pilgrims < GodPlacesMath.PilgrimDays * GenDate.TicksPerDay) continue;
                var g = Gods.ById(s.god);
                if (g == null || Shrines.Temple(map, g) != s.thing || !Rand.Chance(GodPlacesMath.PilgrimChance)) continue;
                var f = g.factions?.Where(x => x != null && !x.IsPlayer && !x.defeated && !x.Hidden && !x.HostileTo(Faction.OfPlayer)).InRandomOrder().FirstOrDefault();
                if (f == null) continue;
                try
                {
                    var def = IncidentDefOf.VisitorGroup;
                    var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                    parms.faction = f;
                    parms.forced = true;
                    if (!def.Worker.CanFireNow(parms) || !def.Worker.TryExecute(parms)) continue;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("worship: pilgrims sent for", e);
                    continue;
                }
                s.pilgrims = now;
                b.SetMark(home, "pilgrims", g.id, f.loadID, now + 2 * GenDate.TicksPerDay);
                Tell.Minor(NewsKind.World, Importance.Notable, map, "CC_Wsh_Pilgrims".Translate(f.Name.Named("PEOPLE"), s.thing.LabelShort.Named("PLACE"),
                    Worship.Called(g).Named("GOD")).Resolve(), s.thing);
                return;
            }
        }

        private static Faction FactionOf(int loadId) => Find.FactionManager?.AllFactionsListForReading.FirstOrDefault(f => f.loadID == loadId);

        /// <summary>The region's hour: while pilgrims are at the home, one of them who is free walks to the temple to pray there.</summary>
        public static void PilgrimsHourly(WorshipBook b, Map map, int now)
        {
            var m = b.Mark(Gods.HomeKey(map), "pilgrims");
            if (m == null || now > m.until || m.value < 0) return;
            var g = Gods.ById(m.god);
            var temple = g == null ? null : Shrines.Temple(map, g);
            var f = FactionOf(m.value);
            if (temple == null || f == null) return;
            var pilgrims = map.mapPawns.SpawnedPawnsInFaction(f).Where(p => p.RaceProps.Humanlike && !p.Downed && !p.InMentalState && p.GetLord() != null).ToList();
            if (pilgrims.Count == 0 || pilgrims.Any(p => p.CurJobDef?.defName == WorshipJobs.VisitJob)) return;
            if (map.dangerWatcher.DangerRating != StoryDanger.None) return;
            foreach (var p in pilgrims.InRandomOrder())
                if (p.CanReach(temple, PathEndMode.Touch, Danger.Some) && WorshipJobs.Start(p, WorshipJobs.At(WorshipJobs.VisitJob, temple, Rand.Range(900, 1300))))
                    return;
        }

        public static void VisitArrive(Pawn p, Thing place)
        {
            var g = Shrines.GodAt(place);
            if (g == null) return;
            Worship.Say(p, "Rimfolk_Pool_Worship_Pilgrim", g);
            Worship.Glow(place.Map, place.Position, g);
        }

        /// <summary>A pilgrim has prayed at the temple: their people's gift left there, their people's goodwill, the place tended. Once a visit.</summary>
        public static void VisitDone(Pawn p, Thing place)
        {
            var b = Worship.Book;
            var g = Shrines.GodAt(place);
            var map = place?.Map;
            if (b == null || g == null || map == null || p?.Faction == null) return;
            var m = b.Mark(Gods.HomeKey(map), "pilgrims");
            if (m == null || m.value != p.Faction.loadID || Worship.Now > m.until) return;
            m.value = -1;
            GodPlaces.Tend(place);
            int worth = GodPlacesMath.PilgrimGift(map.mapPawns.FreeColonistsSpawnedCount);
            var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = worth;
            GenPlace.TryPlaceThing(silver, p.Position, map, ThingPlaceMode.Near);
            WorldSituationWorker.Goodwill(p.Faction, GodPlacesMath.PilgrimGoodwill, GoodwillWhy.SharedGod, false);
            string text = "CC_Wsh_PilgrimGift".Translate(p.LabelShort.Named("PAWN"), p.Faction.Name.Named("PEOPLE"), place.LabelShort.Named("PLACE"), worth.Named("COUNT"),
                GodPlacesMath.PilgrimGoodwill.Named("GOODWILL")).Resolve();
            Tell.Minor(NewsKind.World, Importance.Notable, map, text, place);
            Worship.Witness(g, map, GodActKind.Favour, text, place.Position);
        }

        // ================= The game's own rites at a god's place (B4) =================

        /// <summary>
        /// A rite of the game's own (a wedding, a funeral, a naming, a speech) held at a god's place or beside it carries that
        /// god into it: written in the colony's book, and its fruit for those who came (<see cref="Held"/>).
        /// </summary>
        public static void GameRite(LordJob_Ritual lj)
        {
            var target = lj?.selectedTarget ?? TargetInfo.Invalid;
            var map = target.Map;
            var b = Worship.Book;
            if (map == null || b == null || !Homes.IsHome(map)) return;
            Thing place = null;
            if (target.HasThing && b.ShrineOf(target.Thing) is ShrineRecord own && !own.pending) place = target.Thing;
            if (place == null)
            {
                var cell = target.Cell;
                var room = cell.IsValid ? cell.GetRoom(map) : null;
                place = b.shrines.Where(s => s != null && !s.pending && s.Live && s.thing.Map == map && s.thing.Position.InHorDistOf(cell, 4f)
                                             && (room == null || room.PsychologicallyOutdoors || s.thing.GetRoom() == room))
                    .Select(s => s.thing).OrderBy(t => t.Position.DistanceToSquared(cell)).FirstOrDefault();
            }
            var g = place == null ? null : Shrines.GodAt(place);
            if (g == null) return;
            var came = lj.PawnsToCountTowardsPresence?.Where(p => p != null && Worship.Ours(p)).ToList() ?? new List<Pawn>();
            if (came.Count == 0) return;
            string told = "CC_Wsh_GameRite".Translate(lj.RitualLabel.Named("RITE"), place.LabelShort.Named("PLACE"), Worship.Called(g).Named("GOD")).Resolve();
            Held(RiteKind.GameRite, g, place, lj.Organizer, told, came);
            Worship.Booked(g, map, place.Position, told);
        }
    }

    /// <summary>A rite of the game's own held at a god's place carries the god into it (GodRites.GameRite).</summary>
    [HarmonyPatch(typeof(LordJob_Ritual), nameof(LordJob_Ritual.ApplyOutcome))]
    public static class Patch_GameRiteAtGodPlace
    {
        public static void Postfix(LordJob_Ritual __instance, float progress, bool cancelled)
        {
            if (!RimfolkPatches.On || cancelled || progress < 0.5f || !Worship.On) return;
            try
            {
                GodRites.GameRite(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: a rite of the game's at a god's place", e);
            }
        }
    }
}
