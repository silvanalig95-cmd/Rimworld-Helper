using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A visitor the colony keeps up with: who, what they are on the road, whom they know here, and how the thread between them stands.</summary>
    public class Regular : IExposable
    {
        public int id;
        public Pawn pawn;
        /// <summary>Their trade on the road: tinker, scholar, doctor, healer, teacher, cook, scribe, teller, kindred.</summary>
        public string kind;
        public Faction faction;
        /// <summary>The colonist they dealt with first, and ask after.</summary>
        public Pawn friendOf;
        public int visits, bond, lastTick;

        private static int Now => Find.TickManager.TicksGame;

        public float DaysSince => (Now - lastTick) / (float)GenDate.TicksPerDay;

        public bool Alive => pawn != null && !pawn.Dead && !pawn.Discarded && faction != null && !faction.defeated && !faction.Hidden && !faction.HostileTo(Faction.OfPlayer) && pawn.Faction == faction;

        /// <summary>The skill their trade is known by (none for a kindred spirit).</summary>
        public SkillDef Skill
        {
            get
            {
                switch (kind)
                {
                    case "tinker": return SkillDefOf.Crafting;
                    case "scholar":
                    case "teacher": return SkillDefOf.Intellectual;
                    case "doctor":
                    case "healer": return SkillDefOf.Medicine;
                    case "cook": return SkillDefOf.Cooking;
                    case "scribe": return SkillDefOf.Artistic;
                    case "teller": return SkillDefOf.Social;
                    default: return null;
                }
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref friendOf, "friendOf");
            Scribe_Values.Look(ref visits, "visits");
            Scribe_Values.Look(ref bond, "bond");
            Scribe_Values.Look(ref lastTick, "lastTick");
        }
    }

    /// <summary>
    /// Visitors who come back (RegularMath). A visitor of a trade (the tinker, the scholar, the traveling doctor, the
    /// teacher, the cook, the scribe, the players' teller, a kindred spirit: the event's HappeningExtension.regular
    /// names it) is remembered when their event happens, and kept in the world. A few weeks on they may come back
    /// (Council_Evt_RegularBack): the same face at the gate, asking after the colonist they know, with a lesson in
    /// their trade for a warm welcome. Each warm visit tightens the bond, a cold answer loosens it; after three visits
    /// with the bond grown, the thread turns: they ask to stay for good, or ask their friend to come along on the road.
    /// Five such regulars at most; forgotten after half a year away.
    /// </summary>
    public static class Regulars
    {
        private static int Now => Find.TickManager.TicksGame;

        public static List<Regular> All => CouncilGameComponent.Instance?.happenings?.regulars ?? new List<Regular>();

        public static Regular Of(Pawn p) => p == null ? null : All.FirstOrDefault(r => r.pawn == p);

        /// <summary>A visitor's event happened: they're remembered, or seen again.</summary>
        public static void Note(string kind, HappeningContext ctx)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            var x = ctx?.stranger;
            if (t == null || kind.NullOrEmpty() || x == null || x.Dead || x.RaceProps == null || !x.RaceProps.Humanlike || x.Faction == null || x.Faction.IsPlayer
                || x.DevelopmentalStage.Juvenile()) return;
            var r = Of(x);
            if (r == null)
            {
                // A return of someone forgotten in the meantime: nothing to add to.
                if (kind == "back") return;
                r = new Regular { id = t.nextRegularId++, pawn = x, kind = kind, faction = x.Faction, friendOf = ctx.a, bond = 0, visits = 0 };
                t.regulars.Add(r);
            }
            r.visits++;
            r.lastTick = Now;
            if (kind != "back") r.bond += RegularMath.Warm;
            if (r.friendOf == null || r.friendOf.Dead) r.friendOf = ctx.a;
            Find.WorldPawns.ForcefullyKeptPawns.Add(x);
            while (t.regulars.Count > RegularMath.KeepMax)
            {
                int i = RegularMath.Weakest(t.regulars.Select(q => q.bond).ToArray(), t.regulars.Select(q => q.DaysSince).ToArray());
                if (i < 0) break;
                Forget(t.regulars[i]);
            }
        }

        /// <summary>The colony answered a regular's event: a cold answer loosens the bond.</summary>
        public static void Answered(HappeningContext ctx, DilemmaOption option)
        {
            var r = Of(ctx?.stranger);
            if (r == null || option?.ways == null) return;
            int change = RegularMath.BondChange(option.ways.openness);
            if (change < 0) r.bond += change;
        }

        public static void Warm(Pawn x, int amount = RegularMath.Warm)
        {
            var r = Of(x);
            if (r != null) r.bond += amount;
        }

        public static void Forget(Regular r)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            if (r == null || t == null) return;
            if (r.pawn != null && !r.pawn.Spawned && !r.pawn.IsColonist) Find.WorldPawns.ForcefullyKeptPawns.Remove(r.pawn);
            t.regulars.Remove(r);
        }

        /// <summary>Once a day: the dead, the fallen-out and the long unseen are forgotten.</summary>
        public static void Daily()
        {
            foreach (var r in All.ToList())
                if (!r.Alive || r.DaysSince > RegularMath.FadeDays || r.pawn.IsColonist) Forget(r);
        }

        /// <summary>The regular due back now, the closest bond first, then the longest away; null when none is.</summary>
        public static Regular Pick(Map map)
        {
            Regular best = null;
            foreach (var r in All)
            {
                if (!r.Alive || r.pawn.Spawned || !RegularMath.Due(r.DaysSince) || !Arrivals.Usable(r.faction)) continue;
                if (best == null || RegularMath.Compare(r.bond, r.DaysSince, best.bond, best.DaysSince) < 0) best = r;
            }
            return best;
        }

        /// <summary>For the World page: who the colony keeps up with ("the tinker Bolt, three visits").</summary>
        public static string Line()
        {
            var list = All.Where(r => r.Alive).OrderByDescending(r => r.bond).ToList();
            if (list.Count == 0) return null;
            var bits = list.Select(r => (r.visits <= 1 ? "CC_Regular_One_Once" : "CC_Regular_One").Translate(r.pawn.Named("X"),
                ("CC_Regular_Kind_" + r.kind).Translate().Named("KIND"), r.visits.Named("COUNT")).Resolve()).ToList();
            return "CC_Regular_Line".Translate(TextUtil.JoinAnd(bits)).Resolve();
        }
    }

    /// <summary>A regular comes back (Council_Evt_RegularBack): the same face, the same friend asked after, a lesson for a welcome; and in time the turning of the thread.</summary>
    public class HappeningWorker_Regular : HappeningWorker_Visitors
    {
        private Regular picked;

        protected override bool CanHappen(Map map, HappeningExtension ext) => Colonists(map).Count > 0 && Regulars.Pick(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            picked = Regulars.Pick(ctx.map);
            if (picked == null) return false;
            ctx.stranger = picked.pawn;
            ctx.faction = picked.faction;
            ctx.group.Clear();
            var friend = picked.friendOf;
            ctx.a = friend != null && !friend.Dead && friend.Spawned && friend.Map == ctx.map && friend.IsColonist && !friend.Downed ? friend
                : Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed).RandomElementWithFallback();
            if (ctx.a == null) return false;
            ctx.skill = picked.Skill;
            ctx.variant = picked.visits;
            ctx.count = picked.bond;
            ctx.tale = ("CC_Regular_Kind_" + picked.kind).Translate();
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, GatheringSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void Abort(HappeningContext ctx)
        {
            // Theirs to keep: a regular who couldn't come stays remembered.
            ctx.stranger = null;
            ctx.group.Clear();
            picked = null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var r = picked ?? Regulars.Of(ctx.stranger);
            if (r == null) return;
            tags.Add(r.kind);
            tags.Add(r.visits <= 1 ? "visit2" : "visit3plus");
            if (RegularMath.Turning(r.bond, r.visits)) tags.Add("turning");
            if (r.friendOf != null && r.friendOf == ctx.a) tags.Add("friend");
            if (r.bond <= 0) tags.Add("cool");
            if (ctx.skill != null) tags.Add("skill");
        }

        /// <summary>Staying, or taking their friend along, only once the thread has turned; and only while they're still here.</summary>
        public override string Unavailable(HappeningContext ctx, int option)
        {
            var r = Regulars.Of(ctx.stranger);
            if (option == 0 || option == 3) return null;
            if (r == null || ctx.stranger == null || ctx.stranger.Dead || !ctx.stranger.Spawned) return "CC_Regular_Gone".Translate().ToString();
            if (!RegularMath.Turning(r.bond, r.visits)) return "CC_Regular_NotReady".Translate(ctx.stranger.Named("X")).Resolve();
            if (option == 2 && (ctx.a == null || ctx.a.Dead || !ctx.a.IsColonist)) return "CC_Regular_NoFriend".Translate().ToString();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var r = Regulars.Of(ctx.stranger);
            switch (option)
            {
                case 0:
                    if (!byDefault) Regulars.Warm(ctx.stranger);
                    // Their evening at the colony's fire: they stay for it.
                    VisitorServices.StayFor(ctx.stranger, ctx.plays);
                    break;
                case 1:
                case 2:
                    if (r != null) Regulars.Forget(r);
                    break;
                case 3:
                    Regulars.Warm(ctx.stranger, RegularMath.Cold);
                    break;
            }
            return outcome;
        }
    }

    /// <summary>The regular stays for good: one of the colony now, with the chronicle's word on it.</summary>
    public class DilemmaEffect_RegularStays : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx) =>
            ctx?.stranger == null || ctx.stranger.Dead ? "CC_Regular_Gone".Translate().ToString() : null;

        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            if (x == null || x.Dead || ctx.map == null) return;
            // apart: the answer to a letter; they're at the gate already, and simply stay.
            if (!Arrivals.Join(x, ctx.map)) return;
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, "CC_Regular_Stayed".Translate(x.Named("X")).Resolve(), ctx.a));
        }
    }
}
