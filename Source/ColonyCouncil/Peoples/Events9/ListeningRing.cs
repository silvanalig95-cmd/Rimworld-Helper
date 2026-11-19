using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 90. A listening ring (the Choir of the Spire) =====================

    /// <summary>The listening ring's stay: the evenings sung and felt, the colony's promise of quiet, the loud nights, and how it turned.</summary>
    public class ListeningRingState : CampWorkState
    {
        public int sangDay = -1, feltDay = -1, loudDay = -1, nights, loudNights, sat;
        public bool promised, strangeRolled, strange, toldFirst;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref sangDay, "sangDay", -1);
            Scribe_Values.Look(ref feltDay, "feltDay", -1);
            Scribe_Values.Look(ref loudDay, "loudDay", -1);
            Scribe_Values.Look(ref nights, "nights");
            Scribe_Values.Look(ref loudNights, "loudNights");
            Scribe_Values.Look(ref sat, "sat");
            Scribe_Values.Look(ref promised, "promised");
            Scribe_Values.Look(ref strangeRolled, "strangeRolled");
            Scribe_Values.Look(ref strange, "strange");
            Scribe_Values.Look(ref toldFirst, "toldFirst");
        }
    }

    /// <summary>
    /// A listening ring (item 90): three to five of the Choir camp three to five days at the colony's quietest place (away from its
    /// workshops), and sing at their fire each evening; late in the evening everyone at home feels it, by the listening's own rule
    /// (PeoplesGMath.Felt: a calm for most, an unease for some, the psychically deaf nothing). A psycaster of the colony may go and sit
    /// with them an evening, and comes back with their focus full. They ask that loud work (mining, building, taking down) be kept away
    /// from them at night: a promise kept leaves them the warmer, loud work heard at night the tenser. It can turn, with Anomaly when the
    /// void stirs, once a stay: their song turns strange, that night everyone at home feels uneasy, and they pack up and leave early.
    /// </summary>
    public static class ListeningRing
    {
        public const string EventDef = "Rimfolk_Evt_ListeningRing";

        public static CampKindDef Kind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_ListeningRing");

        private static int Now => Find.TickManager.TicksGame;

        public static Camp On(Map map) => CampTracker.VisitorsAt(map).FirstOrDefault(c => c.kind == Kind);

        public static ListeningRingState StateOf(Camp c) => c?.workState as ListeningRingState;

        /// <summary>The ring's own moods: their evening's song felt as a calm or an unease at home, and the night it went strange.</summary>
        public static ThoughtDef CalmDef => DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_RingCalm");

        public static ThoughtDef UneaseDef => DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_RingUnease");

        public static ThoughtDef StrangeDef => DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_RingStrange");

        private static Camp CampOf(Pawn p) => p?.Map == null ? null : CampTracker.VisitorsAt(p.Map).FirstOrDefault(c => c.kind == Kind && c.Has(p));

        /// <summary>The colony's quietest place for a camp: of a few sites a long walk out, the one with the fewest of its buildings near, then the farthest.</summary>
        public static IntVec3 QuietSite(Map map, bool shade)
        {
            var mid = CampSite.ColonyCenter(map);
            var best = IntVec3.Invalid;
            int bestNoise = int.MaxValue;
            float bestDist = 0f;
            for (int i = 0; i < 6; i++)
            {
                var aim = (mid + IntVec3.FromVector3(Vector3Utility.HorizontalVectorFromAngle(i * 60f + Rand.Range(-20f, 20f)) * Rand.Range(45f, 60f))).ClampInsideMap(map);
                var c = CampLand.TryFindNear(map, aim, 0f, 14f, shade);
                if (!c.IsValid) continue;
                int noise = map.listerBuildings.allBuildingsColonist.Count(b => b != null && b.Position.InHorDistOf(c, PeopleEvents9Math.LoudRadius));
                float dist = c.DistanceTo(mid);
                if (noise < bestNoise || noise == bestNoise && dist > bestDist)
                {
                    best = c;
                    bestNoise = noise;
                    bestDist = dist;
                }
            }
            return best;
        }

        public static Camp Start(HappeningContext ctx)
        {
            var map = ctx?.map;
            var tracker = CampTracker.Instance;
            var kind = Kind;
            if (map == null || tracker == null || kind == null || ctx.faction == null || !CampTracker.Enabled) return null;
            var people = new List<Pawn>();
            try
            {
                int n = Rand.RangeInclusive(PeopleEvents9Math.RingMin, PeopleEvents9Math.RingMax);
                people.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Choir_Cantor"), ctx.faction, new FloatRange(24f, 65f)));
                for (int i = 1; i < n; i++) people.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Choir_Listener"), ctx.faction, new FloatRange(16f, 60f)));
                people.RemoveAll(p => p == null);
                if (people.Count == 0) return null;
                var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * PeopleEvents9Math.RingMaxDays);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                var center = QuietSite(map, hot);
                if (!center.IsValid && !CampSite.TryFind(map, out center, hot))
                {
                    foreach (var p in people) Arrivals.Discard(p);
                    return null;
                }
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, ctx.faction, hot, null, null, people);
                if (setup == null)
                {
                    foreach (var p in people) Arrivals.Discard(p);
                    return null;
                }
                setup.land = TravelLand.Read(map);
                var c = tracker.Start(setup);
                if (c == null)
                {
                    CampTracker.Discard(setup);
                    return null;
                }
                c.workState = new ListeningRingState();
                return c;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: the listening ring", e);
                return null;
            }
        }

        /// <summary>Their hour: the evening's song, the colony feeling it late in the evening, loud work heard at night, the song turned strange.</summary>
        public static void Hour(Camp c, ListeningRingState st, int hour)
        {
            int day = GenDate.DaysPassed;
            if (PeopleEvents9Math.FeltHour(hour) && st.feltDay != day && st.sangDay == day)
            {
                st.feltDay = day;
                if (!st.strangeRolled && c.DaysHere >= 1f && ChoirListening.VoidStirs())
                {
                    st.strangeRolled = true;
                    if (Rand.Chance(PeopleEvents9Math.StrangeChance))
                    {
                        Strange(c, st);
                        return;
                    }
                }
                Felt(c, st);
            }
            // A night counts from the evening it began, so the hours after midnight are the same night.
            int night = hour < 6 ? day - 1 : day;
            if (PeopleEvents9Math.NightHour(hour) && st.loudDay != night) Loud(c, st, night);
        }

        /// <summary>The evening's song felt at home, by the listening's rule; told the first night (the moods tell the rest).</summary>
        public static void Felt(Camp c, ListeningRingState st)
        {
            var map = c.map;
            bool stirs = ChoirListening.VoidStirs();
            var calm = new List<Pawn>();
            var uneasy = new List<Pawn>();
            foreach (var p in IncidentWorker_Happening.Colonists(map))
            {
                int felt = PeoplesGMath.Felt(p.GetStatValue(StatDefOf.PsychicSensitivity), Rand.Value, stirs);
                var t = felt > 0 ? CalmDef : felt < 0 ? UneaseDef : null;
                if (t == null) continue;
                CouncilGameComponent.GiveThought(p, t);
                (felt > 0 ? calm : uneasy).Add(p);
            }
            st.nights++;
            if (st.toldFirst) return;
            st.toldFirst = true;
            string Names(List<Pawn> l) => l.Count == 0 ? "CC_PeoplesG_Nobody".Translate().Resolve() : TextUtil.JoinAnd(l.Select(p => p.LabelShort).ToList());
            string who = "CC_PEv9_RingFeltWho".Translate(Names(calm).Named("CALM"), Names(uneasy).Named("UNEASY")).Resolve();
            c.Note(c.Text("CC_PEv9_RingFelt", c.leader, null, 0, who), MessageTypeDefOf.NeutralEvent, Importance.Notable, c.leader, true);
        }

        /// <summary>Their song turned strange with the void stirring: everyone at home who hears it uneasy tonight, told, and they leave.</summary>
        public static void Strange(Camp c, ListeningRingState st)
        {
            st.strange = true;
            if (StrangeDef != null)
                foreach (var p in IncidentWorker_Happening.Colonists(c.map))
                    if (p.GetStatValue(StatDefOf.PsychicSensitivity) >= PeoplesGMath.DeafBelow) CouncilGameComponent.GiveThought(p, StrangeDef);
            if (c.leader != null && c.leader.Spawned) c.SayMoment(c.leader, "strange");
            Tell.Letter(NewsKind.World, Importance.Important, c.map, "CC_PEv9_RingStrangeLabel".Translate().Resolve(), c.Text("CC_PEv9_RingStrange", c.leader), LetterDefOf.NegativeEvent, c.Look, c.faction);
            c.Leave("time");
        }

        private static bool LoudJob(JobDef j) =>
            j != null && (j == JobDefOf.Mine || j == JobDefOf.FinishFrame || j == JobDefOf.Deconstruct || j == JobDefOf.SmoothFloor || j == JobDefOf.SmoothWall || j == JobDefOf.Repair);

        /// <summary>A night's loud work near the ring (a colonist mining, building or taking down close by): heard once a night.</summary>
        public static void Loud(Camp c, ListeningRingState st, int day)
        {
            var loud = IncidentWorker_Happening.Colonists(c.map).FirstOrDefault(p => p.Spawned && LoudJob(p.CurJobDef) && p.Position.InHorDistOf(c.center, PeopleEvents9Math.LoudRadius));
            if (loud == null) return;
            st.loudDay = day;
            st.loudNights++;
            c.AddTension(st.promised ? PeopleEvents9Math.LoudTension : PeopleEvents9Math.LoudTension * 0.5f);
            if (c.leader != null && c.leader.Spawned) c.SayMoment(c.leader, "loud");
            if (st.promised) c.Note(c.Text("CC_PEv9_RingLoud", c.leader, loud), MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>An evening's song at their fire done.</summary>
        public static void Sang(Pawn by)
        {
            var c = CampOf(by);
            var st = StateOf(c);
            if (st == null) return;
            if (st.sangDay != GenDate.DaysPassed)
            {
                st.sangDay = GenDate.DaysPassed;
                c.SayMoment(by, "song");
            }
        }

        /// <summary>A psycaster of the colony's evening with them done: their focus full, a calm, told.</summary>
        public static void SatWith(Pawn p)
        {
            if (p == null || p.Dead || p.Faction == null || !p.Faction.IsPlayer) return;
            var c = CampTracker.VisitorsAt(p.Map).FirstOrDefault(x => x.kind == Kind);
            var st = StateOf(c);
            if (st != null) st.sat++;
            if (ModsConfig.RoyaltyActive && p.psychicEntropy != null && p.HasPsylink) p.psychicEntropy.OffsetPsyfocusDirectly(1f);
            if (CalmDef != null) CouncilGameComponent.GiveThought(p, CalmDef);
            if (c != null) c.Note(c.Text("CC_PEv9_RingSat", c.leader, p), MessageTypeDefOf.PositiveEvent, Importance.Notable, p, true);
        }

        /// <summary>The colony's psycaster with the least focus left, to sit with them.</summary>
        public static Pawn Psycaster(Map map) =>
            !ModsConfig.RoyaltyActive ? null : PeoplesG.Grown(map).Where(p => p.HasPsylink && p.psychicEntropy != null).OrderBy(p => p.psychicEntropy.CurrentPsyfocus).FirstOrDefault();

        /// <summary>A place beside their fire for one of the colony's.</summary>
        public static IntVec3 Beside(Camp c, Pawn p)
        {
            var map = c.map;
            return CellFinder.TryFindRandomCellNear(c.FireCell, map, 3, x => x.Standable(map) && x != c.FireCell && x.GetFirstPawn(map) == null && x.GetEdifice(map) == null
                                                                            && p.CanReach(x, PathEndMode.OnCell, Danger.Some), out var cell) ? cell : IntVec3.Invalid;
        }

        public static void Leave(Camp c, ListeningRingState st)
        {
            if (c.Hostile || c.tension >= 50f || st.strange) return;
            PeopleEvents9.Goodwill(c.faction, PeopleEvents9Math.RingGoodwill, GoodwillWhy.CampWell);
            if (st.promised && st.loudNights == 0) PeopleEvents9.Goodwill(c.faction, 2, GoodwillWhy.PromiseKept);
        }
    }

    /// <summary>The listening ring's ways: the evening's song at their fire, the colony feeling it, the promise of quiet, the strange song.</summary>
    public class CampWorker_ListeningRing : CampWorker
    {
        public override void Started(Camp c)
        {
            if (!(c.workState is ListeningRingState)) c.workState = new ListeningRingState();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (!(c.workState is ListeningRingState st))
            {
                Started(c);
                st = ListeningRing.StateOf(c);
            }
            if (!c.Settled || st == null) return;
            ListeningRing.Hour(c, st, hour);
        }

        /// <summary>At dusk all of them sing at their fire an hour and a half (the meditation's mark).</summary>
        public override Job NightJob(Camp c, Pawn p, int hour)
        {
            var st = ListeningRing.StateOf(c);
            if (st == null || !PeopleEvents9Math.SingHour(hour) || !p.RaceProps.Humanlike) return null;
            var cell = ListeningRing.Beside(c, p);
            return cell.IsValid ? PeopleEvents9.Make(PeopleEvents9.SingJob, cell, 3750) : null;
        }

        public override void Leaving(Camp c)
        {
            var st = ListeningRing.StateOf(c);
            if (st != null) ListeningRing.Leave(c, st);
        }

        public override void AddMarks(Camp c, HashSet<string> marks)
        {
            var st = ListeningRing.StateOf(c);
            if (st == null) return;
            if (st.strange) marks.Add("strange");
            if (st.nights >= 2) marks.Add("sang");
            if (st.promised && st.loudNights == 0 && st.nights >= 2) marks.Add("quiet");
        }

        public override string Fill(Camp c, string text) => text == null || !text.Contains("{NIGHTS}") ? text : text.Replace("{NIGHTS}", (ListeningRing.StateOf(c)?.nights ?? 0).ToString());

        public override string AskBlocked(Camp c, CampAsk a)
        {
            var st = ListeningRing.StateOf(c);
            if (st == null) return null;
            switch (a?.key)
            {
                case "sit":
                    return ListeningRing.Psycaster(c.map) == null ? "CC_PEv9_NoPsycaster".Translate().Resolve() : null;
                case "quiet":
                    return st.promised ? "CC_PEv9_Promised".Translate().Resolve() : null;
            }
            return null;
        }
    }

    /// <summary>A cantor of {FACTION} ({X}) at the gate: some of the Choir would camp a few days at the colony's quietest place and sing each evening.</summary>
    public class HappeningWorker_ListeningRing : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            ListeningRing.Kind != null && CampTracker.Enabled && CampTracker.RoomFor(map) && ListeningRing.On(map) == null && PeoplesD.Friendly(PeoplesMath.Listeners) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Listeners);
            if (ctx.faction == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Choir_Cantor"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ChoirListening.VoidStirs()) tags.Add("void");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (ListeningRing.On(ctx.map) != null || !CampTracker.RoomFor(ctx.map)) ? "CC_PEv2_NoRoomCamp".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "camp" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The listening ring made (ListeningRing.Start); <see cref="promise"/>: the colony promises at the gate to keep loud work away at night.</summary>
    public class DilemmaEffect_ListeningRing : DilemmaEffect
    {
        public bool promise;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Add(ImpactKind.People, 1, "CC_PEv9_Impact_Ring".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv9_Impact_RingBrief".Translate().Resolve(), "pev9ring");
            float chance = PeoplesGMath.UneaseChance(1f, ChoirListening.VoidStirs());
            sheet.Add(ImpactKind.Mood, 0, "CC_PEv9_Impact_RingFelt".Translate(Mathf.RoundToInt(chance * 100f).Named("CHANCE")).Resolve(), "CC_PEv9_Impact_RingFeltBrief".Translate().Resolve(), "pev9ringfelt");
            if (promise) sheet.Add(ImpactKind.Other, 0, "CC_PEv9_Impact_RingPromise".Translate().Resolve(), "CC_PEv9_Impact_RingPromiseBrief".Translate().Resolve(), "pev9ringpromise");
            if (ChoirListening.VoidStirs())
                sheet.Add(ImpactKind.Other, -1, "CC_PEv9_Impact_RingVoid".Translate().Resolve(), "CC_PEv9_Impact_RingVoidBrief".Translate().Resolve(), "pev9ringvoid");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents9Math.RingGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            var c = ListeningRing.Start(hc);
            if (promise && ListeningRing.StateOf(c) is ListeningRingState st) st.promised = true;
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>The camp's ask: the colony's psycaster with the least focus goes to sit with them at their fire.</summary>
    public class DilemmaEffect_RingSit : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) => ListeningRing.Psycaster(ctx?.map) == null ? "CC_PEv9_NoPsycaster".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var p = ListeningRing.Psycaster(ctx?.map);
            if (p != null) sheet.Add(ImpactKind.Skill, 1, "CC_PEv9_Impact_RingSit".Translate(p.LabelShort).Resolve(), "CC_PEv9_Impact_RingSitBrief".Translate().Resolve(), "pev9ringsit");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_ListeningRing);
            var p = ListeningRing.Psycaster(ctx?.map);
            if (c == null || p == null) return;
            var cell = ListeningRing.Beside(c, p);
            if (!PlayOuts.On || !cell.IsValid || !PeopleEvents9.Send(p, PeopleEvents9.RingSitJob, cell, PeopleEvents9Math.RingSitTicks)) ListeningRing.SatWith(p);
        }
    }

    /// <summary>The camp's request granted: loud work kept away from them at night (their promise kept or broken by the colony's own work).</summary>
    public class DilemmaEffect_RingQuiet : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) =>
            sheet.Add(ImpactKind.Other, 0, "CC_PEv9_Impact_RingPromise".Translate().Resolve(), "CC_PEv9_Impact_RingPromiseBrief".Translate().Resolve(), "pev9ringpromise");

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.About(ctx, x => x.kind?.Worker is CampWorker_ListeningRing);
            if (ListeningRing.StateOf(c) is ListeningRingState st) st.promised = true;
        }
    }
}
