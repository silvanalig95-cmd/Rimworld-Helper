using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 22. The body made better, on terms (the spacer enclave) =====================

    /// <summary>
    /// The enclave's bionic (item 22): the factor offers one of the colony's people a bionic part, the game's eye, arm, leg or
    /// spine, for one a part is missing or scarred first, else for its best fighter; with Alpha Implants, for the bonded beast of
    /// one of them, one of its animal bionics. Paid now, or on terms: a fifth down, then four seasonal instalments asked for by
    /// letter (EnclaveTerms). Their surgeon drops in by pod (walks in to a tribal colony) and fits it at the colony's medical
    /// bed by the game's own surgery, which rarely fails at their skill.
    /// </summary>
    public static class EnclaveBody
    {
        public const string GiftKind = "bionic";

        private static bool Below(BodyPartRecord part, BodyPartRecord top)
        {
            for (var p = part; p != null; p = p.parent)
                if (p == top) return true;
            return false;
        }

        /// <summary>The recipe for a body part of this pawn (a person's bionic, else with Alpha Implants a beast's).</summary>
        private static RecipeDef RecipeFor(Pawn p, string partDef)
        {
            string name = p.RaceProps.Humanlike ? PeopleEvents2Math.BionicRecipe(partDef) : PeopleEvents2Math.BeastBionicRecipe(partDef);
            var r = name == null ? null : DefDatabase<RecipeDef>.GetNamedSilentFail(name);
            return r?.Worker != null && r.addsHediff != null ? r : null;
        }

        private static bool Fits(Pawn p, RecipeDef r, BodyPartRecord on)
        {
            if (r == null || on == null || p.health.hediffSet.HasDirectlyAddedPartFor(on)) return false;
            try
            {
                return r.Worker.GetPartsToApplyOn(p, r).Contains(on);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// The part this pawn needs: a missing eye, arm or leg (or a spine) whose bionic goes on what is left, else one scarred
        /// for good; <paramref name="better"/> also takes a sound arm or leg made better. False when none fits.
        /// </summary>
        public static bool Need(Pawn p, bool better, out RecipeDef recipe, out BodyPartRecord part)
        {
            recipe = null;
            part = null;
            if (p?.health?.hediffSet == null || p.Dead) return false;
            try
            {
                foreach (var missing in p.health.hediffSet.GetMissingPartsCommonAncestors())
                {
                    var gone = missing?.Part;
                    var r = RecipeFor(p, gone?.def?.defName);
                    var on = r == null ? null : r.Worker.GetPartsToApplyOn(p, r).FirstOrDefault(x => x != null && Below(gone, x) && !p.health.hediffSet.HasDirectlyAddedPartFor(x));
                    if (on == null) continue;
                    recipe = r;
                    part = on;
                    return true;
                }
                foreach (var h in p.health.hediffSet.hediffs)
                {
                    if (!(h is Hediff_Injury inj) || !inj.IsPermanent() || inj.Part == null) continue;
                    var r = RecipeFor(p, inj.Part.def.defName);
                    if (r != null && Fits(p, r, inj.Part))
                    {
                        recipe = r;
                        part = inj.Part;
                        return true;
                    }
                }
                if (!better || !p.RaceProps.Humanlike) return false;
                foreach (var pd in new[] { "Shoulder", "Leg" })
                {
                    var r = RecipeFor(p, pd);
                    var on = r == null ? null : r.Worker.GetPartsToApplyOn(p, r).FirstOrDefault(x => x != null && !p.health.hediffSet.HasDirectlyAddedPartFor(x) && !p.health.hediffSet.PartIsMissing(x));
                    if (on == null) continue;
                    recipe = r;
                    part = on;
                    return true;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the enclave's part", e);
            }
            return false;
        }

        /// <summary>What the part is worth: the thing it is (its hediff's own), else a thousand.</summary>
        public static float Worth(RecipeDef r) => r?.addsHediff?.spawnThingOnRemoved?.BaseMarketValue ?? 1000f;

        /// <summary>The colony's people (or, with Alpha Implants, their bonded beasts) with a part to make better; those in need first.</summary>
        public static IEnumerable<Pawn> Takers(Map map, bool better)
        {
            var people = PeopleEvents.Takers(map, GiftKind).ToList();
            foreach (var p in people)
                if (Need(p, false, out _, out _))
                    yield return p;
            if (DefDatabase<RecipeDef>.GetNamedSilentFail("AI_AnimalBionicLeg") != null)
                foreach (var a in map.mapPawns.SpawnedColonyAnimals)
                    if (a != null && !a.Dead && !a.Downed && !PeopleEventsBook.Given(GiftKind, a) && people.Any(p => a.relations?.DirectRelationExists(PawnRelationDefOf.Bond, p) == true)
                        && Need(a, false, out _, out _))
                        yield return a;
            if (!better) yield break;
            foreach (var p in people.Where(x => !x.WorkTagIsDisabled(WorkTags.Violent))
                         .OrderByDescending(x => (x.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0) + (x.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0)))
                if (!Need(p, false, out _, out _) && Need(p, true, out _, out _))
                    yield return p;
        }

        /// <summary>A bed for the fitting: a person's (OrderLimb.Bed), or for a beast none (held where it stands).</summary>
        public static Building_Bed Bed(Pawn p) => p != null && p.RaceProps.Humanlike ? OrderLimb.Bed(p) : null;

        public static Pawn BondedTo(Pawn beast) => beast?.relations?.DirectRelations.Where(r => r.def == PawnRelationDefOf.Bond && r.otherPawn != null && !r.otherPawn.Dead).Select(r => r.otherPawn).FirstOrDefault();

        /// <summary>The colony is of tribal means: the surgeon walks in; else drops in by pod.</summary>
        public static bool ByPod => PeopleEvents2.ColonyAge >= 1;
    }

    /// <summary>
    /// The enclave's factor offers {B} ({PET} the beast, when it is one) {TALE} for the {RELATION}, {COUNT} silver now, or on
    /// terms: a fifth down ({DOWN}), the rest in four seasonal instalments asked for by letter.
    /// </summary>
    public class HappeningWorker_EnclaveBody : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Brokers);
            return f != null && !EnclaveTerms.InDefault(f) && !EnclaveTerms.Open(f) && EnclaveBody.Takers(map, true).Any(p => !p.RaceProps.Humanlike || EnclaveBody.Bed(p) != null)
                   && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Brokers);
            if (ctx.faction == null) return false;
            var who = EnclaveBody.Takers(ctx.map, true).FirstOrDefault(p => !p.RaceProps.Humanlike || EnclaveBody.Bed(p) != null);
            if (who == null) return false;
            bool needs = EnclaveBody.Need(who, false, out var recipe, out var part);
            if (!needs && !EnclaveBody.Need(who, true, out recipe, out part)) return false;
            if (who.RaceProps.Humanlike) ctx.b = who;
            else
            {
                ctx.pet = who;
                ctx.b = EnclaveBody.BondedTo(who);
            }
            ctx.a = ctx.b;
            ctx.note = recipe.defName + "|" + (needs ? "need" : "better");
            ctx.tale = recipe.addsHediff.label;
            ctx.relation = part.Label;
            ctx.count = PeopleEvents2Math.BionicPrice(EnclaveBody.Worth(recipe));
            ctx.variant = PeopleEvents2Math.Down(PeopleEvents2Math.TermsTotal(ctx.count));
            ctx.place = EnclaveBody.Bed(who);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Enclave_Factor"), ctx.faction, ext.ages);
            var surgeon = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Enclave_Hand"), ctx.faction, new FloatRange(30f, 60f));
            if (ctx.stranger == null || surgeon == null) return false;
            StorytellerEvents.Skilled(surgeon, SkillDefOf.Medicine, new IntRange(PeopleEvents2Math.SurgeonMin, PeopleEvents2Math.SurgeonMax));
            ctx.group.Add(surgeon);
            ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Enclave_Marshal"), ctx.faction, ext.ages));
            ctx.group.RemoveAll(p => p == null);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.b != null;
        }

        /// <summary>By pod by the colony's trade spot (on foot into a tribal colony), and a stay at the gate.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var party = PeoplesG.Party(ctx);
            if (!EnclaveBody.ByPod || MapFacts.InVacuum(ctx.map)) return Arrivals.Visit(party, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;
            try
            {
                var spot = DropCellFinder.TradeDropSpot(ctx.map);
                DropPodUtility.DropThingsNear(spot, ctx.map, party.Cast<Thing>(), 110, false, false, false, false);
                var chill = PeoplesD.GateSpot(ctx.map) ?? spot;
                LordMaker.MakeNewLord(ctx.faction, new LordJob_VisitOnErrand(ctx.faction, chill, Mathf.RoundToInt(ext.stayHours.RandomInRange * GenDate.TicksPerHour)), ctx.map, party);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the surgeon's pod", e);
                return false;
            }
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(EnclaveBody.ByPod ? "pod" : "walk");
            tags.Add((ctx.note ?? "").EndsWith("better") ? "better" : "need");
            if (ctx.pet != null) tags.Add("beast");
        }

        private static Pawn On(HappeningContext ctx) => ctx.pet ?? ctx.b;

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            var on = On(ctx);
            if (on == null || on.Dead || !on.Spawned || on.Map != ctx.map) return "CC_PEv_PatientGone".Translate().Resolve();
            var r = DefDatabase<RecipeDef>.GetNamedSilentFail((ctx.note ?? "").Split('|')[0]);
            if (r == null || !EnclaveBody.Need(on, (ctx.note ?? "").EndsWith("better"), out var now, out _) || now != r) return "CC_PEv_LimbFitted".Translate().Resolve();
            if (on.RaceProps.Humanlike && EnclaveBody.Bed(on) == null) return "CC_PEv_NoBed".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "paid" : option == 1 ? "terms" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>
    /// The fitting set going: the one it is for laid in the bed (a beast held where it stands), the enclave's surgeon at work
    /// (PeopleWorkOn, Rimfolk_PEv2_Bionic or _BeastBionic); then the game's own surgery (EnclaveBodyDone). <see cref="terms"/>:
    /// the price on terms, the instalments to come.
    /// </summary>
    public class DilemmaEffect_EnclaveBody : DilemmaEffect
    {
        public bool terms;

        public override bool ChanceEach => false;

        public static JobDef Job(Pawn on) => DefDatabase<JobDef>.GetNamedSilentFail(on != null && on.RaceProps.Humanlike ? "Rimfolk_PEv2_Bionic" : "Rimfolk_PEv2_BeastBionic");

        private static Pawn On(HappeningContext hc) => hc?.pet ?? hc?.b;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            var on = On(hc);
            var r = DefDatabase<RecipeDef>.GetNamedSilentFail((hc?.note ?? "").Split('|')[0]);
            if (on == null || r == null) return;
            sheet.Add(ImpactKind.Health, 1, "CC_PEv2_Impact_Bionic".Translate(on.LabelShort, hc.tale ?? "?", hc.relation ?? "?").Resolve(), "CC_PEv2_Impact_BionicBrief".Translate().Resolve(), "pev2bionic");
            int chance = 100;
            if (EnclaveBody.Need(on, (hc.note ?? "").EndsWith("better"), out _, out var part)) chance = OrderLimb.Chance(r, hc.group.FirstOrDefault(), on, part);
            sheet.Add(ImpactKind.Health, -1, "CC_PEv_Impact_LimbRisk".Translate(on.LabelShort, (100 - chance).Named("FAIL"), chance.Named("CHANCE")).Resolve(), "CC_PEv_Impact_LimbRiskBrief".Translate().Resolve(), "pev2bionicrisk");
            if (terms)
            {
                int total = PeopleEvents2Math.TermsTotal(hc.count);
                int each = PeopleEvents2Math.Instalment(total, hc.variant, 1);
                sheet.Add(ImpactKind.Goods, -1, "CC_PEv2_Impact_Terms".Translate(total, hc.variant, each, PeopleEvents2Math.Instalments).Resolve(), "CC_PEv2_Impact_TermsBrief".Translate().Resolve(), "pev2terms");
                sheet.Add(ImpactKind.Goodwill, -1, "CC_PEv2_Impact_Default".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv2_Impact_DefaultBrief".Translate().Resolve(), "pev2default");
            }
            sheet.Play((EnclaveBody.ByPod ? "CC_PEv2_Impact_Pod" : "CC_PEv2_Impact_WalkIn").Translate(on.LabelShort).Resolve(), "CC_PEv2_Impact_PodBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var on = On(hc);
            var surgeon = hc?.group.FirstOrDefault(p => p != null && p.Spawned && p.kindDef?.defName == "Rimfolk_Enclave_Hand") ?? hc?.group.FirstOrDefault(p => p != null && p.Spawned);
            var job = Job(on);
            if (hc?.map == null || on == null || surgeon == null || job == null) return;
            var bed = EnclaveBody.Bed(on);
            string note = hc.note + "|" + (terms ? 1 : 0) + "|" + hc.count + "|" + hc.variant;
            PeopleWorkOn.Begin(hc, surgeon, on, job, bed != null ? new LocalTargetInfo(bed) : LocalTargetInfo.Invalid, note, terms ? hc.variant : hc.count, PeopleEvents2Math.FitTicks, 10f);
        }
    }

    /// <summary>The fitting done: the game's own surgery; the terms written if the colony took them; the gift noted.</summary>
    public class EnclaveBodyDone : PeopleWorkDone
    {
        public override void Done(Visit v, Pawn worker, Pawn on)
        {
            var parts = (PeopleWorkOn.Note(v) ?? "").Split('|');
            var r = DefDatabase<RecipeDef>.GetNamedSilentFail(parts.Length > 0 ? parts[0] : "");
            bool better = parts.Length > 1 && parts[1] == "better";
            bool terms = parts.Length > 2 && parts[2] == "1";
            int price = parts.Length > 3 && int.TryParse(parts[3], out int pr) ? pr : 0;
            int down = parts.Length > 4 && int.TryParse(parts[4], out int dn) ? dn : 0;
            var map = on?.MapHeld ?? v.map;
            var f = worker?.Faction;
            if (r == null || on == null || on.Dead || !EnclaveBody.Need(on, better, out var now, out var part) || now != r) return;
            bool fitted = false;
            try
            {
                r.Worker.ApplyOnPawn(on, part, worker, new List<Thing>(), null);
                fitted = !on.Dead && on.health.hediffSet.hediffs.Any(h => h.def == r.addsHediff && h.Part == part);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the enclave's surgery", e);
            }
            PeopleEventsBook.Gave(EnclaveBody.GiftKind, on);
            if (terms && f != null) EnclaveTerms.Write(f, map, on, r.addsHediff.label, PeopleEvents2Math.TermsTotal(price), down);
            if (fitted)
            {
                if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents2Math.FitGoodwill, false);
                PeoplesG.SayLine(worker, v.incident, "fitted");
                Tell.Message(NewsKind.Health, Importance.Notable, map,
                    (terms ? "CC_PEv2_BionicDoneTerms" : "CC_PEv2_BionicDone").Translate(worker.LabelShort.Named("SURGEON"), on.Named("PAWN"), r.addsHediff.label.Named("PART"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                    on.Spawned ? new LookTargets(on) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            }
            else if (!on.Dead)
            {
                PeoplesG.SayLine(worker, v.incident, "sorry");
                Tell.Message(NewsKind.Health, Importance.Notable, map, "CC_PEv_LimbFailed".Translate(worker.LabelShort.Named("SURGEON"), on.Named("PAWN")).Resolve(),
                    on.Spawned ? new LookTargets(on) : LookTargets.Invalid, MessageTypeDefOf.NegativeHealthEvent);
            }
        }

        public override void GaveUp(Visit v)
        {
            if (v?.map == null) return;
            Tell.Message(NewsKind.Health, Importance.Notable, v.map, "CC_PEv_WorkGivenUp".Translate((v.x?.LabelShort ?? "?").Named("WORKER"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }
    }

    // ===================== The enclave's terms =====================

    /// <summary>
    /// The enclave's terms (item 22): a fifth down, then four instalments, one asked for by letter each season
    /// (Rimfolk_Evt_EnclaveInstalment, fired when it comes due). A missed instalment is a default: their goodwill falls, what
    /// is owed joins the colony's debts in the world's ledger (a debt tie), and no lander of theirs comes (their lander, their
    /// caravans, their visits) until it is paid in full; each season after, their letter asks for it again.
    /// </summary>
    public static class EnclaveTerms
    {
        public const string Kind = "pev:terms";
        public const string Origin = "enclaveTerms";

        private static int Now => Find.TickManager.TicksGame;

        private static List<TermsRecord> All => PeopleEvents2Book.Instance?.terms;

        public static bool Open(Faction f) => All?.Any(t => t != null && !t.done && t.faction == f) ?? false;

        /// <summary>This people holds a default against the colony: no lander until it is paid.</summary>
        public static bool InDefault(Faction f) => f != null && (All?.Any(t => t != null && !t.done && t.defaulted && t.faction == f) ?? false);

        /// <summary>The terms whose letter is due now (one at a time).</summary>
        public static TermsRecord Due() => All?.FirstOrDefault(t => t != null && !t.done && t.due >= 0 && Now >= t.due && t.faction != null && !t.faction.defeated);

        public static TermsRecord Get(int id) => All?.FirstOrDefault(t => t != null && t.id == id);

        /// <summary>The terms written after the fitting: the down payment already paid, the first instalment's letter a season on.</summary>
        public static TermsRecord Write(Faction f, Map map, Pawn on, string part, int total, int down)
        {
            var b = PeopleEvents2Book.Instance;
            if (b == null || f == null) return null;
            var r = b.AddTerms(new TermsRecord
            {
                faction = f, map = map, who = on?.LabelShort, part = part, total = total, down = down, owed = Mathf.Max(0, total - down),
                due = Now + PeopleEvents2Math.InstalmentDays * GenDate.TicksPerDay
            });
            var v = CouncilGameComponent.Instance?.happenings?.AddVisit(Kind, null, map, null, null, null, r.due + 30 * GenDate.TicksPerDay);
            if (v != null) v.stake = r.id;
            return r;
        }

        /// <summary>The terms' hour: when a letter is due, it is sent (when a question may be asked); a people gone or at war ends them.</summary>
        public static bool Hour(Visit v, int now)
        {
            var r = Get(v.stake);
            if (r == null || r.done) return false;
            if (r.faction == null || r.faction.defeated)
            {
                Close(r, Tie.State.Ended);
                return false;
            }
            v.until = Mathf.Max(v.until, (r.due >= 0 ? r.due : now) + 30 * GenDate.TicksPerDay);
            if (r.due < 0 || now < r.due) return true;
            var map = r.map != null && Find.Maps.Contains(r.map) ? r.map : HappeningTracker.HomeMap();
            // Asked by letter: the event is the question; while it waits for an answer the due day stays where it is.
            if (map != null && Tell.CanAsk && PeopleEvents2.Fire("Rimfolk_Evt_EnclaveInstalment", map, r.faction)) r.due = -1;
            return true;
        }

        /// <summary>The instalment of the season (or all that is owed, <paramref name="all"/>) paid: the next due a season on, or the terms kept.</summary>
        public static void Paid(TermsRecord r, int amount, bool all)
        {
            if (r == null || r.done) return;
            r.owed = Mathf.Max(0, r.owed - amount);
            if (!r.defaulted) r.paid++;
            if (all || r.owed <= 0 || r.paid >= PeopleEvents2Math.Instalments)
            {
                Close(r, Tie.State.Kept);
                if (r.faction != null && r.faction.HasGoodwill && !r.faction.IsPlayer) WorldSituationWorker.Goodwill(r.faction, PeopleEvents2Math.PaidUpGoodwill);
                FactionMemory.Note(r.faction, "keptWord", r.part);
                return;
            }
            r.due = Now + PeopleEvents2Math.InstalmentDays * GenDate.TicksPerDay;
        }

        /// <summary>
        /// An instalment missed: a default. Their goodwill falls, the debt joins the colony's debts (a debt tie in the world's
        /// ledger, from the colony to them, for all that is owed), and no lander of theirs comes until it is paid; their
        /// letter comes again next season.
        /// </summary>
        public static void Default(TermsRecord r, Map map)
        {
            if (r == null || r.done) return;
            if (!r.defaulted)
            {
                r.defaulted = true;
                if (r.faction != null && r.faction.HasGoodwill && !r.faction.IsPlayer) WorldSituationWorker.Goodwill(r.faction, PeopleEvents2Math.DefaultGoodwill);
                FactionMemory.Note(r.faction, "contractBroken", r.part);
                try
                {
                    var terms = new TieTerms { goods = "Silver", count = r.owed, key = "bodyTerms" };
                    var t = WorldLedger.AddTie(Tie.Kind.Debt, Refs.Colony(map), Refs.Of(r.faction), terms, Tie.Never, Origin);
                    if (t != null) r.tie = t.id;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 2: the default's debt", e);
                }
            }
            r.due = Now + PeopleEvents2Math.InstalmentDays * GenDate.TicksPerDay;
        }

        private static void Close(TermsRecord r, Tie.State how)
        {
            r.done = true;
            r.due = -1;
            if (r.tie <= 0) return;
            var t = WorldLedger.Get(r.tie);
            if (t != null && t.Live) WorldLedger.Set(t, how);
        }

        /// <summary>Dev mode: the next letter is due now.</summary>
        public static bool DueNow()
        {
            var r = All?.FirstOrDefault(t => t != null && !t.done);
            if (r == null) return false;
            r.due = Now;
            return true;
        }
    }

    /// <summary>
    /// The enclave's letter for an instalment ({COUNT} silver, {OWED} owed in all, {TALE} the part, {B_nameDef} who has it),
    /// or after a default for all that is owed. Fired only when it comes due (EnclaveTerms.Hour).
    /// </summary>
    public class HappeningWorker_EnclaveInstalment : IncidentWorker_Happening
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => EnclaveTerms.Due() != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var r = EnclaveTerms.Due();
            if (r == null) return false;
            ctx.faction = r.faction;
            ctx.variant = r.id;
            ctx.count = r.defaulted ? r.owed : Mathf.Min(r.owed, PeopleEvents2Math.Instalment(r.total, r.down, r.paid + 1));
            // {RELATION} says what is owed in all (the event has no relation of its own to name).
            ctx.relation = r.owed.ToString();
            ctx.tale = r.part;
            ctx.dead = r.who;
            ctx.a = ctx.map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.LabelShort == r.who) ?? PeoplesD.Best(ctx.map, SkillDefOf.Social);
            ctx.placeName = LivingEvents.TownOf(r.faction, ctx.map) ?? r.faction.Name;
            return ctx.count > 0;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var r = EnclaveTerms.Get(ctx.variant);
            tags.Add(r != null && r.defaulted ? "default" : r != null && r.paid + 1 >= PeopleEvents2Math.Instalments ? "last" : "due");
            PeopleEvents.AgeTags(tags, ctx.faction);
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            var r = EnclaveTerms.Get(ctx.variant);
            if (r == null || r.done) return "CC_PEv2_TermsGone".Translate().Resolve();
            if (option == 0 && r.defaulted) return "CC_PEv2_DefaultWhole".Translate().Resolve();
            return null;
        }
    }

    /// <summary>The instalment (or <see cref="all"/> that is owed) carried out to the gate, where their courier takes it; the terms go on, or are kept.</summary>
    public class DilemmaEffect_EnclaveInstalment : DilemmaEffect
    {
        public bool all;

        public override bool ChanceEach => false;

        private static int Amount(DilemmaContext ctx, bool all)
        {
            var r = EnclaveTerms.Get((ctx as HappeningContext)?.variant ?? -1);
            return r == null ? 0 : all ? r.owed : ctx.count;
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            int n = Amount(ctx, all);
            return n > 0 && CampAnswers.Silver(ctx.map) < n ? "CC_Dilemma_NotEnough".Translate(n, ThingDefOf.Silver.label).ToString() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            int n = Amount(ctx, all);
            if (n <= 0) return;
            sheet.Silver(-n);
            sheet.Play("CC_PEv2_Impact_Instalment".Translate(n, ctx.faction?.Name ?? "?").Resolve(), "CC_PEv2_Impact_InstalmentBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var r = EnclaveTerms.Get(hc?.variant ?? -1);
            int n = Amount(ctx, all);
            if (r == null || n <= 0 || hc?.map == null) return;
            // apart: a colonist carries the silver out to the gate, where the enclave's courier takes it.
            new DilemmaEffect_CarryOut { what = "silver", count = n, steward = false, who = PlayWho.None, to = PlaySpot.Gate, label = "CC_PEv2_InstalmentLabel".Translate(r.faction?.Name ?? "?").Resolve() }.Apply(ctx);
            EnclaveTerms.Paid(r, n, all);
        }
    }

    /// <summary>The instalment refused or let go: a default (EnclaveTerms.Default), or the debt left standing another season.</summary>
    public class DilemmaEffect_EnclaveDefault : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var r = EnclaveTerms.Get((ctx as HappeningContext)?.variant ?? -1);
            if (r == null) return;
            if (!r.defaulted && r.faction != null) sheet.Goodwill(r.faction, PeopleEvents2Math.DefaultGoodwill);
            sheet.Add(ImpactKind.Other, -1, "CC_PEv2_Impact_DefaultNow".Translate(r.faction?.Name ?? "?", r.owed).Resolve(), "CC_PEv2_Impact_DefaultBrief".Translate().Resolve(), "pev2defaultnow");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var r = EnclaveTerms.Get((ctx as HappeningContext)?.variant ?? -1);
            if (r != null) EnclaveTerms.Default(r, ctx.map);
        }
    }

    /// <summary>A default unpaid: no trader caravan of that people comes (they come by lander, and no lander comes).</summary>
    [HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), nameof(IncidentWorker_TraderCaravanArrival.FactionCanBeGroupSource))]
    public static class Patch_EnclaveDefaultCaravans
    {
        public static void Postfix(Faction f, ref bool __result)
        {
            if (!__result || !RimfolkPatches.On) return;
            try
            {
                if (EnclaveTerms.InDefault(f)) __result = false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: a default's caravans", e);
            }
        }
    }
}
