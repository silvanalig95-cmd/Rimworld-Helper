using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Rimfolk events that follow from how a world event ended (texts: Defs/IncidentDefs/Rimfolk_Events_Aftermath.xml):
    // the plague's survivors come to give thanks, the first harvest after a famine is shared, the flooded-out come up
    // the river for what the water took, the snowbound travelers come back in spring, old friends send seed grain
    // after the colony's own bad year, and a reckoning for the times the colony looked away. Each follows a world event
    // only once, some days to weeks after it ended; none ends in a fight.

    /// <summary>Which world event an aftermath follows, and how it must have ended.</summary>
    public class AftermathEvent : DefModExtension
    {
        /// <summary>The world events it can follow (defNames).</summary>
        public List<string> after;
        /// <summary>Only after one of these endings (keys); empty: any but quietly.</summary>
        public List<string> endings;
        /// <summary>Only when the colony helped (an ending that counts as help) / only when it didn't.</summary>
        public bool helped;
        public bool ignored;
        /// <summary>Only when the world event ended with one of these flags (the swarm struck, food sold dear).</summary>
        public List<string> flags;
        /// <summary>Days after it ended.</summary>
        public FloatRange days = new FloatRange(6f, 45f);
        /// <summary>Someone of its people walks in ({X}, and a group of <see cref="HappeningExtension.count"/>); off: a letter only.</summary>
        public bool visitors = true;
        /// <summary>The visitors are old friends instead (a people that remembers the colony's good turns), not the world event's own people.</summary>
        public bool fromFriends;
        /// <summary>The goods they bring ({THING}, {COUNT}): the first of these that exists, and how much (scaled by the colony's size).</summary>
        public List<string> goods;
        public IntRange goodsCount = IntRange.Zero;
    }

    /// <summary>
    /// A follow-up to a world event that ended (see <see cref="AftermathEvent"/>): its people, its town ({PLACE}) and its
    /// name ({TALE}: "the gray cough") are that world event's. Lines can ask for what it was ([plague], [famine], [flood],
    /// [winter], [war], [drought], [locusts]), how it ended ([end:helped], [end:profiteer]...), whether the colony
    /// helped ([helped], [ignored]), and the people's memory ([their:grateful], [their:friend]...). {COUNT} is a gift's
    /// worth (or the goods), {THING} the goods. The visitor asks for the colonist their people remember, if any.
    /// </summary>
    public class HappeningWorker_Aftermath : HappeningWorker_Visitors
    {
        private static readonly AftermathEvent Empty = new AftermathEvent();

        public AftermathEvent A => def.GetModExtension<AftermathEvent>() ?? Empty;

        public const string NotePrefix = "after:";

        /// <summary>The world event a context's aftermath follows (by its end), or null.</summary>
        public static SituationRecord RecordOf(HappeningContext ctx)
        {
            string n = ctx?.note;
            if (n == null || !n.StartsWith(NotePrefix)) return null;
            string[] parts = n.Substring(NotePrefix.Length).Split('@');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int end)) return null;
            return WorldSituations.Instance?.history.FirstOrDefault(r => r != null && r.defName == parts[0] && r.endTick == end);
        }

        private IEnumerable<SituationRecord> Records()
        {
            var history = WorldSituations.Instance?.history;
            if (history == null) yield break;
            var a = A;
            int now = Now;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var r = history[i];
                if (r == null || r.defName == null || a.after.NullOrEmpty() || !a.after.Contains(r.defName) || r.Followed(def.defName)) continue;
                if (r.ending == null || r.ending == WorldSituations.Quietly) continue;
                if (!a.endings.NullOrEmpty() && !a.endings.Contains(r.ending)) continue;
                // Helped: an ending that counts as help, or any answer that did (a hard winter's thaw counts nobody as helped).
                bool helpedThem = r.helped || r.help > 0;
                if (a.helped && !helpedThem || a.ignored && helpedThem) continue;
                if (!a.flags.NullOrEmpty() && !a.flags.Any(r.Has)) continue;
                float days = (now - r.endTick) / (float)GenDate.TicksPerDay;
                if (days < a.days.min || days > a.days.max) continue;
                if (a.visitors && !a.fromFriends && (r.faction == null || r.faction.defeated || !LivingEvents.Friendly(r.faction) || Arrivals.PersonKind(r.faction) == null)) continue;
                yield return r;
            }
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            Colonists(map).Any(p => !p.DevelopmentalStage.Juvenile()) && Records().Any() && (!A.fromFriends || MissionPeoples.Grateful() != null);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var r = Records().FirstOrDefault();
            if (r == null) return false;
            var a = A;
            ctx.note = NotePrefix + r.defName + "@" + r.endTick;
            ctx.faction = a.fromFriends ? MissionPeoples.Grateful() : r.faction != null && !r.faction.defeated ? r.faction : null;
            ctx.placeName = ctx.faction != null ? LivingEvents.TownOf(ctx.faction, map) ?? ctx.faction.Name : "";
            ctx.tale = r.Called;
            // The colonist their people remember by name comes to the gate; else whoever talks best.
            ctx.a = (ctx.faction != null ? KinMemory.Greeter(ctx.faction, map) : null) ?? StorytellerEvents.BestOrAny(map, SkillDefOf.Social);
            if (ctx.a == null) return false;
            if (!a.goods.NullOrEmpty())
            {
                ctx.thing = a.goods.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null);
                if (ctx.thing == null) return false;
                int people = Mathf.Max(1, Colonists(map).Count);
                ctx.count = Mathf.Max(1, Mathf.RoundToInt(a.goodsCount.RandomInRange * Mathf.Lerp(0.7f, 1.6f, Mathf.Clamp01((people - 2) / 10f)) / 5f) * 5);
            }
            else if (ext.value.max > 0f) ctx.count = Mathf.Max(10, Mathf.RoundToInt(ext.value.RandomInRange * LivingEvents.WealthFactor(map) / 10f) * 10);
            if (a.visitors)
            {
                if (ctx.faction == null) return false;
                MakeGroup(ctx, ext, Mathf.Max(1, ext.count.RandomInRange), ext.ages);
                if (ctx.stranger == null) return false;
            }
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var r = RecordOf(ctx);
            if (r == null) return false;
            if (A.visitors)
            {
                var everyone = ctx.group.Prepend(ctx.stranger).Where(p => p != null).ToList();
                if (Arrivals.Visit(everyone, ctx.map, ctx.faction, GatheringSpot(ctx.map), ext.stayHours.RandomInRange) == null) return false;
            }
            r.Follow(def.defName);
            return true;
        }

        /// <summary>What the world event was, as a tag.</summary>
        public static string KindTag(string defName)
        {
            switch (defName)
            {
                case "Council_WS_Plague":
                case "Council_WS_Murrain": return "plague";
                case "Council_WS_Famine": return "famine";
                case "Council_WS_Locusts": return "locusts";
                case "Council_WS_Drought": return "drought";
                case "Council_WS_Flood": return "flood";
                case "Council_WS_HardWinter": return "winter";
                case "Council_WS_GreatFire": return "fire";
                case "Council_WS_Warlord":
                case "Council_WS_HolyWar":
                case "Council_WS_BanditLordFalls":
                case "Council_WS_Claimants":
                case "Council_WS_BanditRoad": return "war";
            }
            return null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var r = RecordOf(ctx);
            if (r == null) return;
            string k = KindTag(r.defName);
            if (k != null) tags.Add(k);
            if (!r.ending.NullOrEmpty()) tags.Add("end:" + r.ending);
            tags.Add(r.helped || r.help > 0 ? "helped" : "ignored");
            foreach (var f in r.flags ?? new List<string>()) tags.Add("flag:" + f);
        }
    }
}
