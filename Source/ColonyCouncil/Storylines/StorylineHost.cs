using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A storyline's part whose people stay at the colony some days, as every hosting mission's guests (Missions/Hosting.cs:
    /// they eat at the colony's table and sleep in its beds): the order's healer, the pilgrims, the pledge. What the
    /// children's shelter does for itself (StorylineChildren.cs), shared: the storyline it belongs to, its title and terms,
    /// its messages and its end, and a day of the stay counted fed and sheltered or short.
    /// </summary>
    public abstract class HappeningWorker_StorylineHost : HappeningWorker_MissionHost, IStorylinePartWorker
    {
        public StorylinePart P => def.GetModExtension<StorylinePart>();

        protected override string Kind => "storyline:" + P?.story + ":" + P?.part;

        protected override float RestDays => 0f;

        protected override int Rating => 2;

        public bool CanOffer(IncidentParms parms) => CanFireNowSub(parms);

        protected override bool CanHappen(Map map, HappeningExtension ext) => StorylineKit.CanHappen(P, map, Possible);

        protected static Storyline Story(Mission m) => (m != null ? Storylines.OfMission(m.id) : null) ?? StorylineKit.Offering;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            StorylineKit.Cast(P, ctx.map);
            return base.Cast(ctx, ext);
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) => base.Begin(ctx, ext) && StorylineKit.Begin(P, ctx);

        protected override void Abort(HappeningContext ctx)
        {
            base.Abort(ctx);
            StorylineKit.Abort(P);
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            StorylineKit.AddTags(Missions.Of(ctx) ?? Making, tags);
        }

        /// <summary>A second sum the terms name ({SILVER}).</summary>
        protected virtual string Silver(Mission m, Storyline s) => s != null ? s.amount.ToString() : null;

        protected override string Extra(HappeningContext ctx)
        {
            var m = Missions.Of(ctx) ?? Making;
            return StorylineKit.Terms(P, ctx, m, Silver(m, Story(m)));
        }

        protected override void Send(HappeningContext ctx, HappeningExtension ext) => StorylineKit.Send(this, ctx, ext, Rating, Extra(ctx), Look(ctx));

        public virtual void Answered(Mission m, Storyline s, HappeningContext ctx, int option, bool byDefault)
        {
        }

        public virtual void Describe(Mission m, Storyline s, string choice, HappeningContext ctx, ImpactSheet sheet)
        {
        }

        protected void Note(Mission m, Map map, string key, LookTargets look, MessageTypeDef type = null, string item = null, string silver = null, string days = null) =>
            StorylineKit.Note(P, Story(m), m, map, key, look, type, item, silver, days);

        protected void End(Mission m, MissionState state, string outcome, LookTargets look = null, string item = null, string silver = null) =>
            StorylineKit.End(P, m, state, outcome, look, item, silver);

        protected void Say(Pawn who, Mission m, string key) => StorylineKit.Say(who, def, Story(m), m, key);

        /// <summary>The beds and sleeping spots the guests may sleep in (<see cref="Hosting.Beds"/>): free, or already one of theirs.</summary>
        protected List<Building_Bed> Beds(Mission m, Map map) => Hosting.Beds(map, o => Ours(m, o), m.x);

        protected override Thing FoodFor(Mission m, Map map, Pawn p) => Hosting.Meal(p, false) ?? Hosting.Raw(p);

        /// <summary>The bed they had last night; else a free one (single ones first); else a free side of another of theirs.</summary>
        protected override Building_Bed BedFor(Mission m, Map map, Pawn p)
        {
            var beds = Beds(m, map);
            var own = p.ownership?.OwnedBed;
            if (own != null && beds.Contains(own)) return own;
            return beds.Where(b => b.OwnersForReading.Count == 0).OrderBy(b => b.SleepingSlotsCount).ThenBy(b => b.Position.DistanceToSquared(p.Position)).FirstOrDefault()
                   ?? beds.Where(b => b.OwnersForReading.Count < b.SleepingSlotsCount && Hosting.CanShare(b, p)).OrderBy(b => b.Position.DistanceToSquared(p.Position)).FirstOrDefault();
        }

        /// <summary>
        /// A day of the stay: fed (nobody hungry with nothing of the colony's to eat) and sheltered (a bed for each), counted
        /// in <see cref="Mission.tally"/>; else short, counted in <see cref="Mission.misses"/> and told, naming <paramref name="who"/>.
        /// </summary>
        protected void Day(Mission m, Map map, List<Pawn> here, string who)
        {
            var lack = (Hosting.Lack)m.lacks;
            m.lacks = 0;
            if (here.Count == 0) return;
            bool fed = (lack & Hosting.Lack.Food) == 0;
            bool roofed = (lack & Hosting.Lack.Bed) == 0 && Hosting.Slots(Beds(m, map)) >= here.Count;
            if (fed && roofed)
            {
                m.tally++;
                return;
            }
            m.misses++;
            string key = !fed && !roofed ? "CC_Storyline_Host_Neither" : !fed ? "CC_Storyline_Host_Hungry" : "CC_Storyline_Host_NoRoof";
            ColonyCouncil.Tell.Message(NewsKind.World, Importance.Notable, map, key.Translate(who.Named("WHO")).Resolve(), new LookTargets(here), MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>
        /// Every look while they stay: fed and housed, and a day counted each full day; true while the stay goes on (before
        /// <see cref="Mission.deadline"/> and with someone of theirs still here).
        /// </summary>
        protected bool Stay(Mission m, Map map, int now, string who)
        {
            var here = Here(m, map).ToList();
            m.lacks |= (int)Tend(m, map);
            if (now - m.lastLook >= GenDate.TicksPerDay)
            {
                m.lastLook += GenDate.TicksPerDay;
                Day(m, map, here, who);
            }
            if (now < m.deadline && here.Count > 0) return true;
            // The last part of a day counts too: half a day or more of it, or any part they went without in.
            if (m.lacks != 0 || now - m.lastLook >= GenDate.TicksPerDay / 2) Day(m, map, here, who);
            return false;
        }

        /// <summary>The storyline's people stay its own: nothing is let go here but beds, and people who never came.</summary>
        public override void Release(Mission m)
        {
            var map = m.Map;
            foreach (var p in Guests(m).Where(p => p != null).Distinct().ToList())
            {
                Hosting.GetUp(p);
                Hosting.GiveBack(p, map);
                if (!p.Dead && !p.Spawned && m.started < 0 && p.GetCaravan() == null && !Storylines.Holds(p)) Arrivals.Discard(p);
            }
        }
    }
}
