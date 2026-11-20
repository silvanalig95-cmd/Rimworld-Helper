using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // A promise in the seat, halfway through (the content update's A5, 20 November 2026): an election's promise is judged
    // only at the next yearly vote; halfway (25 days or more in the seat), when the map clearly shows it going badly by the
    // very test the vote uses (RecordMath.Judge), someone who stood against the holder for the seat (or else the one who
    // thinks least of them) raises it at the council. Answers: give them hands (the seat's own work on the map, the holder
    // and the one who raised it: a night's watch on the walls, the food brought in, the sickroom seen to, the herd brought
    // home, an evening together); have them answer for it at supper (a word at the table, and their "weight of office"
    // story has its hard call, A21); or leave it to the vote. Each promise is raised once.

    public partial class OfficeTracker
    {
        /// <summary>At most this many promises remembered as raised (the newest kept).</summary>
        public const int MaxRaised = 12;

        /// <summary>Dev mode (Offices/SeatDebug.cs): the next look takes any promise hands can help, however it is going.</summary>
        internal static bool DebugAny;

        /// <summary>
        /// A holder at home whose promise, not yet raised, is going badly halfway through (SeatMath.GoingBadly): the seat,
        /// the holder, their promise in the history, how it is judged, and what the map shows of it in words.
        /// </summary>
        internal bool TryHalfway(out OfficeDef office, out Pawn holder, out HistoryEntry promise, out PromiseTest test, out string evidence)
        {
            office = null;
            holder = null;
            promise = null;
            test = PromiseTest.None;
            evidence = null;
            var map = HomeMap;
            if (map == null) return false;
            int now = Find.TickManager.TicksGame;
            foreach (var o in Offices)
                for (int seat = 0; seat < o.Seats; seat++)
                {
                    var h = HolderOf(o, seat);
                    if (h == null || !h.Spawned || h.Map != map) continue;
                    var pr = PromiseOf(h, o, seat);
                    if (pr == null || raised.Contains(pr.id)) continue;
                    var t = RecordMath.TestOf(pr.values);
                    float before = RecordMath.Number(pr.values, RecordMath.ReadingAt);
                    float after = t == PromiseTest.None ? -1f : Reading(t, o, h);
                    float days = (now - pr.tick) / (float)GenDate.TicksPerDay;
                    if (after < 0f) continue;
                    if (!SeatMath.GoingBadly(t, before, after, days) && !(DebugAny && SeatMath.HandsFor(t) != SeatMath.Hands.None)) continue;
                    office = o;
                    holder = h;
                    promise = pr;
                    test = t;
                    evidence = Evidence(t, PromiseVerdict.Broken, h, o, before, after, false);
                    return true;
                }
            return false;
        }

        /// <summary>This promise has been raised at the council: not again.</summary>
        internal void Raised(int promiseId)
        {
            if (raised.Contains(promiseId)) return;
            raised.Add(promiseId);
            while (raised.Count > MaxRaised) raised.RemoveAt(0);
        }
    }

    /// <summary>
    /// The question of a promise going badly: B the holder, A who raised it, {OFFICE} the seat, {PROMISE} what was promised,
    /// {EVIDENCE} what the map shows of it. Its tags: hands:watch, hands:food, hands:sick, hands:herd or hands:evening (the
    /// seat's own work), and stood when A stood against B for the seat. Saved with the letter.
    /// </summary>
    public class DilemmaContextHalfway : DilemmaContext
    {
        public OfficeDef office;
        public string promise, evidence;
        public PromiseTest test;
        public bool stood;

        public SeatMath.Hands Hands => SeatMath.HandsFor(test);

        public override string SeatName => office?.Name ?? "";

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            args.Add((promise ?? "").Named("PROMISE"));
            args.Add((evidence ?? "").Named("EVIDENCE"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref office, "office");
            Scribe_Values.Look(ref promise, "promise");
            Scribe_Values.Look(ref evidence, "evidence");
            Scribe_Values.Look(ref test, "test");
            Scribe_Values.Look(ref stood, "stood");
        }
    }

    /// <summary>Finds a promise going badly halfway through, its holder (B) and who raises it (A).</summary>
    public class DilemmaWorker_SeatHalfway : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!SeatCalls.On || !CanHappenNow(map)) return false;
            var council = OfficeTracker.For(map);
            if (council == null || !council.TryHalfway(out var office, out var holder, out var promise, out var test, out string evidence)) return false;
            var people = map.mapPawns.FreeColonistsSpawned.Where(p => p != holder && !p.IsQuestLodger() && def.roleA.Allows(p, null)).ToList();
            var rivals = RecordMath.RivalsOf(promise.values);
            int Opinion(Pawn p) => p.relations?.OpinionOf(holder) ?? 0;
            var a = people.Where(p => rivals.Contains(p.thingIDNumber)).OrderBy(Opinion).FirstOrDefault();
            bool stood = a != null;
            a ??= people.Where(p => Opinion(p) < 20).OrderBy(Opinion).FirstOrDefault();
            if (a == null) return false;
            council.Raised(promise.id);
            ctx = new DilemmaContextHalfway
            {
                def = def, map = map, a = a, b = holder, office = office, test = test, stood = stood,
                promise = RecordMath.Get(promise.values, RecordMath.PromiseAt), evidence = evidence
            };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (!(ctx is DilemmaContextHalfway h)) return;
            tags.Add("hands:" + h.Hands.ToString().ToLowerInvariant());
            if (h.stood) tags.Add("stood");
        }
    }

    /// <summary>
    /// The holder is given hands with the seat's work (A5's first answer): the holder and the one who raised it, on the
    /// map: a night's watch on the walls, the harvest and the hauling, the sickroom cleaned and the medicine carried in,
    /// the herd brought home and seen to, or an evening the holder calls for everyone.
    /// </summary>
    public class DilemmaEffect_SeatHands : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private static DilemmaEffect_PlayOut For(DilemmaContext ctx)
        {
            var hands = (ctx as DilemmaContextHalfway)?.Hands ?? SeatMath.Hands.None;
            string label = ctx.Format("CC_Seat_HandsLabel".Translate().RawText);
            switch (hands)
            {
                case SeatMath.Hands.Watch:
                    return new DilemmaEffect_Vigil
                    {
                        label = label, target = DilemmaTarget.AAndB, organizer = PlayWho.B, count = 2, to = PlaySpot.Gate, patrol = true,
                        hours = new IntRange(21, 1), duration = new FloatRange(3f, 4f)
                    };
                case SeatMath.Hands.Food:
                    return new DilemmaEffect_WorkParty
                    {
                        label = label, target = DilemmaTarget.AAndB, organizer = PlayWho.B, count = 2,
                        tasks = new List<WorkTask> { WorkTask.Harvest, WorkTask.Fish, WorkTask.Haul }, hours = new IntRange(12, 18), duration = new FloatRange(2.5f, 3.5f)
                    };
                case SeatMath.Hands.Sick:
                    return new DilemmaEffect_WorkParty
                    {
                        label = label, target = DilemmaTarget.AAndB, organizer = PlayWho.B, count = 2,
                        tasks = new List<WorkTask> { WorkTask.Clean, WorkTask.Haul }, hours = new IntRange(12, 18), duration = new FloatRange(2.5f, 3.5f)
                    };
                case SeatMath.Hands.Herd:
                    return new DilemmaEffect_WorkParty
                    {
                        label = label, target = DilemmaTarget.AAndB, organizer = PlayWho.B, count = 2,
                        tasks = new List<WorkTask> { WorkTask.Herd, WorkTask.Tend, WorkTask.Haul }, hours = new IntRange(12, 18), duration = new FloatRange(2.5f, 3.5f)
                    };
                case SeatMath.Hands.Evening:
                    return new DilemmaEffect_Gathering
                    {
                        label = label, target = DilemmaTarget.Colony, organizer = PlayWho.B, spots = new List<PlaySpot> { PlaySpot.Rec, PlaySpot.Dining, PlaySpot.Fire },
                        hours = new IntRange(18, 22), duration = new FloatRange(2f, 2.5f)
                    };
                default:
                    return null;
            }
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx == null) return;
            For(ctx)?.Describe(ctx, sheet);
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.map == null) return;
            For(ctx)?.Apply(ctx);
        }
    }

    /// <summary>
    /// The holder answers for the promise at supper (A5's second answer): a word at the dining table, the holder speaking,
    /// for everyone at home; when it is over (or at once without the play-outs), it is the hard call of their new seat.
    /// </summary>
    public class DilemmaEffect_SeatSupper : DilemmaEffect
    {
        public override bool ChanceEach => false;

        /// <summary>Its afterward: the hard call, once the supper was held (Interplay.SeatWorkOver).</summary>
        public const string After = Interplay.SeatPrefix + "supper";

        private static DilemmaEffect_Words Words(DilemmaContext ctx) => new DilemmaEffect_Words
        {
            label = ctx.Format("CC_Seat_SupperLabel".Translate().RawText), target = DilemmaTarget.Colony, organizer = PlayWho.B,
            spots = new List<PlaySpot> { PlaySpot.Dining }, hours = new IntRange(17, 20), duration = new FloatRange(0.75f, 1f), pool = "Rimfolk_Pool_SeatSupper"
        };

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx == null) return;
            Words(ctx).Describe(ctx, sheet);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var h = ctx as DilemmaContextHalfway;
            if (ctx?.map == null || ctx.b == null) return;
            var play = Words(ctx).Begin(ctx);
            if (play != null)
            {
                // The promise rides with it for the story's {CALL} (a gathering carries no errand).
                play.afterward = After;
                play.what = h?.promise;
                return;
            }
            SeatCalls.Call(ctx.b, SeatMath.CallSupper, h?.promise ?? "");
        }
    }
}
