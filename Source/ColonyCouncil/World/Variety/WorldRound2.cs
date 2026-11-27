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
    // The content round's world events (docs/content-update-proposals-2.md, section 5, D39 to D44; Defs/WorldSituationDefs/
    // Rimfolk_WS_Round2.xml): a god put away, the dancing sickness, the restless dead, bad coin, a rising in the town and two
    // claims on the valley. Each in its people's age's form (SituationWorker_Variety's tags), their state in the world event's
    // own flags and numbers; the pure rules are World/Variety/Round2Math.cs.

    // ===================== D39. A god put away =====================

    /// <summary>
    /// A god put away (D39): a people at peace that holds several gods puts one away after a hard year. Their letter names it
    /// by their own name for it ({ITEM}), and the colony's book keeps it as told in a letter; the colony's believers of that god
    /// are uneasy. One of its priests asks shelter at the gate: taken in, they camp a season by the colony (the priest's camp,
    /// CampWorker_PutAwayPriest), keep the god's rite at the colony's place of it where there is one and tell its tales by
    /// their people's name. The ending says whether the people took the god back, by what befell them next.
    /// </summary>
    public class SituationWorker_GodPutAway : SituationWorker_Variety
    {
        /// <summary>The god put away, by the world event's own number (its id, one up, so none is 0).</summary>
        public static God GodOf(WorldSituation s) => s == null || s.Num("god") <= 0 ? null : Gods.ById(s.Num("god") - 1);

        /// <summary>The one running now, or null.</summary>
        public static WorldSituation Running() => WorldSituations.Instance?.Running?.FirstOrDefault(s => s?.def?.defName == "Rimfolk_WS_GodPutAway");

        private IEnumerable<Faction> Peoples(Map map) => Candidates(def.faction, null, map).Where(f => Gods.Of(f).Count >= 2);

        protected override bool CanHappen(Map map) => Gods.On && Peoples(map).Any();

        public override bool Cast(WorldSituation s, Map map)
        {
            var ctx = s.ctx;
            ctx.map = map;
            ctx.faction = Peoples(map).RandomElementByWeightWithFallback(f => Mathf.Lerp(1.5f, 0.5f, Mathf.Clamp01(Distance(map, NearestTown(f, map)) / 40f)));
            if (ctx.faction == null) return false;
            s.At(NearestTown(ctx.faction, map), ctx.faction.Name);
            var g = Gods.Of(ctx.faction).RandomElement();
            s.SetNum("god", g.id + 1);
            ctx.item = Gods.NameBy(g, ctx.faction.leader);
            if (Gods.ColonyGods(map).Contains(g)) s.Set("ourGod");
            ctx.a = IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            if (!def.names.NullOrEmpty()) ctx.name = Pick(def.names, s);
            return true;
        }

        public override void Begin(WorldSituation s)
        {
            var g = GodOf(s);
            var map = s.ctx.map ?? HappeningTracker.HomeMap();
            if (g == null) return;
            // So their letter said: the colony's book keeps it as told, by their name for the god.
            GodLore.Heard("CC_GodLore_TellerPeople".Translate((s.ctx.faction?.Name ?? "?").Named("PEOPLE")).Resolve(), g,
                "CC_R2Away_Told".Translate((s.ctx.item ?? g.name).Named("GOD"), (s.ctx.faction?.Name ?? "?").Named("FACTION")).Resolve(), "Letter", null, -1, s.ctx.item, s.ctx.faction);
            int n = CampRound2.Feel(map, g, "Rimfolk_R2_GodPutAway");
            if (n > 0) s.Set("believers");
        }
    }

    /// <summary>
    /// The priest at the gate taken in for a season (D39): they come to camp by the colony, alone (the priest's camp), keep their
    /// god's rite and tell its tales. Nothing when camps are off or the land has no room for one; the letter tells it as before.
    /// </summary>
    public class WorldEffect_PriestStays : WorldEffect
    {
        public const string Kind = "Rimfolk_Camp_PutAwayPriest";

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!CampTracker.Enabled || !CampTracker.RoomFor(SituationEffectUtil.MapOf(ctx))) return;
            sheet.Add(ImpactKind.People, 1, "CC_R2Away_PriestImpact".Translate().Resolve(), "CC_R2Away_PriestImpactBrief".Translate().Resolve(), "r2priest");
        }

        public override string Unavailable(DilemmaContext ctx) =>
            !CampTracker.Enabled || !CampTracker.RoomFor(SituationEffectUtil.MapOf(ctx)) ? "CC_R2Away_NoRoom".Translate().Resolve() : null;

        public override void Apply(DilemmaContext ctx)
        {
            var map = SituationEffectUtil.MapOf(ctx);
            var kind = DefDatabase<CampKindDef>.GetNamedSilentFail(Kind);
            var tracker = CampTracker.Instance;
            var trip = VisitorTracker.Instance?.WaitingFor(ctx);
            var priest = ctx?.stranger;
            if (map == null || kind == null || tracker == null || !CampTracker.Enabled || !Gate(ctx) || priest == null || priest.Dead || !priest.Spawned || priest.Map != map) return;
            try
            {
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 12);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                if (!CampSite.TryFind(map, out var center, hot)) return;
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, priest.Faction ?? ctx.faction, hot, null, null, new List<Pawn> { priest });
                if (setup == null) return;
                setup.land = TravelLand.Read(map);
                // Taken in from the gate: their visit is over, and they walk to where they will camp.
                VisitorTracker.Instance?.Joined(trip, new List<Pawn> { priest });
                tracker.Start(setup, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 2 world events: the priest stays", e);
            }
        }
    }

    /// <summary>
    /// The priest of a god put away, camped a season by the colony (D39): they keep the god's rite at the colony's place of it,
    /// morning and evening, where it has one (else at their own fire), and tell its tales by their people's name to anyone of
    /// the colony who sits at their fire.
    /// </summary>
    public class CampWorker_PutAwayPriest : CampWorker
    {
        public static NoviceState StateOf(Camp c) => c?.workState as NoviceState;

        public override void Started(Camp c)
        {
            if (c.workState is NoviceState) return;
            var g = SituationWorker_GodPutAway.GodOf(SituationWorker_GodPutAway.Running()) ?? Gods.Of(c.faction).RandomElementWithFallback();
            c.workState = new NoviceState { gods = g != null ? new List<int> { g.id } : new List<int>() };
        }

        private static God GodOf(NoviceState st) => st != null && st.gods.Count > 0 ? Gods.ById(st.gods[0]) : null;

        /// <summary>The name the priest's people know the god by: the one their letter gave, else the priest's own.</summary>
        private static string TheirName(NoviceState st, Pawn priest)
        {
            var g = GodOf(st);
            if (g == null) return null;
            var s = SituationWorker_GodPutAway.Running();
            return SituationWorker_GodPutAway.GodOf(s) == g && !(s.ctx.item).NullOrEmpty() ? s.ctx.item : Gods.NameBy(g, priest);
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is NoviceState st))
            {
                Started(c);
                st = StateOf(c);
            }
            var g = GodOf(st);
            var priest = c.Here.FirstOrDefault(p => p.RaceProps.Humanlike && !p.Downed && p.Awake());
            if (priest == null || g == null || hour < 7 || hour > 22) return;
            var map = c.map;
            // Anyone of the colony at their fire hears a tale of the god by their people's name, each once.
            var keys = new List<string> { g.Key };
            foreach (var x in IncidentWorker_Happening.Colonists(map).Where(p => !st.heard.Contains(p) && Together.Can(priest, p)))
            {
                st.heard.Add(x);
                // together: the colonist sits within talking range of the priest at their fire (Together.Can).
                if (GodTales.Tell(priest, x, keys, "Fire", TheirName(st, priest)) || GodTales.WhatOf(priest, x, keys, false, "Fire"))
                {
                    c.SayMoment(priest, "tale");
                    c.Warm(x, 1);
                }
                break;
            }
            // Morning and evening, the god's rite: at the colony's place of it where it has one, else by their own fire.
            if (hour != 7 && hour != 18) return;
            var place = Shrines.Of(map, g).Select(r => r.thing).FirstOrDefault(t => t != null && t.Spawned && priest.CanReach(t, PathEndMode.Touch, Danger.Some));
            var pray = DefDatabase<JobDef>.GetNamedSilentFail("Council_CampPray");
            var near = place?.Position ?? c.FireCell;
            if (pray == null || !CellFinder.TryFindRandomCellNear(near, map, 3, x => x != near && x.Standable(map) && x.GetFirstPawn(map) == null && priest.CanReach(x, PathEndMode.OnCell, Danger.Some), out var at))
                return;
            try
            {
                priest.jobs.StartJob(JobMaker.MakeJob(pray, at, place != null ? new LocalTargetInfo(place) : new LocalTargetInfo(c.FireCell)), JobCondition.InterruptOptional, null, true, true);
                c.SayMoment(priest, "rite", 0.5f);
                if (place != null) st.knelt = true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 2 world events: the priest's rite", e);
            }
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = StateOf(c);
            if ((st?.heard.Count ?? 0) > 0) marks.Add("told");
            if (st?.knelt ?? false) marks.Add("rite");
        }

        public override string Fill(Camp c, string text)
        {
            var st = StateOf(c);
            return text?.Replace("{GOD}", (GodOf(st) != null ? TheirName(st, c.leader) : null) ?? "CC_R2Stone_TheirGod".Translate().Resolve());
        }
    }

    // ===================== D40. The dancing sickness =====================

    /// <summary>
    /// The dancing sickness (D40): in one town of a people at peace, people dance in the streets and can't stop. Two or three
    /// of that town come to the gate dancing, and dance an hour at the colony's gate (one at the colony's instrument where it
    /// has one in reach, VisitorMusic); those of the colony who hold the god of the cup and are near join in, those who hold
    /// the god of quiet ask for quiet. Rested indoors (the answer), one of the colony who danced with them may catch the
    /// dancing fever for a day the next morning, through the shared harm and illness budgets, and told.
    /// </summary>
    public class SituationWorker_DancingSickness : SituationWorker_Variety
    {
        public const string Fever = "Rimfolk_R2_DancingFever";

        public override string Hourly(WorldSituation s)
        {
            string end = base.Hourly(s);
            if (end != null) return end;
            try
            {
                Dance(s);
                MorningAfter(s);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 2 world events: the dancing sickness", e);
            }
            return null;
        }

        /// <summary>Those at the gate dance once, an hour; the cup's believers near join in, the quiet's ask for quiet.</summary>
        private static void Dance(WorldSituation s)
        {
            if (s.Has("danced")) return;
            var trip = VisitorTracker.Instance?.trips.FirstOrDefault(t => t.situationId == s.id && t.state == VisitState.Here);
            var map = trip?.map;
            var dancers = trip?.Here.Where(p => p.RaceProps.Humanlike && !p.Downed && p.Awake()).ToList();
            var job = DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_R2_DanceInYard");
            if (dancers == null || dancers.Count == 0 || job == null) return;
            s.Set("danced");
            bool music = false;
            foreach (var p in dancers)
            {
                // One of them at the colony's instrument, if one is in reach; the rest dance where they stand.
                var play = !music ? VisitorMusic.JobFor(p, trip) : null;
                if (play != null)
                {
                    music = true;
                    p.jobs.StartJob(play, JobCondition.InterruptForced, null, false, true);
                    continue;
                }
                var dance = JobMaker.MakeJob(job, CellFinder.RandomClosewalkCellNear(p.Position, map, 3));
                dance.count = GenDate.TicksPerHour;
                p.jobs.StartJob(dance, JobCondition.InterruptForced, null, false, true);
            }
            if (music) s.Set("music");
            var at = dancers[0].Position;
            var cup = CampRound2.Believers(map, Gods.ByKey("Revel")).Where(p => p.Position.InHorDistOf(at, Round2Math.JoinRange) && PlayOuts.CanTakePart(p, map)).Take(2).ToList();
            foreach (var p in cup)
            {
                var dance = JobMaker.MakeJob(job, CellFinder.RandomClosewalkCellNear(at, map, 4));
                dance.count = GenDate.TicksPerHour / 2;
                if (!p.jobs.TryTakeOrderedJob(dance, JobTag.Misc)) continue;
                s.sufferers.AddDistinct(p);
                CouncilGameComponent.GiveThought(p, DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_R2_DancedWithThem"));
                Speech.Say(p, "CC_R2Dance_JoinLine".Translate().Resolve());
            }
            if (cup.Count > 0) s.Set("joined");
            var quiet = CampRound2.Believers(map, Gods.ByKey("Hush")).FirstOrDefault(p => p.Position.InHorDistOf(at, Round2Math.JoinRange * 1.5f) && p.Awake() && !p.Downed);
            if (quiet != null)
            {
                Speech.Say(quiet, "CC_R2Dance_QuietLine".Translate().Resolve());
                s.Set("quiet");
            }
            Tell.Message(NewsKind.World, Importance.Notable, map, "CC_R2Dance_Dancing".Translate(dancers.Count.Named("COUNT"), (s.ctx.faction?.Name ?? "?").Named("FACTION")).Resolve(),
                new LookTargets(dancers), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>The morning after they slept indoors: one of the colony who danced with them may have caught it, for a day.</summary>
        private static void MorningAfter(WorldSituation s)
        {
            if (!s.Has("rested") || s.Has("feverRolled")) return;
            var map = s.ctx.map ?? HappeningTracker.HomeMap();
            if (map == null || GenLocalDate.HourOfDay(map) != 8 || s.DaysIn < 0.5f) return;
            s.Set("feverRolled");
            var def = DefDatabase<HediffDef>.GetNamedSilentFail(Fever);
            var who = s.sufferers.Where(p => p != null && !p.Dead && p.Spawned && p.Map == map && Harm.May(p, HurtClass.Mild)).RandomElementWithFallback();
            if (def == null || who == null || !Harm.On || !Harm.Roll(HarmKinds.GateSickness, Round2Math.FeverChance) || !IllnessBudget.Allows(map, def, who)) return;
            try
            {
                who.health.AddHediff(HediffMaker.MakeHediff(def, who));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 2 world events: the dancing fever", e);
                return;
            }
            HarmBook.Instance?.Came(HarmKinds.GateSickness, Find.TickManager.TicksGame);
            s.Set("fever");
            Tell.Message(NewsKind.Health, Importance.Notable, map, "CC_R2Dance_Fever".Translate(who.Named("PAWN")).Resolve(), new LookTargets(who), MessageTypeDefOf.NegativeHealthEvent);
        }
    }

    // ===================== D41. The restless dead =====================

    /// <summary>
    /// The restless dead (D41): graves found open in a people's towns, and the colony's own question what to do with its dead
    /// (only while it has some in their graves): leave them as they lie, burn them (their graves taken up and the dead cremated
    /// by the game's own jobs, with a crematorium), or seal them (a colonist works at the graves). Its believers speak as the
    /// question comes. Left as they lie, with Anomaly and harm on, a shambler may rise from the colony's oldest grave once, at
    /// the shared budget's roll; shown.
    /// </summary>
    public class SituationWorker_RestlessDead : SituationWorker_Variety
    {
        /// <summary>With Anomaly, the region's talk is that the dead walked ([anomaly]).</summary>
        protected override void AddTags(WorldSituation s, HashSet<string> tags)
        {
            base.AddTags(s, tags);
            if (ModsConfig.AnomalyActive) tags.Add("anomaly");
        }

        /// <summary>The colony's graves with someone in them, oldest first (by when they were built: their id).</summary>
        public static List<Building_Grave> Graves(Map map) =>
            map == null
                ? new List<Building_Grave>()
                : map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial).OfType<Building_Grave>().Where(g => g.Spawned && g.Faction == Faction.OfPlayer && g.HasCorpse)
                    .OrderBy(g => g.thingIDNumber).ToList();

        public override bool BeatCanHappen(WorldSituation s, SituationBeat beat, Map map, out bool retry)
        {
            if (!base.BeatCanHappen(s, beat, map, out retry)) return false;
            return beat.key != "question" || Graves(map).Count > 0;
        }

        public override bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            if (beat.key != "question") return true;
            ctx.count = Graves(SituationEffectUtil.MapOf(ctx)).Count;
            return ctx.count > 0;
        }

        public override void BeatHappened(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            if (beat.key != "question") return;
            var map = SituationEffectUtil.MapOf(ctx);
            // What its believers say as the question comes: the keeper of the dead's want them whole, the soil's want them in it.
            foreach (var (key, line) in new[] { ("Mog", "CC_R2Dead_SayWhole"), ("Morel", "CC_R2Dead_SaySoil") })
            {
                var p = CampRound2.Believers(map, Gods.ByKey(key)).FirstOrDefault(x => x.Awake() && !x.Downed);
                if (p != null) Speech.Say(p, line.Translate().Resolve());
            }
        }

        public override string Daily(WorldSituation s)
        {
            if (!s.Has("left") || s.Has("riseRolled") || !ModsConfig.AnomalyActive || !Harm.On) return null;
            var map = s.ctx.map ?? HappeningTracker.HomeMap();
            if (map == null || s.DaysIn < 2f) return null;
            s.Set("riseRolled");
            if (!Harm.SystemOn(HarmKinds.Uncanny.system) || !Harm.Roll(HarmKinds.Uncanny, Round2Math.RiseChance)) return null;
            var grave = Graves(map).FirstOrDefault();
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("ShamblerSwarmer");
            var entities = Faction.OfEntities;
            if (grave == null || kind == null || entities == null) return null;
            try
            {
                var at = CellFinder.StandableCellNear(grave.Position, map, 2f);
                if (!at.IsValid) return null;
                var x = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, entities, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
                GenSpawn.Spawn(x, at, map);
                DefDatabase<EffecterDef>.GetNamedSilentFail("ShamblerRaise")?.Spawn(at, map).Cleanup();
                LordMaker.MakeNewLord(entities, new LordJob_AssaultColony(entities, false, false, false, false, false), map, new List<Pawn> { x });
                HarmBook.Instance?.Came(HarmKinds.Uncanny, Find.TickManager.TicksGame);
                s.Set("rose");
                Tell.Letter(NewsKind.Colony, Importance.Important, map, "CC_R2Dead_RoseTitle".Translate().Resolve(), "CC_R2Dead_Rose".Translate(grave.LabelNoCount.Named("GRAVE")).Resolve(),
                    LetterDefOf.ThreatSmall, new LookTargets(x));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 2 world events: the restless dead", e);
            }
            return null;
        }
    }

    /// <summary>
    /// The colony's dead burned (D41): its graves taken up (the game's own deconstruction, which sets each one's dead down
    /// beside it) and a bill to cremate them at its crematorium, the game's own jobs doing the rest. Only with a crematorium.
    /// </summary>
    public class WorldEffect_BurnDead : WorldEffect
    {
        /// <summary>The colony's first building that can cremate the dead, or null.</summary>
        public static Building_WorkTable Crematorium(Map map) =>
            map?.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().FirstOrDefault(b => b.def.AllRecipes.Any(r => r.defName == "CremateCorpse"));

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var map = SituationEffectUtil.MapOf(ctx);
            int n = Mathf.Min(Round2Math.BurnGraves, SituationWorker_RestlessDead.Graves(map).Count);
            if (n > 0 && Crematorium(map) != null) sheet.Play("CC_R2Dead_BurnImpact".Translate(n).Resolve(), "CC_R2Dead_BurnImpactBrief".Translate().Resolve());
        }

        public override string Unavailable(DilemmaContext ctx) =>
            Crematorium(SituationEffectUtil.MapOf(ctx)) == null ? "CC_R2Dead_NoCrematorium".Translate().Resolve() : null;

        public override void Apply(DilemmaContext ctx)
        {
            var map = SituationEffectUtil.MapOf(ctx);
            var crem = Crematorium(map);
            if (map == null || crem == null || !Gate(ctx)) return;
            try
            {
                foreach (var g in SituationWorker_RestlessDead.Graves(map).Take(Round2Math.BurnGraves))
                    if (map.designationManager.DesignationOn(g, DesignationDefOf.Deconstruct) == null) map.designationManager.AddDesignation(new Designation(g, DesignationDefOf.Deconstruct));
                var recipe = DefDatabase<RecipeDef>.GetNamedSilentFail("CremateCorpse");
                if (recipe != null && !crem.BillStack.Bills.Any(b => b.recipe == recipe)) crem.BillStack.AddBill(recipe.MakeNewBill());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 2 world events: the dead burned", e);
            }
        }
    }

    // ===================== D42. Bad coin =====================

    /// <summary>
    /// Bad coin (D42): one people's silver found mixed with tin. Their towns buy good metal dear while it lasts (the region's
    /// prices, RegionPricesMath). A money-changer at the gate offers to weigh the colony's silver for a fee ({COUNT}): weighed,
    /// the bad coin found in it ({THING}) goes out with the fee, carried to the changer. One of the colony who holds the god of
    /// the ledger says the gold goes to the honest.
    /// </summary>
    public class SituationWorker_BadCoin : SituationWorker_Variety
    {
        public override bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            if (beat.key != "changer") return true;
            int silver = CampAnswers.Silver(SituationEffectUtil.MapOf(ctx));
            if (silver < 100) return false;
            int fee = Round2Math.ChangerFee(silver), bad = Round2Math.BadCoin(silver);
            ctx.count = fee;
            ctx.variant = fee + bad;
            ctx.item = bad.ToString();
            return true;
        }

        public override string Chosen(WorldSituation s, SituationContext ctx, SituationBeat beat, int option, bool risk, bool byDefault, string outcome)
        {
            if (beat?.key == "changer" && option == 0)
            {
                var map = SituationEffectUtil.MapOf(ctx);
                var p = CampRound2.Believers(map, Gods.ByKey("Ducat")).FirstOrDefault(x => x.Awake() && !x.Downed);
                if (p != null) Speech.Say(p, "CC_R2Coin_LedgerLine".Translate().Resolve());
            }
            return outcome;
        }
    }

    // ===================== D43. A rising in the town =====================

    /// <summary>
    /// A rising in the town (D43): in a town of a people that keeps the bound (its faith allows slavery, or any people without
    /// Ideology), they rise. Freed ones come down the road and one reaches the gate: taken in, fed and sent on, or handed back
    /// (their masters think better of the colony; the colony's merciful, those of a kind heart, think less of the council). The
    /// colony's own slaves hear of it from the road: their suppression falls a little (the game's own measure against its
    /// rebellion). The ending: the town freed (its people's head replaced by the game's own, where one can be) or put down.
    /// </summary>
    public class SituationWorker_Rising : SituationWorker_Variety
    {
        private static readonly string[] Against = { "Slavery_Abhorrent", "Slavery_Horrible" };

        /// <summary>A people that keeps the bound: without Ideology any, with it one whose faith doesn't abhor slavery.</summary>
        public static bool KeepsBound(Faction f)
        {
            if (f == null) return false;
            if (!ModsConfig.IdeologyActive) return true;
            var ideo = f.ideos?.PrimaryIdeo;
            return ideo == null || !ideo.PreceptsListForReading.Any(p => p?.def != null && Against.Contains(p.def.defName));
        }

        protected override bool CanHappen(Map map) => Candidates(def.faction, null, map).Any(KeepsBound);

        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            if (!KeepsBound(s.ctx.faction))
            {
                var f = Candidates(def.faction, null, map).Where(KeepsBound).RandomElementWithFallback();
                if (f == null) return false;
                s.ctx.faction = f;
                s.At(NearestTown(f, map), f.Name);
            }
            if (map.mapPawns.SlavesOfColonySpawned.Count > 0) s.Set("slaves");
            return true;
        }

        public override void BeatHappened(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            if (beat.key != "news") return;
            var map = SituationEffectUtil.MapOf(ctx);
            var heard = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_R2_RisingHeard");
            foreach (var p in map?.mapPawns.SlavesOfColonySpawned ?? new List<Pawn>())
            {
                var need = p.needs?.TryGetNeed<Need_Suppression>();
                if (need != null) need.CurLevel = Mathf.Max(0f, need.CurLevel - Round2Math.SuppressionDip);
                CouncilGameComponent.GiveThought(p, heard);
            }
        }

        public override string Chosen(WorldSituation s, SituationContext ctx, SituationBeat beat, int option, bool risk, bool byDefault, string outcome)
        {
            if (beat?.key == "gate" && option == 2)
            {
                var map = SituationEffectUtil.MapOf(ctx);
                var sad = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_R2_HandedBack");
                foreach (var p in IncidentWorker_Happening.Colonists(map).Where(p => p.story?.traits?.HasTrait(TraitDefOf.Kind) ?? false)) CouncilGameComponent.GiveThought(p, sad);
            }
            return outcome;
        }

        public override void Ended(WorldSituation s, SituationEnding ending)
        {
            if (ending?.key != "freed") return;
            var f = s.ctx.faction;
            if (f == null || !LeaderReplaceable(f)) return;
            var old = f.leader;
            try
            {
                if (!f.TryGenerateNewLeader()) f.leader = old;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("round 2 world events: the town freed", e);
                f.leader = old;
            }
        }
    }

    // ===================== D44. Two claims on the valley =====================

    /// <summary>
    /// Two claims on the valley (D44): two peoples at peace with the colony each claim the land it stands on, and each sends
    /// someone to the gate. Side with one, say the land is the colony's own (both a little cooler; the stronger, by its towns,
    /// walks a party past the colony one day, seen, not fighting), or pay each a small toll. The ending: settled at a moot of the
    /// peoples if one runs or chance has it, else by the stronger.
    /// </summary>
    public class SituationWorker_TwoClaims : SituationWorker_Variety
    {
        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map) || s.ctx.enemy == null) return false;
            int a = TownsOf(s.ctx.faction, map).Count(), b = TownsOf(s.ctx.enemy, map).Count();
            s.Set(Round2Math.FirstStronger(a, b) ? "strongFirst" : "strongSecond");
            if (WorldSituations.Instance?.Running?.Any(x => x?.def?.defName == "Council_WS_Moot") ?? false) s.Set("moot");
            return true;
        }

        public override bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            if (beat.key == "claims") ctx.count = Round2Math.Toll(Silver(SituationEffectUtil.MapOf(ctx), new IntRange(60, 120))) * 2;
            return true;
        }
    }
}
