using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Revel's happenings under way (OwnHappeningsBook.revel): a long night, a vision walked to, a craving in the night, a binge
    /// seen, a night sat up with an addict, the horned reveller at the fire, and the god in a cup (RevelCup). Single records
    /// only, nothing that grows.
    /// </summary>
    public class RevelState : IExposable
    {
        /// <summary>His feast kept today at a home.</summary>
        public Map feastMap;
        public int feastDay = -1;

        /// <summary>The long night: where it gathers, until when, and the morning after it (who stayed up is read then).</summary>
        public Map nightMap;
        public IntVec3 nightAt = IntVec3.Invalid;
        public int nightUntil = -1, nightMorning = -1;
        public string nightStayed;

        /// <summary>A vision walked to: who, where, true or false, what it showed, the drug, until when.</summary>
        public Pawn seer;
        public IntVec3 visionAt = IntVec3.Invalid;
        public bool visionTrue, visionFromCup;
        public string visionSight, visionDrug;
        public int visionUntil = -1;

        /// <summary>A craving in the night: who got up for what, until when someone may see them at it.</summary>
        public Pawn craver;
        public string cravingDrug;
        public int cravingUntil = -1;

        /// <summary>The last binge seen as egged on, so one binge is seen once.</summary>
        public Pawn binger;
        public int bingeAt = -1;

        /// <summary>The craving eased: who sits up with whom, until dawn.</summary>
        public Pawn sitter, addict;
        public int letGoUntil = -1;

        /// <summary>The horned reveller: the pawn, the home, the fire he walks to, whom he shares his cup with, whether he has, until when.</summary>
        public Pawn satyr, satyrTo;
        public Map satyrMap;
        public IntVec3 satyrAt = IntVec3.Invalid;
        public bool shared;
        public int satyrUntil = -1;

        /// <summary>The god in a cup: the stack he is in, which one of it, since when, whether someone noticed it.</summary>
        public Thing cup;
        public int cupK, cupSince = -1;
        public bool cupNoticed;

        /// <summary>Who took his cup, of what, what it did, when the crash comes, when the book is written, a clean high's hours.</summary>
        public Pawn cupBy;
        public string cupDrug;
        public CupOutcome cupOutcome;
        public int cupCrashAt = -1, cupBookAt = -1, cleanUntil = -1;
        /// <summary>What his cup sets going once the drinking is done (a vision walked to, a craving's second one), and when.</summary>
        public int cupAfter = -1;
        public string cupAfterDef;

        public void ExposeData()
        {
            Scribe_References.Look(ref feastMap, "feastMap");
            Scribe_Values.Look(ref feastDay, "feastDay", -1);
            Scribe_References.Look(ref nightMap, "nightMap");
            Scribe_Values.Look(ref nightAt, "nightAt", IntVec3.Invalid);
            Scribe_Values.Look(ref nightUntil, "nightUntil", -1);
            Scribe_Values.Look(ref nightMorning, "nightMorning", -1);
            Scribe_Values.Look(ref nightStayed, "nightStayed");
            Scribe_References.Look(ref seer, "seer");
            Scribe_Values.Look(ref visionAt, "visionAt", IntVec3.Invalid);
            Scribe_Values.Look(ref visionTrue, "visionTrue");
            Scribe_Values.Look(ref visionFromCup, "visionFromCup");
            Scribe_Values.Look(ref visionSight, "visionSight");
            Scribe_Values.Look(ref visionDrug, "visionDrug");
            Scribe_Values.Look(ref visionUntil, "visionUntil", -1);
            Scribe_References.Look(ref craver, "craver");
            Scribe_Values.Look(ref cravingDrug, "cravingDrug");
            Scribe_Values.Look(ref cravingUntil, "cravingUntil", -1);
            Scribe_References.Look(ref binger, "binger");
            Scribe_Values.Look(ref bingeAt, "bingeAt", -1);
            Scribe_References.Look(ref sitter, "sitter");
            Scribe_References.Look(ref addict, "addict");
            Scribe_Values.Look(ref letGoUntil, "letGoUntil", -1);
            Scribe_References.Look(ref satyr, "satyr");
            Scribe_References.Look(ref satyrTo, "satyrTo");
            Scribe_References.Look(ref satyrMap, "satyrMap");
            Scribe_Values.Look(ref satyrAt, "satyrAt", IntVec3.Invalid);
            Scribe_Values.Look(ref shared, "shared");
            Scribe_Values.Look(ref satyrUntil, "satyrUntil", -1);
            Scribe_References.Look(ref cup, "cup");
            Scribe_Values.Look(ref cupK, "cupK");
            Scribe_Values.Look(ref cupSince, "cupSince", -1);
            Scribe_Values.Look(ref cupNoticed, "cupNoticed");
            Scribe_References.Look(ref cupBy, "cupBy");
            Scribe_Values.Look(ref cupDrug, "cupDrug");
            Scribe_Values.Look(ref cupOutcome, "cupOutcome");
            Scribe_Values.Look(ref cupCrashAt, "cupCrashAt", -1);
            Scribe_Values.Look(ref cupBookAt, "cupBookAt", -1);
            Scribe_Values.Look(ref cleanUntil, "cleanUntil", -1);
            Scribe_Values.Look(ref cupAfter, "cupAfter", -1);
            Scribe_Values.Look(ref cupAfterDef, "cupAfterDef");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (nightMap == null) nightUntil = -1;
                if (seer == null) visionUntil = -1;
                if (craver == null) cravingUntil = -1;
                if (sitter == null || addict == null) letGoUntil = -1;
                if (satyr == null || satyrMap == null) satyrUntil = -1;
                if (cup == null || cup.Destroyed) cupSince = -1;
                if (cupBy == null)
                {
                    cupCrashAt = -1;
                    cupBookAt = -1;
                    cleanUntil = -1;
                }
                RevelCup.Loaded(this);
            }
        }
    }

    /// <summary>
    /// Revel, the Open Cup (the player, 15 and 16 November 2026: "a god of drugs, the god of excess partying ... with the good
    /// and bad that they bring to a colony ... the god likes to deceive or kind of play with pawns, minds and bodies, you know,
    /// like drugs do"). No loving: the player left it out. His own happenings, each played out on the map:
    /// <list type="bullet">
    /// <item>The long night: a party or a feast at dusk, or enough of the colony at their ease round a late fire, runs on past
    /// midnight: they stay where they are, laughing, the recreation coming easy, until two; a good memory for the night, and
    /// the next morning slow (a short night's sleep, a small mood).</item>
    /// <item>A vision: someone high stops, stares, and walks out to where it showed them; true (a seam of ore in the rock,
    /// wild healroot, something real there) or false (nothing anyone else can see), believed either way and kept in the
    /// colony's book as their belief.</item>
    /// <item>A craving in the night: an addict wakes in the small hours wanting it, gets up and goes to the stores for their
    /// drug, where their drug policy lets them take it for the addiction (the game's own dose, its own effects).</item>
    /// <item>A stash found: someone working far out by day comes on a small stash of drink or smoke of the colony's kind,
    /// hidden there by nobody they know of.</item>
    /// <item>A binge egged on: someone on a drug binge of the game's own is seen talking and laughing with an empty seat.</item>
    /// <item>The craving eased (a cure against him): a friend, the priest or one devoted to Revel sits up the night with an
    /// addict in withdrawal, and by morning the addiction has eased (the game's own hediff lifted a little).</item>
    /// <item>The horned reveller: at a party, a feast or a late fire, a man with goat's horns walks in out of the dark with a
    /// cup to share (the god's own, RevelCup), hands it to someone, and is gone before dawn.</item>
    /// </list>
    /// The harm is only the game's own (the drugs taken, their highs and hangovers, addictions and overdoses), and what his
    /// cup does (RevelCup.cs) goes through the shared harm budget where it adds any.
    /// </summary>
    public static class OwnRevel
    {
        public const string Key = "Revel";
        public const string Pool = "Rimfolk_Pool_GodRevel_Say";
        public const string LongNightThought = "Rimfolk_Thought_Revel_LongNight", SlowMorningThought = "Rimfolk_Thought_Revel_SlowMorning",
            SatUpThought = "Rimfolk_Thought_Revel_SatUp", SatWithMeThought = "Rimfolk_Thought_Revel_SatWithMe";

        public static God God => OwnHappenings.God(Key);

        private static int Now => Find.TickManager.TicksGame;

        internal static readonly Color Wine = new Color(0.78f, 0.3f, 0.45f), Amber = new Color(1f, 0.78f, 0.45f);

        private static RevelState S(OwnHappeningsBook b) => b.revel ??= new RevelState();

        internal static void Hourly(OwnHappeningsBook b, Map map, int now)
        {
            var g = God;
            if (g == null) return;
            var s = S(b);
            int hour = GenLocalDate.HourOfDay(map);
            RevelCup.Hourly(b, s, g, map, now, hour);
            if (MapFacts.ThreatOn(map)) return;
            if (s.nightUntil < 0 && hour >= OwnHappeningsMath.LongNightFrom && hour <= OwnHappeningsMath.LongNightTo) Night(b, g, map, now);
            if (s.satyrUntil < 0 && hour >= OwnHappeningsMath.SatyrFrom && hour <= OwnHappeningsMath.SatyrTo) Satyr(b, g, map, now);
            if (s.visionUntil < 0) Vision(b, g, map, now, hour);
            if (s.cravingUntil < 0 && hour >= OwnHappeningsMath.CravingFrom && hour <= OwnHappeningsMath.CravingTo) Craving(b, g, map, now);
            if (hour >= 8 && hour <= 17) Stash(b, g, map, now);
            Binge(b, g, map, now);
            if (s.letGoUntil < 0 && hour >= OwnHappeningsMath.LetGoFrom && hour <= OwnHappeningsMath.LetGoTo) LetGo(b, g, map, now);
            if (s.nightMorning >= 0 && s.nightMap == map && hour >= 7 && hour <= 10) Morning(s, map);
        }

        internal static void Step(OwnHappeningsBook b, int now)
        {
            var s = S(b);
            if (s.nightUntil >= 0) OwnHappenings.Safe("the long night", () => NightStep(b, s, now));
            if (s.visionUntil >= 0) OwnHappenings.Safe("a vision", () => VisionStep(b, s, now));
            if (s.cravingUntil >= 0) OwnHappenings.Safe("a craving", () => CravingStep(b, s, now));
            if (s.letGoUntil >= 0) OwnHappenings.Safe("a night sat up", () => LetGoStep(b, s, now));
            if (s.satyrUntil >= 0) OwnHappenings.Safe("the horned reveller", () => SatyrStep(b, s, now));
            if (s.cupAfter >= 0) OwnHappenings.Safe("his cup, after", () => RevelCup.After(b, s, now));
        }

        /// <summary>His feast kept at a home (OwnHappenings.FestivalKept): the night runs long, and he may come to the fire.</summary>
        internal static void Festival(OwnHappeningsBook b, Map map)
        {
            var s = S(b);
            s.feastMap = map;
            s.feastDay = GenDate.DaysPassed;
        }

        private static bool Feast(RevelState s, Map map) => s.feastMap == map && s.feastDay == GenDate.DaysPassed;

        /// <summary>His presence on the land now (his own condition), for a stronger reason.</summary>
        internal static bool Presence(Map map) => GodPresenceOwn.EffectOn(map, out _, out bool harsh) == PresenceEffect.Revelry && !harsh;

        // ================= Who is at their ease, and where =================

        /// <summary>A party of the game's own under way at the home (a gathering: a party, a concert), with some of the colony in it.</summary>
        internal static Lord Party(Map map) =>
            map.lordManager.lords.FirstOrDefault(l => l.LordJob is LordJob_Joinable_Gathering && l.ownedPawns.Count(p => p.IsFreeColonist) >= OwnHappeningsMath.NightGatheredParty);

        /// <summary>One of the colony at their ease now: awake, free, at their recreation or in a gathering, rested and fed enough to stay up.</summary>
        internal static bool AtEase(Pawn p) =>
            OwnHappenings.Ours(p) && p.Awake() && !p.Downed && !p.Drafted && !p.InMentalState && p.DevelopmentalStage.Adult()
            && (p.CurJob?.def?.joyKind != null || p.GetLord()?.LordJob is LordJob_Joinable_Gathering)
            && OwnHappeningsMath.StaysUp(p.needs?.rest?.CurLevel ?? 1f, p.needs?.food?.CurLevelPercentage ?? 1f);

        /// <summary>The colony's fires and tables (a late fire, a table to drink at), the busiest first.</summary>
        internal static Building Fire(Map map, IntVec3 near)
        {
            bool Lit(Building x) => x is ThingWithComps t && t.GetComp<CompRefuelable>() is CompRefuelable r && r.HasFuel && (t.GetComp<CompHeatPusher>() != null || t.GetComp<CompFireOverlay>() != null);
            return map.listerBuildings.allBuildingsColonist.Where(x => x.Spawned && !x.Position.Fogged(map) && (x.def.IsTable || Lit(x)))
                .OrderBy(x => near.IsValid ? x.Position.DistanceToSquared(near) : 0).FirstOrDefault();
        }

        /// <summary>Who of the colony is gathered at their ease near a place.</summary>
        private static List<Pawn> Gathered(Map map, IntVec3 at, float range) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => AtEase(p) && (!at.IsValid || p.Position.InHorDistOf(at, range))).ToList();

        /// <summary>Where the colony is at its ease now: a party's people, else the busiest fire or table with people at their recreation round it.</summary>
        private static (IntVec3 at, List<Pawn> who, bool party) Gathering(Map map)
        {
            var lord = Party(map);
            if (lord != null)
            {
                var who = lord.ownedPawns.Where(AtEase).ToList();
                if (who.Count > 0) return (Centre(who), who, true);
            }
            var easy = map.mapPawns.FreeColonistsSpawned.Where(AtEase).ToList();
            if (easy.Count == 0) return (IntVec3.Invalid, easy, false);
            var best = (at: IntVec3.Invalid, who: new List<Pawn>());
            foreach (var p in easy)
            {
                var near = easy.Where(x => x.Position.InHorDistOf(p.Position, 12f)).ToList();
                if (near.Count > best.who.Count) best = (p.Position, near);
            }
            var fire = Fire(map, best.at);
            var spot = fire != null && fire.Position.InHorDistOf(best.at, 14f) ? fire.Position : best.at;
            return (spot, best.who, false);
        }

        private static IntVec3 Centre(List<Pawn> who)
        {
            if (who.Count == 0) return IntVec3.Invalid;
            int x = (int)who.Average(p => p.Position.x), z = (int)who.Average(p => p.Position.z);
            return new IntVec3(x, 0, z);
        }

        // ================= The long night =================

        private static void Night(OwnHappeningsBook b, God g, Map map, int now)
        {
            var s = S(b);
            var (at, who, party) = Gathering(map);
            if (!at.IsValid) return;
            var t = OwnHappeningsMath.NightTrigger(Feast(s, map), party, who.Count);
            if (t == GodPaceMath.Trigger.None) return;
            if (!Feast(s, map) || !Rand.Chance(0.6f))
                if (!OwnHappenings.Calls(b, OwnHappening.RevelLongNight, t, g, map, now, Presence(map) ? 1.5f : 1f)) return;
            BeginNight(b, map, at, who);
        }

        /// <summary>The night begins to run long where they are gathered: someone says so, and they stay until two.</summary>
        public static bool BeginNight(OwnHappeningsBook b, Map map, IntVec3 at, List<Pawn> who)
        {
            var s = S(b);
            if (map == null || !at.IsValid || who == null || who.Count == 0) return false;
            s.nightMap = map;
            s.nightAt = at;
            int hour = GenLocalDate.HourOfDay(map);
            int hours = hour >= 12 ? 24 - hour + OwnHappeningsMath.LongNightEnds : Math.Max(1, OwnHappeningsMath.LongNightEnds - hour);
            s.nightUntil = Now + hours * GenDate.TicksPerHour;
            s.nightStayed = null;
            OwnHappenings.Came(b, OwnHappening.RevelLongNight, OwnHappenings.Now, null, who.Count.ToString());
            var g = God;
            var speaker = who.RandomElement();
            OwnHappenings.SayFrom(speaker, Pool, g, "night");
            OwnHappenings.Sound(map, at, 0.5f, "Pawn_Goat_Call");
            OwnHappenings.Line(map, "CC_GodRevel_Night_Text".Translate(Homes.Name(map).Named("HOME"), who.Count.Named("COUNT")).Resolve(), new LookTargets(new TargetInfo(at, map)));
            if (g != null) OwnHappenings.Saw(g, map, at, GodActKind.Omen, speaker, "CC_GodRevel_Saw_Night");
            b.Due(Now + 250);
            return true;
        }

        private static void NightStep(OwnHappeningsBook b, RevelState s, int now)
        {
            var map = s.nightMap;
            if (map == null || !ColonyCouncil.Homes.IsHome(map))
            {
                s.nightUntil = -1;
                return;
            }
            if (now >= s.nightUntil || MapFacts.ThreatOn(map))
            {
                EndNight(s, map);
                return;
            }
            var stayed = new List<string>(s.nightStayed.NullOrEmpty() ? new string[0] : s.nightStayed.Split('|'));
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(p => p.Position.InHorDistOf(s.nightAt, 12f)).ToList())
            {
                if (!OwnHappenings.Ours(p) || !p.Awake() || p.Downed || p.Drafted || p.InMentalState) continue;
                if (!OwnHappeningsMath.StaysUp(p.needs?.rest?.CurLevel ?? 1f, p.needs?.food?.CurLevelPercentage ?? 1f)) continue;
                // They stay where they are, at their ease: the recreation keeps coming, a little at a time.
                p.needs?.joy?.GainJoy(OwnHappeningsMath.NightJoyStep, JoyKindDefOf.Social);
                if (!stayed.Contains(p.ThingID)) stayed.Add(p.ThingID);
                if (p.CurJob == null || p.CurJob.def.casualInterruptible && !p.CurJob.playerForced && p.CurJob.def.joyKind == null && p.GetLord() == null)
                    OwnHappenings.Stand(p, s.nightAt, 600);
            }
            s.nightStayed = string.Join("|", stayed.Take(30));
            if (Rand.Chance(0.15f)) OwnHappenings.Glow(map, s.nightAt, Amber, 3, 2f, 0.35f);
            b.Due(now + 250);
        }

        /// <summary>Two in the morning: they go to bed, glad of the night; the morning after is slow for them.</summary>
        private static void EndNight(RevelState s, Map map)
        {
            var stayed = Stayed(s, map);
            foreach (var p in stayed)
            {
                OwnHappenings.Memory(p, LongNightThought);
                if (p.needs?.rest != null) p.needs.rest.CurLevel = Mathf.Max(0.05f, p.needs.rest.CurLevel - OwnHappeningsMath.NightRestCost);
            }
            var g = God;
            if (stayed.Count > 0)
            {
                OwnHappenings.SayFrom(stayed.RandomElement(), Pool, g, "nightend");
                s.nightMorning = Now + GenDate.TicksPerHour * 3;
            }
            else s.nightStayed = null;
            s.nightUntil = -1;
        }

        private static List<Pawn> Stayed(RevelState s, Map map)
        {
            if (s.nightStayed.NullOrEmpty() || map == null) return new List<Pawn>();
            var ids = new HashSet<string>(s.nightStayed.Split('|'));
            return map.mapPawns.FreeColonistsSpawned.Where(p => ids.Contains(p.ThingID)).ToList();
        }

        /// <summary>The morning after: those who stayed up are slow and heavy, and one of them says so.</summary>
        private static void Morning(RevelState s, Map map)
        {
            var stayed = Stayed(s, map);
            foreach (var p in stayed) OwnHappenings.Memory(p, SlowMorningThought);
            var awake = stayed.Where(p => p.Awake()).ToList();
            if (awake.Count > 0) OwnHappenings.SayFrom(awake.RandomElement(), Pool, God, "morning");
            s.nightMorning = -1;
            s.nightStayed = null;
            s.nightMap = null;
        }

        // ================= A vision =================

        private static void Vision(OwnHappeningsBook b, God g, Map map, int now, int hour)
        {
            var seer = map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Free(p) && p.DevelopmentalStage.Adult() && RevelGoods.High(p) != null).RandomElementWithFallback();
            bool night = hour >= 21 || hour <= 4;
            var t = OwnHappeningsMath.VisionTrigger(seer != null, night, Presence(map));
            if (t == GodPaceMath.Trigger.None || !OwnHappenings.Calls(b, OwnHappening.RevelVision, t, g, map, now)) return;
            See(b, seer, false);
        }

        /// <summary>
        /// Someone stops and stares, then walks out to where the vision showed them: true (a seam of ore in the rock, wild
        /// healroot, a bed of something good) or false (a place with nothing there). <paramref name="fromCup"/>: the god's cup
        /// gave it.
        /// </summary>
        public static bool See(OwnHappeningsBook b, Pawn p, bool fromCup)
        {
            var s = S(b);
            if (p == null || !p.Spawned || s.visionUntil >= 0) return false;
            var map = p.Map;
            var high = RevelGoods.High(p);
            bool truth = OwnHappeningsMath.VisionTrue(Rand.Value, GodNear.Of(God, map));
            IntVec3 at = IntVec3.Invalid;
            string sight = null;
            if (truth) at = TrueSight(map, p, out sight);
            if (!at.IsValid)
            {
                truth = false;
                at = FalseSight(map, p, out sight);
            }
            if (!at.IsValid) return false;
            s.seer = p;
            s.visionAt = at;
            s.visionTrue = truth;
            s.visionSight = sight;
            s.visionDrug = high?.def?.label ?? s.cupDrug ?? "";
            s.visionFromCup = fromCup;
            s.visionUntil = Now + GenDate.TicksPerHour * 4;
            OwnHappenings.Stand(p, at, 360);
            OwnHappenings.Glow(map, p.Position, Wine, 5, 0.6f, 0.3f);
            if (!fromCup) OwnHappenings.Came(b, OwnHappening.RevelVision, OwnHappenings.Now);
            OwnHappenings.SayFrom(p, Pool, God, "vision");
            b.Due(Now + 400);
            return true;
        }

        private static string Dir(IntVec3 from, IntVec3 to)
        {
            var d = to - from;
            string k = Math.Abs(d.x) > Math.Abs(d.z) ? d.x > 0 ? "East" : "West" : d.z > 0 ? "North" : "South";
            return ("CC_GodPO_Dir_" + k).Translate().Resolve();
        }

        /// <summary>Something real out there: a seam of ore in the rock not yet marked for mining, wild healroot, else nothing.</summary>
        private static IntVec3 TrueSight(Map map, Pawn p, out string sight)
        {
            sight = null;
            var from = p.Position;
            Thing ore = null;
            foreach (var c in GenRadial.RadialCellsAround(from, OwnHappeningsMath.VisionTo, false))
            {
                if (!c.InBounds(map) || c.InHorDistOf(from, OwnHappeningsMath.VisionFrom)) continue;
                var e = c.GetEdifice(map);
                if (e?.def?.building == null || !e.def.building.isResourceRock || e.def.building.mineableThing == null) continue;
                if (map.designationManager.DesignationAt(c, DesignationDefOf.Mine) != null) continue;
                ore = e;
                break;
            }
            if (ore != null)
            {
                var stand = OwnHappenings.Beside(map, ore.Position, p);
                if (stand.IsValid)
                {
                    sight = "CC_GodRevel_Sight_Ore".Translate(ore.def.building.mineableThing.label.Named("ORE"), Dir(from, ore.Position).Named("DIR")).Resolve();
                    return stand;
                }
            }
            var healroot = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_HealrootWild");
            if (healroot != null)
            {
                var plant = map.listerThings.ThingsOfDef(healroot).Where(x => x.Spawned && !x.Position.InHorDistOf(from, OwnHappeningsMath.VisionFrom) && x.Position.InHorDistOf(from, OwnHappeningsMath.VisionTo)
                                                                              && p.CanReach(x, PathEndMode.Touch, Danger.Some)).RandomElementWithFallback();
                if (plant != null)
                {
                    sight = "CC_GodRevel_Sight_Healroot".Translate(Dir(from, plant.Position).Named("DIR")).Resolve();
                    return plant.Position;
                }
            }
            return IntVec3.Invalid;
        }

        /// <summary>A place out there with nothing anyone else can see, and what they believe they saw there.</summary>
        private static IntVec3 FalseSight(Map map, Pawn p, out string sight)
        {
            sight = null;
            var from = p.Position;
            if (!CellFinder.TryFindRandomReachableNearbyCell(from, map, OwnHappeningsMath.VisionTo, TraverseParms.For(p, Danger.Some),
                    c => c.Standable(map) && !c.Fogged(map) && !c.InHorDistOf(from, OwnHappeningsMath.VisionFrom * 1.5f), null, out var at))
                return IntVec3.Invalid;
            string k = OwnHappeningsMath.FalseSights.RandomElement();
            sight = ("CC_GodRevel_Sight_" + k).Translate(Dir(from, at).Named("DIR")).Resolve();
            return at;
        }

        private static void VisionStep(OwnHappeningsBook b, RevelState s, int now)
        {
            var p = s.seer;
            if (p == null || p.Dead || !p.Spawned || p.Downed || p.Drafted || now > s.visionUntil)
            {
                s.visionUntil = -1;
                s.seer = null;
                return;
            }
            var map = p.Map;
            if (p.Position.InHorDistOf(s.visionAt, 2.2f))
            {
                Arrived(s, p, map);
                return;
            }
            // The first look (they stand and stare) ends on its own; then they walk out to it.
            if (p.CurJobDef != JobDefOf.Goto && p.CurJobDef != JobDefOf.Wait_MaintainPosture && OwnHappenings.Yields(p)) OwnHappenings.GoTo(p, s.visionAt, GenDate.TicksPerHour * 2);
            b.Due(now + 200);
        }

        /// <summary>At the place: they stand looking, say what they see, and the book keeps it as their belief, true or not.</summary>
        private static void Arrived(RevelState s, Pawn p, Map map)
        {
            var g = God;
            s.visionUntil = -1;
            s.seer = null;
            OwnHappenings.Stand(p, s.visionAt, 400);
            OwnHappenings.Glow(map, s.visionAt, Amber, 6, 1f, 0.35f);
            OwnHappenings.SayFrom(p, Pool, g, s.visionTrue ? "visiontrue" : "visionfalse");
            string text = (s.visionTrue ? "CC_GodRevel_Vision_TextTrue" : "CC_GodRevel_Vision_TextFalse").Translate(p.Named("PAWN"), (s.visionSight ?? "").Named("SIGHT")).Resolve();
            GodPresence.Look(new LookTargets(new TargetInfo(s.visionAt, map)));
            OwnHappenings.Line(map, text, new LookTargets(new TargetInfo(s.visionAt, map)));
            if (g == null) return;
            OwnHappenings.Saw(g, map, s.visionAt, GodActKind.Omen, p, "CC_GodRevel_Saw_Vision", (s.visionSight ?? "").Named("SIGHT"));
            var taken = TakenFor(g, map);
            string words = "CC_GodRevel_Believes_Vision".Translate((s.visionDrug ?? "").Named("DRUG"), (s.visionSight ?? "").Named("SIGHT")).Resolve();
            if (Worship.Scoffer(p)) GodLore.Believes(p, null, words, true);
            else GodLore.Believes(p, taken, words);
        }

        /// <summary>The god someone takes his doing for: him, where the colony knows it was him; else as a believer reads it (maybe wrongly).</summary>
        internal static God TakenFor(God g, Map map)
        {
            if (g == null) return null;
            if (Gods.CardName(g, map) != null) return g;
            var lore = GodLore.Instance;
            return lore == null ? null : Gods.ByKey(GodLoreMath.Attribute(g.Key, lore.KnownOf(), Rand.Value));
        }

        // ================= A craving in the night =================

        private static void Craving(OwnHappeningsBook b, God g, Map map, int now)
        {
            Pawn who = null;
            Thing dose = null;
            bool withdrawal = false, craving = false;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Ours(p) && !p.Awake() && p.InBed() && !p.Downed && p.DevelopmentalStage.Adult()).InRandomOrder())
            {
                var c = RevelGoods.Craving(p);
                if (c.addiction == null || !c.craving) continue;
                var t = RevelGoods.For(map, c.addiction, p);
                if (t == null || !RevelGoods.AllowedForAddiction(p, t.def)) continue;
                who = p;
                dose = t;
                withdrawal = c.withdrawal;
                craving = c.craving;
                break;
            }
            var trig = OwnHappeningsMath.CravingTrigger(who != null, withdrawal, craving, dose != null);
            if (trig == GodPaceMath.Trigger.None || !OwnHappenings.Calls(b, OwnHappening.RevelCraving, trig, g, map, now)) return;
            Crave(b, who, dose);
        }

        /// <summary>They wake and get up for it: the game's own taking of the drug, from the stores, in the dark.</summary>
        public static bool Crave(OwnHappeningsBook b, Pawn p, Thing dose)
        {
            var s = S(b);
            if (p == null || dose == null || !p.Spawned) return false;
            try
            {
                var job = JobMaker.MakeJob(JobDefOf.Ingest, dose);
                job.count = 1;
                job.ingestTotalCount = true;
                if (!OwnHappenings.Job(p, job)) return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a craving in the night", e);
                return false;
            }
            s.craver = p;
            s.cravingDrug = dose.def.label;
            s.cravingUntil = Now + GenDate.TicksPerHour * 2;
            OwnHappenings.Came(b, OwnHappening.RevelCraving, OwnHappenings.Now);
            OwnHappenings.Line(p.Map, "CC_GodRevel_Craving_Text".Translate(p.Named("PAWN"), dose.def.label.Named("DRUG")).Resolve(), new LookTargets(p));
            b.Due(Now + 300);
            return true;
        }

        /// <summary>Someone awake sees them at it (a night owl, a guard), and says so; the book keeps it.</summary>
        private static void CravingStep(OwnHappeningsBook b, RevelState s, int now)
        {
            var p = s.craver;
            if (p == null || p.Dead || !p.Spawned || now > s.cravingUntil)
            {
                s.cravingUntil = -1;
                s.craver = null;
                return;
            }
            var seer = map(p).mapPawns.FreeColonistsSpawned.Where(x => x != p && GodOmens.Awake(x) && x.Position.InHorDistOf(p.Position, 18f) && GenSight.LineOfSight(x.Position, p.Position, p.Map, true))
                .FirstOrDefault();
            if (seer != null)
            {
                var g = God;
                OwnHappenings.SayFromWith(seer, Pool, g, new[] { p.Named("OTHER"), (s.cravingDrug ?? "").Named("DRUG") }, "cravingseen");
                if (g != null) OwnHappenings.Saw(g, p.Map, p.Position, GodActKind.Omen, seer, "CC_GodRevel_Saw_Craving", p.LabelShort.Named("PAWN"), (s.cravingDrug ?? "").Named("DRUG"));
                s.cravingUntil = -1;
                s.craver = null;
                return;
            }
            b.Due(now + 250);
        }

        private static Map map(Pawn p) => p.Map;

        // ================= A stash found =================

        private static void Stash(OwnHappeningsBook b, God g, Map map, int now)
        {
            var middle = OwnHappenings.Middle(map);
            var finder = map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Ours(p) && p.Awake() && !p.Downed && !p.Drafted && !p.InMentalState && !p.Position.Roofed(map)
                                                                     && !p.Position.InHorDistOf(middle, OwnHappeningsMath.StashFar)).RandomElementWithFallback();
            var t = OwnHappeningsMath.StashTrigger(finder != null, Presence(map));
            if (t == GodPaceMath.Trigger.None || !OwnHappenings.Calls(b, OwnHappening.RevelStash, t, g, map, now)) return;
            FindStash(b, finder);
        }

        /// <summary>A small stash of the colony's kind of drink or smoke at their feet, hidden there by nobody they know of.</summary>
        public static bool FindStash(OwnHappeningsBook b, Pawn p)
        {
            if (p == null || !p.Spawned) return false;
            var map = p.Map;
            var def = RevelGoods.Pick(map, false);
            int n = OwnHappeningsMath.StashCount(def.BaseMarketValue);
            if (n <= 0) return false;
            var at = OwnHappenings.Beside(map, p.Position, p);
            if (!at.IsValid) return false;
            Thing placed;
            try
            {
                var t = ThingMaker.MakeThing(def);
                t.stackCount = Math.Min(n, def.stackLimit);
                if (!GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near, out placed) || placed == null) return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a stash found", e);
                return false;
            }
            var g = God;
            OwnHappenings.Came(b, OwnHappening.RevelStash, OwnHappenings.Now, null, def.defName);
            OwnHappenings.Stand(p, placed.Position, 240);
            OwnHappenings.Glow(map, placed.Position, Amber, 5, 0.5f, 0.3f);
            OwnHappenings.SayFromWith(p, Pool, g, new[] { placed.LabelNoCount.Named("WHAT") }, "stash");
            string text = "CC_GodRevel_Stash_Text".Translate(p.Named("PAWN"), placed.Label.Named("WHAT")).Resolve();
            GodPresence.Look(new LookTargets(placed));
            OwnHappenings.Line(map, text, new LookTargets(placed));
            if (g != null) OwnHappenings.Saw(g, map, placed.Position, GodActKind.Blessing, p, "CC_GodRevel_Saw_Stash", placed.Label.Named("WHAT"));
            return true;
        }

        // ================= A binge egged on =================

        private static void Binge(OwnHappeningsBook b, God g, Map map, int now)
        {
            var s = S(b);
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(RevelGoods.Binging).ToList())
            {
                // Once a binge: the same one later in the day is the same binge.
                if (s.binger == p && now - s.bingeAt < GenDate.TicksPerDay) continue;
                var seer = OwnHappenings.Witness(map, p.Position, 16f);
                var t = OwnHappeningsMath.BingeTrigger(true, seer != null && seer != p);
                s.binger = p;
                s.bingeAt = now;
                if (t == GodPaceMath.Trigger.None || !Rand.Chance(OwnHappeningsMath.BingeSeenChance)) continue;
                EggedOn(b, p, seer);
                return;
            }
        }

        /// <summary>The binger raises a cup to an empty seat and talks and laughs with it; whoever sees it says so.</summary>
        public static bool EggedOn(OwnHappeningsBook b, Pawn p, Pawn seer)
        {
            if (p == null || !p.Spawned) return false;
            var map = p.Map;
            var g = God;
            var seat = GenAdj.CellsAdjacent8Way(p).Where(c => c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map) == null).RandomElementWithFallback(p.Position);
            OwnHappenings.Came(b, OwnHappening.RevelBinge, OwnHappenings.Now);
            OwnHappenings.SayFrom(p, Pool, g, "binge");
            OwnHappenings.Glow(map, seat, Wine, 4, 0.4f, 0.3f);
            OwnHappenings.Sound(map, seat, 0.35f, "Pawn_Goat_Call");
            if (g != null) GodTouchFx.Show(g, map, seat, TouchTier.Small, false);
            OwnHappenings.Line(map, "CC_GodRevel_Binge_Text".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p));
            if (seer != null)
            {
                OwnHappenings.SayFromWith(seer, Pool, g, new[] { p.Named("OTHER") }, "bingesaw");
                GodTouch.About(map, "CC_GodRevel_Saw_Binge", p);
                if (g != null) OwnHappenings.Saw(g, map, seat, GodActKind.Omen, seer, "CC_GodRevel_Saw_Binge", p.LabelShort.Named("PAWN"));
            }
            return true;
        }

        // ================= The craving eased =================

        private static void LetGo(OwnHappeningsBook b, God g, Map map, int now)
        {
            Pawn addict = null, sitter = null;
            foreach (var a in map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Ours(p) && !p.Downed).InRandomOrder())
            {
                var c = RevelGoods.Craving(a);
                if (c.addiction == null || !c.withdrawal) continue;
                sitter = Sitter(map, a, g);
                if (sitter == null) continue;
                addict = a;
                break;
            }
            bool devoted = sitter != null && (Worship.IsPriest(sitter) || PawnGods.HoldOf(sitter, g) >= PawnGodsMath.DevotedFrom);
            var t = OwnHappeningsMath.LetGoTrigger(addict != null, sitter != null, devoted);
            if (t == GodPaceMath.Trigger.None || !OwnHappenings.Calls(b, OwnHappening.RevelLetGo, t, g, map, now)) return;
            SitUp(b, sitter, addict);
        }

        /// <summary>Who sits up with them: the priest, one devoted to Revel, else a friend (by the game's own opinion), free tonight.</summary>
        internal static Pawn Sitter(Map map, Pawn addict, God g) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => p != addict && OwnHappenings.Free(p) && p.DevelopmentalStage.Adult() && RevelGoods.Craving(p).addiction == null
                                                         && (Worship.IsPriest(p) || g != null && PawnGods.HoldOf(p, g) >= PawnGodsMath.DevotedFrom
                                                             || (p.relations?.OpinionOf(addict) ?? 0) >= OwnHappeningsMath.LetGoOpinion)
                                                         && p.CanReach(addict, PathEndMode.Touch, Danger.Some))
                .OrderByDescending(p => p.relations?.OpinionOf(addict) ?? 0).FirstOrDefault();

        public static bool SitUp(OwnHappeningsBook b, Pawn sitter, Pawn addict)
        {
            var s = S(b);
            if (sitter == null || addict == null || !sitter.Spawned || !addict.Spawned) return false;
            s.sitter = sitter;
            s.addict = addict;
            var map = addict.Map;
            int hour = GenLocalDate.HourOfDay(map);
            int hours = hour >= 12 ? 24 - hour + OwnHappeningsMath.LetGoDawn : Math.Max(1, OwnHappeningsMath.LetGoDawn - hour);
            s.letGoUntil = Now + hours * GenDate.TicksPerHour;
            OwnHappenings.Came(b, OwnHappening.RevelLetGo, OwnHappenings.Now);
            OwnHappenings.GoTo(sitter, OwnHappenings.Beside(map, addict.Position, sitter), GenDate.TicksPerHour * 2);
            OwnHappenings.Line(map, "CC_GodRevel_LetGo_Begin".Translate(sitter.Named("SITTER"), addict.Named("PAWN")).Resolve(), new LookTargets(addict));
            b.Due(Now + 300);
            return true;
        }

        private static void LetGoStep(OwnHappeningsBook b, RevelState s, int now)
        {
            var p = s.sitter;
            var a = s.addict;
            if (p == null || a == null || p.Dead || a.Dead || !p.Spawned || !a.Spawned || p.Map != a.Map || p.Drafted || p.Downed)
            {
                s.letGoUntil = -1;
                s.sitter = s.addict = null;
                return;
            }
            if (now >= s.letGoUntil)
            {
                Eased(s, p, a);
                return;
            }
            if (!p.Position.InHorDistOf(a.Position, 3f))
            {
                if (p.CurJobDef != JobDefOf.Goto && OwnHappenings.Yields(p)) OwnHappenings.GoTo(p, OwnHappenings.Beside(p.Map, a.Position, p), GenDate.TicksPerHour);
            }
            else if (p.CurJobDef != JobDefOf.Wait_MaintainPosture && OwnHappenings.Yields(p))
            {
                OwnHappenings.Stand(p, a.Position, 1200);
                if (Rand.Chance(0.2f)) OwnHappenings.SayFromWith(p, Pool, God, new[] { a.Named("OTHER") }, "letgo");
            }
            b.Due(now + 300);
        }

        /// <summary>Dawn, and they sat up with them all night: the craving has eased (the game's own addiction lifted a little), and both are glad of it.</summary>
        private static void Eased(RevelState s, Pawn sitter, Pawn addict)
        {
            s.letGoUntil = -1;
            s.sitter = s.addict = null;
            if (!sitter.Position.InHorDistOf(addict.Position, 6f)) return;
            var c = RevelGoods.Craving(addict);
            if (c.addiction == null) return;
            c.addiction.Severity = OwnHappeningsMath.Eased(c.addiction.Severity);
            OwnHappenings.MemoryOf(sitter, SatUpThought, addict);
            OwnHappenings.MemoryOf(addict, SatWithMeThought, sitter);
            var map = addict.Map;
            var g = God;
            OwnHappenings.Glow(map, addict.Position, Amber, 6, 0.6f, 0.35f);
            OwnHappenings.SayFromWith(addict, Pool, g, new[] { sitter.Named("OTHER") }, "letgoeased");
            OwnHappenings.Line(map, "CC_GodRevel_LetGo_Text".Translate(sitter.Named("SITTER"), addict.Named("PAWN"), (c.addiction.Chemical?.label ?? c.addiction.LabelBase).Named("DRUG")).Resolve(),
                new LookTargets(addict));
            if (g == null) return;
            OwnHappenings.Saw(g, map, addict.Position, GodActKind.Blessing, sitter, "CC_GodRevel_Saw_LetGo", addict.LabelShort.Named("PAWN"));
            var taken = TakenFor(g, map);
            if (!Worship.Scoffer(addict)) GodLore.Believes(addict, taken, "CC_GodRevel_Believes_LetGo".Translate().Resolve());
        }

        // ================= The horned reveller =================

        private static void Satyr(OwnHappeningsBook b, God g, Map map, int now)
        {
            var s = S(b);
            var (at, who, party) = Gathering(map);
            if (!at.IsValid) return;
            var t = OwnHappeningsMath.SatyrTrigger(Feast(s, map), party, who.Count, s.nightUntil >= 0 && s.nightMap == map);
            if (t == GodPaceMath.Trigger.None || !OwnHappenings.Calls(b, OwnHappening.RevelSatyr, t, g, map, now, Presence(map) ? 1.5f : 1f)) return;
            Come(b, map, at);
        }

        /// <summary>
        /// The horned reveller comes in out of the dark to where the colony is at its ease, a cup of the colony's kind in his
        /// hand: a person of a people at peace with goat's horns (a satyr's shape where one of the player's mods has one, else
        /// Alpha Genes' goat horns or the game's own), made the god's and shown as one (GodAvatar), with the god's moment.
        /// </summary>
        public static bool Come(OwnHappeningsBook b, Map map, IntVec3 at)
        {
            var s = S(b);
            var g = God;
            if (g == null || map == null || !at.IsValid || s.satyrUntil >= 0) return false;
            var x = MakeSatyr(g);
            if (x == null) return false;
            var drink = RevelGoods.Pick(map, true);
            try
            {
                var cup = ThingMaker.MakeThing(drink);
                cup.stackCount = 1;
                x.inventory?.innerContainer?.TryAdd(cup);
                // A drink of his kind is left where he stood (Gods/Touch/GodImpact.cs).
                GodTouch.About(map, "CC_GodRevel_Saw_Satyr", x, thing: cup);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the horned reveller's cup", e);
            }
            float hours = OwnHappeningsMath.SatyrHours(GenLocalDate.HourOfDay(map));
            var lord = Arrivals.Visit(new List<Pawn> { x }, map, x.Faction, at, hours);
            if (lord == null || !x.Spawned)
            {
                GodStranger.Vanish(x);
                return false;
            }
            s.satyr = x;
            s.satyrMap = map;
            s.satyrAt = at;
            s.satyrTo = null;
            s.shared = false;
            s.satyrUntil = Now + Mathf.RoundToInt(hours * GenDate.TicksPerHour);
            OwnHappenings.Came(b, OwnHappening.RevelSatyr, OwnHappenings.Now);
            var spec = PresenceSpec.For(g, map, PresenceKind.Appear, x.Position, x);
            spec.title = GodsOnMapWords.Title(g, map, false, "CC_GodRevel_Satyr_Title", "CC_GodRevel_Satyr_TitleNamed");
            spec.text = "CC_GodRevel_Satyr_Text".Translate(Homes.Name(map).Named("HOME"), drink.label.Named("DRINK")).Resolve();
            spec.what = "CC_GodRevel_Saw_Satyr";
            if (!GodPresenceSeam.Call(spec)) Tell.Letter(NewsKind.Land, Importance.Notable, map, spec.title, spec.text, LetterDefOf.NeutralEvent, new LookTargets(x));
            b.Due(Now + 250);
            return true;
        }

        /// <summary>A grown man of a people at peace, with a satyr's shape or goat's horns, the god's and shown as one. Null when none can be made.</summary>
        internal static Pawn MakeSatyr(God g)
        {
            var f = Arrivals.Friendly(null, x => g.factions.Contains(x) ? 4f : 1f);
            var kind = f == null ? null : Arrivals.PersonKind(f);
            if (kind == null) return null;
            Pawn x;
            try
            {
                x = Arrivals.Make(kind, f, new FloatRange(28f, 50f), Gender.Male);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the horned reveller made", e);
                return null;
            }
            if (x == null) return null;
            Horns(x);
            if (!Gods.On || !GodAvatar.Make(x, GodsWiring.LookOf(g), Gods.Name(g), false, g.colour, g.colour2))
            {
                Arrivals.Discard(x);
                return null;
            }
            return x;
        }

        /// <summary>Goat's horns: a satyr's shape where one of the player's mods has one (any xenotype called so), else the horns of Alpha Genes or the game's own.</summary>
        internal static void Horns(Pawn x)
        {
            if (!ModsConfig.BiotechActive || x?.genes == null) return;
            try
            {
                var satyr = DefDatabase<XenotypeDef>.AllDefsListForReading.FirstOrDefault(d => d.defName.IndexOf("satyr", StringComparison.OrdinalIgnoreCase) >= 0
                                                                                              || (d.label ?? "").IndexOf("satyr", StringComparison.OrdinalIgnoreCase) >= 0);
                if (satyr != null)
                {
                    x.genes.SetXenotype(satyr);
                    return;
                }
                foreach (var n in new[] { "AG_ForsakenHornsGoat", "AG_DevilsheepHorns", "Headbone_MiniHorns", "Headbone_CenterHorn" })
                {
                    var gene = DefDatabase<GeneDef>.GetNamedSilentFail(n);
                    if (gene == null) continue;
                    x.genes.AddGene(gene, true);
                    return;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the horned reveller's horns", e);
            }
        }

        private static void SatyrStep(OwnHappeningsBook b, RevelState s, int now)
        {
            var x = s.satyr;
            var map = s.satyrMap;
            if (x == null || map == null || x.Dead || x.Downed || !x.Spawned || x.Map != map || now >= s.satyrUntil || MapFacts.ThreatOn(map))
            {
                Gone(s);
                return;
            }
            if (!s.shared)
            {
                var to = s.satyrTo;
                if (to == null || !to.Spawned || to.Map != map || RevelGoods.WontTake(to, Cup(x)?.def))
                {
                    to = s.satyrTo = Recipient(map, x);
                }
                if (to != null)
                {
                    if (x.Position.InHorDistOf(to.Position, 2.2f))
                    {
                        Share(s, x, to, map);
                    }
                    else if (x.CurJobDef != JobDefOf.Goto || !(x.CurJob?.targetA.Cell.InHorDistOf(to.Position, 2.5f) ?? false))
                    {
                        OwnHappenings.GoTo(x, OwnHappenings.Beside(map, to.Position, x), GenDate.TicksPerHour);
                    }
                }
            }
            else if (Rand.Chance(0.1f)) OwnHappenings.Glow(map, x.Position, Wine, 2, 0.4f, 0.3f);
            b.Due(now + 250);
        }

        private static Thing Cup(Pawn x) => x?.inventory?.innerContainer?.FirstOrDefault(t => RevelGoods.His(t.def));

        /// <summary>Whom he shares his cup with: someone of the colony at their ease near the fire who would take it, the nearest.</summary>
        private static Pawn Recipient(Map map, Pawn x)
        {
            var cup = Cup(x);
            if (cup == null) return null;
            return map.mapPawns.FreeColonistsSpawned.Where(p => AtEase(p) || OwnHappenings.Free(p) && p.Awake())
                .Where(p => p.Position.InHorDistOf(x.Position, 25f) && !RevelGoods.WontTake(p, cup.def) && x.CanReach(p, PathEndMode.Touch, Danger.Some))
                .OrderBy(p => p.Position.DistanceToSquared(x.Position)).FirstOrDefault();
        }

        /// <summary>He hands them his cup and they drink it there and then: the god in it (RevelCup), with whatever it does.</summary>
        private static void Share(RevelState s, Pawn x, Pawn to, Map map)
        {
            var cup = Cup(x);
            s.shared = true;
            if (cup == null) return;
            try
            {
                x.inventory.innerContainer.Remove(cup);
                if (!to.inventory.innerContainer.TryAdd(cup))
                {
                    cup.Destroy();
                    return;
                }
                RevelCup.Mark(OwnHappenings.Book, cup, 1, false);
                var job = JobMaker.MakeJob(JobDefOf.Ingest, cup);
                job.count = 1;
                OwnHappenings.Job(to, job);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the horned reveller's cup shared", e);
                return;
            }
            var g = God;
            OwnHappenings.Stand(x, to.Position, 300);
            // together: he walked up to them and stands beside them, the cup in their hands.
            string line = OwnHappenings.LineFrom(x, Pool, null, new[] { to.Named("OTHER") }, "satyrshare");
            if (!line.NullOrEmpty()) Speech.Say(x, line, to);
            OwnHappenings.Glow(map, to.Position, Amber, 5, 0.5f, 0.3f);
            OwnHappenings.Line(map, "CC_GodRevel_Satyr_Share".Translate(to.Named("PAWN"), cup.def.label.Named("DRINK")).Resolve(), new LookTargets(to));
            if (g != null) OwnHappenings.Saw(g, map, to.Position, GodActKind.Appear, to, "CC_GodRevel_Saw_Shared", to.LabelShort.Named("PAWN"));
        }

        /// <summary>He is gone before dawn (or when the night is broken up): out of the world, nobody sees him leave.</summary>
        private static void Gone(RevelState s)
        {
            var x = s.satyr;
            var map = s.satyrMap;
            s.satyrUntil = -1;
            s.satyr = s.satyrTo = null;
            s.satyrMap = null;
            if (x == null) return;
            bool here = x.Spawned && map != null;
            if (here) OwnHappenings.Glow(map, x.Position, Wine, 6, 0.8f, 0.4f);
            GodStranger.Vanish(x);
            if (here) OwnHappenings.Line(map, "CC_GodRevel_Satyr_Gone".Translate(Homes.Name(map).Named("HOME")).Resolve(), null);
        }
    }
}
