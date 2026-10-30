using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Missions that come from a people that thinks well of the colony (World/FactionMemory.cs), and add to what it
    // remembers: an invitation to their feast or wedding (a caravan to their town), an apprentice sent to learn from one
    // of their masters (a caravan there, days at the master's side), a dignitary to host (a bedroom fit for them, kept
    // for their stay), and refugees to see through (fed and sheltered for some days). Texts:
    // Defs/MissionDefs/Council_Missions_Remembered.xml.

    /// <summary>Which people asks, and where they live.</summary>
    public static class MissionPeoples
    {
        /// <summary>A people on good terms with the colony, likelier the more it owes it; with <paramref name="pred"/>.</summary>
        public static Faction Grateful(Func<Faction, bool> pred = null)
        {
            var mem = FactionMemory.On ? FactionMemory.Instance : null;
            var tech = Avail.EffectiveTech;
            return Find.FactionManager.AllFactionsListForReading
                .Where(f => FactionMemory.Remembers(f) && !f.defeated && LivingEvents.Friendly(f) && VarietyUtil.OnGoodTerms(f) && Arrivals.PersonKind(f) != null
                            && (f.def.defName != "Empire" || ModsConfig.RoyaltyActive) && (pred == null || pred(f)))
                .RandomElementByWeightWithFallback(f => Arrivals.TechWeight(f, tech) * (1f + (mem?.Sum(f, DeedSide.Favor) ?? 0f) / 8f));
        }

        /// <summary>The people's town nearest the colony, or null.</summary>
        public static Settlement Town(Faction f, Map map)
        {
            if (f == null || map == null) return null;
            Settlement best = null;
            float bestDist = float.MaxValue;
            foreach (var s in Find.WorldObjects.Settlements)
            {
                if (s.Faction != f || !s.HasName) continue;
                float d = LivingEvents.Distance(map.Tile, s.Tile);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = s;
                }
            }
            return best;
        }

        /// <summary>The town a mission names, if it still stands.</summary>
        public static Settlement TownOf(Mission m) =>
            m?.faction == null ? null : Find.WorldObjects.Settlements.FirstOrDefault(s => s.Faction == m.faction && s.HasName && s.Name == m.place);

        private static readonly Dictionary<int, (int day, float days)> travelCache = new Dictionary<int, (int, float)>();

        /// <summary>About how long a caravan takes from the colony to a town, in days (worked out once a day per town: it's a path search).</summary>
        public static float TravelDays(Map map, Settlement s)
        {
            if (map == null || s == null) return 99f;
            int today = Find.TickManager.TicksGame / GenDate.TicksPerDay;
            int key = Gen.HashCombineInt(s.ID, map.uniqueID);
            if (travelCache.TryGetValue(key, out var c) && c.day == today) return c.days;
            float days = LivingEvents.Distance(map.Tile, s.Tile) * 0.3f;
            try
            {
                int ticks = CaravanArrivalTimeEstimator.EstimatedTicksToArrive(map.Tile, s.Tile, null);
                if (ticks > 0) days = ticks / (float)GenDate.TicksPerDay;
            }
            catch
            {
                // No path the estimator can find: the straight distance will do.
            }
            if (travelCache.Count > 200) travelCache.Clear();
            travelCache[key] = (today, days);
            return days;
        }

        /// <summary>The colony's caravans standing at a town.</summary>
        public static List<Caravan> CaravansAt(Settlement s)
        {
            var list = new List<Caravan>();
            if (s == null) return list;
            foreach (var c in Find.WorldObjects.Caravans)
                if (c != null && c.IsPlayerControlled && c.Tile == s.Tile)
                    list.Add(c);
            return list;
        }

        /// <summary>The colony's own people in a caravan (not prisoners, not animals).</summary>
        public static List<Pawn> Colonists(Caravan c) =>
            c?.PawnsListForReading.Where(p => p != null && !p.Dead && p.IsColonist && p.RaceProps.Humanlike && !p.IsPrisoner).ToList() ?? new List<Pawn>();
    }

    // ===================== Journeys: a caravan to their town =====================

    /// <summary>A mission the colony fulfils by sending a caravan to the people's nearest town.</summary>
    public abstract class HappeningWorker_MissionJourney : HappeningWorker_Mission
    {
        public override bool OwnMemory => true;

        /// <summary>The farthest a town may be, in a caravan's days.</summary>
        protected virtual float MaxTravelDays => 3.5f;

        protected bool Reachable(Faction f, Map map)
        {
            var s = MissionPeoples.Town(f, map);
            return s != null && MissionPeoples.TravelDays(map, s) <= MaxTravelDays;
        }

        protected Faction Asker(Map map) => MissionPeoples.Grateful(f => Reachable(f, map));

        public override LookTargets Targets(Mission m)
        {
            var s = MissionPeoples.TownOf(m);
            return s != null ? new LookTargets(s) : LookTargets.Invalid;
        }

        /// <summary>The town is gone (destroyed, abandoned): the mission can't go on.</summary>
        protected static bool TownGone(Mission m)
        {
            if (MissionPeoples.TownOf(m) != null) return false;
            Missions.Finish(m, MissionState.Failed, "cancelled", null);
            return true;
        }
    }

    /// <summary>
    /// The people invite the colony to their feast or a wedding at their town: at least two of the colony's people must
    /// get there by caravan before the day, and stay for it (about half a day). They come home with a gift and a good
    /// memory; the people remember who came. Not going is noticed, though not much held against the colony.
    /// </summary>
    public class HappeningWorker_MissionInvitation : HappeningWorker_MissionJourney
    {
        protected override string Kind => "invitation";

        protected override float RestDays => 60f;

        protected override int Rating => 1;

        /// <summary>Hours the guests stay for the feast.</summary>
        private const int FeastHours = 10;

        protected override bool Possible(Map map) => StorytellerEvents.Adults(map).Count() >= 3 && Asker(map) != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Asker(map);
            var town = MissionPeoples.Town(m.faction, map);
            if (m.faction == null || town == null) return false;
            m.place = town.Name;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            var season = GenLocalDate.Season(map);
            m.variant = Rand.Chance(0.45f) ? "wedding" : season == Season.Winter || season == Season.PermanentWinter ? "midwinter" : "feast";
            // Who it's for: the bride, or the people's head who gives the feast. Named, never met.
            var kind = Arrivals.PersonKind(m.faction);
            bool wedding = m.variant == "wedding";
            var p = kind == null ? null : Arrivals.Make(kind, m.faction, wedding ? new FloatRange(18f, 28f) : new FloatRange(45f, 75f), null, null);
            m.name = p?.LabelShort ?? "?";
            if (p != null) Arrivals.Discard(p);
            m.target = 2;
            float travel = MissionPeoples.TravelDays(map, town);
            m.tale = TextUtil.DaysString(Mathf.RoundToInt(Mathf.Max(0.3f, travel) * GenDate.TicksPerDay));
            m.days = Mathf.Ceil(travel * 1.5f + 3f);
            m.reward = StoryChains.Silver(map, 180f, 320f);
            m.goodwill = 10;
            m.penalty = 4;
            Fill(m, ctx);
            return true;
        }

        public override bool Start(Mission m, Map map) => MissionPeoples.TownOf(m) != null;

        public override void Check(Mission m, Map map, int now)
        {
            if (TownGone(m)) return;
            var town = MissionPeoples.TownOf(m);
            var here = MissionPeoples.CaravansAt(town).OrderByDescending(c => MissionPeoples.Colonists(c).Count).FirstOrDefault();
            int guests = here == null ? 0 : MissionPeoples.Colonists(here).Count;
            if (m.stage == 0)
            {
                if (guests >= m.target)
                {
                    m.stage = 1;
                    m.due = now + FeastHours * GenDate.TicksPerHour;
                    string line = Line(More?.ready, m, map, null, TextUtil.DaysString(m.due - now));
                    // At once, not in the evening's news: it says how long the caravan must stay.
                    if (!line.NullOrEmpty()) Messages.Message(line, new LookTargets(here), MessageTypeDefOf.PositiveEvent, false);
                }
                else if (now >= m.deadline)
                {
                    Missions.Goodwill(m, -m.penalty);
                    FactionMemory.Note(m.faction, "absent", m.place);
                    Missions.Finish(m, MissionState.Failed, "absent", null);
                }
                return;
            }
            if (now < m.due) return;
            if (guests > 0)
            {
                // The guests sat at the feast to the end: a gift goes home with them, and a good evening.
                var gifts = Loot.Make(m.reward);
                foreach (var t in gifts) CaravanInventoryUtility.GiveThing(here, t);
                var thought = StorytellerEvents.Thought("Council_Mem_FeastGuest");
                if (thought != null)
                    foreach (var p in MissionPeoples.Colonists(here))
                        CouncilGameComponent.GiveThought(p, thought);
                Missions.Goodwill(m, m.goodwill);
                FactionMemory.Note(m.faction, "feast", m.place);
                Missions.Finish(m, MissionState.Done, "attended", Loot.Describe(gifts), new LookTargets(here));
                return;
            }
            // They came, and left before the end.
            Missions.Goodwill(m, m.goodwill / 2);
            FactionMemory.Note(m.faction, "feast", m.place, 4);
            Missions.Finish(m, MissionState.Done, "brief", null, null, LetterDefOf.NeutralEvent);
        }

        public override string Status(Mission m)
        {
            if (m.stage == 1) return "CC_Mission_Invitation_There".Translate(m.place ?? "?", TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame))).Resolve();
            return "CC_Mission_Invitation_Go".Translate(m.target, m.place ?? "?", m.tale ?? "?").Resolve();
        }
    }

    /// <summary>
    /// A master of the people's craft offers to teach one of the colony's people for a few days at their town. The
    /// apprentice must get there by caravan and stay at the master's side long enough (the caravan waits at the town);
    /// they come back about two levels better at the craft. The people remember they taught the colony.
    /// </summary>
    public class HappeningWorker_MissionApprentice : HappeningWorker_MissionJourney
    {
        protected override string Kind => "apprentice";

        protected override float RestDays => 90f;

        protected override float MaxTravelDays => 3f;

        private static readonly string[] Crafts = { "Crafting", "Artistic", "Construction", "Cooking", "Plants", "Animals", "Medicine", "Intellectual" };

        /// <summary>A craft the people could teach: a modern one only from a modern people.</summary>
        private static IEnumerable<SkillDef> Teaches(Faction f)
        {
            foreach (var n in Crafts)
            {
                var s = DefDatabase<SkillDef>.GetNamedSilentFail(n);
                if (s == null) continue;
                if (n == "Intellectual" && f.def.techLevel < TechLevel.Industrial) continue;
                yield return s;
            }
        }

        /// <summary>Someone who could learn it: old enough, able, not already a master.</summary>
        private static bool CanLearn(Pawn p, SkillDef s)
        {
            if (p == null || p.Dead || !p.IsColonist || p.DevelopmentalStage.Baby() || p.ageTracker.AgeBiologicalYears < 13) return false;
            var r = p.skills?.GetSkill(s);
            return r != null && !r.TotallyDisabled && r.Level >= 2 && r.Level <= 12;
        }

        protected override bool Possible(Map map)
        {
            var f = Asker(map);
            return f != null && Teaches(f).Any(s => IncidentWorker_Happening.Colonists(map).Any(p => CanLearn(p, s)));
        }

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = Asker(map);
            var town = MissionPeoples.Town(m.faction, map);
            if (m.faction == null || town == null) return false;
            var people = IncidentWorker_Happening.Colonists(map);
            // A craft someone here could learn, the keen first.
            var options = new List<(SkillDef s, Pawn p, float w)>();
            foreach (var s in Teaches(m.faction))
                foreach (var p in people.Where(p => CanLearn(p, s)))
                {
                    var pass = p.skills.GetSkill(s).passion;
                    options.Add((s, p, pass == Passion.Major ? 4f : pass == Passion.Minor ? 2.5f : 1f));
                }
            if (options.Count == 0) return false;
            var pick = options.RandomElementByWeight(o => o.w);
            m.a = pick.p;
            m.variant = pick.s.defName;
            m.tale = HappeningText.SkillNoun(pick.s);
            m.place = town.Name;
            var kind = Arrivals.PersonKind(m.faction);
            var master = kind == null ? null : Arrivals.Make(kind, m.faction, new FloatRange(40f, 70f), null, null);
            m.name = master?.LabelShort ?? "?";
            if (master != null) Arrivals.Discard(master);
            m.target = Rand.RangeInclusive(30, 42);
            float travel = MissionPeoples.TravelDays(map, town);
            m.days = Mathf.Ceil(travel * 2f + m.target / 24f + 3f);
            m.goodwill = 6;
            m.penalty = 3;
            Fill(m, ctx);
            ctx.a = m.a;
            ctx.skill = pick.s;
            return true;
        }

        public override bool Start(Mission m, Map map) => m.a != null && !m.a.Dead && MissionPeoples.TownOf(m) != null;

        private SkillDef Skill(Mission m) => DefDatabase<SkillDef>.GetNamedSilentFail(m.variant ?? "");

        public override void Check(Mission m, Map map, int now)
        {
            var a = m.a;
            if (a == null || a.Dead || !a.IsColonist)
            {
                Missions.Finish(m, MissionState.Failed, "lost", null);
                return;
            }
            if (TownGone(m)) return;
            var town = MissionPeoples.TownOf(m);
            var caravan = a.GetCaravan();
            bool there = caravan != null && caravan.Tile == town.Tile;
            if (there)
            {
                if (m.stage == 0)
                {
                    m.stage = 1;
                    m.lastLook = now;
                    string line = Line(More?.ready, m, map, null, TextUtil.DaysString(m.target * GenDate.TicksPerHour));
                    // At once, not in the evening's news: it says how long the caravan must stay.
                    if (!line.NullOrEmpty()) Messages.Message(line, new LookTargets(caravan), MessageTypeDefOf.PositiveEvent, false);
                }
                else m.tally += Mathf.Max(0, now - Mathf.Max(m.lastLook, m.started));
            }
            m.lastLook = now;
            int need = m.target * GenDate.TicksPerHour;
            if (m.tally >= need)
            {
                Learned(m, 1f);
                Missions.Goodwill(m, m.goodwill);
                FactionMemory.Note(m.faction, "taught", a.LabelShort);
                Missions.Finish(m, MissionState.Done, "learned", null, caravan != null ? new LookTargets(caravan) : LookTargets.Invalid);
                return;
            }
            if (now < m.deadline) return;
            if (m.tally * 2 >= need)
            {
                Learned(m, 0.5f);
                FactionMemory.Note(m.faction, "taught", a.LabelShort, 4);
                Missions.Finish(m, MissionState.Done, "partial", null, null, LetterDefOf.NeutralEvent);
                return;
            }
            Missions.Goodwill(m, -m.penalty);
            FactionMemory.Note(m.faction, "letDown", null, 4);
            Missions.Finish(m, MissionState.Failed, "missed", null);
        }

        /// <summary>About two levels (a share of them) in the craft, and a good memory of the master.</summary>
        private void Learned(Mission m, float share)
        {
            var s = Skill(m);
            var r = s == null ? null : m.a.skills?.GetSkill(s);
            if (r == null || r.TotallyDisabled) return;
            float xp = 0f;
            for (int i = 0; i < 2; i++) xp += SkillRecord.XpRequiredToLevelUpFrom(Mathf.Min(19, r.Level + i));
            r.Learn(xp * 0.9f * share, true);
            var thought = StorytellerEvents.Thought("Council_Mem_Apprenticed");
            if (thought != null) CouncilGameComponent.GiveThought(m.a, thought);
        }

        public override string Status(Mission m)
        {
            string who = m.a?.LabelShort ?? "?";
            if (m.stage == 0) return "CC_Mission_Apprentice_Go".Translate(who, m.place ?? "?").Resolve();
            int hours = Mathf.Min(m.target, m.tally / GenDate.TicksPerHour);
            return "CC_Mission_Apprentice_There".Translate(who, m.name ?? "?", hours, m.target).Resolve();
        }

        public override LookTargets Targets(Mission m)
        {
            var c = m.a?.GetCaravan();
            if (c != null) return new LookTargets(c);
            if (m.a != null && m.a.Spawned) return new LookTargets(m.a);
            return base.Targets(m);
        }
    }

    // ===================== Hosting: their people at the colony =====================

    /// <summary>
    /// A mission where the people's own come to the colony for some days (they're made when it's offered and kept in
    /// the world until they arrive, <see cref="Mission.wait"/> days after the colony agrees). While they stay they eat
    /// at the colony's table and sleep in the beds it keeps for them (<see cref="Hosting"/>).
    /// </summary>
    public abstract class HappeningWorker_MissionHost : HappeningWorker_Mission
    {
        public override bool OwnMemory => true;

        protected IEnumerable<Pawn> Guests(Mission m) => m.group.Prepend(m.x).Where(p => p != null).Distinct();

        protected IEnumerable<Pawn> Here(Mission m, Map map) => Guests(m).Where(p => !p.Dead && p.Spawned && p.Map == map && p.Faction == m.faction);

        /// <summary>One of this mission's guests.</summary>
        protected static bool Ours(Mission m, Pawn p) => p != null && (p == m.x || m.group.Contains(p));

        public override bool Start(Mission m, Map map)
        {
            if (!Guests(m).Any(p => !p.Dead && !p.Discarded)) return false;
            m.due = Find.TickManager.TicksGame + Mathf.RoundToInt(m.wait * GenDate.TicksPerDay);
            return true;
        }

        /// <summary>Half a day before they come, a reminder of what they asked (once).</summary>
        protected void Remind(Mission m, Map map, int now)
        {
            if (m.reminded || now < m.due - GenDate.TicksPerDay / 2) return;
            m.reminded = true;
            string line = Line(More?.reminder, m, map, Missions.GoodsText(m), TextUtil.DaysString(Mathf.Max(0, m.due - now)));
            if (!line.NullOrEmpty()) Messages.Message(line, LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>They walk in with food enough for their stay, and stay until the mission's end. False: they couldn't come.</summary>
        protected bool Arrive(Mission m, Map map, IntVec3? spot, float hours)
        {
            var people = Guests(m).Where(p => !p.Dead && !p.Discarded && !p.Spawned).ToList();
            if (people.Count == 0) return false;
            hours = Mathf.Max(4f, hours);
            if (Arrivals.Visit(people, map, m.faction, spot, hours) == null) return false;
            foreach (var p in people) Hosting.Provision(p, hours / 24f);
            return true;
        }

        /// <summary>The mission's guests were harmed: most of them dead, or one of them taken prisoner.</summary>
        protected bool Harmed(Mission m)
        {
            var all = Guests(m).ToList();
            if (all.Count == 0) return true;
            int dead = all.Count(p => p.Dead);
            return dead * 2 > all.Count || all.Any(p => !p.Dead && p.IsPrisonerOfColony);
        }

        /// <summary>Something of the colony's for this guest to eat now, or null.</summary>
        protected abstract Thing FoodFor(Mission m, Map map, Pawn p);

        /// <summary>The bed this guest sleeps in tonight, or null.</summary>
        protected abstract Building_Bed BedFor(Mission m, Map map, Pawn p);

        /// <summary>
        /// Every look while they stay: the hungry go and eat the colony's food at its table (with none for them, what they
        /// brought), anyone off to bed hungry has supper first, at night each lies down in the bed kept for them (or on
        /// the floor with none), and once they've slept they get up, to breakfast if they're hungry. A guest no longer
        /// here gives their bed back. Returns what someone went without.
        /// </summary>
        protected Hosting.Lack Tend(Mission m, Map map)
        {
            var lack = Hosting.Lack.None;
            foreach (var p in Guests(m).ToList())
            {
                if (p.Dead || !p.Spawned || p.Map != map || p.Faction != m.faction)
                {
                    // On the road again, or dead: the bed they had goes back to the colony.
                    Hosting.GiveBack(p);
                    continue;
                }
                float food = p.needs?.food?.CurLevelPercentage ?? 1f;
                Thing ours = null;
                bool looked = false;
                // Hungry with nothing of the colony's to eat: they eat what they brought, and the day goes short.
                if (food < Hosting.PackBelow && p.Awake() && !Hosting.EatingOurs(p))
                {
                    ours = FoodFor(m, map, p);
                    looked = true;
                    if (ours == null) lack |= Hosting.Lack.Food;
                }
                bool sleepy = Hosting.WantsSleep(p);
                if (Hosting.LyingAwake(p) && !sleepy)
                {
                    // Slept: up, and to breakfast if they're hungry.
                    if (food < Hosting.EatBelow)
                    {
                        if (!looked) ours = FoodFor(m, map, p);
                        if (ours != null && Hosting.Eat(p, ours, m)) continue;
                    }
                    Hosting.GetUp(p);
                    continue;
                }
                if (!Hosting.Free(p)) continue;
                if (food < Hosting.EatBelow || sleepy && food < Hosting.SupperBelow)
                {
                    if (!looked) ours = FoodFor(m, map, p);
                    if (ours != null && Hosting.Eat(p, ours, m)) continue;
                    // Nothing of the colony's: what they brought, once they're hungry, or before they sleep hungry.
                    if (food < Hosting.PackBelow || sleepy)
                    {
                        if (ours == null) lack |= Hosting.Lack.Food;
                        var pack = Hosting.Pack(p);
                        if (pack != null && Hosting.Eat(p, pack, null)) continue;
                    }
                }
                if (!sleepy) continue;
                var bed = BedFor(m, map, p);
                if (bed != null && Hosting.Sleep(p, bed)) continue;
                lack |= Hosting.Lack.Bed;
                Hosting.SleepRough(p);
            }
            return lack;
        }

        /// <summary>The stay is over: those still here set off home now (the mission's end gives their beds back).</summary>
        protected void SeeOff(Mission m, Map map) => Arrivals.SendAway(Here(m, map).ToList());

        public override void Release(Mission m)
        {
            var map = m.Map;
            foreach (var p in Guests(m).ToList())
            {
                // Up from where they lay, and the bed they had goes back to the colony.
                Hosting.GetUp(p);
                Hosting.GiveBack(p, map);
                if (p.Dead) continue;
                // Never came (the offer was turned down, or ran out): they were only ever names.
                if (!p.Spawned && m.started < 0 && p.GetCaravan() == null) Arrivals.Discard(p);
                else Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            }
        }

        public override LookTargets Targets(Mission m)
        {
            var here = Guests(m).Where(p => p.Spawned).ToList();
            return here.Count > 0 ? new LookTargets(here) : LookTargets.Invalid;
        }

        /// <summary>"Meals they have eaten from the colony's stores: 5." once they're here.</summary>
        protected static string MealsText(Mission m) => "CC_Mission_Host_Meals".Translate(m.meals).Resolve();

        protected static Pawn Make(Faction f, FloatRange ages)
        {
            var kind = Arrivals.PersonKind(f);
            var p = kind == null ? null : Arrivals.Make(kind, f, ages);
            if (p != null) Arrivals.Hold(p);
            return p;
        }
    }

    /// <summary>
    /// A dignitary of the people (an elder, an envoy of rank, a lord's kin) comes to stay a few days, and asks for a
    /// bedroom fit for them: a room with one bed nobody else sleeps in, at least so impressive, ready when they come and
    /// kept for them while they stay (checked every hour). The bed is in their name for the stay, they sleep in it at
    /// night and eat the colony's finest meals at its table. Kept well, they leave a gift and speak well of the colony;
    /// not there at all, they wait outside until morning and leave, slighted.
    /// </summary>
    public class HappeningWorker_MissionDignitary : HappeningWorker_MissionHost
    {
        protected override string Kind => "dignitary";

        protected override float RestDays => 75f;

        protected override bool Possible(Map map) => MissionPeoples.Grateful() != null;

        /// <summary>
        /// The best room fit for the guest: a bedroom with one bed, nobody's or already <paramref name="guest"/>'s, at least
        /// <paramref name="target"/> impressive.
        /// </summary>
        public static Room GuestRoom(Map map, int target, Pawn guest = null)
        {
            Room best = null;
            float bestImp = -1f;
            foreach (var room in RoomUtil.ColonyRooms(map))
            {
                if (!Fits(room, target, guest, out float imp) || imp <= bestImp) continue;
                best = room;
                bestImp = imp;
            }
            return best;
        }

        /// <summary>
        /// A bedroom with one bed for the guest's use alone: the colony's, not a prisoner's, slave's or sickbed, not one of
        /// Hospitality's guest beds (those are for its own guests), owned by nobody or by <paramref name="guest"/>.
        /// </summary>
        private static bool Fits(Room room, int target, Pawn guest, out float imp)
        {
            imp = 0f;
            if (room == null || room.Dereferenced || room.PsychologicallyOutdoors || room.Role != RoomRoleDefOf.Bedroom || room.IsPrisonCell) return false;
            var beds = room.ContainedBeds.ToList();
            if (beds.Count != 1) return false;
            var bed = beds[0];
            if (bed.Medical || bed.ForPrisoners || bed.ForSlaves || bed.Faction != Faction.OfPlayer || GuestBeds.IsGuestBed(bed)) return false;
            if (bed.def.building?.bed_humanlike != true || bed.OwnersForReading.Any(o => o != guest)) return false;
            imp = RoomUtil.Impressiveness(room);
            return imp >= target;
        }

        /// <summary>The room the guest sleeps in: the one whose bed is theirs while it stays fit, else the best other.</summary>
        private static Room RoomFor(Map map, Mission m)
        {
            var own = m.x?.ownership?.OwnedBed;
            var room = own != null && own.Spawned && own.Map == map ? own.GetRoom() : null;
            if (room != null && Fits(room, m.target, m.x, out _)) return room;
            return GuestRoom(map, m.target, m.x);
        }

        private static string StageOf(int target) => RoomUtil.StageLabel(RoomStatDefOf.Impressiveness, target);

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = MissionPeoples.Grateful();
            if (m.faction == null) return false;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            m.x = Make(m.faction, new FloatRange(35f, 68f));
            if (m.x == null) return false;
            m.name = m.x.LabelShort;
            float wealth = map.wealthWatcher.WealthTotal;
            m.target = wealth < 60000f ? 40 : wealth < 160000f ? 50 : 65;
            m.tale = StageOf(m.target);
            m.variant = m.faction.def.techLevel <= TechLevel.Medieval ? "elder" : "official";
            m.wait = Rand.Range(1.5f, 2.5f);
            m.days = m.wait + Rand.Range(2f, 3f);
            m.reward = StoryChains.Silver(map, 250f, 450f);
            m.goodwill = 14;
            m.penalty = 6;
            Fill(m, ctx);
            ctx.stranger = m.x;
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var x = m.x;
            if (x == null || x.Discarded)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (m.stage >= 1 && Harmed(m))
            {
                Missions.Goodwill(m, -m.penalty * 2);
                Missions.Finish(m, MissionState.Failed, "harmed", null);
                return;
            }
            if (m.stage == 0)
            {
                Remind(m, map, now);
                if (now < m.due) return;
                var room = GuestRoom(map, m.target, x);
                // Nowhere fit for them: they wait outside the colony until morning, and leave then.
                float hours = room != null ? (m.deadline - now) / (float)GenDate.TicksPerHour : Hosting.HoursToMorning(map);
                IntVec3? spot = null;
                if (room != null)
                {
                    var cell = room.Cells.Where(c => c.Standable(map)).RandomElementWithFallback(IntVec3.Invalid);
                    if (cell.IsValid) spot = cell;
                }
                if (!Arrive(m, map, spot, hours))
                {
                    Missions.Finish(m, MissionState.Failed, "ended", null);
                    return;
                }
                if (room == null)
                {
                    m.stage = 2;
                    Missions.Goodwill(m, -m.penalty);
                    FactionMemory.Note(m.faction, "slighted", m.name);
                    Missions.Finish(m, MissionState.Failed, "noroom", null, new LookTargets(x));
                    return;
                }
                // The room is theirs for the stay: its bed in their name, so nobody else takes it.
                var bed = room.ContainedBeds.FirstOrDefault();
                if (bed != null && !bed.OwnersForReading.Contains(x)) x.ownership?.ClaimBedIfNonMedical(bed);
                m.stage = 1;
                m.lastLook = now;
                string line = Line(More?.ready, m, map, null, TextUtil.DaysString(Mathf.Max(0, m.deadline - now)));
                // At once, not in the evening's news: the room is checked from now on.
                if (!line.NullOrEmpty()) Messages.Message(line, new LookTargets(x), MessageTypeDefOf.PositiveEvent, false);
                return;
            }
            // Every look of the stay: the guest eats at the colony's table, and sleeps in the room kept for them.
            Tend(m, map);
            // Every hour, the room is looked at.
            if (now - m.lastLook >= GenDate.TicksPerHour)
            {
                m.lastLook = now;
                m.tally++;
                if (GuestRoom(map, m.target, x) == null) m.misses++;
            }
            // Gone before the end (a fight drove them off): the stay is judged on the hours there were.
            bool gone = !x.Spawned || x.Map != map;
            if (now < m.deadline && !gone) return;
            if (m.tally == 0)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            var look = x.Spawned ? new LookTargets(x) : LookTargets.Invalid;
            if (m.misses == 0)
            {
                Couriers.PaySilver(map, x.Spawned && x.Map == map ? x.Position : IntVec3.Invalid, m.reward, m.faction, x);
                Missions.Goodwill(m, m.goodwill);
                FactionMemory.Note(m.faction, "dignitary", m.name);
                var thought = StorytellerEvents.Thought("Council_Mem_DignitaryPleased");
                if (thought != null)
                    foreach (var p in StorytellerEvents.Adults(map).ToList())
                        CouncilGameComponent.GiveThought(p, thought);
                SeeOff(m, map);
                Missions.Finish(m, MissionState.Done, "honored", m.reward.ToString(), look);
            }
            else if (m.misses * 4 <= Mathf.Max(1, m.tally))
            {
                Missions.Goodwill(m, m.goodwill / 2);
                FactionMemory.Note(m.faction, "dignitary", m.name, 6);
                SeeOff(m, map);
                Missions.Finish(m, MissionState.Done, "fair", null, look, LetterDefOf.NeutralEvent);
            }
            else
            {
                Missions.Goodwill(m, -m.penalty);
                FactionMemory.Note(m.faction, "slighted", m.name);
                SeeOff(m, map);
                Missions.Finish(m, MissionState.Failed, "slighted", null, look);
            }
        }

        /// <summary>The colony's finest meal for them.</summary>
        protected override Thing FoodFor(Mission m, Map map, Pawn p) => Hosting.Meal(p, true);

        protected override Building_Bed BedFor(Mission m, Map map, Pawn p)
        {
            var bed = RoomFor(map, m)?.ContainedBeds.FirstOrDefault();
            if (bed != null) return bed;
            // No room fit for them tonight: a bed nobody uses, if there's one.
            return Hosting.Beds(map, o => o == p, p).OrderBy(b => b.Position.DistanceToSquared(p.Position)).FirstOrDefault();
        }

        public override string Status(Mission m)
        {
            var map = m.Map;
            bool ready = map != null && GuestRoom(map, m.target, m.x) != null;
            string room = (ready ? "CC_Mission_Dignitary_RoomReady" : "CC_Mission_Dignitary_RoomNot").Translate(m.tale ?? "?").Resolve();
            if (m.stage == 0)
                return "CC_Mission_Dignitary_Coming".Translate(m.name ?? "?", TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame))).Resolve() + " " + room;
            return "CC_Mission_Dignitary_Staying".Translate(m.name ?? "?", m.misses).Resolve() + " " + room + " " + MealsText(m);
        }
    }

    /// <summary>
    /// People of a friendly town who lost their homes (to fire, or to whatever runs in the region: a plague, a famine, a
    /// flood, a hard winter, a war) ask the colony to see them through a few days: fed from its stores (they eat at its
    /// table when hungry, simple meals or better, raw food when there are none) and sheltered (a bed or sleeping spot
    /// under a roof nobody else uses for each, slept in at night). Each day is counted fed and sheltered, or not: short
    /// when one of them was hungry with nothing of the colony's to eat, or had no bed for the night. Nobody starves for
    /// it: they came with food enough for the stay, and eat it when the colony has none for them.
    /// </summary>
    public class HappeningWorker_MissionRefugees : HappeningWorker_MissionHost
    {
        protected override string Kind => "refugees";

        protected override float RestDays => 60f;

        /// <summary>The trouble in the region the refugees fled, as a tag, and its people (null: a fire at home).</summary>
        private static string Fled(out Faction f)
        {
            f = null;
            var ws = WorldSituations.Instance;
            if (ws == null) return null;
            foreach (var s in ws.Running)
            {
                string tag = null;
                switch (s.def?.defName)
                {
                    case "Council_WS_Plague": tag = "plague"; break;
                    case "Council_WS_Famine":
                    case "Council_WS_Locusts":
                    case "Council_WS_Drought": tag = "famine"; break;
                    case "Council_WS_Flood": tag = "flood"; break;
                    case "Council_WS_HardWinter": tag = "winter"; break;
                    case "Council_WS_Abandoned": tag = "abandoned"; break;
                    case "Council_WS_GreatFire": tag = "fire"; break;
                    case "Council_WS_Warlord":
                    case "Council_WS_BanditRoad": tag = "war"; break;
                }
                if (tag == null) continue;
                var who = s.ctx?.faction;
                if (who != null && LivingEvents.Friendly(who) && Arrivals.PersonKind(who) != null && FactionMemory.Remembers(who)) f = who;
                return tag;
            }
            // Some weeks after a plague, a famine, a flood, a hard winter or a war has ended, its homeless are still on the roads.
            int now = Find.TickManager.TicksGame;
            for (int i = ws.history.Count - 1; i >= 0; i--)
            {
                var r = ws.history[i];
                if (r == null || r.ending == null || r.ending == WorldSituations.Quietly || now - r.endTick > GenDate.TicksPerDay * 30) continue;
                string tag = r.defName == "Council_WS_Murrain" ? null : HappeningWorker_Aftermath.KindTag(r.defName);
                if (tag == null || tag == "locusts") continue;
                if (tag == "drought") tag = "famine";
                var who = r.faction;
                if (who == null || who.defeated || !LivingEvents.Friendly(who) || Arrivals.PersonKind(who) == null || !FactionMemory.Remembers(who)) continue;
                f = who;
                return tag;
            }
            return null;
        }

        /// <summary>The beds and sleeping spots the refugees may sleep in (<see cref="Hosting.Beds"/>): free, or already one of theirs.</summary>
        private static List<Building_Bed> Beds(Mission m, Map map) => Hosting.Beds(map, o => Ours(m, o), m.x);

        protected override bool Possible(Map map)
        {
            Fled(out var f);
            return f != null || MissionPeoples.Grateful() != null;
        }

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            string fled = Fled(out var f);
            m.faction = f ?? MissionPeoples.Grateful();
            if (m.faction == null) return false;
            m.variant = fled ?? "fire";
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            int n = Rand.RangeInclusive(3, 4) + (IncidentWorker_Happening.Colonists(map).Count >= 6 ? 1 : 0);
            for (int i = 0; i < n; i++)
            {
                var p = Make(m.faction, i == 0 ? new FloatRange(30f, 60f) : new FloatRange(16f, 70f));
                if (p == null) continue;
                if (m.x == null) m.x = p;
                else m.group.Add(p);
            }
            if (m.x == null) return false;
            n = Guests(m).Count();
            m.target = n;
            m.name = m.x.LabelShort;
            m.tale = n.ToString();
            m.wait = Rand.Range(0.6f, 1.2f);
            float stay = Rand.Range(3f, 4f);
            m.days = m.wait + stay;
            // Eating whenever they're hungry (and before bed when they would sleep hungry), each eats two or three meals a day.
            m.goals.Add(new MissionGoal { what = "meals", count = Mathf.CeilToInt(2.5f * n * stay) });
            m.goodwill = 15;
            m.penalty = 5;
            Fill(m, ctx);
            ctx.stranger = m.x;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            return base.Start(m, map);
        }

        public override void Check(Mission m, Map map, int now)
        {
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            if (m.stage >= 1 && Harmed(m))
            {
                Missions.Goodwill(m, -m.penalty * 2);
                Missions.Finish(m, MissionState.Failed, "harmed", null);
                return;
            }
            if (m.stage == 0)
            {
                Remind(m, map, now);
                if (now < m.due) return;
                var spot = Hosting.Spot(Beds(m, map), map);
                if (!Arrive(m, map, spot.IsValid ? spot : GatheringSpot(map), (m.deadline - now) / (float)GenDate.TicksPerHour))
                {
                    Missions.Finish(m, MissionState.Failed, "ended", null);
                    return;
                }
                m.stage = 1;
                m.lastLook = now;
                m.lacks = 0;
                string line = Line(More?.ready, m, map, null, TextUtil.DaysString(Mathf.Max(0, m.deadline - now)));
                // At once, not in the evening's news: their days are counted from now on.
                if (!line.NullOrEmpty()) Messages.Message(line, Targets(m), MessageTypeDefOf.NeutralEvent, false);
                return;
            }
            var here = Here(m, map).ToList();
            // They eat at the colony's table and sleep in its beds; what they went without is kept for the day.
            m.lacks |= (int)Tend(m, map);
            if (now - m.lastLook >= GenDate.TicksPerDay)
            {
                m.lastLook += GenDate.TicksPerDay;
                Day(m, map, here);
            }
            // All of them gone before the end (a fight drove them off): judged on the days there were.
            if (now < m.deadline && here.Count > 0) return;
            // The last part of a day counts too: half a day or more of it, or any part of it they went without in.
            if (m.lacks != 0 || now - m.lastLook >= GenDate.TicksPerDay / 2) Day(m, map, here);
            if (m.tally == 0 && m.misses == 0)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            var look = Targets(m);
            if (m.misses == 0 && m.tally > 0)
            {
                Missions.Goodwill(m, m.goodwill);
                FactionMemory.Note(m.faction, "refugees", m.place);
                var thought = StorytellerEvents.Thought("Council_Mem_RefugeesThrough");
                if (thought != null)
                    foreach (var p in StorytellerEvents.Adults(map).ToList())
                        CouncilGameComponent.GiveThought(p, thought);
                SeeOff(m, map);
                Missions.Finish(m, MissionState.Done, "seen", null, look);
            }
            else if (m.tally >= m.misses && m.tally > 0)
            {
                Missions.Goodwill(m, m.goodwill / 3);
                FactionMemory.Note(m.faction, "refugees", m.place, 8);
                SeeOff(m, map);
                Missions.Finish(m, MissionState.Done, "partly", null, look, LetterDefOf.NeutralEvent);
            }
            else
            {
                Missions.Goodwill(m, -m.penalty);
                FactionMemory.Note(m.faction, "unfed", m.place);
                SeeOff(m, map);
                Missions.Finish(m, MissionState.Failed, "hungry", null, look);
            }
        }

        /// <summary>
        /// A day of their stay: fed (nobody was hungry with nothing of the colony's to eat) and sheltered (everyone had a
        /// bed when they lay down, and there are still enough). Told when either falls short.
        /// </summary>
        private void Day(Mission m, Map map, List<Pawn> here)
        {
            var lack = (Hosting.Lack)m.lacks;
            m.lacks = 0;
            if (here.Count == 0) return;
            bool fed = (lack & Hosting.Lack.Food) == 0;
            bool roofed = (lack & Hosting.Lack.Bed) == 0 && Hosting.Slots(Beds(m, map)) >= here.Count;
            if (fed && roofed)
            {
                m.tally++;
                return;
            }
            m.misses++;
            string key = !fed && !roofed ? "CC_Mission_Refugees_Neither" : !fed ? "CC_Mission_Refugees_Hungry" : "CC_Mission_Refugees_NoRoof";
            Messages.Message(key.Translate(m.place ?? m.faction?.Name ?? "?").Resolve(), new LookTargets(here), MessageTypeDefOf.NegativeEvent, false);
        }

        /// <summary>The plainest meal first (they ask to be fed, not feasted), else raw food from the stores.</summary>
        protected override Thing FoodFor(Mission m, Map map, Pawn p) => Hosting.Meal(p, false) ?? Hosting.Raw(p);

        /// <summary>The bed they had last night; else a bed to themselves (single ones first, then the nearest); else a free side of another refugee's.</summary>
        protected override Building_Bed BedFor(Mission m, Map map, Pawn p)
        {
            var beds = Beds(m, map);
            var own = p.ownership?.OwnedBed;
            if (own != null && beds.Contains(own)) return own;
            return beds.Where(b => b.OwnersForReading.Count == 0).OrderBy(b => b.SleepingSlotsCount).ThenBy(b => b.Position.DistanceToSquared(p.Position)).FirstOrDefault()
                   ?? beds.Where(b => b.OwnersForReading.Count < b.SleepingSlotsCount && Hosting.CanShare(b, p)).OrderBy(b => b.Position.DistanceToSquared(p.Position)).FirstOrDefault();
        }

        private static IntVec3? GatheringSpot(Map map)
        {
            var spot = map.listerBuildings.allBuildingsColonist.FirstOrDefault(b => b.def.defName == "PartySpot" || b.def.defName == "Campfire");
            return spot?.Position;
        }

        public override string Status(Mission m)
        {
            int now = Find.TickManager.TicksGame;
            var map = m.Map;
            int beds = map == null ? 0 : Hosting.Slots(Beds(m, map));
            string roof = "CC_Mission_Refugees_Beds".Translate(beds, m.target).Resolve();
            if (m.stage == 0) return "CC_Mission_Refugees_Coming".Translate(m.target, TextUtil.DaysString(Mathf.Max(0, m.due - now))).Resolve() + " " + roof;
            return "CC_Mission_Refugees_Staying".Translate(m.tally, m.misses).Resolve() + " " + roof + " " + MealsText(m);
        }
    }
}
