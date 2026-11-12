using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 59. The guild's mark (the Kiln Guild) =====================

    /// <summary>What the master would make of a weapon: its unique kind, or a masterwork (in better stuff for a blade).</summary>
    public class MarkPlan
    {
        /// <summary>"unique" or "masterwork".</summary>
        public string kind;
        public ThingDef def;
        /// <summary>The weapon's stuff after (a blade's better stuff), else its own.</summary>
        public ThingDef stuff;
        /// <summary>What the colony gives besides silver: the better stuff for a blade, else the weapon's own main material, half of it.</summary>
        public ThingDef pay;
        public int payCount;
        public int price;
    }

    /// <summary>
    /// The guild's mark (item 59): a master-smith of the Kiln Guild reworks one colonist's bow or gun into a unique piece of its
    /// kind (Odyssey's and Vanilla Weapons Expanded's unique weapons, found by name: "Bow_Great" to "Bow_Great_Unique"; their
    /// traits are the game's own), or a blade into a masterwork in better stuff (else a masterwork of what it is). The
    /// colonist's name goes into its label (the unique piece's own name; a blade's label, Patch_GuildMarkLabel). A day at the
    /// colony's bench while the colonist watches (PeopleWorkOn at the place), paid in silver and the stuff. A unique greatbow
    /// or crossbow in the medieval age, rifles and charge guns after.
    /// </summary>
    public static class GuildMark
    {
        public const string GiftKind = "guildmark";

        /// <summary>The master's plan for this weapon, or null when there is nothing he would do for it.</summary>
        public static MarkPlan Plan(Thing w, Map map)
        {
            if (w?.def == null || !w.def.IsWeapon || w.def.IsStuff) return null;
            try
            {
                float before = w.MarketValue;
                // A bow or a gun of a kind that has a unique piece (with Odyssey, whose traits make it one).
                if (w.def.IsRangedWeapon && ModsConfig.OdysseyActive && w.TryGetComp<CompUniqueWeapon>() == null)
                {
                    var u = DefDatabase<ThingDef>.GetNamedSilentFail(PeopleEvents2Math.UniqueOf(w.def.defName) ?? "");
                    if (u != null && u.HasComp(typeof(CompUniqueWeapon)) && !u.MadeFromStuff)
                    {
                        var (pay, n) = Makings(w.def, null);
                        return new MarkPlan { kind = "unique", def = u, pay = pay, payCount = n, price = PeopleEvents2Math.MarkPrice(before, u.BaseMarketValue * 1.6f) };
                    }
                }
                var q = w.TryGetComp<CompQuality>();
                if (q == null) return null;
                // A blade in better stuff the colony has enough of.
                if (w.def.IsMeleeWeapon && w.def.MadeFromStuff && w.Stuff != null)
                {
                    var stat = w.def.tools?.Any(t => t?.capacities?.Any(c => c?.defName == "Cut" || c?.defName == "Stab") == true) == true ? StatDefOf.SharpDamageMultiplier : StatDefOf.BluntDamageMultiplier;
                    float now = w.Stuff.GetStatValueAbstract(stat);
                    int need = Mathf.Max(1, w.def.CostStuffCount);
                    var better = GenStuff.AllowedStuffsFor(w.def).Where(s => s != w.Stuff && PeopleEvents2Math.Better(now, s.GetStatValueAbstract(stat)) && PlayCarry.Have(map, s.defName, s) >= need)
                        .OrderByDescending(s => s.GetStatValueAbstract(stat)).FirstOrDefault();
                    if (better != null)
                        return new MarkPlan
                        {
                            kind = "masterwork", def = w.def, stuff = better, pay = better, payCount = need,
                            price = PeopleEvents2Math.MarkPrice(before, w.def.GetStatValueAbstract(StatDefOf.MarketValue, better) * 2.5f)
                        };
                }
                if (q.Quality >= QualityCategory.Masterwork) return null;
                var (p2, n2) = Makings(w.def, w.Stuff);
                return new MarkPlan { kind = "masterwork", def = w.def, stuff = w.Stuff, pay = p2, payCount = n2, price = PeopleEvents2Math.MarkPrice(before, w.def.GetStatValueAbstract(StatDefOf.MarketValue, w.Stuff) * 2.5f) };
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the guild's plan", e);
                return null;
            }
        }

        /// <summary>The weapon's main material, half of it (at least ten): its stuff, else the largest of its makings.</summary>
        private static (ThingDef, int) Makings(ThingDef def, ThingDef stuff)
        {
            if (stuff != null && def.CostStuffCount > 0) return (stuff, Mathf.Max(10, def.CostStuffCount / 2));
            var main = def.CostList?.OrderByDescending(c => c.count).FirstOrDefault(c => c?.thingDef != null);
            return main == null ? (ThingDefOf.Steel, 20) : (main.thingDef, Mathf.Max(10, main.count / 2));
        }

        /// <summary>The colony's people whose weapon the master would rework, with a bench for it, never one already marked.</summary>
        public static IEnumerable<Pawn> Takers(Map map) =>
            PeopleEvents.Takers(map, GiftKind).Where(p => p.equipment?.Primary != null && Plan(p.equipment.Primary, map) != null && WanderYear.Bench(map, p) != null);

        /// <summary>The colony's best crafter but the one whose weapon it is, to stand at the master's side.</summary>
        public static Pawn Crafter(Map map, Pawn not) => PeoplesD.Best(map, SkillDefOf.Crafting, p => p != not && !p.WorkTypeIsDisabled(WorkTypeDefOf.Crafting));

        // ---------------- The names in blades' labels ----------------

        private static Dictionary<int, string> names;

        /// <summary>The record changed: the names are read again from it.</summary>
        public static void Forget() => names = null;

        /// <summary>"Alder's " for a blade the guild marked with Alder's name, else null.</summary>
        public static string NameOf(int thingId)
        {
            if (names == null)
            {
                names = new Dictionary<int, string>();
                var marks = PeopleEvents2Book.Instance?.marks;
                if (marks != null)
                    foreach (var m in marks)
                    {
                        int colon = m.IndexOf(':');
                        if (colon > 0 && int.TryParse(m.Substring(0, colon), out int id)) names[id] = "CC_PEv2_MarkPrefix".Translate(m.Substring(colon + 1).Named("PAWN")).Resolve();
                    }
            }
            return names.Count == 0 ? null : names.TryGetValue(thingId, out var s) ? s : null;
        }

        /// <summary>
        /// The work done: the old piece gone, the new made (the game's unique piece with its own traits and name, or a masterwork
        /// in its stuff), the colonist's name put in it, and it handed to them.
        /// </summary>
        public static Thing Remake(Thing old, MarkPlan plan, Pawn owner)
        {
            if (old == null || plan?.def == null || owner == null) return null;
            ThingWithComps made;
            try
            {
                made = (ThingWithComps)ThingMaker.MakeThing(plan.def, plan.def.MadeFromStuff ? plan.stuff ?? GenStuff.DefaultStuffFor(plan.def) : null);
                if (plan.kind == "masterwork") made.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Masterwork, ArtGenerationContext.Outsider);
                var u = made.TryGetComp<CompUniqueWeapon>();
                if (u != null)
                {
                    var field = AccessTools.Field(typeof(CompUniqueWeapon), "name");
                    string was = field?.GetValue(u) as string;
                    string named = "CC_PEv2_MarkUnique".Translate(owner.LabelShort.Named("PAWN"), (was.NullOrEmpty() ? plan.def.label : was).Named("NAME")).Resolve();
                    field?.SetValue(u, named);
                    var art = made.TryGetComp<CompArt>();
                    if (art != null) art.Title = named;
                }
                else PeopleEvents2Book.Instance?.AddMark(made.thingIDNumber, owner.LabelShort);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the guild's work", e);
                return null;
            }
            if (!old.Destroyed) old.Destroy();
            try
            {
                if (owner.Spawned && !owner.Dead && owner.equipment != null && owner.equipment.Primary == null) owner.equipment.AddEquipment(made);
                else if (owner.Spawned) GenPlace.TryPlaceThing(made, owner.Position, owner.Map, ThingPlaceMode.Near);
                else if (owner.MapHeld != null) GenPlace.TryPlaceThing(made, owner.PositionHeld, owner.MapHeld, ThingPlaceMode.Near);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the piece handed back", e);
            }
            return made;
        }
    }

    /// <summary>
    /// A master-smith of {FACTION} at the gate offers to rework {A_nameDef}'s {TALE} ({COUNT} silver and {THING}), or with
    /// {B_nameDef}, the colony's best crafter, at the master's side for less ({VARIANT}... told on the card).
    /// </summary>
    public class HappeningWorker_GuildMark : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Artisans) != null && GuildMark.Takers(map).Any() && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Artisans);
            if (ctx.faction == null) return false;
            ctx.a = GuildMark.Takers(ctx.map).OrderByDescending(p => p.equipment.Primary.MarketValue).FirstOrDefault();
            var w = ctx.a?.equipment?.Primary;
            var plan = GuildMark.Plan(w, ctx.map);
            if (plan == null) return false;
            ctx.b = GuildMark.Crafter(ctx.map, ctx.a);
            ctx.tale = w.LabelNoCount;
            ctx.thing = plan.pay;
            ctx.count = plan.price;
            ctx.variant = PeopleEvents2Math.WithCrafter(plan.price);
            // {RELATION} says what it would become (the event has no relation of its own to name).
            ctx.relation = plan.kind == "unique" ? "CC_PEv2_MarkAsUnique".Translate(plan.def.label).Resolve()
                : plan.stuff != null && plan.stuff != w.Stuff ? "CC_PEv2_MarkAsStuff".Translate(plan.stuff.label).Resolve()
                : "CC_PEv2_MarkAsMaster".Translate().Resolve();
            ctx.note = plan.kind + "|" + plan.def.defName + "|" + (plan.stuff?.defName ?? "") + "|" + (plan.pay?.defName ?? "") + "|" + plan.payCount + "|" + w.thingIDNumber;
            ctx.place = WanderYear.Bench(ctx.map, ctx.a);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_KilnGuild_Guildmaster"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Crafting, new IntRange(14, 18));
            ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_KilnGuild_Apprentice"), ctx.faction, new FloatRange(16f, 24f)));
            ctx.group.RemoveAll(p => p == null);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.place != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            var bits = (ctx.note ?? "").Split('|');
            tags.Add(bits[0]);
            var w = ctx.a?.equipment?.Primary;
            // A bow or a crossbow (a ranged piece of the medieval age or before), a gun, or a blade.
            if (w != null) tags.Add(!w.def.IsRangedWeapon ? "blade" : w.def.techLevel <= TechLevel.Medieval ? "bow" : "gun");
            if (ctx.b == null) tags.Add("nocrafter");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            var bits = (ctx.note ?? "").Split('|');
            int id = bits.Length > 5 && int.TryParse(bits[5], out int i) ? i : -1;
            if (ctx.a == null || ctx.a.Dead || !ctx.a.Spawned || ctx.a.Map != ctx.map) return "CC_PEv2_OwnerGone".Translate().Resolve();
            if (ctx.a.equipment?.Primary == null || ctx.a.equipment.Primary.thingIDNumber != id) return "CC_PEv2_WeaponGone".Translate(ctx.a.LabelShort.Named("PAWN")).Resolve();
            if (WanderYear.Bench(ctx.map, ctx.a) == null) return "CC_PeoplesE_NoBench".Translate(ctx.tale ?? "?").Resolve();
            if (option == 1 && ctx.b == null) return "CC_PEv2_NoCrafter".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "work" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>
    /// The rework set going: the silver and the stuff carried out to the master, the colonist's weapon handed to him, the
    /// colonist (and with <see cref="crafter"/> the colony's best crafter, {B}) beside the bench, the day's work there
    /// (Rimfolk_PEv2_Mark), then the piece made (GuildMarkDone).
    /// </summary>
    public class DilemmaEffect_GuildMark : DilemmaEffect
    {
        public bool crafter;

        public override bool ChanceEach => false;

        public static JobDef Job => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv2_Mark");

        private static int PayCount(HappeningContext hc)
        {
            var bits = (hc?.note ?? "").Split('|');
            return bits.Length > 4 && int.TryParse(bits[4], out int n) ? n : 0;
        }

        private int Price(HappeningContext hc) => crafter ? hc.variant : hc.count;

        public override string Unavailable(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return null;
            int price = Price(hc);
            if (CampAnswers.Silver(ctx.map) < price) return "CC_Dilemma_NotEnough".Translate(price, ThingDefOf.Silver.label).ToString();
            int n = PayCount(hc);
            if (hc.thing != null && n > 0 && PlayCarry.Have(ctx.map, hc.thing.defName, hc.thing) < n) return "CC_Dilemma_NotEnough".Translate(n, hc.thing.label).ToString();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null) return;
            sheet.Silver(-Price(hc));
            if (hc.thing != null && PayCount(hc) > 0) sheet.Goods(-1, hc.thing, PayCount(hc));
            bool unique = (hc.note ?? "").StartsWith("unique");
            sheet.Add(ImpactKind.Goods, 1, (unique ? "CC_PEv2_Impact_MarkUnique" : "CC_PEv2_Impact_MarkMaster").Translate(hc.a.LabelShort, hc.tale ?? "?", hc.relation ?? "?").Resolve(),
                "CC_PEv2_Impact_MarkBrief".Translate().Resolve(), "pev2mark");
            sheet.Play("CC_PEv2_Impact_MarkBench".Translate(hc.a.LabelShort, hc.stranger?.LabelShort ?? "?").Resolve(), "CC_PEv2_Impact_MarkBenchBrief".Translate().Resolve());
            if (crafter && hc.b != null) sheet.Add(ImpactKind.Skill, 1, "CC_PEv2_Impact_MarkCrafter".Translate(hc.b.LabelShort).Resolve(), "CC_PEv2_Impact_MarkCrafterBrief".Translate().Resolve(), "pev2markcrafter");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = hc?.map;
            var x = hc?.stranger;
            var a = hc?.a;
            var w = a?.equipment?.Primary;
            var bench = WanderYear.Bench(map, a) ?? hc?.place;
            if (map == null || x == null || a == null || w == null || bench == null || Job == null) return;
            int price = Price(hc);
            // apart: a colonist carries the silver and the stuff out to the master at the gate, who takes them.
            new DilemmaEffect_CarryOut { what = "silver", count = price, steward = false, who = PlayWho.None, label = "CC_PEv2_MarkSilverLabel".Translate(x.LabelShort.Named("X")).Resolve() }.Apply(ctx);
            int n = PayCount(hc);
            // apart: the stuff for the work, carried out the same way.
            if (hc.thing != null && n > 0)
                new DilemmaEffect_CarryOut { what = hc.thing.defName, count = n, steward = false, who = PlayWho.None, label = "CC_PEv2_MarkStuffLabel".Translate(x.LabelShort.Named("X")).Resolve() }.Apply(ctx);
            // The weapon handed over to the master for the day.
            try
            {
                a.equipment.Remove(w);
                if (!x.inventory.innerContainer.TryAdd(w)) GenPlace.TryPlaceThing(w, a.Position, map, ThingPlaceMode.Near);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the weapon handed over", e);
            }
            if (crafter && hc.b != null && PlayOuts.On && PeoplesG.Up(hc.b, map)) PeopleWorkOn.Bring(hc.b, bench, "beside", PeopleEvents2Math.MarkTicks + 2 * GenDate.TicksPerHour);
            string note = hc.note + "|" + (crafter ? hc.b?.thingIDNumber ?? -1 : -1);
            PeopleWorkOn.Begin(hc, x, a, Job, new LocalTargetInfo(bench), note, price, PeopleEvents2Math.MarkTicks, 16f);
        }
    }

    /// <summary>The day's work done: the piece made and handed back (GuildMark.Remake), the crafter's lesson, the guild's word.</summary>
    public class GuildMarkDone : PeopleWorkDone
    {
        private static (MarkPlan plan, int weapon, int crafter) Read(Visit v)
        {
            var bits = (PeopleWorkOn.Note(v) ?? "").Split('|');
            if (bits.Length < 7) return (null, -1, -1);
            var plan = new MarkPlan
            {
                kind = bits[0], def = DefDatabase<ThingDef>.GetNamedSilentFail(bits[1]), stuff = DefDatabase<ThingDef>.GetNamedSilentFail(bits[2]), pay = DefDatabase<ThingDef>.GetNamedSilentFail(bits[3])
            };
            return (plan, int.TryParse(bits[5], out int w) ? w : -1, int.TryParse(bits[6], out int c) ? c : -1);
        }

        private static Thing Held(Pawn worker, int id) => worker?.inventory?.innerContainer?.FirstOrDefault(t => t.thingIDNumber == id);

        public override void Done(Visit v, Pawn worker, Pawn on)
        {
            var (plan, id, crafterId) = Read(v);
            var old = Held(worker, id);
            if (plan?.def == null || old == null || on == null || on.Dead) return;
            worker.inventory.innerContainer.Remove(old);
            var made = GuildMark.Remake(old, plan, on);
            var map = on.MapHeld ?? v.map;
            var crafter = map?.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.thingIDNumber == crafterId);
            if (crafter != null)
            {
                crafter.skills?.Learn(SkillDefOf.Crafting, PeopleEvents2Math.CrafterXp);
                PeopleWorkOn.Release(crafter);
            }
            PeopleEventsBook.Gave(GuildMark.GiftKind, on);
            var f = worker.Faction;
            if (made == null) return;
            if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents2Math.MarkGoodwill, false);
            PeoplesG.SayLine(worker, v.incident, "marked");
            Tell.Message(NewsKind.People, Importance.Notable, map,
                "CC_PEv2_MarkDone".Translate(worker.LabelShort.Named("SMITH"), on.Named("PAWN"), made.LabelNoCount.Named("PIECE"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                on.Spawned ? new LookTargets(on) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, "CC_PEv2_MarkChronicle".Translate(on.Named("PAWN"), made.LabelNoCount.Named("PIECE"), (f?.Name ?? "?").Named("FACTION")).Resolve(), on));
        }

        /// <summary>Given up: the weapon handed back where its owner is (or the gate), the silver back with the master's people.</summary>
        public override void GaveUp(Visit v)
        {
            var (_, id, _) = Read(v);
            var worker = v?.x;
            var old = Held(worker, id);
            var owner = v?.a;
            if (old != null)
            {
                worker.inventory.innerContainer.Remove(old);
                var at = owner != null && owner.Spawned ? owner.Position : worker != null && worker.Spawned ? worker.Position : IntVec3.Invalid;
                var map = owner?.MapHeld ?? worker?.MapHeld ?? v?.map;
                if (map != null && at.IsValid) GenPlace.TryPlaceThing(old, at, map, ThingPlaceMode.Near);
            }
            if (v?.map == null) return;
            Tell.Message(NewsKind.People, Importance.Notable, v.map, "CC_PEv2_MarkGivenUp".Translate((worker?.LabelShort ?? "?").Named("SMITH"), (owner?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                owner != null && owner.Spawned ? new LookTargets(owner) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }
    }

    /// <summary>A blade the guild marked carries its colonist's name in its label ("Alder's longsword").</summary>
    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.LabelNoCount), MethodType.Getter)]
    public static class Patch_GuildMarkLabel
    {
        public static void Postfix(ThingWithComps __instance, ref string __result)
        {
            if (!RimfolkPatches.On || __instance == null) return;
            try
            {
                string name = GuildMark.NameOf(__instance.thingIDNumber);
                if (name != null) __result = name + " " + __result;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: a marked blade's name", e);
            }
        }
    }
}
