using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 47. Storm-sense (the Stormglass Conclave) =====================

    /// <summary>
    /// What a reader of the Conclave wakes in a colonist who can hear the storm: with Royalty a first psylink (with Vanilla
    /// Psycasts Expanded on its Staticlord or Frostshaper path); without Royalty, with A RimWorld of Magic, the Lightning
    /// tome. Without either it never comes.
    /// </summary>
    public static class StormSense
    {
        public const string GiftKind = "storm";

        /// <summary>A thunderstorm over the map now (a weather with lightning in it).</summary>
        public static bool Thunder(Map map)
        {
            var w = map?.weatherManager?.curWeather;
            return w?.eventMakers != null && w.eventMakers.Any(e => e?.eventClass != null && e.eventClass.Name.IndexOf("Lightning", StringComparison.Ordinal) >= 0);
        }

        /// <summary>"psylink" or "tome": what the storm would give this colonist; null for nothing.</summary>
        public static string Gift(Pawn p)
        {
            if (p == null) return null;
            if (ModsConfig.RoyaltyActive) return p.GetMainPsylinkSource() == null && p.health?.hediffSet?.GetBrain() != null ? "psylink" : null;
            return DefDatabase<ThingDef>.GetNamedSilentFail(PeopleEventsMath.StormTome) != null ? "tome" : null;
        }

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

        /// <summary>The colony's most psychically sensitive grown colonist who could hear the storm and has no psylink yet.</summary>
        public static Pawn Listener(Map map) =>
            PeopleEvents.Takers(map, GiftKind).Where(p => Gift(p) != null && PeopleEventsMath.Hears(Sensitivity(p))).OrderByDescending(Sensitivity).FirstOrDefault();

        /// <summary>The colony's own stormglass rod (Peoples/PeoplesE.cs, the Conclave's watch), standing.</summary>
        public static Building Rod(Map map) => Avail.ColonyBuildings(map, d => d?.defName == "Rimfolk_StormglassRod").FirstOrDefault();

        /// <summary>The storm's gift given: a flash over the rod, the psylink woken (and its path), or the tome handed over.</summary>
        public static void Woke(Pawn reader, Pawn on, Visit v)
        {
            var map = on?.MapHeld ?? v?.map;
            if (on == null || on.Dead || map == null) return;
            try
            {
                map.weatherManager?.eventHandler?.AddEvent(new WeatherEvent_LightningFlash(map));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the storm's flash", e);
            }
            string gift = Gift(on);
            string what = null;
            try
            {
                if (gift == "psylink")
                {
                    on.ChangePsylinkLevel(1, false);
                    PsycastPaths.Follow(on, PeopleEventsMath.StormPaths.ToList(), 1);
                    what = "CC_PEv_StormPsylink".Translate().Resolve();
                }
                else if (gift == "tome")
                {
                    var tome = DefDatabase<ThingDef>.GetNamedSilentFail(PeopleEventsMath.StormTome);
                    Couriers.Hand(tome, 1, map, on.Spawned ? on.Position : IntVec3.Invalid, reader?.Faction, reader);
                    what = "CC_PEv_StormTome".Translate(tome.label.Named("TOME")).Resolve();
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: the storm's gift", e);
            }
            if (what == null) return;
            PeopleEventsBook.Gave(GiftKind, on);
            if (on.Spawned) FleckMaker.ThrowMetaIcon(on.Position, on.Map, FleckDefOf.PsycastAreaEffect);
            PeoplesG.SayLine(reader, v?.incident, "woke");
            Tell.Message(NewsKind.People, Importance.Notable, map, "CC_PEv_StormWoke".Translate(on.Named("PAWN"), (reader?.LabelShort ?? "?").Named("READER"), what.Named("WHAT")).Resolve(),
                on.Spawned ? new LookTargets(on) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, "CC_PEv_StormChronicle".Translate(on.Named("PAWN"), what.Named("WHAT")).Resolve(), on));
        }
    }

    /// <summary>
    /// In a thunderstorm, a reader of the Stormglass Conclave asks the colony's most psychically sensitive colonist ({B}) to
    /// stand out with her an hour at the rod (the colony's own stormglass rod, else the open sky): {B} comes back with
    /// {TALE}. The price is {COUNT} silver, or, with a rod of the colony's own, a vial of the colony's own storm for their archive.
    /// </summary>
    public class HappeningWorker_StormSense : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Seers) != null && StormSense.Thunder(map) && StormSense.Listener(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Seers);
            ctx.b = StormSense.Listener(ctx.map);
            if (ctx.faction == null || ctx.b == null) return false;
            string gift = StormSense.Gift(ctx.b);
            ctx.note = gift;
            ctx.tale = gift == "psylink" ? "CC_PEv_StormPsylink".Translate().Resolve()
                : "CC_PEv_StormTome".Translate((DefDatabase<ThingDef>.GetNamedSilentFail(PeopleEventsMath.StormTome)?.label ?? "?").Named("TOME")).Resolve();
            ctx.count = PeopleEventsMath.StormPrice(LivingEvents.WealthFactor(ctx.map));
            ctx.place = StormSense.Rod(ctx.map);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Stormglass_Reader"), ctx.faction, ext.ages);
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
            if (ctx.place != null) tags.Add("rod");
            tags.Add(ctx.note == "tome" ? "tome" : "psylink");
            if (ModsConfig.IsActive("vanillaexpanded.vpsycastse")) tags.Add("paths");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.b == null || ctx.b.Dead || !ctx.b.Spawned || ctx.b.Map != ctx.map) return "CC_PEv_ListenerGone".Translate().Resolve();
            if (StormSense.Gift(ctx.b) != ctx.note) return "CC_PEv_StormHeard".Translate().Resolve();
            if (!StormSense.Thunder(ctx.map)) return "CC_PEv_StormPassed".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "storm" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>
    /// {B} and the reader out under the storm an hour (PeopleWorkOn, Rimfolk_PEv_Storm: by the colony's rod, else in the
    /// open). <see cref="vial"/>: paid with a vial of the colony's own storm, which needs a rod of the colony's.
    /// </summary>
    public class DilemmaEffect_StormSense : DilemmaEffect
    {
        public bool vial;

        public override bool ChanceEach => false;

        public static JobDef Job => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv_Storm");

        public override string Unavailable(DilemmaContext ctx) => vial && StormSense.Rod(ctx?.map) == null ? "CC_PEv_NoRod".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.b == null) return;
            sheet.Add(ImpactKind.Skill, 1, "CC_PEv_Impact_Storm".Translate(hc.b.LabelShort, hc.tale ?? "?").Resolve(), "CC_PEv_Impact_StormBrief".Translate().Resolve(), "pevstorm");
            if (PlayOuts.On)
                sheet.Play((StormSense.Rod(hc.map) != null ? "CC_PEv_Impact_StormRod" : "CC_PEv_Impact_StormOpen").Translate(hc.b.LabelShort, hc.stranger?.LabelShort ?? "?").Resolve(),
                    "CC_PEv_Impact_StormRodBrief".Translate().Resolve());
            if (vial) sheet.Add(ImpactKind.Other, 0, "CC_PEv_Impact_Vial".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv_Impact_VialBrief".Translate().Resolve(), "pevvial");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var x = hc?.stranger;
            if (hc?.map == null || x == null || hc.b == null || Job == null) return;
            if (vial && hc.faction != null) FactionMemory.Note(hc.faction, "gift", "CC_PEv_VialName".Translate().Resolve());
            PeopleWorkOn.Begin(hc, x, hc.b, Job, StormSense.Rod(hc.map) is Building rod ? new LocalTargetInfo(rod) : LocalTargetInfo.Invalid, hc.note, vial ? 0 : hc.count, PeopleEventsMath.StormTicks, PeopleEventsMath.StormHours + 3f);
        }
    }

    /// <summary>The hour in the storm done: the flash, and the psylink woken or the tome handed over (StormSense.Woke).</summary>
    public class StormSenseDone : PeopleWorkDone
    {
        public override void Done(Visit v, Pawn worker, Pawn on) => StormSense.Woke(worker, on, v);

        public override void GaveUp(Visit v)
        {
            if (v?.map == null) return;
            Tell.Message(NewsKind.People, Importance.Notable, v.map, "CC_PEv_WorkGivenUp".Translate((v.x?.LabelShort ?? "?").Named("WORKER"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }
    }
}
