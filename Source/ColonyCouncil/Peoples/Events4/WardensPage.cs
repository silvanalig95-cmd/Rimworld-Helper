using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 31. A page in the book (the Lantern Wardens) =====================

    /// <summary>
    /// A page in the book (item 31): when a colonist kills a legend of the region, a beast nemesis or a pack's alpha, a Warden
    /// comes some hours later to write it in the book, and ties a red cord on the hunter's sleeve at the gate. The hunter
    /// keeps a lasting memory and the Wardens' word on them (a deed naming them); with Vanilla Skills Expanded the Hunter
    /// expertise, otherwise a passion in the skill that made the kill. The Wardens give +6, and ask for the pelt for their
    /// hall at twice its worth. The beasts that make legends are the player's (Megafauna, Dinosauria, ReGrowth among them).
    /// </summary>
    public static class WardensPage
    {
        public const string GiftKind = "page";
        public const string EventDef = "Rimfolk_Evt_WardensPage";

        public static JobDef CordJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv4_Cord");

        public static HediffDef Cord => DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_RedCord");

        private static ThoughtDef PageThought => DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_PageInBook");

        private static HediffDef legendDef;

        private static int Now => Find.TickManager.TicksGame;

        public static void Forget()
        {
        }

        /// <summary>What kind of great beast this is, while it still lives (before its death is told): "legend", "nemesis", "alpha", or null.</summary>
        public static string GreatKind(Pawn p)
        {
            if (p?.RaceProps?.Animal != true || p.Faction != null) return null;
            legendDef ??= DefDatabase<HediffDef>.GetNamedSilentFail("Council_Legendary");
            if (legendDef != null && p.health?.hediffSet?.HasHediff(legendDef) == true) return "legend";
            if (CouncilGameComponent.Instance?.nemeses?.IsNemesis(p) == true) return "nemesis";
            if (BeastBook.Instance?.alphas?.Any(a => a != null && a.Open && a.alpha == p) == true) return "alpha";
            return null;
        }

        /// <summary>A great beast died (HappeningTracker.Notify_Killed): brought down by one of the colony, the page waits for its Warden.</summary>
        public static void Killed(Pawn victim, DamageInfo? dinfo, string kind, Map map)
        {
            var b = PeopleEvents4Book.Instance;
            if (b == null || kind == null || map == null || !Homes.IsHome(map)) return;
            if (!(dinfo?.Instigator is Pawn killer) || killer.Faction != Faction.OfPlayer || killer.RaceProps?.Humanlike != true || killer.Dead) return;
            float hide = 0f;
            try
            {
                hide = victim.GetStatValue(StatDefOf.LeatherAmount);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: a beast's hide", e);
            }
            b.AddPage(new PageRecord
            {
                killer = killer, map = map, beast = victim.LabelShort, kind = kind, leather = victim.RaceProps.leatherDef?.defName, hide = hide,
                ranged = dinfo?.Weapon != null && dinfo.Value.Weapon.IsRangedWeapon, tick = Now
            });
        }

        /// <summary>The page waiting for its Warden here: the kill some hours past and not too old, the hunter at home and up.</summary>
        public static PageRecord Ready(Map map)
        {
            var b = PeopleEvents4Book.Instance;
            int now = Now;
            return b?.pages.FirstOrDefault(r => r != null && !r.done && r.map == map && now - r.tick >= PeopleEvents4Math.PageMinHours * GenDate.TicksPerHour
                                               && PeoplesG.Up(r.killer, map) && r.killer.Faction == Faction.OfPlayer);
        }

        /// <summary>The homes' hour: a page waiting, the Wardens at peace, a Warden comes at a small chance (the storytellers' lean on visitors).</summary>
        public static void Watch(Map map, int now)
        {
            var b = PeopleEvents4Book.Instance;
            if (b == null) return;
            foreach (var r in b.pages.Where(r => r != null && !r.done && r.map == map && now - r.tick > PeopleEvents4Math.PageExpireDays * GenDate.TicksPerDay)) r.done = true;
            var page = Ready(map);
            if (page == null || MapFacts.ThreatOn(map) || !Tell.CanAsk) return;
            var f = PeoplesD.Friendly(PeoplesMath.Hunters);
            if (f == null || !Rand.Chance(PeopleEvents4Math.PageChance * StoryDirection.Factor(StoryWaysMath.Visitors))) return;
            if (PeopleEvents2.Fire(EventDef, map, f)) b.lastPage = now;
        }

        /// <summary>Dev mode: the page here waits no longer.</summary>
        public static bool Soon(Map map)
        {
            var r = PeopleEvents4Book.Instance?.pages.FirstOrDefault(x => x != null && !x.done && x.map == map);
            if (r == null) return false;
            r.tick = Math.Min(r.tick, Now - PeopleEvents4Math.PageMinHours * GenDate.TicksPerHour);
            return true;
        }

        /// <summary>The cord tied (the Warden's work at the gate done): the cord, the memory, the Wardens' word, the expertise or passion, their thanks.</summary>
        public static void Tied(Pawn warden, Pawn hunter, PageRecord r)
        {
            if (hunter == null || hunter.Dead) return;
            // together: the keeper of the book has just tied the cord at the hunter's side at the gate (PeopleWorkOn brought them there).
            var map = hunter.MapHeld;
            var f = warden?.Faction;
            try
            {
                var cord = Cord;
                if (cord != null && !hunter.health.hediffSet.HasHediff(cord))
                {
                    var arm = hunter.health.hediffSet.GetNotMissingParts().FirstOrDefault(p => p.def.defName == "Arm" || p.def.defName == "Shoulder");
                    hunter.health.AddHediff(HediffMaker.MakeHediff(cord, hunter, arm), arm);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: the red cord", e);
            }
            PeopleEventsBook.Gave(GiftKind, hunter);
            if (PageThought != null)
            {
                using var _cause = Causes.Push("CC_PEv4_Cause_Page".Translate(r?.beast ?? "?").Resolve());
                CouncilGameComponent.GiveThought(hunter, PageThought, warden);
            }
            string skillName = PeopleEvents4Math.KillSkill(r?.ranged ?? true);
            string what;
            if (Expertise.Give(hunter, "Hunter")) what = "CC_PEv4_PageExpert".Translate(hunter.Named("PAWN")).Resolve().CapitalizeFirst();
            else
            {
                var sk = hunter.skills?.GetSkill(DefDatabase<SkillDef>.GetNamedSilentFail(skillName));
                if (sk != null && !sk.TotallyDisabled && sk.passion != Passion.Major)
                {
                    sk.passion = sk.passion == Passion.None ? Passion.Minor : Passion.Major;
                    what = "CC_PEv4_PagePassion".Translate(hunter.Named("PAWN"), sk.def.label.Named("SKILL")).Resolve();
                }
                else what = "";
            }
            if (f != null)
            {
                KinMemory.Note(f, "favorDone", hunter, r?.beast);
                if (f.HasGoodwill && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer)) WorldSituationWorker.Goodwill(f, PeopleEvents4Math.PageGoodwill, GoodwillWhy.Helped);
            }
            if (warden != null) PeoplesG.SayLine(warden, DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef), "cord");
            Tell.Message(NewsKind.People, Importance.Notable, map,
                "CC_PEv4_PageTied".Translate(hunter.Named("PAWN"), (r?.beast ?? "?").Named("BEAST"), (f?.Name ?? "?").Named("FACTION")).Resolve() + (what.NullOrEmpty() ? "" : " " + what),
                hunter.Spawned ? new LookTargets(hunter) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, "CC_PEv4_PageChronicle".Translate(hunter.Named("PAWN"), (r?.beast ?? "?").Named("BEAST"), (f?.Name ?? "?").Named("FACTION")).Resolve(), hunter));
        }
    }

    /// <summary>
    /// A Warden ({X}) at the gate with the book: {A} brought down {BEAST} ({TALE}), and the Wardens would write it in and tie a
    /// red cord on {A_possessive} sleeve; the pelt ({COUNT} {THING}) for their hall, at twice its worth ({DEAD} silver).
    /// </summary>
    public class HappeningWorker_WardensPage : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Hunters) != null && WardensPage.Ready(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Hunters);
            var r = WardensPage.Ready(ctx.map);
            if (ctx.faction == null || r == null) return false;
            ctx.a = r.killer;
            ctx.beast = r.beast;
            ctx.note = r.id.ToString();
            ctx.tale = ("CC_PEv4_Page_" + r.kind).Translate().Resolve();
            ctx.thing = r.leather != null ? DefDatabase<ThingDef>.GetNamedSilentFail(r.leather) : null;
            int have = ctx.thing != null ? VarietyUtil.InStores(ctx.map, ctx.thing) : 0;
            ctx.count = PeopleEvents4Math.PeltCount(r.hide, have);
            ctx.variant = ctx.count > 0 ? PeopleEvents4Math.PeltPrice(ctx.count, ctx.thing.BaseMarketValue) : 0;
            ctx.dead = ctx.variant.ToString();
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Wardens_Master"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        /// <summary>The Warden walks in to the gate; the page is theirs to write now (no other Warden comes for it).</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            if (Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) == null) return false;
            var r = PeopleEvents4Book.Instance?.pages.FirstOrDefault(x => x != null && x.id.ToString() == ctx.note);
            if (r != null) r.done = true;
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            var r = PeopleEvents4Book.Instance?.pages.FirstOrDefault(x => x != null && x.id.ToString() == ctx.note);
            if (r?.kind != null) tags.Add(r.kind);
            tags.Add(ctx.count > 0 ? "pelt" : "nopelt");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.a == null || ctx.a.Dead || !ctx.a.Spawned || ctx.a.Map != ctx.map) return "CC_PEv4_HunterGone".Translate().Resolve();
            if (option == 0 && (ctx.thing == null || ctx.count <= 0)) return "CC_PEv4_NoPelt".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // The pelt paid for by the Warden at the gate, carried out by the answer's errand.
            if (option == 0 && !byDefault && ctx.variant > 0) Couriers.PaySilver(ctx.map, VarietyUtil.Near(ctx.stranger, ctx.map), ctx.variant, ctx.faction, ctx.stranger);
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "write" : "noted");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The page written and the cord tied at the gate: the hunter walked there and the Warden at work on the sleeve (PeopleWorkOn, Rimfolk_PEv4_Cord).</summary>
    public class DilemmaEffect_WardensPage : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null) return;
            if (PlayOuts.On) sheet.Play("CC_PEv4_Impact_Cord".Translate(hc.a.LabelShort, hc.stranger?.LabelShort ?? "?").Resolve(), "CC_PEv4_Impact_CordBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Skill, 1, (Expertise.CanGive(hc.a, "Hunter") ? "CC_PEv4_Impact_PageExpert" : "CC_PEv4_Impact_PagePassion").Translate(hc.a.LabelShort).Resolve(),
                "CC_PEv4_Impact_PageBrief".Translate().Resolve(), "pev4page");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents4Math.PageGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null) return;
            var job = WardensPage.CordJob;
            var gate = PeoplesD.GateSpot(hc.map);
            LocalTargetInfo at = gate.HasValue ? new LocalTargetInfo(gate.Value) : LocalTargetInfo.Invalid;
            if (hc.stranger == null || job == null || !PeopleWorkOn.Begin(hc, hc.stranger, hc.a, job, at, hc.note, 0, PeopleEvents4Math.CordTicks, 6f))
                WardensPage.Tied(hc.stranger, hc.a, PeopleEvents4Book.Instance?.pages.FirstOrDefault(x => x != null && x.id.ToString() == hc.note));
        }
    }

    /// <summary>The cord tied at the gate: the page's gifts (WardensPage.Tied).</summary>
    public class WardensCordDone : PeopleWorkDone
    {
        public override void Done(Visit v, Pawn worker, Pawn on) =>
            WardensPage.Tied(worker, on, PeopleEvents4Book.Instance?.pages.FirstOrDefault(x => x != null && x.id.ToString() == PeopleWorkOn.Note(v)));

        public override void GaveUp(Visit v)
        {
            if (v?.map == null) return;
            Tell.Message(NewsKind.People, Importance.Notable, v.map, "CC_PEv_WorkGivenUp".Translate((v.x?.LabelShort ?? "?").Named("WORKER"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }
    }
}
