using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // Answers that send a few people out to do something together, each on a job of their own (never in a lord, so
    // nothing here keeps anyone from the game's own gatherings and rituals): a work party, a walk together, a vigil,
    // a search, an errand to carry something out, a hunt. Like the gatherings and visits (PlayOutEffects.cs) they wait
    // for their hours, for danger to pass and for people to be free; nobody is called from eating, sleeping, a sickbed
    // or work they were ordered to do; any danger, drafting or pressing need ends it for them. The engine is
    // PlayGroups.cs.

    /// <summary>
    /// The shared part of a work party, a walk, a vigil and a search: who may go (the effect's target; A unless given),
    /// how many go (<see cref="count"/>; the kind's own number unless given, never more than eight), who leads it
    /// (<see cref="organizer"/>, A unless given, when they're among those going), and what's said now and then while
    /// it goes on (<see cref="lines"/>, then <see cref="pool"/>, else the kind's own lines).
    /// </summary>
    public abstract class DilemmaEffect_Group : DilemmaEffect_PlayOut
    {
        public int count;
        public PlayWho organizer = PlayWho.A;
        public string pool;
        [MustTranslate] public List<string> lines;

        protected virtual int DefaultCount => 3;

        protected virtual bool Long => false;

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            var who = Everyone(ctx);
            if (who.Count == 0) return false;
            var lead = PlayOuts.Who(organizer, ctx);
            // The one who leads it goes first, when they're among those it's for.
            if (lead != null && who.Remove(lead)) who.Insert(0, lead);
            else lead = null;
            p.invited = who;
            p.organizer = lead;
            // The answer's own people hold their good moods until it's done; the rest of the colony has theirs at once.
            if (target == DilemmaTarget.A || target == DilemmaTarget.B || target == DilemmaTarget.AAndB || target == DilemmaTarget.X) p.named = who.ToList();
            else if (lead != null) p.named = new List<Pawn> { lead };
            p.count = Math.Min(PlayOutMath.MaxGroup, count > 0 ? count : DefaultCount);
            p.durationTicks = PlayOutMath.DurationTicks(PlayOutMath.CapHours(duration.RandomInRange, Long));
            p.pool = pool;
            p.lines = lines?.Where(l => !l.NullOrEmpty()).Select(ctx.Format).ToList() ?? new List<string>();
            p.place = (ctx as HappeningContext)?.place;
            p.seed = Rand.Range(0, 360);
            return true;
        }
    }

    /// <summary>
    /// A work party: a few of those it's for (the colony unless given; three unless <see cref="DilemmaEffect_Group.count"/>
    /// says) do the colony's own work together with the game's own jobs, in the order of <see cref="tasks"/> (cleaning,
    /// then hauling, unless given): filth in the home area, things lying about, damaged buildings, blueprints and
    /// frames, the dead, snow, sowing, the harvest, plants marked for cutting. Only what really needs doing, and only
    /// work they're able for; the nearest to <see cref="near"/> first (a place, such as the dining room after a feast),
    /// else to the middle of the colony. Nothing to do: it waits, then falls through.
    /// </summary>
    public class DilemmaEffect_WorkParty : DilemmaEffect_Group
    {
        public List<WorkTask> tasks;
        public List<PlaySpot> near;

        public DilemmaEffect_WorkParty()
        {
            target = DilemmaTarget.Colony;
            duration = new FloatRange(1.5f, 2.5f);
            hours = new IntRange(6, 20);
        }

        protected override PlayKind Kind => PlayKind.WorkParty;

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            if (!base.Fill(p, ctx)) return false;
            p.tasks = tasks.NullOrEmpty() ? new List<WorkTask> { WorkTask.Clean, WorkTask.Haul } : tasks.Distinct().ToList();
            p.spots = near?.ToList() ?? new List<PlaySpot>();
            return true;
        }
    }

    /// <summary>
    /// A walk together: those it's for (the colony unless given; six unless the count says) gather at a gather spot or
    /// in the open, walk out together to <see cref="to"/> (a grave, the gate, the fields, the water, the camp, the
    /// event's place, or a long way out, by default), stay a while, and walk back. A funeral procession, beating the
    /// bounds, seeing someone off to the road, a walk out to look at something.
    /// </summary>
    public class DilemmaEffect_Procession : DilemmaEffect_Group
    {
        public PlaySpot to = PlaySpot.Walk;

        public DilemmaEffect_Procession()
        {
            target = DilemmaTarget.Colony;
            duration = new FloatRange(1.5f, 2f);
            hours = new IntRange(7, 19);
        }

        protected override PlayKind Kind => PlayKind.Procession;

        protected override int DefaultCount => 6;

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            if (!base.Fill(p, ctx)) return false;
            p.to = to;
            p.other = ctx.stranger;
            return true;
        }
    }

    /// <summary>
    /// A vigil: those it's for (A unless given; up to three) keep watch quietly at <see cref="to"/> (a grave unless
    /// given; the gate, the fields, a fire, the event's place) or at someone's side (<see cref="to"/> Person, with
    /// <see cref="whom"/>: A unless given), through the evening and into the night unless other hours are given. Each
    /// stays until it's over, or until hunger or tiredness sends them off (a vigil isn't kept at the cost of collapsing).
    /// </summary>
    public class DilemmaEffect_Vigil : DilemmaEffect_Group
    {
        public PlaySpot to = PlaySpot.Grave;
        public PlayWho whom = PlayWho.A;

        public DilemmaEffect_Vigil()
        {
            duration = new FloatRange(4f, 6f);
            hours = new IntRange(19, 1);
        }

        protected override PlayKind Kind => PlayKind.Vigil;

        protected override bool Long => true;

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            if (!base.Fill(p, ctx)) return false;
            p.to = to;
            if (to == PlaySpot.Person || to == PlaySpot.Stranger)
            {
                var o = PlayOuts.Who(to == PlaySpot.Stranger ? PlayWho.X : whom, ctx);
                if (o == null || !DilemmaEffect_Visit.Here(o, ctx)) return false;
                // Nobody keeps watch at their own side.
                p.invited.Remove(o);
                p.named.Remove(o);
                if (p.organizer == o) p.organizer = null;
                if (p.invited.Count == 0) return false;
                p.other = o;
                p.to = PlaySpot.Person;
            }
            return true;
        }
    }

    /// <summary>
    /// A search: a few of those it's for (A unless given; one unless the count says, never more than four) go out and
    /// look over places round the colony (or round the fields, or along the water, with <see cref="to"/>), each their own
    /// way. When they're back the find is given, at the feet of whoever found it: <see cref="thing"/>
    /// (<see cref="amount"/> of it), or things of the colony's own era worth <see cref="value"/> silver, or nothing
    /// (a search for herbs, for a lost child, whose end the answer already told).
    /// </summary>
    public class DilemmaEffect_Search : DilemmaEffect_Group
    {
        public PlaySpot to = PlaySpot.Walk;
        public ThingDef thing;
        public IntRange amount = new IntRange(0, 0);
        public FloatRange value = new FloatRange(0f, 0f);

        public DilemmaEffect_Search()
        {
            duration = new FloatRange(2f, 3f);
            hours = new IntRange(6, 18);
        }

        protected override PlayKind Kind => PlayKind.Search;

        protected override int DefaultCount => 1;

        private bool Era => thing == null || thing.techLevel == TechLevel.Undefined || thing.techLevel <= Avail.EffectiveTech;

        public override void Apply(DilemmaContext ctx)
        {
            PlayOut p = null;
            try
            {
                p = Create(ctx);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out search", e);
            }
            // Nothing set going (the setting off, nobody to send): what the answer promised is found all the same.
            if (p != null || ctx?.map == null) return;
            try
            {
                PlayGroups.Drop(ctx.map, Era ? thing : null, Math.Max(1, amount.RandomInRange), value.RandomInRange, DropCellFinder.TradeDropSpot(ctx.map));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out search find", e);
            }
        }

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            if (!base.Fill(p, ctx)) return false;
            p.count = Math.Min(p.count, 4);
            p.to = to;
            // Only what fits the colony's era: nothing from beyond what it knows is ever found.
            if (thing != null && (thing.techLevel == TechLevel.Undefined || thing.techLevel <= Avail.EffectiveTech))
            {
                p.thing = thing;
                p.amount = Math.Max(1, amount.RandomInRange);
            }
            p.findValue = Math.Max(0f, value.RandomInRange);
            return true;
        }
    }

    /// <summary>
    /// An errand: someone (<see cref="who"/>, A unless given; anyone free and able to carry if they aren't) takes real
    /// things from the stores and carries them out: to the visitor (X), if they're here; else to the gate, where a rider
    /// or a cart waits; or to the camp (<see cref="to"/> Camp). What goes, and how much, is as the answer's own taking
    /// was: <see cref="what"/> ("silver", "rawfood", "medicine", "meals", "wood", or a thing's defName; empty: the event's
    /// thing), <see cref="count"/> (else the letter's {COUNT}) times <see cref="factor"/>, less the quartermaster's
    /// saving (<see cref="steward"/>), rounded to tens for a sum of silver the letter names (<see cref="tens"/>). The
    /// answer is greyed out without enough. One trip: what the carrier can't hold goes on the cart when they hand it
    /// over. If it never happens, or with the setting off, the things are taken from the stores at once, as always.
    /// </summary>
    public class DilemmaEffect_CarryOut : DilemmaEffect_PlayOut
    {
        public string what;
        public int count;
        public float factor = 1f;
        public bool steward = true;
        public bool tens;
        public PlayWho who = PlayWho.A;
        public PlaySpot to = PlaySpot.Stranger;

        public DilemmaEffect_CarryOut()
        {
            duration = new FloatRange(1f, 2f);
            hours = new IntRange(6, 21);
            waitHours = 4f;
        }

        protected override PlayKind Kind => PlayKind.Carry;

        private ThingDef Def(DilemmaContext ctx) => PlayCarry.DefOf(what, ctx?.thing);

        private int Need(DilemmaContext ctx) =>
            PlayOutMath.CarryAmount(count, ctx?.count ?? 0, factor, steward ? CouncilGameComponent.Instance?.offices.CostFactor ?? 1f : 1f, tens);

        public override string Unavailable(DilemmaContext ctx)
        {
            var map = ctx?.map ?? HappeningTracker.HomeMap();
            int need = Need(ctx);
            return PlayCarry.Have(map, what, Def(ctx)) < need ? "CC_Dilemma_NotEnough".Translate(need, PlayCarry.Label(what, Def(ctx))).ToString() : null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map ?? HappeningTracker.HomeMap();
            if (map == null) return;
            if (ctx.map == null) ctx.map = map;
            PlayOut p = null;
            try
            {
                p = Create(ctx);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out errand", e);
            }
            // Nothing set going: the things leave the stores now, as an answer's taking always did.
            if (p == null) PlayCarry.Take(map, what, Def(ctx), Need(ctx));
        }

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            var carrier = PlayOuts.Who(who, ctx);
            p.visitor = carrier != null && PlayOuts.OurPerson(carrier) ? carrier : null;
            // Whoever is free may carry it in their place.
            var all = ctx.Colonists.Where(PlayOuts.OurPerson).ToList();
            if (p.visitor != null)
            {
                all.Remove(p.visitor);
                all.Insert(0, p.visitor);
            }
            if (all.Count == 0) return false;
            p.invited = all;
            p.organizer = p.visitor;
            p.what = what;
            p.thing = Def(ctx);
            p.amount = Need(ctx);
            p.count = 1;
            p.to = to;
            var x = ctx.stranger;
            p.other = x != null && !x.Dead && x.Faction != Faction.OfPlayer && (x.RaceProps?.Humanlike ?? false) ? x : null;
            p.durationTicks = PlayOutMath.DurationTicks(PlayOutMath.CapHours(duration.RandomInRange, false));
            return true;
        }
    }

    /// <summary>
    /// A hunt: game is marked for the hunters (with <see cref="mark"/>, on by default; else what's already marked), and
    /// the colony's hunters (<see cref="who"/> first, when they can hunt; up to <see cref="count"/>) go out after it with
    /// the game's own hunting, by its own rules. The game: the event's animal (X), else the kinds in
    /// <see cref="kinds"/> ("Elk|Deer"), else the best eating on the map; <see cref="animals"/> of them. Never a predator,
    /// anything likely to turn on its hunters, or anyone's animal; only hunters with a hunting weapon. Over when the game
    /// is down, or when its time is up (the game got away, though it stays marked).
    /// </summary>
    public class DilemmaEffect_HuntParty : DilemmaEffect_PlayOut
    {
        public PlayWho who = PlayWho.None;
        public int count = 1;
        public int animals = 1;
        public string kinds;
        public bool mark = true;

        public DilemmaEffect_HuntParty()
        {
            duration = new FloatRange(3f, 4f);
            hours = new IntRange(6, 18);
        }

        protected override PlayKind Kind => PlayKind.Hunt;

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            var map = ctx.map;
            var game = PlayHunt.ChooseGame(map, ctx.stranger, kinds, Math.Max(1, Math.Min(3, animals)), !mark);
            if (game.Count == 0) return false;
            var hunters = ctx.Colonists.Where(PlayOuts.OurPerson).ToList();
            var first = PlayOuts.Who(who, ctx);
            if (first != null && hunters.Remove(first)) hunters.Insert(0, first);
            else first = null;
            if (hunters.Count == 0) return false;
            if (mark)
            {
                var dm = map.designationManager;
                foreach (var a in game)
                    if (dm.DesignationOn(a, DesignationDefOf.Hunt) == null) dm.AddDesignation(new Designation(a, DesignationDefOf.Hunt));
            }
            p.game = game;
            p.invited = hunters;
            p.organizer = first;
            p.count = Math.Max(1, Math.Min(2, count));
            p.durationTicks = PlayOutMath.DurationTicks(PlayOutMath.CapHours(duration.RandomInRange, false));
            Messages.Message("CC_Play_Hunt".Translate(TextUtil.JoinAnd(game.Select(a => a.LabelShort).ToList())).Resolve(), new LookTargets(game),
                MessageTypeDefOf.NeutralEvent, false);
            return true;
        }
    }
}
