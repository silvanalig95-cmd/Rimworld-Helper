using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 96. The moot over the old wood (the Deepwell Union and the Mossborn) =====================

    /// <summary>
    /// The moot over the old wood (item 96): a pit of the Deepwell Union wants to sink a shaft under an old wood near a grove of the
    /// Mossborn, and both send an envoy to the colony as neutral ground. The colony's best talker hears them at its table (two hours,
    /// both envoys making their case) and rules: for the shaft (the union thinks better of the colony, the Mossborn worse, and steel is a
    /// fifth cheaper with every trader for a season), for the wood (the reverse, and the Mossborn's herbal medicine a fifth cheaper), or a
    /// bargain by the talker's Social (struck: the shaft at the wood's edge with a seedling planted for every tree felled, both a little
    /// better and steel a tenth cheaper; failed: both a little worse). The region hears of it. Kept as one of the events' visits (kind
    /// "pev:moot"): x the union's envoy, group the Mossborn's, a the talker, place the table, cell where they sit when there is none,
    /// stake 0 waiting or 1 at the table, text "union|mossborn|ruling".
    /// </summary>
    public static class WoodMoot
    {
        public const string Kind = "pev:moot";

        private static int Now => Find.TickManager.TicksGame;

        private static string[] Parts(Visit v) => (v?.text ?? "").Split('|');

        public static Faction Union(Visit v) => PeopleEvents10.PeopleById(Parts(v).ElementAtOrDefault(0));

        public static Faction Mossborn(Visit v) => PeopleEvents10.PeopleById(Parts(v).ElementAtOrDefault(1));

        public static string Ruling(Visit v) => Parts(v).ElementAtOrDefault(2) ?? "bargain";

        /// <summary>The colony's best talker, up and about.</summary>
        public static Pawn Talker(Map map) => ConsoleTalk.Talker(map);

        /// <summary>The answer taken: the hearing at the colony's table (or at once, with the play-outs off).</summary>
        public static void Begin(HappeningContext ctx, string ruling)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var moss = ctx?.group?.FirstOrDefault(p => p != null && p.Faction == ctx.enemy);
            if (map == null || t == null || ctx.a == null || ctx.faction == null || ctx.enemy == null) return;
            var v = t.AddVisit(Kind, ctx.incident, map, ctx.stranger, ctx.a, moss != null ? new List<Pawn> { moss } : null,
                Now + PeopleEvents10Math.HearWaitHours * GenDate.TicksPerHour + PeopleEvents10Math.HearTicks + 2 * GenDate.TicksPerHour);
            v.text = ctx.faction.GetUniqueLoadID() + "|" + ctx.enemy.GetUniqueLoadID() + "|" + ruling;
            var at = ConsoleTalk.Table(map, ctx.a);
            v.place = at.HasThing ? at.Thing : null;
            v.cell = at.Cell;
            v.stake = 0;
            if (!PlayOuts.On)
            {
                Rule(v, true);
                return;
            }
            TryHear(v);
        }

        /// <summary>The hearing begun: the talker and both envoys at the table, each envoy making their case.</summary>
        private static void TryHear(Visit v)
        {
            var map = v.map;
            var talker = v.a;
            if (map == null || !PeoplesG.Up(talker, map) || talker.Drafted || MapFacts.ThreatOn(map)) return;
            LocalTargetInfo at = v.place != null && v.place.Spawned ? new LocalTargetInfo(v.place) : new LocalTargetInfo(v.cell);
            if (!at.IsValid) return;
            if (!PeopleEvents10.Send(talker, PeopleEvents10.HearJob, at, PeopleEvents10Math.HearTicks)) return;
            v.stake = 1;
            float hours = PeopleEvents10Math.HearTicks / (float)GenDate.TicksPerHour + 3f;
            foreach (var x in PeopleEvents10.Up(v)) PeopleEvents10.Errand(x, map, at, PeopleEvents10.PleadJob, PeopleEvents10Math.HearTicks + GenDate.TicksPerHour, hours);
            PeoplesG.SayLine(v.x, v.incident, "shaftcase");
            PeoplesG.SayLine(v.group.FirstOrDefault(), v.incident, "woodcase");
        }

        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            // Waited too long for the hearing, or both envoys gone: the ruling goes after them by word.
            if (now > v.until || PeopleEvents10.Up(v).Count == 0)
            {
                Rule(v, false);
                return false;
            }
            if (v.stake == 0) TryHear(v);
            // The talker called away before the end: the hearing waits to begin again.
            else if (v.a == null || !v.a.Spawned || v.a.CurJobDef != PeopleEvents10.HearJob) v.stake = 0;
            return !v.done;
        }

        /// <summary>The talker's hearing at the table done (round 9's work at a place): the ruling. False when it wasn't this hearing.</summary>
        public static bool Heard(Pawn talker)
        {
            var v = PeopleEvents10.OfColonist(Kind, talker);
            if (v == null) return false;
            Rule(v, true);
            return true;
        }

        /// <summary>
        /// The ruling, as the card said: goodwill, the peoples' memory of it, the season's price, the talker's lesson; told in a letter and
        /// on the roads, and the envoys home. <paramref name="atTable"/> false: they were never heard at the table, and the ruling went to
        /// them by word.
        /// </summary>
        private static void Rule(Visit v, bool atTable)
        {
            v.done = true;
            var map = v.map;
            var union = Union(v);
            var moss = Mossborn(v);
            var talker = v.a;
            string ruling = Ruling(v);
            if (union == null || moss == null) return;
            if (atTable && talker != null && !talker.Dead) talker.skills?.Learn(SkillDefOf.Social, PeopleEvents10Math.HearSocialXp, true);
            int social = talker?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            bool struck = ruling == "bargain" && PeopleEvents10Math.Bargained(Rand.Value, social);
            var unionEnvoy = v.x;
            var mossEnvoy = v.group.FirstOrDefault();
            string key;
            switch (ruling)
            {
                case "shaft":
                    PeopleEvents10.Goodwill(union, PeopleEvents10Math.MootGain, GoodwillWhy.Judged);
                    PeopleEvents10.Goodwill(moss, PeopleEvents10Math.MootLoss, GoodwillWhy.Judged);
                    FactionMemory.Note(union, "sidedWith", moss.Name);
                    FactionMemory.Note(moss, "tookSides", union.Name);
                    PeoplesG.SayLine(unionEnvoy, v.incident, "wonshaft");
                    PeoplesG.SayLine(mossEnvoy, v.incident, "lostwood");
                    key = "CC_PEv10_MootShaft";
                    break;
                case "wood":
                    PeopleEvents10.Goodwill(moss, PeopleEvents10Math.MootGain, GoodwillWhy.Judged);
                    PeopleEvents10.Goodwill(union, PeopleEvents10Math.MootLoss, GoodwillWhy.Judged);
                    FactionMemory.Note(moss, "sidedWith", union.Name);
                    FactionMemory.Note(union, "tookSides", moss.Name);
                    PeoplesG.SayLine(mossEnvoy, v.incident, "wonwood");
                    PeoplesG.SayLine(unionEnvoy, v.incident, "lostshaft");
                    key = "CC_PEv10_MootWood";
                    break;
                default:
                    int each = struck ? PeopleEvents10Math.BargainGain : PeopleEvents10Math.BargainLoss;
                    PeopleEvents10.Goodwill(union, each, GoodwillWhy.Judged);
                    PeopleEvents10.Goodwill(moss, each, GoodwillWhy.Judged);
                    if (struck)
                    {
                        FactionMemory.Note(union, "mediated", moss.Name);
                        FactionMemory.Note(moss, "mediated", union.Name);
                    }
                    PeoplesG.SayLine(unionEnvoy, v.incident, struck ? "bargain" : "nobargain");
                    key = struck ? "CC_PEv10_MootBargain" : "CC_PEv10_MootNoBargain";
                    break;
            }
            string thing = PeopleEvents10Math.PricedThing(ruling, struck);
            float factor = PeopleEvents10Math.PriceFactor(ruling, struck);
            var def = thing == null ? null : DefDatabase<ThingDef>.GetNamedSilentFail(thing);
            if (def != null && factor != 1f)
                PeopleEvents10Book.Instance?.AddPrice(thing, factor, Now + PeopleEvents10Math.SeasonDays * GenDate.TicksPerDay, (ruling == "wood" ? moss : union).Name);
            string text = key.Translate((talker?.LabelShort ?? "?").Named("TALKER"), union.Name.Named("UNION"), moss.Name.Named("MOSSBORN"),
                Mathf.RoundToInt((1f - factor) * 100f).Named("PCT")).Resolve();
            if (!atTable) text = "CC_PEv10_MootByWord".Translate().Resolve() + " " + text;
            Tell.Letter(NewsKind.People, Importance.Notable, map, "CC_PEv10_MootLetterLabel".Translate().Resolve(), text, LetterDefOf.NeutralEvent,
                talker != null && talker.Spawned ? new LookTargets(talker) : LookTargets.Invalid, ruling == "wood" ? moss : union);
            string heard = (ruling == "shaft" ? "CC_PEv10_HeardShaft" : ruling == "wood" ? "CC_PEv10_HeardWood" : struck ? "CC_PEv10_HeardBargain" : "CC_PEv10_HeardNoBargain")
                .Translate(union.Name.Named("UNION"), moss.Name.Named("MOSSBORN")).Resolve();
            PeopleEvents10.Heard(heard, LookTargets.Invalid);
            PeoplesG.Home(PeopleEvents10.Up(v));
        }

        /// <summary>The hour's look at the season's prices: one whose season is over is told.</summary>
        public static void Prices(int now)
        {
            var b = PeopleEvents10Book.Instance;
            if (b == null || b.prices.Count == 0) return;
            foreach (var p in b.Refresh(now))
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(p.thing ?? "");
                if (def == null) continue;
                Tell.Message(NewsKind.World, Importance.Flavor, Homes.All.FirstOrDefault(),
                    "CC_PEv10_PriceOver".Translate(def.label.Named("THING"), (p.why ?? "?").Named("FACTION")).Resolve(), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            }
        }
    }

    /// <summary>
    /// An envoy of the Deepwell Union ({FACTION}, {X}) and one of the Mossborn ({ENEMY}) at the gate together: the union's pit wants to
    /// sink a shaft under an old wood by a Mossborn grove, and they ask the colony to judge it on neutral ground, {A} hearing them at its
    /// table.
    /// </summary>
    public class HappeningWorker_WoodMoot : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeopleEvents10.Pair(PeoplesMath.Drillers, PeoplesMath.Grovekeepers, out _) != null && WoodMoot.Talker(map) != null
            && PeopleEvents10.OfColonist(WoodMoot.Kind, WoodMoot.Talker(map)) == null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeopleEvents10.Pair(PeoplesMath.Drillers, PeoplesMath.Grovekeepers, out var moss);
            ctx.enemy = moss;
            ctx.a = WoodMoot.Talker(ctx.map);
            if (ctx.faction == null || ctx.enemy == null || ctx.a == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Deepwell_Captain"), ctx.faction, ext.ages);
            var envoy = Arrivals.Make(PeoplesD.KindNamed(ctx.enemy, "Rimfolk_Mossborn_Tender"), ctx.enemy, ext.ages);
            if (ctx.stranger == null || envoy == null) return false;
            ctx.group.Add(envoy);
            int social = ctx.a.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            ctx.count = Mathf.RoundToInt(PeopleEvents10Math.BargainChance(social) * 100f);
            ctx.placeName = LivingEvents.TownOf(ctx.enemy, ctx.map) ?? ctx.enemy.Name;
            ctx.note = ConsoleTalk.Table(ctx.map, ctx.a).HasThing ? "table" : "gate";
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var gate = PeoplesD.GateSpot(ctx.map);
            float hours = ext.stayHours.RandomInRange;
            return Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, gate, hours) != null
                   && Arrivals.Visit(ctx.group.Where(p => p != null && p.Faction == ctx.enemy).ToList(), ctx.map, ctx.enemy, gate, hours) != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.note == "gate" ? "gate" : "table");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 2) return null;
            if (ctx.a == null || ctx.a.Dead || ctx.a.MapHeld != ctx.map) return "CC_PEv10_TalkerGone".Translate().Resolve();
            if (!PeopleEvents10.Thanks(ctx.faction) || !PeopleEvents10.Thanks(ctx.enemy)) return "CC_PEv10_PeoplesGone".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault && option > 2) PeoplesG.Say(ctx.stranger, Ext, ctx, "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The colony's ruling, {A} hearing both at the table first: <see cref="ruling"/> "shaft", "wood" or "bargain".</summary>
    public class DilemmaEffect_WoodMoot : DilemmaEffect
    {
        public string ruling = "bargain";

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null || hc.faction == null || hc.enemy == null) return;
            bool table = hc.note != "gate";
            sheet.Play((table ? "CC_PEv10_Impact_Hearing" : "CC_PEv10_Impact_HearingGate").Translate(hc.a.LabelShort).Resolve(), "CC_PEv10_Impact_HearingBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Skill, 1, "CC_PEv10_Impact_HearSocial".Translate(hc.a.LabelShort).Resolve(), "CC_PEv10_Impact_HearSocialBrief".Translate().Resolve(), "pev10hear");
            switch (ruling)
            {
                case "shaft":
                    sheet.Goodwill(hc.faction, PeopleEvents10Math.MootGain);
                    sheet.Goodwill(hc.enemy, PeopleEvents10Math.MootLoss);
                    sheet.Add(ImpactKind.Goods, 1, "CC_PEv10_Impact_Steel".Translate(Mathf.RoundToInt((1f - PeopleEvents10Math.SteelFactor) * 100f)).Resolve(),
                        "CC_PEv10_Impact_PriceBrief".Translate().Resolve(), "pev10price");
                    break;
                case "wood":
                    sheet.Goodwill(hc.enemy, PeopleEvents10Math.MootGain);
                    sheet.Goodwill(hc.faction, PeopleEvents10Math.MootLoss);
                    sheet.Add(ImpactKind.Goods, 1, "CC_PEv10_Impact_Herbs".Translate(Mathf.RoundToInt((1f - PeopleEvents10Math.HerbFactor) * 100f)).Resolve(),
                        "CC_PEv10_Impact_PriceBrief".Translate().Resolve(), "pev10price");
                    break;
                default:
                    sheet.Add(ImpactKind.Goodwill, 0, "CC_PEv10_Impact_Bargain".Translate(hc.a.LabelShort, hc.count, PeopleEvents10Math.BargainGain, -PeopleEvents10Math.BargainLoss,
                        Mathf.RoundToInt((1f - PeopleEvents10Math.BargainSteelFactor) * 100f)).Resolve(), "CC_PEv10_Impact_BargainBrief".Translate().Resolve(), "pev10bargain");
                    break;
            }
            sheet.Add(ImpactKind.Other, 0, "CC_PEv10_Impact_RegionHears".Translate().Resolve(), "CC_PEv10_Impact_RegionHearsBrief".Translate().Resolve(), "pev10heard");
        }

        public override void Apply(DilemmaContext ctx) => WoodMoot.Begin(ctx as HappeningContext, ruling);
    }
}
