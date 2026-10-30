using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The world round (Defs/WorldSituationDefs/Rimfolk_WS_World.xml): other settlers a few days off, whose year beside
    // the colony ends in friendship, in their joining, in a feud or in their giving up; the road the colony let past its
    // gate, used, watched by wardens who put a post by the colony, and in time a crossroads town; and the camps a world
    // event calls for (WorldEffect_Camp, WorldEffect_CampSettle).

    /// <summary>
    /// Other settlers of {FACTION} found a steading a few days from the colony ({PLACE}: a settlement on the world map,
    /// made as a new town is), led by {X}, who comes to the gate at the start and at the turning of the year (the
    /// visits' withStranger). The steading grows by RivalMath (households, in {COUNT}); the colony's answers warm or
    /// sour them, and the flag "sour" is kept up to date from the flags the answers set, so the beats and endings can
    /// read it. Endings: alliance (the town stays, friends), absorbed (they join the colony and the town is left),
    /// feud, failed (they give up: an abandoned settlement), or they simply stay as neighbours.
    /// </summary>
    public class SituationWorker_RivalColony : SituationWorker_NewTown
    {
        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            var x = MakePerson(s, s.ctx.faction, null, new FloatRange(28f, 52f));
            if (x == null)
            {
                // No leader to be had: the steading founded a moment ago goes with the story.
                Town(s)?.Destroy();
                return false;
            }
            s.ctx.stranger = x;
            s.SetNum("start", Rand.RangeInclusive(RivalMath.StartMin, RivalMath.StartMax));
            s.ctx.count = Households(s);
            return true;
        }

        public static int Households(WorldSituation s) => RivalMath.Households(Mathf.Max(1, s.Num("start")), s.DaysIn, RivalMath.Warm(s.flags));

        /// <summary>Where the colony stands with them, from every answer so far.</summary>
        private static void Sync(WorldSituation s)
        {
            bool sour = RivalMath.Sour(s.flags);
            if (sour && !s.Has("sour")) s.Set("sour");
            else if (!sour && s.Has("sour")) s.Clear("sour");
        }

        public override string Summary(WorldSituation s)
        {
            s.ctx.count = Households(s);
            return base.Summary(s);
        }

        public override bool BeatCanHappen(WorldSituation s, SituationBeat beat, Map map, out bool retry)
        {
            Sync(s);
            return base.BeatCanHappen(s, beat, map, out retry);
        }

        public override bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            // The households, unless the beat counts something of its own (the food they ask for).
            if (beat.count.max <= 0) ctx.count = Households(s);
            return true;
        }

        public override string Unavailable(WorldSituation s, SituationContext ctx, SituationBeat beat, int option)
        {
            var o = beat.options[option];
            if (o.flags != null && o.flags.Contains("joined") && !RivalMath.CanAbsorb(Households(s)))
                return "CC_WS_Rival_TooMany".Translate(ctx.placeName ?? "", Households(s)).Resolve();
            return null;
        }

        public override string Chosen(WorldSituation s, SituationContext ctx, SituationBeat beat, int option, bool risk, bool byDefault, string outcome)
        {
            var o = beat.options[option];
            // The answer's own flags may not be on the situation yet: read them with the rest.
            var flags = s.flags.ToList();
            if (o.flags != null) flags.AddRange(o.flags);
            if (risk && o.riskFlags != null) flags.AddRange(o.riskFlags);
            if (o.clears != null) flags.RemoveAll(o.clears.Contains);
            bool sour = RivalMath.Sour(flags);
            if (sour && !s.Has("sour")) s.Set("sour");
            else if (!sour && s.Has("sour")) s.Clear("sour");
            return outcome;
        }

        public override SituationEnding TimeoutEnding(WorldSituation s)
        {
            Sync(s);
            return base.TimeoutEnding(s);
        }

        public override void Ended(WorldSituation s, SituationEnding ending)
        {
            if (ending == null || (ending.key != "absorbed" && ending.key != "failed")) return;
            var town = Town(s);
            if (town == null || !SituationWorker_Abandoned.Safe(town)) return;
            try
            {
                if (ending.key == "failed")
                {
                    var ruin = WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.AbandonedSettlement);
                    ruin.Tile = town.Tile;
                    ruin.SetFaction(town.Faction);
                    Find.WorldObjects.Add(ruin);
                }
                town.Destroy();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event other settlers", e);
            }
        }
    }

    /// <summary>
    /// {FACTION}'s road wardens ask leave to put a post by the colony. Let build, a wardens' camp comes
    /// (Council_Camp_Wardens, WorldEffect_Camp) and, when the post is finished, settles as the colony's neighbour
    /// (WorldEffect_CampSettle, Camps/Holding.cs). Flags: here (the camp came), built (it settled), left (the wardens
    /// went before it was done), noRoom (another camp had settled meanwhile), near, refused.
    /// </summary>
    public class SituationWorker_Waystation : WorldSituationWorker
    {
        public static CampKindDef Wardens => DefDatabase<CampKindDef>.GetNamedSilentFail("Council_Camp_Wardens");

        protected override bool CanHappen(Map map)
        {
            var tracker = CampTracker.Instance;
            var kind = Wardens;
            return CampTracker.Enabled && tracker != null && tracker.holding == null && kind != null && kind.FitsEra() && CampSite.AnyRoom(map);
        }

        public override bool BeatCanHappen(WorldSituation s, SituationBeat beat, Map map, out bool retry)
        {
            if (!base.BeatCanHappen(s, beat, map, out retry)) return false;
            if (beat.key != "settle") return true;
            var tracker = CampTracker.Instance;
            var c = tracker?.camp;
            if (c == null || c.kind != Wardens)
            {
                // The wardens left before the post was done (the weather, a raid): the story goes on without it.
                s.Set("left");
                retry = false;
                return false;
            }
            if (tracker.holding != null)
            {
                s.Set("noRoom");
                retry = false;
                return false;
            }
            if (!c.Settled || !c.CanTalk)
            {
                retry = true;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// A crossroads town grows up at the wardens' post: a new town ({PLACE}) of {FACTION}, founded as the situation
    /// starts (as a new town is). The colony may give it its own name (the "name" beat's first answer).
    /// </summary>
    public class SituationWorker_Crossroads : SituationWorker_NewTown
    {
        public override string Chosen(WorldSituation s, SituationContext ctx, SituationBeat beat, int option, bool risk, bool byDefault, string outcome)
        {
            if (beat.key != "name" || option != 0) return outcome;
            var town = Town(s);
            if (town == null) return outcome;
            var home = ctx.map?.Parent as Settlement;
            string ours = home != null && home.HasName ? home.Name : Faction.OfPlayer.Name;
            try
            {
                town.Name = "CC_WS_CrossingName".Translate(ours).Resolve();
                s.ctx.placeName = ctx.placeName = town.Name;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event crossroads name", e);
            }
            return outcome;
        }
    }

    /// <summary>
    /// A camp of one kind comes now, for the situation (a wardens' post by the new road): from the situation's people
    /// when the kind fits them, else from any friendly people. Nothing when a camp is here already or none can come;
    /// the letter tells it as before. With <see cref="flag"/>, the situation gets that flag when the camp came.
    /// </summary>
    public class WorldEffect_Camp : WorldEffect
    {
        public string kind;
        public string flag;

        public override void Apply(DilemmaContext ctx)
        {
            var def = DefDatabase<CampKindDef>.GetNamedSilentFail(kind ?? "");
            var map = SituationEffectUtil.MapOf(ctx);
            var tracker = CampTracker.Instance;
            if (def == null || map == null || tracker == null || !CampTracker.Enabled || !Gate(ctx)) return;
            Camp c;
            try
            {
                c = tracker.StartKind(map, def, ctx.faction);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event camp " + kind, e);
                return;
            }
            if (c != null && !flag.NullOrEmpty()) SituationEffectUtil.Of(ctx)?.Set(flag);
        }
    }

    /// <summary>
    /// The camp of this kind on the map settles for good as the colony's neighbour (Camps/Holding.cs): the wardens'
    /// post is finished. Nothing when it isn't here, can't be talked to, or the land has a neighbour already.
    /// </summary>
    public class WorldEffect_CampSettle : WorldEffect
    {
        public string kind;
        public string flag;

        public override void Apply(DilemmaContext ctx)
        {
            var tracker = CampTracker.Instance;
            var c = tracker?.camp;
            if (c == null || tracker.holding != null || c.kind?.defName != kind || !c.CanTalk || !Gate(ctx)) return;
            string told;
            try
            {
                told = c.Settle();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event camp settles " + kind, e);
                return;
            }
            if (told != null && !flag.NullOrEmpty()) SituationEffectUtil.Of(ctx)?.Set(flag);
        }
    }
}
