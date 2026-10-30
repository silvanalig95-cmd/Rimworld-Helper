using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's allies: the other settlers whose year ended in an alliance (Council_WS_RivalColony, the "alliance"
    /// ending, World/Workers_World.cs). An ally answers a call in a bad year: food when the stores run thin
    /// (Council_Evt_AllyFood), and fighters when the colony is attacked (the war camp comes from them first, Camps/WarCamp.cs).
    /// </summary>
    public static class Allies
    {
        public const string Situation = "Council_WS_RivalColony", Ending = "alliance";

        /// <summary>The stores are thin enough that an ally sends food: fewer days of food than this.</summary>
        public const float HungryDays = 6f;

        /// <summary>A people bound to the colony as allies, still at peace with it; null when there is none.</summary>
        public static Faction Of()
        {
            var history = WorldSituations.Instance?.history;
            if (history == null) return null;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var r = history[i];
                if (r?.faction == null || r.defName != Situation || r.ending != Ending) continue;
                if (Arrivals.Usable(r.faction)) return r.faction;
            }
            return null;
        }

        public static bool IsAlly(Faction f) => f != null && Of() == f;

        /// <summary>The allies' steading: their town nearest the colony.</summary>
        public static string PlaceOf(Faction f, Map map) => WorldSituationWorker.NearestTown(f, map)?.Name ?? f?.Name ?? "";
    }

    /// <summary>
    /// The allies send food when the colony's stores run thin (Council_Evt_AllyFood): two of theirs at the gate with
    /// sacks of what they grow ({THING}, {COUNT} of it), from their steading ({PLACE}).
    /// </summary>
    public class HappeningWorker_AllyFood : HappeningWorker_Visitors
    {
        private static readonly string[] Grain = { "RawCorn", "RawRice", "RawPotatoes", "Hay", "RawBerries" };

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            Allies.Of() != null && AdvisorWorker_Food.DaysOfFood(map) < Allies.HungryDays && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Allies.Of();
            if (ctx.faction == null || Arrivals.PersonKind(ctx.faction) == null) return false;
            ctx.placeName = Allies.PlaceOf(ctx.faction, ctx.map);
            ctx.thing = Grain.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null);
            if (ctx.thing == null) return false;
            int people = Mathf.Max(1, Colonists(ctx.map).Count);
            ctx.count = Mathf.Max(20, Mathf.RoundToInt(ext.count.RandomInRange * Mathf.Lerp(0.7f, 1.6f, Mathf.Clamp01((people - 2) / 10f)) / 5f) * 5);
            MakeGroup(ctx, ext, 2, ext.ages, null, false);
            ctx.a = KinMemory.Greeter(ctx.faction, ctx.map) ?? StorytellerEvents.BestOrAny(ctx.map, SkillDefOf.Social);
            return ctx.stranger != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var everyone = ctx.group.Prepend(ctx.stranger).Where(p => p != null).ToList();
            return Arrivals.Visit(everyone, ctx.map, ctx.faction, GatheringSpot(ctx.map), ext.stayHours.RandomInRange) != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            if (ctx.map != null && AdvisorWorker_Food.DaysOfFood(ctx.map) < 2f) tags.Add("starving");
        }
    }
}
