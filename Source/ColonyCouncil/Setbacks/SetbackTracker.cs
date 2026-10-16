using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>A building of note one of the colony put up, remembered so its loss can be felt by whoever built it.</summary>
    public class BuiltRecord : IExposable
    {
        public int id;
        public Pawn builder;
        public int tick;
        public string label;
        public float work;
        public bool art;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref builder, "builder");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref work, "work");
            Scribe_Values.Look(ref art, "art");
        }
    }

    /// <summary>Who sowed a field (a growing zone or a hydroponics basin) lately, and with what.</summary>
    public class SownRecord : IExposable
    {
        public string key;
        public string plant;
        public List<Pawn> sowers = new List<Pawn>();
        public List<int> ticks = new List<int>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref plant, "plant");
            Scribe_Collections.Look(ref sowers, "sowers", LookMode.Reference);
            Scribe_Collections.Look(ref ticks, "ticks", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && (sowers == null || ticks == null || sowers.Count != ticks.Count))
            {
                sowers = new List<Pawn>();
                ticks = new List<int>();
            }
        }

        public void Note(Pawn p, int now)
        {
            int i = sowers.IndexOf(p);
            if (i >= 0)
            {
                sowers.RemoveAt(i);
                ticks.RemoveAt(i);
            }
            sowers.Add(p);
            ticks.Add(now);
            while (sowers.Count > 4)
            {
                sowers.RemoveAt(0);
                ticks.RemoveAt(0);
            }
        }
    }

    /// <summary>One colonist's lasting setbacks, as the hour's look found them (not saved: worked out again after a load).</summary>
    public class SetbackState
    {
        public int waitStage = -1;
        public string waitWish;
        public int waitTicks, grantedSince;

        public int dragStage = -1;
        public string dragDecree;
        public int dragTicks, dragLeft;

        public int sickStage = -1;
        public Pawn sick;
        public string sickWhat;

        public Pawn caravanOf;
        public string caravanWhy;

        public string nemesis;
        public int nemesisTicks;

        public string debtFaction;
        public string debtWhat;

        public bool envy;
        public Pawn envyOf;
        public string envyMine, envyBest;

        public int quietStage = -1;
        public int joyKinds, joyNeeded, quietTicks;

        public bool researchIdle;
        public int researchIdleTicks;
    }

    /// <summary>
    /// Keeps the setbacks (see <see cref="Setbacks"/>): once an hour it works out the lasting ones (a wish still waiting, a
    /// decree dragging on, a friend who is ill, a caravan in trouble, quiet days...), turns the crops lost since into
    /// moods, and looks at the night's noise, the day's long hauls, gatherings in the rain and anyone left waiting for a
    /// doctor; once a day it notices who was outshone, which friendships cooled and who had a hard week. What it has to
    /// remember (who built what, who sowed which field, who was best at what) is pruned as people and things go.
    /// </summary>
    public class SetbackTracker : GameComponent
    {
        private List<BuiltRecord> built = new List<BuiltRecord>();
        private List<SownRecord> sown = new List<SownRecord>();
        /// <summary>"ThingID|what": when they last had that setback (cooldowns).</summary>
        private Dictionary<string, int> last = new Dictionary<string, int>();
        /// <summary>Announced friendships ("ThingID|ThingID") at the last daily look.</summary>
        private List<string> friends;
        /// <summary>Skill defName: the ThingID of the colony's best at it at the last daily look.</summary>
        private Dictionary<string, string> bestAt = new Dictionary<string, string>();
        /// <summary>Map uniqueID: the last tick a party, gathering or ritual was going on there.</summary>
        private Dictionary<int, int> lastGathering = new Dictionary<int, int>();
        private int researchIdleSince = -1;
        /// <summary>Wishes granted lately: when each was voiced and when it came true, and whose it was (ThingID).</summary>
        private List<int> grantVoiced = new List<int>();
        private List<int> grantTicks = new List<int>();
        private List<string> grantWho = new List<string>();

        [Unsaved] private Dictionary<int, BuiltRecord> builtById;
        [Unsaved] private Dictionary<string, SownRecord> sownByKey;
        [Unsaved] private readonly Dictionary<string, CropLoss> losses = new Dictionary<string, CropLoss>();
        [Unsaved] private readonly Dictionary<Pawn, SetbackState> states = new Dictionary<Pawn, SetbackState>();
        [Unsaved] private int statesTick = -1;
        [Unsaved] private readonly Dictionary<int, int> hauls = new Dictionary<int, int>();
        [Unsaved] private int haulDay = -1;
        [Unsaved] private readonly Dictionary<int, int> tendWait = new Dictionary<int, int>();
        [Unsaved] private readonly HashSet<int> rainedLords = new HashSet<int>();

        private class CropLoss
        {
            public int count;
            public bool blight;
            public string plant;
            public int first;
        }

        private static CouncilSettings S => ColonyCouncilMod.Settings;
        private static int Now => Find.TickManager.TicksGame;

        private const int MaxBuilt = 800;
        private const int MaxSown = 120;

        private static SetbackTracker cached;
        private static Game cachedGame;

        public static SetbackTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<SetbackTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public SetbackTracker(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            statesTick = -1;
            states.Clear();
        }

        public override void GameComponentTick()
        {
            int t = Now;
            // After a load, the lasting ones are worked out again on the first quiet tick rather than waiting for the hour.
            if (statesTick < 0 && t % 60 == 17 && (S?.MoodEffects ?? false)) Safe("lasting setbacks", () => Refresh(t));
            if (!Sched.Hourly(t, Sched.Setbacks)) return;
            if (!(S?.MoodEffects ?? false)) return;
            Safe("hour", Hourly);
            if (Sched.FirstHourOfDay(t)) Safe("day", Daily);
        }

        private static void Safe(string context, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setbacks: " + context, e);
            }
        }

        // ---------------- Cooldowns ----------------

        /// <summary>Not had this one within <paramref name="days"/>; if so, it's noted as had now.</summary>
        public bool Ready(Pawn p, string what, float days)
        {
            if (p == null) return false;
            string key = p.ThingID + "|" + what;
            int now = Now;
            if (last.TryGetValue(key, out int t) && t <= now && now - t < days * GenDate.TicksPerDay) return false;
            last[key] = now;
            return true;
        }

        // ---------------- The hour ----------------

        private void Hourly()
        {
            int now = Now;
            Safe("lasting setbacks", () => Refresh(now));
            Safe("crops lost", () => Crops(now));
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                Safe("gatherings", () => Gatherings(map, now));
                Safe("noisy nights", () => Nights(map, now));
                Safe("long hauls", () => Hauls(map, now));
                Safe("waiting for a doctor", () => Tending(map, now));
            }
            Safe("research", () => Research(now));
        }

        // ---------------- Lasting setbacks ----------------

        /// <summary>A colonist's lasting setbacks, as the last look found them (none until the first look after a load).</summary>
        public SetbackState StateOf(Pawn p) => p != null && states.TryGetValue(p, out var s) ? s : null;

        private void Refresh(int now)
        {
            statesTick = now;
            states.Clear();
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            var everyone = Setbacks.Everyone();
            // Who is ill, and whose caravan is in trouble: worked out once, then asked about by those close to them.
            var ill = new List<(Pawn who, string what, bool deadly)>();
            var stranded = new List<(Pawn who, string why)>();
            foreach (var o in everyone)
            {
                var what = SeriousIllness(o, out bool deadly);
                if (what != null) ill.Add((o, what, deadly));
                var why = CaravanTrouble(o);
                if (why != null) stranded.Add((o, why));
            }
            var envy = new Dictionary<Map, (int stage, Pawn owner, string label)>();
            var joy = new Dictionary<Map, int>();
            foreach (var p in everyone)
            {
                var s = new SetbackState();
                try
                {
                    Waiting(comp, p, s, now);
                    Decrees(comp, p, s, now);
                    foreach (var (who, what, deadly) in ill)
                    {
                        if (who == p) continue;
                        int close = Setbacks.Closeness(p, who);
                        if (close <= 0) continue;
                        int stage = SetbackMath.SickStage(close >= 2, deadly);
                        if (stage > s.sickStage)
                        {
                            s.sickStage = stage;
                            s.sick = who;
                            s.sickWhat = what;
                        }
                    }
                    foreach (var (who, why) in stranded)
                    {
                        if (who == p || CaravanUtility.GetCaravan(p) == CaravanUtility.GetCaravan(who) || Setbacks.Closeness(p, who) <= 0) continue;
                        s.caravanOf = who;
                        s.caravanWhy = why;
                        break;
                    }
                    Nemesis(comp, p, s, now);
                    Debt(p, s);
                    Envy(comp, p, s, envy);
                    Quiet(comp, p, s, now, joy);
                    if (researchIdleSince >= 0 && now - researchIdleSince >= SetbackMath.ResearchIdleDays * GenDate.TicksPerDay && KeenResearcher(p))
                    {
                        s.researchIdle = true;
                        s.researchIdleTicks = now - researchIdleSince;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("setbacks for a colonist", e);
                }
                states[p] = s;
            }
        }

        /// <summary>A wish came true (from the council's own code): remembered a month, for those still waiting on theirs.</summary>
        public void Notify_Granted(Wish w)
        {
            if (w?.Beneficiary == null) return;
            int now = Now;
            grantVoiced.Add(w.createdTick);
            grantTicks.Add(now);
            grantWho.Add(w.Beneficiary.ThingID);
            while (grantTicks.Count > 0 && (now - grantTicks[0] > GenDate.TicksPerDay * 30 || grantTicks.Count > 60))
            {
                grantVoiced.RemoveAt(0);
                grantTicks.RemoveAt(0);
                grantWho.RemoveAt(0);
            }
        }

        /// <summary>Their own wish, not promised, waiting while wishes voiced after it came true for others.</summary>
        private void Waiting(CouncilGameComponent comp, Pawn p, SetbackState s, int now)
        {
            if (!S.enableWishes) return;
            var w = comp.OpenWishBy(p);
            if (w == null || w.promised || w.Beneficiary != p) return;
            // Favoritism already says it (a memory about the one who got theirs first).
            if (p.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(CouncilDefOf.Council_Favoritism) != null) return;
            int granted = 0;
            for (int i = 0; i < grantTicks.Count; i++)
                if (grantVoiced[i] > w.createdTick && grantTicks[i] > w.createdTick && grantWho[i] != p.ThingID)
                    granted++;
            int waited = now - w.createdTick;
            int stage = SetbackMath.WaitStage(waited / (float)GenDate.TicksPerDay, granted);
            if (stage < 0) return;
            s.waitStage = stage;
            s.waitWish = w.Label;
            s.waitTicks = waited;
            s.grantedSince = granted;
        }

        /// <summary>A decree in force they dislike, two days in or more.</summary>
        private static void Decrees(CouncilGameComponent comp, Pawn p, SetbackState s, int now)
        {
            foreach (var a in comp.decrees.active)
            {
                if (a?.def == null) continue;
                var r = a.def.ReactionOf(p);
                if (r != DecreeReaction.Dislike && r != DecreeReaction.Hate) continue;
                var req = comp.decrees.RequestFor(a.def);
                bool ignored = req != null && req.repeal && now - req.tick >= GenDate.TicksPerDay;
                int stage = SetbackMath.DragStage((now - a.startTick) / (float)GenDate.TicksPerDay, r == DecreeReaction.Hate, ignored);
                if (stage <= s.dragStage) continue;
                s.dragStage = stage;
                s.dragDecree = a.def.label;
                s.dragTicks = now - a.startTick;
                s.dragLeft = Math.Max(0, a.endTick - now);
            }
        }

        /// <summary>
        /// An illness that's a real worry: one of Rimfolk's serious illnesses, or a disease the body is still fighting
        /// that can kill (the plague, malaria, the flu). A cold isn't. Null when there's none.
        /// </summary>
        private static string SeriousIllness(Pawn p, out bool deadly)
        {
            deadly = false;
            var hediffs = p?.health?.hediffSet?.hediffs;
            if (hediffs == null) return null;
            string what = null;
            for (int i = 0; i < hediffs.Count; i++)
            {
                var h = hediffs[i];
                if (h?.def == null || !h.Visible) continue;
                var info = AilmentTracker.InfoOf(h.def);
                bool serious = info != null ? info.serious : h.def.lethalSeverity > 0f && h is HediffWithComps hc && hc.TryGetComp<HediffComp_Immunizable>() != null && !h.FullyImmune();
                if (!serious) continue;
                what = h.LabelBase;
                if (h.def.lethalSeverity > 0f && h.Severity >= h.def.lethalSeverity * 0.6f) deadly = true;
            }
            return what;
        }

        /// <summary>Why their caravan is a worry (short of food, someone in it down), or null.</summary>
        private static string CaravanTrouble(Pawn p)
        {
            var c = CaravanUtility.GetCaravan(p);
            if (c == null || !c.IsPlayerControlled) return null;
            var pawns = c.PawnsListForReading;
            for (int i = 0; i < pawns.Count; i++)
            {
                var o = pawns[i];
                if (o != null && o.IsColonist && o.Downed && !o.Dead) return "CC_Setback_Why_CaravanDown".Translate(o.Named("OTHER")).Resolve();
            }
            float days;
            try
            {
                days = c.DaysWorthOfFood.Item1;
            }
            catch
            {
                return null;
            }
            return days < SetbackMath.CaravanFoodDays ? "CC_Setback_Why_CaravanFood".Translate(days.ToString("0.#")).Resolve() : null;
        }

        /// <summary>The nemesis who hurt them is still out there, after the first shock has faded.</summary>
        private static void Nemesis(CouncilGameComponent comp, Pawn p, SetbackState s, int now)
        {
            if (!S.nemeses || comp.nemeses == null) return;
            if (Setbacks.HasTrait(p, "Psychopath") || Setbacks.TraitDegree(p, "Nerves") >= 1) return;
            foreach (var n in comp.nemeses.Of(p))
            {
                if (n == null || n.ended || n.captured) continue;
                int since = now - n.tick;
                if (!SetbackMath.NemesisWorry(since / (float)GenDate.TicksPerDay)) continue;
                if (s.nemesis == null || since < s.nemesisTicks)
                {
                    s.nemesis = n.Name;
                    s.nemesisTicks = since;
                }
            }
        }

        /// <summary>A kind colonist minds that the colony owes a people a good turn it hasn't returned.</summary>
        private static void Debt(Pawn p, SetbackState s)
        {
            if (!FactionMemory.On || p.story?.traits == null || !p.story.traits.HasTrait(TraitDefOf.Kind)) return;
            var mem = FactionMemory.Instance;
            foreach (var f in mem.Peoples())
            {
                if (f == null || f.defeated || f.HostileTo(Faction.OfPlayer)) continue;
                var d = mem.Best(f, DeedSide.TheirFavor, 5f, 15f, true);
                if (d == null) continue;
                // Good turns done for them since balance it out.
                if (mem.Sum(f, DeedSide.Favor) >= mem.Sum(f, DeedSide.TheirFavor, true)) continue;
                s.debtFaction = f.Name;
                s.debtWhat = FactionMemory.Text(d);
                return;
            }
        }

        /// <summary>Their bedroom is three or more grades worse than the best one at home (someone else's).</summary>
        private static void Envy(CouncilGameComponent comp, Pawn p, SetbackState s, Dictionary<Map, (int stage, Pawn owner, string label)> best)
        {
            var map = p.Map;
            if (map == null || !p.Spawned || !map.IsPlayerHome || p.DevelopmentalStage.Juvenile()) return;
            // The game's jealous already mind (a bedroom worse than someone's); ascetics don't care.
            if (Setbacks.HasTrait(p, "Jealous") || Setbacks.HasTrait(p, "Ascetic")) return;
            var w = comp.OpenWishBy(p);
            if (w?.def != null && w.def.defName.IndexOf("Bedroom", StringComparison.OrdinalIgnoreCase) >= 0) return;
            var stat = RoomStatDefOf.Impressiveness;
            if (!best.TryGetValue(map, out var b))
            {
                b = (-1, null, null);
                foreach (var o in map.mapPawns.FreeColonistsSpawned)
                {
                    var room = o.ownership?.OwnedRoom;
                    if (room == null || room.PsychologicallyOutdoors) continue;
                    float v = room.GetStat(stat);
                    int i = stat.GetScoreStageIndex(v);
                    if (i > b.stage) b = (i, o, stat.GetScoreStage(v)?.label);
                }
                best[map] = b;
            }
            if (b.owner == null || b.owner == p) return;
            // Sleeping out under the sky is the game's own mood already.
            var mine = RoomUtil.BedRoom(p);
            if (mine == null || mine.PsychologicallyOutdoors) return;
            float mv = mine.GetStat(stat);
            int mi = stat.GetScoreStageIndex(mv);
            if (!SetbackMath.Envious(mi, b.stage)) return;
            s.envy = true;
            s.envyOf = b.owner;
            s.envyMine = stat.GetScoreStage(mv)?.label;
            s.envyBest = b.label;
        }

        /// <summary>Long quiet days at home: less recreation to choose from than they expect, and no party or gathering in ten days.</summary>
        private void Quiet(CouncilGameComponent comp, Pawn p, SetbackState s, int now, Dictionary<Map, int> joy)
        {
            var map = p.Map;
            if (map == null || !p.Spawned || !map.IsPlayerHome || p.DevelopmentalStage.Juvenile()) return;
            if (!lastGathering.TryGetValue(map.uniqueID, out int since)) return;
            var w = comp.OpenWishBy(p);
            if (w?.def != null && w.def.defName.IndexOf("Recreation", StringComparison.OrdinalIgnoreCase) >= 0) return;
            if (!joy.TryGetValue(map, out int kinds)) joy[map] = kinds = Avail.JoyKindsOnMap(map);
            int needed = RoomUtil.Expectation(p)?.joyKindsNeeded ?? 3;
            int stage = SetbackMath.QuietStage(kinds, needed, (now - since) / (float)GenDate.TicksPerDay);
            if (stage < 0) return;
            s.quietStage = stage;
            s.joyKinds = kinds;
            s.joyNeeded = needed;
            s.quietTicks = now - since;
        }

        private static bool KeenResearcher(Pawn p)
        {
            var sk = p.skills?.GetSkill(SkillDefOf.Intellectual);
            if (sk == null || sk.TotallyDisabled || !People.Burning(sk)) return false;
            var research = WorkTypeDefOf.Research;
            return research != null && !p.WorkTypeIsDisabled(research) && p.MapHeld != null && p.MapHeld.IsPlayerHome;
        }

        /// <summary>Research stopped: a bench at home, projects to pick from, and none picked.</summary>
        private void Research(int now)
        {
            bool idle = false;
            var rm = Find.ResearchManager;
            if (rm != null && rm.GetProject() == null && rm.AnyProjectIsAvailable)
                foreach (var map in Find.Maps)
                    if (map.IsPlayerHome && Avail.ColonyHas(map, Avail.IsResearchBench))
                    {
                        idle = true;
                        break;
                    }
            if (!idle) researchIdleSince = -1;
            else if (researchIdleSince < 0 || researchIdleSince > now) researchIdleSince = now;
        }

        // ---------------- Crops ----------------

        /// <summary>A field's key: its growing zone, or the hydroponics basin (or other grower) at the cell.</summary>
        public static string FieldKey(Map map, IntVec3 cell)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map)) return null;
            if (map.zoneManager.ZoneAt(cell) is Zone_Growing z) return map.uniqueID + "|z" + z.ID;
            var grower = cell.GetEdifice(map);
            if (grower is Building_PlantGrower) return map.uniqueID + "|b" + grower.thingIDNumber;
            return null;
        }

        private SownRecord Sown(string key, bool create)
        {
            if (sownByKey == null)
            {
                sownByKey = new Dictionary<string, SownRecord>();
                foreach (var r in sown)
                    if (r?.key != null) sownByKey[r.key] = r;
            }
            if (sownByKey.TryGetValue(key, out var rec) || !create) return rec;
            rec = new SownRecord { key = key };
            sown.Add(rec);
            sownByKey[key] = rec;
            if (sown.Count > MaxSown)
            {
                var oldest = sown.OrderBy(x => x.ticks.Count == 0 ? 0 : x.ticks[x.ticks.Count - 1]).First();
                sown.Remove(oldest);
                sownByKey.Remove(oldest.key);
            }
            return rec;
        }

        /// <summary>A colonist sowed a plant (from the records hook).</summary>
        public void Notify_Sown(Pawn p, Map map, IntVec3 cell, ThingDef plant)
        {
            if (p == null || !p.IsColonist) return;
            string key = FieldKey(map, cell);
            if (key == null) return;
            var rec = Sown(key, true);
            rec.Note(p, Now);
            if (plant != null) rec.plant = CropName(plant);
        }

        /// <summary>What the field grows, by its harvest ("rice", "potatoes") rather than the plant ("rice plant").</summary>
        private static string CropName(ThingDef plant) => plant?.plant?.harvestedThingDef?.label ?? plant?.label;

        /// <summary>A sown crop was lost: blighted, or killed by frost.</summary>
        public void Notify_CropLost(Plant plant, bool blight)
        {
            if (plant?.Map == null) return;
            string key = FieldKey(plant.Map, plant.Position);
            if (key == null || Sown(key, false) == null) return;
            if (!losses.TryGetValue(key, out var l)) losses[key] = l = new CropLoss { blight = blight, plant = CropName(plant.def), first = Now };
            l.count++;
            if (!blight) l.blight = false;
        }

        private void Crops(int now)
        {
            if (losses.Count == 0) return;
            foreach (var kv in losses.ToList())
            {
                var l = kv.Value;
                var rec = Sown(kv.Key, false);
                if (rec == null || now - l.first > GenDate.TicksPerDay)
                {
                    losses.Remove(kv.Key);
                    continue;
                }
                if (l.count < SetbackMath.CropMinLost) continue;
                losses.Remove(kv.Key);
                string cause = (l.blight ? "CC_Setback_Cause_Blight" : "CC_Setback_Cause_Frost").Translate(l.plant ?? rec.plant ?? "?", l.count).Resolve();
                for (int i = 0; i < rec.sowers.Count; i++)
                {
                    var p = rec.sowers[i];
                    if (p == null || !Setbacks.Eligible(p) || now - rec.ticks[i] > GenDate.TicksPerDay * 60) continue;
                    if (!Ready(p, "crop|" + kv.Key, 5f)) continue;
                    var sk = p.skills?.GetSkill(SkillDefOf.Plants);
                    bool keen = sk != null && People.Passionate(sk);
                    int stage = SetbackMath.CropStage(l.count, l.blight, keen);
                    if (stage < 0 || !Setbacks.Give(p, SetbackDefOf.Council_Setback_CropLost, null, cause, stage)) continue;
                    Setbacks.Say(p, "Council_Pool_Setback_CropLost", (l.plant ?? "?").Named("THING"));
                    Setbacks.Write(p, "Council_Journal_Setback_CropLost", stage == 2 ? 0.8f : 0.4f, (l.plant ?? "?").Named("THING"), l.count.Named("COUNT"),
                        (l.blight ? "CC_Setback_Blight" : "CC_Setback_Frost").Translate().Named("SUBJECT"));
                }
            }
        }

        // ---------------- Buildings ----------------

        private BuiltRecord BuiltRec(int id)
        {
            if (builtById == null)
            {
                builtById = new Dictionary<int, BuiltRecord>();
                foreach (var r in built)
                    if (r != null) builtById[r.id] = r;
            }
            return builtById.TryGetValue(id, out var rec) ? rec : null;
        }

        /// <summary>Only buildings someone would mind losing: real work (not walls, doors, fences, turrets or traps), or art.</summary>
        public static bool Notable(Thing t, out float work, out bool art)
        {
            work = 0f;
            art = false;
            var d = t?.def;
            if (d?.building == null || d.category != ThingCategory.Building) return false;
            art = t.TryGetComp<CompArt>() != null;
            if (art) return true;
            if (d.IsWall || d.IsDoor || d.IsFence || d.building.isTrap || d.building.IsTurret || d.building.isNaturalRock || d.IsFrame) return false;
            work = d.GetStatValueAbstract(StatDefOf.WorkToBuild, t.Stuff);
            return work >= SetbackMath.NotableWork;
        }

        /// <summary>A colonist finished building something (from the construction hook).</summary>
        public void Notify_Built(Pawn worker, Thing t)
        {
            if (worker == null || t == null || !worker.IsColonist || !Notable(t, out float work, out bool art)) return;
            if (BuiltRec(t.thingIDNumber) != null) return;
            var rec = new BuiltRecord { id = t.thingIDNumber, builder = worker, tick = Now, label = t.LabelNoCount, work = work, art = art };
            built.Add(rec);
            builtById[rec.id] = rec;
            if (built.Count > MaxBuilt)
            {
                builtById.Remove(built[0].id);
                built.RemoveAt(0);
            }
        }

        /// <summary>A building is being destroyed: if someone of ours built it, they mind (more if it was wrecked than taken down).</summary>
        public void Notify_Gone(Building b, DestroyMode mode)
        {
            if (b == null || (builtById == null && built.Count == 0)) return;
            var rec = BuiltRec(b.thingIDNumber);
            if (rec == null) return;
            built.Remove(rec);
            builtById.Remove(rec.id);
            var p = rec.builder;
            if (p == null || !Setbacks.Eligible(p)) return;
            var map = b.Map;
            if (mode == DestroyMode.Deconstruct)
            {
                // Taking it down to build something better in its place, or the builder taking down their own: no grief.
                if (map != null && (Replaced(b, map) || Deconstructor(b, map, out bool forBlueprint) == p || forBlueprint)) return;
                if (Now - rec.tick > GenDate.TicksPerDay * 60) return;
                if (!Ready(p, "tornDown", 1f)) return;
                Setbacks.Give(p, SetbackDefOf.Council_Setback_WorkTornDown, null, "CC_Setback_Cause_TornDown".Translate(rec.label).Resolve());
                return;
            }
            if (mode != DestroyMode.KillFinalize) return;
            // A raid can take down a dozen things at once: one mood (the worst), not a dozen.
            int stage = SetbackMath.WreckStage(rec.work, rec.art);
            string cause = "CC_Setback_Cause_Wrecked".Translate(rec.label).Resolve();
            if (!Ready(p, "wrecked" + stage, 1f)) return;
            if (!Setbacks.Give(p, SetbackDefOf.Council_Setback_WorkWrecked, null, cause, stage)) return;
            if (Ready(p, "wreckedTalk", 2f))
            {
                Setbacks.Say(p, "Council_Pool_Setback_Wrecked", rec.label.Named("THING"));
                Setbacks.Write(p, "Council_Journal_Setback_Wrecked", stage == 1 ? 0.9f : 0.5f, rec.label.Named("THING"));
            }
        }

        /// <summary>Who is taking it down right now (the one whose job it is), or null; and whether it's to make way for a blueprint.</summary>
        private static Pawn Deconstructor(Thing b, Map map, out bool forBlueprint)
        {
            forBlueprint = false;
            foreach (var o in map.mapPawns.FreeColonistsSpawned)
            {
                var job = o.CurJob;
                if (job == null || job.targetA.Thing != b) continue;
                if (job.def == JobDefOf.DeconstructForBlueprint)
                {
                    forBlueprint = true;
                    return o;
                }
                if (job.def == JobDefOf.Deconstruct) return o;
            }
            return null;
        }

        /// <summary>A blueprint or frame of something new stands where it is: it's being replaced, not lost.</summary>
        private static bool Replaced(Thing b, Map map)
        {
            foreach (var cell in b.OccupiedRect())
            {
                if (!cell.InBounds(map)) continue;
                var list = cell.GetThingList(map);
                for (int i = 0; i < list.Count; i++)
                    if (list[i] is Blueprint || (list[i] is Frame && list[i] != b)) return true;
            }
            return false;
        }

        // ---------------- Gatherings ----------------

        /// <summary>Parties and gatherings (the game's, Rimfolk's, rituals): when there last was one, and whether it's raining on one now.</summary>
        private void Gatherings(Map map, int now)
        {
            if (!lastGathering.ContainsKey(map.uniqueID)) lastGathering[map.uniqueID] = now;
            var lords = map.lordManager?.lords;
            if (lords == null) return;
            var weather = map.weatherManager;
            bool wet = weather != null && weather.RainRate >= 0.3f && map.mapTemperature.OutdoorTemp > 0f;
            foreach (var lord in lords)
            {
                var job = lord?.LordJob;
                if (job is LordJob_Ritual)
                {
                    lastGathering[map.uniqueID] = now;
                    continue;
                }
                if (!(job is LordJob_Joinable_Gathering g)) continue;
                lastGathering[map.uniqueID] = now;
                if (!wet) continue;
                var spot = g.Spot;
                if (!spot.IsValid || !spot.InBounds(map) || spot.Roofed(map) || !rainedLords.Add(lord.loadID)) continue;
                if (rainedLords.Count > 200) rainedLords.Clear();
                string what = job is LordJob_PlayOut po && !po.label.NullOrEmpty() ? po.label
                    : (job.GetType().Name.IndexOf("Marriage", StringComparison.Ordinal) >= 0 ? "CC_Setback_TheWedding" : "CC_Setback_TheParty").Translate().ToString();
                string cause = "CC_Setback_Cause_Rained".Translate(what).Resolve();
                foreach (var p in lord.ownedPawns.ToList())
                {
                    if (p == null || !p.Spawned || p.Position.Roofed(map) || !Ready(p, "rained", 1f)) continue;
                    if (Setbacks.Give(p, SetbackDefOf.Council_Setback_RainedOut, null, cause) && Rand.Chance(0.3f))
                        Setbacks.Say(p, "Council_Pool_Setback_Rained");
                }
            }
        }

        // ---------------- Noisy nights ----------------

        private static readonly HashSet<string> NoisyWork = new HashSet<string> { "Construction", "Mining", "Smithing", "Crafting", "Repair" };

        /// <summary>At night: someone asleep in their own bed, and someone else at loud work just through the wall.</summary>
        private void Nights(Map map, int now)
        {
            if (!Setbacks.On(SetbackDefOf.Council_Setback_NoisyNeighbour)) return;
            int hour = GenLocalDate.HourOfDay(map);
            if (hour >= 5 && hour < 23) return;
            var people = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < people.Count; i++)
            {
                var p = people[i];
                if (p.Awake() || !Setbacks.Eligible(p)) continue;
                var bed = RestUtility.CurrentBed(p);
                if (bed == null || !bed.OwnersForReading.Contains(p)) continue;
                var room = bed.GetRoom();
                if (room == null || room.PsychologicallyOutdoors) continue;
                for (int j = 0; j < people.Count; j++)
                {
                    var o = people[j];
                    if (o == p || !o.Awake() || (o.Position - bed.Position).LengthHorizontalSquared > 49) continue;
                    var work = o.CurJob?.workGiverDef?.workType;
                    if (work == null || !NoisyWork.Contains(work.defName) || o.GetRoom() == room) continue;
                    if (!MomentTones.Allows("friction", p, o) || !Ready(p, "noise", 1f)) break;
                    if (Setbacks.Give(p, SetbackDefOf.Council_Setback_NoisyNeighbour, o, "CC_Setback_Cause_Noise".Translate(o.LabelShort, work.gerundLabel ?? work.label).Resolve()))
                        MomentTones.RecordEvent("friction", p, o, 6f);
                    break;
                }
            }
        }

        // ---------------- Long hauls ----------------

        private void Hauls(Map map, int now)
        {
            if (!Setbacks.On(SetbackDefOf.Council_Setback_LongHauls)) return;
            int day = now / GenDate.TicksPerDay;
            if (day != haulDay)
            {
                haulDay = day;
                hauls.Clear();
            }
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                var job = p.CurJob;
                if (job == null || (job.def != JobDefOf.HaulToCell && job.def != JobDefOf.HaulToContainer) || !Setbacks.Eligible(p)) continue;
                var dest = job.targetB.Cell;
                if (!dest.IsValid) continue;
                var from = p.carryTracker?.CarriedThing != null ? p.Position : job.targetA.Cell;
                if (!from.IsValid || !SetbackMath.LongHaul((int)(from - dest).LengthHorizontal)) continue;
                hauls.TryGetValue(p.thingIDNumber, out int n);
                hauls[p.thingIDNumber] = ++n;
                if (n == SetbackMath.LongHaulSamples && Ready(p, "hauls", 2f))
                    Setbacks.Give(p, SetbackDefOf.Council_Setback_LongHauls, null, "CC_Setback_Cause_Hauls".Translate().Resolve());
            }
        }

        // ---------------- Waiting for a doctor ----------------

        private void Tending(Map map, int now)
        {
            if (!Setbacks.On(SetbackDefOf.Council_Setback_LeftWaiting)) return;
            bool calm = map.dangerWatcher.DangerRating == StoryDanger.None;
            var people = map.mapPawns.FreeColonistsSpawned;
            foreach (var p in people)
            {
                bool waiting = calm && (p.Downed || p.InBed()) && p.health.HasHediffsNeedingTendByPlayer() && Setbacks.Eligible(p) && DoctorFree(people, p);
                if (!waiting)
                {
                    tendWait.Remove(p.thingIDNumber);
                    continue;
                }
                tendWait.TryGetValue(p.thingIDNumber, out int n);
                tendWait[p.thingIDNumber] = ++n;
                if (n < SetbackMath.TendWaitHours) continue;
                tendWait.Remove(p.thingIDNumber);
                if (Ready(p, "tend", 2f)) Setbacks.Give(p, SetbackDefOf.Council_Setback_LeftWaiting, null, "CC_Setback_Cause_Tend".Translate(n).Resolve());
            }
        }

        /// <summary>Someone else at home could doctor: up, not in a sickbed, with doctoring switched on.</summary>
        private static bool DoctorFree(List<Pawn> people, Pawn patient)
        {
            var doctor = WorkTypeDefOf.Doctor;
            foreach (var o in people)
                if (o != patient && !o.Downed && !o.InBed() && !o.Drafted && o.workSettings != null && o.workSettings.EverWork && o.workSettings.WorkIsActive(doctor))
                    return true;
            return false;
        }

        // ---------------- The day ----------------

        private void Daily()
        {
            Safe("outshone", Outshone);
            Safe("friendships", Friendships);
            Safe("hard weeks", HardWeeks);
            Safe("pruning", Prune);
        }

        /// <summary>Someone else became the colony's best at a passion of theirs (joined, or learned past them).</summary>
        private void Outshone()
        {
            var people = Setbacks.Everyone().Where(p => p.skills != null && Setbacks.Somewhere(p)).ToList();
            bool first = bestAt.Count == 0;
            foreach (var skill in DefDatabase<SkillDef>.AllDefsListForReading)
            {
                Pawn best = null;
                int bestLevel = -1;
                bestAt.TryGetValue(skill.defName, out string prevId);
                Pawn prev = null;
                foreach (var p in people)
                {
                    var r = p.skills.GetSkill(skill);
                    if (r == null || r.TotallyDisabled) continue;
                    if (p.ThingID == prevId) prev = p;
                    // Ties stay with whoever was best already.
                    if (r.Level > bestLevel || (r.Level == bestLevel && p.ThingID == prevId))
                    {
                        best = p;
                        bestLevel = r.Level;
                    }
                }
                if (best == null) continue;
                bestAt[skill.defName] = best.ThingID;
                if (first || prev == null || best == prev) continue;
                var mine = prev.skills.GetSkill(skill);
                if (mine == null || !People.Passionate(mine) || mine.Level < 6 || bestLevel <= mine.Level) continue;
                if (prev.story?.traits?.HasTrait(TraitDefOf.Kind) ?? false) continue;
                // Their own student: that's pride, not envy.
                var mentor = S.socialWeb ? CouncilGameComponent.Instance?.social?.Get(prev, best, TieKind.Mentor) : null;
                if (mentor != null && mentor.a == prev) continue;
                if (!Ready(prev, "outshone|" + skill.defName, 10f)) continue;
                if (Setbacks.Give(prev, SetbackDefOf.Council_Setback_Outshone, best, "CC_Setback_Cause_Outshone".Translate(best.LabelShort, skill.label, bestLevel, mine.Level).Resolve()))
                    Setbacks.Write(prev, "Council_Journal_Setback_Outshone", 0.5f, best.Named("OTHER"), skill.label.Named("SKILL"));
            }
        }

        private static string PairKey(Pawn a, Pawn b) => a.thingIDNumber < b.thingIDNumber ? a.ThingID + "|" + b.ThingID : b.ThingID + "|" + a.ThingID;

        /// <summary>A friendship that was there yesterday is gone today, and both are still here: it cooled.</summary>
        private void Friendships()
        {
            var web = CouncilGameComponent.Instance?.social;
            if (web == null || !S.socialWeb)
            {
                // Bonds aren't kept while the social web is off: start afresh when it's back.
                friends = null;
                return;
            }
            var now = new Dictionary<string, (Pawn, Pawn)>();
            foreach (var t in web.ties)
                if (t != null && t.kind == TieKind.Friends && t.announced && t.a != null && t.b != null && !t.a.Dead && !t.b.Dead)
                    now[PairKey(t.a, t.b)] = (t.a, t.b);
            if (friends != null)
            {
                var people = Setbacks.Everyone().ToDictionary(p => p.ThingID);
                foreach (var key in friends)
                {
                    if (now.ContainsKey(key)) continue;
                    int bar = key.IndexOf('|');
                    if (bar <= 0 || !people.TryGetValue(key.Substring(0, bar), out var a) || !people.TryGetValue(key.Substring(bar + 1), out var b)) continue;
                    foreach (var (x, y) in new[] { (a, b), (b, a) })
                    {
                        if (!Ready(x, "drifted|" + y.ThingID, 10f)) continue;
                        if (Setbacks.Give(x, SetbackDefOf.Council_Setback_DriftedApart, y, "CC_Setback_Cause_Drifted".Translate(y.LabelShort).Resolve()))
                            Setbacks.Write(x, "Council_Journal_Setback_Drifted", 0.6f, y.Named("OTHER"));
                    }
                }
            }
            friends = now.Keys.ToList();
        }

        /// <summary>A hard week (three or more hard memories): someone who came from elsewhere thinks of home.</summary>
        private void HardWeeks()
        {
            if (!Setbacks.On(SetbackDefOf.Council_Setback_Homesick)) return;
            var timeHere = DefDatabase<RecordDef>.GetNamedSilentFail("TimeAsColonistOrColonyAnimal");
            int week = GenDate.TicksPerDay * 7;
            foreach (var p in Setbacks.Everyone())
            {
                if (p.DevelopmentalStage.Juvenile() || p.needs?.mood?.thoughts?.memories == null) continue;
                // Born here (or here most of their life): this is home.
                if (timeHere != null && p.ageTracker != null && p.records.GetValue(timeHere) >= p.ageTracker.AgeBiologicalTicks * 0.6f) continue;
                var hard = new HashSet<ThoughtDef>();
                foreach (var m in p.needs.mood.thoughts.memories.Memories)
                {
                    if (m?.def == null || m.age > week || m.def == SetbackDefOf.Council_Setback_Homesick) continue;
                    float mood;
                    try
                    {
                        mood = m.MoodOffset();
                    }
                    catch
                    {
                        continue;
                    }
                    if (mood <= -5f) hard.Add(m.def);
                }
                if (!SetbackMath.HardWeek(hard.Count) || !Ready(p, "homesick", 10f)) continue;
                if (Setbacks.Give(p, SetbackDefOf.Council_Setback_Homesick, null, "CC_Setback_Cause_Homesick".Translate(hard.Count).Resolve()))
                    Setbacks.Write(p, "Council_Journal_Setback_Homesick", 0.7f);
            }
        }

        private void Prune()
        {
            int now = Now;
            last.RemoveAll(kv => kv.Value > now || now - kv.Value > GenDate.TicksPerDay * 20);
            CouncilGameComponent.PruneByPawn(last);
            built.RemoveAll(r => r == null || r.builder == null || r.builder.Dead || !People.StillOurs(r.builder));
            builtById = null;
            foreach (var r in sown)
            {
                for (int i = r.sowers.Count - 1; i >= 0; i--)
                    if (r.sowers[i] == null || r.sowers[i].Dead || now - r.ticks[i] > GenDate.TicksPerDay * 90)
                    {
                        r.sowers.RemoveAt(i);
                        r.ticks.RemoveAt(i);
                    }
            }
            sown.RemoveAll(r => r == null || r.sowers.Count == 0);
            sownByKey = null;
            var maps = new HashSet<int>(Find.Maps.Select(m => m.uniqueID));
            foreach (var k in lastGathering.Keys.Where(k => !maps.Contains(k)).ToList()) lastGathering.Remove(k);
        }

        // ---------------- Saving ----------------

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                built.RemoveAll(r => r == null || r.builder == null || r.builder.Dead);
                sown.RemoveAll(r => r == null || r.sowers.Count == 0);
            }
            Scribe_Collections.Look(ref built, "built", LookMode.Deep);
            Scribe_Collections.Look(ref sown, "sown", LookMode.Deep);
            Scribe_Collections.Look(ref last, "last", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref friends, "friends", LookMode.Value);
            Scribe_Collections.Look(ref bestAt, "bestAt", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref lastGathering, "lastGathering", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref researchIdleSince, "researchIdleSince", -1);
            Scribe_Collections.Look(ref grantVoiced, "grantVoiced", LookMode.Value);
            Scribe_Collections.Look(ref grantTicks, "grantTicks", LookMode.Value);
            Scribe_Collections.Look(ref grantWho, "grantWho", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (built == null) built = new List<BuiltRecord>();
                built.RemoveAll(r => r == null);
                if (sown == null) sown = new List<SownRecord>();
                sown.RemoveAll(r => r == null || r.key == null);
                if (last == null) last = new Dictionary<string, int>();
                if (bestAt == null) bestAt = new Dictionary<string, string>();
                if (lastGathering == null) lastGathering = new Dictionary<int, int>();
                if (grantVoiced == null || grantTicks == null || grantWho == null || grantVoiced.Count != grantTicks.Count || grantWho.Count != grantTicks.Count)
                {
                    grantVoiced = new List<int>();
                    grantTicks = new List<int>();
                    grantWho = new List<string>();
                }
                builtById = null;
                sownByKey = null;
                statesTick = -1;
            }
        }
    }
}
