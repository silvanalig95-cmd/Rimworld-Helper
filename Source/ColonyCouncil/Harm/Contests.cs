using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Harm, part 6: contests, festivals and fights (Harm/HarmSix.cs is the hub). Each is seen on the map; the fights that
    /// follow are the game's own.
    /// <list type="bullet">
    /// <item>The bonfire's spark (4.1.2): a bonfire (an answer's, the calendar's bonfire night) laid in the open on a dry,
    /// windy summer night, once a night, one in 20 throws a spark into dry grass two to six cells off: the game's own fire,
    /// the nearest few beating it out, and whoever stood within a step or two singed (the mildest). Never in rain or snow; a
    /// cleared spot, or a firebreak, leaves it nothing to catch. Named under the answer. With answers on the map.</item>
    /// <item>Musters at the line (4.2.1): with the militia's drills or the "hold the way in" project, on the evening off
    /// the militia's bouts (one in four), the marshal, or the first of them, calls the shooters to the line: each walks to
    /// a post behind the way in and stands a while, some Shooting learned. One muster in 30 has a mishap at a post (half
    /// with a marshal of Shooting 10): a bowstring's snap, a powder burn, a ricochet's graze, a charge cell's burn, by the
    /// weapon. Named on the militia's card. With answers on the map.</item>
    /// <item>Fighters spar (4.8.2, A RimWorld of Magic): two of its combat classes, free in the evening and on good enough
    /// terms (neither worn down), one evening in three spar by the sparring rule: one bout in 25 lands a blow. With the mod
    /// and answers on the map.</item>
    /// <item>Brave to a fault (3.6.2): a group brave to a fault, or the bold ones, about once a season eggs one of its own
    /// into a dare: a swim in cold water, or wrestling its strongest; one dare in three ends in a chill or a sprain. A
    /// cautious one at hand (its worrier, a nervous anchor) talks them out of it half the time. "Crossing thin ice" is left
    /// out: the game has no ice that gives. With the groups and answers on the map.</item>
    /// <item>A coward's name (3.7.2): once their name for courage turns to a coward's, in the next fight at home, undrafted,
    /// they push forward out of cover to prove it wrong (the pass and the setting decide), at the nearest enemy, the game's
    /// own fight after. Drafted and held, or a word from the marshal at their side first, keeps them in cover. With the
    /// names.</item>
    /// <item>Fury that forgets cover (3.8.2): in a fight against the people they swore a vendetta on, once a battle, one in
    /// three, an undrafted avenger breaks from cover toward them. Drafting and holding them keeps them in it. With the
    /// vendettas.</item>
    /// </list>
    /// </summary>
    public static class Contests
    {
        private static int Now => Find.TickManager.TicksGame;

        public static bool DebugSpark, DebugMuster, DebugSpar, DebugDare, DebugBreak;

        private static readonly HashSet<int> sparked = new HashSet<int>();
        private static readonly Dictionary<int, int> musterDay = new Dictionary<int, int>(), sparDay = new Dictionary<int, int>(), dareDay = new Dictionary<int, int>();
        private static readonly Dictionary<int, Muster> musters = new Dictionary<int, Muster>();
        /// <summary>A dared swimmer: the group's name, for the line.</summary>
        private static readonly Dictionary<int, string> swimmers = new Dictionary<int, string>();
        /// <summary>The battles each avenger has had their one look in (the avenger's thingIDNumber and the fight's start).</summary>
        private static readonly HashSet<long> furyLooked = new HashSet<long>();

        private sealed class Muster
        {
            public Map map;
            public bool mishap, done;
            public int start;
        }

        private static bool? magic;

        /// <summary>A RimWorld of Magic is loaded.</summary>
        public static bool MagicLoaded => magic ??= ModsConfig.IsActive("torann.arimworldofmagic");

        public static void Reset()
        {
            sparked.Clear();
            musterDay.Clear();
            sparDay.Clear();
            dareDay.Clear();
            musters.Clear();
            swimmers.Clear();
            furyLooked.Clear();
        }

        private static int Day(Map map) => GenLocalDate.Year(map) * 60 + GenLocalDate.DayOfYear(map);

        /// <summary>The harm book's hour: the bonfires, the musters, the spars and the dares.</summary>
        public static void Hourly(int now)
        {
            if (!Harm.On) return;
            Sparks();
            Musters(now);
            Spars();
            Dares();
            if (furyLooked.Count > 200) furyLooked.Clear();
        }

        // ================= The bonfire's spark (4.1.2) =================

        private static bool Summer(Map map)
        {
            var s = GenLocalDate.Season(map);
            return s == Season.Summer || s == Season.PermanentSummer;
        }

        private static void Sparks()
        {
            var tracker = PlayOutTracker.Instance;
            if (tracker == null || !Harm.SystemOn(HarmKinds.BonfireSpark.system)) return;
            foreach (var p in tracker.plays)
            {
                if (p == null || p.kind != PlayKind.Bonfire || p.state != PlayState.Running || p.map == null || sparked.Contains(p.id)) continue;
                var fire = p.fireLaid ?? p.place;
                if (fire == null || !fire.Spawned || fire.Map != p.map) continue;
                var map = p.map;
                bool outdoors = !fire.Position.Roofed(map) && (fire.GetRoom()?.PsychologicallyOutdoors ?? true);
                var w = map.weatherManager;
                bool wet = w.RainRate > 0.01f || w.SnowRate > 0.01f;
                bool debug = DebugSpark;
                float chance = HarmSixMath.Spark(Summer(map), wet, map.windManager.WindSpeed, outdoors);
                if (!debug && chance <= 0f) continue;
                // Once a night: the first windy hour of a dry summer bonfire has its roll.
                sparked.Add(p.id);
                if (sparked.Count > 200) sparked.Clear();
                DebugSpark = false;
                if (!debug && !Harm.Roll(HarmKinds.BonfireSpark, chance)) continue;
                Spark(map, fire.Position);
            }
        }

        /// <summary>Dry grass off the fire, two to six cells away: a wild plant (not a tree) under the open sky that burns.</summary>
        private static IntVec3 DryGrass(IntVec3 at, Map map)
        {
            foreach (var c in GenRadial.RadialCellsAround(at, HarmSixMath.SparkFar, true).InRandomOrder())
            {
                if (!c.InBounds(map) || c.InHorDistOf(at, HarmSixMath.SparkNear) || c.Roofed(map)) continue;
                var plant = c.GetPlant(map);
                if (plant?.def?.plant == null || plant.def.plant.IsTree || plant.def.BaseFlammability <= 0f || c.GetFirstThing<Fire>(map) != null) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        private static void Spark(Map map, IntVec3 at)
        {
            var cell = DryGrass(at, map);
            // A cleared spot or a firebreak: the spark dies on bare ground.
            if (!cell.IsValid) return;
            FleckMaker.ThrowMicroSparks(cell.ToVector3Shifted(), map);
            if (!FireUtility.TryStartFireIn(cell, map, 0.1f, null)) return;
            var fire = cell.GetFirstThing<Fire>(map);
            var ours = map.mapPawns.FreeColonistsSpawned.Where(o => HarmFour.Ours(o) && Together.Able(o)).OrderBy(o => o.Position.DistanceToSquared(cell)).ToList();
            var near = ours.FirstOrDefault(o => o.Position.InHorDistOf(cell, HarmSixMath.SingeRange));
            if (near != null && Harm.Injure(near, HurtKind.Burn, Rand.Bool ? HurtWhere.Hand : HurtWhere.Leg, HarmSixMath.SingeLow, HarmSixMath.SingeHigh, HarmKinds.BonfireSpark, HurtClass.Mild) != null)
            {
                HarmFour.Say(near, "Rimfolk_Pool_Singed");
                HarmFour.Mote(near, "CC_HarmSix_Mote_Singed");
            }
            // Everyone near beating it out, with the game's own firefighting.
            int beating = 0;
            Pawn first = null;
            foreach (var o in ours)
            {
                if (fire == null || beating >= HarmSixMath.Beaters || !o.Position.InHorDistOf(cell, 20f) || o.WorkTypeIsDisabled(WorkTypeDefOf.Firefighter) || o.Downed) continue;
                try
                {
                    o.jobs.StartJob(JobMaker.MakeJob(JobDefOf.BeatFire, fire), JobCondition.InterruptForced, null, false, true);
                }
                catch
                {
                    continue;
                }
                beating++;
                first ??= o;
            }
            // apart: the shout goes up to everyone round the fire.
            if (first != null) HarmFour.Say(first, "Rimfolk_Pool_SparkCaught");
        }

        /// <summary>A bonfire's answer: on a dry, windy summer night a spark can catch the grass (in summer only, while the kind's system is on).</summary>
        public static void DescribeSpark(ImpactSheet sheet, Map map)
        {
            if (sheet == null || map == null || !Harm.On || !Harm.SystemOn(HarmKinds.BonfireSpark.system) || !Summer(map)) return;
            sheet.Add(ImpactKind.Health, -1, "CC_Impact_BonfireSpark".Translate().Resolve(), "CC_Impact_BonfireSparkBrief".Translate().Resolve(), "bonfirespark");
        }

        // ================= Musters at the line (4.2.1) =================

        /// <summary>One of the colony who can answer the muster: a grown-up who can fight, free and not pressed, with a gun or a bow in hand.</summary>
        private static bool CanMuster(Pawn p) =>
            PlayOuts.OurPerson(p) && !p.DevelopmentalStage.Juvenile() && !p.WorkTagIsDisabled(WorkTags.Violent) && Together.Able(p) && PlayOuts.CanJoin(p)
            && PlayOuts.Interruptible(p) && !PlayOuts.NeedsPress(p) && p.GetLord() == null && DrillBouts.Of(p) == null && (p.equipment?.Primary?.def?.IsRangedWeapon ?? false);

        private static void Musters(int now)
        {
            if (!Harm.SystemOn(HarmKinds.LineMuster.system)) return;
            var all = CouncilGameComponent.Instance?.colonies;
            if (all == null) return;
            foreach (var c in all)
            {
                if (c == null || c.lostAt != ColonyStateMath.NotLost) continue;
                var d = c.decrees;
                var map = d?.Ground;
                if (map == null || !map.IsPlayerHome) continue;
                bool militia = d.active.Any(a => a?.def != null && a.def.hurts == DecreeHurt.Muster);
                var amb = AmbitionTracker.For(map)?.active;
                bool project = amb?.def?.defName == "Rimfolk_Amb_HoldTheWayIn" && amb.map == map;
                bool debug = DebugMuster;
                if (!militia && !project && !debug) continue;
                if (!debug && !HarmMath.DrillHour(GenLocalDate.HourOfDay(map))) continue;
                int day = Day(map);
                if (!debug && musterDay.TryGetValue(map.uniqueID, out int md) && md == day) continue;
                if (!debug && !HarmSixMath.MusterEvening(day))
                {
                    musterDay[map.uniqueID] = day;
                    continue;
                }
                if (PlayOuts.WhyNotNow(map, false) != null || !JoyUtility.EnjoyableOutsideNow(map)) continue;
                DebugMuster = false;
                if (Call(map, debug)) musterDay[map.uniqueID] = day;
            }
            foreach (var k in musters.Where(kv => kv.Value == null || now - kv.Value.start > 2 * GenDate.TicksPerHour).Select(kv => kv.Key).ToList()) musters.Remove(k);
        }

        /// <summary>Where the line is: the way in a defense line holds, else the gate.</summary>
        private static IntVec3 LineAt(Map map, Pawn near)
        {
            var line = DefenseLines.Of(map)?.lines?.FirstOrDefault(l => l != null && l.gate.IsValid && l.gate.InBounds(map));
            if (line != null) return line.gate;
            return near != null ? PlaySpots.GateCell(map, near) : IntVec3.Invalid;
        }

        /// <summary>The posts behind the way in: by the colony's cover there if it has some, a couple of cells apart, on the home side.</summary>
        private static List<IntVec3> Posts(Map map, IntVec3 gate, Pawn by, int wanted)
        {
            var center = TemplatePlanner.ColonyCenter(map);
            float gateD = gate.DistanceToSquared(center);
            var cells = GenRadial.RadialCellsAround(gate, 8f, false)
                .Where(c => c.InBounds(map) && c.Standable(map) && c.DistanceToSquared(center) < gateD && c.GetFirstPawn(map) == null && by.CanReach(c, PathEndMode.OnCell, Danger.Some))
                .OrderByDescending(c => GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)).Any(n => n.InBounds(map) && n.GetEdifice(map) is Building b && Avail.IsCover(b.def)) ? 1 : 0)
                .ThenBy(c => c.DistanceToSquared(gate)).ToList();
            var posts = new List<IntVec3>();
            foreach (var c in cells)
            {
                if (posts.Count >= wanted) break;
                if (posts.Any(x => x.InHorDistOf(c, 1.9f))) continue;
                posts.Add(c);
            }
            return posts;
        }

        private static bool Call(Map map, bool debug)
        {
            var shooters = map.mapPawns.FreeColonistsSpawned.Where(CanMuster).InRandomOrder().Take(HarmSixMath.MusterShooters).ToList();
            if (shooters.Count == 0) return false;
            var gate = LineAt(map, shooters[0]);
            if (!gate.IsValid) return false;
            var posts = Posts(map, gate, shooters[0], shooters.Count);
            if (posts.Count == 0) return false;
            var marshal = OfficeTracker.For(map)?.HolderOf("Council_Office_Marshal");
            bool skilled = marshal != null && (marshal.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0) >= HarmSixMath.MusterMarshalSkill;
            var m = new Muster { map = map, start = Now, mishap = debug || Harm.Roll(HarmKinds.LineMuster, HarmSixMath.Muster(skilled)) };
            int sent = 0;
            for (int i = 0; i < shooters.Count && i < posts.Count; i++)
            {
                var job = JobMaker.MakeJob(HarmSixDefOf.Rimfolk_MusterPost, posts[i]);
                job.count = HarmSixMath.MusterTicks;
                shooters[i].jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (shooters[i].CurJobDef != HarmSixDefOf.Rimfolk_MusterPost) continue;
                musters[shooters[i].thingIDNumber] = m;
                sent++;
            }
            if (sent == 0) return false;
            var caller = marshal != null && shooters.Contains(marshal) ? marshal : shooters[0];
            // apart: the call goes out to everyone at the muster; each walks to their post.
            HarmFour.Say(caller, "Rimfolk_Pool_MusterCall", null, Ages.Tag(Ages.Ours));
            return true;
        }

        /// <summary>A shooter at their post (JobDriver_HarmWalk's arrival): the muster's mishap, if it has one, on the first there the cap allows.</summary>
        public static void AtPost(Pawn p)
        {
            if (!musters.TryGetValue(p.thingIDNumber, out var m) || m == null || !m.mishap || m.done || !Harm.May(p, HurtClass.Injury)) return;
            var weapon = p.equipment?.Primary?.def;
            if (weapon == null) return;
            m.done = true;
            var kind = HarmSixMath.MishapOf(weapon.techLevel <= TechLevel.Medieval, weapon.techLevel >= TechLevel.Spacer, Rand.Value);
            HurtKind hk;
            HurtWhere where;
            switch (kind)
            {
                case MusterMishap.Bowstring:
                    hk = HurtKind.Cut;
                    where = Rand.Bool ? HurtWhere.Hand : HurtWhere.Arm;
                    break;
                case MusterMishap.Graze:
                    hk = HurtKind.Cut;
                    where = HurtWhere.Arm;
                    break;
                default:
                    hk = HurtKind.Burn;
                    where = HurtWhere.Hand;
                    break;
            }
            var h = Harm.Injure(p, hk, where, HarmSixMath.MishapLow, HarmSixMath.MishapHigh, HarmKinds.LineMuster);
            if (h == null) return;
            if (hk == HurtKind.Burn && p.Map != null) FleckMaker.ThrowSmoke(p.DrawPos, p.Map, 0.5f);
            HarmFour.Say(p, "Rimfolk_Pool_MusterMishap", null, kind.ToString().ToLowerInvariant());
            HarmFour.Mote(p, "CC_HarmSix_Mote_Muster_" + kind);
            musters.Remove(p.thingIDNumber);
            // The hurt one walks back from the line.
            p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
        }

        /// <summary>A shooter's while at the post is over (JobDriver_HarmWalk's end): a little Shooting learned.</summary>
        public static void PostDone(Pawn p)
        {
            if (!musters.Remove(p.thingIDNumber)) return;
            p.skills?.Learn(SkillDefOf.Shooting, HarmSixMath.MusterXp);
        }

        // ================= Fighters spar (4.8.2) =================

        private static bool CanBout(Pawn p) =>
            PlayOuts.OurPerson(p) && !p.DevelopmentalStage.Juvenile() && !p.WorkTagIsDisabled(WorkTags.Violent) && Together.Able(p) && Together.FreeTime(p) && PlayOuts.CanJoin(p)
            && PlayOuts.Interruptible(p) && !PlayOuts.NeedsPress(p) && p.GetLord() == null && DrillBouts.Of(p) == null && !(p.skills?.GetSkill(SkillDefOf.Melee)?.TotallyDisabled ?? true);

        private static void Spars()
        {
            if (!Harm.SystemOn(HarmKinds.MagicSpar.system) || DrillBouts.Job == null) return;
            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                var map = maps[i];
                bool debug = DebugSpar;
                if (!map.IsPlayerHome || !debug && !HarmMath.DrillHour(GenLocalDate.HourOfDay(map))) continue;
                int day = Day(map);
                if (!debug && (!HarmSixMath.SparDay(day) || sparDay.TryGetValue(map.uniqueID, out int sd) && sd == day)) continue;
                if (PlayOuts.WhyNotNow(map, false) != null) continue;
                var fighters = map.mapPawns.FreeColonistsSpawned.Where(p => PeopleTags.Has(p, "fighter") && CanBout(p)).ToList();
                Pawn a = null, b = null;
                for (int x = 0; x < fighters.Count && a == null; x++)
                    for (int y = x + 1; y < fighters.Count; y++)
                    {
                        var f = fighters[x];
                        var g = fighters[y];
                        if (!HarmSixMath.Consent(f.relations?.OpinionOf(g) ?? 0, g.relations?.OpinionOf(f) ?? 0, Heart.BandOf(f) >= StrainBand.Worn, Heart.BandOf(g) >= StrainBand.Worn)) continue;
                        a = f;
                        b = g;
                        break;
                    }
                if (a == null) continue;
                sparDay[map.uniqueID] = day;
                DebugSpar = false;
                Bout(map, a, b, "spar", HarmKinds.MagicSpar, debug ? 1f : HarmSixMath.SparBlowChance);
            }
        }

        private static bool Bout(Map map, Pawn a, Pawn b, string form, HarmKind kind, float chance)
        {
            var center = PlaySpots.OpenCell(map, a);
            if (!center.IsValid || !DrillBouts.Places(map, center, a, b, new List<IntVec3>(), out var ra, out var rb)) return false;
            // A dev tool's certain blow still goes through the kind's pass: past the rate, it asks only the cap.
            return DrillBouts.Begin(map, a, b, ra, rb, false, false, false, form, kind, chance >= 1f ? 99f : chance);
        }

        public static string BeginPool(string form) => form == "dare" ? "Rimfolk_Pool_DareBegin" : "Rimfolk_Pool_SparBegin";

        public static string EndPool(string form) => form == "dare" ? "Rimfolk_Pool_DareDone" : "Rimfolk_Pool_SparEnd";

        /// <summary>A spar's or a dare's wrestle's real blow (DrillBouts.RealBlow): the spar's bruise by the colony's means, the dare's sprain.</summary>
        public static void BoutBlow(DrillBout bout, Pawn attacker, Pawn defender)
        {
            Hediff given = null;
            string tag;
            if (bout.form == "dare")
            {
                var sprain = HarmFour.Def("Council_Ail_SprainedAnkle");
                var info = sprain != null ? AilmentTracker.InfoOf(sprain) : null;
                if (sprain != null && !defender.health.hediffSet.HasHediff(sprain))
                {
                    given = Harm.Ail(defender, sprain, info?.startSeverity.RandomInRange ?? 0.4f, HarmKinds.BraveDare, HurtClass.Injury);
                    if (given != null && info != null) AilmentTracker.Instance?.Notify_Caught(defender, given);
                }
                given ??= Harm.Injure(defender, HurtKind.Bruise, HurtWhere.Leg, HarmMath.DrillLow, HarmMath.DrillHigh, HarmKinds.BraveDare, HurtClass.Bout);
                tag = "sprain";
            }
            else
            {
                DrillBouts.BlowOf(Ages.Ours, out var kind, out var where);
                given = Harm.Injure(defender, kind, where, HarmMath.DrillLow, HarmMath.DrillHigh, bout.kind ?? HarmKinds.MagicSpar, HurtClass.Bout);
                tag = kind.ToString().ToLowerInvariant();
            }
            SoundDefOf.Pawn_Melee_Punch_HitPawn?.PlayOneShot(new TargetInfo(defender.Position, defender.Map));
            if (given == null) return;
            bout.hurt = defender;
            // together: the two are at their bout, side by side; the hurt is seen there, with no message.
            HarmFour.Say(defender, bout.form == "dare" ? "Rimfolk_Pool_DareHurt" : "Rimfolk_Pool_SparHit", attacker, tag);
            HarmFour.Say(attacker, "Rimfolk_Pool_SparSorry", defender);
            HarmFour.Mote(defender, bout.form == "dare" ? "CC_HarmSix_Mote_Sprain" : "CC_HarmFour_Mote_GamesBlow");
            DrillBouts.End(bout, false);
        }

        // ================= Brave to a fault (3.6.2) =================

        private static bool Brave(SocialGroup g) => g?.norms != null && (g.norms.Contains("Rimfolk_Norm_Brave") || g.kind?.defName == "Rimfolk_Group_Bold");

        /// <summary>A cautious one of the group beside the dared: its worrier, or a nervous anchor; null with none at hand.</summary>
        private static Pawn Cautious(SocialGroup g, Pawn dared)
        {
            var worrier = g.Holder(GroupRole.Worrier);
            if (worrier != null && worrier != dared && Together.Can(worrier, dared)) return worrier;
            var anchor = g.Holder(GroupRole.Anchor);
            var nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");
            return anchor != null && anchor != dared && nerves != null && (anchor.story?.traits?.DegreeOfTrait(nerves) ?? 0) < 0 && Together.Can(anchor, dared) ? anchor : null;
        }

        private static void Dares()
        {
            if (!Harm.SystemOn(HarmKinds.BraveDare.system)) return;
            foreach (var g in Groups.All)
            {
                bool debug = DebugDare;
                if (!Brave(g) && !debug) continue;
                var free = g.Pawns.Where(p => HarmFour.Ours(p) && !HarmFour.Child(p) && Together.Able(p) && Together.FreeTime(p) && PlayOuts.CanJoin(p) && DrillBouts.Of(p) == null).ToList();
                if (free.Count < 2) continue;
                var map = free[0].Map;
                if (!debug && !HarmMath.DrillHour(GenLocalDate.HourOfDay(map))) continue;
                int day = Day(map);
                if (!debug && dareDay.TryGetValue(g.id, out int dd) && dd == day) continue;
                dareDay[g.id] = day;
                if (map.dangerWatcher.DangerRating != StoryDanger.None) continue;
                if (!debug && Rand.ValueSeeded(g.id * 977 + day * 13) >= HarmSixMath.DareChance) continue;
                var egger = g.Holder(GroupRole.Firebrand) is Pawn fb && free.Contains(fb) ? fb : free[0];
                var dared = free.FirstOrDefault(p => p != egger && Together.Can(egger, p));
                if (dared == null) continue;
                DebugDare = false;
                var calm = Cautious(g, dared);
                if (calm != null && Rand.Chance(HarmSixMath.TalkedOut))
                {
                    // together: Cautious checked Together.Can, the cautious one beside the dared.
                    HarmFour.Say(calm, "Rimfolk_Pool_DareTalkedOut", dared);
                    continue;
                }
                var water = Water(dared);
                var kind = HarmSixMath.DareOf(water.IsValid, map.mapTemperature.OutdoorTemp);
                // together: Together.Can, the one egging them on beside them.
                HarmFour.Say(egger, "Rimfolk_Pool_DareCall", dared, kind == DareKind.Swim ? "swim" : "wrestle");
                if (kind == DareKind.Swim)
                {
                    var job = JobMaker.MakeJob(HarmSixDefOf.Rimfolk_DareWalk, water);
                    job.count = 600;
                    dared.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                    if (dared.CurJobDef == HarmSixDefOf.Rimfolk_DareWalk) swimmers[dared.thingIDNumber] = g.Name;
                    continue;
                }
                var strongest = free.Where(p => p != dared).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0).FirstOrDefault();
                if (strongest != null) Bout(map, dared, strongest, "dare", HarmKinds.BraveDare, debug ? 1f : HarmSixMath.DareHurtChance);
            }
        }

        /// <summary>A standable cell beside water within forty cells of them (not the sea's depths), or invalid.</summary>
        private static IntVec3 Water(Pawn p)
        {
            var map = p.Map;
            foreach (var c in GenRadial.RadialCellsAround(p.Position, 40f, true))
            {
                if (!c.InBounds(map) || !(c.GetTerrain(map)?.IsWater ?? false) || c.GetTerrain(map) == TerrainDefOf.WaterOceanDeep || c.GetTerrain(map) == TerrainDefOf.WaterDeep) continue;
                foreach (var n in GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)))
                    if (n.InBounds(map) && n.Standable(map) && !(n.GetTerrain(map)?.IsWater ?? false) && p.CanReach(n, PathEndMode.OnCell, Danger.Some))
                        return n;
            }
            return IntVec3.Invalid;
        }

        /// <summary>A dared swim is over (JobDriver_HarmWalk's end): a chill one time in three, else a laugh.</summary>
        public static void SwimDone(Pawn p)
        {
            if (!swimmers.Remove(p.thingIDNumber) || p.Map == null) return;
            try
            {
                FleckMaker.WaterSplash(p.DrawPos, p.Map, 1.4f, 5f);
            }
            catch
            {
                // A splash is only for the eye.
            }
            float outdoor = p.Map.mapTemperature.OutdoorTemp;
            bool freezing = outdoor <= HarmSixMath.DareFreezing;
            var cls = freezing ? HurtClass.Injury : HurtClass.Mild;
            if (!Harm.Roll(p, HarmKinds.BraveDare, HarmSixMath.DareHurtChance, cls))
            {
                // apart: the laugh, to nobody in particular, back on the bank.
                HarmFour.Say(p, "Rimfolk_Pool_DareDone");
                return;
            }
            var def = freezing ? HarmFour.Def("Hypothermia") : HarmFour.Def("Council_Ail_HeadCold");
            if (def == null || p.health.hediffSet.HasHediff(def)) return;
            var info = freezing ? null : AilmentTracker.InfoOf(def);
            var h = Harm.Ail(p, def, freezing ? Rand.Range(HarmSixMath.ChillLow, HarmSixMath.ChillHigh) : info?.startSeverity.RandomInRange ?? 0.3f, HarmKinds.BraveDare, cls);
            if (h == null) return;
            if (info != null) AilmentTracker.Instance?.Notify_Caught(p, h);
            HarmFour.Say(p, "Rimfolk_Pool_DareHurt", null, "chill");
            HarmFour.Mote(p, "CC_HarmSix_Mote_Chill");
            if (freezing) ColdHurts.ToTheFire(p);
        }

        // ================= A coward's name (3.7.2), fury that forgets cover (3.8.2) =================

        /// <summary>Their name for courage turned to a coward's (ReputationTracker's news): the next fight waits for them.</summary>
        public static void CowardFormed(Pawn p)
        {
            if (p == null || !Harm.On || !Harm.SystemOn(HarmKinds.CowardName.system)) return;
            var book = HarmBook.Instance;
            if (book == null || HarmSix.WaitingFor(book, p, HarmSix.Coward) != null) return;
            HarmFour.Wait(new HarmWaiting { what = HarmSix.Coward, kind = HarmKinds.CowardName.key, pawn = p, due = Now });
        }

        /// <summary>One of ours who could break from cover now: undrafted, armed, up and about, a grown-up who can fight.</summary>
        private static bool CanBreak(Pawn p) =>
            HarmFour.Ours(p) && !HarmFour.Child(p) && !p.Downed && !p.InMentalState && RestUtility.Awake(p) && !p.WorkTagIsDisabled(WorkTags.Violent) && p.equipment?.Primary != null;

        private static Pawn NearestEnemy(Pawn p, Faction of)
        {
            Pawn best = null;
            float bestD = HarmSixMath.BreakRange * HarmSixMath.BreakRange;
            foreach (var x in p.Map.mapPawns.AllPawnsSpawned)
            {
                if (x == null || x.Dead || x.Downed || x.IsPrisoner || x.RaceProps?.Humanlike != true || !x.HostileTo(Faction.OfPlayer) || of != null && x.Faction != of) continue;
                float d = x.Position.DistanceToSquared(p.Position);
                if (d >= bestD || !p.CanReach(x, PathEndMode.Touch, Danger.Deadly)) continue;
                best = x;
                bestD = d;
            }
            return best;
        }

        /// <summary>Every 250 ticks: a fight going on at a home, the coward's name and the avenger's fury.</summary>
        public static void Battles(HarmBook book, int now)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !Harm.On) return;
            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                var map = maps[i];
                if (!map.IsPlayerHome) continue;
                var battle = comp.social?.BattleAt(map);
                if (battle == null || !battle.active && !DebugBreak) continue;
                Cowards(book, map);
                Fury(comp, battle, map);
            }
        }

        private static void Cowards(HarmBook book, Map map)
        {
            if (book?.waiting == null || !Harm.SystemOn(HarmKinds.CowardName.system)) return;
            foreach (var x in book.waiting.Where(w => w != null && w.what == HarmSix.Coward && w.pawn != null && w.pawn.Spawned && w.pawn.Map == map).ToList())
            {
                var p = x.pawn;
                var enemy = NearestEnemy(p, null);
                if (enemy == null) continue;
                // The next fight they're in: drafted and held, it passes in cover.
                book.waiting.Remove(x);
                if (p.Drafted || !CanBreak(p)) continue;
                var marshal = OfficeTracker.For(map)?.HolderOf("Council_Office_Marshal");
                if (marshal != null && marshal != p && Together.Can(marshal, p))
                {
                    // together: Together.Can, the marshal at their side as the fight begins.
                    HarmFour.Say(marshal, "Rimfolk_Pool_MarshalWord", p);
                    continue;
                }
                bool debug = DebugBreak;
                DebugBreak = false;
                if (debug ? !Harm.May(p, HurtClass.Fight) : !Harm.Roll(p, HarmKinds.CowardName, HarmSixMath.CowardChance, HurtClass.Fight)) continue;
                Break(p, enemy, HarmKinds.CowardName, "Rimfolk_Pool_CowardBreaks");
            }
        }

        private static void Fury(CouncilGameComponent comp, Battle battle, Map map)
        {
            if (comp.vendettas?.vendettas == null || battle.faction == null || !Harm.SystemOn(HarmKinds.VendettaFury.system)) return;
            foreach (var v in comp.vendettas.vendettas)
            {
                if (v == null || !v.Active || v.faction != battle.faction || v.avenger?.Map != map || !v.avenger.Spawned) continue;
                var p = v.avenger;
                long key = (long)p.thingIDNumber * 1000003L + battle.countStart;
                if (furyLooked.Contains(key)) continue;
                var enemy = NearestEnemy(p, v.faction);
                if (enemy == null) continue;
                furyLooked.Add(key);
                // Drafted and held: they keep to their cover.
                if (p.Drafted || !CanBreak(p)) continue;
                bool debug = DebugBreak;
                DebugBreak = false;
                if (debug ? !Harm.May(p, HurtClass.Fight) : !Harm.Roll(p, HarmKinds.VendettaFury, HarmSixMath.FuryChance, HurtClass.Fight)) continue;
                Break(p, enemy, HarmKinds.VendettaFury, "Rimfolk_Pool_FuryBreaks");
            }
        }

        /// <summary>Out of cover at the enemy: a melee weapon's holder at them, a gun's or a bow's to within range of them and firing; the game's own fight after.</summary>
        private static void Break(Pawn p, Pawn enemy, HarmKind kind, string pool)
        {
            try
            {
                var weapon = p.equipment.Primary;
                Job job;
                if (!weapon.def.IsRangedWeapon)
                {
                    job = JobMaker.MakeJob(JobDefOf.AttackMelee, enemy);
                    job.expiryInterval = HarmSixMath.BreakTicks;
                    p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                }
                else
                {
                    float range = p.equipment.PrimaryEq?.PrimaryVerb?.verbProps?.range ?? 20f;
                    var from = enemy.Position.ToVector3Shifted();
                    var toward = (p.Position.ToVector3Shifted() - from).normalized * Mathf.Max(3f, range * HarmSixMath.BreakRangeShare);
                    var aim = (from + toward).ToIntVec3().ClampInsideMap(p.Map);
                    if (!CellFinder.TryFindRandomCellNear(aim, p.Map, 3, c => c.Standable(p.Map) && p.CanReach(c, PathEndMode.OnCell, Danger.Deadly), out var cell)) return;
                    var go = JobMaker.MakeJob(JobDefOf.Goto, cell);
                    go.locomotionUrgency = LocomotionUrgency.Sprint;
                    go.expiryInterval = HarmSixMath.BreakTicks;
                    p.jobs.StartJob(go, JobCondition.InterruptForced, null, false, true);
                    var shoot = JobMaker.MakeJob(JobDefOf.AttackStatic, enemy);
                    shoot.expiryInterval = HarmSixMath.BreakTicks;
                    p.jobs.jobQueue.EnqueueFirst(shoot);
                }
                Harm.Note(p, kind);
                // apart: shouted at the enemy, breaking cover.
                HarmFour.Say(p, pool);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm six: breaking cover", e);
            }
        }
    }
}
