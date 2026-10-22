using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Effects that put the people a letter speaks of on the map (Visitors/). They sit in a world event's beat, a Rimfolk
    // event's effects or an answer's effects, next to the rest; with the setting off, or when nobody can come, they do
    // nothing and the letter tells it as before.

    /// <summary>What every visit effect shares: who comes and what they're called.</summary>
    public abstract class VisitEffect : DilemmaEffect
    {
        public IntRange count = new IntRange(1, 1);
        /// <summary>What they are, for what they say and the messages: rider, family, travelers, mourners, drovers...</summary>
        public string what = "rider";
        /// <summary>The second people ({ENEMY}) send them, not the first.</summary>
        public bool enemy;
        /// <summary>A family: a couple sharing a name, any more their children.</summary>
        public bool family;
        /// <summary>Armed, as a caravan's guards are: drovers, a band, hunters.</summary>
        public bool armed;
        public FloatRange ages = new FloatRange(18f, 50f);
        /// <summary>The first of them is this (a girl sent running with a message).</summary>
        public Gender gender = Gender.None;
        /// <summary>A child where the game has children, else a young teenager.</summary>
        public bool young;
        /// <summary>A beast of their caravans walks with them (a cart's ox, a sledge's beast): peoples of the old ways only.</summary>
        public bool pack;
        /// <summary>Their own animal: "dog", "goat", or a kind's defName (it joins with them if they're taken in).</summary>
        public string pet;
        /// <summary>Animals of a herd they drive; <see cref="beast"/> the kind wanted first ("Cow").</summary>
        public IntRange livestock = IntRange.Zero;
        public string beast;
        /// <summary>They go on past the colony when they leave, not back the way they came.</summary>
        public bool onward;
        /// <summary>A world event's flags: only when set / not when set.</summary>
        public string requires;
        public string forbids;

        protected bool Gate(DilemmaContext ctx)
        {
            if (!Visitors.On || ctx == null) return false;
            if (chance < 1f && !Rand.Chance(chance)) return false;
            if (requires.NullOrEmpty() && forbids.NullOrEmpty()) return true;
            var s = SituationEffectUtil.Of(ctx);
            if (!requires.NullOrEmpty() && (s == null || !s.Has(requires))) return false;
            if (!forbids.NullOrEmpty() && s != null && s.Has(forbids)) return false;
            return true;
        }

        protected VisitorTrip Trip(DilemmaContext ctx, Map map, Faction f, VisitKind kind)
        {
            int now = Find.TickManager.TicksGame;
            return new VisitorTrip
            {
                kind = kind, map = map, faction = f, what = what, count = count.RandomInRange, family = family, armed = armed, ages = ages,
                gender = gender, young = young, pack = pack, pet = pet, livestock = livestock.RandomInRange, beast = beast, onward = onward,
                created = now, due = now, giveUp = now + VisitorMath.GiveUpTicks, situationId = (ctx as SituationContext)?.situationId ?? -1
            };
        }
    }

    /// <summary>
    /// The people a letter says are at the gate are there: they walk in with it (from the event's people, at peace with
    /// the colony) and wait by the gate until the colony answers, then go (or stay the night, VisitEffect_Stay, or are
    /// taken in, WorldEffect_Join). The first of them is the event's {X} for the answers, if it has none: what the colony
    /// sends is carried out to them, and what their people pay they hand over themselves. Put it in a beat's (or an
    /// event's) effects, which come just before the letter. Nothing is asked: they stay <see cref="hours"/>, then go.
    /// </summary>
    public class VisitEffect_AtGate : VisitEffect
    {
        /// <summary>How long they stay when the letter asks nothing.</summary>
        public FloatRange hours = new FloatRange(2f, 4f);
        /// <summary>Came with next to nothing (robbed on the road).</summary>
        public bool robbed;
        /// <summary>The first of them becomes the event's {X} (when it has none).</summary>
        public bool stranger = true;

        public override void Apply(DilemmaContext ctx)
        {
            if (!Gate(ctx)) return;
            var map = SituationEffectUtil.MapOf(ctx);
            var f = Visitors.PeopleOf(ctx, enemy);
            var tracker = VisitorTracker.Instance;
            if (map == null || !map.IsPlayerHome || tracker == null || !Visitors.Usable(f)) return;
            try
            {
                var t = Trip(ctx, map, f, VisitKind.Wait);
                t.spotKind = VisitSpot.Gate;
                t.hours = hours.RandomInRange;
                t.robbed = robbed;
                t.ctxRef = ctx;
                if (!tracker.ComeNow(t)) return;
                if (ctx.map == null) ctx.map = map;
                var first = t.people.Find(p => p != null && p.Spawned);
                if (stranger && first != null && ctx.stranger == null) ctx.stranger = first;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visit at gate", e);
            }
        }
    }

    /// <summary>
    /// The answer gives those waiting at the gate a bed (or a corner, or the lee of the wall): they stay
    /// <see cref="nights"/> nights and go on at first light, if a night out there won't hurt them. With
    /// <see cref="spot"/>, they move to the far ground (a stargazer's hill) or just outside the colony to stay.
    /// </summary>
    public class VisitEffect_Stay : DilemmaEffect
    {
        public int nights = 1;
        /// <summary>Where they stay: where they are (the gate), or they move on to the far ground or just outside the colony.</summary>
        public VisitSpot spot = VisitSpot.Gate;

        public override void Apply(DilemmaContext ctx)
        {
            if (!Visitors.On) return;
            try
            {
                var tracker = VisitorTracker.Instance;
                tracker?.Stay(tracker.WaitingFor(ctx), nights, spot);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visit stay", e);
            }
        }
    }

    /// <summary>
    /// A group comes by: mourners on the road, a band, a herd and its drovers. They come between <see cref="hourFrom"/>
    /// and <see cref="hourTo"/>, at least <see cref="delay"/> hours from now, when it's safe; go to their spot (the gate,
    /// the far ground, the water, just outside the colony), stop there <see cref="stopHours"/> (or <see cref="nights"/>
    /// nights), and go on (with <see cref="VisitEffect.onward"/>, past the colony to the far side). If they can't come
    /// within a day and a half, they don't.
    /// </summary>
    public class VisitEffect_Come : VisitEffect
    {
        public VisitSpot spot = VisitSpot.Road;
        public int hourFrom = 7;
        public int hourTo = 18;
        public FloatRange delay = new FloatRange(0.5f, 2f);
        /// <summary>How long they stop at their spot (0: they walk past it).</summary>
        public FloatRange stopHours = FloatRange.Zero;
        /// <summary>They stay this many nights at their spot (a herd on the far meadow), going on at first light.</summary>
        public int nights;
        /// <summary>A beat of the world event that tells of this visit afterward: it waits until they've gone.</summary>
        public string holdBeat;

        public override void Apply(DilemmaContext ctx)
        {
            if (!Gate(ctx)) return;
            var map = SituationEffectUtil.MapOf(ctx);
            var f = Visitors.PeopleOf(ctx, enemy);
            var tracker = VisitorTracker.Instance;
            if (map == null || !map.IsPlayerHome || tracker == null || !Visitors.Usable(f)) return;
            try
            {
                var t = Trip(ctx, map, f, nights > 0 ? VisitKind.Stay : VisitKind.Pass);
                t.spotKind = spot;
                t.hourFrom = hourFrom;
                t.hourTo = hourTo;
                t.due += Mathf.RoundToInt(delay.RandomInRange * VisitorMath.TicksPerHour);
                t.giveUp = t.due + VisitorMath.GiveUpTicks;
                t.hours = stopHours.RandomInRange;
                t.stayNights = Mathf.Clamp(nights, 0, VisitorMath.MaxNights);
                t.holdBeat = holdBeat;
                tracker.Add(t);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visit come", e);
            }
        }
    }
}
