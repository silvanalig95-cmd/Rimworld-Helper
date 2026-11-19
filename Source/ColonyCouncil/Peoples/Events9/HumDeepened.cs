using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 87. The hum deepened (the Choir of the Spire) =====================

    /// <summary>
    /// The hum deepened (item 87, a gift): a cantor of the Choir comes in the evening and sits through the night at the bed of a colonist
    /// with a psylink, humming while they lie listening (five hours, the meditation's mark: round 1's work on a colonist), and by morning
    /// the psylink is a level higher (with Vanilla Psycasts Expanded, the level's point for one more power). A colonist without one but
    /// psychically sensitive (the trait or more) wakes a first level instead, on the Choir's own paths. The price is a piece of the
    /// colony's art (its dearest, carried out to the cantor), or an evening in which the cantor listens to the colony: once the night is
    /// over everyone at home feels it by the listening's own rule (ChoirListening.Heard: a calm for most, an unease for some). Royalty only.
    /// </summary>
    public static class HumDeepened
    {
        public const string GiftKind = "hum";

        public static readonly string[] Paths = { "VPE_Harmonist", "VPE_Empath" };

        public static JobDef Job => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv9_Hum");

        private static float Sensitivity(Pawn p)
        {
            try
            {
                return p.GetStatValue(StatDefOf.PsychicSensitivity);
            }
            catch
            {
                return 0f;
            }
        }

        public static int Level(Pawn p) => ModsConfig.RoyaltyActive && p != null && p.HasPsylink ? p.GetPsylinkLevel() : 0;

        /// <summary>One who can take the hum: a psylink below six, or none and sensitive enough, a brain, a bed.</summary>
        public static bool CanTake(Pawn p) =>
            ModsConfig.RoyaltyActive && p?.health?.hediffSet?.GetBrain() != null && PeopleEvents9Math.HumTaker(Level(p), Sensitivity(p)) && OrderLimb.Bed(p) != null;

        /// <summary>The one the cantor hears: a psycaster first (the highest below six), else the most sensitive.</summary>
        public static Pawn Pick(Map map) =>
            PeopleEvents.Takers(map, GiftKind).Where(CanTake).OrderByDescending(Level).ThenByDescending(Sensitivity).FirstOrDefault();

        /// <summary>The colony's dearest piece of art, worth enough to offer.</summary>
        public static Thing Art(Map map)
        {
            var art = ChoirGoods.Art(map).FirstOrDefault();
            return art != null && art.MarketValue * art.stackCount >= PeopleEvents9Math.HumArtMinWorth ? art : null;
        }

        /// <summary>The night over: the psylink a level higher (or woken), the mark, told; with the evening's listening, the colony feels it.</summary>
        public static void Deepened(Pawn cantor, Pawn on, Visit v)
        {
            var map = on?.MapHeld ?? v?.map;
            var f = cantor?.Faction ?? PeopleEvents9.PeopleOf(v);
            if (on == null || on.Dead || map == null || !ModsConfig.RoyaltyActive) return;
            int before = Level(on);
            if (before >= 6) return;
            try
            {
                on.ChangePsylinkLevel(1, false);
                if (before == 0) PsycastPaths.Follow(on, Paths.ToList(), 1);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: the hum deepened", e);
                return;
            }
            PeopleEventsBook.Gave(GiftKind, on);
            if (on.Spawned) FleckMaker.ThrowMetaIcon(on.Position, on.Map, FleckDefOf.PsycastAreaEffect);
            PeoplesG.SayLine(cantor, v?.incident, "morning");
            Tell.Message(NewsKind.People, Importance.Notable, map,
                (before == 0 ? "CC_PEv9_HumWoke" : "CC_PEv9_HumDeeper").Translate(on.Named("PAWN"), (cantor?.LabelShort ?? "?").Named("CANTOR"), (before + 1).Named("LEVEL")).Resolve(),
                on.Spawned ? new LookTargets(on) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, "CC_PEv9_HumChronicle".Translate(on.Named("PAWN"), (f?.Name ?? "?").Named("FACTION")).Resolve(), on));
            PeopleEvents9.Goodwill(f, PeopleEvents9Math.HumGoodwill, GoodwillWhy.TheirWork);
            // The evening's listening paid for it: everyone at home feels it now, by the listening's rule.
            if (PeopleWorkOn.Note(v) == "listened") ChoirListening.Heard(map, f, on, new List<Pawn>(), v?.incident);
        }
    }

    /// <summary>
    /// A cantor of {FACTION} ({X}) at the gate at evening: {X} would sit through the night at {B}'s bed, humming, and by morning {B}'s psylink
    /// would be a level higher ({TALE}). For a piece of the colony's art ({THING}), or an evening of being listened to. Royalty only.
    /// </summary>
    public class HappeningWorker_HumDeepened : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            ModsConfig.RoyaltyActive && PeoplesD.Friendly(PeoplesMath.Listeners) != null && PeopleEvents9Math.HumHour(GenLocalDate.HourOfDay(map)) && HumDeepened.Pick(map) != null
            && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Listeners);
            ctx.b = HumDeepened.Pick(ctx.map);
            if (ctx.faction == null || ctx.b == null) return false;
            int level = HumDeepened.Level(ctx.b);
            ctx.variant = level;
            ctx.tale = (level == 0 ? "CC_PEv9_HumFirst" : "CC_PEv9_HumLevel").Translate((level + 1).Named("LEVEL")).Resolve();
            ctx.place = OrderLimb.Bed(ctx.b);
            var art = HumDeepened.Art(ctx.map);
            ctx.thing = art?.def;
            ctx.relation = art != null ? ((art as MinifiedThing)?.InnerThing ?? art).LabelNoCount : null;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Choir_Cantor"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = ctx.b;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.variant > 0 ? "psylink" : "first");
            if (ctx.relation != null) tags.Add("art");
            if (ModsConfig.IsActive("vanillaexpanded.vpsycastse")) tags.Add("paths");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.b == null || ctx.b.Dead || !ctx.b.Spawned || ctx.b.Map != ctx.map) return "CC_PEv9_HumGone".Translate().Resolve();
            if (!HumDeepened.CanTake(ctx.b) || HumDeepened.Level(ctx.b) != ctx.variant) return "CC_PEv9_HumChanged".Translate().Resolve();
            if (option == 0 && HumDeepened.Art(ctx.map) == null) return "CC_PEv9_NoArt".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "night" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>
    /// The night at {B}'s bed (PeopleWorkOn, Rimfolk_PEv9_Hum): with <see cref="art"/> the colony's dearest piece of art carried out to the
    /// cantor first; else the evening's listening, felt by the colony once the night is over.
    /// </summary>
    public class DilemmaEffect_HumDeepened : DilemmaEffect
    {
        public bool art;

        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) => art && HumDeepened.Art(ctx?.map) == null ? "CC_PEv9_NoArt".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.b == null) return;
            if (PlayOuts.On) sheet.Play("CC_PEv9_Impact_HumNight".Translate(hc.stranger?.LabelShort ?? "?", hc.b.LabelShort).Resolve(), "CC_PEv9_Impact_HumNightBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Skill, 1, "CC_PEv9_Impact_Hum".Translate(hc.b.LabelShort, hc.tale ?? "?").Resolve(), "CC_PEv9_Impact_HumBrief".Translate().Resolve(), "pev9hum");
            if (art) sheet.Goods(-1, hc.relation ?? PlayCarry.Label(PlayCarry.Art, null), 1, PlayCarry.Art);
            else
            {
                float chance = PeoplesGMath.UneaseChance(1f, ChoirListening.VoidStirs());
                sheet.Add(ImpactKind.Mood, 0, "CC_PEv9_Impact_Listened".Translate(Mathf.RoundToInt(chance * 100f).Named("CHANCE")).Resolve(), "CC_PEv9_Impact_ListenedBrief".Translate().Resolve(),
                    "pev9listened");
            }
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents9Math.HumGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var x = hc?.stranger;
            var map = hc?.map;
            if (map == null || x == null || hc.b == null || HumDeepened.Job == null) return;
            if (art && !Couriers.TakeFor(ctx, PlayCarry.Art, null, 1)) PlayCarry.Take(map, PlayCarry.Art, null, 1);
            var bed = OrderLimb.Bed(hc.b) ?? hc.place as Building_Bed;
            PeopleWorkOn.Begin(hc, x, hc.b, HumDeepened.Job, bed != null ? new LocalTargetInfo(bed) : LocalTargetInfo.Invalid, art ? "art" : "listened", 0, PeopleEvents9Math.HumTicks, 12f);
        }
    }

    /// <summary>The night at the bed done: the hum deepened (HumDeepened.Deepened).</summary>
    public class HumDeepenedDone : PeopleWorkDone
    {
        public override void Done(Visit v, Pawn worker, Pawn on) => HumDeepened.Deepened(worker, on, v);

        public override void GaveUp(Visit v)
        {
            if (v?.map == null) return;
            Tell.Message(NewsKind.People, Importance.Notable, v.map, "CC_PEv_WorkGivenUp".Translate((v.x?.LabelShort ?? "?").Named("WORKER"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }
    }
}
