using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 69. The organizer's evening (the Deepwell Union) =====================

    /// <summary>
    /// The organizer's evening (item 69): when the colony's people are worn down (the heart's strain of the colony at forty or more, or
    /// the long hours in force by decree), a shift-captain of the Deepwell Union comes to the gate at dusk and asks to speak at the
    /// colony's fire about how the union keeps its shifts. Let in, they speak there that evening (a gathering at the fire, the captain
    /// its guest; with the second answer they are given supper too). Next morning the colonist under the most strain brings a petition
    /// to the council, through the council's own petitions (Decrees/Decrees.cs): to lift the long hours when they are in force, else for
    /// the days of rest (the council's own decree; a council keeps no weekly calendar, so the words say "days of rest"). Granted while it
    /// stands, those under strain are glad of it and the union thinks better of the colony; refused, or left to lapse, those under strain
    /// are stung. Only with the heart's strain or the long hours to read, and only for a decree the council could grant.
    /// </summary>
    public static class OrganizersEvening
    {
        public const string EventDef = "Rimfolk_Evt_OrganizersEvening";

        private static int Now => Find.TickManager.TicksGame;

        public static DecreeDef LongHours => DefDatabase<DecreeDef>.GetNamedSilentFail("Council_Decree_LongHours");

        public static DecreeDef Rest => DefDatabase<DecreeDef>.GetNamedSilentFail("Council_Decree_Rest");

        private static List<Pawn> People(Map map) =>
            map?.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && CouncilGameComponent.CanVoiceWishes(p)).ToList() ?? new List<Pawn>();

        /// <summary>The long hours in force for this home's colony.</summary>
        public static bool LongHoursOn(Map map)
        {
            var d = LongHours;
            return d != null && (DecreeTracker.For(map)?.IsActive(d) ?? false);
        }

        /// <summary>The colony's people at this home are worn down: the heart's strain of the colony, or the long hours.</summary>
        public static bool Worn(Map map)
        {
            var people = People(map);
            if (people.Count == 0) return false;
            return PeopleEvents7Math.Worn(Heart.On ? Heart.ColonyStrain(people) : 0f, LongHoursOn(map));
        }

        /// <summary>The petition they would bring: to lift the long hours, else for days of rest; null when the council could grant neither.</summary>
        public static DecreeDef Petition(Map map, out bool repeal)
        {
            repeal = false;
            var t = DecreeTracker.For(map);
            if (t == null) return null;
            if (LongHoursOn(map))
            {
                repeal = true;
                return LongHours;
            }
            var rest = Rest;
            return rest != null && !t.IsActive(rest) && t.CannotEnact(rest, true) == null ? rest : null;
        }

        /// <summary>The colonist under the most strain (the heart's), else anyone who may speak to the council.</summary>
        public static Pawn MostStrained(Map map) => People(map).OrderByDescending(Heart.StrainOf).ThenBy(p => p.needs?.mood?.CurLevel ?? 1f).FirstOrDefault();

        /// <summary>The events' hour at a home: at dusk, once a day, people worn down and a petition the council could grant, the captain comes at a chance.</summary>
        public static void Watch(Map map, int now)
        {
            var b = PeopleEvents7Book.Instance;
            if (b == null || GenLocalDate.HourOfDay(map) != 16) return;
            if (b.lastEvening >= 0 && now - b.lastEvening < PeopleEvents7Math.EveningGapDays * GenDate.TicksPerDay) return;
            if (b.evenings.Any(r => r != null && !r.Done && r.map == map)) return;
            var f = PeoplesD.Friendly(PeoplesMath.Drillers);
            if (f == null || !Tell.CanAsk || MapFacts.ThreatOn(map) || !Worn(map) || Petition(map, out _) == null) return;
            if (!Rand.Chance(PeopleEvents7Math.EveningChance * StoryDirection.Factor(StoryWaysMath.Visitors))) return;
            if (PeopleEvents2.Fire(EventDef, map, f)) b.lastEvening = now;
        }

        /// <summary>They spoke at the fire (the answer's own gathering): the petition is noted for the morning.</summary>
        public static void Spoke(HappeningContext ctx)
        {
            var map = ctx?.map;
            var b = PeopleEvents7Book.Instance;
            if (map == null || b == null) return;
            var def = Petition(map, out bool repeal);
            if (def == null) return;
            b.AddEvening(new EveningRecord { map = map, faction = ctx.faction, decree = def.defName, repeal = repeal, spoke = Now });
        }

        /// <summary>
        /// The events' hour: the morning after an evening, the petition brought to the council by the one under the most strain; while
        /// it stands, granted (the decree declared, or the long hours lifted) or refused (it lapsed, or the council let its days pass).
        /// </summary>
        public static void Petitions(int now)
        {
            var b = PeopleEvents7Book.Instance;
            if (b == null) return;
            foreach (var r in b.evenings.Where(r => r != null && !r.Done).ToList())
            {
                var map = r.map;
                var t = DecreeTracker.For(map);
                var def = r.decree.NullOrEmpty() ? null : DefDatabase<DecreeDef>.GetNamedSilentFail(r.decree);
                if (map == null || !Find.Maps.Contains(map) || t == null || def == null)
                {
                    r.stage = "done";
                    continue;
                }
                if (r.stage == "spoke")
                {
                    if (now - r.spoke < 8 * GenDate.TicksPerHour || !PeopleEvents7Math.Morning(GenLocalDate.HourOfDay(map))) continue;
                    if (now - r.spoke > 2 * GenDate.TicksPerDay)
                    {
                        r.stage = "done";
                        continue;
                    }
                    Ask(r, t, def, map, now);
                    continue;
                }
                bool granted = r.repeal ? !t.IsActive(def) : t.IsActive(def);
                if (granted)
                {
                    Answered(r, map, true);
                    continue;
                }
                var req = t.RequestFor(def);
                if (req == null || req.repeal != r.repeal || now - r.asked > PeopleEvents7Math.PetitionDays * GenDate.TicksPerDay) Answered(r, map, false);
            }
        }

        /// <summary>The petition brought: through the council's own petitions, with a line of the one who brings it; told.</summary>
        private static void Ask(EveningRecord r, DecreeTracker t, DecreeDef def, Map map, int now)
        {
            var asker = MostStrained(map);
            if (asker == null)
            {
                r.stage = "done";
                return;
            }
            r.asker = asker;
            r.asked = now;
            r.stage = "asked";
            if (r.repeal)
            {
                if (!t.IsActive(def))
                {
                    r.stage = "done";
                    return;
                }
                if (t.RequestFor(def) == null)
                {
                    string gripe = def.oppose.Pick(ToneUtility.ToneFor(asker), Flavor.TagsFor(asker), asker);
                    gripe = gripe.NullOrEmpty() ? null : gripe.Formatted(asker.Named("PAWN")).Resolve();
                    t.requests.Add(new DecreeRequest { def = def, pawn = asker, tick = now, line = gripe, repeal = true });
                    Tell.Message(NewsKind.Colony, Importance.Notable, map, "CC_PEv7_PetitionLift".Translate(asker.Named("PAWN"), def.label.Named("DECREE")).Resolve(), new LookTargets(asker),
                        MessageTypeDefOf.NeutralEvent);
                    // Their own line for the council, said where they stand, as the council's own petitions are (Decrees/Decrees.cs).
                    CouncilGameComponent.Speak(asker, gripe, new Color(0.95f, 0.6f, 0.5f));
                }
                return;
            }
            if (t.Petition(def, asker) == null)
            {
                r.stage = "done";
                return;
            }
            Tell.Message(NewsKind.Colony, Importance.Notable, map, "CC_PEv7_PetitionRest".Translate(asker.Named("PAWN"), def.label.Named("DECREE")).Resolve(), new LookTargets(asker),
                MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>The petition answered: those under strain glad of it or stung; the union's thanks when granted; told.</summary>
        private static void Answered(EveningRecord r, Map map, bool granted)
        {
            r.stage = "done";
            var strained = People(map).Where(p => p == r.asker || Heart.On && Heart.StrainOf(p) >= PeopleEvents7Math.StrainedOne).ToList();
            foreach (var p in strained) PeopleEvents7.Thought(p, granted ? "Rimfolk_Thought_PetitionHeard" : "Rimfolk_Thought_PetitionTurnedAway");
            if (granted) PeopleEvents7.Goodwill(r.faction, PeopleEvents7Math.HeardGoodwill, GoodwillWhy.Council);
            Tell.Message(NewsKind.Colony, Importance.Notable, map,
                (granted ? "CC_PEv7_PetitionGranted" : "CC_PEv7_PetitionRefused").Translate((r.asker?.LabelShort ?? "?").Named("PAWN"), (r.faction?.Name ?? "?").Named("FACTION"),
                    strained.Count.Named("COUNT")).Resolve(), r.asker != null && r.asker.Spawned ? new LookTargets(r.asker) : LookTargets.Invalid,
                granted ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>Dev mode: the captain at the gate now (the people worn down or not), or the morning's petition now.</summary>
        public static string Now_(Map map)
        {
            var b = PeopleEvents7Book.Instance;
            var f = PeoplesD.Friendly(PeoplesMath.Drillers);
            if (b == null || map == null || f == null) return "no Deepwell Union at peace.";
            var open = b.evenings.FirstOrDefault(r => r != null && r.stage == "spoke" && r.map == map);
            if (open != null)
            {
                open.spoke = Now - GenDate.TicksPerDay;
                var t = DecreeTracker.For(map);
                var def = DefDatabase<DecreeDef>.GetNamedSilentFail(open.decree);
                if (t != null && def != null) Ask(open, t, def, map, Now);
                return "the petition is brought now (if the council could grant it).";
            }
            if (Petition(map, out bool repeal) == null) return "no petition the council could grant (the days of rest need the heart's strain and its cooldown done; or the long hours in force).";
            b.lastEvening = Now;
            return PeopleEvents2.Fire(EventDef, map, f) ? "a shift-captain of " + f.Name + " comes at dusk (" + (repeal ? "the long hours" : "days of rest") + ")." : "they could not come now.";
        }
    }

    /// <summary>A shift-captain of {FACTION} ({X}) at the gate at dusk: the colony's people look worn down ({TALE}), and they ask to speak at its fire tonight.</summary>
    public class HappeningWorker_OrganizersEvening : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Drillers) != null && OrganizersEvening.Petition(map, out _) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Drillers);
            if (ctx.faction == null || OrganizersEvening.Petition(ctx.map, out bool repeal) == null) return false;
            ctx.note = repeal ? "long" : "rest";
            ctx.tale = (repeal ? "CC_PEv7_WornLong" : "CC_PEv7_WornStrain").Translate().Resolve();
            ctx.a = OrganizersEvening.MostStrained(ctx.map) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Deepwell_Captain"), ctx.faction, ext.ages);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.stranger != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.note == "long" ? "longhours" : "strain");
            if (UnionStrike.Striking(ctx.faction)) tags.Add("striking");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "speak" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The evening noted for the morning's petition (OrganizersEvening.Spoke): what it is for and what follows each way.</summary>
    public class DilemmaEffect_OrganizerSpoke : DilemmaEffect
    {
        public bool supper;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            var def = OrganizersEvening.Petition(hc.map, out bool repeal);
            if (def == null) return;
            sheet.Add(ImpactKind.Other, 0, (repeal ? "CC_PEv7_Impact_PetitionLift" : "CC_PEv7_Impact_PetitionRest").Translate(def.label).Resolve(), "CC_PEv7_Impact_PetitionBrief".Translate().Resolve(),
                "pev7petition");
            sheet.Add(ImpactKind.Mood, 0, "CC_PEv7_Impact_PetitionEither".Translate().Resolve(), "CC_PEv7_Impact_PetitionEitherBrief".Translate().Resolve(), "pev7petitioneither");
            if (supper && hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents7Math.SupperGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            OrganizersEvening.Spoke(hc);
            if (supper) PeopleEvents7.Goodwill(hc?.faction, PeopleEvents7Math.SupperGoodwill, GoodwillWhy.Feast);
        }
    }
}
