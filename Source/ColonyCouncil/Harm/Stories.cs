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
    /// Harm, part 7: personal stories (Harm/HarmSeven.cs is the hub), each from a story already told on the map.
    /// <list type="bullet">
    /// <item>The nemesis seeks the one they hurt (2.10.1, outside the budget): a nemesis back on the colony's land in a
    /// fight, a raider in a raid or a beast nemesis come back (Beasts/BeastNemeses.cs), once a return goes for the one it
    /// hurt before rather than the nearest, if they are out on the field and not behind cover; a taunt, and the game's
    /// own fight. It adds no hurt of its own (the raid's and the beast's attacks are the game's, counted where they
    /// always were), so it takes no share of the budget; it makes no return of its own either.</item>
    /// <item>The worst night, alone (3.10.2): in "getting clean", the worst night with the friend not at their side, half
    /// the time they go to the stores for the drug, and the game's own binge (with its own overdose risk) follows. The
    /// friend at their side, or the drug forbidden or locked away, keeps it from them. With personal stories.</item>
    /// <item>First steps alone (3.10.3): in "the new leg", first steps by the gate without the friend at their side, one
    /// time in three a fall (a bruise), and someone near helps them up. The friend's walk to the gate and back keeps
    /// it from them. With personal stories.</item>
    /// <item>A working gone wrong (4.8.1, A RimWorld of Magic): in "the long discipline", the long practice at dawn of a
    /// fire, ice or storm mage, out in the open, one working in 25 slips: a burn, the game's frostbite, a shock; half
    /// with the teacher there. With personal stories and the mod.</item>
    /// <item>The lost child, found hurt (2.6.1, outside the budget: a neighbour's child): found only after the first
    /// night out, about half of them have frostnip, a chill or a turned ankle (a child's scraped knee); found early,
    /// unhurt. A search sent out early prevents it. The setting and the storyteller's lean scale it.</item>
    /// <item>Set upon at the edge (2.7.1, outside the budget: their courier): half of the messengers waylaid on a dangerous
    /// road are set upon at the colony's own edge, in place of coming in already hurt: two or three bandits at them as
    /// they come on, the courier's cut, the bandits running off; one of the colony near where they come in scares them
    /// off first. The game's own enemies: the colony can go after them.</item>
    /// </list>
    /// </summary>
    public static class Stories
    {
        private static int Now => Find.TickManager.TicksGame;

        public static bool DebugSeek, DebugWorst, DebugSteps, DebugWorking, DebugLost, DebugSetUpon;

        /// <summary>Stories and returns already looked at (an arc's stage, a nemesis's return): session memory, one look each.</summary>
        private static readonly HashSet<string> looked = new HashSet<string>();

        /// <summary>A mage at the dawn practice: the element, and the teacher if they came along.</summary>
        private static readonly Dictionary<int, (WorkingSlip slip, Pawn teacher)> practising = new Dictionary<int, (WorkingSlip, Pawn)>();

        public static void Reset()
        {
            looked.Clear();
            practising.Clear();
        }

        private static bool Look(string key)
        {
            if (looked.Count > 600) looked.Clear();
            return looked.Add(key);
        }

        private static string ArcKey(Arc a) => a.pawn.thingIDNumber + "|" + a.startedTick + "|" + a.stage;

        public static void Hourly(int now)
        {
        }

        /// <summary>Each colonist's hour: a story's worst night, first steps, the dawn practice.</summary>
        public static void Hour(Pawn p, int now)
        {
            if (!Harm.On) return;
            var arc = ArcTracker.Instance?.Of(p);
            if (arc?.def == null) return;
            switch (arc.def.defName)
            {
                case "Rimfolk_Arc_GettingClean":
                    WorstNight(arc, p);
                    break;
                case "Rimfolk_Arc_NewLeg":
                    FirstSteps(arc, p);
                    break;
                case "Council_Arc_PM_Discipline":
                    Practice(arc, p);
                    break;
            }
        }

        // ================= The nemesis seeks the one they hurt (2.10.1) =================

        /// <summary>Behind cover from the nemesis: indoors under a roof, or cover between them that blocks enough.</summary>
        private static bool InCover(Pawn victim, Pawn x)
        {
            var map = victim.Map;
            var room = victim.GetRoom();
            if (victim.Position.Roofed(map) && room != null && !room.PsychologicallyOutdoors) return true;
            try
            {
                return CoverUtility.CalculateOverallBlockChance(victim, x.Position, map) >= HarmSevenMath.CoverBlock;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Every 250 ticks: a nemesis on the colony's land in a fight, its one push at the one it hurt.</summary>
        public static void Battles(int now)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp?.nemeses == null || !Harm.On || !(ColonyCouncilMod.Settings?.nemeses ?? false)) return;
            foreach (var n in comp.nemeses.Open)
            {
                var x = n.pawn;
                var v = n.victim;
                if (x == null || v == null || !x.Spawned || x.Dead || x.Downed || x.Map == null || !x.Map.IsPlayerHome || !x.HostileTo(Faction.OfPlayer)) continue;
                if (!v.Spawned || v.Dead || v.Downed || v.Map != x.Map) continue;
                bool fight = MapFacts.ThreatOn(x.Map) || (comp.social?.BattleOn(x.Map) ?? false) || x.InMentalState;
                if (!fight && !DebugSeek) continue;
                string key = "seek|" + x.thingIDNumber + "|" + n.returns;
                if (looked.Contains(key)) continue;
                if (!x.CanReach(v, PathEndMode.Touch, Danger.Deadly)) continue;
                bool cover = InCover(v, x);
                if (!HarmSevenMath.Seeks(true, cover, false) && !DebugSeek) continue;
                looked.Add(key);
                DebugSeek = false;
                // The setting decides, and the storyteller's lean: at 100% always, as the nemesis would.
                if (!Rand.Chance(Mathf.Clamp01(Harm.Scale))) continue;
                Seek(x, v, n.IsBeast);
            }
        }

        private static void Seek(Pawn x, Pawn v, bool beast)
        {
            try
            {
                var weapon = x.equipment?.Primary;
                if (beast || weapon == null || !weapon.def.IsRangedWeapon)
                {
                    var job = JobMaker.MakeJob(JobDefOf.AttackMelee, v);
                    job.expiryInterval = HarmSevenMath.SeekTicks;
                    job.killIncappedTarget = false;
                    x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                }
                else
                {
                    float range = x.equipment.PrimaryEq?.PrimaryVerb?.verbProps?.range ?? 20f;
                    var from = v.Position.ToVector3Shifted();
                    var toward = (x.Position.ToVector3Shifted() - from).normalized * Mathf.Max(3f, range * HarmSixMath.BreakRangeShare);
                    var aim = (from + toward).ToIntVec3().ClampInsideMap(x.Map);
                    if (!CellFinder.TryFindRandomCellNear(aim, x.Map, 3, c => c.Standable(x.Map) && x.CanReach(c, PathEndMode.OnCell, Danger.Deadly), out var cell)) return;
                    var go = JobMaker.MakeJob(JobDefOf.Goto, cell);
                    go.locomotionUrgency = LocomotionUrgency.Jog;
                    go.expiryInterval = HarmSevenMath.SeekTicks;
                    x.jobs.StartJob(go, JobCondition.InterruptForced, null, false, true);
                    var shoot = JobMaker.MakeJob(JobDefOf.AttackStatic, v);
                    shoot.expiryInterval = HarmSevenMath.SeekTicks;
                    x.jobs.jobQueue.EnqueueFirst(shoot);
                }
                // apart: shouted across the field (a beast's sign is its own: the words over it).
                if (!beast) HarmFour.Say(x, "Rimfolk_Pool_NemesisSeeks");
                HarmFour.Mote(x, beast ? "CC_HarmSeven_Mote_SeeksBeast" : "CC_HarmSeven_Mote_Seeks", v.Named("VICTIM"));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm seven: a nemesis seeks", e);
            }
        }

        // ================= The worst night, alone (3.10.2) =================

        /// <summary>A drug of their addiction they could get to: not forbidden, in reach, not claimed.</summary>
        private static Thing DrugFor(Pawn p, ChemicalDef chemical)
        {
            if (chemical == null) return null;
            foreach (var t in p.Map.listerThings.ThingsInGroup(ThingRequestGroup.Drug))
            {
                var drug = t?.def?.GetCompProperties<CompProperties_Drug>();
                if (drug?.chemical != chemical || !t.Spawned || t.IsForbidden(p) || !p.CanReserve(t) || !p.CanReach(t, PathEndMode.ClosestTouch, Danger.Some)) continue;
                return t;
            }
            return null;
        }

        private static MentalStateDef binge;

        private static void WorstNight(Arc arc, Pawn p)
        {
            if (arc.stage != 1 || !Harm.SystemOn(HarmKinds.WorstNight.system)) return;
            bool debug = DebugWorst;
            if (!debug && !HarmSevenMath.WorstHour(GenLocalDate.HourOfDay(p.Map))) return;
            if (p.Downed || p.InMentalState || p.Drafted || p.DevelopmentalStage.Juvenile()) return;
            var friend = arc.other;
            bool beside = friend != null && friend.Spawned && friend.Map == p.Map && !friend.Downed && friend.Position.InHorDistOf(p.Position, HarmSevenMath.SideRange);
            // The friend at their side on the worst night: the story's own step, and nothing comes of it.
            if (beside && !debug) return;
            if (!Look("worst|" + ArcKey(arc))) return;
            DebugWorst = false;
            var a = ArcWorker_GettingClean.Addiction(p, arc.key);
            var drug = DrugFor(p, a?.Chemical);
            // The drug forbidden or locked away: the night is hard, and that's all.
            if (drug == null) return;
            if (!debug && !Harm.Roll(p, HarmKinds.WorstNight, HarmSevenMath.WorstNightChance, HurtClass.Injury)) return;
            binge ??= DefDatabase<MentalStateDef>.GetNamedSilentFail("Binging_DrugExtreme") ?? DefDatabase<MentalStateDef>.GetNamedSilentFail("Binging_DrugMajor");
            if (binge == null) return;
            // apart: said to the dark, on the way to the stores.
            HarmFour.Say(p, "Rimfolk_Pool_WorstNight");
            bool started;
            using (WorldReactions.OwnBreak()) started = p.mindState?.mentalStateHandler?.TryStartMentalState(binge, "CC_HarmSeven_WorstNightReason".Translate().Resolve(), false, false, true) == true;
            if (started) Harm.Note(p, HarmKinds.WorstNight);
        }

        // ================= First steps alone (3.10.3) =================

        private static void FirstSteps(Arc arc, Pawn p)
        {
            if (arc.stage != 0 || !Harm.SystemOn(HarmKinds.FirstSteps.system)) return;
            var map = p.Map;
            bool debug = DebugSteps;
            if (!debug && (!HarmSevenMath.Daytime(GenLocalDate.HourOfDay(map)) || !(p.pather?.Moving ?? false))) return;
            var gate = PlaySpots.GateCell(map, p);
            if (!gate.IsValid || !debug && !p.Position.InHorDistOf(gate, HarmSevenMath.GateNear)) return;
            var friend = arc.other;
            bool beside = friend != null && friend.Spawned && friend.Map == map && !friend.Downed && friend.Position.InHorDistOf(p.Position, HarmSevenMath.SideRange);
            string key = "steps|" + ArcKey(arc);
            // With the friend at their side: the walk to the gate and back as the story has it, and nothing more to fear.
            if (beside && !debug)
            {
                looked.Add(key);
                return;
            }
            if (!Look(key)) return;
            DebugSteps = false;
            if (!(debug ? Harm.May(p, HurtClass.Injury) : Harm.Roll(p, HarmKinds.FirstSteps, HarmSevenMath.FirstStepsChance, HurtClass.Injury))) return;
            if (Harm.Injure(p, HurtKind.Bruise, HurtWhere.Leg, HarmSevenMath.StepsLow, HarmSevenMath.StepsHigh, HarmKinds.FirstSteps) == null) return;
            HarmFour.SitDown(p, 300);
            HarmFour.Say(p, "Rimfolk_Pool_FirstStepsFall");
            HarmFour.Mote(p, "CC_HarmSeven_Mote_StepsFall");
            var by = map.mapPawns.FreeColonistsSpawned.Where(o => o != p && Together.Can(o, p) && !o.DevelopmentalStage.Baby()).FirstOrDefault();
            // together: Together.Can, the one near helping them up.
            if (by != null) HarmFour.Say(by, "Rimfolk_Pool_FirstStepsHelp", p);
        }

        // ================= A working gone wrong (4.8.1, A RimWorld of Magic) =================

        private static readonly Dictionary<string, TraitDef> traits = new Dictionary<string, TraitDef>();

        private static bool Has(Pawn p, string trait)
        {
            if (!traits.TryGetValue(trait, out var def))
            {
                def = DefDatabase<TraitDef>.GetNamedSilentFail(trait);
                traits[trait] = def;
            }
            return def != null && (p.story?.traits?.HasTrait(def) ?? false);
        }

        /// <summary>The mage's element: a fire mage, an ice mage or a storm mage (A RimWorld of Magic's own classes, by name).</summary>
        public static WorkingSlip ElementOf(Pawn p) => p == null ? WorkingSlip.None : HarmSevenMath.SlipOf(Has(p, "InnerFire"), Has(p, "HeartOfFrost"), Has(p, "StormBorn"));

        private static void Practice(Arc arc, Pawn p)
        {
            if (arc.stage != 1 || !Harm.SystemOn(HarmKinds.WorkingWrong.system)) return;
            var map = p.Map;
            bool debug = DebugWorking;
            if (!debug && !HarmSevenMath.Dawn(GenLocalDate.HourOfDay(map))) return;
            var slip = ElementOf(p);
            if (slip == WorkingSlip.None || !PlayOuts.OurPerson(p) || !Together.Able(p) || !PlayOuts.CanJoin(p) || !PlayOuts.Interruptible(p) || PlayOuts.NeedsPress(p)) return;
            if (map.dangerWatcher.DangerRating != StoryDanger.None) return;
            if (!Look("practice|" + p.thingIDNumber + "|" + (Now / HarmMath.DayTicks))) return;
            if (!CellFinder.TryFindRandomCellNear(p.Position, map, 15, c => c.Standable(map) && !c.Roofed(map) && c.GetFirstPawn(map) == null && !c.IsForbidden(p)
                                                                         && p.CanReach(c, PathEndMode.OnCell, Danger.None), out var cell)) return;
            var job = JobMaker.MakeJob(HarmSevenDefOf.Rimfolk_PracticeWalk, cell);
            job.count = HarmSevenMath.PracticeTicks;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (p.CurJobDef != HarmSevenDefOf.Rimfolk_PracticeWalk) return;
            // The teacher comes along to watch, if they're free.
            Pawn teacher = null;
            var o = arc.other;
            if (arc.tag == "teacher" && o != null && Together.Able(o) && o.Map == map && PlayOuts.Interruptible(o) && !PlayOuts.NeedsPress(o))
            {
                var near = CellFinder.RandomClosewalkCellNear(cell, map, 3);
                var go = JobMaker.MakeJob(JobDefOf.Goto, near);
                go.expiryInterval = 1200;
                o.jobs.StartJob(go, JobCondition.InterruptForced, null, false, true);
                var watch = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture);
                watch.expiryInterval = HarmSevenMath.PracticeTicks;
                o.jobs.jobQueue.EnqueueFirst(watch);
                teacher = o;
            }
            practising[p.thingIDNumber] = (slip, teacher);
        }

        /// <summary>At the practice ground (JobDriver_HarmWalk's arrival): the working's light, and one in 25 it slips.</summary>
        public static void Practising(Pawn p)
        {
            if (!practising.TryGetValue(p.thingIDNumber, out var pr)) return;
            try
            {
                if (pr.slip == WorkingSlip.Fire) FleckMaker.ThrowFireGlow(p.DrawPos, p.Map, 1f);
                else if (pr.slip == WorkingSlip.Storm) FleckMaker.ThrowLightningGlow(p.DrawPos, p.Map, 1f);
                else FleckMaker.ThrowAirPuffUp(p.DrawPos, p.Map);
            }
            catch
            {
                // A glow is only for the eye.
            }
            var teacher = pr.teacher;
            bool there = teacher != null && teacher.Spawned && teacher.Map == p.Map && teacher.Position.InHorDistOf(p.Position, HarmSevenMath.SideRange);
            bool debug = DebugWorking;
            DebugWorking = false;
            if (!(debug ? Harm.May(p, HurtClass.Injury) : Harm.Roll(p, HarmKinds.WorkingWrong, HarmSevenMath.Working(pr.slip, there), HurtClass.Injury))) return;
            var kind = pr.slip == WorkingSlip.Ice ? HurtKind.Frostbite : HurtKind.Burn;
            var where = pr.slip == WorkingSlip.Storm ? HurtWhere.Arm : HurtWhere.Hand;
            if (Harm.Injure(p, kind, where, HarmSevenMath.WorkingLow, HarmSevenMath.WorkingHigh, HarmKinds.WorkingWrong) == null) return;
            string tag = pr.slip.ToString().ToLowerInvariant();
            HarmFour.Say(p, "Rimfolk_Pool_WorkingSlips", null, tag);
            HarmFour.Mote(p, "CC_HarmSeven_Mote_Working_" + pr.slip);
            // together: the teacher watching from a few steps off.
            if (there && Together.Can(teacher, p)) HarmFour.Say(teacher, "Rimfolk_Pool_TeacherWord", p);
            practising.Remove(p.thingIDNumber);
        }

        /// <summary>The practice is over (JobDriver_HarmWalk's end).</summary>
        public static void PracticeDone(Pawn p) => practising.Remove(p.thingIDNumber);

        // ================= The lost child, found hurt (2.6.1) =================

        /// <summary>
        /// The lost one is found (HappeningWorker_MissionLostChild.Check): after the first night out, about half of them hurt
        /// by it. Theirs, not ours: outside the budget, the setting and the storyteller's lean scaling it, the cap's rule for
        /// children kept (a child only the mildest).
        /// </summary>
        public static void LostFound(Mission m, Pawn x, Pawn finder)
        {
            if (!Harm.On || m == null || x?.Map == null || x.health?.hediffSet == null) return;
            var map = x.Map;
            int now = Now;
            int from = m.started >= 0 ? m.started : now;
            float hours = (now - from) / (float)HarmMath.HourTicks;
            int startHour = ((GenLocalDate.HourOfDay(map) - (int)hours) % 24 + 48) % 24;
            bool debug = DebugLost;
            DebugLost = false;
            if (!debug && !HarmSevenMath.NightPassed(startHour, hours)) return;
            if (!debug && !Rand.Chance(HarmMath.Chance(HarmSevenMath.LostHurtChance, Harm.Scale))) return;
            bool child = x.DevelopmentalStage.Juvenile();
            var cls = child ? HurtClass.Mild : HurtClass.Injury;
            float cold = map.mapTemperature.OutdoorTemp;
            bool wet = map.weatherManager.RainRate > 0.01f || map.weatherManager.SnowRate > 0.01f;
            int which = HarmSevenMath.LostWhich(cold, wet);
            string tag;
            if (which == 2 && child)
            {
                if (Harm.Injure(x, HurtKind.Scratch, HurtWhere.Leg, HarmSevenMath.ScrapeLow, HarmSevenMath.ScrapeHigh, why: null, c: HurtClass.Mild) == null) return;
                tag = "scrape";
            }
            else
            {
                var def = HarmFour.Def(which == 0 ? "Council_Ail_Frostnip" : which == 1 ? "Council_Ail_HeadCold" : "Council_Ail_SprainedAnkle");
                var info = def != null ? AilmentTracker.InfoOf(def) : null;
                if (def == null || Harm.Ail(x, def, info?.startSeverity.RandomInRange ?? 0.3f, why: null, c: cls) == null) return;
                tag = which == 0 ? "frost" : which == 1 ? "chill" : "ankle";
            }
            HarmFour.Say(x, "Rimfolk_Pool_LostChildHurt", null, tag);
            HarmFour.Mote(x, "CC_HarmSeven_Mote_Lost_" + tag);
            // together: the finder has just found them (in sight, a few steps off).
            if (finder != null && Together.Can(finder, x)) HarmFour.Say(finder, "Rimfolk_Pool_LostChildFinder", x, tag);
        }

        // ================= Set upon at the edge (2.7.1) =================

        /// <summary>
        /// A messenger waylaid on a dangerous road comes onto the colony's land (CourierTracker): half the time they are set
        /// upon at the edge instead of coming in hurt. True when it happened here (the old cut is then not given).
        /// </summary>
        public static bool SetUpon(CourierTrip t, Pawn courier, Map map, IntVec3 entry)
        {
            if (!Harm.On || t == null || courier == null || map == null || !entry.IsValid) return false;
            bool debug = DebugSetUpon;
            DebugSetUpon = false;
            if (!debug && !Rand.Chance(HarmMath.Chance(HarmSevenMath.SetUponShare, Harm.Scale))) return false;
            var f = Find.FactionManager.AllFactionsListForReading.Where(x => x != null && !x.IsPlayer && !x.Hidden && !x.defeated && x.def.humanlikeFaction
                                                                             && x.HostileTo(Faction.OfPlayer) && Arrivals.PersonKind(x) != null).RandomElementWithFallback();
            if (f == null) return false;
            var kind = Arrivals.PersonKind(f);
            var bandits = new List<Pawn>();
            int n = Rand.RangeInclusive(HarmSevenMath.BanditsMin, HarmSevenMath.BanditsMax);
            try
            {
                for (int i = 0; i < n; i++)
                {
                    var b = Arrivals.Make(kind, f, new FloatRange(18f, 45f));
                    if (b == null) continue;
                    b.inventory?.DestroyAll();
                    bandits.Add(b);
                }
                foreach (var b in bandits) GenSpawn.Spawn(b, CellFinder.RandomClosewalkCellNear(entry, map, 4), map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm seven: bandits at the edge", e);
            }
            bandits.RemoveAll(b => b == null || !b.Spawned);
            if (bandits.Count == 0) return false;
            // One of ours near where they come in: the bandits run before they strike.
            bool helped = map.mapPawns.FreeColonistsSpawned.Any(o => HarmFour.Ours(o) && !o.Downed && o.Awake() && o.Position.InHorDistOf(entry, HarmSevenMath.SetUponHelp));
            bool hurt = !helped && Harm.Injure(courier, HurtKind.Cut, Rand.Bool ? HurtWhere.Arm : HurtWhere.Leg, HarmSevenMath.SetUponLow, HarmSevenMath.SetUponHigh, why: null, by: bandits[0]) != null;
            if (hurt)
            {
                if (!t.letterText.NullOrEmpty()) t.letterText += "\n\n" + "CC_Letter_Waylaid".Translate(courier.Named("PAWN")).Resolve();
                HarmFour.Say(courier, "Rimfolk_Pool_CourierSetUpon");
            }
            else if (helped) HarmFour.Say(courier, "Rimfolk_Pool_CourierSaved");
            HarmFour.Mote(bandits[0], "CC_HarmSeven_Mote_Bandits");
            try
            {
                LordMaker.MakeNewLord(f, new LordJob_ExitMapBest(LocomotionUrgency.Jog, false, true), map, bandits);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm seven: the bandits run", e);
            }
            // The game warns of enemies on the land; here a word of what happened at the edge.
            string text = (hurt ? "CC_HarmSeven_SetUpon" : helped ? "CC_HarmSeven_SetUponScared" : "CC_HarmSeven_SetUponMissed").Translate(courier.Named("PAWN"), (courier.Faction?.Name ?? "").Named("FACTION"), f.Name.Named("BANDITS")).Resolve();
            ColonyCouncil.Tell.Message(NewsKind.World, Importance.Important, map, text, new LookTargets(bandits.Cast<Thing>().Append(courier)), MessageTypeDefOf.ThreatSmall);
            return true;
        }
    }
}
