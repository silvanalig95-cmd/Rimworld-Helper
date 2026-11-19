using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 42. The Circuit's ring (the Circuit of Tamers) =====================

    /// <summary>The ring's days: who of the colony waits to challenge, the bout on now, and each challenger's bouts and wins.</summary>
    public class RingState : CampWorkState
    {
        public List<Pawn> waiting = new List<Pawn>();
        public List<Pawn> fought = new List<Pawn>();
        public List<int> wins = new List<int>();
        public Pawn trainer, mon;
        public int boutTick = -1, day = -1, today;
        public bool crowned;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref waiting, "waiting", LookMode.Reference);
            Scribe_Collections.Look(ref fought, "fought", LookMode.Reference);
            Scribe_Collections.Look(ref wins, "wins", LookMode.Value);
            Scribe_References.Look(ref trainer, "trainer");
            Scribe_References.Look(ref mon, "mon");
            Scribe_Values.Look(ref boutTick, "boutTick", -1);
            Scribe_Values.Look(ref day, "day", -1);
            Scribe_Values.Look(ref today, "today");
            Scribe_Values.Look(ref crowned, "crowned");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                waiting ??= new List<Pawn>();
                waiting.RemoveAll(p => p == null);
                fought ??= new List<Pawn>();
                wins ??= new List<int>();
                for (int i = fought.Count - 1; i >= 0; i--)
                    if (fought[i] == null)
                    {
                        fought.RemoveAt(i);
                        if (i < wins.Count) wins.RemoveAt(i);
                    }
                while (wins.Count > fought.Count) wins.RemoveAt(wins.Count - 1);
                while (wins.Count < fought.Count) wins.Add(0);
            }
        }

        public void Won(Pawn p, bool won)
        {
            int i = fought.IndexOf(p);
            if (i < 0)
            {
                fought.Add(p);
                wins.Add(won ? 1 : 0);
            }
            else if (won) wins[i]++;
        }

        /// <summary>The colony's best: the most bouts won (one at least), alive.</summary>
        public Pawn Best(out int most)
        {
            most = 0;
            Pawn best = null;
            for (int i = 0; i < fought.Count && i < wins.Count; i++)
                if (fought[i] != null && !fought[i].Dead && wins[i] > most)
                {
                    most = wins[i];
                    best = fought[i];
                }
            return PeopleEvents5Math.Champion(most) ? best : null;
        }
    }

    /// <summary>
    /// The Circuit's ring (item 42): while a great fair runs in the region, six to ten tamers of the Circuit pitch a bout-ring
    /// by the colony for four to six days, torches round it, with their companions (with PokéWorld, Pokémon; else dogs).
    /// Each afternoon there is a bout: the tamers spar with their companions among themselves, and the colony's hands may be
    /// sent to challenge (two a day). A bout goes by the friendly bout's own rules (Peoples/PeoplesD2.cs,
    /// JobDriver_TamerBout): the winner by their way with beasts and their own hands, a lesson in handling, and one bout in 25
    /// a bruise through the harm budget (HarmKinds.TamerBout), told. When the ring packs up, the colony's best (the most bouts
    /// won) has a token tied on and an egg of the Circuit's hatchery, and the region hears of it.
    /// </summary>
    public static class CircuitRing
    {
        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_CircuitRing");

        public static HediffDef Token => DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_CircuitToken");

        private static int Now => Find.TickManager.TicksGame;

        public static void Forget()
        {
        }

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static RingState StateOf(Camp c) => c?.workState as RingState;

        /// <summary>A great fair running in the region now (a world event of the fair's kind): its name, or null.</summary>
        public static string Fair()
        {
            var s = WorldSituations.Instance?.Running?.FirstOrDefault(x => x?.def?.kind == "fair");
            return s == null ? null : (!(s.ctx?.placeName).NullOrEmpty() ? s.ctx.placeName : s.def.label);
        }

        /// <summary>The ring's companions on their feet: the camp's own beasts (its herd), small enough to spar with.</summary>
        public static List<Pawn> Companions(Camp c) =>
            c?.herd.Concat(c.members).Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Map == c.map && p.RaceProps.Animal && !p.InMentalState && p.BodySize >= 0.4f
                                                 && p.BodySize <= 2.5f).Distinct().ToList() ?? new List<Pawn>();

        /// <summary>The camp made where a camp may stand, its tamers walking in from the edge.</summary>
        public static Camp Start(HappeningContext ctx)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            try
            {
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 5);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                if (!CampSite.TryFind(map, out var center, hot)) return null;
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, ctx.faction, hot, null, null);
                if (setup == null) return null;
                setup.land = TravelLand.Read(map);
                var c = tracker.Start(setup);
                if (c == null)
                {
                    CampTracker.Discard(setup);
                    return null;
                }
                c.workState = new RingState();
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 5: the Circuit's ring", e);
                return null;
            }
        }

        private static bool Fighting(Pawn p) => p?.CurJobDef != null && p.CurJobDef == HappeningWorker_TamerBout.BoutJob;

        /// <summary>
        /// The afternoon's bout: the next of the colony's challengers waiting steps up to one of the companions (the friendly
        /// bout's own job), a tamer beside the ring; when none is waiting, two of the tamers spar now and then.
        /// </summary>
        public static void Afternoon(Camp c, RingState st)
        {
            if (Fighting(st.trainer) || HappeningWorker_TamerBout.BoutJob == null) return;
            st.trainer = null;
            st.mon = null;
            var mons = Companions(c);
            if (mons.Count == 0) return;
            st.waiting.RemoveAll(p => p == null || p.Dead || !p.Spawned || p.Map != c.map || p.Downed || p.Drafted || p.InMentalState);
            var who = st.waiting.FirstOrDefault();
            var mon = mons.RandomElement();
            if (who != null)
            {
                st.waiting.Remove(who);
                var job = JobMaker.MakeJob(HappeningWorker_TamerBout.BoutJob, mon);
                job.count = Rand.RangeInclusive(PeoplesDMath.BoutMinTicks, PeoplesDMath.BoutMaxTicks);
                if (!who.jobs.TryTakeOrderedJob(job, JobTag.Misc)) return;
                st.trainer = who;
                st.mon = mon;
                st.boutTick = Now;
                var tamer = c.Here.Where(p => p.RaceProps.Humanlike).OrderBy(p => p.Position.DistanceToSquared(mon.Position)).FirstOrDefault();
                if (tamer != null) c.SayMoment(tamer, "challenge");
                return;
            }
            // Their own spar: a tamer and a companion, now and then through the afternoon.
            if (!Rand.Chance(0.35f)) return;
            var sparrer = c.Here.Where(p => p.RaceProps.Humanlike && p != c.trader && !p.Downed && p.CurJobDef != HappeningWorker_TamerBout.BoutJob).RandomElementWithFallback();
            if (sparrer == null) return;
            try
            {
                var spar = JobMaker.MakeJob(HappeningWorker_TamerBout.BoutJob, mon);
                spar.count = Rand.RangeInclusive(PeoplesDMath.BoutMinTicks, PeoplesDMath.BoutMaxTicks) / 2;
                sparrer.jobs.StartJob(spar, JobCondition.InterruptForced, null, false, true);
                st.trainer = sparrer;
                st.mon = mon;
                st.boutTick = Now;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 5: a tamers' spar", e);
            }
        }

        /// <summary>
        /// A bout at the ring is over (HappeningWorker_TamerBout.Done asks first): the friendly bout's rules for one of the
        /// colony, a word from the tamers for their own spar. False: not one of the ring's bouts.
        /// </summary>
        public static bool BoutDone(Pawn trainer, Pawn mon)
        {
            if (trainer == null || mon == null) return false;
            var c = CampTracker.VisitorsAt(mon.MapHeld).FirstOrDefault(x => x.kind == Kind && (x.herd.Contains(mon) || x.members.Contains(mon)));
            var st = StateOf(c);
            if (st == null) return false;
            if (st.trainer == trainer)
            {
                st.trainer = null;
                st.mon = null;
            }
            // Their own spar: a cheer from the ring and nothing more.
            if (trainer.Faction != Faction.OfPlayer)
            {
                if (Rand.Chance(0.5f)) c.SayMoment(trainer, "spar");
                return true;
            }
            var map = trainer.MapHeld;
            int animals = trainer.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0, melee = trainer.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
            bool won = Rand.Chance(PeoplesDMath.WinChance(animals, melee));
            trainer.skills?.Learn(SkillDefOf.Animals, PeoplesDMath.BoutAnimalsXp);
            st.Won(trainer, won);
            // together: the bout's own end, the tamers at the ring's side.
            var tamer = c.Here.Where(p => p.RaceProps.Humanlike).OrderBy(p => p.Position.DistanceToSquared(trainer.Position)).FirstOrDefault();
            if (tamer != null) c.SayMoment(tamer, won ? "won" : "lost");
            if (map != null)
                Tell.Message(NewsKind.People, Importance.Notable, map,
                    (won ? "CC_PEv5_RingWon" : "CC_PEv5_RingLost").Translate(trainer.Named("PAWN"), mon.LabelShort.Named("PET")).Resolve(), new LookTargets(trainer),
                    MessageTypeDefOf.NeutralEvent);
            // One bout in 25 the companion's blow lands: the friendly bout's own rule, through the harm setting and the budget.
            if (!Harm.Roll(trainer, HarmKinds.TamerBout, PeoplesDMath.BoutBlowChance, HurtClass.Bout)) return true;
            var wound = Harm.Injure(trainer, HurtKind.Bruise, HurtWhere.Any, PeoplesDMath.BoutLow, PeoplesDMath.BoutHigh, HarmKinds.TamerBout, HurtClass.Bout);
            if (wound == null) return true;
            if (mon.Spawned) mon.Drawer?.Notify_MeleeAttackOn(trainer);
            Tell.Message(NewsKind.People, Importance.Important, map,
                "CC_PeoplesD_BoutBlow".Translate(trainer.Named("PAWN"), mon.LabelShort.Named("PET"), (wound.Part?.Label ?? "").Named("PART")).Resolve(), new LookTargets(trainer),
                MessageTypeDefOf.NegativeHealthEvent);
            return true;
        }

        /// <summary>The ring packs up: the colony's best has a token tied on and an egg of the Circuit's hatchery, and the region hears of it.</summary>
        public static void Crown(Camp c, RingState st)
        {
            st.crowned = true;
            var best = st.Best(out int wins);
            if (best == null || c.faction == null) return;
            try
            {
                var token = Token;
                if (token != null && !best.health.hediffSet.HasHediff(token))
                {
                    var arm = best.health.hediffSet.GetNotMissingParts().FirstOrDefault(p => p.def.defName == "Arm" || p.def.defName == "Shoulder");
                    best.health.AddHediff(HediffMaker.MakeHediff(token, best, arm), arm);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 5: the ring's token", e);
            }
            var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_RingChampion");
            if (thought != null) CouncilGameComponent.GiveThought(best, thought, null);
            var egg = HappeningWorker_EggTrade.Egg(c.map, null) ?? HappeningWorker_EggTrade.Egg(null, null);
            if (egg != null) Couriers.Hand(egg, 1, c.map, best.Spawned && best.Map == c.map ? best.Position : c.FireCell, c.faction, c.leader);
            if (c.faction.HasGoodwill && !c.faction.IsPlayer) WorldSituationWorker.Goodwill(c.faction, PeopleEvents5Math.ChampionGoodwill, GoodwillWhy.Bout);
            KinMemory.Note(c.faction, "duel", best, "CC_PEv5_RingName".Translate().Resolve());
            if (c.leader != null && c.leader.Spawned) c.SayMoment(c.leader, "champion");
            c.Tell("CC_PEv5_RingChampionLabel".Translate().Resolve(), c.Text("CC_PEv5_RingChampion", c.leader, best, wins, egg?.label ?? "?"), LetterDefOf.PositiveEvent, true, best);
        }
    }

    /// <summary>The Circuit's ring: a bout each afternoon, the colony's challengers stepping up, the champion crowned as they go.</summary>
    public class CampWorker_CircuitRing : CampWorker
    {
        public override void Started(Camp c)
        {
            if (!(c.workState is RingState)) c.workState = new RingState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is RingState st))
            {
                Started(c);
                st = CircuitRing.StateOf(c);
            }
            int day = GenDate.DaysPassed;
            if (st.day != day)
            {
                st.day = day;
                st.today = 0;
            }
            if (!c.Settled || !PeopleEvents5Math.BoutHour(hour) || c.map.dangerWatcher.DangerRating != StoryDanger.None) return;
            CircuitRing.Afternoon(c, st);
        }

        public override void Leaving(Camp c)
        {
            var st = CircuitRing.StateOf(c);
            if (st == null || st.crowned || c.Hostile) return;
            CircuitRing.Crown(c, st);
            if (c.faction != null && c.faction.HasGoodwill && !c.faction.IsPlayer && st.fought.Count > 0 && c.tension < 50f)
                WorldSituationWorker.Goodwill(c.faction, PeopleEvents5Math.RingGoodwill, GoodwillWhy.Bout);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = CircuitRing.StateOf(c);
            if (st == null) return;
            if (st.Best(out _) != null) marks.Add("champion");
            else if (st.fought.Count > 0) marks.Add("fought");
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = CircuitRing.StateOf(c);
            if (st == null) return null;
            switch (a?.key)
            {
                case "challenge":
                    if (CircuitRing.Companions(c).Count == 0) return "CC_PEv5_RingNoCompanion".Translate().Resolve();
                    if (st.today >= PeopleEvents5Math.ChallengesPerDay) return "CC_PEv5_RingEnoughToday".Translate().Resolve();
                    return null;
            }
            return null;
        }
    }

    /// <summary>A ring-keeper of {FACTION} ({X}) at the gate: the Circuit would pitch a bout-ring by the colony while {PLACE}'s fair runs.</summary>
    public class HappeningWorker_CircuitRing : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            CircuitRing.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && PeoplesD.Friendly(PeoplesMath.Tamers) != null && CircuitRing.On(map) == null
            && CircuitRing.Fair() != null && map.dangerWatcher.DangerRating == StoryDanger.None && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Tamers);
            ctx.tale = CircuitRing.Fair();
            if (ctx.faction == null || ctx.tale == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Tamers_Tamer"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = HappeningWorker_TamerBout.Trainer(ctx.map) ?? PeoplesD.Best(ctx.map, SkillDefOf.Animals);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (PokeWorldLink.Active) tags.Add("pokemon");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (CircuitRing.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map)) ? "CC_PEv2_NoRoomCamp".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "ring" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The tamers pitch their ring (CircuitRing.Start).</summary>
    public class DilemmaEffect_CircuitRing : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_PEv5_Impact_Ring".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv5_Impact_RingBrief".Translate().Resolve(), "pev5ring");
            if (Harm.On) sheet.Add(ImpactKind.Health, -1, "CC_PEv5_Impact_RingRisk".Translate().Resolve(), "CC_PeoplesD_Impact_BoutRiskBrief".Translate().Resolve(), "pev5ringrisk");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            CircuitRing.Start(hc);
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>A challenger sent to this afternoon's bout (the camp's ask): waiting at the ring for their turn.</summary>
    public class DilemmaEffect_RingChallenge : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a == null) return;
            sheet.Play("CC_PEv5_Impact_Challenge".Translate(ctx.a.LabelShort).Resolve(), "CC_PEv5_Impact_ChallengeBrief".Translate().Resolve());
            if (Harm.On) sheet.Add(ImpactKind.Health, -1, "CC_PeoplesD_Impact_BoutRisk".Translate(ctx.a.LabelShort).Resolve(), "CC_PeoplesD_Impact_BoutRiskBrief".Translate().Resolve(), "boutrisk");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_CircuitRing);
            var st = CircuitRing.StateOf(c);
            if (st == null || ctx?.a == null || st.waiting.Contains(ctx.a)) return;
            st.waiting.Add(ctx.a);
            st.today++;
        }
    }
}
