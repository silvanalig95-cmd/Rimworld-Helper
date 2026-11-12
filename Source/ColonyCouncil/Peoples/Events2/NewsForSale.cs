using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    // ===================== 63. News for sale (the Signal Line) =====================

    /// <summary>
    /// News for sale (item 63): an operator of the Signal Line sells what the masts heard, 60 to 200 silver, of a site within
    /// reach: an item stash, a wreck's cargo (Odyssey's, where the game would send the colony to it), a bandit outpost. Bought,
    /// the game's own quest makes the site (QuestUtility), and the places-heard-of marks show it with who told the colony,
    /// where they heard it and when (World/HeardPlaces.cs). The same in every age: their runners walk the word in where the
    /// colony has no radio.
    /// </summary>
    public static class NewsForSale
    {
        public static QuestScriptDef Quest(string kind) => DefDatabase<QuestScriptDef>.GetNamedSilentFail(PeopleEvents2Math.NewsQuest(kind) ?? "");

        /// <summary>The game would make this kind of site now.</summary>
        public static bool Can(string kind, Map map)
        {
            var q = Quest(kind);
            if (q == null) return false;
            try
            {
                return q.CanRun(StorytellerUtility.DefaultSiteThreatPointsNow(), (IIncidentTarget)map ?? Find.World);
            }
            catch
            {
                return false;
            }
        }

        public static List<string> Kinds(Map map) => PeopleEvents2Math.NewsKinds.Where(k => Can(k, map)).ToList();

        /// <summary>
        /// The word bought: the game's quest for it made and offered, the site it made marked as heard of from the Line at
        /// <paramref name="town"/>, through <paramref name="through"/>. Null when the game couldn't make it after all.
        /// </summary>
        public static Site Make(string kind, Map map, string teller, string through)
        {
            var q = Quest(kind);
            if (q == null || !Can(kind, map)) return null;
            try
            {
                var before = new HashSet<int>(Find.WorldObjects.Sites.Select(s => s.ID));
                var quest = QuestUtility.GenerateQuestAndMakeAvailable(q, StorytellerUtility.DefaultSiteThreatPointsNow());
                if (quest == null) return null;
                QuestUtility.SendLetterQuestAvailable(quest);
                var site = Find.WorldObjects.Sites.FirstOrDefault(s => !before.Contains(s.ID));
                if (site != null)
                    HeardPlaces.Heard(new[] { "site:" + site.ID }, ("CC_PEv2_NewsWhat_" + kind).Translate().Resolve(), teller, through);
                return site;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the news bought", e);
                return null;
            }
        }
    }

    /// <summary>
    /// An operator of {FACTION} at the gate with what the masts at {PLACE} heard: {TALE} for {COUNT} silver, or {RELATION}
    /// for {BEAST} silver (the second price; the event has no beast of its own to name).
    /// </summary>
    public class HappeningWorker_NewsForSale : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Linesmen) != null && HeardPlaces.On && NewsForSale.Kinds(map).Count > 0 && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Linesmen);
            var kinds = NewsForSale.Kinds(ctx.map).InRandomOrder().Take(2).ToList();
            if (ctx.faction == null || kinds.Count == 0) return false;
            string a = kinds[0], b = kinds.Count > 1 ? kinds[1] : null;
            ctx.count = PeopleEvents2Math.NewsPrice(a, Rand.Value);
            ctx.variant = b == null ? 0 : PeopleEvents2Math.NewsPrice(b, Rand.Value);
            ctx.note = a + "|" + (b ?? "");
            ctx.tale = ("CC_PEv2_News_" + a).Translate().Resolve();
            ctx.relation = b == null ? "" : ("CC_PEv2_News_" + b).Translate().Resolve();
            ctx.beast = ctx.variant.ToString();
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_SignalLine_Operator"), ctx.faction, ext.ages);
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Social) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.stranger != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ctx.map != null && !RepublicRadio.HasRadio(ctx.map)) tags.Add("noradio");
            foreach (var k in (ctx.note ?? "").Split('|').Where(k => !k.NullOrEmpty())) tags.Add(k);
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            var kinds = (ctx.note ?? "").Split('|');
            string kind = option < kinds.Length ? kinds[option] : null;
            if (kind.NullOrEmpty()) return "CC_PEv2_NoMoreNews".Translate().Resolve();
            if (!NewsForSale.Can(kind, ctx.map)) return "CC_PEv2_NewsStale".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "sold" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The word bought (the first piece of news, or the <see cref="second"/>): the silver carried out to the operator, the game's site made and marked as heard of.</summary>
    public class DilemmaEffect_NewsBought : DilemmaEffect
    {
        public bool second;

        public override bool ChanceEach => false;

        private int Price(HappeningContext hc) => second ? hc.variant : hc.count;

        private string Kind(HappeningContext hc)
        {
            var kinds = (hc?.note ?? "").Split('|');
            int i = second ? 1 : 0;
            return i < kinds.Length ? kinds[i] : null;
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return null;
            int price = Price(hc);
            return price > 0 && CampAnswers.Silver(ctx.map) < price ? "CC_Dilemma_NotEnough".Translate(price, ThingDefOf.Silver.label).ToString() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            string kind = Kind(hc);
            if (hc == null || kind.NullOrEmpty()) return;
            sheet.Silver(-Price(hc));
            sheet.Add(ImpactKind.Other, 1, "CC_PEv2_Impact_News".Translate(("CC_PEv2_News_" + kind).Translate().Resolve(), hc.placeName ?? "?").Resolve(), "CC_PEv2_Impact_NewsBrief".Translate().Resolve(), "pev2news");
            sheet.Play("CC_PEv2_Impact_NewsPaid".Translate(hc.stranger?.LabelShort ?? "?").Resolve(), "CC_PEv2_Impact_NewsPaidBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            string kind = Kind(hc);
            if (hc?.map == null || kind.NullOrEmpty()) return;
            string teller = "CC_PEv2_NewsTeller".Translate((hc.stranger?.LabelShort ?? "?").Named("X"), (hc.placeName ?? "?").Named("PLACE")).Resolve();
            var site = NewsForSale.Make(kind, hc.map, teller, hc.a?.LabelShort ?? "?");
            if (site == null)
            {
                Tell.Message(NewsKind.World, Importance.Notable, hc.map, "CC_PEv2_NewsGone".Translate((hc.stranger?.LabelShort ?? "?").Named("X")).Resolve(), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                return;
            }
            // apart: a colonist carries the silver out to the operator at the gate, who takes it.
            new DilemmaEffect_CarryOut { what = "silver", count = Price(hc), steward = false, who = PlayWho.None, label = "CC_PEv2_NewsSilverLabel".Translate((hc.stranger?.LabelShort ?? "?").Named("X")).Resolve() }.Apply(ctx);
            if (hc.faction != null && hc.faction.HasGoodwill && !hc.faction.IsPlayer) WorldSituationWorker.Goodwill(hc.faction, PeopleEvents2Math.NewsGoodwill, false);
            FactionMemory.Note(hc.faction, "traded", ("CC_PEv2_News_" + kind).Translate().Resolve());
        }
    }
}
