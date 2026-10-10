using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>"A room of my own." For colonists in barracks, sharing with a non-partner, or without a bed.</summary>
    public class WishWorker_OwnRoom : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || RoomUtil.PrivateRoom(pawn) != null) yield break;
            var bedDef = Avail.BestBuildable(map, Avail.IsSingleBed, "Bed", "Bedroll");
            if (bedDef == null) yield break;

            var bed = pawn.ownership?.OwnedBed;
            float w = 0f;
            string reason = null;
            if (bed == null)
            {
                w = 2.5f;
                reason = "CC_Reason_NoBed".Translate(pawn.Named("PAWN"));
            }
            else
            {
                var room = bed.GetRoom();
                if (room == null || room.PsychologicallyOutdoors)
                {
                    w = 2f;
                    reason = "CC_Reason_SleepsOutside".Translate(pawn.Named("PAWN"));
                }
                else if (room.Role == RoomRoleDefOf.Barracks || room.Owners.Count() > 1)
                {
                    w = 1f;
                    reason = "CC_Reason_Barracks".Translate(pawn.Named("PAWN"));
                    if (HasMemory(pawn, ThoughtDefOf.SleptInBarracks)) w += 0.8f;
                }
            }
            if (w <= 0f) yield break;
            if (HasTrait(pawn, TraitDefOf.Ascetic)) w *= 0.2f;
            var c = Candidate(w, reason);
            c.thingDef = bedDef;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => RoomUtil.PrivateRoom(wish.Beneficiary) != null;

        public override string ProgressText(Wish wish)
        {
            var room = RoomUtil.BedRoom(wish.Beneficiary);
            if (room == null) return "CC_Progress_NoBed".Translate();
            return "CC_Progress_SleepsIn".Translate(room.GetRoomRoleLabel());
        }
    }

    /// <summary>A more impressive bedroom. Greedy, jealous, or colonists with high expectations.</summary>
    public class WishWorker_NicerBedroom : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var room = RoomUtil.PrivateRoom(pawn);
            if (room == null || HasTrait(pawn, TraitDefOf.Ascetic)) yield break;
            float imp = RoomUtil.Impressiveness(room);
            float target = RoomUtil.NextStageThreshold(RoomStatDefOf.Impressiveness, imp);
            float w = 0f;
            string reason = null;

            if (HasTrait(pawn, TraitDefOf.Jealous))
            {
                float best = 0f;
                foreach (var other in pawn.Map.mapPawns.FreeColonistsSpawned)
                {
                    if (other == pawn) continue;
                    var r = RoomUtil.PrivateRoom(other);
                    if (r != null) best = UnityEngine.Mathf.Max(best, RoomUtil.Impressiveness(r));
                }
                if (best > imp + 1f)
                {
                    target = UnityEngine.Mathf.Ceil(best);
                    w = 2f;
                    reason = "CC_Reason_Jealous".Translate(pawn.Named("PAWN"));
                }
            }
            if (w <= 0f && target > 0f)
            {
                if (HasTrait(pawn, TraitDefOf.Greedy))
                {
                    w = 2f;
                    reason = "CC_Reason_Greedy".Translate(pawn.Named("PAWN"));
                }
                else if (RoomUtil.ExpectationOrder(pawn) >= 4 && imp < 50f)
                {
                    w = 0.8f;
                    reason = "CC_Reason_HighExpectations".Translate(pawn.Named("PAWN"));
                }
            }
            if (w <= 0f || target <= 0f) yield break;

            var c = Candidate(w, reason);
            c.targetValue = target;
            c.baseline = imp;
            c.subjectLabel = RoomUtil.StageLabel(RoomStatDefOf.Impressiveness, target);
            yield return c;
        }

        public override bool IsFulfilled(Wish wish)
        {
            var room = RoomUtil.PrivateRoom(wish.Beneficiary);
            return room != null && RoomUtil.Impressiveness(room) >= wish.targetValue;
        }

        public override bool StillValid(Wish wish) => RoomUtil.PrivateRoom(wish.Beneficiary) != null;

        public override string TrySelfHelp(Wish wish) => PlanInOwnRoom(wish.Beneficiary, wish.thingDef) ? "CC_SelfHelp_Plan" : null;

        public override string ProgressText(Wish wish)
        {
            float imp = RoomUtil.Impressiveness(RoomUtil.PrivateRoom(wish.Beneficiary));
            return "CC_Progress_Impressiveness".Translate(imp.ToString("0"), wish.targetValue.ToString("0"));
        }

        public override float Progress(Wish wish)
        {
            float imp = RoomUtil.Impressiveness(RoomUtil.PrivateRoom(wish.Beneficiary));
            if (wish.targetValue <= wish.baseline) return imp >= wish.targetValue ? 1f : 0f;
            return UnityEngine.Mathf.Clamp01((imp - wish.baseline) / (wish.targetValue - wish.baseline));
        }
    }

    /// <summary>A bigger room for colonists stuck in a cramped one.</summary>
    public class WishWorker_Spacious : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var room = RoomUtil.PrivateRoom(pawn);
            if (room == null || HasTrait(pawn, TraitDefOf.Ascetic) || HasTrait(pawn, TraitDefOf.Undergrounder)) yield break;
            float space = RoomUtil.Space(room);
            int stage = RoomStatDefOf.Space.GetScoreStageIndex(space);
            if (stage > 1) yield break;
            float target = RoomUtil.NextStageThreshold(RoomStatDefOf.Space, space);
            if (target <= 0f) yield break;
            var c = Candidate(stage == 0 ? 1.6f : 0.7f, "CC_Reason_Cramped".Translate(pawn.Named("PAWN"), RoomUtil.StageLabel(RoomStatDefOf.Space, space)));
            c.targetValue = target;
            c.baseline = space;
            c.subjectLabel = RoomUtil.StageLabel(RoomStatDefOf.Space, target);
            yield return c;
        }

        public override bool IsFulfilled(Wish wish)
        {
            var room = RoomUtil.PrivateRoom(wish.Beneficiary);
            return room != null && RoomUtil.Space(room) >= wish.targetValue;
        }

        public override bool StillValid(Wish wish) => RoomUtil.PrivateRoom(wish.Beneficiary) != null;

        public override string TrySelfHelp(Wish wish) => PlanInOwnRoom(wish.Beneficiary, wish.thingDef) ? "CC_SelfHelp_Plan" : null;

        public override string ProgressText(Wish wish) =>
            "CC_Progress_Space".Translate(RoomUtil.Space(RoomUtil.PrivateRoom(wish.Beneficiary)).ToString("0"), wish.targetValue.ToString("0"));
    }

    /// <summary>Partners who sleep apart want to share a bed.</summary>
    public class WishWorker_ShareBed : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !pawn.DevelopmentalStage.Adult() || pawn.relations == null) yield break;
            var doubleBed = Avail.BestBuildable(map, Avail.IsDoubleBed, "DoubleBed", "BedrollDouble");
            if (doubleBed == null) yield break;

            foreach (var rel in pawn.relations.DirectRelations)
            {
                if (rel.def != PawnRelationDefOf.Lover && rel.def != PawnRelationDefOf.Spouse && rel.def != PawnRelationDefOf.Fiance) continue;
                var partner = rel.otherPawn;
                if (partner == null || partner.Dead || !partner.IsFreeColonist || partner.MapHeld != map) continue;
                var myBed = pawn.ownership?.OwnedBed;
                var theirBed = partner.ownership?.OwnedBed;
                if (myBed != null && myBed == theirBed) continue;
                var c = Candidate(2f, "CC_Reason_SleepApart".Translate(pawn.Named("PAWN"), partner.Named("OTHER")));
                c.other = partner;
                c.thingDef = doubleBed;
                c.subjectKey = PairKey(pawn, partner);
                yield return c;
            }
        }

        public static string PairKey(Pawn a, Pawn b) =>
            string.CompareOrdinal(a.ThingID, b.ThingID) < 0 ? a.ThingID + "+" + b.ThingID : b.ThingID + "+" + a.ThingID;

        public override bool IsFulfilled(Wish wish)
        {
            var bed = wish.Beneficiary.ownership?.OwnedBed;
            return bed != null && wish.other != null && wish.other.ownership?.OwnedBed == bed;
        }

        public override bool StillValid(Wish wish)
        {
            var o = wish.other;
            if (o == null || o.Dead || !o.IsFreeColonist) return false;
            return LovePartnerRelationUtility.LovePartnerRelationExists(wish.Beneficiary, o);
        }

        /// <summary>If the partner already has a bed for two, they simply move in.</summary>
        public override string TrySelfHelp(Wish wish)
        {
            var bed = wish.other?.ownership?.OwnedBed;
            var me = wish.Beneficiary;
            if (bed == null || me.ownership == null || bed.Medical || !bed.AnyUnownedSleepingSlot) return null;
            me.ownership.ClaimBedIfNonMedical(bed);
            return me.ownership.OwnedBed == bed ? "CC_SelfHelp_ShareBed" : null;
        }
    }

    /// <summary>Small furniture for a private bedroom: an end table, a dresser.</summary>
    public class WishWorker_BedroomFurniture : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var room = RoomUtil.PrivateRoom(pawn);
            var map = HomeMap(pawn);
            if (room == null || map == null || HasTrait(pawn, TraitDefOf.Ascetic)) yield break;
            if (RoomUtil.ExpectationOrder(pawn) < 2 && !HasTrait(pawn, TraitDefOf.Greedy)) yield break;

            if (!RoomUtil.RoomContains(room, Avail.IsEndTable))
            {
                var d = Avail.BestBuildable(map, Avail.IsEndTable, "EndTable");
                if (d != null)
                {
                    var c = Candidate(0.8f, "CC_Reason_BareRoom".Translate(pawn.Named("PAWN")));
                    c.thingDef = d;
                    c.subjectKey = "endtable";
                    yield return c;
                }
            }
            if (!RoomUtil.RoomContains(room, Avail.IsDresser))
            {
                var d = Avail.BestBuildable(map, Avail.IsDresser, "Dresser");
                if (d != null)
                {
                    var c = Candidate(0.6f, "CC_Reason_BareRoom".Translate(pawn.Named("PAWN")));
                    c.thingDef = d;
                    c.subjectKey = "dresser";
                    yield return c;
                }
            }
        }

        public override bool IsFulfilled(Wish wish)
        {
            var room = RoomUtil.PrivateRoom(wish.Beneficiary);
            if (room == null) return false;
            return wish.subjectKey == "dresser" ? RoomUtil.RoomContains(room, Avail.IsDresser) : RoomUtil.RoomContains(room, Avail.IsEndTable);
        }

        public override bool StillValid(Wish wish) => RoomUtil.PrivateRoom(wish.Beneficiary) != null;

        public override string TrySelfHelp(Wish wish) => PlanInOwnRoom(wish.Beneficiary, wish.thingDef) ? "CC_SelfHelp_Plan" : null;
    }

    /// <summary>Light in the bedroom: night owls, and colonists who've been stuck in the dark.</summary>
    public class WishWorker_Light : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var room = RoomUtil.PrivateRoom(pawn);
            var map = HomeMap(pawn);
            if (room == null || map == null) yield break;
            if (RoomUtil.RoomContains(room, Avail.IsLight)) yield break;
            var nightOwl = DefDatabase<TraitDef>.GetNamedSilentFail("NightOwl");
            bool dark = RoomUtil.HasActiveThought(pawn, "EnvironmentDark");
            if (!dark && !HasTrait(pawn, nightOwl) && RoomUtil.ExpectationOrder(pawn) < 3) yield break;
            var light = Avail.BestBuildable(map, Avail.IsLight, "StandingLamp", "TorchLamp");
            if (light == null) yield break;
            var c = Candidate(dark ? 1.4f : 0.7f, HasTrait(pawn, nightOwl)
                ? "CC_Reason_NightOwl".Translate(pawn.Named("PAWN"))
                : "CC_Reason_Dark".Translate(pawn.Named("PAWN")));
            c.thingDef = light;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => RoomUtil.RoomContains(RoomUtil.PrivateRoom(wish.Beneficiary), Avail.IsLight);

        public override bool StillValid(Wish wish) => RoomUtil.PrivateRoom(wish.Beneficiary) != null;

        public override string TrySelfHelp(Wish wish) => PlanInOwnRoom(wish.Beneficiary, wish.thingDef) ? "CC_SelfHelp_Plan" : null;
    }

    /// <summary>Slept in the cold or the heat: a heat source (or cooler) in the bedroom.</summary>
    public partial class WishWorker_ComfortTemperature : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            var room = RoomUtil.BedRoom(pawn);
            if (map == null || room == null || room.PsychologicallyOutdoors) yield break;
            bool cold = HasMemory(pawn, ThoughtDefOf.SleptInCold);
            bool hot = HasMemory(pawn, ThoughtDefOf.SleptInHeat);
            if (!cold && !hot) yield break;
            if (cold && !RoomUtil.RoomContains(room, Avail.IsHeater))
            {
                var heater = Avail.BestBuildable(map, Avail.IsHeater, "Heater", "Brazier", "Campfire");
                if (heater != null)
                {
                    var c = Candidate(1.5f, "CC_Reason_SleptCold".Translate(pawn.Named("PAWN")));
                    c.thingDef = heater;
                    c.subjectKey = "cold";
                    c.subjectLabel = "CC_Warmth".Translate();
                    yield return c;
                }
            }
            if (hot && !RoomUtil.RoomContains(room, Avail.IsCooler))
            {
                var cooler = Avail.BestBuildable(map, Avail.IsCooler, "Cooler", "PassiveCooler");
                if (cooler != null)
                {
                    var c = Candidate(1.5f, "CC_Reason_SleptHot".Translate(pawn.Named("PAWN")));
                    c.thingDef = cooler;
                    c.subjectKey = "hot";
                    c.subjectLabel = "CC_Coolness".Translate();
                    yield return c;
                }
            }
        }

        public override bool IsFulfilled(Wish wish)
        {
            var room = RoomUtil.BedRoom(wish.Beneficiary);
            if (room == null) return false;
            bool hot = wish.subjectKey == "hot";
            if (RoomUtil.RoomContains(room, hot ? (System.Func<ThingDef, bool>)Avail.IsCooler : Avail.IsHeater)) return true;
            // Otherwise accept a comfortable bedroom at night, when it matters.
            int hour = GenLocalDate.HourOfDay(wish.Beneficiary.MapHeld);
            if (hour >= 6 && hour < 22) return false;
            var range = wish.Beneficiary.ComfortableTemperatureRange();
            return room.Temperature >= range.min && room.Temperature <= range.max;
        }
    }

    public partial class WishWorker_ComfortTemperature
    {
        public override string TrySelfHelp(Wish wish) => PlanInRoom(RoomUtil.BedRoom(wish.Beneficiary), wish.Beneficiary, wish.thingDef) ? "CC_SelfHelp_Plan" : null;
    }

    /// <summary>Somewhere proper to eat.</summary>
    public class WishWorker_DiningTable : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !HasMemory(pawn, ThoughtDefOf.AteWithoutTable)) yield break;
            if (HasDiningSpot(map)) yield break;
            var table = Avail.BestBuildable(map, Avail.IsEatingTable, "Table2x2c", "Table1x2c", "Table2x4c");
            if (table == null) yield break;
            var c = Candidate(1.6f, "CC_Reason_AteWithoutTable".Translate(pawn.Named("PAWN")));
            c.thingDef = table;
            c.subjectKey = "colony";
            yield return c;
        }

        public static bool HasDiningSpot(Map map)
        {
            foreach (var b in Avail.ColonyBuildings(map, Avail.IsEatingTable))
            {
                var room = b.GetRoom();
                if (room != null && !room.PsychologicallyOutdoors) return true;
            }
            return false;
        }

        public override bool IsFulfilled(Wish wish) => HasDiningSpot(wish.Beneficiary.MapHeld);
    }

    /// <summary>Recreation variety: something new to do.</summary>
    public class WishWorker_RecreationVariety : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || pawn.needs?.joy == null) yield break;
            int kinds = Avail.JoyKindsOnMap(map);
            int needed = RoomUtil.Expectation(pawn)?.joyKindsNeeded ?? 3;
            bool bored = pawn.needs.joy.CurLevelPercentage < 0.35f;
            if (kinds >= needed && !bored) yield break;
            var present = new HashSet<JoyKindDef>(JoyUtility.JoyKindsOnMapTempList(map));
            var giver = Avail.BestBuildable(map, d => Avail.IsJoyGiver(d) && !present.Contains(d.building.joyKind),
                "HorseshoesPin", "ChessTable", "BilliardsTable", "TubeTelevision", "PokerTable");
            if (giver == null) yield break;
            var c = Candidate(kinds < needed ? 1.5f : 0.8f, "CC_Reason_Bored".Translate(pawn.Named("PAWN"), kinds, needed));
            c.thingDef = giver;
            c.baseline = kinds;
            c.subjectKey = "colony";
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => Avail.JoyKindsOnMap(wish.Beneficiary.MapHeld) > wish.baseline;

        public override string ProgressText(Wish wish) =>
            "CC_Progress_JoyKinds".Translate(Avail.JoyKindsOnMap(wish.Beneficiary.MapHeld), (wish.baseline + 1).ToString("0"));
    }

    /// <summary>Something green: a potted plant in the bedroom.</summary>
    public class WishWorker_Greenery : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var room = RoomUtil.PrivateRoom(pawn);
            var map = HomeMap(pawn);
            if (room == null || map == null || HasTrait(pawn, TraitDefOf.Ascetic)) yield break;
            var plants = pawn.skills?.GetSkill(SkillDefOf.Plants);
            if (plants == null || plants.TotallyDisabled || plants.passion == Passion.None) yield break;
            if (RoomUtil.RoomContains(room, Avail.IsPlantPot)) yield break;
            var pot = Avail.BestBuildable(map, Avail.IsPlantPot, "PlantPot");
            if (pot == null) yield break;
            var c = Candidate(plants.passion == Passion.Major ? 1f : 0.5f, "CC_Reason_Greenery".Translate(pawn.Named("PAWN")));
            c.thingDef = pot;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => RoomUtil.RoomContains(RoomUtil.PrivateRoom(wish.Beneficiary), Avail.IsPlantPot);

        public override bool StillValid(Wish wish) => RoomUtil.PrivateRoom(wish.Beneficiary) != null;

        public override string TrySelfHelp(Wish wish) => PlanInOwnRoom(wish.Beneficiary, wish.thingDef) ? "CC_SelfHelp_Plan" : null;
    }
}
