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
    /// A war party on the colony's map: fighters of a people at war with whoever is attacking the colony, come to
    /// fight them. They hunt the enemy (the game's own duty); once the fight is over they make camp to rest a day or
    /// two (a camp of the kind that fights, Council_Camp_WarParty) or, with a camp already on the land, go home.
    /// </summary>
    public class WarBand : IExposable
    {
        public Faction faction, enemy;
        public Map map;
        public Lord lord;
        public List<Pawn> pawns = new List<Pawn>();
        public int startTick;
        public int came;
        public bool holdBack, dismissed;

        private static int Now => Find.TickManager.TicksGame;

        public List<Pawn> Here => pawns.Where(p => p != null && !p.Dead && p.Spawned && p.Map == map).ToList();

        public bool LordAlive => lord != null && map != null && Find.Maps.Contains(map) && map.lordManager.lords.Contains(lord);

        public float Days => (Now - startTick) / (float)GenDate.TicksPerDay;

        public int Fell => came - pawns.Count(p => p != null && !p.Dead);

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                pawns.RemoveAll(p => p == null || p.Discarded);
                if (lord != null && !LordAlive) lord = null;
                if (map != null && !Find.Maps.Contains(map)) map = null;
            }
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref enemy, "enemy");
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref lord, "lord");
            Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref came, "came");
            Scribe_Values.Look(ref holdBack, "holdBack");
            Scribe_Values.Look(ref dismissed, "dismissed");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pawns ??= new List<Pawn>();
                pawns.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// Help in a fight from the colony's friends. Each hour while a threat is on the home map and has lasted an hour
    /// (CampStoryMath.WarAfterHours), once per threat, a people at peace with the colony and at war with the attacker
    /// may send fighters (CampStoryMath.WarChance by their goodwill, likelier with an old quarrel): the event
    /// Council_Evt_WarCamp, a letter whose answers are to welcome them, to ask them to hold at the edge, or to send
    /// them away. When the fight is over they make camp and rest, and their people remember the colony owes them.
    /// </summary>
    public static class WarCamps
    {
        private static int Now => Find.TickManager.TicksGame;

        public static void Hourly(Map map, CampTracker tracker)
        {
            if (map == null || tracker == null) return;
            if (tracker.warBand != null)
            {
                Watch(tracker.warBand, tracker);
                return;
            }
            bool threat = MapFacts.ThreatOn(map);
            if (!threat)
            {
                tracker.threatSince = -1;
                tracker.threatRolled = false;
                return;
            }
            if (tracker.threatSince < 0) tracker.threatSince = Now;
            if (tracker.threatRolled || Now - tracker.threatSince < GenDate.TicksPerHour * CampStoryMath.WarAfterHours) return;
            tracker.threatRolled = true;
            if (!CampTracker.Enabled || !CanCome(map, out var enemy, out var helper)) return;
            // An ally answers the call as an old foe of the attacker would: surely enough to be counted on.
            if (!Rand.Chance(CampStoryMath.WarChance(helper.PlayerGoodwill, Allies.IsAlly(helper) || (enemy.def.humanlikeFaction && helper.HostileTo(enemy))))) return;
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail("Council_Evt_WarCamp");
            if (def?.Worker == null) return;
            var parms = StorytellerUtility.DefaultParmsNow(def.category ?? IncidentCategoryDefOf.Misc, map);
            parms.forced = true;
            if (def.Worker.CanFireNow(parms)) def.Worker.TryExecute(parms);
        }

        /// <summary>A people is attacking (not animals), and another, at peace with the colony and at war with them, could come.</summary>
        public static bool CanCome(Map map, out Faction enemy, out Faction helper)
        {
            enemy = Attacker(map);
            helper = enemy == null ? null : Helper(enemy);
            return enemy != null && helper != null;
        }

        /// <summary>Whose people most of the hostiles on the map are (mechanoids count as a people; wild animals don't).</summary>
        public static Faction Attacker(Map map)
        {
            if (map?.attackTargetsCache == null) return null;
            var counts = new Dictionary<Faction, int>();
            foreach (var t in map.attackTargetsCache.TargetsHostileToColony)
            {
                var p = t.Thing as Pawn;
                var f = t.Thing?.Faction;
                if (f == null || f.IsPlayer || !f.HostileTo(Faction.OfPlayer) || (p != null && (p.Dead || p.Downed))) continue;
                if (p != null && p.RaceProps != null && p.RaceProps.Animal && !f.def.isPlayer && !f.def.humanlikeFaction && f != Faction.OfMechanoids) continue;
                counts[f] = (counts.TryGetValue(f, out int n) ? n : 0) + 1;
            }
            return counts.Count == 0 ? null : counts.OrderByDescending(kv => kv.Value).First().Key;
        }

        /// <summary>A people at peace with the colony that can send fighters: the colony's allies first (World/Allies.cs), then those at odds with the attacker.</summary>
        public static Faction Helper(Faction enemy)
        {
            var tech = Avail.EffectiveTech;
            var ally = Allies.Of();
            if (ally != null && ally != enemy && CanSendFighters(ally)) return ally;
            return Find.FactionManager.AllFactionsListForReading
                .Where(f => f != enemy && !f.IsPlayer && !f.Hidden && !f.defeated && !f.temporary && f.def.humanlikeFaction && !f.HostileTo(Faction.OfPlayer)
                            && f.PlayerGoodwill >= 0 && CanSendFighters(f))
                .RandomElementByWeightWithFallback(f => CampStoryMath.HelperWeight(f.PlayerGoodwill, f.HostileTo(enemy)) * Arrivals.TechWeight(f, tech));
        }

        private static bool CanSendFighters(Faction f) => f?.def?.pawnGroupMakers != null && f.def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Combat);

        /// <summary>Fighters of theirs, as their people fight (the game's own war bands), about half as strong as the threat.</summary>
        public static List<Pawn> MakeFighters(Faction f, Map map)
        {
            float threat = StorytellerUtility.DefaultThreatPointsNow(map);
            int max = CampStoryMath.Fighters(threat);
            var parms = new PawnGroupMakerParms { groupKind = PawnGroupKindDefOf.Combat, faction = f, points = CampStoryMath.WarPoints(threat), tile = map.Tile };
            var list = new List<Pawn>();
            try
            {
                foreach (var p in PawnGroupMakerUtility.GeneratePawns(parms, false))
                {
                    if (list.Count >= max)
                    {
                        Arrivals.Discard(p);
                        continue;
                    }
                    list.Add(p);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("war party people", e);
            }
            return list;
        }

        /// <summary>They walk in from an edge and hunt the enemy.</summary>
        public static WarBand Start(Map map, Faction f, Faction enemy, List<Pawn> pawns)
        {
            var tracker = CampTracker.Instance;
            if (tracker == null || tracker.warBand != null || map == null || pawns.NullOrEmpty()) return null;
            if (!Arrivals.TryEntryCell(map, out var entry)) return null;
            foreach (var p in pawns)
            {
                if (p.Spawned) continue;
                var cell = CellFinder.RandomClosewalkCellNear(entry, map, 4);
                GenSpawn.Spawn(p, cell, map);
            }
            var band = new WarBand { faction = f, enemy = enemy, map = map, pawns = pawns.ToList(), startTick = Now, came = pawns.Count };
            try
            {
                band.lord = LordMaker.MakeNewLord(f, new LordJob_WarBand(entry), map, pawns);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("war party group", e);
                Arrivals.SendAway(pawns);
                return null;
            }
            tracker.warBand = band;
            return band;
        }

        public static void HoldBack(WarBand b)
        {
            if (b == null) return;
            b.holdBack = true;
            if (b.LordAlive) b.lord.ReceiveMemo(LordJob_WarBand.MemoHold);
        }

        /// <summary>Sent away: they go, and think a little less of the colony.</summary>
        public static void Dismiss(WarBand b)
        {
            if (b == null) return;
            b.dismissed = true;
            if (b.LordAlive) b.lord.ReceiveMemo(LordJob_WarBand.MemoLeave);
            if (b.faction != null) FactionMemory.Note(b.faction, "slighted", null, 4);
            if (CampTracker.Instance != null) CampTracker.Instance.warBand = null;
        }

        private static void Watch(WarBand b, CampTracker tracker)
        {
            if (b.map == null || !Find.Maps.Contains(b.map) || !b.LordAlive || b.Here.Count == 0)
            {
                tracker.warBand = null;
                return;
            }
            if (b.dismissed) return;
            bool threat = MapFacts.ThreatOn(b.map);
            if (threat)
            {
                // A fight that drags on (a siege they can't break): they give up and go home.
                if (b.Days >= CampStoryMath.FightGiveUpDays) GoHome(b, tracker, "CC_WarCamp_GaveUp");
                return;
            }
            // The fight is over: a camp to rest in, or the road home.
            Settle(b, tracker);
        }

        private static void Settle(WarBand b, CampTracker tracker)
        {
            var map = b.map;
            Thank(b);
            var kind = DefDatabase<CampKindDef>.GetNamedSilentFail("Council_Camp_WarParty");
            // The hurt who can't walk come along in the camp's keeping, carried in spirit; those on their feet first (the first speaks for them).
            var here = b.Here.OrderBy(p => p.Downed ? 1 : 0).ToList();
            if (kind == null || tracker.camp != null || here.Count == 0 || here.All(p => p.Downed) || CampWeather.Hazard(map) != null || CampWeatherMath.TooExtremeToCome(map.mapTemperature.OutdoorTemp)
                || !CampSite.TryFind(map, out var center))
            {
                GoHome(b, tracker, "CC_WarCamp_Gone");
                return;
            }
            var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, b.faction, false, null, null, here);
            if (setup == null)
            {
                GoHome(b, tracker, "CC_WarCamp_Gone");
                return;
            }
            setup.onMap = true;
            // Out of the war band's group before they join the camp's.
            foreach (var p in here) b.lord.RemovePawn(p);
            var camp = tracker.Start(setup);
            if (camp == null)
            {
                tracker.warBand = null;
                Arrivals.SendAway(here);
                return;
            }
            camp.enemy = b.enemy;
            camp.returns = 0;
            string body = camp.Text("CC_WarCamp_Rest") + "\n\n" + "CC_WarCamp_Facts".Translate(b.came, b.Fell, (b.enemy?.Name ?? "").Named("ENEMY")).Resolve() + " " + camp.Text("CC_Camp_Facts_Talk");
            camp.Tell(camp.Text("CC_WarCamp_RestTitle"), body, LetterDefOf.PositiveEvent, true, camp.leader);
            if (b.LordAlive && b.lord.ownedPawns.Count > 0) b.lord.ReceiveMemo(LordJob_WarBand.MemoLeave);
            tracker.warBand = null;
        }

        private static void GoHome(WarBand b, CampTracker tracker, string key)
        {
            if (b.LordAlive) b.lord.ReceiveMemo(LordJob_WarBand.MemoLeave);
            string text = key.Translate((b.faction?.Name ?? "").Named("FACTION"), (b.enemy?.Name ?? "").Named("ENEMY"), b.came.Named("COUNT"), b.Fell.Named("FELL")).Resolve();
            Find.LetterStack.ReceiveLetter("CC_WarCamp_GoneTitle".Translate((b.faction?.Name ?? "").Named("FACTION")).Resolve(), text, LetterDefOf.NeutralEvent,
                new LookTargets(b.Here.Cast<Thing>()), b.faction);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, text));
            tracker.warBand = null;
        }

        /// <summary>The fight is over with them in it: their people remember the colony owes them, and the colonists remember who stood with them.</summary>
        private static void Thank(WarBand b)
        {
            if (b.faction == null || b.map == null) return;
            try
            {
                if (!b.holdBack) FactionMemory.Note(b.faction, "aid", b.enemy?.Name, b.Fell > 0 ? 16 : 12);
                foreach (var c in IncidentWorker_Happening.Colonists(b.map)) CouncilGameComponent.GiveThought(c, CampDefOf.Council_Camp_StoodWithUs);
                var comp = CouncilGameComponent.Instance;
                comp?.character.Shift(new WayShift { openness = 1f }, "CC_WarCamp_Chronicle".Translate(b.faction.Name).Resolve());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("war party thanks", e);
            }
        }
    }

    /// <summary>
    /// A war party's group: they hunt the enemy on the map (the game's own duty), or hold at the edge when asked, and
    /// leave when told; hurt by the colony or at war with it, they fight their way out.
    /// </summary>
    public class LordJob_WarBand : LordJob
    {
        public IntVec3 entry;

        public const string MemoHold = "WarHold";
        public const string MemoFight = "WarFight";
        public const string MemoLeave = "WarLeave";

        public LordJob_WarBand()
        {
        }

        public LordJob_WarBand(IntVec3 entry)
        {
            this.entry = entry;
        }

        public override bool AddFleeToil => false;

        public override bool ShouldExistWithoutPawns => false;

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var hunt = new LordToil_HuntEnemies(entry);
            graph.StartingToil = hunt;
            var hold = new LordToil_DefendPoint(entry, 10f, 6f);
            graph.AddToil(hold);
            var leave = new LordToil_ExitMap(LocomotionUrgency.Walk, false, true);
            graph.AddToil(leave);
            var flee = new LordToil_ExitMapAndDefendSelf();
            graph.AddToil(flee);

            var toHold = new Transition(hunt, hold);
            toHold.AddTrigger(new Trigger_Memo(MemoHold));
            graph.AddTransition(toHold);
            var toFight = new Transition(hold, hunt);
            toFight.AddTrigger(new Trigger_Memo(MemoFight));
            graph.AddTransition(toFight);

            var goes = new Transition(hunt, leave);
            goes.AddSource(hold);
            goes.AddTrigger(new Trigger_Memo(MemoLeave));
            goes.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(goes);

            var scatter = new Transition(hunt, flee);
            scatter.AddSources(hold, leave);
            scatter.AddTrigger(new Trigger_BecamePlayerEnemy());
            scatter.AddTrigger(new Trigger_PawnHarmed(1f, true, Faction.OfPlayer, null, null));
            scatter.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(scatter);
            return graph;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref entry, "entry");
        }
    }

    /// <summary>
    /// The event: fighters of a people at war with the colony's attacker arrive to fight them (Council_Evt_WarCamp,
    /// fired by <see cref="WarCamps.Hourly"/> while a threat lasts). The answers: welcome them, ask them to hold at the
    /// edge, or send them away.
    /// </summary>
    public class HappeningWorker_WarCamp : IncidentWorker_Happening
    {
        private List<Pawn> made;

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            CampTracker.Enabled && CampTracker.Instance != null && CampTracker.Instance.warBand == null && MapFacts.ThreatOn(map) && WarCamps.CanCome(map, out _, out _);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!WarCamps.CanCome(ctx.map, out var enemy, out var helper)) return false;
            made = WarCamps.MakeFighters(helper, ctx.map);
            if (made.Count == 0) return false;
            ctx.faction = helper;
            ctx.enemy = enemy;
            ctx.stranger = made[0];
            ctx.group = made.Skip(1).ToList();
            ctx.count = made.Count;
            ctx.a = KinMemory.Greeter(helper, ctx.map) ?? Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) => WarCamps.Start(ctx.map, ctx.faction, ctx.enemy, made) != null;

        protected override void Abort(HappeningContext ctx)
        {
            if (made != null) foreach (var p in made) Arrivals.Discard(p);
            made = null;
            ctx.stranger = null;
            ctx.group.Clear();
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            if (ctx.enemy == Faction.OfMechanoids) tags.Add("mechs");
            if (ctx.enemy != null && ctx.faction != null && ctx.enemy.def.humanlikeFaction && ctx.faction.HostileTo(ctx.enemy)) tags.Add("oldfoe");
            if (Allies.IsAlly(ctx.faction)) tags.Add("ally");
        }

        protected override LookTargets Look(HappeningContext ctx) => ctx.stranger != null && ctx.stranger.Spawned ? new LookTargets(ctx.stranger) : base.Look(ctx);

        /// <summary>Gone already (the fight ended, or they left) by the time the colony answers: only welcoming them is left.</summary>
        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option == 0) return null;
            return CampTracker.Instance?.warBand != null ? null : "CC_WarCamp_NotHere".Translate().ToString();
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var band = CampTracker.Instance?.warBand;
            if (band == null) return outcome;
            switch (option)
            {
                case 1:
                    WarCamps.HoldBack(band);
                    break;
                case 2:
                    WarCamps.Dismiss(band);
                    break;
            }
            return outcome;
        }
    }

    /// <summary>The colony's doctor (A) sees to the war party's wounded at their camp: up to three of them tended.</summary>
    public class DilemmaEffect_CampTendWounded : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampTracker.Asked ?? CampTracker.Instance?.camp;
            if (c == null || !c.Here.Any(p => p.health?.hediffSet != null && p.health.hediffSet.HasTendableHediff())) return "CC_CampAsk_NobodyHurt".Translate();
            if (ctx.a == null || ctx.a.WorkTagIsDisabled(WorkTags.Caring)) return "CC_CampTrouble_NoDoctor".Translate();
            return null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.Asked ?? CampTracker.Instance?.camp;
            if (c == null || ctx.a == null) return;
            int n = 0;
            foreach (var p in c.Here.Where(p => p.health?.hediffSet != null && p.health.hediffSet.HasTendableHediff()).OrderByDescending(p => p.health.summaryHealth.SummaryHealthPercent < 0.6f))
            {
                if (n++ >= 3) break;
                try
                {
                    var medicine = DilemmaEffect_Consume.Medicine(c.map).FirstOrDefault();
                    var med = medicine?.SplitOff(1) as Medicine;
                    TendUtility.DoTend(ctx.a, p, med);
                    if (med != null && !med.Destroyed) med.Destroy();
                    KinMemory.Notify_Tended(ctx.a, p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("war camp wounded tended", e);
                }
            }
        }
    }
    /// <summary>
    /// The camp stands guard (an ask of deserters, or any camp of fighters): from now on its people take up arms against
    /// whatever threatens the colony while they're here, as a war party does (CampLife.Ends sends the fight memo).
    /// </summary>
    public class DilemmaEffect_CampGuards : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null) return "CC_CampAsk_NotNow".Translate().ToString();
            return c.guards || c.kind.fights ? "CC_CampGuards_Already".Translate().ToString() : null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null) return;
            c.guards = true;
            // apart: a word with their leader at the camp; it shows when the next raid comes and they take up arms with the colony.
            if (c.map != null && MapFacts.ThreatOn(c.map) && c.LordAlive && !c.fighting)
            {
                c.fighting = true;
                c.lord.ReceiveMemo(LordJob_Camp.MemoFight);
            }
        }
    }
}
