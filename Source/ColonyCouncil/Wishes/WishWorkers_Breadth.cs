using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Sick, and sleeping where they always sleep: they'd like a bed set aside for the sick, where someone can look in on
    /// them. Any bed counts once it's marked for medical use, so it's a small thing to grant.
    /// </summary>
    public class WishWorker_Sickbed : WishWorker
    {
        /// <summary>
        /// Unwell right now: one of Rimfolk's ailments (not a cough left over from one), or one of the game's illnesses
        /// (flu, plague, malaria, food poisoning and the like make the game's own "sick" mood).
        /// </summary>
        public static bool Ailing(Pawn p)
        {
            var hediffs = p?.health?.hediffSet?.hediffs;
            if (hediffs == null) return false;
            for (int i = 0; i < hediffs.Count; i++)
            {
                var h = hediffs[i];
                if (h?.def == null || !h.Visible) continue;
                var info = AilmentTracker.InfoOf(h.def);
                if (info != null)
                {
                    if (!info.lingering) return true;
                    continue;
                }
                if (h.def.makesSickThought) return true;
            }
            return false;
        }

        /// <summary>A bed for people (not prisoners) marked for medical use.</summary>
        public static bool HasSickbed(Map map)
        {
            if (map == null) return false;
            var list = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < list.Count; i++)
                if (list[i] is Building_Bed bed && bed.Spawned && bed.Medical && !bed.ForPrisoners && bed.def.building != null && bed.def.building.bed_humanlike) return true;
            return false;
        }

        private static bool AnyBed(Map map) =>
            map.listerBuildings.allBuildingsColonist.Any(b => b is Building_Bed bed && bed.def.building != null && bed.def.building.bed_humanlike && !bed.ForPrisoners)
            || Avail.BestBuildable(map, Avail.IsHumanBed, "Bed") != null;

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !Ailing(pawn) || HasSickbed(map) || !AnyBed(map)) yield break;
            var c = Candidate(1.4f, "CC_Reason_Sickbed".Translate(pawn.Named("PAWN")));
            c.subjectKey = "colony";
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => HasSickbed(wish.Beneficiary.MapHeld);

        /// <summary>Well again before anyone saw to it: there's nothing left to ask for.</summary>
        public override bool StillValid(Wish wish) => Ailing(wish.Beneficiary) || HasSickbed(wish.Beneficiary.MapHeld);
    }

    /// <summary>
    /// Strangers are camped on the map, and someone wants to go and sit at their fire for an evening: news, songs, other
    /// faces. It comes true once they've talked with the campers a couple of times (the camp keeps count of who visits).
    /// </summary>
    public class WishWorker_CampEvening : WishWorker
    {
        private static Camp CampOn(Map map)
        {
            var camp = CampTracker.Instance?.camp;
            return camp != null && map != null && camp.map == map && camp.Settled && camp.CanTalk ? camp : null;
        }

        private static int Warmth(Camp camp, Pawn p) => camp?.warmth != null && p != null && camp.warmth.TryGetValue(p, out int w) ? w : 0;

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            var camp = CampOn(map);
            if (camp == null || pawn.DevelopmentalStage.Juvenile() || camp.Here.Count == 0) yield break;
            if (pawn.story?.traits?.allTraits?.Any(t => t.def.defName == "Recluse") ?? false) yield break;
            bool social = People.Passionate(pawn.skills?.GetSkill(SkillDefOf.Social));
            bool kind = HasTrait(pawn, TraitDefOf.Kind);
            bool lacking = Lacks(pawn, HeartNeed.Belonging) || Lacks(pawn, HeartNeed.Freedom);
            if (!social && !kind && !lacking) yield break;
            int w = Warmth(camp, pawn);
            if (w >= 6) yield break;
            var c = Candidate(social || lacking ? 1.3f : 0.8f, "CC_Reason_CampEvening".Translate(pawn.Named("PAWN"), camp.Label.Named("CAMP")));
            c.subjectLabel = camp.Label;
            c.subjectKey = "camp" + camp.id;
            c.baseline = w;
            c.targetValue = w + 2;
            yield return c;
        }

        /// <summary>The heart's need they lack most is this one.</summary>
        public static bool Lacks(Pawn p, HeartNeed need)
        {
            var lack = Heart.LowestNeed(Heart.Of(p), out _);
            return lack != null && lack.need == need;
        }

        private static Camp CampOf(Wish wish)
        {
            var camp = CampOn(wish.Beneficiary.MapHeld);
            return camp != null && "camp" + camp.id == wish.subjectKey ? camp : null;
        }

        public override bool IsFulfilled(Wish wish) => Warmth(CampOf(wish), wish.Beneficiary) >= wish.targetValue;

        /// <summary>The camp packed up and left (or turned hostile) before they got there.</summary>
        public override bool StillValid(Wish wish) => CampOf(wish) != null;

        public override string ProgressText(Wish wish) =>
            "CC_Progress_CampEvening".Translate(Mathf.Max(0, Warmth(CampOf(wish), wish.Beneficiary) - (int)wish.baseline), (wish.targetValue - wish.baseline).ToString("0")).ToString();

        public override LookTargets LookAt(Wish wish)
        {
            var camp = CampOf(wish);
            return camp != null && camp.FireCell.IsValid ? new LookTargets(wish.Beneficiary, new TargetInfo(camp.FireCell, camp.map)) : base.LookAt(wish);
        }

        public override InspirationDef InspirationFor(Wish wish) => InspirationDefOf.Inspired_Trade;
    }

    /// <summary>
    /// Hospitality: visitors have come, and the colony has no bed set aside for guests. A kind or sociable colonist would
    /// like one, so nobody who comes in peace sleeps on the floor. Any bed switched to "for guests" will do.
    /// </summary>
    public class WishWorker_GuestBed : WishWorker
    {
        public const string Mod = "orion.hospitality";

        public static bool HasGuestBed(Map map) =>
            map != null && map.listerBuildings.allBuildingsColonist.Any(b => b is Building_Bed && b.Spawned && b.GetType().Name == "Building_GuestBed");

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || pawn.DevelopmentalStage.Juvenile() || !People.ModActive(Mod) || HasGuestBed(map)) yield break;
            bool kind = HasTrait(pawn, TraitDefOf.Kind);
            bool social = People.Passionate(pawn.skills?.GetSkill(SkillDefOf.Social));
            if (!kind && !social) yield break;
            if (Avail.BestBuildable(map, Avail.IsHumanBed, "Bed") == null && !map.listerBuildings.allBuildingsColonist.Any(b => b is Building_Bed bed && Avail.IsHumanBed(bed.def))) yield break;
            bool visitors = Strangers.On(map, StrangerKind.Visitor).Any();
            var c = Candidate(visitors ? 1.4f : 0.4f, (visitors ? "CC_Reason_GuestBed_Visitors" : "CC_Reason_GuestBed").Translate(pawn.Named("PAWN")));
            c.subjectKey = "colony";
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => HasGuestBed(wish.Beneficiary.MapHeld);

        /// <summary>Hospitality was removed: a guest bed can't be had any more.</summary>
        public override bool StillObtainable(Wish wish) => base.StillObtainable(wish) && People.ModActive(Mod);

        public override InspirationDef InspirationFor(Wish wish) => InspirationDefOf.Inspired_Recruitment;
    }

    /// <summary>
    /// Someone has something to put to the council, and it hasn't sat down together in a long while (or they missed the
    /// last meeting). Promising it calls a meeting; it comes true once a meeting has been held since they asked.
    /// </summary>
    public class WishWorker_Meeting : WishWorker
    {
        private static AmbitionTracker Tracker => CouncilGameComponent.Instance?.ambitions;

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var t = Tracker;
            var comp = CouncilGameComponent.Instance;
            var map = HomeMap(pawn);
            if (t == null || comp == null || map == null || !ColonyCouncilMod.Settings.enableAmbitions || pawn.DevelopmentalStage.Juvenile()) yield break;
            if (t.active != null || t.meeting != null) yield break;
            int now = Find.TickManager.TicksGame;
            if (now - t.lastMeetingTick < GenDate.TicksPerDay * 8) yield break;
            // One is due soon anyway.
            if (t.nextMeetingTick >= 0 && t.nextMeetingTick - now < GenDate.TicksPerDay * 2) yield break;
            if (map.mapPawns.FreeColonistsSpawned.Count(CouncilGameComponent.CanVoiceWishes) < 2) yield break;
            bool missed = HasMemory(pawn, "Council_MissedMeeting") || HasMemory(pawn, "Council_DecidedWithoutMe");
            bool social = People.Passionate(pawn.skills?.GetSkill(SkillDefOf.Social));
            bool officer = ColonyCouncilMod.Settings.offices && comp.offices.OfficeOf(pawn) != null;
            if (!missed && !social && !officer) yield break;
            var c = Candidate(missed ? 1.5f : officer ? 1f : 0.7f, (missed ? "CC_Reason_MeetingMissed" : "CC_Reason_Meeting").Translate(pawn.Named("PAWN")));
            c.subjectKey = "colony";
            yield return c;
        }

        public override bool IsFulfilled(Wish wish)
        {
            var t = Tracker;
            return t != null && t.lastMeetingTick > wish.createdTick && t.meeting == null;
        }

        public override bool StillValid(Wish wish) => ColonyCouncilMod.Settings.enableAmbitions;

        /// <summary>Promised: the council calls the meeting now, as it would from the Ambition page.</summary>
        public override string OnPromised(Wish wish)
        {
            var t = Tracker;
            var comp = CouncilGameComponent.Instance;
            if (t == null || comp == null || !t.CanCallMeetingNow(out _)) return null;
            t.HoldMeeting(comp, true);
            return t.meeting != null ? "CC_Promise_Meeting" : null;
        }
    }

    /// <summary>
    /// The colony eats in shifts or on its feet: someone wants a table where everyone fits at once. Counts the chairs
    /// (anything to sit on) that stand beside an eating table indoors, against the people who eat.
    /// </summary>
    public class WishWorker_SeatsForAll : WishWorker
    {
        private static readonly Dictionary<int, (int hour, int seats)> seatCache = new Dictionary<int, (int, int)>();
        private static Game seatGame;

        /// <summary>Seats at tables indoors, remembered for the in-game hour.</summary>
        public static int Seats(Map map)
        {
            if (map == null) return 0;
            if (seatGame != Current.Game)
            {
                seatCache.Clear();
                seatGame = Current.Game;
            }
            int hour = Find.TickManager.TicksGame / GenDate.TicksPerHour;
            if (seatCache.TryGetValue(map.uniqueID, out var c) && c.hour == hour) return c.seats;
            int n = 0;
            var list = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (!b.Spawned || !Avail.IsChair(b.def)) continue;
                var room = b.GetRoom();
                if (room == null || room.PsychologicallyOutdoors) continue;
                if (AtTable(b, map)) n++;
            }
            seatCache[map.uniqueID] = (hour, n);
            return n;
        }

        private static bool AtTable(Building chair, Map map)
        {
            foreach (var cell in GenAdj.CellsAdjacentCardinal(chair))
            {
                if (!cell.InBounds(map)) continue;
                var things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                    if (things[i] is Building t && Avail.IsEatingTable(t.def)) return true;
            }
            return false;
        }

        /// <summary>The colony's people who eat (babies and those who never eat aside).</summary>
        public static int Diners(Map map) =>
            map == null ? 0 : map.mapPawns.FreeColonistsSpawned.Count(p => !p.DevelopmentalStage.Baby() && p.needs?.food != null && !p.IsQuestLodger());

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || pawn.needs?.food == null || !WishWorker_DiningTable.HasDiningSpot(map)) yield break;
            int diners = Diners(map);
            if (diners < 3) yield break;
            int seats = Seats(map);
            if (seats >= diners) yield break;
            bool lacking = WishWorker_CampEvening.Lacks(pawn, HeartNeed.Belonging);
            bool kind = HasTrait(pawn, TraitDefOf.Kind);
            bool social = People.Passionate(pawn.skills?.GetSkill(SkillDefOf.Social));
            bool ateStanding = HasMemory(pawn, ThoughtDefOf.AteWithoutTable);
            if (!lacking && !kind && !social && !ateStanding) yield break;
            var chair = Avail.BestBuildable(map, Avail.IsChair, "DiningChair", "Stool");
            if (chair == null) yield break;
            var c = Candidate(lacking || ateStanding ? 1.3f : 0.8f, "CC_Reason_SeatsForAll".Translate(pawn.Named("PAWN"), seats.Named("SEATS"), diners.Named("COUNT")));
            c.thingDef = chair;
            c.subjectKey = "colony";
            c.baseline = seats;
            c.targetValue = diners;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => Seats(wish.Beneficiary.MapHeld) >= wish.targetValue;

        public override float Progress(Wish wish) => Mathf.Clamp01(Seats(wish.Beneficiary.MapHeld) / Mathf.Max(1f, wish.targetValue));

        public override string ProgressText(Wish wish) => "CC_Progress_Seats".Translate(Seats(wish.Beneficiary.MapHeld), wish.targetValue.ToString("0")).ToString();
    }

    /// <summary>
    /// The teacher's side of lessons: someone who knows a skill they love wants to pass it on to someone in the colony who
    /// is still learning it (a child, often). The old ask most. Promised, the council pairs them up; it comes true once
    /// the student has come on two levels.
    /// </summary>
    public class WishWorker_Apprentice : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            var social = CouncilGameComponent.Instance?.social;
            if (map == null || social == null || !ColonyCouncilMod.Settings.socialWeb || pawn.skills == null || pawn.DevelopmentalStage.Juvenile()) yield break;
            if (social.ties.Any(t => t.kind == TieKind.Mentor && t.a == pawn)) yield break;
            float age = Flavor.HumanAge(pawn);
            foreach (var s in pawn.skills.skills)
            {
                if (s.TotallyDisabled || !People.Passionate(s) || s.Level < 12) continue;
                var student = map.mapPawns.FreeColonistsSpawned
                    .Where(t => t != pawn && !t.IsQuestLodger() && People.HasInnerLife(t) && !t.DevelopmentalStage.Baby()
                                && t.skills?.GetSkill(s.def) is SkillRecord r && !r.TotallyDisabled && r.Level <= 7
                                && (People.Passionate(r) || t.DevelopmentalStage.Child())
                                && !social.ties.Any(x => x.kind == TieKind.Mentor && x.b == t)
                                && (pawn.relations == null || pawn.relations.OpinionOf(t) >= 0))
                    .OrderBy(t => t.DevelopmentalStage.Child() ? 0 : 1)
                    .ThenByDescending(t => People.PassionLevel(t.skills.GetSkill(s.def)))
                    .FirstOrDefault();
                if (student == null) continue;
                float w = (age >= 55f ? 1.6f : 0.6f) * (People.Burning(s) ? 1.3f : 1f) * (student.DevelopmentalStage.Child() ? 1.3f : 1f);
                var c = Candidate(w, "CC_Reason_Apprentice".Translate(pawn.Named("PAWN"), s.def.label.Named("SKILL"), student.Named("OTHER")));
                c.other = student;
                c.subjectLabel = s.def.label;
                c.subjectKey = s.def.defName;
                c.baseline = student.skills.GetSkill(s.def).Level;
                c.targetValue = c.baseline + 2;
                yield return c;
            }
        }

        private static SkillDef Skill(Wish wish) => DefDatabase<SkillDef>.GetNamedSilentFail(wish.subjectKey);

        private static int StudentLevel(Wish wish) => wish.other?.skills?.GetSkill(Skill(wish))?.Level ?? 0;

        public override bool IsFulfilled(Wish wish) => StudentLevel(wish) >= wish.targetValue;

        /// <summary>The student died or left.</summary>
        public override bool StillValid(Wish wish) => Skill(wish) != null && wish.other != null && !wish.other.Dead && wish.other.IsColonist;

        public override string ProgressText(Wish wish) => "CC_Progress_Apprentice".Translate(wish.other?.LabelShort ?? "?", StudentLevel(wish), wish.targetValue.ToString("0")).ToString();

        public override LookTargets LookAt(Wish wish) => wish.other != null && wish.other.Spawned ? new LookTargets(wish.Beneficiary, wish.other) : base.LookAt(wish);

        public override string OnPromised(Wish wish)
        {
            var social = CouncilGameComponent.Instance?.social;
            var skill = Skill(wish);
            if (social == null || skill == null || wish.other == null || wish.other.Dead || !wish.other.IsColonist) return null;
            return social.StartMentorship(wish.Beneficiary, wish.other, skill) != null ? "CC_Promise_Apprentice" : null;
        }
    }

    /// <summary>
    /// Odyssey: fish in the water on the map, fishing open to the colony, and no fishing spot marked yet. Someone who'd
    /// like an afternoon at the water's edge (and fish for supper) wants one. Comes true once there's a fishing zone.
    /// </summary>
    public class WishWorker_Fishing : WishWorker
    {
        public static bool HasFishingZone(Map map) => map?.zoneManager != null && map.zoneManager.AllZones.Any(z => z != null && z.GetType().Name == "Zone_Fishing");

        private static WorkTypeDef FishingWork => DefDatabase<WorkTypeDef>.GetNamedSilentFail("Fishing");

        private static Designator fishingDesignator;
        private static bool designatorLooked;

        /// <summary>The game's own fishing-zone tool, if it offers one (it knows the research and the DLC).</summary>
        private static Designator FishingDesignator
        {
            get
            {
                if (designatorLooked) return fishingDesignator;
                designatorLooked = true;
                try
                {
                    foreach (var cat in DefDatabase<DesignationCategoryDef>.AllDefsListForReading)
                    {
                        fishingDesignator = cat.AllResolvedDesignators.FirstOrDefault(d => d != null && d.GetType().Name == "Designator_ZoneAdd_Fishing");
                        if (fishingDesignator != null) break;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("fishing designator", e);
                }
                return fishingDesignator;
            }
        }

        /// <summary>Fishing is open to the colony (Odyssey, and its research if it needs one) and there are fish here.</summary>
        public static bool CanFish(Map map)
        {
            if (!ModsConfig.OdysseyActive || map == null || FishingWork == null) return false;
            try
            {
                var d = FishingDesignator;
                if (d != null && !d.Visible) return false;
                if (d == null && !Avail.ResearchDone("Fishing") && DefDatabase<ResearchProjectDef>.GetNamedSilentFail("Fishing") != null) return false;
                return map.waterBodyTracker != null && map.waterBodyTracker.AnyBodyContainsFish;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fishing water", e);
                return false;
            }
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            var work = FishingWork;
            if (map == null || work == null || pawn.DevelopmentalStage.Juvenile() || pawn.WorkTypeIsDisabled(work) || HasFishingZone(map) || !CanFish(map)) yield break;
            bool passion = work.relevantSkills != null && work.relevantSkills.Any(s => People.Passionate(pawn.skills?.GetSkill(s)));
            bool gourmand = HasTrait(pawn, DefDatabase<TraitDef>.GetNamedSilentFail("Gourmand")) || PeopleTags.Has(pawn, "gourmet");
            bool restless = WishWorker_CampEvening.Lacks(pawn, HeartNeed.Freedom);
            if (!passion && !gourmand && !restless) yield break;
            var c = Candidate(passion || restless ? 1.2f : 0.7f, "CC_Reason_Fishing".Translate(pawn.Named("PAWN")));
            c.subjectKey = "colony";
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => HasFishingZone(wish.Beneficiary.MapHeld);

        public override bool StillObtainable(Wish wish) => base.StillObtainable(wish) && (HasFishingZone(wish.Beneficiary.MapHeld) || CanFish(wish.Beneficiary.MapHeld));
    }

    /// <summary>
    /// A child wants an animal of the colony's own to look after. Any new animal (tamed, bought, born) will do; the wish
    /// only comes when there's something calm and small roaming the map that grown-ups could tame.
    /// </summary>
    public class WishWorker_ChildPet : WishWorker
    {
        private static bool Gentle(Pawn a)
        {
            var r = a.RaceProps;
            return r != null && r.Animal && !r.predator && r.baseBodySize <= 1.2f && r.manhunterOnTameFailChance <= 0.05f
                   && a.GetStatValue(StatDefOf.Wildness) < 0.75f && TameUtility.CanTame(a);
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !pawn.DevelopmentalStage.Child()) yield break;
            int owned = PokeWorldLink.OwnedAnimalCount(map);
            if (owned >= 6) yield break;
            var wild = map.mapPawns.AllPawnsSpawned
                .Where(a => a.Faction == null && !a.Downed && !a.InMentalState && Gentle(a) && !map.fogGrid.IsFogged(a.Position))
                .ToList();
            if (wild.Count == 0) yield break;
            var example = wild.RandomElement();
            var c = Candidate(owned == 0 ? 1.5f : 0.9f, "CC_Reason_ChildPet".Translate(pawn.Named("PAWN"), example.kindDef.GetLabelPlural().Named("ANIMALS")));
            c.baseline = owned;
            c.subjectLabel = example.kindDef.label;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => PokeWorldLink.OwnedAnimalCount(wish.Beneficiary.MapHeld) > wish.baseline;
    }

    /// <summary>
    /// Their own room still has a floor of bare earth, sand or mud: tracked in on every boot, cold in winter, a swamp in
    /// the rain. They want a real floor, any the colony can lay. Promised (or out of patience), the floor is planned.
    /// </summary>
    public class WishWorker_Floor : WishWorker
    {
        /// <summary>Natural ground that holds footprints or grows things: soil, sand, mud, marsh. Rough stone isn't.</summary>
        private static bool Bare(TerrainDef t) => t != null && t.natural && (t.IsSoil || t.takeFootprints || t.fertility > 0f);

        /// <summary>The share of the room's floor that is bare ground (0 with no room).</summary>
        public static float BareShare(Room room, Map map)
        {
            if (room == null || map == null || room.PsychologicallyOutdoors) return 0f;
            int bare = 0, all = 0;
            foreach (var c in room.Cells)
            {
                all++;
                if (Bare(c.GetTerrain(map))) bare++;
            }
            return all == 0 ? 0f : bare / (float)all;
        }

        /// <summary>The floor to lay: the cheapest the colony can build whose materials are within reach, else the cheapest.</summary>
        private static TerrainDef FloorFor(Map map)
        {
            var options = RoomPlanConfig.FloorOptions(map);
            return options.FirstOrDefault(f => Avail.MaterialsWithinReach(f, map)) ?? options.FirstOrDefault();
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            var room = RoomUtil.PrivateRoom(pawn);
            if (map == null || room == null || HasTrait(pawn, TraitDefOf.Ascetic)) yield break;
            float share = BareShare(room, map);
            if (share < 0.5f) yield break;
            var floor = FloorFor(map);
            if (floor == null) yield break;
            bool particular = Standards.Of(pawn) > 0.4f || HasTrait(pawn, TraitDefOf.Greedy);
            bool wet = map.weatherManager?.curWeather != null && map.weatherManager.curWeather.rainRate > 0.01f;
            // Other mods' tidy people (Vanilla Traits Expanded's neat) ask more often: see Council_People_Mods_Breadth.xml.
            float favor = def.wants?.Favor(pawn) ?? 1f;
            var c = Candidate((particular ? 1.2f : 0.6f) * (wet ? 1.5f : 1f) * favor, "CC_Reason_Floor".Translate(pawn.Named("PAWN")));
            c.subjectLabel = floor.label;
            c.subjectKey = pawn.ThingID;
            c.baseline = share;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => BareShare(RoomUtil.PrivateRoom(wish.Beneficiary), wish.Beneficiary.MapHeld) <= 0.1f;

        /// <summary>Their room is still theirs.</summary>
        public override bool StillValid(Wish wish) => wish.Beneficiary.MapHeld == null || RoomUtil.PrivateRoom(wish.Beneficiary) != null;

        public override string ProgressText(Wish wish) =>
            "CC_Progress_Floor".Translate((1f - BareShare(RoomUtil.PrivateRoom(wish.Beneficiary), wish.Beneficiary.MapHeld)).ToStringPercent()).ToString();

        /// <summary>Plans the floor over the bare ground in their room. True if anything was planned.</summary>
        private static bool PlanFloor(Pawn p)
        {
            var map = p?.MapHeld;
            var room = RoomUtil.PrivateRoom(p);
            var floor = map == null ? null : FloorFor(map);
            if (room == null || floor == null || room.PsychologicallyOutdoors) return false;
            var stuff = floor.MadeFromStuff ? Avail.BestStuff(floor, map) : null;
            int planned = 0;
            foreach (var c in room.Cells.ToList())
            {
                if (!Bare(c.GetTerrain(map))) continue;
                try
                {
                    if (TemplatePlanner.TryPlace(floor, c, Rot4.North, stuff, map)) planned++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("planning a floor", e);
                    break;
                }
            }
            return planned > 0;
        }

        public override string OnPromised(Wish wish) => PlanFloor(wish.Beneficiary) ? "CC_Promise_Floor" : null;

        public override string TrySelfHelp(Wish wish) => PlanFloor(wish.Beneficiary) ? "CC_SelfHelp_Floor" : null;
    }
}
