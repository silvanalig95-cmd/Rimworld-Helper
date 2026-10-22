using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // More world events, with the industrial and spacer colony in mind (each has a pre-industrial counterpart).

    /// <summary>A neighbour's cargo (a pod gone astray, or a runaway pack mule's load) lands on the colony's land. Give it back?</summary>
    public class HappeningWorker_LostCargo : IncidentWorker_Happening
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => Arrivals.Friendly() != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!base.Cast(ctx, ext)) return false;
            ctx.faction = Arrivals.Friendly();
            return ctx.faction != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var things = Loot.Make(ext.value.RandomInRange * Mathf.Clamp(Mathf.Sqrt(map.wealthWatcher.WealthTotal / 40000f), 0.6f, 2f));
            if (things.Count == 0) return false;
            var home = map.mapPawns.FreeColonistsSpawned.FirstOrDefault()?.Position ?? map.Center;
            if (!CellFinderLoose.TryGetRandomCellWith(c => c.Standable(map) && !c.Fogged(map) && !c.Roofed(map) && c.GetFirstBuilding(map) == null
                                                           && !map.areaManager.Home[c] && c.DistanceTo(home) > 20f, map, 500, out var cell)) return false;
            ctx.spot = cell;
            ctx.things.AddRange(things);
            ctx.tale = Loot.Describe(things);
            if (Avail.IsPreIndustrial)
                foreach (var t in things) GenPlace.TryPlaceThing(t, cell, map, ThingPlaceMode.Near);
            else DropPodUtility.DropThingsNear(cell, map, things, 110, false, false, false, false);
            return true;
        }

        protected override string Extra(HappeningContext ctx) => "CC_Evt_Found".Translate(ctx.tale ?? "").Resolve();

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // Sent back: whatever is still in one piece goes home with the owners' rider (or their next pod).
            if (option == 0 || option == 2)
            {
                int back = 0;
                float worth = 0f;
                var left = ctx.things.Where(t => t != null && !t.Destroyed).ToList();
                foreach (var t in left)
                {
                    back++;
                    worth += t.MarketValue * t.stackCount;
                }
                // Sent back for a finder's fee, and they paid it: 15% of what came back.
                bool fee = option == 2 && !risk && back > 0 && ctx.map != null;
                if (fee) ctx.count = Mathf.Max(20, Mathf.RoundToInt(worth * 0.15f / 5f) * 5);
                // Their people come for what's lying about (and bring the fee) (Couriers/); else it goes with their rider at once.
                var lying = left.Where(t => t.Spawned && t.Map == ctx.map).ToList();
                if (ctx.map != null && lying.Count > 0 && Couriers.CollectThings(ctx.map, lying, ctx.faction, fee ? Couriers.Make(ThingDefOf.Silver, ctx.count) : null))
                {
                    foreach (var t in left.Where(t => !t.Spawned)) t.Destroy();
                }
                else
                {
                    foreach (var t in left) t.Destroy();
                    if (fee)
                    {
                        var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                        silver.stackCount = ctx.count;
                        var near = (ctx.b ?? ctx.a) is Pawn p && p.Spawned && p.Map == ctx.map ? p.Position : ctx.spot.IsValid ? ctx.spot : ctx.map.Center;
                        GenPlace.TryPlaceThing(silver, near, ctx.map, ThingPlaceMode.Near);
                    }
                }
                if (back < ctx.things.Count) outcome += " " + "CC_Evt_CargoPartly".Translate().Resolve();
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>Someone wants to buy out the contract of the colony's most skilled colonist.</summary>
    public class HappeningWorker_TalentScout : IncidentWorker_Happening
    {
        private static SkillRecord Best(Pawn p) => p.skills?.skills.Where(r => !r.TotallyDisabled).OrderByDescending(r => r.Level).FirstOrDefault();

        private static IEnumerable<Pawn> Candidates(Map map) =>
            Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.IsSlave && Best(p) is SkillRecord r && r.Level >= 10);

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            Colonists(map).Count >= 3 && Candidates(map).Any() && Arrivals.Friendly() != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Candidates(ctx.map).OrderByDescending(p => Best(p).Level).FirstOrDefault();
            ctx.faction = Arrivals.Friendly();
            if (ctx.a == null || ctx.faction == null) return false;
            var r = Best(ctx.a);
            ctx.skill = r.def;
            ctx.count = Mathf.RoundToInt(r.Level * Rand.Range(70f, 110f) / 10f) * 10;
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (Pasts.Has(ctx.a, Past.Noble)) tags.Add("noble");
        }

        /// <summary>Asked to choose: whether they go depends on how they feel about the colony.</summary>
        public override float RiskChance(HappeningContext ctx, int option, float chance)
        {
            if (option != 1 || ctx.a == null) return base.RiskChance(ctx, option, chance);
            float trust = CouncilGameComponent.Instance?.standings.TrustOf(ctx.a) ?? 0f;
            float mood = ctx.a.needs?.mood?.CurLevelPercentage ?? 0.5f;
            // Content and trusting: stays. Bitter or miserable: gone.
            return Mathf.Clamp(0.45f - trust / 100f - (mood - 0.5f), 0.03f, 0.95f);
        }
    }

    /// <summary>A journalist (a chronicler, before electricity) writes about the colony. What they write depends on what it's known for.</summary>
    public class HappeningWorker_Journalist : HappeningWorker_Visitors
    {
        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            if (ctx.faction == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages);
            var r = ctx.stranger.skills?.GetSkill(SkillDefOf.Social);
            if (r != null && !r.TotallyDisabled && r.Level < 8) r.Level = Rand.RangeInclusive(8, 14);
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        /// <summary>How the colony comes across: merciful, generous and open make good copy; harsh, thrifty and guarded don't.</summary>
        private static int Reputation()
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !ColonyCouncilMod.Settings.colonyCharacter) return 0;
            var ch = comp.character;
            float score = ch.Value(Way.Mercy) + ch.Value(Way.Generosity) + ch.Value(Way.Openness);
            return score > 25f ? 1 : score < -25f ? -1 : 0;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option == 0 && !byDefault)
            {
                int rep = Reputation();
                string known = CouncilGameComponent.Instance?.character.Describe();
                outcome = (rep > 0 ? "CC_Evt_Article_Good" : rep < 0 ? "CC_Evt_Article_Bad" : "CC_Evt_Article_Plain")
                    .Translate(ctx.stranger?.LabelShort ?? "?", known ?? "CC_Evt_Article_Unknown".Translate().ToString()).Resolve();
                int goodwill = rep > 0 ? 4 : rep < 0 ? -3 : 1;
                foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.HasGoodwill && !f.HostileTo(Faction.OfPlayer)))
                    f.TryAffectGoodwillWith(Faction.OfPlayer, goodwill, false, false);
                var thought = rep > 0 ? CouncilDefOf.Council_Evt_GoodPress : rep < 0 ? CouncilDefOf.Council_Evt_BadPress : null;
                if (thought != null && ctx.map != null)
                {
                    using var _cause = Causes.Push(ctx.cause);
                    foreach (var p in Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Baby())) CouncilGameComponent.GiveThought(p, thought);
                }
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>A travelling doctor (a herbalist, before electricity) sells protection against sickness for the whole colony.</summary>
    public class HappeningWorker_Inoculation : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Colonists(map).Count >= 2;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            if (ctx.faction == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages);
            var r = ctx.stranger.skills?.GetSkill(SkillDefOf.Medicine);
            if (r != null && !r.TotallyDisabled && r.Level < 8) r.Level = Rand.RangeInclusive(8, 15);
            int people = Colonists(ctx.map).Count;
            ctx.count = Mathf.RoundToInt(people * (Avail.IsPreIndustrial ? 10f : 18f) / 5f) * 5;
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // Paid for: everyone at home gets it (a shot, or a bitter tonic, by era).
            if (option == 0 && ctx.map != null)
            {
                var hediff = Avail.IsPreIndustrial ? CouncilDefOf.Council_HerbalTonic : CouncilDefOf.Council_Inoculated;
                if (hediff != null)
                    foreach (var p in ctx.map.mapPawns.FreeColonistsSpawned.ToList())
                        if (p.health?.hediffSet?.HasHediff(hediff) == false) p.health.AddHediff(hediff);
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>Music: an old station on the comms (or minstrels camped nearby, before electricity).</summary>
    public class HappeningWorker_Music : IncidentWorker_Happening
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (Colonists(map).Count == 0) return false;
            // Without electricity there are minstrels; with it, someone has to own a radio.
            if (Avail.IsPreIndustrial) return true;
            return map.listerBuildings.allBuildingsColonist.Any(b => b.def.defName == "CommsConsole" || b.def.defName.Contains("Radio"));
        }
    }
}
